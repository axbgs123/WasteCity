using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Progression
{
    public enum AttentionPressureState
    {
        Queued,
        Warning,
        Active,
        Completed,
    }

    public enum AttentionPressureCommandKind
    {
        None,
        WarningStarted,
        StartEncounterRequested,
        EncounterCompleted,
    }

    public sealed class AttentionPressureCommand
    {
        private AttentionPressureCommand(
            AttentionPressureCommandKind kind,
            int threshold,
            string encounterId)
        {
            Kind = kind;
            Threshold = threshold;
            EncounterId = encounterId ?? string.Empty;
        }

        public static AttentionPressureCommand None { get; } =
            new AttentionPressureCommand(
                AttentionPressureCommandKind.None,
                0,
                string.Empty);

        public AttentionPressureCommandKind Kind { get; }
        public int Threshold { get; }
        public string EncounterId { get; }

        internal static AttentionPressureCommand For(
            AttentionPressureCommandKind kind,
            AttentionPressureDefinition definition)
        {
            return new AttentionPressureCommand(
                kind,
                definition.Threshold,
                definition.EncounterId.Value);
        }
    }

    public sealed class AttentionPressureEntrySnapshot
    {
        public AttentionPressureEntrySnapshot(
            int threshold,
            AttentionPressureState state,
            float warningRemainingSeconds)
        {
            Threshold = threshold;
            State = state;
            WarningRemainingSeconds = warningRemainingSeconds;
        }

        public int Threshold { get; }
        public AttentionPressureState State { get; }
        public float WarningRemainingSeconds { get; }
        public string EncounterId =>
            AttentionPressureCatalog.FindByThreshold(Threshold)?
                .EncounterId.Value ?? string.Empty;
    }

    public sealed class AttentionPressureSnapshot
    {
        private readonly ReadOnlyCollection<AttentionPressureEntrySnapshot>
            entries;

        public AttentionPressureSnapshot(
            ulong revision,
            AttentionPressureEntrySnapshot[] entries)
        {
            Revision = revision;
            AttentionPressureEntrySnapshot[] source = entries ??
                Array.Empty<AttentionPressureEntrySnapshot>();
            var copy = new AttentionPressureEntrySnapshot[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                AttentionPressureEntrySnapshot entry = source[index];
                copy[index] = entry == null
                    ? null
                    : new AttentionPressureEntrySnapshot(
                        entry.Threshold,
                        entry.State,
                        entry.WarningRemainingSeconds);
                if (entry != null && entry.Threshold == 90 &&
                    entry.State == AttentionPressureState.Completed)
                    CrystalBroodmotherDefeated = true;
            }
            this.entries = Array.AsReadOnly(copy);
        }

        public ulong Revision { get; }
        public IReadOnlyList<AttentionPressureEntrySnapshot> Entries => entries;
        public bool CrystalBroodmotherDefeated { get; }
    }

    public sealed class AttentionPressureRuntime
    {
        private const float WarningEpsilon = .00001f;
        public const float WarningVisibleSampleSeconds = .1f;

        private readonly List<Entry> entries = new List<Entry>(
            AttentionPressureCatalog.QueueCapacity);
        private float warningVisibleSampleAccumulator;
        private ulong revision;
        private AttentionPressureSnapshot cachedSnapshot;

        public AttentionPressureRuntime()
        {
            RebuildSnapshot();
        }

        public ulong Revision => revision;

        public bool TryQueueThreshold(int threshold, out string error)
        {
            AttentionPressureDefinition definition =
                AttentionPressureCatalog.FindByThreshold(threshold);
            if (definition == null || entries.Count >=
                    AttentionPressureCatalog.QueueCapacity ||
                FindEntry(threshold) != null)
            {
                error = "Pressure threshold is unknown, full, or already queued.";
                return false;
            }

            var candidate = new Entry(
                definition,
                AttentionPressureState.Queued,
                0f);
            int index = 0;
            while (index < entries.Count &&
                   entries[index].Definition.Threshold < threshold)
                index++;
            entries.Insert(index, candidate);
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool Tick(
            float ruleDeltaSeconds,
            bool tenWaveCampaignActive,
            bool tutorialWaveCompleted,
            bool firstMachineGunCompleted,
            out AttentionPressureCommand command,
            out string error)
        {
            command = AttentionPressureCommand.None;
            if (float.IsNaN(ruleDeltaSeconds) ||
                float.IsInfinity(ruleDeltaSeconds) ||
                ruleDeltaSeconds < 0f)
            {
                error = "Pressure rule delta must be finite and non-negative.";
                return false;
            }
            if (ruleDeltaSeconds == 0f || tenWaveCampaignActive)
            {
                error = string.Empty;
                return true;
            }

            Entry owner = CurrentOwner();
            if (owner != null)
            {
                if (owner.State == AttentionPressureState.Active)
                {
                    error = string.Empty;
                    return true;
                }
                owner.WarningRemainingSeconds -= ruleDeltaSeconds;
                warningVisibleSampleAccumulator += ruleDeltaSeconds;
                bool publish = warningVisibleSampleAccumulator +
                    WarningEpsilon >= WarningVisibleSampleSeconds;
                if (owner.WarningRemainingSeconds <= WarningEpsilon)
                {
                    owner.WarningRemainingSeconds = 0f;
                    owner.State = AttentionPressureState.Active;
                    warningVisibleSampleAccumulator = 0f;
                    publish = true;
                    command = AttentionPressureCommand.For(
                        AttentionPressureCommandKind.StartEncounterRequested,
                        owner.Definition);
                }
                else if (publish)
                {
                    warningVisibleSampleAccumulator %=
                        WarningVisibleSampleSeconds;
                }
                if (publish)
                {
                    unchecked { revision++; }
                    RebuildSnapshot();
                }
                error = string.Empty;
                return true;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry.State != AttentionPressureState.Queued ||
                    !IsReady(
                        entry.Definition.Threshold,
                        tutorialWaveCompleted,
                        firstMachineGunCompleted))
                    continue;
                entry.State = AttentionPressureState.Warning;
                entry.WarningRemainingSeconds =
                    entry.Definition.WarningSeconds;
                warningVisibleSampleAccumulator = 0f;
                command = AttentionPressureCommand.For(
                    AttentionPressureCommandKind.WarningStarted,
                    entry.Definition);
                unchecked { revision++; }
                RebuildSnapshot();
                error = string.Empty;
                return true;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCompleteActive(
            string encounterId,
            out AttentionPressureCommand command,
            out string error)
        {
            command = AttentionPressureCommand.None;
            Entry owner = CurrentOwner();
            if (owner == null || owner.State != AttentionPressureState.Active ||
                !string.Equals(
                    owner.Definition.EncounterId.Value,
                    encounterId,
                    StringComparison.Ordinal))
            {
                error = "The requested pressure encounter is not active.";
                return false;
            }
            owner.State = AttentionPressureState.Completed;
            owner.WarningRemainingSeconds = 0f;
            warningVisibleSampleAccumulator = 0f;
            command = AttentionPressureCommand.For(
                AttentionPressureCommandKind.EncounterCompleted,
                owner.Definition);
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public AttentionPressureSnapshot Capture()
        {
            return cachedSnapshot;
        }

        public bool TryRestore(
            AttentionPressureSnapshot snapshot,
            out string error)
        {
            if (snapshot == null || snapshot.Entries.Count >
                    AttentionPressureCatalog.QueueCapacity)
            {
                error = "Pressure snapshot is missing or exceeds capacity.";
                return false;
            }
            var prepared = new List<Entry>(snapshot.Entries.Count);
            int previousThreshold = 0;
            int ownerCount = 0;
            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                AttentionPressureEntrySnapshot saved = snapshot.Entries[index];
                AttentionPressureDefinition definition = saved == null
                    ? null
                    : AttentionPressureCatalog.FindByThreshold(saved.Threshold);
                if (saved == null || definition == null ||
                    saved.Threshold <= previousThreshold ||
                    !Enum.IsDefined(typeof(AttentionPressureState), saved.State) ||
                    float.IsNaN(saved.WarningRemainingSeconds) ||
                    float.IsInfinity(saved.WarningRemainingSeconds))
                {
                    error = "Pressure snapshot entries are invalid or unordered.";
                    return false;
                }
                bool warning = saved.State == AttentionPressureState.Warning;
                if (warning)
                {
                    if (saved.WarningRemainingSeconds <= 0f ||
                        saved.WarningRemainingSeconds > definition.WarningSeconds)
                    {
                        error = "Pressure warning time is invalid.";
                        return false;
                    }
                }
                else if (saved.WarningRemainingSeconds != 0f)
                {
                    error = "Only warning pressure may retain warning time.";
                    return false;
                }
                if (warning || saved.State == AttentionPressureState.Active)
                    ownerCount++;
                if (ownerCount > 1 ||
                    RequiresCompletedPredecessor(saved.State) &&
                    !HasCompletedPredecessor(prepared, saved.Threshold))
                {
                    error = "Pressure snapshot violates serial ownership.";
                    return false;
                }
                prepared.Add(new Entry(
                    definition,
                    saved.State,
                    saved.WarningRemainingSeconds));
                previousThreshold = saved.Threshold;
            }

            entries.Clear();
            entries.AddRange(prepared);
            revision = snapshot.Revision;
            warningVisibleSampleAccumulator = 0f;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private Entry FindEntry(int threshold)
        {
            for (var index = 0; index < entries.Count; index++)
                if (entries[index].Definition.Threshold == threshold)
                    return entries[index];
            return null;
        }

        private Entry CurrentOwner()
        {
            for (var index = 0; index < entries.Count; index++)
                if (entries[index].State == AttentionPressureState.Warning ||
                    entries[index].State == AttentionPressureState.Active)
                    return entries[index];
            return null;
        }

        private bool IsReady(
            int threshold,
            bool tutorialWaveCompleted,
            bool firstMachineGunCompleted)
        {
            if (threshold == 30)
                return tutorialWaveCompleted && firstMachineGunCompleted;
            int predecessor = threshold == 60 ? 30 : 60;
            Entry entry = FindEntry(predecessor);
            return entry != null &&
                entry.State == AttentionPressureState.Completed;
        }

        private static bool RequiresCompletedPredecessor(
            AttentionPressureState state)
        {
            return state == AttentionPressureState.Warning ||
                state == AttentionPressureState.Active ||
                state == AttentionPressureState.Completed;
        }

        private static bool HasCompletedPredecessor(
            IReadOnlyList<Entry> prepared,
            int threshold)
        {
            if (threshold == 30) return true;
            int predecessor = threshold == 60 ? 30 : 60;
            for (var index = 0; index < prepared.Count; index++)
                if (prepared[index].Definition.Threshold == predecessor)
                    return prepared[index].State ==
                        AttentionPressureState.Completed;
            return false;
        }

        private void RebuildSnapshot()
        {
            var snapshot = new AttentionPressureEntrySnapshot[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                snapshot[index] = new AttentionPressureEntrySnapshot(
                    entry.Definition.Threshold,
                    entry.State,
                    entry.WarningRemainingSeconds);
            }
            cachedSnapshot = new AttentionPressureSnapshot(
                revision,
                snapshot);
        }

        private sealed class Entry
        {
            public Entry(
                AttentionPressureDefinition definition,
                AttentionPressureState state,
                float warningRemainingSeconds)
            {
                Definition = definition;
                State = state;
                WarningRemainingSeconds = warningRemainingSeconds;
            }

            public AttentionPressureDefinition Definition { get; }
            public AttentionPressureState State { get; set; }
            public float WarningRemainingSeconds { get; set; }
        }
    }
}
