using System;
using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.World
{
    /// A round pool in a cave. Id 0 is still water; Id 1 is a flood pit: climb down into it and the water comes up
    /// until you float out over the rim, then drains once you've left.
    public sealed class CaveWater : MonoBehaviour
    {
        static readonly List<CaveWater> all = new List<CaveWater>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        static Material material;
        Mesh mesh;
        Transform sheet;
        AudioSource sound;
        float radius, full, level, left;
        bool floods, rising;

        public static bool TrySurface(Vector3 p, out float surface)
        {
            foreach (var w in all)
            {
                var o = w.transform.position;
                if (w.level > 0.05f && p.y > o.y - 1f && p.y < o.y + w.full + 1f && new Vector2(p.x - o.x, p.z - o.z).sqrMagnitude < w.radius * w.radius)
                {
                    surface = o.y + w.level;
                    return true;
                }
            }
            surface = float.NegativeInfinity;
            return false;
        }

        public void Build(SpecialSpec s)
        {
            radius = s.Size.x / 2f;
            full = s.Size.y;
            floods = s.Id == 1;
            level = floods ? 0f : full;
            if (material == null) material = HdMat.DoubleSided(HdMat.Transparent(HdMat.Make(new Color(0.012f, 0.03f, 0.034f, 0.86f), 0.93f)));
            mesh = Disc(radius);
            sheet = new GameObject("Water").transform;
            sheet.SetParent(transform, false);
            sheet.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = sheet.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (floods)
            {
                sound = Level5Sound.Source(gameObject, 30f);
                sound.clip = Audio.ProceduralAudio.Surf();
                sound.loop = true;
            }
            Place();
            all.Add(this);
        }

        public static Mesh Disc(float radius)
        {
            const int n = 40;
            var v = new Vector3[n + 1];
            var normal = new Vector3[n + 1];
            var t = new int[n * 3];
            normal[n] = Vector3.up;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                normal[i] = Vector3.up;
                t[i * 3] = n; t[i * 3 + 1] = (i + 1) % n; t[i * 3 + 2] = i;
            }
            var mesh = new Mesh { name = "Pool", vertices = v, normals = normal, triangles = t };
            return mesh;
        }

        void Place()
        {
            sheet.gameObject.SetActive(level > 0.05f);
            sheet.localPosition = Vector3.up * (level + Mathf.Sin(Time.time * 0.9f) * 0.02f);
        }

        void Update()
        {
            if (floods)
            {
                var me = Players.Local;
                var d = me != null ? me.Position - transform.position : Vector3.one * 999f;
                bool inside = new Vector2(d.x, d.z).magnitude < radius && d.y > -1f && d.y < full + 2f;
                bool down = inside && new Vector2(d.x, d.z).magnitude < 4.5f && d.y < 2f;
                if (down && !rising)
                {
                    rising = true;
                    UI.Captions.Say("[the rock groans; water comes roaring in]");
                    me.Body.GetComponent<PlayerHud>()?.Flash("The pit is flooding. You can't climb these walls: let the water lift you.", 5f);
                    AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Splash(), transform.position + Vector3.up, Meta.AudioMix.Effects);
                    sound.Play();
                }
                left = inside ? 0f : left + Time.deltaTime;
                if (rising && left > 12f) rising = false;
                level = Mathf.MoveTowards(level, rising ? full : 0f, Time.deltaTime * (rising ? 0.45f : 0.2f));
                sound.volume = Mathf.MoveTowards(sound.volume, rising && level < full ? 0.6f * Meta.AudioMix.Effects : 0f, Time.deltaTime * 0.4f);
            }
            Place();
        }

        void OnDestroy() { all.Remove(this); if (mesh != null) Destroy(mesh); }
    }

    /// Boiling tar. It bubbles; hands come up out of it and reach for whoever stands close; it slows and burns whoever
    /// wades in. Id 1 is Level 7's: only the bubbles.
    public sealed class TarPool : MonoBehaviour
    {
        const int Bubbles = 12, Hands = 7;
        static Mesh bubbleMesh, handMesh;
        Mesh disc;
        readonly Transform[] bubble = new Transform[Bubbles], hand = new Transform[Hands];
        readonly float[] age = new float[Bubbles];
        float radius, nextBurn;
        bool bubblesOnly, warned;

        public void Build(SpecialSpec s, Material[] paints)
        {
            radius = s.Size.x / 2f;
            bubblesOnly = s.Id == 1;
            var oil = paints[(int)PropPaint.Oil];
            if (bubbleMesh == null)
            {
                var m = new RockMesh(default, false);
                m.Blob(Vector3.zero, Vector3.one, Quaternion.identity, 0f, 12);
                bubbleMesh = m.ToMesh("Tar bubble", Vector3.zero);
                var h = new RockMesh(default, false);
                StoneHands.Add(h, Vector3.zero, 0f, 1f, 0.35f);
                handMesh = h.ToMesh("Tar hand", Vector3.zero);
            }
            Transform Part(string name, Mesh mesh)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = oil;
                return go.transform;
            }
            if (!bubblesOnly) Part("Tar", disc = CaveWater.Disc(radius));
            for (int i = 0; i < Bubbles; i++) { bubble[i] = Part("Bubble", bubbleMesh); age[i] = i / (float)Bubbles; Pop(i); }
            if (bubblesOnly) return;
            for (int i = 0; i < Hands; i++) { hand[i] = Part("Hand", handMesh); hand[i].localScale = Vector3.one * 0.8f; hand[i].gameObject.SetActive(false); }
            var a = Level5Sound.Source(gameObject, 16f);
            a.clip = Audio.ProceduralAudio.Squelch();
            a.loop = true;
            a.pitch = 0.45f;
            a.volume = 0.5f * Meta.AudioMix.Effects;
            a.Play();
        }

        void Pop(int i)
        {
            float a = UnityEngine.Random.value * 6.283f, r = Mathf.Sqrt(UnityEngine.Random.value) * radius * 0.85f;
            bubble[i].localPosition = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        void Update()
        {
            var me = Players.Local;
            if (me == null || (me.Position - transform.position).sqrMagnitude > 40f * 40f) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < Bubbles; i++)
            {
                age[i] += dt * (0.5f + i % 4 * 0.2f);
                if (age[i] > 1f) { age[i] = 0f; Pop(i); }
                bubble[i].localScale = new Vector3(1f, 0.75f, 1f) * (Mathf.Sqrt(age[i]) * (0.12f + i % 3 * 0.1f));
                if (bubblesOnly) bubble[i].localPosition = new Vector3(bubble[i].localPosition.x, age[i] * 4f, bubble[i].localPosition.z);   // under water they rise
            }
            if (bubblesOnly) return;
            var to = me.Position - transform.position;
            float flat = new Vector2(to.x, to.z).magnitude;
            bool near = me.Alive && flat < radius + 2f && Mathf.Abs(to.y) < 2.5f;
            for (int i = 0; i < Hands; i++)
            {
                // They come up at the edge of the tar, between it and you.
                float spread = (i - (Hands - 1) / 2f) * 0.34f;
                var dir = Quaternion.Euler(0f, spread * 40f, 0f) * (flat > 0.1f ? new Vector3(to.x, 0f, to.z) / flat : Vector3.forward);
                var spot = dir * Mathf.Max(0.6f, Mathf.Min(flat - 0.9f, radius - 0.7f) - i % 3 * 0.5f);
                float y = hand[i].gameObject.activeSelf ? hand[i].localPosition.y : -1.3f;
                y = Mathf.MoveTowards(y, near ? -0.1f + Mathf.Sin(Time.time * 2f + i * 1.9f) * 0.12f : -1.3f, dt * (1.2f + i * 0.15f));
                if (y <= -1.29f) { hand[i].gameObject.SetActive(false); hand[i].localPosition = new Vector3(spot.x, -1.3f, spot.z); continue; }
                hand[i].gameObject.SetActive(true);
                var flatNow = Vector3.Lerp(new Vector3(hand[i].localPosition.x, 0f, hand[i].localPosition.z), spot, dt * 1.5f);
                hand[i].localPosition = new Vector3(flatNow.x, y, flatNow.z);
                hand[i].localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(22f + Mathf.Sin(Time.time * 1.3f + i) * 10f, 0f, 0f);
            }
            if (near && !warned) { warned = true; UI.Captions.Say("[hands push up out of the tar]"); }
            if (!near || flat > radius - 0.2f || to.y > 0.4f || !Meta.Difficulty.SurvivalEnabled) return;
            me.Body.SlowedUntil = Time.time + 0.4f;
            if (Time.time < nextBurn) return;
            nextBurn = Time.time + 0.8f;
            me.Hurt(0.07f, false);
            me.LoseSanity(0.02f);
            UI.Captions.Say("[the tar burns; something has hold of your legs]");
        }

        void OnDestroy() { if (disc != null) Destroy(disc); }
    }

    /// A stone (or tar) hand as geometry: wrist, palm, four fingers and a thumb, reaching up.
    public static class StoneHands
    {
        public static void Add(RockMesh m, Vector3 foot, float yaw, float tall, float curl)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(float x, float y, float z) => foot + q * (new Vector3(x, y, z) * tall);
            m.Tube(new[] { P(0f, -0.15f, 0f), P(0.01f, 0.2f, 0f), P(0f, 0.42f, 0f), P(0f, 0.56f, 0f) }, new[] { 0.15f * tall, 0.1f * tall, 0.095f * tall, 0.12f * tall }, 9, 0.2f);
            m.Blob(P(0f, 0.6f, 0f), new Vector3(0.17f, 0.19f, 0.08f) * tall, q, 0.12f, 12);
            for (int f = 0; f < 4; f++)
            {
                float x = (f - 1.5f) * 0.095f, len = f == 1 || f == 2 ? 0.34f : f == 0 ? 0.29f : 0.23f;
                float bend = curl * (0.7f + f * 0.2f);
                var a = P(x, 0.72f, 0f);
                var dir = new Vector3(x * 0.9f, 1f, 0f).normalized;
                var mid = dir * len * 0.5f + new Vector3(0f, 0f, bend * len * 0.35f);
                var tip = mid + new Vector3(dir.x * 0.4f, Mathf.Cos(bend * 1.6f), Mathf.Sin(bend * 1.6f)) * len * 0.5f;
                m.Tube(new[] { a - q * (Vector3.up * 0.08f * tall), a, a + q * (mid * tall), a + q * (tip * tall), a + q * (tip * tall * 1.04f) },
                       new[] { 0.05f * tall, 0.048f * tall, 0.042f * tall, 0.03f * tall, 0.005f * tall }, 7, 0.15f);
            }
            var t = P(0.14f, 0.52f, 0.02f);
            m.Tube(new[] { t, t + q * (new Vector3(0.12f, 0.12f, 0.06f) * tall), t + q * (new Vector3(0.17f, 0.26f, 0.12f) * tall), t + q * (new Vector3(0.17f, 0.28f, 0.13f) * tall) },
                   new[] { 0.06f * tall, 0.05f * tall, 0.034f * tall, 0.005f * tall }, 7, 0.15f);
        }
    }

    /// The end of a crawl that shouldn't go where it goes: you come out back at a marker you already passed.
    public sealed class LoopCrawl : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var p = Players.Owns(other);
            if (p != null && p.IsLocal) Level8Events.Instance?.Loop();
        }
    }

    /// Level 8 while you're in it: which of the 9th Road's markers you've reached, and the noises of the rock.
    public sealed class Level8Events : MonoBehaviour
    {
        public static Level8Events Instance { get; private set; }
        FirstPersonController player;
        uint seed;
        Action<int> save;
        Vector3[] road;
        float nextNoise;

        /// The furthest marker reached (-1: none yet).
        public int Reached { get; private set; }
        public int Markers => road.Length;
        public Vector3 Next => road[Mathf.Min(Reached + 1, road.Length - 1)];

        public void Init(FirstPersonController body, uint levelSeed, int reached, Action<int> onReached)
        {
            Instance = this;
            player = body;
            seed = levelSeed;
            Reached = reached;
            save = onReached;
            road = CaveLayout.Road(seed);
            nextNoise = Time.time + 12f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (player == null) return;
            for (int i = road.Length - 2; i > Reached; i--)   // the last point is the drop itself
                if (Vector3.Distance(player.transform.position, road[i]) < 8f)
                {
                    Reached = i;
                    save?.Invoke(i);
                    player.GetComponent<PlayerHud>()?.Flash(i == 0 ? "A M.E.G. post with a 9 on it. The 9th Road starts here." : i == road.Length - 2 ? "The last marker. The road ends just ahead." : $"9th Road: marker {i} of {road.Length - 2}.", 3.5f);
                    break;
                }
            if (Time.time < nextNoise) return;
            nextNoise = Time.time + UnityEngine.Random.Range(9f, 26f);
            float r = UnityEngine.Random.value;
            var from = player.transform.position + UnityEngine.Random.onUnitSphere * 11f;
            if (r < 0.6f) Play(Audio.ProceduralAudio.Drip(), from, 0.8f, 1f);
            else if (r < 0.85f) Play(Audio.ProceduralAudio.Clang(), from, 0.35f, 0.4f);   // rock cracking, a long way off
            else { Play(Audio.ProceduralAudio.Machine(), from, 0.4f, 0.3f); UI.Captions.Say("[a long rumble in the rock]"); }
        }

        static void Play(AudioClip clip, Vector3 at, float volume, float pitch)
        {
            var go = new GameObject("Cave sound");
            go.transform.position = at;
            var a = Level5Sound.Source(go, 40f);
            a.volume = volume * Meta.AudioMix.Effects;
            a.pitch = pitch;
            a.PlayOneShot(clip);
            Destroy(go, Mathf.Min(6f, clip.length / pitch));
        }

        public void Loop()
        {
            int to = Mathf.Max(0, Reached - 1);
            var dir = road[to + 1] - road[to];
            player.Teleport(CaveLayout.Stand(seed, road[to]), Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 180f);
            UI.Captions.Say("[the crawl opens out]");
            player.GetComponent<PlayerHud>()?.Flash($"You've crawled out beside marker {to}. You passed this one already.", 4.5f);
        }
    }
}
