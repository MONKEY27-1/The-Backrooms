using Backrooms.Player;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// Wiki Entity 10. It wears a victim's skin and passes for a lost survivor (or, in co-op, for one of your
    /// friends). It comes up to you and follows. Walk away calmly and keep your distance and it loses interest
    /// and wanders off. Let it get behind you while you're alone and it sheds the skin and comes for you; if it
    /// catches you it tears you in half (checkpoint). Tells: it doesn't move right, and it bleeds clear.
    /// States: Disguise (approach / follow) -> Chase -> Grab, Search, Leave.
    public sealed class SkinStealer : Creature
    {
        const float ApproachSpeed = 1.3f, TrailSpeed = 2.4f, ChaseSpeed = 4.6f, CatchDistance = 1.2f;
        const float FollowGap = 3.5f, AloneRadius = 12f, BehindTime = 2.5f;

        /// Copying this friend's suit, or -1 for the wanderer.
        public int Suit = -1;

        SkinStealerLook look;
        AudioSource cry;
        Vector3 lastSeen;
        float interest = 1f, behind, lostFor;
        bool done, shed;

        protected override void Begin(CreatureState start)
        {
            // In co-op, now and then it wears a friend instead of a stranger.
            if (Net.Coop.Active && Random.value < 0.35f)
                foreach (var a in Net.CoopAvatar.All.Values)
                    if (a != null && !a.IsMine) { Suit = a.Suit; break; }
            look = SkinStealerLook.Build(transform, Suit);
            voice.clip = null;
            cry = gameObject.AddComponent<AudioSource>();
            cry.spatialBlend = 1f;
            cry.maxDistance = 30f;
            cry.rolloffMode = AudioRolloffMode.Linear;
            Set(CreatureState.Disguise);
        }

        protected override void Entered(CreatureState s)
        {
            if (look != null) look.State = s;
            if (s == CreatureState.Chase && !shed)
            {
                shed = true;
                UI.Captions.Say("[wet skin tearing]");
                cry.pitch = 0.6f;
                cry.PlayOneShot(Audio.CreatureSounds.Maul(), 0.9f * Meta.AudioMix.Creatures);
                cry.PlayOneShot(Audio.ProceduralAudio.Laughter(), 0.4f * Meta.AudioMix.Creatures);
            }
        }

        public override void Hear(Vector3 where, float radius)
        {
            if (State != CreatureState.Search || Vector3.Distance(where, transform.position) > radius) return;
            lastSeen = where;
            stateTime = 0f;
        }

        /// Nobody else alive near this player?
        static bool Alone(PlayerAgent who)
        {
            foreach (var o in Players.All)
                if (o != null && o != who && o.Alive && Vector3.Distance(o.Position, who.Position) < AloneRadius) return false;
            return true;
        }

        PlayerAgent PickPrey()
        {
            PlayerAgent best = null;
            float bestD = float.MaxValue;
            foreach (var p in Players.All)   // whoever is on their own, nearest first; anyone if nobody is
            {
                if (p == null || !p.Alive) continue;
                float d = Vector3.Distance(p.Position, transform.position) - (Alone(p) ? 100f : 0f);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        protected override void Think(float dt)
        {
            if (State == CreatureState.Disguise || Target == null || !Target.Alive) Target = PickPrey();
            if (Target == null) return;
            var player = Target.Position;
            float dist = Flat(player - transform.position).magnitude;
            bool sees = CanSee(Target, dist);
            if (sees) lastSeen = player;

            switch (State)
            {
                case CreatureState.Disguise:
                    Face(player, 400f);   // it turns its whole body to look at you, never just the head
                    var away = Flat(player - transform.position).normalized;
                    bool leaving = Vector3.Dot(Target.Velocity, away) > 0.6f;
                    if (dist > FollowGap) MoveToward(player, Target.Running ? TrailSpeed : ApproachSpeed, dt);
                    // Walking off calmly with distance between you: it gives up on you.
                    if (leaving && !Target.Running && dist > 6f) interest -= dt / 12f;
                    else if (dist > 14f) interest -= dt / 6f;
                    else interest = Mathf.Min(1f, interest + dt / 30f);
                    // Behind a lone player, close, for long enough: the skin comes off.
                    bool unseen = !Target.Sees(transform.position + Vector3.up * 1.5f);
                    behind = Alone(Target) && dist < 4.5f && unseen ? behind + dt : 0f;
                    if (behind > BehindTime) { lostFor = 0f; Set(CreatureState.Chase); break; }
                    if (interest <= 0f || life > 180f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Chase:
                    lostFor = sees ? 0f : lostFor + dt;
                    MoveToward(sees ? player : lastSeen, ChaseSpeed, dt);
                    Face(sees ? player : lastSeen);
                    if (sees && dist < CatchDistance) { Set(CreatureState.Grab); break; }
                    if (lostFor > 6f) Set(CreatureState.Search);
                    break;

                case CreatureState.Grab:
                    Face(player, 720f);
                    if (done) break;
                    done = true;
                    var prey = Target;
                    Execute(prey);   // local or synced: the victim sees the full kill before the checkpoint
                    break;

                case CreatureState.Search:
                    if (sees && dist < 14f) { Set(CreatureState.Chase); break; }
                    if (Flat(lastSeen - transform.position).magnitude > 0.8f) MoveToward(lastSeen, 2.6f, dt);
                    else transform.Rotate(0f, 80f * dt, 0f);
                    if (stateTime > 12f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Leave:
                    MoveToward(transform.position - Flat(player - transform.position).normalized * 3f, 1.1f, dt, false);
                    if ((stateTime > 3f && !InPlayerView()) || stateTime > 25f) Destroy(gameObject);
                    break;
            }
        }
    }
}
