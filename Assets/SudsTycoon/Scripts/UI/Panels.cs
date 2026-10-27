using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>Options passed to a panel (only the fields that panel uses are set).</summary>
    public class PanelArgs
    {
        public string tab;
        public int to, level, gems, popped;
        public Car car;
        public OfflineResult off;
        public string title, text, ok;
        public bool danger, win;
        public Action cb;
        public double cash;
        public PanelArgs With(string newTab) { var c = (PanelArgs)MemberwiseClone(); c.tab = newTab; return c; }
    }

    public class PanelDef
    {
        public string title, icon, sub;
        public bool wide, noClose, breakAfter;
        public List<(string id, string label)> tabs;
        public string tab;
        public Action<VisualElement, PanelCtx> render;
        public Action onClose;
    }

    public class PanelCtx
    {
        public string name;
        public PanelDef def;
        public PanelArgs args;
        public VisualElement body;
        public ScrollView scroll;
        public Action tick;
        public Action<float> update;
    }

    public static partial class Hud
    {
        static PanelCtx current;
        public static bool PanelOpen => current != null;
        public static string CurrentPanel => current?.name;

        static readonly Dictionary<string, Func<PanelArgs, PanelDef>> Factory = new Dictionary<string, Func<PanelArgs, PanelDef>>
        {
            { "upgrades", UpgradesPanel }, { "shop", ShopPanel }, { "freecash", FreeCashPanel }, { "daily", DailyPanel },
            { "spin", SpinPanel }, { "album", AlbumPanel }, { "map", MapPanel }, { "empire", EmpirePanel }, { "perk", PerkPanel }, { "travel", TravelPanel }, { "finale", FinalePanel },
            { "settings", SettingsPanel }, { "confirm", ConfirmPanel }, { "offline", OfflinePanel }, { "noOffline", NoOfflinePanel },
            { "levelup", LevelUpPanel }, { "gift", GiftPanel }, { "golden", GoldenPanel },
            { "blaster", _ => Blaster.Panel() }, { "blasterResult", Blaster.ResultPanel },
        };

        // =====================================================================
        // Panel frame
        // =====================================================================
        public static void OpenPanel(string name, PanelArgs a = null)
        {
            if (Blaster.Active && name != "blasterResult") return;
            ClosePanel(true);
            if (a == null) a = new PanelArgs();
            if (!Factory.TryGetValue(name, out var f)) return;
            var def = f(a);
            if (def == null) return;
            panelLayer.Clear();
            var panel = UiKit.El("panel enter" + (def.wide ? " wide" : ""));
            panel.pickingMode = PickingMode.Position;
            var head = UiKit.El("panel-head");
            if (def.icon != null) head.Add(UiKit.Icon(def.icon));
            var ht = UiKit.El("ph-text");
            ht.Add(UiKit.Txt(def.title, "ph-title"));
            if (def.sub != null) ht.Add(UiKit.Txt(def.sub, "ph-sub"));
            head.Add(ht);
            if (!def.noClose) head.Add(UiKit.Btn("panel-close", () => { Sfx.Play("click"); ClosePanel(); }, UiKit.Icon("close")));
            panel.Add(head);
            if (def.tabs != null)
            {
                var tabs = UiKit.El("tabs");
                foreach (var (id, label) in def.tabs)
                {
                    string tid = id;
                    tabs.Add(UiKit.Btn("tab" + (id == def.tab ? " on" : ""), () => { Sfx.Play("click"); OpenPanel(name, a.With(tid)); }, UiKit.Txt(label)));
                }
                panel.Add(tabs);
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("panel-body");
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            panel.Add(scroll);
            panelLayer.Add(panel);
            panelLayer.AddToClassList("on");
            current = new PanelCtx { name = name, def = def, args = a, body = scroll.contentContainer, scroll = scroll };
            def.render(current.body, current);
            panel.schedule.Execute(() => panel.RemoveFromClassList("enter")).ExecuteLater(16);
            Joystick.RemoveFromClassList("on");
        }

        public static void Rerender()
        {
            if (current == null) return;
            var off = current.scroll.scrollOffset;
            current.body.Clear();
            current.tick = null; current.update = null;
            current.def.render(current.body, current);
            var sc = current.scroll;
            sc.schedule.Execute(() => sc.scrollOffset = off).ExecuteLater(1);
        }

        public static void ClosePanel(bool silent = false)
        {
            if (current == null) return;
            var was = current;
            current = null;
            panelLayer.RemoveFromClassList("on");
            panelLayer.Clear();
            if (!silent)
            {
                was.def.onClose?.Invoke();
                if (was.def.breakAfter) Ads.Interstitial("panel_close");
            }
        }

        static void UpdatePanels(float dt) => current?.update?.Invoke(dt);
        /// <summary>Escape closes the open panel unless it must be answered.</summary>
        public static void CloseFromKey() { if (current != null && !current.def.noClose) { Sfx.Play("click"); ClosePanel(); } }

        // ---------- builders ----------
        static string Soft(string s) => $"<size=14><color=#5b6b85>{s}</color></size>";
        static VisualElement CostBtn(double cost, bool maxed, Func<bool> onBuy, PanelCtx cur, Func<double> costFn)
        {
            if (maxed) { var m = UiKit.Button("MAX", "gold disabled", null); return m; }
            VisualElement b = null;
            b = UiKit.Button(U.Fmt(cost), Game.CanAfford(cost) ? "" : "gray", () => { if (onBuy()) Rerender(); }, "cash");
            var prevTick = cur.tick;
            cur.tick = () => { prevTick?.Invoke(); if (b.panel != null) b.EnableInClassList("gray", !Game.CanAfford(costFn())); };
            return b;
        }
        static VisualElement GemBtn(int cost, bool maxed, Action onClick)
        {
            if (maxed) return UiKit.Button("MAX", "gold disabled", null);
            return UiKit.Button(cost.ToString(), "purple" + (S.gems >= cost ? "" : " gray"), onClick, "gem");
        }
        static VisualElement Note(string text, string cls = "note") => UiKit.Txt(text, cls);
        static VisualElement BtnRow(params VisualElement[] kids) => UiKit.El("btn-row", kids);
        static VisualElement HeroNum(params (string icon, string text)[] parts)
        {
            var h = UiKit.El("hero-num");
            foreach (var (icon, text) in parts) { h.Add(UiKit.Icon(icon)); h.Add(UiKit.Txt(text)); }
            return h;
        }

        // =====================================================================
        // Upgrades: two tabs, and every row reads "what you have -> what you get"
        // =====================================================================
        /// <summary>"Carry {0} items" with "7" and "8" -> "Carry 7 → 8 items"; maxed -> "Carry 14 items · max".</summary>
        static string NowNext(string text, Func<int, string> num, int l, bool maxed)
            => maxed ? string.Format(text, num(l)) + " · max" : string.Format(text, num(l) + " → " + num(l + 1));

        static PanelDef UpgradesPanel(PanelArgs o)
        {
            var tabs = new List<(string, string)> { ("machines", "Machines"), ("business", "Business") };
            string tab = o.tab == "business" ? "business" : "machines";
            return new PanelDef
            {
                title = "Upgrades", icon = "rack", tabs = tabs, tab = tab, wide = true,
                render = (b, cur) =>
                {
                    if (tab == "machines")
                    {
                        foreach (var t in Cfg.SlotOrder)
                        {
                            if (!Game.Lanes.Any(l => l.Get(t) != null)) continue;
                            var c = Cfg.Stations[t]; int l = Game.Lv(t); bool mx = l >= Cfg.StationMaxLv;
                            string tt = t;
                            string sub = NowNext("Earns {0} per car", n => "$" + U.Fmt(Game.MachinePay(tt, n)), l, mx);
                            if (t == Game.Bottleneck) sub += " · <color=#ff8a1f><b>slowest, upgrade me!</b></color>";
                            b.Add(UiKit.Row(c.icon, $"{c.name} {Soft("Lv " + l)}", sub,
                                (float)l / Cfg.StationMaxLv, CostBtn(Game.StationUpCost(t), mx, () => Game.BuyStation(tt), cur, () => Game.StationUpCost(tt))));
                        }
                        b.Add(Note("Each upgrade makes a machine earn more and work a little faster."));
                    }
                    else if (!Game.Built("office"))
                        b.Add(UiKit.Row("lock", "Business upgrades", "Build the Manager Office to unlock them"));
                    else
                    {
                        foreach (var d in Cfg.Biz)
                        {
                            int l = Game.Biz(d.id); bool mx = l >= d.max;
                            string id = d.id;
                            b.Add(UiKit.Row(d.icon, $"{d.name} {Soft("Lv " + l)}", NowNext(d.text, d.num, l, mx),
                                (float)l / d.max, CostBtn(Game.BizCost(id), mx, () => Game.BuyBiz(id), cur, () => Game.BizCost(id))));
                        }
                        b.Add(Note("Business upgrades are yours for good: they work in every city."));
                    }
                },
            };
        }

        // =====================================================================
        // Shop
        // =====================================================================
        static PanelDef ShopPanel(PanelArgs o)
        {
            // paid offers only exist while purchases are simulated (Editor / development builds)
            bool iap = Cfg.IapEnabled;
            string tab = o.tab == "gems" || o.tab == "hats" ? o.tab : iap ? "offers" : "gems";
            var tabs = new List<(string, string)> { ("gems", "Gems"), ("hats", "Hats") };
            if (iap) tabs.Insert(0, ("offers", "Offers"));
            VisualElement HqRow() => UiKit.Row("trophy", "Gem upgrades", "Spend gems at HQ on upgrades for every city", null,
                UiKit.Button("Open", "blue", () => { Sfx.Play("click"); OpenPanel("empire"); }));
            return new PanelDef
            {
                title = "Shop", icon = "bag", tab = tab, wide = true, tabs = tabs,
                render = (b, cur) =>
                {
                    var s = S;
                    if (tab == "offers")
                    {
                        b.Add(HqRow());
                        foreach (var p in Cfg.Iap.Where(x => x.perk != null))
                        {
                            bool owned = Perk(p.perk);
                            string id = p.id;
                            b.Add(UiKit.Row(p.icon, p.name, p.desc, null, owned ? UiKit.Button("Owned", "gold disabled", null) : UiKit.Button(p.price, "blue", () => BuyIap(id))));
                        }
                        b.Add(Note("Purchases are simulated in this build (no real payment). Hook up Unity IAP (Google Play Billing / App Store) to sell them for real."));
                    }
                    else if (tab == "gems")
                    {
                        if (!iap) b.Add(HqRow());
                        int left = Cfg.Ads.LimitFreeGems - Store.Caps().freeGems;
                        b.Add(UiKit.Row("gem", "Free gems", (Ads.Free ? "5 gems" : "Watch an ad for 5 gems") + $" · {left} left today", null, UiKit.Button("+5", "ad" + (left > 0 ? "" : " gray"), () =>
                        {
                            if (Store.Caps().freeGems >= Cfg.Ads.LimitFreeGems) { Toast("Come back tomorrow for more free gems", "clock"); return; }
                            Ads.Rewarded("free_gems", ok => { if (!ok) return; Store.Caps().freeGems++; Game.AddGems(5); Sfx.Play("gem"); RewardBurst(0, 5); Rerender(); });
                        })));
                        if (iap) foreach (var p in Cfg.Iap.Where(x => x.perk == null)) { string id = p.id; b.Add(UiKit.Row(p.icon, p.name, $"{p.gems} gems", null, UiKit.Button(p.price, "blue", () => BuyIap(id)))); }
                        b.Add(UiKit.Txt("Swap gems for cash", "sect"));
                        foreach (var c in Cfg.CashPacks)
                        {
                            var cp = c;
                            b.Add(UiKit.Row(c.icon, c.name, $"${U.Fmt(Game.RewardFor(c.minutes))} ({c.minutes} min of income)", null, GemBtn(c.gems, false, () =>
                            {
                                if (S.gems < cp.gems) { Sfx.Play("error"); OpenPanel("shop", new PanelArgs { tab = "gems" }); return; }
                                Game.AddGems(-cp.gems); double v = Game.RewardFor(cp.minutes); Game.AddCash(v); RewardBurst(v, 0); Sfx.Play("reward"); Rerender();
                            })));
                        }
                    }
                    else if (tab == "hats")
                    {
                        var grid = UiKit.El("grid");
                        foreach (var ht in Cfg.Hats)
                        {
                            bool own = s.hats.owned.Contains(ht.id), on = s.hats.on == ht.id;
                            var tile = UiKit.El("tile" + (on ? " today" : ""));
                            tile.Add(UiKit.Icon("hat_" + ht.id));
                            tile.Add(UiKit.Txt(ht.name, "t-name"));
                            var h = ht;
                            if (on) tile.Add(UiKit.Button("Wearing", "gold disabled", null));
                            else if (own) tile.Add(UiKit.Button("Wear", "blue", () => { WearHat(h.id); Rerender(); }));
                            else tile.Add(UiKit.Button(h.cost.ToString(), "purple" + (s.gems >= h.cost ? "" : " gray"), () =>
                            {
                                if (S.gems < h.cost) { Sfx.Play("error"); Toast("Not enough gems", "gem"); return; }
                                Game.AddGems(-h.cost); S.hats.owned.Add(h.id); WearHat(h.id); Rerender(); Store.Save();
                            }, "gem"));
                            grid.Add(tile);
                        }
                        b.Add(grid);
                    }
                },
            };
        }
        static bool Perk(string p) => p == "noads" ? S.perks.noads : p == "starter" ? S.perks.starter : p == "vip" && S.perks.vip;
        static void SetPerk(string p) { if (p == "noads") S.perks.noads = true; else if (p == "starter") S.perks.starter = true; else if (p == "vip") S.perks.vip = true; }

        static void BuyIap(string id)
        {
            var p = Cfg.Iap.First(x => x.id == id);
            OpenPanel("confirm", new PanelArgs
            {
                title = p.name, text = $"Simulated purchase of <b>{p.name}</b> for {p.price}. No money is charged in this build.", ok = "Buy (test)",
                cb = () =>
                {
                    if (p.gems > 0) { Game.AddGems(p.gems); RewardBurst(0, p.gems); }
                    if (p.perk != null) { SetPerk(p.perk); if (p.perk == "noads" || p.perk == "vip") Ads.Banner(false); }
                    Sfx.Play("reward"); Toast($"Thanks! {p.name} is yours.", p.icon); Store.Save(); RefreshCards();
                },
            });
        }
        static void WearHat(string id) { S.hats.on = id; Game.Player?.Rig.SetHat(id); Sfx.Play("pop"); }

        // =====================================================================
        // Free cash, daily, spin, album, map, travel
        // =====================================================================
        static PanelDef FreeCashPanel(PanelArgs o)
        {
            int left = Cfg.Ads.LimitFreeCash - Store.Caps().freeCash; double v = Game.RewardFor(3);
            return new PanelDef
            {
                title = "Free Cash", icon = "cash",
                render = (b, cur) =>
                {
                    b.Add(HeroNum(("cash", U.Fmt(v))));
                    b.Add(Note((Ads.Free ? "Get" : "Watch a short ad to get") + $" 3 minutes of income right now.\n{left} left today."));
                    b.Add(BtnRow(UiKit.Button("Get it", "big ad" + (left > 0 ? "" : " gray"), () =>
                    {
                        if (Store.Caps().freeCash >= Cfg.Ads.LimitFreeCash) { Toast("No more free cash today", "clock"); return; }
                        Ads.Rewarded("free_cash", ok => { if (!ok) return; Store.Caps().freeCash++; Game.AddCash(v); RewardBurst(v, 0); Sfx.Play("reward"); ClosePanel(true); });
                    })));
                },
            };
        }

        static string DailyIcon(RewardDef d) => d.kind == "cash" ? "cash" : d.kind == "gems" ? (d.hat ? "crown" : "gem") : d.kind == "boost" ? "income" : "wheel";
        static string DailyLabel(RewardDef d) => d.kind == "cash" ? "$" + U.Fmt(Game.RewardFor(d.minutes)) : d.kind == "gems" ? $"{d.n} gems{(d.hat ? " + hat" : "")}" : d.kind == "boost" ? $"2x for {d.secs / 60}m" : $"{d.n} spins";
        static string SpinIcon(RewardDef sg) => sg.kind == "cash" ? (sg.minutes >= 30 ? "trophy" : "cash") : sg.kind == "gems" ? "gem" : sg.boost == "speed" ? "shoe" : "income";

        static PanelDef DailyPanel(PanelArgs o)
        {
            var s = S; bool canClaim = s.daily.last != U.Today(); int day = s.daily.day % 7;
            return new PanelDef
            {
                title = "Daily Rewards", icon = "calendar", sub = "Come back every day for bigger rewards", wide = true,
                render = (b, cur) =>
                {
                    var grid = UiKit.El("grid");
                    for (int i = 0; i < Cfg.Daily.Count; i++)
                    {
                        var d = Cfg.Daily[i];
                        string st = i < day ? " claimed" : i == day && canClaim ? " today" : "";
                        var tile = UiKit.El("tile" + st);
                        tile.Add(UiKit.Icon(DailyIcon(d)));
                        tile.Add(UiKit.Txt("Day " + (i + 1), "t-name"));
                        tile.Add(UiKit.Txt(DailyLabel(d), "t-sub"));
                        if (i < day) tile.Add(UiKit.Icon("check", "icon check"));
                        grid.Add(tile);
                    }
                    b.Add(grid);
                    void Claim(int mult)
                    {
                        GrantReward(Cfg.Daily[day], mult);
                        S.daily.last = U.Today(); S.daily.day++;
                        Store.Save(); ClosePanel(true); Tick1s();
                    }
                    if (canClaim) b.Add(BtnRow(UiKit.Button("Claim", "big", () => Claim(1)), UiKit.Button("Claim x2", "big ad purple", () => Ads.Rewarded("daily_x2", ok => { if (ok) Claim(2); }))));
                    else
                    {
                        var midnight = DateTime.Today.AddDays(1);
                        b.Add(Note("Next reward in " + U.FmtTime((midnight - DateTime.Now).TotalSeconds)));
                    }
                },
            };
        }

        static readonly uint[] SpinCols = { 0xff4d5a, 0xffc93c, 0x46c23a, 0x2f8cff, 0xb44cff, 0xff8a1f, 0x22c7c7, 0xff6fb1 };
        static PanelDef SpinPanel(PanelArgs o)
        {
            return new PanelDef
            {
                title = "Lucky Spin", icon = "wheel",
                render = (b, cur) =>
                {
                    var s = S;
                    bool free = SpinFreeReady(); var caps = Store.Caps(); int adLeft = Cfg.Ads.LimitSpin - caps.spin;
                    var segs = Cfg.Spin; int n = segs.Count;
                    var wrap = UiKit.El("wheel-wrap");
                    var wheel = new SpinWheel(segs.Select((x, i) => (U.Hex(SpinCols[i % SpinCols.Length]), x.label, SpinIcon(x))).ToList());
                    wheel.AddToClassList("wheel");
                    wheel.style.rotate = new Rotate(new Angle(s.spin.angle, AngleUnit.Degree));
                    wrap.Add(wheel);
                    var ptr = new Pointer(); ptr.AddToClassList("wheel-ptr");
                    wrap.Add(ptr);
                    var hub = UiKit.El("wheel-hub"); hub.Add(UiKit.Txt("SPIN"));
                    wrap.Add(hub);
                    b.Add(wrap);
                    bool spinning = false;
                    void DoSpin(string how)
                    {
                        if (spinning) return;
                        spinning = true;
                        int pick = U.Weighted(Enumerable.Range(0, n).ToList(), i => segs[i].w);
                        float a0 = S.spin.angle, bas = a0 - a0 % 360f, target = bas + 360f * 5f + (360f - pick * 360f / n);
                        S.spin.angle = target;
                        if (how == "free") S.spin.lastFree = U.NowMs; else if (how == "token") S.spin.tokens--; else Store.Caps().spin++;
                        float t = 0f, tickAcc = 0f; int ticks = 0;
                        cur.update = dt =>
                        {
                            t += dt; tickAcc += dt;
                            float k = Mathf.Clamp01(t / 4.2f), e = 1f - Mathf.Pow(1f - k, 4f);
                            wheel.style.rotate = new Rotate(new Angle(Mathf.Lerp(a0, target, e), AngleUnit.Degree));
                            if (tickAcc > 0.14f && ticks < 27) { tickAcc = 0f; ticks++; Sfx.Play("tick"); }
                            if (t >= 4.3f)
                            {
                                cur.update = null;
                                GrantReward(segs[pick], 1); Store.Save();
                                if (current == cur) Rerender();
                                Tick1s();
                            }
                        };
                    }
                    if (free || s.spin.tokens > 0) b.Add(BtnRow(UiKit.Button(free ? "Free spin" : $"Spin ({s.spin.tokens} left)", "big", () => DoSpin(free ? "free" : "token"))));
                    else b.Add(BtnRow(UiKit.Button("Spin", "big ad purple" + (adLeft > 0 ? "" : " gray"), () =>
                    {
                        if (Store.Caps().spin >= Cfg.Ads.LimitSpin) { Toast("No more bonus spins today", "clock"); return; }
                        Ads.Rewarded("spin", ok => { if (ok) DoSpin("ad"); });
                    })));
                    double wait = (Game.SpinEvery() * 1000 - (U.NowMs - s.spin.lastFree)) / 1000.0;
                    b.Add(Note(free ? "Your free spin is ready!" : $"Free spin in {U.FmtTime(wait)} · {Math.Max(0, adLeft)} bonus spins left today"));
                },
            };
        }

        static PanelDef AlbumPanel(PanelArgs o)
        {
            var s = S; var types = Cfg.Cars.Keys.ToList(); int got = types.Count(t => s.album.ContainsKey(t));
            return new PanelDef
            {
                title = "Car Album", icon = "album", sub = $"{got} of {types.Count} collected", wide = true,
                render = (b, cur) =>
                {
                    var grid = UiKit.El("grid");
                    foreach (var t in types)
                    {
                        int n = s.album.TryGetValue(t, out var v) ? v : 0;
                        var tile = UiKit.El("tile" + (n > 0 ? "" : " unknown"));
                        tile.Add(UiKit.Icon(t == "golden" ? "golden" : "car"));
                        tile.Add(UiKit.Txt(n > 0 ? Cfg.Cars[t].name : "???", "t-name"));
                        tile.Add(UiKit.Txt(n > 0 ? $"Washed {n}" : "Not seen yet", "t-sub"));
                        grid.Add(tile);
                    }
                    b.Add(grid);
                    b.Add(Note($"Every new car type gives 3 gems. Collect all {types.Count} for a 100 gem bonus."));
                    if (got >= types.Count && !s.albumDone)
                        b.Add(BtnRow(UiKit.Button("Claim 100 gems", "big gold", () => { S.albumDone = true; Game.AddGems(100); RewardBurst(0, 100); Rerender(); })));
                },
            };
        }

        static readonly Dictionary<string, string> RegionIcons = new Dictionary<string, string>
        { { "Hometown", "pin" }, { "USA", "flag" }, { "Europe", "flag" }, { "Asia", "flag" }, { "Around the World", "planet" }, { "Space", "planet" } };

        static PanelDef MapPanel(PanelArgs o)
        {
            return new PanelDef
            {
                title = "World Tour", icon = "map", sub = "Open a car wash in every city on Earth, then the stars", wide = true,
                render = (b, cur) =>
                {
                    var s = S;
                    foreach (var region in Cfg.Regions)
                    {
                        var head = UiKit.El("region");
                        head.Add(UiKit.Icon(RegionIcons.TryGetValue(region, out var ri) ? ri : "flag"));
                        head.Add(UiKit.Txt(region.ToUpperInvariant()));
                        int open = 0, total = 0;
                        for (int i = 0; i < Cfg.Locations.Count; i++) if (Cfg.Locations[i].region == region) { total++; if (i < s.unlockedLocs) open++; }
                        head.Add(UiKit.Txt($"{open}/{total} open", "region-sub"));
                        b.Add(head);
                        for (int i = 0; i < Cfg.Locations.Count; i++)
                        {
                            var L = Cfg.Locations[i];
                            if (L.region != region) continue;
                            bool unlocked = i < s.unlockedLocs, here = i == Game.LocIdx;
                            s.locs.TryGetValue(L.id, out var ls);
                            int doneN = ls != null ? ls.pads.Count : 0;
                            int idx = i;
                            var btn = here ? UiKit.Button("Here", "gold disabled", null) : unlocked ? UiKit.Button("Go", "blue", () =>
                            {
                                ClosePanel(true);
                                Ads.Interstitial("travel", () => Game.Travel(idx));
                            }) : UiKit.Button("Locked", "gray disabled", null);
                            string sub;
                            var perk = Cfg.PerkFor(L.id);
                            string perkTxt = perk == null ? "" : unlocked ? $" · perk: {perk.name}" : $" · unlocks {perk.name}: {perk.desc}";
                            if (!unlocked) sub = $"Prices x{U.Fmt(L.mult)}{perkTxt}";
                            else if (here) sub = $"Prices x{U.Fmt(L.mult)} · {doneN}/{Cfg.Pads.Count} built · you are here{perkTxt}";
                            else if (ls != null && ls.branchRate > 0) sub = $"Prices x{U.Fmt(L.mult)} · {doneN}/{Cfg.Pads.Count} built · branch earns ${U.Fmt(ls.branchRate)}/s{perkTxt}";
                            else sub = $"Prices x{U.Fmt(L.mult)} · {doneN}/{Cfg.Pads.Count} built{perkTxt}";
                            b.Add(UiKit.Row(unlocked ? (here ? "pin" : "office") : "lock", L.name, sub, unlocked ? (float)doneN / Cfg.Pads.Count : (float?)null, btn, "loc-row" + (here ? " current" : "") + (unlocked ? "" : " locked")));
                        }
                    }
                    b.Add(Note("Build the Road Trip pad in your newest city to open the next one. Cities you leave keep earning as branches of your empire."));
                },
            };
        }

        static PanelDef EmpirePanel(PanelArgs o)
        {
            return new PanelDef
            {
                title = "Suds Empire", icon = "trophy", sub = "Your car washes around the world", wide = true,
                render = (b, cur) =>
                {
                    var s = S;
                    var branches = Game.Branches().ToList();
                    double rate = Game.EmpireRate(), pending = Game.EmpirePending();
                    var hero = UiKit.El("empire-hero");
                    hero.Add(UiKit.Txt($"${U.Fmt(rate)}/s", "empire-rate"));
                    hero.Add(UiKit.Txt(branches.Count == 0
                        ? "Your empire starts when you open your second car wash. Cities you leave keep earning while you grow the next one."
                        : $"{branches.Count + 1} car washes · {branches.Count} branch{(branches.Count == 1 ? "" : "es")} earning {Mathf.RoundToInt(Game.BranchEff() * 100)}% of their income, up to {Game.AwayHours()}h at a time", "empire-sub"));
                    b.Add(hero);
                    if (branches.Count > 0)
                    {
                        b.Add(HeroNum(("cash", U.Fmt(pending))));
                        var collect = UiKit.Button("Collect", "big" + (pending >= 1 ? "" : " gray"), () =>
                        {
                            if (Game.EmpirePending() < 1) { Toast("Nothing to collect yet", "clock"); return; }
                            double v = Game.CollectEmpire(1); RewardBurst(v, 0); Sfx.Play("cash"); Rerender();
                        });
                        var x2 = UiKit.Button("Collect x2", "big ad purple" + (pending >= 1 ? "" : " gray"), () =>
                        {
                            if (Game.EmpirePending() < 1) { Toast("Nothing to collect yet", "clock"); return; }
                            Ads.Rewarded("empire_x2", ok => { if (!ok) return; double v = Game.CollectEmpire(2); RewardBurst(v, 0); Sfx.Play("reward"); Rerender(); });
                        });
                        b.Add(BtnRow(collect, x2));
                        b.Add(UiKit.Txt("Branches", "sect"));
                        float cap = Game.AwayHours() * 3600f;
                        foreach (var (def, ls) in branches)
                        {
                            double p = Game.BranchPending(ls);
                            float fill = ls.branchRate > 0 ? (float)(p / (ls.branchRate * cap)) : 0f;
                            b.Add(UiKit.Row("office", def.name, $"${U.Fmt(ls.branchRate)}/s · ${U.Fmt(p)} waiting" + (fill >= 0.999f ? " · vault full!" : ""), fill));
                        }
                    }
                    b.Add(UiKit.Txt($"City perks: {Cfg.Perks.Count(x => Game.HasPerk(x.city))}/{Cfg.Perks.Count} collected", "sect"));
                    foreach (var pk in Cfg.Perks)
                    {
                        bool have = Game.HasPerk(pk.city);
                        string city = Cfg.Locations[Cfg.LocIndex(pk.city)].name;
                        b.Add(UiKit.Row(have ? pk.icon : "lock", pk.name, have ? pk.desc : $"Open {city} to unlock: {pk.desc}", null, null, have ? null : "locked"));
                    }
                    b.Add(UiKit.Txt("HQ gem upgrades: every city, for good", "sect"));
                    foreach (var d in Cfg.HqUps)
                    {
                        int l = s.Hq(d.id); bool mx = l >= d.max;
                        var dd = d;
                        b.Add(UiKit.Row(d.icon, $"{d.name} {Soft("Lv " + l)}", NowNext(d.text, d.num, l, mx), (float)l / d.max, GemBtn(d.cost(l), mx, () =>
                        {
                            int lv = S.Hq(dd.id);
                            if (lv >= dd.max) return;
                            if (S.gems < dd.cost(lv)) { Sfx.Play("error"); Toast("Not enough gems", "gem"); return; }
                            Game.AddGems(-dd.cost(lv)); S.AddHq(dd.id); Sfx.Play("upgrade");
                            if (dd.id == "tickets") Blaster.RefillTickets();
                            Rerender(); Store.Save();
                        })));
                    }
                    if (s.unlockedLocs < Cfg.Locations.Count)
                    {
                        var nx = Cfg.Locations[s.unlockedLocs];
                        b.Add(Note($"Next city: {nx.name} ({nx.region}). Build the Road Trip pad in {Cfg.Locations[s.unlockedLocs - 1].name} to open it."));
                    }
                    else b.Add(Note("Every city is open. Keep upgrading your branches and HQ."));
                    cur.tick = () => { if (current == cur && Game.Branches().Any()) Rerender(); };
                },
            };
        }

        static string HatName(string id) => Cfg.Hats.Find(h => h.id == id)?.name ?? id;

        /// <summary>"New perk unlocked" celebration when a city is opened for the first time.</summary>
        static PanelDef PerkPanel(PanelArgs o)
        {
            var p = Cfg.PerkFor(o.tab);
            if (p == null) return null;
            int got = Cfg.Perks.Count(x => Game.HasPerk(x.city));
            return new PanelDef
            {
                title = "New Perk Unlocked!", icon = "trophy", sub = $"{Cfg.Locations[Cfg.LocIndex(p.city)].name} perk · works in every city · {got}/{Cfg.Perks.Count} collected",
                render = (b, cur) =>
                {
                    var h = HeroNum((p.icon, p.name));
                    h.Q<Label>().style.color = U.Hex(0x1a8f2a);
                    b.Add(h);
                    b.Add(Note(p.desc + (p.hat != null ? $"\nBonus: a {HatName(p.hat)} for your manager (wear it from the Shop)" : ""), "note big-note"));
                    b.Add(BtnRow(UiKit.Button("Awesome!", "big", () => { Sfx.Play("reward"); ClosePanel(true); })));
                },
            };
        }

        static PanelDef TravelPanel(PanelArgs o)
        {
            var L = Cfg.Locations[o.to];
            return new PanelDef
            {
                title = "New Location!", icon = "map",
                render = (b, cur) =>
                {
                    var h = HeroNum(("pin", L.name));
                    h.Q<Label>().style.color = U.Hex(0x1a46b8);
                    b.Add(h);
                    var perk = Cfg.PerkFor(L.id);
                    if (perk != null) b.Add(UiKit.Row(perk.icon, "New perk: " + perk.name, perk.desc + (perk.hat != null ? $" + a {HatName(perk.hat)}" : "")));
                    b.Add(Note($"{L.blurb} Prices are x{U.Fmt(L.mult)} here. Your Franchise Kit is already built, your Business upgrades come with you, and the city you leave keeps earning as a branch."));
                    int to = o.to;
                    b.Add(BtnRow(UiKit.Button("Travel now", "big", () => { ClosePanel(true); Ads.Interstitial("travel", () => Game.Travel(to)); }), UiKit.Button("Later", "big gray", () => ClosePanel(true))));
                },
            };
        }

        static PanelDef FinalePanel(PanelArgs o) => new PanelDef
        {
            title = "Car Wash Legend!", icon = "trophy",
            render = (b, cur) =>
            {
                var h = HeroNum(("crown", "You did it!"));
                h.Q<Label>().style.color = U.Hex(0xd48f00);
                b.Add(h);
                b.Add(Note("You built car washes from your hometown to Mars. Keep growing your branches, maxing upgrades and Golden Sponges, and watch for new cities in future updates."));
                b.Add(BtnRow(UiKit.Button("Keep washing", "big", () => ClosePanel(true))));
            },
        };

        // =====================================================================
        // Settings, confirm
        // =====================================================================
        static bool Dev => Application.isEditor || Debug.isDebugBuild;
        static PanelDef SettingsPanel(PanelArgs o) => new PanelDef
        {
            title = "Settings", icon = "gear",
            render = (b, cur) =>
            {
                var s = S;
                VisualElement Toggle(bool on, Action flip)
                {
                    var t = UiKit.Btn("toggle" + (on ? " on" : ""), () => { flip(); Sfx.Play("click"); Rerender(); }, UiKit.El("knob"));
                    return t;
                }
                b.Add(UiKit.Row("music", "Music", null, null, Toggle(s.settings.music, () => { S.settings.music = !S.settings.music; Sfx.SetMusic(S.settings.music); })));
                b.Add(UiKit.Row("sound", "Sound effects", null, null, Toggle(s.settings.sfx, () => { S.settings.sfx = !S.settings.sfx; Sfx.SetSfx(S.settings.sfx); })));
                b.Add(UiKit.Row("vibrate", "Vibration", null, null, Toggle(s.settings.vib, () => S.settings.vib = !S.settings.vib)));
                bool high = s.settings.quality == "high";
                b.Add(UiKit.Row("sparkles", "Graphics", "Low turns off shadows for older phones", null, UiKit.Button(high ? "High" : "Low", high ? "blue" : "gray", () =>
                {
                    S.settings.quality = S.settings.quality == "high" ? "low" : "high";
                    SudsGame.SetQuality(S.settings.quality); Rerender();
                })));
                b.Add(Note(Dev ? $"Ads: {Ads.ProviderName} provider · Version {Application.version}" : $"Version {Application.version}"));
                b.Add(BtnRow(UiKit.Button("Reset progress", "red", () => OpenPanel("confirm", new PanelArgs
                {
                    title = "Reset progress?", text = "This deletes your car wash, cash, gems and level on this device.", ok = "Reset", danger = true,
                    cb = SudsGame.ResetProgress,
                }))));
                if (Dev)
                {
                    b.Add(UiKit.Txt("Developer", "sect"));
                    b.Add(BtnRow(
                        UiKit.Button("+ lots of cash", "blue", () => Game.DevGive(Game.RewardFor(60) * 3)),
                        UiKit.Button("+500 gems", "purple", () => Game.DevGive(0, 500)),
                        UiKit.Button("Build all pads", "", () => { ClosePanel(true); Game.DevBuildAll(); }),
                        UiKit.Button("+1 level", "gold", () => Game.AddXP(Cfg.XpFor(S.level) - S.xp))));
                }
                Store.Save();
            },
        };

        static PanelDef ConfirmPanel(PanelArgs o) => new PanelDef
        {
            title = o.title, icon = "flag",
            render = (b, cur) =>
            {
                b.Add(Note(o.text, "note big-note"));
                b.Add(BtnRow(UiKit.Button(o.ok ?? "OK", "big" + (o.danger ? " red" : ""), () => { ClosePanel(true); o.cb?.Invoke(); }), UiKit.Button("Cancel", "big gray", () => ClosePanel(true))));
            },
        };

        // =====================================================================
        // Offline, level up, gift, golden car
        // =====================================================================
        static PanelDef OfflinePanel(PanelArgs o)
        {
            var off = o.off;
            return new PanelDef
            {
                title = "Welcome back!", icon = "clock", sub = "You were away " + U.FmtTime(off.secs),
                render = (b, cur) =>
                {
                    b.Add(Note("Your crew kept the bubbles going and earned"));
                    b.Add(HeroNum(("cash", U.Fmt(off.amount))));
                    if (off.capped) b.Add(Note($"Offline earnings stop after {off.capH}h. The Night Shift HQ upgrade raises the limit."));
                    void Take(int m) { Game.AddCash(off.amount * m); RewardBurst(off.amount * m, 0); Sfx.Play("reward"); ClosePanel(true); }
                    b.Add(BtnRow(UiKit.Button("Collect", "big", () => Take(1)), UiKit.Button("Collect x3", "big ad purple", () => Ads.Rewarded("offline_x3", ok => { if (ok) Take(3); }))));
                },
            };
        }

        static PanelDef NoOfflinePanel(PanelArgs o) => new PanelDef
        {
            title = "Welcome back!", icon = "clock",
            render = (b, cur) =>
            {
                b.Add(Note("Your lot sat idle while you were away. Hire a <b>Cashier</b> and a <b>Stocker</b> and they'll earn money even when you're not playing.", "note big-note"));
                b.Add(BtnRow(UiKit.Button("Let's go", "big", () => ClosePanel(true))));
            },
        };

        static readonly Dictionary<string, string> UnlockNames = new Dictionary<string, string>
        { { "spin", "Lucky Spin" }, { "shop", "Shop" }, { "gift", "Gift Balloons" }, { "blaster", "Bubble Blaster" }, { "album", "Car Album" }, { "golden", "Golden Cars" }, { "rush", "Rush Hour" }, { "daily", "Daily Rewards" } };
        static PanelDef LevelUpPanel(PanelArgs o)
        {
            var unl = Cfg.Unlocks.Where(kv => kv.Value == o.level).Select(kv => UnlockNames[kv.Key]).ToList();
            return new PanelDef
            {
                title = $"Level {o.level}!", icon = "level", breakAfter = true,
                render = (b, cur) =>
                {
                    var h = HeroNum(("gem", "+" + o.gems));
                    h.Q<Label>().style.color = U.Hex(0x9126e0);
                    b.Add(h);
                    if (unl.Count > 0) b.Add(Note("Unlocked: <b>" + string.Join(", ", unl) + "</b>", "note big-note"));
                    int gems = o.gems;
                    b.Add(BtnRow(UiKit.Button("Awesome", "big", () => ClosePanel()),
                        UiKit.Button($"+{gems} more", "big ad purple", () => Ads.Rewarded("level_x2", ok => { if (!ok) return; Game.AddGems(gems); RewardBurst(0, gems); Sfx.Play("gem"); ClosePanel(true); }))));
                },
            };
        }

        static PanelDef GiftPanel(PanelArgs o)
        {
            double v = Game.RewardFor(4);
            return new PanelDef
            {
                title = "Gift Balloon!", icon = "gift",
                render = (b, cur) =>
                {
                    b.Add(Note("A gift floated over your car wash. Pick one:", "note big-note"));
                    void Take(bool cash) => Ads.Rewarded("gift_balloon", ok =>
                    {
                        if (!ok) return;
                        if (cash) { Game.AddCash(v); RewardBurst(v, 0); } else { Game.AddGems(5); RewardBurst(0, 5); }
                        Sfx.Play("reward"); Game.PopBalloon(); ClosePanel(true);
                    });
                    b.Add(BtnRow(UiKit.Button(U.Fmt(v), "big ad", () => Take(true), "cash"), UiKit.Button("5", "big ad purple", () => Take(false), "gem")));
                },
            };
        }

        static PanelDef GoldenPanel(PanelArgs o) => new PanelDef
        {
            title = "Golden Car!", icon = "golden",
            render = (b, cur) =>
            {
                b.Add(Note("A golden car is in the queue. It pays <b>x" + Game.GoldenPay() + "</b>. " + (Ads.Free ? "Boost it" : "Watch an ad") + " to make it pay <b>x10</b>!", "note big-note"));
                var car = o.car;
                b.Add(BtnRow(UiKit.Button("Make it x10", "big ad gold", () => Ads.Rewarded("golden_car", ok =>
                {
                    if (!ok) return;
                    if (car != null && !car.Dead) car.SetGoldenMult(10f);
                    Sfx.Play("reward"); ClosePanel(true);
                })), UiKit.Button("x" + Game.GoldenPay() + " is fine", "big gray", () => ClosePanel(true))));
            },
        };

        // =====================================================================
        // Rewards
        // =====================================================================
        public static void GrantReward(RewardDef d, int mult = 1)
        {
            var s = S;
            if (d.kind == "cash") { double v = Game.RewardFor(d.minutes) * mult; Game.AddCash(v); RewardBurst(v, 0); }
            else if (d.kind == "gems")
            {
                Game.AddGems(d.n * mult); RewardBurst(0, d.n * mult);
                if (d.hat)
                {
                    var next = Cfg.Hats.FirstOrDefault(h => !s.hats.owned.Contains(h.id));
                    if (next != null) { s.hats.owned.Add(next.id); Toast($"New hat: {next.name}! Wear it from the Shop.", "hat_" + next.id); }
                }
            }
            else if (d.kind == "boost") { Game.AddBoost(d.boost, d.secs * mult); Toast(d.boost == "income" ? "2x income is on!" : "Speed shoes on!", d.boost == "income" ? "income" : "shoe"); }
            else if (d.kind == "spins") { s.spin.tokens += d.n * mult; Toast($"+{d.n * mult} free spins", "wheel"); }
            Sfx.Play("reward");
        }
    }

    /// <summary>The Lucky Spin wheel: coloured segments with labels and icons, rotated by the panel.</summary>
    public class SpinWheel : VisualElement
    {
        readonly List<(Color col, string label, string icon)> segs;
        public SpinWheel(List<(Color col, string label, string icon)> segs)
        {
            this.segs = segs;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }
        void Layout()
        {
            Clear();
            var r = contentRect;
            float size = Mathf.Min(r.width, r.height);
            if (size <= 0f) return;
            float k = size / 220f;
            int n = segs.Count;
            for (int i = 0; i < n; i++)
            {
                float am = (i / (float)n) * Mathf.PI * 2f - Mathf.PI / 2f;
                var lbl = UiKit.Txt(segs[i].label, "wheel-seg-label");
                lbl.style.left = r.width / 2f + Mathf.Cos(am) * 68f * k;
                lbl.style.top = r.height / 2f + Mathf.Sin(am) * 68f * k;
                lbl.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                lbl.style.rotate = new Rotate(new Angle(am * Mathf.Rad2Deg + 90f, AngleUnit.Degree));
                Add(lbl);
                var ic = UiKit.Icon(segs[i].icon, "wheel-seg-icon");
                ic.style.left = r.width / 2f + Mathf.Cos(am) * 44f * k - 15f;
                ic.style.top = r.height / 2f + Mathf.Sin(am) * 44f * k - 15f;
                Add(ic);
            }
        }
        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float size = Mathf.Min(r.width, r.height);
            if (size <= 0f) return;
            var c = r.center;
            float R = size / 2f - 2f;
            var p = ctx.painter2D;
            int n = segs.Count;
            // white rim + brand ring (the web used box-shadows)
            p.fillColor = U.Hex(0x2563eb); p.BeginPath(); p.Arc(c, R + 2f, 0f, 360f); p.Fill();
            p.fillColor = Color.white; p.BeginPath(); p.Arc(c, R - 2f, 0f, 360f); p.Fill();
            float rr = R - 8f;
            for (int i = 0; i < n; i++)
            {
                float a1 = i * 360f / n - 90f - 180f / n, a2 = (i + 1) * 360f / n - 90f - 180f / n;
                p.fillColor = segs[i].col;
                p.strokeColor = Color.white; p.lineWidth = 2f;
                p.BeginPath(); p.MoveTo(c); p.Arc(c, rr, a1, a2); p.ClosePath(); p.Fill(); p.Stroke();
            }
        }
    }

    /// <summary>Red triangle pointer at the top of the wheel.</summary>
    public class Pointer : VisualElement
    {
        public Pointer() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var p = ctx.painter2D;
            p.fillColor = U.Hex(0xff4d5a); p.strokeColor = Color.white; p.lineWidth = 4f; p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(r.width * 0.1f, r.height * 0.08f));
            p.LineTo(new Vector2(r.width * 0.9f, r.height * 0.08f));
            p.LineTo(new Vector2(r.width * 0.5f, r.height * 0.92f));
            p.ClosePath(); p.Fill(); p.Stroke();
        }
    }
}
