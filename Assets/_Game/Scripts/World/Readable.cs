using UnityEngine;

namespace Backrooms.World
{
    /// A painting you can turn over, or a note you can read and keep in your logbook. Id is stable per position.
    public sealed class Readable : MonoBehaviour
    {
        public ReadableKind Kind;
        public int Id;
        public string Title, Text;

        static Material frame, paper;
        static Font font;

        /// Builds the object for a readable spec under `parent`, or null if a note was already taken.
        public static GameObject Create(Transform parent, ReadableSpec spec, uint seed, System.Func<int, bool> isPicked)
        {
            Vector3 at = spec.Pose.GetColumn(3);
            var rot = spec.Pose.rotation;
            int id = Pickup.IdAt(seed ^ 0x7EAD, at);
            if (spec.Kind == ReadableKind.Note && isPicked != null && isPicked(id)) return null;
            var go = new GameObject(spec.Kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, rot);

            if (spec.Kind == ReadableKind.Writing)
            {
                // Words written on the wall: a small world-space canvas just off the wall, facing out.
                font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(1600f, 600f);
                rt.localScale = Vector3.one * 0.001f;
                go.transform.position += rot * Vector3.forward * 0.012f;
                go.transform.rotation = rot * Quaternion.Euler(0f, 180f, 0f);   // a UI canvas reads from behind its forward, so turn it to face out
                var textGo = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                textGo.transform.SetParent(go.transform, false);
                var trt = (RectTransform)textGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                var text = textGo.GetComponent<UnityEngine.UI.Text>();
                text.text = Wrap(spec.Text, 26);
                text.font = font;
                text.fontSize = 110;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.28f, 0.1f, 0.06f, 0.9f);
                text.raycastTarget = false;
                return go;
            }

            frame = frame != null ? frame : Lit(new Color(0.22f, 0.15f, 0.09f), null);
            paper = paper != null ? paper : Lit(new Color(0.92f, 0.9f, 0.82f), null);
            var r = go.AddComponent<Readable>();
            r.Kind = spec.Kind;
            r.Id = id;
            r.Title = string.IsNullOrEmpty(spec.Title) ? (spec.Kind == ReadableKind.Note ? "A note" : "A painting") : spec.Title;
            r.Text = spec.Text ?? "";

            if (spec.Kind == ReadableKind.Painting)
            {
                Part(go.transform, frame, new Vector3(0f, 0f, 0.02f), new Vector3(1.0f, 0.76f, 0.04f));
                Part(go.transform, Lit(Color.white, Picture(id)), new Vector3(0f, 0f, 0.045f), new Vector3(0.88f, 0.64f, 0.01f));
                var c = go.AddComponent<BoxCollider>();
                c.center = new Vector3(0f, 0f, 0.05f);
                c.size = new Vector3(1.0f, 0.76f, 0.1f);
            }
            else
            {
                Part(go.transform, paper, new Vector3(0f, 0.002f, 0f), new Vector3(0.21f, 0.004f, 0.29f));
                var c = go.AddComponent<SphereCollider>();
                c.isTrigger = true;
                c.radius = 0.3f;
            }
            return go;
        }

        static void Part(Transform parent, Material m, Vector3 pos, Vector3 size)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = size;
            p.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        static Material Lit(Color c, Texture2D tex)
        {
            return HdMat.Make(c, 0.2f, 0f, tex);
        }

        /// A muted, slightly wrong landscape: sky, horizon, a dark shape where something stands.
        static Texture2D Picture(int id)
        {
            var rng = new WorldRng((uint)id);
            const int w = 64, h = 48;
            var tex = new Texture2D(w, h) { wrapMode = TextureWrapMode.Clamp };
            var sky = Color.HSVToRGB(rng.Value(), 0.25f, 0.75f);
            var ground = Color.HSVToRGB(rng.Value(), 0.35f, 0.4f);
            int horizon = rng.Range(14, 30), sx = rng.Range(10, 54), sh = rng.Range(6, 16);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = y < horizon ? ground * (0.8f + 0.2f * y / horizon) : Color.Lerp(sky, Color.white, (y - horizon) / (float)h * 0.6f);
                if (Mathf.Abs(x - sx) < 2 && y >= horizon && y < horizon + sh) c = new Color(0.08f, 0.07f, 0.06f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        static string Wrap(string s, int width)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            int line = 0;
            foreach (var word in s.Split(' '))
            {
                if (line > 0 && line + word.Length > width) { sb.Append('\n'); line = 0; }
                else if (line > 0) { sb.Append(' '); line++; }
                sb.Append(word);
                line += word.Length;
            }
            return sb.ToString();
        }
    }
}
