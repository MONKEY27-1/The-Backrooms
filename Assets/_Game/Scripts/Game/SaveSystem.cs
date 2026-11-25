using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Backrooms.Game
{
    [Serializable]
    public sealed class SaveData
    {
        public string slot;                                   // file name stem, e.g. "slot_3"
        public string name = "World";
        public int difficulty = 1;                            // Meta.Difficulty: 0 Easy .. 3 Extreme
        public int mode = (int)Meta.GameMode.Survival;
        public float customThirst = 1.3f, customBattery = 1.2f, customSanity = 1.15f, customSupplies = 0.8f;
        public List<string> hardcoreDead = new List<string>();
        public List<int> hardcoreWait = new List<int>();
        public uint worldSeed;
        public string levelId = "0";
        public int arrivalX, arrivalZ;                       // checkpoint chunk in the current level
        public string shrineLevel;                           // a Level 3 Sanctum you rested in: respawn there instead
        public float shrineX, shrineY, shrineZ;
        public List<string> visited = new List<string>();    // level ids, for the level log and achievements
        public float playSeconds;
        public string lastPlayed;                            // ISO date

        // The adaptive encounter layer: what it placed in each curiosity slot, and how often you've seen each kind.
        public List<int> curiositySlots = new List<int>();
        public List<int> curiosityKinds = new List<int>();
        public List<int> curiositySeen = new List<int>();

        // Survival (Phase 4). 1 = full.
        public float thirst = 1f, sanity = 1f, flashlightBattery = 1f, camcorderBattery = 1f;
        public int almondWater = 1, batteries = 1, glowsticks, exterminators, firstAid, repellent, flares;
        public float infection;   // the Wretched Cycle
        public float health = 1f;
        public List<int> picked = new List<int>();   // pickups already taken, so they stay gone
        public List<string> logbook = new List<string>();   // notes you've read: title, newline, text
        public List<string> goals = new List<string>();     // level goals: "level:itemIndex" and "level:done"
        public List<string> explored = new List<string>();  // map cells: level/floor/chunkX/chunkZ
        public List<MapMarkerData> markers = new List<MapMarkerData>();
        public List<string> photographed = new List<string>();
        public List<PhotoData> photos = new List<PhotoData>();

        public void EnsureCollections()
        {
            hardcoreDead ??= new List<string>();
            hardcoreWait ??= new List<int>();
            visited ??= new List<string>();
            curiositySlots ??= new List<int>();
            curiosityKinds ??= new List<int>();
            curiositySeen ??= new List<int>();
            picked ??= new List<int>();
            logbook ??= new List<string>();
            goals ??= new List<string>();
            explored ??= new List<string>();
            markers ??= new List<MapMarkerData>();
            photographed ??= new List<string>();
            photos ??= new List<PhotoData>();
        }
    }

    [Serializable]
    public sealed class MapMarkerData
    {
        public string name, level;
        public int floor, x, z;
    }

    [Serializable]
    public sealed class PhotoData
    {
        public string level, discovery;
        public int floor, x, z;
        public string taken;
        public byte[] jpeg;
    }

    [Serializable]
    sealed class PersonalExplorationData
    {
        public List<string> explored = new List<string>();
        public List<MapMarkerData> markers = new List<MapMarkerData>();
        public List<string> photographed = new List<string>();
        public List<PhotoData> photos = new List<PhotoData>();
    }

    /// Any number of save slots, each its own world: saves/slot_N.json plus a slot_N.jpg thumbnail.
    public static class SaveSystem
    {
        static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        static string PersonalExplorationFile => Path.Combine(Application.persistentDataPath, "personal-exploration.json");
        static string File_(string slot) => Path.Combine(Dir, slot + ".json");
        public static string Thumbnail(string slot) => Path.Combine(Dir, slot + ".jpg");

        /// Every saved world, most recently played first.
        public static List<SaveData> List()
        {
            var list = new List<SaveData>();
            try
            {
                string old = Path.Combine(Application.persistentDataPath, "save.json");
                if (File.Exists(old) && (!Directory.Exists(Dir) || Directory.GetFiles(Dir, "slot_*.json").Length == 0))
                {
                    Directory.CreateDirectory(Dir);
                    File.Move(old, File_("slot_1"));   // the save from before there were slots
                }
                if (!Directory.Exists(Dir)) return list;
                foreach (var f in Directory.GetFiles(Dir, "slot_*.json"))
                {
                    var d = Read(f);
                    if (d != null) { d.slot = Path.GetFileNameWithoutExtension(f); list.Add(d); }
                }
            }
            catch (Exception e) { Debug.LogWarning($"Couldn't list saves: {e.Message}"); }
            list.Sort((a, b) => string.CompareOrdinal(b.lastPlayed ?? "", a.lastPlayed ?? ""));
            return list;
        }

        public static SaveData Load(string slot) { var d = Read(File_(slot)); if (d != null) d.slot = slot; return d; }

        public static void LoadPersonalExploration(SaveData save)
        {
            if (save == null || !string.IsNullOrEmpty(save.slot)) return;
            try
            {
                if (!File.Exists(PersonalExplorationFile)) return;
                var data = JsonUtility.FromJson<PersonalExplorationData>(File.ReadAllText(PersonalExplorationFile));
                if (data == null) return;
                save.explored = data.explored ?? new List<string>();
                save.markers = data.markers ?? new List<MapMarkerData>();
                save.photographed = data.photographed ?? new List<string>();
                save.photos = data.photos ?? new List<PhotoData>();
            }
            catch (Exception e) { Debug.LogWarning($"Personal exploration data unreadable: {e.Message}"); }
        }

        public static void SaveExploration(SaveData save)
        {
            if (save == null) return;
            if (!string.IsNullOrEmpty(save.slot)) { Save(save); return; }
            try
            {
                var data = new PersonalExplorationData
                {
                    explored = save.explored ?? new List<string>(), markers = save.markers ?? new List<MapMarkerData>(),
                    photographed = save.photographed ?? new List<string>(), photos = save.photos ?? new List<PhotoData>(),
                };
                File.WriteAllText(PersonalExplorationFile, JsonUtility.ToJson(data));
            }
            catch (Exception e) { Debug.LogWarning($"Personal exploration save failed: {e.Message}"); }
        }

        static SaveData Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                data?.EnsureCollections();
                return data;
            }
            catch (Exception e) { Debug.LogWarning($"Save file unreadable: {e.Message}"); return null; }
        }

        /// A fresh slot name (never reuses an old one).
        public static string NewSlot()
        {
            Directory.CreateDirectory(Dir);
            int n = 1;
            while (File.Exists(File_($"slot_{n}"))) n++;
            return $"slot_{n}";
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;
            data.EnsureCollections();
            if (string.IsNullOrEmpty(data.slot)) data.slot = NewSlot();
            data.lastPlayed = DateTime.Now.ToString("s");
            try
            {
                Directory.CreateDirectory(Dir);
                string path = File_(data.slot), temp = path + ".tmp";
                // Write to a temp file first so a crash mid-write can't wipe the old save.
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (Exception e) { Debug.LogWarning($"Autosave failed: {e.Message}"); }
        }

        public static void Delete(string slot)
        {
            try
            {
                if (File.Exists(File_(slot))) File.Delete(File_(slot));
                if (File.Exists(Thumbnail(slot))) File.Delete(Thumbnail(slot));
            }
            catch (Exception e) { Debug.LogWarning($"Couldn't delete save: {e.Message}"); }
        }

        /// What the camera sees right now, small, as the slot's picture.
        public static void SaveThumbnail(string slot, Camera cam)
        {
            if (cam == null || string.IsNullOrEmpty(slot)) return;
            try
            {
                var rt = RenderTexture.GetTemporary(384, 216, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
                else
                {
                    var old = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = old;
                }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(384, 216, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 384, 216), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(Thumbnail(slot), tex.EncodeToJPG(85));
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e) { Debug.LogWarning($"Couldn't save thumbnail: {e.Message}"); }
        }

        public static Texture2D LoadThumbnail(string slot)
        {
            try
            {
                string p = Thumbnail(slot);
                if (!File.Exists(p)) return null;
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(p));
                return tex;
            }
            catch { return null; }
        }
    }
}
