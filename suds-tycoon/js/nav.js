// nav.js — grid A* over the lot so staff walk around machines, racks and buildings.
const Nav = (() => {
  const cs = 0.5;
  let x0 = 0, z0 = 0, w = 1, h = 1, grid = new Uint8Array(1);
  const colliders = [];

  function init(b) {
    x0 = b.x0; z0 = b.z0;
    w = Math.ceil((b.x1 - b.x0) / cs); h = Math.ceil((b.z1 - b.z0) / cs);
    grid = new Uint8Array(w * h);
  }
  function setColliders(list) { colliders.length = 0; colliders.push(...list); rebuild(); }
  function rebuild(pad = 0.38) {
    grid.fill(0);
    for (const c of colliders) {
      const i0 = Math.max(0, Math.floor((c.x0 - pad - x0) / cs)), i1 = Math.min(w - 1, Math.floor((c.x1 + pad - x0) / cs));
      const j0 = Math.max(0, Math.floor((c.z0 - pad - z0) / cs)), j1 = Math.min(h - 1, Math.floor((c.z1 + pad - z0) / cs));
      for (let j = j0; j <= j1; j++) for (let i = i0; i <= i1; i++) grid[j * w + i] = 1;
    }
  }
  const ci = (x) => Math.floor((x - x0) / cs), cj = (z) => Math.floor((z - z0) / cs);
  const blocked = (i, j) => i < 0 || j < 0 || i >= w || j >= h || grid[j * w + i] === 1;
  const cx = (i) => x0 + (i + 0.5) * cs, cz = (j) => z0 + (j + 0.5) * cs;

  function nearestFree(i, j) {
    if (!blocked(i, j)) return [i, j];
    for (let r = 1; r < 30; r++) {
      for (let dj = -r; dj <= r; dj++) for (let di = -r; di <= r; di++) {
        if (Math.abs(di) !== r && Math.abs(dj) !== r) continue;
        if (!blocked(i + di, j + dj)) return [i + di, j + dj];
      }
    }
    return [i, j];
  }
  function los(ax, az, bx, bz) {
    const d = Math.hypot(bx - ax, bz - az), n = Math.ceil(d / (cs * 0.5));
    for (let k = 1; k < n; k++) {
      const t = k / n, x = ax + (bx - ax) * t, z = az + (bz - az) * t;
      if (blocked(ci(x), cj(z))) return false;
    }
    return true;
  }
  // binary heap keyed by f
  function find(ax, az, bx, bz) {
    let [si, sj] = nearestFree(ci(ax), cj(az));
    let [ti, tj] = nearestFree(ci(bx), cj(bz));
    if (si === ti && sj === tj) return [{ x: bx, z: bz }];
    if (los(ax, az, bx, bz) && !blocked(ci(bx), cj(bz))) return [{ x: bx, z: bz }];
    const N = w * h;
    const g = new Float32Array(N).fill(Infinity), came = new Int32Array(N).fill(-1), closed = new Uint8Array(N);
    const heap = [], hf = [];
    const push = (n, f) => { heap.push(n); hf.push(f); let k = heap.length - 1; while (k > 0) { const p = (k - 1) >> 1; if (hf[p] <= hf[k]) break; [heap[p], heap[k]] = [heap[k], heap[p]]; [hf[p], hf[k]] = [hf[k], hf[p]]; k = p; } };
    const pop = () => { const top = heap[0], last = heap.pop(), lf = hf.pop(); if (heap.length) { heap[0] = last; hf[0] = lf; let k = 0; for (;;) { const l = 2 * k + 1, r = l + 1; let m = k; if (l < heap.length && hf[l] < hf[m]) m = l; if (r < heap.length && hf[r] < hf[m]) m = r; if (m === k) break; [heap[m], heap[k]] = [heap[k], heap[m]]; [hf[m], hf[k]] = [hf[k], hf[m]]; k = m; } } return top; };
    const H = (i, j) => { const dx = Math.abs(i - ti), dz = Math.abs(j - tj); return (dx + dz) + (Math.SQRT2 - 2) * Math.min(dx, dz); };
    const s = sj * w + si, t = tj * w + ti;
    g[s] = 0; push(s, H(si, sj));
    let found = false, iter = 0;
    while (heap.length && iter++ < 20000) {
      const cur = pop();
      if (cur === t) { found = true; break; }
      if (closed[cur]) continue;
      closed[cur] = 1;
      const i = cur % w, j = (cur / w) | 0;
      for (let dj = -1; dj <= 1; dj++) for (let di = -1; di <= 1; di++) {
        if (!di && !dj) continue;
        const ni = i + di, nj = j + dj;
        if (blocked(ni, nj)) continue;
        if (di && dj && (blocked(i + di, j) || blocked(i, j + dj))) continue;
        const n = nj * w + ni;
        if (closed[n]) continue;
        const ng = g[cur] + (di && dj ? Math.SQRT2 : 1);
        if (ng < g[n]) { g[n] = ng; came[n] = cur; push(n, ng + H(ni, nj)); }
      }
    }
    if (!found) return [{ x: bx, z: bz }];
    const cells = [];
    for (let c = t; c !== -1 && c !== s; c = came[c]) cells.push(c);
    cells.reverse();
    const pts = cells.map(c => ({ x: cx(c % w), z: cz((c / w) | 0) }));
    pts[pts.length - 1] = { x: bx, z: bz };
    // string pulling
    const out = [];
    let ax2 = ax, az2 = az, k = 0;
    while (k < pts.length) {
      let far = k;
      for (let m = pts.length - 1; m > k; m--) if (los(ax2, az2, pts[m].x, pts[m].z)) { far = m; break; }
      out.push(pts[far]); ax2 = pts[far].x; az2 = pts[far].z; k = far + 1;
    }
    return out;
  }
  // push a circle out of all colliders (used for the manager and to un-stick staff)
  function resolve(p, r) {
    for (const c of colliders) {
      const nx = U.clamp(p.x, c.x0, c.x1), nz = U.clamp(p.z, c.z0, c.z1);
      const dx = p.x - nx, dz = p.z - nz, d2 = dx * dx + dz * dz;
      if (d2 < r * r) {
        if (d2 > 1e-6) { const d = Math.sqrt(d2), k = (r - d) / d; p.x += dx * k; p.z += dz * k; }
        else { // centre inside: push out along the shallowest axis
          const l = p.x - c.x0, rr = c.x1 - p.x, t = p.z - c.z0, b = c.z1 - p.z, m = Math.min(l, rr, t, b);
          if (m === l) p.x = c.x0 - r; else if (m === rr) p.x = c.x1 + r; else if (m === t) p.z = c.z0 - r; else p.z = c.z1 + r;
        }
      }
    }
  }
  return { init, setColliders, rebuild, find, los, resolve, get colliders() { return colliders; }, blockedAt: (x, z) => blocked(ci(x), cj(z)) };
})();
