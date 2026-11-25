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
        static float ShadowCooldown => 160f * Meta.Difficulty.Cooldowns;
        static float SkinStealerCooldown => 300f * Meta.Difficulty.Cooldowns;
        static float PartyGoerCooldown => 900f * Meta.Difficulty.Cooldowns;
        static float SmallCooldown => 45f * Meta.Difficulty.Cooldowns;
        static float LurkerCooldown => 200f * Meta.Difficulty.Cooldowns;
        static float HoundCooldown => 260f * Meta.Difficulty.Cooldowns;
        static float ClumpCooldown => 220f * Meta.Difficulty.Cooldowns;
        static float MothCooldown => 240f * Meta.Difficulty.Cooldowns;
        static float WretchCooldown => 240f * Meta.Difficulty.Cooldowns;
        const float HuntLength = 75f;

        public float Tension { get; private set; }
        public string Status { get; private set; } = "quiet";
        /// A creature caught this player.
        public event Action<PlayerAgent> CaughtPlayer;
        public event Action HuntSurvived, FacelingSeen;

        /// Co-op guests: only the phantom footsteps run here (the owner's game decides everything else).
        public bool StepsOnly;
        /// The owner's game has a Partygoer speak in a friend's head.
        public event Action<PlayerAgent, string, Vector3> RemoteWhisper;
        /// The owner picked a friend to hear footsteps behind them.
        public event Action<PlayerAgent> RemoteSteps;
        bool huntHadHunter;

        public NavGrid Grid { get; private set; }
        public bool Dark => streamer != null && (streamer.Blackout > 0.5f || streamer.NearestLight > 7f);
        public bool DarkAt(Vector3 p) => streamer != null && (streamer.Blackout > 0.5f || streamer.LightDistanceAt(p) > 5.5f);

        ChunkStreamer streamer;
        readonly List<Creature> creatures = new List<Creature>();
        /// What's alive on this level right now (may hold destroyed entries until the next tidy).
        public IReadOnlyList<Creature> Creatures => creatures;
        float visitTime, nextRoll, lastHunt = -999f, lastResident = -999f, lastSkinStealer = -999f, lastPartyGoer = -999f, lastSmall = -999f, lastLurker = -999f, lastHounds = -999f, lastClump = -999f, lastShadow = -999f, lastMoths = -999f, lastWretches = -999f;
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

        /// Level 3's Sanctums: whatever hunts you panics at the threshold and is gone; the lights come back.
        public void Sanctuary(Vector3 where, float radius)
        {
            if (!Net.Coop.Authority) return;
            bool fled = false;
            foreach (var c in creatures)
                if (c != null && Vector3.Distance(c.transform.position, where) < radius) { Destroy(c.gameObject); fled = true; }
            creatures.RemoveAll(c => c == null);
            if (huntEnds > 0f)
            {
                huntEnds = spawnHunterAt = -1f;
                if (streamer != null) streamer.Blackout = 0f;
                FirstPersonController.Chased = false;
                Status = "quiet";
                fled = true;
            }
            Tension = 0f;
            if (fled) UI.Captions.Say("[something shrieks and scrambles away]");
        }

        /// Something you did woke the dark (Level 6's light switch): a hunt starts now.
        public void ForceHunt()
        {
            if (!Net.Coop.Authority || huntEnds > 0f || streamer == null) return;
            visitTime = Mathf.Max(visitTime, Grace);
            StartHunt();
        }

        /// An Exterminator hit a Smiler: no hunts or lurkers for a good while.
        public void Numbed(Creature who)
        {
            lastHunt = Time.time;
            lastLurker = Time.time + 300f * Meta.Difficulty.Cooldowns;
            Tension = 0f;
        }

        public void Caught(Creature by, PlayerAgent who)
        {
            if (who == null) return;
            who.LoseSanity(0.2f);
            CaughtPlayer?.Invoke(who);
        }

        public void Whisper(PlayerAgent who, string line, Vector3? from = null)
        {
            if (who == null) return;
            var at = from ?? who.Eyes;
            if (who.IsLocal) WhisperHere(line, at);
            else RemoteWhisper?.Invoke(who, line, at);
        }

        /// A voice in your own head: the words always show (you aren't hearing them with your ears).
        public void WhisperHere(string line, Vector3 at)
        {
            UI.Captions.Say(line, true);
            AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Whisper(), Players.Local != null ? Vector3.MoveTowards(Players.Local.Eyes, at, 3f) : at, 0.7f * Meta.AudioMix.Creatures);
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
            Grid.Uneven = streamer != null && streamer.Theme != null && streamer.Theme.id == "8";
            Grid.Stepped = streamer != null && streamer.Theme != null && streamer.Theme.id == "9";
            if (!Players.AnyAlive()) return;
            float dt = Time.deltaTime;
            visitTime += dt;
            creatures.RemoveAll(c => c == null);
            foreach (var c in creatures) if ((c.Kind == CreatureKind.Faceling || c.Kind == CreatureKind.FacelingChild) && Visible(c.transform.position)) FacelingSeen?.Invoke();

            // Tension builds in quiet, faster in the dark and when your mind is going.
            float dark = streamer != null && streamer.NearestLight > 7f ? 1f : 0f;
            float mind = 1f - Players.MeanSanity();
            // Level 4 is nearly empty of entities (Backrooms Wiki, survival difficulty 1): dread builds at a third of the pace.
            bool calm = streamer != null && streamer.Theme != null && streamer.Theme.id == "4";
            Tension = Mathf.Clamp01(Tension + dt * (1f / 480f + dark / 300f + mind / 600f) * Meta.Difficulty.Tension * (calm ? 0.3f : 1f));

            FollowPlayers();
            Hunt(dt);
            FirstPersonController.Chased = creatures.Exists(c => c != null && c.State == CreatureState.Chase && c.Target == Players.Local);
            PhantomSteps();
            DevKeys();

            if (Time.time < nextRoll) return;
            nextRoll = Time.time + 20f;
            if (huntEnds > 0f) return;
            int guests = creatures.FindAll(c => c != null && c.Kind == CreatureKind.PartyGoer && c.State != CreatureState.Leave).Count;
            if (streamer != null && streamer.Theme != null && streamer.Theme.id == "fun" && guests < 4 && visitTime > Grace * 0.3f && Time.time - lastPartyGoer > 60f * Meta.Difficulty.Cooldowns)
            {
                var host = Players.Pick(UnityEngine.Random.value);   // their home: a pack or two is always about
                if (host != null && UnityEngine.Random.value < 0.5f && SpawnParty(host.Position, UnityEngine.Random.Range(2, 4), 14, 30, 0f)) return;
            }
            if (guests > 0) return;
            float r = UnityEngine.Random.value;
            if (calm && UnityEngine.Random.value < 0.75f) return;   // and when something might happen, it usually doesn't
            if (RoamingParty() && visitTime > Grace && Tension > 0.45f && Time.time - lastPartyGoer > PartyGoerCooldown && r < 0.008f) SpawnRoaming();
            else if (visitTime > Grace && Tension > 0.45f && Time.time - lastHunt > HuntCooldown && r < 0.35f) StartHunt();
            else if (visitTime > Grace * 0.6f && Tension > 0.4f && Time.time - lastHounds > HoundCooldown && HoundChance() > UnityEngine.Random.value && !creatures.Exists(c => c != null && c.Kind == CreatureKind.Hound)) SpawnPack(PackSize());
            else if (visitTime > Grace * 0.4f && Tension > 0.3f && Time.time - lastClump > ClumpCooldown && ClumpChance() > UnityEngine.Random.value && !creatures.Exists(c => c != null && c.Kind == CreatureKind.Clump)) SpawnClump(UnityEngine.Random.value);
            else if (visitTime > Grace * 0.5f && Tension > 0.35f && Time.time - lastWretches > WretchCooldown && WretchChance() > UnityEngine.Random.value && !creatures.Exists(c => c is Wretch)) SpawnWretches(UnityEngine.Random.value);
            else if (visitTime > Grace * 0.4f && Time.time - lastMoths > MothCooldown && MothChance() > UnityEngine.Random.value && !creatures.Exists(c => c != null && Deathmoth.Is(c.Kind))) SpawnMoths();
            else if (visitTime > Grace * 0.5f && Tension > 0.3f && Time.time - lastLurker > LurkerCooldown && LurkerChance() > UnityEngine.Random.value) SpawnLurker();
            else if (!calm && visitTime > Grace * 0.5f && Tension > 0.3f && Time.time - lastSkinStealer > SkinStealerCooldown && SkinStealerChance() > UnityEngine.Random.value) SpawnRare(CreatureKind.SkinStealer, 8, 16);
            else if (visitTime > Grace * 0.35f && Tension > 0.35f && Time.time - lastResident > ResidentCooldown && r < 0.3f * FacelingChance()) SpawnResident();
            else if (visitTime > Grace * 0.4f && Time.time - lastShadow > ShadowCooldown && FacelingChance() > 0f && Players.Local != null && Players.Local.InDark && UnityEngine.Random.value < 0.25f) SpawnShadow();
            else if (visitTime > Grace * 0.2f && Tension > 0.2f && Time.time - lastSmall > SmallCooldown && r < 0.35f) StartSteps();
        }

        /// Keeps the path grid around whoever the creatures are nearest to or chasing, so they walk to where people
        /// are now (it used to be rebuilt only in a hunt, and everything else walked to where you'd been).
        void FollowPlayers()
        {
            if (huntEnds > 0f || creatures.Count == 0 || Time.time < nextGrid) return;
            var c = creatures.Find(x => x != null && x.State == CreatureState.Chase && x.Target != null) ?? creatures.Find(x => x != null && x.Target != null);
            if (c == null || !c.Target.Alive || Grid.TargetCell == Cell(c.Target.Position)) return;
            Grid.Build(c.Target.Position);
            nextGrid = Time.time + 0.35f;
        }

        // ---------- rare encounters ----------

        /// Level 4 stays calm, Level Fun has its own packs, and the levels without entities never see one.
        bool RoamingParty()
        {
            string id = streamer != null && streamer.Theme != null ? streamer.Theme.id : "";
            return id == "0" || id == "1" || id == "2" || id == "3" || id == "5" || id == "9";
        }

        /// A pack crossing the level on a hunt, its balloons and music ahead of it.
        void SpawnRoaming()
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null || !SpawnParty(who.Position, UnityEngine.Random.Range(2, 5), 16, 24, 90f)) return;
            var at = new List<Vector3>();
            for (int i = 0; i < 3; i++) { var p = Grid.RandomSpot(6, 16, q => !Visible(q)); if (p.HasValue) at.Add(p.Value); }
            Game.PartyScenes.Instance?.Announce(at);
        }

        /// A pack of Partygoers, together, out of sight, this many steps from a spot. False if there's nowhere.
        public bool SpawnParty(Vector3 near, int size, int min, int max, float patience)
        {
            if (Grid.TargetCell != Cell(near)) Grid.Build(near);
            var at = Grid.RandomSpot(min, max, p => !Visible(p)) ?? Grid.RandomSpot(min, 40, p => !Visible(p));
            if (!at.HasValue) return false;
            lastPartyGoer = Time.time;
            var pack = new List<Partygoer>();
            for (int i = 0; i < size; i++)
            {
                var p = at;
                if (i > 0) { p = null; for (int k = 0; k < 8 && p == null; k++) p = Grid.RandomSpot(0, 60, q => Vector3.Distance(q, at.Value) < 5f && !Visible(q) && pack.TrueForAll(o => Vector3.Distance(o.transform.position, q) > 0.9f)); }
                if (!p.HasValue) continue;
                var g = (Partygoer)Creature.Spawn(CreatureKind.PartyGoer, p.Value, this);
                g.Pack = pack;
                g.Patience = patience;
                pack.Add(g);
                creatures.Add(g);
                if (patience > 0f) g.Hear(near, 99f);   // it knows roughly where you are
            }
            return true;
        }

        float SkinStealerChance()
        {
            string level = streamer != null && streamer.Theme != null ? streamer.Theme.id : "";
            return level == "1" || level == "2" || level == "3" ? 0.04f : 0.004f;
        }

        /// Smilers waiting in naturally dark rooms. Wiki canon: thick on Levels 2, 3 and 9 (and in Level 8's caves),
        /// present on Level 1, only a rumour on Level 0, and absent from the levels whose pages list no Smilers.
        float LurkerChance()
        {
            switch (streamer != null && streamer.Theme != null ? streamer.Theme.id : "")
            {
                case "2": case "3": return 0.45f;
                case "9": return 0.4f;
                case "8": return 0.25f;
                case "1": return 0.18f;
                case "0": return 0.05f;
                default: return 0f;
            }
        }

        /// Hounds (wiki Entity 8): "majority" of levels. Packs on Levels 1-3 and 9; Level 4's page and Level 5's list
        /// only mention them, so they come alone there; Level 0 has the oldest sighting and little else.
        float HoundChance()
        {
            switch (streamer != null && streamer.Theme != null ? streamer.Theme.id : "")
            {
                case "2": case "3": return 0.3f;
                case "1": case "9": return 0.2f;
                case "4": case "5": return 0.12f;
                case "0": return 0.04f;
                default: return 0f;
            }
        }

        /// Clumps (wiki Entity 5): "majority of levels, especially 2 and 3"; Level 1's page lists them too.
        float ClumpChance()
        {
            switch (streamer != null && streamer.Theme != null ? streamer.Theme.id : "")
            {
                case "2": case "3": return 0.3f;
                case "1": return 0.18f;
                case "4": case "5": case "9": return 0.08f;
                case "0": return 0.03f;
                default: return 0f;
            }
        }

        /// One Clump somewhere ahead: buried in the floor (45%), crouched at a corner (35%) or in the ceiling ducts (20%).
        void SpawnClump(float roll)
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null) return;
            if (Grid.TargetCell != Cell(who.Position)) Grid.Build(who.Position);
            var start = roll < 0.45f ? CreatureState.Lurk : roll < 0.8f ? CreatureState.Stalk : CreatureState.Vent;
            System.Func<Vector3, bool> ok = p => !Visible(p);
            if (start == CreatureState.Stalk) ok = p => !Visible(p) && WallsAround(p) >= 2;   // tucked into a corner
            var at = Grid.RandomSpot(start == CreatureState.Vent ? 10 : 7, 20, ok) ?? Grid.RandomSpot(7, 20, p => !Visible(p));
            if (!at.HasValue) return;
            lastClump = Time.time;
            creatures.Add(Creature.Spawn(CreatureKind.Clump, at.Value, this, start));
        }

        /// Wretches (wiki Entity 15): "the majority of levels", thickest where nobody goes. Level 8's caves have whole hordes.
        float WretchChance()
        {
            switch (streamer != null && streamer.Theme != null ? streamer.Theme.id : "")
            {
                case "8": return 0.4f;
                case "2": case "3": return 0.18f;
                case "1": case "9": return 0.12f;
                case "5": return 0.08f;
                default: return 0f;
            }
        }

        /// A horde on Level 8, otherwise one to three somewhere dark; some of them lie there like the dead (roll below 0.45, 0.25 on Level 8).
        public void SpawnWretches(float roll)
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null) return;
            if (Grid.TargetCell != Cell(who.Position)) Grid.Build(who.Position);
            bool caves = streamer.Theme.id == "8";
            int size = caves ? UnityEngine.Random.Range(3, 7) : UnityEngine.Random.Range(1, 4);
            bool dead = roll < (caves ? 0.25f : 0.45f);
            System.Func<Vector3, bool> room = p => !dead || !Physics.CheckSphere(p + Vector3.up, 0.85f, ~0, QueryTriggerInteraction.Ignore);   // lying down takes space
            var at = Grid.RandomSpot(10, 24, p => !Visible(p) && streamer.LightDistanceAt(p) > 5f && room(p)) ?? Grid.RandomSpot(12, 24, p => !Visible(p) && room(p));
            if (!at.HasValue) return;
            lastWretches = Time.time;
            var horde = new List<Wretch>();
            for (int i = 0; i < size; i++)
            {
                var p = at;
                if (i > 0) { p = null; for (int k = 0; k < 8 && p == null; k++) p = Grid.RandomSpot(0, 60, q => Vector3.Distance(q, at.Value) < 5f && !Visible(q) && room(q) && horde.TrueForAll(o => Vector3.Distance(o.transform.position, q) > (dead ? 1.6f : 0.9f))); }
                if (p.HasValue) AddWretch(CreatureKind.Wretch, p.Value, horde, dead ? CreatureState.Lurk : CreatureState.Stalk);
            }
            // Now and then the worst of them comes too, where there's room for it.
            if (!dead && (caves || streamer.Theme.id == "3") && UnityEngine.Random.value < 0.18f)
            {
                var big = Grid.RandomSpot(8, 60, q => Vector3.Distance(q, at.Value) < 9f && !Visible(q) && !Physics.CheckSphere(q + Vector3.up * 1.1f, 0.9f, ~0, QueryTriggerInteraction.Ignore));
                if (big.HasValue) AddWretch(CreatureKind.WretchMass, big.Value, horde, CreatureState.Stalk);
            }
        }

        void AddWretch(CreatureKind kind, Vector3 at, List<Wretch> horde, CreatureState start)
        {
            var w = (Wretch)Creature.Spawn(kind, at, this, start);
            if (start == CreatureState.Lurk) w.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f);
            w.Horde = horde;
            horde.Add(w);
            creatures.Add(w);
        }

        /// Deathmoths (wiki Entity 4): Level 5 is one of their central hives, Level 3's swarms are the aggressive ones,
        /// and a few harmless males turn up round the lights of Levels 1, 2, 8 and 9.
        float MothChance()
        {
            switch (streamer != null && streamer.Theme != null ? streamer.Theme.id : "")
            {
                case "5": return 0.35f;
                case "3": return 0.25f;
                case "1": case "2": case "8": case "9": return 0.15f;
                default: return 0f;
            }
        }

        void SpawnMoths()
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null) return;
            lastMoths = Time.time;
            string id = streamer.Theme.id;
            if (id == "5" && SpawnHive(who, UnityEngine.Random.value < 0.2f)) return;
            if (Grid.TargetCell != Cell(who.Position)) Grid.Build(who.Position);
            var at = Grid.RandomSpot(10, 22, p => !Visible(p));
            if (!at.HasValue) return;
            creatures.Add(Creature.Spawn(id == "3" || id == "5" ? CreatureKind.MothSwarm : CreatureKind.MothMales, at.Value, this));
        }

        /// A hive on the back wall of a hotel room nobody is looking into: dead lights, the swarm asleep on it, a Praetorian
        /// on guard, and now and then the Empress. False if there's no room to put one in.
        bool SpawnHive(PlayerAgent who, bool empress)
        {
            HotelDoor best = null;
            Vector3 wall = default, normal = default;
            float bestSize = 0f;
            foreach (var door in FindObjectsByType<HotelDoor>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(door.transform.position, who.Position);
                if (d < 9f || d > 45f || Mathf.Abs(door.transform.position.y - who.Position.y) > 1.5f) continue;
                var inside = door.transform.position - door.transform.forward * 1.2f + Vector3.up * 1.3f;   // the room is behind the door (+z is the corridor)
                if (Visible(inside - Vector3.up * 1.2f) || Physics.CheckSphere(inside, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(inside, -door.transform.forward, out var hit, 9f, ~0, QueryTriggerInteraction.Ignore) || hit.distance < 1.6f) continue;
                // The roomiest wins: the hive fills whatever room it's in.
                var probe = hit.point + hit.normal * 0.5f;
                var along = Vector3.Cross(Vector3.up, hit.normal);
                float Free(Vector3 dir) => Physics.Raycast(probe, dir, out var h2, 4f, ~0, QueryTriggerInteraction.Ignore) ? h2.distance : 4f;
                float span = Free(along) + Free(-along), size = Mathf.Min(5.2f, span) + Mathf.Min(6f, hit.distance) * 0.5f;
                if (span < 2.2f || size <= bestSize) continue;
                best = door; bestSize = size; wall = hit.point; normal = Flat(hit.normal).normalized;
            }
            if (best == null) return false;
            wall.y = who.Position.y;
            var hive = (MothHive)Creature.Spawn(CreatureKind.MothHive, wall, this, CreatureState.Lurk);
            hive.Settle(normal);
            hive.Attach(best);
            hive.Pocket = streamer.AddDarkPocket(wall, MothHive.PocketRadius);
            creatures.Add(hive);
            void Brood(CreatureKind k, Vector3 at)
            {
                var m = (Deathmoth)Creature.Spawn(k, at, this, CreatureState.Lurk);
                m.Hive = hive;
                m.transform.rotation = Quaternion.LookRotation(normal);
                hive.Brood.Add(m);
                creatures.Add(m);
            }
            var front = wall + normal * (hive.Depth + 0.6f);
            var across = Vector3.Cross(Vector3.up, normal);
            Brood(CreatureKind.MothSwarm, front + across * 0.3f);
            Brood(CreatureKind.MothSwarm, front - across * 0.3f);
            Brood(CreatureKind.Praetorian, front + across * 0.9f);
            if (empress) Brood(CreatureKind.MothEmpress, front - across * 0.8f);
            return true;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static int WallsAround(Vector3 p)
        {
            int n = 0;
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (Physics.Raycast(p + Vector3.up, d, 1.2f, ~0, QueryTriggerInteraction.Ignore)) n++;
            return n;
        }

        int PackSize()
        {
            string id = streamer != null && streamer.Theme != null ? streamer.Theme.id : "";
            return id == "1" || id == "2" || id == "3" || id == "9" ? UnityEngine.Random.Range(2, 4) : 1;
        }

        /// A pack, each from its own direction somewhere out of sight; they find each other by barking.
        void SpawnPack(int size)
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null) return;
            if (Grid.TargetCell != Cell(who.Position)) Grid.Build(who.Position);
            var pack = new List<Hound>();
            for (int i = 0; i < size; i++)
            {
                var at = Grid.RandomSpot(14, 28, p => !Visible(p) && pack.TrueForAll(h => Vector3.Distance(h.transform.position, p) > 6f));
                if (!at.HasValue) continue;
                var h = (Hound)Creature.Spawn(CreatureKind.Hound, at.Value, this);
                h.Pack = pack;
                pack.Add(h);
                creatures.Add(h);
            }
            if (pack.Count == 0) return;
            lastHounds = Time.time;
            Tension = Mathf.Max(0f, Tension - 0.3f);
            UI.Captions.Say("[growling, somewhere close]");
        }

        void SpawnLurker()
        {
            if (creatures.Exists(c => c != null && c.Kind == CreatureKind.Smiler)) return;
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null) return;
            if (Grid.TargetCell != Cell(who.Position)) Grid.Build(who.Position);
            // Somewhere ahead, not yet in anyone's view: in real darkness if there is any (Level 9's night, the caves),
            // otherwise the lights around its spot die with it there (a dead light ahead means danger).
            var at = Grid.RandomSpot(9, 22, p => !Visible(p) && streamer.LightDistanceAt(p) > 7f) ?? Grid.RandomSpot(10, 22, p => !Visible(p));
            if (!at.HasValue) return;
            lastLurker = Time.time;
            var pocket = streamer.LightDistanceAt(at.Value) > 7f ? null : streamer.AddDarkPocket(at.Value, Smiler.PocketRadius);
            var smiler = (Smiler)Creature.Spawn(CreatureKind.Smiler, at.Value, this, CreatureState.Lurk);
            smiler.Pocket = pocket;
            if (pocket != null) UI.Captions.Say("[a light dies somewhere ahead]");
            creatures.Add(smiler);
        }

        void SpawnRare(CreatureKind kind, int min, int max)
        {
            var who = Players.Pick(UnityEngine.Random.value);
            if (who == null) return;
            if (Grid.TargetCell != Cell(who.Position)) Grid.Build(who.Position);
            var at = Grid.RandomSpot(min, max, p => !Visible(p));
            if (!at.HasValue) return;
            if (kind == CreatureKind.SkinStealer) lastSkinStealer = Time.time;
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
            if (kind == CreatureKind.Hound) { SpawnPack(PackSize() < 2 ? 2 : PackSize()); return; }
            if (kind == CreatureKind.Clump) { SpawnClump(UnityEngine.Random.value); return; }
            if (kind == CreatureKind.MothHive) { SpawnHive(near, true); return; }
            if (kind == CreatureKind.Wretch) { SpawnWretches(UnityEngine.Random.value); return; }
            if (kind == CreatureKind.WretchMass) { var big = Grid.RandomSpot(6, 20, p => !Visible(p)); if (big.HasValue) AddWretch(kind, big.Value, new List<Wretch>(), CreatureState.Stalk); return; }
            if (kind == CreatureKind.PartyGoer) { if (RoamingParty()) SpawnRoaming(); else SpawnParty(near.Position, 3, 10, 20, 0f); return; }
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

        /// Facelings (wiki Entity 9) live wherever people built things: Levels 0 (rarely), 1, 2, 4, 5, 9, Fun and 188.
        /// Level 3 has the Evolved instead. Elsewhere none.
        float FacelingChance()
        {
            switch (streamer != null && streamer.Theme != null ? streamer.Theme.id : "")
            {
                case "1": case "2": case "4": case "5": case "9": case "fun": case "188": case "3": return 1f;
                case "0": return 0.35f;
                default: return 0f;
            }
        }

        /// A small group going about their routines (2-4 adults, sometimes a child), or on Level 3 a hunting party of Evolved.
        void SpawnResident()
        {
            var near = Players.Pick(UnityEngine.Random.value);   // shows itself to one of the living
            if (near == null) return;
            if (Grid.TargetCell != Cell(near.Position)) Grid.Build(near.Position);
            var at = Grid.RandomSpot(12, 22, p => !Visible(p));
            if (!at.HasValue) return;
            lastResident = Time.time;
            Tension = Mathf.Max(0f, Tension - 0.25f);
            bool evolved = streamer != null && streamer.Theme != null && streamer.Theme.id == "3";
            int adults = evolved ? UnityEngine.Random.Range(2, 4) : UnityEngine.Random.Range(2, 5);
            var group = new List<Faceling>();
            for (int i = 0; i < adults; i++)
            {
                var p = at.Value + (i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 137f, 0f) * Vector3.forward * UnityEngine.Random.Range(1.2f, 2.5f));
                if (i > 0 && Physics.CheckSphere(p + Vector3.up, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var f = (Faceling)Creature.Spawn(evolved ? CreatureKind.EvolvedFaceling : CreatureKind.Faceling, p, this);
                f.Group = group; group.Add(f); creatures.Add(f);
            }
            if (!evolved && group.Count > 0 && UnityEngine.Random.value < 0.35f)
            {
                var parent = group[0];
                var c = (Faceling)Creature.Spawn(CreatureKind.FacelingChild, parent.transform.position + parent.transform.right * 0.7f, this);
                c.Group = group; c.Parent = parent; group.Add(c); creatures.Add(c);
            }
        }

        /// A Shadow Faceling somewhere in the dark near someone standing in the dark.
        void SpawnShadow()
        {
            var near = Players.Local;
            if (Grid.TargetCell != Cell(near.Position)) Grid.Build(near.Position);
            var at = Grid.RandomSpot(8, 18, p => !Visible(p) && DarkAt(p + Vector3.up));
            if (!at.HasValue) return;
            lastShadow = Time.time;
            creatures.Add(Creature.Spawn(CreatureKind.ShadowFaceling, at.Value, this));
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
