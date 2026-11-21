using UnityEngine;

namespace Backrooms.World
{
    /// A boarded-up doorway. Pryable ones come off if you hold Interact long enough (it's loud).
    public sealed class BoardedDoor : MonoBehaviour
    {
        public const float PryTime = 1.6f, ForceTime = 2.6f;
        public bool Pryable;
        public int Id;
        public bool LockedDoor;   // a door you force rather than planks you pull off
        public string Exit;       // where a forced door leads, if anywhere
        public Transform Slab;    // the door leaf, swung open when forced; a crate's lid
        public bool Crate;        // a crate you pry the lid off (Level 1)

        /// What's inside a crate, decided by its id (so it's the same every time): "water", "battery", "note" or null.
        public string Loot
        {
            get
            {
                float r = WorldHash.Value((uint)Id, 3, 0, 0xC8A), keep = WorldHash.Value((uint)Id, 4, 0, 0xC8A);
                if (keep > Meta.Difficulty.Supplies + 0.1f) return null;
                return r < 0.42f ? "water" : r < 0.72f ? "battery" : r < 0.85f ? "note" : null;
            }
        }
        public float Progress { get; private set; }

        /// Called every frame Interact is held while looking at the boards. True when they come off.
        public bool Pry(float dt)
        {
            if (!Pryable) return false;
            Progress += dt / (LockedDoor ? ForceTime : Crate ? 1.1f : PryTime);
            return Progress >= 1f;
        }

        public void Release() => Progress = Mathf.Max(0f, Progress - Time.deltaTime);

        /// A forced door swings open (it stays that way: the game remembers it). If it leads somewhere, walking
        /// through it takes you there; otherwise there's only bare brick behind it.
        public void ForceOpen()
        {
            Pryable = false;
            Progress = 0f;
            if (Crate)
            {
                // The lid comes off and leans against the side; the crate stays solid.
                if (Slab != null) { Slab.localPosition = new Vector3(0.55f, 0.42f, 0f); Slab.localRotation = Quaternion.Euler(0f, 0f, -75f); }
                return;
            }
            foreach (var c in GetComponents<Collider>()) Destroy(c);
            if (Slab != null) Slab.localRotation = Quaternion.Euler(0f, 105f, 0f);
            if (string.IsNullOrEmpty(Exit)) return;
            var trigger = new GameObject("Exit to " + Exit);
            trigger.transform.SetParent(transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1f, 0.25f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 2f, 0.5f);
            trigger.AddComponent<LevelTrigger>().Target = Exit;
        }

        /// The planks across a doorway: uneven, some crooked, gaps you can see through, nail heads at the ends.
        public static void BuildPlanks(BoxMeshBuilder mesh, Matrix4x4 pose, float width, int seed)
        {
            var rng = new WorldRng((uint)seed * 2654435761u + 7u);
            int planks = 3 + rng.Range(0, 3);
            for (int i = 0; i < planks; i++)
            {
                float y = 0.35f + (1.55f / planks) * (i + 0.5f) + (rng.Value() - 0.5f) * 0.15f;
                float tilt = (rng.Value() - 0.5f) * (i % 2 == 0 ? 30f : 14f);
                float len = width + 0.35f + rng.Value() * 0.2f;
                var m = pose * Matrix4x4.TRS(new Vector3((rng.Value() - 0.5f) * 0.1f, y, 0.07f), Quaternion.Euler(0f, 0f, tilt), Vector3.one);
                // The Blender plank lies flat (length x, width z, thickness y): stand it up against the doorway.
                if (ModelLibrary.Add(mesh, "Plank_" + rng.Range(0, 3), m * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), new Vector3(len, 1f, 1f)))) continue;
                mesh.AddOrientedBox(m, Vector3.zero, new Vector3(len, 0.15f + rng.Value() * 0.05f, 0.03f), (int)PropPaint.Wood);
                foreach (float end in new[] { -len / 2 + 0.06f, len / 2 - 0.06f })
                    mesh.AddOrientedBox(m, new Vector3(end, 0f, 0.018f), new Vector3(0.018f, 0.018f, 0.008f), (int)PropPaint.Metal);
            }
        }
    }
}
