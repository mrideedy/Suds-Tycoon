// entities.js — the manager, staff, the stacks they carry, and customer cars.

// ---------------------------------------------------------------------------
// A stack of supplies carried in front of a person (Pizza-Ready style).
// ---------------------------------------------------------------------------
class CarryStack {
  constructor(owner, capFn) {
    this.owner = owner; this.capFn = capFn;
    this.items = [];      // { type, mesh, landed }
    this.holder = new THREE.Group();
    this.holder.position.set(0, 0.98, 0.5);
    owner.model.body.add(this.holder);
    this.sway = 0;
  }
  get count() { return this.items.length; }
  get cap() { return this.capFn(); }
  get full() { return this.items.length >= this.cap; }
  countOf(type) { let n = 0; for (const it of this.items) if (it.type === type) n++; return n; }
  slotWorld(i, out) { out.set(0, i * 0.44, 0); return this.holder.localToWorld(out); }
  add(type, fromWorld) {
    const mesh = Models.item(type);
    const it = { type, mesh, landed: false };
    const idx = this.items.length;
    this.items.push(it);
    const tmp = new THREE.Vector3();
    FX.fly(mesh, fromWorld, () => this.slotWorld(this.items.indexOf(it) >= 0 ? this.items.indexOf(it) : idx, tmp), 0.22, 0.9, () => {
      if (this.items.indexOf(it) < 0) { if (mesh.parent) mesh.parent.remove(mesh); return; }
      this.holder.add(mesh); mesh.rotation.set(0, 0, 0); it.landed = true; this.layout();
    }, { spin: 8 });
    return it;
  }
  // remove the top-most item of a type; returns its world position (mesh is flown elsewhere by caller)
  take(type) {
    for (let i = this.items.length - 1; i >= 0; i--) {
      if (this.items[i].type !== type) continue;
      const it = this.items[i];
      this.items.splice(i, 1);
      const wp = new THREE.Vector3();
      it.mesh.getWorldPosition(wp);
      if (it.mesh.parent) it.mesh.parent.remove(it.mesh);
      this.layout();
      return { mesh: it.mesh, pos: wp };
    }
    return null;
  }
  clear() { for (const it of this.items) if (it.mesh.parent) it.mesh.parent.remove(it.mesh); this.items.length = 0; }
  layout() {
    this.items.forEach((it, i) => { if (it.landed) it.mesh.position.set(0, i * 0.44, 0); });
  }
  update(dt, speed01) {
    this.sway = U.damp(this.sway, speed01, 5, dt);
    const t = performance.now() / 1000;
    this.items.forEach((it, i) => {
      if (!it.landed) return;
      it.mesh.position.z = -this.sway * i * 0.035 + Math.sin(t * 9 + i * 0.6) * 0.008 * i * this.sway;
      it.mesh.position.x = Math.sin(t * 7 + i * 0.5) * 0.01 * i * this.sway;
      it.mesh.position.y = U.damp(it.mesh.position.y, i * 0.44, 18, dt);
    });
  }
}

// ---------------------------------------------------------------------------
// The manager (player character)
// ---------------------------------------------------------------------------
class Player {
  constructor() {
    this.model = Models.person({ shirt: 0xffffff, vest: PAL.blue, star: true, badge: true, hat: Store.s.hats.on, scale: 1.08 });
    this.pos = new THREE.Vector3(CFG.playerStart[0], 0, CFG.playerStart[1]);
    this.vel = new THREE.Vector3();
    this.facing = Math.PI;
    this.radius = 0.45;
    this.stack = new CarryStack(this, () => Game.mgrCarry());
    this.moveDir = new THREE.Vector3();
    GFX.scene.add(this.model.group);
    this.maxLabel = FX.label(() => this.stackTop(), 0.4, '<span class="carry-cnt max">MAX</span>', '');
    this.maxLabel.show(false);
    this.shadow = blobShadow(1.2); GFX.scene.add(this.shadow);
    this.idleT = 0;
  }
  stackTop() { const v = new THREE.Vector3(); this.stack.slotWorld(this.stack.count, v); return v; }
  get speed01() { return U.clamp(this.vel.length() / Game.mgrSpeed(), 0, 1); }
  update(dt, input) {
    const spd = Game.mgrSpeed();
    const want = this.moveDir.set(input.x, 0, input.z);
    const mag = Math.min(1, want.length());
    if (mag > 0.01) want.normalize().multiplyScalar(spd * mag);
    const k = mag > 0.01 ? 14 : 18;
    this.vel.x = U.damp(this.vel.x, want.x, k, dt);
    this.vel.z = U.damp(this.vel.z, want.z, k, dt);
    this.pos.x += this.vel.x * dt; this.pos.z += this.vel.z * dt;
    const B = CFG.walkBounds;
    this.pos.x = U.clamp(this.pos.x, B.x0, B.x1); this.pos.z = U.clamp(this.pos.z, B.z0, B.z1);
    Nav.resolve(this.pos, this.radius);
    if (this.vel.lengthSq() > 0.05) this.facing = U.angleLerp(this.facing, Math.atan2(this.vel.x, this.vel.z), 1 - Math.exp(-14 * dt));
    this.model.group.position.copy(this.pos);
    this.model.group.rotation.y = this.facing;
    const s01 = this.speed01;
    this.model.animate(dt, s01, this.stack.count > 0, Game.polishingPlayer ? 'polish' : null);
    this.stack.update(dt, s01);
    this.maxLabel.show(this.stack.full && this.stack.count > 0);
    this.shadow.position.set(this.pos.x, 0.03, this.pos.z);
    if (s01 < 0.05) this.idleT += dt; else this.idleT = 0;
  }
  near(x, z, r) { return U.dist2(this.pos.x, this.pos.z, x, z) < r * r; }
}

function blobShadow(size) {
  const m = new THREE.Mesh(GEO.plane(size, size), new THREE.MeshBasicMaterial({ map: TEX.soft(), transparent: true, depthWrite: false, opacity: 0.5 }));
  m.rotation.x = -Math.PI / 2;
  return m;
}

// ---------------------------------------------------------------------------
// Staff: stocker / cashier / polisher / turbo (temporary super stocker)
// ---------------------------------------------------------------------------
class Worker {
  constructor(role, o = {}) {
    this.role = role; this.lane = o.lane != null ? o.lane : null; this.name = o.name || 'Worker';
    this.turbo = role === 'turbo';
    const looks = {
      stocker: { shirt: 0x4f8cff, vest: 0xff8a1f },
      cashier: { shirt: 0xffffff, vest: 0x46c23a },
      polisher: { shirt: 0xb44cff, vest: 0xffc93c },
      turbo: { shirt: 0xffd23f, vest: 0xff4d5a, cap: 0xffc92e },
    }[role];
    this.model = Models.person(Object.assign({ skin: U.pick(PAL.skin), pants: 0x2f4f9e }, looks, { hat: this.turbo ? 'cap' : 'cap', cap: looks.cap || PAL.blue }));
    const door = Game.staffDoor();
    this.pos = new THREE.Vector3(door.x + U.rand(-0.5, 0.5), 0, door.z + U.rand(0, 0.6));
    this.facing = 0;
    this.path = []; this.state = 'idle'; this.wait = 0;
    this.stack = new CarryStack(this, () => (this.turbo ? 10 : Game.staffCarry()));
    this.napIn = U.rand(70, 140); this.napping = false; this.napT = 0;
    this.speedNow = 0; this.task = null; this.atPost = false; this.loadT = 0; this.stuck = 0;
    GFX.scene.add(this.model.group);
    this.shadow = blobShadow(1.1); GFX.scene.add(this.shadow);
    this.tag = FX.label(this.model.group, 2.3, `<span class="name-tag">${this.name}</span>`, '');
    this.zzz = FX.label(this.model.group, 2.0, '<span class="zzz">z z Z</span>', '');
    this.zzz.show(false);
    this.life = this.turbo ? CFG.boosts.turbo.add : Infinity;
    this.model.group.position.copy(this.pos);
  }
  get spd() { return this.turbo ? 9 : Game.staffSpeed(); }
  goTo(x, z) { this.path = Nav.find(this.pos.x, this.pos.z, x, z); this.goal = { x, z }; this.stuck = 0; }
  walk(dt) {
    if (!this.path.length) { this.speedNow = U.damp(this.speedNow, 0, 10, dt); return true; }
    const p = this.path[0], dx = p.x - this.pos.x, dz = p.z - this.pos.z, d = Math.hypot(dx, dz);
    const step = this.spd * dt;
    if (d <= step) { this.pos.x = p.x; this.pos.z = p.z; this.path.shift(); }
    else { this.pos.x += (dx / d) * step; this.pos.z += (dz / d) * step; this.facing = U.angleLerp(this.facing, Math.atan2(dx, dz), 1 - Math.exp(-12 * dt)); }
    this.speedNow = U.damp(this.speedNow, 1, 10, dt);
    return !this.path.length;
  }
  sleep() {
    if (this.turbo || this.napping) return;
    this.napping = true; this.napT = U.rand(8, 14); this.zzz.show(true);
    if (Game.player.near(this.pos.x, this.pos.z, 9)) Sfx.play.snore();
    Bus.emit('worker:nap', this);
  }
  wake(byPlayer) {
    if (!this.napping) return;
    this.napping = false; this.zzz.show(false);
    this.napIn = U.rand(70, 140) / Game.napMul();
    this.model.body.rotation.x = 0;
    if (byPlayer) { Sfx.play.wake(); FX.text(this.pos.clone().setY(2.4), '!', 'big', { dur: 0.7, rise: 30 }); Bus.emit('worker:woke', this); }
  }
  update(dt) {
    if (this.turbo) {
      this.life -= dt;
      if (this.life <= 0) { this.dismiss(); return; }
    }
    // naps
    if (this.napping) {
      this.napT -= dt;
      this.model.animate(dt, 0, this.stack.count > 0, 'sleep');
      if (Game.player.near(this.pos.x, this.pos.z, 2.2)) this.wake(true);
      else if (this.napT <= 0) this.wake(false);
      this.sync(); return;
    }
    if (!this.turbo) { this.napIn -= dt * Game.napMul(); if (this.napIn <= 0) { this.sleep(); this.sync(); return; } }
    if (this.role === 'stocker' || this.role === 'turbo') this.stockerAI(dt);
    else this.postAI(dt);
    this.sync();
  }
  sync() {
    this.model.group.position.copy(this.pos);
    this.model.group.rotation.y = this.facing;
    const extra = this.napping ? 'sleep' : (this.role === 'polisher' && this.atPost && this.post && this.post.busy) ? 'polish' : null;
    if (!this.napping) this.model.animate(1 / 60, this.speedNow, this.stack.count > 0, extra);
    this.stack.update(1 / 60, this.speedNow);
    this.shadow.position.set(this.pos.x, 0.03, this.pos.z);
  }
  // cashier / polisher: walk to the post and stay there
  postAI(dt) {
    const st = Game.postFor(this.role, this.lane);
    this.post = st;
    if (!st) { this.atPost = false; return; }
    const spot = st.operatorSpot;
    if (!this.atPost) {
      if (!this.path.length || !this.goal || U.dist2(this.goal.x, this.goal.z, spot.x, spot.z) > 0.01) this.goTo(spot.x, spot.z);
      if (this.walk(dt) || U.dist2(this.pos.x, this.pos.z, spot.x, spot.z) < 0.04) { this.atPost = true; this.path = []; }
    } else {
      this.speedNow = U.damp(this.speedNow, 0, 10, dt);
      this.facing = U.angleLerp(this.facing, st.operatorFacing, 1 - Math.exp(-8 * dt));
      if (U.dist2(this.pos.x, this.pos.z, spot.x, spot.z) > 0.25) this.atPost = false;
    }
  }
  stockerAI(dt) {
    if (this.wait > 0) { this.wait -= dt; this.speedNow = U.damp(this.speedNow, 0, 10, dt); return; }
    switch (this.state) {
      case 'idle': {
        const t = Game.pickStockTask(this);
        if (t) {
          this.task = t; t.hopper.incoming += t.amount;
          if (this.stack.countOf(t.supply) >= 1) { this.state = 'toHopper'; this.goTo(t.hopper.mat.x, t.hopper.mat.z); }
          else { this.state = 'toRack'; this.goTo(t.rack.mat.x, t.rack.mat.z); }
        } else if (this.stack.count) {
          // carrying leftovers nobody needs right now: wait near the racks
          this.state = 'wander'; this.goTo(U.rand(-10, 6), U.rand(4.6, 5.6)); this.wait = 0;
        } else { this.state = 'wander'; this.goTo(U.rand(-14, 8), U.rand(4.5, 5.8)); }
        break;
      }
      case 'wander':
        if (this.walk(dt)) { this.wait = U.rand(1.0, 2.5); this.state = 'idle'; }
        break;
      case 'toRack':
        if (this.walk(dt)) { this.state = 'loading'; this.loadT = 0; this.waitedRack = 0; }
        break;
      case 'loading': {
        const r = this.task.rack;
        this.speedNow = U.damp(this.speedNow, 0, 10, dt);
        this.facing = U.angleLerp(this.facing, Math.PI * 0, 1 - Math.exp(-8 * dt));
        this.loadT -= dt;
        const have = this.stack.countOf(this.task.supply);
        if (have >= this.task.amount || this.stack.full) { this.state = 'toHopper'; this.goTo(this.task.hopper.mat.x, this.task.hopper.mat.z); break; }
        if (this.loadT <= 0 && r.stock > 0) { const p = r.take(); if (p) { this.stack.add(this.task.supply, p); Sfx.play.pop(); } this.loadT = this.turbo ? 0.06 : 0.14; }
        if (r.stock <= 0) { this.waitedRack += dt; if (this.waitedRack > 3 && have > 0) { this.state = 'toHopper'; this.goTo(this.task.hopper.mat.x, this.task.hopper.mat.z); } }
        break;
      }
      case 'toHopper':
        if (this.walk(dt)) { this.state = 'unloading'; this.loadT = 0; }
        break;
      case 'unloading': {
        const h = this.task.hopper;
        this.speedNow = U.damp(this.speedNow, 0, 10, dt);
        this.facing = U.angleLerp(this.facing, Math.PI, 1 - Math.exp(-8 * dt));
        this.loadT -= dt;
        if (this.loadT <= 0) {
          if (h.count < h.cap && this.stack.countOf(this.task.supply) > 0) {
            const it = this.stack.take(this.task.supply);
            h.receive(it); h.incoming = Math.max(0, h.incoming - 1); this.task.amount--;
            this.loadT = this.turbo ? 0.06 : 0.12;
          } else {
            h.incoming = Math.max(0, h.incoming - Math.max(0, this.task.amount)); this.task = null; this.state = 'idle';
          }
        }
        break;
      }
    }
  }
  dropTask() { if (this.task) { this.task.hopper.incoming = Math.max(0, this.task.hopper.incoming - Math.max(0, this.task.amount)); this.task = null; } }
  dismiss() {
    this.dropTask();
    FX.smoke(this.pos.x, 0.8, this.pos.z, 8, 0.6); Sfx.play.poof();
    this.destroy();
    Game.removeWorker(this);
  }
  destroy() {
    this.stack.clear();
    GFX.scene.remove(this.model.group); GFX.scene.remove(this.shadow);
    this.tag.remove(); this.zzz.remove();
  }
}

// ---------------------------------------------------------------------------
// Customer cars
// ---------------------------------------------------------------------------
const PAINT_FOR = {
  taxi: 0xffc93c, police: 0xf5f6fa, icecream: 0xf5f6fa, limo: 0x22252c, duck: 0xffd23f, hotdog: 0xd9a066, ufo: 0xc8d0dc,
  rover: 0xf2f4f8, snowplow: 0xff8a1f, tractor: 0x46c23a, golden: 0xffc92e, pizza: 0xe8383d, buggy: null, sports: null, monster: null,
};
class Car {
  constructor(type, lane, o = {}) {
    this.type = type; this.def = CFG.cars[type]; this.lane = lane;
    const paint = PAINT_FOR[type] != null ? PAINT_FOR[type] : U.pick(CFG.paintColors);
    this.paint = new THREE.Color(paint);
    this.model = Models.car(type, paint);
    this.group = this.model.group;
    GFX.scene.add(this.group);
    this.x = o.x != null ? o.x : CFG.spawnX - U.rand(0, 6); this.z = lane.z;
    this.v = CFG.roadSpeed * 0.8;
    this.state = 'drive'; this.stop = null; this.doneX = -Infinity;
    this.patMax = Game.patience(this.def); this.patience = this.patMax; this.waited = 0;
    this.bill = 0; this.stages = 0; this.dirt = 1; this.shine = 0;
    this.golden = type === 'golden'; this.goldenMult = this.golden ? 3 : 1;
    this.vip = !!this.def.vip;
    this.mood = 'happy';
    const nMud = Math.round(U.rand(10, 15) * (this.def.dirt || 1));
    this.mud = FX.addMud(this, nMud, this.def.dirt || 1);
    this.coat = [];
    this.model.extras.washMe.visible = this.model.extras.washMe.visible && Math.random() < 0.55;
    this.dustTint();
    this.label = FX.label(this.group, this.model.h + 0.75, '', '');
    this.sayLbl = null; this.sayT = 0; this.honkT = U.rand(4, 9);
    this.bounce = 0; this.wheelSpin = 0;
    this.flies = (this.def.dirt || 1) > 1.2 || Math.random() < 0.25 ? this.makeFlies() : null;
    this.group.position.set(this.x, 0, this.z);
    this.group.rotation.y = 0;
    this.t = Math.random() * 10;
    if (this.golden) { this.goldLbl = FX.label(this.group, this.model.h + 1.6, `<div class="st-badge info" style="background:#d99a00">${ICONS.html('golden')}GOLDEN x${this.goldenMult}</div>`, ''); }
    this.updateLabel(true);
  }
  makeFlies() {
    const g = new THREE.Group();
    for (let i = 0; i < 3; i++) { const f = new THREE.Mesh(GEO.sphere(0.06, 6, 4), MAT.lam(0x1b1b1b)); g.add(f); }
    this.group.add(g); g.position.y = this.model.h + 0.3;
    return g;
  }
  dustTint() {
    const d = U.clamp(this.dirt, 0, 1);
    const dusty = this.paint.clone().lerp(new THREE.Color(0x8b6a3e), 0.45);
    this.model.paintMat.color.copy(this.paint).lerp(dusty, d);
    if (!this.golden) this.model.paintMat.emissive.setRGB(0.12 * this.shine, 0.12 * this.shine, 0.13 * this.shine);
  }
  setMud(target) { for (const m of this.mud) m.target = Math.min(m.target, target); }
  addFoam() { if (!this.coat.length) this.coat = FX.addCoat(this, Math.round(this.model.len * 7)); }
  clearFoam() { for (const c of this.coat) { c.target = 0; c.dead = true; } this.coat = []; }
  // --- queue + line movement (lanes run along +x) ---
  moveTo(tx, dt, vmax, acc = 9) {
    const d = tx - this.x;
    if (d <= 0.01) { this.x = Math.max(this.x, Math.min(this.x, tx)); this.v = 0; return true; }
    const vt = Math.min(vmax, Math.sqrt(2 * 7 * d) + 0.2);
    this.v = this.v < vt ? Math.min(vt, this.v + acc * dt) : vt;
    this.x = Math.min(tx, this.x + this.v * dt);
    return tx - this.x < 0.02;
  }
  // Lanes are conveyors: each car drives toward its next unvisited station but never closer than a
  // safe gap to the car ahead, and only occupies a station while it is stopped there.
  ahead() { const a = this.lane.cars, i = a.indexOf(this); return i > 0 ? a[i - 1] : null; }
  nextStation() { const s = this.lane.stops(); for (const st of s) if (st.x > this.doneX + 0.1) return st; return null; }
  get waiting() { return this.x < CFG.entranceX - 0.3 && this.state === 'drive'; }
  update(dt) {
    this.t += dt;
    const L = this.lane;
    if (this.state === 'drive') {
      const st = this.nextStation();
      let tx = st ? st.x : CFG.despawnX + 20;
      const a = this.ahead();
      if (a && !a.dead) tx = Math.min(tx, a.x - ((a.model.len + this.model.len) / 2 + 0.9));
      const far = (st ? st.x : tx) - this.x;
      const vmax = this.x < CFG.entranceX - 4 ? CFG.roadSpeed : (far > 7 ? CFG.lineSpeed * 2.1 : CFG.lineSpeed);
      if (tx > this.x) this.moveTo(tx, dt, vmax, this.x < CFG.entranceX ? 9 : 6);
      else this.v = U.damp(this.v, 0, 12, dt);
      if (!st) { this.leave(); }
      else if (Math.abs(this.x - st.x) < 0.06 && (!st.occupant || st.occupant === this)) {
        if (!st.occupant) { st.occupant = this; this.bounce = 1; if (st === L.firstStop()) this.enteredLine(); }
        if (st.tryStart(this)) { this.state = 'proc'; this.stop = st; }
        else this.waited += dt * 0.5;
      }
      // patience only drains while waiting outside the entrance
      if (this.waiting && this.x > CFG.entranceX - 45 && this.v < 0.8) {
        this.waited += dt;
        this.patience -= dt * (Game.rushHour ? 0.6 : 1);
        this.honkT -= dt;
        if (this.patience < this.patMax * 0.45 && this.honkT <= 0) { this.honk(); this.honkT = U.rand(4, 8); }
        if (this.patience <= 0) { this.rageQuit(); return; }
        if (!this.sayLbl && this.waited > 2 && Math.random() < dt * 0.06) this.say(U.pick(CFG.driverLines));
      }
    } else if (this.state === 'proc') {
      this.v = 0;
    } else if (this.state === 'leave') {
      this.moveTo(CFG.despawnX + 10, dt, CFG.roadSpeed * 1.2, 5);
      if (this.x > CFG.despawnX) { this.destroy(); return; }
    }
    // visuals
    this.group.position.x = this.x;
    for (const w of this.model.wheels) w.mesh.rotation.z -= (this.v * dt) / w.r;
    this.bounce = U.damp(this.bounce, 0, 6, dt);
    const sq = Math.sin(this.t * 22) * this.bounce * 0.06;
    this.model.inner.scale.set(1 + sq * 0.5, 1 - sq, 1 + sq * 0.5);
    this.model.inner.rotation.z = -U.clamp(this.v - (this.prevV || 0), -2, 2) * 0.02;
    this.prevV = this.v;
    if (this.model.extras.hover) this.model.extras.hover.position.y = 1.25 + Math.sin(this.t * 3) * 0.12;
    if (this.model.extras.siren) { const on = Math.floor(this.t * 6) % 2; this.model.extras.siren[0].visible = !!on; this.model.extras.siren[1].visible = !on; }
    if (this.model.extras.lights) this.model.extras.lights.forEach((l, i) => { l.visible = (Math.floor(this.t * 8) + i) % 2 === 0; });
    if (this.flies) this.flies.children.forEach((f, i) => { const a = this.t * (5 + i) + i * 2; f.position.set(Math.cos(a) * 0.9, Math.sin(a * 1.7) * 0.3, Math.sin(a) * 0.7); });
    if (this.sayLbl) { this.sayT -= dt; if (this.sayT <= 0) { this.sayLbl.remove(); this.sayLbl = null; } }
    this.updateLabel();
  }
  honk() {
    const type = this.type;
    if (type === 'duck') Sfx.play.quack(); else if (type === 'police') Sfx.play.siren(); else if (type === 'icecream') Sfx.play.jingle(); else Sfx.play.honk(this.def.honky ? 1.1 : 1);
    if (Game.player.near(this.x, this.z, 30)) FX.text(new THREE.Vector3(this.x, this.model.h + 0.5, this.z), type === 'duck' ? 'QUACK!' : 'HONK!', 'honk', { dur: 0.9, rise: 40 });
    this.bounce = 0.8;
  }
  say(text) {
    if (this.sayLbl) return;
    this.sayLbl = FX.label(this.group, this.model.h + 1.55, `<div class="say">${text}</div>`, '');
    this.sayT = 3.2;
  }
  face() {
    if (this.state === 'leave') return this.mood === 'love' ? 'love' : this.mood === 'meh' ? 'meh' : 'happy';
    const p = this.patience / this.patMax;
    if (!this.waiting) return this.waited > 8 ? 'meh' : 'happy';
    return p > 0.6 ? 'happy' : p > 0.3 ? 'meh' : 'angry';
  }
  updateLabel(force) {
    const f = this.face();
    const p = U.clamp(this.patience / this.patMax, 0, 1);
    const showBar = this.waiting && p < 0.75 && this.x > CFG.entranceX - 45;
    const key = f + (showBar ? Math.round(p * 20) : 'x');
    if (!force && key === this._lk) return;
    this._lk = key;
    const col = p > 0.6 ? '#46c23a' : p > 0.3 ? '#ffc93c' : '#ff4d5a';
    const showFace = !this.waiting || this.x > CFG.entranceX - 45;
    this.label.set(showFace ? `<div class="mood"><div class="face">${ICONS.html(f)}</div>${showBar ? `<div class="pbar"><div style="width:${(p * 100).toFixed(0)}%;background:${col}"></div></div>` : ''}</div>` : '');
  }
  enteredLine() {
    const g = this.lane.entrance; if (!g) return;
    g.lamp.material.color.set(0xff4d4d);
    setTimeout(() => g.lamp.material.color.set(0x3dff6a), 900);
  }
  onProcessed(station) {
    this.state = 'drive'; this.doneX = station.x; this.stop = null;
    if (station.occupant === this) station.occupant = null;
    this.waited = 0;
    if (station.type !== 'pay') { this.bill += Game.stationValue(station.type); this.stages++; }
  }
  leave() {
    this.state = 'leave';
    const clean = this.dirt < 0.05;
    this.mood = clean && this.stages >= 3 ? 'love' : clean ? 'happy' : 'meh';
    this.updateLabel(true);
    if (this.goldLbl) { this.goldLbl.remove(); this.goldLbl = null; }
    // funny: a pigeon sometimes ruins a freshly cleaned car on its way out
    if (clean && Math.random() < 0.05) {
      setTimeout(() => {
        if (!this.group.parent) return;
        const pos = new THREE.Vector3(0, this.model.h + 0.02, 0);
        const it = FX.sys.mud.add(this.model.inner, pos, new THREE.Quaternion(), new THREE.Vector3(0.35, 0.06, 0.3), 0xffffff);
        if (it) this.mud.push(it);
        FX.text(new THREE.Vector3(this.x, this.model.h + 0.8, this.z), 'SPLAT! NOOO!', 'rage', { dur: 1.1 });
        Sfx.play.splat();
      }, 900);
    }
  }
  rageQuit() {
    FX.smoke(this.x, 1, this.z, 16, 1.2); Sfx.play.rage();
    FX.text(new THREE.Vector3(this.x, 2.6, this.z), U.pick(CFG.ragePuns), 'rage', { dur: 1.4 });
    Bus.emit('car:rage', this);
    this.destroy();
  }
  destroy() {
    this.dead = true;
    const i = this.lane.cars.indexOf(this); if (i >= 0) this.lane.cars.splice(i, 1);
    for (const st of Object.values(this.lane.stations)) if (st.occupant === this) st.occupant = null;
    FX.sys.mud.removeAll(this.model.inner); if (this.model.extras.hover) FX.sys.mud.removeAll(this.model.extras.hover);
    FX.sys.coat.removeAll(this.model.inner); if (this.model.extras.hover) FX.sys.coat.removeAll(this.model.extras.hover);
    GFX.scene.remove(this.group);
    this.label.remove(); if (this.sayLbl) this.sayLbl.remove(); if (this.goldLbl) this.goldLbl.remove();
    this.model.paintMat.dispose();
    Game.removeCar(this);
  }
}
