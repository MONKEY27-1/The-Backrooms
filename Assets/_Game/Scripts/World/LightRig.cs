using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.World
{
    public struct Emitter
    {
        public Vector3 Position;
        public float Range;
        public int Id;
        public bool Faulty, Red, Sick, Floor;
        public Color Tint;   // alpha 0 = the level's own light colour
    }

    /// A fixed pool of real-time lights and hum sources, moved to the nearest light panels a few times a second.
    /// Panels themselves are just glowing mesh, so thousands can exist while only the pool costs anything.
    /// The nearest few lights cast soft shadows, which stops light leaking through walls where you can see it.
    public sealed class LightRig
    {
        const int LightCount = 64, ShadowCount = 6, AreaCount = 6, HumCount = 8;
        const float LumensPerUnit = 2600f;   // theme.lightIntensity 1 = a ~2600 lm fluorescent troffer
        static readonly Color RedTint = new Color(1f, 0.45f, 0.4f);
        static readonly Color SickTint = new Color(0.72f, 1f, 0.62f);
        static readonly Color GlowTint = new Color(1f, 0.5f, 0.18f);

        readonly List<Light> lights = new List<Light>();
        readonly List<AudioSource> hums = new List<AudioSource>();
        readonly Emitter[] assigned = new Emitter[LightCount];
        readonly bool[] active = new bool[LightCount];
        readonly List<(Emitter e, float d)> nearby = new List<(Emitter, float)>();

        /// Flat distance from the player to the nearest working light (sanity drains in the dark).
        public float NearestDistance { get; private set; } = float.MaxValue;

        /// 0 = normal, 1 = every light dead (the fear director's blackouts).
        public float Dim;

        public LightRig(Transform parent, AudioClip hum)
        {
            for (int i = 0; i < LightCount; i++)
            {
                var go = new GameObject($"Pooled Light {i}");
                go.transform.SetParent(parent, false);
                // The nearest few are rectangle lights the size of a panel: soft, real-looking falloff and shadows.
                var hd = go.AddHDLight(i < AreaCount ? LightType.Rectangle : LightType.Point);
                var light = go.GetComponent<Light>();
                light.lightUnit = LightUnit.Lumen;
                if (i < AreaCount)
                {
                    light.areaSize = new Vector2(1.1f, 0.55f);
                    go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // facing down
                }
                light.shadows = i < ShadowCount ? LightShadows.Soft : LightShadows.None;
                hd.SetShadowResolutionLevel(i < 2 ? 3 : 2);
                hd.volumetricDimmer = 0.6f;   // a little light in the haze
                light.shadowNearPlane = 0.1f;
                light.enabled = false;
                lights.Add(light);
            }
            for (int i = 0; i < HumCount; i++)
            {
                var go = new GameObject($"Pooled Hum {i}");
                go.transform.SetParent(parent, false);
                var source = go.AddComponent<AudioSource>();
                source.clip = hum;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 0.5f;
                source.maxDistance = 9f;
                source.dopplerLevel = 0f;
                hums.Add(source);
            }
        }

        /// Reassigns the pool to the emitters nearest `player`. Call a few times a second.
        public void Refresh(Vector3 player, IEnumerable<List<Emitter>> sets, LevelTheme theme, float maxDistance, float drift = 0f)
        {
            nearby.Clear();
            foreach (var set in sets)
                foreach (var e in set)
                {
                    float dx = e.Position.x - player.x, dz = e.Position.z - player.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < maxDistance) nearby.Add((e, d));
                }
            nearby.Sort((a, b) => a.d.CompareTo(b.d));
            NearestDistance = nearby.Count > 0 ? nearby[0].d : float.MaxValue;

            for (int i = 0; i < LightCount; i++)
            {
                var light = lights[i];
                active[i] = i < nearby.Count;
                if (!active[i])
                {
                    light.enabled = false;
                    continue;
                }
                var (e, d) = nearby[i];
                assigned[i] = e;
                bool area = light.type == LightType.Rectangle;
                light.transform.position = e.Position + (e.Floor ? Vector3.up * 0.25f : area ? Vector3.down * 0.03f : Vector3.down * 0.6f);
                var baseColor = e.Tint.a > 0f ? e.Tint : e.Floor ? GlowTint : e.Red ? theme.lightColor * RedTint : e.Sick ? theme.lightColor * SickTint : theme.lightColor;
                float hue = 0.985f + 0.03f * WorldHash.Value((uint)e.Id, 9, 0, 0x11A8);
                float cool = 0.975f + 0.05f * WorldHash.Value((uint)e.Id, 11, 0, 0x11A9);
                light.color = baseColor * new Color(1f, hue, cool, 1f);
                light.range = e.Range;
                float fade = Mathf.Clamp01((maxDistance - d) / 6f);
                float v = 0.9f + 0.2f * WorldHash.Value((uint)e.Id, 7, 0, 0x11A7);
                light.intensity = LumensPerUnit * theme.lightIntensity * Mathf.Pow(e.Range / 6f, 1.2f) * fade * (1f - Dim) * (e.Floor ? 0.3f : 1f) * v;
                light.enabled = Dim < 0.98f && (!e.Faulty || Flicker.On(e.Id, Time.time));
            }

            for (int i = 0; i < HumCount; i++)
            {
                var source = hums[i];
                bool use = Dim < 0.5f && i < nearby.Count && nearby[i].d < source.maxDistance;
                if (!use)
                {
                    if (source.isPlaying) source.Stop();
                    continue;
                }
                source.transform.position = nearby[i].e.Position;
                source.volume = theme.humVolume * Meta.AudioMix.Ambience;
                // Copy drift detunes the hum: lower, and each fixture a little off from the next.
                source.pitch = 1f - drift * 0.3f + (WorldHash.Value((uint)nearby[i].e.Id, 3, 0, 61) - 0.5f) * drift * 0.25f;
                if (!source.isPlaying)
                {
                    source.timeSamples = Random.Range(0, source.clip.samples);   // desync so they don't phase
                    source.Play();
                }
            }
        }

        /// Per-frame: failing lights stutter in step with their panels.
        public void Tick(float time)
        {
            for (int i = 0; i < LightCount; i++)
                if (active[i] && assigned[i].Faulty) lights[i].enabled = Dim < 0.98f && Flicker.On(assigned[i].Id, time);
        }

        public void Clear()
        {
            for (int i = 0; i < LightCount; i++) { active[i] = false; lights[i].enabled = false; }
            foreach (var h in hums) h.Stop();
        }
    }
}
