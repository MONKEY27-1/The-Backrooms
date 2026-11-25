using Backrooms.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Creatures
{
    /// Wiki Entity 5: a knot of human limbs around a mouth. Blind (Level 1 canon): it only hears. Running, landing,
    /// talking, breaking things carry; walking slowly or crouching doesn't. It waits buried in the floor with its long
    /// limb sticking up, or crouched just round a corner, or crawls through the ceiling ducts and drops down where
    /// it heard you. Within reach its long limb hooks you and reels you in to the mouth while you bleed; mash
    /// Interact to tear free. Reach the mouth and you're eaten (checkpoint). No fighting it.
    /// States: Lurk (buried) / Stalk (corner ambush) / Vent (in the ceiling) -> Chase -> Grab -> Search -> Lurk ... Leave.
    public sealed class Clump : Creature
    {
        const float CrawlSpeed = 4.6f, VentSpeed = 3f, Reach = 2.6f, MouthDistance = 0.7f, EatTime = 3.5f;
        const float BleedPerSecond = 0.08f;

        ClumpLook look;
        AudioSource scrape;
        Vector3 heardAt;
        float heardTime = -99f, grabCooldown, struggle, need, reel;
        PlayerAgent held;
        readonly System.Collections.Generic.Dictionary<PlayerAgent, float> lastNoise = new System.Collections.Generic.Dictionary<PlayerAgent, float>();

        protected override void Begin(CreatureState start)
        {
            look = ClumpLook.Build(transform);
            voice.clip = Audio.CreatureSounds.Scuttle();
            voice.loop = true;
            voice.volume = 0f;
            voice.maxDistance = 20f;
            voice.Play();
            scrape = gameObject.AddComponent<AudioSource>();
            scrape.spatialBlend = 1f;
            scrape.maxDistance = 25f;
            scrape.rolloffMode = AudioRolloffMode.Linear;
            Set(start == CreatureState.Stalk || start == CreatureState.Vent ? start : CreatureState.Lurk);
        }

        protected override void Entered(CreatureState s)
        {
            if (look != null) look.State = s;
            if (s == CreatureState.Chase) UI.Captions.Say("[limbs slapping the floor, fast]");
            if (s == CreatureState.Vent) UI.Captions.Say("[something scraping inside the ceiling]");
        }

        /// A loud noise somewhere (boards torn off, glass breaking, a friend shouting).
        public override void Hear(Vector3 where, float radius)
        {
            if (State == CreatureState.Leave || State == CreatureState.Grab) return;
            if (Vector3.Distance(where, transform.position) > radius) return;
            Heard(where);
        }

        void Heard(Vector3 where)
        {
            heardAt = where;
            heardTime = Time.time;
            if (State == CreatureState.Lurk || State == CreatureState.Stalk || State == CreatureState.Search) Set(CreatureState.Chase);
        }

        /// How far this player's movement carries right now: a sprint carries far, walking a little, creeping not at all.
        static float Loudness(PlayerAgent p)
        {
            float speed = new Vector2(p.Velocity.x, p.Velocity.z).magnitude;
            if (p.Running && speed > 3f) return 14f;
            if (!p.Crouching && speed > 1.8f) return 3.5f;
            return 0f;
        }

        protected override void Think(float dt)
        {
            grabCooldown -= dt;
            // Listen: the nearest sound it can hear.
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive) continue;
                float loud = Loudness(p);
                if (loud > 0f) lastNoise[p] = Time.time;
                if (loud > 0f && Vector3.Distance(p.Position, transform.position) < loud && State != CreatureState.Grab && State != CreatureState.Leave)
                {
                    if (State == CreatureState.Vent) { heardAt = p.Position; heardTime = Time.time; }
                    else Heard(p.Position);
                }
            }

            switch (State)
            {
                case CreatureState.Lurk:   // buried, its long limb in the air
                case CreatureState.Stalk:  // crouched round a corner
                    voice.volume = 0f;
                    var bump = Nearest(1.5f);   // stepping on it, or walking right into the corner it's in
                    if (bump != null) { Heard(bump.Position); break; }
                    if (life > 240f && !InPlayerView()) Set(CreatureState.Leave);
                    break;

                case CreatureState.Vent:
                    voice.volume = 0.25f * Meta.AudioMix.Creatures;
                    voice.pitch = 0.6f;
                    if (Time.time - heardTime < 20f)
                    {
                        MoveToward(heardAt, VentSpeed, dt);
                        // Over the sound, with someone close below: it drops out of the ceiling.
                        if (Flat(heardAt - transform.position).magnitude < 2f && Nearest(7f) != null) { look.Drop(); Set(CreatureState.Chase); }
                    }
                    else if (life > 60f) Destroy(gameObject);   // never heard anything: gone up the ducts
                    break;

                case CreatureState.Chase:
                    voice.volume = 0.6f * Meta.AudioMix.Creatures;
                    voice.pitch = 1f;
                    if (!look.Ready) break;   // still tearing out of the floor
                    MoveToward(heardAt, CrawlSpeed, dt);
                    Face(heardAt, 360f);
                    if (TryGrab()) break;
                    if (Flat(heardAt - transform.position).magnitude < 1f && Time.time - heardTime > 5f) Set(CreatureState.Search);
                    break;

                case CreatureState.Search:
                    voice.volume = 0.3f * Meta.AudioMix.Creatures;
                    if (TryGrab()) break;
                    if (stateTime % 4f < dt) heardAt = transform.position + Random.insideUnitSphere.WithY(0f) * 4f;
                    MoveToward(heardAt, 1.2f, dt);
                    if (stateTime > 15f) Set(CreatureState.Lurk);   // fed or not, it digs back in
                    break;

                case CreatureState.Grab:
                    Reel(dt);
                    break;

                case CreatureState.Leave:
                    voice.volume = Mathf.MoveTowards(voice.volume, 0f, dt);
                    if (stateTime > 2f) Destroy(gameObject);
                    break;
            }
        }

        PlayerAgent Nearest(float within)
        {
            PlayerAgent best = null;
            float bestD = within;
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive) continue;
                float d = Vector3.Distance(p.Position, transform.position);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// Someone within reach who just made a sound, or who walked right into it: the long limb shoots out.
        bool TryGrab()
        {
            if (grabCooldown > 0f) return false;
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive) continue;
                float d = Flat(p.Position - transform.position).magnitude;
                bool noisy = lastNoise.TryGetValue(p, out var t) && Time.time - t < 1.5f;
                if (d < Reach && (noisy || d < 1.3f))
                {
                    held = p;
                    Target = p;
                    reel = Mathf.Max(0.15f, (d - MouthDistance) / EatTime);   // however close: always a few seconds to tear free
                    struggle = 0f;
                    need = new[] { 6f, 9f, 12f, 15f }[Mathf.Clamp(Meta.Difficulty.Level, 0, 3)];
                    look.Target = p.transform;
                    Set(CreatureState.Grab);
                    UI.Captions.Say("[a hand closes on your leg]");
                    if (p.IsLocal) UI.Captions.Say($"[mash {Controls.Interact.GetBindingDisplayString(0)} to tear free]");
                    return true;
                }
            }
            return false;
        }

        void Reel(float dt)
        {
            if (held == null || !held.Alive) { Release(false); return; }
            Face(held.Position, 360f);
            float d = Flat(held.Position - transform.position).magnitude;
            if (held.IsLocal)
            {
                held.Body.Pull(Flat(transform.position - held.Position).normalized * reel * dt);
                held.Bleed(BleedPerSecond * dt, this);
                if (Controls.Interact.WasPressedThisFrame()) struggle += 1f;
                if (struggle >= need) { Release(true); return; }
            }
            else if (stateTime > 3.5f) { held.Hurt(0.3f, true, this); Release(true); return; }   // shortcut: friends can't mash over the network yet; they tear free after a while, hurt
            if (d < MouthDistance && stateTime > EatTime * 0.9f)
            {
                var prey = held;
                held = null;
                UI.Captions.Say("[it pulls you into the mouth]");
                Execute(prey);
                held = null;
            }
        }

        void Release(bool tornFree)
        {
            if (tornFree && held != null && held.IsLocal) { UI.Captions.Say("[you tear free]"); held.Body.KnockDown(0.4f); }
            held = null;
            look.Target = null;
            grabCooldown = 4f;   // it recoils: a moment to get away (quietly)
            Set(CreatureState.Search);
        }
    }

    static class VectorExt
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}
