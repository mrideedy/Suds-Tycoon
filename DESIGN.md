# Suds Tycoon (Unity)

Idle-arcade car wash tycoon (Pizza Ready / My Little Restaurant style), grown from the web build in `WebVersion/` into a world business: a 17-city world tour from your hometown to Mars, branches that keep earning after you move on, staff, 32 build pads per city (10 on the main road), machines up to level 30, 4 business upgrades, 4 HQ gem upgrades, daily rewards, lucky spin, car album, hats, golden cars, rush hour, offline earnings, ad boosts and the Bubble Blaster mini-game (Mob Control style).

Unity 6000.3 (6.3 LTS), Universal Render Pipeline, new Input System, UI Toolkit. No paid assets.

## Run it

1. Open the project in Unity 6.3 and let it import (the first import of the models takes a minute or two).
2. Menu **Tools > Suds Tycoon > Create Game Scene**. This creates `Assets/SudsTycoon/Scenes/SudsTycoon.unity` and puts it first in Build Settings.
3. Press **Play**.

Controls: drag anywhere to move (floating joystick), or WASD / arrow keys. Mouse wheel or two-finger pinch zooms. Esc closes a panel. Tap a sleeping worker to wake them, tap a machine to open its upgrades.

Developer buttons (cash, gems, build everything, +1 level) are in **Settings** while playing in the Editor or in a Development build. **Tools > Suds Tycoon > Delete Save File** starts over.

## Progression

- **World tour**: 17 cities in 6 regions (Hometown, USA, Europe, Asia, Around the World, Space). City prices go x1, x2, x5, x10, x20, x50 ... up to x200K on Mars. The Road Trip pad opens the next city as soon as the first lane has its Tire Shine; see the whole tour from the World button.
- **Fast exploring**: every city after the first starts with the Franchise Kit (Water, Dryer, Cashier, Staff Room, a Stocker and the Manager Office already built) plus a welcome gift, so you skip the repeats. The other 21 pads (Lane 2, Polish Bay, VIP Lounge, landmark ...) are optional extras for more income, a stronger branch and the City Complete reward.
- **Simple numbers**: pad prices are round, every machine earns whole dollars per car, and upgrade costs and rewards are rounded to two digits (1,234 becomes 1.2K).
- **Upgrades**: two tabs, every row reads "now → next". *Machines*: each level adds money per car (+$1 for Brush, Water, Dryer and Cashier, +$2 for Wax and Tires, +$4 for Polish, times the city price) and makes the machine a little faster; new paint at Lv 5, 10, 20 and 30. *Business* (after the Manager Office): Faster Team, Carry More, More Customers, Higher Prices, kept for good in every city. All gem upgrades live at HQ in the Empire panel: Golden Sponge, Franchise Managers, Night Shift, Ticket Box.
- **Empire**: every city you leave keeps earning as a branch (25% of its income, more with Franchise Managers) for up to 4 hours (more with Night Shift, which also extends offline earnings). Collect from the Empire button; an ad doubles it.
- **Economy**: tuned for exploring. In the simulator a player without ads opens the first Road Trip after about 18 minutes and each later city after about 10 (about 9 and 5 minutes with 2x income running); the whole tour takes about 2h45 without ads. Tune `Cfg.cs` and check with `node .offline/econ-sim.js` (`--cities 17` for the whole tour, `--log` for the first city's build order).
- **Old saves** (v2) migrate on load: per-city upgrade tracks become business upgrades (highest level kept), retired HQ upgrades refund their gems, and cities you already opened get the Franchise Kit.
- **Ad boosts** (right side): 2x Income (+10 min per ad, stacks to 4 h), Turbo Wash (machines 2x faster), Car Rush (3x customers) and Turbo Tom (a helper). Ads also finish a nearly-paid build pad, triple offline earnings, double branch earnings, and give free upgrades, cash and gems.

## Fonts

The UI uses the same fonts as the web build, Lilita One and Nunito (SIL Open Font License, see `Resources/Fonts/FONTS-LICENSE.txt`). They are turned into SDF font assets at startup in `Scripts/UI/Fonts.cs`.

## Project layout

| Path | What it is |
| --- | --- |
| `Assets/SudsTycoon/Scripts/SudsGame.cs` | Entry point: camera, light, UI document, save load, main loop, offline earnings, auto quality |
| `Scripts/Core/Cfg.cs` | Every number and piece of content (stations, pads, cars, locations, upgrades, rewards, ad limits) |
| `Scripts/Core/Store.cs` | Save game (one JSON file in `Application.persistentDataPath`) |
| `Scripts/Gameplay/Game.cs` | Simulation: economy, spawning, payouts, pads, upgrades, staff tasks, events, quests, travel |
| `Scripts/Gameplay/*.cs` | Player, Worker, Car, Station, Hopper, Rack, CashPile, Pad, Lane, Building, grid pathfinding |
| `Scripts/World/WorldBuilder.cs` | The themed lots: every city has its own theme, lighting and ground tile (`Resources/Textures/lot_<theme>.png`: parking lot, boardwalk, terracotta, casino carpet, cobbles, Paris stone, marble, Mumbai tiles, Tokyo neon, Copacabana waves, sandstone, craters, Mars deck plates...) |
| `Scripts/Audio/Sfx.cs` | Every sound and the music are synthesised at startup (no audio files): the title tune plus one music loop per world region (`Songs`) |
| `Scripts/FX/Fx.cs` | Particles, mud and foam stuck to cars, flying cash and items |
| `Scripts/UI/` | HUD, upgrade cards, all panels, world labels, joystick and keyboard (UI Toolkit, built in code) |
| `Scripts/UI/Intro.cs` | Animated title screen: the logo letters bounce in, bubbles float up (tap to pop), then TAP TO START. Its own music loop is synthesised in `Sfx.BuildIntroMusic` and fades into the game music on tap |
| `Scripts/MiniGame/Blaster.cs` | Bubble Blaster |
| `Scripts/Ads/Ads.cs` | Ad manager: mock provider (Editor / dev builds), free rewards (store builds without an ad network), and a Google AdMob adapter |
| `Scripts/Dev/AutoPlay.cs` | Editor-only playtest bot |
| `Scripts/Audio/Sfx.cs` | Every sound and the music loop are synthesised at startup (no audio files) |
| `Resources/Models` | Low-poly models exported from the web build as `.glb` (imported by glTFast) |
| `Resources/Icons`, `Resources/Textures` | UI icons and decals exported from the web build |
| `Resources/UI/Suds.uss` | All UI styling |
| `Editor/SudsSetup.cs` | The Tools menu and texture import settings |
| `Editor/PlayStoreBuild.cs`, `Editor/AppIcon.cs` | Google Play release settings, upload key, signed `.aab` build, launcher icons |
| `Assets/SudsTycoon/AppIcon/` | Launcher icon sources (square, round, adaptive background and foreground) |

Gameplay code works in the web build's coordinates and converts at the transform boundary (`U.P` / `U.W` flip x, because glTFast mirrors x on import). That keeps every number in `Cfg.cs` identical to `WebVersion/js/config.js`.

## Ads

All ad calls go through `Ads`:

- `Ads.Rewarded("placement", ok => ...)`: give the reward only when `ok` is true.
- `Ads.Interstitial("placement", () => ...)`: respects the grace periods and cooldowns in `Cfg.Ads`; the callback always runs.
- `Ads.Banner(true / false)`.

The default `mock` provider shows test ads inside the game so every placement can be tried. It only runs in the Editor and development builds: a store build with no ad network uses `FreeRewards`, which gives every rewarded placement at once (gift icon instead of the AD tag), shows no banners or interstitials, and doesn't stack boosts. To use **Google AdMob**:

1. Install the Google Mobile Ads Unity plugin (https://github.com/googleads/googleads-mobile-unity/releases) and set your AdMob app IDs in **Assets > Google Mobile Ads > Settings**.
2. Menu **Tools > Suds Tycoon > Use AdMob** (adds the `SUDS_ADMOB` scripting define).
3. Set `Provider = "admob"` in `Cfg.Ads` (`Scripts/Core/Cfg.cs`).
4. Replace the test ad unit IDs at the top of `AdMobAds` in `Scripts/Ads/Ads.cs` with your own.

Rewarded placements: `boost_income`, `boost_wash`, `boost_rush`, `boost_turbo`, `pad_finish`, `empire_x2`, `free_cash`, `free_gems`, `free_upgrade`, `offline_x3`, `daily_x2`, `spin`, `gift_balloon`, `golden_car`, `level_x2`, `blaster_ticket`, `blaster_x3`. Interstitials: `blaster_end`, `travel`, `panel_close`. Players who buy Remove Ads or the VIP Pass get no banners or interstitials; rewarded ads stay available.

Purchases in the Shop (gem packs, Starter Pack, Remove Ads, VIP Pass) are simulated, so store builds hide them (`Cfg.IapEnabled`). Hook up Unity IAP (`com.unity.purchasing`) in `BuyIap` in `Scripts/UI/Panels.cs` to sell them for real.

## Google Play

Everything for the store is in **[PLAYSTORE.md](PLAYSTORE.md)**: build steps, listing text, privacy policy, content rating and Data safety answers.

- **Tools > Suds Tycoon > Google Play Build...**: creates your upload key (you choose the password) and builds the signed `.aab` into `Builds/Android/`. The key lives in `Keystore/`; back it up.
- **Tools > Suds Tycoon > Apply Release Settings**: package `com.sudstycoon.game`, IL2CPP + ARM64, newest target API, AAB, landscape, launcher icons from `Assets/SudsTycoon/AppIcon/`.
- Store images (512 px icon, 1024 × 500 feature graphic, screenshots) are in `StoreAssets/`.
- The HUD and panels keep clear of notches and rounded corners (`Hud.ApplySafeArea`).
- The game drops shadows automatically when the first seconds run under 32 fps, and players can toggle graphics quality in Settings.

## Playtest bot

`Scripts/Dev/AutoPlay.cs` (Editor only) plays like a greedy player who never watches ads. It keeps the machines stocked, collects cash, builds pads, buys upgrades, claims quests and takes every Road Trip, and logs milestones and errors to `Temp/autoplay.log`. In Play mode, run `new GameObject("AutoPlay").AddComponent<SudsTycoon.AutoPlay>()`. `Speed` sets the simulation steps per frame, and `StopAtCity` sets where it stops. Last run (4× speed, no ads, no branch collecting): Sunny Suburb took 9 min, Downtown 9.5 min, Palm Beach 14.5 min, with no errors.

## Changing the art

The models, icons and decals come from the web build. To re-export after changing `WebVersion/js/models.js`:

```
node WebVersion/tools/serve.js 8766 "Assets/SudsTycoon/Resources"
```

then open http://localhost:8766/export-unity.html in a browser and wait for "done". Back in Unity, run **Tools > Suds Tycoon > Reimport Art**.
