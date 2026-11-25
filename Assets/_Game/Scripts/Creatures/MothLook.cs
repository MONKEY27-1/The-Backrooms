using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// The shared moth body (Art/build_deathmoth.py): painted materials, flapping wings, stepping legs.
    public sealed class MothBody
    {
        static readonly Dictionary<string, Material[]> mats = new Dictionary<string, Material[]>();

        public readonly Transform Root;
        readonly List<(Transform t, Quaternion rest, float side)> wings = new List<(Transform, Quaternion, float)>();
        readonly List<(Transform t, Quaternion rest)> legs = new List<(Transform, Quaternion)>();

        public MothBody(string model, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Monsters/" + model);
            if (prefab == null) { Debug.LogError($"Moth model missing: Resources/Monsters/{model}.fbx (run Art/build_deathmoth.py)"); Root = new GameObject(model).transform; Root.SetParent(parent, false); return; }
            Root = new GameObject(model).transform;   // a clean frame: one-piece models import with a turned root
            Root.SetParent(parent, false);
            Object.Instantiate(prefab, Root, false);
            var set = Materials(model);
            foreach (var r in Root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterials;
                for (int i = 0; i < m.Length; i++)
                {
                    string n = m[i] != null ? m[i].name : "";
                    m[i] = n.Contains("Eye") ? set[1] : n.Contains("Horn") ? set[2] : n.Contains("Egg") ? set[3] : n.Contains("Bone") ? set[4] : set[0];
                }
                r.sharedMaterials = m;
                if (r.name.Contains("WingL") || r.name.Contains("WingR")) wings.Add((r.transform, r.transform.localRotation, r.name.Contains("WingL") ? 1f : -1f));
                else if (r.name.Contains("Leg")) legs.Add((r.transform, r.transform.localRotation));
            }
        }

        static Material[] Materials(string model)
        {
            if (mats.TryGetValue(model, out var set) && set[0] != null) return set;
            set = new[]
            {
                HdMat.DoubleSided(HdMat.Make(Color.white, 0.22f, 0f, Resources.Load<Texture2D>("Monsters/MothTex/" + model))),
                HdMat.Make(new Color(0.14f, 0.1f, 0.09f), 0.92f),
                HdMat.Make(new Color(0.27f, 0.2f, 0.15f), 0.7f),
                HdMat.Make(new Color(0.9f, 0.86f, 0.72f), 0.8f),
                HdMat.Make(new Color(0.85f, 0.81f, 0.7f), 0.4f),
            };
            return mats[model] = set;
        }

        /// Wings to an angle in degrees: 0 as modelled, + raised.
        public void Wings(float degrees)
        {
            foreach (var (t, rest, side) in wings)
            {
                t.localRotation = rest;
                t.rotation = Quaternion.AngleAxis(-degrees * side * SideSign, Root.forward) * t.rotation;
            }
        }

        // The model's left is at -x once imported (it faces +z); raising that wing is a turn the other way.
        const float SideSign = -1f;

        /// Long legs stepping: neighbours and opposite sides half a step apart.
        public void Legs(float phase, float lift, float rear = 0f)
        {
            for (int i = 0; i < legs.Count; i++)
            {
                var (t, rest) = legs[i];
                float p = phase + ((i / 2 + i % 2) % 2) * Mathf.PI;
                t.localRotation = rest;
                t.rotation = Quaternion.AngleAxis(Mathf.Sin(p) * 14f * lift, Root.up) * Quaternion.AngleAxis(-Mathf.Max(0f, Mathf.Cos(p)) * 12f * lift - (i < 4 ? rear * 45f : 0f), Root.right) * t.rotation;
            }
        }
    }

    /// A flock drawn round a point (the swarm's or the docile males'): each moth chases its own drifting spot with
    /// some inertia. The owner's game and the guests' each fly their own; only the centre and the state are shared.
    public sealed class MothFlock : MonoBehaviour
    {
        public CreatureState State = CreatureState.Watch;
        /// Whoever it's smothering (the flock closes round their head).
        public Transform Prey;

        sealed class One { public MothBody Body; public Vector3 Pos, Vel, Perch, PerchUp; public float Seed, Flap; public bool Male, Perched; }
        readonly List<One> moths = new List<One>();
        Transform holder;
        float scatter, settleAt;

        public static MothFlock Build(Transform root, bool males, int count)
        {
            var f = root.gameObject.AddComponent<MothFlock>();
            f.holder = new GameObject("Moths").transform;
            f.settleAt = Time.time + 0.05f;   // once it has been turned to face out from the nest
            for (int i = 0; i < count; i++)
            {
                // Level 3's swarms have males among the females; a docile group is males only.
                bool male = males || i % 4 == 3;
                var b = new MothBody(male ? "MothMale" : "MothFemale", f.holder);
                var o = new One { Body = b, Male = male, Seed = i * 7.31f + 1f, Pos = root.position + Vector3.up * 1.6f + Random.insideUnitSphere };
                f.moths.Add(o);
            }
            return f;
        }

        static Vector3 Head(Vector3 normal)
        {
            var h = Vector3.ProjectOnPlane(Vector3.up, normal);
            return h.sqrMagnitude > 0.01f ? h : Vector3.Cross(normal, Vector3.right);
        }

        /// Somewhere on the nest's surface to settle (the swarm faces out from it).
        void Perch(One m, int i)
        {
            m.Perched = true;
            float a = Mathf.Repeat(m.Seed * 0.618f + i * 0.382f + transform.position.x, 1f) * 2f - 1f, h = Mathf.Repeat(m.Seed * 0.271f + i * 0.163f, 1f);
            var from = transform.position + transform.right * (a * 2f) + Vector3.up * (0.35f + h * 1.9f);
            bool on = Physics.Raycast(from, -transform.forward, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore);
            m.Perch = on ? hit.point + hit.normal * 0.06f : from;
            m.PerchUp = on ? hit.normal : transform.forward;
            m.Pos = m.Perch; m.Vel = Vector3.zero;
        }

        /// Repellent on Level 3: the females are gone, the males stay. shortcut: guests still see the whole flock.
        public void Thin()
        {
            foreach (var m in moths) if (!m.Male) Destroy(m.Body.Root.gameObject);
            moths.RemoveAll(m => !m.Male);
        }

        void OnDestroy() { if (holder != null) Destroy(holder.gameObject); }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f), t = Time.time;
            if (dt <= 0f) return;
            if (State == CreatureState.Leave) scatter += dt;
            for (int i = 0; i < moths.Count; i++)
            {
                var m = moths[i];
                Vector3 drift = new Vector3(Noise(m.Seed, t * 0.55f), Noise(m.Seed + 40f, t * 0.7f), Noise(m.Seed + 80f, t * 0.55f));
                Vector3 spot; float speed;
                bool still = false;
                switch (State)
                {
                    case CreatureState.Lurk:    // settled on the hive, wings flat, shivering now and then
                        if (!m.Perched && Time.time > settleAt) Perch(m, i);
                        spot = m.Perched ? m.Perch : m.Pos; speed = 2.5f; still = true; break;
                    case CreatureState.Chase:
                        spot = transform.position + Vector3.up * 1.55f + drift * 1.3f; speed = 5.5f; break;
                    case CreatureState.Grab:
                        var head = Prey != null ? Prey.position : transform.position + Vector3.up * 1.6f;
                        spot = head + drift.normalized * (0.45f + 0.2f * (i % 3)); speed = 4f; break;
                    case CreatureState.Leave:
                        spot = transform.position + Vector3.up * 2.2f + drift * (2f + scatter * 4f); speed = 6f; break;
                    default:
                        spot = transform.position + Vector3.up * 1.9f + Vector3.Scale(drift, new Vector3(1.6f, 0.6f, 1.6f)); speed = 2.2f; break;
                }
                var want = Vector3.ClampMagnitude((spot - m.Pos) * 3f, speed);
                m.Vel = Vector3.Lerp(m.Vel, want, 1f - Mathf.Exp(-dt * 4f));
                m.Pos += m.Vel * dt;
                // Never through a wall, the floor or the ceiling.
                var from = transform.position + Vector3.up * 1.5f;
                if (Physics.Linecast(from, m.Pos, out var hit, ~0, QueryTriggerInteraction.Ignore) && Players.Owns(hit.collider) == null && !still) { m.Pos = hit.point + hit.normal * 0.12f; m.Vel = Vector3.Reflect(m.Vel, hit.normal) * 0.4f; }
                var tr = m.Body.Root;
                tr.position = m.Pos;
                if (still && m.Vel.magnitude < 0.15f)
                {
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(Head(m.PerchUp), m.PerchUp), dt * 3f);   // clinging, head up
                    m.Body.Wings(Mathf.PerlinNoise(m.Seed, t * 0.8f) > 0.72f ? Mathf.Sin(t * 40f) * 6f : -4f);
                    continue;
                }
                var face = new Vector3(m.Vel.x, m.Vel.y * 0.5f, m.Vel.z);
                if (face.sqrMagnitude > 0.01f) tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(face), dt * 6f);
                m.Flap += dt * (State == CreatureState.Watch ? 46f : 62f);
                m.Body.Wings(12f + Mathf.Sin(m.Flap + m.Seed) * 52f);
            }
        }

        static float Noise(float x, float y) => Mathf.PerlinNoise(x, y) * 2f - 1f;
    }

    /// A hive on a co-op guest's game: grows once its place and facing have arrived.
    public sealed class MothHiveCopy : MonoBehaviour
    {
        void Start() => Invoke(nameof(Grow), 0.8f);
        void Grow() => MothHive.Grow(transform);
    }

    /// Moths crawling on your own lens while a swarm smothers you, and your air going. Runs on the victim's own game
    /// (the owner's or a guest's); lets go by itself the moment nothing keeps it up.
    public sealed class MothSmother : MonoBehaviour
    {
        static MothSmother current;
        readonly List<(MothBody b, Vector2 at, float seed)> crawlers = new List<(MothBody, Vector2, float)>();
        float keep, amount;
        int held = -1;
        Camera cam;

        /// Call every frame it has hold of your own player.
        public static void Hold(PlayerAgent me, Creature by = null, DeathAttack? attributed = null)
        {
            if (me == null || !me.IsLocal || !me.Alive) return;
            if (current == null) current = new GameObject("Moth Smother").AddComponent<MothSmother>();
            current.keep = Time.time + 0.2f;
            if (current.held == Time.frameCount) return;                   // two swarms take no more air than one
            current.held = Time.frameCount;
            me.Body.Smother(Time.deltaTime / 11f);                          // eleven seconds of air
            if (me.Body.Breath <= 0f) me.Bleed(Time.deltaTime * 0.08f, by, attributed);     // then they start on you
        }

        void Awake()
        {
            cam = Camera.main;
            if (cam == null) { Destroy(gameObject); return; }
            transform.SetParent(cam.transform, false);
            for (int i = 0; i < 7; i++)
            {
                var b = new MothBody(i % 3 == 2 ? "MothMale" : "MothFemale", transform);
                b.Root.localScale = Vector3.one * 0.55f;
                foreach (var r in b.Root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float a = i / 7f * Mathf.PI * 2f;
                crawlers.Add((b, new Vector2(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.19f), i * 13.7f));
            }
        }

        void LateUpdate()
        {
            bool on = Time.time < keep;
            amount = Mathf.MoveTowards(amount, on ? 1f : 0f, Time.deltaTime * (on ? 0.8f : 2f));
            if (!on && amount <= 0f) { Destroy(gameObject); return; }
            for (int i = 0; i < crawlers.Count; i++)
            {
                var (b, at, seed) = crawlers[i];
                float t = Time.time;
                var p = at * Mathf.Lerp(2.6f, 1f, amount) + new Vector2(Mathf.PerlinNoise(seed, t * 0.6f) - 0.5f, Mathf.PerlinNoise(seed + 9f, t * 0.6f) - 0.5f) * 0.24f;
                b.Root.localPosition = new Vector3(p.x, p.y, 0.34f + 0.03f * (i % 3));
                // Belly to the glass, crawling this way and that.
                float turn = seed * 40f + (Mathf.PerlinNoise(seed + 3f, t * 0.4f) - 0.5f) * 260f;
                b.Root.localRotation = Quaternion.Euler(0f, 0f, turn) * Quaternion.Euler(-90f, 0f, 0f);
                b.Wings(10f + Mathf.Sin(t * 34f + seed) * 16f);
            }
        }
    }
}
