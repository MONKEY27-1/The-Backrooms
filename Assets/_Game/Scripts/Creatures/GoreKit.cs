using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms.Creatures
{
    /// Blender Gore Kit (GAME_PLAN §24, Stages B & C): torn stumps, guts, ribs, meat chunks,
    /// hearts, opened bellies, severed spines, flayed skulls, and cocoons.
    /// Strictly excluded from child Facelings.
    public static class GoreKit
    {
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material[]> matCache = new Dictionary<string, Material[]>();
        static readonly List<GameObject> activeBodies = new List<GameObject>();
        const int MaxPersistentBodies = 10;

        static GameObject kitPrefab;

        static void EnsureLoaded()
        {
            if (kitPrefab != null) return;
            kitPrefab = Resources.Load<GameObject>("Monsters/GoreKit");
            if (kitPrefab == null)
            {
                Debug.LogWarning("[GoreKit] Resources/Monsters/GoreKit.fbx not found; building runtime fallback.");
                return;
            }

            foreach (var mf in kitPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null && !meshCache.ContainsKey(mf.gameObject.name))
                    meshCache[mf.gameObject.name] = mf.sharedMesh;
            }
        }

        public static Mesh GetMesh(string name)
        {
            EnsureLoaded();
            meshCache.TryGetValue(name, out var m);
            return m;
        }

        public static Material[] GetMaterials(string name)
        {
            if (matCache.TryGetValue(name, out var cached) && cached != null && cached.Length > 0 && cached[0] != null)
                return cached;

            if (name == "GoreSkullTeeth" || name == "GoreFaceTeeth")
            {
                var toothMat = HdMat.Make(new Color(0.82f, 0.75f, 0.55f), 0.45f);
                return matCache[name] = new[] { toothMat };
            }

            if (name == "GoreFaceEye")
            {
                var sclera = HdMat.Make(new Color(0.6f, 0.37f, 0.27f), 0.88f);
                var iris = HdMat.Make(new Color(0.05f, 0.035f, 0.03f), 0.9f);
                return matCache[name] = new[] { sclera, iris };
            }

            string baseName = name;
            var col = Resources.Load<Texture2D>($"Monsters/GoreTex/{baseName}_color");
            var nrm = Resources.Load<Texture2D>($"Monsters/GoreTex/{baseName}_normal");

            float smooth = name.Contains("Ribs") || name.Contains("Skull") ? 0.55f
                : name.Contains("Hide") ? 0.6f
                : name.Contains("Cocoon") ? 0.7f : 0.78f;

            var mat = HdMat.Make(Color.white, smooth, 0f, col, nrm);
            if (name.Contains("Hide") || name.Contains("Cocoon")) HdMat.DoubleSided(mat);
            return matCache[name] = new[] { mat };
        }

        /// Instantiates a single gore piece as a GameObject with mesh and HDRP materials.
        public static GameObject CreatePiece(string pieceName, Vector3 pos, Quaternion rot, Transform parent = null, float scale = 1f)
        {
            var mesh = GetMesh(pieceName);
            if (mesh == null) return null;

            var go = new GameObject(pieceName);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * scale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = GetMaterials(pieceName);
            mr.shadowCastingMode = ShadowCastingMode.On;

            // Composite pieces: skull teeth, face eyes/teeth
            if (pieceName == "GoreSkull")
            {
                CreatePiece("GoreSkullTeeth", pos, rot, go.transform, 1f);
            }
            else if (pieceName == "GoreFace")
            {
                CreatePiece("GoreFaceTeeth", pos, rot, go.transform, 1f);
                CreatePiece("GoreFaceEye", pos, rot, go.transform, 1f);
            }

            return go;
        }

        /// Attaches a bleeding flesh stump to a severed bone.
        public static GameObject AttachStump(Transform bone, Vector3 localOffset, Quaternion localRotation, float scale = 1f)
        {
            if (bone == null) return null;
            var stump = CreatePiece("GoreStump", bone.position, bone.rotation, bone, scale);
            if (stump != null)
            {
                stump.transform.localPosition = localOffset;
                stump.transform.localRotation = localRotation;
            }
            return stump;
        }

        /// Drops physical gore chunks around an explosion, maul or dismemberment site.
        public static void DropMeatChunks(Vector3 center, int count = 4, float spreadSpeed = 3.5f)
        {
            string[] chunkTypes = { "GoreChunkA", "GoreChunkB", "GoreChunkC", "GoreHeart" };
            for (int i = 0; i < count; i++)
            {
                string type = chunkTypes[Random.Range(0, chunkTypes.Length)];
                Vector3 spawnPos = center + Vector3.up * Random.Range(0.2f, 0.9f) + Random.insideUnitSphere * 0.25f;
                var chunk = CreatePiece(type, spawnPos, Random.rotation, null, Random.Range(0.85f, 1.25f));
                if (chunk == null) continue;

                var col = chunk.AddComponent<SphereCollider>();
                col.radius = 0.08f;
                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 2f;
                rb.linearVelocity = (Random.onUnitSphere + Vector3.up * 0.8f).normalized * Random.Range(spreadSpeed * 0.4f, spreadSpeed);
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                TrackBody(chunk);
            }
        }

        /// Dismembers an adult humanoid body (player or adult NPC).
        /// CHILD FACELINGS ARE EXPLICITLY AND FULLY EXCLUDED.
        public static void DismemberCorpse(Transform root, bool isChild = false, string monsterCause = null)
        {
            // ABSOLUTE RULE: Gore stays on adults. Child Facelings are completely excluded.
            if (isChild || root == null) return;

            Vector3 pos = root.position;
            Blood.Burst(pos);
            DropMeatChunks(pos + Vector3.up * 0.6f, Random.Range(3, 6));

            // Attach torn belly and guts
            var spine = Deep(root, "Spine") ?? Deep(root, "Chest") ?? Deep(root, "Hips");
            if (spine != null)
            {
                var belly = CreatePiece("GoreBelly", spine.position + spine.forward * 0.08f, spine.rotation, spine, 1f);
                if (belly != null) belly.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                // Guts pooling out onto the floor
                CreatePiece("GoreGuts", pos + Vector3.up * 0.02f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null, 1f);
            }

            // Sever limb or head based on monster style
            if (monsterCause == "Partygoer")
            {
                // Carved bloody smile & picked ribs
                var head = Deep(root, "Head");
                if (head != null)
                {
                    head.localScale = Vector3.zero;
                    CreatePiece("GoreFace", head.position, head.rotation, head.parent, 1f);
                }
            }
            else if (monsterCause == "Hound")
            {
                // Gnawed forearm & severed arm
                var leftArm = Deep(root, "UpperArm.L") ?? Deep(root, "LeftArm");
                var leftFore = Deep(root, "Forearm.L") ?? Deep(root, "LeftForeArm");
                if (leftFore != null && leftArm != null)
                {
                    leftFore.localScale = Vector3.zero;
                    AttachStump(leftArm, Vector3.forward * 0.22f, Quaternion.identity);
                    CreatePiece("GoreArm", pos + root.right * 0.6f + Vector3.up * 0.05f, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f), null, 1f);
                }
            }
            else if (monsterCause == "SkinStealer")
            {
                // Flayed skull & ribs
                var head = Deep(root, "Head");
                if (head != null)
                {
                    head.localScale = Vector3.zero;
                    CreatePiece("GoreSkull", head.position, head.rotation, head.parent, 1f);
                }
                CreatePiece("GoreRibs", pos + Vector3.up * 0.05f, root.rotation, null, 1f);
            }
            else
            {
                // General severe trauma: severed spine hanging from hips
                var hips = Deep(root, "Hips");
                if (hips != null)
                {
                    CreatePiece("GoreSpine", hips.position, hips.rotation, hips, 1f);
                }
            }
        }

        /// Spawns a persistent dead victim (e.g. fallen co-op friend or found victim).
        public static GameObject SpawnVictim(Vector3 pos, Quaternion rot, int suitIndex = 0, string monsterCause = null)
        {
            var prefab = Resources.Load<GameObject>("Models/PlayerHazmat/Player_Hazmat");
            if (prefab == null) prefab = Resources.Load<GameObject>("Models/Player_HazmatArms");
            var victim = prefab != null ? Object.Instantiate(prefab, pos, rot) : new GameObject("Victim");
            victim.name = "GoreVictim";

            // Suit material
            var mat = Resources.Load<Material>($"Suits/Suit_{Mathf.Clamp(suitIndex, 0, 3)}");
            if (mat != null)
            {
                foreach (var r in victim.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = mat;
            }

            // Lay victim on the ground
            victim.transform.position = pos + Vector3.up * 0.1f;
            victim.transform.rotation = rot * Quaternion.Euler(85f, Random.Range(-25f, 25f), Random.Range(-15f, 15f));

            // Dismember body with gore attachments
            DismemberCorpse(victim.transform, isChild: false, monsterCause);

            // Blood pool underneath
            Blood.Burst(pos);

            TrackBody(victim);
            return victim;
        }

        static void TrackBody(GameObject go)
        {
            if (go == null) return;
            activeBodies.Add(go);
            if (activeBodies.Count > MaxPersistentBodies)
            {
                var oldest = activeBodies[0];
                activeBodies.RemoveAt(0);
                if (oldest != null) Object.Destroy(oldest);
            }
        }

        public static void ClearBodies()
        {
            foreach (var b in activeBodies) if (b != null) Object.Destroy(b);
            activeBodies.Clear();
        }

        static Transform Deep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name || t.name.EndsWith(name)) return t;
            foreach (Transform c in t)
            {
                var f = Deep(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
