using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.World
{
    /// A water cooler, vending machine or fountain: Interact for a drink of almond water.
    public sealed class WaterSpot : MonoBehaviour
    {
        public string Label = "Drink (almond water)";
    }

    /// Level 4's trap: one of the few windows that isn't blacked out. Behind it, a sunny street that looks like
    /// home. Stand and stare, or touch the glass, and it takes you: you wake somewhere you've been before.
    public sealed class LureWindow : MonoBehaviour
    {
        static readonly string[] Earlier = { "0", "1", "2", "3" };
        static Texture2D daylight;
        static Material pane;
        static float lastWarned = -99f;

        FirstPersonController player;
        Camera cam;
        AudioSource birds;
        Light sun;
        float stare;
        bool taken;
        Mesh frame;

        public void Build(Material[] paints, int id)
        {
            var b = new BoxMeshBuilder(Vector3.zero, paints.Length);
            PropLibrary.OfficeWindow(b, Matrix4x4.identity, 0, true);
            frame = b.ToMesh("Lure window");
            gameObject.AddComponent<MeshFilter>().sharedMesh = frame;
            gameObject.AddComponent<MeshRenderer>().sharedMaterials = paints;

            if (pane == null) pane = HdMat.Emissive(Color.white, 1600f, Daylight());
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(glass.GetComponent<Collider>());
            glass.name = "Daylight";
            glass.transform.SetParent(transform, false);
            glass.transform.localPosition = new Vector3(0f, 1.55f, 0.012f);
            glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the quad faces -z; turn it into the room
            glass.transform.localScale = new Vector3(1.48f, 1.28f, 1f);
            glass.GetComponent<MeshRenderer>().sharedMaterial = pane;

            var touch = gameObject.AddComponent<BoxCollider>();   // the glass is solid; touching it is what springs the trap
            touch.center = new Vector3(0f, 1.55f, 0.03f);
            touch.size = new Vector3(1.5f, 1.3f, 0.06f);

            // Daylight spilling in: one soft rectangle light the size of the window.
            var lightGo = new GameObject("Daylight spill");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.55f, 0.08f);
            lightGo.AddHDLight(LightType.Rectangle);
            sun = lightGo.GetComponent<Light>();
            sun.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            sun.areaSize = new Vector2(1.4f, 1.2f);
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 2500f;
            sun.range = 9f;
            sun.shadows = LightShadows.None;

            birds = gameObject.AddComponent<AudioSource>();
            birds.clip = Audio.ProceduralAudio.Birds();
            birds.loop = true;
            birds.spatialBlend = 1f;
            birds.rolloffMode = AudioRolloffMode.Linear;
            birds.minDistance = 1f;
            birds.maxDistance = 14f;
            birds.time = (id & 0xFFFF) % 10;
            birds.Play();
        }

        void OnDestroy() { if (frame != null) Destroy(frame); }

        void Update()
        {
            if (taken) return;
            if (player == null) { player = FindFirstObjectByType<FirstPersonController>(); cam = player != null ? player.GetComponentInChildren<Camera>() : null; if (player == null) return; }
            var glass = transform.TransformPoint(new Vector3(0f, 1.55f, 0f));
            var eye = cam != null ? cam.transform.position : player.transform.position + Vector3.up * 1.6f;
            float d = Vector3.Distance(eye, glass);
            bool looking = cam != null && Vector3.Angle(cam.transform.forward, glass - eye) < 22f && d < 4f;
            stare = looking ? stare + Mathf.Min(Time.deltaTime, 0.05f) : Mathf.Max(0f, stare - Time.deltaTime * 2f);   // capped: a hitch or a low frame rate never cuts the six seconds short
            float pull = Mathf.Clamp01(stare / 6f);
            birds.volume = (0.35f + pull * 0.65f) * Meta.AudioMix.Effects;
            sun.intensity = 2500f * (1f + pull * 2f);
            if (looking && stare > 1.5f && Time.time - lastWarned > 40f)
            {
                lastWarned = Time.time;
                player.GetComponent<PlayerHud>()?.Flash("Daylight. A quiet street, a parked car, a lawn. It looks like home. You can't look away.", 4f);
                UI.Captions.Say("[birdsong; a lawnmower somewhere]");
            }
            float flat = Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(glass.x, glass.z));
            if (stare > 6f || flat < 0.75f) Take();
        }

        /// The street pulls you through: to the game session it's an exit like any other.
        void Take()
        {
            taken = true;
            UI.Captions.Say("[glass, then nothing]");
            LevelTrigger.Fire(Earlier[Random.Range(0, Earlier.Length)], transform.position);
        }

        /// A bright summer afternoon seen through glass: across a quiet street, a row of houses with pitched roofs, a parked
        /// car, a lawn and a tree. Drawn small, blurred and a little overexposed, the way daylight looks from a dim room.
        static Texture2D Daylight()
        {
            if (daylight != null) return daylight;
            const int W = 128, H = 112;
            var px = new Color[W * H];
            var rng = new System.Random(1999);
            Color Rand(Color a, Color b) => Color.Lerp(a, b, (float)rng.NextDouble());
            void Fill(float u0, float v0, float u1, float v1, Color c)
            {
                for (int y = Mathf.Max(0, (int)(v0 * H)); y < Mathf.Min(H, (int)(v1 * H)); y++)
                for (int x = Mathf.Max(0, (int)(u0 * W)); x < Mathf.Min(W, (int)(u1 * W)); x++) px[y * W + x] = c;
            }
            // Sky, a soft sun high on the left, a few clouds.
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                var c = Color.Lerp(new Color(0.85f, 0.92f, 1f), new Color(0.45f, 0.68f, 0.98f), Mathf.Clamp01((v - 0.45f) / 0.55f));
                c = Color.Lerp(c, Color.white, Mathf.Exp(-((u - 0.2f) * (u - 0.2f) + (v - 0.9f) * (v - 0.9f)) * 30f));
                float cloud = Mathf.PerlinNoise(u * 3f + 11f, v * 7f) * Mathf.PerlinNoise(u * 7f, v * 2.5f + 5f);
                if (v > 0.62f) c = Color.Lerp(c, Color.white, Mathf.Clamp01((cloud - 0.22f) * 5f) * 0.7f);
                px[y * W + x] = c;
            }
            // Houses across the street: walls, pitched roofs, dark windows, a door.
            float hu = -0.05f;
            while (hu < 1.05f)
            {
                float w = 0.22f + (float)rng.NextDouble() * 0.12f, top = 0.42f + (float)rng.NextDouble() * 0.06f, ridge = top + 0.09f + (float)rng.NextDouble() * 0.05f;
                var wall = Rand(new Color(0.92f, 0.88f, 0.78f), new Color(0.75f, 0.82f, 0.88f));
                var roof = Rand(new Color(0.35f, 0.3f, 0.3f), new Color(0.55f, 0.32f, 0.25f));
                Fill(hu, 0.27f, hu + w, top, wall);
                for (int y = (int)(top * H); y < (int)(ridge * H); y++)   // the roof: a triangle with eaves
                {
                    float k = (y / (float)H - top) / (ridge - top), half = (w / 2f + 0.02f) * (1f - k);
                    Fill(hu + w / 2f - half, y / (float)H, hu + w / 2f + half, (y + 1f) / H, roof);
                }
                for (int i = 0; i < 2; i++) Fill(hu + w * (0.18f + i * 0.45f), top - 0.09f, hu + w * (0.37f + i * 0.45f), top - 0.03f, new Color(0.25f, 0.3f, 0.36f));
                Fill(hu + w * 0.42f, 0.27f, hu + w * 0.56f, 0.36f, Rand(new Color(0.45f, 0.2f, 0.15f), new Color(0.2f, 0.3f, 0.45f)));
                hu += w + 0.03f + (float)rng.NextDouble() * 0.04f;
            }
            Fill(0f, 0.24f, 1f, 0.27f, new Color(0.38f, 0.55f, 0.28f));   // their lawns
            Fill(0f, 0.15f, 1f, 0.24f, new Color(0.42f, 0.43f, 0.45f));   // the street
            Fill(0.55f, 0.18f, 0.8f, 0.25f, new Color(0.7f, 0.15f, 0.12f)); // a parked car
            Fill(0.58f, 0.24f, 0.76f, 0.29f, new Color(0.62f, 0.12f, 0.1f));
            Fill(0.6f, 0.245f, 0.74f, 0.28f, new Color(0.55f, 0.65f, 0.75f));
            foreach (float wx in new[] { 0.6f, 0.75f }) Fill(wx - 0.02f, 0.16f, wx + 0.02f, 0.19f, new Color(0.1f, 0.1f, 0.1f));
            Fill(0f, 0.12f, 1f, 0.15f, new Color(0.8f, 0.8f, 0.78f));     // the sidewalk
            for (int y = 0; y < (int)(0.12f * H); y++)                    // this side's lawn
            for (int x = 0; x < W; x++) px[y * W + x] = new Color(0.36f, 0.58f, 0.24f) * (0.85f + 0.3f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
            for (int y = 0; y < H; y++)                                    // a tree on the left: a soft round crown on a trunk
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                if (Mathf.Abs(u - 0.13f) < 0.015f && v < 0.5f) px[y * W + x] = new Color(0.3f, 0.22f, 0.15f);
                float d = ((u - 0.13f) * (u - 0.13f)) * 1.6f + (v - 0.6f) * (v - 0.6f);
                if (d < 0.025f + 0.01f * Mathf.PerlinNoise(u * 25f, v * 25f)) px[y * W + x] = new Color(0.22f, 0.4f, 0.17f) * (0.8f + 0.4f * Mathf.PerlinNoise(u * 40f, v * 40f));
            }
            // Soften twice, then push it toward overexposed.
            var tmp = new Color[px.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color sum = Color.clear;
                    for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                        sum += px[Mathf.Clamp(y + oy, 0, H - 1) * W + Mathf.Clamp(x + ox, 0, W - 1)];
                    tmp[y * W + x] = sum / 9f;
                }
                System.Array.Copy(tmp, px, px.Length);
            }
            for (int i = 0; i < px.Length; i++) { var c = Color.Lerp(px[i] * 1.12f, Color.white, 0.12f); c.a = 1f; px[i] = c; }
            daylight = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Lure daylight", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            daylight.SetPixels(px);
            daylight.Apply(true);
            return daylight;
        }
    }

    /// Level 4: "exits can vanish once out of sight". Once you've seen this door and lost sight of it, there's a
    /// chance it's plain wall when you come back. Gone stays gone until you leave the level.
    public sealed class VanishingExit : MonoBehaviour
    {
        const float VanishChance = 0.35f;
        static readonly HashSet<Vector3Int> gone = new HashSet<Vector3Int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => gone.Clear();

        /// A new level (or a respawn): every door is back where it was.
        public static void Forget() => gone.Clear();

        public Vector3 Door;   // the middle of the doorway, for sight checks
        bool seen, willVanish;
        float lostAt = -1f, nextCheck;

        Vector3Int Key => Vector3Int.RoundToInt(Door * 2f);

        void Start() { if (gone.Contains(Key)) Destroy(gameObject); }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.2f;
            var cam = Camera.main;
            if (cam == null) return;
            var to = Door - cam.transform.position;
            float d = to.magnitude;
            bool inView = d < 22f && Vector3.Angle(cam.transform.forward, to) < 50f
                          && !Physics.Raycast(cam.transform.position, to / d, d - 0.6f, ~0, QueryTriggerInteraction.Ignore);
            if (inView) { seen = true; lostAt = -1f; return; }
            if (!seen) return;
            if (lostAt < 0f) { lostAt = Time.time; willVanish = Random.value < VanishChance; }   // decided once each time you look away
            if (willVanish && Time.time - lostAt > 1.5f && d > 6f)
            {
                gone.Add(Key);
                Destroy(gameObject);
            }
        }
    }
}
