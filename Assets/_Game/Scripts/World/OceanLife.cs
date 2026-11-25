using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 7's surface: black swells. One 32 m sheet per chunk, moved on the CPU so swimmers ride the same waves they see.
    public sealed class RollingSea : MonoBehaviour
    {
        const int N = 16;
        static Material material;
        Mesh mesh;
        Vector3[] v, n;

        public static float Swell(float x, float z)
        {
            float t = Time.time;
            return 0.2f * Mathf.Sin((x * 0.8f + z * 0.6f) * 0.21f + t * 0.9f) + 0.12f * Mathf.Sin((z * 0.86f - x * 0.5f) * 0.37f + t * 1.3f)
                 + 0.05f * Mathf.Sin((x * 0.3f - z * 0.95f) * 0.8f + t * 2.1f);
        }

        public static void Create(Transform parent, SurfaceQuad q, Material basis)
        {
            if (basis == null) return;
            if (material == null)
            {
                material = new Material(basis) { name = "Black sea" };
                material.SetColor("_BaseColor", new Color(0.004f, 0.008f, 0.011f, 0.965f));
                material.SetFloat("_Smoothness", 0.86f);
                material.SetFloat("_NormalScale", 0.8f);
                material.SetFloat("_DetailNormalScale", 0.6f);
                var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);   // occluded: no sky for it to mirror white
                mask.SetPixel(0, 0, new Color(0f, 0.22f, 0f, 1f));
                mask.Apply();
                material.SetTexture("_MaskMap", mask);
                material.SetFloat("_SmoothnessRemapMin", 0f);
                material.SetFloat("_SmoothnessRemapMax", 0.86f);
                material.SetFloat("_AORemapMin", 0f);
                material.SetFloat("_AORemapMax", 1f);
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            }
            var go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(q.Area.xMin, q.Height, q.Area.yMin);
            go.AddComponent<RollingSea>().Build(q.Area);
        }

        void Build(Rect area)
        {
            v = new Vector3[(N + 1) * (N + 1)];
            n = new Vector3[v.Length];
            var uv = new Vector2[v.Length];
            var tangents = new Vector4[v.Length];
            var tris = new int[N * N * 6];
            for (int i = 0, at = 0; i <= N; i++)
            for (int k = 0; k <= N; k++, at++)
            {
                v[at] = new Vector3(i * area.width / N, 0f, k * area.height / N);
                uv[at] = new Vector2(area.xMin + v[at].x, area.yMin + v[at].z);
                tangents[at] = new Vector4(1f, 0f, 0f, 1f);
            }
            for (int i = 0, t = 0; i < N; i++)
            for (int k = 0; k < N; k++)
            {
                int a = i * (N + 1) + k, b = a + N + 1;
                tris[t++] = a; tris[t++] = a + 1; tris[t++] = b; tris[t++] = b; tris[t++] = a + 1; tris[t++] = b + 1;
            }
            mesh = new Mesh { name = "Sea" };
            mesh.MarkDynamic();
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.tangents = tangents;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(new Vector3(area.width / 2f, 0f, area.height / 2f), new Vector3(area.width, 2f, area.height));
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Update();
        }

        void Update()
        {
            var o = transform.position;
            const float e = 0.4f;
            for (int i = 0; i < v.Length; i++)
            {
                float x = o.x + v[i].x, z = o.z + v[i].z, h = Swell(x, z);
                v[i].y = h;
                n[i] = new Vector3(h - Swell(x + e, z), e, h - Swell(x, z + e)).normalized;
            }
            mesh.SetVertices(v);
            mesh.SetNormals(n);
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }

    /// A school of pale fish. They circle, and scatter from you and from your light.
    public sealed class FishSchool : MonoBehaviour
    {
        const int Count = 16;
        static Mesh fish;
        static Material skin;
        readonly Transform[] body = new Transform[Count];
        readonly Vector3[] at = new Vector3[Count], velocity = new Vector3[Count];
        float phase;

        public void Build(SpecialSpec s)
        {
            if (fish == null)
            {
                var m = new RockMesh(default, false);
                m.Tube(new[] { new Vector3(0f, 0f, 0.2f), new Vector3(0f, 0f, 0.13f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -0.14f) }, new[] { 0.005f, 0.04f, 0.055f, 0.012f }, 6);
                m.Leaf(new Vector3(0f, 0f, -0.12f), new Vector3(0f, 0f, -0.26f), new Vector3(0f, 0.08f, 0f));
                m.Leaf(new Vector3(0f, 0.04f, 0.04f), new Vector3(0f, 0.05f, -0.09f), new Vector3(0f, 0.06f, 0f));
                fish = m.ToMesh("Fish", Vector3.zero);
            }
            if (skin == null) skin = HdMat.DoubleSided(HdMat.Make(new Color(0.75f, 0.74f, 0.7f), 0.55f));
            phase = s.Id * 0.37f;
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Fish");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = fish;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = skin;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = Vector3.one * (0.8f + (i * 37 % 10) * 0.07f);
                body[i] = go.transform;
                at[i] = Home(i);
            }
        }

        Vector3 Home(int i)
        {
            float a = Time.time * 0.22f + phase + i * 0.17f;
            return transform.position + new Vector3(Mathf.Cos(a) * (3.5f + i % 4), Mathf.Sin(a * 1.7f + i) * 0.7f, Mathf.Sin(a) * (3.5f + i % 3));
        }

        void Update()
        {
            var me = Players.Local;
            if (me == null || (me.Position - transform.position).sqrMagnitude > 45f * 45f) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < Count; i++)
            {
                var want = (Home(i) - at[i]) * 0.8f;
                foreach (var p in Players.All)
                {
                    var away = at[i] - p.Eyes;
                    float d = away.magnitude;
                    bool lit = p.FlashlightOn && d < 14f && Vector3.Angle(p.Forward, away) < 18f;
                    if (d < 2.5f || lit) want += away / Mathf.Max(0.3f, d) * (lit ? 9f : 6f) + Vector3.Cross(away, Vector3.up).normalized * (i % 2 == 0 ? 3f : -3f);
                }
                velocity[i] = Vector3.Lerp(velocity[i], Vector3.ClampMagnitude(want, 5f), dt * 3f);
                at[i] += velocity[i] * dt;
                at[i].y = Mathf.Min(at[i].y, OceanLayout.OceanWaterLevel - 0.8f);
                body[i].position = at[i];
                if (velocity[i].sqrMagnitude > 0.01f)
                    body[i].rotation = Quaternion.LookRotation(velocity[i]) * Quaternion.Euler(0f, Mathf.Sin(Time.time * (6f + velocity[i].magnitude * 3f) + i) * 12f, 0f);
            }
        }
    }

    /// Silt and a few bubbles hanging in the water round the camera.
    public sealed class SeaDrift : MonoBehaviour
    {
        ParticleSystem silt, bubbles;
        static Texture2D dot;

        public static SeaDrift Create(Transform cam)
        {
            var go = new GameObject("Sea drift");
            go.transform.SetParent(cam, false);
            var d = go.AddComponent<SeaDrift>();
            d.silt = Make(go, "Silt", new Color(0.7f, 0.7f, 0.62f, 0.5f), 0.008f, 0.03f, 8f, -0.004f, 12f);
            d.bubbles = Make(go, "Bubbles", new Color(0.75f, 0.85f, 0.9f, 0.45f), 0.01f, 0.05f, 4f, -0.06f, 4f);
            return d;
        }

        static Texture2D Dot()
        {
            if (dot != null) return dot;
            dot = new Texture2D(32, 32, TextureFormat.RGBA32, true);
            for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
                dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, new Vector2(x - 15.5f, y - 15.5f).magnitude / 15f)));
            dot.Apply();
            return dot;
        }

        static ParticleSystem Make(GameObject parent, string name, Color color, float min, float max, float life, float gravity, float box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.loop = true; main.startLifetime = life; main.startSpeed = 0f; main.maxParticles = 260; main.gravityModifier = gravity;
            main.startSize = new ParticleSystem.MinMaxCurve(min, max);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = Vector3.one * box;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.3f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var em = ps.emission; em.rateOverTime = 0f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = HdMat.Transparent(HdMat.Make(color, 0.1f, 0f, Dot()));
            ps.Play();
            return ps;
        }

        public void Set(bool under, float depth)
        {
            var a = silt.emission; a.rateOverTime = under ? 30f : 0f;
            var b = bubbles.emission; b.rateOverTime = under && depth < 60f ? 2.5f : 0f;
            if (!under) { silt.Clear(); bubbles.Clear(); }   // no specks left hanging in the air
        }

        void OnDestroy()
        {
            if (silt != null) Destroy(silt.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            if (bubbles != null) Destroy(bubbles.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        }
    }
}
