using System;
using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>Small shared helpers: math, easing, number formatting and the web-to-Unity coordinate mapping.</summary>
    public static class U
    {
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Damp(float a, float b, float k, float dt) => a + (b - a) * (1f - Mathf.Exp(-k * dt));
        public static float Rand(float a, float b) => UnityEngine.Random.Range(a, b);
        public static int RandI(int a, int b) => UnityEngine.Random.Range(a, b + 1);
        public static bool Chance(float p) => UnityEngine.Random.value < p;
        public static T Pick<T>(IList<T> list) => list[UnityEngine.Random.Range(0, list.Count)];

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        public static float AngleLerp(float a, float b, float t)
        {
            float d = (b - a) % (Mathf.PI * 2f);
            if (d > Mathf.PI) d -= Mathf.PI * 2f; else if (d < -Mathf.PI) d += Mathf.PI * 2f;
            return a + d * t;
        }
        public static float Dist2(float ax, float az, float bx, float bz) { float dx = ax - bx, dz = az - bz; return dx * dx + dz * dz; }
        public static float Dist(float ax, float az, float bx, float bz) => Mathf.Sqrt(Dist2(ax, az, bx, bz));

        public static T Weighted<T>(IList<T> items, Func<T, float> weight)
        {
            float total = 0f;
            foreach (var it in items) total += Mathf.Max(0f, weight(it));
            float r = UnityEngine.Random.value * total;
            foreach (var it in items) { r -= Mathf.Max(0f, weight(it)); if (r <= 0f) return it; }
            return items[items.Count - 1];
        }

        static readonly string[] Units = { "K", "M", "B", "T", "aa", "ab", "ac", "ad", "ae", "af" };
        /// <summary>2.4K style numbers, like the reference HUD.</summary>
        public static string Fmt(double n)
        {
            if (double.IsNaN(n) || double.IsInfinity(n)) return "0";
            bool neg = n < 0; n = Math.Abs(n);
            string s;
            if (n < 1000) s = Math.Floor(n).ToString("0");
            else
            {
                int u = -1;
                while (n >= 1000 && u < Units.Length - 1) { n /= 1000; u++; }
                int d = n < 10 ? 2 : n < 100 ? 1 : 0;
                double p = Math.Pow(10, d);
                double v = Math.Floor(n * p + 1e-6) / p;
                s = v.ToString(d == 2 ? "0.##" : d == 1 ? "0.#" : "0", System.Globalization.CultureInfo.InvariantCulture) + Units[u];
            }
            return (neg ? "-" : "") + s;
        }
        /// <summary>Rounds a price to two significant digits (1,234 -> 1,200), so every cost reads as a clean number.</summary>
        public static double Nice(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 100) return Math.Max(0, Math.Round(v));
            double p = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
            return Math.Round(v / p) * p;
        }
        public static string FmtTime(double sec)
        {
            long t = (long)Math.Ceiling(Math.Max(0, sec));
            long h = t / 3600, m = (t % 3600) / 60, s = t % 60;
            return h > 0 ? $"{h}h {m}m" : $"{m}:{s:00}";
        }
        public static string Today() => DateTime.Now.ToString("yyyy-MM-dd");
        public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        /// <summary>Frame step: the fixed capture step while a frame rate is being recorded (the editor's trailer shoot), else real time.</summary>
        public static float FrameDt => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        /// <summary>Clock for timers and UI animation that follows FrameDt.</summary>
        public static float Clock => Time.captureDeltaTime > 0f ? Time.time : Time.unscaledTime;

        // ---- coordinates: gameplay runs in the web version's space; glTFast mirrors X on import ----
        public static Vector3 P(float x, float y, float z) => new Vector3(-x, y, z);
        public static Vector3 P(Vector3 web) => new Vector3(-web.x, web.y, web.z);
        public static Vector3 W(Vector3 unity) => new Vector3(-unity.x, unity.y, unity.z);
        /// <summary>Web yaw (three.js rotation.y, radians) to a Unity rotation.</summary>
        public static Quaternion Yaw(float rad) => Quaternion.Euler(0f, -rad * Mathf.Rad2Deg, 0f);

        public static Color Hex(uint hex, float a = 1f) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a);
        public static Color Css(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static void Vibrate(int ms = 15)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Store.S != null && Store.S.settings.vib) Handheld.Vibrate();
#endif
        }

        /// <summary>Depth-first search for a child transform by exact name.</summary>
        public static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = Find(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }

    /// <summary>Minimal event hub so systems can react to each other without hard links.</summary>
    public static class Bus
    {
        static readonly Dictionary<string, List<Action<object>>> handlers = new Dictionary<string, List<Action<object>>>();
        public static void On(string ev, Action<object> fn)
        {
            if (!handlers.TryGetValue(ev, out var l)) handlers[ev] = l = new List<Action<object>>();
            l.Add(fn);
        }
        public static void Off(string ev, Action<object> fn) { if (handlers.TryGetValue(ev, out var l)) l.Remove(fn); }
        public static void Emit(string ev, object data = null)
        {
            if (!handlers.TryGetValue(ev, out var l)) return;
            foreach (var fn in l.ToArray())
            {
                try { fn(data); } catch (Exception e) { Debug.LogException(e); }
            }
        }
        public static void Clear() => handlers.Clear();
    }

    /// <summary>Game-time delayed calls (the web build used setTimeout). Ticked by SudsGame; callbacks must tolerate destroyed objects.</summary>
    public static class Sched
    {
        struct Job { public float t; public Action fn; }
        static readonly List<Job> jobs = new List<Job>();
        static float now;
        public static void After(float secs, Action fn) => jobs.Add(new Job { t = now + secs, fn = fn });
        public static void Tick(float dt)
        {
            now += dt;
            for (int i = 0; i < jobs.Count; i++)
            {
                if (jobs[i].t > now) continue;
                var fn = jobs[i].fn;
                jobs.RemoveAt(i); i--;
                try { fn(); } catch (Exception e) { Debug.LogException(e); }
            }
        }
        public static void Clear() => jobs.Clear();
    }
}
