using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 5 "The Hotel" (Backrooms Wiki "Level 5 - Terror Hotel", https://backrooms-wiki.wikidot.com/level-5 , CC BY-SA 3.0):
    /// an endless 1930s hotel with 1920s furniture. The Main Hall: mahogany and gold, carpeted corridors, furnished guest
    /// rooms behind numbered doors that follow no pattern (some open and close by themselves), antique copper-cage
    /// elevators. The Beverly Room: a ballroom with a chandelier, a table of drinks and an unfinished game of Mahjong,
    /// and a number of doors that is never the same twice. The Boiler Room: cramped, hot, leaking almond water; deep
    /// inside, the way down to Level 6. Jazz from turntables, candle-style lamps.
    ///
    /// District kinds: HotelSuites = the Main Hall, Ballroom = the Beverly Room, BoilerWorks = the boiler room.
    public static partial class DistrictLayout
    {
        /// The number on the door of "your room" (the level goal). No other door in the world shows it.
        public static int GoalRoomNumber(uint levelSeed) => 100 + (int)(WorldHash.Hash(levelSeed, 5, 5, 0x5005) % 800u);

        sealed partial class Plan
        {
            struct HotelRoom { public RectInt Rect; public int Zone, DoorX, DoorZ, Side; public bool Elevator, HasDoor; }
            readonly List<HotelRoom> hotelRooms = new List<HotelRoom>();
            const int L5Damask = 0, L5Panel = 1, L5Cream = 2;
            static readonly Color LampTint = new Color(1f, 0.78f, 0.5f);

            SurfaceRef L5Wall(int i) => new SurfaceRef(Surface.Wall, Mathf.Min(i, wallpapers - 1));

            int RoomNumber(int wx, int wz)
            {
                int goal = GoalRoomNumber(levelSeed);
                int n = 100 + (int)(WorldHash.Hash(levelSeed, wx, wz, 0x5D00) % 800u);
                return n == goal ? n + 1 : n;
            }

            void Lamp(Vector3 at, float range, float dead, int salt)
            {
                float roll = WorldHash.Value(seed, Mathf.RoundToInt(at.x * 4f), Mathf.RoundToInt(at.z * 4f), salt);
                panels.Add(new PanelSpec { Position = at, Bare = true, Emitter = true, Range = range, Tint = LampTint,
                                           State = roll < dead ? FixtureState.Dead : roll < dead + 0.05f ? FixtureState.Faulty : FixtureState.Lit,
                                           Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(at.x * 4f), Mathf.RoundToInt(at.z * 4f), salt + 1) });
            }

            // ---------- the Main Hall ----------

            /// Corridors on a world grid (every 7 cells along z, every 14 along x) plus a ring round the district, so
            /// district seams always meet corridor to corridor. Between them, bands of guest rooms, each with one door.
            void BuildHotelWing()
            {
                var all = new RectInt(0, 0, D, D);
                int cid = NewZone(ZoneKind.Corridor, all);
                var c = zones[cid];
                c.Height = 3.2f;
                c.Lamps = true;
                c.ChairRail = true;
                c.Baseboard = true;
                c.Trim = new SurfaceRef(Surface.Trim, 1);   // dark wood
                c.Wall = L5Wall(L5Damask);
                c.Floor = new SurfaceRef(Surface.Floor, 0);
                Paint(all, cid);

                bool Corr(int x, int z) => x <= 0 || z <= 0 || x >= D - 1 || z >= D - 1 || Mod(lz * D + z, 7) == 3 || Mod(lx * D + x, 14) == 7;
                var open = new HashSet<int>();
                for (int x = 0; x < D; x++)
                for (int z = 0; z < D; z++)
                {
                    if (!Allowed(x, z) || !Corr(x, z)) continue;
                    if (x + 1 < D && Allowed(x + 1, z) && Corr(x + 1, z)) open.Add(EdgeBetween(new Vector2Int(x, z), new Vector2Int(x + 1, z)));
                    if (z + 1 < D && Allowed(x, z + 1) && Corr(x, z + 1)) open.Add(EdgeBetween(new Vector2Int(x, z), new Vector2Int(x, z + 1)));
                }

                // Room bands: runs of non-corridor rows. 1 row widens the corridor; 2-3 rows face one corridor; 4-6 split in two.
                var bands = new List<(int z0, int depth, int side)>();
                for (int z = 1; z < D - 1;)
                {
                    if (Mod(lz * D + z, 7) == 3) { z++; continue; }
                    int z1 = z;
                    while (z1 < D - 1 && Mod(lz * D + z1, 7) != 3) z1++;
                    int n = z1 - z;
                    if (n == 1) { for (int x = 0; x < D; x++) if (Allowed(x, z)) { if (x + 1 < D) open.Add(EdgeBetween(new Vector2Int(x, z), new Vector2Int(x + 1, z))); open.Add(EdgeBetween(new Vector2Int(x, z - 1), new Vector2Int(x, z))); open.Add(EdgeBetween(new Vector2Int(x, z), new Vector2Int(x, z + 1))); } }
                    else if (n <= 3) bands.Add((z, n, -1));
                    else { int lower = n / 2; bands.Add((z, lower, -1)); bands.Add((z + lower, n - lower, 1)); }
                    z = z1;
                }
                foreach (var (z0, depth, side) in bands)
                {
                    for (int x = 1; x < D - 1;)
                    {
                        bool Fits(int cx) { for (int k = 0; k < depth; k++) if (!Allowed(cx, z0 + k)) return false; return !Corr(cx, z0); }   // only this region's cells
                        if (!Fits(x)) { x++; continue; }
                        int x1 = x;
                        while (x1 < D - 1 && Fits(x1)) x1++;
                        for (int a = x; a < x1;)
                        {
                            int left = x1 - a, w = left <= 4 ? left : rng.Range(2, 4);
                            if (left - w == 1) w++;
                            HotelRoomAt(new RectInt(a, z0, w, depth), side, open);
                            a += w;
                        }
                        x = x1;
                    }
                }

                for (int x = 0; x < D; x++)
                for (int z = 0; z < D; z++)
                {
                    if (!Allowed(x, z)) continue;
                    var cell = new Vector2Int(x, z);
                    if (x + 1 < D && !open.Contains(EdgeBetween(cell, new Vector2Int(x + 1, z)))) Wall(x + 1, z, x + 1, z + 1, EdgeKind.Doorway);
                    if (z + 1 < D && !open.Contains(EdgeBetween(cell, new Vector2Int(x, z + 1)))) Wall(x, z + 1, x + 1, z + 1, EdgeKind.Doorway);
                }
            }

            /// One guest room (or, rarely, an elevator): its door edge is cut before the walls go up.
            void HotelRoomAt(RectInt r, int side, HashSet<int> open)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                {
                    if (x + 1 < r.xMax) open.Add(EdgeBetween(new Vector2Int(x, z), new Vector2Int(x + 1, z)));
                    if (z + 1 < r.yMax) open.Add(EdgeBetween(new Vector2Int(x, z), new Vector2Int(x, z + 1)));
                }
                var h = new HotelRoom { Rect = r, Side = side, DoorX = r.xMin + r.width / 2, DoorZ = side < 0 ? r.yMin : r.yMax - 1 };
                h.Elevator = r.width >= 2 && r.height >= 2 && rng.Value() < 0.018f;
                int nz = h.DoorZ + side, edgeZ = side < 0 ? r.yMin : r.yMax;
                h.HasDoor = OpenCell(h.DoorX, h.DoorZ) && OpenCell(h.DoorX, nz) && Allowed(h.DoorX, h.DoorZ) && Allowed(h.DoorX, nz);
                if (h.HasDoor) Opening(h.DoorX, edgeZ, h.DoorX + 1, edgeZ, h.Elevator ? EdgeKind.Wide : EdgeKind.Doorway);
                int id = NewZone(h.Elevator ? ZoneKind.Room : ZoneKind.HotelRoom, r);
                var z0 = zones[id];
                z0.Height = 3.0f;
                z0.Lamps = true;
                z0.Wall = L5Wall(h.Elevator ? L5Panel : rng.Range(0, 3));
                z0.Floor = h.Elevator ? new SurfaceRef(Surface.Tile) : rng.Value() < 0.6f ? new SurfaceRef(Surface.Floor, rng.Range(0, carpets)) : new SurfaceRef(Surface.Wood);
                z0.Trim = new SurfaceRef(Surface.Trim, 1);
                z0.Baseboard = true;
                z0.ChairRail = rng.Value() < 0.5f;
                z0.Dead = 0f;
                Paint(r, id);
                h.Zone = id;
                hotelRooms.Add(h);
            }

            /// Doors, furniture, elevators: once the walls are final.
            void FinishHotelRooms()
            {
                foreach (var h in hotelRooms)
                {
                    var r = h.Rect;
                    float edgeZ = (h.Side < 0 ? r.yMin : r.yMax) * C;
                    var doorPose = Matrix4x4.TRS(origin + new Vector3((h.DoorX + 0.5f) * C, 0f, edgeZ), Quaternion.Euler(0f, h.Side < 0 ? 180f : 0f, 0f), Vector3.one);
                    int number = RoomNumber(lx * D + h.DoorX, lz * D + h.DoorZ);
                    // The back wall is opposite the door; furniture faces the door.
                    float backZ = (h.Side < 0 ? r.yMax : r.yMin) * C, inward = h.Side < 0 ? -1f : 1f;
                    float midX = (r.xMin + r.width / 2f) * C;
                    var facing = Quaternion.Euler(0f, h.Side < 0 ? 180f : 0f, 0f);   // local +z toward the door
                    Vector3 At(float x, float fromBack) => origin + new Vector3(x, 0f, backZ + inward * fromBack);
                    for (int x = r.xMin; x < r.xMax; x++) for (int z = r.yMin; z < r.yMax; z++) used[x, z] = true;
                    if (h.Elevator)
                    {
                        var car = Matrix4x4.TRS(At(midX, 1.0f), facing, Vector3.one);
                        props.Add(new PropSpec { Kind = PropKind.CageElevator, Pose = car, Ceiling = 3f });
                        Special(SpecialKind.Elevator, car, Vector3.zero);
                        Lamp(At(midX, 1.0f) + Vector3.up * 2.35f, 5f, 0f, 0x5E1);
                        continue;
                    }
                    if (h.HasDoor) specials.Add(new SpecialSpec { Kind = SpecialKind.HotelDoor, Pose = doorPose, Id = number });
                    float depth = r.height * C, width = r.width * C;
                    props.Add(new PropSpec { Kind = PropKind.HotelBed, Pose = Matrix4x4.TRS(At(midX, 1.15f), facing, Vector3.one), Ceiling = 3f });
                    if (width >= 4f)
                        foreach (float sx in new[] { -1.2f, 1.2f })
                            props.Add(new PropSpec { Kind = PropKind.Nightstand, Variant = rng.Range(0, 12), Pose = Matrix4x4.TRS(At(midX + sx, 0.3f), facing, Vector3.one), Ceiling = 3f });
                    float left = r.xMin * C + 0.6f, right = r.xMax * C - 0.6f;
                    if (depth >= 5f && rng.Value() < 0.65f)
                        props.Add(new PropSpec { Kind = PropKind.Armchair, Variant = rng.Range(0, 12), Pose = Matrix4x4.TRS(At(rng.Value() < 0.5f ? left : right, depth - 1.1f), facing * Quaternion.Euler(0f, rng.Value() * 60f - 30f, 0f), Vector3.one), Ceiling = 3f });
                    if (rng.Value() < 0.5f) props.Add(new PropSpec { Kind = PropKind.Rug, Variant = rng.Range(0, 12), Pose = Matrix4x4.TRS(At(midX, Mathf.Min(depth - 1.4f, 2.8f)), facing, Vector3.one) });
                    if (rng.Value() < 0.7f)   // a standard lamp in the corner, often still on
                    {
                        var lampAt = At(rng.Value() < 0.5f ? left : right, 0.45f);
                        props.Add(new PropSpec { Kind = PropKind.FloorLamp, Variant = 0, Pose = Matrix4x4.TRS(lampAt, Quaternion.identity, Vector3.one), Ceiling = 3f });
                        Lamp(lampAt + Vector3.up * 1.55f, 5.5f, 0.35f, 0x5A0);
                    }
                    if (width >= 4f && depth >= 5f && rng.Value() < 0.035f)   // a turntable playing jazz to nobody
                    {
                        var g = Matrix4x4.TRS(At(right - 0.1f, depth - 0.8f), facing * Quaternion.Euler(0f, 180f, 0f), Vector3.one);
                        props.Add(new PropSpec { Kind = PropKind.Gramophone, Pose = g, Ceiling = 3f });
                        var roomMid = origin + new Vector3(midX, 0f, (r.yMin + r.height / 2f) * C);
                        Special(SpecialKind.Gramophone, Matrix4x4.TRS(roomMid, Quaternion.identity, Vector3.one), new Vector3(width - 0.4f, 2.6f, depth - 0.4f));
                    }
                }
            }

            // ---------- the Beverly Room ----------

            /// The Eternal Ballroom: wood floor, red and gold walls, columns, chandeliers, banquet tables, the Mahjong table
            /// in the middle, jazz from a gramophone. Doors are added round its walls once they're final; how many of them
            /// are really there changes every time you come back.
            void BuildBallroom()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Hall, all);
                var z = zones[id];
                z.Height = 6.2f;
                z.Lamps = true;
                z.Wall = L5Wall(L5Damask);
                z.Floor = new SurfaceRef(Surface.Wood);
                z.Trim = new SurfaceRef(Surface.Trim, 1);
                z.ChairRail = true;
                z.Baseboard = true;
                z.Dead = 0f;
                Paint(all, id);
                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                {
                    if (!Allowed(x, zz)) continue;
                    int wx = lx * D + x, wz = lz * D + zz;
                    var cc = CellCenter(x, zz);
                    if (Mod(wx, 8) == 0 && Mod(wz, 8) == 0 && FreeStanding(x, zz))
                    {
                        used[x, zz] = true;
                        Put(PropKind.RoundPillar, cc, 0f, 0, z.Height, true, new Vector3(0.9f, z.Height, 0.9f));
                    }
                    else if (Mod(wx, 8) == 4 && Mod(wz, 8) == 4)
                    {
                        props.Add(new PropSpec { Kind = PropKind.Chandelier, Pose = Matrix4x4.TRS(cc, Quaternion.Euler(0f, 15f, 0f), Vector3.one), Ceiling = z.Height });
                        Lamp(cc + Vector3.up * (z.Height - 1.3f), 14f, 0.08f, 0x5C0);
                    }
                }
                // The Mahjong table nearest the middle, chairs pulled back, a game nobody finished.
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    int ring = attempt / 4;
                    int x = D / 2 + rng.Range(-ring, ring + 1), zz = D / 2 + rng.Range(-ring, ring + 1);
                    if (!FreeStanding(x, zz) || !Allowed(x, zz)) continue;
                    used[x, zz] = true;
                    var t = CellCenter(x, zz);
                    Put(PropKind.MahjongTable, t, 0f, 0, z.Height, true, new Vector3(0.95f, 0.9f, 0.95f));
                    for (int i = 0; i < 4; i++)
                        props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(t + Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0f, 0.8f), Quaternion.Euler(0f, i * 90f + 180f + rng.Value() * 30f - 15f, 0f), Vector3.one), Ceiling = z.Height });
                    break;
                }
                for (int k = rng.Range(2, 5); k > 0; k--)   // banquet tables with their chairs
                {
                    int x = rng.Range(2, D - 3), zz = rng.Range(2, D - 3);
                    if (!FreeStanding(x, zz) || !FreeStanding(x + 1, zz) || !Allowed(x, zz) || !Allowed(x + 1, zz)) continue;
                    used[x, zz] = used[x + 1, zz] = true;
                    var t = CellCenter(x, zz) + new Vector3(C / 2f, 0f, 0f);
                    Put(PropKind.DiningTable, t, 0f, 0, z.Height, true, new Vector3(1.8f, 0.8f, 0.9f));
                    for (int i = 0; i < 6; i++)
                        props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(t + new Vector3(-0.6f + (i % 3) * 0.6f, 0f, i < 3 ? -0.75f : 0.75f), Quaternion.Euler(0f, i < 3 ? 0f : 180f, 0f), Vector3.one), Ceiling = z.Height });
                }
                if (WallSpotIn(id, out var gp))
                {
                    props.Add(new PropSpec { Kind = PropKind.Gramophone, Pose = gp * Matrix4x4.Translate(new Vector3(0f, 0f, 0.35f)), Ceiling = z.Height });
                    Special(SpecialKind.Gramophone, gp * Matrix4x4.Translate(new Vector3(0f, 0f, 1f)), new Vector3(0.5f, 9f, 0.5f));   // tall = the ballroom's: it never stops
                }
                Special(SpecialKind.BallroomZone, Matrix4x4.TRS(origin + new Vector3(D * C / 2f, 0f, D * C / 2f), Quaternion.identity, Vector3.one), new Vector3(D * C - 2f, 5f, D * C - 2f));
            }

            // ---------- lights ----------

            /// Candle-style sconces along the corridors; rooms and the ballroom light themselves (lamps, chandeliers).
            void Level5Lights(int id)
            {
                var z = zones[id];
                if (z.Kind != ZoneKind.Corridor) return;
                for (int x = 0; x < D; x++)
                for (int zz = 0; zz < D; zz++)
                {
                    if (zone[x, zz] != id || Mod((lx * D + x) * 3 + (lz * D + zz) * 5, 3) != 0 || !WallOfCell(x, zz, out var pose)) continue;
                    props.Add(new PropSpec { Kind = PropKind.Sconce, Pose = pose, Ceiling = z.Height });
                    Lamp(pose.MultiplyPoint3x4(new Vector3(0f, 1.95f, 0.25f)), 5.5f, Mathf.Max(z.Dead, 0.12f), 0x5B0);
                }
            }

            // ---------- after the walls ----------

            void Level5Finish()
            {
                if (theme.id != "5") return;
                FinishHotelRooms();
                // Oil paintings in the corridors; doors round the ballroom.
                var keys = new List<int>(edges.Keys);
                keys.Sort();
                foreach (int key in keys)
                {
                    if (edges[key].Kind != EdgeKind.Wall && edges[key].Kind != EdgeKind.Run) continue;
                    foreach (bool side in new[] { true, false })
                    {
                        if (!WallFrame(key, side, out var pose, out var cell) || Void(cell.x, cell.y) || used[cell.x, cell.y]) continue;
                        var k = zones[zone[cell.x, cell.y]].Kind;
                        if (k == ZoneKind.Hall && zones[zone[cell.x, cell.y]].Lamps && rng.Value() < 0.7f)
                        {
                            used[cell.x, cell.y] = true;
                            Special(SpecialKind.BallroomDoor, pose, Vector3.zero);
                        }
                        else if (k == ZoneKind.Corridor && rng.Value() < 0.035f)
                        {
                            used[cell.x, cell.y] = true;
                            readables.Add(new ReadableSpec { Kind = ReadableKind.Painting, Pose = pose * Matrix4x4.Translate(new Vector3(0f, 1.55f, 0f)), Title = "An oil painting of this corridor", Text = "" });
                        }
                    }
                }
                // Deep in the boiler room: the stairs down to Level 6, in the heat.
                if (rng.Value() < 0.5f)
                    for (int i = 0; i < zones.Count; i++)
                    {
                        if (zones[i].Kind != ZoneKind.Storage || !zones[i].Bulbs || !WallSpotIn(i, out var sp)) continue;
                        AddExit(ExitKind.Stairs, "6", sp);
                        break;
                    }
            }
        }
    }
}
