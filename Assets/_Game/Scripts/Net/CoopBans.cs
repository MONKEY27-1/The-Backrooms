using System;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.Net
{
    /// The owner's co-op settings and ban list. Bans are forever (for this owner's games) until unbanned.
    public static class CoopBans
    {
        [Serializable] public sealed class Entry { public string id, name, when; }
        [Serializable] sealed class List { public List<Entry> bans = new List<Entry>(); }

        const string Key = "coop_bans";
        static List cache;

        static List Data
        {
            get
            {
                if (cache != null) return cache;
                try { cache = JsonUtility.FromJson<List>(PlayerPrefs.GetString(Key, "")) ?? new List(); }
                catch { cache = new List(); }
                return cache;
            }
        }

        public static IReadOnlyList<Entry> All => Data.bans;

        public static bool IsBanned(string playerId) => !string.IsNullOrEmpty(playerId) && Data.bans.Exists(b => b.id == playerId);

        public static void Ban(string playerId, string name)
        {
            if (string.IsNullOrEmpty(playerId) || IsBanned(playerId)) return;
            Data.bans.Add(new Entry { id = playerId, name = name, when = DateTime.Now.ToString("yyyy-MM-dd") });
            Save();
        }

        public static void Unban(string playerId)
        {
            Data.bans.RemoveAll(b => b.id == playerId);
            Save();
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        /// Ask the owner before anyone joins (off by default).
        public static bool ApproveJoins
        {
            get => PlayerPrefs.GetInt("coop_approve", 0) == 1;
            set { PlayerPrefs.SetInt("coop_approve", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// When the owner leaves, hand the world to another player instead of ending the game (off by default).
        public static bool KeepGoing
        {
            get => PlayerPrefs.GetInt("coop_keepgoing", 0) == 1;
            set { PlayerPrefs.SetInt("coop_keepgoing", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache = null;
    }
}
