using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>Supply tank next to a station (soap / wax / gel). Port of Hopper in stations.js.</summary>
    public class Hopper
    {
        public readonly Station St;
        public readonly string Supply;
        public readonly int Cap = Cfg.HopperCap;
        public readonly Transform Model;
        public readonly Vector3 Pos;
        /// <summary>Drop-off mat (web x / z).</summary>
        public readonly Vector2 Mat;
        public int Count, Incoming, Pending;

        readonly Transform decal;
        readonly WLabel lbl;
        readonly List<Renderer> pips = new List<Renderer>();
        readonly Color pipOn, pipOff = U.Hex(0x3a3f47);
        float bump;
        string lblKey;
        static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        public Hopper(Station st, int count, Transform parent)
        {
            St = st; Supply = st.Def.supply;
            Model = Art.Spawn("Machines/hopper_" + Supply, parent).transform;
            Pos = new Vector3(st.X, 0f, st.Z + Cfg.HopperDZ);
            Model.position = U.P(Pos);
            Mat = new Vector2(st.X, st.Z + Cfg.MatDZ);
            decal = Art.GroundDecal("HopperMat", Art.DecalMat("mat_drop_" + Supply), 1.6f, parent, Mat.x, 0.05f, Mat.y);
            for (int i = 0; i < 8; i++)
            {
                var p = U.Find(Model, "Pip" + i);
                if (p && p.TryGetComponent<Renderer>(out var r)) pips.Add(r);
            }
            pipOn = U.Hex(Cfg.Supplies[Supply].color);
            Count = count;
            lbl = WorldUI.Label(new Vector3(Pos.x, 2.0f, Pos.z));
            Refresh();
        }

        public int Space => Cap - Count - Pending;

        /// <summary>An item flies from a carry stack into the tank.</summary>
        public void Receive(Transform item, Vector3 fromWeb)
        {
            Pending++;
            var top = new Vector3(Pos.x, 1.7f, Pos.z);
            Fx.Fly(item, fromWeb, () => top, 0.25f, 1.0f, m =>
            {
                if (m) Object.Destroy(m.gameObject);
                Pending = Mathf.Max(0, Pending - 1);
                Count = Mathf.Min(Cap, Count + 1);
                Refresh(); Sfx.Play("pop");
                bump = 1f;
            }, 6f);
        }

        public void Consume() { Count = Mathf.Max(0, Count - 1); Refresh(); }

        void Refresh()
        {
            for (int i = 0; i < pips.Count; i++)
            {
                mpb.SetColor("_BaseColor", i < Count ? pipOn : pipOff);
                pips[i].SetPropertyBlock(mpb);
            }
            bool low = Count <= 1;
            string key = Count >= Cap ? null : Count + (low ? "L" : "");
            if (key == lblKey) return;
            lblKey = key;
            if (key == null) { lbl.Clear(); return; }
            string text = Count + "/" + Cap;
            lbl.Set(key, e => e.Add(WorldUI.Chip("hopper-lbl" + (low ? " low" : ""), Cfg.Supplies[Supply].icon, text)));
        }

        public void Update(float dt)
        {
            if (bump <= 0f) return;
            bump = U.Damp(bump, 0f, 8f, dt);
            if (bump < 0.002f) bump = 0f;
            float s = 1f + Mathf.Sin(bump * 12f) * bump * 0.06f;
            Model.localScale = new Vector3(s, 1f / s, s);
        }

        public IEnumerable<Box2> Colliders() { yield return new Box2(Pos.x - 0.62f, Pos.x + 0.62f, Pos.z - 0.5f, Pos.z + 0.5f); }

        public void Destroy()
        {
            if (Model) Object.Destroy(Model.gameObject);
            if (decal) Object.Destroy(decal.gameObject);
            lbl.Remove();
        }
    }
}
