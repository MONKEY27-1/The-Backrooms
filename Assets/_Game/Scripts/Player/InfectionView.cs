using UnityEngine;
using UnityEngine.UI;

namespace Backrooms.Player
{
    /// What the Wretched Cycle does to you (wiki Entity 15), as you feel it: an itch, skin going red on your arms, your
    /// sight closing in red-brown, and your own voice begging for water. PlayerStatus keeps how far along it is.
    public sealed class InfectionView : MonoBehaviour
    {
        static readonly string[] Pleas = { "water... please...", "it itches. it itches.", "don't leave me here", "so thirsty" };

        PlayerStatus status;
        RawImage veil;
        Texture2D tex;
        AudioSource voice;
        SkinnedMeshRenderer[] arms;
        MaterialPropertyBlock tint;
        float nextVoice, last;

        void Start() => status = GetComponent<PlayerStatus>();

        void OnDestroy()
        {
            if (veil != null) Destroy(veil.transform.root.gameObject);
            if (tex != null) Destroy(tex);
        }

        void Update()
        {
            float now = status.Infection;
            if (now > 0f && last <= 0f) UI.Captions.Say("your skin begins to itch", true);
            else if (now <= 0f && last > 0f) UI.Captions.Say("the itching stops", true);
            else if (Crossed(now, 0.33f)) UI.Captions.Say("your skin is coming off under your nails", true);
            else if (Crossed(now, 0.66f)) UI.Captions.Say("you can't remember your name. water. water.", true);
            bool changed = !Mathf.Approximately(now, last);
            last = now;
            if (now <= 0f && veil == null) return;
            if (veil == null) Make();
            float t = Mathf.InverseLerp(0.08f, 1f, now);
            veil.color = new Color(0.34f, 0.07f, 0.04f, t * (0.8f + 0.12f * Mathf.Sin(Time.time * (2f + now * 4f))));
            veil.uvRect = new Rect(0.5f - 0.5f / (1f + t * 0.5f), 0.5f - 0.5f / (1f + t * 0.5f), 1f / (1f + t * 0.5f), 1f / (1f + t * 0.5f));
            if (changed) Arms(now);
            if (now > 0.3f && Time.time > nextVoice)
            {
                nextVoice = Time.time + Random.Range(45f, 70f) * (1.2f - now * 0.8f);
                voice.pitch = Random.Range(0.6f, 0.75f);
                voice.PlayOneShot(Audio.ProceduralAudio.Whisper(), 0.6f * Meta.AudioMix.Creatures);
                UI.Captions.Say(Pleas[Random.Range(0, Pleas.Length)], true);
            }
        }

        bool Crossed(float now, float at) => last < at && now >= at;

        void Arms(float now)
        {
            if (arms == null || arms.Length == 0)
            {
                var view = FindFirstObjectByType<FirstPersonArms>();
                if (view == null) return;
                arms = view.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                tint = new MaterialPropertyBlock();
            }
            foreach (var r in arms)
            {
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_BaseColor")) continue;
                tint.SetColor("_BaseColor", r.sharedMaterial.GetColor("_BaseColor") * Color.Lerp(Color.white, new Color(0.85f, 0.32f, 0.24f), now * 1.2f));
                r.SetPropertyBlock(now > 0f ? tint : null);
            }
        }

        void Make()
        {
            var canvas = new GameObject("Infection", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2;
            veil = new GameObject("Veil", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            veil.transform.SetParent(canvas.transform, false);
            var rt = veil.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            veil.raycastTarget = false;
            // Clear in the middle, thick and veined at the edges.
            const int W = 256, H = 144;
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Infection Veil" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float edge = Mathf.Clamp01((Mathf.Sqrt(u * u * 0.9f + v * v * 0.8f) - 0.45f) / 0.75f);
                    float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f), vein = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.PerlinNoise(x * 0.045f + 9f, y * 0.045f) - 0.5f) * 14f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(edge * (0.55f + 0.6f * n) + vein * edge * 0.6f)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            veil.texture = tex;
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
        }
    }
}
