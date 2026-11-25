using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Backrooms.Creatures
{
    /// Blood: drops thrown through the air, the marks they leave on floors, walls and ceilings (one merged decal mesh),
    /// pools that spread, drips behind a badly hurt player, and splashes on your lens. It stays until you leave the
    /// level; past the cap the oldest marks go first. Material and atlas: EditorTools/GrimeMaterials.cs.
    /// shortcut: marks on a door stay in the air when it opens; give doors their own marks if it shows.
    public sealed class Blood : MonoBehaviour
    {
        enum Cell { Splat, Pool, Run, Drops }

        const int MaxMarks = 600;
        const float Lift = 0.004f, DripBelow = 0.6f, DripEvery = 1.1f, PoolTime = 9f;

        static Blood instance;
        static readonly RaycastHit[] hits = new RaycastHit[16];

        struct Spread { public int Index; public Vector3 At, Normal, Right; public float Size, Age; }
        struct Landing { public float Time, Size; public Vector3 At, Normal; public bool Audible; }

        readonly Vector3[] verts = new Vector3[MaxMarks * 4], normals = new Vector3[MaxMarks * 4];
        readonly Vector4[] tangents = new Vector4[MaxMarks * 4];
        readonly Vector2[] uvs = new Vector2[MaxMarks * 4];
        readonly List<Spread> spreading = new List<Spread>();
        readonly List<Landing> landing = new List<Landing>();
        Mesh mesh;
        Material dropMaterial;
        ParticleSystem drops;
        RawImage[] lens;
        float[] lensLeft;
        int next, nextLens;
        bool dirty;
        Vector3 lastDrip;

        static Blood Get()
        {
            if (instance == null) instance = new GameObject("Blood").AddComponent<Blood>();
            return instance;
        }

        // ---------- what happens ----------

        /// A wound: blood bursts from the body, more the worse the hit. Your own also splashes the lens.
        public static void Wound(PlayerAgent who, float amount)
        {
            var b = Get();
            b.Throw(who.Position + Vector3.up * 1.1f, Mathf.Clamp(Mathf.RoundToInt(amount * 70f), 2, 40));
            if (amount >= 0.15f) b.Pool(who.Position, 0.5f + amount);
            if (who.IsLocal) b.Splash(amount);
            scentTrail.Add(new TrailPoint { Position = who.Position, Time = Time.time, Prey = who });
        }

        /// A death: the room gets painted.
        public static void Burst(Vector3 feet)
        {
            var b = Get();
            b.Throw(feet + Vector3.up * 1.1f, 110, 1.9f);
            for (int i = 0; i < 4; i++)
            {
                var off = Random.insideUnitCircle * 0.8f;
                b.Pool(feet + new Vector3(off.x, 0f, off.y), Random.Range(0.8f, 2f));
            }
        }

        public struct TrailPoint { public Vector3 Position; public float Time; public PlayerAgent Prey; }
        static readonly List<TrailPoint> scentTrail = new List<TrailPoint>();

        /// Scent tracking: find the closest recent blood drop within radius.
        public static bool FindTrailNear(Vector3 from, float radius, out Vector3 bestPoint, out PlayerAgent prey)
        {
            bestPoint = default;
            prey = null;
            float maxAge = 50f;
            float now = Time.time;
            float bestDistSq = radius * radius;
            for (int i = scentTrail.Count - 1; i >= 0; i--)
            {
                var t = scentTrail[i];
                if (now - t.Time > maxAge) { scentTrail.RemoveAt(i); continue; }
                if (t.Prey != null && !t.Prey.Alive) continue;
                float dsq = (t.Position - from).sqrMagnitude;
                if (dsq < bestDistSq)
                {
                    bestDistSq = dsq;
                    bestPoint = t.Position;
                    prey = t.Prey;
                }
            }
            return prey != null;
        }

        /// A drag mark (e.g. from a victim dragged into a monster's den).
        public static void Drag(Vector3 from, Vector3 to, float width = 0.45f)
        {
            var b = Get();
            float dist = Vector3.Distance(from, to);
            int steps = Mathf.Max(2, Mathf.CeilToInt(dist / (width * 0.65f)));
            Vector3 dragDir = (to - from).normalized;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 p = Vector3.Lerp(from, to, t) + Random.insideUnitSphere * (width * 0.15f);
                if (Surface(p + Vector3.up * 0.5f, Vector3.down, 1.5f, out var hit) && hit.normal.y > 0.6f)
                {
                    Vector3 forwardOnFloor = Vector3.ProjectOnPlane(dragDir, hit.normal).normalized;
                    b.Place(Cell.Run, hit, forwardOnFloor, width * Random.Range(0.85f, 1.25f));
                }
            }
        }

        /// A badly hurt player walking: drops on the floor, closer together the worse it is.
        public static void Drip(PlayerAgent who, float health)
        {
            if (health >= DripBelow) return;
            var b = Get();
            if ((who.Position - b.lastDrip).sqrMagnitude < DripEvery * DripEvery * Mathf.Lerp(0.2f, 1f, health / DripBelow)) return;
            b.lastDrip = who.Position;
            var off = Random.insideUnitCircle * 0.2f;
            if (!Surface(who.Position + new Vector3(off.x, 0.5f, off.y), Vector3.down, 1.5f, out var hit)) return;
            b.Place(Cell.Drops, hit, Any(hit.normal), Random.Range(0.15f, 0.4f) * Mathf.Lerp(1.6f, 1f, health / DripBelow));
            scentTrail.Add(new TrailPoint { Position = hit.point, Time = Time.time, Prey = who });
        }

        /// A new level (or back to the menu): everything goes.
        public static void Clear()
        {
            scentTrail.Clear();
            GoreKit.ClearBodies();
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        // ---------- throwing it ----------

        void Throw(Vector3 from, int count, float scale = 1f)
        {
            if (drops == null) MakeDrops();
            AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Squelch(), from, Meta.AudioMix.Creatures);
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                if (dir.y < -0.3f) dir.y = -dir.y;
                float speed = Random.Range(3f, 7f), reach = Random.Range(0.8f, 3.2f), size = Random.Range(0.1f, 0.6f) * scale;
                for (int k = 0; k < 3; k++)
                    drops.Emit(new ParticleSystem.EmitParams
                    {
                        position = from, velocity = dir * speed * Random.Range(0.6f, 1f) + Random.insideUnitSphere,
                        startSize = Random.Range(0.012f, 0.05f), startLifetime = Random.Range(0.35f, 0.8f),
                    }, 1);
                // Straight out to a wall or ceiling, or else down to the floor below where it ran out of speed.
                if (Surface(from, dir, reach, out var hit)) Land(hit, hit.distance / speed);
                else if (Surface(from + dir * reach, Vector3.down, 6f, out hit)) Land(hit, reach / speed + Mathf.Sqrt(hit.distance / 4.9f));

                void Land(RaycastHit h, float after) =>
                    landing.Add(new Landing { Time = Time.time + after, At = h.point, Normal = h.normal, Size = size, Audible = i % 8 == 0 });
            }
        }

        void Pool(Vector3 feet, float size)
        {
            if (!Surface(feet + Vector3.up * 0.5f, Vector3.down, 3f, out var hit) || hit.normal.y < 0.7f) return;
            var right = Any(hit.normal);
            size = Fit(hit.point, hit.normal, right, size);
            if (size <= 0f) return;
            spreading.Add(new Spread { Index = Mark(Cell.Pool, hit.point, hit.normal, right, size * 0.2f), At = hit.point, Normal = hit.normal, Right = right, Size = size });
        }

        static Vector3 Any(Vector3 normal) => Vector3.ProjectOnPlane(Random.onUnitSphere, normal).normalized;

        /// The nearest thing that can be stained: not a player, a creature or anything that moves.
        static bool Surface(Vector3 from, Vector3 dir, float reach, out RaycastHit best)
        {
            best = default;
            float near = float.MaxValue;
            int n = Physics.RaycastNonAlloc(from, dir, hits, reach, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || h.distance >= near || h.rigidbody != null || Players.Owns(h.collider) != null) continue;
                if (h.collider.GetComponentInParent<Creature>() != null) continue;
                near = h.distance;
                best = h;
            }
            return near < float.MaxValue;
        }

        // ---------- marks ----------

        /// The biggest size (halved up to twice) whose corners all rest on the surface, or 0: a decal hanging past an
        /// edge would paint whatever is behind it.
        static float Fit(Vector3 at, Vector3 normal, Vector3 right, float size)
        {
            var up = Vector3.Cross(normal, right);
            for (int k = 0; k < 3; k++, size *= 0.5f)
            {
                bool ok = true;
                for (int c = 0; c < 4 && ok; c++)
                    ok = Surface(at + (right * ((c & 1) * 2 - 1) + up * ((c & 2) - 1)) * (size * 0.5f) + normal * 0.05f, -normal, 0.08f, out _);
                if (ok) return size;
            }
            return 0f;
        }

        void Place(Cell cell, RaycastHit hit, Vector3 right, float size)
        {
            size = Fit(hit.point, hit.normal, right, size);
            if (size > 0f) Mark(cell, hit.point, hit.normal, right, size);
        }

        int Mark(Cell cell, Vector3 at, Vector3 normal, Vector3 right, float size)
        {
            int i = next;
            next = (next + 1) % MaxMarks;
            spreading.RemoveAll(s => s.Index == i);   // a pool that lost its place stops growing over the new mark
            float u0 = ((int)cell & 1) * 0.5f + 0.004f, u1 = u0 + 0.492f, v0 = ((int)cell >> 1) * 0.5f + 0.004f, v1 = v0 + 0.492f;
            if (Random.value < 0.5f) (u0, u1) = (u1, u0);
            int s0 = i * 4;
            uvs[s0] = new Vector2(u0, v0); uvs[s0 + 1] = new Vector2(u0, v1); uvs[s0 + 2] = new Vector2(u1, v1); uvs[s0 + 3] = new Vector2(u1, v0);
            for (int k = 0; k < 4; k++) { normals[s0 + k] = normal; tangents[s0 + k] = new Vector4(right.x, right.y, right.z, -1f); }
            Corners(i, at, normal, right, size);
            return i;
        }

        /// Clockwise as seen from outside, like GrimeDressing; `right` is right as seen looking at the surface.
        void Corners(int i, Vector3 at, Vector3 normal, Vector3 right, float size)
        {
            var c = at + normal * (Lift + i % 8 * 0.0003f);   // overlapping marks don't fight
            Vector3 r = right * (size / 2), u = Vector3.Cross(right, normal) * (size / 2);
            int s = i * 4;
            verts[s] = c - r - u; verts[s + 1] = c - r + u; verts[s + 2] = c + r + u; verts[s + 3] = c + r - u;
            dirty = true;
        }

        void Update()
        {
            for (int i = landing.Count - 1; i >= 0; i--)
            {
                var l = landing[i];
                if (Time.time < l.Time) continue;
                landing.RemoveAt(i);
                bool wall = Mathf.Abs(l.Normal.y) < 0.4f;
                var right = wall ? Vector3.Cross(Vector3.up, -l.Normal).normalized : Any(l.Normal);
                var cell = wall && l.Size > 0.25f ? Cell.Run : Random.value < 0.35f ? Cell.Drops : Cell.Splat;
                Place(cell, new RaycastHit { point = l.At, normal = l.Normal }, right, l.Size * (cell == Cell.Run ? 1.8f : 1f));
                if (l.Audible) AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Drip(), l.At, 0.5f * Meta.AudioMix.Effects);
            }
            for (int i = spreading.Count - 1; i >= 0; i--)
            {
                var p = spreading[i];
                p.Age += Time.deltaTime;
                Corners(p.Index, p.At, p.Normal, p.Right, p.Size * Mathf.Lerp(1f, 0.2f, Mathf.Pow(1f - Mathf.Clamp01(p.Age / PoolTime), 3f)));
                if (p.Age >= PoolTime) spreading.RemoveAt(i); else spreading[i] = p;
            }
            FadeLens();
            if (dirty) Upload();
        }

        void Upload()
        {
            dirty = false;
            if (mesh == null)
            {
                mesh = new Mesh { name = "Blood" };
                mesh.MarkDynamic();
                var tris = new int[MaxMarks * 6];
                for (int i = 0; i < MaxMarks; i++)
                {
                    int s = i * 4, t = i * 6;
                    tris[t] = s; tris[t + 1] = s + 1; tris[t + 2] = s + 2; tris[t + 3] = s; tris[t + 4] = s + 2; tris[t + 5] = s + 3;
                }
                mesh.vertices = verts;
                mesh.triangles = tris;
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Resources.Load<Material>("BloodDecal");
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);   // marks can be anywhere on the level; it's one small draw
        }

        // ---------- drops in the air ----------

        void MakeDrops()
        {
            var go = new GameObject("Drops");
            go.transform.SetParent(transform, false);
            drops = go.AddComponent<ParticleSystem>();
            drops.Stop();
            var main = drops.main;
            main.loop = false;
            main.maxParticles = 600;
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = drops.emission;
            emission.enabled = false;
            dropMaterial = HdMat.Make(new Color(0.34f, 0.01f, 0.012f), 0.9f);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = ball.GetComponent<MeshFilter>().sharedMesh;
            Destroy(ball);
            pr.sharedMaterial = dropMaterial;
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- the lens ----------

        const int LensSpots = 24;

        /// Blood on the lens: spots from the same atlas, kept off the middle unless it's the end of you.
        void Splash(float amount)
        {
            var atlas = Resources.Load<Material>("BloodDecal").GetTexture("_BaseColorMap");
            if (lens == null)
            {
                var canvas = new GameObject("Lens Blood", typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(transform, false);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1;
                lens = new RawImage[LensSpots];
                lensLeft = new float[LensSpots];
                for (int i = 0; i < LensSpots; i++) lens[i] = GoreDeath.Full(canvas.transform, atlas, Color.clear);
            }
            int count = Mathf.Clamp(Mathf.CeilToInt(amount * LensSpots), 2, LensSpots);
            for (int n = 0; n < count; n++)
            {
                int at = nextLens++ % LensSpots;
                var cell = (Cell)new[] { 0, 2, 3, 3 }[Random.Range(0, 4)];
                lens[at].uvRect = new Rect(((int)cell & 1) * 0.5f, ((int)cell >> 1) * 0.5f, 0.5f, 0.5f);
                var rt = lens[at].rectTransform;
                float angle = Random.value * Mathf.PI * 2f, far = Random.Range(amount >= 0.9f ? 0f : 0.34f, 0.55f);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f + Mathf.Cos(angle) * far, 0.5f + Mathf.Sin(angle) * far);
                rt.sizeDelta = Vector2.one * (Screen.height * Random.Range(0.12f, 0.3f + amount * 0.6f));
                rt.localEulerAngles = new Vector3(0f, 0f, cell == Cell.Run ? Random.Range(-8f, 8f) : Random.value * 360f);
                rt.anchoredPosition = Vector2.zero;
                lensLeft[at] = Random.Range(0.6f, 1.1f);
            }
        }

        void FadeLens()
        {
            if (lens == null) return;
            for (int i = 0; i < LensSpots; i++)
            {
                lens[i].enabled = lensLeft[i] > 0f;
                if (lensLeft[i] <= 0f) continue;
                lensLeft[i] -= Time.deltaTime * 0.09f;
                lens[i].color = new Color(0.7f, 0.7f, 0.7f, Mathf.Clamp01(lensLeft[i] * 1.5f));
                lens[i].rectTransform.anchoredPosition += Vector2.down * (Time.deltaTime * Screen.height * 0.006f);   // it runs
            }
        }

        /// The last of you, seen from inside.
        public static void DrownLens() => Get().Splash(1f);

        /// You wake up with a clean lens.
        public static void WipeLens()
        {
            if (instance != null && instance.lensLeft != null) System.Array.Clear(instance.lensLeft, 0, LensSpots);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (dropMaterial != null) Destroy(dropMaterial);
        }

#if UNITY_EDITOR
        /// Test hook: marks on the level right now, and whether every one faces out of its surface.
        public static (int count, bool facing) Check()
        {
            int n = 0;
            bool ok = true;
            if (instance == null) return (0, true);
            for (int i = 0; i < MaxMarks; i++)
            {
                Vector3 a = instance.verts[i * 4], b = instance.verts[i * 4 + 1], c = instance.verts[i * 4 + 2];
                if (a == c) continue;
                n++;
                ok &= Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, instance.normals[i * 4]) > 0.99f;
            }
            return (n, ok);
        }
#endif
    }
}
