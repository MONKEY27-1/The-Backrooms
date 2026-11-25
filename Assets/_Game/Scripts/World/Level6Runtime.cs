using System.Collections;
using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.World
{
    /// The World's Quietest Room: someone you can't see, barely breathing the words, begs you not to touch the switch.
    public sealed class QuietRoom : MonoBehaviour
    {
        public static readonly HashSet<Vector3Int> Silenced = new HashSet<Vector3Int>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Silenced.Clear();

        AudioSource voice;
        float lastSpoke = -99f;
        public Vector3Int Key => Vector3Int.RoundToInt(transform.position);

        void Start()
        {
            if (Silenced.Contains(Key)) return;
            voice = Level5Sound.Source(gameObject, 6f);
            voice.clip = Audio.ProceduralAudio.Whisper();
            voice.loop = true;
            voice.volume = 0.25f * Meta.AudioMix.Effects;
            voice.Play();
        }

        public void Silence()
        {
            Silenced.Add(Key);
            if (voice != null) voice.Stop();
        }

        void OnTriggerEnter(Collider other)
        {
            var body = other.GetComponentInParent<FirstPersonController>();
            if (body == null || Silenced.Contains(Key) || Time.time - lastSpoke < 30f) return;
            lastSpoke = Time.time;
            UI.Captions.Say("[barely a breath, right beside you: please... don't. please don't touch the switch.]");
            body.GetComponent<PlayerHud>()?.Flash("Someone is in here with you. They're begging you not to touch the switch.", 5f);
        }
    }

    /// The switch in the Quietest Room. Flip it and the room floods with light for a heartbeat, someone screams, and
    /// something in the dark comes looking.
    public sealed class QuietSwitch : MonoBehaviour, IInteractable
    {
        bool flipped;
        public string Prompt => flipped ? "" : "Flip the switch";

        public void Interact()
        {
            if (flipped) return;
            flipped = true;
            StartCoroutine(Flip());
        }

        IEnumerator Flip()
        {
            var room = FindNearestRoom();
            var a = Level5Sound.Source(gameObject, 30f);
            a.PlayOneShot(Audio.ProceduralAudio.DoorClick(), 0.9f * Meta.AudioMix.Effects);
            var go = new GameObject("Blinding light");
            go.transform.position = transform.position + transform.forward * 1.5f;
            go.AddHDLight(LightType.Point);
            var l = go.GetComponent<Light>();
            l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            l.intensity = 60000f;
            l.range = 25f;
            yield return new WaitForSeconds(0.35f);
            Destroy(go);
            a.PlayOneShot(Audio.ProceduralAudio.Static(), 1f * Meta.AudioMix.Effects);
            UI.Captions.Say("[a scream, inches from your ear, cut off]");
            room?.Silence();
            var body = FindFirstObjectByType<FirstPersonController>();
            var status = body != null ? body.GetComponent<PlayerStatus>() : null;
            if (status != null) status.Sanity = Mathf.Max(0f, status.Sanity - 0.5f);
            body?.GetComponent<PlayerHud>()?.Flash("You shouldn't have done that.", 4f);
            yield return new WaitForSeconds(1.5f);
            FindFirstObjectByType<Creatures.FearDirector>()?.ForceHunt();
        }

        QuietRoom FindNearestRoom()
        {
            QuietRoom best = null;
            float bd = 8f;
            foreach (var r in FindObjectsByType<QuietRoom>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(r.transform.position, transform.position);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }
    }

    /// Chalk marks you draw on Level 6's walls: an arrow the way you were walking, or a cross. Faint, but they catch
    /// what little light there is. They stay until you leave the level.
    public static class Chalk
    {
        static readonly List<GameObject> marks = new List<GameObject>();
        static Material chalk;
        const int MaxMarks = 80;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => marks.Clear();

        public static void Clear()
        {
            foreach (var m in marks) if (m != null) { Object.Destroy(m.GetComponent<MeshFilter>()?.sharedMesh); Object.Destroy(m); }
            marks.Clear();
        }

        /// Draws on the wall in front of `eye`, pointing along `walking` (or a cross if you were standing still).
        public static bool Draw(Vector3 eye, Vector3 forward, Vector3 walking)
        {
            if (!Physics.Raycast(eye, forward, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.normal.y) > 0.3f) return false;
            if (chalk == null) chalk = HdMat.Emissive(new Color(0.92f, 0.92f, 0.86f), 6f);
            var normal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            var tangent = Vector3.Cross(Vector3.up, normal);
            float along = Vector3.Dot(new Vector3(walking.x, 0f, walking.z), tangent);
            var at = hit.point + normal * 0.004f;
            var pose = Matrix4x4.TRS(at, Quaternion.LookRotation(-normal, Vector3.up), Vector3.one);   // local +x along the wall, +z into it
            var b = new BoxMeshBuilder(at, 1);
            void Stroke(float x0, float y0, float x1, float y1)
            {
                var mid = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f);
                float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                float ang = Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg;
                b.AddOrientedBox(pose * Matrix4x4.TRS(mid, Quaternion.Euler(0f, 0f, ang), Vector3.one), Vector3.zero, new Vector3(len, 0.018f, 0.004f), 0);
            }
            if (Mathf.Abs(along) < 0.25f) { Stroke(-0.12f, -0.12f, 0.12f, 0.12f); Stroke(-0.12f, 0.12f, 0.12f, -0.12f); }
            else
            {
                // LookRotation(-normal) puts local +x at -tangent, so the arrow's sign flips.
                float s = along > 0f ? -1f : 1f;
                Stroke(-0.2f * s, 0f, 0.2f * s, 0f);
                Stroke(0.2f * s, 0f, 0.1f * s, 0.08f);
                Stroke(0.2f * s, 0f, 0.1f * s, -0.08f);
            }
            var go = new GameObject("Chalk mark");
            go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Chalk");
            go.AddComponent<MeshRenderer>().sharedMaterial = chalk;
            marks.Add(go);
            if (marks.Count > MaxMarks) { var old = marks[0]; marks.RemoveAt(0); if (old != null) { Object.Destroy(old.GetComponent<MeshFilter>().sharedMesh); Object.Destroy(old); } }
            return true;
        }
    }

    /// Level 6 while you're in it: your sounds come back to you from up ahead (the mimics), and now and then a
    /// corridor you've left quietly becomes a wall: never one that would shut you in.
    public sealed class Level6Events : MonoBehaviour
    {
        FirstPersonController player;
        PlayerTools tools;
        readonly List<Vector2Int> trail = new List<Vector2Int>();
        readonly List<GameObject> seals = new List<GameObject>();
        float nextSeal, nextMimic, mimicUntil, nextStep;
        bool lastFlashlight;
        Vector3 mimicAt;
        AudioClip[] steps;

        public void Init(FirstPersonController body)
        {
            player = body;
            tools = body.GetComponent<PlayerTools>();
            nextSeal = Time.time + Random.Range(40f, 70f);
            nextMimic = Time.time + Random.Range(30f, 60f);
            steps = Audio.ProceduralAudio.Footsteps();
            Chalk.Clear();
        }

        void OnDestroy()
        {
            foreach (var s in seals) if (s != null) { Destroy(s.GetComponent<MeshFilter>()?.sharedMesh); Destroy(s); }
            Chalk.Clear();
        }

        static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / MazeLayout.CellSize), Mathf.FloorToInt(p.z / MazeLayout.CellSize));
        static Vector3 CenterOf(Vector2Int c, float y) => new Vector3((c.x + 0.5f) * MazeLayout.CellSize, y, (c.y + 0.5f) * MazeLayout.CellSize);

        void Update()
        {
            if (player == null) return;
            var cell = CellOf(player.transform.position);
            if (trail.Count == 0 || trail[trail.Count - 1] != cell) { trail.Add(cell); if (trail.Count > 60) trail.RemoveAt(0); }
            Mimics();
            if (Time.time >= nextSeal && !Net.Coop.Active) { nextSeal = Time.time + Random.Range(35f, 70f); TrySeal(); }
        }

        // ---------- the mimics ----------

        void Mimics()
        {
            var flat = player.transform.forward;
            bool moving = player.GetComponent<CharacterController>().velocity.sqrMagnitude > 0.5f;
            if (Time.time >= nextMimic && moving)
            {
                nextMimic = Time.time + Random.Range(50f, 110f);
                mimicUntil = Time.time + Random.Range(8f, 14f);
                mimicAt = player.transform.position + flat * Random.Range(12f, 18f) + player.transform.right * Random.Range(-4f, 4f);
            }
            // Footsteps a beat behind yours, from somewhere ahead. When you stop, they stop.
            if (Time.time < mimicUntil && moving && Time.time >= nextStep)
            {
                nextStep = Time.time + (player.Running ? 0.36f : 0.55f) + 0.08f;
                AudioSource.PlayClipAtPoint(steps[Random.Range(0, steps.Length)], mimicAt, 0.5f * Meta.AudioMix.Effects);
            }
            // Your flashlight's click, answered.
            if (tools != null && tools.FlashlightOn != lastFlashlight)
            {
                lastFlashlight = tools.FlashlightOn;
                if (Random.value < 0.4f) StartCoroutine(Echo(Audio.ProceduralAudio.DoorClick(), player.transform.position + flat * Random.Range(8f, 15f), Random.Range(1.2f, 2.5f)));
            }
        }

        IEnumerator Echo(AudioClip clip, Vector3 at, float delay)
        {
            yield return new WaitForSeconds(delay);
            var go = new GameObject("Mimic");
            go.transform.position = at;
            var a = Level5Sound.Source(go, 25f);
            a.pitch = 1.8f;
            a.PlayOneShot(clip, 0.6f * Meta.AudioMix.Effects);
            Destroy(go, 2f);
        }

        // ---------- the way back closes ----------

        /// Picks a corridor cell well behind you on your trail, out of sight, and walls it off across your direction of
        /// travel. If a flood fill from where you stand can't still get well away, the wall comes straight back down.
        void TrySeal()
        {
            if (trail.Count < 10) return;
            var cam = Camera.main;
            for (int back = 6; back < Mathf.Min(trail.Count - 1, 16); back++)
            {
                int i = trail.Count - 1 - back;
                var c = trail[i];
                var center = CenterOf(c, player.transform.position.y + 1.2f);
                if (Vector3.Distance(center, player.transform.position) < 10f) continue;
                if (cam != null && Level5Sound.Seen(center, 80f)) continue;
                var step = trail[i + 1] - c;
                bool acrossX = step.x != 0;   // walking along x: the wall spans z
                if (Physics.CheckBox(center, new Vector3(0.5f, 1f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide)) continue;   // something's there (an exit, a prop)
                var wall = MakeSeal(center - Vector3.up * 1.2f, acrossX);
                Physics.SyncTransforms();
                if (CanGetAway(CellOf(player.transform.position), player.transform.position.y + 2.05f))   // above your head, so your own body never blocks the check { seals.Add(wall); return; }
                Destroy(wall.GetComponent<MeshFilter>().sharedMesh);
                Destroy(wall);
            }
        }

        GameObject MakeSeal(Vector3 floor, bool acrossX)
        {
            var go = new GameObject("Sealed corridor");
            go.transform.position = floor;
            var size = acrossX ? new Vector3(0.3f, 3.2f, 2.1f) : new Vector3(2.1f, 3.2f, 0.3f);
            var b = new BoxMeshBuilder(floor, 1);
            b.AddBox(new Bounds(floor + Vector3.up * 1.6f, size), BoxMeshBuilder.Faces.All, 0, 0, 0, 0, 0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Seal");
            go.AddComponent<MeshRenderer>().sharedMaterial = ChunkStreamer.Instance != null ? ChunkStreamer.Instance.WallMaterial : null;
            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.up * 1.6f;
            box.size = size;
            return go;
        }

        /// Breadth-first over 2 m cells (a line of sight between centres = a way through) out to 16 cells: true if you
        /// can still reach somewhere 12 cells away.
        static bool CanGetAway(Vector2Int start, float y)
        {
            var seen = new HashSet<Vector2Int> { start };
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (Mathf.Max(Mathf.Abs(c.x - start.x), Mathf.Abs(c.y - start.y)) >= 12) return true;
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (seen.Contains(n) || Mathf.Max(Mathf.Abs(n.x - start.x), Mathf.Abs(n.y - start.y)) > 16) continue;
                    var a = CenterOf(c, y);
                    if (Physics.Linecast(a, CenterOf(n, y), ~0, QueryTriggerInteraction.Ignore)) continue;
                    seen.Add(n);
                    q.Enqueue(n);
                }
            }
            return false;
        }
    }
}
