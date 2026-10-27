// tools/econ-sim.js — greedy-player economy simulation for one location.
// Run: node tools/econ-sim.js   (reads js/config.js; prints when each pad gets built)
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const ctx = { console, Math };
vm.createContext(ctx);
vm.runInContext(fs.readFileSync(path.join(__dirname, '../js/config.js'), 'utf8') + '\nthis.CFG = CFG;', ctx);
const CFG = ctx.CFG;

const M = Number(process.argv[2] || 1);
const loc = CFG.locations.find(l => l.mult === M) || CFG.locations[0];
const st = { brush: 1, rinse: 1, wax: 1, dryer: 1, tire: 1, polish: 1, pay: 1 };
const tr = {};
const built = new Set();
const trk = (id) => tr[id] || 0;
const has = (id) => built.has(id);

function stationValue(t) { const l = st[t]; return CFG.stations[t].value * (1 + 0.12 * (l - 1)) * Math.pow(1.5, Math.floor(l / 10)); }
function stationTime(t) { const l = st[t]; return Math.max(0.45, CFG.stations[t].time * Math.pow(0.95, l - 1)); }
function laneStations(lane) {
  const out = ['brush', 'pay'];
  if (lane === 1 && !has('lane2')) return [];
  for (const p of CFG.pads) if (p.kind === 'station' && p.lane === lane && has(p.id)) out.push(p.st);
  return out;
}
function income() {
  let iv = loc.spawn * Math.pow(0.92, trk('mktArrive'));
  if (has('tubeman')) iv *= 0.85; if (has('billboard')) iv *= 0.8;
  const arrive = 1 / iv;
  const mult = M * (1 + 0.1 * trk('mktPrice')) * (has('landmark') ? 1.25 : 1);
  let total = 0, left = arrive;
  for (const lane of [0, 1]) {
    const s = laneStations(lane); if (!s.length) continue;
    let cycle = 0, value = 0;
    for (const t of s) { cycle = Math.max(cycle, stationTime(t) + 1.8); if (t !== 'pay') value += stationValue(t); }
    const served = Math.min(left, 1 / cycle); left -= served;
    total += served * value * 1.2;
  }
  // before staff is hired the player has to run the line by hand
  const manual = has('cashier1') ? (has('stocker1') ? 1 : 0.8) : 0.55;
  return total * mult * manual;
}
function upgrades() {
  const out = [];
  for (const t of Object.keys(st)) {
    const exists = t === 'brush' || t === 'pay' || CFG.pads.some(p => p.kind === 'station' && p.st === t && has(p.id));
    if (!exists || st[t] >= CFG.stationMaxLv) continue;
    out.push({ kind: 'st', id: t, cost: Math.round(CFG.stations[t].upCost * M * Math.pow(CFG.upGrowth, st[t] - 1)) });
  }
  for (const id of ['mktArrive', 'mktPrice']) {
    const d = CFG.tracks[id]; if (!has('office') || trk(id) >= d.max) continue;
    out.push({ kind: 'tr', id, cost: Math.round(d.cost * M * Math.pow(d.growth, trk(id))) });
  }
  return out;
}
let cash = 0, t = 0, padIdx = 0, ups = 0;
const pads = CFG.pads.slice();
const log = [];
while (t < 4 * 3600 && built.size < pads.length) {
  cash += income();
  t += 1;
  // buy pads in order when affordable; otherwise the best ROI upgrade that is cheaper than 35% of the next pad
  const next = pads.find(p => !has(p.id) && p.req.every(has));
  if (next && cash >= next.cost * M) { cash -= next.cost * M; built.add(next.id); log.push([t, next.id, next.cost * M, income()]); continue; }
  const base = income();
  let best = null, bestR = 0;
  for (const u of upgrades()) {
    if (next && u.cost > next.cost * M * 0.35) continue;
    if (u.cost > cash) continue;
    const save = u.kind === 'st' ? st[u.id] : tr[u.id];
    if (u.kind === 'st') st[u.id]++; else tr[u.id] = trk(u.id) + 1;
    const r = (income() - base) / u.cost;
    if (u.kind === 'st') st[u.id] = save; else tr[u.id] = save;
    if (r > bestR) { bestR = r; best = u; }
  }
  if (best) { cash -= best.cost; if (best.kind === 'st') st[best.id]++; else tr[best.id] = trk(best.id) + 1; ups++; }
}
const mm = (s) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
let prev = 0;
for (const [tt, id, c, inc] of log) { console.log(`${mm(tt).padStart(6)}  +${String(tt - prev).padStart(4)}s  ${id.padEnd(10)} $${Math.round(c).toString().padStart(7)}  income ${inc.toFixed(1)}/s`); prev = tt; }
console.log(`done in ${mm(t)} with ${ups} upgrades; levels`, JSON.stringify(st), JSON.stringify(tr));
