using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backrooms.Meta;
using Backrooms.Net;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// The co-op half of the session. The owner's run is the shared world: guests get it when they join, follow the
    /// owner from level to level, and never write it to their own saves (the world lives only in the owner's save).
    public sealed partial class GameSession
    {
        CoopSession coop;
        public CoopSession CoopNet => coop;

        // Lobby (host) pending config before start
        bool lobbyPending, lobbyFromNewGame;
        string lobbyNewName; int lobbyNewDiff; GameMode lobbyNewMode; SaveData lobbyExistingSave;

        public bool InLobby => Coop.Active && coop != null && coop.IsLobby;

        void InitCoop()
        {
            coop = gameObject.AddComponent<CoopSession>();
            coop.PlayerJoined += PlayLobbyJoinSound;
            coop.DescribeWorld = DescribeWorld;
            coop.WorldReceived += OnWorldReceived;
            coop.EnterLevel += OnOwnerEntered;
            coop.ExitRequested += (target, at, from) => { exitBy = from; OnExit(target, at); exitBy = null; };
            coop.Ended += OnCoopEnded;
            coop.HostStart += OnHostStartLobby;
            coop.StartRequested += _ => { /* guest will receive world/start; ensure UI reacts */ };
            WireWorldSync();
            WireGroup();
            WireDirector();
            WireOwnership();
        }

        ulong? exitBy;   // which player walked into the exit being handled (null: you)

        /// Opens this run to friends (pause menu). Returns the join code, or null.
        public async Task<string> HostCoop()
        {
            // Legacy path no longer used by UI (in-game open removed), kept for compatibility.
            if (InMenu || save == null) return null;
            Persist();
            var code = await coop.Host(save.name);
            if (code != null && hud != null) hud.Flash("Friends can join with code " + code);
            return code;
        }

        /// Joins a friend's world/lobby from the main menu or join flows.
        public async Task<bool> JoinCoop(string code)
        {
            if (busy) return false;
            bool ok = await coop.Join(code);
            if (!ok) return false;
            // If joined into a pre-start lobby, keep menu/UI in lobby state; world will come on start (or never for lobby-only).
            if (coop.IsLobby)
            {
                // stay in menu context; UI will show lobby room
                menus.CoopStatus("Joined lobby.");
            }
            return true;
        }

        /// Leaves (or, as the owner, closes) the co-op game/lobby.
        public async void LeaveCoop()
        {
            bool wasInLobby = coop != null && coop.IsLobby;
            bool wasInMenu = InMenu;
            bool returnToPublicBrowser = coop != null && coop.Visibility == LobbyVisibility.Public;
            lobbyPending = false;
            ExitLobbyView();
            if (coop != null) coop.GiveUp();
            if (coop != null) await coop.Leave();

            // Leave Game should return both hosts and guests to a menu, not leave a host running solo.
            // Public sessions return directly to the browser so the player can choose another lobby.
            if (!wasInMenu || wasInLobby)
            {
                StartCoroutine(Transition(() =>
                {
                    ShowMenu();
                    if (returnToPublicBrowser) menus.ReturnToPublicLobbyBrowser();
                }));
            }
            else menus.ShowMain();
        }

        public void PrepareLobbyHost(bool fromNewGame, string worldName, int difficulty, GameMode mode, SaveData existingSave)
        {
            lobbyPending = true;
            lobbyFromNewGame = fromNewGame;
            lobbyNewName = worldName;
            lobbyNewDiff = difficulty;
            lobbyNewMode = mode;
            lobbyExistingSave = existingSave;
        }

        public void StartLobbyGame()
        {
            if (coop == null || !Coop.IsOwner || !coop.IsLobby) return;
            if (!coop.AllReady()) { menus?.CoopStatus("Everyone must be ready before starting."); return; }
            ExitLobbyView();
            if (lobbyFromNewGame) BeginNewGameAsLobbyHost();
            else BeginExistingSaveAsLobbyHost();
        }

        void OnHostStartLobby()
        {
            // Host clicked start; actual begin happens from UI/start path to ensure config known.
            if (Coop.IsOwner && lobbyPending)
            {
                // UI should call StartLobbyGame; fallback no-op
            }
        }

        void BeginNewGameAsLobbyHost()
        {
            if (!lobbyPending) return;
            lobbyPending = false;
            StartCoroutine(Transition(() =>
            {
                if (backdrop != null) Destroy(backdrop);
                backdrop = null;
                save = NewSave(lobbyNewName, lobbyNewDiff, lobbyNewMode);
                enteredRunLevel = false;
                if (catalog.Find(save.levelId) == null) save.levelId = catalog.levels[0].id;
                InMenu = false;
                menus.Hide();
                director.enabled = true;
                tracker.enabled = true;
                tracker.StartSession();
                SetPlayerActive(true);
                Begin();
                // mark started and send world to everyone waiting in lobby
                coop.ClearReady();
                coop.BroadcastStartAndWorld(save.levelId, lobbyNewMode.ToString(), Difficulty.Names[Mathf.Clamp(save.difficulty, 0, Difficulty.Names.Length - 1)]);
            }));
        }

        void BeginExistingSaveAsLobbyHost()
        {
            if (!lobbyPending || lobbyExistingSave == null) return;
            var slot = lobbyExistingSave;
            lobbyPending = false;
            StartCoroutine(Transition(() =>
            {
                if (backdrop != null) Destroy(backdrop);
                backdrop = null;
                save = SaveSystem.Load(slot.slot) ?? slot;
                if (save == null) return;
                enteredRunLevel = false;
                if (catalog.Find(save.levelId) == null) save.levelId = catalog.levels[0].id;
                InMenu = false;
                menus.Hide();
                director.enabled = true;
                tracker.enabled = true;
                tracker.StartSession();
                SetPlayerActive(true);
                Begin();
                coop.ClearReady();
                coop.BroadcastStartAndWorld(save.levelId, ((GameMode)Mathf.Clamp(save.mode,0,5)).ToString(), Difficulty.Names[Mathf.Clamp(save.difficulty, 0, Difficulty.Names.Length - 1)]);
            }));
        }

        WorldInfo DescribeWorld()
        {
            var me = player.transform;
            return new WorldInfo
            {
                Seed = save.worldSeed,
                Difficulty = save.difficulty,
                Mode = save.mode,
                CustomThirst = save.customThirst,
                CustomBattery = save.customBattery,
                CustomSanity = save.customSanity,
                CustomSupplies = save.customSupplies,
                HardcoreDead = save.hardcoreDead?.ToArray() ?? System.Array.Empty<string>(),
                HardcoreWait = save.hardcoreWait?.ToArray() ?? System.Array.Empty<int>(),
                Level = save.levelId,
                Arrival = new Vector2Int(save.arrivalX, save.arrivalZ),
                CameFrom = current,
                OwnerAt = me != null ? me.position : Vector3.zero,
                OwnerYaw = me != null ? me.eulerAngles.y : 0f,
                Picked = save.picked?.ToArray() ?? new int[0],
                Goals = save.goals?.ToArray() ?? new string[0],
                PoolPulled = PoolSwitch.Pulled,
            };
        }

        /// A guest: the owner's world has arrived. Stand in it, next to the owner.
        void OnWorldReceived(WorldInfo w)
        {
            if (busy) return;
            if (ResumeAfterReconnect(w)) return;
            ExitLobbyView();
            StartCoroutine(Transition(() =>
            {
                enteredRunLevel = false;
                if (backdrop != null) Destroy(backdrop);
                backdrop = null;
                save = new SaveData
                {
                    slot = null,   // a guest's copy is never written
                    name = "Co-op",
                    difficulty = w.Difficulty,
                    mode = w.Mode,
                    hardcoreDead = new List<string>(w.HardcoreDead ?? System.Array.Empty<string>()),
                    hardcoreWait = new List<int>(w.HardcoreWait ?? System.Array.Empty<int>()),
                    customThirst = w.CustomThirst, customBattery = w.CustomBattery, customSanity = w.CustomSanity, customSupplies = w.CustomSupplies,
                    worldSeed = w.Seed,
                    levelId = catalog.Find(w.Level) != null ? w.Level : catalog.levels[0].id,
                    arrivalX = w.Arrival.x,
                    arrivalZ = w.Arrival.y,
                    almondWater = Difficulty.StartWater,
                    batteries = Difficulty.StartBatteries,
                    picked = new List<int>(w.Picked ?? new int[0]),
                    goals = new List<string>(w.Goals ?? new string[0]),
                };
                Difficulty.Level = save.difficulty;
                Difficulty.Mode = (GameMode)Mathf.Clamp(save.mode, 0, System.Enum.GetValues(typeof(GameMode)).Length - 1);
                Difficulty.SetCustom(save.customThirst, save.customBattery, save.customSanity, save.customSupplies);
                SaveSystem.LoadPersonalExploration(save);
                InMenu = false;
                menus.Hide();
                tracker.enabled = true;
                tracker.StartSession();
                SetPlayerActive(true);
                current = string.IsNullOrEmpty(w.CameFrom) ? null : w.CameFrom;
                Begin();
                PoolSwitch.Pulled = w.PoolPulled;
                player.Teleport(SafeSpot(w.OwnerAt + Quaternion.Euler(0f, w.OwnerYaw, 0f) * new Vector3(0.9f, 0f, 0.6f)), w.OwnerYaw);
                hud?.Flash("You're in. Stay close.");
            }));
        }

        /// A guest: the owner went through an exit; everyone follows.
        void OnOwnerEntered(string level, Vector2Int arrival, string cameFrom)
        {
            if (catalog.Find(level) == null) return;
            StartCoroutine(Transition(() =>
            {
                current = string.IsNullOrEmpty(cameFrom) ? current : cameFrom;
                Enter(level, arrival);
            }));
        }

        void OnCoopEnded(string why)
        {
            lobbyPending = false;
            if (InMenu)
            {
                ExitLobbyView();
                ShowMenu();
                menus.CoopStatus(why);
                return;
            }
            if (save != null && save.slot == null)   // a guest: the shared world is gone
            {
                StartCoroutine(Transition(() => { ShowMenu(); menus.CoopStatus(why); }));
                return;
            }
            hud?.Flash(why);   // the owner keeps playing alone
        }

        /// Moves to a level. The owner takes everyone along; a guest asks the owner (who decides).
        void GoTo(string level, Vector2Int arrival)
        {
            if (Coop.IsGuest) return;
            if (Coop.IsOwner) coop.BroadcastEnter(level, arrival, save.levelId);
            StartCoroutine(Transition(() => Enter(level, arrival)));
        }
    }
}

