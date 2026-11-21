using System.Collections.Generic;
using System.Linq;
using Backrooms.Net;
using Backrooms.Player;
using Backrooms.UI;
using Backrooms.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Backrooms.Game
{
    /// The co-op waiting room is its own isolated scene, not a rearranged main-menu level. Its camera, props,
    /// materials and lights are destroyed together; networked avatars remain in the persistent game scene.
    public sealed class LobbyEnvironment : MonoBehaviour
    {
        public static readonly Vector3 Center = new Vector3(5000f, 0f, 5000f);
        Scene lobbyScene;
        GameObject room;
        Camera lobbyCamera, playerCamera;
        string playerCameraTag;
        Menus menus;
        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<CoopAvatar, TextMesh> nameplates = new Dictionary<CoopAvatar, TextMesh>();
        readonly Dictionary<CoopAvatar, MeshRenderer> readyMats = new Dictionary<CoopAvatar, MeshRenderer>();
        Material matWaiting, matReady;
        Light flickerLight;
        AudioSource hum;
        float statusCheck;
        float yaw, pitch = 10f, distance = 7.8f;
        bool dragging;

        public static Vector3 PlayerSpot(ulong client)
        {
            var ids = new List<ulong>(CoopAvatar.All.Keys);
            ids.Sort();
            int index = Mathf.Max(0, ids.IndexOf(client));
            return Center + new Vector3((index - (Mathf.Max(1, ids.Count) - 1) * 0.5f) * 1.35f, 0f, 0f);
        }

        public void Init(Camera currentCamera, Menus ui)
        {
            playerCamera = currentCamera;
            menus = ui;
            var previous = SceneManager.GetActiveScene();
            lobbyScene = SceneManager.CreateScene("Co-op Preparation Room");
            SceneManager.SetActiveScene(lobbyScene);
            room = new GameObject("Hazmat Preparation Room");
            room.transform.position = Center;
            SceneManager.MoveGameObjectToScene(room, lobbyScene);

            var concrete = Paint(new Color(0.24f, 0.25f, 0.22f), 0.15f);
            var wall = Paint(new Color(0.43f, 0.43f, 0.34f), 0.22f);
            var trim = Paint(new Color(0.12f, 0.13f, 0.12f), 0.4f, 0.65f);
            var steel = Paint(new Color(0.3f, 0.34f, 0.33f), 0.6f, 0.8f);
            var yellow = Paint(new Color(0.75f, 0.55f, 0.08f), 0.22f);
            var upholstery = Paint(new Color(0.12f, 0.16f, 0.13f), 0.08f);
            var boards = Paint(new Color(0.035f, 0.05f, 0.045f), 0.15f);
            var glow = HdMat.Emissive(new Color(0.82f, 0.9f, 0.78f), 450f);
            materials.Add(glow);

            Box("Sealed concrete floor", new Vector3(0f, -0.12f, -3.4f), new Vector3(12f, 0.24f, 18f), concrete, true);
            Box("Ceiling", new Vector3(0f, 3.65f, -3.4f), new Vector3(12f, 0.22f, 18f), trim);
            Box("Front wall", new Vector3(0f, 1.8f, -12.4f), new Vector3(12f, 3.6f, 0.2f), wall);
            Box("Equipment wall", new Vector3(0f, 1.8f, 4.7f), new Vector3(12f, 3.6f, 0.2f), wall);
            Box("Left wall", new Vector3(-6f, 1.8f, -3.4f), new Vector3(0.2f, 3.6f, 18f), wall);
            Box("Right wall", new Vector3(6f, 1.8f, -3.4f), new Vector3(0.2f, 3.6f, 18f), wall);
            // Scuffed lower wall panels and a yellow safety stripe establish the preparation-bay identity.
            Box("Rear kick plate", new Vector3(0f, 0.55f, 4.54f), new Vector3(11.8f, 1.1f, 0.04f), trim);
            Box("Safety stripe", new Vector3(0f, 1.15f, 4.51f), new Vector3(11.8f, 0.07f, 0.04f), yellow);

            // Lockers behind the team, complete with offset doors, vents and handles.
            for (int i = 0; i < 6; i++)
            {
                float x = -4.75f + i * 0.83f;
                Box("Suit locker", new Vector3(x, 1.5f, 4.12f), new Vector3(0.76f, 2.65f, 0.66f), steel);
                Box("Locker door", new Vector3(x, 1.5f, 3.76f), new Vector3(0.67f, 2.5f, 0.06f), trim);
                Box("Locker handle", new Vector3(x + 0.2f, 1.35f, 3.70f), new Vector3(0.055f, 0.28f, 0.06f), yellow);
                for (int v = 0; v < 3; v++) Box("Vent", new Vector3(x, 2.39f - v * 0.1f, 3.70f), new Vector3(0.4f, 0.025f, 0.02f), steel);
            }
            // Briefing table and a dark assignment board. Code and settings remain readable in the suite's UI.
            Box("Briefing table top", new Vector3(0.2f, 0.78f, 2.35f), new Vector3(3.5f, 0.13f, 1.05f), steel);
            foreach (float x in new[] { -1.3f, 1.7f }) Box("Table leg", new Vector3(x, 0.38f, 2.35f), new Vector3(0.09f, 0.76f, 0.8f), trim);
            Box("Assignment board", new Vector3(0f, 2.25f, 4.50f), new Vector3(2.7f, 1.25f, 0.08f), boards);
            Box("Board frame", new Vector3(0f, 2.92f, 4.49f), new Vector3(2.85f, 0.08f, 0.12f), steel);
            Sign("ASSIGNMENT", new Vector3(0f, 2.56f, 4.42f), 0.024f, Color.white);
            Sign("PREPARE  /  CHECK IN", new Vector3(0f, 2.16f, 4.42f), 0.014f, Ui.Hot);

            // A deliberately closed heavy exit, not a playable level entrance.
            Box("Sealed exit", new Vector3(4.1f, 1.65f, 4.42f), new Vector3(2f, 3.18f, 0.18f), trim);
            Box("Exit panel", new Vector3(4.1f, 1.65f, 4.29f), new Vector3(1.77f, 2.98f, 0.08f), steel);
            Box("Exit caution", new Vector3(4.1f, 2.8f, 4.21f), new Vector3(1.9f, 0.18f, 0.06f), yellow);
            Sign("SECTOR ACCESS", new Vector3(4.1f, 2.17f, 4.17f), 0.019f, Color.white);
            Sign("START WHEN READY", new Vector3(4.1f, 1.89f, 4.17f), 0.013f, Ui.Hot);

            // Benches, personal kit and stools (players remain posed in the same spots during emotes).
            Box("Team bench", new Vector3(-4.4f, 0.45f, 0.7f), new Vector3(0.75f, 0.12f, 3.7f), upholstery);
            for (int i = 0; i < 3; i++) Box("Bench support", new Vector3(-4.4f, 0.2f, -0.8f + i * 1.5f), new Vector3(0.5f, 0.4f, 0.1f), steel);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 1.35f;
                Box("Ready stool", new Vector3(x, 0.48f, 0.12f), new Vector3(0.52f, 0.12f, 0.5f), upholstery);
                Box("Stool post", new Vector3(x, 0.23f, 0.12f), new Vector3(0.11f, 0.44f, 0.11f), steel);
            }
            Box("Supply chest", new Vector3(4.45f, 0.42f, 0.5f), new Vector3(1.5f, 0.85f, 0.85f), yellow);
            Box("Chest lid", new Vector3(4.45f, 0.89f, 0.5f), new Vector3(1.58f, 0.12f, 0.93f), trim);
            Sign("ALMOND WATER / FIELD KIT", new Vector3(4.45f, 0.46f, 0.055f), 0.011f, Color.black);

            // Three fluorescent fixtures, practical lights and a restrained fill for faces.
            for (int i = -1; i <= 1; i++)
            {
                Box("Fixture housing", new Vector3(i * 3.1f, 3.46f, -0.1f), new Vector3(0.62f, 0.14f, 2.6f), trim);
                Box("Fluorescent diffuser", new Vector3(i * 3.1f, 3.35f, -0.1f), new Vector3(0.43f, 0.05f, 2.35f), glow);
                var lamp = Lamp(new Vector3(i * 3.1f, 3.23f, -0.1f), new Color(0.85f, 0.94f, 0.78f), 450f, 7f);
                if (i == 0) flickerLight = lamp;
            }
            Lamp(new Vector3(0f, 2.3f, -4f), new Color(1f, 0.85f, 0.6f), 200f, 6f);

            // The middle tube never quite settles; a ballast hum hangs in the room.
            matWaiting = HdMat.Emissive(new Color(0.85f, 0.62f, 0.18f), 4f);
            matReady = HdMat.Emissive(new Color(0.35f, 1f, 0.45f), 18f);
            materials.Add(matWaiting);
            materials.Add(matReady);
            hum = room.AddComponent<AudioSource>();
            hum.spatialBlend = 0f;
            hum.loop = true;
            hum.playOnAwake = false;
            hum.volume = 0.07f * Meta.AudioMix.Ambience;
            hum.clip = Audio.ProceduralAudio.Hum();
            hum.Play();

            CoopAvatar.LobbyAvatarSpawned += OnLobbyAvatar;
            foreach (var present in CoopAvatar.All.Values.ToList()) OnLobbyAvatar(present);

            // Reuse the first-person camera's HDRP settings, but never move the actual player or activate controls.
            var cameraGo = Object.Instantiate(playerCamera.gameObject, room.transform);
            cameraGo.name = "Lobby Cinematic Camera";
            lobbyCamera = cameraGo.GetComponent<Camera>();
            foreach (var behaviour in cameraGo.GetComponents<MonoBehaviour>()) if (behaviour != null) behaviour.enabled = true;
            lobbyCamera.enabled = true;
            playerCameraTag = playerCamera.tag;
            playerCamera.tag = "Untagged";
            lobbyCamera.tag = "MainCamera";
            playerCamera.enabled = false;
            var originalListener = playerCamera.GetComponent<AudioListener>();
            if (originalListener != null) originalListener.enabled = false;
            var listener = cameraGo.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
            cameraGo.transform.position = Center + new Vector3(0f, 2f, -distance);
            cameraGo.transform.LookAt(Center + Vector3.up);
            FirstPersonController.LockCursor(false);
            SceneManager.SetActiveScene(previous);
        }

        /// A suit appeared in the room: add a compact readiness label beneath its built-in name tag and a pad.
        void OnLobbyAvatar(CoopAvatar avatar)
        {
            if (avatar == null || nameplates.ContainsKey(avatar)) return;
            var plateGo = new GameObject("Lobby Nameplate");
            plateGo.transform.SetParent(avatar.transform, false);
            plateGo.transform.localPosition = Vector3.up * 1.83f;
            var plate = plateGo.AddComponent<TextMesh>();
            plate.font = Ui.Font;
            plate.anchor = TextAnchor.LowerCenter;
            plate.alignment = TextAlignment.Center;
            plate.fontSize = 48;
            plate.characterSize = 0.011f;
            plate.text = "...";
            nameplates[avatar] = plate;

            var mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "Ready Pad";
            mat.transform.SetParent(avatar.transform, false);
            mat.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            mat.transform.localScale = new Vector3(0.85f, 0.024f, 0.85f);
            Object.Destroy(mat.GetComponent<Collider>());
            var pad = mat.GetComponent<MeshRenderer>();
            pad.sharedMaterial = matWaiting;
            pad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            readyMats[avatar] = pad;
        }

        void RefreshAvatars()
        {
            var session = Coop.Session;
            foreach (var dead in nameplates.Keys.Where(a => a == null).ToList()) nameplates.Remove(dead);
            foreach (var dead in readyMats.Keys.Where(a => a == null).ToList()) readyMats.Remove(dead);
            foreach (var kv in nameplates)
            {
                if (kv.Key == null || kv.Value == null) continue;
                bool ready = session != null && session.IsReady(kv.Key.OwnerClientId);
                kv.Value.text = ready ? "READY" : "NOT READY";
                kv.Value.color = ready ? new Color(0.56f, 1f, 0.56f) : SuitColor(kv.Key.Suit);
            }
            foreach (var kv in readyMats)
            {
                if (kv.Key == null || kv.Value == null) continue;
                bool ready = session != null && session.IsReady(kv.Key.OwnerClientId);
                kv.Value.sharedMaterial = ready ? matReady : matWaiting;
            }
        }

        static Color SuitColor(int i)
        {
            switch (Mathf.Abs(i) % 4)
            {
                case 1: return new Color(0.38f, 0.64f, 1f);    // Blue
                case 2: return new Color(1f, 0.36f, 0.30f);    // Red
                case 3: return new Color(0.44f, 0.88f, 0.50f); // Green
                default: return new Color(1f, 0.83f, 0.30f);   // Yellow
            }
        }

        void Update()
        {
            if (lobbyCamera == null) return;
            if (flickerLight != null)
                flickerLight.intensity = 450f * (0.9f + 0.1f * Mathf.PerlinNoise(Time.unscaledTime * 6.3f, 0.17f));
            statusCheck += Time.unscaledDeltaTime;
            if (statusCheck >= 0.25f) { statusCheck = 0f; RefreshAvatars(); }
            foreach (var kv in nameplates)
                if (kv.Key != null && kv.Value != null)
                    kv.Value.transform.rotation = Quaternion.LookRotation(kv.Value.transform.position - lobbyCamera.transform.position);
            float dx = 0f, dy = 0f;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed && !EventSystem.current.IsPointerOverGameObject())
            {
                dragging = true;
                Vector2 delta = mouse.delta.ReadValue();
                dx = delta.x * 0.12f; dy = delta.y * 0.1f;
            }
            else dragging = false;
            yaw = dragging ? Mathf.Clamp(yaw + dx, -20f, 20f) : Mathf.Sin(Time.unscaledTime * 0.12f) * 4f;
            pitch = Mathf.Clamp(pitch - dy, 5f, 22f);
            if (mouse != null) distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.005f, 5.2f, 9.4f);
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            var wanted = Center + new Vector3(0f, 1.4f, 0f) + orbit * new Vector3(0f, 0f, -distance);
            lobbyCamera.transform.position = Vector3.Lerp(lobbyCamera.transform.position, wanted, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));
            lobbyCamera.transform.rotation = Quaternion.Slerp(lobbyCamera.transform.rotation,
                Quaternion.LookRotation(Center + Vector3.up * 1.05f - lobbyCamera.transform.position), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !EventSystem.current.IsPointerOverGameObject())
            {
                var ray = lobbyCamera.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 30f, ~0, QueryTriggerInteraction.Collide))
                {
                    var avatar = hit.collider.GetComponentInParent<CoopAvatar>();
                    if (avatar != null && menus != null) menus.ShowLobbyPlayer(avatar);
                }
            }
        }

        Material Paint(Color color, float smoothness, float metal = 0f)
        {
            var m = HdMat.Make(color, smoothness, metal, tiling: new Vector2(2f, 2f));
            materials.Add(m);
            return m;
        }

        void Box(string name, Vector3 at, Vector3 size, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
        }

        Light Lamp(Vector3 at, Color color, float lumens, float range)
        {
            var go = new GameObject("Room practical");
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            light.intensity = lumens;
            light.range = range;
            light.shadows = LightShadows.Soft;
            return light;
        }

        void Sign(string text, Vector3 at, float size, Color color)
        {
            var go = new GameObject(text);
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var sign = go.AddComponent<TextMesh>();
            sign.text = text;
            sign.font = Ui.Font;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.fontSize = 48;
            sign.characterSize = size;
            sign.color = color;
        }

        void OnDestroy()
        {
            CoopAvatar.LobbyAvatarSpawned -= OnLobbyAvatar;
            // Plates and pads are parented to the avatars, which outlive the lobby; take them down ourselves.
            foreach (var kv in nameplates) if (kv.Value != null) Destroy(kv.Value.gameObject);
            foreach (var kv in readyMats) if (kv.Value != null) Destroy(kv.Value.gameObject);
            nameplates.Clear();
            readyMats.Clear();
            if (playerCamera != null)
            {
                playerCamera.tag = playerCameraTag;
                playerCamera.enabled = true;
                var listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }
            FirstPersonController.LockCursor(false);
            foreach (var mat in materials) if (mat != null) Destroy(mat);
            if (lobbyScene.IsValid() && lobbyScene.isLoaded) SceneManager.UnloadSceneAsync(lobbyScene);
        }
    }
}
