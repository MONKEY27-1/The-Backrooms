using System;
using Backrooms.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Backrooms.UI
{
    /// What's in this game, shown once each time it starts, before the main menu. Any key goes on.
    public sealed class ContentWarning : MonoBehaviour
    {
        const string Body =
            "EXTREME GORE and dismemberment\n" +
            "Lots of jumpscares and flashing lights\n\n" +
            "Graphic violence and death, mutilated bodies, body horror and infection, sudden loud noises,\n" +
            "darkness, tight spaces, deep water, insects, stalking, and moments that play tricks on your mind.\n\n" +
            "Flashing lights and screen glitches may trigger seizures in people with photosensitive epilepsy.\n" +
            "Stop playing at once and see a doctor if you feel dizzy or your sight changes.\n" +
            "Settings has Reduce Flashing, which softens them but does not remove everything.\n\n" +
            "Meant for adults. Best with headphones, at a volume you can stand.";

        static bool seen;
        GameSession session;
        Action then;
        Text prompt;
        float shownAt;

        /// Shows the warning and then calls `then`; straight away if it has been seen since the game started.
        public static void Show(GameSession session, Action then)
        {
            if (seen) { then(); return; }
            seen = true;
            var w = new GameObject("Content Warning").AddComponent<ContentWarning>();
            w.session = session;
            w.then = then;
        }

        void Start()
        {
            shownAt = Time.unscaledTime;
            var canvas = Ui.Canvas("Content Warning Canvas", 950);
            canvas.transform.SetParent(transform, false);
            Ui.Fill(canvas.transform, Color.black).raycastTarget = true;   // nothing behind it can be clicked
            Line(canvas.transform, "WARNING", 92, 0.8f, 0.94f, new Color(0.85f, 0.08f, 0.06f));
            Line(canvas.transform, Body, 32, 0.2f, 0.78f, Ui.Ink);
            prompt = Line(canvas.transform, "", 28, 0.07f, 0.14f, Ui.Dim);
        }

        static Text Line(Transform parent, string text, int size, float bottom, float top, Color color)
        {
            var t = Ui.Label(Ui.Rect(parent, "Line", new Vector2(0.04f, bottom), new Vector2(0.96f, top)), text, size, TextAnchor.MiddleCenter, color);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        void Update()
        {
            if (!session.InMenu) { Destroy(gameObject); return; }   // a run was started some other way (dev tools)
            if (Time.unscaledTime - shownAt < 2.5f) return;   // long enough that a stray key doesn't skip it
            prompt.text = "Press any key to continue";
            bool go = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                   || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                   || (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame));
            if (!go) return;
            then();
            Destroy(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => seen = false;
    }
}
