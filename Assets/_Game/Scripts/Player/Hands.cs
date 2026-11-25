using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.Player
{
    /// What your hands are doing, shared by your first-person arms and your co-op suit (synced as one byte).
    /// Poses are written for one hand in eye space (x outward from the body's middle, y up, z forward, origin at
    /// the eyes) and mirrored for the left. The flashlight lives in the left hand; the right one does the work.
    public static class Hands
    {
        public const int None = 0, Interact = 1, Give = 2, Eat = 3, Push = 4, Pickup = 5, Drink = 6, Battery = 7,
            Bag = 8, Pry = 9, Read = 10, Camcorder = 11, Vault = 12, Ladder = 13, Slide = 14, Swim = 15;

        /// The local player's current action and when it started (Time.time).
        public static int Current { get; private set; }
        public static float Since { get; private set; }
        static int oneShot;
        static float oneShotAt;

        /// A short one-off motion (reaching, drinking...). Ongoing states (bag, ladder...) come from FirstPersonArms.
        public static void Play(int id) { oneShot = id; oneShotAt = Time.time; }

        public static float Duration(int id) => id switch
        {
            Interact => 0.45f, Give => 0.7f, Eat => 0.8f, Push => 0.45f, Pickup => 0.75f, Drink => 1.3f, Battery => 0.65f, _ => 0f,
        };

        /// Called once a frame with the ongoing state (or None); a one-shot still playing wins.
        public static void Resolve(int state)
        {
            if (oneShot != None && Time.time - oneShotAt < Duration(oneShot)) { Current = oneShot; Since = oneShotAt; return; }
            oneShot = None;
            if (state != Current) { Current = state; Since = Time.time; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = oneShot = None; Since = oneShotAt = 0f; }

        public struct Pose
        {
            public Vector3 Pos, Fingers, Dorsal;   // wrist position, wrist->knuckles, back of the hand
            public float Curl, Weight;              // 0 open .. 1 fist; 0 = leave the arm to the walk animation

            public Pose(Vector3 pos, Vector3 fingers, Vector3 dorsal, float curl, float weight = 1f)
            {
                Pos = pos; Fingers = fingers.normalized; Dorsal = dorsal.normalized; Curl = curl; Weight = weight;
            }

            public Pose Mirrored() => new Pose(M(Pos), M(Fingers), M(Dorsal), Curl, Weight);
            static Vector3 M(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        }

        /// Arm hanging at your side, out of the first-person view.
        public static Pose Down(bool left)
        {
            var p = new Pose(new Vector3(0.25f, -0.8f, 0.05f), new Vector3(0f, -1f, 0.2f), new Vector3(1f, 0f, 0f), 0.3f, 0f);
            return left ? p.Mirrored() : p;
        }

        static readonly Pose Torch = new Pose(new Vector3(0.17f, -0.25f, 0.42f), new Vector3(-0.3f, -0.1f, 1f), new Vector3(1f, 0.3f, 0.1f), 0.85f);
        public static readonly Pose BagGrip = new Pose(new Vector3(0.19f, -0.42f, 0.58f), new Vector3(-0.6f, 0.3f, 1f), new Vector3(1f, 0f, 0.4f), 0.7f);

        /// The pose for one hand. `cycle` drives looping or progress-driven motions (ladder rungs, vault progress).
        public static Pose Get(int id, float t, bool torch, bool left, float cycle)
        {
            Pose p;
            bool both = true;
            switch (id)
            {
                case Interact:
                {
                    float k = Bump(t, 0.45f);
                    p = new Pose(Vector3.Lerp(new Vector3(0.2f, -0.4f, 0.3f), new Vector3(0.1f, -0.1f, 0.58f), k), new Vector3(-0.1f, -0.2f, 1f), new Vector3(0f, 1f, 0.2f), Mathf.Lerp(0.2f, 0.7f, k));
                    both = false; break;
                }
                case Give:
                    p = new Pose(Vector3.Lerp(new Vector3(0.2f, -0.4f, 0.3f), new Vector3(0.08f, -0.2f, 0.55f), Bump(t, 0.7f)), new Vector3(-0.1f, 0f, 1f), new Vector3(0f, -1f, 0f), 0.2f);
                    both = false; break;
                case Eat:
                    p = new Pose(Vector3.Lerp(new Vector3(0.2f, -0.4f, 0.3f), new Vector3(0.03f, -0.1f, 0.13f), Bump(t, 0.8f)), new Vector3(-1f, 0.4f, 0.2f), new Vector3(0f, 0f, 1f), 0.6f);
                    both = false; break;
                case Push:
                    p = new Pose(Vector3.Lerp(new Vector3(0.2f, -0.25f, 0.3f), new Vector3(0.18f, -0.1f, 0.6f), Bump(t, 0.45f)), new Vector3(0f, 1f, 0.2f), new Vector3(0f, 0f, -1f), 0.05f);
                    break;
                case Pickup:   // what friends see; your own hand follows the actual item
                {
                    var at = t < 0.3f ? Vector3.Lerp(new Vector3(0.15f, -0.55f, 0.6f), new Vector3(0.12f, -0.2f, 0.42f), t / 0.3f)
                        : t < 0.45f ? new Vector3(0.12f, -0.2f, 0.42f)
                        : Vector3.Lerp(new Vector3(0.12f, -0.2f, 0.42f), new Vector3(0.05f, -0.6f, 0.2f), Mathf.Clamp01((t - 0.45f) / 0.3f));
                    p = new Pose(at, new Vector3(-0.4f, 0f, 1f), new Vector3(1f, 0f, 0.4f), 0.75f);
                    both = false; break;
                }
                case Drink:
                {
                    float k = Mathf.Clamp01((t - 0.3f) / 0.35f);
                    var at = t < 0.3f ? Vector3.Lerp(new Vector3(0.1f, -0.5f, 0.4f), new Vector3(0.1f, -0.15f, 0.34f), t / 0.3f)
                        : Vector3.Lerp(new Vector3(0.1f, -0.15f, 0.34f), new Vector3(0.04f, -0.06f, 0.14f), k);
                    p = new Pose(at, Vector3.Lerp(new Vector3(-0.4f, 0f, 1f), new Vector3(-1f, 0.3f, 0f), k), Vector3.Lerp(new Vector3(1f, 0f, 0.4f), new Vector3(0f, 0f, 1f), k), 0.75f);
                    both = false; break;
                }
                case Battery:
                {
                    var at = t < 0.3f ? Vector3.Lerp(new Vector3(0.1f, -0.5f, 0.4f), new Vector3(0.1f, -0.15f, 0.34f), t / 0.3f)
                        : Vector3.Lerp(new Vector3(0.1f, -0.15f, 0.34f), new Vector3(0f, -0.27f, 0.38f), Mathf.Clamp01((t - 0.3f) / 0.3f));
                    p = new Pose(at, new Vector3(-0.6f, 0f, 1f), new Vector3(1f, 0f, 0.3f), 0.8f);
                    both = false; break;
                }
                case Bag: p = BagGrip; break;
                case Pry:
                {
                    float pull = Mathf.Abs(Mathf.Sin(t * 5f));
                    p = new Pose(new Vector3(0.15f, -0.2f, 0.55f - 0.1f * pull), new Vector3(-0.3f, -0.2f, 1f), new Vector3(0.3f, 1f, 0f), 0.95f);
                    break;
                }
                case Read: p = new Pose(new Vector3(0.13f, -0.25f, 0.38f), new Vector3(-0.3f, 0.8f, 0.5f), new Vector3(1f, 0f, -0.3f), 0.35f); break;
                case Camcorder:
                    p = left ? new Pose(new Vector3(0.09f, -0.1f, 0.22f), new Vector3(-0.5f, 0.5f, 0.7f), new Vector3(1f, 0f, 0f), 0.7f)
                        : new Pose(new Vector3(0.07f, -0.06f, 0.2f), new Vector3(0f, 0.3f, 1f), new Vector3(1f, 0f, 0f), 0.7f);
                    break;
                case Vault:
                {
                    float k = Mathf.Clamp01(cycle);
                    p = new Pose(new Vector3(0.22f, -0.55f - 0.25f * k, 0.5f - 0.45f * k), new Vector3(0f, -0.3f, 1f), new Vector3(0f, 1f, 0.3f), 0.15f);
                    break;
                }
                case Ladder:
                {
                    float s = Mathf.Sin(cycle * Mathf.PI) * (left ? -1f : 1f);   // hand over hand
                    p = new Pose(new Vector3(0.2f, 0.1f + 0.18f * s, 0.35f), new Vector3(0f, 1f, 0.3f), new Vector3(0f, -0.3f, -1f), 0.85f);
                    break;
                }
                case Slide:
                    p = new Pose(new Vector3(0.35f, -0.85f, 0.3f), new Vector3(0f, -0.3f, 1f), new Vector3(0f, 1f, 0.2f), 0.1f);   // hand trailing on the floor
                    both = false; break;
                case Swim:
                {
                    // Breaststroke: reach, sweep out, pull in to the chest.
                    float c = Mathf.Repeat(cycle / 1.3f, 1f);
                    Vector3 reach = new Vector3(0.05f, -0.15f, 0.62f), sweep = new Vector3(0.42f, -0.2f, 0.4f), chest = new Vector3(0.12f, -0.32f, 0.15f);
                    var at = c < 0.45f ? Vector3.Lerp(reach, sweep, Mathf.SmoothStep(0f, 1f, c / 0.45f))
                        : c < 0.75f ? Vector3.Lerp(sweep, chest, Mathf.SmoothStep(0f, 1f, (c - 0.45f) / 0.3f))
                        : Vector3.Lerp(chest, reach, Mathf.SmoothStep(0f, 1f, (c - 0.75f) / 0.25f));
                    p = new Pose(at, new Vector3(0.2f, -0.1f, 1f), new Vector3(-0.3f, 1f, 0f), 0.1f);
                    break;
                }
                default: p = Down(false); both = false; break;
            }
            if (left && !both) p = torch ? Torch : Down(false);   // the flashlight hand carries on
            return left ? p.Mirrored() : p;
        }

        /// 0 -> 1 -> 0 over `time` seconds: reach out and back.
        static float Bump(float t, float time) => Mathf.Sin(Mathf.Clamp01(t / time) * Mathf.PI);

        /// A small handheld torch, built in code. Its forward is the beam.
        public static GameObject MakeFlashlight(Transform parent)
        {
            var root = new GameObject("Handheld Torch");
            root.transform.SetParent(parent, false);
            var body = World.HdMat.Make(new Color(0.08f, 0.08f, 0.09f), 0.45f, 0.6f);
            Part(root.transform, new Vector3(0f, 0f, -0.06f), new Vector3(0.034f, 0.07f, 0.034f), body);
            Part(root.transform, new Vector3(0f, 0f, 0.025f), new Vector3(0.046f, 0.02f, 0.046f), body);
            Part(root.transform, new Vector3(0f, 0f, 0.046f), new Vector3(0.04f, 0.002f, 0.04f), World.HdMat.Make(new Color(1f, 0.97f, 0.85f), 0.95f));
            return root;
        }

        static void Part(Transform root, Vector3 at, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // the cylinder's length along the beam
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// Eases a hand from one pose to the next so actions blend instead of snapping.
    public sealed class HandDriver
    {
        Hands.Pose cur;
        bool started;

        public Hands.Pose Step(Hands.Pose target, float dt, float rate)
        {
            if (!started) { started = true; cur = target; return cur; }
            float k = 1f - Mathf.Exp(-dt * rate);
            cur.Pos = Vector3.Lerp(cur.Pos, target.Pos, k);
            cur.Fingers = Vector3.Slerp(cur.Fingers, target.Fingers, k);
            cur.Dorsal = Vector3.Slerp(cur.Dorsal, target.Dorsal, k);
            cur.Curl = Mathf.Lerp(cur.Curl, target.Curl, k);
            cur.Weight = Mathf.Lerp(cur.Weight, target.Weight, k);
            return cur;
        }
    }

    /// Two-bone IK for one arm of the hazmat rig (upper arm, forearm, hand), plus finger curl. Reads the rest
    /// (T) pose once, so it works whatever axes the bones were exported with.
    public sealed class ArmChain
    {
        readonly Transform upper, fore, hand;
        readonly Quaternion upperOffset, handOffset, foreRest;
        readonly Vector3 curlAxis;   // in the hand's own space
        readonly float lenA, lenB;
        readonly Transform[] fingers;
        readonly Quaternion[] fingerRest;
        readonly float[] fingerBend;

        public Transform Hand => hand;
        public Transform Fore => fore;
        public Transform Upper => upper;
        public bool Ok => upper != null && fore != null && hand != null;

        /// Call while the rig is still in its rest pose. `facing` is where the body faces, `up` its up.
        public ArmChain(Transform root, string side, Vector3 facing, Vector3 up)
        {
            upper = Find(root, side + "Arm");
            fore = Find(root, side + "ForeArm");
            hand = Find(root, side + "Hand");
            if (!Ok) return;
            lenA = Vector3.Distance(upper.position, fore.position);
            lenB = Vector3.Distance(fore.position, hand.position);
            var dir = (fore.position - upper.position).normalized;
            // In the T pose, bending the elbow swings the forearm forward.
            upperOffset = Quaternion.Inverse(Quaternion.LookRotation(dir, facing)) * upper.rotation;
            foreRest = fore.localRotation;
            var mid = Find(root, side + "HandMiddle1");
            var f = mid != null ? (mid.position - hand.position).normalized : dir;
            handOffset = Quaternion.Inverse(Quaternion.LookRotation(f, up)) * hand.rotation;   // palms down in the T pose
            curlAxis = Quaternion.Inverse(hand.rotation) * Vector3.Cross(up, f);                // turns fingers toward the palm

            var list = new List<Transform>();
            var bends = new List<float>();
            foreach (var n in new[] { "Index", "Middle", "Ring", "Pinky", "Thumb" })
                for (int i = 1; i <= 3; i++)
                {
                    var b = Find(root, side + "Hand" + n + i);
                    if (b == null) continue;
                    list.Add(b);
                    bends.Add(n == "Thumb" ? (i == 1 ? 0f : 30f) : i == 1 ? 55f : i == 2 ? 75f : 50f);
                }
            fingers = list.ToArray();
            fingerBend = bends.ToArray();
            fingerRest = new Quaternion[fingers.Length];
            for (int i = 0; i < fingers.Length; i++) fingerRest[i] = fingers[i].localRotation;
        }

        /// Puts the wrist at `target` (world), the hand turned by `fingersDir` / `dorsal`, the elbow toward `pole`.
        /// weight < 1 blends from whatever pose the arm already has (the walk animation).
        public void Solve(Vector3 target, Vector3 fingersDir, Vector3 dorsal, Vector3 pole, float curl, float weight)
        {
            if (!Ok || weight <= 0.001f) return;
            Quaternion u0 = upper.rotation, f0 = fore.rotation, h0 = hand.rotation;
            var a = upper.position;
            var to = target - a;
            float d = Mathf.Clamp(to.magnitude, 0.05f, (lenA + lenB) * 0.999f);
            var dir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector3.down;
            var bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            bend.Normalize();
            float cos = Mathf.Clamp((lenA * lenA + d * d - lenB * lenB) / (2f * lenA * d), -1f, 1f);
            var elbow = a + (dir * cos + bend * Mathf.Sqrt(1f - cos * cos)) * lenA;
            var wrist = a + dir * d;
            var upDir = (elbow - a).normalized;
            var side = Vector3.ProjectOnPlane(wrist - elbow, upDir);
            if (side.sqrMagnitude < 1e-6f) side = -bend;

            var uIK = Quaternion.LookRotation(upDir, side) * upperOffset;
            upper.rotation = uIK;
            fore.localRotation = foreRest;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, wrist - fore.position) * fore.rotation;
            var fIK = fore.rotation;
            var hIK = Quaternion.LookRotation(fingersDir, dorsal) * handOffset;
            if (weight < 1f)
            {
                upper.rotation = Quaternion.Slerp(u0, uIK, weight);
                fore.rotation = Quaternion.Slerp(f0, fIK, weight);
                hand.rotation = Quaternion.Slerp(h0, hIK, weight);
            }
            else hand.rotation = hIK;

            var axis = hand.rotation * curlAxis;   // rotating about this axis keeps it fixed, so every joint shares it
            for (int i = 0; i < fingers.Length; i++)
            {
                fingers[i].localRotation = fingerRest[i];
                fingers[i].rotation = Quaternion.AngleAxis(fingerBend[i] * curl * weight, axis) * fingers[i].rotation;
            }
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
