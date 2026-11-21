namespace Backrooms.World
{
    /// Deterministic hashing: the same inputs give the same output on every machine and every run.
    /// All world generation draws from this, never from UnityEngine.Random.
    public static class WorldHash
    {
        public const uint DefaultWorldSeed = 0xB4C72019;

        public static uint Hash(uint seed, int a, int b = 0, int c = 0)
        {
            uint h = seed + 374761393u;
            h = Mix(h, (uint)a);
            h = Mix(h, (uint)b);
            h = Mix(h, (uint)c);
            h ^= h >> 15; h *= 2246822519u;
            h ^= h >> 13; h *= 3266489917u;
            h ^= h >> 16;
            return h;
        }

        /// Uniform float in [0, 1).
        public static float Value(uint seed, int a, int b = 0, int c = 0) =>
            (Hash(seed, a, b, c) >> 8) * (1f / 16777216f);

        /// Uniform float in [0, 1) from a pre-computed hash or indexed seed.
        public static float Float(uint hash, int index = 0) =>
            Value(hash, index);

        public static uint LevelSeed(uint worldSeed, int levelId) => Hash(worldSeed, levelId, 0, 0x4C56);

        public static uint LevelSeed(uint worldSeed, string levelId) => LevelSeed(worldSeed, (int)StringHash(levelId));

        /// FNV-1a: a stable hash for level ids and typed-in seeds ("yellow" is always the same world).
        public static uint StringHash(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s) h = (h ^ c) * 16777619u;
            return h;
        }

        static uint Mix(uint h, uint v)
        {
            h += v * 3266489917u;
            return ((h << 17) | (h >> 15)) * 668265263u;
        }
    }

    /// Small deterministic sequence (xorshift32) for planning one chunk from its own seed.
    public sealed class WorldRng
    {
        uint state;

        public WorldRng(uint seed) => state = seed == 0 ? 0x9E3779B9u : seed;

        public uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// Uniform float in [0, 1).
        public float Value() => (Next() >> 8) * (1f / 16777216f);

        /// Uniform int in [min, maxExclusive). Requires maxExclusive > min.
        public int Range(int min, int maxExclusive) => min + (int)(Next() % (uint)(maxExclusive - min));
    }
}
