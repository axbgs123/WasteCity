using System;
using System.Collections.Generic;

namespace WasteCity.World.Exploration
{
    public delegate bool TryCommitExplorationAttention(
        string attentionReasonId,
        string stableEventKey);

    public enum WorldScanStatus
    {
        None = 0,
        Committed = 1,
        InvalidRequest = 2,
        UnknownNode = 3,
        NotVisible = 4,
        AlreadyScanned = 5,
        AttentionRejected = 6,
    }

    public readonly struct WorldScanResult
    {
        internal WorldScanResult(
            WorldScanStatus status,
            string zoneId,
            string attentionReasonId,
            string stableEventKey,
            int revealedCellCount)
        {
            Status = status;
            ZoneId = zoneId ?? string.Empty;
            AttentionReasonId = attentionReasonId ?? string.Empty;
            StableEventKey = stableEventKey ?? string.Empty;
            RevealedCellCount = revealedCellCount;
        }

        public WorldScanStatus Status { get; }
        public string ZoneId { get; }
        public string AttentionReasonId { get; }
        public string StableEventKey { get; }
        public int RevealedCellCount { get; }
    }

    public sealed class WorldScanRuntime
    {
        private const string EventKeyPrefix = "exploration.scan:";

        private readonly WorldVisibilityRuntime visibility;
        private readonly TryCommitExplorationAttention attentionCommitter;
        private readonly HashSet<string> scannedZoneIds =
            new HashSet<string>(StringComparer.Ordinal);

        public WorldScanRuntime(
            WorldVisibilityRuntime visibility,
            TryCommitExplorationAttention attentionCommitter)
        {
            this.visibility = visibility ??
                throw new ArgumentNullException(nameof(visibility));
            this.attentionCommitter = attentionCommitter ??
                throw new ArgumentNullException(nameof(attentionCommitter));
        }

        public int ScannedZoneCount => scannedZoneIds.Count;
        public ulong Revision { get; private set; }

        public bool IsScanned(string stableZoneId)
        {
            return !string.IsNullOrWhiteSpace(stableZoneId) &&
                scannedZoneIds.Contains(stableZoneId);
        }

        public bool TryScanVisibleNode(
            string sessionId,
            string stableNodeId,
            int nodeX,
            int nodeY,
            out WorldScanResult result,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(sessionId) ||
                string.IsNullOrWhiteSpace(stableNodeId) ||
                nodeX < 0 || nodeY < 0 ||
                nodeX >= visibility.Width || nodeY >= visibility.Height)
            {
                result = Result(WorldScanStatus.InvalidRequest, null);
                error = "Scan request is invalid.";
                return false;
            }
            ExplorationScanZoneDefinition zone =
                FormalExplorationCatalog3D.FindScanZoneForNode(stableNodeId);
            if (zone == null)
            {
                result = Result(WorldScanStatus.UnknownNode, null);
                error = "Resource node is not part of a formal scan zone.";
                return false;
            }
            if (scannedZoneIds.Contains(zone.StableId))
            {
                result = Result(WorldScanStatus.AlreadyScanned, zone);
                error = "Scan zone is already complete.";
                return false;
            }
            if (!visibility.IsVisible(nodeX, nodeY))
            {
                result = Result(WorldScanStatus.NotVisible, zone);
                error = "Resource node is not in current vision.";
                return false;
            }

            string stableEventKey = EventKeyPrefix + sessionId + ":" +
                zone.StableId;
            if (!attentionCommitter(
                    zone.AttentionReasonId,
                    stableEventKey))
            {
                result = new WorldScanResult(
                    WorldScanStatus.AttentionRejected,
                    zone.StableId,
                    zone.AttentionReasonId,
                    stableEventKey,
                    0);
                error = "Attention owner rejected the scan event.";
                return false;
            }

            int revealed = visibility.Reveal(
                nodeX,
                nodeY,
                zone.RevealRadius);
            scannedZoneIds.Add(zone.StableId);
            AdvanceRevision();
            result = new WorldScanResult(
                WorldScanStatus.Committed,
                zone.StableId,
                zone.AttentionReasonId,
                stableEventKey,
                revealed);
            error = string.Empty;
            return true;
        }

        public string[] CaptureScannedZoneIds()
        {
            var result = new string[scannedZoneIds.Count];
            scannedZoneIds.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        public bool TryRestoreScannedZoneIds(
            IReadOnlyList<string> stableZoneIds,
            out string error)
        {
            if (stableZoneIds == null)
            {
                error = "Scanned zone IDs are required.";
                return false;
            }
            var candidate = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < stableZoneIds.Count; index++)
            {
                string id = stableZoneIds[index];
                if (FormalExplorationCatalog3D.FindScanZone(id) == null ||
                    !candidate.Add(id))
                {
                    error = "Scanned zone IDs must be known and unique.";
                    return false;
                }
            }
            if (candidate.SetEquals(scannedZoneIds))
            {
                error = string.Empty;
                return true;
            }
            scannedZoneIds.Clear();
            foreach (string id in candidate)
                scannedZoneIds.Add(id);
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        private static WorldScanResult Result(
            WorldScanStatus status,
            ExplorationScanZoneDefinition zone)
        {
            return new WorldScanResult(
                status,
                zone?.StableId,
                zone?.AttentionReasonId,
                string.Empty,
                0);
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }
}
