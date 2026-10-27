// main.js — boot, main loop, autosave, offline earnings, quality auto-detect.
(() => {
  const loadFill = document.getElementById('load-fill');
  const tips = ['Filling the soap tanks...', 'Untangling the brushes...', 'Teaching the dryer to blow...', 'Inflating the tube man...', 'Hiding the pigeons...'];
  let tipI = 0;
  const tipEl = document.getElementById('load-tip');
  const tipTimer = setInterval(() => { tipEl.textContent = tips[++tipI % tips.length]; }, 900);
  const progress = (p) => { loadFill.style.width = Math.round(p * 100) + '%'; };

  async function start(hotData) {
    progress(0.1);
    try { await Promise.race([document.fonts && document.fonts.load('64px "Lilita One"'), new Promise(r => setTimeout(r, 2500))]); } catch (e) { /* fonts optional */ }
    progress(0.3);
    await ICONS.load();
    progress(0.5);
    const { fresh } = Store.load(hotData && hotData.save);
    const s = Store.s;
    const awaySecs = (Date.now() - (s.lastSeen || Date.now())) / 1000;
    s.sessions++;
    Sfx.setSfx(s.settings.sfx); Sfx.setMusic(s.settings.music);
    GFX.init(document.getElementById('stage'));
    if (s.settings.quality !== 'high') GFX.setQuality(s.settings.quality);
    FX.init(GFX.scene);
    progress(0.7);
    await Ads.init();
    Game.load(Math.min(s.loc, s.unlockedLocs - 1));
    progress(0.95);
    UI.init();
    Blaster.refillTickets();
    progress(1);
    clearInterval(tipTimer);
    // tap to start (unlocks audio on every browser)
    const ld = document.getElementById('loading');
    tipEl.outerHTML = `<div class="tap-start">${fresh ? 'Tap to start washing!' : 'Tap to continue'}</div>`;
    const go = () => {
      ld.removeEventListener('pointerdown', go); window.removeEventListener('keydown', go);
      Sfx.unlock(); ld.classList.add('done'); setTimeout(() => ld.remove(), 600);
      afterStart(fresh, awaySecs);
    };
    ld.addEventListener('pointerdown', go); window.addEventListener('keydown', go);
    loop();
    hookHot();
  }

  function afterStart(fresh, awaySecs) {
    const s = Store.s;
    Ads.gameplay(true);
    if (fresh) { UI.toast('Welcome to Suds Tycoon! Follow the arrow.', 'star'); return; }
    const off = Game.offlineEarnings(awaySecs);
    const queue = [];
    if (off && off.amount > 0) queue.push(['offline', off]);
    else if (awaySecs > 600 && Object.keys(Game.ls.pads).length >= 3) queue.push(['noOffline', {}]);
    if (s.level >= CFG.unlocks.daily && s.daily.last !== U.today()) queue.push(['daily', {}]);
    const next = () => { if (!queue.length) return; const [n, o] = queue.shift(); UI.openPanel(n, o); };
    next();
    // chain the next popup when the previous one closes
    const iv = setInterval(() => { if (!UI.panelOpen()) { if (queue.length) next(); else clearInterval(iv); } }, 400);
  }

  // ---------- main loop ----------
  let last = performance.now(), t = 0, fpsT = 0, fpsN = 0, qualityChecked = false;
  const camT = new THREE.Vector3();
  function loop() {
    requestAnimationFrame(frame);
  }
  function frame(now) {
    requestAnimationFrame(frame);
    const dt = Math.min(0.05, Math.max(0, (now - last) / 1000)); last = now; t += dt;
    try {
      if (Blaster.active) {
        Blaster.update(dt);
        if (Blaster.scene) GFX.renderer.render(Blaster.scene, Blaster.camera);
        return;
      }
      if (!Game.paused) {
        Game.update(dt);
        FX.update(dt);
        World.update(dt, t);
      }
      const p = Game.player;
      if (p) {
        const B = CFG.camBounds;
        camT.set(U.clamp(p.pos.x + p.vel.x * 0.25, B.x0, B.x1), 0, U.clamp(p.pos.z + p.vel.z * 0.25 - 1.0, B.z0, B.z1));
        GFX.updateCamera(dt, camT);
      }
      GFX.render();
    } catch (e) {
      console.error(e);
    }
    // auto quality: drop shadows if the first seconds run slowly
    if (!qualityChecked && t > 3) {
      fpsT += dt; fpsN++;
      if (fpsT > 4) {
        qualityChecked = true;
        const fps = fpsN / fpsT;
        if (fps < 32 && Store.s.settings.quality === 'high') { Store.s.settings.quality = 'low'; GFX.setQuality('low'); UI.toast('Switched to low graphics for smoother play', 'sparkles'); }
      }
    }
  }

  // ---------- save on hide / close ----------
  function persist() { if (Game.running) { Game.saveRuntime(); } Store.save(); }
  let hiddenAt = 0;
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) { hiddenAt = Date.now(); persist(); Sfx.duck(true); }
    else {
      Sfx.duck(false);
      const away = (Date.now() - hiddenAt) / 1000;
      if (hiddenAt && away > 60 && Game.running && !Blaster.active) {
        const off = Game.offlineEarnings(away);
        if (off && off.amount > 0 && !UI.panelOpen()) UI.openPanel('offline', off);
      }
      last = performance.now();
    }
  });
  window.addEventListener('pagehide', persist);
  window.addEventListener('beforeunload', persist);

  // ---------- live updates when hosted as an artifact ----------
  function hookHot() {
    const hot = window.claude && window.claude.hot;
    if (hot && typeof hot.snapshot === 'function') {
      try { hot.snapshot(() => { persist(); return { save: Store.serialize() }; }); } catch (e) { /* optional */ }
    }
  }
  const hot = window.claude && window.claude.hot;
  const boot = (data) => start(data || {}).catch(err => { console.error(err); tipEl.textContent = 'Something went wrong while loading. Please reload the page.'; });
  if (hot && typeof hot.ready === 'function') hot.ready(boot);
  else boot(hot && hot.data ? hot.data : {});
})();
