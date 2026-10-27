// tools/set-pad-costs.js — rewrite pad costs in js/config.js from a JSON map: node tools/set-pad-costs.js '{"rinse":20}'
const fs = require('fs');
const path = require('path');
const file = path.join(__dirname, '../js/config.js');
let src = fs.readFileSync(file, 'utf8');
const costs = JSON.parse(process.argv[2]);
for (const [id, v] of Object.entries(costs)) {
  const re = new RegExp(`(\\{ id: '${id}',[^}]*?cost: )\\d+`);
  if (!re.test(src)) { console.log('missing pad', id); continue; }
  src = src.replace(re, `$1${v}`);
}
fs.writeFileSync(file, src);
console.log('updated', Object.keys(costs).length, 'pads');
