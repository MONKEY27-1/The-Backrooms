using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Details from the Backrooms Wiki's level pages (backrooms-wiki.wikidot.com, CC BY-SA 3.0):
    /// - Level 0: always-moist carpet (glossy damp patches), Arches (pale walls full of archways, the wettest and
    ///   most stable spots), Blackout Zones (unlit, rough walls, recessed floors with ankle-deep fluid), Red Rooms
    ///   (red paper and sticky red carpet, walled off so they're very hard to leave), and the Peripheral Shift: rooms
    ///   you've left can rearrange while nobody is looking.
    /// - Level 0.3 "The Icy Rooms": frosted bare walls, rusty ducts, huge ice blocks nearly filling small rooms.
    public static partial class DistrictLayout
    {
        public enum Mood : byte { Normal, Blackout, Red }

        // ---------- the Peripheral Shift ----------

        static readonly Dictionary<(uint, int, int), int> epochs = new Dictionary<(uint, int, int), int>();

        static int EpochOf(uint floorSeed, int dx, int dz) { lock (epochs) return epochs.TryGetValue((floorSeed, dx, dz), out int e) ? e : 0; }

        /// A district nobody is looking at rearranges: next time it's built, it's built differently (border walls
        /// stay put, so neighbours still fit). Level 0 only, never the photo rooms or hand-made rooms.
        public static void Shift(uint levelSeed, int floor, int dx, int dz, LevelTheme theme)
        {
            if (theme == null || theme.id != "0" || (dx == 0 && dz == 0 && floor == 0)) return;
            if (floor == 0 && ForgeLibrary.RoomAt(levelSeed, dx, dz, theme) != null) return;
            var key = (FloorSeed(levelSeed, floor), dx, dz);
            lock (epochs) epochs[key] = (epochs.TryGetValue(key, out int e) ? e : 0) + 1;
        }

        public static void ForgetShifts() { lock (epochs) epochs.Clear(); }

        sealed partial class Plan
        {
            readonly Mood[,] mood = new Mood[D, D];
            readonly List<Rect> damp = new List<Rect>();

            /// Blackout Zones and Red Rooms as regions of Level 0 (from the region sites), applied after the archetypes.
            void ApplyMoods()
            {
                if (!ds.wikiZones) return;
                foreach (var z in zones)
                {
                    if (z.Kind == ZoneKind.Origin || z.Shaft || z.Rect.width <= 0) continue;
                    var c = new Vector2Int(Mathf.Clamp((int)z.Rect.center.x, 0, D - 1), Mathf.Clamp((int)z.Rect.center.y, 0, D - 1));
                    switch (mood[c.x, c.y])
                    {
                        case Mood.Blackout:
                            z.Dead = 1f;
                            z.Pool = false;
                            z.Wall = new SurfaceRef(Surface.Wall, Mathf.Min(5, wallpapers - 1));   // the worn, stained paper: rough
                            if (z.Sunk <= 0f) { z.Sunk = 0.15f; z.Water = -0.05f; }                // recessed, ankle-deep fluid
                            break;
                        case Mood.Red:
                            z.Red = true;
                            z.Wall = new SurfaceRef(Surface.RedWall);
                            z.Floor = new SurfaceRef(Surface.RedFloor);
                            break;
                    }
                }
            }

            // ---------- Arches ----------

            /// Pale rooms opening into each other through archways; the carpet here is the wettest.
            void BuildArches()
            {
                int step = 4;
                for (int line = step; line < D - 1; line += step)
                    for (int i = 1; i < D - 1; i++)
                    {
                        foreach (bool alongX in new[] { true, false })
                        {
                            int x0 = alongX ? i : line, z0 = alongX ? line : i, x1 = alongX ? i + 1 : line, z1 = alongX ? line : i + 1;
                            if (rng.Value() < 0.6f) Opening(x0, z0, x1, z1, EdgeKind.Arch);
                            Wall(x0, z0, x1, z1, EdgeKind.Arch);
                        }
                    }
                for (int x = 0; x < D; x += step)
                for (int z = 0; z < D; z += step)
                {
                    var r = new RectInt(x, z, Mathf.Min(step, D - x), Mathf.Min(step, D - z));
                    int id = NewZone(ZoneKind.Room, r);
                    var zz = zones[id];
                    zz.Wall = new SurfaceRef(Surface.Wall, Mathf.Min(8, wallpapers - 1));   // plain pale paper
                    zz.Floor = new SurfaceRef(Surface.Floor, Mathf.Min(1, carpets - 1));    // the stained, wetter carpet
                    zz.Height = 2.6f;
                    zz.Lights = LightLayout.Grid;
                    zz.Damp = 0.35f;
                    Paint(r, id);
                }
            }

            // ---------- the always-moist carpet ----------

            /// Glossy damp patches on Level 0's carpet (more in the Arches and Blackout Zones).
            void DampPatches()
            {
                if (!ds.wikiZones) return;
                foreach (var z in zones)
                {
                    if (z.Shaft || z.Rect.width <= 0 || z.Floor.Kind != Surface.Floor) continue;
                    float chance = 0.12f + z.Damp;
                    int area = z.Rect.width * z.Rect.height;
                    for (int n = Mathf.Max(1, area / 10); n > 0; n--)
                    {
                        if (rng.Value() > chance) continue;
                        int x = rng.Range(z.Rect.xMin, z.Rect.xMax), zz = rng.Range(z.Rect.yMin, z.Rect.yMax);
                        if (!OpenCell(x, zz) || zone[x, zz] != zones.IndexOf(z) || Void(x, zz)) continue;
                        float w = 0.6f + rng.Value() * 1.4f, d = 0.5f + rng.Value() * 1.2f;
                        var c = CellCenter(x, zz) + new Vector3((rng.Value() - 0.5f) * 0.8f, 0f, (rng.Value() - 0.5f) * 0.8f);
                        damp.Add(new Rect(c.x - w / 2, c.z - d / 2, w, d));
                    }
                }
            }

            // ---------- Level 0.3: The Icy Rooms ----------

            /// Huge blocks of ice nearly filling the small rooms; rusty ducts along the ceilings.
            void IceRooms()
            {
                if (theme.id != "0.3") return;
                for (int id = 0; id < zones.Count; id++)
                {
                    var z = zones[id];
                    if (z.Rect.width <= 0 || z.Shaft) continue;
                    var c = origin + new Vector3((z.Rect.xMin + z.Rect.width / 2f) * C, 0f, (z.Rect.yMin + z.Rect.height / 2f) * C);
                    int area = z.Rect.width * z.Rect.height;
                    bool small = z.Kind != ZoneKind.Open && z.Kind != ZoneKind.Maze && z.Kind != ZoneKind.Corridor && area <= 16;
                    if (small && rng.Value() < 0.7f)
                    {
                        // Nearly fills the room, but leaves about a metre inside every doorway to squeeze past.
                        float sx = z.Rect.width * C - 2.3f - rng.Value() * 0.5f;
                        float sz = z.Rect.height * C - 2.3f - rng.Value() * 0.5f;
                        if (sx < 0.8f || sz < 0.8f) continue;
                        float h = Mathf.Max(1.2f, z.Height - 0.25f - rng.Value() * 0.6f);
                        var where = c + new Vector3(0f, 0f, 0f);
                        var ice = new Bounds(where + Vector3.up * h / 2, new Vector3(sx, h, sz));
                        bool blocksDoor = false;
                        foreach (var kv in edges) if (IsOpening(kv.Value.Kind) && PassageBounds(kv.Key).Intersects(ice)) { blocksDoor = true; break; }
                        if (blocksDoor) continue;
                        props.Add(new PropSpec { Kind = PropKind.IceBlock, Pose = Matrix4x4.TRS(where, Quaternion.identity, new Vector3(sx, h, sz)), Ceiling = z.Height });
                        blockers.Add(new Bounds(c + Vector3.up * h / 2, new Vector3(sx, h, sz)));
                    }
                    // A rusty duct running along the room, just under the ceiling, breathing cold mist.
                    if (rng.Value() < 0.6f)
                    {
                        bool alongX = z.Rect.width >= z.Rect.height;
                        int len = alongX ? z.Rect.width : z.Rect.height;
                        for (int i = 0; i < len; i++)
                        {
                            var p = alongX ? origin + new Vector3((z.Rect.xMin + i + 0.5f) * C, 0f, (z.Rect.yMin + 0.5f) * C)
                                           : origin + new Vector3((z.Rect.xMin + 0.5f) * C, 0f, (z.Rect.yMin + i + 0.5f) * C);
                            props.Add(new PropSpec { Kind = PropKind.Duct, Variant = i, Pose = Matrix4x4.TRS(p, Quaternion.Euler(0f, alongX ? 0f : 90f, 0f), Vector3.one), Ceiling = z.Height });
                        }
                    }
                }
            }
        }
    }
}
