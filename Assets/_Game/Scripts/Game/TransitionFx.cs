using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Backrooms.Game
{
    /// The level-change effect: a second of static and dropped frames, a blink of black, then the new place.
    public sealed class TransitionFx : MonoBehaviour
    {
        [SerializeField] float glitchTime = 0.7f, fadeTime = 0.25f, holdTime = 0.15f, revealTime = 0.4f;

        const int NoiseW = 160, NoiseH = 90;
        RawImage noise;
        Image black;
        Texture2D noiseTexture;
        Color32[] pixels;
        AudioSource sound;

        void Awake()
        {
            var canvasGo = new GameObject("Transition Overlay", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            noiseTexture = new Texture2D(NoiseW, NoiseH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            pixels = new Color32[NoiseW * NoiseH];
            noise = Layer<RawImage>(canvasGo.transform, "Static");
            noise.texture = noiseTexture;
            black = Layer<Image>(canvasGo.transform, "Black");
            black.color = Color.black;
            SetAlpha(noise, 0f);
            SetAlpha(black, 0f);

            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.clip = Audio.ProceduralAudio.Static();
        }

        void OnDestroy() => Destroy(noiseTexture);

        /// Glitches, blacks out, runs `swap` while the screen is hidden, then fades back in.
        public IEnumerator Play(Action swap)
        {
            sound.volume = Meta.AudioMix.Effects;
            sound.Play();
            for (float t = 0f; t < glitchTime; t += Time.unscaledDeltaTime)
            {
                RefreshNoise();
                SetAlpha(noise, UnityEngine.Random.Range(0.15f, 0.7f) * (0.3f + t / glitchTime));
                SetAlpha(black, !Meta.GameSettings.ReduceFlashing && UnityEngine.Random.value < 0.15f ? 1f : 0f);   // dropped frames
                yield return null;
            }
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                RefreshNoise();
                SetAlpha(black, t / fadeTime);
                yield return null;
            }
            SetAlpha(black, 1f);
            SetAlpha(noise, 0f);
            swap();
            yield return new WaitForSecondsRealtime(holdTime);
            for (float t = 0f; t < revealTime; t += Time.unscaledDeltaTime)
            {
                SetAlpha(black, 1f - t / revealTime);
                yield return null;
            }
            SetAlpha(black, 0f);
        }

        void RefreshNoise()
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)UnityEngine.Random.Range(0, 256);
                pixels[i] = new Color32(v, v, v, 255);
            }
            noiseTexture.SetPixels32(pixels);
            noiseTexture.Apply(false);
        }

        static T Layer<T>(Transform parent, string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = go.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
