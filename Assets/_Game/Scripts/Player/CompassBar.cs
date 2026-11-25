using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Backrooms.Player
{
    /// A slim heading strip at the top of the screen: ticks, N/E/S/W, and a gold marker for the open exit.
    /// World north is +Z, matching the objective text.
    public sealed class CompassBar
    {
        const float Width = 640f, Range = 90f;   // shows +-90 degrees
        static readonly Color Ink = new Color(0.94f, 0.92f, 0.87f), Gold = new Color(1f, 0.86f, 0.42f);
        readonly List<(Graphic g, float angle, float alpha)> marks = new List<(Graphic, float, float)>();
        readonly RectTransform marker;
        readonly Text markerLabel, heading;
        readonly Graphic markerGlyph;

        public CompassBar(Transform root)
        {
            var bar = Rect("Compass", root, new Vector2(0.5f, 0.975f), new Vector2(Width, 40f), new Vector2(0.5f, 1f));
            var bg = bar.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.28f);
            bg.raycastTarget = false;
            var clip = Rect("Clip", bar, new Vector2(0.5f, 0.5f), new Vector2(Width, 40f), new Vector2(0.5f, 0.5f));
            clip.gameObject.AddComponent<RectMask2D>();

            for (int a = 0; a < 360; a += 15)
            {
                bool card = a % 90 == 0, inter = a % 45 == 0;
                if (a % 45 == 0)
                {
                    string s = a switch { 0 => "N", 45 => "NE", 90 => "E", 135 => "SE", 180 => "S", 225 => "SW", 270 => "W", _ => "NW" };
                    var t = Label(clip, s, card ? 22 : 15, card ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.7f));
                    t.rectTransform.anchoredPosition = new Vector2(0f, 4f);
                    marks.Add((t, a, 1f));
                }
                else
                {
                    var tick = Rect("Tick", clip, new Vector2(0.5f, 0.5f), new Vector2(2f, 8f), new Vector2(0.5f, 0.5f));
                    tick.anchoredPosition = new Vector2(0f, -8f);
                    var img = tick.gameObject.AddComponent<Image>();
                    img.color = Ink;
                    img.raycastTarget = false;
                    marks.Add((img, a, 0.45f));
                }
            }

            marker = Rect("Exit", clip, new Vector2(0.5f, 0.5f), new Vector2(60f, 40f), new Vector2(0.5f, 0.5f));
            var glyph = Label(marker, "◆", 20, Gold);
            glyph.rectTransform.anchoredPosition = new Vector2(0f, 7f);
            markerGlyph = glyph;
            markerLabel = Label(marker, "", 12, Gold);
            markerLabel.rectTransform.anchoredPosition = new Vector2(0f, -10f);

            // The centre notch and the current bearing, always on top.
            var notch = Label(bar, "▼", 11, Gold);
            notch.rectTransform.anchorMin = notch.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            notch.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            heading = Label(bar, "", 12, new Color(Ink.r, Ink.g, Ink.b, 0.8f));
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            bar.SetAsFirstSibling();
        }

        public void Tick(float yaw, Vector3? waypoint, Vector3 from)
        {
            float ppd = Width / (Range * 2f);
            foreach (var (g, angle, alpha) in marks) Place(g, Mathf.DeltaAngle(yaw, angle), alpha, ppd);
            heading.text = $"{Mathf.RoundToInt(Mathf.Repeat(yaw, 360f)):000}°";
            marker.gameObject.SetActive(waypoint.HasValue);
            if (!waypoint.HasValue) return;
            var to = waypoint.Value - from;
            float d = Mathf.DeltaAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            marker.anchoredPosition = new Vector2(Mathf.Clamp(d * ppd, -Width / 2f + 24f, Width / 2f - 24f), 0f);   // pinned to the edge when off to the side
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            markerGlyph.color = new Color(Gold.r, Gold.g, Gold.b, pulse);
            markerLabel.text = $"EXIT {Mathf.RoundToInt(new Vector2(to.x, to.z).magnitude)} m";
        }

        static void Place(Graphic g, float delta, float baseAlpha, float ppd)
        {
            float x = delta * ppd, fade = Mathf.Clamp01((Range - Mathf.Abs(delta)) / 25f);   // soft at the edges
            g.rectTransform.anchoredPosition = new Vector2(x, g.rectTransform.anchoredPosition.y);
            var c = g.color;
            c.a = baseAlpha * fade;
            g.color = c;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            return rt;
        }

        static Text Label(Transform parent, string s, int size, Color c)
        {
            var rt = Rect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(60f, 30f), new Vector2(0.5f, 0.5f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UI.Ui.Font;
            t.text = s;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = c;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            return t;
        }
    }
}
