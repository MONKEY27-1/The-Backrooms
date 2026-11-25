using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 8 "Cave Systems" (Backrooms Wiki, https://backrooms-wiki.wikidot.com/level-8 , CC BY-SA 3.0, rebuilt 2026-11-23):
    /// an endless karst network. One chamber per chunk, at its own height, joined by tunnels that climb, dip and cross over
    /// each other; carved out of solid rock as a density field, so everything is smooth natural stone.
    ///
    /// The 9th Road runs from the entrance pool through a fixed chain of chambers, a M.E.G. marker in each, to a shaft that
    /// drops into Level 9. Off the road: flood pits, boiling tar, crawls that loop back, glowing fungus, and supplies.
    public static class CaveLayout
    {
        const float S = MazeLayout.ChunkSize;
        const int Low = -27, High = 27;   // the rock is solid outside these heights
        public const int RoadLength = 14;

        enum Room { Plain, Pool, Road, Organ, Handy, End, Flood, Tar, Glow, Camp }

        struct Node { public Vector3 P; public float R, H, Bowl, BowlR, Pit, PitR; public Room Kind; public int Road; }

        struct Shape
        {
            public bool Cavern;
            public Vector3 A, B;       // a tunnel's floor ends, or a cavern's floor centre (A)
            public float R, H, Rough, Bowl, BowlR, Pit, PitR;
            public Bounds Box;
        }

        // ---------- the plan (pure functions of the seed) ----------

        /// The chunks the 9th Road passes through, in order.
        static Vector2Int[] RoadCells(uint seed)
        {
            var main = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down }[WorldHash.Hash(seed, 8, 1, 0x81) % 4];
            var side = new Vector2Int(-main.y, main.x);
            var cells = new Vector2Int[RoadLength];
            var at = Vector2Int.zero;
            bool turned = true;
            for (int i = 1; i < RoadLength; i++)
            {
                float r = WorldHash.Value(seed, i, 2, 0x82);
                if (!turned && r < 0.45f && i < RoadLength - 1) { at += r < 0.225f ? side : -side; turned = true; }
                else { at += main; turned = false; }
                cells[i] = at;
            }
            return cells;
        }

        static int RoadIndex(Vector2Int[] road, int cx, int cz)
        {
            for (int i = 0; i < road.Length; i++) if (road[i].x == cx && road[i].y == cz) return i;
            return -1;
        }

        static Node NodeAt(uint seed, Vector2Int[] road, int cx, int cz)
        {
            int r = RoadIndex(road, cx, cz);
            float V(int salt) => WorldHash.Value(seed, cx, cz, salt);
            var n = new Node { Road = r };
            float jitter = r >= 0 ? 3f : 6f;
            n.P = new Vector3((cx + 0.5f) * S + (V(0x8A) - 0.5f) * 2f * jitter, RockMesh.Noise(cx * 0.31f + 0.5f, seed % 89, cz * 0.31f + 0.5f) * 9f, (cz + 0.5f) * S + (V(0x8B) - 0.5f) * 2f * jitter);
            float k = V(0x8C);
            n.Kind = r == 0 ? Room.Pool : r == road.Length - 1 ? Room.End : r == 4 ? Room.Organ : r == 7 ? Room.Handy : r > 0 ? Room.Road
                   : k < 0.13f ? Room.Flood : k < 0.28f ? Room.Tar : k < 0.45f ? Room.Glow : k < 0.56f ? Room.Camp : k < 0.6f ? Room.Organ : k < 0.63f ? Room.Handy : Room.Plain;
            switch (n.Kind)
            {
                case Room.Pool: n.R = 10f; n.H = 6f; n.Bowl = 3.4f; n.BowlR = 5.5f; break;
                case Room.End: n.R = 9f; n.H = 8f; n.Pit = 13f; n.PitR = 3.6f; n.P.y = Mathf.Clamp(n.P.y, -6f, 6f); break;
                case Room.Organ: n.R = 11.5f; n.H = 10f; break;
                case Room.Handy: n.R = 13.5f; n.H = 9f; break;
                case Room.Flood: n.R = 11.5f; n.H = 6f; n.Bowl = 1.7f; n.BowlR = 9f; n.Pit = 4.6f; n.PitR = 3.8f; break;
                case Room.Tar: n.R = 10.5f; n.H = 5f; n.Bowl = 1.3f; n.BowlR = 6.5f; break;
                case Room.Glow: n.R = 8.5f; n.H = 6f; break;
                case Room.Camp: n.R = 6f; n.H = 4f; break;
                case Room.Road: n.R = 5f + V(0x8D) * 4f; n.H = 4f + V(0x8E) * 4f; break;
                default: n.R = 3f + V(0x8D) * 6f; n.H = 3f + V(0x8E) * 6f; break;
            }
            return n;
        }

        /// The floor points of the road's chambers, beside each marker; the last is the drop itself.
        public static Vector3[] Road(uint seed)
        {
            var cells = RoadCells(seed);
            var points = new Vector3[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                var n = NodeAt(seed, cells, cells[i].x, cells[i].y);
                points[i] = n.Kind == Room.Pool ? n.P + PoolBank(seed, cells) : n.P;
            }
            return points;
        }

        /// From the entrance pool's middle to its bank, on the side the road leaves by.
        static Vector3 PoolBank(uint seed, Vector2Int[] cells) => new Vector3(cells[1].x - cells[0].x, 0f, cells[1].y - cells[0].y) * 7.4f;

        // ---------- the rock ----------

        static void Gather(uint seed, Vector2Int[] road, int cx, int cz, float pad, List<Shape> shapes)
        {
            var area = new Bounds(new Vector3((cx + 0.5f) * S, 0f, (cz + 0.5f) * S), new Vector3(S + 2f + pad, 200f, S + 2f + pad));
            void Add(Shape s)
            {
                s.Box.Expand(5f);   // the noise
                if (s.Box.Intersects(area)) shapes.Add(s);
            }
            void Tunnel(Vector3 a, Vector3 b, float r)
            {
                var box = new Bounds((a + b) / 2f, Vector3.zero);
                foreach (var e in new[] { a, b }) { box.Encapsulate(e + Vector3.one * r * 2f); box.Encapsulate(e - Vector3.one * r); }
                Add(new Shape { A = a, B = b, R = r, Rough = Mathf.Clamp(r * 0.36f, 0.22f, 1f), Box = box });
            }
            for (int x = cx - 2; x <= cx + 2; x++)
            for (int z = cz - 2; z <= cz + 2; z++)
            {
                var n = NodeAt(seed, road, x, z);
                var box = new Bounds(n.P + Vector3.up * (n.H - n.Pit - n.Bowl) / 2f, new Vector3(n.R * 2f, n.H * 1.4f + n.Pit + n.Bowl, n.R * 2f));
                Add(new Shape { Cavern = true, A = n.P, R = n.R, H = n.H, Rough = 1.3f, Bowl = n.Bowl, BowlR = n.BowlR, Pit = n.Pit, PitR = n.PitR, Box = box });
                float V(int salt) => WorldHash.Value(seed, x, z, salt);
                void Edge(int dx, int dz, int salt, float chance)
                {
                    var m = NodeAt(seed, road, x + dx, z + dz);
                    bool isRoad = n.Road >= 0 && m.Road >= 0 && Mathf.Abs(n.Road - m.Road) == 1;
                    if (!isRoad && V(salt) >= chance) return;
                    var flat = new Vector3(m.P.x - n.P.x, 0f, m.P.z - n.P.z).normalized;
                    var mid = (n.P + m.P) / 2f + new Vector3(-flat.z, 0f, flat.x) * ((V(salt + 1) - 0.5f) * (isRoad ? 6f : 11f)) + Vector3.up * ((V(salt + 2) - 0.5f) * (isRoad ? 2.5f : dx != 0 && dz != 0 ? 14f : 9f));
                    float r = isRoad ? 2.5f + V(salt + 3) * 0.9f : V(salt + 3) < 0.2f ? 1.05f : 1.5f + V(salt + 3) * 1.6f;
                    Tunnel(n.P, mid, r);
                    Tunnel(mid, m.P, r);
                }
                bool east = V(0x90) < 0.5f;   // every chamber leads on one way at least, so the whole network joins up
                Edge(1, 0, 0xA0, east ? 2f : 0.4f);
                Edge(0, 1, 0xB0, east ? 0.4f : 2f);
                if (n.Kind == Room.Plain || n.Kind == Room.Glow) Edge(1, 1, 0xC0, 0.16f);
                if (HasLoop(n, x, z, seed)) Tunnel(n.P, LoopEnd(seed, n, x, z), 1.05f);
            }
        }

        static bool HasLoop(Node n, int cx, int cz, uint seed) => n.Road == 9 || (n.Road < 0 && n.Kind == Room.Plain && WorldHash.Value(seed, cx, cz, 0x9A) < 0.2f);

        static Vector3 LoopEnd(uint seed, Node n, int cx, int cz)
        {
            int c = (int)(WorldHash.Hash(seed, cx, cz, 0x9B) % 4);
            return n.P + new Vector3(c % 2 == 0 ? 11f : -11f, 1.2f, c < 2 ? 11f : -11f);
        }

        static float Smin(float a, float b, float k) { float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k; return Mathf.Min(a, b) - h * h * k * 0.25f; }

        /// Rock (positive) or air (negative), in about metres.
        static float Field(List<Shape> shapes, Vector3 p)
        {
            float best = 6f, noise = 0f, floorNoise = 0f;
            bool noisy = false;
            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                if (!s.Box.Contains(p)) continue;
                if (!noisy)
                {
                    noisy = true;
                    noise = RockMesh.Noise(p.x / 6.5f, p.y / 5f, p.z / 6.5f) + RockMesh.Noise(p.x / 2.4f, p.y / 2f, p.z / 2.4f) * 0.65f;
                    floorNoise = RockMesh.Noise(p.x / 4.5f, 5.5f, p.z / 4.5f) * 0.35f + RockMesh.Noise(p.x / 1.7f, 8.5f, p.z / 1.7f) * 0.1f;
                }
                float wall, floor;
                if (s.Cavern)
                {
                    float qx = p.x - s.A.x, qz = p.z - s.A.z, qy = Mathf.Max(0f, p.y - s.A.y - s.H * 0.3f);
                    float flat = Mathf.Sqrt(qx * qx + qz * qz);
                    wall = (Mathf.Sqrt(flat * flat / (s.R * s.R) + qy * qy / (s.H * s.H)) - 1f) * Mathf.Min(s.R, s.H) + noise * s.Rough;
                    floor = s.A.y + floorNoise;
                    if (flat < s.BowlR) floor -= s.Bowl * Mathf.SmoothStep(0f, 1f, (s.BowlR - flat) / s.BowlR);
                    if (flat < s.PitR) floor -= s.Pit * Mathf.SmoothStep(0f, 1f, (s.PitR - flat) / 0.9f);
                }
                else
                {
                    float dx = s.B.x - s.A.x, dz = s.B.z - s.A.z;
                    float t = Mathf.Clamp01(((p.x - s.A.x) * dx + (p.z - s.A.z) * dz) / Mathf.Max(0.01f, dx * dx + dz * dz));
                    var c = Vector3.Lerp(s.A, s.B, t);
                    floor = c.y + floorNoise;
                    c.y += s.R * 0.7f;
                    wall = Vector3.Distance(p, c) - s.R + noise * s.Rough;
                }
                float under = floor - p.y;
                float air = -Smin(-wall, -under, 0.5f);   // smooth where the floor meets the walls
                best = Smin(best, air, 1.2f);
            }
            return best;
        }

        /// A place to stand at or under a floor point.
        public static Vector3 Stand(uint seed, Vector3 at)
        {
            var c = ChunkStreamer.ChunkOf(at);
            var shapes = new List<Shape>();
            Gather(seed, RoadCells(seed), c.x, c.y, 4f, shapes);
            var p = at + Vector3.up * 1.2f;
            for (int i = 0; i < 80 && Field(shapes, p) < 0f; i++) p.y -= 0.1f;
            return p + Vector3.up * 0.25f;
        }

        public static Vector3 Entrance(uint seed, int marker) => Stand(seed, Road(seed)[Mathf.Clamp(marker, 0, RoadLength - 2)] + Vector3.right * 0.9f);

        /// Self-check: the whole road is open air at body height and never steeper than you can walk.
        public static string CheckRoad(uint seed)
        {
            var cells = RoadCells(seed);
            for (int i = 0; i < cells.Length - 1; i++)
            {
                var shapes = new List<Shape>();
                Gather(seed, cells, cells[i].x, cells[i].y, 80f, shapes);
                var n = NodeAt(seed, cells, cells[i].x, cells[i].y);
                bool linked = false;
                foreach (var s in shapes)
                {
                    if (s.Cavern || s.R < 2.5f || (s.A != n.P && s.B != n.P)) continue;
                    linked = true;
                    for (float t = 0f; t <= 1f; t += 0.04f)
                        if (Field(shapes, Vector3.Lerp(s.A, s.B, t) + Vector3.up * 1.2f) > -0.3f) return $"the 9th Road is blocked near chamber {i}";
                    var d = s.B - s.A;
                    if (Mathf.Abs(d.y) > new Vector2(d.x, d.z).magnitude * 0.75f) return $"the 9th Road is too steep near chamber {i}";
                }
                if (!linked) return $"the 9th Road doesn't leave chamber {i}";
            }
            return null;
        }

        // ---------- the chunk ----------

        public static ChunkLayout Generate(uint seed, int cx, int cz, int layer, LevelTheme theme)
        {
            var L = new ChunkLayout();
            float x0 = cx * S, z0 = cz * S;
            var rng = new WorldRng(WorldHash.Hash(seed, cx, cz, 0x8800));
            var road = RoadCells(seed);
            var node = NodeAt(seed, road, cx, cz);
            var shapes = new List<Shape>();
            Gather(seed, road, cx, cz, 0f, shapes);
            const int n = RockMesh.Samples, ny = High - Low + 1;
            var field = new float[n * ny * n];
            for (int i = 0; i < n; i++)
            for (int j = 0; j < ny; j++)
            for (int k = 0; k < n; k++)
                field[RockMesh.Index(i, j, k, ny)] = j == 0 || j == ny - 1 ? 6f : Field(shapes, new Vector3(x0 + i - 1, Low + j, z0 + k - 1));
            var rock = new RockMesh(new SurfaceRef(Surface.Wall));
            rock.SurfaceNets(field, ny, new Vector3(x0, Low, z0));
            var glow = new RockMesh(new SurfaceRef(Surface.Floor, 1), false);
            L.Rocks.Add(rock);
            L.Rocks.Add(glow);

            void Prop(PropKind k, Vector3 at, float yaw, int v = 0) => L.Props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Variant = v, Ceiling = 4f });
            void Light(Vector3 at, float range, Color tint) => L.Panels.Add(new PanelSpec { Position = at, Bare = true, Emitter = true, Range = range, Tint = tint, State = FixtureState.Lit,
                                                                                         Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(at.x * 4f), Mathf.RoundToInt(at.z * 4f), 0x8F0) });
            // From a point in the air, down to the floor or up to the roof. False if it starts in rock or never lands.
            bool Find(Vector3 from, float dir, out Vector3 hit)
            {
                hit = from;
                if (Field(shapes, from) > -0.4f) return false;
                for (int i = 0; i < 90; i++)
                {
                    hit.y += dir * 0.25f;
                    if (Field(shapes, hit) > 0f) return hit.y > Low + 1 && hit.y < High - 1;
                }
                return false;
            }
            // How close a point is to the middle of the road, so nothing is put in the way.
            float RoadDistance(Vector3 p)
            {
                float best = 99f;
                if (node.Kind == Room.Pool || node.Kind == Room.End) best = Mathf.Max(0f, Vector3.Distance(p, node.P) - 5f);
                foreach (var s in shapes)
                {
                    if (s.Cavern || s.R < 2.5f) continue;
                    float dx = s.B.x - s.A.x, dz = s.B.z - s.A.z;
                    float t = Mathf.Clamp01(((p.x - s.A.x) * dx + (p.z - s.A.z) * dz) / Mathf.Max(0.01f, dx * dx + dz * dz));
                    var c = Vector3.Lerp(s.A, s.B, t);
                    if (Mathf.Abs(p.y - c.y) < 4f) best = Mathf.Min(best, new Vector2(p.x - c.x, p.z - c.z).magnitude);
                }
                return best;
            }
            void Spike(Vector3 foot, float tall, float r, float dir)
            {
                var path = new Vector3[6];
                var radius = new float[6];
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 5f;
                    path[i] = foot + new Vector3(Mathf.Sin(foot.x + t * 2f) * 0.08f, dir * (t * tall - 0.3f), Mathf.Cos(foot.z + t * 2f) * 0.08f);
                    radius[i] = i == 5 ? 0.01f : r * Mathf.Pow(1f - t, 0.75f) + 0.03f;
                }
                rock.Tube(path, radius, 8, 0.35f);
            }
            void Fungus(Vector3 foot)
            {
                int caps = 3 + rng.Range(0, 5);
                for (int i = 0; i < caps; i++)
                {
                    var f = foot + new Vector3(rng.Value() - 0.5f, -0.05f, rng.Value() - 0.5f) * 0.9f;
                    float h = 0.12f + rng.Value() * 0.4f, r = 0.06f + rng.Value() * 0.13f;
                    var lean = new Vector3(rng.Value() - 0.5f, 0f, rng.Value() - 0.5f) * 0.12f;
                    rock.Tube(new[] { f, f + lean + Vector3.up * h }, new[] { 0.025f, 0.018f }, 5);
                    var top = f + lean + Vector3.up * h;
                    glow.Tube(new[] { top - Vector3.up * 0.03f, top, top + Vector3.up * r * 0.4f, top + Vector3.up * r * 0.6f }, new[] { r, r * 1.02f, r * 0.65f, 0.004f }, 9);
                }
            }

            // Speleothems and rubble everywhere, fungus here and there.
            int fungi = 0;
            for (int i = 0; i < 260; i++)
            {
                var from = new Vector3(x0 + rng.Value() * S, node.P.y + Mathf.Lerp(-7f, 8f, rng.Value()), z0 + rng.Value() * S);
                float r = rng.Value();
                if (Find(from, -1f, out var floor))
                {
                    float clear = RoadDistance(floor);
                    if (r < 0.35f && clear > 1.9f) Spike(floor, 0.4f + rng.Value() * rng.Value() * 2.4f, 0.12f + rng.Value() * 0.22f, 1f);
                    else if (r < 0.6f && clear > 1.9f) { float b = 0.15f + rng.Value() * rng.Value() * 0.8f; rock.Blob(floor, new Vector3(b, b * 0.7f, b * 1.1f), Quaternion.Euler(rng.Value() * 60f, rng.Value() * 360f, 0f), 0.35f, 9); }
                    else if (r < 0.66f && fungi < (node.Kind == Room.Glow ? 14 : 2)) { fungi++; Fungus(floor); if (node.Kind != Room.Glow) Light(floor + Vector3.up * 0.6f, 4f, new Color(0.3f, 1f, 0.75f)); }
                }
                if (r > 0.45f && Find(from, 1f, out var roof) && (RoadDistance(roof) > 1.2f || roof.y > from.y + 4f)) Spike(roof, 0.3f + rng.Value() * rng.Value() * 2.2f, 0.08f + rng.Value() * 0.16f, -1f);
            }

            // The chamber itself.
            var at = node.P;
            Vector3 Round(float angle, float dist) => at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
            bool Ground(Vector3 p, out Vector3 floor) => Find(p + Vector3.up * 1.2f, -1f, out floor) || Find(p + Vector3.up * 2.4f, -1f, out floor);
            switch (node.Kind)
            {
                case Room.Pool:
                {
                    L.Specials.Add(new SpecialSpec { Kind = SpecialKind.CaveWater, Pose = Matrix4x4.Translate(at - Vector3.up * 3.5f), Size = new Vector3(11f, 2.9f, 11f) });
                    L.Exits.Add(new ExitSpec { Kind = ExitKind.Hatch, Target = "7", Pose = Matrix4x4.Translate(at - Vector3.up * 3.6f), Trigger = new Bounds(at - Vector3.up * 3.2f, new Vector3(1.6f, 0.8f, 1.6f)) });
                    var bank = at + PoolBank(seed, road);
                    var out_ = PoolBank(seed, road).normalized;
                    var side = new Vector3(-out_.z, 0f, out_.x);
                    if (Ground(bank + side * 2.6f, out var f)) { Prop(PropKind.SleepingBag, f, 40f); }
                    if (Ground(bank + side * 3.6f, out f)) { Prop(PropKind.Lantern, f, 0f); Light(f + Vector3.up * 0.5f, 7f, new Color(1f, 0.72f, 0.4f)); }
                    if (Ground(bank - side * 2.2f, out f)) Prop(PropKind.Battery, f, 20f);
                    if (Ground(bank - side * 2.8f, out f)) Prop(PropKind.AlmondWater, f, 20f);
                    if (Ground(bank + side * 1.6f, out f))
                        L.Readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(f + Vector3.up * 0.06f, Quaternion.Euler(0f, 30f, 0f), Vector3.one),
                            Title = "M.E.G. field note, laminated",
                            Text = "You've come up through the pool. Good. Now listen.\n\nStay on the 9th Road. Yellow posts, a lamp on each, one to a chamber, fifty metres or so " +
                                   "apart. It ends at a hole. The hole is the way out.\n\nBatteries go flat twice as fast down here and no lamp reaches as far as it should. The pale " +
                                   "caps glow; use them.\n\nOff the road: pits that flood the moment you're at the bottom, tar that boils and has hands in it, and crawls that come " +
                                   "out where you've already been. People leave the road because that's where the dropped packs are. So are the people." });
                    break;
                }
                case Room.End:
                {
                    var bottom = at - Vector3.up * 12f;
                    L.Exits.Add(new ExitSpec { Kind = ExitKind.Hatch, Target = "9", Pose = Matrix4x4.Translate(bottom), Trigger = new Bounds(at - Vector3.up * 7.5f, new Vector3(6.4f, 3f, 6.4f)) });
                    Light(at - Vector3.up * 4f, 9f, new Color(0.6f, 0.75f, 1f));   // night sky, from below
                    break;
                }
                case Room.Organ:
                    // Stalagspikes grown into instruments: a rank of organ pipes, a harp, a thing like a cello.
                    for (int i = 0; i < 17; i++)
                    {
                        float a = -1.1f + i * 0.14f;
                        if (!Ground(Round(a, 8.2f + (i % 2) * 0.5f), out var f) || RoadDistance(f) < 1.6f) continue;
                        float tall = 2.2f + Mathf.Abs(i - 8) * 0.38f, r = 0.17f + (i % 3) * 0.03f;
                        rock.Tube(new[] { f - Vector3.up * 0.4f, f + Vector3.up * 0.25f, f + Vector3.up * 0.4f, f + Vector3.up * tall * 0.5f, f + Vector3.up * tall, f + Vector3.up * (tall + 0.02f) },
                                  new[] { r * 1.9f, r * 1.5f, r * 0.75f, r * 1.05f, r, 0.01f }, 10, 0.12f);
                    }
                    if (Ground(Round(2.4f, 6.5f), out var h) && RoadDistance(h) > 1.6f)   // the harp
                    {
                        var neck = new Vector3[9];
                        var fat = new float[9];
                        for (int i = 0; i < 9; i++) { float t = i / 8f; neck[i] = h + new Vector3(Mathf.Sin(t * 2.9f) * 1.7f, t * 3.4f - 0.2f, 0f); fat[i] = Mathf.Lerp(0.34f, 0.12f, t); }
                        rock.Tube(neck, fat, 9, 0.25f);
                        for (int i = 1; i < 8; i++) rock.Tube(new[] { h + new Vector3(i * 0.17f, 0f, 0f), neck[i] }, new[] { 0.025f, 0.02f }, 5, 0.2f);
                    }
                    if (Ground(Round(3.7f, 6.5f), out var c) && RoadDistance(c) > 1.6f)   // the cello
                    {
                        rock.Blob(c + Vector3.up * 0.75f, new Vector3(0.62f, 0.7f, 0.26f), Quaternion.identity, 0.1f, 14);
                        rock.Blob(c + Vector3.up * 1.65f, new Vector3(0.48f, 0.55f, 0.22f), Quaternion.identity, 0.1f, 14);
                        rock.Tube(new[] { c + Vector3.up * 1.9f, c + Vector3.up * 3f, c + new Vector3(0f, 3.25f, 0.12f), c + new Vector3(0f, 3.2f, 0.3f) }, new[] { 0.1f, 0.07f, 0.08f, 0.03f }, 8, 0.12f);
                        for (int i = 0; i < 4; i++) rock.Tube(new[] { c + new Vector3((i - 1.5f) * 0.05f, 0.5f, 0.3f), c + new Vector3((i - 1.5f) * 0.03f, 3f, 0.1f) }, new[] { 0.008f, 0.008f }, 4);
                    }
                    L.Specials.Add(new SpecialSpec { Kind = SpecialKind.RockSong, Pose = Matrix4x4.Translate(at + Vector3.up * 2f) });
                    Light(at + Vector3.up * 4f, 12f, new Color(0.55f, 0.75f, 1f));
                    break;
                case Room.Handy:
                    // Handyland: a forest of stone hands under a cold green light.
                    for (int i = 0; i < 60; i++)
                    {
                        if (!Ground(Round(rng.Value() * 6.283f, 2.5f + Mathf.Sqrt(rng.Value()) * 9.5f), out var f) || RoadDistance(f) < 2.2f) continue;
                        StoneHands.Add(rock, f, rng.Value() * 360f, 0.7f + rng.Value() * rng.Value() * 3.6f, rng.Value() * 1.1f);
                    }
                    Light(at + new Vector3(4f, 5f, 2f), 14f, new Color(0.3f, 1f, 0.6f));
                    Light(at + new Vector3(-5f, 5.5f, -3f), 14f, new Color(0.35f, 0.6f, 1f));
                    Light(at + new Vector3(1f, 6f, -6f), 14f, new Color(0.7f, 0.4f, 1f));
                    break;
                case Room.Flood:
                    L.Specials.Add(new SpecialSpec { Kind = SpecialKind.CaveWater, Pose = Matrix4x4.Translate(at - Vector3.up * 6.4f), Size = new Vector3(17.6f, 6.15f, 17.6f), Id = 1 });
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Round(i * 1.57f + 0.4f, 1.5f) - Vector3.up * 6.2f;
                        if (!Ground(p, out var f)) continue;
                        if (i < 2) Prop(PropKind.AlmondWater, f, i * 70f); else if (i == 2) Prop(PropKind.Battery, f, 10f); else Prop(PropKind.Bones, f, 80f, 3);
                    }
                    Light(at - Vector3.up * 4f, 7f, new Color(0.3f, 0.9f, 1f));   // you can see there's something down there
                    break;
                case Room.Tar:
                    L.Specials.Add(new SpecialSpec { Kind = SpecialKind.TarPool, Pose = Matrix4x4.Translate(at - Vector3.up * 0.5f), Size = new Vector3(10.4f, 1f, 10.4f) });
                    if (Ground(Round(rng.Value() * 6.283f, 8.2f), out var shore)) Prop(PropKind.AlmondWater, shore, 30f);
                    if (Ground(Round(rng.Value() * 6.283f, 8.2f), out shore)) Prop(rng.Value() < 0.6f ? PropKind.Battery : PropKind.Bones, shore, 30f, 5);
                    Light(at + Vector3.up * 2f, 11f, new Color(1f, 0.42f, 0.15f));
                    break;
                case Room.Glow:
                    for (int i = 0; i < 4; i++) Light(Round(i * 1.57f + rng.Value(), 4.5f) + Vector3.up * 1.4f, 6.5f, new Color(0.3f, 1f, 0.75f));
                    for (int i = 0; i < 8; i++) if (Ground(Round(rng.Value() * 6.283f, rng.Value() * 7f), out var f)) Prop(PropKind.MossPatch, f, rng.Value() * 360f, i);
                    if (rng.Value() < 0.4f && Ground(Round(rng.Value() * 6.283f, 3f), out var g)) Prop(PropKind.AlmondWater, g, 0f);
                    break;
                case Room.Camp:
                    if (Ground(at + new Vector3(1.4f, 0f, 0.5f), out var bag)) Prop(PropKind.SleepingBag, bag, rng.Value() * 360f);
                    if (Ground(at + new Vector3(-1f, 0f, 0.9f), out var cr)) Prop(PropKind.Crate, cr, rng.Value() * 360f, rng.Range(0, 4));
                    if (Ground(at + new Vector3(-0.4f, 0f, -1.2f), out var can)) Prop(rng.Value() < 0.5f ? PropKind.Battery : PropKind.AlmondWater, can, 15f);
                    if (Ground(at + new Vector3(0.6f, 0f, -1.3f), out can)) Prop(PropKind.Can, can, 15f);
                    break;
            }
            if (node.Road >= 0 && node.Road < RoadLength - 1)   // the road's marker, and its lamp
            {
                var spot = Road(seed)[node.Road];
                var next = road[node.Road + 1] - road[node.Road];
                if (Ground(spot + new Vector3(next.y, 0f, next.x) * 0.9f, out var f))
                {
                    Prop(PropKind.RoadMarker, f, Mathf.Atan2(-next.x, -next.y) * Mathf.Rad2Deg, node.Road);
                    Light(f + new Vector3(next.x, 0f, next.y) * -0.35f + Vector3.up * 1.9f, 8f, new Color(1f, 0.75f, 0.4f));
                }
            }
            if (HasLoop(node, cx, cz, seed))
            {
                var end = Vector3.Lerp(node.P, LoopEnd(seed, node, cx, cz), 0.96f);
                L.Specials.Add(new SpecialSpec { Kind = SpecialKind.LoopCrawl, Pose = Matrix4x4.Translate(end), Size = new Vector3(2f, 2f, 2f) });
            }
            return L;
        }
    }
}
