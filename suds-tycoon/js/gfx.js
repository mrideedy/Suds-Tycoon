// gfx.js — renderer, camera rig, lights, materials, a merged low-poly model builder and canvas textures.
const PAL = {
  grass: 0x7cc850, grass2: 0x86d05a, grassDark: 0x69b443,
  lot: 0xdfe1e6, lotEdge: 0xc7cbd3, curb: 0xf4f5f8, curbTop: 0xffffff,
  road: 0x4d525b, roadEdge: 0x3f434b, sidewalk: 0xc9cdd5,
  water: 0x4aa8e8, sand: 0xf2dfa6,
  white: 0xeef1f6, whiteShade: 0xd5dbe5, steel: 0xa9b4c6, steelDark: 0x6f7b8e,
  blue: 0x2563eb, blueDark: 0x1a46b8, blueLight: 0x4f8cff, brush: 0x2d5fe3,
  orange: 0xff9f1c, orangeDark: 0xe07b00, yellow: 0xffc93c, hazard: 0xf4c531,
  black: 0x2a2d33, grate: 0x3b4049, dark: 0x22262d,
  wall: 0xf3dca8, wallShade: 0xe2c48c, roof: 0x2457c5, wood: 0xb07a45, woodDark: 0x8a5a33,
  trunk: 0x8a5a33, leaf: [0x5cb53c, 0x4ea935, 0x6cc44a, 0x58b83f],
  skin: [0xffcc99, 0xf1b98a, 0xd59a6a, 0x9c6844],
  cash: 0x4cc13a, cashDark: 0x2f9a22, gold: 0xffc92e, goldDark: 0xd99a00, gem: 0xb44cff,
  red: 0xe8383d, glass: 0x2b3c52, tire: 0x25282e, hub: 0xc8ced8, foam: 0xffffff,
  mud: [0x6b4a2b, 0x7a5532, 0x8b6a3e, 0x5e3f22, 0x7d5a35],
  purple: 0x7b2ff7, pink: 0xff6fb1, teal: 0x2fc6c6, green: 0x46c23a,
};

const GFX = (() => {
  let renderer, scene, camera, sun, hemi, amb;
  const cam = { yaw: 0.34, pitch: 0.8, fov: 30, zoom: 1, dist: 46, shake: 0, minZoom: 0.62, maxZoom: 1.55 };
  const target = new THREE.Vector3(), look = new THREE.Vector3(), camPos = new THREE.Vector3();
  const lightOff = new THREE.Vector3(-20, 34, -12);
  const lightDir = lightOff.clone().normalize();
  const lx = new THREE.Vector3(0, 1, 0).cross(lightDir).normalize();
  const ly = lightDir.clone().cross(lx).normalize();
  let shadowSize = 2048, shadowExtent = 34, quality = 'high';
  let W = 1, H = 1;

  function init(container) {
    renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x9fd9ff);
    scene.fog = new THREE.Fog(0x9fd9ff, 95, 210);

    camera = new THREE.PerspectiveCamera(cam.fov, 1, 1, 500);
    hemi = new THREE.HemisphereLight(0xeaf6ff, 0x9fb07c, 0.64);
    amb = new THREE.AmbientLight(0xffffff, 0.1);
    sun = new THREE.DirectionalLight(0xfff1dc, 0.6);
    sun.castShadow = true;
    sun.shadow.mapSize.set(shadowSize, shadowSize);
    const sc = sun.shadow.camera;
    sc.left = -shadowExtent; sc.right = shadowExtent; sc.top = shadowExtent; sc.bottom = -shadowExtent;
    sc.near = 1; sc.far = 140;
    sun.shadow.bias = -0.0004;
    sun.shadow.normalBias = 0.03;
    scene.add(hemi, amb, sun, sun.target);
    resize();
    window.addEventListener('resize', resize);
  }

  function setQuality(q) {
    quality = q;
    const pr = q === 'low' ? 1 : Math.min(window.devicePixelRatio || 1, 2);
    renderer.setPixelRatio(pr);
    renderer.shadowMap.enabled = q !== 'low';
    sun.castShadow = q !== 'low';
    const size = q === 'high' ? 2048 : 1024;
    if (size !== shadowSize) {
      shadowSize = size; sun.shadow.mapSize.set(size, size);
      if (sun.shadow.map) { sun.shadow.map.dispose(); sun.shadow.map = null; }
    }
    scene.traverse(o => { if (o.material) { const ms = Array.isArray(o.material) ? o.material : [o.material]; ms.forEach(m => (m.needsUpdate = true)); } });
    resize();
  }

  function resize() {
    const el = renderer.domElement.parentElement;
    W = el.clientWidth || window.innerWidth; H = el.clientHeight || window.innerHeight;
    renderer.setSize(W, H);
    camera.aspect = W / H;
    camera.updateProjectionMatrix();
  }

  // Distance that keeps a sensible slice of the lot in view for any aspect ratio.
  function baseDist() {
    const a = W / H, t = Math.tan(THREE.MathUtils.degToRad(cam.fov / 2)) * 2;
    const wantW = U.lerp(16, 40, U.clamp((a - 0.45) / (1.6 - 0.45), 0, 1));
    const wantH = 23;
    return Math.max(wantH / t, wantW / (t * a));
  }

  function setTheme(th) {
    scene.background.set(th.sky);
    scene.fog.color.set(th.fog || th.sky);
    scene.fog.near = th.fogNear || 95; scene.fog.far = th.fogFar || 210;
    hemi.color.set(th.hemiSky || 0xeaf6ff); hemi.groundColor.set(th.hemiGround || 0x9fb07c);
    hemi.intensity = th.hemi != null ? th.hemi : 0.64;
    sun.color.set(th.sunColor || 0xfff1dc); sun.intensity = th.sun != null ? th.sun : 0.6;
    amb.intensity = th.amb != null ? th.amb : 0.1;
  }

  const _shake = new THREE.Vector3();
  function updateCamera(dt, follow, snap) {
    target.set(follow.x, 0, follow.z);
    if (snap) look.copy(target); else look.lerp(target, 1 - Math.exp(-6 * dt));
    cam.dist = U.damp(cam.dist, baseDist() * cam.zoom, 8, dt);
    const d = cam.dist;
    camPos.set(Math.sin(cam.yaw) * Math.cos(cam.pitch) * d, Math.sin(cam.pitch) * d, Math.cos(cam.yaw) * Math.cos(cam.pitch) * d).add(look);
    if (cam.shake > 0.001) {
      cam.shake *= Math.exp(-9 * dt);
      _shake.set(U.rand(-1, 1), U.rand(-1, 1), U.rand(-1, 1)).multiplyScalar(cam.shake);
      camPos.add(_shake);
    }
    camera.position.copy(camPos);
    camera.lookAt(look.x, look.y + 0.5, look.z);
    // shadow box follows the view, snapped to texels to stop shimmering
    const texel = (shadowExtent * 2) / shadowSize;
    const c = look.clone().add(new THREE.Vector3(-3, 0, -4));
    const sx = Math.round(c.dot(lx) / texel) * texel, sy = Math.round(c.dot(ly) / texel) * texel, sz = c.dot(lightDir);
    const snapped = lx.clone().multiplyScalar(sx).add(ly.clone().multiplyScalar(sy)).add(lightDir.clone().multiplyScalar(sz));
    sun.target.position.copy(snapped);
    sun.position.copy(snapped).addScaledVector(lightDir, 70);
  }

  function shake(a) { if (!U.reducedMotion) cam.shake = Math.max(cam.shake, a); }
  function render() { renderer.render(scene, camera); }

  const _v = new THREE.Vector3();
  function project(p, out) {
    _v.copy(p).project(camera);
    out.x = (_v.x * 0.5 + 0.5) * W;
    out.y = (-_v.y * 0.5 + 0.5) * H;
    out.vis = _v.z < 1 && _v.z > -1;
    return out;
  }
  // screen -> ground plane (y = 0)
  const _ray = new THREE.Raycaster(), _ndc = new THREE.Vector2();
  function screenToGround(x, y) {
    _ndc.set((x / W) * 2 - 1, -(y / H) * 2 + 1);
    _ray.setFromCamera(_ndc, camera);
    const r = _ray.ray;
    if (Math.abs(r.direction.y) < 1e-4) return null;
    const t = -r.origin.y / r.direction.y;
    return r.origin.clone().addScaledVector(r.direction, t);
  }
  function raycast(x, y, objs) {
    _ndc.set((x / W) * 2 - 1, -(y / H) * 2 + 1);
    _ray.setFromCamera(_ndc, camera);
    return _ray.intersectObjects(objs, true);
  }

  return {
    init, resize, render, updateCamera, setTheme, setQuality, shake, project, screenToGround, raycast,
    get renderer() { return renderer; }, get scene() { return scene; }, get camera() { return camera; },
    get W() { return W; }, get H() { return H; }, cam, get quality() { return quality; },
  };
})();

// ---------------------------------------------------------------------------
// Geometry cache + merged vertex-coloured builder (one draw call per model).
// ---------------------------------------------------------------------------
const GEO = (() => {
  const cache = {};
  const k = (...a) => a.map(v => (typeof v === 'number' ? +v.toFixed(3) : v)).join('|');
  function rbox(w, h, d, r = 0.1, segs = 2) {
    const key = k('rb', w, h, d, r, segs);
    if (cache[key]) return cache[key];
    const seg = segs * 2 + 1;
    r = Math.min(w / 2, h / 2, d / 2, r);
    const g = new THREE.BoxGeometry(1, 1, 1, seg, seg, seg);
    const pos = g.attributes.position, nor = g.attributes.normal;
    const bx = w / 2 - r, by = h / 2 - r, bz = d / 2 - r, half = 0.5 / seg;
    const p = new THREE.Vector3(), n = new THREE.Vector3();
    for (let i = 0; i < pos.count; i++) {
      p.fromBufferAttribute(pos, i);
      n.copy(p);
      n.x -= Math.sign(n.x) * half; n.y -= Math.sign(n.y) * half; n.z -= Math.sign(n.z) * half;
      n.normalize();
      pos.setXYZ(i, bx * Math.sign(p.x) + n.x * r, by * Math.sign(p.y) + n.y * r, bz * Math.sign(p.z) + n.z * r);
      nor.setXYZ(i, n.x, n.y, n.z);
    }
    g.deleteAttribute('uv');
    return (cache[key] = g);
  }
  const box = (w, h, d) => cache[k('b', w, h, d)] || (cache[k('b', w, h, d)] = new THREE.BoxGeometry(w, h, d));
  const cyl = (rt, rb, h, s = 12, open = false) => cache[k('c', rt, rb, h, s, open)] || (cache[k('c', rt, rb, h, s, open)] = new THREE.CylinderGeometry(rt, rb, h, s, 1, open));
  const sphere = (r, ws = 12, hs = 8) => cache[k('s', r, ws, hs)] || (cache[k('s', r, ws, hs)] = new THREE.SphereGeometry(r, ws, hs));
  const ico = (r, d = 0) => cache[k('i', r, d)] || (cache[k('i', r, d)] = new THREE.IcosahedronGeometry(r, d));
  const dodeca = (r) => cache[k('d', r)] || (cache[k('d', r)] = new THREE.DodecahedronGeometry(r, 0));
  const cone = (r, h, s = 12) => cache[k('co', r, h, s)] || (cache[k('co', r, h, s)] = new THREE.ConeGeometry(r, h, s));
  const torus = (r, t, rs = 8, ts = 16, arc = Math.PI * 2) => cache[k('t', r, t, rs, ts, arc)] || (cache[k('t', r, t, rs, ts, arc)] = new THREE.TorusGeometry(r, t, rs, ts, arc));
  const lathe = (pts, s = 8) => { const key = k('l', JSON.stringify(pts), s); return cache[key] || (cache[key] = new THREE.LatheGeometry(pts.map(p => new THREE.Vector2(p[0], p[1])), s)); };
  const plane = (w, h) => cache[k('p', w, h)] || (cache[k('p', w, h)] = new THREE.PlaneGeometry(w, h));
  const circle = (r, s = 24) => cache[k('ci', r, s)] || (cache[k('ci', r, s)] = new THREE.CircleGeometry(r, s));
  return { rbox, box, cyl, sphere, ico, dodeca, cone, torus, lathe, plane, circle };
})();

const MAT = (() => {
  const vc = new THREE.MeshLambertMaterial({ vertexColors: true });
  const cache = {};
  function lam(color, o = {}) {
    const key = 'l' + color + JSON.stringify(o);
    return cache[key] || (cache[key] = new THREE.MeshLambertMaterial(Object.assign({ color }, o)));
  }
  function basic(color, o = {}) {
    const key = 'b' + color + JSON.stringify(Object.keys(o).filter(x => x !== 'map').reduce((a, x) => (a[x] = o[x], a), {})) + (o.map ? o.map.uuid : '');
    return cache[key] || (cache[key] = new THREE.MeshBasicMaterial(Object.assign({ color }, o)));
  }
  return { vc, lam, basic };
})();

const _m4 = new THREE.Matrix4(), _q = new THREE.Quaternion(), _e = new THREE.Euler(), _p = new THREE.Vector3(), _s = new THREE.Vector3();
class Builder {
  constructor() { this.parts = []; }
  add(geo, color, o = {}) {
    const g = geo.index ? geo.toNonIndexed() : geo.clone();
    _e.set(o.rx || 0, o.ry || 0, o.rz || 0, o.order || 'XYZ');
    _q.setFromEuler(_e);
    _p.set(o.x || 0, o.y || 0, o.z || 0);
    _s.set(o.sx != null ? o.sx : (o.s || 1), o.sy != null ? o.sy : (o.s || 1), o.sz != null ? o.sz : (o.s || 1));
    _m4.compose(_p, _q, _s);
    if (o.m) _m4.premultiply(o.m);
    g.applyMatrix4(_m4);
    if (o.flat) g.computeVertexNormals();
    const n = g.attributes.position.count, c = new THREE.Color(color), col = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) { col[i * 3] = c.r; col[i * 3 + 1] = c.g; col[i * 3 + 2] = c.b; }
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    for (const a of Object.keys(g.attributes)) if (a !== 'position' && a !== 'normal' && a !== 'color') g.deleteAttribute(a);
    this.parts.push(g);
    return this;
  }
  box(w, h, d, color, o) { return this.add(GEO.box(w, h, d), color, o); }
  rbox(w, h, d, r, color, o = {}) { return this.add(GEO.rbox(w, h, d, r, o.seg || 2), color, o); }
  cyl(rt, rb, h, s, color, o) { return this.add(GEO.cyl(rt, rb, h, s), color, o); }
  sphere(r, color, o = {}) { return this.add(GEO.sphere(r, o.ws || 12, o.hs || 8), color, o); }
  ico(r, color, o = {}) { return this.add(GEO.ico(r, o.detail || 0), color, Object.assign({ flat: true }, o)); }
  cone(r, h, s, color, o) { return this.add(GEO.cone(r, h, s), color, o); }
  torus(r, t, color, o = {}) { return this.add(GEO.torus(r, t, o.rs || 8, o.ts || 16, o.arc), color, o); }
  lathe(pts, s, color, o) { return this.add(GEO.lathe(pts, s), color, o); }
  merge() {
    let total = 0;
    for (const g of this.parts) total += g.attributes.position.count;
    const pos = new Float32Array(total * 3), nor = new Float32Array(total * 3), col = new Float32Array(total * 3);
    let off = 0;
    for (const g of this.parts) {
      pos.set(g.attributes.position.array, off * 3);
      nor.set(g.attributes.normal.array, off * 3);
      col.set(g.attributes.color.array, off * 3);
      off += g.attributes.position.count;
      g.dispose();
    }
    const out = new THREE.BufferGeometry();
    out.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    out.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
    out.setAttribute('color', new THREE.BufferAttribute(col, 3));
    out.computeBoundingSphere();
    this.parts = [];
    return out;
  }
  build(mat = MAT.vc, shadow = true) {
    const mesh = new THREE.Mesh(this.merge(), mat);
    mesh.castShadow = shadow; mesh.receiveShadow = true;
    return mesh;
  }
}

// ---------------------------------------------------------------------------
// Canvas textures: signs, stripes, decals.
// ---------------------------------------------------------------------------
const TEX = (() => {
  const cache = {};
  const FONT = "'Lilita One', 'Arial Rounded MT Bold', 'Trebuchet MS', sans-serif";
  function make(w, h, draw, key) {
    if (key && cache[key]) return cache[key];
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    const ctx = c.getContext('2d');
    draw(ctx, w, h);
    const t = new THREE.CanvasTexture(c);
    t.anisotropy = 4;
    t.userData = { canvas: c, ctx, draw };
    if (key) cache[key] = t;
    return t;
  }
  function rr(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y); ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath();
  }
  function text(ctx, s, x, y, size, fill = '#fff', stroke = null, sw = 0, align = 'center', font = FONT) {
    ctx.font = `${size}px ${font}`; ctx.textAlign = align; ctx.textBaseline = 'middle';
    if (stroke) { ctx.lineJoin = 'round'; ctx.lineWidth = sw; ctx.strokeStyle = stroke; ctx.strokeText(s, x, y); }
    ctx.fillStyle = fill; ctx.fillText(s, x, y);
  }
  function fit(ctx, s, maxW, size, font = FONT) {
    ctx.font = `${size}px ${font}`;
    const w = ctx.measureText(s).width;
    return w > maxW ? Math.floor(size * maxW / w) : size;
  }
  function icon(ctx, name, x, y, size) {
    const im = ICONS.imgs[name];
    if (im) ctx.drawImage(im, x - size / 2, y - size / 2, size, size);
  }
  const sign = (label, bg = '#2563eb', fg = '#ffffff', w = 512, h = 128, ico) => make(w, h, (ctx) => {
    rr(ctx, 4, 4, w - 8, h - 8, h * 0.22); ctx.fillStyle = bg; ctx.fill();
    ctx.lineWidth = 6; ctx.strokeStyle = 'rgba(255,255,255,.35)'; rr(ctx, 12, 12, w - 24, h - 24, h * 0.17); ctx.stroke();
    let tx = w / 2;
    if (ico) { icon(ctx, ico, h * 0.62, h / 2, h * 0.62); tx = w / 2 + h * 0.3; }
    const size = fit(ctx, label, w - (ico ? h * 1.35 : h * 0.5), h * 0.56);
    text(ctx, label, tx, h / 2 + 3, size, fg, 'rgba(0,0,0,.25)', 8);
  }, `sign|${label}|${bg}|${fg}|${w}|${h}|${ico}`);
  const hazard = () => make(128, 32, (ctx, w, h) => {
    ctx.fillStyle = '#f4c531'; ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = '#23262c';
    for (let i = -2; i < 8; i++) { ctx.beginPath(); ctx.moveTo(i * 32, h); ctx.lineTo(i * 32 + 16, h); ctx.lineTo(i * 32 + 32, 0); ctx.lineTo(i * 32 + 16, 0); ctx.fill(); }
  }, 'hazard');
  const grate = () => make(128, 128, (ctx, w, h) => {
    ctx.fillStyle = '#353a42'; ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = '#23272d';
    for (let i = 0; i < 8; i++) ctx.fillRect(4, 6 + i * 16, w - 8, 7);
  }, 'grate');
  const soft = () => make(128, 128, (ctx, w, h) => {
    const g = ctx.createRadialGradient(64, 64, 0, 64, 64, 64);
    g.addColorStop(0, 'rgba(0,0,0,0.55)'); g.addColorStop(0.55, 'rgba(0,0,0,0.25)'); g.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);
  }, 'soft');
  const glow = () => make(128, 128, (ctx, w, h) => {
    const g = ctx.createRadialGradient(64, 64, 0, 64, 64, 64);
    g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(0.35, 'rgba(255,255,255,.5)'); g.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);
  }, 'glow');
  const sparkle = () => make(64, 64, (ctx) => {
    ctx.fillStyle = '#fff';
    ctx.beginPath(); ctx.moveTo(32, 0); ctx.quadraticCurveTo(36, 28, 64, 32); ctx.quadraticCurveTo(36, 36, 32, 64);
    ctx.quadraticCurveTo(28, 36, 0, 32); ctx.quadraticCurveTo(28, 28, 32, 0); ctx.fill();
  }, 'sparkle');
  const waterStreaks = () => make(64, 256, (ctx, w, h) => {
    ctx.clearRect(0, 0, w, h);
    for (let i = 0; i < 14; i++) {
      const x = 2 + Math.random() * (w - 4), y = Math.random() * h, len = 30 + Math.random() * 90;
      const g = ctx.createLinearGradient(0, y, 0, y + len);
      g.addColorStop(0, 'rgba(160,220,255,0)'); g.addColorStop(0.5, 'rgba(200,236,255,.95)'); g.addColorStop(1, 'rgba(160,220,255,0)');
      ctx.fillStyle = g; ctx.fillRect(x, y, 3 + Math.random() * 3, len);
      ctx.fillRect(x, y - h, 3, len);
    }
  }, 'water');
  const dash = () => make(64, 16, (ctx, w, h) => { ctx.clearRect(0, 0, w, h); ctx.fillStyle = '#fff'; ctx.fillRect(0, 2, 36, h - 4); }, 'dash');
  const ripple = () => make(128, 128, (ctx, w, h) => {
    ctx.clearRect(0, 0, w, h);
    ctx.strokeStyle = 'rgba(255,255,255,.9)'; ctx.lineWidth = 6;
    ctx.beginPath(); ctx.arc(64, 64, 52, 0, Math.PI * 2); ctx.stroke();
  }, 'ripple');
  const ring = () => make(128, 128, (ctx, w, h) => {
    ctx.clearRect(0, 0, w, h);
    ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 14; ctx.setLineDash([22, 14]);
    ctx.beginPath(); ctx.arc(64, 64, 54, 0, Math.PI * 2); ctx.stroke();
  }, 'ring');
  return { make, rr, text, fit, icon, sign, hazard, grate, soft, glow, sparkle, waterStreaks, dash, ripple, ring, FONT };
})();
