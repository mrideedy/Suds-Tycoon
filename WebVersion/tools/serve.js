// tools/serve.js — zero-dependency static server for local play: `node tools/serve.js [port] [uploadRoot]`
// With an uploadRoot, POST /__upload?dest=relative/path.ext writes the request body under that folder
// (used by export-unity.html to drop models and icons straight into the Unity project).
const http = require('http');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const port = Number(process.argv[2]) || 8765;
const uploadRoot = process.argv[3] ? path.resolve(process.argv[3]) : null;
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json', '.png': 'image/png', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml', '.ico': 'image/x-icon',
  '.webp': 'image/webp', '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.wav': 'audio/wav', '.md': 'text/markdown; charset=utf-8',
};

http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (req.method === 'POST' && url.pathname === '/__upload') {
    if (!uploadRoot) { res.writeHead(403); return res.end('uploads disabled'); }
    const dest = path.normalize(path.join(uploadRoot, url.searchParams.get('dest') || ''));
    if (!dest.startsWith(uploadRoot + path.sep)) { res.writeHead(403); return res.end('bad path'); }
    const chunks = [];
    req.on('data', (c) => chunks.push(c));
    req.on('end', () => {
      fs.mkdirSync(path.dirname(dest), { recursive: true });
      fs.writeFileSync(dest, Buffer.concat(chunks));
      res.writeHead(200, { 'Content-Type': 'text/plain' });
      res.end('ok ' + Buffer.concat(chunks).length);
    });
    return;
  }
  let p = decodeURIComponent(url.pathname);
  if (p.endsWith('/')) p += 'index.html';
  const file = path.normalize(path.join(root, p));
  if (!file.startsWith(root)) { res.writeHead(403); return res.end('Forbidden'); }
  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404); return res.end('Not found'); }
    res.writeHead(200, { 'Content-Type': types[path.extname(file).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    res.end(data);
  });
}).listen(port, () => console.log(`Suds Tycoon running at http://localhost:${port}` + (uploadRoot ? ` (uploads -> ${uploadRoot})` : '')));
