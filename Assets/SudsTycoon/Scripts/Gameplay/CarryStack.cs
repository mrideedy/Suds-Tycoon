using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SudsTycoon
{
    /// <summary>A wobbly tower of supplies carried in front of a person (Pizza Ready style).</summary>
    public class CarryStack
    {
        public class Item { public string type; public Transform tr; public bool landed; }

        readonly Func<int> capFn;
        public readonly List<Item> Items = new List<Item>();
        public readonly Transform Holder;
        float sway;

        public CarryStack(Transform body, Func<int> capFn)
        {
            this.capFn = capFn;
            Holder = new GameObject("Stack").transform;
            Holder.SetParent(body, false);
            Holder.localPosition = U.P(0f, 0.98f, 0.5f);
        }

        public int Count => Items.Count;
        public int Cap => capFn();
        public bool Full => Items.Count >= Cap;
        public int CountOf(string type) { int n = 0; foreach (var it in Items) if (it.type == type) n++; return n; }
        /// <summary>World (web) position of stack slot i.</summary>
        public Vector3 SlotWorld(int i) => U.W(Holder.TransformPoint(0f, i * 0.44f, 0f));

        public Item Add(string type, Vector3 fromWeb)
        {
            var tr = Art.Spawn("Items/item_" + type).transform;
            var it = new Item { type = type, tr = tr };
            int idx = Items.Count;
            Items.Add(it);
            Fx.Fly(tr, fromWeb, () => { int i = Items.IndexOf(it); return SlotWorld(i >= 0 ? i : idx); }, 0.22f, 0.9f, t =>
            {
                if (!Items.Contains(it)) { Object.Destroy(t.gameObject); return; }
                t.SetParent(Holder, false);
                t.localRotation = Quaternion.identity;
                it.landed = true;
                Layout();
            }, 8f);
            return it;
        }

        /// <summary>Remove the top-most item of a type. Returns its transform (now unparented) and web position, or null.</summary>
        public Item Take(string type, out Vector3 webPos)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i].type != type) continue;
                var it = Items[i];
                Items.RemoveAt(i);
                Fx.CancelFly(it.tr);
                webPos = U.W(it.tr.position);
                it.tr.SetParent(null, true);
                Layout();
                return it;
            }
            webPos = Vector3.zero;
            return null;
        }

        public void Clear()
        {
            foreach (var it in Items) { Fx.CancelFly(it.tr); if (it.tr) Object.Destroy(it.tr.gameObject); }
            Items.Clear();
        }

        void Layout()
        {
            for (int i = 0; i < Items.Count; i++) if (Items[i].landed) Items[i].tr.localPosition = new Vector3(0f, i * 0.44f, 0f);
        }

        public void Update(float dt, float speed01)
        {
            sway = U.Damp(sway, speed01, 5f, dt);
            float t = Time.time;
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (!it.landed || !it.tr) continue;
                var p = it.tr.localPosition;
                float z = -sway * i * 0.035f + Mathf.Sin(t * 9f + i * 0.6f) * 0.008f * i * sway;
                float x = Mathf.Sin(t * 7f + i * 0.5f) * 0.01f * i * sway;
                it.tr.localPosition = new Vector3(-x, U.Damp(p.y, i * 0.44f, 18f, dt), z);
            }
        }
    }
}
