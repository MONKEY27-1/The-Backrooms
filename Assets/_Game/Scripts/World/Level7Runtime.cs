using System;
using Backrooms.Player;
using UnityEngine;

namespace Backrooms.World
{
    /// Fort Surrender's diving gear: a brass helmet and an air tank. Taking it doubles how long you can stay under.
    public sealed class DivingGear : MonoBehaviour, IInteractable
    {
        public static Action Taken;   // set by Level7Events: saves it and applies it
        Mesh mesh;

        public string Prompt => "Take the diving helmet and tank";

        public void Build(Material[] paints)
        {
            var b = new BoxMeshBuilder(Vector3.zero, paints.Length);
            // A round brass helmet (stacked rings), its front port, and an air tank beside it.
            for (int i = 0; i < 6; i++)
            {
                float y = i * 0.07f, r = 0.17f * Mathf.Sin((i + 1) / 7f * Mathf.PI) + 0.06f;
                b.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, y + 0.035f, 0f), new Vector3(r * 2f, 0.07f, r * 2f), (int)PropPaint.Brass);
            }
            b.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, 0.2f, 0.17f), new Vector3(0.16f, 0.16f, 0.04f), (int)PropPaint.Glass);
            b.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, 0.2f, 0.19f), new Vector3(0.19f, 0.03f, 0.02f), (int)PropPaint.Brass);
            b.AddOrientedBox(Matrix4x4.identity, new Vector3(0f, -0.02f, 0f), new Vector3(0.42f, 0.04f, 0.38f), (int)PropPaint.Brass);   // the collar
            b.AddOrientedBox(Matrix4x4.identity, new Vector3(0.45f, 0.1f, 0f), new Vector3(0.18f, 0.6f, 0.18f), (int)PropPaint.Yellow); // the tank
            b.AddOrientedBox(Matrix4x4.identity, new Vector3(0.45f, 0.43f, 0f), new Vector3(0.06f, 0.06f, 0.06f), (int)PropPaint.Metal);
            mesh = b.ToMesh("Diving gear");
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0.2f, 0.15f, 0f);
            box.size = new Vector3(0.9f, 0.7f, 0.6f);
            if (Level7Events.HasGear) gameObject.SetActive(false);
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }

        public void Interact()
        {
            Taken?.Invoke();
            gameObject.SetActive(false);
        }
    }

    /// Level 7 while you're in it: the dark closes in zone by zone as you go down; below the Twilight zone the
    /// pressure eats your breath and your nerve. The diving gear halves how fast your breath goes.
    public sealed class Level7Events : MonoBehaviour
    {
        public static bool HasGear { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { HasGear = false; DivingGear.Taken = null; }

        static readonly string[] ZoneNames = { "the Daylight zone", "the Twilight zone", "the Midnight zone", "the Abyss" };
        static readonly string[] ZoneLines =
        {
            "", "The Twilight zone. The last of the light goes grey and then goes.",
            "The Midnight zone. Your ears ache; your lamp shows a few metres of black water.",
            "The Abyss. Every breath costs twice. Something down here is very large.",
        };

        FirstPersonController player;
        PlayerStatus status;
        Camera cam;
        int shownZone;
        SeaDrift drift;
        Action save;

        public static int ZoneOf(float depth) => depth < 30f ? 0 : depth < 80f ? 1 : depth < 150f ? 2 : 3;

        public void Init(FirstPersonController body, bool gear, Action onTaken)
        {
            player = body;
            status = body.GetComponent<PlayerStatus>();
            cam = body.GetComponentInChildren<Camera>();
            HasGear = gear;
            if (cam != null) drift = SeaDrift.Create(cam.transform);
            save = onTaken;
            DivingGear.Taken = () =>
            {
                HasGear = true;
                save?.Invoke();
                player.GetComponent<PlayerHud>()?.Flash("A brass diving helmet and a tank, still half full. You'll last twice as long under water.", 6f);
                UI.Captions.Say("[the helmet's valve hisses]");
            };
        }

        void OnDestroy()
        {
            if (player != null) player.BreathRate = 1f;
            if (drift != null) Destroy(drift.gameObject);
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetOceanDepth(-1f);
        }

        void Update()
        {
            if (player == null) return;
            float eyeY = cam != null ? cam.transform.position.y : player.transform.position.y + 1.6f;
            float depth = Mathf.Max(0f, OceanLayout.OceanWaterLevel - eyeY);
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetOceanDepth(depth);
            if (drift != null) drift.Set(player.Underwater, depth);
            // The pressure: past 80 m your breath goes faster and faster, and it wears on you.
            float pressure = depth > 80f ? 1f + (depth - 80f) / 50f : 1f;
            player.BreathRate = (HasGear ? 0.5f : 1f) * pressure;
            if (depth > 80f && status != null && Meta.Difficulty.SurvivalEnabled)
                status.Sanity = Mathf.Max(0f, status.Sanity - Time.deltaTime * (depth - 80f) / 6000f * Meta.Difficulty.Sanity);
            int zone = player.Underwater ? ZoneOf(depth) : 0;
            if (zone > shownZone)
            {
                player.GetComponent<PlayerHud>()?.Flash(ZoneLines[zone], 4f);
                UI.Captions.Say($"[{ZoneNames[zone]}: the water presses in]");
            }
            shownZone = zone;
        }
    }
}
