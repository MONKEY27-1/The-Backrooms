using Backrooms.Audio;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// Level 37's "Sublimity": warm water that calms you while the light holds, and turns flat and watchful in
    /// the unlit deep. Handles the water bed, muffling and sudden sound drop-offs, splashes, and the moving ripples.
    public sealed class PoolAmbience : MonoBehaviour
    {
        FirstPersonController player;
        ChunkStreamer streamer;
        PlayerStatus status;
        Material water, waterDeep, caustics, causticsDeep;
        AudioSource lap, muffle, splashes;
        AudioLowPassFilter listenerFilter;
        bool addedFilter;
        float originalCutoff;
        Vector2 originalRippleOffset, originalDetailOffset, originalDeepOffset, originalDeepDetailOffset, originalCausticOffset, originalDeepCausticOffset;
        float nextSplash, nextDrop, dropUntil;

        public void Init(FirstPersonController firstPerson, ChunkStreamer world, Material waterMaterial,
                         Material deepMaterial, Material causticMaterial, Material deepCausticMaterial)
        {
            player = firstPerson;
            streamer = world;
            status = player != null ? player.GetComponent<PlayerStatus>() : null;
            water = waterMaterial;
            waterDeep = deepMaterial;
            caustics = causticMaterial;
            causticsDeep = deepCausticMaterial;
            if (water != null)
            {
                originalRippleOffset = water.GetTextureOffset("_BaseColorMap");
                originalDetailOffset = water.GetTextureOffset("_DetailMap");
            }
            if (waterDeep != null)
            {
                originalDeepOffset = waterDeep.GetTextureOffset("_BaseColorMap");
                originalDeepDetailOffset = waterDeep.GetTextureOffset("_DetailMap");
            }
            if (caustics != null) originalCausticOffset = caustics.GetTextureOffset("_BaseColorMap");
            if (causticsDeep != null) originalDeepCausticOffset = causticsDeep.GetTextureOffset("_BaseColorMap");

            lap = Source("Pool Water", ProceduralAudio.WaterLap(), false);
            muffle = Source("Underwater", ProceduralAudio.WaterMuffle(), false);
            splashes = Source("Pool Sounds", null, true);
            lap.Play();
            muffle.Play();

            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null)
            {
                listenerFilter = listener.GetComponent<AudioLowPassFilter>();
                if (listenerFilter == null) { listenerFilter = listener.gameObject.AddComponent<AudioLowPassFilter>(); addedFilter = true; }
                originalCutoff = listenerFilter.cutoffFrequency;
                listenerFilter.cutoffFrequency = 22000f;
                listenerFilter.lowpassResonanceQ = 0.7f;
            }

            float now = Time.time;
            nextSplash = now + Random.Range(3f, 8f);
            nextDrop = now + Random.Range(14f, 30f);
        }

        /// `spatial` sources carry distant splashes around the player; the beds sit at the listener.
        AudioSource Source(string name, AudioClip clip, bool spatial)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = clip;
            source.loop = !spatial;
            source.volume = 0f;
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            if (spatial)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1f;
                source.maxDistance = 55f;
            }
            return source;
        }

        static void Scroll(Material mat, string property, Vector2 delta)
        {
            if (mat == null || !mat.HasProperty(property)) return;
            var o = mat.GetTextureOffset(property) + delta;
            mat.SetTextureOffset(property, new Vector2(Mathf.Repeat(o.x, 1f), Mathf.Repeat(o.y, 1f)));
        }

        void OnDestroy()
        {
            if (water != null)
            {
                water.SetTextureOffset("_BaseColorMap", originalRippleOffset);
                water.SetTextureOffset("_DetailMap", originalDetailOffset);
            }
            if (waterDeep != null)
            {
                waterDeep.SetTextureOffset("_BaseColorMap", originalDeepOffset);
                waterDeep.SetTextureOffset("_DetailMap", originalDeepDetailOffset);
            }
            if (caustics != null)
            {
                caustics.SetTextureOffset("_BaseColorMap", originalCausticOffset);
                caustics.SetTextureOffset("_EmissiveColorMap", originalCausticOffset);
            }
            if (causticsDeep != null)
            {
                causticsDeep.SetTextureOffset("_BaseColorMap", originalDeepCausticOffset);
                causticsDeep.SetTextureOffset("_EmissiveColorMap", originalDeepCausticOffset);
            }
            if (listenerFilter != null)
            {
                listenerFilter.cutoffFrequency = originalCutoff;
                if (addedFilter) Destroy(listenerFilter);
            }
            if (lap != null) Destroy(lap.gameObject);
            if (muffle != null) Destroy(muffle.gameObject);
            if (splashes != null) Destroy(splashes.gameObject);
        }

        void Update()
        {
            if (player == null || lap == null) return;
            float dt = Time.deltaTime;
            float surface = float.NegativeInfinity;
            bool inWater = streamer != null && streamer.TryWaterSurfaceAt(player.transform.position, out surface);
            float depth = inWater ? surface - player.transform.position.y : 0f;
            bool wading = inWater && depth > 0.15f;
            bool underwater = wading && player.Underwater;
            bool dark = status != null && status.InDark;

            // The level's signature: sound vanishes for a moment, then comes back without a cue.
            bool drop = Time.time < dropUntil;
            if (Time.time >= nextDrop)
            {
                nextDrop = Time.time + Random.Range(12f, 28f);
                dropUntil = Time.time + Random.Range(1.1f, 2.4f);
                drop = true;
                UI.Captions.Say("[the hum stops]");
            }

            float ambience = Meta.AudioMix.Ambience;
            float lapTarget = wading ? Mathf.Lerp(0.08f, 0.42f, Mathf.InverseLerp(0.15f, 1.6f, depth)) : 0f;
            if (underwater) lapTarget *= 0.3f;
            float muffleTarget = underwater ? 0.5f : wading ? 0.07f : 0f;
            if (drop) { lapTarget *= 0.1f; muffleTarget *= 0.2f; }

            float fade = drop ? 3.5f : 0.5f;
            lap.volume = Mathf.MoveTowards(lap.volume, lapTarget * ambience, dt * fade);
            muffle.volume = Mathf.MoveTowards(muffle.volume, muffleTarget * ambience, dt * fade);

            if (listenerFilter != null)
            {
                float cutoff = drop ? 320f : underwater ? 700f : wading ? 12000f : 22000f;
                listenerFilter.cutoffFrequency = Mathf.MoveTowards(listenerFilter.cutoffFrequency, cutoff,
                    dt * (drop ? 9000f : 3000f));
            }

            // Wading disturbs the surface slightly; underwater everything slows and muffles. The two normal
            // layers and the caustics all move at different rates, which hides the underlying texture tiles.
            float stir = underwater ? 0.55f : wading ? 1.45f : 1f;
            float t = dt * stir;
            Scroll(water, "_BaseColorMap", new Vector2(0.006f, 0.004f) * t);
            Scroll(water, "_DetailMap", new Vector2(0.018f, -0.011f) * t);
            Scroll(waterDeep, "_BaseColorMap", new Vector2(0.0048f, 0.0032f) * t);
            Scroll(waterDeep, "_DetailMap", new Vector2(0.014f, -0.009f) * t);
            Scroll(caustics, "_BaseColorMap", new Vector2(0.012f, 0.009f) * t);
            Scroll(caustics, "_EmissiveColorMap", new Vector2(0.012f, 0.009f) * t);
            Scroll(causticsDeep, "_BaseColorMap", new Vector2(0.008f, 0.006f) * t);
            Scroll(causticsDeep, "_EmissiveColorMap", new Vector2(0.008f, 0.006f) * t);

            // Epsom salts in the water: lit shallows restore you; lightless water does the opposite.
            if (status != null)
            {
                if (wading && !underwater)
                {
                    status.Stamina = Mathf.MoveTowards(status.Stamina, 1f, dt * 0.12f);
                    status.Sanity = dark
                        ? Mathf.MoveTowards(status.Sanity, 0f, dt * 0.012f)
                        : Mathf.MoveTowards(status.Sanity, 1f, dt * 0.018f);
                }
                else if (underwater)
                    status.Sanity = Mathf.MoveTowards(status.Sanity, 0f, dt * (dark ? 0.045f : 0.03f));
            }

            if (wading && Time.time >= nextSplash)
            {
                bool distant = Random.value < 0.62f;
                nextSplash = Time.time + Random.Range(distant ? 7f : 4f, distant ? 16f : 10f);
                var away = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                splashes.transform.position = player.transform.position + away * Random.Range(distant ? 9f : 3f, distant ? 24f : 8f)
                                               + Vector3.down * depth;
                splashes.pitch = Random.Range(0.85f, 1.15f);
                splashes.PlayOneShot(ProceduralAudio.Splash(distant), (distant ? 0.18f : 0.36f) * Meta.AudioMix.Effects);
                if (distant && Random.value < 0.25f) UI.Captions.Say("[water shifts somewhere in the dark]");
            }
        }
    }
}
