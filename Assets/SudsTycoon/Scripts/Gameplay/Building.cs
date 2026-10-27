using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>Buildings and decorations bought with pads (port of Building in stations.js).</summary>
    public class Building
    {
        public readonly string Id;
        public readonly Vector3 Pos;
        public readonly Transform Group;
        public readonly List<Box2> Coll = new List<Box2>();
        /// <summary>Walk-on mat that opens a panel (shop / staff / office / map), if any.</summary>
        public Vector2? Mat;
        public string Panel;
        public CashPile Tray;
        public Transform Keeper;

        readonly Transform decal;
        readonly WLabel matLbl;
        readonly List<Transform> segs = new List<Transform>();
        readonly List<Transform> armsL = new List<Transform>(), armsR = new List<Transform>();
        readonly float jetY;
        bool inMat;
        float t, coinT = 4f;

        public Building(string id, bool anim, string theme, Transform parent)
        {
            Id = id;
            var p = Cfg.BuildingPos[id];
            Pos = new Vector3(p.x, 0f, p.y);
            void Box(float hx, float hz, float ox = 0f, float oz = 0f) => Coll.Add(new Box2(Pos.x + ox - hx, Pos.x + ox + hx, Pos.z + oz - hz, Pos.z + oz + hz));
            Group = new GameObject("Building_" + id).transform;
            Group.SetParent(parent, false);
            Group.position = U.P(Pos);
            switch (id)
            {
                case "shop": Keeper = U.Find(Art.Spawn("Buildings/shop", Group).transform, "Keeper"); Box(6.3f, 3.3f); break;
                case "staff": Art.Spawn("Buildings/staff_room", Group); Box(3.25f, 2.55f); Box(1.0f, 0.3f, 2.0f, 2.9f); break;
                case "office": Art.Spawn("Buildings/office", Group); Box(2.75f, 2.45f); break;
                case "vip": Art.Spawn("Buildings/vip_lounge", Group); Box(3.55f, 2.55f); break;
                case "coffee": Art.Spawn("Buildings/coffee_machine", Group); Box(0.7f, 0.55f); break;
                case "vending": Art.Spawn("Buildings/vending", Group); Box(0.7f, 0.5f); break;
                case "queue": Art.Spawn("Buildings/waiting_area", Group); Box(2.4f, 0.5f); break;
                case "tubeman":
                {
                    bool night = WorldBuilder.Themes.TryGetValue(theme, out var th) && th.night;
                    var tm = Art.Spawn(night ? "Buildings/tube_man_neon" : "Buildings/tube_man", Group).transform;
                    for (int i = 0; i < 6; i++) { var s = U.Find(tm, "Seg" + i); if (s) segs.Add(s); }
                    for (int i = 0; i < 3; i++) { var a = U.Find(tm, "ArmL" + i); if (a) armsL.Add(a); a = U.Find(tm, "ArmR" + i); if (a) armsR.Add(a); }
                    Box(0.55f, 0.55f);
                    break;
                }
                case "fountain":
                {
                    var f = Art.Spawn("Buildings/fountain", Group).transform;
                    var jet = U.Find(f, "Jet");
                    jetY = jet ? U.W(jet.position).y : 2.9f;
                    Box(2.35f, 2.35f);
                    break;
                }
                case "billboard":
                {
                    var b = Art.Spawn("Buildings/billboard", Group).transform;
                    b.localRotation = U.Yaw(0.25f);
                    Box(0.3f, 0.3f, -2.3f, 0.6f); Box(0.3f, 0.3f, 2.3f, -0.6f);
                    break;
                }
                case "flowers":
                {
                    var spots = new[] { (0f, 0f, 3.4f, "flower_bed_3_4"), (-14.5f, 1.4f, 3.0f, "flower_bed_3"), (3.7f, 2.0f, 2.6f, "flower_bed_2_6") };
                    foreach (var (dx, dz, l, model) in spots)
                    {
                        var fb = Art.Spawn("Buildings/" + model, Group).transform;
                        fb.localPosition = U.P(dx, 0f, dz);
                        Box(l / 2f + 0.05f, 0.62f, dx, dz);
                    }
                    break;
                }
                case "landmark": Art.Spawn("Buildings/landmark_" + Game.L.landmark, Group); Box(2.3f, 2.3f); break;
                case "travel":
                    Art.Spawn("Buildings/travel_sign", Group);
                    Box(1.0f, 0.2f, 0f, -1.25f);
                    Mat = new Vector2(Pos.x, Pos.z + 0.4f); Panel = "map";
                    break;
            }
            if (Cfg.BuildingMat.TryGetValue(id, out var mp)) { Mat = mp; Panel = id; }
            if (Mat.HasValue)
            {
                string icon = Panel == "shop" ? "bag" : Panel == "staff" ? "staff" : Panel == "office" ? "office" : "map";
                string text = Panel == "shop" ? "SHOP" : Panel == "staff" ? "STAFF" : Panel == "office" ? "UPGRADES" : "MAP";
                var m = Mat.Value;
                decal = Art.GroundDecal("BuildingMat", Art.DecalMat("mat_spot_" + icon), 1.8f, parent, m.x, 0.06f, m.y);
                matLbl = WorldUI.Label(new Vector3(m.x, 0.15f, m.y + 1.15f));
                matLbl.Set(text, e => e.Add(WorldUI.Txt(text, "mat-lbl")));
            }
            if (id == "vending") Tray = new CashPile(Pos.x + 1.25f, Pos.z + 0.7f, parent, true, 0.02f);
            t = Random.value * 10f;
            if (anim) { Fx.PopIn(Group); Fx.Confetti(new Vector3(Pos.x, 2f, Pos.z), 50, 7f); }
        }

        public void Update(float dt)
        {
            t += dt;
            if (segs.Count > 0)
            {
                for (int i = 0; i < segs.Count; i++)
                {
                    float rz = Mathf.Sin(t * 5.5f + i * 0.9f) * (0.18f + i * 0.05f) + Mathf.Sin(t * 2.1f) * 0.08f;
                    float rx = Mathf.Sin(t * 4.3f + i * 1.3f) * 0.12f;
                    segs[i].localRotation = Quaternion.AngleAxis(rx * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(-rz * Mathf.Rad2Deg, Vector3.forward);
                }
                for (int i = 0; i < armsL.Count; i++) armsL[i].localRotation = Quaternion.Euler(0f, 0f, -(-1f * (Mathf.Sin(t * 7f + i) * 0.9f + 0.2f)) * Mathf.Rad2Deg);
                for (int i = 0; i < armsR.Count; i++) armsR[i].localRotation = Quaternion.Euler(0f, 0f, -(Mathf.Sin(t * 7f + i + 1.5f) * 0.9f + 0.2f) * Mathf.Rad2Deg);
            }
            if (Id == "fountain" && Random.value < dt * 14f)
            {
                float a = Random.value * Mathf.PI * 2f;
                Fx.Jet(new Vector3(Pos.x, jetY, Pos.z), new Vector3(Mathf.Cos(a) * 1.5f, U.Rand(3f, 4.5f), Mathf.Sin(a) * 1.5f), 0.9f, 0.13f);
            }
            if (Tray != null)
            {
                Tray.Update(dt);
                coinT -= dt;
                if (coinT <= 0f)
                {
                    coinT = 6f;
                    int waiting = 0;
                    foreach (var l in Game.Lanes) waiting += l.QueueCount;
                    if (waiting > 0)
                    {
                        double v = System.Math.Max(1, System.Math.Round(Game.BasePrice() * 0.35 * waiting));
                        Tray.Add(v, new Vector3(Pos.x, 1.2f, Pos.z), 1);
                        if (Game.Player.Near(Pos.x, Pos.z, 25f)) WorldUI.Text(new Vector3(Pos.x, 2.6f, Pos.z), "SNACK SALE!", "tip", 1f);
                    }
                }
            }
            if (Mat.HasValue)
            {
                var m = Mat.Value;
                bool inside = Game.Player.Near(m.x, m.y, 1.0f);
                if (inside && !inMat && Game.Player.Speed01 < 0.95f) { inMat = true; Bus.Emit("mat:enter", Panel); }
                else if (!inside && inMat && !Game.Player.Near(m.x, m.y, 1.5f)) inMat = false;
            }
        }

        public void Destroy()
        {
            if (Group) Object.Destroy(Group.gameObject);
            if (decal) Object.Destroy(decal.gameObject);
            matLbl?.Remove();
            Tray?.Destroy();
        }
    }
}
