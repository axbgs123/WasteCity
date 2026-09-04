using System;
using System.Collections.Generic;

namespace WasteCity.Persistence
{
    public static class FormalSaveCheckpointReasonIds
    {
        public const string NewGameReady = "new-game-ready";
        public const string FirstDeploymentComplete =
            "first-deployment-complete";
        public const string FirstMachineGunComplete =
            "first-machine-gun-complete";
        public const string TutorialCombatStarted =
            "tutorial-combat-started";
        public const string CampaignWaveWarningStarted =
            "campaign-wave-warning-started";
        public const string EvacuationBatchConfirmed =
            "evacuation-batch-confirmed";
        public const string EvacuationWorkCommitted =
            "evacuation-work-committed";
        public const string PackingComplete = "packing-complete";

        public const string FateSelectionComplete =
            "fate-selection-complete";
        public const string RewindAnchorCreated = "rewind-anchor-created";
        public const string RewindAnchorUsed = "rewind-anchor-used";
        public const string RewindAnchorCleared = "rewind-anchor-cleared";
        public const string PressureWarningStarted =
            "pressure-warning-started";
        public const string PressureEncounterStarted =
            "pressure-encounter-started";
        public const string PressureEncounterCompleted =
            "pressure-encounter-completed";
        public const string BossEventStarted = "boss-event-started";
        public const string FirstCivilizationAscension =
            "first-civilization-ascension";
        public const string ExplorationScanCompleted =
            "exploration-scan-completed";
        public const string CenJinRescueCompleted =
            "cen-jin-rescue-completed";
    }

    public sealed class FormalSaveCheckpointPolicy
    {
        private sealed class PendingBatch
        {
            public readonly HashSet<string> EventKeys =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> MilestoneIds =
                new HashSet<string>(StringComparer.Ordinal);
            public string ReasonId;
            public int Priority = int.MinValue;

            public bool IsEmpty => EventKeys.Count == 0;
        }

        private readonly Func<FormalSaveCheckpointMetadata, bool> save;
        private readonly Func<float> captureRuleTime;
        private readonly HashSet<string> completedMilestoneIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> committedEventKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<string, string> boundListener;
        private readonly Dictionary<
            string,
            Action<Action<string, string>>> boundUnsubscribers =
                new Dictionary<
                    string,
                    Action<Action<string, string>>>(StringComparer.Ordinal);

        private PendingBatch pending = new PendingBatch();
        private long sequence;
        private bool isSaving;
        private bool isSuppressed;
        private bool hasFailureWarning;
        private ulong pendingRevision;

        public FormalSaveCheckpointPolicy(
            Func<FormalSaveCheckpointMetadata, bool> save,
            Func<float> captureRuleTime)
        {
            this.save = save ??
                throw new ArgumentNullException(nameof(save));
            this.captureRuleTime = captureRuleTime ??
                throw new ArgumentNullException(nameof(captureRuleTime));
            boundListener = QueueFromBoundSource;
        }

        public long Sequence => sequence;

        public IReadOnlyCollection<string> CompletedMilestoneIds =>
            SortedCopy(completedMilestoneIds);

        public bool HasPending => !pending.IsEmpty;
        public bool IsSaving => isSaving;
        public bool HasFailureWarning => hasFailureWarning;
        public bool IsSuppressed => isSuppressed;
        public ulong PendingRevision => pendingRevision;

        public bool QueueCheckpoint(
            string reasonId,
            string stableEventId)
        {
            if (isSuppressed ||
                string.IsNullOrWhiteSpace(stableEventId) ||
                !TryDescribeReason(
                    reasonId,
                    out int priority,
                    out bool oneShot))
            {
                return false;
            }

            if (oneShot && completedMilestoneIds.Contains(reasonId))
                return false;

            string eventKey = CreateEventKey(reasonId, stableEventId);
            if (committedEventKeys.Contains(eventKey) ||
                !pending.EventKeys.Add(eventKey))
            {
                return false;
            }

            if (oneShot)
                pending.MilestoneIds.Add(reasonId);
            unchecked { pendingRevision++; }
            if (priority > pending.Priority ||
                priority == pending.Priority &&
                string.CompareOrdinal(reasonId, pending.ReasonId) < 0)
            {
                pending.Priority = priority;
                pending.ReasonId = reasonId;
            }
            return true;
        }

        public bool FlushPending()
        {
            if (isSuppressed || isSaving || pending.IsEmpty)
                return false;

            PendingBatch inFlight = pending;
            pending = new PendingBatch();
            isSaving = true;
            bool succeeded;
            try
            {
                var metadata = new FormalSaveCheckpointMetadata
                {
                    sequence = sequence + 1L,
                    reasonId = inFlight.ReasonId,
                    ruleTimeSeconds = captureRuleTime(),
                    completedMilestoneIds = BuildMilestoneSnapshot(inFlight),
                };
                succeeded = save(metadata);
            }
            catch
            {
                succeeded = false;
            }
            finally
            {
                isSaving = false;
            }

            if (!succeeded)
            {
                MergePending(inFlight);
                hasFailureWarning = true;
                return false;
            }

            sequence++;
            completedMilestoneIds.UnionWith(inFlight.MilestoneIds);
            committedEventKeys.UnionWith(inFlight.EventKeys);
            PruneCommittedPending();
            hasFailureWarning = false;
            return true;
        }

        public bool TryRestoreBaseline(FormalSaveCheckpointMetadata checkpoint)
        {
            if (isSaving || checkpoint == null || checkpoint.sequence < 0L ||
                checkpoint.completedMilestoneIds == null)
            {
                return false;
            }

            var restoredMilestones = new HashSet<string>(
                StringComparer.Ordinal);
            for (var index = 0;
                 index < checkpoint.completedMilestoneIds.Length;
                 index++)
            {
                string milestoneId =
                    checkpoint.completedMilestoneIds[index];
                if (string.IsNullOrWhiteSpace(milestoneId) ||
                    !restoredMilestones.Add(milestoneId))
                {
                    return false;
                }
            }

            sequence = checkpoint.sequence;
            completedMilestoneIds.Clear();
            completedMilestoneIds.UnionWith(restoredMilestones);
            committedEventKeys.Clear();
            pending = new PendingBatch();
            hasFailureWarning = false;
            pendingRevision = 0;
            return true;
        }

        public void SetSuppressed(bool suppressed)
        {
            isSuppressed = suppressed;
        }

        public void Bind(
            string bindingId,
            Action<Action<string, string>> subscribe,
            Action<Action<string, string>> unsubscribe)
        {
            if (string.IsNullOrWhiteSpace(bindingId))
                throw new ArgumentException(
                    "Binding ID must not be blank.",
                    nameof(bindingId));
            if (subscribe == null)
                throw new ArgumentNullException(nameof(subscribe));
            if (unsubscribe == null)
                throw new ArgumentNullException(nameof(unsubscribe));

            Unbind(bindingId);
            boundUnsubscribers.Add(bindingId, unsubscribe);
            try
            {
                subscribe(boundListener);
            }
            catch
            {
                boundUnsubscribers.Remove(bindingId);
                throw;
            }
        }

        public bool Unbind(string bindingId)
        {
            if (string.IsNullOrWhiteSpace(bindingId) ||
                !boundUnsubscribers.TryGetValue(
                    bindingId,
                    out Action<Action<string, string>> unsubscribe))
            {
                return false;
            }

            boundUnsubscribers.Remove(bindingId);
            unsubscribe(boundListener);
            return true;
        }

        public void Unbind()
        {
            if (boundUnsubscribers.Count == 0)
                return;

            var unsubscribers = new List<Action<Action<string, string>>>(
                boundUnsubscribers.Values);
            boundUnsubscribers.Clear();
            for (var index = 0; index < unsubscribers.Count; index++)
                unsubscribers[index](boundListener);
        }

        private void QueueFromBoundSource(
            string reasonId,
            string stableEventId)
        {
            QueueCheckpoint(reasonId, stableEventId);
        }

        private string[] BuildMilestoneSnapshot(PendingBatch inFlight)
        {
            var milestones = new HashSet<string>(
                completedMilestoneIds,
                StringComparer.Ordinal);
            milestones.UnionWith(inFlight.MilestoneIds);
            return SortedCopy(milestones);
        }

        private void MergePending(PendingBatch batch)
        {
            pending.EventKeys.UnionWith(batch.EventKeys);
            pending.MilestoneIds.UnionWith(batch.MilestoneIds);
            if (batch.Priority > pending.Priority ||
                batch.Priority == pending.Priority &&
                string.CompareOrdinal(batch.ReasonId, pending.ReasonId) < 0)
            {
                pending.Priority = batch.Priority;
                pending.ReasonId = batch.ReasonId;
            }
        }

        private void PruneCommittedPending()
        {
            pending.EventKeys.ExceptWith(committedEventKeys);
            pending.EventKeys.RemoveWhere(eventKey =>
            {
                string reasonId = ReadReasonFromEventKey(eventKey);
                return completedMilestoneIds.Contains(reasonId) &&
                    TryDescribeReason(
                        reasonId,
                        out int ignoredPriority,
                        out bool oneShot) &&
                    oneShot;
            });
            pending.MilestoneIds.ExceptWith(completedMilestoneIds);
            if (pending.IsEmpty)
            {
                pending = new PendingBatch();
                return;
            }

            RecalculatePendingReason();
        }

        private void RecalculatePendingReason()
        {
            pending.Priority = int.MinValue;
            pending.ReasonId = null;
            foreach (string eventKey in pending.EventKeys)
            {
                string reasonId = ReadReasonFromEventKey(eventKey);
                if (!TryDescribeReason(
                        reasonId,
                        out int priority,
                        out bool ignored))
                {
                    continue;
                }
                if (priority > pending.Priority ||
                    priority == pending.Priority &&
                    string.CompareOrdinal(reasonId, pending.ReasonId) < 0)
                {
                    pending.Priority = priority;
                    pending.ReasonId = reasonId;
                }
            }
        }

        private static bool TryDescribeReason(
            string reasonId,
            out int priority,
            out bool oneShot)
        {
            switch (reasonId)
            {
                case FormalSaveCheckpointReasonIds.NewGameReady:
                    priority = 10;
                    oneShot = true;
                    return true;
                case FormalSaveCheckpointReasonIds.FirstDeploymentComplete:
                    priority = 20;
                    oneShot = true;
                    return true;
                case FormalSaveCheckpointReasonIds.FirstMachineGunComplete:
                    priority = 30;
                    oneShot = true;
                    return true;
                case FormalSaveCheckpointReasonIds.TutorialCombatStarted:
                    priority = 40;
                    oneShot = true;
                    return true;
                case FormalSaveCheckpointReasonIds.FateSelectionComplete:
                    priority = 41;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.RewindAnchorCreated:
                    priority = 42;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.RewindAnchorUsed:
                    priority = 43;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.RewindAnchorCleared:
                    priority = 44;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.CampaignWaveWarningStarted:
                    priority = 45;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.PressureWarningStarted:
                    priority = 46;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.PressureEncounterStarted:
                    priority = 47;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.PressureEncounterCompleted:
                    priority = 48;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.FirstCivilizationAscension:
                    priority = 49;
                    oneShot = true;
                    return true;
                case FormalSaveCheckpointReasonIds.EvacuationBatchConfirmed:
                    priority = 50;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.EvacuationWorkCommitted:
                    priority = 60;
                    oneShot = false;
                    return true;
                case FormalSaveCheckpointReasonIds.PackingComplete:
                    priority = 70;
                    oneShot = false;
                    return true;
                default:
                    priority = int.MinValue;
                    oneShot = false;
                    return false;
            }
        }

        private static string CreateEventKey(
            string reasonId,
            string stableEventId)
        {
            return reasonId + "\n" + stableEventId;
        }

        private static string ReadReasonFromEventKey(string eventKey)
        {
            int separator = eventKey.IndexOf('\n');
            return separator < 0
                ? eventKey
                : eventKey.Substring(0, separator);
        }

        private static string[] SortedCopy(IEnumerable<string> source)
        {
            var result = new List<string>(source).ToArray();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }
}
