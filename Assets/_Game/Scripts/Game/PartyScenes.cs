using System.Collections.Generic;
using Backrooms.Creatures;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// Partygoers outside Level Fun (wiki Entity 67: they decorate rooms to draw people in). On Levels 1, 2, 4 and 5 a
    /// corner is now and then done up for a party: linger in it or touch anything and a pack comes. And when a pack
    /// roams through any level, red balloons and out-of-tune music come first. The owner's game decides; guests see it.
    public sealed class PartyScenes : MonoBehaviour
    {
        const float Linger = 7f, Room = 4.5f, Touch = 1f;

        public static PartyScenes Instance { get; private set; }

        ChunkStreamer world;
        FearDirector director;
        bool traps, sprung;
        float next, lingered;
        GameObject trap;
        Vector3 trapAt;
        readonly List<Vector3> touchy = new List<Vector3>();
        readonly List<(GameObject go, float until, float bob)> loose = new List<(GameObject, float, float)>();
        AudioSource music;

        public void Init(ChunkStreamer streamer, FearDirector fear, string level)
        {
            Instance = this;
            world = streamer;
            director = fear;
            traps = level == "1" || level == "2" || level == "4" || level == "5";
            next = Time.time + Random.Range(200f, 480f);
            Net.LevelSync.On("ptrap", r => { r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out float yaw); Decorate(at, yaw); });
            Net.LevelSync.On("pspring", r => Spring());
            Net.LevelSync.On("pballoons", r => { r.ReadValueSafe(out Vector3[] at); Balloons(at); });
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            else return;   // the next level's scenes are already up and own the handlers
            Clear();
            Net.LevelSync.Off("ptrap"); Net.LevelSync.Off("pspring"); Net.LevelSync.Off("pballoons");
        }

        void Clear()
        {
            if (trap != null) Destroy(trap);
            if (music != null) Destroy(music.gameObject);
            foreach (var l in loose) if (l.go != null) Destroy(l.go);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Instance != this) { Clear(); return; }
            for (int i = loose.Count - 1; i >= 0; i--)
            {
                var l = loose[i];
                if (l.go == null || Time.time > l.until) { if (l.go != null) Destroy(l.go); loose.RemoveAt(i); continue; }
                l.go.transform.position += Vector3.up * (Mathf.Sin(Time.time * 1.2f + l.bob) * 0.04f * dt);
            }
            if (!Net.LevelSync.Decides || !traps) return;
            if (trap == null || sprung)
            {
                if (Time.time > next) { next = Time.time + Random.Range(300f, 600f); if (Random.value < 0.6f) Place(Players.Pick(Random.value)); }
                return;
            }
            PlayerAgent guest = null;
            bool touched = false, far = true;
            foreach (var p in Players.All)
            {
                if (p == null || !p.Alive) continue;
                var to = p.Position - trapAt;
                if (to.magnitude < 70f) far = false;
                if (Mathf.Abs(to.y) > 1.5f) continue;
                if (new Vector2(to.x, to.z).magnitude < Room) guest = p;
                foreach (var t in touchy)
                    if (Vector2.Distance(new Vector2(t.x, t.z), new Vector2(p.Position.x, p.Position.z)) < Touch) { touched = true; guest = p; }
                if (touched) break;
            }
            lingered = guest != null ? lingered + dt : Mathf.Max(0f, lingered - dt * 0.5f);
            if (guest != null && (touched || lingered > Linger))
            {
                Net.LevelSync.Send("pspring");
                Spring();
                director.SpawnParty(guest.Position, Random.Range(2, 5), 8, 18, 60f);
            }
            else if (far) { Destroy(trap); trap = null; }   // left behind
        }

        /// Does up a spot ahead of this player that nobody can see. False if there's no room for one.
        public bool Place(PlayerAgent who)
        {
            if (who == null || director == null) return false;
            director.Grid.Build(who.Position);
            var at = director.Grid.RandomSpot(9, 22, p => !Players.AnySees(p + Vector3.up * 1.2f) && !Physics.CheckSphere(p + Vector3.up, 0.8f, ~0, QueryTriggerInteraction.Ignore)
                && Physics.Raycast(p + Vector3.up, Vector3.down, out var f, 1.3f, ~0, QueryTriggerInteraction.Ignore) && f.normal.y > 0.9f);
            if (!at.HasValue) return false;
            // Its back to the nearest wall.
            float yaw = 0f, best = 99f;
            for (int i = 0; i < 4; i++)
                if (Physics.Raycast(at.Value + Vector3.up * 1.5f, Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward, out var w, 6f, ~0, QueryTriggerInteraction.Ignore) && w.distance < best) { best = w.distance; yaw = i * 90f; }
            Decorate(at.Value, yaw);
            var a = at.Value;
            Net.LevelSync.Send("ptrap", w => { w.WriteValueSafe(a); w.WriteValueSafe(yaw); });
            return true;
        }

        void Decorate(Vector3 at, float yaw)
        {
            if (trap != null) Destroy(trap);
            touchy.Clear();
            trapAt = at;
            sprung = false;
            lingered = 0f;
            trap = new GameObject("A Party");
            trap.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var rng = new System.Random(Mathf.RoundToInt(at.x * 7f) * 31 + Mathf.RoundToInt(at.z * 7f));   // the same on every game
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 up = Vector3.up, fwd = trap.transform.forward;
            bool Clear(Vector3 a, Vector3 b) => !Physics.Linecast(a, b, ~0, QueryTriggerInteraction.Ignore);
            float wall = Physics.Raycast(at + up * 1.5f, fwd, out var back, 6f, ~0, QueryTriggerInteraction.Ignore) ? back.distance : 0f;
            float ceiling = Physics.Raycast(at + up, up, out var top, 6f, ~0, QueryTriggerInteraction.Ignore) ? top.distance + 1f : 2.7f;

            var mesh = new BoxMeshBuilder(at, world.Paints.Length);
            // A table with a cake on it, and party hats.
            var t = Matrix4x4.TRS(at + fwd * 0.2f, trap.transform.rotation, Vector3.one);
            void Box(PropPaint c, float x, float y, float z, float sx, float sy, float sz) => mesh.AddOrientedBox(t, new Vector3(x, y, z), new Vector3(sx, sy, sz), (int)c);
            Box(PropPaint.White, 0, 0.74f, 0, 1.3f, 0.04f, 0.7f);
            Box(PropPaint.White, 0, 0.6f, 0.35f, 1.32f, 0.28f, 0.01f); Box(PropPaint.White, 0, 0.6f, -0.35f, 1.32f, 0.28f, 0.01f);   // the cloth
            foreach (float x in new[] { -0.6f, 0.6f }) foreach (float z in new[] { -0.3f, 0.3f }) Box(PropPaint.Metal, x, 0.36f, z, 0.04f, 0.72f, 0.04f);
            Box(PropPaint.Red, 0, 0.82f, 0, 0.34f, 0.12f, 0.34f); Box(PropPaint.White, 0, 0.89f, 0, 0.36f, 0.03f, 0.36f); Box(PropPaint.Red, 0, 0.95f, 0, 0.2f, 0.1f, 0.2f);
            for (int i = 0; i < 5; i++) Box(PropPaint.Yellow, -0.07f + 0.035f * i, 1.03f, R(-0.06f, 0.06f), 0.012f, 0.07f, 0.012f);
            for (int i = 0; i < 4; i++) Box(new[] { PropPaint.Blue, PropPaint.Green, PropPaint.NeonPink, PropPaint.Yellow }[i], -0.5f + 0.32f * i + (i > 1 ? 0.08f : 0f), 0.83f, -0.2f, 0.09f, 0.14f, 0.09f);
            touchy.Add(at + fwd * 0.2f);
            var solid = trap.AddComponent<BoxCollider>();   // you can keep it between you and them
            solid.center = new Vector3(0f, 0.38f, 0.2f);
            solid.size = new Vector3(1.3f, 0.76f, 0.7f);
            // Streamers swagged across the ceiling.
            var high = at + up * (ceiling - 0.1f);
            for (int s = 0; s < 4; s++)
            {
                var arm = Quaternion.Euler(0f, s * 45f + R(-10f, 10f), 0f) * Vector3.forward * 1.7f;
                if (!Clear(high - up * 0.2f, high - up * 0.2f + arm) || !Clear(high - up * 0.2f, high - up * 0.2f - arm)) continue;
                Vector3 P(float u) => Vector3.Lerp(high - arm, high + arm, u) - up * (1.1f * u * (1f - u));
                for (int k = 0; k < 10; k++)
                {
                    Vector3 p0 = P(k / 10f), p1 = P((k + 1) / 10f);
                    mesh.AddOrientedBox(Matrix4x4.TRS((p0 + p1) / 2f, Quaternion.LookRotation(p1 - p0) * Quaternion.Euler(0f, 0f, k * 40f), Vector3.one), Vector3.zero, new Vector3(0.06f, 0.004f, (p1 - p0).magnitude + 0.01f), (int)new[] { PropPaint.Red, PropPaint.Yellow, PropPaint.Blue, PropPaint.NeonPink }[s]);
                }
            }
            // The poster: a yellow sheet with a smile on it.
            if (wall > 0f)
            {
                var post = Matrix4x4.TRS(at + up * 1.55f + fwd * (wall - 0.012f), trap.transform.rotation, Vector3.one);
                void Sheet(PropPaint c, float x, float y, float z, float sx, float sy) => mesh.AddOrientedBox(post, new Vector3(x, y, z), new Vector3(sx, sy, 0.006f), (int)c);
                Sheet(PropPaint.Yellow, 0, 0, 0, 0.6f, 0.8f);
                Sheet(PropPaint.Dark, -0.1f, 0.14f, -0.006f, 0.05f, 0.14f); Sheet(PropPaint.Dark, 0.1f, 0.14f, -0.006f, 0.05f, 0.14f);
                for (int i = 0; i < 13; i++) { float ang = (i / 12f - 0.5f) * 2.3f; Sheet(PropPaint.Dark, Mathf.Sin(ang) * 0.2f, 0.06f - Mathf.Cos(ang) * 0.2f, -0.006f, 0.055f, 0.045f); }
                for (int i = 0; i < 4; i++) Sheet(PropPaint.Red, R(-0.2f, 0.2f), R(-0.36f, -0.15f), -0.008f, 0.02f, R(0.06f, 0.2f));
            }
            var deco = new GameObject("Decorations");
            deco.transform.SetParent(trap.transform, false);
            deco.transform.SetPositionAndRotation(at, Quaternion.identity);
            deco.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh("Party");
            deco.AddComponent<MeshRenderer>().sharedMaterials = world.Paints;
            for (int i = 0; i < 6; i++)
            {
                var p = at + Quaternion.Euler(0f, i * 60f + R(-20f, 20f), 0f) * Vector3.forward * R(1.3f, 2.1f);
                float height = R(1.5f, 2f);
                if (!Clear(at + up * 1.6f, p + up * 1.6f)) continue;
                Balloon(p + up * height, i % 2 == 0 ? PropPaint.Red : new[] { PropPaint.Yellow, PropPaint.Blue, PropPaint.Green }[i / 2]).transform.SetParent(trap.transform, true);
                touchy.Add(p);
            }
        }

        GameObject Balloon(Vector3 at, PropPaint colour)
        {
            var go = new GameObject("Balloon");
            var mesh = new BoxMeshBuilder(Vector3.zero, world.Paints.Length);
            PropLibrary.Balloon(Matrix4x4.identity, new Vector3(0f, -1.1f, 0f), Vector3.zero, colour, mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh("Balloon");
            go.AddComponent<MeshRenderer>().sharedMaterials = world.Paints;
            go.transform.position = at;
            return go;
        }

        void Spring()
        {
            sprung = true;
            if (trap == null) return;
            foreach (Transform t in trap.transform)
                if (t.name == "Balloon") { AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Pop(), t.position, 0.6f * Meta.AudioMix.Effects); Destroy(t.gameObject); }
            Music(trapAt + Vector3.up * 1.5f, 40f);
            UI.Captions.Say("[every balloon pops at once; party music starts]");
        }

        /// Red balloons drift out ahead of a roaming pack, with its music.
        public void Announce(List<Vector3> at)
        {
            var spots = at.ToArray();
            Balloons(spots);
            Net.LevelSync.Send("pballoons", w => w.WriteValueSafe(spots));
        }

        void Balloons(Vector3[] at)
        {
            foreach (var p in at) loose.Add((Balloon(p + Vector3.up * 1.8f, PropPaint.Red), Time.time + 60f, p.x));
            if (at.Length > 0) Music(at[0] + Vector3.up * 1.5f, 45f);
            UI.Captions.Say("[party music, out of tune, somewhere near]");
        }

        void Music(Vector3 at, float seconds)
        {
            if (music != null) Destroy(music.gameObject);
            music = new GameObject("Party Music").AddComponent<AudioSource>();
            music.transform.position = at;
            music.clip = Audio.ProceduralAudio.PartyMusic();
            music.pitch = 0.82f;
            music.loop = true;
            music.spatialBlend = 1f;
            music.maxDistance = 35f;
            music.rolloffMode = AudioRolloffMode.Linear;
            music.volume = 0.45f * Meta.AudioMix.Creatures;
            music.Play();
            Destroy(music.gameObject, seconds);
        }
    }
}
