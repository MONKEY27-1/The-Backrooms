using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    public enum FixtureState { Lit, Faulty, Dead }

    public struct FixtureSpec
    {
        public Vector3 Position;   // centre of the ceiling panel, world space
        public FixtureState State;
    }

    public struct ExitSpec
    {
        public ExitKind Kind;
        public string Target;      // level id
        public Matrix4x4 Pose;     // wall frame: origin on the floor at the wall face, local +z away from the wall
        public Bounds Trigger;     // walk into this to leave; for flicker walls it is the wall itself
    }

    public sealed class ChunkLayout
    {
        public readonly List<Bounds> Walls = new List<Bounds>();        // walls, blocks and pillars, world space
        public readonly List<Bounds> AccentWalls = new List<Bounds>();  // Manila Room walls
        public readonly List<FixtureSpec> Fixtures = new List<FixtureSpec>();
        public readonly List<PropSpec> Props = new List<PropSpec>();
        public readonly List<ExitSpec> Exits = new List<ExitSpec>();
        public readonly List<Bounds> Pits = new List<Bounds>();         // fall triggers
        public bool Red;                                                // a Red Rooms area chunk

        // Surface-based output (district generator; maze output is converted into these too).
        public readonly List<SurfaceBox> Boxes = new List<SurfaceBox>();
        public readonly List<SurfaceOrientedBox> OrientedBoxes = new List<SurfaceOrientedBox>();
        public readonly List<SurfaceQuad> Quads = new List<SurfaceQuad>();
        public readonly List<SurfaceQuad> Water = new List<SurfaceQuad>();
        public readonly List<SurfaceQuad> WaterDeep = new List<SurfaceQuad>();
        public readonly List<SurfaceQuad> WaterBed = new List<SurfaceQuad>();          // animated caustics on shallow flooded floors
        public readonly List<SurfaceQuad> WaterBedDeep = new List<SurfaceQuad>();      // dimmer caustics in the basins
        public readonly List<PanelSpec> Panels = new List<PanelSpec>();
        public readonly List<Bounds> Blockers = new List<Bounds>();     // large props, for reachability checks
        public readonly List<Bounds> Openings = new List<Bounds>();     // walkable volume through doorways and arches
        public readonly List<CuriositySlot> Curiosities = new List<CuriositySlot>();
        public readonly List<ReadableSpec> Readables = new List<ReadableSpec>();   // forged rooms: wall writing, paintings, notes
        public readonly List<BoardSpec> Boards = new List<BoardSpec>();            // boarded-up doorways
        public readonly List<PlankSpec> Planks = new List<PlankSpec>();            // planks across pitfalls
        public readonly List<SurfaceQuad> Damp = new List<SurfaceQuad>();          // Level 0's moist carpet: glossy wet patches
        public readonly List<SurfaceSlope> Slopes = new List<SurfaceSlope>();      // Level 1's car ramps
        public readonly List<SpecialSpec> Specials = new List<SpecialSpec>();      // Level 3: bars, sludge, the Sanctum, machine hum

        public readonly List<RockMesh> Rocks = new List<RockMesh>();               // natural terrain: Level 7's sea floor, Level 8's caves

        /// Moves everything up by `dy` (stacked floors are generated at height 0, then lifted into place).
        public void Shift(float dy)
        {
            if (dy == 0f) return;
            var up = Vector3.up * dy;
            var lift = Matrix4x4.Translate(up);
            for (int i = 0; i < Boxes.Count; i++) { var b = Boxes[i]; b.Box.center += up; Boxes[i] = b; }
            for (int i = 0; i < OrientedBoxes.Count; i++) { var b = OrientedBoxes[i]; b.Center += up; OrientedBoxes[i] = b; }
            for (int i = 0; i < Quads.Count; i++) { var q = Quads[i]; q.Height += dy; Quads[i] = q; }
            for (int i = 0; i < Water.Count; i++) { var q = Water[i]; q.Height += dy; Water[i] = q; }
            for (int i = 0; i < WaterDeep.Count; i++) { var q = WaterDeep[i]; q.Height += dy; WaterDeep[i] = q; }
            for (int i = 0; i < WaterBed.Count; i++) { var q = WaterBed[i]; q.Height += dy; WaterBed[i] = q; }
            for (int i = 0; i < WaterBedDeep.Count; i++) { var q = WaterBedDeep[i]; q.Height += dy; WaterBedDeep[i] = q; }
            for (int i = 0; i < Damp.Count; i++) { var q = Damp[i]; q.Height += dy; Damp[i] = q; }
            for (int i = 0; i < Slopes.Count; i++) { var q = Slopes[i]; q.A += up; q.B += up; q.Bottom += dy; Slopes[i] = q; }
            for (int i = 0; i < Panels.Count; i++) { var p = Panels[i]; p.Position += up; Panels[i] = p; }
            for (int i = 0; i < Props.Count; i++) { var p = Props[i]; p.Pose = lift * p.Pose; Props[i] = p; }
            for (int i = 0; i < Exits.Count; i++) { var e = Exits[i]; e.Pose = lift * e.Pose; e.Trigger.center += up; Exits[i] = e; }
            for (int i = 0; i < Curiosities.Count; i++) { var c = Curiosities[i]; c.Pose = lift * c.Pose; Curiosities[i] = c; }
            for (int i = 0; i < Readables.Count; i++) { var r = Readables[i]; r.Pose = lift * r.Pose; Readables[i] = r; }
            for (int i = 0; i < Boards.Count; i++) { var b = Boards[i]; b.Pose = lift * b.Pose; Boards[i] = b; }
            for (int i = 0; i < Planks.Count; i++) { var p = Planks[i]; p.Pose = lift * p.Pose; Planks[i] = p; }
            for (int i = 0; i < Pits.Count; i++) { var b = Pits[i]; b.center += up; Pits[i] = b; }
            for (int i = 0; i < Blockers.Count; i++) { var b = Blockers[i]; b.center += up; Blockers[i] = b; }
            for (int i = 0; i < Openings.Count; i++) { var b = Openings[i]; b.center += up; Openings[i] = b; }
            foreach (var r in Rocks) for (int i = 0; i < r.V.Count; i++) r.V[i] += up;
            for (int i = 0; i < Specials.Count; i++) { var s = Specials[i]; s.Pose = lift * s.Pose; Specials[i] = s; }
        }
    }

    /// The Backrooms maze: an open plan of free-standing partition walls (L, T and U shapes, hooks, spirals,
    /// rooms with one gap) on a 2 m grid, with pillar halls, block grids, solid blocks, props and exits.
    /// Every level uses it with its own MazeStyle.
    ///
    /// Walls are drawn by "walkers" that move along grid lines. A walker may join another wall once, but may
    /// never close a loop, so the walls form a forest and can never seal off floor: everything is reachable.
    /// Chunk border lines carry only "runs", a pure function of the global edge, so both neighbours agree on them.
    /// Each wall group in a chunk may touch at most one run, which keeps the forest property across chunks.
    public static class MazeLayout
    {
        public const float CellSize = 2f;
        public const int CellsPerChunk = 16;
        public const float ChunkSize = CellSize * CellsPerChunk;
        public const float WallThickness = 0.3f;

        const float DoorWidth = 1.2f, DoorHeight = 2.1f, WallHalf = WallThickness / 2;
        const int N = CellsPerChunk;
        const int SaltHall = 11, SaltHallSmall = 12, SaltRun = 13, SaltDensity = 15, SaltPillarStyle = 16, SaltPlan = 17;
        static readonly int[] Dx = { 1, 0, -1, 0 }, Dz = { 0, 1, 0, -1 };

        public static ChunkLayout Generate(uint seed, int chunkX, int chunkZ, MazeStyle style) =>
            new Planner(seed, chunkX, chunkZ, style).Build();

        /// Giant pillar halls come in 2x2-chunk clusters (64 m across), plus the odd single-chunk hall.
        static bool IsHall(uint seed, int cx, int cz, MazeStyle s) =>
            WorldHash.Value(seed, cx >> 1, cz >> 1, SaltHall) < s.hallChance
            || WorldHash.Value(seed, cx, cz, SaltHallSmall) < s.hallChance / 3;

        /// Is there a wall on this border-line edge? `line` is the global coordinate of the line,
        /// `along` the global vertex index where the edge starts. Edges come in runs of up to 4 (8 m).
        static bool RunEdge(uint seed, bool alongZ, int line, int along)
        {
            int salt = alongZ ? SaltRun : SaltRun + 100;
            return WorldHash.Value(seed, line, along >> 2, salt) < 0.3f && WorldHash.Value(seed, line, along, salt + 1) > 0.08f;
        }

        sealed class Planner
        {
            readonly uint seed;
            readonly int cx, cz;
            readonly MazeStyle style;
            readonly WorldRng rng;
            readonly Vector3 origin;
            readonly int[,] owner = new int[N + 1, N + 1];        // wall group per grid vertex, -1 = free
            readonly bool[,] reserved = new bool[N + 1, N + 1];   // walkers keep out (halls, features)
            readonly bool[,] cellUsed = new bool[N, N];           // no props or exits here
            readonly List<int> parent = new List<int>();          // union-find over wall groups
            readonly List<bool> touchesRun = new List<bool>();
            readonly List<int> edges = new List<int>();           // wall edges this chunk builds
            readonly List<int> walkerEdges = new List<int>();     // where exits may go
            readonly HashSet<int> wallEdges = new HashSet<int>(); // every wall bounding this chunk's cells
            readonly HashSet<int> doorways = new HashSet<int>();
            readonly HashSet<int> flickerEdges = new HashSet<int>();
            readonly ChunkLayout layout = new ChunkLayout();

            public Planner(uint seed, int cx, int cz, MazeStyle style)
            {
                this.seed = seed;
                this.cx = cx;
                this.cz = cz;
                this.style = style;
                rng = new WorldRng(WorldHash.Hash(seed, cx, cz, SaltPlan));
                origin = new Vector3(cx * ChunkSize, 0f, cz * ChunkSize);
                for (int x = 0; x <= N; x++)
                for (int z = 0; z <= N; z++)
                    owner[x, z] = -1;
            }

            public ChunkLayout Build()
            {
                bool hall = IsHall(seed, cx, cz, style);
                layout.Red = !hall && rng.Value() < style.redChance;
                float dead = rng.Value() < style.blackoutChance ? 0.85f : style.deadLights;
                SeedRuns(hall);

                if (hall)
                {
                    Reserve(new RectInt(0, 0, N, N), false);
                    Pillars(new RectInt(0, 0, N, N));
                }
                else
                {
                    // Density is half shared with the 2x2 neighbourhood, so it drifts gradually across the map.
                    float density = 0.5f * (WorldHash.Value(seed, cx >> 1, cz >> 1, SaltDensity) + rng.Value());
                    float f = rng.Value();
                    if (f < style.manilaChance) ManilaRoom();
                    else if (f < style.manilaChance + style.pitChance) Pits(Feature());
                    else if (f < style.manilaChance + style.pitChance + style.featureChance)
                    {
                        var r = Feature();
                        if (rng.Value() < 0.5f) BlockGrid(r); else Pillars(r);
                    }
                    for (int i = rng.Range(0, style.solidBlocks + 1); i > 0; i--) SolidBlock();
                    int walkers = Mathf.RoundToInt(Mathf.Lerp(style.walkers.x, style.walkers.y, density));
                    for (int i = 0; i < walkers; i++) Walk(density);
                }

                edges.Sort();   // fixed order, so every roll below is deterministic
                foreach (int key in edges)
                    if (rng.Value() < 0.07f) doorways.Add(key);
                PlaceExit();
                EmitWalls();
                PlaceProps();
                EmitFixtures(dead);
                return layout;
            }

            // ---------- wall groups ----------

            int NewGroup(bool run)
            {
                parent.Add(parent.Count);
                touchesRun.Add(run);
                return parent.Count - 1;
            }

            int Find(int g)
            {
                while (parent[g] != g)
                {
                    parent[g] = parent[parent[g]];
                    g = parent[g];
                }
                return g;
            }

            /// Refuses joins that would close a loop or link two border runs: either could seal off floor.
            bool TryJoin(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a == b || (touchesRun[a] && touchesRun[b])) return false;
                parent[a] = b;
                touchesRun[b] |= touchesRun[a];
                return true;
            }

            // ---------- structure ----------

            void SeedRuns(bool hall)
            {
                // West and south lines are built by this chunk; east and north by the neighbours (as their west/south).
                SeedLine(true, 0, cx * N, IsHall(seed, cx - 1, cz, style), hall, true);
                SeedLine(true, N, (cx + 1) * N, IsHall(seed, cx + 1, cz, style), hall, false);
                SeedLine(false, 0, cz * N, IsHall(seed, cx, cz - 1, style), hall, true);
                SeedLine(false, N, (cz + 1) * N, IsHall(seed, cx, cz + 1, style), hall, false);
            }

            void SeedLine(bool alongZ, int local, int line, bool neighbourHall, bool hall, bool build)
            {
                if (hall && neighbourHall) return;   // inside a big hall: no walls on the seam
                int group = -1, baseAlong = alongZ ? cz * N : cx * N;
                for (int a = 1; a < N - 1; a++)       // corners stay free so lines never meet
                {
                    if (!RunEdge(seed, alongZ, line, baseAlong + a)) { group = -1; continue; }
                    if (group < 0) group = NewGroup(true);
                    int x0 = alongZ ? local : a, z0 = alongZ ? a : local;
                    int x1 = alongZ ? local : a + 1, z1 = alongZ ? a + 1 : local;
                    owner[x0, z0] = group;
                    owner[x1, z1] = group;
                    int key = EdgeKey(x0, z0, x1, z1);
                    wallEdges.Add(key);
                    if (build) edges.Add(key);
                }
            }

            /// One walker draws one wall: straight-ish, wiggly, a room that stops one wall short of closing, or a spiral.
            void Walk(float density)
            {
                int x = rng.Range(1, N), z = rng.Range(1, N);
                if (owner[x, z] >= 0 || reserved[x, z]) return;
                int group = NewGroup(false);
                owner[x, z] = group;

                float roll = rng.Value();
                bool room = roll < style.roomChance, spiral = !room && roll < style.roomChance + style.spiralChance;
                int dir = rng.Range(0, 4), turn = rng.Value() < 0.5f ? 1 : 3;
                int legA = rng.Range(2, 7), legB = rng.Range(2, 6);
                float turnChance = rng.Value() < 0.5f ? style.wiggle * 0.4f : style.wiggle;
                int length = room ? 2 * (legA + legB) : spiral ? rng.Range(8, 15)
                    : rng.Range(4, Mathf.Max(5, Mathf.RoundToInt(style.maxWallLength * (0.5f + density))));
                int legLeft = room ? legA : 1, leg = 0;

                for (int step = 0; step < length; step++)
                {
                    if (room || spiral)
                    {
                        if (legLeft == 0)
                        {
                            dir = (dir + turn) % 4;
                            leg++;
                            legLeft = room ? (leg % 2 == 0 ? legA : legB) : leg / 2 + 1;   // spiral legs: 1,1,2,2,3,3...
                        }
                        legLeft--;
                    }
                    else if (rng.Value() < turnChance) dir = (dir + (rng.Value() < 0.5f ? 1 : 3)) % 4;

                    int nx = x + Dx[dir], nz = z + Dz[dir];
                    if (nx < 0 || nz < 0 || nx > N || nz > N || reserved[nx, nz]) return;
                    int other = owner[nx, nz];
                    bool border = nx == 0 || nz == 0 || nx == N || nz == N;
                    if (other < 0 && border) return;   // border lines carry only the shared runs
                    if (other >= 0)
                    {
                        if (TryJoin(group, other)) AddWalkerEdge(EdgeKey(x, z, nx, nz));   // T-junction, then stop
                        return;
                    }
                    AddWalkerEdge(EdgeKey(x, z, nx, nz));
                    owner[nx, nz] = group;
                    x = nx;
                    z = nz;
                }
            }

            void AddWalkerEdge(int key)
            {
                edges.Add(key);
                walkerEdges.Add(key);
                wallEdges.Add(key);
            }

            /// A solid block of 1-3 x 1-3 cells. Walls may butt against it (once), like any other wall group.
            void SolidBlock()
            {
                int w = rng.Range(1, 4), h = rng.Range(1, 4);
                int x0 = rng.Range(1, N - w), z0 = rng.Range(1, N - h);
                for (int x = x0; x <= x0 + w; x++)
                for (int z = z0; z <= z0 + h; z++)
                    if (owner[x, z] >= 0 || reserved[x, z]) return;
                int group = NewGroup(false);
                for (int x = x0; x <= x0 + w; x++)
                for (int z = z0; z <= z0 + h; z++)
                    owner[x, z] = group;
                for (int x = x0; x < x0 + w; x++)
                for (int z = z0; z < z0 + h; z++)
                    cellUsed[x, z] = true;
                AddBox(x0 + w / 2f, z0 + h / 2f, w * CellSize + WallThickness, h * CellSize + WallThickness);
            }

            /// A random area of 5-10 x 5-10 cells that walkers keep out of.
            RectInt Feature()
            {
                var r = new RectInt(0, 0, rng.Range(5, 11), rng.Range(5, 11));
                r.position = new Vector2Int(rng.Range(1, N - r.width), rng.Range(1, N - r.height));
                Reserve(r, true);
                return r;
            }

            void BlockGrid(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x += 2)
                for (int z = r.yMin; z < r.yMax; z += 2)
                    AddBox(x + 0.5f, z + 0.5f, CellSize + WallThickness, CellSize + WallThickness);
            }

            /// Pillars in every other cell on a global grid, so neighbouring hall chunks line up seamlessly.
            /// They stand mid-cell and never touch a grid line, so they can't affect reachability.
            void Pillars(RectInt r)
            {
                var shape = style.pillars;
                if (shape == PillarShape.Mixed)
                    shape = WorldHash.Value(seed, cx >> 1, cz >> 1, SaltPillarStyle) < 0.6f ? PillarShape.Cross : PillarShape.Square;
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                {
                    if (((cx * N + x) & 1) == 0 || ((cz * N + z) & 1) == 0) continue;
                    cellUsed[x, z] = true;
                    if (shape == PillarShape.Cross)
                    {
                        AddBox(x + 0.5f, z + 0.5f, 1.2f, 0.35f);
                        AddBox(x + 0.5f, z + 0.5f, 0.35f, 1.2f);
                    }
                    else AddBox(x + 0.5f, z + 0.5f, 0.8f, 0.8f);
                }
            }

            /// Level 0 "Holes": a grid of pitch-black pits. Stepping into one drops you back to your checkpoint.
            void Pits(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x += 2)
                for (int z = r.yMin; z < r.yMax; z += 2)
                {
                    var centre = origin + new Vector3((x + 0.5f) * CellSize, 0f, (z + 0.5f) * CellSize);
                    layout.Props.Add(new PropSpec { Kind = PropKind.Pit, Pose = Matrix4x4.Translate(centre) });
                    layout.Pits.Add(new Bounds(centre + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f)));
                }
            }

            /// The Manila Room: an 8 x 8 m beige room with floorboards, an octagonal table, two chairs
            /// and a door on each wall. Three are open doorways; one is a wooden door to Level 1.
            void ManilaRoom()
            {
                var r = new RectInt(rng.Range(1, N - 5), rng.Range(1, N - 5), 4, 4);
                Reserve(r, true);
                int exitSide = rng.Range(0, 4);
                for (int side = 0; side < 4; side++)
                {
                    int doorAt = 1 + rng.Range(0, 2);
                    for (int i = 0; i < 4; i++)
                    {
                        int x0 = side < 2 ? r.xMin + i : side == 2 ? r.xMin : r.xMax;
                        int z0 = side < 2 ? (side == 0 ? r.yMin : r.yMax) : r.yMin + i;
                        bool alongX = side < 2;
                        int key = EdgeKey(x0, z0, alongX ? x0 + 1 : x0, alongX ? z0 : z0 + 1);
                        wallEdges.Add(key);
                        EdgePieces(layout.AccentWalls, key, i == doorAt && side != exitSide);
                        if (i == doorAt && side == exitSide)
                        {
                            // The door faces into the room: south and west walls face +, north and east face -.
                            WallFrame(key, side == 0 || side == 2, out var pose, out _);
                            AddExit(ExitKind.Door, "1", pose);
                        }
                    }
                }
                var centre = origin + new Vector3((r.xMin + 2) * CellSize, 0f, (r.yMin + 2) * CellSize);
                layout.Props.Add(new PropSpec { Kind = PropKind.WoodFloor, Pose = Matrix4x4.Translate(centre) });
                layout.Props.Add(new PropSpec { Kind = PropKind.OctagonTable, Pose = Matrix4x4.Translate(centre) });
                layout.Props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(centre + new Vector3(0, 0, -0.9f), Quaternion.identity, Vector3.one) });
                layout.Props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(centre + new Vector3(0, 0, 0.9f), Quaternion.Euler(0, 180, 0), Vector3.one) });
            }

            void Reserve(RectInt r, bool blockCells)
            {
                for (int x = r.xMin; x <= r.xMax; x++)
                for (int z = r.yMin; z <= r.yMax; z++)
                    reserved[x, z] = true;
                if (!blockCells) return;
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                    cellUsed[x, z] = true;
            }

            // ---------- exits ----------

            void PlaceExit()
            {
                if (style.exits.Count == 0 || rng.Value() >= style.exitChance) return;
                var link = PickWeighted(style.exits, l => l.weight);
                if (walkerEdges.Count == 0)
                {
                    HallExit(link);
                    return;
                }
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int key = walkerEdges[rng.Range(0, walkerEdges.Count)];
                    if (doorways.Contains(key) || !WallFrame(key, rng.Value() < 0.5f, out var pose, out var cell)
                        || cellUsed[cell.x, cell.y] || cell == Vector2Int.zero)   // keep exits away from the arrival cell
                        continue;
                    cellUsed[cell.x, cell.y] = true;
                    if (link.kind == ExitKind.FlickerWall)
                    {
                        // The wall itself flickers; walking into it is the noclip.
                        flickerEdges.Add(key);
                        layout.Exits.Add(new ExitSpec { Kind = ExitKind.FlickerWall, Target = link.target, Pose = pose, Trigger = EdgeBounds(key) });
                    }
                    else AddExit(link.kind, link.target, pose);
                    return;
                }
            }

            /// Halls have no walls to put a door on, so the exit gets its own free-standing stairwell block.
            /// The block sits in a cell between pillars, away from the chunk border, so it can't seal anything off.
            void HallExit(ExitLink link)
            {
                var kind = link.kind == ExitKind.FlickerWall ? ExitKind.Door : link.kind;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int x = rng.Range(2, N - 2), z = rng.Range(2, N - 2);
                    if (cellUsed[x, z] || ((cx * N + x) & 1) == 1 || ((cz * N + z) & 1) == 1) continue;
                    cellUsed[x, z] = true;
                    AddBox(x + 0.5f, z + 0.5f, CellSize + WallThickness, CellSize + WallThickness);
                    int face = rng.Range(0, 4);
                    var normal = new Vector3(Dx[face], 0f, Dz[face]);
                    var at = origin + new Vector3((x + 0.5f) * CellSize, 0f, (z + 0.5f) * CellSize) + normal * (CellSize / 2 + WallHalf);
                    AddExit(kind, link.target, Matrix4x4.TRS(at, Quaternion.LookRotation(normal), Vector3.one));
                    return;
                }
            }

            void AddExit(ExitKind kind, string target, Matrix4x4 pose)
            {
                // Trigger just in front of the door: you leave by walking into it.
                var trigger = LocalBox(pose, new Vector3(0f, 1f, 0.25f), new Vector3(0.9f, 2f, 0.5f));
                layout.Exits.Add(new ExitSpec { Kind = kind, Target = target, Pose = pose, Trigger = trigger });
            }

            // ---------- props ----------

            void PlaceProps()
            {
                if (style.props.Count == 0 || style.propDensity <= 0f) return;
                for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    if (cellUsed[x, z] || (x == 0 && z == 0) || rng.Value() >= style.propDensity) continue;   // spawn cell stays clear
                    var kind = PickWeighted(style.props, p => p.weight).kind;
                    int variant = rng.Range(0, 12);
                    Matrix4x4 pose;
                    if (PropLibrary.IsWallProp(kind))
                    {
                        if (!WallOfCell(x, z, out pose)) continue;
                        pose *= Matrix4x4.Translate(new Vector3(rng.Range(-3, 4) * 0.1f, 0f, 0f));   // slide along the wall
                    }
                    else
                    {
                        var at = new Vector3((x + 0.5f + (rng.Value() - 0.5f) * 0.5f) * CellSize, 0f, (z + 0.5f + (rng.Value() - 0.5f) * 0.5f) * CellSize);
                        pose = Matrix4x4.TRS(origin + at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one);
                    }
                    cellUsed[x, z] = true;

                    bool doubled = false;
                    if (rng.Value() < style.glitchChance) pose = Glitch(pose, kind, out doubled);
                    layout.Props.Add(new PropSpec { Kind = kind, Pose = pose, Variant = variant });
                    if (doubled)   // two copies fighting over the same space: the surfaces shimmer
                        layout.Props.Add(new PropSpec { Kind = kind, Pose = pose * Matrix4x4.TRS(new Vector3(0.02f, 0.01f, 0.015f), Quaternion.Euler(0f, 2.5f, 0f), Vector3.one), Variant = variant });
                }
            }

            /// Props in the wrong place: the world forgot how to hold them.
            Matrix4x4 Glitch(Matrix4x4 pose, PropKind kind, out bool doubled)
            {
                doubled = false;
                switch (rng.Range(0, 5))
                {
                    case 0:   // sunk into the floor
                        return Matrix4x4.Translate(Vector3.down * Mathf.Lerp(0.2f, 0.6f, rng.Value())) * pose;
                    case 1:   // floating, tumbled
                        return Matrix4x4.Translate(Vector3.up * Mathf.Lerp(0.4f, 1.3f, rng.Value())) * pose
                               * Matrix4x4.Rotate(Quaternion.Euler(rng.Value() * 360f, rng.Value() * 360f, rng.Value() * 360f));
                    case 2:   // stretched upward, sometimes through the ceiling
                        return pose * Matrix4x4.Scale(new Vector3(1f, Mathf.Lerp(1.6f, 3f, rng.Value()), 1f));
                    case 3:   // wall props sink into the wall and poke out the far side; others lie on their side
                        return PropLibrary.IsWallProp(kind)
                            ? pose * Matrix4x4.Translate(new Vector3(0f, 0f, -Mathf.Lerp(0.2f, 0.5f, rng.Value())))
                            : pose * Matrix4x4.TRS(new Vector3(0f, 0.25f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                    default:
                        doubled = true;
                        return pose;
                }
            }

            /// A wall on one of the cell's four sides that a prop can stand against, facing into the cell.
            bool WallOfCell(int x, int z, out Matrix4x4 pose)
            {
                int start = rng.Range(0, 4);
                for (int i = 0; i < 4; i++)
                {
                    int side = (start + i) % 4;   // 0 south, 1 north, 2 west, 3 east
                    int key = side switch
                    {
                        0 => EdgeKey(x, z, x + 1, z),
                        1 => EdgeKey(x, z + 1, x + 1, z + 1),
                        2 => EdgeKey(x, z, x, z + 1),
                        _ => EdgeKey(x + 1, z, x + 1, z + 1),
                    };
                    if (!wallEdges.Contains(key) || doorways.Contains(key) || flickerEdges.Contains(key)) continue;
                    if (WallFrame(key, side == 0 || side == 2, out pose, out _)) return true;
                }
                pose = default;
                return false;
            }

            // ---------- output ----------

            void EmitWalls()
            {
                foreach (int key in edges)
                    if (!flickerEdges.Contains(key)) EdgePieces(layout.Walls, key, doorways.Contains(key));
            }

            void EdgePieces(List<Bounds> into, int key, bool doorway)
            {
                Decode(key, out int x, out int z, out int dir);
                var start = origin + new Vector3(x * CellSize, 0f, z * CellSize);
                var along = dir == 0 ? Vector3.right : Vector3.forward;
                float h = style.ceiling;
                if (!doorway)
                {
                    AddPiece(into, start, along, -WallHalf, CellSize + WallHalf, 0f, h);   // overlap at corners so walls meet
                    return;
                }
                const float d0 = (CellSize - DoorWidth) / 2, d1 = d0 + DoorWidth;
                AddPiece(into, start, along, -WallHalf, d0, 0f, h);
                AddPiece(into, start, along, d1, CellSize + WallHalf, 0f, h);
                AddPiece(into, start, along, d0, d1, DoorHeight, h);
            }

            Bounds EdgeBounds(int key)
            {
                var pieces = new List<Bounds>(1);
                EdgePieces(pieces, key, false);
                return pieces[0];
            }

            /// Ceiling panels on a global 4 m grid, independent of the walls, like a real drop ceiling.
            void EmitFixtures(float dead)
            {
                for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    if (((cx * N + x) & 1) != 0 || ((cz * N + z) & 1) != 0) continue;
                    float r = rng.Value();
                    layout.Fixtures.Add(new FixtureSpec
                    {
                        Position = origin + new Vector3((x + 0.5f) * CellSize, style.ceiling, (z + 0.5f) * CellSize),
                        State = r < dead ? FixtureState.Dead : r < dead + 0.04f ? FixtureState.Faulty : FixtureState.Lit,
                    });
                }
            }

            static void AddPiece(List<Bounds> into, Vector3 start, Vector3 along, float from, float to, float y0, float y1)
            {
                Vector3 center = start + along * ((from + to) / 2) + Vector3.up * ((y0 + y1) / 2);
                float length = to - from;
                var size = along.x != 0
                    ? new Vector3(length, y1 - y0, WallThickness)
                    : new Vector3(WallThickness, y1 - y0, length);
                into.Add(new Bounds(center, size));
            }

            /// Full-height box centred on cell coordinates (cellX, cellZ), sizes in meters.
            void AddBox(float cellX, float cellZ, float sizeX, float sizeZ) =>
                layout.Walls.Add(new Bounds(origin + new Vector3(cellX * CellSize, style.ceiling / 2, cellZ * CellSize),
                                            new Vector3(sizeX, style.ceiling, sizeZ)));

            /// Frame on one face of a wall edge: origin on the floor at the wall face, local +z away from the wall.
            /// `positive` picks the +z (or +x) side. Fails if the cell on that side is outside this chunk.
            bool WallFrame(int key, bool positive, out Matrix4x4 pose, out Vector2Int cell)
            {
                Decode(key, out int x, out int z, out int dir);
                Vector3 normal, at;
                float yaw;
                if (dir == 0)
                {
                    cell = new Vector2Int(x, positive ? z : z - 1);
                    normal = positive ? Vector3.forward : Vector3.back;
                    yaw = positive ? 0f : 180f;
                    at = new Vector3((x + 0.5f) * CellSize, 0f, z * CellSize);
                }
                else
                {
                    cell = new Vector2Int(positive ? x : x - 1, z);
                    normal = positive ? Vector3.right : Vector3.left;
                    yaw = positive ? 90f : 270f;
                    at = new Vector3(x * CellSize, 0f, (z + 0.5f) * CellSize);
                }
                pose = Matrix4x4.TRS(origin + at + normal * WallHalf, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                return cell.x >= 0 && cell.y >= 0 && cell.x < N && cell.y < N;
            }

            T PickWeighted<T>(List<T> items, System.Func<T, float> weight)
            {
                float total = 0f;
                foreach (var item in items) total += Mathf.Max(0f, weight(item));
                float roll = rng.Value() * total;
                foreach (var item in items)
                {
                    roll -= Mathf.Max(0f, weight(item));
                    if (roll < 0f) return item;
                }
                return items[items.Count - 1];
            }

            static Bounds LocalBox(Matrix4x4 pose, Vector3 center, Vector3 size)
            {
                var b = new Bounds(pose.MultiplyPoint3x4(center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = center + Vector3.Scale(size / 2, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1));
                    b.Encapsulate(pose.MultiplyPoint3x4(corner));
                }
                return b;
            }

            /// Edge between two adjacent grid vertices, keyed by its lower vertex and axis (0 = x, 1 = z).
            static int EdgeKey(int x0, int z0, int x1, int z1) =>
                ((Mathf.Min(x0, x1) * (N + 1) + Mathf.Min(z0, z1)) << 1) | (x0 != x1 ? 0 : 1);

            static void Decode(int key, out int x, out int z, out int dir)
            {
                dir = key & 1;
                int v = key >> 1;
                x = v / (N + 1);
                z = v % (N + 1);
            }
        }
    }
}
