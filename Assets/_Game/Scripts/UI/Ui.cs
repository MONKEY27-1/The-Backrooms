using System;
using Backrooms.Meta;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Backrooms.UI
{
    /// A tiny runtime UGUI kit in a camcorder-tape style: monospace text, thin white rules, no boxes.
    public static class Ui
    {
        public static readonly Color Ink = new Color(0.92f, 0.9f, 0.82f, 0.95f);
        public static readonly Color Dim = new Color(0.92f, 0.9f, 0.82f, 0.45f);
        public static readonly Color Hot = new Color(1f, 0.86f, 0.42f, 1f);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.55f);

        static Font font;
        static AudioSource clickSource;
        static AudioClip click;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                // Windows ships these; anywhere else falls back to Unity's built-in font.
                try { font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Lucida Console", "Courier New" }, 32); } catch { }
                return font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one);

        public static Image Fill(Transform parent, Color color, string name = "Fill")
        {
            var img = Stretch(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Ink;
            t.text = text;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// A row in a vertical list with a fixed height.
        public static RectTransform Row(Transform list, float height, string name = "Row")
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            rt.SetParent(list, false);
            var le = rt.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            return rt;
        }

        /// A vertical list that grows with its content.
        public static RectTransform List(Transform parent, float spacing = 6f)
        {
            var rt = Stretch(parent, "List");
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return rt;
        }

        /// A scrolling vertical list inside `parent`. Returns the content to add rows to.
        public static RectTransform Scroll(Transform parent)
        {
            var view = Stretch(parent, "Scroll");
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var content = Rect(view, "Content", new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        /// A text button: dim until hovered or selected, then bright with a ▶ marker.
        public static Button Button(Transform parent, string text, Action onClick, int size = 34, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Button: " + text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = Mathf.Max(46f, size * 1.8f);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            var label = Label(go.transform, text, size, align);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(40f, 0f);
            lrt.offsetMax = Vector2.zero;
            var b = go.GetComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = colors.selectedColor = new Color(1f, 1f, 1f, 0.06f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            b.colors = colors;
            go.AddComponent<Marker>().Init(label);
            b.onClick.AddListener(() => { Click(); onClick?.Invoke(); });
            return b;
        }

        public static Text Text(Button b) => b.GetComponentInChildren<Text>();

        /// label ........ [ value ]   (click to cycle)
        public static Button Cycler(Transform parent, string label, Func<string> value, Action next)
        {
            var row = Row(parent, 46f, label);
            Text shown = null;
            var b = Button(row, label, () => { next(); shown.text = value(); }, 28);
            var brt = (RectTransform)b.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            shown = Label(b.transform, value(), 28, TextAnchor.MiddleRight);
            shown.rectTransform.anchorMin = new Vector2(0.55f, 0f);
            shown.rectTransform.anchorMax = Vector2.one;
            shown.rectTransform.offsetMin = Vector2.zero;
            shown.rectTransform.offsetMax = new Vector2(-20f, 0f);
            return b;
        }

        public static Button Toggle(Transform parent, string label, Func<bool> get, Action<bool> set) =>
            Cycler(parent, label, () => get() ? "On" : "Off", () => set(!get()));

        /// label ......  ====|=====  value
        public static Slider Slider(Transform parent, string label, float min, float max, float value, Func<float, string> format, Action<float> changed)
        {
            var row = Row(parent, 46f, label);
            var name = Label(row, label, 28);
            name.rectTransform.anchorMin = Vector2.zero;
            name.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            name.rectTransform.offsetMin = new Vector2(40f, 0f);
            name.rectTransform.offsetMax = Vector2.zero;
            var shown = Label(row, format(value), 28, TextAnchor.MiddleRight);
            shown.rectTransform.anchorMin = new Vector2(0.85f, 0f);
            shown.rectTransform.anchorMax = Vector2.one;
            shown.rectTransform.offsetMin = Vector2.zero;
            shown.rectTransform.offsetMax = new Vector2(-20f, 0f);

            var area = Rect(row, "Slider", new Vector2(0.47f, 0.5f), new Vector2(0.83f, 0.5f), new Vector2(0f, -12f), new Vector2(0f, 12f));
            var track = Rect(area, "Track", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -1.5f), new Vector2(0f, 1.5f)).gameObject.AddComponent<Image>();
            track.color = Dim;
            var fillArea = Rect(area, "Fill Area", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -1.5f), new Vector2(0f, 1.5f));
            var fill = Stretch(fillArea, "Fill").gameObject.AddComponent<Image>();
            fill.color = Ink;
            var handleArea = Stretch(area, "Handle Area");
            var handle = Rect(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), new Vector2(-5f, 0f), new Vector2(5f, 0f)).gameObject.AddComponent<Image>();
            handle.color = Ink;
            var s = area.gameObject.AddComponent<Slider>();
            s.fillRect = (RectTransform)fill.transform;
            s.handleRect = (RectTransform)handle.transform;
            s.targetGraphic = handle;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            var colors = s.colors;
            colors.highlightedColor = colors.selectedColor = Hot;
            s.colors = colors;
            s.onValueChanged.AddListener(v => { shown.text = format(v); changed(v); });
            return s;
        }

        /// label ......  [ typed text ]
        public static InputField Input(Transform parent, string label, string value, string placeholder, int maxLength, Action<string> changed)
        {
            var row = Row(parent, 52f, label);
            var name = Label(row, label, 28);
            name.rectTransform.anchorMin = Vector2.zero;
            name.rectTransform.anchorMax = new Vector2(0.35f, 1f);
            name.rectTransform.offsetMin = new Vector2(40f, 0f);
            name.rectTransform.offsetMax = Vector2.zero;
            var box = Rect(row, "Field", new Vector2(0.37f, 0.08f), new Vector2(0.97f, 0.92f));
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.07f);
            var text = Label(box, "", 28);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = Label(box, placeholder, 28, TextAnchor.MiddleLeft, Dim);
            foreach (var t in new[] { text, ph })
            {
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.offsetMin = new Vector2(12f, 0f);
                t.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }
            var f = box.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = ph;
            f.characterLimit = maxLength;
            f.text = value;
            f.caretColor = Ink;
            f.selectionColor = new Color(1f, 0.86f, 0.42f, 0.35f);
            f.onValueChanged.AddListener(v => changed(v));
            return f;
        }

        public static Text Heading(Transform parent, string text)
        {
            var row = Row(parent, 60f, text);
            var t = Label(row, text, 30, TextAnchor.LowerLeft, Dim);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(40f, 6f);
            return t;
        }

        public static Text Paragraph(Transform parent, string text, int size = 26, Color? color = null)
        {
            var row = new GameObject("Paragraph", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            var t = Label(row, text, size, TextAnchor.UpperLeft, color);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(40f, 0f);
            t.rectTransform.offsetMax = new Vector2(-20f, 0f);
            var fit = row.gameObject.AddComponent<LayoutElement>();
            fit.preferredHeight = t.preferredHeight + 10f;   // rough: the width is set later by the layout
            row.gameObject.AddComponent<FitText>().Init(t, fit);
            return t;
        }

        public static void Click()
        {
            if (clickSource == null)
            {
                clickSource = new GameObject("UI Sound").AddComponent<AudioSource>();
                UnityEngine.Object.DontDestroyOnLoad(clickSource.gameObject);
                clickSource.ignoreListenerPause = true;   // the pause menu pauses the listener
                clickSource.spatialBlend = 0f;
                click = Audio.ProceduralAudio.Static();
            }
            clickSource.pitch = 2.6f;
            clickSource.PlayOneShot(click, 0.05f * AudioMix.UI);
        }

        /// The ▶ marker and brightening when a button is hovered or selected (mouse, keys or gamepad).
        sealed class Marker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            Text label;
            string text;
            bool hover, selected;

            public void Init(Text t) { label = t; text = t.text; Refresh(); }
            public void OnPointerEnter(PointerEventData e) { hover = true; Refresh(); }
            public void OnPointerExit(PointerEventData e) { hover = false; Refresh(); }
            public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
            public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
            void OnDisable() { hover = selected = false; Refresh(); }

            void Refresh()
            {
                if (label == null) return;
                if (label.text.TrimStart('▶', ' ') != text) text = label.text.TrimStart('▶', ' ');   // the text was changed from outside
                bool on = hover || selected;
                label.color = on ? Hot : Ink;
                label.text = on ? "▶ " + text : text;
            }
        }

        /// Keeps a paragraph's height matched to its wrapped text once the layout gives it a width.
        sealed class FitText : MonoBehaviour
        {
            Text text;
            LayoutElement element;
            public void Init(Text t, LayoutElement e) { text = t; element = e; }
            void LateUpdate()
            {
                float h = text.preferredHeight + 10f;
                if (Mathf.Abs(element.preferredHeight - h) > 1f) element.preferredHeight = h;
            }
        }
    }
}
