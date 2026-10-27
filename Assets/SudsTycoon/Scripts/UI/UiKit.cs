using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>Small helpers to build UI Toolkit trees in code (the HUD is built like the web DOM was).</summary>
    public static class UiKit
    {
        public static VisualElement El(string cls = null, params VisualElement[] kids)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            AddClasses(e, cls);
            foreach (var k in kids) if (k != null) e.Add(k);
            return e;
        }

        public static void AddClasses(VisualElement e, string cls)
        {
            if (string.IsNullOrEmpty(cls)) return;
            foreach (var c in cls.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
            Fonts.Apply(e, cls);
        }

        public static VisualElement Icon(string name, string cls = "icon")
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            AddClasses(e, cls);
            var tex = Art.Icon(name);
            if (tex) e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }
        public static void SetIcon(VisualElement e, string name)
        {
            var tex = Art.Icon(name);
            e.style.backgroundImage = tex ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
        }

        public static Label Txt(string text, string cls = null)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
            AddClasses(l, cls);
            return l;
        }

        /// <summary>A tappable element (not a UI Toolkit Button, so it carries no theme styling).</summary>
        public static VisualElement Btn(string cls, Action onClick, params VisualElement[] kids)
        {
            var e = new VisualElement { pickingMode = PickingMode.Position };
            AddClasses(e, cls);
            foreach (var k in kids) if (k != null) e.Add(k);
            if (onClick != null) e.AddManipulator(new Clickable(() => { if (!e.ClassListContains("disabled")) onClick(); }));
            return e;
        }
        /// <summary>Button with an optional icon and a text label.</summary>
        public static VisualElement Button(string text, string cls, Action onClick, string icon = null)
        {
            var b = Btn("btn " + (cls ?? ""), onClick);
            if (b.ClassListContains("ad") && !Ads.Free) b.Add(Txt("AD", "ad-tag"));
            if (icon != null) b.Add(Icon(icon));
            if (!string.IsNullOrEmpty(text)) b.Add(Txt(text, "btn-text"));
            return b;
        }

        /// <summary>Panel list row: icon tile, title + sub + optional progress bar, and a trailing button.</summary>
        public static VisualElement Row(string icon, string title, string sub = null, float? bar = null, VisualElement btn = null, string cls = null)
        {
            var row = El("row " + (cls ?? ""));
            var ico = El("r-ico");
            ico.Add(Icon(icon));
            row.Add(ico);
            var main = El("r-main");
            main.Add(Txt(title, "r-title"));
            if (!string.IsNullOrEmpty(sub)) main.Add(Txt(sub, "r-sub"));
            if (bar.HasValue) main.Add(Bar("r-bar", bar.Value, null));
            row.Add(main);
            if (btn != null) { var end = El("r-end"); end.Add(btn); row.Add(end); }
            return row;
        }

        /// <summary>Horizontal progress bar with a fill child (class "fill").</summary>
        public static VisualElement Bar(string cls, float value, Color? fill)
        {
            var bar = El(cls);
            var f = El("fill");
            f.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
            if (fill.HasValue) f.style.backgroundColor = fill.Value;
            bar.Add(f);
            return bar;
        }
        public static void SetBar(VisualElement bar, float value)
        {
            var f = bar.Q(className: "fill");
            if (f != null) f.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Circular progress ring drawn with the vector API (port of the SVG ring above busy machines).</summary>
    public class RingProgress : VisualElement
    {
        float value;
        public float Value
        {
            get => value;
            set { if (Mathf.Abs(this.value - value) < 0.004f) return; this.value = value; MarkDirtyRepaint(); }
        }
        public RingProgress()
        {
            AddToClassList("ring-prog");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }
        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float size = Mathf.Min(r.width, r.height);
            if (size <= 1f) return;
            var c = r.center;
            float rad = size * 15f / 40f, w = size * 5f / 40f;
            var p = ctx.painter2D;
            p.fillColor = new Color(12 / 255f, 24 / 255f, 44 / 255f, 0.55f);
            p.BeginPath(); p.Arc(c, rad, 0f, 360f); p.Fill();
            p.lineWidth = w;
            p.strokeColor = new Color(1f, 1f, 1f, 0.35f);
            p.BeginPath(); p.Arc(c, rad, 0f, 360f); p.Stroke();
            if (value > 0.001f)
            {
                p.strokeColor = new Color(0.275f, 0.761f, 0.227f);
                p.lineCap = LineCap.Round;
                p.BeginPath(); p.Arc(c, rad, -90f, -90f + 360f * Mathf.Clamp01(value)); p.Stroke();
            }
        }
    }
}
