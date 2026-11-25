using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Backrooms.Net
{
    /// What a joining player needs to stand in the same world as the owner.
    public struct WorldInfo
    {
        public uint Seed;
        public int Difficulty, Mode;
        public float CustomThirst, CustomBattery, CustomSanity, CustomSupplies;
        public string[] HardcoreDead;
        public int[] HardcoreWait;
        public string Level, CameFrom;
        public Vector2Int Arrival;
        public Vector3 OwnerAt;
        public float OwnerYaw;
        public int[] Picked;
        public string[] Goals;
        public bool PoolPulled;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Seed);
            w.WriteValueSafe(Difficulty);
            w.WriteValueSafe(Mode);
            w.WriteValueSafe(CustomThirst);
            w.WriteValueSafe(CustomBattery);
            w.WriteValueSafe(CustomSanity);
            w.WriteValueSafe(CustomSupplies);
            Coop.WriteStrings(w, HardcoreDead);
            w.WriteValueSafe(HardcoreWait ?? Array.Empty<int>());
            w.WriteValueSafe(Level ?? "");
            w.WriteValueSafe(CameFrom ?? "");
            w.WriteValueSafe(Arrival.x);
            w.WriteValueSafe(Arrival.y);
            w.WriteValueSafe(OwnerAt);
            w.WriteValueSafe(OwnerYaw);
            w.WriteValueSafe(Picked ?? Array.Empty<int>());
            Coop.WriteStrings(w, Goals);
            w.WriteValueSafe(PoolPulled);
        }

        public static WorldInfo Read(FastBufferReader r)
        {
            var i = new WorldInfo();
            r.ReadValueSafe(out i.Seed);
            r.ReadValueSafe(out i.Difficulty);
            r.ReadValueSafe(out i.Mode);
            r.ReadValueSafe(out i.CustomThirst);
            r.ReadValueSafe(out i.CustomBattery);
            r.ReadValueSafe(out i.CustomSanity);
            r.ReadValueSafe(out i.CustomSupplies);
            i.HardcoreDead = Coop.ReadStrings(r);
            r.ReadValueSafe(out i.HardcoreWait);
            r.ReadValueSafe(out i.Level);
            r.ReadValueSafe(out i.CameFrom);
            r.ReadValueSafe(out int ax);
            r.ReadValueSafe(out int az);
            i.Arrival = new Vector2Int(ax, az);
            r.ReadValueSafe(out i.OwnerAt);
            r.ReadValueSafe(out i.OwnerYaw);
            r.ReadValueSafe(out i.Picked);
            i.Goals = Coop.ReadStrings(r);
            r.ReadValueSafe(out i.PoolPulled);
            return i;
        }
    }

    /// Who is in charge right now. Single-player: you, offline. Co-op: the owner (host) runs the shared world â€”
    /// creatures, the fear director, level events, anything random â€” and the others follow what it decides.
    public static class Coop
    {
        public static bool Active => Session != null && Session.Running;
        /// Hosting a co-op game (and so in charge).
        public static bool IsOwner => Active && Session.Hosting;
        /// A guest in someone else's world.
        public static bool IsGuest => Active && !Session.Hosting;
        /// True when this game decides shared things: single-player, or the owner in co-op.
        public static bool Authority => !IsGuest;
        public static CoopSession Session { get; internal set; }

        /// The name shown in the owner's player list (never in the world).
        public static string PlayerName
        {
            get => PlayerPrefs.GetString("coop_name", "Wanderer");
            set { PlayerPrefs.SetString("coop_name", string.IsNullOrWhiteSpace(value) ? "Wanderer" : value.Trim()); PlayerPrefs.Save(); }
        }

        internal static void WriteStrings(FastBufferWriter w, string[] items)
        {
            items ??= Array.Empty<string>();
            w.WriteValueSafe(items.Length);
            foreach (var s in items) w.WriteValueSafe(s ?? "");
        }

        internal static string[] ReadStrings(FastBufferReader r)
        {
            r.ReadValueSafe(out int n);
            var items = new string[Mathf.Clamp(n, 0, 4096)];
            for (int i = 0; i < items.Length; i++) r.ReadValueSafe(out items[i]);
            return items;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Session = null;
    }

    public enum LobbyVisibility { Public, CodeOnly }
    public enum SessionPhase { None, Lobby, InProgress }

    public sealed class LobbyListing
    {
        public string Id, Name, HostName, Description, Level, LevelName, Mode, Difficulty, Phase, JoinCode;
        public int PlayerCount, MaxPlayers;
        public bool RequiresCode, InProgress;
        public ISessionInfo Info;
    }

    /// Hosting and joining co-op games: Unity sign-in (anonymous), a session with a join code over Relay (no port
    /// forwarding), Netcode for GameObjects for players and messages. Lives next to the GameSession.
    /// Nobody is ever dropped except by the owner (kick or ban): a lost connection retries until it's back, and
    /// the guest's body waits where it stood. When the owner leaves, everyone goes back to the menu — unless the
    /// owner chose to keep the game going, in which case the world is handed to another player and everyone
    /// follows them automatically.
    public sealed class CoopSession : MonoBehaviour
    {
        public const int MinPlayers = 2, MaxPlayersCap = 4;
        public const int MaxPlayers = MaxPlayersCap; // legacy
        const string AvatarPrefab = "CoopAvatar", CreaturePrefab = "CoopCreature";
        const string PPhase = "phase", PLevel = "level", PLevelName = "lvlname", PMode = "mode", PDifficulty = "diff", PHost = "host", PDesc = "desc", PReqCode = "reqcode", PVis = "vis";

        public ISession Session { get; private set; }
        public string Code => Session?.Code;
        public bool Hosting { get; private set; }
        public bool Running => Net != null && Net.IsListening;
        public bool Busy { get; private set; }
        public bool Reconnecting { get; private set; }
        public NetworkManager Net { get; private set; }
        public static string MyPlayerId => AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";

        public SessionPhase Phase { get; private set; } = SessionPhase.None;
        public bool IsLobby => Phase == SessionPhase.Lobby;
        public bool IsStarted => Phase == SessionPhase.InProgress;
        public LobbyVisibility Visibility { get; private set; } = LobbyVisibility.Public;
        public string LobbyName { get; private set; } = "";
        public string LobbyDescription { get; private set; } = "";
        public int ConfiguredMaxPlayers { get; private set; } = MaxPlayersCap;
        public bool SessionRequiresCode => Visibility == LobbyVisibility.CodeOnly;
        readonly HashSet<ulong> ready = new HashSet<ulong>();
        public IReadOnlyCollection<ulong> ReadyClients => ready;
        public event Action LobbyChanged;
        public event Action PlayerJoined;
        public event Action<string> StartRequested; // guests: host started (payload optional)
        public event Action HostStart; // owner local trigger to begin world

        static string Prop(ISessionInfo s, string key) => s != null && s.Properties != null && s.Properties.TryGetValue(key, out var p) ? p.Value : null;
        static string Prop(ISession s, string key) => s != null && s.Properties != null && s.Properties.TryGetValue(key, out var p) ? p.Value : null;

        public bool IsReady(ulong client) => ready.Contains(client);
        public void SetReadyLocal(bool on)
        {
            if (Net == null || !Net.IsListening) return;
            if (on) ready.Add(Net.LocalClientId); else ready.Remove(Net.LocalClientId);
            if (Hosting) { LobbyChanged?.Invoke(); BroadcastLobbyState(); }
            else Send(Msg.Ready, NetworkManager.ServerClientId, w => w.WriteValueSafe(on));
        }
        public bool AllReady()
        {
            if (Net == null) return false;
            foreach (var id in Net.ConnectedClientsIds) if (!ready.Contains(id)) return false;
            return Net.ConnectedClientsIds.Count >= 1;
        }

        public void ClearReady() { ready.Clear(); }


        /// Short status lines for the menus ("Connecting...", "Couldn't join: ...").
        public event Action<string> Status;
        /// The owner sent the world (guest only).
        public event Action<WorldInfo> WorldReceived;
        /// The owner moved everyone to a level (guest only): level id, arrival chunk, where they came from.
        public event Action<string, Vector2Int, string> EnterLevel;
        /// A guest walked into an exit (owner only): target level, exit position, who.
        public event Action<string, Vector3, ulong> ExitRequested;
        /// The co-op game ended for us (owner left, we left, or we were removed); the text says why.
        public event Action<string> Ended;
        /// Someone asks to join and the owner approves joins by hand: their name, and the answer to give.
        public event Action<string, Action<bool>> JoinAsked;
        /// You're the new owner: the old owner's save (JSON) to keep the world in.
        public event Action<string> BecameOwner;
        /// A guest's game needs the world (owner only): build it from the current run.
        public Func<WorldInfo> DescribeWorld;
        /// The owner's save as JSON (for handing the world to the next owner).
        public Func<string> SaveJson;

        /// Who is in the game (owner only): client id â†’ (player id, name).
        readonly Dictionary<ulong, (string id, string name)> people = new Dictionary<ulong, (string, string)>();
        public IReadOnlyDictionary<ulong, (string id, string name)> People => people;
        readonly Dictionary<ulong, (string id, string name)> pendingPeople = new Dictionary<ulong, (string, string)>();
        bool leaving, handlersRegistered, shuttingDown;
        string lastCode, lastSessionId, kickedFor, followName;

        void Awake()
        {
            Coop.Session = this;
            LevelSync.Hook(this);
            Player.PlayerAgent.RemoteSanityLoss -= SendSanity;
            Player.PlayerAgent.RemoteSanityLoss += SendSanity;
            Player.PlayerAgent.RemoteHurt -= SendHurt;
            Player.PlayerAgent.RemoteHurt += SendHurt;
            Player.PlayerAgent.RemoteInfect -= SendInfect;
            Player.PlayerAgent.RemoteInfect += SendInfect;
        }

        void OnDestroy()
        {
            if (Coop.Session == this) Coop.Session = null;
            Player.PlayerAgent.RemoteSanityLoss -= SendSanity;
            Player.PlayerAgent.RemoteHurt -= SendHurt;
            Player.PlayerAgent.RemoteInfect -= SendInfect;
        }

        // ---------- sign-in ----------

        async Task<bool> SignIn()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    string profile = Profile();
                    if (profile != null) options.SetProfile(profile);
                    await UnityServices.InitializeAsync(options);
                }
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception e)
            {
                Say("Couldn't reach the co-op service: " + Short(e));
                Debug.LogException(e);
                return false;
            }
        }

        /// Each test copy of the game on one machine needs its own identity: Multiplayer Play Mode clones run from
        /// their own folder, and a build can be given one with -coopProfile NAME.
        static string Profile()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-coopProfile") return Clean(args[i + 1]);
#if UNITY_EDITOR
            // Avoid a hard dependency on Multiplayer Play Mode packages; pass -coopProfile for parallel editor instances when needed.
            string mpm = Environment.GetEnvironmentVariable("MPM_TAG");
            if (!string.IsNullOrEmpty(mpm)) return Clean("clone_" + mpm);
#endif
            return null;
        }

        static string Clean(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
            return sb.Length == 0 ? null : sb.ToString(0, Mathf.Min(30, sb.Length));
        }

        // ---------- host and join ----------

        /// Opens the current run to friends. Returns the join code, or null. `publicName`: a hand-over's
        /// findable name (the others search for it), otherwise the game is private (code only).
        /// Legacy in-run hosting path (kept for compatibility with handover); new UI uses lobbies.
        public async Task<string> Host(string worldName, string publicName = null)
        {
            if (Busy || Running) return Code;
            Busy = true;
            try
            {
                Say("Opening your world to friends...");
                if (!await SignIn()) return null;
                MakeNetworkManager();
                Hosting = true;
                ConfiguredMaxPlayers = MaxPlayersCap;
                Visibility = LobbyVisibility.Public;
                LobbyName = publicName ?? Trim(worldName, 30);
                LobbyDescription = "";
                Phase = SessionPhase.InProgress;
                var options = new SessionOptions { MaxPlayers = ConfiguredMaxPlayers, IsPrivate = publicName == null, Name = LobbyName }.WithRelayNetwork();
                options.SessionProperties = BuildSessionProperties("In progress", "", worldName, "", "");
                Session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Remember(Session);
                Say("Friends can join with code " + Session.Code);
                await RefreshSessionProperties();
                return Session.Code;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Say("Couldn't open the world: " + Short(e));
                await Shutdown();
                return null;
            }
            finally { Busy = false; }
        }


        Dictionary<string, SessionProperty> BuildSessionProperties(string phase, string levelName, string mode, string difficulty, string description)
        {
            var props = new Dictionary<string, SessionProperty>
            {
                [PPhase] = new SessionProperty(phase ?? "", VisibilityPropertyOptions.Public),
                [PLevelName] = new SessionProperty(levelName ?? "", VisibilityPropertyOptions.Public),
                [PMode] = new SessionProperty(mode ?? "", VisibilityPropertyOptions.Public),
                [PDifficulty] = new SessionProperty(difficulty ?? "", VisibilityPropertyOptions.Public),
                [PHost] = new SessionProperty(Coop.PlayerName ?? "Host", VisibilityPropertyOptions.Public),
                [PDesc] = new SessionProperty(Trim(description ?? "", 80), VisibilityPropertyOptions.Public),
                [PReqCode] = new SessionProperty(SessionRequiresCode ? "1" : "0", VisibilityPropertyOptions.Public),
                [PVis] = new SessionProperty(Visibility.ToString(), VisibilityPropertyOptions.Public),
            };
            return props;
        }

        public async Task<string> CreateLobby(string lobbyName, string description, LobbyVisibility visibility, int maxPlayers, string modeSummary, string difficultySummary)
        {
            if (Busy || Running) return null;
            Busy = true;
            try
            {
                Say("Creating lobby...");
                if (!await SignIn()) return null;
                MakeNetworkManager();
                Hosting = true;
                Visibility = visibility;
                LobbyName = Trim(string.IsNullOrWhiteSpace(lobbyName) ? Coop.PlayerName + "'s Lobby" : lobbyName, 30);
                LobbyDescription = Trim(description ?? "", 80);
                ConfiguredMaxPlayers = Mathf.Clamp(maxPlayers, MinPlayers, MaxPlayersCap);
                Phase = SessionPhase.Lobby;
                ready.Clear();
                var options = new SessionOptions { MaxPlayers = ConfiguredMaxPlayers, IsPrivate = false, Name = LobbyName }.WithRelayNetwork();
                options.SessionProperties = BuildSessionProperties("In lobby", "", modeSummary ?? "", difficultySummary ?? "", LobbyDescription);
                Session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Remember(Session);
                Say("Lobby created. Code " + Session.Code);
                return Session.Code;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Say("Couldn't create lobby: " + Short(e));
                await Shutdown();
                return null;
            }
            finally { Busy = false; }
        }

        public async Task<IList<LobbyListing>> QueryPublicLobbies()
        {
            if (!await SignIn()) return Array.Empty<LobbyListing>();
            try
            {
                var opts = new QuerySessionsOptions { Count = 100 };
                var res = await MultiplayerService.Instance.QuerySessionsAsync(opts);
                var list = new List<LobbyListing>();
                if (res?.Sessions == null) return list;
                foreach (var s in res.Sessions)
                {
                    if (s == null) continue;
                    bool req = Prop(s, PReqCode) == "1";
                    string phase = Prop(s, PPhase);
                    bool inProgress = string.Equals(phase, "In progress", StringComparison.OrdinalIgnoreCase) || string.Equals(phase, "InProgress", StringComparison.OrdinalIgnoreCase);
                    list.Add(new LobbyListing
                    {
                        Id = s.Id,
                        Name = s.Name,
                        HostName = Prop(s, PHost) ?? "",
                        Description = Prop(s, PDesc) ?? "",
                        LevelName = Prop(s, PLevelName) ?? "",
                        Mode = Prop(s, PMode) ?? "",
                        Difficulty = Prop(s, PDifficulty) ?? "",
                        Phase = string.IsNullOrEmpty(phase) ? (inProgress ? "In progress" : "In lobby") : phase,
                        InProgress = inProgress,
                        RequiresCode = req,
                        PlayerCount = s.MaxPlayers - s.AvailableSlots,
                        MaxPlayers = s.MaxPlayers,
                        Info = s,
                    });
                }
                return list;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Say("Couldn't list games: " + Short(e));
                return Array.Empty<LobbyListing>();
            }
        }

        public async Task<bool> JoinById(string sessionId, string providedCode = null)
        {
            if (Busy || Running) return false;
            Busy = true;
            try
            {
                Say("Joining...");
                if (!await SignIn()) return false;
                MakeNetworkManager();
                Hosting = false;
                kickedFor = null;
                try
                {
                    // If a code is required/provided, try by code path if user supplied it; otherwise join by id.
                    if (!string.IsNullOrWhiteSpace(providedCode))
                        Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(providedCode.Trim().ToUpperInvariant());
                    else
                        Session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
                }
                catch (SessionException e) when (e.Message.ToLowerInvariant().Contains("already a member"))
                {
                    Session = null;
                    foreach (var id in await MultiplayerService.Instance.GetJoinedSessionIdsAsync())
                    {
                        try { Session = await MultiplayerService.Instance.ReconnectToSessionAsync(id); break; }
                        catch (SessionException) { }
                    }
                    if (Session == null) throw;
                }
                Remember(Session);
                Visibility = Prop(Session, PReqCode) == "1" ? LobbyVisibility.CodeOnly : LobbyVisibility.Public;
                LobbyName = Session.Name ?? "";
                LobbyDescription = Prop(Session, PDesc) ?? "";
                ConfiguredMaxPlayers = Session.MaxPlayers;
                Phase = string.Equals(Prop(Session, PPhase), "In progress", StringComparison.OrdinalIgnoreCase) ? SessionPhase.InProgress : SessionPhase.Lobby;
                Say(Phase == SessionPhase.Lobby ? "Joined lobby. Waiting..." : "Connected. Waiting for the world...");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                string why = Net != null && !string.IsNullOrEmpty(Net.DisconnectReason) ? Net.DisconnectReason : "Couldn't join: " + Short(e);
                Say(why);
                await Shutdown();
                return false;
            }
            finally { Busy = false; }
        }

        public async Task RefreshSessionProperties(string levelName = null, string mode = null, string difficulty = null)
        {
            if (Session == null || !Hosting) return;
            try
            {
                var host = Session.AsHost();
                host.Name = LobbyName;
                host.IsPrivate = false; // keep queryable; use property for code-only listing behavior
                var props = BuildSessionProperties(Phase == SessionPhase.Lobby ? "In lobby" : "In progress",
                    levelName ?? (Phase == SessionPhase.Lobby ? "" : Prop(Session, PLevelName)),
                    mode ?? Prop(Session, PMode),
                    difficulty ?? Prop(Session, PDifficulty),
                    LobbyDescription);
                foreach (var kv in props) host.SetProperty(kv.Key, kv.Value);
                await host.SavePropertiesAsync();
            }
            catch (Exception e) { Debug.LogWarning("[Co-op] Couldn't update session: " + e.Message); }
        }

        public async Task<bool> UpdateLobbySettings(string name, string description, LobbyVisibility visibility, int maxPlayers, bool approveJoins)
        {
            if (!Hosting || Session == null) return false;
            LobbyName = Trim(string.IsNullOrWhiteSpace(name) ? LobbyName : name, 30);
            LobbyDescription = Trim(description ?? "", 80);
            Visibility = visibility;
            ConfiguredMaxPlayers = Mathf.Clamp(maxPlayers, MinPlayers, MaxPlayersCap);
            CoopBans.ApproveJoins = approveJoins;
            // If lowered below current count, kick excess (non-host) players.
            if (Net != null && Net.IsServer)
            {
                var clients = new List<ulong>(Net.ConnectedClientsIds);
                // keep host, kick others from end as needed
                for (int i = clients.Count - 1; i >= 0 && clients.Count > ConfiguredMaxPlayers; i--)
                {
                    var id = clients[i];
                    if (id == Net.LocalClientId) continue;
                    Kick(id, "The lobby is now full.");
                    clients.RemoveAt(i);
                }
            }
            await RefreshSessionProperties();
            BroadcastLobbyState();
            LobbyChanged?.Invoke();
            return true;
        }

        void BroadcastLobbyState()
        {
            if (!Hosting || Net == null) return;
            // send a simple per-client ready update for everyone currently known; UI can refresh on LobbyChanged and poll IsReady/People
            foreach (var id in Net.ConnectedClientsIds)
                SendAll(Msg.Lobby, w => { w.WriteValueSafe(id); w.WriteValueSafe(ready.Contains(id)); w.WriteValueSafe((byte)1); });
        }

        void OnReady(ulong from, FastBufferReader r)
        {
            if (!Net.IsServer) return;
            r.ReadValueSafe(out bool on);
            if (on) ready.Add(from); else ready.Remove(from);
            LobbyChanged?.Invoke();
            SendAll(Msg.Lobby, w => { w.WriteValueSafe(from); w.WriteValueSafe(on); w.WriteValueSafe((byte)1); });
        }

        void OnLobbyState(ulong from, FastBufferReader r)
        {
            if (Net.IsServer) return;
            try
            {
                r.ReadValueSafe(out ulong client);
                r.ReadValueSafe(out bool on);
                r.ReadValueSafe(out byte _);
                if (on) ready.Add(client); else ready.Remove(client);
                LobbyChanged?.Invoke();
            }
            catch { }
        }


        void OnPlayerJoinedMessage(ulong from, FastBufferReader r)
        {
            if (Net.IsServer || from != NetworkManager.ServerClientId) return;
            PlayerJoined?.Invoke();
        }

        void OnStartMsg(ulong from, FastBufferReader r)
        {
            if (Net.IsServer) return;
            StartRequested?.Invoke(null);
        }

        void OnLobbyCfg(ulong from, FastBufferReader r) { if (Net.IsServer) return; LobbyChanged?.Invoke(); }

        void OnSuit(ulong from, FastBufferReader r)
        {
            if (!Net.IsServer) return;
            r.ReadValueSafe(out int suit);
            if (CoopAvatar.All.TryGetValue(from, out var a) && a != null) a.SetSuitServer(suit);
            LobbyChanged?.Invoke();
        }

        public void RequestSuit(int suit)
        {
            if (Net == null || !Net.IsListening) return;
            suit = Mathf.Clamp(suit, 0, CoopAvatar.SuitNames.Length - 1);
            if (Hosting) { if (CoopAvatar.All.TryGetValue(Net.LocalClientId, out var mine) && mine != null) mine.SetSuitServer(suit); }
            else Send(Msg.Suit, NetworkManager.ServerClientId, w => w.WriteValueSafe(suit));
        }


        public bool TryStartLobby()
        {

            if (!Hosting || Phase != SessionPhase.Lobby) return false;
            if (!AllReady()) { Say("Everyone must be ready before starting."); return false; }
            // Host/game layer is responsible for creating/loading the run, then calling BroadcastStartAndWorld.
            HostStart?.Invoke();
            return true;
        }

        public bool AllConnectedReady()
        {
            if (Net == null) return false;
            int n = 0;
            foreach (var id in Net.ConnectedClientsIds) { n++; if (!ready.Contains(id)) return false; }
            return n >= 1;
        }



        /// Joins a friend's world by its code. The owner then sends the world (WorldReceived).
        public async Task<bool> Join(string code)
        {
            if (Busy || Running) return false;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) { Say("Type the code for the lobby."); return false; }
            Busy = true;
            try
            {
                Say("Joining " + code + "...");
                if (!await SignIn()) return false;
                MakeNetworkManager();
                Hosting = false;
                kickedFor = null;
                try { Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code); }
                catch (SessionException e) when (e.Message.ToLowerInvariant().Contains("already a member"))
                {
                    // Our game closed or crashed while we were in: we're still on the list, so step back in.
                    Session = null;
                    foreach (var id in await MultiplayerService.Instance.GetJoinedSessionIdsAsync())
                    {
                        try { Session = await MultiplayerService.Instance.ReconnectToSessionAsync(id); break; }
                        catch (SessionException) { }
                    }
                    if (Session == null) throw;
                }
                Remember(Session);
                Visibility = Prop(Session, PReqCode) == "1" ? LobbyVisibility.CodeOnly : LobbyVisibility.Public;
                LobbyName = Session.Name ?? "";
                LobbyDescription = Prop(Session, PDesc) ?? "";
                ConfiguredMaxPlayers = Session.MaxPlayers;
                Phase = string.Equals(Prop(Session, PPhase), "In progress", StringComparison.OrdinalIgnoreCase) ? SessionPhase.InProgress : SessionPhase.Lobby;
                Say(Phase == SessionPhase.Lobby ? "Joined lobby. Waiting..." : "Connected. Waiting for the world...");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // The owner's own words if they turned us away (banned, full, not let in).
                string why = Net != null && !string.IsNullOrEmpty(Net.DisconnectReason) ? Net.DisconnectReason : "Couldn't join: " + Short(e);
                Say(why);
                await Shutdown();
                return false;
            }
            finally { Busy = false; }
        }


        void Remember(ISession s)
        {
            lastCode = s.Code;
            lastSessionId = s.Id;
            s.RemovedFromSession += OnRemoved;
            s.Deleted += OnDeleted;
        }

        void Forget(ISession s)
        {
            s.RemovedFromSession -= OnRemoved;
            s.Deleted -= OnDeleted;
        }

        /// Leaves (a guest) or closes (the owner) the co-op game. The owner may hand the world on instead.
        public async Task Leave()
        {
            if (leaving) return;
            leaving = true;
            try
            {
                if (Hosting && Phase == SessionPhase.InProgress && CoopBans.KeepGoing && Net != null && Net.ConnectedClientsIds.Count > 1) await HandOver();
                if (Session != null)
                {
                    try
                    {
                        if (Hosting) await Session.AsHost().DeleteAsync();
                        else await Session.LeaveAsync();
                    }
                    catch (Exception e) { Debug.LogWarning("Leaving the session: " + e.Message); }
                }
                await Shutdown();
                lastCode = lastSessionId = null;
                Reconnecting = false;
            }
            finally { leaving = false; }
        }

        async Task Shutdown()
        {
            var s = Session;
            Session = null;
            if (s != null) Forget(s);
            if (Net != null)
            {
                // The network object lives for the whole game: destroying it while it shuts down crashes the transport.
                shuttingDown = true;   // our own goodbye isn't a lost connection
                try
                {
                    if (Net.IsListening) Net.Shutdown();
                    for (int i = 0; i < 300 && Net != null && (Net.ShutdownInProgress || Net.IsListening); i++) await Task.Yield();
                }
                finally { shuttingDown = false; }
            }
            handlersRegistered = false;
            people.Clear();
            pendingPeople.Clear();
            ready.Clear();
            Hosting = false;
            Phase = SessionPhase.None;
        }


        void OnRemoved()
        {
            if (Hosting) { End("The co-op game closed."); return; }
            if (kickedFor != null) { End(kickedFor); return; }
            Dropped();   // a relay hiccup also removes you: come back
        }

        void OnDeleted()
        {
            if (followName != null) { Follow(); return; }   // the owner handed the world on: go with it
            End("The owner closed the game.");
        }

        async void End(string why)
        {
            if (leaving) return;
            Reconnecting = false;
            await Shutdown();
            lastCode = lastSessionId = null;
            Ended?.Invoke(why);
        }

        // ---------- staying connected ----------

        /// The connection to the owner is gone, but nobody kicked us: keep trying until we're back.
        async void Dropped()
        {
            if (Reconnecting || leaving || Hosting) return;
            Reconnecting = true;
            string id = lastSessionId, code = lastCode;
            await Shutdown();
            Say("Lost the connection. Reconnecting...");
            for (int attempt = 0; Reconnecting; attempt++)
            {
                await Task.Delay(Mathf.Min(10000, 2000 + attempt * 1000));
                if (!Reconnecting || this == null) return;
                try
                {
                    MakeNetworkManager();
                    try { Session = await MultiplayerService.Instance.ReconnectToSessionAsync(id); }
                    catch (SessionException) { Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code); }
                    Remember(Session);
                    Reconnecting = false;
                    Say("Back in.");
                    return;
                }
                catch (SessionException e) when (Gone(e))
                {
                    Reconnecting = false;
                    End("The owner closed the game.");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Reconnect attempt failed: " + e.Message);
                    await Shutdown();
                    if (attempt % 5 == 0) Say("Still reconnecting...");
                }
            }
        }

        /// Stop trying (the player chose Leave while reconnecting).
        public void GiveUp()
        {
            Reconnecting = false;
            followName = null;
        }

        static bool Gone(SessionException e)
        {
            var m = e.Message.ToLowerInvariant();
            return m.Contains("not found") || m.Contains("does not exist") || m.Contains("deleted");
        }

        // ---------- the owner hands the world on ----------

        /// The owner is leaving with "keep going" on: the chosen player (or the first guest) gets the save and opens
        /// a new game under a name only we know; everyone else is told to look for it.
        async Task HandOver()
        {
            ulong heir = ulong.MaxValue;
            string want = PlayerPrefs.GetString("coop_heir", "");
            foreach (var kv in people) if (kv.Key != Net.LocalClientId && kv.Value.id == want) heir = kv.Key;
            if (heir == ulong.MaxValue) foreach (var id in Net.ConnectedClientsIds) if (id != Net.LocalClientId) { heir = id; break; }
            if (heir == ulong.MaxValue) return;
            string newName = "bk_" + Guid.NewGuid().ToString("N").Substring(0, 16);
            string json = SaveJson != null ? SaveJson() : "";
            Send(Msg.HandOff, heir, w => { w.WriteValueSafe(newName); w.WriteValueSafe(json); });
            foreach (var id in Net.ConnectedClientsIds)
                if (id != Net.LocalClientId && id != heir)
                    Send(Msg.MoveTo, id, w => { w.WriteValueSafe(newName); w.WriteValueSafe(people.TryGetValue(heir, out var p) ? p.name : "a friend"); });
            await Task.Delay(1500);   // let the messages arrive before the game closes
        }

        void OnHandOff(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out string newName);
            r.ReadValueSafe(out string json);
            Say("The owner left you their world. Opening it for the others...");
            BecameOwner?.Invoke(json);
            TakeOver(newName);
        }

        async void TakeOver(string newName)
        {
            followName = null;
            await Shutdown();
            await Host("", newName);
        }

        void OnMoveTo(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out followName);
            r.ReadValueSafe(out string heir);
            Say(heir + " is the new owner. Following...");
        }

        /// Finds the new owner's game by its name and joins it (the world stays loaded meanwhile).
        async void Follow()
        {
            string name = followName;
            followName = null;
            Reconnecting = true;
            await Shutdown();
            for (int attempt = 0; Reconnecting && attempt < 90; attempt++)
            {
                await Task.Delay(2000);
                try
                {
                    var found = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions
                    {
                        FilterOptions = new List<FilterOption> { new FilterOption(FilterField.Name, name, FilterOperation.Equal) },
                    });
                    if (found.Sessions == null || found.Sessions.Count == 0) continue;
                    MakeNetworkManager();
                    Session = await MultiplayerService.Instance.JoinSessionByIdAsync(found.Sessions[0].Id);
                    Remember(Session);
                    Reconnecting = false;
                    Say("Following the new owner. Back in.");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Following the new owner: " + e.Message);
                    await Shutdown();
                }
            }
            if (Reconnecting) { Reconnecting = false; End("Couldn't find the new owner's game."); }
        }

        // ---------- the owner's controls ----------

        /// Removes a player (they can come back with the code).
        public async void Kick(ulong client, string why = "The owner removed you from the game.")
        {
            if (!Hosting || Net == null || client == Net.LocalClientId) return;
            string pid = people.TryGetValue(client, out var p) ? p.id : null;
            people.Remove(client);   // not a dropped connection: no body left waiting
            CoopAvatar.Forget(client);
            Send(Msg.Kicked, client, w => w.WriteValueSafe(why));
            await Task.Delay(300);
            if (Net != null && System.Linq.Enumerable.Contains(Net.ConnectedClientsIds, client)) Net.DisconnectClient(client, why);
            if (pid != null && Session != null)
            {
                try { await Session.AsHost().RemovePlayerAsync(pid); }
                catch (Exception e) { Debug.LogWarning("Removing from the session: " + e.Message); }
            }
        }

        /// Removes a player for good (the owner can unban them in the co-op page).
        public void Ban(ulong client)
        {
            if (!people.TryGetValue(client, out var p)) return;
            CoopBans.Ban(p.id, p.name);
            Kick(client, "The owner banned you from their games.");
        }

        void OnKicked(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out string why);
            kickedFor = string.IsNullOrEmpty(why) ? "The owner removed you from the game." : why;
        }

        // ---------- netcode ----------

        void MakeNetworkManager()
        {
            if (Net != null)
            {
                Net.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(MyPlayerId + "\n" + Coop.PlayerName);
                return;
            }
            var go = new GameObject("Network");   // Netcode needs it at the top of the scene
            var transport = go.AddComponent<UnityTransport>();
            // Never drop anyone for a slow link: a long silence is tolerated, heartbeats keep the relay awake.
            transport.HeartbeatTimeoutMS = 1000;
            transport.DisconnectTimeoutMS = 120000;
            transport.MaxConnectAttempts = 60;
            Net = go.AddComponent<NetworkManager>();
            Net.NetworkConfig ??= new NetworkConfig();
            Net.NetworkConfig.NetworkTransport = transport;
            Net.NetworkConfig.EnableSceneManagement = false;   // everyone already has the one game scene
            Net.NetworkConfig.ConnectionApproval = true;
            Net.NetworkConfig.TickRate = 30;
            Net.NetworkConfig.ClientConnectionBufferTimeout = 60;
            Net.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(MyPlayerId + "\n" + Coop.PlayerName);
            foreach (var name in new[] { AvatarPrefab, CreaturePrefab })
            {
                var prefab = Resources.Load<GameObject>(name);
                if (prefab != null) Net.AddNetworkPrefab(prefab);
                else Debug.LogError("Resources/" + name + " is missing: run Backrooms > Build Co-op Assets");
            }
            Net.ConnectionApprovalCallback = Approve;
            Net.OnClientConnectedCallback += OnClientConnected;
            Net.OnClientDisconnectCallback += OnClientDisconnected;
            Net.OnServerStarted += RegisterHandlers;
            Net.OnClientStarted += RegisterHandlers;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;   // avatars are spawned by hand once the guest says hello
            var payload = request.Payload != null ? System.Text.Encoding.UTF8.GetString(request.Payload) : "";
            var parts = payload.Split('\n');
            string pid = parts.Length > 0 ? parts[0] : "", name = parts.Length > 1 ? Trim(parts[1], 24) : "Wanderer";
            if (request.ClientNetworkId == Net.LocalClientId) { response.Approved = true; return; }   // the owner
            if (CoopBans.IsBanned(pid)) { response.Approved = false; response.Reason = "The owner banned you from their games."; return; }
            int cap = ConfiguredMaxPlayers > 0 ? ConfiguredMaxPlayers : MaxPlayersCap;
            if (Net.ConnectedClientsIds.Count >= cap) { response.Approved = false; response.Reason = "The game is full."; return; }
            pendingPeople[request.ClientNetworkId] = (pid, name);
            bool returning = CoopAvatar.Waiting(pid) != null;   // a dropped player coming back never needs asking
            if (!CoopBans.ApproveJoins || returning || JoinAsked == null) { response.Approved = true; return; }
            response.Pending = true;
            JoinAsked.Invoke(name, ok =>
            {
                response.Approved = ok;
                response.Reason = ok ? null : "The owner didn't let you in.";
                response.Pending = false;
            });
        }


        void RegisterHandlers()
        {
            if (handlersRegistered || Net == null) return;
            handlersRegistered = true;
            var m = Net.CustomMessagingManager;
            m.RegisterNamedMessageHandler(Msg.Hello, OnHello);
            m.RegisterNamedMessageHandler(Msg.World, OnWorld);
            m.RegisterNamedMessageHandler(Msg.Enter, OnEnter);
            m.RegisterNamedMessageHandler(Msg.Exit, OnExit);
            m.RegisterNamedMessageHandler(Msg.Sanity, OnSanity);
            m.RegisterNamedMessageHandler(Msg.Hurt, OnHurt);
            m.RegisterNamedMessageHandler(Msg.Infect, OnInfect);
            m.RegisterNamedMessageHandler(Msg.Kicked, OnKicked);
            m.RegisterNamedMessageHandler(Msg.HandOff, OnHandOff);
            m.RegisterNamedMessageHandler(Msg.MoveTo, OnMoveTo);
            m.RegisterNamedMessageHandler(Msg.Ready, OnReady);
            m.RegisterNamedMessageHandler(Msg.Lobby, OnLobbyState);
            m.RegisterNamedMessageHandler(Msg.Join, OnPlayerJoinedMessage);
            m.RegisterNamedMessageHandler(Msg.Start, OnStartMsg);
            m.RegisterNamedMessageHandler(Msg.LobbyCfg, OnLobbyCfg);
            m.RegisterNamedMessageHandler(Msg.Suit, OnSuit);
            foreach (var h in extra) m.RegisterNamedMessageHandler(h.Key, h.Value);
        }



        readonly Dictionary<string, CustomMessagingManager.HandleNamedMessageDelegate> extra = new Dictionary<string, CustomMessagingManager.HandleNamedMessageDelegate>();

        /// Other systems' messages (shared world state, creatures, voice...). Registered on every connection.
        public void Handle(string name, CustomMessagingManager.HandleNamedMessageDelegate handler)
        {
            extra[name] = handler;
            if (Net != null && handlersRegistered) Net.CustomMessagingManager.RegisterNamedMessageHandler(name, handler);
        }

        void OnClientConnected(ulong id)
        {
            if (Net.IsServer && id == Net.LocalClientId)
            {
                people[id] = (MyPlayerId, Coop.PlayerName);
                // host is not auto-ready in lobby; must ready up like everyone else
                SpawnAvatar(id, MyPlayerId);
                LobbyChanged?.Invoke();
            }
            else if (!Net.IsServer && id == Net.LocalClientId)
                Send(Msg.Hello, NetworkManager.ServerClientId, w => w.WriteValueSafe(Coop.PlayerName));
        }


        void OnClientDisconnected(ulong id)
        {
            if (Net == null || shuttingDown) return;
            ready.Remove(id);
            if (!Net.IsServer && id == Net.LocalClientId)
            {
                string reason = Net.DisconnectReason;
                if (!string.IsNullOrEmpty(kickedFor)) End(kickedFor);
                else if (!string.IsNullOrEmpty(reason) && (reason.Contains("banned") || reason.Contains("full") || reason.Contains("didn't let you"))) End(reason);
                else if (followName != null) Follow();
                else if (Phase == SessionPhase.Lobby) End("Disconnected from lobby.");
                else End("Disconnected from game.");
                return;
            }
            if (Net.IsServer && people.TryGetValue(id, out var p))
            {
                if (Phase == SessionPhase.Lobby)
                {
                    // pre-start: leave lobby instead of holding a waiting body
                    Say(p.name + " left the lobby.");
                    people.Remove(id);
                    CoopAvatar.Forget(id);
                    LobbyChanged?.Invoke();
                    BroadcastLobbyState();
                    _ = RefreshSessionProperties();
                }
                else
                {
                    // in-game: player leaves (no waiting body / no reconnect-in-place per lobby rules)
                    Say(p.name + " disconnected.");
                    people.Remove(id);
                    CoopAvatar.Forget(id);
                    LobbyChanged?.Invoke();
                    _ = RefreshSessionProperties();
                }
            }
        }



        void SpawnAvatar(ulong owner, string playerId)
        {
            var back = CoopAvatar.Waiting(playerId);
            if (back != null) { back.Return(owner); return; }   // a dropped player is back in their own body
            var prefab = Resources.Load<GameObject>(AvatarPrefab);
            if (prefab == null) return;
            var go = Instantiate(prefab);
            var no = go.GetComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;   // if their connection drops, the body stays and waits
            no.SpawnWithOwnership(owner, true);
            go.GetComponent<CoopAvatar>().SetPlayer(playerId);
        }

        // ---------- messages ----------

        static class Msg
        {
            public const string Hello = "bk.hello", World = "bk.world", Enter = "bk.enter", Exit = "bk.exit", Sanity = "bk.sanity", Hurt = "bk.hurt", Infect = "bk.infect",
                Kicked = "bk.kicked", HandOff = "bk.handoff", MoveTo = "bk.moveto",
                Ready = "bk.ready", Lobby = "bk.lobby", Join = "bk.join", Start = "bk.start", LobbyCfg = "bk.lobbycfg", Suit = "bk.suit";
        }


        public void Send(string name, ulong to, Action<FastBufferWriter> write, NetworkDelivery delivery = NetworkDelivery.ReliableFragmentedSequenced)
        {
            if (Net == null || !Net.IsListening) return;
            using var w = new FastBufferWriter(256, Unity.Collections.Allocator.Temp, 1 << 20);
            write(w);
            Net.CustomMessagingManager.SendNamedMessage(name, to, w, delivery);
        }

        /// To everyone else (owner) â€” guests only ever talk to the owner.
        public void SendAll(string name, Action<FastBufferWriter> write, NetworkDelivery delivery = NetworkDelivery.ReliableFragmentedSequenced)
        {
            if (Net == null || !Net.IsServer) return;
            foreach (var id in Net.ConnectedClientsIds)
                if (id != Net.LocalClientId) Send(name, id, write, delivery);
        }

        void OnHello(ulong from, FastBufferReader r)
        {
            if (!Net.IsServer) return;
            r.ReadValueSafe(out string n);
            var who = pendingPeople.TryGetValue(from, out var p) ? p : ("", Trim(n, 24));
            pendingPeople.Remove(from);
            // Back before we noticed they'd gone (their game crashed or restarted): the old connection is stale.
            ulong stale = ulong.MaxValue;
            foreach (var kv in people) if (kv.Key != from && !string.IsNullOrEmpty(who.Item1) && kv.Value.id == who.Item1) stale = kv.Key;
            if (stale != ulong.MaxValue)
            {
                people.Remove(stale);
                CoopAvatar.MarkWaiting(who.Item1);
                Net.DisconnectClient(stale, "Replaced by a new connection.");
            }
            people[from] = (who.Item1, Trim(n, 24));
            bool returning = CoopAvatar.Waiting(who.Item1) != null;
            Say(people[from].name + (returning ? " is back." : " joined."));
            PlayerJoined?.Invoke();
            SendAll(Msg.Join, w => { });
            ready.Remove(from); // must ready up (including after join/rejoin in lobby)
            if (Phase == SessionPhase.Lobby)
            {
                // in lobby: spawn avatar for lobby visuals/state but don't send world/start yet
                SpawnAvatar(from, who.Item1);
                LobbyChanged?.Invoke();
                BroadcastLobbyState();
                _ = RefreshSessionProperties();
                return;
            }
            var info = DescribeWorld != null ? DescribeWorld() : default;
            Send(Msg.World, from, w => info.Write(w));
            SpawnAvatar(from, who.Item1);
            _ = RefreshSessionProperties();
        }


        void OnWorld(ulong from, FastBufferReader r)
        {
            if (Net.IsServer) return;
            var info = WorldInfo.Read(r);
            WorldReceived?.Invoke(info);
        }

        /// The owner moves everyone to a level.
        public void BroadcastEnter(string level, Vector2Int arrival, string cameFrom) =>
            SendAll(Msg.Enter, w => { w.WriteValueSafe(level); w.WriteValueSafe(arrival.x); w.WriteValueSafe(arrival.y); w.WriteValueSafe(cameFrom ?? ""); });

        void OnEnter(ulong from, FastBufferReader r)
        {
            if (Net.IsServer) return;
            r.ReadValueSafe(out string level);
            r.ReadValueSafe(out int x);
            r.ReadValueSafe(out int z);
            r.ReadValueSafe(out string came);
            EnterLevel?.Invoke(level, new Vector2Int(x, z), came);
        }

        /// A guest stepped into an exit: the owner decides.
        public void RequestExit(string target, Vector3 at) =>
            Send(Msg.Exit, NetworkManager.ServerClientId, w => { w.WriteValueSafe(target); w.WriteValueSafe(at); });

        readonly Dictionary<ulong, float> nextExitRequest = new Dictionary<ulong, float>();

        void OnExit(ulong from, FastBufferReader r)
        {
            if (!Net.IsServer) return;
            r.ReadValueSafe(out string target);
            r.ReadValueSafe(out Vector3 at);
            if (string.IsNullOrEmpty(target) || target.Length > 32) return;
            if (float.IsNaN(at.x) || float.IsInfinity(at.x) || at.sqrMagnitude > 100000000f) return;
            float now = Time.unscaledTime;
            if (nextExitRequest.TryGetValue(from, out float next) && now < next) return;
            nextExitRequest[from] = now + 1.5f;
            ExitRequested?.Invoke(target, at, from);
        }

        public void BroadcastStartAndWorld(string levelName = null, string mode = null, string difficulty = null)
        {
            if (!Hosting || Net == null) return;
            Phase = SessionPhase.InProgress;
            _ = RefreshSessionProperties(levelName, mode, difficulty);
            SendAll(Msg.Start, w => { });
            var info = DescribeWorld != null ? DescribeWorld() : default;
            foreach (var id in Net.ConnectedClientsIds)
                if (id != Net.LocalClientId) Send(Msg.World, id, w => info.Write(w));
        }

        public void NotifyReady(ulong client, bool on)
        {
            if (Hosting) { if (on) ready.Add(client); else ready.Remove(client); LobbyChanged?.Invoke(); }
        }


        /// A creature on the owner's game shook a guest: their own game applies it.
        void SendSanity(Player.PlayerAgent who, float amount)
        {
            if (Net == null || !Net.IsServer || who == null || !who.TryGetComponent(out NetworkObject no)) return;
            amount = Mathf.Clamp(amount, 0f, 1f);
            Send(Msg.Sanity, no.OwnerClientId, w => w.WriteValueSafe(amount), NetworkDelivery.Unreliable);
        }

        void OnSanity(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out float amount);
            if (Net == null || Net.IsServer || from != NetworkManager.ServerClientId) return;
            if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
            amount = Mathf.Clamp(amount, 0f, 1f);
            var me = Player.Players.Local;
            if (me != null) me.LoseSanity(amount);
        }

        /// A creature on the owner's game wounded a guest: their own game applies it (and decides if they go down).
        void SendHurt(Player.PlayerAgent who, float amount, bool knock, Player.DeathAttack? attack)
        {
            if (Net == null || !Net.IsServer || who == null || !who.TryGetComponent(out NetworkObject no)) return;
            amount = Mathf.Clamp(amount, 0f, 1f);
            bool hasAttack = attack.HasValue;
            var a = attack ?? default;
            Send(Msg.Hurt, no.OwnerClientId, w =>
            {
                w.WriteValueSafe(amount); w.WriteValueSafe(knock); w.WriteValueSafe(hasAttack);
                if (!hasAttack) return;
                w.WriteValueSafe((byte)a.Kind); w.WriteValueSafe((byte)a.Variant);
                w.WriteValueSafe(a.KillerPosition); w.WriteValueSafe(a.KillerYaw);
            });
        }

        void SendInfect(Player.PlayerAgent who)
        {
            if (Net == null || !Net.IsServer || who == null || !who.TryGetComponent(out NetworkObject no)) return;
            Send(Msg.Infect, no.OwnerClientId, w => w.WriteValueSafe(true));
        }

        void OnInfect(ulong from, FastBufferReader r)
        {
            if (Net == null || Net.IsServer || from != NetworkManager.ServerClientId) return;
            var me = Player.Players.Local;
            if (me != null) me.Infect();
        }

        void OnHurt(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out float amount);
            r.ReadValueSafe(out bool knock);
            r.ReadValueSafe(out bool hasAttack);
            Player.DeathAttack attack = default;
            if (hasAttack)
            {
                r.ReadValueSafe(out byte kind); r.ReadValueSafe(out byte variant);
                r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out float yaw);
                if (!System.Enum.IsDefined(typeof(Creatures.CreatureKind), (int)kind)) return;
                attack = new Player.DeathAttack { Kind = (Creatures.CreatureKind)kind, Variant = variant & 1, KillerPosition = at, KillerYaw = yaw };
            }
            if (Net == null || Net.IsServer || from != NetworkManager.ServerClientId) return;
            if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
            var me = Player.Players.Local;
            if (me == null) return;
            me.Hurt(Mathf.Clamp(amount, 0f, 1f), knock, fatal: hasAttack ? attack : (Player.DeathAttack?)null);   // our own health decides whether this is the killing blow
        }

        // ---------- helpers ----------

        void Say(string text)
        {
            Debug.Log("[Co-op] " + text);
            Status?.Invoke(text);
        }

        static string Short(Exception e)
        {
            var m = e is SessionException se ? se.Message : e.Message;
            return m.Length > 120 ? m.Substring(0, 120) + "..." : m;
        }

        static string Trim(string s, int max)
        {
            s = string.IsNullOrWhiteSpace(s) ? "Wanderer" : s.Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

        void OnApplicationQuit()
        {
            Reconnecting = false;
            if (Session != null) _ = Leave();
        }
    }
}

