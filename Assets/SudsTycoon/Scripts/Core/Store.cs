using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace SudsTycoon
{
    [Serializable]
    public class QuestDef
    {
        public string k, id, st, text, icon;
        public double n;
        public int lv;
        public QuestDef Clone() => (QuestDef)MemberwiseClone();
    }

    [Serializable]
    public class LocSave
    {
        public HashSet<string> pads = new HashSet<string>();
        public Dictionary<string, double> paid = new Dictionary<string, double>();
        public Dictionary<string, int> st = new Dictionary<string, int> { { "brush", 1 }, { "rinse", 1 }, { "wax", 1 }, { "dryer", 1 }, { "tire", 1 }, { "polish", 1 }, { "pay", 1 } };
        public Dictionary<string, int> racks = new Dictionary<string, int> { { "soap", 1 }, { "wax", 1 }, { "gel", 1 } };
        public Dictionary<string, int> tr = new Dictionary<string, int>();
        public int quest;
        public QuestDef qDef;
        public Dictionary<string, double> qBase;
        public Dictionary<string, double> cnt = new Dictionary<string, double>();
        public double cars, earned, tips, vip;
        public double? cash;
        public bool started;
        public Dictionary<string, int> hoppers = new Dictionary<string, int>();
        public Dictionary<string, int> rackStock = new Dictionary<string, int>();
        public Dictionary<string, double> piles = new Dictionary<string, double>();
        public Dictionary<string, double> trays = new Dictionary<string, double>();
        /// <summary>Income per second this city keeps making as a branch while you run another city.</summary>
        public double branchRate;
        /// <summary>When its branch earnings were last collected (unix ms).</summary>
        public long lastCollect;
        /// <summary>Every pad built and the City Complete reward given.</summary>
        public bool completed;
    }

    [Serializable] public class GemUpSave { public int sponge, offline, tickets; }
    [Serializable] public class HatSave { public List<string> owned = new List<string> { "cap" }; public string on = "cap"; }
    [Serializable] public class PerkSave { public bool noads, starter, vip; }
    [Serializable] public class DailySave { public int day; public string last = ""; }
    [Serializable] public class SpinSave { public long lastFree; public int tokens = 1; public float angle; }
    [Serializable] public class BlasterSave { public int level = 1, tickets = 3; public long lastTick; }
    [Serializable] public class BoostSave { public long income, speed, turbo, wash, rush; }
    [Serializable] public class AdCapSave { public string day = ""; public int freeCash, freeGems, ticket, blasterX3, spin; }
    [Serializable] public class CoolSave { public long freeUpgrade, turbo; }
    [Serializable] public class StatSave { public double cars, earned, tips, rage, vip, playSecs, blasterWins, upgrades, collects; }
    [Serializable] public class SettingSave { public bool sfx = true, music = true, vib = true; public string quality = "high"; }

    [Serializable]
    public class SaveData
    {
        public int v = 3;
        public long created, lastSeen;
        public int sessions;
        public double cash, gems = 15, xp;
        public int level = 1;
        public int loc, unlockedLocs = 1;
        public Dictionary<string, LocSave> locs = new Dictionary<string, LocSave>();
        public GemUpSave gemUp = new GemUpSave();
        public HatSave hats = new HatSave();
        public PerkSave perks = new PerkSave();
        public DailySave daily = new DailySave();
        public SpinSave spin = new SpinSave();
        public BlasterSave blaster = new BlasterSave();
        public Dictionary<string, int> album = new Dictionary<string, int>();
        public bool albumDone;
        public BoostSave boosts = new BoostSave();
        public AdCapSave adCaps = new AdCapSave();
        public CoolSave cool = new CoolSave();
        public StatSave stats = new StatSave();
        public SettingSave settings = new SettingSave();
        /// <summary>HQ upgrade levels that are not in gemUp (franchise).</summary>
        public Dictionary<string, int> empire = new Dictionary<string, int>();
        /// <summary>Business upgrade levels (Cfg.Biz): kept for good, used in every city.</summary>
        public Dictionary<string, int> biz = new Dictionary<string, int>();

        public long Boost(string name)
        {
            switch (name)
            {
                case "income": return boosts.income;
                case "speed": return boosts.speed;
                case "wash": return boosts.wash;
                case "rush": return boosts.rush;
                default: return boosts.turbo;
            }
        }
        public void SetBoost(string name, long until)
        {
            switch (name)
            {
                case "income": boosts.income = until; break;
                case "speed": boosts.speed = until; break;
                case "wash": boosts.wash = until; break;
                case "rush": boosts.rush = until; break;
                default: boosts.turbo = until; break;
            }
        }
        public int Biz(string id) => biz != null && biz.TryGetValue(id, out var v) ? v : 0;
        public void AddBiz(string id) { if (biz == null) biz = new Dictionary<string, int>(); biz[id] = Biz(id) + 1; }
        /// <summary>Level of an HQ (gem) upgrade, Cfg.HqUps.</summary>
        public int Hq(string id)
        {
            switch (id)
            {
                case "sponge": return gemUp.sponge;
                case "offline": return gemUp.offline;
                case "tickets": return gemUp.tickets;
                default: return empire != null && empire.TryGetValue(id, out var v) ? v : 0;
            }
        }
        public void AddHq(string id)
        {
            switch (id)
            {
                case "sponge": gemUp.sponge++; break;
                case "offline": gemUp.offline++; break;
                case "tickets": gemUp.tickets++; break;
                default: if (empire == null) empire = new Dictionary<string, int>(); empire[id] = Hq(id) + 1; break;
            }
        }
    }

    /// <summary>The whole save is one JSON file. Writes are wrapped so a full disk never stops play.</summary>
    public static class Store
    {
        public static SaveData S { get; private set; }
        /// <summary>Set while the editor-only trailer shoot runs: nothing is written to the save file.</summary>
        internal static bool NoWrite;
        static string FilePath => Path.Combine(Application.persistentDataPath, "suds_tycoon_save.json");
        static readonly JsonSerializerSettings Json = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, ObjectCreationHandling = ObjectCreationHandling.Replace };

        public static bool Load()
        {
            SaveData data = null;
            try { if (File.Exists(FilePath)) data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(FilePath), Json); }
            catch (Exception e) { Debug.LogWarning("[save] could not read save, starting fresh: " + e.Message); }
            bool fresh = data == null;
            if (fresh) { data = new SaveData(); data.created = data.lastSeen = U.NowMs; data.blaster.lastTick = U.NowMs; }
            Migrate(data);
            S = data;
            return fresh;
        }
        static void Migrate(SaveData d)
        {
            if (d.empire == null) d.empire = new Dictionary<string, int>();
            if (d.biz == null) d.biz = new Dictionary<string, int>();
            // v1 saves had six cities; the world tour inserts new ones between them, so remap by city id
            if (d.v < 2)
            {
                string[] old = { "suburb", "beach", "desert", "snow", "city", "moon" };
                int cur = d.loc >= 0 && d.loc < old.Length ? Cfg.LocIndex(old[d.loc]) : 0;
                int maxIdx = 0;
                for (int i = 0; i < Math.Min(d.unlockedLocs, old.Length); i++) maxIdx = Math.Max(maxIdx, Cfg.LocIndex(old[i]));
                d.loc = Math.Max(0, cur);
                d.unlockedLocs = Math.Max(d.loc + 1, maxIdx + 1);
                d.v = 2;
            }
            // v2 had eleven per-city upgrade tracks and five HQ upgrades; v3 keeps four business upgrades for good
            if (d.v < 3)
            {
                int Max(string a, string b = null)
                {
                    int best = 0;
                    foreach (var ls in d.locs.Values)
                    {
                        if (ls?.tr == null) continue;
                        if (ls.tr.TryGetValue(a, out var x)) best = Math.Max(best, x);
                        if (b != null && ls.tr.TryGetValue(b, out var y)) best = Math.Max(best, y);
                    }
                    return best;
                }
                d.biz["speed"] = Max("mgrSpeed", "stfSpeed");
                d.biz["carry"] = Max("mgrCarry", "stfCarry");
                d.biz["cars"] = Max("mktArrive");
                d.biz["price"] = Max("mktPrice");
                foreach (var b in Cfg.Biz) d.biz[b.id] = Mathf.Clamp(d.Biz(b.id), 0, b.max);
                foreach (var kv in Cfg.RetiredHq)
                {
                    if (!d.empire.TryGetValue(kv.Key, out var lv)) continue;
                    for (int n = 0; n < lv; n++) d.gems += kv.Value(n);
                    d.empire.Remove(kv.Key);
                }
                // cities opened before the Franchise Kit existed get it now
                foreach (var kv in d.locs)
                    if (kv.Value != null && kv.Value.started && Cfg.LocIndex(kv.Key) > 0)
                        foreach (var id in Cfg.Econ.FranchiseKit) kv.Value.pads.Add(id);
                foreach (var ls in d.locs.Values)
                {
                    if (ls == null) continue;
                    ls.tr?.Clear();
                    var keys = new List<string>(ls.st.Keys);
                    foreach (var k in keys) ls.st[k] = Mathf.Clamp(ls.st[k], 1, Cfg.StationMaxLv);
                    // a quest that pointed at a retired upgrade track becomes a fresh one; level goals fit the new max
                    if (ls.qDef != null && ls.qDef.k == "track") ls.qDef = null;
                    if (ls.qDef != null && ls.qDef.k == "upgrade") ls.qDef.lv = Math.Min(ls.qDef.lv, Cfg.StationMaxLv);
                }
                d.v = 3;
            }
            d.unlockedLocs = Mathf.Clamp(d.unlockedLocs, 1, Cfg.Locations.Count);
            d.loc = Mathf.Clamp(d.loc, 0, d.unlockedLocs - 1);
        }

        public static LocSave Loc(string id)
        {
            if (!S.locs.TryGetValue(id, out var l)) S.locs[id] = l = new LocSave();
            return l;
        }
        public static void Save()
        {
            if (S == null || NoWrite) return;
            S.lastSeen = U.NowMs;
            try { File.WriteAllText(FilePath, JsonConvert.SerializeObject(S, Json)); }
            catch (Exception e) { Debug.LogWarning("[save] write failed: " + e.Message); }
        }
        public static void Wipe()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (Exception) { /* ignore */ }
            S = new SaveData { created = U.NowMs, lastSeen = U.NowMs };
            S.blaster.lastTick = U.NowMs;
        }
        /// <summary>Daily ad caps reset at local midnight.</summary>
        public static AdCapSave Caps()
        {
            var t = U.Today();
            if (S.adCaps.day != t) S.adCaps = new AdCapSave { day = t };
            return S.adCaps;
        }
    }
}
