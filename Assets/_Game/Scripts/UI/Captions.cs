using System.Collections.Generic;
using Backrooms.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Backrooms.UI
{
    /// Sound captions along the bottom of the screen (when turned on), and achievement toasts in the top right.
    public sealed class Captions : MonoBehaviour
    {
        static Captions instance;
        Text lines, toast;
        readonly List<(string text, float until)> shown = new List<(string, float)>();
        readonly Queue<Achievements.Def> toasts = new Queue<Achievements.Def>();
        float toastUntil;

        /// A caption for a sound, e.g. "[footsteps behind you]". Repeats within a couple of seconds are merged.
        /// `always`: shown even with captions off (a voice in your head, not a sound).
        public static void Say(string text, bool always = false)
        {
            if (!always && !GameSettings.Current.captions) return;
            if (instance == null) instance = new GameObject("Captions").AddComponent<Captions>();
            instance.Add(text);
        }

        public static void Ensure() { if (instance == null) instance = new GameObject("Captions").AddComponent<Captions>(); }

        void Awake()
        {
            instance = this;
            var canvas = Ui.Canvas("Captions Canvas", 120).transform;
            canvas.SetParent(transform, false);
            lines = Ui.Label(canvas, "", 28, TextAnchor.LowerCenter);
            var rt = lines.rectTransform;
            rt.anchorMin = new Vector2(0.2f, 0.06f);
            rt.anchorMax = new Vector2(0.8f, 0.25f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var outline = lines.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);

            toast = Ui.Label(canvas, "", 26, TextAnchor.UpperRight);
            rt = toast.rectTransform;
            rt.anchorMin = new Vector2(0.55f, 0.8f);
            rt.anchorMax = new Vector2(0.98f, 0.97f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            toast.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            Achievements.Unlocked += OnUnlocked;
        }

        void OnDestroy() => Achievements.Unlocked -= OnUnlocked;

        void OnUnlocked(Achievements.Def d) => toasts.Enqueue(d);

        void Add(string text)
        {
            float now = Time.unscaledTime;
            int i = shown.FindIndex(s => s.text == text);
            if (i >= 0) shown[i] = (text, now + 3.5f);
            else shown.Add((text, now + 3.5f));
            if (shown.Count > 3) shown.RemoveAt(0);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            shown.RemoveAll(s => s.until < now);
            var sb = new System.Text.StringBuilder();
            foreach (var s in shown) sb.AppendLine(s.text);
            lines.text = sb.ToString();

            if (now > toastUntil && toasts.Count > 0)
            {
                var d = toasts.Dequeue();
                toast.text = $"<color=#FFDB6B>ACHIEVEMENT</color>\n<b>{d.Name}</b>\n<size=22>{d.Text}</size>";
                toastUntil = now + 5f;
                Ui.Click();
            }
            if (now > toastUntil) toast.text = "";
        }
    }
}
