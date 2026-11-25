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

        public float Stamina = 1f, Thirst = 1f, Sanity = 1f, Health = 1f;
        public int FirstAid, Repellent;   // repellent counts sprays: three to a can
        /// The Wretched Cycle (wiki Entity 15): 0 = well, 1 = you're one of them. Almond water stops it.
        public float Infection;
        public float BleedRate;
        public bool SeveredArm;
        public bool Limping => Health < 0.4f || BleedRate > 0.02f;
        const float CycleTime = 360f, NeglectTime = 90f;
        float neglect;
        float lastHurt = -999f;
        const float RegenDelay = 15f, RegenTime = 150f;   // healing starts 15 s after the last hit; 2.5 min to heal fully

        /// Wounded (a Hound's maul). Returns true if that was the end of you.
        public int AlmondWater = 1, Batteries = 1, Glowsticks, Exterminators, Flares;
        public readonly System.Collections.Generic.List<string> Logbook = new System.Collections.Generic.List<string>();   // title, newline, text

        public bool Hurt(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            lastHurt = Time.time;
            if (amount >= 0.2f)
            {
                BleedRate = Mathf.Clamp(BleedRate + amount * 0.06f, 0.015f, 0.06f);
                if (amount >= 0.3f && !SeveredArm && (Health < 0.35f || Random.value < 0.35f))
                {
                    SeveredArm = true;
                    BleedRate = Mathf.Max(BleedRate, 0.045f);
                    UI.Captions.Say("[your arm is ripped away!]", true);
                    Creatures.Blood.Burst(transform.position);
                }
            }
            return Health <= 0f;
        }

        public bool InDark { get; private set; }
        ChunkStreamer streamer;
        PlayerTools tools;

        void Awake()
        {
            streamer = FindFirstObjectByType<ChunkStreamer>();
            tools = GetComponent<PlayerTools>();
            gameObject.AddComponent<InfectionView>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Cycle(dt);
            if (BleedRate > 0f)
            {
                Health = Mathf.Max(0f, Health - BleedRate * dt);
                Creatures.Blood.Drip(GetComponent<PlayerAgent>(), Health);
                if (Health <= 0f) GetComponent<PlayerAgent>()?.Bleed(0.01f);
            }
            // Wounds close slowly while you're left alone and not parched.
            if (Health < 1f && BleedRate <= 0f && Time.time - lastHurt > RegenDelay && Thirst >= 0.2f) Health = Mathf.Min(1f, Health + dt / RegenTime);
            Thirst = Mathf.Clamp01(Thirst - dt / thirstTime * Meta.Difficulty.Thirst);
            if (!Meta.Difficulty.SurvivalEnabled) { Thirst = 1f; Sanity = 1f; InDark = false; return; }
            bool lit = streamer == null || streamer.NearestLight < darkDistance;
            bool isLevel6 = streamer != null && streamer.Theme != null && streamer.Theme.id == "6";
            InDark = (!lit && (tools == null || !tools.FlashlightOn)) || isLevel6;
            float sanity = 0f;
            if (InDark) sanity -= dt / (isLevel6 ? sanityDarkTime * 1.5f : sanityDarkTime) * Meta.Difficulty.Sanity;   // Level 6 is all dark: a slower wear (about 6 min)
            if (Thirst < 0.2f) sanity -= dt / sanityThirstTime * Meta.Difficulty.Sanity;
            if (!InDark && Thirst >= 0.2f) sanity += dt / sanityRecoverTime;
            // Copy drift: the further from the original, the harder it is to stay sane.
            if (streamer != null) sanity -= dt * streamer.CurrentDrift / 900f;
            Sanity = Mathf.Clamp01(Sanity + sanity);
        }

        void Cycle(float dt)
        {
            // Parched and half out of your mind for long enough starts it by itself.
            neglect = Meta.Difficulty.SurvivalEnabled && Thirst < 0.05f && Sanity < 0.25f ? neglect + dt : 0f;
            if (neglect > NeglectTime) { neglect = 0f; Infection = Mathf.Max(Infection, 0.02f); }
            if (Infection <= 0f) return;
            Infection += dt / CycleTime;
            if (Infection < 1f) return;
            Infection = 0f;
            UI.Captions.Say("you don't remember what you were", true);
            GetComponent<PlayerAgent>()?.Bleed(10f);
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
            Health = Mathf.Min(1f, Health + 0.12f);
            Infection = 0f;
            return true;
        }

        /// A first-aid kit: bandages and antiseptic, most of your health back.
        public event System.Action Patched;

        public bool Patch()
        {
            if (FirstAid <= 0 || (Health >= 0.99f && BleedRate <= 0f)) return false;
            FirstAid--;
            BleedRate = 0f;
            Health = Mathf.Min(1f, Health + 0.6f);
            Patched?.Invoke();
            return true;
        }
    }
}
