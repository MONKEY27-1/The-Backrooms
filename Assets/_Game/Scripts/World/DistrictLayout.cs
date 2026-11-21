using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 0's architecture. The world is split into 64 m districts (2 x 2 chunks), each built by one of many
    /// archetypes: partition maze, open expanse, pillar hall, offices, corridors, panel rows, nested rooms, atrium,
    /// storage, furniture showroom, supermarket, clothing store and mall concourse. Every room then rolls its own
    /// wallpaper, carpet, ceiling height, light layout, trim and openings, so the combinations run into the millions.
    ///
    /// Reachability is guaranteed the same way as in the maze: every wall goes through one union-find, and a wall
    /// that would close a loop (or link two border runs) becomes a cased doorway instead. Rooms get doors exactly
    /// where they need them and nothing can ever be sealed off. Room walls are added corner-first, so the doorway
    /// lands mid-wall, where an architect would put it.
    ///
    /// A district is planned whole from (seed, district coords) and each chunk extracts the part it owns, so chunk
    /// seams inside a district are invisible. Plans are cached because each district serves four chunks.
    public static partial class DistrictLayout
    {
        public const int Cells = 32;
        const int D = Cells, ChunkCells = MazeLayout.CellsPerChunk, SolidZone = -1;
        const float C = MazeLayout.CellSize, T = MazeLayout.WallThickness, ThickWall = 0.5f;
        const float DoorHeight = 2.1f, DoorWidth = 1.1f, WideHeight = 2.4f, LowWallHeight = 1.4f, BorderHeight = 2.8f;
        static readonly int[] Dx = { 1, 0, -1, 0 }, Dz = { 0, 1, 0, -1 };

        static readonly Dictionary<(uint, int, int, LevelTheme), Plan> cache = new Dictionary<(uint, int, int, LevelTheme), Plan>();
        static readonly Queue<(uint, int, int, LevelTheme)> cacheOrder = new Queue<(uint, int, int, LevelTheme)>();

        public static ChunkLayout Generate(uint levelSeed, int floor, int cx, int cz, LevelTheme theme)
        {
            uint seed = HasFloors(theme) ? FloorSeed(levelSeed, floor) : levelSeed;
            int epoch = EpochOf(seed, FloorDiv(cx, 2), FloorDiv(cz, 2));   // the Peripheral Shift
            var key = (seed ^ (uint)epoch * 2654435761u, FloorDiv(cx, 2), FloorDiv(cz, 2), theme);
            // The chunk streamer calls this from a worker thread, so the cache is locked; the slow planning isn't.
            Plan plan;
            lock (cache) cache.TryGetValue(key, out plan);
            if (plan == null)
            {
                var built = new Plan(levelSeed, floor, key.Item2, key.Item3, theme);
                built.Build();
                lock (cache)
                    if (!cache.TryGetValue(key, out plan))
                    {
                        cache[key] = plan = built;
                        cacheOrder.Enqueue(key);
                        while (cacheOrder.Count > 16) cache.Remove(cacheOrder.Dequeue());
                    }
            }
            lock (plan) return plan.Extract(cx, cz);
        }

        public static void ClearCache()
        {
            lock (cache)
            {
                cache.Clear();
                cacheOrder.Clear();
            }
        }

        /// The archetype of the district containing chunk (cx, cz).
        public static DistrictKind KindAt(uint seed, int cx, int cz, DistrictStyle style) =>
            RegionAt(seed, new Vector2((cx + 0.5f) * MazeLayout.ChunkSize, (cz + 0.5f) * MazeLayout.ChunkSize), style).Kind;

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        static DistrictKind RawPick(uint seed, int dx, int dz, DistrictStyle s, float roll)
        {
            float total = 0f;
            foreach (var k in s.kinds) total += Mathf.Max(0f, k.weight);
            roll *= total;
            foreach (var k in s.kinds)
            {
                roll -= Mathf.Max(0f, k.weight);
                if (roll < 0f) return k.kind;
            }
            return s.kinds.Count > 0 ? s.kinds[s.kinds.Count - 1].kind : DistrictKind.Maze;
        }

        /// Is there a run (border wall) on this district-border edge? Pure function of the global edge.
        static bool RunEdge(uint seed, bool alongZ, int line, int along)
        {
            int salt = alongZ ? 613 : 713;
            return WorldHash.Value(seed, line, along >> 2, salt) < 0.35f && WorldHash.Value(seed, line, along, salt + 1) > 0.08f;
        }

        enum EdgeKind : byte { Wall, LowWall, Run, Doorway, Arch, Wide, Flicker, Arcade }

        static bool Blocking(EdgeKind k) => k == EdgeKind.Wall || k == EdgeKind.LowWall || k == EdgeKind.Run || k == EdgeKind.Arcade;

        struct Edge
        {
            public EdgeKind Kind;
            public bool Thick;
            // Copy drift: walls that stop short of the ceiling, walls drawn twice, door headers at the wrong height.
            public bool Short, Doubled;
            public float Header;
            public Edge(EdgeKind kind, bool thick) { Kind = kind; Thick = thick; Short = Doubled = false; Header = 0f; }
        }

        enum ZoneKind
        {
            Open, Maze, Hall, Corridor, Room, Office, BreakRoom, CopyRoom, Conference, Reception, Cubicles, Closet,
            HotelRoom, Storage, Vignette, ShowroomPath, Supermarket, Clothing, Concourse, StoreUnit, Atrium, Manila, Origin,
        }

        enum LightLayout { Grid, Dense, Sparse, Rows, DoubleRows, Staggered, CenterStrip }

        sealed class Zone
        {
            public ZoneKind Kind;
            public RectInt Rect;
            public SurfaceRef Wall, Floor, Ceiling, Trim;
            public bool FloorRot, Baseboard, ChairRail, Pilasters, Red, LightsAlongX;
            public bool Sick, NoCeiling;   // copy drift: wrong-coloured light, a ceiling that isn't there
            public bool Pool;              // dark but for one light
            public float Damp;             // extra chance of damp patches (the Arches)
            public bool Shaft;             // open up to the floor above (under a stairwell or a drop)
            public float Sunk;             // floor sits this far down (sunken rooms)
            public float Height, Dead, Water;
            public LightLayout Lights;
        }

        sealed partial class Plan
        {
            readonly uint seed;
            readonly int dx, dz;   // where this district is
            readonly int lx, lz;   // whose plan it uses (itself, or the district it copies)
            readonly uint levelSeed;   // the level's seed; `seed` is this floor's
            readonly int floor;
            readonly LevelTheme theme;
            readonly MazeStyle ms;
            readonly DistrictStyle ds;
            readonly WorldRng rng;
            readonly Vector3 origin;
            readonly int wallpapers, carpets, ceilings;
            readonly int[,] zone = new int[D, D];
            readonly List<Zone> zones = new List<Zone>();
            readonly int[,] owner = new int[D + 1, D + 1];
            readonly bool[,] reserved = new bool[D + 1, D + 1];
            readonly bool[,] used = new bool[D, D];
            readonly List<int> parent = new List<int>();
            readonly List<bool> run = new List<bool>();
            readonly Dictionary<int, Edge> edges = new Dictionary<int, Edge>();
            readonly List<RectInt> solids = new List<RectInt>();
            readonly List<(Bounds box, int zone)> pillars = new List<(Bounds, int)>();
            readonly List<PropSpec> props = new List<PropSpec>();
            readonly List<Bounds> blockers = new List<Bounds>();
            readonly List<(int prop, int blocker)> placed = new List<(int, int)>();   // large props, so doorways can be cleared
            readonly HashSet<int> removedProps = new HashSet<int>(), removedBlockers = new HashSet<int>();
            readonly List<ExitSpec> exits = new List<ExitSpec>();
            readonly List<Bounds> pits = new List<Bounds>();
            readonly bool[,] pit = new bool[D, D];   // a Hole: no floor, a black shaft
            readonly List<PanelSpec> panels = new List<PanelSpec>();
            readonly List<CuriositySlot> slots = new List<CuriositySlot>();
            readonly List<int> palette = new List<int>();
            int mainCarpet, trim;
            readonly float drift;
            readonly bool origin0;
            readonly ForgeRoom forged;
            readonly List<(Bounds box, int zone)> hangers = new List<(Bounds, int)>();
            bool thick, pilasters;
            float chairRailBias;
            public DistrictKind Kind;

            public Plan(uint levelSeed, int floor, int dx, int dz, LevelTheme theme)
            {
                this.levelSeed = levelSeed;
                this.floor = floor;
                seed = HasFloors(theme) ? FloorSeed(levelSeed, floor) : levelSeed;
                this.dx = dx;
                this.dz = dz;
                this.theme = theme;
                ms = theme.style;
                ds = theme.districts;
                origin0 = theme.id == "0" && dx == 0 && dz == 0 && floor == 0;   // the original rooms: the source everything copies
                forged = origin0 || floor != 0 ? null : ForgeLibrary.RoomAt(levelSeed, dx, dz, theme);
                lx = dx; lz = dz;
                // Deliberate copies: a few districts are built from a nearby district's plan (even offsets keep grids aligned).
                if (!origin0 && forged == null && Mathf.Abs(dx) + Mathf.Abs(dz) > 3 && WorldHash.Value(seed, dx, dz, 861) < 0.03f)
                {
                    lx = dx + 2 * (1 + (int)(WorldHash.Value(seed, dx, dz, 862) * 3f)) * (WorldHash.Value(seed, dx, dz, 863) < 0.5f ? 1 : -1);
                    lz = dz + 2 * (int)(WorldHash.Value(seed, dx, dz, 864) * 3f) * (WorldHash.Value(seed, dx, dz, 865) < 0.5f ? 1 : -1);
                    if (lx == 0 && lz == 0) lx = 2;
                }
                rng = new WorldRng(WorldHash.Hash(seed, lx, lz, 0x6D15 + EpochOf(seed, dx, dz) * 7919));
                drift = origin0 ? 0f : DriftAt(seed, dx, dz, theme) * (forged != null ? 0.3f : 1f);
                origin = new Vector3(dx * D * C, 0f, dz * D * C);
                wallpapers = Mathf.Max(1, theme.wallpapers != null ? theme.wallpapers.Length : 1);
                carpets = Mathf.Max(1, theme.carpets != null ? theme.carpets.Length : 1);
                ceilings = Mathf.Max(1, theme.ceilings != null ? theme.ceilings.Length : 1);
                for (int x = 0; x <= D; x++)
                for (int z = 0; z <= D; z++)
                    owner[x, z] = -1;
            }

            public void Build()
            {
                // District character: a small wallpaper palette, a main carpet, one trim, a wall style.
                palette.Add(rng.Value() < 0.5f ? 0 : rng.Range(0, wallpapers));
                for (int i = 1 + rng.Range(1, 3); i > 1; i--) palette.Add(rng.Range(0, wallpapers));
                mainCarpet = rng.Value() < 0.6f ? 0 : rng.Range(0, carpets);
                trim = rng.Range(0, 3);
                thick = rng.Value() < 0.3f;
                pilasters = rng.Value() < 0.3f;
                chairRailBias = rng.Value() * 0.5f;

                SeedRuns();
                var all = new RectInt(0, 0, D, D);
                Paint(all, NewZone(ZoneKind.Open, all));
                Kind = DistrictKind.Maze;
                BuildVerticals();
                if (origin0) { BuildOrigin(); BuildMaze(); }
                else if (PoolHallHere) BuildPoolHall();
                else if (theme.zoneWater) { BuildPoolrooms(); Kind = DistrictKind.Poolrooms; }  // one waterline, one coherent plan
                else if (forged != null && !ControlRoomHere) { BuildForged(forged); BuildMaze(); }   // the pump room wins
                else
                {
                    if (ControlRoomHere)
                    {
                        // The pump room stands alone in a server hall; no other kind of place is built over it.
                        BuildControlRoom();
                        BuildServerHall();
                        Kind = DistrictKind.ServerHall;
                    }
                    else
                    {
                        CarveHalls();
                        ComputeRegions();
                        BuildRegions();
                    }
                }
                SinkRooms();
                ApplyMoods();
                IceRooms();
                Landmarks();

                ScatterPillars();
                ApplyDrift();
                Hazards();
                FurnitureHeap();
                PlaceExits();
                ExitTrails();
                SprinkleProps();
                Wrongness();
                ClearPassages();
                PlaceSlots();
                LowerSunkContents();
                DampPatches();
                LightMoods();
                for (int i = 0; i < zones.Count; i++) { LightZone(i); FloorGlow(i); }
            }

            // =====================================================================================
            // Walls and the union-find that keeps everything reachable
            // =====================================================================================

            int NewGroup(bool isRun)
            {
                parent.Add(parent.Count);
                run.Add(isRun);
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

            static bool OnBorder(int x, int z) => x == 0 || z == 0 || x == D || z == D;

            static bool OnBorderLine(int x0, int z0, int x1, int z1) =>
                x0 == x1 ? x0 == 0 || x0 == D : z0 == 0 || z0 == D;

            static int Key(int x0, int z0, int x1, int z1) =>
                ((Mathf.Min(x0, x1) * (D + 1) + Mathf.Min(z0, z1)) << 1) | (x0 != x1 ? 0 : 1);

            static void Decode(int key, out int x, out int z, out int dir)
            {
                dir = key & 1;
                int v = key >> 1;
                x = v / (D + 1);
                z = v % (D + 1);
            }

            bool IsSolidCell(int x, int z) => x >= 0 && z >= 0 && x < D && z < D && zone[x, z] == SolidZone;

            bool OpenCell(int x, int z) => x >= 0 && z >= 0 && x < D && z < D && zone[x, z] != SolidZone;

            /// The two cells beside an edge: `pos` on the +z (or +x) side, `neg` on the other.
            static void Sides(int key, out Vector2Int pos, out Vector2Int neg)
            {
                Decode(key, out int x, out int z, out int dir);
                pos = new Vector2Int(x, z);
                neg = dir == 0 ? new Vector2Int(x, z - 1) : new Vector2Int(x - 1, z);
            }

            /// Adds a blocking wall. If it would close a loop, link two border runs or touch the district border
            /// away from a run, it becomes `opening` instead (or is skipped when `opening` is null).
            /// `joined` reports that the far vertex already belonged to another wall.
            bool Wall(int x0, int z0, int x1, int z1, EdgeKind kind, EdgeKind? opening, out bool joined)
            {
                joined = false;
                if (OnBorderLine(x0, z0, x1, z1)) return false;
                int key = Key(x0, z0, x1, z1);
                if (edges.ContainsKey(key) || !EdgeAllowed(key)) return false;
                Sides(key, out var p, out var n);
                if (IsSolidCell(p.x, p.y) || IsSolidCell(n.x, n.y) || (Void(p.x, p.y) && Void(n.x, n.y))) return false;   // solids are their own wall

                int ga = owner[x0, z0], gb = owner[x1, z1];
                bool conflict = reserved[x0, z0] || reserved[x1, z1] || (ga < 0 && OnBorder(x0, z0)) || (gb < 0 && OnBorder(x1, z1));
                if (!conflict && ga >= 0 && gb >= 0)
                {
                    int ra = Find(ga), rb = Find(gb);
                    conflict = ra == rb || (run[ra] && run[rb]);
                }
                if (conflict)
                {
                    if (opening.HasValue) edges[key] = new Edge(opening.Value, false);
                    return false;
                }

                joined = gb >= 0;
                if (ga >= 0 && gb >= 0)
                {
                    int ra = Find(ga), rb = Find(gb);
                    parent[ra] = rb;
                    run[rb] |= run[ra];
                }
                else if (ga >= 0) owner[x1, z1] = ga;
                else if (gb >= 0) owner[x0, z0] = gb;
                else
                {
                    int g = NewGroup(false);
                    owner[x0, z0] = g;
                    owner[x1, z1] = g;
                }
                bool border = OnBorder(x0, z0) || OnBorder(x1, z1);   // border walls stay standard so posts match the neighbour
                edges[key] = new Edge(kind, thick && kind == EdgeKind.Wall && !border);
                return true;
            }

            bool Wall(int x0, int z0, int x1, int z1, EdgeKind? opening = EdgeKind.Doorway) => Wall(x0, z0, x1, z1, EdgeKind.Wall, opening, out _);

            /// An explicit opening (placed before the walls, so they leave it alone).
            void Opening(int x0, int z0, int x1, int z1, EdgeKind kind)
            {
                if (OnBorderLine(x0, z0, x1, z1)) return;
                int key = Key(x0, z0, x1, z1);
                if (edges.ContainsKey(key) || !EdgeAllowed(key)) return;
                Sides(key, out var p, out var n);
                if (!OpenCell(p.x, p.y) || !OpenCell(n.x, n.y) || Void(p.x, p.y) || Void(n.x, n.y)) return;
                edges[key] = new Edge(kind, false);
            }

            EdgeKind OpeningStyle()
            {
                float r = rng.Value();
                return r < 0.7f ? EdgeKind.Doorway : r < 0.85f ? EdgeKind.Arch : EdgeKind.Wide;
            }

            void SeedRuns()
            {
                SeedLine(true, 0, dx * D);
                SeedLine(true, D, (dx + 1) * D);
                SeedLine(false, 0, dz * D);
                SeedLine(false, D, (dz + 1) * D);
            }

            void SeedLine(bool alongZ, int local, int line)
            {
                int group = -1, baseAlong = alongZ ? dz * D : dx * D;
                for (int a = 1; a < D - 1; a++)   // corners stay free so lines never meet
                {
                    if (!RunEdge(seed, alongZ, line, baseAlong + a) || HallCrosses(seed, alongZ, line, baseAlong + a)) { group = -1; continue; }
                    if (group < 0) group = NewGroup(true);
                    int x0 = alongZ ? local : a, z0 = alongZ ? a : local;
                    int x1 = alongZ ? local : a + 1, z1 = alongZ ? a + 1 : local;
                    owner[x0, z0] = group;
                    owner[x1, z1] = group;
                    edges[Key(x0, z0, x1, z1)] = new Edge(EdgeKind.Run, false);
                }
            }

            /// A solid block. Walls may butt against it once, like against any other wall.
            bool Solid(RectInt r)
            {
                if (r.xMin < 1 || r.yMin < 1 || r.xMax > D - 1 || r.yMax > D - 1 || !AllAllowed(r)) return false;
                for (int x = r.xMin; x <= r.xMax; x++)
                for (int z = r.yMin; z <= r.yMax; z++)
                    if (owner[x, z] >= 0 || reserved[x, z]) return false;
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                    if (used[x, z]) return false;
                int g = NewGroup(false);
                for (int x = r.xMin; x <= r.xMax; x++)
                for (int z = r.yMin; z <= r.yMax; z++)
                    owner[x, z] = g;
                MarkSolid(r);
                return true;
            }

            /// A solid inside a reserved area (no walls can reach it there, so no bookkeeping is needed).
            void MarkSolid(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                {
                    zone[x, z] = SolidZone;
                    used[x, z] = true;
                }
                solids.Add(r);
            }

            void Reserve(RectInt r)
            {
                for (int x = r.xMin; x <= r.xMax; x++)
                for (int z = r.yMin; z <= r.yMax; z++)
                    reserved[x, z] = true;
            }

            void Use(RectInt r)
            {
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(D, r.xMax); x++)
                for (int z = Mathf.Max(0, r.yMin); z < Mathf.Min(D, r.yMax); z++)
                    used[x, z] = true;
            }

            /// Walls around a room. Extra openings first; then perimeter walls corner-first, so whichever wall
            /// closes the loop (and becomes the doorway) is in the middle of a side.
            void RoomWalls(RectInt r, int extraOpenings, EdgeKind opening)
            {
                var sides = new List<(int x0, int z0, int x1, int z1, float priority)>();
                void Side(int x0, int z0, int dxs, int dzs, int length)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int ax = x0 + dxs * i, az = z0 + dzs * i;
                        float corner = Mathf.Min(i, length - 1 - i);
                        sides.Add((ax, az, ax + dxs, az + dzs, corner + rng.Value() * 0.5f));
                    }
                }
                Side(r.xMin, r.yMin, 1, 0, r.width);
                Side(r.xMin, r.yMax, 1, 0, r.width);
                Side(r.xMin, r.yMin, 0, 1, r.height);
                Side(r.xMax, r.yMin, 0, 1, r.height);

                for (int i = 0; i < extraOpenings && sides.Count > 0; i++)
                {
                    var s = sides[rng.Range(0, sides.Count)];
                    if (s.priority >= 1f) Opening(s.x0, s.z0, s.x1, s.z1, opening);
                }
                sides.Sort((a, b) => a.priority.CompareTo(b.priority));
                foreach (var s in sides) Wall(s.x0, s.z0, s.x1, s.z1, opening);
            }

            /// Walls around an irregular region of cells (shuffled order; loops still become doorways).
            void RegionWalls(List<Vector2Int> cells, HashSet<Vector2Int> inside, EdgeKind opening)
            {
                var list = new List<(int, int, int, int)>();
                foreach (var c in cells)
                    for (int s = 0; s < 4; s++)
                    {
                        var o = new Vector2Int(c.x + Dx[s], c.y + Dz[s]);
                        if (inside.Contains(o)) continue;
                        int x0 = s == 0 ? c.x + 1 : c.x, z0 = s == 1 ? c.y + 1 : c.y;
                        if (s == 0 || s == 2) list.Add((x0, z0, x0, z0 + 1));
                        else list.Add((x0, z0, x0 + 1, z0));
                    }
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
                foreach (var (x0, z0, x1, z1) in list) Wall(x0, z0, x1, z1, opening);
            }

            // =====================================================================================
            // Zones: every room's look
            // =====================================================================================

            int NewZone(ZoneKind kind, RectInt rect)
            {
                var z = new Zone { Kind = kind, Rect = rect };
                z.Wall = new SurfaceRef(Surface.Wall, kind == ZoneKind.Open || kind == ZoneKind.Maze || kind == ZoneKind.Corridor
                    ? palette[0] : palette[rng.Range(0, palette.Count)]);
                bool plainFloor = kind == ZoneKind.Open || kind == ZoneKind.Maze || kind == ZoneKind.Corridor || kind == ZoneKind.Hall || kind == ZoneKind.Atrium;
                z.Floor = new SurfaceRef(Surface.Floor, plainFloor || rng.Value() < 0.5f ? mainCarpet : rng.Range(0, carpets));
                switch (kind)
                {
                    case ZoneKind.Storage: z.Floor = new SurfaceRef(Surface.Concrete); break;
                    case ZoneKind.Supermarket: case ZoneKind.Concourse: z.Floor = new SurfaceRef(Surface.Tile); break;
                    case ZoneKind.Clothing: case ZoneKind.StoreUnit: z.Floor = new SurfaceRef(rng.Value() < 0.5f ? Surface.Wood : Surface.Tile); break;
                    case ZoneKind.Vignette:
                        z.Floor = rng.Value() < 0.4f ? new SurfaceRef(Surface.Wood) : new SurfaceRef(Surface.Floor, rng.Range(0, carpets));
                        z.Wall = new SurfaceRef(Surface.Wall, rng.Range(0, wallpapers));
                        break;
                    case ZoneKind.Manila:
                        z.Wall = new SurfaceRef(Surface.Manila);
                        z.Floor = new SurfaceRef(Surface.Wood);
                        break;
                }
                z.FloorRot = rng.Value() < 0.5f;
                z.Ceiling = new SurfaceRef(Surface.Ceiling, rng.Value() < 0.25f ? rng.Range(0, ceilings) : 0);
                z.Trim = new SurfaceRef(Surface.Trim, trim);
                z.Baseboard = kind != ZoneKind.Storage && rng.Value() < 0.85f;
                z.ChairRail = rng.Value() < chairRailBias + (kind == ZoneKind.Corridor || kind == ZoneKind.Office ? 0.2f : 0f);
                z.Pilasters = pilasters && rng.Value() < 0.6f;
                z.Height = Snap(HeightFor(kind));
                z.Lights = LayoutFor(kind);
                z.LightsAlongX = rect.width >= rect.height ? rng.Value() < 0.8f : rng.Value() < 0.2f;
                bool enclosed = kind != ZoneKind.Open && kind != ZoneKind.Maze && kind != ZoneKind.Hall && kind != ZoneKind.Corridor && kind != ZoneKind.Manila;
                z.Dead = enclosed && rng.Value() < ds.darkRoomChance ? 1f : ms.deadLights;
                if (!enclosed && kind != ZoneKind.Manila) RegionLook(z);
                zones.Add(z);
                return zones.Count - 1;
            }

            static float Snap(float h) => Mathf.Round(h * 10f) / 10f;

            float Range(float a, float b) => Mathf.Lerp(a, b, rng.Value());

            float HeightFor(ZoneKind k)
            {
                switch (k)
                {
                    case ZoneKind.Corridor: return Range(2.4f, 2.7f);
                    case ZoneKind.Closet: return Range(2.4f, 2.6f);
                    case ZoneKind.Conference: case ZoneKind.Cubicles: return Range(2.8f, 3.2f);
                    case ZoneKind.Open: case ZoneKind.Maze: return rng.Value() < 0.65f ? 2.8f : Range(2.5f, 3.8f);
                    case ZoneKind.Hall: return Range(3.2f, 5.6f);
                    case ZoneKind.Atrium: return Range(5f, 6.2f);
                    case ZoneKind.Storage: return Range(4f, 5.2f);
                    case ZoneKind.Supermarket: return Range(3.6f, 4.4f);
                    case ZoneKind.Clothing: return Range(3f, 3.5f);
                    case ZoneKind.Concourse: return Range(4.2f, 5.4f);
                    case ZoneKind.ShowroomPath: return Range(3f, 3.6f);
                    case ZoneKind.Manila: return 2.8f;
                    default: return Range(ds.roomHeight.x, ds.roomHeight.y);
                }
            }

            LightLayout LayoutFor(ZoneKind k)
            {
                float r = rng.Value();
                switch (k)
                {
                    case ZoneKind.Corridor: return r < 0.6f ? LightLayout.CenterStrip : LightLayout.Grid;
                    case ZoneKind.Hall: case ZoneKind.Atrium: return r < 0.4f ? LightLayout.Grid : r < 0.75f ? LightLayout.Rows : LightLayout.Sparse;
                    case ZoneKind.Storage: case ZoneKind.Supermarket: return r < 0.6f ? LightLayout.Rows : LightLayout.DoubleRows;
                    case ZoneKind.Concourse: return r < 0.5f ? LightLayout.DoubleRows : LightLayout.Grid;
                    case ZoneKind.Office: case ZoneKind.Cubicles: return r < 0.4f ? LightLayout.Grid : r < 0.7f ? LightLayout.Dense : LightLayout.Staggered;
                    case ZoneKind.Open: case ZoneKind.Maze:
                        return r < 0.55f ? LightLayout.Grid : r < 0.7f ? LightLayout.Staggered : r < 0.85f ? LightLayout.Sparse : LightLayout.Rows;
                    default: return r < 0.5f ? LightLayout.Grid : r < 0.75f ? LightLayout.Staggered : LightLayout.Dense;
                }
            }

            void Paint(RectInt r, int id)
            {
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(D, r.xMax); x++)
                for (int z = Mathf.Max(0, r.yMin); z < Mathf.Min(D, r.yMax); z++)
                    if (zone[x, z] != SolidZone && Allowed(x, z) && (locked == null || !locked[x, z]) && (zones.Count == 0 || zones[zone[x, z]].Kind != ZoneKind.Origin)) zone[x, z] = id;   // the original rooms are never painted over
            }

            int Room(RectInt r, ZoneKind kind, int extraOpenings)
            {
                int id = NewZone(kind, r);
                Paint(r, id);
                RoomWalls(r, extraOpenings, OpeningStyle());
                return id;
            }

            /// Cells next to the district border keep a standard height, so neighbouring districts always meet flush.
            float HeightAt(int x, int z)
            {
                int id = zone[x, z];
                if (id == SolidZone) return BorderHeight;
                float h = zones[id].Height;
                if (zones[id].Shaft) return h;
                if (ds.ceilingCap > 0f) h = Mathf.Min(h, ds.ceilingCap);
                // Copy drift: the ceiling forgets it was flat.
                if (drift > 0.3f)
                    h = Mathf.Max(2.3f, Snap(h + (WorldHash.Value(seed, dx * D + x, dz * D + z, 911) - 0.5f) * drift));
                return h;
            }

            Vector3 CellCenter(int x, int z) => origin + new Vector3((x + 0.5f) * C, 0f, (z + 0.5f) * C);

            // =====================================================================================
            // Archetypes
            // =====================================================================================

            void BuildMaze()
            {
                for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    var r = new RectInt(i * 8, j * 8, 8, 8);
                    Paint(r, NewZone(ZoneKind.Maze, r));
                }
                float f = rng.Value();
                var fr = FeatureRect();
                float featureRoll = theme.id == "0" && !origin0 ? 0.58f : 0.45f;
                if (f < featureRoll && !origin0 && forged == null && AllAllowed(fr))
                {
                    var r = fr;
                    Reserve(r);
                    float kindRoll = f / featureRoll;
                    if (kindRoll < 0.34f) BlockGrid(r);
                    else if (kindRoll < 0.67f) PillarPatch(r, false);
                    else PitField(r);
                }
                if (!origin0 && forged == null && rng.Value() < ds.manilaChance * 2f) ManilaRoom();
                // More stubs and dead-end texture in Level 0, while reachability remains guaranteed by the wall rules.
                for (int i = rng.Range(theme.id == "0" && !origin0 ? 1 : 0, theme.id == "0" && !origin0 ? 8 : 6); i > 0; i--) RandomSolid(3);
                float density = 0.5f * (WorldHash.Value(seed, lx >> 1, lz >> 1, 517) + rng.Value());
                float tightness = theme.id == "0" && !origin0 ? 1.16f : 1f;
                int walkers = Mathf.RoundToInt(Mathf.Lerp(ms.walkers.x, ms.walkers.y, density) * 6f * tightness);   // denser beyond the original rooms
                for (int i = 0; i < walkers; i++) Walker(new RectInt(0, 0, D, D), density, ms.roomChance, ms.spiralChance, ms.wiggle, ms.maxWallLength);
                if (theme.id == "0" && !origin0 && forged == null && rng.Value() < 0.45f)
                    for (int i = rng.Range(1, 4); i > 0; i--) RandomSolid(rng.Range(2, 4));
                // A few extra doorways/arches punch loops and odd connections through tight mazes without reducing reachability.
                if (theme.id == "0" && !origin0)
                {
                    int loops = rng.Range(1, 6);
                    for (int n = 0; n < loops * 14 && loops > 0; n++)
                    {
                        int x = rng.Range(1, D - 1), z = rng.Range(1, D - 1);
                        bool horiz = rng.Value() < 0.5f;
                        int x1 = horiz ? x + 1 : x, z1 = horiz ? z : z + 1;
                        if (!OpenCell(x, z) || !OpenCell(x1, z1)) continue;
                        int key = Key(x, z, x1, z1);
                        if (!edges.TryGetValue(key, out var e) || !Blocking(e.Kind)) continue;
                        if (locked != null && locked[x, z] && locked[x1, z1]) continue;
                        edges[key] = new Edge(rng.Value() < 0.78f ? EdgeKind.Doorway : EdgeKind.Arch, false);
                        loops--;
                    }
                }
            }

            void BuildOpenExpanse()
            {
                int bands = rng.Range(1, 4);
                bool alongX = rng.Value() < 0.5f;
                int start = 0;
                for (int b = 0; b < bands; b++)
                {
                    int end = b == bands - 1 ? D : start + rng.Range(8, Mathf.Max(9, D - start - 8 * (bands - b - 1)));
                    var r = alongX ? new RectInt(0, start, D, end - start) : new RectInt(start, 0, end - start, D);
                    Paint(r, NewZone(rng.Value() < 0.2f ? ZoneKind.Hall : ZoneKind.Open, r));
                    if (b > 0)
                    {
                        // A long dividing wall with wide openings, arches or doorways along it.
                        var kind = OpeningStyle();
                        int gap = rng.Range(3, 7);
                        for (int i = 1; i < D - 1; i++)
                        {
                            if (i % gap == gap / 2) Opening(alongX ? i : start, alongX ? start : i, alongX ? i + 1 : start, alongX ? start : i + 1, kind);
                            Wall(alongX ? i : start, alongX ? start : i, alongX ? i + 1 : start, alongX ? start : i + 1, kind);
                        }
                    }
                    start = end;
                }
                if (rng.Value() < 0.4f)
                {
                    int s = rng.Range(4, 7), ox = rng.Range(1, s), oz = rng.Range(1, s);
                    for (int x = ox; x < D - 1; x += s)
                    for (int z = oz; z < D - 1; z += s)
                        Pillar(x, z, rng.Value() < 0.5f, 0.8f);
                }
                if (rng.Value() < ds.manilaChance * 2f) ManilaRoom();
                for (int i = rng.Range(0, 4); i > 0; i--) RandomSolid(4);
                int walkers = rng.Range(theme.id == "0" && !origin0 ? 26 : 22, theme.id == "0" && !origin0 ? 56 : 48);   // more broken-up open space in Level 0
                for (int i = 0; i < walkers; i++) Walker(new RectInt(0, 0, D, D), 0.32f, 0.1f, 0f, 0.06f, 7);
                if (rng.Value() < 0.38f)
                    for (int i = rng.Range(2, 7); i > 0; i--) LowSegment();
                // Broken-up expanses sometimes gain extra pass-throughs and looping side connections.
                if (theme.id == "0" && !origin0 && rng.Value() < 0.55f)
                {
                    int loops = rng.Range(1, 5);
                    for (int n = 0; n < loops * 14 && loops > 0; n++)
                    {
                        int x = rng.Range(1, D - 1), z = rng.Range(1, D - 1);
                        bool horiz = rng.Value() < 0.5f;
                        int x1 = horiz ? x + 1 : x, z1 = horiz ? z : z + 1;
                        if (!OpenCell(x, z) || !OpenCell(x1, z1)) continue;
                        int key = Key(x, z, x1, z1);
                        if (!edges.TryGetValue(key, out var e) || !Blocking(e.Kind)) continue;
                        if (locked != null && locked[x, z] && locked[x1, z1]) continue;
                        edges[key] = new Edge(rng.Value() < 0.7f ? EdgeKind.Doorway : (rng.Value() < 0.5f ? EdgeKind.Arch : EdgeKind.Wide), false);
                        loops--;
                    }
                }
            }

            void BuildPillarHall()
            {
                var all = new RectInt(0, 0, D, D);
                Paint(all, NewZone(ZoneKind.Hall, all));
                int s = rng.Range(2, 5), ox = rng.Range(0, s), oz = rng.Range(0, s);
                bool cross = rng.Value() < 0.35f;
                float size = rng.Value() < 0.5f ? 0.7f : rng.Value() < 0.5f ? 0.5f : 1.1f;
                for (int x = 1 + ox; x < D - 1; x += s)
                for (int z = 1 + oz; z < D - 1; z += s)
                    Pillar(x, z, cross, size);
                if (rng.Value() < 0.4f)
                {
                    // A wall of wide openings slicing the hall, like a colonnade.
                    bool alongX = rng.Value() < 0.5f;
                    int line = rng.Range(8, D - 8);
                    for (int i = 1; i < D - 1; i++)
                    {
                        if (rng.Value() < 0.6f) Opening(alongX ? i : line, alongX ? line : i, alongX ? i + 1 : line, alongX ? line : i + 1, EdgeKind.Wide);
                        Wall(alongX ? i : line, alongX ? line : i, alongX ? i + 1 : line, alongX ? line : i + 1, EdgeKind.Wide);
                    }
                }
            }

            /// Offices: binary space partition into rooms and corridors, each room typed and furnished.
            void BuildOffices(RectInt area, bool corridors)
            {
                if (area.width < 3 || area.height < 3)
                {
                    if (area.width > 0 && area.height > 0) Paint(area, NewZone(ZoneKind.Open, area));
                    return;
                }
                var leaves = new List<RectInt>();
                var halls = new List<RectInt>();
                Bsp(area, 0, 5, 3, corridors ? 0.55f : 0f, leaves, halls);
                foreach (var h in halls) Paint(h, NewZone(ZoneKind.Corridor, h));
                foreach (var leaf in leaves)
                {
                    int cells = leaf.width * leaf.height;
                    ZoneKind kind;
                    float r = rng.Value();
                    if (cells <= 9) kind = r < 0.6f ? ZoneKind.Closet : ZoneKind.CopyRoom;
                    else if (cells >= 40) kind = r < 0.35f ? ZoneKind.Cubicles : r < 0.6f ? ZoneKind.Conference : r < 0.8f ? ZoneKind.Office : ZoneKind.Open;
                    else kind = r < 0.4f ? ZoneKind.Office : r < 0.6f ? ZoneKind.BreakRoom : r < 0.75f ? ZoneKind.Reception : r < 0.9f ? ZoneKind.CopyRoom : ZoneKind.Room;
                    int id = NewZone(kind, leaf);
                    Paint(leaf, id);
                    DoorTowardCorridor(leaf);
                    RoomWalls(leaf, rng.Range(0, 2), OpeningStyle());
                    Furnish(id);
                }
            }

            void DoorTowardCorridor(RectInt r)
            {
                var options = new List<(int, int, int, int)>();
                for (int x = r.xMin + 1; x < r.xMax - 1; x++)
                {
                    if (CellKind(x, r.yMin - 1) == ZoneKind.Corridor) options.Add((x, r.yMin, x + 1, r.yMin));
                    if (CellKind(x, r.yMax) == ZoneKind.Corridor) options.Add((x, r.yMax, x + 1, r.yMax));
                }
                for (int z = r.yMin + 1; z < r.yMax - 1; z++)
                {
                    if (CellKind(r.xMin - 1, z) == ZoneKind.Corridor) options.Add((r.xMin, z, r.xMin, z + 1));
                    if (CellKind(r.xMax, z) == ZoneKind.Corridor) options.Add((r.xMax, z, r.xMax, z + 1));
                }
                if (options.Count == 0) return;
                var (a, b, c, d) = options[rng.Range(0, options.Count)];
                Opening(a, b, c, d, rng.Value() < 0.85f ? EdgeKind.Doorway : EdgeKind.Arch);
            }

            ZoneKind? CellKind(int x, int z) => OpenCell(x, z) ? zones[zone[x, z]].Kind : (ZoneKind?)null;

            void Bsp(RectInt r, int depth, int maxDepth, int minSide, float corridorChance, List<RectInt> leaves, List<RectInt> halls)
            {
                bool canV = r.width >= 2 * minSide + 1, canH = r.height >= 2 * minSide + 1;
                if ((!canV && !canH) || depth >= maxDepth || (depth > 1 && rng.Value() < 0.2f))
                {
                    leaves.Add(r);
                    return;
                }
                bool vertical = canV && (!canH || (r.width == r.height ? rng.Value() < 0.5f : r.width > r.height));
                int lo = vertical ? r.xMin : r.yMin, len = vertical ? r.width : r.height;
                int cw = rng.Value() < corridorChance ? (rng.Value() < 0.7f ? 1 : 2) : 0;
                if (len < 2 * minSide + cw) cw = 0;
                int s = lo + rng.Range(minSide, len - minSide - cw + 1);
                RectInt a, b, hall;
                if (vertical)
                {
                    a = new RectInt(r.xMin, r.yMin, s - r.xMin, r.height);
                    hall = new RectInt(s, r.yMin, cw, r.height);
                    b = new RectInt(s + cw, r.yMin, r.xMax - s - cw, r.height);
                }
                else
                {
                    a = new RectInt(r.xMin, r.yMin, r.width, s - r.yMin);
                    hall = new RectInt(r.xMin, s, r.width, cw);
                    b = new RectInt(r.xMin, s + cw, r.width, r.yMax - s - cw);
                }
                if (cw > 0) halls.Add(hall);
                Bsp(a, depth + 1, maxDepth, minSide, corridorChance, leaves, halls);
                Bsp(b, depth + 1, maxDepth, minSide, corridorChance, leaves, halls);
            }

            /// A grid of corridors with changing widths. Blocks between them become solid mass, rows of small
            /// rooms behind doors, or single rooms. As a showroom, corridors are a path and blocks become vignettes.
            void BuildCorridors(bool showroom)
            {
                var corridor = new bool[D, D];
                var lineZones = new List<int>();
                var lines = new List<(bool alongX, int p, int w)>();
                void Lines(bool alongX)
                {
                    for (int p = rng.Range(1, 5); p < D - 2;)
                    {
                        int w = showroom ? 2 : rng.Range(1, 4);
                        var rect = alongX ? new RectInt(0, p, D, w) : new RectInt(p, 0, w, D);
                        int id = NewZone(showroom ? ZoneKind.ShowroomPath : ZoneKind.Corridor, rect);
                        lineZones.Add(id);
                        // The width changes along the corridor, stepping out on one side.
                        int seg = 0, extra = 0;
                        for (int a = 0; a < D; a++)
                        {
                            if (a >= seg) { seg = a + rng.Range(4, 12); extra = showroom ? 0 : (rng.Value() < 0.3f ? 1 : 0); }
                            for (int k = 0; k < w + extra && p + k < D - 1; k++)
                            {
                                int x = alongX ? a : p + k, z = alongX ? p + k : a;
                                corridor[x, z] = true;
                                zone[x, z] = id;
                            }
                        }
                        if (!showroom) lines.Add((alongX, p, w));
                        p += w + rng.Range(showroom ? 5 : 4, showroom ? 8 : 10);
                    }
                }
                Lines(true);
                Lines(false);

                // Flood the blocks between corridors.
                var seen = new bool[D, D];
                for (int x = 0; x < D; x++)
                for (int z = 0; z < D; z++)
                {
                    if (corridor[x, z] || seen[x, z]) continue;
                    var cells = new List<Vector2Int>();
                    var inside = new HashSet<Vector2Int>();
                    var q = new Queue<Vector2Int>();
                    q.Enqueue(new Vector2Int(x, z));
                    seen[x, z] = true;
                    while (q.Count > 0)
                    {
                        var c = q.Dequeue();
                        cells.Add(c);
                        inside.Add(c);
                        for (int s = 0; s < 4; s++)
                        {
                            int nx = c.x + Dx[s], nz = c.y + Dz[s];
                            if (nx < 0 || nz < 0 || nx >= D || nz >= D || corridor[nx, nz] || seen[nx, nz]) continue;
                            seen[nx, nz] = true;
                            q.Enqueue(new Vector2Int(nx, nz));
                        }
                    }
                    var bounds = Bounding(cells);
                    bool rect = bounds.width * bounds.height == cells.Count;
                    if (showroom && rect) Vignettes(bounds);
                    else if (!showroom && rect && rng.Value() < 0.35f && Solid(bounds)) DoorsToNowhere(bounds);
                    else if (rect && cells.Count >= 10 && rng.Value() < 0.55f) RoomRow(bounds, showroom ? ZoneKind.Vignette : ZoneKind.HotelRoom);
                    else
                    {
                        int id = NewZone(cells.Count > 30 ? ZoneKind.Room : ZoneKind.Closet, bounds);
                        foreach (var c in cells) zone[c.x, c.y] = id;
                        RegionWalls(cells, inside, OpeningStyle());
                        Furnish(id);
                    }
                }

                // Dead ends: some corridors are walled off partway. A cap that would seal anything off is skipped.
                foreach (var (alongX, p, w) in lines)
                {
                    if (rng.Value() >= 0.45f) continue;
                    int a = rng.Range(3, D - 3);
                    for (int k = 0; k < w; k++)
                        if (alongX) Wall(a, p + k, a, p + k + 1, null);
                        else Wall(p + k, a, p + k + 1, a, null);
                }
            }

            static RectInt Bounding(List<Vector2Int> cells)
            {
                int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
                foreach (var c in cells)
                {
                    x0 = Mathf.Min(x0, c.x); z0 = Mathf.Min(z0, c.y);
                    x1 = Mathf.Max(x1, c.x); z1 = Mathf.Max(z1, c.y);
                }
                return new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            }

            /// "Hallways of doors": doors on the solid block's faces that lead nowhere.
            void DoorsToNowhere(RectInt b)
            {
                if (rng.Value() < 0.5f) return;
                for (int x = b.xMin; x < b.xMax; x += 2)
                {
                    TryFaceProp(x, b.yMin - 1, x, b.yMin, PropKind.DoorToNowhere, 0.5f);
                    TryFaceProp(x, b.yMax, x, b.yMax - 1, PropKind.DoorToNowhere, 0.5f);
                }
                for (int z = b.yMin; z < b.yMax; z += 2)
                {
                    TryFaceProp(b.xMin - 1, z, b.xMin, z, PropKind.DoorToNowhere, 0.5f);
                    TryFaceProp(b.xMax, z, b.xMax - 1, z, PropKind.DoorToNowhere, 0.5f);
                }
            }

            /// A prop standing in cell `open`, backed against the face of neighbouring cell `wall`.
            void TryFaceProp(int ox, int oz, int wx, int wz, PropKind kind, float chance)
            {
                if (!OpenCell(ox, oz) || used[ox, oz] || rng.Value() >= chance) return;
                var normal = new Vector3(ox - wx, 0f, oz - wz);
                var at = CellCenter(ox, oz) - normal * (C / 2);
                AddProp(kind, Matrix4x4.TRS(at, Quaternion.LookRotation(normal), Vector3.one), ox, oz);
                used[ox, oz] = true;
            }

            /// A row of small rooms along the block's longer side, each with its own door onto the corridor.
            void RoomRow(RectInt b, ZoneKind kind)
            {
                bool alongX = b.width >= b.height;
                int length = alongX ? b.width : b.height;
                for (int a = 0; a < length;)
                {
                    int w = Mathf.Min(length - a, rng.Range(2, 4));
                    if (length - a - w == 1) w++;
                    var r = alongX ? new RectInt(b.xMin + a, b.yMin, w, b.height) : new RectInt(b.xMin, b.yMin + a, b.width, w);
                    int id = NewZone(kind, r);
                    Paint(r, id);
                    DoorTowardCorridor(r);
                    RoomWalls(r, 0, kind == ZoneKind.Vignette ? EdgeKind.Wide : EdgeKind.Doorway);
                    Furnish(id);
                    a += w;
                }
            }

            /// Showroom nooks: three-walled rooms open onto the path, each staged with furniture.
            void Vignettes(RectInt b)
            {
                bool alongX = b.width >= b.height;
                int length = alongX ? b.width : b.height, depth = alongX ? b.height : b.width;
                int split = depth >= 6 ? depth / 2 : depth;
                for (int half = 0; half < depth; half += split)
                {
                    int d = Mathf.Min(split, depth - half);
                    for (int a = 0; a < length;)
                    {
                        int w = Mathf.Min(length - a, rng.Range(3, 5));
                        if (length - a - w < 3) w = length - a;
                        var r = alongX ? new RectInt(b.xMin + a, b.yMin + half, w, d) : new RectInt(b.xMin + half, b.yMin + a, d, w);
                        int id = NewZone(ZoneKind.Vignette, r);
                        Paint(r, id);
                        OpenTowardPath(r);
                        RoomWalls(r, 0, EdgeKind.Wide);
                        Furnish(id);
                        a += w;
                    }
                }
            }

            void OpenTowardPath(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    if (CellKind(x, r.yMin - 1) == ZoneKind.ShowroomPath) Opening(x, r.yMin, x + 1, r.yMin, EdgeKind.Wide);
                    if (CellKind(x, r.yMax) == ZoneKind.ShowroomPath) Opening(x, r.yMax, x + 1, r.yMax, EdgeKind.Wide);
                }
                for (int z = r.yMin; z < r.yMax; z++)
                {
                    if (CellKind(r.xMin - 1, z) == ZoneKind.ShowroomPath) Opening(r.xMin, z, r.xMin, z + 1, EdgeKind.Wide);
                    if (CellKind(r.xMax, z) == ZoneKind.ShowroomPath) Opening(r.xMax, z, r.xMax, z + 1, EdgeKind.Wide);
                }
            }

            /// Rows of free-standing wall panels, like the original Level 0 photo.
            void BuildPanelRows()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Open, all);
                zones[id].ChairRail = rng.Value() < 0.6f;
                Paint(all, id);
                bool alongX = rng.Value() < 0.5f;
                for (int line = rng.Range(2, 4); line < D - 1; line += rng.Range(2, 4))
                {
                    for (int pos = rng.Range(1, 4); pos < D - 1;)
                    {
                        int len = rng.Range(2, 7);
                        for (int i = 0; i < len && pos + i < D - 1; i++)
                            Wall(alongX ? pos + i : line, alongX ? line : pos + i, alongX ? pos + i + 1 : line, alongX ? line : pos + i + 1, null);
                        if (rng.Value() < 0.25f)   // a short stub at the end
                        {
                            int e = Mathf.Min(pos + len, D - 2), dir = rng.Value() < 0.5f ? 1 : -1;
                            Wall(alongX ? e : line, alongX ? line : e, alongX ? e : line + dir, alongX ? line + dir : e, null);
                        }
                        pos += len + rng.Range(1, 3);
                    }
                }
            }

            /// Rooms inside rooms inside rooms, each ring its own look and height.
            void BuildNested()
            {
                for (int n = rng.Range(1, 4); n > 0; n--)
                {
                    int w = rng.Range(10, 26), h = rng.Range(10, 26);
                    var r = new RectInt(rng.Range(1, Mathf.Max(2, D - w)), rng.Range(1, Mathf.Max(2, D - h)), w, h);
                    float step = rng.Value() < 0.5f ? 0.2f : -0.2f, height = Range(2.6f, 3.4f);
                    while (r.width >= 4 && r.height >= 4)
                    {
                        int id = Room(r, ZoneKind.Room, rng.Range(0, 2));
                        zones[id].Height = Snap(Mathf.Clamp(height, 2.4f, 4.2f));
                        height += step;
                        int inset = rng.Range(2, 4);
                        r = new RectInt(r.xMin + inset, r.yMin + inset, r.width - 2 * inset, r.height - 2 * inset);
                    }
                }
            }

            /// A tall central atrium ringed by columns and a low corridor, with rooms around it.
            void BuildAtrium()
            {
                int size = rng.Range(12, 20), ring = rng.Range(2, 4);
                var a = new RectInt((D - size) / 2 + rng.Range(-2, 3), (D - size) / 2 + rng.Range(-2, 3), size, size);
                var outer = new RectInt(a.xMin - ring, a.yMin - ring, size + 2 * ring, size + 2 * ring);
                // Outside the ring: four bands of rooms.
                BuildOffices(new RectInt(0, 0, D, outer.yMin), false);
                BuildOffices(new RectInt(0, outer.yMax, D, D - outer.yMax), false);
                BuildOffices(new RectInt(0, outer.yMin, outer.xMin, outer.height), false);
                BuildOffices(new RectInt(outer.xMax, outer.yMin, D - outer.xMax, outer.height), false);
                int ringId = NewZone(ZoneKind.Corridor, outer);
                Paint(outer, ringId);
                RoomWalls(outer, 2, EdgeKind.Wide);
                int atriumId = NewZone(ZoneKind.Atrium, a);
                Paint(a, atriumId);
                for (int x = a.xMin; x < a.xMax; x += 2)
                {
                    Pillar(x, a.yMin, false, 0.6f);
                    Pillar(x, a.yMax - 1, false, 0.6f);
                }
                for (int z = a.yMin + 2; z < a.yMax - 1; z += 2)
                {
                    Pillar(a.xMin, z, false, 0.6f);
                    Pillar(a.xMax - 1, z, false, 0.6f);
                }
            }

            /// Back-of-store storage: aisles of shelving racks, pallets, boxes, a loading wall.
            void BuildStorage()
            {
                var all = new RectInt(0, 0, D, D);
                int split = rng.Value() < 0.5f ? rng.Range(20, 27) : D;
                var main = new RectInt(0, 0, D, split);
                int id = NewZone(ZoneKind.Storage, main);
                Paint(main, id);
                if (split < D) BuildOffices(new RectInt(0, split, D, D - split), true);
                bool alongX = rng.Value() < 0.5f;
                var area = ToWorld(main, 1.6f);
                float period = 3.2f, rackLen = 2.4f;
                for (float row = (alongX ? area.yMin : area.xMin) + 1f; row < (alongX ? area.yMax : area.xMax) - 1f; row += period)
                {
                    int count = 0;
                    for (float p = (alongX ? area.xMin : area.yMin) + rackLen / 2; p < (alongX ? area.xMax : area.yMax) - rackLen / 2; p += rackLen, count++)
                    {
                        if (count % 5 == 4) continue;   // cross aisle
                        var at = alongX ? new Vector3(p, 0f, row) : new Vector3(row, 0f, p);
                        PlaceFree(PropKind.ShelfRack, at, alongX ? 0f : 90f, true);
                    }
                }
                for (int i = rng.Range(2, 7); i > 0; i--)
                    PlaceInZone(id, rng.Value() < 0.5f ? PropKind.Pallet : PropKind.StackedBoxes, true);
                for (int i = rng.Range(1, 4); i > 0; i--) PlaceOnWall(id, PropKind.RollUpDoor);
            }

            void BuildSupermarket()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Supermarket, all);
                Paint(all, id);
                var area = ToWorld(all, 2f);
                // Checkout lanes along the south, freezers along the north, aisles of shelving between.
                for (float x = area.xMin + 2f; x < area.xMax - 2f; x += 3.4f)
                    PlaceFree(PropKind.Checkout, new Vector3(x, 0f, area.yMin + 3f), 0f, true);
                for (float x = area.xMin + 2f; x < area.xMax - 2f; x += 2f)
                    PlaceFree(PropKind.Freezer, new Vector3(x, 0f, area.yMax - 1f), 180f, true);
                for (float x = area.xMin + 2f; x < area.xMax - 2f; x += 3.2f)
                    for (float z = area.yMin + 7f; z < area.yMax - 4f; z += 2f)
                    {
                        if (Mathf.Abs(z - (area.yMin + area.yMax) / 2) < 1.5f) continue;   // middle cross aisle
                        PlaceFree(PropKind.Gondola, new Vector3(x, 0f, z), 90f, true);
                    }
            }

            void BuildClothing()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Clothing, all);
                Paint(all, id);
                // Fitting rooms: a row of stalls with curtains.
                int fx = rng.Range(3, 10), fz = rng.Range(3, 20), stalls = rng.Range(4, 7);
                var row = new RectInt(fx, fz, stalls, 1);
                for (int i = 0; i < stalls; i++)
                {
                    var stall = new RectInt(fx + i, fz, 1, 1);
                    int sid = NewZone(ZoneKind.Closet, stall);
                    Paint(stall, sid);
                    Opening(fx + i, fz, fx + i + 1, fz, EdgeKind.Doorway);
                    RoomWalls(stall, 0, EdgeKind.Doorway);
                    AddProp(PropKind.FittingCurtain, Matrix4x4.TRS(origin + new Vector3((fx + i + 0.5f) * C, 0f, fz * C), Quaternion.identity, Vector3.one), fx + i, fz);
                }
                Use(new RectInt(row.xMin - 1, row.yMin - 1, row.width + 2, row.height + 2));
                var area = ToWorld(all, 2f);
                for (float x = area.xMin + 1.5f; x < area.xMax - 1.5f; x += 3f)
                    for (float z = area.yMin + 1.5f; z < area.yMax - 1.5f; z += 3f)
                    {
                        var at = new Vector3(x + Range(-0.4f, 0.4f), 0f, z + Range(-0.4f, 0.4f));
                        if (rng.Value() < 0.7f && FreeAt(at)) PlaceFree(rng.Value() < 0.5f ? PropKind.ClothingRack : PropKind.ClothingRound, at, rng.Value() < 0.5f ? 0f : 90f, true);
                    }
                PlaceInZone(id, PropKind.Counter, true);
            }

            /// A mall: a wide concourse with storefronts on both sides behind wide openings and shutters.
            void BuildMall()
            {
                int w = rng.Range(3, 5), z0 = rng.Range(12, D - 12 - w);
                var concourse = new RectInt(0, z0, D, w);
                int cid = NewZone(ZoneKind.Concourse, concourse);
                Paint(concourse, cid);
                void Units(int zFrom, int zTo, bool south)
                {
                    for (int x = 0; x < D;)
                    {
                        int uw = Mathf.Min(D - x, rng.Range(3, 6));
                        if (D - x - uw < 3) uw = D - x;
                        var r = new RectInt(x, zFrom, uw, zTo - zFrom);
                        int id = NewZone(ZoneKind.StoreUnit, r);
                        Paint(r, id);
                        int front = south ? zTo : zFrom;
                        for (int i = x + 1; i < x + uw - 1; i++)
                        {
                            Opening(i, front, i + 1, front, EdgeKind.Wide);
                            AddProp(PropKind.Shutter, Matrix4x4.TRS(origin + new Vector3((i + 0.5f) * C, 0f, front * C), Quaternion.Euler(0f, south ? 0f : 180f, 0f), Vector3.one), i, south ? front : front - 1);
                        }
                        RoomWalls(r, 0, EdgeKind.Doorway);
                        Furnish(id);
                        x += uw;
                    }
                }
                Units(0, z0, true);
                Units(z0 + w, D, false);
                var area = ToWorld(concourse, 0.5f);
                float mid = (area.yMin + area.yMax) / 2;
                for (float x = area.xMin + 4f; x < area.xMax - 4f; x += 8f)
                {
                    if (Mathf.Abs(x - (area.xMin + area.xMax) / 2) < 4f && w >= 4) { PlaceFree(PropKind.Fountain, new Vector3(x, 0f, mid), 0f, true); continue; }
                    PlaceFree(rng.Value() < 0.5f ? PropKind.Bench : PropKind.Planter, new Vector3(x, 0f, mid), 0f, true);
                }
            }

            // =====================================================================================
            // Features
            // =====================================================================================

            RectInt FeatureRect()
            {
                var r = new RectInt(0, 0, rng.Range(6, 11), rng.Range(6, 11));
                r.position = new Vector2Int(rng.Range(2, D - r.width - 1), rng.Range(2, D - r.height - 1));
                return r;
            }

            void BlockGrid(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x += 2)
                for (int z = r.yMin; z < r.yMax; z += 2)
                    MarkSolid(new RectInt(x, z, 1, 1));
            }

            void PillarPatch(RectInt r, bool cross)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                    if (((dx * D + x) & 1) == 1 && ((dz * D + z) & 1) == 1) Pillar(x, z, cross, 0.8f);
            }

            /// Level 0 "Holes": a floor that's nothing but square pits, a grid of narrow walkways between them. The pits
            /// are lined with the room's own walls and fade to black. Fall in and you're back at your checkpoint
            /// (the trigger sits near the bottom of the shaft).
            void PitField(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                {
                    if (!OpenCell(x, z) || used[x, z]) continue;
                    pit[x, z] = true;
                    float open = C - 2f * PitRim - 0.1f;
                    pits.Add(new Bounds(CellCenter(x, z) + Vector3.down * (PitDepth - 0.5f), new Vector3(open, 0.6f, open)));
                    used[x, z] = true;
                }
            }

            void Pillar(int x, int z, bool cross, float size)
            {
                if (!OpenCell(x, z) || used[x, z] || !Allowed(x, z)) return;
                used[x, z] = true;
                var c = CellCenter(x, z) + Vector3.up * (ds.wallHeight / 2);
                if (cross)
                {
                    pillars.Add((new Bounds(c, new Vector3(1.2f, ds.wallHeight, 0.35f)), zone[x, z]));
                    pillars.Add((new Bounds(c, new Vector3(0.35f, ds.wallHeight, 1.2f)), zone[x, z]));
                }
                else pillars.Add((new Bounds(c, new Vector3(size, ds.wallHeight, size)), zone[x, z]));
            }

            /// Pillars outside the pillar halls too: a loose grid in a big open room, or a few lone columns.
            /// They stand mid-cell, so they never affect reachability.
            void ScatterPillars()
            {
                // Level 37 places its own pillars, already lowered into the basins. Generic pillars would stand
                // at y=0 over a four-metre drop and poke through the ceiling of a sunken room.
                if (Kind == DistrictKind.PillarHall || theme.zoneWater) return;
                var open = new List<int>();
                for (int i = 0; i < zones.Count; i++)
                {
                    var k = zones[i].Kind;
                    if ((k == ZoneKind.Open || k == ZoneKind.Maze || k == ZoneKind.Hall) && zones[i].Rect.width * zones[i].Rect.height >= 36) open.Add(i);
                }
                if (open.Count == 0) return;
                if (rng.Value() < 0.35f)
                {
                    int id = open[rng.Range(0, open.Count)];
                    var r = zones[id].Rect;
                    int s = rng.Range(3, 6), ox = rng.Range(0, s), oz = rng.Range(0, s);
                    bool cross = rng.Value() < 0.25f;
                    float size = rng.Value() < 0.5f ? 0.6f : 0.9f;
                    for (int x = r.xMin + 1 + ox; x < r.xMax - 1; x += s)
                    for (int z = r.yMin + 1 + oz; z < r.yMax - 1; z += s)
                        if (FreeStanding(x, z) && zone[x, z] == id) Pillar(x, z, cross, size);
                }
                for (int i = rng.Range(0, ds.wikiZones ? 10 : 5); i > 0; i--)   // Level 0: lone pillars all over
                {
                    int id = open[rng.Range(0, open.Count)];
                    var r = zones[id].Rect;
                    int x = rng.Range(Mathf.Max(1, r.xMin), Mathf.Min(D - 1, r.xMax)), z = rng.Range(Mathf.Max(1, r.yMin), Mathf.Min(D - 1, r.yMax));
                    if (FreeStanding(x, z) && zone[x, z] == id) Pillar(x, z, false, rng.Value() < 0.5f ? 0.6f : 0.9f);
                }
            }

            /// A cell in open floor: no wall or opening on any side and open neighbours, so a pillar here can be walked around.
            bool FreeStanding(int x, int z)
            {
                if (x < 1 || z < 1 || x >= D - 1 || z >= D - 1 || !OpenCell(x, z) || used[x, z]) return false;
                if (edges.ContainsKey(Key(x, z, x + 1, z)) || edges.ContainsKey(Key(x, z + 1, x + 1, z + 1))
                    || edges.ContainsKey(Key(x, z, x, z + 1)) || edges.ContainsKey(Key(x + 1, z, x + 1, z + 1))) return false;
                for (int s = 0; s < 4; s++)
                    if (!OpenCell(x + Dx[s], z + Dz[s])) return false;
                return true;
            }

            void RandomSolid(int maxSide)
            {
                int w = rng.Range(1, maxSide + 1), h = rng.Range(1, maxSide + 1);
                Solid(new RectInt(rng.Range(1, D - w), rng.Range(1, D - h), w, h));
            }

            void LowSegment()
            {
                int x = rng.Range(2, D - 6), z = rng.Range(2, D - 6), len = rng.Range(2, 5);
                bool alongX = rng.Value() < 0.5f;
                for (int i = 0; i < len; i++)
                    Wall(alongX ? x + i : x, alongX ? z : z + i, alongX ? x + i + 1 : x, alongX ? z : z + i + 1, EdgeKind.LowWall, null, out _);
            }

            /// One maze walker: a wall that wanders, turns, makes a room with one gap, or spirals.
            void Walker(RectInt area, float density, float roomChance, float spiralChance, float wiggle, int maxLength)
            {
                int x = rng.Range(area.xMin + 1, area.xMax), z = rng.Range(area.yMin + 1, area.yMax);
                if (x >= D || z >= D || owner[x, z] >= 0 || reserved[x, z]) return;
                owner[x, z] = NewGroup(false);
                float roll = rng.Value();
                bool room = roll < roomChance, spiral = !room && roll < roomChance + spiralChance;
                int dir = rng.Range(0, 4), turn = rng.Value() < 0.5f ? 1 : 3;
                int legA = rng.Range(2, 7), legB = rng.Range(2, 6);
                float turnChance = rng.Value() < 0.5f ? wiggle * 0.4f : wiggle;
                int length = room ? 2 * (legA + legB) : spiral ? rng.Range(8, 15)
                    : rng.Range(4, Mathf.Max(5, Mathf.RoundToInt(maxLength * (0.5f + density))));
                int legLeft = room ? legA : 1, leg = 0;
                for (int step = 0; step < length; step++)
                {
                    if (room || spiral)
                    {
                        if (legLeft == 0)
                        {
                            dir = (dir + turn) % 4;
                            leg++;
                            legLeft = room ? (leg % 2 == 0 ? legA : legB) : leg / 2 + 1;
                        }
                        legLeft--;
                    }
                    else if (rng.Value() < turnChance) dir = (dir + (rng.Value() < 0.5f ? 1 : 3)) % 4;
                    int nx = x + Dx[dir], nz = z + Dz[dir];
                    if (nx < 0 || nz < 0 || nx > D || nz > D) return;
                    if (!Wall(x, z, nx, nz, EdgeKind.Wall, null, out bool joined) || joined) return;
                    x = nx;
                    z = nz;
                }
            }

            /// The Manila Room: beige walls, floorboards, an octagonal table, two chairs, a door on every wall.
            /// Three are open doorways; one is a wooden door to Level 1.
            void ManilaRoom()
            {
                var r = new RectInt(rng.Range(2, D - 6), rng.Range(2, D - 6), 4, 4);
                for (int x = r.xMin - 1; x <= r.xMax; x++)
                for (int z = r.yMin - 1; z <= r.yMax; z++)
                    if (owner[x, z] >= 0 || reserved[x, z]) return;
                int id = NewZone(ZoneKind.Manila, r);
                Paint(r, id);
                int exitSide = rng.Range(0, 4);
                var doorKeys = new int[4];
                for (int side = 0; side < 4; side++)
                {
                    int i = 1 + rng.Range(0, 2);
                    int x0 = side < 2 ? r.xMin + i : side == 2 ? r.xMin : r.xMax;
                    int z0 = side < 2 ? (side == 0 ? r.yMin : r.yMax) : r.yMin + i;
                    int x1 = side < 2 ? x0 + 1 : x0, z1 = side < 2 ? z0 : z0 + 1;
                    doorKeys[side] = Key(x0, z0, x1, z1);
                    if (side != exitSide) Opening(x0, z0, x1, z1, EdgeKind.Doorway);
                }
                RoomWalls(r, 0, EdgeKind.Doorway);
                Reserve(new RectInt(r.xMin + 1, r.yMin + 1, r.width - 2, r.height - 2));
                Use(r);
                if (edges.TryGetValue(doorKeys[exitSide], out var e) && e.Kind == EdgeKind.Wall && WallFrame(doorKeys[exitSide], exitSide == 0 || exitSide == 2, out var pose, out _))
                    AddExit(ExitKind.Door, "1", pose);
                var centre = origin + new Vector3((r.xMin + 2) * C, 0f, (r.yMin + 2) * C);
                props.Add(new PropSpec { Kind = PropKind.OctagonTable, Pose = Matrix4x4.Translate(centre) });
                props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(centre + new Vector3(0, 0, -0.9f), Quaternion.identity, Vector3.one) });
                props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(centre + new Vector3(0, 0, 0.9f), Quaternion.Euler(0, 180, 0), Vector3.one) });
            }

            void Hazards()
            {
                // Pit fields in open spaces.
                if ((Kind == DistrictKind.OpenExpanse || Kind == DistrictKind.PillarHall) && rng.Value() < ds.pitFieldChance * 3f)
                {
                    var r = FeatureRect();
                    bool free = true;
                    for (int x = r.xMin; x <= r.xMax && free; x++)
                    for (int z = r.yMin; z <= r.yMax && free; z++)
                        free = owner[x, z] < 0 && (x >= r.xMax || z >= r.yMax || !used[x, z]);
                    if (free) { Reserve(r); PitField(r); }
                }
                // A flooded room. Zone-water levels own their surfaces: overwriting one here either drowns a
                // dry shelf or drops the level-wide waterline a metre below where the neighbouring district has it.
                float floodRoll = rng.Value();
                if (!theme.zoneWater && floodRoll < ds.floodChance * 4f)
                {
                    var z = zones[rng.Range(0, zones.Count)];
                    if (z.Kind != ZoneKind.Manila && z.Kind != ZoneKind.Origin) z.Water = Range(0.06f, 0.16f);
                }
                // A Red Rooms patch.
                float redRoll = rng.Value();
                if (!theme.zoneWater && redRoll < ms.redChance * 4f)
                {
                    var z = zones[rng.Range(0, zones.Count)];
                    if (z.Kind != ZoneKind.Manila && z.Kind != ZoneKind.Origin)
                    {
                        z.Red = true;
                        z.Wall = new SurfaceRef(Surface.RedWall);
                        z.Floor = new SurfaceRef(Surface.RedFloor);
                    }
                }
            }

            /// A heap of abandoned furniture piled in the middle of a big room, and chairs left around it.
            void FurnitureHeap()
            {
                bool roomy = Kind == DistrictKind.OpenExpanse || Kind == DistrictKind.PillarHall || Kind == DistrictKind.Maze || Kind == DistrictKind.Atrium;
                if (!roomy || rng.Value() >= ds.furnitureHeapChance) return;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int cx = rng.Range(3, D - 4), cz = rng.Range(3, D - 4);
                    bool free = true;
                    for (int x = cx - 1; x <= cx + 2 && free; x++)
                    for (int z = cz - 1; z <= cz + 2 && free; z++)
                        free = owner[x, z] < 0 && !reserved[x, z] && (x > cx + 1 || z > cz + 1 || (OpenCell(x, z) && !used[x, z]));
                    if (!free) continue;
                    Use(new RectInt(cx - 1, cz - 1, 3, 3));
                    var centre = origin + new Vector3((cx + 0.5f) * C, 0f, (cz + 0.5f) * C);
                    var pieces = new[] { PropKind.Sofa, PropKind.DiningTable, PropKind.Armchair, PropKind.Bookshelf, PropKind.CardboardBox,
                                         PropKind.Desk, PropKind.WoodChair, PropKind.FilingCabinet, PropKind.OfficeChair, PropKind.FloorLamp, PropKind.Nightstand };
                    float y = 0f;
                    for (int i = 0, n = rng.Range(10, 19); i < n; i++)
                    {
                        float radius = Mathf.Lerp(1.3f, 0.3f, (float)i / n);
                        var at = centre + new Vector3(Range(-radius, radius), y, Range(-radius, radius));
                        var tilt = i < 4 ? Quaternion.Euler(0f, rng.Value() * 360f, 0f)
                                         : Quaternion.Euler(Range(-70f, 70f), rng.Value() * 360f, Range(-70f, 70f));
                        props.Add(new PropSpec { Kind = pieces[rng.Range(0, pieces.Length)], Pose = Matrix4x4.TRS(at, tilt, Vector3.one), Variant = rng.Range(0, 12) });
                        if (i >= 3) y += Range(0.15f, 0.35f);
                    }
                    blockers.Add(new Bounds(centre + Vector3.up, new Vector3(3.4f, 2f, 3.4f)));
                    for (int i = rng.Range(0, 7); i > 0; i--)
                    {
                        float angle = rng.Value() * Mathf.PI * 2f, dist = Range(3f, 6f);
                        var at = centre + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                        if (FreeAt(at)) PlaceFree(PropKind.WoodChair, at, rng.Value() * 360f, false);
                    }
                    return;
                }
            }

            // =====================================================================================
            // Exits, props, wrongness, curiosity slots
            // =====================================================================================

            void PlaceExits()
            {
                if (ms.exits.Count == 0) return;
                for (int q = 0; q < 4; q++)
                {
                    float exitChance = theme.id == "0" ? Mathf.Max(ms.exitChance, 0.32f) : ms.exitChance;
                    if (rng.Value() >= exitChance) continue;
                    var link = PickWeighted(ms.exits, l => l.weight);
                    var quad = new RectInt((q & 1) * ChunkCells, (q >> 1) * ChunkCells, ChunkCells, ChunkCells);
                    var walls = new List<int>();
                    foreach (var kv in edges)
                    {
                        if (kv.Value.Kind != EdgeKind.Wall) continue;
                        Decode(kv.Key, out int x, out int z, out _);
                        if (quad.Contains(new Vector2Int(x, z))) walls.Add(kv.Key);
                    }
                    bool placed = false;
                    for (int attempt = 0; attempt < 10 && walls.Count > 0 && !placed; attempt++)
                    {
                        int key = walls[rng.Range(0, walls.Count)];
                        if (!WallFrame(key, rng.Value() < 0.5f, out var pose, out var cell) || !quad.Contains(cell) || used[cell.x, cell.y]
                            || zones[zone[cell.x, cell.y]].Kind == ZoneKind.Manila) continue;
                        used[cell.x, cell.y] = true;
                        if (link.kind == ExitKind.FlickerWall)
                        {
                            edges[key] = new Edge(EdgeKind.Flicker, false);   // no longer blocks; the exit renders it
                            exits.Add(new ExitSpec { Kind = ExitKind.FlickerWall, Target = link.target, Pose = pose, Trigger = WallBounds(key) });
                        }
                        else AddExit(link.kind, link.target, pose);
                        placed = true;
                    }
                    if (!placed) BlockExit(quad, link);
                }
            }

            /// No walls nearby: the exit gets its own free-standing stairwell block.
            void BlockExit(RectInt quad, ExitLink link)
            {
                var kind = link.kind == ExitKind.FlickerWall ? ExitKind.Door : link.kind;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int x = rng.Range(quad.xMin + 2, quad.xMax - 2), z = rng.Range(quad.yMin + 2, quad.yMax - 2);
                    int face = rng.Range(0, 4);
                    int fx = x + Dx[face], fz = z + Dz[face];
                    if (!OpenCell(fx, fz) || used[fx, fz] || !Solid(new RectInt(x, z, 1, 1))) continue;
                    used[fx, fz] = true;
                    var normal = new Vector3(Dx[face], 0f, Dz[face]);
                    var at = CellCenter(x, z) + normal * (C / 2 + T / 2);
                    AddExit(kind, link.target, Matrix4x4.TRS(at, Quaternion.LookRotation(normal), Vector3.one));
                    return;
                }
            }

            void AddExit(ExitKind kind, string target, Matrix4x4 pose)
            {
                var trigger = LocalBox(pose, new Vector3(0f, 1f, 0.25f), new Vector3(0.9f, 2f, 0.5f));
                exits.Add(new ExitSpec { Kind = kind, Target = target, Pose = pose, Trigger = trigger });
                if (ds.markedExits && kind == ExitKind.Door)   // the lamp over a way out really shines
                {
                    var lamp = pose.MultiplyPoint3x4(new Vector3(0f, 2.62f, 0.22f));
                    panels.Add(new PanelSpec { Position = lamp, State = FixtureState.Lit, Emitter = true, Bare = true, Range = 5f,
                        Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(lamp.x * 2f), Mathf.RoundToInt(lamp.z * 2f), 0xE1A) });
                }
            }

            void SprinkleProps()
            {
                if (ms.props.Count == 0 || ms.propDensity <= 0f) return;
                for (int x = 0; x < D; x++)
                for (int z = 0; z < D; z++)
                {
                    if (!OpenCell(x, z) || used[x, z] || rng.Value() >= ms.propDensity) continue;
                    var k = zones[zone[x, z]].Kind;
                    if (k != ZoneKind.Open && k != ZoneKind.Maze && k != ZoneKind.Hall && k != ZoneKind.Corridor && k != ZoneKind.Room && k != ZoneKind.Atrium) continue;
                    var kind = PickWeighted(ms.props, p => p.weight).kind;
                    int variant = rng.Range(0, 12);
                    Matrix4x4 pose;
                    if (PropLibrary.IsWallProp(kind))
                    {
                        if (!WallOfCell(x, z, out pose)) continue;
                        pose *= Matrix4x4.Translate(new Vector3(rng.Range(-3, 4) * 0.1f, 0f, 0f));
                    }
                    else
                    {
                        var at = new Vector3((x + 0.5f + (rng.Value() - 0.5f) * 0.5f) * C, 0f, (z + 0.5f + (rng.Value() - 0.5f) * 0.5f) * C);
                        pose = Matrix4x4.TRS(origin + at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one);
                    }
                    used[x, z] = true;
                    bool doubled = false;
                    float glitch = ms.glitchChance + drift * 0.4f + (theme.id == "0" && !origin0 ? 0.03f : 0f);
                    if (rng.Value() < glitch) pose = Glitch(pose, kind, out doubled);
                    props.Add(new PropSpec { Kind = kind, Pose = pose, Variant = variant, Ceiling = HeightAt(x, z) });
                    if (doubled)
                        props.Add(new PropSpec { Kind = kind, Pose = pose * Matrix4x4.TRS(new Vector3(0.02f, 0.01f, 0.015f), Quaternion.Euler(0f, 2.5f, 0f), Vector3.one), Variant = variant, Ceiling = HeightAt(x, z) });
                }
            }

            /// Props in the wrong place: the world forgot how to hold them.
            Matrix4x4 Glitch(Matrix4x4 pose, PropKind kind, out bool doubled)
            {
                doubled = false;
                switch (rng.Range(0, 7))
                {
                    case 0: return Matrix4x4.Translate(Vector3.down * Range(0.15f, 0.55f)) * pose;
                    case 1:
                        return Matrix4x4.Translate(Vector3.up * Range(0.25f, 1.1f)) * pose
                               * Matrix4x4.Rotate(Quaternion.Euler(rng.Value() * 360f, rng.Value() * 360f, rng.Value() * 360f));
                    case 2: return pose * Matrix4x4.Scale(new Vector3(1f, Range(1.4f, 2.8f), 1f));
                    case 3:
                        return PropLibrary.IsWallProp(kind)
                            ? pose * Matrix4x4.Translate(new Vector3(0f, Range(-0.25f, 0.35f), -Range(0.15f, 0.45f)))
                            : pose * Matrix4x4.TRS(new Vector3(0f, 0.18f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                    case 4:
                        return pose * Matrix4x4.TRS(new Vector3(Range(-0.12f, 0.12f), 0f, Range(-0.12f, 0.12f)), Quaternion.Euler(0f, Range(-35f, 35f), 0f), new Vector3(Range(0.9f, 1.1f), Range(0.85f, 1.2f), Range(0.9f, 1.1f)));
                    case 5:
                        return PropLibrary.IsWallProp(kind)
                            ? pose * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, Range(-18f, 18f)))
                            : pose * Matrix4x4.TRS(Vector3.up * Range(0.05f, 0.22f), Quaternion.Euler(Range(-12f, 12f), Range(-180f, 180f), Range(-12f, 12f)), Vector3.one);
                    default:
                        doubled = true;
                        return pose;
                }
            }

            /// Subtle wrongness: outlets too high, a window onto more wall, doors too tall or too small, and things placed where they should not be.
            void Wrongness()
            {
                // Level 37 is uncanny through geometry and water, not outlets, fake windows or doors to nowhere.
                if (theme.zoneWater) return;
                bool level0 = theme.id == "0";
                // Noticeable and more frequent in places, but still rare overall; copy drift brings more.
                int baseCount = (rng.Value() < (level0 ? 0.5f : 0.22f) ? 1 : 0)
                    + (level0 && rng.Value() < 0.18f ? 1 : 0)
                    + Mathf.FloorToInt(drift * rng.Range(level0 ? 4 : 2, level0 ? 9 : 7));
                for (int i = baseCount; i > 0; i--)
                {
                    int x = rng.Range(1, D - 1), z = rng.Range(1, D - 1);
                    if (!OpenCell(x, z) || used[x, z]) continue;
                    var zz = zones[zone[x, z]];
                    if (zz.Kind == ZoneKind.Manila || zz.Kind == ZoneKind.Origin) continue;

                    float r = rng.Value();
                    // Wall-bound wrongness.
                    if (r < 0.52f)
                    {
                        if (!WallOfCell(x, z, out var pose)) continue;
                        used[x, z] = true;
                        float w = rng.Value();
                        if (w < 0.22f) props.Add(new PropSpec { Kind = PropKind.OddOutlet, Pose = pose, Variant = rng.Range(0, 12) });
                        else if (w < 0.36f) props.Add(new PropSpec { Kind = PropKind.WindowToWall, Pose = pose });
                        else if (w < 0.48f) props.Add(new PropSpec { Kind = PropKind.DoorToNowhere, Pose = pose * Matrix4x4.Scale(new Vector3(1f, 1.15f, 1f)) });
                        else if (w < 0.58f) props.Add(new PropSpec { Kind = PropKind.DoorToNowhere, Pose = pose * Matrix4x4.Scale(new Vector3(0.45f, 0.45f, 1f)) });
                        else if (w < 0.68f) props.Add(new PropSpec { Kind = PropKind.Outlet, Pose = pose * Matrix4x4.Translate(new Vector3(Range(-0.25f, 0.25f), Range(1.1f, 2.05f), 0f)), Variant = rng.Range(0, 12) });
                        else if (w < 0.78f) props.Add(new PropSpec { Kind = PropKind.LightSwitch, Pose = pose * Matrix4x4.Translate(new Vector3(Range(-0.35f, 0.35f), Range(0.4f, 1.7f), 0f)), Variant = rng.Range(0, 12) });
                        else if (w < 0.88f)
                        {
                            props.Add(new PropSpec { Kind = PropKind.LoneDoor, Pose = pose });
                            props.Add(new PropSpec { Kind = PropKind.LightSwitch, Pose = pose * Matrix4x4.Translate(new Vector3(0.55f, Range(0.2f, 1.4f), 0f)), Variant = rng.Range(0, 12) });
                        }
                        else
                        {
                            // Repeated fixtures or an opening detail that feels off.
                            for (int n = 0; n < 2 + rng.Range(0, 2); n++)
                                props.Add(new PropSpec { Kind = PropKind.Outlet, Pose = pose * Matrix4x4.Translate(new Vector3(Range(-0.4f, 0.4f), 0.35f + n * 0.55f + Range(-0.05f, 0.05f), 0f)), Variant = rng.Range(0, 12) });
                        }
                        continue;
                    }

                    // Floor-bound wrongness: objects in the wrong spots.
                    if (!level0 && r < 0.78f) continue;
                    var at = CellCenter(x, z) + new Vector3(Range(-0.35f, 0.35f), 0f, Range(-0.35f, 0.35f));
                    float h = HeightAt(x, z);
                    used[x, z] = true;
                    float f = rng.Value();
                    if (f < 0.14f)
                    {
                        props.Add(new PropSpec { Kind = PropKind.CrtTv, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.Phone, Pose = Matrix4x4.TRS(at + new Vector3(Range(-0.4f, 0.4f), 0f, Range(-0.4f, 0.4f)), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else if (f < 0.26f)
                    {
                        for (int n = 0; n < 2 + rng.Range(0, 4); n++)
                            props.Add(new PropSpec { Kind = PropKind.Phone, Pose = Matrix4x4.TRS(at + new Vector3(Range(-0.7f, 0.7f), 0f, Range(-0.7f, 0.7f)), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else if (f < 0.38f)
                    {
                        props.Add(new PropSpec { Kind = PropKind.CrtTv, Pose = Matrix4x4.TRS(at + new Vector3(-0.3f, 0f, 0f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.CrtTv, Pose = Matrix4x4.TRS(at + new Vector3(0.45f, 0f, 0.25f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        if (rng.Value() < 0.5f) props.Add(new PropSpec { Kind = PropKind.Phone, Pose = Matrix4x4.TRS(at + new Vector3(0f, 0f, -0.5f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else if (f < 0.5f)
                    {
                        props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.OfficeChair, Pose = Matrix4x4.TRS(at + new Vector3(0.05f, 0.42f, 0.05f), Quaternion.Euler(180f, rng.Value() * 360f, 12f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        if (rng.Value() < 0.45f) props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(at + new Vector3(-0.55f, 0f, 0.35f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else if (f < 0.62f)
                    {
                        for (int n = 0; n < 5 + rng.Range(0, 5); n++)
                            props.Add(new PropSpec { Kind = PropKind.CardboardBox, Pose = Matrix4x4.TRS(at + new Vector3(Range(-0.8f, 0.8f), 0.01f + (n % 2) * 0.005f, Range(-0.8f, 0.8f)), Quaternion.Euler(0f, rng.Value() * 360f, 0f), new Vector3(Range(0.45f, 1.05f), 0.03f, Range(0.4f, 0.95f))), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else if (f < 0.72f)
                    {
                        props.Add(new PropSpec { Kind = PropKind.CardboardBox, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.Bucket, Pose = Matrix4x4.TRS(at + new Vector3(0.45f, 0f, -0.25f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.Can, Pose = Matrix4x4.TRS(at + new Vector3(-0.35f, 0f, 0.4f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        if (rng.Value() < 0.5f) props.Add(new PropSpec { Kind = PropKind.Battery, Pose = Matrix4x4.TRS(at + new Vector3(0.1f, 0f, 0.55f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else if (f < 0.82f)
                    {
                        props.Add(new PropSpec { Kind = PropKind.DoorToNowhere, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.TrashCan, Pose = Matrix4x4.TRS(at + new Vector3(0.5f, 0f, 0.2f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        if (rng.Value() < 0.4f) props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(at + new Vector3(-0.45f, 0f, -0.35f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else if (f < 0.91f)
                    {
                        props.Add(new PropSpec { Kind = PropKind.Desk, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.FilingCabinet, Pose = Matrix4x4.TRS(at + new Vector3(0.9f, 0f, -0.1f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        if (rng.Value() < 0.5f) props.Add(new PropSpec { Kind = PropKind.CardboardBox, Pose = Matrix4x4.TRS(at + new Vector3(-0.7f, 0f, 0.5f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                    else
                    {
                        props.Add(new PropSpec { Kind = PropKind.Bucket, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        props.Add(new PropSpec { Kind = PropKind.TilePile, Pose = Matrix4x4.TRS(at + new Vector3(Range(-0.4f, 0.4f), 0f, Range(-0.4f, 0.4f)), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                        if (rng.Value() < 0.35f) props.Add(new PropSpec { Kind = PropKind.Ladder, Pose = Matrix4x4.TRS(at + new Vector3(0.35f, 0f, -0.35f), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Variant = rng.Range(0, 12), Ceiling = h });
                    }
                }
            }

            void PlaceSlots()
            {
                // Curiosities would break the clean, entity-free wiki presentation of Level 37.
                if (theme.zoneWater) return;
                float expected = ds.curiosities;
                // Extra uncanny clutter in Level 0: more noticeable and frequent in places, but still rare overall.
                if (theme.id == "0") expected += 0.28f + drift * 0.35f;
                int count = Mathf.FloorToInt(expected) + (rng.Value() < expected - Mathf.FloorToInt(expected) ? 1 : 0);
                if (theme.id == "0" && rng.Value() < 0.18f) count++; // occasional extra odd pocket
                for (int i = 0; i < count; i++)
                    for (int attempt = 0; attempt < 18; attempt++)
                    {
                        int x = rng.Range(2, D - 2), z = rng.Range(2, D - 2);
                        if (!OpenCell(x, z) || used[x, z]) continue;
                        var zz = zones[zone[x, z]];
                        // Keep the cleanest presentation spaces calmer.
                        if (zz.Kind == ZoneKind.Manila || zz.Kind == ZoneKind.Origin) continue;
                        Matrix4x4 pose = default;
                        float wallChance = zz.Kind == ZoneKind.Corridor || zz.Kind == ZoneKind.Maze ? 0.46f : 0.32f;
                        bool wall = rng.Value() < wallChance && WallOfCell(x, z, out pose);
                        if (!wall)
                        {
                            var at = CellCenter(x, z) + new Vector3(Range(-0.4f, 0.4f), 0f, Range(-0.4f, 0.4f));
                            pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one);
                        }
                        used[x, z] = true;
                        slots.Add(new CuriositySlot
                        {
                            Id = (int)WorldHash.Hash(seed, dx * D + x, dz * D + z, 0x51),
                            Pose = pose,
                            OnWall = wall,
                            BigRoom = zz.Rect.width * zz.Rect.height >= 64,
                            Ceiling = HeightAt(x, z),
                        });
                        break;
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
                        0 => Key(x, z, x + 1, z),
                        1 => Key(x, z + 1, x + 1, z + 1),
                        2 => Key(x, z, x, z + 1),
                        _ => Key(x + 1, z, x + 1, z + 1),
                    };
                    if (!edges.TryGetValue(key, out var e) || (e.Kind != EdgeKind.Wall && e.Kind != EdgeKind.Run)) continue;
                    if (WallFrame(key, side == 0 || side == 2, out pose, out _)) return true;
                }
                pose = default;
                return false;
            }

            /// Frame on one face of a wall edge: origin on the floor at the wall face, local +z away from the wall.
            bool WallFrame(int key, bool positive, out Matrix4x4 pose, out Vector2Int cell)
            {
                Decode(key, out int x, out int z, out int dir);
                float half = edges.TryGetValue(key, out var e) && e.Thick ? ThickWall / 2 : T / 2;
                Vector3 normal, at;
                float yaw;
                if (dir == 0)
                {
                    cell = new Vector2Int(x, positive ? z : z - 1);
                    normal = positive ? Vector3.forward : Vector3.back;
                    yaw = positive ? 0f : 180f;
                    at = new Vector3((x + 0.5f) * C, 0f, z * C);
                }
                else
                {
                    cell = new Vector2Int(positive ? x : x - 1, z);
                    normal = positive ? Vector3.right : Vector3.left;
                    yaw = positive ? 90f : 270f;
                    at = new Vector3(x * C, 0f, (z + 0.5f) * C);
                }
                pose = Matrix4x4.TRS(origin + at + normal * half, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                return OpenCell(cell.x, cell.y);
            }

            // =====================================================================================
            // Furnishing real places
            // =====================================================================================

            void Furnish(int id)
            {
                var z = zones[id];
                // Most rooms were emptied long ago; stores and showrooms keep their fixtures more often.
                bool store = z.Kind == ZoneKind.Vignette || z.Kind == ZoneKind.StoreUnit;
                if (rng.Value() < (store ? 0.4f : 0.75f)) return;
                switch (z.Kind)
                {
                    case ZoneKind.Office:
                        for (int i = rng.Range(1, 4); i > 0; i--) { PlaceOnWall(id, PropKind.Desk); PlaceInZone(id, PropKind.OfficeChair, false); }
                        if (rng.Value() < 0.6f) PlaceOnWall(id, PropKind.FilingCabinet);
                        if (rng.Value() < 0.3f) PlaceOnWall(id, PropKind.Whiteboard);
                        break;
                    case ZoneKind.BreakRoom:
                        PlaceOnWall(id, PropKind.BreakCounter);
                        PlaceOnWall(id, PropKind.Fridge);
                        PlaceInZone(id, PropKind.DiningTable, true);
                        for (int i = rng.Range(1, 4); i > 0; i--) PlaceInZone(id, PropKind.WoodChair, false);
                        break;
                    case ZoneKind.CopyRoom:
                        PlaceInZone(id, PropKind.Copier, true);
                        if (rng.Value() < 0.6f) PlaceOnWall(id, PropKind.Bookshelf);
                        if (rng.Value() < 0.5f) PlaceInZone(id, PropKind.CardboardBox, false);
                        break;
                    case ZoneKind.Conference:
                        PlaceInZone(id, PropKind.ConferenceTable, true);
                        for (int i = rng.Range(3, 8); i > 0; i--) PlaceInZone(id, PropKind.OfficeChair, false);
                        PlaceOnWall(id, PropKind.Whiteboard);
                        break;
                    case ZoneKind.Reception:
                        PlaceInZone(id, PropKind.Counter, true);
                        for (int i = rng.Range(2, 5); i > 0; i--) PlaceInZone(id, PropKind.Armchair, true);
                        break;
                    case ZoneKind.Cubicles:
                        Cubicles(id);
                        break;
                    case ZoneKind.Closet:
                        if (rng.Value() < 0.5f) PlaceOnWall(id, PropKind.Bookshelf);
                        if (rng.Value() < 0.5f) PlaceInZone(id, rng.Value() < 0.5f ? PropKind.Bucket : PropKind.CardboardBox, false);
                        break;
                    case ZoneKind.HotelRoom:
                        PlaceInZone(id, PropKind.Bed, true);
                        PlaceInZone(id, PropKind.Nightstand, true);
                        if (rng.Value() < 0.5f) PlaceInZone(id, PropKind.FloorLamp, false);
                        break;
                    case ZoneKind.Vignette:
                        Vignette(id);
                        break;
                    case ZoneKind.StoreUnit:
                    {
                        float r = rng.Value();
                        if (r < 0.25f) for (int i = rng.Range(2, 6); i > 0; i--) PlaceInZone(id, PropKind.ClothingRack, true);
                        else if (r < 0.45f) for (int i = rng.Range(2, 5); i > 0; i--) PlaceInZone(id, PropKind.Gondola, true);
                        else if (r < 0.6f) Vignette(id);
                        else if (r < 0.8f) PlaceInZone(id, PropKind.Counter, true);
                        break;   // the rest stand empty
                    }
                    case ZoneKind.Room:
                        if (rng.Value() < 0.3f) PlaceInZone(id, PropKind.OfficeChair, false);
                        break;
                }
            }

            void Vignette(int id)
            {
                float r = rng.Value();
                if (r < 0.35f)   // living room
                {
                    PlaceInZone(id, PropKind.Rug, false);
                    PlaceOnWall(id, PropKind.Bookshelf);
                    PlaceInZone(id, PropKind.Sofa, true);
                    PlaceInZone(id, PropKind.Armchair, true);
                    PlaceInZone(id, PropKind.CoffeeTable, true);
                    PlaceInZone(id, PropKind.FloorLamp, false);
                }
                else if (r < 0.65f)   // bedroom
                {
                    PlaceInZone(id, PropKind.Rug, false);
                    PlaceInZone(id, PropKind.Bed, true);
                    PlaceInZone(id, PropKind.Nightstand, true);
                    PlaceInZone(id, PropKind.FloorLamp, false);
                }
                else if (r < 0.85f)   // dining
                {
                    PlaceInZone(id, PropKind.DiningTable, true);
                    for (int i = 0; i < 4; i++) PlaceInZone(id, PropKind.WoodChair, false);
                }
                else   // study
                {
                    PlaceOnWall(id, PropKind.Desk);
                    PlaceInZone(id, PropKind.OfficeChair, false);
                    PlaceOnWall(id, PropKind.Bookshelf);
                }
                PlaceInZone(id, PropKind.PriceTag, false);
            }

            /// Low partition walls cut the room into cubicles, each with a desk and a chair.
            void Cubicles(int id)
            {
                var r = zones[id].Rect;
                for (int z = r.yMin + 2; z < r.yMax - 1; z += 2)
                    for (int x = r.xMin + 1; x < r.xMax - 1; x++)
                    {
                        Wall(x, z, x + 1, z, EdgeKind.LowWall, null, out _);
                        if ((x - r.xMin) % 2 == 1) Wall(x, z, x, z + 1, EdgeKind.LowWall, null, out _);
                    }
                for (int i = rng.Range(3, 8); i > 0; i--)
                {
                    PlaceInZone(id, PropKind.Desk, false);
                    PlaceInZone(id, PropKind.OfficeChair, false);
                }
            }

            /// A prop on a random free spot in the zone. `block` keeps it in the reachability check.
            void PlaceInZone(int id, PropKind kind, bool block)
            {
                var r = zones[id].Rect;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int x = rng.Range(r.xMin, r.xMax), z = rng.Range(r.yMin, r.yMax);
                    if (x < 0 || z < 0 || x >= D || z >= D || zone[x, z] != id || used[x, z]) continue;
                    var at = CellCenter(x, z) + new Vector3(Range(-0.3f, 0.3f), 0f, Range(-0.3f, 0.3f));
                    float yaw = rng.Range(0, 4) * 90f + (kind == PropKind.OfficeChair || kind == PropKind.WoodChair ? Range(-25f, 25f) : 0f);
                    used[x, z] = true;
                    PlaceFree(kind, at, yaw, block);
                    return;
                }
            }

            void PlaceOnWall(int id, PropKind kind)
            {
                var r = zones[id].Rect;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int x = rng.Range(r.xMin, r.xMax), z = rng.Range(r.yMin, r.yMax);
                    if (x < 0 || z < 0 || x >= D || z >= D || zone[x, z] != id || used[x, z] || !WallOfCell(x, z, out var pose)) continue;
                    used[x, z] = true;
                    AddProp(kind, pose, x, z);
                    return;
                }
            }

            void PlaceFree(PropKind kind, Vector3 worldAt, float yaw, bool block)
            {
                var pose = Matrix4x4.TRS(worldAt, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                var cell = CellOf(worldAt);
                AddProp(kind, pose, cell.x, cell.y);
                if (!block) return;
                var f = PropLibrary.Footprint(kind);
                bool turned = Mathf.Abs(Mathf.Repeat(yaw, 180f) - 90f) < 45f;
                blockers.Add(new Bounds(worldAt + Vector3.up, new Vector3(turned ? f.y : f.x, 2f, turned ? f.x : f.y)));
                placed.Add((props.Count - 1, blockers.Count - 1));
            }

            /// The walkable volume through an opening, reaching 0.8 m into the rooms on both sides.
            public Bounds PassageBounds(int key)
            {
                Decode(key, out int x, out int z, out int dir);
                var mid = origin + (dir == 0 ? new Vector3((x + 0.5f) * C, 1f, z * C) : new Vector3(x * C, 1f, (z + 0.5f) * C));
                return new Bounds(mid, dir == 0 ? new Vector3(1.2f, 2f, 1.6f + T) : new Vector3(1.6f + T, 2f, 1.2f));
            }

            static bool IsOpening(EdgeKind k) => k == EdgeKind.Doorway || k == EdgeKind.Arch || k == EdgeKind.Wide;

            /// Nothing large stands in front of a doorway. Walls are final by now (later rooms can turn an earlier
            /// room's wall into a doorway), so this runs after everything is furnished.
            void ClearPassages()
            {
                foreach (int k in forcedOpen) edges.Remove(k);   // ramp mouths and ladder ways in, whatever was built across them
                var passages = new List<Bounds>();
                foreach (var kv in edges) if (IsOpening(kv.Value.Kind)) passages.Add(PassageBounds(kv.Key));
                foreach (var (prop, blocker) in placed)
                    foreach (var p in passages)
                        if (p.Intersects(blockers[blocker]))
                        {
                            removedProps.Add(prop);
                            removedBlockers.Add(blocker);
                            break;
                        }
            }

            void AddProp(PropKind kind, Matrix4x4 pose, int x, int z) =>
                props.Add(new PropSpec { Kind = kind, Pose = pose, Variant = rng.Range(0, 12), Ceiling = HeightAt(Mathf.Clamp(x, 0, D - 1), Mathf.Clamp(z, 0, D - 1)) });

            Vector2Int CellOf(Vector3 world) =>
                new Vector2Int(Mathf.FloorToInt((world.x - origin.x) / C), Mathf.FloorToInt((world.z - origin.z) / C));

            bool FreeAt(Vector3 world)
            {
                var c = CellOf(world);
                return OpenCell(c.x, c.y) && !used[c.x, c.y];
            }

            Rect ToWorld(RectInt r, float inset) =>
                new Rect(origin.x + r.xMin * C + inset, origin.z + r.yMin * C + inset, r.width * C - 2 * inset, r.height * C - 2 * inset);

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

            // =====================================================================================
            // Lights: every room has its own layout, snapped to the 0.5 m ceiling-tile grid
            // =====================================================================================

            void LightZone(int id)
            {
                var z = zones[id];
                if (z.Rect.width <= 0 || z.Rect.height <= 0 || z.Shaft || ds.rafters) return;   // the attic has no ceiling panels, only the glow
                float sx, sz;
                int emitEvery = 1;
                bool continuous = false;
                switch (z.Lights)
                {
                    case LightLayout.Dense: sx = 2f; sz = 2f; emitEvery = 2; break;
                    case LightLayout.Sparse: sx = 6f; sz = 6f; break;
                    case LightLayout.Rows: sx = 1f; sz = 4f; emitEvery = 3; continuous = true; break;
                    case LightLayout.DoubleRows: sx = 1f; sz = 5f; emitEvery = 3; continuous = true; break;
                    case LightLayout.CenterStrip: sx = 2f; sz = 1000f; emitEvery = 2; break;
                    default: sx = 4f; sz = 4f; break;
                }
                bool alongX = z.LightsAlongX;
                var area = ToWorld(z.Rect, 0.6f);
                float u0 = alongX ? area.xMin : area.yMin, u1 = alongX ? area.xMax : area.yMax;   // along the rows
                float v0 = alongX ? area.yMin : area.xMin, v1 = alongX ? area.yMax : area.xMax;
                float vMid = (v0 + v1) / 2f;
                int row = 0;
                var rows = new List<float>();
                if (z.Lights == LightLayout.CenterStrip) rows.Add(vMid);
                else
                {
                    // World-aligned rows: the same lines in every zone and district, so rows run on unbroken.
                    for (float v = (Mathf.Floor(v0 / sz) + 0.5f) * sz; v <= v1; v += sz)
                    {
                        if (v < v0) continue;
                        rows.Add(v);
                        if (z.Lights == LightLayout.DoubleRows) rows.Add(v + 1f);
                    }
                    if (rows.Count == 0) rows.Add(vMid);
                }
                int firstPanel = panels.Count;
                foreach (float v in rows)
                {
                    int count = 0;
                    float offset = z.Lights == LightLayout.Staggered && row % 2 == 1 ? sx / 2f : 0f;
                    float start = (Mathf.Floor(u0 / sx) + 0.5f) * sx + offset;
                    if (start - sx >= u0) start -= sx;
                    int n = Mathf.Max(1, Mathf.FloorToInt((u1 - start) / sx) + 2);
                    for (int i = 0; i < n; i++, count++)
                    {
                        float u = start + i * sx;
                        if (u < u0 || u > u1 || v < v0 - 0.01f || v > v1 + 0.01f) continue;
                        // Panels are 0.5 x 1 m on 0.5 m tiles: the long side spans two tiles.
                        float px = alongX ? Mathf.Round(u * 2f) / 2f : Mathf.Floor(v * 2f) / 2f + 0.25f;
                        float pz = alongX ? Mathf.Floor(v * 2f) / 2f + 0.25f : Mathf.Round(u * 2f) / 2f;
                        var cell = CellOf(new Vector3(px, 0f, pz));
                        if (cell.x < 0 || cell.y < 0 || cell.x >= D || cell.y >= D || zone[cell.x, cell.y] != id || noLights.Contains(cell)) continue;
                        bool pillar = false;
                        foreach (var p in pillars) if (p.zone == id && Mathf.Abs(p.box.center.x - px) < 0.8f && Mathf.Abs(p.box.center.z - pz) < 0.8f) { pillar = true; break; }
                        if (pillar) continue;
                        float h = HeightAt(cell.x, cell.y);
                        float roll = WorldHash.Value(seed, Mathf.RoundToInt(px * 2f), Mathf.RoundToInt(pz * 2f), 97);
                        // Copy drift: panels slip off the tile grid.
                        if (drift > 0.3f && WorldHash.Value(seed, Mathf.RoundToInt(px * 2f), Mathf.RoundToInt(pz * 2f), 96) < drift * 0.5f)
                        {
                            px += (WorldHash.Value(seed, Mathf.RoundToInt(px * 2f), 1, 95) - 0.5f) * 0.6f;
                            pz += (WorldHash.Value(seed, Mathf.RoundToInt(pz * 2f), 2, 95) - 0.5f) * 0.6f;
                        }
                        panels.Add(new PanelSpec
                        {
                            Position = new Vector3(px, h - 0.01f, pz),
                            Sick = z.Sick,
                            AlongX = alongX,
                            State = roll < z.Dead ? FixtureState.Dead : roll < z.Dead + 0.04f ? FixtureState.Faulty : FixtureState.Lit,
                            Emitter = !continuous ? count % emitEvery == 0 : count % emitEvery == 1,
                            Range = Mathf.Max(6f, h * 2.2f),
                            Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(px * 2f), Mathf.RoundToInt(pz * 2f), 98),
                            Red = z.Red,
                        });
                    }
                    row++;
                }
                if (z.Pool && panels.Count > firstPanel)
                {
                    // One light left burning in the dark: the panel nearest the middle of the room.
                    var mid = ToWorld(z.Rect, 0f).center;
                    int best = firstPanel;
                    for (int i = firstPanel; i < panels.Count; i++)
                        if (Vector2.Distance(new Vector2(panels[i].Position.x, panels[i].Position.z), mid) < Vector2.Distance(new Vector2(panels[best].Position.x, panels[best].Position.z), mid)) best = i;
                    var pool = panels[best];
                    pool.State = FixtureState.Lit;
                    pool.Emitter = true;
                    panels[best] = pool;
                }
            }

            Bounds WallBounds(int key)
            {
                var list = new List<SurfaceBox>();
                EmitEdge(list, key, new Edge(EdgeKind.Wall, false), false);
                return list[0].Box;
            }
        }
    }
}
