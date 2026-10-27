// minigame.js — Bubble Blaster: a Mob Control-style battle against the Mud Monster.
// Slide the soap cannon, fire bubbles through multiplier gates, pop the mud blobs, splat the boss.
const Blaster = (() => {
  let active = false, scene = null, camera = null, st = null, hudEl = null, stageEl = null;
  const AR = { x: 6, zCannon: 17, zBoss: -21, zSpawn: -18 };
  const S = () => Store.s;

  // ---------------- tickets ----------------
  function maxTickets() { return CFG.blaster.baseTickets + S().gemUp.tickets; }
  function refillTickets() {
    const b = S().blaster, now = Date.now(), every = CFG.blaster.ticketEvery * 1000;
    if (b.tickets >= maxTickets()) { b.lastTick = now; return; }
    const n = Math.floor((now - b.lastTick) / every);
    if (n > 0) { b.tickets = Math.min(maxTickets(), b.tickets + n); b.lastTick += n * every; }
  }
  function nextTicketIn() { const b = S().blaster; return Math.max(0, CFG.blaster.ticketEvery * 1000 - (Date.now() - b.lastTick)) / 1000; }

  function panel() {
    refillTickets();
    const b = S().blaster, lvl = b.level, boss = bossName(lvl);
    return {
      title: 'Bubble Blaster', icon: 'blaster', sub: `Level ${lvl} · ${boss}`,
      render(body, cur) {
        const adLeft = CFG.ads.limits.ticket - Store.caps().ticket;
        body.innerHTML = `
          <div class="note" style="font-size:15px">Drag to aim the soap cannon. Bubbles that pass through <b style="color:#1a8f2a">green gates</b> multiply, <b style="color:#e8383d">red gates</b> pop them.
          Pop the mud blobs before they reach you, then splat the <b>${boss}</b>!</div>
          <div class="row"><div class="r-ico">${ICONS.html('blaster')}</div><div class="r-main"><div class="r-title">Tickets: ${b.tickets}/${maxTickets()}</div>
          <div class="r-sub" id="bb-next">${b.tickets >= maxTickets() ? 'Full' : 'Next ticket in ' + U.fmtTime(nextTicketIn())}</div></div>
          <div><button class="btn ad purple${adLeft > 0 ? '' : ' gray'}" id="bb-ticket">+1</button></div></div>
          <div class="row"><div class="r-ico">${ICONS.html('trophy')}</div><div class="r-main"><div class="r-title">Win reward</div><div class="r-sub">${ICONS.html('cash')} ${U.fmt(winCash(lvl))} &nbsp; ${ICONS.html('gem')} ${winGems(lvl)}</div></div></div>
          <div class="btn-row"><button class="btn big${b.tickets > 0 ? '' : ' gray'}" id="bb-play">Play</button></div>`;
        body.querySelector('#bb-play').onclick = () => {
          if (S().blaster.tickets <= 0) { UI.toast('No tickets left. Watch an ad for one more!', 'blaster'); return; }
          S().blaster.tickets--; if (S().blaster.tickets === maxTickets() - 1) S().blaster.lastTick = Date.now();
          UI.closePanel(true); start();
        };
        body.querySelector('#bb-ticket').onclick = async () => {
          if (Store.caps().ticket >= CFG.ads.limits.ticket) { UI.toast('No more ad tickets today', 'clock'); return; }
          if (await Ads.rewarded('blaster_ticket')) { Store.caps().ticket++; S().blaster.tickets++; Sfx.play.reward(); UI.rerender(); }
        };
        cur.tick = () => { const e = body.querySelector('#bb-next'); if (e) e.textContent = S().blaster.tickets >= maxTickets() ? 'Full' : 'Next ticket in ' + U.fmtTime(nextTicketIn()); };
      },
    };
  }
  const bossName = (l) => (l % 5 === 0 ? (l % 10 === 0 ? 'Swamp King' : 'Mud Truck') : 'Mud Monster');
  const winCash = (l) => Game.rewardFor(3 + Math.min(l, 20) * 0.5) * (l % 5 === 0 ? 2 : 1);
  const winGems = (l) => 3 + Math.floor(l / 2) + (l % 5 === 0 ? 5 : 0);

  // ---------------- scene ----------------
  function build(level) {
    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x8fd3ff);
    scene.fog = new THREE.Fog(0x8fd3ff, 60, 120);
    camera = new THREE.PerspectiveCamera(42, GFX.W / GFX.H, 1, 300);
    const hemi = new THREE.HemisphereLight(0xeaf6ff, 0x8aa070, 0.75);
    const sun = new THREE.DirectionalLight(0xfff1dc, 0.55);
    sun.position.set(-10, 30, 10); sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    Object.assign(sun.shadow.camera, { left: -16, right: 16, top: 30, bottom: -30, near: 1, far: 80 });
    scene.add(hemi, sun);
    // arena
    const b = new Builder();
    b.box(14, 1, 46, 0xeef3fb, { y: -0.5, z: -2 });
    for (let i = 0; i < 12; i++) b.box(14, 0.02, 1.6, i % 2 ? 0xdfe9f7 : 0xeef3fb, { y: 0.005, z: 16 - i * 3.6 });
    for (const sx of [-1, 1]) { b.rbox(0.6, 0.9, 46, 0.2, PAL.blue, { x: sx * 7.2, y: 0.2, z: -2 }); b.rbox(0.62, 0.2, 46.2, 0.08, 0xffffff, { x: sx * 7.2, y: 0.68, z: -2 }); }
    b.box(14, 0.03, 8, 0x7a5532, { y: 0.01, z: -21 });
    for (let i = 0; i < 14; i++) b.ico(U.rand(0.4, 1.0), U.pick(PAL.mud), { x: U.rand(-6.5, 6.5), y: 0.1, z: U.rand(-24, -17), sy: 0.35 });
    // grass + trees around
    b.box(200, 0.1, 200, 0x7cc850, { y: -1.05 });
    for (let i = 0; i < 40; i++) { const sx = Math.random() < 0.5 ? -1 : 1; Models.tree(b, sx * U.rand(10, 40), U.rand(-60, 25), U.rand(1, 1.6)); }
    const arena = b.build(); arena.receiveShadow = true; scene.add(arena);
    // cannon
    const cb = new Builder();
    cb.rbox(2.4, 0.6, 2.0, 0.2, 0x9aa3b1, { y: 0.3 });
    cb.cyl(0.9, 1.0, 0.6, 16, PAL.blue, { y: 0.9 });
    cb.rbox(0.9, 0.9, 2.6, 0.4, 0x2f63e6, { y: 1.45, z: -1.0, rx: -0.2 });
    cb.cyl(0.5, 0.5, 0.3, 14, 0x9fdcff, { y: 1.7, z: -2.3, rx: Math.PI / 2 - 0.2 });
    cb.sphere(0.55, 0xff6fb1, { y: 2.0, z: 0.6 });
    const cannon = cb.build(); cannon.position.set(0, 0, AR.zCannon); scene.add(cannon);
    // boss
    const boss = makeBoss(level);
    boss.position.set(0, 0, AR.zBoss); scene.add(boss);
    // instanced bubbles + blobs
    const bubbleMesh = new THREE.InstancedMesh(GEO.sphere(0.32, 12, 8), new THREE.MeshLambertMaterial({ color: 0x9fe0ff, emissive: 0x1a4a6a }), 700);
    bubbleMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); bubbleMesh.count = 0; bubbleMesh.frustumCulled = false; bubbleMesh.castShadow = true;
    const blobGeoB = new Builder();
    blobGeoB.ico(0.5, 0x7a5532, { y: 0.4, sy: 0.85, detail: 1, flat: true });
    blobGeoB.sphere(0.12, 0xffffff, { x: -0.16, y: 0.62, z: 0.36, ws: 8, hs: 6 }); blobGeoB.sphere(0.12, 0xffffff, { x: 0.16, y: 0.62, z: 0.36, ws: 8, hs: 6 });
    blobGeoB.sphere(0.06, 0x1b1b1b, { x: -0.16, y: 0.62, z: 0.46, ws: 6, hs: 4 }); blobGeoB.sphere(0.06, 0x1b1b1b, { x: 0.16, y: 0.62, z: 0.46, ws: 6, hs: 4 });
    const blobMesh = new THREE.InstancedMesh(blobGeoB.merge(), MAT.vc, 400);
    blobMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); blobMesh.count = 0; blobMesh.frustumCulled = false; blobMesh.castShadow = true;
    scene.add(bubbleMesh, blobMesh);
    // particles reuse FX classes on our own scene
    const pops = new Particles(GEO.ico(0.3, 1), new THREE.MeshLambertMaterial({ color: 0xffffff }), 400);
    const muds = new Particles(GEO.ico(0.25, 0), MAT.lam(0x6b4a2b), 400);
    scene.add(pops.mesh, muds.mesh);
    // gates
    const gates = makeGates(level);
    for (const g of gates) scene.add(g.mesh);
    // mega bubble
    const mega = new THREE.Mesh(GEO.sphere(1.3, 18, 14), new THREE.MeshLambertMaterial({ color: 0xbfeaff, transparent: true, opacity: 0.85, emissive: 0x2a6a9a }));
    mega.visible = false; scene.add(mega);
    camera.position.set(0, 21, 31); camera.lookAt(0, 0, -3);
    return { cannon, boss, bubbleMesh, blobMesh, pops, muds, gates, mega };
  }
  function makeBoss(level) {
    const g = new THREE.Group();
    const big = level % 5 === 0;
    const s = big ? 1.35 : 1;
    const b = new Builder();
    b.ico(3.2 * s, 0x6b4a2b, { y: 2.6 * s, sy: 0.8, detail: 1, flat: true });
    for (let i = 0; i < 10; i++) { const a = i / 10 * Math.PI * 2; b.ico(U.rand(0.7, 1.2) * s, U.pick(PAL.mud), { x: Math.cos(a) * 2.8 * s, y: U.rand(0.6, 3.6) * s, z: Math.sin(a) * 2.2 * s, flat: true }); }
    b.sphere(0.75 * s, 0xffffff, { x: -1.1 * s, y: 3.5 * s, z: 2.4 * s, ws: 12, hs: 10 }); b.sphere(0.75 * s, 0xffffff, { x: 1.1 * s, y: 3.5 * s, z: 2.4 * s, ws: 12, hs: 10 });
    b.sphere(0.35 * s, 0x1b1b1b, { x: -1.0 * s, y: 3.4 * s, z: 3.05 * s, ws: 10, hs: 8 }); b.sphere(0.35 * s, 0x1b1b1b, { x: 1.0 * s, y: 3.4 * s, z: 3.05 * s, ws: 10, hs: 8 });
    b.box(1.4 * s, 0.3 * s, 0.3 * s, 0x3a2614, { x: -1.1 * s, y: 4.4 * s, z: 2.6 * s, rz: -0.4 }); b.box(1.4 * s, 0.3 * s, 0.3 * s, 0x3a2614, { x: 1.1 * s, y: 4.4 * s, z: 2.6 * s, rz: 0.4 });
    b.rbox(2.4 * s, 0.7 * s, 0.6 * s, 0.25, 0x2a1508, { y: 1.9 * s, z: 2.7 * s });
    for (let i = 0; i < 4; i++) b.cone(0.18 * s, 0.4 * s, 4, 0xffffff, { x: (-0.75 + i * 0.5) * s, y: 2.1 * s, z: 2.95 * s, rx: Math.PI });
    if (level % 10 === 0) { b.cyl(1.0, 1.1, 0.6, 10, PAL.gold, { y: 5.6 * s }); for (let i = 0; i < 6; i++) { const a = i / 6 * Math.PI * 2; b.cone(0.22, 0.6, 5, PAL.gold, { x: Math.cos(a) * 0.9, y: 6.2 * s, z: Math.sin(a) * 0.9 }); } }
    else if (big) { for (const sx of [-1, 1]) for (const sz of [-1, 1]) b.cyl(0.9, 0.9, 0.7, 14, 0x25282e, { x: sx * 2.6, y: 0.9, z: sz * 1.6, rz: Math.PI / 2 }); }
    const m = b.build(); g.add(m);
    return g;
  }
  function gateTex(label, good) {
    return TEX.make(256, 128, (ctx, w, h) => {
      ctx.clearRect(0, 0, w, h);
      TEX.rr(ctx, 6, 6, w - 12, h - 12, 22); ctx.fillStyle = good ? 'rgba(70,194,58,.55)' : 'rgba(232,56,61,.55)'; ctx.fill();
      ctx.lineWidth = 8; ctx.strokeStyle = '#ffffff'; ctx.stroke();
      TEX.text(ctx, label, w / 2, h / 2 + 4, 76, '#ffffff', good ? '#14600f' : '#7a0a0a', 10);
    }, 'gate|' + label + good);
  }
  function makeGates(level) {
    const rows = [8.5, 1, -6.5, -12.5];
    const rng = mulberry(level * 977 + 13);
    const gates = [];
    rows.forEach((z, ri) => {
      if (ri === 3 && level < 3) return;
      const layout = rng() < 0.5 ? 'split' : 'single';
      const pick = (good) => good ? ['x2', 'x2', 'x3', '+10', '+5'][Math.floor(rng() * 5)] : ['-5', '÷2', '-10'][Math.floor(rng() * 3)];
      const mk = (x, w, label, moving) => {
        const good = label[0] === 'x' || label[0] === '+';
        const mesh = new THREE.Group();
        const pane = new THREE.Mesh(GEO.plane(w, 2.2), new THREE.MeshBasicMaterial({ map: gateTex(label, good), transparent: true, depthWrite: false, side: THREE.DoubleSide }));
        pane.position.y = 1.2; mesh.add(pane);
        const pb = new Builder();
        pb.rbox(0.3, 2.6, 0.3, 0.1, good ? 0x46c23a : 0xe8383d, { x: -w / 2, y: 1.3 }); pb.rbox(0.3, 2.6, 0.3, 0.1, good ? 0x46c23a : 0xe8383d, { x: w / 2, y: 1.3 });
        mesh.add(pb.build());
        mesh.position.set(x, 0, z);
        gates.push({ mesh, x, z, w, label, good, moving, phase: rng() * 6, cd: 0, id: gates.length });
      };
      if (layout === 'split') { mk(-3.2, 5.4, pick(true), false); mk(3.2, 5.4, pick(rng() < 0.35), false); }
      else mk(0, 5.6, pick(rng() < 0.85), level > 1 && rng() < 0.6);
    });
    return gates;
  }
  function mulberry(a) { return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

  // ---------------- run ----------------
  function start() {
    const level = S().blaster.level;
    const sc = build(level);
    const big = level % 5 === 0;
    st = Object.assign(sc, {
      level, t: 60, hp: Math.round((780 + level * 95) * (big ? 1.3 : 1)), hpMax: 0, hearts: 3,
      cx: 0, tx: 0, fireT: 0, firing: false, spawnT: 1.5, charge: 0, megaOn: false, megaZ: 0, megaX: 0,
      bubbles: [], blobs: [], over: false, hitFlash: 0, bossWob: 0, popped: 0,
      spawnEvery: Math.max(0.2, 0.42 - level * 0.012), blobSpeed: 2.4 + Math.min(level, 20) * 0.08, fireEvery: 1 / (6 + Math.min(level, 20) * 0.3),
    });
    st.hpMax = st.hp;
    active = true;
    Game.paused = true;
    document.getElementById('hud').hidden = true;
    Ads.banner(false);
    hudEl = U.el('div', 'bb-hud');
    hudEl.innerHTML = `<div class="bb-top"><div class="bb-boss"><div id="bb-hp" style="width:100%"></div><span>${bossName(level)} · Lv ${level}</span></div>
      <div class="bb-row"><span id="bb-hearts"></span><span>${ICONS.html('clock')} <span id="bb-time">60</span></span><span>${ICONS.html('bubble')} <span id="bb-count">0</span></span></div></div>
      <button class="bb-charge" id="bb-mega"><div class="inner">MEGA<br>BUBBLE</div></button>
      <div class="bb-hint" id="bb-hint">Hold and drag to shoot!</div>
      <button class="btn red bb-quit" id="bb-quit">Give up</button>`;
    document.getElementById('app').appendChild(hudEl);
    hudEl.querySelector('#bb-mega').onclick = (e) => { e.stopPropagation(); fireMega(); };
    hudEl.querySelector('#bb-mega').addEventListener('pointerdown', (e) => e.stopPropagation());
    hudEl.querySelector('#bb-quit').onclick = () => finish(false);
    hearts();
    stageEl = document.getElementById('stage');
    stageEl.addEventListener('pointerdown', pDown); stageEl.addEventListener('pointermove', pMove);
    window.addEventListener('pointerup', pUp); window.addEventListener('keydown', kDown); window.addEventListener('keyup', kUp);
    Sfx.play.whoosh();
    Ads.gameplay(true);
  }
  function pointerToX(clientX) {
    // map screen x across the arena width at the cannon line
    const r = stageEl.getBoundingClientRect();
    const nx = ((clientX - r.left) / r.width) * 2 - 1;
    return U.clamp(nx * AR.x * 1.25, -AR.x + 0.8, AR.x - 0.8);
  }
  function pDown(e) { Sfx.unlock(); st.firing = true; st.tx = pointerToX(e.clientX); const h = document.getElementById('bb-hint'); if (h) h.remove(); }
  function pMove(e) { if (st && (st.firing || e.pointerType === 'mouse')) st.tx = pointerToX(e.clientX); }
  function pUp() { if (st) st.firing = false; }
  const keys = {};
  function kDown(e) { keys[e.key.toLowerCase()] = true; if (e.key === ' ') st.firing = true; if (e.key.toLowerCase() === 'e') fireMega(); }
  function kUp(e) { keys[e.key.toLowerCase()] = false; if (e.key === ' ') st.firing = false; }
  function input() { /* joystick input is ignored in the blaster */ }
  function hearts() { const h = hudEl && hudEl.querySelector('#bb-hearts'); if (h) h.innerHTML = Array.from({ length: 3 }, (_, i) => `<span style="opacity:${i < st.hearts ? 1 : 0.25}">${ICONS.html('heart')}</span>`).join(' '); }
  function fireMega() {
    if (!st || st.over || st.charge < 1 || st.megaOn) return;
    st.charge = 0; st.megaOn = true; st.megaZ = AR.zCannon - 2; st.megaX = st.cx;
    st.mega.visible = true; Sfx.play.whoosh(); GFX.shake(0.2);
  }
  function addBubble(x, z, mask) { if (st.bubbles.length < 680) st.bubbles.push({ x, z, mask: mask || 0, vx: 0 }); }

  function update(dt) {
    if (!active || !st) return;
    if (st.over) { render(dt); return; }
    st.t -= dt;
    if (keys.a || keys.arrowleft) st.tx = Math.max(-AR.x + 0.8, st.tx - 12 * dt);
    if (keys.d || keys.arrowright) st.tx = Math.min(AR.x - 0.8, st.tx + 12 * dt);
    st.cx = U.damp(st.cx, st.tx, 14, dt);
    st.cannon.position.x = st.cx;
    // fire
    st.fireT -= dt;
    if (st.firing && st.fireT <= 0) { st.fireT = st.fireEvery; addBubble(st.cx + U.rand(-0.15, 0.15), AR.zCannon - 2.4, 0); Sfx.play.pew(); st.cannon.scale.set(1.04, 0.96, 1.04); }
    st.cannon.scale.lerp(new THREE.Vector3(1, 1, 1), 0.3);
    // gates
    for (const g of st.gates) {
      if (g.moving) { g.x = Math.sin(st.t * 0.9 + g.phase) * 3.2; g.mesh.position.x = g.x; }
      g.cd = Math.max(0, g.cd - dt);
      g.mesh.children[0].material.opacity = g.cd > 0 ? 0.4 : 1;
    }
    // bubbles
    for (let i = st.bubbles.length - 1; i >= 0; i--) {
      const b = st.bubbles[i];
      const pz = b.z;
      b.z -= 9.5 * dt; b.x += b.vx * dt; b.vx *= 0.9;
      b.x = U.clamp(b.x, -AR.x + 0.4, AR.x - 0.4);
      for (const g of st.gates) {
        if (b.mask & (1 << g.id)) continue;
        if (pz >= g.z && b.z < g.z && Math.abs(b.x - g.x) < g.w / 2) {
          b.mask |= 1 << g.id;
          const L = g.label;
          if (L[0] === 'x') { const n = +L.slice(1) - 1; for (let k = 0; k < n; k++) { addBubble(b.x + U.rand(-0.6, 0.6), b.z - 0.3, b.mask); st.bubbles[st.bubbles.length - 1].vx = U.rand(-2, 2); } }
          else if (L[0] === '+') { if (g.cd <= 0) { g.cd = 1.2; const n = +L.slice(1); for (let k = 0; k < n; k++) { addBubble(g.x + U.rand(-g.w / 2, g.w / 2), b.z - U.rand(0.2, 1.2), b.mask); } Sfx.play.bubble(); } }
          else if (L === '÷2') { if (Math.random() < 0.5) { st.bubbles.splice(i, 1); pop(b.x, b.z, false); break; } }
          else { st.bubbles.splice(i, 1); pop(b.x, b.z, false); break; }
        }
      }
      if (st.bubbles[i] !== b) continue;
      if (b.z < AR.zBoss + 3.5) {
        if (Math.abs(b.x) < 4.2) { st.bubbles.splice(i, 1); hitBoss(1, b.x); }
        else if (b.z < AR.zBoss) st.bubbles.splice(i, 1);
      }
    }
    // spawn blobs
    st.spawnT -= dt;
    const enrage = 1 + (1 - st.hp / st.hpMax) * 0.8;
    if (st.spawnT <= 0) {
      st.spawnT = st.spawnEvery / enrage;
      const n = 2 + (Math.random() < 0.3 + Math.min(st.level, 20) * 0.02 ? 2 : 0);
      for (let k = 0; k < n; k++) st.blobs.push({ x: U.rand(-AR.x + 1, AR.x - 1), z: AR.zSpawn - U.rand(0, 1), w: Math.random() * 6 });
    }
    // blobs move + collide
    for (let i = st.blobs.length - 1; i >= 0; i--) {
      const m = st.blobs[i];
      m.z += st.blobSpeed * dt; m.w += dt * 8;
      let dead = false;
      for (let j = st.bubbles.length - 1; j >= 0; j--) {
        const b = st.bubbles[j];
        if (Math.abs(b.z - m.z) < 0.7 && Math.abs(b.x - m.x) < 0.7) { st.bubbles.splice(j, 1); dead = true; break; }
      }
      if (!dead && st.megaOn && Math.abs(st.megaZ - m.z) < 1.6 && Math.abs(st.megaX - m.x) < 1.8) dead = true;
      if (dead) { st.blobs.splice(i, 1); pop(m.x, m.z, true); st.charge = Math.min(1, st.charge + 0.035); st.popped++; continue; }
      if (m.z > AR.zCannon - 1) {
        st.blobs.splice(i, 1); st.hearts--; hearts(); GFX.shake(0.5); Sfx.play.splat(); Sfx.play.error();
        st.muds.spawn({ x: m.x, y: 1, z: m.z, vy: 3, life: 0.6, s0: 2, s1: 0, g: 9 });
        if (st.hearts <= 0) { finish(false); return; }
      }
    }
    // mega bubble
    if (st.megaOn) {
      st.megaZ -= 7 * dt;
      st.mega.position.set(st.megaX, 1.4 + Math.sin(st.t * 10) * 0.1, st.megaZ);
      st.mega.rotation.y += dt * 3;
      if (st.megaZ < AR.zBoss + 3.8) { st.megaOn = false; st.mega.visible = false; hitBoss(25, st.megaX); Sfx.play.boom(); GFX.shake(0.6); for (let k = 0; k < 30; k++) st.pops.spawn({ x: st.megaX + U.rand(-2, 2), y: U.rand(1, 4), z: AR.zBoss + 3, vx: U.rand(-4, 4), vy: U.rand(2, 7), vz: U.rand(-1, 4), life: 0.8, s0: U.rand(1, 2.5), s1: 0, g: 8 }); }
    }
    if (st.t <= 0) { finish(false); return; }
    render(dt);
    // HUD
    hudEl.querySelector('#bb-hp').style.width = Math.max(0, (st.hp / st.hpMax) * 100) + '%';
    hudEl.querySelector('#bb-time').textContent = Math.ceil(st.t);
    hudEl.querySelector('#bb-count').textContent = st.bubbles.length;
    const mg = hudEl.querySelector('#bb-mega');
    mg.style.setProperty('--p', (st.charge * 100).toFixed(0) + '%');
    mg.classList.toggle('ready', st.charge >= 1 && !st.megaOn);
  }
  function hitBoss(dmg, x) {
    st.hp -= dmg; st.bossWob = 1; st.hitFlash = 0.1;
    if (Math.random() < 0.5 || dmg > 1) st.muds.spawn({ x: x + U.rand(-0.5, 0.5), y: U.rand(2, 4.5), z: AR.zBoss + 2.8, vx: U.rand(-3, 3), vy: U.rand(2, 5), vz: U.rand(1, 3), life: 0.7, s0: 1.4, s1: 0, g: 10 });
    Sfx.play.splat();
    if (st.hp <= 0 && !st.over) { st.hp = 0; finish(true); }
  }
  function pop(x, z, mud) {
    for (let k = 0; k < 3; k++) st.pops.spawn({ x: x + U.rand(-0.2, 0.2), y: 0.6, z, vx: U.rand(-2, 2), vy: U.rand(1, 3), vz: U.rand(-2, 2), life: 0.35, s0: 0.9, s1: 0, g: 6 });
    if (mud) for (let k = 0; k < 3; k++) st.muds.spawn({ x, y: 0.5, z, vx: U.rand(-3, 3), vy: U.rand(2, 4), vz: U.rand(-2, 2), life: 0.5, s0: 1, s1: 0, g: 12 });
    Sfx.play.pop();
  }
  function render(dt) {
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), p = new THREE.Vector3(), s = new THREE.Vector3();
    const bm = st.bubbleMesh;
    st.bubbles.forEach((b, i) => { p.set(b.x, 0.55 + Math.sin(b.z * 2 + i) * 0.06, b.z); s.setScalar(1); m.compose(p, q, s); bm.setMatrixAt(i, m); });
    bm.count = st.bubbles.length; bm.instanceMatrix.needsUpdate = true;
    const lm = st.blobMesh;
    st.blobs.forEach((b, i) => { p.set(b.x, Math.abs(Math.sin(b.w)) * 0.25, b.z); const sq = 1 + Math.sin(b.w * 2) * 0.08; s.set(sq, 1 / sq, sq); m.compose(p, q, s); lm.setMatrixAt(i, m); });
    lm.count = st.blobs.length; lm.instanceMatrix.needsUpdate = true;
    st.pops.update(dt, camera.quaternion); st.muds.update(dt, camera.quaternion);
    st.bossWob = U.damp(st.bossWob, 0, 6, dt);
    const w = 1 + Math.sin(performance.now() / 60) * st.bossWob * 0.06;
    st.boss.scale.set(w, 1 / w, w);
    st.boss.rotation.y = Math.sin(performance.now() / 700) * 0.15;
    camera.aspect = GFX.W / GFX.H;
    camera.fov = GFX.W / GFX.H < 0.8 ? 58 : 42; camera.updateProjectionMatrix();
    camera.position.set(st.cx * 0.15, GFX.W / GFX.H < 0.8 ? 26 : 21, 31);
    camera.lookAt(0, 0, -3);
  }
  function finish(win) {
    if (!st || st.over) return;
    st.over = true; st.firing = false;
    const level = st.level;
    if (win) { Sfx.play.win(); S().blaster.level++; S().stats.blasterWins++; for (let k = 0; k < 60; k++) st.pops.spawn({ x: U.rand(-4, 4), y: U.rand(1, 6), z: AR.zBoss + 2, vx: U.rand(-5, 5), vy: U.rand(3, 9), vz: U.rand(-1, 5), life: 1.2, s0: U.rand(1, 2), s1: 0, g: 8 }); st.boss.visible = false; }
    else Sfx.play.lose();
    const cash = win ? winCash(level) : Math.round(winCash(level) * 0.2);
    const gems = win ? winGems(level) : 0;
    setTimeout(() => UI.openPanel('blasterResult', { win, cash, gems, level, popped: st.popped }), 900);
  }
  function resultPanel(o) {
    return {
      title: o.win ? 'Mud Monster splatted!' : 'The mud won this time', icon: o.win ? 'trophy' : 'blaster', noClose: true,
      render(b) {
        const x3Left = CFG.ads.limits.blasterX3 - Store.caps().blasterX3;
        b.innerHTML = `<div class="hero-num">${ICONS.html('cash')}${U.fmt(o.cash)}${o.gems ? `&nbsp;${ICONS.html('gem')}${o.gems}` : ''}</div>
          <div class="note">${o.popped} mud blobs popped${o.win ? ` · next: Level ${o.level + 1}` : ''}</div>
          <div class="btn-row"><button class="btn big" id="br-ok">Collect</button>${x3Left > 0 ? `<button class="btn big ad purple" id="br-x3">Collect x3</button>` : ''}</div>`;
        const take = (m) => {
          UI.closePanel(true); stop();
          Game.addCash(o.cash * m); if (o.gems) Game.addGems(o.gems * m);
          UI.rewardBurst({ cash: o.cash * m, gems: o.gems * m }); Sfx.play.reward();
          if (m === 1) setTimeout(() => Ads.interstitial('blaster_end'), 600);
          Store.save();
        };
        b.querySelector('#br-ok').onclick = () => take(1);
        const x3 = b.querySelector('#br-x3');
        if (x3) x3.onclick = async () => { if (await Ads.rewarded('blaster_x3')) { Store.caps().blasterX3++; take(3); } };
      },
    };
  }
  function stop() {
    active = false;
    if (stageEl) { stageEl.removeEventListener('pointerdown', pDown); stageEl.removeEventListener('pointermove', pMove); }
    window.removeEventListener('pointerup', pUp); window.removeEventListener('keydown', kDown); window.removeEventListener('keyup', kUp);
    if (hudEl) hudEl.remove(); hudEl = null;
    if (scene) scene.traverse(o => { if (o.isInstancedMesh) o.dispose(); });
    scene = null; camera = null; st = null;
    document.getElementById('hud').hidden = false;
    Ads.banner(CFG.ads.banner);
    Game.paused = false;
    UI.refreshAll();
  }
  return {
    panel, resultPanel, refillTickets, update, input, forceStop: () => { if (active) stop(); },
    get active() { return active; }, get scene() { return scene; }, get camera() { return camera; }, get state() { return st; },
  };
})();
