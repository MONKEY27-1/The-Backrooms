using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// A boarded-up doorway. Pryable ones can be pulled open (Hold E); the rest are nailed shut for good,
    /// and only ever block a doorway you can walk around.
    public struct BoardSpec
    {
        public Matrix4x4 Pose;   // centre of the doorway on the floor; local +z is the side the boards are nailed on
        public float Width;
        public bool Pryable;
        public int Id;           // stable per position, so a pried doorway stays open
        public int Seed;         // plank layout
        public bool LockedDoor;  // not planks: a "locked" door you can force (Level 2)
        public string Exit;      // a forced door that leads somewhere: the level id, else null
    }

    /// A plank laid across a gap: you can walk on it.
    public struct PlankSpec
    {
        public Matrix4x4 Pose;   // centre of the plank's top face; local x along the plank
        public float Length;
        public int Variant;
    }

    /// Stage 4 landmarks and traces:
    /// - The Cut: very rarely, Level 0 is sliced open: a huge shaft through every floor, the rooms of each floor
    ///   ending at its edge. Look down and the floors go on; fall in and you leave Level 0.
    /// - Boards: plank bridges over some pitfalls; boarded-up doorways, some of which you can pry open.
    /// - Signs of people (rare, quiet): abandoned camps, chairs facing walls, tally marks, arrows, notes and traces.
    ///   Arrows and notes are ambiguous and sometimes misleading, but some still help (including toward exits/stairs).
    public static partial class DistrictLayout
    {
        static readonly RectInt CutRect = new RectInt(10, 10, 12, 12);

        /// The Cut: about one district in seventy on Level 0, through every floor.
        static bool CutAt(uint levelSeed, int dx, int dz, LevelTheme theme)
        {
            if (theme.id != "0" || (dx == 0 && dz == 0) || Mathf.Abs(dx) + Mathf.Abs(dz) < 3 || ForgeLibrary.RoomAt(levelSeed, dx, dz, theme) != null) return false;
            return WorldHash.Value(levelSeed, dx, dz, 0xC07) < 0.015f;
        }

        /// Standing in or right at the edge of the Cut (for the achievement).
        public static bool NearCut(uint levelSeed, Vector3 p, LevelTheme theme)
        {
            if (!HasFloors(theme)) return false;
            float size = D * C;
            int dx = Mathf.FloorToInt(p.x / size), dz = Mathf.FloorToInt(p.z / size);
            if (!CutAt(levelSeed, dx, dz, theme)) return false;
            var cell = new Vector2Int(Mathf.FloorToInt((p.x - dx * size) / C), Mathf.FloorToInt((p.z - dz * size) / C));
            var r = CutRect;
            return cell.x >= r.xMin - 2 && cell.x < r.xMax + 2 && cell.y >= r.yMin - 2 && cell.y < r.yMax + 2;
        }

        static readonly string[] CampNotes =
        {
            "Day 12. The hum is louder near the stairs. Going up tomorrow.",
            "Left two bottles of water under the box for whoever comes next. Pay it forward.",
            "Don't sleep near the tall doors. They weren't there when I lay down.",
            "Counted rooms for three days. They start to repeat after a while. Same stain, same chair.",
            "If you hear footsteps that stop when you stop, keep walking. Don't turn around.",
            "Found a stairwell going up into the dark. Smells like dust and old wood up there.",
            "The lights went out for a minute. Something smiled at me. I stayed very still.",
            "Whoever drew the arrows: thank you. They led me to stairs.",
            "ORD survey log: the east corridor does not match yesterday's map. Recheck every marker on the way back.",
            "They sent us in wearing the same suits. I haven't seen the others since the blackout.",
            "ORD log, maybe: recover the object, document the bodies. Nobody said whose bodies.",
            "If you find another yellow suit, don't assume they're asleep. Keep moving.",
            "Map is useless. The halls I marked yesterday are gone today.",
            "The carpet is always wet here. Follow the dry seams if you can find any.",
            "I heard a radio voice in the walls. It knew my name. I didn't answer.",
            "Left the lantern on. If it's out when you find this, don't stay.",
            "Tried to map this place. The arrows I left don't point the same way now.",
            "Someone else's chalk is newer than mine. I don't remember passing them.",
            "ORD: sample recovered. Route back unclear. Marking what I can.",
            "If this note is in the same place twice, leave immediately.",
            "Camp moved. Or the room did. Supplies left behind on purpose.",
            "Follow the marks only if they agree. They don't always agree.",
        };

        static readonly string[] WallWritings =
        {
            "HELP",
            "IT MOVED",
            "NO EXIT",
            "STAY QUIET",
            "DON'T TRUST THE HUM",
            "NOT ALONE",
            "TURN BACK",
            "THIS ROOM WASN'T HERE",
            "KEEP COUNTING",
            "THE WALLS SHIFT",
            "WRONG WAY",
            "THIS WAY?",
            "EXIT?",
            "DON'T SLEEP",
            "MARKER MISSING",
            "SAME ROOM",
            "ORD?",
            "SUIT #?",
            "LISTEN",
            "DOOR WAS HERE",
            "NOT THE SAME",
        };

        static readonly string[] SurveyNotes =
        {
            "ORD field copy. West marker missing again. Do not replace it from memory.",
            "Survey pass 3. The corridor beyond this point is shorter on the return walk.",
            "Inventory: two lamps, one map, three names crossed out. No reason given.",
            "If this is the same room, the chair was facing the other way yesterday.",
            "ORD trace: equipment left at junction. Junction could not be found again.",
            "Field note: arrows drawn by previous team conflict with current layout.",
            "Recovered: one sample, one damaged recorder. Voices present but unintelligible.",
            "Checklist incomplete. The person assigned to finish it is not listed.",
            "ORD note: north mark says exit, east mark says return. Both look recent.",
            "Survey scrap: chalk arrows left where the route was, not where it is.",
        };

        static readonly string[] AmbiguousArrowWritings =
        {
            "<-- ?", "? -->", "EXIT?", "OUT?", "THIS WAY?", "NOT HERE", "MAYBE", "WRONG", "BACK?", "STAIRS?",
            "<-- OR -->", "X", "CIRCLE BACK", "FOLLOW HUM?", "NO MAP", "MARKED", "SAFE?", "DON'T",
            "WAS EXIT", "NOT EXIT", "TRY LEFT", "TRY RIGHT", "OLD MARK", "NEW MARK",
        };

        static readonly string[] HelpfulArrowHints =
        {
            "UP", "STAIRS", "LIGHT", "OPEN DOOR", "QUIETER", "DRYER", "LOWER HUM", "KEEP LEFT", "KEEP RIGHT",
            "EXIT WALL", "MARKED EXIT", "STAIRS DOWN", "STAIRS UP",
        };

        sealed partial class Plan
        {
            readonly List<BoardSpec> boards = new List<BoardSpec>();
            readonly List<PlankSpec> planks = new List<PlankSpec>();

            /// Everything of stage 4 that lives inside this district.
            void Landmarks()
            {
                // No boarded doors or traces of people in the pristine, empty Poolrooms.
                if (theme.zoneWater || origin0 || forged != null || PoolHallHere) return;
                BoardUpDoorways();
                if (!HasFloors(theme) && theme.id != "19") return;   // traces of people: Level 0 and the attic
                bool level0 = theme.id == "0";
                if (rng.Value() < (level0 ? 0.18f : 0.08f)) Camp();
                if (rng.Value() < (level0 ? 0.14f : 0.08f)) ChairFacingWall();
                if (rng.Value() < (level0 ? 0.18f : 0.08f)) Tally();
                if (level0)
                {
                    for (int i = rng.Range(1, 4); i > 0; i--)
                        if (rng.Value() < 0.6f) WriteOnSomeWall(WallWritings[rng.Range(0, WallWritings.Length)], 1.0f + rng.Value() * 0.65f);
                    if (rng.Value() < 0.18f) WriteOnSomeWall(rng.Value() < 0.5f ? "ORD?" : "SUIT #?", 1.25f + rng.Value() * 0.35f);
                    if (rng.Value() < 0.14f) Tally(); // extra marks, as if someone counted more than once
                    if (rng.Value() < 0.16f) ScatteredCampTrace();
                    if (rng.Value() < 0.22f) AmbiguousMarks();
                    if (rng.Value() < 0.12f) OrdnanceTraceNote();
                    LevelZeroSetpieces();
                }
                ArrowsToStairs();
            }

            // ---------- the Cut ----------

            void BuildCut()
            {
                if (!HasFloors(theme) || !CutAt(levelSeed, dx, dz, theme)) return;
                var r = CutRect;
                int id = NewZone(ZoneKind.Open, r);
                var z = zones[id];
                z.Shaft = true;
                z.Height = FloorHeight - 0.01f;
                z.Dead = 1f;
                Paint(r, id);
                Lock(r);
                MarkVoid(r);
                ReserveInside(r);
                for (int x = r.xMin; x < r.xMax; x++)
                for (int zz = r.yMin; zz < r.yMax; zz++)
                    hole[x, zz] = true;
                // A railing along one side only: the other edges just stop, like a building sliced open.
                int side = (int)(WorldHash.Value(levelSeed, dx, dz, 0xC08 + floor) * 4f);
                for (int i = 0; i < r.width; i++)
                {
                    if (side == 0) Wall(r.xMin + i, r.yMin, r.xMin + i + 1, r.yMin, EdgeKind.LowWall, null, out _);
                    if (side == 1) Wall(r.xMin + i, r.yMax, r.xMin + i + 1, r.yMax, EdgeKind.LowWall, null, out _);
                    if (side == 2) Wall(r.xMin, r.yMin + i, r.xMin, r.yMin + i + 1, EdgeKind.LowWall, null, out _);
                    if (side == 3) Wall(r.xMax, r.yMin + i, r.xMax, r.yMin + i + 1, EdgeKind.LowWall, null, out _);
                }
            }

            // ---------- boards ----------

            /// Planks laid across a pitfall, so you can cross it (carefully).
            void Bridge(RectInt r)
            {
                if (WorldHash.Value(levelSeed, dx * 13 + r.xMin, dz * 7 + r.yMin, 0xB0A + floor) >= 0.5f) return;
                bool alongX = r.width <= r.height;   // span the short way
                float span = (alongX ? r.width : r.height) * C + 0.8f;
                var mid = origin + new Vector3((r.xMin + r.width / 2f) * C, 0.03f, (r.yMin + r.height / 2f) * C);
                for (int i = -1; i <= 1; i++)
                {
                    var c = mid + (alongX ? Vector3.forward : Vector3.right) * (i * 0.17f) + Vector3.up * (i == 0 ? 0.008f : 0f);
                    float yaw = (alongX ? 0f : 90f) + (WorldHash.Value(levelSeed, dx * 31 + i, dz, 0xB0C + floor) - 0.5f) * 6f;
                    planks.Add(new PlankSpec { Pose = Matrix4x4.TRS(c, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Length = span, Variant = i + 1 });
                }
            }

            /// Some doorways are boarded up. One you can't walk around can always be pried open.
            void BoardUpDoorways()
            {
                var doors = new List<int>();
                foreach (var kv in edges)
                {
                    if (kv.Value.Kind != EdgeKind.Doorway) continue;
                    Sides(kv.Key, out var p, out var n);
                    if (!OpenCell(p.x, p.y) || !OpenCell(n.x, n.y) || Void(p.x, p.y) || Void(n.x, n.y)) continue;
                    doors.Add(kv.Key);
                }
                doors.Sort();
                int count = rng.Value() < 0.5f ? 0 : rng.Range(1, 4);
                for (int i = 0; i < count && doors.Count > 0; i++)
                {
                    int key = doors[rng.Range(0, doors.Count)];
                    doors.Remove(key);
                    bool redundant = ConnectedWithout(key);
                    bool pryable = !redundant || rng.Value() < 0.5f;
                    if (!WallFrame(key, rng.Value() < 0.5f, out var pose, out _)) continue;
                    Vector3 at = pose.GetColumn(3);
                    boards.Add(new BoardSpec
                    {
                        Pose = pose,
                        Width = DoorWidth,
                        Pryable = pryable,
                        Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(at.x * 4f), Mathf.RoundToInt(at.z * 4f), 0xB0B + floor),
                        Seed = rng.Range(0, 1000),
                    });
                }
            }

            /// Are the two cells beside `key` still connected inside the district if this edge is closed?
            bool ConnectedWithout(int key)
            {
                Sides(key, out var a, out var b);
                var seen = new bool[D, D];
                var queue = new Queue<Vector2Int>();
                seen[a.x, a.y] = true;
                queue.Enqueue(a);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    if (c == b) return true;
                    for (int s = 0; s < 4; s++)
                    {
                        int nx = c.x + Dx[s], nz = c.y + Dz[s];
                        if (nx < 0 || nz < 0 || nx >= D || nz >= D || seen[nx, nz] || !OpenCell(nx, nz) || hole[nx, nz]) continue;
                        int k = s == 0 ? Key(c.x + 1, c.y, c.x + 1, c.y + 1) : s == 1 ? Key(c.x, c.y + 1, c.x + 1, c.y + 1) : s == 2 ? Key(c.x, c.y, c.x, c.y + 1) : Key(c.x, c.y, c.x + 1, c.y);
                        if (k == key) continue;
                        if (edges.TryGetValue(k, out var e) && Blocking(e.Kind)) continue;
                        seen[nx, nz] = true;
                        queue.Enqueue(new Vector2Int(nx, nz));
                    }
                }
                return false;
            }

            // ---------- signs of people ----------

            /// A free 2 x 2 cell spot in an open area or a room, clear of openings and vertical hazards.
            bool FreeSpot(out int fx, out int fz)
            {
                bool Safe(int x, int z) => FreeStanding(x, z) && !Void(x, z) && !hole[x, z] && !pit[x, z];
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    fx = rng.Range(2, D - 3);
                    fz = rng.Range(2, D - 3);
                    if (Safe(fx, fz) && Safe(fx + 1, fz) && Safe(fx, fz + 1) && Safe(fx + 1, fz + 1)) return true;
                }
                fx = fz = 0;
                return false;
            }

            void Camp()
            {
                if (!FreeSpot(out int x, out int z)) return;
                used[x, z] = used[x + 1, z] = used[x, z + 1] = used[x + 1, z + 1] = true;
                var c = CellCenter(x, z) + new Vector3(C / 2, 0f, C / 2);
                float yaw = rng.Range(0, 4) * 90f + (rng.Value() - 0.5f) * 20f;
                var rot = Quaternion.Euler(0f, yaw, 0f);
                float h = HeightAt(x, z);
                void Put(PropKind k, Vector3 local, float turn = 0f, int variant = -1) =>
                    props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(c + rot * local, rot * Quaternion.Euler(0f, turn, 0f), Vector3.one), Ceiling = h, Variant = variant < 0 ? rng.Range(0, 3) : variant });
                bool level0 = theme.id == "0";

                int style = level0 ? rng.Range(0, 5) : 0;
                switch (style)
                {
                    case 1: // packed up in a hurry, supplies scattered
                        Put(PropKind.CardboardBox, new Vector3(-0.2f, 0f, 0.2f), 12f, 1);
                        Put(PropKind.CardboardBox, new Vector3(0.55f, 0f, -0.35f), -20f, 2);
                        Put(PropKind.Bucket, new Vector3(0.8f, 0f, 0.5f), 0f, 0);
                        Put(PropKind.Can, new Vector3(-0.5f, 0f, -0.6f), 30f, 1);
                        Put(PropKind.Can, new Vector3(0.2f, 0f, -0.85f), 70f, 0);
                        Put(PropKind.Lantern, new Vector3(0.1f, 0f, 0.75f), 0f, 0);
                        break;
                    case 2: // waiting spot, chair and wall marks implied
                        Put(PropKind.WoodChair, new Vector3(-0.45f, 0f, 0.15f), 165f + rng.Value() * 20f, 0);
                        Put(PropKind.CardboardBox, new Vector3(0.7f, 0f, 0.45f), 8f, 0);
                        Put(PropKind.AlmondWater, new Vector3(0.15f, 0f, -0.55f), 0f, 1);
                        Put(PropKind.Can, new Vector3(0.35f, 0f, -0.8f), 40f, 2);
                        if (rng.Value() < 0.6f) Put(PropKind.Battery, new Vector3(-0.1f, 0f, -0.75f), 0f, 1);
                        break;
                    case 3: // makeshift work/survey corner
                        Put(PropKind.Desk, new Vector3(-0.35f, 0f, -0.35f), 10f, 0);
                        Put(PropKind.OfficeChair, new Vector3(-0.25f, 0f, 0.45f), 185f, 0);
                        Put(PropKind.Crate, new Vector3(0.75f, 0f, 0.55f), 0f, 0);
                        Put(PropKind.Lantern, new Vector3(0.35f, 0f, -0.8f), 0f, 0);
                        if (rng.Value() < 0.5f) Put(PropKind.Can, new Vector3(0.6f, 0f, -0.15f), 25f, 0);
                        break;
                    case 4: // abandoned bedding and clutter
                        Put(PropKind.SleepingBag, new Vector3(-0.3f, 0f, 0.1f), 18f, 0);
                        Put(PropKind.CardboardBox, new Vector3(0.65f, 0f, -0.2f), 35f, 1);
                        Put(PropKind.TrashCan, new Vector3(-0.85f, 0f, -0.65f), 0f, 0);
                        Put(PropKind.Can, new Vector3(0.2f, 0f, -0.85f), 60f, 1);
                        Put(PropKind.Can, new Vector3(0.45f, 0f, -0.65f), 15f, 0);
                        break;
                    default:
                        if (rng.Value() < 0.7f) Put(PropKind.SleepingBag, new Vector3(-0.45f + rng.Value() * 0.2f, 0f, rng.Value() * 0.2f), rng.Value() * 18f - 9f);
                        else Put(PropKind.WoodChair, new Vector3(-0.55f, 0f, 0.2f), 140f + rng.Value() * 40f);
                        if (rng.Value() < 0.85f) Put(PropKind.CardboardBox, new Vector3(0.75f, 0f, 0.55f), 10f + rng.Value() * 30f);
                        if (rng.Value() < 0.75f) Put(PropKind.Lantern, new Vector3(0.55f, 0f, -0.35f + rng.Value() * 0.2f));
                        for (int i = rng.Range(1, 6); i > 0; i--) Put(PropKind.Can, new Vector3(0.2f + rng.Value() * 0.9f, 0f, -0.95f + rng.Value() * 0.7f), rng.Value() * 360f);
                        if (rng.Value() < 0.55f) Put(PropKind.AlmondWater, new Vector3(0.95f, 0f, 0.05f + rng.Value() * 0.2f));
                        if (rng.Value() < 0.35f) Put(PropKind.Battery, new Vector3(-0.1f, 0f, -0.8f), rng.Value() * 360f);
                        if (level0 && rng.Value() < 0.25f) Put(PropKind.Bucket, new Vector3(0.1f, 0f, 0.95f), rng.Value() * 360f);
                        if (level0 && rng.Value() < 0.2f) Put(PropKind.TrashCan, new Vector3(-1.0f, 0f, -0.7f), rng.Value() * 360f);
                        break;
                }

                readables.Add(new ReadableSpec
                {
                    Kind = ReadableKind.Note,
                    Pose = Matrix4x4.TRS(c + rot * new Vector3(-0.1f, 0.01f, 0.7f), rot, Vector3.one),
                    Title = rng.Value() < 0.25f ? "A worn page" : "A note at an old camp",
                    Text = CampNotes[rng.Range(0, CampNotes.Length)],
                });
                if (level0 && rng.Value() < 0.45f)
                {
                    // Scattered paper or tally-like trace near the camp.
                    string t = rng.Value() < 0.45f ? WallWritings[rng.Range(0, WallWritings.Length)]
                        : rng.Value() < 0.7f ? "|||| ||" : AmbiguousArrowWritings[rng.Range(0, AmbiguousArrowWritings.Length)];
                    readables.Add(new ReadableSpec
                    {
                        Kind = rng.Value() < 0.55f ? ReadableKind.Writing : ReadableKind.Note,
                        Pose = Matrix4x4.TRS(c + rot * new Vector3(rng.Value() * 0.8f - 0.4f, rng.Value() < 0.5f ? 0.01f : 1.1f, rng.Value() * 0.8f - 0.4f), rot * Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one),
                        Title = "Scrap",
                        Text = t,
                    });
                }
                if (level0 && rng.Value() < 0.18f)
                {
                    readables.Add(new ReadableSpec
                    {
                        Kind = ReadableKind.Note,
                        Pose = Matrix4x4.TRS(c + rot * new Vector3(0.35f, 0.01f, -0.2f), rot * Quaternion.Euler(0f, 25f, 0f), Vector3.one),
                        Title = "Survey scrap",
                        Text = SurveyNotes[rng.Range(0, SurveyNotes.Length)],
                    });
                }
            }

            /// Small traces left behind without a full camp.
            void ScatteredCampTrace()
            {
                if (!FreeSpot(out int x, out int z)) return;
                used[x, z] = true;
                var c = CellCenter(x, z);
                float h = HeightAt(x, z);
                void Put(PropKind k, Vector3 local, float turn = 0f) =>
                    props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(c + local, Quaternion.Euler(0f, turn, 0f), Vector3.one), Ceiling = h, Variant = rng.Range(0, 3) });
                if (rng.Value() < 0.55f) Put(PropKind.CardboardBox, new Vector3(0.1f, 0f, 0.1f), rng.Value() * 360f);
                if (rng.Value() < 0.55f) Put(PropKind.Can, new Vector3(-0.25f, 0f, 0.2f), rng.Value() * 360f);
                if (rng.Value() < 0.4f) Put(PropKind.AlmondWater, new Vector3(0.3f, 0f, -0.2f), rng.Value() * 360f);
                if (rng.Value() < 0.3f) Put(PropKind.Battery, new Vector3(0.0f, 0f, -0.35f), rng.Value() * 360f);
                if (rng.Value() < 0.2f) Put(PropKind.Bucket, new Vector3(-0.1f, 0f, -0.1f), rng.Value() * 360f);
                if (rng.Value() < 0.25f) Put(PropKind.Phone, new Vector3(0.15f, 0f, 0.35f), rng.Value() * 360f);
                if (rng.Value() < 0.55f)
                {
                    string t = rng.Value() < 0.35f ? "THIS WAY?" : rng.Value() < 0.55f ? "NO WAY BACK"
                        : rng.Value() < 0.75f ? AmbiguousArrowWritings[rng.Range(0, AmbiguousArrowWritings.Length)]
                        : HelpfulArrowHints[rng.Range(0, HelpfulArrowHints.Length)] + "?";
                    WriteOnSomeWall(t, 1.05f + rng.Value() * 0.45f);
                }
                if (rng.Value() < 0.25f) Tally();
            }

            /// A brief ORD/survey trace: equipment or paperwork without a clear story.
            void OrdnanceTraceNote()
            {
                if (!FreeSpot(out int x, out int z)) return;
                used[x, z] = true;
                var c = CellCenter(x, z);
                float h = HeightAt(x, z);
                var rot = Quaternion.Euler(0f, rng.Range(0, 360), 0f);
                void Put(PropKind k, Vector3 local, float turn = 0f, int variant = -1) =>
                    props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(c + rot * local, rot * Quaternion.Euler(0f, turn, 0f), Vector3.one), Ceiling = h, Variant = variant < 0 ? rng.Range(0, 3) : variant });
                Put(PropKind.Crate, new Vector3(-0.15f, 0f, 0.05f), 8f, 0);
                if (rng.Value() < 0.6f) Put(PropKind.CardboardBox, new Vector3(0.45f, 0f, -0.25f), -15f, 1);
                if (rng.Value() < 0.5f) Put(PropKind.Bucket, new Vector3(0.2f, 0f, 0.55f), 0f, 0);
                if (rng.Value() < 0.45f) Put(PropKind.Lantern, new Vector3(-0.55f, 0f, -0.35f), 0f, 0);
                if (rng.Value() < 0.35f) Put(PropKind.Can, new Vector3(-0.2f, 0f, -0.65f), 30f, 1);
                readables.Add(new ReadableSpec
                {
                    Kind = ReadableKind.Note,
                    Pose = Matrix4x4.TRS(c + rot * new Vector3(0.05f, 0.01f, 0.25f), rot, Vector3.one),
                    Title = rng.Value() < 0.5f ? "ORD field copy" : "Field scrap",
                    Text = SurveyNotes[rng.Range(0, SurveyNotes.Length)],
                });
                if (rng.Value() < 0.45f) WriteOnSomeWall(AmbiguousArrowWritings[rng.Range(0, AmbiguousArrowWritings.Length)], 1.2f + rng.Value() * 0.3f);
            }

            /// Distinct, compact scenes that make familiar office rooms feel used, hostile and unreliable.
            /// Each takes a clear 4 x 4 m island of floor, so none can close a route or block an exit.
            void LevelZeroSetpieces()
            {
                // One to three deliberate scenes per district, in addition to the smaller scattered details.
                for (int i = rng.Range(1, 4); i > 0; i--)
                {
                    if (!FreeSpot(out int x, out int z)) return;
                    used[x, z] = used[x + 1, z] = used[x, z + 1] = used[x + 1, z + 1] = true;
                    var centre = CellCenter(x, z) + new Vector3(C / 2f, 0f, C / 2f);
                    var rotation = Quaternion.Euler(0f, rng.Range(0, 4) * 90f, 0f);
                    float height = HeightAt(x, z);
                    void Put(PropKind kind, Vector3 local, float turn = 0f, int variant = 0) =>
                        props.Add(new PropSpec { Kind = kind, Pose = Matrix4x4.TRS(centre + rotation * local, rotation * Quaternion.Euler(0f, turn, 0f), Vector3.one), Ceiling = height, Variant = variant });

                    switch (rng.Range(0, 8))
                    {
                        case 0: // Failed ceiling repair: wet carpet, dead light, ladder left behind.
                            Put(PropKind.Ladder, new Vector3(-1.1f, 0f, 0.25f), -12f);
                            Put(PropKind.Bucket, new Vector3(0.7f, 0f, 0.7f), 0f, rng.Range(0, 3));
                            Put(PropKind.WetFloorSign, new Vector3(0.95f, 0f, -0.55f));
                            Put(PropKind.FallenTile, new Vector3(-0.55f, 0.015f, -0.7f), rng.Range(0, 360));
                            Put(PropKind.FallenTile, new Vector3(0.05f, 0.02f, -0.35f), rng.Range(0, 360));
                            Put(PropKind.HangingWires, new Vector3(0.35f, 0f, -0.4f), 0f, rng.Range(0, 12));
                            break;

                        case 1: // ORD field desk, abandoned mid-survey; paperwork stays inconclusive.
                            Put(PropKind.Desk, new Vector3(-0.55f, 0f, -0.55f));
                            Put(PropKind.OfficeChair, new Vector3(-0.55f, 0f, 0.35f), 180f);
                            Put(PropKind.FilingCabinet, new Vector3(0.95f, 0f, -0.45f), 90f);
                            Put(PropKind.CardboardBox, new Vector3(0.7f, 0f, 0.65f), rng.Range(0, 360), 1);
                            Put(PropKind.Phone, new Vector3(-0.15f, 0.76f, -0.55f));
                            if (rng.Value() < 0.8f)
                                readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(centre + rotation * new Vector3(0.15f, 0.015f, 0.75f), rotation, Vector3.one), Title = "ORD field copy", Text = SurveyNotes[rng.Range(0, SurveyNotes.Length)] });
                            break;

                        case 2: // A meeting was arranged around an empty space and never resumed.
                            Put(PropKind.OfficeChair, new Vector3(-0.75f, 0f, -0.7f), 0f);
                            Put(PropKind.OfficeChair, new Vector3(0.75f, 0f, -0.7f), 0f);
                            Put(PropKind.OfficeChair, new Vector3(-0.75f, 0f, 0.55f), 180f);
                            Put(PropKind.OfficeChair, new Vector3(0.75f, 0f, 0.55f), 180f);
                            Put(PropKind.TrashCan, new Vector3(0f, 0f, 0f), 0f, rng.Range(0, 12));
                            if (rng.Value() < 0.4f)
                                readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(centre + rotation * new Vector3(0f, 0.015f, 0.9f), rotation, Vector3.one), Title = "Unsent note", Text = "They said to wait here. I don't know who 'they' meant." });
                            break;

                        case 3: // An unattached doorway has been treated like a real entrance.
                            Put(PropKind.LoneDoor, Vector3.zero, rng.Range(0, 4) * 90f);
                            Put(PropKind.WoodChair, new Vector3(-1.2f, 0f, 0.55f), 180f);
                            Put(PropKind.TilePile, new Vector3(0.95f, 0f, -0.75f), 0f, rng.Range(0, 12));
                            if (rng.Value() < 0.35f)
                                readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(centre + rotation * new Vector3(1.0f, 0.015f, 0.65f), rotation, Vector3.one), Title = "Unsent note", Text = "This door is not on the plan. Do not number it." });
                            break;

                        case 4: // Boiler-room equipment in an office, with the leak stopping just short of it.
                            Put(PropKind.Boiler, new Vector3(0.65f, 0f, 0.55f), rng.Range(0, 4) * 90f);
                            Put(PropKind.Bucket, new Vector3(-0.8f, 0f, 0.5f), 0f, rng.Range(0, 3));
                            Put(PropKind.CeilingPipes, new Vector3(-0.1f, 0f, -0.8f), 0f, rng.Range(0, 12));
                            Put(PropKind.WetFloorSign, new Vector3(-0.95f, 0f, -0.55f));
                            readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(centre + rotation * new Vector3(-0.3f, 0.015f, 0.9f), rotation, Vector3.one), Title = "Maintenance tag", Text = "Pressure is still rising. This is not connected to the building plan." });
                            break;

                        case 5: // Paper clusters and office clutter gathered in wrong places.
                            Put(PropKind.Desk, new Vector3(-0.7f, 0f, -0.45f), 15f);
                            Put(PropKind.CardboardBox, new Vector3(0.2f, 0f, 0.55f), 5f, 1);
                            Put(PropKind.CardboardBox, new Vector3(0.55f, 0f, 0.2f), -20f, 0);
                            Put(PropKind.OfficeChair, new Vector3(0.8f, 0f, -0.55f), 170f);
                            Put(PropKind.Phone, new Vector3(-0.2f, 0.76f, -0.35f), 40f);
                            if (rng.Value() < 0.35f) WriteOnSomeWall(AmbiguousArrowWritings[rng.Range(0, AmbiguousArrowWritings.Length)], 1.15f);
                            break;

                        case 6: // Electronics arranged wrong: screens, phones, no reason.
                            Put(PropKind.CrtTv, new Vector3(-0.55f, 0f, 0.2f), 25f);
                            Put(PropKind.CrtTv, new Vector3(0.55f, 0f, -0.35f), 200f);
                            Put(PropKind.Phone, new Vector3(0.1f, 0f, 0.7f), 60f);
                            Put(PropKind.Phone, new Vector3(-0.2f, 0f, -0.7f), 15f);
                            if (rng.Value() < 0.25f) Put(PropKind.CardboardBox, new Vector3(0.9f, 0f, 0.6f), 0f, 2);
                            break;

                        default: // A paper-and-furniture spill, partially sorted into neat rows.
                            Put(PropKind.FilingCabinet, new Vector3(-0.85f, 0f, -0.55f), 0f);
                            Put(PropKind.FilingCabinet, new Vector3(-0.85f, 0f, 0.45f), 0f);
                            Put(PropKind.CardboardBox, new Vector3(0.45f, 0f, 0.55f), 15f, 1);
                            Put(PropKind.OfficeChair, new Vector3(0.8f, 0f, -0.65f), 165f);
                            Put(PropKind.Phone, new Vector3(0.15f, 0f, -0.15f));
                            Put(PropKind.Can, new Vector3(0.55f, 0f, 0.1f), rng.Value() * 360f, rng.Range(0, 3));
                            if (rng.Value() < 0.35f)
                                readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(centre + rotation * new Vector3(0.2f, 0.015f, 0.95f), rotation, Vector3.one), Title = "Inventory slip", Text = "One chair missing. The count was checked twice." });
                            break;
                    }
                }
            }

            /// A single chair pushed up to a wall, facing it.
            void ChairFacingWall()
            {
                var walls = new List<int>();
                foreach (var kv in edges) if (kv.Value.Kind == EdgeKind.Wall) walls.Add(kv.Key);
                walls.Sort();
                for (int attempt = 0; attempt < 10 && walls.Count > 0; attempt++)
                {
                    int key = walls[rng.Range(0, walls.Count)];
                    if (!WallFrame(key, rng.Value() < 0.5f, out var pose, out var cell) || used[cell.x, cell.y] || Void(cell.x, cell.y)) continue;
                    used[cell.x, cell.y] = true;
                    // The frame's +z points out of the wall; the chair sits 0.5 m out and turns to face the wall.
                    props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = pose * Matrix4x4.TRS(new Vector3(0f, 0f, 0.5f), Quaternion.Euler(0f, 180f, 0f), Vector3.one), Ceiling = HeightAt(cell.x, cell.y) });
                    if (theme.id == "0" && rng.Value() < 0.25f) WriteOnSomeWall(rng.Value() < 0.5f ? "WAIT" : "COUNTING", 1.15f);
                    return;
                }
            }

            /// Somebody counting days on a wall.
            void Tally()
            {
                int days = rng.Range(5, 48);
                var sb = new System.Text.StringBuilder();
                for (int i = 1; i <= days; i++) sb.Append(i % 5 == 0 ? "/ " : "|");
                if (rng.Value() < 0.18f) sb.Append(rng.Value() < 0.5f ? " ?" : " //");
                WriteOnSomeWall(sb.ToString(), 1.15f + rng.Value() * 0.15f);
            }

            /// Ambiguous, sometimes misleading marks and notes that still occasionally help.
            void AmbiguousMarks()
            {
                int count = rng.Range(1, 4);
                for (int i = 0; i < count; i++)
                {
                    if (!SomeWall(out var pose, null)) continue;
                    string text;
                    float r = rng.Value();
                    if (r < 0.45f) text = AmbiguousArrowWritings[rng.Range(0, AmbiguousArrowWritings.Length)];
                    else if (r < 0.7f) text = WallWritings[rng.Range(0, WallWritings.Length)];
                    else text = HelpfulArrowHints[rng.Range(0, HelpfulArrowHints.Length)] + (rng.Value() < 0.4f ? "?" : "");
                    readables.Add(new ReadableSpec { Kind = rng.Value() < 0.8f ? ReadableKind.Writing : ReadableKind.Note, Pose = pose * Matrix4x4.Translate(new Vector3((rng.Value() - 0.5f) * 0.3f, 0.95f + rng.Value() * 0.7f, 0f)), Title = "Mark", Text = text });
                }
                if (rng.Value() < 0.3f && SomeWall(out var np, null))
                    readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = np * Matrix4x4.Translate(new Vector3(0f, 0f, 0.5f)), Title = "Loose note", Text = CampNotes[rng.Range(0, CampNotes.Length)] });
            }

            /// Arrows scratched on walls, pointing along the wall toward this floor's stairs up.
            /// Some are helpful, some are ambiguous or wrong.
            void ArrowsToStairs()
            {
                if (!HasFloors(theme)) return;
                var stairs = new List<Vertical>();
                VerticalsBetween(levelSeed, dx, dz, floor, theme, stairs);
                foreach (var v in stairs)
                {
                    if (v.Kind != VerticalKind.Stair || rng.Value() > 0.7f) continue;
                    var target = origin + new Vector3((v.Rect.xMin + v.Rect.width / 2f) * C, 0f, (v.Rect.yMin + v.Rect.height / 2f) * C);
                    for (int i = 0; i < 2; i++)
                    {
                        // A wall within 22 m of the stairs; marks may help or mislead.
                        if (!SomeWall(out var pose, w => Vector3.Distance(w, target) < 22f && Vector3.Distance(w, target) > 4f)) continue;
                        float side = Vector3.Dot(target - (Vector3)pose.GetColumn(3), pose.GetColumn(0));
                        bool correct = side > 0f;
                        float r = rng.Value();
                        string arrow;
                        if (r < 0.55f) arrow = correct ? "<-- UP" : "UP -->";          // helpful
                        else if (r < 0.72f) arrow = correct ? "UP -->" : "<-- UP";     // misleading/wrong direction
                        else if (r < 0.86f) arrow = rng.Value() < 0.5f ? "STAIRS?" : "UP?";
                        else arrow = AmbiguousArrowWritings[rng.Range(0, AmbiguousArrowWritings.Length)];
                        readables.Add(new ReadableSpec { Kind = ReadableKind.Writing, Pose = pose * Matrix4x4.Translate(new Vector3(0f, 1.25f + rng.Value() * 0.2f, 0f)), Text = arrow });
                    }
                }
            }

            static readonly string[] ExitNotes =
            {
                "The doors with a light over them are the ones that open. Everything else is painted shut.",
                "Followed the chalk for an hour. It works. Add your own arrows if you get out.",
                "If a door won't open, lean on it. Some of them give. Loud though.",
                "Hot down the dead ends. Don't stay near the boilers.",
                "Number over the door is where it goes. 1 goes back up to the warehouse.",
                "Some arrows were right. Some were from before the room changed.",
                "I marked the way out twice. Only one set still makes sense.",
                "If the marks disagree, trust the light and the number, not the chalk.",
            };

            /// Chalk arrows from earlier wanderers pointing toward this district's exit doors, and sometimes a note.
            /// Some nearby marks are helpful (including to exits), others are ambiguous or conflicting.
            void ExitTrails()
            {
                if (!ds.markedExits) return;
                // Nothing runs across a way out: take the wall pipes off the stretch of wall an exit door is on.
                for (int i = 0; i < props.Count; i++)
                {
                    if (props[i].Kind != PropKind.WallPipes) continue;
                    Vector3 at = props[i].Pose.GetColumn(3);
                    foreach (var e in exits)
                        if (e.Kind == ExitKind.Door && Vector3.Distance(at, e.Pose.GetColumn(3)) < 1.5f) { removedProps.Add(i); break; }
                }
                foreach (var e in exits.ToArray())
                {
                    if (e.Kind != ExitKind.Door) continue;
                    var target = (Vector3)e.Pose.GetColumn(3);
                    int arrows = rng.Range(2, 5);
                    for (int i = 0; i < arrows; i++)
                    {
                        if (!SomeWall(out var pose, w => Vector3.Distance(w, target) < 30f && Vector3.Distance(w, target) > 5f)) continue;
                        float side = Vector3.Dot(target - (Vector3)pose.GetColumn(3), pose.GetColumn(0));
                        bool correct = side > 0f;
                        string[] left = { "<-- EXIT", "<-- WAY OUT", "<-- " + e.Target + " THIS WAY", "EXIT?" }, right = { "EXIT -->", "WAY OUT -->", "THIS WAY " + e.Target + " -->", "EXIT?" };
                        string text;
                        float rr = rng.Value();
                        if (rr < 0.68f) text = (correct ? left : right)[rng.Range(0, 3)]; // helpful
                        else if (rr < 0.84f) text = (correct ? left : right)[3];           // ambiguous
                        else text = (!correct ? left : right)[rng.Range(0, 3)];            // conflicting/misleading
                        readables.Add(new ReadableSpec { Kind = ReadableKind.Writing, Pose = pose * Matrix4x4.Translate(new Vector3(0f, 1.1f + rng.Value() * 0.5f, 0f)), Text = text });
                    }
                    if (rng.Value() < 0.45f && SomeWall(out var np, w => Vector3.Distance(w, target) < 15f && Vector3.Distance(w, target) > 3f))
                        readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = np * Matrix4x4.Translate(new Vector3(0f, 0f, 0.5f)), Title = "Scrawled note", Text = ExitNotes[rng.Range(0, ExitNotes.Length)] });
                }
            }

            void WriteOnSomeWall(string text, float height)
            {
                if (SomeWall(out var pose, null))
                    readables.Add(new ReadableSpec { Kind = ReadableKind.Writing, Pose = pose * Matrix4x4.Translate(new Vector3(0f, height, 0f)), Text = text });
            }

            bool SomeWall(out Matrix4x4 pose, System.Func<Vector3, bool> accept)
            {
                var walls = new List<int>();
                foreach (var kv in edges) if (kv.Value.Kind == EdgeKind.Wall) walls.Add(kv.Key);
                walls.Sort();
                for (int attempt = 0; attempt < 20 && walls.Count > 0; attempt++)
                {
                    int key = walls[rng.Range(0, walls.Count)];
                    if (!WallFrame(key, rng.Value() < 0.5f, out pose, out var cell) || Void(cell.x, cell.y)) continue;
                    if (accept != null && !accept(pose.GetColumn(3))) continue;
                    return true;
                }
                pose = default;
                return false;
            }
        }
    }
}
