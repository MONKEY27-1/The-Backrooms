using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Backrooms.Meta
{
    /// What carries across every save slot: achievements, lifetime stats, and the level log (where you've been).
    [Serializable]
    public sealed class ProfileData
    {
        public List<string> achievements = new List<string>();
        public List<string> achievementDates = new List<string>();
        public List<LevelVisit> levels = new List<LevelVisit>();
        public float distance, playSeconds, longestSession, camcorderSeconds;
        public int timesCaught, huntsSurvived, almondWater, notesRead, paintingsTurned, boardsPried, bestHopChain;
        public int highestFloor, lowestFloor;
    }

    [Serializable]
    public sealed class LevelVisit
    {
        public string id;
        public string firstVisit;
        public string firstFrom;   // the level you came from the first time
        public int visits;
        public float seconds;
    }

    public static class Profile
    {
        static ProfileData data;
        static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "profile.json");

        public static ProfileData Data
        {
            get
            {
                if (data != null) return data;
                try { data = File.Exists(PathFile) ? JsonUtility.FromJson<ProfileData>(File.ReadAllText(PathFile)) : null; }
                catch (Exception e) { Debug.LogWarning($"Profile unreadable: {e.Message}"); }
                return data ??= new ProfileData();
            }
        }

        public static void Save()
        {
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(Data, true)); }
            catch (Exception e) { Debug.LogWarning($"Couldn't save the profile: {e.Message}"); }
        }

        public static LevelVisit Visit(string id) => Data.levels.Find(l => l.id == id);

        /// Records arriving in a level (first time: when, and from where).
        public static void Arrive(string id, string from)
        {
            var v = Visit(id);
            if (v == null)
            {
                v = new LevelVisit { id = id, firstVisit = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), firstFrom = from };
                Data.levels.Add(v);
            }
            v.visits++;
            Save();
        }

        /// How far the player has come: drives how strange the main menu gets.
        public static float Progress => Mathf.Clamp01(Data.levels.Count / 8f + Data.achievements.Count / 50f);
    }
}
