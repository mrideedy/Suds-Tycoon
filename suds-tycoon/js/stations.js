// stations.js — wash stations, hoppers, supply racks, cash piles, build pads, lanes and buildings.

const MatDecal = (() => {
  const cache = {};
  function tex(icon, col) {
    const key = icon + col;
    return cache[key] || (cache[key] = TEX.make(256, 256, (ctx, w, h) => {
      ctx.clearRect(0, 0, w, h);
      TEX.rr(ctx, 12, 12, w - 24, h - 24, 40); ctx.fillStyle = col; ctx.globalAlpha = 0.55; ctx.fill(); ctx.globalAlpha = 1;
      ctx.lineWidth = 12; ctx.strokeStyle = '#ffffff'; TEX.rr(ctx, 18, 18, w - 36, h - 36, 34); ctx.stroke();
      TEX.icon(ctx, icon, w / 2, h / 2, 130);
    }, 'mat|' + key));
  }
  return function (icon, col = '#2563eb', size = 1.7) {
    const m = new THREE.Mesh(GEO.plane(size, size), new THREE.MeshBasicMaterial({ map: tex(icon, col), transparent: true, depthWrite: false }));
    m.rotation.x = -Math.PI / 2; m.renderOrder = 1;
    return m;
  };
})();

function popIn(obj, delay = 0, from = 0.01) {
  obj.scale.setScalar(from);
  const t0 = performance.now() + delay * 1000;
  const step = () => {
    const t = (performance.now() - t0) / 450;
    if (t < 0) { requestAnimationFrame(step); return; }
    obj.scale.setScalar(t >= 1 ? 1 : Math.max(0.01, U.easeOutBack(t)));
    if (t < 1) requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
}

// ---------------------------------------------------------------------------
class Hopper {
  constructor(st, count) {
    this.st = st; this.supply = st.def.supply; this.cap = CFG.hopperCap;
    this.model = Models.hopper(this.supply);
    this.pos = new THREE.Vector3(st.x, 0, st.z + CFG.hopperDZ);
    this.model.group.position.copy(this.pos);
    GFX.scene.add(this.model.group);
    this.mat = { x: st.x, z: st.z + CFG.matDZ };
    this.decal = MatDecal(CFG.supplies[this.supply].icon, '#46c23a', 1.6);
    this.decal.position.set(this.mat.x, 0.05, this.mat.z); GFX.scene.add(this.decal);
    this.count = count; this.incoming = 0; this.pending = 0;
    this.lbl = FX.label(new THREE.Vector3(this.pos.x, 2.0, this.pos.z), 0, '', '');
    this.refresh();
  }
  get space() { return this.cap - this.count - this.pending; }
  receive(it) {
    this.pending++;
    const top = this.pos.clone().setY(1.7);
    FX.fly(it.mesh, it.pos, top, 0.25, 1.0, (m) => {
      if (m.parent) m.parent.remove(m);
      this.pending = Math.max(0, this.pending - 1);
      this.count = Math.min(this.cap, this.count + 1);
      this.refresh(); Sfx.play.pop();
      this.bump = 1;
    }, { spin: 6 });
  }
  consume() { this.count = Math.max(0, this.count - 1); this.refresh(); }
  refresh() {
    this.model.pips.forEach((p, i) => p.material.color.set(i < this.count ? this.model.color : 0x3a3f47));
    const low = this.count <= 1;
    this.lbl.set(this.count >= this.cap ? '' : `<div class="hopper-lbl${low ? ' low' : ''}">${ICONS.html(CFG.supplies[this.supply].icon)}${this.count}/${this.cap}</div>`);
  }
  update(dt) {
    if (this.bump) { this.bump = U.damp(this.bump, 0, 8, dt); const s = 1 + Math.sin(this.bump * 12) * this.bump * 0.06; this.model.group.scale.set(s, 1 / s, s); }
  }
  colliders() { return [{ x0: this.pos.x - 0.62, x1: this.pos.x + 0.62, z0: this.pos.z - 0.5, z1: this.pos.z + 0.5 }]; }
  destroy() { GFX.scene.remove(this.model.group); GFX.scene.remove(this.decal); this.lbl.remove(); }
}

// ---------------------------------------------------------------------------
const STATION_COLL = {
  brush: [[-1.25, -0.65, -2.5, -1.9], [-1.25, -0.65, 1.9, 2.5]],
  rinse: [[-0.42, 0.42, -2.55, -1.75], [-0.42, 0.42, 1.75, 2.55]],
  wax: [[-0.42, 0.42, -2.55, -1.75], [-0.42, 0.42, 1.75, 2.55]],
  dryer: [[-1.05, 1.05, -2.45, -1.85], [-1.05, 1.05, 1.85, 2.45]],
  tire: [[-1.3, 1.3, -2.3, -1.38], [-1.3, 1.3, 1.38, 2.3]],
  polish: [[-2.17, -1.83, -2.37, -2.03], [1.83, 2.17, -2.37, -2.03], [-2.17, -1.83, 2.03, 2.37], [1.83, 2.17, 2.03, 2.37]],
  pay: [[-1.15, 1.15, 2.05, 2.55], [-1.25, -0.78, 2.3, 3.75], [1.35, 1.75, 1.7, 2.1]],
};

class Station {
  constructor(lane, type) {
    this.lane = lane; this.type = type; this.def = CFG.stations[type];
    this.x = CFG.slotX[type]; this.z = lane.z;
    this.group = new THREE.Group(); this.group.position.set(this.x, 0, this.z);
    GFX.scene.add(this.group);
    if (type !== 'pay') {
      const fb = new Builder(); Models.stationFloor(fb, 0, 0);
      const floor = fb.build(MAT.vc, false); floor.receiveShadow = true; floor.userData.noTint = true; this.group.add(floor);
      this.machine = Models.machine(type); this.group.add(this.machine.group);
    } else {
      this.booth = Models.payBooth(); this.booth.group.position.set(0, 0, CFG.boothDZ); this.group.add(this.booth.group);
      this.barrierT = 0;
    }
    if (this.def.operator) {
      this.operatorSpot = new THREE.Vector3(this.x, 0, this.z + (type === 'pay' ? CFG.cashierDZ : CFG.polishDZ));
      this.operatorFacing = Math.PI;
      this.opDecal = MatDecal(type === 'pay' ? 'pay' : 'polish', '#ff8a1f', 1.3);
      this.opDecal.position.set(this.operatorSpot.x, 0.06, this.operatorSpot.z); GFX.scene.add(this.opDecal);
    }
    this.hopper = null;
    this.occupant = null; this.car = null; this.busy = false; this.t = 0; this.dur = 1; this.fxT = 0;
    this.block = null; this.blockT = 0;
    this.badge = FX.label(new THREE.Vector3(this.x, type === 'pay' ? 3.9 : 5.2, this.z + (type === 'pay' ? CFG.boothDZ : 0)), 0, '', '');
    this.prog = FX.label(new THREE.Vector3(this.x, type === 'pay' ? 3.4 : 4.6, this.z + (type === 'pay' ? CFG.boothDZ : 0)), 0, '', '');
    this.lastProg = -1;
    // own material so upgrades can repaint this machine (tiers at Lv 10 / 20 / 30)
    this.mat = MAT.vc.clone();
    this.group.traverse(o => { if (o.isMesh && o.material === MAT.vc && !o.userData.noTint) o.material = this.mat; });
    this.tier = -1; this.applyTier();
  }
  applyTier() {
    const l = Game.lv(this.type), tier = l >= 30 ? 3 : l >= 20 ? 2 : l >= 10 ? 1 : 0;
    if (tier === this.tier) return false;
    const first = this.tier < 0;
    this.tier = tier;
    this.mat.color.set([0xffffff, 0xd8f1ff, 0xeadcff, 0xffe39a][tier]);
    if (!first && tier > 0) { FX.confetti(this.x, 4, this.z, 40, 6); FX.text(new THREE.Vector3(this.x, 5.4, this.z), ['', 'FROSTY PAINT!', 'ROYAL PAINT!', 'SOLID GOLD!'][tier], 'new', { dur: 1.5 }); }
    return true;
  }
  makeHopper(count) { if (this.def.supply) this.hopper = new Hopper(this, count); }
  colliders() {
    const out = (STATION_COLL[this.type] || []).map(([a, b, c, d]) => ({ x0: this.x + a, x1: this.x + b, z0: this.z + c, z1: this.z + d }));
    if (this.hopper) out.push(...this.hopper.colliders());
    return out;
  }
  operatorPresent() {
    const p = Game.player;
    if (p.near(this.operatorSpot.x, this.operatorSpot.z, 1.15)) return 'player';
    const w = Game.workers.find(w => w.post === this && w.atPost && !w.napping);
    return w ? 'worker' : null;
  }
  tryStart(car) {
    if (this.def.supply && this.hopper.count <= 0) { this.setBlock('supply'); return false; }
    if (this.def.operator && !this.operatorPresent()) { this.setBlock('staff'); return false; }
    this.setBlock(null);
    if (this.hopper) this.hopper.consume();
    this.busy = true; this.car = car; this.t = 0; this.fxT = 0;
    this.dur = Game.stationTime(this.type);
    this.onStart(car);
    return true;
  }
  setBlock(b) {
    if (b === this.block) return;
    this.block = b;
    if (!b) { this.badge.set(''); return; }
    if (b === 'supply') this.badge.set(`<div class="st-badge">${ICONS.html(CFG.supplies[this.def.supply].icon)}NO ${CFG.supplies[this.def.supply].name.toUpperCase()}!</div>`);
    else this.badge.set(`<div class="st-badge warn">${ICONS.html('hand')}${this.type === 'pay' ? 'NEED CASHIER' : 'NEED POLISHER'}</div>`);
  }
  onStart(car) {
    const T = this.type;
    if (T === 'brush') { car.addFoam(); Sfx.play.scrub(); }
    else if (T === 'rinse') Sfx.play.spray();
    else if (T === 'dryer') Sfx.play.fan();
    else if (T === 'wax') Sfx.play.bubble();
  }
  update(dt) {
    if (this.machine && this.machine.anim.update) this.machine.anim.update(dt, this.busy);
    if (this.type === 'pay') {
      this.barrierT = Math.max(0, this.barrierT - dt);
      const want = this.barrierT > 0 ? 1.35 : 0;
      this.booth.barrier.rotation.x = U.damp(this.booth.barrier.rotation.x, want, 9, dt);
    }
    if (this.hopper) this.hopper.update(dt);
    if (!this.busy) {
      if (this.prog.html) { this.prog.set(''); this.lastProg = -1; }
      // a blocked car sits here: keep the badge, otherwise clear it
      if (this.block && !(this.occupant && this.occupant.state === 'drive')) this.setBlock(null);
      return;
    }
    // operator stations only run while someone is there
    if (this.def.operator && !this.operatorPresent()) { this.setBlock('staff'); return; }
    if (this.def.operator) this.setBlock(null);
    this.t += dt;
    this.fxT -= dt;
    const car = this.car, k = this.t / this.dur;
    this.stageFx(car, k, dt);
    const pr = Math.round(k * 20);
    if (pr !== this.lastProg) {
      this.lastProg = pr;
      const c = 2 * Math.PI * 15;
      this.prog.set(`<svg class="ring-prog" viewBox="0 0 40 40"><circle cx="20" cy="20" r="15" fill="rgba(12,24,44,.55)" stroke="rgba(255,255,255,.35)" stroke-width="5"/><circle cx="20" cy="20" r="15" fill="none" stroke="#46c23a" stroke-width="5" stroke-linecap="round" stroke-dasharray="${(c * Math.min(1, k)).toFixed(1)} ${c.toFixed(1)}" transform="rotate(-90 20 20)"/></svg>`);
    }
    if (this.t >= this.dur) this.finish();
  }
  stageFx(car, k, dt) {
    const x = this.x, z = this.z, T = this.type;
    if (T === 'brush') {
      if (this.fxT <= 0) {
        this.fxT = 0.07;
        FX.foamBurst(x + U.rand(-1.4, 1.4), U.rand(0.5, 1.8), z + (Math.random() < 0.5 ? -1 : 1) * U.rand(0.9, 1.4), 2, 0.25);
        if (Math.random() < 0.3) FX.bubbles(x, 2, z, 1);
        if (Math.random() < 0.15) Sfx.play.scrub();
      }
      car.setMud(U.lerp(1, 0.45, k));
      car.dirt = Math.min(car.dirt, U.lerp(1, 0.5, k)); car.dustTint();
    } else if (T === 'rinse') {
      if (this.fxT <= 0) { this.fxT = 0.05; FX.rain(x, z, 0.4, 1.7, 3); if (Math.random() < 0.5) FX.splash(x + U.rand(-1, 1), 0.1, z + U.rand(-1.5, 1.5), 3, 1.5); if (Math.random() < 0.08) Sfx.play.splash(); }
      if (k > 0.3 && car.coat.length) car.clearFoam();
      car.setMud(U.lerp(0.45, 0, U.clamp((k - 0.2) / 0.6, 0, 1)));
      car.dirt = Math.min(car.dirt, U.lerp(0.5, 0, k)); car.dustTint();
      if (car.flies && k > 0.5) { car.group.remove(car.flies); car.flies = null; }
      if (k > 0.5 && car.model.extras.washMe.visible) car.model.extras.washMe.visible = false;
    } else if (T === 'wax') {
      if (this.fxT <= 0) { this.fxT = 0.08; FX.foamBurst(x + U.rand(-1.2, 1.2), U.rand(0.9, 1.9), z + U.rand(-0.9, 0.9), 2, 0.3, [0xff6fb1, 0xffd23f, 0x4fa3ff]); }
      car.shine = Math.max(car.shine, k * 0.5); car.dustTint();
    } else if (T === 'dryer') {
      if (this.fxT <= 0) { this.fxT = 0.05; FX.wind(x, z, 0.9, 1.4, 2); if (k > 0.6 && Math.random() < 0.4) FX.sparkles(x, 1.6, z, 1, 1.4); }
      car.shine = Math.max(car.shine, 0.5 + k * 0.3); car.dustTint();
    } else if (T === 'tire') {
      if (this.fxT <= 0) { this.fxT = 0.12; for (const w of car.model.wheels) { const wp = new THREE.Vector3(); w.mesh.getWorldPosition(wp); if (Math.random() < 0.5) FX.sparkles(wp.x, 0.3, wp.z, 1, 0.2); } }
    } else if (T === 'polish') {
      if (this.fxT <= 0) { this.fxT = 0.1; FX.sparkles(x + U.rand(-1.5, 1.5), U.rand(0.8, 1.8), z + U.rand(-0.9, 0.9), 1, 0.3); if (Math.random() < 0.2) Sfx.play.scrub(); }
      car.shine = Math.max(car.shine, 0.8 + k * 0.2); car.dustTint();
    }
  }
  finish() {
    const car = this.car, T = this.type;
    this.busy = false; this.car = null;
    this.prog.set(''); this.lastProg = -1;
    if (T === 'rinse') { car.clearFoam(); car.setMud(0); car.dirt = 0; car.dustTint(); }
    if (T === 'brush') car.setMud(0.45);
    if (T === 'dryer' || T === 'polish') FX.sparkles(this.x, 1.5, this.z, T === 'polish' ? 10 : 5, 1.6);
    if (T === 'polish') { Sfx.play.squeak(); FX.text(new THREE.Vector3(this.x, car.model.h + 1.0, this.z), 'SO SHINY!', 'big', { dur: 1.1 }); }
    car.onProcessed(this);
    if (T === 'pay') { this.barrierT = 1.4; Game.payout(this, car); }
    Bus.emit('station:done', { st: this, car });
  }
  destroy() {
    GFX.scene.remove(this.group); this.badge.remove(); this.prog.remove();
    if (this.opDecal) GFX.scene.remove(this.opDecal);
    if (this.hopper) this.hopper.destroy();
  }
}

// ---------------------------------------------------------------------------
class Rack {
  constructor(supply, stock) {
    this.supply = supply; this.cfg = CFG.racks[supply];
    this.model = Models.rack(supply);
    const [x, z] = CFG.racksPos[supply];
    this.pos = new THREE.Vector3(x, 0, z);
    this.model.group.position.copy(this.pos);
    GFX.scene.add(this.model.group);
    this.mat = { x: x + 0.35, z: z + CFG.rackMatDZ };
    this.decal = MatDecal(CFG.supplies[supply].icon, '#2563eb', 1.7);
    this.decal.position.set(this.mat.x, 0.05, this.mat.z); GFX.scene.add(this.decal);
    this.items = [];
    this.t = 0;
    for (let i = 0; i < Math.min(stock, this.cap); i++) this.spawnItem(false);
  }
  get lv() { return Game.ls.racks[this.supply] || 1; }
  get cap() { return Math.min(this.model.slots.length, this.cfg.cap + Math.floor((this.lv - 1) / 2)); }
  get time() { return this.cfg.time * Math.pow(0.92, this.lv - 1); }
  get stock() { return this.items.length; }
  spawnItem(anim = true) {
    const m = Models.item(this.supply);
    m.position.copy(this.model.slots[this.items.length]);
    m.rotation.y = U.rand(-0.2, 0.2);
    this.model.group.add(m);
    this.items.push(m);
    if (anim) { popIn(m); this.puff = 1; }
  }
  take() {
    if (!this.items.length) return null;
    const m = this.items.pop();
    const wp = new THREE.Vector3(); m.getWorldPosition(wp);
    this.model.group.remove(m);
    return wp;
  }
  update(dt) {
    if (this.items.length < this.cap) {
      this.t += dt;
      if (this.t >= this.time) { this.t = 0; this.spawnItem(true); }
    } else this.t = 0;
    const g = this.model.giant;
    this.puff = U.damp(this.puff || 0, 0, 6, dt);
    const s = 2.7 * (1 + Math.sin(performance.now() / 120) * 0.04 * this.puff + this.puff * 0.08);
    g.scale.set(s, 2.7 * (1 - this.puff * 0.08), s);
    g.rotation.y += dt * 0.4;
  }
  colliders() {
    const { x, z } = this.pos;
    return [{ x0: x - 2.75, x1: x - 1.15, z0: z - 0.68, z1: z + 0.68 }, { x0: x - 0.95, x1: x + 1.65, z0: z - 0.66, z1: z + 0.66 }];
  }
  destroy() { GFX.scene.remove(this.model.group); GFX.scene.remove(this.decal); }
}

// ---------------------------------------------------------------------------
// Stack of cash bundles waiting to be collected (one per pay booth, plus the vending tray).
class CashPile {
  constructor(x, z, o = {}) {
    this.pos = new THREE.Vector3(x, 0, z);
    this.group = new THREE.Group(); this.group.position.copy(this.pos); GFX.scene.add(this.group);
    this.base = o.base != null ? o.base : 0.39;
    if (!o.noTable) { this.table = Models.cashTable(); this.group.add(this.table); }
    this.inst = new THREE.InstancedMesh(Models.cashBundleGeo(), MAT.vc, 64);
    this.inst.castShadow = true; this.inst.count = 0; this.group.add(this.inst);
    this.values = [];
    this.collectT = 0; this.combo = 0; this.comboT = 0;
    this.lbl = FX.label(new THREE.Vector3(x, 1.2, z), 0, '', '');
    this.rot = Array.from({ length: 64 }, () => U.rand(-0.15, 0.15));
  }
  get total() { return this.values.reduce((a, b) => a + b, 0); }
  slot(i, out) {
    const layer = Math.floor(i / 4), j = i % 4;
    return out.set((j % 2 ? 0.31 : -0.31), this.base + layer * 0.135, (j < 2 ? -0.19 : 0.19));
  }
  redraw() {
    const m = new THREE.Matrix4(), p = new THREE.Vector3(), q = new THREE.Quaternion(), s = new THREE.Vector3(1, 1, 1);
    const n = Math.min(64, this.values.length);
    for (let i = 0; i < n; i++) { this.slot(i, p); q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), this.rot[i]); m.compose(p, q, s); this.inst.setMatrixAt(i, m); }
    this.inst.count = n; this.inst.instanceMatrix.needsUpdate = true;
    const tot = this.total;
    this.lbl.set(tot > 0 ? `<div class="pile-lbl">${ICONS.html('cash')}${U.fmt(tot)}</div>` : '');
    this.lbl.anchor.y = 1.0 + Math.min(64, this.values.length) / 4 * 0.135 + 0.5;
  }
  add(amount, from, nBundles) {
    const n = nBundles || U.clamp(Math.ceil(Math.log2(1 + amount / Math.max(1, Game.basePrice()))) + 1, 2, 5);
    const each = amount / n;
    for (let i = 0; i < n; i++) {
      const b = FX.getBundle();
      const dest = new THREE.Vector3();
      setTimeout(() => {
        const idx = Math.min(63, this.values.length + i);
        this.slot(idx, dest); dest.add(this.pos);
        FX.fly(b, from.clone().add(new THREE.Vector3(U.rand(-0.3, 0.3), 0, U.rand(-0.3, 0.3))), dest, 0.35, 1.6, () => {
          FX.freeBundle(b);
          if (this.values.length >= 64) this.values[U.randi(0, 63)] += each; else this.values.push(each);
          this.redraw(); Sfx.play.tick();
        }, { spin: 4 });
      }, i * 70);
    }
  }
  restore(total) { if (total > 0) { const n = U.clamp(Math.round(total / Math.max(1, Game.basePrice() * 2)), 1, 24); for (let i = 0; i < n; i++) this.values.push(total / n); this.redraw(); } }
  update(dt) {
    this.comboT -= dt; if (this.comboT <= 0) this.combo = 0;
    if (!this.values.length) return;
    const p = Game.player;
    if (!p.near(this.pos.x, this.pos.z, Game.magnet())) return;
    this.collectT -= dt;
    while (this.collectT <= 0 && this.values.length) {
      this.collectT += 0.035;
      const i = this.values.length - 1, v = this.values.pop();
      const from = new THREE.Vector3(); this.slot(Math.min(63, i), from); from.add(this.pos);
      const b = FX.getBundle();
      const c = this.combo++; this.comboT = 0.6;
      FX.fly(b, from, () => p.pos.clone().setY(1.3), 0.22, 0.9, () => { FX.freeBundle(b); Game.addCash(v, null, { silent: true }); Sfx.play.coin(c * 0.5); }, { scaleTo: 0.4 });
      if (c === 0) Game.cnt('collect');
      this.redraw();
      if (!this.values.length) { Bus.emit('cash:collected', this); U.vibrate(10); }
    }
  }
  destroy() { GFX.scene.remove(this.group); this.lbl.remove(); }
}

// ---------------------------------------------------------------------------
class Pad {
  constructor(def, M, paid) {
    this.def = def; this.id = def.id;
    this.cost = Math.round(def.cost * M);
    this.paid = Math.min(paid || 0, this.cost - 1);
    const p = Game.padPos(def);
    this.pos = new THREE.Vector3(p[0], 0, p[1]);
    this.canvas = document.createElement('canvas'); this.canvas.width = this.canvas.height = 256;
    this.ctx = this.canvas.getContext('2d');
    this.tex = new THREE.CanvasTexture(this.canvas);
    this.icon = Game.padIcon(def);
    this.mesh = new THREE.Mesh(GEO.plane(2.8, 2.8), new THREE.MeshBasicMaterial({ map: this.tex, transparent: true, depthWrite: false }));
    this.mesh.rotation.x = -Math.PI / 2; this.mesh.position.set(this.pos.x, 0.07, this.pos.z); this.mesh.renderOrder = 2;
    this.name = FX.label(new THREE.Vector3(this.pos.x, 0.2, this.pos.z + 1.75), 0, `<div class="mat-lbl">${Game.padName(def)}</div>`, '');
    this.name.show(false);
    this.shown = false; this.tick = 0; this.dirty = true; this.payT = 0; this.coinN = 0; this.hintT = 0;
    this.draw();
  }
  get remaining() { return Math.max(0, this.cost - this.paid); }
  draw() { Models.drawPad(this.ctx, 256, 256, { icon: this.icon }, this.remaining, this.cost); this.tex.needsUpdate = true; this.dirty = false; }
  show(anim = true) {
    if (this.shown) return;
    this.shown = true; GFX.scene.add(this.mesh); this.name.show(true);
    if (anim) { popIn(this.mesh, 0.3); FX.sparkles(this.pos.x, 0.5, this.pos.z, 6, 1); }
  }
  update(dt) {
    if (!this.shown) return;
    this.tick += dt;
    const pulse = 1 + Math.sin(this.tick * 4) * 0.025;
    if (this.mesh.scale.x > 0.9) this.mesh.scale.setScalar(pulse);
    const p = Game.player;
    const inside = Math.abs(p.pos.x - this.pos.x) < 1.4 && Math.abs(p.pos.z - this.pos.z) < 1.4;
    if (!inside) { this.coinN = 0; this.hintT = 0; return; }
    const S = Store.s;
    if (S.cash < 1) {
      this.hintT += dt;
      if (this.hintT > 0.5 && this.hintT - dt <= 0.5) Bus.emit('pad:broke', this);
      return;
    }
    // drain cash into the pad: always ~1.3s for a full pad
    const rate = Math.max(this.cost / 1.3, 12);
    const amt = Math.min(this.remaining, S.cash, rate * dt);
    if (amt <= 0) return;
    Game.spend(amt, true);
    this.paid += amt;
    Game.ls.paid[this.id] = this.paid;
    this.payT -= dt;
    if (this.payT <= 0) {
      this.payT = 0.07;
      const b = FX.getBundle();
      FX.fly(b, p.pos.clone().setY(1.3), this.pos.clone().setY(0.2), 0.25, 1.2, () => FX.freeBundle(b), { scaleTo: 0.5 });
      Sfx.play.coin(Math.min(20, this.coinN++ * 0.6));
      this.draw();
    }
    if (this.remaining <= 0.5) { this.paid = this.cost; this.draw(); Game.completePad(this); }
  }
  hide() { GFX.scene.remove(this.mesh); this.name.remove(); this.shown = false; }
}

// ---------------------------------------------------------------------------
class Lane {
  constructor(idx) {
    this.idx = idx; this.z = CFG.laneZ[idx];
    this.stations = {}; this.cars = [];
    this.unlocked = false; this.entrance = null; this.exit = null; this.pile = null;
  }
  unlock(anim) {
    if (this.unlocked) return;
    this.unlocked = true;
    this.entrance = Models.gate('ENTRANCE'); this.entrance.group.position.set(CFG.entranceX, 0, this.z); GFX.scene.add(this.entrance.group);
    this.exit = Models.gate('EXIT'); this.exit.group.position.set(CFG.exitX, 0, this.z); GFX.scene.add(this.exit.group);
    this.pile = new CashPile(CFG.slotX.pay + CFG.cashDX, this.z + CFG.cashDZ);
    if (this.idx > 0) World.openLane2();
    if (anim) { popIn(this.entrance.group, 0.1); popIn(this.exit.group, 0.25); }
  }
  addStation(type, anim, hopperCount) {
    const st = new Station(this, type);
    st.makeHopper(hopperCount != null ? hopperCount : 3);
    this.stations[type] = st;
    if (anim) { popIn(st.group); if (st.hopper) popIn(st.hopper.model.group, 0.15); }
    this._stops = null;
    return st;
  }
  get queue() { return this.cars.filter(c => c.waiting); }
  stops() { return this._stops || (this._stops = CFG.slotOrder.map(t => this.stations[t]).filter(Boolean)); }
  firstStop() { return this.stops()[0] || null; }
  nextStop(st) { const s = this.stops(); const i = s.indexOf(st); return i >= 0 ? s[i + 1] || null : null; }
  colliders() {
    const out = [];
    for (const st of Object.values(this.stations)) out.push(...st.colliders());
    if (this.unlocked) {
      for (const gx of [CFG.entranceX, CFG.exitX]) for (const sz of [-1, 1]) out.push({ x0: gx - 0.36, x1: gx + 0.36, z0: this.z + sz * 2.35 - 0.36, z1: this.z + sz * 2.35 + 0.36 });
      const px = CFG.slotX.pay + CFG.cashDX, pz = this.z + CFG.cashDZ;
      out.push({ x0: px - 0.85, x1: px + 0.85, z0: pz - 0.62, z1: pz + 0.62 });
    }
    return out;
  }
  destroy() {
    for (const st of Object.values(this.stations)) st.destroy();
    if (this.entrance) GFX.scene.remove(this.entrance.group);
    if (this.exit) GFX.scene.remove(this.exit.group);
    if (this.pile) this.pile.destroy();
  }
}

// ---------------------------------------------------------------------------
// Buildings and decorations bought with pads.
class Building {
  constructor(id, anim, theme) {
    this.id = id;
    const cfg = CFG.buildings[id];
    this.pos = new THREE.Vector3(cfg.pos[0], 0, cfg.pos[1]);
    let g, coll = [];
    const P = this.pos;
    const box = (hx, hz, ox = 0, oz = 0) => coll.push({ x0: P.x + ox - hx, x1: P.x + ox + hx, z0: P.z + oz - hz, z1: P.z + oz + hz });
    switch (id) {
      case 'shop': { const s = Models.shop(); g = s.group; this.keeper = s.keeper; box(6.3, 3.3); break; }
      case 'staff': g = Models.staffRoom(); box(3.25, 2.55); box(1.0, 0.3, 2.0, 2.9); break;
      case 'office': g = Models.office(); box(2.75, 2.45); break;
      case 'vip': g = Models.vipLounge(); box(3.55, 2.55); break;
      case 'coffee': g = Models.coffeeMachine(); box(0.7, 0.55); break;
      case 'vending': g = Models.vending(); box(0.7, 0.5); break;
      case 'queue': g = Models.waitingArea(); box(2.4, 0.5); break;
      case 'tubeman': { const t = Models.tubeMan(theme === 'city' ? 0xff3df2 : 0xff4d5a); g = t.group; this.tm = t; box(0.55, 0.55); break; }
      case 'fountain': { const f = Models.fountain(); g = f.group; this.jetY = f.jetY; box(2.35, 2.35); break; }
      case 'billboard': g = Models.billboard(); g.rotation.y = 0.25; box(0.3, 0.3, -2.3, 0.6); box(0.3, 0.3, 2.3, -0.6); break;
      case 'flowers': {
        g = new THREE.Group();
        const spots = [[0, 0, 3.4], [-14.5, 1.4, 3.0], [3.7, 2.0, 2.6]];
        for (const [dx, dz, l] of spots) { const fb = Models.flowerBed(l); fb.position.set(dx, 0, dz); g.add(fb); box(l / 2 + 0.05, 0.62, dx, dz); }
        break;
      }
      case 'landmark': { g = Models.landmark(Game.L.landmark); box(2.3, 2.3); break; }
      case 'travel': {
        g = new THREE.Group();
        const s = Models.signBoard('ROAD TRIP', 2.4, 0.7, '#ff8a1f', '#ffffff', 'road');
        s.position.set(0, 2.3, -1.2); g.add(s);
        const b = new Builder(); b.cyl(0.08, 0.08, 2.0, 6, 0x7d8796, { x: -0.8, y: 1.0, z: -1.25 }); b.cyl(0.08, 0.08, 2.0, 6, 0x7d8796, { x: 0.8, y: 1.0, z: -1.25 }); g.add(b.build());
        box(1.0, 0.2, 0, -1.25);
        this.mat = { x: P.x, z: P.z + 0.4, panel: 'map' };
        break;
      }
    }
    this.group = g; g.position.add(this.pos); GFX.scene.add(g);
    this.coll = coll;
    if (cfg.mat) this.mat = { x: cfg.mat[0], z: cfg.mat[1], panel: { shop: 'shop', staff: 'staff', office: 'office' }[id] };
    if (this.mat) {
      const icon = { shop: 'bag', staff: 'staff', office: 'office', map: 'map' }[this.mat.panel];
      this.decal = MatDecal(icon, '#ff8a1f', 1.8); this.decal.position.set(this.mat.x, 0.06, this.mat.z); GFX.scene.add(this.decal);
      this.matLbl = FX.label(new THREE.Vector3(this.mat.x, 0.15, this.mat.z + 1.15), 0, `<div class="mat-lbl">${{ shop: 'SHOP', staff: 'STAFF', office: 'UPGRADES', map: 'MAP' }[this.mat.panel]}</div>`, '');
      this.inMat = false;
    }
    if (id === 'vending') this.tray = new CashPile(this.pos.x + 1.25, this.pos.z + 0.7, { noTable: true, base: 0.02 });
    this.t = Math.random() * 10; this.coinT = 4;
    if (anim) { popIn(g); FX.confetti(this.pos.x, 2, this.pos.z, 50, 7); }
  }
  update(dt) {
    this.t += dt;
    if (this.tm) this.tm.update(this.t);
    if (this.jetY && Math.random() < dt * 14) {
      const a = Math.random() * Math.PI * 2;
      FX.sys.water.spawn({ x: this.pos.x, y: this.jetY, z: this.pos.z, vx: Math.cos(a) * 1.5, vy: U.rand(3, 4.5), vz: Math.sin(a) * 1.5, life: 0.9, s0: 1.8, s1: 1.0, g: 9, floor: 0.6 });
    }
    if (this.tray) {
      this.tray.update(dt);
      this.coinT -= dt;
      if (this.coinT <= 0) {
        this.coinT = 6;
        const waiting = Game.lanes.reduce((a, l) => a + l.queue.length, 0);
        if (waiting > 0) {
          const v = Math.max(1, Math.round(Game.basePrice() * 0.35 * waiting));
          this.tray.add(v, this.pos.clone().setY(1.2), 1);
          if (Game.player.near(this.pos.x, this.pos.z, 25)) FX.text(this.pos.clone().setY(2.6), 'SNACK SALE!', 'tip', { dur: 1 });
        }
      }
    }
    if (this.mat) {
      const inside = Game.player.near(this.mat.x, this.mat.z, 1.0);
      if (inside && !this.inMat && Game.player.speed01 < 0.95) { this.inMat = true; Bus.emit('mat:enter', this.mat.panel); }
      else if (!inside && this.inMat && !Game.player.near(this.mat.x, this.mat.z, 1.5)) this.inMat = false;
    }
  }
  destroy() {
    GFX.scene.remove(this.group);
    if (this.decal) GFX.scene.remove(this.decal);
    if (this.matLbl) this.matLbl.remove();
    if (this.tray) this.tray.destroy();
  }
}
