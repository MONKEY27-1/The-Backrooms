using UnityEngine;

namespace Backrooms.Audio
{
    /// Creature voices, synthesized like ProceduralAudio (no files to license).
    /// shortcut: placeholder quality; swap in recorded CC0 sounds in the audio pass.
    public static class CreatureSounds
    {
        const int Rate = 44100;
        static AudioClip growl, bark, maul, scuttle, flutter;

        /// A low, wet growl from a human throat: a rough 70-95 Hz buzz with rattling and breath. Loops (2 s).
        public static AudioClip Growl()
        {
            if (growl != null) return growl;
            int n = Rate * 2;
            var data = new float[n];
            var rng = new System.Random(8);
            float phase = 0f, noise = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = 82f + 12f * Mathf.Sin(2f * Mathf.PI * 0.5f * t) + 6f * Mathf.Sin(2f * Mathf.PI * 3.1f * t);
                phase += f / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float rattle = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 23f * t);         // the throat flutter
                noise = Mathf.Lerp(noise, (float)(rng.NextDouble() * 2 - 1), 0.08f);   // low breathy noise
                float breath = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);     // whole cycles in 2 s: loops cleanly
                data[i] = Mathf.Clamp((saw * 0.55f * rattle + noise * 0.9f) * breath, -1f, 1f) * 0.6f;
            }
            growl = AudioClip.Create("Growl", n, 1, Rate, false);
            growl.SetData(data, 0);
            return growl;
        }

        /// Many hands and feet slapping and scraping a floor at once: uneven knocks. Loops (2 s).
        public static AudioClip Scuttle()
        {
            if (scuttle != null) return scuttle;
            int n = Rate * 2;
            var data = new float[n];
            var rng = new System.Random(5);
            int next = 0;
            while (next < n)
            {
                float amp = 0.4f + (float)rng.NextDouble() * 0.6f, freq = 90f + (float)rng.NextDouble() * 160f;
                int len = (int)(Rate * (0.025f + (float)rng.NextDouble() * 0.03f));
                for (int i = 0; i < len && next + i < n; i++)
                {
                    float t = (float)i / len;
                    data[next + i] += (Mathf.Sin(2f * Mathf.PI * freq * i / Rate) * 0.6f + ((float)rng.NextDouble() * 2 - 1) * 0.5f) * amp * Mathf.Pow(1f - t, 3f);
                }
                next += (int)(Rate * (0.04f + (float)rng.NextDouble() * 0.09f));   // 8-25 knocks a second
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f) * 0.7f;
            scuttle = AudioClip.Create("Scuttle", n, 1, Rate, false);
            scuttle.SetData(data, 0);
            return scuttle;
        }

        /// Dozens of dry, heavy wings at once: soft broadband beats, never in step. Loops (2 s).
        public static AudioClip Flutter()
        {
            if (flutter != null) return flutter;
            int n = Rate * 2;
            var data = new float[n];
            var rng = new System.Random(4);
            float low = 0f, mid = 0f;
            var rates = new float[9]; var off = new float[9];
            for (int k = 0; k < 9; k++) { rates[k] = 8f + rng.Next(0, 22) * 0.5f; off[k] = (float)rng.NextDouble() * 6.28f; }   // whole beats in 2 s: loops cleanly
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, beat = 0f;
                for (int k = 0; k < 9; k++) beat += Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * rates[k] * t + off[k])), 3f);
                float w = (float)(rng.NextDouble() * 2 - 1);
                low = Mathf.Lerp(low, w, 0.06f); mid = Mathf.Lerp(mid, w, 0.35f);
                data[i] = Mathf.Clamp((low * 1.6f + (mid - low) * 0.35f) * beat * 0.5f, -1f, 1f) * 0.8f;
            }
            flutter = AudioClip.Create("Flutter", n, 1, Rate, false);
            flutter.SetData(data, 0);
            return flutter;
        }

        /// One hoarse bark: a cracked shout that drops in pitch, about a third of a second.
        public static AudioClip Bark()
        {
            if (bark != null) return bark;
            int n = (int)(Rate * 0.35f);
            var data = new float[n];
            var rng = new System.Random(80);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(310f, 140f, t) / Rate;
                float s = Mathf.Sin(2f * Mathf.PI * phase) + 0.5f * Mathf.Sin(4f * Mathf.PI * phase) + 0.6f * (float)(rng.NextDouble() * 2 - 1);
                float env = Mathf.Min(1f, t * 25f) * Mathf.Pow(1f - t, 1.6f);
                data[i] = Mathf.Clamp(s * 1.6f, -1f, 1f) * env * 0.8f;   // clipped: a voice torn ragged
            }
            bark = AudioClip.Create("Bark", n, 1, Rate, false);
            bark.SetData(data, 0);
            return bark;
        }

        /// The maul: a snarl, a thud and a tear.
        public static AudioClip Maul()
        {
            if (maul != null) return maul;
            int n = (int)(Rate * 0.7f);
            var data = new float[n];
            var rng = new System.Random(81);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, sec = (float)i / Rate;
                phase += Mathf.Lerp(180f, 90f, t) / Rate;
                float snarl = (2f * (phase - Mathf.Floor(phase)) - 1f) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 31f * sec));
                float thud = sec < 0.12f ? Mathf.Sin(2f * Mathf.PI * 55f * sec) * (1f - sec / 0.12f) * 1.5f : 0f;
                float tear = sec > 0.15f && sec < 0.45f ? (float)(rng.NextDouble() * 2 - 1) * 0.8f : 0f;
                data[i] = Mathf.Clamp(snarl * 0.6f * (1f - t) + thud + tear * (1f - t), -1f, 1f) * 0.8f;
            }
            maul = AudioClip.Create("Maul", n, 1, Rate, false);
            maul.SetData(data, 0);
            return maul;
        }
    }
}
