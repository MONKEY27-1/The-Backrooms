using Backrooms.Player;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// Wiki Entity 3. It lives in the dark and only its grin and eyes show. It doesn't attack on sight:
    /// hold its gaze and back away slowly and it sinks back into the black. Panic (run), turn your back on it
    /// close by, make a loud noise, walk into it, or shine a light in its face, and it charges.
    /// A thrown glowstick draws it away; an Exterminator bottle sends it shrieking off for a long while.
    /// States: Lurk (waits in a dark room) / Stalk (blackout hunt, drifts closer) -> Stare -> Chase -> Search -> Leave,
    /// plus Lured and Numb.
    public sealed class Smiler : Creature
    {
        const float ChaseSpeed = 4.6f;           // a sprint (4.8) outruns it, but stamina runs out
        const float CatchDistance = 1.1f;
        const float StareRange = 13f;
        /// The dead lights a lurker brings with it (null in real darkness); they come back when it goes.
        public const float PocketRadius = 7.5f;
        public World.ChunkStreamer.DarkPocket Pocket;

        SmilerLook look;
        Vector3 lastSeen, numbFrom;
        float lostFor, backTurned, unseenTime;
        Lure lure;
        AudioSource cry;

        protected override void Begin(CreatureState start)
        {
            look = SmilerLook.Build(transform);
            voice.clip = Audio.ProceduralAudio.Static();
            voice.pitch = 0.45f;
            voice.volume = 0.2f * Meta.AudioMix.Creatures;
            voice.Play();
            cry = gameObject.AddComponent<AudioSource>();
            cry.spatialBlend = 1f;
            cry.maxDistance = 30f;
            cry.rolloffMode = AudioRolloffMode.Linear;
            Set(start == CreatureState.Lurk ? CreatureState.Lurk : CreatureState.Stalk);
        }

        public override void Hear(Vector3 where, float radius)
        {
            if (State == CreatureState.Leave || State == CreatureState.Numb || State == CreatureState.Chase) return;
            if (Vector3.Distance(where, transform.position) > radius) return;
            lastSeen = where;
            // A loud noise sets it off: straight at you if it can see you, otherwise toward the sound.
            if (Target != null && CanSee(Target, Flat(Target.Position - transform.position).magnitude)) Set(CreatureState.Chase);
            else Set(CreatureState.Search);
        }

        void OnDestroy() => World.ChunkStreamer.Instance?.RemoveDarkPocket(Pocket);

        /// An Exterminator broke near it.
        public void Numb(Vector3 from)
        {
            if (State == CreatureState.Numb) return;
            Wounds.Add(CreatureWoundType.Chemical);
            numbFrom = from;
            Set(CreatureState.Numb);
            world.Numbed(this);
        }

        protected override void Entered(CreatureState s)
        {
            if (look != null) look.State = s;
            if (s == CreatureState.Chase)
            {
                UI.Captions.Say("[a shriek of static]");
                cry.pitch = Random.Range(0.85f, 1.0f);
                cry.PlayOneShot(Audio.ProceduralAudio.Static(), 0.9f * Meta.AudioMix.Creatures);
                cry.PlayOneShot(Audio.ProceduralAudio.Laughter(), 0.35f * Meta.AudioMix.Creatures);
            }
            else if (s == CreatureState.Numb)
            {
                UI.Captions.Say("[something shrieks and scrambles away]");
                cry.pitch = 1.6f;
                cry.PlayOneShot(Audio.ProceduralAudio.Laughter(), 0.8f * Meta.AudioMix.Creatures);
            }
            else if (s == CreatureState.Stare) UI.Captions.Say("[a grin in the dark]");
        }

        protected override void Think(float dt)
        {
            if (State != CreatureState.Chase || Target == null || !Target.Alive) Target = Players.Nearest(transform.position);
            if (Target == null) return;
            var player = Target.Position;
            float dist = Flat(player - transform.position).magnitude;
            bool sees = CanSee(Target, dist);

            // In the light it can't stay.
            if (State != CreatureState.Leave && State != CreatureState.Numb && !world.DarkAt(transform.position + Vector3.up)) { Set(CreatureState.Leave); }

            // A light thrown nearby pulls it away (even mid-charge, if the light lands close to it).
            if (State == CreatureState.Lurk || State == CreatureState.Stalk || State == CreatureState.Stare || State == CreatureState.Search
                || (State == CreatureState.Chase && dist > 2.5f))
            {
                var near = Lure.Nearest(transform.position, State == CreatureState.Chase ? 9f : 20f);
                if (near != null) { lure = near; Set(CreatureState.Lured); }
            }

            switch (State)
            {
                case CreatureState.Lurk:
                    if (sees && dist < StareRange) { Set(CreatureState.Stare); break; }
                    if (Provoked(dist, sees, dt)) { lastSeen = player; Set(CreatureState.Chase); break; }
                    if (life > 240f && !InPlayerView()) Set(CreatureState.Leave);
                    break;

                case CreatureState.Stalk:
                    // Blackout hunt: drifts closer through the dark until it has you in sight.
                    if (Provoked(dist, sees, dt)) { lastSeen = player; Set(CreatureState.Chase); break; }
                    if (sees && dist < StareRange) { Set(CreatureState.Stare); break; }
                    MoveToward(player, 1.2f, dt);
                    Face(player);
                    if (stateTime > 50f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Stare:
                    Face(player, 60f);
                    if (Provoked(dist, sees, dt, true)) { lastSeen = player; Set(CreatureState.Chase); break; }
                    unseenTime = sees ? 0f : unseenTime + dt;
                    // You held its gaze and got away, or it lost interest: it sinks back into the dark.
                    if (dist > StareRange + 4f || unseenTime > 3f || stateTime > 25f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Chase:
                    if (sees) { lastSeen = player; lostFor = 0f; }
                    else lostFor += dt;
                    MoveToward(sees ? player : lastSeen, ChaseSpeed, dt);
                    Face(sees ? player : lastSeen);
                    if (dist < CatchDistance && sees) { Execute(Target); break; }
                    if (lostFor > 4f) Set(CreatureState.Search);
                    break;

                case CreatureState.Search:
                    if (Provoked(dist, sees, dt)) { lastSeen = player; Set(CreatureState.Chase); break; }
                    if (sees && dist < StareRange) { Set(CreatureState.Stare); break; }
                    if (Flat(lastSeen - transform.position).magnitude > 0.8f) MoveToward(lastSeen, 2.2f, dt);
                    else transform.Rotate(0f, 60f * dt, 0f);
                    if (stateTime > 12f) Set(CreatureState.Leave);
                    break;

                case CreatureState.Lured:
                    if (lure == null) { Set(CreatureState.Search); break; }
                    lastSeen = lure.transform.position;
                    if (Flat(lastSeen - transform.position).magnitude > 1.4f) MoveToward(lastSeen, 3.2f, dt);
                    Face(lastSeen, 180f);
                    break;

                case CreatureState.Numb:
                    var away = transform.position + Flat(transform.position - numbFrom).normalized * 6f;
                    MoveToward(away, 6.5f, dt, false);
                    if (stateTime > 2.5f) Destroy(gameObject);
                    break;

                case CreatureState.Leave:
                    voice.volume = Mathf.MoveTowards(voice.volume, 0f, dt * 0.3f);
                    if (stateTime > 1.6f) Destroy(gameObject);
                    break;
            }

            if (State == CreatureState.Chase) { voice.pitch = 0.75f; voice.volume = Mathf.MoveTowards(voice.volume, 0.6f * Meta.AudioMix.Creatures, dt); }
            else if (State != CreatureState.Leave) { voice.pitch = 0.45f; voice.volume = 0.2f * Meta.AudioMix.Creatures; }
        }

        /// Canon triggers. Anyone in range can set it off; whoever does becomes the prey.
        bool Provoked(float dist, bool sees, float dt, bool watchBack = false)
        {
            var head = transform.position + Vector3.up * 1.8f;
            bool turned = false;
            foreach (var who in Players.All)
            {
                if (who == null || !who.Alive) continue;
                float d = Flat(who.Position - transform.position).magnitude;
                bool seesWho = who == Target ? sees : CanSee(who, d);
                if (!seesWho && d > 2.5f) continue;
                bool hit = false;
                if (who.Running && d < 18f) hit = true;                                   // panic
                else if (d < 2.5f) hit = true;                                            // walked into it
                else if (who.FlashlightOn && d < 20f && Vector3.Angle(who.Forward, head - who.Eyes) < 16f) hit = true;   // light in its face
                else if (watchBack && d < 9f && Vector3.Angle(who.Forward, head - who.Eyes) > 75f) turned = true;   // turned your back on it, close by
                if (hit) { Target = who; return true; }
            }
            backTurned = turned ? backTurned + dt : 0f;
            return backTurned > 1.0f;
        }
    }
}
