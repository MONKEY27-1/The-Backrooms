using System;
using System.Collections.Generic;
using Backrooms.Net;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Creatures
{
    /// The hidden game master. Keeps the "calm unease" rhythm: long quiet stretches, small unease events
    /// (footsteps that stop when you stop, a faceless resident watching), and rarely a hunt: the lights die
    /// and something that smiles in the dark comes looking. Never anything in the first minutes.
    public sealed class FearDirector : MonoBehaviour, ICreatureWorld
    {
        // Scaled by the save's difficulty: how long you're left alone on arrival, how long between events.
        static float Grace => Meta.Difficulty.Grace;
        static float HuntCooldown => 420f * Meta.Difficulty.Cooldowns;
        static float ResidentCooldown => 150f * Meta.Difficulty.Cooldowns;
        static float SkinStealerCooldown => 300f * Meta.Difficulty.Cooldowns;
        static float PartyGoerCooldown => 900f * Meta.Difficulty.Cooldowns;
        static float SmallCooldown => 45f * Meta.Difficulty.Cooldowns;
        const float HuntLength = 75f;

        public float Tension { get; private set; }
        public string Status { get; private set; } = "quiet";
        /// A creature caught this player.
        public event Action<PlayerAgent> CaughtPlayer;
        public event Action HuntSurvived, FacelingSeen;

        /// Co-op guests: only the phantom footsteps run here (the owner's game decides everything else).
        public bool StepsOnly;
        /// The owner picked a friend to hear footsteps behind them.
        public event Action<PlayerAgent> RemoteSteps;
        bool huntHadHunter;

        public NavGrid Grid { get; private set; }
        public bool Dark => streamer != null && (streamer.Blackout > 0.5f || streamer.NearestLight > 7f);

        ChunkStreamer streamer;
        readonly List<Creature> creatures = new List<Creature>();
        float visitTime, nextRoll, lastHunt = -999f, lastResident = -999f, lastSkinStealer = -999f, lastPartyGoer = -999f, lastSmall = -999f;
        float huntEnds = -1f, spawnHunterAt = -1f, nextGrid;
        PlayerAgent huntPrey;   // who the hunt comes for (any of the living)
        AudioSource steps;
        int stepsLeft;
        float nextStep;

        public void Init(ChunkStreamer world)
        {
            streamer = world;
            Grid = new NavGrid(null);   // players' own colliders never block the grid (see NavGrid)
            steps = new GameObject("Phantom Steps").AddComponent<AudioSource>();
            steps.transform.SetParent(transform, false);
            steps.spatialBlend = 1f;
            steps.maxDistance = 25f;
            steps.rolloffMode = AudioRolloffMode.Linear;
        }

        /// A new level (or a respawn): clear everything and start the grace period again.
        public void Reset()
        {
            foreach (var c in creatures) if (c != null) Destroy(c.gameObject);
            creatures.Clear();
            if (streamer != null) streamer.Blackout = 0f;
            huntEnds = spawnHunterAt = -1f;
            visitTime = 0f;
            Tension = Mathf.Min(Tension, 0.3f);
            stepsLeft = 0;
            Status = "quiet";
        }

        /// Something loud happened (boards torn off a doorway): hunters nearby come to look, and it sets you on edge.
        public void Noise(Vector3 where, float radius)
        {
            foreach (var c in creatures) if (c != null) c.Hear(where, radius);
            Tension = Mathf.Clamp01(Tension + 0.1f);
        }

        public void Caught(Creature by, PlayerAgent who)
        {
            if (who == null) return;
            who.LoseSanity(0.2f);
            CaughtPlayer?.Invoke(who);
        }

        void Update()
        {
            if (streamer != null && streamer.Theme != null && (streamer.Theme.generator == LevelGenerator.Stairwell || streamer.Theme.id == "37"))
            {
                // Both sources describe these levels as devoid of entities.
                streamer.Blackout = 0f;
                Status = "quiet";
                return;
            }
            if (StepsOnly) { PhantomSteps(); return; }
            if (!Players.AnyAlive()) return;
            float dt = Time.deltaTime;
            visitTime += dt;
            creatures.RemoveAll(c => c == null);
            foreach (var c in creatures) if (c.Kind == CreatureKind.Faceling && Visible(c.transform.position)) FacelingSeen?.Invoke();

            // Tension builds in quiet, faster in the dark and when your mind is going.
            float dark = streamer != null && streamer.NearestLight > 7f ? 1f : 0f;
            float mind = 1f - Players.MeanSanity();
            Tension = Mathf.Clamp01(Tension + dt * (1f / 480f + dark / 300f + mind / 600f) * Meta.Difficulty.Tension);

            Hunt(dt);
            PhantomSteps();
            DevKeys();

            if (Time.time < nextRoll) return;
            nextRoll = Time.time + 20f;
            if (huntEnds > 0f || creatures.Exists(c => c != null && c.Kind == CreatureKind.PartyGoer && c.State != CreatureState.Leave)) return;
            float r = UnityEngine.Random.value;
            if (visitTime > Grace && Tension > 0.45f && Time.time - lastPartyGoer > PartyGoerCooldown && r < 0.008f) SpawnRare(CreatureKind.PartyGoer, 12, 22);
            else if (visitTime > Grace && Tension > 0.45f && Time.time - lastHunt > HuntCooldown && r < 0.35f) StartHunt();
            else if (visitTime > Grace * 0.5f && Tension > 0.3f && Time.time - lastSkinStealer > SkinStealerCooldown && SkinStealerChance() > UnityEngine.Random.value) SpawnRare(CreatureKind.SkinStealer, 8, 16);
            else if (visitTime > Grace * 0.35f && Tension > 0.35f && Time.time - lastResident > ResidentCooldown && r < 0.3f) SpawnResident();
            else if (visitTime > Grace * 0.2f && Tension > 0.2f && Time.time - lastSmall > SmallCooldown && r < 0.35f) StartSteps();
        }

        // ---------- rare encounters ----------

        float SkinStealerChance()
        {
            string level = streamer != null && streamer.Theme != null ? streamer.Theme.id : "";
            return level == "1" || level == "2" || level == "3" ? 0.04f : 0.004f;
        }

        void SpawnRare(CreatureKind kind, int min, int max)
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null) return;
            if (Grid.TargetCell != Cell(who.Position)) Grid.Build(who.Position);
            var at = Grid.RandomSpot(min, max, p => !Visible(p));
            if (!at.HasValue) return;
            if (kind == CreatureKind.SkinStealer) lastSkinStealer = Time.time;
            else if (kind == CreatureKind.PartyGoer) lastPartyGoer = Time.time;
            creatures.Add(Creature.Spawn(kind, at.Value, this));
        }

        // ---------- the hunt ----------

        void StartHunt()
        {
            lastHunt = Time.time;
            huntEnds = Time.time + HuntLength;
            spawnHunterAt = Time.time + 2.5f;   // the lights go first
            streamer.Blackout = 1f;
            huntHadHunter = false;
            huntPrey = Players.Pick(UnityEngine.Random.value);
            UI.Captions.Say("[the lights die]");
            Tension = 0f;
            Status = "lights out";
        }

        void Hunt(float dt)
        {
            if (huntEnds < 0f) return;
            bool hunterAlive = creatures.Exists(c => c.Kind == CreatureKind.Smiler);
            // The flow field leads to whoever the hunter is after (one of the living, picked when the hunt starts).
            var hunter = creatures.Find(c => c.Kind == CreatureKind.Smiler);
            var prey = hunter != null && hunter.Target != null ? hunter.Target : huntPrey != null && huntPrey.Alive ? huntPrey : Players.Nearest(Vector3.zero);
            if (prey == null) return;
            if (hunterAlive || spawnHunterAt > 0f)
                if (Time.time >= nextGrid && Grid.TargetCell != Cell(prey.Position))
                {
                    Grid.Build(prey.Position);
                    nextGrid = Time.time + 0.3f;
                }

            if (spawnHunterAt > 0f && Time.time >= spawnHunterAt)
            {
                spawnHunterAt = -1f;
                if (Grid.TargetCell != Cell(prey.Position)) Grid.Build(prey.Position);
                var at = Grid.RandomSpot(14, 26, p => !Visible(p));
                if (at.HasValue) { creatures.Add(Creature.Spawn(CreatureKind.Smiler, at.Value, this)); huntHadHunter = true; UI.Captions.Say("[crackling static]"); }
                else huntEnds = Time.time + 12f;   // nowhere for it to come from: just a blackout
            }

            var smiler = creatures.Find(c => c.Kind == CreatureKind.Smiler);
            FirstPersonController.Chased = smiler != null && smiler.State == CreatureState.Chase && smiler.Target == Players.Local;
            if (smiler != null)
            {
                Status = "hunt: " + smiler.State.ToString().ToLowerInvariant();
                foreach (var a in Players.All)   // being near it wears you down
                    if (a != null && a.Alive && Vector3.Distance(smiler.transform.position, a.Position) < 15f) a.LoseSanity(dt / 150f);
            }

            // The lights come back when the hunt is over or its time runs out; in the light it can't stay.
            bool over = Time.time > huntEnds || (!hunterAlive && spawnHunterAt < 0f && Time.time > lastHunt + 8f);
            if (over)
            {
                if (huntHadHunter) HuntSurvived?.Invoke();
                huntHadHunter = false;
                streamer.Blackout = 0f;
                huntEnds = -1f;
                FirstPersonController.Chased = false;
                Status = "quiet";
            }
        }

        // ---------- small things ----------

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DeveloperSpawn(CreatureKind kind)
        {
            if (!Net.Coop.Authority || streamer?.Theme?.generator == LevelGenerator.Stairwell || streamer?.Theme?.id == "37") return;
            var near = Players.Local;
            if (near == null || !near.Alive) near = Players.Nearest(Vector3.zero);
            if (near == null) return;
            if (Grid.TargetCell != Cell(near.Position)) Grid.Build(near.Position);
            var at = Grid.RandomSpot(8, 16, p => !Visible(p));
            if (at.HasValue) creatures.Add(Creature.Spawn(kind, at.Value, this));
        }

        public void DeveloperStartHunt()
        {
            if (streamer?.Theme?.generator != LevelGenerator.Stairwell && streamer?.Theme?.id != "37" && Net.Coop.Authority && huntEnds < 0f) { visitTime = Mathf.Max(visitTime, Grace); StartHunt(); }
        }

        public void DeveloperClearCreatures()
        {
            if (!Net.Coop.Authority) return;
            foreach (var c in creatures) if (c != null) Destroy(c.gameObject);
            creatures.Clear();
            huntEnds = spawnHunterAt = -1f;
            huntHadHunter = false;
            FirstPersonController.Chased = false;
            if (streamer != null) streamer.Blackout = 0f;
            Status = "quiet";
        }
#endif

        void SpawnResident()
        {
            var near = Players.Pick(UnityEngine.Random.value);   // shows itself to one of the living
            if (near == null) return;
            if (Grid.TargetCell != Cell(near.Position)) Grid.Build(near.Position);
            var at = Grid.RandomSpot(12, 22, p => !Visible(p));
            if (!at.HasValue) return;
            lastResident = Time.time;
            Tension = Mathf.Max(0f, Tension - 0.25f);
            creatures.Add(Creature.Spawn(CreatureKind.Faceling, at.Value, this));
        }

        /// Footsteps behind someone that stop when they stop (sent to their game if it's a friend).
        void StartSteps()
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who != null && !who.IsLocal)
            {
                lastSmall = Time.time;
                Tension = Mathf.Max(0f, Tension - 0.1f);
                RemoteSteps?.Invoke(who);
                return;
            }
            StartStepsHere();
        }

        /// Footsteps behind you (your own game), now.
        public void StartStepsHere()
        {
            lastSmall = Time.time;
            stepsLeft = UnityEngine.Random.Range(4, 8);
            UI.Captions.Say("[footsteps behind you]");
            nextStep = Time.time + 0.5f;
            Tension = Mathf.Max(0f, Tension - 0.1f);
        }

        /// Only ever behind you (your own player): a friend doesn't hear the footsteps you imagine.
        void PhantomSteps()
        {
            if (stepsLeft <= 0 || Time.time < nextStep) return;
            var me = Players.Local;
            if (me == null || !me.Alive || me.Velocity.sqrMagnitude < 0.2f) { stepsLeft = 0; return; }   // you stopped; so did they
            stepsLeft--;
            nextStep = Time.time + 0.55f;
            steps.transform.position = me.Position - me.transform.forward * 9f + Vector3.up * 0.1f;
            var clips = Audio.ProceduralAudio.Footsteps();
            steps.pitch = UnityEngine.Random.Range(0.85f, 0.95f);
            steps.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], 0.8f * Meta.AudioMix.Creatures);
        }

        // ---------- helpers ----------

        static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));

        /// Could any living player see this spot right now?
        static bool Visible(Vector3 p) => Players.AnySees(p + Vector3.up * 1.2f);

        void DevKeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keys = Keyboard.current;
            if (keys == null || Coop.IsGuest) return;
            if (keys.f5Key.wasPressedThisFrame && huntEnds < 0f) { visitTime = Mathf.Max(visitTime, Grace); StartHunt(); }   // F5: a hunt now
            if (keys.f4Key.wasPressedThisFrame) SpawnResident();                                                            // F4: a resident now
#endif
        }
    }
}
