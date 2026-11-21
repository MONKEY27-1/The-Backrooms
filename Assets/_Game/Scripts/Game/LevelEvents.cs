using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// Level 0.2 "Remodeled Mess" (Backrooms Wiki): it looks freshly renovated until you've been inside a while,
    /// then it falls apart around you: ceiling tiles drop, walls topple, the floor caves in. The chain reaction never
    /// reaches the door back to Level 0. Re-entering restores everything (this component starts fresh each visit).
    public sealed class Collapse : MonoBehaviour
    {
        const float QuietTime = 25f, RampTime = 150f;
        FirstPersonController player;
        PlayerStatus status;
        PlayerHud hud;
        Material wall, ceiling, dark;
        readonly List<GameObject> debris = new List<GameObject>();
        AudioSource rumble;
        float started, next;
        bool warned;

        public void Init(FirstPersonController player, LevelTheme theme, LevelCatalog catalog)
        {
            this.player = player;
            status = player.GetComponent<PlayerStatus>();
            hud = player.GetComponent<PlayerHud>();
            wall = catalog.Resolve(theme, new SurfaceRef(Surface.Wall));
            ceiling = catalog.Resolve(theme, new SurfaceRef(Surface.Ceiling));
            dark = HdMat.Make(new Color(0.02f, 0.02f, 0.02f), 0.05f);
            started = Time.time;
            next = started + QuietTime;
            rumble = gameObject.AddComponent<AudioSource>();
            rumble.clip = Audio.ProceduralAudio.Hum();
            rumble.loop = true;
            rumble.pitch = 0.25f;
            rumble.volume = 0f;
            rumble.Play();
            Net.LevelSync.On("collapse", r =>
            {
                r.ReadValueSafe(out byte kind);
                r.ReadValueSafe(out Vector3 at);
                r.ReadValueSafe(out float yaw);
                r.ReadValueSafe(out float power);
                r.ReadValueSafe(out float radius);
                Happen(kind, at, yaw, power, radius);
            });
        }

        void OnDestroy()
        {
            foreach (var d in debris) if (d != null) Destroy(d);
            if (rumble != null) Destroy(rumble);
            Net.LevelSync.Off("collapse");
        }

        void Update()
        {
            if (player == null) return;
            float t = Mathf.Clamp01((Time.time - started - QuietTime) / RampTime);
            rumble.volume = Mathf.MoveTowards(rumble.volume, t * 0.35f * Meta.AudioMix.Ambience, Time.deltaTime * 0.1f);
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetDrift(t * 0.7f);   // dust in the air
            if (Time.time < next || !Net.LevelSync.Decides) return;   // a co-op guest sees what the owner's game decides
            next = Time.time + Mathf.Lerp(3.5f, 0.5f, t);
            var who = Players.Pick(Random.value);   // around one of the living
            if (who == null) return;
            var at = SpotNear(who.Position, 3f, 12f);
            if (!at.HasValue || NearExit(at.Value)) return;
            float r = Random.value;
            byte kind = r < 0.55f ? (byte)0 : r < 0.85f ? (byte)1 : t > 0.35f ? (byte)2 : (byte)255;
            if (kind == 255) return;
            float yaw = Random.value * 360f, power = Random.Range(40f, 90f), radius = Random.Range(0.8f, 1.4f);
            var spot = at.Value;
            Happen(kind, spot, yaw, power, radius);
            Net.LevelSync.Send("collapse", w => { w.WriteValueSafe(kind); w.WriteValueSafe(spot); w.WriteValueSafe(yaw); w.WriteValueSafe(power); w.WriteValueSafe(radius); });
        }

        void Happen(byte kind, Vector3 at, float yaw, float power, float radius)
        {
            if (!warned) { warned = true; hud?.Flash("Something creaks overhead. Plaster dust drifts down. Find the door back."); UI.Captions.Say("[creaking overhead]"); }
            if (kind == 0) FallingTile(at);
            else if (kind == 1) ToppleWall(at, yaw, power);
            else CaveIn(at, radius);
        }

        static Vector3? SpotNear(Vector3 p, float min, float max)
        {
            for (int i = 0; i < 12; i++)
            {
                var dir = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                var q = p + dir * Random.Range(min, max);
                if (Physics.Raycast(q + Vector3.up * 1f, Vector3.down, 1.5f) && !Physics.CheckSphere(q + Vector3.up * 1f, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    return new Vector3(q.x, p.y, q.z);
            }
            return null;
        }

        static bool NearExit(Vector3 p)
        {
            foreach (var t in FindObjectsByType<LevelTrigger>(FindObjectsSortMode.None))
                if (t.Target == "0" && Vector3.Distance(t.transform.position, p) < 6f) return true;
            return false;
        }

        GameObject Piece(string name, PrimitiveType type, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = at;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            debris.Add(go);
            if (debris.Count > 120) { Destroy(debris[0]); debris.RemoveAt(0); }
            return go;
        }

        void FallingTile(Vector3 at)
        {
            float top = Physics.Raycast(at + Vector3.up * 0.5f, Vector3.up, out var hit, 6f) ? hit.point.y - 0.05f : at.y + 2.6f;
            var tile = Piece("Ceiling Tile", PrimitiveType.Cube, new Vector3(at.x, top, at.z), new Vector3(0.6f, 0.025f, 0.6f), ceiling);
            var body = tile.AddComponent<Rigidbody>();
            body.mass = 2f;
            body.angularVelocity = Random.insideUnitSphere * 3f;
            tile.AddComponent<Hurt>().Setup(status, hud, 0.25f, "A ceiling tile hits you.");
        }

        void ToppleWall(Vector3 at, float yaw, float power)
        {
            UI.Captions.Say("[a wall crashes down]");
            var slab = Piece("Toppling Wall", PrimitiveType.Cube, at + Vector3.up * 1.3f, new Vector3(2f, 2.6f, 0.12f), wall);
            slab.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var body = slab.AddComponent<Rigidbody>();
            body.mass = 60f;
            body.AddTorque(slab.transform.right * power, ForceMode.Impulse);
            slab.AddComponent<Hurt>().Setup(status, hud, 0.5f, "A wall comes down on you.");
        }

        void CaveIn(Vector3 at, float radius)
        {
            var hole = Piece("Cave-in", PrimitiveType.Cylinder, at + Vector3.up * 0.01f, new Vector3(radius * 2f, 0.005f, radius * 2f), dark);
            Destroy(hole.GetComponent<Collider>());
            for (int i = 0; i < 6; i++)
            {
                var rubble = Piece("Rubble", PrimitiveType.Cube, at + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * (radius + 0.2f) + Vector3.up * 0.08f,
                                   Vector3.one * Random.Range(0.1f, 0.25f), ceiling);
                rubble.transform.rotation = Random.rotation;
            }
            // Fall in and you come out in Level 0 again.
            var trigger = new GameObject("Cave-in Trigger");
            trigger.transform.position = at + Vector3.up * 0.5f;
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(radius * 1.4f, 1f, radius * 1.4f);
            trigger.AddComponent<LevelTrigger>().Target = "";
            debris.Add(trigger);
        }

        /// Debris that hurts when it lands on you.
        sealed class Hurt : MonoBehaviour
        {
            PlayerStatus status;
            PlayerHud hud;
            float amount;
            string message;
            bool done;

            public void Setup(PlayerStatus s, PlayerHud h, float a, string m) { status = s; hud = h; amount = a; message = m; }

            void OnCollisionEnter(Collision c)
            {
                if (done || c.relativeVelocity.magnitude < 2f || !c.collider.GetComponent<CharacterController>()) return;
                done = true;
                if (status != null) { status.Stamina = Mathf.Max(0f, status.Stamina - amount); status.Sanity = Mathf.Max(0f, status.Sanity - amount * 0.2f); }
                hud?.Flash(message, 3f);
            }
        }
    }

    /// Level 1's "Flickering" (Backrooms Wiki): a faint electrical pulse, then every light dies for a while. Entities
    /// get bolder in the dark; the green exit lights over the corridor doors keep glowing.
    public sealed class Flickering : MonoBehaviour
    {
        ChunkStreamer streamer;
        AudioSource pulse;
        float next, endAt = -1f;

        public void Init(ChunkStreamer world)
        {
            streamer = world;
            pulse = gameObject.AddComponent<AudioSource>();
            pulse.clip = Audio.ProceduralAudio.Static();
            pulse.spatialBlend = 0f;
            next = Time.time + Random.Range(60f, 150f);
            Net.LevelSync.On("pulse", r => Pulse());
        }

        void Pulse()
        {
            pulse.pitch = 0.6f;
            pulse.PlayOneShot(pulse.clip, 0.25f * Meta.AudioMix.Effects);   // the warning
            UI.Captions.Say("[electrical pulse]");
        }

        void OnDestroy()
        {
            if (streamer != null && endAt > 0f) streamer.Blackout = 0f;
            if (pulse != null) Destroy(pulse);
            Net.LevelSync.Off("pulse");
        }

        void Update()
        {
            if (streamer == null || !Net.LevelSync.Decides) return;   // guests get the blackout from the owner
            if (endAt > 0f && Time.time > endAt) { streamer.Blackout = 0f; endAt = -1f; next = Time.time + Random.Range(90f, 240f); }
            if (endAt > 0f || Time.time < next) return;
            Pulse();
            Net.LevelSync.Send("pulse");
            Invoke(nameof(LightsOut), 1.6f);
            endAt = Time.time + 1.6f + Random.Range(8f, 22f);
        }

        void LightsOut()
        {
            if (streamer == null || endAt < 0f || streamer.Blackout >= 0.5f) return;
            streamer.Blackout = 1f;
            UI.Captions.Say("[the lights die]");
        }
    }
}

namespace Backrooms.Game
{
    /// Level 1's warehouse sounds and its crates. Pipes drip into puddles and somewhere a big pipe knocks; after the
    /// lights come back, a crate may be standing behind you that wasn't there; crates you've walked away from can be
    /// gone when you look again (Backrooms Wiki, Level 1).
    public sealed class Warehouse : MonoBehaviour
    {
        FirstPersonController player;
        ChunkStreamer streamer;
        AudioSource source;
        readonly List<GameObject> crates = new List<GameObject>();
        float nextDrip, nextClang, nextCrate, nextVanish;
        bool wasDark;

        public void Init(FirstPersonController player, ChunkStreamer world)
        {
            this.player = player;
            streamer = world;
            source = new GameObject("Warehouse Sounds").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 40f;
            float now = Time.time;
            nextDrip = now + 2f;
            nextClang = now + Random.Range(20f, 50f);
            nextCrate = now + Random.Range(60f, 140f);
        }

        void OnDestroy()
        {
            foreach (var c in crates) if (c != null) Destroy(c);
            if (source != null) Destroy(source.gameObject);
        }

        void Update()
        {
            if (player == null) return;
            float now = Time.time;
            var p = player.transform.position;
            if (now > nextDrip)
            {
                nextDrip = now + Random.Range(1.5f, 6f);
                source.transform.position = p + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * Random.Range(3f, 12f);
                source.pitch = Random.Range(0.8f, 1.25f);
                source.PlayOneShot(Audio.ProceduralAudio.Drip(), 0.35f * Meta.AudioMix.Ambience);
            }
            if (now > nextClang)
            {
                nextClang = now + Random.Range(35f, 90f);
                source.transform.position = p + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * Random.Range(15f, 35f) + Vector3.up * 3f;
                source.pitch = Random.Range(0.7f, 1.1f);
                source.PlayOneShot(Audio.ProceduralAudio.Clang(), 0.5f * Meta.AudioMix.Ambience);
                UI.Captions.Say("[a pipe clangs in the distance]");
            }
            // A crate behind you once the lights come back (or now and then anyway).
            bool dark = streamer.Blackout > 0.5f;
            if ((wasDark && !dark) || now > nextCrate)
            {
                nextCrate = now + Random.Range(90f, 200f);
                SpawnBehind();
            }
            wasDark = dark;
            if (now > nextVanish)
            {
                nextVanish = now + 4f;
                var cam = player.GetComponentInChildren<Camera>();
                for (int i = crates.Count - 1; i >= 0; i--)
                {
                    var c = crates[i];
                    if (c == null) { crates.RemoveAt(i); continue; }
                    var v = cam.WorldToViewportPoint(c.transform.position + Vector3.up * 0.4f);
                    bool seen = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
                    if (!seen && Vector3.Distance(c.transform.position, p) > 18f && Random.value < 0.35f) { Destroy(c); crates.RemoveAt(i); }
                }
            }
        }

        void SpawnBehind()
        {
            var behind = player.transform.position - player.transform.forward * Random.Range(3f, 6f) + player.transform.right * Random.Range(-2f, 2f);
            if (!Physics.Raycast(behind + Vector3.up * 1.2f, Vector3.down, out var floor, 2f)) return;
            if (Physics.CheckBox(floor.point + Vector3.up * 0.45f, Vector3.one * 0.45f)) return;
            var pose = Matrix4x4.TRS(floor.point, Quaternion.Euler(0f, Random.value * 360f, 0f), Vector3.one);
            int id = -1 - Random.Range(0, 1 << 30);   // not remembered: these come and go
            crates.Add(streamer.CreateCrate(transform, null, pose, id));
        }
    }
}
