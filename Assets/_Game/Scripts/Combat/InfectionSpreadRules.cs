using System;
using System.Collections.Generic;

namespace WasteCity.Combat
{
    public readonly struct InfectionSpreadCandidate
    {
        public InfectionSpreadCandidate(int id, float x, float y, bool isAlive, bool isFriendly)
        {
            Id = id;
            X = x;
            Y = y;
            IsAlive = isAlive;
            IsFriendly = isFriendly;
        }

        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public bool IsAlive { get; }
        public bool IsFriendly { get; }
    }

    public static class InfectionSpreadRules
    {
        public static int[] SelectTargets(
            int sourceId,
            float sourceX,
            float sourceY,
            float radius,
            InfectionSpreadCandidate[] candidates)
        {
            if (candidates == null || candidates.Length == 0) return Array.Empty<int>();
            float normalizedRadius = Math.Max(0f, radius);
            float radiusSquared = normalizedRadius * normalizedRadius;
            var selected = new List<int>();
            foreach (InfectionSpreadCandidate candidate in candidates)
            {
                if (candidate.Id == sourceId || !candidate.IsAlive || candidate.IsFriendly) continue;
                float x = candidate.X - sourceX;
                float y = candidate.Y - sourceY;
                if (x * x + y * y <= radiusSquared) selected.Add(candidate.Id);
            }

            return selected.ToArray();
        }
    }
}
