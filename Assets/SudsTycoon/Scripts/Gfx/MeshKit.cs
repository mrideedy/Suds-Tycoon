using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SudsTycoon
{
    /// <summary>
    /// Builds one merged, vertex-coloured mesh from simple parts (boxes, flat quads, discs, cylinders).
    /// Positions are given in web coordinates and converted with U.P; faces are wound for Unity automatically.
    /// </summary>
    public class MeshKit
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();
        public int VertexCount => v.Count;

        void Tri(Vector3 a, Vector3 b, Vector3 d, Color col, Vector3 outward, Vector2 ua, Vector2 ub, Vector2 ud)
        {
            var normal = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(normal, outward) < 0f) { (b, d) = (d, b); (ub, ud) = (ud, ub); normal = -normal; }
            normal.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal);
            // the project renders in linear space and vertex colours are not converted, so store them linear
            col = col.linear;
            c.Add(col); c.Add(col); c.Add(col);
            uv.Add(ua); uv.Add(ub); uv.Add(ud);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
        void QuadU(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color col, Vector3 outward)
        {
            Tri(p0, p1, p2, col, outward, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
            Tri(p0, p2, p3, col, outward, new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0));
        }

        /// <summary>Axis-aligned box (web coordinates), optional yaw in radians (web convention).</summary>
        public MeshKit Box(float w, float h, float d, Color col, float x, float y, float z, float ry = 0f)
        {
            var center = U.P(x, y, z);
            var rot = U.Yaw(ry);
            float hx = w / 2, hy = h / 2, hz = d / 2;
            Vector3 P(float a, float b, float e) => center + rot * new Vector3(a, b, e);
            // +Y, -Y, +X, -X, +Z, -Z
            QuadU(P(-hx, hy, -hz), P(-hx, hy, hz), P(hx, hy, hz), P(hx, hy, -hz), col, rot * Vector3.up);
            QuadU(P(-hx, -hy, -hz), P(hx, -hy, -hz), P(hx, -hy, hz), P(-hx, -hy, hz), col, rot * Vector3.down);
            QuadU(P(hx, -hy, -hz), P(hx, hy, -hz), P(hx, hy, hz), P(hx, -hy, hz), col, rot * Vector3.right);
            QuadU(P(-hx, -hy, -hz), P(-hx, -hy, hz), P(-hx, hy, hz), P(-hx, hy, -hz), col, rot * Vector3.left);
            QuadU(P(-hx, -hy, hz), P(hx, -hy, hz), P(hx, hy, hz), P(-hx, hy, hz), col, rot * Vector3.forward);
            QuadU(P(-hx, -hy, -hz), P(-hx, hy, -hz), P(hx, hy, -hz), P(hx, -hy, -hz), col, rot * Vector3.back);
            return this;
        }
        /// <summary>Flat horizontal rectangle facing up (web coordinates).</summary>
        public MeshKit Flat(float w, float d, Color col, float x, float y, float z, float ry = 0f)
        {
            var center = U.P(x, y, z);
            var rot = U.Yaw(ry);
            float hx = w / 2, hz = d / 2;
            QuadU(center + rot * new Vector3(-hx, 0, -hz), center + rot * new Vector3(-hx, 0, hz), center + rot * new Vector3(hx, 0, hz), center + rot * new Vector3(hx, 0, -hz), col, Vector3.up);
            return this;
        }
        /// <summary>Upward-facing triangle from three web-coordinate points.</summary>
        public MeshKit Triangle(Vector3 a, Vector3 b, Vector3 d, Color col)
        {
            Tri(U.P(a), U.P(b), U.P(d), col, Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero);
            return this;
        }
        public MeshKit Disc(float r, int seg, Color col, float x, float y, float z, float sx = 1f, float sz = 1f)
        {
            var center = U.P(x, y, z);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Tri(center, center + new Vector3(Mathf.Cos(a0) * r * sx, 0, Mathf.Sin(a0) * r * sz), center + new Vector3(Mathf.Cos(a1) * r * sx, 0, Mathf.Sin(a1) * r * sz), col, Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero);
            }
            return this;
        }
        /// <summary>Upright cylinder / cone (rt = 0 gives a cone pointing up, rb = 0 one pointing down). phase rotates the segments.</summary>
        public MeshKit Cylinder(float rt, float rb, float h, int seg, Color col, float x, float y, float z, float phase = 0f)
        {
            var center = U.P(x, y, z);
            float y0 = -h / 2, y1 = h / 2;
            for (int i = 0; i < seg; i++)
            {
                float a0 = phase + i * Mathf.PI * 2 / seg, a1 = phase + (i + 1) * Mathf.PI * 2 / seg, am = (a0 + a1) / 2;
                Vector3 b0 = center + new Vector3(Mathf.Cos(a0) * rb, y0, Mathf.Sin(a0) * rb), b1 = center + new Vector3(Mathf.Cos(a1) * rb, y0, Mathf.Sin(a1) * rb);
                Vector3 t0 = center + new Vector3(Mathf.Cos(a0) * rt, y1, Mathf.Sin(a0) * rt), t1 = center + new Vector3(Mathf.Cos(a1) * rt, y1, Mathf.Sin(a1) * rt);
                var outward = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am));
                QuadU(b0, t0, t1, b1, col, outward);
                Tri(center + new Vector3(0, y1, 0), t0, t1, col, Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero);
                Tri(center + new Vector3(0, y0, 0), b1, b0, col, Vector3.down, Vector2.zero, Vector2.zero, Vector2.zero);
            }
            return this;
        }

        /// <summary>Faceted sphere (icosphere) with optional per-axis scale (web coordinates).</summary>
        public MeshKit Ico(float r, Color col, float x, float y, float z, float sx = 1f, float sy = 1f, float sz = 1f)
        {
            var m = Art.Ico; var vs = m.vertices; var ts = m.triangles;
            var center = U.P(x, y, z); var sc = new Vector3(sx, sy, sz) * r;
            for (int i = 0; i < ts.Length; i += 3)
            {
                Vector3 a = center + Vector3.Scale(vs[ts[i]], sc), b = center + Vector3.Scale(vs[ts[i + 1]], sc), d = center + Vector3.Scale(vs[ts[i + 2]], sc);
                Tri(a, b, d, col, (a + b + d) / 3f - center, Vector2.zero, Vector2.zero, Vector2.zero);
            }
            return this;
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
        /// <summary>Build into a new GameObject with the shared vertex-colour material.</summary>
        public GameObject BuildObject(string name, Transform parent, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Build(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Art.VertexColor();
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }
    }
}
