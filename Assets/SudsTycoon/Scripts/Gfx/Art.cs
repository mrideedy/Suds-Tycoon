using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SudsTycoon
{
    /// <summary>
    /// Loads the models exported from the web build (Resources/Models/*.glb, imported by glTFast) and swaps their
    /// materials for the vertex-colour shader. Also owns shared materials, textures, icons and a few basic meshes.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> converted = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> colorMats = new Dictionary<string, Material>();
        static Shader litShader, particleShader;
        static Mesh ico, quad, cube, cylinder;
        static Font font;

        public static Shader Lit => litShader ? litShader : (litShader = Resources.Load<Shader>("Shaders/VertexColorLit"));
        public static Shader ParticleShader => particleShader ? particleShader : (particleShader = Resources.Load<Shader>("Shaders/ParticleUnlit"));

        // ---------------- prefabs ----------------
        public static GameObject Prefab(string path)
        {
            if (prefabs.TryGetValue(path, out var p)) return p;
            p = Resources.Load<GameObject>("Models/" + path);
            if (!p) Debug.LogWarning("[art] missing model: " + path);
            prefabs[path] = p;
            return p;
        }
        /// <summary>Instantiate an exported model with converted materials. Returns an empty object if missing.</summary>
        public static GameObject Spawn(string path, Transform parent = null, bool shadows = true)
        {
            var p = Prefab(path);
            var go = p ? Object.Instantiate(p, parent, false) : new GameObject(path + " (missing)");
            if (parent) go.transform.SetParent(parent, false);
            go.name = path.Substring(path.LastIndexOf('/') + 1);
            Convert(go, shadows);
            return go;
        }

        public static void Convert(GameObject go, bool shadows = true)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                bool vc = mf && mf.sharedMesh && mf.sharedMesh.HasVertexAttribute(VertexAttribute.Color);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = ConvertMat(mats[i], vc, r.name);
                r.sharedMaterials = mats;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
        }

        static readonly string[] ColorProps = { "baseColorFactor", "_BaseColor", "_Color" };
        static readonly string[] TexProps = { "baseColorTexture", "_BaseMap", "_MainTex" };
        static Material ConvertMat(Material src, bool vc, string rendererName)
        {
            if (src == null) return VertexColor();
            if (src.shader == Lit) return src;
            string key = src.GetInstanceID() + (vc ? "v" : "n");
            if (converted.TryGetValue(key, out var m)) return m;
            Color col = Color.white;
            foreach (var p in ColorProps) if (src.HasProperty(p)) { col = src.GetColor(p); break; }
            Texture tex = null;
            foreach (var p in TexProps) if (src.HasProperty(p)) { tex = src.GetTexture(p); if (tex) break; }
            bool unlit = src.name.StartsWith("Unlit") || src.shader.name.ToLowerInvariant().Contains("unlit");
            bool transparent = col.a < 0.99f || src.renderQueue >= 3000;
            bool doubleSided = (src.HasProperty("_CullMode") && src.GetFloat("_CullMode") < 0.5f) || (src.HasProperty("_Cull") && src.GetFloat("_Cull") < 0.5f) || src.doubleSidedGI;
            m = Make(col, tex, vc, unlit, transparent, doubleSided);
            m.name = src.name + " (Suds)";
            converted[key] = m;
            return m;
        }

        public static Material Make(Color col, Texture tex = null, bool vertexColors = false, bool unlit = false, bool transparent = false, bool doubleSided = false)
        {
            var m = new Material(Lit) { enableInstancing = true };
            m.SetColor("_BaseColor", col);
            if (tex) m.SetTexture("_BaseMap", tex);
            if (vertexColors) { m.EnableKeyword("_VERTEX_COLORS"); m.SetFloat("_UseVertexColors", 1); }
            if (unlit) { m.EnableKeyword("_UNLIT"); m.SetFloat("_Unlit", 1); }
            m.SetFloat("_Cull", doubleSided ? 0 : 2);
            if (transparent) SetTransparent(m, true);
            return m;
        }
        public static void SetTransparent(Material m, bool on)
        {
            m.SetFloat("_SrcBlend", on ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            m.SetFloat("_DstBlend", on ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", on ? 0 : 1);
            m.SetOverrideTag("RenderType", on ? "Transparent" : "Opaque");
            m.renderQueue = on ? (int)RenderQueue.Transparent : -1;
            m.SetShaderPassEnabled("ShadowCaster", !on);
        }

        static Material vcShared;
        /// <summary>Shared opaque material for vertex-coloured meshes built in code.</summary>
        public static Material VertexColor() => vcShared ? vcShared : (vcShared = Make(Color.white, null, true));
        /// <summary>Cached solid colour material (opaque, lit unless unlit is set).</summary>
        public static Material Solid(Color c, bool unlit = false, bool transparent = false)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + (unlit ? "u" : "") + (transparent ? "t" : "");
            if (!colorMats.TryGetValue(key, out var m)) colorMats[key] = m = Make(c, null, false, unlit, transparent);
            return m;
        }
        /// <summary>Unlit textured material for decals and particles.</summary>
        public static Material Decal(Texture tex, Color tint, bool additive = false)
        {
            var m = new Material(ParticleShader) { enableInstancing = true };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_SrcBlend", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            return m;
        }

        // ---------------- ground decals ----------------
        static Material shadowMat;
        static readonly Dictionary<string, Material> decalMats = new Dictionary<string, Material>();
        /// <summary>Soft round blob shadow under people.</summary>
        public static Material ShadowMat
        {
            get
            {
                if (shadowMat) return shadowMat;
                shadowMat = Decal(Tex("soft_shadow"), new Color(1f, 1f, 1f, 0.5f));
                shadowMat.renderQueue = 3005;   // after the floor mats so it darkens them
                return shadowMat;
            }
        }
        /// <summary>Cached decal material for a texture in Resources/Textures.</summary>
        public static Material DecalMat(string tex)
        {
            if (!decalMats.TryGetValue(tex, out var m)) decalMats[tex] = m = Decal(Tex(tex), Color.white);
            return m;
        }
        /// <summary>Flat quad lying on the ground at a web position. The texture's top points away from the camera, like the web build.</summary>
        public static Transform GroundDecal(string name, Material mat, float size, Transform parent = null, float x = 0f, float y = 0.05f, float z = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.localPosition = U.P(x, y, z);
            go.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            return go.transform;
        }

        // ---------------- textures, icons, font ----------------
        public static Texture2D Tex(string name)
        {
            if (textures.TryGetValue("t/" + name, out var t)) return t;
            t = Resources.Load<Texture2D>("Textures/" + name);
            if (!t) Debug.LogWarning("[art] missing texture: " + name);
            return textures["t/" + name] = t;
        }
        public static Texture2D Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "star";
            if (textures.TryGetValue("i/" + name, out var t)) return t;
            t = Resources.Load<Texture2D>("Icons/" + name);
            if (!t && name != "star") t = Icon("star");
            return textures["i/" + name] = t;
        }
        public static Font Font => font ? font : (font = Resources.Load<Font>("Fonts/LilitaOne-Regular"));

        // ---------------- basic meshes ----------------
        public static Mesh Quad
        {
            get
            {
                if (quad) return quad;
                quad = new Mesh { name = "SudsQuad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quad.RecalculateBounds();
                return quad;
            }
        }
        public static Mesh Cube => cube ? cube : (cube = Primitive(PrimitiveType.Cube));
        public static Mesh Cylinder => cylinder ? cylinder : (cylinder = Primitive(PrimitiveType.Cylinder));
        static Mesh Primitive(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return m;
        }
        /// <summary>Faceted icosphere (one subdivision), radius 1, flat normals: the low-poly blob used for foam, mud and smoke.</summary>
        public static Mesh Ico
        {
            get
            {
                if (ico) return ico;
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var v = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
                var tris = new List<int>();
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(k, out int i)) return i;
                    v.Add((v[a] + v[b]) * 0.5f); return cache[k] = v.Count - 1;
                }
                for (int i = 0; i < f.Length; i += 3)
                {
                    int a = Mid(f[i], f[i + 1]), b = Mid(f[i + 1], f[i + 2]), c = Mid(f[i + 2], f[i]);
                    tris.AddRange(new[] { f[i], a, c, f[i + 1], b, a, f[i + 2], c, b, a, b, c });
                }
                // un-index for flat shading; swap winding (the face list is counter-clockwise, Unity wants clockwise)
                var pos = new Vector3[tris.Count]; var idx = new int[tris.Count]; var col = new Color[tris.Count];
                for (int i = 0; i < tris.Count; i += 3)
                {
                    pos[i] = v[tris[i]].normalized; pos[i + 1] = v[tris[i + 2]].normalized; pos[i + 2] = v[tris[i + 1]].normalized;
                    idx[i] = i; idx[i + 1] = i + 1; idx[i + 2] = i + 2;
                    col[i] = col[i + 1] = col[i + 2] = Color.white;
                }
                ico = new Mesh { name = "SudsIco", vertices = pos, colors = col };
                ico.SetTriangles(idx, 0);
                ico.RecalculateNormals();
                ico.RecalculateBounds();
                return ico;
            }
        }
    }
}
