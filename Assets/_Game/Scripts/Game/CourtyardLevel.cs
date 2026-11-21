using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Game
{
    /// Level 188 "The Courtyard of Windows" (Backrooms Wiki; the model is hand-made for this game). An enclosed atrium
    /// walled with windows. Until its entities arrive with the creature phase: shadows stand behind windows at the edge
    /// of your sight and are gone when you look; a gramophone starts up somewhere (the wiki's signal to leave) and the
    /// courtyard leans on your mind while it plays; something bangs behind the locked doors; the odd objects (vases,
    /// books, the gramophone) are not to be approached. The window with light behind it is the way out.
    public sealed class Courtyard : MonoBehaviour
    {
        FirstPersonController player;
        PlayerStatus status;
        PlayerHud hud;
        Camera cam;
        ChunkStreamer world;
        ChunkStreamer.StaticInfo info;
        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<(GameObject go, float until)> shadows = new List<(GameObject, float)>();
        AudioSource record, knock;
        float nextShadow, nextRecord, recordEnds = -1f, nextBang, nextCurioWarn;
        Material black;
        readonly List<Mesh> madeMeshes = new List<Mesh>();

        /// How many doors were opened, and how many had a way through behind them (for checks).
        public int DoorsOpened { get; private set; }
        public int DoorsPassable { get; private set; }
        public int WallDoors { get; private set; }
        Material mahogany;
        Vector3 exitAt = Vector3.one * 1e5f;

        public void Init(FirstPersonController body, ChunkStreamer streamer, uint worldSeed, string back)
        {
            player = body;
            status = body.GetComponent<PlayerStatus>();
            hud = body.GetComponent<PlayerHud>();
            cam = body.GetComponentInChildren<Camera>();
            world = streamer;
            info = streamer.Static;
            if (info == null) return;
            Physics.SyncTransforms();   // the model was just re-centred; every cast below needs its colliders where it is
            black = HdMat.Make(new Color(0.01f, 0.01f, 0.012f), 0.05f);

            // The way out: one window you can reach from the ground, with warm light behind it.
            var low = info.Windows.FindAll(w => w.center.y < 3.2f && w.size.y > 0.6f);
            if (low.Count > 0)
            {
                var w = low[(int)(WorldHash.Value(worldSeed, 1, 88, 0x188) * low.Count) % low.Count];
                var exit = new GameObject("The window out");
                exitAt = w.center;
                exit.transform.position = new Vector3(w.center.x, Mathf.Max(1f, w.center.y), w.center.z);
                var box = exit.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = w.size + new Vector3(0.8f, 0.4f, 0.8f);
                exit.AddComponent<LevelTrigger>().Target = string.IsNullOrEmpty(back) ? "0" : back;
                var glow = new GameObject("Light behind it");
                glow.transform.SetParent(exit.transform, false);
                var away = new Vector3(w.center.x, 0f, w.center.z).normalized;   // the atrium is centred on the origin
                glow.transform.position = w.center + away * 0.8f;
                glow.AddHDLight(LightType.Point);
                var l = glow.GetComponent<Light>();
                l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
                l.intensity = 2500f;
                l.range = 7f;
                l.color = new Color(1f, 0.8f, 0.55f);
                // Warm light through the slats: a glowing panel just behind the glass.
                var pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(pane.GetComponent<Collider>());
                pane.name = "Light through the blinds";
                pane.transform.SetParent(exit.transform, true);
                pane.transform.position = w.center + away * 0.12f;
                pane.transform.rotation = Quaternion.LookRotation(away);
                pane.transform.localScale = new Vector3(Mathf.Max(w.size.x, w.size.z), w.size.y, 0.01f);
                pane.GetComponent<MeshRenderer>().sharedMaterial = streamer.Paints[(int)PropPaint.Warm];
                spawned.Add(exit);
            }

            StartCoroutine(WaysIn(worldSeed));

            record = new GameObject("Gramophone").AddComponent<AudioSource>();
            record.clip = Audio.ProceduralAudio.Gramophone();
            record.loop = true;
            record.spatialBlend = 0.6f;   // everywhere and nowhere
            record.volume = 0f;
            spawned.Add(record.gameObject);
            knock = new GameObject("Knocking").AddComponent<AudioSource>();
            knock.spatialBlend = 1f;
            knock.maxDistance = 40f;
            knock.rolloffMode = AudioRolloffMode.Linear;
            spawned.Add(knock.gameObject);
            float now = Time.time;
            nextShadow = now + 8f;
            nextRecord = now + Random.Range(70f, 140f);
            nextBang = now + Random.Range(25f, 50f);
            // Co-op: the record and the banging are the owner's game's to start; everyone hears them.
            Net.LevelSync.On("record", r => { r.ReadValueSafe(out float length); StartRecord(length); });
            Net.LevelSync.On("bang", r => { r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out int n); Bang(at, n); });
        }

        void OnDestroy()
        {
            foreach (var g in spawned) if (g != null) Destroy(g);
            foreach (var s in shadows) if (s.go != null) Destroy(s.go);
            foreach (var m in madeMeshes) if (m != null) Destroy(m);
            Net.LevelSync.Off("record");
            Net.LevelSync.Off("bang");
            if (world != null) world.Blackout = 0f;
        }

        /// Most doors here are locked (wiki); some aren't. Each door leaf in the merged door mesh is found, and of the
        /// ones with a real way through behind them, about two in five are cut out of the mesh and hung open on their
        /// hinges (which ones depends on the world). The rest stay shut and solid.
        void OpenSomeDoors(uint worldSeed)
        {
            int part = 0;
            foreach (var f in info.Root.GetComponentsInChildren<MeshFilter>())
                if (f.name.Contains("Doors") && f.sharedMesh != null && f.sharedMesh.isReadable) OpenSomeDoors(f, worldSeed + (uint)(part++ * 7919));
        }

        void OpenSomeDoors(MeshFilter mf, uint worldSeed)
        {
            var r = mf.GetComponent<MeshRenderer>();
            var mats = r.sharedMaterials;
            int leafSub = -1, knobSub = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (mats[i].name.Contains("Mahogany")) leafSub = i;
                if (mats[i].name.Contains("Brass")) knobSub = i;
            }
            if (leafSub < 0) return;
            mahogany = mats[leafSub];
            var mesh = mf.sharedMesh;
            var v = mesh.vertices;
            var toWorld = mf.transform.localToWorldMatrix;
            var subs = new List<int[]>();
            for (int i = 0; i < mesh.subMeshCount; i++) subs.Add(mesh.GetTriangles(i));

            // Leaves: the leaf submesh's triangles grouped into touching pieces (world-space bounds).
            var leaves = new List<(Bounds b, List<int> tris)>();
            var lt = subs[leafSub];
            for (int t = 0; t < lt.Length; t += 3)
            {
                var b = new Bounds(toWorld.MultiplyPoint3x4(v[lt[t]]), Vector3.zero);
                b.Encapsulate(toWorld.MultiplyPoint3x4(v[lt[t + 1]]));
                b.Encapsulate(toWorld.MultiplyPoint3x4(v[lt[t + 2]]));
                b.Expand(0.01f);
                int hit = -1;
                for (int k = 0; k < leaves.Count; k++) if (leaves[k].b.Intersects(b)) { hit = k; break; }
                if (hit < 0) leaves.Add((b, new List<int> { t }));
                else { var e = leaves[hit]; e.b.Encapsulate(b); e.tris.Add(t); leaves[hit] = e; }
            }
            var doorCollider = mf.GetComponent<MeshCollider>();
            var open = new List<int>();
            for (int k = 0; k < leaves.Count; k++)
            {
                var b = leaves[k].b;
                if (b.size.y < 1.6f || b.size.y > 3.2f) continue;   // not a door-sized piece
                bool thinX = b.size.x < b.size.z;
                var normal = thinX ? Vector3.right : Vector3.forward;
                // A way through: nothing but this door across the doorway, and floor on both sides.
                var mid = new Vector3(b.center.x, b.min.y + 1.0f, b.center.z);
                bool blocked = false;
                foreach (var h in Physics.RaycastAll(mid - normal * 1.2f, normal, 2.4f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider != doorCollider) { blocked = true; break; }
                if (blocked) continue;
                bool floorA = Physics.Raycast(mid - normal * 1f, Vector3.down, out var fa, 1.5f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(fa.point.y - b.min.y) < 0.4f;
                bool floorB = Physics.Raycast(mid + normal * 1f, Vector3.down, out var fb, 1.5f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(fb.point.y - b.min.y) < 0.4f;
                if (!floorA || !floorB) continue;
                DoorsPassable++;
                if (WorldHash.Value(worldSeed, k, 7, 0xD188) < 0.4f || (b.min.y < 1f && open.Count < 3 && WorldHash.Value(worldSeed, k, 8, 0xD188) < 0.7f)) open.Add(k);
            }
            if (open.Count == 0) return;

            // Which triangles leave the merged mesh: the open leaves and the knobs on them.
            var removed = new HashSet<(int sub, int tri)>();
            var moving = new List<(Bounds b, List<(int sub, int tri)> tris)>();
            foreach (int k in open)
            {
                var list = new List<(int, int)>();
                foreach (int t in leaves[k].tris) { removed.Add((leafSub, t)); list.Add((leafSub, t)); }
                if (knobSub >= 0)
                {
                    var near = leaves[k].b;
                    near.Expand(0.2f);
                    var kt = subs[knobSub];
                    for (int t = 0; t < kt.Length; t += 3)
                        if (near.Contains(toWorld.MultiplyPoint3x4(v[kt[t]]))) { removed.Add((knobSub, t)); list.Add((knobSub, t)); }
                }
                moving.Add((leaves[k].b, list));
            }

            // The door mesh without them.
            var rest = Object.Instantiate(mesh);
            rest.name = mesh.name + " (some open)";
            for (int i = 0; i < subs.Count; i++)
            {
                var keep = new List<int>(subs[i].Length);
                for (int t = 0; t < subs[i].Length; t += 3)
                    if (!removed.Contains((i, t))) { keep.Add(subs[i][t]); keep.Add(subs[i][t + 1]); keep.Add(subs[i][t + 2]); }
                rest.SetTriangles(keep, i);
            }
            madeMeshes.Add(rest);
            mf.sharedMesh = rest;
            if (doorCollider != null) doorCollider.sharedMesh = rest;

            // Each open leaf: its own mesh, hung on a hinge at one edge and swung away from the atrium.
            var uv = mesh.uv;
            var normals = mesh.normals;
            foreach (var (b, tris) in moving)
            {
                var map = new Dictionary<int, int>();
                var verts = new List<Vector3>();
                var nrm = new List<Vector3>();
                var uvs = new List<Vector2>();
                var bySub = new List<int>[2] { new List<int>(), new List<int>() };
                foreach (var (sub, t) in tris)
                    for (int c = 0; c < 3; c++)
                    {
                        int vi = subs[sub][t + c];
                        if (!map.TryGetValue(vi, out int ni))
                        {
                            ni = verts.Count;
                            map[vi] = ni;
                            verts.Add(v[vi]);
                            nrm.Add(vi < normals.Length ? normals[vi] : Vector3.up);
                            uvs.Add(vi < uv.Length ? uv[vi] : Vector2.zero);
                        }
                        bySub[sub == leafSub ? 0 : 1].Add(ni);
                    }
                var leafMesh = new Mesh { name = "Open Door" };
                leafMesh.SetVertices(verts);
                leafMesh.SetNormals(nrm);
                leafMesh.SetUVs(0, uvs);
                leafMesh.subMeshCount = 2;
                leafMesh.SetTriangles(bySub[0], 0);
                leafMesh.SetTriangles(bySub[1], 1);
                leafMesh.RecalculateBounds();
                leafMesh.RecalculateTangents();
                madeMeshes.Add(leafMesh);

                bool thinX = b.size.x < b.size.z;
                var along = thinX ? Vector3.forward : Vector3.right;   // across the doorway
                var hinge = new Vector3(b.center.x, b.min.y, b.center.z) - along * (Vector3.Scale(b.size, along).magnitude / 2f);
                var pivot = new GameObject("Door (unlocked)");
                pivot.transform.position = hinge;
                var leaf = new GameObject("Leaf");
                leaf.transform.SetParent(pivot.transform, true);
                leaf.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                leaf.transform.localScale = mf.transform.lossyScale;
                leaf.AddComponent<MeshFilter>().sharedMesh = leafMesh;
                leaf.AddComponent<MeshRenderer>().sharedMaterials = new[] { mats[leafSub], knobSub >= 0 ? mats[knobSub] : mats[leafSub] };
                leaf.AddComponent<MeshCollider>().sharedMesh = leafMesh;
                // Swing whichever way takes the leaf away from the middle of the atrium.
                var centre = new Vector3(b.center.x, 0f, b.center.z);
                var arm = centre - new Vector3(hinge.x, 0f, hinge.z);
                float best = 95f;
                foreach (float angle in new[] { 95f, -95f })
                {
                    var swung = new Vector3(hinge.x, 0f, hinge.z) + Quaternion.Euler(0f, angle, 0f) * arm;
                    if (swung.magnitude > centre.magnitude) { best = angle; break; }   // the atrium is centred on the origin
                }
                pivot.transform.rotation = Quaternion.Euler(0f, best, 0f);
                spawned.Add(pivot);
                DoorsOpened++;
            }
        }

        /// The atrium's walls are one sealed piece, so its ways into the building are door pairs: a door on the atrium
        /// wall and one on the corridor side behind it. Walk into one and you come out of the other.
        System.Collections.IEnumerator WaysIn(uint worldSeed)
        {
            // The model's colliders aren't all settled on the frame it loads (scans then miss walls), so wait a moment.
            yield return new WaitForSeconds(0.5f);
            Physics.SyncTransforms();
            OpenSomeDoors(worldSeed);
            Physics.SyncTransforms();   // the swung leaves
            DoorsIntoTheBuilding(worldSeed, true);
            if (WallDoors == 0) DoorsIntoTheBuilding(worldSeed, false);   // every world gets a way in
        }

        void DoorsIntoTheBuilding(uint worldSeed, bool strict)
        {
            var spots = new List<(Vector3 front, Vector3 frontN, Vector3 back, Vector3 backN)>();
            for (float a = 0f; a < 360f; a += 0.75f)
            {
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var eye = new Vector3(0f, 1.1f, 0f);
                if (!Physics.Raycast(eye, dir, out var wall, 30f, ~0, QueryTriggerInteraction.Ignore) || !wall.collider.name.Contains("AtriumWall") || Mathf.Abs(wall.normal.y) > 0.1f) continue;
                var n = wall.normal;
                // The wall is a single surface; the corridor is right behind it. It must be open there, with floor.
                var right = Vector3.Cross(Vector3.up, n);
                bool clear = true;
                foreach (float off in new[] { -0.4f, 0f, 0.4f })
                {
                    // A body's room, knees to head so no chair or desk sits in the way (waist up if nothing passes that).
                    var at = new Vector3(wall.point.x, 0f, wall.point.z) - n * 0.75f + right * off;
                    if (Physics.CheckCapsule(at + Vector3.up * (strict ? 0.35f : 0.85f), at + Vector3.up * 1.7f, 0.25f, ~0, QueryTriggerInteraction.Ignore)) clear = false;
                }
                if (!clear) continue;
                if (!Physics.Raycast(wall.point - n * 0.75f + Vector3.up * 0.5f, Vector3.down, out var floor, 3f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(floor.point.y) > 0.6f) continue;
                // Room for a door on the atrium side (no window or prop in the way).
                foreach (float off in new[] { -0.6f, 0.6f })
                    if (!Physics.Raycast(new Vector3(wall.point.x, 2.2f, wall.point.z) + n * 0.3f + right * off, -n, 0.5f)) clear = false;
                if (!clear) continue;
                foreach (var win in info.Windows)   // never under or through a window (the way out is one)
                {
                    var d = win.center - wall.point;
                    float halfWide = Mathf.Abs(win.extents.x * right.x) + Mathf.Abs(win.extents.z * right.z);
                    // On this wall, low enough to meet a door, and less than half a door plus a hand to one side.
                    if (win.min.y < 2.6f && Mathf.Abs(Vector3.Dot(d, n)) < 1f && Mathf.Abs(Vector3.Dot(d, right)) < halfWide + (strict ? 1.05f : 0.85f)) clear = false;   // glass bounds leave out the trim and open shutters
                }
                if (!clear) continue;
                if (Vector2.Distance(new Vector2(exitAt.x, exitAt.z), new Vector2(wall.point.x, wall.point.z)) < 3.5f) continue;   // the way out stays a window
                // The wall is thick and its courtyard face points inward (physics can't see it), so the door goes on
                // the nearest face found with backfaces on, not on the one the cast above hit.
                Physics.queriesHitBackfaces = true;
                var face = Physics.Raycast(eye, dir, out var seen, 30f, ~0, QueryTriggerInteraction.Ignore) ? seen.point : wall.point;
                Physics.queriesHitBackfaces = false;
                // Proud of the 8 cm skirting, standing on the raised walk along the wall.
                var front = new Vector3(face.x, 0f, face.z) + n * 0.1f;
                if (Physics.Raycast(front + n * 0.3f + Vector3.up * 1f, Vector3.down, out var walk, 2f, ~0, QueryTriggerInteraction.Ignore)) front.y = walk.point.y;
                var back = new Vector3(wall.point.x, floor.point.y, wall.point.z) - n * 0.06f;
                bool far = true;
                foreach (var sp in spots) if (Vector3.Distance(sp.front, front) < 6f) far = false;
                if (far) spots.Add((front, n, back, -n));
            }
            // Up to four of them, picked by the world.
            for (int i = 0; i < spots.Count && WallDoors < 4; i++)
            {
                if (WorldHash.Value(worldSeed, i, 9, 0xD189) > 0.6f && spots.Count - i > 4 - WallDoors) continue;
                var (front, fn, back, bn) = spots[i];
                var a = DoorOnWall(front, fn, "Door into the building");
                var b = DoorOnWall(back, bn, "Door into the courtyard");
                a.GetComponentInChildren<WallDoor>().Exit = back + bn * 1.2f;
                a.GetComponentInChildren<WallDoor>().Facing = bn;
                b.GetComponentInChildren<WallDoor>().Exit = front + fn * 1.2f;
                b.GetComponentInChildren<WallDoor>().Facing = fn;
                WallDoors++;
            }
        }

        GameObject DoorOnWall(Vector3 foot, Vector3 outward, string name)
        {
            var pose = Matrix4x4.TRS(foot + outward * 0.02f, Quaternion.LookRotation(outward), Vector3.one);
            var go = new GameObject(name);
            var mesh = new BoxMeshBuilder(Vector3.zero, world.Paints.Length);
            PropLibrary.DoorFrameOnly(pose, mesh, true);
            var m = mesh.ToMesh(name);
            madeMeshes.Add(m);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterials = world.Paints;
            // The leaf, a hand's width open, in the building's own mahogany.
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(leaf.GetComponent<Collider>());
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(go.transform, false);
            hinge.position = pose.MultiplyPoint3x4(new Vector3(-0.45f, 0f, 0.03f));
            hinge.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(0f, 30f, 0f);
            leaf.transform.SetParent(hinge, false);
            leaf.transform.localPosition = new Vector3(0.45f, 1.02f, 0f);
            leaf.transform.localScale = new Vector3(0.9f, 2.04f, 0.045f);
            if (mahogany != null) leaf.GetComponent<MeshRenderer>().sharedMaterial = mahogany;
            var trigger = new GameObject("Through");
            trigger.transform.SetParent(go.transform, false);
            trigger.transform.SetPositionAndRotation(pose.MultiplyPoint3x4(new Vector3(0f, 1f, 0.3f)), pose.rotation);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 2f, 0.6f);   // deep enough to step into before the body meets the wall
            trigger.AddComponent<WallDoor>();
            spawned.Add(go);
            return go;
        }

        Vector3 Viewport(Vector3 p) => cam.WorldToViewportPoint(p);

        void Update()
        {
            if (info == null || player == null) return;
            float now = Time.time, dt = Time.deltaTime;
            var p = player.transform.position;

            // Shadows behind windows, only ever at the edge of your sight.
            if (now > nextShadow && info.Windows.Count > 0)
            {
                nextShadow = now + Random.Range(5f, 12f);
                for (int tries = 0; tries < 12; tries++)
                {
                    var w = info.Windows[Random.Range(0, info.Windows.Count)];
                    var v = Viewport(w.center);
                    bool edge = v.z > 0f && v.y > 0f && v.y < 1f && (v.x is > 0.02f and < 0.22f || v.x is > 0.78f and < 0.98f);
                    if (!edge) continue;
                    var away = new Vector3(w.center.x, 0f, w.center.z).normalized;
                    var fig = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Destroy(fig.GetComponent<Collider>());
                    fig.name = "Shadow";
                    fig.transform.position = w.center + away * 0.6f + Vector3.down * 0.15f;
                    fig.transform.localScale = new Vector3(0.45f, Mathf.Min(0.85f, w.size.y * 0.6f), 0.3f);
                    fig.GetComponent<MeshRenderer>().sharedMaterial = black;
                    shadows.Add((fig, now + Random.Range(4f, 9f)));
                    break;
                }
            }
            for (int i = shadows.Count - 1; i >= 0; i--)
            {
                var (go, until) = shadows[i];
                if (go == null) { shadows.RemoveAt(i); continue; }
                var v = Viewport(go.transform.position);
                bool lookedAt = v.z > 0f && Mathf.Abs(v.x - 0.5f) < 0.12f && Mathf.Abs(v.y - 0.5f) < 0.15f;
                if (lookedAt)
                {
                    // Don't look at them directly.
                    if (status != null) status.Sanity = Mathf.Max(0f, status.Sanity - 0.04f);
                    UI.Captions.Say("[a blind snaps shut]");
                }
                if (lookedAt || now > until) { Destroy(go); shadows.RemoveAt(i); }
            }

            // The gramophone: the signal to leave. While it plays the place presses on you.
            if (Net.LevelSync.Decides && recordEnds < 0f && now > nextRecord)
            {
                float length = Random.Range(20f, 35f);
                StartRecord(length);
                Net.LevelSync.Send("record", w => w.WriteValueSafe(length));
            }
            if (recordEnds > 0f)
            {
                record.volume = Mathf.MoveTowards(record.volume, 0.45f * Meta.AudioMix.Ambience, dt * 0.3f);
                if (status != null) status.Sanity = Mathf.Max(0f, status.Sanity - dt / 120f * Meta.Difficulty.Sanity);
                world.Blackout = 0.25f + Mathf.PerlinNoise(now * 0.7f, 0f) * 0.25f;
                if (now > recordEnds)
                {
                    recordEnds = -1f;
                    nextRecord = now + Random.Range(120f, 240f);
                    world.Blackout = 0f;
                }
            }
            else
            {
                record.volume = Mathf.MoveTowards(record.volume, 0f, dt * 0.5f);
                if (record.volume <= 0f && record.isPlaying) record.Stop();
            }

            // Banging behind the doors (near one of the living).
            if (Net.LevelSync.Decides && now > nextBang)
            {
                nextBang = now + Random.Range(35f, 80f);
                var who = Players.Pick(Random.value);
                var near = who != null ? who.Position : p;
                var at = near + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * Random.Range(10f, 18f) + Vector3.up * Random.Range(1f, 12f);
                int n = Random.Range(3, 7);
                Bang(at, n);
                Net.LevelSync.Send("bang", w => { w.WriteValueSafe(at); w.WriteValueSafe(n); });
            }

            // The odd objects: being close to them is bad for you.
            foreach (var c in info.Curios)
            {
                if (Vector3.Distance(c, p + Vector3.up * 0.8f) > 1.6f) continue;
                if (status != null) status.Sanity = Mathf.Max(0f, status.Sanity - dt / 15f);
                if (now > nextCurioWarn) { nextCurioWarn = now + 10f; hud?.Flash("You shouldn't be this close to it.", 3f); }
                break;
            }
        }

        void StartRecord(float length)
        {
            recordEnds = Time.time + length;
            record.timeSamples = 0;
            record.Play();
            hud?.Flash("Music. A record has started playing somewhere. Leave.", 5f);
            UI.Captions.Say("[an old record starts playing]");
        }

        void Bang(Vector3 at, int n)
        {
            knock.transform.position = at;
            StartCoroutine(Bangs(n));
            UI.Captions.Say("[banging behind a door]");
        }

        System.Collections.IEnumerator Bangs(int n)
        {
            for (int i = 0; i < n; i++)
            {
                knock.pitch = Random.Range(0.35f, 0.45f);
                knock.PlayOneShot(Audio.ProceduralAudio.Clang(), 0.4f * Meta.AudioMix.Creatures);
                yield return new WaitForSeconds(0.55f + Random.value * 0.15f);
            }
        }
    }

    /// Very rarely, in the ordinary levels, a lit window is set in a wall ahead where there wasn't one. Climb through
    /// it and you're in the courtyard. It's gone a couple of minutes later (Backrooms Wiki, Level 188).
    public sealed class WindowLure : MonoBehaviour
    {
        FirstPersonController player;
        ChunkStreamer world;
        GameObject window;
        float next, until;

        public void Init(FirstPersonController body, ChunkStreamer streamer)
        {
            player = body;
            world = streamer;
            next = Time.time + Random.Range(300f, 900f);
            Net.LevelSync.On("window", r => { r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out Vector3 normal); Make(at, normal); });
        }

        void OnDestroy()
        {
            if (window != null) Destroy(window);
            Net.LevelSync.Off("window");
        }

        void Update()
        {
            if (player == null) return;
            if (window != null) { if (Time.time > until) { Destroy(window); window = null; } return; }
            if (Time.time < next || !Net.LevelSync.Decides) return;
            next = Time.time + Random.Range(420f, 900f);
            if (Random.value > 0.4f) return;
            var who = Players.Pick(Random.value);   // ahead of one of the living
            if (who == null) return;
            var ahead = new Vector3(who.Forward.x, 0f, who.Forward.z).normalized;
            var eye = who.Position + Vector3.up * 1.4f;
            var dir = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * ahead;
            if (!Physics.Raycast(eye, dir, out var wall, 14f, ~0, QueryTriggerInteraction.Ignore) || wall.distance < 5f || Mathf.Abs(wall.normal.y) > 0.1f) return;
            var at = wall.point;
            var normal = wall.normal;
            Make(at, normal);
            Net.LevelSync.Send("window", w => { w.WriteValueSafe(at); w.WriteValueSafe(normal); });
        }

        void Make(Vector3 at, Vector3 normal)
        {
            if (window != null) Destroy(window);
            var pose = Matrix4x4.TRS(at + normal * 0.02f, Quaternion.LookRotation(normal), Vector3.one);
            window = new GameObject("A Window");
            var mesh = new BoxMeshBuilder(Vector3.zero, world.Paints.Length);
            void Box(PropPaint paint, float x, float y, float z, float sx, float sy, float sz) => mesh.AddOrientedBox(pose, new Vector3(x, y, z), new Vector3(sx, sy, sz), (int)paint);
            Box(PropPaint.White, 0f, 0f, 0.02f, 1.1f, 1.3f, 0.04f);          // frame
            Box(PropPaint.Warm, 0f, 0f, 0.025f, 0.96f, 1.16f, 0.01f);       // light behind the blinds
            for (int i = 0; i < 9; i++) Box(PropPaint.White, 0f, -0.52f + i * 0.13f, 0.035f, 0.94f, 0.03f, 0.01f);   // slats
            Box(PropPaint.White, 0f, -0.68f, 0.07f, 1.2f, 0.05f, 0.14f);     // sill
            window.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh("Window");
            window.AddComponent<MeshRenderer>().sharedMaterials = world.Paints;
            var trigger = new GameObject("Climb through");
            trigger.transform.SetParent(window.transform, false);
            trigger.transform.SetPositionAndRotation(at + normal * 0.35f + Vector3.down * 0.4f, pose.rotation);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1f, 2f, 0.5f);
            trigger.AddComponent<LevelTrigger>().Target = "188";
            until = Time.time + 150f;
        }
    }

    /// One side of a door through the courtyard's wall: walk in and you come out of its partner on the other side.
    public sealed class WallDoor : MonoBehaviour
    {
        public Vector3 Exit, Facing;
        static float lastUse;

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out FirstPersonController player) || Mathf.Abs(Time.time - lastUse) < 0.5f) return;   // Abs: the clock restarts while statics survive (no domain reload)
            lastUse = Time.time;
            player.Teleport(Exit + Vector3.up * 0.05f, Quaternion.LookRotation(Facing).eulerAngles.y);
        }
    }
}
