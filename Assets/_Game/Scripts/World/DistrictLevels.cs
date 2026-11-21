using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Archetypes for levels rebuilt from their Backrooms Wiki pages (backrooms-wiki.wikidot.com, CC BY-SA 3.0):
    /// - Level 2 "Pipe Dreams" (archived page): endless dark grey concrete maintenance tunnels, pipes along every
    ///   wall, the odd vent duct, black liquid leaking, unbearable heat where the machinery is loudest (the dead ends),
    ///   mostly locked doors, rooms with shelves of junk and the occasional computer.
    /// - Level 1 "Habitable Zone": the Halls (parking-garage/warehouse spaces in sectors: Aquila grey with huge
    ///   concrete pillars and leaking ceiling pipes; Gild, brighter and full of crates; Gothic, arches and round
    ///   pillars) and the Corridors (tight concrete passages, green exit lights over doors, odd little rooms).
    /// - Level 37 "Sublimity": white tiles, lukewarm water everywhere, open odd-shaped areas, pillars rising out of
    ///   deep pools, stairs that go down into the deep parts.
    public static partial class DistrictLayout
    {
        sealed partial class Plan
        {
            /// A helical climb set into a flooded room. The decks use `Center` and `Radius` to leave a shaft open.
            struct PoolSpiral
            {
                public Vector3 Center;
                public float Radius, Bottom, Top, Rise, Start, StepAngle, Tread;
                public int Steps;
                public bool Clockwise;

                public Vector3 At(float angle, float y) =>
                    Center + new Vector3(Mathf.Cos(angle) * Radius, y, Mathf.Sin(angle) * Radius);
            }

            // ---------- tunnels (Level 2, and Level 1's corridors) ----------

            /// A maze of two-metre tunnels: every cell edge is a wall except the passages of a winding spanning tree
            /// that prefers to keep going straight, so tunnels run long.
            void BuildTunnels(bool level1)
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Corridor, all);
                var z = zones[id];
                z.Height = level1 ? 2.7f : 2.5f;
                z.Lights = LightLayout.Sparse;
                z.Baseboard = false;
                z.ChairRail = false;
                Paint(all, id);

                var open = new HashSet<int>();
                var seen = new bool[D, D];
                var stack = new Stack<(Vector2Int c, int dir)>();
                var start = new Vector2Int(rng.Range(1, D - 1), rng.Range(1, D - 1));
                seen[start.x, start.y] = true;
                stack.Push((start, rng.Range(0, 4)));
                while (stack.Count > 0)
                {
                    var (c, lastDir) = stack.Peek();
                    var options = new List<int>();
                    for (int s = 0; s < 4; s++)
                    {
                        int nx = c.x + Dx[s], nz = c.y + Dz[s];
                        if (nx < 0 || nz < 0 || nx >= D || nz >= D || seen[nx, nz] || !Allowed(nx, nz)) continue;
                        options.Add(s);
                    }
                    if (options.Count == 0) { stack.Pop(); continue; }
                    int dir = options.Contains(lastDir) && rng.Value() < 0.72f ? lastDir : options[rng.Range(0, options.Count)];
                    var n = new Vector2Int(c.x + Dx[dir], c.y + Dz[dir]);
                    open.Add(EdgeBetween(c, n));
                    seen[n.x, n.y] = true;
                    stack.Push((n, dir));
                }
                // Rooms: a few rectangles with no inner walls.
                for (int r = rng.Range(1, 4); r > 0; r--)
                {
                    int w = rng.Range(2, 4), d = rng.Range(2, 4), x0 = rng.Range(2, D - 2 - w), z0 = rng.Range(2, D - 2 - d);
                    var room = new RectInt(x0, z0, w, d);
                    for (int x = x0; x < x0 + w; x++)
                    for (int zz = z0; zz < z0 + d; zz++)
                    {
                        if (x + 1 < x0 + w) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x + 1, zz)));
                        if (zz + 1 < z0 + d) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x, zz + 1)));
                    }
                    int rid = NewZone(ZoneKind.Room, room);
                    zones[rid].Height = z.Height;
                    zones[rid].Lights = level1 ? LightLayout.Sparse : LightLayout.Sparse;
                    zones[rid].Dead = level1 ? 0.5f : 0.2f;
                    Paint(room, rid);
                    FurnishTunnelRoom(room, rid, level1);
                }
                // Every other edge is a wall (the reachability rule turns any loop-closing one into a doorway).
                for (int x = 0; x < D; x++)
                for (int zz = 0; zz < D; zz++)
                {
                    var c = new Vector2Int(x, zz);
                    if (x + 1 < D && !open.Contains(EdgeBetween(c, new Vector2Int(x + 1, zz))) && Allowed(x, zz)) Wall(x + 1, zz, x + 1, zz + 1, EdgeKind.Doorway);
                    if (zz + 1 < D && !open.Contains(EdgeBetween(c, new Vector2Int(x, zz + 1))) && Allowed(x, zz)) Wall(x, zz + 1, x + 1, zz + 1, EdgeKind.Doorway);
                }
            }

            static int EdgeBetween(Vector2Int a, Vector2Int b) =>
                a.x != b.x ? Key(Mathf.Max(a.x, b.x), a.y, Mathf.Max(a.x, b.x), a.y + 1) : Key(a.x, Mathf.Max(a.y, b.y), a.x + 1, Mathf.Max(a.y, b.y));

            void FurnishTunnelRoom(RectInt r, int id, bool level1)
            {
                var c = origin + new Vector3((r.xMin + r.width / 2f) * C, 0f, (r.yMin + r.height / 2f) * C);
                float h = zones[id].Height;
                void Put(PropKind k, Vector3 at, float yaw) => props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Ceiling = h, Variant = rng.Range(0, 4) });
                int kind = rng.Range(0, level1 ? 5 : 2);
                switch (kind)
                {
                    case 0:   // shelves of random objects
                        for (int i = 0; i < r.width; i++) Put(PropKind.ShelfRack, origin + new Vector3((r.xMin + i + 0.5f) * C, 0f, r.yMin * C + 0.45f), 0f);
                        Put(PropKind.CardboardBox, c, rng.Value() * 90f);
                        break;
                    case 1:   // an old computer under a single bulb
                        Put(PropKind.Desk, c, 0f);
                        Put(PropKind.CrtTv, c + new Vector3(0.2f, 0.76f, 0.1f), 180f);
                        Put(PropKind.OfficeChair, c + new Vector3(0f, 0f, -0.7f), 10f);
                        zones[id].Lights = LightLayout.CenterStrip;
                        zones[id].Dead = 0.6f;
                        break;
                    case 2:   // an infirmary: a hospital bed in the middle
                        Put(PropKind.Bed, c, 90f);
                        Put(PropKind.Nightstand, c + new Vector3(1.2f, 0f, 0.6f), 0f);
                        zones[id].Wall = new SurfaceRef(Surface.Wall, 0);
                        break;
                    case 3:   // a rubber room: padded white, one chair sinking into the floor
                        zones[id].Wall = new SurfaceRef(Surface.Wall, Mathf.Min(1, wallpapers - 1));
                        props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(c + Vector3.down * 0.18f, Quaternion.Euler(4f, rng.Value() * 360f, 0f), Vector3.one), Ceiling = h });
                        break;
                    default:   // paintings on the walls and on the floor
                        for (int i = 0; i < 3; i++)
                        {
                            var at = c + new Vector3((rng.Value() - 0.5f) * r.width, 0.02f, (rng.Value() - 0.5f) * r.height);
                            readables.Add(new ReadableSpec { Kind = ReadableKind.Painting, Pose = Matrix4x4.TRS(at, Quaternion.Euler(-90f, rng.Value() * 360f, 0f), Vector3.one), Title = "A painting on the floor", Text = "" });
                        }
                        break;
                }
            }

            /// Level 2: pipes along every wall, ducts here and there, black liquid, boilers in the hot dead ends,
            /// locked doors. Level 1's corridors get green exit lights over their doorways instead.
            void DressTunnels(bool level1)
            {
                var keys = new List<int>(edges.Keys);
                keys.Sort();
                foreach (int key in keys)
                {
                    var e = edges[key];
                    if (!level1 && e.Kind == EdgeKind.Wall)
                    {
                        foreach (bool side in new[] { true, false })
                        {
                            if (!WallFrame(key, side, out var pose, out var cell) || Void(cell.x, cell.y)) continue;
                            // The same pipe run along the whole wall line, so pipes carry on from segment to segment.
                            Decode(key, out int ex, out int ez, out int edir);
                            int line = edir == 0 ? (dz * D + ez) * 2 : (dx * D + ex) * 2 + 1;
                            props.Add(new PropSpec { Kind = PropKind.WallPipes, Variant = (int)(WorldHash.Hash(seed, line, side ? 1 : 0, 0x91) & 7), Pose = pose, Ceiling = HeightAt(cell.x, cell.y) });
                        }
                        if (rng.Value() < 0.03f && WallFrame(key, rng.Value() < 0.5f, out var dp, out var dc) && !used[dc.x, dc.y])
                        {
                            used[dc.x, dc.y] = true;
                            if (rng.Value() < 0.35f)
                            {
                                // This one gives if you lean on it; about half of them lead out.
                                Vector3 at = dp.GetColumn(3);
                                string exit = rng.Value() < 0.5f && ms.exits.Count > 0 ? PickWeighted(ms.exits, l => l.weight).target : null;
                                boards.Add(new BoardSpec { Pose = dp, Width = DoorWidth, Pryable = true, LockedDoor = true, Exit = exit, Seed = rng.Range(0, 1000),
                                                           Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(at.x * 4f), Mathf.RoundToInt(at.z * 4f), 0xD00) });
                            }
                            else props.Add(new PropSpec { Kind = PropKind.DoorToNowhere, Pose = dp, Ceiling = HeightAt(dc.x, dc.y) });   // locked for good
                        }
                    }
                    if (level1 && e.Kind == EdgeKind.Doorway && rng.Value() < 0.5f && WallFrame(key, rng.Value() < 0.5f, out var sp, out var sc))
                        props.Add(new PropSpec { Kind = PropKind.ExitSign, Pose = sp * Matrix4x4.Translate(new Vector3(0f, 0f, 0f)), Ceiling = HeightAt(sc.x, sc.y) });
                }
                if (level1) return;
                for (int x = 1; x < D - 1; x++)
                for (int z = 1; z < D - 1; z++)
                {
                    if (!OpenCell(x, z) || used[x, z] || Void(x, z)) continue;
                    int walls = 0, openSide = -1;
                    for (int s = 0; s < 4; s++)
                    {
                        int k = s == 0 ? Key(x + 1, z, x + 1, z + 1) : s == 1 ? Key(x, z + 1, x + 1, z + 1) : s == 2 ? Key(x, z, x, z + 1) : Key(x, z, x + 1, z);
                        if (edges.TryGetValue(k, out var e) && Blocking(e.Kind)) walls++; else openSide = s;
                    }
                    var c = CellCenter(x, z);
                    if (walls == 3 && rng.Value() < 0.35f)
                    {
                        // A dead end: the machinery is loudest here, and it's unbearably hot.
                        used[x, z] = true;
                        float yaw = openSide == 0 ? 270f : openSide == 1 ? 180f : openSide == 2 ? 90f : 0f;
                        props.Add(new PropSpec { Kind = PropKind.Boiler, Pose = Matrix4x4.TRS(c, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Ceiling = HeightAt(x, z) });
                        panels.Add(new PanelSpec { Position = new Vector3(c.x, 0.012f, c.z), AlongX = true, State = FixtureState.Lit, Emitter = true, Range = 4f, Floor = true,
                                                   Id = (int)WorldHash.Hash(seed, dx * D + x, dz * D + z, 0xB01) });
                        hotSpots.Add(c);
                    }
                    else if (rng.Value() < 0.04f)
                        props.Add(new PropSpec { Kind = PropKind.Puddle, Variant = 1, Pose = Matrix4x4.TRS(c + new Vector3((rng.Value() - 0.5f) * 0.6f, 0f, (rng.Value() - 0.5f) * 0.6f), Quaternion.Euler(0f, rng.Value() * 180f, 0f), Vector3.one), Ceiling = HeightAt(x, z) });
                    else if (rng.Value() < 0.03f) DuctRun(x, z);
                }
            }

            readonly List<Vector3> hotSpots = new List<Vector3>();

            /// A duct along a straight stretch of tunnel, from wall to wall (never ending in mid-air), with no ceiling
            /// light under it.
            void DuctRun(int x, int z)
            {
                foreach (bool alongX in new[] { true, false })
                {
                    int sx = alongX ? 1 : 0, sz = alongX ? 0 : 1;
                    bool Blocked(int cx, int cz, int step)   // a wall on the far side of (cx, cz) going `step`
                    {
                        int k = alongX ? Key(cx + (step > 0 ? 1 : 0), cz, cx + (step > 0 ? 1 : 0), cz + 1) : Key(cx, cz + (step > 0 ? 1 : 0), cx + 1, cz + (step > 0 ? 1 : 0));
                        return edges.TryGetValue(k, out var e) && Blocking(e.Kind);
                    }
                    int a = 0, b = 0;
                    while (!Blocked(x - a * sx, z - a * sz, -1) && OpenCell(x - (a + 1) * sx, z - (a + 1) * sz) && !Void(x - (a + 1) * sx, z - (a + 1) * sz) && a < 12) a++;
                    while (!Blocked(x + b * sx, z + b * sz, 1) && OpenCell(x + (b + 1) * sx, z + (b + 1) * sz) && !Void(x + (b + 1) * sx, z + (b + 1) * sz) && b < 12) b++;
                    if (!Blocked(x - a * sx, z - a * sz, -1) || !Blocked(x + b * sx, z + b * sz, 1) || a + b < 1) continue;   // must end in walls at both ends
                    for (int i = -a; i <= b; i++)
                    {
                        int cx = x + i * sx, cz = z + i * sz;
                        noLights.Add(new Vector2Int(cx, cz));
                        props.Add(new PropSpec { Kind = PropKind.Duct, Variant = cx + cz, Pose = Matrix4x4.TRS(CellCenter(cx, cz), Quaternion.Euler(0f, alongX ? 0f : 90f, 0f), Vector3.one), Ceiling = HeightAt(cx, cz) });
                    }
                    return;
                }
            }

            readonly HashSet<Vector2Int> noLights = new HashSet<Vector2Int>();   // cells where something hangs from the ceiling

            // ---------- Level 1: the Halls ----------

            /// Aquila (0): the parking garage (grey decks, square pillars 16 m apart, painted bays, leaking pipes).
            /// Gild (1): brighter, colourful, crates everywhere. Gothic (2): arches and round pillars.
            /// Ouroboros (3): forever under construction. Garden (4): overgrown. Fabled (5): neon and cables over vermilion.
            /// (3-5 are our own takes on the wiki's sectors.)
            void BuildHall(int sector)
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Hall, all);
                var z = zones[id];
                z.Height = sector == 2 ? 4.4f : sector == 5 ? 3.2f : 3.6f;
                z.Lights = sector == 1 ? LightLayout.Dense : sector == 3 ? LightLayout.Sparse : LightLayout.Grid;
                z.Dead = sector switch { 1 => 0.04f, 3 => 0.45f, 5 => 0.55f, _ => 0.12f };
                z.Baseboard = false;
                z.ChairRail = false;
                if (sector == 1) z.Wall = new SurfaceRef(Surface.Wall, Mathf.Min(1 + rng.Range(0, 3), wallpapers - 1));   // painted concrete
                if (sector == 3) z.Wall = new SurfaceRef(Surface.Wall, Mathf.Min(4, wallpapers - 1));                     // bare block
                if (sector == 5) z.Wall = new SurfaceRef(Surface.Wall, Mathf.Min(2, wallpapers - 1));                     // vermilion
                if (sector == 4) z.Sick = true;   // light through leaves: greenish
                Paint(all, id);
                int s = sector == 2 ? 4 : sector == 0 ? 8 : 5;
                var pillarCells = new List<Vector2Int>();
                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                {
                    bool grid = (((lx * D + x) % s) + s) % s == 2 && (((lz * D + zz) % s) + s) % s == 2;
                    if (!grid || !Allowed(x, zz) || !OpenCell(x, zz) || used[x, zz]) continue;
                    if (sector == 2)
                    {
                        used[x, zz] = true;
                        props.Add(new PropSpec { Kind = PropKind.RoundPillar, Pose = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.identity, Vector3.one), Ceiling = z.Height });
                        blockers.Add(new Bounds(CellCenter(x, zz) + Vector3.up * z.Height / 2, new Vector3(0.9f, z.Height, 0.9f)));
                        placed.Add((props.Count - 1, blockers.Count - 1));
                    }
                    else Pillar(x, zz, false, 1.2f);
                    if (used[x, zz]) pillarCells.Add(new Vector2Int(x, zz));
                }
                if (sector == 2)
                {
                    // Arcades of arches across the hall.
                    for (int line = 8; line < D - 1; line += 8)
                        for (int i = 1; i < D - 1; i++)
                        {
                            if (rng.Value() < 0.75f) Opening(i, line, i + 1, line, EdgeKind.Arch);
                            Wall(i, line, i + 1, line, EdgeKind.Arch);
                        }
                }
                if (sector == 0) ParkingBays(z, s, pillarCells);
                // Leaking pipes along the ceiling.
                if (sector != 2 && sector != 5)
                    for (int row = 3; row < D - 1; row += 6)
                        for (int i = 0; i < D; i++)
                            props.Add(new PropSpec { Kind = PropKind.CeilingPipes, Variant = row + i, Pose = Matrix4x4.TRS(origin + new Vector3((i + 0.5f) * C, 0f, (row + 0.5f) * C), Quaternion.identity, Vector3.one), Ceiling = z.Height });
                // Crates on metal shelves: everywhere, most in Gild.
                int clusters = sector == 1 ? rng.Range(5, 9) : sector == 0 ? rng.Range(1, 3) : rng.Range(1, 4);
                for (int k = 0; k < clusters; k++)
                {
                    int x = rng.Range(2, D - 4), zz = rng.Range(2, D - 4);
                    if (!FreeStanding(x, zz) || !FreeStanding(x + 1, zz)) continue;
                    used[x, zz] = used[x + 1, zz] = true;
                    var c = CellCenter(x, zz) + new Vector3(C / 2, 0f, 0f);
                    props.Add(new PropSpec { Kind = PropKind.ShelfRack, Pose = Matrix4x4.TRS(c, Quaternion.identity, Vector3.one), Ceiling = z.Height });
                    for (int i = rng.Range(1, 4); i > 0; i--)
                        props.Add(new PropSpec { Kind = PropKind.Crate, Variant = i, Pose = Matrix4x4.TRS(c + new Vector3((rng.Value() - 0.5f) * 3f, 0f, 1.2f + rng.Value()), Quaternion.Euler(0f, rng.Value() * 40f, 0f), Vector3.one), Ceiling = z.Height });
                }
                if (sector == 0 || sector == 4)
                    for (int k = rng.Range(3, 8); k > 0; k--)
                    {
                        int x = rng.Range(1, D - 1), zz = rng.Range(1, D - 1);
                        if (OpenCell(x, zz) && !used[x, zz])
                            damp.Add(new Rect(CellCenter(x, zz).x - 0.8f - rng.Value(), CellCenter(x, zz).z - 0.6f, 1.6f + rng.Value() * 1.5f, 1.2f + rng.Value()));
                    }
                if (sector == 3) Ouroboros(z);
                if (sector == 4) Garden(z, pillarCells);
                if (sector == 5) Fabled(z, pillarCells);
            }

            bool Floorable(Vector3 p)
            {
                var c = CellOf(p);
                return c.x >= 0 && c.y >= 0 && c.x < D && c.y < D && OpenCell(c.x, c.y) && !Void(c.x, c.y);
            }

            void Put(PropKind kind, Vector3 at, float yaw, int variant, float ceiling, bool solid, Vector3 size = default)
            {
                if (!Floorable(at)) return;
                Bounds body = default;
                if (solid && size != default)
                {
                    var r = Quaternion.Euler(0f, yaw, 0f) * size;
                    body = new Bounds(at + Vector3.up * size.y / 2f, new Vector3(Mathf.Abs(r.x) + 0.3f, size.y, Mathf.Abs(r.z) + 0.3f));
                    foreach (var kv in edges) if (IsOpening(kv.Value.Kind) && PassageBounds(kv.Key).Intersects(body)) return;   // never in a doorway
                }
                props.Add(new PropSpec { Kind = kind, Variant = variant, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Ceiling = ceiling });
                if (!solid || size == default) return;
                var sz = Quaternion.Euler(0f, yaw, 0f) * size;
                blockers.Add(new Bounds(at + Vector3.up * size.y / 2f, new Vector3(Mathf.Abs(sz.x), size.y, Mathf.Abs(sz.z))));
                placed.Add((props.Count - 1, blockers.Count - 1));   // so ClearPassages can take it out of a doorway made later
            }

            /// The parking garage: rows of painted bays either side of each line of pillars, wheel stops, a car under
            /// a tarp here and there (rarely one left uncovered), oil stains, numbered pillars, hanging deck signs that
            /// point to the ramp up, a speed bump, a few barriers.
            void ParkingBays(Zone z, int s, List<Vector2Int> pillarCells)
            {
                int deck = floor + 4;   // P1 is the lowest deck
                var ups = new List<Vertical>();
                VerticalsBetween(levelSeed, dx, dz, floor, theme, ups);
                float rampX = float.NaN;
                foreach (var v in ups) if (v.Kind == VerticalKind.Ramp) rampX = origin.x + (v.Rect.xMin + 3) * C;
                var rows = new SortedSet<int>();
                var cols = new SortedSet<int>();
                foreach (var pc in pillarCells) { rows.Add(pc.y); cols.Add(pc.x); }
                int number = 1;
                foreach (var pc in pillarCells)
                    Put(PropKind.PillarNumber, CellCenter(pc.x, pc.y), 0f, 600 + ((Mathf.Abs(lx * 7 + lz * 13) + number++) % 89) + 10, z.Height, false);
                foreach (int row in rows)
                {
                    float zP = CellCenter(0, row).z;
                    var colList = new List<int>(cols);
                    for (int k = 0; k + 1 < colList.Count; k++)
                    {
                        float xa = CellCenter(colList[k], row).x, xb = CellCenter(colList[k + 1], row).x;
                        if (xb - xa > s * C + 0.1f) continue;   // a pillar's missing here: no clean gap
                        float cx = (xa + xb) / 2f;
                        foreach (int dir in new[] { 1, -1 })
                        {
                            if (!Floorable(new Vector3(cx, 0f, zP + dir * 3f)) || !Floorable(new Vector3(cx - 6f, 0f, zP + dir * 3f)) || !Floorable(new Vector3(cx + 6f, 0f, zP + dir * 3f))) continue;
                            int v = new[] { 2, 6, 10 }[rng.Range(0, 3)];   // five bays, white or yellow lines
                            props.Add(new PropSpec { Kind = PropKind.StallLines, Variant = v, Pose = Matrix4x4.TRS(new Vector3(cx, origin.y, zP + dir * 0.7f), Quaternion.Euler(0f, dir > 0 ? 0f : 180f, 0f), Vector3.one) });
                            for (int b = 0; b < 5; b++)
                            {
                                float bx = cx + (b - 2) * 2.6f;
                                var bay = new Vector3(bx, origin.y, zP + dir * 3.3f);
                                Put(PropKind.WheelStop, new Vector3(bx, origin.y, zP + dir * 1.25f), 0f, 0, z.Height, false);
                                float r = rng.Value();
                                if (r < 0.09f) Put(PropKind.TarpCar, bay, (dir > 0 ? 0f : 180f) + (rng.Value() - 0.5f) * 6f, rng.Range(0, 100), z.Height, true, new Vector3(1.9f, 1.4f, 4.4f));
                                else if (r < 0.105f) Put(PropKind.OldCar, bay, (dir > 0 ? 180f : 0f) + (rng.Value() - 0.5f) * 10f, rng.Range(0, 100), z.Height, true, new Vector3(1.8f, 1.4f, 4.5f));
                                else if (r < 0.25f) Put(PropKind.OilStain, bay, 0f, rng.Range(0, 100), z.Height, false);
                            }
                        }
                    }
                    // Deck signs over the aisle, half way to the next row, pointing at the ramp up.
                    int aisle = row + s / 2;
                    if (aisle < D - 1)
                        foreach (int col in cols)
                        {
                            if (rng.Value() > 0.5f) continue;
                            var at = CellCenter(col, aisle);
                            int arrow = float.IsNaN(rampX) || rampX >= at.x ? 0 : 1;
                            Put(PropKind.ParkingSign, at, 0f, deck + 10 * arrow, z.Height, false);
                        }
                }
                // A speed bump across an aisle, a few barriers.
                if (rows.Count > 0 && cols.Count > 1)
                {
                    int row = new List<int>(rows)[rng.Range(0, rows.Count)] + s / 2;
                    if (row < D - 1) Put(PropKind.SpeedBump, CellCenter(new List<int>(cols)[0] + s / 2, row), 90f, 0, z.Height, false);
                }
                for (int k = rng.Range(1, 4); k > 0; k--)
                {
                    int x = rng.Range(2, D - 2), zz = rng.Range(2, D - 2);
                    if (FreeStanding(x, zz)) { used[x, zz] = true; Put(PropKind.Barrier, CellCenter(x, zz), rng.Value() < 0.5f ? 0f : 90f, 0, z.Height, true, new Vector3(2f, 0.9f, 0.6f)); }
                }
            }

            /// Ouroboros: half-built walls of block, scaffolding, work lamps on tripods (the only good light here),
            /// cones, plastic sheeting hanging in the air.
            void Ouroboros(Zone z)
            {
                for (int k = rng.Range(8, 14); k > 0; k--)
                {
                    int x = rng.Range(2, D - 3), zz = rng.Range(2, D - 3);
                    if (!FreeStanding(x, zz)) continue;
                    used[x, zz] = true;
                    var c = CellCenter(x, zz);
                    float yaw = rng.Value() < 0.5f ? 0f : 90f;
                    switch (rng.Range(0, 4))
                    {
                        case 0: Put(PropKind.BlockWall, c, yaw, rng.Range(0, 100), z.Height, true, new Vector3(2f, 1.2f, 0.25f)); break;
                        case 1: Put(PropKind.Scaffold, c, yaw, 0, z.Height, true, new Vector3(2.1f, 3f, 1.1f)); break;
                        case 2: Put(PropKind.PlasticSheet, c, yaw, rng.Range(0, 100), z.Height, false); break;
                        default:
                            Put(PropKind.WorkLamp, c, rng.Value() * 360f, 0, z.Height, true, new Vector3(0.6f, 1.5f, 0.6f));
                            panels.Add(new PanelSpec { Position = c + Vector3.up * 1.45f, State = FixtureState.Lit, Emitter = true, Bare = true, Range = 10f, Tint = new Color(1f, 0.85f, 0.62f),
                                                       Id = (int)WorldHash.Hash(seed, dx * D + x, dz * D + zz, 0x0B0) });
                            break;
                    }
                }
                for (int k = rng.Range(8, 16); k > 0; k--)
                {
                    int x = rng.Range(1, D - 1), zz = rng.Range(1, D - 1);
                    if (OpenCell(x, zz) && !used[x, zz]) Put(PropKind.TrafficCone, CellCenter(x, zz) + new Vector3((rng.Value() - 0.5f) * 1.4f, 0f, (rng.Value() - 0.5f) * 1.4f), rng.Value() * 360f, 0, z.Height, false);
                }
            }

            /// Garden: vines down the pillars, plants pushing through cracks, moss on the floor.
            void Garden(Zone z, List<Vector2Int> pillarCells)
            {
                foreach (var pc in pillarCells)
                    for (int side = 0; side < 4; side++)
                        if (rng.Value() < 0.45f)
                        {
                            var n = new Vector3(Dx[side], 0f, Dz[side]);
                            props.Add(new PropSpec { Kind = PropKind.Vines, Variant = rng.Range(0, 100), Pose = Matrix4x4.TRS(CellCenter(pc.x, pc.y) + n * 0.6f, Quaternion.LookRotation(n), Vector3.one), Ceiling = z.Height });
                        }
                for (int k = rng.Range(14, 26); k > 0; k--)
                {
                    int x = rng.Range(1, D - 1), zz = rng.Range(1, D - 1);
                    if (!OpenCell(x, zz) || used[x, zz]) continue;
                    var c = CellCenter(x, zz) + new Vector3((rng.Value() - 0.5f) * 1.2f, 0f, (rng.Value() - 0.5f) * 1.2f);
                    Put(rng.Value() < 0.5f ? PropKind.Plant : PropKind.MossPatch, c, rng.Value() * 360f, rng.Range(0, 100), z.Height, false);
                }
            }

            /// Fabled: neon tubes on the pillars throwing coloured light, cables sagging from the ceiling.
            void Fabled(Zone z, List<Vector2Int> pillarCells)
            {
                foreach (var pc in pillarCells)
                {
                    if (rng.Value() > 0.7f) continue;
                    int side = rng.Range(0, 4), colour = rng.Range(0, 2);
                    var n = new Vector3(Dx[side], 0f, Dz[side]);
                    var at = CellCenter(pc.x, pc.y) + n * 0.6f;
                    props.Add(new PropSpec { Kind = PropKind.NeonStrip, Variant = colour + 2 * rng.Range(0, 3), Pose = Matrix4x4.TRS(at, Quaternion.LookRotation(n), Vector3.one), Ceiling = z.Height });
                    panels.Add(new PanelSpec { Position = at + n * 0.4f + Vector3.up * 2.3f, State = FixtureState.Lit, Emitter = true, Bare = true, Range = 6f,
                                               Tint = colour == 0 ? new Color(1f, 0.25f, 0.65f) : new Color(0.2f, 0.85f, 1f),
                                               Id = (int)WorldHash.Hash(seed, dx * D + pc.x, dz * D + pc.y, 0xF4B) });
                }
                for (int k = rng.Range(8, 14); k > 0; k--)
                {
                    int x = rng.Range(1, D - 1), zz = rng.Range(1, D - 1);
                    if (OpenCell(x, zz) && !Void(x, zz)) props.Add(new PropSpec { Kind = PropKind.Cables, Variant = rng.Range(0, 100), Pose = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.Euler(0f, rng.Value() * 180f, 0f), Vector3.one), Ceiling = z.Height });
                }
            }

            // ---------- Level 37: the Poolrooms ----------

            /// "Sublimity": pristine white tile under lukewarm water that never quite stops rippling.
            /// The wiki's level varies from uniform pools to odd, open shapes, with senseless connections, far too
            /// many pillars, ledges that are often missing, light from the wrong angles and stairs into deep water.
            /// One waterline for the whole level, so the surface never steps at a district border.
            void BuildPoolrooms()
            {
                var all = new RectInt(0, 0, D, D);
                float water = theme.waterHeight > 0f ? theme.waterHeight : Range(0.88f, 1.02f);
                int layout = rng.Range(0, 4);
                var tile = new SurfaceRef(Surface.Tile);
                var wall = new SurfaceRef(Surface.Wall);
                var ceiling = new SurfaceRef(Surface.Ceiling);

                int baseId = NewZone(ZoneKind.Hall, all);
                var z = zones[baseId];
                z.Height = Range(4.8f, 7.2f);
                z.Water = water;
                z.Lights = rng.Value() < 0.5f ? LightLayout.Staggered : LightLayout.Sparse;
                z.Dead = Mathf.Max(z.Dead, 0.08f);
                PoolLook(z, tile, wall, ceiling);
                Paint(all, baseId);

                // Partitions break sightlines; the union-find rule turns any wall that would seal a route into an opening.
                int walkers = layout == 1 ? rng.Range(13, 21) : layout == 2 ? rng.Range(6, 11) : rng.Range(9, 16);
                for (int i = walkers; i > 0; i--)
                    Walker(all, 0.26f, 0.08f, 0.05f, 0.24f, 10);

                // Irregular flooded rooms give each district its own ceiling steps and pools of light. The water
                // value is identical everywhere, so none of these boundaries shows up as a step in the surface.
                int rooms = layout == 1 ? rng.Range(4, 7) : layout == 2 ? rng.Range(2, 5) : rng.Range(2, 4);
                for (int i = rooms; i > 0; i--) PoolRoom(water, z.Height, tile, wall, ceiling);

                // Long, dead-straight (occasionally bent) courses: the level's purposeless connections between rooms.
                int courses = layout == 1 ? rng.Range(2, 4) : layout == 2 ? 1 : rng.Range(0, 2);
                for (int i = courses; i > 0; i--) PoolCourse(water, z.Height, tile, wall, ceiling);

                // Dry shelves are still the exception, but the poolrooms have more than one level of them now:
                // ledges, submerged galleries and the landings on the spiral climbs all read as separate floors.
                int ledges = layout == 2 ? rng.Range(3, 6) : rng.Range(2, 5);
                for (int i = ledges; i > 0; i--) PoolLedge(water, z.Height, tile, wall, ceiling);

                var basins = new List<RectInt>();
                int basinsWanted = layout == 3 ? rng.Range(4, 7) : rng.Range(3, 6);
                if (layout == 2 && rng.Value() < 0.35f) basinsWanted--;
                for (int i = basinsWanted; i > 0; i--) PoolDeepBasin(water, z.Height, tile, wall, ceiling, basins);

                // Smaller sunk rooms fill the gaps between the big basins. Several are completely under water,
                // so swimming from one to another becomes a route through the level rather than a dead end.
                int pockets = layout == 2 ? rng.Range(2, 4) : rng.Range(3, 6);
                for (int i = pockets; i > 0; i--) PoolDeepPocket(water, z.Height, tile, wall, ceiling, basins);

                // Pillars come in clusters with gaps rather than a square grid; some rise from the deep basins.
                int clusters = layout == 2 ? rng.Range(2, 4) : rng.Range(3, 6);
                for (int i = clusters; i > 0; i--) PoolPillarCluster(z.Height);

                PoolLights(basins);   // Level 37 is described as devoid of life; keep its pools pristine and empty.
            }

            /// The waterpark pass. Each district gets one seed-driven mood: bright attraction, abandoned play
            /// area, or the oversized/wrong version. Toys are scattered through every flooded room; slides and
            /// larger objects claim whole cells so they never sit in a doorway or clip a pillar.
            void PoolToys(float water)
            {
                int mood = rng.Range(0, 4);   // 0 bright, 1 abandoned, 2 uncanny, 3 mixed
                int count = mood == 0 ? rng.Range(16, 24) : mood == 1 ? rng.Range(9, 15)
                          : mood == 2 ? rng.Range(6, 11) : rng.Range(12, 19);

                // Small toys: any clear flooded cell. LowerSunkContents drops them later, so a pose at
                // water + Sunk ends up floating exactly on the single level-wide surface.
                for (int i = count; i > 0; i--)
                {
                    int x = rng.Range(1, D - 1), zz = rng.Range(1, D - 1);
                    if (!FreeStanding(x, zz) || used[x, zz]) continue;
                    var zoneInfo = zones[zone[x, zz]];
                    if (zoneInfo.Water <= 0f) continue;   // dry ledges stay dry

                    float pick = rng.Value();
                    PropKind kind;
                    if (mood == 0) kind = pick < 0.28f ? PropKind.Floatie : pick < 0.56f ? PropKind.PoolNoodle : pick < 0.78f ? PropKind.RubberDuck : PropKind.Slide;
                    else if (mood == 1) kind = pick < 0.2f ? PropKind.Floatie : pick < 0.55f ? PropKind.PoolNoodle : pick < 0.95f ? PropKind.RubberDuck : PropKind.Slide;
                    else if (mood == 2) kind = pick < 0.3f ? PropKind.Floatie : pick < 0.45f ? PropKind.PoolNoodle : pick < 0.95f ? PropKind.RubberDuck : PropKind.Slide;
                    else kind = pick < 0.3f ? PropKind.Floatie : pick < 0.55f ? PropKind.PoolNoodle : pick < 0.8f ? PropKind.RubberDuck : PropKind.Slide;

                    if (kind == PropKind.Slide)
                    {
                        int dir = rng.Range(0, 4);
                        if (PlaceSlide(x, zz, dir)) used[x, zz] = true;
                        continue;
                    }

                    float sunk = zoneInfo.Sunk;
                    float size = mood == 2 && kind == PropKind.RubberDuck ? Range(1.5f, 1.9f)
                               : mood == 1 ? Range(0.82f, 1f) : Range(0.95f, 1.1f);
                    var at = CellCenter(x, zz);
                    at.y = water + Mathf.Max(0f, sunk);   // float on the surface, even above a deep basin
                    props.Add(new PropSpec { Kind = kind, Variant = rng.Range(0, 100),
                        Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one * size),
                        Ceiling = HeightAt(x, zz) });
                    used[x, zz] = true;
                }

                // A few larger showpieces in the open shallow water, one per district at most.
                if (mood == 0 || mood == 3)
                {
                    for (int tries = 20; tries > 0; tries--)
                    {
                        int x = rng.Range(2, D - 3), zz = rng.Range(2, D - 3);
                        if (PlaceSlide(x, zz, rng.Range(0, 4))) break;
                    }
                }
            }

            /// A slide needs a straight, wall-free run of four cells on level flooded floor. Its chute is built
            /// into the solid Props mesh, so the chunk collider makes it genuinely climbable and slideable.
            bool PlaceSlide(int x, int zz, int dir)
            {
                int dx = Dx[dir], dz = Dz[dir];
                var cells = new List<Vector2Int>();
                for (int k = -1; k <= 2; k++)
                {
                    int cx = x + dx * k, cz = zz + dz * k;
                    if (cx < 1 || cz < 1 || cx >= D - 1 || cz >= D - 1) return false;
                    if (!FreeStanding(cx, cz) || used[cx, cz]) return false;
                    var zn = zones[zone[cx, cz]];
                    if (zn.Water <= 0f || Mathf.Abs(zn.Sunk) > 0.01f) return false;
                    cells.Add(new Vector2Int(cx, cz));
                }
                var at = Vector3.zero;
                foreach (var c in cells) at += CellCenter(c.x, c.y);
                at /= cells.Count;
                at.y = 0f;
                float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                props.Add(new PropSpec { Kind = PropKind.Slide, Variant = rng.Range(0, 100),
                    Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Ceiling = 4f });
                foreach (var c in cells) used[c.x, c.y] = true;
                return true;
            }

            /// Every Poolrooms room is the same pristine white tile; no carpet, trim or damage anywhere.
            void PoolLook(Zone z, SurfaceRef tile, SurfaceRef wall, SurfaceRef ceiling)
            {
                z.Wall = wall;
                z.Floor = tile;
                z.Ceiling = ceiling;
                z.Baseboard = false;
                z.ChairRail = false;
                z.Pilasters = false;
                z.FloorRot = false;
            }

            /// The cells of a set of overlapping rectangles, deduplicated so L and T rooms behave like one room.
            HashSet<Vector2Int> PoolShapeCells(IEnumerable<RectInt> parts)
            {
                var cells = new HashSet<Vector2Int>();
                foreach (var r in parts)
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(D, r.xMax); x++)
                for (int zz = Mathf.Max(0, r.yMin); zz < Mathf.Min(D, r.yMax); zz++)
                    if (OpenCell(x, zz)) cells.Add(new Vector2Int(x, zz));
                return cells;
            }

            /// Features need a clear cell and a clear rim, so their walls and drops never cut another room in half.
            bool PoolShapeFree(IEnumerable<RectInt> parts)
            {
                foreach (var r in parts)
                for (int x = r.xMin - 1; x <= r.xMax; x++)
                for (int zz = r.yMin - 1; zz <= r.yMax; zz++)
                {
                    if (x < 0 || zz < 0 || x >= D || zz >= D) continue;
                    if (!OpenCell(x, zz) || used[x, zz] || reserved[x, zz] || (locked != null && locked[x, zz])) return false;
                }
                return true;
            }

            void ReservePoolShape(HashSet<Vector2Int> cells)
            {
                foreach (var c in cells)
                for (int x = Mathf.Max(0, c.x - 1); x <= Mathf.Min(D, c.x + 1); x++)
                for (int zz = Mathf.Max(0, c.y - 1); zz <= Mathf.Min(D, c.y + 1); zz++)
                    reserved[x, zz] = true;
            }

            void ClearPoolEdges(HashSet<Vector2Int> cells)
            {
                foreach (var c in cells)
                {
                    var east = new Vector2Int(c.x + 1, c.y);
                    var north = new Vector2Int(c.x, c.y + 1);
                    if (cells.Contains(east)) edges.Remove(EdgeBetween(c, east));
                    if (cells.Contains(north)) edges.Remove(EdgeBetween(c, north));
                }
            }

            void PoolWalls(HashSet<Vector2Int> cells) => RegionWalls(new List<Vector2Int>(cells), cells, EdgeKind.Wide);

            /// An L (or sometimes T) made from two crossing bars, still inside a clean bounding rectangle.
            RectInt[] PoolLShape(out RectInt bounds)
            {
                int w = rng.Range(7, 13), d = rng.Range(6, 12);
                int x = rng.Range(2, D - 2 - w), zz = rng.Range(2, D - 2 - d);
                bounds = new RectInt(x, zz, w, d);
                int t = rng.Range(3, Mathf.Min(w, d));
                int barZ = rng.Value() < 0.5f ? zz : zz + d - t;
                int barX = rng.Value() < 0.5f ? x : x + w - t;
                if (w - t > 1 && rng.Value() < 0.35f) barX = x + rng.Range(1, w - t);
                return new[] { new RectInt(x, barZ, w, t), new RectInt(barX, zz, t, d) };
            }

            RectInt[] PoolShelfShape(out RectInt bounds)
            {
                int w = rng.Range(4, 11), d = rng.Range(4, 11);
                int x = rng.Range(2, D - 2 - w), zz = rng.Range(2, D - 2 - d);
                bounds = new RectInt(x, zz, w, d);
                if (rng.Value() > 0.55f) return new[] { bounds };
                int t = rng.Range(2, Mathf.Min(w, d));
                int barZ = rng.Value() < 0.5f ? zz : zz + d - t;
                int barX = rng.Value() < 0.5f ? x : x + w - t;
                return new[] { new RectInt(x, barZ, w, t), new RectInt(barX, zz, t, d) };
            }

            void PoolRoom(float water, float baseHeight, SurfaceRef tile, SurfaceRef wall, SurfaceRef ceiling)
            {
                var parts = PoolLShape(out var bounds);
                if (!PoolShapeFree(parts)) return;
                int id = NewZone(rng.Value() < 0.7f ? ZoneKind.Hall : ZoneKind.Room, bounds);
                var zz = zones[id];
                zz.Height = Mathf.Clamp(Snap(baseHeight + Range(-1.6f, 2f)), 3.6f, 8.2f);
                zz.Water = water;
                float lights = rng.Value();
                zz.Lights = lights < 0.4f ? LightLayout.Sparse : lights < 0.7f ? LightLayout.Grid : LightLayout.Staggered;
                float dark = rng.Value();
                zz.Dead = dark < 0.18f ? 1f : Mathf.Max(zz.Dead, dark < 0.45f ? 0.4f : 0.04f);
                zz.Pool = zz.Dead > 0.9f && rng.Value() < 0.7f;
                PoolLook(zz, tile, wall, ceiling);
                foreach (var r in parts) Paint(r, id);
                var cells = PoolShapeCells(parts);
                ClearPoolEdges(cells);
                if (rng.Value() < 0.65f) PoolWalls(cells);
                ReservePoolShape(cells);
            }

            void PoolCourse(float water, float baseHeight, SurfaceRef tile, SurfaceRef wall, SurfaceRef ceiling)
            {
                bool alongX = rng.Value() < 0.5f;
                int lane = rng.Range(2, D - 5), width = rng.Range(2, 4);
                var main = alongX ? new RectInt(1, lane, D - 2, width) : new RectInt(lane, 1, width, D - 2);
                var parts = new List<RectInt> { main };
                if (rng.Value() < 0.4f)
                {
                    int bend = rng.Range(2, D - 5);
                    parts.Add(alongX ? new RectInt(bend, 1, width, D - 2) : new RectInt(1, bend, D - 2, width));
                }
                if (!PoolShapeFree(parts)) return;
                int id = NewZone(ZoneKind.Corridor, main);
                var zz = zones[id];
                zz.Height = Mathf.Clamp(Snap(baseHeight - Range(0.2f, 1.2f)), 3.4f, 7.4f);
                zz.Water = water;
                zz.Lights = rng.Value() < 0.5f ? LightLayout.Rows : LightLayout.CenterStrip;
                zz.LightsAlongX = alongX;
                zz.Dead = Mathf.Max(zz.Dead, rng.Value() < 0.3f ? 0.65f : 0.12f);
                PoolLook(zz, tile, wall, ceiling);
                foreach (var r in parts) Paint(r, id);
                var cells = PoolShapeCells(parts);
                ClearPoolEdges(cells);
                if (rng.Value() < 0.7f) PoolWalls(cells);
                ReservePoolShape(cells);
                foreach (var c in cells)
                    if (rng.Value() < 0.12f && FreeStanding(c.x, c.y))
                    {
                        PoolPillar(c.x, c.y, zz.Height, Range(0.26f, 0.36f));
                        used[c.x, c.y] = true;
                    }
            }

            void PoolLedge(float water, float baseHeight, SurfaceRef tile, SurfaceRef wall, SurfaceRef ceiling)
            {
                var parts = PoolShelfShape(out var bounds);
                if (!PoolShapeFree(parts)) return;
                int id = NewZone(ZoneKind.Hall, bounds);
                var zz = zones[id];
                // Negative Sunk raises the floor. No railing: a ledge is somewhere to haul yourself out, nothing more.
                zz.Sunk = -(water + Range(0.10f, 0.32f));
                zz.Water = 0f;
                zz.Height = Mathf.Clamp(Snap(baseHeight + Range(-0.4f, 0.9f)), 3.6f, 8f);
                zz.Lights = rng.Value() < 0.5f ? LightLayout.Grid : LightLayout.Sparse;
                zz.Dead = Mathf.Max(zz.Dead, rng.Value() < 0.25f ? 0.8f : 0.04f);
                PoolLook(zz, tile, wall, ceiling);
                foreach (var r in parts) Paint(r, id);
                var cells = PoolShapeCells(parts);
                ClearPoolEdges(cells);
                ReservePoolShape(cells);
            }

            void PoolDeepBasin(float water, float baseHeight, SurfaceRef tile, SurfaceRef wall, SurfaceRef ceiling, List<RectInt> basins)
            {
                int w = rng.Range(6, 13), d = rng.Range(7, 15);
                int x0 = rng.Range(2, D - 2 - w), z0 = rng.Range(2, D - 2 - d);
                var r = new RectInt(x0, z0, w, d);
                if (!PoolFree(r)) return;
                int id = NewZone(ZoneKind.Hall, r);
                var zz = zones[id];
                float depth = Range(4.2f, 5.8f);
                zz.Height = baseHeight;
                zz.Sunk = depth;
                zz.Water = water;
                float lights = rng.Value();
                zz.Lights = lights < 0.6f ? LightLayout.Sparse : LightLayout.Grid;
                float dark = rng.Value();
                if (dark < 0.4f) zz.Dead = 1f;                              // unlit depths
                else if (dark < 0.7f) { zz.Dead = 0.92f; zz.Pool = true; }  // one light left burning
                PoolLook(zz, tile, wall, ceiling);
                Paint(r, id);
                ClearInternalEdges(r);
                Lock(r);
                Reserve(r);
                basins.Add(r);

                var mouths = PoolBasinMouths(r, depth, tile, rng.Range(2, 4));
                PoolBasinRim(r, depth, mouths, tile);

                // Every large basin gets a spiral and two or three submerged galleries. The climb comes out above
                // the waterline, so the top is walkable and the rest is a route you can swim.
                var spiral = PoolSpiralStair(r, depth, water, zz.Height, tile);
                PoolSubmergedFloors(r, depth, tile, spiral);

                for (int i = r.width * r.height / 9; i > 0; i--)
                {
                    int x = rng.Range(x0 + 2, x0 + w - 2), cz = rng.Range(z0 + 2, z0 + d - 2);
                    var c = CellCenter(x, cz);
                    if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(spiral.Center.x, spiral.Center.z)) < spiral.Radius + 1.1f) continue;
                    PoolPillar(x, cz, baseHeight + depth, Range(0.28f, 0.46f));
                }
            }

            /// Carves one to three wide mouths and puts tiled steps in most of them. The returned edge keys are
            /// the gaps the basin's rim has to leave open, so a drop face never stands in a staircase.
            HashSet<int> PoolBasinMouths(RectInt r, float depth, SurfaceRef tile, int count)
            {
                var mouths = new HashSet<int>();
                bool first = true;
                for (int m = count; m > 0; m--)
                {
                    int side = rng.Range(0, 4);
                    int along = side < 2 ? rng.Range(r.xMin + 1, r.xMax - 1) : rng.Range(r.yMin + 1, r.yMax - 1);
                    int key = side == 0 ? Key(along, r.yMin, along + 1, r.yMin)
                            : side == 1 ? Key(along, r.yMax, along + 1, r.yMax)
                            : side == 2 ? Key(r.xMin, along, r.xMin, along + 1)
                                        : Key(r.xMax, along, r.xMax, along + 1);
                    if (!mouths.Add(key)) continue;
                    edges[key] = new Edge(EdgeKind.Wide, false);
                    if (first || rng.Value() < 0.65f)
                    {
                        poolStairMouths.Add(key);
                        PoolStair(r, depth, side, along, tile);
                    }
                    first = false;
                }
                return mouths;
            }

            /// The sheer tiled lip all round a sunk room, inset a centimetre behind FloorStep's drop face.
            void PoolBasinRim(RectInt r, float depth, HashSet<int> mouths, SurfaceRef tile)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int cz = r.yMin; cz < r.yMax; cz++)
                {
                    if (x > r.xMin && x < r.xMax - 1 && cz > r.yMin && cz < r.yMax - 1) continue;
                    bool mouth = (cz == r.yMin && mouths.Contains(Key(x, r.yMin, x + 1, r.yMin)))
                              || (cz == r.yMax - 1 && mouths.Contains(Key(x, r.yMax, x + 1, r.yMax)))
                              || (x == r.xMin && mouths.Contains(Key(r.xMin, cz, r.xMin, cz + 1)))
                              || (x == r.xMax - 1 && mouths.Contains(Key(r.xMax, cz, r.xMax, cz + 1)));
                    if (mouth) continue;
                    var c = origin + new Vector3((x + 0.5f) * C, -depth / 2f, (cz + 0.5f) * C);
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(c, new Vector3(C - 0.02f, depth, C - 0.02f)),
                        Faces = BoxMeshBuilder.Faces.Sides | BoxMeshBuilder.Faces.PosY,
                        PosX = tile, NegX = tile, PosZ = tile, NegZ = tile, PosY = tile, Solid = true });
                }
            }

            /// A smaller sunk room filling the space between the big basins. Same waterline, different floor height.
            void PoolDeepPocket(float water, float baseHeight, SurfaceRef tile, SurfaceRef wall, SurfaceRef ceiling, List<RectInt> basins)
            {
                int w = rng.Range(4, 8), d = rng.Range(4, 9);
                int x0 = rng.Range(2, D - 2 - w), z0 = rng.Range(2, D - 2 - d);
                var r = new RectInt(x0, z0, w, d);
                if (!PoolFree(r)) return;
                int id = NewZone(ZoneKind.Room, r);
                var zz = zones[id];
                float depth = Range(2.6f, 4f);
                zz.Height = Mathf.Clamp(Snap(baseHeight - Range(0f, 1.1f)), 3.4f, 7.6f);
                zz.Sunk = depth;
                zz.Water = water;
                zz.Lights = rng.Value() < 0.6f ? LightLayout.Sparse : LightLayout.Grid;
                float dark = rng.Value();
                if (dark < 0.35f) zz.Dead = 1f;                              // a pocket you cannot see the bottom of
                else if (dark < 0.75f) { zz.Dead = 0.92f; zz.Pool = true; }
                PoolLook(zz, tile, wall, ceiling);
                Paint(r, id);
                ClearInternalEdges(r);
                Lock(r);
                Reserve(r);
                basins.Add(r);

                var mouths = PoolBasinMouths(r, depth, tile, rng.Range(1, 3));
                PoolBasinRim(r, depth, mouths, tile);

                PoolSpiral spiral = default;
                if (Mathf.Min(r.width, r.height) >= 6 && rng.Value() < 0.55f)
                    spiral = PoolSpiralStair(r, depth, water, zz.Height, tile);
                PoolSubmergedFloors(r, depth, tile, spiral);

                for (int i = r.width * r.height / 12; i > 0; i--)
                {
                    int x = rng.Range(x0 + 1, x0 + w - 1), cz = rng.Range(z0 + 1, z0 + d - 1);
                    var c = CellCenter(x, cz);
                    if (spiral.Radius > 0f && Vector2.Distance(new Vector2(c.x, c.z), new Vector2(spiral.Center.x, spiral.Center.z)) < spiral.Radius + 0.8f) continue;
                    PoolPillar(x, cz, zz.Height + depth, Range(0.24f, 0.38f));
                }
            }

            /// A tiled helix: flat radial treads with a 0.2 m rise, a square tile core, and landings at both ends.
            /// Its triangles all go into the Structure mesh, so the same mesh collider you see is what you climb.
            PoolSpiral PoolSpiralStair(RectInt r, float depth, float water, float ceiling, SurfaceRef tile)
            {
                float halfCells = Mathf.Min(r.width, r.height) * 0.5f;
                float radius = Mathf.Clamp(halfCells * C - 0.8f, 1.45f, 2.5f);
                float outerCells = (radius + 0.6f) / C;   // tread half-width plus a little landing
                float jitter = Mathf.Max(0f, Mathf.Min(1.4f, halfCells - outerCells - 0.25f));
                int perTurn = rng.Range(18, 23);
                var s = new PoolSpiral
                {
                    Center = origin + new Vector3((r.xMin + r.width / 2f + (rng.Value() * 2f - 1f) * jitter) * C, 0f,
                                                  (r.yMin + r.height / 2f + (rng.Value() * 2f - 1f) * jitter) * C),
                    Radius = radius,
                    Bottom = -depth + 0.12f,
                    Top = Mathf.Min(water + Range(1.25f, 1.85f), ceiling - 0.55f),
                    Tread = 1.15f,
                    Start = rng.Value() * Mathf.PI * 2f,
                    Clockwise = rng.Value() < 0.5f,
                    StepAngle = Mathf.PI * 2f / perTurn
                };
                s.Steps = Mathf.Max(8, Mathf.CeilToInt((s.Top - s.Bottom) / 0.2f));
                s.Rise = (s.Top - s.Bottom) / s.Steps;

                float columnBottom = s.Bottom - 0.25f, columnTop = Mathf.Min(s.Top + 0.7f, ceiling - 0.05f);
                extraBoxes.Add(new SurfaceBox
                {
                    Box = new Bounds(s.Center + Vector3.up * ((columnBottom + columnTop) / 2f), new Vector3(0.72f, columnTop - columnBottom, 0.72f)),
                    Faces = BoxMeshBuilder.Faces.All,
                    PosX = tile, NegX = tile, PosY = tile, NegY = tile, PosZ = tile, NegZ = tile, Solid = true
                });

                Vector3 P(float angle, float atRadius, float y) =>
                    s.Center + new Vector3(Mathf.Cos(angle) * atRadius, y, Mathf.Sin(angle) * atRadius);

                float dir = s.Clockwise ? -1f : 1f;
                float tangent = s.Radius * s.StepAngle * 1.14f;   // slight overlap: no walkable gaps between treads
                for (int i = 0; i < s.Steps; i++)
                {
                    float angle = s.Start + dir * s.StepAngle * i;
                    float y = s.Bottom + (i + 1) * s.Rise;
                    slopes.Add(new SurfaceSlope
                    {
                        A = P(angle, s.Radius - s.Tread * 0.5f, y),
                        B = P(angle, s.Radius + s.Tread * 0.5f, y),
                        Width = tangent, Thickness = 0.16f, Top = tile, Side = tile
                    });
                }
                PoolSpiralLanding(s, s.Bottom, s.Start, tile);
                PoolSpiralLanding(s, s.Top, s.Start + dir * s.StepAngle * s.Steps, tile);
                return s;
            }

            void PoolSpiralLanding(PoolSpiral s, float y, float angle, SurfaceRef tile)
            {
                Vector3 P(float atRadius) => s.Center + new Vector3(Mathf.Cos(angle) * atRadius, y, Mathf.Sin(angle) * atRadius);
                slopes.Add(new SurfaceSlope { A = P(s.Radius - s.Tread * 0.5f), B = P(s.Radius + 0.6f),
                    Width = 1.5f, Thickness = 0.18f, Top = tile, Side = tile });
            }

            void PoolSpiralBridge(Vector3 from, Vector3 to, float y, SurfaceRef tile)
            {
                from.y = to.y = y;
                if ((to - from).sqrMagnitude < 0.09f) return;
                slopes.Add(new SurfaceSlope { A = from, B = to, Width = 1.25f, Thickness = 0.16f, Top = tile, Side = tile });
            }

            /// Two or three partial floors below the waterline. Each tier is a pair of galleries on alternating
            /// axes, leaving an open shaft through the middle for the spiral and for swimmers going straight down.
            void PoolSubmergedFloors(RectInt r, float depth, SurfaceRef tile, PoolSpiral spiral)
            {
                if (spiral.Radius <= 0f)
                {
                    var deck = new RectInt(r.xMin + 1, r.yMin + 1, Mathf.Max(0, r.width - 2), Mathf.Max(0, r.height - 2));
                    PoolDeck(deck, -depth * 0.62f, depth, tile, spiral, 0);
                    return;
                }

                int tiers = depth >= 4.8f ? 3 : 2;
                int strip = Mathf.Clamp(Mathf.Min(r.width, r.height) / 4, 2, 3);
                for (int t = 0; t < tiers; t++)
                {
                    float y = -depth + (t + 1) * depth / (tiers + 1f) - 0.08f;
                    int inset = 1 + (t & 1);
                    if ((t & 1) == 0)
                    {
                        int span = r.width - 2 * inset, thick = Mathf.Min(strip, r.height - 2 * inset);
                        if (span <= 2 || thick <= 0) continue;
                        var north = new RectInt(r.xMin + inset, r.yMin + inset, span, thick);
                        var south = new RectInt(r.xMin + inset, r.yMax - inset - thick, span, thick);
                        PoolDeck(north, y, depth, tile, spiral, t);
                        PoolDeck(south, y, depth, tile, spiral, t);
                        float northEdge = origin.z + (north.yMin + north.height - 0.5f) * C;
                        if (spiral.Center.z > northEdge + 0.2f)
                            PoolSpiralBridge(new Vector3(spiral.Center.x, y, spiral.Center.z - spiral.Radius), new Vector3(spiral.Center.x, y, northEdge), y, tile);
                        float southEdge = origin.z + (south.yMin + 0.5f) * C;
                        if (spiral.Center.z < southEdge - 0.2f)
                            PoolSpiralBridge(new Vector3(spiral.Center.x, y, spiral.Center.z + spiral.Radius), new Vector3(spiral.Center.x, y, southEdge), y, tile);
                    }
                    else
                    {
                        int span = r.height - 2 * inset, thick = Mathf.Min(strip, r.width - 2 * inset);
                        if (span <= 2 || thick <= 0) continue;
                        var west = new RectInt(r.xMin + inset, r.yMin + inset, thick, span);
                        var east = new RectInt(r.xMax - inset - thick, r.yMin + inset, thick, span);
                        PoolDeck(west, y, depth, tile, spiral, t);
                        PoolDeck(east, y, depth, tile, spiral, t);
                        float eastEdge = origin.x + (east.xMin + 0.5f) * C;
                        if (spiral.Center.x < eastEdge - 0.2f)
                            PoolSpiralBridge(new Vector3(spiral.Center.x + spiral.Radius, y, spiral.Center.z), new Vector3(eastEdge, y, spiral.Center.z), y, tile);
                        float westEdge = origin.x + (west.xMin + west.width - 0.5f) * C;
                        if (spiral.Center.x > westEdge + 0.2f)
                            PoolSpiralBridge(new Vector3(spiral.Center.x - spiral.Radius, y, spiral.Center.z), new Vector3(westEdge, y, spiral.Center.z), y, tile);
                    }
                }
            }

            /// One gallery, split into 2 m tiles so each piece is owned by the chunk its centre sits in.
            void PoolDeck(RectInt cells, float y, float depth, SurfaceRef tile, PoolSpiral spiral, int tier)
            {
                for (int x = cells.xMin; x < cells.xMax; x++)
                for (int z = cells.yMin; z < cells.yMax; z++)
                {
                    var c = CellCenter(x, z);
                    if (spiral.Radius > 0f && Vector2.Distance(new Vector2(c.x, c.z), new Vector2(spiral.Center.x, spiral.Center.z)) < spiral.Radius + 0.7f) continue;
                    extraBoxes.Add(new SurfaceBox
                    {
                        Box = new Bounds(new Vector3(c.x, y - 0.09f, c.z), new Vector3(C - 0.05f, 0.18f, C - 0.05f)),
                        Faces = BoxMeshBuilder.Faces.All,
                        PosX = tile, NegX = tile, PosY = tile, NegY = tile, PosZ = tile, NegZ = tile, Solid = true
                    });
                    if ((x + z + tier) % 5 != 0) continue;
                    float bottom = -depth + 0.08f, top = y - 0.18f, h = top - bottom;
                    if (h < 0.25f) continue;
                    extraBoxes.Add(new SurfaceBox
                    {
                        Box = new Bounds(new Vector3(c.x, (bottom + top) / 2f, c.z), new Vector3(0.26f, h, 0.26f)),
                        Faces = BoxMeshBuilder.Faces.Sides,
                        PosX = tile, NegX = tile, PosZ = tile, NegZ = tile, Solid = true
                    });
                }
            }

            void PoolPillarCluster(float baseHeight)
            {
                int cx = rng.Range(3, D - 3), cz = rng.Range(3, D - 3);
                for (int i = rng.Range(4, 10); i > 0; i--)
                {
                    int x = Mathf.Clamp(cx + rng.Range(-4, 5), 1, D - 2);
                    int zz = Mathf.Clamp(cz + rng.Range(-4, 5), 1, D - 2);
                    if (!OpenCell(x, zz) || Void(x, zz) || used[x, zz] || reserved[x, zz] || !FreeStanding(x, zz)) continue;
                    var cellZone = zones[zone[x, zz]];
                    if (cellZone.Water <= 0f || Mathf.Abs(cellZone.Sunk) > 0.01f) continue;  // only level flooded floor
                    PoolPillar(x, zz, HeightAt(x, zz), Range(0.24f, 0.40f));
                    used[x, zz] = true;
                }
            }

            int PoolSideKey(int x, int zz, int dir) => dir == 0 ? Key(x + 1, zz, x + 1, zz + 1)
                : dir == 1 ? Key(x, zz + 1, x + 1, zz + 1)
                : dir == 2 ? Key(x, zz, x, zz + 1)
                           : Key(x, zz, x + 1, zz);

            void PoolLights(List<RectInt> basins)
            {
                for (int k = rng.Range(6, 12); k > 0; k--)
                {
                    int x, zz;
                    if (basins.Count > 0 && rng.Value() < 0.4f)
                    {
                        var b = basins[rng.Range(0, basins.Count)];
                        x = rng.Range(b.xMin + 1, b.xMax - 1);
                        zz = rng.Range(b.yMin + 1, b.yMax - 1);
                    }
                    else
                    {
                        x = rng.Range(1, D - 1);
                        zz = rng.Range(1, D - 1);
                    }
                    if (!OpenCell(x, zz) || Void(x, zz)) continue;
                    int dir = rng.Range(0, 4);
                    var n = new Vector3(Dx[dir], 0f, Dz[dir]);
                    var at = CellCenter(x, zz);
                    float height = HeightAt(x, zz);
                    float floorY = -SunkAt(x, zz);
                    bool ceiling = rng.Value() < 0.28f;
                    if (ceiling)
                    {
                        // A bare light hung in open water, with no fixture or visible source. Ceiling lights stay
                        // at world ceiling height even in a basin; only wall fixtures follow the floor down.
                        at.y = height - 0.08f;
                    }
                    else
                    {
                        if (!edges.TryGetValue(PoolSideKey(x, zz, dir), out var edge) || !Blocking(edge.Kind)) continue;
                        at += n * (C * 0.5f - 0.06f);
                        at.y = floorY + Range(0.7f, Mathf.Min(2.8f, height - floorY - 0.7f));
                    }
                    panels.Add(new PanelSpec { Position = at, State = FixtureState.Lit, Emitter = true, Bare = true,
                        Range = Range(4.5f, 7.5f), Tint = new Color(0.78f, 0.94f, 1f),
                        Id = (int)WorldHash.Hash(seed, x, zz, 0x3711 + dir) });
                }
            }

            /// Clears every internal edge of a rectangle so it becomes one open volume.
            void ClearInternalEdges(RectInt r)
            {
                for (int x = r.xMin; x <= r.xMax; x++)
                for (int zz = r.yMin; zz < r.yMax; zz++) edges.Remove(Key(x, zz, x, zz + 1));
                for (int x = r.xMin; x < r.xMax; x++)
                for (int zz = r.yMin; zz <= r.yMax; zz++) edges.Remove(Key(x, zz, x + 1, zz));
            }

            /// True when no cell of a rectangle (or its rim) is already taken by another feature.
            bool PoolFree(RectInt r)
            {
                for (int x = r.xMin - 1; x <= r.xMax; x++)
                for (int zz = r.yMin - 1; zz <= r.yMax; zz++)
                {
                    int cx = Mathf.Clamp(x, 0, D - 1), cz = Mathf.Clamp(zz, 0, D - 1);
                    if (used[cx, cz] || reserved[cx, cz]) return false;
                }
                return true;
            }

            /// A round tiled pillar rising to `ceiling` (world height). LowerSunkContents drops props in a basin
            /// by the zone's Sunk, so the pose stays at floor level here; doing both would bury it twice.
            /// The cylinder is rotated 90 degrees around z, so its length runs along pose-local +y and its
            /// radius along pose-local x/z. Scale those two radius axes only; scaling y would stretch the height.
            void PoolPillar(int x, int zz, float ceiling, float radius)
            {
                float wide = radius / 0.32f;
                props.Add(new PropSpec { Kind = PropKind.RoundPillar, Variant = 1,
                    Pose = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.identity, new Vector3(wide, 1f, wide)),
                    Ceiling = ceiling });
            }

            /// A tiled staircase from the shallow floor into a basin: 0.2 m risers, 0.3 m treads.
            void PoolStair(RectInt r, float depth, int side, int along, SurfaceRef tile)
            {
                int steps = Mathf.RoundToInt(depth / 0.2f);
                for (int i = 0; i < steps; i++)
                {
                    float top = -depth + (i + 1) * 0.2f;
                    float run = (steps - 1 - i) * 0.3f + 0.15f;   // 0 at the shallow lip
                    Vector3 c;
                    Vector3 size;
                    if (side == 0)       // north
                    { c = origin + new Vector3((along + 0.5f) * C, (-depth + top) / 2f, r.yMin * C + run); size = new Vector3(1.8f, top + depth, 0.3f); }
                    else if (side == 1)  // south
                    { c = origin + new Vector3((along + 0.5f) * C, (-depth + top) / 2f, r.yMax * C - run); size = new Vector3(1.8f, top + depth, 0.3f); }
                    else if (side == 2)  // west
                    { c = origin + new Vector3(r.xMin * C + run, (-depth + top) / 2f, (along + 0.5f) * C); size = new Vector3(0.3f, top + depth, 1.8f); }
                    else                 // east
                    { c = origin + new Vector3(r.xMax * C - run, (-depth + top) / 2f, (along + 0.5f) * C); size = new Vector3(0.3f, top + depth, 1.8f); }
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(c, size), Faces = BoxMeshBuilder.Faces.All & ~BoxMeshBuilder.Faces.NegY,
                        PosY = tile, NegY = tile, PosX = tile, NegX = tile, PosZ = tile, NegZ = tile, Solid = true });
                }
            }
        }
    }
}
