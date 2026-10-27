using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>
    /// A customer car (port of Car in entities.js). Lanes run along +x: each car drives toward its next unvisited
    /// station but never closer than a safe gap to the car ahead, and only occupies a station while stopped there.
    /// </summary>
    public class Car
    {
        public readonly string Type;
        public readonly CarDef Def;
        public readonly CarMeta Meta;
        public readonly Lane Lane;
        public readonly Transform Root, Inner, Hover;
        public float X, Z, V;
        public string State = "drive";
        public Station Stop;
        public float DoneX = float.NegativeInfinity;
        public float PatMax, Patience, Waited;
        public float Bill, Dirt = 1f, Shine;
        public int Stages;
        public readonly bool Golden, Vip;
        public float GoldenMult { get; private set; }
        public string Mood = "happy";
        public List<Sticker> Mud = new List<Sticker>();
        public List<Sticker> Coat = new List<Sticker>();
        public bool Dead;

        readonly Color paint;
        readonly Material paintMat;
        readonly Transform washMe, sirenRed, sirenBlue, flies;
        readonly List<Transform> wheels = new List<Transform>(), lights = new List<Transform>();
        readonly List<float> wheelR = new List<float>(), wheelA = new List<float>();
        readonly WLabel label;
        WLabel sayLbl, goldLbl;
        float sayT, honkT, bounce, t, prevV;
        string labelKey;

        static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        public Car(string type, Lane lane, Transform parent, float? x = null)
        {
            Type = type; Def = Cfg.Cars[type]; Meta = CarMeta.Get(type); Lane = lane;
            paint = U.Hex(Cfg.PaintFor.TryGetValue(type, out var pc) ? pc : U.Pick(Cfg.PaintColors));
            Root = Art.Spawn("Cars/car_" + type, parent).transform;
            Inner = U.Find(Root, "Inner") ?? Root;
            Hover = U.Find(Root, "Hover");
            var paintTr = U.Find(Root, "Paint");
            if (paintTr && paintTr.TryGetComponent<MeshRenderer>(out var pr))
            {
                paintMat = new Material(pr.sharedMaterial);
                pr.sharedMaterial = paintMat;
                if (type == "golden") paintMat.SetColor("_EmissionColor", U.Hex(0x553300) * 1.6f);
            }
            for (int i = 0; ; i++)
            {
                var w = U.Find(Root, "Wheel" + i);
                if (!w) break;
                wheels.Add(w); wheelA.Add(0f);
                wheelR.Add(Meta != null && i < Meta.wheels.Count ? Mathf.Max(0.1f, Meta.wheels[i].r) : 0.42f);
            }
            sirenRed = U.Find(Root, "SirenRed"); sirenBlue = U.Find(Root, "SirenBlue");
            for (int i = 0; i < 8; i++) { var l = U.Find(Root, "Light" + i); if (l) lights.Add(l); }
            washMe = U.Find(Root, "WashMe");

            X = x ?? Cfg.SpawnX - U.Rand(0f, 6f);
            Z = lane.Z;
            V = Cfg.RoadSpeed * 0.8f;
            PatMax = Game.Patience(Def); Patience = PatMax;
            Golden = type == "golden"; GoldenMult = Golden ? Game.GoldenPay() : 1f;
            Vip = Def.vip;
            Mud = Fx.AddMud(this, Mathf.RoundToInt(U.Rand(10f, 15f) * Def.dirt), Def.dirt);
            if (washMe && washMe.gameObject.activeSelf && !U.Chance(0.55f)) washMe.gameObject.SetActive(false);
            DustTint();
            label = WorldUI.Label(Root, H + 0.75f);
            honkT = U.Rand(4f, 9f);
            if (Def.dirt > 1.2f || U.Chance(0.25f)) flies = MakeFlies();
            Root.position = U.P(X, 0f, Z);
            Root.rotation = Quaternion.identity;
            t = Random.value * 10f;
            if (Golden)
            {
                goldLbl = WorldUI.Label(Root, H + 1.6f);
                goldLbl.Set("gold", e => e.Add(WorldUI.Chip("st-badge gold", "golden", "GOLDEN x" + GoldenMult)));
            }
            UpdateLabel(true);
        }

        public float Len => Meta != null ? Meta.len : Def.len;
        public float H => Meta != null ? Meta.h : 1.7f;

        /// <summary>Transform that mud / foam patches are glued to ("hover" for the UFO body).</summary>
        public Transform StickerParent(string parent) => parent == "hover" && Hover ? Hover : Inner;

        Transform MakeFlies()
        {
            var g = new GameObject("Flies").transform;
            g.SetParent(Root, false);
            g.localPosition = new Vector3(0f, H + 0.3f, 0f);
            var mat = Art.Solid(U.Hex(0x1b1b1b));
            for (int i = 0; i < 3; i++)
            {
                var f = new GameObject("Fly");
                f.transform.SetParent(g, false);
                f.transform.localScale = Vector3.one * 0.06f;
                f.AddComponent<MeshFilter>().sharedMesh = Art.Ico;
                var r = f.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return g;
        }

        public void DustTint()
        {
            if (!paintMat) return;
            float d = U.Clamp(Dirt, 0f, 1f);
            var dusty = Color.Lerp(paint, U.Hex(0x8b6a3e), 0.45f);
            paintMat.SetColor("_BaseColor", Color.Lerp(paint, dusty, d));
            if (!Golden) paintMat.SetColor("_EmissionColor", new Color(0.3f * Shine, 0.3f * Shine, 0.32f * Shine));
        }
        public void SetMud(float target) { foreach (var m in Mud) m.target = Mathf.Min(m.target, target); }
        public void AddFoam() { if (Coat.Count == 0) Coat = Fx.AddCoat(this, Mathf.RoundToInt(Len * 7f)); }
        public void ClearFoam() { foreach (var c in Coat) { c.target = 0f; c.dead = true; } Coat.Clear(); }
        /// <summary>Watching the golden-car ad makes it pay x10.</summary>
        public void SetGoldenMult(float m)
        {
            GoldenMult = m;
            goldLbl?.Set("gold" + m, e => e.Add(WorldUI.Chip("st-badge gold", "golden", "GOLDEN x" + m)));
        }
        public bool HasFlies => flies;
        public void ShooFlies() { if (flies) Object.Destroy(flies.gameObject); }
        public bool WashMeVisible => washMe && washMe.gameObject.activeSelf;
        public void HideWashMe() { if (washMe) washMe.gameObject.SetActive(false); }
        public IEnumerable<Transform> Wheels => wheels;

        // ---------------- queue + line movement ----------------
        bool MoveTo(float tx, float dt, float vmax, float acc = 9f)
        {
            float d = tx - X;
            if (d <= 0.01f) { V = 0f; return true; }
            float vt = Mathf.Min(vmax, Mathf.Sqrt(2f * 7f * d) + 0.2f);
            V = V < vt ? Mathf.Min(vt, V + acc * dt) : vt;
            X = Mathf.Min(tx, X + V * dt);
            return tx - X < 0.02f;
        }
        Car Ahead() { var a = Lane.Cars; int i = a.IndexOf(this); return i > 0 ? a[i - 1] : null; }
        // Skip stations the car has already driven past: a machine built behind a car mid-lane used to leave it
        // waiting forever for a station it could never reach, jamming the whole lane.
        Station NextStation() { foreach (var st in Lane.Stops()) if (st.X > DoneX + 0.1f && st.X >= X - 0.05f) return st; return null; }
        public bool Waiting => X < Cfg.EntranceX - 0.3f && State == "drive";

        public void Update(float dt)
        {
            t += dt;
            if (State == "drive")
            {
                var st = NextStation();
                float tx = st != null ? st.X : Cfg.DespawnX + 20f;
                var a = Ahead();
                if (a != null && !a.Dead) tx = Mathf.Min(tx, a.X - ((a.Len + Len) / 2f + 0.9f));
                float far = (st != null ? st.X : tx) - X;
                float vmax = X < Cfg.EntranceX - 4f ? Cfg.RoadSpeed : (far > 7f ? Cfg.LineSpeed * 2.1f : Cfg.LineSpeed);
                if (tx > X) MoveTo(tx, dt, vmax, X < Cfg.EntranceX ? 9f : 6f);
                else V = U.Damp(V, 0f, 12f, dt);
                if (st == null) Leave();
                else if (Mathf.Abs(X - st.X) < 0.06f && (st.Occupant == null || st.Occupant == this))
                {
                    if (st.Occupant == null) { st.Occupant = this; bounce = 1f; if (st == Lane.FirstStop()) EnteredLine(); }
                    if (st.TryStart(this)) { State = "proc"; Stop = st; }
                    else Waited += dt * 0.5f;
                }
                // patience only drains while waiting outside the entrance
                if (Waiting && X > Cfg.EntranceX - 45f && V < 0.8f)
                {
                    Waited += dt;
                    Patience -= dt * Game.PatienceDrain();
                    honkT -= dt;
                    if (Patience < PatMax * 0.45f && honkT <= 0f) { Honk(); honkT = U.Rand(4f, 8f); }
                    if (Patience <= 0f) { RageQuit(); return; }
                    if (sayLbl == null && Waited > 2f && Random.value < dt * 0.06f) Say(U.Pick(Cfg.DriverLines));
                }
            }
            else if (State == "proc") V = 0f;
            else if (State == "leave")
            {
                MoveTo(Cfg.DespawnX + 10f, dt, Cfg.RoadSpeed * 1.2f, 5f);
                if (X > Cfg.DespawnX) { Destroy(); return; }
            }
            Visuals(dt);
        }

        void Visuals(float dt)
        {
            Root.position = U.P(X, 0f, Z);
            for (int i = 0; i < wheels.Count; i++)
            {
                wheelA[i] -= V * dt / wheelR[i];
                wheels[i].localRotation = Quaternion.Euler(0f, 0f, -wheelA[i] * Mathf.Rad2Deg);
            }
            bounce = U.Damp(bounce, 0f, 6f, dt);
            float sq = Mathf.Sin(t * 22f) * bounce * 0.06f;
            Inner.localScale = new Vector3(1f + sq * 0.5f, 1f - sq, 1f + sq * 0.5f);
            Inner.localRotation = Quaternion.Euler(0f, 0f, U.Clamp(V - prevV, -2f, 2f) * 0.02f * Mathf.Rad2Deg);
            prevV = V;
            if (Hover) { var hp = Hover.localPosition; hp.y = 1.25f + Mathf.Sin(t * 3f) * 0.12f; Hover.localPosition = hp; }
            if (sirenRed && sirenBlue) { bool on = Mathf.FloorToInt(t * 6f) % 2 == 1; sirenRed.gameObject.SetActive(on); sirenBlue.gameObject.SetActive(!on); }
            for (int i = 0; i < lights.Count; i++) lights[i].gameObject.SetActive((Mathf.FloorToInt(t * 8f) + i) % 2 == 0);
            if (flies)
                for (int i = 0; i < flies.childCount; i++)
                {
                    float a = t * (5 + i) + i * 2;
                    flies.GetChild(i).localPosition = U.P(Mathf.Cos(a) * 0.9f, Mathf.Sin(a * 1.7f) * 0.3f, Mathf.Sin(a) * 0.7f);
                }
            if (sayLbl != null) { sayT -= dt; if (sayT <= 0f) { sayLbl.Remove(); sayLbl = null; } }
            UpdateLabel(false);
        }

        void Honk()
        {
            if (Type == "duck") Sfx.Play("quack");
            else if (Type == "police") Sfx.Play("siren");
            else if (Type == "icecream") Sfx.Play("jingle");
            else Sfx.Play("honk", Def.honky ? 1.1f : 1f);
            if (Game.Player.Near(X, Z, 30f)) WorldUI.Text(new Vector3(X, H + 0.5f, Z), Type == "duck" ? "QUACK!" : "HONK!", "honk", 0.9f, 40f);
            bounce = 0.8f;
        }

        void Say(string text)
        {
            if (sayLbl != null) return;
            sayLbl = WorldUI.Label(Root, H + 1.55f);
            sayLbl.Set(text, e => e.Add(WorldUI.Txt(text, "say")));
            sayT = 3.2f;
        }

        string Face()
        {
            if (State == "leave") return Mood == "love" ? "love" : Mood == "meh" ? "meh" : "happy";
            float p = Patience / PatMax;
            if (!Waiting) return Waited > 8f ? "meh" : "happy";
            return p > 0.6f ? "happy" : p > 0.3f ? "meh" : "angry";
        }

        void UpdateLabel(bool force)
        {
            string f = Face();
            float p = U.Clamp(Patience / PatMax, 0f, 1f);
            bool showBar = Waiting && p < 0.75f && X > Cfg.EntranceX - 45f;
            bool showFace = !Waiting || X > Cfg.EntranceX - 45f;
            string key = showFace ? f + (showBar ? Mathf.RoundToInt(p * 20f).ToString() : "x") : "";
            if (!force && key == labelKey) return;
            labelKey = key;
            if (!showFace) { label.Clear(); return; }
            var col = p > 0.6f ? U.Hex(0x46c23a) : p > 0.3f ? U.Hex(0xffc93c) : U.Hex(0xff4d5a);
            label.Set(key, e =>
            {
                var mood = WorldUI.El("mood");
                mood.Add(WorldUI.Icon(f, "face"));
                if (showBar) mood.Add(WorldUI.Bar("pbar", p, col));
                e.Add(mood);
            });
        }

        void EnteredLine()
        {
            var lamp = Lane.EntranceLamp;
            if (!lamp) return;
            mpb.SetColor("_BaseColor", U.Hex(0xff4d4d)); lamp.SetPropertyBlock(mpb);
            Sched.After(0.9f, () => { if (!lamp) return; mpb.SetColor("_BaseColor", U.Hex(0x3dff6a)); lamp.SetPropertyBlock(mpb); });
        }

        public void OnProcessed(Station station)
        {
            State = "drive"; DoneX = station.X; Stop = null;
            if (station.Occupant == this) station.Occupant = null;
            Waited = 0f;
            Bill += Game.StationValue(station.Type);   // the Pay Booth adds its service fee just before the payout
            if (station.Type != "pay") Stages++;
        }

        void Leave()
        {
            State = "leave";
            bool clean = Dirt < 0.05f;
            Mood = clean && Stages >= 3 ? "love" : clean ? "happy" : "meh";
            UpdateLabel(true);
            if (goldLbl != null) { goldLbl.Remove(); goldLbl = null; }
            // funny: a pigeon sometimes ruins a freshly cleaned car on its way out
            if (clean && U.Chance(0.05f))
                Sched.After(0.9f, () =>
                {
                    if (Dead) return;
                    var s = Fx.AddSticker(Inner, new Vector3(0f, H + 0.02f, 0f), new Vector3(0.35f, 0.06f, 0.3f), Color.white);
                    if (s != null) Mud.Add(s);
                    WorldUI.Text(new Vector3(X, H + 0.8f, Z), "SPLAT! NOOO!", "rage", 1.1f);
                    Sfx.Play("splat");
                });
        }

        void RageQuit()
        {
            Fx.Smoke(new Vector3(X, 1f, Z), 16, 1.2f); Sfx.Play("rage");
            WorldUI.Text(new Vector3(X, 2.6f, Z), U.Pick(Cfg.RagePuns), "rage", 1.4f);
            Bus.Emit("car:rage", this);
            Destroy();
        }

        public void Destroy()
        {
            if (Dead) return;
            Dead = true;
            Lane.Cars.Remove(this);
            foreach (var st in Lane.Stations.Values) if (st.Occupant == this) st.Occupant = null;
            label.Remove(); sayLbl?.Remove(); goldLbl?.Remove();
            if (Root) Object.Destroy(Root.gameObject);
            if (paintMat) Object.Destroy(paintMat);
            Game.RemoveCar(this);
        }

        /// <summary>Remove without side effects (location unload).</summary>
        public void Discard()
        {
            Dead = true;
            label.Remove(); sayLbl?.Remove(); goldLbl?.Remove();
            if (Root) Object.Destroy(Root.gameObject);
            if (paintMat) Object.Destroy(paintMat);
        }
    }
}
