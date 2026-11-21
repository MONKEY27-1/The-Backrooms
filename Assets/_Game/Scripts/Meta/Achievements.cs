using System;
using UnityEngine;

namespace Backrooms.Meta
{
    /// Local achievements, kept in the profile (shared by every save slot).
    public static class Achievements
    {
        public sealed class Def
        {
            public readonly string Id, Name, Text;
            public readonly bool Hidden;
            public Def(string id, string name, string text, bool hidden = false) { Id = id; Name = name; Text = text; Hidden = hidden; }
        }

        public static readonly Def[] All =
        {
            new Def("first_steps", "Noclipped", "Fall out of reality into Level 0."),
            new Def("threshold", "Threshold", "Walk 500 metres."),
            new Def("lost", "Truly Lost", "Walk 5 kilometres in total."),
            new Def("lvl_1", "Habitable Zone", "Reach Level 1."),
            new Def("lvl_2", "Pipe Dreams", "Reach Level 2."),
            new Def("lvl_37", "Sublimity", "Reach Level 37."),
            new Def("lvl_0.2", "Fresh Paint", "Find Level 0.2."),
            new Def("lvl_0.3", "Cold Feet", "Find Level 0.3."),
            new Def("lvl_19", "Up in the Rafters", "Climb into Level 19."),
            new Def("lvl_-1", "Deep Storage", "Fall through to Level -1."),
            new Def("lvl_fun", "Party Crasher", "Find Level Fun =)."),
            new Def("lvl_188", "Watched", "Climb into the Courtyard of Windows."),
            new Def("escaped_0.2", "Got Out in Time", "Leave Level 0.2 through the door you came in."),
            new Def("drained", "Pump Room", "Drain the pool in Level -1."),
            new Def("floor_3", "Top Floor", "Reach the highest floor of Level 0."),
            new Def("floor_-3", "Rock Bottom", "Reach the lowest floor of Level 0."),
            new Def("the_cut", "Mind the Gap", "Stand at the edge of the Cut."),
            new Def("survived_hunt", "Lights Back On", "Live through a hunt."),
            new Def("caught", "Smile Back", "Get caught in the dark.", hidden: true),
            new Def("hydrated", "Hydrated", "Drink 10 almond waters."),
            new Def("reader", "Reader", "Read 10 notes."),
            new Def("archivist", "Found Footage", "Record 2 minutes on the camcorder."),
            new Def("art_critic", "Art Critic", "Turn over 5 paintings."),
            new Def("carpenter", "Crowbar Not Included", "Pry 5 boarded doorways open."),
            new Def("bunny", "Bunny Hop", "Chain 5 jumps in a row."),
            new Def("marathon", "Marathon", "Play for 30 minutes without stopping."),
            new Def("faceling", "Familiar Face", "See a Faceling."),
        };

        /// Fired once per new unlock (the toast listens).
        public static event Action<Def> Unlocked;

        public static bool Has(string id) => Profile.Data.achievements.Contains(id);

        public static Def Find(string id) => Array.Find(All, d => d.Id == id);

        public static void Unlock(string id)
        {
            var def = Find(id);
            if (def == null) { Debug.LogWarning($"Unknown achievement '{id}'"); return; }
            if (Has(id)) return;
            Profile.Data.achievements.Add(id);
            Profile.Data.achievementDates.Add(DateTime.Now.ToString("yyyy-MM-dd"));
            Profile.Save();
            Unlocked?.Invoke(def);
        }

        public static string DateOf(string id)
        {
            int i = Profile.Data.achievements.IndexOf(id);
            return i >= 0 && i < Profile.Data.achievementDates.Count ? Profile.Data.achievementDates[i] : "";
        }
    }
}
