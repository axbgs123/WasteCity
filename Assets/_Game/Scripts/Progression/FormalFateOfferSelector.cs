using System;
using System.Collections.Generic;

namespace WasteCity.Progression
{
    public static class FormalFateOfferSelector
    {
        public const int OfferCount = 3;

        public static IReadOnlyList<string> Select(
            string sessionId,
            int worldSeed,
            int version)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException(
                    "A non-blank session id is required.",
                    nameof(sessionId));
            if (version <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(version),
                    "The offer selector version must be positive.");

            IReadOnlyList<FormalFateDefinition> catalog =
                FormalFateCatalog.All;
            var candidates = new string[catalog.Count];
            for (var index = 0; index < catalog.Count; index++)
                candidates[index] = catalog[index].Id.Value;

            ulong state = BuildSeed(sessionId, worldSeed, version);
            for (var index = candidates.Length - 1; index > 0; index--)
            {
                int swapIndex = (int)(Next(ref state) % (ulong)(index + 1));
                string value = candidates[index];
                candidates[index] = candidates[swapIndex];
                candidates[swapIndex] = value;
            }

            return Array.AsReadOnly(new[]
            {
                candidates[0],
                candidates[1],
                candidates[2],
            });
        }

        private static ulong BuildSeed(
            string sessionId,
            int worldSeed,
            int version)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (var index = 0; index < sessionId.Length; index++)
            {
                char value = sessionId[index];
                hash = Mix(hash, (byte)value, prime);
                hash = Mix(hash, (byte)(value >> 8), prime);
            }
            hash = MixInt32(hash, worldSeed, prime);
            hash = MixInt32(hash, version, prime);
            return hash == 0UL ? offset : hash;
        }

        private static ulong MixInt32(ulong hash, int value, ulong prime)
        {
            uint bits = unchecked((uint)value);
            for (var shift = 0; shift < 32; shift += 8)
                hash = Mix(hash, (byte)(bits >> shift), prime);
            return hash;
        }

        private static ulong Mix(ulong hash, byte value, ulong prime)
        {
            hash ^= value;
            return unchecked(hash * prime);
        }

        private static ulong Next(ref ulong state)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return unchecked(state * 2685821657736338717UL);
        }
    }
}
