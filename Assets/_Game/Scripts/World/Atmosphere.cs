using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.World
{
    /// The air of the current level in HDRP terms: volumetric fog, the soft fill light that stands in for light
    /// bouncing around the rooms, and their response to copy drift and blackouts. One global volume, set per level.
    public sealed class Atmosphere : MonoBehaviour
    {
        Volume volume;
        Fog fog;
        GradientSky sky;
        VisualEnvironment environment;
        IndirectLightingController indirect;
        LevelTheme theme;
        float drift, blackout, underwater;

        public static Atmosphere Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            environment = profile.Add<VisualEnvironment>(true);
            environment.skyType.Override((int)SkyType.Gradient);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            sky = profile.Add<GradientSky>(true);
            fog = profile.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.maximumHeight.Override(60f);
            fog.baseHeight.Override(-40f);
            fog.maxFogDistance.Override(200f);
            fog.depthExtent.Override(48f);
            indirect = profile.Add<IndirectLightingController>(true);
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
            Meta.GameSettings.Changed += Refresh;
        }

        void OnDestroy() => Meta.GameSettings.Changed -= Refresh;

        /// How much of the old flat fill stays. With bounce light (SSGI from High, ray traced on Ultra) the rooms light
        /// each other, and the fill only keeps the darkest corners from going pure black.
        public static float FillShare => Meta.GameSettings.Current.quality >= 2 ? FillWithGi : 0.75f;
        public static float FillWithGi = 0.65f;

        public void Apply(LevelTheme level)
        {
            theme = level;
            drift = 0f;
            blackout = 0f;
            underwater = 0f;
            Refresh();
        }

        public void SetDrift(float value) { if (!Mathf.Approximately(drift, value)) { drift = value; Refresh(); } }
        public void SetBlackout(float value) { if (!Mathf.Approximately(blackout, value)) { blackout = value; Refresh(); } }

        /// 0 above water, 1 fully submerged. Called with a smoothed value by the player controller so the
        /// air closes into a blue-green murk as your head goes under, without a visible pop at the surface.
        public void SetUnderwater(float value) { if (!Mathf.Approximately(underwater, value)) { underwater = value; Refresh(); } }

        /// Level 7: how far below the surface the camera is (-1 = not in the ocean). The deeper, the darker and closer the water.
        public void SetOceanDepth(float metres) { if (Mathf.Abs(oceanDepth - metres) > 0.5f || (metres < 0f) != (oceanDepth < 0f)) { oceanDepth = metres; Refresh(); } }
        float oceanDepth = -1f, bank;

        /// Level 9: how deep in a rolling fog bank you are (0..1).
        public void SetBank(float value) { if (!Mathf.Approximately(bank, value)) { bank = value; Refresh(); } }

        void Refresh()
        {
            if (theme == null || fog == null) return;
            // Theme fog density (old exponential-squared units) -> HDRP's attenuation distance.
            float density = theme.fogDensity * (1f + drift * 2.5f + bank * 3f);
            // x2 since the graphics revamp: the old density washed out every hall behind a white haze on camera.
            float airPath = Mathf.Clamp(2.4f / Mathf.Max(0.002f, density), 8f, 600f);
            // Underwater visibility collapses to a few metres; the tint turns the pool's blue-green.
            // In the ocean, the visibility and colour follow the depth zones: murky teal, grey-blue, near black, black.
            float zoneT = oceanDepth < 0f ? -1f : Mathf.Clamp01(oceanDepth / 160f);
            float waterPath = zoneT < 0f ? 5f : Mathf.Lerp(18f, 16f, Mathf.Sqrt(zoneT));   // never under 16 m: your lamp still finds the rock and the cave beacon
            var waterTint = zoneT < 0f ? new Color(0.05f, 0.27f, 0.31f)
                : zoneT < 0.19f ? Color.Lerp(new Color(0.02f, 0.1f, 0.13f), new Color(0.005f, 0.02f, 0.045f), zoneT / 0.19f)
                : Color.Lerp(new Color(0.005f, 0.02f, 0.045f), new Color(0f, 0.002f, 0.006f), Mathf.Clamp01((zoneT - 0.19f) / 0.3f));
            bool sea = theme.generator == LevelGenerator.Ocean;
            if (sea) airPath = 20f;   // a sea mist: it closes before the world runs out
            fog.baseHeight.Override(sea || theme.generator == LevelGenerator.Caves ? 40f : theme.generator == LevelGenerator.Suburbs ? 4f : -40f);   // the same air at every height, not a layer thinning upwards
            fog.meanFreePath.Override(Mathf.Lerp(airPath, waterPath, underwater));
            var tint = Color.Lerp(theme.fogColor, theme.fogColor * new Color(0.75f, 0.82f, 0.7f), drift);
            var air = Color.Lerp(tint, Color.black, blackout);
            if (sea) air = new Color(8f, 10.2f, 11f) * (1f - blackout);   // bright enough to hide the far edge
            fog.color.Override(Color.Lerp(air, waterTint, underwater));
            // Deep water scatters almost nothing back: the murk goes from teal to black with depth.
            float deepDark = zoneT < 0f ? 0f : Mathf.Clamp01(zoneT * 2.5f);   // fully dark by ~65 m, so the camcorder's exposure can't lift it
            fog.albedo.Override(Color.Lerp(Color.Lerp(tint, Color.black, blackout * 0.8f),
                                           Color.Lerp(new Color(0.04f, 0.2f, 0.24f), new Color(0f, 0.004f, 0.01f), deepDark), underwater));
            // The fill light: a sky the rooms never show, whose ambient light stands in for bounced light.
            var a = theme.ambient * (1f - blackout * 0.97f) * FillShare * (1f - 0.97f * deepDark * underwater);   // no daylight reaches the deep
            sky.top.Override(a);
            sky.middle.Override(a * 0.85f);
            sky.bottom.Override(a * 0.6f);
            sky.exposure.Override(Mathf.Lerp(9.6f, 4f, blackout));
            indirect.indirectDiffuseLightingMultiplier.Override(Mathf.Lerp(1f, 0.04f, blackout));
            indirect.reflectionLightingMultiplier.Override(Mathf.Lerp(1f, 0.05f, blackout));
        }
    }
}
