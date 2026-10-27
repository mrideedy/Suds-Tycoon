// world.js — the static environment of each location: ground, lot, roads, water, scenery, weather.
const World = (() => {
  const THEMES = {
    suburb: {
      sky: 0x9fd9ff, fog: 0xc4e9ff, ground: 0x7cc850, patch: 0x8ad75b, lot: 0xc8ccd4, road: 0x4d525b, sidewalk: 0xbfc4cc,
      water: 0x4aa8e8, shore: 0xe9dcb0, hemi: 0.6, sun: 0.52, flora: 'oak',
    },
    beach: {
      sky: 0x86d8ff, fog: 0xbfeaff, ground: 0xf1dc9f, patch: 0xe8cf8a, lot: 0xc9ccd2, road: 0x55595f, sidewalk: 0xd5d0c4,
      water: 0x22c3e6, shore: 0xfff2c9, hemi: 0.62, sun: 0.52, flora: 'palm',
    },
    desert: {
      sky: 0xa8d8ff, fog: 0xf3d9b0, ground: 0xe6b273, patch: 0xd9a163, lot: 0xd2c9bc, road: 0x5a5550, sidewalk: 0xd8c3a2,
      water: null, shore: null, hemi: 0.66, sun: 0.66, sunColor: 0xffe6c4, hemiGround: 0xc49a6a, flora: 'cactus',
    },
    snow: {
      sky: 0xcfe7ff, fog: 0xe6f2ff, ground: 0xf3f8ff, patch: 0xe2ecf8, lot: 0xc4cad3, road: 0x50555e, sidewalk: 0xe1e6ee,
      water: 0xa9dcf2, shore: 0xffffff, hemi: 0.6, sun: 0.45, hemiGround: 0xb9c7da, flora: 'pine', snow: true,
    },
    city: {
      sky: 0x161c38, fog: 0x1d2446, ground: 0x343a45, patch: 0x3c4350, lot: 0xb7bbc4, road: 0x2b2f36, sidewalk: 0x5a606b,
      water: 0x1e3a6b, shore: 0x4a505c, hemi: 0.52, sun: 0.28, sunColor: 0xa9c4ff, hemiSky: 0x9fb5ff, hemiGround: 0x4b4060, amb: 0.18, flora: 'city', night: true,
    },
    moon: {
      sky: 0x05060f, fog: 0x070914, ground: 0x9fa2aa, patch: 0x8d9099, lot: 0xbfc3ca, road: 0x5a5e66, sidewalk: 0xb5b8bf,
      water: null, shore: null, hemi: 0.5, sun: 0.75, sunColor: 0xffffff, hemiSky: 0xc9d4ff, hemiGround: 0x55586a, amb: 0.12, flora: 'moon', stars: true,
    },
  };

  let group = null, theme = null, waterTex = null, snowPts = null, tumble = [], lane2Bits = null, stars = null;
  const lb = () => CFG.lotBounds;

  function build(themeId) {
    clear();
    theme = THEMES[themeId] || THEMES.suburb;
    GFX.setTheme(theme);
    group = new THREE.Group();
    GFX.scene.add(group);
    const L = lb();

    // --- ground ---
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(520, 520), MAT.lam(theme.ground));
    ground.rotation.x = -Math.PI / 2; ground.receiveShadow = true; group.add(ground);
    const pb = new Builder();
    for (let i = 0; i < 70; i++) {
      const x = U.rand(-110, 110), z = U.rand(-30, 110);
      if (inLot(x, z, 4)) continue;
      pb.add(GEO.circle(1, 10), theme.patch, { x, y: 0.005, z, rx: -Math.PI / 2, sx: U.rand(2, 7), sy: U.rand(1.5, 5), sz: 1 });
    }
    const patches = pb.build(MAT.vc, false); patches.receiveShadow = true; group.add(patches);

    // --- lot slab, curbs ---
    const sb = new Builder();
    const lotTex = TEX.make(512, 512, (ctx, w2, h2) => {
      const base = new THREE.Color(theme.lot);
      ctx.fillStyle = '#' + base.getHexString(); ctx.fillRect(0, 0, w2, h2);
      for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) {
        const c = base.clone().offsetHSL(0, 0, U.rand(-0.025, 0.02));
        ctx.fillStyle = '#' + c.getHexString(); ctx.fillRect(i * 128 + 2, j * 128 + 2, 124, 124);
      }
      ctx.fillStyle = 'rgba(0,0,0,0.035)';
      for (let k = 0; k < 900; k++) ctx.fillRect(Math.random() * w2, Math.random() * h2, 2, 2);
      ctx.fillStyle = 'rgba(40,45,55,0.16)';
      for (let i = 0; i <= 4; i++) { ctx.fillRect(i * 128 - 2, 0, 4, h2); ctx.fillRect(0, i * 128 - 2, w2, 4); }
    }, 'lot|' + theme.lot);
    lotTex.wrapS = lotTex.wrapT = THREE.RepeatWrapping;
    lotTex.repeat.set((L.x1 - L.x0) / 12, (L.z1 - L.z0) / 12);
    const lotMesh = new THREE.Mesh(new THREE.PlaneGeometry(L.x1 - L.x0, L.z1 - L.z0), new THREE.MeshLambertMaterial({ map: lotTex }));
    lotMesh.rotation.x = -Math.PI / 2; lotMesh.position.set((L.x0 + L.x1) / 2, 0.02, (L.z0 + L.z1) / 2); lotMesh.receiveShadow = true;
    group.add(lotMesh);
    // painted parking-style lines along the lot edges
    for (let x = L.x0 + 3; x < L.x1 - 2; x += 3.2) sb.box(0.12, 0.012, 1.6, 0xffffff, { x, y: 0.045, z: L.z1 - 1.1 });
    const gaps = CFG.laneZ.map(z => [z - 2.3, z + 2.3]);
    const curbCol = theme.night ? 0x8d929c : 0xf2f3f6;
    const fence = (x0, z0, x1, z1) => Models.fenceSeg(sb, x0, z0, x1, z1, 0.5, curbCol);
    fence(L.x0, L.z0, L.x1, L.z0);
    fence(L.x0, L.z1, L.x1, L.z1);
    for (const x of [L.x0, L.x1]) {
      let z = L.z0;
      for (const [a, b] of gaps) { fence(x, z, x, a); z = b; }
      fence(x, z, x, L.z1);
    }

    // --- lane roads (west queue + east exit) ---
    for (let li = 0; li < CFG.laneZ.length; li++) {
      const z = CFG.laneZ[li];
      roadStrip(sb, -100, L.x0 + 0.2, z, 4.4, theme.road);
      roadStrip(sb, L.x1 - 0.2, 100, z, 4.4, theme.road);
      // inside the lot: queue apron before the entrance and the exit apron
      sb.box(CFG.entranceX - L.x0, 0.02, 4.4, theme.road, { x: (L.x0 + CFG.entranceX) / 2, y: 0.03, z });
      sb.box(L.x1 - CFG.exitX, 0.02, 4.4, theme.road, { x: (CFG.exitX + L.x1) / 2, y: 0.03, z });
      for (let x = -98; x < 98; x += 6) if (x < L.x0 - 2 || x > L.x1 + 2) sb.box(2.6, 0.012, 0.16, 0xffd23f, { x, y: 0.034, z });
      // painted arrows on the aprons
      arrowDecal(sb, CFG.entranceX - 4.5, z, 0);
      arrowDecal(sb, CFG.exitX + 2.4, z, 0);
    }
    Models.laneTrack(sb, CFG.laneZ[0], CFG.entranceX - 0.6, CFG.exitX + 0.6);

    // --- main road behind the buildings ---
    const mz = -18.6;
    roadStrip(sb, -120, 120, mz, 7, theme.road);
    for (let x = -118; x < 118; x += 5) sb.box(2.4, 0.012, 0.18, 0xffffff, { x, y: 0.034, z: mz });
    sb.box(240, 0.12, 1.5, theme.sidewalk, { x: 0, y: 0.06, z: mz + 4.2 });
    sb.box(240, 0.12, 1.5, theme.sidewalk, { x: 0, y: 0.06, z: mz - 4.2 });
    for (let x = -100; x <= 100; x += 13) Models.lamp(sb, x, mz + 4.6, Math.PI / 2, theme.night);
    if (theme.water != null) {
      sb.box(240, 0.1, 2.4, theme.shore, { x: 0, y: 0.03, z: mz - 6.2 });
      const wt = waterTex = TEX.make(256, 256, (ctx, w, h) => {
        ctx.fillStyle = '#' + new THREE.Color(theme.water).getHexString(); ctx.fillRect(0, 0, w, h);
        ctx.strokeStyle = 'rgba(255,255,255,.28)'; ctx.lineWidth = 3;
        for (let i = 0; i < 22; i++) { const x = Math.random() * w, y = Math.random() * h, l = 14 + Math.random() * 30; ctx.beginPath(); ctx.moveTo(x, y); ctx.quadraticCurveTo(x + l / 2, y - 4, x + l, y); ctx.stroke(); }
      });
      wt.wrapS = wt.wrapT = THREE.RepeatWrapping; wt.repeat.set(20, 6);
      const water = new THREE.Mesh(new THREE.PlaneGeometry(260, 70), new THREE.MeshLambertMaterial({ map: wt }));
      water.rotation.x = -Math.PI / 2; water.position.set(0, -0.02, mz - 7.4 - 35); water.receiveShadow = true;
      group.add(water);
    }

    // --- scenery ---
    const tb = new Builder();
    scatterFlora(tb, sb);
    // lot-edge lamps and props
    for (const [x, z] of [[L.x0 + 1, -11.5], [L.x1 - 1, -11.5], [L.x0 + 1, 22], [L.x1 - 1, 22], [-8, 22.6], [8, 22.6]]) Models.lamp(sb, x, z, x < 0 ? 0 : Math.PI, theme.night);
    Models.trafficCone(sb, CFG.exitX + 3.5, 3.1); Models.trafficCone(sb, CFG.exitX + 3.5, -3.1);
    Models.trashCan(sb, -23.2, 22.2); Models.trashCan(sb, 26, -12);
    const scen = tb.build(); scen.castShadow = true; group.add(scen);
    const stat = sb.build(MAT.vc, true); stat.receiveShadow = true; group.add(stat);

    if (theme.night) addNightGlows();
    if (theme.snow) addSnow();
    if (theme.stars) addStars();
    buildLane2Construction();
    return group;
  }

  function inLot(x, z, m = 0) { const L = lb(); return x > L.x0 - m && x < L.x1 + m && z > L.z0 - m && z < L.z1 + m; }
  function onRoad(x, z, m = 0) {
    if (Math.abs(z + 18.6) < 7.2 + m) return true;
    for (const lz of CFG.laneZ) if (Math.abs(z - lz) < 3 + m) return true;
    return false;
  }
  function roadStrip(b, x0, x1, z, w, col) {
    b.box(x1 - x0, 0.02, w, col, { x: (x0 + x1) / 2, y: 0.02, z });
    b.box(x1 - x0, 0.022, 0.14, 0xffffff, { x: (x0 + x1) / 2, y: 0.025, z: z - w / 2 + 0.3 });
    b.box(x1 - x0, 0.022, 0.14, 0xffffff, { x: (x0 + x1) / 2, y: 0.025, z: z + w / 2 - 0.3 });
  }
  function arrowDecal(b, x, z, ry) {
    b.box(1.8, 0.012, 0.4, 0xffffff, { x: x - 0.4, y: 0.045, z });
    b.add(new THREE.CircleGeometry(0.75, 3), 0xffffff, { x: x + 0.7, y: 0.046, z, rx: -Math.PI / 2, rz: 0 });
  }

  function scatterFlora(tb, sb) {
    const L = lb();
    const spots = [];
    const tryPlace = (x, z, r) => {
      if (inLot(x, z, 2.5) || onRoad(x, z, r)) return false;
      if (z < -26 && theme.water != null) return false;
      for (const s of spots) if (U.dist2(s[0], s[1], x, z) < (r + s[2]) * (r + s[2])) return false;
      spots.push([x, z, r]); return true;
    };
    const fl = theme.flora;
    const N = fl === 'city' ? 40 : 170;
    for (let i = 0; i < N * 3 && spots.length < N; i++) {
      const x = U.rand(-90, 95), z = U.rand(-24, 70);
      const near = Math.abs(x) < 50 && z < 40;
      if (!near && Math.random() < 0.5) continue;
      const s = U.rand(0.85, 1.35);
      if (!tryPlace(x, z, 1.8 * s)) continue;
      if (fl === 'oak') { Math.random() < 0.78 ? Models.tree(tb, x, z, s) : Models.bush(tb, x, z, s * 1.2); }
      else if (fl === 'palm') { Math.random() < 0.6 ? Models.palm(tb, x, z, s) : beachProp(tb, x, z); }
      else if (fl === 'cactus') { const r = Math.random(); r < 0.55 ? Models.cactus(tb, x, z, s) : r < 0.85 ? Models.rock(tb, x, z, s * 1.6, U.pick([0xc0703f, 0xb35f34, 0xd08a55])) : mesa(tb, x, z, s); }
      else if (fl === 'pine') { Math.random() < 0.82 ? Models.pine(tb, x, z, s) : Math.random() < 0.5 ? Models.snowman(tb, x, z, s * 0.8) : Models.rock(tb, x, z, s * 1.3, 0xd9e2ee); }
      else if (fl === 'moon') { const r = Math.random(); r < 0.45 ? crater(tb, x, z, s * 2) : r < 0.75 ? Models.rock(tb, x, z, s * 1.4, 0x7d8089) : moonProp(tb, x, z); }
      else if (fl === 'city') { /* handled below */ }
    }
    if (fl === 'city') cityBlocks(tb);
    // a few bushes hugging the lot curb so it reads like the reference
    if (fl === 'oak' || fl === 'palm') {
      for (let x = L.x0 + 2; x < L.x1 - 1; x += 4.2) { if (Math.random() < 0.7) Models.bush(tb, x, L.z1 + 1.4, U.rand(0.8, 1.1)); }
    }
    // tumbleweeds
    tumble = [];
    if (fl === 'cactus') {
      for (let i = 0; i < 4; i++) {
        const m = new THREE.Mesh(GEO.ico(0.55, 1), new THREE.MeshLambertMaterial({ color: 0xb08a55, wireframe: true }));
        m.position.set(U.rand(-60, 60), 0.55, U.rand(26, 40)); group.add(m); tumble.push({ m, v: U.rand(2, 4) });
      }
    }
  }
  function beachProp(b, x, z) {
    const r = Math.random();
    if (r < 0.5) {
      b.cyl(0.05, 0.05, 2.4, 6, 0xffffff, { x, y: 1.2, z });
      const c = U.pick([0xff4d5a, 0x2f8cff, 0xffd23f, 0x46c23a]);
      b.cone(1.5, 0.6, 8, c, { x, y: 2.45, z, flat: true });
      b.box(1.0, 0.04, 1.8, U.pick([0xffffff, 0x6fd3ff, 0xff9ec8]), { x: x + 0.9, y: 0.03, z: z + 0.6, ry: 0.3 });
    } else if (r < 0.75) {
      b.rbox(0.5, 2.2, 0.12, 0.2, U.pick([0xff8a1f, 0x2fc6c6, 0xff6fb1]), { x, y: 1.0, z, rz: 0.2, ry: Math.random() });
    } else {
      b.sphere(0.4, 0xffffff, { x, y: 0.4, z }); b.sphere(0.405, 0xff4d5a, { x, y: 0.4, z, sx: 0.4 });
    }
  }
  function mesa(b, x, z, s) {
    b.cyl(2.2 * s, 2.8 * s, 3.2 * s, 7, 0xc0703f, { x, y: 1.6 * s, z, flat: true });
    b.cyl(2.25 * s, 2.25 * s, 0.4 * s, 7, 0xd08a55, { x, y: 3.25 * s, z, flat: true });
  }
  function crater(b, x, z, s) {
    b.torus(1.1 * s, 0.28 * s, 0x8a8d95, { x, y: 0.04, z, rx: -Math.PI / 2, sz: 0.5, rs: 6, ts: 14, flat: true });
    b.add(GEO.circle(1.0 * s, 14), 0x7d8089, { x, y: 0.02, z, rx: -Math.PI / 2 });
  }
  function moonProp(b, x, z) {
    if (Math.random() < 0.5) {
      b.sphere(1.6, 0xeef1f6, { x, y: 0, z, ws: 14, hs: 8 });
      b.box(0.8, 0.9, 0.5, 0xbfc5d0, { x, y: 0.45, z: z + 1.5 });
    } else {
      b.cyl(0.06, 0.06, 2.6, 6, 0xd9dde4, { x, y: 1.3, z });
      b.cone(0.7, 0.35, 12, 0xeef1f6, { x, y: 2.7, z, rx: Math.PI * 0.8 });
    }
  }
  function cityBlocks(tb) {
    const L = lb();
    const cols = [0x4a5168, 0x3d4459, 0x55607a, 0x46405e, 0x384a5e];
    for (let x = -90; x < 95; x += U.rand(7, 11)) {
      const h = U.rand(8, 26), w = U.rand(5, 9), d = U.rand(5, 8);
      Models.tallBuilding(tb, x, -30 - d / 2 - U.rand(0, 6), w, d, h, U.pick(cols), true);
      if (x < L.x0 - 8 || x > L.x1 + 8) Models.tallBuilding(tb, x, U.rand(30, 45), w, d, h * 0.8, U.pick(cols), true);
    }
    for (let z = -6; z < 40; z += U.rand(8, 12)) {
      for (const x of [-48, 52]) Models.tallBuilding(tb, x + U.rand(-4, 4), z, U.rand(6, 9), U.rand(6, 9), U.rand(8, 22), U.pick(cols), true);
    }
    for (let x = L.x0 - 6; x > -95; x -= 9) for (const z of [5.5, 7.5]) if (Math.random() < 0.6) { tb.rbox(1.3, 0.6, 1.3, 0.1, 0x6f7783, { x, y: 0.3, z }); Models.tree(tb, x, z, 0.6, 0x3c9a4a); }
  }
  function addNightGlows() {
    const mat = new THREE.MeshBasicMaterial({ map: TEX.glow(), transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, color: 0xffd99a, opacity: 0.55 });
    const L = lb();
    const pts = [];
    for (let x = -100; x <= 100; x += 13) pts.push([x + 0.95 * 0, -18.6 + 4.6]);
    pts.push([L.x0 + 1, -11.5], [L.x1 - 1, -11.5], [L.x0 + 1, 22], [L.x1 - 1, 22], [-8, 22.6], [8, 22.6]);
    for (const [x, z] of pts) {
      const m = new THREE.Mesh(GEO.plane(9, 9), mat); m.rotation.x = -Math.PI / 2; m.position.set(x, 0.06, z); group.add(m);
    }
    // neon strips along the lot edge
    const neon = [0xff3df2, 0x3df2ff, 0xfff23d];
    for (let i = 0; i < 3; i++) {
      const s = new THREE.Mesh(GEO.box(L.x1 - L.x0, 0.1, 0.1), new THREE.MeshBasicMaterial({ color: neon[i] }));
      s.position.set((L.x0 + L.x1) / 2, 0.62 + i * 0.001, L.z1 + 0.2 + i * 0.12); group.add(s);
    }
  }
  function addSnow() {
    const n = 1400, pos = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) { pos[i * 3] = U.rand(-45, 45); pos[i * 3 + 1] = U.rand(0, 30); pos[i * 3 + 2] = U.rand(-35, 45); }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    snowPts = new THREE.Points(g, new THREE.PointsMaterial({ color: 0xffffff, size: 0.28, transparent: true, opacity: 0.9, depthWrite: false }));
    snowPts.frustumCulled = false;
    group.add(snowPts);
  }
  function addStars() {
    const n = 900, pos = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, el = U.rand(0.02, 1.2), r = 300;
      pos[i * 3] = Math.cos(a) * Math.cos(el) * r; pos[i * 3 + 1] = Math.sin(el) * r; pos[i * 3 + 2] = Math.sin(a) * Math.cos(el) * r;
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    stars = new THREE.Points(g, new THREE.PointsMaterial({ color: 0xffffff, size: 1.6, sizeAttenuation: false, fog: false }));
    group.add(stars);
    const earth = new THREE.Mesh(GEO.sphere(14, 24, 16), new THREE.MeshBasicMaterial({ color: 0x3f8cff, fog: false }));
    earth.position.set(-120, 90, -220); group.add(earth);
    const land = new THREE.Mesh(GEO.sphere(14.2, 10, 8), new THREE.MeshBasicMaterial({ color: 0x46c23a, fog: false, wireframe: false, transparent: true, opacity: 0.55 }));
    land.position.copy(earth.position); land.scale.set(1, 0.7, 0.6); group.add(land);
  }

  // Lane 2 sits behind cones and a sign until it is bought.
  function buildLane2Construction() {
    lane2Bits = new THREE.Group();
    const b = new Builder();
    const z = CFG.laneZ[1];
    for (let i = 0; i < 9; i++) Models.trafficCone(b, -14 + i * 4.2, z + (i % 2 ? 1.2 : -1.2), 1.1);
    for (const x of [lb().x0 + 1.2, lb().x1 - 1.2]) {
      b.box(0.2, 1.0, 0.2, 0xffffff, { x, y: 0.5, z: z - 1.9 }); b.box(0.2, 1.0, 0.2, 0xffffff, { x, y: 0.5, z: z + 1.9 });
      for (let i = 0; i < 6; i++) b.box(0.22, 0.32, 0.66, i % 2 ? 0xffffff : 0xe8383d, { x, y: 0.9, z: z - 1.65 + i * 0.66 });
    }
    lane2Bits.add(b.build());
    const s = Models.signBoard('LANE 2 SOON', 4.4, 1.0, '#ff8a1f', '#ffffff', 'lane');
    s.position.set(-4, 2.6, z); s.rotation.y = GFX.cam.yaw * 0.5;
    const pb = new Builder(); pb.cyl(0.08, 0.08, 2.2, 6, 0x7d8796, { x: -5.5, y: 1.1, z }); pb.cyl(0.08, 0.08, 2.2, 6, 0x7d8796, { x: -2.5, y: 1.1, z });
    lane2Bits.add(s, pb.build());
    group.add(lane2Bits);
  }
  function openLane2() {
    if (lane2Bits) { group.remove(lane2Bits); lane2Bits = null; }
    const b = new Builder();
    Models.laneTrack(b, CFG.laneZ[1], CFG.entranceX - 0.6, CFG.exitX + 0.6);
    const m = b.build(MAT.vc, false); m.receiveShadow = true; group.add(m);
  }

  function update(dt, t) {
    if (waterTex) { waterTex.offset.x += dt * 0.012; waterTex.offset.y += dt * 0.006; }
    if (snowPts) {
      const p = snowPts.geometry.attributes.position, c = GFX.camera.position;
      for (let i = 0; i < p.count; i++) {
        let y = p.getY(i) - dt * 2.2, x = p.getX(i) + Math.sin(t + i) * dt * 0.4;
        if (y < 0) { y = 30; }
        p.setXYZ(i, x, y, p.getZ(i));
      }
      p.needsUpdate = true;
      snowPts.position.set(Math.round(c.x / 10) * 10 - 10, 0, Math.round(c.z / 10) * 10 - 30);
    }
    for (const tw of tumble) {
      tw.m.position.x += tw.v * dt; tw.m.rotation.z -= tw.v * dt / 0.55;
      tw.m.position.y = 0.55 + Math.abs(Math.sin(t * 3 + tw.v)) * 0.5;
      if (tw.m.position.x > 70) tw.m.position.x = -70;
    }
  }

  function clear() {
    if (group) {
      GFX.scene.remove(group);
      group.traverse(o => { if (o.geometry && !o.geometry.parameters) o.geometry.dispose(); });
    }
    group = null; snowPts = null; stars = null; waterTex = null; tumble = []; lane2Bits = null;
  }

  return { build, update, clear, openLane2, THEMES, get theme() { return theme; }, get group() { return group; } };
})();
