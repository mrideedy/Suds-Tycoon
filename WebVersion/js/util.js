// util.js — small shared helpers: math, easing, number formatting, a tiny event bus.
const U = {
  clamp: (v, a, b) => (v < a ? a : v > b ? b : v),
  lerp: (a, b, t) => a + (b - a) * t,
  damp: (a, b, k, dt) => a + (b - a) * (1 - Math.exp(-k * dt)),
  rand: (a, b) => a + Math.random() * (b - a),
  randi: (a, b) => Math.floor(a + Math.random() * (b - a + 1)),
  pick: (arr) => arr[Math.floor(Math.random() * arr.length)],
  chance: (p) => Math.random() < p,
  shuffle(arr) { for (let i = arr.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [arr[i], arr[j]] = [arr[j], arr[i]]; } return arr; },
  easeOutBack(t) { const c1 = 1.70158, c3 = c1 + 1; return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2); },
  easeOutCubic: (t) => 1 - Math.pow(1 - t, 3),
  easeInCubic: (t) => t * t * t,
  easeInOut: (t) => (t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2),
  easeOutElastic(t) { if (t === 0 || t === 1) return t; return Math.pow(2, -10 * t) * Math.sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1; },
  angleLerp(a, b, t) {
    let d = (b - a) % (Math.PI * 2);
    if (d > Math.PI) d -= Math.PI * 2; else if (d < -Math.PI) d += Math.PI * 2;
    return a + d * t;
  },
  dist2(ax, az, bx, bz) { const dx = ax - bx, dz = az - bz; return dx * dx + dz * dz; },
  dist(ax, az, bx, bz) { return Math.sqrt(U.dist2(ax, az, bx, bz)); },
  weighted(list, wf) {
    let tot = 0; for (const it of list) tot += Math.max(0, wf(it));
    let r = Math.random() * tot;
    for (const it of list) { r -= Math.max(0, wf(it)); if (r <= 0) return it; }
    return list[list.length - 1];
  },
  // 2.4K style numbers, matching the reference HUD.
  fmt(n) {
    if (!isFinite(n)) return '0';
    const neg = n < 0; n = Math.abs(n);
    let s;
    if (n < 1000) s = String(Math.floor(n));
    else {
      const units = ['K', 'M', 'B', 'T', 'aa', 'ab', 'ac', 'ad', 'ae', 'af'];
      let u = -1;
      while (n >= 1000 && u < units.length - 1) { n /= 1000; u++; }
      const d = n < 10 ? 2 : n < 100 ? 1 : 0, p = Math.pow(10, d);
      let t = (Math.floor(n * p + 1e-6) / p).toFixed(d);
      if (t.indexOf('.') >= 0) t = t.replace(/0+$/, '').replace(/\.$/, '');
      s = t + units[u];
    }
    return (neg ? '-' : '') + s;
  },
  fmtTime(sec) {
    sec = Math.max(0, Math.ceil(sec));
    const h = Math.floor(sec / 3600), m = Math.floor((sec % 3600) / 60), s = sec % 60;
    if (h > 0) return `${h}h ${m}m`;
    return `${m}:${String(s).padStart(2, '0')}`;
  },
  today() { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; },
  now: () => Date.now(),
  el(tag, cls, html) { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; },
  $(sel) { return document.querySelector(sel); },
  vibrate(ms) { try { if (Store && Store.s && Store.s.settings.vib && navigator.vibrate) navigator.vibrate(ms); } catch (e) { /* not supported */ } },
  reducedMotion: (() => { try { return window.matchMedia('(prefers-reduced-motion: reduce)').matches; } catch (e) { return false; } })(),
};

// Minimal pub/sub so systems can react to each other without hard links.
const Bus = {
  _h: {},
  on(ev, fn) { (this._h[ev] || (this._h[ev] = [])).push(fn); return fn; },
  off(ev, fn) { const a = this._h[ev]; if (a) { const i = a.indexOf(fn); if (i >= 0) a.splice(i, 1); } },
  emit(ev, data) { const a = this._h[ev]; if (a) for (const fn of a.slice()) { try { fn(data); } catch (e) { console.error('[bus]', ev, e); } } },
};
