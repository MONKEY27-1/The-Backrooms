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
        }

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

        void Refresh()
        {
            if (theme == null || fog == null) return;
            // Theme fog density (old exponential-squared units) -> HDRP's attenuation distance.
            float density = theme.fogDensity * (1f + drift * 2.5f);
            float airPath = Mathf.Clamp(1.2f / Mathf.Max(0.002f, density), 8f, 600f);
            // Underwater visibility collapses to a few metres; the tint turns the pool's blue-green.
            fog.meanFreePath.Override(Mathf.Lerp(airPath, 5f, underwater));
            var tint = Color.Lerp(theme.fogColor, theme.fogColor * new Color(0.75f, 0.82f, 0.7f), drift);
            var air = Color.Lerp(tint, Color.black, blackout);
            fog.color.Override(Color.Lerp(air, new Color(0.05f, 0.27f, 0.31f), underwater));
            fog.albedo.Override(Color.Lerp(Color.Lerp(tint, Color.black, blackout * 0.8f),
                                           new Color(0.04f, 0.2f, 0.24f), underwater));
            // The fill light: a sky the rooms never show, whose ambient light stands in for bounced light.
            var a = theme.ambient * (1f - blackout * 0.97f);
            sky.top.Override(a);
            sky.middle.Override(a * 0.85f);
            sky.bottom.Override(a * 0.6f);
            sky.exposure.Override(Mathf.Lerp(9.6f, 4f, blackout));
            indirect.indirectDiffuseLightingMultiplier.Override(Mathf.Lerp(1f, 0.04f, blackout));
            indirect.reflectionLightingMultiplier.Override(Mathf.Lerp(1f, 0.05f, blackout));
        }
    }
}
