// game.js — the simulation: locations, economy, spawning, payouts, pads, upgrades, staff tasks, events, quests.
const Input = { x: 0, z: 0, active: false };

const Game = (() => {
  const G = {
    L: null, ls: null, locIdx: 0,
    lanes: [], racks: {}, workers: [], cars: [], pads: [], buildings: {},
    player: null, t: 0, paused: false, running: false,
    spawnT: 0, rushHour: false, rushT: 0, nextRush: 0, nextGift: 0, nextGolden: 0, goldenDue: false,
    streak: 0, polishingPlayer: false, pickT: 0, dropT: 0, balloon: null, quest: null, saveT: 0, bottleneck: null, bnT: 0,
  };
  const S = () => Store.s;
  const M = () => G.L.mult;
  const now = () => Date.now();

  // =====================================================================
  // Economy formulas (see design doc)
  // =====================================================================
  const lv = (t) => G.ls.st[t] || 1;
  const trk = (id) => G.ls.tr[id] || 0;
  const built = (id) => !!G.ls.pads[id];
  const boost = (name) => S().boosts[name] > now();

  function stationValue(t) { const l = lv(t); return CFG.stations[t].value * (1 + 0.12 * (l - 1)) * Math.pow(1.5, Math.floor(l / 10)); }
  function stationTime(t) { const l = lv(t); return Math.max(0.45, CFG.stations[t].time * Math.pow(0.95, l - 1)); }
  function stationUpCost(t) { return Math.round(CFG.stations[t].upCost * M() * Math.pow(CFG.upGrowth, lv(t) - 1)); }
  function trackCost(id) { const d = CFG.tracks[id]; return Math.round(d.cost * M() * Math.pow(d.growth, trk(id))); }
  function rackLv(s) { return G.ls.racks[s] || 1; }
  function rackCost(s) { return Math.round(CFG.racks[s].upCost * M() * Math.pow(1.22, rackLv(s) - 1)); }
  function incomeMult() {
    const s = S();
    return (1 + 0.1 * trk('mktPrice')) * (built('landmark') ? 1.25 : 1) * (1 + 0.1 * s.gemUp.sponge) * (s.perks.starter ? 1.5 : 1) * (s.perks.vip ? 2 : 1);
  }
  function priceMult() { return M() * incomeMult() * (boost('income') ? 2 : 1); }
  function basePrice() { return CFG.stations.brush.value * M(); }
  function arrivalInterval() {
    let iv = G.L.spawn * Math.pow(0.92, trk('mktArrive'));
    if (built('tubeman')) iv *= CFG.buildingFx.tubeman.arrive;
    if (built('billboard')) iv *= CFG.buildingFx.billboard.arrive;
    if (G.rushHour) iv /= 3;
    return iv;
  }
  function queueCap() { return 3 + (built('queue') ? 3 : 0) + (G.rushHour ? 2 : 0); }
  function patience(def) { return (def.pat + 4 * trk('mktPatience') + (built('queue') ? 5 : 0)) * (built('flowers') ? 1.2 : 1); }
  function tipChance() { return 0.12 + 0.05 * trk('mktTips') + (built('fountain') ? 0.1 : 0) + 0.01 * (lv('pay') - 1); }
  function vipChance() { return built('vip') ? 0.08 + 0.04 * trk('mktVip') : 0; }
  function mgrSpeed() { return 6.2 * (1 + 0.07 * trk('mgrSpeed')) * (boost('speed') ? 1.6 : 1); }
  function mgrCarry() { return 4 + trk('mgrCarry'); }
  function magnet() { return 2.3 + 0.5 * trk('mgrMagnet'); }
  function staffSpeed() { return 3.5 * (1 + 0.08 * trk('stfSpeed')) * (built('coffee') ? 1.1 : 1); }
  function staffCarry() { return 2 + trk('stfCarry'); }
  function napMul() { return Math.pow(0.85, trk('stfCoffee')) * (built('coffee') ? 0.5 : 1); }

  // rough steady-state income, used for rewards and offline earnings
  function incomePerSec(withBoost = false) {
    let total = 0;
    const lanes = G.lanes.filter(l => l.unlocked && l.stations.pay);
    const arrive = 1 / arrivalInterval();
    for (const l of lanes) {
      const stops = l.stops();
      let cycle = 0, value = 0;
      for (const st of stops) { cycle = Math.max(cycle, stationTime(st.type) + 1.5); if (st.type !== 'pay') value += stationValue(st.type); }
      const cps = Math.min(1 / Math.max(0.5, cycle), arrive / lanes.length);
      total += cps * value * 1.2;
    }
    return total * M() * incomeMult() * (withBoost && boost('income') ? 2 : 1);
  }
  function rewardFor(minutes) { return Math.round(Math.max(incomePerSec() * 60 * minutes, (8 + 10 * minutes) * M())); }

  // =====================================================================
  // Money, gems, XP
  // =====================================================================
  function addCash(v, worldPos, o = {}) {
    v = Math.max(0, v);
    const s = S(); s.cash += v; s.stats.earned += v; G.ls.earned += v; cnt('earn', v);
    UI.cashChanged(!o.silent);
  }
  function spend(v, silent) { const s = S(); s.cash = Math.max(0, s.cash - v); UI.cashChanged(false); }
  function canAfford(v) { return S().cash >= v - 1e-6; }
  function addGems(n) { S().gems += n; UI.gemsChanged(); }
  function addXP(n) {
    const s = S(); s.xp += n;
    let up = false;
    while (s.xp >= CFG.xpFor(s.level)) { s.xp -= CFG.xpFor(s.level); s.level++; up = true; levelUp(s.level); }
    UI.xpChanged(up);
  }
  function levelUp(level) {
    const gems = CFG.levelGems(level);
    addGems(gems);
    Sfx.play.levelup(); U.vibrate([20, 40, 20]);
    FX.confetti(G.player.pos.x, 2.5, G.player.pos.z, 70, 8);
    UI.levelUp(level, gems);
    Bus.emit('level', level);
  }
  function cnt(k, v = 1) { const c = G.ls.cnt || (G.ls.cnt = {}); c[k] = (c[k] || 0) + v; }

  // =====================================================================
  // Location lifecycle
  // =====================================================================
  function load(idx, o = {}) {
    unload();
    const s = S();
    G.locIdx = idx; G.L = CFG.locations[idx]; s.loc = idx;
    G.ls = Store.loc(G.L.id);
    if (!G.ls.cnt) G.ls.cnt = {};
    if (G.ls.cash != null) { s.cash = G.ls.cash; }
    World.build(G.L.theme);
    Nav.init({ x0: CFG.walkBounds.x0 - 1, x1: CFG.walkBounds.x1 + 1, z0: CFG.walkBounds.z0 - 1, z1: CFG.walkBounds.z1 + 1 });
    G.player = new Player();
    const fresh = !G.ls.started;
    G.ls.started = true;
    // lanes + stations
    G.lanes = CFG.laneZ.map((_, i) => new Lane(i));
    G.lanes[0].unlock(false);
    const hop = (li, t, dflt) => { const k = li + ':' + t; return G.ls.hoppers[k] != null ? G.ls.hoppers[k] : dflt; };
    G.lanes[0].addStation('brush', false, hop(0, 'brush', fresh && idx === 0 ? 0 : 4));
    G.lanes[0].addStation('pay', false);
    if (built('lane2')) { G.lanes[1].unlock(false); G.lanes[1].addStation('brush', false, hop(1, 'brush', 4)); G.lanes[1].addStation('pay', false); }
    for (const def of CFG.pads) if (def.kind === 'station' && built(def.id)) G.lanes[def.lane].addStation(def.st, false, hop(def.lane, def.st, 4));
    // racks
    G.racks = {};
    const stock = (s2, d) => G.ls.rackStock[s2] != null ? G.ls.rackStock[s2] : d;
    G.racks.soap = new Rack('soap', stock('soap', fresh ? 3 : 4));
    if (built('waxRack')) G.racks.wax = new Rack('wax', stock('wax', 3));
    if (built('gelRack')) G.racks.gel = new Rack('gel', stock('gel', 3));
    // buildings
    G.buildings = {};
    G.buildings.shop = new Building('shop', false, G.L.theme);
    for (const def of CFG.pads) if ((def.kind === 'building' || def.kind === 'travel') && built(def.id)) G.buildings[def.b || 'travel'] = new Building(def.b || 'travel', false, G.L.theme);
    // staff
    G.workers = [];
    for (const def of CFG.pads) if (def.kind === 'hire' && built(def.id)) hire(def, false);
    // piles
    for (const l of G.lanes) if (l.pile && G.ls.piles[l.idx]) l.pile.restore(G.ls.piles[l.idx]);
    if (G.buildings.vending && G.ls.trays.vending) G.buildings.vending.tray.restore(G.ls.trays.vending);
    // pads
    G.pads = [];
    for (const def of CFG.pads) if (!built(def.id)) G.pads.push(new Pad(def, M(), G.ls.paid[def.id]));
    revealPads(false);
    rebuildNav();
    G.cars = [];
    G.spawnT = 2.5; G.rushHour = false; G.streak = 0;
    const t = performance.now() / 1000;
    G.nextGift = t + U.rand(...CFG.events.giftEvery) * 0.5;
    G.nextGolden = t + U.rand(...CFG.events.goldenEvery);
    G.nextRush = t + U.rand(...CFG.events.rushEvery);
    pickQuest(false);
    GFX.updateCamera(0.016, G.player.pos, true);
    G.running = true;
    UI.onLocation && UI.onLocation();
    Bus.emit('location', idx);
  }
  function unload() {
    if (!G.L) return;
    saveRuntime();
    G.running = false;
    for (const c of G.cars.slice()) { c.dead = true; GFX.scene.remove(c.group); c.label.remove(); if (c.sayLbl) c.sayLbl.remove(); if (c.goldLbl) c.goldLbl.remove(); }
    G.cars = [];
    for (const w of G.workers) w.destroy(); G.workers = [];
    for (const l of G.lanes) l.destroy(); G.lanes = [];
    for (const k in G.racks) G.racks[k].destroy(); G.racks = {};
    for (const k in G.buildings) G.buildings[k].destroy(); G.buildings = {};
    for (const p of G.pads) p.hide(); G.pads = [];
    if (G.balloon) { GFX.scene.remove(G.balloon.mesh); G.balloon.lbl.remove(); G.balloon = null; }
    if (G.player) { GFX.scene.remove(G.player.model.group); GFX.scene.remove(G.player.shadow); G.player.maxLabel.remove(); G.player.stack.clear(); }
    if (G.arrow) { GFX.scene.remove(G.arrow); GFX.scene.remove(G.ring); G.arrow = null; G.ring = null; }
    FX.clearAll();
    World.clear();
  }
  function saveRuntime() {
    if (!G.ls) return;
    const ls = G.ls;
    ls.hoppers = {}; ls.piles = {}; ls.rackStock = {}; ls.trays = {};
    for (const l of G.lanes) {
      for (const st of Object.values(l.stations)) if (st.hopper) ls.hoppers[l.idx + ':' + st.type] = st.hopper.count + st.hopper.pending;
      if (l.pile) ls.piles[l.idx] = Math.round(l.pile.total);
    }
    for (const k in G.racks) ls.rackStock[k] = G.racks[k].stock;
    if (G.buildings.vending) ls.trays.vending = Math.round(G.buildings.vending.tray.total);
    ls.cash = S().cash;
  }
  function rebuildNav() {
    const list = [];
    for (const l of G.lanes) list.push(...l.colliders());
    for (const k in G.racks) list.push(...G.racks[k].colliders());
    for (const k in G.buildings) list.push(...G.buildings[k].coll);
    Nav.setColliders(list);
  }

  // =====================================================================
  // Pads
  // =====================================================================
  function padPos(def) {
    if (def.pos) return def.pos;
    if (def.kind === 'station') return [CFG.slotX[def.st], CFG.laneZ[def.lane]];
    if (def.kind === 'rack') { const p = CFG.racksPos[def.rack]; return [p[0] - 0.4, p[1]]; }
    return [0, 0];
  }
  function padIcon(def) {
    if (def.kind === 'station') return CFG.stations[def.st].icon;
    if (def.kind === 'rack') return CFG.supplies[def.rack].icon;
    if (def.kind === 'hire') return { stocker: 'worker', cashier: 'pay', polisher: 'polish' }[def.role];
    return def.icon || 'star';
  }
  function padName(def) {
    if (def.kind === 'station') return (def.lane ? 'Lane 2 ' : '') + CFG.stations[def.st].name;
    if (def.kind === 'rack') return CFG.racks[def.rack].name;
    if (def.kind === 'hire') return 'Hire ' + { stocker: 'Stocker', cashier: 'Cashier', polisher: 'Polisher' }[def.role] + (def.lane ? ' (Lane 2)' : '');
    if (def.kind === 'travel') { const nx = CFG.locations[G.locIdx + 1]; return nx ? 'Road Trip: ' + nx.name : 'Grand Finale'; }
    return def.name || def.id;
  }
  function revealPads(anim = true) {
    for (const p of G.pads) {
      if (p.shown) continue;
      if (p.def.req.every(built)) p.show(anim);
    }
  }
  function completePad(pad) {
    const def = pad.def;
    G.ls.pads[def.id] = true; delete G.ls.paid[def.id];
    pad.hide(); G.pads.splice(G.pads.indexOf(pad), 1);
    Sfx.play.build(); U.vibrate(30); GFX.shake(0.25);
    FX.confetti(pad.pos.x, 1.5, pad.pos.z, 45, 6);
    build(def, true);
    rebuildNav();
    UI.refreshCards();
    const idx = CFG.pads.indexOf(def);
    addXP(10 + idx * 3);
    cnt('build');
    setTimeout(() => revealPads(true), 650);
    UI.toast(`${padName(def)} built!`, padIcon(def));
    Store.save();
    Bus.emit('pad:built', def);
  }
  function build(def, anim) {
    if (def.kind === 'station') {
      const st = G.lanes[def.lane].addStation(def.st, anim, 4);
      if (anim) FX.text(new THREE.Vector3(st.x, 5.2, st.z), 'NEW!', 'new', { dur: 1.3 });
    } else if (def.kind === 'rack') {
      G.racks[def.rack] = new Rack(def.rack, 3); if (anim) popIn(G.racks[def.rack].model.group);
    } else if (def.kind === 'hire') {
      hire(def, anim);
    } else if (def.kind === 'building') {
      G.buildings[def.b] = new Building(def.b, anim, G.L.theme);
    } else if (def.kind === 'lane') {
      const l = G.lanes[1]; l.unlock(anim); l.addStation('brush', anim, 4); l.addStation('pay', anim);
      UI.toast('Lane 2 is open! Twice the cars, twice the cash.', 'lane');
    } else if (def.kind === 'travel') {
      G.buildings.travel = new Building('travel', anim, G.L.theme);
      const s = S();
      if (G.locIdx + 1 < CFG.locations.length) { s.unlockedLocs = Math.max(s.unlockedLocs, G.locIdx + 2); if (anim) setTimeout(() => UI.openPanel('travel', { to: G.locIdx + 1 }), 900); }
      else if (anim) setTimeout(() => UI.openPanel('finale'), 900);
    }
  }
  function hire(def, anim) {
    const role = def.role;
    const names = CFG.hireNames[role];
    const n = G.workers.filter(w => w.role === role).length;
    const w = new Worker(role, { lane: def.lane || 0, name: names[n % names.length] });
    G.workers.push(w);
    if (anim) { Sfx.play.hire(); FX.text(w.pos.clone().setY(2.6), 'HIRED!', 'big', { dur: 1.2 }); popIn(w.model.group); }
    return w;
  }
  function staffDoor() { const b = G.buildings.staff; return b ? { x: b.pos.x, z: b.pos.z + 3.2 } : { x: CFG.buildings.shop.pos[0] + 3.0, z: CFG.buildings.shop.pos[1] + 3.8 }; }
  function postFor(role, laneIdx) {
    const l = G.lanes[laneIdx || 0]; if (!l) return null;
    return role === 'cashier' ? l.stations.pay || null : role === 'polisher' ? l.stations.polish || null : null;
  }
  function removeWorker(w) { const i = G.workers.indexOf(w); if (i >= 0) G.workers.splice(i, 1); }
  function removeCar(c) { const i = G.cars.indexOf(c); if (i >= 0) G.cars.splice(i, 1); }

  // =====================================================================
  // Upgrades
  // =====================================================================
  function buyStation(t, free) {
    if (lv(t) >= CFG.stationMaxLv) return false;
    const cost = stationUpCost(t);
    if (!free && !canAfford(cost)) { Sfx.play.error(); return false; }
    if (!free) spend(cost);
    G.ls.st[t] = lv(t) + 1;
    afterUpgrade(t);
    for (const l of G.lanes) { const st = l.stations[t]; if (st) { FX.sparkles(st.x, 3, st.z, 8, 1.6); st.applyTier(); const g = st.group; g.scale.setScalar(1.08); setTimeout(() => g.scale.setScalar(1), 120); } }
    if (lv(t) % 10 === 0) UI.toast(`${CFG.stations[t].name} reached Lv ${lv(t)}: value x1.5!`, CFG.stations[t].icon);
    return true;
  }
  function buyTrack(id, free) {
    const d = CFG.tracks[id];
    if (trk(id) >= d.max) return false;
    const cost = trackCost(id);
    if (!free && !canAfford(cost)) { Sfx.play.error(); return false; }
    if (!free) spend(cost);
    G.ls.tr[id] = trk(id) + 1;
    afterUpgrade(id);
    if (id === 'mgrCarry' || id === 'mgrSpeed') FX.sparkles(G.player.pos.x, 1.5, G.player.pos.z, 8, 1);
    return true;
  }
  function buyRack(s2) {
    if (rackLv(s2) >= CFG.racks[s2].maxLv) return false;
    const cost = rackCost(s2);
    if (!canAfford(cost)) { Sfx.play.error(); return false; }
    spend(cost); G.ls.racks[s2] = rackLv(s2) + 1; afterUpgrade('rack:' + s2);
    const r = G.racks[s2]; if (r) FX.sparkles(r.pos.x, 2, r.pos.z, 8, 1.4);
    return true;
  }
  function afterUpgrade(what) {
    Sfx.play.upgrade(); U.vibrate(15);
    S().stats.upgrades++; cnt('upgrades');
    addXP(2);
    UI.cashChanged(false); UI.refreshCards();
    Bus.emit('upgrade', what);
  }

  // =====================================================================
  // Payout at the pay booth
  // =====================================================================
  function payout(st, car) {
    const s = S();
    let amount = car.bill * car.def.pay * priceMult() * car.goldenMult * (G.rushHour ? 1.5 : 1);
    amount = Math.max(1, Math.round(amount));
    const clean = car.dirt < 0.05;
    let tip = 0;
    if (Math.random() < tipChance() * (clean ? 1 : 0.35)) tip = Math.max(1, Math.round(amount * U.rand(0.2, 0.5) * (1 + Math.min(G.streak, 10) * 0.1)));
    const from = new THREE.Vector3(car.x, 1.2, car.z);
    st.lane.pile.add(amount + tip, from);
    const near = G.player.near(car.x, car.z, 26);
    if (near) {
      FX.text(new THREE.Vector3(car.x, car.model.h + 1.0, car.z), `${ICONS.html('cash')} +${U.fmt(amount)}`, 'cash', { dur: 1.3 });
      if (tip) setTimeout(() => FX.text(new THREE.Vector3(car.x + 0.6, car.model.h + 0.6, car.z), `TIP +${U.fmt(tip)}`, 'tip', { dur: 1.2 }), 250);
      if (clean && car.stages >= 3 && Math.random() < 0.6) setTimeout(() => FX.text(new THREE.Vector3(car.x - 0.5, car.model.h + 1.6, car.z), U.pick(CFG.cleanWords), 'big', { dur: 1.2 }), 120);
      if (!clean && car.coat.length && Math.random() < 0.5) FX.text(new THREE.Vector3(car.x, car.model.h + 1.4, car.z), 'STILL FOAMY?!', 'honk', { dur: 1.2 });
    }
    Sfx.play.cash();
    // stats
    s.stats.cars++; G.ls.cars++; cnt('wash');
    if (tip) { s.stats.tips++; G.ls.tips++; cnt('tips'); }
    if (car.vip) { s.stats.vip++; G.ls.vip++; cnt('vip'); }
    if (st.operatorPresent() === 'player') cnt('serve');
    G.streak = clean ? G.streak + 1 : 0;
    UI.streak(G.streak);
    addXP((1 + car.stages) * (car.vip ? 3 : 1) * (car.golden ? 5 : 1));
    // album
    const first = !s.album[car.type];
    s.album[car.type] = (s.album[car.type] || 0) + 1;
    if (first && s.level >= 1) { addGems(3); setTimeout(() => UI.toast(`New car for your album: ${car.def.name}! +3 gems`, 'album'), 400); }
    Bus.emit('car:paid', { car, amount, tip });
  }

  // =====================================================================
  // Spawning
  // =====================================================================
  function pickCarType() {
    if (G.goldenDue) { G.goldenDue = false; return 'golden'; }
    if (Math.random() < vipChance()) return U.pick(['limo', 'sports', 'monster']);
    const w = G.L.cars;
    return U.weighted(Object.keys(w), k => w[k]);
  }
  function spawnUpdate(dt) {
    G.spawnT -= dt;
    if (G.spawnT > 0) return;
    const lanes = G.lanes.filter(l => l.unlocked && l.stations.pay);
    // lane 2 works as overflow: cars pick the shortest queue, ties go to the better-equipped lane
    let best = null, bestScore = Infinity;
    for (const l of lanes) { if (l.queue.length >= queueCap()) continue; const sc = l.queue.length * 10 - l.stops().length; if (sc < bestScore) { bestScore = sc; best = l; } }
    if (!best) { G.spawnT = 0.8; return; }
    const iv = arrivalInterval();
    G.spawnT = iv * U.rand(0.75, 1.25);
    const type = pickCarType();
    const back = best.cars[best.cars.length - 1];
    const car = new Car(type, best, { x: back ? Math.min(CFG.spawnX, back.x - 6) : CFG.spawnX });
    best.cars.push(car); G.cars.push(car);
    if (type === 'golden') { UI.goldenOffer(car); }
  }

  // =====================================================================
  // Stockers pick the most urgent hopper
  // =====================================================================
  function allHoppers() { const out = []; for (const l of G.lanes) for (const st of Object.values(l.stations)) if (st.hopper) out.push(st.hopper); return out; }
  function pickStockTask(w) {
    let best = null, bestS = -Infinity;
    for (const h of allHoppers()) {
      const r = G.racks[h.supply]; if (!r) continue;
      const need = h.cap - h.count - h.incoming - h.pending;
      if (need < 2 && !(h.count === 0 && need >= 1)) continue;
      const carrying = w.stack.countOf(h.supply);
      const amount = Math.min(need, Math.max(carrying, w.stack.cap - w.stack.count + carrying));
      if (amount <= 0) continue;
      const urg = (h.cap - h.count) / h.cap;
      const d = U.dist(w.pos.x, w.pos.z, r.mat.x, r.mat.z) + U.dist(r.mat.x, r.mat.z, h.mat.x, h.mat.z);
      const sc = urg * 30 + (h.count === 0 ? 25 : 0) - d * 0.5 + (carrying ? 25 : 0) + (r.stock > 0 ? 6 : 0);
      if (sc > bestS) { bestS = sc; best = { hopper: h, rack: r, supply: h.supply, amount }; }
    }
    return best;
  }

  // =====================================================================
  // Player <-> world interactions (pickup, delivery)
  // =====================================================================
  function playerInteract(dt) {
    const p = G.player;
    G.pickT -= dt; G.dropT -= dt;
    for (const k in G.racks) {
      const r = G.racks[k];
      if (!p.near(r.mat.x, r.mat.z, 1.1)) continue;
      if (G.pickT <= 0 && !p.stack.full && r.stock > 0) {
        const wp = r.take(); if (wp) { p.stack.add(k, wp); Sfx.play.pop(); cnt('pickup'); G.pickT = 0.09; }
      }
    }
    for (const h of allHoppers()) {
      if (!p.near(h.mat.x, h.mat.z, 1.1)) continue;
      if (G.dropT <= 0 && p.stack.countOf(h.supply) > 0 && h.space > 0) {
        const it = p.stack.take(h.supply); h.receive(it); cnt('deliver'); G.dropT = 0.08;
      }
    }
    G.polishingPlayer = false;
    for (const l of G.lanes) { const st = l.stations.polish; if (st && st.busy && p.near(st.operatorSpot.x, st.operatorSpot.z, 1.15)) { G.polishingPlayer = true; p.facing = U.angleLerp(p.facing, Math.PI, 0.2); } }
  }

  // =====================================================================
  // Events: gift balloon, golden car, rush hour
  // =====================================================================
  function eventsUpdate(dt) {
    const t = performance.now() / 1000, s = S();
    if (s.level >= CFG.unlocks.gift && !G.balloon && t > G.nextGift) spawnBalloon();
    if (G.balloon) {
      const b = G.balloon; b.t += dt;
      b.mesh.position.set(-34 + b.t * 2.4, 8.5 + Math.sin(b.t * 1.4) * 0.5, b.z);
      b.mesh.rotation.y += dt * 0.5;
      if (b.t > 29) { GFX.scene.remove(b.mesh); b.lbl.remove(); G.balloon = null; G.nextGift = t + U.rand(...CFG.events.giftEvery); }
    }
    if (s.level >= CFG.unlocks.golden && t > G.nextGolden) { G.goldenDue = true; G.nextGolden = t + U.rand(...CFG.events.goldenEvery); }
    if (s.level >= CFG.unlocks.rush) {
      if (!G.rushHour && t > G.nextRush) { G.rushHour = true; G.rushT = CFG.events.rushSecs; UI.banner('RUSH HOUR!', 'Cars x3 and +50% pay', CFG.events.rushSecs); Sfx.play.siren(); }
      if (G.rushHour) { G.rushT -= dt; if (G.rushT <= 0) { G.rushHour = false; G.nextRush = t + U.rand(...CFG.events.rushEvery); } }
    }
  }
  function spawnBalloon() {
    const g = new THREE.Group();
    const b = new Builder();
    b.sphere(1.1, 0xff4d5a, { y: 2.2, sy: 1.15, ws: 18, hs: 14 });
    b.cone(0.25, 0.35, 10, 0xff4d5a, { y: 0.95, rx: Math.PI });
    b.cyl(0.02, 0.02, 1.2, 4, 0xffffff, { y: 0.2 });
    b.rbox(0.8, 0.7, 0.8, 0.1, 0x46c23a, { y: -0.6 });
    b.box(0.84, 0.16, 0.84, 0xffd23f, { y: -0.6 }); b.box(0.16, 0.74, 0.84, 0xffd23f, { y: -0.6 });
    g.add(b.build());
    GFX.scene.add(g);
    const z = U.clamp(G.player.pos.z - 2, -4, 14);
    const lbl = FX.label(g, 3.8, `<div class="gift-tag" id="gift-tag">${ICONS.html('gift')}FREE GIFT</div>`, '');
    lbl.el.style.pointerEvents = 'auto';
    lbl.el.onclick = (e) => { e.stopPropagation(); UI.giftOffer(); };
    lbl.el.addEventListener('pointerdown', (e) => e.stopPropagation());
    G.balloon = { mesh: g, lbl, t: 0, z };
    g.userData.clickable = 'gift';
  }
  function popBalloon() {
    if (!G.balloon) return;
    const p = G.balloon.mesh.position;
    FX.confetti(p.x, p.y, p.z, 40, 5); Sfx.play.pop();
    GFX.scene.remove(G.balloon.mesh); G.balloon.lbl.remove(); G.balloon = null;
    G.nextGift = performance.now() / 1000 + U.rand(...CFG.events.giftEvery);
  }
  function addBoost(name, secs) {
    const s = S(), n = now();
    const cur = Math.max(n, s.boosts[name] || 0);
    s.boosts[name] = Math.min(n + CFG.boosts[name].max * 1000, cur + secs * 1000);
    if (name === 'turbo') {
      if (!G.workers.some(w => w.turbo)) { const w = new Worker('turbo', { name: 'Turbo Tom' }); w.pos.set(G.player.pos.x + 1, 0, G.player.pos.z + 1); G.workers.push(w); popIn(w.model.group); FX.text(w.pos.clone().setY(2.6), 'TURBO TOM!', 'big'); }
    }
    UI.refreshBoosts();
  }

  // =====================================================================
  // Quests (task list)
  // =====================================================================
  const TUT = [
    { k: 'pickup', n: 1, text: 'Grab soap from the Soap Rack', icon: 'soap' },
    { k: 'deliver', n: 1, text: 'Load soap into the Foam Brush', icon: 'brush' },
    { k: 'serve', n: 1, text: 'Stand at the Pay Booth to take payment', icon: 'pay' },
    { k: 'collect', n: 1, text: 'Pick up your cash', icon: 'cash' },
  ];
  const CHAIN = [
    { k: 'build', id: 'rinse' }, { k: 'build', id: 'cashier1' }, { k: 'wash', n: 6 }, { k: 'upgrade', st: 'brush', lv: 3 }, { k: 'build', id: 'dryer' },
    { k: 'build', id: 'staff' }, { k: 'build', id: 'stocker1' }, { k: 'earn', n: 400 }, { k: 'upgrade', st: 'rinse', lv: 4 },
    { k: 'build', id: 'office' }, { k: 'track', id: 'mgrSpeed', lv: 1 }, { k: 'build', id: 'queue' }, { k: 'build', id: 'waxRack' },
    { k: 'build', id: 'wax' }, { k: 'tips', n: 5 }, { k: 'build', id: 'tubeman' },
    { k: 'build', id: 'vending' }, { k: 'upgrade', st: 'pay', lv: 5 }, { k: 'build', id: 'gelRack' }, { k: 'build', id: 'tire' },
    { k: 'wash', n: 40 }, { k: 'build', id: 'stocker2' }, { k: 'track', id: 'mktArrive', lv: 2 }, { k: 'build', id: 'lane2' },
  ];
  function questList() { return (G.locIdx === 0 ? TUT : []).concat(CHAIN); }
  function genQuest(i) {
    const kinds = ['build', 'wash', 'upgrade', 'build', 'earn', 'tips', 'build', 'track'];
    const k = kinds[i % kinds.length];
    if (k === 'build') {
      const vis = G.pads.filter(p => p.shown).sort((a, b) => a.cost - b.cost)[0];
      if (vis) return { k: 'build', id: vis.id };
      return { k: 'wash', n: 30 + i * 2 };
    }
    if (k === 'wash') return { k: 'wash', n: 20 + Math.min(200, i * 3) };
    if (k === 'earn') return { k: 'earn', n: Math.round(Math.max(500, rewardFor(4)) / M()) };
    if (k === 'tips') return { k: 'tips', n: 5 + Math.floor(i / 4) };
    if (k === 'upgrade') {
      const types = CFG.slotOrder.filter(t => G.lanes.some(l => l.stations[t])).filter(t => lv(t) < CFG.stationMaxLv);
      if (!types.length) return { k: 'wash', n: 50 };
      const t = types.sort((a, b) => lv(a) - lv(b))[0];
      return { k: 'upgrade', st: t, lv: Math.min(CFG.stationMaxLv, lv(t) + 2 + (i % 3)) };
    }
    if (k === 'track') {
      const ids = Object.keys(CFG.tracks).filter(id => trk(id) < CFG.tracks[id].max && (!CFG.tracks[id].needs || built(CFG.tracks[id].needs)) && (CFG.tracks[id].group !== 'staff' || built('staff')) && (CFG.tracks[id].group === 'staff' || built('office')));
      if (!ids.length) return { k: 'wash', n: 40 };
      const id = ids.sort((a, b) => trk(a) - trk(b))[0];
      return { k: 'track', id, lv: trk(id) + 1 };
    }
    return { k: 'wash', n: 25 };
  }
  function questText(q) {
    switch (q.k) {
      case 'build': return `Build: ${padName(CFG.pads.find(p => p.id === q.id))}`;
      case 'wash': return `Wash ${q.n} cars`;
      case 'upgrade': return `Upgrade ${CFG.stations[q.st].name} to Lv ${q.lv}`;
      case 'track': return `Buy ${CFG.tracks[q.id].name} Lv ${q.lv}`;
      case 'earn': return `Earn $${U.fmt(q.n * M())}`;
      case 'tips': return `Collect ${q.n} tips`;
      default: return q.text;
    }
  }
  function questIcon(q) {
    if (q.icon) return q.icon;
    if (q.k === 'build') return padIcon(CFG.pads.find(p => p.id === q.id));
    if (q.k === 'upgrade') return CFG.stations[q.st].icon;
    if (q.k === 'track') return CFG.tracks[q.id].icon;
    return { wash: 'car', earn: 'cash', tips: 'heart' }[q.k] || 'flag';
  }
  function pickQuest(advance) {
    const ls = G.ls;
    if (advance) { ls.quest = (ls.quest || 0) + 1; ls.qDef = null; }
    const list = questList(), i = ls.quest || 0;
    let q = ls.qDef || (i < list.length ? Object.assign({}, list[i]) : genQuest(i));
    if (q.k === 'build' && !CFG.pads.find(p => p.id === q.id)) q = { k: 'wash', n: 30 };
    ls.qDef = q;
    if (!ls.qBase || advance) ls.qBase = Object.assign({}, ls.cnt);
    G.quest = q;
    UI.questChanged();
  }
  function questProgress() {
    const q = G.quest; if (!q) return [0, 1];
    const c = G.ls.cnt, b = G.ls.qBase || {};
    const d = (k) => (c[k] || 0) - (b[k] || 0);
    switch (q.k) {
      case 'pickup': case 'deliver': case 'serve': case 'collect': case 'tips': return [Math.min(q.n, d(q.k)), q.n];
      case 'wash': return [Math.min(q.n, d('wash')), q.n];
      case 'earn': return [Math.min(q.n * M(), d('earn')), q.n * M()];
      case 'build': return [built(q.id) ? 1 : 0, 1];
      case 'upgrade': return [Math.min(q.lv, lv(q.st)), q.lv];
      case 'track': return [Math.min(q.lv, trk(q.id)), q.lv];
    }
    return [0, 1];
  }
  function questReward() {
    const i = G.ls.quest || 0;
    return { cash: rewardFor(0.25 + Math.min(i, 40) * 0.05), gems: i % 4 === 3 ? 3 : 1, xp: 8 + Math.min(i, 60) * 2 };
  }
  function questTarget() {
    const q = G.quest; if (!q) return null;
    const v = (x, z) => new THREE.Vector3(x, 0, z);
    switch (q.k) {
      case 'pickup': { const r = G.racks.soap; return r ? v(r.mat.x, r.mat.z) : null; }
      case 'deliver': { const st = G.lanes[0].stations.brush; return st ? v(st.hopper.mat.x, st.hopper.mat.z) : null; }
      case 'serve': { const st = G.lanes[0].stations.pay; return v(st.operatorSpot.x, st.operatorSpot.z); }
      case 'collect': { const pl = G.lanes[0].pile; return pl && pl.values.length ? pl.pos.clone() : null; }
      case 'build': { const p = G.pads.find(p2 => p2.id === q.id && p2.shown); return p ? p.pos.clone() : null; }
      case 'track': { const b = G.buildings.office || G.buildings.staff; return b && b.mat ? v(b.mat.x, b.mat.z) : null; }
    }
    return null;
  }
  function claimQuest() {
    const [p, n] = questProgress();
    if (p < n) return false;
    const r = questReward();
    addCash(r.cash); addGems(r.gems); addXP(r.xp);
    Sfx.play.reward();
    UI.rewardBurst({ cash: r.cash, gems: r.gems });
    pickQuest(true);
    Store.save();
    return true;
  }

  // quest arrow in the world
  function arrowUpdate(dt) {
    const tgt = questTarget();
    if (!G.arrow) {
      const b = new Builder();
      b.cone(0.62, 0.95, 4, 0xff8a1f, { rx: Math.PI, y: 0.48, ry: Math.PI / 4 });
      b.box(0.42, 0.75, 0.42, 0xff8a1f, { y: 1.3 });
      G.arrow = b.build(MAT.vc, false); GFX.scene.add(G.arrow);
      G.ring = new THREE.Mesh(GEO.plane(2.4, 2.4), new THREE.MeshBasicMaterial({ map: TEX.ring(), transparent: true, depthWrite: false, color: 0xff8a1f }));
      G.ring.rotation.x = -Math.PI / 2; G.ring.renderOrder = 3; GFX.scene.add(G.ring);
    }
    const qp = questProgress();
    const near = tgt && (G.player.near(tgt.x, tgt.z, 1.3) || qp[0] >= qp[1]);
    G.arrow.visible = G.ring.visible = !!tgt && !near;
    if (tgt) {
      const t = performance.now() / 1000;
      G.arrow.position.set(tgt.x, 1.1 + Math.abs(Math.sin(t * 4)) * 0.55, tgt.z);
      G.arrow.rotation.y += dt * 2;
      G.ring.position.set(tgt.x, 0.09, tgt.z);
      const s = 1 + (t * 1.5 % 1) * 0.35; G.ring.scale.set(s, s, 1); G.ring.material.opacity = 1 - (t * 1.5 % 1) * 0.7;
    }
    UI.edgeArrow(G.arrow.visible ? tgt : null);
  }

  // =====================================================================
  // Main update
  // =====================================================================
  function update(dt) {
    if (!G.running || G.paused) return;
    G.t += dt;
    S().stats.playSecs += dt;
    const inp = UI.panelOpen() ? { x: 0, z: 0 } : Input;
    G.player.update(dt, inp);
    playerInteract(dt);
    spawnUpdate(dt);
    for (const c of G.cars.slice()) c.update(dt);
    for (const l of G.lanes) { for (const st of l.stops()) st.update(dt); if (l.pile) l.pile.update(dt); }
    for (const k in G.racks) G.racks[k].update(dt);
    for (const w of G.workers.slice()) { w._dt = dt; w.update(dt); }
    for (const p of G.pads.slice()) p.update(dt);
    for (const k in G.buildings) G.buildings[k].update(dt);
    eventsUpdate(dt);
    arrowUpdate(dt);
    // bottleneck: slowest station type on lane 1
    G.bnT -= dt;
    if (G.bnT <= 0) {
      G.bnT = 1;
      let slow = null, worst = 0;
      for (const st of G.lanes[0].stops()) { const tt = stationTime(st.type); if (tt > worst) { worst = tt; slow = st.type; } }
      const backed = G.lanes.some(l => l.queue.length >= 2);
      const b = backed ? slow : null;
      if (b !== G.bottleneck) { G.bottleneck = b; UI.refreshCards(); }
      UI.tick1s();
    }
    G.saveT += dt;
    if (G.saveT > 5) { G.saveT = 0; saveRuntime(); Store.save(); }
  }

  // offline earnings, computed on boot
  function offlineEarnings(awaySecs) {
    const s = S();
    if (awaySecs < 60) return null;
    const capH = CFG.offlineBaseHours + s.gemUp.offline;
    const secs = Math.min(awaySecs, capH * 3600);
    const lanes = G.lanes.filter(l => l.unlocked && l.stations.pay);
    const staffed = lanes.filter(l => G.workers.some(w => w.role === 'cashier' && w.lane === l.idx)).length;
    const autom = lanes.length ? (staffed / lanes.length) * (G.workers.some(w => w.role === 'stocker') ? 1 : 0.4) : 0;
    const amount = Math.round(incomePerSec() * secs * CFG.offlineEfficiency * autom);
    return { amount, secs, capped: awaySecs > capH * 3600, capH, autom };
  }

  function travel(idx) {
    if (idx === G.locIdx || idx >= S().unlockedLocs) return;
    saveRuntime();
    G.ls.cash = S().cash;
    const go = () => { const nl = Store.loc(CFG.locations[idx].id); S().cash = nl.cash != null ? nl.cash : 0; load(idx); Store.save(); UI.toast(`Welcome to ${CFG.locations[idx].name}!`, 'map'); };
    UI.fade(go);
  }

  function devGive(cash, gems) { if (cash) addCash(cash); if (gems) addGems(gems); }
  function devBuildAll() { for (const p of G.pads.slice()) if (p.def.kind !== 'travel') { p.paid = p.cost; completePad(p); } }

  return {
    G, load, unload, update, saveRuntime, rebuildNav,
    stationValue, stationTime, stationUpCost, trackCost, rackLv, rackCost, priceMult, basePrice, arrivalInterval, queueCap,
    patience, tipChance, vipChance, mgrSpeed, mgrCarry, magnet, staffSpeed, staffCarry, napMul, incomePerSec, rewardFor, incomeMult,
    addCash, spend, canAfford, addGems, addXP, cnt,
    padPos, padIcon, padName, completePad, revealPads, staffDoor, postFor, removeWorker, removeCar, pickStockTask,
    buyStation, buyTrack, buyRack, payout, addBoost, popBalloon, boost,
    questText, questIcon, questProgress, questReward, claimQuest, offlineEarnings, travel, built, lv, trk,
    devGive, devBuildAll,
    get player() { return G.player; }, get lanes() { return G.lanes; }, get workers() { return G.workers; }, get cars() { return G.cars; },
    get racks() { return G.racks; }, get pads() { return G.pads; }, get buildings() { return G.buildings; },
    get L() { return G.L; }, get ls() { return G.ls; }, get locIdx() { return G.locIdx; }, get quest() { return G.quest; },
    get rushHour() { return G.rushHour; }, get polishingPlayer() { return G.polishingPlayer; }, get bottleneck() { return G.bottleneck; },
    get streakN() { return G.streak; }, get running() { return G.running; },
    set paused(v) { G.paused = v; }, get paused() { return G.paused; },
  };
})();

// rage quits reset the wash streak
Bus.on('car:rage', () => { Game.G.streak = 0; UI.streak(0); Store.s.stats.rage++; Game.cnt('rage'); });
Bus.on('ads:pause', (v) => { Game.paused = v; });
