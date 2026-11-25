using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 6 "Lights Out" (Backrooms Wiki, https://backrooms-wiki.wikidot.com/level-6 , CC BY-SA 3.0): a long series
    /// of tight hallways of a smooth, cold material, in a darkness no light survives. Total silence but for what you
    /// imagine. The stairs down to Level 7 are found by listening for waves. Rumoured: the World's Quietest Room, where
    /// someone begs you not to flip the light switch.
    ///
    /// District kind: DarkHalls.
    public static partial class DistrictLayout
    {
        sealed partial class Plan
        {
            const float QuietRoomChance = 0.12f;

            /// A winding spanning-tree maze of 2 m corridors, no light fixtures at all, the odd wider junction; now and then
            /// the Quietest Room, a dead end off the maze with a single switch on its wall.
            void BuildDarkHalls()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Corridor, all);
                var z = zones[id];
                z.Height = 2.4f;
                z.Bulbs = true;   // with Dead = 1: no fixtures of any kind
                z.Dead = 1f;
                z.Baseboard = z.ChairRail = false;
                z.Wall = new SurfaceRef(Surface.Wall, 0);
                z.Floor = new SurfaceRef(Surface.Floor, 0);
                z.Trim = new SurfaceRef(Surface.Wall, 0);   // door casings the same cold concrete, not office trim
                Paint(all, id);

                var open = new HashSet<int>();
                var seen = new bool[D, D];
                RectInt quiet = default;
                int quietSide = -1;
                if (rng.Value() < QuietRoomChance) PlanQuietRoom(open, seen, out quiet, out quietSide);

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
                        int dir = options.Contains(lastDir) && rng.Value() < 0.78f ? lastDir : options[rng.Range(0, options.Count)];   // long, straight, identical halls
                        var n = new Vector2Int(c.x + Dx[dir], c.y + Dz[dir]);
                        open.Add(EdgeBetween(c, n));
                        seen[n.x, n.y] = true;
                        stack.Push((n, dir));
                    }
                }

                for (int x = 0; x < D; x++)
                for (int zz = 0; zz < D; zz++)
                {
                    if (!Allowed(x, zz)) continue;
                    var c = new Vector2Int(x, zz);
                    if (x + 1 < D && !open.Contains(EdgeBetween(c, new Vector2Int(x + 1, zz)))) Wall(x + 1, zz, x + 1, zz + 1, EdgeKind.Doorway);
                    if (zz + 1 < D && !open.Contains(EdgeBetween(c, new Vector2Int(x, zz + 1)))) Wall(x, zz + 1, x + 1, zz + 1, EdgeKind.Doorway);
                }
                if (quietSide >= 0) FurnishQuietRoom(quiet, quietSide);
            }

            /// Reserves a 3 x 3 room whose only way in is one edge from the middle of a side.
            void PlanQuietRoom(HashSet<int> open, bool[,] seen, out RectInt r, out int side)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    int x0 = rng.Range(3, D - 6), z0 = rng.Range(3, D - 6);
                    r = new RectInt(x0, z0, 3, 3);
                    bool ok = AllAllowed(new RectInt(x0 - 1, z0 - 1, 5, 5));
                    if (!ok) continue;
                    side = rng.Range(0, 4);
                    var inside = side switch { 0 => new Vector2Int(x0 + 2, z0 + 1), 1 => new Vector2Int(x0 + 1, z0 + 2), 2 => new Vector2Int(x0, z0 + 1), _ => new Vector2Int(x0 + 1, z0) };
                    var outside = inside + new Vector2Int(Dx[side], Dz[side]);
                    for (int x = x0; x < x0 + 3; x++)
                    for (int zz = z0; zz < z0 + 3; zz++)
                    {
                        seen[x, zz] = true;
                        if (x + 1 < x0 + 3) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x + 1, zz)));
                        if (zz + 1 < z0 + 3) open.Add(EdgeBetween(new Vector2Int(x, zz), new Vector2Int(x, zz + 1)));
                    }
                    open.Add(EdgeBetween(inside, outside));
                    return;
                }
                r = default;
                side = -1;
            }

            /// Bare walls, a wooden chair turned to face the switch, the switch. And someone, very quietly, asking you not to.
            void FurnishQuietRoom(RectInt r, int side)
            {
                int rid = NewZone(ZoneKind.Room, r);
                var q = zones[rid];
                q.Height = 2.4f;
                q.Bulbs = true;
                q.Dead = 1f;
                q.Baseboard = q.ChairRail = false;
                q.Wall = new SurfaceRef(Surface.Wall, 0);
                q.Floor = new SurfaceRef(Surface.Floor, 0);
                q.Trim = new SurfaceRef(Surface.Wall, 0);
                Paint(r, rid);
                for (int x = r.xMin; x < r.xMax; x++) for (int zz = r.yMin; zz < r.yMax; zz++) used[x, zz] = true;
                // The switch on the wall opposite the way in.
                int back = side switch { 0 => 2, 1 => 3, 2 => 0, _ => 1 };
                var cell = new Vector2Int(r.xMin + 1, r.yMin + 1) + new Vector2Int(Dx[back], Dz[back]);
                int key = back switch
                {
                    0 => Key(cell.x + 1, cell.y, cell.x + 1, cell.y + 1), 1 => Key(cell.x, cell.y + 1, cell.x + 1, cell.y + 1),
                    2 => Key(cell.x, cell.y, cell.x, cell.y + 1), _ => Key(cell.x, cell.y, cell.x + 1, cell.y),
                };
                if (!WallFrame(key, back == 2 || back == 3, out var pose, out _)) return;
                props.Add(new PropSpec { Kind = PropKind.LightSwitch, Pose = pose, Ceiling = q.Height });
                Special(SpecialKind.QuietSwitch, pose * Matrix4x4.Translate(new Vector3(0f, 1.2f, 0.03f)), Vector3.zero);
                var mid = CellCenter(r.xMin + 1, r.yMin + 1);
                Vector3 toSwitch = (Vector3)pose.GetColumn(3) - mid;
                props.Add(new PropSpec { Kind = PropKind.WoodChair, Pose = Matrix4x4.TRS(mid - toSwitch.normalized * 0.4f, Quaternion.LookRotation(new Vector3(toSwitch.x, 0f, toSwitch.z)), Vector3.one), Ceiling = q.Height });
                Special(SpecialKind.QuietRoom, Matrix4x4.TRS(mid, Quaternion.identity, Vector3.one), new Vector3(3 * C - 0.4f, 2.3f, 3 * C - 0.4f));
            }
        }
    }
}
