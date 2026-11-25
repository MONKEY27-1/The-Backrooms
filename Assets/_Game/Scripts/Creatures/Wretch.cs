using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// Wiki Entity 15: what people become when the Backrooms wear them down. You judge one by its voice: a low gurgle is
    /// a slow, strong, stupid one; a high chitter is quick, opens doors and cuts you off. About one in eight sounds wrong.
    /// Hordes act as one. Some lie like dead bodies in the dark until you've gone past. Boards and doors don't hold them.
    /// Claws take a quarter of your health and their sludge can start the Cycle in you (PlayerStatus.Infection).
    /// The mass (WretchMass) is slow, sees all round, and sprays sludge.
    /// States: Lurk (playing dead), Stalk (wandering) -> Chase -> Lunge, Break (a barrier), Search, Leave.
    public sealed class Wretch : Creature
    {
        const float HitDamage = 0.25f, SlamDamage = 0.4f, InfectChance = 0.35f, BreakTime = 3.5f, SprayRange = 5.5f;

        /// The horde (shared list); what one knows, all know.
        public List<Wretch> Horde;
        /// How this one looks and what it is, for co-op too: bit 0 quick, bit 1 high voice, the rest is its looks.
        public int Variant { get; private set; }

        WretchLook look;
        Vector3 goal, lastSeen;
        float lostFor, nextGoal, nextHit, lastHit = -99f, nextSpray, idleFor, closest = 99f, nextBarrier;
        Component barrier;
        bool struck;

        bool Mass => Kind == CreatureKind.WretchMass;
        public bool Quick => (Variant & 1) != 0;

        float Wander => Mass ? 0.6f : Quick ? 1.5f : 0.8f;
        float Speed => Mass ? 1.2f : Quick ? 4.1f : 1.8f;
        float Reach => Mass ? 2.2f : 1.4f;

        protected override void Begin(CreatureState start)
        {
            Horde = new List<Wretch> { this };
            bool quick = !Mass && Random.value < 0.35f;
            bool high = quick != (Random.value < 0.125f);   // one in eight sounds like the other sort
            Variant = Random.Range(1, 1 << 20) << 2 | (quick ? 1 : 0) | (high ? 2 : 0);
            look = WretchLook.Build(transform, Kind, Variant);
            goal = lastSeen = transform.position;
            voice.clip = Audio.CreatureSounds.Growl();
            voice.pitch = WretchLook.VoicePitch(Kind, Variant);
            voice.maxDistance = 24f;
            voice.volume = 0f;
            voice.Play();
            Set(start == CreatureState.Lurk ? CreatureState.Lurk : CreatureState.Stalk);
        }

        protected override void Entered(CreatureState s)
        {
            if (look != null) look.State = s;
            if (s == CreatureState.Chase) { lostFor = 0f; idleFor = 0f; UI.Captions.Say((Variant & 2) != 0 ? "[a high, wet chittering]" : "[a low gurgling]"); }
            if (s == CreatureState.Lunge) struck = false;
            if (s == CreatureState.Search) nextGoal = 0f;
        }

        bool Busy => State == CreatureState.Chase || State == CreatureState.Lunge || State == CreatureState.Break || State == CreatureState.Leave;

        public override void Hear(Vector3 where, float radius)
        {
            float d = Vector3.Distance(where, transform.position);
            if (Busy || d > radius) return;
            lastSeen = where;
            if (State == CreatureState.Lurk) { if (d < 6f) Rise(Players.Nearest(transform.position)); }
            else if (State != CreatureState.Search) Set(CreatureState.Search);
            else { stateTime = 0f; nextGoal = 0f; }
        }

        /// It was lying there. It isn't any more, and neither are the others.
        void Rise(PlayerAgent prey)
        {
            if (prey == null) return;
            if (State == CreatureState.Lurk) UI.Captions.Say("[something gets up behind you]");
            foreach (var w in Horde)
                if (w != null && w.State == CreatureState.Lurk && Vector3.Distance(w.transform.position, transform.position) < 12f) w.Alert(prey);
            Alert(prey);
        }

        void Alert(PlayerAgent prey)
        {
            if (Busy) return;
            Target = prey;
            lastSeen = prey.Position;
            Set(CreatureState.Chase);
        }

        bool Notices(PlayerAgent who, float dist)
        {
            float range = (Mass ? 14f : 12f) * (who.Crouching ? 0.6f : 1f) + (who.FlashlightOn ? 4f : 0f);
            if (dist > (State == CreatureState.Chase ? 18f : range) || !CanSee(who, dist)) return false;
            return State == CreatureState.Chase || Mass || dist < 2f || Vector3.Angle(transform.forward, Flat(who.Position - transform.position)) < 80f;
        }

        protected override void Think(float dt)
        {
            if (Target == null || !Target.Alive || !Busy) Target = Players.Nearest(transform.position);
            if (Target == null) return;
            if (State == CreatureState.Lurk) { PlayDead(); return; }
            if (State == CreatureState.Stalk || State == CreatureState.Search) Sense();

            var player = Target.Position;
            float dist = Flat(player - transform.position).magnitude;
            bool sees = Notices(Target, dist);

            switch (State)
            {
                case CreatureState.Stalk:
                    Roam(dt);
                    idleFor += dt;
                    if (idleFor > 150f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Chase:
                    if (sees) { lastSeen = player; lostFor = 0f; }
                    else
                    {
                        lostFor += dt;
                        foreach (var w in Horde)
                            if (w != null && w != this && w.State == CreatureState.Chase && w.lostFor <= 0f && w.Target == Target && Vector3.Distance(w.transform.position, transform.position) < 30f) { lastSeen = w.lastSeen; lostFor = 0.01f; }
                    }
                    if (FindBarrier()) break;
                    var to = Heading(dist);
                    if (dist > Reach * 0.6f || !sees) MoveToward(to, Speed, dt);
                    Face(sees ? player : to, Mass ? 60f : Quick ? 400f : 160f);
                    if (Mass && sees && dist > Reach && dist < SprayRange - 0.5f && Time.time > nextSpray) { nextSpray = Time.time + 5f; Set(CreatureState.Lunge); break; }
                    bool turn = !Horde.Exists(w => w != null && w != this && Time.time - w.lastHit < 0.8f);
                    if (sees && dist < Reach && Time.time > nextHit && turn) { lastHit = Time.time; Set(CreatureState.Lunge); break; }
                    if (lostFor > 6f) Set(CreatureState.Search);
                    break;

                case CreatureState.Lunge:
                    Face(player, 200f);
                    if (!struck && stateTime > (Mass ? 0.7f : Quick ? 0.3f : 0.55f)) { struck = true; Strike(dist); }
                    if (stateTime > (Mass ? 1.6f : 1f)) { nextHit = Time.time + (Quick ? 0.7f : 1.2f); Set(CreatureState.Chase); }
                    break;

                case CreatureState.Break:
                    if (barrier == null) { Set(CreatureState.Chase); break; }
                    Face(barrier.transform.position, 200f);
                    if (Vector3.Distance(Flat(barrier.transform.position), Flat(transform.position)) > 1.5f && stateTime < 6f) { MoveToward(barrier.transform.position, Speed, dt, false); if (stateTime > 2f) nextBarrier = 0f; }
                    else if (stateTime > BreakTime) Smash();
                    else if (Mathf.Repeat(stateTime, 0.7f) < dt) Bang(0.6f);
                    break;

                case CreatureState.Search:
                    if (nextGoal <= 0f) { goal = lastSeen; nextGoal = Time.time + 14f; }
                    if (Flat(goal - transform.position).magnitude > 0.9f && Time.time < nextGoal) { MoveToward(goal, Speed * 0.7f, dt); Face(goal, 160f); }
                    else { goal = Near(lastSeen, 5f); nextGoal = Time.time + 5f; }
                    if (stateTime > 18f) Set(CreatureState.Stalk);
                    break;

                case CreatureState.Leave:
                    MoveToward(goal, Wander, dt);
                    if (Flat(goal - transform.position).magnitude < 1f || nextGoal < Time.time) { nextGoal = Time.time + 8f; goal = world.Grid?.RandomSpot(10, 60, q => Vector3.Distance(q, player) > dist) ?? goal; }
                    if ((stateTime > 3f && !InPlayerView()) || stateTime > 40f) Destroy(gameObject);
                    break;
            }
            float loud = State == CreatureState.Chase || State == CreatureState.Lunge ? 0.6f : 0.28f;
            voice.volume = Mathf.MoveTowards(voice.volume, loud * Meta.AudioMix.Creatures, dt * 0.5f);
        }

        /// A body on the floor. Walk past and it waits until you've turned your back; touch it, light it for long, or make a noise and it doesn't wait.
        void PlayDead()
        {
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive || Mathf.Abs(p.Position.y - transform.position.y) > 2f) continue;
                float d = Flat(p.Position - transform.position).magnitude;
                var head = transform.position + Vector3.up * 0.3f;
                if (p == Target) closest = Mathf.Min(closest, d);
                bool watched = p.Sees(head);
                if (d < 0.9f || (d < 8f && p.Running && d < 4f) || (p == Target && closest < 4f && d > closest + 1.2f && !watched)) { Rise(p); return; }
                if (p.FlashlightOn && d < 6f && watched && Vector3.Angle(p.Forward, head - p.Eyes) < 10f) lostFor += Time.deltaTime;   // a light held on its face
                if (lostFor > 3f) { Rise(p); return; }
            }
            if (life > 400f && !InPlayerView()) Destroy(gameObject);
        }

        void Sense()
        {
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive || Mathf.Abs(p.Position.y - transform.position.y) > 3f) continue;
                float d = Flat(p.Position - transform.position).magnitude;
                if (Notices(p, d))
                {
                    Alert(p);
                    foreach (var w in Horde) if (w != null && w != this && w.State != CreatureState.Lurk && Vector3.Distance(w.transform.position, transform.position) < 30f) w.Alert(p);
                    return;
                }
                if (!p.Crouching && Flat(p.Velocity).magnitude > 1f && d < (p.Running ? 12f : 4f) && (State == CreatureState.Stalk || Vector3.Distance(lastSeen, p.Position) > 2f)) Hear(p.Position, d + 2f);
            }
        }

        Vector3 Near(Vector3 p, float r) => world.Grid?.RandomSpot(0, 60, q => Vector3.Distance(q, p) < r) ?? p;

        void Roam(float dt)
        {
            var lead = Horde.Find(w => w != null && w.State == CreatureState.Stalk);
            if (lead == this || lead == null)
            {
                if (Flat(goal - transform.position).magnitude < 1f || Time.time > nextGoal)
                {
                    goal = Random.value < 0.35f ? Near(Target.Position, 10f) : world.Grid?.RandomSpot(4, 20, null) ?? transform.position;
                    nextGoal = Time.time + 16f;
                }
                MoveToward(goal, Wander, dt);
                Face(goal, 90f);
                return;
            }
            var spot = lead.transform.position + Quaternion.Euler(0f, Horde.IndexOf(this) * 137f, 0f) * Vector3.forward * 1.8f;
            float gap = Flat(spot - transform.position).magnitude;
            if (gap > 4f) { MoveToward(lead.transform.position, Mathf.Max(Wander, 1.3f), dt); Face(lead.transform.position, 120f); }
            else if (gap > 0.7f) { MoveToward(spot, Mathf.Max(Wander, 0.9f), dt); Face(spot, 120f); }
        }

        /// The quick ones swing round to where you're heading; the rest come straight.
        Vector3 Heading(float dist)
        {
            if (!Quick || dist < 4f || lostFor > 0.2f) return lastSeen;
            var run = Flat(Target.Velocity);
            if (run.magnitude < 0.8f) return lastSeen;
            var dir = Quaternion.Euler(0f, Horde.IndexOf(this) % 2 == 0 ? 30f : -30f, 0f) * run.normalized;
            float reach = Physics.Raycast(lastSeen + Vector3.up, dir, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore) && Players.Owns(hit.collider) == null ? Mathf.Max(0f, hit.distance - 0.7f) : 5f;
            return lastSeen + dir * reach;
        }

        void Strike(float dist)
        {
            if (Mass && dist > Reach)   // the spray
            {
                UI.Captions.Say("[a wet spray]");
                if (dist < SprayRange && Vector3.Angle(transform.forward, Flat(Target.Position - transform.position)) < 28f && CanSee(Target, dist))
                {
                    Target.Infect();
                    Target.Hurt(0.08f, false, this);
                }
                return;
            }
            if (dist > Reach + 0.35f || Mathf.Abs(Target.Position.y - transform.position.y) > 1.6f || !Clear()) return;
            if (Random.value < InfectChance) Target.Infect();   // before the blow: if that kills you, you wake up clean
            UI.Captions.Say(Mass ? "[it falls on you]" : "[claws rake you]");
            Target.Hurt(Mass ? SlamDamage : HitDamage, Mass, this);
        }

        bool Clear() => !Physics.Linecast(transform.position + Vector3.up, Target.Position + Vector3.up, out var hit, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) == Target;

        // ---------- boards and doors ----------

        /// Something breakable straight between it and you, close by.
        bool FindBarrier()
        {
            if (Time.time < nextBarrier) return false;
            nextBarrier = Time.time + 0.4f;
            var from = transform.position + Vector3.up * 1.1f;
            if (!Physics.Linecast(from, lastSeen + Vector3.up * 1.1f, out var hit, ~0, QueryTriggerInteraction.Ignore) || hit.distance > 5f) return false;
            var boards = hit.collider.GetComponentInParent<BoardedDoor>();
            if (boards != null && boards.Pryable && !boards.Crate && !boards.LockedDoor) barrier = boards;
            else
            {
                var door = hit.collider.GetComponentInParent<HotelDoor>();
                if (door == null || !door.Shut) return false;
                if (Quick && hit.distance < 1.6f) { door.Interact(); Rebuild(); return false; }   // it knows what a handle is for
                barrier = door;
            }
            Set(CreatureState.Break);
            return true;
        }

        void Bang(float volume)
        {
            AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Clang(), barrier.transform.position + Vector3.up, volume * Meta.AudioMix.Creatures);
            UI.Captions.Say("[something hammers at the door]");
        }

        void Smash()
        {
            Bang(1f);
            Wounds.Add(CreatureWoundType.Door);
            if (barrier is BoardedDoor b) BoardedDoor.Smashed?.Invoke(b);
            else if (barrier is HotelDoor d && d.Shut) d.Interact();   // shortcut: in co-op a hotel door opens on the owner's game only, as when a player opens one
            UI.Captions.Say("[wood splinters]");
            barrier = null;
            Set(CreatureState.Chase);
            Rebuild();
        }

        void Rebuild() => Invoke(nameof(RebuildNow), 0.8f);   // once it has swung open or is gone
        void RebuildNow() { if (Target != null) world?.Grid?.Build(Target.Position); }
    }
}
