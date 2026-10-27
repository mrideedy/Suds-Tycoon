using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>A wash lane: entrance and exit gates, its stations in slot order, the cars on it and its cash pile.</summary>
    public class Lane
    {
        public readonly int Idx;
        public readonly float Z;
        public readonly Dictionary<string, Station> Stations = new Dictionary<string, Station>();
        public readonly List<Car> Cars = new List<Car>();
        public bool Unlocked;
        public Transform Entrance, Exit;
        public Renderer EntranceLamp;
        public CashPile Pile;
        readonly Transform parent;
        List<Station> stops;

        public Lane(int idx, Transform parent)
        {
            Idx = idx; Z = Cfg.LaneZ[idx];
            this.parent = parent;
        }

        public void Unlock(bool anim)
        {
            if (Unlocked) return;
            Unlocked = true;
            Entrance = Art.Spawn("Machines/gate_entrance", parent).transform;
            Entrance.position = U.P(Cfg.EntranceX, 0f, Z);
            var lamp = U.Find(Entrance, "Lamp");
            if (lamp) EntranceLamp = lamp.GetComponent<Renderer>();
            Exit = Art.Spawn("Machines/gate_exit", parent).transform;
            Exit.position = U.P(Cfg.ExitX, 0f, Z);
            Pile = new CashPile(Cfg.SlotX["pay"] + Cfg.CashDX, Z + Cfg.CashDZ, parent);
            if (Idx > 0) WorldBuilder.OpenLane2();
            if (anim) { Fx.PopIn(Entrance, 0.1f); Fx.PopIn(Exit, 0.25f); }
        }

        public Station AddStation(string type, bool anim, int hopperCount = 3)
        {
            var st = new Station(this, type, parent);
            st.MakeHopper(hopperCount, parent);
            Stations[type] = st;
            if (anim) { Fx.PopIn(st.Group); if (st.Hopper != null) Fx.PopIn(st.Hopper.Model, 0.15f); }
            stops = null;
            return st;
        }

        /// <summary>Cars still waiting outside the entrance.</summary>
        public int QueueCount { get { int n = 0; foreach (var c in Cars) if (c.Waiting) n++; return n; } }

        public List<Station> Stops()
        {
            if (stops != null) return stops;
            stops = new List<Station>();
            foreach (var t in Cfg.SlotOrder) if (Stations.TryGetValue(t, out var st)) stops.Add(st);
            return stops;
        }
        public Station FirstStop() { var s = Stops(); return s.Count > 0 ? s[0] : null; }
        public Station Get(string type) => Stations.TryGetValue(type, out var st) ? st : null;

        public IEnumerable<Box2> Colliders()
        {
            foreach (var st in Stations.Values) foreach (var b in st.Colliders()) yield return b;
            if (!Unlocked) yield break;
            foreach (var gx in new[] { Cfg.EntranceX, Cfg.ExitX })
                foreach (var sz in new[] { -1f, 1f })
                    yield return new Box2(gx - 0.36f, gx + 0.36f, Z + sz * 2.35f - 0.36f, Z + sz * 2.35f + 0.36f);
            float px = Cfg.SlotX["pay"] + Cfg.CashDX, pz = Z + Cfg.CashDZ;
            yield return new Box2(px - 0.85f, px + 0.85f, pz - 0.62f, pz + 0.62f);
        }

        public void Destroy()
        {
            foreach (var st in Stations.Values) st.Destroy();
            if (Entrance) Object.Destroy(Entrance.gameObject);
            if (Exit) Object.Destroy(Exit.gameObject);
            Pile?.Destroy();
        }
    }
}
