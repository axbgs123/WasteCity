using System;
using System.Collections.Generic;

namespace WasteCity.World.Exploration
{
    public enum WorldIntelKind
    {
        Resource = 0,
        Building = 1,
        Settlement = 2,
        Character = 3,
        Enemy = 4,
    }

    public enum WorldIntelState
    {
        Fresh = 0,
        Stale = 1,
        Expired = 2,
    }

    public readonly struct WorldIntelObservation
    {
        public WorldIntelObservation(
            string stableId,
            WorldIntelKind kind,
            int x,
            int y,
            string summary,
            bool hasMutableValue,
            int mutableValue,
            float observedRuleTimeSeconds,
            ulong sourceRevision = 0ul)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException(
                    "Intel stable ID is required.",
                    nameof(stableId));
            if (!Enum.IsDefined(typeof(WorldIntelKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!IsFiniteNonNegative(observedRuleTimeSeconds))
                throw new ArgumentOutOfRangeException(
                    nameof(observedRuleTimeSeconds));
            StableId = stableId;
            Kind = kind;
            X = x;
            Y = y;
            Summary = summary ?? string.Empty;
            HasMutableValue = hasMutableValue;
            MutableValue = hasMutableValue ? mutableValue : 0;
            ObservedRuleTimeSeconds = observedRuleTimeSeconds;
            SourceRevision = sourceRevision;
        }

        public string StableId { get; }
        public WorldIntelKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public string Summary { get; }
        public bool HasMutableValue { get; }
        public int MutableValue { get; }
        public float ObservedRuleTimeSeconds { get; }
        public ulong SourceRevision { get; }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }

    public readonly struct WorldIntelSnapshot
    {
        internal WorldIntelSnapshot(
            WorldIntelObservation observation,
            WorldIntelState state,
            float ageSeconds)
        {
            StableId = observation.StableId;
            Kind = observation.Kind;
            X = observation.X;
            Y = observation.Y;
            State = state;
            AgeSeconds = ageSeconds;
            ObservedRuleTimeSeconds = observation.ObservedRuleTimeSeconds;
            SourceRevision = observation.SourceRevision;
            bool expired = state == WorldIntelState.Expired;
            Summary = expired ? string.Empty : observation.Summary;
            HasMutableValue = !expired && observation.HasMutableValue;
            MutableValue = HasMutableValue ? observation.MutableValue : 0;
        }

        public string StableId { get; }
        public WorldIntelKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public string Summary { get; }
        public bool HasMutableValue { get; }
        public int MutableValue { get; }
        public WorldIntelState State { get; }
        public float AgeSeconds { get; }
        public float ObservedRuleTimeSeconds { get; }
        public ulong SourceRevision { get; }
    }

    public sealed class WorldIntelRuntime
    {
        private readonly SortedDictionary<string, WorldIntelObservation>
            observations =
                new SortedDictionary<string, WorldIntelObservation>(
                    StringComparer.Ordinal);

        public int Count => observations.Count;
        public ulong Revision { get; private set; }

        public bool Observe(WorldIntelObservation observation)
        {
            if (observations.TryGetValue(
                    observation.StableId,
                    out WorldIntelObservation existing))
            {
                if (observation.ObservedRuleTimeSeconds <
                        existing.ObservedRuleTimeSeconds ||
                    observation.ObservedRuleTimeSeconds ==
                        existing.ObservedRuleTimeSeconds &&
                    observation.SourceRevision <= existing.SourceRevision)
                    return false;
            }
            observations[observation.StableId] = observation;
            AdvanceRevision();
            return true;
        }

        public bool Remove(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                !observations.Remove(stableId))
                return false;
            AdvanceRevision();
            return true;
        }

        public bool TryGet(
            string stableId,
            float currentRuleTimeSeconds,
            out WorldIntelSnapshot snapshot)
        {
            snapshot = default;
            if (!IsFiniteNonNegative(currentRuleTimeSeconds) ||
                string.IsNullOrWhiteSpace(stableId) ||
                !observations.TryGetValue(
                    stableId,
                    out WorldIntelObservation observation))
                return false;
            if (currentRuleTimeSeconds < observation.ObservedRuleTimeSeconds)
                throw new ArgumentOutOfRangeException(
                    nameof(currentRuleTimeSeconds),
                    "Rule time cannot precede the observation.");
            float age = currentRuleTimeSeconds -
                observation.ObservedRuleTimeSeconds;
            WorldIntelState state = age >=
                    FormalExplorationCatalog3D.IntelExpiredSeconds
                ? WorldIntelState.Expired
                : age >= FormalExplorationCatalog3D.IntelStaleSeconds
                    ? WorldIntelState.Stale
                    : WorldIntelState.Fresh;
            snapshot = new WorldIntelSnapshot(observation, state, age);
            return true;
        }

        public WorldIntelObservation[] Capture()
        {
            var result = new WorldIntelObservation[observations.Count];
            var index = 0;
            foreach (KeyValuePair<string, WorldIntelObservation> item in
                     observations)
                result[index++] = item.Value;
            return result;
        }

        public bool TryRestore(
            IReadOnlyList<WorldIntelObservation> values,
            out string error)
        {
            if (values == null)
            {
                error = "Intel collection is required.";
                return false;
            }
            var candidate = new SortedDictionary<string,
                WorldIntelObservation>(StringComparer.Ordinal);
            for (var index = 0; index < values.Count; index++)
            {
                WorldIntelObservation value = values[index];
                if (string.IsNullOrWhiteSpace(value.StableId) ||
                    !candidate.TryAdd(value.StableId, value))
                {
                    error = "Intel stable IDs must be unique and non-empty.";
                    return false;
                }
            }
            observations.Clear();
            foreach (KeyValuePair<string, WorldIntelObservation> item in
                     candidate)
                observations.Add(item.Key, item.Value);
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }
}
