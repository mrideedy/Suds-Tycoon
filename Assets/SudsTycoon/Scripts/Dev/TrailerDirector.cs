#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>
    /// Editor-only trailer shoot. While Trailer/shoot.txt exists, entering Play mode films scripted shots at a fixed
    /// frame rate into Trailer/raw/&lt;shot&gt;/0000.jpg (every sound effect and its frame go to sfx.txt next to them),
    /// exports the synthesised music and sound effects to Trailer/audio as WAV files, then deletes shoot.txt and
    /// leaves Play mode. shoot.txt holds key=value lines: shots=title,hook,... (all when missing) and stride=N
    /// (keep every Nth frame, for a quick framing test). The player's save is never written during a shoot.
    /// </summary>
    public class TrailerDirector : MonoBehaviour
    {
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trailer"));
        static string FlagPath => Path.Combine(Dir, "shoot.txt");
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags PrivI = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly string[] Tour = { "downtown", "beach", "desert", "snow", "city", "london", "paris", "alps", "dubai", "mumbai", "tokyo", "sydney", "rio", "cairo", "moon", "mars" };
        static readonly string[] TourHats = { "cone", "cap", "cowboy", "cap", "crown", "cap", "chef", "cap", "crown", "cap", "cap", "duck", "duck", "cowboy", "bubble", "bubble" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Store.NoWrite = File.Exists(FlagPath);
            if (!Store.NoWrite) return;
            Application.runInBackground = true;   // keep filming while the editor is in the background
            new GameObject("TrailerDirector").AddComponent<TrailerDirector>();
        }

        HashSet<string> want;
        int stride = 1, fps = 30, frame;
        StreamWriter sfxLog, log;

        bool Want(string s) => want == null || want.Contains(s);
        void Log(string s) { log?.WriteLine(s); Debug.Log("[trailer] " + s); }

        IEnumerator Start()
        {
            foreach (var line in File.ReadAllLines(FlagPath))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                if (k == "shots" && v.Length > 0) want = new HashSet<string>(v.Split(',').Select(x => x.Trim()));
                if (k == "stride") int.TryParse(v, out stride);
            }
            stride = Mathf.Max(1, stride);
            Directory.CreateDirectory(Path.Combine(Dir, "raw"));
            log = new StreamWriter(Path.Combine(Dir, "raw", "shoot.log"), false) { AutoFlush = true };
            Log($"start {DateTime.Now:HH:mm:ss} screen {Screen.width}x{Screen.height} stride {stride}");
            var game = FindAnyObjectByType<SudsGame>();
            if (!game) { Log("!! no SudsGame in this scene: open Assets/SudsTycoon/Scenes/SudsTycoon.unity"); Finish(); yield break; }
            EditorUtility.audioMasterMute = true;
            Sfx.Played += OnSfx;
            SetFps(30);
            // the auto quality check would measure the fixed 30 fps capture rate and turn shadows off
            typeof(SudsGame).GetField("qualityChecked", PrivI)?.SetValue(game, true);

            // the title screen plays straight from boot
            if (Want("title")) yield return Film("title", 6.5f, null, _ => { var s = Hud.Root.Q(className: "intro-start"); if (s != null) s.style.visibility = Visibility.Hidden; });
            while (!Game.Running) yield return null;
            for (int i = 0; i < 5; i++) yield return null;
            Hud.AnyKey();
            // start-up popups (offline earnings, daily reward) chain one after another: close them as they come
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until) { if (Hud.PanelOpen) Hud.ClosePanel(true); yield return null; }
            Ads.UseFreeRewards();   // what the store build shows: gift icons, no banner
            if (Want("audio")) ExportAudio();

            var shots = new (string name, Func<IEnumerator> run)[]
            {
                ("hook", ShotHook), ("early", ShotEarly), ("build", ShotBuild), ("crew", ShotCrew), ("upgrades", ShotUpgrades), ("rush", ShotRush),
                ("cities", ShotCities), ("perk", ShotPerk), ("duck", ShotDuck), ("blaster", ShotBlaster), ("map", ShotMap),
            };
            foreach (var (name, run) in shots)
            {
                if (!Want(name) && !(name == "cities" && want.Any(w => w.StartsWith("c_") || w.StartsWith("d_")))) continue;
                Log($"shot {name} {DateTime.Now:HH:mm:ss}");
                yield return Safe(run(), name);
                SetFps(30);
                ResetCam();
                Game.Move = Vector2.zero;
            }
            Finish();
        }

        IEnumerator Safe(IEnumerator e, string name)
        {
            while (true)
            {
                object cur;
                try { if (!e.MoveNext()) yield break; cur = e.Current; }
                catch (Exception ex) { Log($"!! {name}: {ex}"); yield break; }
                yield return cur;
            }
        }

        void Finish()
        {
            Log($"done {DateTime.Now:HH:mm:ss}");
            Sfx.Played -= OnSfx;
            Time.captureFramerate = 0;
            EditorUtility.audioMasterMute = false;
            try { File.Delete(FlagPath); } catch (Exception) { /* ignore */ }
            File.WriteAllText(Path.Combine(Dir, "raw", "done.txt"), DateTime.Now.ToString("s"));
            log.Dispose(); log = null;
            EditorApplication.isPlaying = false;
        }

        void OnSfx(string name, float pitch) => sfxLog?.WriteLine($"{frame}\t{name}\t{pitch:0.###}");
        void SetFps(int n) { fps = n; Time.captureFramerate = n; }

        // =====================================================================
        // Filming
        // =====================================================================
        /// <summary>Film secs of game time (or until stop says so). each runs before the camera; cam places the camera (null: game camera).</summary>
        IEnumerator Film(string name, float secs, Action<float> cam, Action<float> each = null, Func<bool> stop = null)
        {
            var dir = Path.Combine(Dir, "raw", name);
            if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            Directory.CreateDirectory(dir);
            sfxLog = new StreamWriter(Path.Combine(dir, "sfx.txt"), false);
            sfxLog.WriteLine($"# fps {fps}");
            int n = Mathf.RoundToInt(secs * fps);
            for (frame = 0; frame < n; frame++)
            {
                yield return null;
                float t = frame / (float)fps;
                each?.Invoke(t);
                cam?.Invoke(t);
                yield return new WaitForEndOfFrame();
                if (frame % stride == 0) Grab(Path.Combine(dir, frame.ToString("0000") + ".jpg"));
                if (stop != null && stop()) { frame++; break; }
            }
            sfxLog.Dispose(); sfxLog = null;
            Log($"  {name}: {frame} frames at {fps} fps");
        }

        static void Grab(string path)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(path, tex.EncodeToJPG(92)); }
            catch (Exception e) { Debug.LogWarning("[trailer] " + e.Message); }
            finally { Destroy(tex); }
        }

        /// <summary>Same framing maths as CameraRig, with every value free (web coordinates).</summary>
        static void Cam(Vector3 look, float yaw, float pitch, float dist, float fov = 30f)
        {
            var cam = CameraRig.I.Cam;
            cam.fieldOfView = fov;
            var off = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * dist;
            cam.transform.position = U.P(look + off);
            cam.transform.LookAt(U.P(look));
        }
        static void ResetCam() { if (CameraRig.I) { CameraRig.I.Cam.fieldOfView = CameraRig.I.fov; CameraRig.I.zoom = 1f; } }
        float Smooth(float rate) => 1f - Mathf.Exp(-rate / fps);

        // =====================================================================
        // Scene set-up
        // =====================================================================
        static SaveData NewSave(int level, int biz = 6)
        {
            var s = new SaveData { created = U.NowMs, lastSeen = U.NowMs, level = level, unlockedLocs = Cfg.Locations.Count, gems = 480 };
            s.blaster.lastTick = U.NowMs;
            s.daily.last = U.Today();
            s.spin.lastFree = U.NowMs;
            foreach (var h in Cfg.Hats) if (!s.hats.owned.Contains(h.id)) s.hats.owned.Add(h.id);
            foreach (var b in Cfg.Biz) s.biz[b.id] = Math.Min(b.max, biz);
            typeof(Store).GetProperty("S").SetValue(null, s);
            return s;
        }
        static void StartAll() { foreach (var l in Cfg.Locations) Store.Loc(l.id).started = true; }
        static bool Lane1Only(string id) => !id.StartsWith("l2") && id != "lane2" && id != "cashier2" && id != "polisher2";

        /// <summary>A city with its pads built (all, or those keep allows) and every machine at level lv.</summary>
        static LocSave Prep(string id, int lv, Func<string, bool> keep = null, double cash = 0)
        {
            var ls = Store.Loc(id);
            ls.started = true;
            ls.pads.Clear(); ls.paid.Clear(); ls.hoppers.Clear(); ls.rackStock.Clear(); ls.piles.Clear(); ls.trays.Clear();
            foreach (var p in Cfg.Pads) if (keep == null || keep(p.id)) ls.pads.Add(p.id);
            foreach (var t in Cfg.SlotOrder) ls.st[t] = lv;
            ls.cash = cash;
            return ls;
        }

        /// <summary>Load a city, rebuild the HUD for the new save and run warm seconds of simulation so the lot is busy.</summary>
        IEnumerator Go(string id, float warm, bool clean, string hat = "cap", bool rush = false)
        {
            Store.S.hats.on = hat;
            Hud.ClosePanel(true);
            Game.Load(Cfg.LocIndex(id));
            if (rush) { Game.RushHour = true; typeof(Game).GetField("rushT", Priv).SetValue(null, 9999f); }   // more cars, no banner
            Hud.Init();
            SudsGame.SetQuality("high");
            Clean(clean);
            yield return null;
            const float dt = 1f / 30f;
            for (int i = 0, n = Mathf.RoundToInt(warm * 30f); i < n; i++) { Sched.Tick(dt); Game.Update(dt); }
            for (int i = 0; i < 20; i++) yield return null;
        }

        /// <summary>Hide the HUD for clean world shots (the floating cash labels stay; panels the bot closes stay invisible).</summary>
        static void Clean(bool on)
        {
            Hud.HudLayer.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var cls in new[] { "toasts-layer", "edge-arrow", "joystick", "panel-layer" })
            {
                var e = Hud.Root.Q(className: cls);
                if (e != null) e.style.visibility = on ? Visibility.Hidden : Visibility.Visible;
            }
        }

        static Car Spawn(string type, int lane, float x)
        {
            var l = Game.Lanes[lane];
            var car = new Car(type, l, Game.Root, x);
            l.Cars.Add(car); Game.Cars.Add(car);
            return car;
        }

        AutoPlay Bot()
        {
            var bot = gameObject.AddComponent<AutoPlay>();
            bot.Speed = 1; bot.StopAtCity = 0;
            return bot;
        }

        // =====================================================================
        // Shots
        // =====================================================================
        /// <summary>The hook: a filthy monster truck through the whole wash line, camera riding alongside (60 fps for speed ramps).</summary>
        IEnumerator ShotHook()
        {
            NewSave(20); StartAll();
            Prep("suburb", 25, id => Lane1Only(id) && id != "tubeman" && id != "vending");
            yield return Go("suburb", 0f, true);
            var car = Spawn("monster", 0, -40f);
            typeof(Car).GetField("paint", PrivI).SetValue(car, U.Hex(0xe8383d));   // bright red, so the clean reveal pops
            car.DustTint();
            SetFps(60);
            float x = car.X;
            yield return Film("hook", 34f, _ =>
            {
                if (!car.Dead) x = Mathf.Lerp(x, car.X, Smooth(4f));
                Cam(new Vector3(x + 1.5f, 1f, 0.5f), 0.45f, 0.55f, 16f, 34f);
            }, null, () => car.Dead || car.X > Cfg.ExitX + 8f);
        }

        /// <summary>A brand new game: the bot stocks soap, serves, collects, then builds with a cash gift (game camera, HUD on).</summary>
        IEnumerator ShotEarly()
        {
            NewSave(1, 0).gems = 15;
            yield return Go("suburb", 0f, false);
            CameraRig.I.zoom = 0.66f;
            var bot = Bot();
            yield return Film("early", 30f, null);
            Destroy(bot);
        }

        /// <summary>A build spree: plenty of cash and the bot walks from pad to pad (closer game camera, HUD on).</summary>
        IEnumerator ShotBuild()
        {
            NewSave(8, 2);
            Prep("suburb", 3, id => id == "rinse" || id == "cashier1" || id == "dryer" || id == "staff" || id == "stocker1" || id == "travel", 60000);
            yield return Go("suburb", 4f, false);
            CameraRig.I.zoom = 0.72f;
            var bot = Bot();
            yield return Film("build", 30f, null);
            Destroy(bot);
        }

        /// <summary>Staff at work: follow a stocker around a busy Downtown lot.</summary>
        IEnumerator ShotCrew()
        {
            NewSave(20); StartAll();
            Prep("downtown", 20);
            yield return Go("downtown", 12f, true, "cone");
            var bot = Bot();
            var w = Game.Workers.FirstOrDefault(x => x.Role == "stocker");
            var look = w != null ? w.Pos : new Vector3(-8f, 0f, 5f);
            yield return Film("crew", 6f, t =>
            {
                if (w != null) look = Vector3.Lerp(look, w.Pos, Smooth(3f));
                Cam(look + new Vector3(0f, 0.8f, 0f), 0.22f + t * 0.03f, 0.75f, 18f);
            });
            Destroy(bot);
        }

        /// <summary>The Upgrades panel with levels ticking up (HUD on).</summary>
        IEnumerator ShotUpgrades()
        {
            NewSave(14); StartAll();
            Prep("suburb", 6, null, 250000);
            yield return Go("suburb", 8f, false);
            Hud.OpenPanel("upgrades");
            string[] order = { "brush", "rinse", "wax", "dryer", "tire", "polish", "brush", "rinse", "wax", "dryer", "brush", "tire" };
            int k = 0;
            yield return Film("upgrades", 6f, null, _ => { if (frame >= 24 && frame % 12 == 0 && k < order.Length) { Game.BuyStation(order[k++]); Hud.Rerender(); } });
            Hud.ClosePanel(true);
        }

        /// <summary>Rush hour in Las Vegas: the banner, the siren and a wide pull-back over both lanes.</summary>
        IEnumerator ShotRush()
        {
            NewSave(30); StartAll();
            Prep("city", 25, null, 4.2e6);
            yield return Go("city", 10f, false, "crown");
            var bot = Bot();
            var rush = typeof(Game).GetField("nextRush", Priv);
            yield return Film("rush", 7f, t =>
            {
                float k = t / 7f;
                Cam(new Vector3(2f, 0f, 6.5f), Mathf.Lerp(0.12f, 0.40f, k), Mathf.Lerp(0.80f, 0.74f, k), Mathf.Lerp(48f, 60f, k));
            }, _ => { if (frame == 12) rush.SetValue(null, 0f); });
            Destroy(bot);
        }

        /// <summary>The world tour: every city fully built, a few seconds each with one of three camera moves.</summary>
        IEnumerator ShotCities()
        {
            for (int i = 0; i < Tour.Length; i++)
            {
                string id = Tour[i], name = "c_" + id;
                if (!Want("cities") && !Want(name)) continue;
                NewSave(30); StartAll();
                Prep(id, 30);
                yield return Go(id, 26f, true, TourHats[i], true);
                var bot = Bot();
                int kind = i % 3;
                Log($"  city {id}");
                yield return Film(name, 3.2f, t => CityCam(kind, t / 3.2f));
                yield return Film("d_" + id, 3.2f, t => CityCam(3, t / 3.2f));
                Destroy(bot);
                Game.RushHour = false;
            }
        }
        static void CityCam(int kind, float k)
        {
            switch (kind)
            {
                case 0: Cam(Vector3.Lerp(new Vector3(1f, 0f, 5f), new Vector3(-1f, 0f, 3f), k), 0.30f, 0.70f, Mathf.Lerp(60f, 47f, k)); break;
                case 1: Cam(Vector3.Lerp(new Vector3(-9f, 0f, 5f), new Vector3(7f, 0f, 5f), k), 0.22f, 0.62f, 44f); break;
                case 2: Cam(new Vector3(2f, 0f, 6f), Mathf.Lerp(0.02f, 0.50f, k), 0.70f, 52f); break;
                default: Cam(Vector3.Lerp(new Vector3(-11f, 1f, 1f), new Vector3(3f, 1f, 1f), k), 0.38f, 0.60f, 25f, 32f); break;   // close pass along lane 1
            }
        }

        /// <summary>Arriving in Rio for the first time: Franchise Kit, then the New Perk popup with confetti (HUD on).</summary>
        IEnumerator ShotPerk()
        {
            NewSave(30); StartAll();
            var ls = Store.Loc("rio");
            ls.started = false; ls.pads.Clear();
            Store.S.hats.owned.Remove("duck");
            yield return Go("rio", 0f, false, "cowboy");
            yield return Film("perk", 7f, null);
            Hud.ClosePanel(true);
        }

        /// <summary>A parade of silly cars (duck, UFO, ice cream truck, hot dog) rolling into the wash at Palm Beach (60 fps).</summary>
        IEnumerator ShotDuck()
        {
            NewSave(20); StartAll();
            Prep("beach", 15, Lane1Only);
            yield return Go("beach", 0f, true, "duck");
            var duck = Spawn("duck", 0, -30f);
            Spawn("ufo", 0, -37.5f); Spawn("icecream", 0, -45f); Spawn("hotdog", 0, -52.5f);
            SetFps(60);
            float x = duck.X;
            yield return Film("duck", 11f, _ =>
            {
                if (!duck.Dead) x = Mathf.Lerp(x, duck.X, Smooth(5f));
                Cam(new Vector3(x - 3f, 1f, 0.5f), 0.62f, 0.45f, 17f, 34f);
            });
        }

        /// <summary>Bubble Blaster against the Mud Truck boss, the cannon sweeping while it fires.</summary>
        IEnumerator ShotBlaster()
        {
            NewSave(20).blaster.level = 5;
            StartAll();
            Prep("suburb", 20);
            yield return Go("suburb", 2f, false);
            typeof(Blaster).GetMethod("Start", Priv).Invoke(null, null);
            yield return Film("blaster", 10f, null, t => Blaster.Keys(Mathf.Sin(t * 1.4f) * 0.9f, true, t > 5f && t < 5.1f));
            Blaster.ForceStop();
            Hud.ClosePanel(true);
        }

        /// <summary>The World Tour map with every city open.</summary>
        IEnumerator ShotMap()
        {
            NewSave(30); StartAll();
            Prep("paris", 30);
            yield return Go("paris", 4f, false, "chef");
            Hud.OpenPanel("map");
            yield return Film("map", 4f, null);
            Hud.ClosePanel(true);
        }

        // =====================================================================
        // Audio export: every sound effect and every music loop as 16-bit WAV
        // =====================================================================
        void ExportAudio()
        {
            var dir = Path.Combine(Dir, "audio");
            Directory.CreateDirectory(dir);
            var sfx = FindAnyObjectByType<Sfx>();
            var clips = (Dictionary<string, AudioClip>)typeof(Sfx).GetField("clips", PrivI).GetValue(sfx);
            foreach (var kv in clips) WriteWav(Path.Combine(dir, "sfx_" + kv.Key + ".wav"), kv.Value);
            WriteWav(Path.Combine(dir, "music_intro.wav"), (AudioClip)typeof(Sfx).GetField("introLoop", PrivI).GetValue(sfx));
            WriteWav(Path.Combine(dir, "music_Hometown.wav"), (AudioClip)typeof(Sfx).GetField("homeLoop", PrivI).GetValue(sfx));
            var render = typeof(Sfx).GetMethod("RenderSong", PrivI);
            foreach (DictionaryEntry e in (IDictionary)typeof(Sfx).GetField("Songs", Priv).GetValue(null))
                WriteWav(Path.Combine(dir, "music_" + e.Key + ".wav"), (AudioClip)render.Invoke(sfx, new[] { e.Value, e.Key }));
            Log($"audio: {clips.Count} sounds and 7 music loops");
        }

        static void WriteWav(string path, AudioClip clip)
        {
            if (!clip) return;
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = data.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)clip.channels); w.Write(clip.frequency); w.Write(clip.frequency * clip.channels * 2);
                w.Write((short)(clip.channels * 2)); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var f in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(f * 32767f), -32768, 32767));
            }
        }
    }
}
#endif
