using UnityEngine;
using UnityEngine.UI;

namespace Backrooms.UI
{
    /// A sheet of paper held up in front of you: notes, the backs of paintings, and the logbook (a lined notebook
    /// with pages). It rises from below, sways a little in your hand, and the world dims behind it.
    public sealed class PaperView : MonoBehaviour
    {
        RectTransform sheet;
        RawImage paper;
        Image dim;
        Text title, body, footer, hint;
        float shown;
        bool open;
        static Texture2D sheetTex, notebookTex;
        static Font hand;

        /// A handwriting font from the system when there is one (Windows ships Ink Free and Segoe Print).
        public static Font Handwriting
        {
            get
            {
                if (hand != null) return hand;
                try { hand = Font.CreateDynamicFontFromOSFont(new[] { "Ink Free", "Segoe Print", "Bradley Hand ITC", "Comic Sans MS" }, 36); } catch { }
                return hand != null ? hand : hand = Ui.Font;
            }
        }

        public void Init(Transform canvas)
        {
            dim = Ui.Fill(canvas, new Color(0f, 0f, 0f, 0f), "Paper Dim");
            sheet = Ui.Rect(canvas, "Paper", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            sheet.sizeDelta = new Vector2(760f, 980f);
            paper = sheet.gameObject.AddComponent<RawImage>();
            paper.raycastTarget = false;
            var shadow = sheet.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(10f, -12f);

            title = Text(sheet, 46, new Vector2(0f, 1f), Vector2.one, new Vector2(80f, -150f), new Vector2(-60f, -60f), true);
            body = Text(sheet, 34, Vector2.zero, Vector2.one, new Vector2(80f, 110f), new Vector2(-60f, -150f), false);
            body.lineSpacing = 1.15f;
            footer = Text(sheet, 26, Vector2.zero, new Vector2(1f, 0f), new Vector2(80f, 40f), new Vector2(-60f, 100f), false);
            footer.alignment = TextAnchor.LowerRight;
            footer.color = new Color(0.25f, 0.2f, 0.18f, 0.8f);

            hint = Ui.Label(canvas, "", 22, TextAnchor.LowerCenter);
            hint.rectTransform.anchorMin = new Vector2(0.2f, 0.01f);
            hint.rectTransform.anchorMax = new Vector2(0.8f, 0.06f);
            hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            sheet.gameObject.SetActive(false);
        }

        Text Text(RectTransform parent, int size, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool bold)
        {
            var t = Ui.Label(parent, "", size, TextAnchor.UpperLeft, new Color(0.13f, 0.11f, 0.16f, 0.95f));   // ink
            t.font = Handwriting;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.rectTransform.anchorMin = anchorMin;
            t.rectTransform.anchorMax = anchorMax;
            t.rectTransform.offsetMin = offsetMin;
            t.rectTransform.offsetMax = offsetMax;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        /// Shows a note (notebook = false) or a logbook page. `seed` varies the creases and stains.
        public void Show(string heading, string text, string pageInfo, bool notebook, int seed, string controls)
        {
            if (!open) shown = 0f;
            open = true;
            sheet.gameObject.SetActive(true);
            if (notebookTex == null) notebookTex = MakePaper(true, 7);
            if (sheetTex == null) sheetTex = MakePaper(false, 3);
            paper.texture = notebook ? notebookTex : sheetTex;
            // Each note gets its own look: the texture is shared, so vary it by offsetting and flipping the UVs.
            float u = (seed & 3) * 0.08f, v = ((seed >> 2) & 3) * 0.05f;
            paper.uvRect = notebook ? new Rect(0f, 0f, 1f, 1f) : new Rect(u, v, (seed & 16) != 0 ? -0.84f : 0.84f, 0.9f);
            title.text = heading;
            body.text = text;
            footer.text = pageInfo;
            hint.text = controls;
            body.fontSize = text.Length > 600 ? 26 : text.Length > 350 ? 30 : 34;
        }

        public void Hide() { open = false; }

        void Update()
        {
            float target = open ? 1f : 0f;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * (open ? 4f : 6f));
            float e = shown * shown * (3f - 2f * shown);
            if (shown <= 0f && !open) { sheet.gameObject.SetActive(false); hint.text = ""; }
            float t = Time.unscaledTime;
            // Held in a hand: rises from below, settles at a slight angle, and never stays perfectly still.
            sheet.anchoredPosition = new Vector2((Mathf.PerlinNoise(t * 0.4f, 1.7f) - 0.5f) * 14f, Mathf.Lerp(-1150f, 25f, e) + (Mathf.PerlinNoise(3.1f, t * 0.35f) - 0.5f) * 12f);
            sheet.localRotation = Quaternion.Euler(Mathf.Lerp(30f, 6f, e), 0f, Mathf.Lerp(-14f, -2.5f, e) + (Mathf.PerlinNoise(t * 0.3f, 9.2f) - 0.5f) * 2f);
            sheet.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, e) * 0.8f;
            dim.color = new Color(0f, 0f, 0f, 0.5f * e);
        }

        // ---------- generated textures ----------

        /// Aged paper: warm off-white with fibres, darker worn edges, a couple of folds, a coffee ring.
        /// Notebook paper adds blue rules and a red margin.
        static Texture2D MakePaper(bool notebook, int seed)
        {
            const int W = 384, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Mirror };
            var px = new Color[W * H];
            var rnd = new System.Random(seed);
            float foldY = 0.35f + (float)rnd.NextDouble() * 0.3f, foldX = 0.3f + (float)rnd.NextDouble() * 0.4f;
            Vector2 ring = new Vector2(0.55f + (float)rnd.NextDouble() * 0.3f, 0.12f + (float)rnd.NextDouble() * 0.25f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;
                var c = notebook ? new Color(0.93f, 0.91f, 0.84f) : new Color(0.88f, 0.82f, 0.67f);
                float fibre = Mathf.PerlinNoise(u * 60f, v * 7f) * 0.05f + Mathf.PerlinNoise(u * 9f + 3f, v * 11f) * 0.07f;
                c *= 0.95f + fibre;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                c *= Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 0.09f, edge + Mathf.PerlinNoise(u * 13f, v * 13f) * 0.03f));   // worn, yellowed edges
                // Folds: a light ridge and a dark valley.
                float fy = Mathf.Abs(v - foldY), fx = Mathf.Abs(u - foldX);
                if (fy < 0.006f) c *= v > foldY ? 0.86f : 1.06f;
                if (fx < 0.005f) c *= u > foldX ? 0.9f : 1.05f;
                if (notebook)
                {
                    if (y % 28 == 0 && v < 0.9f) c = Color.Lerp(c, new Color(0.45f, 0.6f, 0.85f), 0.55f);
                    if (Mathf.Abs(u - 0.12f) < 0.003f) c = Color.Lerp(c, new Color(0.85f, 0.35f, 0.35f), 0.6f);
                }
                // A coffee ring.
                float d = Vector2.Distance(new Vector2(u, v * H / W), new Vector2(ring.x, ring.y * H / W));
                float r = Mathf.Abs(d - 0.11f);
                if (!notebook && r < 0.012f) c *= Mathf.Lerp(0.78f, 1f, r / 0.012f);
                c.a = 1f;
                px[y * W + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
