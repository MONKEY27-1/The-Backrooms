using Backrooms.Player;
using UnityEngine;

namespace Backrooms.Creatures
{
    public enum CreatureKind
    {
        Smiler,     // wiki Entity 3: a grin in the dark. Hold its gaze and back away; run, shout or shine a light and it comes.
        Faceling,   // resident: a faceless person who stands and watches, steps away when you come close. Harmless.
        SkinStealer, // looks like an ordinary wanderer until approached, then reveals itself and attacks.
        PartyGoer,   // wiki Entity 67: nearsighted packs that hunt by sound. One touch is the end; fire breaks them up.
        Clump,       // wiki Entity 5: a knot of limbs, blind, hunts by sound; buried, cornered or in the ducts. Reels you into its mouth.
        FacelingChild, EvolvedFaceling, ShadowFaceling,   // wiki Entity 9's other kinds (see Faceling.cs)
        Hound,       // wiki Entity 8: packs on all fours. Stare one down; don't bolt on sight. Mauls, backs off, circles.
        MothSwarm, MothMales, Praetorian, MothEmpress, MothHive,   // wiki Entity 4 (see Deathmoths.cs)
        Wretch, WretchMass,   // wiki Entity 15: judge one by its voice; boards don't hold them; their sludge turns you
    }

    // Appended only: co-op sends the state as a byte.
    public enum CreatureState { Stalk, Chase, Search, Leave, Watch, Disguise, Lurk, Stare, Lured, Numb, Circle, Lunge, Held, Grab, Vent, Break }

    /// What a creature can see of the world: the flow field and the darkness. Players come from `Players`.
    public interface ICreatureWorld
    {
        NavGrid Grid { get; }
        bool Dark { get; }
        /// Is this spot dark (no working light close by, or a blackout)?
        bool DarkAt(Vector3 p);
        void Caught(Creature by, PlayerAgent who);
        /// A Smiler was hit by an Exterminator: keep them away for a long while.
        void Numbed(Creature who);
        /// A voice in this player's head (on their own game); `from` if it seems to come from somewhere.
        void Whisper(PlayerAgent who, string line, Vector3? from = null);
    }

    /// The shared body of every creature: spawning, senses, movement and state timing. Each kind's brain
    /// decides what to do with them in Think.
    public abstract class Creature : MonoBehaviour
    {
        public CreatureKind Kind;
        public CreatureState State { get; private set; }

        protected ICreatureWorld world;
        /// Who it's after (hunters) or watching (residents).
        public PlayerAgent Target { get; protected set; }
        protected float stateTime, life;
        protected AudioSource voice;

        /// A creature appeared (co-op mirrors it for the guests).
        public static event System.Action<Creature> Spawned;
        /// The authority started a friend's kill. The friend plays it before reporting the death back.
        public static event System.Action<DeathAttack, PlayerAgent> RemoteExecution;

        public CreatureWounds Wounds { get; private set; }
        public bool Executing { get; private set; }
        float executionEnds;
        bool executionSent;

        void Awake() => Wounds = gameObject.AddComponent<CreatureWounds>();

        public int NextDeathVariant() => Random.Range(0, 2);

        public void HoldForExecution(PlayerAgent victim)
        {
            if (victim == null || executionSent) return;
            executionSent = Executing = true;
            executionEnds = Time.time + 9f;
            Target = victim;
            Set(CreatureState.Grab);
        }

        public void ResumeAfterExecution()
        {
            if (!Executing) return;
            Executing = executionSent = false;
            if (world != null && Players.AnyAlive()) Set(CreatureState.Leave);
        }

        /// Begin one of this creature's two full first-person kills. The actual checkpoint/spectator
        /// transition happens only after that sequence finishes on the victim's own game.
        public void Execute(PlayerAgent victim, int variant = -1)
        {
            if (victim == null || !victim.Alive || victim.Dying || executionSent) return;
            var attack = DeathAttack.From(this, variant);
            HoldForExecution(victim);
            victim.Dying = true;
            victim.DeathCause = Kind;
            if (victim.IsLocal)
            {
                victim.CommitFatalAttack(this, attack.Variant);
                world?.Caught(this, victim);
            }
            else RemoteExecution?.Invoke(attack, victim);
        }

        public static bool IsFaceling(CreatureKind k) => k == CreatureKind.Faceling || k == CreatureKind.FacelingChild || k == CreatureKind.EvolvedFaceling || k == CreatureKind.ShadowFaceling;

        public static Creature Spawn(CreatureKind kind, Vector3 at, ICreatureWorld world, CreatureState start = CreatureState.Stalk)
        {
            var go = new GameObject(kind.ToString());
            go.transform.position = at;
            Creature c = kind == CreatureKind.Smiler ? go.AddComponent<Smiler>() : kind == CreatureKind.Hound ? go.AddComponent<Hound>() : kind == CreatureKind.SkinStealer ? go.AddComponent<SkinStealer>() : kind == CreatureKind.Clump ? go.AddComponent<Clump>() : IsFaceling(kind) ? go.AddComponent<Faceling>() : Deathmoth.Is(kind) ? Deathmoth.Add(go, kind) : kind == CreatureKind.Wretch || kind == CreatureKind.WretchMass ? go.AddComponent<Wretch>() : go.AddComponent<Partygoer>();
            c.Kind = kind;
            c.world = world;
            c.voice = go.AddComponent<AudioSource>();
            c.voice.spatialBlend = 1f;
            c.voice.rolloffMode = AudioRolloffMode.Linear;
            c.voice.maxDistance = 22f;
            c.voice.loop = true;
            c.voice.dopplerLevel = 0f;
            c.Target = Players.Nearest(at);
            c.Begin(start);
            if (c.Target != null) c.Face(c.Target.Position);
            Spawned?.Invoke(c);
            return c;
        }

        /// Builds the body and picks the first state.
        protected abstract void Begin(CreatureState start);
        protected abstract void Think(float dt);
        /// A loud noise somewhere.
        public virtual void Hear(Vector3 where, float radius) { }
        protected virtual void Entered(CreatureState s) { }

        protected void Set(CreatureState s)
        {
            State = s;
            stateTime = 0f;
            Entered(s);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            life += dt;
            if (world == null || !Players.AnyAlive()) { Destroy(gameObject); return; }
            if (Executing)
            {
                if (Target != null) Face(Target.Position, 720f);
                if (Time.time >= executionEnds) { Executing = false; executionSent = false; Set(CreatureState.Leave); }
                return;   // held in the kill pose; the victim's sequence decides when the body falls
            }
            Think(dt);
        }

        // ---------- senses and movement ----------

        protected bool CanSee(PlayerAgent who, float dist)
        {
            if (dist > 30f) return false;
            var from = transform.position + Vector3.up * 1.7f;
            return !Physics.Linecast(from, who.Eyes, out var hit, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) == who;
        }

        /// Is any living player looking at it?
        protected bool InPlayerView() => Players.AnySees(transform.position + Vector3.up * 1.2f);

        protected void MoveToward(Vector3 target, float speed, float dt, bool usePath = true)
        {
            var next = target;
            if (usePath) usePath = world.Grid != null && world.Grid.NextWaypoint(transform.position, target, out next);   // no known way: straight, minding the walls
            var step = Flat(next - transform.position);
            if (step.sqrMagnitude < 0.0001f) return;
            var move = Vector3.ClampMagnitude(step, speed * dt);
            if (!usePath && Blocked(transform.position + move)) return;
            transform.position += move;
            if (world.Grid != null && (world.Grid.Uneven || world.Grid.Stepped)) Ground(dt);
        }

        /// Sloping levels (the caves): feet on the rock.
        void Ground(float dt)
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) != null) return;
            var p = transform.position;
            p.y = Mathf.MoveTowards(p.y, hit.point.y, dt * 6f);
            transform.position = p;
        }

        static readonly Collider[] touching = new Collider[8];

        /// Something solid there (players don't count: creatures come right up to them).
        static bool Blocked(Vector3 p)
        {
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up, 0.3f, touching, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Players.Owns(touching[i]) == null) return true;
            return false;
        }

        protected void Face(Vector3 target, float degreesPerSecond = 720f)
        {
            var d = Flat(target - transform.position);
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degreesPerSecond * Time.deltaTime);
        }

        protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
