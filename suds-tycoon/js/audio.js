// audio.js — every sound is synthesised with WebAudio, so the game ships with no audio files.
const Sfx = (() => {
  let ctx = null, master, sfxBus, musicBus, noiseBuf;
  const on = { sfx: true, music: true };
  const last = {};
  let musicTimer = null, nextNote = 0, step = 0, musicPlaying = false;

  function init() {
    if (ctx) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    ctx = new AC();
    master = ctx.createGain(); master.gain.value = 0.85; master.connect(ctx.destination);
    const comp = ctx.createDynamicsCompressor(); comp.threshold.value = -14; comp.ratio.value = 4; comp.connect(master);
    sfxBus = ctx.createGain(); sfxBus.gain.value = 0.6; sfxBus.connect(comp);
    musicBus = ctx.createGain(); musicBus.gain.value = 0.16; musicBus.connect(comp);
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 1.0, ctx.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  }
  function unlock() {
    init();
    if (ctx && ctx.state === 'suspended') ctx.resume();
    if (on.music && !musicPlaying) startMusic();
  }
  function limit(name, ms) {
    const t = performance.now();
    if (last[name] && t - last[name] < ms) return false;
    last[name] = t; return true;
  }
  function tone(freq, dur, type = 'sine', vol = 0.3, o = {}) {
    if (!ctx || !on.sfx) return;
    const t0 = ctx.currentTime + (o.delay || 0);
    const osc = ctx.createOscillator(), g = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t0);
    if (o.to) osc.frequency.exponentialRampToValueAtTime(Math.max(20, o.to), t0 + (o.slide || dur));
    if (o.detune) osc.detune.value = o.detune;
    const a = o.attack || 0.005;
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(vol, t0 + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    let node = osc;
    if (o.lp) { const f = ctx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = o.lp; osc.connect(f); node = f; }
    node.connect(g); g.connect(o.bus || sfxBus);
    osc.start(t0); osc.stop(t0 + dur + 0.05);
  }
  function noise(dur, vol = 0.2, freq = 1200, type = 'bandpass', o = {}) {
    if (!ctx || !on.sfx) return;
    const t0 = ctx.currentTime + (o.delay || 0);
    const src = ctx.createBufferSource(); src.buffer = noiseBuf;
    const f = ctx.createBiquadFilter(); f.type = type; f.frequency.setValueAtTime(freq, t0); f.Q.value = o.q || 1;
    if (o.to) f.frequency.exponentialRampToValueAtTime(o.to, t0 + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(vol, t0 + (o.attack || 0.01)); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    src.connect(f); f.connect(g); g.connect(sfxBus);
    src.start(t0, Math.random() * 0.5); src.stop(t0 + dur + 0.05);
  }

  const play = {
    coin(n = 0) { if (!limit('coin', 35)) return; const f = 988 * Math.pow(2, Math.min(n, 20) / 12); tone(f, 0.08, 'square', 0.05, { lp: 4000 }); tone(f * 1.5, 0.14, 'triangle', 0.07, { delay: 0.035 }); },
    cash() { if (!limit('cash', 80)) return; noise(0.08, 0.15, 3000, 'highpass'); tone(1568, 0.12, 'square', 0.05, { delay: 0.05, lp: 5000 }); tone(2093, 0.25, 'triangle', 0.09, { delay: 0.1 }); },
    pop() { if (!limit('pop', 40)) return; tone(U.rand(500, 800), 0.07, 'sine', 0.16, { to: 180, slide: 0.06 }); },
    bubble() { if (!limit('bubble', 60)) return; tone(U.rand(300, 500), 0.12, 'sine', 0.1, { to: U.rand(900, 1300), slide: 0.1 }); },
    squeak() { tone(1800, 0.18, 'sine', 0.08, { to: 2700, slide: 0.08 }); tone(2600, 0.12, 'sine', 0.06, { delay: 0.1, to: 1700 }); },
    honk(p = 1) { if (!limit('honk', 250)) return; tone(330 * p, 0.28, 'sawtooth', 0.07, { lp: 1200, detune: 8 }); tone(415 * p, 0.28, 'sawtooth', 0.06, { lp: 1200 }); },
    quack() { tone(560, 0.12, 'sawtooth', 0.07, { to: 380, lp: 1500 }); tone(520, 0.12, 'sawtooth', 0.06, { delay: 0.14, to: 340, lp: 1500 }); },
    siren() { if (!limit('siren', 1200)) return; for (let i = 0; i < 4; i++) tone(i % 2 ? 900 : 700, 0.22, 'triangle', 0.05, { delay: i * 0.22 }); },
    jingle() { if (!limit('jingle', 2500)) return; [784, 988, 1175, 988, 784, 659].forEach((f, i) => tone(f, 0.16, 'triangle', 0.05, { delay: i * 0.14 })); },
    click() { tone(1200, 0.04, 'square', 0.04, { lp: 3000 }); },
    tap() { tone(700, 0.05, 'sine', 0.08, { to: 1000 }); },
    error() { tone(160, 0.16, 'square', 0.06, { lp: 800 }); tone(120, 0.2, 'square', 0.06, { delay: 0.08, lp: 800 }); },
    whoosh() { noise(0.35, 0.12, 400, 'bandpass', { to: 2400, q: 0.8 }); },
    spray() { if (!limit('spray', 220)) return; noise(0.3, 0.06, 2500, 'bandpass', { q: 0.6 }); },
    splash() { if (!limit('splash', 150)) return; noise(0.25, 0.1, 900, 'lowpass', { to: 300 }); },
    fan() { if (!limit('fan', 400)) return; noise(0.45, 0.05, 600, 'bandpass', { to: 900, q: 0.5 }); },
    scrub() { if (!limit('scrub', 260)) return; noise(0.22, 0.06, 1500, 'bandpass', { q: 2 }); },
    build() { [523, 659, 784, 1047].forEach((f, i) => tone(f, 0.18, 'triangle', 0.09, { delay: i * 0.07 })); noise(0.2, 0.08, 300, 'lowpass', { delay: 0.28 }); },
    upgrade() { [659, 880, 1319].forEach((f, i) => tone(f, 0.14, 'square', 0.045, { delay: i * 0.06, lp: 3500 })); tone(1760, 0.3, 'triangle', 0.06, { delay: 0.18 }); },
    levelup() { [523, 659, 784, 1047, 1319, 1568].forEach((f, i) => tone(f, 0.25, 'triangle', 0.09, { delay: i * 0.08 })); tone(2093, 0.6, 'sine', 0.08, { delay: 0.5 }); },
    reward() { [784, 1047, 1319, 1568].forEach((f, i) => tone(f, 0.2, 'triangle', 0.08, { delay: i * 0.06 })); },
    rage() { tone(220, 0.3, 'sawtooth', 0.07, { to: 90, lp: 900 }); noise(0.4, 0.1, 500, 'lowpass', { delay: 0.05 }); },
    poof() { noise(0.35, 0.14, 700, 'lowpass', { to: 200 }); },
    snore() { tone(110, 0.6, 'sawtooth', 0.025, { lp: 400, to: 140, slide: 0.5 }); },
    wake() { tone(600, 0.1, 'square', 0.04, { to: 1200, lp: 2500 }); },
    tick() { if (!limit('tick', 30)) return; tone(1500, 0.025, 'square', 0.03, { lp: 4000 }); },
    pew() { if (!limit('pew', 45)) return; tone(900, 0.08, 'sine', 0.05, { to: 400 }); },
    splat() { if (!limit('splat', 50)) return; noise(0.12, 0.12, 500, 'lowpass', { to: 150 }); },
    boom() { noise(0.6, 0.25, 300, 'lowpass', { to: 60 }); tone(80, 0.5, 'sine', 0.15, { to: 40 }); },
    win() { [523, 659, 784, 659, 784, 1047].forEach((f, i) => tone(f, 0.22, 'square', 0.05, { delay: i * 0.12, lp: 3000 })); },
    lose() { [392, 349, 330, 262].forEach((f, i) => tone(f, 0.3, 'triangle', 0.08, { delay: i * 0.18 })); },
    hire() { [440, 554, 659].forEach((f, i) => tone(f, 0.16, 'triangle', 0.08, { delay: i * 0.08 })); tone(880, 0.3, 'sine', 0.06, { delay: 0.25 }); },
    gem() { [1319, 1760, 2349].forEach((f, i) => tone(f, 0.18, 'sine', 0.07, { delay: i * 0.05 })); },
  };

  // ---------- music: bouncy loop in C major, 112 bpm ----------
  const BPM = 112, S16 = 60 / BPM / 4;
  const chords = [[48, 55, 64, 67], [45, 52, 60, 64], [41, 48, 57, 60], [43, 50, 59, 62]];
  const mel = [72, -1, 76, 79, 76, -1, 74, 72, 69, -1, 72, 74, 72, -1, 67, -1, 65, -1, 69, 72, 69, -1, 67, 65, 67, -1, 71, 74, 72, -1, -1, -1];
  const mtof = (m) => 440 * Math.pow(2, (m - 69) / 12);
  function note(m, t, dur, type, vol) {
    const osc = ctx.createOscillator(), g = ctx.createGain();
    osc.type = type; osc.frequency.setValueAtTime(mtof(m), t);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(vol, t + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    osc.connect(g); g.connect(musicBus); osc.start(t); osc.stop(t + dur + 0.05);
  }
  function scheduler() {
    if (!ctx) return;
    while (nextNote < ctx.currentTime + 0.2) {
      const bar = Math.floor(step / 16) % 4, s = step % 16, ch = chords[bar];
      if (s % 4 === 0) note(ch[0] - 12, nextNote, S16 * 3, 'triangle', 0.5);
      if (s % 4 === 2) note(ch[0], nextNote, S16 * 1.5, 'triangle', 0.25);
      if (s % 4 === 2) { note(ch[2], nextNote, S16 * 1.2, 'square', 0.05); note(ch[3], nextNote, S16 * 1.2, 'square', 0.04); }
      const mm = mel[(step % 32)];
      if (mm > 0 && (Math.floor(step / 32) % 2 === 0 || s % 2 === 0)) note(mm, nextNote, S16 * 1.8, 'sine', 0.16);
      if (s % 8 === 4) { // soft clap
        const src = ctx.createBufferSource(); src.buffer = noiseBuf;
        const f = ctx.createBiquadFilter(); f.type = 'highpass'; f.frequency.value = 2500;
        const g = ctx.createGain(); g.gain.setValueAtTime(0.12, nextNote); g.gain.exponentialRampToValueAtTime(0.0001, nextNote + 0.08);
        src.connect(f); f.connect(g); g.connect(musicBus); src.start(nextNote, Math.random() * 0.5); src.stop(nextNote + 0.1);
      }
      nextNote += S16; step++;
    }
  }
  function startMusic() {
    if (!ctx || musicPlaying) return;
    musicPlaying = true; nextNote = ctx.currentTime + 0.1; step = 0;
    musicTimer = setInterval(scheduler, 50);
  }
  function stopMusic() { musicPlaying = false; if (musicTimer) clearInterval(musicTimer); musicTimer = null; }
  function setSfx(v) { on.sfx = v; }
  function setMusic(v) { on.music = v; if (v) { if (ctx) startMusic(); } else stopMusic(); }
  function duck(v) { if (master) master.gain.setTargetAtTime(v ? 0.0001 : 0.85, ctx.currentTime, 0.05); }

  return { unlock, play, setSfx, setMusic, duck, get ready() { return !!ctx; } };
})();
