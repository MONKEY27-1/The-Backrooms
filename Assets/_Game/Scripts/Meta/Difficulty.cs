using UnityEngine;

namespace Backrooms.Meta
{
    public enum GameMode { Exploration, Survival, Hardcore, Endless, Story, Custom }

    /// Chosen per save at New Game. Everything that makes survival harder reads its numbers from here.
    public static class Difficulty
    {
        public static readonly string[] Names = { "Easy", "Normal", "Hard", "Extreme" };
        public static readonly string[] Blurbs =
        {
            "Slow thirst, plenty of supplies, a long quiet before anything happens.",
            "The intended balance: you'll need to search for water and batteries.",
            "Supplies are scarce, things start early, and the dark gets to you fast.",
            "Almost nothing to find, no grace period, every sound is a problem.",
        };

        /// The current save's difficulty (0 Easy .. 3 Extreme) and mode.
        public static int Level { get; set; } = 1;
        public static GameMode Mode { get; set; } = GameMode.Survival;
        public static bool SurvivalEnabled => Mode != GameMode.Exploration;
        public static bool Hardcore => Mode == GameMode.Hardcore;
        public static float CustomThirst { get; set; } = 1.3f;
        public static float CustomBattery { get; set; } = 1.2f;
        public static float CustomSanity { get; set; } = 1.15f;
        public static float CustomSupplies { get; set; } = 0.8f;
        public static void SetCustom(float thirst, float battery, float sanity, float supplies)
        {
            CustomThirst = Mathf.Clamp(thirst, 0f, 3f);
            CustomBattery = Mathf.Clamp(battery, 0f, 3f);
            CustomSanity = Mathf.Clamp(sanity, 0f, 3f);
            CustomSupplies = Mathf.Clamp01(supplies);
        }

        static float Pick(float easy, float normal, float hard, float extreme) =>
            Level <= 0 ? easy : Level == 1 ? normal : Level == 2 ? hard : extreme;
        static float ModeValue(float value, float custom) => !SurvivalEnabled ? 0f : Mode == GameMode.Custom ? custom : value;

        // Drain multipliers (Normal is deliberately harsher than the original tuning).
        public static float Thirst => ModeValue(Pick(0.7f, 1.3f, 1.8f, 2.6f), CustomThirst);
        public static float Battery => ModeValue(Pick(0.7f, 1.2f, 1.6f, 2.2f), CustomBattery);
        public static float Sanity => ModeValue(Pick(0.7f, 1.15f, 1.5f, 2f), CustomSanity);
        /// Share of pickups that are really there.
        public static float Supplies => !SurvivalEnabled ? 1f : Mode == GameMode.Custom ? CustomSupplies : Pick(1f, 0.8f, 0.55f, 0.35f);
        /// Fear director: seconds of peace on arriving somewhere, how long it waits between events, how fast tension builds.
        public static float Grace => Mode == GameMode.Exploration ? float.MaxValue : Pick(240f, 110f, 60f, 25f);
        public static float Cooldowns => Mode == GameMode.Exploration ? float.MaxValue : Pick(1.4f, 0.8f, 0.6f, 0.4f);
        public static float Tension => Mode == GameMode.Exploration ? 0f : Pick(0.7f, 1.3f, 1.7f, 2.2f);
        public static int StartWater => !SurvivalEnabled || Level <= 0 ? 2 : Level >= 3 ? 0 : 1;
        public static int StartBatteries => !SurvivalEnabled || Level <= 0 ? 2 : Level >= 2 ? 0 : 1;
    }
}
