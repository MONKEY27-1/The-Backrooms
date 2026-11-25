using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// What you see of a Hound (the owner's real one and a co-op guest's copy): the body from Art/build_hound.py,
    /// posed in code. A diagonal four-legged gait from how fast it moves; a crouched, trembling freeze while you
    /// stare it down; rearing up with the jaw dropped open when it lunges.
    public sealed class HoundLook : MonoBehaviour
    {
        public CreatureState State = CreatureState.Stalk;

        static Material skin, hair, teeth, claws, mouth;

        Transform model, hips, spine, chest, neck, head, jaw;
        Transform armL, armR, foreL, foreR, thighL, thighR, shinL, shinR;
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        float phase, rear, jawOpen, crouch;
        Vector3 lastPos;

        public static HoundLook Build(Transform root)
        {
            var look = root.gameObject.AddComponent<HoundLook>();
            var prefab = Resources.Load<GameObject>("Monsters/Hound");
            if (prefab == null) { Debug.LogError("Hound model missing: Resources/Monsters/Hound.fbx (run Art/build_hound.py)"); return look; }
            look.model = Instantiate(prefab, root, false).transform;
            look.model.name = "Hound Model";
            look.Setup();
            return look;
        }

        void Setup()
        {
            if (skin == null)
            {
                skin = HdMat.Make(new Color(0.78f, 0.74f, 0.68f), 0.35f);   // sickly, waxy grey
                hair = HdMat.DoubleSided(HdMat.Make(new Color(0.012f, 0.011f, 0.012f), 0.45f));
                teeth = HdMat.Make(new Color(0.62f, 0.56f, 0.42f), 0.5f);
                claws = HdMat.Make(new Color(0.06f, 0.05f, 0.045f), 0.6f);
                mouth = HdMat.Make(new Color(0.09f, 0.01f, 0.01f), 0.7f);
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                switch (r.name)
                {
                    case "Hair": r.sharedMaterial = hair; break;
                    case "TeethUpper": case "TeethLower": r.sharedMaterial = teeth; break;
                    case "ClawsL": case "ClawsR": r.sharedMaterial = claws; break;
                    case "HoundMouth": r.sharedMaterial = mouth; break;
                    default: r.sharedMaterial = skin; break;
                }
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            hips = Bone("Hips"); spine = Bone("Spine"); chest = Bone("Chest"); neck = Bone("Neck"); head = Bone("Head"); jaw = Bone("Jaw");
            armL = Bone("UpperArm.L"); armR = Bone("UpperArm.R"); foreL = Bone("Forearm.L"); foreR = Bone("Forearm.R");
            thighL = Bone("Thigh.L"); thighR = Bone("Thigh.R"); shinL = Bone("Shin.L"); shinR = Bone("Shin.R");
            phase = Random.value * 10f;
            lastPos = transform.position;
        }

        Transform Bone(string n)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == n) { rest[t] = t.localRotation; return t; }
            return null;
        }

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime;
            float speed = Flat(transform.position - lastPos).magnitude / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            float stride = Mathf.Clamp01(speed / 3.5f);
            phase += dt * (1.5f + speed * 2.6f);

            bool lunging = State == CreatureState.Lunge, held = State == CreatureState.Held;
            rear = Mathf.MoveTowards(rear, lunging ? 1f : 0f, dt * 7f);
            jawOpen = Mathf.MoveTowards(jawOpen, lunging ? 55f : held ? 8f + Mathf.PerlinNoise(Time.time * 9f, 1f) * 10f : State == CreatureState.Chase ? 14f : 0f, dt * 160f);
            crouch = Mathf.MoveTowards(crouch, held ? 1f : 0f, dt * 4f);
            float tremble = held ? (Mathf.PerlinNoise(Time.time * 22f, 3f) - 0.5f) * 6f : 0f;

            // Diagonal gait: front-left with hind-right, front-right with hind-left. + swings a limb back.
            float swing = Mathf.Sin(phase) * 32f * stride, lift = 30f * stride;
            Vector3 right = transform.right, fwd = transform.forward;
            Pose(spine, right, Mathf.Sin(phase * 2f) * 3f * stride - rear * 15f);
            Pose(chest, right, -rear * 25f + Mathf.Sin(Time.time * 1.7f + phase) * 1.5f + tremble);
            Pose(neck, right, rear * 10f + crouch * 12f);
            Pose(head, right, -rear * 25f + crouch * 10f + Mathf.Sin(phase * 2f) * 4f * stride, fwd, Mathf.Sin(Time.time * 0.8f + phase) * 6f);
            Pose(jaw, right, jawOpen);
            float reach = rear * 75f;   // arms thrown forward and up in the lunge
            Pose(armL, right, swing - reach);
            Pose(armR, right, -swing - reach);
            Pose(foreL, right, -Mathf.Max(0f, Mathf.Cos(phase)) * lift - reach * 0.3f);
            Pose(foreR, right, -Mathf.Max(0f, -Mathf.Cos(phase)) * lift - reach * 0.3f);
            Pose(thighL, right, -swing + rear * 20f);
            Pose(thighR, right, swing + rear * 20f);
            Pose(shinL, right, Mathf.Max(0f, -Mathf.Cos(phase)) * lift);
            Pose(shinR, right, Mathf.Max(0f, Mathf.Cos(phase)) * lift);
            model.localPosition = new Vector3(0f, -crouch * 0.12f + rear * 0.15f, 0f);
        }

        /// Rest pose, then turns about directions in world space (after the parent's pose).
        void Pose(Transform b, Vector3 axis, float degrees, Vector3 axis2 = default, float degrees2 = 0f)
        {
            if (b == null) return;
            b.localRotation = rest[b];
            b.rotation = Quaternion.AngleAxis(degrees, axis) * (degrees2 != 0f ? Quaternion.AngleAxis(degrees2, axis2) : Quaternion.identity) * b.rotation;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
