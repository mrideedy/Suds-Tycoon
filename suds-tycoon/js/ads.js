// ads.js — one ad API for the whole game, with swappable providers.
//   Ads.rewarded('placement')    -> Promise<boolean>  (true = reward earned)
//   Ads.interstitial('placement')-> Promise<void>     (respects cooldowns and grace periods)
//   Ads.banner(true|false)
// Provider is picked from CFG.ads.provider (or ?ads=poki etc. in the URL for testing).
// The mock provider simulates real ad flows so every placement can be tested before an SDK is wired in.
const Ads = (() => {
  let provider = null, providerName = 'mock';
  let busy = false, lastInter = 0, lastRewarded = 0;
  const sessionStart = Date.now();
  const log = (...a) => console.info('[ads]', ...a);

  // ---------------- mock provider ----------------
  const creatives = [
    { game: 'Bubble Pop Saga', line: 'Pop 10,000 bubbles. Relax. Repeat.', bg: 'linear-gradient(160deg,#ff8ad1,#7b4dff 60%,#3a2bd8)', icon: 'bubble' },
    { game: 'Mega Mower Tycoon', line: 'Mow lawns. Get rich. Buy a gold mower.', bg: 'linear-gradient(160deg,#9cf06e,#2fae24 60%,#127a2a)', icon: 'trophy' },
    { game: 'Burger Bounce', line: 'Stack the tallest burger in town!', bg: 'linear-gradient(160deg,#ffd36e,#ff8a1f 60%,#e8383d)', icon: 'star' },
    { game: 'Penguin Plumber', line: 'Fix pipes. Save the igloo.', bg: 'linear-gradient(160deg,#9fe6ff,#2f8cff 60%,#1a46b8)', icon: 'water' },
  ];
  function showMock(kind, secs) {
    return new Promise((resolve) => {
      const layer = document.getElementById('ad-layer');
      const c = U.pick(creatives);
      const bubbles = Array.from({ length: 12 }, (_, i) => `<i style="left:${(i * 8.5 + Math.random() * 6).toFixed(1)}%;animation-delay:${(Math.random() * 3).toFixed(2)}s;transform:scale(${(0.5 + Math.random()).toFixed(2)})"></i>`).join('');
      layer.innerHTML = `
        <div class="ad-top"><span><span class="adtag">AD</span> &nbsp;${kind === 'rewarded' ? 'Reward in <b id="ad-count">' + secs + '</b>s' : 'Skip in <b id="ad-count">' + secs + '</b>s'}</span>
          <button class="ad-x" id="ad-close" disabled>${kind === 'rewarded' ? 'Cancel' : '...'}</button></div>
        <div class="ad-creative" style="background:${c.bg}">
          <div class="ad-bubbles">${bubbles}</div>
          ${ICONS.html(c.icon)}
          <div class="ad-game">${c.game}</div><div class="ad-line">${c.line}</div>
          <div class="ad-cta"><span class="btn big gold">Play Free</span></div>
          <div class="ad-progress"><div id="ad-prog"></div></div>
        </div>
        <div class="ad-note">Test ad from the built-in mock provider. Real ads appear once an ad SDK is connected.</div>`;
      layer.querySelector('.ad-creative > .ico').style.cssText = 'width:110px;height:110px;filter:drop-shadow(0 6px 0 rgba(0,0,0,.2))';
      layer.classList.add('on');
      const btn = layer.querySelector('#ad-close'), cnt = layer.querySelector('#ad-count'), prog = layer.querySelector('#ad-prog');
      const t0 = performance.now();
      let done = false;
      if (kind === 'rewarded') { btn.disabled = false; }
      const finish = (ok) => { if (done) return; done = true; clearInterval(iv); layer.classList.remove('on'); layer.innerHTML = ''; resolve(ok); };
      const iv = setInterval(() => {
        const el = (performance.now() - t0) / 1000, left = Math.max(0, Math.ceil(secs - el));
        if (cnt) cnt.textContent = left;
        prog.style.width = Math.min(100, (el / secs) * 100) + '%';
        if (el >= secs) {
          btn.disabled = false;
          btn.textContent = kind === 'rewarded' ? 'Collect reward' : 'Skip';
          btn.onclick = () => finish(true);
          if (cnt) cnt.parentElement.innerHTML = kind === 'rewarded' ? 'Reward earned' : 'You can skip now';
          clearInterval(iv);
        }
      }, 100);
      btn.onclick = () => finish(false);
    });
  }
  const mock = {
    name: 'mock',
    init: async () => true,
    rewarded: () => showMock('rewarded', CFG.ads.mockRewardSecs),
    interstitial: () => showMock('interstitial', CFG.ads.mockInterSecs),
    banner(show) {
      const el = document.getElementById('banner-ad');
      el.hidden = !show;
      document.documentElement.style.setProperty('--banner-h', show ? '50px' : '0px');
      if (show && !el.dataset.filled) {
        el.dataset.filled = '1';
        const lines = ['Banner slot 320x50 · your ad network fills this', 'Love Suds Tycoon? Tell a friend!', 'Remove Ads in the Shop for a clean screen'];
        let i = 0;
        const draw = () => { el.innerHTML = `<span class="adtag">AD</span>${ICONS.html('ad', 'bn-ico')}<span>${lines[i++ % lines.length]}</span>`; };
        draw(); setInterval(draw, 30000);
      }
    },
    gameplay() {},
  };

  // ---------------- real SDK adapters (load the SDK script in index.html first) ----------------
  const crazygames = {
    name: 'crazygames',
    async init() { const sdk = window.CrazyGames && window.CrazyGames.SDK; if (!sdk) throw new Error('CrazyGames SDK missing'); await sdk.init(); this.sdk = sdk; },
    rewarded() { return new Promise(res => this.sdk.ad.requestAd('rewarded', { adStarted() {}, adFinished: () => res(true), adError: () => res(false) })); },
    interstitial() { return new Promise(res => this.sdk.ad.requestAd('midgame', { adStarted() {}, adFinished: () => res(), adError: () => res() })); },
    banner(show) { try { if (show) this.sdk.banner.requestResponsiveBanner('banner-ad'); else this.sdk.banner.clearAllBanners(); document.getElementById('banner-ad').hidden = !show; } catch (e) { log(e); } },
    gameplay(on) { try { on ? this.sdk.game.gameplayStart() : this.sdk.game.gameplayStop(); } catch (e) { /* optional */ } },
  };
  const poki = {
    name: 'poki',
    async init() { if (!window.PokiSDK) throw new Error('Poki SDK missing'); await window.PokiSDK.init(); window.PokiSDK.gameLoadingFinished(); },
    rewarded() { return window.PokiSDK.rewardedBreak().then(Boolean).catch(() => false); },
    interstitial() { return window.PokiSDK.commercialBreak().catch(() => {}); },
    banner() { document.getElementById('banner-ad').hidden = true; }, // Poki serves display ads around the game
    gameplay(on) { try { on ? window.PokiSDK.gameplayStart() : window.PokiSDK.gameplayStop(); } catch (e) { /* optional */ } },
  };
  const gamedistribution = {
    name: 'gamedistribution',
    async init() { if (!window.gdsdk) throw new Error('GameDistribution SDK missing'); },
    rewarded() {
      return new Promise(res => {
        let granted = false;
        const h = (e) => { if (e.detail && e.detail.name === 'SDK_REWARDED_WATCH_COMPLETE') granted = true; };
        window.addEventListener('gd-event', h);
        window.gdsdk.showAd('rewarded').then(() => res(granted)).catch(() => res(false)).finally(() => window.removeEventListener('gd-event', h));
      });
    },
    interstitial() { return window.gdsdk.showAd().catch(() => {}); },
    banner() { document.getElementById('banner-ad').hidden = true; },
    gameplay() {},
  };
  // Capacitor app (Android/iOS) with @capacitor-community/admob. IDs below are Google's public Android TEST units;
  // replace them with your own ad unit IDs before release.
  const admob = {
    name: 'admob',
    ids: { banner: 'ca-app-pub-3940256099942544/6300978111', interstitial: 'ca-app-pub-3940256099942544/1033173712', rewarded: 'ca-app-pub-3940256099942544/5224354917' },
    async init() { const A = window.Capacitor && window.Capacitor.Plugins && window.Capacitor.Plugins.AdMob; if (!A) throw new Error('AdMob plugin missing'); this.A = A; await A.initialize({ initializeForTesting: true }); },
    async rewarded() { try { await this.A.prepareRewardVideoAd({ adId: this.ids.rewarded }); const r = await this.A.showRewardVideoAd(); return !!r; } catch (e) { return false; } },
    async interstitial() { try { await this.A.prepareInterstitial({ adId: this.ids.interstitial }); await this.A.showInterstitial(); } catch (e) { /* no fill */ } },
    banner(show) { try { show ? this.A.showBanner({ adId: this.ids.banner, adSize: 'ADAPTIVE_BANNER', position: 'BOTTOM_CENTER', margin: 0 }) : this.A.hideBanner(); } catch (e) { log(e); } document.getElementById('banner-ad').hidden = true; },
    gameplay() {},
  };
  const all = { mock, crazygames, poki, gamedistribution, admob };

  async function init() {
    let want = CFG.ads.provider;
    try { const q = new URLSearchParams(location.search).get('ads'); if (q && all[q]) want = q; } catch (e) { /* ignore */ }
    provider = all[want] || mock;
    try { await provider.init(); providerName = provider.name; }
    catch (e) { log('falling back to mock:', e.message); provider = mock; providerName = 'mock'; }
    banner(CFG.ads.banner);
  }
  const noAds = () => Store.s.perks.noads || Store.s.perks.vip;

  async function rewarded(placement) {
    if (busy) return false;
    busy = true;
    Bus.emit('ads:pause', true); Sfx.duck(true);
    let ok = false;
    try { ok = await provider.rewarded(placement); } catch (e) { ok = false; }
    busy = false;
    Bus.emit('ads:pause', false); Sfx.duck(false);
    if (ok) lastRewarded = Date.now();
    Bus.emit('ad', { kind: 'rewarded', placement, ok });
    if (!ok) UI && UI.toast && UI.toast('No reward this time. Watch the whole ad to earn it.', 'ad');
    return ok;
  }
  function canInterstitial() {
    if (noAds() || busy) return false;
    if (typeof Input !== 'undefined' && Input.active) return false; // never interrupt a player who is moving
    const now = Date.now();
    if (now - sessionStart < CFG.ads.sessionGrace * 1000) return false;
    if (now - lastInter < CFG.ads.interstitialCooldown * 1000) return false;
    if (now - lastRewarded < CFG.ads.afterRewardedGrace * 1000) return false;
    return true;
  }
  async function interstitial(placement) {
    if (!canInterstitial()) return false;
    busy = true; lastInter = Date.now();
    Bus.emit('ads:pause', true); Sfx.duck(true);
    try { await provider.interstitial(placement); } catch (e) { /* no fill */ }
    busy = false;
    Bus.emit('ads:pause', false); Sfx.duck(false);
    Bus.emit('ad', { kind: 'interstitial', placement });
    return true;
  }
  function banner(show) { if (provider) provider.banner(show && !noAds()); }
  function gameplay(on) { if (provider) provider.gameplay(on); }
  return { init, rewarded, interstitial, banner, gameplay, canInterstitial, get busy() { return busy; }, get provider() { return providerName; } };
})();
