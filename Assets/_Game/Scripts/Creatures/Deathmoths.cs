using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// Wiki Entity 4, what every kind of Deathmoth shares: light draws them, repellent drives them off.
    public abstract class Deathmoth : Creature
    {
        /// The hive it belongs to, or null.
        public MothHive Hive;

        public static bool Is(CreatureKind k) => k == CreatureKind.MothSwarm || k == CreatureKind.MothMales || k == CreatureKind.Praetorian || k == CreatureKind.MothEmpress || k == CreatureKind.MothHive;

        public static Creature Add(GameObject go, CreatureKind k) =>
            k == CreatureKind.Praetorian ? go.AddComponent<Praetorian>() : k == CreatureKind.MothEmpress ? go.AddComponent<MothEmpress>()
            : k == CreatureKind.MothHive ? go.AddComponent<MothHive>() : (Creature)go.AddComponent<MothSwarm>();

        /// A repellent can sprayed at it.
        public virtual void Repel(Vector3 from) { }
        /// Whatever guards the hive wakes: someone came in.
        public virtual void Rouse(PlayerAgent at) { }

        protected static bool OnLevel(string id) => ChunkStreamer.Instance != null && ChunkStreamer.Instance.Theme != null && ChunkStreamer.Instance.Theme.id == id;
    }

    /// A swarm of females (on Level 3, males too), or a harmless handful of males.
    /// They hang round the nearest light. Your torch draws them: they spit burning fluid from a few metres, then close
    /// in and smother you. Put the light out and walk away and they lose you; a thrown glowstick pulls them off.
    /// Males only ever follow a light about; set a bottle of almond water down and they drink, and keep you company.
    public sealed class MothSwarm : Deathmoth
    {
        const float TorchRange = 18f, NearRange = 3.5f, SpitMin = 3.5f, SpitMax = 9f, ClingRange = 1.3f, ShedWalk = 6f;
        const float Speed = 3.7f, SpitDamage = 0.07f;

        public bool Males => Kind == CreatureKind.MothMales;

        MothFlock flock;
        Vector3 home, goal, shedAt;
        float nextSpit, shed, lost, nextThink, company, frenzy, spared;
        bool tough;      // Level 3: the males ignore repellent
        bool thinned;    // repellent has taken the females

        protected override void Begin(CreatureState start)
        {
            tough = !Males && OnLevel("3");
            home = goal = transform.position;
            flock = MothFlock.Build(transform, Males, Males ? 4 : 14);
            voice.clip = Audio.CreatureSounds.Flutter();
            voice.volume = 0.2f * Meta.AudioMix.Creatures;
            voice.pitch = Males ? 1.25f : 0.95f;
            voice.Play();
            Set(start == CreatureState.Lurk ? CreatureState.Lurk : CreatureState.Watch);
        }

        protected override void Entered(CreatureState s)
        {
            if (flock != null) { flock.State = s; flock.Prey = s == CreatureState.Grab && Target != null ? Target.Head : null; }
            if (s == CreatureState.Chase) { shed = lost = 0f; UI.Captions.Say("[a rush of wings]"); }
            if (s == CreatureState.Grab) { shed = 0f; if (Target != null) shedAt = Flat(Target.Position); UI.Captions.Say("[wings over your face: put the light out and walk]"); }
        }

        public override void Rouse(PlayerAgent at)
        {
            if (Males || State == CreatureState.Leave || State == CreatureState.Grab) return;
            Target = at;
            if (State != CreatureState.Chase) Set(CreatureState.Chase);
        }

        /// The Empress is threatened: for a while they need no light to find you.
        public void Frenzy(PlayerAgent at) { frenzy = Time.time + 20f; Rouse(at); }

        public override void Repel(Vector3 from)
        {
            if (State == CreatureState.Leave) return;
            Wounds.Add(CreatureWoundType.Repellent);
            UI.Captions.Say(tough && !thinned ? "[the females scatter; the males keep coming]" : "[they scatter]");
            if (tough) { if (!thinned) { thinned = true; flock.Thin(); } return; }   // Level 3's males don't care
            Set(CreatureState.Leave);
        }

        /// A lit torch it can see, nearest first.
        PlayerAgent Torch(float range)
        {
            PlayerAgent best = null;
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive || !p.FlashlightOn) continue;
                float d = Flat(p.Position - transform.position).magnitude;
                if (d < range && CanSee(p, d)) { best = p; range = d; }
            }
            return best;
        }

        protected override void Think(float dt)
        {
            if (State == CreatureState.Leave)
            {
                voice.volume = Mathf.MoveTowards(voice.volume, 0f, dt * 0.3f);
                if (stateTime > 4f) Destroy(gameObject);
                return;
            }
            if (Males) { Docile(dt); return; }
            voice.volume = Mathf.MoveTowards(voice.volume, (State == CreatureState.Lurk ? 0.08f : State == CreatureState.Watch ? 0.25f : 0.75f) * Meta.AudioMix.Creatures, dt);

            // A thrown light beats everything.
            var lure = Lure.Nearest(transform.position, 22f);
            if (lure != null && State != CreatureState.Lurk)
            {
                if (State != CreatureState.Lured) Set(CreatureState.Lured);
                if (Flat(lure.transform.position - transform.position).magnitude > 0.5f) MoveToward(lure.transform.position, Speed, dt);
                return;
            }

            switch (State)
            {
                case CreatureState.Lurk:    // settled on the hive until somebody comes in
                    break;

                case CreatureState.Lured:   // it went out
                    Set(CreatureState.Watch);
                    break;

                case CreatureState.Watch:
                    Drift(dt);
                    var t = Torch(TorchRange);
                    var close = Players.Nearest(transform.position);
                    if (t == null && Time.time > spared && close != null && Flat(close.Position - transform.position).magnitude < NearRange && CanSee(close, NearRange)) t = close;
                    if (t != null) { Target = t; Set(CreatureState.Chase); }
                    break;

                case CreatureState.Chase:
                    if (Target == null || !Target.Alive) { Set(CreatureState.Watch); break; }
                    float d = Flat(Target.Position - transform.position).magnitude;
                    bool sees = CanSee(Target, d);
                    MoveToward(Target.Position, Speed * (Time.time < frenzy ? 1.25f : 1f), dt);
                    Face(Target.Position, 300f);
                    if (sees && d > SpitMin && d < SpitMax && Time.time > nextSpit)
                    {
                        nextSpit = Time.time + (thinned ? 4f : 2.2f);
                        AcidGlob.Spit(transform.position + Vector3.up * 1.6f, Target, SpitDamage, this);
                    }
                    if (d < ClingRange && sees) { Set(CreatureState.Grab); break; }
                    // Nothing to see by: dark and out of reach, they lose you.
                    lost = (Target.FlashlightOn && sees) || Time.time < frenzy || d < NearRange ? 0f : lost + dt;
                    if (lost > 5f || (Hive != null && Vector3.Distance(transform.position, home) > 40f)) Set(CreatureState.Watch);
                    break;

                case CreatureState.Grab:
                    if (Target == null || !Target.Alive) { Set(CreatureState.Watch); break; }
                    float gap = Flat(Target.Position - transform.position).magnitude;
                    if (gap > 0.15f) MoveToward(Target.Position, 4.2f, dt);
                    if (Target.IsLocal) MothSmother.Hold(Target, this);   // a guest's own game does this from its copy
                    // Light out and walking: they drop off, a few at a time.
                    shed = Target.FlashlightOn ? 0f : shed + Vector3.Distance(Flat(Target.Position), shedAt) * (thinned ? 2f : 1f);   // metres walked in the dark
                    shedAt = Flat(Target.Position);
                    if (shed > ShedWalk || gap > 5f)
                    {
                        UI.Captions.Say("[they lose you in the dark]");
                        nextThink = spared = Time.time + 6f;      // long enough to get away
                        Set(CreatureState.Watch);
                    }
                    break;
            }
        }

        /// Round the nearest working light, or home if there isn't one.
        void Drift(float dt)
        {
            if (Time.time > nextThink)
            {
                nextThink = Time.time + 4f;
                goal = Hive != null ? home : ChunkStreamer.Instance != null ? ChunkStreamer.Instance.NearestLightPoint(transform.position, 14f) ?? home : home;
                goal.y = transform.position.y;
            }
            if (Flat(goal - transform.position).magnitude > 0.6f) MoveToward(goal, 1.4f, dt);
            if (Hive == null && life > 260f && !InPlayerView()) Set(CreatureState.Leave);
        }

        void Docile(float dt)
        {
            var bait = MothBait.Nearest(transform.position, 14f);
            if (bait != null)
            {
                if (State != CreatureState.Lured) Set(CreatureState.Lured);
                if (Flat(bait.transform.position - transform.position).magnitude > 0.8f) { MoveToward(bait.transform.position, 1.8f, dt); stateTime = 0f; }
                else if (stateTime > 8f)
                {
                    Destroy(bait.gameObject);
                    company = Time.time + 90f;
                    UI.Captions.Say("[the moths have drunk; they settle around you]");
                    Set(CreatureState.Watch);
                }
                return;
            }
            if (State != CreatureState.Watch) Set(CreatureState.Watch);
            var near = Players.Nearest(transform.position);
            if (near == null) return;
            float d = Flat(near.Position - transform.position).magnitude;
            if (Time.time < company)   // fed: they keep you company, and it helps
            {
                if (d > 1.6f) MoveToward(near.Position, 2.8f, dt);
                if (d < 6f) near.LoseSanity(-dt / 120f);
                return;
            }
            var torch = Torch(8f);
            if (torch != null) { if (Flat(torch.Position - transform.position).magnitude > 1.8f) MoveToward(torch.Position, 1.6f, dt); }
            else Drift(dt);
        }
    }

    /// The nest's guard: nearly a metre of legs. Stays by the hive; its touch half-paralyses you.
    public sealed class Praetorian : Deathmoth
    {
        const float Speed = 4.3f, Leash = 18f;
        MothBody body;
        AudioSource sound;
        float phase, rear, nextStrike, numbUntil;
        Vector3 home, last;

        protected override void Begin(CreatureState start)
        {
            body = new MothBody("MothPraetorian", transform);
            body.Root.localPosition = Vector3.up * 0.59f;   // hips this high: the feet on the floor
            home = last = transform.position;
            voice.clip = Audio.CreatureSounds.Scuttle();
            voice.volume = 0f;
            voice.Play();
            sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 1f; sound.maxDistance = 25f; sound.rolloffMode = AudioRolloffMode.Linear;
            Set(CreatureState.Lurk);
        }

        public override void Rouse(PlayerAgent at)
        {
            if (State != CreatureState.Lurk && State != CreatureState.Watch) return;
            Target = at;
            UI.Captions.Say("[long legs unfold, clicking]");
            sound.PlayOneShot(Audio.CreatureSounds.Bark(), 0.5f * Meta.AudioMix.Creatures);
            Set(CreatureState.Chase);
        }

        public override void Repel(Vector3 from)   // it backs off a moment, no more
        {
            Wounds.Add(CreatureWoundType.Repellent);
            numbUntil = Time.time + 5f;
            if (State == CreatureState.Chase || State == CreatureState.Lunge) Set(CreatureState.Circle);
        }

        protected override void Think(float dt)
        {
            float d = Target != null ? Flat(Target.Position - transform.position).magnitude : 99f;
            voice.volume = (State == CreatureState.Lurk ? 0f : 0.5f) * Meta.AudioMix.Creatures;
            switch (State)
            {
                case CreatureState.Lurk:
                    var near = Players.Nearest(transform.position);
                    if (near != null && Flat(near.Position - transform.position).magnitude < 6f && CanSee(near, 6f)) { Rouse(near); Hive?.Wake(near); }
                    break;
                case CreatureState.Chase:
                    if (Target == null || !Target.Alive || Vector3.Distance(transform.position, home) > Leash) { Set(CreatureState.Search); break; }
                    if (d > 1.2f) MoveToward(Target.Position, Speed, dt);
                    Face(Target.Position, 360f);
                    if (d < 1.5f && Time.time > nextStrike) Set(CreatureState.Lunge);
                    break;
                case CreatureState.Lunge:
                    Face(Target.Position, 500f);
                    if (stateTime > 0.35f && Time.time > nextStrike)
                    {
                        nextStrike = Time.time + 2.2f;
                        if (d < 1.8f)
                        {
                            Target.Hurt(0.28f, true, this);
                            if (Target.IsLocal) Target.Body.SlowedUntil = Time.time + 5f;   // paralytic toxin
                            sound.PlayOneShot(Audio.CreatureSounds.Maul(), 0.8f * Meta.AudioMix.Creatures);
                            UI.Captions.Say("[your legs go numb]");
                        }
                    }
                    if (stateTime > 0.7f) Set(CreatureState.Circle);
                    break;
                case CreatureState.Circle:
                    if (Target != null) { MoveToward(transform.position - Flat(Target.Position - transform.position).normalized * 2f, 2.4f, dt, false); Face(Target.Position, 300f); }
                    if (stateTime > 1.2f && Time.time > numbUntil) Set(CreatureState.Chase);
                    break;
                case CreatureState.Search:   // back to its nest
                    if (Flat(home - transform.position).magnitude > 0.6f) { MoveToward(home, 2.2f, dt); Face(home, 200f); }
                    else Set(CreatureState.Lurk);
                    break;
            }
            Pose(body, State, ref phase, ref rear, ref last, dt);
        }

        void Pose(MothBody b, CreatureState s, ref float phase, ref float rear, ref Vector3 last, float dt) => MothWalker.Pose(b, transform, s, ref phase, ref rear, ref last, dt, 0.59f);
    }

    /// Legs and wings of the walking castes (owner's and guests').
    public sealed class MothWalker : MonoBehaviour
    {
        public CreatureState State;
        MothBody body;
        float phase, rear, height;
        Vector3 last;

        public static MothWalker Build(Transform root, string model, float height)
        {
            var w = root.gameObject.AddComponent<MothWalker>();
            w.body = new MothBody(model, root);
            w.body.Root.localPosition = Vector3.up * height;
            w.height = height;
            w.last = root.position;
            return w;
        }

        void LateUpdate() => Pose(body, transform, State, ref phase, ref rear, ref last, Time.deltaTime, height);

        public static void Pose(MothBody body, Transform root, CreatureState s, ref float phase, ref float rear, ref Vector3 last, float dt, float height)
        {
            if (dt <= 0f) return;
            float speed = new Vector2(root.position.x - last.x, root.position.z - last.z).magnitude / dt;
            last = root.position;
            phase += dt * (2f + speed * 4.5f);
            bool strike = s == CreatureState.Lunge;
            rear = Mathf.MoveTowards(rear, strike ? 1f : 0f, dt * 5f);
            bool asleep = s == CreatureState.Lurk;
            body.Legs(phase, asleep ? 0.05f : 0.25f + Mathf.Clamp01(speed / 3f), rear);
            body.Root.localPosition = Vector3.up * (height + Mathf.Sin(phase * 2f) * 0.015f);
            body.Root.localRotation = Quaternion.Euler(-rear * 28f, 0f, 0f);
            body.Wings(asleep ? Mathf.Sin(Time.time * 0.7f) * 4f : 8f + Mathf.Sin(Time.time * (strike ? 55f : 30f)) * (strike ? 30f : 10f));
        }
    }

    /// The matriarch at the heart of a rare Level 5 hive: three metres of wing, too heavy to fly. Threaten her (come close,
    /// or put your light on her) and every moth near goes into a frenzy; come closer and a beat of her wings throws you back.
    public sealed class MothEmpress : Deathmoth
    {
        MothBody body;
        float phase, rear, nextFrenzy, nextBeat;
        Vector3 last;

        protected override void Begin(CreatureState start)
        {
            body = new MothBody("MothEmpress", transform);
            body.Root.localPosition = Vector3.up * 0.55f;
            last = transform.position;
            voice.clip = Audio.CreatureSounds.Flutter();
            voice.pitch = 0.4f;
            voice.maxDistance = 30f;
            voice.volume = 0.4f * Meta.AudioMix.Creatures;
            voice.Play();
            Set(CreatureState.Lurk);
        }

        protected override void Think(float dt)
        {
            if (State == CreatureState.Lunge && stateTime > 0.8f) Set(CreatureState.Watch);
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive) continue;
                float d = Flat(p.Position - transform.position).magnitude;
                if (d > 14f || !CanSee(p, d)) continue;
                bool lit = p.FlashlightOn && Vector3.Angle(p.Forward, transform.position + Vector3.up * 0.8f - p.Eyes) < 16f;
                if ((d < 6f || lit) && Time.time > nextFrenzy)
                {
                    nextFrenzy = Time.time + 15f;
                    Set(CreatureState.Watch);
                    UI.Captions.Say("[a vast drumming of wings: the whole hive answers]");
                    Hive?.Wake(p);
                    foreach (var s in FindObjectsByType<MothSwarm>(FindObjectsSortMode.None))
                        if (Vector3.Distance(s.transform.position, transform.position) < 45f) s.Frenzy(p);
                }
                if (d < 3f && Time.time > nextBeat)
                {
                    nextBeat = Time.time + 3f;
                    Set(CreatureState.Lunge);
                    p.Hurt(0.18f, true, this);
                    if (p.IsLocal) p.Body.Pull(Flat(p.Position - transform.position).normalized * 1.4f);
                }
            }
            MothWalker.Pose(body, transform, State, ref phase, ref rear, ref last, dt, 0.55f);
        }
    }

    /// The nest: resin over the wall with eggs, bones and larvae in it, its lights dead. Wakes when someone comes in or
    /// opens its door, or makes a noise close by. Sits with its back to the wall behind it.
    public sealed class MothHive : Deathmoth
    {
        public readonly List<Deathmoth> Brood = new List<Deathmoth>();
        HotelDoor door;
        public ChunkStreamer.DarkPocket Pocket;
        public const float PocketRadius = 7.5f;
        float quiet = -99f;

        /// How far it bulges out from its wall in this room.
        public float Depth { get; private set; } = 1f;

        protected override void Begin(CreatureState start) => Set(CreatureState.Lurk);

        /// Fills the room: turned to face away from its wall, then grown to fit.
        public void Settle(Vector3 normal)
        {
            transform.rotation = Quaternion.LookRotation(normal);
            Depth = Grow(transform);
        }

        /// The growth, fitted to the room it's in: wall to wall and floor to ceiling on the back wall, then smaller ones
        /// on the side walls. Measured the same way on every game, so co-op guests get the same hive.
        public static float Grow(Transform root)
        {
            const float ModelWidth = 5.2f, ModelHeight = 2.7f, ModelDepth = 2.1f;
            Vector3 up = Vector3.up, fwd = root.forward, side = root.right;
            var mid = root.position + fwd * 0.5f + up * 1.2f;
            float Free(Vector3 from, Vector3 dir, float max) => Physics.Raycast(from, dir, out var h, max, ~0, QueryTriggerInteraction.Ignore) ? h.distance : max;
            float right = Free(mid, side, 4f), left = Free(mid, -side, 4f), ceiling = Free(mid, up, 4f) + 1.2f, room = Free(mid, fwd, 9f) + 0.5f;
            float width = right + left, depth = Mathf.Min(ModelDepth * 1.25f, room * 0.42f);   // always leaves room to stand
            var body = new MothBody("MothHive", root).Root;
            body.localPosition = new Vector3((right - left) * 0.5f, 0f, 0f);
            body.localScale = Vector3.Scale(body.localScale, new Vector3(width / ModelWidth, ceiling / ModelHeight, depth / ModelDepth));
            Solid(body);
            foreach (var s in new[] { 1f, -1f })   // along the side walls, the way a nest spreads
            {
                var at = root.position + fwd * (depth + 0.2f) + side * ((right - left) * 0.5f);
                if (room - depth < 2.2f || !Physics.Raycast(at + up * 1.2f, side * s, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore) || Vector3.Dot(hit.normal, -side * s) < 0.9f) continue;
                var g = new MothBody("MothHiveSmall", root).Root;
                g.position = new Vector3(hit.point.x, root.position.y, hit.point.z) + fwd * 0.4f;
                g.rotation = Quaternion.LookRotation(hit.normal);
                g.localScale = Vector3.Scale(g.localScale, new Vector3(1.15f, Mathf.Min(1.1f, ceiling / 2.5f), 0.9f));
                Solid(g);
            }
            return depth;
        }

        static void Solid(Transform t)
        {
            foreach (var f in t.GetComponentsInChildren<MeshFilter>()) f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
        }

        /// The room's door: opening it wakes them.
        public void Attach(HotelDoor d) { door = d; door.Opened += OnDoor; }

        void OnDestroy()
        {
            if (door != null) door.Opened -= OnDoor;
            ChunkStreamer.Instance?.RemoveDarkPocket(Pocket);
        }

        void OnDoor()
        {
            var who = Players.Nearest(door.transform.position);
            if (who != null && Vector3.Distance(who.Position, door.transform.position) < 4f) Wake(who);
        }

        public override void Hear(Vector3 where, float radius)
        {
            if (Vector3.Distance(where, transform.position) < Mathf.Min(radius, 9f)) Wake(Players.Nearest(where));
        }

        public void Wake(PlayerAgent who)
        {
            if (who == null || Time.time < quiet) return;
            quiet = Time.time + 10f;
            UI.Captions.Say("[the walls rustle awake]");
            foreach (var m in Brood) if (m != null) m.Rouse(who);
        }

        protected override void Think(float dt)
        {
            var p = Players.Nearest(transform.position);
            if (p == null) return;
            float d = Flat(p.Position - transform.position).magnitude;
            // Seen from its face, not from inside it.
            var front = transform.position + transform.forward * (Depth + 0.3f) + Vector3.up * 1.4f;
            if (Vector3.Distance(front, p.Eyes) < 3.6f && (!Physics.Linecast(front, p.Eyes, out var hit, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) == p)) Wake(p);
            if (d > 60f) { foreach (var m in Brood) if (m != null) Destroy(m.gameObject); Destroy(gameObject); }   // left far behind
        }
    }

    /// A gob of caustic fluid, thrown at someone. Burns on a hit; on a wall it leaves a wet stain for a while.
    public sealed class AcidGlob : MonoBehaviour
    {
        static Material wet;
        PlayerAgent at;
        Vector3 velocity;
        float damage, born;
        Creature by;

        public static void Spit(Vector3 from, PlayerAgent at, float damage, Creature by)
        {
            if (wet == null) wet = HdMat.Make(new Color(0.72f, 0.78f, 0.22f), 0.95f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "Acid";
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.09f;
            go.GetComponent<MeshRenderer>().sharedMaterial = wet;
            var g = go.AddComponent<AcidGlob>();
            var aim = at.Eyes + Vector3.down * 0.2f;
            float t = Vector3.Distance(from, aim) / 11f;
            g.velocity = (aim - from) / t - 0.5f * Physics.gravity * t;   // lands where you are now: step aside
            g.at = at; g.damage = damage; g.born = Time.time; g.by = by;
            AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Squelch(), from, 0.5f * Meta.AudioMix.Creatures);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var p = transform.position;
            velocity += Physics.gravity * dt;
            var q = p + velocity * dt;
            if (at != null && at.Alive && (Vector3.Distance(q, at.Eyes) < 0.5f || Vector3.Distance(q, at.Position + Vector3.up) < 0.5f))
            {
                at.Hurt(damage, false, by);
                UI.Captions.Say("[it burns]");
                AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Hiss(), q, 0.6f * Meta.AudioMix.Creatures);
                Destroy(gameObject);
                return;
            }
            if (Physics.Linecast(p, q, out var hit, ~0, QueryTriggerInteraction.Ignore) && Players.Owns(hit.collider) == null)
            {
                transform.SetPositionAndRotation(hit.point + hit.normal * 0.004f, Quaternion.FromToRotation(Vector3.up, hit.normal));
                transform.localScale = new Vector3(0.22f, 0.008f, 0.22f);
                enabled = false;
                Destroy(gameObject, 8f);
                return;
            }
            transform.position = q;
            if (Time.time - born > 4f) Destroy(gameObject);
        }
    }

    /// A bottle of almond water set down open: docile males come and drink.
    public sealed class MothBait : MonoBehaviour
    {
        static readonly List<MothBait> all = new List<MothBait>();
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static MothBait Nearest(Vector3 from, float range)
        {
            MothBait best = null;
            foreach (var b in all)
            {
                if (b == null) continue;
                float d = Vector3.Distance(from, b.transform.position);
                if (d < range) { range = d; best = b; }
            }
            return best;
        }
    }
}
