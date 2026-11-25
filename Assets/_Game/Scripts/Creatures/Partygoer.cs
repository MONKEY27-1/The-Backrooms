using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// Wiki Entity 67. Hunts in tight packs because it can barely see: past a few metres it goes by sound. The pack
    /// shares what any of them notices; the nearest comes straight for you, the rest cut round to where you're heading.
    /// Slower than a sprint. Keep something solid between you, stay quiet, or set one alight with a thrown flare and
    /// the whole pack breaks off. Caught once is the end: the arms latch on and the chest opens.
    /// States: Stalk (the pack wandering) -> Search (a noise, a lost trail) -> Chase -> Grab; Numb (on fire), Circle (fleeing fire), Leave.
    public sealed class Partygoer : Creature
    {
        const float WanderSpeed = 1.3f, SearchSpeed = 2.3f, ChaseSpeed = 3.7f, FleeSpeed = 3.6f;
        const float Sight = 9f, ChaseSight = 12f, CatchDistance = 1.5f, PackRange = 25f;
        const float BurnTime = 7f, ScareTime = 22f;

        /// The pack (shared list); they keep each other in sight.
        public List<Partygoer> Pack;
        /// Came for a trap or on a roaming hunt: gives up and goes after this long with nothing to chase. 0 = lives here.
        public float Patience;

        PartygoerLook look;
        AudioSource cry;
        Vector3 goal, lastSeen, fire;
        float lostFor, nextGoal, idleFor, nextWhisper;
        bool done;
        static float nextLure;

        protected override void Begin(CreatureState start)
        {
            Pack = new List<Partygoer> { this };
            look = PartygoerLook.Build(transform);
            cry = gameObject.AddComponent<AudioSource>();
            cry.spatialBlend = 1f;
            cry.maxDistance = 35f;
            cry.rolloffMode = AudioRolloffMode.Linear;
            goal = transform.position;
            nextWhisper = Time.time + Random.Range(8f, 20f);
            Set(CreatureState.Stalk);
        }

        Partygoer Leader => Pack.Find(p => p != null && p.State != CreatureState.Numb && p.State != CreatureState.Leave);

        protected override void Entered(CreatureState s)
        {
            if (look != null) look.State = s;
            if (s == CreatureState.Chase) { lostFor = 0f; idleFor = 0f; Say(Audio.ProceduralAudio.Laughter(), 0.75f, 0.6f); UI.Captions.Say("[laughter, close]"); }
            if (s == CreatureState.Numb) { Say(Audio.CreatureSounds.Maul(), 1.7f, 1f); Say(Audio.ProceduralAudio.Hiss(), 0.8f, 0.8f); UI.Captions.Say("[an inhuman scream]"); }
            if (s == CreatureState.Search || s == CreatureState.Numb || s == CreatureState.Circle || s == CreatureState.Leave) nextGoal = 0f;   // pick somewhere new at once
        }

        void Say(AudioClip clip, float pitch, float volume)
        {
            cry.pitch = pitch;
            cry.PlayOneShot(clip, volume * Meta.AudioMix.Creatures);
        }

        bool Busy => State == CreatureState.Chase || State == CreatureState.Grab || State == CreatureState.Numb || State == CreatureState.Circle || State == CreatureState.Leave;

        public override void Hear(Vector3 where, float radius)
        {
            if (Busy || Vector3.Distance(where, transform.position) > radius) return;
            lastSeen = where;
            if (State != CreatureState.Search) Set(CreatureState.Search);
            else { stateTime = 0f; nextGoal = 0f; }
        }

        /// A packmate found someone.
        void Alert(PlayerAgent prey)
        {
            if (Busy) return;
            Target = prey;
            lastSeen = prey.Position;
            Set(CreatureState.Chase);
        }

        /// Set alight by a flare: it runs screaming, and the pack breaks off.
        public void Ignite(Vector3 from)
        {
            if (State == CreatureState.Numb || State == CreatureState.Leave || State == CreatureState.Grab) return;
            Wounds.Add(CreatureWoundType.Burn);
            fire = from;
            Set(CreatureState.Numb);
            foreach (var p in Pack)
                if (p != null && p != this && p.State != CreatureState.Numb && p.State != CreatureState.Grab && p.State != CreatureState.Leave) { p.fire = transform.position; p.Set(CreatureState.Circle); }
        }

        /// Nearsighted: only what's close, closer still if you keep low; a torch gives you away a little further.
        bool Notices(PlayerAgent who, float dist, bool sees)
        {
            if (!sees) return false;
            if (State == CreatureState.Chase) return dist < ChaseSight;
            float range = Sight * (who.Crouching ? 0.55f : 1f) + (who.FlashlightOn ? 3f : 0f);
            return dist < range && (dist < 2.5f || Vector3.Angle(transform.forward, Flat(who.Position - transform.position)) < 75f);
        }

        protected override void Think(float dt)
        {
            if (State == CreatureState.Grab) { Grabbing(); return; }
            if (Target == null || !Target.Alive || State != CreatureState.Chase) Target = Players.Nearest(transform.position);
            if (Target == null) return;

            if (State == CreatureState.Stalk || State == CreatureState.Search) Sense();

            var player = Target.Position;
            float dist = Flat(player - transform.position).magnitude;
            bool sees = CanSee(Target, dist);

            switch (State)
            {
                case CreatureState.Stalk:
                    Wander(dt);
                    idleFor += dt;
                    if (Patience > 0f && idleFor > Patience) { fire = player; Set(CreatureState.Leave); }
                    break;

                case CreatureState.Chase:
                    bool notices = Notices(Target, dist, sees);
                    if (notices) { lastSeen = player; lostFor = 0f; }
                    else
                    {
                        lostFor += dt;
                        // What one of them sees, all of them know.
                        foreach (var m in Pack)
                            if (m != null && m != this && m.State == CreatureState.Chase && m.Target == Target && m.lostFor <= 0f && Vector3.Distance(m.transform.position, transform.position) < PackRange) { lastSeen = m.lastSeen; lostFor = 0.01f; }
                    }
                    var to = Heading(dist);
                    MoveToward(to, ChaseSpeed, dt);
                    Face(notices ? player : to, 300f);
                    if (notices && dist < CatchDistance && Reaches() && !Pack.Exists(m => m != null && m.State == CreatureState.Grab && m.Target == Target)) { Set(CreatureState.Grab); break; }
                    if (lostFor > 3.5f) Set(CreatureState.Search);
                    break;

                case CreatureState.Search:
                    // Goes to the spot, then pokes about near it, whispering.
                    if (nextGoal <= 0f) { goal = lastSeen; nextGoal = Time.time + 12f; }
                    if (Flat(goal - transform.position).magnitude > 0.9f && Time.time < nextGoal) { MoveToward(goal, SearchSpeed, dt); Face(goal, 220f); }
                    else
                    {
                        goal = Near(lastSeen, 4f);
                        nextGoal = Time.time + 4f;
                    }
                    if (dist < 14f && Time.time > nextWhisper)
                    {
                        foreach (var m in Pack) if (m != null) m.nextWhisper = Time.time + Random.Range(14f, 24f);   // one voice at a time
                        world.Whisper(Target, Taunts[Random.Range(0, Taunts.Length)]);
                    }
                    if (stateTime > 16f) Set(CreatureState.Stalk);
                    break;

                case CreatureState.Numb:
                    Flee(FleeSpeed + 0.8f, dt);
                    if (stateTime > BurnTime) Set(CreatureState.Leave);
                    break;

                case CreatureState.Circle:
                    Flee(FleeSpeed, dt);
                    if (stateTime > ScareTime) Set(CreatureState.Stalk);
                    break;

                case CreatureState.Leave:
                    Flee(2f, dt);
                    if ((stateTime > 3f && !InPlayerView()) || stateTime > 20f) Destroy(gameObject);
                    break;
            }

            if (Net.Coop.Active && (State == CreatureState.Stalk || State == CreatureState.Search) && Leader == this) CoopLure();
        }

        /// Eyes for what's close, ears for the rest.
        void Sense()
        {
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive || Mathf.Abs(p.Position.y - transform.position.y) > 2f) continue;
                float d = Flat(p.Position - transform.position).magnitude;
                if (Notices(p, d, CanSee(p, d)))
                {
                    Alert(p);
                    foreach (var m in Pack) if (m != null && m != this && Vector3.Distance(m.transform.position, transform.position) < PackRange) m.Alert(p);
                    return;
                }
                float pace = Flat(p.Velocity).magnitude;   // footsteps
                if (!p.Crouching && pace > 1f && d < (p.Running ? 15f : 6f) && (State == CreatureState.Stalk || Vector3.Distance(lastSeen, p.Position) > 2f)) Hear(p.Position, d + 2f);
            }
        }

        static readonly string[] Taunts = { "don't you want cake? =)", "we can hear you breathing =)", "come out, everyone's waiting =)", "you're our guest of honour =)" };

        void Wander(float dt)
        {
            var lead = Leader;
            if (lead == null || lead == this)
            {
                if (Flat(goal - transform.position).magnitude < 1f || Time.time > nextGoal)
                {
                    // Now and then toward whoever is nearest, otherwise anywhere.
                    goal = Random.value < 0.4f ? Near(Target.Position, 9f) : world.Grid?.RandomSpot(5, 20, null) ?? transform.position;
                    nextGoal = Time.time + 14f;
                }
                MoveToward(goal, WanderSpeed, dt);
                Face(goal, 120f);
                return;
            }
            // The rest keep to a loose knot round the one in front.
            var spot = lead.transform.position + Quaternion.Euler(0f, Pack.IndexOf(this) * 137f, 0f) * Vector3.forward * 1.7f;
            float gap = Flat(spot - transform.position).magnitude;
            if (gap > 4f) { MoveToward(lead.transform.position, SearchSpeed, dt); Face(lead.transform.position, 160f); }
            else if (gap > 0.6f) { MoveToward(spot, WanderSpeed * 1.2f, dt); Face(spot, 160f); }
            else Face(transform.position + lead.transform.forward, 60f);
        }

        /// The nearest one comes straight at you; the others head for where you're going, to either side.
        Vector3 Heading(float dist)
        {
            if (lostFor > 0.2f) return lastSeen;
            var nearest = this;
            float best = Flat(transform.position - lastSeen).magnitude;
            foreach (var m in Pack)
            {
                if (m == null || m == this || m.State != CreatureState.Chase) continue;
                float d = Flat(m.transform.position - lastSeen).magnitude;
                if (d < best) { best = d; nearest = m; }
            }
            if (nearest == this || dist < 4f) return lastSeen;
            var run = Flat(Target.Velocity);
            if (run.magnitude < 0.5f) run = Flat(lastSeen - nearest.transform.position);
            var dir = Quaternion.Euler(0f, Pack.IndexOf(this) % 2 == 0 ? 35f : -35f, 0f) * run.normalized;
            float reach = Physics.Raycast(lastSeen + Vector3.up, dir, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) && Players.Owns(hit.collider) == null ? Mathf.Max(0f, hit.distance - 0.7f) : 6f;
            return lastSeen + dir * reach;
        }

        /// Nothing solid at waist height between you (a table, a shelf): it can't reach over.
        bool Reaches()
        {
            var a = transform.position + Vector3.up * 0.7f;
            var b = Target.Position + Vector3.up * 0.7f;
            return !Physics.Linecast(a, b, out var hit, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) == Target;
        }

        Vector3 Near(Vector3 p, float r) => world.Grid?.RandomSpot(0, 60, q => Vector3.Distance(q, p) < r) ?? p;

        void Flee(float speed, float dt)
        {
            if (Time.time > nextGoal || Flat(goal - transform.position).magnitude < 1f)
            {
                nextGoal = Time.time + 3f;
                float here = Vector3.Distance(transform.position, fire);
                goal = world.Grid?.RandomSpot(0, 60, q => Vector3.Distance(q, fire) > here + 3f) ?? transform.position + Flat(transform.position - fire).normalized * 4f;
            }
            MoveToward(goal, speed, dt);
            Face(goal, 360f);
        }

        void Grabbing()
        {
            var prey = Target;
            if (prey != null) Face(prey.Position, 900f);
            if (!done && prey != null)
            {
                done = true;
                Execute(prey);   // both kills run everywhere the victim has a screen, not just on the owner
            }
            if (stateTime > 5f) { done = false; Set(CreatureState.Stalk); }
        }

        /// Co-op: someone off on their own hears a friend calling them, from where the pack is.
        void CoopLure()
        {
            if (Time.time < nextLure) return;
            nextLure = Time.time + Random.Range(35f, 70f);
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive) continue;
                PlayerAgent friend = null;
                bool alone = true;
                foreach (var o in Players.All)
                    if (o != null && o != p && o.Alive) { friend = o; if (Vector3.Distance(o.Position, p.Position) < 12f) alone = false; }
                float d = Vector3.Distance(p.Position, transform.position);
                if (!alone || friend == null || d < 8f || d > 45f) continue;
                string name = friend.TryGetComponent(out Net.CoopAvatar a) && !string.IsNullOrEmpty(a.Nick) ? a.Nick : Net.Coop.PlayerName;
                world.Whisper(p, name + ": over here. I found the way out.", transform.position);
                return;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => nextLure = 0f;
    }
}
