#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>
    /// Editor-only playtest bot. It plays like a greedy player who never watches ads: serves the pay booth until a
    /// cashier is hired, keeps the machines stocked, collects cash, builds the cheapest pad it can afford, buys
    /// upgrades with spare cash, claims quests and takes every Road Trip. Milestones and errors go to Temp/autoplay.log.
    /// Start it in Play mode: new GameObject("AutoPlay").AddComponent&lt;AutoPlay&gt;(). Speed = simulation steps per frame.
    /// </summary>
    public class AutoPlay : MonoBehaviour
    {
        public int Speed = 4;
        public int StopAtCity = 99;
        public static float GameTime { get; private set; }
        public static int Built, Errors;
        static string LogPath => Path.Combine(Application.dataPath, "..", "Temp", "autoplay.log");

        List<Vector2> path = new List<Vector2>();
        Vector2 goal, lastPos;
        float thinkT, stuckT, logT;
        bool hooked;

        void Start()
        {
            GameTime = 0f; Built = 0; Errors = 0;
            File.WriteAllText(LogPath, $"autoplay start {DateTime.Now:HH:mm:ss} city={Game.L?.name} cash={Store.S.cash:0}\n");
            Application.logMessageReceived += OnLog;
            if (!hooked)
            {
                hooked = true;
                Bus.On("pad:built", d => { Built++; var def = (PadDef)d; Write($"built {def.id} ({Game.PadName(def)}) level={Store.S.level}"); });
                Bus.On("location", d => Write($"=== arrived in {Game.L.name} (x{Game.L.mult}) cash={Store.S.cash:0}"));
                Bus.On("level", d => Write($"level {d}"));
            }
        }
        void OnDestroy() => Application.logMessageReceived -= OnLog;
        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            Errors++;
            File.AppendAllText(LogPath, $"!! {type}: {msg}\n{stack}\n");
        }
        static void Write(string s) => File.AppendAllText(LogPath, $"[{Fmt(GameTime)}] {s}\n");
        static string Fmt(float t) => $"{(int)(t / 60)}:{(int)(t % 60):00}";

        void Update()
        {
            if (Game.Player == null || !Game.Running || Store.S == null) return;
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            HandlePanels();
            if (Hud.PanelOpen || Game.Paused) return;
            GameTime += dt * Speed;
            // extra simulation steps on top of the one SudsGame already ran this frame
            for (int k = 1; k < Speed; k++) { Sched.Tick(dt); Game.Update(dt); }
            thinkT -= dt;
            if (thinkT <= 0f) { thinkT = 0.2f; Think(); }
            Steer();
            logT -= dt * Speed;
            if (logT <= 0f) { logT = 60f; Write($"cash={Store.S.cash:0} income/s={Game.IncomePerSec():0.0} lv={string.Join(",", Cfg.SlotOrder.Where(t => Game.Lanes.Any(l => l.Get(t) != null)).Select(t => t + Game.Lv(t)))}"); }
        }

        void HandlePanels()
        {
            var name = Hud.CurrentPanel;
            if (name == null) return;
            if (name == "travel")
            {
                Hud.ClosePanel(true);
                int to = Game.LocIdx + 1;
                if (to < StopAtCity) { Write($"road trip to {Cfg.Locations[to].name}"); Game.Travel(to); }
                else { Write("stop city reached"); enabled = false; }
                return;
            }
            Hud.ClosePanel(true);
        }

        void Think()
        {
            var (qp, qn) = Game.QuestProgress();
            if (qp >= qn) Game.ClaimQuest();
            var p = Game.Player;
            var stack = p.Stack;
            var pad = NextPad();
            double cash = Store.S.cash, piles = Game.Lanes.Where(l => l.Pile != null).Sum(l => l.Pile.Total);
            bool canBuild = pad != null && cash + piles >= pad.Remaining;
            // 1. serve the pay booth / polish bay while nobody is hired for it (unless there is enough cash to build: go build)
            foreach (var l in Game.Lanes)
            {
                if (!l.Unlocked || canBuild && piles > 0) continue;
                var pay = l.Get("pay");
                if (pay != null && (pay.Occupant != null || pay.Car != null) && !Game.Workers.Any(w => w.Role == "cashier" && w.Lane == l.Idx)) { Go(pay.OperatorSpot); return; }
                var pol = l.Get("polish");
                if (pol != null && (pol.Occupant != null || pol.Car != null) && !Game.Workers.Any(w => w.Role == "polisher" && w.Lane == l.Idx)) { Go(pol.OperatorSpot); return; }
            }
            // 2. deliver what we carry
            if (stack.Count > 0)
            {
                var h = Game.AllHoppers().Where(x => stack.CountOf(x.Supply) > 0 && x.Space > 0).OrderBy(x => x.Count).FirstOrDefault();
                if (h != null) { Go(h.Mat); return; }
            }
            // 3. an empty machine stops the line: restock it
            var urgent = Game.AllHoppers().Where(x => x.Count + x.Incoming <= 2 && Game.Racks.TryGetValue(x.Supply, out var r) && r.Stock > 0).OrderBy(x => x.Count).FirstOrDefault();
            if (urgent != null && !stack.Full) { Go(Game.Racks[urgent.Supply].Mat); return; }
            // 4. spend: the next pad, or an upgrade with spare cash
            if (pad != null && cash >= pad.Remaining) { Go(new Vector2(pad.Pos.x, pad.Pos.z)); return; }
            BuyUpgrades(pad);
            // 5. collect cash
            var pile = Game.Lanes.Where(l => l.Pile != null && l.Pile.Values.Count > 0).OrderByDescending(l => l.Pile.Total).Select(l => l.Pile).FirstOrDefault();
            if (pile != null) { Go(new Vector2(pile.Pos.x, pile.Pos.z)); return; }
            // 6. top up the machines
            var low = Game.AllHoppers().Where(x => x.Count + x.Incoming < x.Cap - 2 && Game.Racks.TryGetValue(x.Supply, out var r) && r.Stock > 0).OrderBy(x => x.Count).FirstOrDefault();
            if (low != null && !stack.Full) { Go(Game.Racks[low.Supply].Mat); return; }
            Go(new Vector2(-4f, 4f));
        }

        /// <summary>The Road Trip as soon as it shows (a player keen to explore), else the cheapest shown pad.</summary>
        static Pad NextPad()
        {
            var shown = Game.Pads.Where(x => x.Shown).ToList();
            var travel = shown.FirstOrDefault(x => x.Def.kind == "travel");
            if (travel != null && Game.LocIdx + 1 < Cfg.Locations.Count) return travel;
            return shown.Where(x => x.Def.kind != "travel").OrderBy(x => x.Remaining).FirstOrDefault();
        }

        static void BuyUpgrades(Pad next)
        {
            bool saving = next != null && next.Def.kind == "travel";
            double cap = next != null ? next.Remaining * 0.35 : double.MaxValue;
            for (int guard = 0; guard < 10 && !saving; guard++)
            {
                double cash = Store.S.cash;
                string best = null; double bestCost = double.MaxValue; bool biz = false;
                foreach (var t in Cfg.SlotOrder)
                {
                    if (!Game.Lanes.Any(l => l.Get(t) != null) || Game.Lv(t) >= Cfg.StationMaxLv) continue;
                    double c = Game.StationUpCost(t);
                    if (c < bestCost) { bestCost = c; best = t; biz = false; }
                }
                if (Game.Built("office"))
                    foreach (var d in Cfg.Biz)
                    {
                        if (Game.Biz(d.id) >= d.max || d.id == "carry" && Game.Biz("carry") >= 4) continue;
                        double c = Game.BizCost(d.id) * 1.5;   // a little less keen than on machines
                        if (c < bestCost) { bestCost = c; best = d.id; biz = true; }
                    }
                if (best == null) return;
                double real = biz ? Game.BizCost(best) : Game.StationUpCost(best);
                if (real > cash || real > cap) return;
                bool ok = biz ? Game.BuyBiz(best) : Game.BuyStation(best);
                if (!ok) return;
            }
        }

        void Go(Vector3 web) => Go(new Vector2(web.x, web.z));
        void Go(Vector2 target)
        {
            if ((target - goal).sqrMagnitude < 0.01f && path.Count > 0) return;
            goal = target;
            var p = Game.Player.Pos;
            path = Nav.Find(p.x, p.z, target.x, target.y) ?? new List<Vector2> { target };
        }

        void Steer()
        {
            var p = Game.Player.Pos;
            var pos = new Vector2(p.x, p.z);
            while (path.Count > 0 && (path[0] - pos).sqrMagnitude < 0.25f * 0.25f) path.RemoveAt(0);
            if (path.Count == 0) { Game.Move = Vector2.zero; return; }
            var d = path[0] - pos;
            Game.Move = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.zero;
            // unstick: no progress for a while -> plan again from here
            stuckT = (pos - lastPos).sqrMagnitude < 0.0004f ? stuckT + Time.unscaledDeltaTime : 0f;
            lastPos = pos;
            if (stuckT > 1.5f) { stuckT = 0f; path = Nav.Find(p.x, p.z, goal.x, goal.y) ?? new List<Vector2>(); if (path.Count > 0) path[0] += UnityEngine.Random.insideUnitCircle * 0.6f; }
        }
    }
}
#endif
