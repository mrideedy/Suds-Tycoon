using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>Supply shelf that slowly restocks itself (port of Rack in stations.js).</summary>
    public class Rack
    {
        public readonly string Supply;
        readonly RackDef cfg;
        public readonly Transform Model;
        public readonly Vector3 Pos;
        /// <summary>Pick-up mat (web x / z).</summary>
        public readonly Vector2 Mat;
        readonly Transform decal, giant;
        readonly List<Transform> slots = new List<Transform>();
        readonly List<Transform> items = new List<Transform>();
        float t, puff, giantAngle;

        public Rack(string supply, int stock, Transform parent)
        {
            Supply = supply; cfg = Cfg.Racks[supply];
            Model = Art.Spawn("Machines/rack_" + supply, parent).transform;
            var p = Cfg.RacksPos[supply];
            Pos = new Vector3(p.x, 0f, p.y);
            Model.position = U.P(Pos);
            Mat = new Vector2(p.x + 0.35f, p.y + Cfg.RackMatDZ);
            decal = Art.GroundDecal("RackMat", Art.DecalMat("mat_pick_" + supply), 1.7f, parent, Mat.x, 0.05f, Mat.y);
            giant = U.Find(Model, "Giant");
            for (int i = 0; i < 18; i++) { var s = U.Find(Model, "Slot" + i); if (s) slots.Add(s); }
            for (int i = 0; i < Mathf.Min(stock, Cap); i++) SpawnItem(false);
        }

        public int Cap => Mathf.Min(slots.Count, cfg.cap);
        public float Time => cfg.time;
        public int Stock => items.Count;

        void SpawnItem(bool anim)
        {
            if (items.Count >= slots.Count) return;
            var m = Art.Spawn("Items/item_" + Supply, slots[items.Count]).transform;
            m.localPosition = Vector3.zero;
            m.localRotation = Quaternion.Euler(0f, U.Rand(-0.2f, 0.2f) * Mathf.Rad2Deg, 0f);
            items.Add(m);
            if (anim) { Fx.PopIn(m); puff = 1f; }
        }

        /// <summary>Remove the top item; returns its web position for the flying copy.</summary>
        public bool Take(out Vector3 webPos)
        {
            webPos = Vector3.zero;
            if (items.Count == 0) return false;
            var m = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            webPos = U.W(m.position);
            Object.Destroy(m.gameObject);
            return true;
        }

        public void Update(float dt)
        {
            if (items.Count < Cap)
            {
                t += dt;
                if (t >= Time) { t = 0f; SpawnItem(true); }
            }
            else t = 0f;
            if (!giant) return;
            puff = U.Damp(puff, 0f, 6f, dt);
            float s = 2.7f * (1f + Mathf.Sin(UnityEngine.Time.time * 1000f / 120f) * 0.04f * puff + puff * 0.08f);
            giant.localScale = new Vector3(s, 2.7f * (1f - puff * 0.08f), s);
            giantAngle += dt * 0.4f;
            giant.localRotation = Quaternion.Euler(0f, -giantAngle * Mathf.Rad2Deg, 0f);
        }

        public IEnumerable<Box2> Colliders()
        {
            float x = Pos.x, z = Pos.z;
            yield return new Box2(x - 2.75f, x - 1.15f, z - 0.68f, z + 0.68f);
            yield return new Box2(x - 0.95f, x + 1.65f, z - 0.66f, z + 0.66f);
        }

        public void Destroy()
        {
            if (Model) Object.Destroy(Model.gameObject);
            if (decal) Object.Destroy(decal.gameObject);
        }
    }
}
