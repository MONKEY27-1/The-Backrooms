using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Creatures
{
    /// What you see of a Smiler (the owner's real one and a co-op guest's copy alike): the grin and eyes, and the
    /// body (Art/build_smiler.py) that only shows for a split second when a flashlight beam or a camera flash
    /// catches it. Poses are code-driven on the rig, per state: a lurker's eyes are half-lidded and its grin dim,
    /// a staring one cocks its head, a charging one drops forward with the jaw open.
    public sealed class SmilerLook : MonoBehaviour
    {
        public CreatureState State = CreatureState.Stalk;

        static readonly List<SmilerLook> all = new List<SmilerLook>();
        static Material skin;

        readonly List<Renderer> hidden = new List<Renderer>();
        Material teeth, eyes;
        Light catchLight;   // the beam or flash catching it: lights the body for the glimpse
        Transform eyeL, eyeR, model;
        Vector3 eyeScale;
        int lidAxis = 2;   // the eye's local axis that points up (FBX import axes vary)
        // Bones driven in code, each with its rest rotation.
        Transform spine, chest, neck, head, jaw, armL, armR, foreL, foreR, thighL, thighR, shinL, shinR;
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        float glow = -1f, shownGlow = -1f, lids = 1f, lean, jawOpen, phase, glimpseUntil, nextGlimpse, sink;
        Vector3 lastPos;

        public static SmilerLook Build(Transform root)
        {
            var look = root.gameObject.AddComponent<SmilerLook>();
            var prefab = Resources.Load<GameObject>("Monsters/Smiler");
            if (prefab == null) { Debug.LogError("Smiler model missing: Resources/Monsters/Smiler.fbx (run Art/build_smiler.py)"); return look; }
            look.model = Instantiate(prefab, root, false).transform;
            look.model.name = "Smiler Model";
            // The model is 2.7 m tall: stoop it under this room's ceiling and pipes, at most ~2.1 m (grin just above your eyes).
            // shortcut: sized where it appears, not per room it moves through; fine while ceilings stay similar within a level.
            float ceiling = Physics.Raycast(root.position + Vector3.up * 0.5f, Vector3.up, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y - root.position.y : 3f;
            look.model.localScale = Vector3.one * Mathf.Clamp((ceiling - 0.45f) / 2.7f, 0.55f, 0.8f);
            look.Setup();
            return look;
        }

        void Setup()
        {
            if (skin == null) skin = HdMat.Make(new Color(0.07f, 0.064f, 0.06f), 0.78f);   // ash-grey, wet
            teeth = HdMat.Emissive(new Color(1f, 0.97f, 0.9f), 400f);
            eyes = HdMat.Emissive(Color.white, 600f);
            var mouth = HdMat.Make(Color.black, 0.05f);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                switch (r.name)
                {
                    case "Body": case "Head": r.sharedMaterial = skin; r.enabled = false; hidden.Add(r); break;
                    case "GrinUpper": case "GrinLower": r.sharedMaterial = teeth; break;
                    case "Mouth": r.sharedMaterial = mouth; break;
                    case "EyeL": case "EyeR": r.sharedMaterial = eyes; break;
                }
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            eyeL = Find("EyeL"); eyeR = Find("EyeR");
            if (eyeL != null)
            {
                eyeScale = eyeL.localScale;
                float best = 0f;
                for (int i = 0; i < 3; i++)
                {
                    var a = Vector3.zero; a[i] = 1f;
                    float d = Mathf.Abs(Vector3.Dot(eyeL.TransformDirection(a).normalized, transform.up));
                    if (d > best) { best = d; lidAxis = i; }
                }
            }
            spine = Bone("Spine"); chest = Bone("Chest"); neck = Bone("Neck"); head = Bone("Head"); jaw = Bone("Jaw");
            armL = Bone("UpperArm.L"); armR = Bone("UpperArm.R"); foreL = Bone("Forearm.L"); foreR = Bone("Forearm.R");
            thighL = Bone("Thigh.L"); thighR = Bone("Thigh.R"); shinL = Bone("Shin.L"); shinR = Bone("Shin.R");
            phase = Random.value * 10f;
            var lg = new GameObject("Glimpse Light");
            lg.transform.SetParent(transform, false);
            lg.AddHDLight(LightType.Point);
            catchLight = lg.GetComponent<Light>();
            catchLight.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            catchLight.intensity = 900f;
            catchLight.range = 5f;
            catchLight.color = new Color(0.85f, 0.9f, 1f);
            catchLight.enabled = false;
            lastPos = transform.position;
        }

        Transform Find(string n)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        Transform Bone(string n)
        {
            var t = Find(n);
            if (t != null) rest[t] = t.localRotation;
            return t;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// A photo was taken: every Smiler in the shot shows its body for a moment.
        public static void CameraFlash()
        {
            foreach (var s in all) if (s != null && Players.Local != null && Players.Local.Sees(s.HeadPoint)) s.Glimpse(0.18f);
        }

        Vector3 HeadPoint => head != null ? head.position : transform.position + Vector3.up * 2.3f;

        void Glimpse(float seconds)
        {
            glimpseUntil = Time.time + seconds;
            nextGlimpse = Time.time + 1.6f;
        }

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime;

            // Caught in your beam: the body flickers into view, just for a moment, now and then.
            var me = Players.Local;
            if (me != null && me.FlashlightOn && Time.time > nextGlimpse)
            {
                var to = HeadPoint - me.Eyes;
                if (to.magnitude < 18f && Vector3.Angle(me.Forward, to) < 14f && me.Sees(HeadPoint)) Glimpse(0.12f);
            }
            bool show = Time.time < glimpseUntil;
            foreach (var r in hidden) if (r.enabled != show) r.enabled = show;
            if (catchLight != null)
            {
                catchLight.enabled = show;
                if (show && me != null) catchLight.transform.position = transform.position + Vector3.up * 1.3f + Flat(me.Eyes - transform.position).normalized * 1.5f;
            }

            // Per-state targets: grin brightness, eyelids, forward lean, jaw.
            float g = 1f, l = 1f, ln = 0f, jw = 0f;
            switch (State)
            {
                case CreatureState.Lurk: g = 0.35f; l = 0.35f; break;                     // lids and lips half closed
                case CreatureState.Chase: g = 1.3f; ln = 38f; jw = 28f; break;
                case CreatureState.Lured: g = 0.8f; ln = 12f; break;
                case CreatureState.Numb: g = 0.2f; l = 0.1f; ln = -15f; jw = 40f; break;
                case CreatureState.Leave: g = 0f; l = 0f; break;
            }
            glow = glow < 0f ? g : Mathf.MoveTowards(glow, g, dt * (State == CreatureState.Leave ? 0.7f : 2f));
            float flicker = State == CreatureState.Chase ? 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 18f, phase) : 1f;
            float shown = glow * flicker;
            if (Mathf.Abs(shown - shownGlow) > 0.02f)
            {
                shownGlow = shown;
                HdMat.Emissive(new Color(1f, 0.97f, 0.9f), 400f * shown, null, teeth);
                HdMat.Emissive(Color.white, 600f * shown, null, eyes);
            }
            lids = Mathf.MoveTowards(lids, l, dt * 1.5f);
            var lidded = eyeScale; lidded[lidAxis] *= Mathf.Max(0.05f, lids);
            if (eyeL != null) eyeL.localScale = lidded;
            if (eyeR != null) eyeR.localScale = lidded;
            lean = Mathf.MoveTowards(lean, ln, dt * 90f);
            jawOpen = Mathf.MoveTowards(jawOpen, jw, dt * 120f);
            sink = State == CreatureState.Leave ? sink + dt * 0.6f : 0f;     // sinks back into the dark as it goes

            // Gait from how fast the root moves.
            float speed = Flat(transform.position - lastPos).magnitude / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            float stride = Mathf.Clamp01(speed / 4.5f);
            phase += dt * (1.2f + speed * 2.2f);
            float swing = Mathf.Sin(phase) * 38f * stride;

            // Pose in the creature's own space (bone axes from Blender don't matter).
            Vector3 right = transform.right, fwd = transform.forward;
            Pose(spine, right, lean * 0.45f);
            Pose(chest, right, lean * 0.35f + Mathf.Sin(Time.time * 1.3f + phase) * 1.5f);   // slow breathing
            float tilt = State == CreatureState.Stare ? 24f + Mathf.Sin(Time.time * 0.6f) * 6f : Mathf.Sin(Time.time * 0.35f + phase) * 9f;
            Pose(neck, right, -lean * 0.5f);
            Pose(head, fwd, tilt, right, -lean * 0.3f + Mathf.Sin(Time.time * 7.3f) * (State == CreatureState.Stare ? 1.5f : 0f));
            Pose(jaw, right, jawOpen);
            float reach = State == CreatureState.Chase ? 55f : 0f;           // arms come up and forward when it lunges
            Pose(armL, right, -reach + swing * 0.6f);
            Pose(armR, right, -reach - swing * 0.6f);
            Pose(foreL, right, -reach * 0.4f);
            Pose(foreR, right, -reach * 0.4f);
            Pose(thighL, right, -swing);
            Pose(thighR, right, swing);
            Pose(shinL, right, Mathf.Max(0f, Mathf.Sin(phase + 1.2f)) * 40f * stride);
            Pose(shinR, right, Mathf.Max(0f, Mathf.Sin(phase + 1.2f + Mathf.PI)) * 40f * stride);
            model.localPosition = Vector3.down * sink;
        }

        /// Rest pose, then a turn about a direction in world space (applied after the parent's pose).
        void Pose(Transform b, Vector3 axis, float degrees, Vector3 axis2 = default, float degrees2 = 0f)
        {
            if (b == null) return;
            b.localRotation = rest[b];
            b.rotation = Quaternion.AngleAxis(degrees, axis) * (degrees2 != 0f ? Quaternion.AngleAxis(degrees2, axis2) : Quaternion.identity) * b.rotation;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
