using System.Collections.Generic;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// Cake or a drink on a party table in Level Fun. It helps your body and hurts your mind.
    public sealed class PartyFood : MonoBehaviour
    {
        public bool Drink;
        public string Label => Drink ? "Drink from the bottle" : "Eat a slice of cake";
    }

    /// Level Fun =) (after the Backrooms Wiki's Level Fun; the model is hand-made for this game). Besides
    /// the Partygoers (FearDirector), the party itself is what's wrong: music and laughter that are
    /// always somewhere else, balloons that drift closer whenever you look away, smiles on walls you just turned
    /// from, and food you shouldn't eat. Some of the exit signs are real.
    public sealed class PartyLevel : MonoBehaviour
    {
        FirstPersonController player;
        Camera cam;
        ChunkStreamer.StaticInfo info;
        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<AudioSource> music = new List<AudioSource>();
        readonly List<Transform> balloons = new List<Transform>();
        readonly List<GameObject> smiles = new List<GameObject>();
        AudioSource voices;
        float nextLaugh, nextSmile, nextShare;
        Vector3[] shown;   // co-op guests: where the owner's game says the balloons are

        public void Init(FirstPersonController body, ChunkStreamer world, uint worldSeed)
        {
            player = body;
            cam = body.GetComponentInChildren<Camera>();
            info = world.Static;
            if (info == null) return;

            // A few of the exit signs really are exits (which ones depends on the world).
            var signs = new List<Vector3>(info.ExitSigns);
            int real = Mathf.Min(3, signs.Count);
            for (int i = 0; i < real; i++)
            {
                int pick = (int)(WorldHash.Value(worldSeed, i, 0, 0xF0E) * signs.Count) % signs.Count;
                var s = signs[pick];
                signs.RemoveAt(pick);
                // The floor under the sign: the lowest thing below it (not the door frame the sign sits on).
                float floorY = float.MaxValue;
                foreach (var h in Physics.RaycastAll(s + Vector3.up * 0.1f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore)) floorY = Mathf.Min(floorY, h.point.y);
                if (floorY == float.MaxValue) continue;
                var t = new GameObject("Exit (a real one)");
                t.transform.position = new Vector3(s.x, floorY + 1.1f, s.z);
                var box = t.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.4f, 2.2f, 1.4f);
                t.AddComponent<LevelTrigger>().Target = i % 2 == 0 ? "0" : "1";
                spawned.Add(t);
            }
            // The food.
            foreach (var c in info.Cakes) Food(c, false);
            foreach (var c in info.Bottles) Food(c, true);

            // Road flares someone left behind.
            for (int i = 0; i < 5; i++)
            {
                var l = info.Lights[(int)(WorldHash.Value(worldSeed, i, 1, 0xF1A) * info.Lights.Count) % info.Lights.Count];
                float floor = float.MaxValue;
                foreach (var h in Physics.RaycastAll(l, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore)) floor = Mathf.Min(floor, h.point.y);
                if (floor == float.MaxValue) continue;
                var at = new Vector3(l.x, floor, l.z);
                int id = Pickup.IdAt(worldSeed, at);
                if (world.IsPicked != null && world.IsPicked(id)) continue;
                var go = new GameObject("Flare");
                go.transform.position = at;
                var flare = new BoxMeshBuilder(at, world.Paints.Length);
                PropLibrary.Build(new PropSpec { Kind = PropKind.Flare, Pose = Matrix4x4.Translate(at) }, flare, 3f);
                go.AddComponent<MeshFilter>().sharedMesh = flare.ToMesh("Flare");
                go.AddComponent<MeshRenderer>().sharedMaterials = world.Paints;
                var grab = go.AddComponent<SphereCollider>();
                grab.isTrigger = true; grab.radius = 0.35f; grab.center = Vector3.up * 0.15f;
                var pick = go.AddComponent<Pickup>();
                pick.Kind = PropKind.Flare;
                pick.Id = id;
                spawned.Add(go);
            }

            // The party you can hear but never reach.
            for (int i = 0; i < 2; i++)
            {
                var src = new GameObject("Party Music").AddComponent<AudioSource>();
                src.clip = Audio.ProceduralAudio.PartyMusic();
                src.loop = true;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = 55f;
                src.volume = 0f;
                src.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1400f;   // through the walls
                src.transform.position = FarSpot(30f);
                src.timeSamples = Random.Range(0, src.clip.samples);
                src.Play();
                music.Add(src);
                spawned.Add(src.gameObject);
            }
            voices = new GameObject("Party Voices").AddComponent<AudioSource>();
            voices.spatialBlend = 1f;
            voices.maxDistance = 50f;
            voices.rolloffMode = AudioRolloffMode.Linear;
            spawned.Add(voices.gameObject);
            nextLaugh = Time.time + Random.Range(15f, 30f);
            nextSmile = Time.time + Random.Range(20f, 35f);

            // Balloons that come closer when you aren't looking.
            var paints = world.Paints;
            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject("Balloon");
                var mesh = new BoxMeshBuilder(Vector3.zero, paints.Length);
                var colour = new[] { PropPaint.Red, PropPaint.Yellow, PropPaint.Blue, PropPaint.Green, PropPaint.NeonPink }[i % 5];
                PropLibrary.Balloon(Matrix4x4.identity, new Vector3(0f, -1.1f, 0f), Vector3.zero, colour, mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh("Balloon");
                go.AddComponent<MeshRenderer>().sharedMaterials = paints;
                go.transform.position = FarSpot(15f) + Vector3.up * 1.9f;
                balloons.Add(go.transform);
                spawned.Add(go);
            }
            // Co-op: the balloons are the owner's to move (towards whoever is nearest, while nobody looks).
            Net.LevelSync.On("balloons", r =>
            {
                r.ReadValueSafe(out Vector3[] at);
                shown = at;
            });
            Net.LevelSync.On("pop", r => { r.ReadValueSafe(out Vector3 at); Pop(at); });
        }

        void OnDestroy()
        {
            foreach (var g in spawned) if (g != null) Destroy(g);
            foreach (var g in smiles) if (g != null) Destroy(g);
            Net.LevelSync.Off("balloons");
            Net.LevelSync.Off("pop");
        }

        void Food(Vector3 at, bool drink)
        {
            var go = new GameObject(drink ? "Bottle" : "Cake");
            go.transform.position = at;
            var hit = go.AddComponent<SphereCollider>();
            hit.isTrigger = true;
            hit.radius = drink ? 0.2f : 0.35f;
            go.AddComponent<PartyFood>().Drink = drink;
            spawned.Add(go);
        }

        /// A spot by one of the lights (so, in a room), at least `min` metres from you.
        Vector3 FarSpot(float min) => FarSpot(player.transform.position, min);

        Vector3 FarSpot(Vector3 p, float min)
        {
            for (int i = 0; i < 30; i++)
            {
                var l = info.Lights[Random.Range(0, info.Lights.Count)];
                if (Vector3.Distance(new Vector3(l.x, p.y, l.z), p) < min) continue;
                return new Vector3(l.x, p.y, l.z);
            }
            return p + Vector3.forward * min;
        }

        void Pop(Vector3 at)
        {
            voices.transform.position = at;
            voices.pitch = 1f;
            voices.PlayOneShot(Audio.ProceduralAudio.Pop(), 0.6f * Meta.AudioMix.Effects);
            UI.Captions.Say("[a balloon pops]");
        }

        bool Seen(Vector3 at)
        {
            var v = cam.WorldToViewportPoint(at);
            if (v.z <= 0f || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f) return false;
            return !Physics.Linecast(cam.transform.position, at, ~0, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            if (info == null || player == null || info.Lights.Count == 0) return;
            float dt = Time.deltaTime;
            var p = player.transform.position;

            // Music: always somewhere else. Get close and it stops, then starts up again further off.
            foreach (var m in music)
            {
                bool tooClose = Vector3.Distance(m.transform.position, p) < 14f;
                m.volume = Mathf.MoveTowards(m.volume, tooClose ? 0f : 0.35f * Meta.AudioMix.Ambience, dt * (tooClose ? 0.8f : 0.15f));
                if (tooClose && m.volume <= 0.01f) m.transform.position = FarSpot(35f);
            }
            if (Time.time > nextLaugh)
            {
                nextLaugh = Time.time + Random.Range(25f, 60f);
                voices.transform.position = FarSpot(20f) + Vector3.up * 1.5f;
                voices.pitch = Random.Range(0.85f, 1.15f);
                voices.PlayOneShot(Audio.ProceduralAudio.Laughter(), 0.5f * Meta.AudioMix.Creatures);
                UI.Captions.Say("[laughter, somewhere]");
            }

            // Balloons: still while anyone looks at them; closer to whoever is nearest when nobody does. Touch one and it pops.
            if (Net.LevelSync.Decides)
            {
                for (int i = 0; i < balloons.Count; i++)
                {
                    var b = balloons[i];
                    var near = Players.Nearest(b.position);
                    if (near == null) continue;
                    var to = near.Position + Vector3.up * 1.9f - b.position;
                    float bob = Mathf.Sin(Time.time * 1.3f + i * 1.7f) * 0.05f;
                    if (!Players.AnySees(b.position) && to.magnitude > 2.5f)
                    {
                        var step = to.normalized * 0.6f * dt;
                        if (!Physics.SphereCast(b.position, 0.2f, step.normalized, out _, 0.4f, ~0, QueryTriggerInteraction.Ignore)) b.position += step;
                    }
                    b.position = new Vector3(b.position.x, near.Position.y + 1.9f + bob, b.position.z);
                    if (to.magnitude < 1.1f)
                    {
                        var at = b.position;
                        Pop(at);
                        Net.LevelSync.Send("pop", w => w.WriteValueSafe(at));
                        b.position = FarSpot(near.Position, 25f) + Vector3.up * 1.9f;
                    }
                }
                if (Time.time >= nextShare)
                {
                    nextShare = Time.time + 0.2f;
                    var at = new Vector3[balloons.Count];
                    for (int i = 0; i < at.Length; i++) at[i] = balloons[i].position;
                    Net.LevelSync.Send("balloons", w => w.WriteValueSafe(at), Unity.Netcode.NetworkDelivery.Unreliable);
                }
            }
            else if (shown != null)
                for (int i = 0; i < balloons.Count && i < shown.Length; i++)
                {
                    var b = balloons[i];
                    b.position = (b.position - shown[i]).sqrMagnitude > 25f ? shown[i] : Vector3.Lerp(b.position, shown[i], 1f - Mathf.Exp(-dt * 6f));
                }

            // A smile on the wall behind you.
            if (Time.time > nextSmile && info.SmileDecal != null)
            {
                nextSmile = Time.time + Random.Range(20f, 45f);
                var eye = cam.transform.position;
                var back = -player.transform.forward + player.transform.right * Random.Range(-0.6f, 0.6f);
                if (Physics.Raycast(eye, back.normalized, out var wall, 9f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(wall.normal.y) < 0.2f && !Seen(wall.point))
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(q.GetComponent<Collider>());
                    q.name = "=)";
                    q.transform.position = wall.point + wall.normal * 0.01f + Vector3.up * Random.Range(-0.3f, 0.4f);
                    q.transform.rotation = Quaternion.LookRotation(-wall.normal);
                    q.transform.localScale = Vector3.one * Random.Range(0.6f, 1.1f);
                    q.GetComponent<MeshRenderer>().sharedMaterial = info.SmileDecal;
                    smiles.Add(q);
                    if (smiles.Count > 12) { Destroy(smiles[0]); smiles.RemoveAt(0); }
                }
            }
        }
    }

    /// Now and then, in the ordinary levels, a single balloon is floating somewhere ahead. Walk into it and you're
    /// at the party (Backrooms Wiki: touching party things takes you to Level Fun).
    public sealed class PartyLure : MonoBehaviour
    {
        FirstPersonController player;
        ChunkStreamer world;
        GameObject balloon;
        float next, until;

        public void Init(FirstPersonController body, ChunkStreamer streamer)
        {
            player = body;
            world = streamer;
            next = Time.time + Random.Range(240f, 600f);
            Net.LevelSync.On("lure", r => { r.ReadValueSafe(out Vector3 at); Make(at); });
        }

        void OnDestroy()
        {
            if (balloon != null) Destroy(balloon);
            Net.LevelSync.Off("lure");
        }

        void Update()
        {
            if (player == null) return;
            if (balloon != null)
            {
                balloon.transform.position += Vector3.up * Mathf.Sin(Time.time * 1.2f) * 0.002f;
                if (Time.time > until) { Destroy(balloon); balloon = null; }
                return;
            }
            if (Time.time < next || !Net.LevelSync.Decides) return;
            next = Time.time + Random.Range(300f, 720f);
            if (Random.value > 0.5f) return;
            // Somewhere ahead of one of the living, down a clear line of sight, on their floor.
            var who = Players.Pick(Random.value);
            if (who == null) return;
            var ahead = new Vector3(who.Forward.x, 0f, who.Forward.z).normalized;
            var eye = who.Position + Vector3.up * 1.6f;
            var dir = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * ahead;
            float d = Physics.Raycast(eye, dir, out var hit, 16f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance - 1f : 16f;
            if (d < 8f) return;
            var at = eye + dir * d;
            Make(at);
            Net.LevelSync.Send("lure", w => w.WriteValueSafe(at));
        }

        void Make(Vector3 at)
        {
            if (balloon != null) Destroy(balloon);
            balloon = new GameObject("A Balloon");
            var mesh = new BoxMeshBuilder(Vector3.zero, world.Paints.Length);
            PropLibrary.Balloon(Matrix4x4.identity, new Vector3(0f, -1.3f, 0f), Vector3.zero, PropPaint.Red, mesh);
            balloon.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh("Balloon");
            balloon.AddComponent<MeshRenderer>().sharedMaterials = world.Paints;
            balloon.transform.position = at + Vector3.up * 0.3f;
            var trigger = balloon.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;
            trigger.center = Vector3.down * 0.6f;
            balloon.AddComponent<LevelTrigger>().Target = "fun";
            until = Time.time + 150f;
        }
    }
}
