using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>An ad network behind the game's single ad API.</summary>
    public interface IAdProvider
    {
        string Name { get; }
        void Init(Action<bool> done);
        void Rewarded(string placement, Action<bool> done);
        void Interstitial(string placement, Action done);
        void Banner(bool show);
    }

    /// <summary>
    /// One ad API for the whole game (port of ads.js):
    ///   Ads.Rewarded("placement", ok => ...)   reward only when ok
    ///   Ads.Interstitial("placement", () => ...) respects cooldowns and grace periods; the callback always runs
    ///   Ads.Banner(true / false)
    /// The provider comes from Cfg.Ads.Provider. "mock" shows test ads in the game UI so every placement can be tried
    /// before an SDK is installed; "admob" needs the Google Mobile Ads Unity plugin and the SUDS_ADMOB scripting define.
    /// </summary>
    public static class Ads
    {
        static IAdProvider provider;
        static long lastInter, lastRewarded, sessionStart;
        public static bool Busy { get; private set; }
        public static string ProviderName => provider != null ? provider.Name : "mock";
        static bool NoAds => Store.S.perks.noads || Store.S.perks.vip;
        /// <summary>A store build with no ad network connected: rewards are given at once, with no video.</summary>
        public static bool Free => provider is FreeRewards;
        /// <summary>Icon for buttons that play a rewarded ad (a gift box when rewards are free).</summary>
        public static string Icon => Free ? "gift" : "ad";

        internal static void ResetStatics() { provider = null; Busy = false; lastInter = lastRewarded = 0; }

        public static void Init()
        {
            sessionStart = U.NowMs;
            provider = Pick(Cfg.Ads.Provider);
            provider.Init(ok =>
            {
                if (!ok) { Debug.Log("[ads] provider failed, falling back"); provider = Fallback(); }
                Banner(Cfg.Ads.Banner);
            });
        }

        static IAdProvider Pick(string name)
        {
#if SUDS_ADMOB
            if (name == "admob") return new AdMobAds();
#endif
            return Fallback();
        }
        /// <summary>The mock shows made-up test creatives: fine in the Editor and development builds, never in a store build.</summary>
        static IAdProvider Fallback() => Application.isEditor || Debug.isDebugBuild ? new MockAds() : (IAdProvider)new FreeRewards();
#if UNITY_EDITOR
        /// <summary>Editor testing: behave like a store build with no ad network (instant rewards, no ad screens).</summary>
        public static void UseFreeRewards() { provider = new FreeRewards(); Banner(false); Hud.Init(); }
#endif

        public static void Rewarded(string placement, Action<bool> done)
        {
            if (Busy || provider == null) { done?.Invoke(false); return; }
            Busy = true;
            Bus.Emit("ads:pause", true); Sfx.Duck(true);
            provider.Rewarded(placement, ok =>
            {
                Busy = false;
                Bus.Emit("ads:pause", false); Sfx.Duck(false);
                if (ok) lastRewarded = U.NowMs;
                Bus.Emit("ad", placement);
                if (!ok) Hud.Toast("No reward this time. Watch the whole ad to earn it.", "ad");
                done?.Invoke(ok);
            });
        }

        public static bool CanInterstitial()
        {
            if (NoAds || Busy || provider == null || Free) return false;
            if (Game.MoveActive) return false;   // never interrupt a player who is moving
            long now = U.NowMs;
            if (now - sessionStart < Cfg.Ads.SessionGrace * 1000) return false;
            if (now - lastInter < Cfg.Ads.InterstitialCooldown * 1000) return false;
            if (now - lastRewarded < Cfg.Ads.AfterRewardedGrace * 1000) return false;
            return true;
        }

        public static void Interstitial(string placement, Action done = null)
        {
            if (!CanInterstitial()) { done?.Invoke(); return; }
            Busy = true; lastInter = U.NowMs;
            Bus.Emit("ads:pause", true); Sfx.Duck(true);
            provider.Interstitial(placement, () =>
            {
                Busy = false;
                Bus.Emit("ads:pause", false); Sfx.Duck(false);
                Bus.Emit("ad", placement);
                done?.Invoke();
            });
        }

        public static void Banner(bool show) => provider?.Banner(show && !NoAds);
    }

    /// <summary>
    /// Store builds without an ad network: rewarded placements pay out at once, with no interstitials or banners.
    /// Connect AdMob (see PLAYSTORE.md) to earn money from ads.
    /// </summary>
    public class FreeRewards : IAdProvider
    {
        public string Name => "none";
        public void Init(Action<bool> done) => done(true);
        public void Rewarded(string placement, Action<bool> done) => done(true);
        public void Interstitial(string placement, Action done) => done();
        public void Banner(bool show) => Hud.SetBannerVisible(false);
    }

    /// <summary>Built-in test ads drawn with the game UI (fake creatives, countdown, skip / collect).</summary>
    public class MockAds : IAdProvider
    {
        public string Name => "mock";
        static readonly (string game, string line, Color bg, string icon)[] Creatives =
        {
            ("Bubble Pop Saga", "Pop 10,000 bubbles. Relax. Repeat.", U.Hex(0x8a5cf6), "bubble"),
            ("Mega Mower Tycoon", "Mow lawns. Get rich. Buy a gold mower.", U.Hex(0x2fae24), "trophy"),
            ("Burger Bounce", "Stack the tallest burger in town!", U.Hex(0xff8a1f), "star"),
            ("Penguin Plumber", "Fix pipes. Save the igloo.", U.Hex(0x2f8cff), "water"),
        };
        static readonly string[] BannerLines = { "Banner slot 320x50 · your ad network fills this", "Love Suds Tycoon? Tell a friend!", "Remove Ads in the Shop for a clean screen" };
        IVisualElementScheduledItem bannerJob;
        int bannerI;

        public void Init(Action<bool> done) => done(true);
        public void Rewarded(string placement, Action<bool> done) => Show(true, Cfg.Ads.MockRewardSecs, done);
        public void Interstitial(string placement, Action done) => Show(false, Cfg.Ads.MockInterSecs, _ => done());

        public void Banner(bool show)
        {
            var el = Hud.BannerAd;
            if (el == null) return;
            Hud.SetBannerVisible(show);
            if (!show || bannerJob != null) return;
            void Draw()
            {
                el.Clear();
                el.Add(UiKit.Txt("AD", "adtag"));
                el.Add(UiKit.Icon("ad", "bn-ico"));
                el.Add(UiKit.Txt(BannerLines[bannerI++ % BannerLines.Length], "bn-text"));
            }
            Draw();
            bannerJob = el.schedule.Execute(Draw).Every(30000);
        }

        void Show(bool rewarded, float secs, Action<bool> done)
        {
            var layer = Hud.AdLayer;
            if (layer == null) { done(rewarded); return; }
            var c = Creatives[UnityEngine.Random.Range(0, Creatives.Length)];
            layer.Clear();
            layer.style.display = DisplayStyle.Flex;

            var top = UiKit.El("ad-top");
            var info = UiKit.El("ad-info");
            info.Add(UiKit.Txt("AD", "adtag"));
            var count = UiKit.Txt(rewarded ? $"Reward in {Mathf.CeilToInt(secs)}s" : $"Skip in {Mathf.CeilToInt(secs)}s", "ad-count");
            info.Add(count);
            top.Add(info);
            bool finished = false, ready = false;
            IVisualElementScheduledItem job = null;
            void Finish(bool ok)
            {
                if (finished) return;
                finished = true;
                job?.Pause();
                layer.Clear();
                layer.style.display = DisplayStyle.None;
                done(ok);
            }
            var closeTxt = UiKit.Txt(rewarded ? "Cancel" : "...", "ad-x-text");
            var close = UiKit.Btn("ad-x", () => { if (ready) Finish(true); else if (rewarded) Finish(false); }, closeTxt);
            top.Add(close);
            layer.Add(top);

            var card = UiKit.El("ad-creative");
            card.style.backgroundColor = c.bg;
            var bubbles = UiKit.El("ad-bubbles");
            var bubbleList = new List<VisualElement>();
            for (int i = 0; i < 12; i++)
            {
                var b = UiKit.El("ad-bubble");
                float s = U.Rand(0.5f, 1.5f);
                b.style.left = Length.Percent(i * 8.5f + U.Rand(0f, 6f));
                b.style.width = b.style.height = 30f * s;
                b.userData = U.Rand(0f, 3.5f);
                bubbles.Add(b); bubbleList.Add(b);
            }
            card.Add(bubbles);
            card.Add(UiKit.Icon(c.icon, "ad-icon"));
            card.Add(UiKit.Txt(c.game, "ad-game"));
            card.Add(UiKit.Txt(c.line, "ad-line"));
            card.Add(UiKit.Button("Play Free", "big gold ad-cta", null));
            var prog = UiKit.Bar("ad-progress", 0f, U.Hex(0xffc92e));
            card.Add(prog);
            layer.Add(card);
            layer.Add(UiKit.Txt("Test ad from the built-in mock provider. Real ads appear once an ad SDK is connected.", "ad-note"));

            float t0 = Time.unscaledTime;
            job = layer.schedule.Execute(() =>
            {
                float el = Time.unscaledTime - t0;
                int left = Mathf.Max(0, Mathf.CeilToInt(secs - el));
                UiKit.SetBar(prog, el / secs);
                foreach (var b in bubbleList)
                {
                    float ph = ((el + (float)b.userData) % 3.5f) / 3.5f;
                    b.style.bottom = Length.Percent(-10f + ph * 120f);
                }
                if (!ready)
                {
                    count.text = rewarded ? $"Reward in {left}s" : $"Skip in {left}s";
                    if (el >= secs)
                    {
                        ready = true;
                        closeTxt.text = rewarded ? "Collect reward" : "Skip";
                        count.text = rewarded ? "Reward earned" : "You can skip now";
                    }
                }
            }).Every(50);
        }
    }

#if SUDS_ADMOB
    /// <summary>
    /// Google AdMob via the Google Mobile Ads Unity plugin (https://github.com/googleads/googleads-mobile-unity).
    /// The IDs below are Google's public TEST units: replace them with your own before release, and put your
    /// AdMob app ID in Assets > Google Mobile Ads > Settings.
    /// </summary>
    public class AdMobAds : IAdProvider
    {
#if UNITY_IOS
        const string BannerId = "ca-app-pub-3940256099942544/2934735716", InterId = "ca-app-pub-3940256099942544/4411468910", RewardId = "ca-app-pub-3940256099942544/1712485313";
#else
        const string BannerId = "ca-app-pub-3940256099942544/6300978111", InterId = "ca-app-pub-3940256099942544/1033173712", RewardId = "ca-app-pub-3940256099942544/5224354917";
#endif
        GoogleMobileAds.Api.BannerView banner;
        GoogleMobileAds.Api.RewardedAd rewarded;
        GoogleMobileAds.Api.InterstitialAd inter;
        public string Name => "admob";

        public void Init(Action<bool> done)
        {
            GoogleMobileAds.Api.MobileAds.RaiseAdEventsOnUnityMainThread = true;
            GoogleMobileAds.Api.MobileAds.Initialize(_ => { LoadRewarded(); LoadInter(); done(true); });
        }
        void LoadRewarded() => GoogleMobileAds.Api.RewardedAd.Load(RewardId, new GoogleMobileAds.Api.AdRequest(), (ad, err) => { if (err == null && ad != null) rewarded = ad; });
        void LoadInter() => GoogleMobileAds.Api.InterstitialAd.Load(InterId, new GoogleMobileAds.Api.AdRequest(), (ad, err) => { if (err == null && ad != null) inter = ad; });

        public void Rewarded(string placement, Action<bool> done)
        {
            var ad = rewarded;
            if (ad == null || !ad.CanShowAd()) { LoadRewarded(); done(false); return; }
            rewarded = null;
            bool earned = false, called = false;
            void End(bool ok) { if (called) return; called = true; ad.Destroy(); LoadRewarded(); done(ok); }
            ad.OnAdFullScreenContentClosed += () => End(earned);
            ad.OnAdFullScreenContentFailed += _ => End(false);
            ad.Show(_ => earned = true);
        }

        public void Interstitial(string placement, Action done)
        {
            var ad = inter;
            if (ad == null || !ad.CanShowAd()) { LoadInter(); done(); return; }
            inter = null;
            bool called = false;
            void End() { if (called) return; called = true; ad.Destroy(); LoadInter(); done(); }
            ad.OnAdFullScreenContentClosed += End;
            ad.OnAdFullScreenContentFailed += _ => End();
            ad.Show();
        }

        public void Banner(bool show)
        {
            Hud.SetBannerVisible(false);   // the SDK draws its own banner view
            if (show)
            {
                if (banner == null)
                {
                    banner = new GoogleMobileAds.Api.BannerView(BannerId, GoogleMobileAds.Api.AdSize.Banner, GoogleMobileAds.Api.AdPosition.Bottom);
                    banner.LoadAd(new GoogleMobileAds.Api.AdRequest());
                }
                banner.Show();
            }
            else banner?.Hide();
        }
    }
#endif
}
