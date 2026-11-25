using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// What you see of a Faceling (owner's and guests' alike). Adults and children wear clothes recoloured from
    /// their variant number, so a group is a mix of people; Evolved ones are ragged and carry a pipe; Shadow
    /// Facelings are the adult body gone pitch black, floating. Poses follow what it's doing: walking its route,
    /// "working" at a wall (hands moving on nothing), waving, or coming at you with its arms up.
    public sealed class FacelingLook : MonoBehaviour
    {
        public CreatureState State = CreatureState.Watch;
        /// 0 walker, 1 worker, 2 waver (the routine the brain gave it).
        public int Routine;
        public bool Waving;

        static readonly Color[] Shirts = { new Color(0.75f, 0.73f, 0.68f), new Color(0.42f, 0.5f, 0.62f), new Color(0.6f, 0.32f, 0.3f), new Color(0.45f, 0.5f, 0.38f), new Color(0.82f, 0.75f, 0.55f), new Color(0.3f, 0.3f, 0.34f), new Color(0.7f, 0.55f, 0.65f) };
        static readonly Color[] Pants = { new Color(0.22f, 0.26f, 0.36f), new Color(0.35f, 0.3f, 0.25f), new Color(0.18f, 0.18f, 0.2f), new Color(0.5f, 0.46f, 0.38f) };
        static readonly Color[] Skins = { new Color(0.92f, 0.76f, 0.65f), new Color(0.78f, 0.6f, 0.48f), new Color(0.58f, 0.42f, 0.32f), new Color(0.4f, 0.28f, 0.2f) };
        static readonly Color[] Hairs = { new Color(0.12f, 0.09f, 0.07f), new Color(0.4f, 0.28f, 0.15f), new Color(0.75f, 0.62f, 0.4f), new Color(0.55f, 0.55f, 0.55f), new Color(0.3f, 0.12f, 0.08f) };
        static Material shadow, pipe, eye, pit;

        CreatureKind kind;
        string file;   // which model: its baked maps are FacelingTex/<file>_<part>_color|normal
        Transform model;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        float phase;
        Vector3 lastPos;

        public static FacelingLook Build(Transform root, CreatureKind kind, int variant)
        {
            var look = root.gameObject.AddComponent<FacelingLook>();
            look.kind = kind;
            string file = kind == CreatureKind.FacelingChild ? "FacelingChild" : kind == CreatureKind.EvolvedFaceling ? "FacelingEvolved" : "Faceling";
            var prefab = Resources.Load<GameObject>("Monsters/" + file);
            if (prefab == null) { Debug.LogError($"Faceling model missing: Resources/Monsters/{file}.fbx (run Art/build_faceling.py)"); return look; }
            look.model = Instantiate(prefab, root, false).transform;
            look.model.name = file + " Model";
            look.file = file;
            look.Setup(variant);
            return look;
        }

        void Setup(int variant)
        {
            if (shadow == null)
            {
                shadow = HdMat.Make(new Color(0.01f, 0.01f, 0.012f), 0.05f);
                pipe = HdMat.Make(new Color(0.45f, 0.42f, 0.38f), 0.5f, 0.8f);
                eye = HdMat.Make(new Color(0.03f, 0.03f, 0.03f), 0.9f);
                pit = HdMat.Make(new Color(0.15f, 0.04f, 0.04f), 0.6f);
            }
            var rng = new System.Random(variant);
            bool evolved = kind == CreatureKind.EvolvedFaceling;
            // Per-Faceling colours (sRGB): one set of materials for this one body.
            // The baked maps (Art/build_faceling_hq*.py) hold the weave, pores and folds in near-white; the tint is this Faceling's.
            Material Part(string part, Color tint, float smooth) => HdMat.Make(tint, smooth, 0f,
                Resources.Load<Texture2D>($"Monsters/FacelingTex/{file}_{part}_color"), Resources.Load<Texture2D>($"Monsters/FacelingTex/{file}_{part}_normal"));
            var shirtM = Part("Shirt", evolved ? Shirts[rng.Next(Shirts.Length)] * 0.55f : Shirts[rng.Next(Shirts.Length)], 0.15f);
            var pantsM = Part("Pants", evolved ? new Color(0.25f, 0.22f, 0.18f) : Pants[rng.Next(Pants.Length)], 0.15f);
            var skinM = Part("Body", evolved ? new Color(0.72f, 0.62f, 0.5f) : Skins[rng.Next(Skins.Length)], 0.35f);
            var shoeM = Part("Shoes", new Color(0.18f, 0.15f, 0.13f), 0.3f);
            var hairM = Part("Hair", Hairs[rng.Next(Hairs.Length)], 0.3f);
            bool bald = rng.NextDouble() < 0.2;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "";
                    mats[i] = kind == CreatureKind.ShadowFaceling ? shadow
                        : n.StartsWith("FacelingShirt") ? shirtM : n.StartsWith("FacelingPants") ? pantsM : n.StartsWith("FacelingShoes") ? shoeM
                        : n.StartsWith("FacelingHair") ? hairM : n.StartsWith("FacelingPipe") ? pipe : n.StartsWith("FacelingEyeDot") ? eye
                        : n.StartsWith("FacelingPit") ? pit : skinM;
                }
                r.sharedMaterials = mats;
                if (r.name.EndsWith("Hair") && (bald || kind == CreatureKind.ShadowFaceling)) r.enabled = false;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
                if (kind == CreatureKind.ShadowFaceling) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!t.GetComponent<Renderer>()) { bones[t.name] = t; rest[t] = t.localRotation; }
            phase = (float)rng.NextDouble() * 10f;
            lastPos = transform.position;
        }

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime;
            float speed = new Vector2(transform.position.x - lastPos.x, transform.position.z - lastPos.z).magnitude / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            float stride = Mathf.Clamp01(speed / 2.2f);
            phase += dt * (1.5f + speed * 3f);
            Vector3 right = transform.right, fwd = transform.forward;
            bool angry = State == CreatureState.Chase || State == CreatureState.Lunge;
            bool evolved = kind == CreatureKind.EvolvedFaceling;

            float swing = Mathf.Sin(phase) * 30f * stride;
            Pose("Thigh.L", right, -swing); Pose("Thigh.R", right, swing);
            Pose("Shin.L", right, Mathf.Max(0f, Mathf.Sin(phase)) * 35f * stride);
            Pose("Shin.R", right, Mathf.Max(0f, -Mathf.Sin(phase)) * 35f * stride);
            Pose("Spine", right, evolved ? 22f : angry ? 10f : 0f);
            Pose("Head", right, evolved ? -15f : 0f, fwd, kind == CreatureKind.ShadowFaceling ? Mathf.Sin(Time.time * 0.7f) * 12f : 0f);

            if (angry)
            {
                // Arms up and swinging at you (the Evolved swing the pipe overhead).
                float flail = Mathf.Sin(Time.time * 9f) * 35f;
                Pose("UpperArm.L", right, evolved ? -40f : -95f + flail);
                Pose("UpperArm.R", right, evolved ? -150f + Mathf.Max(0f, Mathf.Sin(Time.time * 4f)) * 110f : -95f - flail);
                Pose("Forearm.L", right, -30f); Pose("Forearm.R", right, -30f);
            }
            else if (Waving)
            {
                Pose("UpperArm.L", right, -swing * 0.5f);
                Pose("UpperArm.R", fwd, -150f, right, 0f);   // arm up at the side
                Pose("Forearm.R", fwd, Mathf.Sin(Time.time * 6f) * 25f);   // the hand going side to side
            }
            else if (Routine == 1 && speed < 0.2f)
            {
                // "Working": hands out in front, fingers moving on a keyboard or a till that isn't there.
                Pose("UpperArm.L", right, -40f); Pose("UpperArm.R", right, -40f);
                Pose("Forearm.L", right, -45f + Mathf.Sin(Time.time * 11f) * 4f);
                Pose("Forearm.R", right, -45f + Mathf.Sin(Time.time * 13f + 1f) * 4f);
            }
            else
            {
                Pose("UpperArm.L", right, swing * 0.6f); Pose("UpperArm.R", right, -swing * 0.6f);
                Pose("Forearm.L", right, -8f); Pose("Forearm.R", right, -8f);
            }
            // Shadow Facelings float a little above the floor, bobbing.
            model.localPosition = kind == CreatureKind.ShadowFaceling ? new Vector3(0f, 0.3f + Mathf.Sin(Time.time * 1.6f + phase) * 0.06f, 0f) : Vector3.zero;
        }

        void Pose(string bone, Vector3 axis, float degrees, Vector3 axis2 = default, float degrees2 = 0f)
        {
            if (!bones.TryGetValue(bone, out var b)) return;
            b.localRotation = rest[b];
            b.rotation = Quaternion.AngleAxis(degrees, axis) * (degrees2 != 0f ? Quaternion.AngleAxis(degrees2, axis2) : Quaternion.identity) * b.rotation;
        }
    }
}
