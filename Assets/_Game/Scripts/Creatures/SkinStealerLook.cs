using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// What you see of a Skin-Stealer (the owner's real one and a co-op guest's copy).
    /// Disguised: a lost wanderer (Art/build_skinstealer.py), or in co-op a friend's hazmat suit gone slightly off.
    /// The tells are in the movement — it never breathes, its knees bend a little backwards at each step, its arms
    /// barely swing, it turns its whole body instead of its head — and in the clear fluid that drips from the wet
    /// patch on its side and leaves a trail. Once it has shed the skin: the tall, pale-yellow thing underneath.
    public sealed class SkinStealerLook : MonoBehaviour
    {
        public CreatureState State = CreatureState.Disguise;
        /// Copying a friend's suit (index), or -1 for the wanderer.
        public int Suit = -1;

        // Colours are sRGB (Unity converts them to linear): pick them as they should look.
        static Material jeans, jacket, skin, shoes, wound, hair, pack, yellow, eye, shred, clearDrop;

        Transform disguise, trueForm;
        Net.SuitRig suitRig;
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        bool revealed;
        float phase, nextDrop, twitchAt, twitch;
        Vector3 lastPos;

        public static SkinStealerLook Build(Transform root, int suit)
        {
            var look = root.gameObject.AddComponent<SkinStealerLook>();
            look.Suit = suit;
            look.Setup(root);
            return look;
        }

        void Setup(Transform root)
        {
            if (jeans == null)
            {
                jeans = HdMat.Make(new Color(0.32f, 0.38f, 0.5f), 0.15f);
                jacket = HdMat.Make(new Color(0.48f, 0.48f, 0.32f), 0.2f);
                skin = HdMat.Make(new Color(0.86f, 0.67f, 0.55f), 0.35f);
                shoes = HdMat.Make(new Color(0.13f, 0.12f, 0.11f), 0.3f);
                wound = HdMat.Make(new Color(0.16f, 0.15f, 0.13f), 0.95f);   // soaked: mirror-wet
                hair = HdMat.Make(new Color(0.16f, 0.12f, 0.09f), 0.25f);
                pack = HdMat.Make(new Color(0.55f, 0.18f, 0.12f), 0.25f);
                yellow = HdMat.Make(new Color(0.86f, 0.78f, 0.48f), 0.7f);    // pale yellow, slick
                eye = HdMat.Make(new Color(0.9f, 0.9f, 0.86f), 0.6f);
                shred = HdMat.DoubleSided(HdMat.Make(new Color(0.55f, 0.12f, 0.08f), 0.85f));
                clearDrop = HdMat.Transparent(HdMat.Make(new Color(0.85f, 0.9f, 0.9f, 0.35f), 0.98f));
            }
            if (Suit >= 0) disguise = SuitDisguise(root, Suit);
            if (disguise == null) disguise = Model(root, "SkinStealerWanderer", "Wanderer");
            trueForm = Model(root, "SkinStealer", "Stealer");
            if (trueForm != null) trueForm.gameObject.SetActive(false);
            phase = Random.value * 10f;
            lastPos = transform.position;
        }

        Transform Model(Transform root, string file, string prefix)
        {
            var prefab = Resources.Load<GameObject>("Monsters/" + file);
            if (prefab == null) { Debug.LogError($"Skin-Stealer model missing: Resources/Monsters/{file}.fbx (run Art/build_skinstealer.py)"); return null; }
            var m = Instantiate(prefab, root, false).transform;
            m.name = prefix + " Model";
            foreach (var r in m.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Pick(r.name, mats[i] != null ? mats[i].name : "");
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            foreach (var t in m.GetComponentsInChildren<Transform>(true))
                if (!t.GetComponent<Renderer>()) { bones[prefix + t.name] = t; rest[t] = t.localRotation; }
            return m;
        }

        // Blender material names come through on import; map each to ours.
        static Material Pick(string renderer, string imported)
        {
            if (imported.StartsWith("WandererJeans")) return jeans;
            if (imported.StartsWith("WandererJacket")) return jacket;
            if (imported.StartsWith("WandererShoes")) return shoes;
            if (imported.StartsWith("WandererWound")) return wound;
            if (imported.StartsWith("WandererHair")) return hair;
            if (imported.StartsWith("WandererPack")) return pack;
            if (imported.StartsWith("WandererSkin")) return skin;
            if (imported.StartsWith("StealerEye")) return eye;
            if (imported.StartsWith("StealerShred")) return shred;
            return renderer.StartsWith("Wanderer") ? skin : yellow;
        }

        /// A friend's suit, its colour a shade off (the skin doesn't sit quite right).
        Transform SuitDisguise(Transform root, int suit)
        {
            var go = CreatureBodies.HazmatDisguise(root);
            if (go == null) return null;
            var src = Resources.Load<Material>("Suits/Suit_" + Mathf.Clamp(suit, 0, Net.CoopAvatar.SuitNames.Length - 1));
            if (src != null)
            {
                var off = new Material(src);
                if (off.HasProperty("_BaseColor")) off.SetColor("_BaseColor", off.GetColor("_BaseColor") * new Color(0.86f, 0.84f, 0.8f));
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.sharedMaterial = off;
            }
            suitRig = go.GetComponentInChildren<Net.SuitRig>(true);
            return go.transform;
        }

        Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;

        void LateUpdate()
        {
            if (disguise == null && trueForm == null) return;
            float dt = Time.deltaTime;
            if (!revealed && State != CreatureState.Disguise && State != CreatureState.Leave)
            {
                revealed = true;   // the skin comes off
                if (disguise != null) disguise.gameObject.SetActive(false);
                if (trueForm != null) trueForm.gameObject.SetActive(true);
            }
            float speed = Flat(transform.position - lastPos).magnitude / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            float stride = Mathf.Clamp01(speed / (revealed ? 4.5f : 1.6f));
            phase += dt * (revealed ? 1.4f + speed * 1.6f : 1.2f + speed * 3.2f);
            Vector3 right = transform.right, fwd = transform.forward;

            if (!revealed)
            {
                if (suitRig != null) { suitRig.Animate(speed, false, false, 0f, dt); return; }
                // Wrong walk: stiff arms, knees snapping a little past straight, a head that never turns on its own.
                float swing = Mathf.Sin(phase) * 24f * stride;
                if (Time.time > twitchAt) { twitch = Random.Range(-14f, 14f); twitchAt = Time.time + Random.Range(2.5f, 6f); }
                float tw = Time.time < twitchAt - 2.3f ? twitch : 0f;   // a snap of the head, held a moment, then gone
                Pose("WandererThigh.L", right, -swing); Pose("WandererThigh.R", right, swing);
                Pose("WandererShin.L", right, Mathf.Sin(phase) > 0f ? Mathf.Sin(phase) * 30f * stride : -10f * stride);   // over-bent back
                Pose("WandererShin.R", right, Mathf.Sin(phase) < 0f ? -Mathf.Sin(phase) * 30f * stride : -10f * stride);
                Pose("WandererUpperArm.L", right, swing * 0.15f); Pose("WandererUpperArm.R", right, -swing * 0.15f);
                Pose("WandererHead", fwd, tw);
                // Clear blood drips from the wet patch, a little trail behind it.
                if (Time.time > nextDrop && speed > 0.2f) { nextDrop = Time.time + 0.7f; Drop(); }
                return;
            }
            // The thing underneath: stooped, arms out ahead of it, a long loping stride.
            bool grab = State == CreatureState.Grab;
            float s2 = Mathf.Sin(phase) * 38f * stride;
            Pose("StealerSpine", right, 18f);
            Pose("StealerChest", right, 12f);
            Pose("StealerHead", right, -20f, fwd, Mathf.Sin(Time.time * 0.9f) * 10f);
            float reach = State == CreatureState.Chase || grab ? (grab ? 95f : 60f) : 10f;
            Pose("StealerUpperArm.L", right, -reach + s2 * 0.4f); Pose("StealerUpperArm.R", right, -reach - s2 * 0.4f);
            Pose("StealerForearm.L", right, grab ? -30f : -15f); Pose("StealerForearm.R", right, grab ? -30f : -15f);
            Pose("StealerThigh.L", right, -s2); Pose("StealerThigh.R", right, s2);
            Pose("StealerShin.L", right, Mathf.Max(0f, Mathf.Sin(phase)) * 45f * stride);
            Pose("StealerShin.R", right, Mathf.Max(0f, -Mathf.Sin(phase)) * 45f * stride);
        }

        void Pose(string bone, Vector3 axis, float degrees, Vector3 axis2 = default, float degrees2 = 0f)
        {
            var b = B(bone);
            if (b == null) return;
            b.localRotation = rest[b];
            b.rotation = Quaternion.AngleAxis(degrees, axis) * (degrees2 != 0f ? Quaternion.AngleAxis(degrees2, axis2) : Quaternion.identity) * b.rotation;
        }

        /// One clear drop on the floor under the wound; it stays a while.
        void Drop()
        {
            var at = transform.position + transform.right * -0.14f + transform.forward * 0.02f;
            if (!Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) return;
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(d.GetComponent<Collider>());
            d.name = "Clear Blood";
            d.transform.position = hit.point + Vector3.up * 0.003f;
            float r = Random.Range(0.035f, 0.07f);
            d.transform.localScale = new Vector3(r, 0.006f, r * Random.Range(0.8f, 1.3f));
            d.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
            var mr = d.GetComponent<MeshRenderer>();
            mr.sharedMaterial = clearDrop;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(d, 40f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
