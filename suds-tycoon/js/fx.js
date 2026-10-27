// fx.js — instanced particles, mud splats and foam that stick to cars, flying objects,
// floating text and world-anchored DOM labels.
class Particles {
  constructor(geo, mat, cap, o = {}) {
    this.mesh = new THREE.InstancedMesh(geo, mat, cap);
    this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.mesh.frustumCulled = false;
    this.mesh.castShadow = !!o.shadow;
    this.mesh.count = 0;
    this.cap = cap; this.p = []; this.bill = !!o.billboard;
    if (o.colors) { this.mesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(cap * 3).fill(1), 3); this.mesh.instanceColor.setUsage(THREE.DynamicDrawUsage); }
    this.colors = !!o.colors;
  }
  spawn(o) {
    if (this.p.length >= this.cap) this.p.shift();
    const q = {
      x: o.x, y: o.y, z: o.z, vx: o.vx || 0, vy: o.vy || 0, vz: o.vz || 0,
      life: 0, max: o.life || 1, s0: o.s0 != null ? o.s0 : 1, s1: o.s1 != null ? o.s1 : 0, sm: o.sm || 0,
      g: o.g || 0, drag: o.drag || 0, rx: o.rx || 0, ry: o.ry || 0, rz: o.rz || 0, vr: o.vr || 0,
      col: o.col != null ? new THREE.Color(o.col) : null, sx: o.sx || 1, sy: o.sy || 1, sz: o.sz || 1, floor: o.floor,
    };
    this.p.push(q);
    return q;
  }
  update(dt, camQ) {
    const m = new THREE.Matrix4(), pos = new THREE.Vector3(), qu = new THREE.Quaternion(), sc = new THREE.Vector3(), e = new THREE.Euler();
    let n = 0;
    for (let i = 0; i < this.p.length; i++) {
      const q = this.p[i];
      q.life += dt;
      if (q.life >= q.max) { this.p.splice(i, 1); i--; continue; }
      q.vy -= q.g * dt;
      if (q.drag) { const d = Math.exp(-q.drag * dt); q.vx *= d; q.vy *= d; q.vz *= d; }
      q.x += q.vx * dt; q.y += q.vy * dt; q.z += q.vz * dt;
      if (q.floor != null && q.y < q.floor) { q.y = q.floor; q.vy *= -0.3; q.vx *= 0.6; q.vz *= 0.6; }
      q.rx += q.vr * dt; q.rz += q.vr * 0.7 * dt;
      const t = q.life / q.max;
      let s;
      if (q.sm) { // grow in then shrink out
        s = t < q.sm ? U.easeOutBack(t / q.sm) * q.s0 : U.lerp(q.s0, q.s1, (t - q.sm) / (1 - q.sm));
      } else s = U.lerp(q.s0, q.s1, t);
      pos.set(q.x, q.y, q.z);
      if (this.bill) { qu.copy(camQ); if (q.rz) { const zq = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 0, 1), q.rz); qu.multiply(zq); } }
      else { e.set(q.rx, q.ry, q.rz); qu.setFromEuler(e); }
      sc.set(s * q.sx, s * q.sy, s * q.sz);
      m.compose(pos, qu, sc);
      this.mesh.setMatrixAt(n, m);
      if (this.colors && q.col) this.mesh.instanceColor.setXYZ(n, q.col.r, q.col.g, q.col.b);
      n++;
    }
    this.mesh.count = n;
    this.mesh.instanceMatrix.needsUpdate = true;
    if (this.colors) this.mesh.instanceColor.needsUpdate = true;
  }
  clear() { this.p.length = 0; this.mesh.count = 0; }
}

// Things glued to a moving object (mud on cars, foam coats). Each item: parent Object3D + local TRS + live scale k.
class Stickers {
  constructor(geo, mat, cap, colors = false) {
    this.mesh = new THREE.InstancedMesh(geo, mat, cap);
    this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.mesh.frustumCulled = false; this.mesh.castShadow = false; this.mesh.receiveShadow = true;
    this.mesh.count = 0; this.cap = cap; this.items = [];
    if (colors) { this.mesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(cap * 3).fill(1), 3); }
    this.colors = colors;
  }
  add(parent, pos, quat, scale, col) {
    if (this.items.length >= this.cap) return null;
    const it = { parent, pos: pos.clone(), quat: quat.clone(), scale: scale.clone(), k: 0, target: 1, col: col != null ? new THREE.Color(col) : null, dead: false, wob: Math.random() * 6 };
    this.items.push(it);
    return it;
  }
  update(dt) {
    const m = new THREE.Matrix4(), s = new THREE.Vector3();
    let n = 0;
    for (let i = 0; i < this.items.length; i++) {
      const it = this.items[i];
      it.k = U.damp(it.k, it.target, it.target > it.k ? 10 : 7, dt);
      if ((it.dead || it.target === 0) && it.k < 0.03) { this.items.splice(i, 1); i--; continue; }
      it.wob += dt * 3;
      const wob = 1 + Math.sin(it.wob) * 0.04;
      s.copy(it.scale).multiplyScalar(it.k * wob);
      m.compose(it.pos, it.quat, s);
      m.premultiply(it.parent.matrixWorld);
      this.mesh.setMatrixAt(n, m);
      if (this.colors && it.col) this.mesh.instanceColor.setXYZ(n, it.col.r, it.col.g, it.col.b);
      n++;
    }
    this.mesh.count = n;
    this.mesh.instanceMatrix.needsUpdate = true;
    if (this.colors) this.mesh.instanceColor.needsUpdate = true;
  }
  removeAll(parent) { for (const it of this.items) if (it.parent === parent) { it.target = 0; it.dead = true; } }
}

const FX = (() => {
  let sys = {}, labelsEl, root;
  const texts = [], labels = [], flyers = [];
  const tmp = { x: 0, y: 0, vis: false };

  function init(scene) {
    root = new THREE.Group(); scene.add(root);
    labelsEl = document.getElementById('labels');
    const lam = (c, o) => new THREE.MeshLambertMaterial(Object.assign({ color: c }, o || {}));
    sys.foam = new Particles(GEO.ico(0.3, 1), lam(0xffffff), 500, { shadow: false });
    sys.colorFoam = new Particles(GEO.ico(0.3, 1), lam(0xffffff), 300, { colors: true });
    sys.bubble = new Particles(GEO.sphere(0.2, 10, 8), new THREE.MeshLambertMaterial({ color: 0xcdeeff, transparent: true, opacity: 0.55, depthWrite: false }), 200);
    sys.water = new Particles(GEO.sphere(0.07, 6, 4), new THREE.MeshBasicMaterial({ color: 0x7fd0ff }), 600);
    sys.sparkle = new Particles(GEO.plane(0.6, 0.6), new THREE.MeshBasicMaterial({ map: TEX.sparkle(), transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, color: 0xfff3a0 }), 300, { billboard: true });
    sys.confetti = new Particles(GEO.box(0.16, 0.03, 0.1), new THREE.MeshBasicMaterial({ color: 0xffffff }), 500, { colors: true });
    sys.smoke = new Particles(GEO.ico(0.5, 1), lam(0xd9dde4), 160);
    sys.wind = new Particles(GEO.box(0.04, 0.9, 0.04), new THREE.MeshBasicMaterial({ color: 0xfff6d0, transparent: true, opacity: 0.7 }), 260);
    sys.coin = new Particles(Models.coinGeometry(), MAT.vc, 200, { shadow: true });
    sys.cash = new Particles(Models.cashBundleGeo(), MAT.vc, 120, { shadow: true });
    sys.glow = new Particles(GEO.plane(1, 1), new THREE.MeshBasicMaterial({ map: TEX.glow(), transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, color: 0xffe9a0 }), 60, { billboard: true });
    for (const k in sys) root.add(sys[k].mesh);
    // mud splats + foam coats
    sys.mud = new Stickers(GEO.ico(1, 1), new THREE.MeshLambertMaterial({ color: 0xffffff, flatShading: false }), 1400, true);
    sys.coat = new Stickers(GEO.ico(1, 1), lam(0xffffff), 900, true);
    root.add(sys.mud.mesh, sys.coat.mesh);
  }

  function update(dt) {
    const cq = GFX.camera.quaternion;
    for (const k in sys) sys[k].update(dt, cq);
    // flyers
    for (let i = 0; i < flyers.length; i++) {
      const f = flyers[i];
      f.t += dt / f.dur;
      const to = typeof f.to === 'function' ? f.to() : f.to;
      const t = Math.min(1, f.t), e = f.ease ? f.ease(t) : t;
      const p = f.obj.position;
      p.lerpVectors(f.from, to, e);
      p.y += Math.sin(Math.PI * t) * f.arc;
      if (f.spin) { f.obj.rotation.y += dt * f.spin; f.obj.rotation.x += dt * f.spin * 0.5; }
      if (f.scaleTo != null) f.obj.scale.setScalar(U.lerp(f.scaleFrom, f.scaleTo, t));
      if (f.t >= 1) { flyers.splice(i, 1); i--; if (f.done) f.done(f.obj); }
    }
    updateDom(dt);
  }

  // ---------- particle helpers ----------
  function foamBurst(x, y, z, n = 10, spread = 1.2, colors) {
    for (let i = 0; i < n; i++) {
      const s = colors ? sys.colorFoam : sys.foam;
      s.spawn({ x: x + U.rand(-spread, spread), y: y + U.rand(-0.3, 0.8), z: z + U.rand(-spread, spread), vx: U.rand(-0.6, 0.6), vy: U.rand(0.2, 1.4), vz: U.rand(-0.6, 0.6),
        life: U.rand(0.6, 1.3), s0: U.rand(0.7, 1.5), s1: 0, sm: 0.25, g: 0.8, drag: 1.5, col: colors ? U.pick(colors) : null });
    }
  }
  function bubbles(x, y, z, n = 4) {
    for (let i = 0; i < n; i++) sys.bubble.spawn({ x: x + U.rand(-1, 1), y: y + U.rand(0, 1), z: z + U.rand(-1, 1), vx: U.rand(-0.3, 0.3), vy: U.rand(0.5, 1.2), vz: U.rand(-0.3, 0.3), life: U.rand(1.5, 3), s0: U.rand(0.6, 1.5), s1: 1.1, sm: 0.2, drag: 0.6 });
  }
  function splash(x, y, z, n = 8, spd = 2) {
    for (let i = 0; i < n; i++) sys.water.spawn({ x: x + U.rand(-0.3, 0.3), y, z: z + U.rand(-0.3, 0.3), vx: U.rand(-spd, spd), vy: U.rand(1, 3.5), vz: U.rand(-spd, spd), life: U.rand(0.4, 0.8), s0: U.rand(0.8, 1.6), s1: 0.3, g: 12, floor: 0.05 });
  }
  function rain(x, z, w, d, n = 6) {
    for (let i = 0; i < n; i++) sys.water.spawn({ x: x + U.rand(-w, w), y: U.rand(2.6, 3.0), z: z + U.rand(-d, d), vx: 0, vy: -U.rand(5, 8), vz: 0, life: 0.45, s0: 1.2, s1: 0.9, sy: 3 });
  }
  function sparkles(x, y, z, n = 6, spread = 1.5, col) {
    for (let i = 0; i < n; i++) sys.sparkle.spawn({ x: x + U.rand(-spread, spread), y: y + U.rand(0, 1.2), z: z + U.rand(-spread, spread), vy: U.rand(0.2, 0.8), life: U.rand(0.5, 1.0), s0: U.rand(0.6, 1.3), s1: 0, sm: 0.3, rz: U.rand(0, 3) });
  }
  function confetti(x, y, z, n = 40, power = 6) {
    const cols = [0xff4d5a, 0xffd23f, 0x46c23a, 0x2f8cff, 0xb44cff, 0xff8a1f, 0xffffff];
    for (let i = 0; i < n; i++) sys.confetti.spawn({ x, y, z, vx: U.rand(-power, power) * 0.5, vy: U.rand(power * 0.6, power * 1.2), vz: U.rand(-power, power) * 0.5, life: U.rand(1.4, 2.2), s0: U.rand(1, 1.8), s1: 0.6, g: 9, drag: 1.4, vr: U.rand(-12, 12), col: U.pick(cols), floor: 0.06 });
  }
  function smoke(x, y, z, n = 10, s = 1) {
    for (let i = 0; i < n; i++) sys.smoke.spawn({ x: x + U.rand(-1, 1) * s, y: y + U.rand(0, 1) * s, z: z + U.rand(-1, 1) * s, vx: U.rand(-1.5, 1.5), vy: U.rand(0.5, 2.0), vz: U.rand(-1.5, 1.5), life: U.rand(0.6, 1.1), s0: U.rand(1.2, 2.2) * s, s1: 0, sm: 0.2, drag: 2 });
  }
  function wind(x, z, w, d, n = 3) {
    for (let i = 0; i < n; i++) sys.wind.spawn({ x: x + U.rand(-w, w), y: 3.0, z: z + U.rand(-d, d), vx: U.rand(-0.5, 0.5), vy: -U.rand(6, 9), vz: U.rand(-0.5, 0.5), life: 0.35, s0: 1, s1: 0.5 });
  }
  function coinBurst(x, y, z, n = 6) {
    for (let i = 0; i < n; i++) sys.coin.spawn({ x, y, z, vx: U.rand(-2, 2), vy: U.rand(4, 7), vz: U.rand(-2, 2), life: U.rand(0.8, 1.2), s0: 1.2, s1: 0.8, g: 14, floor: 0.12, vr: U.rand(6, 14) });
  }
  function glow(x, y, z, s = 3, life = 0.5) { sys.glow.spawn({ x, y, z, life, s0: s, s1: 0, sm: 0.2 }); }

  // ---------- stickers ----------
  function addMud(car, n, dirtMul = 1) {
    const surf = car.model.surf, parent = car.model.extras.hover || car.model.inner;
    const out = [];
    const areas = surf.map(p => p.kind === 'top' ? (p.x1 - p.x0) * (p.z1 - p.z0) : (p.x1 - p.x0) * (p.y1 - p.y0) * 2);
    const tot = areas.reduce((a, b) => a + b, 0);
    for (let i = 0; i < n; i++) {
      let r = Math.random() * tot, idx = 0;
      while (r > areas[idx] && idx < areas.length - 1) { r -= areas[idx]; idx++; }
      const p = surf[idx];
      const pos = new THREE.Vector3(), quat = new THREE.Quaternion(), sc = new THREE.Vector3();
      const size = U.rand(0.16, 0.34) * Math.sqrt(dirtMul);
      if (p.kind === 'top') {
        pos.set(U.rand(p.x0, p.x1), p.y + 0.01, U.rand(p.z0, p.z1));
        quat.setFromEuler(new THREE.Euler(0, Math.random() * 6, 0));
        sc.set(size * U.rand(1, 1.6), 0.06, size);
      } else {
        const side = U.chance(0.75) ? 1 : -1;
        pos.set(U.rand(p.x0, p.x1), U.rand(p.y0, p.y1), p.z * side);
        quat.setFromEuler(new THREE.Euler(0, 0, Math.random() * 6));
        sc.set(size * U.rand(1, 1.6), size, 0.06);
      }
      const it = sys.mud.add(parent, pos, quat, sc, U.pick(PAL.mud));
      if (it) out.push(it);
    }
    return out;
  }
  function addCoat(car, n) {
    const surf = car.model.surf, parent = car.model.extras.hover || car.model.inner;
    const out = [];
    for (let i = 0; i < n; i++) {
      const p = U.pick(surf);
      const pos = new THREE.Vector3(), quat = new THREE.Quaternion(), sc = new THREE.Vector3();
      if (p.kind === 'top') pos.set(U.rand(p.x0, p.x1), p.y + 0.05, U.rand(p.z0, p.z1));
      else pos.set(U.rand(p.x0, p.x1), U.rand(p.y0, p.y1 + 0.3), p.z * (U.chance(0.5) ? 1 : -1));
      const s = U.rand(0.22, 0.42);
      sc.set(s, s * U.rand(0.8, 1.1), s);
      const it = sys.coat.add(parent, pos, quat, sc, 0xffffff);
      if (it) { it.k = 0; out.push(it); }
    }
    return out;
  }
  function tintCoat(items, col) { const c = new THREE.Color(col); for (const it of items) it.col = c.clone(); }

  // ---------- flyers ----------
  function fly(obj, from, to, dur, arc = 1.5, done, o = {}) {
    if (!obj.parent) root.add(obj);
    obj.position.copy(from);
    flyers.push({ obj, from: from.clone(), to, dur, arc, done, t: 0, spin: o.spin, ease: o.ease, scaleFrom: o.scaleFrom != null ? o.scaleFrom : 1, scaleTo: o.scaleTo });
  }
  const bundlePool = [];
  function getBundle() {
    const m = bundlePool.pop() || new THREE.Mesh(Models.cashBundleGeo(), MAT.vc);
    m.castShadow = true; m.scale.setScalar(1); m.rotation.set(0, Math.random() * 3, 0); m.visible = true;
    return m;
  }
  function freeBundle(m) { if (m.parent) m.parent.remove(m); bundlePool.push(m); }

  // ---------- DOM: floating text & labels ----------
  function text(pos, html, cls = '', o = {}) {
    const el = document.createElement('div');
    el.className = 'ftext ' + cls;
    el.innerHTML = html;
    labelsEl.appendChild(el);
    texts.push({ el, pos: pos.clone(), t: 0, dur: o.dur || 1.3, rise: o.rise != null ? o.rise : 70, follow: o.follow || null, fy: o.fy || 0 });
  }
  function label(anchor, offY, html, cls = '') {
    const el = document.createElement('div');
    el.className = 'wlabel ' + cls;
    el.innerHTML = html;
    labelsEl.appendChild(el);
    const L = { el, anchor, offY, visible: true, html, pos: new THREE.Vector3(), offX: 0,
      set(h) { if (h !== L.html) { L.html = h; el.innerHTML = h; } },
      show(v) { if (v !== L.visible) { L.visible = v; el.style.display = v ? '' : 'none'; } },
      cls(c, on) { el.classList.toggle(c, on); },
      remove() { el.remove(); const i = labels.indexOf(L); if (i >= 0) labels.splice(i, 1); } };
    labels.push(L);
    return L;
  }
  const _wp = new THREE.Vector3();
  function anchorPos(a, out) {
    if (a.isObject3D) a.getWorldPosition(out);
    else if (typeof a === 'function') out.copy(a());
    else out.copy(a);
    return out;
  }
  function updateDom(dt) {
    for (let i = 0; i < texts.length; i++) {
      const t = texts[i];
      t.t += dt;
      if (t.t >= t.dur) { t.el.remove(); texts.splice(i, 1); i--; continue; }
      if (t.follow) { t.follow.getWorldPosition(t.pos); t.pos.y += t.fy; }
      GFX.project(t.pos, tmp);
      const k = t.t / t.dur;
      const y = tmp.y - U.easeOutCubic(Math.min(1, k * 1.4)) * t.rise;
      t.el.style.transform = `translate3d(${tmp.x.toFixed(1)}px, ${y.toFixed(1)}px, 0) translate(-50%, -50%)`;
      t.el.style.opacity = k > 0.7 ? (1 - (k - 0.7) / 0.3).toFixed(2) : '1';
      t.el.style.display = tmp.vis ? '' : 'none';
    }
    for (const L of labels) {
      if (!L.visible) continue;
      anchorPos(L.anchor, _wp); _wp.y += L.offY;
      GFX.project(_wp, tmp);
      L.el.style.transform = `translate3d(${(tmp.x + L.offX).toFixed(1)}px, ${tmp.y.toFixed(1)}px, 0) translate(-50%, -100%)`;
      L.el.style.visibility = tmp.vis ? 'visible' : 'hidden';
    }
  }
  function clearAll() {
    for (const t of texts) t.el.remove(); texts.length = 0;
    for (const L of labels.slice()) L.remove();
    flyers.length = 0;
    for (const k in sys) { if (sys[k].clear) sys[k].clear(); if (sys[k].items) sys[k].items.length = 0; }
  }

  return { init, update, sys, foamBurst, bubbles, splash, rain, sparkles, confetti, smoke, wind, coinBurst, glow, addMud, addCoat, tintCoat, fly, getBundle, freeBundle, text, label, clearAll, get root() { return root; } };
})();
