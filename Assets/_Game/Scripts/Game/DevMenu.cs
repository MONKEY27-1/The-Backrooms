#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Backrooms.Creatures;
using Backrooms.Meta;
using Backrooms.Net;
using Backrooms.Player;
using Backrooms.UI;
using Backrooms.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Game
{
    /// Local developer control panel. Shared-world changes are initiated by the owner and follow normal co-op sync.
    public sealed class DevMenu : MonoBehaviour
    {
        GameSession session;
        FirstPersonController player;
        ChunkStreamer streamer;
        FearDirector director;
        PlayerStatus status;
        PlayerHud hud;
        Func<SaveData> getSave;
        Func<LevelCatalog> getCatalog;
        Func<bool> menuBlocked;
        Canvas canvas;
        bool fly, freecam;
        int deathTestKind;
        Camera cameraView;
        Vector3 freecamPosition;
        float freecamYaw, freecamPitch;

        public bool IsOpen => canvas != null;
        public bool FlyActive => fly;
        public bool FreecamActive => freecam;
        public bool AnyDevActionActive => IsOpen || FlyActive || FreecamActive;

        public void Init(GameSession game, FirstPersonController body, ChunkStreamer world, FearDirector fear,
            PlayerStatus playerStatus, PlayerHud playerHud, Func<SaveData> save, Func<LevelCatalog> catalog)
        {
            session = game; player = body; streamer = world; director = fear; status = playerStatus; hud = playerHud;
            getSave = save; getCatalog = catalog;
            cameraView = player.GetComponentInChildren<Camera>();
        }

        public void SetMenuState(Func<bool> blocked) => menuBlocked = blocked;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f10Key.wasPressedThisFrame)
            {
                if (canvas != null) Close();
                else if (CanOpen())
                {
                    if (freecam) ToggleFreecam();
                    Open();
                }
            }
            if (canvas != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) Close();
                return;
            }
            if (!CanOpen()) return;
            if (freecam) UpdateFreecam();
            if (fly && !freecam) UpdateFly();
        }

        bool CanOpen()
        {
            var activeSave = getSave != null ? getSave() : null;
            return Debug.isDebugBuild && !Coop.IsGuest && activeSave != null && (menuBlocked == null || !menuBlocked());
        }

        void Open()
        {
            if (Coop.IsGuest || getSave == null || getSave() == null) return;
            canvas = Ui.Canvas("Developer Menu", 150);
            var root = Ui.Stretch(canvas.transform, "Root");
            Ui.Fill(root, new Color(0f, 0f, 0f, 0.88f));
            var panel = Ui.Rect(root, "Panel", new Vector2(0.12f, 0.05f), new Vector2(0.88f, 0.95f));
            var title = Ui.Label(panel, "DEVELOPER CONSOLE   ·   F10 / ESC CLOSE", 32, TextAnchor.MiddleLeft, Ui.Hot);
            Place(title.rectTransform, new Vector2(0.03f, 0.92f), new Vector2(0.97f, 1f));
            var list = Ui.Scroll(panel);
            Place(list, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.92f));
            Ui.Paragraph(list, "F4 resident · F5 hunt · F8 debug HUD · F10 console", 22, Ui.Dim);
            Ui.Heading(list, "PLAYER");
            Ui.Toggle(list, "Fly / noclip", () => fly, ToggleFly);
            Ui.Button(list, "FREE CAMERA (close menu to activate)", () => { Close(); ToggleFreecam(); }, 26);
            Ui.Button(list, "Teleport to spawn", () => Teleport(new Vector3(1f, 0.05f, 1f)), 26);
            var others = new List<PlayerAgent>();
            foreach (var a in Players.All) if (a != null && a != Players.Local && a.Alive) others.Add(a);
            Ui.Cycler(list, "Teleport to player", () => CurrentPlayerName(others), () => TeleportToPlayer(others));
            Ui.Heading(list, "WORLD");
            Ui.Cycler(list, "Level", () => CurrentLevelName(), CycleLevel);
            Ui.Button(list, "Jump 512 m forward (copy drift)", () => JumpOut(16), 26);
            Ui.Button(list, "Random nearby position", RandomTeleport, 26);
            Ui.Toggle(list, "Blackout", () => streamer != null && streamer.Blackout > 0.5f, ToggleBlackout);
            Ui.Button(list, "Clear blackout", () => SetBlackout(0f), 26);
            Ui.Heading(list, "ITEMS / OBJECTIVES");
            Ui.Button(list, "Spawn almond water", () => SpawnPickup(PropKind.AlmondWater), 26);
            Ui.Button(list, "Spawn batteries", () => SpawnPickup(PropKind.Battery), 26);
            Ui.Button(list, "Complete current objective", () => { if (CanControlShared) session.DevCompleteObjective(); else Say("Only the host can complete a shared objective."); }, 26);
            Ui.Button(list, "Refill meters and batteries", Refill, 26);
            Ui.Button(list, "+5 water and batteries", GiveItems, 26);
            Ui.Button(list, "Reset current checkpoint", ResetCheckpoint, 26);
            Ui.Heading(list, "CREATURES / EVENTS");
            Ui.Button(list, "Spawn a Faceling", SpawnResident, 26);
            Ui.Button(list, "Spawn a Skin Stealer", SpawnSkinStealer, 26);
            Ui.Button(list, "Spawn a Partygoer pack", SpawnPartyGoer, 26);
            Ui.Button(list, "Spawn a party trap", () => { if (CanControlShared && PartyScenes.Instance != null) Say(PartyScenes.Instance.Place(Players.Local) ? "Somewhere near, a party is waiting." : "No room for one here."); }, 26);
            Ui.Button(list, "Spawn Wretches", () => { if (CanControlShared) session.DevSpawnCreature(CreatureKind.Wretch); }, 26);
            Ui.Button(list, "Spawn a Wretch mass", () => { if (CanControlShared) session.DevSpawnCreature(CreatureKind.WretchMass); }, 26);
            Ui.Button(list, "Infect me", () => { if (status != null) status.Infection = Mathf.Min(0.95f, status.Infection + 0.3f); }, 26);
            Ui.Button(list, "+3 flares", () => { if (status != null) status.Flares += 3; }, 26);
            Ui.Button(list, "Spawn a moth swarm", () => { if (CanControlShared) session.DevSpawnCreature(CreatureKind.MothSwarm); }, 26);
            Ui.Button(list, "Spawn harmless moths", () => { if (CanControlShared) session.DevSpawnCreature(CreatureKind.MothMales); }, 26);
            Ui.Button(list, "Spawn a moth hive (Level 5)", () => { if (CanControlShared) session.DevSpawnCreature(CreatureKind.MothHive); }, 26);
            Ui.Button(list, "Spawn a Clump", () => { if (CanControlShared) session.DevSpawnCreature(CreatureKind.Clump); else Say("Only the host can spawn shared creatures."); }, 26);
            Ui.Button(list, "Spawn a Hound pack", () => { if (CanControlShared) session.DevSpawnCreature(CreatureKind.Hound); else Say("Only the host can spawn shared creatures."); }, 26);
            Ui.Heading(list, "DEATH SEQUENCE TESTS");
            Ui.Cycler(list, "Creature", () => GoreDeath.Describe(GoreDeath.TestKinds[deathTestKind], 0), () => deathTestKind = (deathTestKind + 1) % GoreDeath.TestKinds.Count);
            Ui.Button(list, "Play death variant 1 (checkpoint follows)", () => TestDeathSequence(0), 26);
            Ui.Button(list, "Play death variant 2 (checkpoint follows)", () => TestDeathSequence(1), 26);
            Ui.Button(list, "Check death sequence catalog", () => Say(GoreDeath.CheckCatalog(out var error) ? "Death catalog passed." : "Death catalog failed: " + error), 26);
            Ui.Button(list, "Start Smiler hunt", StartHunt, 26);
            Ui.Button(list, "Clear creatures", ClearCreatures, 26);
            Ui.Button(list, "Trigger random environmental event", TriggerEvent, 26);
            Ui.Heading(list, "SAVE");
            Ui.Button(list, "Save now", SaveNow, 26);
            Ui.Button(list, "Delete save and return to menu", DeleteSave, 26);
            Ui.Button(list, "Close console", Close, 26);
            player.enabled = false;
            var playerTools = GetComponent<PlayerTools>();
            if (playerTools != null) playerTools.enabled = false;
            FirstPersonController.LockCursor(false);
        }

        static void Place(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Close()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
            if (player != null) player.enabled = !freecam;
            var tools = GetComponent<PlayerTools>();
            if (tools != null) tools.enabled = !freecam;
            if (player != null) player.GetComponent<CharacterController>().enabled = !freecam && !fly;
            FirstPersonController.LockCursor(!freecam);
        }

        bool CanControlShared => !Coop.Active || Coop.IsOwner;
        void Say(string message) => hud?.Flash(message, 3f);

        void TestDeathSequence(int variant)
        {
            var victim = Players.Local;
            if (victim == null || !victim.Alive || victim.Dying) { Say("Death test needs a living player."); return; }
            var kind = GoreDeath.TestKinds[Mathf.Clamp(deathTestKind, 0, GoreDeath.TestKinds.Count - 1)];
            var at = player.transform.position + player.transform.forward * 0.7f;
            float yaw = player.transform.eulerAngles.y;
            Close();
            victim.PlayRemoteDeathPose(kind, variant, at, yaw);
        }

        void ToggleFly(bool enabled)
        {
            if (enabled && !CanControlShared) { Say("Only the host can change the shared world."); return; }
            fly = enabled;
            if (fly) player.GetComponent<CharacterController>().enabled = false;
            else if (!freecam && canvas == null) { player.GetComponent<CharacterController>().enabled = true; Physics.SyncTransforms(); }
            Say(fly ? "Fly / noclip enabled." : "Fly / noclip disabled.");
        }

        void UpdateFly()
        {
            var k = Keyboard.current;
            float speed = k.leftShiftKey.isPressed ? 18f : 7f;
            Vector3 move = player.transform.forward * ((k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f))
                         + player.transform.right * ((k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f))
                         + Vector3.up * ((k.spaceKey.isPressed ? 1f : 0f) - (k.leftCtrlKey.isPressed ? 1f : 0f));
            player.transform.position += move * speed * Time.unscaledDeltaTime;
        }

        void ToggleFreecam()
        {
            freecam = !freecam;
            if (freecam)
            {
                cameraView = player.GetComponentInChildren<Camera>();
                freecamPosition = cameraView.transform.position;
                freecamYaw = player.transform.eulerAngles.y;
                freecamPitch = 0f;
                player.enabled = false;
                var tools = GetComponent<PlayerTools>();
                if (tools != null) tools.enabled = false;
                player.GetComponent<CharacterController>().enabled = false;
            }
            else
            {
                if (cameraView != null) { cameraView.transform.SetParent(player.transform, false); cameraView.transform.localPosition = new Vector3(0f, 1.6f, 0f); cameraView.transform.localRotation = Quaternion.identity; }
                if (canvas == null) player.GetComponent<CharacterController>().enabled = !fly;
                if (canvas == null)
                {
                    player.enabled = true;
                    var tools = GetComponent<PlayerTools>();
                    if (tools != null) tools.enabled = true;
                }
            }
            Say(freecam ? "Free camera: WASD move, Space/Ctrl up/down, mouse look." : "Free camera closed.");
        }

        void UpdateFreecam()
        {
            var k = Keyboard.current;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var d = mouse.delta.ReadValue() * 0.12f;
                freecamYaw += d.x; freecamPitch = Mathf.Clamp(freecamPitch - d.y, -85f, 85f);
            }
            var rotation = Quaternion.Euler(freecamPitch, freecamYaw, 0f);
            float speed = k.leftShiftKey.isPressed ? 18f : 7f;
            Vector3 move = rotation * Vector3.forward * ((k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f))
                         + rotation * Vector3.right * ((k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f))
                         + Vector3.up * ((k.spaceKey.isPressed ? 1f : 0f) - (k.leftCtrlKey.isPressed ? 1f : 0f));
            freecamPosition += move * speed * Time.unscaledDeltaTime;
            cameraView.transform.SetParent(null, true);
            cameraView.transform.position = freecamPosition;
            cameraView.transform.rotation = rotation;
        }

        void Teleport(Vector3 position)
        {
            if (!CanControlShared) { Say("Only the host can teleport."); return; }
            player.Teleport(position, player.transform.eulerAngles.y);
            if (fly) player.GetComponent<CharacterController>().enabled = false;
            Say("Teleported.");
        }

        void TeleportToPlayer(List<PlayerAgent> others)
        {
            if (others.Count == 0) { Say("No other living players."); return; }
            int i = PlayerPrefs.GetInt("dev_player", -1);
            i = (i + 1) % others.Count; PlayerPrefs.SetInt("dev_player", i);
            Teleport(others[i].Position + others[i].transform.forward * 1.5f);
        }
        static string CurrentPlayerName(List<PlayerAgent> others) => others.Count == 0 ? "No players" : "Cycle on click (" + others.Count + ")";
        string CurrentLevelName() => streamer != null && streamer.Theme != null ? streamer.Theme.displayName : "Unknown";

        void CycleLevel()
        {
            var catalog = getCatalog?.Invoke();
            var save = getSave?.Invoke();
            if (!CanControlShared || catalog == null || save == null) { Say("Only the host can change levels."); return; }
            // In level-number order (-1, 0, 0.2, 0.3, 1 ... 9, 19, 37, 188, the endless staircase), named levels last.
            var ordered = new List<string>();
            foreach (var l in catalog.levels) if (l != null) ordered.Add(l.id);
            double Number(string id) => double.TryParse(id, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : double.MaxValue;
            ordered.Sort((a, b) => { int c = Number(a).CompareTo(Number(b)); return c != 0 ? c : string.CompareOrdinal(a, b); });
            int i = ordered.IndexOf(save.levelId);
            string next = ordered[(i + 1 + ordered.Count) % ordered.Count];
            session.DevGoTo(next, Vector2Int.zero);
        }

        void JumpOut(int chunks)
        {
            if (!CanControlShared) { Say("Only the host can teleport."); return; }
            session.DevGoTo(getSave().levelId, ChunkStreamer.ChunkOf(player.transform.position) + new Vector2Int(chunks, chunks));
        }

        void RandomTeleport()
        {
            if (!CanControlShared) { Say("Only the host can teleport."); return; }
            var p = player.transform.position + UnityEngine.Random.insideUnitSphere * 24f;
            p.y = player.transform.position.y;
            Teleport(p);
        }

        void ToggleBlackout(bool enabled) => SetBlackout(enabled ? 1f : 0f);
        void SetBlackout(float value)
        {
            if (!CanControlShared) { Say("Only the host can change the world."); return; }
            session.DevSetBlackout(value);
        }

        void SpawnPickup(PropKind kind)
        {
            if (!CanControlShared) { Say("Only the host can spawn shared items."); return; }
            if (streamer == null || streamer.Paints == null) return;
            Vector3 at = player.transform.position + player.transform.forward * 1.5f;
            at.y = player.transform.position.y;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = kind == PropKind.AlmondWater ? "Developer Almond Water" : "Developer Battery";
            go.transform.position = at;
            go.transform.localScale = Vector3.one * 0.22f;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = kind == PropKind.AlmondWater ? streamer.Paints[(int)PropPaint.Water] : streamer.Paints[(int)PropPaint.Yellow];
            var collider = go.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            var pickup = go.AddComponent<Pickup>();
            pickup.Kind = kind;
            pickup.Id = Pickup.IdAt(streamer.Seed, at);
            go.transform.SetParent(streamer.transform, true);
            session.DevSharePickup(pickup);
            Say("Spawned " + pickup.Label + ".");
        }

        void Refill()
        {
            if (status != null) { status.Thirst = status.Sanity = status.Stamina = 1f; }
            var tools = GetComponent<PlayerTools>();
            if (tools != null) tools.FlashlightBattery = tools.CamcorderBattery = 1f;
            Say("Meters and batteries refilled.");
        }
        void GiveItems() { if (status != null) { status.AlmondWater += 5; status.Batteries += 5; Say("Added five water and batteries."); } }
        void ResetCheckpoint() { if (CanControlShared) session.DevResetCheckpoint(); else Say("Only the host can reset the checkpoint."); }
        void SpawnResident() { if (CanControlShared) session.DevSpawnCreature(CreatureKind.Faceling); else Say("Only the host can spawn shared creatures."); }
        void SpawnSkinStealer() { if (CanControlShared) session.DevSpawnCreature(CreatureKind.SkinStealer); else Say("Only the host can spawn shared creatures."); }
        void SpawnPartyGoer() { if (CanControlShared) session.DevSpawnCreature(CreatureKind.PartyGoer); else Say("Only the host can spawn shared creatures."); }
        void StartHunt() { if (CanControlShared) session.DevStartHunt(); else Say("Only the host can start a shared hunt."); }
        void ClearCreatures() { if (CanControlShared) session.DevClearCreatures(); else Say("Only the host can clear shared creatures."); }
        void TriggerEvent() { if (CanControlShared) session.DevTriggerEvent(); else Say("Only the host can trigger shared events."); }
        void SaveNow() { session.DevSave(); Say("Saved."); }
        void DeleteSave() { if (CanControlShared) { Close(); session.DevDeleteSave(); } else Say("Only the host can delete the shared save."); }
    }
}
#endif
