using System.Collections.Generic;
using Backrooms.Net;
using Backrooms.World;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Backrooms.Game
{
    /// A short-lived physical copy of the hazmat suit left where a player died.
    public static class DeathRagdoll
    {
        static readonly Queue<GameObject> bodies = new Queue<GameObject>();
        static Material blood;

        public static void Spawn(Vector3 position, Quaternion rotation, Vector3 impulse = default)
        {
            while (bodies.Count >= 32)
            {
                var oldest = bodies.Dequeue();
                if (oldest != null) Object.Destroy(oldest);
            }
            var prefab = Resources.Load<GameObject>("CoopAvatar");
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, position, rotation);
            go.name = "Dead wanderer";
            foreach (var net in go.GetComponentsInChildren<NetworkObject>(true)) Object.Destroy(net);
            foreach (var net in go.GetComponentsInChildren<NetworkTransform>(true)) Object.Destroy(net);
            foreach (var avatar in go.GetComponentsInChildren<CoopAvatar>(true)) Object.Destroy(avatar);
            foreach (var rig in go.GetComponentsInChildren<SuitRig>(true)) rig.EnableRagdoll();
            foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            var bones = new List<(Transform bone, Rigidbody body)>();
            foreach (string name in new[] { "Hips", "Spine1", "Head", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm" })
            {
                var bone = FindDeep(go.transform, name);
                if (bone == null) continue;
                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = name == "Hips" ? 10f : name == "Spine1" ? 7f : name == "Head" ? 3f : 2f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                bones.Add((bone, rb));
                AddBoneCollider(bone, name);
            }

            foreach (var part in bones)
            {
                if (part.bone.name == "Hips") continue;
                var parent = part.bone.parent;
                while (parent != null && !parent.TryGetComponent<Rigidbody>(out _)) parent = parent.parent;
                if (parent == null || !parent.TryGetComponent(out Rigidbody connected)) continue;
                var joint = part.bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = connected;
                joint.enableProjection = true;
                joint.projectionDistance = 0.1f;
                joint.projectionAngle = 15f;
            }

            var hips = FindDeep(go.transform, "Hips");
            if (hips != null && hips.TryGetComponent(out Rigidbody torso))
                torso.AddForce((impulse.sqrMagnitude > 0.01f ? impulse : (rotation * Vector3.back + Vector3.up * 0.35f) * 1.8f), ForceMode.Impulse);
            MakeBlood(position);
            bodies.Enqueue(go);
            Object.Destroy(go, 150f);
        }

        static void AddBoneCollider(Transform bone, string name)
        {
            Transform child = null;
            float longest = 0f;
            foreach (Transform t in bone)
            {
                if (!IsBodyBone(t.name)) continue;
                float distance = Vector3.Distance(bone.position, t.position);
                if (distance > longest) { longest = distance; child = t; }
            }
            if (child == null)
            {
                foreach (Transform t in bone)
                {
                    float distance = Vector3.Distance(bone.position, t.position);
                    if (distance > longest) { longest = distance; child = t; }
                }
            }
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            float radius = name == "Head" ? 0.12f : name == "Hips" || name == "Spine1" ? 0.18f : 0.085f;
            if (child == null || longest < 0.025f)
            {
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = radius * 2f;
                return;
            }
            Vector3 local = bone.InverseTransformPoint(child.position);
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2f, local.magnitude + radius);
            capsule.center = local * 0.5f;
            capsule.transform.localRotation *= Quaternion.FromToRotation(Vector3.up, local.normalized);
        }

        static bool IsBodyBone(string name) => name == "Spine1" || name == "Head" || name == "LeftUpLeg" || name == "RightUpLeg"
            || name == "LeftLeg" || name == "RightLeg" || name == "LeftFoot" || name == "RightFoot"
            || name == "LeftArm" || name == "RightArm" || name == "LeftForeArm" || name == "RightForeArm";

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static void MakeBlood(Vector3 position)
        {
            if (blood == null) blood = Backrooms.World.HdMat.Make(new Color(0.12f, 0.004f, 0.008f), 0.15f);
            Vector3 ground = position;
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                ground = hit.point + Vector3.up * 0.012f;
            var stain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stain.name = "Blood stain";
            stain.transform.position = ground;
            stain.transform.localScale = new Vector3(0.8f, 0.006f, 0.65f);
            var collider = stain.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            stain.GetComponent<Renderer>().sharedMaterial = blood;
            Object.Destroy(stain, 150f);
        }
    }
}
