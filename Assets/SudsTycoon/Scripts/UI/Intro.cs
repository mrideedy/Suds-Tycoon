using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>
    /// The animated title screen shown while the game loads, with its own music (Sfx.PlayIntro): the logo letters drop
    /// in and wobble, soap bubbles rise (tap one to pop it), then a pulsing TAP TO START. Built in code on the Hud's loading layer.
    /// </summary>
    public static class Intro
    {
        class Bubble { public VisualElement el; public float x, y, r, speed, wob, phase, pop; }
        class Drop { public VisualElement el; public float x, y, vx, vy, r, age; }

        static VisualElement layer, logo, start;
        static readonly List<VisualElement> big = new List<VisualElement>(), small = new List<VisualElement>();
        static readonly List<Bubble> bubbles = new List<Bubble>();
        static readonly List<Drop> drops = new List<Drop>();
        static Texture2D skyTex;
        static IVisualElementScheduledItem job;
        static float t, last, readyAt = -1f, leaveAt = -1f;
        static bool placed;

        internal static void Reset()
        {
            job?.Pause(); job = null;
            layer = logo = start = null;
            big.Clear(); small.Clear(); bubbles.Clear(); drops.Clear();
            t = last = 0f; readyAt = leaveAt = -1f; placed = false;
        }

        public static void Build(VisualElement l)
        {
            Reset();
            layer = l;
            layer.Clear();
            skyTex = skyTex ? skyTex : Gradient(U.Hex(0x6fd3ff), U.Hex(0x2468e0));
            var sky = UiKit.El("intro-sky");
            sky.style.backgroundImage = new StyleBackground(skyTex);
            sky.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            layer.Add(sky);

            // soap bubbles: tap to pop
            for (int i = 0; i < 24; i++)
            {
                var b = new Bubble { el = UiKit.El("intro-bubble"), phase = U.Rand(0f, 6.28f) };
                b.el.pickingMode = PickingMode.Position;
                b.el.Add(UiKit.El("intro-bubble-shine"));
                var bb = b;
                b.el.RegisterCallback<PointerDownEvent>(e => { e.StopPropagation(); Pop(bb); });
                layer.Add(b.el);
                Respawn(b, true);
                bubbles.Add(b);
            }

            // logo: one label per letter so every letter can bounce on its own
            logo = UiKit.El("intro-logo");
            var row1 = UiKit.El("intro-row");
            foreach (char ch in "SUDS") { var lb = UiKit.Txt(ch.ToString(), "intro-l1"); lb.style.opacity = 0f; row1.Add(lb); big.Add(lb); }
            var row2 = UiKit.El("intro-row intro-row2");
            foreach (char ch in "TYCOON") { var lb = UiKit.Txt(ch.ToString(), "intro-l2"); lb.style.scale = new Scale(Vector3.zero); row2.Add(lb); small.Add(lb); }
            logo.Add(row1); logo.Add(row2);
            layer.Add(logo);

            start = UiKit.El("intro-start");
            start.Add(UiKit.Txt("TAP TO START"));
            start.style.display = DisplayStyle.None;
            layer.Add(start);

            last = U.Clock;
            job = layer.schedule.Execute(Tick).Every(16);
        }

        /// <summary>Loading is done: show the bouncing TAP TO START.</summary>
        public static void Ready(string text)
        {
            if (start == null) return;
            start.Q<Label>().text = text;
            readyAt = t;
        }

        /// <summary>Exit animation; the Hud fades the layer out and calls Clear when it is hidden.</summary>
        public static void Leave() => leaveAt = t;

        public static void Clear()
        {
            layer?.Clear();
            Reset();
        }

        // =====================================================================
        static void Tick()
        {
            if (layer == null) return;
            float now = U.Clock, dt = Mathf.Clamp(now - last, 0f, 0.05f);
            last = now;
            t += dt;   // a long loading frame pauses the show instead of skipping it
            float W = layer.resolvedStyle.width, H = layer.resolvedStyle.height;
            if (float.IsNaN(W) || W <= 0f || float.IsNaN(H) || H <= 0f) return;
            if (!placed) { placed = true; foreach (var b in bubbles) { b.x = U.Rand(0f, W); b.y = U.Rand(0f, H); } }   // the first frame with a real size
            float leave = leaveAt >= 0f ? t - leaveAt : -1f;

            foreach (var b in bubbles)
            {
                b.y -= b.speed * dt * (leave >= 0f ? 5f : 1f);
                float x = b.x + Mathf.Sin(t * 0.9f + b.phase) * b.wob;
                if (b.pop > 0f)
                {
                    b.pop += dt;
                    b.el.style.scale = new Scale(Vector3.one * (1f + b.pop * 4f));
                    b.el.style.opacity = 1f - b.pop / 0.16f;
                    if (b.pop > 0.16f) Respawn(b, false);
                }
                else if (b.y < -b.r * 2f) Respawn(b, false);
                b.el.style.translate = new Translate(x - b.r, b.y - b.r);
            }

            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.age += dt;
                if (d.age >= 0.35f) { d.el.RemoveFromHierarchy(); drops.RemoveAt(i); continue; }
                float k = d.age / 0.35f, r = d.r * (1f - 0.6f * k);
                d.x += d.vx * dt; d.y += d.vy * dt;
                d.el.style.width = r * 2f; d.el.style.height = r * 2f; Round(d.el, r);
                d.el.style.translate = new Translate(d.x - r, d.y - r);
                d.el.style.opacity = 1f - k * k;
            }

            // logo: SUDS drops in with a bounce, TYCOON pops in, then everything wobbles
            logo.style.top = H * 0.14f;
            for (int i = 0; i < big.Count; i++)
            {
                var lb = big[i];
                float p = Mathf.Clamp01((t - 0.15f - i * 0.11f) / 0.65f);
                float settle = Mathf.Clamp01((t - 0.8f - i * 0.11f) / 0.4f);
                float y = -H * 0.5f * (1f - EaseOutBounce(p)) + Mathf.Sin(t * 2.4f + i * 0.9f) * 6f * settle;
                if (leave >= 0f) y -= leave * leave * 2600f * (1f + i * 0.15f);
                lb.style.opacity = p > 0f ? 1f : 0f;
                lb.style.translate = new Translate(0f, y);
                lb.style.rotate = new Rotate(new Angle(Mathf.Sin(t * 1.7f + i * 1.3f) * 4f * settle, AngleUnit.Degree));
                float squash = Mathf.Sin(t * 3.1f + i) * 0.035f * settle;
                lb.style.scale = new Scale(new Vector3(1f - squash, 1f + squash, 1f));
            }
            for (int i = 0; i < small.Count; i++)
            {
                var lb = small[i];
                float p = Mathf.Clamp01((t - 0.85f - i * 0.07f) / 0.45f);
                float s = EaseOutBack(p);
                float y = Mathf.Sin(t * 3.2f - i * 0.7f) * 4f * Mathf.Clamp01((t - 1.3f) / 0.5f);
                if (leave >= 0f) { y -= leave * leave * 2000f; s *= Mathf.Max(0f, 1f - leave * 2f); }
                lb.style.scale = new Scale(new Vector3(s, s, 1f));
                lb.style.translate = new Translate(0f, y);
            }

            // TAP TO START pops in once loading is done (and the logo has landed), then pulses
            float ready = readyAt >= 0f ? Mathf.Min(t - readyAt, t - 1.4f) : -1f;
            if (ready >= 0f)
            {
                start.style.display = DisplayStyle.Flex;
                float sw = start.resolvedStyle.width;
                if (!float.IsNaN(sw) && sw > 0f) start.style.left = W * 0.5f - sw * 0.5f;
                start.style.top = H * 0.66f;
                float pop = EaseOutBack(Mathf.Clamp01(ready / 0.4f));
                float pulse = 1f + Mathf.Sin(ready * 5.5f) * 0.045f * Mathf.Clamp01((ready - 0.4f) / 0.3f);
                if (leave >= 0f) pulse *= 1f + leave * 0.8f;
                start.style.scale = new Scale(new Vector3(pop * pulse, pop * pulse, 1f));
            }
        }

        // =====================================================================
        static void Respawn(Bubble b, bool anywhere)
        {
            float W = layer != null ? layer.resolvedStyle.width : float.NaN, H = layer != null ? layer.resolvedStyle.height : float.NaN;
            if (float.IsNaN(W) || W <= 0f) W = 1280f;
            if (float.IsNaN(H) || H <= 0f) H = 720f;
            b.r = U.Rand(9f, 36f);
            b.speed = U.Rand(35f, 95f) * (1.3f - b.r / 60f);
            b.wob = U.Rand(8f, 26f);
            b.x = U.Rand(0f, W);
            b.y = anywhere ? U.Rand(0f, H) : H + b.r + U.Rand(0f, 90f);
            b.pop = 0f;
            b.el.style.width = b.r * 2f; b.el.style.height = b.r * 2f; Round(b.el, b.r);
            var shine = b.el[0];
            float sr = b.r * 0.32f;
            shine.style.width = sr * 2f; shine.style.height = sr * 1.4f; Round(shine, sr);
            shine.style.left = b.r * 0.32f; shine.style.top = b.r * 0.28f;
            b.el.style.scale = new Scale(Vector3.one);
            b.el.style.opacity = 1f;
        }

        static void Pop(Bubble b)
        {
            if (b.pop > 0f || leaveAt >= 0f) return;
            b.pop = 0.0001f;
            Sfx.Play("pop");
            float x = b.x + Mathf.Sin(t * 0.9f + b.phase) * b.wob;
            for (int i = 0; i < 7 && drops.Count < 60; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + U.Rand(-0.2f, 0.2f), v = U.Rand(140f, 260f);
                var el = UiKit.El("intro-drop");
                layer.Add(el);
                drops.Add(new Drop { el = el, x = x + Mathf.Cos(a) * b.r, y = b.y + Mathf.Sin(a) * b.r, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, r = U.Rand(2.5f, 4.5f) });
            }
        }

        static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        static Texture2D Gradient(Color top, Color bottom, int h = 128)
        {
            var tx = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++) tx.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
            tx.Apply();
            return tx;
        }

        static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); }
        static float EaseOutBounce(float x)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (x < 1f / d1) return n1 * x * x;
            if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
            if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
            x -= 2.625f / d1; return n1 * x * x + 0.984375f;
        }
    }
}
