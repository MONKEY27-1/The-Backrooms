using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// What you see of a Wretch or the mass (owner's and guests' alike; Art/build_wretch.py). The variant number picks
    /// its rags and what grew where it shouldn't; what it does picks the pose: shambling, scuttling low, lying like a corpse.
    public sealed class WretchLook : MonoBehaviour
    {
        public CreatureState State = CreatureState.Stalk;

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Color[] Cloth = { new Color(0.9f, 0.88f, 0.82f), new Color(0.5f, 0.55f, 0.62f), new Color(0.55f, 0.4f, 0.32f), new Color(0.45f, 0.5f, 0.38f), new Color(0.62f, 0.3f, 0.26f), new Color(0.35f, 0.34f, 0.36f) };

        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        Transform model;
        ParticleSystem spray;
        bool mass, quick;
        float phase, seed, down, strike, limp;
        CreatureState shown;
        Vector3 lastPos;

        public static float VoicePitch(CreatureKind kind, int variant) => kind == CreatureKind.WretchMass ? 0.42f : (variant & 2) != 0 ? 1.95f : 0.6f;

        public static WretchLook Build(Transform root, CreatureKind kind, int variant)
        {
            var look = root.gameObject.AddComponent<WretchLook>();
            look.mass = kind == CreatureKind.WretchMass;
            look.quick = (variant & 1) != 0;
            string file = look.mass ? "WretchMass" : "Wretch";
            var prefab = Resources.Load<GameObject>("Monsters/" + file);
            if (prefab == null) { Debug.LogError($"Wretch model missing: Resources/Monsters/{file}.fbx (run Art/build_wretch.py)"); return look; }
            look.model = Instantiate(prefab, root, false).transform;
            look.model.name = file + " Model";
            look.Setup(variant >> 2);
            return look;
        }

        static Material Part(string name, float smooth, Color? tint = null, string key = null)
        {
            key ??= name;
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            return mats[key] = HdMat.Make(tint ?? Color.white, smooth, 0f, Resources.Load<Texture2D>($"Monsters/WretchTex/{name}_color"), Resources.Load<Texture2D>($"Monsters/WretchTex/{name}_normal"));
        }

        static Material Flat(string name, Color c, float smooth)
        {
            if (mats.TryGetValue(name, out var m) && m != null) return m;
            return mats[name] = HdMat.Make(c, smooth);
        }

        void Setup(int looks)
        {
            var rng = new System.Random(looks);
            bool Has(double chance) => rng.NextDouble() < chance;
            bool eyesA = Has(0.35), eyesB = Has(0.35), arm = Has(0.25), wings = Has(0.15), shirt = Has(0.55), trousers = Has(0.75);
            int shirtTint = rng.Next(Cloth.Length), trouserTint = rng.Next(Cloth.Length);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if ((n.Contains("ExtraEyesA") && !eyesA) || (n.Contains("ExtraEyesB") && !eyesB) || (n.Contains("ExtraArm") && !arm) || (n.Contains("Wings") && !wings)
                    || (n.Contains("Shirt") && !shirt) || (n.Contains("Trousers") && !trousers)) { r.gameObject.SetActive(false); continue; }
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    string m = ms[i] != null ? ms[i].name : "";
                    ms[i] = m.StartsWith("WretchIris") ? Flat("Iris", new Color(0.05f, 0.035f, 0.03f), 0.9f)
                        : m.StartsWith("WretchEye") ? Flat("Eye", new Color(0.62f, 0.4f, 0.3f), 0.9f)
                        : m.StartsWith("WretchTeeth") ? Flat("Teeth", new Color(0.58f, 0.49f, 0.3f), 0.5f)
                        : n.Contains("Shirt") ? Part("WretchShirt", 0.1f, Cloth[shirtTint], "Shirt" + shirtTint)
                        : n.Contains("Trousers") ? Part("WretchTrousers", 0.1f, Cloth[trouserTint], "Trousers" + trouserTint)
                        : n.Contains("ExtraArm") ? Part("WretchExtraArm", 0.4f) : n.Contains("Wings") ? Part("WretchWings", 0.4f)
                        : mass ? Part("WretchMassBody", 0.5f) : Part("WretchBody", 0.4f);
                }
                r.sharedMaterials = ms;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!t.GetComponent<Renderer>()) { bones[t.name] = t; rest[t] = t.localRotation; }
            model.localScale *= 0.92f + (float)rng.NextDouble() * 0.16f;
            limp = (float)rng.NextDouble();
            seed = phase = (float)rng.NextDouble() * 10f;
            lastPos = transform.position;
            if (State == CreatureState.Lurk) down = 1f;
            if (mass) Sludge();
        }

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime, time = Time.time + seed;
            float speed = new Vector2(transform.position.x - lastPos.x, transform.position.z - lastPos.z).magnitude / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            if (State != shown)
            {
                if (State == CreatureState.Lurk) down = 1f;   // was already lying there
                if (State == CreatureState.Lunge) { strike = 1f; if (spray != null) spray.Play(); }
                shown = State;
            }
            strike = Mathf.MoveTowards(strike, 0f, dt * 1.3f);
            down = Mathf.MoveTowards(down, State == CreatureState.Lurk ? 1f : 0f, dt * 0.9f);
            Vector3 right = transform.right, fwd = transform.forward, up = transform.up;
            if (mass) { PoseMass(time, speed, dt, right, fwd); return; }

            bool hunting = State == CreatureState.Chase || State == CreatureState.Lunge || State == CreatureState.Break;
            float stride = Mathf.Clamp01(speed / (quick ? 3f : 1.2f));
            phase += dt * (quick ? speed * 2.6f : speed * 3.4f);
            float sw = Mathf.Sin(phase), live = 1f - down;
            float jerk = Mathf.PerlinNoise(time * 3f, seed) > 0.68f ? Mathf.Sin(time * 31f) * 7f : 0f;   // fits and starts
            // Lying on its side, limbs anyhow; getting up it lurches.
            model.localRotation = Quaternion.Euler(0f, 0f, 84f * down) * Quaternion.Euler(0f, 0f, sw * (quick ? 2f : 5f) * stride * live);
            model.localPosition = Vector3.up * (0.14f * down) + right * 0f;
            float bend = quick ? 38f : 20f, drag = quick ? 1f : 1f - 0.65f * limp;   // a dull one drags a leg
            Pose("Hips", right, quick && hunting ? 12f * live : 0f);
            Pose("Spine", right, bend * live + 25f * down, fwd, sw * 4f * stride + jerk * 0.5f * live);
            Pose("Chest", right, bend * 0.5f * live + Mathf.Sin(time * 2.1f) * 2f * live);
            Pose("Neck", right, -bend * 0.5f * live); Pose("Head", right, -bend * (quick ? 1.05f : 0.6f) * live + 20f * down, fwd, (Mathf.Sin(time * 0.8f) * 14f + jerk * 1.6f) * live + 30f * down);
            float leg = (quick ? 42f : 26f) * stride * live;
            Pose("Thigh.L", right, -sw * leg - 45f * down - (quick ? 14f : 0f) * live); Pose("Thigh.R", right, sw * leg * drag - 15f * down - (quick ? 14f : 0f) * live);
            Pose("Shin.L", right, Mathf.Max(0f, Mathf.Cos(phase)) * leg * 1.2f + 70f * down + (quick ? 26f : 6f) * live); Pose("Shin.R", right, Mathf.Max(0f, -Mathf.Cos(phase)) * leg * drag + 30f * down + (quick ? 26f : 6f) * live);
            float hit = strike > 0f ? Mathf.Sin((1f - strike) * Mathf.PI * 1.4f) : 0f;   // arm back, then across
            float reach = hunting ? (quick ? 55f : 80f) : 8f;
            bool hammer = State == CreatureState.Break;
            for (int k = 0; k < 2; k++)
            {
                string s = k == 0 ? ".L" : ".R";
                float m = Side("UpperArm" + s, right), wob = Mathf.Sin(time * (hammer ? 9f : 1.7f) + k * 2.2f);
                Pose("UpperArm" + s, right, (-reach - (hammer ? 45f + wob * 45f : wob * 6f) - hit * (k == 0 ? 25f : 50f)) * live + sw * m * 12f * stride * live - 20f * down, up, -m * hit * 45f * live);
                Pose("Forearm" + s, right, (hunting ? -22f : -10f) * live - 30f * down + jerk);
                Pose("Hand" + s, right, (hunting ? -20f : 0f) + Mathf.Sin(time * 7f + k) * (hunting ? 8f : 2f) * live);
            }
            Pose("ExtraA", right, Mathf.Sin(time * 2.3f) * 14f * live, up, Mathf.Sin(time * 1.3f) * 12f * live); Pose("ExtraB", up, Mathf.Sin(time * 3.1f) * 22f * live);
            for (int k = 0; k < 2; k++)
            {
                string s = k == 0 ? ".L" : ".R"; float m = Side("WingA" + s, right);
                Pose("WingA" + s, fwd, m * (Mathf.Sin(time * 1.2f) * 6f + (hunting ? Mathf.Sin(time * 11f) * 12f : 0f)) * live); Pose("WingB" + s, fwd, m * Mathf.Sin(time * 1.6f + 1f) * 9f * live);
            }
        }

        /// Heaving itself along, every lump of it breathing on its own.
        void PoseMass(float time, float speed, float dt, Vector3 right, Vector3 fwd)
        {
            phase += dt * (0.8f + speed * 2.4f);
            float gait = Mathf.Clamp01(speed / 0.8f), hit = strike > 0f ? Mathf.Sin((1f - strike) * Mathf.PI) : 0f;
            model.localRotation = Quaternion.Euler(hit * 14f + Mathf.Sin(phase) * 3f * gait, 0f, Mathf.Sin(phase * 0.5f) * 4f * gait);
            for (int i = 0; i < 7; i++)
            {
                if (!bones.TryGetValue("Lobe" + i, out var b)) continue;
                b.localScale = Vector3.one * (1f + Mathf.Sin(time * (1.1f + i * 0.23f) + i * 1.9f) * 0.06f + hit * 0.07f);
                Pose("Lobe" + i, right, Mathf.Sin(time * 0.9f + i) * 5f, fwd, Mathf.Sin(time * 0.7f + i * 2f) * 5f);
            }
            for (int i = 0; i < 5; i++)
            {
                float p = phase + i * 1.7f;
                Pose("LimbA" + i, transform.up, Mathf.Sin(p) * (i == 4 ? 12f : 20f * gait), right, i == 4 ? Mathf.Sin(time * 1.4f) * 12f - hit * 35f : -Mathf.Max(0f, Mathf.Cos(p)) * 14f * gait);
                Pose("LimbB" + i, right, Mathf.Sin(p + 0.8f) * (i == 4 ? 20f : 10f * gait));
            }
        }

        float Side(string bone, Vector3 right) => bones.TryGetValue(bone, out var b) ? Mathf.Sign(Vector3.Dot(b.position - transform.position, right)) : 1f;

        void Pose(string bone, Vector3 axis, float degrees, Vector3 axis2 = default, float degrees2 = 0f)
        {
            if (!bones.TryGetValue(bone, out var b)) return;
            b.localRotation = rest[b];
            b.rotation = Quaternion.AngleAxis(degrees, axis) * (degrees2 != 0f ? Quaternion.AngleAxis(degrees2, axis2) : Quaternion.identity) * b.rotation;
        }

        /// The sludge it sprays.
        void Sludge()
        {
            var go = new GameObject("Sludge");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.8f, 0.55f);
            go.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            spray = go.AddComponent<ParticleSystem>();
            spray.Stop();
            var main = spray.main;
            main.duration = 0.5f; main.loop = false; main.startDelay = 0.6f; main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.11f);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = spray.emission; em.rateOverTime = 220f;
            var sh = spray.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 20f; sh.radius = 0.1f;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = Flat("Sludge", new Color(0.2f, 0.09f, 0.04f), 0.9f);
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        }
    }
}
