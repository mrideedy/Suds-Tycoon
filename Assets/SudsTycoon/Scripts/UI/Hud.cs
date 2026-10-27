using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>
    /// HUD, upgrade cards, toasts and small UI effects (port of ui.js). Panels live in Panels.cs.
    /// Everything is built in code on one UIDocument; layers are siblings in draw order.
    /// </summary>
    public static partial class Hud
    {
        public static VisualElement Root { get; private set; }
        public static VisualElement Touch { get; private set; }
        public static VisualElement Labels { get; private set; }
        public static VisualElement HudLayer { get; private set; }
        public static VisualElement BannerAd { get; private set; }
        public static VisualElement AdLayer { get; private set; }
        public static VisualElement BlasterLayer { get; private set; }
        public static VisualElement Joystick { get; private set; }
        public static VisualElement JoyKnob { get; private set; }

        static VisualElement cardsWrap, toasts, panelLayer, fade, loading, edgeArrow, edgeIcon, streakEl, eventBanner;
        static ScrollView cardsView;
        static Label lvlNum, xpText, cashVal, gemVal;
        static VisualElement xpFill, questEl, cashPill, gemPill, cashPlus, sideLeft, sideRight;
        static readonly Dictionary<string, VisualElement> sideBtns = new Dictionary<string, VisualElement>();
        static readonly Dictionary<string, (VisualElement btn, Label sub, VisualElement subIcon)> boostBtns = new Dictionary<string, (VisualElement, Label, VisualElement)>();
        static readonly List<(VisualElement card, string kind, string id)> cards = new List<(VisualElement, string, string)>();
        static double shownCash;
        static string freeCardType;
        static float bannerT, plusPulseT;
        static bool inited, bannerVisible;

        static SaveData S => Store.S;

        internal static void ResetStatics()
        {
            inited = false; current = null; pendingStart = null; bannerVisible = false; padPrompt = null; promptPad = null;
            Intro.Reset();
            cards.Clear(); sideBtns.Clear(); boostBtns.Clear();
            Root = Touch = Labels = HudLayer = BannerAd = AdLayer = BlasterLayer = Joystick = JoyKnob = null;
            cashVal = gemVal = lvlNum = xpText = null; questEl = null;
        }

        // =====================================================================
        // Setup
        // =====================================================================
        /// <summary>Build the layer stack on the document root. Called once at boot, before the world loads.</summary>
        public static void Build(VisualElement root)
        {
            Root = root;
            root.AddToClassList("suds-root");
            Touch = Layer("touch"); Touch.pickingMode = PickingMode.Position;
            Labels = Layer("labels"); Labels.style.overflow = Overflow.Hidden;
            WorldUI.Init(Labels);
            Joystick = UiKit.El("joystick");
            JoyKnob = UiKit.El("joy-knob");
            Joystick.Add(JoyKnob);
            root.Add(Joystick);
            edgeArrow = UiKit.El("edge-arrow");
            edgeIcon = UiKit.Icon("arrow");
            edgeArrow.Add(edgeIcon);
            root.Add(edgeArrow);
            HudLayer = Layer("hud");
            BlasterLayer = Layer("bb-hud");
            BlasterLayer.style.display = DisplayStyle.None;
            BannerAd = UiKit.El("banner-ad");
            BannerAd.style.display = DisplayStyle.None;
            root.Add(BannerAd);
            toasts = Layer("toasts-layer");
            var tl = UiKit.El("toasts");
            toasts.Add(tl);
            toasts = tl;
            panelLayer = Layer("panel-layer");
            panelLayer.pickingMode = PickingMode.Position;
            panelLayer.RegisterCallback<PointerDownEvent>(e => { if (e.target == panelLayer && current != null && !current.def.noClose) ClosePanel(); });
            AdLayer = Layer("ad-layer");
            AdLayer.pickingMode = PickingMode.Position;
            fade = Layer("fade");
            loading = Layer("loading");
            loading.pickingMode = PickingMode.Position;
        }

        static VisualElement Layer(string cls)
        {
            var e = UiKit.El("layer " + cls);
            Root.Add(e);
            return e;
        }

        /// <summary>Fill in the HUD once the save and the first location are loaded.</summary>
        public static void Init()
        {
            var h = HudLayer;
            h.Clear();
            // top left: level + XP, quest card
            var tl = UiKit.El("hud-tl");
            var lvlRow = UiKit.El("lvl-row");
            var xpBar = UiKit.El("xp-bar");
            xpFill = UiKit.El("xp-fill");
            xpBar.Add(xpFill);
            xpText = UiKit.Txt("0 / 60", "xp-text");
            xpBar.Add(xpText);
            var badge = UiKit.El("lvl-badge");
            badge.Add(UiKit.Icon("level"));
            lvlNum = UiKit.Txt("1", "lvl-num");
            badge.Add(lvlNum);
            lvlRow.Add(xpBar); lvlRow.Add(badge);   // row-reverse: the badge draws on top
            tl.Add(lvlRow);
            questEl = UiKit.Btn("quest", () => { if (Game.ClaimQuest()) Sfx.Play("click"); });
            tl.Add(questEl);
            h.Add(tl);
            // top right: cash, gems, settings
            var tr = UiKit.El("hud-tr");
            cashPill = UiKit.El("cur-pill");
            cashPill.Add(UiKit.Icon("cash", "icon pill-icon"));
            cashVal = UiKit.Txt("0", "val");
            cashPill.Add(cashVal);
            cashPlus = UiKit.Btn("plus-btn", () => { Sfx.Play("click"); OpenPanel("freecash"); }, UiKit.Icon("plus"));
            cashPill.Add(cashPlus);
            tr.Add(cashPill);
            gemPill = UiKit.El("cur-pill");
            gemPill.Add(UiKit.Icon("gem", "icon pill-icon"));
            gemVal = UiKit.Txt("0", "val");
            gemPill.Add(gemVal);
            gemPill.Add(UiKit.Btn("plus-btn", () => { Sfx.Play("click"); OpenPanel("shop", new PanelArgs { tab = "gems" }); }, UiKit.Icon("plus")));
            tr.Add(gemPill);
            tr.Add(UiKit.Btn("gear-btn", () => { Sfx.Play("click"); OpenPanel("settings"); }, UiKit.Icon("gear")));
            h.Add(tr);
            sideLeft = UiKit.El("side left");
            sideRight = UiKit.El("side right");
            h.Add(sideLeft); h.Add(sideRight);
            streakEl = UiKit.El("streak");
            h.Add(streakEl);
            eventBanner = UiKit.El("event-banner");
            h.Add(eventBanner);
            cardsWrap = UiKit.El("cards-wrap");
            cardsView = new ScrollView(ScrollViewMode.Horizontal) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            cardsView.AddToClassList("cards");
            cardsView.mode = ScrollViewMode.Horizontal;
            cardsView.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            cardsWrap.Add(cardsView);
            h.Add(cardsWrap);
            cardsWrap.RegisterCallback<GeometryChangedEvent>(_ => FitCards());
            DragScroll(cardsView);
            padPrompt = UiKit.Btn("pad-prompt", OnPadPrompt);
            if (!Ads.Free) padPrompt.Add(UiKit.Txt("AD", "ad-tag"));
            padPromptText = UiKit.Txt("", "pad-prompt-text");
            padPrompt.Add(padPromptText);
            padPrompt.style.display = DisplayStyle.None;
            h.Add(padPrompt);
            cardsWrap.style.bottom = bannerVisible ? 58f : 8f;   // the banner may have been shown before the HUD existed

            if (!inited)
            {
                inited = true;
                Bus.On("mat:enter", p =>
                {
                    if (current != null) return;
                    string panel = (string)p;
                    if (panel == "shop" && S.level < Cfg.Unlocks["shop"]) { Toast($"The Shop opens at level {Cfg.Unlocks["shop"]}", "lock"); return; }
                    if (panel == "staff" || panel == "office") OpenPanel("upgrades", new PanelArgs { tab = Game.Built("office") ? "business" : "machines" });
                    else OpenPanel(panel);
                });
                Bus.On("pad:broke", p =>
                {
                    var pad = (Pad)p;
                    // close to done: one ad finishes it; far away: point at free cash
                    if (pad.Remaining <= Game.RewardFor(6)) ShowPadPrompt(pad);
                    else if (Store.Caps().freeCash < Cfg.Ads.LimitFreeCash) OfferFreeCash();
                });
                Bus.On("level", _ => BuildSide());
            }
            shownCash = S.cash;
            cashVal.text = U.Fmt(shownCash);   // the counter only redraws on change, so draw the starting cash now
            RefreshAll();
            Responsive();
        }

        public static void RefreshAll()
        {
            if (cashVal == null) return;
            CashChanged(false); GemsChanged(); XpChanged(false); QuestChanged(); RefreshCards(); BuildSide();
        }
        public static void OnLocation() => RefreshAll();

        /// <summary>Web media queries: narrow &lt; 640 px, tiny &lt; 480 px, short &lt; 520 px tall.</summary>
        public static void Responsive()
        {
            if (Root == null) return;
            var r = Root.layout;
            if (float.IsNaN(r.width) || r.width <= 0f) return;
            Root.EnableInClassList("narrow", r.width < 640f);
            Root.EnableInClassList("tiny", r.width < 480f);
            Root.EnableInClassList("short", r.height < 520f);
            ApplySafeArea(r);
        }

        /// <summary>Keep the HUD, panels and toasts clear of notches and rounded corners (Screen.safeArea in panel units).</summary>
        static void ApplySafeArea(Rect r)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var sa = Screen.safeArea;
            float k = r.width / Screen.width;
            float l = sa.xMin * k, rt = (Screen.width - sa.xMax) * k, t = (Screen.height - sa.yMax) * k, b = sa.yMin * k;
            foreach (var e in new[] { HudLayer, BlasterLayer, toasts?.parent })
            {
                if (e == null) continue;
                e.style.left = l; e.style.right = rt; e.style.top = t; e.style.bottom = b;
            }
            // panels keep the stylesheet's 12 / 14 px breathing room on top of the insets
            if (panelLayer != null) { panelLayer.style.paddingLeft = 14 + l; panelLayer.style.paddingRight = 14 + rt; panelLayer.style.paddingTop = 12 + t; panelLayer.style.paddingBottom = 12 + b; }
        }

        // =====================================================================
        // Currency + XP
        // =====================================================================
        public static void CashChanged(bool bump)
        {
            if (cashVal == null) return;
            if (bump) Bump(cashVal);
            RefreshCardsAfford();
        }
        public static void GemsChanged()
        {
            if (gemVal == null) return;
            gemVal.text = U.Fmt(S.gems);
            Bump(gemVal);
        }
        static void Bump(VisualElement e)
        {
            e.AddToClassList("bump");
            e.schedule.Execute(() => e.RemoveFromClassList("bump")).ExecuteLater(130);
        }
        public static void XpChanged(bool up)
        {
            if (lvlNum == null) return;
            var s = S; int need = Cfg.XpFor(s.level);
            lvlNum.text = s.level.ToString();
            xpFill.style.width = Length.Percent((float)Math.Min(100.0, s.xp / need * 100.0));
            xpText.text = $"{Math.Floor(s.xp)} / {need}";
        }

        // ---------- quest card ----------
        public static void QuestChanged()
        {
            if (questEl == null || Game.Quest == null) return;
            var q = Game.Quest;
            var r = Game.QuestReward();
            questEl.Clear();
            var ico = UiKit.El("q-ico");
            ico.Add(UiKit.Icon(Game.QuestIcon(q)));
            questEl.Add(ico);
            var body = UiKit.El("q-body");
            body.Add(UiKit.Txt(Game.QuestText(q), "q-text"));
            body.Add(UiKit.Bar("q-prog", 0f, null));
            var rew = UiKit.El("q-reward");
            rew.Add(UiKit.Icon("cash")); rew.Add(UiKit.Txt(U.Fmt(r.cash)));
            rew.Add(UiKit.Icon("gem")); rew.Add(UiKit.Txt(r.gems.ToString()));
            body.Add(rew);
            questEl.Add(body);
            var claim = UiKit.El("q-claim");
            claim.Add(UiKit.Txt("Claim"));
            questEl.Add(claim);
            questEl.RemoveFromClassList("done");
            QuestTick();
        }
        static void QuestTick()
        {
            if (questEl == null || Game.Quest == null) return;
            var (p, n) = Game.QuestProgress();
            var bar = questEl.Q(className: "q-prog");
            if (bar != null) UiKit.SetBar(bar, (float)(p / n));
            bool done = p >= n;
            if (done != questEl.ClassListContains("done"))
            {
                questEl.EnableInClassList("done", done);
                if (done) { Sfx.Play("reward"); Toast("Task complete! Tap it to claim.", "flag"); }
            }
        }

        // ---------- side buttons ----------
        static void BuildSide()
        {
            if (sideLeft == null) return;
            var s = S;
            sideLeft.Clear(); sideBtns.Clear(); sideRight.Clear(); boostBtns.Clear();
            void Btn(VisualElement col, string id, string icon, string label, int lockLv)
            {
                bool locked = lockLv > 0 && s.level < lockLv;
                var disc = UiKit.El("disc");
                disc.Add(UiKit.Icon(icon));
                var dot = UiKit.El("dot");
                dot.Add(UiKit.Txt("!"));
                dot.style.display = DisplayStyle.None;
                var b = UiKit.Btn("side-btn" + (locked ? " locked" : ""), () =>
                {
                    if (lockLv > 0 && S.level < lockLv) { Toast($"Unlocks at level {lockLv}", "lock"); Sfx.Play("error"); return; }
                    Sfx.Play("click");
                    OpenPanel(id);
                }, disc, UiKit.Txt(label, "lbl"), UiKit.Icon("lock", "icon lock"), dot);
                col.Add(b);
                sideBtns[id] = b;
            }
            // left: the business (empire, world map, shop) and daily rewards
            Btn(sideLeft, "empire", "trophy", "Empire", 0);
            Btn(sideLeft, "map", "map", "World", 0);
            Btn(sideLeft, "shop", "bag", "Shop", Cfg.Unlocks["shop"]);
            Btn(sideLeft, "daily", "calendar", "Daily", Cfg.Unlocks["daily"]);
            Btn(sideLeft, "spin", "wheel", "Spin", Cfg.Unlocks["spin"]);

            void Boost(string id, string icon, string title)
            {
                var disc = UiKit.El("disc");
                disc.Add(UiKit.Icon(icon));
                var txt = UiKit.El("b-col");
                txt.Add(UiKit.Txt(title, "b-txt"));
                var sub = UiKit.El("b-sub");
                var subIcon = UiKit.Icon(Ads.Icon);
                var subTxt = UiKit.Txt("", null);
                sub.Add(subIcon); sub.Add(subTxt);
                txt.Add(sub);
                var b = UiKit.Btn("boost-btn", () => OnBoost(id), disc, txt);
                sideRight.Add(b);
                boostBtns[id] = (b, subTxt, subIcon);
            }
            // right: ad boosts, then the mini-game and album
            Boost("income", "income", "2x Income");
            Boost("wash", "bolt", "Turbo Wash");
            Boost("rush", "rush", "Car Rush");
            Boost("turbo", "turbo", "Turbo Tom");
            var extra = UiKit.El("side-col");
            Btn(extra, "blaster", "blaster", "Blaster", Cfg.Unlocks["blaster"]);
            Btn(extra, "album", "album", "Album", Cfg.Unlocks["album"]);
            sideRight.Add(extra);
            RefreshBoosts();
            Tick1s();
        }

        static void OnBoost(string name)
        {
            if (name == "turbo")
            {
                long c = S.cool.turbo - U.NowMs;
                if (c > 0) { Toast($"Turbo Tom is resting. Back in {U.FmtTime(c / 1000.0)}", "turbo"); return; }
            }
            // free rewards (no ad network) do not stack: one boost at a time
            else if (Ads.Free && S.Boost(name) > U.NowMs) { Toast("Already running! Tap again when it ends.", "clock"); return; }
            Ads.Rewarded("boost_" + name, ok =>
            {
                if (!ok) return;
                if (name == "turbo") S.cool.turbo = U.NowMs + (long)(Cfg.Ads.TurboEvery * 1000);
                Game.AddBoost(name, Cfg.Boosts[name].x);
                Sfx.Play("reward");
                switch (name)
                {
                    case "income": Toast("2x income for 10 more minutes!", "income"); break;
                    case "wash": Toast("Turbo Wash! Every machine works twice as fast.", "bolt"); break;
                    case "rush": Toast("Car Rush! Three times the customers.", "rush"); break;
                    case "speed": Toast("Speed shoes on. Zoom zoom!", "shoe"); break;
                    default: Toast("Turbo Tom is here to help!", "turbo"); break;
                }
            });
        }

        public static void RefreshBoosts()
        {
            foreach (var n in new[] { "income", "wash", "rush" })
            {
                if (!boostBtns.TryGetValue(n, out var b)) continue;
                double left = (S.Boost(n) - U.NowMs) / 1000.0;
                b.btn.EnableInClassList("active", left > 0);
                UiKit.SetIcon(b.subIcon, left > 0 ? "clock" : Ads.Icon);
                // the 2x income timer stacks with ads: while it runs the button still offers +10 min
                b.sub.text = left > 0 ? U.FmtTime(left) + (n == "income" && !Ads.Free ? "  +10m" : "") : n == "income" ? (Ads.Free ? "10 min" : "+10 min") : n == "wash" ? "5 min" : "3 min";
            }
            if (boostBtns.TryGetValue("turbo", out var t))
            {
                var active = Game.Workers.FirstOrDefault(w => w.Turbo);
                double cool = (S.cool.turbo - U.NowMs) / 1000.0;
                t.btn.EnableInClassList("active", active != null);
                UiKit.SetIcon(t.subIcon, active != null || cool > 0 ? "clock" : Ads.Icon);
                t.sub.text = active != null ? U.FmtTime(active.Life) : cool > 0 ? U.FmtTime(cool) : "Helper";
            }
        }

        /// <summary>Once per second: dots, timers, quest progress, tickets.</summary>
        public static void Tick1s()
        {
            var s = S;
            RefreshBoosts(); QuestTick();
            void Dot(string id, string v)
            {
                if (!sideBtns.TryGetValue(id, out var b)) return;
                var d = b.Q(className: "dot");
                d.style.display = v != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (v != null) d.Q<Label>().text = v;
                b.EnableInClassList("attn", v != null);
            }
            Dot("daily", s.level >= Cfg.Unlocks["daily"] && s.daily.last != U.Today() ? "!" : null);
            // branch cash worth collecting (a minute of this city's income or more)
            Dot("empire", Game.L != null && Game.EmpirePending() >= Math.Max(1, Game.IncomePerSec() * 60) ? "$" : null);
            Dot("spin", s.level >= Cfg.Unlocks["spin"] && (SpinFreeReady() || s.spin.tokens > 0) ? "!" : null);
            Blaster.RefillTickets();
            Dot("blaster", s.level >= Cfg.Unlocks["blaster"] && s.blaster.tickets > 0 ? s.blaster.tickets.ToString() : null);
            current?.tick?.Invoke();
        }
        static bool SpinFreeReady() => U.NowMs - S.spin.lastFree >= Game.SpinEvery() * 1000;

        // =====================================================================
        // Bottom upgrade cards (like the reference: Brush / Water / Dryer / Worker)
        // =====================================================================
        public static void RefreshCards()
        {
            if (cardsView == null || Game.L == null) return;
            var defs = new List<(string kind, string id)>();
            foreach (var t in Cfg.SlotOrder) if (Game.Lanes.Any(l => l.Get(t) != null)) defs.Add(("st", t));
            if (Game.Built("office")) defs.Add(("biz", "speed"));
            defs.Add(("more", null));
            bool freeReady = U.NowMs >= S.cool.freeUpgrade;
            freeCardType = null;
            if (freeReady && S.level >= 2)
            {
                var poor = defs.FirstOrDefault(d => d.kind == "st" && Game.Lv(d.id) < Cfg.StationMaxLv && !Game.CanAfford(Game.StationUpCost(d.id)));
                if (poor.id != null) freeCardType = poor.id;
            }
            cardsView.Clear(); cards.Clear();
            foreach (var d in defs)
            {
                VisualElement card;
                if (d.kind == "more")
                {
                    var buy = UiKit.El("buy"); buy.style.backgroundColor = U.Hex(0x3b74f0); buy.style.borderBottomColor = U.Hex(0x1d4fc4);
                    buy.Add(UiKit.Txt("Open"));
                    card = UiKit.Btn("ucard", () => { if (JustDragged) return; Sfx.Play("click"); OpenPanel("upgrades"); }, CardTop("rack", "Upgrades", "See all", null), buy);
                }
                else
                {
                    // the line under the name says what one tap gives you; the level sits in a badge on the icon
                    string name, icon, gain; int l, max; double cost;
                    if (d.kind == "st")
                    {
                        var c = Cfg.Stations[d.id]; name = c.shortName; icon = c.icon; l = Game.Lv(d.id); max = Cfg.StationMaxLv; cost = Game.StationUpCost(d.id);
                        gain = l >= max ? $"${U.Fmt(Game.MachinePay(d.id, l))}/car" : $"+${U.Fmt(c.inc * Game.L.mult)}/car";
                    }
                    else
                    {
                        var c = Cfg.BizById(d.id); name = "Team"; icon = "worker"; l = Game.Biz(d.id); max = c.max; cost = Game.BizCost(d.id);
                        gain = l >= max ? $"+{l * 5}% speed" : "+5% speed";
                    }
                    bool isMax = l >= max, free = d.id == freeCardType;
                    var buy = UiKit.El("buy");
                    if (isMax) buy.Add(UiKit.Txt("MAX"));
                    else if (free) { buy.Add(UiKit.Icon(Ads.Icon)); buy.Add(UiKit.Txt("FREE")); }
                    else { buy.Add(UiKit.Icon("cash")); buy.Add(UiKit.Txt(U.Fmt(cost))); }
                    string kind = d.kind, id = d.id;
                    card = UiKit.Btn("ucard" + (isMax ? " max" : "") + (free ? " free" : ""), null, CardTop(icon, name, gain, l > 0 ? l.ToString() : ""), buy);
                    card.AddManipulator(new Clickable(() => OnCard(card, kind, id)));
                    if (Game.Bottleneck == d.id) { var tag = UiKit.El("tag"); tag.Add(UiKit.Txt("SLOW")); card.Add(tag); }
                }
                cardsView.Add(card);
                cards.Add((card, d.kind, d.id));
            }
            RefreshCardsAfford();
            cardsView.schedule.Execute(FitCards).ExecuteLater(1);
        }

        /// <summary>Shrink the cards so the whole row fits on screen; scroll (drag) only when it still does not.</summary>
        static void FitCards()
        {
            if (cardsWrap == null || cards.Count == 0) return;
            float avail = cardsWrap.layout.width;
            if (float.IsNaN(avail) || avail <= 0) return;
            bool narrow = Root.ClassListContains("narrow");
            float gap = narrow ? 8f : 10f, pad = narrow ? 20f : 28f, max = narrow ? 148f : 214f, min = narrow ? 118f : 124f;
            float w = Mathf.Clamp((avail - pad - gap * (cards.Count - 1)) / cards.Count, min, max);
            bool compact = w < 190f && !narrow;
            foreach (var (card, _, _) in cards)
            {
                card.style.width = w;
                card.style.marginRight = gap;
                card.EnableInClassList("compact", compact);
            }
        }

        // Mouse / pen / touch drag to scroll a horizontal ScrollView; a drag cancels the tap on the card under it.
        static float lastDragEnd = -1f;
        static void DragScroll(ScrollView sv)
        {
            bool down = false, dragging = false; float startX = 0f, startOffset = 0f; int id = -1;
            sv.RegisterCallback<PointerDownEvent>(e => { down = true; dragging = false; id = e.pointerId; startX = e.position.x; startOffset = sv.scrollOffset.x; }, TrickleDown.TrickleDown);
            sv.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!down || e.pointerId != id) return;
                float dx = e.position.x - startX;
                if (!dragging && Mathf.Abs(dx) > 8f) dragging = true;
                if (dragging) sv.scrollOffset = new Vector2(startOffset - dx, sv.scrollOffset.y);
            }, TrickleDown.TrickleDown);
            void End() { if (dragging) lastDragEnd = Time.unscaledTime; down = false; dragging = false; }
            sv.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == id) End(); }, TrickleDown.TrickleDown);
            sv.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == id) End(); }, TrickleDown.TrickleDown);
            sv.RegisterCallback<WheelEvent>(e =>
            {
                sv.scrollOffset = new Vector2(sv.scrollOffset.x + (e.delta.y + e.delta.x) * 40f, sv.scrollOffset.y);
                e.StopPropagation();
            });
        }
        static bool JustDragged => Time.unscaledTime - lastDragEnd < 0.15f;

        // ---------- "finish this build" rewarded ad ----------
        static VisualElement padPrompt;
        static Label padPromptText;
        static Pad promptPad;
        public static void ShowPadPrompt(Pad pad)
        {
            if (padPrompt == null || current != null) return;
            promptPad = pad;
            padPromptText.text = "Finish " + pad.Name + " now";
            padPrompt.style.display = DisplayStyle.Flex;
        }
        public static void HidePadPrompt(Pad pad)
        {
            if (padPrompt == null || (pad != null && pad != promptPad)) return;
            promptPad = null;
            padPrompt.style.display = DisplayStyle.None;
        }
        static void OnPadPrompt()
        {
            var pad = promptPad;
            if (pad == null) return;
            Ads.Rewarded("pad_finish", ok =>
            {
                if (ok && pad.Shown) { pad.FinishWithAd(); Sfx.Play("reward"); }
                HidePadPrompt(pad);
            });
        }
        static VisualElement CardTop(string icon, string name, string gain, string level)
        {
            var top = UiKit.El("u-top");
            var ico = UiKit.El("u-ico"); ico.Add(UiKit.Icon(icon));
            if (!string.IsNullOrEmpty(level)) { var badge = UiKit.El("u-badge"); badge.Add(UiKit.Txt(level)); ico.Add(badge); }
            top.Add(ico);
            var info = UiKit.El("u-info");
            info.Add(UiKit.Txt(name, "u-name"));
            info.Add(UiKit.Txt(gain, "u-lv" + (level != null ? " u-gain" : "")));
            top.Add(info);
            return top;
        }
        static void RefreshCardsAfford()
        {
            foreach (var (card, kind, id) in cards)
            {
                if (kind == "more" || card.ClassListContains("max") || card.ClassListContains("free")) continue;
                double cost = kind == "st" ? Game.StationUpCost(id) : Game.BizCost(id);
                card.EnableInClassList("poor", !Game.CanAfford(cost));
            }
        }
        static void OnCard(VisualElement c, string kind, string id)
        {
            if (JustDragged || c.ClassListContains("max")) return;
            if (c.ClassListContains("free"))
            {
                Ads.Rewarded("free_upgrade", ok =>
                {
                    if (ok) { S.cool.freeUpgrade = U.NowMs + (long)(Cfg.Ads.FreeUpgradeEvery * 1000); Game.BuyStation(id, true); Toast("Free upgrade!", Cfg.Stations[id].icon); }
                    RefreshCards();
                });
                return;
            }
            bool ok2 = kind == "st" ? Game.BuyStation(id) : Game.BuyBiz(id);
            if (ok2)
            {
                RefreshCards();
                var nc = cards.FirstOrDefault(x => x.id == id).card;
                if (nc != null) { nc.AddToClassList("flash"); nc.schedule.Execute(() => nc.RemoveFromClassList("flash")).ExecuteLater(180); }
            }
            else Shake(c);
        }
        static void Shake(VisualElement e)
        {
            float t0 = U.Clock;
            e.schedule.Execute(() =>
            {
                float k = (U.Clock - t0) / 0.25f;
                e.style.translate = k >= 1f ? new Translate(0, 0, 0) : new Translate(Mathf.Sin(k * Mathf.PI * 4f) * 6f, 0, 0);
            }).Every(16).Until(() => U.Clock - t0 > 0.27f);
        }

        // =====================================================================
        // Small UI effects
        // =====================================================================
        public static void Toast(string text, string icon = null)
        {
            if (toasts == null) return;
            var el = UiKit.El("toast enter");
            if (icon != null) el.Add(UiKit.Icon(icon));
            el.Add(UiKit.Txt(text));
            toasts.Add(el);
            while (toasts.childCount > 3) toasts.RemoveAt(0);
            el.schedule.Execute(() => el.RemoveFromClassList("enter")).ExecuteLater(16);
            el.schedule.Execute(() =>
            {
                el.AddToClassList("out");
                el.schedule.Execute(() => el.RemoveFromHierarchy()).ExecuteLater(320);
            }).ExecuteLater(2600);
        }

        /// <summary>Icons burst from the screen centre and fly into the cash / gem pills.</summary>
        public static void RewardBurst(double cash, int gems)
        {
            if (cash > 0) FlyIcons("cash", cashPill, 8);
            if (gems > 0) FlyIcons("gem", gemPill, Math.Min(10, 3 + gems));
        }
        static void FlyIcons(string icon, VisualElement to, int n)
        {
            if (to == null || Root == null) return;
            var r = Root.layout;
            var tr = to.worldBound;
            var start = new Vector2(r.width / 2f, r.height / 2f);
            var end = new Vector2(tr.xMin + 20f, tr.center.y);
            for (int i = 0; i < n; i++)
            {
                var el = UiKit.Icon(icon, "burst-icon");
                Root.Add(el);
                var mid = start + new Vector2(U.Rand(-90f, 90f), U.Rand(-90f, 60f));
                float dur = 0.9f + i * 0.04f, t0 = U.Clock;
                void Place(float k)
                {
                    Vector2 p; float s;
                    if (k < 0.35f) { float a = U.EaseOutCubic(k / 0.35f); p = Vector2.Lerp(start, mid, a); s = Mathf.Lerp(0.4f, 1.1f, a); }
                    else { float a = U.EaseInOut((k - 0.35f) / 0.65f); p = Vector2.Lerp(mid, end, a); s = Mathf.Lerp(1.1f, 0.6f, a); }
                    el.style.translate = new Translate(p.x - 20f, p.y - 20f, 0);
                    el.style.scale = new Scale(new Vector3(s, s, 1f));
                }
                Place(0f);
                el.schedule.Execute(() =>
                {
                    float k = (U.Clock - t0) / dur;
                    if (k >= 1f) { el.RemoveFromHierarchy(); return; }
                    Place(k);
                }).Every(16).Until(() => el.parent == null);
            }
        }

        public static void LevelUp(int level, int gems)
        {
            Sched.After(current != null ? 0f : 0.4f, () => OpenPanel("levelup", new PanelArgs { level = level, gems = gems }));
            BuildSide();
        }

        public static void Streak(int n)
        {
            if (streakEl == null) return;
            streakEl.EnableInClassList("on", n >= 3);
            if (n < 3) return;
            streakEl.Clear();
            streakEl.Add(UiKit.Icon("sparkles"));
            streakEl.Add(UiKit.Txt($"WASH STREAK x{n} · tips +{Math.Min(n, 10) * 10}%"));
        }

        public static void Banner(string title, string sub, float secs)
        {
            if (eventBanner == null) return;
            eventBanner.Clear();
            eventBanner.Add(UiKit.Txt(title, "eb-title"));
            if (!string.IsNullOrEmpty(sub)) eventBanner.Add(UiKit.Txt(sub, "eb-sub"));
            eventBanner.AddToClassList("on");
            bannerT = Mathf.Min(4f, secs > 0 ? secs : 4f);
        }

        public static void GoldenOffer(Car car)
        {
            Toast("A Golden Car arrived! It pays x3.", "golden");
            Sched.After(1.2f, () => { if (current == null && !car.Dead) OpenPanel("golden", new PanelArgs { car = car }); });
        }
        public static void GiftOffer() => OpenPanel("gift");

        static void OfferFreeCash()
        {
            if (current != null) return;
            Toast("Short on cash? Tap + for free cash", "cash");
            plusPulseT = 1.5f;
        }

        /// <summary>Arrow on the screen edge pointing at an off-screen quest target.</summary>
        public static void EdgeArrow(Vector3? tgt)
        {
            if (edgeArrow == null) return;
            var cam = CameraRig.I ? CameraRig.I.Cam : null;
            if (!tgt.HasValue || cam == null) { edgeArrow.RemoveFromClassList("on"); return; }
            bool vis = WorldUI.Project(cam, tgt.Value + new Vector3(0f, 1f, 0f), out var p);
            var r = Root.layout;
            float W = r.width, H = r.height, m = 60f;
            bool inside = vis && p.x > m && p.x < W - m && p.y > m + 60f && p.y < H - 170f;
            if (inside) { edgeArrow.RemoveFromClassList("on"); return; }
            float cx = W / 2f, cy = H / 2f, dx = p.x - cx, dy = p.y - cy;
            if (!vis) { dx = -dx; dy = -dy; }
            float k = Mathf.Min((W / 2f - m) / Mathf.Max(Mathf.Abs(dx), 1e-3f), (H / 2f - m - 70f) / Mathf.Max(Mathf.Abs(dy), 1e-3f));
            edgeArrow.style.left = cx + dx * k;
            edgeArrow.style.top = cy + dy * k;
            edgeIcon.style.rotate = new Rotate(new Angle((Mathf.Atan2(dy, dx) + Mathf.PI / 2f) * Mathf.Rad2Deg, AngleUnit.Degree));
            edgeArrow.AddToClassList("on");
        }

        /// <summary>Blue fade used when travelling between locations.</summary>
        public static void Fade(Action cb)
        {
            fade.pickingMode = PickingMode.Position;
            fade.AddToClassList("on");
            fade.schedule.Execute(() =>
            {
                try { cb(); } catch (Exception e) { Debug.LogException(e); }
                fade.schedule.Execute(() => { fade.RemoveFromClassList("on"); fade.pickingMode = PickingMode.Ignore; }).ExecuteLater(120);
            }).ExecuteLater(380);
        }

        public static void SetBannerVisible(bool on)
        {
            bannerVisible = on;
            if (BannerAd == null) return;
            BannerAd.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (cardsWrap != null) cardsWrap.style.bottom = on ? 58f : 8f;
        }

        public static void ShowHud(bool on)
        {
            if (HudLayer == null) return;
            HudLayer.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            Labels.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) { edgeArrow.RemoveFromClassList("on"); Joystick.RemoveFromClassList("on"); }
            BannerAd.style.display = on && bannerVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // =====================================================================
        // Loading screen: the animated title scene lives in Intro
        // =====================================================================
        public static void ShowLoading() => Intro.Build(loading);
        /// <summary>"Tap to start" (the web needed a tap to unlock audio; here it gives the player a moment).</summary>
        public static void LoadingReady(bool fresh, Action go)
        {
            Intro.Ready(fresh ? "TAP TO START" : "TAP TO PLAY");
            bool done = false;
            void Go()
            {
                if (done) return;
                done = true;
                Sfx.Play("whoosh");
                Sfx.EndIntro();
                Intro.Leave();
                loading.AddToClassList("done");
                loading.pickingMode = PickingMode.Ignore;
                loading.schedule.Execute(() => { loading.style.display = DisplayStyle.None; Intro.Clear(); }).ExecuteLater(650);
                go();
            }
            loading.RegisterCallback<PointerDownEvent>(_ => Go());
            pendingStart = Go;
        }
        static Action pendingStart;
        /// <summary>Any key also starts the game from the loading screen.</summary>
        public static void AnyKey() { var p = pendingStart; pendingStart = null; p?.Invoke(); }

        // =====================================================================
        // Per-frame UI animation (cash counter, quest pulse, wiggles)
        // =====================================================================
        public static void Update(float dt)
        {
            if (cashVal == null) return;
            double target = S.cash;
            if (shownCash != target)
            {
                shownCash += (target - shownCash) * (1 - Math.Exp(-17 * dt));
                if (Math.Abs(shownCash - target) < Math.Max(1, target * 0.001)) shownCash = target;
                cashVal.text = U.Fmt(shownCash);
            }
            float t = U.Clock;
            if (questEl != null)
            {
                float s = questEl.ClassListContains("done") ? 1f + Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f)) * 0.04f : 1f;
                questEl.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            foreach (var b in sideBtns.Values)
            {
                var disc = b.Q(className: "disc");
                if (disc == null) continue;
                float ph = t % 1.6f / 1.6f, ang = 0f;
                if (b.ClassListContains("attn") && ph > 0.7f) ang = Mathf.Sin((ph - 0.7f) / 0.3f * Mathf.PI * 3f) * 8f * (1f - (ph - 0.7f) / 0.3f);
                disc.style.rotate = new Rotate(new Angle(ang, AngleUnit.Degree));
            }
            if (plusPulseT > 0f)
            {
                plusPulseT -= dt;
                float s = plusPulseT > 0f ? 1f + Mathf.Abs(Mathf.Sin(plusPulseT * Mathf.PI * 2f)) * 0.35f : 1f;
                cashPlus.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            if (bannerT > 0f) { bannerT -= dt; if (bannerT <= 0f) eventBanner.RemoveFromClassList("on"); }
            UpdatePanels(dt);
        }
    }
}
