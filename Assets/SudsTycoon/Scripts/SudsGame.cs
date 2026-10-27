using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>
    /// Entry point (port of main.js): put this on one GameObject in an empty scene, or use
    /// Tools > Suds Tycoon > Create Game Scene. It creates the camera rig, light, UI document, effects and audio,
    /// loads the save, builds the current location and runs the main loop.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SudsGame : MonoBehaviour
    {
        public static SudsGame I { get; private set; }
        public Light Sun { get; private set; }
        PanelSettings panelSettings;
        int lastW, lastH, fpsN;
        bool started, qualityChecked;
        float t, fpsT;
        long hiddenAt;
        Rect lastSafe;

        // Statics survive play-mode restarts when domain reload is off: reset them first.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Bus.Clear(); Sched.Clear();
            Game.ResetStatics(); Hud.ResetStatics(); WorldUI.ResetStatics(); Blaster.ResetStatics(); Ads.ResetStatics();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var cam = Camera.main;
            if (!cam)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            if (!cam.GetComponent<CameraRig>()) cam.gameObject.AddComponent<CameraRig>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;   // the look is flat colour; tonemapping would shift it
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camData.renderShadows = true;

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { Sun = l; break; }
            if (!Sun) { Sun = new GameObject("Sun").AddComponent<Light>(); Sun.type = LightType.Directional; }
            RenderSettings.skybox = null;

            if (!FindAnyObjectByType<Fx>()) new GameObject("Fx").AddComponent<Fx>();
            if (!FindAnyObjectByType<Sfx>()) new GameObject("Sfx").AddComponent<Sfx>();
            if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // UI: one document, built in code (see Hud)
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "SudsPanelSettings";
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/SudsTheme");
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.clearColor = false;
            UpdateScale();
            var uiGo = new GameObject("UI");
            uiGo.SetActive(false);
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            uiGo.SetActive(true);
            var root = doc.rootVisualElement;
            var uss = Resources.Load<StyleSheet>("UI/Suds");
            if (uss) root.styleSheets.Add(uss);
            // The web build's fonts as SDF font assets (see Fonts.cs)
            Fonts.Init();
            Fonts.ApplyRoot(root);
            Hud.Build(root);
            GameInput.Init(Hud.Touch);
            Hud.ShowLoading();
            root.RegisterCallback<GeometryChangedEvent>(_ => Hud.Responsive());
        }

        IEnumerator Start()
        {
            yield return null;
            bool fresh = Store.Load();
            var s = Store.S;
            double awaySecs = (U.NowMs - (s.lastSeen > 0 ? s.lastSeen : U.NowMs)) / 1000.0;
            s.sessions++;
            Sfx.SetSfx(s.settings.sfx);
            Sfx.PlayIntro(s.settings.music);   // the title screen's own tune; the game loop takes over on tap
            yield return null;
            Ads.Init();
            Game.Load(Mathf.Clamp(s.loc, 0, Mathf.Max(0, s.unlockedLocs - 1)));
            SetQuality(s.settings.quality);
            yield return null;
            Hud.Init();
            Blaster.RefillTickets();
            started = true;
            Hud.LoadingReady(fresh, () => AfterStart(fresh, awaySecs));
        }

        void AfterStart(bool fresh, double awaySecs)
        {
            var s = Store.S;
            Sfx.SetMusic(s.settings.music);
            if (fresh) { Hud.Toast("Welcome to Suds Tycoon! Follow the arrow.", "star"); return; }
            var queue = new Queue<(string, PanelArgs)>();
            var off = Game.OfflineEarnings(awaySecs);
            if (off != null && off.amount > 0) queue.Enqueue(("offline", new PanelArgs { off = off }));
            else if (awaySecs > 600 && Game.Ls.pads.Count >= 3) queue.Enqueue(("noOffline", null));
            if (s.level >= Cfg.Unlocks["daily"] && s.daily.last != U.Today()) queue.Enqueue(("daily", null));
            StartCoroutine(PopupChain(queue));
        }

        static IEnumerator PopupChain(Queue<(string name, PanelArgs args)> queue)
        {
            // chain the next popup when the previous one closes
            while (queue.Count > 0)
            {
                while (Hud.PanelOpen || Blaster.Active) yield return new WaitForSecondsRealtime(0.4f);
                var (name, args) = queue.Dequeue();
                Hud.OpenPanel(name, args);
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        void Update()
        {
            float dt = Mathf.Min(0.05f, U.FrameDt);
            t += dt;
            if (Screen.width != lastW || Screen.height != lastH) UpdateScale();
            // flipping between the two landscape sides moves the notch without resizing the screen
            if (Screen.safeArea != lastSafe) { lastSafe = Screen.safeArea; Hud.Responsive(); }
            Sched.Tick(dt);
            GameInput.Tick(dt);
            if (!started) return;
            if (Blaster.Active) Blaster.Update(dt);
            else
            {
                if (!Game.Paused) { Game.Update(dt); WorldBuilder.Tick(dt); }
                if (Game.Player != null) CameraRig.I.Follow(Game.CamTarget(), dt);
            }
            Hud.Update(dt);
            AutoQuality(dt);
        }

        void LateUpdate()
        {
            if (CameraRig.I) WorldUI.Tick(CameraRig.I.Cam, Mathf.Min(0.05f, U.FrameDt));
        }

        /// <summary>~720 panel px tall in landscape, 430 px wide in portrait (close to the CSS pixels the web layout used).</summary>
        void UpdateScale()
        {
            lastW = Screen.width; lastH = Screen.height;
            bool portrait = lastH > lastW;
            panelSettings.referenceResolution = portrait ? new Vector2Int(430, 932) : new Vector2Int(1280, 720);
            panelSettings.match = portrait ? 0f : 1f;
        }

        // auto quality: drop shadows if the first seconds run slowly
        void AutoQuality(float dt)
        {
            if (qualityChecked || t < 3f) return;
            fpsT += dt; fpsN++;
            if (fpsT < 4f) return;
            qualityChecked = true;
            float fps = fpsN / fpsT;
            if (fps < 32f && Store.S.settings.quality == "high")
            {
                Store.S.settings.quality = "low"; SetQuality("low");
                Hud.Toast("Switched to low graphics for smoother play", "sparkles");
            }
        }

        public static void SetQuality(string q)
        {
            if (!I || !I.Sun) return;
            I.Sun.shadows = q == "low" ? LightShadows.None : LightShadows.Soft;
        }

        // ---------- save on pause / quit ----------
        static void Persist()
        {
            if (Store.S == null) return;
            if (Game.Running) Game.SaveRuntime();
            Store.Save();
        }
        void OnApplicationPause(bool paused)
        {
            if (paused) { hiddenAt = U.NowMs; Persist(); return; }
            if (hiddenAt == 0 || !started) return;
            double away = (U.NowMs - hiddenAt) / 1000.0;
            hiddenAt = 0;
            if (away > 60 && Game.Running && !Blaster.Active)
            {
                var off = Game.OfflineEarnings(away);
                if (off != null && off.amount > 0 && !Hud.PanelOpen) Hud.OpenPanel("offline", new PanelArgs { off = off });
            }
        }
        void OnApplicationQuit() => Persist();

        /// <summary>Wipe the save and start over in the first location (Settings > Reset progress).</summary>
        public static void ResetProgress()
        {
            Blaster.ForceStop();
            Hud.ClosePanel(true);
            Game.Unload();
            WorldUI.ClearAll();
            Store.Wipe();
            Store.Save();
            Game.Load(0);
            Hud.Init();
            Hud.Toast("Fresh start! Follow the arrow.", "star");
        }
    }
}
