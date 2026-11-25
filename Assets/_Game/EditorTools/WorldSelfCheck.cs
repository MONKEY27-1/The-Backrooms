using System.Collections.Generic;
using Backrooms.World;
using Backrooms.Creatures;
using UnityEditor;
using UnityEngine;

namespace Backrooms.EditorTools
{
    /// Backrooms > Run Self-Checks: verifies the generators' core promises for every level in the catalog,
    /// and measures how varied the district levels are.
    public static class WorldSelfCheck
    {
        const int Radius = 2;   // checks a (2R+1) x (2R+1) chunk window around chunk 0,0

        [MenuItem("Backrooms/Run Self-Checks")]
        public static bool Run()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Game/Data/LevelCatalog.asset");
            if (catalog == null) { Debug.LogError("[SelfCheck] no level catalog; run Backrooms > Create Game Scene"); return false; }
            int failures = 0;
            if (!GoreDeath.CheckCatalog(out var deathError)) { failures++; Debug.LogError("[SelfCheck] death catalog: " + deathError); }
            foreach (var level in catalog.levels)
            {
                if (level.generator == LevelGenerator.Static)
                {
                    if (level.staticLevel == null)
                    {
                        failures++;
                        Debug.LogError($"[SelfCheck] static level {level.id} has no model assigned");
                    }
                    else if (level.id == "fun" && AssetDatabase.GetAssetPath(level.staticLevel) != "Assets/Models/BackroomsLevelFun/LevelFun.fbx")
                    {
                        failures++;
                        Debug.LogError($"[SelfCheck] Level Fun model is {AssetDatabase.GetAssetPath(level.staticLevel)}, expected Assets/Models/BackroomsLevelFun/LevelFun.fbx");
                    }
                    continue;   // hand-made models aren't generated
                }
                if (level.generator == LevelGenerator.Stairwell) failures += CheckStairwell(level);
                else if (level.generator == LevelGenerator.Ocean || level.generator == LevelGenerator.Caves || level.generator == LevelGenerator.Suburbs)
                    failures += CheckSpecialGenerator(level);
                else failures += CheckLevel(level, Vector2Int.zero);
                if (DistrictLayout.HasFloors(level))   // stacked floors: the ones above and below must hold up too
                    foreach (int floor in new[] { 1, -1, DistrictLayout.TopFloor, DistrictLayout.BottomFloor })
                        failures += CheckLevel(level, new Vector2Int(3, -2), floor);
                if (level.id == "0") failures += CheckForged(level);
                if (level.id != "9223372036854775807" && UI.LevelLog.Entry(level.id).danger == "Unknown") { failures++; Debug.LogError($"[SelfCheck] no level log entry for {level.id}"); }
                if (level.id == "9223372036854775807" && string.IsNullOrEmpty(UI.LevelLog.Entry(level.id).text)) { failures++; Debug.LogError($"[SelfCheck] no level log entry for {level.id}"); }
                if (level.generator == LevelGenerator.Districts && level.id == "0") Variety(level);
            }
            failures += CheckMeta();
            Debug.Log(failures == 0 ? $"[SelfCheck] all world checks passed ({catalog.levels.Count} levels)" : $"[SelfCheck] {failures} failures");
            return failures == 0;
        }

        static int CheckStairwell(LevelTheme level)
        {
            int failures = 0;
            uint seed = WorldHash.LevelSeed(WorldHash.DefaultWorldSeed, level.id);
            var a = ChunkStreamer.GenerateChunk(level, seed, 0, 0, 0);
            var b = ChunkStreamer.GenerateChunk(level, seed, 0, 0, 0);
            var lower = ChunkStreamer.GenerateChunk(level, seed, 0, 0, -1);
            void Fail(string message) { failures++; Debug.LogError($"[SelfCheck] stairwell: {message}"); }
            if (a.Boxes.Count == 0 || a.Boxes.Count != b.Boxes.Count) Fail("missing or non-deterministic geometry");
            if (a.Exits.Count != 1 || a.Exits[0].Target != "0" || a.Exits[0].Kind != ExitKind.Elevator) Fail("expected a single elevator exit to Level 0");
            if (a.Props.Count != 0 || a.Readables.Count != 0) Fail("wiki level should contain no props, notes, or entities");
            bool upperHasSteps = a.OrientedBoxes.Count >= 140, lowerHasSteps = lower.OrientedBoxes.Count >= 140;
            if (!upperHasSteps || !lowerHasSteps) Fail("spiral treads are missing from an upper or lower streamed segment");
            if (a.OrientedBoxes.Count > 1 && Mathf.Abs(a.OrientedBoxes[1].Center.y - a.OrientedBoxes[0].Center.y) > 0.07f)
                Fail("spiral risers exceed the walkable step height");
            if (upperHasSteps)
            {
                var first = a.OrientedBoxes[0];
                if (Mathf.Abs(first.Center.y + first.Size.y * 0.5f) > 0.01f)
                    Fail("the first tread should meet the lobby floor at height zero");
                var pose = Matrix4x4.TRS(first.Center, Quaternion.Euler(0f, first.Yaw, 0f), Vector3.one);
                var treadBounds = new Bounds(pose.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
                for (int corner = 0; corner < 8; corner++)
                    treadBounds.Encapsulate(pose.MultiplyPoint3x4(Vector3.Scale(first.Size * 0.5f, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
                if (treadBounds.min.x > 16f || treadBounds.max.x < 16f || treadBounds.min.z > 13.4f || treadBounds.max.z < 13.4f)
                    Fail("the first tread does not cover the initial stairwell spawn");
            }
            foreach (var quad in a.Quads)
                if (quad.FacingUp && quad.Height <= 0.01f && quad.Area.Contains(new Vector2(16f, 16f)))
                { Fail("a lobby floor quad covers the spiral shaft"); break; }
            Debug.Log($"[SelfCheck] stairwell: {a.Boxes.Count} upper-segment boxes, {lower.Boxes.Count} lower-segment boxes, {a.Exits.Count} elevator exit");
            return failures;
        }

        static int CheckSpecialGenerator(LevelTheme level)
        {
            int failures = 0;
            uint seed = WorldHash.LevelSeed(WorldHash.DefaultWorldSeed, level.id);
            for (int cx = -1; cx <= 1; cx++)
            for (int cz = -1; cz <= 1; cz++)
            {
                var a = ChunkStreamer.GenerateChunk(level, seed, cx, cz, 0);
                var b = ChunkStreamer.GenerateChunk(level, seed, cx, cz, 0);
                if (a.Boxes.Count != b.Boxes.Count || a.Quads.Count != b.Quads.Count || a.Props.Count != b.Props.Count || a.Water.Count != b.Water.Count || a.Rocks.Count != b.Rocks.Count || a.Rocks.Exists(r => r.V.Count != b.Rocks[a.Rocks.IndexOf(r)].V.Count))
                {
                    failures++;
                    Debug.LogError($"[SelfCheck] special level {level.id}: chunk {cx},{cz} is not deterministic");
                }
            }
            if (level.generator == LevelGenerator.Suburbs)
                for (uint k = 0; k < 40; k++)
                {
                    string bad = SuburbsLayout.CheckLine(seed + k * 7919u);
                    if (bad != null) { failures++; Debug.LogError($"[SelfCheck] level 9, seed {seed + k * 7919u}: {bad}"); }
                }
            if (level.generator == LevelGenerator.Caves)
                for (uint k = 0; k < 12; k++)
                {
                    string bad = CaveLayout.CheckRoad(seed + k * 7919u);
                    if (bad != null) { failures++; Debug.LogError($"[SelfCheck] level 8, seed {seed + k * 7919u}: {bad}"); }
                }
            Debug.Log($"[SelfCheck] special level {level.id} ({level.generator}) checked across 9 chunks: fully deterministic");
            return failures;
        }

        /// Hand-made rooms (Assets/_Game/Resources/Forge): every one must be fully walkable too.
        /// Save slots round-trip; achievement ids are unique.
        static int CheckMeta()
        {
            int failures = 0;
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var a in Meta.Achievements.All)
                if (!ids.Add(a.Id)) { failures++; Debug.LogError($"[SelfCheck] duplicate achievement {a.Id}"); }
            var data = new Game.SaveData { slot = Game.SaveSystem.NewSlot(), name = "SelfCheck", levelId = "1", worldSeed = 42, playSeconds = 12f };
            Game.SaveSystem.Save(data);
            var back = Game.SaveSystem.Load(data.slot);
            if (back == null || back.levelId != "1" || back.worldSeed != 42 || !Game.SaveSystem.List().Exists(d => d.slot == data.slot))
            { failures++; Debug.LogError("[SelfCheck] save slot didn't round-trip"); }
            Game.SaveSystem.Delete(data.slot);
            if (Game.SaveSystem.Load(data.slot) != null) { failures++; Debug.LogError("[SelfCheck] save slot wasn't deleted"); }
            return failures;
        }

        static int CheckForged(LevelTheme level)
        {
            ForgeLibrary.Reload();
            uint seed = WorldHash.LevelSeed(WorldHash.DefaultWorldSeed, level.id);
            int failures = 0;
            var seen = new HashSet<string>();
            for (int r = 2; r < 40; r++)
                for (int dx = -r; dx <= r; dx++)
                foreach (int dz in new[] { -r, r })
                {
                    var room = ForgeLibrary.RoomAt(seed, dx, dz, level);
                    if (room == null || !seen.Add(room.name)) continue;
                    failures += CheckLevel(level, new Vector2Int(dx * 2 + 1, dz * 2 + 1));
                }
            Debug.Log($"[SelfCheck] forged rooms checked: {string.Join(", ", seen)}");
            return failures;
        }

        static int CheckLevel(LevelTheme level, Vector2Int centre, int floor = 0)
        {
            uint seed = WorldHash.LevelSeed(WorldHash.DefaultWorldSeed, level.id);
            int failures = 0, exits = 0, props = 0, slots = 0;
            string name = floor == 0 ? level.id : $"{level.id} floor {floor}";
            void Fail(string msg) { failures++; if (failures < 6) Debug.LogError($"[SelfCheck] level {name}: {msg}"); }
            float baseY = floor * DistrictLayout.FloorHeight;

            var chunks = new Dictionary<Vector2Int, List<Bounds>>();
            var floors = new Dictionary<Vector2Int, List<Rect>>();
            var openings = new List<Bounds>();
            var furniture = new List<Bounds>();
            for (int cx = centre.x - Radius; cx <= centre.x + Radius; cx++)
            for (int cz = centre.y - Radius; cz <= centre.y + Radius; cz++)
            {
                DistrictLayout.ClearCache();
                var a = ChunkStreamer.GenerateChunk(level, seed, cx, cz, floor);
                DistrictLayout.ClearCache();
                var b = ChunkStreamer.GenerateChunk(level, seed, cx, cz, floor);
                var walkable = new List<Rect>();
                foreach (var q in a.Quads) if (q.FacingUp && q.Height < baseY + 0.5f) walkable.Add(q.Area);
                floors[new Vector2Int(cx, cz)] = walkable;
                // Reachability is about architecture: walls, solids, pillars. Furniture is checked against doorways below.
                var solid = new List<Bounds>();
                // Lone pillars and posts (under 1.3 m across) never fill a 2 m cell: you walk around them.
                foreach (var box in a.Boxes) if (box.Solid && (box.Box.size.x >= 1.3f || box.Box.size.z >= 1.3f)) solid.Add(box.Box);
                chunks[new Vector2Int(cx, cz)] = solid;
                openings.AddRange(a.Openings);
                furniture.AddRange(a.Blockers);
                exits += a.Exits.Count;
                props += a.Props.Count;
                slots += a.Curiosities.Count;

                // Same inputs, same chunk: revisiting a place must look identical.
                if (a.Boxes.Count != b.Boxes.Count || a.Props.Count != b.Props.Count || a.Exits.Count != b.Exits.Count || a.Panels.Count != b.Panels.Count)
                    Fail($"chunk {cx},{cz} not deterministic");
                else
                    for (int i = 0; i < a.Boxes.Count; i++)
                        if (a.Boxes[i].Box != b.Boxes[i].Box || !a.Boxes[i].PosX.Equals(b.Boxes[i].PosX)) { Fail($"chunk {cx},{cz} box {i} differs between runs"); break; }

                // Chunks only build what they own, so nothing spills into a neighbour.
                float size = MazeLayout.ChunkSize;
                var chunkRect = new Rect(cx * size - 0.35f, cz * size - 0.35f, size + 0.7f, size + 0.7f);
                foreach (var box in a.Boxes)
                    if (box.Solid && (!chunkRect.Contains(new Vector2(box.Box.min.x, box.Box.min.z)) || !chunkRect.Contains(new Vector2(box.Box.max.x, box.Box.max.z))
                                      || box.Box.min.y < baseY - DistrictLayout.PoolDepth - 0.5f || box.Box.max.y > baseY + DistrictLayout.FloorHeight + 0.1f))
                        Fail($"chunk {cx},{cz} box {box.Box} outside its chunk");
            }

            // No large furniture may stand in a doorway.
            foreach (var f in furniture)
                foreach (var o in openings)
                    if (f.Intersects(o)) { Fail($"furniture at {f.center} blocks the doorway at {o.center}"); break; }

            if (level.style.exits.Count > 0 && exits == 0) Debug.LogWarning($"[SelfCheck] level {name}: no exits in a {2 * Radius + 1}x{2 * Radius + 1} chunk area");
            if (level.generator != LevelGenerator.Stairwell) failures += CheckReachable(name, chunks, floors, centre, baseY);
            Debug.Log($"[SelfCheck] level {name}: {exits} exits, {props} props, {slots} curiosity slots in {(2 * Radius + 1) * (2 * Radius + 1)} chunks");
            return failures;
        }

        /// Flood-fills cells from an open cell near the origin. Every open cell in the inner 3x3 chunks must be reachable.
        static int CheckReachable(string id, Dictionary<Vector2Int, List<Bounds>> chunks, Dictionary<Vector2Int, List<Rect>> floors, Vector2Int centre, float baseY)
        {
            // A cell with no floor under it (a stairwell or drop opening) isn't somewhere you stand.
            bool HasFloor(Vector3 p)
            {
                if (!floors.TryGetValue(ChunkStreamer.ChunkOf(p), out var rects)) return true;
                foreach (var r in rects) if (r.Contains(new Vector2(p.x, p.z))) return true;
                return false;
            }
            int n = MazeLayout.CellsPerChunk, size = (2 * Radius + 1) * n, offset = Radius * n;
            float cell = MazeLayout.CellSize;

            bool Blocked(Vector3 p)
            {
                var c = ChunkStreamer.ChunkOf(p);
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (chunks.TryGetValue(c + new Vector2Int(dx, dz), out var walls))
                        foreach (var w in walls)
                            if (w.Contains(p)) return true;
                return false;
            }
            Vector3 Centre(int i, int j) => new Vector3((i - offset + 0.5f) * cell + centre.x * MazeLayout.ChunkSize, baseY + 1f, (j - offset + 0.5f) * cell + centre.y * MazeLayout.ChunkSize);

            var open = new bool[size, size];
            for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                open[i, j] = !Blocked(Centre(i, j)) && HasFloor(Centre(i, j));

            var start = new Vector2Int(-1, -1);
            for (int r = 0; r < n && start.x < 0; r++)
                for (int i = offset - r; i <= offset + r && start.x < 0; i++)
                for (int j = offset - r; j <= offset + r && start.x < 0; j++)
                    if (open[i, j]) start = new Vector2Int(i, j);
            if (start.x < 0) { Debug.LogError($"[SelfCheck] level {id}: no open cell near the origin"); return 1; }

            var seen = new bool[size, size];
            var queue = new Queue<Vector2Int>();
            seen[start.x, start.y] = true;
            queue.Enqueue(start);
            var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var s in steps)
                {
                    var d = c + s;
                    if (d.x < 0 || d.y < 0 || d.x >= size || d.y >= size || seen[d.x, d.y] || !open[d.x, d.y]) continue;
                    if (Blocked((Centre(c.x, c.y) + Centre(d.x, d.y)) / 2)) continue;   // wall on the shared edge
                    seen[d.x, d.y] = true;
                    queue.Enqueue(d);
                }
            }

            int unreachable = 0;
            for (int i = n; i < size - n; i++)
            for (int j = n; j < size - n; j++)
                if (open[i, j] && !seen[i, j])
                {
                    if (unreachable < 5) Debug.LogError($"[SelfCheck] level {id}: cell at {Centre(i, j)} is sealed off");
                    unreachable++;
                }
            if (unreachable > 0) Debug.LogError($"[SelfCheck] level {id}: {unreachable} open cells unreachable");
            return unreachable > 0 ? 1 : 0;
        }

        /// How varied is the district level? Counts archetypes, repeats between neighbours, and distinct chunk layouts.
        static void Variety(LevelTheme level)
        {
            uint seed = WorldHash.LevelSeed(WorldHash.DefaultWorldSeed, level.id);
            const int districts = 14;
            var kinds = new Dictionary<DistrictKind, int>();
            int sameNeighbour = 0, pairs = 0;
            for (int x = 0; x < districts; x++)
            for (int z = 0; z < districts; z++)
            {
                var k = DistrictLayout.KindAt(seed, x * 2, z * 2, level.districts);
                kinds[k] = kinds.TryGetValue(k, out int c) ? c + 1 : 1;
                if (x > 0) { pairs++; if (DistrictLayout.KindAt(seed, (x - 1) * 2, z * 2, level.districts) == k) sameNeighbour++; }
                if (z > 0) { pairs++; if (DistrictLayout.KindAt(seed, x * 2, (z - 1) * 2, level.districts) == k) sameNeighbour++; }
            }
            var summary = new System.Text.StringBuilder();
            foreach (var kv in kinds) summary.Append($"{kv.Key} {kv.Value}, ");

            // Distinct chunk layouts: fingerprint of each chunk's geometry and materials relative to its corner.
            var fingerprints = new HashSet<long>();
            var looks = new HashSet<long>();
            const int span = 12;
            for (int cx = 0; cx < span; cx++)
            for (int cz = 0; cz < span; cz++)
            {
                var layout = ChunkStreamer.GenerateChunk(level, seed, cx, cz);
                var corner = new Vector3(cx, 0f, cz) * MazeLayout.ChunkSize;
                long h = 17;
                foreach (var b in layout.Boxes)
                {
                    var p = b.Box.center - corner;
                    h = h * 31 + Mathf.RoundToInt(p.x * 10f) * 7919 + Mathf.RoundToInt(p.z * 10f) * 104729 + Mathf.RoundToInt(b.Box.size.x * 10f) + b.PosX.GetHashCode();
                }
                fingerprints.Add(h);
                foreach (var q in layout.Quads) looks.Add(((long)q.Surface.GetHashCode() << 20) | (long)(q.Height * 10f) << 1 | (q.Rotated ? 1L : 0L));
            }
            Debug.Log($"[Variety] level {level.id}: districts in a {districts}x{districts} area: {summary}"
                      + $"same archetype as a neighbour: {sameNeighbour}/{pairs}; "
                      + $"distinct chunk layouts: {fingerprints.Count}/{span * span}; distinct floor/ceiling looks: {looks.Count}");
        }
    }
}
