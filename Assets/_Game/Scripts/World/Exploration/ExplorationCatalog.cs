using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.World.Exploration
{
    public sealed class ExplorationScanZoneDefinition
    {
        internal ExplorationScanZoneDefinition(
            string stableId,
            string nodeStableIdPrefix,
            int revealRadius,
            string attentionReasonId)
        {
            StableId = RequireText(stableId, nameof(stableId));
            NodeStableIdPrefix = RequireText(
                nodeStableIdPrefix,
                nameof(nodeStableIdPrefix));
            if (revealRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(revealRadius));
            RevealRadius = revealRadius;
            AttentionReasonId = RequireText(
                attentionReasonId,
                nameof(attentionReasonId));
        }

        public string StableId { get; }
        public string NodeStableIdPrefix { get; }
        public int RevealRadius { get; }
        public string AttentionReasonId { get; }

        public bool ContainsNode(string stableNodeId)
        {
            return !string.IsNullOrWhiteSpace(stableNodeId) &&
                stableNodeId.StartsWith(
                    NodeStableIdPrefix,
                    StringComparison.Ordinal);
        }

        private static string RequireText(string value, string name)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException(
                    "Stable exploration text is required.",
                    name)
                : value;
        }
    }

    public static class FormalExplorationCatalog3D
    {
        public const float IntelStaleSeconds = 60f;
        public const float IntelExpiredSeconds = 180f;

        private static readonly ReadOnlyCollection<
            ExplorationScanZoneDefinition> scanZones =
                Array.AsReadOnly(new[]
                {
                    new ExplorationScanZoneDefinition(
                        "core.exploration.zone.safe-mining",
                        "world.deposit.safe-",
                        7,
                        "core.attention.scan.safe-mining-zone"),
                    new ExplorationScanZoneDefinition(
                        "core.exploration.zone.crystal-rift",
                        "world.deposit.rift-",
                        8,
                        "core.attention.scan.crystal-rift"),
                });

        public static IReadOnlyList<ExplorationScanZoneDefinition>
            ScanZones => scanZones;

        public static int ResolveSightRadius(WorldVisionSourceKind kind)
        {
            switch (kind)
            {
                case WorldVisionSourceKind.PrimaryCity: return 7;
                case WorldVisionSourceKind.SecondaryCity: return 5;
                case WorldVisionSourceKind.Leader: return 4;
                case WorldVisionSourceKind.Outpost: return 3;
                case WorldVisionSourceKind.ScoutDrone: return 6;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static ExplorationScanZoneDefinition FindScanZoneForNode(
            string stableNodeId)
        {
            for (var index = 0; index < scanZones.Count; index++)
            {
                if (scanZones[index].ContainsNode(stableNodeId))
                    return scanZones[index];
            }
            return null;
        }

        public static ExplorationScanZoneDefinition FindScanZone(
            string stableZoneId)
        {
            if (string.IsNullOrWhiteSpace(stableZoneId)) return null;
            for (var index = 0; index < scanZones.Count; index++)
            {
                if (string.Equals(
                        scanZones[index].StableId,
                        stableZoneId,
                        StringComparison.Ordinal))
                    return scanZones[index];
            }
            return null;
        }
    }
}
