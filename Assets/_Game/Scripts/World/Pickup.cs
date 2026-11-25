using UnityEngine;

namespace Backrooms.World
{
    /// Something you can pick up: almond water or batteries. Id is stable per world position, so taken
    /// pickups stay gone when you come back.
    public sealed class Pickup : MonoBehaviour
    {
        public PropKind Kind;
        public int Id;
        public string Goal;   // a level goal's item (a fuse, a map page...): Id is its index

        public string Label => Goal ?? (Kind == PropKind.AlmondWater ? "almond water" : Kind == PropKind.Glowstick ? "glowsticks" : Kind == PropKind.Exterminator ? "a Smiler Exterminator" : Kind == PropKind.FirstAid ? "a first-aid kit" : Kind == PropKind.Repellent ? "moth repellent" : Kind == PropKind.Flare ? "a road flare" : "batteries");

        public static int IdAt(uint seed, Vector3 p) =>
            (int)WorldHash.Hash(seed ^ 0x9C1u, Mathf.RoundToInt(p.x * 20f), Mathf.RoundToInt(p.z * 20f), Mathf.RoundToInt(p.y * 20f));
    }
}
