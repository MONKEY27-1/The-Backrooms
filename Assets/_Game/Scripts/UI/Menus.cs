using System;
using System.Collections.Generic;
using System.Linq;
using Backrooms.Game;
using Backrooms.Meta;
using Backrooms.Net;
using Backrooms.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Backrooms.UI
{
    /// The main menu and the pause menu, plus their pages: load game, settings, achievements, level log, stats.
    /// Looks like a camcorder tape's playback screen. Esc (or gamepad Start) pauses, and backs out of pages.
    public sealed partial class Menus : MonoBehaviour
    {
        GameSession session;
        RectTransform root, column, page;
        Image pauseShade, pageShade;
        Text title, tagline, tapeTop, tapeBottom;
        string titleText = "The Backrooms: Uncharted Reality";
        float nextGlitch, glitchUntil, flickerUntil;
        float titleNeed, titleFitWidth = -1f;
        string flickerText;
        Color titleInk;
        static readonly string[] MildTitles =
        {
            "The Backr00ms: Uncharted Reality",
            "Th3 Backrooms: UncHarTed R3aLitY",
            "The Backrooms: Uncharted Reality_",
            "The ackrooms: Unchard lity",
            "The Backrooms: Unch@rted Reality",
        };
        static readonly string[] LateTitles = { "YOU ARE HERE", "LET ME OUT", "STILL HERE?", "NO EXIT", "IT KNOWS YOU" };
        bool main, settingsDirty, rebinding;
        int escBlockedUntil, settingsTab, waitingFor = -1;
        public void BlockPauseForOneFrame() => escBlockedUntil = Time.frameCount + 1;
        int newGameDifficulty = 1;
        GameMode newGameMode = GameMode.Survival;
        bool hardcoreArmed;
        Action back;   // what Esc does on the open page
        Action openColumnAction;
        readonly List<Texture2D> thumbs = new List<Texture2D>();

        public bool Paused { get; private set; }
        bool Visible => root.gameObject.activeSelf;

        public void Init(GameSession s)
        {
            session = s;
            var canvas = Ui.Canvas("Menus", 90);
            canvas.transform.SetParent(transform, false);
            root = Ui.Stretch(canvas.transform, "Root");

            pauseShade = Ui.Fill(root, new Color(0f, 0f, 0f, 0.6f), "Pause Shade");
            // A soft dark fade from the left edge behind the title and buttons.
            var fade = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) fade.SetPixel(x, 0, new Color(0f, 0f, 0f, 0.6f * (1f - Mathf.SmoothStep(0f, 1f, x / 63f))));
            fade.Apply();
            var side = Ui.Rect(root, "Side Shade", Vector2.zero, new Vector2(0.55f, 1f)).gameObject.AddComponent<RawImage>();
            side.texture = fade;
            side.raycastTarget = false;
            pageShade = Ui.Rect(root, "Page Shade", new Vector2(0.38f, 0.04f), new Vector2(0.98f, 0.9f)).gameObject.AddComponent<Image>();
            pageShade.color = new Color(0.02f, 0.02f, 0.015f, 0.78f);
            pageShade.raycastTarget = false;

            title = Ui.Label(Ui.Rect(root, "Title", new Vector2(0.04f, 0.785f), new Vector2(0.78f, 0.95f)), titleText, 88, TextAnchor.LowerLeft);
            Fit(title);
            title.fontStyle = FontStyle.Bold;
            titleInk = title.color;
            FitTitle();
            tagline = Ui.Label(Ui.Rect(root, "Tagline", new Vector2(0.04f, 0.62f), new Vector2(0.35f, 0.71f)), "", 28, TextAnchor.UpperLeft, Ui.Dim);
            Fit(tagline);
            tapeTop = Ui.Label(Ui.Rect(root, "Tape Top", new Vector2(0.7f, 0.9f), new Vector2(0.97f, 0.97f)), "", 36, TextAnchor.UpperRight);
            Fit(tapeTop);
            tapeBottom = Ui.Label(Ui.Rect(root, "Tape Bottom", new Vector2(0.6f, 0.02f), new Vector2(0.97f, 0.08f)), "", 26, TextAnchor.LowerRight, Ui.Dim);
            Fit(tapeBottom);
            var credit = Ui.Label(Ui.Rect(root, "Credit", new Vector2(0.04f, 0.02f), new Vector2(0.35f, 0.06f)),
                "Inspired by the Backrooms Wiki · CC BY-SA 3.0", 18, TextAnchor.LowerLeft, Ui.Dim);
            Fit(credit);

            column = Ui.Rect(root, "Column", new Vector2(0.04f, 0.06f), new Vector2(0.35f, 0.61f));
            page = Ui.Rect(root, "Page", new Vector2(0.4f, 0.08f), new Vector2(0.96f, 0.86f));
            Hide();
        }

        static void Fit(Text t)
        {
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        }

        /// The full name is long: use the largest size that keeps it on one line instead of wrapping it over the menu.
        void FitTitle()
        {
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            float room = title.rectTransform.rect.width;
            titleFitWidth = room;
            if (room <= 1f) return;
            if (titleNeed <= 1f)   // measured once, over the title and its glitched spellings
            {
                string shown = title.text;
                int size = title.fontSize;
                title.fontSize = 88;
                titleNeed = 0f;
                for (int i = -1; i < MildTitles.Length; i++)
                {
                    title.text = i < 0 ? titleText : MildTitles[i];
                    titleNeed = Mathf.Max(titleNeed, title.preferredWidth);
                }
                title.text = shown;
                title.fontSize = size;
            }
            int fit = titleNeed > room ? Mathf.Max(36, Mathf.FloorToInt(88f * room / titleNeed)) : 88;
            if (title.fontSize != fit) title.fontSize = fit;
        }

        // ---------- modes ----------

        public void ShowMain()
        {
            main = true;
            Paused = false;
            root.gameObject.SetActive(true);
            pauseShade.enabled = false;
            title.gameObject.SetActive(true);
            tagline.text = Tagline();
            var saves = SaveSystem.List();
            title.text = titleText;
            title.color = titleInk;
            FitTitle();
            var items = new List<(string, Action)>();
            if (saves.Count > 0) items.Add(("Continue", () => session.Play(saves[0])));
            items.Add(("New Game", NewGamePage));
            items.Add(("Co-op", CoopHubPage));
            if (saves.Count > 0) items.Add(("Load Game", LoadPage));
            items.Add(("Settings", SettingsPage));
            items.Add(("Achievements", AchievementsPage));
            items.Add(("Level Log", LevelLogPage));
            items.Add(("Stats", StatsPage));
            items.Add(("Quit", Quit));
            Column(items.ToArray());
            ClosePage();
        }

        public void Hide()
        {
            ClosePage();
            root.gameObject.SetActive(false);
        }

        /// Show the menu UI root for lobby/overlay pages without rebuilding the main menu.
        public void ShowShell()
        {
            main = false;
            Paused = false;
            root.gameObject.SetActive(true);
            pauseShade.enabled = false;
            title.gameObject.SetActive(false);
            tagline.text = "";
            foreach (Transform c in column) Destroy(c.gameObject);
            ClosePage();
        }

        void Pause()
        {
            Paused = true;
            main = false;
            if (!Coop.Active)   // co-op never stops the world: only your menu opens
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            session.SetPlayerActive(false);
            root.gameObject.SetActive(true);
            pauseShade.enabled = true;
            title.gameObject.SetActive(true);
            title.text = "PAUSED";
            title.color = titleInk;
            tagline.text = "";
            var items = new List<(string, Action)> { ("Resume", Resume) };
            bool inCoop = Coop.Active || (session.CoopNet != null && session.CoopNet.Reconnecting);
            if (inCoop) items.Add(("Co-op", CoopSessionPage));
            items.Add(("Settings", SettingsPage));
            items.Add(("Level Log", LevelLogPage));
            items.Add(("Achievements", AchievementsPage));
            items.Add(("Stats", StatsPage));
            if (inCoop) items.Add(("Leave Game", () => { Resume(); session.LeaveCoop(); }));
            else items.Add(("Save & Quit to Menu", () => { Resume(); session.SaveAndQuit(); }));

            Column(items.ToArray());
            ClosePage();
        }

        void Resume()
        {
            Hide();
            Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            session.SetPlayerActive(true);
        }

        static void Quit()
        {
            Profile.Save();
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        string Tagline()
        {
            float p = Profile.Progress;
            if (p > 0.8f) return "There is no way out. Keep walking.";
            if (p > 0.5f) return "It remembers you.";
            if (Profile.Data.levels.Count > 0) return "You've been here before.";
            return "You shouldn't be here.";
        }

        void Column(params (string label, Action action)[] items)
        {
            foreach (Transform c in column) Destroy(c.gameObject);
            var list = Ui.List(column, 4f);
            Button first = null;
            foreach (var (label, action) in items)
            {
                var row = Ui.Row(list, 56f);
                var menuAction = action;
                var b = Ui.Button(row, label.ToUpperInvariant(), () =>
                {
                    if (openColumnAction == menuAction && back != null)
                    {
                        ClosePage();
                        return;
                    }
                    menuAction();
                    openColumnAction = back != null ? menuAction : null;
                }, 34);
                Stretch(b.transform);
                first ??= b;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            Select(first);
        }

        static void Select(Selectable s)
        {
            if (s != null && Gamepad.current != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Update()
        {
            bool emoteCancel = Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
            if (emoteCancel && CoopAvatar.Mine != null && CoopAvatar.Mine.HasActiveEmote)
            {
                CoopAvatar.Mine.CancelEmote();
                BlockPauseForOneFrame();
            }
            bool esc = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (esc && !rebinding && Time.frameCount > escBlockedUntil)
            {
                if (back != null) back();
                else if (Paused) Resume();
                else if (!Visible && session.CanPause) Pause();
            }
            if (!Visible) return;

            // Refit when the window size (and so the title's room) changes.
            if (main && title.gameObject.activeSelf && Mathf.Abs(title.rectTransform.rect.width - titleFitWidth) > 1f) FitTitle();

            float now = Time.unscaledTime;
            var date = DateTime.Now;
            tapeTop.text = Paused ? "❚❚ PAUSE" : Mathf.Repeat(now, 1.6f) < 1.1f ? "PLAY ▶" : "";
            tapeBottom.text = $"{date:MMM dd yyyy}  {date:hh:mm:ss tt}".ToUpperInvariant();
            if (main && title.gameObject.activeSelf) Glitch(now);
        }

        /// Now and then the title isn't quite what it should be; more often the further you've got.
        void Glitch(float now)
        {
            if (GameSettings.ReduceFlashing)
            {
                if (title.text != titleText) title.text = titleText;
                if (title.color != titleInk) title.color = titleInk;
                return;
            }
            float p = Profile.Progress;
            if (now >= glitchUntil)
            {
                if (title.text != titleText) title.text = titleText;
                if (title.color != titleInk) title.color = titleInk;
                if (now < nextGlitch)
                {
                    // Between bursts the tape slips: a character or two drop out for a few frames, so the
                    // title is never quite still.
                    if (now < flickerUntil)
                    {
                        title.text = flickerText;
                        title.color = Ui.Hot;
                    }
                    else if (UnityEngine.Random.value < 0.012f)
                    {
                        flickerUntil = now + UnityEngine.Random.Range(0.04f, 0.09f);
                        flickerText = Corrupt(Corrupt(titleText, 1), 1);
                    }
                    return;
                }
                nextGlitch = now + UnityEngine.Random.Range(1.6f, 4.2f) * Mathf.Lerp(1f, 0.3f, p);
                glitchUntil = now + UnityEngine.Random.Range(0.09f, 0.2f);
                bool late = p > 0.45f && UnityEngine.Random.value < 0.45f;
                title.text = late ? LateTitles[UnityEngine.Random.Range(0, LateTitles.Length)]
                                  : Corrupt(MildTitles[UnityEngine.Random.Range(0, MildTitles.Length)], 1);
                title.color = UnityEngine.Random.value < 0.5f ? Ui.Hot : new Color(1f, 0.42f, 0.42f, 1f);
                return;
            }
            // Mid-burst the corruption crawls and flickers between ink and colour.
            if (UnityEngine.Random.value < 0.45f)
            {
                title.text = Corrupt(title.text, 1);
                title.color = UnityEngine.Random.value < 0.5f ? title.color : titleInk;
            }
        }

        /// Swaps a few characters for tape-noise glyphs, keeping the length (so the line never reflows).
        static string Corrupt(string s, int count)
        {
            const string noise = "#%&@!?<>/\\_01";
            var chars = s.ToCharArray();
            for (int i = 0; i < count; i++)
            {
                int at = UnityEngine.Random.Range(0, chars.Length);
                if (chars[at] == ' ' || chars[at] == ':') continue;
                chars[at] = noise[UnityEngine.Random.Range(0, noise.Length)];
            }
            return new string(chars);
        }

        // ---------- pages ----------

        void SetPageLayout(bool full)
        {
            if (page == null) return;
            if (full)
            {
                page.anchorMin = new Vector2(0.05f, 0.04f);
                page.anchorMax = new Vector2(0.95f, 0.94f);
            }
            else
            {
                page.anchorMin = new Vector2(0.4f, 0.08f);
                page.anchorMax = new Vector2(0.96f, 0.86f);
            }
            page.offsetMin = page.offsetMax = Vector2.zero;
        }

        RectTransform OpenPage(string heading, Action onBack = null, bool full = false)
        {
            ClosePage();
            SetPageLayout(full);
            back = onBack ?? ClosePage;
            pageShade.enabled = true;
            if (title != null) title.gameObject.SetActive(false);
            if (tagline != null) tagline.gameObject.SetActive(false);
            var head = Ui.Label(Ui.Rect(page, "Heading", new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -64f), Vector2.zero), heading.ToUpperInvariant(), 40, TextAnchor.UpperLeft);
            Fit(head);
            var rule = Ui.Rect(page, "Rule", new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -70f), new Vector2(0f, -68f)).gameObject.AddComponent<Image>();
            rule.color = Ui.Dim;
            return Ui.Rect(page, "Body", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -84f));
        }

        void ClosePage()
        {
            if (settingsDirty) { GameSettings.Save(); settingsDirty = false; }
            foreach (var t in thumbs) if (t != null) Destroy(t);
            thumbs.Clear();
            if (page != null) foreach (Transform c in page) Destroy(c.gameObject);
            SetPageLayout(false);
            back = null;
            openColumnAction = null;
            if (pageShade != null) pageShade.enabled = false;
            if (title != null) title.gameObject.SetActive(true);
            if (tagline != null) tagline.gameObject.SetActive(main);
        }

        // New game: choose the rules for this saved world.

        void NewGamePage()
        {
            hardcoreArmed = false;
            var body = OpenPage("New Game");
            var list = Ui.Scroll(body);
            string slot = SaveSystem.NewSlot();
            string name = "World " + slot.Substring(5);
            Text details = null;
            Button difficultyButton = null;
            Text difficultyValue = null;
            RectTransform customSettings = null;
            void Refresh()
            {
                string text = newGameMode switch
                {
                    GameMode.Exploration => "Peaceful exploration. Survival meters do not drain.",
                    GameMode.Survival => "Manage water, batteries and sanity. Checkpoints are active.",
                    GameMode.Hardcore => "PERMADEATH: death ends this run and deletes its save. In co-op, dead players return only after the party beats three levels. This run can’t be restored.",
                    GameMode.Endless => "Open-ended exploration with survival difficulty. No campaign missions or completion goal.",
                    GameMode.Story => "Rated missions in the procedural world. Story content is planned for a later update.",
                    _ => "Custom survival settings. Tune resource drains and pickup frequency.",
                };
                details.text = newGameMode == GameMode.Hardcore ? "<color=#FF5A4A>PERMADEATH WARNING</color>\n" + text : text;
                if (difficultyButton != null) difficultyButton.interactable = newGameMode != GameMode.Hardcore && newGameMode != GameMode.Exploration;
                if (difficultyValue != null) difficultyValue.text = Difficulty.Names[newGameDifficulty];
                if (customSettings != null) customSettings.gameObject.SetActive(newGameMode == GameMode.Custom);
            }
            var field = Ui.Input(list, "Name", name, "Name this world", 32, v => name = v);
            string[] modes = { "Exploration", "Survival", "Hardcore", "Endless", "Story", "Custom" };
            Ui.Cycler(list, "Game mode", () => modes[(int)newGameMode], () =>
            {
                newGameMode = (GameMode)(((int)newGameMode + 1) % modes.Length);
                if (newGameMode == GameMode.Hardcore) newGameDifficulty = 3;
                hardcoreArmed = false;
                Refresh();
            });
            difficultyButton = Ui.Cycler(list, "Difficulty", () => Difficulty.Names[newGameDifficulty], () =>
            {
                newGameDifficulty = (newGameDifficulty + 1) % Difficulty.Names.Length;
                Refresh();
            });
            var difficultyLabels = difficultyButton.GetComponentsInChildren<Text>();
            if (difficultyLabels.Length > 1) difficultyValue = difficultyLabels[difficultyLabels.Length - 1];
            details = Ui.Paragraph(list, "", 22, Ui.Dim);
            customSettings = Ui.Row(list, 270f, "Custom Settings");
            var customLayout = customSettings.gameObject.AddComponent<VerticalLayoutGroup>();
            customLayout.spacing = 4f;
            customLayout.childControlHeight = true;
            customLayout.childControlWidth = true;
            customLayout.childForceExpandHeight = false;
            customLayout.childForceExpandWidth = true;
            Ui.Heading(customSettings, "CUSTOM SURVIVAL");
            Ui.Slider(customSettings, "Thirst drain", 0f, 3f, Difficulty.CustomThirst, Pct, v => Difficulty.SetCustom(v, Difficulty.CustomBattery, Difficulty.CustomSanity, Difficulty.CustomSupplies));
            Ui.Slider(customSettings, "Battery drain", 0f, 3f, Difficulty.CustomBattery, Pct, v => Difficulty.SetCustom(Difficulty.CustomThirst, v, Difficulty.CustomSanity, Difficulty.CustomSupplies));
            Ui.Slider(customSettings, "Sanity drain", 0f, 3f, Difficulty.CustomSanity, Pct, v => Difficulty.SetCustom(Difficulty.CustomThirst, Difficulty.CustomBattery, v, Difficulty.CustomSupplies));
            Ui.Slider(customSettings, "Supplies", 0f, 1f, Difficulty.CustomSupplies, Pct, v => Difficulty.SetCustom(Difficulty.CustomThirst, Difficulty.CustomBattery, Difficulty.CustomSanity, v));
            Refresh();
            var go = Ui.Row(list, 64f);
            string begin = newGameMode == GameMode.Hardcore
                ? hardcoreArmed ? "I UNDERSTAND — BEGIN HARDCORE" : "READ WARNING — CONTINUE"
                : "BEGIN";
            Button beginButton = null;
            beginButton = Ui.Button(go, begin, () =>
            {
                if (newGameMode == GameMode.Hardcore && !hardcoreArmed)
                {
                    hardcoreArmed = true;
                    Ui.Text(beginButton).text = "I UNDERSTAND — BEGIN HARDCORE";
                    return;
                }
                session.NewGame(name, newGameDifficulty, newGameMode);
            }, 30);
            Stretch(beginButton.transform);
            Select(field);
        }

        // Load game

        void LoadPage()
        {
            var body = OpenPage("Load Game");
            var list = Ui.Scroll(body);
            foreach (var save in SaveSystem.List())
            {
                var row = Ui.Row(list, 124f, save.slot);
                var thumb = Ui.Rect(row, "Thumb", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, -54f), new Vector2(232f, 54f)).gameObject.AddComponent<RawImage>();
                var tex = SaveSystem.LoadThumbnail(save.slot);
                if (tex != null) { thumbs.Add(tex); thumb.texture = tex; }
                else thumb.color = new Color(0.1f, 0.1f, 0.08f);
                string level = session.Catalog.Find(save.levelId)?.displayName ?? save.levelId;
                string when = DateTime.TryParse(save.lastPlayed, out var d) ? d.ToString("yyyy-MM-dd HH:mm") : "";
                string diffName = Difficulty.Names[Mathf.Clamp(save.difficulty, 0, Difficulty.Names.Length - 1)];
                var info = Ui.Label(row, $"<b>{save.name}</b>   <color=#FFDB6B>{diffName}</color>\n{level}\n<color=#FFFFFF80>{Hours(save.playSeconds)} played · {when}</color>", 24, TextAnchor.MiddleLeft);
                info.rectTransform.anchorMin = Vector2.zero;
                info.rectTransform.anchorMax = Vector2.one;
                info.rectTransform.offsetMin = new Vector2(256f, 6f);
                info.rectTransform.offsetMax = new Vector2(-240f, -6f);
                var picked = save;
                var play = Ui.Button(row, "Play", () => session.Play(picked), 28);
                Place(play.transform, new Vector2(-230f, 4f), new Vector2(-20f, 52f));
                bool armed = false;
                Button del = null;
                del = Ui.Button(row, "Delete", () =>
                {
                    if (!armed) { armed = true; Ui.Text(del).text = "Really?"; return; }
                    SaveSystem.Delete(picked.slot);
                    if (SaveSystem.List().Count == 0) ShowMain();
                    else LoadPage();
                }, 28);
                Place(del.transform, new Vector2(-230f, -52f), new Vector2(-20f, -4f));
            }
        }

        static void Place(Transform t, Vector2 min, Vector2 max)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.offsetMin = min;
            rt.offsetMax = max;
        }

        // Settings

        static readonly string[] Tabs = { "Graphics", "Audio", "Controls", "Comfort" };

        void SettingsPage()
        {
            var body = OpenPage("Settings");
            var tabs = Ui.Rect(body, "Tabs", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -56f), Vector2.zero);
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childForceExpandWidth = true;
            h.childControlWidth = true;
            h.childControlHeight = true;
            for (int i = 0; i < Tabs.Length; i++)
            {
                int tab = i;
                var b = Ui.Button(tabs, (i == settingsTab ? "[" + Tabs[i] + "]" : Tabs[i]).ToUpperInvariant(), () => { settingsTab = tab; SettingsPage(); }, 26);
                if (i == settingsTab) Select(b);
            }
            var list = Ui.Scroll(Ui.Rect(body, "Rows", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -66f)));
            var s = GameSettings.Current;
            switch (settingsTab)
            {
                case 0: Graphics(list, s); break;
                case 1:
                    Volume(list, "Master", s.master, v => s.master = v);
                    Volume(list, "Ambience (hum)", s.ambience, v => s.ambience = v);
                    Volume(list, "Effects", s.sfx, v => s.sfx = v);
                    Volume(list, "Creatures", s.creatures, v => s.creatures = v);
                    Volume(list, "Interface", s.ui, v => s.ui = v);
                    VoiceInputSetting(list);
                    Ui.Slider(list, "Friends' voices", 0f, 1f, VoiceChat.Volume, v => Mathf.RoundToInt(v * 100f) + "%", v => VoiceChat.Volume = v);
                    break;

                case 2: ControlsTab(list, s); break;
                case 3:
                    Ui.Toggle(list, "Head bob", () => s.headBob, v => Set(() => s.headBob = v));
                    Ui.Slider(list, "Camera effects", 0f, 1f, s.effects, Pct, v => Set(() => s.effects = v));
                    Ui.Paragraph(list, "Film grain, colour fringing, motion blur and head bob strength.", 22, Ui.Dim);
                    Ui.Toggle(list, "Reduce flashing", () => s.reduceFlashing, v => Set(() => s.reduceFlashing = v));
                    Ui.Paragraph(list, "Failing lights change slowly instead of strobing; no dropped frames or title glitches.", 22, Ui.Dim);
                    Ui.Toggle(list, "Sound captions", () => s.captions, v => Set(() => s.captions = v));
                    break;
            }
        }

        void Set(Action change)
        {
            change();
            settingsDirty = true;
            GameSettings.Apply();
        }

        // Co-op hub & lobby UI live in MenusCoop.cs partial.


        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        void Volume(Transform list, string label, float value, Action<float> set) =>
            Ui.Slider(list, label, 0f, 1f, value, Pct, v => Set(() => set(v)));

        void Graphics(Transform list, SettingsData s)
        {
            var sizes = Screen.resolutions.Select(r => (r.width, r.height)).Distinct().ToList();
            if (sizes.Count == 0) sizes.Add((Screen.width, Screen.height));
            int Current() { int i = sizes.FindIndex(r => r.width == (s.width > 0 ? s.width : Screen.width) && r.height == (s.height > 0 ? s.height : Screen.height)); return i < 0 ? sizes.Count - 1 : i; }
            Ui.Cycler(list, "Resolution", () => { var r = sizes[Current()]; return $"{r.width} × {r.height}"; },
                () => { var r = sizes[(Current() + 1) % sizes.Count]; Set(() => { s.width = r.width; s.height = r.height; }); });
            Ui.Toggle(list, "Fullscreen", () => s.fullscreen, v => Set(() => s.fullscreen = v));
            Ui.Toggle(list, "V-Sync", () => s.vsync, v => Set(() => s.vsync = v));
            Ui.Cycler(list, "Quality", () => QualityName(s.quality), () => Set(() => s.quality = (s.quality + 1) % 4));
            Ui.Paragraph(list, "Low: no reflections or bounce light. Medium: reflections. High: bounce light (SSGI). "
                + (GameSettings.RayTracingAvailable ? "Ultra: ray-traced bounce light." : "Ultra: longer shadows (ray tracing isn't available on this system)."), 22, Ui.Dim);
            Ui.Slider(list, "Field of view", 60f, 100f, s.fov, v => Mathf.RoundToInt(v) + "°", v => Set(() => s.fov = Mathf.Round(v)));
            Ui.Slider(list, "Brightness", -1f, 1f, s.brightness, v => (v >= 0f ? "+" : "") + v.ToString("0.0"), v => Set(() => s.brightness = v));
        }

        static string QualityName(int q) => q == 3 && GameSettings.RayTracingAvailable ? "Ultra (RT)" : GameSettings.QualityNames[q];

        void ControlsTab(Transform list, SettingsData s)
        {
            Ui.Slider(list, "Mouse sensitivity", 0.2f, 3f, s.sensitivity, v => "×" + v.ToString("0.00"), v => Set(() => s.sensitivity = v));
            Ui.Toggle(list, "Invert look", () => s.invertY, v => Set(() => s.invertY = v));
            Ui.Heading(list, "KEYS  (click, then press a key · Esc cancels)");
            var entries = Controls.Rebindable();
            for (int i = 0; i < entries.Count; i++)
            {
                int index = i;
                var e = entries[i];
                Ui.Cycler(list, e.Label, () => waitingFor == index ? "press a key…" : e.Shown, () =>
                {
                    if (rebinding) return;
                    rebinding = true;
                    waitingFor = index;
                    Controls.Rebind(e, () =>
                    {
                        rebinding = false;
                        waitingFor = -1;
                        escBlockedUntil = Time.frameCount + 1;   // the Esc that cancelled shouldn't also close the page
                        SettingsPage();
                    });
                });
            }
            var reset = Ui.Row(list, 56f);
            Stretch(Ui.Button(reset, "Reset keys to defaults", () => { Controls.ResetAll(); SettingsPage(); }, 28).transform);
        }

        // Achievements

        void AchievementsPage()
        {
            var body = OpenPage($"Achievements  {Profile.Data.achievements.Count} / {Achievements.All.Length}");
            var list = Ui.Scroll(body);
            foreach (var a in Achievements.All)
            {
                bool has = Achievements.Has(a.Id);
                string name = has || !a.Hidden ? a.Name : "???";
                string text = has || !a.Hidden ? a.Text : "A hidden achievement.";
                string date = has ? $"   <color=#FFFFFF70>{Achievements.DateOf(a.Id)}</color>" : "";
                Ui.Paragraph(list, $"<b>{name}</b>{date}\n<size=22>{text}</size>", 28, has ? Ui.Hot : Ui.Dim);
            }
        }

        // Level log

        void LevelLogPage()
        {
            var body = OpenPage($"Level Log  {Profile.Data.levels.Count(l => session.Catalog.Find(l.id) != null)} / {session.Catalog.levels.Count}");
            var list = Ui.Scroll(body);
            foreach (var level in session.Catalog.levels)
            {
                var visit = Profile.Visit(level.id);
                var row = Ui.Row(list, 52f);
                var t = level;
                var b = Ui.Button(row, visit != null ? level.displayName : $"Level {level.id} · ???", () => LevelPage(t), 28);
                Stretch(b.transform);
                b.interactable = visit != null;
            }
        }

        void LevelPage(World.LevelTheme level)
        {
            var body = OpenPage($"Level {level.id}", LevelLogPage);
            var list = Ui.Scroll(body);
            var v = Profile.Visit(level.id);
            var (danger, text) = LevelLog.Entry(level.id);
            Ui.Paragraph(list, $"<b>{level.displayName}</b>", 34);
            Ui.Paragraph(list, $"Danger: {danger}", 26, Ui.Hot);
            Ui.Paragraph(list, text, 26);
            if (v != null)
            {
                string from = string.IsNullOrEmpty(v.firstFrom) ? "" : $" from Level {v.firstFrom}";
                Ui.Paragraph(list, $"First entered {v.firstVisit}{from}.\nVisits: {v.visits}    Time spent: {Hours(v.seconds)}", 24, Ui.Dim);
            }
            Ui.Paragraph(list, LevelLog.Credit(level.id), 20, Ui.Dim);
        }

        // Stats

        void StatsPage()
        {
            var body = OpenPage("Stats");
            var list = Ui.Scroll(body);
            var p = Profile.Data;
            Ui.Paragraph(list,
                $"Distance walked        {p.distance / 1000f:0.00} km\n" +
                $"Time in the Backrooms  {Hours(p.playSeconds)}\n" +
                $"Longest stay           {Hours(p.longestSession)}\n" +
                $"Levels found           {p.levels.Count}\n" +
                $"Achievements           {p.achievements.Count} / {Achievements.All.Length}\n" +
                $"Times caught           {p.timesCaught}\n" +
                $"Hunts survived         {p.huntsSurvived}\n" +
                $"Almond water drunk     {p.almondWater}\n" +
                $"Notes read             {p.notesRead}\n" +
                $"Paintings turned       {p.paintingsTurned}\n" +
                $"Doorways pried open    {p.boardsPried}\n" +
                $"Longest bunny hop      {p.bestHopChain}\n" +
                $"Highest / lowest floor {p.highestFloor:+0;-0;0} / {p.lowestFloor:+0;-0;0}\n" +
                $"Footage recorded       {Hours(p.camcorderSeconds)}\n" +
                $"Saved worlds           {SaveSystem.List().Count}", 28);
        }

        static string Hours(float seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
        }
    }
}
