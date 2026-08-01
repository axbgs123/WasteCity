using System;

namespace WasteCity.World
{
    public sealed class WorldSeed
    {
        public int Value { get; }
        public WorldSeed(int value) => Value = value;
        public int Sample(int x, int y, int channel = 0)
        {
            unchecked
            {
                int hash = Value; hash = hash * 397 ^ x; hash = hash * 397 ^ y; hash = hash * 397 ^ channel;
                hash ^= hash >> 16; hash *= 0x45d9f3b; hash ^= hash >> 16;
                return hash & int.MaxValue;
            }
        }
    }
}
