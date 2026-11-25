using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// What you see of a Clump (owner's and guests' alike): the knot of limbs from Art/build_clump.py, posed in code.
    /// Buried: sunk into the floor, only the long limb up, twitching. In a corner: hunched, limbs folded in, trembling.
    /// In the ceiling: not there at all. Crawling: every limb slapping down in turn, the long one curled over the top.
    /// Grabbing: the long limb stretched out to whoever it's holding, folding as it reels them in.
    public sealed class ClumpLook : MonoBehaviour
    {
        public CreatureState State = CreatureState.Lurk;
        /// Whoever the long limb holds.
        public Transform Target;
        /// Out of the floor (or down from the ceiling) and able to move.
        public bool Ready => rise >= 1f;

        static Material flesh, eye, iris, mouth, teeth;

        Transform model, core, longA, longB, longC;
        readonly List<(Transform a, Transform b, Vector3 dir, float offset)> limbs = new List<(Transform, Transform, Vector3, float)>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        Renderer[] renderers;
        float phase, rise = 1f, dropFrom;
        CreatureState shown = (CreatureState)255;
        Vector3 lastPos;

        public static ClumpLook Build(Transform root)
        {
            var look = root.gameObject.AddComponent<ClumpLook>();
            var prefab = Resources.Load<GameObject>("Monsters/Clump");
            if (prefab == null) { Debug.LogError("Clump model missing: Resources/Monsters/Clump.fbx (run Art/build_clump.py)"); return look; }
            look.model = Instantiate(prefab, root, false).transform;
            look.model.name = "Clump Model";
            look.Setup();
            return look;
        }

        void Setup()
        {
            if (flesh == null)
            {
                flesh = HdMat.Make(new Color(0.8f, 0.62f, 0.56f), 0.55f);   // sRGB: dry, pale flesh
                eye = HdMat.Make(new Color(0.9f, 0.78f, 0.74f), 0.85f);
                iris = HdMat.Make(new Color(0.1f, 0.08f, 0.07f), 0.85f);
                mouth = HdMat.Make(new Color(0.3f, 0.05f, 0.05f), 0.7f);
                teeth = HdMat.Make(new Color(0.85f, 0.8f, 0.68f), 0.5f);
            }
            renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = r.name.StartsWith("ClumpIris") ? iris : r.name.StartsWith("ClumpEye") ? eye : r.name == "ClumpMouth" ? mouth : r.name == "ClumpTeeth" ? teeth : flesh;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (!t.GetComponent<Renderer>()) { bones[t.name] = t; rest[t] = t.localRotation; }
            bones.TryGetValue("Core", out core);
            bones.TryGetValue("LongA", out longA); bones.TryGetValue("LongB", out longB); bones.TryGetValue("LongC", out longC);
            for (int i = 0; bones.TryGetValue($"Limb{i}a", out var a); i++)
            {
                bones.TryGetValue($"Limb{i}b", out var b);
                var dir = core != null ? a.position - core.position : a.position - transform.position;
                dir.y = 0f;
                limbs.Add((a, b, transform.InverseTransformDirection(dir.normalized), i * 2.4f));
            }
            phase = Random.value * 10f;
            lastPos = transform.position;
        }

        /// Comes down out of the ceiling.
        public void Drop() { rise = 0f; dropFrom = 2.2f; }

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime;
            if (State != shown)
            {
                if ((int)shown == 255 && State == CreatureState.Lurk) model.localPosition = Vector3.down * 0.95f;   // spawned already buried
                if (shown == CreatureState.Lurk && State == CreatureState.Chase) { rise = 0f; dropFrom = -0.95f; }   // tearing up out of the floor
                shown = State;
                bool hidden = State == CreatureState.Vent;
                foreach (var r in renderers) r.enabled = !hidden;
            }
            rise = Mathf.Min(1f, rise + dt / 0.7f);
            float speed = new Vector2(transform.position.x - lastPos.x, transform.position.z - lastPos.z).magnitude / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            float stride = Mathf.Clamp01(speed / 3f);
            phase += dt * (2f + speed * 3.5f);

            float y = 0f;
            if (State == CreatureState.Lurk) y = Mathf.MoveTowards(model.localPosition.y, -0.95f, dt * 0.8f);   // digs in
            else if (State == CreatureState.Stalk) y = -0.25f;
            else if (rise < 1f) y = Mathf.Lerp(dropFrom, 0f, rise * rise);
            model.localPosition = new Vector3(0f, y + Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.04f * stride, 0f);

            // Limbs: each slaps down in turn (tip lifts and swings), folded in while it hides.
            bool folded = State == CreatureState.Stalk;
            float tremble = folded ? (Mathf.PerlinNoise(Time.time * 15f, 0f) - 0.5f) * 6f : 0f;
            foreach (var (a, b, dir, off) in limbs)
            {
                var d = transform.TransformDirection(dir);
                var side = Vector3.Cross(Vector3.up, d);   // turning about this lifts the limb's tip
                float lift = Mathf.Max(0f, Mathf.Sin(phase + off)) * 28f * stride;
                Pose(a, side, -(folded ? 30f : 0f) - lift + tremble);
                Pose(b, side, folded ? 40f : lift * 0.5f);
            }
            if (core != null) Pose(core, transform.forward, Mathf.Sin(phase * 0.7f) * 4f * stride + tremble, transform.right, 0f);

            // The long limb: up out of the floor, curled over the top, or stretched out to its prey.
            Vector3 right = transform.right;
            if (State == CreatureState.Grab && Target != null && longA != null)
            {
                Pose(longA, right, 0f);
                var want = Target.position + Vector3.up * 0.4f - longA.position;
                longA.rotation = Quaternion.FromToRotation(longA.TransformDirection(Vector3.up), want.normalized) * longA.rotation;   // Unity bones point down their +Y
                float bend = Mathf.Clamp01(1f - want.magnitude / 2.6f) * 70f;
                Pose(longB, right, -bend);
                Pose(longC, right, -bend);
            }
            else if (State == CreatureState.Lurk)
            {
                Pose(longA, right, -75f + Mathf.Sin(Time.time * 1.3f) * 4f);   // a crooked arm up out of the floor
                Pose(longB, right, 40f + Mathf.Sin(Time.time * 2.1f) * 6f);
                Pose(longC, right, 55f + Mathf.PerlinNoise(Time.time * 3f, 2f) * 25f);   // hand hanging, fingers twitching
            }
            else
            {
                Pose(longA, right, -130f);   // curled back over the body like a tail
                Pose(longB, right, -60f + Mathf.Sin(phase) * 8f);
                Pose(longC, right, -50f);
            }
        }

        void Pose(Transform b, Vector3 axis, float degrees, Vector3 axis2 = default, float degrees2 = 0f)
        {
            if (b == null) return;
            b.localRotation = rest[b];
            b.rotation = Quaternion.AngleAxis(degrees, axis) * (degrees2 != 0f ? Quaternion.AngleAxis(degrees2, axis2) : Quaternion.identity) * b.rotation;
        }
    }
}
