using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Layout rhythm on top of the 64 m districts:
    /// - Regions: irregular areas of one archetype, from a warped, weighted Voronoi of scattered sites, so areas
    ///   are blobs of very different sizes and the district grid disappears. Rare sites are giant halls.
    /// - Long halls: finite straight corridors (30-80 m) that can cross a district border.
    /// - Copies: now and then a district is built from another district's plan, as if the building copied it.
    /// - Light moods: broad stretches where half the lights are dead, and dark patches with one light left on.
    /// All pure functions of the seed and global position, so neighbours always agree.
    public static partial class DistrictLayout
    {
        const float SiteSpacing = 38f;        // tighter region spacing makes architecture change more often
        const float GiantChance = 0.035f;      // rare cinematic halls emerge from the maze through the fog
        const float GiantWeight = 34f, GiantHeight = 5.8f;
        const int BandCells = 48;             // long halls: one possible hall per 96 m band
        const float HallChance = 0.35f;

        struct Site { public Vector2 Pos; public float Weight; public DistrictKind Kind; public bool Giant; public Mood Mood; }

        static Site SiteAt(uint seed, int i, int j, DistrictStyle s)
        {
            var site = new Site
            {
                Pos = new Vector2((i + 0.15f + 0.7f * WorldHash.Value(seed, i, j, 801)) * SiteSpacing,
                                  (j + 0.15f + 0.7f * WorldHash.Value(seed, i, j, 802)) * SiteSpacing),
                Giant = WorldHash.Value(seed, i, j, 803) < GiantChance,
            };
            site.Kind = site.Giant ? DistrictKind.PillarHall : RawPick(seed, i, j, s, WorldHash.Value(seed, i, j, 804));
            if (s.wikiZones && !site.Giant)
            {
                float m = WorldHash.Value(seed, i, j, 806);
                site.Mood = m < 0.035f ? Mood.Blackout : m < 0.05f ? Mood.Red : Mood.Normal;
            }
            site.Weight = site.Giant ? GiantWeight : WorldHash.Value(seed, i, j, 805) * 16f;
            return site;
        }

        /// Wobbly boundaries: the lookup position is pushed around by smooth noise.
        static Vector2 Warp(uint seed, Vector2 p)
        {
            float o = (seed % 997) * 0.37f;
            return p + new Vector2(Mathf.PerlinNoise(p.x / 26f + o, p.y / 26f) - 0.5f, Mathf.PerlinNoise(p.x / 26f, p.y / 26f + o + 5f) - 0.5f) * 18f;
        }

        static Site RegionAt(uint seed, Vector2 world, DistrictStyle s)
        {
            var p = Warp(seed, world);
            int ci = Mathf.FloorToInt(p.x / SiteSpacing), cj = Mathf.FloorToInt(p.y / SiteSpacing);
            Site best = default;
            float bestD = float.MaxValue;
            for (int i = ci - 2; i <= ci + 2; i++)
            for (int j = cj - 2; j <= cj + 2; j++)
            {
                var site = SiteAt(seed, i, j, s);
                float d = Vector2.Distance(p, site.Pos) - site.Weight;
                if (d < bestD) { bestD = d; best = site; }
            }
            return best;
        }

        /// The archetype at a world position (for the debug readout and the variety check).
        public static DistrictKind KindAtPosition(uint seed, Vector3 p, DistrictStyle s) => RegionAt(seed, new Vector2(p.x, p.z), s).Kind;

        // ---------- long halls ----------

        /// The hall in band `band` along one axis: its cross position (global cell) and segments along it.
        static bool HallInBand(uint seed, bool alongX, int band, out int cross)
        {
            int salt = alongX ? 821 : 831;
            cross = 0;
            if (WorldHash.Value(seed, band, 0, salt) >= HallChance) return false;
            // Two cells wide, never on or next to a district border line.
            int start = band * BandCells;
            for (int tries = 0; tries < 8; tries++)
            {
                cross = start + 2 + (int)(WorldHash.Value(seed, band, tries + 1, salt) * (BandCells - 4));
                int local = ((cross % D) + D) % D;
                if (local >= 2 && local <= D - 4) return true;
            }
            return false;
        }

        /// Hall segments along a band repeat every 480 m: each 30-80 m long, starting at a random spot.
        static bool HallSegment(uint seed, bool alongX, int band, int k, out int from, out int to)
        {
            int salt = alongX ? 841 : 851;
            const int period = 240;
            from = k * period + (int)(WorldHash.Value(seed, band, k, salt) * 80f);
            to = from + 15 + (int)(WorldHash.Value(seed, band, k, salt + 1) * 25f);   // 30-80 m
            return WorldHash.Value(seed, band, k, salt + 2) < 0.6f;
        }

        /// Is global cell `along` on the line covered by a hall crossing it? (Keeps border runs out of halls.)
        /// alongZ: a vertical border line at global x = line, crossed by halls that run along x.
        static bool HallCrosses(uint seed, bool alongZ, int line, int along)
        {
            bool hallAlongX = alongZ;   // a hall running along x crosses vertical lines
            int band = FloorDiv(along, BandCells);
            if (!HallInBand(seed, hallAlongX, band, out int cross) || (along != cross && along != cross + 1)) return false;
            int k = FloorDiv(line, 240);
            for (int kk = k - 1; kk <= k; kk++)
                if (HallSegment(seed, hallAlongX, band, kk, out int a, out int b) && line > a && line < b) return true;
            return false;
        }

        sealed partial class Plan
        {
            /// Open areas (maze, open floor, halls, corridors) take their look from the region, not the district,
            /// so the same place continues unchanged across a district border.
            void RegionLook(Zone z)
            {
                var c = z.Rect.center;
                var site = RegionAt(seed, new Vector2((lx * D + c.x) * C, (lz * D + c.y) * C), ds);
                uint h = WorldHash.Hash(seed, Mathf.RoundToInt(site.Pos.x), Mathf.RoundToInt(site.Pos.y), 870);
                float T(int i) => WorldHash.Value(h, i, 0, 871);
                z.Wall = new SurfaceRef(Surface.Wall, T(0) < 0.5f ? 0 : Mathf.Min(wallpapers - 1, (int)(T(1) * wallpapers)));
                z.Floor = new SurfaceRef(Surface.Floor, T(2) < 0.6f ? 0 : Mathf.Min(carpets - 1, (int)(T(3) * carpets)));
                z.Ceiling = new SurfaceRef(Surface.Ceiling, T(4) < 0.25f ? Mathf.Min(ceilings - 1, (int)(T(5) * ceilings)) : 0);
                z.FloorRot = T(6) < 0.5f;
                z.Baseboard = T(7) < 0.85f;
                z.ChairRail = T(8) < 0.3f;
                float t = T(9);
                switch (z.Kind)
                {
                    case ZoneKind.Corridor: z.Height = Snap(Mathf.Lerp(2.4f, 2.7f, t)); break;
                    case ZoneKind.Hall: z.Height = Snap(Mathf.Lerp(3.2f, 5.6f, t)); break;
                    case ZoneKind.Atrium: z.Height = Snap(Mathf.Lerp(5f, 6.2f, t)); break;
                    default: z.Height = T(10) < 0.65f ? 2.8f : Snap(Mathf.Lerp(2.5f, 3.8f, t)); break;
                }
                float r = T(11);
                if (z.Kind == ZoneKind.Open || z.Kind == ZoneKind.Maze)
                    z.Lights = r < 0.55f ? LightLayout.Grid : r < 0.7f ? LightLayout.Staggered : r < 0.85f ? LightLayout.Sparse : LightLayout.Rows;
                z.LightsAlongX = T(12) < 0.5f;
            }

            bool[,] allow;    // cells the current builder may touch (null = all)
            bool[,] locked;   // cells already given a fixed purpose (long halls): not repainted
            readonly List<(DistrictKind kind, bool giant, bool[,] cells, int count)> regions = new List<(DistrictKind, bool, bool[,], int)>();

            bool Allowed(int x, int z) => allow == null || (x >= 0 && z >= 0 && x < D && z < D && allow[x, z]);
            bool EdgeAllowed(int key)
            {
                if (allow == null) return true;
                Sides(key, out var p, out var n);
                return Allowed(p.x, p.y) || Allowed(n.x, n.y);
            }
            bool AllAllowed(RectInt r)
            {
                if (allow == null) return true;
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                    if (!Allowed(x, z)) return false;
                return true;
            }

            /// Which region each cell belongs to. Copies read the source district's regions.
            void ComputeRegions()
            {
                var map = new Dictionary<(DistrictKind, bool), int>();
                var site = new Site[D, D];
                for (int x = 0; x < D; x++)
                for (int z = 0; z < D; z++)
                {
                    var world = new Vector2((lx * D + x + 0.5f) * C, (lz * D + z + 0.5f) * C);
                    var s = RegionAt(seed, world, ds);
                    mood[x, z] = s.Mood;
                    if (!map.TryGetValue((s.Kind, s.Giant), out int r))
                    {
                        r = regions.Count;
                        map[(s.Kind, s.Giant)] = r;
                        regions.Add((s.Kind, s.Giant, new bool[D, D], 0));
                    }
                    regions[r].cells[x, z] = true;
                    var e = regions[r];
                    e.count++;
                    regions[r] = e;
                }
                int most = 0;
                for (int i = 1; i < regions.Count; i++) if (regions[i].count > regions[most].count) most = i;
                Kind = regions[most].kind;
            }

            /// Builds each region's archetype inside its own cells, then marks some region edges with partitions.
            void BuildRegions()
            {
                var all = new RectInt(0, 0, D, D);
                foreach (var (kind, giant, cells, _) in regions)
                {
                    allow = cells;
                    if (giant) BuildGiantHall();
                    else switch (kind)
                    {
                        case DistrictKind.Maze: BuildMaze(); break;
                        case DistrictKind.OpenExpanse: BuildOpenExpanse(); break;
                        case DistrictKind.PillarHall: BuildPillarHall(); break;
                        case DistrictKind.OfficeBlock: BuildOffices(all, true); break;
                        case DistrictKind.Corridors: BuildCorridors(false); break;
                        case DistrictKind.PanelRows: BuildPanelRows(); break;
                        case DistrictKind.NestedRooms: BuildNested(); break;
                        case DistrictKind.Atrium: BuildAtrium(); break;
                        case DistrictKind.Storage: BuildStorage(); break;
                        case DistrictKind.Showroom: BuildCorridors(true); break;
                        case DistrictKind.Supermarket: BuildSupermarket(); break;
                        case DistrictKind.ClothingStore: BuildClothing(); break;
                        case DistrictKind.MallConcourse: BuildMall(); break;
                        case DistrictKind.ServerHall: BuildServerHall(); break;
                        case DistrictKind.Arches: BuildArches(); break;
                        case DistrictKind.Tunnels: BuildTunnels(theme.id == "1"); break;
                        case DistrictKind.ParkingHall: BuildHall(0); break;
                        case DistrictKind.GildHall: BuildHall(1); break;
                        case DistrictKind.GothicHall: BuildHall(2); break;
                        case DistrictKind.OuroborosHall: BuildHall(3); break;
                        case DistrictKind.GardenHall: BuildHall(4); break;
                        case DistrictKind.FabledHall: BuildHall(5); break;
                        case DistrictKind.Poolrooms: BuildPoolrooms(); break;
                        case DistrictKind.ElectricalStation: BuildElectricalStation(); break;
                        case DistrictKind.OfficeCubicles: BuildOfficeCubicles(); break;
                        case DistrictKind.HotelSuites: BuildHotelSuites(); break;
                    }
                }
                allow = null;
                foreach (var r in regions) if (r.kind == DistrictKind.Tunnels) { DressTunnels(theme.id == "1"); break; }
                if (regions.Count < 2) return;
                // Where two places meet, the edge is often a partition with doorways.
                for (int x = 1; x < D - 1; x++)
                for (int z = 1; z < D - 1; z++)
                {
                    int here = RegionOf(x, z);
                    // Red Rooms are walled off all round (only the doorways the reachability rule forces stay open).
                    bool redX = x + 1 < D && (mood[x, z] == Mood.Red) != (mood[x + 1, z] == Mood.Red);
                    bool redZ = z + 1 < D && (mood[x, z] == Mood.Red) != (mood[x, z + 1] == Mood.Red);
                    if (x + 1 < D && (redX || (RegionOf(x + 1, z) != here && rng.Value() < 0.45f))) Wall(x + 1, z, x + 1, z + 1);
                    if (z + 1 < D && (redZ || (RegionOf(x, z + 1) != here && rng.Value() < 0.45f))) Wall(x, z + 1, x + 1, z + 1);
                }
            }

            int RegionOf(int x, int z)
            {
                for (int i = 0; i < regions.Count; i++) if (regions[i].cells[x, z]) return i;
                return -1;
            }

            /// A hall so big it feels wrong: tall ceiling, heavy pillars fading into the fog, sparse light.
            void BuildGiantHall()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Hall, all);
                var z = zones[id];
                z.Height = GiantHeight;
                z.Lights = LightLayout.Grid;
                Paint(all, id);
                // Pillars on a grid aligned to world cells, so the grid continues across districts.
                int s = 5;
                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                    if (((lx * D + x) % s + s) % s == 2 && ((lz * D + zz) % s + s) % s == 2 && Allowed(x, zz)) Pillar(x, zz, false, 1.1f);
            }

            // ---------- long halls ----------

            void CarveHalls()
            {
                foreach (bool alongX in new[] { true, false })
                {
                    int crossBase = alongX ? dz * D : dx * D, alongBase = alongX ? dx * D : dz * D;
                    for (int band = FloorDiv(crossBase, BandCells); band <= FloorDiv(crossBase + D - 1, BandCells); band++)
                    {
                        if (!HallInBand(seed, alongX, band, out int cross)) continue;
                        int local = cross - crossBase;
                        if (local < 2 || local > D - 4) continue;
                        for (int k = FloorDiv(alongBase, 240) - 1; k <= FloorDiv(alongBase + D, 240); k++)
                        {
                            if (!HallSegment(seed, alongX, band, k, out int a, out int b)) continue;
                            int from = Mathf.Max(0, a - alongBase), to = Mathf.Min(D, b - alongBase);
                            if (to - from < 2) continue;
                            Hall(alongX, local, from, to);
                        }
                    }
                }
            }

            bool Locked(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < D && c.y < D && locked[c.x, c.y];

            /// A two-cell-wide corridor from `from` to `to` along one axis at cross position `c`.
            void Hall(bool alongX, int c, int from, int to)
            {
                var rect = alongX ? new RectInt(from, c, to - from, 2) : new RectInt(c, from, 2, to - from);
                int id = NewZone(ZoneKind.Corridor, rect);
                var z = zones[id];
                z.Height = 2.7f;
                z.Lights = LightLayout.CenterStrip;
                z.LightsAlongX = alongX;
                Paint(rect, id);
                int gap = rng.Range(5, 10);
                for (int i = from; i < to; i++)
                {
                    foreach (int side in new[] { c, c + 2 })
                    {
                        int x0 = alongX ? i : side, z0 = alongX ? side : i, x1 = alongX ? i + 1 : side, z1 = alongX ? side : i + 1;
                        Sides(Key(x0, z0, x1, z1), out var sp, out var sn);
                        if (locked != null && (Locked(sp) || Locked(sn))) continue;   // a stairwell or drop got here first
                        if ((i - from) % gap == gap / 2) Opening(x0, z0, x1, z1, OpeningStyle());
                        else Wall(x0, z0, x1, z1);
                    }
                }
                Reserve(alongX ? new RectInt(from + 1, c + 1, Mathf.Max(0, to - from - 2), 0) : new RectInt(c + 1, from + 1, 0, Mathf.Max(0, to - from - 2)));
                locked ??= new bool[D, D];
                for (int x = rect.xMin; x < rect.xMax; x++)
                for (int zz = rect.yMin; zz < rect.yMax; zz++)
                {
                    locked[x, zz] = true;
                    used[x, zz] = true;
                }
            }

            // ---------- light moods ----------

            /// Broad dim stretches (half the lights dead) and dark patches with a single light left on.
            void LightMoods()
            {
                float o = (seed % 991) * 0.53f;
                foreach (var z in zones)
                {
                    if (z.Kind == ZoneKind.Origin || z.Red || z.Rect.width <= 0) continue;
                    var c = z.Rect.center;
                    float wxm = (dx * D + c.x) * C, wzm = (dz * D + c.y) * C;
                    float mood = Mathf.PerlinNoise(wxm / 170f + o, wzm / 170f + o * 0.5f);
                    if (mood > 0.74f) { z.Dead = 1f; z.Pool = true; }
                    else if (mood > 0.62f) z.Dead = Mathf.Max(z.Dead, 0.5f);
                }
            }
        }
    }
}
