using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SudsTycoon
{
    /// <summary>
    /// Bubble Blaster: a Mob Control-style battle against the Mud Monster (port of minigame.js).
    /// Slide the soap cannon, fire bubbles through multiplier gates, pop the mud blobs, splat the boss.
    /// The lot is hidden while it runs and the arena is built at the origin; everything is in web coordinates.
    /// </summary>
    public static class Blaster
    {
        const float ArX = 6f, ZCannon = 17f, ZBoss = -21f, ZSpawn = -18f;
        public static bool Active { get; private set; }
        static State st;
        static SaveData S => Store.S;

        class Gate { public Transform tr; public WLabel lbl; public Material pane; public Color paneCol; public float x, z, w, phase, cd; public string label; public bool good, moving; public int id; }
        struct Bubble { public float x, z, vx; public int mask; }
        struct Blob { public float x, z, w; }

        class State
        {
            public int level, hearts = 3, popped;
            public float t = 60f, hp, hpMax, cx, tx, fireT, spawnT = 1.5f, charge, megaZ, megaX, bossWob, spawnEvery, blobSpeed, fireEvery;
            public bool firing, megaOn, over;
            public readonly List<Bubble> bubbles = new List<Bubble>();
            public readonly List<Blob> blobs = new List<Blob>();
            public readonly List<Gate> gates = new List<Gate>();
            public Transform root, cannon, boss, mega;
            public Mesh blobMesh;
            public Material bubbleMat, blobMat;
            public VisualElement hpFill, heartsEl, mg;
            public Label time, count, hint;
            public RingProgress megaRing;
        }

        // ---------------- tickets ----------------
        static int MaxTickets() => Cfg.BaseTickets + S.gemUp.tickets;
        public static void RefillTickets()
        {
            var b = S.blaster; long now = U.NowMs, every = (long)(Cfg.TicketEvery * 1000);
            if (b.lastTick == 0) b.lastTick = now;
            if (b.tickets >= MaxTickets()) { b.lastTick = now; return; }
            long n = (now - b.lastTick) / every;
            if (n > 0) { b.tickets = (int)Math.Min(MaxTickets(), b.tickets + n); b.lastTick += n * every; }
        }
        static double NextTicketIn() => Math.Max(0, Cfg.TicketEvery * 1000 - (U.NowMs - S.blaster.lastTick)) / 1000.0;

        static string BossName(int l) => l % 5 == 0 ? (l % 10 == 0 ? "Swamp King" : "Mud Truck") : "Mud Monster";
        static double WinCash(int l) => Game.RewardFor(3 + Math.Min(l, 20) * 0.5) * (l % 5 == 0 ? 2 : 1);
        static int WinGems(int l) => 3 + l / 2 + (l % 5 == 0 ? 5 : 0);

        public static PanelDef Panel()
        {
            RefillTickets();
            var b = S.blaster; int lvl = b.level; string boss = BossName(lvl);
            return new PanelDef
            {
                title = "Bubble Blaster", icon = "blaster", sub = $"Level {lvl} · {boss}",
                render = (body, cur) =>
                {
                    int adLeft = Cfg.Ads.LimitTicket - Store.Caps().ticket;
                    body.Add(UiKit.Txt($"Drag to aim the soap cannon. Bubbles that pass through <color=#1a8f2a><b>green gates</b></color> multiply, <color=#e8383d><b>red gates</b></color> pop them. Pop the mud blobs before they reach you, then splat the <b>{boss}</b>!", "note big-note"));
                    var next = S.blaster.tickets >= MaxTickets() ? "Full" : "Next ticket in " + U.FmtTime(NextTicketIn());
                    var row = UiKit.Row("blaster", $"Tickets: {S.blaster.tickets}/{MaxTickets()}", next, null, UiKit.Button("+1", "ad purple" + (adLeft > 0 ? "" : " gray"), () =>
                    {
                        if (Store.Caps().ticket >= Cfg.Ads.LimitTicket) { Hud.Toast("No more bonus tickets today", "clock"); return; }
                        Ads.Rewarded("blaster_ticket", ok => { if (!ok) return; Store.Caps().ticket++; S.blaster.tickets++; Sfx.Play("reward"); Hud.Rerender(); });
                    }));
                    body.Add(row);
                    body.Add(UiKit.Row("trophy", "Win reward", $"${U.Fmt(WinCash(lvl))} and {WinGems(lvl)} gems"));
                    body.Add(UiKit.El("btn-row", UiKit.Button("Play", "big" + (S.blaster.tickets > 0 ? "" : " gray"), () =>
                    {
                        if (S.blaster.tickets <= 0) { Hud.Toast(Ads.Free ? "No tickets left. Tap +1 for a bonus ticket!" : "No tickets left. Watch an ad for one more!", "blaster"); return; }
                        S.blaster.tickets--;
                        if (S.blaster.tickets == MaxTickets() - 1) S.blaster.lastTick = U.NowMs;
                        Hud.ClosePanel(true);
                        Start();
                    })));
                    cur.tick = () =>
                    {
                        var sub = row.Q(className: "r-sub") as Label;
                        if (sub != null) sub.text = S.blaster.tickets >= MaxTickets() ? "Full" : "Next ticket in " + U.FmtTime(NextTicketIn());
                    };
                },
            };
        }

        // ---------------- scene ----------------
        static readonly uint[] MudCols = { 0x6b4a2b, 0x7a5532, 0x8b6a3e, 0x5e3f22, 0x7d5a35 };
        static Transform Build(int level)
        {
            var root = new GameObject("BubbleBlaster").transform;
            var b = new MeshKit();
            b.Box(14, 1, 46, U.Hex(0xeef3fb), 0, -0.5f, -2);
            for (int i = 0; i < 12; i++) b.Box(14, 0.02f, 1.6f, U.Hex(i % 2 == 1 ? 0xdfe9f7u : 0xeef3fbu), 0, 0.005f, 16 - i * 3.6f);
            foreach (var sx in new[] { -1f, 1f })
            {
                b.Box(0.6f, 0.9f, 46, U.Hex(0x2563eb), sx * 7.2f, 0.2f, -2);
                b.Box(0.62f, 0.2f, 46.2f, Color.white, sx * 7.2f, 0.68f, -2);
            }
            b.Box(14, 0.03f, 8, U.Hex(0x7a5532), 0, 0.01f, -21);
            for (int i = 0; i < 14; i++) b.Ico(U.Rand(0.4f, 1.0f), U.Hex(U.Pick(MudCols)), U.Rand(-6.5f, 6.5f), 0.1f, U.Rand(-24f, -17f), 1f, 0.35f, 1f);
            b.Box(200, 0.1f, 200, U.Hex(0x7cc850), 0, -1.05f, 0);
            b.BuildObject("Arena", root, false);
            for (int i = 0; i < 40; i++)
            {
                float sx = U.Chance(0.5f) ? -1 : 1;
                var tr = Art.Spawn("Props/tree_" + U.RandI(0, 3), root).transform;
                tr.position = U.P(sx * U.Rand(10f, 40f), 0f, U.Rand(-60f, 25f));
                tr.localScale = Vector3.one * U.Rand(1f, 1.6f);
            }
            return root;
        }

        static Transform MakeCannon(Transform root)
        {
            var b = new MeshKit();
            b.Box(2.4f, 0.6f, 2.0f, U.Hex(0x9aa3b1), 0, 0.3f, 0);
            b.Cylinder(0.9f, 1.0f, 0.6f, 16, U.Hex(0x2563eb), 0, 0.9f, 0);
            b.Box(0.9f, 0.9f, 2.6f, U.Hex(0x2f63e6), 0, 1.45f, -1.0f);
            b.Box(1.0f, 1.0f, 0.3f, U.Hex(0x9fdcff), 0, 1.6f, -2.35f);
            b.Ico(0.55f, U.Hex(0xff6fb1), 0, 2.0f, 0.6f);
            var c = b.BuildObject("Cannon", root).transform;
            c.position = U.P(0, 0, ZCannon);
            return c;
        }

        static Transform MakeBoss(int level, Transform root)
        {
            bool big = level % 5 == 0;
            float s = big ? 1.35f : 1f;
            var b = new MeshKit();
            b.Ico(3.2f * s, U.Hex(0x6b4a2b), 0, 2.6f * s, 0, 1f, 0.8f, 1f);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                b.Ico(U.Rand(0.7f, 1.2f) * s, U.Hex(U.Pick(MudCols)), Mathf.Cos(a) * 2.8f * s, U.Rand(0.6f, 3.6f) * s, Mathf.Sin(a) * 2.2f * s);
            }
            foreach (var sx in new[] { -1f, 1f })
            {
                b.Ico(0.75f * s, Color.white, sx * 1.1f * s, 3.5f * s, 2.4f * s);
                b.Ico(0.35f * s, U.Hex(0x1b1b1b), sx * 1.0f * s, 3.4f * s, 3.05f * s);
                b.Box(1.4f * s, 0.3f * s, 0.3f * s, U.Hex(0x3a2614), sx * 1.1f * s, 4.4f * s + 0.15f, 2.6f * s);
            }
            b.Box(2.4f * s, 0.7f * s, 0.6f * s, U.Hex(0x2a1508), 0, 1.9f * s, 2.7f * s);
            for (int i = 0; i < 4; i++) b.Cylinder(0.18f * s, 0f, 0.4f * s, 4, Color.white, (-0.75f + i * 0.5f) * s, 2.1f * s, 2.95f * s);
            if (level % 10 == 0)
            {
                b.Cylinder(1.0f, 1.1f, 0.6f, 10, U.Hex(0xffc92e), 0, 5.6f * s, 0);
                for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2f; b.Cylinder(0f, 0.22f, 0.6f, 5, U.Hex(0xffc92e), Mathf.Cos(a) * 0.9f, 6.2f * s, Mathf.Sin(a) * 0.9f); }
            }
            else if (big)
                foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f }) b.Box(0.7f, 1.8f, 1.8f, U.Hex(0x25282e), sx * 2.6f, 0.9f, sz * 1.6f);
            var g = new GameObject("Boss").transform;
            g.SetParent(root, false);
            g.position = U.P(0, 0, ZBoss);
            b.BuildObject("BossMesh", g);
            return g;
        }

        static Func<double> Mulberry(uint a)
        {
            return () =>
            {
                unchecked
                {
                    a += 0x6D2B79F5;
                    uint t = a;
                    t = (t ^ (t >> 15)) * (1 | t);
                    t = (t + (t ^ (t >> 7)) * (61 | t)) ^ t;
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            };
        }

        static void MakeGates(int level, Transform root)
        {
            float[] rows = { 8.5f, 1f, -6.5f, -12.5f };
            var rng = Mulberry((uint)(level * 977 + 13));
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (ri == 3 && level < 3) continue;
                float z = rows[ri];
                bool split = rng() < 0.5;
                string Pick(bool good) => good ? new[] { "x2", "x2", "x3", "+10", "+5" }[(int)(rng() * 5)] : new[] { "-5", "÷2", "-10" }[(int)(rng() * 3)];
                void Mk(float x, float w, string label, bool moving)
                {
                    bool good = label[0] == 'x' || label[0] == '+';
                    var g = new Gate { x = x, z = z, w = w, label = label, good = good, moving = moving, phase = (float)rng() * 6f, id = st.gates.Count };
                    g.tr = new GameObject("Gate_" + label).transform;
                    g.tr.SetParent(root, false);
                    g.tr.position = U.P(x, 0, z);
                    g.paneCol = good ? new Color(0.27f, 0.76f, 0.23f, 0.55f) : new Color(0.91f, 0.22f, 0.24f, 0.55f);
                    g.pane = Art.Make(g.paneCol, null, false, true, true, true);
                    var pane = new GameObject("Pane");
                    pane.transform.SetParent(g.tr, false);
                    pane.transform.localPosition = new Vector3(0, 1.2f, 0);
                    pane.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    pane.transform.localScale = new Vector3(w, 2.2f, 1);
                    pane.AddComponent<MeshFilter>().sharedMesh = Art.Quad;
                    var pr = pane.AddComponent<MeshRenderer>(); pr.sharedMaterial = g.pane; pr.shadowCastingMode = ShadowCastingMode.Off;
                    var pb = new MeshKit();
                    var post = U.Hex(good ? 0x46c23au : 0xe8383du);
                    pb.Box(0.3f, 2.6f, 0.3f, post, -w / 2f, 1.3f, 0); pb.Box(0.3f, 2.6f, 0.3f, post, w / 2f, 1.3f, 0);
                    pb.Box(w + 0.3f, 0.14f, 0.3f, Color.white, 0, 2.62f, 0);
                    pb.BuildObject("Posts", g.tr);
                    var gg = g;
                    g.lbl = WorldUI.LabelIn(Hud.BlasterLayer, () => new Vector3(gg.x, 1.6f, gg.z));
                    g.lbl.Set(label, e => e.Add(UiKit.Txt(label, good ? "gate-lbl good" : "gate-lbl bad")));
                    st.gates.Add(g);
                }
                if (split) { Mk(-3.2f, 5.4f, Pick(true), false); Mk(3.2f, 5.4f, Pick(rng() < 0.35), false); }
                else Mk(0f, 5.6f, Pick(rng() < 0.85), level > 1 && rng() < 0.6);
            }
        }

        // ---------------- run ----------------
        static void Start()
        {
            int level = S.blaster.level;
            bool big = level % 5 == 0;
            st = new State
            {
                level = level,
                hp = Mathf.Round((780 + level * 95) * (big ? 1.3f : 1f)),
                spawnEvery = Mathf.Max(0.2f, 0.42f - level * 0.012f),
                blobSpeed = 2.4f + Mathf.Min(level, 20) * 0.08f,
                fireEvery = 1f / (6f + Mathf.Min(level, 20) * 0.3f),
            };
            st.hpMax = st.hp;
            Active = true;
            Game.Paused = true;
            SetLotVisible(false);
            st.root = Build(level);
            st.cannon = MakeCannon(st.root);
            st.boss = MakeBoss(level, st.root);
            MakeGates(level, st.root);
            var mb = new MeshKit();
            mb.Ico(0.5f, U.Hex(0x7a5532), 0, 0.4f, 0, 1f, 0.85f, 1f);
            foreach (var sx in new[] { -1f, 1f }) { mb.Ico(0.12f, Color.white, sx * 0.16f, 0.62f, 0.36f); mb.Ico(0.06f, U.Hex(0x1b1b1b), sx * 0.16f, 0.62f, 0.46f); }
            st.blobMesh = mb.Build("Blob");
            st.blobMat = Art.VertexColor();
            st.bubbleMat = Art.Make(U.Hex(0x9fe0ff));
            st.bubbleMat.SetColor("_EmissionColor", U.Hex(0x1a4a6a));
            var mega = new GameObject("MegaBubble");
            mega.transform.SetParent(st.root, false);
            mega.transform.localScale = Vector3.one * 1.3f;
            mega.AddComponent<MeshFilter>().sharedMesh = Art.Ico;
            var mm = Art.Make(new Color(0.75f, 0.92f, 1f, 0.85f), null, false, false, true);
            mm.SetColor("_EmissionColor", U.Hex(0x2a6a9a));
            mega.AddComponent<MeshRenderer>().sharedMaterial = mm;
            mega.SetActive(false);
            st.mega = mega.transform;
            // lighting and camera for the arena
            var th = new WorldBuilder.Theme { id = "blaster", sky = 0x8fd3ff, fog = 0x8fd3ff, fogNear = 60, fogFar = 120, hemi = 0.75f, sun = 0.55f, sunColor = 0xfff1dc, hemiSky = 0xeaf6ff, hemiGround = 0x8aa070 };
            WorldBuilder.ApplyLighting(th, SudsGame.I.Sun, CameraRig.I.Cam);
            Hud.ShowHud(false);
            Ads.Banner(false);
            BuildHud();
            Sfx.Play("whoosh");
            Render(0f);
        }

        static void SetLotVisible(bool on)
        {
            if (WorldBuilder.Root) WorldBuilder.Root.gameObject.SetActive(on);
            if (Game.Root) Game.Root.gameObject.SetActive(on);
            Fx.ClearParticles();
        }

        static void BuildHud()
        {
            var L = Hud.BlasterLayer;
            L.Clear();
            L.style.display = DisplayStyle.Flex;
            var top = UiKit.El("bb-top");
            var boss = UiKit.Bar("bb-boss", 1f, null);
            st.hpFill = boss.Q(className: "fill");
            boss.Add(UiKit.Txt($"{BossName(st.level)} · Lv {st.level}", "bb-boss-name"));
            top.Add(boss);
            var row = UiKit.El("bb-row");
            st.heartsEl = UiKit.El("bb-row");
            for (int i = 0; i < 3; i++) st.heartsEl.Add(UiKit.Icon("heart", "icon bb-heart"));
            row.Add(st.heartsEl);
            row.Add(UiKit.Icon("clock"));
            st.time = UiKit.Txt("60"); row.Add(st.time);
            row.Add(UiKit.Icon("bubble"));
            st.count = UiKit.Txt("0"); row.Add(st.count);
            top.Add(row);
            L.Add(top);
            st.mg = UiKit.Btn("bb-charge", FireMega);
            st.mg.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            st.megaRing = new RingProgress();
            st.megaRing.style.position = Position.Absolute;
            st.megaRing.style.left = 0; st.megaRing.style.top = 0; st.megaRing.style.width = 84; st.megaRing.style.height = 84;
            st.mg.Add(st.megaRing);
            var inner = UiKit.El("bb-charge-inner");
            inner.Add(UiKit.Txt("MEGA\nBUBBLE"));
            st.mg.Add(inner);
            L.Add(st.mg);
            st.hint = UiKit.Txt("Hold and drag to shoot!", "bb-hint");
            L.Add(st.hint);
            var quit = UiKit.Button("Give up", "red bb-quit", () => Finish(false));
            quit.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            L.Add(quit);
            Hearts();
        }

        static float PointerToX(float panelX)
        {
            float w = Hud.Root.layout.width;
            float nx = panelX / Mathf.Max(1f, w) * 2f - 1f;
            return U.Clamp(nx * ArX * 1.25f, -ArX + 0.8f, ArX - 0.8f);
        }
        public static void PointerDown(float panelX)
        {
            if (st == null || st.over) return;
            st.firing = true; st.tx = PointerToX(panelX);
            if (st.hint != null) { st.hint.RemoveFromHierarchy(); st.hint = null; }
        }
        public static void PointerMove(float panelX, bool mouse) { if (st != null && (st.firing || mouse)) st.tx = PointerToX(panelX); }
        public static void PointerUp() { if (st != null) st.firing = false; }
        public static void Keys(float steer, bool fire, bool mega)
        {
            if (st == null || st.over) return;
            if (steer != 0f) st.tx = U.Clamp(st.tx + steer * 12f * Time.deltaTime, -ArX + 0.8f, ArX - 0.8f);
            if (fire) st.firing = true;
            if (mega) FireMega();
        }
        public static void KeyFireUp() { if (st != null) st.firing = false; }

        static void Hearts()
        {
            if (st?.heartsEl == null) return;
            for (int i = 0; i < st.heartsEl.childCount; i++) st.heartsEl[i].EnableInClassList("lost", i >= st.hearts);
        }
        static void FireMega()
        {
            if (st == null || st.over || st.charge < 1f || st.megaOn) return;
            st.charge = 0f; st.megaOn = true; st.megaZ = ZCannon - 2f; st.megaX = st.cx;
            st.mega.gameObject.SetActive(true); Sfx.Play("whoosh"); CameraRig.I.Shake(0.2f);
        }
        static void AddBubble(float x, float z, int mask, float vx = 0f) { if (st.bubbles.Count < 680) st.bubbles.Add(new Bubble { x = x, z = z, mask = mask, vx = vx }); }

        public static void Update(float dt)
        {
            if (!Active || st == null) return;
            if (st.over) { Render(dt); return; }
            st.t -= dt;
            st.cx = U.Damp(st.cx, st.tx, 14f, dt);
            st.cannon.position = U.P(st.cx, 0, ZCannon);
            // fire
            st.fireT -= dt;
            if (st.firing && st.fireT <= 0f) { st.fireT = st.fireEvery; AddBubble(st.cx + U.Rand(-0.15f, 0.15f), ZCannon - 2.4f, 0); Sfx.Play("pew"); st.cannon.localScale = new Vector3(1.04f, 0.96f, 1.04f); }
            st.cannon.localScale = Vector3.Lerp(st.cannon.localScale, Vector3.one, 0.3f);
            // gates
            foreach (var g in st.gates)
            {
                if (g.moving) { g.x = Mathf.Sin(st.t * 0.9f + g.phase) * 3.2f; g.tr.position = U.P(g.x, 0, g.z); }
                g.cd = Mathf.Max(0f, g.cd - dt);
                var c = g.paneCol; c.a *= g.cd > 0f ? 0.4f : 1f; g.pane.SetColor("_BaseColor", c);
            }
            // bubbles
            for (int i = st.bubbles.Count - 1; i >= 0; i--)
            {
                if (i >= st.bubbles.Count) continue;
                var b = st.bubbles[i];
                float pz = b.z;
                b.z -= 9.5f * dt; b.x += b.vx * dt; b.vx *= 0.9f;
                b.x = U.Clamp(b.x, -ArX + 0.4f, ArX - 0.4f);
                bool removed = false;
                foreach (var g in st.gates)
                {
                    if ((b.mask & (1 << g.id)) != 0) continue;
                    if (pz >= g.z && b.z < g.z && Mathf.Abs(b.x - g.x) < g.w / 2f)
                    {
                        b.mask |= 1 << g.id;
                        string L = g.label;
                        if (L[0] == 'x') { int n = int.Parse(L.Substring(1)) - 1; for (int k = 0; k < n; k++) AddBubble(b.x + U.Rand(-0.6f, 0.6f), b.z - 0.3f, b.mask, U.Rand(-2f, 2f)); }
                        else if (L[0] == '+') { if (g.cd <= 0f) { g.cd = 1.2f; int n = int.Parse(L.Substring(1)); for (int k = 0; k < n; k++) AddBubble(g.x + U.Rand(-g.w / 2f, g.w / 2f), b.z - U.Rand(0.2f, 1.2f), b.mask); Sfx.Play("bubble"); } }
                        else if (L == "÷2") { if (U.Chance(0.5f)) { removed = true; Pop(b.x, b.z, false); break; } }
                        else { removed = true; Pop(b.x, b.z, false); break; }
                    }
                }
                if (removed) { st.bubbles.RemoveAt(i); continue; }
                if (b.z < ZBoss + 3.5f)
                {
                    if (Mathf.Abs(b.x) < 4.2f) { st.bubbles.RemoveAt(i); HitBoss(1, b.x); if (st.over) return; continue; }
                    if (b.z < ZBoss) { st.bubbles.RemoveAt(i); continue; }
                }
                st.bubbles[i] = b;
            }
            // spawn blobs
            st.spawnT -= dt;
            float enrage = 1f + (1f - st.hp / st.hpMax) * 0.8f;
            if (st.spawnT <= 0f)
            {
                st.spawnT = st.spawnEvery / enrage;
                int n = 2 + (UnityEngine.Random.value < 0.3f + Mathf.Min(st.level, 20) * 0.02f ? 2 : 0);
                for (int k = 0; k < n; k++) st.blobs.Add(new Blob { x = U.Rand(-ArX + 1f, ArX - 1f), z = ZSpawn - U.Rand(0f, 1f), w = UnityEngine.Random.value * 6f });
            }
            // blobs move + collide
            for (int i = st.blobs.Count - 1; i >= 0; i--)
            {
                var m = st.blobs[i];
                m.z += st.blobSpeed * dt; m.w += dt * 8f;
                bool dead = false;
                for (int j = st.bubbles.Count - 1; j >= 0; j--)
                {
                    var b = st.bubbles[j];
                    if (Mathf.Abs(b.z - m.z) < 0.7f && Mathf.Abs(b.x - m.x) < 0.7f) { st.bubbles.RemoveAt(j); dead = true; break; }
                }
                if (!dead && st.megaOn && Mathf.Abs(st.megaZ - m.z) < 1.6f && Mathf.Abs(st.megaX - m.x) < 1.8f) dead = true;
                if (dead) { st.blobs.RemoveAt(i); Pop(m.x, m.z, true); st.charge = Mathf.Min(1f, st.charge + 0.035f); st.popped++; continue; }
                if (m.z > ZCannon - 1f)
                {
                    st.blobs.RemoveAt(i); st.hearts--; Hearts(); CameraRig.I.Shake(0.5f); Sfx.Play("splat"); Sfx.Play("error");
                    Fx.Debris(new Vector3(m.x, 1f, m.z), new Vector3(0, 3, 0), 0.6f, 0.5f, U.Hex(0x6b4a2b));
                    if (st.hearts <= 0) { Finish(false); return; }
                    continue;
                }
                st.blobs[i] = m;
            }
            // mega bubble
            if (st.megaOn)
            {
                st.megaZ -= 7f * dt;
                st.mega.position = U.P(st.megaX, 1.4f + Mathf.Sin(st.t * 10f) * 0.1f, st.megaZ);
                st.mega.rotation = U.Yaw(st.t * 3f);
                if (st.megaZ < ZBoss + 3.8f)
                {
                    st.megaOn = false; st.mega.gameObject.SetActive(false);
                    for (int k = 0; k < 30; k++) Fx.Debris(new Vector3(st.megaX + U.Rand(-2f, 2f), U.Rand(1f, 4f), ZBoss + 3f), new Vector3(U.Rand(-4f, 4f), U.Rand(2f, 7f), U.Rand(-1f, 4f)), 0.8f, U.Rand(0.3f, 0.75f), U.Hex(0xeaf7ff));
                    Sfx.Play("boom"); CameraRig.I.Shake(0.6f);
                    HitBoss(25, st.megaX);
                    if (st.over) return;
                }
            }
            if (st.t <= 0f) { Finish(false); return; }
            Render(dt);
            // HUD
            st.hpFill.style.width = Length.Percent(Mathf.Max(0f, st.hp / st.hpMax * 100f));
            st.time.text = Mathf.CeilToInt(st.t).ToString();
            st.count.text = st.bubbles.Count.ToString();
            st.megaRing.Value = st.charge;
            st.mg.EnableInClassList("ready", st.charge >= 1f && !st.megaOn);
            if (st.charge >= 1f && !st.megaOn) { float s = 1f + Mathf.Max(0f, Mathf.Sin(U.Clock * Mathf.PI / 0.3f)) * 0.04f; st.mg.style.scale = new Scale(new Vector3(s, s, 1f)); }
            else st.mg.style.scale = new Scale(Vector3.one);
        }

        static void HitBoss(int dmg, float x)
        {
            st.hp -= dmg; st.bossWob = 1f;
            if (UnityEngine.Random.value < 0.5f || dmg > 1) Fx.Debris(new Vector3(x + U.Rand(-0.5f, 0.5f), U.Rand(2f, 4.5f), ZBoss + 2.8f), new Vector3(U.Rand(-3f, 3f), U.Rand(2f, 5f), U.Rand(1f, 3f)), 0.7f, 0.35f, U.Hex(0x6b4a2b));
            Sfx.Play("splat");
            if (st.hp <= 0f && !st.over) { st.hp = 0f; Finish(true); }
        }
        static void Pop(float x, float z, bool mud)
        {
            for (int k = 0; k < 3; k++) Fx.Debris(new Vector3(x + U.Rand(-0.2f, 0.2f), 0.6f, z), new Vector3(U.Rand(-2f, 2f), U.Rand(1f, 3f), U.Rand(-2f, 2f)), 0.35f, 0.27f, U.Hex(0xeaf7ff));
            if (mud) for (int k = 0; k < 3; k++) Fx.Debris(new Vector3(x, 0.5f, z), new Vector3(U.Rand(-3f, 3f), U.Rand(2f, 4f), U.Rand(-2f, 2f)), 0.5f, 0.25f, U.Hex(0x6b4a2b));
            Sfx.Play("pop");
        }

        static readonly Matrix4x4[] mats = new Matrix4x4[1023];
        static void Render(float dt)
        {
            if (st == null) return;
            // bubbles and blobs are drawn instanced (no GameObjects)
            var bubbleScale = Vector3.one * 0.32f;
            int n = 0;
            for (int i = 0; i < st.bubbles.Count && n < mats.Length; i++)
            {
                var b = st.bubbles[i];
                mats[n++] = Matrix4x4.TRS(U.P(b.x, 0.55f + Mathf.Sin(b.z * 2f + i) * 0.06f, b.z), Quaternion.identity, bubbleScale);
            }
            var rp = new RenderParams(st.bubbleMat) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            if (n > 0) Graphics.RenderMeshInstanced(rp, Art.Ico, 0, mats, n);
            n = 0;
            for (int i = 0; i < st.blobs.Count && n < mats.Length; i++)
            {
                var b = st.blobs[i];
                float sq = 1f + Mathf.Sin(b.w * 2f) * 0.08f;
                mats[n++] = Matrix4x4.TRS(U.P(b.x, Mathf.Abs(Mathf.Sin(b.w)) * 0.25f, b.z), Quaternion.identity, new Vector3(sq, 1f / sq, sq));
            }
            var rp2 = new RenderParams(st.blobMat) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            if (n > 0) Graphics.RenderMeshInstanced(rp2, st.blobMesh, 0, mats, n);
            st.bossWob = U.Damp(st.bossWob, 0f, 6f, dt);
            float tt = Time.time, w = 1f + Mathf.Sin(tt * 1000f / 60f) * st.bossWob * 0.06f;
            st.boss.localScale = new Vector3(w, 1f / w, w);
            st.boss.rotation = U.Yaw(Mathf.Sin(tt * 1000f / 700f) * 0.15f);
            // camera
            var cam = CameraRig.I.Cam;
            bool tall = cam.aspect < 0.8f;
            cam.fieldOfView = tall ? 58f : 42f;
            cam.transform.position = U.P(st.cx * 0.15f, tall ? 26f : 21f, 31f);
            cam.transform.LookAt(U.P(0f, 0f, -3f));
        }

        static void Finish(bool win)
        {
            if (st == null || st.over) return;
            st.over = true; st.firing = false;
            int level = st.level;
            if (win)
            {
                Sfx.Play("win"); S.blaster.level++; S.stats.blasterWins++;
                for (int k = 0; k < 60; k++) Fx.Debris(new Vector3(U.Rand(-4f, 4f), U.Rand(1f, 6f), ZBoss + 2f), new Vector3(U.Rand(-5f, 5f), U.Rand(3f, 9f), U.Rand(-1f, 5f)), 1.2f, U.Rand(0.3f, 0.6f), U.Hex(0xeaf7ff));
                st.boss.gameObject.SetActive(false);
            }
            else Sfx.Play("lose");
            double cash = win ? WinCash(level) : Math.Round(WinCash(level) * 0.2);
            int gems = win ? WinGems(level) : 0;
            int popped = st.popped;
            Sched.After(0.9f, () => Hud.OpenPanel("blasterResult", new PanelArgs { win = win, cash = cash, gems = gems, level = level, popped = popped }));
        }

        public static PanelDef ResultPanel(PanelArgs o) => new PanelDef
        {
            title = o.win ? "Mud Monster splatted!" : "The mud won this time", icon = o.win ? "trophy" : "blaster", noClose = true,
            render = (b, cur) =>
            {
                int x3Left = Cfg.Ads.LimitBlasterX3 - Store.Caps().blasterX3;
                var hero = UiKit.El("hero-num");
                hero.Add(UiKit.Icon("cash")); hero.Add(UiKit.Txt(U.Fmt(o.cash)));
                if (o.gems > 0) { hero.Add(UiKit.Icon("gem")); hero.Add(UiKit.Txt(o.gems.ToString())); }
                b.Add(hero);
                b.Add(UiKit.Txt($"{o.popped} mud blobs popped" + (o.win ? $" · next: Level {o.level + 1}" : ""), "note"));
                void Take(int m)
                {
                    Hud.ClosePanel(true); Stop();
                    Game.AddCash(o.cash * m); if (o.gems > 0) Game.AddGems(o.gems * m);
                    Hud.RewardBurst(o.cash * m, o.gems * m); Sfx.Play("reward");
                    if (m == 1) Sched.After(0.6f, () => Ads.Interstitial("blaster_end"));
                    Store.Save();
                }
                var row = UiKit.El("btn-row");
                row.Add(UiKit.Button("Collect", "big", () => Take(1)));
                if (x3Left > 0) row.Add(UiKit.Button("Collect x3", "big ad purple", () => Ads.Rewarded("blaster_x3", ok => { if (!ok) return; Store.Caps().blasterX3++; Take(3); })));
                b.Add(row);
            },
        };

        static void Stop()
        {
            Active = false;
            if (st != null)
            {
                foreach (var g in st.gates) { g.lbl?.Remove(); if (g.pane) Object.Destroy(g.pane); }
                if (st.root) Object.Destroy(st.root.gameObject);
                if (st.blobMesh) Object.Destroy(st.blobMesh);
                if (st.bubbleMat) Object.Destroy(st.bubbleMat);
            }
            st = null;
            Hud.BlasterLayer.Clear();
            Hud.BlasterLayer.style.display = DisplayStyle.None;
            SetLotVisible(true);
            CameraRig.I.Cam.fieldOfView = CameraRig.I.fov;
            WorldBuilder.Relight(SudsGame.I.Sun, CameraRig.I.Cam);
            Hud.ShowHud(true);
            Ads.Banner(Cfg.Ads.Banner);
            Game.Paused = false;
            Hud.RefreshAll();
            if (Game.Player != null) CameraRig.I.Follow(Game.CamTarget(), 0.016f, true);
        }

        internal static void ResetStatics() { Active = false; st = null; }

        /// <summary>Leave the mini-game immediately (e.g. on a progress reset).</summary>
        public static void ForceStop() { if (Active) Stop(); }
    }
}
