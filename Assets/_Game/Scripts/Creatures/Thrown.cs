using Backrooms.World;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Creatures
{
    /// Something you threw: a glowstick (a green light on the floor that draws Smilers for over a minute) or a
    /// Smiler Exterminator (wiki Object 3: breaks on impact; any Smiler close by flees and stays away a long while).
    /// Every game in co-op spawns the same throw; only the owner's copy (authority) touches the creatures.
    public sealed class Thrown : MonoBehaviour
    {
        const float GlowLife = 75f, NumbRadius = 4.5f;

        PropKind kind;
        bool authority, broken;
        float born;
        Vector3 flownFrom;
        Light glow;

        const float FlareLife = 40f;
        static Material stick, glass, cap, mist, steel, paper;

        public static void Spawn(PropKind kind, Vector3 at, Vector3 velocity, bool authority)
        {
            if (kind == PropKind.Repellent) { Spray(at, velocity.normalized, authority); return; }
            if (stick == null) stick = HdMat.Emissive(new Color(0.35f, 1f, 0.4f), 900f);
            if (glass == null) glass = HdMat.Make(new Color(0.32f, 0.2f, 0.08f), 0.9f);
            if (cap == null) cap = HdMat.Make(new Color(0.6f, 0.08f, 0.06f), 0.4f);

            var go = new GameObject(kind == PropKind.Glowstick ? "Thrown Glowstick" : "Thrown Exterminator");
            go.transform.position = at;
            go.transform.rotation = Random.rotation;
            var t = go.AddComponent<Thrown>();
            t.kind = kind;
            t.authority = authority;
            t.born = Time.time;
            if (kind == PropKind.AlmondWater) { Bottle(go, velocity); return; }
            if (kind == PropKind.Flare) { t.Flare(velocity); return; }
            bool light = kind == PropKind.Glowstick;
            Part(go.transform, light ? stick : glass, Vector3.zero, light ? new Vector3(0.022f, 0.08f, 0.022f) : new Vector3(0.06f, 0.08f, 0.06f));
            if (!light) Part(go.transform, cap, new Vector3(0f, 0.1f, 0f), new Vector3(0.025f, 0.025f, 0.025f));
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = light ? 0.02f : 0.035f;
            col.height = light ? 0.17f : 0.2f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = light ? 0.05f : 0.3f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 12f;
            if (light)
            {
                var lg = new GameObject("Glow");
                lg.transform.SetParent(go.transform, false);
                lg.AddHDLight(LightType.Point);
                t.glow = lg.GetComponent<Light>();
                t.glow.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
                t.glow.intensity = 160f;
                t.glow.range = 5f;
                t.glow.color = new Color(0.4f, 1f, 0.45f);
                go.AddComponent<Lure>();
                UI.Captions.Say("[a glowstick cracks]");
            }
        }

        /// A road flare: burns on the floor (lighting the room, drawing Smilers and moths) and sets a Partygoer alight on contact.
        void Flare(Vector3 velocity)
        {
            if (paper == null) paper = HdMat.Make(new Color(0.75f, 0.1f, 0.08f), 0.3f);
            name = "Thrown Flare";
            flownFrom = transform.position;
            Part(transform, paper, Vector3.zero, new Vector3(0.03f, 0.12f, 0.03f));
            var col = gameObject.AddComponent<CapsuleCollider>(); col.radius = 0.02f; col.height = 0.25f;
            var body = gameObject.AddComponent<Rigidbody>(); body.mass = 0.2f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = velocity; body.angularVelocity = Random.insideUnitSphere * 9f;
            PartygoerLook.Flames(transform, 0.13f, 45f, 700f);
            gameObject.AddComponent<Lure>();
            var hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = Audio.ProceduralAudio.Hiss(); hiss.loop = true; hiss.spatialBlend = 1f; hiss.maxDistance = 14f; hiss.rolloffMode = AudioRolloffMode.Linear; hiss.volume = 0.35f * Meta.AudioMix.Effects; hiss.Play();
            UI.Captions.Say("[a flare hisses alight]");
            Destroy(gameObject, FlareLife);
        }

        /// A cloud from a repellent can: moths in front of it, up to 5 m, go.
        static void Spray(Vector3 at, Vector3 dir, bool authority)
        {
            UI.Captions.Say("[a long hiss of spray]");
            AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Hiss(), at, 0.8f * Meta.AudioMix.Effects);
            var go = new GameObject("Repellent");
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.duration = 0.6f; main.loop = false; main.startLifetime = 1.1f; main.startSpeed = 5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startColor = new Color(0.85f, 0.9f, 0.82f, 0.16f);
            var em = ps.emission; em.rateOverTime = 110f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.03f;
            if (mist == null) mist = HdMat.Transparent(HdMat.Make(new Color(0.85f, 0.9f, 0.82f, 0.16f), 0f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mist;
            ps.Play();
            Destroy(go, 2.5f);
            if (!authority) return;
            foreach (var m in FindObjectsByType<Deathmoth>(FindObjectsSortMode.None))
            {
                var to = m.transform.position + Vector3.up * 1.3f - at;
                if (to.magnitude < 1.6f || (to.magnitude < 5f && Vector3.Angle(dir, to) < 40f)) m.Repel(at);
            }
        }

        static void Part(Transform parent, Material m, Vector3 pos, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            var r = p.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// Almond water set down open (a thermos): stands where it lands and draws docile moths until they've drunk it.
        static void Bottle(GameObject go, Vector3 velocity)
        {
            if (steel == null) steel = HdMat.Make(new Color(0.62f, 0.64f, 0.66f), 0.6f, 0.9f);
            go.name = "Set-down Bottle";
            go.transform.rotation = Quaternion.identity;
            Part(go.transform, steel, Vector3.zero, new Vector3(0.09f, 0.13f, 0.09f));
            var col = go.AddComponent<CapsuleCollider>(); col.radius = 0.045f; col.height = 0.27f;
            var body = go.AddComponent<Rigidbody>(); body.mass = 0.5f; body.constraints = RigidbodyConstraints.FreezeRotation; body.linearVelocity = velocity;
            go.AddComponent<MothBait>();
            Destroy(go, 240f);
        }

        void Update()
        {
            if (kind == PropKind.Flare && authority)
            {
                // The whole way it flew since last frame (a fast throw would skip past one), or lying where it treads.
                var step = transform.position - flownFrom;
                foreach (var g in FindObjectsByType<Partygoer>(FindObjectsSortMode.None))
                {
                    float u = step.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(g.transform.position + Vector3.up - flownFrom, step) / step.sqrMagnitude);
                    var d = flownFrom + step * u - g.transform.position;
                    if (d.y > -0.3f && d.y < 2.4f && new Vector2(d.x, d.z).magnitude < (Time.time - born < 1.5f ? 0.8f : 0.5f)) g.Ignite(transform.position);
                }
                flownFrom = transform.position;
            }
            if (glow == null) return;
            float left = GlowLife - (Time.time - born);
            glow.intensity = 160f * Mathf.Clamp01(left / 15f);   // fades over its last seconds
            if (left <= 0f) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (kind != PropKind.Exterminator || broken || Time.time - born < 0.05f) return;
            broken = true;
            var at = transform.position;
            var sfx = new GameObject("Shatter").AddComponent<AudioSource>();
            sfx.transform.position = at;
            sfx.spatialBlend = 1f;
            sfx.maxDistance = 25f;
            sfx.rolloffMode = AudioRolloffMode.Linear;
            sfx.pitch = 1.8f;
            sfx.PlayOneShot(Audio.ProceduralAudio.Clang(), 0.7f * Meta.AudioMix.Effects);
            sfx.PlayOneShot(Audio.ProceduralAudio.Hiss(), 0.5f * Meta.AudioMix.Effects);
            Destroy(sfx.gameObject, 3f);
            UI.Captions.Say("[glass shatters, a sharp chemical hiss]");
            if (authority) FindFirstObjectByType<FearDirector>()?.Noise(at, 12f);   // glass breaking carries
            if (authority)
                foreach (var s in FindObjectsByType<Smiler>(FindObjectsSortMode.None))
                    if (Vector3.Distance(s.transform.position + Vector3.up * 1.2f, at) < NumbRadius) s.Numb(at);
            Destroy(gameObject);
        }
    }
}
