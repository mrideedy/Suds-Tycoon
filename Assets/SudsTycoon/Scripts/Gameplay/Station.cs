using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>One machine on a wash lane, or the pay booth (port of Station in stations.js).</summary>
    public class Station
    {
        // collision boxes per machine, relative to the station centre: x0, x1, z0, z1 (web coordinates)
        static readonly Dictionary<string, float[][]> Coll = new Dictionary<string, float[][]>
        {
            { "brush", new[] { new[] { -1.25f, -0.65f, -2.5f, -1.9f }, new[] { -1.25f, -0.65f, 1.9f, 2.5f } } },
            { "rinse", new[] { new[] { -0.42f, 0.42f, -2.55f, -1.75f }, new[] { -0.42f, 0.42f, 1.75f, 2.55f } } },
            { "wax", new[] { new[] { -0.42f, 0.42f, -2.55f, -1.75f }, new[] { -0.42f, 0.42f, 1.75f, 2.55f } } },
            { "dryer", new[] { new[] { -1.05f, 1.05f, -2.45f, -1.85f }, new[] { -1.05f, 1.05f, 1.85f, 2.45f } } },
            { "tire", new[] { new[] { -1.3f, 1.3f, -2.3f, -1.38f }, new[] { -1.3f, 1.3f, 1.38f, 2.3f } } },
            { "polish", new[] { new[] { -2.17f, -1.83f, -2.37f, -2.03f }, new[] { 1.83f, 2.17f, -2.37f, -2.03f }, new[] { -2.17f, -1.83f, 2.03f, 2.37f }, new[] { 1.83f, 2.17f, 2.03f, 2.37f } } },
            { "pay", new[] { new[] { -1.15f, 1.15f, 2.05f, 2.55f }, new[] { -1.25f, -0.78f, 2.3f, 3.75f }, new[] { 1.35f, 1.75f, 1.7f, 2.1f } } },
        };
        static readonly uint[] TierColors = { 0xffffff, 0xd8f1ff, 0xeadcff, 0xffe39a, 0xa8fff4 };
        static readonly string[] TierWords = { "", "FROSTY PAINT!", "ROYAL PAINT!", "SOLID GOLD!", "DIAMOND SHINE!" };
        static readonly Dictionary<uint, Material> tierMats = new Dictionary<uint, Material>();

        public readonly Lane Lane;
        public readonly string Type;
        public readonly StationDef Def;
        public readonly float X, Z;
        public readonly Transform Group;
        public Vector3 OperatorSpot;
        public readonly float OperatorFacing = Mathf.PI;
        public Hopper Hopper;
        public Car Occupant, Car;
        public bool Busy;

        readonly Transform machine, booth, barrier, opDecal;
        readonly List<Renderer> tintable = new List<Renderer>();
        readonly MachineAnim anim;
        readonly WLabel badge, prog;
        RingProgress ring;
        float t, dur = 1f, fxT, barrierT, barrierRot;
        string block;
        int tier = -1;

        public Station(Lane lane, string type, Transform parent)
        {
            Lane = lane; Type = type; Def = Cfg.Stations[type];
            X = Cfg.SlotX[type]; Z = lane.Z;
            Group = new GameObject("Station_" + type + "_" + lane.Idx).transform;
            Group.SetParent(parent, false);
            Group.position = U.P(X, 0f, Z);
            if (type != "pay")
            {
                machine = Art.Spawn("Machines/machine_" + type, Group).transform;
                anim = new MachineAnim(type, machine);
            }
            else
            {
                booth = Art.Spawn("Machines/pay_booth", Group).transform;
                booth.localPosition = U.P(0f, 0f, Cfg.BoothDZ);
                barrier = U.Find(booth, "Barrier");
            }
            if (Def.op != null)
            {
                OperatorSpot = new Vector3(X, 0f, Z + (type == "pay" ? Cfg.CashierDZ : Cfg.PolishDZ));
                opDecal = Art.GroundDecal("OperatorMat", Art.DecalMat(type == "pay" ? "mat_spot_pay" : "mat_spot_polish"), 1.3f, parent, OperatorSpot.x, 0.06f, OperatorSpot.z);
            }
            float lz = Z + (type == "pay" ? Cfg.BoothDZ : 0f);
            badge = WorldUI.Label(new Vector3(X, type == "pay" ? 3.9f : 5.2f, lz));
            prog = WorldUI.Label(new Vector3(X, type == "pay" ? 3.4f : 4.6f, lz));
            // machines are repainted at Lv 10 / 20 / 30: collect the vertex-coloured parts (not the floor)
            foreach (var r in Group.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (!m || !m.name.StartsWith("VertexColor") || UnderFloor(r.transform)) continue;
                tintable.Add(r);
            }
            ApplyTier();
        }

        bool UnderFloor(Transform tr)
        {
            for (var p = tr; p && p != Group; p = p.parent) if (p.name == "Floor") return true;
            return false;
        }

        static Material TierMat(uint col)
        {
            if (!tierMats.TryGetValue(col, out var m)) tierMats[col] = m = Art.Make(U.Hex(col), null, true);
            return m;
        }

        public bool ApplyTier()
        {
            int l = Game.Lv(Type), nt = 0;
            while (nt < Cfg.PaintLevels.Length && l >= Cfg.PaintLevels[nt]) nt++;   // new paint at Lv 5, 10, 20 and 30 (max)
            if (nt == tier) return false;
            bool first = tier < 0;
            tier = nt;
            if (nt > 0 || !first)
            {
                var m = TierMat(TierColors[nt]);
                foreach (var r in tintable) if (r) r.sharedMaterial = m;
            }
            if (!first && nt > 0)
            {
                Fx.Confetti(new Vector3(X, 4f, Z), 40, 6f);
                WorldUI.Text(new Vector3(X, 5.4f, Z), TierWords[nt], "new", 1.5f);
            }
            return true;
        }

        public void MakeHopper(int count, Transform parent) { if (Def.supply != null) Hopper = new Hopper(this, count, parent); }

        public IEnumerable<Box2> Colliders()
        {
            if (Coll.TryGetValue(Type, out var list))
                foreach (var c in list) yield return new Box2(X + c[0], X + c[1], Z + c[2], Z + c[3]);
            if (Hopper != null) foreach (var b in Hopper.Colliders()) yield return b;
        }

        /// <summary>"player", "worker" or null.</summary>
        public string OperatorPresent()
        {
            var p = Game.Player;
            if (p.Near(OperatorSpot.x, OperatorSpot.z, 1.15f)) return "player";
            foreach (var w in Game.Workers) if (w.Post == this && w.AtPost && !w.Napping) return "worker";
            return null;
        }

        public bool TryStart(Car car)
        {
            if (Def.supply != null && Hopper.Count <= 0) { SetBlock("supply"); return false; }
            if (Def.op != null && OperatorPresent() == null) { SetBlock("staff"); return false; }
            SetBlock(null);
            Hopper?.Consume();
            Busy = true; Car = car; t = 0f; fxT = 0f;
            dur = Game.StationTime(Type);
            OnStart(car);
            return true;
        }

        public string Block => block;

        void SetBlock(string b)
        {
            if (b == block) return;
            block = b;
            if (b == null) { badge.Clear(); return; }
            if (b == "supply")
            {
                var s = Cfg.Supplies[Def.supply];
                badge.Set("supply", e => e.Add(WorldUI.Chip("st-badge", s.icon, "NO " + s.name.ToUpperInvariant() + "!")));
            }
            else badge.Set("staff", e => e.Add(WorldUI.Chip("st-badge warn", "worker", Type == "pay" ? "NEED CASHIER" : "NEED POLISHER")));
        }

        void OnStart(Car car)
        {
            switch (Type)
            {
                case "brush": car.AddFoam(); Sfx.Play("scrub"); break;
                case "rinse": Sfx.Play("spray"); break;
                case "dryer": Sfx.Play("fan"); break;
                case "wax": Sfx.Play("bubble"); break;
            }
        }

        public void Update(float dt)
        {
            anim?.Update(dt, Busy);
            if (booth)
            {
                barrierT = Mathf.Max(0f, barrierT - dt);
                barrierRot = U.Damp(barrierRot, barrierT > 0f ? 1.35f : 0f, 9f, dt);
                if (barrier) barrier.localRotation = Quaternion.Euler(barrierRot * Mathf.Rad2Deg, 0f, 0f);
            }
            Hopper?.Update(dt);
            if (!Busy)
            {
                if (ring != null) { prog.Clear(); ring = null; }
                // a blocked car sits here: keep the badge, otherwise clear it
                if (block != null && !(Occupant != null && Occupant.State == "drive")) SetBlock(null);
                return;
            }
            // operator stations only run while someone is there
            if (Def.op != null && OperatorPresent() == null) { SetBlock("staff"); return; }
            if (Def.op != null) SetBlock(null);
            t += dt;
            fxT -= dt;
            float k = t / dur;
            StageFx(Car, k);
            if (ring == null) prog.Set("ring", e => { ring = new RingProgress(); e.Add(ring); });
            ring.Value = Mathf.Min(1f, k);
            if (t >= dur) Finish();
        }

        static readonly Color[] WaxColors = { U.Hex(0xff6fb1), U.Hex(0xffd23f), U.Hex(0x4fa3ff) };

        void StageFx(Car car, float k)
        {
            float x = X, z = Z;
            switch (Type)
            {
                case "brush":
                    if (fxT <= 0f)
                    {
                        fxT = 0.07f;
                        Fx.FoamBurst(new Vector3(x + U.Rand(-1.4f, 1.4f), U.Rand(0.5f, 1.8f), z + (U.Chance(0.5f) ? -1f : 1f) * U.Rand(0.9f, 1.4f)), 2, 0.25f);
                        if (U.Chance(0.3f)) Fx.Bubbles(new Vector3(x, 2f, z), 1);
                        if (U.Chance(0.15f)) Sfx.Play("scrub");
                    }
                    car.SetMud(U.Lerp(1f, 0.45f, k));
                    car.Dirt = Mathf.Min(car.Dirt, U.Lerp(1f, 0.5f, k)); car.DustTint();
                    break;
                case "rinse":
                    if (fxT <= 0f)
                    {
                        fxT = 0.05f;
                        Fx.Rain(x, z, 0.4f, 1.7f, 3);
                        if (U.Chance(0.5f)) Fx.Splash(new Vector3(x + U.Rand(-1f, 1f), 0.1f, z + U.Rand(-1.5f, 1.5f)), 3, 1.5f);
                        if (U.Chance(0.08f)) Sfx.Play("splash");
                    }
                    if (k > 0.3f && car.Coat.Count > 0) car.ClearFoam();
                    car.SetMud(U.Lerp(0.45f, 0f, U.Clamp((k - 0.2f) / 0.6f, 0f, 1f)));
                    car.Dirt = Mathf.Min(car.Dirt, U.Lerp(0.5f, 0f, k)); car.DustTint();
                    if (car.HasFlies && k > 0.5f) car.ShooFlies();
                    if (k > 0.5f && car.WashMeVisible) car.HideWashMe();
                    break;
                case "wax":
                    if (fxT <= 0f) { fxT = 0.08f; Fx.FoamBurst(new Vector3(x + U.Rand(-1.2f, 1.2f), U.Rand(0.9f, 1.9f), z + U.Rand(-0.9f, 0.9f)), 2, 0.3f, WaxColors); }
                    car.Shine = Mathf.Max(car.Shine, k * 0.5f); car.DustTint();
                    break;
                case "dryer":
                    if (fxT <= 0f)
                    {
                        fxT = 0.05f;
                        Fx.Wind(x, z, 0.9f, 1.4f, 2);
                        if (k > 0.6f && U.Chance(0.4f)) Fx.Sparkles(new Vector3(x, 1.6f, z), 1, 1.4f);
                    }
                    car.Shine = Mathf.Max(car.Shine, 0.5f + k * 0.3f); car.DustTint();
                    break;
                case "tire":
                    if (fxT <= 0f)
                    {
                        fxT = 0.12f;
                        foreach (var w in car.Wheels) if (w && U.Chance(0.5f)) { var wp = U.W(w.position); Fx.Sparkles(new Vector3(wp.x, 0.3f, wp.z), 1, 0.2f); }
                    }
                    break;
                case "polish":
                    if (fxT <= 0f)
                    {
                        fxT = 0.1f;
                        Fx.Sparkles(new Vector3(x + U.Rand(-1.5f, 1.5f), U.Rand(0.8f, 1.8f), z + U.Rand(-0.9f, 0.9f)), 1, 0.3f);
                        if (U.Chance(0.2f)) Sfx.Play("scrub");
                    }
                    car.Shine = Mathf.Max(car.Shine, 0.8f + k * 0.2f); car.DustTint();
                    break;
            }
        }

        void Finish()
        {
            var car = Car;
            Busy = false; Car = null;
            prog.Clear(); ring = null;
            if (Type == "rinse") { car.ClearFoam(); car.SetMud(0f); car.Dirt = 0f; car.DustTint(); }
            if (Type == "brush") car.SetMud(0.45f);
            if (Type == "dryer" || Type == "polish") Fx.Sparkles(new Vector3(X, 1.5f, Z), Type == "polish" ? 10 : 5, 1.6f);
            if (Type == "polish") { Sfx.Play("squeak"); WorldUI.Text(new Vector3(X, car.H + 1.0f, Z), "SO SHINY!", "big", 1.1f); }
            car.OnProcessed(this);
            if (Type == "pay") { barrierT = 1.4f; Game.Payout(this, car); }
            Bus.Emit("station:done", this);
        }

        /// <summary>Little "boing" when upgraded.</summary>
        public void Punch()
        {
            Fx.Sparkles(new Vector3(X, 3f, Z), 8, 1.6f);
            ApplyTier();
            Group.localScale = Vector3.one * 1.08f;
            Sched.After(0.12f, () => { if (Group) Group.localScale = Vector3.one; });
        }

        public void Destroy()
        {
            if (Group) Object.Destroy(Group.gameObject);
            badge.Remove(); prog.Remove();
            if (opDecal) Object.Destroy(opDecal.gameObject);
            Hopper?.Destroy();
        }
    }

    /// <summary>Idle / busy animation of a machine's moving parts (rollers, water curtain, fans, tire brushes).</summary>
    public class MachineAnim
    {
        readonly string type;
        readonly List<Transform> spinners = new List<Transform>();
        readonly List<Quaternion> bases = new List<Quaternion>();
        readonly List<Material> curtainMats = new List<Material>();
        readonly Material puddleMat;
        readonly Color curtainCol, puddleCol;
        float spin, flow, angle, offset;

        public MachineAnim(string type, Transform root)
        {
            this.type = type;
            void Add(string name) { var tr = U.Find(root, name); if (tr) { spinners.Add(tr); bases.Add(tr.localRotation); } }
            switch (type)
            {
                case "brush": Add("RollerL"); Add("RollerR"); break;
                case "dryer": Add("Fan0"); Add("Fan1"); break;
                case "tire": Add("TireBrush0"); Add("TireBrush1"); break;
                case "rinse":
                    // own material copies: the curtain fades in and its streaks scroll while a car is rinsed
                    Material shared = null;
                    for (int i = 0; i < 2; i++)
                    {
                        var c = U.Find(root, "Curtain" + i);
                        if (!c || !c.TryGetComponent<Renderer>(out var r)) continue;
                        if (shared == null) { shared = new Material(r.sharedMaterial); Art.SetTransparent(shared, true); curtainMats.Add(shared); }
                        r.sharedMaterial = shared;
                    }
                    curtainCol = shared ? shared.GetColor("_BaseColor") : Color.white;
                    var pd = U.Find(root, "Puddle");
                    if (pd && pd.TryGetComponent<Renderer>(out var pr)) { puddleMat = new Material(pr.sharedMaterial); Art.SetTransparent(puddleMat, true); pr.sharedMaterial = puddleMat; puddleCol = puddleMat.GetColor("_BaseColor"); }
                    break;
            }
        }

        public void Update(float dt, bool active)
        {
            switch (type)
            {
                case "brush":
                    spin = U.Damp(spin, active ? 9f : 0.3f, 3f, dt); angle += spin * dt;
                    // web: left roller rotation.y += spin, right -= spin (Y rotations flip sign in Unity)
                    if (spinners.Count > 0) spinners[0].localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f) * bases[0];
                    if (spinners.Count > 1) spinners[1].localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f) * bases[1];
                    break;
                case "dryer":
                    spin = U.Damp(spin, active ? 22f : 0.5f, 2.5f, dt); angle += spin * dt;
                    for (int i = 0; i < spinners.Count; i++) spinners[i].localRotation = bases[i] * Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                    break;
                case "tire":
                    spin = U.Damp(spin, active ? 14f : 0.2f, 3f, dt); angle += spin * dt;
                    for (int i = 0; i < spinners.Count; i++) spinners[i].localRotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.right) * bases[i];
                    break;
                case "rinse":
                    flow = U.Damp(flow, active ? 0.92f : 0f, 5f, dt);
                    offset += dt * 2.2f;
                    foreach (var m in curtainMats)
                    {
                        var c = curtainCol; c.a = flow; m.SetColor("_BaseColor", c);
                        // glTFast flips v on import, so scroll the other way to keep the water falling
                        m.SetTextureOffset("_BaseMap", new Vector2(0f, -(offset % 1f)));
                    }
                    if (puddleMat) { var c = puddleCol; c.a = 0.32f + flow * 0.2f; puddleMat.SetColor("_BaseColor", c); }
                    break;
            }
        }
    }
}
