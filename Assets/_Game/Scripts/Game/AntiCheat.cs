using Backrooms.Net;
using Backrooms.Player;
using Backrooms.UI;
using UnityEngine;

namespace Backrooms.Game
{
    /// Local anti-cheat: watches for obvious movement abuse and rejects suspicious co-op actions that could interfere
    /// with other players. Legitimate developer tools/actions (F4/F5/F8/F10, fly/freecam, dev teleports and world
    /// edits) are exempt so testing never trips it.
    public sealed class AntiCheat : MonoBehaviour
    {
        public static AntiCheat Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] float maxHorizontalSpeed = 11f;      // generous over sprint+hop/slide to avoid false positives
        [SerializeField] float maxVerticalUpSpeed = 28f;      // jumps, climbs, swimming ascent
        [SerializeField] float maxVerticalDownSpeed = 80f;    // long falls
        [SerializeField] float maxInstantMove = 6f;           // position jump in one sample without a trusted teleport
        [SerializeField] float speedViolationTime = 0.45f;
        [SerializeField] float warnCooldown = 4f;

        GameSession session;
        FirstPersonController body;
        CharacterController controller;
        PlayerHud hud;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevMenu devMenu;
#endif

        Vector3 lastGood;
        bool hasGood;
        float speedBadTime;
        float nextWarn;
        float trustedUntil;        // grace window for teleports/level loads
        int correctionBursts;

        public static bool IsMovementBypassed
        {
            get
            {
                if (Instance == null) return true;
                return Instance.Bypassed();
            }
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Init(GameSession game, FirstPersonController player, CharacterController cc, PlayerHud playerHud
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            , DevMenu menu
#endif
            )
        {
            session = game;
            body = player;
            controller = cc;
            hud = playerHud;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            devMenu = menu;
#endif
            TrustPosition();
        }

        /// Call around intentional teleports/warps (level changes, checkpoints, respawns).
        public void TrustPosition(float graceSeconds = 1.25f)
        {
            if (body != null) lastGood = body.transform.position;
            hasGood = true;
            speedBadTime = 0f;
            correctionBursts = 0;
            trustedUntil = Mathf.Max(trustedUntil, Time.unscaledTime + graceSeconds);
        }

        void LateUpdate()
        {
            if (body == null || controller == null || session == null) return;
            if (Bypassed())
            {
                TrustPosition(0.2f);
                return;
            }

            Vector3 pos = body.transform.position;
            if (!hasGood)
            {
                lastGood = pos;
                hasGood = true;
                return;
            }

            float dt = Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime, 0.0001f);
            Vector3 delta = pos - lastGood;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float flatSpeed = flat.magnitude / dt;
            float vertSpeed = delta.y / dt;

            bool instantJump = delta.magnitude > maxInstantMove;
            bool speedBad = flatSpeed > AllowedHorizontalSpeed()
                || vertSpeed > maxVerticalUpSpeed
                || vertSpeed < -maxVerticalDownSpeed;

            if (instantJump)
            {
                Correct("Movement anomaly corrected.");
                return;
            }

            if (speedBad) speedBadTime += dt;
            else speedBadTime = Mathf.Max(0f, speedBadTime - dt * 2f);

            if (speedBadTime >= speedViolationTime)
            {
                Correct("Abnormal movement corrected.");
                return;
            }

            lastGood = pos;
        }

        bool Bypassed()
        {
            if (session == null || body == null) return true;
            if (session.InMenu || session.Busy) return true;
            if (!body.enabled) return true;
            if (Time.unscaledTime < trustedUntil) return true;
            // Legit states where position/CC behavior is special.
            if (body.IsSeated || body.Vaulting || body.Sliding || body.Climbing || body.Swimming) return true;
            if (!controller.enabled) return true;
            var spectator = body.GetComponent<Spectator>();
            if (spectator != null && spectator.Watching) return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (devMenu != null)
            {
                if (devMenu.IsOpen || devMenu.FlyActive || devMenu.FreecamActive) return true;
            }
#endif
            return false;
        }

        float AllowedHorizontalSpeed()
        {
            // Sprint (4.8) + hop boost (x1.25) ~= 6, slide 6.4, plus tolerance for latency/frame spikes.
            float allowed = maxHorizontalSpeed;
            if (body.Running) allowed = Mathf.Max(allowed, 8.5f);
            return allowed;
        }

        void Correct(string message)
        {
            correctionBursts++;
            speedBadTime = 0f;
            if (hasGood && body != null)
            {
                bool cc = controller.enabled;
                controller.enabled = false;
                body.transform.position = lastGood;
                Physics.SyncTransforms();
                controller.enabled = cc;
            }
            if (Time.unscaledTime >= nextWarn)
            {
                nextWarn = Time.unscaledTime + warnCooldown;
                Debug.LogWarning("[AntiCheat] " + message);
                if (hud != null) hud.Flash(message, 3f);
            }
            if (correctionBursts > 12) trustedUntil = Time.unscaledTime + 1f; // avoid correction spam loops
        }

        // ---------- co-op validation helpers ----------

        public struct RateLimit
        {
            public float window;
            public int max;
            public float start;
            public int count;
            public RateLimit(float windowSeconds, int maxCount) { window = windowSeconds; max = maxCount; start = 0f; count = 0; }
            public bool Allow(float now)
            {
                if (now - start > window) { start = now; count = 0; }
                count++;
                return count <= max;
            }
        }

        public static bool SenderIsServer(ulong from)
        {
            return Coop.Session != null && Coop.Session.Net != null && from == Unity.Netcode.NetworkManager.ServerClientId;
        }

        public static bool ReasonableNoise(Vector3 at, float radius, ulong from)
        {
            if (radius <= 0f || radius > 60f) return false;
            if (at.sqrMagnitude > 100000000f || float.IsNaN(at.x) || float.IsInfinity(at.x)) return false;
            var avatar = FindAvatar(from);
            if (avatar != null && (avatar.transform.position - at).sqrMagnitude > 2500f) return false; // within ~50m of sender
            return true;
        }

        public static CoopAvatar FindAvatar(ulong clientId)
        {
            if (CoopAvatar.All.TryGetValue(clientId, out var a)) return a;
            return null;
        }
    }
}
