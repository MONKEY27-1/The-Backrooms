using UnityEngine;

namespace Backrooms.Audio
{
    /// Sounds synthesized at startup, so there are no audio files to license.
    /// shortcut: placeholder quality; swap in recorded CC0 sounds once the audio pass happens.
    public static class ProceduralAudio
    {
        const int Rate = 44100;
        static AudioClip hum;
        static AudioClip[] footsteps;

        /// Fluorescent ballast hum: 120 Hz mains buzz with harmonics and hiss.
        /// One second holds whole 120 Hz cycles, so it loops without a click.
        public static AudioClip Hum()
        {
            if (hum != null) return hum;
            var data = new float[Rate];
            var rng = new System.Random(120);
            for (int i = 0; i < data.Length; i++)
            {
                float w = 2f * Mathf.PI * 120f * i / Rate;
                float s = 0.5f * Mathf.Sin(w) + 0.25f * Mathf.Sin(2f * w) + 0.15f * Mathf.Sin(3f * w) + 0.08f * Mathf.Sin(5f * w);
                s += 0.1f * Mathf.Clamp(Mathf.Sin(w) * 4f, -1f, 1f);   // a little clipped buzz
                s += 0.04f * (float)(rng.NextDouble() * 2 - 1);
                data[i] = s * 0.5f;
            }
            hum = AudioClip.Create("Hum", data.Length, 1, Rate, false);
            hum.SetData(data, 0);
            return hum;
        }

        static AudioClip squelch;

        /// A wet carpet underfoot: a soft, low suck of water.
        public static AudioClip Squelch()
        {
            if (squelch != null) return squelch;
            var data = new float[(int)(Rate * 0.18f)];
            var rng = new System.Random(77);
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                lp += 0.05f * ((float)(rng.NextDouble() * 2 - 1) - lp);
                float bubble = Mathf.Sin(2f * Mathf.PI * (180f + 220f * t) * t) * Mathf.Exp(-t * 30f) * 0.4f;
                data[i] = (lp * 2.2f + bubble) * Mathf.Exp(-t * 16f) * Mathf.Clamp01(t / 0.01f);
            }
            squelch = AudioClip.Create("Squelch", data.Length, 1, Rate, false);
            squelch.SetData(data, 0);
            return squelch;
        }

        static AudioClip staticBurst, ring;

        /// An old desk phone: two-tone ring, two seconds on, four off. Loops.
        public static AudioClip Ring()
        {
            if (ring != null) return ring;
            var data = new float[Rate * 6];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                if (t >= 2f) break;
                float bell = Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 480f * t);
                float trill = Mathf.Sin(2f * Mathf.PI * 20f * t) > 0f ? 1f : 0.4f;   // the bell's rattle
                data[i] = 0.2f * bell * trill * Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((2f - t) / 0.02f);
            }
            ring = AudioClip.Create("Ring", data.Length, 1, Rate, false);
            ring.SetData(data, 0);
            return ring;
        }

        static AudioClip drip, clang, party, laugh, pop, gramophone, fabric;
        static AudioClip waterLap, waterMuffle, splash, distantSplash;

        /// An old record: a slow waltz of our own in a minor key, thin and nasal like a horn gramophone, with
        /// crackle and a slight warp. Loops every 12 seconds.
        public static AudioClip Gramophone()
        {
            if (gramophone != null) return gramophone;
            int[] tune = { 0, 3, 7, 5, 3, 2, 0, -2, 0, 3, 8, 7, 5, 3, 2, -1, 0, 7, 3, 0 };   // semitones above A4
            const float beat = 0.6f;
            var data = new float[(int)(Rate * 12f)];
            var rng = new System.Random(188);
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float warp = 1f + Mathf.Sin(t * 2f * Mathf.PI * 0.55f) * 0.012f;     // a warped disc
                int step = (int)(t / beat) % tune.Length;
                float nt = t % beat;
                float f = 440f * Mathf.Pow(2f, tune[step] / 12f) * warp;
                float s = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(4f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(6f * Mathf.PI * f * t)) * Mathf.Exp(-nt * 2.5f) * 0.18f;
                if (step % 3 == 0 && nt < 0.08f) s += Mathf.Sin(2f * Mathf.PI * 110f * warp * t) * 0.2f * (1f - nt / 0.08f);   // oom (of oom-pah-pah)
                lp += 0.25f * (s - lp);                                                // thin, muffled
                float crackle = rng.NextDouble() < 0.0015 ? (float)(rng.NextDouble() * 2 - 1) * 0.5f : (float)(rng.NextDouble() * 2 - 1) * 0.015f;
                data[i] = lp + crackle;
            }
            gramophone = AudioClip.Create("Gramophone", data.Length, 1, Rate, false);
            gramophone.SetData(data, 0);
            return gramophone;
        }

        /// A cheap party tune, our own: a bouncy major-key loop on a square wave with a bass line, slightly out of
        /// tune and wobbling like an old tape. Loops every 8 seconds.
        public static AudioClip PartyMusic()
        {
            if (party != null) return party;
            const float bpm = 120f, beat = 60f / bpm;
            int[] melody = { 0, 4, 7, 12, 11, 7, 4, 2, 0, 4, 7, 9, 7, 4, 2, -1 };   // semitones above C5, two bars of eighths ×2
            int[] bass = { 0, 0, 5, 5, 7, 7, 5, 4 };                                 // C C F F G G F E (one note per beat... per half bar)
            var data = new float[(int)(Rate * beat * 16)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float wobble = 1f + Mathf.Sin(t * 2f * Mathf.PI * 0.6f) * 0.006f;   // tape wow
                int step = (int)(t / (beat / 2f)) % melody.Length;
                float noteT = t % (beat / 2f);
                float s = 0f;
                if (melody[step] >= 0)
                {
                    float f = 523.25f * Mathf.Pow(2f, melody[step] / 12f) * wobble * 1.012f;   // a little sharp
                    s += (Mathf.Repeat(f * t, 1f) < 0.5f ? 0.18f : -0.18f) * Mathf.Exp(-noteT * 5f);
                }
                int b = (int)(t / (beat * 2f)) % bass.Length;
                float fb = 130.81f * Mathf.Pow(2f, bass[b] / 12f) * wobble;
                s += Mathf.Sin(2f * Mathf.PI * fb * t) * 0.22f;
                if (Mathf.Repeat(t, beat) < 0.03f) s += (Random.value * 2f - 1f) * 0.15f;   // a hi-hat tick
                data[i] = s;
            }
            party = AudioClip.Create("Party", data.Length, 1, Rate, false);
            party.SetData(data, 0);
            return party;
        }

        /// Far-off laughter: a run of breathy "ha"s, falling in pitch.
        public static AudioClip Laughter()
        {
            if (laugh != null) return laugh;
            var data = new float[(int)(Rate * 1.6f)];
            var rng = new System.Random(5);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                int ha = (int)(t / 0.2f);
                float local = t - ha * 0.2f;
                float env = ha < 7 && local < 0.13f ? Mathf.Sin(local / 0.13f * Mathf.PI) * (1f - ha * 0.1f) : 0f;
                float f0 = 240f - ha * 12f;
                phase += f0 / Rate;
                float voice = Mathf.Repeat(phase, 1f) * 2f - 1f;          // buzzy vocal cords
                lp += 0.35f * (voice + (float)(rng.NextDouble() * 2 - 1) * 0.6f - lp);   // breath, softened
                data[i] = lp * env * 0.5f;
            }
            laugh = AudioClip.Create("Laughter", data.Length, 1, Rate, false);
            laugh.SetData(data, 0);
            return laugh;
        }

        /// A balloon popping.
        public static AudioClip Pop()
        {
            if (pop != null) return pop;
            var data = new float[(int)(Rate * 0.12f)];
            var rng = new System.Random(9);
            for (int i = 0; i < data.Length; i++) data[i] = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-i / (Rate * 0.012f));
            pop = AudioClip.Create("Pop", data.Length, 1, Rate, false);
            pop.SetData(data, 0);
            return pop;
        }

        /// Brief cloth rasp for animated emotes, mixed below footsteps and creature sounds.
        public static AudioClip Fabric()
        {
            if (fabric != null) return fabric;
            var data = new float[(int)(Rate * 0.24f)];
            var rng = new System.Random(73);
            float low = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float n = (float)(rng.NextDouble() * 2 - 1);
                low = Mathf.Lerp(low, n, 0.18f);
                float t = (float)i / Rate;
                float envelope = Mathf.Sin(Mathf.Clamp01(t / 0.24f) * Mathf.PI);
                data[i] = ((n - low) * 0.24f + low * 0.56f) * envelope;
            }
            fabric = AudioClip.Create("Suit fabric", data.Length, 1, Rate, false);
            fabric.SetData(data, 0);
            return fabric;
        }

        /// A drop of water landing in a puddle: a short falling "plink".
        public static AudioClip Drip()
        {
            if (drip != null) return drip;
            var data = new float[(int)(Rate * 0.25f)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 1400f * Mathf.Exp(-t * 18f) + 500f;   // pitch drops as the bubble collapses
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 28f) * 0.6f;
            }
            drip = AudioClip.Create("Drip", data.Length, 1, Rate, false);
            drip.SetData(data, 0);
            return drip;
        }

        /// A big pipe somewhere knocking: inharmonic metal partials that ring out.
        public static AudioClip Clang()
        {
            if (clang != null) return clang;
            var data = new float[(int)(Rate * 2.2f)];
            float[] partials = { 87f, 231f, 412f, 655f, 1043f };
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate, s = 0f;
                for (int k = 0; k < partials.Length; k++) s += Mathf.Sin(2f * Mathf.PI * partials[k] * t) * Mathf.Exp(-t * (1.6f + k * 1.3f)) / (k + 1);
                data[i] = s * 0.5f * Mathf.Min(1f, t * 400f);
            }
            clang = AudioClip.Create("Clang", data.Length, 1, Rate, false);
            clang.SetData(data, 0);
            return clang;
        }

        /// Harsh, stuttering radio static for level transitions.
        public static AudioClip Static()
        {
            if (staticBurst != null) return staticBurst;
            var data = new float[(int)(Rate * 1.2f)];
            var rng = new System.Random(7);
            float gate = 1f;
            for (int i = 0; i < data.Length; i++)
            {
                if (i % 1200 == 0) gate = rng.NextDouble() < 0.25 ? 0.15f : 1f;   // stutter every ~27 ms
                float t = (float)i / data.Length;
                data[i] = (float)(rng.NextDouble() * 2 - 1) * gate * 0.5f * Mathf.Min(1f, (1f - t) * 4f);
            }
            staticBurst = AudioClip.Create("Static", data.Length, 1, Rate, false);
            staticBurst.SetData(data, 0);
            return staticBurst;
        }

        /// The Poolrooms' bed: heavily filtered noise with slow, uneven swells. The last half-second is
        /// cross-faded into the first so the loop has no click.
        public static AudioClip WaterLap()
        {
            if (waterLap != null) return waterLap;
            const float seconds = 8f;
            int n = (int)(Rate * seconds), fade = Rate / 2;
            var raw = new float[n + fade];
            var rng = new System.Random(3701);
            float lp = 0f, deep = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2f - 1f);
                lp += 0.055f * (noise - lp);
                deep += 0.009f * (lp - deep);
                float swell = 0.68f + 0.22f * Mathf.Sin(2f * Mathf.PI * 2f * t / seconds)
                                     + 0.10f * Mathf.Sin(2f * Mathf.PI * 5f * t / seconds);
                float ripple = Mathf.Sin(2f * Mathf.PI * 1.5f * t) * 0.018f;
                raw[i] = (deep * 3.2f + lp * 0.45f) * swell + ripple;
            }
            var data = Loop(raw, n, fade, 0.34f);
            waterLap = AudioClip.Create("Water Lap", n, 1, Rate, false);
            waterLap.SetData(data, 0);
            return waterLap;
        }

        /// What the pool sounds like once your head is under: the same water, heard through the Epsom-salt hush.
        public static AudioClip WaterMuffle()
        {
            if (waterMuffle != null) return waterMuffle;
            const float seconds = 4f;
            int n = (int)(Rate * seconds), fade = Rate / 3;
            var raw = new float[n + fade];
            var rng = new System.Random(3702);
            float lp = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / Rate;
                lp += 0.012f * ((float)(rng.NextDouble() * 2f - 1f) - lp);
                float drone = Mathf.Sin(2f * Mathf.PI * 2f * t / seconds) * 0.045f
                            + Mathf.Sin(2f * Mathf.PI * 3f * t / seconds) * 0.025f;
                raw[i] = lp * 1.6f + drone;
            }
            var data = Loop(raw, n, fade, 0.42f);
            waterMuffle = AudioClip.Create("Water Muffle", n, 1, Rate, false);
            waterMuffle.SetData(data, 0);
            return waterMuffle;
        }

        /// A body of water disturbing the surface. Distant splashes are duller and leave a longer tail.
        public static AudioClip Splash(bool distant = false) => distant ? DistantSplash() : NearSplash();

        static AudioClip NearSplash()
        {
            if (splash != null) return splash;
            splash = MakeSplash("Splash", false);
            return splash;
        }

        static AudioClip DistantSplash()
        {
            if (distantSplash != null) return distantSplash;
            distantSplash = MakeSplash("Distant Splash", true);
            return distantSplash;
        }

        static AudioClip MakeSplash(string name, bool distant)
        {
            float seconds = distant ? 1.5f : 0.65f;
            var data = new float[(int)(Rate * seconds)];
            var rng = new System.Random(distant ? 3704 : 3703);
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float cutoff = distant ? 0.02f : 0.14f * Mathf.Exp(-t * 5f) + 0.02f;
                lp += cutoff * ((float)(rng.NextDouble() * 2f - 1f) - lp);
                float env = Mathf.Exp(-t * (distant ? 3.5f : 9f)) * Mathf.Clamp01(t * 500f);
                float plink = Mathf.Sin(2f * Mathf.PI * (distant ? 260f : 900f) * Mathf.Exp(-t * 3f) * t) * Mathf.Exp(-t * 18f) * 0.25f;
                data[i] = (lp * 2.2f + plink) * env;
            }
            float peak = 0f;
            foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 0f) for (int i = 0; i < data.Length; i++) data[i] *= 0.7f / peak;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// Makes a seamless loop of `n` samples from `raw` (which has `fade` extra samples at the end).
        static float[] Loop(float[] raw, int n, int fade, float peakLevel)
        {
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = i < fade ? Mathf.Lerp(raw[n + i], raw[i], (float)i / fade) : raw[i];
            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak > 0f) for (int i = 0; i < n; i++) data[i] *= peakLevel / peak;
            return data;
        }

        /// Soft, damp carpet footsteps: short low-passed noise thuds, a few variants.
        public static AudioClip[] Footsteps()
        {
            if (footsteps != null && footsteps[0] != null) return footsteps;   // (clips die when Play stops)
            footsteps = new AudioClip[4];
            for (int v = 0; v < footsteps.Length; v++)
            {
                var rng = new System.Random(1000 + v);
                var data = new float[(int)(Rate * 0.22f)];
                float lowPass = 0f, peak = 0f, cutoff = 0.08f + 0.03f * v;
                for (int i = 0; i < data.Length; i++)
                {
                    float t = (float)i / Rate;
                    lowPass += cutoff * ((float)(rng.NextDouble() * 2 - 1) - lowPass);
                    float thump = Mathf.Sin(2f * Mathf.PI * (65f + 8f * v) * t) * Mathf.Exp(-t * 45f);
                    float s = (lowPass * Mathf.Exp(-t * 28f) + 0.6f * thump) * Mathf.Clamp01(t / 0.003f);
                    data[i] = s;
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                }
                for (int i = 0; i < data.Length; i++) data[i] *= 0.7f / peak;
                footsteps[v] = AudioClip.Create($"Footstep {v}", data.Length, 1, Rate, false);
                footsteps[v].SetData(data, 0);
            }
            return footsteps;
        }
    }
}
