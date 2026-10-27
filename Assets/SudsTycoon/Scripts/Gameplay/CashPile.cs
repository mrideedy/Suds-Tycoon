using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SudsTycoon
{
    /// <summary>Stack of cash bundles waiting to be collected (one per pay booth, plus the vending tray).</summary>
    public class CashPile
    {
        const int Max = 64;
        public readonly Vector3 Pos;
        public readonly List<double> Values = new List<double>();
        readonly Transform group;
        readonly float baseY;
        readonly List<Transform> bundles = new List<Transform>();
        readonly float[] rot = new float[Max];
        readonly WLabel lbl;
        float collectT, comboT;
        int combo;
        bool destroyed;
        string lblKey;

        public CashPile(float x, float z, Transform parent, bool noTable = false, float baseY = 0.39f)
        {
            Pos = new Vector3(x, 0f, z);
            this.baseY = baseY;
            group = new GameObject("CashPile").transform;
            group.SetParent(parent, false);
            group.position = U.P(Pos);
            if (!noTable) Art.Spawn("Machines/cash_table", group);
            for (int i = 0; i < Max; i++) rot[i] = U.Rand(-0.15f, 0.15f);
            lbl = WorldUI.Label(new Vector3(x, 1.2f, z));
        }

        public double Total { get { double t = 0; foreach (var v in Values) t += v; return t; } }

        Vector3 Slot(int i)
        {
            int layer = i / 4, j = i % 4;
            return new Vector3(j % 2 == 1 ? 0.31f : -0.31f, baseY + layer * 0.135f, j < 2 ? -0.19f : 0.19f);
        }

        void Redraw()
        {
            int n = Mathf.Min(Max, Values.Count);
            while (bundles.Count < n)
            {
                var b = Art.Spawn("Items/cash_bundle", group).transform;
                bundles.Add(b);
            }
            for (int i = 0; i < bundles.Count; i++)
            {
                bool on = i < n;
                if (bundles[i].gameObject.activeSelf != on) bundles[i].gameObject.SetActive(on);
                if (!on) continue;
                bundles[i].localPosition = U.P(Slot(i));
                bundles[i].localRotation = Quaternion.Euler(0f, -rot[i] * Mathf.Rad2Deg, 0f);
            }
            double tot = Total;
            lbl.FixedPos = new Vector3(Pos.x, 1.0f + n / 4f * 0.135f + 0.5f, Pos.z);
            string key = tot > 0 ? U.Fmt(tot) : null;
            if (key == lblKey) return;
            lblKey = key;
            if (key == null) lbl.Clear();
            else lbl.Set(key, e => e.Add(WorldUI.Chip("pile-lbl", "cash", key)));
        }

        /// <summary>Cash flies in from a car (or a vending machine) and lands on the pile in a few bundles.</summary>
        public void Add(double amount, Vector3 fromWeb, int nBundles = 0)
        {
            int n = nBundles > 0 ? nBundles : Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(1f + (float)(amount / Math.Max(1.0, Game.BasePrice())), 2f)) + 1, 2, 5);
            double each = amount / n;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                Sched.After(k * 0.07f, () =>
                {
                    if (destroyed) return;
                    var b = Fx.GetBundle();
                    var dest = Pos + Slot(Mathf.Min(Max - 1, Values.Count + k));
                    var from = fromWeb + new Vector3(U.Rand(-0.3f, 0.3f), 0f, U.Rand(-0.3f, 0.3f));
                    Fx.Fly(b, from, () => dest, 0.35f, 1.6f, t =>
                    {
                        Fx.FreeBundle(t);
                        if (destroyed) return;
                        if (Values.Count >= Max) Values[U.RandI(0, Max - 1)] += each; else Values.Add(each);
                        Redraw(); Sfx.Play("tick");
                    }, 4f);
                });
            }
        }

        public void Restore(double total)
        {
            if (total <= 0) return;
            int n = Mathf.Clamp(Mathf.RoundToInt((float)(total / Math.Max(1.0, Game.BasePrice() * 2))), 1, 24);
            for (int i = 0; i < n; i++) Values.Add(total / n);
            Redraw();
        }

        public void Update(float dt)
        {
            comboT -= dt; if (comboT <= 0f) combo = 0;
            if (Values.Count == 0) return;
            var p = Game.Player;
            if (!p.Near(Pos.x, Pos.z, Game.Magnet())) return;
            collectT -= dt;
            while (collectT <= 0f && Values.Count > 0)
            {
                collectT += 0.035f;
                int i = Values.Count - 1;
                double v = Values[i];
                Values.RemoveAt(i);
                var from = Pos + Slot(Mathf.Min(Max - 1, i));
                var b = Fx.GetBundle();
                int c = combo++; comboT = 0.6f;
                Fx.Fly(b, from, () => Game.Player.Pos + new Vector3(0f, 1.3f, 0f), 0.22f, 0.9f, t =>
                {
                    Fx.FreeBundle(t);
                    Game.AddCash(v, true);
                    Sfx.Play("coin", c * 0.5f);
                }, 0f, 0.4f);
                if (c == 0) Game.Cnt("collect");
                Redraw();
                if (Values.Count == 0) { Bus.Emit("cash:collected", this); U.Vibrate(10); }
            }
        }

        public void Destroy()
        {
            destroyed = true;
            if (group) Object.Destroy(group.gameObject);
            lbl.Remove();
        }
    }
}
