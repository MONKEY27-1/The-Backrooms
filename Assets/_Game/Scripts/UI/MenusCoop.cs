using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backrooms.Game;
using Backrooms.Meta;
using Backrooms.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Backrooms.UI
{
    /// Co-op hub, browser, join-by-code, create-lobby flow, lobby room, and in-game co-op session pages.
    public sealed partial class Menus
    {
        Text coopStatus;
        string lastCoopStatus = "";
        bool browsing;
        List<LobbyListing> listings = new List<LobbyListing>();
        string browseStatus = "", browseSearch = "";
        bool filterOpenSlots = true, filterNotStarted, filterInProgress, filterShowCodeOnly, refreshWhileViewing, filtersOpen;
        int sortMode; // 0 name,1 players
        string joinCodeInput = "", lobbyJoinCodeInput = "";
        LobbyListing selectedListing;
        // create lobby state
        enum CreateFrom { NewGame, ExistingSave }
        CreateFrom createFrom;
        SaveData selectedSave;
        string cLobbyName = "", cLobbyDesc = "";
        LobbyVisibility cVisibility = LobbyVisibility.Public;
        int cMaxPlayers = 4;
        bool cApprove;
        // new game config (mirrors NewGamePage state but kept separate for lobby flow)
        int cDiff = 1; GameMode cMode = GameMode.Survival; string cWorldName = ""; bool cHardcoreArmed;
        Action refreshCoopPage;

        public void CoopStatus(string text)
        {
            lastCoopStatus = text ?? "";
            if (coopStatus != null) coopStatus.text = lastCoopStatus;
        }

        void HookCoopStatus()
        {
            var net = session?.CoopNet;
            if (net == null) return;
            net.Status -= CoopStatus; net.Status += CoopStatus;
            net.LobbyChanged -= OnLobbyChanged; net.LobbyChanged += OnLobbyChanged;
        }

        void OnLobbyChanged()
        {
            // Avoid full-page rebuild storms while typing/interacting in browse/lobby UI.
            // Pages that need live updates set refreshCoopPage to a lightweight refresh.
            refreshCoopPage?.Invoke();
        }

        // ===== Hub (main menu) =====
        void CoopHubPage()
        {
            HookCoopStatus();
            refreshCoopPage = null;
            var body = OpenPage("Co-op", () => { browsing = false; refreshCoopPage = null; ClosePage(); ShowMain(); });
            var list = Ui.List(body, 8f);
            Ui.Paragraph(list, "Play with others: browse public games, join with a code, or create a lobby.", 24, Ui.Dim);
            Stretch(Ui.Button(Ui.Row(list, 64f), "Browse Public Games", () => { browsing = true; _ = BrowsePage(); }, 34).transform);
            Stretch(Ui.Button(Ui.Row(list, 64f), "Join by Code", JoinByCodePage, 34).transform);
            Stretch(Ui.Button(Ui.Row(list, 64f), "Create Lobby", CreateLobbyStartPage, 34).transform);
            coopStatus = Ui.Paragraph(list, lastCoopStatus, 22, Ui.Dim);
        }

        public void ReturnToPublicLobbyBrowser()
        {
            browsing = true;
            filtersOpen = false;
            _ = BrowsePage();
        }

        // ===== Browse =====
        async Task BrowsePage(bool reload = true)
        {
            HookCoopStatus();
            var body = OpenPage("Browse Public Games", () => { browsing = false; filtersOpen = false; CoopHubPage(); });
            // Compact top bar to avoid overlap; filters open in a dropdown panel.
            var topRect = Ui.Rect(body, "Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -170f), Vector2.zero);
            var top = Ui.List(topRect, 6f);
            if (reload || string.IsNullOrEmpty(browseStatus)) browseStatus = "Loading...";
            coopStatus = Ui.Paragraph(top, browseStatus, 22, Ui.Hot);
            Ui.Input(top, "Search", browseSearch, "Name, host, description", 40, v => { browseSearch = v; });
            var controls = Ui.Row(top, 52f, "Controls");
            var filtersBtn = Ui.Button(controls, filtersOpen ? "Filters ▲" : "Filters ▼", () => { filtersOpen = !filtersOpen; _ = BrowsePage(false); }, 26);
            filtersBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f);
            filtersBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
            filtersBtn.GetComponent<RectTransform>().offsetMin = new Vector2(10f, -22f);
            filtersBtn.GetComponent<RectTransform>().offsetMax = new Vector2(190f, 22f);
            var sortBtn = Ui.Button(controls, "Sort: " + (sortMode == 0 ? "Name" : "Players"), () => { sortMode = (sortMode + 1) % 2; _ = BrowsePage(false); }, 26);
            sortBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f);
            sortBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
            sortBtn.GetComponent<RectTransform>().offsetMin = new Vector2(200f, -22f);
            sortBtn.GetComponent<RectTransform>().offsetMax = new Vector2(420f, 22f);
            var refreshBtn = Ui.Button(controls, "Refresh", async () => { await LoadListings(); RenderBrowseList(); }, 26);
            refreshBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            refreshBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            refreshBtn.GetComponent<RectTransform>().offsetMin = new Vector2(-300f, -22f);
            refreshBtn.GetComponent<RectTransform>().offsetMax = new Vector2(-160f, 22f);
            var applyBtn = Ui.Button(controls, "Apply", () => { RenderBrowseList(); }, 26);
            applyBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            applyBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            applyBtn.GetComponent<RectTransform>().offsetMin = new Vector2(-150f, -22f);
            applyBtn.GetComponent<RectTransform>().offsetMax = new Vector2(-10f, 22f);

            var listRect = Ui.Rect(body, "ListArea", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -170f));
            browseListParent = Ui.Scroll(listRect);

            if (filtersOpen)
            {
                // Dropdown panel overlaying the list area
                var drop = Ui.Rect(body, "FiltersDropdown", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -430f), new Vector2(520f, -176f));
                var bg = drop.gameObject.AddComponent<UnityEngine.UI.Image>();
                bg.color = new Color(0.06f, 0.06f, 0.05f, 0.96f);
                var outline = drop.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = Ui.Dim;
                var content = Ui.Rect(drop, "Content", Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
                var fl = Ui.List(content, 4f);
                Ui.Toggle(fl, "Has open slots", () => filterOpenSlots, v => { filterOpenSlots = v; RenderBrowseList(); });
                Ui.Toggle(fl, "Not started", () => filterNotStarted, v => { filterNotStarted = v; RenderBrowseList(); });
                Ui.Toggle(fl, "In progress", () => filterInProgress, v => { filterInProgress = v; RenderBrowseList(); });
                Ui.Toggle(fl, "Show code-only", () => filterShowCodeOnly, v => { filterShowCodeOnly = v; RenderBrowseList(); });
                Ui.Toggle(fl, "Auto refresh", () => refreshWhileViewing, v => { refreshWhileViewing = v; });
            }

            if (reload) await LoadListings();
            RenderBrowseList();
        }

        RectTransform browseListParent;
        async Task LoadListings()
        {
            browseStatus = "Loading..."; if (coopStatus) coopStatus.text = browseStatus;
            listings = (await session.CoopNet.QueryPublicLobbies())?.ToList() ?? new List<LobbyListing>();
            browseStatus = listings.Count == 0 ? "No Public Games Available Try Again Later" : $"{listings.Count} game(s)";
            if (coopStatus) coopStatus.text = browseStatus;
        }

        IEnumerable<LobbyListing> FilteredListings()
        {
            IEnumerable<LobbyListing> q = listings;
            if (!string.IsNullOrWhiteSpace(browseSearch))
            {
                var s = browseSearch.Trim().ToLowerInvariant();
                q = q.Where(x => (x.Name ?? "").ToLowerInvariant().Contains(s) || (x.HostName ?? "").ToLowerInvariant().Contains(s) || (x.Description ?? "").ToLowerInvariant().Contains(s));
            }
            if (filterOpenSlots) q = q.Where(x => x.PlayerCount < x.MaxPlayers);
            if (filterNotStarted && !filterInProgress) q = q.Where(x => !x.InProgress);
            if (filterInProgress && !filterNotStarted) q = q.Where(x => x.InProgress);
            // if both or neither selected, don't filter by progress state
            if (!filterShowCodeOnly) q = q.Where(x => !x.RequiresCode);
            if (sortMode == 1) q = q.OrderByDescending(x => x.PlayerCount).ThenBy(x => x.Name); else q = q.OrderBy(x => x.Name);
            return q;
        }

        void RenderBrowseList()
        {
            if (browseListParent == null) return;
            foreach (Transform c in browseListParent) Destroy(c.gameObject);
            var items = FilteredListings().ToList();
            if (items.Count == 0)
            {
                Ui.Paragraph(browseListParent, "No Public Games Available Try Again Later", 26, Ui.Dim);
                return;
            }
            foreach (var l in items)
            {
                var entry = l;
                string vis = entry.RequiresCode ? "Code-only" : "Public";
                string prog = entry.InProgress ? $"In progress - { (string.IsNullOrEmpty(entry.LevelName) ? entry.Mode : entry.LevelName) }" : "In lobby";
                string meta = $"{entry.PlayerCount}/{entry.MaxPlayers}  ·  {prog}  ·  {vis}\nHost: {entry.HostName}  ·  {entry.Mode} {entry.Difficulty}\n{entry.Description}";
                var b = Ui.Button(browseListParent, $"{entry.Name}\n<size=20><color=#FFFFFFA0>{meta}</color></size>", () => ShowListingDetails(entry), 26, TextAnchor.UpperLeft);
                var le = b.GetComponent<LayoutElement>(); if (le != null) { le.preferredHeight = 130f; le.flexibleWidth = 1f; }
            }
        }

        void ShowListingDetails(LobbyListing l)
        {
            selectedListing = l;
            var body = OpenPage("Game Details", () => _ = BrowsePage(false));
            var list = Ui.Scroll(body);
            Ui.Paragraph(list, $"<b>{l.Name}</b>", 34);
            Ui.Paragraph(list, $"Host: {l.HostName}\nPlayers: {l.PlayerCount}/{l.MaxPlayers}\nStatus: {(l.InProgress ? "In progress" : "In lobby")}\nLevel/Area: {(string.IsNullOrEmpty(l.LevelName) ? "-" : l.LevelName)}\nMode: {l.Mode}\nDifficulty: {l.Difficulty}\nPing/Region: -\nVisibility: {(l.RequiresCode ? "Code-only (code required)" : "Public")}\nDescription: {l.Description}", 24);
            if (l.RequiresCode)
            {
                lobbyJoinCodeInput = "";
                Ui.Input(list, "Code", "", "Required to join this game", 12, v => lobbyJoinCodeInput = v);
            }
            var row = Ui.Row(list, 64f);
            Stretch(Ui.Button(row, "Join", async () =>
            {
                if (session.CoopNet.Busy) return;
                string code = l.RequiresCode ? lobbyJoinCodeInput : null;
                bool ok = await session.CoopNet.JoinById(l.Id, code);
                if (!ok) { lastCoopStatus = string.IsNullOrEmpty(lastCoopStatus) ? "Couldn't join: session unavailable or offline." : lastCoopStatus; CoopStatus(lastCoopStatus); ShowListingDetails(l); }
                else { EnterLobbyOrWait(); }
            }, 34).transform);
            coopStatus = Ui.Paragraph(list, lastCoopStatus, 22, Ui.Hot);
        }

        // ===== Join by code =====
        void JoinByCodePage()
        {
            HookCoopStatus();
            var body = OpenPage("Join by Code", CoopHubPage);
            var list = Ui.List(body, 8f);
            Ui.Input(list, "Your name", Coop.PlayerName, "Shown in the player list", 24, v => Coop.PlayerName = v);
            var field = Ui.Input(list, "Code", joinCodeInput, "Enter lobby/game code", 12, v => joinCodeInput = v);
            Ui.Paragraph(list, "Enter a code to join a public or private game/lobby.", 22, Ui.Dim);
            var row = Ui.Row(list, 64f);
            Stretch(Ui.Button(row, "JOIN", async () =>
            {
                if (session.CoopNet.Busy) return;
                bool ok = await session.JoinCoop(joinCodeInput);
                if (ok) EnterLobbyOrWait(); else { coopStatus.text = lastCoopStatus; }
            }, 36).transform);
            coopStatus = Ui.Paragraph(list, lastCoopStatus, 24, Ui.Hot);
            Select(field);
        }

        void EnterLobbyOrWait()
        {
            HookCoopStatus();
            var net = session.CoopNet;
            // if in lobby pre-start show lobby room; if in-progress world will arrive via OnWorldReceived and session will transition
            if (net.IsLobby) { session.EnterLobbyView(); LobbyRoomPage(); }
            else
            {
                // waiting for world; show status page
                var body = OpenPage("Joining...", CoopHubPage);
                var list = Ui.List(body, 8f);
                Ui.Paragraph(list, "Connecting to game world...", 26, Ui.Dim);
                coopStatus = Ui.Paragraph(list, lastCoopStatus, 24, Ui.Hot);
            }
        }


        // ===== Create lobby =====
        void CreateLobbyStartPage()
        {
            HookCoopStatus();
            var body = OpenPage("Create Lobby", CoopHubPage);
            var list = Ui.List(body, 8f);
            Ui.Paragraph(list, "Create a lobby from a new game or an existing save.", 24, Ui.Dim);
            var row = Ui.Row(list, 64f);
            Stretch(Ui.Button(row, "New Game", () => { createFrom = CreateFrom.NewGame; cWorldName = ""; cMode = GameMode.Survival; cDiff = 1; cHardcoreArmed = false; CreateNewGameSetupPage(); }, 34).transform);
            var row2 = Ui.Row(list, 64f);
            Stretch(Ui.Button(row2, "Existing Save", () => { createFrom = CreateFrom.ExistingSave; selectedSave = null; CreateSelectSavePage(); }, 34).transform);
        }

        void CreateNewGameSetupPage()
        {
            var body = OpenPage("New Game Setup", CreateLobbyStartPage);
            var list = Ui.Scroll(body);
            if (string.IsNullOrEmpty(cWorldName)) cWorldName = "World " + SaveSystem.NewSlot().Substring(5);
            Text details = null; Button difficultyButton = null; Text difficultyValue = null; RectTransform customSettings = null;
            void Refresh()
            {
                string text = cMode switch
                {
                    GameMode.Exploration => "Peaceful exploration. Survival meters do not drain.",
                    GameMode.Survival => "Manage water, batteries and sanity. Checkpoints are active.",
                    GameMode.Hardcore => "PERMADEATH: death ends this run and deletes its save. In co-op, dead players return only after the party beats three levels. This run can't be restored.",
                    GameMode.Endless => "Open-ended exploration with survival difficulty. No campaign missions or completion goal.",
                    GameMode.Story => "Rated missions in the procedural world. Story content is planned for a later update.",
                    _ => "Custom survival settings. Tune resource drains and pickup frequency.",
                };
                details.text = cMode == GameMode.Hardcore ? "<color=#FF5A4A>PERMADEATH WARNING</color>\n" + text : text;
                if (difficultyButton != null) difficultyButton.interactable = cMode != GameMode.Hardcore && cMode != GameMode.Exploration;
                if (difficultyValue != null) difficultyValue.text = Difficulty.Names[cDiff];
                if (customSettings != null) customSettings.gameObject.SetActive(cMode == GameMode.Custom);
            }
            Ui.Input(list, "Name", cWorldName, "Name this world", 32, v => cWorldName = v);
            string[] modes = { "Exploration", "Survival", "Hardcore", "Endless", "Story", "Custom" };
            Ui.Cycler(list, "Game mode", () => modes[(int)cMode], () => { cMode = (GameMode)(((int)cMode + 1) % modes.Length); if (cMode == GameMode.Hardcore) cDiff = 3; cHardcoreArmed = false; Refresh(); });
            difficultyButton = Ui.Cycler(list, "Difficulty", () => Difficulty.Names[cDiff], () => { cDiff = (cDiff + 1) % Difficulty.Names.Length; Refresh(); });
            var difficultyLabels = difficultyButton.GetComponentsInChildren<Text>(); if (difficultyLabels.Length > 1) difficultyValue = difficultyLabels[difficultyLabels.Length - 1];
            details = Ui.Paragraph(list, "", 22, Ui.Dim);
            customSettings = Ui.Row(list, 270f, "Custom Settings");
            var customLayout = customSettings.gameObject.AddComponent<VerticalLayoutGroup>();
            customLayout.spacing = 4f; customLayout.childControlHeight = true; customLayout.childControlWidth = true; customLayout.childForceExpandHeight = false; customLayout.childForceExpandWidth = true;
            Ui.Heading(customSettings, "CUSTOM SURVIVAL");
            Ui.Slider(customSettings, "Thirst drain", 0f, 3f, Difficulty.CustomThirst, Pct, v => Difficulty.SetCustom(v, Difficulty.CustomBattery, Difficulty.CustomSanity, Difficulty.CustomSupplies));
            Ui.Slider(customSettings, "Battery drain", 0f, 3f, Difficulty.CustomBattery, Pct, v => Difficulty.SetCustom(Difficulty.CustomThirst, v, Difficulty.CustomSanity, Difficulty.CustomSupplies));
            Ui.Slider(customSettings, "Sanity drain", 0f, 3f, Difficulty.CustomSanity, Pct, v => Difficulty.SetCustom(Difficulty.CustomThirst, Difficulty.CustomBattery, v, Difficulty.CustomSupplies));
            Ui.Slider(customSettings, "Supplies", 0f, 1f, Difficulty.CustomSupplies, Pct, v => Difficulty.SetCustom(Difficulty.CustomThirst, Difficulty.CustomBattery, Difficulty.CustomSanity, v));
            Refresh();
            var go = Ui.Row(list, 64f);
            Button nextBtn = null;
            nextBtn = Ui.Button(go, cMode == GameMode.Hardcore && !cHardcoreArmed ? "READ WARNING → CONTINUE" : "Next: Lobby Settings", () =>
            {
                if (cMode == GameMode.Hardcore && !cHardcoreArmed) { cHardcoreArmed = true; Ui.Text(nextBtn).text = "I UNDERSTAND → NEXT"; return; }
                createFrom = CreateFrom.NewGame; CreateLobbySettingsPage();
            }, 30);
            Stretch(nextBtn.transform);
        }

        void CreateSelectSavePage()
        {
            var body = OpenPage("Select Save", CreateLobbyStartPage);
            var list = Ui.Scroll(body);
            var saves = SaveSystem.List();
            if (saves.Count == 0) Ui.Paragraph(list, "No saves found.", 26, Ui.Dim);
            foreach (var save in saves)
            {
                var s = save;
                string level = session.Catalog.Find(s.levelId)?.displayName ?? s.levelId;
                string diffName = Difficulty.Names[Mathf.Clamp(s.difficulty, 0, Difficulty.Names.Length - 1)];
                string label = $"<b>{s.name}</b>  <color=#FFDB6B>{diffName}</color>\n{level}";
                var b = Ui.Button(list, label, () => { selectedSave = s; createFrom = CreateFrom.ExistingSave; CreateLobbySettingsPage(); }, 28, TextAnchor.UpperLeft);
                var le = b.GetComponent<LayoutElement>(); if (le != null) { le.preferredHeight = 90f; le.flexibleWidth = 1f; }
            }
        }

        void CreateLobbySettingsPage()
        {
            if (createFrom == CreateFrom.ExistingSave && selectedSave == null) { CreateSelectSavePage(); return; }
            var body = OpenPage("Lobby Settings", () => { if (createFrom == CreateFrom.NewGame) CreateNewGameSetupPage(); else CreateSelectSavePage(); });
            var list = Ui.Scroll(body);
            if (string.IsNullOrWhiteSpace(cLobbyName)) cLobbyName = Coop.PlayerName + "'s Lobby";
            Ui.Input(list, "Lobby name", cLobbyName, "Lobby name", 30, v => cLobbyName = v);
            Ui.Input(list, "Description", cLobbyDesc, "Optional description", 80, v => cLobbyDesc = v);
            Ui.Cycler(list, "Visibility", () => cVisibility == LobbyVisibility.Public ? "Public" : "Code-only", () => cVisibility = cVisibility == LobbyVisibility.Public ? LobbyVisibility.CodeOnly : LobbyVisibility.Public);
            Ui.Cycler(list, "Max players", () => cMaxPlayers.ToString(), () => cMaxPlayers = cMaxPlayers >= 4 ? 2 : cMaxPlayers + 1);
            Ui.Toggle(list, "Approve each join", () => cApprove, v => cApprove = v);
            string summary = createFrom == CreateFrom.NewGame ? $"New game: {cWorldName}  ·  {cMode}  ·  {Difficulty.Names[cDiff]}" : $"Save: {selectedSave.name}  ·  {session.Catalog.Find(selectedSave.levelId)?.displayName ?? selectedSave.levelId}";
            Ui.Paragraph(list, summary, 24, Ui.Dim);
            var row = Ui.Row(list, 64f);
            Stretch(Ui.Button(row, "Create Lobby", async () =>
            {
                if (session.CoopNet.Busy) return;
                CoopBans.ApproveJoins = cApprove;
                string modeSummary = createFrom == CreateFrom.NewGame ? cMode.ToString() : ((GameMode)Mathf.Clamp(selectedSave.mode,0,5)).ToString();
                string diffSummary = createFrom == CreateFrom.NewGame ? Difficulty.Names[cDiff] : Difficulty.Names[Mathf.Clamp(selectedSave.difficulty,0,3)];
                var code = await session.CoopNet.CreateLobby(cLobbyName, cLobbyDesc, cVisibility, cMaxPlayers, modeSummary, diffSummary);
                if (!string.IsNullOrEmpty(code))
                {
                    session.PrepareLobbyHost(createFrom == CreateFrom.NewGame, cWorldName, cDiff, cMode, selectedSave);
                    session.EnterLobbyView();
                    LobbyRoomPage();
                }

                else coopStatus.text = lastCoopStatus;
            }, 34).transform);
            coopStatus = Ui.Paragraph(list, lastCoopStatus, 22, Ui.Hot);
        }

        // ===== Lobby room (pre-start) =====
        void LobbyRoomPage()
        {
            HookCoopStatus();
            var net = session.CoopNet;
            if (net == null || !net.IsLobby) { refreshCoopPage = null; EnterLobbyOrWait(); return; }
            session.EnterLobbyView();
            // Dedicated lobby screen (not the main menu split layout).
            foreach (Transform c in column) Destroy(c.gameObject);
            refreshCoopPage = () =>
            {
                if (session == null || session.CoopNet == null || !session.CoopNet.IsLobby) return;
                // don't tear the page down while the host is typing in a settings field
                var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (sel != null && sel.GetComponentInParent<InputField>() != null && sel.transform.IsChildOf(page.transform)) return;
                LobbyRoomPage();
            };
            var body = OpenPage("Lobby", () =>
            {
                refreshCoopPage = null;
                if (session != null && session.InMenu) session.LeaveCoop();
                else { ClosePage(); Pause(); }
            }, true);
            // Keep the cinematic room visible behind the lobby UI.
            pageShade.enabled = false;

            int playerCount = net.People != null ? net.People.Count : CoopAvatar.All.Count;
            ulong myClient = net.Net != null ? net.Net.LocalClientId : 0;
            bool myReady = net.IsReady(myClient);
            var avatars = new List<CoopAvatar>(CoopAvatar.All.Values);
            avatars.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

            // ---- right rail: identity, join code, primary actions ----
            var rail = Ui.Rect(body, "Rail", new Vector2(1f, 0f), Vector2.one, new Vector2(-532f, 12f), new Vector2(-44f, -12f));
            rail.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var rl = Ui.List(Ui.Rect(rail, "Pad", Vector2.zero, Vector2.one, new Vector2(6f, 18f), new Vector2(-6f, -18f)), 10f);
            Ui.Paragraph(rl, $"<b><size=36>{net.LobbyName}</size></b>", 30);
            if (!string.IsNullOrWhiteSpace(net.LobbyDescription)) Ui.Paragraph(rl, net.LobbyDescription, 22, Ui.Dim);
            Ui.Paragraph(rl, net.SessionRequiresCode ? "<color=#FFCC66>Code-only</color>" : "Public game", 22, Ui.Dim);
            Ui.Heading(rl, "JOIN CODE");
            var codeRow = Ui.Row(rl, 72f, "CodeRow");
            var codeLabel = Ui.Label(codeRow, $"<b><size=56>{net.Code}</size></b>", 30, TextAnchor.MiddleLeft, Ui.Hot);
            codeLabel.rectTransform.anchorMin = Vector2.zero;
            codeLabel.rectTransform.anchorMax = Vector2.one;
            codeLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            codeLabel.rectTransform.offsetMax = new Vector2(-140f, 0f);
            var copyBtn = Ui.Button(codeRow, "COPY", () =>
            {
                GUIUtility.systemCopyBuffer = net.Code;
                CoopStatus("Code copied — send it to a friend.");
            }, 24, TextAnchor.MiddleCenter);
            Place(copyBtn.transform, new Vector2(-132f, -26f), new Vector2(-10f, 26f));
            Ui.Paragraph(rl, net.SessionRequiresCode ? "Friends need this code to join." : "Also listed publicly — anyone can join.", 21, Ui.Dim);
            Rule(rl);

            var readyRow = Ui.Row(rl, 64f, "ReadyRow");
            if (myReady) readyRow.gameObject.AddComponent<Image>().color = new Color(0.45f, 1f, 0.5f, 0.10f);
            Stretch(Ui.Button(readyRow, myReady ? "READY!  (click to undo)" : "READY UP", () => { net.SetReadyLocal(!myReady); LobbyRoomPage(); }, 34).transform);
            if (Coop.IsOwner)
            {
                bool allReady = net.AllReady();
                var startRow = Ui.Row(rl, 74f, "StartRow");
                if (allReady) startRow.gameObject.AddComponent<Image>().color = new Color(1f, 0.86f, 0.42f, 0.16f);
                Stretch(Ui.Button(startRow, allReady ? "<b><size=42>START</size></b>" : "START  <color=#FFFFFF80>(waiting for everyone)</color>", () =>
                {
                    if (!net.AllReady()) { CoopStatus("Everyone must be ready before starting."); LobbyRoomPage(); return; }
                    session.StartLobbyGame();
                }, 34).transform);
                Stretch(Ui.Button(Ui.Row(rl, 52f), "CLOSE LOBBY", () => { session.LeaveCoop(); }, 24).transform);
            }
            else
            {
                Stretch(Ui.Button(Ui.Row(rl, 52f), "LEAVE LOBBY", () => { session.LeaveCoop(); }, 24).transform);
            }
            coopStatus = Ui.Paragraph(rl, lastCoopStatus, 22, Ui.Dim);
            Ui.Paragraph(rl, "Right-drag: look around  ·  scroll: zoom  ·  click a player: details", 20, Ui.Dim);

            // ---- left column: squad, suit, settings ----
            var left = Ui.Rect(body, "Squad", Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-564f, 0f));
            var list = Ui.Scroll(left);
            Ui.Heading(list, $"SQUAD · {playerCount}/{net.ConfiguredMaxPlayers}");
            if (avatars.Count == 0) Ui.Paragraph(list, "Waiting for players...", 24, Ui.Dim);
            foreach (var a in avatars)
            {
                if (a == null || !a.IsSpawned) continue;
                ulong client = a.OwnerClientId;
                string who = net.People != null && net.People.TryGetValue(client, out var person) ? person.name : (string.IsNullOrEmpty(a.Nick) ? "..." : a.Nick);
                bool self = client == myClient;
                bool rdy = net.IsReady(client);
                bool canModerate = Coop.IsOwner && !self;

                var card = Ui.Row(list, 76f, "Card");
                card.gameObject.AddComponent<Image>().color = self ? new Color(1f, 1f, 1f, 0.07f) : new Color(1f, 1f, 1f, 0.035f);
                Swatch(card, SuitTint(a.Suit), 32f);
                string tags = (self ? "   <color=#FFDB6B>YOU</color>" : "") + (Coop.IsOwner && self ? "   <color=#FFDB6B>HOST</color>" : "");
                var name = Ui.Label(card, $"<size=30>{who}</size>{tags}", 26);
                name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.offsetMin = new Vector2(96f, 0f); name.rectTransform.offsetMax = new Vector2(-280f, 0f);
                float badgeRight = canModerate ? -330f : -40f;
                var badge = Ui.Label(card, rdy ? "READY" : "WAITING", 24, TextAnchor.MiddleRight, rdy ? new Color(0.56f, 1f, 0.56f) : new Color(1f, 0.8f, 0.4f));
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                badge.rectTransform.offsetMin = new Vector2(badgeRight - 220f, -20f);
                badge.rectTransform.offsetMax = new Vector2(badgeRight, 20f);
                if (canModerate)
                {
                    var kick = Ui.Button(card, "Kick", () => { net.Kick(client); LobbyRoomPage(); }, 24);
                    Place(kick.transform, new Vector2(-310f, -24f), new Vector2(-180f, 24f));
                    bool armed = false; Button ban = null;
                    ban = Ui.Button(card, "Ban", () => { if (!armed) { armed = true; Ui.Text(ban).text = "Sure?"; return; } net.Ban(client); LobbyRoomPage(); }, 24);
                    Place(ban.transform, new Vector2(-170f, -24f), new Vector2(-40f, 24f));
                }
            }

            Rule(list);
            Ui.Heading(list, "YOUR HAZMAT SUIT");
            int mySuit = Mathf.Clamp(PlayerPrefs.GetInt("coop_suit", 0), 0, CoopAvatar.SuitNames.Length - 1);
            var suitRow = Ui.Row(list, 64f, "Suit");
            Stretch(Ui.Button(suitRow, CoopAvatar.SuitNames[mySuit], () =>
            {
                int s = (PlayerPrefs.GetInt("coop_suit", 0) + 1) % CoopAvatar.SuitNames.Length;
                PlayerPrefs.SetInt("coop_suit", s); PlayerPrefs.Save();
                net.RequestSuit(s);
                LobbyRoomPage();
            }, 28).transform);
            Swatch(suitRow, SuitTint(mySuit), 200f);
            var suitHint = Ui.Label(suitRow, "click to change", 21, TextAnchor.MiddleRight, Ui.Dim);
            suitHint.rectTransform.anchorMin = suitHint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            suitHint.rectTransform.offsetMin = new Vector2(-280f, -18f); suitHint.rectTransform.offsetMax = new Vector2(-40f, 18f);
            net.RequestSuit(mySuit);

            Rule(list);
            Ui.Heading(list, "EMOTES");
            EmoteBar(list);

            if (Coop.IsOwner)
            {
                Rule(list);
                Ui.Heading(list, "HOST SETTINGS");
                LobbySettingsControls(list, net, inLobby: true);
            }
            Rule(list);
            VoiceSettingsCompact(list);
        }

        /// Clicking a floating nameplate in the preparation room.
        public void ShowLobbyPlayer(CoopAvatar avatar)
        {
            var net = session.CoopNet;
            if (avatar == null || !avatar.IsSpawned || net == null || !net.IsLobby) return;
            ulong client = avatar.OwnerClientId;
            bool self = net.Net != null && client == net.Net.LocalClientId;
            string who = net.People != null && net.People.TryGetValue(client, out var person) ? person.name : (string.IsNullOrEmpty(avatar.Nick) ? Coop.PlayerName : avatar.Nick);
            var body = OpenPage(self ? "Your Player" : "Player Info", LobbyRoomPage);
            var list = Ui.List(body, 10f);
            Ui.Paragraph(list, $"<b><size=38>{who}</size></b>\n{(net.IsReady(client) ? "<color=#90FF90>Ready</color>" : "<color=#FFCC66>Waiting</color>")}", 28);
            Ui.Heading(list, "HAZMAT SUIT");
            Ui.Paragraph(list, CoopAvatar.SuitNames[Mathf.Clamp(avatar.Suit, 0, CoopAvatar.SuitNames.Length - 1)], 28, Ui.Hot);
            if (self)
            {
                Stretch(Ui.Button(Ui.Row(list, 56f), "CHANGE HAZMAT SUIT", () =>
                {
                    int suit = (PlayerPrefs.GetInt("coop_suit", 0) + 1) % CoopAvatar.SuitNames.Length;
                    PlayerPrefs.SetInt("coop_suit", suit); PlayerPrefs.Save();
                    net.RequestSuit(suit);
                    LobbyRoomPage();
                }, 26).transform);
            }
            if (Coop.IsOwner && !self)
            {
                Ui.Heading(list, "HOST ACTIONS");
                Stretch(Ui.Button(Ui.Row(list, 52f), "KICK PLAYER", () => { net.Kick(client); LobbyRoomPage(); }, 26).transform);
                bool armed = false; Button ban = null;
                var row = Ui.Row(list, 52f);
                ban = Ui.Button(row, "BAN PLAYER", () => { if (!armed) { armed = true; Ui.Text(ban).text = "CONFIRM BAN"; return; } net.Ban(client); LobbyRoomPage(); }, 26);
                Stretch(ban.transform);
            }
        }

        /// Two compact rows of gestures that play on your suit in the room; the lobby used to expose none of them.
        void EmoteBar(Transform list)
        {
            int[] ids =
            {
                SuitEmote.Wave, SuitEmote.ThumbsUp, SuitEmote.Point, SuitEmote.ComeHere, SuitEmote.Laugh, SuitEmote.Salute,
                SuitEmote.Facepalm, SuitEmote.TakeL, SuitEmote.Dance, SuitEmote.Floss, SuitEmote.Sit,
            };
            for (int r = 0; r < 2; r++)
            {
                int count = r == 0 ? 6 : 5;
                var row = Ui.Row(list, 52f, "Emotes " + r);
                for (int i = 0; i < count; i++)
                {
                    int id = ids[r * 6 + i];
                    var cell = Ui.Rect(row, "Emote", new Vector2(i / (float)count, 0f), new Vector2((i + 1) / (float)count, 1f),
                        new Vector2(3f, 3f), new Vector2(-3f, -3f));
                    Stretch(Ui.Button(cell, SuitEmote.Names[id], () =>
                    {
                        if (CoopAvatar.Mine != null) CoopAvatar.Mine.PlayEmote(id);
                        else CoopStatus("Emotes need an active co-op session.");
                    }, 16, TextAnchor.MiddleCenter).transform);
                }
            }
            Ui.Paragraph(list, "Dance, Sit and Floss loop — pick the gesture again or press E to stop.", 20, Ui.Dim);
        }

        static void Rule(Transform list)

        {
            var row = Ui.Row(list, 26f, "Rule");
            var line = Ui.Rect(row, "Line", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(40f, -1f), new Vector2(-40f, 1f));
            line.gameObject.AddComponent<Image>().color = new Color(0.92f, 0.9f, 0.82f, 0.25f);
        }

        static Image Swatch(Transform parent, Color color, float left)
        {
            var rt = Ui.Rect(parent, "SuitSwatch", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(left, -15f), new Vector2(left + 30f, 15f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Color SuitTint(int i)
        {
            switch (Mathf.Abs(i) % 4)
            {
                case 1: return new Color(0.38f, 0.64f, 1f);    // Blue
                case 2: return new Color(1f, 0.36f, 0.30f);    // Red
                case 3: return new Color(0.44f, 0.88f, 0.50f); // Green
                default: return new Color(1f, 0.83f, 0.30f);   // Yellow
            }
        }

        void LobbySettingsControls(Transform list, CoopSession net, bool inLobby)
        {
            // editable by host
            string name = net.LobbyName, desc = net.LobbyDescription;
            var vis = net.Visibility; int max = net.ConfiguredMaxPlayers; bool approve = CoopBans.ApproveJoins;
            // text edits apply on end-edit, not per keystroke — avoids a full page rebuild for every character
            var nameField = Ui.Input(list, "Lobby name", name, "Lobby name", 30, v => name = v);
            nameField.onEndEdit.AddListener(v => { name = v; _ = net.UpdateLobbySettings(name, desc, vis, max, approve); });
            var descField = Ui.Input(list, "Description", desc, "Description", 80, v => desc = v);
            descField.onEndEdit.AddListener(v => { desc = v; _ = net.UpdateLobbySettings(name, desc, vis, max, approve); });
            Ui.Cycler(list, "Visibility", () => vis == LobbyVisibility.Public ? "Public" : "Code-only", () => { vis = vis == LobbyVisibility.Public ? LobbyVisibility.CodeOnly : LobbyVisibility.Public; _ = net.UpdateLobbySettings(name, desc, vis, max, approve); });
            Ui.Cycler(list, "Max players", () => max.ToString(), () => { max = max >= 4 ? 2 : max + 1; _ = net.UpdateLobbySettings(name, desc, vis, max, approve); });
            Ui.Toggle(list, "Approve each join", () => approve, v => { approve = v; _ = net.UpdateLobbySettings(name, desc, vis, max, approve); });
            Ui.Toggle(list, "Keep going when I leave", () => CoopBans.KeepGoing, v => { CoopBans.KeepGoing = v; });
            if (CoopBans.KeepGoing && Coop.IsOwner)
            {
                var guests = new List<(string id, string name)>();
                foreach (var kv in net.People) if (kv.Value.id != CoopSession.MyPlayerId) guests.Add(kv.Value);
                if (guests.Count > 0)
                {
                    int Current() { string want = PlayerPrefs.GetString("coop_heir", ""); int i = guests.FindIndex(g => g.id == want); return i < 0 ? 0 : i; }
                    Ui.Cycler(list, "Next owner", () => guests[Current()].name, () => { PlayerPrefs.SetString("coop_heir", guests[(Current() + 1) % guests.Count].id); PlayerPrefs.Save(); });
                }
            }
            Ui.Heading(list, "BANNED");
            if (CoopBans.All.Count == 0) Ui.Paragraph(list, "Nobody.", 22, Ui.Dim);
            foreach (var b in new List<CoopBans.Entry>(CoopBans.All))
            {
                var row = Ui.Row(list, 52f, b.name);
                var label = Ui.Label(row, $"{b.name}   <color=#FFFFFF80>since {b.when}</color>", 24);
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = new Vector2(40f, 0f); label.rectTransform.offsetMax = new Vector2(-170f, 0f);
                string id = b.id; var unban = Ui.Button(row, "Unban", () => { CoopBans.Unban(id); if (inLobby) LobbyRoomPage(); else CoopSessionPage(); }, 24);
                Place(unban.transform, new Vector2(-160f, -22f), new Vector2(-20f, 22f));
            }
        }

        // ===== In-game co-op session management (replaces "Open to Friends") =====
        void CoopSessionPage()
        {
            HookCoopStatus();
            var net = session.CoopNet;
            if (net == null) { refreshCoopPage = null; ClosePage(); return; }
            if (net.IsLobby) { LobbyRoomPage(); return; }
            refreshCoopPage = () =>
            {
                if (session == null || session.CoopNet == null || session.CoopNet.IsLobby) return;
                var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (sel != null && sel.GetComponentInParent<InputField>() != null && sel.transform.IsChildOf(page.transform)) return;
                CoopSessionPage();
            };
            var body = OpenPage("Co-op", () => { refreshCoopPage = null; ClosePage(); });
            var list = Ui.Scroll(body);
            if (net.Reconnecting)
            {
                Ui.Paragraph(list, "Lost the connection to the owner. Reconnecting... Your body is waiting where you stood.", 26, Ui.Hot);
                Ui.Paragraph(list, "Use Leave Game to stop trying.", 22, Ui.Dim);
            }
            else if (Coop.Active)
            {
                if (Coop.IsOwner) Ui.Paragraph(list, $"<size=26>Join code</size>\n<b><size=64>{net.Code}</size></b>", 30, Ui.Hot);
                Ui.Heading(list, "PLAYERS");
                foreach (var kv in net.People.ToList())
                {
                    ulong client = kv.Key; var p = kv.Value;
                    string who = p.name; string tag = (Coop.IsOwner && client == net.Net.LocalClientId) ? "  (you, host)" : client == net.Net.LocalClientId ? "  (you)" : "";
                    var row = Ui.Row(list, 56f, who);
                    var label = Ui.Label(row, $"{who}{tag}", 26);
                    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = new Vector2(40f, 0f); label.rectTransform.offsetMax = new Vector2(-300f, 0f);
                    if (Coop.IsOwner && client != net.Net.LocalClientId)
                    {
                        var kick = Ui.Button(row, "Kick", () => { net.Kick(client); CoopSessionPage(); }, 26);
                        Place(kick.transform, new Vector2(-290f, -22f), new Vector2(-160f, 22f));
                        bool armed = false; Button ban = null;
                        ban = Ui.Button(row, "Ban", () => { if (!armed) { armed = true; Ui.Text(ban).text = "Sure?"; return; } net.Ban(client); CoopSessionPage(); }, 26);
                        Place(ban.transform, new Vector2(-150f, -22f), new Vector2(-20f, 22f));
                    }
                }
                if (Coop.IsOwner)
                {
                    Ui.Heading(list, "SESSION SETTINGS");
                    LobbySettingsControls(list, net, inLobby: false);
                }
            }
            else
            {
                Ui.Paragraph(list, "No active co-op session.", 24, Ui.Dim);
            }
            VoiceSettingsCompact(list);
            coopStatus = Ui.Paragraph(list, lastCoopStatus, 22, Ui.Dim);
        }

        // Voice: compact for co-op pages; full mic/input settings also available in Settings > Audio
        void VoiceSettingsCompact(Transform list)
        {
            Ui.Heading(list, "VOICE");
            Ui.Cycler(list, "Microphone", () => MicModes[(int)VoiceChat.MicMode], () => VoiceChat.MicMode = (VoiceChat.Mode)(((int)VoiceChat.MicMode + 1) % MicModes.Length));
            Ui.Slider(list, "Friends' voices", 0f, 1f, VoiceChat.Volume, v => Mathf.RoundToInt(v * 100f) + "%", v => VoiceChat.Volume = v);
            Ui.Paragraph(list, "Microphone device and full audio settings are in Settings → Audio.", 21, Ui.Dim);
        }

        static readonly string[] MicModes = { "Open mic", "Push to talk (V)", "Off" };
        VoiceChat voiceSettings;
        void VoiceInputSetting(Transform list)
        {
            voiceSettings = session != null ? session.GetComponent<VoiceChat>() : null;
            Ui.Heading(list, "MICROPHONE");
            Ui.Cycler(list, "Mode", () => MicModes[(int)VoiceChat.MicMode], () => VoiceChat.MicMode = (VoiceChat.Mode)(((int)VoiceChat.MicMode + 1) % MicModes.Length));
            Ui.Cycler(list, "Input device", () => voiceSettings != null ? voiceSettings.InputDeviceLabel : "System Default",
                () => { if (voiceSettings != null) _ = voiceSettings.CycleInputDeviceAsync(); });
            Ui.Paragraph(list, "Changes take effect immediately. System Default follows your operating system's microphone setting.", 21, Ui.Dim);
        }
    }
}

