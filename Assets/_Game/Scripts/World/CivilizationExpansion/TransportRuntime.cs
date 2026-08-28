using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.World.CivilizationExpansion
{
    public enum ConvoyStatus
    {
        InTransit,
        WaitingForCapacity,
        Delivered,
        Destroyed,
    }

    public interface IConvoyEscortStatusProvider
    {
        bool IsKnownSquad(string stableSquadId);
        bool IsNonDormant(string stableSquadId);
    }

    public interface IConvoyInterceptionImmunityProvider
    {
        bool TryConsumeConvoyInterceptionImmunity();
    }

    public sealed class ConvoySnapshot
    {
        public ConvoySnapshot(
            string stableId,
            string sessionId,
            string sourceSettlementId,
            string destinationSettlementId,
            IReadOnlyList<ResourceAmount> cargo,
            IReadOnlyList<WorldGridPoint> path,
            int completedPathCells,
            float segmentProgressSeconds,
            string escortSquadId,
            bool riskResolved,
            int appliedRiskPercent,
            ConvoyStatus status)
        {
            StableId = stableId;
            SessionId = sessionId;
            SourceSettlementId = sourceSettlementId;
            DestinationSettlementId = destinationSettlementId;
            Cargo = Array.AsReadOnly(Copy(cargo));
            Path = Array.AsReadOnly(Copy(path));
            CompletedPathCells = completedPathCells;
            SegmentProgressSeconds = segmentProgressSeconds;
            EscortSquadId = escortSquadId ?? string.Empty;
            RiskResolved = riskResolved;
            AppliedRiskPercent = appliedRiskPercent;
            Status = status;
        }

        public string StableId { get; }
        public string SessionId { get; }
        public string SourceSettlementId { get; }
        public string DestinationSettlementId { get; }
        public IReadOnlyList<ResourceAmount> Cargo { get; }
        public IReadOnlyList<WorldGridPoint> Path { get; }
        public int CompletedPathCells { get; }
        public float SegmentProgressSeconds { get; }
        public string EscortSquadId { get; }
        public bool RiskResolved { get; }
        public int AppliedRiskPercent { get; }
        public ConvoyStatus Status { get; }
        public int CargoTotal => Cargo.Sum(value => value.Amount);

        public int CargoAmount(string resourceId)
        {
            for (var index = 0; index < Cargo.Count; index++)
            {
                if (string.Equals(
                        Cargo[index].ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    return Cargo[index].Amount;
            }
            return 0;
        }

        private static ResourceAmount[] Copy(
            IReadOnlyList<ResourceAmount> source)
        {
            var result = new ResourceAmount[source?.Count ?? 0];
            if (source != null)
            {
                for (var index = 0; index < source.Count; index++)
                    result[index] = source[index];
            }
            return result;
        }

        private static WorldGridPoint[] Copy(
            IReadOnlyList<WorldGridPoint> source)
        {
            var result = new WorldGridPoint[source?.Count ?? 0];
            if (source != null)
            {
                for (var index = 0; index < source.Count; index++)
                    result[index] = source[index];
            }
            return result;
        }
    }

    public sealed class TransportRuntimeSnapshot
    {
        public TransportRuntimeSnapshot(
            int nextConvoyOrdinal,
            IReadOnlyList<ConvoySnapshot> convoys)
            : this(0ul, nextConvoyOrdinal, convoys)
        {
        }

        public TransportRuntimeSnapshot(
            ulong revision,
            int nextConvoyOrdinal,
            IReadOnlyList<ConvoySnapshot> convoys)
        {
            Revision = revision;
            NextConvoyOrdinal = nextConvoyOrdinal;
            var copy = new ConvoySnapshot[convoys?.Count ?? 0];
            if (convoys != null)
            {
                for (var index = 0; index < convoys.Count; index++)
                    copy[index] = convoys[index];
            }
            Convoys = Array.AsReadOnly(copy);
        }

        public ulong Revision { get; }
        public int NextConvoyOrdinal { get; }
        public IReadOnlyList<ConvoySnapshot> Convoys { get; }
    }

    public sealed class TransportRuntime
    {
        private const float TimeEpsilon = .00001f;

        private sealed class ConvoyState
        {
            public string StableId;
            public string SessionId;
            public string SourceSettlementId;
            public string DestinationSettlementId;
            public ResourceAmount[] Cargo;
            public WorldGridPoint[] Path;
            public int CompletedPathCells;
            public float SegmentProgressSeconds;
            public string EscortSquadId;
            public bool RiskResolved;
            public int AppliedRiskPercent;
            public ConvoyStatus Status;

            public ConvoySnapshot Capture()
            {
                return new ConvoySnapshot(
                    StableId,
                    SessionId,
                    SourceSettlementId,
                    DestinationSettlementId,
                    Cargo,
                    Path,
                    CompletedPathCells,
                    SegmentProgressSeconds,
                    EscortSquadId,
                    RiskResolved,
                    AppliedRiskPercent,
                    Status);
            }
        }

        private readonly WorldMapModel map;
        private readonly WorldLayerRuntime worldLayer;
        private IConvoyEscortStatusProvider escortStatus;
        private SortedDictionary<string, ConvoyState> convoys =
            new SortedDictionary<string, ConvoyState>(StringComparer.Ordinal);
        private int nextConvoyOrdinal = 1;

        public TransportRuntime(
            WorldMapModel map,
            WorldLayerRuntime worldLayer,
            IConvoyEscortStatusProvider escortStatus = null)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.worldLayer = worldLayer ??
                throw new ArgumentNullException(nameof(worldLayer));
            this.escortStatus = escortStatus;
        }

        public int ConvoyCount => convoys.Count;
        public int NextConvoyOrdinal => nextConvoyOrdinal;
        public ulong Revision { get; private set; }

        public ConvoySnapshot GetConvoy(string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId) &&
                   convoys.TryGetValue(stableId, out ConvoyState state)
                ? state.Capture()
                : null;
        }

        public bool IsEscortCommittedToUnfinishedConvoy(string squadId)
        {
            return !string.IsNullOrWhiteSpace(squadId) &&
                IsEscortAssignedToUnfinishedConvoy(squadId);
        }

        public bool TryDispatch(
            string sessionId,
            string sourceSettlementId,
            string destinationSettlementId,
            IReadOnlyList<ResourceAmount> cargo,
            string escortSquadId,
            out string convoyId,
            out string error)
        {
            convoyId = string.Empty;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                error = "Convoy session ID is required.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(escortSquadId) &&
                (!IsStableId(escortSquadId) ||
                 escortStatus?.IsKnownSquad(escortSquadId) != true))
            {
                error = "Convoy escort squad ID is invalid.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(escortSquadId) &&
                (escortStatus?.IsNonDormant(escortSquadId) != true ||
                 IsEscortAssignedToUnfinishedConvoy(escortSquadId)))
            {
                error = "护送小队正在出征、休眠或已被运输队占用";
                return false;
            }
            if (!worldLayer.TryGetInventoryEndpoint(
                    sourceSettlementId,
                    out ISettlementInventoryEndpoint source) ||
                !worldLayer.TryGetInventoryEndpoint(
                    destinationSettlementId,
                    out _) ||
                string.Equals(
                    sourceSettlementId,
                    destinationSettlementId,
                    StringComparison.Ordinal))
            {
                error = "Convoy endpoints are invalid.";
                return false;
            }
            if (!worldLayer.TryFindPath(
                    sourceSettlementId,
                    destinationSettlementId,
                    out WorldGridPoint[] path) || path.Length == 0)
            {
                error = "Convoy route is unavailable.";
                return false;
            }
            if (!SettlementInventory.TryAggregate(
                    cargo,
                    out SortedDictionary<string, int> aggregate,
                    out int total) || total <= 0)
            {
                error = "Convoy cargo is invalid.";
                return false;
            }
            ResourceAmount[] normalizedCargo = ToAmounts(aggregate);
            if (!source.TryExtract(normalizedCargo))
            {
                error = "Convoy source inventory cannot atomically load cargo.";
                return false;
            }

            convoyId = "core.convoy." + nextConvoyOrdinal.ToString(
                "D6",
                CultureInfo.InvariantCulture);
            var state = new ConvoyState
            {
                StableId = convoyId,
                SessionId = sessionId,
                SourceSettlementId = sourceSettlementId,
                DestinationSettlementId = destinationSettlementId,
                Cargo = normalizedCargo,
                Path = (WorldGridPoint[])path.Clone(),
                EscortSquadId = escortSquadId ?? string.Empty,
                Status = ConvoyStatus.InTransit,
            };
            convoys.Add(convoyId, state);
            nextConvoyOrdinal++;
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds)) return;
            foreach (KeyValuePair<string, ConvoyState> item in convoys)
            {
                ConvoyState state = item.Value;
                if (state.Status == ConvoyStatus.WaitingForCapacity)
                {
                    TryUnload(state);
                    continue;
                }
                if (state.Status != ConvoyStatus.InTransit ||
                    deltaSeconds <= 0f)
                    continue;
                if (!state.RiskResolved && !ResolveRisk(state))
                    continue;

                float remaining = deltaSeconds;
                while (remaining > TimeEpsilon &&
                       state.CompletedPathCells < state.Path.Length)
                {
                    float needed = WorldLayerCatalog.ConvoySecondsPerCell -
                                   state.SegmentProgressSeconds;
                    if (remaining + TimeEpsilon < needed)
                    {
                        state.SegmentProgressSeconds += remaining;
                        remaining = 0f;
                        AdvanceRevision();
                        break;
                    }
                    remaining -= Math.Max(0f, needed);
                    state.CompletedPathCells++;
                    state.SegmentProgressSeconds = 0f;
                    AdvanceRevision();
                }
                if (state.CompletedPathCells >= state.Path.Length)
                    TryUnload(state);
            }
        }

        public TransportRuntimeSnapshot Capture()
        {
            var snapshots = new ConvoySnapshot[convoys.Count];
            var index = 0;
            foreach (KeyValuePair<string, ConvoyState> item in convoys)
                snapshots[index++] = item.Value.Capture();
            return new TransportRuntimeSnapshot(
                Revision,
                nextConvoyOrdinal,
                snapshots);
        }

        public bool TryRestore(
            TransportRuntimeSnapshot snapshot,
            out string error)
        {
            return TryRestore(snapshot, escortStatus, out error);
        }

        public bool TryRestore(
            TransportRuntimeSnapshot snapshot,
            IConvoyEscortStatusProvider restoredEscortStatus,
            out string error)
        {
            if (snapshot?.Convoys == null || snapshot.NextConvoyOrdinal < 1)
            {
                error = "Transport snapshot header is invalid.";
                return false;
            }
            var candidate = new SortedDictionary<string, ConvoyState>(
                StringComparer.Ordinal);
            var assignedEscorts = new HashSet<string>(StringComparer.Ordinal);
            var maximumOrdinal = 0;
            for (var index = 0; index < snapshot.Convoys.Count; index++)
            {
                ConvoySnapshot saved = snapshot.Convoys[index];
                if (!TryBuildRestoredState(
                        saved,
                        restoredEscortStatus,
                        out ConvoyState restored,
                        out int ordinal,
                        out error) ||
                    !candidate.TryAdd(restored.StableId, restored) ||
                    IsUnfinished(restored.Status) &&
                    !string.IsNullOrWhiteSpace(restored.EscortSquadId) &&
                    !assignedEscorts.Add(restored.EscortSquadId))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "Transport snapshot contains duplicate convoys.";
                    return false;
                }
                maximumOrdinal = Math.Max(maximumOrdinal, ordinal);
            }
            if (snapshot.NextConvoyOrdinal <= maximumOrdinal)
            {
                error = "Transport next convoy ordinal is stale.";
                return false;
            }
            convoys = candidate;
            nextConvoyOrdinal = snapshot.NextConvoyOrdinal;
            escortStatus = restoredEscortStatus;
            Revision = snapshot.Revision;
            error = string.Empty;
            return true;
        }

        public static int DeterministicRiskRoll(
            string sessionId,
            string convoyId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                Hash(ref hash, sessionId ?? string.Empty);
                hash ^= (byte)'|';
                hash *= 16777619u;
                Hash(ref hash, convoyId ?? string.Empty);
                return (int)(hash % 100u);
            }
        }

        private bool ResolveRisk(ConvoyState state)
        {
            bool escorted = !string.IsNullOrWhiteSpace(state.EscortSquadId) &&
                escortStatus?.IsNonDormant(state.EscortSquadId) == true;
            state.AppliedRiskPercent = escorted
                ? WorldLayerCatalog.EscortedInterceptionPercent
                : WorldLayerCatalog.UnescortedInterceptionPercent;
            state.RiskResolved = true;
            int roll = DeterministicRiskRoll(
                state.SessionId,
                state.StableId);
            if (roll >= state.AppliedRiskPercent)
            {
                AdvanceRevision();
                return true;
            }
            if (escortStatus is IConvoyInterceptionImmunityProvider immunity &&
                immunity.TryConsumeConvoyInterceptionImmunity())
            {
                state.AppliedRiskPercent = 0;
                AdvanceRevision();
                return true;
            }
            state.Cargo = Array.Empty<ResourceAmount>();
            state.Status = ConvoyStatus.Destroyed;
            state.SegmentProgressSeconds = 0f;
            AdvanceRevision();
            return false;
        }

        private void TryUnload(ConvoyState state)
        {
            if (!worldLayer.TryGetInventoryEndpoint(
                    state.DestinationSettlementId,
                    out ISettlementInventoryEndpoint destination) ||
                !destination.TryAccept(state.Cargo))
            {
                if (state.Status != ConvoyStatus.WaitingForCapacity)
                {
                    state.Status = ConvoyStatus.WaitingForCapacity;
                    AdvanceRevision();
                }
                return;
            }
            state.Cargo = Array.Empty<ResourceAmount>();
            state.Status = ConvoyStatus.Delivered;
            state.SegmentProgressSeconds = 0f;
            AdvanceRevision();
        }

        private bool TryBuildRestoredState(
            ConvoySnapshot saved,
            IConvoyEscortStatusProvider restoredEscortStatus,
            out ConvoyState state,
            out int ordinal,
            out string error)
        {
            state = null;
            ordinal = 0;
            if (saved == null ||
                !TryParseConvoyOrdinal(saved.StableId, out ordinal) ||
                string.IsNullOrWhiteSpace(saved.SessionId) ||
                !worldLayer.TryGetInventoryEndpoint(
                    saved.SourceSettlementId,
                    out _) ||
                !worldLayer.TryGetInventoryEndpoint(
                    saved.DestinationSettlementId,
                    out _) ||
                string.Equals(
                    saved.SourceSettlementId,
                    saved.DestinationSettlementId,
                    StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(saved.EscortSquadId) &&
                (!IsStableId(saved.EscortSquadId) ||
                 restoredEscortStatus?.IsKnownSquad(
                     saved.EscortSquadId) != true) ||
                !Enum.IsDefined(typeof(ConvoyStatus), saved.Status) ||
                saved.AppliedRiskPercent < 0 ||
                saved.AppliedRiskPercent > 100 ||
                saved.CompletedPathCells < 0 ||
                saved.CompletedPathCells > saved.Path.Count ||
                saved.SegmentProgressSeconds < 0f ||
                saved.SegmentProgressSeconds >=
                    WorldLayerCatalog.ConvoySecondsPerCell ||
                float.IsNaN(saved.SegmentProgressSeconds) ||
                float.IsInfinity(saved.SegmentProgressSeconds) ||
                !ValidatePath(
                    saved.SourceSettlementId,
                    saved.DestinationSettlementId,
                    saved.Path))
            {
                error = "Convoy snapshot fields are invalid.";
                return false;
            }
            if (!SettlementInventory.TryAggregate(
                    saved.Cargo,
                    out SortedDictionary<string, int> cargo,
                    out int cargoTotal,
                    rejectDuplicate: true))
            {
                error = "Convoy snapshot cargo is invalid.";
                return false;
            }
            bool terminal = saved.Status == ConvoyStatus.Delivered ||
                            saved.Status == ConvoyStatus.Destroyed;
            if (terminal != (cargoTotal == 0) ||
                saved.RiskResolved &&
                saved.AppliedRiskPercent != 0 &&
                saved.AppliedRiskPercent !=
                    WorldLayerCatalog.UnescortedInterceptionPercent &&
                saved.AppliedRiskPercent !=
                    WorldLayerCatalog.EscortedInterceptionPercent ||
                !saved.RiskResolved && saved.CompletedPathCells != 0 ||
                saved.Status == ConvoyStatus.Destroyed &&
                !saved.RiskResolved ||
                saved.Status == ConvoyStatus.Delivered &&
                saved.CompletedPathCells != saved.Path.Count ||
                saved.Status == ConvoyStatus.WaitingForCapacity &&
                saved.CompletedPathCells != saved.Path.Count ||
                saved.Status == ConvoyStatus.InTransit &&
                saved.CompletedPathCells >= saved.Path.Count ||
                saved.Status != ConvoyStatus.InTransit &&
                saved.SegmentProgressSeconds != 0f ||
                !saved.RiskResolved && saved.AppliedRiskPercent != 0)
            {
                error = "Convoy snapshot status and cargo disagree.";
                return false;
            }
            state = new ConvoyState
            {
                StableId = saved.StableId,
                SessionId = saved.SessionId,
                SourceSettlementId = saved.SourceSettlementId,
                DestinationSettlementId = saved.DestinationSettlementId,
                Cargo = ToAmounts(cargo),
                Path = saved.Path.ToArray(),
                CompletedPathCells = saved.CompletedPathCells,
                SegmentProgressSeconds = saved.SegmentProgressSeconds,
                EscortSquadId = saved.EscortSquadId,
                RiskResolved = saved.RiskResolved,
                AppliedRiskPercent = saved.AppliedRiskPercent,
                Status = saved.Status,
            };
            error = string.Empty;
            return true;
        }

        private bool IsEscortAssignedToUnfinishedConvoy(string squadId)
        {
            foreach (KeyValuePair<string, ConvoyState> item in convoys)
            {
                if (IsUnfinished(item.Value.Status) &&
                    string.Equals(
                        item.Value.EscortSquadId,
                        squadId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsUnfinished(ConvoyStatus status)
        {
            return status == ConvoyStatus.InTransit ||
                   status == ConvoyStatus.WaitingForCapacity;
        }

        private bool ValidatePath(
            string sourceSettlementId,
            string destinationSettlementId,
            IReadOnlyList<WorldGridPoint> path)
        {
            SettlementRuntime source = worldLayer.GetSettlement(
                sourceSettlementId);
            SettlementRuntime destination = worldLayer.GetSettlement(
                destinationSettlementId);
            if (source == null || destination == null || path == null ||
                path.Count == 0)
                return false;
            int previousX = source.X;
            int previousY = source.Y;
            for (var index = 0; index < path.Count; index++)
            {
                WorldGridPoint point = path[index];
                if (point.X < 0 || point.Y < 0 ||
                    point.X >= map.Width || point.Y >= map.Height ||
                    Math.Abs(point.X - previousX) +
                    Math.Abs(point.Y - previousY) != 1 ||
                    !CityTerrainRules.IsPassable(map.Get(point.X, point.Y)))
                    return false;
                previousX = point.X;
                previousY = point.Y;
            }
            WorldGridPoint last = path[path.Count - 1];
            return last.X == destination.X && last.Y == destination.Y;
        }

        private static ResourceAmount[] ToAmounts(
            SortedDictionary<string, int> aggregate)
        {
            var result = new ResourceAmount[aggregate.Count];
            var index = 0;
            foreach (KeyValuePair<string, int> item in aggregate)
                result[index++] = new ResourceAmount(item.Key, item.Value);
            return result;
        }

        private static bool TryParseConvoyOrdinal(
            string stableId,
            out int ordinal)
        {
            ordinal = 0;
            const string prefix = "core.convoy.";
            return stableId != null &&
                   stableId.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(
                       stableId.Substring(prefix.Length),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out ordinal) && ordinal > 0;
        }

        private static bool IsStableId(string value)
        {
            try
            {
                _ = new StableId(value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void Hash(ref uint hash, string value)
        {
            unchecked
            {
                for (var index = 0; index < value.Length; index++)
                {
                    char character = value[index];
                    hash ^= (byte)character;
                    hash *= 16777619u;
                    hash ^= (byte)(character >> 8);
                    hash *= 16777619u;
                }
            }
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }
}
