using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// A door that stays locked until the level's goal is done, then stands open: walk through to leave.
    public sealed class GoalDoor : MonoBehaviour
    {
        public string Target, LockedText;
        public bool Locked = true;
        public Transform Leaf;
        public GameObject LampLocked, LampOpen;   // Level 4's badge reader: red, then green
        GameObject trigger;

        public void Unlock()
        {
            if (!Locked && trigger != null) return;
            Locked = false;
            if (LampLocked != null) LampLocked.SetActive(false);
            if (LampOpen != null) LampOpen.SetActive(true);
            if (Leaf != null) Leaf.localRotation = Quaternion.Euler(0f, 100f, 0f);
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            trigger = new GameObject("Exit to " + Target);
            trigger.transform.SetParent(transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1f, 0.25f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 2f, 0.5f);
            trigger.AddComponent<LevelTrigger>().Target = Target;
        }
    }

    /// Each level's goal: find a few things somewhere out there, which unlocks a marked door: a guaranteed way out
    /// (other exits still work). Items and the door appear when their chunk loads; progress is saved per world.
    public sealed class Objectives : MonoBehaviour
    {
        sealed class Goal
        {
            public string Text, Item, Plural, Door, Hint;
            public int Count;
            public PropPaint Colour;
        }

        static readonly Dictionary<string, Goal> Goals = new Dictionary<string, Goal>
        {
            ["0"] = new Goal { Text = "Find the pages of a surveyor's map", Item = "map page", Plural = "map pages", Count = 3, Door = "1", Hint = "Locked. Someone has written on it: BRING THE MAP.", Colour = PropPaint.White },
            ["1"] = new Goal { Text = "Find a fuse for the stairwell door", Item = "fuse", Plural = "fuses", Count = 1, Door = "2", Hint = "No power. The panel next to it is missing a fuse.", Colour = PropPaint.Yellow },
            ["2"] = new Goal { Text = "Find the wheels for the steam door's valves", Item = "valve wheel", Plural = "valve wheels", Count = 2, Door = "1", Hint = "Two bare valve stems. They need their wheels.", Colour = PropPaint.Red },
            ["3"] = new Goal { Text = "Find fuses for the freight lift's breaker panel", Item = "fuse", Plural = "fuses", Count = 3, Door = "4", Hint = "No power to the lift. The breaker panel beside it is missing three fuses.", Colour = PropPaint.Yellow },
            ["4"] = new Goal { Text = "Find two employee keycards for the stairwell door", Item = "keycard", Plural = "keycards", Count = 2, Door = "5", Hint = "The badge reader beside the stairwell door blinks red. ACCESS DENIED.", Colour = PropPaint.Blue },
            ["5"] = new Goal { Text = "Find the key to your room", Item = "room key", Plural = "room keys", Count = 1, Door = "6", Hint = "Locked. Your key is somewhere in the hotel.", Colour = PropPaint.Brass },
            ["37"] = new Goal { Text = "Find the keys to the staff door", Item = "brass key", Plural = "brass keys", Count = 2, Door = "0", Hint = "Two keyholes. Locked.", Colour = PropPaint.Yellow },
            ["0.3"] = new Goal { Text = "Find fuses to restart the heater door", Item = "heater fuse", Plural = "heater fuses", Count = 2, Door = "0", Hint = "Frozen shut. The heater panel is missing its fuses.", Colour = PropPaint.Yellow },
            ["19"] = new Goal { Text = "Find the key to the hatch", Item = "iron key", Plural = "iron keys", Count = 1, Door = "0", Hint = "A padlock, rusted but solid.", Colour = PropPaint.Metal },
        };

        static readonly Dictionary<string, string> TextOnly = new Dictionary<string, string>
        {
            ["0.2"] = "Get back to the door you came in through, before it all comes down",
            ["-1"] = "Find the pump control room and drain the pool",
            ["6"] = "Listen. Follow the sound of waves to the stairs down",
            ["188"] = "Find the lit window. Don't look at the shadows. Music means leave.",
            ["fun"] = "Find a way out. Some of the exit signs are real. Don't eat anything.",
            ["9223372036854775807"] = "Find the elevator back to the Lobby; the staircase has no known end",
        };

        ChunkStreamer streamer;
        FirstPersonController player;
        PlayerHud hud;
        SaveData save;
        string level;
        Goal goal;
        readonly List<Vector3Int> itemChunks = new List<Vector3Int>();
        Vector3Int doorChunk;
        readonly Dictionary<int, GameObject> items = new Dictionary<int, GameObject>();
        GoalDoor door;

        public void Init(ChunkStreamer world, FirstPersonController body, PlayerHud h, SaveData s, string levelId, Vector2Int arrival)
        {
            streamer = world;
            player = body;
            hud = h;
            save = s;
            level = levelId;
            Goals.TryGetValue(levelId, out goal);
            if (levelId == "3") Level3Power.Set(save.goals.Contains("3:done"), false);   // the station's power stays on once you've restored it
            if (goal != null)
            {
                // Where things are: a pure function of the world and level, measured from where this level was entered.
                uint seed = WorldHash.Hash(save.worldSeed, (int)WorldHash.StringHash(levelId), 0, 0x60A1);
                for (int i = 0; i < goal.Count; i++)
                {
                    float a = (WorldHash.Value(seed, i, 1) + i / (float)goal.Count) * Mathf.PI * 2f;
                    int d = 2 + (int)(WorldHash.Value(seed, i, 2) * 2f);
                    itemChunks.Add(new Vector3Int(arrival.x + Mathf.RoundToInt(Mathf.Cos(a) * d), 0, arrival.y + Mathf.RoundToInt(Mathf.Sin(a) * d)));
                }
                float da = WorldHash.Value(seed, 9, 1) * Mathf.PI * 2f;
                doorChunk = new Vector3Int(arrival.x + Mathf.RoundToInt(Mathf.Cos(da) * 2f), 0, arrival.y + Mathf.RoundToInt(Mathf.Sin(da) * 2f));
            }
            streamer.ChunkReady += OnChunk;
            foreach (var k in itemChunks) if (streamer.Loaded(k)) OnChunk(k);
            if (goal != null && streamer.Loaded(doorChunk)) OnChunk(doorChunk);
        }

        void OnDestroy()
        {
            if (streamer != null) streamer.ChunkReady -= OnChunk;
            foreach (var g in items.Values) if (g != null) Destroy(g);
            if (door != null) Destroy(door.gameObject);
        }

        /// Level 5: the number on your room's door (and on the key's tag).
        int MyRoom => DistrictLayout.GoalRoomNumber(WorldHash.LevelSeed(save.worldSeed, "5"));

        bool Has(int i) => save.goals.Contains($"{level}:{i}");
        bool Done => save.goals.Contains($"{level}:done");
        int Found { get { int n = 0; for (int i = 0; i < goal.Count; i++) if (Has(i)) n++; return n; } }

        void OnChunk(Vector3Int key)
        {
            if (goal == null) return;
            for (int i = 0; i < itemChunks.Count; i++)
                if (itemChunks[i] == key && !Has(i) && !Done && !items.ContainsKey(i))
                {
                    var spot = FloorSpot(key, i);
                    if (spot.HasValue) items[i] = MakeItem(i, spot.Value);
                }
            if (key == doorChunk && door == null)
            {
                if (WallSpot(key, out var pose)) door = MakeDoor(pose);
            }
        }

        /// A clear spot on the floor in that chunk.
        Vector3? FloorSpot(Vector3Int key, int salt)
        {
            Physics.SyncTransforms();
            float size = MazeLayout.ChunkSize, y = key.y * DistrictLayout.FloorHeight;
            for (int n = 0; n < 60; n++)
            {
                uint h = WorldHash.Hash(save.worldSeed, key.x * 31 + n, key.z * 17 + salt, 0x60A2);
                var p = new Vector3((key.x + WorldHash.Value(h, 1)) * size, y + 1.5f, (key.z + WorldHash.Value(h, 2)) * size);
                if (!Physics.Raycast(p, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - y) > 0.3f) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 0.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                return hit.point;
            }
            return null;
        }

        /// A stretch of wall in that chunk tall and wide enough for a door.
        bool WallSpot(Vector3Int key, out Matrix4x4 pose)
        {
            Physics.SyncTransforms();
            float size = MazeLayout.ChunkSize, y = key.y * DistrictLayout.FloorHeight;
            for (int n = 0; n < 80; n++)
            {
                uint h = WorldHash.Hash(save.worldSeed, key.x * 13 + n, key.z * 29, 0x60A3);
                var p = new Vector3((key.x + WorldHash.Value(h, 1)) * size, y + 1f, (key.z + WorldHash.Value(h, 2)) * size);
                if (Physics.CheckSphere(p, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var dir = Quaternion.Euler(0f, (int)(WorldHash.Value(h, 3) * 4f) * 90f, 0f) * Vector3.forward;
                if (!Physics.Raycast(p, dir, out var wall, 6f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(wall.normal.y) > 0.1f) continue;
                var n2 = wall.normal;
                var right = Vector3.Cross(Vector3.up, n2);
                bool tall = Physics.Raycast(wall.point + n2 * 0.3f + Vector3.up * 1.3f, -n2, 0.5f) && Physics.Raycast(wall.point + n2 * 0.3f + right * 0.6f, -n2, 0.5f)
                            && Physics.Raycast(wall.point + n2 * 0.3f - right * 0.6f, -n2, 0.5f);
                if (!tall) continue;
                var foot = new Vector3(wall.point.x, y, wall.point.z);
                pose = Matrix4x4.TRS(foot, Quaternion.LookRotation(n2), Vector3.one);
                return true;
            }
            pose = default;
            return false;
        }

        GameObject MakeItem(int i, Vector3 at)
        {
            var go = new GameObject(goal.Item);
            go.transform.position = at;
            var mesh = new BoxMeshBuilder(at, streamer.Paints.Length);
            var p = Matrix4x4.TRS(at, Quaternion.Euler(0f, i * 67f, 0f), Vector3.one);
            switch (goal.Item)
            {
                case "map page":
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.005f, 0f), new Vector3(0.21f, 0.004f, 0.3f), (int)PropPaint.White);
                    for (int k = 0; k < 4; k++) mesh.AddOrientedBox(p, new Vector3(-0.05f + k * 0.03f, 0.008f, 0.02f * k - 0.04f), new Vector3(0.12f, 0.002f, 0.006f), (int)PropPaint.Blue);
                    break;
                case "valve wheel":
                    for (int k = 0; k < 8; k++)
                        mesh.AddOrientedBox(p * Matrix4x4.TRS(new Vector3(0f, 0.025f, 0f), Quaternion.Euler(0f, k * 45f, 0f), Vector3.one), new Vector3(0f, 0f, 0.13f), new Vector3(0.1f, 0.03f, 0.03f), (int)PropPaint.Red);
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.025f, 0f), new Vector3(0.04f, 0.04f, 0.04f), (int)PropPaint.Metal);
                    break;
                case "room key":   // a brass key on a heavy brass tag stamped with your room number
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.004f, 0f), new Vector3(0.05f, 0.006f, 0.085f), (int)PropPaint.Brass);
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.005f, 0.075f), new Vector3(0.008f, 0.005f, 0.07f), (int)PropPaint.Brass);
                    mesh.AddOrientedBox(p, new Vector3(0.008f, 0.005f, 0.105f), new Vector3(0.012f, 0.005f, 0.01f), (int)PropPaint.Brass);
                    PropLibrary.Label(mesh, p * Matrix4x4.TRS(new Vector3(0f, 0.008f, 0f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one), MyRoom.ToString(), 0.18f);   // face up
                    break;
                case "keycard":   // a white employee badge with a blue stripe and a clip
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.002f, 0f), new Vector3(0.054f, 0.003f, 0.086f), (int)PropPaint.White);
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.004f, 0.02f), new Vector3(0.055f, 0.002f, 0.018f), (int)PropPaint.Blue);
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.004f, -0.018f), new Vector3(0.02f, 0.002f, 0.025f), (int)PropPaint.Dark);
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.004f, 0.05f), new Vector3(0.014f, 0.004f, 0.012f), (int)PropPaint.Metal);
                    break;
                case "brass key":
                case "iron key":
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.01f, 0f), new Vector3(0.025f, 0.012f, 0.1f), (int)goal.Colour);
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.01f, -0.07f), new Vector3(0.05f, 0.012f, 0.05f), (int)goal.Colour);
                    break;
                default:   // a fuse: a ceramic cartridge with metal caps
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.025f, 0f), new Vector3(0.05f, 0.05f, 0.12f), (int)PropPaint.White);
                    foreach (float z in new[] { -0.07f, 0.07f }) mesh.AddOrientedBox(p, new Vector3(0f, 0.025f, z), new Vector3(0.055f, 0.055f, 0.03f), (int)PropPaint.Metal);
                    mesh.AddOrientedBox(p, new Vector3(0f, 0.051f, 0f), new Vector3(0.03f, 0.002f, 0.06f), (int)goal.Colour);
                    break;
            }
            // A glint, so it can be found in the dark.
            mesh.AddOrientedBox(p, new Vector3(0f, 0.06f, 0f), new Vector3(0.015f, 0.015f, 0.015f), (int)PropPaint.Warm);
            var m = mesh.ToMesh(goal.Item);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterials = streamer.Paints;
            var hit = go.AddComponent<SphereCollider>();
            hit.isTrigger = true;
            hit.center = Vector3.up * 0.15f;
            hit.radius = 0.35f;
            var pick = go.AddComponent<Pickup>();
            pick.Goal = goal.Item;
            pick.Id = i;
            return go;
        }

        GoalDoor MakeDoor(Matrix4x4 pose)
        {
            Vector3 at = pose.GetColumn(3);
            var go = new GameObject("Goal Door");
            go.transform.SetPositionAndRotation(at, pose.rotation);
            var frame = new BoxMeshBuilder(at, streamer.Paints.Length);
            if (level != "5") PropLibrary.BuildMarkedFrame(goal.Door, pose, frame);
            if (level == "5") PropLibrary.DoorFrameOnly(pose, frame, true);   // your room: a plain casing, the hotel leaf below
            else
            if (level == "4")   // the badge reader by the stairwell door (its lamps are separate, below)
                PropLibrary.Build(new PropSpec { Kind = PropKind.BadgeReader, Pose = pose * Matrix4x4.Translate(new Vector3(0.78f, 0f, 0f)) }, frame, 3f);
            if (level == "3")   // the lift's breaker panel, beside the door: three empty fuse holders at the top
                PropLibrary.Build(new PropSpec { Kind = PropKind.BreakerPanel, Pose = pose * Matrix4x4.Translate(new Vector3(1.25f, 0f, 0f)) }, frame, 3f);
            var mf = new GameObject("Frame");
            mf.transform.SetParent(go.transform, false);
            mf.transform.SetPositionAndRotation(at, Quaternion.identity);
            mf.AddComponent<MeshFilter>().sharedMesh = frame.ToMesh("Goal Door");
            mf.AddComponent<MeshRenderer>().sharedMaterials = streamer.Paints;
            var hinge = new GameObject("Leaf").transform;
            hinge.SetParent(go.transform, false);
            hinge.localPosition = new Vector3(-0.45f, 0f, 0.05f);
            var leaf = new BoxMeshBuilder(Vector3.zero, streamer.Paints.Length);
            if (level == "5" && ModelLibrary.Add(leaf, "L5DoorLeaf", Matrix4x4.Translate(new Vector3(0.92f, 0f, 0f))))   // a guest room door with your number on it
                PropLibrary.RoomNumber(leaf, Matrix4x4.Translate(new Vector3(0.46f, 0f, 0f)), MyRoom, 0.034f);
            else
            {
                leaf.AddOrientedBox(Matrix4x4.identity, new Vector3(0.45f, 1.02f, 0f), new Vector3(0.92f, 2.06f, 0.05f), (int)PropPaint.Metal);
                leaf.AddOrientedBox(Matrix4x4.identity, new Vector3(0.8f, 1.0f, 0.04f), new Vector3(0.08f, 0.14f, 0.04f), (int)PropPaint.Dark);   // the lock
            }
            hinge.gameObject.AddComponent<MeshFilter>().sharedMesh = leaf.ToMesh("Goal Door Leaf");
            hinge.gameObject.AddComponent<MeshRenderer>().sharedMaterials = streamer.Paints;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, 0.15f);
            box.size = new Vector3(1.1f, 2.1f, 0.3f);
            var d = go.AddComponent<GoalDoor>();
            d.Target = goal.Door;
            d.LockedText = goal.Hint;
            d.Leaf = hinge;
            if (level == "4")
            {
                GameObject Lamp(string name, PropPaint paint)
                {
                    var lamp = new GameObject(name);
                    lamp.transform.SetParent(go.transform, false);
                    lamp.transform.localPosition = new Vector3(0.78f, 1.29f, 0.034f);
                    var lb = new BoxMeshBuilder(Vector3.zero, streamer.Paints.Length);
                    lb.AddOrientedBox(Matrix4x4.identity, Vector3.zero, new Vector3(0.014f, 0.014f, 0.004f), (int)paint);
                    lamp.AddComponent<MeshFilter>().sharedMesh = lb.ToMesh(name);
                    lamp.AddComponent<MeshRenderer>().sharedMaterials = streamer.Paints;
                    return lamp;
                }
                d.LampLocked = Lamp("Reader lamp red", PropPaint.Red);
                d.LampOpen = Lamp("Reader lamp green", PropPaint.NeonCyan);
                d.LampOpen.SetActive(false);
            }
            if (Done) d.Unlock();
            return d;
        }

        /// The player took a goal item.
        /// A friend picked up goal item `i` (co-op): it's gone here too.
        public void TookRemotely(int i)
        {
            if (items.TryGetValue(i, out var go) && go != null) Destroy(go);
            Took(i);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DeveloperComplete()
        {
            if (goal == null || Done) return;
            for (int i = 0; i < goal.Count; i++)
            {
                if (Has(i)) continue;
                save.goals.Add($"{level}:{i}");
                if (items.TryGetValue(i, out var item) && item != null) Destroy(item);
                items.Remove(i);
            }
            save.goals.Add($"{level}:done");
            door?.Unlock();
            if (level == "3") Level3Power.Set(true, true);
            hud?.Flash("Developer: objective complete.", 3f);
        }
#endif

        public void Took(int i)
        {
            if (goal == null || Has(i)) return;
            save.goals.Add($"{level}:{i}");
            items.Remove(i);
            int left = goal.Count - Found;
            if (left > 0) hud.Flash($"{Capital(goal.Item)}. {left} more to find.", 4f);
            else
            {
                save.goals.Add($"{level}:done");
                var st = player.GetComponent<PlayerStatus>();
                if (st != null) { st.AlmondWater++; st.Batteries++; }
                hud.Flash((level == "5" ? $"A brass key on a heavy tag: ROOM {MyRoom}. Somewhere in the hotel, a lock turns. (Found some water and a battery too.)" :
                          level == "4" ? "Second keycard. Somewhere a badge reader clicks and turns green. (Found some water and a battery too.)" :
                          level == "3" ? "The last fuse clicks home. Power floods the station, and the lift door opens. (Found some water and a battery too.)"
                                       : "That's everything. The marked door will open now. (Found some water and a battery too.)") + ExitHint(), 8f);
                if (door != null) door.Unlock();
                if (level == "3") Level3Power.Set(true, true);
            }
        }

        List<Vector3> wires;   // Level 9's line
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static string Capital(string s) => char.ToUpper(s[0]) + s.Substring(1);

        void Update()
        {
            if (hud == null) return;
            hud.Waypoint = goal != null && Done ? ExitPos() : (Vector3?)null;
            if (level == "7")   // dive to the light on the drowned mountain
            {
                var cave = OceanLayout.CaveMouth(WorldHash.LevelSeed(save.worldSeed, "7"));
                var toCave = cave - player.transform.position;
                float depth = Mathf.Max(0f, OceanLayout.OceanWaterLevel - player.transform.position.y);
                float flat = new Vector2(toCave.x, toCave.z).magnitude;
                string gear = Level7Events.HasGear ? "" : "   (Fort Surrender had a diving helmet)";
                hud.Waypoint = cave;
                hud.Objective = $"Dive to the light on the drowned mountain  {Compass(toCave)}, about {Mathf.Max(10, Mathf.RoundToInt(flat / 10f) * 10)} m, 88 m down   (you: {Mathf.RoundToInt(depth)} m){gear}";
                return;
            }
            if (level == "9")   // pole to pole, to wherever the wires go
            {
                wires ??= SuburbsLayout.Line(WorldHash.LevelSeed(save.worldSeed, "9"));
                int near = 0;
                for (int i = 1; i < wires.Count; i++)
                    if (Flat(wires[i] - player.transform.position) < Flat(wires[near] - player.transform.position)) near = i;
                int next = Mathf.Min(near + 1, wires.Count - 1);
                var to = wires[next] - player.transform.position;
                hud.Waypoint = wires[next] + Vector3.up * 2f;
                hud.Objective = $"{(next == wires.Count - 1 ? "The wires end at that house. Go in" : "Follow the power line")}   {Compass(to)}, about {Mathf.Max(5, Mathf.RoundToInt(Flat(to) / 5f) * 5)} m";
                return;
            }
            if (level == "8" && Level8Events.Instance != null)   // the 9th Road, marker by marker
            {
                var road = Level8Events.Instance;
                var to = road.Next - player.transform.position;
                bool last = road.Reached >= road.Markers - 2;
                hud.Waypoint = road.Next;
                hud.Objective = $"{(last ? "The road ends in a shaft. Drop into it" : road.Reached < 0 ? "Find the first post of the 9th Road" : $"Walk the 9th Road: marker {road.Reached + 1} of {road.Markers - 2}")}   {Compass(to)}, about {Mathf.Max(5, Mathf.RoundToInt(to.magnitude / 5f) * 5)} m";
                return;
            }
            if (goal == null)
            {
                hud.Objective = TextOnly.TryGetValue(level, out var t) && !(level == "-1" && PoolSwitch.Pulled) ? t : "";
                return;
            }
            if (Done && level == "5")
            {
                var to = ExitPos() - player.transform.position;
                hud.Objective = to.magnitude < 6f ? $"Room {MyRoom}. Your key fits." : $"Find room {MyRoom}   {Compass(to)}, about {Mathf.Max(10, Mathf.RoundToInt(to.magnitude / 10f) * 10)} m";
                return;
            }
            if (Done) { hud.Objective = door != null && Vector3.Distance(door.transform.position, player.transform.position) < 40f ? $"The door to {goal.Door} is open" : $"Find the marked door to {goal.Door} (it's open now)"; return; }
            // The nearest item still out there: roughly which way, roughly how far.
            Vector3 p = player.transform.position, best = Vector3.zero;
            float bestD = float.MaxValue;
            for (int i = 0; i < itemChunks.Count; i++)
            {
                if (Has(i)) continue;
                var c = items.TryGetValue(i, out var go) && go != null ? go.transform.position
                    : new Vector3((itemChunks[i].x + 0.5f) * MazeLayout.ChunkSize, p.y, (itemChunks[i].z + 0.5f) * MazeLayout.ChunkSize);
                float d = Vector3.Distance(new Vector3(c.x, p.y, c.z), p);
                if (d < bestD) { bestD = d; best = c; }
            }
            string where = bestD < 6f ? "right here" : $"{Compass(best - p)}, about {Mathf.Max(10, Mathf.RoundToInt(bestD / 10f) * 10)} m";
            hud.Objective = $"{goal.Text}  ({Found}/{goal.Count})   {where}";
        }

        /// The open exit: the door itself once it's built, else the middle of its chunk.
        Vector3 ExitPos() => door != null ? door.transform.position : new Vector3((doorChunk.x + 0.5f) * MazeLayout.ChunkSize, 0f, (doorChunk.z + 0.5f) * MazeLayout.ChunkSize);

        string ExitHint()
        {
            var to = ExitPos() - player.transform.position;
            return $"\nThe way out is open: {Compass(to)}, about {Mathf.Max(10, Mathf.RoundToInt(new Vector2(to.x, to.z).magnitude / 10f) * 10)} m. Follow the compass.";
        }

        static string Compass(Vector3 d)
        {
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            string[] n = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return n[(Mathf.RoundToInt(a / 45f) % 8 + 8) % 8];
        }
    }
}
