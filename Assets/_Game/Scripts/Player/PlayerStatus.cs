using Backrooms.World;
using UnityEngine;

namespace Backrooms.Player
{
    /// The body and mind: stamina, thirst, sanity, and what's in the bag. 1 = full, 0 = empty.
    public sealed class PlayerStatus : MonoBehaviour
    {
        [Header("Rates (seconds to drain fully)")]
        [SerializeField] float thirstTime = 1500f;        // 25 minutes without a drink
        [SerializeField] float sanityDarkTime = 240f;     // 4 minutes standing in the dark
        [SerializeField] float sanityThirstTime = 400f;   // dehydration wears on the mind too
        [SerializeField] float sanityRecoverTime = 600f;  // slow recovery in the light
        [SerializeField] float darkDistance = 7f;         // no working light this close = dark

        public float Stamina = 1f, Thirst = 1f, Sanity = 1f;
        public int AlmondWater = 1, Batteries = 1;
        public readonly System.Collections.Generic.List<string> Logbook = new System.Collections.Generic.List<string>();   // title, newline, text

        public bool InDark { get; private set; }
        ChunkStreamer streamer;
        PlayerTools tools;

        void Awake()
        {
            streamer = FindFirstObjectByType<ChunkStreamer>();
            tools = GetComponent<PlayerTools>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Thirst = Mathf.Clamp01(Thirst - dt / thirstTime * Meta.Difficulty.Thirst);
            if (!Meta.Difficulty.SurvivalEnabled) { Thirst = 1f; Sanity = 1f; InDark = false; return; }
            bool lit = streamer == null || streamer.NearestLight < darkDistance;
            bool isLevel6 = streamer != null && streamer.Theme != null && streamer.Theme.id == "6";
            InDark = (!lit && (tools == null || !tools.FlashlightOn)) || isLevel6;
            float sanity = 0f;
            if (InDark) sanity -= dt / (isLevel6 ? sanityDarkTime * 0.6f : sanityDarkTime) * Meta.Difficulty.Sanity;
            if (Thirst < 0.2f) sanity -= dt / sanityThirstTime * Meta.Difficulty.Sanity;
            if (!InDark && Thirst >= 0.2f) sanity += dt / sanityRecoverTime;
            // Copy drift: the further from the original, the harder it is to stay sane.
            if (streamer != null) sanity -= dt * streamer.CurrentDrift / 900f;
            Sanity = Mathf.Clamp01(Sanity + sanity);
        }

        /// Almond water: quenches thirst and steadies the mind.
        public event System.Action Drank;

        public bool Drink()
        {
            if (AlmondWater <= 0) return false;
            AlmondWater--;
            Drank?.Invoke();
            Thirst = Mathf.Clamp01(Thirst + 0.6f);
            Sanity = Mathf.Clamp01(Sanity + 0.3f);
            return true;
        }
    }
}
