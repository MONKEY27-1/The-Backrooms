using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Creatures
{
    /// What you see of a Partygoer (owner's and guests' alike; Art/build_partygoer.py). It stamps along on blocky legs
    /// with its boneless arms swaying, reaches with both when it hunts, and opens its chest only when it has you.
    public sealed class PartygoerLook : MonoBehaviour
    {
        const int Links = 7;   // bones in an arm
        public CreatureState State = CreatureState.Stalk;
        /// What you're made to look at when it has you.
        public Transform Chest => B("Chest");

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material fireMat;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        readonly List<Renderer> skins = new List<Renderer>();
        Transform model;
        AudioSource feet;
        GameObject flames;
        MaterialPropertyBlock soot;
        float phase, open, reach, burnt, seed;
        int lastStep;
        Vector3 lastPos;

        public static PartygoerLook Build(Transform root)
        {
            var look = root.gameObject.AddComponent<PartygoerLook>();
            var prefab = Resources.Load<GameObject>("Monsters/Partygoer");
            if (prefab == null) { Debug.LogError("Partygoer model missing: Resources/Monsters/Partygoer.fbx (run Art/build_partygoer.py)"); return look; }
            look.model = Instantiate(prefab, root, false).transform;
            look.model.name = "Partygoer Model";
            look.Setup();
            return look;
        }

        static Material Part(string part, float smooth)
        {
            if (mats.TryGetValue(part, out var m) && m != null) return m;
            return mats[part] = part == "Teeth" ? HdMat.Make(new Color(0.82f, 0.75f, 0.55f), smooth)
                : HdMat.Make(Color.white, smooth, 0f, Resources.Load<Texture2D>($"Monsters/PartygoerTex/Partygoer_{part}_color"), Resources.Load<Texture2D>($"Monsters/PartygoerTex/Partygoer_{part}_normal"));
        }

        void Setup()
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                r.sharedMaterial = n.Contains("Teeth") ? Part("Teeth", 0.6f) : n.Contains("Shell") ? Part("Shell", 0.6f) : n.Contains("Claws") ? Part("Claws", 0.75f) : n.Contains("Arms") ? Part("Arms", 0.45f) : Part("Body", 0.45f);
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
                skins.Add(r);
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!t.GetComponent<Renderer>()) { bones[t.name] = t; rest[t] = t.localRotation; }
            feet = gameObject.AddComponent<AudioSource>();
            feet.spatialBlend = 1f;
            feet.maxDistance = 20f;
            feet.rolloffMode = AudioRolloffMode.Linear;
            soot = new MaterialPropertyBlock();
            seed = Random.value * 10f;
            phase = seed;
            lastPos = transform.position;
        }

        Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime, time = Time.time + seed;
            float speed = new Vector2(transform.position.x - lastPos.x, transform.position.z - lastPos.z).magnitude / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            float stride = Mathf.Clamp01(speed / 2.4f);
            phase += dt * speed * 2.1f;
            Vector3 right = transform.right, fwd = transform.forward, up = transform.up;
            bool grab = State == CreatureState.Grab, burning = State == CreatureState.Numb;
            open = Mathf.MoveTowards(open, grab ? 1f : 0f, dt * (grab ? 1.6f : 1f));
            reach = Mathf.MoveTowards(reach, grab || State == CreatureState.Chase ? 1f : State == CreatureState.Search ? 0.35f : 0f, dt * 1.8f);

            // A heavy stamp: stiff legs, the whole body rocking over each foot.
            float sw = Mathf.Sin(phase);
            model.localPosition = Vector3.up * (Mathf.Abs(Mathf.Cos(phase)) * 0.03f * stride);
            Pose("Thigh.L", right, -sw * 24f * stride); Pose("Thigh.R", right, sw * 24f * stride);
            Pose("Shin.L", right, Mathf.Max(0f, Mathf.Cos(phase)) * 22f * stride); Pose("Shin.R", right, Mathf.Max(0f, -Mathf.Cos(phase)) * 22f * stride);
            Pose("Spine", right, 7f + reach * 9f, fwd, Mathf.Sin(time * 0.7f) * 2f + sw * 5f * stride);
            Pose("Chest", right, 4f + Mathf.Sin(time * 1.6f) * 1.5f);
            // The head cocks slowly to one side, and to the other, listening.
            Pose("Neck", right, -8f - reach * 6f);
            Pose("Head", fwd, Mathf.Sin(time * 0.45f) * 16f + (burning ? Mathf.Sin(time * 23f) * 12f : 0f), right, State == CreatureState.Search ? Mathf.Sin(time * 1.1f) * 8f : 0f);

            for (int k = 0; k < 2; k++)
            {
                string s = k == 0 ? ".L" : ".R";
                var first = B("Arm0" + s);
                float m = first != null ? Mathf.Sign(Vector3.Dot(first.position - transform.position, right)) : 1f;
                for (int i = 0; i < Links; i++)
                {
                    float wave = Mathf.Sin(time * (burning ? 13f : 1.3f + reach * 2.6f) - i * 0.85f + k * 2.1f);
                    float lift = (i == 0 ? 20f : 12f) * reach;                                 // up and out in front of it
                    Pose("Arm" + i + s, right, -lift + wave * (burning ? 16f : 4f + reach * 5f) + sw * m * 3f * stride * (1f - reach),
                        fwd, (i == 0 ? m * 6f * (1f - reach) : 0f) + Mathf.Cos(time * 1.1f - i * 0.7f + k) * (burning ? 14f : 3f),
                        up, grab && i > 2 ? -m * 16f : 0f);                                       // curling round you
                }
                Pose("Shell" + s, up, m * 118f * open);
                for (int c = 0; c < 2; c++)
                {
                    var claw = B("ClawA" + c + s);
                    if (claw == null) continue;
                    claw.localScale = Vector3.one * Mathf.Lerp(0.03f, 1f, Mathf.Clamp01(open * 1.5f - 0.4f));   // folded away in the chest until it opens
                    Pose("ClawA" + c + s, up, Mathf.Sin(time * 15f + c * 2f + k) * 16f, right, Mathf.Sin(time * 12f + c + k * 2f) * 14f);
                    Pose("ClawB" + c + s, right, Mathf.Sin(time * 17f + c * 1.3f + k) * 26f);
                }
            }

            int step = Mathf.FloorToInt(phase / Mathf.PI);
            if (step != lastStep && stride > 0.2f)
            {
                var clips = Audio.ProceduralAudio.Footsteps();
                feet.pitch = Random.Range(0.5f, 0.6f);
                feet.PlayOneShot(clips[Random.Range(0, clips.Length)], 0.8f * Meta.AudioMix.Creatures);
            }
            lastStep = step;
            Burn(burning, dt);
        }

        void Burn(bool burning, float dt)
        {
            if (burning && flames == null) flames = Flames(transform, 1.1f, 300f, 1200f);
            if (!burning && flames != null) { Destroy(flames); flames = null; }
            if (!burning) return;
            burnt = Mathf.MoveTowards(burnt, 1f, dt / 12f);   // chars, and stays charred
            soot.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.16f, 0.12f, 0.08f), burnt));
            foreach (var r in skins) if (r != null) r.SetPropertyBlock(soot);
        }

        /// A soft tongue of flame: bright yellow at the heart, red at the edges, nothing beyond.
        static Material FireMaterial()
        {
            const int N = 64;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N;
                    float width = Mathf.Sin(Mathf.Pow(v, 0.55f) * Mathf.PI) * 0.8f;   // a teardrop, fat near the bottom
                    float heat = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(0.02f, width)) * Mathf.Clamp01((1f - v) * 1.8f);
                    px[y * N + x] = new Color(heat, heat * heat * 0.6f, heat * heat * heat * 0.15f, heat * 0.55f);
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Flame", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            var m = HdMat.Transparent(HdMat.Emissive(Color.white, 3500f, tex));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseColorMap", tex);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// Fire with its own light: over a body (height about 1) or at the tip of a flare (a few cm).
        public static GameObject Flames(Transform on, float height, float rate, float lumens)
        {
            if (fireMat == null) fireMat = FireMaterial();
            var go = new GameObject("Flames");
            go.transform.SetParent(on, false);
            go.transform.localPosition = Vector3.up * height;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            float k = Mathf.Clamp01(height);
            var main = ps.main;
            main.maxParticles = 600;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            main.startSpeed = 0f;
            main.startSize3D = true;   // tongues, not balls
            main.startSizeX = main.startSizeZ = new ParticleSystem.MinMaxCurve(0.05f + 0.25f * k, 0.08f + 0.4f * k);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.1f + 0.4f * k, 0.16f + 0.65f * k);
            main.gravityModifier = -0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = k > 0.5f ? ParticleSystemShapeType.BoxShell : ParticleSystemShapeType.Box;   // round a body, not inside it sh.scale = new Vector3(0.02f + 0.45f * k, 0.02f + 1.7f * k, 0.02f + 0.36f * k);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = fireMat;
            pr.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            var lg = new GameObject("Fire Light");
            lg.transform.SetParent(go.transform, false);
            lg.AddHDLight(LightType.Point);
            var l = lg.GetComponent<Light>();
            l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            l.intensity = lumens;
            l.range = 9f;
            l.color = new Color(1f, 0.5f, 0.2f);
            return go;
        }

        void Pose(string bone, Vector3 axis, float degrees, Vector3 axis2 = default, float degrees2 = 0f, Vector3 axis3 = default, float degrees3 = 0f)
        {
            if (!bones.TryGetValue(bone, out var b)) return;
            b.localRotation = rest[b];
            var q = Quaternion.AngleAxis(degrees, axis);
            if (degrees2 != 0f) q *= Quaternion.AngleAxis(degrees2, axis2);
            if (degrees3 != 0f) q *= Quaternion.AngleAxis(degrees3, axis3);
            b.rotation = q * b.rotation;
        }
    }
}
