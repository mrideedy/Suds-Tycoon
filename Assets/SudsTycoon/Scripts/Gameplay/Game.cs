using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SudsTycoon
{
    /// <summary>Offline earnings computed on boot / resume.</summary>
    public class OfflineResult { public double amount, secs; public bool capped; public float capH, autom; }

    /// <summary>
    /// The simulation (port of game.js): locations, economy, spawning, payouts, pads, upgrades, staff tasks,
    /// events and quests. Everything here works in web coordinates; objects convert at the transform boundary.
    /// </summary>
    public static class Game
    {
        public static LocDef L;
        public static LocSave Ls;
        public static int LocIdx;
        public static readonly List<Lane> Lanes = new List<Lane>();
        public static readonly Dictionary<string, Rack> Racks = new Dictionary<string, Rack>();
        public static readonly List<Worker> Workers = new List<Worker>();
        public static readonly List<Car> Cars = new List<Car>();
        public static readonly List<Pad> Pads = new List<Pad>();
        public static readonly Dictionary<string, Building> Buildings = new Dictionary<string, Building>();
        public static Player Player;
        public static Transform Root;
        public static bool Paused, Running, RushHour, PolishingPlayer;
        public static string Bottleneck;
        public static QuestDef Quest;
        public static int Streak;
        public static float T;
        /// <summary>Joystick / keyboard direction in web x / z, set by GameInput.</summary>
        public static Vector2 Move;
        public static bool MoveActive => Move.sqrMagnitude > 0.0001f;

        static float spawnT, rushT, nextRush, nextGift, nextGolden, pickT, dropT, saveT, bnT;
        static bool goldenDue;
        static Balloon balloon;
        static Transform arrow, ring;
        static Material ringMat;
        static bool hooked;

        static SaveData S => Store.S;
        static double M => L.mult;
        static float Clock => U.Clock;
        static long Now => U.NowMs;

        // =====================================================================
        // Economy formulas (see the design doc)
        // =====================================================================
        public static int Lv(string t) => Ls != null && Ls.st.TryGetValue(t, out var v) ? v : 1;
        /// <summary>Business upgrade level (kept for good, the same in every city).</summary>
        public static int Biz(string id) => S.Biz(id);
        public static bool Built(string id) => Ls != null && Ls.pads.Contains(id);
        public static bool Boost(string name) => S.Boost(name) > Now;
        /// <summary>City perk (Cfg.Perks): yours for good once that city has been opened.</summary>
        public static bool HasPerk(string city) => S.locs.TryGetValue(city, out var ls) && ls.started;

        /// <summary>Whole dollars a machine adds to every car at a level (before the city price): value + inc per level.</summary>
        public static int StationValueAt(string t, int lv) { var c = Cfg.Stations[t]; return c.value + c.inc * (lv - 1); }
        public static float StationValue(string t) => StationValueAt(t, Lv(t));
        /// <summary>What a machine adds to each car in this city, as shown on the upgrade rows ("$7 per car").</summary>
        public static double MachinePay(string t, int lv) => StationValueAt(t, lv) * M;
        public static float StationTime(string t, bool boosts = true)
        {
            int l = Lv(t);
            float time = Mathf.Max(0.45f, Cfg.Stations[t].time * Mathf.Pow(0.95f, l - 1) * (HasPerk("alps") ? 0.85f : 1f));
            return boosts && Boost("wash") ? Mathf.Max(0.3f, time * 0.5f) : time;   // Turbo Wash (ad boost)
        }
        public static double StationUpCost(string t) => U.Nice(Cfg.Stations[t].upCost * M * Math.Pow(Cfg.UpGrowth, Lv(t) - 1));
        public static double BizCost(string id) { var d = Cfg.BizById(id); return U.Nice(d.cost * M * Math.Pow(d.growth, Biz(id))); }
        public static double IncomeMult()
        {
            var s = S;
            return (1 + 0.1 * Biz("price")) * (Built("landmark") ? 1.25 : 1) * (1 + 0.1 * s.Hq("sponge")) * (s.perks.starter ? 1.5 : 1) * (s.perks.vip ? 2 : 1) * (HasPerk("mars") ? 1.5 : 1);
        }
        public static double PriceMult() => M * IncomeMult() * (Boost("income") ? 2 : 1);
        public static double BasePrice() => Cfg.Stations["brush"].value * M;
        static float ArrivalAt(int carsLv)
        {
            float iv = L.spawn / (1f + 0.1f * carsLv + (HasPerk("desert") ? 0.1f : 0f));
            if (Built("tubeman")) iv *= 0.85f;
            if (Built("billboard")) iv *= 0.8f;
            return iv;
        }
        public static float ArrivalInterval(bool boosts = true)
        {
            float iv = ArrivalAt(Biz("cars"));
            if (!boosts) return iv;
            if (RushHour) iv /= 3f;
            if (Boost("rush")) iv /= 3f;   // Car Rush (ad boost)
            return iv;
        }
        /// <summary>Customers a minute with a More Customers level (shown on the Business row).</summary>
        public static int CarsPerMin(int carsLv) => L == null ? 0 : Mathf.RoundToInt(60f / ArrivalAt(carsLv));
        public static int QueueCap() => 3 + (Built("queue") ? 3 : 0) + (HasPerk("london") ? 3 : 0) + (RushHour || Boost("rush") ? 2 : 0);
        /// <summary>Waiting drivers lose patience slower during rush hour and Car Rush.</summary>
        public static float PatienceDrain() => RushHour || Boost("rush") ? 0.6f : 1f;
        public static float Patience(CarDef def) => (def.pat + (Built("queue") ? 5 : 0)) * (Built("flowers") ? 1.2f : 1f);
        public static float TipChance() => 0.15f + (Built("fountain") ? 0.1f : 0f) + (HasPerk("beach") ? 0.1f : 0f);
        public static float VipChance() => (Built("vip") ? 0.12f : 0f) + (HasPerk("paris") ? 0.05f : 0f);
        public static float MgrSpeed() => 6.2f * (1f + 0.05f * Biz("speed")) * (Boost("speed") ? 1.6f : 1f) * (HasPerk("tokyo") ? 1.2f : 1f);
        public static int MgrCarry() => 4 + Biz("carry");
        public static float Magnet() => HasPerk("downtown") ? 6.4f : 3.2f;
        public static float StaffSpeed() => 3.5f * (1f + 0.05f * Biz("speed")) * (Built("coffee") ? 1.1f : 1f);
        public static int StaffCarry() => 2 + Biz("carry") + (HasPerk("moon") ? 2 : 0);
        public static float NapMul() => (Built("coffee") ? 0.5f : 1f) * (HasPerk("snow") ? 0.05f : 1f);
        /// <summary>Golden cars pay this many times the bill (x10 after the ad).</summary>
        public static float GoldenPay() => HasPerk("dubai") ? 5f : 3f;
        /// <summary>Seconds between free Lucky Spins.</summary>
        public static float SpinEvery() => Cfg.SpinFreeEvery * (HasPerk("rio") ? 0.5f : 1f);

        /// <summary>Rough steady-state income per second, used for rewards and offline earnings.</summary>
        public static double IncomePerSec(bool withBoost = false)
        {
            double total = 0;
            var lanes = Lanes.Where(l => l.Unlocked && l.Get("pay") != null).ToList();
            float arrive = 1f / ArrivalInterval(false);
            foreach (var l in lanes)
            {
                float cycle = 0f, value = 0f;
                foreach (var st in l.Stops()) { cycle = Mathf.Max(cycle, StationTime(st.Type, false) + 1.5f); value += StationValue(st.Type); }
                float cps = Mathf.Min(1f / Mathf.Max(0.5f, cycle), arrive / lanes.Count);
                total += cps * value * 1.2;
            }
            return total * M * IncomeMult() * (withBoost && Boost("income") ? 2 : 1);
        }
        public static double RewardFor(double minutes) => U.Nice(Math.Max(IncomePerSec() * 60 * minutes, (8 + 10 * minutes) * M));

        // =====================================================================
        // Money, gems, XP
        // =====================================================================
        public static void AddCash(double v, bool silent = false)
        {
            v = Math.Max(0, v);
            var s = S; s.cash += v; s.stats.earned += v; Ls.earned += v; Cnt("earn", v);
            Hud.CashChanged(!silent);
        }
        public static void Spend(double v, bool silent = false) { S.cash = Math.Max(0, S.cash - v); Hud.CashChanged(false); }
        public static bool CanAfford(double v) => S.cash >= v - 1e-6;
        public static void AddGems(double n) { S.gems += n; Hud.GemsChanged(); }
        public static void AddXP(double n)
        {
            var s = S; s.xp += n;
            bool up = false;
            while (s.xp >= Cfg.XpFor(s.level)) { s.xp -= Cfg.XpFor(s.level); s.level++; up = true; LevelUp(s.level); }
            Hud.XpChanged(up);
        }
        static void LevelUp(int level)
        {
            int gems = Cfg.LevelGems(level);
            AddGems(gems);
            Sfx.Play("levelup"); U.Vibrate(40);
            Fx.Confetti(new Vector3(Player.Pos.x, 2.5f, Player.Pos.z), 70, 8f);
            Hud.LevelUp(level, gems);
            Bus.Emit("level", level);
        }
        public static void Cnt(string k, double v = 1) { var c = Ls.cnt; c[k] = (c.TryGetValue(k, out var o) ? o : 0) + v; }

        // =====================================================================
        // Location lifecycle
        // =====================================================================
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            // rage quits reset the wash streak
            Bus.On("car:rage", _ => { Streak = 0; Hud.Streak(0); S.stats.rage++; Cnt("rage"); });
            Bus.On("ads:pause", v => Paused = (bool)v);
        }

        public static void Load(int idx)
        {
            Hook();
            Unload();
            var s = S;
            LocIdx = idx; L = Cfg.Locations[idx]; s.loc = idx;
            Sfx.SetWorld(L.region);   // every region has its own tune
            Ls = Store.Loc(L.id);
            if (Ls.cnt == null) Ls.cnt = new Dictionary<string, double>();
            if (Ls.cash.HasValue) s.cash = Ls.cash.Value;
            // coming back to a branch: what it earned while you were away goes into its till
            double branchCash = BranchPending(Ls);
            Ls.lastCollect = 0;
            if (branchCash > 0) { s.cash += branchCash; Sched.After(1.2f, () => Hud.Toast($"While you were away this branch earned ${U.Fmt(branchCash)}", "income")); }
            WorldBuilder.Build(L.theme, SudsGame.I.Sun, CameraRig.I.Cam);
            Root = new GameObject("Location_" + L.id).transform;
            var wb = Cfg.WalkBounds;
            Nav.Init(Rect.MinMaxRect(wb.xMin - 1, wb.yMin - 1, wb.xMax + 1, wb.yMax + 1));
            Player = new Player(Root);
            bool fresh = !Ls.started;
            Ls.started = true;
            if (fresh && idx > 0)
            {
                // a new city starts with the Franchise Kit already built and a welcome gift, so you skip the repeats
                foreach (var id in Cfg.Econ.FranchiseKit) Ls.pads.Add(id);
                double gift = Math.Round(Cfg.Econ.WelcomeCash * M);
                s.cash += gift;
                Sched.After(1.6f, () => Hud.Toast($"Franchise Kit built for you, plus a ${U.Fmt(gift)} welcome gift!", "gift"));
            }
            // every city adds its own permanent perk (and some a hat): celebrate it the first time you arrive
            foreach (var p in Cfg.Perks)
                if (p.hat != null && HasPerk(p.city) && !s.hats.owned.Contains(p.hat)) s.hats.owned.Add(p.hat);
            var perk = Cfg.PerkFor(L.id);
            if (fresh && perk != null)
                Sched.After(2.6f, () =>
                {
                    if (!Running) return;
                    Sfx.Play("levelup");
                    Fx.Confetti(new Vector3(Player.Pos.x, 2.5f, Player.Pos.z), 70, 8f);
                    Hud.OpenPanel("perk", new PanelArgs { tab = perk.city });
                });
            // lanes + stations
            Lanes.Clear();
            for (int i = 0; i < Cfg.LaneZ.Length; i++) Lanes.Add(new Lane(i, Root));
            Lanes[0].Unlock(false);
            int Hop(int li, string t, int dflt) => Ls.hoppers.TryGetValue(li + ":" + t, out var v) ? v : dflt;
            Lanes[0].AddStation("brush", false, Hop(0, "brush", fresh && idx == 0 ? 0 : 4));
            Lanes[0].AddStation("pay", false);
            if (Built("lane2")) { Lanes[1].Unlock(false); Lanes[1].AddStation("brush", false, Hop(1, "brush", 4)); Lanes[1].AddStation("pay", false); }
            foreach (var def in Cfg.Pads) if (def.kind == "station" && Built(def.id)) Lanes[def.lane].AddStation(def.st, false, Hop(def.lane, def.st, 4));
            // racks
            Racks.Clear();
            int Stock(string s2, int d) => Ls.rackStock.TryGetValue(s2, out var v) ? v : d;
            Racks["soap"] = new Rack("soap", Stock("soap", fresh ? 3 : 4), Root);
            if (Built("waxRack")) Racks["wax"] = new Rack("wax", Stock("wax", 3), Root);
            if (Built("gelRack")) Racks["gel"] = new Rack("gel", Stock("gel", 3), Root);
            // buildings
            Buildings.Clear();
            Buildings["shop"] = new Building("shop", false, L.theme, Root);
            foreach (var def in Cfg.Pads)
                if ((def.kind == "building" || def.kind == "travel") && Built(def.id)) { string b = def.b ?? "travel"; Buildings[b] = new Building(b, false, L.theme, Root); }
            // staff
            Workers.Clear();
            foreach (var def in Cfg.Pads) if (def.kind == "hire" && Built(def.id)) Hire(def, false);
            // piles
            foreach (var l in Lanes) if (l.Pile != null && Ls.piles.TryGetValue(l.Idx.ToString(), out var pv)) l.Pile.Restore(pv);
            if (Buildings.TryGetValue("vending", out var vend) && Ls.trays.TryGetValue("vending", out var tv)) vend.Tray.Restore(tv);
            // pads
            Pads.Clear();
            foreach (var def in Cfg.Pads)
            {
                if (Built(def.id)) continue;
                double paid = Ls.paid.TryGetValue(def.id, out var pd) ? pd : 0;
                var pad = new Pad(def, M, paid, Root);
                if (paid > pad.Paid) { s.cash += paid - pad.Paid; Ls.paid[def.id] = pad.Paid; }   // the pad got cheaper: refund the extra
                Pads.Add(pad);
            }
            RevealPads(false);
            RebuildNav();
            Cars.Clear();
            spawnT = 2.5f; RushHour = false; Streak = 0;
            Hud.Streak(0);
            float t = Clock;
            nextGift = t + U.Rand(Cfg.GiftEvery.x, Cfg.GiftEvery.y) * 0.5f;
            nextGolden = t + U.Rand(Cfg.GoldenEvery.x, Cfg.GoldenEvery.y) * (HasPerk("city") ? 0.5f : 1f);
            nextRush = t + U.Rand(Cfg.RushEvery.x, Cfg.RushEvery.y) * (HasPerk("mumbai") ? 0.5f : 1f);
            PickQuest(false);
            CameraRig.I.Follow(CamTarget(), 0.016f, true);
            Running = true;
            Hud.OnLocation();
            Bus.Emit("location", idx);
        }

        public static void Unload()
        {
            if (L == null) return;
            SaveRuntime();
            Running = false;
            foreach (var c in Cars.ToArray()) c.Discard();
            Cars.Clear();
            foreach (var w in Workers) w.Destroy();
            Workers.Clear();
            foreach (var l in Lanes) l.Destroy();
            Lanes.Clear();
            foreach (var r in Racks.Values) r.Destroy();
            Racks.Clear();
            foreach (var b in Buildings.Values) b.Destroy();
            Buildings.Clear();
            foreach (var p in Pads) p.Hide();
            Pads.Clear();
            balloon?.Destroy(); balloon = null;
            Player?.Destroy(); Player = null;
            if (arrow) Object.Destroy(arrow.gameObject);
            if (ring) Object.Destroy(ring.gameObject);
            arrow = ring = null;
            Sched.Clear();
            Fx.ClearAll();
            WorldBuilder.Clear();
            if (Root) Object.Destroy(Root.gameObject);
            Root = null;
        }

        public static void SaveRuntime()
        {
            if (Ls == null || L == null) return;
            Ls.hoppers.Clear(); Ls.piles.Clear(); Ls.rackStock.Clear(); Ls.trays.Clear();
            foreach (var l in Lanes)
            {
                foreach (var st in l.Stations.Values) if (st.Hopper != null) Ls.hoppers[l.Idx + ":" + st.Type] = st.Hopper.Count + st.Hopper.Pending;
                if (l.Pile != null) Ls.piles[l.Idx.ToString()] = Math.Round(l.Pile.Total);
            }
            foreach (var kv in Racks) Ls.rackStock[kv.Key] = kv.Value.Stock;
            if (Buildings.TryGetValue("vending", out var v)) Ls.trays["vending"] = Math.Round(v.Tray.Total);
            Ls.cash = S.cash;
        }

        public static void RebuildNav()
        {
            var list = new List<Box2>();
            foreach (var l in Lanes) list.AddRange(l.Colliders());
            foreach (var r in Racks.Values) list.AddRange(r.Colliders());
            foreach (var b in Buildings.Values) list.AddRange(b.Coll);
            Nav.SetColliders(list);
        }

        // =====================================================================
        // Pads
        // =====================================================================
        public static Vector2 PadPos(PadDef def)
        {
            if (def.pos.HasValue) return def.pos.Value;
            if (def.kind == "station") return new Vector2(Cfg.SlotX[def.st], Cfg.LaneZ[def.lane]);
            if (def.kind == "rack") { var p = Cfg.RacksPos[def.rack]; return new Vector2(p.x - 0.4f, p.y); }
            return Vector2.zero;
        }
        public static string PadIcon(PadDef def)
        {
            if (def.kind == "station") return Cfg.Stations[def.st].icon;
            if (def.kind == "rack") return Cfg.Supplies[def.rack].icon;
            if (def.kind == "hire") return def.role == "stocker" ? "worker" : def.role == "cashier" ? "pay" : "polish";
            return def.icon ?? "star";
        }
        public static string PadName(PadDef def)
        {
            if (def.kind == "station") return (def.lane > 0 ? "Lane 2 " : "") + Cfg.Stations[def.st].name;
            if (def.kind == "rack") return Cfg.Racks[def.rack].name;
            if (def.kind == "hire") return "Hire " + (def.role == "stocker" ? "Stocker" : def.role == "cashier" ? "Cashier" : "Polisher") + (def.lane > 0 ? " (Lane 2)" : "");
            if (def.kind == "travel") { var nx = LocIdx + 1 < Cfg.Locations.Count ? Cfg.Locations[LocIdx + 1] : null; return nx != null ? "Road Trip to " + nx.name : "Grand Finale"; }
            return def.name ?? def.id;
        }
        public static void RevealPads(bool anim = true)
        {
            foreach (var p in Pads)
            {
                if (p.Shown) continue;
                if (p.Def.req.All(Built)) p.Show(anim);
            }
        }
        public static void CompletePad(Pad pad)
        {
            var def = pad.Def;
            Ls.pads.Add(def.id); Ls.paid.Remove(def.id);
            pad.Hide(); Pads.Remove(pad);
            Sfx.Play("build"); U.Vibrate(30); CameraRig.I.Shake(0.25f);
            Fx.Confetti(new Vector3(pad.Pos.x, 1.5f, pad.Pos.z), 45, 6f);
            Build(def, true);
            RebuildNav();
            Hud.RefreshCards();
            int idx = Cfg.Pads.IndexOf(def);
            AddXP(10 + idx * 3);
            Cnt("build");
            Sched.After(0.65f, () => { if (Running) RevealPads(true); });
            Hud.Toast(PadName(def) + " built!", PadIcon(def));
            CheckCityComplete();
            Store.Save();
            Bus.Emit("pad:built", def);
        }
        /// <summary>All pads built: celebrate and pay the City Complete reward once.</summary>
        static void CheckCityComplete()
        {
            if (Ls.completed || Cfg.Pads.Any(p => !Built(p.id))) return;
            Ls.completed = true;
            int gems = Cfg.Econ.CompleteGems(LocIdx);
            double cash = RewardFor(10);
            Sched.After(1.0f, () =>
            {
                if (!Running) return;
                AddGems(gems); AddCash(cash);
                Sfx.Play("levelup"); CameraRig.I.Shake(0.3f);
                Fx.Confetti(new Vector3(Player.Pos.x, 3f, Player.Pos.z), 90, 9f);
                Hud.Banner("CITY COMPLETE!", $"{L.name} is fully built: +{gems} gems", 4f);
                Hud.RewardBurst(cash, gems);
            });
        }

        static void Build(PadDef def, bool anim)
        {
            switch (def.kind)
            {
                case "station":
                {
                    var st = Lanes[def.lane].AddStation(def.st, anim, 4);
                    if (anim) WorldUI.Text(new Vector3(st.X, 5.2f, st.Z), "NEW!", "new", 1.3f);
                    break;
                }
                case "rack":
                    Racks[def.rack] = new Rack(def.rack, 3, Root);
                    if (anim) Fx.PopIn(Racks[def.rack].Model);
                    break;
                case "hire": Hire(def, anim); break;
                case "building": Buildings[def.b] = new Building(def.b, anim, L.theme, Root); break;
                case "lane":
                {
                    var l = Lanes[1]; l.Unlock(anim); l.AddStation("brush", anim, 4); l.AddStation("pay", anim);
                    Hud.Toast("Lane 2 is open! Twice the cars, twice the cash.", "lane");
                    break;
                }
                case "travel":
                {
                    Buildings["travel"] = new Building("travel", anim, L.theme, Root);
                    var s = S;
                    if (LocIdx + 1 < Cfg.Locations.Count)
                    {
                        s.unlockedLocs = Math.Max(s.unlockedLocs, LocIdx + 2);
                        int to = LocIdx + 1;
                        if (anim) Sched.After(0.9f, () => Hud.OpenPanel("travel", new PanelArgs { to = to }));
                    }
                    else if (anim) Sched.After(0.9f, () => Hud.OpenPanel("finale"));
                    break;
                }
            }
        }
        static Worker Hire(PadDef def, bool anim)
        {
            string role = def.role;
            var names = Cfg.HireNames[role];
            int n = Workers.Count(w => w.Role == role);
            var wk = new Worker(role, Root, def.lane, names[n % names.Length]);
            Workers.Add(wk);
            if (anim) { Sfx.Play("hire"); WorldUI.Text(new Vector3(wk.Pos.x, 2.6f, wk.Pos.z), "HIRED!", "big", 1.2f); Fx.PopIn(wk.Rig.Root); }
            return wk;
        }
        /// <summary>Where new staff appear (web x / z).</summary>
        public static Vector2 StaffDoor()
        {
            if (Buildings.TryGetValue("staff", out var b)) return new Vector2(b.Pos.x, b.Pos.z + 3.2f);
            var sp = Cfg.BuildingPos["shop"];
            return new Vector2(sp.x + 3.0f, sp.y + 3.8f);
        }
        public static Station PostFor(string role, int laneIdx)
        {
            if (laneIdx < 0 || laneIdx >= Lanes.Count) return null;
            var l = Lanes[laneIdx];
            return role == "cashier" ? l.Get("pay") : role == "polisher" ? l.Get("polish") : null;
        }
        public static void RemoveWorker(Worker w) => Workers.Remove(w);
        public static void RemoveCar(Car c) => Cars.Remove(c);

        // =====================================================================
        // Upgrades
        // =====================================================================
        public static bool BuyStation(string t, bool free = false)
        {
            if (Lv(t) >= Cfg.StationMaxLv) return false;
            double cost = StationUpCost(t);
            if (!free && !CanAfford(cost)) { Sfx.Play("error"); return false; }
            if (!free) Spend(cost);
            Ls.st[t] = Lv(t) + 1;
            AfterUpgrade(t);
            foreach (var l in Lanes) l.Get(t)?.Punch();
            return true;
        }
        public static bool BuyBiz(string id)
        {
            var d = Cfg.BizById(id);
            if (Biz(id) >= d.max) return false;
            double cost = BizCost(id);
            if (!CanAfford(cost)) { Sfx.Play("error"); return false; }
            Spend(cost);
            S.AddBiz(id);
            AfterUpgrade(id);
            if (id == "carry" || id == "speed") Fx.Sparkles(new Vector3(Player.Pos.x, 1.5f, Player.Pos.z), 8, 1f);
            return true;
        }
        static void AfterUpgrade(string what)
        {
            Sfx.Play("upgrade"); U.Vibrate(15);
            S.stats.upgrades++; Cnt("upgrades");
            AddXP(2);
            Hud.CashChanged(false); Hud.RefreshCards();
            Bus.Emit("upgrade", what);
        }

        // =====================================================================
        // Payout at the pay booth
        // =====================================================================
        public static void Payout(Station st, Car car)
        {
            var s = S;
            double amount = car.Bill * car.Def.pay * PriceMult() * car.GoldenMult * (RushHour ? 1.5 : 1);
            amount = Math.Max(1, Math.Round(amount));
            bool clean = car.Dirt < 0.05f;
            double tip = 0;
            if (UnityEngine.Random.value < TipChance() * (clean ? 1f : 0.35f)) tip = Math.Max(1, Math.Round(amount * U.Rand(0.2f, 0.5f) * (1 + Math.Min(Streak, 10) * 0.1)));
            var from = new Vector3(car.X, 1.2f, car.Z);
            st.Lane.Pile.Add(amount + tip, from);
            if (Player.Near(car.X, car.Z, 26f))
            {
                float x = car.X, z = car.Z, h = car.H;
                WorldUI.Text(new Vector3(x, h + 1.0f, z), "+" + U.Fmt(amount), "cash", 1.3f, 60f, "cash");
                if (tip > 0) Sched.After(0.25f, () => WorldUI.Text(new Vector3(x + 0.6f, h + 0.6f, z), "TIP +" + U.Fmt(tip), "tip", 1.2f));
                if (clean && car.Stages >= 3 && U.Chance(0.6f)) Sched.After(0.12f, () => WorldUI.Text(new Vector3(x - 0.5f, h + 1.6f, z), U.Pick(Cfg.CleanWords), "big", 1.2f));
                if (!clean && car.Coat.Count > 0 && U.Chance(0.5f)) WorldUI.Text(new Vector3(x, h + 1.4f, z), "STILL FOAMY?!", "honk", 1.2f);
            }
            Sfx.Play("cash");
            s.stats.cars++; Ls.cars++; Cnt("wash");
            if (tip > 0) { s.stats.tips++; Ls.tips++; Cnt("tips"); }
            if (car.Vip) { s.stats.vip++; Ls.vip++; Cnt("vip"); }
            if (st.OperatorPresent() == "player") Cnt("serve");
            Streak = clean ? Streak + 1 : 0;
            Hud.Streak(Streak);
            AddXP((1 + car.Stages) * (car.Vip ? 3 : 1) * (car.Golden ? 5 : 1));
            // album
            bool first = !s.album.ContainsKey(car.Type);
            s.album[car.Type] = (s.album.TryGetValue(car.Type, out var n) ? n : 0) + 1;
            if (first)
            {
                AddGems(3);
                string nm = car.Def.name;
                Sched.After(0.4f, () => Hud.Toast($"New car for your album: {nm}! +3 gems", "album"));
            }
            Bus.Emit("car:paid", car);
        }

        // =====================================================================
        // Spawning
        // =====================================================================
        static readonly string[] VipCars = { "limo", "sports", "monster" };
        static string PickCarType()
        {
            if (goldenDue) { goldenDue = false; return "golden"; }
            if (UnityEngine.Random.value < VipChance()) return U.Pick(VipCars);
            var w = L.cars;
            return U.Weighted(w.Keys.ToList(), k => w[k]);
        }
        static void SpawnUpdate(float dt)
        {
            spawnT -= dt;
            if (spawnT > 0f) return;
            // lane 2 works as overflow: cars pick the shortest queue, ties go to the better-equipped lane
            Lane best = null; float bestScore = float.PositiveInfinity;
            foreach (var l in Lanes)
            {
                if (!l.Unlocked || l.Get("pay") == null) continue;
                int q = l.QueueCount;
                if (q >= QueueCap()) continue;
                float sc = q * 10 - l.Stops().Count;
                if (sc < bestScore) { bestScore = sc; best = l; }
            }
            if (best == null) { spawnT = 0.8f; return; }
            spawnT = ArrivalInterval() * U.Rand(0.75f, 1.25f);
            string type = PickCarType();
            var back = best.Cars.Count > 0 ? best.Cars[best.Cars.Count - 1] : null;
            var car = new Car(type, best, Root, back != null ? Mathf.Min(Cfg.SpawnX, back.X - 6f) : Cfg.SpawnX);
            best.Cars.Add(car); Cars.Add(car);
            if (type == "golden") Hud.GoldenOffer(car);
        }

        // =====================================================================
        // Stockers pick the most urgent hopper
        // =====================================================================
        public static IEnumerable<Hopper> AllHoppers()
        {
            foreach (var l in Lanes) foreach (var st in l.Stations.Values) if (st.Hopper != null) yield return st.Hopper;
        }
        public static StockTask PickStockTask(Worker w)
        {
            StockTask best = null; float bestS = float.NegativeInfinity;
            foreach (var h in AllHoppers())
            {
                if (!Racks.TryGetValue(h.Supply, out var r)) continue;
                int need = h.Cap - h.Count - h.Incoming - h.Pending;
                if (need < 2 && !(h.Count == 0 && need >= 1)) continue;
                int carrying = w.Stack.CountOf(h.Supply);
                int amount = Mathf.Min(need, Mathf.Max(carrying, w.Stack.Cap - w.Stack.Count + carrying));
                if (amount <= 0) continue;
                float urg = (float)(h.Cap - h.Count) / h.Cap;
                float d = U.Dist(w.Pos.x, w.Pos.z, r.Mat.x, r.Mat.y) + U.Dist(r.Mat.x, r.Mat.y, h.Mat.x, h.Mat.y);
                float sc = urg * 30 + (h.Count == 0 ? 25 : 0) - d * 0.5f + (carrying > 0 ? 25 : 0) + (r.Stock > 0 ? 6 : 0);
                if (sc > bestS) { bestS = sc; best = new StockTask { hopper = h, rack = r, supply = h.Supply, amount = amount }; }
            }
            return best;
        }

        // =====================================================================
        // Player <-> world interactions (pickup, delivery)
        // =====================================================================
        static void PlayerInteract(float dt)
        {
            var p = Player;
            pickT -= dt; dropT -= dt;
            foreach (var kv in Racks)
            {
                var r = kv.Value;
                if (!p.Near(r.Mat.x, r.Mat.y, 1.1f)) continue;
                if (pickT <= 0f && !p.Stack.Full && r.Stock > 0 && r.Take(out var wp))
                {
                    p.Stack.Add(kv.Key, wp); Sfx.Play("pop"); Cnt("pickup"); pickT = 0.09f;
                }
            }
            foreach (var h in AllHoppers())
            {
                if (!p.Near(h.Mat.x, h.Mat.y, 1.1f)) continue;
                if (dropT <= 0f && p.Stack.CountOf(h.Supply) > 0 && h.Space > 0)
                {
                    var it = p.Stack.Take(h.Supply, out var from);
                    h.Receive(it.tr, from); Cnt("deliver"); dropT = 0.08f;
                }
            }
            PolishingPlayer = false;
            foreach (var l in Lanes)
            {
                var st = l.Get("polish");
                if (st != null && st.Busy && p.Near(st.OperatorSpot.x, st.OperatorSpot.z, 1.15f)) { PolishingPlayer = true; p.Facing = U.AngleLerp(p.Facing, Mathf.PI, 0.2f); }
            }
        }

        // =====================================================================
        // Events: gift balloon, golden car, rush hour
        // =====================================================================
        class Balloon
        {
            public Transform tr; public WLabel lbl; public float t, z;
            public void Destroy() { if (tr) Object.Destroy(tr.gameObject); lbl?.Remove(); }
        }
        static void EventsUpdate(float dt)
        {
            float t = Clock; var s = S;
            if (s.level >= Cfg.Unlocks["gift"] && balloon == null && t > nextGift) SpawnBalloon();
            if (balloon != null)
            {
                var b = balloon; b.t += dt;
                b.tr.position = U.P(-34f + b.t * 2.4f, 8.5f + Mathf.Sin(b.t * 1.4f) * 0.5f, b.z);
                b.tr.rotation = U.Yaw(b.t * 0.5f);
                if (b.t > 29f) { b.Destroy(); balloon = null; nextGift = t + U.Rand(Cfg.GiftEvery.x, Cfg.GiftEvery.y); }
            }
            if (s.level >= Cfg.Unlocks["golden"] && t > nextGolden) { goldenDue = true; nextGolden = t + U.Rand(Cfg.GoldenEvery.x, Cfg.GoldenEvery.y) * (HasPerk("city") ? 0.5f : 1f); }
            if (s.level >= Cfg.Unlocks["rush"])
            {
                if (!RushHour && t > nextRush) { RushHour = true; rushT = Cfg.RushSecs; Hud.Banner("RUSH HOUR!", "Cars x3 and +50% pay", Cfg.RushSecs); Sfx.Play("siren"); }
                if (RushHour) { rushT -= dt; if (rushT <= 0f) { RushHour = false; nextRush = t + U.Rand(Cfg.RushEvery.x, Cfg.RushEvery.y) * (HasPerk("mumbai") ? 0.5f : 1f); } }
            }
        }
        static void SpawnBalloon()
        {
            var g = new GameObject("GiftBalloon").transform;
            g.SetParent(Root, false);
            var red = U.Hex(0xff4d5a);
            var body = new GameObject("Balloon");
            body.transform.SetParent(g, false);
            body.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            body.transform.localScale = new Vector3(1.1f, 1.265f, 1.1f);
            body.AddComponent<MeshFilter>().sharedMesh = Art.Ico;
            body.AddComponent<MeshRenderer>().sharedMaterial = Art.Solid(red);
            var k = new MeshKit();
            k.Cylinder(0.35f, 0.02f, 0.25f, 10, red, 0f, 0.95f, 0f);
            k.Cylinder(0.02f, 0.02f, 1.2f, 4, Color.white, 0f, 0.2f, 0f);
            k.Box(0.8f, 0.7f, 0.8f, U.Hex(0x46c23a), 0f, -0.6f, 0f);
            k.Box(0.84f, 0.16f, 0.84f, U.Hex(0xffd23f), 0f, -0.6f, 0f);
            k.Box(0.16f, 0.74f, 0.84f, U.Hex(0xffd23f), 0f, -0.6f, 0f);
            k.BuildObject("Gift", g);
            float z = U.Clamp(Player.Pos.z - 2f, -4f, 14f);
            var lbl = WorldUI.Label(g, 3.8f);
            lbl.Set("gift", e => e.Add(WorldUI.Clickable(WorldUI.Chip("gift-tag", "gift", "FREE GIFT"), Hud.GiftOffer)));
            balloon = new Balloon { tr = g, lbl = lbl, t = 0f, z = z };
            g.position = U.P(-34f, 8.5f, z);
        }
        public static void PopBalloon()
        {
            if (balloon == null) return;
            var p = U.W(balloon.tr.position);
            Fx.Confetti(p, 40, 5f); Sfx.Play("pop");
            balloon.Destroy(); balloon = null;
            nextGift = Clock + U.Rand(Cfg.GiftEvery.x, Cfg.GiftEvery.y);
        }
        public static void AddBoost(string name, float secs)
        {
            var s = S; long n = Now;
            long cur = Math.Max(n, s.Boost(name));
            s.SetBoost(name, Math.Min(n + (long)(Cfg.Boosts[name].y * 1000), cur + (long)(secs * 1000)));
            if (name == "turbo" && !Workers.Any(w => w.Turbo))
            {
                var w = new Worker("turbo", Root, 0, "Turbo Tom");
                w.Pos = new Vector3(Player.Pos.x + 1f, 0f, Player.Pos.z + 1f);
                Workers.Add(w);
                Fx.PopIn(w.Rig.Root);
                WorldUI.Text(new Vector3(w.Pos.x, 2.6f, w.Pos.z), "TURBO TOM!", "big");
            }
            Hud.RefreshBoosts();
        }

        // =====================================================================
        // Quests (task list)
        // =====================================================================
        static QuestDef Q(string k, string id = null, double n = 0, string st = null, int lv = 0, string text = null, string icon = null)
            => new QuestDef { k = k, id = id, n = n, st = st, lv = lv, text = text, icon = icon };
        static readonly List<QuestDef> Tut = new List<QuestDef>
        {
            Q("pickup", n: 1, text: "Grab soap from the Soap Rack", icon: "soap"),
            Q("deliver", n: 1, text: "Load soap into the Foam Brush", icon: "brush"),
            Q("serve", n: 1, text: "Stand at the Pay Booth to take payment", icon: "pay"),
            Q("collect", n: 1, text: "Pick up your cash", icon: "cash"),
        };
        // the main road to the Road Trip first, then the fun extras
        static readonly List<QuestDef> Chain = new List<QuestDef>
        {
            Q("build", "rinse"), Q("build", "cashier1"), Q("wash", n: 6), Q("upgrade", st: "brush", lv: 3), Q("build", "dryer"),
            Q("build", "staff"), Q("build", "stocker1"), Q("upgrade", st: "rinse", lv: 4), Q("build", "office"),
            Q("biz", "speed", lv: 1), Q("build", "waxRack"), Q("build", "wax"), Q("upgrade", st: "wax", lv: 3),
            Q("build", "gelRack"), Q("build", "tire"), Q("build", "travel"),
            Q("build", "tubeman"), Q("wash", n: 40), Q("build", "lane2"), Q("biz", "cars", lv: 2),
        };
        /// <summary>Quests for this city; after the first city the Franchise Kit is already built, so its build quests are skipped.</summary>
        static List<QuestDef> QuestList() => (LocIdx == 0 ? Tut : new List<QuestDef>())
            .Concat(Chain.Where(q => LocIdx == 0 || !(q.k == "build" && Cfg.Econ.FranchiseKit.Contains(q.id)))).ToList();
        static readonly string[] GenKinds = { "build", "wash", "upgrade", "build", "earn", "tips", "build", "biz" };
        static QuestDef GenQuest(int i)
        {
            string k = GenKinds[i % GenKinds.Length];
            switch (k)
            {
                case "build":
                {
                    var vis = Pads.Where(p => p.Shown).OrderBy(p => p.Cost).FirstOrDefault();
                    return vis != null ? Q("build", vis.Id) : Q("wash", n: 30 + i * 2);
                }
                case "wash": return Q("wash", n: 20 + Math.Min(200, i * 3));
                case "earn": return Q("earn", n: Math.Round(Math.Max(500, RewardFor(4)) / M));
                case "tips": return Q("tips", n: 5 + i / 4);
                case "upgrade":
                {
                    var types = Cfg.SlotOrder.Where(t => Lanes.Any(l => l.Get(t) != null) && Lv(t) < Cfg.StationMaxLv).OrderBy(Lv).ToList();
                    if (types.Count == 0) return Q("wash", n: 50);
                    var t0 = types[0];
                    return Q("upgrade", st: t0, lv: Math.Min(Cfg.StationMaxLv, Lv(t0) + 2 + i % 3));
                }
                case "biz":
                {
                    var ids = Built("office") ? Cfg.Biz.Where(d => Biz(d.id) < d.max).OrderBy(d => Biz(d.id)).ToList() : new List<BizDef>();
                    if (ids.Count == 0) return Q("wash", n: 40);
                    return Q("biz", ids[0].id, lv: Biz(ids[0].id) + 1);
                }
            }
            return Q("wash", n: 25);
        }
        public static string QuestText(QuestDef q)
        {
            switch (q.k)
            {
                case "build": return "Build: " + PadName(Cfg.PadById(q.id));
                case "wash": return $"Wash {q.n} cars";
                case "upgrade": return $"Upgrade {Cfg.Stations[q.st].name} to Lv {q.lv}";
                case "biz": return $"Buy {Cfg.BizById(q.id).name} Lv {q.lv}";
                case "earn": return "Earn $" + U.Fmt(q.n * M);
                case "tips": return $"Collect {q.n} tips";
                default: return q.text;
            }
        }
        public static string QuestIcon(QuestDef q)
        {
            if (q.icon != null) return q.icon;
            switch (q.k)
            {
                case "build": return PadIcon(Cfg.PadById(q.id));
                case "upgrade": return Cfg.Stations[q.st].icon;
                case "biz": return Cfg.BizById(q.id).icon;
                case "wash": return "car";
                case "earn": return "cash";
                case "tips": return "heart";
                default: return "flag";
            }
        }
        static void PickQuest(bool advance)
        {
            var ls = Ls;
            if (advance) { ls.quest++; ls.qDef = null; }
            var list = QuestList(); int i = ls.quest;
            var q = ls.qDef ?? (i < list.Count ? list[i].Clone() : GenQuest(i));
            if (q.k == "build" && Cfg.PadById(q.id) == null) q = Q("wash", n: 30);
            if (q.k == "biz" && Cfg.BizById(q.id) == null) q = Q("wash", n: 30);
            ls.qDef = q;
            if (ls.qBase == null || advance) ls.qBase = new Dictionary<string, double>(ls.cnt);
            Quest = q;
            Hud.QuestChanged();
        }
        /// <summary>(progress, needed)</summary>
        public static (double p, double n) QuestProgress()
        {
            var q = Quest;
            if (q == null) return (0, 1);
            var c = Ls.cnt; var b = Ls.qBase ?? new Dictionary<string, double>();
            double D(string k) => (c.TryGetValue(k, out var a) ? a : 0) - (b.TryGetValue(k, out var o) ? o : 0);
            switch (q.k)
            {
                case "pickup": case "deliver": case "serve": case "collect": case "tips": return (Math.Min(q.n, D(q.k)), q.n);
                case "wash": return (Math.Min(q.n, D("wash")), q.n);
                case "earn": return (Math.Min(q.n * M, D("earn")), q.n * M);
                case "build": return (Built(q.id) ? 1 : 0, 1);
                case "upgrade": return (Math.Min(q.lv, Lv(q.st)), q.lv);
                case "biz": return (Math.Min(q.lv, Biz(q.id)), q.lv);
            }
            return (0, 1);
        }
        public static (double cash, int gems, int xp) QuestReward()
        {
            int i = Ls.quest;
            return (RewardFor(0.25 + Math.Min(i, 40) * 0.05) * (HasPerk("cairo") ? 2 : 1), i % 4 == 3 ? 3 : 1, 8 + Math.Min(i, 60) * 2);
        }
        static Vector3? QuestTarget()
        {
            var q = Quest;
            if (q == null) return null;
            switch (q.k)
            {
                case "pickup": return Racks.TryGetValue("soap", out var r) ? new Vector3(r.Mat.x, 0f, r.Mat.y) : (Vector3?)null;
                case "deliver": { var st = Lanes[0].Get("brush"); return st?.Hopper != null ? new Vector3(st.Hopper.Mat.x, 0f, st.Hopper.Mat.y) : (Vector3?)null; }
                case "serve": { var st = Lanes[0].Get("pay"); return st != null ? new Vector3(st.OperatorSpot.x, 0f, st.OperatorSpot.z) : (Vector3?)null; }
                case "collect": { var pl = Lanes[0].Pile; return pl != null && pl.Values.Count > 0 ? pl.Pos : (Vector3?)null; }
                case "build": { var p = Pads.FirstOrDefault(p2 => p2.Id == q.id && p2.Shown); return p != null ? p.Pos : (Vector3?)null; }
                case "biz":
                {
                    Building b = Buildings.TryGetValue("office", out var o) ? o : null;
                    return b != null && b.Mat.HasValue ? new Vector3(b.Mat.Value.x, 0f, b.Mat.Value.y) : (Vector3?)null;
                }
            }
            return null;
        }
        public static bool ClaimQuest()
        {
            var (p, n) = QuestProgress();
            if (p < n) return false;
            var r = QuestReward();
            AddCash(r.cash); AddGems(r.gems); AddXP(r.xp);
            Sfx.Play("reward");
            Hud.RewardBurst(r.cash, r.gems);
            PickQuest(true);
            Store.Save();
            return true;
        }

        // quest arrow in the world
        static void ArrowUpdate(float dt)
        {
            var tgt = QuestTarget();
            if (!arrow)
            {
                var orange = U.Hex(0xff8a1f);
                var k = new MeshKit();
                k.Cylinder(0.62f, 0.001f, 0.95f, 4, orange, 0f, 0.48f, 0f, Mathf.PI / 4f);
                k.Box(0.42f, 0.75f, 0.42f, orange, 0f, 1.3f, 0f);
                arrow = k.BuildObject("QuestArrow", Root, false).transform;
                ringMat = Art.Decal(Art.Tex("ring"), orange);
                ringMat.renderQueue = 3002;
                ring = Art.GroundDecal("QuestRing", ringMat, 2.4f, Root);
            }
            var (qp, qn) = QuestProgress();
            bool near = tgt.HasValue && (Player.Near(tgt.Value.x, tgt.Value.z, 1.3f) || qp >= qn);
            bool vis = tgt.HasValue && !near;
            if (arrow.gameObject.activeSelf != vis) { arrow.gameObject.SetActive(vis); ring.gameObject.SetActive(vis); }
            if (tgt.HasValue)
            {
                var v = tgt.Value;
                float t = Time.time;
                arrow.position = U.P(v.x, 1.1f + Mathf.Abs(Mathf.Sin(t * 4f)) * 0.55f, v.z);
                arrow.rotation = U.Yaw(t * 2f);
                ring.position = U.P(v.x, 0.09f, v.z);
                float ph = t * 1.5f % 1f, s = 2.4f * (1f + ph * 0.35f);
                ring.localScale = new Vector3(s, s, 1f);
                var c = ringMat.GetColor("_BaseColor"); c.a = 1f - ph * 0.7f; ringMat.SetColor("_BaseColor", c);
            }
            Hud.EdgeArrow(vis ? tgt : null);
        }

        /// <summary>Camera look-at point: leads the player a little and stays inside the lot.</summary>
        public static Vector3 CamTarget()
        {
            var p = Player; var b = Cfg.CamBounds;
            return new Vector3(U.Clamp(p.Pos.x + p.Vel.x * 0.25f, b.xMin, b.xMax), 0f, U.Clamp(p.Pos.z + p.Vel.z * 0.25f - 1.0f, b.yMin, b.yMax));
        }

        // =====================================================================
        // Main update
        // =====================================================================
        public static void Update(float dt)
        {
            if (!Running || Paused) return;
            T += dt;
            S.stats.playSecs += dt;
            var inp = Hud.PanelOpen ? Vector2.zero : Move;
            Player.Update(dt, inp);
            PlayerInteract(dt);
            SpawnUpdate(dt);
            foreach (var c in Cars.ToArray()) if (!c.Dead) c.Update(dt);
            foreach (var l in Lanes) { foreach (var st in l.Stops()) st.Update(dt); l.Pile?.Update(dt); }
            foreach (var r in Racks.Values) r.Update(dt);
            foreach (var w in Workers.ToArray()) w.Update(dt);
            foreach (var p in Pads.ToArray()) p.Update(dt);
            foreach (var b in Buildings.Values.ToArray()) b.Update(dt);
            EventsUpdate(dt);
            ArrowUpdate(dt);
            // bottleneck: slowest station type on lane 1, flagged while cars back up
            bnT -= dt;
            if (bnT <= 0f)
            {
                bnT = 1f;
                string slow = null; float worst = 0f;
                foreach (var st in Lanes[0].Stops()) { float tt = StationTime(st.Type); if (tt > worst) { worst = tt; slow = st.Type; } }
                bool backed = Lanes.Any(l => l.QueueCount >= 2);
                string b = backed ? slow : null;
                if (b != Bottleneck) { Bottleneck = b; Hud.RefreshCards(); }
                Hud.Tick1s();
            }
            saveT += dt;
            if (saveT > 5f) { saveT = 0f; SaveRuntime(); Store.Save(); }
        }

        /// <summary>Offline earnings for a period away, or null when under a minute.</summary>
        public static OfflineResult OfflineEarnings(double awaySecs)
        {
            var s = S;
            if (awaySecs < 60) return null;
            float capH = AwayHours();
            double secs = Math.Min(awaySecs, capH * 3600);
            float autom = Automation();
            double amount = Math.Round(IncomePerSec() * secs * Cfg.OfflineEfficiency * (HasPerk("sydney") ? 1.5 : 1) * autom);
            return new OfflineResult { amount = amount, secs = secs, capped = awaySecs > capH * 3600, capH = capH, autom = autom };
        }

        /// <summary>How much of the lot runs without the manager: staffed pay booths, and stockers to keep machines fed.</summary>
        public static float Automation()
        {
            var lanes = Lanes.Where(l => l.Unlocked && l.Get("pay") != null).ToList();
            if (lanes.Count == 0) return 0f;
            int staffed = lanes.Count(l => Workers.Any(w => w.Role == "cashier" && w.Lane == l.Idx));
            return (float)staffed / lanes.Count * (Workers.Any(w => w.Role == "stocker") ? 1f : 0.4f);
        }

        // =====================================================================
        // Empire: every city you leave keeps earning as a branch
        // =====================================================================
        public static float BranchEff() => Cfg.Econ.BranchBaseEff + 0.05f * S.Hq("franchise");
        /// <summary>How long offline and branch earnings pile up (Night Shift adds an hour per level).</summary>
        public static float AwayHours() => Cfg.Econ.AwayHours + S.Hq("offline");
        public static double BranchPending(LocSave ls)
        {
            if (ls == null || ls.branchRate <= 0 || ls.lastCollect <= 0) return 0;
            double secs = Math.Max(0, (Now - ls.lastCollect) / 1000.0);
            return ls.branchRate * Math.Min(secs, AwayHours() * 3600.0);
        }
        /// <summary>Branch cities (every started city except the one you are running).</summary>
        public static IEnumerable<(LocDef def, LocSave ls)> Branches()
        {
            foreach (var d in Cfg.Locations)
            {
                if (L != null && d.id == L.id) continue;
                if (S.locs.TryGetValue(d.id, out var ls) && ls.started && ls.branchRate > 0) yield return (d, ls);
            }
        }
        public static double EmpireRate() => Branches().Sum(b => b.ls.branchRate);
        public static double EmpirePending() => Branches().Sum(b => BranchPending(b.ls));
        /// <summary>Move all branch earnings into the current city (mult = 2 after a rewarded ad).</summary>
        public static double CollectEmpire(double mult = 1)
        {
            double total = 0;
            foreach (var b in Branches().ToList()) { total += BranchPending(b.ls); b.ls.lastCollect = Now; }
            if (total > 0) AddCash(total * mult);
            Store.Save();
            return total * mult;
        }

        public static void Travel(int idx)
        {
            if (idx == LocIdx || idx >= S.unlockedLocs) return;
            SaveRuntime();
            Ls.cash = S.cash;
            // the city we leave becomes a branch that keeps earning a share of its income
            Ls.branchRate = IncomePerSec() * Automation() * BranchEff();
            Ls.lastCollect = Now;
            Hud.Fade(() =>
            {
                var nl = Store.Loc(Cfg.Locations[idx].id);
                S.cash = nl.cash ?? 0;
                Load(idx);
                Store.Save();
                Hud.Toast($"Welcome to {Cfg.Locations[idx].name}!", "map");
            });
        }

        internal static void ResetStatics()
        {
            L = null; Ls = null; LocIdx = 0;
            Lanes.Clear(); Racks.Clear(); Workers.Clear(); Cars.Clear(); Pads.Clear(); Buildings.Clear();
            Player = null; Root = null; Paused = Running = RushHour = PolishingPlayer = false;
            Bottleneck = null; Quest = null; Streak = 0; T = 0f; Move = Vector2.zero;
            hooked = false; goldenDue = false; balloon = null; arrow = ring = null; ringMat = null;
        }

        public static void DevGive(double cash, double gems = 0) { if (cash > 0) AddCash(cash); if (gems > 0) AddGems(gems); }
        public static void DevBuildAll() { foreach (var p in Pads.ToArray()) if (p.Def.kind != "travel") { p.Paid = p.Cost; CompletePad(p); } }
    }
}
