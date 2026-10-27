using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SudsTycoon
{
    /// <summary>Mud splats and foam glued to a car; they grow in, wobble, and shrink away when cleaned.</summary>
    public class Sticker
    {
        public Transform tr;
        public Vector3 baseScale;
        public float k, target = 1f, wob;
        public bool dead;
    }

    /// <summary>Particles, stickers and flying objects (port of fx.js). All positions are web coordinates.</summary>
    public class Fx : MonoBehaviour
    {
        public static Fx I { get; private set; }
        ParticleSystem foam, colorFoam, bubble, water, sparkle, confetti, smoke, wind, coin, glow, debris;
        readonly List<Sticker> stickers = new List<Sticker>();
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly Stack<Transform> bundlePool = new Stack<Transform>();
        readonly List<Pop> pops = new List<Pop>();
        class Pop { public Transform tr; public Vector3 target; public float t; }
        Material[] mudMats;
        Material coatMat;
        readonly Dictionary<Color, Material> coatTints = new Dictionary<Color, Material>();
        static readonly uint[] Mud = { 0x6b4a2b, 0x7a5532, 0x8b6a3e, 0x5e3f22, 0x7d5a35 };

        class Flyer
        {
            public Transform obj; public Vector3 from; public Func<Vector3> to; public float dur, arc, t, spin, scaleFrom, scaleTo = -1;
            public Action<Transform> done;
        }

        void Awake()
        {
            I = this;
            var white = Art.Make(Color.white, null, true);
            var ground = new GameObject("FxGround").transform; ground.SetParent(transform, false); ground.position = new Vector3(0, 0.06f, 0);
            AnimationCurve popCurve(float peakAt) => new AnimationCurve(new Keyframe(0, 0), new Keyframe(peakAt, 1, 0, 0), new Keyframe(1, 0));
            foam = Make("Foam", Art.Ico, white, 600, 0.08f, popCurve(0.25f), 1.5f);
            colorFoam = Make("ColorFoam", Art.Ico, white, 300, 0.08f, popCurve(0.25f), 1.5f);
            bubble = Make("Bubbles", Art.Ico, Art.Make(new Color(0.8f, 0.93f, 1f, 0.55f), null, false, false, true), 200, 0f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.2f, 1), new Keyframe(1, 0.9f)), 0.6f);
            water = Make("Water", Art.Ico, Art.Make(U.Hex(0x7fd0ff), null, true, true), 700, 1.22f, AnimationCurve.Linear(0, 1, 1, 0.3f), 0f, ground);
            sparkle = Make("Sparkle", null, Art.Decal(Art.Tex("sparkle"), U.Hex(0xfff3a0), true), 300, 0f, popCurve(0.3f), 0f);
            confetti = Make("Confetti", Art.Cube, white, 500, 0.92f, AnimationCurve.Linear(0, 1, 1, 0.6f), 1.4f, ground);
            smoke = Make("Smoke", Art.Ico, Art.Make(U.Hex(0xd9dde4), null, true), 160, 0f, popCurve(0.2f), 2f);
            wind = Make("Wind", Art.Cube, Art.Make(new Color(1f, 0.97f, 0.82f, 0.7f), null, true, true, true), 260, 0f, AnimationCurve.Linear(0, 1, 1, 0.5f), 0f);
            var coinPrefab = Art.Prefab("Items/coin");
            var coinMesh = coinPrefab ? coinPrefab.GetComponentInChildren<MeshFilter>()?.sharedMesh : null;
            coin = Make("Coins", coinMesh ? coinMesh : Art.Cylinder, Art.Make(Color.white, null, coinMesh != null), 200, 1.43f, AnimationCurve.Linear(0, 1, 1, 0.7f), 0f, ground);
            debris = Make("Debris", Art.Ico, white, 500, 1.0f, AnimationCurve.Linear(0, 1, 1, 0), 0f, ground);
            glow = Make("Glow", null, Art.Decal(Art.Tex("glow"), U.Hex(0xffe9a0), true), 60, 0f, popCurve(0.2f), 0f);
            mudMats = new Material[Mud.Length];
            for (int i = 0; i < Mud.Length; i++) mudMats[i] = Art.Solid(U.Hex(Mud[i]));
            coatMat = Art.Solid(Color.white);
        }

        ParticleSystem Make(string name, Mesh mesh, Material mat, int max, float gravity, AnimationCurve size, float drag, Transform floor = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startRotation3D = mesh != null;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, size);
            if (drag > 0f) { var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.drag = drag; lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false; lim.limit = 1000f; }
            if (floor) { var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.Planes; col.SetPlane(0, floor); col.bounce = 0.3f; col.dampen = 0.4f; col.lifetimeLoss = 0f; }
            if (mesh != null) { var rol = ps.rotationOverLifetime; rol.enabled = true; rol.separateAxes = true; rol.x = new ParticleSystem.MinMaxCurve(-6f, 6f); rol.z = new ParticleSystem.MinMaxCurve(-6f, 6f); }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            if (mesh != null) { r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = mesh; r.alignment = ParticleSystemRenderSpace.Local; r.shadowCastingMode = ShadowCastingMode.Off; }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 webPos, Vector3 webVel, float life, float size, Color? col = null, Vector3? size3 = null)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = U.P(webPos),
                velocity = new Vector3(-webVel.x, webVel.y, webVel.z),
                startLifetime = life,
                startColor = col ?? Color.white,
                rotation3D = new Vector3(U.Rand(0, 360), U.Rand(0, 360), U.Rand(0, 360)),
                applyShapeToPosition = false,
            };
            if (size3.HasValue) ep.startSize3D = size3.Value; else ep.startSize = size;
            ps.Emit(ep, 1);
        }

        // ---------------- particle helpers (match fx.js) ----------------
        public static void FoamBurst(Vector3 p, int n = 10, float spread = 1.2f, Color[] colors = null)
        {
            if (!I) return;
            for (int i = 0; i < n; i++)
                Emit(colors != null ? I.colorFoam : I.foam, p + new Vector3(U.Rand(-spread, spread), U.Rand(-0.3f, 0.8f), U.Rand(-spread, spread)),
                    new Vector3(U.Rand(-0.6f, 0.6f), U.Rand(0.2f, 1.4f), U.Rand(-0.6f, 0.6f)), U.Rand(0.6f, 1.3f), 0.3f * U.Rand(0.7f, 1.5f), colors != null ? U.Pick(colors) : (Color?)null);
        }
        public static void Bubbles(Vector3 p, int n = 4)
        {
            if (!I) return;
            for (int i = 0; i < n; i++) Emit(I.bubble, p + new Vector3(U.Rand(-1, 1), U.Rand(0, 1), U.Rand(-1, 1)), new Vector3(U.Rand(-0.3f, 0.3f), U.Rand(0.5f, 1.2f), U.Rand(-0.3f, 0.3f)), U.Rand(1.5f, 3f), 0.2f * U.Rand(0.6f, 1.5f));
        }
        public static void Splash(Vector3 p, int n = 8, float spd = 2f)
        {
            if (!I) return;
            for (int i = 0; i < n; i++) Emit(I.water, p + new Vector3(U.Rand(-0.3f, 0.3f), 0, U.Rand(-0.3f, 0.3f)), new Vector3(U.Rand(-spd, spd), U.Rand(1f, 3.5f), U.Rand(-spd, spd)), U.Rand(0.4f, 0.8f), 0.07f * U.Rand(0.8f, 1.6f));
        }
        public static void Rain(float x, float z, float w, float d, int n = 6)
        {
            if (!I) return;
            for (int i = 0; i < n; i++) Emit(I.water, new Vector3(x + U.Rand(-w, w), U.Rand(2.6f, 3f), z + U.Rand(-d, d)), new Vector3(0, -U.Rand(5f, 8f), 0), 0.45f, 0, null, new Vector3(0.08f, 0.25f, 0.08f));
        }
        public static void Sparkles(Vector3 p, int n = 6, float spread = 1.5f)
        {
            if (!I) return;
            for (int i = 0; i < n; i++) Emit(I.sparkle, p + new Vector3(U.Rand(-spread, spread), U.Rand(0, 1.2f), U.Rand(-spread, spread)), new Vector3(0, U.Rand(0.2f, 0.8f), 0), U.Rand(0.5f, 1f), 0.6f * U.Rand(0.6f, 1.3f));
        }
        static readonly uint[] ConfettiCols = { 0xff4d5a, 0xffd23f, 0x46c23a, 0x2f8cff, 0xb44cff, 0xff8a1f, 0xffffff };
        public static void Confetti(Vector3 p, int n = 40, float power = 6f)
        {
            if (!I) return;
            for (int i = 0; i < n; i++)
            {
                float s = U.Rand(1f, 1.8f);
                Emit(I.confetti, p, new Vector3(U.Rand(-power, power) * 0.5f, U.Rand(power * 0.6f, power * 1.2f), U.Rand(-power, power) * 0.5f), U.Rand(1.4f, 2.2f), 0, U.Hex(U.Pick(ConfettiCols)), new Vector3(0.16f, 0.03f, 0.1f) * s);
            }
        }
        public static void Smoke(Vector3 p, int n = 10, float s = 1f)
        {
            if (!I) return;
            for (int i = 0; i < n; i++) Emit(I.smoke, p + new Vector3(U.Rand(-1, 1), U.Rand(0, 1), U.Rand(-1, 1)) * s, new Vector3(U.Rand(-1.5f, 1.5f), U.Rand(0.5f, 2f), U.Rand(-1.5f, 1.5f)), U.Rand(0.6f, 1.1f), 0.5f * U.Rand(1.2f, 2.2f) * s);
        }
        public static void Wind(float x, float z, float w, float d, int n = 3)
        {
            if (!I) return;
            for (int i = 0; i < n; i++) Emit(I.wind, new Vector3(x + U.Rand(-w, w), 3f, z + U.Rand(-d, d)), new Vector3(U.Rand(-0.5f, 0.5f), -U.Rand(6f, 9f), U.Rand(-0.5f, 0.5f)), 0.35f, 0, null, new Vector3(0.04f, 0.9f, 0.04f));
        }
        public static void CoinBurst(Vector3 p, int n = 6)
        {
            if (!I) return;
            for (int i = 0; i < n; i++) Emit(I.coin, p, new Vector3(U.Rand(-2, 2), U.Rand(4, 7), U.Rand(-2, 2)), U.Rand(0.8f, 1.2f), 1.2f);
        }
        public static void Glow(Vector3 p, float s = 3f, float life = 0.5f) { if (I) Emit(I.glow, p, Vector3.zero, life, s); }
        /// <summary>One water drop with its own velocity (fountain jets).</summary>
        public static void Jet(Vector3 p, Vector3 vel, float life, float size) { if (I) Emit(I.water, p, vel, life, size); }
        /// <summary>A tumbling chunk that falls and shrinks (bubble pops and mud in the mini-game).</summary>
        public static void Debris(Vector3 p, Vector3 vel, float life, float size, Color col) { if (I) Emit(I.debris, p, vel, life, size, col); }
        public static void ClearParticles()
        {
            if (!I) return;
            foreach (var ps in new[] { I.foam, I.colorFoam, I.bubble, I.water, I.sparkle, I.confetti, I.smoke, I.wind, I.coin, I.glow, I.debris }) ps.Clear();
        }

        // ---------------- stickers on cars ----------------
        public static List<Sticker> AddMud(Car car, int n, float dirtMul)
        {
            var list = new List<Sticker>();
            if (!I || car.Meta == null) return list;
            var surf = car.Meta.surf;
            float[] areas = new float[surf.Count]; float tot = 0;
            for (int i = 0; i < surf.Count; i++) { var p = surf[i]; areas[i] = p.kind == "top" ? (p.x1 - p.x0) * (p.z1 - p.z0) : (p.x1 - p.x0) * (p.y1 - p.y0) * 2; tot += areas[i]; }
            for (int i = 0; i < n; i++)
            {
                float r = UnityEngine.Random.value * tot; int idx = 0;
                while (r > areas[idx] && idx < areas.Length - 1) { r -= areas[idx]; idx++; }
                var p = surf[idx];
                float size = U.Rand(0.16f, 0.34f) * Mathf.Sqrt(dirtMul);
                Vector3 pos, scale; Quaternion rot;
                if (p.kind == "top") { pos = new Vector3(U.Rand(p.x0, p.x1), p.y + 0.01f, U.Rand(p.z0, p.z1)); rot = Quaternion.Euler(0, U.Rand(0, 360), 0); scale = new Vector3(size * U.Rand(1f, 1.6f), 0.06f, size); }
                else { float side = U.Chance(0.75f) ? 1 : -1; pos = new Vector3(U.Rand(p.x0, p.x1), U.Rand(p.y0, p.y1), p.z * side); rot = Quaternion.Euler(0, 0, U.Rand(0, 360)); scale = new Vector3(size * U.Rand(1f, 1.6f), size, 0.06f); }
                list.Add(I.Stick(car.StickerParent(p.parent), U.P(pos), rot, scale, U.Pick(I.mudMats)));
            }
            return list;
        }
        public static List<Sticker> AddCoat(Car car, int n)
        {
            var list = new List<Sticker>();
            if (!I || car.Meta == null) return list;
            var surf = car.Meta.surf;
            for (int i = 0; i < n; i++)
            {
                var p = U.Pick(surf);
                Vector3 pos = p.kind == "top" ? new Vector3(U.Rand(p.x0, p.x1), p.y + 0.05f, U.Rand(p.z0, p.z1)) : new Vector3(U.Rand(p.x0, p.x1), U.Rand(p.y0, p.y1 + 0.3f), p.z * (U.Chance(0.5f) ? 1 : -1));
                float s = U.Rand(0.22f, 0.42f);
                var st = I.Stick(car.StickerParent(p.parent), U.P(pos), Quaternion.identity, new Vector3(s, s * U.Rand(0.8f, 1.1f), s), I.coatMat);
                list.Add(st);
            }
            return list;
        }
        public static void TintCoat(List<Sticker> items, Color c)
        {
            if (!I) return;
            if (!I.coatTints.TryGetValue(c, out var m)) I.coatTints[c] = m = Art.Solid(c);
            foreach (var s in items) if (s.tr) s.tr.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
        Sticker Stick(Transform parent, Vector3 localPos, Quaternion rot, Vector3 scale, Material mat)
        {
            var go = new GameObject("Sticker");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localRotation = rot; go.transform.localScale = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = Art.Ico;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
            var s = new Sticker { tr = go.transform, baseScale = scale, k = 0f, target = 1f, wob = U.Rand(0, 6) };
            stickers.Add(s);
            return s;
        }
        public static void Unstick(IEnumerable<Sticker> items) { foreach (var s in items) { s.target = 0; s.dead = true; } }

        // ---------------- flyers ----------------
        public static void Fly(Transform obj, Vector3 from, Func<Vector3> to, float dur, float arc, Action<Transform> done, float spin = 0f, float scaleTo = -1f)
        {
            if (!I) { done?.Invoke(obj); return; }
            obj.SetParent(I.transform, true);
            obj.position = U.P(from);
            I.flyers.Add(new Flyer { obj = obj, from = from, to = to, dur = Mathf.Max(0.01f, dur), arc = arc, done = done, spin = spin, scaleFrom = obj.localScale.x, scaleTo = scaleTo });
        }
        /// <summary>Stop any flight of this object without running its done callback.</summary>
        public static void CancelFly(Transform obj)
        {
            if (!I || !obj) return;
            for (int i = I.flyers.Count - 1; i >= 0; i--) if (I.flyers[i].obj == obj) I.flyers.RemoveAt(i);
        }
        public static Transform GetBundle()
        {
            Transform t = I.bundlePool.Count > 0 ? I.bundlePool.Pop() : Art.Spawn("Items/cash_bundle", I.transform).transform;
            t.gameObject.SetActive(true); t.localScale = Vector3.one; t.rotation = Quaternion.Euler(0, U.Rand(0, 360), 0);
            return t;
        }
        public static void FreeBundle(Transform t) { t.gameObject.SetActive(false); t.SetParent(I.transform, false); I.bundlePool.Push(t); }

        /// <summary>Springy scale-in from almost nothing to the object's current scale (port of popIn).</summary>
        public static void PopIn(Transform tr, float delay = 0f, float from = 0.01f)
        {
            if (!I || !tr) return;
            var target = tr.localScale;
            foreach (var p in I.pops) if (p.tr == tr) { target = p.target; I.pops.Remove(p); break; }
            tr.localScale = target * from;
            I.pops.Add(new Pop { tr = tr, target = target, t = -delay });
        }
        /// <summary>Single sticker on any transform (used for the pigeon surprise).</summary>
        public static Sticker AddSticker(Transform parent, Vector3 webLocalPos, Vector3 scale, Color col)
            => I ? I.Stick(parent, U.P(webLocalPos), Quaternion.identity, scale, Art.Solid(col)) : null;

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                if (!p.tr) { pops.RemoveAt(i); continue; }
                p.t += dt;
                if (p.t < 0f) continue;
                float k = p.t / 0.45f;
                p.tr.localScale = p.target * (k >= 1f ? 1f : Mathf.Max(0.01f, U.EaseOutBack(k)));
                if (k >= 1f) pops.RemoveAt(i);
            }
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                if (!f.obj) { flyers.RemoveAt(i); continue; }
                f.t += dt / f.dur;
                float t = Mathf.Min(1f, f.t);
                var target = f.to();
                var p = Vector3.Lerp(f.from, target, t);
                p.y += Mathf.Sin(Mathf.PI * t) * f.arc;
                f.obj.position = U.P(p);
                if (f.spin != 0f) f.obj.Rotate(f.spin * 57.3f * dt * 0.5f, -f.spin * 57.3f * dt, 0f, Space.Self);
                if (f.scaleTo >= 0f) f.obj.localScale = Vector3.one * Mathf.Lerp(f.scaleFrom, f.scaleTo, t);
                if (f.t >= 1f) { flyers.RemoveAt(i); f.done?.Invoke(f.obj); }
            }
            for (int i = stickers.Count - 1; i >= 0; i--)
            {
                var s = stickers[i];
                if (!s.tr) { stickers.RemoveAt(i); continue; }
                s.k = U.Damp(s.k, s.target, s.target > s.k ? 10f : 7f, dt);
                if ((s.dead || s.target == 0f) && s.k < 0.03f) { Destroy(s.tr.gameObject); stickers.RemoveAt(i); continue; }
                s.wob += dt * 3f;
                s.tr.localScale = s.baseScale * (s.k * (1f + Mathf.Sin(s.wob) * 0.04f));
            }
        }
        public static void ClearAll()
        {
            if (!I) return;
            foreach (var s in I.stickers) if (s.tr) Destroy(s.tr.gameObject);
            I.stickers.Clear();
            foreach (var f in I.flyers) if (f.obj && f.obj.parent == I.transform && !I.bundlePool.Contains(f.obj)) Destroy(f.obj.gameObject);
            I.flyers.Clear();
            I.pops.Clear();
            ClearParticles();
        }
    }
}
