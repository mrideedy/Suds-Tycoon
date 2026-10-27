// icons.js — chunky casual-game icons as inline SVG.
// Used in the HUD (as markup) and drawn onto in-world canvas textures (as preloaded images).
const ICONS = (() => {
  const O = '#172338';
  const S = (inner, vb = '0 0 64 64') => `<svg viewBox="${vb}" xmlns="http://www.w3.org/2000/svg">${inner}</svg>`;
  const st = (w = 3) => `stroke="${O}" stroke-width="${w}" stroke-linejoin="round" stroke-linecap="round"`;

  function starPath(cx, cy, n, r1, r2, rot = -Math.PI / 2) {
    let d = '';
    for (let i = 0; i < n * 2; i++) {
      const r = i % 2 ? r2 : r1, a = rot + (i * Math.PI) / n;
      d += (i ? 'L' : 'M') + (cx + Math.cos(a) * r).toFixed(1) + ' ' + (cy + Math.sin(a) * r).toFixed(1);
    }
    return d + 'Z';
  }
  function gearPath(cx, cy, teeth, rOut, rIn, tw) {
    let d = '';
    for (let i = 0; i < teeth; i++) {
      const a = (i / teeth) * Math.PI * 2, a1 = a - tw, a2 = a + tw;
      const b1 = a + Math.PI / teeth - tw * 0.9, b2 = a - Math.PI / teeth + tw * 0.9;
      const p = (ang, r) => (cx + Math.cos(ang) * r).toFixed(1) + ' ' + (cy + Math.sin(ang) * r).toFixed(1);
      d += (i ? 'L' : 'M') + p(b2, rIn) + 'L' + p(a1, rOut) + 'L' + p(a2, rOut) + 'L' + p(b1, rIn);
    }
    return d + 'Z';
  }
  const wheelSegs = () => {
    const cols = ['#ff4d5a', '#ffc93c', '#46c23a', '#2f8cff', '#b44cff', '#ff8a1f', '#22c7c7', '#ff6fb1'];
    let s = '';
    for (let i = 0; i < 8; i++) {
      const a1 = (i / 8) * Math.PI * 2 - Math.PI / 2, a2 = ((i + 1) / 8) * Math.PI * 2 - Math.PI / 2;
      const p = (a) => (32 + Math.cos(a) * 25).toFixed(1) + ' ' + (34 + Math.sin(a) * 25).toFixed(1);
      s += `<path d="M32 34 L${p(a1)} A25 25 0 0 1 ${p(a2)}Z" fill="${cols[i]}"/>`;
    }
    return s;
  };
  const carPath = 'M6 42 L9 32 Q11 27 17 27 L23 18 Q25 15 30 15 L43 15 Q47 15 49 19 L54 27 Q59 28 59 33 L59 41 Q59 44 56 44 L9 44 Q6 44 6 42Z';
  const head = (vest, capCol, star) => `
    <path d="M9 62 C9 47 19 41 32 41 C45 41 55 47 55 62Z" fill="${vest}" ${st()}/>
    <circle cx="32" cy="28" r="14" fill="#ffcc99" ${st()}/>
    <path d="M17 26 C16 11 48 11 47 26Z" fill="${capCol}" ${st()}/>
    <path d="M31 25 L55 25 C55 29 51 30 45 30 L31 30Z" fill="${capCol === '#2563eb' ? '#1a46b8' : '#c98a00'}" ${st()}/>
    ${star ? `<path d="${starPath(30, 19, 5, 5.5, 2.4)}" fill="#ffd84a" stroke="${O}" stroke-width="1.5"/>` : ''}
    <circle cx="27" cy="31" r="2.2" fill="${O}"/><circle cx="37" cy="31" r="2.2" fill="${O}"/>
    <path d="M28 36 Q32 39 36 36" fill="none" stroke="${O}" stroke-width="2" stroke-linecap="round"/>`;

  const I = {
    cash: S(`<g transform="rotate(-12 32 32)" ${st()}>
        <rect x="7" y="28" width="46" height="22" rx="4" fill="#2f9e26"/>
        <rect x="11" y="17" width="46" height="22" rx="4" fill="#5ad84a"/>
        <circle cx="34" cy="28" r="6.5" fill="#c8f5b5" stroke-width="2.5"/></g>
        <g transform="rotate(-12 32 32)" fill="#2f9e26" opacity=".45"><rect x="15" y="22" width="5" height="12" rx="2"/><rect x="48" y="22" width="5" height="12" rx="2"/></g>`),
    gem: S(`<polygon points="16,13 48,13 59,26 32,57 5,26" fill="#b44cff" ${st()}/>
        <polygon points="16,13 26,26 5,26" fill="#d58cff"/>
        <polygon points="26,26 38,26 32,57" fill="#c362ff"/>
        <polygon points="48,13 59,26 38,26" fill="#9126e0"/>
        <polygon points="16,13 48,13 38,26 26,26" fill="#e7b6ff"/>
        <polygon points="16,13 48,13 59,26 32,57 5,26" fill="none" ${st()}/>`),
    star: S(`<path d="${starPath(32, 33, 5, 27, 12)}" fill="#ffd23f" ${st()}/><path d="${starPath(29, 29, 5, 9, 4)}" fill="#fff3b0" opacity=".7"/>`),
    level: S(`<path d="${starPath(32, 32, 8, 30, 23.5, -Math.PI / 2 + Math.PI / 8)}" fill="#ffc92e" stroke="#9a5a00" stroke-width="3" stroke-linejoin="round"/>
        <circle cx="32" cy="32" r="18" fill="#ffd84d"/><path d="M18 26 A16 16 0 0 1 40 15" stroke="#fff5c4" stroke-width="4" fill="none" stroke-linecap="round" opacity=".8"/>`),
    brush: S(`<rect x="8" y="5" width="48" height="9" rx="3" fill="#a9b4c6" ${st()}/>
        <rect x="9" y="14" width="19" height="45" rx="8" fill="#2f63e6" ${st()}/>
        <rect x="36" y="14" width="19" height="45" rx="8" fill="#2f63e6" ${st()}/>
        <g fill="#86a8ff">${[21, 29, 37, 45].map(y => `<rect x="12" y="${y}" width="13" height="3.5" rx="1.75"/><rect x="39" y="${y}" width="13" height="3.5" rx="1.75"/>`).join('')}</g>`),
    water: S(`<path d="M32 4 C24 18 11 30 11 41 a21 21 0 0 0 42 0 C53 30 40 18 32 4Z" fill="#33a3ff" ${st()}/>
        <path d="M21 41 a11 11 0 0 0 8 12" fill="none" stroke="#c4e8ff" stroke-width="5" stroke-linecap="round"/>`),
    dryer: S(`<circle cx="32" cy="32" r="27" fill="#ff9f1c" ${st()}/><circle cx="32" cy="32" r="19" fill="#6b4413" ${st(2.5)}/>
        ${[0, 72, 144, 216, 288].map(a => `<ellipse cx="32" cy="21.5" rx="5" ry="9.5" fill="#ffd27a" transform="rotate(${a} 32 32)"/>`).join('')}
        <circle cx="32" cy="32" r="5" fill="#fff0cf" ${st(2)}/>`),
    wax: S(`<rect x="20" y="7" width="24" height="10" rx="3" fill="#e0e6ef" ${st()}/>
        <rect x="13" y="15" width="38" height="44" rx="7" fill="#ffc93c" ${st()}/>
        <path d="M32 25 C28 32 23 37 23 42 a9 9 0 0 0 18 0 C41 37 36 32 32 25Z" fill="#ff7a1a" ${st(2.5)}/>
        <rect x="17" y="20" width="5" height="34" rx="2.5" fill="#fff" opacity=".45"/>`),
    tire: S(`<circle cx="32" cy="32" r="27" fill="#2a2e36" ${st()}/>
        ${[0, 45, 90, 135, 180, 225, 270, 315].map(a => `<rect x="30" y="5" width="4" height="7" rx="1" fill="#4a505c" transform="rotate(${a} 32 32)"/>`).join('')}
        <circle cx="32" cy="32" r="14" fill="#c9d1dc" ${st()}/><circle cx="32" cy="32" r="5" fill="#7d8796"/>
        <path d="M13 23 A21 21 0 0 1 25 10" stroke="#ffffff" stroke-width="4" fill="none" stroke-linecap="round" opacity=".85"/>`),
    polish: S(`<path d="M27 5 L32.5 23 L50 28.5 L32.5 34 L27 52 L21.5 34 L4 28.5 L21.5 23Z" fill="#ffe14d" ${st()}/>
        <path d="M48 36 L50.5 44.5 L59 47 L50.5 49.5 L48 58 L45.5 49.5 L37 47 L45.5 44.5Z" fill="#fff6b0" ${st(2.5)}/>`),
    pay: S(`<rect x="15" y="8" width="28" height="17" rx="3" fill="#2a3b55" ${st()}/><rect x="19" y="12" width="20" height="8" rx="2" fill="#5df07a"/>
        <rect x="27" y="24" width="8" height="8" fill="#9aa6b8" ${st(2)}/>
        <rect x="7" y="30" width="50" height="26" rx="5" fill="#4f8cff" ${st()}/>
        <g fill="#d6e4ff">${[13, 24, 35].map(x => `<rect x="${x}" y="35" width="8" height="5" rx="1.5"/><rect x="${x}" y="43" width="8" height="5" rx="1.5"/>`).join('')}</g>
        <rect x="46" y="35" width="6" height="13" rx="2" fill="#ffc93c"/>`),
    worker: S(head('#ff8a1f', '#2563eb', false)),
    manager: S(head('#ffffff', '#2563eb', true)),
    staff: S(`<g transform="translate(-10 4) scale(.82)">${head('#ff8a1f', '#2563eb', false)}</g><g transform="translate(22 8) scale(.82)">${head('#ff8a1f', '#2563eb', false)}</g>`),
    soap: S(`<path d="M44 23 L52 23 Q57 23 57 29 L57 36 Q57 41 52 41 L50 41" fill="none" ${st(3.5)}/>
        <path d="M17 25 L17 54 Q17 59 22 59 L46 59 Q51 59 51 54 L51 25 Q51 18 44 18 L24 18 Q17 18 17 25Z" fill="#ff6fb1" ${st()}/>
        <rect x="26" y="7" width="13" height="11" rx="2" fill="#5ad0ff" ${st()}/>
        <rect x="23" y="30" width="22" height="17" rx="3" fill="#ffffff" ${st(2)}/>
        <circle cx="30" cy="39" r="3.2" fill="#8fdcff"/><circle cx="37" cy="36" r="2.2" fill="#8fdcff"/><circle cx="37" cy="42" r="1.6" fill="#8fdcff"/>`),
    gel: S(`<rect x="19" y="19" width="26" height="40" rx="7" fill="#3ad17a" ${st()}/>
        <rect x="26" y="8" width="12" height="12" rx="2" fill="#2a2e36" ${st()}/>
        <rect x="23" y="29" width="18" height="16" rx="3" fill="#2a2e36"/><circle cx="32" cy="37" r="4.5" fill="#c9d1dc"/><circle cx="32" cy="37" r="1.6" fill="#2a2e36"/>
        <rect x="22" y="23" width="4" height="30" rx="2" fill="#fff" opacity=".35"/>`),
    gear: S(`<path d="${gearPath(32, 32, 8, 28, 21, 0.2)}" fill="#ffffff"/><circle cx="32" cy="32" r="9" fill="#2563eb"/>`),
    calendar: S(`<rect x="7" y="12" width="50" height="46" rx="7" fill="#fff" ${st()}/>
        <path d="M7 19 Q7 12 14 12 L50 12 Q57 12 57 19 L57 27 L7 27Z" fill="#ff4d5a" ${st()}/>
        <rect x="17" y="5" width="6" height="13" rx="3" fill="#a9b4c6" ${st(2)}/><rect x="41" y="5" width="6" height="13" rx="3" fill="#a9b4c6" ${st(2)}/>
        <path d="M20 41 L28 49 L44 33" stroke="#3cb52f" stroke-width="6.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`),
    wheel: S(`${wheelSegs()}<circle cx="32" cy="34" r="25" fill="none" ${st()}/><circle cx="32" cy="34" r="6" fill="#fff" ${st(2.5)}/>
        <path d="M25 3 L39 3 L32 15Z" fill="#ffd23f" ${st(2.5)}/>`),
    bag: S(`<path d="M22 25 L22 18 A10 10 0 0 1 42 18 L42 25" fill="none" ${st(3.5)}/>
        <path d="M11 22 L53 22 L49 59 L15 59Z" fill="#ff7a3a" ${st()}/>
        <path d="${starPath(32, 41, 5, 10, 4.4)}" fill="#ffe14d" ${st(2)}/>`),
    album: S(`<rect x="11" y="6" width="44" height="53" rx="6" fill="#2f8cff" ${st()}/><rect x="11" y="6" width="9" height="53" rx="3" fill="#1a5fd0" ${st()}/>
        <g transform="translate(19 14) scale(.55)"><path d="${carPath}" fill="#fff" ${st(4)}/><circle cx="19" cy="44" r="6" fill="${O}"/><circle cx="46" cy="44" r="6" fill="${O}"/></g>
        <rect x="24" y="44" width="24" height="4" rx="2" fill="#bfe0ff"/><rect x="24" y="51" width="16" height="3.5" rx="1.75" fill="#bfe0ff"/>`),
    map: S(`<path d="M5 14 L22 8 L42 14 L59 8 L59 50 L42 56 L22 50 L5 56Z" fill="#7fd36a" ${st()}/>
        <path d="M22 8 L22 50 M42 14 L42 56" stroke="${O}" stroke-width="2.5"/>
        <path d="M14 40 Q24 30 32 36 T50 26" stroke="#ffffff" stroke-width="3" stroke-dasharray="4 4" fill="none"/>
        <path d="M44 9 a9 9 0 0 1 9 9 c0 7 -9 15 -9 15 s-9 -8 -9 -15 a9 9 0 0 1 9 -9z" fill="#ff4d5a" ${st(2.5)}/><circle cx="44" cy="18" r="3.4" fill="#fff"/>`),
    blaster: S(`<rect x="22" y="4" width="16" height="30" rx="5" fill="#2f63e6" transform="rotate(35 30 30)" ${st()}/>
        <circle cx="26" cy="44" r="15" fill="#a9b4c6" ${st()}/><circle cx="26" cy="44" r="6" fill="#6f7b8e"/>
        <circle cx="49" cy="14" r="9" fill="#bfe8ff" ${st(2.5)}/><circle cx="46" cy="11" r="2.6" fill="#fff"/>
        <circle cx="54" cy="34" r="6" fill="#bfe8ff" ${st(2)}/><circle cx="52.5" cy="32" r="1.8" fill="#fff"/>`),
    ad: S(`<path d="M24 5 L32 13 L40 5" stroke="${O}" stroke-width="3.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
        <rect x="5" y="13" width="54" height="40" rx="8" fill="#2a3b55" ${st()}/><rect x="11" y="19" width="42" height="28" rx="4" fill="#4fc3ff"/>
        <path d="M27 25 L40 33 L27 41Z" fill="#fff"/>`),
    clock: S(`<circle cx="32" cy="34" r="25" fill="#fff" ${st()}/><path d="M32 18 L32 34 L43 41" stroke="${O}" stroke-width="4.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`),
    shoe: S(`<path d="M3 24 L13 24 M1 32 L10 32 M4 40 L11 40" stroke="#ffffff" stroke-width="3.5" stroke-linecap="round"/>
        <path d="M14 40 L17 20 L29 20 Q32 29 40 31 L53 35 Q60 37 60 44 L60 47 L14 47Z" fill="#ff4d5a" ${st()}/>
        <rect x="13" y="45" width="48" height="8" rx="4" fill="#fff" ${st()}/>
        <path d="M30 26 L36 24 M33 30 L39 28" stroke="#fff" stroke-width="2.5" stroke-linecap="round"/>`),
    gift: S(`<rect x="10" y="27" width="44" height="31" rx="4" fill="#ff4d5a" ${st()}/><rect x="6" y="19" width="52" height="12" rx="3" fill="#ff7480" ${st()}/>
        <rect x="28" y="19" width="8" height="39" fill="#ffe14d" ${st(2.5)}/>
        <path d="M32 19 C25 5 13 9 19 17Z M32 19 C39 5 51 9 45 17Z" fill="#ffe14d" ${st(2.5)}/>`),
    lock: S(`<path d="M20 28 L20 20 A12 12 0 0 1 44 20 L44 28" fill="none" ${st(5)}/><rect x="13" y="27" width="38" height="31" rx="6" fill="#ffc93c" ${st()}/><circle cx="32" cy="40" r="4.5" fill="${O}"/><rect x="30" y="41" width="4" height="9" rx="2" fill="${O}"/>`),
    check: S(`<circle cx="32" cy="32" r="27" fill="#46c23a" ${st()}/><path d="M19 33 L28 42 L45 24" stroke="#fff" stroke-width="7" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`),
    close: S(`<path d="M18 18 L46 46 M46 18 L18 46" stroke="#fff" stroke-width="8" stroke-linecap="round"/>`),
    plus: S(`<path d="M32 14 L32 50 M14 32 L50 32" stroke="#fff" stroke-width="10" stroke-linecap="round"/>`),
    arrow: S(`<path d="M32 58 L32 16 M14 32 L32 12 L50 32" stroke="#fff" stroke-width="9" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`),
    sound: S(`<path d="M8 24 L20 24 L34 11 L34 53 L20 40 L8 40Z" fill="#fff" ${st()}/><path d="M42 22 Q48 32 42 42 M49 15 Q60 32 49 49" stroke="#fff" stroke-width="5" fill="none" stroke-linecap="round"/>`),
    music: S(`<path d="M24 46 L24 12 L52 6 L52 40" fill="none" stroke="#fff" stroke-width="5" stroke-linejoin="round"/><ellipse cx="18" cy="47" rx="8" ry="6.5" fill="#fff"/><ellipse cx="46" cy="41" rx="8" ry="6.5" fill="#fff"/>`),
    vibrate: S(`<rect x="20" y="8" width="24" height="48" rx="5" fill="#fff"/><rect x="25" y="14" width="14" height="30" rx="2" fill="#2563eb"/><path d="M10 20 L6 26 L10 32 L6 38 L10 44 M54 20 L58 26 L54 32 L58 38 L54 44" stroke="#fff" stroke-width="3.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`),
    trophy: S(`<path d="M18 8 L46 8 L46 24 A14 14 0 0 1 18 24Z" fill="#ffc92e" ${st()}/><path d="M18 13 L8 13 Q8 27 19 28 M46 13 L56 13 Q56 27 45 28" fill="none" ${st(3.5)}/>
        <rect x="28" y="37" width="8" height="9" fill="#e0a000" ${st(2.5)}/><rect x="18" y="46" width="28" height="10" rx="3" fill="#8a5a33" ${st()}/><path d="M25 14 L25 24" stroke="#fff3b0" stroke-width="4" stroke-linecap="round"/>`),
    crown: S(`<path d="M7 22 L19 35 L32 13 L45 35 L57 22 L52 52 L12 52Z" fill="#ffc92e" ${st()}/><rect x="12" y="48" width="40" height="8" rx="3" fill="#e0a000" ${st()}/>
        <circle cx="7" cy="20" r="4" fill="#ff4d5a" ${st(2)}/><circle cx="32" cy="11" r="4" fill="#2f8cff" ${st(2)}/><circle cx="57" cy="20" r="4" fill="#46c23a" ${st(2)}/>`),
    office: S(`<rect x="22" y="8" width="20" height="12" rx="4" fill="none" ${st(4)}/><rect x="6" y="18" width="52" height="38" rx="6" fill="#a8693a" ${st()}/>
        <rect x="6" y="30" width="52" height="4" fill="#7d4a24"/><rect x="27" y="27" width="10" height="10" rx="2" fill="#ffc92e" ${st(2.5)}/>`),
    megaphone: S(`<path d="M8 26 L20 26 L48 10 L48 54 L20 38 L8 38Z" fill="#ff8a1f" ${st()}/><rect x="14" y="38" width="9" height="16" rx="3" fill="#a9b4c6" ${st(2.5)}/>
        <path d="M54 22 L60 18 M55 32 L62 32 M54 42 L60 46" stroke="#fff" stroke-width="3.5" stroke-linecap="round"/>`),
    coffee: S(`<path d="M44 26 L50 26 Q57 26 57 34 Q57 42 49 42 L44 42" fill="none" ${st(4)}/><path d="M10 22 L46 22 L42 54 Q42 58 38 58 L18 58 Q14 58 14 54Z" fill="#fff" ${st()}/>
        <rect x="10" y="22" width="36" height="6" fill="#8a5a33"/><path d="M20 6 Q16 12 22 15 M30 4 Q26 11 32 14" stroke="#c9d1dc" stroke-width="3.5" fill="none" stroke-linecap="round"/>`),
    bolt: S(`<path d="M36 3 L12 36 L29 36 L24 61 L52 24 L34 24Z" fill="#ffd23f" ${st()}/>`),
    heart: S(`<path d="M32 56 C8 40 4 28 8 19 C12 10 26 8 32 20 C38 8 52 10 56 19 C60 28 56 40 32 56Z" fill="#ff4d5a" ${st()}/><path d="M14 22 Q16 15 23 15" stroke="#fff" stroke-width="4" fill="none" stroke-linecap="round" opacity=".7"/>`),
    bubble: S(`<circle cx="32" cy="32" r="25" fill="#bfe8ff" ${st()}/><circle cx="24" cy="23" r="7" fill="#fff"/><circle cx="40" cy="44" r="3" fill="#fff" opacity=".8"/>`),
    flag: S(`<rect x="12" y="6" width="5" height="54" rx="2.5" fill="#8a5a33" ${st(2.5)}/><path d="M17 9 L54 9 L45 21 L54 33 L17 33Z" fill="#ff4d5a" ${st()}/>`),
    car: S(`<path d="${carPath}" fill="#ff4d5a" ${st()}/><path d="M24 26 L28 19 Q29 18 31 18 L42 18 Q44 18 45 20 L49 26Z" fill="#9fd8ff" ${st(2)}/><circle cx="19" cy="44" r="7" fill="#2a2e36" ${st(2.5)}/><circle cx="46" cy="44" r="7" fill="#2a2e36" ${st(2.5)}/><circle cx="19" cy="44" r="2.5" fill="#c9d1dc"/><circle cx="46" cy="44" r="2.5" fill="#c9d1dc"/>`),
    road: S(`<path d="M22 6 L42 6 L58 58 L6 58Z" fill="#4d525b" ${st()}/><path d="M32 10 L32 18 M32 26 L32 36 M32 44 L32 56" stroke="#fff" stroke-width="4" stroke-linecap="round"/>`),
    tubeman: S(`<path d="M26 58 L28 34 Q22 30 12 20 M36 34 Q44 26 54 14 M28 34 Q30 20 26 8 L38 8 Q36 20 36 34 L38 58" fill="none" stroke="${O}" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"/>
        <path d="M26 58 L28 34 Q22 30 12 20 M36 34 Q44 26 54 14 M28 34 Q30 20 26 8 L38 8 Q36 20 36 34 L38 58" fill="none" stroke="#ff4d5a" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>
        <circle cx="29" cy="14" r="2" fill="#fff"/><circle cx="35" cy="14" r="2" fill="#fff"/>`),
    fountain: S(`<path d="M32 8 Q22 14 18 26 M32 8 Q42 14 46 26" stroke="#4fc3ff" stroke-width="4" fill="none" stroke-linecap="round"/><rect x="29" y="10" width="6" height="26" fill="#d6dbe4" ${st(2.5)}/>
        <ellipse cx="32" cy="44" rx="27" ry="10" fill="#d6dbe4" ${st()}/><ellipse cx="32" cy="42" rx="21" ry="6" fill="#4fc3ff"/>`),
    billboard: S(`<rect x="15" y="40" width="5" height="20" fill="#7d8796" ${st(2.5)}/><rect x="44" y="40" width="5" height="20" fill="#7d8796" ${st(2.5)}/>
        <rect x="5" y="8" width="54" height="34" rx="4" fill="#2563eb" ${st()}/><rect x="11" y="14" width="42" height="22" rx="2" fill="#fff"/><g transform="translate(16 15) scale(.5)"><path d="${carPath}" fill="#ff4d5a"/></g>`),
    flowers: S(`<rect x="8" y="40" width="48" height="18" rx="4" fill="#a8693a" ${st()}/>${[[18, 30, '#ff4d5a'], [32, 24, '#ffd23f'], [46, 30, '#ff6fb1']].map(([x, y, c]) => `<path d="M${x} 42 L${x} ${y + 6}" stroke="#3cb52f" stroke-width="4"/><circle cx="${x}" cy="${y}" r="7" fill="${c}" ${st(2.5)}/><circle cx="${x}" cy="${y}" r="2.5" fill="#fff6b0"/>`).join('')}`),
    vending: S(`<rect x="12" y="4" width="40" height="56" rx="5" fill="#ff4d5a" ${st()}/><rect x="17" y="10" width="22" height="34" rx="2" fill="#bfe8ff" ${st(2)}/>
        ${[16, 24, 32].map(y => `<rect x="20" y="${y}" width="6" height="5" rx="1" fill="#ffc93c"/><rect x="29" y="${y}" width="6" height="5" rx="1" fill="#46c23a"/>`).join('')}
        <rect x="42" y="12" width="6" height="12" rx="1.5" fill="#2a3b55"/><rect x="18" y="49" width="28" height="6" rx="2" fill="#2a3b55"/>`),
    vip: S(`<rect x="6" y="14" width="52" height="38" rx="8" fill="#7b2ff7" ${st()}/><path d="${starPath(32, 33, 5, 14, 6)}" fill="#ffd23f" ${st(2.5)}/>`),
    landmark: S(`<rect x="12" y="46" width="40" height="12" rx="3" fill="#d6dbe4" ${st()}/><g transform="translate(5 14) scale(.85)"><path d="${carPath}" fill="#ffc92e" ${st(3.5)}/><circle cx="19" cy="44" r="6" fill="#e0a000" ${st(2.5)}/><circle cx="46" cy="44" r="6" fill="#e0a000" ${st(2.5)}/></g><path d="M48 6 L50 12 L56 14 L50 16 L48 22 L46 16 L40 14 L46 12Z" fill="#fff6b0"/>`),
    queue: S(`<rect x="6" y="30" width="52" height="8" rx="3" fill="#a8693a" ${st()}/><rect x="6" y="18" width="52" height="8" rx="3" fill="#c9884f" ${st()}/><rect x="12" y="38" width="5" height="18" fill="#7d8796" ${st(2)}/><rect x="47" y="38" width="5" height="18" fill="#7d8796" ${st(2)}/>`),
    lane: S(`<rect x="4" y="16" width="56" height="32" rx="4" fill="#4d525b" ${st()}/><path d="M10 32 L20 32 M28 32 L38 32 M46 32 L56 32" stroke="#ffd23f" stroke-width="4" stroke-linecap="round"/><path d="M4 20 L60 20 M4 44 L60 44" stroke="#fff" stroke-width="2.5"/>`),
    rack: S(`<rect x="6" y="6" width="52" height="54" rx="4" fill="#2f63e6" ${st()}/><rect x="11" y="12" width="42" height="18" fill="#dfe7f5"/><rect x="11" y="36" width="42" height="18" fill="#dfe7f5"/>
        ${[14, 25, 36, 47].map((x, i) => `<rect x="${x}" y="15" width="7" height="15" rx="2" fill="${['#ff6fb1', '#ffc93c', '#3ad17a', '#ff6fb1'][i]}"/><rect x="${x}" y="39" width="7" height="15" rx="2" fill="${['#ffc93c', '#ff6fb1', '#ffc93c', '#3ad17a'][i]}"/>`).join('')}`),
    staffroom: S(`<path d="M6 28 L32 8 L58 28" fill="#2563eb" ${st()}/><rect x="10" y="26" width="44" height="32" fill="#f3dca8" ${st()}/><rect x="26" y="38" width="12" height="20" fill="#2563eb" ${st(2.5)}/>`),
    turbo: S(`<circle cx="32" cy="32" r="27" fill="#ffd23f" ${st()}/><path d="M36 10 L18 36 L30 36 L26 54 L46 28 L34 28Z" fill="#ff8a1f" ${st(2.5)}/>`),
    income: S(`<circle cx="32" cy="32" r="27" fill="#46c23a" ${st()}/><path d="M32 14 L32 50 M40 21 Q36 18 30 18 Q22 18 22 25 Q22 31 32 32 Q42 33 42 40 Q42 46 33 46 Q26 46 22 42" stroke="#fff" stroke-width="5" fill="none" stroke-linecap="round"/>`),
    hat_cap: S(`<path d="M10 40 C10 16 54 16 54 40Z" fill="#2563eb" ${st()}/><path d="M30 38 L60 38 C60 44 54 46 46 46 L30 46Z" fill="#1a46b8" ${st()}/><path d="${starPath(28, 30, 5, 7, 3)}" fill="#ffd84a"/>`),
    hat_cowboy: S(`<ellipse cx="32" cy="44" rx="29" ry="8" fill="#a8693a" ${st()}/><path d="M16 42 C14 20 22 14 26 20 Q32 26 38 20 C42 14 50 20 48 42Z" fill="#c9884f" ${st()}/><rect x="16" y="34" width="32" height="5" fill="#5a3418"/>`),
    hat_crown: S(`<path d="M7 24 L19 36 L32 14 L45 36 L57 24 L52 52 L12 52Z" fill="#ffc92e" ${st()}/><circle cx="32" cy="13" r="4" fill="#ff4d5a" ${st(2)}/>`),
    hat_cone: S(`<path d="M32 4 L50 50 L14 50Z" fill="#ff8a1f" ${st()}/><path d="M24 26 L40 26 L43 34 L21 34Z" fill="#fff"/><rect x="8" y="48" width="48" height="8" rx="3" fill="#ff8a1f" ${st()}/>`),
    hat_chef: S(`<path d="M18 44 L18 30 C8 30 8 14 20 16 C22 6 42 6 44 16 C56 14 56 30 46 30 L46 44Z" fill="#fff" ${st()}/><rect x="17" y="42" width="30" height="12" rx="3" fill="#eef1f6" ${st()}/>`),
    hat_bubble: S(`<circle cx="32" cy="32" r="26" fill="#bfe8ff" fill-opacity=".85" ${st()}/><circle cx="22" cy="22" r="6" fill="#fff"/><rect x="12" y="50" width="40" height="8" rx="3" fill="#a9b4c6" ${st()}/>`),
    hat_duck: S(`<ellipse cx="30" cy="44" rx="22" ry="12" fill="#ffd23f" ${st()}/><circle cx="38" cy="24" r="12" fill="#ffd23f" ${st()}/><path d="M48 24 L60 27 L48 30Z" fill="#ff8a1f" ${st(2.5)}/><circle cx="41" cy="21" r="2.5" fill="${O}"/>`),
    speech: S(`<path d="M8 10 L56 10 Q60 10 60 14 L60 40 Q60 44 56 44 L26 44 L14 56 L16 44 L8 44 Q4 44 4 40 L4 14 Q4 10 8 10Z" fill="#fff" ${st()}/>`),
    camera_zoom: S(`<circle cx="27" cy="27" r="17" fill="#fff" ${st()}/><path d="M39 39 L56 56" ${st(7)}/>`),
    rush: S(`<path d="${carPath}" fill="#ff8a1f" ${st()}/><path d="M1 22 L12 22 M0 30 L8 30 M3 38 L10 38" stroke="#fff" stroke-width="3.5" stroke-linecap="round"/>`),
    golden: S(`<path d="${carPath}" fill="#ffc92e" ${st()}/><path d="M24 26 L28 19 Q29 18 31 18 L42 18 Q44 18 45 20 L49 26Z" fill="#fff6b0" ${st(2)}/><circle cx="19" cy="44" r="7" fill="#e0a000" ${st(2.5)}/><circle cx="46" cy="44" r="7" fill="#e0a000" ${st(2.5)}/><path d="M54 4 L56 10 L62 12 L56 14 L54 20 L52 14 L46 12 L52 10Z" fill="#fff6b0"/>`),
    zzz: S(`<path d="M10 40 L26 40 L10 58 L26 58 M30 16 L44 16 L30 32 L44 32 M46 4 L56 4 L46 14 L56 14" stroke="#fff" stroke-width="5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`),
    angry: S(`<circle cx="32" cy="32" r="27" fill="#ff5a3c" ${st()}/><path d="M17 22 L27 27 M47 22 L37 27" ${st(4)}/><circle cx="24" cy="31" r="3.5" fill="${O}"/><circle cx="40" cy="31" r="3.5" fill="${O}"/><path d="M22 46 Q32 38 42 46" fill="none" ${st(4)}/>`),
    meh: S(`<circle cx="32" cy="32" r="27" fill="#ffd23f" ${st()}/><circle cx="24" cy="28" r="3.5" fill="${O}"/><circle cx="40" cy="28" r="3.5" fill="${O}"/><path d="M22 43 L42 43" ${st(4)}/>`),
    happy: S(`<circle cx="32" cy="32" r="27" fill="#ffd23f" ${st()}/><circle cx="24" cy="27" r="3.5" fill="${O}"/><circle cx="40" cy="27" r="3.5" fill="${O}"/><path d="M20 38 Q32 50 44 38" fill="none" ${st(4)}/>`),
    love: S(`<circle cx="32" cy="32" r="27" fill="#ffd23f" ${st()}/><path d="M24 33 C14 26 16 18 21 19 C23 19.5 24 21 24 22 C24 21 25 19.5 27 19 C32 18 34 26 24 33Z M40 33 C30 26 32 18 37 19 C39 19.5 40 21 40 22 C40 21 41 19.5 43 19 C48 18 50 26 40 33Z" fill="#ff4d5a"/><path d="M20 40 Q32 52 44 40Z" fill="${O}"/>`),
    rage: S(`<circle cx="32" cy="32" r="27" fill="#e0242f" ${st()}/><path d="M15 21 L28 27 M49 21 L36 27" ${st(4.5)}/><circle cx="24" cy="31" r="3" fill="#fff"/><circle cx="40" cy="31" r="3" fill="#fff"/><path d="M20 47 L26 43 L32 47 L38 43 L44 47" fill="none" stroke="#fff" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"/>`),
    honk: S(`<path d="M6 20 L60 20 L52 44 L14 44Z" fill="#ffd23f" ${st()}/>`),
    nosoap: S(`<circle cx="32" cy="32" r="27" fill="#ff4d5a" ${st()}/><path d="M32 16 L32 36" stroke="#fff" stroke-width="7" stroke-linecap="round"/><circle cx="32" cy="47" r="4.5" fill="#fff"/>`),
    hand: S(`<path d="M22 58 L18 36 Q16 30 21 30 Q25 30 26 35 L26 14 Q26 9 30.5 9 Q35 9 35 14 L35 30 L35 26 Q35 22 39 22 Q43 22 43 26 L43 31 Q43 27 47 27 Q51 27 51 31 L51 44 Q51 52 46 58Z" fill="#ffcc99" ${st()}/>`),
    pin: S(`<path d="M32 4 a17 17 0 0 1 17 17 c0 13 -17 38 -17 38 s-17 -25 -17 -38 a17 17 0 0 1 17 -17z" fill="#ff4d5a" ${st()}/><circle cx="32" cy="21" r="7" fill="#fff"/>`),
    sparkles: S(`<path d="M24 6 L29 22 L45 27 L29 32 L24 48 L19 32 L3 27 L19 22Z" fill="#ffe14d" ${st(2.5)}/><path d="M46 34 L48.5 42 L56.5 44.5 L48.5 47 L46 55 L43.5 47 L35.5 44.5 L43.5 42Z" fill="#fff" ${st(2)}/>`),
    planet: S(`<circle cx="32" cy="32" r="18" fill="#b44cff" ${st()}/><ellipse cx="32" cy="34" rx="29" ry="8" fill="none" stroke="#ffd23f" stroke-width="4" transform="rotate(-15 32 32)"/>`),
  };

  // Preload as <img> so canvas textures can draw them synchronously later.
  const imgs = {};
  function load() {
    const names = Object.keys(I);
    return Promise.all(names.map(n => new Promise(res => {
      const im = new Image();
      im.onload = () => { imgs[n] = im; res(); };
      im.onerror = () => res();
      im.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(I[n]);
    })));
  }
  function html(name, cls = '') {
    return `<span class="ico ${cls}" aria-hidden="true">${I[name] || I.star}</span>`;
  }
  return { svg: I, imgs, load, html, starPath };
})();
