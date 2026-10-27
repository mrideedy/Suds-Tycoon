using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SudsTycoon
{
    /// <summary>
    /// A build pad: stand on it and cash drains into it until the thing is built (port of Pad in stations.js).
    /// The dashed frame and green fill lie on the ground; the icon and price float above it as a world label.
    /// </summary>
    public class Pad
    {
        const float Size = 2.8f, Inner = Size * 236f / 256f;
        public readonly PadDef Def;
        public readonly string Id;
        public readonly double Cost;
        public double Paid;
        public readonly Vector3 Pos;
        public readonly string Icon;
        public bool Shown { get; private set; }

        readonly Transform root, fill;
        readonly WLabel priceLbl;
        bool inside;
        public string Name { get; }
        float tick, payT, hintT;
        int coinN;
        Label priceText;

        static Material fillMat;

        public Pad(PadDef def, double mult, double paid, Transform parent)
        {
            Def = def; Id = def.id;
            Cost = Math.Round(def.cost * mult);   // round pad prices x the city price (1, 2, 5, 10 ...) stay round
            Paid = Math.Min(paid, Cost - 1);
            var p = Game.PadPos(def);
            Pos = new Vector3(p.x, 0f, p.y);
            Icon = Game.PadIcon(def);
            root = new GameObject("Pad_" + Id).transform;
            root.SetParent(parent, false);
            root.position = U.P(Pos.x, 0.07f, Pos.z);
            if (!fillMat) { fillMat = Art.Decal(Texture2D.whiteTexture, new Color(0.298f, 0.757f, 0.227f, 0.75f)); fillMat.renderQueue = 3000; Art.DecalMat("pad_frame").renderQueue = 3001; }
            fill = Art.GroundDecal("Fill", fillMat, 1f, root, 0f, -0.005f, 0f);
            Art.GroundDecal("Frame", Art.DecalMat("pad_frame"), Size, root, 0f, 0f, 0f);
            root.gameObject.SetActive(false);
            Name = Game.PadName(def);
            // one label (icon, name, price) centred on the pad, so the name can never cover the price
            priceLbl = WorldUI.Label(new Vector3(Pos.x, 0.1f, Pos.z));
            priceLbl.Set("pad", e =>
            {
                var box = WorldUI.El("pad-lbl");
                box.Add(WorldUI.Icon(Icon, "pad-icon"));
                box.Add(WorldUI.Txt(Name, "pad-name"));
                var row = WorldUI.El("pad-price");
                row.Add(WorldUI.Icon("cash", "icon"));
                priceText = WorldUI.Txt("", "pad-cost");
                row.Add(priceText);
                box.Add(row);
                e.Add(box);
            });
            priceLbl.Show(false);
            Draw();
        }

        public double Remaining => Math.Max(0, Cost - Paid);

        void Draw()
        {
            float prog = Cost > 0 ? (float)(1 - Remaining / Cost) : 0f;
            fill.gameObject.SetActive(prog > 0.001f);
            float h = Inner * prog;
            fill.localScale = new Vector3(Inner, Mathf.Max(0.001f, h), 1f);
            // grow from the camera-facing edge (web +z)
            fill.localPosition = U.P(0f, -0.005f, Inner / 2f - h / 2f);
            if (priceText != null) priceText.text = U.Fmt(Math.Ceiling(Remaining));
        }

        public void Show(bool anim = true)
        {
            if (Shown) return;
            Shown = true;
            root.gameObject.SetActive(true);
            priceLbl.Show(true);
            if (anim) { Fx.PopIn(root, 0.3f); Fx.Sparkles(new Vector3(Pos.x, 0.5f, Pos.z), 6, 1f); }
        }

        public void Update(float dt)
        {
            if (!Shown) return;
            tick += dt;
            float pulse = 1f + Mathf.Sin(tick * 4f) * 0.025f;
            if (root.localScale.x > 0.9f) root.localScale = Vector3.one * pulse;
            var p = Game.Player;
            bool now = Mathf.Abs(p.Pos.x - Pos.x) < 1.4f && Mathf.Abs(p.Pos.z - Pos.z) < 1.4f;
            if (now != inside) { inside = now; if (!now) Hud.HidePadPrompt(this); }
            if (!inside) { coinN = 0; hintT = 0f; return; }
            var S = Store.S;
            if (S.cash < 1)
            {
                hintT += dt;
                if (hintT > 0.5f && hintT - dt <= 0.5f) Bus.Emit("pad:broke", this);
                return;
            }
            // drain cash into the pad: always ~1.3 s for a full pad
            double rate = Math.Max(Cost / 1.3, 12);
            double amt = Math.Min(Math.Min(Remaining, S.cash), rate * dt);
            if (amt <= 0) return;
            Game.Spend(amt, true);
            Paid += amt;
            Game.Ls.paid[Id] = Paid;
            payT -= dt;
            if (payT <= 0f)
            {
                payT = 0.07f;
                var b = Fx.GetBundle();
                var dest = new Vector3(Pos.x, 0.2f, Pos.z);
                Fx.Fly(b, p.Pos + new Vector3(0f, 1.3f, 0f), () => dest, 0.25f, 1.2f, Fx.FreeBundle, 0f, 0.5f);
                Sfx.Play("coin", Mathf.Min(20f, coinN++ * 0.6f));
                Draw();
            }
            if (Remaining <= 0.5) { Paid = Cost; Draw(); Game.CompletePad(this); }
        }

        /// <summary>Rewarded-ad shortcut: pay the rest of this pad.</summary>
        public void FinishWithAd()
        {
            if (!Shown) return;
            Paid = Cost; Draw();
            Game.CompletePad(this);
        }

        public void Hide()
        {
            if (root) Object.Destroy(root.gameObject);
            priceLbl.Remove();
            Hud.HidePadPrompt(this);
            Shown = false;
        }
    }
}
