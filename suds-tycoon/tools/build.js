// tools/build.js — bundle the game into single HTML files.
//   node tools/build.js
// Outputs:
//   dist/suds-tycoon.html  full standalone page (double-click to play, or upload to a web game portal)
//   dist/artifact.html     the same page without <html>/<head>/<body> wrappers, for hosts that add their own
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');
let html = read('index.html');

// inline the stylesheet
html = html.replace(/<link rel="stylesheet" href="css\/style\.css">/, () => `<style>\n${read('css/style.css')}\n</style>`);
// inline local scripts (keep CDN scripts as they are)
html = html.replace(/<script src="(js\/[^"]+)"><\/script>/g, (_, src) => {
  const code = read(src).replace(/<\/script/gi, '<\\/script');
  return `<script>\n// ---- ${src} ----\n${code}\n</script>`;
});

const dist = path.join(root, 'dist');
fs.mkdirSync(dist, { recursive: true });
fs.writeFileSync(path.join(dist, 'suds-tycoon.html'), html);

// artifact variant: the host supplies doctype/html/head/body plus charset + viewport metas
let art = html
  .replace(/<!doctype html>\s*/i, '')
  .replace(/<html[^>]*>\s*/i, '')
  .replace(/<\/html>\s*/i, '')
  .replace(/<head>\s*/i, '')
  .replace(/<\/head>\s*/i, '')
  .replace(/<body>\s*/i, '')
  .replace(/<\/body>\s*/i, '')
  .replace(/<meta charset="utf-8">\s*/i, '')
  .replace(/<meta name="viewport"[^>]*>\s*/i, '');
// keep <title> first so hosts that scan the first bytes find it
art = art.replace(/(<title>[^<]*<\/title>\s*)/, '');
art = '<title>Suds Tycoon</title>\n' + art;
fs.writeFileSync(path.join(dist, 'artifact.html'), art);

const kb = (f) => (fs.statSync(path.join(dist, f)).size / 1024).toFixed(0) + ' KB';
console.log('built dist/suds-tycoon.html', kb('suds-tycoon.html'), '| dist/artifact.html', kb('artifact.html'));
