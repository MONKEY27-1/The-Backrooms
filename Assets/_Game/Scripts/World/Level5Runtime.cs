using System.Collections;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.World
{
    /// Something you can look at and press Interact on (the HUD shows Prompt).
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact();
    }

    static class Level5Sound
    {
        public static AudioSource Source(GameObject go, float far)
        {
            var a = go.AddComponent<AudioSource>();
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1f;
            a.maxDistance = far;
            a.playOnAwake = false;
            return a;
        }

        public static bool Seen(Vector3 at, float maxAngle)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var to = at - cam.transform.position;
            float d = to.magnitude;
            return Vector3.Angle(cam.transform.forward, to) < maxAngle && !Physics.Raycast(cam.transform.position, to / d, d - 0.5f, ~0, QueryTriggerInteraction.Ignore);
        }
    }

    /// A guest room door: mahogany, a brass number that follows no pattern. Interact opens or closes it. Now and then,
    /// when nobody is looking, it opens or closes by itself.
    public sealed class HotelDoor : MonoBehaviour, IInteractable
    {
        Transform hinge;
        float angle, target, nextLiving;
        AudioSource click;
        Mesh leafMesh;

        public string Prompt => Mathf.Approximately(target, 0f) ? "Open the door" : "Close the door";
        /// In the way (closed, or not open far enough to get through).
        public bool Shut => target > -50f;
        /// Swung open, by anyone (what nests behind it wakes).
        public event System.Action Opened;

        public void Build(SpecialSpec s, Material[] paints)
        {
            // s.Pose: the doorway's middle on the floor, +z into the corridor. The hinge is on the +x jamb; the leaf swings into the room.
            hinge = new GameObject("Hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = new Vector3(0.46f, 0f, 0f);
            var b = new BoxMeshBuilder(Vector3.zero, paints.Length);
            if (!ModelLibrary.Add(b, "L5DoorLeaf", Matrix4x4.identity))
                b.AddOrientedBox(Matrix4x4.identity, new Vector3(-0.46f, 1.03f, 0f), new Vector3(0.92f, 2.05f, 0.045f), (int)PropPaint.Mahogany);
            PropLibrary.RoomNumber(b, Matrix4x4.Translate(new Vector3(-0.46f, 0f, 0f)), s.Id, 0.034f);
            leafMesh = b.ToMesh("Hotel door");
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(hinge, false);
            leaf.AddComponent<MeshFilter>().sharedMesh = leafMesh;
            leaf.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var col = leaf.AddComponent<BoxCollider>();
            col.center = new Vector3(-0.46f, 1.03f, 0f);
            col.size = new Vector3(0.92f, 2.05f, 0.07f);
            uint h = (uint)s.Id * 2654435761u;
            float r = (h >> 8) % 100 / 100f;
            target = angle = r < 0.45f ? 0f : r < 0.8f ? -95f : -25f;   // closed, open, ajar
            hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
            click = Level5Sound.Source(gameObject, 14f);
            nextLiving = Time.time + Random.Range(4f, 12f);
        }

        void OnDestroy() { if (leafMesh != null) Destroy(leafMesh); }

        public void Interact() => Swing(Mathf.Approximately(target, 0f) ? -95f : 0f);

        void Swing(float to)
        {
            target = to;
            if (to < -50f) Opened?.Invoke();
            click.pitch = Random.Range(0.85f, 1.1f);
            click.PlayOneShot(Audio.ProceduralAudio.DoorClick(), 0.7f * Meta.AudioMix.Effects);
        }

        void Update()
        {
            if (!Mathf.Approximately(angle, target))
            {
                angle = Mathf.MoveTowards(angle, target, 150f * Time.deltaTime);
                hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            if (Time.time < nextLiving) return;
            nextLiving = Time.time + Random.Range(4f, 9f);
            var cam = Camera.main;
            if (cam == null || Net.Coop.Active) return;   // in co-op every player must see the same doors
            var mid = transform.position + Vector3.up * 1.2f;
            float d = Vector3.Distance(cam.transform.position, mid);
            if (d < 4f || d > 20f || Level5Sound.Seen(mid, 70f) || Random.value > 0.05f) return;
            Swing(Mathf.Approximately(target, 0f) ? (Random.value < 0.5f ? -25f : -95f) : 0f);   // a soft click, somewhere behind you
        }
    }

    /// One button on an elevator's panel.
    public sealed class ElevatorButton : MonoBehaviour, IInteractable
    {
        public int Floor;
        public HotelElevator Car;
        public string Prompt => $"Press {Floor}";
        public void Interact() => Car.Ride(Floor);
    }

    /// An antique copper-cage elevator. Floors 1-12 take you to another part of the hotel; 13 and above, nobody comes back.
    public sealed class HotelElevator : MonoBehaviour
    {
        static readonly int[] Floors = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 40, 382 };
        bool riding;
        Mesh panel;

        public void Build(Material[] paints)
        {
            // The panel on the car's left wall (local -x), facing in.
            var plate = Matrix4x4.TRS(new Vector3(-0.8f, 0f, 0.35f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            var b = new BoxMeshBuilder(Vector3.zero, paints.Length);
            b.AddOrientedBox(plate, new Vector3(0f, 1.3f, 0.01f), new Vector3(0.3f, 0.62f, 0.02f), (int)PropPaint.Brass);
            for (int i = 0; i < Floors.Length; i++)
            {
                var at = new Vector3(-0.09f + (i % 3) * 0.09f, 1.54f - (i / 3) * 0.1f, 0.025f);
                b.AddOrientedBox(plate, at, new Vector3(0.04f, 0.04f, 0.012f), (int)(Floors[i] >= 13 ? PropPaint.Red : PropPaint.White));
                PropLibrary.Label(b, plate * Matrix4x4.Translate(at + new Vector3(0f, -0.033f, 0.004f)), Floors[i].ToString(), 0.12f);
                var btn = new GameObject("Button " + Floors[i]);
                btn.transform.SetParent(transform, false);
                btn.transform.localPosition = plate.MultiplyPoint3x4(at);
                var sc = btn.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.035f;
                var eb = btn.AddComponent<ElevatorButton>();
                eb.Floor = Floors[i];
                eb.Car = this;
            }
            panel = b.ToMesh("Elevator panel");
            gameObject.AddComponent<MeshFilter>().sharedMesh = panel;
            gameObject.AddComponent<MeshRenderer>().sharedMaterials = paints;
        }

        void OnDestroy() { if (panel != null) Destroy(panel); }

        public void Ride(int floor)
        {
            if (riding) return;
            riding = true;
            StartCoroutine(Go(floor));
        }

        IEnumerator Go(int floor)
        {
            var a = Level5Sound.Source(gameObject, 12f);
            a.PlayOneShot(Audio.ProceduralAudio.Clang(), 0.6f * Meta.AudioMix.Effects);
            UI.Captions.Say("[the gate rattles shut; the cage lurches]");
            a.clip = Audio.ProceduralAudio.Machine();
            a.pitch = 0.45f;
            a.loop = true;
            a.volume = 0.5f * Meta.AudioMix.Effects;
            a.Play();
            yield return new WaitForSeconds(3f);
            if (floor <= 12)
                LevelTrigger.Fire("5", transform.position + new Vector3(floor * 64f + 7f, 0f, -floor * 96f));   // somewhere else in the hotel, the same for each floor
            else
            {
                UI.Captions.Say("[the cable sings, then snaps]");
                if (ChunkStreamer.Instance != null) ChunkStreamer.Instance.Blackout = 1f;
                yield return new WaitForSeconds(1.5f);
                if (ChunkStreamer.Instance != null) ChunkStreamer.Instance.Blackout = 0f;
                LevelTrigger.FirePit();
            }
            riding = false;
        }
    }

    /// One of the Beverly Room's doors. How many are really there changes every time you leave the ballroom and come
    /// back; each leads somewhere else in the hotel.
    public sealed class BallroomDoor : MonoBehaviour, IInteractable
    {
        public static int Visit = new System.Random().Next(100000);   // not UnityEngine.Random: it can't run in a static initializer
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Visit = new System.Random().Next(100000);

        int id, shown = -1;
        GameObject body;
        Mesh mesh;

        public string Prompt => "Open the door";

        public void Build(SpecialSpec s, Material[] paints)
        {
            id = s.Id;
            body = new GameObject("Door");
            body.transform.SetParent(transform, false);
            var b = new BoxMeshBuilder(Vector3.zero, paints.Length);
            foreach (float x in new[] { -0.52f, 0.52f }) b.AddOrientedBox(Matrix4x4.identity, new Vector3(x, 1.08f, 0.03f), new Vector3(0.1f, 2.16f, 0.06f), (int)PropPaint.Mahogany);
            b.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, 2.2f, 0.03f), new Vector3(1.14f, 0.12f, 0.07f), (int)PropPaint.Mahogany);
            if (!ModelLibrary.Add(b, "L5DoorLeaf", Matrix4x4.Translate(new Vector3(0.46f, 0f, 0.03f))))
                b.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, 1.03f, 0.03f), new Vector3(0.92f, 2.05f, 0.045f), (int)PropPaint.Mahogany);
            mesh = b.ToMesh("Ballroom door");
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var col = body.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.08f, 0.05f);
            col.size = new Vector3(1.1f, 2.16f, 0.1f);
            Refresh();
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }

        void Refresh()
        {
            shown = Visit;
            float fraction = 0.15f + 0.85f * WorldHash.Value((uint)Visit, 7, 7, 0xBA11);
            body.SetActive(WorldHash.Value((uint)Visit, id & 0xFFFF, id >> 16, 0xBA12) < fraction);
        }

        void Update() { if (shown != Visit) Refresh(); }

        public void Interact()
        {
            UI.Captions.Say("[the band stops mid-note]");
            LevelTrigger.Fire("5", transform.position + new Vector3((Visit % 997) * 13f, 0f, (id & 0x3FF) * 9f));
        }
    }

    /// The ballroom floor: leaving it (after a little while inside) changes which of its doors are there.
    public sealed class BallroomZone : MonoBehaviour
    {
        float enteredAt = -1f;

        void OnTriggerEnter(Collider other) { if (other.GetComponentInParent<FirstPersonController>() != null) enteredAt = Time.time; }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonController>() == null || enteredAt < 0f) return;
            if (Time.time - enteredAt > 3f) BallroomDoor.Visit++;
            enteredAt = -1f;
        }
    }

    /// Jazz from a gramophone in a room nobody is in. Walk in and the record stops. (The ballroom's never stops.)
    public sealed class Turntable : MonoBehaviour
    {
        AudioSource music;
        bool ballroom, inside;
        float stoppedAt = -1f;

        public void Build(SpecialSpec s)
        {
            ballroom = s.Size.y > 5f;
            music = Level5Sound.Source(gameObject, ballroom ? 36f : 14f);
            music.clip = Audio.ProceduralAudio.Gramophone();
            music.loop = true;
            music.volume = (ballroom ? 0.6f : 0.4f) * Meta.AudioMix.Effects;
            music.time = Random.value * music.clip.length;
            music.Play();
            if (ballroom) return;
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * s.Size.y / 2f;
            box.size = s.Size;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonController>() == null) return;
            inside = true;
            if (!music.isPlaying) return;
            music.Stop();
            music.PlayOneShot(Audio.ProceduralAudio.Static(), 0.15f * Meta.AudioMix.Effects);
            stoppedAt = Time.time;
            UI.Captions.Say("[the record stops]");
        }

        void OnTriggerExit(Collider other) { if (other.GetComponentInParent<FirstPersonController>() != null) inside = false; }

        void Update()
        {
            if (stoppedAt > 0f && !inside && Time.time - stoppedAt > 25f) { stoppedAt = -1f; music.Play(); }
        }
    }

    /// The boiler room's heat: thirst drains fast in here.
    public sealed class HeatZone : MonoBehaviour
    {
        static float lastWarned = -99f;

        void OnTriggerStay(Collider other)
        {
            var body = other.GetComponentInParent<FirstPersonController>();
            if (body == null || !Meta.Difficulty.SurvivalEnabled) return;
            var status = body.GetComponent<PlayerStatus>();
            if (status != null) status.Thirst = Mathf.Max(0f, status.Thirst - Time.deltaTime / 150f * Meta.Difficulty.Thirst);
            if (Time.time - lastWarned < 60f) return;
            lastWarned = Time.time;
            body.GetComponent<PlayerHud>()?.Flash("It's sweltering down here. Your mouth is already dry.", 4f);
            UI.Captions.Say("[the furnaces roar; something whispers inside them]");
        }
    }
}
