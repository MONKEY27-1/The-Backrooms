using System;
using System.Collections.Generic;
using Backrooms.Creatures;
using UnityEngine;

namespace Backrooms.Player
{
    /// A fatal attack on the authority's game, or the same attack recreated on the victim's.
    /// Source exists only where the real creature exists; guests use the transmitted transform.
    public struct DeathAttack
    {
        public CreatureKind Kind;
        public int Variant;
        public Vector3 KillerPosition;
        public float KillerYaw;
        public Creature Source;

        public static DeathAttack From(Creature by, int variant = -1)
        {
            return new DeathAttack
            {
                Kind = by.Kind,
                Variant = variant >= 0 ? variant & 1 : by.NextDeathVariant(),
                KillerPosition = by.transform.position,
                KillerYaw = by.transform.eulerAngles.y,
                Source = by,
            };
        }
    }

    /// Anyone playing: you (the scene's player) or, in co-op, a friend's avatar. Creatures, the fear director and
    /// shared level events see players only through this, so they behave the same for one player or four.
    /// On your own player it reads your controller every frame; on a friend's avatar the network layer fills it in.
    public sealed class PlayerAgent : MonoBehaviour
    {
        public bool IsLocal { get; private set; }

        /// False while dead and spectating (co-op on Easy/Normal).
        public bool Alive = true;

        public bool FlashlightOn, Running, Crouching, Sitting, InDark;
        public Vector3 Velocity;
        public float Sanity = 1f;

        /// Where the eyes are (the camera on your own player, the head on an avatar).
        public Transform Head;

        /// A friend's sanity hit (being caught, standing near a hunter): the network layer sends it to their game.
        public static event Action<PlayerAgent, float> RemoteSanityLoss;
        /// A friend was wounded on the owner's game: the network layer sends it to theirs.
        /// The attack data is what lets the victim play that creature's actual kill if this wound is lethal.
        public static event Action<PlayerAgent, float, bool, DeathAttack?> RemoteHurt;
        /// A friend caught the Cycle from a Wretch on the owner's game: the network layer sends it to theirs.
        public static event Action<PlayerAgent> RemoteInfect;
        /// Your own health ran out: back to the checkpoint (or a co-op death).
        public static event Action<PlayerAgent> Downed;
        public static event Action<PlayerAgent, DeathAttack> FatalAttackReceived;

        FirstPersonController body;
        CharacterController controller;
        PlayerTools tools;
        PlayerStatus status;
        Camera cam;

        /// True from the lethal blow until checkpoint/spectator logic takes over. Blocks double kills and input.
        public bool Dying { get; internal set; }
        /// The creature responsible for the current death, for the persistent friend-visible body.
        public CreatureKind? DeathCause { get; internal set; }
        DeathAttack? fatalAttack, lastFatalAttack;
        Creature lastAttacker;
        bool executionHeld;

        public Vector3 Position => transform.position;
        public Vector3 Eyes => Head != null ? Head.position : transform.position + Vector3.up * 1.6f;
        public Vector3 Forward => Head != null ? Head.forward : transform.forward;
        public FirstPersonController Body => body;

        public void ReceiveFatalAttack(DeathAttack attack)
        {
            attack.Source = null;   // only the owner has the real brain; guests find the mirrored body or use the sent pose
            fatalAttack = attack;
            DeathCause = attack.Kind;
            FatalAttackReceived?.Invoke(this, attack);
        }

        internal void CommitFatalAttack(Creature by, int variant = -1)
        {
            Dying = true;
            fatalAttack = DeathAttack.From(by, variant);
            DeathCause = by.Kind;
        }

        public void PlayRemoteDeathPose(CreatureKind kind, int variant, Vector3 position, float yaw)
        {
            if (!IsLocal || Dying || !Alive || !GoreDeath.HasSequence(kind)) return;
            Dying = true;
            DeathCause = kind;
            ReceiveFatalAttack(new DeathAttack { Kind = kind, Variant = variant & 1, KillerPosition = position, KillerYaw = yaw });
            Downed?.Invoke(this);
        }

        void HoldKiller()
        {
            if (executionHeld || lastAttacker == null || !GoreDeath.HasSequence(lastAttacker.Kind)) return;
            executionHeld = true;
            lastAttacker.HoldForExecution(this);
        }

        public void ReleaseKiller()
        {
            if (!executionHeld || lastAttacker == null) return;
            lastAttacker.ResumeAfterExecution();
            executionHeld = false;
        }

        internal bool TakeFatalAttack(out DeathAttack attack)
        {
            attack = fatalAttack ?? default;
            bool has = fatalAttack.HasValue;
            fatalAttack = null;
            return has;
        }

        void Awake()
        {
            body = GetComponent<FirstPersonController>();
            IsLocal = body != null;
            if (!IsLocal) return;
            controller = GetComponent<CharacterController>();
            tools = GetComponent<PlayerTools>();
            status = GetComponent<PlayerStatus>();
            cam = GetComponentInChildren<Camera>();
            if (cam != null) Head = cam.transform;
        }

        void OnEnable() => Players.Add(this);
        void OnDisable() => Players.Remove(this);

        void Update()
        {
            if (!IsLocal) return;
            FlashlightOn = tools != null && tools.FlashlightOn;
            Running = body.Running;
            Crouching = body.Crouching;
            Sitting = body.IsSeated;
            Velocity = controller != null ? controller.velocity : Vector3.zero;
            if (status != null) { Sanity = status.Sanity; InDark = status.InDark; if (Alive) Creatures.Blood.Drip(this, status.Health); }
        }

        /// Shakes this player's mind: straight away for you, through the network for a friend.
        public void LoseSanity(float amount)
        {
            if (IsLocal) { if (status != null) status.Sanity = Mathf.Max(0f, status.Sanity - amount); }
            else RemoteSanityLoss?.Invoke(this, amount);
        }

        /// Wounds this player and knocks them down: straight away for you, through the network for a friend.
        public void Hurt(float amount, bool knock = true, Creature by = null, int deathVariant = -1, DeathAttack? fatal = null)
        {
            if (Dying || !Alive) return;
            if (by != null) lastAttacker = by;
            if (fatal.HasValue) lastFatalAttack = fatal;
            else if (by != null) lastFatalAttack = DeathAttack.From(by, deathVariant);
            if (!IsLocal)
            {
                if (Alive) Creatures.Blood.Wound(this, amount);
                RemoteHurt?.Invoke(this, amount, knock, fatal ?? (by != null ? DeathAttack.From(by, deathVariant) : (DeathAttack?)null));
                return;
            }
            if (status == null) return;
            Creatures.Blood.Wound(this, amount);
            if (knock) { body.KnockDown(1.1f); UI.Captions.Say("[you're thrown to the floor]"); }
            bool down = status.Hurt(amount);
            if (!down) { fatalAttack = null; return; }
            Dying = true;
            fatalAttack = fatal ?? lastFatalAttack ?? (by != null ? DeathAttack.From(by, deathVariant) : lastAttacker != null ? DeathAttack.From(lastAttacker) : (DeathAttack?)null);
            if (fatalAttack.HasValue) DeathCause = fatalAttack.Value.Kind;
            HoldKiller();
            Downed?.Invoke(this);
        }

        /// Wretch sludge: starts the Cycle (almond water stops it).
        public void Infect()
        {
            if (!IsLocal) { RemoteInfect?.Invoke(this); return; }
            if (status != null && Alive) status.Infection = Mathf.Max(status.Infection, 0.02f);
        }

        /// Losing health little by little (no knockdown): your own player only.
        public void Bleed(float amount, Creature by = null, DeathAttack? fatal = null)
        {
            if (!IsLocal || status == null || !Alive || Dying) return;
            if (by != null) lastAttacker = by;
            if (fatal.HasValue) lastFatalAttack = fatal;
            else if (by != null) lastFatalAttack = DeathAttack.From(by);
            bool down = status.Hurt(amount);
            if (!down) { fatalAttack = null; return; }
            Dying = true;
            fatalAttack = fatal ?? lastFatalAttack ?? (by != null ? DeathAttack.From(by) : lastAttacker != null ? DeathAttack.From(lastAttacker) : (DeathAttack?)null);
            if (fatalAttack.HasValue) DeathCause = fatalAttack.Value.Kind;
            HoldKiller();
            Downed?.Invoke(this);
        }

        /// Could this player see the spot right now (in view, nothing in the way)?
        public bool Sees(Vector3 p)
        {
            if (!Alive) return false;
            if (IsLocal && cam != null)
            {
                var v = cam.WorldToViewportPoint(p);
                if (v.z <= 0f || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) return false;
            }
            else if (Vector3.Angle(Forward, p - Eyes) > 50f) return false;   // a friend's view: about a 100° cone
            return !Physics.Linecast(Eyes, p, out var hit, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) == this;
        }

        public static void ResetEvents() { RemoteSanityLoss = null; RemoteHurt = null; RemoteInfect = null; Downed = null; FatalAttackReceived = null; }

        /// Level/checkpoint entry: clear any unfinished attack left by a disconnected or interrupted sequence.
        public void ResetDeath()
        {
            Dying = false;
            DeathCause = null;
            fatalAttack = null;
            lastFatalAttack = null;
            lastAttacker = null;
            executionHeld = false;
        }
    }

    /// Everyone playing right now. One player in single-player; up to four in co-op.
    public static class Players
    {
        static readonly List<PlayerAgent> all = new List<PlayerAgent>();
        static readonly Dictionary<Collider, PlayerAgent> colliders = new Dictionary<Collider, PlayerAgent>();

        public static IReadOnlyList<PlayerAgent> All => all;
        public static PlayerAgent Local { get; private set; }

        public static void Add(PlayerAgent a)
        {
            if (all.Contains(a)) return;
            all.Add(a);
            if (a.IsLocal) Local = a;
            foreach (var c in a.GetComponentsInChildren<Collider>(true)) colliders[c] = a;
        }

        public static void Remove(PlayerAgent a)
        {
            all.Remove(a);
            if (Local == a) Local = null;
            var drop = new List<Collider>();
            foreach (var kv in colliders) if (kv.Value == a || kv.Key == null) drop.Add(kv.Key);
            foreach (var c in drop) colliders.Remove(c);
        }

        /// The player this collider belongs to, or null.
        public static PlayerAgent Owns(Collider c) => c != null && colliders.TryGetValue(c, out var a) ? a : null;

        public static bool AnyAlive()
        {
            foreach (var a in all) if (a != null && a.Alive) return true;
            return false;
        }

        /// The nearest living player (flat distance), or null.
        public static PlayerAgent Nearest(Vector3 p)
        {
            PlayerAgent best = null;
            float bestD = float.MaxValue;
            foreach (var a in all)
            {
                if (a == null || !a.Alive) continue;
                var d = a.Position - p;
                float dd = d.x * d.x + d.z * d.z + d.y * d.y * 4f;   // another floor counts as further away
                if (dd < bestD) { bestD = dd; best = a; }
            }
            return best;
        }

        /// A living player picked by the roll (0..1), or null.
        public static PlayerAgent Pick(float roll)
        {
            int alive = 0;
            foreach (var a in all) if (a != null && a.Alive) alive++;
            if (alive == 0) return null;
            int k = Mathf.Min(alive - 1, (int)(roll * alive));
            foreach (var a in all)
                if (a != null && a.Alive && k-- == 0) return a;
            return null;
        }

        /// Can any living player see this spot?
        public static bool AnySees(Vector3 p)
        {
            foreach (var a in all) if (a != null && a.Sees(p)) return true;
            return false;
        }

        /// The average sanity of the living (1 when nobody is).
        public static float MeanSanity()
        {
            float sum = 0f;
            int n = 0;
            foreach (var a in all) if (a != null && a.Alive) { sum += a.Sanity; n++; }
            return n == 0 ? 1f : sum / n;
        }

        /// Statics survive Play sessions here (no domain reload).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all.Clear();
            colliders.Clear();
            Local = null;
            PlayerAgent.ResetEvents();
        }
    }
}
