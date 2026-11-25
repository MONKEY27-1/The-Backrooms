using System;
using System.Collections;
using System.Linq;
using Backrooms.Creatures;
using Backrooms.Meta;
using Backrooms.Net;
using Backrooms.Player;
using Backrooms.UI;
using Backrooms.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Game
{
    /// Which world and level you're in: starts or continues a run, moves you between levels, autosaves checkpoints.
    public sealed partial class GameSession : MonoBehaviour
    {
        [SerializeField] LevelCatalog catalog;
        [SerializeField] ChunkStreamer streamer;
        [SerializeField] FirstPersonController player;
        [SerializeField] TransitionFx fx;
        [Tooltip("Developer tools only (editor or development build): type a seed to get that world. Empty = a new random world each new game.")]
        [SerializeField] string customSeed = "";

        SaveData save;
        bool busy;
        float nextSave;

        public bool Busy => busy;
        PlayerStatus status;
        PlayerTools tools;
        FearDirector director;
        PoolDrain pool;
        PoolAmbience poolAmbience;
        Collapse collapse;
        Flickering flickering;
        EnvironmentalEvents environmentalEvents;
        Warehouse warehouse;
        Level3Events level3;
        Level6Events level6;
        Level7Events level7;
        Level8Events level8;
        Level9Events level9;
        Objectives objectives;
        PartyLevel party;
        PartyLure lure;
        PartyScenes partyScenes;
        Courtyard courtyard;
        WindowLure windowLure;
        PlayerHud hud;
        ExplorationTools exploration;
        ProgressTracker tracker;
        Menus menus;
        DevMenu devMenu;
        AntiCheat antiCheat;
        MenuBackdrop backdrop;
        LobbyEnvironment lobbyEnvironment;
        string current;   // the level you're in, for the level log's "came from"
        bool enteredRunLevel;
        const uint MenuSeed = 0xB4C4F00D;

        /// Behind the main menu (no run in progress).
        public bool InMenu { get; private set; }
        public bool CanPause => !InMenu && !busy && (exploration == null || !exploration.IsOpen);
        public LevelCatalog Catalog => catalog;
        readonly System.Collections.Generic.HashSet<int> picked = new System.Collections.Generic.HashSet<int>();

        void OnEnable()
        {
            LevelTrigger.ExitEntered += OnExit;
            LevelTrigger.PitEntered += OnPit;
        }

        void OnDisable()
        {
            LevelTrigger.ExitEntered -= OnExit;
            LevelTrigger.PitEntered -= OnPit;
            if (player != null) player.Drowned -= OnPit;
        }

        void Start()
        {
            Controls.Load();
            GameSettings.Apply();
            Captions.Ensure();
            status = player.GetComponent<PlayerStatus>();
            hud = player.GetComponent<PlayerHud>();
            tools = player.GetComponent<PlayerTools>();
            exploration = player.GetComponent<ExplorationTools>() ?? player.gameObject.AddComponent<ExplorationTools>();
            exploration.Init(player.GetComponentInChildren<Camera>(), player, streamer, () => save, PersistExploration);
            exploration.SetMenuState(() => InMenu, () => menus?.BlockPauseForOneFrame());
            if (tools != null)
            {
                tools.PickedUp += p => { picked.Add(p.Id); save.picked.Add(p.Id); };
                tools.Threw += ThrowShared;
                tools.Read += r => { picked.Add(r.Id); save.picked.Add(r.Id); save.logbook.Add(r.Title + "\n" + r.Text); };
                tools.GoalTaken += i => { if (objectives != null) objectives.Took(i); };
                tools.Pried += b =>
                {
                    picked.Add(b.Id);
                    save.picked.Add(b.Id);
                    MakeNoise(b.transform.position, 30f);   // splintering wood carries
                };
            }
            BoardedDoor.Smashed = b =>
            {
                picked.Add(b.Id);
                if (save != null) save.picked.Add(b.Id);
                Share(Act.Boards, b.Id);
                MakeNoise(b.transform.position, 20f);
                Destroy(b.gameObject);
            };
            streamer.IsPicked = id => picked.Contains(id);
            ForgeLibrary.Reload();   // pick up rooms added since the last run
            if (!TryGetComponent(out director)) director = gameObject.AddComponent<FearDirector>();   // (not ??: Unity fakes null)
            director.Init(streamer);
            director.CaughtPlayer += OnCaught;
            PlayerAgent.Downed -= OnCaught;
            PlayerAgent.Downed += OnCaught;   // wounds ran out (a Hound): the same as being caught
            player.Landed += OnLanded;
            player.Drowned += OnPit;
            tracker = gameObject.AddComponent<ProgressTracker>();
            tracker.Init(player, tools, status, director, streamer);
            InitCoop();
            menus = gameObject.AddComponent<Menus>();
            menus.Init(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            devMenu = gameObject.AddComponent<DevMenu>();
            devMenu.Init(this, player, streamer, director, status, hud, () => save, () => catalog);
            devMenu.SetMenuState(() => InMenu || menus.Paused || busy);
#endif
            antiCheat = gameObject.AddComponent<AntiCheat>();
            antiCheat.Init(this, player, player.GetComponent<CharacterController>(), hud
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                , devMenu
#endif
            );
            ShowMenu();
        }

        /// Game-wide state that would otherwise carry over from the last Play session (scripts aren't reloaded).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PoolSwitch.Pulled = false;
            FirstPersonController.Chased = false;
            FirstPersonController.WetFloor = false;
            Difficulty.Level = 1;
            Difficulty.Mode = GameMode.Survival;
            DistrictLayout.ForgetShifts();
            VanishingExit.Forget();
        }

        /// The player's body, tools and HUD on or off (menus, pause).
        public void SetPlayerActive(bool on)
        {
            bool ghost = spectator != null && spectator.Watching;   // dead or through the exit (co-op): stays a ghost
            player.enabled = on && !ghost;
            if (tools != null) tools.enabled = on && !ghost;
            if (exploration != null) exploration.enabled = on && !ghost;
            if (hud != null)
            {
                hud.enabled = on;
                var c = hud.GetComponentInChildren<Canvas>(true);
                if (c != null) c.enabled = on;
            }
            if (!on) FirstPersonController.LockCursor(false);
        }

        /// Switch to the dedicated preparation-room scene, leaving no menu level loaded behind the lobby.
        public void EnterLobbyView()
        {
            if (busy) return;
            if (!InMenu)
            {
                InMenu = true;
                exploration?.CloseOverlay();
                current = null;
                SetPlayerActive(false);
                tools?.Stow();
                director.Reset();
                director.enabled = false;
                tracker.enabled = false;
                ClearLevelEvents();
            }
            if (lobbyEnvironment != null) { menus.ShowShell(); return; }
            if (backdrop != null) Destroy(backdrop);
            backdrop = null;
            streamer.Unload();
            DistrictLayout.ForgetShifts();
            FirstPersonController.WetFloor = false;
            player.WaterLevel = float.NegativeInfinity;
            player.HasSwimControl = false;
            player.GetComponent<CharacterController>().enabled = false;
            lobbyEnvironment = gameObject.AddComponent<LobbyEnvironment>();
            lobbyEnvironment.Init(player.GetComponentInChildren<Camera>(), menus);
            menus.ShowShell();
        }

        /// Always called before gameplay loads a level. The reverse path restores the ordinary main-menu backdrop.
        public void ExitLobbyView()
        {
            if (lobbyEnvironment == null) return;
            Destroy(lobbyEnvironment);
            lobbyEnvironment = null;
        }

        AudioSource lobbyJoinSound;
        void PlayLobbyJoinSound()
        {
            if (lobbyJoinSound == null)
            {
                lobbyJoinSound = gameObject.AddComponent<AudioSource>();
                lobbyJoinSound.playOnAwake = false;
                lobbyJoinSound.spatialBlend = 0f;
                lobbyJoinSound.clip = Audio.ProceduralAudio.Pop();
            }
            lobbyJoinSound.pitch = 1.35f;
            lobbyJoinSound.PlayOneShot(lobbyJoinSound.clip, 0.7f * Meta.AudioMix.UI);
        }


        /// The main menu, with the level drifting by behind it.
        void ShowMenu()
        {
            ExitLobbyView();
            InMenu = true;
            exploration?.CloseOverlay();
            current = null;
            SetPlayerActive(false);
            tools?.Stow();
            director.Reset();
            director.enabled = false;
            tracker.enabled = false;
            ClearLevelEvents();

            // Further into the game, the menu sometimes shows somewhere else you've been.
            var theme = catalog.Find("0") ?? catalog.levels[0];
            var been = Profile.Data.levels.Select(l => catalog.Find(l.id)).Where(t => t != null && t.id != "0" && t.id != "-1").ToList();
            if (Profile.Progress >= 0.5f && been.Count > 0 && UnityEngine.Random.value < 0.35f) theme = been[UnityEngine.Random.Range(0, been.Count)];
            DistrictLayout.ForgetShifts();
            FirstPersonController.WetFloor = false;
            player.WaterLevel = float.NegativeInfinity;
            player.HasSwimControl = false;
            streamer.Load(theme, catalog, WorldHash.LevelSeed(MenuSeed, theme.id), Vector2Int.zero);
            if (theme.id == "0") player.Teleport(DistrictLayout.OriginSpawn, DistrictLayout.OriginYaw);
            else if (theme.generator == LevelGenerator.Stairwell) player.Teleport(new Vector3(16f, 0.05f, 13.4f), 0f);
            else player.Teleport(SafeSpot(new Vector3(1f, 0.05f, 1f)), 45f);
            player.GetComponent<CharacterController>().enabled = false;   // the backdrop moves the body by hand
            backdrop = gameObject.AddComponent<MenuBackdrop>();
            backdrop.Init(player.transform, player.GetComponentInChildren<Camera>(), streamer, catalog, theme);
            Creatures.Blood.Clear();
            ContentWarning.Show(this, menus.ShowMain);
        }

        /// Starts a run: a saved world, or a brand-new one when `slot` is null.
        public void Play(SaveData slot)
        {
            if (busy) return;
            StartCoroutine(Transition(() =>
            {
                if (backdrop != null) Destroy(backdrop);
                backdrop = null;
                ExitLobbyView();
                save = slot ?? NewSave();
                enteredRunLevel = false;
                if (catalog.Find(save.levelId) == null) save.levelId = catalog.levels[0].id;
                InMenu = false;
                menus.Hide();
                director.enabled = true;
                tracker.enabled = true;
                tracker.StartSession();
                SetPlayerActive(true);
                Begin();
            }));
        }

        /// A brand-new world with the player's chosen name and difficulty.
        public void NewGame(string name, int difficulty, GameMode mode = GameMode.Survival)
        {
            if (busy) return;
            Play(NewSave(name, difficulty, mode));
        }

        /// Autosaves and goes back to the main menu.
        public void SaveAndQuit()
        {
            if (busy || InMenu) return;
            Persist();
            if (Coop.Active) _ = coop.Leave();
            StartCoroutine(Transition(ShowMenu));
        }


        void OnApplicationQuit()
        {
            if (!InMenu && save != null) Persist();
            Profile.Save();
            GameSettings.Save();
        }

        /// A Level 3 Sanctum: from now on you wake up here when caught in this level.
        public void SetShrineCheckpoint(Vector3 at)
        {
            if (save == null) return;
            save.shrineLevel = save.levelId;
            save.shrineX = at.x;
            save.shrineY = at.y;
            save.shrineZ = at.z;
            Persist();
        }

        void ClearLevelEvents()
        {
            if (level3 != null) Destroy(level3);
            level3 = null;
            if (level7 != null) Destroy(level7);
            level7 = null;
            if (level8 != null) Destroy(level8);
            level8 = null;
            if (level9 != null) Destroy(level9);
            level9 = null;
            if (level6 != null) Destroy(level6);
            level6 = null;
            if (collapse != null) Destroy(collapse);
            if (flickering != null) Destroy(flickering);
            if (pool != null) Destroy(pool);
            if (poolAmbience != null) Destroy(poolAmbience);
            if (warehouse != null) Destroy(warehouse);
            if (environmentalEvents != null) Destroy(environmentalEvents);
            warehouse = null;
            environmentalEvents = null;
            if (objectives != null) Destroy(objectives);
            objectives = null;
            if (party != null) Destroy(party);
            party = null;
            if (partyScenes != null) Destroy(partyScenes);
            partyScenes = null;
            if (lure != null) Destroy(lure);
            lure = null;
            if (courtyard != null) Destroy(courtyard);
            courtyard = null;
            if (windowLure != null) Destroy(windowLure);
            windowLure = null;
            collapse = null;
            flickering = null;
            pool = null;
            poolAmbience = null;
        }

        /// Starts (or continues) the run in `save`.
        void Begin()
        {
            Difficulty.Level = save.difficulty;
            Difficulty.Mode = (GameMode)Mathf.Clamp(save.mode, 0, Enum.GetValues(typeof(GameMode)).Length - 1);
            Difficulty.SetCustom(save.customThirst, save.customBattery, save.customSanity, save.customSupplies);
            streamer.Curiosities = new EncounterDirector(save);
            picked.Clear();
            foreach (int id in save.picked) picked.Add(id);
            if (status != null)
            {
                status.Thirst = save.thirst;
                status.Sanity = save.sanity;
                status.Stamina = 1f;
                status.AlmondWater = save.almondWater;
                status.Batteries = save.batteries;
                status.Glowsticks = save.glowsticks;
                status.Exterminators = save.exterminators;
                status.FirstAid = save.firstAid;
                status.Repellent = save.repellent;
                status.Flares = save.flares;
                status.Infection = save.infection;
                status.Health = save.health > 0f ? save.health : 1f;
                status.Logbook.Clear();
                status.Logbook.AddRange(save.logbook);
            }
            if (tools != null)
            {
                tools.FlashlightBattery = save.flashlightBattery;
                tools.CamcorderBattery = save.camcorderBattery;
            }
            Enter(save.levelId, new Vector2Int(save.arrivalX, save.arrivalZ));
        }

        void PersistExploration()
        {
            if (save == null) return;
            if (status != null)
            {
                save.thirst = status.Thirst;
                save.sanity = status.Sanity;
                save.almondWater = status.AlmondWater;
                save.batteries = status.Batteries;
                save.glowsticks = status.Glowsticks;
                save.exterminators = status.Exterminators;
                save.firstAid = status.FirstAid;
                save.repellent = status.Repellent;
                save.flares = status.Flares;
                save.infection = status.Infection;
                save.health = status.Health;
            }
            if (tools != null)
            {
                save.flashlightBattery = tools.FlashlightBattery;
                save.camcorderBattery = tools.CamcorderBattery;
            }
            if (!string.IsNullOrEmpty(save.slot)) SaveSystem.Save(save);
            else SaveSystem.SaveExploration(save);
        }

        /// Copies the player's state into the save and writes it.
        void Persist()
        {
            if (save == null || save.slot == null) return;   // a co-op guest's copy of the owner's world
            if (status != null)
            {
                save.thirst = status.Thirst;
                save.sanity = status.Sanity;
                save.almondWater = status.AlmondWater;
                save.batteries = status.Batteries;
                save.glowsticks = status.Glowsticks;
                save.exterminators = status.Exterminators;
                save.firstAid = status.FirstAid;
                save.repellent = status.Repellent;
                save.flares = status.Flares;
                save.infection = status.Infection;
                save.health = status.Health;
            }
            if (tools != null)
            {
                save.flashlightBattery = tools.FlashlightBattery;
                save.camcorderBattery = tools.CamcorderBattery;
            }
            SaveSystem.Save(save);
            SaveSystem.SaveThumbnail(save.slot, player.GetComponentInChildren<Camera>());
            Profile.Save();
        }

        /// Every new game gets its own world. A typed seed only counts with developer tools on (editor or
        /// development build); players can't pick or change their seed.
        SaveData NewSave(string name = null, int difficulty = 1, GameMode mode = GameMode.Survival)
        {
            uint seed = (uint)Guid.NewGuid().GetHashCode();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrWhiteSpace(customSeed)) seed = WorldHash.StringHash(customSeed.Trim());
#endif
            string slot = SaveSystem.NewSlot();
            Difficulty.Level = difficulty;
            Difficulty.Mode = mode;
            return new SaveData
            {
                slot = slot, name = string.IsNullOrWhiteSpace(name) ? "World " + slot.Substring(5) : name.Trim(), difficulty = difficulty, mode = (int)mode,
                customThirst = Difficulty.CustomThirst, customBattery = Difficulty.CustomBattery, customSanity = Difficulty.CustomSanity, customSupplies = Difficulty.CustomSupplies,
                worldSeed = seed, levelId = catalog.levels[0].id, almondWater = Difficulty.StartWater, batteries = Difficulty.StartBatteries,
            };
        }

        void Enter(string levelId, Vector2Int arrival)
        {
            exploration?.CloseOverlay();
            var theme = catalog.Find(levelId);
            director.enabled = true;
            director.StepsOnly = !Coop.Authority;   // a co-op guest: the owner's game runs the scares
            ReviveForLevel(enteredRunLevel && levelId != current);
            enteredRunLevel = true;

            if (director != null) director.Reset();
            DistrictLayout.ForgetShifts();
            FirstPersonController.WetFloor = theme.districts.wikiZones;
            if (theme.generator == LevelGenerator.Stairwell) arrival = Vector2Int.zero; // this level has one fixed entrance shaft
            if (theme.id == "9") arrival = Vector2Int.zero;   // everyone lands where the power line starts
            if (theme.id == "7") arrival = Vector2Int.zero;   // everyone arrives in the sideways room in the ceiling
            var caveStart = Vector3.zero;
            if (theme.id == "8")   // everyone comes up in the entrance pool's cave, or at the last 9th Road marker reached
            {
                caveStart = CaveLayout.Entrance(WorldHash.LevelSeed(save.worldSeed, "8"), Mathf.Max(0, CaveMarker()));
                arrival = ChunkStreamer.ChunkOf(caveStart);
            }
            streamer.Load(theme, catalog, WorldHash.LevelSeed(save.worldSeed, levelId), arrival);
            ClearLevelEvents();
            if (theme.generator != LevelGenerator.Stairwell && theme.id != "37")
            {
                environmentalEvents = gameObject.AddComponent<EnvironmentalEvents>();
                environmentalEvents.Init(player, streamer, director);
            }
            if (theme.id == "0.2") { collapse = gameObject.AddComponent<Collapse>(); collapse.Init(player, theme, catalog); }
            if (theme.id == "1")
            {
                flickering = gameObject.AddComponent<Flickering>();
                flickering.Init(streamer);
                warehouse = gameObject.AddComponent<Warehouse>();
                warehouse.Init(player, streamer);
            }
            player.HasSwimControl = theme.zoneWater || theme.id == "7" || theme.id == "8";   // Level 7: you can dive; Level 8: pools and floods
            player.WaterLevel = theme.waterHeight > 0f && !theme.zoneWater ? theme.waterHeight : float.NegativeInfinity;
            if (DistrictLayout.IsDataCenter(theme))
            {
                pool = gameObject.AddComponent<PoolDrain>();
                pool.Init(catalog.water, player, player.GetComponent<PlayerHud>());
            }
            if (DistrictLayout.IsDataCenter(theme) && arrival == Vector2Int.zero)   // falling out of Level 0: you drop into the pool
            {
                var area = DistrictLayout.PoolArea;
                player.Teleport(new Vector3(area.center.x, 4.5f, area.center.y), 0f);
            }
            else if (streamer.IsStatic)   // a hand-made level: you arrive in the middle of it
                player.Teleport(SafeSpot(new Vector3(0f, 0.05f, 0f)), 0f);
            else if (theme.generator == LevelGenerator.Stairwell && arrival == Vector2Int.zero)
                player.Teleport(new Vector3(16f, 0.05f, 13.4f), 0f);   // stand on the first tread, facing into the spiral
            else if (theme.id == "0" && arrival == Vector2Int.zero)   // the original rooms: stand where the first photo was taken
                player.Teleport(DistrictLayout.OriginSpawn, DistrictLayout.OriginYaw);
            else if (theme.id == "7")   // the sideways room; its door is a hole in the floor over the sea
                player.Teleport(OceanLayout.EntranceSpawn, 90f);
            else if (theme.id == "9")
                player.Teleport(SafeSpot(SuburbsLayout.Landing), 45f);
            else if (theme.id == "8")
                player.Teleport(caveStart, 0f);
            else if (save.shrineLevel == levelId && save.levelId == levelId)   // back to the Sanctum you last rested in
                player.Teleport(SafeSpot(new Vector3(save.shrineX, save.shrineY + 0.05f, save.shrineZ)), 0f);
            else
                player.Teleport(SafeSpot(new Vector3(arrival.x * MazeLayout.ChunkSize + 1f, 0.05f, arrival.y * MazeLayout.ChunkSize + 1f)), 45f);
            if (save.levelId != levelId) save.shrineLevel = null;   // a Sanctum only holds you in its own level

            if (theme.id == "3") { level3 = gameObject.AddComponent<Level3Events>(); level3.Init(player, streamer); }
            if (theme.id == "6") { level6 = gameObject.AddComponent<Level6Events>(); level6.Init(player); }
            if (theme.id == "7") { level7 = gameObject.AddComponent<Level7Events>(); level7.Init(player, save.goals.Contains("7:gear"), () => { if (!save.goals.Contains("7:gear")) save.goals.Add("7:gear"); Persist(); }); }
            if (theme.id == "9") { level9 = gameObject.AddComponent<Level9Events>(); level9.Init(player); }
            if (theme.id == "8")
            {
                level8 = gameObject.AddComponent<Level8Events>();
                level8.Init(player, WorldHash.LevelSeed(save.worldSeed, "8"), CaveMarker(), i => { save.goals.RemoveAll(g => g.StartsWith("8:marker:")); save.goals.Add("8:marker:" + i); Persist(); });
            }
            if (streamer.IsStatic && theme.id == "fun") { party = gameObject.AddComponent<PartyLevel>(); party.Init(player, streamer, save.worldSeed); }
            if (!streamer.IsStatic) { partyScenes = gameObject.AddComponent<PartyScenes>(); partyScenes.Init(streamer, director, theme.id); }
            if (theme.id == "0" || theme.id == "1" || theme.id == "2") { lure = gameObject.AddComponent<PartyLure>(); lure.Init(player, streamer); }
            if (streamer.IsStatic && theme.id == "188") { courtyard = gameObject.AddComponent<Courtyard>(); courtyard.Init(player, streamer, save.worldSeed, current != null && current != "188" ? current : "0"); }
            if (theme.id == "0" || theme.id == "1" || theme.id == "2") { windowLure = gameObject.AddComponent<WindowLure>(); windowLure.Init(player, streamer); }
            objectives = gameObject.AddComponent<Objectives>();
            objectives.Init(streamer, player, hud, save, levelId, arrival);
            if (theme.id == "37")
            {
                poolAmbience = gameObject.AddComponent<PoolAmbience>();
                poolAmbience.Init(player, streamer, catalog.water, catalog.waterDeep, catalog.waterCaustics, catalog.waterCausticsDeep);
            }
            Creatures.Blood.WipeLens();
            if (levelId != current) Creatures.Blood.Clear();   // waking up on the same level, your blood is still there
            if (levelId != current) tracker.Arrive(levelId, current ?? "");
            current = levelId;
            save.levelId = levelId;
            save.arrivalX = arrival.x;
            save.arrivalZ = arrival.y;
            if (!save.visited.Contains(levelId)) save.visited.Add(levelId);
            Persist();   // checkpoint: entering a level
            Debug.Log($"Entered {theme.displayName}");
        }

        /// The furthest 9th Road marker reached in Level 8 (-1: none).
        int CaveMarker()
        {
            var g = save.goals.Find(x => x.StartsWith("8:marker:"));
            return g != null && int.TryParse(g.Substring("8:marker:".Length), out int i) ? i : -1;
        }

        /// The nearest spot to `wanted` where the player fits on open floor, clear of furniture, pits and exits.
        static Vector3 SafeSpot(Vector3 wanted)
        {
            Physics.SyncTransforms();   // the level was just built this frame
            for (int ring = 0; ring < 12; ring++)
                for (int i = 0; i < Mathf.Max(1, ring * 8); i++)
                {
                    float angle = i * Mathf.PI * 2f / Mathf.Max(1, ring * 8);
                    var p = wanted + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 0.7f);
                    if (Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.5f, 0.35f, ~0, QueryTriggerInteraction.Collide)) continue;
                    if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 1f)) continue;   // there must be floor
                    return p;
                }
            return wanted;
        }

        void OnExit(string target, Vector3 exitPosition)
        {
            if (busy) return;
            if (pool != null && !pool.Drained && exitPosition.y < -1f)   // the door at the bottom of the pool
            {
                player.GetComponent<PlayerHud>()?.Flash("The door won't open under all this water. Find the pump control room.");
                return;
            }
            if (catalog.Find(target) == null)
            {
                Debug.LogWarning($"Exit leads to unknown level '{target}'");
                return;
            }
            // A given exit always leads to the same spot, so routes can be shared between players.
            if (save.levelId == "0.2" && target == "0") Achievements.Unlock("escaped_0.2");
            var exitChunk = ChunkStreamer.ChunkOf(exitPosition);
            uint h = WorldHash.Hash(save.worldSeed ^ WorldHash.StringHash(save.levelId), exitChunk.x, exitChunk.y, (int)WorldHash.StringHash(target));
            var arrival = new Vector2Int((int)(h % 101) - 50, (int)(h / 101 % 101) - 50);
            if (Coop.IsGuest) { coop.RequestExit(target, exitPosition); return; }
            if (Coop.IsOwner) { GroupExit(target, arrival, exitPosition, exitBy ?? MyId); return; }
            GoTo(target, arrival);
        }


        /// Dropping to the floor below hurts a little: winded, and shaken. Water breaks the fall.
        void OnLanded(float drop)
        {
            MakeNoise(player.transform.position, Mathf.Clamp(4f + drop * 3f, 4f, 20f));   // a thump: things that hear come to look
            if (!Difficulty.SurvivalEnabled || drop < 3f || status == null || player.transform.position.y < player.WaterLevel + 0.1f) return;
            status.Stamina = Mathf.Min(status.Stamina, 0.15f);
            status.Sanity = Mathf.Max(0f, status.Sanity - Mathf.Clamp01(drop / DistrictLayout.FloorHeight) * 0.08f);
        }

        /// Falling much further than one floor: you noclip out of Level 0 into a level below it.
        void NoclipDown()
        {
            string target = catalog.Find("-1") != null ? "-1" : null;
            if (Coop.Active) { OnPit(); return; }   // co-op: falling out of the world is a death
            if (target != null) GoTo(target, Vector2Int.zero);
            else StartCoroutine(Transition(() => Enter(save.levelId, new Vector2Int(save.arrivalX, save.arrivalZ))));   // until the negative level exists
        }

        /// Caught by a hunter: its full kill plays first; only then static, a co-op body and the checkpoint.
        void OnCaught(PlayerAgent who)
        {
            if (who != null && who.IsLocal && who.Dying && who.TakeFatalAttack(out var attack) && GoreDeath.HasSequence(attack.Kind))
            {
                var victim = who;
                SyncDeathPose(attack);
                GoreDeath.Play(victim, attack, () => FinishCaught(victim));
                return;
            }
            FinishCaught(who);
        }

        void FinishCaught(PlayerAgent who)
        {
            if (who != null) { who.ReleaseKiller(); who.Dying = false; }
            if ((who == null || who.IsLocal) && status != null) { status.Health = 1f; status.Infection = 0f; status.BleedRate = 0f; status.SeveredArm = false; }   // you wake up whole at the checkpoint
            if (Coop.IsOwner)
            {
                if (who == null || who.IsLocal) DieInCoop();
                else { var id = ClientOf(who); coop.Send(DieMsg, id, w => { }); OnPlayerDied(id, who.DeathCause.HasValue ? (int)who.DeathCause.Value : -1); }
                return;
            }
            if (who != null && !who.IsLocal) return;
            if (Coop.IsGuest) { DieInCoop(); return; }
            Creatures.Blood.Burst(player.transform.position);   // in co-op the body left behind does this
            if (Difficulty.Hardcore) { EndHardcoreRun(); return; }
            if (!busy) StartCoroutine(Transition(() => Enter(save.levelId, new Vector2Int(save.arrivalX, save.arrivalZ))));
        }

        /// Falling into a pit: you wake up back at the checkpoint.
        void OnPit()
        {
            if (busy) return;
            if (Coop.IsGuest) { DieInCoop(); return; }
            if (save.levelId == "19" || save.levelId == "0.2")   // through the attic floorboards, or Level 0.2's caved-in floor: back to Level 0
            {
                var p = player.transform.position;
                if (!Coop.IsGuest) { GoTo("0", ChunkStreamer.ChunkOf(p) + new Vector2Int(3, 3)); return; }
            }
            if (Coop.IsOwner) { DieInCoop(); return; }
            if (Difficulty.Hardcore) { EndHardcoreRun(); return; }
            StartCoroutine(Transition(() => Enter(save.levelId, new Vector2Int(save.arrivalX, save.arrivalZ))));
        }

        void EndHardcoreRun()
        {
            hud?.Flash("This Hardcore run is over. Your save has been deleted.", 6f);
            if (save != null && !string.IsNullOrEmpty(save.slot)) SaveSystem.Delete(save.slot);
            StartCoroutine(Transition(ShowMenu));
        }

        IEnumerator Transition(Action swap)
        {
            busy = true;
            if (antiCheat != null) antiCheat.TrustPosition(99f);
            player.enabled = false;
            yield return fx.Play(swap);
            bool canControl = !InMenu && !menus.Paused && !(spectator != null && spectator.Watching);
            player.enabled = canControl;
            if (tools != null) tools.enabled = canControl;
            if (exploration != null) exploration.enabled = canControl;
            if (antiCheat != null) antiCheat.TrustPosition(2f);
            busy = false;
        }

        void Update()
        {
            if (InMenu) return;
            save.playSeconds += Time.deltaTime;
            UpdateGroup();
            UpdateDirectorSync();
            UpdateJoinRequest();
            if (!busy && player.enabled && player.FallDistance > DistrictLayout.FloorHeight * 1.6f)
            {
                if (save.levelId == "0") NoclipDown();   // only Level 0 is thin enough to fall out of
                else OnPit();
            }
            // Keep what the encounter layer has placed, so a crash doesn't reshuffle places you've seen.
            if (Time.time >= nextSave && !busy)
            {
                nextSave = Time.time + 120f;
                if (save != null) Persist();
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F4/F5 remain useful quick creature-spawn shortcuts; all other dev actions live in the F10 panel.
            var keys = Keyboard.current;
            if (keys == null || busy || save == null || Coop.IsGuest || !Debug.isDebugBuild) return;
            if (keys.f6Key.wasPressedThisFrame)
            {
                int i = catalog.levels.FindIndex(l => l.id == save.levelId);
                string next = catalog.levels[(i + 1) % catalog.levels.Count].id;
                GoTo(next, Vector2Int.zero);
            }
            if (keys.f7Key.wasPressedThisFrame)   // level browser: jump 8 districts (512 m) further out, deeper into copy drift
            {
                var here = ChunkStreamer.ChunkOf(player.transform.position) + new Vector2Int(16, 16);
                GoTo(save.levelId, here);
            }
            if (keys.f9Key.wasPressedThisFrame)
            {
                SaveSystem.Delete(save.slot);
                save = NewSave();
                StartCoroutine(Transition(Begin));
            }
#endif
        }
    }
}
