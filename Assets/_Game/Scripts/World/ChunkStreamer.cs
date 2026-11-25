using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms.World
{
    /// Builds the current level's chunks around the player and removes the ones left behind.
    public sealed class ChunkStreamer : MonoBehaviour
    {
        [SerializeField] Transform player;
        [Tooltip("Chunks kept loaded in each direction around the player.")]
        [SerializeField] int viewRadius = 2;   // 5 x 5 chunks: chunks build in ~20 ms now, so long sightlines can stay filled
        [SerializeField] int chunksPerFrame = 1;
        [Tooltip("Real-time lights are only placed within this distance; farther panels just glow.")]
        [SerializeField] float lightDistance = 28f;

        sealed class Chunk
        {
            public int Floor;
            public GameObject Root;
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public readonly List<Emitter> Emitters = new List<Emitter>();
            public readonly List<SurfaceQuad> Water = new List<SurfaceQuad>();
        }

        readonly Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();   // (x, floor, z)
        readonly List<Vector3Int> toBuild = new List<Vector3Int>();
        // Layouts being generated on worker threads; the main thread only turns finished ones into objects.
        readonly Dictionary<Vector3Int, System.Threading.Tasks.Task<ChunkLayout>> pending = new Dictionary<Vector3Int, System.Threading.Tasks.Task<ChunkLayout>>();
        const int MaxPending = 3;
        readonly List<List<Emitter>> emitterSets = new List<List<Emitter>>();
        LevelTheme theme;
        LevelCatalog catalog;
        Material[] paints;   // runtime copy, so the animated TV static doesn't touch the asset
        uint seed;
        Vector2Int center;
        LightRig rig;
        AudioClip ring;
        float nextLights;

        public static ChunkStreamer Instance { get; private set; }
        public LevelTheme Theme => theme;

        void Awake() => Instance = this;

        /// Leave the world completely empty (used by the isolated co-op preparation room).
        /// The next Load builds its level from scratch, so no lobby geometry or chunks can leak into a run.
        public void Unload()
        {
            foreach (var chunk in chunks.Values) Unload(chunk);
            chunks.Clear();
            toBuild.Clear();
            pending.Clear();
            ExtraFocus.Clear();
            extraWant.Clear();
            rig?.Clear();
            ClearDarkPockets();
            theme = null;
            Static = null;
            Blackout = shownBlackout = 0f;
        }


        /// A hand-made level: what was found in its model (light panels, exit signs, cakes, bottles), world space.
        public sealed class StaticInfo
        {
            public GameObject Root;
            public readonly List<Vector3> Lights = new List<Vector3>(), ExitSigns = new List<Vector3>(), Cakes = new List<Vector3>(), Bottles = new List<Vector3>();
            public readonly List<Bounds> Windows = new List<Bounds>();   // window panes (Level 188)
            public readonly List<Vector3> Curios = new List<Vector3>();   // odd objects (Level 188's vases, books, gramophone)
            public Material SmileDecal;
        }
        public StaticInfo Static { get; private set; }
        public bool IsStatic => theme != null && theme.generator == LevelGenerator.Static;

        public uint Seed => seed;

        /// A chunk was built (key = x, floor, z): level goals place their items and doors then.
        public event System.Action<Vector3Int> ChunkReady;
        public bool Loaded(Vector3Int key) => chunks.ContainsKey(key);

        /// Water surface in the loaded chunk under a world position (false for dry rooms and unloaded chunks).
        public bool TryWaterSurfaceAt(Vector3 position, out float surface)
        {
            if (theme != null && theme.generator == LevelGenerator.Caves) return CaveWater.TrySurface(position, out surface);   // pools and floods, not a level-wide waterline
            var c = ChunkOf(position);
            if (chunks.TryGetValue(new Vector3Int(c.x, FloorAt(position.y), c.y), out var chunk))
            {
                var point = new Vector2(position.x, position.z);
                for (int i = 0; i < chunk.Water.Count; i++)
                    if (chunk.Water[i].Area.Contains(point))
                    {
                        surface = chunk.Water[i].Height + (theme.generator == LevelGenerator.Ocean ? RollingSea.Swell(position.x, position.z) : 0f);
                        return true;
                    }
            }
            surface = float.NegativeInfinity;
            return false;
        }

        /// The floor of Level 0 the player is on (0 for single-floor levels).
        public int CurrentFloor { get; private set; }

        int FloorAt(float y) => DistrictLayout.HasFloors(theme)
            ? Mathf.Clamp(DistrictLayout.FloorOf(y), DistrictLayout.BottomFloor, DistrictLayout.TopFloor)
            // Stairwell layer 0 is the entrance flight [0,-8]; layer -1 is [-8,-16].
            : theme != null && theme.generator == LevelGenerator.Stairwell ? Mathf.CeilToInt(Mathf.Min(y, -0.001f) / DistrictLayout.FloorHeight) : 0;

        /// Copy drift where the player is (0..1), eased so the air changes gradually as you walk.
        public float CurrentDrift { get; private set; }

        /// Distance to the nearest working light; large means you're standing in the dark.
        public float NearestLight => rig != null && Blackout < 0.6f ? rig.NearestDistance : float.MaxValue;

        /// The nearest working light's spot within `range`, or null (moths gather round it).
        public Vector3? NearestLightPoint(Vector3 p, float range)
        {
            if (Blackout >= 0.6f) return null;
            Vector3? best = null;
            float bestSq = range * range;
            foreach (var set in emitterSets)
                foreach (var e in set)
                {
                    if (e.Floor || (pockets.Count > 0 && InDarkPocket(e.Position))) continue;
                    float dx = e.Position.x - p.x, dz = e.Position.z - p.z, d = dx * dx + dz * dz;
                    if (d < bestSq) { bestSq = d; best = e.Position; }
                }
            return best;
        }

        /// Flat distance from any spot on this floor to the nearest light panel (Smilers keep to the dark).
        public float LightDistanceAt(Vector3 p)
        {
            if (Blackout >= 0.6f) return float.MaxValue;
            float best = float.MaxValue;
            foreach (var set in emitterSets)
                foreach (var e in set)
                {
                    if (pockets.Count > 0 && InDarkPocket(e.Position)) continue;
                    float dx = e.Position.x - p.x, dz = e.Position.z - p.z, d = dx * dx + dz * dz;
                    if (d < best) best = d;
                }
            return Mathf.Sqrt(best);
        }

        /// The lights die (0 = normal, 1 = black). Set by the fear director; eased here.
        public float Blackout;
        float shownBlackout;

        /// A patch of dead lights around a lurking Smiler: Levels 2 and 3 are lit nearly everywhere, and canon
        /// keeps Smilers in the dark, so the dark comes with it. The lenses go grey, the lights and hum stop.
        public sealed class DarkPocket { public Vector3 At; public float Radius; public readonly List<GameObject> Covers = new List<GameObject>(); }
        readonly List<DarkPocket> pockets = new List<DarkPocket>();

        public DarkPocket AddDarkPocket(Vector3 at, float radius)
        {
            var pocket = new DarkPocket { At = at, Radius = radius };
            foreach (var set in emitterSets)
                foreach (var e in set)
                {
                    float dx = e.Position.x - at.x, dz = e.Position.z - at.z;
                    if (e.Floor || dx * dx + dz * dz > radius * radius || Mathf.Abs(e.Position.y - at.y) > 5f) continue;
                    var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(cover.GetComponent<Collider>());
                    cover.name = "Dead Panel";
                    cover.transform.SetParent(transform, false);
                    cover.transform.position = e.Position + Vector3.down * 0.02f;
                    cover.transform.localScale = e.AlongX ? new Vector3(1.04f, 0.015f, 0.54f) : new Vector3(0.54f, 0.015f, 1.04f);
                    var r = cover.GetComponent<MeshRenderer>();
                    r.sharedMaterial = catalog.panelDead;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    pocket.Covers.Add(cover);
                }
            pockets.Add(pocket);
            nextLights = 0f;
            return pocket;
        }

        public void RemoveDarkPocket(DarkPocket pocket)
        {
            if (pocket == null || !pockets.Remove(pocket)) return;
            foreach (var c in pocket.Covers) if (c != null) Destroy(c);
            nextLights = 0f;
        }

        void ClearDarkPockets()
        {
            foreach (var k in pockets) foreach (var c in k.Covers) if (c != null) Destroy(c);
            pockets.Clear();
        }

        bool InDarkPocket(Vector3 p)
        {
            foreach (var k in pockets)
            {
                float dx = p.x - k.At.x, dz = p.z - k.At.z;
                if (dx * dx + dz * dz <= k.Radius * k.Radius && Mathf.Abs(p.y - k.At.y) < 5f) return true;
            }
            return false;
        }
        Material panelLit;   // our own copy, so a blackout never edits the asset
        Material glowLit;    // the attic's orange glow between floorboards
        Material dampMat;    // wet carpet
        Color panelGlow;
        string glowProperty = "_BaseColor";
        Atmosphere atmosphere;

        /// Which kind of district the player is in (Level 0 and its variants), for the debug readout.
        public string Where => theme == null ? "" : theme.generator == LevelGenerator.Districts
            ? DistrictLayout.KindAt(seed, center.x, center.y, theme.districts).ToString()
            : theme.generator == LevelGenerator.Stairwell ? "endless stairwell"
            : theme.generator == LevelGenerator.Ocean ? "thalassophobia ocean"
            : theme.generator == LevelGenerator.Caves ? "karst cave system"
            : theme.generator == LevelGenerator.Suburbs ? "darkened suburbs"
            : theme.generator == LevelGenerator.Static ? "hand-built" : "maze";

        /// The adaptive layer that picks curiosities (optional).
        public ICuriosityResolver Curiosities { get; set; }

        /// Asks whether a pickup (by id) was already taken; taken ones are not rebuilt.
        public System.Func<int, bool> IsPicked { get; set; }

        /// Co-op (the owner): friends' positions. Their surroundings stay loaded too (for creatures to path in).
        public readonly List<Vector3> ExtraFocus = new List<Vector3>();
        HashSet<Vector3Int> extraWant = new HashSet<Vector3Int>();
        float nextExtra;

        /// Prop materials (one per PropPaint), for other systems that draw props (the bag).
        public Material[] Paints => paints;
        /// The level's main wall material (Level 6 builds walls at runtime).
        public Material WallMaterial => catalog != null && theme != null ? catalog.Resolve(theme, new SurfaceRef(Surface.Wall)) : null;

        /// Replaces whatever level is loaded with `level`, fully built around `startChunk` before returning.
        public void Load(LevelTheme level, LevelCatalog shared, uint levelSeed, Vector2Int startChunk)
        {
            if (rig == null)
            {
                rig = new LightRig(transform, Audio.ProceduralAudio.Hum()) { Suppressed = p => pockets.Count > 0 && InDarkPocket(p) };
                ring = Audio.ProceduralAudio.Ring();
            }
            if (paints == null)
            {
                paints = (Material[])shared.propPaints.Clone();
                int s = (int)PropPaint.Static;
                if (s < paints.Length && paints[s] != null) paints[s] = new Material(paints[s]);
            }
            foreach (var chunk in chunks.Values) Unload(chunk);
            chunks.Clear();
            pending.Clear();   // anything still running belongs to the old level; its results are dropped
            rig.Clear();
            theme = level;
            catalog = shared;
            if (panelLit == null)
            {
                panelLit = new Material(shared.panelLit);
                glowProperty = panelLit.HasProperty(HdMat.EmissiveProperty) ? HdMat.EmissiveProperty : "_BaseColor";
                panelGlow = panelLit.GetColor(glowProperty);
            }
            if (atmosphere == null) atmosphere = Atmosphere.Instance != null ? Atmosphere.Instance : new GameObject("Atmosphere").AddComponent<Atmosphere>();
            atmosphere.Apply(level);
            Blackout = shownBlackout = 0f;
            ClearDarkPockets();
            ApplyBlackout();
            seed = levelSeed;
            Static = null;
            if (IsStatic)
            {
                CurrentDrift = 0f;
                CurrentFloor = 0;
                LoadStatic();
                RefreshLights();
                return;
            }
            CurrentDrift = DistrictLayout.DriftAtChunk(levelSeed, startChunk.x, startChunk.y, level);
            CurrentFloor = 0;
            Recenter(startChunk, 0);
            while (toBuild.Count > 0) BuildNext();
            RefreshLights();
        }

        void Update()
        {
            if (theme == null) return;
            if (!IsStatic)
            {
                // While spectating a friend, stream the world around them so you see their level, not where you fell.
                var focus = player.position;
                if (player.TryGetComponent(out Backrooms.Player.Spectator spec) && spec.Watching && spec.Target != null)
                    focus = spec.Target.position;
                var c = ChunkOf(focus);
                int f = FloorAt(focus.y);
                bool extraChanged = false;
                if (Time.time >= nextExtra)
                {
                    nextExtra = Time.time + 0.5f;
                    var keys = new HashSet<Vector3Int>();
                    foreach (var p in ExtraFocus)
                    {
                        var pc = ChunkOf(p);
                        int pf = FloorAt(p.y);
                        for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            keys.Add(new Vector3Int(pc.x + dx, pf, pc.y + dz));
                    }
                    if (!keys.SetEquals(extraWant)) { extraWant = keys; extraChanged = true; }
                }
                if (c != center || f != CurrentFloor || extraChanged)
                {
                    bool newFloor = f != CurrentFloor;
                    CurrentFloor = f;
                    Recenter(c, f);
                    if (newFloor) RefreshLights();
                }
                StreamChunks();
            }
            if (Time.time >= nextLights) RefreshLights();
            if (!Mathf.Approximately(shownBlackout, Blackout))
            {
                // Lights go out fast, with a stutter; they come back slowly.
                float speed = Blackout > shownBlackout ? 1.5f : 0.35f;
                shownBlackout = Mathf.MoveTowards(shownBlackout, Blackout, Time.deltaTime * speed);
                ApplyBlackout();
            }
            rig.Tick(Time.time);
            // Copy drift thickens and sours the air.
            float target = IsStatic ? 0f : DistrictLayout.DriftAtChunk(seed, center.x, center.y, theme);
            CurrentDrift = Mathf.MoveTowards(CurrentDrift, target, Time.deltaTime * 0.05f);
            if (atmosphere != null) atmosphere.SetDrift(Mathf.Round(CurrentDrift * 50f) / 50f);
            int s = (int)PropPaint.Static;
            if (paints != null && s < paints.Length && paints[s] != null)
                paints[s].SetTextureOffset("_EmissiveColorMap", new Vector2(Random.value, Random.value));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var chunk in chunks.Values) Unload(chunk);
        }

        /// A hand-made level: the model, solid where you'd walk into it, with real lights at its light panels.
        void LoadStatic()
        {
            if (theme.staticLevel == null)
            {
                Debug.LogError($"Static level '{theme.displayName}' has no model assigned; refusing to generate a different level.");
                return;
            }
            var root = Instantiate(theme.staticLevel, transform);
            root.name = theme.displayName;
            // Centre the model on the origin (floor stays at 0), whatever origin it was built around.
            var all = new Bounds();
            bool first = true;
            foreach (var rr in root.GetComponentsInChildren<MeshRenderer>()) { if (first) { all = rr.bounds; first = false; } else all.Encapsulate(rr.bounds); }
            root.transform.position -= new Vector3(all.center.x, 0f, all.center.z);
            var chunk = new Chunk { Floor = 0, Root = root };
            var info = new StaticInfo { Root = root };
            var doubled = new HashSet<Material>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                string n = mf.name;
                var r = mf.GetComponent<MeshRenderer>();
                foreach (var m in r.sharedMaterials) if (m != null && doubled.Add(m)) HdMat.DoubleSided(m);
                // Solid, except the things you'd walk under or through: lights, glows, decals, balloons, gifts.
                if (!(n.Contains("Light") || n.Contains("Glow") || n.Contains("Decals") || n.Contains("Balloons") || n.Contains("Gifts")
                      || n.Contains("ExteriorWindows") || n.Contains("Detail")))   // panes and trim: big meshes nobody walks into
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                if (n.Contains("Decals")) r.shadowCastingMode = ShadowCastingMode.Off;
                if (n.Contains("Smiles")) info.SmileDecal = r.sharedMaterial;
                // Real lights wherever the model has a light material (panels, strips, glowing rooms).
                var mats = r.sharedMaterials;
                for (int sm = 0; sm < mats.Length; sm++)
                {
                    if (mats[sm] == null || !mats[sm].name.Contains("Light")) continue;
                    foreach (var c in Islands(mf, mats.Length == 1 ? -1 : sm, 0.3f))
                    {
                        info.Lights.Add(c);
                        float range = c.y > 8f ? Mathf.Min(30f, c.y + 6f) : 8f;   // a tall atrium needs light that reaches the floor
                        chunk.Emitters.Add(new Emitter { Position = c + Vector3.down * 0.05f, Range = range, Id = (int)WorldHash.Hash(seed, Mathf.RoundToInt(c.x * 2f), Mathf.RoundToInt(c.z * 2f) + Mathf.RoundToInt(c.y), 0xF0F) });
                    }
                }
                if (n.Contains("Windows") && !n.Contains("Exterior")) info.Windows.AddRange(IslandBounds(mf, SubmeshOf(r, "Glass")));   // the courtyard's own windows
                if (n.Contains("Props")) info.Curios.AddRange(Islands(mf, -1, 0.5f));
                if (n.Contains("Exits")) info.ExitSigns.AddRange(Islands(mf, SubmeshOf(r, "ExitGreen"), 0.6f));
                if (n.Contains("Furniture"))
                {
                    info.Cakes.AddRange(Islands(mf, SubmeshOf(r, "Cake"), 0.6f));
                    info.Bottles.AddRange(Islands(mf, SubmeshOf(r, "Bottle"), 0.2f));
                }
            }
            chunks[Vector3Int.zero] = chunk;
            Static = info;
            Physics.SyncTransforms();
            ChunkReady?.Invoke(Vector3Int.zero);
        }

        static int SubmeshOf(MeshRenderer r, string part)
        {
            var m = r.sharedMaterials;
            for (int i = 0; i < m.Length; i++) if (m[i] != null && m[i].name.Contains(part)) return i;
            return -2;
        }

        /// The world-space bounds of each separate piece of one submesh (window panes).
        static List<Bounds> IslandBounds(MeshFilter mf, int submesh)
        {
            var result = new List<Bounds>();
            var mesh = mf.sharedMesh;
            if (submesh == -2 || mesh == null || !mesh.isReadable) return result;
            var v = mesh.vertices;
            var tris = mesh.GetTriangles(submesh);
            var m = mf.transform.localToWorldMatrix;
            // Panes are simple: group triangles by touching bounds.
            var boxes = new List<Bounds>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                var b = new Bounds(m.MultiplyPoint3x4(v[tris[t]]), Vector3.zero);
                b.Encapsulate(m.MultiplyPoint3x4(v[tris[t + 1]]));
                b.Encapsulate(m.MultiplyPoint3x4(v[tris[t + 2]]));
                b.Expand(0.02f);
                bool merged = false;
                for (int k = 0; k < boxes.Count; k++) if (boxes[k].Intersects(b)) { var bk = boxes[k]; bk.Encapsulate(b); boxes[k] = bk; merged = true; break; }
                if (!merged) boxes.Add(b);
            }
            result.AddRange(boxes);
            return result;
        }

        /// The centres (world space) of the separate pieces of a mesh: one per light panel, sign, cake or bottle.
        /// `submesh` -1 means all of it; pieces closer than `merge` are counted as one (a cake's tiers).
        static List<Vector3> Islands(MeshFilter mf, int submesh, float merge)
        {
            var result = new List<Vector3>();
            var mesh = mf.sharedMesh;
            if (submesh == -2 || mesh == null || !mesh.isReadable) return result;
            var v = mesh.vertices;
            int[] tris = submesh < 0 ? mesh.triangles : mesh.GetTriangles(submesh);
            // Vertices at the same spot belong together even when the mesh splits them (hard edges, UV seams).
            var canon = new Dictionary<Vector3Int, int>();
            var parent = new Dictionary<int, int>();
            int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            int Canon(int i)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
                if (!canon.TryGetValue(k, out int c)) { canon[k] = c = i; parent[c] = c; }
                return c;
            }
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = Root(Canon(tris[t])), b = Root(Canon(tris[t + 1])), c = Root(Canon(tris[t + 2]));
                parent[b] = a;
                parent[Root(c)] = a;
            }
            var sums = new Dictionary<int, (Vector3 min, Vector3 max)>();
            foreach (int i in tris)
            {
                int g = Root(Canon(i));
                var p = v[i];
                sums[g] = sums.TryGetValue(g, out var bb) ? (Vector3.Min(bb.min, p), Vector3.Max(bb.max, p)) : (p, p);
            }
            var m = mf.transform.localToWorldMatrix;
            foreach (var bb in sums.Values)
            {
                var c = m.MultiplyPoint3x4((bb.min + bb.max) / 2f);
                bool near = false;
                for (int k = 0; k < result.Count; k++) if (Vector3.Distance(result[k], c) < merge) { near = true; break; }
                if (!near) result.Add(c);
            }
            return result;
        }

        public static Vector2Int ChunkOf(Vector3 p) => new Vector2Int(
            Mathf.FloorToInt(p.x / MazeLayout.ChunkSize), Mathf.FloorToInt(p.z / MazeLayout.ChunkSize));

        /// One chunk of any level, in the surface-based format. Shared with the self-checks.
        /// `floor` only matters for levels with stacked floors (Level 0); the layout comes back at its real height.
        public static ChunkLayout GenerateChunk(LevelTheme theme, uint seed, int cx, int cz, int floor = 0)
        {
            ChunkLayout layout;
            if (theme.generator == LevelGenerator.Districts)
            {
                layout = DistrictLayout.Generate(seed, floor, cx, cz, theme);
                layout.Shift(floor * DistrictLayout.FloorHeight);
            }
            else if (theme.generator == LevelGenerator.Stairwell)
                layout = StairwellLayout.Generate(seed, cx, cz, floor, theme.style.ceiling);
            else if (theme.generator == LevelGenerator.Ocean)
                layout = OceanLayout.Generate(seed, cx, cz, floor, theme);
            else if (theme.generator == LevelGenerator.Caves)
                layout = CaveLayout.Generate(seed, cx, cz, floor, theme);
            else if (theme.generator == LevelGenerator.Suburbs)
                layout = SuburbsLayout.Generate(seed, cx, cz, floor, theme);
            else if (theme.generator == LevelGenerator.Static)
                return new ChunkLayout();
            else
            {
                layout = MazeLayout.Generate(seed, cx, cz, theme.style);
                FromMaze(layout, theme, cx, cz);
            }
            if (theme.waterHeight > 0f && !theme.zoneWater)
            {
                const float size = MazeLayout.ChunkSize;
                (theme.generator == LevelGenerator.Ocean ? layout.WaterDeep : layout.Water).Add(new SurfaceQuad { Area = new Rect(cx * size, cz * size, size, size), Height = theme.waterHeight, FacingUp = true });   // the open sea gets the dark water
            }
            return layout;
        }

        /// The maze generator's walls/slabs/fixtures, expressed as surfaces.
        static void FromMaze(ChunkLayout L, LevelTheme theme, int cx, int cz)
        {
            const float size = MazeLayout.ChunkSize;
            const BoxMeshBuilder.Faces faces = BoxMeshBuilder.Faces.Sides | BoxMeshBuilder.Faces.NegY | BoxMeshBuilder.Faces.PosY;
            var wall = new SurfaceRef(L.Red ? Surface.RedWall : Surface.Wall);
            foreach (var w in L.Walls) L.Boxes.Add(SurfaceBox.Uniform(w, wall, faces));
            foreach (var w in L.AccentWalls) L.Boxes.Add(SurfaceBox.Uniform(w, new SurfaceRef(Surface.Manila), faces));
            var area = new Rect(cx * size, cz * size, size, size);
            L.Quads.Add(new SurfaceQuad { Area = area, Height = 0f, FacingUp = true, Surface = new SurfaceRef(L.Red ? Surface.RedFloor : Surface.Floor) });
            L.Quads.Add(new SurfaceQuad { Area = area, Height = theme.style.ceiling, FacingUp = false, Surface = new SurfaceRef(Surface.Ceiling) });
            foreach (var f in L.Fixtures)
                L.Panels.Add(new PanelSpec
                {
                    Position = f.Position + Vector3.down * 0.01f,
                    State = f.State,
                    Emitter = true,
                    Range = theme.lightRange,
                    Id = (int)WorldHash.Hash(0x1234, Mathf.RoundToInt(f.Position.x * 2f), Mathf.RoundToInt(f.Position.z * 2f), 98),
                    Red = L.Red,
                });
        }

        void Recenter(Vector2Int c, int floor)
        {
            center = c;
            // The player's floor all around; the floors above and below only where a stairwell or drop joins them.
            var want = new HashSet<Vector3Int>();
            for (int dx = -viewRadius; dx <= viewRadius; dx++)
            for (int dz = -viewRadius; dz <= viewRadius; dz++)
                want.Add(new Vector3Int(c.x + dx, floor, c.y + dz));
            if (DistrictLayout.HasFloors(theme))
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    foreach (int other in new[] { floor - 2, floor - 1, floor + 1, floor + 2 })   // two floors only show down the Cut
                    {
                        if (other < DistrictLayout.BottomFloor - 1 || other > DistrictLayout.TopFloor) continue;
                        if (DistrictLayout.ChunkJoins(seed, floor, other, c.x + dx, c.y + dz, theme))
                            for (int nx = -1; nx <= 1; nx++)   // and its neighbours, so you never look down into black
                            for (int nz = -1; nz <= 1; nz++)
                                want.Add(new Vector3Int(c.x + dx + nx, other, c.y + dz + nz));
                    }
            else if (theme.generator == LevelGenerator.Stairwell && c.x == 0 && c.y == 0)
                // The staircase continues vertically in its fixed shaft at chunk 0,0.
                for (int offset = -3; offset <= 3; offset++)
                {
                    if (offset == 0) continue;
                    want.Add(new Vector3Int(0, floor + offset, 0));
                }

            foreach (var k in extraWant) want.Add(k);   // around friends (co-op owner)

            // One chunk of slack on the player's floor, so pacing back and forth over a border doesn't rebuild anything.
            var drop = new List<Vector3Int>();
            foreach (var key in chunks.Keys)
            {
                bool near = key.y == floor && Mathf.Max(Mathf.Abs(key.x - c.x), Mathf.Abs(key.z - c.y)) <= viewRadius + 1;
                if (!near && !want.Contains(key)) drop.Add(key);
            }
            foreach (var key in drop)
            {
                Unload(chunks[key]);
                chunks.Remove(key);
            }
            // The Peripheral Shift: a Level 0 district you've fully left may rearrange before you come back.
            foreach (var key in drop)
            {
                int dx = Mathf.FloorToInt(key.x / 2f), dz = Mathf.FloorToInt(key.z / 2f);
                bool stillLoaded = false;
                foreach (var k in chunks.Keys) if (k.y == key.y && Mathf.FloorToInt(k.x / 2f) == dx && Mathf.FloorToInt(k.z / 2f) == dz) { stillLoaded = true; break; }
                if (!stillLoaded && !Net.Coop.Active && Random.value < 0.4f) DistrictLayout.Shift(seed, key.y, dx, dz, theme);   // never in co-op: everyone's copy must match
            }

            toBuild.Clear();
            foreach (var key in want) if (!chunks.ContainsKey(key)) toBuild.Add(key);
            var stale = new List<Vector3Int>();
            foreach (var key in pending.Keys) if (!want.Contains(key)) stale.Add(key);
            foreach (var key in stale) pending.Remove(key);   // still finishes in the background; the result is dropped
            int Dist(Vector3Int k) => (k.x - c.x) * (k.x - c.x) + (k.z - c.y) * (k.z - c.y) + (k.y == floor ? 0 : 3);
            toBuild.Sort((a, b) => Dist(a).CompareTo(Dist(b)));
        }

        /// Synchronous (level loads, behind the transition).
        void BuildNext()
        {
            var key = toBuild[0];
            toBuild.RemoveAt(0);
            chunks[key] = Build(key, pending.TryGetValue(key, out var t) && t.IsCompletedSuccessfully ? t.Result : GenerateChunk(theme, seed, key.x, key.z, key.y));
            pending.Remove(key);
            ChunkReady?.Invoke(key);
        }

        /// While walking: generate the nearest wanted chunks on worker threads, and build at most `chunksPerFrame`
        /// finished ones per frame, nearest first.
        void StreamChunks()
        {
            var level = theme;
            uint s = seed;
            foreach (var key in toBuild)
            {
                if (pending.Count >= MaxPending) break;
                if (pending.ContainsKey(key)) continue;
                var k = key;
                pending[k] = System.Threading.Tasks.Task.Run(() => GenerateChunk(level, s, k.x, k.z, k.y));
            }
            int built = 0;
            for (int i = 0; i < toBuild.Count && built < chunksPerFrame; i++)
            {
                var key = toBuild[i];
                if (!pending.TryGetValue(key, out var task) || !task.IsCompleted) continue;
                pending.Remove(key);
                ChunkLayout layout;
                if (task.IsCompletedSuccessfully) layout = task.Result;
                else
                {
                    Debug.LogException(task.Exception);
                    layout = GenerateChunk(theme, seed, key.x, key.z, key.y);
                }
                toBuild.RemoveAt(i);
                chunks[key] = Build(key, layout);
                ChunkReady?.Invoke(key);
                built++;
                i--;
            }
        }

        static void Unload(Chunk chunk)
        {
            Destroy(chunk.Root);
            foreach (var mesh in chunk.Meshes) Destroy(mesh);
        }

        // shortcut: chunks are created and destroyed, not pooled; pool them if the profiler shows GC spikes.
        Chunk Build(Vector3Int key, ChunkLayout layout)
        {
            var origin = new Vector3(key.x * MazeLayout.ChunkSize, key.y * DistrictLayout.FloorHeight, key.z * MazeLayout.ChunkSize);
            var chunk = new Chunk { Floor = key.y, Root = new GameObject($"Chunk {key.x},{key.z} floor {key.y}") };
            chunk.Root.transform.SetParent(transform, false);
            chunk.Root.transform.position = origin;
            // Both shallow water and deep basins share the level's single waterline. Both must be in the
            // physics list or TryWaterSurfaceAt can't find the deep pools and swimming silently stops there.
            chunk.Water.AddRange(layout.Water);
            chunk.Water.AddRange(layout.WaterDeep);

            // Structure: walls, trims, floors and ceilings, one submesh per material actually used.
            var structure = new BoxMeshBuilder(origin, 0);
            var oriented = new BoxMeshBuilder(origin, 0);
            var materials = new List<Material>();
            var slots = new Dictionary<SurfaceRef, int>();
            int Sub(SurfaceRef s)
            {
                if (slots.TryGetValue(s, out int i)) return i;
                slots[s] = i = materials.Count;
                materials.Add(catalog.Resolve(theme, s));
                return i;
            }
            var solids = layout.Boxes.FindAll(x => x.Solid);   // for the bevel neighbour check
            foreach (var b in layout.Boxes)
            {
                if (KitDressing.Baseboard(oriented, b, Sub)) continue;   // flat strip -> profiled skirting (Stage C kit)
                var f = b.Faces;
                if (KitDressing.Bevels(b)) structure.AddBevelledBox(b.Box, f, KitDressing.Bevel, KitDressing.FreeCorners(b, solids),
                    (f & BoxMeshBuilder.Faces.PosX) != 0 ? Sub(b.PosX) : 0, (f & BoxMeshBuilder.Faces.NegX) != 0 ? Sub(b.NegX) : 0,
                    (f & BoxMeshBuilder.Faces.PosY) != 0 ? Sub(b.PosY) : 0, (f & BoxMeshBuilder.Faces.NegY) != 0 ? Sub(b.NegY) : 0,
                    (f & BoxMeshBuilder.Faces.PosZ) != 0 ? Sub(b.PosZ) : 0, (f & BoxMeshBuilder.Faces.NegZ) != 0 ? Sub(b.NegZ) : 0);
                else structure.AddBox(b.Box, f,
                    (f & BoxMeshBuilder.Faces.PosX) != 0 ? Sub(b.PosX) : 0, (f & BoxMeshBuilder.Faces.NegX) != 0 ? Sub(b.NegX) : 0,
                    (f & BoxMeshBuilder.Faces.PosY) != 0 ? Sub(b.PosY) : 0, (f & BoxMeshBuilder.Faces.NegY) != 0 ? Sub(b.NegY) : 0,
                    (f & BoxMeshBuilder.Faces.PosZ) != 0 ? Sub(b.PosZ) : 0, (f & BoxMeshBuilder.Faces.NegZ) != 0 ? Sub(b.NegZ) : 0);
            }
            foreach (var b in layout.OrientedBoxes)
                oriented.AddOrientedBox(Matrix4x4.TRS(b.Center, Quaternion.Euler(0f, b.Yaw, 0f), Vector3.one), Vector3.zero, b.Size, Sub(b.Surface));
            var tops = new BoxMeshBuilder(origin, 0);   // shares the structure's material slots
            foreach (var q in layout.Quads)
                if (q.Visual) tops.AddHorizontal(q.Area, q.Height, q.FacingUp, q.Rotated, Sub(q.Surface));
                else structure.AddHorizontal(q.Area, q.Height, q.FacingUp, q.Rotated, Sub(q.Surface));
            foreach (var r in layout.Slopes) structure.AddSlope(r.A, r.B, r.Width, r.Bottom, r.Thickness, Sub(r.Top), Sub(r.Side));
            // Tile ceilings: T-bar rails (ceiling material, into the non-colliding surfaces) and fixtures (prop paints).
            var ceilingKit = new BoxMeshBuilder(origin, paints.Length);
            KitDressing.Ceilings(layout, oriented, ceilingKit, Sub, s => catalog.Resolve(theme, s), WorldHash.Hash(seed, key.x, key.z, key.y));
            if (ceilingKit.VertexCount > 0) AddMesh(chunk, "Ceiling Fixtures", ceilingKit.ToMesh("Ceiling Fixtures"), paints, false, false);
            var grime = GrimeDressing.Material != null ? GrimeDressing.Build(layout, origin, s => catalog.Resolve(theme, s), WorldHash.Hash(seed, key.x, key.z, key.y)) : null;
            if (grime != null) AddMesh(chunk, "Grime", grime, new[] { GrimeDressing.Material }, false, false);
            GoreDressing.Decorate(chunk.Root.transform, layout, origin, theme, seed, key);
            // One material per submesh each (a mesh with more materials than submeshes draws its last part again).
            if (structure.VertexCount > 0) AddMesh(chunk, "Structure", structure.ToMesh("Structure"), materials.GetRange(0, structure.SubmeshCount).ToArray(), true);
            if (oriented.VertexCount > 0) AddMesh(chunk, "Oriented Surfaces", oriented.ToMesh("Oriented Surfaces"), materials.GetRange(0, oriented.SubmeshCount).ToArray(), false);
            foreach (var b in layout.OrientedBoxes)
            {
                var tread = new GameObject("Stair tread collider");
                tread.transform.SetParent(chunk.Root.transform, false);
                tread.transform.localPosition = b.Center - origin;
                tread.transform.localRotation = Quaternion.Euler(0f, b.Yaw, 0f);
                tread.AddComponent<BoxCollider>().size = b.Size;
            }
            if (tops.VertexCount > 0) AddMesh(chunk, "Ceiling Tops", tops.ToMesh("Ceiling Tops"), materials.GetRange(0, tops.SubmeshCount).ToArray(), false, false);

            foreach (var r in layout.Rocks)
                if (r.T.Count > 0) AddMesh(chunk, "Rock", r.ToMesh("Rock", origin), new[] { catalog.Resolve(theme, r.Surface) }, r.Collide);

            // Props, curiosities and exit doors: solid ones collide, decor doesn't.
            var solid = new BoxMeshBuilder(origin, paints.Length);
            var decor = new BoxMeshBuilder(origin, paints.Length);
            float ceiling = theme.style.ceiling;
            foreach (var prop in layout.Props)
            {
                if (prop.Kind == PropKind.AlmondWater || prop.Kind == PropKind.Battery) { CreatePickup(chunk, prop, origin); continue; }
                if (prop.Kind == PropKind.ControlConsole) { CreateSwitch(chunk, prop); continue; }
                if (prop.Kind == PropKind.WallLadder) CreateLadder(chunk, prop);
                if (prop.Kind == PropKind.Crate && theme.id == "1") { CreateCrate(chunk.Root.transform, chunk.Meshes, prop.Pose, Pickup.IdAt(seed ^ 0xC4A7Eu, prop.Pose.GetColumn(3))); continue; }
                PropLibrary.Build(prop, PropLibrary.IsSolid(prop.Kind) ? solid : decor, ceiling);
                if (prop.Kind == PropKind.OfficeChair || prop.Kind == PropKind.WoodChair || prop.Kind == PropKind.Sofa || prop.Kind == PropKind.Armchair || prop.Kind == PropKind.Bench)
                    CreateSeats(chunk.Root.transform, prop);
            }
            if (Curiosities != null)
            {
                var extra = new List<PropSpec>();
                foreach (var slot in layout.Curiosities)
                {
                    var kind = Curiosities.Resolve(slot);
                    if (kind == CuriosityKind.None) continue;
                    extra.Clear();
                    CuriosityLibrary.Build(kind, slot, extra, out bool ringing);
                    foreach (var prop in extra)
                    {
                        if (prop.Kind == PropKind.AlmondWater || prop.Kind == PropKind.Battery) { CreatePickup(chunk, prop, origin); continue; }
                        PropLibrary.Build(prop, PropLibrary.IsSolid(prop.Kind) ? solid : decor, ceiling);
                    }
                    if (ringing) AddRinging(chunk.Root.transform, slot.Pose.GetColumn(3));
                }
            }
            foreach (var exit in layout.Exits)
            {
                if (Vanishing(exit)) continue;   // built on its own in CreateExit, so it can disappear
                if (exit.Kind == ExitKind.Door && theme.districts.markedExits) PropLibrary.BuildMarkedExit(exit.Target, exit.Pose, decor);
                else if (exit.Kind != ExitKind.FlickerWall) PropLibrary.BuildExit(exit.Kind, exit.Pose, decor);
                if (exit.Target == "fun") PropLibrary.Balloon(exit.Pose, new Vector3(0.36f, 1.0f, 0.06f), new Vector3(0.3f, 1.95f, 0.3f), PropPaint.Red, decor);   // a balloon tied to the handle
            }
            if (solid.VertexCount > 0) AddMesh(chunk, "Props", solid.ToMesh("Props"), paints, true);
            if (decor.VertexCount > 0) AddMesh(chunk, "Decor", decor.ToMesh("Decor"), paints, false);

            if (layout.Damp.Count > 0)
            {
                if (dampMat == null) dampMat = HdMat.Transparent(HdMat.Make(new Color(0.10f, 0.075f, 0.035f, 0.42f), 0.93f));
                var wet = new BoxMeshBuilder(origin, 1);
                foreach (var q in layout.Damp) wet.AddHorizontal(q.Area, q.Height, true, false, 0);
                AddMesh(chunk, "Damp", wet.ToMesh("Damp"), new[] { dampMat }, false, false);
            }
            if (layout.WaterBed.Count > 0 && catalog.waterCaustics != null)
            {
                var bed = new BoxMeshBuilder(origin, 1);
                foreach (var q in layout.WaterBed) bed.AddHorizontal(q.Area, q.Height, true, false, 0);
                AddMesh(chunk, "Waterbed", bed.ToMesh("Waterbed"), new[] { catalog.waterCaustics }, false, false);
            }
            if (layout.WaterBedDeep.Count > 0 && catalog.waterCausticsDeep != null)
            {
                var bed = new BoxMeshBuilder(origin, 1);
                foreach (var q in layout.WaterBedDeep) bed.AddHorizontal(q.Area, q.Height, true, false, 0);
                AddMesh(chunk, "Waterbed Deep", bed.ToMesh("Waterbed Deep"), new[] { catalog.waterCausticsDeep }, false, false);
            }
            if (layout.Water.Count > 0)
            {
                var water = new BoxMeshBuilder(origin, 1);
                foreach (var q in layout.Water) water.AddHorizontal(q.Area, q.Height, true, false, 0);
                AddMesh(chunk, "Water", water.ToMesh("Water"), new[] { catalog.water }, false, false);
            }
            if (theme.generator == LevelGenerator.Ocean)
                foreach (var q in layout.WaterDeep) RollingSea.Create(chunk.Root.transform, q, catalog.waterDeep);   // swells, not a flat sheet
            else if (layout.WaterDeep.Count > 0)
            {
                var water = new BoxMeshBuilder(origin, 1);
                foreach (var q in layout.WaterDeep) water.AddHorizontal(q.Area, q.Height, true, false, 0);
                var mat = catalog.waterDeep != null ? catalog.waterDeep : catalog.water;
                AddMesh(chunk, "Water Deep", water.ToMesh("Water Deep"), new[] { mat }, false, false);
            }

            BuildPanels(chunk, layout.Panels, origin);
            foreach (var exit in layout.Exits) CreateExit(chunk, exit);
            foreach (var pit in layout.Pits) CreateTrigger(chunk.Root.transform, "Pit", pit, null);
            foreach (var r in layout.Readables) Readable.Create(chunk.Root.transform, r, seed, IsPicked);
            foreach (var b in layout.Boards) CreateBoards(chunk, b);
            if (layout.Planks.Count > 0) CreatePlanks(chunk, layout.Planks);
            foreach (var s in layout.Specials) Level3Power.Create(chunk.Root.transform, s, paints);
            return chunk;
        }

        static void CreateSeats(Transform parent, PropSpec prop)
        {
            bool sofa = prop.Kind == PropKind.Sofa;
            bool bench = prop.Kind == PropKind.Bench;
            int count = sofa || bench ? 2 : 1;
            float spread = sofa ? 0.52f : 0.36f;
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? 0f : (i == 0 ? -spread : spread);
                var go = new GameObject("Seat");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(prop.Pose.MultiplyPoint3x4(new Vector3(x, 0f, 0f)), prop.Pose.rotation);
                var trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 0.62f, 0.28f);
                trigger.size = new Vector3(0.72f, 1.15f, 0.52f);
                var seat = go.AddComponent<Seat>();
                seat.EyeHeight = sofa || prop.Kind == PropKind.Armchair ? 0.78f : 0.72f;
            }
        }

        static Mesh trofferMesh;

        /// The troffer model with two submeshes: 0 the lens (lit or dead material), 1 the frame. Built once, shared.
        static Mesh TrofferMesh()
        {
            if (trofferMesh != null) return trofferMesh;
            var b = new BoxMeshBuilder(Vector3.zero, 2);
            ModelLibrary.AddMapped(b, "Troffer", Matrix4x4.identity, n => n == "PanelGlow" ? 0 : 1);
            return trofferMesh = b.ToMesh("Troffer");
        }

        /// All panels of a chunk as one mesh (lit and dead submeshes); failing ones get their own small object.
        void BuildPanels(Chunk chunk, List<PanelSpec> panels, Vector3 origin)
        {
            var mesh = new BoxMeshBuilder(origin, 3);   // 0 lit lens, 1 dead lens, 2 frame
            var glow = new BoxMeshBuilder(origin, 1);
            bool troffers = ModelLibrary.Has("Troffer");
            Matrix4x4 TrofferPose(PanelSpec p) => Matrix4x4.TRS(p.Position + Vector3.up * 0.01f, p.AlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            foreach (var p in panels)
            {
                if (p.Bare)
                {
                    if (p.Emitter && (theme.generator != LevelGenerator.Stairwell || p.State != FixtureState.Dead))
                        chunk.Emitters.Add(new Emitter { Position = p.Position, Range = p.Range, Id = p.Id, Tint = p.Tint });
                    continue;
                }
                if (p.Floor)   // light coming up through a crack between floorboards
                {
                    glow.AddHorizontal(new Rect(p.Position.x - 0.8f, p.Position.z - 0.04f, 1.6f, 0.08f), p.Position.y, true, false, 0);
                    chunk.Emitters.Add(new Emitter { Position = p.Position, Range = p.Range, Id = p.Id, Floor = true });
                    continue;
                }
                var size = p.AlongX ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 1f);
                var area = new Rect(p.Position.x - size.x / 2, p.Position.z - size.y / 2, size.x, size.y);
                if (troffers)
                {
                    // One shared troffer mesh per light. (Copying the 5,000-vertex model into the chunk mesh for every
                    // panel made each chunk take ~250 ms to build: the lag spike when a chunk loaded.)
                    var go = new GameObject(p.State == FixtureState.Faulty ? "Faulty Panel" : "Troffer");
                    go.transform.SetParent(chunk.Root.transform, false);
                    var pose = TrofferPose(p);
                    go.transform.SetPositionAndRotation(pose.GetColumn(3), pose.rotation);
                    go.AddComponent<MeshFilter>().sharedMesh = TrofferMesh();
                    var r = go.AddComponent<MeshRenderer>();
                    var trim = catalog.trims != null && catalog.trims.Length > 0 ? catalog.trims[0] : catalog.panelDead;
                    r.sharedMaterials = new[] { p.State == FixtureState.Dead ? catalog.panelDead : panelLit, trim };
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    if (p.State == FixtureState.Faulty)
                    {
                        var f = go.AddComponent<FaultyPanel>();
                        f.Id = p.Id;
                        f.Lit = panelLit;
                        f.Dead = catalog.panelDead;
                    }
                }
                else if (p.State == FixtureState.Faulty)
                {
                    var single = new BoxMeshBuilder(p.Position, 1);
                    single.AddHorizontal(area, p.Position.y, false, false, 0);
                    var m = single.ToMesh("Faulty Panel");
                    chunk.Meshes.Add(m);
                    var go = new GameObject("Faulty Panel");
                    go.transform.SetParent(chunk.Root.transform, false);
                    go.transform.position = p.Position;
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = panelLit;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    var f = go.AddComponent<FaultyPanel>();
                    f.Id = p.Id;
                    f.Lit = panelLit;
                    f.Dead = catalog.panelDead;
                }
                else mesh.AddHorizontal(area, p.Position.y, false, false, p.State == FixtureState.Dead ? 1 : 0);
                if (p.State != FixtureState.Dead && p.Emitter)
                    chunk.Emitters.Add(new Emitter { Position = p.Position, Range = p.Range, Id = p.Id, Faulty = p.State == FixtureState.Faulty, Red = p.Red, Sick = p.Sick, AlongX = p.AlongX });
            }
            if (mesh.VertexCount > 0) AddMesh(chunk, "Panels", mesh.ToMesh("Panels"), new[] { panelLit, catalog.panelDead, catalog.trims != null && catalog.trims.Length > 0 ? catalog.trims[0] : catalog.panelDead }, false, false);
            if (glow.VertexCount > 0)
            {
                if (glowLit == null)
                {
                    glowLit = new Material(panelLit);
                    HdMat.Emissive(new Color(1f, 0.45f, 0.12f), 900f, null, glowLit);
                }
                AddMesh(chunk, "Floor Glow", glow.ToMesh("Floor Glow"), new[] { glowLit }, false, false);
            }
        }

        /// A boarded-up doorway: its own object, so pryable boards can be pulled off. Pried ones stay gone.
        void CreateBoards(Chunk chunk, BoardSpec b)
        {
            bool forced = b.LockedDoor && IsPicked != null && IsPicked(b.Id);
            if (b.LockedDoor) { CreateLockedDoor(chunk, b, forced); return; }
            if (b.Pryable && IsPicked != null && IsPicked(b.Id)) return;
            Vector3 at = b.Pose.GetColumn(3);
            var go = new GameObject(b.Pryable ? "Boarded Doorway" : "Nailed-Shut Doorway");
            go.transform.SetParent(chunk.Root.transform, false);
            go.transform.SetPositionAndRotation(at, b.Pose.rotation);
            var mesh = new BoxMeshBuilder(at, paints.Length);
            BoardedDoor.BuildPlanks(mesh, b.Pose, b.Width, b.Seed);
            var m = mesh.ToMesh("Boards");
            chunk.Meshes.Add(m);
            var holder = new GameObject("Planks");
            holder.transform.SetParent(go.transform, false);
            holder.transform.SetPositionAndRotation(at, Quaternion.identity);
            holder.AddComponent<MeshFilter>().sharedMesh = m;
            holder.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, 0.07f);
            box.size = new Vector3(b.Width + 0.2f, 2.1f, 0.25f);
            var door = go.AddComponent<BoardedDoor>();
            door.Pryable = b.Pryable;
            door.Id = b.Id;
        }

        /// A wooden crate you can pry the lid off (hold Interact). Opened ones stay open, and empty.
        public GameObject CreateCrate(Transform parent, List<Mesh> meshes, Matrix4x4 pose, int id)
        {
            bool open = IsPicked != null && IsPicked(id);
            Vector3 at = pose.GetColumn(3);
            var go = new GameObject("Crate");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, pose.rotation);
            var body = new BoxMeshBuilder(Vector3.zero, paints.Length);
            body.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), (int)PropPaint.Wood);
            for (int i = 0; i < 3; i++)   // planks and corner battens
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    body.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, 0.13f + i * 0.27f, s * 0.405f), new Vector3(0.82f, 0.02f, 0.012f), (int)PropPaint.Cardboard);
                    body.AddOrientedBox(Matrix4x4.identity, new Vector3(s * 0.405f, 0.13f + i * 0.27f, 0f), new Vector3(0.012f, 0.02f, 0.82f), (int)PropPaint.Cardboard);
                }
            }
            if (open) body.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, 0.79f, 0f), new Vector3(0.72f, 0.01f, 0.72f), (int)PropPaint.Dark);   // the dark inside
            var bm = body.ToMesh("Crate");
            meshes?.Add(bm);
            go.AddComponent<MeshFilter>().sharedMesh = bm;
            go.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var lidT = new GameObject("Lid").transform;
            lidT.SetParent(go.transform, false);
            lidT.localPosition = new Vector3(0f, 0.81f, 0f);
            var lid = new BoxMeshBuilder(Vector3.zero, paints.Length);
            lid.AddOrientedBox(Matrix4x4.identity, Vector3.zero, new Vector3(0.84f, 0.04f, 0.84f), (int)PropPaint.Wood);
            foreach (float s in new[] { -0.3f, 0.3f }) lid.AddOrientedBox(Matrix4x4.identity, new Vector3(s, 0.022f, 0f), new Vector3(0.06f, 0.01f, 0.84f), (int)PropPaint.Cardboard);
            var lm = lid.ToMesh("Crate Lid");
            meshes?.Add(lm);
            lidT.gameObject.AddComponent<MeshFilter>().sharedMesh = lm;
            lidT.gameObject.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.42f, 0f);
            box.size = new Vector3(0.84f, 0.84f, 0.84f);
            var crate = go.AddComponent<BoardedDoor>();
            crate.Crate = true;
            crate.Pryable = !open;
            crate.Id = id;
            crate.Slab = lidT;
            if (open) crate.ForceOpen();
            return go;
        }

        /// The climbable part of a wall ladder: a trigger in front of the rungs, as tall as the ladder.
        static void CreateLadder(Chunk chunk, PropSpec prop)
        {
            var go = new GameObject("Ladder");
            go.transform.SetParent(chunk.Root.transform, false);
            go.transform.SetPositionAndRotation(prop.Pose.GetColumn(3), prop.Pose.rotation);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, prop.Ceiling / 2f, 0.4f);
            box.size = new Vector3(0.9f, prop.Ceiling, 0.8f);
            var ladder = go.AddComponent<Ladder>();
            ladder.Top = prop.Pose.GetColumn(3).y + prop.Ceiling - 1.1f;   // the floor you climb out onto
        }

        /// A "locked" door you can force: frame on the wall, a leaf that swings on its hinge, and (if it leads
        /// somewhere) darkness behind it instead of brick.
        void CreateLockedDoor(Chunk chunk, BoardSpec b, bool forced)
        {
            Vector3 at = b.Pose.GetColumn(3);
            var go = new GameObject("Stuck Door");
            go.transform.SetParent(chunk.Root.transform, false);
            go.transform.SetPositionAndRotation(at, b.Pose.rotation);
            var frame = new BoxMeshBuilder(at, paints.Length);
            PropLibrary.DoorFrameOnly(b.Pose, frame, !string.IsNullOrEmpty(b.Exit));
            var fm = frame.ToMesh("Door Frame");
            chunk.Meshes.Add(fm);
            var fgo = new GameObject("Frame");
            fgo.transform.SetParent(go.transform, false);
            fgo.transform.SetPositionAndRotation(at, Quaternion.identity);
            fgo.AddComponent<MeshFilter>().sharedMesh = fm;
            fgo.AddComponent<MeshRenderer>().sharedMaterials = paints;
            // The leaf, hinged on its left edge.
            var hinge = new GameObject("Leaf").transform;
            hinge.SetParent(go.transform, false);
            hinge.localPosition = new Vector3(-0.45f, 0f, 0.03f);
            var leaf = new BoxMeshBuilder(Vector3.zero, paints.Length);
            leaf.AddOrientedBox(Matrix4x4.identity, new Vector3(0.45f, 1.02f, 0f), new Vector3(0.9f, 2.04f, 0.04f), (int)PropPaint.Metal);
            leaf.AddOrientedBox(Matrix4x4.identity, new Vector3(0.81f, 1.0f, 0.035f), new Vector3(0.05f, 0.05f, 0.05f), (int)PropPaint.Dark);
            var lm = leaf.ToMesh("Door Leaf");
            chunk.Meshes.Add(lm);
            hinge.gameObject.AddComponent<MeshFilter>().sharedMesh = lm;
            hinge.gameObject.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, 0.07f);
            box.size = new Vector3(1.1f, 2.1f, 0.25f);
            var door = go.AddComponent<BoardedDoor>();
            door.Pryable = true;
            door.LockedDoor = true;
            door.Exit = b.Exit;
            door.Id = b.Id;
            door.Slab = hinge;
            if (forced) door.ForceOpen();
        }

        /// Planks across pitfalls: one mesh, a box collider per plank so you can walk across.
        void CreatePlanks(Chunk chunk, List<PlankSpec> planks)
        {
            Vector3 at = planks[0].Pose.GetColumn(3);
            var go = new GameObject("Bridge Planks");
            go.transform.SetParent(chunk.Root.transform, false);
            go.transform.position = at;
            var mesh = new BoxMeshBuilder(at, paints.Length);
            foreach (var p in planks)
            {
                var scale = Matrix4x4.TRS(Vector3.down * 0.0125f, Quaternion.identity, new Vector3(p.Length, 1f, 1f));
                if (!ModelLibrary.Add(mesh, "Plank_" + p.Variant % 3, p.Pose * scale * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 0f))))
                    mesh.AddOrientedBox(p.Pose, new Vector3(0f, -0.0125f, 0f), new Vector3(p.Length, 0.025f, 0.15f), (int)PropPaint.Wood);
                var col = new GameObject("Plank");
                col.transform.SetParent(go.transform, false);
                col.transform.SetPositionAndRotation(p.Pose.GetColumn(3), p.Pose.rotation);
                var box = col.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, -0.03f, 0f);
                box.size = new Vector3(p.Length, 0.06f, 0.16f);
            }
            var m = mesh.ToMesh("Bridge Planks");
            chunk.Meshes.Add(m);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterials = paints;
        }

        /// The pump console in Level -1's control room: its own object, so you can use it.
        void CreateSwitch(Chunk chunk, PropSpec prop)
        {
            Vector3 at = prop.Pose.GetColumn(3);
            var go = new GameObject("Pump Console");
            go.transform.SetParent(chunk.Root.transform, false);
            go.transform.SetPositionAndRotation(at, prop.Pose.rotation);
            var mesh = new BoxMeshBuilder(at, paints.Length);
            PropLibrary.Build(prop, mesh, theme.style.ceiling);
            var m = mesh.ToMesh("Pump Console");
            chunk.Meshes.Add(m);
            var holder = new GameObject("Mesh");
            holder.transform.SetParent(go.transform, false);
            holder.transform.SetPositionAndRotation(at, Quaternion.identity);
            holder.AddComponent<MeshFilter>().sharedMesh = m;
            holder.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.8f, 0f);
            box.size = new Vector3(2.2f, 1.6f, 0.8f);
            go.AddComponent<PoolSwitch>();
        }

        /// Almond water and batteries are their own objects, so you can pick them up.
        void CreatePickup(Chunk chunk, PropSpec prop, Vector3 origin)
        {
            Vector3 at = prop.Pose.GetColumn(3);
            int id = Pickup.IdAt(seed, at);
            if (WorldHash.Value((uint)id, 57) > Meta.Difficulty.Supplies) return;   // harder saves: some of it was never there
            if (IsPicked != null && IsPicked(id)) return;
            if (prop.Kind == PropKind.Battery)   // some battery spots hold a throwable instead
            {
                float r = WorldHash.Value((uint)id, 91);
                if (r < 0.06f) prop.Kind = PropKind.Exterminator;
                else if (r < 0.36f) prop.Kind = PropKind.Glowstick;
                else if (r < 0.44f) prop.Kind = PropKind.Repellent;
                else if (r < 0.52f) prop.Kind = PropKind.Flare;
            }
            else if (prop.Kind == PropKind.AlmondWater && WorldHash.Value((uint)id, 93) < 0.07f) prop.Kind = PropKind.FirstAid;   // and some water spots a first-aid kit
            var go = new GameObject(prop.Kind.ToString());
            go.transform.SetParent(chunk.Root.transform, false);
            go.transform.position = at;
            var mesh = new BoxMeshBuilder(at, paints.Length);
            PropLibrary.Build(prop, mesh, theme.style.ceiling);
            var m = mesh.ToMesh(prop.Kind.ToString());
            chunk.Meshes.Add(m);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var hit = go.AddComponent<SphereCollider>();
            hit.isTrigger = true;
            hit.center = Vector3.up * 0.15f;
            hit.radius = 0.35f;
            var p = go.AddComponent<Pickup>();
            p.Kind = prop.Kind;
            p.Id = id;
        }

        void AddRinging(Transform parent, Vector3 at)
        {
            var go = new GameObject("Ringing Phone");
            go.transform.SetParent(parent, false);
            go.transform.position = at + Vector3.up * 0.1f;
            var source = go.AddComponent<AudioSource>();
            source.clip = ring;
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 30f;
            source.volume = 0.5f * Meta.AudioMix.Effects;
            source.Play();
        }

        static void AddMesh(Chunk chunk, string name, Mesh mesh, Material[] materials, bool collide, bool shadows = true)
        {
            chunk.Meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(chunk.Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;   // walls block the pooled shadow lights
            if (collide && mesh.vertexCount > 0) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void CreateExit(Chunk chunk, ExitSpec exit)
        {
            var go = CreateTrigger(chunk.Root.transform, $"Exit to {exit.Target}", exit.Trigger, exit.Target);
            if (theme != null && theme.id == "6" && exit.Target == "7")   // the way out of the dark: listen for the waves
            {
                var surf = go.AddComponent<AudioSource>();
                surf.clip = Audio.ProceduralAudio.Surf();
                surf.loop = true;
                surf.spatialBlend = 1f;
                surf.rolloffMode = AudioRolloffMode.Linear;
                surf.minDistance = 2f;
                surf.maxDistance = 48f;
                surf.volume = 0.55f * Meta.AudioMix.Effects;
                surf.time = Random.value * surf.clip.length;
                surf.Play();
            }
            if (Vanishing(exit))
            {
                Vector3 at = exit.Pose.GetColumn(3);
                var b = new BoxMeshBuilder(at, paints.Length);
                if (exit.Kind == ExitKind.Door && theme.districts.markedExits) PropLibrary.BuildMarkedExit(exit.Target, exit.Pose, b);
                else PropLibrary.BuildExit(exit.Kind, exit.Pose, b);
                var doorMesh = b.ToMesh("Exit");
                chunk.Meshes.Add(doorMesh);
                var door = new GameObject("Exit door");
                door.transform.SetParent(go.transform, false);
                door.transform.position = at;
                door.AddComponent<MeshFilter>().sharedMesh = doorMesh;
                door.AddComponent<MeshRenderer>().sharedMaterials = paints;
                go.AddComponent<VanishingExit>().Door = at + exit.Pose.MultiplyVector(Vector3.up) * 1.1f;
            }
            if (exit.Kind != ExitKind.FlickerWall) return;
            // The flickering wall: looks like any other wall, but has no collision and flickers.
            var wall = new BoxMeshBuilder(exit.Trigger.center, 1);
            wall.AddBox(exit.Trigger, BoxMeshBuilder.Faces.Sides, 0);
            var mesh = wall.ToMesh("Flicker Wall");
            chunk.Meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = catalog.Resolve(theme, new SurfaceRef(Surface.Wall));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<FlickerWall>();
        }

        /// Level 4's exits can be gone once you've looked away (never in co-op, where everyone must see the same doors).
        bool Vanishing(ExitSpec e) => theme != null && theme.id == "4" && e.Kind != ExitKind.FlickerWall && !Net.Coop.Active;

        static GameObject CreateTrigger(Transform parent, string name, Bounds bounds, string target)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = bounds.center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = bounds.size;
            go.AddComponent<LevelTrigger>().Target = target;
            return go;
        }

        void ApplyBlackout()
        {
            float stutter = shownBlackout > 0.02f && shownBlackout < 0.95f && Random.value < 0.3f ? 0.5f : 0f;
            float dim = Mathf.Clamp01(shownBlackout + stutter);
            rig.Dim = dim;
            if (panelLit != null) panelLit.SetColor(glowProperty, Color.Lerp(panelGlow, new Color(0.08f, 0.08f, 0.07f), dim));
            // Without the lights only a trace of the room is left: no fill light, no reflections, black fog.
            if (atmosphere != null) atmosphere.SetBlackout(dim);
            nextLights = 0f;   // refresh the pool now
        }

        void RefreshLights()
        {
            nextLights = Time.time + 0.2f;
            emitterSets.Clear();
            foreach (var chunk in chunks.Values) if (chunk.Floor == CurrentFloor) emitterSets.Add(chunk.Emitters);   // light never leaks through floors
            rig.Refresh(player.position, emitterSets, theme, theme.generator == LevelGenerator.Suburbs ? 75f : lightDistance, CurrentDrift);
        }
    }
}
