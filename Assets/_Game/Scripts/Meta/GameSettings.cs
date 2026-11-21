using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Meta
{
    [Serializable]
    public sealed class SettingsData
    {
        // Graphics
        public int width, height;               // 0 = leave the resolution alone
        public bool fullscreen = true, vsync = true;
        public int quality = 2;                 // 0 Low, 1 Medium, 2 High, 3 Ultra (ray-traced GI where the GPU can)
        public float fov = 70f, brightness;     // brightness in stops, -1..1
        // Controls
        public float sensitivity = 1f;
        public bool invertY;
        // Comfort
        public bool headBob = true, reduceFlashing, captions = true;
        public float effects = 1f;              // film grain, chromatic aberration, motion blur, head bob
        // Audio, 0..1
        public float master = 1f, ambience = 1f, sfx = 1f, creatures = 1f, ui = 1f;
    }

    /// Player settings: saved to settings.json, applied to the screen, the post-processing and the audio.
    public static class GameSettings
    {
        public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };
        static SettingsData current;
        static Volume volume;
        static string PathFile => Path.Combine(Application.persistentDataPath, "settings.json");

        public static SettingsData Current
        {
            get
            {
                if (current != null) return current;
                try { current = File.Exists(PathFile) ? JsonUtility.FromJson<SettingsData>(File.ReadAllText(PathFile)) : null; }
                catch (Exception e) { Debug.LogWarning($"Settings unreadable: {e.Message}"); }
                return current ??= new SettingsData();
            }
        }

        public static bool ReduceFlashing => Current.reduceFlashing;
        public static bool RayTracingAvailable => SystemInfo.supportsRayTracing;

        public static event Action Changed;

        public static void Save()
        {
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(Current, true)); }
            catch (Exception e) { Debug.LogWarning($"Couldn't save settings: {e.Message}"); }
        }

        /// Pushes everything to the engine. Call after any change.
        public static void Apply()
        {
            var s = Current;
            if (!Application.isEditor && s.width > 0 && (Screen.width != s.width || Screen.height != s.height || Screen.fullScreen != s.fullscreen))
                Screen.SetResolution(s.width, s.height, s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            else if (!Application.isEditor) Screen.fullScreenMode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            AudioListener.volume = s.master;
            ApplyVolume(s);
            foreach (var cam in Camera.allCameras)
                if (cam.TryGetComponent(out HDAdditionalCameraData data))
                    data.antialiasing = s.quality == 0 ? HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing
                                                       : HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            Changed?.Invoke();
        }

        /// A global volume just above the game's own (priority 0.5): quality switches, brightness, effect strength.
        /// Below the night-vision and madness volumes, so those still win.
        static void ApplyVolume(SettingsData s)
        {
            if (volume == null)
            {
                var go = new GameObject("Settings Volume");
                UnityEngine.Object.DontDestroyOnLoad(go);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0.5f;
                volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            }
            var p = volume.sharedProfile;
            T Get<T>() where T : VolumeComponent => p.TryGet(out T c) ? c : p.Add<T>(true);

            Get<ColorAdjustments>().postExposure.Override(s.brightness);
            float e = Mathf.Clamp01(s.effects);
            Get<FilmGrain>().intensity.Override(0.32f * e);
            Get<ChromaticAberration>().intensity.Override(0.07f * e);
            Get<MotionBlur>().intensity.Override(0.15f * e);

            int q = s.quality;
            var gi = Get<GlobalIllumination>();
            gi.enable.Override(q >= 2);
            gi.tracing.Override(q >= 3 && RayTracingAvailable ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            Get<ScreenSpaceReflection>().enabled.Override(q >= 1);
            Get<ScreenSpaceAmbientOcclusion>().intensity.Override(q == 0 ? 0.8f : 1.3f);
            Get<ContactShadows>().enable.Override(q >= 1);
            Get<HDShadowSettings>().maxShadowDistance.Override(q == 0 ? 25f : q == 1 ? 35f : q == 2 ? 45f : 60f);
        }
    }

    /// Volume per channel (the master volume is the listener's). Sources multiply their own volume by this.
    public static class AudioMix
    {
        public static float Ambience => GameSettings.Current.ambience;
        public static float Effects => GameSettings.Current.sfx;
        public static float Creatures => GameSettings.Current.creatures;
        public static float UI => GameSettings.Current.ui;
    }
}
