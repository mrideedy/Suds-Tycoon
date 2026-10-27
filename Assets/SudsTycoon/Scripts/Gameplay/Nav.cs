using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>Axis-aligned blocker in web coordinates.</summary>
    public struct Box2
    {
        public float x0, x1, z0, z1;
        public Box2(float x0, float x1, float z0, float z1) { this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; }
    }

    /// <summary>Grid A* over the lot so staff walk around machines, racks and buildings (port of nav.js).</summary>
    public static class Nav
    {
        const float Cs = 0.5f;
        static float x0, z0;
        static int w = 1, h = 1;
        static bool[] grid = new bool[1];
        static readonly List<Box2> colliders = new List<Box2>();
        public static IReadOnlyList<Box2> Colliders => colliders;

        public static void Init(Rect b)
        {
            x0 = b.xMin; z0 = b.yMin;
            w = Mathf.CeilToInt(b.width / Cs); h = Mathf.CeilToInt(b.height / Cs);
            grid = new bool[w * h];
        }
        public static void SetColliders(List<Box2> list) { colliders.Clear(); colliders.AddRange(list); Rebuild(); }
        static void Rebuild(float pad = 0.38f)
        {
            System.Array.Clear(grid, 0, grid.Length);
            foreach (var c in colliders)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((c.x0 - pad - x0) / Cs)), i1 = Mathf.Min(w - 1, Mathf.FloorToInt((c.x1 + pad - x0) / Cs));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((c.z0 - pad - z0) / Cs)), j1 = Mathf.Min(h - 1, Mathf.FloorToInt((c.z1 + pad - z0) / Cs));
                for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) grid[j * w + i] = true;
            }
        }
        static int Ci(float x) => Mathf.FloorToInt((x - x0) / Cs);
        static int Cj(float z) => Mathf.FloorToInt((z - z0) / Cs);
        static bool Blocked(int i, int j) => i < 0 || j < 0 || i >= w || j >= h || grid[j * w + i];
        static float Cx(int i) => x0 + (i + 0.5f) * Cs;
        static float Cz(int j) => z0 + (j + 0.5f) * Cs;
        public static bool BlockedAt(float x, float z) => Blocked(Ci(x), Cj(z));

        static Vector2Int NearestFree(int i, int j)
        {
            if (!Blocked(i, j)) return new Vector2Int(i, j);
            for (int r = 1; r < 30; r++)
                for (int dj = -r; dj <= r; dj++) for (int di = -r; di <= r; di++)
                {
                    if (Mathf.Abs(di) != r && Mathf.Abs(dj) != r) continue;
                    if (!Blocked(i + di, j + dj)) return new Vector2Int(i + di, j + dj);
                }
            return new Vector2Int(i, j);
        }
        public static bool Los(float ax, float az, float bx, float bz)
        {
            float d = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            int n = Mathf.CeilToInt(d / (Cs * 0.5f));
            for (int k = 1; k < n; k++)
            {
                float t = (float)k / n;
                if (Blocked(Ci(ax + (bx - ax) * t), Cj(az + (bz - az) * t))) return false;
            }
            return true;
        }

        /// <summary>Path from a to b as a list of waypoints (x, z), smoothed by string pulling.</summary>
        public static List<Vector2> Find(float ax, float az, float bx, float bz)
        {
            var s0 = NearestFree(Ci(ax), Cj(az));
            var t0 = NearestFree(Ci(bx), Cj(bz));
            if (s0 == t0) return new List<Vector2> { new Vector2(bx, bz) };
            if (Los(ax, az, bx, bz) && !Blocked(Ci(bx), Cj(bz))) return new List<Vector2> { new Vector2(bx, bz) };
            int N = w * h;
            var g = new float[N]; var came = new int[N]; var closed = new bool[N];
            for (int i = 0; i < N; i++) { g[i] = float.PositiveInfinity; came[i] = -1; }
            var heap = new List<int>(); var hf = new List<float>();
            void Push(int node, float f)
            {
                heap.Add(node); hf.Add(f); int k = heap.Count - 1;
                while (k > 0) { int p = (k - 1) >> 1; if (hf[p] <= hf[k]) break; (heap[p], heap[k]) = (heap[k], heap[p]); (hf[p], hf[k]) = (hf[k], hf[p]); k = p; }
            }
            int Pop()
            {
                int top = heap[0], last = heap[heap.Count - 1]; float lf = hf[hf.Count - 1];
                heap.RemoveAt(heap.Count - 1); hf.RemoveAt(hf.Count - 1);
                if (heap.Count > 0)
                {
                    heap[0] = last; hf[0] = lf; int k = 0;
                    while (true)
                    {
                        int l = 2 * k + 1, r = l + 1, m = k;
                        if (l < heap.Count && hf[l] < hf[m]) m = l;
                        if (r < heap.Count && hf[r] < hf[m]) m = r;
                        if (m == k) break;
                        (heap[m], heap[k]) = (heap[k], heap[m]); (hf[m], hf[k]) = (hf[k], hf[m]); k = m;
                    }
                }
                return top;
            }
            float H(int i, int j) { float dx = Mathf.Abs(i - t0.x), dz = Mathf.Abs(j - t0.y); return dx + dz + (1.41421356f - 2f) * Mathf.Min(dx, dz); }
            int s = s0.y * w + s0.x, t = t0.y * w + t0.x;
            g[s] = 0; Push(s, H(s0.x, s0.y));
            bool found = false; int iter = 0;
            while (heap.Count > 0 && iter++ < 20000)
            {
                int cur = Pop();
                if (cur == t) { found = true; break; }
                if (closed[cur]) continue;
                closed[cur] = true;
                int ci = cur % w, cj = cur / w;
                for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int ni = ci + di, nj = cj + dj;
                    if (Blocked(ni, nj)) continue;
                    if (di != 0 && dj != 0 && (Blocked(ci + di, cj) || Blocked(ci, cj + dj))) continue;
                    int nn = nj * w + ni;
                    if (closed[nn]) continue;
                    float ng = g[cur] + (di != 0 && dj != 0 ? 1.41421356f : 1f);
                    if (ng < g[nn]) { g[nn] = ng; came[nn] = cur; Push(nn, ng + H(ni, nj)); }
                }
            }
            if (!found) return new List<Vector2> { new Vector2(bx, bz) };
            var cells = new List<int>();
            for (int c = t; c != -1 && c != s; c = came[c]) cells.Add(c);
            cells.Reverse();
            var pts = new List<Vector2>();
            foreach (var c in cells) pts.Add(new Vector2(Cx(c % w), Cz(c / w)));
            pts[pts.Count - 1] = new Vector2(bx, bz);
            var outp = new List<Vector2>();
            float px = ax, pz = az; int kk = 0;
            while (kk < pts.Count)
            {
                int far = kk;
                for (int m = pts.Count - 1; m > kk; m--) if (Los(px, pz, pts[m].x, pts[m].y)) { far = m; break; }
                outp.Add(pts[far]); px = pts[far].x; pz = pts[far].y; kk = far + 1;
            }
            return outp;
        }

        /// <summary>Push a circle (web x/z) out of every collider.</summary>
        public static void Resolve(ref Vector3 p, float r)
        {
            foreach (var c in colliders)
            {
                float nx = U.Clamp(p.x, c.x0, c.x1), nz = U.Clamp(p.z, c.z0, c.z1);
                float dx = p.x - nx, dz = p.z - nz, d2 = dx * dx + dz * dz;
                if (d2 >= r * r) continue;
                if (d2 > 1e-6f) { float d = Mathf.Sqrt(d2), k = (r - d) / d; p.x += dx * k; p.z += dz * k; }
                else
                {
                    float l = p.x - c.x0, rr = c.x1 - p.x, t = p.z - c.z0, b = c.z1 - p.z, m = Mathf.Min(Mathf.Min(l, rr), Mathf.Min(t, b));
                    if (m == l) p.x = c.x0 - r; else if (m == rr) p.x = c.x1 + r; else if (m == t) p.z = c.z0 - r; else p.z = c.z1 + r;
                }
            }
        }
    }
}
