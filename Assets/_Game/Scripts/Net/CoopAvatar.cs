using System.Collections.Generic;
using Backrooms.Creatures;
using Backrooms.Player;
using Backrooms.Game;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Net
{
    /// A player's body in co-op. Its owner moves it to follow their own (first-person) player; everyone else sees
    /// the hazmat suit walking, looking around, its flashlight, and hears its steps. On other games it is that
    /// player's PlayerAgent, so creatures and the director treat friends like you. If its player's connection
    /// drops, the body stays where it stood (held by the owner's game) until they come back to it.
    public sealed class CoopAvatar : NetworkBehaviour
    {
        /// Suit colours, by join order (the owner's is the original yellow).
        public static readonly string[] SuitNames = { "Yellow", "Blue", "Red", "Green" };

        public static CoopAvatar Mine { get; private set; }
        public static readonly Dictionary<ulong, CoopAvatar> All = new Dictionary<ulong, CoopAvatar>();
        public static event System.Action<CoopAvatar> LobbyAvatarSpawned;

        const byte FlagTorch = 1, FlagRun = 2, FlagCrouch = 4, FlagAlive = 8, FlagDark = 16, FlagSit = 32;
        readonly NetworkVariable<byte> flags = new NetworkVariable<byte>(FlagAlive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> deathPose = new NetworkVariable<bool>();
        readonly NetworkVariable<byte> deathKind = new NetworkVariable<byte>();
        readonly NetworkVariable<byte> deathVariant = new NetworkVariable<byte>();
        readonly NetworkVariable<bool> dying = new NetworkVariable<bool>();
        readonly NetworkVariable<bool> died = new NetworkVariable<bool>();
        bool deathPoseHandled;
        readonly NetworkVariable<sbyte> pitch = new NetworkVariable<sbyte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> sanity = new NetworkVariable<byte>(255, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<FixedString32Bytes> nick = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> suit = new NetworkVariable<byte>(0);
        readonly NetworkVariable<FixedString64Bytes> playerKey = new NetworkVariable<FixedString64Bytes>();
        readonly NetworkVariable<bool> waiting = new NetworkVariable<bool>();
        readonly NetworkVariable<byte> emote = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> emoteStarted = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> action = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> actionStarted = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// Called by the shared B-key emote wheel on whichever computer owns this player.
        public void PlayEmote(int id)
        {
            if (!IsOwner || !IsSpawned || id < 0 || id >= SuitEmote.Names.Length) return;
            emote.Value = (byte)(emote.Value == id && SuitEmote.Looping(id) ? 0 : id);
            var net = Coop.Session != null ? Coop.Session.Net : null;
            emoteStartTime = net != null ? (float)net.ServerTime.Time : Time.time;
            emoteStarted.Value = emoteStartTime;
        }

        public void CancelEmote() { if (IsOwner && IsSpawned) emote.Value = 0; }

        bool EmoteActive => emote.Value != SuitEmote.None && (SuitEmote.Looping(emote.Value) ||
            (Coop.Session != null && Coop.Session.Net != null ? (float)Coop.Session.Net.ServerTime.Time - emoteStarted.Value : Time.time - emoteStartTime) < SuitEmote.Duration(emote.Value));

        public bool HasActiveEmote => EmoteActive;

        int VisibleEmote()
        {
            int id = appliedEmote;
            bool lobby = Coop.Session != null && Coop.Session.IsLobby;
            if (id != SuitEmote.None && EmoteActive) return id;
            // Idle thumbs-up is the ready pose in the pre-game room, not a short one-shot.
            return lobby && Coop.Session.IsReady(OwnerClientId) ? SuitEmote.ThumbsUp : SuitEmote.None;
        }

        void AnimateEmote()
        {
            if (rig == null) return;
            int id = VisibleEmote();
            if (id == SuitEmote.None) return;
            var net = Coop.Session != null ? Coop.Session.Net : null;
            float elapsed = net != null ? (float)net.ServerTime.Time - emoteStartTime : Time.time - emoteStartTime;
            bool readyPose = Coop.Session != null && Coop.Session.IsLobby && id == SuitEmote.ThumbsUp && !EmoteActive;
            if (!readyPose && !SuitEmote.Looping(id) && elapsed >= SuitEmote.Duration(id)) return;
            rig.ApplyEmote(id, Mathf.Max(0f, elapsed));
        }

        float NetTime => Coop.Session != null && Coop.Session.Net != null ? (float)Coop.Session.Net.ServerTime.Time : Time.time;

        /// Owners replicate a new action only when it changes (or the same one-shot is deliberately replayed).
        void SyncAction()
        {
            int id = Hands.Current;
            if (action.Value == id && Mathf.Abs(localActionStamp - Hands.Since) < 0.001f) return;
            localActionStamp = Hands.Since;
            action.Value = (byte)id;
            actionStarted.Value = NetTime;
        }

        void AnimateRemoteHands()
        {
            if (rig == null) return;
            int id = action.Value;
            float elapsed = Mathf.Max(0f, NetTime - actionStarted.Value);
            float duration = Hands.Duration(id);
            if (duration > 0f && elapsed >= duration) id = Hands.None;
            float cycle = id == Hands.Vault ? Mathf.Clamp01(elapsed / 0.45f)
                : id == Hands.Ladder ? transform.position.y / 0.35f : elapsed;
            rig.ApplyHands(id, elapsed, (flags.Value & FlagTorch) != 0, cycle);
        }

        public void SetSuitServer(int index)
        {
            if (!IsServer) return;
            index = Mathf.Clamp(index, 0, SuitNames.Length - 1);
            // ensure unique when possible; if taken, keep current
            foreach (var a in FindObjectsByType<CoopAvatar>(FindObjectsSortMode.None))
                if (a != this && a.IsSpawned && a.suit.Value == index) return;
            suit.Value = (byte)index;
        }


        public string Nick => nick.Value.ToString();
        public int VisibleEmoteId => emote.Value;
        public int Suit => suit.Value;
        public string PlayerId => playerKey.Value.ToString();
        public bool Alive => (flags.Value & FlagAlive) != 0 && !waiting.Value;
        /// Its player's connection dropped; the body waits for them.
        public bool Away => waiting.Value;
        /// This body is yours (you move it; you don't see it).
        public bool IsMine => IsOwner && !waiting.Value && PlayerId == CoopSession.MyPlayerId;
        public PlayerAgent Agent { get; private set; }

        Transform head, torch;
        Light torchLight;
        SuitRig rig;
        Renderer[] renderers;
        Collider body;
        AudioSource feet;
        AudioClip[] steps;
        Vector3 lastPos;
        float stepPhase, speed, emoteStartTime;
        float localActionStamp = -1f;
        byte appliedEmote;
        bool mineSetUp, lastLobbyMode;
        AudioSource emoteAudio;
        AudioClip fabricSound;
        ulong key;
        TextMesh nameTag;
        float poseSeed;

        // ---------- the owner's game (server) ----------

        /// Before spawning: whose body this is.
        public void SetPlayer(string playerId)
        {
            playerKey.Value = new FixedString64Bytes(playerId ?? "");
            suit.Value = (byte)FreeSuit();
        }

        public static CoopAvatar Waiting(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            foreach (var a in FindObjectsByType<CoopAvatar>(FindObjectsSortMode.None))
                if (a.IsSpawned && a.waiting.Value && a.PlayerId == playerId) return a;
            return null;
        }

        /// Its player's connection dropped (owner's game): keep the body standing there.
        public static void MarkWaiting(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            foreach (var a in FindObjectsByType<CoopAvatar>(FindObjectsSortMode.None))
                if (a.IsSpawned && a.PlayerId == playerId) a.waiting.Value = true;
        }

        /// They're back (owner's game): the body is theirs again.
        public void Return(ulong client)
        {
            waiting.Value = false;
            NetworkObject.ChangeOwnership(client);
        }

        /// Kicked or banned (owner's game): the body goes with them.
        public static void Forget(ulong client)
        {
            foreach (var a in FindObjectsByType<CoopAvatar>(FindObjectsSortMode.None))
                if (a.IsSpawned && a.OwnerClientId == client) a.NetworkObject.Despawn(true);
        }

        int FreeSuit()
        {
            var used = new HashSet<int>();
            foreach (var a in FindObjectsByType<CoopAvatar>(FindObjectsSortMode.None)) if (a != this && a.IsSpawned) used.Add(a.suit.Value);
            for (int i = 0; i < SuitNames.Length; i++) if (!used.Contains(i)) return i;
            return 0;
        }

        // ---------- everyone ----------

        public override void OnNetworkSpawn()
        {
            head = transform.Find("Head");
            torch = head != null ? head.Find("Torch") : null;
            torchLight = torch != null ? torch.GetComponent<Light>() : null;
            rig = GetComponentInChildren<SuitRig>();
            renderers = GetComponentsInChildren<Renderer>(true);
            body = GetComponent<Collider>();
            suit.OnValueChanged += (_, s) => ApplySuit(s);
            playerKey.OnValueChanged += (_, __) => SetUp();
            waiting.OnValueChanged += (_, __) => SetUp();
            dying.OnValueChanged += OnDyingChanged;
            died.OnValueChanged += OnDiedChanged;
            nick.OnValueChanged += (_, __) => UpdateNameTag();
            ApplySuit(suit.Value);
            deathPose.OnValueChanged += OnDeathPoseChanged;
            if (deathPose.Value) OnDeathPoseChanged(false, true);
            poseSeed = (OwnerClientId * 0.37f) % 1f;
            CreateNameTag();
            lastPos = transform.position;
            Rekey();
            SetUp();
            lastLobbyMode = Coop.Session != null && Coop.Session.IsLobby;
            if (lastLobbyMode) LobbyAvatarSpawned?.Invoke(this);
            appliedEmote = emote.Value;
            emoteStartTime = Time.time;
            emote.OnValueChanged += OnEmoteChanged;
            emoteStarted.OnValueChanged += (_, t) => emoteStartTime = t;
            emoteAudio = gameObject.AddComponent<AudioSource>();
            emoteAudio.playOnAwake = false;
            emoteAudio.spatialBlend = 1f;
            emoteAudio.rolloffMode = AudioRolloffMode.Linear;
            emoteAudio.minDistance = 1.5f;
            emoteAudio.maxDistance = 14f;
            emoteAudio.dopplerLevel = 0f;
            fabricSound = Audio.ProceduralAudio.Fabric();
        }

        void OnEmoteChanged(byte previous, byte next)
        {
            appliedEmote = next;
            if (next == SuitEmote.None) return;
            var net = Coop.Session != null ? Coop.Session.Net : null;
            emoteStartTime = net != null ? (float)net.ServerTime.Time : Time.time;
            if (emoteAudio == null || fabricSound == null) return;
            emoteAudio.transform.position = transform.position;
            emoteAudio.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            emoteAudio.PlayOneShot(fabricSound, 0.12f * Meta.AudioMix.Effects);
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            base.OnOwnershipChanged(previous, current);
            Rekey();
            SetUp();
        }

        void Rekey()
        {
            if (All.TryGetValue(key, out var a) && a == this) All.Remove(key);
            key = OwnerClientId;
            All[key] = this;
        }

        /// Yours (hidden, you move it) or someone else's (a body creatures can see).
        void SetUp()
        {
            bool mine = IsMine;
            bool lobby = Coop.Session != null && Coop.Session.IsLobby;
            if (mine && !mineSetUp)
            {
                mineSetUp = true;
                Mine = this;
                if (Agent != null) { Destroy(Agent); Agent = null; }
                nick.Value = new FixedString32Bytes(Coop.PlayerName.Length > 28 ? Coop.PlayerName.Substring(0, 28) : Coop.PlayerName);
                // In lobby show your own suit; in gameplay camera is inside it so hide body but keep shadow.
                foreach (var r in renderers) r.shadowCastingMode = lobby ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                foreach (var r in renderers) r.enabled = lobby || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; // keep visible in lobby
                if (body != null) body.enabled = lobby;
                if (torchLight != null) torchLight.enabled = false;   // your real flashlight lights the way
            }
            else if (!mine)
            {
                if (mineSetUp)
                {
                    mineSetUp = false;
                    if (Mine == this) Mine = null;
                    foreach (var r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                if (Agent == null)
                {
                    Agent = gameObject.AddComponent<PlayerAgent>();
                    Agent.Head = head;
                }
                if (feet == null)
                {
                    feet = gameObject.AddComponent<AudioSource>();
                    feet.spatialBlend = 1f;
                    feet.rolloffMode = AudioRolloffMode.Linear;
                    feet.maxDistance = 22f;
                    feet.dopplerLevel = 0f;
                    steps = Audio.ProceduralAudio.Footsteps();
                }
            }

            // A lobby<->gameplay phase switch does not despawn the network avatar.
            if (mine && lobby)
            {
                foreach (var r in renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.enabled = true; }
                if (body != null) body.enabled = true;
            }
            else if (mine)
            {
                foreach (var r in renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; r.enabled = true; }
                if (body != null) body.enabled = false;
            }
            UpdateNameTag();
        }

        public override void OnNetworkDespawn()
        {
            deathPose.OnValueChanged -= OnDeathPoseChanged;
            dying.OnValueChanged -= OnDyingChanged;
            died.OnValueChanged -= OnDiedChanged;
            if (All.TryGetValue(key, out var a) && a == this) All.Remove(key);
            if (Mine == this) Mine = null;
            if (Agent != null) Destroy(Agent);
        }

        void ApplySuit(int index)
        {
            var mat = Resources.Load<Material>("Suits/Suit_" + Mathf.Clamp(index, 0, SuitNames.Length - 1));
            if (mat == null) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.sharedMaterial = mat;
        }

        void CreateNameTag()
        {
            if (nameTag != null) return;
            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            nameTag = go.AddComponent<TextMesh>();
            nameTag.alignment = TextAlignment.Center;
            nameTag.anchor = TextAnchor.LowerCenter;
            nameTag.fontSize = 48;
            nameTag.characterSize = 0.03f;
            nameTag.color = Color.white;
            var clickArea = go.AddComponent<BoxCollider>();
            clickArea.isTrigger = true;
            clickArea.size = new Vector3(2.4f, 0.45f, 0.08f);
            clickArea.center = new Vector3(0f, 0.13f, 0f);
            UpdateNameTag();
        }

        void UpdateNameTag()
        {
            if (nameTag == null) return;
            bool lobby = Coop.Session != null && Coop.Session.IsLobby;
            nameTag.gameObject.SetActive(lobby);
            nameTag.text = string.IsNullOrEmpty(Nick) ? Coop.PlayerName : Nick;
            if (Camera.main != null) nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - Camera.main.transform.position);
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            bool lobby = Coop.Session != null && Coop.Session.IsLobby;
            if (lobby != lastLobbyMode) { lastLobbyMode = lobby; SetUp(); }
            UpdateNameTag();
            if (lobby) { LobbyPose(); return; }
            if (IsMine) Follow();
            else Show();
        }

        /// Yours: stand where your real player stands, and tell the others what you're doing.
        void Follow()
        {
            var me = Players.Local;
            if (me == null) return;
            transform.SetPositionAndRotation(me.Position, Quaternion.Euler(0f, me.transform.eulerAngles.y, 0f));
            byte f = 0;
            if (me.FlashlightOn) f |= FlagTorch;
            if (me.Running) f |= FlagRun;
            if (me.Crouching) f |= FlagCrouch;
            if (me.Alive) f |= FlagAlive;
            if (me.InDark) f |= FlagDark;
            if (me.Sitting) f |= FlagSit;
            if (flags.Value != f) flags.Value = f;
            var look = me.Forward;
            sbyte p = (sbyte)Mathf.Clamp(Mathf.RoundToInt(-Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f)) * Mathf.Rad2Deg), -85, 85);
            if (pitch.Value != p) pitch.Value = p;
            byte s = (byte)Mathf.RoundToInt(Mathf.Clamp01(me.Sanity) * 255f);
            if (Mathf.Abs(sanity.Value - s) > 6) sanity.Value = s;
            SyncAction();
            if (rig != null)
            {
                rig.Animate(me.Velocity.magnitude, me.Crouching, me.Running, -p, Time.deltaTime, me.Sitting);
                float elapsed = Mathf.Max(0f, Time.time - Hands.Since);
                float cycle = Hands.Current == Hands.Vault && me.Body != null ? me.Body.VaultProgress
                    : Hands.Current == Hands.Ladder ? me.Position.y / 0.35f : elapsed;
                rig.ApplyHands(Hands.Current, elapsed, me.FlashlightOn, cycle);
                AnimateEmote();
            }
        }

        void LobbyPose()
        {
            lastPos = transform.position;
            if (IsOwner)
            {
                flags.Value = FlagAlive;
                pitch.Value = 0;
            }
            transform.SetPositionAndRotation(LobbyEnvironment.PlayerSpot(OwnerClientId), Quaternion.Euler(0f, 180f, 0f));
            if (rig != null) rig.Animate(0f, false, false, 0f, Time.deltaTime, (EmoteActive ? emote.Value : VisibleEmote()) == SuitEmote.Sit);
            AnimateEmote();
            if (head != null) head.localRotation = Quaternion.identity;
            if (head != null) head.localRotation = Quaternion.identity;
            foreach (var rend in renderers) if (rend != null) rend.enabled = true;
            if (body != null) body.enabled = true;
            if (nameTag != null) nameTag.transform.localRotation = Quaternion.identity;
            if (Agent != null) Agent.Alive = false; // no creature interactions in lobby
        }

        /// Host announces that the owner is being executed; every client animates the same killer-driven pose.
        public void PlayDeathPose(DeathAttack attack)
        {
            if (!IsServer) return;
            deathKind.Value = (byte)attack.Kind;
            deathVariant.Value = (byte)(attack.Variant & 1);
            deathPose.Value = true;
            dying.Value = true;
            died.Value = false;
            deathPoseHandled = false;
            // This explicit call matters when the boolean NetworkVariable was already true on a previous death.
            OnDeathPoseChanged(false, true);
        }

        void OnDyingChanged(bool oldValue, bool newValue)
        {
            if (Agent != null) Agent.Alive = !newValue && !died.Value && Alive;
        }

        void OnDiedChanged(bool oldValue, bool newValue)
        {
            if (Agent != null) Agent.Alive = !newValue && !dying.Value && Alive;
        }

        public void SetDiedServer(bool value)
        {
            if (!IsServer) return;
            died.Value = value;
            dying.Value = false;
            deathPose.Value = false;   // completed/revived death: re-arm the event for next time
            if (!value)
            {
                if (!IsMine && rig != null) rig.Animate(0f, false, false, 0f, Time.deltaTime, false);
            }
            if (value && !IsMine) foreach (var r in renderers) if (r != null) r.enabled = false;
        }

        void OnDeathPoseChanged(bool oldValue, bool newValue)
        {
            if (!newValue) { deathPoseHandled = false; return; }
            if (deathPoseHandled) return;
            deathPoseHandled = true;
            int variant = deathVariant.Value;
            var kind = (CreatureKind)deathKind.Value;
            if (IsMine)
            {
                if (!IsServer)
                {
                    var me = Players.Local;
                    if (me != null && me.IsLocal) me.PlayRemoteDeathPose(kind, variant, transform.position, transform.eulerAngles.y);
                }
                return;
            }
            rig?.KillPose(kind, 1f);
        }

        /// Someone else's: their body, flashlight and footsteps, and the PlayerAgent creatures look at.
        void Show()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var delta = transform.position - lastPos;
            lastPos = transform.position;
            float flat = new Vector2(delta.x, delta.z).magnitude / dt;
            if (flat > 20f) flat = 0f;   // a teleport, not a sprint
            speed = Mathf.Lerp(speed, flat, 1f - Mathf.Exp(-dt * 10f));
            byte f = flags.Value;
            bool away = waiting.Value;
            bool shown = (f & FlagAlive) != 0 || away;   // a waiting body stays visible
            bool isDying = dying.Value || died.Value;
            if (Agent != null)
            {
                Agent.Alive = Alive && !isDying;   // creatures leave a waiting or fallen body alone
                Agent.FlashlightOn = (f & FlagTorch) != 0 && !away;
                Agent.Running = (f & FlagRun) != 0 && !away;
                Agent.Crouching = (f & FlagCrouch) != 0;
                Agent.InDark = (f & FlagDark) != 0;
                Agent.Sanity = sanity.Value / 255f;
                Agent.Velocity = delta / dt;
            }
            foreach (var r in renderers) if (r != null) r.enabled = shown || dying.Value;
            if (body != null) body.enabled = shown || dying.Value;
            if (head != null) head.localRotation = Quaternion.Euler(away ? 25f : pitch.Value, 0f, 0f);
            if (torchLight != null) torchLight.enabled = shown && !away && (f & FlagTorch) != 0;
            if (rig != null && !isDying) rig.Animate(away ? 0f : speed, (f & FlagCrouch) != 0, (f & FlagRun) != 0, away ? -25f : -pitch.Value, dt, (f & FlagSit) != 0);
            if (!isDying) AnimateRemoteHands();
            if (!away && !isDying) AnimateEmote();
            if (isDying && !IsMine && rig != null && !died.Value) rig.KillPose((CreatureKind)deathKind.Value, 1f);

            // Footsteps: one per half stride.
            if (!shown || away || isDying || feet == null || steps == null || steps.Length == 0) return;
            float stride = (f & FlagRun) != 0 ? 1.3f : 0.9f;
            stepPhase += speed * dt / stride;
            if (stepPhase >= 1f && speed > 0.4f)
            {
                stepPhase = 0f;
                feet.pitch = Random.Range(0.92f, 1.08f);
                float loud = (f & FlagCrouch) != 0 ? 0.25f : (f & FlagRun) != 0 ? 0.8f : 0.5f;
                feet.PlayOneShot(steps[Random.Range(0, steps.Length)], loud * Meta.AudioMix.Effects);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            Mine = null;
        }
    }
}
