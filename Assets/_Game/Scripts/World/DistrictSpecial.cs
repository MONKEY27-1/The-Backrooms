using UnityEngine;

namespace Backrooms.World
{
    /// Level -1 (an original level: an endless AI data center) and Level 19 (the wiki's "Attic Floorboards").
    /// - Server halls: rows of racks with aisles and cross-aisles, cold rows of light.
    /// - The pool hall at Level -1's centre: where you land after falling out of Level 0. The basin drains slowly;
    ///   the door at its bottom (back up to Level 0) only opens once it's empty. Signs point to the control room.
    /// - The control room, in the district next to the pool (side set by the seed): its console drains the pool.
    ///   More control rooms turn up in about one district in four further out, for anyone who wanders off.
    /// - The attic: low ceilings, rafters, and an orange glow between some floorboards.
    public static partial class DistrictLayout
    {
        public const float PoolDepth = 3f;
        public static readonly RectInt PoolBasin = new RectInt(8, 8, 16, 16);   // cells of district (0, 0)

        public static bool IsDataCenter(LevelTheme t) => t != null && t.id == "-1";

        /// World-space bounds of the pool basin (Level -1, district 0,0).
        public static Rect PoolArea => new Rect(PoolBasin.xMin * C, PoolBasin.yMin * C, PoolBasin.width * C, PoolBasin.height * C);

        /// Which district holds the signed control room: right next to the pool, on a side set by the seed.
        public static Vector2Int ControlRoomDistrict(uint levelSeed)
        {
            int dir = (int)(WorldHash.Hash(levelSeed, 5, 5, 0xC0DE) % 4u);
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            return dirs[dir];
        }

        /// Whether district (dx, dz) of Level -1 has a control room: the signed one, or a spare further out.
        public static bool HasControlRoom(uint levelSeed, int dx, int dz)
        {
            if (dx == 0 && dz == 0) return false;   // the pool hall
            if (ControlRoomDistrict(levelSeed) == new Vector2Int(dx, dz)) return true;
            return Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) >= 2 && WorldHash.Value(levelSeed, dx, dz, 0xC0DF) < 0.25f;
        }

        /// Where the control room's door is, in world space (for signs and the debug readout).
        public static Vector3 ControlRoomDoor(uint levelSeed)
        {
            var d = ControlRoomDistrict(levelSeed);
            return new Vector3((d.x * D + 16f) * C, 0f, (d.y * D + 12f) * C);
        }

        sealed partial class Plan
        {
            bool PoolHallHere => IsDataCenter(theme) && dx == 0 && dz == 0 && floor == 0;
            bool ControlRoomHere => IsDataCenter(theme) && floor == 0 && HasControlRoom(levelSeed, dx, dz);

            void BuildServerHall()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Hall, all);
                var hz = zones[id];
                hz.Height = 3.4f;
                bool alongX = WorldHash.Value(seed, lx, lz, 0x5E1) < 0.5f;
                hz.Lights = LightLayout.Rows;
                hz.LightsAlongX = alongX;
                hz.Baseboard = false;
                hz.ChairRail = false;
                Paint(all, id);
                // Rows of racks every third cell (world-aligned, so rows continue into the next district), a cross-aisle every 9 cells.
                for (int row = 1; row < D - 1; row++)
                {
                    int worldRow = (alongX ? lz : lx) * D + row;
                    if (((worldRow % 3) + 3) % 3 != 0) continue;
                    for (int a = 1; a < D - 1; a++)
                    {
                        int worldA = (alongX ? lx : lz) * D + a;
                        if (((worldA % 9) + 9) % 9 == 0) continue;
                        int x = alongX ? a : row, z = alongX ? row : a;
                        if (!OpenCell(x, z) || used[x, z] || !Allowed(x, z) || Void(x, z)) continue;
                        used[x, z] = true;
                        var c = CellCenter(x, z);
                        var along = alongX ? Vector3.right : Vector3.forward;
                        float yaw = alongX ? (worldRow / 3 % 2 == 0 ? 0f : 180f) : (worldRow / 3 % 2 == 0 ? 90f : 270f);
                        for (int k = -1; k <= 1; k++)
                            props.Add(new PropSpec { Kind = PropKind.ServerRack, Variant = (x * 7 + z * 3 + k) & 7,
                                Pose = Matrix4x4.TRS(c + along * (k * 0.62f), Quaternion.Euler(0f, yaw, 0f), Vector3.one), Ceiling = hz.Height });
                    }
                }
            }

            /// Level -1's centre: a tall hall around a deep basin of water, steps up one side, the way out at the bottom.
            void BuildPoolHall()
            {
                var hall = new RectInt(2, 2, D - 4, D - 4);
                int hid = NewZone(ZoneKind.Hall, hall);
                var h = zones[hid];
                h.Height = 6f;
                h.Lights = LightLayout.Grid;
                Paint(hall, hid);
                // A doorway in the middle of every side.
                int mid = D / 2;
                Opening(mid, hall.yMin, mid + 1, hall.yMin, EdgeKind.Wide);
                Opening(mid, hall.yMax, mid + 1, hall.yMax, EdgeKind.Wide);
                Opening(hall.xMin, mid, hall.xMin, mid + 1, EdgeKind.Wide);
                Opening(hall.xMax, mid, hall.xMax, mid + 1, EdgeKind.Wide);
                RoomWalls(hall, 0, EdgeKind.Doorway);

                var basin = PoolBasin;
                int bid = NewZone(ZoneKind.Hall, basin);
                var b = zones[bid];
                b.Height = 6f;
                b.Sunk = PoolDepth;
                b.Lights = LightLayout.Grid;
                b.Floor = new SurfaceRef(Surface.Tile);
                b.Wall = new SurfaceRef(Surface.Tile);
                Paint(basin, bid);
                Lock(new RectInt(basin.xMin - 1, basin.yMin - 1, basin.width + 2, basin.height + 2));
                Reserve(new RectInt(basin.xMin - 1, basin.yMin - 1, basin.width + 2, basin.height + 2));

                // Steps out on the west side: 15 steps of 0.2 m.
                var side = new SurfaceRef(Surface.Tile);
                float edgeX = basin.xMin * C, zc = (basin.yMin + 4.5f) * C;   // clear of the chunk edge at the basin middle
                for (int i = 0; i < 15; i++)
                {
                    float top = -PoolDepth + (i + 1) * 0.2f;
                    float x0 = edgeX + (14 - i) * 0.3f;
                    var c = origin + new Vector3(x0 + 0.15f, (-PoolDepth + top) / 2f, zc);
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(c, new Vector3(0.3f, top + PoolDepth, 2.4f)), Faces = BoxMeshBuilder.Faces.All & ~BoxMeshBuilder.Faces.NegY,
                        PosY = side, NegY = side, PosX = side, NegX = side, PosZ = side, NegZ = side, Solid = true });
                }
                // The way out: a door at the bottom of the south wall of the basin, back up to Level 0.
                var doorAt = origin + new Vector3((basin.xMin + basin.width / 2f) * C, -PoolDepth, basin.yMin * C);
                AddExit(ExitKind.Door, "0", Matrix4x4.TRS(doorAt, Quaternion.identity, Vector3.one));

                // Signs: the control room is that way.
                var target = ControlRoomDistrict(levelSeed);
                string arrow = Mathf.Abs(target.x) >= Mathf.Abs(target.y) ? (target.x > 0 ? "EAST" : "WEST") : (target.y > 0 ? "NORTH" : "SOUTH");
                string sign = $"PUMP CONTROL ROOM: {arrow}, NEXT HALL";
                Sign(Key(hall.xMin + 3, hall.yMin, hall.xMin + 4, hall.yMin), true, sign);
                Sign(Key(hall.xMax - 4, hall.yMax, hall.xMax - 3, hall.yMax), false, sign);
                Sign(Key(hall.xMin, hall.yMax - 4, hall.xMin, hall.yMax - 3), true, "WATER LEVEL CONTROLLED FROM PUMP ROOM ONLY");
                Sign(Key(hall.xMax, hall.yMin + 3, hall.xMax, hall.yMin + 4), false, sign);
                Use(new RectInt(0, 0, D, D));
            }

            void Sign(int key, bool positive, string text)
            {
                if (!edges.ContainsKey(key) || !Blocking(edges[key].Kind)) return;
                if (WallFrame(key, positive, out var pose, out _))
                    readables.Add(new ReadableSpec { Kind = ReadableKind.Writing, Pose = pose * Matrix4x4.Translate(new Vector3(0f, 1.9f, 0f)), Text = text });
            }

            /// The pump control room: a walled room with the console, two desks and a sign over the door.
            void BuildControlRoom()
            {
                var room = new RectInt(11, 12, 10, 8);
                int mid = room.xMin + room.width / 2;
                Opening(mid, room.yMin, mid + 1, room.yMin, EdgeKind.Doorway);
                int id = NewZone(ZoneKind.Office, room);
                var z = zones[id];
                z.Height = 2.8f;
                z.Lights = LightLayout.Grid;
                z.Floor = new SurfaceRef(Surface.Tile);
                Paint(room, id);
                RoomWalls(room, 0, EdgeKind.Doorway);
                Lock(new RectInt(room.xMin - 1, room.yMin - 1, room.width + 2, room.height + 2));
                Reserve(new RectInt(room.xMin + 1, room.yMin + 1, room.width - 2, room.height - 2));
                Use(room);
                Use(new RectInt(mid - 2, room.yMin - 3, 5, 3));   // a clear way up to the door
                var centre = origin + new Vector3((room.xMin + room.width / 2f) * C, 0f, (room.yMin + room.height / 2f + 1.5f) * C);
                props.Add(new PropSpec { Kind = PropKind.ControlConsole, Pose = Matrix4x4.TRS(centre, Quaternion.Euler(0f, 180f, 0f), Vector3.one), Ceiling = z.Height });
                props.Add(new PropSpec { Kind = PropKind.Desk, Pose = Matrix4x4.TRS(centre + new Vector3(-5f, 0f, -3f), Quaternion.identity, Vector3.one), Ceiling = z.Height });
                props.Add(new PropSpec { Kind = PropKind.OfficeChair, Pose = Matrix4x4.TRS(centre + new Vector3(-5f, 0f, -3.7f), Quaternion.Euler(0f, 160f, 0f), Vector3.one), Ceiling = z.Height });
                props.Add(new PropSpec { Kind = PropKind.CrtTv, Pose = Matrix4x4.TRS(centre + new Vector3(5f, 0f, -3f), Quaternion.Euler(0f, 200f, 0f), Vector3.one), Ceiling = z.Height });
                Sign(Key(mid - 1, room.yMin, mid, room.yMin), false, "PUMP CONTROL");
                Sign(Key(mid + 1, room.yMin, mid + 2, room.yMin), false, "PUMP CONTROL");
                Sign(Key(mid, room.yMax, mid + 1, room.yMax), false, "BASIN DRAIN: PULL THE RED LEVER");
            }

            // ---------- the attic ----------

            /// Glowing cracks between floorboards: light comes up from below.
            void FloorGlow(int id)
            {
                if (ds.floorGlow <= 0f) return;
                var z = zones[id];
                for (int x = z.Rect.xMin; x < z.Rect.xMax; x++)
                for (int zz = z.Rect.yMin; zz < z.Rect.yMax; zz++)
                {
                    if (!OpenCell(x, zz) || zone[x, zz] != id || used[x, zz] || Void(x, zz)) continue;
                    if (WorldHash.Value(seed, dx * D + x, dz * D + zz, 0x6A0) >= ds.floorGlow) continue;
                    var c = CellCenter(x, zz);
                    panels.Add(new PanelSpec
                    {
                        Position = new Vector3(c.x, 0.012f, c.z + 0.25f),
                        AlongX = true,
                        State = FixtureState.Lit,
                        Emitter = true,
                        Range = 4.5f,
                        Id = (int)WorldHash.Hash(seed, dx * D + x, dz * D + zz, 0x6A1),
                        Floor = true,
                    });
                }
            }

            /// Rafters: a beam across the ceiling every metre.
            void Rafters(ChunkLayout L, int x, int z, float h, Zone zz)
            {
                if (!ds.rafters || zz.Shaft) return;
                for (int i = 0; i < 2; i++)
                {
                    var c = origin + new Vector3((x + 0.5f) * C, h - 0.08f, (z + 0.25f + i * 0.5f) * C);
                    L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, new Vector3(C, 0.16f, 0.12f)), zz.Ceiling, BoxMeshBuilder.Faces.All & ~BoxMeshBuilder.Faces.PosY, false));
                }
            }
        }
    }
}
