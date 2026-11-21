using UnityEngine;

namespace Backrooms.World
{
    /// Copy drift: the further you wander, the worse the building remembers itself. Each district gets a drift
    /// value (0 = faithful, 1 = barely a memory) and a set of mutation operators whose chance scales with it.
    /// Everything is still a pure function of the seed, so a place drifts the same way every visit.
    public static partial class DistrictLayout
    {
        /// Drift rises with distance from the origin: noticeable after ~300 m, strong after ~2 km. Some districts
        /// lurch ahead or lag behind. Sub-levels start already drifted (theme.baseDrift).
        public static float DriftAt(uint seed, int dx, int dz, LevelTheme theme)
        {
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float noise = (WorldHash.Value(seed, dx, dz, 909) - 0.5f) * 0.25f;
            return Mathf.Clamp01(theme.baseDrift + distance * 0.03f - 0.08f + noise);
        }

        public static float DriftAtChunk(uint seed, int cx, int cz, LevelTheme theme) =>
            theme.generator == LevelGenerator.Districts ? DriftAt(seed, FloorDiv(cx, 2), FloorDiv(cz, 2), theme) : theme.baseDrift;

        sealed partial class Plan
        {
            /// The mutation operators that change structure and surfaces. (Others live where they act:
            /// lumpy ceilings in HeightAt, off-grid panels in LightZone, extra glitched props and wrongness.)
            void ApplyDrift()
            {
                if (drift < 0.05f) return;
                var keys = new System.Collections.Generic.List<int>(edges.Keys);
                foreach (int key in keys)
                {
                    var e = edges[key];
                    if (e.Kind == EdgeKind.Wall)
                    {
                        if (rng.Value() < drift * 0.25f) e.Short = true;          // stops short of the ceiling
                        else if (rng.Value() < drift * 0.15f) e.Doubled = true;   // drawn twice, shimmering
                    }
                    else if (e.Kind == EdgeKind.Doorway && rng.Value() < drift * 0.4f)
                        e.Header = rng.Value() < 0.5f ? 1.55f : 2.7f;            // a door you must duck through, or one too tall
                    edges[key] = e;
                }

                foreach (var z in zones)
                {
                    if (z.Kind == ZoneKind.Manila) continue;
                    if (rng.Value() < drift * 0.3f) z.Sick = true;                // the light turns the wrong colour
                    if (rng.Value() < drift * 0.12f && drift > 0.25f && z.Rect.width * z.Rect.height <= 64) z.NoCeiling = true;   // nothing above but black (small rooms, far out)
                    if (!z.Red && rng.Value() < drift * 0.15f)                    // wallpaper on the floor, carpet on the walls
                        (z.Wall, z.Floor) = (new SurfaceRef(Surface.Floor, z.Floor.Variant), new SurfaceRef(Surface.Wall, z.Wall.Variant));
                }

                // Pillars that hang from the ceiling and stop above your head.
                for (int i = Mathf.FloorToInt(drift * 8f); i > 0; i--)
                {
                    int x = rng.Range(1, D - 1), z = rng.Range(1, D - 1);
                    if (!FreeStanding(x, z)) continue;
                    used[x, z] = true;
                    float h = HeightAt(x, z), bottom = 2.0f + rng.Value() * 0.4f;
                    var c = CellCenter(x, z);
                    hangers.Add((new Bounds(new Vector3(c.x, (bottom + h) / 2f, c.z), new Vector3(0.7f, h - bottom, 0.7f)), zone[x, z]));
                }

                // The same object, repeated in a row, as if the copy got stuck.
                if (rng.Value() < drift * 0.6f)
                {
                    var kinds = new[] { PropKind.WoodChair, PropKind.OfficeChair, PropKind.TrashCan, PropKind.WetFloorSign, PropKind.Bucket };
                    var kind = kinds[rng.Range(0, kinds.Length)];
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        int x = rng.Range(2, D - 3), z = rng.Range(1, D - 1);
                        if (!FreeStanding(x, z) || !FreeStanding(x + 1, z)) continue;
                        used[x, z] = used[x + 1, z] = true;
                        var start = CellCenter(x, z) + new Vector3(-0.8f, 0f, 0f);
                        for (int k = 0; k < 7; k++)
                            props.Add(new PropSpec { Kind = kind, Pose = Matrix4x4.TRS(start + new Vector3(0.6f * k, 0f, 0f), Quaternion.identity, Vector3.one), Ceiling = HeightAt(x, z) });
                        break;
                    }
                }
            }
        }
    }

    /// The original rooms: a recreation of the two famous Level 0 photos at the world origin. Every new game
    /// starts here, and they never drift: this is the source everything else is a copy of.
    ///
    /// Photo 1: you stand in a short opening between two wall ends (small-dot paper with a low rail on the left,
    /// pale ogee/chevron paper with an outlet on the right) looking into a big carpeted hall: a free-standing
    /// partition, walls at different depths, a row of strip lights running away.
    /// Photo 2: next door, a long room whose right side is an arcade (half walls between pillars, arches above)
    /// onto a row of alcoves; a plain far wall; a trash can and a bucket by the left wall.
    public static partial class DistrictLayout
    {
        /// Where a new game puts you: standing where the first photo was taken.
        public static readonly Vector3 OriginSpawn = new Vector3(22f, 0.05f, 21f);
        public const float OriginYaw = 0f;

        sealed partial class Plan
        {
            Zone OriginZone(RectInt r, int paper, int carpet, float height, bool rail)
            {
                int id = NewZone(ZoneKind.Origin, r);
                var z = zones[id];
                z.Wall = new SurfaceRef(Surface.Wall, Mathf.Min(paper, wallpapers - 1));
                z.Floor = new SurfaceRef(Surface.Floor, carpet % carpets);
                z.Ceiling = new SurfaceRef(Surface.Ceiling, 0);
                z.Height = height; z.Lights = LightLayout.Rows; z.LightsAlongX = false; z.Dead = 0f;
                z.Baseboard = true; z.ChairRail = rail; z.Pilasters = false; z.FloorRot = false;
                Paint(r, id);
                return z;
            }

            void BuildOrigin()
            {
                // Wallpaper variants are pattern * 2 + tone (tone 0 cream, 1 yellow): 0 stripes, 1 chevrons, 3 dots, 4 plain.
                var all = new RectInt(6, 6, 22, 22);
                OriginZone(new RectInt(6, 6, 5, 6), 7, 0, 2.6f, true);              // left of the opening: dots, low rail
                OriginZone(new RectInt(11, 6, 9, 6), 2, 0, 2.6f, false);            // right of it: pale chevrons
                OriginZone(new RectInt(6, 12, 14, 16), 9, 0, 2.7f, false);          // photo 1's hall
                var arcade = OriginZone(new RectInt(20, 6, 6, 22), 8, 0, 2.8f, true);   // photo 2's long room
                OriginZone(new RectInt(26, 6, 2, 22), 9, 1, 2.6f, false);           // the alcoves past the arches

                // Photo 1: the short opening you stand in, its two wall ends close on either side.
                for (int z = 10; z < 12; z++) { Wall(10, z, 10, z + 1); Wall(12, z, 12, z + 1); }
                Wall(10, 10, 11, 10); Wall(11, 10, 12, 10);
                // The line between the front rooms and the hall, open where the photo looks through.
                Opening(7, 12, 8, 12, EdgeKind.Doorway);
                Opening(16, 12, 17, 12, EdgeKind.Doorway);
                for (int x = 6; x < 10; x++) Wall(x, 12, x + 1, 12);
                for (int x = 12; x < 20; x++) Wall(x, 12, x + 1, 12);
                // In the hall: the free-standing partition ahead-left, and a deeper wall on the right.
                for (int x = 8; x < 12; x++) Wall(x, 19, x + 1, 19);
                for (int z = 22; z < 26; z++) Wall(16, z, 16, z + 1);

                // Between the hall and photo 2's room: a wide gap near the front, a door at the back.
                Opening(20, 13, 20, 14, EdgeKind.Wide);
                Opening(20, 14, 20, 15, EdgeKind.Wide);
                Opening(20, 24, 20, 25, EdgeKind.Doorway);
                for (int z = 6; z < 28; z++) Wall(20, z, 20, z + 1);

                // Photo 2: the arcade. Half walls with arches above, a pillar at every joint, one bay left open.
                Opening(26, 17, 26, 18, EdgeKind.Arch);
                for (int z = 8; z < 26; z++) Wall(26, z, 26, z + 1, EdgeKind.Arcade, EdgeKind.Arch, out _);
                for (int z = 8; z <= 26; z++)
                    pillars.Add((new Bounds(new Vector3(26 * C, arcade.Height / 2f, z * C), new Vector3(0.4f, arcade.Height, 0.4f)), zone[25, Mathf.Min(z, 27)]));
                Wall(26, 6, 26, 7); Wall(26, 7, 26, 8); Wall(26, 26, 26, 27); Wall(26, 27, 26, 28);
                // Dividers between the alcoves, every other bay.
                for (int z = 9; z < 26; z += 3) Wall(26, z, 27, z);

                // The outside walls, with a way out on every side into ordinary Level 0.
                Opening(6, 15, 6, 16, EdgeKind.Doorway);
                Opening(12, 28, 13, 28, EdgeKind.Doorway);
                Opening(28, 20, 28, 21, EdgeKind.Doorway);
                Opening(16, 6, 17, 6, EdgeKind.Doorway);
                RoomWalls(all, 0, EdgeKind.Doorway);

                // The outlet low on the chevron wall at your right shoulder; the trash can and bucket in photo 2.
                if (WallFrame(Key(12, 11, 12, 12), false, out var pose, out _))
                    props.Add(new PropSpec { Kind = PropKind.Outlet, Pose = pose * Matrix4x4.Translate(new Vector3(0.6f, 0f, 0f)), Ceiling = 2.6f });
                props.Add(new PropSpec { Kind = PropKind.TrashCan, Pose = Matrix4x4.TRS(new Vector3(20 * C + 0.5f, 0f, 9 * C), Quaternion.identity, Vector3.one), Ceiling = arcade.Height });
                props.Add(new PropSpec { Kind = PropKind.Bucket, Pose = Matrix4x4.TRS(new Vector3(20 * C + 1.1f, 0f, 9 * C + 0.2f), Quaternion.identity, Vector3.one), Ceiling = arcade.Height });

                Reserve(new RectInt(all.xMin + 1, all.yMin + 1, all.width - 2, all.height - 2));
                Use(all);
            }
        }
    }
}
