using System;
using System.Collections.Generic;
using System.Linq;
using Backrooms.Game;
using Backrooms.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Backrooms.Player
{
    /// Save-backed map exploration, private named markers, and a camcorder photo album.
    public sealed class ExplorationTools : MonoBehaviour
    {
        const int MapRadius = 6, PhotoWidth = 384, PhotoHeight = 216, MaxPhotos = 24;
        Camera cameraView;
        FirstPersonController body;
        PlayerTools tools;
        PlayerHud hud;
        ChunkStreamer streamer;
        Func<SaveData> getSave;
        Action saveNow;
        Canvas canvas;
        RectTransform page;
        InputField markerName;
        Text content;
        string selectedLevel;
        int selectedFloor, selectedPhoto;
        bool showingMap;
        Vector2Int mapCenter;
        bool mapPanned;
        float nextExplore, nextExplorationSave;
        bool explorationDirty;
        readonly List<Texture2D> albumTextures = new List<Texture2D>();

        public void Init(Camera camera, FirstPersonController controller, ChunkStreamer world, Func<SaveData> save, Action persist)
        {
            cameraView = camera;
            body = controller;
            tools = GetComponent<PlayerTools>();
            hud = GetComponent<PlayerHud>();
            streamer = world;
            getSave = save;
            saveNow = persist;
            SaveSystem.LoadPersonalExploration(save?.Invoke());
        }

        void Update()
        {
            if (getSave == null || getSave() == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (canvas != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { blockPause?.Invoke(); Close(); return; }
                if (kb.mKey.wasPressedThisFrame) { ToggleMap(); return; }
                if (kb.uKey.wasPressedThisFrame) { ToggleAlbum(); return; }
                if (showingMap)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame) PanMap(Vector2Int.left);
                    else if (kb.rightArrowKey.wasPressedThisFrame) PanMap(Vector2Int.right);
                    else if (kb.upArrowKey.wasPressedThisFrame) PanMap(Vector2Int.up);
                    else if (kb.downArrowKey.wasPressedThisFrame) PanMap(Vector2Int.down);
                }
                return;
            }
            if (kb.mKey.wasPressedThisFrame) { ToggleMap(); return; }
            if (kb.uKey.wasPressedThisFrame) { ToggleAlbum(); return; }
            if (kb.pKey.wasPressedThisFrame) TakePhoto();
            if (explorationDirty && Time.unscaledTime >= nextExplorationSave) FlushExploration();
            if (canvas == null && Time.time >= nextExplore)
            {
                nextExplore = Time.time + 1f;
                DiscoverCurrentCell();
            }
        }

        void DiscoverCurrentCell()
        {
            var save = getSave();
            if (save == null || streamer == null || body == null) return;
            save.explored ??= new List<string>();
            string key = CellKey(save.levelId, streamer.CurrentFloor, ChunkStreamer.ChunkOf(body.transform.position));
            if (save.explored.Contains(key)) return;
            save.explored.Add(key);
            explorationDirty = true;
            if (Time.unscaledTime >= nextExplorationSave) FlushExploration();
        }

        static string CellKey(string level, int floor, Vector2Int cell) => $"{level}|{floor}|{cell.x}|{cell.y}";

        void ToggleMap()
        {
            if (canvas != null)
            {
                if (showingMap) { Close(); return; }
                Close(false);
            }
            selectedLevel = getSave().levelId;
            selectedFloor = streamer != null ? streamer.CurrentFloor : 0;
            mapCenter = streamer != null && body != null ? ChunkStreamer.ChunkOf(body.transform.position) : Vector2Int.zero;
            mapPanned = false;
            BuildPage("MAP", true);
            DrawMap();
        }

        void ToggleAlbum()
        {
            if (canvas != null)
            {
                if (!showingMap) { Close(); return; }
                Close(false);
            }
            BuildPage("PHOTO ALBUM", false);
            DrawAlbum();
        }

        void BuildPage(string heading, bool map)
        {
            showingMap = map;
            canvas = Backrooms.UI.Ui.Canvas("Exploration UI", 70);
            var root = Backrooms.UI.Ui.Stretch(canvas.transform, "Root");
            Backrooms.UI.Ui.Fill(root, new Color(0f, 0f, 0f, 0.83f), "Shade");
            var panel = Backrooms.UI.Ui.Rect(root, "Panel", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            var title = Backrooms.UI.Ui.Label(panel, heading + (map ? "   ·   ARROWS PAN  [M] MAP  [U] ALBUM  [ESC] CLOSE" : "   ·   [M] MAP  [U] ALBUM  [ESC] CLOSE"), 30, TextAnchor.MiddleLeft, Backrooms.UI.Ui.Hot);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.91f);
            title.rectTransform.anchorMax = new Vector2(0.96f, 0.99f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            var close = Backrooms.UI.Ui.Button(panel, "CLOSE", () => Close(), 24, TextAnchor.MiddleRight);
            close.GetComponent<RectTransform>().anchorMin = new Vector2(0.82f, 0.91f);
            close.GetComponent<RectTransform>().anchorMax = new Vector2(0.97f, 0.99f);
            close.GetComponent<RectTransform>().offsetMin = close.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            var controls = Backrooms.UI.Ui.Rect(panel, "Controls", new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.9f));
            if (body != null) body.enabled = false;
            if (tools != null) tools.enabled = false;
            FirstPersonController.LockCursor(false);
            if (map)
            {
                var prevLevel = Backrooms.UI.Ui.Button(controls, "◀ LEVEL", () => CycleLevel(-1), 23);
                SetRect(prevLevel.transform, new Vector2(0f, 0f), new Vector2(0.2f, 1f));
                var nextLevel = Backrooms.UI.Ui.Button(controls, "LEVEL ▶", () => CycleLevel(1), 23, TextAnchor.MiddleRight);
                SetRect(nextLevel.transform, new Vector2(0.8f, 0f), Vector2.one);
                var prevFloor = Backrooms.UI.Ui.Button(controls, "◀ FLOOR", () => CycleFloor(-1), 23);
                SetRect(prevFloor.transform, new Vector2(0.24f, 0f), new Vector2(0.43f, 1f));
                var nextFloor = Backrooms.UI.Ui.Button(controls, "FLOOR ▶", () => CycleFloor(1), 23, TextAnchor.MiddleRight);
                SetRect(nextFloor.transform, new Vector2(0.57f, 0f), new Vector2(0.76f, 1f));
            }
            else
            {
                var prev = Backrooms.UI.Ui.Button(controls, "◀ PREVIOUS PHOTO", () => ChangePhoto(-1), 23);
                SetRect(prev.transform, new Vector2(0.12f, 0f), new Vector2(0.48f, 1f));
                var next = Backrooms.UI.Ui.Button(controls, "NEXT PHOTO ▶", () => ChangePhoto(1), 23, TextAnchor.MiddleRight);
                SetRect(next.transform, new Vector2(0.52f, 0f), new Vector2(0.88f, 1f));
            }
            page = Backrooms.UI.Ui.Rect(panel, "Content", new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.8f));
            content = Backrooms.UI.Ui.Label(page, "", 25, TextAnchor.UpperLeft);
            content.rectTransform.anchorMin = Vector2.zero;
            content.rectTransform.anchorMax = Vector2.one;
            content.rectTransform.offsetMin = new Vector2(8f, 8f);
            content.rectTransform.offsetMax = new Vector2(-8f, -8f);
            if (map)
            {
                var footer = Backrooms.UI.Ui.Rect(panel, "Footer", new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.15f));
                markerName = Backrooms.UI.Ui.Input(footer, "Marker", "", "Name this place", 32, _ => { });
                SetRect(markerName.transform, new Vector2(0f, 0.35f), new Vector2(0.68f, 1f));
                var add = Backrooms.UI.Ui.Button(footer, "ADD MARKER HERE", AddMarker, 22);
                SetRect(add.transform, new Vector2(0.7f, 0.35f), Vector2.one);
            }
        }

        static void SetRect(Transform transform, Vector2 min, Vector2 max)
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void CycleLevel(int delta)
        {
            var save = getSave();
            var levels = new List<string>(save.visited ?? new List<string>());
            if (!levels.Contains(save.levelId)) levels.Add(save.levelId);
            if (levels.Count == 0) return;
            int i = levels.IndexOf(selectedLevel);
            selectedLevel = levels[(i + delta + levels.Count) % levels.Count];
            DrawMap();
        }

        void CycleFloor(int delta)
        {
            var save = getSave();
            var floors = new SortedSet<int>();
            foreach (string key in save.explored ?? new List<string>())
            {
                var parts = key.Split('|');
                if (parts.Length == 4 && parts[0] == selectedLevel && int.TryParse(parts[1], out int floor)) floors.Add(floor);
            }
            foreach (var marker in save.markers ?? new List<MapMarkerData>())
                if (marker.level == selectedLevel) floors.Add(marker.floor);
            if (selectedLevel == save.levelId && streamer != null) floors.Add(streamer.CurrentFloor);
            if (floors.Count == 0) floors.Add(0);
            var list = floors.ToList();
            int index = list.IndexOf(selectedFloor);
            selectedFloor = list[(index + delta + list.Count) % list.Count];
            mapPanned = false;
            DrawMap();
        }

        void DrawMap()
        {
            if (content == null) return;
            content.rectTransform.anchorMax = new Vector2(1f, 1f);
            content.rectTransform.offsetMax = new Vector2(-8f, -8f);
            var save = getSave();
            var known = new HashSet<string>(save.explored ?? new List<string>());
            if (!mapPanned) mapCenter = selectedLevel == save.levelId && body != null ? ChunkStreamer.ChunkOf(body.transform.position) : MapCenter(selectedLevel, selectedFloor, save, known);
            var center = mapCenter;
            var playerCell = selectedLevel == save.levelId && streamer != null && body != null && selectedFloor == streamer.CurrentFloor ? ChunkStreamer.ChunkOf(body.transform.position) : new Vector2Int(int.MinValue, int.MinValue);
            var marks = (save.markers ?? new List<MapMarkerData>()).Where(m => m.level == selectedLevel && m.floor == selectedFloor).ToList();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"LEVEL {selectedLevel}    FLOOR {selectedFloor}    ·    NORTH ↑");
            sb.AppendLine("@ YOU   # EXPLORED   M MARKER   · UNKNOWN");
            sb.AppendLine();
            for (int z = MapRadius; z >= -MapRadius; z--)
            {
                for (int x = -MapRadius; x <= MapRadius; x++)
                {
                    int cx = center.x + x, cz = center.y + z;
                    char symbol = known.Contains(CellKey(selectedLevel, selectedFloor, new Vector2Int(cx, cz))) ? '#' : '·';
                    if (marks.Any(m => m.x == cx && m.z == cz)) symbol = 'M';
                    if (cx == playerCell.x && cz == playerCell.y) symbol = '@';
                    sb.Append(symbol).Append(' ');
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            if (marks.Count == 0) sb.AppendLine("No named markers on this floor.");
            else foreach (var m in marks) sb.AppendLine($"M  {m.name}  ({m.x}, {m.z})");
            content.text = sb.ToString();
        }

        void PanMap(Vector2Int direction)
        {
            mapPanned = true;
            mapCenter += direction;
            DrawMap();
        }

        static Vector2Int MapCenter(string level, int floor, SaveData save, HashSet<string> known)
        {
            var cells = new List<Vector2Int>();
            foreach (string key in known)
            {
                var parts = key.Split('|');
                if (parts.Length != 4 || parts[0] != level || !int.TryParse(parts[1], out int f) || f != floor
                    || !int.TryParse(parts[2], out int x) || !int.TryParse(parts[3], out int z)) continue;
                cells.Add(new Vector2Int(x, z));
            }
            foreach (var marker in save.markers ?? new List<MapMarkerData>())
                if (marker.level == level && marker.floor == floor) cells.Add(new Vector2Int(marker.x, marker.z));
            if (cells.Count == 0) return Vector2Int.zero;
            int minX = cells.Min(c => c.x), maxX = cells.Max(c => c.x);
            int minZ = cells.Min(c => c.y), maxZ = cells.Max(c => c.y);
            return new Vector2Int((minX + maxX) / 2, (minZ + maxZ) / 2);
        }

        void AddMarker()
        {
            var save = getSave();
            if (save == null || body == null || streamer == null) return;
            string name = markerName != null ? markerName.text.Trim() : "";
            if (string.IsNullOrEmpty(name)) name = "Marker " + ((save.markers?.Count ?? 0) + 1);
            save.markers ??= new List<MapMarkerData>();
            var cell = ChunkStreamer.ChunkOf(body.transform.position);
            save.markers.Add(new MapMarkerData { name = name, level = save.levelId, floor = streamer.CurrentFloor, x = cell.x, z = cell.y });
            Save(save);
            selectedLevel = save.levelId;
            selectedFloor = streamer.CurrentFloor;
            mapPanned = false;
            DrawMap();
            hud?.Flash("Map marker added: " + name, 3f);
        }

        void DrawAlbum()
        {
            ClearAlbumTextures();
            if (content == null) return;
            content.rectTransform.anchorMax = new Vector2(0.56f, 1f);
            content.rectTransform.offsetMax = new Vector2(-8f, -8f);
            var save = getSave();
            var photos = save.photos ?? new List<PhotoData>();
            if (photos.Count == 0) selectedPhoto = 0;
            else selectedPhoto = Mathf.Clamp(selectedPhoto, 0, photos.Count - 1);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"DISCOVERIES PHOTOGRAPHED: {save.photographed?.Count ?? 0}");
            sb.AppendLine($"PHOTO {(photos.Count == 0 ? 0 : selectedPhoto + 1)} / {photos.Count}");
            sb.AppendLine();
            if (photos.Count == 0) sb.AppendLine("No photos yet. Raise the camcorder and press [P] to take one.");
            else
            {
                var photo = photos[selectedPhoto];
                sb.AppendLine(photo.taken);
                sb.AppendLine($"{photo.level} / floor {photo.floor}");
                sb.AppendLine(photo.discovery ?? "Unidentified");
                sb.AppendLine($"({photo.x}, {photo.z})");
            }
            content.text = sb.ToString();
            if (photos.Count == 0 || photos[selectedPhoto].jpeg == null || photos[selectedPhoto].jpeg.Length == 0) return;
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(photos[selectedPhoto].jpeg)) { Destroy(texture); return; }
            albumTextures.Add(texture);
            var image = new GameObject("Photo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(page, false);
            image.texture = texture;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(1f, 1f);
            image.rectTransform.pivot = new Vector2(1f, 1f);
            image.rectTransform.anchoredPosition = new Vector2(-8f, -8f);
            image.rectTransform.sizeDelta = new Vector2(480f, 270f);
        }

        void ChangePhoto(int delta)
        {
            var photos = getSave()?.photos;
            if (photos == null || photos.Count == 0) return;
            selectedPhoto = (selectedPhoto + delta + photos.Count) % photos.Count;
            DrawAlbum();
        }

        void TakePhoto()
        {
            if (cameraView == null || tools == null || !tools.CamcorderUp || canvas != null) return;
            string discovery = Recognize();
            byte[] jpeg = CaptureJpeg(cameraView);
            if (jpeg == null) { hud?.Flash("The camera failed to capture an image.", 3f); return; }
            Creatures.SmilerLook.CameraFlash();   // the flash catches what the dark hides
            var save = getSave();
            save.photos ??= new List<PhotoData>();
            save.photographed ??= new List<string>();
            var pos = body.transform.position;
            var chunk = ChunkStreamer.ChunkOf(pos);
            save.photos.Add(new PhotoData
            {
                level = save.levelId, floor = streamer != null ? streamer.CurrentFloor : 0,
                x = chunk.x, z = chunk.y, discovery = discovery,
                taken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), jpeg = jpeg,
            });
            while (save.photos.Count > MaxPhotos) save.photos.RemoveAt(0);
            selectedPhoto = save.photos.Count - 1;
            if (!string.IsNullOrEmpty(discovery) && !save.photographed.Contains(discovery)) save.photographed.Add(discovery);
            Save(save);
            hud?.Flash(string.IsNullOrEmpty(discovery) ? "Photograph saved. No discovery recognized." : "Discovery photographed: " + discovery, 4f);
        }

        string Recognize()
        {
            if (!Physics.Raycast(cameraView.transform.position, cameraView.transform.forward, out var hit, 80f, ~0, QueryTriggerInteraction.Collide)) return "";
            var readable = hit.collider.GetComponentInParent<Readable>();
            if (readable != null) return readable.Kind + ": " + readable.Title;
            var pickup = hit.collider.GetComponentInParent<Pickup>();
            if (pickup != null) return "Supply: " + pickup.Label;
            var exit = hit.collider.GetComponentInParent<LevelTrigger>();
            if (exit != null && !string.IsNullOrEmpty(exit.Target)) return "Exit to Level " + exit.Target;
            var creature = hit.collider.GetComponentInParent<Creatures.Creature>();
            if (creature != null) return "Entity: " + creature.Kind;
            var avatar = hit.collider.GetComponentInParent<Net.CoopAvatar>();
            if (avatar != null && !avatar.IsMine) return "Wanderer: " + Net.CoopAvatar.SuitNames[Mathf.Clamp(avatar.Suit, 0, Net.CoopAvatar.SuitNames.Length - 1)];
            return "";
        }

        static byte[] CaptureJpeg(Camera camera)
        {
            RenderTexture rt = null;
            Texture2D texture = null;
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            try
            {
                rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                else { camera.targetTexture = rt; camera.Render(); }
                RenderTexture.active = rt;
                texture = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
                texture.Apply();
                return texture.EncodeToJPG(80);
            }
            catch (Exception e) { Debug.LogWarning("Photo capture failed: " + e.Message); return null; }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (texture != null) Destroy(texture);
            }
        }

        void Save(SaveData save)
        {
            if (!string.IsNullOrEmpty(save.slot)) saveNow?.Invoke();
            else SaveSystem.SaveExploration(save);
            explorationDirty = false;
            nextExplorationSave = Time.unscaledTime + 15f;
        }

        void FlushExploration()
        {
            var save = getSave?.Invoke();
            if (save == null || !explorationDirty) return;
            Save(save);
        }

        void Close(bool restorePlayer = true)
        {
            ClearAlbumTextures();
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
            page = null;
            content = null;
            markerName = null;
            showingMap = false;
            if (restorePlayer && !InMenu())
            {
                if (body != null) body.enabled = true;
                if (tools != null) tools.enabled = true;
                FirstPersonController.LockCursor(true);
            }
        }

        void ClearAlbumTextures()
        {
            foreach (var texture in albumTextures) if (texture != null) Destroy(texture);
            albumTextures.Clear();
            if (page != null)
                foreach (Transform child in page)
                    if (child.name == "Photo") Destroy(child.gameObject);
        }

        Func<bool> inMenu;
        Action blockPause;
        public bool IsOpen => canvas != null;
        public void SetMenuState(Func<bool> menuState, Action pauseBlock = null) { inMenu = menuState; blockPause = pauseBlock; }
        bool InMenu() => inMenu != null && inMenu();
        public void CloseOverlay() { if (canvas != null) Close(false); }

        void OnDisable()
        {
            FlushExploration();
            if (canvas != null) Close(false);
        }
        void OnDestroy()
        {
            FlushExploration();
            if (canvas != null) Close(false);
        }
    }
}
