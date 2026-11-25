using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// Wiki Entity 8. A starved human that runs on all fours, black hair over a mouth far too big. Hostile on sight,
    /// and they hunt in packs: the first to see you barks and the others come from wherever they are.
    /// Canon counter: look straight at one and it freezes, growling (longer with your light on it) — long enough to
    /// back off or break line of sight. It wears off and won't work again straight away, and running the moment
    /// you see one sets it off. It doesn't kill outright: it lunges, mauls you to the floor, backs off and circles.
    /// States: Stalk (prowling, growling) -> Chase -> Lunge -> Circle -> Chase ..., Held (stared down), Search, Leave.
    public sealed class Hound : Creature
    {
        const float ProwlSpeed = 1.5f, ChaseSpeed = 4.3f, LungeSpeed = 8.5f, CircleSpeed = 2.4f;
        const float SightRange = 16f, LungeRange = 3.2f, HitRange = 1.3f;
        public const float MaulDamage = 0.34f;   // three hits and you're done

        /// The pack this Hound runs with (shared list); one bark brings the rest.
        public List<Hound> Pack;

        HoundLook look;
        AudioSource bark;
        Vector3 lastSeen, lungeDir;
        float lostFor, nextStare, circleFor, circleSide, holdFor, lastMaul = -99f, nextSniff;
        bool hit;

        protected override void Begin(CreatureState start)
        {
            look = HoundLook.Build(transform);
            voice.clip = Audio.CreatureSounds.Growl();
            voice.pitch = Random.Range(0.85f, 1.1f);
            voice.volume = 0.25f * Meta.AudioMix.Creatures;
            voice.maxDistance = 18f;
            voice.Play();
            bark = gameObject.AddComponent<AudioSource>();
            bark.spatialBlend = 1f;
            bark.maxDistance = 45f;
            bark.rolloffMode = AudioRolloffMode.Linear;
            circleSide = Random.value < 0.5f ? -1f : 1f;
            Set(CreatureState.Stalk);
        }

        protected override void Entered(CreatureState s)
        {
            if (look != null) look.State = s;
            if (s == CreatureState.Lunge) { hit = false; bark.PlayOneShot(Audio.CreatureSounds.Maul(), 0.8f * Meta.AudioMix.Creatures); }
            if (s == CreatureState.Held) UI.Captions.Say("[a low growl]");
            if (s == CreatureState.Circle) circleFor = Random.Range(2.5f, 4.5f);
        }

        public override void Hear(Vector3 where, float radius)
        {
            if (State != CreatureState.Stalk && State != CreatureState.Search) return;
            if (Vector3.Distance(where, transform.position) > radius) return;
            lastSeen = where;
            Set(CreatureState.Search);
        }

        /// A packmate barked: come for them, from wherever you are.
        void Alert(Vector3 at, PlayerAgent prey)
        {
            if (State == CreatureState.Leave || State == CreatureState.Chase || State == CreatureState.Lunge || State == CreatureState.Circle) return;
            lastSeen = at;
            Target = prey;
            Set(CreatureState.Chase);
            lostFor = 0f;
        }

        void Bark(PlayerAgent prey)
        {
            UI.Captions.Say("[a bark, and others answer]");
            bark.pitch = Random.Range(0.9f, 1.1f);
            bark.PlayOneShot(Audio.CreatureSounds.Bark(), 1f * Meta.AudioMix.Creatures);
            if (Pack != null) foreach (var h in Pack) if (h != null && h != this) h.Alert(prey.Position, prey);
        }

        protected override void Think(float dt)
        {
            if (Target == null || !Target.Alive || State == CreatureState.Stalk) Target = Players.Nearest(transform.position);
            if (Target == null) return;
            var player = Target.Position;
            float dist = Flat(player - transform.position).magnitude;
            bool sees = CanSee(Target, dist);
            if (sees) lastSeen = player;

            // Tracking wounded prey by blood scent
            if (!sees && (State == CreatureState.Stalk || State == CreatureState.Search) && Time.time > nextSniff)
            {
                nextSniff = Time.time + 1.2f;
                if (Blood.FindTrailNear(transform.position, 18f, out var scentPoint, out var woundedPrey))
                {
                    lastSeen = scentPoint;
                    Target = woundedPrey;
                    if (Flat(scentPoint - transform.position).magnitude < 4f && Flat(woundedPrey.Position - transform.position).magnitude < 14f)
                    {
                        Bark(woundedPrey);
                        Set(CreatureState.Chase);
                    }
                }
            }

            // The stare-down: looked straight in the face (wider with a light on it) while it can see you.
            if (sees && dist < 14f && Time.time > nextStare && !Target.Running
                && (State == CreatureState.Chase || State == CreatureState.Circle || State == CreatureState.Stalk))
            {
                float angle = Vector3.Angle(Target.Forward, transform.position + Vector3.up * 0.6f - Target.Eyes);
                if (angle < (Target.FlashlightOn ? 14f : 9f))
                {
                    holdFor = Target.FlashlightOn ? 5f : 3f;
                    Set(CreatureState.Held);
                }
            }

            switch (State)
            {
                case CreatureState.Stalk:
                    // Prowls toward you, growling. Seen, or you bolt nearby: it barks and comes.
                    if ((sees && dist < SightRange) || (Target.Running && dist < 18f) || dist < 4f) { Bark(Target); Set(CreatureState.Chase); break; }
                    MoveToward(player, ProwlSpeed, dt);
                    Face(player, 180f);
                    if (life > 120f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Held:
                    Face(player, 90f);
                    voice.volume = 0.6f * Meta.AudioMix.Creatures;
                    // Holds while you keep looking; running breaks it at once (and so does looking away for a moment).
                    float a = Vector3.Angle(Target.Forward, transform.position + Vector3.up * 0.6f - Target.Eyes);
                    bool still = sees && a < 25f && !Target.Running;
                    if (!still || stateTime > holdFor)
                    {
                        nextStare = Time.time + 8f;   // it won't fall for it again straight away
                        Set(CreatureState.Chase);
                    }
                    break;

                case CreatureState.Chase:
                    lostFor = sees ? 0f : lostFor + dt;
                    MoveToward(sees ? player : lastSeen, ChaseSpeed, dt);
                    Face(sees ? player : lastSeen);
                    // A pack takes turns: nobody lunges within 3 s of a packmate's maul.
                    bool packWaits = Pack != null && Pack.Exists(h => h != null && Time.time - h.lastMaul < 3f);
                    if (sees && dist < LungeRange && !packWaits) { lungeDir = Flat(player - transform.position).normalized; Set(CreatureState.Lunge); break; }
                    if (lostFor > 5f) Set(CreatureState.Search);
                    break;

                case CreatureState.Lunge:
                    MoveToward(transform.position + lungeDir, LungeSpeed, dt, false);
                    if (!hit && dist < HitRange)
                    {
                        hit = true;
                        lastMaul = Time.time;
                        Target.Hurt(MaulDamage, true, this);
                        Target.LoseSanity(0.08f);
                    }
                    if (stateTime > 0.35f) Set(CreatureState.Circle);
                    break;

                case CreatureState.Circle:
                    // Backs off to a few metres and circles, then comes again.
                    var away = Flat(transform.position - player);
                    if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                    var side = Vector3.Cross(Vector3.up, away.normalized) * circleSide;
                    var want = player + away.normalized * 5.5f + side * 2.5f;
                    MoveToward(want, CircleSpeed + (dist < 4f ? 2f : 0f), dt);
                    Face(player, 240f);
                    if (stateTime > circleFor) Set(sees ? CreatureState.Chase : CreatureState.Search);
                    break;

                case CreatureState.Search:
                    if (sees && dist < SightRange) { Bark(Target); Set(CreatureState.Chase); break; }
                    if (Flat(lastSeen - transform.position).magnitude > 0.8f) MoveToward(lastSeen, 2.6f, dt);
                    else transform.Rotate(0f, 70f * dt, 0f);
                    if (stateTime > 15f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Leave:
                    MoveToward(transform.position - Flat(player - transform.position).normalized * 3f, 2f, dt, false);
                    voice.volume = Mathf.MoveTowards(voice.volume, 0f, dt * 0.2f);
                    if ((stateTime > 4f && !InPlayerView()) || stateTime > 20f) Destroy(gameObject);
                    break;
            }

            if (State != CreatureState.Held && State != CreatureState.Leave)
                voice.volume = (State == CreatureState.Stalk ? 0.25f : 0.45f) * Meta.AudioMix.Creatures;
        }
    }
}
