using Backrooms.Player;
using UnityEngine;

namespace Backrooms.Creatures
{
    public enum CreatureKind
    {
        Smiler,     // hunter: lives in the dark. Tell: a grin in the black and a crackle. Way out: hide, or outlast the dark.
        Faceling,   // resident: a faceless person who stands and watches, steps away when you come close. Harmless.
        SkinStealer, // looks like an ordinary wanderer until approached, then reveals itself and attacks.
        PartyGoer,   // rare, roaming hunter; seeing its fixed smile may be the last warning.
    }

    public enum CreatureState { Stalk, Chase, Search, Leave, Watch, Disguise }

    /// What a creature can see of the world: the flow field and the darkness. Players come from `Players`.
    public interface ICreatureWorld
    {
        NavGrid Grid { get; }
        bool Dark { get; }
        void Caught(Creature by, PlayerAgent who);
    }

    /// One shared brain; each kind sets its own numbers. States: stalk -> chase -> search -> leave (hunters),
    /// watch -> leave (residents).
    public sealed class Creature : MonoBehaviour
    {
        public CreatureKind Kind;
        public CreatureState State { get; private set; }

        ICreatureWorld world;
        /// Who it's after (hunters) or watching (residents): the nearest living player unless it's chasing someone.
        public PlayerAgent Target { get; private set; }
        Vector3 lastSeen;
        float stateTime, lostFor, seenFor, life;
        AudioSource voice;
        Transform glow;   // the Smiler's grin and eyes
        bool hostile;
        bool revealed;
        CreatureBodyMotion bodyMotion;

        float StalkSpeed => Kind == CreatureKind.PartyGoer ? 1.5f : 1.1f;
        float ChaseSpeed => Kind == CreatureKind.PartyGoer ? 4.8f : Kind == CreatureKind.SkinStealer ? 4.5f : 4.0f;
        const float CatchDistance = 1.1f;

        /// A creature appeared (co-op mirrors it for the guests).
        public static event System.Action<Creature> Spawned;

        public static Creature Spawn(CreatureKind kind, Vector3 at, ICreatureWorld world)
        {
            var go = new GameObject(kind.ToString());
            go.transform.position = at;
            var c = go.AddComponent<Creature>();
            c.Kind = kind;
            c.world = world;
            c.hostile = kind == CreatureKind.Smiler || kind == CreatureKind.PartyGoer;
            c.revealed = kind != CreatureKind.SkinStealer;
            c.State = kind == CreatureKind.SkinStealer ? CreatureState.Disguise : c.hostile ? CreatureState.Stalk : CreatureState.Watch;
            CreatureBodies.Build(kind, go.transform, out c.glow);
            CreatureBodies.SetSkinStealerRevealed(go.transform, c.revealed);
            c.bodyMotion = go.AddComponent<CreatureBodyMotion>();
            c.bodyMotion.Initialize(kind);
            c.voice = go.AddComponent<AudioSource>();
            c.voice.spatialBlend = 1f;
            c.voice.rolloffMode = AudioRolloffMode.Linear;
            c.voice.maxDistance = 22f;
            c.voice.loop = true;
            c.voice.dopplerLevel = 0f;
            if (kind == CreatureKind.Smiler)
            {
                c.voice.clip = Audio.ProceduralAudio.Static();
                c.voice.pitch = 0.45f;
                c.voice.volume = 0.25f * Meta.AudioMix.Creatures;
                c.voice.Play();
            }
            c.Target = Players.Nearest(at);
            if (c.Target != null) c.Face(c.Target.Position);
            Spawned?.Invoke(c);
            return c;
        }

        /// A loud noise somewhere: a hunter that hasn't found you yet comes to look.
        public void Hear(Vector3 where, float radius)
        {
            if (!hostile || State == CreatureState.Leave || State == CreatureState.Chase || State == CreatureState.Disguise) return;
            if (Vector3.Distance(where, transform.position) > radius) return;
            lastSeen = where;
            Set(CreatureState.Search);
        }

        void Set(CreatureState s)
        {
            State = s;
            stateTime = 0f;
            if (Kind == CreatureKind.SkinStealer && !revealed && s == CreatureState.Chase)
            {
                revealed = true;
                hostile = true;
                CreatureBodies.SetSkinStealerRevealed(transform, true);
                if (voice != null) voice.PlayOneShot(Audio.ProceduralAudio.Laughter(), 0.45f * Meta.AudioMix.Creatures);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            life += dt;
            if (world == null || !Players.AnyAlive()) { Destroy(gameObject); return; }
            // A chase sticks to its prey while they live; otherwise it turns to whoever is nearest.
            if (Target == null || !Target.Alive || State != CreatureState.Chase) Target = Players.Nearest(transform.position);
            if (Target == null) return;
            if (hostile && State == CreatureState.Stalk)
                foreach (var other in Players.All)   // anyone who gives themselves away becomes the prey
                    if (other != null && other.Alive && other != Target)
                    {
                        float d = Flat(other.Position - transform.position).magnitude;
                        if (Noticed(other, d, CanSee(other, d))) { Target = other; break; }
                    }
            var player = Target.Position;
            float dist = Flat(player - transform.position).magnitude;
            bool sees = CanSee(Target, dist);

            if (Kind == CreatureKind.SkinStealer && State == CreatureState.Disguise)
            {
                Face(player, 90f);
                // It passes for a lone wanderer until you get close enough to recognize what is wrong.
                if ((sees && dist < 5f) || dist < 2.5f)
                {
                    lastSeen = player;
                    Set(CreatureState.Chase);
                }
                else if (life > 150f) Set(CreatureState.Leave);
            }
            else if (hostile) Hunt(player, dist, sees, dt);
            else Reside(player, dist, dt);
        }

        // ---------- hunters ----------

        void Hunt(Vector3 player, float dist, bool sees, float dt)
        {
            // The grin only shows in the dark; in the light it is just gone.
            if (Kind == CreatureKind.Smiler && !world.Dark && State != CreatureState.Leave) Set(CreatureState.Leave);
            if (glow != null) glow.gameObject.SetActive(State != CreatureState.Leave || Mathf.Repeat(stateTime * 7f, 1f) < 0.5f);

            switch (State)
            {
                case CreatureState.Stalk:
                    // Drifts closer, keeping its distance, until something gives you away.
                    if (Noticed(Target, dist, sees)) { lastSeen = player; Set(CreatureState.Chase); break; }
                    if (dist > 7f) MoveToward(player, StalkSpeed, dt);
                    Face(player);
                    if (stateTime > 50f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Chase:
                    if (sees) { lastSeen = player; lostFor = 0f; }
                    else lostFor += dt;
                    MoveToward(sees ? player : lastSeen, ChaseSpeed, dt);
                    Face(sees ? player : lastSeen);
                    if (dist < CatchDistance && sees) { world.Caught(this, Target); Set(CreatureState.Leave); break; }
                    if (lostFor > 4f) Set(CreatureState.Search);   // you broke its line of sight long enough
                    break;

                case CreatureState.Search:
                    if (Noticed(Target, dist, sees)) { lastSeen = player; Set(CreatureState.Chase); break; }
                    if (Flat(lastSeen - transform.position).magnitude > 0.8f) MoveToward(lastSeen, StalkSpeed * 1.5f, dt);
                    else transform.Rotate(0f, 60f * dt, 0f);   // looks around where it lost you
                    if (stateTime > 12f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Leave:
                    voice.volume = Mathf.MoveTowards(voice.volume, 0f, dt * 0.3f);
                    if (stateTime > 1.5f) Destroy(gameObject);
                    break;
            }

            if (voice == null || Kind != CreatureKind.Smiler) return;
            if (State == CreatureState.Chase) { voice.pitch = 0.75f; voice.volume = Mathf.MoveTowards(voice.volume, 0.6f * Meta.AudioMix.Creatures, dt); }
            else if (State != CreatureState.Leave) { voice.pitch = 0.45f; voice.volume = 0.25f * Meta.AudioMix.Creatures; }
        }

        /// Does it notice this player? A flashlight beam on it, running nearby, or being seen up close.
        bool Noticed(PlayerAgent who, float dist, bool sees)
        {
            if (sees && who.FlashlightOn && dist < 20f)
            {
                var toMe = (transform.position + Vector3.up * 1.6f - who.Eyes).normalized;
                if (Vector3.Angle(who.Forward, toMe) < 25f) return true;   // they shone the light on it
            }
            if (who.Running && dist < 18f) return true;                    // it heard them run
            float sight = 9f * (who.Crouching ? 0.45f : 1f) * (who.FlashlightOn ? 1.8f : 1f);
            if (sees && dist < sight) return true;
            return dist < 2.2f;   // close enough to smell
        }

        // ---------- residents ----------

        void Reside(Vector3 player, float dist, float dt)
        {
            bool inView = InPlayerView();
            if (inView) seenFor += dt;
            switch (State)
            {
                case CreatureState.Watch:
                    Face(player, 25f);
                    if (dist < 6f) MoveToward(transform.position + Flat(transform.position - player).normalized * 3f, 1.0f, dt, false);
                    if (seenFor > 2f && !inView && stateTime > 8f) Set(CreatureState.Leave);   // gone the moment you look away
                    if (life > 90f) Set(CreatureState.Leave);
                    break;
                case CreatureState.Leave:
                    if (!inView || stateTime > 6f) Destroy(gameObject);
                    else MoveToward(transform.position + transform.forward * -2f, 0.8f, dt, false);
                    break;
            }
        }

        // ---------- senses and movement ----------

        bool CanSee(PlayerAgent who, float dist)
        {
            if (dist > 30f) return false;
            var from = transform.position + Vector3.up * 1.7f;
            return !Physics.Linecast(from, who.Eyes, out var hit, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) == who;
        }

        /// Is any living player looking at it?
        bool InPlayerView() => Players.AnySees(transform.position + Vector3.up * 1.2f);

        void MoveToward(Vector3 target, float speed, float dt, bool usePath = true)
        {
            var next = usePath && world.Grid != null ? world.Grid.NextWaypoint(transform.position, target) : target;
            var step = Flat(next - transform.position);
            if (step.sqrMagnitude < 0.0001f) return;
            var move = Vector3.ClampMagnitude(step, speed * dt);
            if (!usePath && Physics.CheckSphere(transform.position + move + Vector3.up * 1f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return;
            transform.position += move;
        }

        void Face(Vector3 target, float degreesPerSecond = 720f)
        {
            var d = Flat(target - transform.position);
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degreesPerSecond * Time.deltaTime);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
