using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// Wiki Entity 9, all of its kinds in one brain:
    /// - Faceling / FacelingChild: people with no faces living out routines they don't understand, in small groups
    ///   (a walker pacing its route, a worker at a dead desk, a waver). Harmless left alone. Bump one, shine your
    ///   light in its face, or crowd it and the group turns on you (weak hits, easy to outrun); touch a child and
    ///   every adult nearby comes. They calm down after a while.
    /// - EvolvedFaceling (Level 3 canon): malformed faces, pipes, always hostile. Short sight and good hearing;
    ///   a hit takes a quarter of your health.
    /// - ShadowFaceling: a black shape floating in the dark, drawn to your flashlight. Shine it straight in its face
    ///   and it startles away; let it reach you in the dark and it tears at you. Light drives it off.
    public sealed class Faceling : Creature
    {
        /// Picks this one's clothes and colours (co-op guests build the same).
        public int Variant;
        /// The others it came with.
        public List<Faceling> Group;
        public Faceling Parent;   // a child's adult

        FacelingLook look;
        int routine;
        Vector3 home, routeEnd, lastSeen;
        bool outbound = true;
        float glare, crowd, nextHit, nextWave, lostFor, glow, lastHit = -99f;
        PlayerAgent angryAt;

        bool Child => Kind == CreatureKind.FacelingChild;
        bool Evolved => Kind == CreatureKind.EvolvedFaceling;
        bool Shadow => Kind == CreatureKind.ShadowFaceling;
        Vector3 HeadPoint => transform.position + Vector3.up * (Child ? 1.05f : 1.65f);

        protected override void Begin(CreatureState start)
        {
            if (Variant == 0) Variant = Random.Range(1, int.MaxValue);
            look = FacelingLook.Build(transform, Kind, Variant);
            home = transform.position;
            routine = Child ? 0 : Random.Range(0, 3);
            look.Routine = routine;
            if (routine == 0) routeEnd = FarthestFree(8f);
            else if (routine == 1) routeEnd = NearestWall();
            voice.clip = null;
            Set(Evolved ? CreatureState.Stalk : CreatureState.Watch);
        }

        protected override void Entered(CreatureState s)
        {
            if (look != null) look.State = s;
            if (s == CreatureState.Chase && !Evolved && !Shadow) UI.Captions.Say(Child ? "[a child's shrill, wordless wail]" : "[an angry, wordless moan]");
        }

        Vector3 FarthestFree(float max)
        {
            Vector3 best = transform.position;
            float bestLen = 0f;
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                float len = Physics.Raycast(transform.position + Vector3.up, d, out var hit, max, ~0, QueryTriggerInteraction.Ignore) ? hit.distance - 0.8f : max;
                if (len > bestLen) { bestLen = len; best = transform.position + d * len; }
            }
            return best;
        }

        Vector3 NearestWall()
        {
            Vector3 best = transform.position;
            float bestLen = 99f;
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (Physics.Raycast(transform.position + Vector3.up, d, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) && hit.distance < bestLen)
                { bestLen = hit.distance; best = transform.position + d * Mathf.Max(0f, hit.distance - 0.6f); }
            return best;
        }

        /// Turn the whole group (within 12 m) on this player.
        void Anger(PlayerAgent who)
        {
            if (Group != null)
                foreach (var f in Group)
                    if (f != null && !f.Child && f.State != CreatureState.Chase && Vector3.Distance(f.transform.position, transform.position) < 12f) f.Provoked(who);
            Provoked(who);
        }

        void Provoked(PlayerAgent who)
        {
            if (State == CreatureState.Chase || State == CreatureState.Leave) return;
            angryAt = who;
            Target = who;
            Set(CreatureState.Chase);
        }

        protected override void Think(float dt)
        {
            if (Shadow) { ThinkShadow(dt); return; }
            if (Evolved) { ThinkEvolved(dt); return; }

            switch (State)
            {
                case CreatureState.Watch:
                    Routine(dt);
                    // What sets them off: walking into one, a light in the face, standing over them too long.
                    foreach (var p in Players.All)
                    {
                        if (p == null || !p.Alive) continue;
                        float d = Flat(p.Position - transform.position).magnitude;
                        bool lit = p.FlashlightOn && d < 7f && Vector3.Angle(p.Forward, HeadPoint - p.Eyes) < 12f && p.Sees(HeadPoint);
                        glare = lit ? glare + dt : Mathf.Max(0f, glare - dt);
                        crowd = d < 1.6f ? crowd + dt : Mathf.Max(0f, crowd - dt * 0.5f);
                        if (d < (Child ? 1.0f : 0.8f) || glare > 1.2f || crowd > 4f)
                        {
                            if (Child && Parent != null) Parent.Anger(p);   // touch a child and the adults come
                            Anger(p);
                            break;
                        }
                    }
                    if (life > 300f && !InPlayerView()) Set(CreatureState.Leave);
                    break;

                case CreatureState.Chase:
                    if (angryAt == null || !angryAt.Alive) { Set(CreatureState.Watch); break; }
                    float dist = Flat(angryAt.Position - transform.position).magnitude;
                    if (dist > 0.85f) MoveToward(angryAt.Position, Child ? 2.6f : 3.3f, dt);   // up close, not on top of you
                    Face(angryAt.Position, 400f);
                    // The group takes turns: weak blows, never all at once.
                    bool turn = Group == null || !Group.Exists(f => f != null && f != this && Time.time - f.lastHit < 1.0f);
                    if (dist < 1.1f && Time.time > nextHit && turn)
                    {
                        nextHit = Time.time + 1.3f;
                        lastHit = Time.time;
                        angryAt.Hurt(Child ? 0.05f : 0.1f, false, this);   // weak: a shove, a scratch
                        UI.Captions.Say(Child ? "[it scratches at you]" : "[it strikes you]");
                    }
                    // They give up on you after a while, or once you're well away.
                    if (stateTime > 14f || dist > 14f) { glare = crowd = 0f; Set(CreatureState.Watch); }
                    break;

                case CreatureState.Leave:
                    MoveToward(home + (transform.position - home).normalized * 20f, 1.0f, dt, false);
                    if ((stateTime > 3f && !InPlayerView()) || stateTime > 20f) Destroy(gameObject);
                    break;
            }
        }

        void Routine(float dt)
        {
            look.Waving = false;
            if (Child && Parent != null)
            {
                var near = Parent.transform.position + Parent.transform.right * 0.7f;   // keeps beside its adult
                if (Flat(near - transform.position).magnitude > 0.4f) MoveToward(near, 1.4f, dt, false);
                Face(Parent.transform.position + Parent.transform.forward * 3f, 120f);
                return;
            }
            switch (routine)
            {
                case 0:   // pacing its route, again and again
                    var to = outbound ? routeEnd : home;
                    if (Flat(to - transform.position).magnitude < 0.3f) outbound = !outbound;
                    else { MoveToward(to, 1.0f, dt, false); Face(to, 120f); }
                    break;
                case 1:   // at its "desk": facing the wall, typing on nothing
                    if (Flat(routeEnd - transform.position).magnitude > 0.2f) { MoveToward(routeEnd, 1.0f, dt, false); Face(routeEnd, 120f); }
                    else Face(routeEnd + (routeEnd - home).normalized, 90f);
                    break;
                default:  // waves at whoever it sees
                    var p = Players.Nearest(transform.position);
                    if (p != null && Flat(p.Position - transform.position).magnitude < 10f && CanSee(p, 10f))
                    {
                        Face(p.Position, 90f);
                        if (Time.time > nextWave) nextWave = Time.time + Random.Range(6f, 10f);
                        look.Waving = Time.time > nextWave - 2f;
                    }
                    break;
            }
        }

        // ---------- Evolved (Level 3) ----------

        void ThinkEvolved(float dt)
        {
            if (Target == null || !Target.Alive || State == CreatureState.Stalk) Target = Players.Nearest(transform.position);
            if (Target == null) return;
            var player = Target.Position;
            float dist = Flat(player - transform.position).magnitude;
            bool sees = dist < 8f && CanSee(Target, dist);                     // short-sighted
            float speed = new Vector2(Target.Velocity.x, Target.Velocity.z).magnitude;
            bool hears = (Target.Running && dist < 12f) || (speed > 1.8f && !Target.Crouching && dist < 3f);
            if (sees || hears) lastSeen = player;
            switch (State)
            {
                case CreatureState.Stalk:
                    if (sees || hears) { AlertEvolved(Target); break; }
                    MoveToward(player, 1.2f, dt);
                    if (life > 180f) Set(CreatureState.Leave);
                    break;
                case CreatureState.Chase:
                    lostFor = sees || hears ? 0f : lostFor + dt;
                    MoveToward(lastSeen, 4.1f, dt);
                    Face(lastSeen, 400f);
                    if (sees && dist < 1.3f) { Set(CreatureState.Lunge); break; }
                    if (lostFor > 6f) Set(CreatureState.Search);
                    break;
                case CreatureState.Lunge:   // the swing
                    Face(player, 400f);
                    if (stateTime > 0.35f && Time.time > nextHit)
                    {
                        nextHit = Time.time + 2f;
                        if (dist < 1.6f) { Target.Hurt(0.25f, true, this); UI.Captions.Say("[a pipe cracks against you]"); }
                    }
                    if (stateTime > 0.6f) Set(CreatureState.Circle);
                    break;
                case CreatureState.Circle:   // steps back before the next swing
                    MoveToward(transform.position - Flat(player - transform.position).normalized * 2f, 2f, dt);
                    if (stateTime > 1.5f) Set(CreatureState.Chase);
                    break;
                case CreatureState.Search:
                    if (sees || hears) { Set(CreatureState.Chase); break; }
                    if (Flat(lastSeen - transform.position).magnitude > 0.8f) MoveToward(lastSeen, 2f, dt);
                    else transform.Rotate(0f, 60f * dt, 0f);
                    if (stateTime > 15f) Set(CreatureState.Leave);
                    break;
                case CreatureState.Leave:
                    MoveToward(transform.position - Flat(player - transform.position).normalized * 3f, 1.5f, dt, false);
                    if ((stateTime > 3f && !InPlayerView()) || stateTime > 20f) Destroy(gameObject);
                    break;
            }
        }

        void AlertEvolved(PlayerAgent who)
        {
            UI.Captions.Say("[guttural shouting, and pipes scraping]");
            if (Group != null)
                foreach (var f in Group)
                    if (f != null && f.State == CreatureState.Stalk) { f.Target = who; f.lastSeen = who.Position; f.Set(CreatureState.Chase); }
            Target = who; lastSeen = who.Position;
            if (State != CreatureState.Chase) Set(CreatureState.Chase);
        }

        // ---------- Shadow ----------

        void ThinkShadow(float dt)
        {
            if (State == CreatureState.Leave)
            {
                MoveToward(transform.position - Flat(lastSeen - transform.position).normalized * 4f, 6f, dt, false);
                if (stateTime > 1.5f) Destroy(gameObject);
                return;
            }
            if (!world.DarkAt(transform.position + Vector3.up)) { Set(CreatureState.Leave); return; }   // light drives it off
            var p = Players.Nearest(transform.position);
            if (p == null) return;
            lastSeen = p.Position;
            float d = Flat(p.Position - transform.position).magnitude;
            bool lit = p.FlashlightOn && d < 12f && Vector3.Angle(p.Forward, HeadPoint - p.Eyes) < 10f && p.Sees(HeadPoint);
            glare = lit ? glare + dt : 0f;
            if (glare > 0.35f) { UI.Captions.Say("[something startles and flits away]"); Set(CreatureState.Leave); return; }
            // Drawn to a light; otherwise it drifts slowly toward whoever is near.
            bool torch = p.FlashlightOn && d < 18f && p.Sees(HeadPoint);
            if (torch || d < 12f) MoveToward(p.Position, torch ? 1.8f : 0.8f, dt);
            Face(p.Position, 90f);
            if (d < 1.0f && Time.time > nextHit)
            {
                nextHit = Time.time + 2f;
                p.Hurt(0.2f, true, this);
                UI.Captions.Say("[cold hands tear at you in the dark]");
            }
            if (life > 120f && !InPlayerView()) Destroy(gameObject);
        }
    }
}
