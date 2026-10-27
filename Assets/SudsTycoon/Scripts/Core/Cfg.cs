using System;
using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    public class StationDef
    {
        public string id, name, shortName, icon, supply, op;
        /// <summary>Money per car at Lv 1, and how much each level adds (whole dollars, x the city price).</summary>
        public int value, inc;
        public float time, upCost;
    }
    public class SupplyDef { public string id, name, icon; public uint color; }
    public class RackDef { public string id, name, supply; public float time; public int cap; }
    /// <summary>
    /// Business upgrade: bought with cash at the Manager Office, kept for good and used in every city.
    /// The row reads text with {0} = num(level), e.g. "Carry {0} items" -> "Carry 7 -> 8 items".
    /// </summary>
    public class BizDef
    {
        public string id, name, icon, text;
        public int max; public float cost, growth;
        public Func<int, string> num;
    }
    public class PadDef
    {
        public string id, kind, st, role, b, rack, name, icon;
        public int lane;
        public float cost;
        public string[] req;
        public Vector2? pos;
    }
    public class CarDef
    {
        public string id, name;
        public float pay, pat, len, dirt = 1f;
        public bool honky, vip, special;
    }
    public class LocDef
    {
        public string id, name, theme, landmark, region, blurb;
        public double mult;
        public float spawn;
        public Dictionary<string, float> cars;
    }
    /// <summary>Gem upgrade at HQ (the Empire panel): permanent, works in every city. Same {0} text scheme as BizDef.</summary>
    public class HqUpDef { public string id, name, icon, text; public int max; public Func<int, int> cost; public Func<int, string> num; }
    public class HatDef { public string id, name; public int cost; }
    /// <summary>A permanent perk each city unlocks the first time you open it (some also give a hat). Works in every city.</summary>
    public class PerkDef { public string city, name, icon, desc, hat; }
    public class IapDef { public string id, name, price, icon, perk, desc; public int gems; }
    public class CashPackDef { public string id, name, icon; public float minutes; public int gems; }
    public class RewardDef { public string kind, boost, label; public float minutes, secs, w; public int n; public bool hat; }

    /// <summary>Every tunable number and every piece of content (ported 1:1 from the web build's config.js).</summary>
    public static class Cfg
    {
        public const string Version = "1.0.0";

        // ---------- layout (web coordinates: +x east, +z south toward the camera) ----------
        public static readonly float[] LaneZ = { 0f, 13f };
        public static readonly string[] SlotOrder = { "brush", "rinse", "wax", "dryer", "tire", "polish", "pay" };
        public static readonly Dictionary<string, float> SlotX = new Dictionary<string, float>
        { { "brush", -12 }, { "rinse", -7 }, { "wax", -2 }, { "dryer", 3 }, { "tire", 8 }, { "polish", 13 }, { "pay", 18 } };
        public const float EntranceX = -16.4f, ExitX = 22.3f;
        public const float HopperDZ = 2.75f, MatDZ = 3.95f, BoothDZ = 2.95f, CashierDZ = 3.15f, CashDX = 2.7f, CashDZ = 3.55f, PolishDZ = 1.85f;
        public static readonly Rect LotBounds = Rect.MinMaxRect(-25f, -13.6f, 27.5f, 23.5f);   // x / z
        public static readonly Rect WalkBounds = Rect.MinMaxRect(-24.4f, -13f, 27f, 23f);
        public static readonly Rect CamBounds = Rect.MinMaxRect(-21f, -8f, 24f, 19f);
        public const float SpawnX = -95f, DespawnX = 95f, RoadSpeed = 10f, LineSpeed = 3.6f;
        public const int HopperCap = 8;
        public static readonly Vector2 PlayerStart = new Vector2(-9f, 5f);
        public static readonly Dictionary<string, Vector2> RacksPos = new Dictionary<string, Vector2>
        { { "soap", new Vector2(-12f, 7.5f) }, { "wax", new Vector2(-4f, 7.5f) }, { "gel", new Vector2(4f, 7.5f) } };
        public const float RackMatDZ = -1.35f;
        public static readonly Dictionary<string, Vector2> BuildingPos = new Dictionary<string, Vector2>
        {
            { "shop", new Vector2(4.5f, -10f) }, { "staff", new Vector2(-17.5f, -9.6f) }, { "office", new Vector2(-9.5f, -9.6f) },
            { "vip", new Vector2(18.5f, -9.6f) }, { "coffee", new Vector2(-13.4f, -6.6f) }, { "vending", new Vector2(-21f, 4.3f) },
            { "queue", new Vector2(-22f, -4.3f) }, { "tubeman", new Vector2(-20.5f, 6.9f) }, { "fountain", new Vector2(-2.5f, 20.4f) },
            { "billboard", new Vector2(-19f, 20.9f) }, { "flowers", new Vector2(6.5f, 20.4f) }, { "landmark", new Vector2(14.2f, 20.6f) },
            { "travel", new Vector2(22.5f, 20.4f) },
        };
        public static readonly Dictionary<string, Vector2> BuildingMat = new Dictionary<string, Vector2>
        { { "shop", new Vector2(2f, -5.5f) }, { "staff", new Vector2(-17.5f, -5.9f) }, { "office", new Vector2(-9.5f, -6f) } };

        // ---------- wash line ----------
        public static readonly Dictionary<string, StationDef> Stations = new Dictionary<string, StationDef>
        {
            { "brush", new StationDef { id = "brush", name = "Foam Brush", shortName = "Brush", icon = "brush", value = 5, inc = 1, time = 3.0f, supply = "soap", upCost = 30 } },
            { "rinse", new StationDef { id = "rinse", name = "Rinse Arch", shortName = "Water", icon = "water", value = 5, inc = 1, time = 2.4f, upCost = 40 } },
            { "wax", new StationDef { id = "wax", name = "Hot Wax", shortName = "Wax", icon = "wax", value = 10, inc = 2, time = 3.0f, supply = "wax", upCost = 100 } },
            { "dryer", new StationDef { id = "dryer", name = "Turbo Dryer", shortName = "Dryer", icon = "dryer", value = 5, inc = 1, time = 2.4f, upCost = 40 } },
            { "tire", new StationDef { id = "tire", name = "Tire Shine", shortName = "Tires", icon = "tire", value = 10, inc = 2, time = 2.6f, supply = "gel", upCost = 150 } },
            { "polish", new StationDef { id = "polish", name = "Polish Bay", shortName = "Polish", icon = "polish", value = 20, inc = 4, time = 3.5f, op = "polisher", upCost = 300 } },
            { "pay", new StationDef { id = "pay", name = "Pay Booth", shortName = "Cashier", icon = "pay", value = 2, inc = 1, time = 1.25f, op = "cashier", upCost = 60 } },
        };
        public const int StationMaxLv = 30;
        public const float UpGrowth = 1.15f;
        /// <summary>New machine paint at these levels (the last one is the max).</summary>
        public static readonly int[] PaintLevels = { 5, 10, 20, 30 };

        /// <summary>
        /// Economy tuning, checked with .offline/econ-sim.js. Built for exploring: a greedy player without ads opens
        /// the first Road Trip after ~18 min and every later city after ~10 min (about half that with 2x income
        /// running). Finishing every pad in a city is optional and pays gems. Every price is a round number:
        /// pads are round, cities multiply by 1, 2, 5, 10, 20, 50 ... and upgrade costs round to two digits.
        /// </summary>
        public static class Econ
        {
            /// <summary>Starting cash in a city you open for the first time (x the city price).</summary>
            public const float WelcomeCash = 300f;
            /// <summary>Already built when you open any city after the first, so you skip straight to the fun part.</summary>
            public static readonly string[] FranchiseKit = { "rinse", "cashier1", "dryer", "staff", "stocker1", "office" };
            /// <summary>Gems for finishing every pad in a city: base + per city along the tour.</summary>
            public static int CompleteGems(int cityIndex) => 25 + 10 * cityIndex;
            public const float BranchBaseEff = 0.25f;   // share of a city's income it keeps earning after you move on
            /// <summary>Offline and branch earnings pile up for this long (+1h per Night Shift level).</summary>
            public const float AwayHours = 4f;
        }

        public static readonly Dictionary<string, SupplyDef> Supplies = new Dictionary<string, SupplyDef>
        {
            { "soap", new SupplyDef { id = "soap", name = "Soap", icon = "soap", color = 0xff6fb1 } },
            { "wax", new SupplyDef { id = "wax", name = "Wax", icon = "wax", color = 0xffc93c } },
            { "gel", new SupplyDef { id = "gel", name = "Tire Gel", icon = "gel", color = 0x3ad17a } },
        };
        public static readonly Dictionary<string, RackDef> Racks = new Dictionary<string, RackDef>
        {
            { "soap", new RackDef { id = "soap", name = "Soap Rack", supply = "soap", time = 1.6f, cap = 8 } },
            { "wax", new RackDef { id = "wax", name = "Wax Rack", supply = "wax", time = 1.8f, cap = 8 } },
            { "gel", new RackDef { id = "gel", name = "Gel Rack", supply = "gel", time = 1.8f, cap = 8 } },
        };

        // ---------- business upgrades (cash, kept for good, every city) ----------
        public static readonly List<BizDef> Biz = new List<BizDef>
        {
            new BizDef { id = "speed", name = "Faster Team", icon = "shoe", max = 10, cost = 150, growth = 1.6f, text = "Everyone walks {0} faster", num = l => $"+{l * 5}%" },
            new BizDef { id = "carry", name = "Carry More", icon = "soap", max = 10, cost = 150, growth = 1.6f, text = "Carry {0} items (+1 for staff too)", num = l => (4 + l).ToString() },
            new BizDef { id = "cars", name = "More Customers", icon = "megaphone", max = 10, cost = 200, growth = 1.6f, text = "{0} cars a minute", num = l => Game.CarsPerMin(l).ToString() },
            new BizDef { id = "price", name = "Higher Prices", icon = "income", max = 10, cost = 300, growth = 1.6f, text = "Cars pay {0}", num = l => $"+{l * 10}%" },
        };
        public static BizDef BizById(string id) => Biz.Find(t => t.id == id);

        // ---------- build pads (costs are multiplied by the location factor) ----------
        static PadDef Pad(string id, string kind, float cost, string[] req, string st = null, int lane = 0, string role = null, string b = null, string rack = null, string name = null, string icon = null, float? px = null, float? pz = null)
            => new PadDef { id = id, kind = kind, cost = cost, req = req, st = st, lane = lane, role = role, b = b, rack = rack, name = name, icon = icon, pos = px.HasValue ? new Vector2(px.Value, pz.Value) : (Vector2?)null };
        static string[] R(params string[] r) => r;
        public static readonly List<PadDef> Pads = new List<PadDef>
        {
            // the main road: one full wash line, then the Road Trip to the next city
            Pad("rinse", "station", 20, R(), st: "rinse"),
            Pad("cashier1", "hire", 50, R("rinse"), role: "cashier", px: 15.2f, pz: 5.4f),
            Pad("dryer", "station", 100, R("cashier1"), st: "dryer"),
            Pad("staff", "building", 150, R("dryer"), b: "staff", name: "Staff Room", icon: "staffroom", px: -17.5f, pz: -6.4f),
            Pad("stocker1", "hire", 150, R("staff"), role: "stocker", px: -8f, pz: 5f),
            Pad("office", "building", 250, R("stocker1"), b: "office", name: "Manager Office", icon: "office", px: -9.5f, pz: -6.6f),
            Pad("waxRack", "rack", 300, R("office"), rack: "wax"),
            Pad("wax", "station", 400, R("waxRack"), st: "wax"),
            Pad("gelRack", "rack", 600, R("wax"), rack: "gel"),
            Pad("tire", "station", 800, R("gelRack"), st: "tire"),
            // optional extras: more income, a stronger branch, and the City Complete reward
            Pad("queue", "building", 300, R("office"), b: "queue", name: "Waiting Area", icon: "queue", px: -22f, pz: -4.3f),
            Pad("tubeman", "building", 500, R("wax"), b: "tubeman", name: "Wacky Tube Man", icon: "tubeman", px: -20.5f, pz: 6.9f),
            Pad("vending", "building", 600, R("wax"), b: "vending", name: "Vending Machine", icon: "vending", px: -21f, pz: 4.3f),
            Pad("stocker2", "hire", 1000, R("tire"), role: "stocker", px: -8f, pz: 5f),
            Pad("fountain", "building", 1200, R("stocker2"), b: "fountain", name: "Fountain", icon: "fountain", px: -2.5f, pz: 20.4f),
            Pad("lane2", "lane", 1500, R("tire"), lane: 1, name: "Open Lane 2", icon: "lane", px: -12f, pz: 13f),
            Pad("l2rinse", "station", 800, R("lane2"), st: "rinse", lane: 1),
            Pad("cashier2", "hire", 1000, R("lane2"), role: "cashier", lane: 1, px: 15.2f, pz: 18.4f),
            Pad("l2dryer", "station", 1000, R("l2rinse"), st: "dryer", lane: 1),
            Pad("billboard", "building", 2000, R("l2dryer"), b: "billboard", name: "Billboard", icon: "billboard", px: -19f, pz: 20.9f),
            Pad("l2wax", "station", 2000, R("l2dryer"), st: "wax", lane: 1),
            Pad("l2tire", "station", 2500, R("l2wax"), st: "tire", lane: 1),
            Pad("polish", "station", 3000, R("tire"), st: "polish"),
            Pad("polisher1", "hire", 3000, R("polish"), role: "polisher", px: 10.6f, pz: 5.3f),
            Pad("coffee", "building", 3500, R("polisher1"), b: "coffee", name: "Coffee Machine", icon: "coffee", px: -13.4f, pz: -6.6f),
            Pad("stocker3", "hire", 4000, R("coffee"), role: "stocker", px: -8f, pz: 5f),
            Pad("l2polish", "station", 5000, R("polisher1", "l2tire"), st: "polish", lane: 1),
            Pad("polisher2", "hire", 5000, R("l2polish"), role: "polisher", lane: 1, px: 10.6f, pz: 18.3f),
            Pad("vip", "building", 7500, R("polisher2"), b: "vip", name: "VIP Lounge", icon: "vip", px: 18.5f, pz: -6.2f),
            Pad("flowers", "building", 10000, R("vip"), b: "flowers", name: "Flower Beds", icon: "flowers", px: 6.5f, pz: 20.4f),
            Pad("landmark", "building", 15000, R("flowers"), b: "landmark", name: "Landmark", icon: "landmark", px: 14.2f, pz: 20.6f),
            // the next city opens as soon as the first lane has its Tire Shine
            Pad("travel", "travel", 1500, R("tire"), name: "Road Trip", icon: "road", px: 22.5f, pz: 20.4f),
        };
        public static PadDef PadById(string id) => Pads.Find(p => p.id == id);

        public static readonly Dictionary<string, string> BuildingDesc = new Dictionary<string, string>
        {
            { "queue", "+3 waiting spots, +5s patience" }, { "tubeman", "+18% more cars" }, { "vending", "Waiting drivers buy snacks" },
            { "fountain", "+10% tip chance" }, { "billboard", "+25% more cars" }, { "coffee", "Staff nap half as much, +10% speed" },
            { "vip", "VIP cars start arriving" }, { "flowers", "+20% driver patience" }, { "landmark", "+25% all income" },
            { "staff", "Hire staff here" }, { "office", "Unlocks Business upgrades" },
        };
        public static readonly Dictionary<string, string[]> HireNames = new Dictionary<string, string[]>
        {
            { "stocker", new[] { "Bubbles Bob", "Soapy Sue", "Rinse Ricky", "Foamy Fran", "Squeaky Sam", "Sudsy Steve" } },
            { "cashier", new[] { "Cash Carla", "Penny Pete", "Change Chad" } },
            { "polisher", new[] { "Polly Shine", "Glossy Gus", "Buff Betty" } },
        };

        // ---------- customers ----------
        public static readonly Dictionary<string, CarDef> Cars = new Dictionary<string, CarDef>
        {
            { "sedan", new CarDef { id = "sedan", name = "Sedan", pay = 1.0f, pat = 30, len = 3.6f } },
            { "hatch", new CarDef { id = "hatch", name = "Hatchback", pay = 0.9f, pat = 34, len = 3.2f } },
            { "pickup", new CarDef { id = "pickup", name = "Pickup", pay = 1.2f, pat = 30, len = 4.0f, dirt = 1.5f } },
            { "taxi", new CarDef { id = "taxi", name = "Taxi", pay = 1.1f, pat = 20, len = 3.6f, honky = true } },
            { "police", new CarDef { id = "police", name = "Police Car", pay = 1.5f, pat = 18, len = 3.6f, honky = true } },
            { "van", new CarDef { id = "van", name = "Van", pay = 1.3f, pat = 30, len = 3.9f } },
            { "suv", new CarDef { id = "suv", name = "SUV", pay = 1.4f, pat = 30, len = 3.9f } },
            { "icecream", new CarDef { id = "icecream", name = "Ice Cream Truck", pay = 1.6f, pat = 30, len = 4.0f } },
            { "pizza", new CarDef { id = "pizza", name = "Pizza Delivery", pay = 1.5f, pat = 22, len = 3.2f, honky = true } },
            { "hotdog", new CarDef { id = "hotdog", name = "Hot Dog Car", pay = 1.8f, pat = 40, len = 4.1f } },
            { "duck", new CarDef { id = "duck", name = "Duck Car", pay = 2.0f, pat = 40, len = 3.5f } },
            { "buggy", new CarDef { id = "buggy", name = "Beach Buggy", pay = 1.3f, pat = 32, len = 3.2f, dirt = 1.3f } },
            { "tractor", new CarDef { id = "tractor", name = "Tractor", pay = 1.5f, pat = 36, len = 3.6f, dirt = 2f } },
            { "snowplow", new CarDef { id = "snowplow", name = "Snowplow", pay = 1.6f, pat = 30, len = 4.2f } },
            { "rover", new CarDef { id = "rover", name = "Moon Rover", pay = 1.7f, pat = 32, len = 3.8f, dirt = 1.4f } },
            { "ufo", new CarDef { id = "ufo", name = "UFO", pay = 2.2f, pat = 26, len = 3.6f } },
            { "limo", new CarDef { id = "limo", name = "Limo", pay = 4.0f, pat = 40, len = 5.4f, vip = true } },
            { "sports", new CarDef { id = "sports", name = "Sports Car", pay = 3.5f, pat = 20, len = 3.9f, vip = true } },
            { "monster", new CarDef { id = "monster", name = "Monster Truck", pay = 3.0f, pat = 30, len = 4.3f, vip = true, dirt = 2f } },
            { "golden", new CarDef { id = "golden", name = "Golden Car", pay = 3.0f, pat = 70, len = 3.6f, special = true } },
        };
        public static readonly uint[] PaintColors = { 0xe8383d, 0xffc93c, 0x3c7be8, 0xf5f6fa, 0x46c23a, 0xff8a1f, 0x8a4dff, 0x2fc6c6, 0x2a2e36, 0xff6fb1, 0x9aa6b8 };
        public static readonly Dictionary<string, uint> PaintFor = new Dictionary<string, uint>
        {
            { "taxi", 0xffc93c }, { "police", 0xf5f6fa }, { "icecream", 0xf5f6fa }, { "limo", 0x22252c }, { "duck", 0xffd23f }, { "hotdog", 0xd9a066 },
            { "ufo", 0xc8d0dc }, { "rover", 0xf2f4f8 }, { "snowplow", 0xff8a1f }, { "tractor", 0x46c23a }, { "golden", 0xffc92e }, { "pizza", 0xe8383d },
        };

        // ---------- locations ----------
        static Dictionary<string, float> W(params object[] kv) { var d = new Dictionary<string, float>(); for (int i = 0; i < kv.Length; i += 2) d[(string)kv[i]] = Convert.ToSingle(kv[i + 1]); return d; }
        // Mixes reused by several cities
        static Dictionary<string, float> TownCars() => W("sedan", 30, "hatch", 22, "pickup", 12, "taxi", 10, "police", 6, "van", 9, "suv", 9, "icecream", 3, "pizza", 4, "hotdog", 2, "duck", 2);
        static Dictionary<string, float> BeachCars() => W("sedan", 20, "hatch", 14, "buggy", 16, "van", 10, "icecream", 10, "suv", 10, "taxi", 6, "pizza", 4, "duck", 4, "hotdog", 4);
        static Dictionary<string, float> DesertCars() => W("pickup", 24, "tractor", 14, "sedan", 14, "suv", 14, "van", 8, "buggy", 8, "monster", 3, "hotdog", 3, "duck", 2);
        static Dictionary<string, float> SnowCars() => W("suv", 22, "snowplow", 16, "sedan", 16, "pickup", 12, "van", 10, "taxi", 6, "icecream", 2, "duck", 3, "hotdog", 3);
        static Dictionary<string, float> CityCars() => W("sedan", 18, "taxi", 18, "sports", 6, "limo", 4, "police", 10, "hatch", 12, "van", 8, "pizza", 8, "duck", 3, "hotdog", 3);
        static Dictionary<string, float> SpaceCars() => W("rover", 30, "ufo", 22, "sedan", 8, "buggy", 10, "duck", 4, "hotdog", 4, "sports", 3);
        static LocDef City(string id, string name, string region, string theme, string landmark, double mult, float spawn, Dictionary<string, float> cars, string blurb)
            => new LocDef { id = id, name = name, region = region, theme = theme, landmark = landmark, mult = mult, spawn = spawn, cars = cars, blurb = blurb };

        /// <summary>Regions in travel order (headers on the world map).</summary>
        public static readonly string[] Regions = { "Hometown", "USA", "Europe", "Asia", "Around the World", "Space" };

        /// <summary>The world tour: city prices go x1, x2, x5, x10, x20, x50 ... Ids of the first release (suburb, beach, desert, snow, city, moon) are kept for old saves.</summary>
        public static readonly List<LocDef> Locations = new List<LocDef>
        {
            City("suburb", "Sunny Suburb", "Hometown", "suburb", "goldcar", 1, 5.6f, TownCars(), "Where it all began: one brush and a dream."),
            City("downtown", "Downtown", "Hometown", "metro", "neontower", 2, 5.5f, CityCars(), "Office towers, taxis and busy commuters."),
            City("beach", "Palm Beach", "USA", "beach", "sandcastle", 5, 5.8f, BeachCars(), "Sandy buggies and melting ice cream trucks."),
            City("desert", "Route 66", "USA", "desert", "oasis", 10, 5.6f, DesertCars(), "Dusty pickups straight off the highway."),
            City("snow", "Snowy Peaks", "USA", "snow", "snowman", 20, 5.4f, SnowCars(), "Salt, slush and snowplows."),
            City("city", "Las Vegas", "USA", "city", "neontower", 50, 5.2f, CityCars(), "Limos and sports cars all night long."),
            City("london", "London", "Europe", "rain", "goldcar", 100, 5.4f, CityCars(), "It always rains, so every car needs a wash."),
            City("paris", "Paris", "Europe", "paris", "neontower", 200, 5.3f, TownCars(), "Tiny cars, big style."),
            City("alps", "Swiss Alps", "Europe", "alps", "snowman", 500, 5.4f, SnowCars(), "Mountain roads, very muddy tyres."),
            City("dubai", "Dubai", "Asia", "dubai", "oasis", 1000, 5.0f, W("sports", 10, "limo", 8, "suv", 20, "sedan", 16, "taxi", 12, "pickup", 8, "monster", 4, "van", 6, "duck", 2), "Gold cars, desert towers, VIPs everywhere."),
            City("mumbai", "Mumbai", "Asia", "mumbai", "goldcar", 2000, 5.0f, W("taxi", 24, "hatch", 20, "sedan", 16, "van", 10, "pickup", 8, "pizza", 8, "hotdog", 4, "duck", 3), "The busiest roads on Earth."),
            City("tokyo", "Tokyo", "Asia", "tokyo", "neontower", 5000, 5.0f, W("hatch", 22, "taxi", 18, "sedan", 14, "sports", 8, "police", 8, "pizza", 8, "duck", 6, "ufo", 2), "Neon nights and spotless streets."),
            City("sydney", "Sydney", "Around the World", "sunset", "sandcastle", 10000, 5.2f, BeachCars(), "Surfers, utes and sunset rush hours."),
            City("rio", "Rio de Janeiro", "Around the World", "tropic", "sandcastle", 20000, 5.1f, BeachCars(), "Carnival floats need a wash too."),
            City("cairo", "Cairo", "Around the World", "cairo", "oasis", 50000, 5.1f, DesertCars(), "Sand in every corner of every car."),
            City("moon", "Moon Base", "Space", "moon", "rocket", 100000, 5.0f, SpaceCars(), "Moon dust gets everywhere."),
            City("mars", "Mars Colony", "Space", "mars", "rocket", 200000, 4.9f, SpaceCars(), "The first car wash on another planet."),
        };
        public static int LocIndex(string id) => Locations.FindIndex(l => l.id == id);

        /// <summary>Every city after the first adds one new permanent perk, so each stop on the tour changes how you play.</summary>
        public static readonly List<PerkDef> Perks = new List<PerkDef>
        {
            new PerkDef { city = "downtown", name = "Cash Magnet", icon = "cash", desc = "Grab cash from twice as far away", hat = "cone" },
            new PerkDef { city = "beach", name = "Beach Tips", icon = "heart", desc = "+10% tip chance in every city" },
            new PerkDef { city = "desert", name = "Highway Traffic", icon = "car", desc = "+10% more customers everywhere", hat = "cowboy" },
            new PerkDef { city = "snow", name = "Hot Cocoa", icon = "coffee", desc = "Your staff almost never nap" },
            new PerkDef { city = "city", name = "Jackpot", icon = "golden", desc = "Golden cars come twice as often" },
            new PerkDef { city = "london", name = "Double-Decker Queue", icon = "queue", desc = "+3 waiting spots in every city" },
            new PerkDef { city = "paris", name = "Fancy Clientele", icon = "vip", desc = "VIP cars visit every city", hat = "chef" },
            new PerkDef { city = "alps", name = "Swiss Precision", icon = "clock", desc = "Every machine works 15% faster" },
            new PerkDef { city = "dubai", name = "Gold Rush", icon = "golden", desc = "Golden cars pay x5 instead of x3" },
            new PerkDef { city = "mumbai", name = "Rush Hour Fever", icon = "rush", desc = "Rush hour comes twice as often" },
            new PerkDef { city = "tokyo", name = "Bullet Shoes", icon = "shoe", desc = "You walk 20% faster" },
            new PerkDef { city = "sydney", name = "Surf's Up", icon = "clock", desc = "+50% offline earnings" },
            new PerkDef { city = "rio", name = "Carnival", icon = "wheel", desc = "A free Lucky Spin every 2 hours", hat = "duck" },
            new PerkDef { city = "cairo", name = "Pharaoh's Treasure", icon = "trophy", desc = "Quests pay double cash" },
            new PerkDef { city = "moon", name = "Low Gravity", icon = "soap", desc = "Staff carry 2 more items" },
            new PerkDef { city = "mars", name = "Red Planet Riches", icon = "income", desc = "+50% income in every city", hat = "crown" },
        };
        public static PerkDef PerkFor(string city) => Perks.Find(p => p.city == city);

        // ---------- player level ----------
        public static int XpFor(int lvl) => 60 * lvl;
        public static int LevelGems(int lvl) => 5 + lvl;
        public static readonly Dictionary<string, int> Unlocks = new Dictionary<string, int>
        { { "spin", 2 }, { "shop", 3 }, { "gift", 3 }, { "blaster", 4 }, { "album", 5 }, { "golden", 4 }, { "rush", 6 }, { "daily", 2 } };

        // ---------- meta ----------
        /// <summary>Every gem upgrade, in one place: HQ in the Empire panel. Levels live in SaveData (see SaveData.Hq).</summary>
        public static readonly List<HqUpDef> HqUps = new List<HqUpDef>
        {
            new HqUpDef { id = "sponge", name = "Golden Sponge", icon = "sparkles", max = 10, cost = n => 20 + n * 15, text = "Income {0} in every city", num = n => $"+{n * 10}%" },
            new HqUpDef { id = "franchise", name = "Franchise Managers", icon = "manager", max = 10, cost = n => 30 + n * 25, text = "Branches keep {0} of their income", num = n => $"{Mathf.RoundToInt((Econ.BranchBaseEff + 0.05f * n) * 100)}%" },
            new HqUpDef { id = "offline", name = "Night Shift", icon = "clock", max = 8, cost = n => 15 + n * 12, text = "Earnings pile up for {0} while you're away", num = n => $"{Econ.AwayHours + n}h" },
            new HqUpDef { id = "tickets", name = "Ticket Box", icon = "blaster", max = 3, cost = n => 25 + n * 20, text = "Hold {0} Bubble Blaster tickets", num = n => (3 + n).ToString() },
        };
        /// <summary>HQ upgrades from v2 saves that no longer exist (gems are refunded): id -> cost of level n.</summary>
        public static readonly Dictionary<string, Func<int, int>> RetiredHq = new Dictionary<string, Func<int, int>>
        { { "vault", n => 25 + n * 20 }, { "marketing", n => 30 + n * 25 }, { "logistics", n => 20 + n * 18 }, { "academy", n => 20 + n * 18 } };

        public static readonly List<HatDef> Hats = new List<HatDef>
        {
            new HatDef { id = "cap", name = "Star Cap", cost = 0 }, new HatDef { id = "cone", name = "Traffic Cone", cost = 40 },
            new HatDef { id = "cowboy", name = "Cowboy Hat", cost = 80 }, new HatDef { id = "chef", name = "Chef Hat", cost = 120 },
            new HatDef { id = "duck", name = "Rubber Duck", cost = 180 }, new HatDef { id = "bubble", name = "Bubble Helmet", cost = 260 },
            new HatDef { id = "crown", name = "Royal Crown", cost = 400 },
        };
        /// <summary>Purchases are simulated (no Google Play Billing yet), so paid items only appear in the Editor and development builds.</summary>
        public static bool IapEnabled => Application.isEditor || Debug.isDebugBuild;
        public static readonly List<IapDef> Iap = new List<IapDef>
        {
            new IapDef { id = "gems_s", name = "Pile of Gems", gems = 80, price = "$0.99", icon = "gem" },
            new IapDef { id = "gems_m", name = "Bag of Gems", gems = 500, price = "$4.99", icon = "gem" },
            new IapDef { id = "gems_l", name = "Chest of Gems", gems = 1200, price = "$9.99", icon = "gem" },
            new IapDef { id = "starter", name = "Starter Pack", gems = 150, perk = "starter", price = "$2.99", icon = "gift", desc = "150 gems + permanent +50% income" },
            new IapDef { id = "noads", name = "Remove Ads", perk = "noads", price = "$2.99", icon = "ad", desc = "No banners or breaks. Rewards stay." },
            new IapDef { id = "vip", name = "VIP Pass", perk = "vip", price = "$7.99", icon = "crown", desc = "Permanent 2x income and no ads" },
        };
        public static readonly List<CashPackDef> CashPacks = new List<CashPackDef>
        {
            new CashPackDef { id = "cash1", name = "Wallet", minutes = 10, gems = 10, icon = "cash" },
            new CashPackDef { id = "cash2", name = "Briefcase", minutes = 45, gems = 35, icon = "office" },
            new CashPackDef { id = "cash3", name = "Vault", minutes = 180, gems = 110, icon = "trophy" },
        };
        public static readonly List<RewardDef> Daily = new List<RewardDef>
        {
            new RewardDef { kind = "cash", minutes = 5 }, new RewardDef { kind = "gems", n = 5 }, new RewardDef { kind = "boost", boost = "income", secs = 900 },
            new RewardDef { kind = "cash", minutes = 12 }, new RewardDef { kind = "gems", n = 10 }, new RewardDef { kind = "spins", n = 2 },
            new RewardDef { kind = "gems", n = 30, hat = true },
        };
        public static readonly List<RewardDef> Spin = new List<RewardDef>
        {
            new RewardDef { kind = "cash", minutes = 2, w = 26, label = "Cash" }, new RewardDef { kind = "gems", n = 3, w = 18, label = "3 Gems" },
            new RewardDef { kind = "cash", minutes = 6, w = 16, label = "Cash x3" }, new RewardDef { kind = "boost", boost = "speed", secs = 180, w = 12, label = "Speed Shoes" },
            new RewardDef { kind = "cash", minutes = 12, w = 9, label = "Big Cash" }, new RewardDef { kind = "boost", boost = "income", secs = 300, w = 10, label = "2x Income" },
            new RewardDef { kind = "gems", n = 12, w = 6, label = "12 Gems" }, new RewardDef { kind = "cash", minutes = 40, w = 3, label = "JACKPOT" },
        };
        public const float SpinFreeEvery = 4 * 3600;

        // ---------- ads & boosts ----------
        public static class Ads
        {
            public const string Provider = "mock";      // mock | levelplay | admob
            public const float InterstitialCooldown = 120, SessionGrace = 180, AfterRewardedGrace = 90;
            public const bool Banner = true;
            public const float MockRewardSecs = 5, MockInterSecs = 3;
            public const int LimitFreeCash = 10, LimitSpin = 5, LimitFreeGems = 3, LimitTicket = 3, LimitBlasterX3 = 3;
            public const float FreeUpgradeEvery = 180, TurboEvery = 300;
        }
        public static readonly Dictionary<string, Vector2> Boosts = new Dictionary<string, Vector2>
        {
            { "income", new Vector2(600, 14400) },   // 2x income: +10 min per ad, stacks to 4 h
            { "wash", new Vector2(300, 1800) },      // Turbo Wash: machines 2x faster
            { "rush", new Vector2(180, 900) },       // Car Rush: 3x customers
            { "speed", new Vector2(180, 1800) },     // speed shoes (spin / daily rewards)
            { "turbo", new Vector2(180, 180) },      // Turbo Tom helper
        };   // add, max (seconds)
        public const float OfflineEfficiency = 0.4f;
        public static readonly Vector2 GiftEvery = new Vector2(150, 240), GoldenEvery = new Vector2(240, 420), RushEvery = new Vector2(360, 600);
        public const float RushSecs = 45;
        public const float TicketEvery = 20 * 60;
        public const int BaseTickets = 3;

        // ---------- words ----------
        public static readonly string[] CleanWords = { "SQUEAKY CLEAN!", "SPARKLING!", "SO SHINY!", "WOW!", "GLEAMING!", "SPOTLESS!", "FRESH!" };
        public static readonly string[] DriverLines =
        {
            "My mom drove through a swamp.", "Is this the drive-thru?", "Make it SHINY!", "Don't scratch my baby!",
            "I have a date tonight!", "A bird attacked me. Twice.", "Do you do tires?", "Smells like nachos in here.",
            "My kid ate a crayon back there.", "Mud? What mud?", "Hurry, my ice cream is melting!", "Can I keep the foam?",
            "I took a shortcut through a farm.", "Is the soap organic?", "Five stars if it sparkles.", "Honk if you love bubbles!",
            "Last time it was purple...", "I washed it in 2009.", "Please be gentle, it is a rental.", "Are the brushes friendly?",
        };
        public static readonly string[] RagePuns = { "RAGE QUIT!", "TOO SLOW!", "NOPE!", "BYE!" };
    }
}
