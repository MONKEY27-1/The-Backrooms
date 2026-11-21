using UnityEngine;

namespace Backrooms.World
{
    /// A failing ceiling panel. Its light (if the rig assigned one) follows the same timing via Flicker.On.
    public sealed class FaultyPanel : MonoBehaviour
    {
        public int Id;
        public Material Lit, Dead;
        MeshRenderer panel;
        bool on = true;

        void Awake() => panel = GetComponent<MeshRenderer>();

        void Update()
        {
            bool now = Flicker.On(Id, Time.time);
            if (now == on) return;
            on = now;
            var mats = panel.sharedMaterials;   // slot 0 is the lens; a troffer's frame stays in slot 1
            mats[0] = on ? Lit : Dead;
            panel.sharedMaterials = mats;
        }
    }

    public static class Flicker
    {
        /// Mostly steady, with bursts of stuttering. Deterministic per id and time, so panel and light agree.
        public static bool On(int id, float time)
        {
            int slot = Mathf.FloorToInt(time * 14f);
            if (Meta.GameSettings.ReduceFlashing) return WorldHash.Value((uint)id, slot >> 5, 0, 78) > 0.15f;   // slow changes only, no strobing
            if (WorldHash.Value((uint)id, slot >> 5, 0, 78) > 0.35f) return true;
            return WorldHash.Value((uint)id, slot, 0, 77) > 0.4f;
        }
    }
}
