using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Stacked floors of Level 0. Every floor is its own layout (its own seed), 8 m apart, joined by:
    /// - Stairwells: a flight of 40 steps in a walled shaft; the floor above has a railed opening over it.
    /// - Balconies: a railed opening looking down on the floor below; Pitfalls: the same with no railing.
    /// - Deep pits: a shaft through every floor. Fall far enough and you noclip out of Level 0.
    /// - Sunken rooms: a few rooms whose floor sits three steps down.
    /// Which features join two floors is a pure function of the level seed and district, so both floors agree.
    public static partial class DistrictLayout
    {
        public const float FloorHeight = 8f;
        const float PitDepth = 2f, PitRim = 0.3f;   // PitRim: half a walkway between two pits   // a Hole's shaft; the floor below's tallest ceiling (giant halls, 5.8 m) ends 2.2 m under us
        public const int TopFloor = 3, BottomFloor = -3;
        const float Sink = 0.45f;   // sunken rooms: three steps of 0.15 m
        const float RailHeight = 1.0f;

        public static bool HasFloors(LevelTheme t) => t != null && t.generator == LevelGenerator.Districts && (t.id == "0" || t.id == "1");   // Level 1: parking decks

        public static uint FloorSeed(uint levelSeed, int floor) => floor == 0 ? levelSeed : WorldHash.Hash(levelSeed, floor, 7, 0xF100);

        /// Which floor a height belongs to (stairs switch over halfway up).
        public static int FloorOf(float y) => Mathf.FloorToInt((y + FloorHeight * 0.4f) / FloorHeight);

        enum VerticalKind { Stair, Balcony, Pitfall, DeepPit, Ramp, Ladder }

        struct Vertical
        {
            public VerticalKind Kind;
            public RectInt Rect;    // district cells
            public bool AlongX;     // stairs: the flight rises toward +x (or +z)
            public int Side;        // ladders: the wall it's fixed to (0 +x, 1 +z, 2 -x, 3 -z)
        }

        /// Features joining floor `lower` and the floor above it, in district (dx, dz).
        static void VerticalsBetween(uint levelSeed, int dx, int dz, int lower, LevelTheme theme, List<Vertical> into)
        {
            if (lower < BottomFloor || lower > TopFloor) return;   // above the top floor: only stairs, up into the attic
            if (theme.id == "1")
            {
                // The garage: a switchback car ramp between most decks, and now and then a service ladder.
                if (lower >= TopFloor) return;
                bool l = (lower & 1) == 0;
                int a0 = l ? 3 : 18, a1 = l ? 14 : 29;
                uint g = WorldHash.Hash(levelSeed, dx * 31 + lower * 7, dz, 0xF210);
                float G(int i) => WorldHash.Value(g, i, 0, 0xF211);
                if (G(0) < 0.75f) into.Add(new Vertical { Kind = VerticalKind.Ramp, Rect = new RectInt(a0 + (int)(G(1) * (a1 - a0 - 6)), 3, 6, 14) });
                if (G(2) < 0.5f) into.Add(new Vertical { Kind = VerticalKind.Ladder, Rect = new RectInt(a0 + 1 + (int)(G(3) * (a1 - a0 - 3)), 20 + (int)(G(4) * 6), 1, 1), Side = (int)(G(5) * 4) & 3 });
                return;
            }
            if (CutAt(levelSeed, dx, dz, theme)) return;   // the Cut takes the whole middle of the district
            bool touchesStart = lower == 0 || lower + 1 == 0;
            if (touchesStart && ((dx == 0 && dz == 0) || ForgeLibrary.RoomAt(levelSeed, dx, dz, theme) != null)) return;   // keep the photo rooms and hand-made rooms whole
            // Consecutive pairs use opposite halves of the district, so a floor's hole never lands on its own stairwell.
            bool left = (lower & 1) == 0;
            int x0 = left ? 3 : 18, x1 = left ? 14 : 29;
            uint h = WorldHash.Hash(levelSeed, dx * 31 + lower * 7, dz, 0xF200);
            float V(int i) => WorldHash.Value(h, i, 0, 0xF201);

            if (V(0) < 0.45f)
            {
                bool alongX = V(1) < 0.5f;
                var r = alongX
                    ? new RectInt(x0 + (int)(V(2) * (x1 - x0 - 5)), 3 + (int)(V(3) * 9), 5, 2)
                    : new RectInt(x0 + (int)(V(2) * (x1 - x0 - 2)), 3 + (int)(V(3) * 7), 2, 5);
                into.Add(new Vertical { Kind = VerticalKind.Stair, Rect = r, AlongX = alongX });
            }
            if (lower == TopFloor) return;
            if (V(4) < 0.4f)
            {
                bool balcony = V(7) < 0.6f;
                int w = balcony ? 2 + (int)(V(5) * 3) : 1 + (int)(V(5) * 2);
                int d = balcony ? 2 + (int)(V(6) * 3) : 1 + (int)(V(6) * 2);
                var r = new RectInt(x0 + (int)(V(8) * (x1 - x0 - w)), 17 + (int)(V(9) * (11 - d)), w, d);
                into.Add(new Vertical { Kind = balcony ? VerticalKind.Balcony : VerticalKind.Pitfall, Rect = r });
            }
        }

        /// A shaft straight through every floor, in the middle column between the two halves.
        static bool DeepPitAt(uint levelSeed, int dx, int dz, LevelTheme theme, out RectInt r)
        {
            r = default;
            if (theme.id != "0" || (dx == 0 && dz == 0) || ForgeLibrary.RoomAt(levelSeed, dx, dz, theme) != null || CutAt(levelSeed, dx, dz, theme)) return false;
            if (WorldHash.Value(levelSeed, dx, dz, 0xF300) >= 0.05f) return false;
            r = new RectInt(15, 6 + (int)(WorldHash.Value(levelSeed, dx, dz, 0xF301) * 18f), 2, 2);
            return true;
        }

        /// Does chunk (cx, cz) on `floor` hold anything that joins it to `other` (one floor up or down)?
        /// The streamer loads the other floor's chunk only then.
        public static bool ChunkJoins(uint levelSeed, int floor, int other, int cx, int cz, LevelTheme theme)
        {
            int dx = FloorDiv(cx, 2), dz = FloorDiv(cz, 2);
            var chunkCells = new RectInt(cx * ChunkCells - dx * D, cz * ChunkCells - dz * D, ChunkCells, ChunkCells);
            if (CutAt(levelSeed, dx, dz, theme) && CutRect.Overlaps(chunkCells) && Mathf.Abs(floor - other) <= 2) return true;
            if (Mathf.Abs(floor - other) != 1) return false;
            var list = new List<Vertical>();
            VerticalsBetween(levelSeed, dx, dz, Mathf.Min(floor, other), theme, list);
            foreach (var v in list) if (v.Rect.Overlaps(chunkCells)) return true;
            return DeepPitAt(levelSeed, dx, dz, theme, out var pit) && pit.Overlaps(chunkCells);
        }

        sealed partial class Plan
        {
            readonly bool[,] hole = new bool[D, D];       // no floor here: you fall to the floor below
            readonly bool[,] voidCell = new bool[D, D];   // hole or shaft: no walls, props or doorways inside
            readonly List<SurfaceBox> extraBoxes = new List<SurfaceBox>();   // stairs, steps, risers
            readonly HashSet<int> poolStairMouths = new HashSet<int>();
            readonly List<SurfaceSlope> slopes = new List<SurfaceSlope>();   // car ramps
            readonly List<int> forcedOpen = new List<int>();   // edges that must stay open: ramp mouths, ladder ways in

            bool Void(int x, int z) => x >= 0 && z >= 0 && x < D && z < D && voidCell[x, z];
            bool IsHole(int x, int z) => x >= 0 && z >= 0 && x < D && z < D && hole[x, z];
            bool IsShaft(int x, int z) => OpenCell(x, z) && zones[zone[x, z]].Shaft;
            float SunkAt(int x, int z) => OpenCell(x, z) ? zones[zone[x, z]].Sunk : 0f;

            void Lock(RectInt r)
            {
                locked ??= new bool[D, D];
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                {
                    locked[x, z] = true;
                    used[x, z] = true;
                }
            }

            void ReserveInside(RectInt r) => Reserve(new RectInt(r.xMin + 1, r.yMin + 1, r.width - 2, r.height - 2));

            /// Places this floor's side of every feature that joins it to the floors above and below.
            void BuildVerticals()
            {
                if (!HasFloors(theme)) return;
                var below = new List<Vertical>();
                var above = new List<Vertical>();
                VerticalsBetween(levelSeed, dx, dz, floor - 1, theme, below);   // we are the upper floor of these
                VerticalsBetween(levelSeed, dx, dz, floor, theme, above);       // and the lower floor of these
                if (DeepPitAt(levelSeed, dx, dz, theme, out var pit))
                {
                    var deep = new Vertical { Kind = VerticalKind.DeepPit, Rect = pit };
                    below.Add(deep);
                    above.Add(deep);
                }
                foreach (var v in above) Shaft(v);
                foreach (var v in below) Hole(v);
                BuildCut();
            }

            /// Lower floor: the space under an opening is open to the floor above. Stairwells get walls and a flight.
            void Shaft(Vertical v)
            {
                var r = v.Rect;
                int id = NewZone(ZoneKind.Open, r);
                var z = zones[id];
                z.Shaft = true;
                z.Height = FloorHeight - 0.01f;
                z.Dead = 1f;
                Paint(r, id);
                Lock(r);
                MarkVoid(r);
                ReserveInside(r);
                if (v.Kind == VerticalKind.Ramp) { RampShaft(r, z); return; }
                if (v.Kind == VerticalKind.Ladder) { LadderShaft(v, z); return; }
                if (v.Kind != VerticalKind.Stair) return;

                // Walls on the long sides and the top end; the bottom end is the way in.
                int len = v.AlongX ? r.width : r.height;
                for (int i = 0; i < len; i++)
                {
                    if (v.AlongX) { Wall(r.xMin + i, r.yMin, r.xMin + i + 1, r.yMin, null); Wall(r.xMin + i, r.yMax, r.xMin + i + 1, r.yMax, null); }
                    else { Wall(r.xMin, r.yMin + i, r.xMin, r.yMin + i + 1, null); Wall(r.xMax, r.yMin + i, r.xMax, r.yMin + i + 1, null); }
                }
                for (int i = 0; i < (v.AlongX ? r.height : r.width); i++)
                {
                    if (v.AlongX) Wall(r.xMax, r.yMin + i, r.xMax, r.yMin + i + 1, null);
                    else Wall(r.xMin + i, r.yMax, r.xMin + i + 1, r.yMax, null);
                }
                // Keep the landing in front of the stairs clear.
                var landing = v.AlongX ? new RectInt(r.xMin - 1, r.yMin, 1, r.height) : new RectInt(r.xMin, r.yMin - 1, r.width, 1);
                Lock(landing);
                Reserve(landing);

                // From the top floor the stairs climb into darkness and come out in the attic (Level 19).
                if (floor == TopFloor && theme.id == "0")
                {
                    var cap = origin + new Vector3((r.xMin + r.width / 2f) * C, FloorHeight - 0.02f, (r.yMin + r.height / 2f) * C);
                    extraBoxes.Add(SurfaceBox.Uniform(new Bounds(cap, new Vector3(r.width * C, 0.04f, r.height * C)), new SurfaceRef(Surface.Void), BoxMeshBuilder.Faces.NegY, false));
                    var top = v.AlongX
                        ? origin + new Vector3(r.xMax * C - 1.6f, FloorHeight - 1.6f, (r.yMin + r.height / 2f) * C)
                        : origin + new Vector3((r.xMin + r.width / 2f) * C, FloorHeight - 1.6f, r.yMax * C - 1.6f);
                    var size = v.AlongX ? new Vector3(3f, 2.4f, r.height * C) : new Vector3(r.width * C, 2.4f, 3f);
                    exits.Add(new ExitSpec { Kind = ExitKind.Hatch, Target = "19", Pose = Matrix4x4.Translate(top), Trigger = new Bounds(top, size) });
                }

                if (floor != TopFloor) SteppedCeiling(r, v.AlongX, len, z);

                // The flight: 40 steps of 0.2 m rise and 0.25 m run.
                const int steps = 40;
                float run = len * C / steps, rise = FloorHeight / steps;
                var tread = z.Floor;
                var side = new SurfaceRef(Surface.Trim, trim);
                for (int i = 0; i < steps; i++)
                {
                    float a = i * run, top = (i + 1) * rise;
                    Vector3 c, size;
                    if (v.AlongX)
                    {
                        c = origin + new Vector3(r.xMin * C + a + run / 2, top / 2, (r.yMin + r.height / 2f) * C);
                        size = new Vector3(run, top, r.height * C - 0.15f);
                    }
                    else
                    {
                        c = origin + new Vector3((r.xMin + r.width / 2f) * C, top / 2, r.yMin * C + a + run / 2);
                        size = new Vector3(r.width * C - 0.15f, top, run);
                    }
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(c, size), Faces = BoxMeshBuilder.Faces.All & ~BoxMeshBuilder.Faces.NegY,
                        PosY = tread, NegY = tread, PosX = side, NegX = side, PosZ = side, NegZ = side, Solid = true });
                }
            }

            /// The edge on one side of a one-cell rect.
            static int SideKey(RectInt r, int side) => side switch
            {
                0 => Key(r.xMax, r.yMin, r.xMax, r.yMin + 1),
                1 => Key(r.xMin, r.yMax, r.xMin + 1, r.yMax),
                2 => Key(r.xMin, r.yMin, r.xMin, r.yMin + 1),
                _ => Key(r.xMin, r.yMin, r.xMin + 1, r.yMin),
            };

            static bool SideEdge(RectInt r, int side, int x0, int z0, int x1, int z1) => side switch
            {
                0 => x0 == r.xMax && x1 == r.xMax,
                1 => z0 == r.yMax && z1 == r.yMax,
                2 => x0 == r.xMin && x1 == r.xMin,
                _ => z0 == r.yMin && z1 == r.yMin,
            };

            /// Level 1's car ramp, a switchback in a walled shaft 12 m wide and 28 m long. Lane A climbs from this deck
            /// to a landing halfway up; lane B turns and climbs back to the deck above, with a parapet between the
            /// lanes. The way in is lane A's mouth at the near end.
            void RampShaft(RectInt r, Zone z)
            {
                for (int i = 0; i < r.height; i++) { Wall(r.xMin, r.yMin + i, r.xMin, r.yMin + i + 1, null); Wall(r.xMax, r.yMin + i, r.xMax, r.yMin + i + 1, null); }
                for (int i = 0; i < r.width; i++) Wall(r.xMin + i, r.yMax, r.xMin + i + 1, r.yMax, null);
                var mouth = new RectInt(r.xMin, r.yMin - 1, 3, 1);
                Lock(mouth);
                Reserve(mouth);
                for (int i = 0; i < 3; i++) forcedOpen.Add(Key(r.xMin + i, r.yMin, r.xMin + i + 1, r.yMin));   // a 6 m mouth, not a door
                z.Wall = new SurfaceRef(Surface.Wall, 0);   // bare garage concrete, whatever the district around it
                const float laneW = 3 * C, run = 12 * C, half = FloorHeight / 2f;
                float x0 = origin.x + r.xMin * C, z0 = origin.z + r.yMin * C;
                var deck = z.Floor;
                var side = z.Wall;
                slopes.Add(new SurfaceSlope { A = new Vector3(x0 + laneW / 2f, 0f, z0), B = new Vector3(x0 + laneW / 2f, half, z0 + run), Width = laneW, Bottom = 0f, Top = deck, Side = side });
                slopes.Add(new SurfaceSlope { A = new Vector3(x0 + laneW * 1.5f, half, z0 + run), B = new Vector3(x0 + laneW * 1.5f, FloorHeight, z0), Width = laneW, Bottom = 0f, Top = deck, Side = side });
                // The landing, one 2 m tile at a time so no piece crosses a chunk border.
                for (int i = 0; i < r.width; i++)
                for (int j = 12; j < 14; j++)
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(origin + new Vector3((r.xMin + i + 0.5f) * C, half / 2f, (r.yMin + j + 0.5f) * C), new Vector3(C, half, C)),
                        Faces = BoxMeshBuilder.Faces.PosY, PosY = deck, Solid = true });
                // The parapet between the lanes, along lane B's inner edge.
                slopes.Add(new SurfaceSlope { A = new Vector3(x0 + laneW + 0.15f, half + 1f, z0 + run), B = new Vector3(x0 + laneW + 0.15f, FloorHeight + 1f, z0), Width = 0.3f, Thickness = 1f,
                    Top = new SurfaceRef(Surface.Trim, trim), Side = side });
                // Caged lamps on the side walls, a little above each lane.
                for (int i = 0; i < 3; i++)
                {
                    float t = (i + 0.5f) / 3f, zz = z0 + run * t;
                    LampOnWall(new Vector3(x0 + 0.06f, half * t + 2.4f, zz), Vector3.right);
                    LampOnWall(new Vector3(x0 + 2f * laneW - 0.06f, half + half * (1f - t) + 2.4f, zz), Vector3.left);
                }
            }

            void LampOnWall(Vector3 at, Vector3 outward)
            {
                props.Add(new PropSpec { Kind = PropKind.WallLamp, Pose = Matrix4x4.TRS(at, Quaternion.LookRotation(outward), Vector3.one) });
                var light = at + outward * 0.3f;
                panels.Add(new PanelSpec { Position = light, State = FixtureState.Lit, Emitter = true, Bare = true, Range = 9f, Tint = new Color(1f, 0.82f, 0.6f),
                    Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(light.x * 2f), Mathf.RoundToInt(light.z * 2f) + Mathf.RoundToInt(light.y), 0x1A7) });
            }

            /// A service ladder in a 2 m shaft: walls on three sides, the ladder fixed to the wall at `Side`, up to
            /// the deck above (where the hole is open on that side so you can step off).
            void LadderShaft(Vertical v, Zone z)
            {
                var r = v.Rect;
                int entry = (v.Side + 2) & 3;
                for (int s = 0; s < 4; s++)
                {
                    if (s == entry) continue;
                    if (s == 0) Wall(r.xMax, r.yMin, r.xMax, r.yMax, null);
                    else if (s == 1) Wall(r.xMin, r.yMax, r.xMax, r.yMax, null);
                    else if (s == 2) Wall(r.xMin, r.yMin, r.xMin, r.yMax, null);
                    else Wall(r.xMin, r.yMin, r.xMax, r.yMin, null);
                }
                var way = new RectInt(r.xMin + Dx[entry], r.yMin + Dz[entry], 1, 1);
                Lock(way);
                Reserve(way);
                forcedOpen.Add(SideKey(r, entry));
                z.Wall = new SurfaceRef(Surface.Wall, 0);
                var n = new Vector3(Dx[v.Side], 0f, Dz[v.Side]);
                var at = CellCenter(r.xMin, r.yMin) + n * (C / 2f - T / 2f - 0.02f);
                props.Add(new PropSpec { Kind = PropKind.WallLadder, Pose = Matrix4x4.TRS(at, Quaternion.LookRotation(-n), Vector3.one), Ceiling = FloorHeight + 1.1f });
                int beside = (v.Side + 1) & 3;   // a lamp on a side wall, halfway up
                var m = new Vector3(Dx[beside], 0f, Dz[beside]);
                LampOnWall(CellCenter(r.xMin, r.yMin) + m * (C / 2f - T / 2f - 0.06f) + Vector3.up * 5.5f, -m);
            }

            /// Over a flight of stairs: a ceiling that steps up with it (2.6 m headroom), lit, with a header over the way
            /// in, so from the bottom you see a lit stairwell climbing away instead of stairs vanishing into the ceiling.
            /// The last stretch is left open: that's where you come out on the floor above.
            void SteppedCeiling(RectInt r, bool alongX, int len, Zone z)
            {
                const float headroom = 2.6f, t = 0.1f;
                float risePerCell = FloorHeight / len;
                float width = (alongX ? r.height : r.width) * C;
                Vector3 At(float along, float y) => alongX
                    ? origin + new Vector3(r.xMin * C + along, y, (r.yMin + r.height / 2f) * C)
                    : origin + new Vector3((r.xMin + r.width / 2f) * C, y, r.yMin * C + along);
                Vector3 Size(float along, float y) => alongX ? new Vector3(along, y, width) : new Vector3(width, y, along);
                var down = alongX ? BoxMeshBuilder.Faces.NegX : BoxMeshBuilder.Faces.NegZ;   // faces looking back down the flight
                float last = 2.4f;   // the header over the bottom step
                for (int i = 0; i < len; i++)
                {
                    float ceil = (i + 1) * risePerCell + headroom;
                    if (ceil > FloorHeight - 0.3f) break;
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(At((i + 0.5f) * C, ceil + t / 2f), Size(C, t)),
                        Faces = BoxMeshBuilder.Faces.NegY, NegY = z.Ceiling, Solid = true });   // underside only: from the floor above you look straight down the flight
                    // The riser between this stretch of ceiling and the one before it.
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(At(i * C, (last + ceil) / 2f), Size(0.05f, ceil - last)),
                        Faces = down, NegX = z.Wall, NegZ = z.Wall, Solid = false });
                    last = ceil;
                    var p = At((i + 0.5f) * C, ceil - 0.01f);
                    panels.Add(new PanelSpec
                    {
                        Position = p, AlongX = !alongX, State = FixtureState.Lit, Emitter = i % 2 == 0, Range = 6f,
                        Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(p.x * 2f), Mathf.RoundToInt(p.z * 2f), 0x5C1),
                    });
                }
            }

            /// Upper floor: a hole in the floor, railed for stairwells and balconies, open for pitfalls and deep pits.
            void Hole(Vertical v)
            {
                var r = v.Rect;
                Lock(r);
                MarkVoid(r);
                ReserveInside(r);
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                    hole[x, z] = true;
                if (v.Kind == VerticalKind.Pitfall) Bridge(r);
                if (v.Kind == VerticalKind.Pitfall || v.Kind == VerticalKind.DeepPit) return;

                // Railing around the edge; stairs leave the top end open where the flight arrives, ramps where lane B
                // comes up, ladders on the side the ladder is fixed to.
                bool StairTop(int x0, int z0, int x1, int z1) =>
                    v.Kind == VerticalKind.Stair ? (v.AlongX ? x0 == r.xMax && x1 == r.xMax : z0 == r.yMax && z1 == r.yMax)
                    : v.Kind == VerticalKind.Ramp ? z0 == r.yMin && z1 == r.yMin && x0 >= r.xMin + 3
                    : v.Kind == VerticalKind.Ladder && SideEdge(r, v.Side, x0, z0, x1, z1);
                if (v.Kind == VerticalKind.Ramp)
                {
                    var arrive = new RectInt(r.xMin + 3, r.yMin - 1, 3, 1);
                    Lock(arrive);
                    Reserve(arrive);
                    for (int i = 3; i < 6; i++) forcedOpen.Add(Key(r.xMin + i, r.yMin, r.xMin + i + 1, r.yMin));
                }
                if (v.Kind == VerticalKind.Ladder)
                {
                    var off = new RectInt(r.xMin + Dx[v.Side], r.yMin + Dz[v.Side], 1, 1);
                    Lock(off);
                    Reserve(off);
                    forcedOpen.Add(SideKey(r, v.Side));
                }
                void Rail(int x0, int z0, int x1, int z1) { if (!StairTop(x0, z0, x1, z1)) Wall(x0, z0, x1, z1, EdgeKind.LowWall, null, out _); }
                for (int x = r.xMin; x < r.xMax; x++) { Rail(x, r.yMin, x + 1, r.yMin); Rail(x, r.yMax, x + 1, r.yMax); }
                for (int z = r.yMin; z < r.yMax; z++) { Rail(r.xMin, z, r.xMin, z + 1); Rail(r.xMax, z, r.xMax, z + 1); }
                if (v.Kind == VerticalKind.Stair)   // where you step off the top: no walls may start there
                {
                    var exit = v.AlongX ? new RectInt(r.xMax, r.yMin, 1, r.height) : new RectInt(r.xMin, r.yMax, r.width, 1);
                    Lock(exit);
                    Reserve(exit);
                }
            }

            void MarkVoid(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                    voidCell[x, z] = true;
            }

            // ---------- sunken rooms ----------

            /// A few enclosed rooms sit three steps down, with steps inside every way in.
            void SinkRooms()
            {
                // Level 37 owns its basin depths and raised ledges; generic sinking would add stray steps.
                if (theme.zoneWater || !HasFloors(theme)) return;
                int sunk = 0;
                for (int id = 0; id < zones.Count && sunk < 2; id++)
                {
                    var z = zones[id];
                    if (!Sinkable(id) || rng.Value() > 0.3f) continue;
                    z.Sunk = Sink;
                    sunk++;
                    var r = z.Rect;
                    for (int x = r.xMin; x < r.xMax; x++)
                    for (int zz = r.yMin; zz < r.yMax; zz++)
                    {
                        if (zone[x, zz] != id) continue;
                        for (int s = 0; s < 4; s++)
                        {
                            int nx = x + Dx[s], nz = zz + Dz[s];
                            if (OpenCell(nx, nz) && zone[nx, nz] == id) continue;
                            int key = s == 0 ? Key(x + 1, zz, x + 1, zz + 1) : s == 1 ? Key(x, zz + 1, x + 1, zz + 1) : s == 2 ? Key(x, zz, x, zz + 1) : Key(x, zz, x + 1, zz);
                            if (edges.TryGetValue(key, out var e) && IsOpening(e.Kind)) Steps(x, zz, s, z);
                        }
                    }
                }
            }

            bool Sinkable(int id)
            {
                var z = zones[id];
                switch (z.Kind)
                {
                    case ZoneKind.Room: case ZoneKind.Office: case ZoneKind.BreakRoom: case ZoneKind.Conference:
                    case ZoneKind.Reception: case ZoneKind.HotelRoom: break;
                    default: return false;
                }
                var r = z.Rect;
                if (r.width * r.height < 9 || r.xMin < 1 || r.yMin < 1 || r.xMax > D - 1 || r.yMax > D - 1) return false;
                for (int x = r.xMin; x < r.xMax; x++)
                for (int zz = r.yMin; zz < r.yMax; zz++)
                {
                    if (zone[x, zz] != id) continue;
                    if (Void(x, zz) || (locked != null && locked[x, zz])) return false;
                    // Every side facing out of the room must be a wall or a doorway, so the drop never shows.
                    for (int s = 0; s < 4; s++)
                    {
                        int nx = x + Dx[s], nz = zz + Dz[s];
                        if (OpenCell(nx, nz) && zone[nx, nz] == id) continue;
                        int key = s == 0 ? Key(x + 1, zz, x + 1, zz + 1) : s == 1 ? Key(x, zz + 1, x + 1, zz + 1) : s == 2 ? Key(x, zz, x, zz + 1) : Key(x, zz, x + 1, zz);
                        if (!edges.ContainsKey(key) && OpenCell(nx, nz)) return false;
                    }
                }
                return true;
            }

            /// Two steps and a riser just inside a way into a sunken room, from side `s` of cell (x, z).
            void Steps(int x, int z, int s, Zone room)
            {
                var c = CellCenter(x, z);
                var outward = new Vector3(Dx[s], 0f, Dz[s]);   // toward the doorway
                var edgePoint = c + outward * (C / 2);
                bool alongX = Dz[s] != 0;                      // the step runs along the doorway
                var side = new SurfaceRef(Surface.Trim, trim);
                void Box(float fromEdge, float depth, float y0, float y1)
                {
                    var center = edgePoint - outward * (fromEdge + depth / 2) + Vector3.up * ((y0 + y1) / 2);
                    var size = alongX ? new Vector3(C - 0.1f, y1 - y0, depth) : new Vector3(depth, y1 - y0, C - 0.1f);
                    extraBoxes.Add(new SurfaceBox { Box = new Bounds(center, size), Faces = BoxMeshBuilder.Faces.All & ~BoxMeshBuilder.Faces.NegY,
                        PosY = room.Floor, NegY = room.Floor, PosX = side, NegX = side, PosZ = side, NegZ = side, Solid = true });
                }
                Box(0.06f, 0.36f, -Sink, -0.15f);
                Box(0.42f, 0.36f, -Sink, -0.30f);
                Box(0f, 0.06f, -Sink, 0f);   // the riser under the threshold
            }

            /// Props, curiosities and other things standing in a sunken room go down with it.
            void LowerSunkContents()
            {
                float SunkUnder(Vector3 p)
                {
                    var cell = CellOf(p);
                    return cell.x >= 0 && cell.y >= 0 && cell.x < D && cell.y < D ? SunkAt(cell.x, cell.y) : 0f;
                }
                for (int i = 0; i < props.Count; i++)
                {
                    float s = SunkUnder(props[i].Pose.GetColumn(3));
                    if (s <= 0f) continue;
                    var p = props[i];
                    p.Pose = Matrix4x4.Translate(Vector3.down * s) * p.Pose;
                    props[i] = p;
                }
                for (int i = 0; i < slots.Count; i++)
                {
                    float s = SunkUnder(slots[i].Pose.GetColumn(3));
                    if (s <= 0f) continue;
                    var p = slots[i];
                    p.Pose = Matrix4x4.Translate(Vector3.down * s) * p.Pose;
                    slots[i] = p;
                }
            }
        }
    }
}
