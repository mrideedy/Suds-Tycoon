// config.js — every tunable number and every piece of content lives here.
const CFG = {
  version: '1.0.0',
  saveKey: 'sudsTycoon.save.v1',

  // ---------- Lot layout (world units ~ metres; +x east, +z south/toward camera) ----------
  // Hoppers, pay booths and racks sit on the south (camera) side of each lane so they are never hidden.
  laneZ: [0, 13],
  slotOrder: ['brush', 'rinse', 'wax', 'dryer', 'tire', 'polish', 'pay'],
  slotX: { brush: -12, rinse: -7, wax: -2, dryer: 3, tire: 8, polish: 13, pay: 18 },
  entranceX: -16.4,
  exitX: 22.3,
  queueHeadX: -20.4,      // where the first waiting car stops
  queueGap: 5.5,
  hopperDZ: 2.75, matDZ: 3.95,          // hopper and its drop-off mat, relative to the lane
  boothDZ: 2.95, cashierDZ: 3.15,      // pay booth and where the cashier stands
  cashDX: 2.7, cashDZ: 3.55,            // cash pile next to the booth
  polishDZ: 1.85,                       // polisher stands beside the car
  lotBounds: { x0: -25, x1: 27.5, z0: -13.6, z1: 23.5 },
  walkBounds: { x0: -24.4, x1: 27.0, z0: -13.0, z1: 23.0 },
  camBounds: { x0: -21, x1: 24, z0: -8, z1: 19 },
  spawnX: -95,
  despawnX: 95,
  roadSpeed: 10,
  lineSpeed: 3.6,
  hopperCap: 8,
  playerStart: [-9, 5.0],

  racksPos: { soap: [-12, 7.5], wax: [-4, 7.5], gel: [4, 7.5] },
  rackMatDZ: -1.35,                     // mat on the north face of each rack
  buildings: {
    shop:   { pos: [4.5, -10.0], mat: [2.0, -5.5] },
    staff:  { pos: [-17.5, -9.6], mat: [-17.5, -5.9] },
    office: { pos: [-9.5, -9.6], mat: [-9.5, -6.0] },
    vip:    { pos: [18.5, -9.6] },
    coffee: { pos: [-13.4, -6.6] },
    vending: { pos: [-21.0, 4.3] },
    queue:  { pos: [-22.0, -4.3] },
    tubeman: { pos: [-20.5, 6.9] },
    fountain: { pos: [-2.5, 20.4] },
    billboard: { pos: [-19.0, 20.9] },
    flowers: { pos: [6.5, 20.4] },
    landmark: { pos: [14.2, 20.6] },
    travel: { pos: [22.5, 20.4] },
  },

  // ---------- Wash line stations ----------
  stations: {
    brush:  { name: 'Foam Brush',  short: 'Brush',  icon: 'brush',  value: 6,  time: 3.0, supply: 'soap', upCost: 40 },
    rinse:  { name: 'Rinse Arch',  short: 'Water',  icon: 'water',  value: 4,  time: 2.4, upCost: 55 },
    wax:    { name: 'Hot Wax',     short: 'Wax',    icon: 'wax',    value: 8,  time: 3.0, supply: 'wax',  upCost: 180 },
    dryer:  { name: 'Turbo Dryer', short: 'Dryer',  icon: 'dryer',  value: 4,  time: 2.4, upCost: 110 },
    tire:   { name: 'Tire Shine',  short: 'Tires',  icon: 'tire',   value: 8,  time: 2.6, supply: 'gel',  upCost: 380 },
    polish: { name: 'Polish Bay',  short: 'Polish', icon: 'polish', value: 16, time: 3.5, operator: 'polisher', upCost: 900 },
    pay:    { name: 'Pay Booth',   short: 'Cashier', icon: 'pay',   value: 0,  time: 1.25, operator: 'cashier', upCost: 90 },
  },
  stationMaxLv: 30,
  upGrowth: 1.15,

  supplies: {
    soap: { name: 'Soap', icon: 'soap', color: 0xff6fb1 },
    wax:  { name: 'Wax', icon: 'wax', color: 0xffc93c },
    gel:  { name: 'Tire Gel', icon: 'gel', color: 0x3ad17a },
  },
  racks: {
    soap: { name: 'Soap Rack', supply: 'soap', time: 2.0, cap: 6, upCost: 60, maxLv: 15 },
    wax:  { name: 'Wax Rack',  supply: 'wax',  time: 2.4, cap: 6, upCost: 320, maxLv: 15 },
    gel:  { name: 'Gel Rack',  supply: 'gel',  time: 2.4, cap: 6, upCost: 900, maxLv: 15 },
  },

  // ---------- Per-location upgrade tracks ----------
  tracks: {
    mgrSpeed:  { group: 'manager', name: 'Walk Speed',   icon: 'shoe',    max: 10, cost: 120, growth: 1.55, desc: l => `+${Math.round((l) * 7)}% walk speed` },
    mgrCarry:  { group: 'manager', name: 'Carry Size',   icon: 'soap',    max: 10, cost: 150, growth: 1.55, desc: l => `Carry ${4 + l} items` },
    mgrMagnet: { group: 'manager', name: 'Cash Magnet',  icon: 'cash',    max: 5,  cost: 300, growth: 1.9,  desc: l => `Grab cash from ${(2.3 + l * 0.5).toFixed(1)} m` },
    stfSpeed:  { group: 'staff',   name: 'Staff Speed',  icon: 'shoe',    max: 10, cost: 200, growth: 1.5,  desc: l => `+${l * 8}% staff speed` },
    stfCarry:  { group: 'staff',   name: 'Staff Carry',  icon: 'soap',    max: 10, cost: 240, growth: 1.5,  desc: l => `Staff carry ${2 + l} items` },
    stfCoffee: { group: 'staff',   name: 'Strong Coffee', icon: 'coffee', max: 5,  cost: 500, growth: 1.9,  desc: l => `${l * 15}% fewer naps` },
    mktArrive: { group: 'marketing', name: 'Flyers',     icon: 'megaphone', max: 10, cost: 180, growth: 1.6, desc: l => `+${Math.round((1 / Math.pow(0.92, l) - 1) * 100)}% more cars` },
    mktPrice:  { group: 'marketing', name: 'Premium Prices', icon: 'income', max: 10, cost: 400, growth: 1.65, desc: l => `+${l * 10}% prices` },
    mktTips:   { group: 'marketing', name: 'Free Air Fresheners', icon: 'heart', max: 5, cost: 350, growth: 1.8, desc: l => `+${l * 5}% tip chance` },
    mktPatience: { group: 'marketing', name: 'Comfy Seats', icon: 'clock', max: 5, cost: 300, growth: 1.8, desc: l => `+${l * 4}s driver patience` },
    mktVip:    { group: 'marketing', name: 'VIP Invites', icon: 'vip',     max: 5, cost: 2500, growth: 1.8, needs: 'vip', desc: l => `+${l * 4}% VIP cars` },
  },

  // ---------- Build pads (template, costs are multiplied per location) ----------
  // kind: station | rack | hire | building | lane | travel
  pads: [
    { id: 'rinse',     kind: 'station', st: 'rinse', lane: 0, cost: 20,    req: [] },
    { id: 'cashier1',  kind: 'hire', role: 'cashier', lane: 0, cost: 50, req: ['rinse'], pos: [15.2, 5.4] },
    { id: 'dryer',     kind: 'station', st: 'dryer', lane: 0, cost: 80,    req: ['cashier1'] },
    { id: 'staff',     kind: 'building', b: 'staff',  cost: 120,  req: ['dryer'], name: 'Staff Room', icon: 'staffroom', pos: [-17.5, -6.4] },
    { id: 'stocker1',  kind: 'hire', role: 'stocker', cost: 110,   req: ['staff'], pos: [-8, 5.0] },
    { id: 'office',    kind: 'building', b: 'office', cost: 200,  req: ['stocker1'], name: 'Manager Office', icon: 'office', pos: [-9.5, -6.6] },
    { id: 'queue',     kind: 'building', b: 'queue',  cost: 220,  req: ['office'], name: 'Waiting Area', icon: 'queue', pos: [-22.0, -4.3] },
    { id: 'waxRack',   kind: 'rack', rack: 'wax',  cost: 260,     req: ['office'] },
    { id: 'wax',       kind: 'station', st: 'wax', lane: 0, cost: 340, req: ['waxRack'] },
    { id: 'tubeman',   kind: 'building', b: 'tubeman', cost: 450, req: ['wax'], name: 'Wacky Tube Man', icon: 'tubeman', pos: [-20.5, 6.9] },
    { id: 'vending',   kind: 'building', b: 'vending', cost: 520, req: ['wax'], name: 'Vending Machine', icon: 'vending', pos: [-21.0, 4.3] },
    { id: 'gelRack',   kind: 'rack', rack: 'gel', cost: 650,     req: ['tubeman', 'vending'] },
    { id: 'tire',      kind: 'station', st: 'tire', lane: 0, cost: 800, req: ['gelRack'] },
    { id: 'stocker2',  kind: 'hire', role: 'stocker', cost: 950,  req: ['tire'], pos: [-8, 5.0] },
    { id: 'fountain',  kind: 'building', b: 'fountain', cost: 1100, req: ['stocker2'], name: 'Fountain', icon: 'fountain', pos: [-2.5, 20.4] },
    { id: 'lane2',     kind: 'lane', lane: 1, cost: 1400,          req: ['stocker2'], name: 'Open Lane 2', icon: 'lane', pos: [-12, 13] },
    { id: 'l2rinse',   kind: 'station', st: 'rinse', lane: 1, cost: 600, req: ['lane2'] },
    { id: 'cashier2',  kind: 'hire', role: 'cashier', lane: 1, cost: 900, req: ['lane2'], pos: [15.2, 18.4] },
    { id: 'l2dryer',   kind: 'station', st: 'dryer', lane: 1, cost: 800, req: ['l2rinse'] },
    { id: 'billboard', kind: 'building', b: 'billboard', cost: 1700, req: ['l2dryer'], name: 'Billboard', icon: 'billboard', pos: [-19.0, 20.9] },
    { id: 'l2wax',     kind: 'station', st: 'wax', lane: 1, cost: 1500, req: ['l2dryer'] },
    { id: 'l2tire',    kind: 'station', st: 'tire', lane: 1, cost: 2000, req: ['l2wax'] },
    { id: 'polish',    kind: 'station', st: 'polish', lane: 0, cost: 2600, req: ['l2tire'] },
    { id: 'polisher1', kind: 'hire', role: 'polisher', lane: 0, cost: 3000, req: ['polish'], pos: [10.6, 5.3] },
    { id: 'coffee',    kind: 'building', b: 'coffee', cost: 3400, req: ['polisher1'], name: 'Coffee Machine', icon: 'coffee', pos: [-13.4, -6.6] },
    { id: 'stocker3',  kind: 'hire', role: 'stocker', cost: 3800, req: ['coffee'], pos: [-8, 5.0] },
    { id: 'l2polish',  kind: 'station', st: 'polish', lane: 1, cost: 4200, req: ['polisher1'] },
    { id: 'polisher2', kind: 'hire', role: 'polisher', lane: 1, cost: 4800, req: ['l2polish'], pos: [10.6, 18.3] },
    { id: 'vip',       kind: 'building', b: 'vip', cost: 6000, req: ['polisher2'], name: 'VIP Lounge', icon: 'vip', pos: [18.5, -6.2] },
    { id: 'flowers',   kind: 'building', b: 'flowers', cost: 7500, req: ['vip'], name: 'Flower Beds', icon: 'flowers', pos: [6.5, 20.4] },
    { id: 'landmark',  kind: 'building', b: 'landmark', cost: 10000, req: ['flowers'], name: 'Landmark', icon: 'landmark', pos: [14.2, 20.6] },
    { id: 'travel',    kind: 'travel', cost: 15000, req: ['landmark'], name: 'Road Trip', icon: 'road', pos: [22.5, 20.4] },
  ],

  // what each building does (applied in Game.recalc)
  buildingFx: {
    queue:     { queueCap: 3, desc: '+3 waiting spots, +5s patience', patience: 5 },
    tubeman:   { arrive: 0.85, desc: '+18% more cars' },
    vending:   { desc: 'Waiting drivers buy snacks' },
    fountain:  { tips: 0.10, desc: '+10% tip chance' },
    billboard: { arrive: 0.8, desc: '+25% more cars' },
    coffee:    { nap: 0.5, staffSpeed: 0.1, desc: 'Staff nap half as much, +10% speed' },
    vip:       { vip: 0.08, desc: 'VIP cars start arriving' },
    flowers:   { patienceMul: 1.2, desc: '+20% driver patience' },
    landmark:  { income: 0.25, desc: '+25% all income' },
    staff:     { desc: 'Hire and train staff here' },
    office:    { desc: 'Manager and marketing upgrades' },
  },

  hireNames: {
    stocker: ['Bubbles Bob', 'Soapy Sue', 'Rinse Ricky', 'Foamy Fran', 'Squeaky Sam', 'Sudsy Steve'],
    cashier: ['Cash Carla', 'Penny Pete', 'Change Chad'],
    polisher: ['Polly Shine', 'Glossy Gus', 'Buff Betty'],
  },

  // ---------- Customers ----------
  cars: {
    sedan:    { name: 'Sedan', pay: 1.0, pat: 30, len: 3.6 },
    hatch:    { name: 'Hatchback', pay: 0.9, pat: 34, len: 3.2 },
    pickup:   { name: 'Pickup', pay: 1.2, pat: 30, len: 4.0, dirt: 1.5 },
    taxi:     { name: 'Taxi', pay: 1.1, pat: 20, len: 3.6, honky: true },
    police:   { name: 'Police Car', pay: 1.5, pat: 18, len: 3.6, honky: true },
    van:      { name: 'Van', pay: 1.3, pat: 30, len: 3.9 },
    suv:      { name: 'SUV', pay: 1.4, pat: 30, len: 3.9 },
    icecream: { name: 'Ice Cream Truck', pay: 1.6, pat: 30, len: 4.0 },
    pizza:    { name: 'Pizza Delivery', pay: 1.5, pat: 22, len: 3.2, honky: true },
    hotdog:   { name: 'Hot Dog Car', pay: 1.8, pat: 40, len: 4.1 },
    duck:     { name: 'Duck Car', pay: 2.0, pat: 40, len: 3.5 },
    buggy:    { name: 'Beach Buggy', pay: 1.3, pat: 32, len: 3.2, dirt: 1.3 },
    tractor:  { name: 'Tractor', pay: 1.5, pat: 36, len: 3.6, dirt: 2 },
    snowplow: { name: 'Snowplow', pay: 1.6, pat: 30, len: 4.2 },
    rover:    { name: 'Moon Rover', pay: 1.7, pat: 32, len: 3.8, dirt: 1.4 },
    ufo:      { name: 'UFO', pay: 2.2, pat: 26, len: 3.6 },
    limo:     { name: 'Limo', pay: 4.0, pat: 40, len: 5.4, vip: true },
    sports:   { name: 'Sports Car', pay: 3.5, pat: 20, len: 3.9, vip: true },
    monster:  { name: 'Monster Truck', pay: 3.0, pat: 30, len: 4.3, vip: true, dirt: 2 },
    golden:   { name: 'Golden Car', pay: 3.0, pat: 70, len: 3.6, special: true },
  },
  paintColors: [0xe8383d, 0xffc93c, 0x3c7be8, 0xf5f6fa, 0x46c23a, 0xff8a1f, 0x8a4dff, 0x2fc6c6, 0x2a2e36, 0xff6fb1, 0x9aa6b8],

  // ---------- Locations ----------
  locations: [
    { id: 'suburb', name: 'Sunny Suburb', mult: 1, theme: 'suburb', spawn: 5.0, landmark: 'goldcar',
      cars: { sedan: 30, hatch: 22, pickup: 12, taxi: 10, police: 6, van: 9, suv: 9, icecream: 3, pizza: 4, hotdog: 2, duck: 2 } },
    { id: 'beach', name: 'Palm Beach', mult: 8, theme: 'beach', spawn: 5.8, landmark: 'sandcastle',
      cars: { sedan: 20, hatch: 14, buggy: 16, van: 10, icecream: 10, suv: 10, taxi: 6, pizza: 4, duck: 4, hotdog: 4 } },
    { id: 'desert', name: 'Dusty Desert', mult: 60, theme: 'desert', spawn: 5.6, landmark: 'oasis',
      cars: { pickup: 24, tractor: 14, sedan: 14, suv: 14, van: 8, buggy: 8, monster: 3, hotdog: 3, duck: 2 } },
    { id: 'snow', name: 'Snowy Peaks', mult: 450, theme: 'snow', spawn: 5.4, landmark: 'snowman',
      cars: { suv: 22, snowplow: 16, sedan: 16, pickup: 12, van: 10, taxi: 6, icecream: 2, duck: 3, hotdog: 3 } },
    { id: 'city', name: 'Neon City', mult: 3500, theme: 'city', spawn: 5.2, landmark: 'neontower',
      cars: { sedan: 18, taxi: 18, sports: 6, limo: 4, police: 10, hatch: 12, van: 8, pizza: 8, duck: 3, hotdog: 3 } },
    { id: 'moon', name: 'Moon Base', mult: 28000, theme: 'moon', spawn: 5.0, landmark: 'rocket',
      cars: { rover: 30, ufo: 22, sedan: 8, buggy: 10, duck: 4, hotdog: 4, sports: 3 } },
  ],

  // ---------- Player level ----------
  xpFor: (lvl) => 60 * lvl,
  levelGems: (lvl) => 5 + lvl,
  unlocks: { spin: 2, shop: 3, gift: 3, blaster: 4, album: 5, golden: 4, rush: 6, daily: 2 },

  // ---------- Meta: gems ----------
  gemUps: {
    sponge:  { name: 'Golden Sponge', icon: 'sparkles', max: 10, cost: (n) => 20 + n * 15, desc: (n) => `+${n * 10}% income everywhere` },
    offline: { name: 'Night Shift Manager', icon: 'clock', max: 6, cost: (n) => 15 + n * 12, desc: (n) => `Offline earnings up to ${2 + n}h` },
    tickets: { name: 'Ticket Box', icon: 'blaster', max: 3, cost: (n) => 25 + n * 20, desc: (n) => `Hold ${3 + n} Bubble Blaster tickets` },
  },
  hats: [
    { id: 'cap', name: 'Star Cap', cost: 0 },
    { id: 'cone', name: 'Traffic Cone', cost: 40 },
    { id: 'cowboy', name: 'Cowboy Hat', cost: 80 },
    { id: 'chef', name: 'Chef Hat', cost: 120 },
    { id: 'duck', name: 'Rubber Duck', cost: 180 },
    { id: 'bubble', name: 'Bubble Helmet', cost: 260 },
    { id: 'crown', name: 'Royal Crown', cost: 400 },
  ],
  iap: [
    { id: 'gems_s', name: 'Pile of Gems', gems: 80, price: '$0.99', icon: 'gem' },
    { id: 'gems_m', name: 'Bag of Gems', gems: 500, price: '$4.99', icon: 'gem' },
    { id: 'gems_l', name: 'Chest of Gems', gems: 1200, price: '$9.99', icon: 'gem' },
    { id: 'starter', name: 'Starter Pack', gems: 150, perk: 'starter', price: '$2.99', icon: 'gift', desc: '150 gems + permanent +50% income' },
    { id: 'noads', name: 'Remove Ads', perk: 'noads', price: '$2.99', icon: 'ad', desc: 'No banners or breaks. Rewards stay.' },
    { id: 'vip', name: 'VIP Pass', perk: 'vip', price: '$7.99', icon: 'crown', desc: 'Permanent 2x income and no ads' },
  ],
  cashPacks: [
    { id: 'cash1', name: 'Wallet', minutes: 10, gems: 10, icon: 'cash' },
    { id: 'cash2', name: 'Briefcase', minutes: 45, gems: 35, icon: 'office' },
    { id: 'cash3', name: 'Vault', minutes: 180, gems: 110, icon: 'trophy' },
  ],

  // ---------- Retention ----------
  daily: [
    { kind: 'cash', minutes: 5 },
    { kind: 'gems', n: 5 },
    { kind: 'boost', boost: 'income', secs: 900 },
    { kind: 'cash', minutes: 12 },
    { kind: 'gems', n: 10 },
    { kind: 'spins', n: 2 },
    { kind: 'gems', n: 30, hat: true },
  ],
  spin: [
    { kind: 'cash', minutes: 2, w: 26, label: 'Cash' },
    { kind: 'gems', n: 3, w: 18, label: '3 Gems' },
    { kind: 'cash', minutes: 6, w: 16, label: 'Cash x3' },
    { kind: 'boost', boost: 'speed', secs: 180, w: 12, label: 'Speed Shoes' },
    { kind: 'cash', minutes: 12, w: 9, label: 'Big Cash' },
    { kind: 'boost', boost: 'income', secs: 300, w: 10, label: '2x Income' },
    { kind: 'gems', n: 12, w: 6, label: '12 Gems' },
    { kind: 'cash', minutes: 40, w: 3, label: 'JACKPOT' },
  ],
  spinFreeEvery: 4 * 3600,

  // ---------- Ads & boosts ----------
  ads: {
    provider: 'mock',            // mock | crazygames | poki | gamedistribution | admob
    interstitialCooldown: 120,   // seconds between breaks
    sessionGrace: 180,           // no breaks in the first 3 minutes
    afterRewardedGrace: 90,      // no break right after a rewarded ad
    banner: true,
    mockRewardSecs: 5,
    mockInterSecs: 3,
    limits: { freeCash: 10, spin: 5, freeGems: 3, ticket: 3, blasterX3: 3 },
    freeUpgradeEvery: 180,
    turboEvery: 300,
  },
  boosts: {
    income: { add: 300, max: 3600 },
    speed: { add: 180, max: 1800 },
    turbo: { add: 180, max: 180 },
  },
  offlineBaseHours: 2,
  offlineEfficiency: 0.4,

  events: {
    giftEvery: [150, 240],
    goldenEvery: [240, 420],
    rushEvery: [360, 600],
    rushSecs: 45,
  },

  blaster: { ticketEvery: 20 * 60, baseTickets: 3 },

  // ---------- Words ----------
  cleanWords: ['SQUEAKY CLEAN!', 'SPARKLING!', 'SO SHINY!', 'WOW!', 'GLEAMING!', 'SPOTLESS!', 'FRESH!'],
  driverLines: [
    'My mom drove through a swamp.', 'Is this the drive-thru?', 'Make it SHINY!', "Don't scratch my baby!",
    'I have a date tonight!', 'A bird attacked me. Twice.', 'Do you do tires?', 'Smells like nachos in here.',
    'My kid ate a crayon back there.', 'Mud? What mud?', 'Hurry, my ice cream is melting!', 'Can I keep the foam?',
    'I took a shortcut through a farm.', 'Is the soap organic?', 'Five stars if it sparkles.', 'Honk if you love bubbles!',
    'Last time it was purple...', 'I washed it in 2009.', 'Please be gentle, it is a rental.', 'Are the brushes friendly?',
  ],
  ragePuns: ['RAGE QUIT!', 'TOO SLOW!', 'NOPE!', 'BYE!'],
};
