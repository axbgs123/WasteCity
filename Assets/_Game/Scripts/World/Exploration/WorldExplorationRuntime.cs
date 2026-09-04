using System;
using System.Collections.Generic;

namespace WasteCity.World.Exploration
{
    public readonly struct WorldExplorationScanRecord
    {
        public WorldExplorationScanRecord(
            string zoneId,
            string stableEventKey)
        {
            ZoneId = zoneId ?? string.Empty;
            StableEventKey = stableEventKey ?? string.Empty;
        }

        public string ZoneId { get; }
        public string StableEventKey { get; }
    }

    public sealed class WorldExplorationSnapshot
    {
        private readonly bool[] exploredCells;
        private readonly WorldExplorationScanRecord[] scanRecords;
        private readonly WorldIntelObservation[] intel;

        public WorldExplorationSnapshot(
            int width,
            int height,
            IReadOnlyList<bool> exploredCells,
            IReadOnlyList<WorldExplorationScanRecord> scanRecords,
            IReadOnlyList<WorldIntelObservation> intel,
            ulong revision)
        {
            Width = width;
            Height = height;
            this.exploredCells = Copy(exploredCells, nameof(exploredCells));
            this.scanRecords = Copy(scanRecords, nameof(scanRecords));
            this.intel = Copy(intel, nameof(intel));
            Revision = revision;
        }

        public int Width { get; }
        public int Height { get; }
        public bool[] ExploredCells => (bool[])exploredCells.Clone();
        public WorldExplorationScanRecord[] ScanRecords =>
            (WorldExplorationScanRecord[])scanRecords.Clone();
        public WorldIntelObservation[] Intel =>
            (WorldIntelObservation[])intel.Clone();
        public ulong Revision { get; }

        internal bool[] CopyExploredCells()
        {
            return (bool[])exploredCells.Clone();
        }

        internal WorldExplorationScanRecord[] CopyScanRecords()
        {
            return (WorldExplorationScanRecord[])scanRecords.Clone();
        }

        internal WorldIntelObservation[] CopyIntel()
        {
            return (WorldIntelObservation[])intel.Clone();
        }

        private static T[] Copy<T>(
            IReadOnlyList<T> values,
            string parameterName)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);
            var copy = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
                copy[index] = values[index];
            return copy;
        }
    }

    /// <summary>
    /// Pure composition owner for IDEA-0029 exploration state. Callers feed
    /// observations only when a local sight-source change or an authoritative
    /// visible fact changes; this type never polls the world.
    /// </summary>
    public sealed class WorldExplorationRuntime
    {
        private const string ScanEventKeyPrefix = "exploration.scan:";

        private readonly string sessionId;
        private readonly TryCommitExplorationAttention attentionCommitter;
        private readonly Dictionary<string, string> scanEventKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private WorldVisibilityRuntime visibility;
        private WorldIntelRuntime intel;
        private WorldScanRuntime scan;

        public WorldExplorationRuntime(
            int width,
            int height,
            string sessionId,
            TryCommitExplorationAttention attentionCommitter)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException(
                    "Exploration session ID is required.",
                    nameof(sessionId));
            this.sessionId = sessionId;
            this.attentionCommitter = attentionCommitter ??
                throw new ArgumentNullException(nameof(attentionCommitter));
            visibility = new WorldVisibilityRuntime(width, height);
            intel = new WorldIntelRuntime();
            scan = CreateScan(visibility);
        }

        public int Width => visibility.Width;
        public int Height => visibility.Height;
        public int SourceCount => visibility.SourceCount;
        public int IntelCount => intel.Count;
        public int ScannedZoneCount => scan.ScannedZoneCount;
        public ulong Revision { get; private set; }

        public WorldVisibilityState GetState(int x, int y)
        {
            return visibility.GetState(x, y);
        }

        public bool IsVisible(int x, int y)
        {
            return visibility.IsVisible(x, y);
        }

        public bool IsExplored(int x, int y)
        {
            return visibility.IsExplored(x, y);
        }

        public bool UpsertSource(WorldVisionSource source)
        {
            if (!visibility.UpsertSource(source)) return false;
            AdvanceRevision();
            return true;
        }

        public bool RemoveSource(string stableSourceId)
        {
            if (!visibility.RemoveSource(stableSourceId)) return false;
            AdvanceRevision();
            return true;
        }

        public bool TryGetSource(
            string stableSourceId,
            out WorldVisionSource source)
        {
            return visibility.TryGetSource(stableSourceId, out source);
        }

        public bool IsScanned(string stableZoneId)
        {
            return scan.IsScanned(stableZoneId);
        }

        public bool TryGetScanEventKey(
            string stableZoneId,
            out string stableEventKey)
        {
            stableEventKey = string.Empty;
            return !string.IsNullOrWhiteSpace(stableZoneId) &&
                scanEventKeys.TryGetValue(stableZoneId, out stableEventKey);
        }

        public bool TryGetIntel(
            string stableId,
            float currentRuleTimeSeconds,
            out WorldIntelSnapshot snapshot)
        {
            return intel.TryGet(stableId, currentRuleTimeSeconds, out snapshot);
        }

        public bool TryObserveVisibleResource(
            WorldIntelObservation observation,
            out WorldScanResult scanResult,
            out string error)
        {
            scanResult = new WorldScanResult(
                WorldScanStatus.None, string.Empty, string.Empty,
                string.Empty, 0);
            if (observation.Kind != WorldIntelKind.Resource)
            {
                error = "Only resource observations use this entry point.";
                return false;
            }
            if (observation.X < 0 || observation.Y < 0 ||
                observation.X >= Width || observation.Y >= Height)
            {
                error = "Resource observation is outside the world.";
                return false;
            }
            if (!visibility.IsVisible(observation.X, observation.Y))
            {
                error = "Resource observation is outside current vision.";
                return false;
            }

            ExplorationScanZoneDefinition zone =
                FormalExplorationCatalog3D.FindScanZoneForNode(
                    observation.StableId);
            if (zone == null || scan.IsScanned(zone.StableId))
            {
                bool changed = intel.Observe(observation);
                if (changed) AdvanceRevision();
                if (zone != null)
                {
                    scanResult = new WorldScanResult(
                        WorldScanStatus.AlreadyScanned,
                        zone.StableId,
                        zone.AttentionReasonId,
                        scanEventKeys.TryGetValue(
                            zone.StableId,
                            out string eventKey)
                            ? eventKey
                            : string.Empty,
                        0);
                }
                error = string.Empty;
                return true;
            }

            WorldIntelObservation[] beforeIntel = intel.Capture();
            if (IsOlderThanRecorded(observation, beforeIntel))
            {
                error = "Resource observation is older than recorded intel.";
                return false;
            }
            bool intelChanged = intel.Observe(observation);
            bool scanCommitted;
            try
            {
                scanCommitted = scan.TryScanVisibleNode(
                    sessionId,
                    observation.StableId,
                    observation.X,
                    observation.Y,
                    out scanResult,
                    out error);
            }
            catch (Exception exception)
            {
                scanResult = new WorldScanResult(
                    WorldScanStatus.AttentionRejected,
                    zone.StableId,
                    zone.AttentionReasonId,
                    ScanEventKeyPrefix + sessionId + ":" + zone.StableId,
                    0);
                error = "Attention owner failed: " + exception.Message;
                RollBackIntel(beforeIntel, intelChanged);
                return false;
            }

            if (scanCommitted)
            {
                scanEventKeys.Add(
                    scanResult.ZoneId,
                    scanResult.StableEventKey);
                AdvanceRevision();
                return true;
            }
            RollBackIntel(beforeIntel, intelChanged);
            return false;
        }

        public bool TryObserveVisibleIntel(
            WorldIntelObservation observation,
            out string error)
        {
            if (observation.Kind == WorldIntelKind.Resource)
            {
                return TryObserveVisibleResource(
                    observation,
                    out _,
                    out error);
            }
            if (observation.X < 0 || observation.Y < 0 ||
                observation.X >= Width || observation.Y >= Height)
            {
                error = "Intel observation is outside the world.";
                return false;
            }
            if (!visibility.IsVisible(observation.X, observation.Y))
            {
                error = "Intel observation is outside current vision.";
                return false;
            }

            bool changed = intel.Observe(observation);
            if (changed) AdvanceRevision();
            error = string.Empty;
            return true;
        }

        public WorldExplorationSnapshot Capture()
        {
            string[] zoneIds = scan.CaptureScannedZoneIds();
            var records = new WorldExplorationScanRecord[zoneIds.Length];
            for (var index = 0; index < zoneIds.Length; index++)
            {
                string zoneId = zoneIds[index];
                records[index] = new WorldExplorationScanRecord(
                    zoneId,
                    scanEventKeys.TryGetValue(zoneId, out string eventKey)
                        ? eventKey
                        : string.Empty);
            }
            return new WorldExplorationSnapshot(
                Width,
                Height,
                visibility.CaptureExplored(),
                records,
                intel.Capture(),
                Revision);
        }

        public bool TryRestore(
            WorldExplorationSnapshot snapshot,
            out string error)
        {
            if (snapshot == null || snapshot.Width != Width ||
                snapshot.Height != Height)
            {
                error = "Exploration snapshot dimensions must match.";
                return false;
            }

            var candidateVisibility =
                new WorldVisibilityRuntime(Width, Height);
            if (!candidateVisibility.TryRestoreExplored(
                    snapshot.CopyExploredCells(), out error))
                return false;

            WorldIntelObservation[] restoredIntel = snapshot.CopyIntel();
            for (var index = 0; index < restoredIntel.Length; index++)
            {
                WorldIntelObservation observation = restoredIntel[index];
                if (observation.X < 0 || observation.Y < 0 ||
                    observation.X >= Width || observation.Y >= Height)
                {
                    error = "Restored intel must be inside the world.";
                    return false;
                }
            }

            var candidateIntel = new WorldIntelRuntime();
            if (!candidateIntel.TryRestore(restoredIntel, out error))
                return false;

            WorldExplorationScanRecord[] records =
                snapshot.CopyScanRecords();
            var candidateEventKeys = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var zoneIds = new string[records.Length];
            var uniqueEventKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < records.Length; index++)
            {
                WorldExplorationScanRecord record = records[index];
                if (FormalExplorationCatalog3D.FindScanZone(
                        record.ZoneId) == null ||
                    !IsValidScanEventKey(
                        record.ZoneId,
                        record.StableEventKey) ||
                    !candidateEventKeys.TryAdd(
                        record.ZoneId,
                        record.StableEventKey) ||
                    !uniqueEventKeys.Add(record.StableEventKey))
                {
                    error = "Scan records must contain unique formal IDs " +
                        "and stable event keys.";
                    return false;
                }
                zoneIds[index] = record.ZoneId;
            }

            WorldScanRuntime candidateScan = CreateScan(candidateVisibility);
            if (!candidateScan.TryRestoreScannedZoneIds(zoneIds, out error))
                return false;

            visibility = candidateVisibility;
            intel = candidateIntel;
            scan = candidateScan;
            scanEventKeys.Clear();
            foreach (KeyValuePair<string, string> item in candidateEventKeys)
                scanEventKeys.Add(item.Key, item.Value);
            Revision = Revision == ulong.MaxValue
                ? ulong.MaxValue
                : Math.Max(snapshot.Revision, Revision + 1ul);
            error = string.Empty;
            return true;
        }

        private WorldScanRuntime CreateScan(
            WorldVisibilityRuntime targetVisibility)
        {
            return new WorldScanRuntime(
                targetVisibility,
                attentionCommitter);
        }

        private bool IsValidScanEventKey(
            string zoneId,
            string stableEventKey)
        {
            return string.Equals(
                stableEventKey,
                ScanEventKeyPrefix + sessionId + ":" + zoneId,
                StringComparison.Ordinal);
        }

        private static bool IsOlderThanRecorded(
            WorldIntelObservation observation,
            IReadOnlyList<WorldIntelObservation> recorded)
        {
            for (var index = 0; index < recorded.Count; index++)
            {
                WorldIntelObservation existing = recorded[index];
                if (!string.Equals(
                        existing.StableId,
                        observation.StableId,
                        StringComparison.Ordinal))
                    continue;
                return observation.ObservedRuleTimeSeconds <
                        existing.ObservedRuleTimeSeconds ||
                    observation.ObservedRuleTimeSeconds ==
                        existing.ObservedRuleTimeSeconds &&
                    observation.SourceRevision < existing.SourceRevision;
            }
            return false;
        }

        private void RollBackIntel(
            IReadOnlyList<WorldIntelObservation> beforeIntel,
            bool intelChanged)
        {
            if (!intelChanged) return;
            if (!intel.TryRestore(beforeIntel, out string rollbackError))
            {
                throw new InvalidOperationException(
                    "Exploration intel rollback failed: " + rollbackError);
            }
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }
}
