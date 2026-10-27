// ui.js — HUD, upgrade cards, panels, toasts, joystick/keyboard input and the quest pointer.
const UI = (() => {
  const $ = (s, r = document) => r.querySelector(s);
  const I = (n, c) => ICONS.html(n, c);
  const fmt = U.fmt;
  let hud, cardsEl, panelLayer, current = null, shownCash = 0, cashAnim = null, freeCardType = null;
  const S = () => Store.s;

  // =====================================================================
  // HUD
  // =====================================================================
  function init() {
    hud = $('#hud');
    hud.innerHTML = `
      <div class="hud-tl">
        <div class="lvl-row"><div class="lvl-badge">${I('level')}<span class="num" id="lvl-num">1</span></div>
          <div class="xp-bar"><div class="xp-fill" id="xp-fill"></div><span class="xp-text num" id="xp-text">0 / 60</span></div></div>
        <div class="quest" id="quest" role="button" tabindex="0"></div>
      </div>
      <div class="hud-tr">
        <div class="cur-pill" id="pill-cash">${I('cash')}<span class="val num" id="cash-val">0</span><button class="plus-btn" id="btn-cash-plus" aria-label="Get free cash">${I('plus')}</button></div>
        <div class="cur-pill" id="pill-gem">${I('gem')}<span class="val num" id="gem-val">0</span><button class="plus-btn" id="btn-gem-plus" aria-label="Get gems">${I('plus')}</button></div>
        <button class="gear-btn" id="btn-settings" aria-label="Settings">${I('gear')}</button>
      </div>
      <div class="side left" id="side-left"></div>
      <div class="side right" id="side-right"></div>
      <div id="streak"></div>
      <div id="event-banner"></div>
      <div class="cards-wrap"><div class="cards" id="cards"></div></div>`;
    hud.hidden = false;
    cardsEl = $('#cards');
    panelLayer = $('#panel-layer');
    $('#btn-cash-plus').onclick = () => openPanel('freecash');
    $('#btn-gem-plus').onclick = () => openPanel('shop', { tab: 'gems' });
    $('#btn-settings').onclick = () => openPanel('settings');
    $('#quest').onclick = () => { if (Game.claimQuest()) Sfx.play.click(); };
    panelLayer.addEventListener('pointerdown', (e) => { if (e.target === panelLayer) closePanel(); });
    buildSide();
    initInput();
    Bus.on('mat:enter', (p) => {
      if (current) return;
      if (p === 'shop' && S().level < CFG.unlocks.shop) { toast(`The Shop opens at level ${CFG.unlocks.shop}`, 'lock'); return; }
      openPanel(p === 'staff' ? 'upgrades' : p === 'office' ? 'upgrades' : p, { tab: p === 'staff' ? 'staff' : p === 'office' ? 'manager' : undefined });
    });
    Bus.on('pad:broke', (pad) => {
      const need = pad.remaining;
      if (need <= Game.rewardFor(3) && Store.caps().freeCash < CFG.ads.limits.freeCash) offerFreeCash(need);
    });
    Bus.on('level', (l) => { buildSide(); });
    shownCash = S().cash;
    refreshAll();
  }
  function refreshAll() { if (!hud) return; cashChanged(false); gemsChanged(); xpChanged(); questChanged(); refreshCards(); refreshBoosts(); buildSide(); }

  function cashChanged(bump) {
    const target = S().cash;
    if (cashAnim) cancelAnimationFrame(cashAnim);
    const el = $('#cash-val'); if (!el) return;
    const step = () => {
      shownCash = U.lerp(shownCash, target, 0.25);
      if (Math.abs(shownCash - target) < Math.max(1, target * 0.001)) shownCash = target;
      el.textContent = fmt(shownCash);
      if (shownCash !== target) cashAnim = requestAnimationFrame(step); else cashAnim = null;
    };
    step();
    if (bump) { const p = $('#pill-cash'); p.classList.remove('bump'); void p.offsetWidth; p.classList.add('bump'); }
    refreshCardsAfford();
  }
  function gemsChanged() { const el = $('#gem-val'); if (el) el.textContent = fmt(S().gems); const p = $('#pill-gem'); if (p) { p.classList.remove('bump'); void p.offsetWidth; p.classList.add('bump'); } }
  function xpChanged() {
    if (!hud) return;
    const s = S(), need = CFG.xpFor(s.level);
    $('#lvl-num').textContent = s.level;
    $('#xp-fill').style.width = Math.min(100, (s.xp / need) * 100) + '%';
    $('#xp-text').textContent = `${Math.floor(s.xp)} / ${need}`;
  }

  // ---------- quest card ----------
  function questChanged() {
    if (!hud) return;
    const q = Game.quest, el = $('#quest'); if (!el || !q) return;
    const r = Game.questReward();
    el.innerHTML = `<div class="q-ico">${I(Game.questIcon(q))}</div>
      <div class="q-body"><div class="q-text" id="q-text">${Game.questText(q)}</div><div class="q-prog"><div id="q-bar"></div></div>
      <div class="q-reward">${I('cash')}${fmt(r.cash)} ${I('gem')}${r.gems}</div></div><div class="q-claim">Claim</div>`;
    questTick();
  }
  function questTick() {
    const el = $('#quest'); if (!el || !Game.quest) return;
    const [p, n] = Game.questProgress();
    const bar = $('#q-bar'); if (bar) bar.style.width = Math.min(100, (p / n) * 100) + '%';
    const done = p >= n;
    if (done !== el.classList.contains('done')) { el.classList.toggle('done', done); if (done) { Sfx.play.reward(); toast('Task complete! Tap it to claim.', 'flag'); } }
  }

  // ---------- side buttons ----------
  function buildSide() {
    const s = S();
    const left = $('#side-left'), right = $('#side-right');
    if (!left) return;
    const btn = (id, icon, label, lockLv) => {
      const locked = lockLv && s.level < lockLv;
      return `<button class="side-btn${locked ? ' locked' : ''}" id="sb-${id}" data-lock="${lockLv || 0}" aria-label="${label}"><span class="disc">${I(icon)}</span><span class="lbl">${label}</span>${I('lock', 'lock')}<span class="dot" hidden></span></button>`;
    };
    left.innerHTML = btn('daily', 'calendar', 'Daily', CFG.unlocks.daily) + btn('spin', 'wheel', 'Spin', CFG.unlocks.spin) + btn('blaster', 'blaster', 'Blaster', CFG.unlocks.blaster) +
      btn('shop', 'bag', 'Shop', CFG.unlocks.shop) + btn('album', 'album', 'Album', CFG.unlocks.album) + btn('map', 'map', 'Map', 0);
    left.querySelectorAll('.side-btn').forEach(b => b.onclick = () => {
      const id = b.id.slice(3), lock = +b.dataset.lock;
      if (lock && S().level < lock) { toast(`Unlocks at level ${lock}`, 'lock'); Sfx.play.error(); return; }
      Sfx.play.click();
      openPanel(id);
    });
    right.innerHTML = `
      <button class="boost-btn" id="bb-income"><span class="disc">${I('income')}</span><span><div class="b-txt">2x Income</div><div class="b-sub" id="bs-income">${I('ad')}5 min</div></span></button>
      <button class="boost-btn" id="bb-speed"><span class="disc">${I('shoe')}</span><span><div class="b-txt">Speed</div><div class="b-sub" id="bs-speed">${I('ad')}3 min</div></span></button>
      <button class="boost-btn" id="bb-turbo"><span class="disc">${I('turbo')}</span><span><div class="b-txt">Turbo Tom</div><div class="b-sub" id="bs-turbo">${I('ad')}Helper</div></span></button>`;
    $('#bb-income').onclick = () => adBoost('income');
    $('#bb-speed').onclick = () => adBoost('speed');
    $('#bb-turbo').onclick = () => {
      const c = S().cool.turbo - Date.now();
      if (c > 0) { toast(`Turbo Tom is resting. Back in ${U.fmtTime(c / 1000)}`, 'turbo'); return; }
      adBoost('turbo');
    };
    refreshBoosts(); tick1s();
  }
  async function adBoost(name) {
    const ok = await Ads.rewarded('boost_' + name);
    if (!ok) return;
    if (name === 'turbo') S().cool.turbo = Date.now() + CFG.ads.turboEvery * 1000;
    Game.addBoost(name, CFG.boosts[name].add);
    Sfx.play.reward();
    toast({ income: '2x income is on!', speed: 'Speed shoes on. Zoom zoom!', turbo: 'Turbo Tom is here to help!' }[name], { income: 'income', speed: 'shoe', turbo: 'turbo' }[name]);
  }
  function refreshBoosts() {
    for (const n of ['income', 'speed']) {
      const el = $('#bs-' + n), b = $('#bb-' + n); if (!el) continue;
      const left = (S().boosts[n] - Date.now()) / 1000;
      b.classList.toggle('active', left > 0);
      el.innerHTML = left > 0 ? `${I('clock')}${U.fmtTime(left)}` : `${I('ad')}${n === 'income' ? '+5 min' : '3 min'}`;
    }
    const tt = $('#bs-turbo'), tb = $('#bb-turbo');
    if (tt) {
      const active = Game.workers && Game.workers.find(w => w.turbo);
      const cool = (S().cool.turbo - Date.now()) / 1000;
      tb.classList.toggle('active', !!active);
      tt.innerHTML = active ? `${I('clock')}${U.fmtTime(active.life)}` : cool > 0 ? `${I('clock')}${U.fmtTime(cool)}` : `${I('ad')}Helper`;
    }
  }
  // once per second: dots, timers, quest progress, tickets
  function tick1s() {
    const s = S();
    refreshBoosts(); questTick();
    const dot = (id, v) => { const d = $('#sb-' + id + ' .dot'); if (d) { d.hidden = !v; if (v) d.textContent = v === true ? '!' : v; } const b = $('#sb-' + id); if (b) b.classList.toggle('attn', !!v); };
    dot('daily', s.level >= CFG.unlocks.daily && s.daily.last !== U.today());
    dot('spin', s.level >= CFG.unlocks.spin && (spinFreeReady() || s.spin.tokens > 0));
    Blaster.refillTickets();
    dot('blaster', s.level >= CFG.unlocks.blaster && s.blaster.tickets > 0 ? s.blaster.tickets : false);
    if (current && current.tick) current.tick();
  }
  const spinFreeReady = () => Date.now() - S().spin.lastFree >= CFG.spinFreeEvery * 1000;

  // =====================================================================
  // Bottom upgrade cards (like the reference: Brush / Water / Dryer / Worker)
  // =====================================================================
  function cardDefs() {
    const out = [];
    for (const t of CFG.slotOrder) if (Game.lanes.some(l => l.stations[t])) out.push({ kind: 'st', id: t });
    if (Game.built('staff')) out.push({ kind: 'tr', id: 'stfSpeed' });
    out.push({ kind: 'more' });
    return out;
  }
  function refreshCards() {
    if (!cardsEl || !Game.L) return;
    const defs = cardDefs();
    const freeReady = Date.now() >= S().cool.freeUpgrade;
    freeCardType = null;
    if (freeReady) {
      const poor = defs.filter(d => d.kind === 'st' && Game.lv(d.id) < CFG.stationMaxLv && !Game.canAfford(Game.stationUpCost(d.id)));
      if (poor.length && S().level >= 2) freeCardType = poor[0].id;
    }
    cardsEl.innerHTML = defs.map(d => {
      if (d.kind === 'more') return `<button class="ucard" data-k="more"><div class="u-ico">${I('rack')}</div><div class="u-name">Upgrades</div><div class="u-lv">All upgrades</div><div class="buy" style="background:linear-gradient(#4f8cff,#1d4fc4)">Open</div></button>`;
      let name, icon, l, max, cost;
      if (d.kind === 'st') { const c = CFG.stations[d.id]; name = c.short; icon = c.icon; l = Game.lv(d.id); max = CFG.stationMaxLv; cost = Game.stationUpCost(d.id); }
      else { const c = CFG.tracks[d.id]; name = 'Worker'; icon = 'worker'; l = Game.trk(d.id) + 1; max = c.max + 1; cost = Game.trackCost(d.id); }
      const isMax = l >= max, free = d.id === freeCardType;
      const tag = Game.bottleneck === d.id ? '<span class="tag slow">SLOW</span>' : '';
      return `<button class="ucard${isMax ? ' max' : ''}${free ? ' free' : ''}" data-k="${d.kind}" data-id="${d.id}">${tag}<div class="u-ico">${I(icon)}</div><div class="u-name">${name}</div><div class="u-lv">Lv. ${l}</div>
        <div class="buy">${isMax ? 'MAX' : free ? `${I('ad')}FREE` : `${I('cash')}<span class="num">${fmt(cost)}</span>`}</div></button>`;
    }).join('');
    cardsEl.querySelectorAll('.ucard').forEach(c => c.onclick = () => onCard(c));
    refreshCardsAfford();
  }
  function refreshCardsAfford() {
    if (!cardsEl) return;
    cardsEl.querySelectorAll('.ucard').forEach(c => {
      const k = c.dataset.k, id = c.dataset.id;
      if (k === 'more' || c.classList.contains('max') || c.classList.contains('free')) return;
      const cost = k === 'st' ? Game.stationUpCost(id) : Game.trackCost(id);
      c.classList.toggle('poor', !Game.canAfford(cost));
    });
  }
  async function onCard(c) {
    const k = c.dataset.k, id = c.dataset.id;
    if (k === 'more') { Sfx.play.click(); openPanel('upgrades'); return; }
    if (c.classList.contains('max')) return;
    if (c.classList.contains('free')) {
      const ok = await Ads.rewarded('free_upgrade');
      if (ok) { S().cool.freeUpgrade = Date.now() + CFG.ads.freeUpgradeEvery * 1000; Game.buyStation(id, true); toast('Free upgrade!', CFG.stations[id].icon); }
      refreshCards(); return;
    }
    const ok = k === 'st' ? Game.buyStation(id) : Game.buyTrack(id);
    if (ok) { refreshCards(); const nc = cardsEl.querySelector(`[data-id="${id}"]`); if (nc) { nc.classList.add('flash'); } }
    else { c.animate([{ transform: 'translateX(0)' }, { transform: 'translateX(-6px)' }, { transform: 'translateX(6px)' }, { transform: 'translateX(0)' }], { duration: 250 }); }
  }

  // =====================================================================
  // Panels
  // =====================================================================
  function panelOpen() { return !!current; }
  function openPanel(name, opts = {}) {
    if (Blaster.active && name !== 'blasterResult') return;
    closePanel(true);
    const P = PANELS[name]; if (!P) return;
    const def = P(opts) || {};
    panelLayer.innerHTML = `<div class="panel${def.wide ? ' wide' : ''}" role="dialog" aria-label="${def.title}">
      <div class="panel-head">${def.icon ? I(def.icon) : ''}<div>${def.title}${def.sub ? `<small>${def.sub}</small>` : ''}</div>
      ${def.noClose ? '' : `<button class="panel-close" aria-label="Close">${I('close')}</button>`}</div>
      ${def.tabs ? `<div class="tabs">${def.tabs.map(t => `<button class="tab${t.id === def.tab ? ' on' : ''}" data-tab="${t.id}">${t.label}</button>`).join('')}</div>` : ''}
      <div class="panel-body"></div></div>`;
    panelLayer.classList.add('on');
    current = { name, def, opts, el: panelLayer.querySelector('.panel'), body: panelLayer.querySelector('.panel-body'), tick: null };
    const close = panelLayer.querySelector('.panel-close');
    if (close) close.onclick = () => { Sfx.play.click(); closePanel(); };
    if (def.tabs) panelLayer.querySelectorAll('.tab').forEach(t => t.onclick = () => { Sfx.play.click(); openPanel(name, Object.assign({}, opts, { tab: t.dataset.tab })); });
    def.render(current.body, current);
    Ads.gameplay(false);
  }
  function rerender() { if (current) { const st = current.body.scrollTop; current.body.innerHTML = ''; current.def.render(current.body, current); current.body.scrollTop = st; } }
  function closePanel(silent) {
    if (!current) return;
    const was = current;
    current = null;
    panelLayer.classList.remove('on'); panelLayer.innerHTML = '';
    Ads.gameplay(true);
    if (!silent) {
      if (was.def.onClose) was.def.onClose();
      if (was.def.breakAfter) Ads.interstitial('panel_close');
    }
  }
  const row = (o) => `<div class="row"><div class="r-ico">${I(o.icon)}</div><div class="r-main"><div class="r-title">${o.title}</div><div class="r-sub">${o.sub || ''}</div>${o.bar != null ? `<div class="r-bar"><div style="width:${(o.bar * 100).toFixed(0)}%"></div></div>` : ''}</div><div>${o.btn || ''}</div></div>`;
  const costBtn = (id, cost, maxed, label) => maxed ? `<button class="btn gold" disabled>MAX</button>` : `<button class="btn${Game.canAfford(cost) ? '' : ' gray'}" data-buy="${id}">${I('cash')}<span class="num">${fmt(cost)}</span>${label || ''}</button>`;
  const gemBtn = (id, cost, maxed, attr = 'data-gem') => maxed ? `<button class="btn gold" disabled>MAX</button>` : `<button class="btn purple${S().gems >= cost ? '' : ' gray'}" ${attr}="${id}">${I('gem')}<span class="num">${cost}</span></button>`;

  const PANELS = {
    upgrades(o) {
      const tabs = [{ id: 'stations', label: 'Wash Line' }, { id: 'racks', label: 'Supplies' }];
      if (Game.built('office')) tabs.push({ id: 'manager', label: 'Manager' }, { id: 'marketing', label: 'Marketing' });
      if (Game.built('staff')) tabs.push({ id: 'staff', label: 'Staff' });
      let tab = o.tab && tabs.find(t => t.id === o.tab) ? o.tab : 'stations';
      return {
        title: 'Upgrades', icon: 'rack', tabs, tab, wide: true,
        render(b, cur) {
          let h = '';
          if (tab === 'stations') {
            for (const t of CFG.slotOrder) {
              if (!Game.lanes.some(l => l.stations[t])) continue;
              const c = CFG.stations[t], l = Game.lv(t), mx = l >= CFG.stationMaxLv;
              const next = mx ? '' : ` → next: +12% value, 5% faster`;
              h += row({ icon: c.icon, title: `${c.name} <span style="color:var(--ink-soft);font-size:14px">Lv ${l}</span>`, sub: `Adds $${fmt(Game.stationValue(t) * Game.L.mult * Game.incomeMult())} per car · ${Game.stationTime(t).toFixed(2)}s${t === Game.bottleneck ? ' · <b style="color:#ff8a1f">bottleneck</b>' : ''}${next}`, bar: l / CFG.stationMaxLv, btn: costBtn('st:' + t, Game.stationUpCost(t), mx) });
            }
            h += `<div class="note">Every 10 levels a machine's value is multiplied by 1.5. The slowest machine sets the pace of the whole line.</div>`;
          } else if (tab === 'racks') {
            for (const s2 of ['soap', 'wax', 'gel']) {
              if (!Game.racks[s2]) continue;
              const r = Game.racks[s2], l = Game.rackLv(s2), mx = l >= CFG.racks[s2].maxLv;
              h += row({ icon: CFG.supplies[s2].icon, title: `${CFG.racks[s2].name} <span style="color:var(--ink-soft);font-size:14px">Lv ${l}</span>`, sub: `Makes one every ${r.time.toFixed(2)}s · holds ${r.cap}`, bar: l / CFG.racks[s2].maxLv, btn: costBtn('rk:' + s2, Game.rackCost(s2), mx) });
            }
          } else {
            const group = tab;
            for (const id of Object.keys(CFG.tracks)) {
              const d = CFG.tracks[id]; if (d.group !== group) continue;
              const l = Game.trk(id), mx = l >= d.max, locked = d.needs && !Game.built(d.needs);
              h += row({ icon: d.icon, title: `${d.name} <span style="color:var(--ink-soft);font-size:14px">${l}/${d.max}</span>`, sub: locked ? 'Needs the VIP Lounge' : (l ? d.desc(l) : 'Not bought yet') + (mx ? '' : ` → ${d.desc(l + 1)}`), bar: l / d.max, btn: locked ? `<button class="btn gray" disabled>${I('lock')}</button>` : costBtn('tr:' + id, Game.trackCost(id), mx) });
            }
            if (group === 'staff') {
              const ws = Game.workers.filter(w => !w.turbo);
              h += `<div class="sect">Your crew</div>` + (ws.length ? ws.map(w => row({ icon: w.role === 'stocker' ? 'worker' : w.role === 'cashier' ? 'pay' : 'polish', title: w.name, sub: `${w.role[0].toUpperCase() + w.role.slice(1)}${w.lane ? ' · Lane 2' : ''}${w.napping ? ' · napping (walk past to wake them)' : ''}` })).join('') : '<div class="note">Hire staff on the HIRE pads around the lot.</div>');
            }
          }
          b.innerHTML = h;
          b.querySelectorAll('[data-buy]').forEach(x => x.onclick = () => {
            const [k, id] = x.dataset.buy.split(':');
            const ok = k === 'st' ? Game.buyStation(id) : k === 'rk' ? Game.buyRack(id) : Game.buyTrack(id);
            if (ok) rerender();
          });
          cur.tick = () => b.querySelectorAll('[data-buy]').forEach(x => {
            const [k, id] = x.dataset.buy.split(':');
            const cost = k === 'st' ? Game.stationUpCost(id) : k === 'rk' ? Game.rackCost(id) : Game.trackCost(id);
            x.classList.toggle('gray', !Game.canAfford(cost));
          });
        },
      };
    },
    shop(o) {
      const tab = o.tab || 'offers';
      return {
        title: 'Shop', icon: 'bag', tabs: [{ id: 'offers', label: 'Offers' }, { id: 'gems', label: 'Gems' }, { id: 'boosts', label: 'Power-ups' }, { id: 'hats', label: 'Hats' }], tab, wide: true,
        render(b) {
          const s = S(); let h = '';
          if (tab === 'offers') {
            for (const p of CFG.iap.filter(x => x.perk)) {
              const owned = s.perks[p.perk];
              h += row({ icon: p.icon, title: p.name, sub: p.desc, btn: owned ? `<button class="btn gold" disabled>Owned</button>` : `<button class="btn blue" data-iap="${p.id}">${p.price}</button>` });
            }
            h += `<div class="note">Purchases are simulated in this build (no real payment). Hook up Google Play Billing, StoreKit or your web store to sell them for real.</div>`;
          } else if (tab === 'gems') {
            const left = CFG.ads.limits.freeGems - Store.caps().freeGems;
            h += row({ icon: 'gem', title: 'Free gems', sub: `Watch an ad for 5 gems · ${left} left today`, btn: `<button class="btn ad${left > 0 ? '' : ' gray'}" data-free-gems="1">+5</button>` });
            for (const p of CFG.iap.filter(x => !x.perk)) h += row({ icon: p.icon, title: p.name, sub: `${p.gems} gems`, btn: `<button class="btn blue" data-iap="${p.id}">${p.price}</button>` });
            h += `<div class="sect">Swap gems for cash</div>`;
            for (const c of CFG.cashPacks) h += row({ icon: c.icon, title: c.name, sub: `$${fmt(Game.rewardFor(c.minutes))} (${c.minutes} min of income)`, btn: gemBtn(c.id, c.gems, false, 'data-cashpack') });
          } else if (tab === 'boosts') {
            for (const id of Object.keys(CFG.gemUps)) {
              const d = CFG.gemUps[id], l = s.gemUp[id], mx = l >= d.max;
              h += row({ icon: d.icon, title: `${d.name} <span style="color:var(--ink-soft);font-size:14px">${l}/${d.max}</span>`, sub: (l ? d.desc(l) : 'Not bought yet') + (mx ? '' : ` → ${d.desc(l + 1)}`), bar: l / d.max, btn: gemBtn(id, d.cost(l), mx) });
            }
            h += `<div class="note">Power-ups are permanent and work in every location.</div>`;
          } else if (tab === 'hats') {
            h += `<div class="grid">` + CFG.hats.map(ht => {
              const own = s.hats.owned.includes(ht.id), on = s.hats.on === ht.id;
              return `<div class="tile${on ? ' today' : ''}">${I('hat_' + ht.id)}<div class="t-name">${ht.name}</div>${on ? '<button class="btn gold" disabled>Wearing</button>' : own ? `<button class="btn blue" data-wear="${ht.id}">Wear</button>` : `<button class="btn purple${s.gems >= ht.cost ? '' : ' gray'}" data-hat="${ht.id}">${I('gem')}${ht.cost}</button>`}</div>`;
            }).join('') + `</div>`;
          }
          b.innerHTML = h;
          b.querySelectorAll('[data-iap]').forEach(x => x.onclick = () => buyIap(x.dataset.iap));
          b.querySelectorAll('[data-free-gems]').forEach(x => x.onclick = async () => {
            if (Store.caps().freeGems >= CFG.ads.limits.freeGems) { toast('Come back tomorrow for more free gems', 'clock'); return; }
            if (await Ads.rewarded('free_gems')) { Store.caps().freeGems++; Game.addGems(5); Sfx.play.gem(); rewardBurst({ gems: 5 }); rerender(); }
          });
          b.querySelectorAll('[data-cashpack]').forEach(x => x.onclick = () => {
            const c = CFG.cashPacks.find(p => p.id === x.dataset.cashpack);
            if (S().gems < c.gems) { Sfx.play.error(); openPanel('shop', { tab: 'gems' }); return; }
            Game.addGems(-c.gems); const v = Game.rewardFor(c.minutes); Game.addCash(v); rewardBurst({ cash: v }); Sfx.play.reward(); rerender();
          });
          b.querySelectorAll('[data-gem]').forEach(x => x.onclick = () => {
            const id = x.dataset.gem, d = CFG.gemUps[id], l = S().gemUp[id];
            if (l >= d.max) return;
            if (S().gems < d.cost(l)) { Sfx.play.error(); toast('Not enough gems', 'gem'); return; }
            Game.addGems(-d.cost(l)); S().gemUp[id]++; Sfx.play.upgrade(); if (id === 'tickets') Blaster.refillTickets(); rerender(); Store.save();
          });
          b.querySelectorAll('[data-hat]').forEach(x => x.onclick = () => {
            const ht = CFG.hats.find(h2 => h2.id === x.dataset.hat);
            if (S().gems < ht.cost) { Sfx.play.error(); toast('Not enough gems', 'gem'); return; }
            Game.addGems(-ht.cost); S().hats.owned.push(ht.id); wearHat(ht.id); rerender(); Store.save();
          });
          b.querySelectorAll('[data-wear]').forEach(x => x.onclick = () => { wearHat(x.dataset.wear); rerender(); });
        },
      };
    },
    freecash() {
      const left = CFG.ads.limits.freeCash - Store.caps().freeCash, v = Game.rewardFor(3);
      return {
        title: 'Free Cash', icon: 'cash',
        render(b) {
          b.innerHTML = `<div class="hero-num">${I('cash')}${fmt(v)}</div><div class="note">Watch a short ad to get 3 minutes of income right now.<br>${left} left today.</div>
            <div class="btn-row"><button class="btn big ad${left > 0 ? '' : ' gray'}" id="fc-go">Get it</button></div>`;
          $('#fc-go', b).onclick = async () => {
            if (Store.caps().freeCash >= CFG.ads.limits.freeCash) { toast('No more free cash today', 'clock'); return; }
            if (await Ads.rewarded('free_cash')) { Store.caps().freeCash++; Game.addCash(v); rewardBurst({ cash: v }); Sfx.play.reward(); closePanel(true); }
          };
        },
      };
    },
    daily() {
      const s = S(), canClaim = s.daily.last !== U.today(), day = s.daily.day % 7;
      return {
        title: 'Daily Rewards', icon: 'calendar', sub: 'Come back every day for bigger rewards', wide: true,
        render(b) {
          const tiles = CFG.daily.map((d, i) => {
            const st = i < day ? 'claimed' : i === day && canClaim ? 'today' : '';
            return `<div class="tile ${st}">${I(dailyIcon(d))}<div class="t-name">Day ${i + 1}</div><div class="t-sub">${dailyLabel(d)}</div></div>`;
          }).join('');
          b.innerHTML = `<div class="grid" style="grid-template-columns:repeat(auto-fill,minmax(88px,1fr))">${tiles}</div>
            <div class="btn-row">${canClaim ? `<button class="btn big" id="dl-claim">Claim</button><button class="btn big ad purple" id="dl-x2">Claim x2</button>` : `<div class="note">Next reward in ${U.fmtTime((new Date().setHours(24, 0, 0, 0) - Date.now()) / 1000)}</div>`}</div>`;
          const claim = (mult) => {
            const d = CFG.daily[day];
            grantReward(d, mult);
            s.daily.last = U.today(); s.daily.day++;
            Store.save(); closePanel(true); tick1s();
          };
          if (canClaim) {
            $('#dl-claim', b).onclick = () => claim(1);
            $('#dl-x2', b).onclick = async () => { if (await Ads.rewarded('daily_x2')) claim(2); };
          }
        },
      };
    },
    spin() {
      const s = S();
      return {
        title: 'Lucky Spin', icon: 'wheel',
        render(b) {
          const free = spinFreeReady(), caps = Store.caps(), adLeft = CFG.ads.limits.spin - caps.spin;
          const segs = CFG.spin;
          const n = segs.length, R = 100;
          const cols = ['#ff4d5a', '#ffc93c', '#46c23a', '#2f8cff', '#b44cff', '#ff8a1f', '#22c7c7', '#ff6fb1'];
          let svg = `<svg viewBox="-110 -110 220 220">`;
          segs.forEach((sg, i) => {
            const a1 = (i / n) * Math.PI * 2 - Math.PI / 2 - Math.PI / n, a2 = ((i + 1) / n) * Math.PI * 2 - Math.PI / 2 - Math.PI / n;
            svg += `<path d="M0 0 L${(Math.cos(a1) * R).toFixed(1)} ${(Math.sin(a1) * R).toFixed(1)} A${R} ${R} 0 0 1 ${(Math.cos(a2) * R).toFixed(1)} ${(Math.sin(a2) * R).toFixed(1)}Z" fill="${cols[i]}" stroke="#fff" stroke-width="2"/>`;
            const am = (a1 + a2) / 2, deg = (am * 180) / Math.PI + 90;
            svg += `<g transform="rotate(${deg.toFixed(1)})"><text x="0" y="-68" text-anchor="middle" font-family="Lilita One, sans-serif" font-size="12" fill="#fff" stroke="rgba(0,0,0,.35)" stroke-width="2.5" paint-order="stroke">${sg.label}</text></g>`;
            svg += `<image href="data:image/svg+xml;charset=utf-8,${encodeURIComponent(ICONS.svg[spinIcon(sg)])}" x="${(Math.cos(am) * 44 - 15).toFixed(1)}" y="${(Math.sin(am) * 44 - 15).toFixed(1)}" width="30" height="30"/>`;
          });
          svg += `</svg>`;
          b.innerHTML = `<div class="wheel-wrap"><svg class="wheel-ptr" viewBox="0 0 40 48"><path d="M4 4 L36 4 L20 44Z" fill="#ff4d5a" stroke="#fff" stroke-width="4" stroke-linejoin="round"/></svg>
            <div class="wheel" id="wheel" style="transform:rotate(${(s.spin.angle || 0)}deg)">${svg}</div><div class="wheel-hub">SPIN</div></div>
            <div class="btn-row">${free || s.spin.tokens > 0 ? `<button class="btn big" id="sp-free">${free ? 'Free spin' : `Spin (${s.spin.tokens} left)`}</button>` : `<button class="btn big ad purple${adLeft > 0 ? '' : ' gray'}" id="sp-ad">Spin</button>`}</div>
            <div class="note" id="sp-note">${free ? 'Your free spin is ready!' : `Free spin in ${U.fmtTime((CFG.spinFreeEvery * 1000 - (Date.now() - s.spin.lastFree)) / 1000)} · ${Math.max(0, adLeft)} ad spins left today`}</div>`;
          let spinning = false;
          const doSpin = (how) => {
            if (spinning) return; spinning = true;
            const pick = U.weighted(segs.map((x, i) => i), i => segs[i].w);
            const base = (s.spin.angle || 0) - ((s.spin.angle || 0) % 360);
            const target = base + 360 * 5 + (360 - (pick * 360 / n));
            s.spin.angle = target;
            const w = $('#wheel', b); w.style.transform = `rotate(${target}deg)`;
            if (how === 'free') s.spin.lastFree = Date.now(); else if (how === 'token') s.spin.tokens--; else Store.caps().spin++;
            let ticks = 0; const tv = setInterval(() => { Sfx.play.tick(); if (++ticks > 26) clearInterval(tv); }, 140);
            setTimeout(() => { grantReward(segs[pick], 1); Store.save(); spinning = false; if (current && current.name === 'spin') rerender(); tick1s(); }, 4300);
          };
          const fb = $('#sp-free', b); if (fb) fb.onclick = () => doSpin(free ? 'free' : 'token');
          const ab = $('#sp-ad', b); if (ab) ab.onclick = async () => { if (Store.caps().spin >= CFG.ads.limits.spin) { toast('No more ad spins today', 'clock'); return; } if (await Ads.rewarded('spin')) doSpin('ad'); };
        },
      };
    },
    album() {
      const s = S();
      const types = Object.keys(CFG.cars);
      const got = types.filter(t => s.album[t]).length;
      return {
        title: 'Car Album', icon: 'album', sub: `${got} of ${types.length} collected`, wide: true,
        render(b) {
          b.innerHTML = `<div class="grid">${types.map(t => { const n = s.album[t] || 0; return `<div class="tile${n ? '' : ' unknown'}">${I(t === 'golden' ? 'golden' : 'car')}<div class="t-name">${n ? CFG.cars[t].name : '???'}</div><div class="t-sub">${n ? `Washed ${n}` : 'Not seen yet'}</div></div>`; }).join('')}</div>
            <div class="note">Every new car type gives 3 gems. Collect all ${types.length} for a 100 gem bonus.</div>
            ${got >= types.length && !s.albumDone ? `<div class="btn-row"><button class="btn big gold" id="al-claim">Claim 100 gems</button></div>` : ''}`;
          const c = $('#al-claim', b); if (c) c.onclick = () => { s.albumDone = true; Game.addGems(100); rewardBurst({ gems: 100 }); rerender(); };
        },
      };
    },
    map() {
      const s = S();
      return {
        title: 'World Map', icon: 'map', sub: 'Every location multiplies your prices',
        render(b) {
          b.innerHTML = CFG.locations.map((L, i) => {
            const unlocked = i < s.unlockedLocs, cur = i === Game.locIdx;
            const ls = s.locs[L.id], doneN = ls ? Object.keys(ls.pads).length : 0;
            return `<div class="row loc-row${cur ? ' current' : ''}${unlocked ? '' : ' locked'}"><div class="r-ico">${I(unlocked ? 'pin' : 'lock')}</div><div class="r-main"><div class="r-title">${L.name}</div>
              <div class="r-sub">Prices x${fmt(L.mult)} · ${doneN}/${CFG.pads.length} built</div><div class="r-bar"><div style="width:${(doneN / CFG.pads.length * 100).toFixed(0)}%"></div></div></div>
              <div>${cur ? '<button class="btn gold" disabled>Here</button>' : unlocked ? `<button class="btn blue" data-go="${i}">Go</button>` : '<button class="btn gray" disabled>Locked</button>'}</div></div>`;
          }).join('') + `<div class="note">Finish the Road Trip pad in a location to unlock the next one.</div>`;
          b.querySelectorAll('[data-go]').forEach(x => x.onclick = () => { const i = +x.dataset.go; closePanel(true); Ads.interstitial('travel').then(() => Game.travel(i)); });
        },
      };
    },
    travel(o) {
      const L = CFG.locations[o.to];
      return {
        title: 'New Location!', icon: 'map', noClose: false,
        render(b) {
          b.innerHTML = `<div class="hero-num" style="color:var(--brand-dark)">${I('pin')}${L.name}</div>
            <div class="note">Prices are x${fmt(L.mult)} here. You start a fresh lot, and your level, gems and power-ups come with you.</div>
            <div class="btn-row"><button class="btn big" id="tr-go">Travel now</button><button class="btn big gray" id="tr-later">Later</button></div>`;
          $('#tr-go', b).onclick = () => { closePanel(true); Ads.interstitial('travel').then(() => Game.travel(o.to)); };
          $('#tr-later', b).onclick = () => closePanel(true);
        },
      };
    },
    finale() {
      return { title: 'Car Wash Legend!', icon: 'trophy', render(b) { b.innerHTML = `<div class="hero-num" style="color:#d48f00">${I('crown')}You did it!</div><div class="note">You built car washes from the suburbs to the Moon. Keep maxing upgrades and Golden Sponges, and watch for new locations in future updates.</div><div class="btn-row"><button class="btn big" id="fn-ok">Keep washing</button></div>`; $('#fn-ok', b).onclick = () => closePanel(true); } };
    },
    settings() {
      const s = S();
      return {
        title: 'Settings', icon: 'gear',
        render(b) {
          const tg = (id, on, label, icon) => `<div class="row"><div class="r-ico" style="background:#2563eb">${I(icon)}</div><div class="r-main"><div class="r-title">${label}</div></div><div><button class="toggle${on ? ' on' : ''}" id="${id}" aria-pressed="${on}" aria-label="${label}"></button></div></div>`;
          b.innerHTML = tg('st-music', s.settings.music, 'Music', 'music') + tg('st-sfx', s.settings.sfx, 'Sound effects', 'sound') + tg('st-vib', s.settings.vib, 'Vibration', 'vibrate') +
            `<div class="row"><div class="r-ico" style="background:#2563eb">${I('sparkles')}</div><div class="r-main"><div class="r-title">Graphics</div><div class="r-sub">Low turns off shadows for older phones</div></div><div><button class="btn ${s.settings.quality === 'high' ? 'blue' : 'gray'}" id="st-q">${s.settings.quality === 'high' ? 'High' : 'Low'}</button></div></div>
            <div class="note">Ads: ${Ads.provider} provider · Version ${CFG.version}</div>
            <div class="btn-row"><button class="btn red" id="st-reset">Reset progress</button></div>
            ${DEV ? `<div class="sect">Developer</div><div class="btn-row"><button class="btn blue" id="dv-cash">+ lots of cash</button><button class="btn purple" id="dv-gems">+500 gems</button><button class="btn" id="dv-pads">Build all pads</button><button class="btn gold" id="dv-lvl">+1 level</button></div>` : ''}`;
          $('#st-music', b).onclick = () => { s.settings.music = !s.settings.music; Sfx.setMusic(s.settings.music); rerender(); };
          $('#st-sfx', b).onclick = () => { s.settings.sfx = !s.settings.sfx; Sfx.setSfx(s.settings.sfx); rerender(); };
          $('#st-vib', b).onclick = () => { s.settings.vib = !s.settings.vib; rerender(); };
          $('#st-q', b).onclick = () => { s.settings.quality = s.settings.quality === 'high' ? 'low' : 'high'; GFX.setQuality(s.settings.quality); rerender(); };
          $('#st-reset', b).onclick = () => openPanel('confirm', { title: 'Reset progress?', text: 'This deletes your car wash, cash, gems and level on this device.', ok: 'Reset', danger: true, cb: () => { Store.wipe(); Store.save(); location.reload(); } });
          if (DEV) {
            $('#dv-cash', b).onclick = () => Game.devGive(Game.rewardFor(60) * 3);
            $('#dv-gems', b).onclick = () => Game.devGive(0, 500);
            $('#dv-pads', b).onclick = () => { closePanel(true); Game.devBuildAll(); };
            $('#dv-lvl', b).onclick = () => Game.addXP(CFG.xpFor(S().level) - S().xp);
          }
          Store.save();
        },
      };
    },
    confirm(o) {
      return {
        title: o.title, icon: 'flag',
        render(b) {
          b.innerHTML = `<div class="note" style="font-size:15px">${o.text}</div><div class="btn-row"><button class="btn big ${o.danger ? 'red' : ''}" id="cf-ok">${o.ok || 'OK'}</button><button class="btn big gray" id="cf-no">Cancel</button></div>`;
          $('#cf-ok', b).onclick = () => { closePanel(true); o.cb(); };
          $('#cf-no', b).onclick = () => closePanel(true);
        },
      };
    },
    offline(o) {
      return {
        title: 'Welcome back!', icon: 'clock', sub: `You were away ${U.fmtTime(o.secs)}`,
        render(b) {
          b.innerHTML = `<div class="note">Your crew kept the bubbles going and earned</div><div class="hero-num">${I('cash')}${fmt(o.amount)}</div>
            ${o.capped ? `<div class="note">Offline earnings stop after ${o.capH}h. The Night Shift Manager power-up raises the limit.</div>` : ''}
            <div class="btn-row"><button class="btn big" id="of-1">Collect</button><button class="btn big ad purple" id="of-2">Collect x2</button></div>`;
          const take = (m) => { Game.addCash(o.amount * m); rewardBurst({ cash: o.amount * m }); Sfx.play.reward(); closePanel(true); };
          $('#of-1', b).onclick = () => take(1);
          $('#of-2', b).onclick = async () => { if (await Ads.rewarded('offline_x2')) take(2); };
        },
      };
    },
    noOffline(o) {
      return { title: 'Welcome back!', icon: 'clock', render(b) { b.innerHTML = `<div class="note" style="font-size:15px">Your lot sat idle while you were away. Hire a <b>Cashier</b> and a <b>Stocker</b> and they'll earn money even when you're not playing.</div><div class="btn-row"><button class="btn big" id="no-ok">Let's go</button></div>`; $('#no-ok', b).onclick = () => closePanel(true); } };
    },
    levelup(o) {
      const unl = Object.entries(CFG.unlocks).filter(([, l]) => l === o.level).map(([k]) => ({ spin: 'Lucky Spin', shop: 'Shop', gift: 'Gift Balloons', blaster: 'Bubble Blaster', album: 'Car Album', golden: 'Golden Cars', rush: 'Rush Hour', daily: 'Daily Rewards' }[k]));
      return {
        title: `Level ${o.level}!`, icon: 'level',
        render(b) {
          b.innerHTML = `<div class="hero-num" style="color:#9126e0">${I('gem')}+${o.gems}</div>${unl.length ? `<div class="note" style="font-size:15px">Unlocked: <b>${unl.join(', ')}</b></div>` : ''}
            <div class="btn-row"><button class="btn big" id="lu-ok">Awesome</button><button class="btn big ad purple" id="lu-x2">+${o.gems} more</button></div>`;
          $('#lu-ok', b).onclick = () => closePanel();
          $('#lu-x2', b).onclick = async () => { if (await Ads.rewarded('level_x2')) { Game.addGems(o.gems); rewardBurst({ gems: o.gems }); Sfx.play.gem(); closePanel(true); } };
        },
        breakAfter: true,
      };
    },
    gift() {
      const v = Game.rewardFor(4);
      return {
        title: 'Gift Balloon!', icon: 'gift',
        render(b) {
          b.innerHTML = `<div class="note" style="font-size:15px">A gift floated over your car wash. Pick one:</div>
            <div class="btn-row"><button class="btn big ad" id="gf-cash">${I('cash')}${fmt(v)}</button><button class="btn big ad purple" id="gf-gems">${I('gem')}5</button></div>`;
          const take = async (k) => { if (await Ads.rewarded('gift_balloon')) { if (k === 'cash') { Game.addCash(v); rewardBurst({ cash: v }); } else { Game.addGems(5); rewardBurst({ gems: 5 }); } Sfx.play.reward(); Game.popBalloon(); closePanel(true); } };
          $('#gf-cash', b).onclick = () => take('cash'); $('#gf-gems', b).onclick = () => take('gems');
        },
      };
    },
    golden(o) {
      return {
        title: 'Golden Car!', icon: 'golden',
        render(b) {
          b.innerHTML = `<div class="note" style="font-size:15px">A golden car is in the queue. It pays <b>x3</b>. Watch an ad to make it pay <b>x10</b>!</div>
            <div class="btn-row"><button class="btn big ad gold" id="gc-go">Make it x10</button><button class="btn big gray" id="gc-no">x3 is fine</button></div>`;
          $('#gc-go', b).onclick = async () => { if (await Ads.rewarded('golden_car')) { o.car.goldenMult = 10; if (o.car.goldLbl) o.car.goldLbl.set(`<div class="st-badge info" style="background:#d99a00">${I('golden')}GOLDEN x10</div>`); Sfx.play.reward(); closePanel(true); } };
          $('#gc-no', b).onclick = () => closePanel(true);
        },
      };
    },
    blaster() { return Blaster.panel(); },
    blasterResult(o) { return Blaster.resultPanel(o); },
  };
  function dailyIcon(d) { return d.kind === 'cash' ? 'cash' : d.kind === 'gems' ? (d.hat ? 'crown' : 'gem') : d.kind === 'boost' ? 'income' : 'wheel'; }
  function dailyLabel(d) { return d.kind === 'cash' ? `$${fmt(Game.rewardFor(d.minutes))}` : d.kind === 'gems' ? `${d.n} gems${d.hat ? ' + hat' : ''}` : d.kind === 'boost' ? `2x for ${d.secs / 60}m` : `${d.n} spins`; }
  function spinIcon(sg) { return sg.kind === 'cash' ? (sg.minutes >= 30 ? 'trophy' : 'cash') : sg.kind === 'gems' ? 'gem' : sg.boost === 'speed' ? 'shoe' : 'income'; }
  function grantReward(d, mult = 1) {
    const s = S();
    if (d.kind === 'cash') { const v = Game.rewardFor(d.minutes) * mult; Game.addCash(v); rewardBurst({ cash: v }); }
    else if (d.kind === 'gems') {
      Game.addGems(d.n * mult); rewardBurst({ gems: d.n * mult });
      if (d.hat) { const next = CFG.hats.find(h => !s.hats.owned.includes(h.id)); if (next) { s.hats.owned.push(next.id); toast(`New hat: ${next.name}! Wear it from the Shop.`, 'hat_' + next.id); } }
    } else if (d.kind === 'boost') { Game.addBoost(d.boost, d.secs * mult); toast(d.boost === 'income' ? '2x income is on!' : 'Speed shoes on!', d.boost === 'income' ? 'income' : 'shoe'); }
    else if (d.kind === 'spins') { s.spin.tokens += d.n * mult; toast(`+${d.n * mult} free spins`, 'wheel'); }
    Sfx.play.reward();
  }
  function wearHat(id) { S().hats.on = id; Game.player.model.setHat(id); Sfx.play.pop(); }
  function buyIap(id) {
    const p = CFG.iap.find(x => x.id === id);
    openPanel('confirm', {
      title: p.name, text: `Simulated purchase of <b>${p.name}</b> for ${p.price}. No money is charged in this build.`, ok: 'Buy (test)',
      cb: () => {
        if (p.gems) { Game.addGems(p.gems); rewardBurst({ gems: p.gems }); }
        if (p.perk) { S().perks[p.perk] = true; if (p.perk === 'noads' || p.perk === 'vip') Ads.banner(false); }
        Sfx.play.reward(); toast(`Thanks! ${p.name} is yours.`, p.icon); Store.save(); refreshCards();
      },
    });
  }
  function offerFreeCash(need) {
    if (current) return;
    toast(`Short on cash? Tap + for free cash`, 'cash');
    const pb = $('#btn-cash-plus'); if (pb) { pb.animate([{ transform: 'scale(1)' }, { transform: 'scale(1.35)' }, { transform: 'scale(1)' }], { duration: 500, iterations: 3 }); }
  }

  // =====================================================================
  // Small UI effects
  // =====================================================================
  function toast(text, icon) {
    const box = $('#toasts');
    const el = U.el('div', 'toast', `${icon ? I(icon) : ''}<span>${text}</span>`);
    box.appendChild(el);
    while (box.children.length > 3) box.firstChild.remove();
    setTimeout(() => { el.classList.add('out'); setTimeout(() => el.remove(), 320); }, 2600);
  }
  function rewardBurst(r) {
    const fly = (icon, toSel, n) => {
      const to = $(toSel); if (!to) return;
      const tr = to.getBoundingClientRect();
      for (let i = 0; i < n; i++) {
        const el = U.el('div', '', I(icon));
        el.style.cssText = `position:absolute;left:${window.innerWidth / 2}px;top:${window.innerHeight / 2}px;width:40px;height:40px;z-index:40;pointer-events:none`;
        el.firstChild.style.cssText = 'width:40px;height:40px';
        document.getElementById('app').appendChild(el);
        const dx = U.rand(-90, 90), dy = U.rand(-90, 60);
        el.animate([
          { transform: 'translate(-50%,-50%) scale(.4)' },
          { transform: `translate(calc(-50% + ${dx}px), calc(-50% + ${dy}px)) scale(1.1)`, offset: 0.35 },
          { transform: `translate(${tr.left + 20 - window.innerWidth / 2 - 20}px, ${tr.top + tr.height / 2 - window.innerHeight / 2 - 20}px) scale(.6)` },
        ], { duration: 900 + i * 40, easing: 'cubic-bezier(.4,0,.2,1)', fill: 'forwards' }).onfinish = () => el.remove();
      }
    };
    if (r.cash) fly('cash', '#pill-cash', 8);
    if (r.gems) fly('gem', '#pill-gem', Math.min(10, 3 + r.gems));
  }
  function levelUp(level, gems) { setTimeout(() => openPanel('levelup', { level, gems }), current ? 0 : 400); buildSide(); }
  function streak(n) {
    const el = $('#streak'); if (!el) return;
    el.classList.toggle('on', n >= 3);
    if (n >= 3) el.innerHTML = `${I('sparkles')}WASH STREAK x${n} · tips +${Math.min(n, 10) * 10}%`;
  }
  let bannerT = null;
  function banner(title, sub, secs) {
    const el = $('#event-banner'); if (!el) return;
    el.innerHTML = `${title}${sub ? `<small>${sub}</small>` : ''}`;
    el.classList.add('on');
    clearTimeout(bannerT);
    bannerT = setTimeout(() => el.classList.remove('on'), Math.min(4, secs || 4) * 1000);
  }
  function goldenOffer(car) {
    toast('A Golden Car arrived! It pays x3.', 'golden');
    setTimeout(() => { if (!current && !car.dead) openPanel('golden', { car }); }, 1200);
  }
  function giftOffer() { openPanel('gift'); }
  const edge = { el: null };
  function edgeArrow(tgt) {
    if (!edge.el) { edge.el = $('#edge-arrow'); edge.el.innerHTML = `<div class="ar">${I('arrow')}</div>`; }
    if (!tgt) { edge.el.classList.remove('on'); return; }
    const p = { x: 0, y: 0, vis: false };
    GFX.project(tgt.clone().setY(1), p);
    const W = GFX.W, H = GFX.H, m = 60;
    const inside = p.vis && p.x > m && p.x < W - m && p.y > m + 60 && p.y < H - 170;
    if (inside) { edge.el.classList.remove('on'); return; }
    const cx = W / 2, cy = H / 2;
    let dx = p.x - cx, dy = p.y - cy;
    if (!p.vis) { dx = -dx; dy = -dy; }
    const k = Math.min((W / 2 - m) / Math.abs(dx || 1e-3), (H / 2 - m - 70) / Math.abs(dy || 1e-3));
    edge.el.style.left = (cx + dx * k) + 'px'; edge.el.style.top = (cy + dy * k) + 'px';
    edge.el.firstChild.style.transform = `rotate(${Math.atan2(dy, dx) + Math.PI / 2}rad)`;
    edge.el.classList.add('on');
  }
  function fade(cb) {
    const f = U.el('div', '');
    f.style.cssText = 'position:absolute;inset:0;z-index:55;background:#2563eb;opacity:0;transition:opacity .35s';
    $('#app').appendChild(f);
    requestAnimationFrame(() => { f.style.opacity = '1'; });
    setTimeout(() => { cb(); setTimeout(() => { f.style.opacity = '0'; setTimeout(() => f.remove(), 400); }, 120); }, 380);
  }
  function onLocation() { refreshAll(); }

  // =====================================================================
  // Input: floating joystick, keyboard, wheel zoom, taps on the world
  // =====================================================================
  const keys = {};
  let joy = null;
  function initInput() {
    const stage = $('#stage'), jEl = $('#joystick'), knob = $('#joy-knob');
    stage.addEventListener('pointerdown', (e) => {
      Sfx.unlock();
      if (Blaster.active || current) return;
      if (joy) return;
      joy = { id: e.pointerId, x0: e.clientX, y0: e.clientY, t0: performance.now(), moved: false };
      try { stage.setPointerCapture(e.pointerId); } catch (er) { /* ignore */ }
      jEl.style.left = e.clientX + 'px'; jEl.style.top = e.clientY + 'px';
    });
    stage.addEventListener('pointermove', (e) => {
      if (!joy || e.pointerId !== joy.id) return;
      const dx = e.clientX - joy.x0, dy = e.clientY - joy.y0, d = Math.hypot(dx, dy), R = 52;
      if (d > 8) { joy.moved = true; jEl.classList.add('on'); }
      const k = d > R ? R / d : 1;
      knob.style.transform = `translate(${dx * k}px, ${dy * k}px)`;
      const m = Math.min(1, d / R);
      setMove(d > 4 ? (dx / d) * m : 0, d > 4 ? (dy / d) * m : 0);
    });
    const end = (e) => {
      if (!joy || e.pointerId !== joy.id) return;
      if (!joy.moved && performance.now() - joy.t0 < 300) worldTap(e.clientX, e.clientY);
      joy = null; jEl.classList.remove('on'); knob.style.transform = ''; setMove(0, 0);
    };
    stage.addEventListener('pointerup', end); stage.addEventListener('pointercancel', end);
    window.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && current && !current.def.noClose) { closePanel(); return; }
      keys[e.key.toLowerCase()] = true; keyMove(); Sfx.unlock();
    });
    window.addEventListener('keyup', (e) => { keys[e.key.toLowerCase()] = false; keyMove(); });
    window.addEventListener('blur', () => { for (const k in keys) keys[k] = false; keyMove(); });
    stage.addEventListener('wheel', (e) => { e.preventDefault(); GFX.cam.zoom = U.clamp(GFX.cam.zoom * (e.deltaY > 0 ? 1.08 : 0.93), GFX.cam.minZoom, GFX.cam.maxZoom); }, { passive: false });
  }
  function keyMove() {
    if (joy) return;
    const x = (keys.d || keys.arrowright ? 1 : 0) - (keys.a || keys.arrowleft ? 1 : 0);
    const y = (keys.s || keys.arrowdown ? 1 : 0) - (keys.w || keys.arrowup ? 1 : 0);
    const d = Math.hypot(x, y) || 1;
    setMove(x / d, y / d);
  }
  function setMove(jx, jy) {
    const yaw = GFX.cam.yaw, c = Math.cos(yaw), s = Math.sin(yaw);
    Input.x = c * jx + s * jy; Input.z = -s * jx + c * jy; Input.active = !!(jx || jy);
    if (Blaster.active) Blaster.input(jx, jy);
  }
  function worldTap(x, y) {
    // wake napping workers, open upgrade panels by tapping machines
    const objs = [];
    for (const w of Game.workers) objs.push(w.model.group);
    for (const l of Game.lanes) for (const st of Object.values(l.stations)) objs.push(st.group);
    for (const k in Game.racks) objs.push(Game.racks[k].model.group);
    const hits = GFX.raycast(x, y, objs);
    if (!hits.length) return;
    let o = hits[0].object;
    while (o && !objs.includes(o)) o = o.parent;
    const w = Game.workers.find(w2 => w2.model.group === o);
    if (w) { if (w.napping) { w.wake(true); toast(`${w.name} is back to work!`, 'worker'); } return; }
    for (const k in Game.racks) if (Game.racks[k].model.group === o) { openPanel('upgrades', { tab: 'racks' }); return; }
    openPanel('upgrades', { tab: 'stations' });
  }

  return {
    init, refreshAll, cashChanged, gemsChanged, xpChanged, questChanged, refreshCards, refreshBoosts, tick1s, openPanel, closePanel, panelOpen,
    toast, rewardBurst, levelUp, streak, banner, goldenOffer, giftOffer, edgeArrow, fade, onLocation, rerender,
    get current() { return current; },
  };
})();
const DEV = (() => { try { return new URLSearchParams(location.search).has('dev') || location.hash === '#dev'; } catch (e) { return false; } })();
