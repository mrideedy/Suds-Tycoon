using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>A rectangle on a car body where mud and foam can stick (web coordinates, relative to Inner or Hover).</summary>
    public class SurfPatch
    {
        public string kind, parent;
        public float x0, x1, y, z0, z1, y0, y1, z;
    }
    public class WheelMeta { public float x, y, z, r; }

    /// <summary>Per car type size, mud surfaces and wheels, exported with the models (Resources/Models/Cars/cars.json).</summary>
    public class CarMeta
    {
        public float len, w, h;
        public List<SurfPatch> surf = new List<SurfPatch>();
        public List<WheelMeta> wheels = new List<WheelMeta>();

        static Dictionary<string, CarMeta> all;
        public static CarMeta Get(string type)
        {
            if (all == null)
            {
                var ta = Resources.Load<TextAsset>("Models/Cars/cars");
                all = ta ? JsonConvert.DeserializeObject<Dictionary<string, CarMeta>>(ta.text) : new Dictionary<string, CarMeta>();
                if (!ta) Debug.LogWarning("[cars] Resources/Models/Cars/cars.json is missing");
            }
            return all.TryGetValue(type, out var m) ? m : null;
        }
    }
}
