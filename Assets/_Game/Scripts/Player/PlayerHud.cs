using Backrooms.World;
using Backrooms.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace Backrooms.Player
{
    /// The on-screen layer: small, quiet meters (stamina, thirst, sanity, batteries), the pickup prompt,
    /// the camcorder's REC overlay, an F8 debug readout, and what low sanity does to your eyes and ears.
    public sealed class PlayerHud : MonoBehaviour
    {
        PlayerStatus status;
        PlayerTools tools;
        FirstPersonController swimmer;
        ChunkStreamer streamer;
        Text meters, prompt, rec, recInfo, debug, flash, objective, voiceIndicator;

        /// The current level's goal, shown small in the top left ("" for none).
        public string Objective { get; set; } = "";
        CanvasGroup meterGroup;
        UI.PaperView paperView;
        int logPage = -1;
        float flashUntil, wakeMeters, lastThirst = 1f;
        GameObject camOverlay;
        Volume madness;
        AudioSource whisper;
        float nextHallucination;
        bool showDebug;

        void Awake()
        {
            status = GetComponent<PlayerStatus>();
            tools = GetComponent<PlayerTools>();
            swimmer = GetComponent<FirstPersonController>();
            streamer = FindFirstObjectByType<ChunkStreamer>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;   // scale with height, so wide windows don't push things off the top
            var root = canvasGo.transform;

            // Meters, bottom left: camcorder-style readouts (battery blocks, bars), dim while everything's fine.
            meters = Label(root, font, 22, TextAnchor.LowerLeft, new Vector2(0.018f, 0.03f), new Vector2(520, 220), new Vector2(0f, 0f));
            meters.font = UI.Ui.Font;
            meters.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.65f);
            meterGroup = meters.gameObject.AddComponent<CanvasGroup>();
            meters.lineSpacing = 1.2f;

            prompt = Label(root, font, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.42f), new Vector2(900, 44));
            prompt.font = UI.Ui.Font;
            prompt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            debug = Label(root, font, 20, TextAnchor.UpperLeft, new Vector2(0.01f, 0.98f), new Vector2(900, 160), new Vector2(0f, 1f));
            objective = Label(root, font, 22, TextAnchor.UpperLeft, new Vector2(0.018f, 0.965f), new Vector2(1200, 40), new Vector2(0f, 1f));
            objective.font = UI.Ui.Font;
            objective.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            voiceIndicator = Label(root, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 22, TextAnchor.UpperRight, new Vector2(0.985f, 0.96f), new Vector2(300, 42), new Vector2(1f, 1f));
            voiceIndicator.font = UI.Ui.Font;
            voiceIndicator.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            flash = Label(root, font, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.88f), new Vector2(1500, 90));
            flash.font = UI.PaperView.Handwriting;   // your own thoughts, in your own hand
            flash.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            // The sheet of paper you hold up: notes, painting backs, the logbook.
            paperView = canvasGo.AddComponent<UI.PaperView>();
            paperView.Init(root);

            camOverlay = new GameObject("Camcorder", typeof(RectTransform));
            camOverlay.transform.SetParent(root, false);
            Stretch((RectTransform)camOverlay.transform);
            rec = Label(camOverlay.transform, font, 38, TextAnchor.UpperLeft, new Vector2(0.06f, 0.92f), new Vector2(300, 50), new Vector2(0f, 1f));
            rec.color = new Color(1f, 0.25f, 0.2f);
            recInfo = Label(camOverlay.transform, font, 30, TextAnchor.LowerRight, new Vector2(0.94f, 0.08f), new Vector2(600, 90), new Vector2(1f, 0f));
            foreach (var corner in new[] { new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.9f), new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.1f) })
                Corner(camOverlay.transform, corner);
            camOverlay.SetActive(false);

            madness = MakeMadness();
            tools.Looted += m => Flash(m, 4f);
            tools.DoorLocked += m => Flash(m, 4f);
            whisper = gameObject.AddComponent<AudioSource>();
            whisper.spatialBlend = 1f;
            whisper.playOnAwake = false;
            nextHallucination = Time.time + 30f;
        }

        void Update()
        {
            meters.text = Meters();
            objective.text = showDebug || string.IsNullOrEmpty(Objective) ? "" : $"<color=#FFDB6B>\u25B8</color> {Objective}";
            var voice = VoiceChat.Instance;
            voiceIndicator.text = voice == null ? "" : voice.Muted ? "MIC MUTED" : voice.Talking ? "● MIC" : "";
            voiceIndicator.color = voice != null && voice.Muted ? new Color(1f, 0.35f, 0.3f) : new Color(0.4f, 1f, 0.55f);
            if (status.Thirst > lastThirst + 0.05f) wakeMeters = Time.time + 4f;   // just drank: show it
            lastThirst = status.Thirst;
            bool attention = status.Stamina < 0.99f || status.Thirst < 0.45f || status.Sanity < 0.45f
                || tools.FlashlightBattery < 0.3f || tools.CamcorderBattery < 0.3f || tools.BagOpen || Time.time < wakeMeters
                || (swimmer != null && swimmer.HasSwimControl && swimmer.Underwater);
            meterGroup.alpha = Mathf.MoveTowards(meterGroup.alpha, attention ? 1f : 0.3f, Time.deltaTime * 1.5f);

            var r = tools.LookingAtReadable;
            string e = Key(Controls.Interact);
            var spec = tools.GetComponent<Spectator>();
            prompt.text = swimmer != null && swimmer.IsSeated ? "Move or jump to stand up"
                : spec != null && spec.Watching ? (spec.Watched.Length > 0 ? $"Watching {spec.Watched}  ·  click to switch" : "Nobody left to watch...")
                : tools.ReadingText != null || tools.LogbookOpen ? ""
                : tools.LookingAt != null ? $"{e}  Take {tools.LookingAt.Label}"
                : r != null ? (r.Kind == ReadableKind.Painting ? $"{e}  Turn the painting over" : $"{e}  Read the note")
                : tools.LookingAtSwitch != null && !PoolSwitch.Pulled ? $"{e}  Pull the pump lever"
                : tools.LookingAtGoalDoor != null && tools.LookingAtGoalDoor.Locked ? $"{e}  Try the door"
                : tools.LookingAtFood != null ? $"{e}  {tools.LookingAtFood.Label}"
                : tools.LookingAtSeat != null ? $"{e}  Sit down"
                : tools.LookingAtFriend != null ? (status.AlmondWater > 0 ? $"{e}  Give almond water" : status.Batteries > 0 ? $"{e}  Give a battery" : "")
                : tools.LookingAtBoards != null ? (tools.LookingAtBoards.Pryable
                    ? (tools.LookingAtBoards.Progress > 0f ? $"{(tools.LookingAtBoards.LockedDoor ? "Forcing" : "Prying")}...  {Mathf.RoundToInt(tools.LookingAtBoards.Progress * 100)}%"
                       : tools.LookingAtBoards.Crate ? $"Hold {e}  Pry the lid off" : tools.LookingAtBoards.LockedDoor ? $"Hold {e}  Force the door" : $"Hold {e}  Pry the boards off")
                    : tools.LookingAtBoards.Crate ? "" : "Nailed shut. These won't come off.")
                : tools.BagOpen ? $"Click an item to use it      {Key(Controls.Bag)}  Close the bag" : "";
            Paper();
            if (Time.time > flashUntil) flash.text = "";

            camOverlay.SetActive(tools.CamcorderUp);
            if (tools.CamcorderUp)
            {
                rec.text = Mathf.Repeat(Time.time, 1f) < 0.6f ? "● REC" : "  REC";
                recInfo.text = $"{System.DateTime.Now:MMM dd yyyy}\n{System.DateTime.Now:hh:mm:ss tt}   BAT {Mathf.RoundToInt(tools.CamcorderBattery * 100)}%";
            }

            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) showDebug = !showDebug;
            debug.text = showDebug && streamer != null && streamer.Theme != null
                ? $"{streamer.Theme.displayName}\nDistrict: {streamer.Where}\nCopy drift: {streamer.CurrentDrift:0.00}\nPosition: {transform.position:F0}\nIn the dark: {status.InDark}\n{DirectorLine()}"
                : "";

            Madness();
        }

        Creatures.FearDirector director;
        string DirectorLine()
        {
            if (director == null) director = FindFirstObjectByType<Creatures.FearDirector>();
            return director != null ? $"Tension: {director.Tension:0.00} ({director.Status})   F4 resident, F5 hunt" : "";
        }

        /// A short message across the top of the screen.
        public void Flash(string message, float seconds = 6f)
        {
            flash.text = message;
            flashUntil = Time.time + seconds;
        }


        static string Key(UnityEngine.InputSystem.InputAction a) => $"<color=#FFDB6B>[{a.GetBindingDisplayString(0)}]</color>";

        /// VHS-style readouts, bottom line first on screen last.
        string Meters()
        {
            static string Blocks(float v, int n)
            {
                int f = Mathf.Clamp(Mathf.CeilToInt(v * n - 0.001f), 0, n);
                return new string('\u2588', f) + new string('\u2591', n - f);
            }
            string Line(string label, float v, int n, bool battery)
            {
                bool low = v < 0.2f;
                string col = !low ? "#F0EBDD" : Mathf.Repeat(Time.time, 0.8f) < 0.4f ? "#FF5A4A" : "#FF9A80";
                return battery ? $"<color={col}>{Blocks(v, n)}  {label,-6}{Mathf.RoundToInt(v * 100),4}%</color>"
                               : $"<color={col}>{label,-7}{Blocks(v, n)}</color>";
            }
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#F0EBDDAA>WATER x{status.AlmondWater}   BATT x{status.Batteries}</color>\n");
            if (status.Stamina < 0.99f) sb.Append(Line("STAMINA", status.Stamina, 10, false)).Append('\n');
            if (swimmer != null && swimmer.HasSwimControl && swimmer.Underwater)
            {
                sb.Append(Line(swimmer.Drowning ? "NO AIR" : "AIR", swimmer.Breath, 10, false)).Append('\n');
                var map = InputSystem.actions.FindActionMap("Player");
                if (map != null)
                    sb.Append($"<color=#F0EBDDAA>{Key(map.FindAction("Sprint"))} swim fast   {Key(map.FindAction("Jump"))} rise   {Key(map.FindAction("Crouch"))} dive</color>\n");
            }
            sb.Append(Line("MIND", status.Sanity, 10, false)).Append('\n');
            sb.Append(Line("THIRST", status.Thirst, 10, false)).Append('\n');
            sb.Append(Line("CAM", tools.CamcorderBattery, 5, true)).Append('\n');
            sb.Append(Line("LIGHT", tools.FlashlightBattery, 5, true));
            return sb.ToString();
        }

        // ---------- reading and the logbook ----------

        void Paper()
        {
            if (tools.ReadingText != null)
            {
                logPage = -1;
                paperView.Show(tools.ReadingTitle, tools.ReadingText, "", false, (tools.ReadingTitle ?? "").GetHashCode() & 0xFF, $"{Key(Controls.Interact)}  Put it down");
                return;
            }
            if (!tools.LogbookOpen) { logPage = -1; paperView.Hide(); return; }
            int count = status.Logbook.Count;
            if (count == 0) { paperView.Show("Logbook", "Nothing yet.\n\nNotes you find end up here.", "", true, 0, $"{Key(Controls.Logbook)}  Close"); return; }
            if (logPage < 0 || logPage >= count) logPage = count - 1;   // open on the newest
            var kb = Keyboard.current;
            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if ((kb != null && kb.leftArrowKey.wasPressedThisFrame) || wheel > 0.1f) logPage = Mathf.Max(0, logPage - 1);
            if ((kb != null && kb.rightArrowKey.wasPressedThisFrame) || wheel < -0.1f) logPage = Mathf.Min(count - 1, logPage + 1);
            var parts = status.Logbook[logPage].Split(new[] { '\n' }, 2);
            paperView.Show(parts[0], parts.Length > 1 ? parts[1] : "", $"page {logPage + 1} of {count}", true, logPage,
                $"<color=#FFDB6B>[\u2190] [\u2192]</color>  Turn the page      {Key(Controls.Logbook)}  Close");
        }

        /// Low sanity: the picture smears and bends, and you start hearing things.
        void Madness()
        {
            bool inStairwell = streamer != null && streamer.Theme != null && streamer.Theme.generator == LevelGenerator.Stairwell;
            float m = inStairwell ? 0f : Mathf.InverseLerp(0.6f, 0.05f, status.Sanity);
            // This level has no entities; keep low-sanity post-processing and hallucination audio out of the way.
            madness.weight = Mathf.MoveTowards(madness.weight, m, Time.deltaTime * 0.5f);
            if (inStairwell || status.Sanity > 0.4f || Time.time < nextHallucination) return;
            nextHallucination = Time.time + Random.Range(15f, 45f) * (0.5f + status.Sanity);
            // Footsteps or static from just behind you, where nothing is.
            var behind = -transform.forward * Random.Range(2f, 5f) + transform.right * Random.Range(-2f, 2f);
            whisper.transform.position = transform.position + behind + Vector3.up * 1.5f;
            if (Random.value < 0.6f)
            {
                var steps = Audio.ProceduralAudio.Footsteps();
                whisper.PlayOneShot(steps[Random.Range(0, steps.Length)], 0.7f * Meta.AudioMix.Effects);
                UI.Captions.Say("[footsteps behind you]");
            }
            else { whisper.PlayOneShot(Audio.ProceduralAudio.Static(), 0.15f * Meta.AudioMix.Effects); UI.Captions.Say("[crackling static]"); }
        }

        Volume MakeMadness()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<ChromaticAberration>(true).intensity.Override(1f);
            profile.Add<LensDistortion>(true).intensity.Override(0.35f);
            profile.Add<Vignette>(true).intensity.Override(0.55f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-40f);
            var go = new GameObject("Madness");
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 5f;
            v.weight = 0f;
            v.sharedProfile = profile;
            return v;
        }

        static Text Label(Transform root, Font font, int size, TextAnchor align, Vector2 anchor, Vector2 box, Vector2? pivot = null)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = box;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = new Color(1f, 1f, 1f, 0.85f);
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        static void Corner(Transform root, Vector2 anchor)
        {
            foreach (var size in new[] { new Vector2(60, 4), new Vector2(4, 60) })
            {
                var go = new GameObject("Corner", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = new Vector2(anchor.x < 0.5f ? 0f : 1f, anchor.y < 0.5f ? 0f : 1f);
                rt.sizeDelta = size;
                var img = go.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.7f);
                img.raycastTarget = false;
            }
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
