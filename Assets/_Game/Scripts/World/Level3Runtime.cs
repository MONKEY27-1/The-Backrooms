using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.World
{
    public enum SpecialKind { BarGate, Sludge, Sanctum, MachineHum, Hotspot, Angel, Pipe, LureWindow, WaterSpot, HotelDoor, Elevator, BallroomDoor, BallroomZone, Gramophone, Heat, QuietSwitch, QuietRoom, DivingGear, FishSchool, TarPool, CaveWater, LoopCrawl, RockSong, HouseDoor, LineHum }

    /// Something a chunk needs at runtime beyond meshes (Level 3 for now).
    public struct SpecialSpec
    {
        public SpecialKind Kind;
        public Matrix4x4 Pose;   // origin on the floor; for bar gates the middle of the opening, +z across it
        public Vector3 Size;     // trigger size for zones; width x height for bar gates
        public int Id;           // stable per world, for hotspots and gates
    }

    /// Level 3's power: off until the breaker panel by the lift has its fuses (the level goal). Turning it on lifts
    /// every barred gate in the level, now and in chunks built later.
    public static class Level3Power
    {
        public static bool On { get; private set; }
        public static event System.Action Restored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { On = false; Restored = null; }

        /// Called when the level loads (from the saved goal) and when the goal completes.
        public static void Set(bool on, bool announce)
        {
            bool was = On;
            On = on;
            if (on && !was && announce) Restored?.Invoke();
        }

        /// Builds the runtime object for one special.
        public static void Create(Transform parent, SpecialSpec s, Material[] paints)
        {
            var go = new GameObject(s.Kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(s.Pose.GetColumn(3), s.Pose.rotation);
            switch (s.Kind)
            {
                case SpecialKind.BarGate: go.AddComponent<BarGate>().Build(s, paints); break;
                case SpecialKind.Sludge: Zone(go, s.Size).gameObject.AddComponent<SludgeZone>(); break;
                case SpecialKind.Sanctum: Zone(go, s.Size).gameObject.AddComponent<Sanctum>(); break;
                case SpecialKind.MachineHum: Loop(go, Audio.ProceduralAudio.Machine(), 0.55f, 3f, 26f); break;
                case SpecialKind.Hotspot:
                    Loop(go, Audio.ProceduralAudio.Whisper(), 0.35f, 1.5f, 9f);
                    Zone(go, s.Size).gameObject.AddComponent<Hotspot>();
                    break;
                case SpecialKind.Angel: Angel(go); break;
                case SpecialKind.Pipe: go.AddComponent<SludgePipeSpot>(); break;
                case SpecialKind.LureWindow: go.AddComponent<LureWindow>().Build(paints, s.Id); break;
                case SpecialKind.HotelDoor: go.AddComponent<HotelDoor>().Build(s, paints); break;
                case SpecialKind.Elevator: go.AddComponent<HotelElevator>().Build(paints); break;
                case SpecialKind.BallroomDoor: go.AddComponent<BallroomDoor>().Build(s, paints); break;
                case SpecialKind.BallroomZone: Zone(go, s.Size).gameObject.AddComponent<BallroomZone>(); break;
                case SpecialKind.Gramophone: go.AddComponent<Turntable>().Build(s); break;
                case SpecialKind.Heat: Zone(go, s.Size).gameObject.AddComponent<HeatZone>(); break;
                case SpecialKind.QuietSwitch:
                {
                    var sc = go.AddComponent<SphereCollider>();
                    sc.isTrigger = true;
                    sc.radius = 0.12f;
                    go.AddComponent<QuietSwitch>();
                    break;
                }
                case SpecialKind.QuietRoom: Zone(go, s.Size).gameObject.AddComponent<QuietRoom>(); break;
                case SpecialKind.DivingGear: go.AddComponent<DivingGear>().Build(paints); break;
                case SpecialKind.FishSchool: go.AddComponent<FishSchool>().Build(s); break;
                case SpecialKind.TarPool: go.AddComponent<TarPool>().Build(s, paints); break;
                case SpecialKind.CaveWater: go.AddComponent<CaveWater>().Build(s); break;
                case SpecialKind.LoopCrawl: Zone(go, s.Size).gameObject.AddComponent<LoopCrawl>(); break;
                case SpecialKind.HouseDoor: go.AddComponent<HouseDoor>().Build(s, paints); break;
                case SpecialKind.LineHum: Loop(go, Audio.ProceduralAudio.Hum(), 0.3f, 3f, 24f); break;   // the power line
                case SpecialKind.RockSong: Loop(go, Audio.ProceduralAudio.Gramophone(), 0.1f, 2f, 22f); break;
                case SpecialKind.WaterSpot:   // a box round the cooler or fountain to look at
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = Vector3.up * s.Size.y / 2f;
                    box.size = s.Size;
                    go.AddComponent<WaterSpot>().Label = s.Id % 3 == 0 ? "Drink (almond water from the cooler)" : "Drink (almond water)";
                    break;
                }
            }
        }

        static BoxCollider Zone(GameObject go, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * size.y / 2f;
            box.size = size;
            return box;
        }

        static void Loop(GameObject go, AudioClip clip, float volume, float near, float far)
        {
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.volume = volume * Meta.AudioMix.Effects;
            a.time = Random.value * clip.length;   // so neighbouring machines don't drone in phase
            a.Play();
        }

        static Material angelMaterial;

        /// The Sanctum's statue: a scanned stone angel (CC BY 4.0, see CREDITS.md) on its own textured material.
        static void Angel(GameObject go)
        {
            var mesh = ModelLibrary.Standalone("L3Angel", out _);
            if (mesh == null) return;
            if (angelMaterial == null)
                angelMaterial = HdMat.Make(Color.white, 0.18f, 0f,
                    Resources.Load<Texture2D>("Textures/L3Angel/L3Angel_BaseColor"), Resources.Load<Texture2D>("Textures/L3Angel/L3Angel_Normal"));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = angelMaterial;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(1.2f, 3f, 0.9f);
        }
    }

    /// Iron bars across an opening. No gate, no lock: they only lift when the level's power comes back.
    public sealed class BarGate : MonoBehaviour
    {
        static readonly List<BarGate> all = new List<BarGate>();
        Transform bars;
        BoxCollider block;
        float lift = -1f;   // seconds into the lift; < 0 = down
        const float Rise = 2.6f, LiftTime = 4.5f;

        public void Build(SpecialSpec s, Material[] paints)
        {
            bars = new GameObject("Bars").transform;
            bars.SetParent(transform, false);
            var b = new BoxMeshBuilder(Vector3.zero, paints.Length);
            // The model is 2 m x 3 m; stretched to the opening, it runs up into the header where it can't be seen.
            var fit = Matrix4x4.Scale(new Vector3(s.Size.x / 2.07f, 1f, 1f));
            if (!ModelLibrary.Add(b, "L3BarGate", fit))
                for (int i = 0; i < 12; i++) b.AddOrientedBox(Matrix4x4.identity, new Vector3(-s.Size.x / 2f + (i + 0.5f) * s.Size.x / 12f, 1.5f, 0f), new Vector3(0.04f, 3f, 0.04f), (int)PropPaint.Rust);
            bars.gameObject.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Bar Gate");
            bars.gameObject.AddComponent<MeshRenderer>().sharedMaterials = paints;
            block = gameObject.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, 1.5f, 0f);
            block.size = new Vector3(s.Size.x, 3f, 0.2f);
            if (Level3Power.On) Raise(true);
        }

        void OnEnable() { all.Add(this); Level3Power.Restored += OnPower; }
        void OnDisable() { all.Remove(this); Level3Power.Restored -= OnPower; }
        void OnDestroy() { var m = bars != null ? bars.GetComponent<MeshFilter>()?.sharedMesh : null; if (m != null) Destroy(m); }

        void OnPower() => Raise(false);

        void Raise(bool instant)
        {
            if (lift >= 0f) return;
            lift = instant ? LiftTime : 0f;
            block.enabled = false;
            if (instant) { bars.localPosition = Vector3.up * Rise; return; }
            var a = gameObject.AddComponent<AudioSource>();
            a.spatialBlend = 1f;
            a.maxDistance = 30f;
            a.PlayOneShot(Audio.ProceduralAudio.Clang(), 0.8f * Meta.AudioMix.Effects);
            a.clip = Audio.ProceduralAudio.Machine();
            a.loop = true;
            a.pitch = 0.6f;
            a.volume = 0.5f * Meta.AudioMix.Effects;
            a.Play();
        }

        void Update()
        {
            if (lift < 0f || lift >= LiftTime) return;
            lift += UnityEngine.Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, lift / LiftTime);
            bars.localPosition = Vector3.up * (Rise * k + Mathf.Sin(lift * 40f) * 0.004f * (1f - k));   // a judder as they grind up
            if (lift >= LiftTime) { var a = GetComponent<AudioSource>(); if (a != null) a.Stop(); }
        }
    }

    /// Black sludge from the boiler pipes: scalding. It slows you to a wade and eats at your stamina and your nerve.
    public sealed class SludgeZone : MonoBehaviour
    {
        static float lastWarned = -99f;
        AudioSource hiss;

        void OnTriggerStay(Collider other)
        {
            var body = other.GetComponentInParent<FirstPersonController>();
            if (body == null) return;
            body.SlowedUntil = UnityEngine.Time.time + 0.15f;
            var status = body.GetComponent<PlayerStatus>();
            if (status != null && Meta.Difficulty.SurvivalEnabled)
            {
                status.Stamina = Mathf.Max(0f, status.Stamina - UnityEngine.Time.deltaTime / 2f);   // outpaces normal recovery: no sprinting out of it
                status.Sanity = Mathf.Max(0f, status.Sanity - UnityEngine.Time.deltaTime / 45f * Meta.Difficulty.Sanity);
            }
            if (hiss == null)
            {
                hiss = gameObject.AddComponent<AudioSource>();
                hiss.clip = Audio.ProceduralAudio.Hiss();
                hiss.loop = true;
                hiss.spatialBlend = 0.6f;
            }
            hiss.volume = 0.5f * Meta.AudioMix.Effects;
            if (!hiss.isPlaying) hiss.Play();
            if (UnityEngine.Time.time - lastWarned > 20f)
            {
                lastWarned = UnityEngine.Time.time;
                body.GetComponent<PlayerHud>()?.Flash("It's hot. The black sludge burns through your boots. Get out of it.", 4f);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (hiss != null && other.GetComponentInParent<FirstPersonController>() != null) hiss.Stop();
        }
    }

    /// A Sanctum: the angel's cathedral. Hunts end at its door, your mind settles, and it becomes your checkpoint.
    public sealed class Sanctum : MonoBehaviour
    {
        static float lastSaved = -99f;

        void OnTriggerEnter(Collider other)
        {
            var body = other.GetComponentInParent<FirstPersonController>();
            if (body == null) return;
            FindFirstObjectByType<Creatures.FearDirector>()?.Sanctuary(transform.position, 40f);
            if (UnityEngine.Time.time - lastSaved < 60f) return;
            lastSaved = UnityEngine.Time.time;
            FindFirstObjectByType<Game.GameSession>()?.SetShrineCheckpoint(body.transform.position);
            body.GetComponent<PlayerHud>()?.Flash("A stillness here. Whatever was behind you won't come in. (Checkpoint)", 5f);
            UI.Captions.Say("[the hum falls silent]");
        }

        void OnTriggerStay(Collider other)
        {
            var body = other.GetComponentInParent<FirstPersonController>();
            if (body == null) return;
            var status = body.GetComponent<PlayerStatus>();
            if (status != null) status.Sanity = Mathf.Min(1f, status.Sanity + UnityEngine.Time.deltaTime / 40f);
            FindFirstObjectByType<Creatures.FearDirector>()?.Sanctuary(transform.position, 40f);
        }
    }

    /// A hotspot: a stretch of corridor where you hear crying and someone asking for help. Nobody is there.
    public sealed class Hotspot : MonoBehaviour
    {
        void OnTriggerStay(Collider other)
        {
            var body = other.GetComponentInParent<FirstPersonController>();
            if (body == null || !Meta.Difficulty.SurvivalEnabled) return;
            var status = body.GetComponent<PlayerStatus>();
            if (status != null) status.Sanity = Mathf.Max(0f, status.Sanity - UnityEngine.Time.deltaTime / 90f * Meta.Difficulty.Sanity);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonController>() != null && Random.value < 0.5f) UI.Captions.Say("[someone crying, close by]");
        }
    }

    /// A sludge pipe that can burst (the crack in the model sits 0.45 m left of its middle).
    public sealed class SludgePipeSpot : MonoBehaviour
    {
        static readonly List<SludgePipeSpot> all = new List<SludgePipeSpot>();
        bool burst;
        Mesh poolMesh;

        void OnDestroy() { if (poolMesh != null) Destroy(poolMesh); }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static SludgePipeSpot Near(Vector3 p, float min, float max)
        {
            SludgePipeSpot best = null;
            float bestD = float.MaxValue;
            foreach (var s in all)
            {
                if (s == null || s.burst) continue;
                float d = Vector3.Distance(s.transform.position, p);
                if (d >= min && d <= max && Mathf.Abs(s.transform.position.y - p.y) < 2f && d < bestD) { best = s; bestD = d; }
            }
            return best;
        }

        public void Burst()
        {
            burst = true;
            var streamer = ChunkStreamer.Instance;
            if (streamer == null) return;
            var at = transform.TransformPoint(new Vector3(-0.45f, 0f, 0.9f));
            var pool = new GameObject("Burst sludge");
            pool.transform.SetParent(transform, true);
            pool.transform.position = at;
            var b = new BoxMeshBuilder(at, streamer.Paints.Length);
            PropLibrary.Build(new PropSpec { Kind = PropKind.SludgePool, Variant = Random.Range(0, 1000), Pose = Matrix4x4.TRS(at, transform.rotation, Vector3.one) }, b, 3f);
            poolMesh = b.ToMesh("Burst sludge");
            pool.AddComponent<MeshFilter>().sharedMesh = poolMesh;
            pool.AddComponent<MeshRenderer>().sharedMaterials = streamer.Paints;
            var zone = pool.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, 0.3f, 0f);
            zone.size = new Vector3(2.2f, 0.6f, 2.2f);
            pool.AddComponent<SludgeZone>();
            var a = pool.AddComponent<AudioSource>();
            a.spatialBlend = 1f;
            a.maxDistance = 30f;
            a.PlayOneShot(Audio.ProceduralAudio.Clang(), 0.9f * Meta.AudioMix.Effects);
            a.PlayOneShot(Audio.ProceduralAudio.Hiss(), 0.9f * Meta.AudioMix.Effects);
            UI.Captions.Say("[a pipe bursts close by, hissing]");
        }
    }

    /// Level 3 while you're in it: pipes burst near you now and then, and the power coming back is an event.
    public sealed class Level3Events : MonoBehaviour
    {
        FirstPersonController player;
        float next;

        public void Init(FirstPersonController body, ChunkStreamer world)
        {
            player = body;
            next = Time.time + Random.Range(60f, 120f);
            Level3Power.Restored += OnPower;
        }

        void OnDestroy() => Level3Power.Restored -= OnPower;

        void OnPower()
        {
            UI.Captions.Say("[generators roar back to life; somewhere, iron grinds upward]");
            player?.GetComponent<PlayerHud>()?.Flash("The breakers hold. Every barred gate in the station is lifting.", 6f);
        }

        void Update()
        {
            if (player == null || Time.time < next) return;
            next = Time.time + Random.Range(70f, 150f);
            SludgePipeSpot.Near(player.transform.position, 4f, 22f)?.Burst();
        }
    }
}
