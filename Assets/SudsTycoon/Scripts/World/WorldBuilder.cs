using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>The static environment of each location (port of world.js).</summary>
    public static class WorldBuilder
    {
        public class Theme
        {
            public string id, flora, lotTex;
            public uint sky, fog, ground, patch, road, sidewalk;
            public uint? water, shore;
            public float hemi = 0.6f, sun = 0.52f, amb = 0f, fogNear = 95f, fogFar = 210f;
            public uint sunColor = 0xfff1dc, hemiSky = 0xeaf6ff, hemiGround = 0x9fb07c;
            public bool night, snow, stars, rain;
            /// <summary>Colour of the neon strips in night themes.</summary>
            public uint[] neon;
        }
        public static readonly Dictionary<string, Theme> Themes = new Dictionary<string, Theme>
        {
            { "suburb", new Theme { id = "suburb", sky = 0x9fd9ff, fog = 0xc4e9ff, ground = 0x7cc850, patch = 0x8ad75b, road = 0x4d525b, sidewalk = 0xbfc4cc, water = 0x4aa8e8, shore = 0xe9dcb0, hemi = 0.6f, sun = 0.52f, flora = "oak" } },
            { "beach", new Theme { id = "beach", sky = 0x86d8ff, fog = 0xbfeaff, ground = 0xf1dc9f, patch = 0xe8cf8a, road = 0x55595f, sidewalk = 0xd5d0c4, water = 0x22c3e6, shore = 0xfff2c9, hemi = 0.62f, sun = 0.52f, flora = "palm" } },
            { "desert", new Theme { id = "desert", sky = 0xa8d8ff, fog = 0xf3d9b0, ground = 0xe6b273, patch = 0xd9a163, road = 0x5a5550, sidewalk = 0xd8c3a2, hemi = 0.66f, sun = 0.66f, sunColor = 0xffe6c4, hemiGround = 0xc49a6a, flora = "cactus" } },
            { "snow", new Theme { id = "snow", sky = 0xcfe7ff, fog = 0xe6f2ff, ground = 0xf3f8ff, patch = 0xe2ecf8, road = 0x50555e, sidewalk = 0xe1e6ee, water = 0xa9dcf2, shore = 0xffffff, hemi = 0.6f, sun = 0.45f, hemiGround = 0xb9c7da, flora = "pine", snow = true } },
            { "city", new Theme { id = "city", sky = 0x161c38, fog = 0x1d2446, ground = 0x343a45, patch = 0x3c4350, road = 0x2b2f36, sidewalk = 0x5a606b, water = 0x1e3a6b, shore = 0x4a505c, hemi = 0.52f, sun = 0.28f, sunColor = 0xa9c4ff, hemiSky = 0x9fb5ff, hemiGround = 0x4b4060, amb = 0.18f, flora = "city", night = true } },
            { "moon", new Theme { id = "moon", sky = 0x05060f, fog = 0x070914, ground = 0x9fa2aa, patch = 0x8d9099, road = 0x5a5e66, sidewalk = 0xb5b8bf, hemi = 0.5f, sun = 0.75f, sunColor = 0xffffff, hemiSky = 0xc9d4ff, hemiGround = 0x55586a, amb = 0.12f, flora = "moon", stars = true } },
            // ---- world tour variants (reuse the six prop sets and lot textures) ----
            { "metro", new Theme { id = "metro", sky = 0xa9d6f5, fog = 0xcfe3f2, ground = 0x8f969f, patch = 0x9aa1ab, road = 0x3f444c, sidewalk = 0xc5cad2, water = 0x3f8fd0, shore = 0xb7bcc4, hemi = 0.6f, sun = 0.55f, hemiGround = 0x8f9aa6, flora = "city" } },
            { "rain", new Theme { id = "rain", sky = 0x9fb0c0, fog = 0xa9b6c3, ground = 0x6e9757, patch = 0x7aa462, road = 0x40454d, sidewalk = 0xb3b8bf, water = 0x5d7f99, shore = 0xc8c2b0, hemi = 0.62f, sun = 0.3f, sunColor = 0xe6edf5, hemiSky = 0xd2dde8, hemiGround = 0x7d8f72, fogNear = 70f, fogFar = 170f, flora = "oak", rain = true } },
            { "paris", new Theme { id = "paris", sky = 0xb6dcff, fog = 0xd8ecff, ground = 0x9cc46a, patch = 0xb0d27e, road = 0x55524f, sidewalk = 0xdccfb7, water = 0x4b9ad6, shore = 0xe2d6bb, hemi = 0.6f, sun = 0.55f, sunColor = 0xfff0d8, flora = "oak" } },
            { "alps", new Theme { id = "alps", sky = 0xb9e0ff, fog = 0xdcefff, ground = 0xeef6fd, patch = 0xd9e7f4, road = 0x4c525c, sidewalk = 0xdfe5ee, water = 0x9fd6f0, shore = 0xffffff, hemi = 0.62f, sun = 0.5f, hemiGround = 0xb9c7da, flora = "pine", snow = true } },
            { "dubai", new Theme { id = "dubai", sky = 0x9fd0ff, fog = 0xf2dcb8, ground = 0xeec58a, patch = 0xe2b678, road = 0x4f4b47, sidewalk = 0xe0cfb0, hemi = 0.66f, sun = 0.66f, sunColor = 0xffe8c8, hemiGround = 0xc9a274, flora = "city" } },
            { "mumbai", new Theme { id = "mumbai", sky = 0xf3d2a4, fog = 0xedcfa6, ground = 0xc9a46a, patch = 0xb8945a, road = 0x4b4744, sidewalk = 0xd2bf9c, water = 0x5aa8b8, shore = 0xe8d4a8, hemi = 0.64f, sun = 0.58f, sunColor = 0xffdcae, hemiSky = 0xffe6c4, hemiGround = 0xa88a5c, flora = "palm" } },
            { "tokyo", new Theme { id = "tokyo", sky = 0x1d1230, fog = 0x2a183f, ground = 0x302a3c, patch = 0x3a3248, road = 0x2a2633, sidewalk = 0x5b5468, water = 0x2a2a6b, shore = 0x4a4458, hemi = 0.52f, sun = 0.26f, sunColor = 0xd0b0ff, hemiSky = 0xc8a8ff, hemiGround = 0x4b3a60, amb = 0.18f, flora = "city", night = true, neon = new[] { 0xff5ec8u, 0x8a6bffu, 0x5ef0ffu } } },
            { "sunset", new Theme { id = "sunset", sky = 0xffb08a, fog = 0xffc6a2, ground = 0xeed39a, patch = 0xe2c387, road = 0x55595f, sidewalk = 0xd8cdbd, water = 0x2aa6d6, shore = 0xffe9c4, hemi = 0.62f, sun = 0.55f, sunColor = 0xffb27a, hemiSky = 0xffd8bc, hemiGround = 0xc8a070, flora = "palm" } },
            { "tropic", new Theme { id = "tropic", sky = 0x7fd4ff, fog = 0xb4e8ff, ground = 0x9fd36a, patch = 0x8cc75a, road = 0x52575d, sidewalk = 0xd3cfc4, water = 0x14c2d9, shore = 0xfff0c2, hemi = 0.62f, sun = 0.55f, flora = "palm" } },
            { "mars", new Theme { id = "mars", sky = 0xd99a6c, fog = 0xcf8f63, ground = 0xb5582f, patch = 0xa44c28, road = 0x5a3a2c, sidewalk = 0xc98a64, hemi = 0.58f, sun = 0.62f, sunColor = 0xffe0c0, hemiSky = 0xffcfa8, hemiGround = 0x8a3a20, flora = "moon" } },
            { "cairo", new Theme { id = "cairo", sky = 0xf3d49c, fog = 0xeccb92, ground = 0xe2bb78, patch = 0xd4aa62, road = 0x5c564e, sidewalk = 0xe4d0a6, water = 0x3f9fb5, shore = 0xead7aa, hemi = 0.66f, sun = 0.68f, sunColor = 0xffdfa8, hemiSky = 0xffecc8, hemiGround = 0xc49a62, fogNear = 80f, fogFar = 190f, flora = "palm" } },
        };

        public static Theme Current { get; private set; }
        static Transform root;
        static GameObject lane2Bits;
        static ParticleSystem snowPs;
        const float MainRoadZ = -18.6f;
        static Rect L => Cfg.LotBounds;

        public static Transform Build(string themeId, Light sun, Camera cam)
        {
            Clear();
            var th = Current = Themes.TryGetValue(themeId, out var t) ? t : Themes["suburb"];
            ApplyLighting(th, sun, cam);
            root = new GameObject("World_" + th.id).transform;

            // --- ground + patches ---
            var g = new MeshKit();
            g.Flat(520, 520, U.Hex(th.ground), 0, 0, 0);
            for (int i = 0; i < 70; i++)
            {
                float x = U.Rand(-110, 110), z = U.Rand(-30, 110);
                if (InLot(x, z, 4)) continue;
                g.Disc(1, 10, U.Hex(th.patch), x, 0.005f, z, U.Rand(2, 7), U.Rand(1.5f, 5));
            }
            g.BuildObject("Ground", root, false);

            // --- lot slab (textured concrete) ---
            var lot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(lot.GetComponent<Collider>());
            lot.name = "Lot";
            lot.transform.SetParent(root, false);
            lot.transform.position = U.P(L.center.x, 0.02f, L.center.y);
            lot.transform.rotation = Quaternion.Euler(90, 0, 0);
            lot.transform.localScale = new Vector3(L.width, L.height, 1);
            var lotMat = Art.Make(Color.white, Art.Tex("lot_" + (th.lotTex ?? th.id)));
            lotMat.SetTextureScale("_BaseMap", new Vector2(L.width / 12f, L.height / 12f));
            lot.GetComponent<MeshRenderer>().sharedMaterial = lotMat;
            lot.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // --- painted lines, curbs, roads, lane 1 track ---
            var s = new MeshKit();
            for (float x = L.xMin + 3; x < L.xMax - 2; x += 3.2f) s.Box(0.12f, 0.012f, 1.6f, Color.white, x, 0.045f, L.yMax - 1.1f);
            var curb = th.night ? U.Hex(0x8d929c) : U.Hex(0xf2f3f6);
            void Fence(float x0, float z0, float x1, float z1)
            {
                float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)), ry = -Mathf.Atan2(z1 - z0, x1 - x0);
                if (len < 0.05f) return;
                s.Box(len, 0.5f, 0.32f, curb, (x0 + x1) / 2, 0.25f, (z0 + z1) / 2, ry);
                s.Box(len + 0.04f, 0.06f, 0.38f, Color.white, (x0 + x1) / 2, 0.53f, (z0 + z1) / 2, ry);
            }
            Fence(L.xMin, L.yMin, L.xMax, L.yMin);
            Fence(L.xMin, L.yMax, L.xMax, L.yMax);
            foreach (var x in new[] { L.xMin, L.xMax })
            {
                float z = L.yMin;
                foreach (var lz in Cfg.LaneZ) { Fence(x, z, x, lz - 2.3f); z = lz + 2.3f; }
                Fence(x, z, x, L.yMax);
            }
            var road = U.Hex(th.road);
            foreach (var z in Cfg.LaneZ)
            {
                RoadStrip(s, -100, L.xMin + 0.2f, z, 4.4f, road);
                RoadStrip(s, L.xMax - 0.2f, 100, z, 4.4f, road);
                s.Box(Cfg.EntranceX - L.xMin, 0.02f, 4.4f, road, (L.xMin + Cfg.EntranceX) / 2, 0.03f, z);
                s.Box(L.xMax - Cfg.ExitX, 0.02f, 4.4f, road, (Cfg.ExitX + L.xMax) / 2, 0.03f, z);
                for (float x = -98; x < 98; x += 6) if (x < L.xMin - 2 || x > L.xMax + 2) s.Box(2.6f, 0.012f, 0.16f, U.Hex(0xffd23f), x, 0.034f, z);
                Arrow(s, Cfg.EntranceX - 4.5f, z);
                Arrow(s, Cfg.ExitX + 2.4f, z);
            }
            LaneTrack(s, Cfg.LaneZ[0], Cfg.EntranceX - 0.6f, Cfg.ExitX + 0.6f);
            // main road behind the buildings
            RoadStrip(s, -120, 120, MainRoadZ, 7, road);
            for (float x = -118; x < 118; x += 5) s.Box(2.4f, 0.012f, 0.18f, Color.white, x, 0.034f, MainRoadZ);
            s.Box(240, 0.12f, 1.5f, U.Hex(th.sidewalk), 0, 0.06f, MainRoadZ + 4.2f);
            s.Box(240, 0.12f, 1.5f, U.Hex(th.sidewalk), 0, 0.06f, MainRoadZ - 4.2f);
            if (th.water.HasValue) s.Box(240, 0.1f, 2.4f, U.Hex(th.shore.Value), 0, 0.03f, MainRoadZ - 6.2f);
            s.BuildObject("LotAndRoads", root, true);

            if (th.water.HasValue)
            {
                var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(water.GetComponent<Collider>());
                water.name = "Water";
                water.transform.SetParent(root, false);
                water.transform.position = U.P(0, -0.02f, MainRoadZ - 7.4f - 35f);
                water.transform.rotation = Quaternion.Euler(90, 0, 0);
                water.transform.localScale = new Vector3(260, 70, 1);
                water.GetComponent<MeshRenderer>().sharedMaterial = Art.Solid(U.Hex(th.water.Value));
            }

            // --- lamps and small props ---
            for (float x = -100; x <= 100; x += 13) Prop(th.night ? "Props/lamp_night" : "Props/lamp", x, MainRoadZ + 4.6f, Mathf.PI / 2);
            foreach (var p in new[] { new Vector2(L.xMin + 1, -11.5f), new Vector2(L.xMax - 1, -11.5f), new Vector2(L.xMin + 1, 22), new Vector2(L.xMax - 1, 22), new Vector2(-8, 22.6f), new Vector2(8, 22.6f) })
                Prop(th.night ? "Props/lamp_night" : "Props/lamp", p.x, p.y, p.x < 0 ? 0 : Mathf.PI);
            Prop("Props/traffic_cone", Cfg.ExitX + 3.5f, 3.1f); Prop("Props/traffic_cone", Cfg.ExitX + 3.5f, -3.1f);
            Prop("Props/trash_can", -23.2f, 22.2f); Prop("Props/trash_can", 26, -12);

            Scatter(th);
            if (th.night) NightGlows();
            if (th.snow) Snow();
            if (th.rain) Rain();
            if (th.stars) Stars();
            Lane2Construction();
            return root;
        }

        /// <summary>Re-apply the current location's lighting (after the mini-game changed it).</summary>
        public static void Relight(Light sun, Camera cam) { if (Current != null) ApplyLighting(Current, sun, cam); }

        public static void ApplyLighting(Theme th, Light sun, Camera cam)
        {
            cam.backgroundColor = U.Hex(th.sky);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = U.Hex(th.fog);
            RenderSettings.fogStartDistance = th.fogNear;
            RenderSettings.fogEndDistance = th.fogFar;
            // The web build multiplied light in gamma space; convert so the linear-space result looks the same.
            float amb = th.hemi + th.amb;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = U.Hex(th.hemiSky) * amb;
            RenderSettings.ambientEquatorColor = Color.Lerp(U.Hex(th.hemiSky), U.Hex(th.hemiGround), 0.5f) * amb;
            RenderSettings.ambientGroundColor = U.Hex(th.hemiGround) * amb;
            RenderSettings.ambientIntensity = 1f;
            const float ndl = 0.825f;
            float lit = Mathf.Pow(amb + th.sun * ndl, 2.2f), shade = Mathf.Pow(amb, 2.2f);
            sun.color = U.Hex(th.sunColor);
            sun.intensity = Mathf.Max(0.05f, (lit - shade) / ndl);
            sun.shadows = Store.S != null && Store.S.settings.quality == "low" ? LightShadows.None : LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.transform.rotation = Quaternion.LookRotation(-U.P(new Vector3(-20, 34, -12)).normalized);
        }

        public static bool InLot(float x, float z, float m = 0) => x > L.xMin - m && x < L.xMax + m && z > L.yMin - m && z < L.yMax + m;
        static bool OnRoad(float x, float z, float m = 0)
        {
            if (Mathf.Abs(z - MainRoadZ) < 7.2f + m) return true;
            foreach (var lz in Cfg.LaneZ) if (Mathf.Abs(z - lz) < 3 + m) return true;
            return false;
        }
        static void RoadStrip(MeshKit b, float x0, float x1, float z, float w, Color col)
        {
            b.Box(x1 - x0, 0.02f, w, col, (x0 + x1) / 2, 0.02f, z);
            b.Box(x1 - x0, 0.022f, 0.14f, Color.white, (x0 + x1) / 2, 0.025f, z - w / 2 + 0.3f);
            b.Box(x1 - x0, 0.022f, 0.14f, Color.white, (x0 + x1) / 2, 0.025f, z + w / 2 - 0.3f);
        }
        static void Arrow(MeshKit b, float x, float z)
        {
            b.Box(1.8f, 0.012f, 0.4f, Color.white, x - 0.4f, 0.045f, z);
            b.Triangle(new Vector3(x + 1.45f, 0.046f, z), new Vector3(x + 0.33f, 0.046f, z - 0.65f), new Vector3(x + 0.33f, 0.046f, z + 0.65f), Color.white);
        }
        public static void LaneTrack(MeshKit b, float z, float x0, float x1)
        {
            b.Box(x1 - x0, 0.02f, 3.6f, U.Hex(0xaeb3bc), (x0 + x1) / 2, 0.03f, z);
            foreach (var sz in new[] { -1f, 1f }) b.Box(x1 - x0, 0.012f, 0.14f, Color.white, (x0 + x1) / 2, 0.042f, z + sz * 1.85f);
            b.Box(x1 - x0, 0.1f, 0.12f, U.Hex(0x8d96a5), (x0 + x1) / 2, 0.06f, z - 0.52f);
            b.Box(x1 - x0, 0.1f, 0.12f, U.Hex(0x8d96a5), (x0 + x1) / 2, 0.06f, z - 1.18f);
        }

        public static GameObject Prop(string path, float x, float z, float ry = 0, float scale = 1, Transform parent = null)
        {
            var go = Art.Spawn(path, parent ? parent : root);
            go.transform.position = U.P(x, 0, z);
            go.transform.rotation = U.Yaw(ry);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static void Scatter(Theme th)
        {
            var spots = new List<Vector3>();
            bool TryPlace(float x, float z, float r)
            {
                if (InLot(x, z, 2.5f) || OnRoad(x, z, r)) return false;
                if (z < -26 && th.water.HasValue) return false;
                foreach (var s in spots) if (U.Dist2(s.x, s.y, x, z) < (r + s.z) * (r + s.z)) return false;
                spots.Add(new Vector3(x, z, r));
                return true;
            }
            var holder = new GameObject("Scenery").transform; holder.SetParent(root, false);
            int n = th.flora == "city" ? 40 : 170;
            for (int i = 0; i < n * 3 && spots.Count < n; i++)
            {
                float x = U.Rand(-90, 95), z = U.Rand(-24, 70);
                bool near = Mathf.Abs(x) < 50 && z < 40;
                if (!near && Random.value < 0.5f) continue;
                float sc = U.Rand(0.85f, 1.35f);
                if (!TryPlace(x, z, 1.8f * sc)) continue;
                float rot = U.Rand(0, Mathf.PI * 2), r = Random.value;
                string path = null; float s2 = sc;
                switch (th.flora)
                {
                    case "oak": path = r < 0.78f ? "Props/tree_" + U.RandI(0, 3) : "Props/bush_" + U.RandI(0, 2); if (r >= 0.78f) s2 = sc * 1.2f; break;
                    case "palm": path = r < 0.6f ? "Props/palm" : r < 0.8f ? "Props/beach_umbrella" : "Props/surfboard"; break;
                    case "cactus": path = r < 0.55f ? "Props/cactus" : r < 0.85f ? "Props/rock_red" : "Props/mesa"; if (path == "Props/rock_red") s2 = sc * 1.6f; break;
                    case "pine": path = r < 0.82f ? "Props/pine_snow" : r < 0.91f ? "Props/snowman" : "Props/rock_snow"; if (path == "Props/snowman") s2 = sc * 0.8f; break;
                    case "moon": path = r < 0.45f ? "Props/crater" : r < 0.75f ? "Props/rock_moon" : r < 0.88f ? "Props/moon_dome" : "Props/antenna"; break;
                }
                if (path != null) Prop(path, x, z, rot, s2, holder);
            }
            if (th.flora == "city")
            {
                for (float x = -90; x < 95; x += U.Rand(7, 11))
                {
                    Prop("Props/city_block_" + U.RandI(0, 2), x, -33 - U.Rand(0, 6), 0, 1, holder);
                    if (x < L.xMin - 8 || x > L.xMax + 8) Prop("Props/city_block_" + U.RandI(0, 2), x, U.Rand(30, 45), Mathf.PI, 0.85f, holder);
                }
                for (float z = -6; z < 40; z += U.Rand(8, 12)) foreach (var x in new[] { -48f, 52f }) Prop("Props/city_block_" + U.RandI(0, 2), x + U.Rand(-4, 4), z, x < 0 ? Mathf.PI / 2 : -Mathf.PI / 2, 0.9f, holder);
            }
            if (th.flora == "oak" || th.flora == "palm")
                for (float x = L.xMin + 2; x < L.xMax - 1; x += 4.2f) if (Random.value < 0.7f) Prop("Props/bush_" + U.RandI(0, 2), x, L.yMax + 1.4f, U.Rand(0, 6), U.Rand(0.8f, 1.1f), holder);
        }

        static void NightGlows()
        {
            var mat = Art.Decal(Art.Tex("glow"), new Color(1f, 0.85f, 0.6f, 0.55f), true);
            void Glow(float x, float z)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root, false);
                q.transform.position = U.P(x, 0.06f, z);
                q.transform.rotation = Quaternion.Euler(90, 0, 0);
                q.transform.localScale = new Vector3(9, 9, 1);
                var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (float x = -100; x <= 100; x += 13) Glow(x, MainRoadZ + 4.6f);
            Glow(L.xMin + 1, -11.5f); Glow(L.xMax - 1, -11.5f); Glow(L.xMin + 1, 22); Glow(L.xMax - 1, 22); Glow(-8, 22.6f); Glow(8, 22.6f);
            var neon = Current.neon ?? new[] { 0xff3df2u, 0x3df2ffu, 0xfff23du };
            for (int i = 0; i < 3; i++)
            {
                var strip = new MeshKit().Box(L.width, 0.1f, 0.1f, Color.white, L.center.x, 0.62f, L.yMax + 0.2f + i * 0.12f).BuildObject("Neon" + i, root, false);
                strip.GetComponent<MeshRenderer>().sharedMaterial = Art.Solid(U.Hex(neon[i]), true);
            }
        }
        static void Snow()
        {
            var go = new GameObject("Snowfall");
            go.transform.SetParent(root, false);
            snowPs = go.AddComponent<ParticleSystem>();
            var main = snowPs.main;
            main.startLifetime = 12f; main.startSpeed = 0f; main.startSize = 0.28f; main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.02f;
            var em = snowPs.emission; em.rateOverTime = 110;
            var shape = snowPs.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(90, 1, 80);
            go.transform.position = U.P(0, 28, 5);
            var vel = snowPs.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f); vel.y = new ParticleSystem.MinMaxCurve(-2.6f, -1.8f); vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Art.Decal(Art.Tex("glow"), new Color(1, 1, 1, 0.9f));
            snowPs.Simulate(10f, true, true);
            snowPs.Play();
        }
        /// <summary>London drizzle: thin streaks falling fast.</summary>
        static void Rain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(root, false);
            snowPs = go.AddComponent<ParticleSystem>();
            var main = snowPs.main;
            main.startLifetime = 1.6f; main.startSpeed = 0f; main.maxParticles = 1600;
            main.startSize3D = true; main.startSizeX = 0.04f; main.startSizeY = 0.9f; main.startSizeZ = 0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = snowPs.emission; em.rateOverTime = 700;
            var shape = snowPs.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(90, 1, 80);
            go.transform.position = U.P(0, 28, 5);
            var vel = snowPs.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, -0.3f); vel.y = new ParticleSystem.MinMaxCurve(-22f, -18f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Art.Cube;
            r.sharedMaterial = Art.Make(new Color(0.78f, 0.86f, 0.95f, 0.45f), null, false, true, true);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            snowPs.Simulate(2f, true, true);
            snowPs.Play();
        }

        static void Stars()
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = false; main.startLifetime = 1e6f; main.startSpeed = 0; main.maxParticles = 900; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Art.Decal(Art.Tex("glow"), Color.white, true);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 900; i++)
            {
                float a = U.Rand(0, Mathf.PI * 2), el = U.Rand(0.02f, 1.2f);
                ep.position = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el)) * 300f;
                ep.startSize = U.Rand(1.2f, 2.6f); ep.startColor = Color.white; ep.startLifetime = 1e6f;
                ps.Emit(ep, 1);
            }
            var earth = new GameObject("Earth"); earth.transform.SetParent(root, false);
            earth.AddComponent<MeshFilter>().sharedMesh = Art.Ico;
            earth.AddComponent<MeshRenderer>().sharedMaterial = Art.Solid(U.Hex(0x3f8cff), true);
            earth.transform.position = U.P(-120, 90, -220); earth.transform.localScale = Vector3.one * 14;
        }

        static void Lane2Construction()
        {
            lane2Bits = new GameObject("Lane2Construction");
            lane2Bits.transform.SetParent(root, false);
            float z = Cfg.LaneZ[1];
            for (int i = 0; i < 9; i++) Prop("Props/traffic_cone", -14 + i * 4.2f, z + (i % 2 == 1 ? 1.2f : -1.2f), 0, 1.1f, lane2Bits.transform);
            var b = new MeshKit();
            foreach (var x in new[] { L.xMin + 1.2f, L.xMax - 1.2f })
            {
                b.Box(0.2f, 1f, 0.2f, Color.white, x, 0.5f, z - 1.9f); b.Box(0.2f, 1f, 0.2f, Color.white, x, 0.5f, z + 1.9f);
                for (int i = 0; i < 6; i++) b.Box(0.22f, 0.32f, 0.66f, i % 2 == 1 ? Color.white : U.Hex(0xe8383d), x, 0.9f, z - 1.65f + i * 0.66f);
            }
            b.Cylinder(0.08f, 0.08f, 2.2f, 6, U.Hex(0x7d8796), -5.5f, 1.1f, z);
            b.Cylinder(0.08f, 0.08f, 2.2f, 6, U.Hex(0x7d8796), -2.5f, 1.1f, z);
            b.BuildObject("Barriers", lane2Bits.transform);
            var sign = Art.Spawn("Buildings/lane2_sign", lane2Bits.transform);
            sign.transform.position = U.P(-4, 2.6f, z);
            sign.transform.rotation = U.Yaw(0.17f);
        }
        public static void OpenLane2()
        {
            if (lane2Bits) Object.Destroy(lane2Bits);
            lane2Bits = null;
            var b = new MeshKit();
            LaneTrack(b, Cfg.LaneZ[1], Cfg.EntranceX - 0.6f, Cfg.ExitX + 0.6f);
            b.BuildObject("Lane2Track", root, false);
        }

        public static void Tick(float dt)
        {
            if (snowPs && CameraRig.I)
            {
                var c = U.W(CameraRig.I.transform.position);
                snowPs.transform.position = U.P(Mathf.Round(c.x / 10) * 10 - 10, 28, Mathf.Round(c.z / 10) * 10 - 30);
            }
        }

        public static void Clear()
        {
            if (root) Object.Destroy(root.gameObject);
            root = null; lane2Bits = null; snowPs = null;
        }
        public static Transform Root => root;
    }
}
