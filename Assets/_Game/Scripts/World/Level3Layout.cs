using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 3 "Electrical Station" (Backrooms Wiki, https://backrooms-wiki.wikidot.com/level-3 , CC BY-SA 3.0):
    /// brick walls of uneven colour over dusty concrete; hallways that swing from crawl-sized squeezes to halls the size
    /// of a food court; generator rooms, assembly lines (where the supplies are), boiler rooms leaking a scalding black
    /// sludge; hotspots where you hear crying; rooms sealed behind bars with no gate; and, rarely, a Sanctum: a pillared
    /// cathedral with an angel statue that nothing hostile will enter. Lit by bare, flickering bulbs.
    ///
    /// District kinds: ElectricalStation = the maintenance tunnels, GeneratorHall, AssemblyLine, BoilerWorks.
    public static partial class DistrictLayout
    {
        sealed partial class Plan
        {
            readonly List<SpecialSpec> specials = new List<SpecialSpec>();
            readonly List<RectInt> cages = new List<RectInt>();
            int sanctumZone = -1;

            const float SqueezeHeight = 1.5f, SqueezeGap = 0.9f, SanctumChance = 0.22f;
            const int WallBrick = 0, WallConcrete = 1, WallHall = 2, WallPlaster = 3;
            static readonly Color BulbTint = new Color(1f, 0.8f, 0.55f), SanctumTint = new Color(1f, 0.72f, 0.42f);

            static int Mod(int a, int m) => (a % m + m) % m;
            SurfaceRef L3Wall(int i) => new SurfaceRef(Surface.Wall, Mathf.Min(i, wallpapers - 1));

            void Special(SpecialKind kind, Matrix4x4 pose, Vector3 size)
            {
                Vector3 p = pose.GetColumn(3);
                specials.Add(new SpecialSpec { Kind = kind, Pose = pose, Size = size, Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(p.x * 4f), Mathf.RoundToInt(p.z * 4f), 0x3A0 + (int)kind) });
            }

            // ---------- the maintenance tunnels (and, with `boiler`, the boiler works) ----------

            /// A maze of brick service tunnels: a winding spanning tree over the district's cells, so every wall the
            /// reachability rule sees is legal and no doorway is forced. Straight stretches become squeezes; a few
            /// small rooms; rarely a Sanctum hidden at the end of its own squeeze.
            void BuildMaintenance(bool boiler)
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Corridor, all);
                var z = zones[id];
                z.Height = boiler ? 3.2f : Snap(Range(2.3f, 2.8f));
                z.Bulbs = true;
                z.Baseboard = z.ChairRail = false;
                z.Wall = theme.id == "5" ? L3Wall(3) : L3Wall(boiler ? WallConcrete : rng.Value() < 0.8f ? WallBrick : WallConcrete);   // the hotel's boiler room: brick
                z.Floor = new SurfaceRef(Surface.Concrete);
                z.Dead = boiler ? 0.45f : 0.25f;
                Paint(all, id);

                var open = new HashSet<int>();
                var seen = new bool[D, D];
                var squeezeCells = new List<(Vector2Int cell, bool alongX)>();
                RectInt sanctum = default;
                int entrance = -1;
                if (!boiler && rng.Value() < SanctumChance) PlanSanctum(open, seen, squeezeCells, out sanctum, out entrance);

                // Rooms first: a few rectangles with no inner walls (workshops, fuse rooms, boiler rooms).
                var rooms = new List<RectInt>();
                for (int r = boiler ? rng.Range(3, 6) : rng.Range(1, 4); r > 0; r--)
                {
                    int w = boiler ? rng.Range(3, 6) : rng.Range(2, 4), d = boiler ? rng.Range(3, 6) : rng.Range(2, 4);
                    int x0 = rng.Range(2, D - 2 - w), z0 = rng.Range(2, D - 2 - d);
                    var room = new RectInt(x0, z0, w, d);
                    bool clear = true;
                    for (int x = x0 - 1; x <= x0 + w && clear; x++)
                    for (int zz = z0 - 1; zz <= z0 + d && clear; zz++)
                        if (!Allowed(x, zz) || seen[x, zz]) clear = false;
                    if (!clear) continue;
                    rooms.Add(room);
                    for (int x = x0; x < x0 + w; x++)
                    for (int zz = z0; zz < z0 + d; zz++)
                    {
                        if (x + 1 < x0 + w) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x + 1, zz)));
                        if (zz + 1 < z0 + d) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x, zz + 1)));
                    }
                }

                // The spanning tree, one walk per connected patch of this region's cells.
                for (int sx = 0; sx < D; sx++)
                for (int sz = 0; sz < D; sz++)
                {
                    if (seen[sx, sz] || !Allowed(sx, sz)) continue;
                    var stack = new Stack<(Vector2Int c, int dir)>();
                    seen[sx, sz] = true;
                    stack.Push((new Vector2Int(sx, sz), rng.Range(0, 4)));
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
                        int dir = options.Contains(lastDir) && rng.Value() < 0.7f ? lastDir : options[rng.Range(0, options.Count)];
                        var n = new Vector2Int(c.x + Dx[dir], c.y + Dz[dir]);
                        open.Add(EdgeBetween(c, n));
                        seen[n.x, n.y] = true;
                        stack.Push((n, dir));
                    }
                }

                // Straight stretches (exactly two open sides, opposite) of three or more cells: some become squeezes.
                bool IsOpen(int x, int zz, int s) => x + Dx[s] >= 0 && zz + Dz[s] >= 0 && x + Dx[s] < D && zz + Dz[s] < D
                                                    && open.Contains(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x + Dx[s], zz + Dz[s])));
                bool InRoom(int x, int zz) { foreach (var r in rooms) if (r.Contains(new Vector2Int(x, zz))) return true; return false; }
                bool Straight(int x, int zz, bool alongX) =>
                    Allowed(x, zz) && !InRoom(x, zz) && zone[x, zz] == id && x > 0 && zz > 0 && x < D - 1 && zz < D - 1
                    && (alongX ? IsOpen(x, zz, 0) && IsOpen(x, zz, 2) && !IsOpen(x, zz, 1) && !IsOpen(x, zz, 3)
                               : IsOpen(x, zz, 1) && IsOpen(x, zz, 3) && !IsOpen(x, zz, 0) && !IsOpen(x, zz, 2));
                int squeezes = 0, maxSqueezes = boiler ? 1 : rng.Range(1, 4);
                foreach (bool alongX in new[] { true, false })
                    for (int line = 1; line < D - 1 && squeezes < maxSqueezes; line++)
                    {
                        int run = 0;
                        for (int a = 1; a < D; a++)
                        {
                            int x = alongX ? a : line, zz = alongX ? line : a;
                            if (a < D - 1 && Straight(x, zz, alongX)) { run++; continue; }
                            if (run >= 3 && squeezes < maxSqueezes && rng.Value() < 0.35f)
                            {
                                squeezes++;
                                int len = Mathf.Min(run, 5);
                                for (int k = 1; k <= len; k++) squeezeCells.Add((alongX ? new Vector2Int(a - k, line) : new Vector2Int(line, a - k), alongX));
                            }
                            run = 0;
                        }
                    }

                // Every other edge is a wall.
                for (int x = 0; x < D; x++)
                for (int zz = 0; zz < D; zz++)
                {
                    var c = new Vector2Int(x, zz);
                    if (!Allowed(x, zz)) continue;
                    if (x + 1 < D && !open.Contains(EdgeBetween(c, new Vector2Int(x + 1, zz)))) Wall(x + 1, zz, x + 1, zz + 1, EdgeKind.Doorway);
                    if (zz + 1 < D && !open.Contains(EdgeBetween(c, new Vector2Int(x, zz + 1)))) Wall(x, zz + 1, x + 1, zz + 1, EdgeKind.Doorway);
                }

                Squeezes(squeezeCells, z.Wall);
                foreach (var room in rooms) MaintenanceRoom(room, z, boiler);
                if (entrance >= 0) FurnishSanctum(sanctum, entrance);
                DressMaintenance(id, boiler);
            }

            /// Squeeze cells: a 1.5 m ceiling, brick narrowing the passage to 0.9 m. You crawl through on your knees.
            void Squeezes(List<(Vector2Int cell, bool alongX)> cells, SurfaceRef wall)
            {
                if (cells.Count == 0) return;
                int sid = NewZone(ZoneKind.Corridor, new RectInt(cells[0].cell.x, cells[0].cell.y, 1, 1));
                var sq = zones[sid];
                sq.Height = SqueezeHeight;
                sq.Dead = 1f;
                sq.Bulbs = true;
                sq.Baseboard = sq.ChairRail = false;
                sq.Wall = wall;
                sq.Floor = new SurfaceRef(Surface.Concrete);
                float slab = (C - T - SqueezeGap) / 2f;
                foreach (var (c, alongX) in cells)
                {
                    zone[c.x, c.y] = sid;
                    used[c.x, c.y] = true;
                    noLights.Add(c);
                    var mid = CellCenter(c.x, c.y);
                    foreach (int sign in new[] { -1, 1 })
                    {
                        float off = sign * (C / 2f - T / 2f - slab / 2f);
                        var center = mid + (alongX ? new Vector3(0f, SqueezeHeight / 2f, off) : new Vector3(off, SqueezeHeight / 2f, 0f));
                        var size = alongX ? new Vector3(C, SqueezeHeight, slab) : new Vector3(slab, SqueezeHeight, C);
                        extraBoxes.Add(SurfaceBox.Uniform(new Bounds(center, size), wall, BoxMeshBuilder.Faces.Sides, true));
                    }
                }
            }

            /// Workshops, fuse rooms and store rooms in the tunnels; in the boiler works, the boiler rooms themselves.
            void MaintenanceRoom(RectInt r, Zone corridor, bool boiler)
            {
                int rid = NewZone(ZoneKind.Storage, r);
                var rz = zones[rid];
                rz.Height = boiler ? Snap(Range(3.4f, 4.2f)) : corridor.Height;
                rz.Bulbs = true;
                rz.Wall = corridor.Wall;
                rz.Floor = new SurfaceRef(Surface.Concrete);
                rz.Baseboard = rz.ChairRail = false;
                rz.Dead = boiler ? 0.5f : 0.3f;
                Paint(r, rid);
                var c = origin + new Vector3((r.xMin + r.width / 2f) * C, 0f, (r.yMin + r.height / 2f) * C);
                if (boiler)
                {
                    // Iron boilers rumbling in the heat, sludge pooling under them.
                    for (int i = rng.Range(1, 3); i > 0; i--) PlaceInZone(rid, PropKind.Boiler, true);
                    Special(SpecialKind.MachineHum, Matrix4x4.TRS(c + Vector3.up, Quaternion.identity, Vector3.one), Vector3.zero);
                    if (theme.id == "5") Special(SpecialKind.Heat, Matrix4x4.TRS(c, Quaternion.identity, Vector3.one), new Vector3(r.width * C, 3f, r.height * C));
                    for (int i = rng.Range(1, 3); i > 0; i--)
                    {
                        int x = rng.Range(r.xMin, r.xMax), zz = rng.Range(r.yMin, r.yMax);
                        if (used[x, zz]) continue;
                        used[x, zz] = true;
                        SludgePool(CellCenter(x, zz));
                    }
                    panels.Add(new PanelSpec { Position = new Vector3(c.x, 0.012f, c.z), AlongX = true, State = FixtureState.Lit, Emitter = true, Range = 5f, Floor = true,
                                               Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.z), 0xB31) });   // the firebox glow
                    return;
                }
                switch (rng.Range(0, 3))
                {
                    case 0:   // a workshop: benches, shelves, a battery left behind
                        for (int i = rng.Range(1, 3); i > 0; i--) PlaceOnWall(rid, PropKind.Desk);
                        PlaceInZone(rid, PropKind.ShelfRack, true);
                        PlaceInZone(rid, PropKind.Battery, false);
                        break;
                    case 1:   // a fuse room: breaker panels on the walls, one sparking
                        for (int i = rng.Range(2, 4); i > 0; i--) PlaceOnWall(rid, PropKind.BreakerPanel);
                        PlaceOnWall(rid, PropKind.OddOutlet);
                        if (rng.Value() < 0.5f) PlaceInZone(rid, PropKind.Battery, false);
                        break;
                    default:   // a store room
                        for (int i = rng.Range(2, 5); i > 0; i--) PlaceInZone(rid, PropKind.CardboardBox, true);
                        PlaceInZone(rid, PropKind.Bucket, false);
                        if (rng.Value() < 0.5f) PlaceInZone(rid, PropKind.AlmondWater, false);
                        break;
                }
            }

            void SludgePool(Vector3 at)
            {
                if (theme.id == "5")   // the hotel's boiler room leaks almond water, not sludge
                {
                    props.Add(new PropSpec { Kind = PropKind.Puddle, Variant = 0, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 180f, 0f), Vector3.one) });
                    return;
                }
                props.Add(new PropSpec { Kind = PropKind.SludgePool, Variant = rng.Range(0, 1000), Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, rng.Value() * 180f, 0f), Vector3.one) });
                Special(SpecialKind.Sludge, Matrix4x4.TRS(at, Quaternion.identity, Vector3.one), new Vector3(1.9f, 0.6f, 1.9f));
            }

            /// Pipes, conduit and panels on the tunnel walls; sludge leaking from the pipes; crying in the dead ends.
            void DressMaintenance(int id, bool boiler)
            {
                var keys = new List<int>(edges.Keys);
                keys.Sort();
                foreach (int key in keys)
                {
                    if (edges[key].Kind != EdgeKind.Wall) continue;
                    foreach (bool side in new[] { true, false })
                    {
                        if (!WallFrame(key, side, out var pose, out var cell) || Void(cell.x, cell.y) || zone[cell.x, cell.y] != id || used[cell.x, cell.y]) continue;
                        float r = rng.Value(), h = HeightAt(cell.x, cell.y);
                        if (r < (boiler ? 0.03f : 0.008f) && theme.id == "5")   // the hotel: plain hot pipes, dripping water
                        {
                            props.Add(new PropSpec { Kind = PropKind.WallPipes, Variant = rng.Range(0, 8), Pose = pose, Ceiling = h });
                            if (rng.Value() < 0.4f) SludgePool(pose.MultiplyPoint3x4(new Vector3(0f, 0f, 0.7f)));
                        }
                        else if (r < (boiler ? 0.03f : 0.008f))
                        {
                            used[cell.x, cell.y] = true;
                            props.Add(new PropSpec { Kind = PropKind.SludgePipe, Pose = pose, Ceiling = h });
                            Special(SpecialKind.Pipe, pose, Vector3.zero);
                            if (rng.Value() < 0.3f) SludgePool(pose.MultiplyPoint3x4(new Vector3(-0.45f, 0f, 0.8f)));
                        }
                        else if (r < 0.3f)
                        {
                            Decode(key, out int ex, out int ez, out int edir);
                            int line = edir == 0 ? (dz * D + ez) * 2 : (dx * D + ex) * 2 + 1;   // the same conduit all along the wall line
                            props.Add(new PropSpec { Kind = PropKind.WallPipes, Variant = (int)(WorldHash.Hash(seed, line, side ? 1 : 0, 0x93) & 7), Pose = pose, Ceiling = h });
                        }
                        else if (r < 0.305f) { used[cell.x, cell.y] = true; props.Add(new PropSpec { Kind = PropKind.BreakerPanel, Pose = pose, Ceiling = h }); }
                        else if (r < 0.32f) props.Add(new PropSpec { Kind = rng.Value() < 0.5f ? PropKind.OddOutlet : PropKind.Outlet, Pose = pose, Ceiling = h, Variant = rng.Range(0, 12) });
                    }
                }
                // Dead ends: hotspots. You hear someone crying for help; there's a warning chalked on the wall.
                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                {
                    if (zone[x, zz] != id || used[x, zz] || Void(x, zz)) continue;
                    int walls = 0, wallSide = -1;
                    for (int s = 0; s < 4; s++)
                    {
                        int k = s == 0 ? Key(x + 1, zz, x + 1, zz + 1) : s == 1 ? Key(x, zz + 1, x + 1, zz + 1) : s == 2 ? Key(x, zz, x, zz + 1) : Key(x, zz, x + 1, zz);
                        if (edges.TryGetValue(k, out var e) && Blocking(e.Kind)) { walls++; if (wallSide < 0 || rng.Value() < 0.5f) wallSide = s; }
                    }
                    if (walls < 3) continue;
                    float roll = rng.Value();
                    var c = CellCenter(x, zz);
                    if (roll < 0.06f)
                    {
                        used[x, zz] = true;
                        Special(SpecialKind.Hotspot, Matrix4x4.TRS(c, Quaternion.identity, Vector3.one), new Vector3(C * 2.5f, 2.6f, C * 2.5f));
                        int wk = wallSide == 0 ? Key(x + 1, zz, x + 1, zz + 1) : wallSide == 1 ? Key(x, zz + 1, x + 1, zz + 1) : wallSide == 2 ? Key(x, zz, x, zz + 1) : Key(x, zz, x + 1, zz);
                        if (theme.id != "5" && rng.Value() < 0.6f && WallFrame(wk, wallSide == 2 || wallSide == 3, out var wp, out _))
                            readables.Add(new ReadableSpec { Kind = ReadableKind.Writing, Pose = wp * Matrix4x4.Translate(new Vector3(0f, 1.45f, 0f)), Title = "Chalk on the bricks",
                                Text = "HOTSPOT\nYou'll hear someone. There is nobody.\nDO NOT STOP HERE." });
                    }
                    else if (roll < 0.14f && boiler) { used[x, zz] = true; SludgePool(c); }
                }
            }

            // ---------- the Sanctum ----------

            /// Reserves a Sanctum (6-7 x 8-9 cells) with a 3-4 cell squeeze as its only way in, before the tunnels are dug.
            bool PlanSanctum(HashSet<int> open, bool[,] seen, List<(Vector2Int, bool)> squeezeCells, out RectInt r, out int side)
            {
                for (int attempt = 0; attempt < 14; attempt++)
                {
                    bool longX = rng.Value() < 0.5f;
                    int w = longX ? rng.Range(8, 10) : rng.Range(6, 8), d = longX ? rng.Range(6, 8) : rng.Range(8, 10), len = rng.Range(3, 5);
                    int x0 = rng.Range(2, D - 2 - w), z0 = rng.Range(2, D - 2 - d);
                    r = new RectInt(x0, z0, w, d);
                    side = longX ? (rng.Value() < 0.5f ? 2 : 0) : (rng.Value() < 0.5f ? 3 : 1);   // enter at a short end: the long view to the angel
                    // The squeeze leaves from the middle of that side; the cell past its far end must be ordinary tunnel.
                    var start = side switch
                    {
                        0 => new Vector2Int(x0 + w, z0 + d / 2), 2 => new Vector2Int(x0 - 1, z0 + d / 2),
                        1 => new Vector2Int(x0 + w / 2, z0 + d), _ => new Vector2Int(x0 + w / 2, z0 - 1),
                    };
                    int sdx = side == 0 ? 1 : side == 2 ? -1 : 0, sdz = side == 1 ? 1 : side == 3 ? -1 : 0;
                    var beyond = new Vector2Int(start.x + sdx * len, start.y + sdz * len);
                    bool ok = beyond.x >= 1 && beyond.y >= 1 && beyond.x < D - 1 && beyond.y < D - 1 && Allowed(beyond.x, beyond.y);
                    for (int x = x0 - 1; x <= x0 + w && ok; x++)
                    for (int zz = z0 - 1; zz <= z0 + d && ok; zz++)
                        if (!Allowed(x, zz) || seen[x, zz]) ok = false;
                    for (int k = 0; k < len && ok; k++)
                    {
                        int x = start.x + sdx * k, zz = start.y + sdz * k;
                        if (!Allowed(x, zz) || seen[x, zz] || x < 1 || zz < 1 || x >= D - 1 || zz >= D - 1) ok = false;
                    }
                    if (!ok) continue;

                    for (int x = x0; x < x0 + w; x++)
                    for (int zz = z0; zz < z0 + d; zz++)
                    {
                        seen[x, zz] = true;
                        if (x + 1 < x0 + w) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x + 1, zz)));
                        if (zz + 1 < z0 + d) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x, zz + 1)));
                    }
                    var prev = new Vector2Int(start.x - sdx, start.y - sdz);   // the Sanctum cell it leaves from
                    for (int k = 0; k <= len; k++)
                    {
                        var cell = new Vector2Int(start.x + sdx * k, start.y + sdz * k);
                        open.Add(EdgeBetween(prev, cell));
                        if (k < len) { seen[cell.x, cell.y] = true; squeezeCells.Add((cell, sdx != 0)); }
                        prev = cell;
                    }
                    return true;
                }
                r = default;
                side = -1;
                return false;
            }

            /// The cathedral: plaster walls nearly eight metres up, two rows of columns, pews, and at the far end the angel on its
            /// plinth, lit warm from below. Hunts end at the threshold.
            void FurnishSanctum(RectInt r, int side)
            {
                int sid = NewZone(ZoneKind.Hall, r);
                sanctumZone = sid;
                var s = zones[sid];
                s.Height = 7.8f;
                s.Wall = L3Wall(WallPlaster);
                s.Floor = new SurfaceRef(Surface.Tile);
                s.Dead = 0f;
                s.Bulbs = true;
                s.Baseboard = true;
                s.ChairRail = false;
                Paint(r, sid);
                for (int x = r.xMin; x < r.xMax; x++) for (int zz = r.yMin; zz < r.yMax; zz++) used[x, zz] = true;

                // Along the long axis, from the squeeze (u = 0) to the angel (u = 1).
                bool alongX = side == 0 || side == 2;
                var lo = origin + new Vector3(r.xMin * C, 0f, r.yMin * C);
                float width = alongX ? r.height * C : r.width * C, length = alongX ? r.width * C : r.height * C;
                bool fromMax = side == 0 || side == 1;   // the squeeze is on the max side: the angel stands at the min end
                Vector3 At(float u, float v)
                {
                    float a = fromMax ? length - u : u;
                    return lo + (alongX ? new Vector3(a, 0f, v) : new Vector3(v, 0f, a));
                }
                float yawToEntrance = side switch { 0 => 90f, 2 => 270f, 1 => 0f, _ => 180f };   // the statue faces the way in

                for (float u = 3f; u < length - 3.5f; u += 3f)   // columns
                    foreach (float v in new[] { 2.2f, width - 2.2f })
                        props.Add(new PropSpec { Kind = PropKind.RoundPillar, Pose = Matrix4x4.TRS(At(u, v), Quaternion.identity, Vector3.one), Ceiling = s.Height });
                for (float u = 4f; u < length - 6f; u += 1.8f)   // pews, both sides of the aisle
                    foreach (float v in new[] { width / 2f - 1.7f, width / 2f + 1.7f })
                        props.Add(new PropSpec { Kind = PropKind.Bench, Pose = Matrix4x4.TRS(At(u, v), Quaternion.Euler(0f, yawToEntrance + 180f, 0f), Vector3.one), Ceiling = s.Height });

                var plinth = At(length - 2.6f, width / 2f);
                props.Add(new PropSpec { Kind = PropKind.Plinth, Pose = Matrix4x4.TRS(plinth, Quaternion.Euler(0f, yawToEntrance, 0f), Vector3.one), Ceiling = s.Height });
                Special(SpecialKind.Angel, Matrix4x4.TRS(plinth + Vector3.up * 0.9f, Quaternion.Euler(0f, yawToEntrance, 0f), Vector3.one), Vector3.zero);
                var mid = At(length / 2f, width / 2f);
                Special(SpecialKind.Sanctum, Matrix4x4.TRS(mid, Quaternion.Euler(0f, alongX ? 0f : 90f, 0f), Vector3.one), new Vector3(length - 0.6f, 5f, width - 0.6f));

                // The glow: warm light washing up the statue, and low lights along the columns like candles.
                int n = 0;
                void Glow(Vector3 p, float range) => panels.Add(new PanelSpec { Position = p, State = FixtureState.Lit, Emitter = true, Bare = true, Range = range, Tint = SanctumTint,
                                                                                 Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(p.x * 4f), Mathf.RoundToInt(p.z * 4f), 0x5A0 + n++) });
                Glow(At(length - 4.2f, width / 2f) + Vector3.up * 0.4f, 12f);
                Glow(At(length - 4.6f, width / 2f) + Vector3.up * 2.8f, 7f);   // a soft key on the statue's face
                Glow(At(length - 6.5f, width / 2f) + Vector3.up * 5.2f, 11f);   // in front of the statue, so its face catches the light
                for (float u = 3f; u < length - 3.5f; u += 6f)
                    foreach (float v in new[] { 2.2f, width - 2.2f }) Glow(At(u, v + (v < width / 2f ? 0.7f : -0.7f)) + Vector3.up * 1.1f, 5f);

                readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(At(4.6f, width / 2f - 1.7f) + Vector3.up * 0.47f, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one),
                    Title = "A note left on a pew",
                    Text = "Third one of these I've found. Same statue every time, same pews, same warm light with no bulb I can find.\n\n" +
                           "The thing that followed me through the pipe rooms stopped at the crawlway and screamed like it had touched a stove. " +
                           "It didn't come in. I slept here. First real sleep in days.\n\nIf you need to rest, rest here. Don't stay forever." });
            }

            // ---------- generator rooms ----------

            /// A hall the size of a food court: rows of diesel generators under cable trays, transformers caged behind
            /// bars, columns fading into the haze. Loud.
            void BuildGeneratorHall()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Hall, all);
                var z = zones[id];
                z.Height = 7.6f;
                z.Bulbs = true;
                z.Baseboard = z.ChairRail = false;
                z.Wall = L3Wall(rng.Value() < 0.6f ? WallBrick : WallHall);
                z.Floor = new SurfaceRef(Surface.Concrete);
                z.Dead = 0.3f;
                Paint(all, id);

                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                    if (Mod(lx * D + x, 8) == 6 && Mod(lz * D + zz, 8) == 6 && Allowed(x, zz)) Pillar(x, zz, false, 1.1f);

                for (int zz = 2; zz < D - 2; zz++)
                {
                    if (Mod(lz * D + zz, 8) != 3) continue;
                    for (int x = 1; x < D - 1; x++)   // cable trays over the whole row
                        if (Allowed(x, zz) && OpenCell(x, zz))
                            props.Add(new PropSpec { Kind = PropKind.CableTray, Variant = x + zz, Pose = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.identity, Vector3.one), Ceiling = z.Height });
                    for (int x = 1; x < D - 2; x++)
                    {
                        if (Mod(lx * D + x, 6) != 2 || !Allowed(x, zz) || !Allowed(x + 1, zz) || !FreeStanding(x, zz) || !FreeStanding(x + 1, zz)) continue;
                        var at = CellCenter(x, zz) + new Vector3(C / 2f, 0f, 0f);
                        float roll = rng.Value();
                        if (roll < 0.08f) continue;   // an empty plinth of oil stains where one was taken out
                        used[x, zz] = used[x + 1, zz] = true;
                        if (roll < 0.22f)
                        {
                            // A transformer in its pen: bars on all four sides, no gate.
                            Put(PropKind.Transformer, at, rng.Value() < 0.5f ? 0f : 180f, 0, z.Height, true, new Vector3(1.8f, 2.2f, 1.4f));
                            foreach (float sz in new[] { -1f, 1f })
                                foreach (float sx in new[] { -1f, 1f })
                                    Put(PropKind.BarFence, at + new Vector3(sx, 0f, sz), 0f, 0, z.Height, true, new Vector3(2f, 3f, 0.15f));
                            foreach (float sx in new[] { -2f, 2f })
                                Put(PropKind.BarFence, at + new Vector3(sx, 0f, 0f), 90f, 0, z.Height, true, new Vector3(2f, 3f, 0.15f));
                            Special(SpecialKind.MachineHum, Matrix4x4.TRS(at + Vector3.up * 1.5f, Quaternion.identity, Vector3.one), Vector3.zero);
                            continue;
                        }
                        Put(PropKind.Generator, at, rng.Value() < 0.5f ? 0f : 180f, rng.Range(0, 12), z.Height, true, new Vector3(4f, 2.6f, 1.95f));
                        if (Mod(lx * D + x, 12) == 2 && rng.Value() < 0.8f)   // one drone per pair, or the hall turns to mush
                            Special(SpecialKind.MachineHum, Matrix4x4.TRS(at + Vector3.up, Quaternion.identity, Vector3.one), Vector3.zero);
                    }
                }
                for (int k = rng.Range(2, 5); k > 0; k--)
                {
                    int x = rng.Range(2, D - 2), zz = rng.Range(2, D - 2);
                    if (FreeStanding(x, zz) && Allowed(x, zz)) { used[x, zz] = true; Put(PropKind.OilStain, CellCenter(x, zz), rng.Value() * 360f, rng.Range(0, 100), z.Height, false); }
                }
                if (rng.Value() < 0.5f) CageRoom(id);
            }

            // ---------- assembly lines ----------

            /// Long conveyor lines, still running, carrying nothing in particular; workbenches and shelves of parts.
            /// The main source of supplies in the level (the wiki says so, and so do the entities that gather here).
            void BuildAssemblyLine()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Hall, all);
                var z = zones[id];
                z.Height = 5.2f;
                z.Bulbs = true;
                z.Baseboard = z.ChairRail = false;
                z.Wall = L3Wall(rng.Value() < 0.5f ? WallHall : WallBrick);
                z.Floor = new SurfaceRef(Surface.Concrete);
                z.Dead = 0.12f;
                Paint(all, id);

                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                    if (Mod(lx * D + x, 8) == 0 && Mod(lz * D + zz, 8) == 0 && Allowed(x, zz)) Pillar(x, zz, false, 0.8f);

                for (int zz = 2; zz < D - 2; zz++)
                {
                    if (Mod(lz * D + zz, 5) != 2) continue;
                    var run = new List<int>();
                    for (int x = 1; x < D - 1; x++)
                    {
                        bool gap = Mod(lx * D + x, 8) == 4;   // a crossing every 16 m
                        if (!gap && Allowed(x, zz) && FreeStanding(x, zz)) { run.Add(x); continue; }
                        ConveyorRun(run, zz, z.Height);
                        run.Clear();
                    }
                    ConveyorRun(run, zz, z.Height);
                }
                for (int i = rng.Range(2, 5); i > 0; i--) PlaceOnWall(id, PropKind.Desk);
                for (int i = rng.Range(1, 4); i > 0; i--) PlaceInZone(id, PropKind.ShelfRack, true);
                for (int i = rng.Range(3, 7); i > 0; i--) PlaceInZone(id, PropKind.CardboardBox, true);
                for (int i = rng.Range(1, 4); i > 0; i--) PlaceInZone(id, PropKind.Battery, false);
                for (int i = rng.Range(1, 3); i > 0; i--) PlaceInZone(id, PropKind.AlmondWater, false);
                if (rng.Value() < 0.4f) CageRoom(id);
            }

            void ConveyorRun(List<int> xs, int zz, float h)
            {
                if (xs.Count < 3) return;
                for (int i = 0; i < xs.Count; i++)
                {
                    int x = xs[i];
                    used[x, zz] = true;
                    var at = CellCenter(x, zz);
                    Put(PropKind.Conveyor, at, 90f, 0, h, true, new Vector3(2f, 1.1f, 1.05f));
                    if (rng.Value() < 0.3f)   // something riding the belt
                        props.Add(new PropSpec { Kind = PropKind.CardboardBox, Variant = rng.Range(0, 12), Pose = Matrix4x4.TRS(at + new Vector3((rng.Value() - 0.5f) * 1.2f, 0.85f, 0f), Quaternion.Euler(0f, rng.Value() * 30f, 0f), Vector3.one), Ceiling = h });
                }
                var end = CellCenter(xs[xs.Count - 1], zz);
                Put(PropKind.ConveyorMotor, end, 90f, 0, h, false);
                Special(SpecialKind.MachineHum, Matrix4x4.TRS(end + Vector3.up, Quaternion.identity, Vector3.one), Vector3.zero);
            }

            /// A small room in a hall, walled off. If it ends up with a single opening, that opening gets bars with no
            /// gate (decided once the walls are final, in Level3Finish); you see the supplies inside until the power's back.
            void CageRoom(int hall)
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int w = rng.Range(2, 4), d = rng.Range(2, 4), x0 = rng.Range(2, D - 3 - w), z0 = rng.Range(2, D - 3 - d);
                    var r = new RectInt(x0, z0, w, d);
                    bool ok = true;
                    for (int x = x0 - 1; x <= x0 + w && ok; x++)
                    for (int zz = z0 - 1; zz <= z0 + d && ok; zz++)
                        if (!Allowed(x, zz) || !OpenCell(x, zz) || used[x, zz] || zone[x, zz] != hall) ok = false;
                    if (!ok) continue;
                    int rid = NewZone(ZoneKind.Storage, r);
                    var cz = zones[rid];
                    cz.Height = 3.2f;
                    cz.Bulbs = true;
                    cz.Wall = zones[hall].Wall;
                    cz.Floor = new SurfaceRef(Surface.Concrete);
                    cz.Dead = 0f;
                    Paint(r, rid);
                    RoomWalls(r, 0, EdgeKind.Wide);
                    cages.Add(r);
                    return;
                }
            }

            /// After every region is built: bars across the single opening of each cage room, and its stash.
            void Level3Finish()
            {
                foreach (var r in cages)
                {
                    var openings = new List<int>();
                    for (int x = r.xMin; x < r.xMax; x++)
                    {
                        openings.Add(Key(x, r.yMin, x + 1, r.yMin));
                        openings.Add(Key(x, r.yMax, x + 1, r.yMax));
                    }
                    for (int zz = r.yMin; zz < r.yMax; zz++)
                    {
                        openings.Add(Key(r.xMin, zz, r.xMin, zz + 1));
                        openings.Add(Key(r.xMax, zz, r.xMax, zz + 1));
                    }
                    openings.RemoveAll(k => !edges.TryGetValue(k, out var e) || !IsOpening(e.Kind));
                    if (openings.Count != 1) continue;   // more than one way in: it's just a room
                    int key = openings[0];
                    edges[key] = new Edge(EdgeKind.Wide, false);
                    Decode(key, out int ex, out int ez, out int dir);
                    var mid = origin + (dir == 0 ? new Vector3((ex + 0.5f) * C, 0f, ez * C) : new Vector3(ex * C, 0f, (ez + 0.5f) * C));
                    Special(SpecialKind.BarGate, Matrix4x4.TRS(mid, Quaternion.Euler(0f, dir == 0 ? 0f : 90f, 0f), Vector3.one), new Vector3(C - T - 0.05f, 3f, 0f));
                    // The stash you can see but not reach, under the one light that still works.
                    var c = origin + new Vector3((r.xMin + r.width / 2f) * C, 0f, (r.yMin + r.height / 2f) * C);
                    props.Add(new PropSpec { Kind = PropKind.Battery, Variant = 1, Pose = Matrix4x4.TRS(c + new Vector3(-0.4f, 0f, 0.2f), Quaternion.identity, Vector3.one) });
                    props.Add(new PropSpec { Kind = PropKind.AlmondWater, Variant = 2, Pose = Matrix4x4.TRS(c + new Vector3(0.3f, 0f, -0.2f), Quaternion.identity, Vector3.one) });
                    props.Add(new PropSpec { Kind = PropKind.Lantern, Pose = Matrix4x4.TRS(c + new Vector3(0.6f, 0f, 0.5f), Quaternion.identity, Vector3.one) });
                    panels.Add(new PanelSpec { Position = c + Vector3.up * 2.4f, State = FixtureState.Lit, Emitter = true, Bare = true, Range = 6f, Tint = BulbTint,
                                               Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.z), 0xCA6) });
                }
            }

            // ---------- lights ----------

            /// Level 3's lights: bare bulbs in wire cages on cords. A loose lattice in the halls, every few metres in the
            /// tunnels; many dead, some flickering. (The Sanctum and the cage rooms place their own.)
            void BulbZone(int id)
            {
                var z = zones[id];
                if (z.Dead >= 0.999f || id == sanctumZone) return;
                bool hall = z.Kind == ZoneKind.Hall;
                for (int x = 0; x < D; x++)
                for (int zz = 0; zz < D; zz++)
                {
                    if (zone[x, zz] != id || noLights.Contains(new Vector2Int(x, zz))) continue;
                    int wx = lx * D + x, wz = lz * D + zz;
                    if (hall ? Mod(wx, 4) != 1 || Mod(wz, 4) != 1 : WorldHash.Value(seed, wx, wz, 0xB0) > 0.24f) continue;
                    if (z.Kind == ZoneKind.Storage && WorldHash.Value(seed, wx, wz, 0xB1) > 0.5f) continue;
                    bool pillar = false;
                    var c = CellCenter(x, zz);
                    foreach (var p in pillars) if (Mathf.Abs(p.box.center.x - c.x) < 0.9f && Mathf.Abs(p.box.center.z - c.z) < 0.9f) { pillar = true; break; }
                    if (pillar) continue;
                    float h = HeightAt(x, zz), roll = WorldHash.Value(seed, wx, wz, 0xB2);
                    var state = roll < z.Dead ? FixtureState.Dead : roll < z.Dead + 0.1f ? FixtureState.Faulty : FixtureState.Lit;
                    int v = (int)(WorldHash.Value(seed, wx, wz, 0xB3) * 1000f);
                    if (state == FixtureState.Dead) v -= v % 4; else if (v % 4 == 0) v++;   // CageBulb draws variant % 4 == 0 dark
                    float drop = PropLibrary.BulbDrop(h, v);
                    props.Add(new PropSpec { Kind = PropKind.CageBulb, Variant = v, Pose = Matrix4x4.TRS(c, Quaternion.identity, Vector3.one), Ceiling = h });
                    panels.Add(new PanelSpec
                    {
                        Position = new Vector3(c.x, h - drop - 0.2f, c.z), State = state, Emitter = true, Bare = true, Tint = BulbTint,
                        Range = hall ? Mathf.Max(12f, h * 1.8f) : 6.5f,
                        Id = (int)WorldHash.Hash(seed, wx, wz, 0xB4),
                    });
                }
            }
        }
    }
}
