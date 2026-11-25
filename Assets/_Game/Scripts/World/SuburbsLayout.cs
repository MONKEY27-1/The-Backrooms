using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 9 "The Suburbs" (Backrooms Wiki, https://backrooms-wiki.wikidot.com/level-9 , CC BY-SA 3.0, rebuilt 2026-11-23):
    /// an endless suburb at midnight. Streets run every third chunk both ways; the four chunks between are lots, two
    /// two-storey houses to a chunk, back yards meeting in the middle. A power line runs along some of the streets from
    /// where you land to a house built in the middle of a crossroads: its front door is the way out.
    ///
    /// Three lots near the line are STORY.md / STORY_PLAN 8i's three houses, built as places (key rack; papers on every
    /// wall; porch light, knot and tape recorder). `StorySpot` is where each one's document goes.
    public static class SuburbsLayout
    {
        const float S = MazeLayout.ChunkSize;
        public const float SkyHeight = 26f;
        const int RunwayX = 13, FieldX = -14;   // two chunks wide each: the airport's edge, the grass fields
        const float Storey = 2.8f, Ground = 0.3f, Thick = 0.16f;

        static readonly SurfaceRef Lawn = new SurfaceRef(Surface.Floor, 0), Asphalt = new SurfaceRef(Surface.Floor, 1), Pavement = new SurfaceRef(Surface.Floor, 2),
            Carpet = new SurfaceRef(Surface.Floor, 3), Bark = new SurfaceRef(Surface.Floor, 4), Hedge = new SurfaceRef(Surface.Floor, 5),
            Shingle = new SurfaceRef(Surface.Floor, 6), Grass = new SurfaceRef(Surface.Floor, 7), Cable = new SurfaceRef(Surface.Floor, 8), Plastic = new SurfaceRef(Surface.Floor, 9),
            Sky = new SurfaceRef(Surface.Ceiling, 0), Plaster = new SurfaceRef(Surface.Ceiling, 1), Wood = new SurfaceRef(Surface.Wood), Trim = new SurfaceRef(Surface.Trim, 0);
        const int Sidings = 5, Papers = 4;

        static int Mod3(int v) => ((v % 3) + 3) % 3;
        static bool Runway(int cx) => cx == RunwayX || cx == RunwayX + 1;
        static bool Field(int cx) => cx == FieldX || cx == FieldX + 1;

        // ---------- the plan (pure functions of the seed) ----------

        /// The crossroads the power line passes, in blocks (3 chunks), from the landing to the house in the road.
        static Vector2Int[] Nodes(uint seed, out int main)
        {
            main = WorldHash.Value(seed, 9, 1, 0x91) < 0.5f ? 1 : -1;
            var n = new Vector2Int[7];
            bool turned = true;
            for (int i = 1; i < n.Length; i++)
            {
                float r = WorldHash.Value(seed, i, 2, 0x92);
                bool turn = !turned && i < n.Length - 1 && r < 0.6f;
                int side = n[i - 1].x >= 2 ? -1 : n[i - 1].x <= -2 ? 1 : r < 0.3f ? 1 : -1;
                n[i] = n[i - 1] + (turn ? new Vector2Int(side, 0) : new Vector2Int(0, main));
                turned = turn;
            }
            return n;
        }

        /// Every chunk the line crosses, in order.
        static List<Vector2Int> Route(uint seed, out int main)
        {
            var nodes = Nodes(seed, out main);
            var cells = new List<Vector2Int> { Vector2Int.zero };
            for (int i = 1; i < nodes.Length; i++)
                for (int k = 1; k <= 3; k++) cells.Add(nodes[i - 1] * 3 + (nodes[i] - nodes[i - 1]) * k);
            return cells;
        }

        static Vector3 PoleAt(Vector2Int c) => new Vector3((c.x + 0.5f) * S + 6.7f, 0f, (c.y + 0.5f) * S + 6.7f);

        /// The poles in order; the last point is the door of the house they end at.
        public static List<Vector3> Line(uint seed)
        {
            var cells = Route(seed, out int main);
            var points = new List<Vector3>();
            for (int i = 0; i < cells.Count - 1; i++) points.Add(PoleAt(cells[i]));
            points.Add(EndDoor(seed));
            return points;
        }

        static Vector3 EndDoor(uint seed)
        {
            var cells = Route(seed, out int main);
            var c = cells[cells.Count - 1];
            return new Vector3((c.x + 0.5f) * S, Ground, (c.y + 0.5f) * S - main * 4.6f);
        }

        public static readonly Vector3 Landing = new Vector3(S / 2f - 1.5f, 0.1f, S / 2f + 1f);

        /// Out on the wet asphalt (and its pavements): the dangerous part.
        public static bool OnStreet(Vector3 p)
        {
            int cx = Mathf.FloorToInt(p.x / S), cz = Mathf.FloorToInt(p.z / S);
            if (Runway(cx) || Field(cx)) return false;
            float lx = p.x - cx * S - S / 2f, lz = p.z - cz * S - S / 2f;
            return (Mod3(cz) == 0 && Mathf.Abs(lz) < 6f) || (Mod3(cx) == 0 && Mathf.Abs(lx) < 6f);
        }

        /// Story house 0..2: the lot chunk it stands in (its first house).
        static Vector2Int StoryChunk(uint seed, int i)
        {
            var n = Nodes(seed, out int main)[1 + i * 2];
            return new Vector2Int(n.x * 3 + 1, n.y * 3 + 1);
        }

        /// Where a story house's document or tape lies, on the table in its living room (world space).
        public static Vector3 StorySpot(uint seed, int i)
        {
            var c = StoryChunk(seed, i);
            return new Vector3(c.x * S + 2.5f + 1.9f, Ground + 0.78f, c.y * S + 4f + 4.6f);
        }

        // ---------- the chunk ----------

        sealed class Ctx
        {
            public ChunkLayout L;
            public RockMesh Pane, Roof, Tree, Leaf, Wire, Tube, Tuft;
            public uint Seed;

            public void Box(Vector3 c, Vector3 s, SurfaceRef m, bool solid = true) => L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, s), m, BoxMeshBuilder.Faces.All, solid));
            public void Prop(PropKind k, Vector3 at, float yaw, int v = 0, float ceiling = 2.8f) => L.Props.Add(new PropSpec { Kind = k, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one), Variant = v, Ceiling = ceiling });
            public void Light(Vector3 at, float range, Color tint, FixtureState state = FixtureState.Lit) =>
                L.Panels.Add(new PanelSpec { Position = at, Bare = true, Emitter = true, Range = range, Tint = tint, State = state, Id = (int)WorldHash.Hash(Seed, Mathf.RoundToInt(at.x * 4f), Mathf.RoundToInt(at.z * 4f), 0x9F0) });
        }

        public static ChunkLayout Generate(uint seed, int cx, int cz, int layer, LevelTheme theme)
        {
            var L = new ChunkLayout();
            var c = new Ctx { L = L, Seed = seed, Pane = new RockMesh(new SurfaceRef(Surface.Floor, 10), false), Roof = new RockMesh(Shingle), Tree = new RockMesh(Bark), Leaf = new RockMesh(Hedge), Wire = new RockMesh(Cable, false), Tube = new RockMesh(Plastic), Tuft = new RockMesh(Grass, false) };
            foreach (var r in new[] { c.Pane, c.Roof, c.Tree, c.Leaf, c.Wire, c.Tube, c.Tuft }) L.Rocks.Add(r);
            float x0 = cx * S, z0 = cz * S;
            var rng = new WorldRng(WorldHash.Hash(seed, cx, cz, 0x9900));
            L.Quads.Add(new SurfaceQuad { Area = new Rect(x0, z0, S, S), Height = SkyHeight, FacingUp = false, Surface = Sky });

            if (Runway(cx)) { RunwayChunk(c, rng, cx, x0, z0, cz); return L; }
            if (Field(cx)) { FieldChunk(c, rng, cx, x0, z0, cz); return L; }
            L.Quads.Add(new SurfaceQuad { Area = new Rect(x0, z0, S, S), Height = 0f, FacingUp = true, Surface = Lawn });
            bool alongX = Mod3(cz) == 0, alongZ = Mod3(cx) == 0;
            if (alongX || alongZ) Street(c, rng, seed, cx, cz, x0, z0, alongX, alongZ);
            else if (IsPark(seed, cx, cz)) Park(c, rng, cx, cz, x0, z0);
            else Lots(c, rng, seed, cx, cz, x0, z0);
            return L;
        }

        static int Block(int v) => Mathf.FloorToInt(v / 3f);

        static bool IsPark(uint seed, int cx, int cz)
        {
            int bx = Block(cx), bz = Block(cz);
            bool Story(int x, int z) { for (int i = 0; i < 3; i++) { var s = StoryChunk(seed, i); if (Block(s.x) == x && Block(s.y) == z) return true; } return false; }
            if (Story(bx, bz)) return false;
            var near = Nodes(seed, out int main)[2];   // one by the line, so it's found
            foreach (var d in new[] { new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(0, 0) })
                if (!Story(near.x + d.x, near.y + d.y)) { if (near + d == new Vector2Int(bx, bz)) return true; break; }
            return WorldHash.Value(seed, bx, bz, 0x93) < 0.06f;
        }

        // ---------- streets ----------

        static void Street(Ctx c, WorldRng rng, uint seed, int cx, int cz, float x0, float z0, bool alongX, bool alongZ)
        {
            var mid = new Vector3(x0 + S / 2f, 0f, z0 + S / 2f);
            void Slab(float ax, float bx, float az, float bz, float top, SurfaceRef m) => c.Box(new Vector3(mid.x + (ax + bx) / 2f, top / 2f, mid.z + (az + bz) / 2f), new Vector3(bx - ax, top, bz - az), m);
            const float h = S / 2f;
            if (alongX && alongZ)
            {
                Slab(-h, h, -4f, 4f, 0.03f, Asphalt); Slab(-4f, 4f, -h, -4f, 0.03f, Asphalt); Slab(-4f, 4f, 4f, h, 0.03f, Asphalt);
                foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                {
                    Slab(Mathf.Min(sx * 4f, sx * h), Mathf.Max(sx * 4f, sx * h), Mathf.Min(sz * 4f, sz * 6f), Mathf.Max(sz * 4f, sz * 6f), 0.15f, Pavement);
                    Slab(Mathf.Min(sx * 4f, sx * 6f), Mathf.Max(sx * 4f, sx * 6f), Mathf.Min(sz * 6f, sz * h), Mathf.Max(sz * 6f, sz * h), 0.15f, Pavement);
                }
            }
            else if (alongX) { Slab(-h, h, -4f, 4f, 0.03f, Asphalt); foreach (float sz in new[] { -1f, 1f }) Slab(-h, h, Mathf.Min(sz * 4f, sz * 6f), Mathf.Max(sz * 4f, sz * 6f), 0.15f, Pavement); }
            else { Slab(-4f, 4f, -h, h, 0.03f, Asphalt); foreach (float sx in new[] { -1f, 1f }) Slab(Mathf.Min(sx * 4f, sx * 6f), Mathf.Max(sx * 4f, sx * 6f), -h, h, 0.15f, Pavement); }
            Vector3 On(float along, float across, float y = 0f) => mid + (alongX ? new Vector3(along, y, across) : new Vector3(across, y, along));
            float Face(float side) => alongX ? (side > 0f ? 180f : 0f) : (side > 0f ? 270f : 90f);   // yaw whose +z points at the road
            // Puddles.
            for (int i = 0; i < 4; i++)
            {
                var p = On((rng.Value() - 0.5f) * 26f, (rng.Value() - 0.5f) * 6f);
                float w = 0.8f + rng.Value() * 2.4f;
                c.L.Damp.Add(new SurfaceQuad { Area = new Rect(p.x - w / 2f, p.z - w / 3f, w, w * 0.66f), Height = 0.036f, FacingUp = true });
            }
            // The lamps: mostly dead, some flickering, a few on.
            float side0 = Mod3(cx + cz) % 2 == 0 ? 1f : -1f;
            if (!(alongX && alongZ))
            {
                float r = WorldHash.Value(seed, cx, cz, 0x94);
                var at = On(0f, -side0 * 5.5f, 0.15f);
                c.Prop(PropKind.StreetLamp, at, Face(-side0), r < 0.55f ? 1 : 0, 9f);
                if (r < 0.55f) c.Light(On(0f, -side0 * 3.2f, 6.2f), 15f, new Color(1f, 0.62f, 0.22f), r < 0.3f ? FixtureState.Lit : FixtureState.Faulty);
            }
            // Trees on the verges, letterboxes, bins, a parked car, leaves.
            for (int i = 0; i < 4; i++)
                if (rng.Value() < 0.6f)
                {
                    float s = rng.Value() < 0.5f ? 1f : -1f;
                    var p = On(-12f + i * 8f + rng.Value() * 3f, s * (8f + rng.Value() * 5f));
                    if (!(alongX && alongZ) && Vector3.Distance(p, PoleAt(new Vector2Int(cx, cz))) > 3f) Tree(c, rng, p);
                }
            if (!(alongX && alongZ))
            {
                foreach (float s in new[] { -1f, 1f })
                    for (int k = 0; k < 2; k++)
                    {
                        if (rng.Value() < 0.6f) c.Prop(PropKind.Mailbox, On(-10f + k * 16f, s * 6.5f), Face(s), 0, 9f);
                        if (rng.Value() < 0.35f) c.Prop(PropKind.WheelieBin, On(-7.5f + k * 16f, s * 6.6f), Face(s) + rng.Value() * 20f, 0, 9f);
                    }
                if (rng.Value() < 0.4f) { float s = rng.Value() < 0.5f ? 1f : -1f; c.Prop(rng.Value() < 0.8f ? PropKind.OldCar : PropKind.TarpCar, On((rng.Value() - 0.5f) * 14f, s * 2.9f, 0.03f), alongX ? 90f : 0f, rng.Range(0, 100), 9f); }
                // Front hedges: something to duck behind.
                foreach (float s in new[] { -1f, 1f })
                    for (float a = -15f; a < 15f; a += 1.5f)
                        if (RockMesh.Noise(mid.x * 0.13f + a * 0.35f, s * 3f, mid.z * 0.13f) > -0.05f) HedgeBit(c, On(a, s * 10.5f));
            }
            // The power line, its hum, and the arrows that follow it.
            var route = Route(seed, out int main);
            int at_ = route.IndexOf(new Vector2Int(cx, cz));
            if (at_ >= 0 && at_ < route.Count - 1) Pole(c, seed, route, at_, main);
            if (alongX && alongZ)
            {
                var next = at_ >= 0 && at_ < route.Count - 1 ? route[at_ + 1] - route[at_] : new Vector2Int(rng.Value() < 0.5f ? 1 : -1, 0);
                c.Prop(PropKind.ArrowSign, mid + new Vector3(-6.6f, 0f, 6.6f), Mathf.Atan2(next.x, next.y) * Mathf.Rad2Deg, 0, 9f);
            }
            if (at_ == route.Count - 1) EndHouse(c, seed, cx, cz, main);
            if (cx == 0 && cz == 0)
                c.L.Readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(Landing + new Vector3(1.6f, -0.03f, 1.2f), Quaternion.Euler(0f, 60f, 0f), Vector3.one),
                    Title = "Back of a water bill", Text = "If you fell you're fine. Get OFF the road.\n\nThe thing to follow is the power line. It hums. Nothing in any of these houses is " +
                        "plugged into it, so it must be going somewhere that is.\n\nThe lights in the sky aren't looking for the way out. Under a porch, behind a hedge, under a car if " +
                        "you fit. Once one stops on you you've got about as long as it takes to say so.\n\nWhen the fog comes down the street you'll hear yourself thinking. That isn't you." });
        }

        static void HedgeBit(Ctx c, Vector3 at) => c.Leaf.Blob(at + Vector3.up * 0.62f, new Vector3(0.95f, 0.72f, 0.85f), Quaternion.identity, 0.3f, 10);

        /// A dead tree: trunk, boughs, twigs.
        static void Tree(Ctx c, WorldRng rng, Vector3 foot)
        {
            float tall = 3.5f + rng.Value() * 3f;
            var lean = new Vector3(rng.Value() - 0.5f, 0f, rng.Value() - 0.5f) * 0.9f;
            var mid = foot + lean * 0.5f + Vector3.up * tall * 0.5f;
            var top = foot + lean + Vector3.up * tall;
            c.Tree.Tube(new[] { foot - Vector3.up * 0.2f, foot + Vector3.up * 0.3f, mid, top }, new[] { 0.36f, 0.24f, 0.17f, 0.05f }, 8, 0.2f);
            for (int i = 0; i < 6; i++)
            {
                float t = 0.4f + i * 0.1f, a = i * 2.4f + rng.Value();
                var from = Vector3.Lerp(foot, top, t);
                var o = new Vector3(Mathf.Cos(a), 0.55f + rng.Value() * 0.4f, Mathf.Sin(a)) * (1.1f + rng.Value() * 1.2f);
                var bend = from + o * 0.55f;
                c.Tree.Tube(new[] { from, bend, from + o + Vector3.up * 0.35f }, new[] { 0.08f, 0.05f, 0.015f }, 6, 0.2f);
                for (int k = 0; k < 2; k++) c.Tree.Tube(new[] { bend, bend + new Vector3(rng.Value() - 0.5f, 0.5f, rng.Value() - 0.5f) * 1.1f }, new[] { 0.03f, 0.008f }, 4);
            }
        }

        static void Pole(Ctx c, uint seed, List<Vector2Int> route, int i, int main)
        {
            var foot = PoleAt(route[i]);
            c.Tree.Tube(new[] { foot - Vector3.up * 0.3f, foot + Vector3.up * 5f, foot + Vector3.up * 9.2f }, new[] { 0.17f, 0.14f, 0.11f }, 10, 0.05f);
            var along = route[i + 1] - route[i];
            var bar = new Vector3(Mathf.Abs(along.y) + 0.12f, 0.14f, Mathf.Abs(along.x) + 0.12f);
            bar = new Vector3(bar.x > 1f ? 2.4f : 0.14f, 0.14f, bar.z > 1f ? 2.4f : 0.14f);
            c.Box(foot + Vector3.up * 8.6f, bar, Wood, false);
            c.Tree.Tube(new[] { foot + new Vector3(0.25f, 6.6f, 0f), foot + new Vector3(0.25f, 7.7f, 0f) }, new[] { 0.24f, 0.24f }, 10);   // a transformer can
            c.L.Specials.Add(new SpecialSpec { Kind = SpecialKind.LineHum, Pose = Matrix4x4.Translate(foot + Vector3.up * 4f), Id = i });
            bool last = i == route.Count - 2;
            var to = last ? EndDoor(seed) + new Vector3(0f, 6.3f, main * 1.2f) : PoleAt(route[i + 1]) + Vector3.up * 8.7f;
            var side = new Vector3(along.y, 0f, along.x);
            foreach (float k in new[] { -1f, 0f, 1f })
            {
                var path = new Vector3[11];
                var radius = new float[11];
                for (int p = 0; p <= 10; p++)
                {
                    float t = p / 10f;
                    path[p] = Vector3.Lerp(foot + Vector3.up * 8.72f + side * k * 1.1f, to + side * k * (last ? 0.2f : 1.1f), t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.3f);
                    radius[p] = 0.02f;
                }
                c.Wire.Tube(path, radius, 4);
            }
        }

        // ---------- the other ground ----------

        static void RunwayChunk(Ctx c, WorldRng rng, int cx, float x0, float z0, int cz)
        {
            c.L.Quads.Add(new SurfaceQuad { Area = new Rect(x0, z0, S, S), Height = 0f, FacingUp = true, Surface = Asphalt });
            float edge = cx == RunwayX ? x0 + 1f : x0 + S - 1f;
            for (int i = 0; i < 8; i++)
            {
                if (Mod3(cz) == 1 && i == 3) continue;   // a panel torn out
                c.Prop(PropKind.ChainFence, new Vector3(edge, 0f, z0 + 2f + i * 4f), 90f, 0, 9f);
            }
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(cx == RunwayX ? x0 + 10f : x0 + S - 10f, 0.1f, z0 + 4f + i * 8f);
                c.Box(p, new Vector3(0.18f, 0.2f, 0.18f), new SurfaceRef(Surface.Trim, 0), false);
                c.Light(p + Vector3.up * 0.5f, 3.5f, new Color(0.35f, 0.55f, 1f));
            }
            c.Box(new Vector3(cx == RunwayX ? x0 + S : x0, 0.005f, z0 + 10f), new Vector3(0.5f, 0.02f, 12f), Trim, false);
            if (rng.Value() < 0.3f) c.Prop(PropKind.TrafficCone, new Vector3(x0 + rng.Value() * S, 0f, z0 + rng.Value() * S), 0f);
        }

        static void FieldChunk(Ctx c, WorldRng rng, int cx, float x0, float z0, int cz)
        {
            c.L.Quads.Add(new SurfaceQuad { Area = new Rect(x0, z0, S, S), Height = 0f, FacingUp = true, Surface = Lawn });
            for (int i = 0; i < 1500; i++)
            {
                var p = new Vector3(x0 + rng.Value() * S, 0f, z0 + rng.Value() * S);
                float a = rng.Value() * 6.283f, h = 0.7f + rng.Value() * 0.7f;
                if (Mod3(cz) == 1 && Mathf.Abs(p.z - z0 - 16f) < 1.2f) continue;   // the walkway
                c.Tuft.Leaf(p, p + new Vector3(Mathf.Cos(a) * 0.25f, h, Mathf.Sin(a) * 0.25f), new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * 0.035f);
            }
            if (Mod3(cz) == 1) c.Box(new Vector3(x0 + S / 2f, 0.04f, z0 + 16f), new Vector3(S, 0.08f, 1.8f), Pavement);
            // Fenced at the far side too: the fields go on (Level 10), but not for you, yet.
            float edge = cx == FieldX ? x0 + 1f : x0 + S - 1f;
            for (int i = 0; i < 8; i++)
            {
                if (cx != FieldX && Mod3(cz) == 1 && (i == 3 || i == 4)) continue;   // the near side is open at the walkway
                c.Prop(PropKind.ChainFence, new Vector3(edge, 0f, z0 + 2f + i * 4f), 90f, 0, 9f);
            }
        }

        /// A playground in place of a block's houses: white tube slides you can crawl through, swings, benches.
        static void Park(Ctx c, WorldRng rng, int cx, int cz, float x0, float z0)
        {
            for (int i = 0; i < 4; i++) Tree(c, rng, new Vector3(x0 + 3f + rng.Value() * 26f, 0f, z0 + (rng.Value() < 0.5f ? 2f : 29f)));
            c.Prop(PropKind.Bench, new Vector3(x0 + 5f, 0f, z0 + 6f), 40f);
            if (Mod3(cx) == 2) { c.Prop(PropKind.SwingSet, new Vector3(x0 + 12f, 0f, z0 + 16f), 20f, 0, 9f); c.Prop(PropKind.SwingSet, new Vector3(x0 + 20f, 0f, z0 + 12f), -30f, 0, 9f); return; }
            // A ring of tube with two mouths at the ground and a hump over the middle; windows glow white along it.
            var mid = new Vector3(x0 + 16f, 0f, z0 + 16f);
            const int n = 30;
            var path = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, a = 0.5f + t * 5.1f;
                float y = 0.88f + 1.5f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(t, 1f - t) * 5f - 0.15f));
                path[i] = mid + new Vector3(Mathf.Cos(a) * 9f, y, Mathf.Sin(a) * 9f);
            }
            c.Tube.Tube(path, Same(n + 1, 0.95f), 14);
            c.Tube.Tube(path, Same(n + 1, 0.88f), 14, 0f, true);
            for (int i = 3; i < n; i += 4)
            {
                var outward = (path[i] - mid); outward.y = 0f; outward.Normalize();
                c.Pane.Blob(path[i] + outward * 0.78f, new Vector3(0.3f, 0.3f, 0.3f), Quaternion.identity, 0f, 12);
                c.Light(path[i] + Vector3.up * 0.2f, 2.6f, new Color(0.9f, 0.95f, 1f));
                c.Tree.Tube(new[] { new Vector3(path[i].x, 0f, path[i].z), path[i] - Vector3.up * 0.9f }, new[] { 0.08f, 0.08f }, 8);
            }
            c.Prop(PropKind.AlmondWater, path[n / 2] - Vector3.up * 0.85f, 0f);
            c.Prop(PropKind.Battery, path[n / 2 + 2] - Vector3.up * 0.85f, 0f);
        }

        static float[] Same(int n, float v) { var a = new float[n]; for (int i = 0; i < n; i++) a[i] = v; return a; }

        // ---------- houses ----------

        struct House
        {
            public float Ox, Front, W, D;
            public int Sgn;             // +1: the house runs back along +z from its front wall; -1: along -z
            public bool Mirror, Open, Porch, GableFront;
            public int Siding, Paper;
            public uint H;
            public int Story;           // 0..2, or -1
        }

        static void Lots(Ctx c, WorldRng rng, uint seed, int cx, int cz, float x0, float z0)
        {
            int sgn = Mod3(cz) == 1 ? 1 : -1;   // each row faces its own street
            float front = sgn > 0 ? z0 + 4f : z0 + S - 4f;
            for (int k = 0; k < 2; k++)
            {
                int story = -1;
                for (int i = 0; i < 3; i++) if (k == 0 && StoryChunk(seed, i) == new Vector2Int(cx, cz)) story = i;
                uint h = WorldHash.Hash(seed, cx * 2 + k, cz, 0x950);
                if (story < 0 && k == 1 && WorldHash.Float(h, 9) < 0.12f) h = WorldHash.Hash(seed, cx * 2, cz, 0x950);   // the same house again, next door
                var house = new House
                {
                    H = h, W = story >= 0 ? 11f : 9.6f + Mathf.Floor(WorldHash.Float(h, 0) * 3f) * 0.9f, D = story >= 0 ? 9f : 8.6f + Mathf.Floor(WorldHash.Float(h, 1) * 3f) * 0.6f,
                    Sgn = sgn, Front = front + (story >= 0 ? 0f : sgn * WorldHash.Float(h, 2) * 1.5f), Mirror = story < 0 && WorldHash.Float(h, 3) < 0.5f, Open = story >= 0 || WorldHash.Float(h, 4) < 0.76f,
                    Porch = story >= 0 || WorldHash.Float(h, 5) < 0.6f, GableFront = WorldHash.Float(h, 6) < 0.45f, Siding = (int)(WorldHash.Float(h, 7) * Sidings), Paper = (int)(WorldHash.Float(h, 8) * Papers), Story = story,
                };
                house.Ox = x0 + k * 16f + (story >= 0 ? 2.5f : (16f - house.W) / 2f);
                Build(c, rng, house);
                // The impossible one: a second house clipped through the first, askew by a room.
                if (story < 0 && k == 0 && WorldHash.Float(h, 10) < 0.07f)
                {
                    var twin = house;
                    twin.Ox += 3.1f; twin.Front += sgn * 2.3f; twin.Siding = (house.Siding + 2) % Sidings; twin.Paper = (house.Paper + 1) % Papers; twin.Open = true; twin.Porch = false; twin.W = 9.6f;
                    Build(c, rng, twin);
                }
                Yard(c, rng, house, x0 + k * 16f, z0, sgn);
            }
        }

        static void Yard(Ctx c, WorldRng rng, House h, float lot, float z0, int sgn)
        {
            float back = sgn > 0 ? z0 + S : z0;           // where the two rows' yards meet
            float houseBack = h.Front + sgn * h.D;
            Vector3 At(float x, float depth) => new Vector3(lot + x, 0f, houseBack + sgn * depth);
            // Fences: down the side of each lot and along the back, with a panel missing to slip through.
            float deep = Mathf.Abs(back - houseBack);
            int gap = rng.Range(0, 3);
            for (int i = 0; i * 2f < deep - 1f; i++) if (i != gap + 1) c.Prop(PropKind.PlankFence, new Vector3(lot + 0.05f, 0f, houseBack + sgn * (1f + i * 2f)), 90f, rng.Range(0, 50), 9f);
            int hole = rng.Range(0, 8);
            if (sgn > 0) for (int i = 0; i < 8; i++) if (i != hole) c.Prop(PropKind.PlankFence, new Vector3(lot + 1f + i * 2f, 0f, back - 0.05f), 0f, rng.Range(0, 50), 9f);
            if (rng.Value() < 0.25f) c.Prop(PropKind.SwingSet, At(8f, 8f), rng.Value() * 60f, 0, 9f);
            if (rng.Value() < 0.4f) c.Prop(PropKind.Grill, At(4f + rng.Value() * 3f, 3f), rng.Value() * 360f, 0, 9f);
            if (rng.Value() < 0.5f) { c.Prop(PropKind.DiningTable, At(11f, 4f), rng.Value() * 40f); c.Prop(PropKind.WoodChair, At(11f, 5f), 180f); c.Prop(PropKind.WoodChair, At(12.4f, 4f), 270f); }
            for (int i = 0; i < 3; i++) if (rng.Value() < 0.5f) Tree(c, rng, At(2f + rng.Value() * 12f, deep * 0.55f + rng.Value() * 5f));
            for (int i = 0; i < 5; i++) if (rng.Value() < 0.5f) HedgeBit(c, At(1.5f + rng.Value() * 13f, deep - 1.2f));
            // The drive and the path to the door.
            float road = Mathf.Abs(h.Front - (sgn > 0 ? z0 : z0 + S)) + 10f;
            float door = h.Ox + (h.Mirror ? h.W - h.W / 2f : h.W / 2f);
            c.Box(new Vector3(door, 0.02f, h.Front - sgn * road / 2f), new Vector3(1.3f, 0.04f, road), Pavement);
            if (rng.Value() < 0.45f && h.W < 10.6f)
            {
                float dx = lot + 1.5f;
                c.Box(new Vector3(dx, 0.015f, h.Front - sgn * road / 2f), new Vector3(2.6f, 0.03f, road), Pavement);
                if (rng.Value() < 0.6f) c.Prop(rng.Value() < 0.7f ? PropKind.OldCar : PropKind.TarpCar, new Vector3(dx, 0.03f, h.Front - sgn * 4f), 0f, rng.Range(0, 100), 9f);
            }
        }

        static void Build(Ctx c, WorldRng rng, House h)
        {
            var L = c.L;
            SurfaceRef outside = new SurfaceRef(Surface.Wall, h.Siding), paper = new SurfaceRef(Surface.Wall, Sidings + h.Paper), paper2 = new SurfaceRef(Surface.Wall, Sidings + (h.Paper + 1) % Papers);
            float X(float lx) => h.Ox + (h.Mirror ? h.W - lx : lx);
            float Z(float lz) => h.Front + h.Sgn * lz;
            float Yaw(float y) { float r = h.Mirror ? -y : y; return h.Sgn > 0 ? r : 180f - r; }   // a yaw in house space (0 = facing the back)
            Vector3 P(float lx, float y, float lz) => new Vector3(X(lx), y, Z(lz));
            Bounds B(float x0, float x1, float y0, float y1, float z0, float z1)
            {
                var b = new Bounds(P(x0, y0, z0), Vector3.zero);
                b.Encapsulate(P(x1, y1, z1));
                return b;
            }
            void Box(float x0, float x1, float y0, float y1, float z0, float z1, SurfaceRef m, bool solid = true) => L.Boxes.Add(SurfaceBox.Uniform(B(x0, x1, y0, y1, z0, z1), m, BoxMeshBuilder.Faces.All, solid));
            void Furnish(PropKind k, float lx, float y, float lz, float yaw, int v = 0) => c.Prop(k, P(lx, y, lz), Yaw(yaw), v);
            // A wall with openings (centre, width, sill, top). `lo` faces the low side (left or front), `hi` the high side.
            void Wall(bool alongX, float at, float a0, float a1, float y0, float top, SurfaceRef lo, SurfaceRef hi, params (float c, float w, float sill, float top)[] holes)
            {
                void Piece(float s0, float s1, float b0, float b1)
                {
                    if (s1 - s0 < 0.02f || b1 - b0 < 0.02f) return;
                    var box = SurfaceBox.Uniform(alongX ? B(s0, s1, b0, b1, at - Thick / 2f, at + Thick / 2f) : B(at - Thick / 2f, at + Thick / 2f, b0, b1, s0, s1), hi, BoxMeshBuilder.Faces.All);
                    if (alongX) { if (h.Sgn > 0) { box.NegZ = lo; box.PosZ = hi; } else { box.PosZ = lo; box.NegZ = hi; } }
                    else { if (!h.Mirror) { box.NegX = lo; box.PosX = hi; } else { box.PosX = lo; box.NegX = hi; } }
                    L.Boxes.Add(box);
                }
                float from = a0;
                foreach (var o in holes)
                {
                    Piece(from, o.c - o.w / 2f, y0, top);
                    Piece(o.c - o.w / 2f, o.c + o.w / 2f, y0, y0 + o.sill);
                    Piece(o.c - o.w / 2f, o.c + o.w / 2f, y0 + o.top, top);
                    if (o.sill > 0.1f)
                        L.Props.Add(new PropSpec { Kind = PropKind.HouseWindow, Pose = Matrix4x4.TRS(alongX ? P(o.c, y0 + o.sill, at) : P(at, y0 + o.sill, o.c), Quaternion.Euler(0f, alongX ? 0f : 90f, 0f), new Vector3(o.w, o.top - o.sill, 1f)), Ceiling = 9f });
                    from = o.c + o.w / 2f;
                }
                Piece(from, a1, y0, top);
            }
            float W = h.W, D = h.D, f1 = Ground, f2 = Ground + Storey, eave = Ground + Storey * 2f - 0.1f;
            float hallL = W / 2f - 1.45f, hallR = W / 2f + 1.45f;
            (float, float, float, float) Win(float at, float w = 1.2f) => (at, w, 0.85f, 2.1f);
            (float, float, float, float) Door(float at) => (at, 0.95f, 0f, 2.12f);

            Box(-0.05f, W + 0.05f, -0.2f, f1 - 0.02f, -0.05f, D + 0.05f, Pavement);   // the plinth
            // Outside walls.
            Wall(true, 0f, 0f, W, f1, f2, outside, paper, Win(hallL / 2f, 1.5f), (W / 2f, 1f, 0f, 2.12f), Win((hallR + W) / 2f, 1.5f));
            Wall(true, 0f, 0f, W, f2, eave, outside, paper2, Win(hallL / 2f), Win(W / 2f, 0.9f), Win((hallR + W) / 2f));
            if (h.Open) Wall(true, D, 0f, W, f1, f2, paper, outside, Win(hallL / 2f), Door(W / 2f), Win((hallR + W) / 2f));
            else Wall(true, D, 0f, W, f1, f2, paper, outside, Win(hallL / 2f), Win((hallR + W) / 2f));
            Wall(true, D, 0f, W, f2, eave, paper2, outside, Win(hallL / 2f), Win((hallR + W) / 2f));
            Wall(false, 0f, 0f, D, f1, f2, outside, paper, Win(D * 0.3f), Win(D * 0.72f));
            Wall(false, 0f, 0f, D, f2, eave, outside, paper2, Win(D * 0.25f, 0.9f), Win(D * 0.75f, 0.9f));
            Wall(false, W, 0f, D, f1, f2, paper, outside, Win(D * 0.25f), Win(D * 0.75f, 0.9f));
            Wall(false, W, 0f, D, f2, eave, paper2, outside, Win(D * 0.25f, 0.9f), Win(D * 0.75f, 0.6f));
            Roof(c, h, P, W, D, eave, outside);
            // The front door, and a step or a porch.
            L.Specials.Add(new SpecialSpec { Kind = SpecialKind.HouseDoor, Pose = Matrix4x4.TRS(P(W / 2f, f1, 0f), Quaternion.Euler(0f, h.Sgn > 0 ? 180f : 0f, 0f), Vector3.one), Id = (int)(h.H & 0x7FFFFF) * 2 + (h.Open ? 0 : 1), Size = new Vector3(1f, 2.12f, 0f) });
            if (h.Porch)
            {
                Box(W / 2f - 2.2f, W / 2f + 2.2f, 0f, f1 - 0.02f, -2.2f, -0.05f, Wood);
                Box(W / 2f - 0.8f, W / 2f + 0.8f, 0f, 0.15f, -2.6f, -2.2f, Pavement);
                Box(W / 2f - 2.4f, W / 2f + 2.4f, f1 + 2.5f, f1 + 2.66f, -2.4f, 0f, Trim);
                foreach (float px in new[] { -2.05f, 2.05f })
                {
                    Box(W / 2f + px - 0.07f, W / 2f + px + 0.07f, f1 - 0.02f, f1 + 2.5f, -2.1f, -1.96f, Trim);
                    Box(Mathf.Min(W / 2f + px, W / 2f + px * 0.42f), Mathf.Max(W / 2f + px, W / 2f + px * 0.42f), f1 + 0.85f, f1 + 0.92f, -2.06f, -2f, Trim);
                    Box(W / 2f + px - 0.03f, W / 2f + px + 0.03f, f1 + 0.85f, f1 + 0.92f, -2f, -0.08f, Trim);
                }
            }
            else Box(W / 2f - 0.8f, W / 2f + 0.8f, 0f, f1 - 0.02f, -0.9f, -0.05f, Pavement);
            if (!h.Open) return;   // a shell: nobody's getting in

            // Ground floor.
            Box(0.08f, W - 0.08f, f1 - 0.02f, f1, 0.08f, D - 0.08f, Wood);
            Box(0.08f, hallL - 0.08f, f1, f1 + 0.012f, 0.08f, D - 0.08f, Carpet, false);
            float part = D * 0.5f;
            Wall(false, hallL, 0.08f, D - 0.08f, f1, f2 - 0.25f, paper, paper, (1.15f, 1.3f, 0f, 2.2f), Door(D - 1.0f));
            Wall(false, hallR, 0.08f, D - 0.08f, f1, f2 - 0.25f, paper, paper, Door(1.6f), Door(D - 1.1f));
            Wall(true, part, hallR + 0.08f, W - 0.08f, f1, f2 - 0.25f, paper, paper, (Mathf.Lerp(hallR, W, 0.5f), 1.2f, 0f, 2.2f));
            // The stairs: up the left of the hall, over a hole in the floor above.
            const int steps = 14;
            float run = 0.27f, rise = Storey / steps, sx0 = hallL + 0.1f, sx1 = hallL + 1.15f, sz0 = 2.1f, sz1 = sz0 + run * steps;
            for (int i = 0; i < steps; i++) Box(sx0, sx1, f1, f1 + rise * (i + 1), sz0 + run * i, sz0 + run * (i + 1), Wood);
            // Upper floor, round the stairwell.
            void Slab(float ax, float bx, float az, float bz)
            {
                var b = SurfaceBox.Uniform(B(ax, bx, f2 - 0.25f, f2, az, bz), Plaster, BoxMeshBuilder.Faces.All);
                b.PosY = Carpet;
                L.Boxes.Add(b);
            }
            Slab(0.08f, sx0, 0.08f, D - 0.08f); Slab(sx1, W - 0.08f, 0.08f, D - 0.08f); Slab(sx0, sx1, 0.08f, sz0); Slab(sx0, sx1, sz1, D - 0.08f);
            Box(0.08f, W - 0.08f, eave - 0.06f, eave, 0.08f, D - 0.08f, Plaster);
            Box(sx1, sx1 + 0.05f, f2, f2 + 0.9f, sz0, sz1 - 0.1f, Trim); Box(sx0, sx1, f2, f2 + 0.9f, sz0 - 0.05f, sz0, Trim);   // banisters
            Wall(false, hallL, 0.08f, D - 0.08f, f2, eave - 0.06f, paper2, paper2, Door(1.2f), Door(D - 1.0f));
            Wall(false, hallR, 0.08f, D - 0.08f, f2, eave - 0.06f, paper2, paper2, Door(1.6f), Door(D - 1.1f));
            Wall(true, part, 0.08f, hallL - 0.08f, f2, eave - 0.06f, paper2, paper2);
            Wall(true, part, hallR + 0.08f, W - 0.08f, f2, eave - 0.06f, paper2, paper2);

            // Furniture. The living room's table is always in the same place in a story house.
            var r = new WorldRng(h.H ^ 0x5A5Au);
            float lw = hallL;
            if (h.Story >= 0) Furnish(PropKind.Desk, 1.9f, f1, 4.6f, 0f);
            else Furnish(PropKind.CoffeeTable, lw * 0.5f, f1, D * 0.42f, 90f);
            Furnish(PropKind.Rug, lw * 0.5f, f1 + 0.013f, D * 0.42f, 0f, r.Range(0, 50));
            Furnish(PropKind.Sofa, lw * 0.5f, f1, D * 0.42f + 1.9f, 180f, r.Range(0, 50));
            if (r.Value() < 0.7f) Furnish(PropKind.Armchair, 0.85f, f1, D * 0.3f, 60f, r.Range(0, 50));
            if (h.Story < 0) Furnish(PropKind.CrtTv, lw * 0.5f, f1, 0.6f, 0f, 1);
            if (r.Value() < 0.7f) Furnish(PropKind.Bookshelf, 0.12f, f1, D * 0.51f, 90f, r.Range(0, 50));
            if (r.Value() < 0.6f) Furnish(PropKind.FloorLamp, 0.5f, f1, D - 0.6f, 0f);
            float rx = (hallR + W) / 2f;
            Furnish(PropKind.DiningTable, rx, f1, part * 0.5f, 90f);
            foreach (float s in new[] { -1f, 1f }) { if (r.Value() < 0.8f) Furnish(PropKind.WoodChair, rx + s * 0.8f, f1, part * 0.5f + 0.35f, s > 0 ? 270f : 90f); if (r.Value() < 0.5f) Furnish(PropKind.WoodChair, rx + s * 0.8f, f1, part * 0.5f - 0.4f, s > 0 ? 270f : 90f); }
            Furnish(PropKind.BreakCounter, rx - 0.1f, f1, D - 0.1f, 180f);
            Furnish(PropKind.Stove, W - 0.12f, f1, D - 1.4f, 270f);
            Furnish(PropKind.Fridge, W - 0.12f, f1, part + 0.7f, 270f);
            // Upstairs: two bedrooms on the left, a bedroom and a bathroom on the right.
            Furnish(PropKind.Bed, 1.2f, f2, part - 1.9f, 180f, r.Range(0, 50));
            Furnish(PropKind.Nightstand, 2.4f, f2, part - 0.4f, 180f);
            Furnish(PropKind.Bed, 1.2f, f2, D - 1.2f, 180f, r.Range(0, 50));
            if (r.Value() < 0.6f) Furnish(PropKind.Desk, 0.2f, f2, part + 1f, 90f);
            Furnish(PropKind.Bed, W - 1.1f, f2, 1.3f, 0f, r.Range(0, 50));
            if (r.Value() < 0.6f) Furnish(PropKind.Bookshelf, W - 0.12f, f2, part - 0.8f, 270f, 4);
            Furnish(PropKind.Bathtub, W - 1f, f2, D - 0.12f, 180f);
            Furnish(PropKind.Toilet, W - 0.12f, f2, part + 0.7f, 270f);
            // Supplies: not in every house, and not where you'd trip on them.
            if (r.Value() < 0.4f) Furnish(PropKind.AlmondWater, rx - 0.5f, f1 + 0.9f, D - 0.4f, 0f);
            if (r.Value() < 0.3f) Furnish(PropKind.Battery, 2.4f, f2 + 0.57f, part - 0.42f, 0f);
            if (h.Story >= 0) StoryHouse(c, h, P, Yaw, W, D, f1, hallL);
        }

        /// STORY_PLAN 8i. Places only: the Story build hangs its documents on `StorySpot`.
        static void StoryHouse(Ctx c, House h, System.Func<float, float, float, Vector3> P, System.Func<float, float> yaw, float W, float D, float f1, float hallL)
        {
            switch (h.Story)
            {
                case 0:   // a motel's key rack by the door; a cup on the table
                    c.Prop(PropKind.KeyRack, P(W / 2f + 1.37f, f1 + 1.5f, 0.9f), yaw(270f));
                    break;
                case 1:   // paper on every wall
                    for (int i = 0; i < 9; i++)
                    {
                        c.Prop(PropKind.PinnedPapers, P(0.09f, f1 + 1.35f, 0.6f + i * 0.92f), yaw(90f), i);
                        if (i < 7 && i != 1) c.Prop(PropKind.PinnedPapers, P(hallL - 0.09f, f1 + 1.35f, 2.2f + i * 0.75f), yaw(270f), i + 20);
                    }
                    break;
                case 2:   // the porch light, the knot on the rail, the tape machine and a chair to sit in
                    c.Light(P(W / 2f, f1 + 2.3f, -1.2f), 3.2f, new Color(1f, 0.75f, 0.4f));
                    c.Prop(PropKind.KnotRail, P(W / 2f + 1.3f, f1 + 0.92f, -2.03f), yaw(180f));
                    c.Prop(PropKind.TapeMachine, P(1.9f, f1 + 0.75f, 4.6f), yaw(0f));
                    c.Prop(PropKind.WoodChair, P(1.9f, f1, 5.4f), yaw(180f));
                    break;
            }
        }

        static void Roof(Ctx c, House h, System.Func<float, float, float, Vector3> P, float W, float D, float eave, SurfaceRef siding)
        {
            const float o = 0.45f, pitch = 0.62f;
            float half = (h.GableFront ? W : D) / 2f, ridge = eave + half * pitch, low = eave - o * pitch;
            var gable = new RockMesh(siding);
            c.L.Rocks.Add(gable);
            void Plane(Vector3 a, Vector3 b, Vector3 d, Vector3 e)
            {
                c.Roof.Panel(a, b, d, e);
                var down = Vector3.down * 0.14f;
                c.Roof.Panel(a + down, b + down, d + down, e + down);
                c.Roof.Panel(a, b, b + down, a + down);   // the eave's edge
                c.Roof.Panel(b, d, d + down, b + down);
                c.Roof.Panel(a, e, e + down, a + down);
            }
            if (h.GableFront)
            {
                foreach (float s in new[] { -1f, 1f })
                    Plane(P(W / 2f + s * (half + o), low, -o), P(W / 2f + s * (half + o), low, D + o), P(W / 2f, ridge, D + o), P(W / 2f, ridge, -o));
                foreach (float z in new[] { 0f, D }) gable.Panel(P(0f, eave - 0.06f, z), P(W, eave - 0.06f, z), P(W / 2f, ridge - 0.1f, z), P(W / 2f, ridge - 0.1f, z));
            }
            else
            {
                foreach (float s in new[] { -1f, 1f })
                    Plane(P(-o, low, D / 2f + s * (half + o)), P(W + o, low, D / 2f + s * (half + o)), P(W + o, ridge, D / 2f), P(-o, ridge, D / 2f));
                foreach (float x in new[] { 0f, W }) gable.Panel(P(x, eave - 0.06f, 0f), P(x, eave - 0.06f, D), P(x, ridge - 0.1f, D / 2f), P(x, ridge - 0.1f, D / 2f));
            }
        }

        /// Where the wires go: a house in the middle of the crossroads, its porch light on. Its door is Level 0.
        static void EndHouse(Ctx c, uint seed, int cx, int cz, int main)
        {
            var mid = new Vector3((cx + 0.5f) * S, 0f, (cz + 0.5f) * S);
            var h = new House { W = 11f, D = 9f, Ox = mid.x - 5.5f, Front = mid.z - main * 4.5f, Sgn = main, Open = false, Porch = true, GableFront = true, Siding = 0, Paper = 0, Story = -1, H = WorldHash.Hash(seed, 9, 9, 0x99) };
            int specials = c.L.Specials.Count;
            Build(c, new WorldRng(7), h);
            c.L.Specials.RemoveRange(specials, c.L.Specials.Count - specials);   // no ordinary door: the exit stands in its place
            var door = EndDoor(seed);
            c.L.Exits.Add(new ExitSpec { Kind = ExitKind.Door, Target = "0", Pose = Matrix4x4.TRS(new Vector3(door.x, Ground, mid.z - main * 4.52f), Quaternion.Euler(0f, main > 0 ? 180f : 0f, 0f), Vector3.one),
                                         Trigger = new Bounds(new Vector3(door.x, Ground + 1.1f, mid.z - main * 4.8f), new Vector3(1f, 2.1f, 0.7f)) });
            c.Light(new Vector3(door.x, Ground + 2.3f, mid.z - main * 5.8f), 3.6f, new Color(1f, 0.78f, 0.45f));
            foreach (float dx in new[] { -3.4f, 3.4f }) c.Light(new Vector3(door.x + dx, 1.7f, mid.z - main * 4f), 2.6f, new Color(1f, 0.82f, 0.5f));   // the only windows lit in the whole level
        }

        /// Self-check: the line is a chain of neighbouring street chunks and never reaches the airport or the fields.
        public static string CheckLine(uint seed)
        {
            var cells = Route(seed, out int main);
            for (int i = 1; i < cells.Count; i++)
            {
                var d = cells[i] - cells[i - 1];
                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) != 1) return "the power line jumps";
                if (Mod3(cells[i].x) != 0 && Mod3(cells[i].y) != 0) return "the power line leaves the streets";
                if (Runway(cells[i].x) || Field(cells[i].x)) return "the power line leaves the suburb";
                if (cells.IndexOf(cells[i]) != i) return "the power line crosses itself";
            }
            for (int i = 0; i < 3; i++) if (IsPark(seed, StoryChunk(seed, i).x, StoryChunk(seed, i).y)) return $"story house {i} is in a park";
            return null;
        }
    }
}
