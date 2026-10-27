# Suds Tycoon on Google Play

Everything you need to publish version 1.0.0. The game side is done: release settings, icon, store-safe ads and shop, and a one-click build window.

## 1. Build the .aab (about 15 minutes, mostly waiting)

1. Open the project in Unity, then **Tools > Suds Tycoon > Google Play Build...**
2. **Upload key** (first time only): type your name or studio and choose a password, then click **Create upload key**.
   It is saved in `Keystore/suds-upload.keystore` (outside `Assets`, never packed into the game).
   **Back up the `Keystore` folder and the password now.** Every future update must be signed with the same key.
3. Click **Build .aab for Google Play**. The file appears in `Builds/Android/SudsTycoon-1.0.0-1.aab`.
   Unity forgets the key password when it restarts, so type it again before each build. A successful build raises the version code for the next upload automatically.
   Next to the .aab, Unity also writes a `…_BackUpThisFolder_ButDontShipItWithYourGame` folder (about 800 MB) with debug symbols for crash reports. Keep the one for the version you upload, delete older ones, and never upload it.

I checked the build itself: a full Android build of this project (IL2CPP, ARM64, AAB) completes with 0 errors, and the .aab is about 52 MB.

> **Disk space:** drive D: was full (30 MB free), and an Android build needs several GB. The project's `Library` cache (Unity's regenerable build cache) now lives on C: at `C:\Users\mriga\UnityLibrary\SudsTycoon`, linked back to `D:\Projects\Suds Tycoon\Library` with a directory junction. Unity sees no difference, and D: got 8.5 GB back. To undo it, close Unity, delete the `Library` junction (not the C: folder), and move the folder back. Unity also rebuilds `Library` by itself if it's missing.

Release settings (applied by the window, or **Tools > Suds Tycoon > Apply Release Settings**):

| Setting | Value |
|---|---|
| Package name | `com.sudstycoon.game` (**permanent once uploaded**; change `PlayStoreBuild.PackageName` before the first upload if you want another) |
| Version | 1.0.0, version code 1 |
| Target API | newest installed (Android 16, API 36); minimum API 25 (Android 7.1) |
| Code | IL2CPP, ARM64 (Google Play's 64-bit requirement) |
| Format | Android App Bundle (.aab) |
| Orientation | Landscape (either way up); notches and rounded corners are kept clear of the HUD |
| Splash | Unity splash off; the game's own loading screen shows straight away |

## 2. Ads and purchases in this build

- **Ads**: no ad network is connected yet, so the store build shows **no ads at all**. Every "watch an ad" reward (2x income, Turbo Wash, Car Rush, free cash, spins, tickets…) is given free with a gift icon, and boosts don't stack. The fake test ads only appear in the Unity Editor and development builds.
- **Purchases**: purchases are still simulated, so the store build **hides** the paid items (Starter Pack, Remove Ads, VIP Pass, gem packs). Free gems, gem upgrades and hats stay.

To earn money from ads later (an update):
1. Make an AdMob account at https://admob.google.com, add the app (Android), and create 3 ad units: Rewarded, Interstitial, Banner.
2. Import the Google Mobile Ads Unity plugin (https://github.com/googleads/googleads-mobile-unity/releases), then put your AdMob **app ID** in Assets > Google Mobile Ads > Settings.
3. Replace the three test unit IDs in `Scripts/Ads/Ads.cs` (class `AdMobAds`) with yours, set `Cfg.Ads.Provider = "admob"` in `Scripts/Core/Cfg.cs`, and run **Tools > Suds Tycoon > Use AdMob**.
4. In Play Console, change "Contains ads" to Yes and update the Data safety form (AdMob collects the advertising ID).

Real purchases need Unity IAP (Google Play Billing); the shop code is ready to wire to it.

## 3. Play Console checklist

1. **Create app**: name "Suds Tycoon", default language English, type **Game**, **Free**.
2. **Store listing**: paste the text below and upload the images from `StoreAssets/`.
3. **App content**:
   - Privacy policy: host the text below on any public page (Google Sites, GitHub Pages…) and paste its URL.
   - Ads: **No, my app does not contain ads** (true for this build).
   - App access: all functionality is available without special access.
   - Content rating (IARC questionnaire): category **Game**. No violence, no blood, no sexual content, no bad language, no drugs, no real-money gambling, no user-to-user communication, no location sharing, no digital purchases. The prize wheel and mini-game give only in-game rewards (no wagering). Expected rating: **Everyone / PEGI 3**.
   - Target audience: choose **13 and over**. Choosing under-13 brings in the Families policy, with strict rules for the ads you'll add later.
   - Data safety: **No data collected, no data shared**. The game saves progress only on the device and has no accounts, analytics or ads SDK yet.
   - News app: No. Government app: No. Financial features: None. Health: None.
4. **Testing**:
   - Personal developer accounts created after November 2023 must run a **closed test with at least 12 testers for 14 days** before they can publish to Production.
   - Create a Closed testing track, upload the .aab, add tester emails, and share the opt-in link.
   - Older accounts, and organization accounts, can go straight to Production. Internal testing is the fastest way to try the build on your own phone.
5. **Release**: upload `SudsTycoon-1.0.0-1.aab`, release name "1.0.0", release notes "First release".

## 4. Store listing text

**App name** (30 characters max):
Suds Tycoon: Car Wash Empire

**Short description** (80 max):
Build a bubbly car wash empire, from your hometown all the way to Mars!

**Full description**:

Grab a sponge and build the bubbliest car wash empire on Earth… and beyond!

Start with one foam brush on a sunny street corner. Load soap, wash muddy cars, collect your cash and build your next machine. Water arches, hot wax, turbo dryers, tire shine and polish bays turn your little lot into a sparkling wash line, and every happy driver pays more.

BUILD AND UPGRADE
• Walk onto build pads to open new machines, racks, buildings and a second wash lane
• Upgrade every machine to earn more per car, with new paint at levels 5, 10, 20 and 30
• Hire stockers, cashiers and polishers who keep the bubbles flowing while you relax
• Business upgrades (faster team, carry more, more customers, higher prices) stay with you in every city

TRAVEL THE WORLD
• Take a road trip to 17 cities: Downtown, Palm Beach, Route 66, Las Vegas, London, Paris, the Swiss Alps, Dubai, Mumbai, Tokyo, Sydney, Rio, Cairo… then the Moon and Mars
• Every city has its own look, its own cars, its own music and its own landmark
• Every city unlocks a unique perk for good: Cash Magnet, Jackpot golden cars, Swiss Precision, Bullet Shoes, Low Gravity and more, plus themed hats like a cowboy hat, a chef hat and a royal crown
• A Franchise Kit sets up each new city, so you get straight to the fun
• Cities you leave keep earning as branches of your Suds Empire

SO MUCH TO DO
• Rush hours, golden cars, gift balloons and a daily prize wheel
• Collect 20 crazy cars in your album, from taxis and ice cream trucks to a duck car and a UFO
• Play the Bubble Blaster mini-game to splat mud monsters for big rewards
• Earn while you're away with offline earnings
• Dress up your manager with hats, from a traffic cone to a royal crown

Simple numbers, quick upgrades and a new city every few minutes. Suds Tycoon is the relaxing idle tycoon that's always one more wash away from something new.

**Category**: Game > Simulation. **Tags**: Idle, Tycoon, Simulation, Casual.

## 5. Privacy policy (host this text, then link it)

> **Suds Tycoon Privacy Policy**
>
> Last updated: October 2026
>
> Suds Tycoon ("the game") is made by [your name or studio]. This policy explains what data the game handles.
>
> **Data we collect:** none. The game does not ask for or collect personal information, has no accounts, and sends no data to us or to third parties. Your progress is saved only on your device, and uninstalling the game deletes it.
>
> **Permissions:** the game needs no special permissions.
>
> **Children:** the game is not directed at children under 13 and does not knowingly collect data from anyone.
>
> **Changes:** if a future version adds ads or analytics, this policy will be updated before that version is released, and will say what data those services use.
>
> **Contact:** [your contact email]

## 6. Images (`StoreAssets/`)

| File | Use |
|---|---|
| `icon-512.png` | App icon (512 × 512) |
| `feature-graphic.png` | Feature graphic (1024 × 500) |
| `screenshot-*.png` | Phone screenshots (landscape 16:9; upload 4 to 8) |

The launcher icons inside the game are in `Assets/SudsTycoon/AppIcon/` (adaptive layers for Android 8 and newer).

## 7. Trailer (`Trailer/`)

| File | Use |
|---|---|
| `SudsTycoon-Trailer-16x9.mp4` | Pre-launch trailer (ends with COMING SOON): YouTube, socials |
| `SudsTycoon-Trailer-16x9-PlayNow.mp4` | Launch trailer (ends with PLAY FREE NOW): the store listing video |
| `SudsTycoon-Reel-9x16.mp4`, `SudsTycoon-Reel-9x16-PlayNow.mp4` | Vertical cuts for Instagram Reels, YouTube Shorts and TikTok |

Google Play only takes a YouTube link for the promo video: upload the 16:9 PlayNow file to YouTube (public or unlisted, ads off), then paste its URL in **Store listing > Video**.

To change the captions or the end-card line, edit `Trailer/make_trailer.py` and run `python make_trailer.py` (add `--cta "..." --suffix Name` for another ending). It needs the frames in `Trailer/raw` (about 3 GB). To film new footage, see `Assets/SudsTycoon/Scripts/Dev/TrailerDirector.cs`. When you're done with the trailer, you can delete `Trailer/raw`.
