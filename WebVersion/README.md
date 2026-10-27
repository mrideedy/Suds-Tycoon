# Suds Tycoon

A 3D idle-arcade car wash tycoon in the style of Pizza Ready, My Little Restaurant and Township, with a Mob Control-style mini-game. Runs in any modern browser on desktop and phones (HTML5 + WebGL via Three.js). All art and sound are generated in code, so there are no asset files.

## Play

- **Quickest:** open `dist/suds-tycoon.html` in Chrome, Edge or Safari. It needs an internet connection the first time (Three.js and the fonts load from a CDN).
- **From source:** `node tools/serve.js` then open http://localhost:8765. Edit any file in `js/` or `css/` and reload.

Controls: drag anywhere to move (floating joystick) or use WASD / arrow keys. Mouse wheel zooms. Walk over racks to pick up supplies, over the green mats to load machines, stand on the orange mat at the pay booth to take payment, walk past cash stacks to collect them, and stand on a price pad to build.

Developer helpers: add `?dev` (or `#dev`) to the URL for cash/gem/level buttons in Settings. Add `?slot=test` to use a separate save.

## Project layout

| File | What it does |
| --- | --- |
| `index.html` | Page shell; loads Three.js r149 from jsDelivr and the scripts below in order |
| `css/style.css` | All UI styling (HUD, cards, panels, ads, mini-game HUD) |
| `js/config.js` | Every number and piece of content: stations, pads, cars, locations, upgrades, rewards, ad limits |
| `js/game.js` | Simulation: economy formulas, spawning, payouts, pads, upgrades, staff tasks, events, quests, offline earnings |
| `js/entities.js` | Manager, staff AI (stocking, posts, naps), carried stacks, customer cars |
| `js/stations.js` | Wash machines and their cleaning effects, hoppers, supply racks, cash piles, build pads, lanes, buildings |
| `js/models.js` | Procedural low-poly models (cars, people, machines, buildings, props) |
| `js/world.js` | The six themed locations (suburb, beach, desert, snow, neon city, moon) |
| `js/ui.js` | HUD, upgrade cards, all panels, joystick and keyboard input |
| `js/minigame.js` | Bubble Blaster mini-game |
| `js/ads.js` | Ad manager with a mock provider and adapters for real SDKs |
| `js/audio.js` | Synthesised sound effects and music |
| `js/save.js` | Save/load in localStorage |
| `tools/build.js` | Bundles everything into `dist/suds-tycoon.html` |
| `tools/econ-sim.js` | Economy simulation: prints when each pad gets built (`node tools/econ-sim.js 1`) |

After changing code, run `node tools/build.js` to refresh the single-file build.

## Ads

All ad calls go through `js/ads.js`:

- `Ads.rewarded('placement')` resolves `true` when the reward was earned.
- `Ads.interstitial('placement')` respects the rules in `CFG.ads` (3 min grace per session, 2 min between breaks, none right after a rewarded ad or while the player is moving).
- `Ads.banner(true/false)`.

The default `mock` provider shows a test ad so every placement can be tried. To switch to a real network, set `CFG.ads.provider` in `js/config.js` and add that SDK's script tag to `index.html`:

| Provider | Value | SDK script |
| --- | --- | --- |
| CrazyGames | `crazygames` | `https://sdk.crazygames.com/crazygames-sdk-v3.js` |
| Poki | `poki` | `https://game-cdn.poki.com/scripts/v2/poki-sdk.js` |
| GameDistribution | `gamedistribution` | `https://html5.api.gamedistribution.com/main.min.js` (set `window.GD_OPTIONS` with your game ID first) |
| AdMob (Android/iOS app) | `admob` | Wrap the game with Capacitor and install `@capacitor-community/admob` |

The AdMob adapter ships with Google's public Android **test** unit IDs. Replace them with your own IDs (`admob.ids` in `js/ads.js`) before release. If an SDK is missing, the game falls back to the mock provider and keeps working.

Rewarded placements in the game: `boost_income`, `boost_speed`, `boost_turbo`, `free_cash`, `free_gems`, `free_upgrade`, `offline_x2`, `daily_x2`, `spin`, `gift_balloon`, `golden_car`, `level_x2`, `blaster_ticket`, `blaster_x3`. Interstitials: `blaster_end`, `travel`, `panel_close`.

Purchases (Remove Ads, VIP Pass, gem packs) are simulated in the Shop. Connect Google Play Billing / StoreKit (for example with a Capacitor purchases plugin) to sell them for real.

## Shipping to Google Play / App Store

1. `npm init -y && npm i @capacitor/core @capacitor/cli @capacitor/android @capacitor-community/admob`
2. `npx cap init "Suds Tycoon" com.yourstudio.sudstycoon --web-dir www`
3. Copy `dist/suds-tycoon.html` to `www/index.html`, then `npx cap add android` and `npx cap open android`.
4. For an offline build, download Three.js r149 (`three.min.js`) into `www/` and point the script tag at it.
