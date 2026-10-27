using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SudsTycoon
{
    /// <summary>
    /// Every sound is synthesised at startup (port of the WebAudio synth in audio.js), so the game ships with no audio files.
    /// Sounds are rendered once into AudioClips and played on a small pool of voices; pitch carries the per-call variation.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx I;
        const int Rate = 44100;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> last = new Dictionary<string, float>();
        AudioSource[] voices;
        AudioSource music;
        AudioClip gameLoop, introLoop, homeLoop;
        readonly Dictionary<string, AudioClip> worldLoops = new Dictionary<string, AudioClip>();
        int next;
        static bool sfxOn = true, musicOn = true, ducked;
        /// <summary>Every sound that actually plays, with its pitch (the editor-only trailer shoot logs them).</summary>
        internal static event Action<string, float> Played;

        // rate limits in seconds (the web `limit(name, ms)` calls)
        static readonly Dictionary<string, float> Limits = new Dictionary<string, float>
        {
            { "coin", 0.035f }, { "cash", 0.08f }, { "pop", 0.04f }, { "bubble", 0.06f }, { "honk", 0.25f }, { "siren", 1.2f }, { "jingle", 2.5f },
            { "spray", 0.22f }, { "splash", 0.15f }, { "fan", 0.4f }, { "scrub", 0.26f }, { "tick", 0.03f }, { "pew", 0.045f }, { "splat", 0.05f },
        };

        void Awake()
        {
            I = this;
            voices = new AudioSource[14];
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].volume = 0.62f; }
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true; music.playOnAwake = false; music.volume = 0.29f;   // the loop is rendered at half level to avoid clipping
            Build();
        }

        // =====================================================================
        // Public API
        // =====================================================================
        public static void Play(string name, float arg = 0f)
        {
            if (!I || !sfxOn) return;
            if (Limits.TryGetValue(name, out var lim))
            {
                float t = U.Clock;
                if (I.last.TryGetValue(name, out var lt) && t - lt < lim) return;
                I.last[name] = t;
            }
            if (!I.clips.TryGetValue(name, out var clip)) return;
            float pitch = 1f;
            switch (name)
            {
                case "coin": pitch = Mathf.Pow(2f, Mathf.Min(arg, 20f) / 12f); break;
                case "honk": pitch = arg > 0f ? arg : 1f; break;
                case "pop": pitch = U.Rand(500f, 800f) / 650f; break;
                case "bubble": pitch = U.Rand(0.8f, 1.2f); break;
            }
            var v = I.voices[I.next = (I.next + 1) % I.voices.Length];
            v.pitch = pitch;
            v.clip = clip;
            v.Play();
            Played?.Invoke(name, pitch);
        }
        public static void SetSfx(bool on) { sfxOn = on; }
        public static void SetMusic(bool on)
        {
            musicOn = on;
            if (!I) return;
            if (on && !I.music.isPlaying) I.music.Play();
            else if (!on) I.music.Stop();
        }
        public static void StartMusic() { if (I && musicOn && !I.music.isPlaying) I.music.Play(); }
        /// <summary>The title screen's own tune (music on or off comes from the save).</summary>
        public static void PlayIntro(bool on)
        {
            musicOn = on;
            if (!I) return;
            I.StopAllCoroutines();
            I.music.clip = I.introLoop;
            I.music.volume = 0.34f;
            if (on) I.music.Play(); else I.music.Stop();
        }
        /// <summary>Fade the title tune out and hand over to the game loop.</summary>
        public static void EndIntro() { if (I && I.music.clip == I.introLoop) I.StartCoroutine(I.SwapToGameLoop()); }
        /// <summary>Each world region has its own tune (built the first time you get there); crossfades when you travel.</summary>
        public static void SetWorld(string region)
        {
            if (!I) return;
            if (!I.worldLoops.TryGetValue(region ?? "", out var clip))
                I.worldLoops[region ?? ""] = clip = Songs.TryGetValue(region ?? "", out var song) ? I.RenderSong(song, region) : I.homeLoop;
            if (clip == I.gameLoop) return;
            I.gameLoop = clip;
            if (I.music.clip == I.introLoop || !I.music.isPlaying) { if (I.music.clip != I.introLoop) I.music.clip = clip; return; }
            I.StopAllCoroutines();
            I.StartCoroutine(I.SwapToGameLoop());
        }
        IEnumerator SwapToGameLoop()
        {
            float v0 = music.volume;
            for (float k = 0f; k < 0.45f; k += Time.unscaledDeltaTime) { music.volume = Mathf.Lerp(v0, 0f, k / 0.45f); yield return null; }
            music.Stop();
            music.clip = gameLoop;
            music.volume = 0.29f;
            if (musicOn) music.Play();
        }
        /// <summary>Mute everything while an ad plays.</summary>
        public static void Duck(bool on) { ducked = on; AudioListener.volume = on ? 0f : 1f; }

        // =====================================================================
        // Synth
        // =====================================================================
        enum Wave { Sine, Square, Saw, Triangle }
        enum Filt { None, Low, High, Band }

        class Buf
        {
            public float[] d;
            public Buf(float secs) { d = new float[Mathf.CeilToInt(secs * Rate)]; }
        }

        static float Osc(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Square: return p < 0.5 ? 1f : -1f;
                case Wave.Saw: return (float)(2 * p - 1);
                case Wave.Triangle: return (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p);
                default: return Mathf.Sin((float)(p * Math.PI * 2));
            }
        }

        /// <summary>Exponential attack to vol, then exponential decay to silence at dur (WebAudio exponentialRampToValueAtTime).</summary>
        static float Env(float t, float dur, float vol, float attack)
        {
            const float floor = 0.0001f;
            if (t < 0f || t > dur) return 0f;
            if (t < attack) return floor * Mathf.Pow(vol / floor, t / attack);
            return vol * Mathf.Pow(floor / vol, (t - attack) / Mathf.Max(0.0001f, dur - attack));
        }

        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public void Set(Filt type, float freq, float q)
            {
                freq = Mathf.Clamp(freq, 10f, Rate * 0.45f);
                float w0 = 2f * Mathf.PI * freq / Rate, cos = Mathf.Cos(w0), alpha = Mathf.Sin(w0) / (2f * Mathf.Max(0.05f, q));
                float a0;
                switch (type)
                {
                    case Filt.High: b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2; break;
                    case Filt.Band: b0 = alpha; b1 = 0; b2 = -alpha; break;
                    default: b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2; break;
                }
                a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            }
            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static void Tone(Buf b, float freq, float dur, Wave w, float vol, float delay = 0f, float to = 0f, float slide = 0f, float lp = 0f, float detune = 0f, float attack = 0.005f)
        {
            int s0 = Mathf.FloorToInt(delay * Rate), n = Mathf.CeilToInt((dur + 0.05f) * Rate);
            double phase = 0;
            float f0 = freq * Mathf.Pow(2f, detune / 1200f), fTo = Mathf.Max(20f, to * Mathf.Pow(2f, detune / 1200f));
            float sl = slide > 0f ? slide : dur;
            var filt = new Biquad();
            if (lp > 0f) filt.Set(Filt.Low, lp, 0.707f);
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = to > 0f ? (t < sl ? f0 * Mathf.Pow(fTo / f0, t / sl) : fTo) : f0;
                phase += f / Rate;
                float x = Osc(w, phase) * Env(t, dur, vol, attack);
                if (lp > 0f) x = filt.Run(x);
                b.d[s0 + i] += x;
            }
        }

        static void Noise(Buf b, float dur, float vol, float freq, Filt type, float delay = 0f, float to = 0f, float q = 1f, float attack = 0.01f)
        {
            int s0 = Mathf.FloorToInt(delay * Rate), n = Mathf.CeilToInt((dur + 0.05f) * Rate);
            var filt = new Biquad();
            filt.Set(type, freq, type == Filt.Band ? q : 0.707f);
            var rng = new System.Random(1234 + (int)(freq * 7) + (int)(delay * 1000));
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float t = (float)i / Rate;
                if (to > 0f && (i & 31) == 0) filt.Set(type, freq * Mathf.Pow(to / freq, Mathf.Clamp01(t / dur)), type == Filt.Band ? q : 0.707f);
                float x = (float)(rng.NextDouble() * 2 - 1);
                b.d[s0 + i] += filt.Run(x) * Env(t, dur, vol, attack);
            }
        }

        AudioClip Clip(string name, Buf b)
        {
            var c = AudioClip.Create(name, b.d.Length, 1, Rate, false);
            c.SetData(b.d, 0);
            clips[name] = c;
            return c;
        }

        void Build()
        {
            Buf B(float s) => new Buf(s);
            Buf x;
            x = B(0.3f); Tone(x, 988, 0.08f, Wave.Square, 0.05f, lp: 4000); Tone(x, 988 * 1.5f, 0.14f, Wave.Triangle, 0.07f, delay: 0.035f); Clip("coin", x);
            x = B(0.45f); Noise(x, 0.08f, 0.15f, 3000, Filt.High); Tone(x, 1568, 0.12f, Wave.Square, 0.05f, delay: 0.05f, lp: 5000); Tone(x, 2093, 0.25f, Wave.Triangle, 0.09f, delay: 0.1f); Clip("cash", x);
            x = B(0.15f); Tone(x, 650, 0.07f, Wave.Sine, 0.16f, to: 180, slide: 0.06f); Clip("pop", x);
            x = B(0.2f); Tone(x, 400, 0.12f, Wave.Sine, 0.1f, to: 1100, slide: 0.1f); Clip("bubble", x);
            x = B(0.35f); Tone(x, 1800, 0.18f, Wave.Sine, 0.08f, to: 2700, slide: 0.08f); Tone(x, 2600, 0.12f, Wave.Sine, 0.06f, delay: 0.1f, to: 1700); Clip("squeak", x);
            x = B(0.36f); Tone(x, 330, 0.28f, Wave.Saw, 0.07f, lp: 1200, detune: 8); Tone(x, 415, 0.28f, Wave.Saw, 0.06f, lp: 1200); Clip("honk", x);
            x = B(0.34f); Tone(x, 560, 0.12f, Wave.Saw, 0.07f, to: 380, lp: 1500); Tone(x, 520, 0.12f, Wave.Saw, 0.06f, delay: 0.14f, to: 340, lp: 1500); Clip("quack", x);
            x = B(1.0f); for (int i = 0; i < 4; i++) Tone(x, i % 2 == 1 ? 900 : 700, 0.22f, Wave.Triangle, 0.05f, delay: i * 0.22f); Clip("siren", x);
            x = B(1.0f); float[] jin = { 784, 988, 1175, 988, 784, 659 }; for (int i = 0; i < jin.Length; i++) Tone(x, jin[i], 0.16f, Wave.Triangle, 0.05f, delay: i * 0.14f); Clip("jingle", x);
            x = B(0.1f); Tone(x, 1200, 0.04f, Wave.Square, 0.04f, lp: 3000); Clip("click", x);
            x = B(0.1f); Tone(x, 700, 0.05f, Wave.Sine, 0.08f, to: 1000); Clip("tap", x);
            x = B(0.35f); Tone(x, 160, 0.16f, Wave.Square, 0.06f, lp: 800); Tone(x, 120, 0.2f, Wave.Square, 0.06f, delay: 0.08f, lp: 800); Clip("error", x);
            x = B(0.42f); Noise(x, 0.35f, 0.12f, 400, Filt.Band, to: 2400, q: 0.8f); Clip("whoosh", x);
            x = B(0.36f); Noise(x, 0.3f, 0.06f, 2500, Filt.Band, q: 0.6f); Clip("spray", x);
            x = B(0.32f); Noise(x, 0.25f, 0.1f, 900, Filt.Low, to: 300); Clip("splash", x);
            x = B(0.52f); Noise(x, 0.45f, 0.05f, 600, Filt.Band, to: 900, q: 0.5f); Clip("fan", x);
            x = B(0.28f); Noise(x, 0.22f, 0.06f, 1500, Filt.Band, q: 2f); Clip("scrub", x);
            x = B(0.6f); float[] bld = { 523, 659, 784, 1047 }; for (int i = 0; i < bld.Length; i++) Tone(x, bld[i], 0.18f, Wave.Triangle, 0.09f, delay: i * 0.07f); Noise(x, 0.2f, 0.08f, 300, Filt.Low, delay: 0.28f); Clip("build", x);
            x = B(0.55f); float[] upg = { 659, 880, 1319 }; for (int i = 0; i < upg.Length; i++) Tone(x, upg[i], 0.14f, Wave.Square, 0.045f, delay: i * 0.06f, lp: 3500); Tone(x, 1760, 0.3f, Wave.Triangle, 0.06f, delay: 0.18f); Clip("upgrade", x);
            x = B(1.2f); float[] lvl = { 523, 659, 784, 1047, 1319, 1568 }; for (int i = 0; i < lvl.Length; i++) Tone(x, lvl[i], 0.25f, Wave.Triangle, 0.09f, delay: i * 0.08f); Tone(x, 2093, 0.6f, Wave.Sine, 0.08f, delay: 0.5f); Clip("levelup", x);
            x = B(0.5f); float[] rew = { 784, 1047, 1319, 1568 }; for (int i = 0; i < rew.Length; i++) Tone(x, rew[i], 0.2f, Wave.Triangle, 0.08f, delay: i * 0.06f); Clip("reward", x);
            x = B(0.5f); Tone(x, 220, 0.3f, Wave.Saw, 0.07f, to: 90, lp: 900); Noise(x, 0.4f, 0.1f, 500, Filt.Low, delay: 0.05f); Clip("rage", x);
            x = B(0.42f); Noise(x, 0.35f, 0.14f, 700, Filt.Low, to: 200); Clip("poof", x);
            x = B(0.7f); Tone(x, 110, 0.6f, Wave.Saw, 0.025f, lp: 400, to: 140, slide: 0.5f); Clip("snore", x);
            x = B(0.16f); Tone(x, 600, 0.1f, Wave.Square, 0.04f, to: 1200, lp: 2500); Clip("wake", x);
            x = B(0.08f); Tone(x, 1500, 0.025f, Wave.Square, 0.03f, lp: 4000); Clip("tick", x);
            x = B(0.14f); Tone(x, 900, 0.08f, Wave.Sine, 0.05f, to: 400); Clip("pew", x);
            x = B(0.18f); Noise(x, 0.12f, 0.12f, 500, Filt.Low, to: 150); Clip("splat", x);
            x = B(0.7f); Noise(x, 0.6f, 0.25f, 300, Filt.Low, to: 60); Tone(x, 80, 0.5f, Wave.Sine, 0.15f, to: 40); Clip("boom", x);
            x = B(1.0f); float[] win = { 523, 659, 784, 659, 784, 1047 }; for (int i = 0; i < win.Length; i++) Tone(x, win[i], 0.22f, Wave.Square, 0.05f, delay: i * 0.12f, lp: 3000); Clip("win", x);
            x = B(0.9f); float[] lose = { 392, 349, 330, 262 }; for (int i = 0; i < lose.Length; i++) Tone(x, lose[i], 0.3f, Wave.Triangle, 0.08f, delay: i * 0.18f); Clip("lose", x);
            x = B(0.6f); float[] hire = { 440, 554, 659 }; for (int i = 0; i < hire.Length; i++) Tone(x, hire[i], 0.16f, Wave.Triangle, 0.08f, delay: i * 0.08f); Tone(x, 880, 0.3f, Wave.Sine, 0.06f, delay: 0.25f); Clip("hire", x);
            x = B(0.35f); float[] gem = { 1319, 1760, 2349 }; for (int i = 0; i < gem.Length; i++) Tone(x, gem[i], 0.18f, Wave.Sine, 0.07f, delay: i * 0.05f); Clip("gem", x);
            music.clip = gameLoop = homeLoop = BuildMusic();
            introLoop = BuildIntroMusic();
        }

        // ---------- world music: one 4-bar loop per region, rendered from a small song description ----------
        class Song
        {
            public float bpm;
            public int[][] chords;            // per bar: bass root then three chord notes (MIDI)
            public int[] mel;                 // sixteenths, -1 = rest (repeats to fill 64 steps)
            public Wave lead = Wave.Sine;
            public float leadVol = 0.16f, leadLen = 1.8f, leadLp;
            public bool octave, detune, echo, arp, pad;
            public string bass = "pulse", drums = "pop";
        }
        static readonly Dictionary<string, Song> Songs = new Dictionary<string, Song>
        {
            // country road trip in G: boom-chick bass and a twangy lead
            { "USA", new Song { bpm = 120, bass = "boomchick", drums = "pop", lead = Wave.Saw, leadLp = 2200, leadVol = 0.11f, leadLen = 1.4f,
                chords = new[] { new[] { 43, 55, 59, 62 }, new[] { 48, 55, 60, 64 }, new[] { 50, 57, 62, 66 }, new[] { 43, 55, 59, 62 } },
                mel = new[] { 67, -1, 71, 74, 76, -1, 74, 71, 72, -1, 76, 79, 76, -1, 74, 72, 74, -1, 78, 81, 79, -1, 78, 74, 71, -1, 74, 71, 67, -1, -1, -1 } } },
            // Paris cafe accordion in A minor: musette (two detuned reeds) over soft brushes
            { "Europe", new Song { bpm = 104, bass = "boomchick", drums = "soft", lead = Wave.Square, leadLp = 2600, leadVol = 0.07f, leadLen = 1.7f, detune = true,
                chords = new[] { new[] { 45, 57, 60, 64 }, new[] { 50, 57, 62, 65 }, new[] { 52, 56, 59, 62 }, new[] { 45, 57, 60, 64 } },
                mel = new[] { 69, -1, 72, 76, 75, -1, 76, -1, 74, -1, 72, 71, 72, -1, -1, -1, 71, -1, 74, 77, 76, -1, 74, -1, 72, -1, 71, 68, 69, -1, -1, -1 } } },
            // pentatonic koto plucks with taiko drums
            { "Asia", new Song { bpm = 96, bass = "octave", drums = "taiko", lead = Wave.Triangle, leadVol = 0.2f, leadLen = 1.1f, octave = true, echo = true,
                chords = new[] { new[] { 45, 57, 60, 64 }, new[] { 41, 57, 60, 65 }, new[] { 43, 55, 59, 62 }, new[] { 40, 55, 59, 64 } },
                mel = new[] { 69, -1, 72, 74, 76, -1, 74, 72, 74, -1, 76, 79, 76, -1, -1, -1, 81, -1, 79, 76, 74, -1, 76, 74, 72, -1, 69, 72, 69, -1, -1, -1 } } },
            // tropical samba: marimba, syncopated bass and shakers
            { "Around the World", new Song { bpm = 118, bass = "samba", drums = "shaker", lead = Wave.Sine, leadVol = 0.18f, leadLen = 0.9f, octave = true,
                chords = new[] { new[] { 41, 57, 60, 65 }, new[] { 46, 58, 62, 65 }, new[] { 48, 55, 60, 64 }, new[] { 41, 57, 60, 65 } },
                mel = new[] { 72, -1, 74, 77, -1, 79, 77, -1, 74, 72, -1, 74, 77, -1, -1, -1, 81, -1, 79, 77, -1, 74, 77, -1, 79, 77, 74, 72, 72, -1, -1, -1 } } },
            // synthwave in space: pads, arpeggios and echoes
            { "Space", new Song { bpm = 100, bass = "drone", drums = "synth", lead = Wave.Sine, leadVol = 0.12f, leadLen = 3f, echo = true, arp = true, pad = true,
                chords = new[] { new[] { 45, 57, 60, 64 }, new[] { 41, 57, 60, 65 }, new[] { 48, 55, 60, 64 }, new[] { 43, 55, 59, 62 } },
                mel = new[] { 76, -1, -1, -1, 74, -1, 72, -1, 69, -1, -1, -1, -1, -1, -1, -1, 72, -1, -1, -1, 74, -1, 76, -1, 79, -1, -1, -1, 74, -1, -1, -1 } } },
        };

        AudioClip RenderSong(Song sg, string name)
        {
            float s16 = 60f / sg.bpm / 4f, loop = 64 * s16;
            float Mtof(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);
            var b = new Buf(loop + 1.2f);
            for (int step = 0; step < 64; step++)
            {
                float t = step * s16;
                int bar = step / 16, s = step % 16;
                var ch = sg.chords[bar];
                switch (sg.bass)
                {
                    case "boomchick":   // root / fifth on the beat, a short chord "chick" on the off-beat
                        if (s % 4 == 0) Tone(b, Mtof(ch[0] - 12 + (s % 8 == 4 ? 7 : 0)), s16 * 2f, Wave.Triangle, 0.45f, delay: t, attack: 0.008f);
                        if (s % 4 == 2) for (int k = 1; k < 4; k++) Tone(b, Mtof(ch[k]), s16 * 0.9f, Wave.Square, 0.035f, delay: t, lp: 2400);
                        break;
                    case "samba":
                        if (s == 0 || s == 3 || s == 6 || s == 8 || s == 11 || s == 14) Tone(b, Mtof(ch[0] - 12 + (s == 6 || s == 14 ? 7 : 0)), s16 * 1.6f, Wave.Triangle, 0.42f, delay: t, attack: 0.008f);
                        break;
                    case "octave":
                        if (s % 2 == 0) Tone(b, Mtof(ch[0] - 12 + (s % 4 == 2 ? 12 : 0)), s16 * 1.6f, Wave.Triangle, 0.36f, delay: t, attack: 0.008f);
                        break;
                    case "drone":
                        if (s % 2 == 0) Tone(b, Mtof(ch[0] - 12), s16 * 1.8f, Wave.Saw, 0.12f, delay: t, lp: 600, attack: 0.01f);
                        break;
                    default:
                        if (s % 4 == 0) Tone(b, Mtof(ch[0] - 12), s16 * 3, Wave.Triangle, 0.5f, delay: t, attack: 0.01f);
                        break;
                }
                if (sg.pad && s == 0) for (int k = 1; k < 4; k++) Tone(b, Mtof(ch[k]), s16 * 15f, Wave.Saw, 0.03f, delay: t, lp: 1400, attack: 0.25f);
                if (sg.arp) Tone(b, Mtof(ch[1 + s % 3] + (s % 6 >= 3 ? 12 : 0)), s16 * 0.9f, Wave.Sine, 0.06f, delay: t, attack: 0.004f);
                int m = sg.mel[step % sg.mel.Length];
                if (m > 0)
                {
                    Tone(b, Mtof(m), s16 * sg.leadLen, sg.lead, sg.leadVol, delay: t, lp: sg.leadLp, attack: 0.006f);
                    if (sg.detune) Tone(b, Mtof(m), s16 * sg.leadLen, sg.lead, sg.leadVol * 0.8f, delay: t, lp: sg.leadLp, detune: 14f, attack: 0.006f);
                    if (sg.octave) Tone(b, Mtof(m + 12), s16 * sg.leadLen * 0.6f, Wave.Sine, sg.leadVol * 0.3f, delay: t, attack: 0.004f);
                    if (sg.echo) Tone(b, Mtof(m), s16 * sg.leadLen, sg.lead, sg.leadVol * 0.35f, delay: t + s16 * 3f, lp: sg.leadLp, attack: 0.006f);
                }
                switch (sg.drums)
                {
                    case "soft":
                        if (s % 8 == 0) Tone(b, 120, 0.14f, Wave.Sine, 0.3f, delay: t, to: 50, slide: 0.1f, attack: 0.002f);
                        if (s % 8 == 4) Noise(b, 0.16f, 0.07f, 1200, Filt.Band, delay: t, q: 0.6f, attack: 0.01f);
                        break;
                    case "taiko":
                        if (s == 0 || s == 10) Tone(b, 110, 0.3f, Wave.Sine, 0.5f, delay: t, to: 55, slide: 0.2f, attack: 0.002f);
                        if (s % 8 == 6) Tone(b, 1400, 0.03f, Wave.Square, 0.03f, delay: t, lp: 3000, attack: 0.001f);
                        break;
                    case "shaker":
                        if (s % 8 == 0) Tone(b, 130, 0.14f, Wave.Sine, 0.45f, delay: t, to: 45, slide: 0.1f, attack: 0.002f);
                        if (s % 8 == 4) Noise(b, 0.1f, 0.12f, 1800, Filt.Band, delay: t, q: 0.9f, attack: 0.001f);
                        Noise(b, 0.035f, s % 4 == 2 ? 0.06f : 0.03f, 6500, Filt.High, delay: t, attack: 0.001f);
                        break;
                    case "synth":
                        if (s % 4 == 0) Tone(b, 130, 0.18f, Wave.Sine, 0.5f, delay: t, to: 40, slide: 0.14f, attack: 0.002f);
                        if (s % 8 == 4) Noise(b, 0.25f, 0.1f, 1500, Filt.Band, delay: t, q: 0.7f, attack: 0.002f);
                        if (s % 4 == 2) Noise(b, 0.05f, 0.04f, 8000, Filt.High, delay: t, attack: 0.001f);
                        break;
                    default:
                        if (s % 8 == 0) Tone(b, 140, 0.16f, Wave.Sine, 0.45f, delay: t, to: 45, slide: 0.12f, attack: 0.002f);
                        if (s % 8 == 4) Noise(b, 0.12f, 0.14f, 1800, Filt.Band, delay: t, q: 0.9f, attack: 0.001f);
                        if (s % 2 == 0) Noise(b, 0.04f, 0.04f, 7000, Filt.High, delay: t, attack: 0.001f);
                        break;
                }
            }
            int len = Mathf.CeilToInt(loop * Rate);
            var outp = new float[len];
            for (int i = 0; i < b.d.Length; i++) outp[i % len] += b.d[i] * 0.5f;
            for (int i = 0; i < len; i++) { float x = Mathf.Clamp(outp[i], -1f, 1f); outp[i] = x * (1.5f - 0.5f * x * x); }
            var c = AudioClip.Create("music_" + name, len, 1, Rate, false);
            c.SetData(outp, 0);
            return c;
        }

        // ---------- intro: bubbly title-screen theme, 128 bpm, C - G - Am - F (4 bars, 64 sixteenths) ----------
        AudioClip BuildIntroMusic()
        {
            const float bpm = 128f, s16 = 60f / bpm / 4f;
            int[][] chords = { new[] { 48, 55, 64, 67 }, new[] { 43, 50, 59, 62 }, new[] { 45, 52, 60, 64 }, new[] { 41, 48, 57, 60 } };
            int[] mel =
            {
                72, -1, 76, -1, 79, -1, 84, -1, 83, -1, 79, -1, 76, -1, 79, -1,
                79, -1, 74, -1, 71, -1, 74, -1, 79, -1, 83, -1, 81, -1, 79, -1,
                76, -1, 72, -1, 69, -1, 72, -1, 76, -1, 81, -1, 79, -1, 76, -1,
                77, -1, 76, -1, 74, -1, 72, -1, 74, -1, 76, -1, 79, -1, -1, -1,
            };
            float Mtof(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);
            float loop = 64 * s16;
            var b = new Buf(loop + 0.8f);
            for (int step = 0; step < 64; step++)
            {
                float t = step * s16;
                int bar = step / 16, s = step % 16;
                var ch = chords[bar];
                // bouncy octave bass on the eighths
                if (s % 2 == 0) Tone(b, Mtof(ch[0] - 12 + (s % 4 == 2 ? 12 : 0)), s16 * 1.6f, Wave.Triangle, 0.4f, delay: t, attack: 0.008f);
                // off-beat chord stabs
                if (s % 4 == 2)
                {
                    Tone(b, Mtof(ch[2]), s16 * 1.1f, Wave.Square, 0.045f, delay: t, lp: 2600);
                    Tone(b, Mtof(ch[3]), s16 * 1.1f, Wave.Square, 0.04f, delay: t, lp: 2600);
                    Tone(b, Mtof(ch[1] + 12), s16 * 1.1f, Wave.Square, 0.035f, delay: t, lp: 2600);
                }
                // lead: a bright bell (sine plus a soft octave)
                int m = mel[step];
                if (m > 0) { Tone(b, Mtof(m), s16 * 2.2f, Wave.Sine, 0.17f, delay: t, attack: 0.006f); Tone(b, Mtof(m + 12), s16 * 1.2f, Wave.Triangle, 0.04f, delay: t, attack: 0.004f); }
                // drums: kick on 1 and 3, clap on 2 and 4, hats on the eighths
                if (s % 8 == 0) Tone(b, 140, 0.16f, Wave.Sine, 0.5f, delay: t, to: 45, slide: 0.12f, attack: 0.002f);
                if (s % 8 == 4) Noise(b, 0.12f, 0.16f, 1800, Filt.Band, delay: t, q: 0.9f, attack: 0.001f);
                if (s % 2 == 0) Noise(b, 0.04f, 0.05f, 7000, Filt.High, delay: t, attack: 0.001f);
                // a rising bubble "bloop" at the end of every second bar
                if (s == 14 && bar % 2 == 1) Tone(b, 420, 0.14f, Wave.Sine, 0.14f, delay: t, to: 1250, slide: 0.12f);
            }
            // fold the tail back to the start for a seamless loop, with a gentle soft clip
            int len = Mathf.CeilToInt(loop * Rate);
            var outp = new float[len];
            for (int i = 0; i < b.d.Length; i++) outp[i % len] += b.d[i] * 0.5f;
            for (int i = 0; i < len; i++) { float x = Mathf.Clamp(outp[i], -1f, 1f); outp[i] = x * (1.5f - 0.5f * x * x); }
            var c = AudioClip.Create("intro", len, 1, Rate, false);
            c.SetData(outp, 0);
            return c;
        }

        // ---------- music: bouncy loop in C major, 112 bpm (4 bars, 64 sixteenths) ----------
        AudioClip BuildMusic()
        {
            const float bpm = 112f, s16 = 60f / bpm / 4f;
            int[][] chords = { new[] { 48, 55, 64, 67 }, new[] { 45, 52, 60, 64 }, new[] { 41, 48, 57, 60 }, new[] { 43, 50, 59, 62 } };
            int[] mel = { 72, -1, 76, 79, 76, -1, 74, 72, 69, -1, 72, 74, 72, -1, 67, -1, 65, -1, 69, 72, 69, -1, 67, 65, 67, -1, 71, 74, 72, -1, -1, -1 };
            float Mtof(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);
            float loop = 64 * s16;
            var b = new Buf(loop + 0.6f);
            for (int step = 0; step < 64; step++)
            {
                float t = step * s16;
                int bar = step / 16 % 4, s = step % 16;
                var ch = chords[bar];
                if (s % 4 == 0) Tone(b, Mtof(ch[0] - 12), s16 * 3, Wave.Triangle, 0.5f, delay: t, attack: 0.01f);
                if (s % 4 == 2)
                {
                    Tone(b, Mtof(ch[0]), s16 * 1.5f, Wave.Triangle, 0.25f, delay: t, attack: 0.01f);
                    Tone(b, Mtof(ch[2]), s16 * 1.2f, Wave.Square, 0.05f, delay: t, attack: 0.01f);
                    Tone(b, Mtof(ch[3]), s16 * 1.2f, Wave.Square, 0.04f, delay: t, attack: 0.01f);
                }
                int mm = mel[step % 32];
                if (mm > 0 && (step / 32 % 2 == 0 || s % 2 == 0)) Tone(b, Mtof(mm), s16 * 1.8f, Wave.Sine, 0.16f, delay: t, attack: 0.01f);
                if (s % 8 == 4) Noise(b, 0.08f, 0.12f, 2500, Filt.High, delay: t, attack: 0.001f);
            }
            // fold the tail past the loop point back to the start so the loop is seamless
            int len = Mathf.CeilToInt(loop * Rate);
            var outp = new float[len];
            for (int i = 0; i < b.d.Length; i++) outp[i % len] += b.d[i] * 0.5f;
            var c = AudioClip.Create("music", len, 1, Rate, false);
            c.SetData(outp, 0);
            return c;
        }

        void OnApplicationPause(bool paused) { if (!ducked) AudioListener.volume = paused ? 0f : 1f; }
    }
}
