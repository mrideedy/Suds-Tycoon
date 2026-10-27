// save.js — the whole save is one JSON object in localStorage (wrapped: storage can be blocked).
const Store = (() => {
  let s = null;
  // ?slot=name keeps a separate save (handy for testing without touching the main one)
  const KEY = (() => { try { const q = new URLSearchParams(location.search).get('slot'); return CFG.saveKey + (q ? '.' + q.replace(/[^a-z0-9_-]/gi, '') : ''); } catch (e) { return CFG.saveKey; } })();
  function defaults() {
    const now = Date.now();
    return {
      v: 1, created: now, lastSeen: now, sessions: 0,
      cash: 0, gems: 15, xp: 0, level: 1,
      loc: 0, unlockedLocs: 1,
      locs: {},
      gemUp: { sponge: 0, offline: 0, tickets: 0 },
      hats: { owned: ['cap'], on: 'cap' },
      perks: { noads: false, starter: false, vip: false },
      daily: { day: 0, last: '' },
      spin: { lastFree: 0, adDay: '', adCount: 0, tokens: 1 },
      blaster: { level: 1, tickets: 3, lastTick: now },
      album: {},
      boosts: { income: 0, speed: 0, turbo: 0 },
      adCaps: { day: '', freeCash: 0, freeGems: 0, ticket: 0, blasterX3: 0, spin: 0 },
      cool: { freeUpgrade: 0, turbo: 0 },
      stats: { cars: 0, earned: 0, tips: 0, rage: 0, vip: 0, playSecs: 0, blasterWins: 0, upgrades: 0, collects: 0 },
      settings: { sfx: true, music: true, vib: true, quality: 'high' },
      tut: { done: false },
    };
  }
  function locDefaults() {
    return {
      pads: {}, paid: {},
      st: { brush: 1, rinse: 1, wax: 1, dryer: 1, tire: 1, polish: 1, pay: 1 },
      racks: { soap: 1, wax: 1, gel: 1 },
      tr: {},
      quest: 0, qBase: null,
      cars: 0, earned: 0, tips: 0, vip: 0,
      hoppers: {}, rackStock: {}, piles: {}, trays: {},
    };
  }
  function deepMerge(base, add) {
    if (!add || typeof add !== 'object') return base;
    for (const k of Object.keys(add)) {
      if (add[k] && typeof add[k] === 'object' && !Array.isArray(add[k]) && base[k] && typeof base[k] === 'object' && !Array.isArray(base[k])) deepMerge(base[k], add[k]);
      else base[k] = add[k];
    }
    return base;
  }
  function load(snapshot) {
    let raw = null;
    if (snapshot) raw = snapshot;
    else { try { raw = localStorage.getItem(KEY); } catch (e) { raw = null; } }
    let data = null;
    try { data = raw ? JSON.parse(raw) : null; } catch (e) { data = null; }
    s = deepMerge(defaults(), data || {});
    for (const id of Object.keys(s.locs)) s.locs[id] = deepMerge(locDefaults(), s.locs[id]);
    return { state: s, fresh: !data };
  }
  function loc(id) {
    if (!s.locs[id]) s.locs[id] = locDefaults();
    return s.locs[id];
  }
  function serialize() { s.lastSeen = Date.now(); return JSON.stringify(s); }
  function save() {
    if (!s) return;
    try { localStorage.setItem(KEY, serialize()); } catch (e) { /* storage unavailable: keep playing */ }
  }
  function wipe() {
    try { localStorage.removeItem(KEY); } catch (e) { /* ignore */ }
    s = defaults();
  }
  // Daily ad caps reset at local midnight.
  function caps() {
    const t = U.today();
    if (s.adCaps.day !== t) s.adCaps = { day: t, freeCash: 0, freeGems: 0, ticket: 0, blasterX3: 0, spin: 0 };
    return s.adCaps;
  }
  return { load, save, wipe, loc, serialize, caps, get s() { return s; }, locDefaults };
})();
