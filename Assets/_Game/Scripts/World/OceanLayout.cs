using UnityEngine;

namespace Backrooms.World
{
    /// Level 7 "Thalassophobia" (Backrooms Wiki, https://backrooms-wiki.wikidot.com/level-7 , CC BY-SA 3.0, rewritten 2026-11-22):
    /// an endless ocean under a concrete ceiling. You arrive in a furnished room built sideways into that ceiling; its
    /// door opens onto nothing, 4.5 m above the water. Below: the Daylight zone (0-30 m), the Twilight zone (30-80 m,
    /// skeletons), the Midnight zone (80-150 m, more bones) and the Abyss (tar). Our ocean is compressed to about 200 m.
    ///
    /// Landmarks: Fort Surrender, an island outpost of tar and furniture (its diving gear doubles your breath); concrete
    /// pillars rising from the deep to the ceiling, with ledges at the waterline; sunken rooms; the cave in a sunken
    /// mountain in the Midnight zone (the way to Level 8, marked by a light); and a ring of pillars and pipes round a stone
    /// platform with a wooden trapdoor (Level 9).
    public static class OceanLayout
    {
        public const float OceanWaterLevel = 4f, OceanCeilingHeight = 28f;
        public const float RoomFloor = OceanWaterLevel + 4.5f;   // the entrance room's door, 4.5 m above the water

        /// Where you stand when you arrive (and wake after drowning): inside the sideways room.
        public static readonly Vector3 EntranceSpawn = new Vector3(-0.7f, RoomFloor + 0.05f, 0.9f);   // clear of the hole in the floor

        // ---------- world-fixed landmarks (pure functions of the level seed) ----------

        static Vector2 Dir(uint seed, int salt)
        {
            float a = WorldHash.Value(seed, salt, 1, 0x7A) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// The sunken mountain with the cave to Level 8: 230-300 m out.
        public static Vector2 MountainAt(uint seed) => Dir(seed, 1) * Mathf.Lerp(230f, 300f, WorldHash.Value(seed, 2, 2, 0x7B));
        /// The cave mouth, on the mountain's flank facing the entrance, 88 m down (just into the Midnight zone).
        public static Vector3 CaveMouth(uint seed)
        {
            var m = MountainAt(seed);
            var toward = -m.normalized;
            var p = m + toward * 26f;
            return new Vector3(p.x, OceanWaterLevel - 88f, p.y);
        }
        /// Fort Surrender: 90-140 m out, a different way.
        public static Vector2 FortAt(uint seed) => Dir(seed, 3) * Mathf.Lerp(90f, 140f, WorldHash.Value(seed, 4, 4, 0x7C));
        /// The ring of pillars round the wooden trapdoor (Level 9): about 150 m west of the entrance, as the wiki says.
        public static Vector2 RingAt(uint seed) => new Vector2(-150f, (WorldHash.Value(seed, 5, 5, 0x7D) - 0.5f) * 40f);

        /// The sea floor's depth below the surface at (x, z): 45-195 m, with the landmarks shaped in.
        public static float DepthAt(uint seed, float x, float z)
        {
            float o = (seed % 997) * 0.37f;
            float n = Mathf.PerlinNoise(x / 190f + o, z / 190f + o * 0.7f) * 0.75f + Mathf.PerlinNoise(x / 55f + o, z / 55f - o) * 0.25f;
            float d = 45f + 150f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((n - 0.2f) / 0.6f));
            var p = new Vector2(x, z);
            float dm = Vector2.Distance(p, MountainAt(seed));
            if (dm < 70f) d = Mathf.Min(d, Mathf.Lerp(62f, 195f, Mathf.Pow(dm / 70f, 1.6f)));   // the sunken mountain
            float dr = Vector2.Distance(p, RingAt(seed));
            if (dr < 16f) d = 40f;                                                             // the ring's stone platform
            else if (dr < 34f) d = Mathf.Min(d, Mathf.Lerp(40f, 120f, (dr - 16f) / 18f));
            float df = Vector2.Distance(p, FortAt(seed));
            if (df < 40f) d = Mathf.Min(d, Mathf.Lerp(4f, 120f, df / 40f));                     // the fort's shallows
            if (p.magnitude < 30f) d = Mathf.Max(d, 70f);                                       // deep water under the room: no landing on rock
            // Rock: ridges and gullies, smoothed away under the ring's platform.
            float rough = Mathf.Clamp01((dr - 17f) / 10f);
            float ridge = 1f - Mathf.Abs(RockMesh.Noise(x / 11f, 3.7f, z / 11f));
            return d - rough * (ridge * ridge * 3.4f + RockMesh.Noise(x / 4.3f, 9.1f, z / 4.3f) * 0.8f - 1.6f);
        }

        /// A whale-sized skeleton, 95-140 m down.
        public static Vector2 WhaleAt(uint seed)
        {
            for (int k = 0; k < 60; k++)
            {
                var p = Dir(seed, 60 + k) * Mathf.Lerp(90f, 220f, WorldHash.Value(seed, k, 6, 0x7E));
                float d = DepthAt(seed, p.x, p.y);
                if (d > 95f && d < 140f && Vector2.Distance(p, MountainAt(seed)) > 45f && Vector2.Distance(p, RingAt(seed)) > 40f) return p;
            }
            return Dir(seed, 60) * 150f;
        }

        public static ChunkLayout Generate(uint seed, int cx, int cz, int layer, LevelTheme theme)
        {
            var L = new ChunkLayout();
            float s = MazeLayout.ChunkSize, x0 = cx * s, z0 = cz * s;
            var rng = new WorldRng(WorldHash.Hash(seed, cx, cz, 0x7700));
            var bed = new SurfaceRef(Surface.Floor, 0);
            var concrete = new SurfaceRef(Surface.Concrete, 0);
            var ceiling = new SurfaceRef(Surface.Ceiling, 0);
            void Box(Vector3 c, Vector3 size, SurfaceRef m, bool solid = true) => L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, size), m, BoxMeshBuilder.Faces.All, solid));
            void Prop(PropKind k, Vector3 at, float yaw, int v = 0) => L.Props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Variant = v, Ceiling = OceanCeilingHeight });
            void Light(Vector3 at, float range, Color tint, int salt) => L.Panels.Add(new PanelSpec { Position = at, Bare = true, Emitter = true, Range = range, Tint = tint, State = FixtureState.Lit,
                                                                                                     Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.z), salt) });

            L.Quads.Add(new SurfaceQuad { Area = new Rect(x0, z0, s, s), Height = OceanCeilingHeight, FacingUp = false, Surface = ceiling });

            // The sea floor: smooth rock. The cave's tunnel is cut into the mountain's flank.
            var cave = CaveMouth(seed);
            var caveIn = new Vector2(cave.x, cave.z).normalized;   // into the mountain (away from the entrance)
            const int n = RockMesh.Samples;
            var bedY = new float[n, n];
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < n; i++)
            for (int k = 0; k < n; k++)
            {
                float h = OceanWaterLevel - DepthAt(seed, x0 + i - 1, z0 + k - 1);
                bedY[i, k] = h;
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
            bool caveHere = Vector2.Distance(new Vector2(cave.x, cave.z), new Vector2(x0 + s / 2f, z0 + s / 2f)) < 48f;
            if (caveHere) low = Mathf.Min(low, cave.y - 3f);
            int y0 = Mathf.FloorToInt(low) - 3, ny = Mathf.CeilToInt(high) + 4 - y0;
            var field = new float[n * ny * n];
            var mouth2 = new Vector3(cave.x, cave.y + 0.8f, cave.z);
            var tunnelDir = new Vector3(caveIn.x, 0f, caveIn.y);
            for (int i = 0; i < n; i++)
            for (int j = 0; j < ny; j++)
            for (int k = 0; k < n; k++)
            {
                var p = new Vector3(x0 + i - 1, y0 + j, z0 + k - 1);
                float rock = bedY[i, k] - p.y;
                if (Mathf.Abs(rock) < 2.5f) rock += RockMesh.Noise(p.x / 5f, p.y / 5f, p.z / 5f) * 0.9f * (1f - Mathf.Abs(rock) / 2.5f) * RockRoughness(seed, p.x, p.z);   // overhangs and ledges
                if (caveHere)
                {
                    float t = Mathf.Clamp(Vector3.Dot(p - mouth2, tunnelDir), -10f, 16f);
                    float air = Vector3.Distance(p, mouth2 + tunnelDir * t) - 2.8f + RockMesh.Noise(p.x / 2f, p.y / 2f, p.z / 2f) * 0.35f;
                    rock = Mathf.Min(rock, Mathf.Max(air, cave.y - 1.5f - p.y));
                }
                field[RockMesh.Index(i, j, k, ny)] = rock;
            }
            var bedMesh = new RockMesh(bed);
            bedMesh.SurfaceNets(field, ny, new Vector3(x0, y0, z0));
            L.Rocks.Add(bedMesh);
            var kelp = new RockMesh(new SurfaceRef(Surface.Floor, 2), false);
            var bone = new RockMesh(new SurfaceRef(Surface.Floor, 3));
            L.Rocks.Add(kelp);
            L.Rocks.Add(bone);
            Vector3 Bed(float x, float z) => new Vector3(x, OceanWaterLevel - DepthAt(seed, x, z), z);

            // What lies on the bottom, by zone: kelp, dead coral and wreckage, then bones, then tar.
            for (int i = 0; i < 34; i++)
            {
                var on = Bed(x0 + rng.Value() * s, z0 + rng.Value() * s);
                var flat = new Vector2(on.x, on.z);
                float depth = OceanWaterLevel - on.y, r = rng.Value();
                if (Vector2.Distance(flat, RingAt(seed)) < 17f || Vector2.Distance(flat, new Vector2(cave.x, cave.z)) < 8f || depth < 6f) continue;
                if (r < 0.3f) { float big = 0.5f + rng.Value() * rng.Value() * 3.2f; bedMesh.Blob(on + Vector3.up * big * 0.25f, new Vector3(big, big * 0.75f, big * 1.2f), Quaternion.Euler(rng.Value() * 40f, rng.Value() * 360f, rng.Value() * 40f), 0.35f, 12); }
                else if (depth < 85f && r < 0.62f) Kelp(kelp, rng, on, Mathf.Min(depth - 8f, 9f + rng.Value() * 20f));
                else if (depth < 120f && r < 0.8f) Coral(bone, rng, on);
                else if (depth > 30f && depth < 150f && r < (depth > 80f ? 0.95f : 0.87f)) Prop(PropKind.Bones, on, r * 3000f, (int)(r * 1000f));
                else if (depth < 80f && r > 0.95f)
                {
                    var junk = new[] { PropKind.Sofa, PropKind.Armchair, PropKind.DiningTable, PropKind.Bookshelf, PropKind.FilingCabinet, PropKind.Bed };
                    L.Props.Add(new PropSpec { Kind = junk[rng.Range(0, junk.Length)], Pose = Matrix4x4.TRS(on - Vector3.up * 0.15f, Quaternion.Euler(rng.Value() * 60f - 30f, rng.Value() * 360f, rng.Value() * 60f - 30f), Vector3.one), Variant = rng.Range(0, 100), Ceiling = OceanCeilingHeight });
                }
                else if (depth >= 150f && r > 0.62f)
                {
                    Prop(PropKind.TarMound, on, r * 3000f, (int)(r * 1000f));
                    if (r > 0.9f) L.Specials.Add(new SpecialSpec { Kind = SpecialKind.TarPool, Pose = Matrix4x4.Translate(on + Vector3.up * 0.5f), Size = new Vector3(3.5f, 1f, 3.5f), Id = 1 });   // it boils, even under all this water
                }
            }
            // Boats, mostly in the shallows round the fort; and now and then far out.
            if (rng.Value() < 0.05f || Vector2.Distance(FortAt(seed), new Vector2(x0 + s / 2f, z0 + s / 2f)) < 40f)
            {
                var on = Bed(x0 + 5f + rng.Value() * 22f, z0 + 5f + rng.Value() * 22f);
                if (on.y < OceanWaterLevel - 9f && Vector2.Distance(FortAt(seed), new Vector2(on.x, on.z)) > 13f)
                    L.Props.Add(new PropSpec { Kind = PropKind.BoatWreck, Pose = Matrix4x4.TRS(on - Vector3.up * 0.2f, Quaternion.Euler(rng.Value() * 16f - 8f, rng.Value() * 360f, 12f + rng.Value() * 22f), Vector3.one), Ceiling = OceanCeilingHeight });
            }
            var whale = WhaleAt(seed);
            if (InChunk(whale, x0, z0, s))
            {
                Whale(bone, seed, whale, Bed);
                foreach (float dx in new[] { -4f, 4f }) Light(Bed(whale.x + dx, whale.y) + Vector3.up * 3f, 16f, new Color(0.25f, 0.55f, 1f), 0x7D0 + (int)dx);   // something in there still glows
            }
            // Pale fish, above the bottom in the lit and half-lit water.
            if (rng.Value() < 0.45f)
            {
                var on = Bed(x0 + rng.Value() * s, z0 + rng.Value() * s);
                float y = Mathf.Lerp(Mathf.Max(on.y + 4f, -95f), -4f, rng.Value());
                if (y > on.y + 3f) L.Specials.Add(new SpecialSpec { Kind = SpecialKind.FishSchool, Pose = Matrix4x4.Translate(new Vector3(on.x, y, on.z)), Size = Vector3.one * 5f, Id = rng.Range(0, 100000) });
            }

            // Concrete pillars from the deep to the ceiling, a ledge and a ladder at the waterline: somewhere to rest.
            int gx = Mathf.FloorToInt(x0 / 96f), gz = Mathf.FloorToInt(z0 / 96f);
            var pillar = new Vector2((gx + 0.3f + WorldHash.Value(seed, gx, gz, 0x77D) * 0.4f) * 96f, (gz + 0.3f + WorldHash.Value(seed, gz, gx, 0x77E) * 0.4f) * 96f);
            if (pillar.x >= x0 && pillar.x < x0 + s && pillar.y >= z0 && pillar.y < z0 + s && pillar.magnitude > 40f)
            {
                float foot = OceanWaterLevel - DepthAt(seed, pillar.x, pillar.y) - 6f;
                Box(new Vector3(pillar.x, (foot + OceanCeilingHeight) / 2f, pillar.y), new Vector3(5f, OceanCeilingHeight - foot, 5f), concrete);
                Box(new Vector3(pillar.x, OceanWaterLevel + 0.45f, pillar.y), new Vector3(8f, 0.3f, 8f), concrete);   // the ledge, just clear of the water
                for (int k = 0; k < 3; k++) Box(new Vector3(pillar.x + 3.75f, OceanWaterLevel - 0.25f - k * 0.3f, pillar.y), new Vector3(0.5f, 0.3f, 1.4f), concrete);   // steps up out of the water
                Light(new Vector3(pillar.x, OceanWaterLevel + 3.2f, pillar.y + 2.6f), 14f, new Color(0.65f, 0.85f, 1f), 0x77F);
            }

            // Sunken rooms: concrete shells on the bottom where it isn't too deep, a doorway open to the water.
            if (rng.Value() < 0.18f)
            {
                float rx = x0 + 6f + rng.Value() * (s - 12f), rz = z0 + 6f + rng.Value() * (s - 12f);
                float d = DepthAt(seed, rx, rz);
                if (d < 120f && new Vector2(rx, rz).magnitude > 40f)
                {
                    float y = OceanWaterLevel - d;
                    Box(new Vector3(rx, y + 3.2f, rz), new Vector3(8f, 0.4f, 6f), concrete);
                    Box(new Vector3(rx - 3.8f, y + 1.6f, rz), new Vector3(0.4f, 3.2f, 6f), concrete);
                    Box(new Vector3(rx + 3.8f, y + 1.6f, rz), new Vector3(0.4f, 3.2f, 6f), concrete);
                    Box(new Vector3(rx, y + 1.6f, rz + 2.8f), new Vector3(8f, 3.2f, 0.4f), concrete);
                    Box(new Vector3(rx - 2.6f, y + 1.6f, rz - 2.8f), new Vector3(2.8f, 3.2f, 0.4f), concrete);
                    Box(new Vector3(rx + 2.6f, y + 1.6f, rz - 2.8f), new Vector3(2.8f, 3.2f, 0.4f), concrete);
                    Prop(PropKind.Bookshelf, new Vector3(rx + 1f, y, rz + 2.5f), 180f);
                    Prop(PropKind.WoodChair, new Vector3(rx - 1f, y, rz), rng.Value() * 360f);
                    if (rng.Value() < 0.5f) Prop(PropKind.Bones, new Vector3(rx, y, rz + 0.5f), rng.Value() * 360f, rng.Range(0, 100));
                }
            }

            if (cx == 0 && cz == 0) EntranceRoom(L, seed);
            CaveExit(L, seed, cave, caveIn, x0, z0, s);
            Fort(L, seed, x0, z0, s);
            Ring(L, seed, x0, z0, s);
            return L;
        }

        static float RockRoughness(uint seed, float x, float z) => Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), RingAt(seed)) - 17f) / 10f);

        /// Weed as tall as a house: a wavering stem with blades all the way up.
        static void Kelp(RockMesh m, WorldRng rng, Vector3 foot, float tall)
        {
            if (tall < 2f) return;
            int stems = 2 + rng.Range(0, 3);
            for (int s = 0; s < stems; s++)
            {
                var path = new System.Collections.Generic.List<Vector3>();
                var radius = new System.Collections.Generic.List<float>();
                var at = foot + new Vector3(rng.Value() - 0.5f, 0f, rng.Value() - 0.5f) * 1.6f;
                float h = tall * (0.6f + rng.Value() * 0.4f), phase = rng.Value() * 6f;
                int steps = Mathf.CeilToInt(h / 1.2f);
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    path.Add(at + new Vector3(Mathf.Sin(phase + t * 5f) * t * 1.6f, t * h, Mathf.Cos(phase * 1.7f + t * 4f) * t * 1.3f));
                    radius.Add(Mathf.Lerp(0.07f, 0.015f, t));
                }
                m.Tube(path, radius, 4);
                for (int i = 1; i < steps; i++)
                {
                    float a = phase + i * 2.4f;
                    var o = new Vector3(Mathf.Cos(a), 0.45f, Mathf.Sin(a));
                    if (i > 1) m.Leaf(path[i], path[i] + o * (1.5f + rng.Value() * 1.1f), new Vector3(-o.z, 0.3f, o.x) * 0.27f);
                }
            }
        }

        /// Dead coral: pale branches.
        static void Coral(RockMesh m, WorldRng rng, Vector3 foot)
        {
            int arms = 3 + rng.Range(0, 4);
            for (int i = 0; i < arms; i++)
            {
                float a = i * 6.283f / arms + rng.Value(), len = 0.5f + rng.Value() * 1.1f;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var mid = foot + o * len * 0.35f + Vector3.up * len * 0.5f;
                var tip = mid + o * len * 0.25f + Vector3.up * len * 0.6f;
                m.Tube(new[] { foot - Vector3.up * 0.2f, mid, tip }, new[] { 0.13f, 0.09f, 0.02f }, 6, 0.4f);
                for (int b = 0; b < 2; b++)
                {
                    var q = new Vector3(rng.Value() - 0.5f, 0.5f, rng.Value() - 0.5f);
                    m.Tube(new[] { mid, mid + q * len * 0.7f }, new[] { 0.06f, 0.015f }, 5, 0.4f);
                }
            }
        }

        /// The skeleton of something far bigger than a whale ought to be: skull, a 26 m spine, ribs you can swim between.
        static void Whale(RockMesh m, uint seed, Vector2 at, System.Func<float, float, Vector3> bed)
        {
            float yaw = WorldHash.Value(seed, 12, 12, 0x7F) * 6.283f;
            var along = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var side = new Vector3(-along.z, 0f, along.x);
            Vector3 Spine(float t) { var f = new Vector3(at.x, 0f, at.y) + along * ((t - 0.35f) * 26f); return bed(f.x, f.z) + Vector3.up * Mathf.Lerp(1.6f, 0.4f, t); }
            for (int i = 0; i <= 26; i++)
            {
                float t = i / 26f, r = Mathf.Lerp(0.55f, 0.14f, t);
                var c = Spine(t);
                m.Blob(c, new Vector3(r, r, r) * 0.85f, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f), 0.2f, 8);
                m.Tube(new[] { c, c + Vector3.up * r * 2.6f }, new[] { r * 0.4f, r * 0.1f }, 5, 0.2f);
                if (i < 2 || i > 14) continue;
                float big = Mathf.Sin((i - 1) / 14f * Mathf.PI) * 2.6f + 1.4f;
                foreach (float k in new[] { -1f, 1f })
                {
                    if (WorldHash.Value(seed, i, (int)k, 0x80) < 0.15f) continue;   // a rib gone
                    var path = new Vector3[8];
                    var radius = new float[8];
                    for (int p = 0; p < 8; p++)
                    {
                        float a = p / 7f * 2.5f;
                        path[p] = c + side * (k * Mathf.Sin(a) * big) + Vector3.up * ((1f - Mathf.Cos(a * 1.05f)) * big * 0.75f) - along * (p * 0.06f);
                        radius[p] = Mathf.Lerp(0.2f, 0.06f, p / 7f);
                    }
                    m.Tube(path, radius, 7, 0.15f);
                }
            }
            var head = Spine(0f) - along * 3f;
            m.Blob(head + Vector3.up * 0.6f, new Vector3(2.4f, 1.2f, 1.4f), Quaternion.LookRotation(side), 0.2f, 14);
            foreach (float k in new[] { -1f, 1f })   // the long jaws
                m.Tube(new[] { head + side * k * 1.2f, head - along * 3f + side * k * 1.5f + Vector3.up * 0.2f, head - along * 6.5f + side * k * 0.25f }, new[] { 0.45f, 0.35f, 0.1f }, 8, 0.2f);
        }

        static bool InChunk(Vector2 p, float x0, float z0, float s) => p.x >= x0 && p.x < x0 + s && p.y >= z0 && p.y < z0 + s;

        /// The room you arrive in, built on its side into the ceiling. The carpet runs up one wall with the bookcase,
        /// table, chair and lamp fixed to it; the ceiling tiles are the opposite wall; the door is a hole in your floor,
        /// and through it, 4.5 m down, the black water.
        static void EntranceRoom(ChunkLayout L, uint seed)
        {
            float f = RoomFloor, h = 3.4f;
            const float w = 5f, d = 4.4f;   // x from -w/2.. +w/2, z from -d/2 .. +d/2
            var paper = new SurfaceRef(Surface.Wall, 0);
            var carpet = new SurfaceRef(Surface.Floor, 1);
            var tiles = new SurfaceRef(Surface.Ceiling, 1);
            var concrete = new SurfaceRef(Surface.Concrete, 0);
            void Box(Vector3 c, Vector3 size, SurfaceRef m) => L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, size), m, BoxMeshBuilder.Faces.All));
            // The floor (the room's old wall: wallpaper underfoot), with the doorway cut in it near the +x end.
            const float hx = 1.5f, hw = 1.0f, hl = 2.0f;   // hole centre x, width (x), length (z)
            Box(new Vector3((-w / 2f + hx - hw / 2f) / 2f, f - 0.15f, 0f), new Vector3(hx - hw / 2f + w / 2f, 0.3f, d), paper);
            Box(new Vector3((hx + hw / 2f + w / 2f) / 2f, f - 0.15f, 0f), new Vector3(w / 2f - hx - hw / 2f, 0.3f, d), paper);
            Box(new Vector3(hx, f - 0.15f, (hl / 2f + d / 2f) / 2f), new Vector3(hw, 0.3f, d / 2f - hl / 2f), paper);
            Box(new Vector3(hx, f - 0.15f, -(hl / 2f + d / 2f) / 2f), new Vector3(hw, 0.3f, d / 2f - hl / 2f), paper);
            // The door frame round the hole, and the door itself hanging open, down into the dark.
            foreach (float sx in new[] { -1f, 1f }) Box(new Vector3(hx + sx * (hw / 2f + 0.04f), f + 0.02f, 0f), new Vector3(0.08f, 0.04f, hl + 0.16f), new SurfaceRef(Surface.Trim, 1));
            foreach (float sz in new[] { -1f, 1f }) Box(new Vector3(hx, f + 0.02f, sz * (hl / 2f + 0.04f)), new Vector3(hw + 0.16f, 0.04f, 0.08f), new SurfaceRef(Surface.Trim, 1));
            Box(new Vector3(hx + hw / 2f + 0.03f, f - 1.0f, 0f), new Vector3(0.05f, 2.0f, 0.95f), new SurfaceRef(Surface.Trim, 1));
            // Walls: carpet up one side (the old floor), ceiling tiles on the other, wallpaper on the ends; and the old
            // wall above you, set into the concrete of the ceiling.
            Box(new Vector3(-w / 2f - 0.15f, f + h / 2f, 0f), new Vector3(0.3f, h, d + 0.6f), carpet);
            Box(new Vector3(w / 2f + 0.15f, f + h / 2f, 0f), new Vector3(0.3f, h, d + 0.6f), tiles);
            Box(new Vector3(0f, f + h / 2f, d / 2f + 0.15f), new Vector3(w, h, 0.3f), paper);
            Box(new Vector3(0f, f + h / 2f, -d / 2f - 0.15f), new Vector3(w, h, 0.3f), paper);
            Box(new Vector3(0f, (f + h + OceanCeilingHeight) / 2f, 0f), new Vector3(w + 0.6f, OceanCeilingHeight - f - h, d + 0.6f), concrete);
            // Furniture fixed to the carpet wall, sideways: the bookcase, the coffee table, the chair, the lamp.
            var side = Quaternion.Euler(0f, 0f, -90f);   // "up" now points along +x, away from the carpet wall
            void Fixed(PropKind k, float y, float z, float yaw, int v = 0) =>
                L.Props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(new Vector3(-w / 2f, f + y, z), side * Quaternion.Euler(0f, yaw, 0f), Vector3.one), Variant = v, Ceiling = w });
            Fixed(PropKind.Bookshelf, 1.6f, 1.6f, 180f);
            Fixed(PropKind.CoffeeTable, 1.7f, -0.4f, 0f);
            Fixed(PropKind.Armchair, 0.8f, -1.4f, 90f, 2);
            Fixed(PropKind.FloorLamp, 2.6f, 0.9f, 0f);
            Fixed(PropKind.Rug, 1.7f, 0f, 0f, 1);
            // The fluorescent lamp, still on, and its light.
            L.Panels.Add(new PanelSpec { Position = new Vector3(-w / 2f + 1.55f, f + 2.6f, 0.9f), Bare = true, Emitter = true, Range = 7f, Tint = new Color(1f, 0.9f, 0.75f), State = FixtureState.Lit,
                                         Id = (int)WorldHash.Hash(seed, 7, 7, 0x7E0) });
            L.Readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(new Vector3(-0.8f, f + 0.01f, -1.2f), Quaternion.Euler(0f, 30f, 0f), Vector3.one),
                Title = "Written on the back of a receipt",
                Text = "Everything in here is on its side and I'm not. Don't think about it.\n\nThe door goes straight down into the sea. There is no " +
                       "climbing back. Out there: a fort of furniture on an island (they had a diving helmet), concrete pillars you can rest on, " +
                       "and a light, very deep, on the side of a drowned mountain. That light is the way on.\n\nDon't stay down long. The deep pushes on you." });
        }

        /// The cave mouth on the sunken mountain: a light that never goes out, and at the back of the tunnel, Level 8.
        static void CaveExit(ChunkLayout L, uint seed, Vector3 mouth, Vector2 inward, float x0, float z0, float s)
        {
            var back = new Vector3(mouth.x + inward.x * 14f, mouth.y, mouth.z + inward.y * 14f);
            if (InChunk(new Vector2(mouth.x, mouth.z), x0, z0, s))
            {
                L.Props.Add(new PropSpec { Kind = PropKind.Beacon, Pose = Matrix4x4.TRS(mouth - Vector3.up * 1.5f + new Vector3(-inward.x, 0f, -inward.y) * 2f, Quaternion.identity, Vector3.one), Ceiling = OceanCeilingHeight });
                L.Panels.Add(new PanelSpec { Position = mouth + Vector3.up * 2.6f, Bare = true, Emitter = true, Range = 22f, Tint = new Color(0.55f, 1f, 0.75f), State = FixtureState.Lit,
                                             Id = (int)WorldHash.Hash(seed, 8, 8, 0x7C0) });
            }
            if (InChunk(new Vector2(back.x, back.z), x0, z0, s))
                L.Exits.Add(new ExitSpec { Kind = ExitKind.Hatch, Target = "8", Pose = Matrix4x4.TRS(back, Quaternion.identity, Vector3.one),
                                           Trigger = new Bounds(back + Vector3.up * 0.8f, new Vector3(4f, 4f, 4f)) });
        }

        /// Fort Surrender: an island of tar and furniture, stepped up out of the shallows. Its diving gear is still there.
        static void Fort(ChunkLayout L, uint seed, float x0, float z0, float s)
        {
            var c = FortAt(seed);
            if (!InChunk(c, x0, z0, s)) return;
            var tar = new SurfaceRef(Surface.Void, 0);
            void Box(Vector3 at, Vector3 size, SurfaceRef m) => L.Boxes.Add(SurfaceBox.Uniform(new Bounds(at, size), m, BoxMeshBuilder.Faces.All));
            float top = OceanWaterLevel + 0.6f;
            Box(new Vector3(c.x, (top - 30f) / 2f, c.y), new Vector3(14f, top + 30f, 12f), new SurfaceRef(Surface.Wood, 0));   // the raft-island of planks and tar
            for (int k = 0; k < 4; k++) Box(new Vector3(c.x - 7.5f - k * 0.5f, top - 0.3f - k * 0.35f, c.y), new Vector3(1f, 0.3f, 4f), new SurfaceRef(Surface.Wood, 0));   // a landing
            // Tar smeared over most of the boards, planks laid across it.
            Box(new Vector3(c.x + 2f, top + 0.01f, c.y - 1f), new Vector3(6f, 0.02f, 5f), tar);
            Box(new Vector3(c.x - 4f, top + 0.01f, c.y + 1.5f), new Vector3(4.5f, 0.02f, 6f), tar);
            Box(new Vector3(c.x + 4.5f, top + 0.01f, c.y + 3.5f), new Vector3(4f, 0.02f, 3.5f), tar);
            foreach (var (px, pz, py) in new[] { (-3.5f, -1.5f, 20f), (1.5f, 3f, 95f), (4.5f, -3.5f, 160f) })
                L.Props.Add(new PropSpec { Kind = PropKind.Pallet, Pose = Matrix4x4.TRS(new Vector3(c.x + px, top, c.y + pz), Quaternion.Euler(0f, py, 0f), Vector3.one), Ceiling = OceanCeilingHeight });
            void Prop(PropKind k, float dx, float dz, float yaw, int v = 0) => L.Props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(new Vector3(c.x + dx, top, c.y + dz), Quaternion.Euler(0f, yaw, 0f), Vector3.one), Variant = v, Ceiling = OceanCeilingHeight });
            // A barricade of furniture round the edge, crates, a lean-to of planks.
            Prop(PropKind.Bookshelf, -4f, 5.2f, 0f); Prop(PropKind.Bookshelf, 1f, 5.2f, 0f, 1);
            Prop(PropKind.DiningTable, 5.5f, 2f, 80f); Prop(PropKind.Sofa, 5.4f, -2.5f, 270f);
            Prop(PropKind.Armchair, -3f, -4.6f, 20f); Prop(PropKind.Crate, 2f, -4.8f, 10f, 1); Prop(PropKind.Crate, 3.2f, -4.6f, 40f, 2);
            Prop(PropKind.SleepingBag, -1f, 1f, 70f); Prop(PropKind.Lantern, 0f, 2.2f, 0f);
            Prop(PropKind.Bones, 3.5f, 1.5f, 50f, 7);
            L.Specials.Add(new SpecialSpec { Kind = SpecialKind.DivingGear, Pose = Matrix4x4.TRS(new Vector3(c.x - 1.6f, top + 0.75f, c.y + 3.6f), Quaternion.Euler(0f, 180f, 0f), Vector3.one), Id = 7 });
            Prop(PropKind.Desk, -1.6f, 4.3f, 180f);
            L.Panels.Add(new PanelSpec { Position = new Vector3(c.x, top + 1.2f, c.y + 2.2f), Bare = true, Emitter = true, Range = 9f, Tint = new Color(1f, 0.7f, 0.35f), State = FixtureState.Lit,
                                         Id = (int)WorldHash.Hash(seed, 9, 9, 0x7F0) });
            L.Readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(new Vector3(c.x - 0.6f, top + 0.01f, c.y + 0.3f), Quaternion.Euler(0f, 200f, 0f), Vector3.one),
                Title = "FORT SURRENDER - rules (painted on a door)",
                Text = "1. Nobody goes below the grey water without the helmet.\n2. The helmet stays HERE.\n3. If it's loud, keep still. If it's lit, put it out.\n4. Nobody gets the helmet but us.\n\n(The last rule has been scratched through, hard.)" });
        }

        /// The ring of pillars and pipes round a stone platform 40 m down, with a wooden trapdoor in it: Level 9.
        static void Ring(ChunkLayout L, uint seed, float x0, float z0, float s)
        {
            var c = RingAt(seed);
            var stone = new SurfaceRef(Surface.Concrete, 0);
            void Box(Vector3 at, Vector3 size, SurfaceRef m) => L.Boxes.Add(SurfaceBox.Uniform(new Bounds(at, size), m, BoxMeshBuilder.Faces.All));
            float plat = OceanWaterLevel - 40f;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f, b = (i + 1) * Mathf.PI * 2f / 10f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12f;
                if (InChunk(p, x0, z0, s))
                {
                    Box(new Vector3(p.x, (plat + OceanCeilingHeight) / 2f, p.y), new Vector3(1.4f, OceanCeilingHeight - plat, 1.4f), stone);
                    var q = c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 12f;   // a rusted pipe to the next pillar
                    var mid = (p + q) / 2f;
                    float len = Vector2.Distance(p, q), yaw = Mathf.Atan2(q.x - p.x, q.y - p.y) * Mathf.Rad2Deg;
                    L.OrientedBoxes.Add(new SurfaceOrientedBox { Center = new Vector3(mid.x, plat + 4f, mid.y), Size = new Vector3(0.5f, 0.5f, len), Yaw = yaw, Surface = new SurfaceRef(Surface.Concrete, 0) });
                }
            }
            if (!InChunk(c, x0, z0, s)) return;
            // The wooden trapdoor in the stone, and bones round it (something lived here).
            L.Props.Add(new PropSpec { Kind = PropKind.Pallet, Pose = Matrix4x4.TRS(new Vector3(c.x, plat, c.y), Quaternion.identity, Vector3.one), Ceiling = OceanCeilingHeight });
            for (int k = 0; k < 4; k++)
                L.Props.Add(new PropSpec { Kind = PropKind.Bones, Pose = Matrix4x4.TRS(new Vector3(c.x + Mathf.Cos(k * 1.7f) * 4f, plat, c.y + Mathf.Sin(k * 1.7f) * 4f), Quaternion.Euler(0f, k * 80f, 0f), Vector3.one), Variant = 20 + k, Ceiling = OceanCeilingHeight });
            L.Exits.Add(new ExitSpec { Kind = ExitKind.Hatch, Target = "9", Pose = Matrix4x4.TRS(new Vector3(c.x, plat, c.y), Quaternion.identity, Vector3.one),
                                       Trigger = new Bounds(new Vector3(c.x, plat + 0.6f, c.y), new Vector3(1.6f, 1.2f, 1.6f)) });
        }
    }
}
