using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>A UI element pinned above a point in the world (web coordinates). Port of FX.label.</summary>
    public class WLabel
    {
        internal readonly VisualElement root, body;
        internal Transform anchorTr;
        internal float offY;
        internal Func<Vector3> anchorFn;
        /// <summary>Anchor for labels created at a fixed position (can be moved, e.g. a growing cash pile).</summary>
        public Vector3 FixedPos;
        internal bool visible = true, removed;
        string key;

        internal WLabel()
        {
            root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList("wlabel");
            body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("wlabel-body");
            root.Add(body);
        }

        public bool Visible => visible;
        public bool Empty => body.childCount == 0;
        public void Show(bool on) { visible = on; }
        /// <summary>Rebuild the content only when the key changes (null clears it).</summary>
        public void Set(string k, Action<VisualElement> build)
        {
            if (k == key) return;
            key = k;
            body.Clear();
            if (k != null) build?.Invoke(body);
        }
        public void Clear() => Set(null, null);
        public void Remove()
        {
            if (removed) return;
            removed = true;
            root.RemoveFromHierarchy();
        }

        internal Vector3 WebPos()
        {
            if (anchorTr) return U.W(anchorTr.position) + new Vector3(0f, offY, 0f);
            if (anchorFn != null) return anchorFn() + new Vector3(0f, offY, 0f);
            return FixedPos;
        }
    }

    /// <summary>World labels and floating text drawn on the UI Toolkit overlay, positioned every frame from the camera.</summary>
    public static class WorldUI
    {
        static VisualElement layer;
        static readonly List<WLabel> labels = new List<WLabel>();
        static readonly List<FText> texts = new List<FText>();
        class FText { public VisualElement el; public Vector3 pos; public float t, dur, rise; }

        public static void Init(VisualElement labelLayer) { layer = labelLayer; }
        internal static void ResetStatics() { layer = null; labels.Clear(); texts.Clear(); }

        static WLabel Make()
        {
            var l = new WLabel();
            layer?.Add(l.root);
            labels.Add(l);
            return l;
        }
        public static WLabel Label(Transform anchor, float offY) { var l = Make(); l.anchorTr = anchor; l.offY = offY; return l; }
        public static WLabel Label(Vector3 webPos) { var l = Make(); l.FixedPos = webPos; return l; }
        public static WLabel Label(Func<Vector3> webPos) { var l = Make(); l.anchorFn = webPos; return l; }
        /// <summary>Label on another layer (the mini-game keeps its labels visible while the HUD labels are hidden).</summary>
        public static WLabel LabelIn(VisualElement parent, Func<Vector3> webPos)
        {
            var l = Make(); l.anchorFn = webPos;
            parent.Add(l.root);
            return l;
        }

        /// <summary>Floating text that pops, rises and fades (classes: cash, tip, big, rage, honk, new).</summary>
        public static void Text(Vector3 webPos, string text, string cls = "", float dur = 1.3f, float rise = 70f, string icon = null)
        {
            if (layer == null) return;
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.AddToClassList("ftext");
            UiKit.AddClasses(el, cls);
            if (icon != null) el.Add(UiKit.Icon(icon, "icon"));
            el.Add(UiKit.Txt(text, "ftext-label"));
            layer.Add(el);
            texts.Add(new FText { el = el, pos = webPos, dur = dur, rise = rise });
        }

        // ---------------- element builders used by world objects ----------------
        public static VisualElement El(string cls) => UiKit.El(cls);
        public static VisualElement Icon(string name, string cls = "icon") => UiKit.Icon(name, cls);
        public static Label Txt(string text, string cls = null) => UiKit.Txt(text, cls);
        public static VisualElement Bar(string cls, float value, Color? fill) => UiKit.Bar(cls, value, fill);
        /// <summary>Pill with an icon and a text, e.g. station badges and the hopper counter.</summary>
        public static VisualElement Chip(string cls, string icon, string text)
        {
            var e = UiKit.El(cls);
            if (icon != null) e.Add(UiKit.Icon(icon));
            e.Add(UiKit.Txt(text));
            return e;
        }
        public static VisualElement Clickable(VisualElement e, Action onClick)
        {
            e.pickingMode = PickingMode.Position;
            e.AddManipulator(new Clickable(onClick));
            return e;
        }

        // ---------------- per-frame placement ----------------
        public static void Tick(Camera cam, float dt)
        {
            if (layer == null || cam == null || layer.panel == null) return;
            var panel = layer.panel;
            float time = U.Clock;
            for (int i = labels.Count - 1; i >= 0; i--)
            {
                var l = labels[i];
                if (l.removed) { labels.RemoveAt(i); continue; }
                bool show = l.visible && !l.Empty;
                Vector2 pp = default;
                if (show)
                {
                    var sp = cam.WorldToScreenPoint(U.P(l.WebPos()));
                    if (sp.z <= 0f) show = false;
                    else pp = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, cam.pixelHeight - sp.y));
                }
                var disp = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (l.root.style.display != disp) l.root.style.display = disp;
                if (!show) continue;
                var lr = l.root.layout;
                float lw = float.IsNaN(lr.width) ? 0f : lr.width, lh = float.IsNaN(lr.height) ? 0f : lr.height;
                // centred above the anchor (the web used translate(-50%, -100%))
                l.root.style.translate = new Translate(pp.x - lw * 0.5f, pp.y - lh, 0f);
                // bobbing badges (the web used a CSS keyframe)
                var first = l.body.childCount > 0 ? l.body[0] : null;
                if (first != null && (first.ClassListContains("st-badge") || first.ClassListContains("gift-tag")))
                    first.style.translate = new Translate(0f, -Mathf.Abs(Mathf.Sin(time * Mathf.PI)) * 5f, 0f);
            }
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                var t = texts[i];
                t.t += dt;
                if (t.t >= t.dur) { t.el.RemoveFromHierarchy(); texts.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(U.P(t.pos));
                if (sp.z <= 0f) { t.el.style.display = DisplayStyle.None; continue; }
                t.el.style.display = DisplayStyle.Flex;
                var pp = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, cam.pixelHeight - sp.y));
                float k = t.t / t.dur;
                float y = pp.y - U.EaseOutCubic(Mathf.Min(1f, k * 1.4f)) * t.rise;
                var tr = t.el.layout;
                float tw = float.IsNaN(tr.width) ? 0f : tr.width, th = float.IsNaN(tr.height) ? 0f : tr.height;
                t.el.style.translate = new Translate(pp.x - tw * 0.5f, y - th * 0.5f, 0f);
                float pop = t.t < 0.3f ? Mathf.Lerp(0.3f, 1f, U.EaseOutBack(t.t / 0.3f)) : 1f;
                t.el.style.scale = new Scale(new Vector3(pop, pop, 1f));
                t.el.style.opacity = k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
            }
        }

        /// <summary>Screen (panel) position of a web point, and whether it is in front of the camera.</summary>
        public static bool Project(Camera cam, Vector3 webPos, out Vector2 panelPos)
        {
            panelPos = default;
            if (layer?.panel == null || !cam) return false;
            var sp = cam.WorldToScreenPoint(U.P(webPos));
            panelPos = RuntimePanelUtils.ScreenToPanel(layer.panel, new Vector2(sp.x, cam.pixelHeight - sp.y));
            return sp.z > 0f;
        }

        public static void ClearAll()
        {
            foreach (var l in labels) l.Remove();
            labels.Clear();
            foreach (var t in texts) t.el.RemoveFromHierarchy();
            texts.Clear();
        }
    }
}
