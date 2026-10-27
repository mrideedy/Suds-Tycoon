// models.js — every procedural low-poly model: props, people, items, cars, machines, buildings.
// Static parts are merged into one vertex-coloured mesh; moving parts stay separate.
const Models = (() => {
  const W = 0xffffff;
  const halfSphere = {};
  function domeGeo(r, ws = 16, hs = 8) {
    const k = r + '|' + ws;
    return halfSphere[k] || (halfSphere[k] = new THREE.SphereGeometry(r, ws, hs, 0, Math.PI * 2, 0, Math.PI / 2));
  }
  const camYawRot = () => GFX.cam.yaw; // signs face the camera

  // =========================================================================
  // Environment props (appended into shared builders for one-draw-call scenery)
  // =========================================================================
  function tree(b, x, z, s = 1, col) {
    col = col || U.pick(PAL.leaf);
    b.cyl(0.17 * s, 0.24 * s, 1.3 * s, 6, PAL.trunk, { x, y: 0.65 * s, z, flat: true });
    const pts = [[0.001, 0], [0.92, 0.18], [1.08, 0.75], [1.0, 1.45], [0.72, 2.0], [0.32, 2.3], [0.001, 2.36]];
    b.lathe(pts, 7, col, { x, y: 0.95 * s, z, s, ry: Math.random() * 6, flat: true });
  }
  function palm(b, x, z, s = 1) {
    let px = x, py = 0, pz = z;
    const lean = U.rand(0, Math.PI * 2), lx = Math.cos(lean), lz = Math.sin(lean);
    for (let i = 0; i < 6; i++) {
      b.cyl(0.2 * s, 0.24 * s, 0.75 * s, 7, i % 2 ? 0x9a6b3c : 0xb07a45, { x: px, y: py + 0.37 * s, z: pz, rz: lx * 0.08 * i, rx: -lz * 0.08 * i, flat: true });
      px += lx * 0.07 * s * i; pz += lz * 0.07 * s * i; py += 0.68 * s;
    }
    for (let i = 0; i < 7; i++) {
      const a = (i / 7) * Math.PI * 2;
      b.box(2.4 * s, 0.08 * s, 0.6 * s, i % 2 ? 0x3fae3a : 0x58c447, { x: px + Math.cos(a) * 1.0 * s, y: py + 0.05 * s - 0.25 * s, z: pz + Math.sin(a) * 1.0 * s, ry: -a, rz: -0.35, flat: true });
    }
    b.sphere(0.22 * s, 0x6b4a2b, { x: px, y: py, z: pz, ws: 6, hs: 4 });
  }
  function cactus(b, x, z, s = 1) {
    const c = 0x4f9e3a;
    b.cyl(0.32 * s, 0.36 * s, 2.6 * s, 8, c, { x, y: 1.3 * s, z, flat: true });
    b.sphere(0.32 * s, c, { x, y: 2.6 * s, z, ws: 8, hs: 5, flat: true });
    const arm = (side, h) => {
      b.cyl(0.2 * s, 0.2 * s, 0.6 * s, 7, c, { x: x + side * 0.55 * s, y: h * s, z, rz: Math.PI / 2, flat: true });
      b.cyl(0.2 * s, 0.2 * s, 0.9 * s, 7, c, { x: x + side * 0.85 * s, y: (h + 0.45) * s, z, flat: true });
      b.sphere(0.2 * s, c, { x: x + side * 0.85 * s, y: (h + 0.9) * s, z, ws: 7, hs: 4 });
    };
    arm(-1, 1.1); if (Math.random() < 0.7) arm(1, 1.5);
    b.cone(0.12 * s, 0.25 * s, 5, 0xff6fb1, { x, y: 2.95 * s, z });
  }
  function pine(b, x, z, s = 1, snow = true) {
    b.cyl(0.18 * s, 0.24 * s, 1.0 * s, 6, PAL.trunk, { x, y: 0.5 * s, z, flat: true });
    const greens = [0x2f7d4a, 0x3a8f56, 0x2b6e43];
    for (let i = 0; i < 3; i++) {
      const r = (1.35 - i * 0.33) * s, y = (1.0 + i * 0.85) * s;
      b.cone(r, 1.4 * s, 7, greens[i], { x, y: y + 0.7 * s, z, flat: true, ry: i });
      if (snow) b.cone(r * 0.62, 0.55 * s, 7, 0xffffff, { x, y: y + 1.12 * s, z, flat: true, ry: i });
    }
  }
  function bush(b, x, z, s = 1, col) {
    b.ico(0.75 * s, col || U.pick(PAL.leaf), { x, y: 0.38 * s, z, sy: 0.72, ry: Math.random() * 3 });
  }
  function rock(b, x, z, s = 1, col = 0x9aa3ad) {
    b.add(GEO.dodeca(0.6 * s), col, { x, y: 0.2 * s, z, sy: 0.6, ry: Math.random() * 3, rx: Math.random() * 0.4, flat: true });
  }
  function lamp(b, x, z, ry = 0, glow = false) {
    b.cyl(0.09, 0.12, 4.4, 8, 0x3d434d, { x, y: 2.2, z });
    b.cyl(0.2, 0.25, 0.2, 8, 0x3d434d, { x, y: 0.1, z });
    const ax = Math.cos(ry), az = -Math.sin(ry);
    b.box(1.0, 0.09, 0.09, 0x3d434d, { x: x + ax * 0.45, y: 4.35, z: z + az * 0.45, ry });
    b.rbox(0.62, 0.2, 0.34, 0.06, 0x5a616d, { x: x + ax * 0.95, y: 4.3, z: z + az * 0.95, ry });
    b.box(0.5, 0.04, 0.24, glow ? 0xfff6c4 : 0xf2f4f8, { x: x + ax * 0.95, y: 4.19, z: z + az * 0.95, ry });
  }
  function trafficCone(b, x, z, s = 1) {
    b.box(0.62 * s, 0.07 * s, 0.62 * s, 0xff7a1a, { x, y: 0.035 * s, z });
    b.cone(0.26 * s, 0.75 * s, 12, 0xff7a1a, { x, y: 0.43 * s, z });
    b.cyl(0.165 * s, 0.2 * s, 0.13 * s, 12, 0xffffff, { x, y: 0.38 * s, z });
  }
  function trashCan(b, x, z) {
    b.cyl(0.33, 0.29, 0.85, 12, 0x8a929e, { x, y: 0.43, z });
    b.cyl(0.36, 0.36, 0.08, 12, 0x6f7783, { x, y: 0.89, z });
    b.cyl(0.08, 0.08, 0.06, 8, 0x6f7783, { x, y: 0.96, z });
  }
  function bench(b, x, z, ry = 0) {
    const m = new THREE.Matrix4().makeRotationY(ry).setPosition(x, 0, z);
    b.rbox(1.8, 0.1, 0.5, 0.03, 0xc9884f, { y: 0.48, m });
    b.rbox(1.8, 0.45, 0.08, 0.03, 0xc9884f, { y: 0.78, z: -0.24, rx: -0.12, m });
    b.box(0.08, 0.48, 0.42, 0x4d525b, { x: -0.75, y: 0.24, m });
    b.box(0.08, 0.48, 0.42, 0x4d525b, { x: 0.75, y: 0.24, m });
  }
  function fenceSeg(b, x0, z0, x1, z1, h = 0.55, col = PAL.curb) {
    const len = Math.hypot(x1 - x0, z1 - z0), ry = -Math.atan2(z1 - z0, x1 - x0);
    b.box(len, h, 0.32, col, { x: (x0 + x1) / 2, y: h / 2, z: (z0 + z1) / 2, ry });
    b.box(len + 0.04, 0.06, 0.38, 0xffffff, { x: (x0 + x1) / 2, y: h + 0.03, z: (z0 + z1) / 2, ry });
  }
  function snowman(b, x, z, s = 1) {
    b.sphere(0.8 * s, 0xffffff, { x, y: 0.7 * s, z, ws: 12, hs: 9 });
    b.sphere(0.58 * s, 0xffffff, { x, y: 1.75 * s, z, ws: 12, hs: 9 });
    b.sphere(0.42 * s, 0xffffff, { x, y: 2.5 * s, z, ws: 12, hs: 9 });
    b.cone(0.07 * s, 0.4 * s, 6, 0xff8a1f, { x: x + 0.1 * s, y: 2.5 * s, z: z + 0.45 * s, rx: Math.PI / 2 });
    b.cyl(0.3 * s, 0.3 * s, 0.4 * s, 10, 0x2a2e36, { x, y: 2.98 * s, z });
    b.cyl(0.45 * s, 0.45 * s, 0.05 * s, 10, 0x2a2e36, { x, y: 2.8 * s, z });
    b.torus(0.44 * s, 0.1 * s, 0xe8383d, { x, y: 2.15 * s, z, rx: Math.PI / 2 });
  }
  function tallBuilding(b, x, z, w, d, h, col, night) {
    b.box(w, h, d, col, { x, y: h / 2, z });
    b.box(w + 0.3, 0.3, d + 0.3, 0x3a3f4a, { x, y: h + 0.15, z });
    const rows = Math.floor(h / 1.6), cols = Math.max(1, Math.floor(w / 1.4));
    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
      const lit = night ? Math.random() < 0.55 : false;
      const wc = lit ? U.pick([0xfff2a8, 0xffd36e, 0xb8f0ff]) : (night ? 0x2a3550 : 0x9fd2f5);
      b.box(0.7, 0.9, 0.05, wc, { x: x - w / 2 + 0.7 + c * ((w - 1.4) / Math.max(1, cols - 1 || 1)), y: 1.2 + r * 1.6, z: z + d / 2 + 0.03 });
    }
  }

  // =========================================================================
  // People
  // =========================================================================
  function hatMesh(id, capCol = PAL.blue, star = false) {
    const b = new Builder();
    if (id === 'cap') {
      b.add(domeGeo(0.385), capCol, { y: 0.07, sy: 0.82 });
      b.rbox(0.52, 0.05, 0.32, 0.03, capCol === PAL.blue ? PAL.blueDark : 0xc98a00, { y: 0.1, z: 0.34 });
      if (star) b.add(new THREE.CircleGeometry(0.1, 5), 0xffd84a, { y: 0.28, z: 0.315, rx: -0.5, rz: Math.PI / 2 });
      b.sphere(0.05, capCol, { y: 0.39 });
    } else if (id === 'cowboy') {
      b.cyl(0.66, 0.66, 0.05, 18, 0xa8693a, { y: 0.2, sz: 0.85 });
      b.cyl(0.3, 0.36, 0.42, 12, 0xc9884f, { y: 0.42 });
      b.cyl(0.365, 0.365, 0.08, 12, 0x5a3418, { y: 0.27 });
    } else if (id === 'chef') {
      b.cyl(0.36, 0.33, 0.28, 14, 0xffffff, { y: 0.32 });
      b.sphere(0.42, 0xffffff, { y: 0.6, sy: 0.7, ws: 12, hs: 8 });
    } else if (id === 'crown') {
      b.cyl(0.3, 0.3, 0.22, 10, PAL.gold, { y: 0.33 });
      for (let i = 0; i < 5; i++) { const a = i / 5 * Math.PI * 2; b.cone(0.07, 0.2, 5, PAL.gold, { x: Math.cos(a) * 0.27, y: 0.53, z: Math.sin(a) * 0.27 }); }
      b.sphere(0.06, 0xff4d5a, { y: 0.34, z: 0.3, ws: 8, hs: 6 });
    } else if (id === 'cone') {
      b.box(0.6, 0.06, 0.6, 0xff7a1a, { y: 0.26 });
      b.cone(0.26, 0.72, 12, 0xff7a1a, { y: 0.64 });
      b.cyl(0.17, 0.2, 0.13, 12, 0xffffff, { y: 0.58 });
    } else if (id === 'duck') {
      b.sphere(0.3, 0xffd23f, { y: 0.44, sx: 1.2, sy: 0.75, ws: 12, hs: 8 });
      b.sphere(0.19, 0xffd23f, { y: 0.68, z: 0.14, ws: 12, hs: 8 });
      b.box(0.14, 0.06, 0.16, 0xff8a1f, { y: 0.66, z: 0.36 });
      b.sphere(0.03, 0x1b2333, { x: 0.08, y: 0.73, z: 0.3, ws: 6, hs: 4 });
      b.sphere(0.03, 0x1b2333, { x: -0.08, y: 0.73, z: 0.3, ws: 6, hs: 4 });
    } else if (id === 'bubble') {
      b.rbox(0.62, 0.1, 0.62, 0.04, 0xa9b4c6, { y: -0.36 });
    }
    const m = b.parts.length ? b.build() : new THREE.Group();
    if (id === 'bubble') {
      const glass = new THREE.Mesh(GEO.sphere(0.58, 20, 14), new THREE.MeshLambertMaterial({ color: 0xbfe8ff, transparent: true, opacity: 0.32, depthWrite: false }));
      glass.position.y = 0.04; m.add(glass);
    }
    return m;
  }

  const armGeoCache = {}, legGeoCache = {};
  function person(o = {}) {
    const shirt = o.shirt != null ? o.shirt : 0xffffff, vest = o.vest, pants = o.pants != null ? o.pants : 0x2f4f9e;
    const skin = o.skin != null ? o.skin : PAL.skin[0];
    const g = new THREE.Group();
    const body = new THREE.Group(); g.add(body);
    const b = new Builder();
    b.rbox(0.64, 0.6, 0.44, 0.17, shirt, { y: 0.88 });
    if (vest) {
      b.rbox(0.66, 0.44, 0.46, 0.14, vest, { y: 0.8 });
      b.box(0.08, 0.3, 0.47, vest, { x: -0.2, y: 1.02 });
      b.box(0.08, 0.3, 0.47, vest, { x: 0.2, y: 1.02 });
      if (o.badge) b.box(0.12, 0.12, 0.02, 0xffd84a, { x: 0.14, y: 0.96, z: 0.235 });
    }
    b.rbox(0.66, 0.08, 0.46, 0.03, 0x2a2d33, { y: 0.6 });
    b.sphere(0.37, skin, { y: 1.43, ws: 16, hs: 12 });
    b.sphere(0.085, skin, { x: -0.36, y: 1.42, ws: 8, hs: 6 });
    b.sphere(0.085, skin, { x: 0.36, y: 1.42, ws: 8, hs: 6 });
    b.sphere(0.058, 0x1b2333, { x: -0.13, y: 1.45, z: 0.325, ws: 8, hs: 6 });
    b.sphere(0.058, 0x1b2333, { x: 0.13, y: 1.45, z: 0.325, ws: 8, hs: 6 });
    b.sphere(0.02, 0xffffff, { x: -0.115, y: 1.47, z: 0.375, ws: 6, hs: 4 });
    b.sphere(0.02, 0xffffff, { x: 0.145, y: 1.47, z: 0.375, ws: 6, hs: 4 });
    b.sphere(0.065, 0xff9a8a, { x: -0.22, y: 1.35, z: 0.28, sy: 0.55, ws: 8, hs: 6 });
    b.sphere(0.065, 0xff9a8a, { x: 0.22, y: 1.35, z: 0.28, sy: 0.55, ws: 8, hs: 6 });
    b.rbox(0.13, 0.04, 0.04, 0.015, 0x8a3b2e, { y: 1.31, z: 0.35 });
    b.sphere(0.05, skin, { y: 1.39, z: 0.37, ws: 8, hs: 6 });
    const torso = b.build(); torso.name = 'Torso'; body.add(torso);
    g.name = 'Person'; body.name = 'Body';

    const hatHolder = new THREE.Group(); hatHolder.name = 'HatHolder'; hatHolder.position.y = 1.43; body.add(hatHolder);
    let hat = hatMesh(o.hat || 'cap', o.cap || PAL.blue, !!o.star); hat.name = 'Hat'; hatHolder.add(hat);

    const ak = shirt + '|' + skin;
    if (!armGeoCache[ak]) { const ab = new Builder(); ab.rbox(0.18, 0.46, 0.18, 0.08, shirt, { y: -0.2 }); ab.sphere(0.1, skin, { y: -0.46, ws: 8, hs: 6 }); armGeoCache[ak] = ab.merge(); }
    const armL = new THREE.Group(), armR = new THREE.Group();
    armL.position.set(-0.42, 1.1, 0); armR.position.set(0.42, 1.1, 0);
    const mkArm = () => { const m = new THREE.Mesh(armGeoCache[ak], MAT.vc); m.castShadow = true; return m; };
    armL.add(mkArm()); armR.add(mkArm()); body.add(armL, armR); armL.name = 'ArmL'; armR.name = 'ArmR';
    const lk = String(pants);
    if (!legGeoCache[lk]) { const lb = new Builder(); lb.rbox(0.22, 0.44, 0.24, 0.08, pants, { y: -0.2 }); lb.rbox(0.24, 0.13, 0.34, 0.05, 0x2a2d33, { y: -0.5, z: 0.05 }); legGeoCache[lk] = lb.merge(); }
    const legL = new THREE.Group(), legR = new THREE.Group();
    legL.position.set(-0.16, 0.57, 0); legR.position.set(0.16, 0.57, 0);
    const mkLeg = () => { const m = new THREE.Mesh(legGeoCache[lk], MAT.vc); m.castShadow = true; return m; };
    legL.add(mkLeg()); legR.add(mkLeg()); g.add(legL, legR); legL.name = 'LegL'; legR.name = 'LegR';
    const s = o.scale || 1; g.scale.setScalar(s);
    const api = {
      group: g, body, armL, armR, legL, legR, phase: Math.random() * 6, hatId: o.hat || 'cap',
      setHat(id) {
        hatHolder.remove(hat);
        hat = hatMesh(id, o.cap || PAL.blue, !!o.star); hatHolder.add(hat); api.hatId = id;
      },
      animate(dt, speed01, carrying, extra) {
        api.phase += dt * (5 + speed01 * 9);
        const sw = Math.sin(api.phase) * 0.75 * speed01;
        legL.rotation.x = sw; legR.rotation.x = -sw;
        if (extra === 'polish') {
          const t = api.phase * 0.8;
          armL.rotation.set(-1.35 + Math.sin(t) * 0.25, 0, Math.cos(t) * 0.3);
          armR.rotation.set(-1.35 + Math.cos(t) * 0.25, 0, -Math.sin(t) * 0.3);
        } else if (extra === 'sleep') {
          armL.rotation.set(0.1, 0, 0.12); armR.rotation.set(0.1, 0, -0.12);
          body.rotation.x = U.damp(body.rotation.x, 0.22, 4, dt);
        } else if (extra === 'cheer') {
          armL.rotation.set(-2.8 + Math.sin(api.phase * 2) * 0.3, 0, 0.2); armR.rotation.set(-2.8 - Math.sin(api.phase * 2) * 0.3, 0, -0.2);
        } else if (carrying) {
          armL.rotation.set(-1.2, 0, 0.1); armR.rotation.set(-1.2, 0, -0.1);
        } else {
          armL.rotation.set(-sw * 0.8, 0, 0.06); armR.rotation.set(sw * 0.8, 0, -0.06);
        }
        if (extra !== 'sleep') body.rotation.x = U.damp(body.rotation.x, speed01 * 0.08, 6, dt);
        body.position.y = Math.abs(Math.sin(api.phase)) * 0.07 * speed01 + (speed01 < 0.05 ? Math.sin(api.phase * 0.4) * 0.012 : 0);
      },
    };
    return api;
  }

  // =========================================================================
  // Carried items
  // =========================================================================
  const itemGeo = {};
  function itemGeometry(type) {
    if (itemGeo[type]) return itemGeo[type];
    const b = new Builder();
    if (type === 'soap') {
      b.rbox(0.38, 0.4, 0.3, 0.08, 0xff6fb1, { y: 0.2 });
      b.cyl(0.075, 0.075, 0.1, 8, 0x5ad0ff, { x: -0.06, y: 0.44 });
      b.box(0.26, 0.18, 0.02, 0xffffff, { y: 0.2, z: 0.152 });
      b.sphere(0.035, 0x8fdcff, { x: -0.04, y: 0.2, z: 0.165, ws: 6, hs: 4 });
      b.torus(0.08, 0.025, 0xff6fb1, { x: 0.15, y: 0.34, rs: 4, ts: 8 });
    } else if (type === 'wax') {
      b.cyl(0.19, 0.19, 0.38, 12, 0xffc93c, { y: 0.19 });
      b.cyl(0.195, 0.195, 0.06, 12, 0xdfe4ec, { y: 0.39 });
      b.cyl(0.2, 0.2, 0.09, 12, 0xff7a1a, { y: 0.17 });
    } else if (type === 'gel') {
      b.rbox(0.26, 0.38, 0.26, 0.07, 0x3ad17a, { y: 0.19 });
      b.cyl(0.06, 0.075, 0.12, 8, 0x2a2e36, { y: 0.44 });
      b.box(0.2, 0.15, 0.02, 0x2a2e36, { y: 0.2, z: 0.132 });
    }
    return (itemGeo[type] = b.merge());
  }
  function item(type) { const m = new THREE.Mesh(itemGeometry(type), MAT.vc); m.castShadow = true; return m; }

  let bundleGeo = null;
  function cashBundleGeo() {
    if (bundleGeo) return bundleGeo;
    const b = new Builder();
    b.rbox(0.56, 0.13, 0.32, 0.03, 0x4cc13a, { y: 0.065 });
    b.box(0.13, 0.135, 0.325, 0xe9f7d6, { y: 0.066 });
    b.box(0.5, 0.01, 0.26, 0x6fd45f, { y: 0.131 });
    return (bundleGeo = b.merge());
  }
  let coinGeo = null;
  function coinGeometry() {
    if (coinGeo) return coinGeo;
    const b = new Builder();
    b.cyl(0.16, 0.16, 0.05, 14, PAL.gold, {});
    b.cyl(0.11, 0.11, 0.055, 14, 0xffe27a, {});
    return (coinGeo = b.merge());
  }

  // =========================================================================
  // Cars
  // =========================================================================
  const wheelGeos = {};
  function wheelGeo(r, wd = 0.34, col = PAL.tire, hub = PAL.hub) {
    const k = [r, wd, col, hub].join('|');
    if (wheelGeos[k]) return wheelGeos[k];
    const b = new Builder();
    b.cyl(r, r, wd, 16, col, { rx: Math.PI / 2 });
    b.cyl(r * 0.6, r * 0.6, wd + 0.03, 12, hub, { rx: Math.PI / 2 });
    b.cyl(r * 0.22, r * 0.22, wd + 0.06, 8, 0x6f7b8e, { rx: Math.PI / 2 });
    b.box(r * 1.1, r * 0.16, wd + 0.04, 0x8b95a5, {});
    b.box(r * 0.16, r * 1.1, wd + 0.04, 0x8b95a5, {});
    return (wheelGeos[k] = b.merge());
  }

  // sedan-like body; P = paint builder (white = paint colour), T = trim builder
  function sedanBody(P, T, o) {
    const top = o.by + o.bh;
    P.rbox(o.len, o.bh, o.w, 0.28, W, { y: o.by + o.bh / 2 });
    T.rbox(o.cl, o.ch, o.w - 0.16, 0.24, PAL.glass, { x: o.cx, y: top + o.ch / 2 - 0.06 });
    P.rbox(o.cl - 0.12, 0.14, o.w - 0.1, 0.07, W, { x: o.cx, y: top + o.ch - 0.03 });
    for (const sz of [-1, 1]) {
      P.box(0.13, o.ch - 0.04, 0.09, W, { x: o.cx + o.cl / 2 - 0.16, y: top + o.ch / 2 - 0.04, z: sz * (o.w / 2 - 0.1), rz: 0.35 });
      P.box(0.13, o.ch - 0.04, 0.09, W, { x: o.cx - o.cl / 2 + 0.16, y: top + o.ch / 2 - 0.04, z: sz * (o.w / 2 - 0.1), rz: -0.3 });
      P.box(0.11, o.ch - 0.06, 0.07, W, { x: o.cx + (o.bp || 0), y: top + o.ch / 2 - 0.04, z: sz * (o.w / 2 - 0.09) });
      T.rbox(0.08, 0.17, 0.38, 0.04, 0xfff3b0, { x: o.len / 2 - 0.03, y: o.by + o.bh * 0.62, z: sz * (o.w / 2 - 0.33) });
      T.rbox(0.08, 0.16, 0.34, 0.04, 0xff3b3b, { x: -o.len / 2 + 0.03, y: o.by + o.bh * 0.62, z: sz * (o.w / 2 - 0.3) });
      P.rbox(0.14, 0.12, 0.18, 0.04, W, { x: o.cx + o.cl / 2 - 0.05, y: top + 0.08, z: sz * (o.w / 2 + 0.07) });
    }
    T.rbox(0.2, 0.22, o.w - 0.08, 0.08, 0x8d96a5, { x: o.len / 2 - 0.03, y: o.by + 0.12 });
    T.rbox(0.2, 0.22, o.w - 0.08, 0.08, 0x8d96a5, { x: -o.len / 2 + 0.03, y: o.by + 0.12 });
    T.box(0.05, 0.16, o.w * 0.42, 0x2a2e36, { x: o.len / 2 + 0.01, y: o.by + o.bh * 0.42 });
    T.box(0.03, 0.15, 0.42, 0xffffff, { x: -o.len / 2 - 0.02, y: o.by + o.bh * 0.38 });
    return [
      { kind: 'top', x0: o.cx + o.cl / 2 + 0.05, x1: o.len / 2 - 0.25, y: top, z0: -o.w / 2 + 0.3, z1: o.w / 2 - 0.3 },
      { kind: 'top', x0: -o.len / 2 + 0.25, x1: o.cx - o.cl / 2 - 0.05, y: top, z0: -o.w / 2 + 0.3, z1: o.w / 2 - 0.3 },
      { kind: 'top', x0: o.cx - o.cl / 2 + 0.3, x1: o.cx + o.cl / 2 - 0.3, y: top + o.ch + 0.04, z0: -o.w / 2 + 0.35, z1: o.w / 2 - 0.35 },
      { kind: 'side', x0: -o.len / 2 + 0.35, x1: o.len / 2 - 0.35, y0: o.by + 0.18, y1: top - 0.12, z: o.w / 2 + 0.01 },
    ];
  }

  // Builds any car type. Returns { group, paintMat, wheels, len, w, h, surf, extras }
  function car(type, paint) {
    const g = new THREE.Group();
    const inner = new THREE.Group(); inner.name = 'Inner'; g.add(inner); // inner bounces
    const P = new Builder(), T = new Builder();
    const extras = {};
    let wheels = [], surf = [], len = 3.6, w = 1.9, h = 1.7, glassY = 1.3, wheelR = 0.42, wheelW = 0.34, wheelPos = [];
    const sed = (o) => { surf = sedanBody(P, T, o); len = o.len; w = o.w; h = o.by + o.bh + o.ch; glassY = o.by + o.bh + o.ch * 0.5; };
    const std4 = (x, z, r) => { wheelR = r; wheelPos = [[x, z], [-x, z], [x, -z], [-x, -z]]; };

    switch (type) {
      case 'hatch':
        sed({ len: 3.2, w: 1.82, by: 0.32, bh: 0.7, cl: 2.0, ch: 0.66, cx: -0.28 });
        std4(1.0, 0.8, 0.4); break;
      case 'pickup': {
        len = 4.0; w = 1.95;
        P.rbox(4.0, 0.72, 1.95, 0.24, W, { y: 0.36 + 0.36 });
        T.rbox(1.55, 0.7, 1.8, 0.22, PAL.glass, { x: 0.45, y: 1.08 + 0.3 });
        P.rbox(1.45, 0.14, 1.85, 0.06, W, { x: 0.45, y: 1.72 });
        P.box(0.12, 0.66, 0.1, W, { x: 1.12, y: 1.4, z: 0.86, rz: 0.35 }); P.box(0.12, 0.66, 0.1, W, { x: 1.12, y: 1.4, z: -0.86, rz: 0.35 });
        // bed walls
        P.box(1.9, 0.4, 0.12, W, { x: -1.0, y: 1.26, z: 0.92 }); P.box(1.9, 0.4, 0.12, W, { x: -1.0, y: 1.26, z: -0.92 });
        P.box(0.12, 0.4, 1.95, W, { x: -1.96, y: 1.26 });
        T.box(1.85, 0.04, 1.7, 0x3a3f47, { x: -1.0, y: 1.09 });
        for (const sz of [-1, 1]) { T.rbox(0.08, 0.17, 0.38, 0.04, 0xfff3b0, { x: 2.0, y: 0.8, z: sz * 0.62 }); T.rbox(0.08, 0.17, 0.3, 0.04, 0xff3b3b, { x: -2.0, y: 0.8, z: sz * 0.66 }); }
        T.rbox(0.22, 0.24, 1.9, 0.08, 0x8d96a5, { x: 2.0, y: 0.48 }); T.rbox(0.22, 0.24, 1.9, 0.08, 0x8d96a5, { x: -2.0, y: 0.48 });
        T.box(0.05, 0.2, 0.9, 0x2a2e36, { x: 2.02, y: 0.7 });
        surf = [{ kind: 'top', x0: 1.3, x1: 1.8, y: 1.08, z0: -0.6, z1: 0.6 }, { kind: 'side', x0: -1.7, x1: 1.7, y0: 0.55, y1: 1.3, z: 0.99 }, { kind: 'top', x0: 0.0, x1: 0.9, y: 1.8, z0: -0.5, z1: 0.5 }];
        h = 1.8; glassY = 1.38; std4(1.25, 0.86, 0.46); break;
      }
      case 'van': {
        len = 3.9; w = 1.98;
        P.rbox(3.9, 0.95, 1.98, 0.28, W, { y: 0.34 + 0.47 });
        T.rbox(3.5, 0.78, 1.86, 0.26, PAL.glass, { x: -0.12, y: 1.29 + 0.36 });
        P.rbox(3.45, 0.18, 1.92, 0.08, W, { x: -0.12, y: 2.1 });
        for (const sz of [-1, 1]) {
          P.box(0.14, 0.76, 0.1, W, { x: -0.5, y: 1.66, z: sz * 0.9 }); P.box(0.14, 0.76, 0.1, W, { x: 0.6, y: 1.66, z: sz * 0.9 });
          P.box(0.14, 0.76, 0.1, W, { x: -1.75, y: 1.66, z: sz * 0.9 });
          T.rbox(0.08, 0.2, 0.36, 0.04, 0xfff3b0, { x: 1.95, y: 0.95, z: sz * 0.62 }); T.rbox(0.08, 0.2, 0.3, 0.04, 0xff3b3b, { x: -1.95, y: 1.0, z: sz * 0.68 });
        }
        T.rbox(0.22, 0.24, 1.9, 0.08, 0x8d96a5, { x: 1.95, y: 0.42 }); T.rbox(0.22, 0.24, 1.9, 0.08, 0x8d96a5, { x: -1.95, y: 0.42 });
        surf = [{ kind: 'top', x0: -1.5, x1: 1.2, y: 2.2, z0: -0.6, z1: 0.6 }, { kind: 'side', x0: -1.6, x1: 1.6, y0: 0.5, y1: 1.2, z: 1.0 }];
        h = 2.2; glassY = 1.65; std4(1.25, 0.86, 0.42); break;
      }
      case 'suv':
        sed({ len: 3.9, w: 2.0, by: 0.42, bh: 0.82, cl: 2.5, ch: 0.72, cx: -0.25 });
        P.box(2.2, 0.06, 0.08, 0x3a3f47, { x: -0.25, y: 2.08, z: 0.6 }); P.box(2.2, 0.06, 0.08, 0x3a3f47, { x: -0.25, y: 2.08, z: -0.6 });
        std4(1.25, 0.88, 0.48); break;
      case 'taxi':
        sed({ len: 3.6, w: 1.9, by: 0.32, bh: 0.72, cl: 1.95, ch: 0.66, cx: -0.15 });
        T.rbox(0.72, 0.3, 0.36, 0.06, 0xffffff, { x: -0.15, y: 1.89 });
        T.box(0.74, 0.08, 0.38, 0x2a2e36, { x: -0.15, y: 1.8 });
        for (let i = 0; i < 7; i++) for (const sz of [-1, 1]) T.box(0.24, 0.12, 0.02, i % 2 ? 0x2a2e36 : 0xffffff, { x: -1.3 + i * 0.4, y: 0.75, z: sz * 0.955 });
        std4(1.15, 0.84, 0.42); break;
      case 'police': {
        sed({ len: 3.6, w: 1.9, by: 0.32, bh: 0.72, cl: 1.95, ch: 0.66, cx: -0.15 });
        for (const sz of [-1, 1]) T.box(1.6, 0.34, 0.02, 0x1d3a8a, { x: -0.1, y: 0.62, z: sz * 0.955 });
        T.box(0.9, 0.08, 1.2, 0x2a2e36, { x: -0.15, y: 1.76 });
        const red = new THREE.Mesh(GEO.rbox(0.36, 0.18, 0.5, 0.05), new THREE.MeshBasicMaterial({ color: 0xff2a2a }));
        const blue = new THREE.Mesh(GEO.rbox(0.36, 0.18, 0.5, 0.05), new THREE.MeshBasicMaterial({ color: 0x2a6bff }));
        red.position.set(-0.15, 1.88, 0.3); blue.position.set(-0.15, 1.88, -0.3); red.name = 'SirenRed'; blue.name = 'SirenBlue';
        inner.add(red, blue); extras.siren = [red, blue];
        std4(1.15, 0.84, 0.42); break;
      }
      case 'icecream': {
        len = 4.0; w = 2.0;
        P.rbox(4.0, 1.75, 2.0, 0.3, W, { y: 0.34 + 0.88 });
        T.rbox(0.6, 0.7, 1.8, 0.2, PAL.glass, { x: 1.62, y: 1.6 });
        T.rbox(1.5, 0.7, 0.05, 0.03, 0x3a3f47, { x: -0.3, y: 1.4, z: 1.01 });
        for (let i = 0; i < 6; i++) T.box(0.26, 0.24, 0.5, i % 2 ? 0xff6fb1 : 0xffffff, { x: -0.95 + i * 0.26, y: 1.86, z: 1.15, rx: 0.35 });
        T.cone(0.34, 0.9, 10, 0xe9b46a, { x: -0.4, y: 2.62, rx: Math.PI });
        T.sphere(0.36, 0xff9ec8, { x: -0.4, y: 3.12, ws: 12, hs: 8 });
        T.sphere(0.3, 0xfff2df, { x: -0.4, y: 3.48, ws: 12, hs: 8 });
        T.sphere(0.1, 0xe8383d, { x: -0.4, y: 3.8, ws: 8, hs: 6 });
        for (const sz of [-1, 1]) { T.rbox(0.08, 0.2, 0.36, 0.04, 0xfff3b0, { x: 2.0, y: 0.8, z: sz * 0.62 }); T.rbox(0.08, 0.2, 0.3, 0.04, 0xff3b3b, { x: -2.0, y: 0.8, z: sz * 0.66 }); }
        T.rbox(0.22, 0.24, 1.9, 0.08, 0x8d96a5, { x: 2.0, y: 0.42 });
        surf = [{ kind: 'side', x0: -1.7, x1: 1.6, y0: 0.5, y1: 1.1, z: 1.01 }, { kind: 'top', x0: 0.4, x1: 1.6, y: 2.1, z0: -0.6, z1: 0.6 }];
        h = 2.4; glassY = 1.6; std4(1.3, 0.88, 0.42); break;
      }
      case 'pizza':
        sed({ len: 3.2, w: 1.82, by: 0.32, bh: 0.7, cl: 2.0, ch: 0.66, cx: -0.28 });
        T.cyl(0.08, 0.08, 0.22, 6, 0x6f7b8e, { x: -0.28, y: 1.79 });
        T.cyl(0.92, 0.92, 0.1, 20, 0xffb54d, { x: -0.28, y: 1.92 });
        T.cyl(0.8, 0.8, 0.11, 20, 0xff5a36, { x: -0.28, y: 1.93 });
        T.cyl(0.74, 0.74, 0.115, 20, 0xffe08a, { x: -0.28, y: 1.935 });
        for (let i = 0; i < 7; i++) { const a = i * 0.9, r = i ? 0.45 : 0; T.cyl(0.11, 0.11, 0.12, 10, 0xc8282c, { x: -0.28 + Math.cos(a) * r, y: 1.945, z: Math.sin(a) * r }); }
        std4(1.0, 0.8, 0.4); break;
      case 'hotdog': {
        len = 4.1; w = 1.9;
        P.rbox(4.0, 0.62, 1.9, 0.3, W, { y: 0.66 });
        T.rbox(3.8, 0.6, 0.85, 0.3, 0xe8b065, { y: 1.18, z: 0.5 }); T.rbox(3.8, 0.6, 0.85, 0.3, 0xe8b065, { y: 1.18, z: -0.5 });
        T.cyl(0.38, 0.38, 3.9, 14, 0xc9502f, { y: 1.42, rz: Math.PI / 2 });
        T.sphere(0.38, 0xc9502f, { x: 1.95, y: 1.42, ws: 12, hs: 8 }); T.sphere(0.38, 0xc9502f, { x: -1.95, y: 1.42, ws: 12, hs: 8 });
        for (let i = 0; i < 9; i++) T.box(0.4, 0.06, 0.12, 0xffd23f, { x: -1.6 + i * 0.4, y: 1.81, z: (i % 2 ? 0.12 : -0.12), ry: i % 2 ? 0.6 : -0.6 });
        T.rbox(0.6, 0.4, 1.4, 0.15, PAL.glass, { x: 1.2, y: 1.25 });
        for (const sz of [-1, 1]) T.rbox(0.08, 0.16, 0.34, 0.04, 0xfff3b0, { x: 2.0, y: 0.7, z: sz * 0.6 });
        surf = [{ kind: 'side', x0: -1.7, x1: 1.7, y0: 0.45, y1: 0.9, z: 0.96 }, { kind: 'top', x0: -1.6, x1: 1.6, y: 1.8, z0: -0.15, z1: 0.15 }];
        h = 1.85; glassY = 1.3; std4(1.3, 0.85, 0.4); break;
      }
      case 'duck': {
        len = 3.5; w = 1.9;
        P.sphere(1.0, W, { y: 0.98, sx: 1.75, sy: 0.72, sz: 0.98, ws: 18, hs: 12 });
        P.sphere(0.62, W, { x: 1.25, y: 1.85, ws: 16, hs: 12 });
        P.sphere(0.4, W, { x: -1.55, y: 1.35, sx: 1.2, sy: 0.6, ws: 10, hs: 8 });
        T.rbox(0.56, 0.16, 0.5, 0.06, 0xff8a1f, { x: 1.88, y: 1.76 });
        T.sphere(0.12, 0xffffff, { x: 1.68, y: 2.0, z: 0.3, ws: 8, hs: 6 }); T.sphere(0.12, 0xffffff, { x: 1.68, y: 2.0, z: -0.3, ws: 8, hs: 6 });
        T.sphere(0.065, 0x1b2333, { x: 1.78, y: 2.02, z: 0.33, ws: 8, hs: 6 }); T.sphere(0.065, 0x1b2333, { x: 1.78, y: 2.02, z: -0.33, ws: 8, hs: 6 });
        T.rbox(1.1, 0.5, 1.5, 0.22, PAL.glass, { x: -0.1, y: 1.62 });
        surf = [{ kind: 'side', x0: -1.2, x1: 1.0, y0: 0.6, y1: 1.2, z: 0.98 }, { kind: 'top', x0: -1.4, x1: -0.8, y: 1.45, z0: -0.4, z1: 0.4 }];
        h = 2.5; glassY = 1.62; std4(1.1, 0.82, 0.4); break;
      }
      case 'buggy': {
        len = 3.2; w = 1.9;
        P.rbox(3.0, 0.4, 1.6, 0.18, W, { y: 0.7 });
        P.rbox(0.9, 0.35, 1.5, 0.16, W, { x: 1.1, y: 0.98 });
        T.box(0.5, 0.5, 0.5, 0x2a2e36, { x: -0.2, y: 1.15, z: 0.38 }); T.box(0.5, 0.5, 0.5, 0x2a2e36, { x: -0.2, y: 1.15, z: -0.38 });
        for (const sz of [-1, 1]) {
          T.cyl(0.05, 0.05, 1.2, 6, 0x3a3f47, { x: 0.45, y: 1.5, z: sz * 0.72, rz: 0.35 });
          T.cyl(0.05, 0.05, 1.1, 6, 0x3a3f47, { x: -0.95, y: 1.45, z: sz * 0.72, rz: -0.2 });
        }
        T.cyl(0.05, 0.05, 1.44, 6, 0x3a3f47, { x: 0.25, y: 2.05, rx: Math.PI / 2 });
        T.cyl(0.05, 0.05, 1.44, 6, 0x3a3f47, { x: -0.85, y: 1.98, rx: Math.PI / 2 });
        surf = [{ kind: 'top', x0: 0.75, x1: 1.45, y: 1.16, z0: -0.5, z1: 0.5 }, { kind: 'side', x0: -1.3, x1: 1.3, y0: 0.55, y1: 0.85, z: 0.81 }];
        h = 2.1; glassY = 1.4; wheelW = 0.46; std4(1.08, 0.95, 0.5); break;
      }
      case 'tractor': {
        len = 3.6; w = 1.8;
        P.rbox(1.9, 0.9, 1.0, 0.18, W, { x: 0.75, y: 1.05 });
        P.rbox(1.3, 0.3, 1.7, 0.1, W, { x: -0.8, y: 0.95 });
        T.rbox(1.15, 1.25, 1.35, 0.12, PAL.glass, { x: -0.75, y: 1.75 });
        P.rbox(1.35, 0.14, 1.5, 0.06, W, { x: -0.75, y: 2.44 });
        T.cyl(0.07, 0.07, 0.9, 8, 0x3a3f47, { x: 1.2, y: 1.85 });
        T.box(0.08, 0.5, 0.6, 0x2a2e36, { x: 1.71, y: 1.0 });
        surf = [{ kind: 'top', x0: 0.1, x1: 1.5, y: 1.5, z0: -0.35, z1: 0.35 }, { kind: 'side', x0: 0.0, x1: 1.6, y0: 0.7, y1: 1.4, z: 0.51 }];
        h = 2.5; glassY = 1.75;
        wheelPos = [[1.15, 0.72, 0.42], [1.15, -0.72, 0.42], [-0.8, 0.98, 0.78], [-0.8, -0.98, 0.78]];
        wheelR = 0; wheelW = 0.42; break;
      }
      case 'snowplow': {
        len = 4.2; w = 2.0;
        P.rbox(1.6, 1.3, 2.0, 0.24, W, { x: 0.55, y: 1.15 });
        T.rbox(1.3, 0.6, 1.9, 0.18, PAL.glass, { x: 0.6, y: 1.65 });
        P.rbox(1.4, 0.14, 1.95, 0.06, W, { x: 0.6, y: 2.02 });
        P.rbox(2.3, 1.0, 2.0, 0.2, W, { x: -1.0, y: 1.0 });
        T.box(0.14, 0.9, 2.6, 0xffd23f, { x: 2.15, y: 0.6, ry: 0.25 });
        for (let i = 0; i < 4; i++) T.box(0.15, 0.2, 0.3, 0x2a2e36, { x: 2.17, y: 0.6, z: -0.9 + i * 0.6, ry: 0.25 });
        T.rbox(0.8, 0.16, 0.3, 0.05, 0xff8a1f, { x: 0.6, y: 2.18 });
        surf = [{ kind: 'top', x0: -2.0, x1: 0.0, y: 1.5, z0: -0.6, z1: 0.6 }, { kind: 'side', x0: -1.9, x1: 1.2, y0: 0.6, y1: 1.3, z: 1.01 }];
        h = 2.3; glassY = 1.65; std4(1.35, 0.88, 0.46); break;
      }
      case 'rover': {
        len = 3.8; w = 2.0;
        P.rbox(3.4, 0.6, 1.8, 0.2, W, { y: 1.0 });
        T.rbox(1.4, 0.7, 1.5, 0.3, PAL.glass, { x: 0.6, y: 1.6 });
        T.box(1.6, 0.06, 1.4, 0x2b4a9a, { x: -0.8, y: 1.66, rz: 0.15 });
        for (let i = 0; i < 4; i++) T.box(0.02, 0.07, 1.4, 0x9fb4e8, { x: -1.4 + i * 0.4, y: 1.69, rz: 0.15 });
        T.cyl(0.04, 0.04, 0.9, 6, 0x9aa3b1, { x: -0.2, y: 2.0, z: 0.6 });
        T.cone(0.3, 0.2, 10, 0xeef1f6, { x: -0.2, y: 2.5, z: 0.6, rx: Math.PI });
        surf = [{ kind: 'side', x0: -1.4, x1: 1.4, y0: 0.8, y1: 1.2, z: 0.91 }, { kind: 'top', x0: -0.1, x1: 1.4, y: 1.3, z0: -0.7, z1: 0.7 }];
        h = 2.6; glassY = 1.6;
        wheelPos = [[1.3, 0.98, 0.42], [0, 0.98, 0.42], [-1.3, 0.98, 0.42], [1.3, -0.98, 0.42], [0, -0.98, 0.42], [-1.3, -0.98, 0.42]];
        wheelR = 0; break;
      }
      case 'ufo': {
        len = 3.6; w = 3.4;
        const hover = new THREE.Group(); hover.name = 'Hover'; inner.add(hover); extras.hover = hover;
        const b = new Builder();
        b.sphere(1.75, 0xb8c2d2, { y: 0.0, sy: 0.28, ws: 24, hs: 10 });
        b.cyl(1.2, 1.5, 0.3, 24, 0x8b95a5, { y: -0.25 });
        b.sphere(0.36, 0x7dff6a, { y: 0.62, ws: 12, hs: 8 });
        b.sphere(0.09, 0x1b2333, { x: 0.25, y: 0.7, z: 0.14, ws: 6, hs: 4 }); b.sphere(0.09, 0x1b2333, { x: 0.25, y: 0.7, z: -0.14, ws: 6, hs: 4 });
        b.cyl(0.02, 0.02, 0.3, 4, 0x7dff6a, { x: 0, y: 1.0, z: 0.12, rx: 0.4 }); b.sphere(0.05, 0xffd23f, { y: 1.15, z: 0.18, ws: 6, hs: 4 });
        const body = b.build(); hover.add(body); hover.position.y = 1.25;
        const dome = new THREE.Mesh(GEO.sphere(0.78, 18, 12), new THREE.MeshLambertMaterial({ color: 0xbfe8ff, transparent: true, opacity: 0.4, depthWrite: false }));
        dome.position.y = 0.42; dome.scale.y = 0.85; hover.add(dome);
        const lights = [];
        for (let i = 0; i < 8; i++) {
          const a = (i / 8) * Math.PI * 2;
          const l = new THREE.Mesh(GEO.sphere(0.1, 8, 6), new THREE.MeshBasicMaterial({ color: i % 2 ? 0xffd23f : 0xff6fb1 }));
          l.position.set(Math.cos(a) * 1.62, 0.02, Math.sin(a) * 1.62); l.name = 'Light' + i; hover.add(l); lights.push(l);
        }
        extras.lights = lights;
        surf = [{ kind: 'top', x0: -1.1, x1: 1.1, y: 0.3, z0: -1.1, z1: 1.1, parent: 'hover' }];
        h = 2.4; glassY = 1.7; wheelPos = []; break;
      }
      case 'limo':
        sed({ len: 5.4, w: 1.95, by: 0.3, bh: 0.7, cl: 3.6, ch: 0.62, cx: -0.3, bp: -0.6 });
        for (const sz of [-1, 1]) P.box(0.11, 0.56, 0.07, W, { x: 0.6, y: 1.31, z: sz * 0.885 });
        wheelPos = [[1.85, 0.86], [-1.85, 0.86], [1.85, -0.86], [-1.85, -0.86]]; wheelR = 0.42; break;
      case 'sports':
        sed({ len: 3.9, w: 2.0, by: 0.26, bh: 0.55, cl: 1.7, ch: 0.52, cx: -0.35 });
        T.box(0.5, 0.06, 1.9, 0x2a2e36, { x: -1.85, y: 1.22 });
        T.box(0.08, 0.36, 0.08, 0x2a2e36, { x: -1.8, y: 1.0, z: 0.7 }); T.box(0.08, 0.36, 0.08, 0x2a2e36, { x: -1.8, y: 1.0, z: -0.7 });
        T.box(1.3, 0.02, 0.24, 0xffffff, { x: 0.9, y: 0.82, z: 0.25 }); T.box(1.3, 0.02, 0.24, 0xffffff, { x: 0.9, y: 0.82, z: -0.25 });
        wheelW = 0.4; std4(1.25, 0.88, 0.44); break;
      case 'monster': {
        len = 4.3; w = 2.3;
        const by = 1.25;
        P.rbox(4.1, 0.75, 2.1, 0.26, W, { y: by + 0.37 });
        T.rbox(1.6, 0.72, 1.95, 0.22, PAL.glass, { x: 0.4, y: by + 1.05 });
        P.rbox(1.5, 0.14, 2.0, 0.06, W, { x: 0.4, y: by + 1.45 });
        P.box(1.9, 0.4, 0.12, W, { x: -1.05, y: by + 0.9, z: 1.0 }); P.box(1.9, 0.4, 0.12, W, { x: -1.05, y: by + 0.9, z: -1.0 });
        for (const sx of [-1, 1]) for (const sz of [-1, 1]) T.cyl(0.12, 0.12, 0.9, 8, 0xff4d5a, { x: sx * 1.35, y: 1.0, z: sz * 0.7 });
        T.box(3.0, 0.2, 0.3, 0x3a3f47, { y: 0.95 });
        for (let i = 0; i < 4; i++) T.box(0.4, 0.1, 0.3, 0xffd23f, { x: 1.0, y: by + 1.62, z: -0.6 + i * 0.4 });
        surf = [{ kind: 'side', x0: -1.8, x1: 1.8, y0: by + 0.1, y1: by + 0.6, z: 1.06 }, { kind: 'top', x0: 1.2, x1: 1.9, y: by + 0.75, z0: -0.6, z1: 0.6 }];
        h = by + 1.55; glassY = by + 1.05; wheelW = 0.62; std4(1.45, 1.2, 0.82); break;
      }
      case 'golden':
      case 'sedan':
      default:
        sed({ len: 3.6, w: 1.9, by: 0.32, bh: 0.72, cl: 1.95, ch: 0.66, cx: -0.15 });
        if (type === 'golden') {
          T.cyl(0.25, 0.3, 0.18, 10, PAL.gold, { x: -0.15, y: 1.82 });
          for (let i = 0; i < 5; i++) { const a = i / 5 * Math.PI * 2; T.cone(0.07, 0.2, 5, PAL.gold, { x: -0.15 + Math.cos(a) * 0.24, y: 2.0, z: Math.sin(a) * 0.24 }); }
        }
        std4(1.15, 0.84, 0.42);
    }

    const paintMat = new THREE.MeshLambertMaterial({ color: paint });
    if (type === 'golden') paintMat.emissive = new THREE.Color(0x553300);
    if (P.parts.length) { const pm = P.build(paintMat); pm.name = 'Paint'; (extras.hover || inner).add(pm); extras.paintMesh = pm; }
    if (T.parts.length) { const tm = T.build(); tm.name = 'Trim'; inner.add(tm); }
    // shadow blob
    const wg = wheelPos.length ? wheelGeo(wheelR || 0.42, wheelW) : null;
    for (const wp of wheelPos) {
      const r = wp[2] || wheelR;
      const wm = new THREE.Mesh(wp[2] ? wheelGeo(r, wheelW) : wg, MAT.vc);
      wm.position.set(wp[0], r, wp[1]); wm.name = 'Wheel' + wheels.length;
      wm.castShadow = true;
      inner.add(wm);
      wheels.push({ mesh: wm, r });
    }
    // "WASH ME" written in the dust on the window that faces the camera
    const washMe = new THREE.Mesh(GEO.plane(1.2, 0.42), new THREE.MeshBasicMaterial({ map: washMeTex(), transparent: true, depthWrite: false }));
    washMe.position.set(type === 'van' ? 0 : -0.1, glassY, w / 2 - 0.02 + (type === 'ufo' ? -0.6 : 0.01)); washMe.name = 'WashMe';
    if (type === 'hotdog' || type === 'buggy' || type === 'tractor' || type === 'ufo' || type === 'duck') washMe.visible = false;
    (extras.hover || inner).add(washMe);
    extras.washMe = washMe;
    return { group: g, inner, paintMat, wheels, len, w, h, surf, extras, type };
  }
  function washMeTex() {
    return TEX.make(256, 96, (ctx, w, h) => {
      ctx.clearRect(0, 0, w, h);
      ctx.fillStyle = 'rgba(150,115,70,.45)';
      ctx.beginPath(); ctx.ellipse(w / 2, h / 2, w / 2 - 6, h / 2 - 8, 0, 0, Math.PI * 2); ctx.fill();
      ctx.save(); ctx.translate(w / 2, h / 2); ctx.rotate(-0.06);
      TEX.text(ctx, 'WASH ME', 0, 2, 50, 'rgba(255,248,230,.9)', null, 0, 'center', "'Permanent Marker', 'Comic Sans MS', 'Lilita One', cursive");
      ctx.restore();
    }, 'washme');
  }

  // =========================================================================
  // Wash line: track pieces, gates, machines, hoppers, racks, pay booth
  // =========================================================================
  function stationFloor(b, x, z, kind) {
    // dark grate + yellow guide rails + hazard edges
    b.box(3.6, 0.03, 2.5, 0x3a3f47, { x, y: 0.035, z });
    for (let i = 0; i < 7; i++) b.box(3.4, 0.032, 0.14, 0x272b31, { x, y: 0.04, z: z - 1.05 + i * 0.35 });
    b.box(4.2, 0.07, 0.16, PAL.hazard, { x, y: 0.05, z: z + 1.38 });
    b.box(4.2, 0.07, 0.16, PAL.hazard, { x, y: 0.05, z: z - 1.38 });
    for (let i = 0; i < 6; i++) {
      b.box(0.34, 0.075, 0.17, 0x23262c, { x: x - 1.8 + i * 0.72, y: 0.052, z: z + 1.38 });
      b.box(0.34, 0.075, 0.17, 0x23262c, { x: x - 1.8 + i * 0.72, y: 0.052, z: z - 1.38 });
    }
  }
  function laneTrack(b, z, x0, x1) {
    b.box(x1 - x0, 0.02, 3.6, 0xaeb3bc, { x: (x0 + x1) / 2, y: 0.03, z });
    for (const sz of [-1, 1]) b.box(x1 - x0, 0.012, 0.14, 0xffffff, { x: (x0 + x1) / 2, y: 0.042, z: z + sz * 1.85 });
    // conveyor guide rail for the left wheels
    b.box(x1 - x0, 0.1, 0.12, 0x8d96a5, { x: (x0 + x1) / 2, y: 0.06, z: z - 0.52 });
    b.box(x1 - x0, 0.1, 0.12, 0x8d96a5, { x: (x0 + x1) / 2, y: 0.06, z: z - 1.18 });
  }

  function gate(label, light) {
    const g = new THREE.Group();
    const b = new Builder();
    for (const sz of [-1, 1]) {
      b.rbox(0.5, 3.7, 0.5, 0.1, PAL.blue, { y: 1.85, z: sz * 2.35 });
      b.rbox(0.72, 0.22, 0.72, 0.06, 0xd9dde4, { y: 0.11, z: sz * 2.35 });
    }
    b.rbox(0.62, 0.95, 5.3, 0.16, PAL.blue, { y: 3.95 });
    b.rbox(0.64, 0.16, 5.32, 0.06, PAL.blueDark, { y: 3.48 });
    const m = b.build(); g.add(m);
    const sign = new THREE.Mesh(GEO.plane(4.0, 1.0), new THREE.MeshLambertMaterial({ map: TEX.sign(label, '#2563eb', '#ffffff', 512, 128), transparent: true }));
    sign.position.set(0.0, 4.95, 0); sign.rotation.y = camYawRot();
    const back = new THREE.Mesh(GEO.rbox(4.1, 1.08, 0.12, 0.08), MAT.lam(PAL.blueDark));
    back.position.set(0, 4.95, 0); back.rotation.y = camYawRot();
    sign.position.add(new THREE.Vector3(Math.sin(camYawRot()), 0, Math.cos(camYawRot())).multiplyScalar(0.07));
    g.add(back, sign);
    // little traffic light on the camera side
    const lamp = new THREE.Mesh(GEO.sphere(0.2, 12, 8), new THREE.MeshBasicMaterial({ color: 0x3dff6a }));
    lamp.position.set(0.38, 3.95, 2.25); lamp.name = 'Lamp';
    const ring = new THREE.Mesh(GEO.cyl(0.27, 0.27, 0.1, 14), MAT.lam(0x2a2d33)); ring.rotation.z = Math.PI / 2; ring.position.set(0.32, 3.95, 2.25);
    g.add(ring, lamp);
    return { group: g, lamp };
  }

  // spinning blue roller brush
  let rollerGeo = null;
  function roller() {
    if (!rollerGeo) {
      const pts = [];
      const hgt = 2.85, n = 16;
      pts.push([0.001, 0]);
      for (let i = 0; i <= n; i++) { const y = (i / n) * hgt; pts.push([i % 2 ? 0.55 : 0.67, y]); }
      pts.push([0.001, hgt]);
      const b = new Builder();
      b.lathe(pts, 14, PAL.brush, { flat: false });
      b.cyl(0.24, 0.24, 0.12, 10, 0x6f7b8e, { y: hgt + 0.02 });
      rollerGeo = b.merge();
    }
    const m = new THREE.Mesh(rollerGeo, MAT.vc); m.castShadow = true; return m;
  }

  function pipe(b, pts, r = 0.11, col = PAL.blue) {
    for (let i = 0; i < pts.length - 1; i++) {
      const [x0, y0, z0] = pts[i], [x1, y1, z1] = pts[i + 1];
      const len = Math.hypot(x1 - x0, y1 - y0, z1 - z0);
      const mid = new THREE.Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
      const dir = new THREE.Vector3(x1 - x0, y1 - y0, z1 - z0).normalize();
      const q = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
      const m = new THREE.Matrix4().compose(mid, q, new THREE.Vector3(1, 1, 1));
      b.cyl(r, r, len, 8, col, { m });
      b.sphere(r * 1.25, col, { x: x1, y: y1, z: z1, ws: 8, hs: 6 });
    }
    b.sphere(r * 1.25, col, { x: pts[0][0], y: pts[0][1], z: pts[0][2], ws: 8, hs: 6 });
  }

  function hopper(supply) {
    const g = new THREE.Group();
    const col = CFG.supplies[supply].color;
    const b = new Builder();
    b.rbox(1.3, 0.18, 1.1, 0.05, 0x9aa3b1, { y: 0.09 });
    b.cyl(0.46, 0.5, 1.1, 16, 0xf4f6fa, { y: 0.73 });
    b.cyl(0.505, 0.505, 0.22, 16, col, { y: 0.55 });
    b.cyl(0.62, 0.46, 0.34, 16, col, { y: 1.45 });
    b.torus(0.6, 0.05, 0xffffff, { y: 1.62, rx: Math.PI / 2, ts: 20 });
    const m = b.build(); g.add(m);
    // fill level column (scaled)
    const fill = new THREE.Mesh(GEO.cyl(0.48, 0.48, 1, 16), new THREE.MeshLambertMaterial({ color: col, transparent: true, opacity: 0.0 }));
    fill.visible = false;
    // icon disc facing the camera
    const icon = new THREE.Mesh(GEO.circle(0.36, 20), new THREE.MeshBasicMaterial({ map: iconTex(CFG.supplies[supply].icon, '#ffffff'), transparent: true }));
    icon.position.set(0, 0.78, 0); icon.rotation.y = camYawRot(); icon.name = 'Icon';
    icon.position.add(new THREE.Vector3(Math.sin(camYawRot()), 0, Math.cos(camYawRot())).multiplyScalar(0.52));
    g.add(icon);
    // stock pips: 8 small lights around the funnel
    const pips = [];
    for (let i = 0; i < CFG.hopperCap; i++) {
      const p = new THREE.Mesh(GEO.box(0.12, 0.1, 0.06), new THREE.MeshBasicMaterial({ color: 0x3a3f47 }));
      const a = camYawRot();
      const off = (i - (CFG.hopperCap - 1) / 2) * 0.14;
      p.position.set(Math.sin(a) * 0.5 + Math.cos(a) * off, 1.18, Math.cos(a) * 0.5 - Math.sin(a) * off);
      p.rotation.y = a; p.name = 'Pip' + i;
      g.add(p); pips.push(p);
    }
    return { group: g, pips, fill, color: col };
  }
  function iconTex(name, bg = '#ffffff', ring = null) {
    return TEX.make(128, 128, (ctx) => {
      ctx.beginPath(); ctx.arc(64, 64, 60, 0, Math.PI * 2); ctx.fillStyle = bg; ctx.fill();
      if (ring) { ctx.lineWidth = 8; ctx.strokeStyle = ring; ctx.stroke(); }
      TEX.icon(ctx, name, 64, 64, 92);
    }, 'icon|' + name + '|' + bg + '|' + ring);
  }

  // ---------- machines (centred at origin; car runs along +x; south side = +z faces camera) ----------
  function legs4(b, dx, dz, h, col = PAL.white) {
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
      b.rbox(0.48, h, 0.48, 0.1, col, { x: sx * dx, y: h / 2, z: sz * dz });
      b.rbox(0.66, 0.22, 0.66, 0.06, 0x9aa3b1, { x: sx * dx, y: 0.11, z: sz * dz });
    }
  }
  function machine(type) {
    const g = new THREE.Group();
    const b = new Builder();
    const anim = { t: 0 };
    const accent = PAL.blue;
    if (type === 'brush') {
      // gantry beam on the west side; rollers hang east of it so they stay visible from above
      for (const sz of [-1, 1]) {
        b.rbox(0.55, 3.75, 0.55, 0.12, PAL.white, { x: -0.95, y: 1.87, z: sz * 2.2 });
        b.rbox(0.58, 0.2, 0.58, 0.08, accent, { x: -0.95, y: 2.6, z: sz * 2.2 });
        b.rbox(0.8, 0.24, 0.8, 0.08, 0x9aa3b1, { x: -0.95, y: 0.12, z: sz * 2.2 });
        b.rbox(1.9, 0.22, 0.34, 0.08, 0x9aa3b1, { x: 0.0, y: 3.42, z: sz * 1.62 });
        b.cyl(0.09, 0.09, 0.32, 8, 0x6f7b8e, { x: 0.32, y: 3.18, z: sz * 1.62 });
      }
      b.rbox(1.35, 0.95, 5.0, 0.3, PAL.white, { x: -0.95, y: 3.95 });
      b.rbox(1.38, 0.16, 5.03, 0.06, accent, { x: -0.95, y: 3.68 });
      b.rbox(1.38, 0.16, 5.03, 0.06, accent, { x: -0.95, y: 4.2 });
      b.rbox(0.9, 0.42, 0.08, 0.06, PAL.blueLight, { x: -0.95, y: 3.95, z: 2.52 });
      pipe(b, [[0, 1.25, 2.75], [0, 1.8, 2.75], [-0.95, 1.8, 2.75], [-0.95, 3.5, 2.75], [-0.95, 3.5, 2.5]]);
      const rl = roller(), rr = roller();
      rl.position.set(0.32, 0.25, -1.62); rr.position.set(0.32, 0.25, 1.62); rl.name = 'RollerL'; rr.name = 'RollerR';
      g.add(rl, rr);
      anim.update = (dt, active) => {
        anim.spin = U.damp(anim.spin || 0, active ? 9 : 0.3, 3, dt);
        rl.rotation.y += anim.spin * dt; rr.rotation.y -= anim.spin * dt;
      };
    } else if (type === 'rinse') {
      for (const sz of [-1, 1]) {
        b.rbox(0.62, 3.4, 0.7, 0.12, 0xe3e7ee, { y: 1.7, z: sz * 2.15 });
        b.rbox(0.8, 0.22, 0.88, 0.06, 0x9aa3b1, { y: 0.11, z: sz * 2.15 });
        b.rbox(0.66, 0.2, 0.74, 0.06, accent, { y: 2.6, z: sz * 2.15 });
      }
      b.rbox(1.25, 0.7, 5.0, 0.2, 0xe3e7ee, { y: 3.55 });
      b.rbox(1.0, 0.18, 4.4, 0.06, 0x3a3f47, { y: 3.14 });
      for (let i = 0; i < 9; i++) b.cyl(0.05, 0.07, 0.14, 6, 0x9aa3b1, { y: 3.0, z: -1.75 + i * 0.44 });
      pipe(b, [[-0.45, 3.95, -2.4], [-0.45, 4.25, -2.4], [-0.45, 4.25, 2.4], [-0.45, 3.95, 2.4]]);
      pipe(b, [[0.45, 4.15, 2.45], [0.45, 4.15, 2.8], [0.45, 0.4, 2.8]], 0.1);
      // water curtain
      const tex = TEX.waterStreaks(); tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
      const wmat = new THREE.MeshBasicMaterial({ map: tex, transparent: true, opacity: 0, depthWrite: false, side: THREE.DoubleSide, color: 0xbfe6ff });
      const curtains = [];
      for (const dx of [-0.25, 0.25]) {
        const c = new THREE.Mesh(GEO.plane(4.2, 3.0), wmat);
        c.rotation.y = Math.PI / 2; c.position.set(dx, 1.55, 0); c.name = 'Curtain' + curtains.length;
        g.add(c); curtains.push(c);
      }
      const puddle = new THREE.Mesh(GEO.circle(1, 20), new THREE.MeshBasicMaterial({ color: 0x5cc3ff, transparent: true, opacity: 0.45, depthWrite: false }));
      puddle.rotation.x = -Math.PI / 2; puddle.position.set(0.3, 0.06, 0.2); puddle.scale.set(2.3, 1.7, 1); puddle.name = 'Puddle';
      g.add(puddle);
      anim.update = (dt, active) => {
        anim.flow = U.damp(anim.flow || 0, active ? 0.92 : 0, 5, dt);
        wmat.opacity = anim.flow; tex.offset.y += dt * 2.2;
        puddle.material.opacity = 0.32 + anim.flow * 0.2;
      };
    } else if (type === 'dryer') {
      legs4(b, 0.8, 2.15, 3.3);
      b.rbox(2.0, 0.95, 5.0, 0.28, PAL.white, { y: 3.72 });
      b.rbox(2.04, 0.18, 5.04, 0.08, accent, { y: 3.32 });
      b.rbox(1.4, 0.1, 4.2, 0.04, 0x3a3f47, { y: 3.22 });
      for (let i = 0; i < 6; i++) b.box(1.2, 0.04, 0.12, 0x6f7b8e, { y: 3.17, z: -1.6 + i * 0.64 });
      // two big turbine fans on top, tilted toward the camera
      const fans = [];
      for (const sz of [-1, 1]) {
        const holder = new THREE.Group();
        holder.position.set(0.0, 4.55, sz * 1.18);
        holder.rotation.set(0.62, GFX.cam.yaw, 0, 'YXZ');
        const hb = new Builder();
        hb.cyl(0.92, 0.98, 0.62, 22, PAL.orange, {});
        hb.torus(0.9, 0.08, PAL.orangeDark, { y: 0.31, rx: Math.PI / 2, ts: 24 });
        hb.cyl(0.78, 0.78, 0.66, 22, 0x4a3214, { y: 0.02 });
        hb.rbox(0.5, 0.7, 0.5, 0.1, 0x9aa3b1, { y: -0.55 });
        const hm = hb.build(); holder.add(hm);
        const fb = new Builder();
        fb.cyl(0.18, 0.18, 0.14, 12, 0xeef1f6, {});
        for (let i = 0; i < 6; i++) fb.box(0.62, 0.04, 0.2, 0xffd27a, { x: Math.cos(i / 6 * Math.PI * 2) * 0.38, z: Math.sin(i / 6 * Math.PI * 2) * 0.38, ry: -i / 6 * Math.PI * 2, rx: 0.45 });
        const fan = fb.build(); fan.position.y = 0.3; holder.add(fan); fan.name = 'Fan' + fans.length; holder.name = 'FanHolder' + fans.length;
        g.add(holder); fans.push(fan);
      }
      anim.update = (dt, active) => {
        anim.spin = U.damp(anim.spin || 0, active ? 22 : 0.5, 2.5, dt);
        for (const f of fans) f.rotation.y += anim.spin * dt;
      };
    } else if (type === 'wax') {
      for (const sz of [-1, 1]) {
        b.rbox(0.6, 3.4, 0.7, 0.12, PAL.white, { y: 1.7, z: sz * 2.15 });
        b.rbox(0.8, 0.22, 0.88, 0.06, 0x9aa3b1, { y: 0.11, z: sz * 2.15 });
        b.rbox(0.64, 0.5, 0.74, 0.1, PAL.yellow, { y: 2.9, z: sz * 2.15 });
      }
      b.rbox(1.3, 0.72, 5.0, 0.22, PAL.white, { y: 3.55 });
      b.rbox(1.34, 0.24, 5.04, 0.08, 0xff8a1f, { y: 3.22 });
      b.cyl(0.08, 0.08, 4.2, 8, 0x9aa3b1, { y: 2.85, rx: Math.PI / 2 });
      for (let i = 0; i < 7; i++) b.cone(0.09, 0.2, 6, 0xff8a1f, { y: 2.72, z: -1.6 + i * 0.53, rx: Math.PI });
      b.cyl(0.55, 0.55, 0.9, 14, 0xffc93c, { y: 4.35 });
      b.cyl(0.57, 0.57, 0.2, 14, 0xff8a1f, { y: 4.1 });
      b.sphere(0.55, 0xffc93c, { y: 4.8, sy: 0.4, ws: 14, hs: 6 });
      pipe(b, [[0, 1.25, 2.75], [0, 2.2, 2.75], [0, 2.2, 2.45]], 0.1, 0xff8a1f);
    } else if (type === 'tire') {
      for (const sz of [-1, 1]) {
        b.rbox(2.6, 1.0, 0.6, 0.14, 0x4a505c, { y: 0.5, z: sz * 1.7 });
        b.rbox(2.64, 0.16, 0.64, 0.06, PAL.hazard, { y: 0.92, z: sz * 1.7 });
        for (let i = 0; i < 4; i++) b.box(0.3, 0.17, 0.66, 0x23262c, { x: -0.95 + i * 0.64, y: 0.93, z: sz * 1.7 });
        b.cyl(0.11, 0.11, 2.9, 8, PAL.blue, { x: -1.05, y: 1.45, z: sz * 2.15 });
      }
      b.cyl(0.11, 0.11, 4.3, 8, PAL.blue, { x: -1.05, y: 2.9, rx: Math.PI / 2 });
      b.sphere(0.14, PAL.blue, { x: -1.05, y: 2.9, z: 2.15, ws: 8, hs: 6 }); b.sphere(0.14, PAL.blue, { x: -1.05, y: 2.9, z: -2.15, ws: 8, hs: 6 });
      for (let i = 0; i < 5; i++) b.cone(0.08, 0.18, 6, 0x3ad17a, { x: -1.05, y: 2.75, z: -1.6 + i * 0.8, rx: Math.PI });
      const brushes = [];
      for (const sz of [-1, 1]) {
        const rb = new THREE.Mesh(GEO.cyl(0.28, 0.28, 2.2, 10), MAT.lam(0x22252b));
        rb.rotation.z = Math.PI / 2; rb.position.set(0, 0.42, sz * 1.27); rb.name = 'TireBrush' + brushes.length; g.add(rb); brushes.push(rb);
      }
      anim.update = (dt, active) => {
        anim.spin = U.damp(anim.spin || 0, active ? 14 : 0.2, 3, dt);
        for (const r of brushes) r.rotation.x += anim.spin * dt;
      };
    } else if (type === 'polish') {
      // open frame so the shiny car stays visible
      for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
        b.cyl(0.1, 0.12, 3.7, 8, 0xeef1f6, { x: sx * 2.0, y: 1.85, z: sz * 2.2 });
        b.rbox(0.4, 0.16, 0.4, 0.06, 0xb44cff, { x: sx * 2.0, y: 0.08, z: sz * 2.2 });
      }
      for (const sz of [-1, 1]) b.rbox(4.4, 0.24, 0.3, 0.08, 0xb44cff, { y: 3.72, z: sz * 2.2 });
      for (const sx of [-1, 1]) b.rbox(0.3, 0.24, 4.7, 0.08, 0xeef1f6, { x: sx * 2.0, y: 3.72 });
      b.box(3.9, 0.025, 3.2, 0x7b2ff7, { y: 0.045 });
      b.box(3.5, 0.03, 2.8, 0x9b59ff, { y: 0.05 });
      for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
        b.cyl(0.2, 0.13, 0.34, 10, 0x3a3f47, { x: sx * 1.4, y: 3.5, z: sz * 2.2, rx: -sz * 0.7 });
        b.cyl(0.15, 0.15, 0.02, 10, 0xfff6c4, { x: sx * 1.4, y: 3.36, z: sz * 2.08, rx: -sz * 0.7 });
      }
    }
    if (b.parts.length) g.add(b.build());
    return { group: g, anim };
  }

  function payBooth() {
    // booth sits south of the lane; origin = booth centre
    const g = new THREE.Group();
    const b = new Builder();
    b.rbox(2.4, 0.18, 1.8, 0.06, 0xb9c0cb, { y: 0.09 });
    b.rbox(2.2, 1.05, 0.46, 0.1, PAL.white, { y: 0.66, z: -0.62 });
    b.rbox(2.24, 0.16, 0.5, 0.05, PAL.blue, { y: 1.22, z: -0.62 });
    b.rbox(0.42, 1.05, 1.5, 0.1, PAL.white, { x: -1.0, y: 0.66, z: 0.05 });
    for (const sx of [-1, 1]) b.cyl(0.07, 0.07, 2.3, 8, 0xd9dde4, { x: sx * 1.05, y: 1.3, z: 0.75 });
    b.cyl(0.07, 0.07, 1.25, 8, 0xd9dde4, { x: 1.05, y: 1.9, z: -0.62 });
    b.cyl(0.07, 0.07, 1.25, 8, 0xd9dde4, { x: -1.05, y: 1.9, z: -0.62 });
    b.rbox(2.6, 0.2, 2.0, 0.08, PAL.blue, { y: 2.5, z: 0.05 });
    b.rbox(2.64, 0.12, 2.04, 0.05, 0xffffff, { y: 2.36, z: 0.05 });
    // register
    b.rbox(0.62, 0.32, 0.44, 0.06, 0x4f8cff, { x: 0.25, y: 1.36, z: -0.62 });
    b.rbox(0.42, 0.24, 0.06, 0.03, 0x2a3b55, { x: 0.25, y: 1.62, z: -0.72, rx: -0.3 });
    b.box(0.3, 0.12, 0.02, 0x5df07a, { x: 0.25, y: 1.63, z: -0.755, rx: -0.3 });
    // barrier post
    b.rbox(0.36, 1.2, 0.36, 0.08, 0xffc93c, { x: 1.55, y: 0.6, z: -1.05 });
    const m = b.build(); g.add(m);
    const sign = new THREE.Mesh(GEO.plane(1.9, 0.62), new THREE.MeshLambertMaterial({ map: TEX.sign('PAY', '#46c23a', '#ffffff', 256, 84, 'cash'), transparent: true }));
    sign.position.set(0, 3.0, 0.2); sign.rotation.y = camYawRot();
    g.add(sign);
    // barrier arm pivots at the post top, spans the lane toward -z
    const pivot = new THREE.Group(); pivot.name = 'Barrier'; pivot.position.set(1.55, 1.15, -1.05);
    const ab = new Builder();
    for (let i = 0; i < 8; i++) ab.box(0.14, 0.14, 0.44, i % 2 ? 0xffffff : 0xe8383d, { y: 0, z: -0.25 - i * 0.44 });
    const arm = ab.build(); pivot.add(arm); g.add(pivot);
    return { group: g, barrier: pivot };
  }

  function cashTable() {
    const b = new Builder();
    b.rbox(1.6, 0.14, 1.2, 0.04, 0xc9884f, { y: 0.32 });
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) b.box(0.12, 0.3, 0.12, 0x8a5a33, { x: sx * 0.65, y: 0.15, z: sz * 0.45 });
    return b.build();
  }

  // Supply station: a machine topped by a giant replica of its product, and an output table
  // where finished products stack up. Origin = table centre; pickup mat is on the north side.
  function rack(supply) {
    const g = new THREE.Group();
    const col = CFG.supplies[supply].color;
    const b = new Builder();
    // machine
    b.rbox(1.5, 1.55, 1.3, 0.2, 0xf4f6fa, { x: -1.95, y: 0.78 });
    b.rbox(1.54, 0.26, 1.34, 0.1, col, { x: -1.95, y: 1.2 });
    b.rbox(1.54, 0.12, 1.34, 0.05, PAL.blue, { x: -1.95, y: 0.12 });
    b.rbox(0.7, 0.44, 0.06, 0.05, 0x2a3b55, { x: -1.95, y: 0.72, z: 0.66 });
    b.box(0.5, 0.18, 0.02, 0x5df07a, { x: -1.95, y: 0.76, z: 0.695 });
    b.cyl(0.12, 0.12, 0.5, 8, 0x9aa3b1, { x: -1.2, y: 1.05, rz: Math.PI / 2 });
    // output table
    b.rbox(2.5, 0.12, 1.25, 0.05, 0xdfe7f5, { x: 0.35, y: 0.72 });
    b.rbox(2.56, 0.12, 1.31, 0.05, PAL.blue, { x: 0.35, y: 0.62 });
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) b.box(0.12, 0.6, 0.12, PAL.blue, { x: 0.35 + sx * 1.12, y: 0.3, z: sz * 0.5 });
    const m = b.build(); g.add(m);
    // giant product replica on top of the machine
    const giant = new THREE.Mesh(itemGeometry(supply), MAT.vc);
    giant.scale.setScalar(2.7); giant.position.set(-1.95, 1.55, 0); giant.rotation.y = 0.5; giant.castShadow = true; giant.name = 'Giant';
    g.add(giant);
    const sign = new THREE.Mesh(GEO.plane(2.2, 0.48), new THREE.MeshLambertMaterial({ map: TEX.sign(CFG.supplies[supply].name.toUpperCase(), '#ffffff', '#1a46b8', 512, 112, CFG.supplies[supply].icon), transparent: true }));
    sign.position.set(-1.95, 1.0, 0.68);
    g.add(sign);
    const slots = [];
    for (let layer = 0; layer < 3; layer++) for (const z of [-0.27, 0.27]) for (let i = 0; i < 3; i++) slots.push(new THREE.Vector3(-0.4 + i * 0.75, 0.78 + layer * 0.45, z));
    slots.forEach((s, i) => { const o = new THREE.Object3D(); o.name = 'Slot' + i; o.position.copy(s); g.add(o); });
    return { group: g, slots, giant };
  }

  // =========================================================================
  // Buildings
  // =========================================================================
  function signBoard(label, w, h, bg, fg, icon, texW = 512) {
    const texH = Math.round(texW * h / w);
    const grp = new THREE.Group();
    const back = new THREE.Mesh(GEO.rbox(w + 0.15, h + 0.15, 0.2, 0.1), MAT.lam(new THREE.Color(bg).multiplyScalar(0.8).getHex()));
    const face = new THREE.Mesh(GEO.plane(w, h), new THREE.MeshLambertMaterial({ map: TEX.sign(label, bg, fg, texW, texH, icon), transparent: true }));
    face.position.z = 0.11;
    back.castShadow = true;
    grp.add(back, face);
    return grp;
  }

  function carWashSignTex() {
    return TEX.make(1024, 360, (ctx, w, h) => {
      TEX.rr(ctx, 8, 8, w - 16, h - 16, 70); ctx.fillStyle = '#2563eb'; ctx.fill();
      ctx.lineWidth = 12; ctx.strokeStyle = '#ffffff'; TEX.rr(ctx, 26, 26, w - 52, h - 52, 56); ctx.stroke();
      // bubbles
      ctx.fillStyle = '#9fd8ff';
      [[110, 90, 26], [70, 160, 16], [930, 110, 30], [960, 200, 16], [880, 260, 22], [140, 280, 14]].forEach(([x, y, r]) => { ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill(); });
      ctx.fillStyle = '#ffffff';
      [[100, 82, 7], [922, 100, 8]].forEach(([x, y, r]) => { ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill(); });
      // car icon
      ctx.save(); ctx.translate(180, 105); ctx.scale(3.4, 3.4);
      ctx.fillStyle = '#ffffff';
      const p = new Path2D('M6 42 L9 32 Q11 27 17 27 L23 18 Q25 15 30 15 L43 15 Q47 15 49 19 L54 27 Q59 28 59 33 L59 41 Q59 44 56 44 L9 44 Q6 44 6 42Z');
      ctx.fill(p);
      ctx.fillStyle = '#2563eb';
      ctx.beginPath(); ctx.arc(19, 44, 7, 0, Math.PI * 2); ctx.fill(); ctx.beginPath(); ctx.arc(46, 44, 7, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = '#ffffff';
      ctx.beginPath(); ctx.arc(19, 44, 4.5, 0, Math.PI * 2); ctx.fill(); ctx.beginPath(); ctx.arc(46, 44, 4.5, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = '#2563eb'; ctx.fill(new Path2D('M24 26 L28 19 Q29 18 31 18 L42 18 Q44 18 45 20 L49 26Z'));
      ctx.restore();
      TEX.text(ctx, 'CAR', 640, 128, 120, '#ffffff', 'rgba(0,0,0,.18)', 10);
      TEX.text(ctx, 'WASH', 640, 248, 120, '#ffffff', 'rgba(0,0,0,.18)', 10);
    }, 'carwashsign');
  }

  function shop() {
    const g = new THREE.Group();
    const b = new Builder();
    const Wd = 12, D = 6, Ht = 3.9;
    b.rbox(Wd + 0.6, 0.3, D + 0.6, 0.08, 0xc9cdd5, { y: 0.15 });
    b.rbox(Wd, Ht, 0.4, 0.1, PAL.wall, { y: Ht / 2 + 0.3, z: -D / 2 + 0.2 });
    b.rbox(0.45, Ht, D, 0.1, PAL.wall, { x: -Wd / 2 + 0.22, y: Ht / 2 + 0.3 });
    b.rbox(0.45, Ht, D, 0.1, PAL.wall, { x: Wd / 2 - 0.22, y: Ht / 2 + 0.3 });
    b.rbox(4.2, Ht, 0.42, 0.1, PAL.wall, { x: Wd / 2 - 2.1, y: Ht / 2 + 0.3, z: D / 2 - 0.21 });
    b.rbox(0.5, Ht, 0.5, 0.1, PAL.wallShade, { x: 1.7, y: Ht / 2 + 0.3, z: D / 2 - 0.2 });
    b.rbox(0.5, Ht, 0.5, 0.1, PAL.wallShade, { x: -Wd / 2 + 0.25, y: Ht / 2 + 0.3, z: D / 2 - 0.2 });
    b.box(Wd - 0.6, 0.06, D - 0.6, 0xe9d7b7, { y: 0.32 });
    // shelves with bottles
    for (let s = 0; s < 3; s++) {
      b.rbox(3.6, 0.08, 0.5, 0.02, PAL.woodDark, { x: -2.6, y: 1.3 + s * 0.75, z: -D / 2 + 0.7 });
      for (let i = 0; i < 9; i++) {
        const c = U.pick([0xff6fb1, 0xffc93c, 0x3ad17a, 0x4f8cff, 0xff8a1f]);
        b.cyl(0.11, 0.11, 0.38, 8, c, { x: -4.2 + i * 0.4, y: 1.53 + s * 0.75, z: -D / 2 + 0.7 });
      }
    }
    b.rbox(0.12, 2.4, 0.5, 0.03, PAL.woodDark, { x: -4.45, y: 1.9, z: -D / 2 + 0.7 });
    b.rbox(0.12, 2.4, 0.5, 0.03, PAL.woodDark, { x: -0.75, y: 1.9, z: -D / 2 + 0.7 });
    // counter
    b.rbox(3.6, 1.05, 0.9, 0.1, 0xffffff, { x: -1.6, y: 0.85, z: 0.4 });
    b.rbox(3.7, 0.1, 1.0, 0.04, PAL.wood, { x: -1.6, y: 1.42, z: 0.4 });
    b.rbox(0.5, 0.36, 0.08, 0.03, 0x2a3b55, { x: -0.6, y: 1.7, z: 0.3 });
    b.box(0.1, 0.2, 0.1, 0x2a3b55, { x: -0.6, y: 1.5, z: 0.3 });
    // cash stack on a little table (like the reference)
    b.rbox(1.3, 0.1, 0.9, 0.03, PAL.wood, { x: 3.2, y: 0.95, z: 1.2 });
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) b.box(0.08, 0.62, 0.08, PAL.woodDark, { x: 3.2 + sx * 0.55, y: 0.62, z: 1.2 + sz * 0.35 });
    for (let i = 0; i < 6; i++) b.rbox(0.5, 0.12, 0.3, 0.03, PAL.cash, { x: 3.0 + (i % 2) * 0.52, y: 1.07 + Math.floor(i / 2) * 0.13, z: 1.2 });
    // roof
    b.rbox(Wd + 1.0, 0.45, D + 1.1, 0.14, PAL.roof, { y: Ht + 0.52 });
    b.rbox(Wd + 1.04, 0.62, 0.3, 0.1, PAL.blueDark, { y: Ht + 0.42, z: D / 2 + 0.45 });
    // door + window on the right part
    b.rbox(1.4, 2.5, 0.12, 0.06, PAL.blue, { x: 3.0, y: 1.55, z: D / 2 + 0.02 });
    b.rbox(0.9, 0.9, 0.05, 0.04, 0xbfe8ff, { x: 3.0, y: 2.1, z: D / 2 + 0.09 });
    b.sphere(0.07, 0xffd23f, { x: 3.5, y: 1.4, z: D / 2 + 0.12, ws: 8, hs: 6 });
    b.rbox(1.0, 1.4, 0.06, 0.05, PAL.blue, { x: 4.85, y: 2.2, z: D / 2 + 0.03 });
    // potted plant
    b.cyl(0.35, 0.28, 0.6, 10, 0xc9884f, { x: -5.4, y: 0.6, z: D / 2 + 0.6 });
    for (let i = 0; i < 6; i++) b.cone(0.12, 0.9, 4, 0x3fae3a, { x: -5.4 + Math.cos(i) * 0.15, y: 1.25, z: D / 2 + 0.6 + Math.sin(i) * 0.15, rz: Math.cos(i) * 0.4, rx: Math.sin(i) * 0.4 });
    const m = b.build(); g.add(m);
    // poster on the door-side wall
    const poster = new THREE.Mesh(GEO.plane(0.86, 1.25), new THREE.MeshLambertMaterial({ map: TEX.make(128, 186, (ctx, w, h) => { ctx.fillStyle = '#2563eb'; ctx.fillRect(0, 0, w, h); TEX.icon(ctx, 'car', 64, 70, 100); ctx.fillStyle = '#fff'; for (let i = 0; i < 3; i++) ctx.fillRect(22, 130 + i * 16, 84 - i * 18, 8); }, 'poster') }));
    poster.position.set(4.85, 2.2, D / 2 + 0.07); g.add(poster);
    // big CAR WASH sign
    const board = new THREE.Mesh(GEO.rbox(8.6, 2.9, 0.4, 0.4), MAT.lam(0x1d4fc4));
    board.position.set(-0.6, Ht + 2.3, D / 2 - 0.4); board.castShadow = true;
    const face = new THREE.Mesh(GEO.plane(8.3, 2.9), new THREE.MeshLambertMaterial({ map: carWashSignTex(), transparent: true }));
    face.position.set(-0.6, Ht + 2.3, D / 2 - 0.18);
    g.add(board, face);
    // decorative shopkeeper
    const keeper = person({ shirt: 0xffffff, vest: PAL.blue, skin: PAL.skin[1] });
    keeper.group.position.set(-1.8, 0.32, -0.4); keeper.group.name = 'Keeper'; g.add(keeper.group);
    return { group: g, keeper, footprint: [Wd + 0.6, D + 0.6] };
  }

  function smallBuilding({ w, d, h, wall, roof, label, icon, signBg }) {
    const g = new THREE.Group();
    const b = new Builder();
    b.rbox(w + 0.5, 0.25, d + 0.5, 0.06, 0xc9cdd5, { y: 0.12 });
    b.rbox(w, h, d, 0.14, wall, { y: h / 2 + 0.2 });
    b.rbox(w + 0.8, 0.36, d + 0.8, 0.12, roof, { y: h + 0.38 });
    b.rbox(w + 0.84, 0.18, d + 0.84, 0.06, new THREE.Color(roof).multiplyScalar(0.75).getHex(), { y: h + 0.15 });
    b.rbox(1.3, 2.2, 0.12, 0.05, PAL.blue, { y: 1.3, z: d / 2 + 0.03 });
    b.sphere(0.07, 0xffd23f, { x: 0.45, y: 1.25, z: d / 2 + 0.12, ws: 8, hs: 6 });
    for (const sx of [-1, 1]) {
      b.rbox(1.1, 0.9, 0.08, 0.05, 0xffffff, { x: sx * (w / 2 - 1.0), y: 1.8, z: d / 2 + 0.03 });
      b.box(0.9, 0.7, 0.06, 0x9fd2f5, { x: sx * (w / 2 - 1.0), y: 1.8, z: d / 2 + 0.06 });
    }
    g.add(b.build());
    const sign = signBoard(label, Math.min(w - 0.6, 3.6), 0.85, signBg || '#2563eb', '#ffffff', icon);
    sign.position.set(0, h + 1.05, d / 2 - 0.1);
    g.add(sign);
    return g;
  }

  function staffRoom() {
    const g = smallBuilding({ w: 6, d: 4.6, h: 3.0, wall: PAL.wall, roof: PAL.roof, label: 'STAFF', icon: 'staff' });
    const b = new Builder(); bench(b, 2.0, 2.9, 0); g.add(b.build());
    return g;
  }
  function office() {
    const g = smallBuilding({ w: 5, d: 4.4, h: 3.2, wall: 0xf4f6fa, roof: 0x1e9e6a, label: 'OFFICE', icon: 'office', signBg: '#1e9e6a' });
    return g;
  }
  function vipLounge() {
    const g = smallBuilding({ w: 6.6, d: 4.6, h: 3.4, wall: 0x6a2bd8, roof: 0xffc92e, label: 'VIP', icon: 'vip', signBg: '#7b2ff7' });
    const b = new Builder();
    b.box(1.6, 0.03, 3.2, 0xd72b3a, { y: 0.04, z: 3.9 });
    for (const sx of [-1, 1]) for (let i = 0; i < 3; i++) {
      b.cyl(0.07, 0.1, 1.0, 8, PAL.gold, { x: sx * 1.1, y: 0.5, z: 2.7 + i * 1.0 });
      b.sphere(0.12, PAL.gold, { x: sx * 1.1, y: 1.05, z: 2.7 + i * 1.0, ws: 8, hs: 6 });
    }
    for (const sx of [-1, 1]) for (let i = 0; i < 2; i++) b.cyl(0.04, 0.04, 1.0, 6, 0xd72b3a, { x: sx * 1.1, y: 0.92, z: 3.2 + i * 1.0, rx: Math.PI / 2, sy: 1, });
    g.add(b.build());
    return g;
  }
  function coffeeMachine() {
    const g = new THREE.Group();
    const b = new Builder();
    b.rbox(1.3, 2.1, 1.0, 0.14, 0x8a4b2a, { y: 1.05 });
    b.rbox(1.1, 0.8, 0.08, 0.06, 0x2a2e36, { y: 1.4, z: 0.5 });
    b.rbox(0.6, 0.1, 0.4, 0.03, 0x9aa3b1, { y: 0.55, z: 0.5 });
    b.cyl(0.13, 0.11, 0.24, 10, 0xffffff, { y: 0.72, z: 0.55 });
    b.cyl(0.5, 0.4, 0.55, 14, 0xffffff, { y: 2.55 });
    b.cyl(0.42, 0.42, 0.05, 14, 0x5a3418, { y: 2.81 });
    b.torus(0.2, 0.06, 0xffffff, { x: 0.52, y: 2.55, ts: 12 });
    g.add(b.build());
    return g;
  }
  function vending() {
    const g = new THREE.Group();
    const b = new Builder();
    b.rbox(1.3, 2.2, 0.9, 0.12, 0xe8383d, { y: 1.1 });
    b.rbox(0.82, 1.5, 0.06, 0.04, 0xbfe8ff, { x: -0.16, y: 1.3, z: 0.46 });
    for (let r = 0; r < 4; r++) for (let c = 0; c < 3; c++) b.box(0.18, 0.2, 0.06, U.pick([0xffc93c, 0x46c23a, 0x4f8cff, 0xff8a1f, 0xffffff]), { x: -0.42 + c * 0.26, y: 0.8 + r * 0.33, z: 0.5 });
    b.box(0.2, 0.42, 0.06, 0x2a3b55, { x: 0.43, y: 1.6, z: 0.46 });
    b.box(0.6, 0.18, 0.06, 0x1d2230, { x: -0.16, y: 0.36, z: 0.47 });
    b.rbox(1.34, 0.3, 0.94, 0.08, 0xffffff, { y: 2.2 });
    g.add(b.build());
    const sign = new THREE.Mesh(GEO.plane(1.2, 0.28), new THREE.MeshBasicMaterial({ map: TEX.sign('SNACKS', '#ffffff', '#e8383d', 256, 60), transparent: true }));
    sign.position.set(0, 2.2, 0.48); g.add(sign);
    return g;
  }
  function tubeMan(color = 0xff4d5a) {
    const g = new THREE.Group();
    const b = new Builder();
    b.rbox(1.0, 0.7, 1.0, 0.14, PAL.blue, { y: 0.35 });
    b.cyl(0.42, 0.42, 0.08, 14, 0x3a3f47, { y: 0.72 });
    g.add(b.build());
    const mat = MAT.lam(color);
    const segs = [];
    let parent = g;
    for (let i = 0; i < 6; i++) {
      const s = new THREE.Group(); s.position.y = i === 0 ? 0.75 : 0.62;
      const m = new THREE.Mesh(GEO.cyl(0.3, 0.33, 0.66, 10), mat); m.position.y = 0.33; m.castShadow = true;
      s.add(m); parent.add(s); s.name = 'Seg' + segs.length; segs.push(s); parent = s;
    }
    // face on the top segment
    const top = segs[5];
    const head = new THREE.Mesh(GEO.sphere(0.32, 12, 8), mat); head.position.y = 0.68; top.add(head);
    const fb = new Builder();
    fb.sphere(0.11, 0xffffff, { x: -0.12, y: 0.78, z: 0.25, ws: 8, hs: 6 }); fb.sphere(0.11, 0xffffff, { x: 0.12, y: 0.78, z: 0.25, ws: 8, hs: 6 });
    fb.sphere(0.05, 0x1b2333, { x: -0.1, y: 0.78, z: 0.34, ws: 6, hs: 4 }); fb.sphere(0.05, 0x1b2333, { x: 0.14, y: 0.78, z: 0.34, ws: 6, hs: 4 });
    fb.sphere(0.1, 0x8a1d26, { y: 0.56, z: 0.27, sy: 0.7, ws: 8, hs: 6 });
    for (let i = 0; i < 7; i++) fb.cone(0.07, 0.3, 4, 0xffd23f, { x: Math.cos(i) * 0.18, y: 1.05, z: Math.sin(i) * 0.18, rz: Math.cos(i) * 0.5, rx: -Math.sin(i) * 0.5 });
    top.add(fb.build());
    const arms = [];
    for (const side of [-1, 1]) {
      let p = segs[3]; const chain = [];
      for (let i = 0; i < 3; i++) {
        const a = new THREE.Group();
        a.position.set(i === 0 ? side * 0.28 : side * 0.5, i === 0 ? 0.45 : 0, 0);
        const m = new THREE.Mesh(GEO.cyl(0.13, 0.15, 0.55, 8), mat); m.rotation.z = Math.PI / 2; m.position.x = side * 0.26;
        a.add(m); p.add(a); a.name = (side < 0 ? 'ArmL' : 'ArmR') + chain.length; chain.push(a); p = a;
      }
      arms.push({ side, chain });
    }
    const update = (t) => {
      for (let i = 0; i < segs.length; i++) {
        segs[i].rotation.z = Math.sin(t * 5.5 + i * 0.9) * (0.18 + i * 0.05) + Math.sin(t * 2.1) * 0.08;
        segs[i].rotation.x = Math.sin(t * 4.3 + i * 1.3) * 0.12;
      }
      for (const a of arms) a.chain.forEach((c, i) => { c.rotation.z = a.side * (Math.sin(t * 7 + i + (a.side > 0 ? 1.5 : 0)) * 0.9 + 0.2); });
    };
    return { group: g, update };
  }
  function fountain() {
    const g = new THREE.Group();
    const b = new Builder();
    b.cyl(2.3, 2.45, 0.6, 24, 0xe3e7ee, { y: 0.3 });
    b.cyl(2.0, 2.0, 0.62, 24, 0xc9cfd9, { y: 0.32 });
    b.cyl(0.42, 0.55, 1.4, 12, 0xe3e7ee, { y: 0.9 });
    b.cyl(1.0, 0.7, 0.3, 16, 0xe3e7ee, { y: 1.65 });
    b.cyl(0.22, 0.3, 0.9, 10, 0xe3e7ee, { y: 2.2 });
    b.sphere(0.32, 0x4fc3ff, { y: 2.75, ws: 12, hs: 8 });
    g.add(b.build());
    const water = new THREE.Mesh(GEO.circle(1.95, 24), new THREE.MeshLambertMaterial({ color: 0x4fc3ff, transparent: true, opacity: 0.85 }));
    water.rotation.x = -Math.PI / 2; water.position.y = 0.6; water.name = 'Water'; g.add(water);
    const jet = new THREE.Object3D(); jet.name = 'Jet'; jet.position.y = 2.9; g.add(jet);
    const top = new THREE.Mesh(GEO.circle(0.92, 16), new THREE.MeshLambertMaterial({ color: 0x4fc3ff }));
    top.rotation.x = -Math.PI / 2; top.position.y = 1.81; g.add(top);
    return { group: g, jetY: 2.9 };
  }
  function billboard(locName) {
    const g = new THREE.Group();
    const b = new Builder();
    for (const sx of [-1, 1]) b.cyl(0.14, 0.16, 4.6, 8, 0x7d8796, { x: sx * 2.4, y: 2.3 });
    b.rbox(7.0, 3.3, 0.3, 0.12, 0x2a2e36, { y: 5.3 });
    b.box(6.4, 0.1, 0.6, 0x7d8796, { y: 3.55, z: 0.3 });
    g.add(b.build());
    const tex = TEX.make(1024, 470, (ctx, w, h) => {
      const grd = ctx.createLinearGradient(0, 0, 0, h); grd.addColorStop(0, '#59c2ff'); grd.addColorStop(1, '#2563eb');
      ctx.fillStyle = grd; ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = 'rgba(255,255,255,.85)';
      for (let i = 0; i < 18; i++) { ctx.beginPath(); ctx.arc(Math.random() * w, Math.random() * h, 8 + Math.random() * 28, 0, Math.PI * 2); ctx.fill(); }
      TEX.icon(ctx, 'car', 200, 260, 300);
      TEX.text(ctx, 'DIRTY CAR?', 640, 150, 104, '#ffd23f', '#14326e', 16);
      TEX.text(ctx, "WE DON'T JUDGE.", 640, 270, 76, '#ffffff', '#14326e', 14);
      TEX.text(ctx, 'SUDS TYCOON CAR WASH', 640, 380, 44, '#ffffff', '#14326e', 10);
    }, 'billboard');
    const face = new THREE.Mesh(GEO.plane(6.6, 3.0), new THREE.MeshLambertMaterial({ map: tex }));
    face.position.set(0, 5.3, 0.16); g.add(face);
    return g;
  }
  function flowerBed(len = 3.4) {
    const b = new Builder();
    b.rbox(len, 0.5, 1.2, 0.12, 0xc9884f, { y: 0.25 });
    b.box(len - 0.2, 0.06, 1.0, 0x6b4a2b, { y: 0.5 });
    const n = Math.round(len * 3);
    for (let i = 0; i < n; i++) {
      const x = -len / 2 + 0.25 + (i / (n - 1)) * (len - 0.5), z = U.rand(-0.32, 0.32);
      b.cyl(0.03, 0.03, 0.4, 4, 0x3cb52f, { x, y: 0.7, z });
      b.ico(0.14, U.pick([0xff4d5a, 0xffd23f, 0xff6fb1, 0xffffff, 0xb44cff, 0xff8a1f]), { x, y: 0.94, z });
      b.ico(0.12, 0x4ea935, { x: x + 0.1, y: 0.62, z: z - 0.1, sy: 0.6 });
    }
    return b.build();
  }
  function waitingArea() {
    const g = new THREE.Group();
    const b = new Builder();
    bench(b, -1.4, 0, 0); bench(b, 1.4, 0, 0);
    b.cyl(0.06, 0.06, 2.6, 8, 0x7d8796, { x: 0, y: 1.3, z: -0.4 });
    b.cone(1.8, 0.7, 10, 0xff4d5a, { x: 0, y: 2.8, z: -0.4 });
    b.cone(1.82, 0.7, 10, 0xffffff, { x: 0, y: 2.79, z: -0.4, ry: Math.PI / 10, sy: 0.98 });
    g.add(b.build());
    const s = signBoard('WAIT HERE', 2.2, 0.5, '#ff8a1f', '#ffffff', 'clock', 512);
    s.position.set(2.9, 1.7, -0.2); g.add(s);
    const pb = new Builder(); pb.cyl(0.06, 0.06, 1.5, 6, 0x7d8796, { x: 2.9, y: 0.72, z: -0.32 }); g.add(pb.build());
    return g;
  }

  // ---------- landmarks ----------
  function landmark(kind) {
    const g = new THREE.Group();
    const b = new Builder();
    if (kind === 'goldcar') {
      b.cyl(2.2, 2.4, 0.6, 20, 0xe3e7ee, { y: 0.3 });
      b.cyl(1.7, 1.9, 0.7, 20, 0xd5dbe5, { y: 0.95 });
      g.add(b.build());
      const c = car('golden', PAL.gold); c.group.position.y = 1.3; c.group.rotation.y = 0.4; c.extras.washMe.visible = false;
      g.add(c.group);
    } else if (kind === 'sandcastle') {
      const s = 0xe9cf8a, s2 = 0xd9b96a;
      b.cyl(2.6, 2.8, 0.5, 20, s2, { y: 0.25 });
      b.box(3.2, 1.6, 3.2, s, { y: 1.3 });
      for (const sx of [-1, 1]) for (const sz of [-1, 1]) { b.cyl(0.6, 0.7, 2.8, 12, s, { x: sx * 1.6, y: 1.9, z: sz * 1.6 }); b.cone(0.75, 1.0, 12, s2, { x: sx * 1.6, y: 3.8, z: sz * 1.6 }); }
      b.cyl(0.8, 0.9, 1.8, 12, s, { y: 3.0 }); b.cone(1.0, 1.4, 12, s2, { y: 4.6 });
      b.cyl(0.05, 0.05, 1.2, 4, 0x7d8796, { y: 5.6 }); b.box(0.7, 0.4, 0.04, 0xff4d5a, { x: 0.35, y: 5.95 });
      b.box(0.8, 1.0, 0.1, 0x8a5a33, { y: 1.0, z: 1.62 });
      g.add(b.build());
    } else if (kind === 'oasis') {
      b.cyl(3.0, 3.2, 0.3, 20, 0xd9b96a, { y: 0.15 });
      for (let i = 0; i < 8; i++) rock(b, Math.cos(i * 0.8) * 2.8, Math.sin(i * 0.8) * 2.8, U.rand(0.6, 1.0), 0xb38a5a);
      palm(b, -1.6, -1.2, 1.2); palm(b, 1.8, -1.0, 1.0); palm(b, 0.2, 1.8, 0.9);
      g.add(b.build());
      const w = new THREE.Mesh(GEO.circle(2.4, 24), new THREE.MeshLambertMaterial({ color: 0x3fb7ff }));
      w.rotation.x = -Math.PI / 2; w.position.y = 0.32; g.add(w);
    } else if (kind === 'snowman') {
      b.cyl(2.2, 2.4, 0.4, 20, 0xe3e7ee, { y: 0.2 });
      snowman(b, 0, 0, 1.9);
      g.add(b.build());
    } else if (kind === 'neontower') {
      b.cyl(1.6, 1.9, 0.6, 16, 0x2a2e36, { y: 0.3 });
      b.cyl(0.4, 0.6, 7.5, 10, 0x3a3f4a, { y: 4.3 });
      g.add(b.build());
      const cols = [0xff3df2, 0x3df2ff, 0xfff23d, 0x7dff6a];
      for (let i = 0; i < 4; i++) {
        const r = new THREE.Mesh(GEO.torus(1.1 - i * 0.15, 0.09, 8, 28), new THREE.MeshBasicMaterial({ color: cols[i] }));
        r.rotation.x = Math.PI / 2; r.position.y = 2 + i * 1.6; g.add(r);
      }
      const orb = new THREE.Mesh(GEO.sphere(1.0, 20, 14), new THREE.MeshBasicMaterial({ color: 0xbfe8ff, transparent: true, opacity: 0.85 }));
      orb.position.y = 8.6; g.add(orb);
    } else if (kind === 'rocket') {
      b.cyl(2.4, 2.6, 0.5, 20, 0x8b95a5, { y: 0.25 });
      b.cyl(0.9, 1.0, 5.0, 16, 0xffffff, { y: 3.0 });
      b.cone(0.9, 1.8, 16, 0xe8383d, { y: 6.4 });
      b.cyl(0.35, 0.35, 0.1, 14, 0x9fd2f5, { y: 4.2, z: 0.88, rx: Math.PI / 2 });
      for (let i = 0; i < 4; i++) { const a = i * Math.PI / 2; b.box(0.12, 1.4, 0.9, 0xe8383d, { x: Math.cos(a) * 1.05, y: 1.2, z: Math.sin(a) * 1.05, ry: -a }); }
      b.cone(0.6, 0.6, 12, 0x3a3f47, { y: 0.4, rx: Math.PI });
      g.add(b.build());
    }
    return g;
  }

  // ---------- pad (build spot) ----------
  function padTexture(def, remaining, total) {
    const key = null;
    return TEX.make(256, 256, (ctx, w, h) => drawPad(ctx, w, h, def, remaining, total), key);
  }
  function drawPad(ctx, w, h, def, remaining, total) {
    ctx.clearRect(0, 0, w, h);
    TEX.rr(ctx, 10, 10, w - 20, h - 20, 34);
    ctx.fillStyle = 'rgba(20,40,80,0.42)'; ctx.fill();
    const prog = total > 0 ? 1 - remaining / total : 0;
    if (prog > 0) {
      ctx.save(); TEX.rr(ctx, 10, 10, w - 20, h - 20, 34); ctx.clip();
      ctx.fillStyle = 'rgba(76,193,58,0.75)'; ctx.fillRect(10, h - 10 - (h - 20) * prog, w - 20, (h - 20) * prog);
      ctx.restore();
    }
    ctx.lineWidth = 9; ctx.setLineDash([26, 16]); ctx.strokeStyle = '#ffffff';
    TEX.rr(ctx, 14, 14, w - 28, h - 28, 30); ctx.stroke(); ctx.setLineDash([]);
    TEX.icon(ctx, def.icon || 'star', w / 2, 92, 104);
    const label = U.fmt(Math.ceil(remaining));
    TEX.icon(ctx, 'cash', w / 2 - 54 - Math.min(40, label.length * 7), 196, 52);
    TEX.text(ctx, label, w / 2 + 18, 198, TEX.fit(ctx, label, 150, 58), '#ffffff', 'rgba(10,25,50,.7)', 10);
  }

  return {
    tree, palm, cactus, pine, bush, rock, lamp, trafficCone, trashCan, bench, fenceSeg, snowman, tallBuilding,
    person, hatMesh, item, itemGeometry, cashBundleGeo, coinGeometry, car, wheelGeo,
    stationFloor, laneTrack, gate, machine, hopper, payBooth, cashTable, rack, pipe, iconTex,
    shop, staffRoom, office, vipLounge, coffeeMachine, vending, tubeMan, fountain, billboard, flowerBed, waitingArea, landmark,
    signBoard, drawPad, padTexture,
  };
})();
