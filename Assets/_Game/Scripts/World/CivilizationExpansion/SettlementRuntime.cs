using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.World.CivilizationExpansion
{
    public interface ISettlementConstructionAccount
    {
        int Population { get; }
        int GetAmount(string resourceId);
        bool TryCommit(
            IReadOnlyList<ResourceAmount> costs,
            int populationCost);
    }

    public interface ISettlementInventoryEndpoint
    {
        string StableSettlementId { get; }
        int GetAmount(string resourceId);
        int AcceptableSpace { get; }
        bool TryExtract(IReadOnlyList<ResourceAmount> amounts);
        bool TryAccept(IReadOnlyList<ResourceAmount> amounts);
    }

    public sealed class SettlementInventory
    {
        private readonly SortedDictionary<string, int> amounts =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        public SettlementInventory(int capacity)
        {
            Capacity = Math.Max(0, capacity);
        }

        public int Capacity { get; }
        public int TotalAmount { get; private set; }
        public int FreeSpace => Math.Max(0, Capacity - TotalAmount);
        public ulong Revision { get; private set; }

        public int Get(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                   amounts.TryGetValue(resourceId, out int value)
                ? value
                : 0;
        }

        public int Add(string resourceId, int requestedAmount)
        {
            if (!IsKnownResource(resourceId) || requestedAmount <= 0)
                return 0;
            int accepted = Math.Min(requestedAmount, FreeSpace);
            if (accepted <= 0) return 0;
            amounts[resourceId] = Get(resourceId) + accepted;
            TotalAmount += accepted;
            AdvanceRevision();
            return accepted;
        }

        public bool TryExtract(IReadOnlyList<ResourceAmount> requested)
        {
            if (!TryAggregate(
                    requested,
                    out SortedDictionary<string, int> aggregate,
                    out int total) ||
                total <= 0)
                return false;
            foreach (KeyValuePair<string, int> item in aggregate)
            {
                if (Get(item.Key) < item.Value) return false;
            }
            foreach (KeyValuePair<string, int> item in aggregate)
            {
                int next = Get(item.Key) - item.Value;
                if (next == 0) amounts.Remove(item.Key);
                else amounts[item.Key] = next;
            }
            TotalAmount -= total;
            AdvanceRevision();
            return true;
        }

        public bool TryAccept(IReadOnlyList<ResourceAmount> incoming)
        {
            if (!TryAggregate(
                    incoming,
                    out SortedDictionary<string, int> aggregate,
                    out int total) ||
                total <= 0 || total > FreeSpace)
                return false;
            foreach (KeyValuePair<string, int> item in aggregate)
                amounts[item.Key] = Get(item.Key) + item.Value;
            TotalAmount += total;
            AdvanceRevision();
            return true;
        }

        public ResourceAmount[] CaptureAmounts()
        {
            var result = new ResourceAmount[amounts.Count];
            var index = 0;
            foreach (KeyValuePair<string, int> item in amounts)
                result[index++] = new ResourceAmount(item.Key, item.Value);
            return result;
        }

        public bool TryRestore(
            IReadOnlyList<ResourceAmount> restored,
            out string error)
        {
            if (!TryAggregate(
                    restored,
                    out SortedDictionary<string, int> aggregate,
                    out int total,
                    rejectDuplicate: true))
            {
                error = "Settlement inventory snapshot is invalid.";
                return false;
            }
            if (total > Capacity)
            {
                error = "Settlement inventory exceeds capacity.";
                return false;
            }
            amounts.Clear();
            foreach (KeyValuePair<string, int> item in aggregate)
                amounts.Add(item.Key, item.Value);
            TotalAmount = total;
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        internal static bool TryAggregate(
            IReadOnlyList<ResourceAmount> source,
            out SortedDictionary<string, int> aggregate,
            out int total,
            bool rejectDuplicate = false)
        {
            aggregate = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            total = 0;
            if (source == null) return false;
            long totalLong = 0;
            for (var index = 0; index < source.Count; index++)
            {
                ResourceAmount value = source[index];
                if (!IsKnownResource(value.ResourceId) || value.Amount <= 0)
                    return false;
                if (aggregate.TryGetValue(value.ResourceId, out int before))
                {
                    if (rejectDuplicate) return false;
                    long combined = (long)before + value.Amount;
                    if (combined > int.MaxValue) return false;
                    aggregate[value.ResourceId] = (int)combined;
                }
                else
                {
                    aggregate.Add(value.ResourceId, value.Amount);
                }
                totalLong += value.Amount;
                if (totalLong > int.MaxValue) return false;
            }
            total = (int)totalLong;
            return true;
        }

        private static bool IsKnownResource(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return false;
            for (var index = 0; index < ResourceIds.All.Length; index++)
            {
                if (string.Equals(
                        ResourceIds.All[index],
                        resourceId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }

    public sealed class SettlementRuntimeSnapshot
    {
        public SettlementRuntimeSnapshot(
            string stableId,
            SettlementKind kind,
            int x,
            int y,
            SettlementAutonomyTemplate autonomyTemplate,
            int population,
            int populationCapacity,
            int loyalty,
            bool communication,
            bool supplied,
            bool maintained,
            float autonomyProgressSeconds,
            IReadOnlyList<ResourceAmount> inventoryAmounts,
            ulong revision = 0ul)
        {
            StableId = stableId;
            Kind = kind;
            X = x;
            Y = y;
            AutonomyTemplate = autonomyTemplate;
            Population = population;
            PopulationCapacity = populationCapacity;
            Loyalty = loyalty;
            IsCommunicationActive = communication;
            IsSupplied = supplied;
            IsMaintained = maintained;
            AutonomyProgressSeconds = autonomyProgressSeconds;
            InventoryAmounts = Array.AsReadOnly(
                inventoryAmounts == null
                    ? Array.Empty<ResourceAmount>()
                    : Copy(inventoryAmounts));
            Revision = revision;
        }

        public string StableId { get; }
        public SettlementKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public SettlementAutonomyTemplate AutonomyTemplate { get; }
        public int Population { get; }
        public int PopulationCapacity { get; }
        public int Loyalty { get; }
        public bool IsCommunicationActive { get; }
        public bool IsSupplied { get; }
        public bool IsMaintained { get; }
        public float AutonomyProgressSeconds { get; }
        public IReadOnlyList<ResourceAmount> InventoryAmounts { get; }
        public ulong Revision { get; }

        private static ResourceAmount[] Copy(
            IReadOnlyList<ResourceAmount> source)
        {
            var result = new ResourceAmount[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }
    }

    public sealed class SettlementRuntime : ISettlementInventoryEndpoint
    {
        private float autonomyProgressSeconds;

        private SettlementRuntime(
            SettlementDefinition definition,
            int x,
            int y,
            SettlementAutonomyTemplate autonomyTemplate,
            bool externalReference)
        {
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            StableId = definition.Id;
            Kind = definition.Kind;
            X = x;
            Y = y;
            AutonomyTemplate = autonomyTemplate;
            IsExternalReference = externalReference;
            Population = definition.InitialPopulation;
            PopulationCapacity = definition.PopulationCapacity;
            Loyalty = WorldLayerCatalog.InitialSettlementLoyalty;
            IsCommunicationActive = true;
            IsSupplied = true;
            IsMaintained = true;
            if (!externalReference)
                Inventory = new SettlementInventory(
                    definition.InventoryCapacity);
        }

        public SettlementDefinition Definition { get; }
        public string StableId { get; }
        string ISettlementInventoryEndpoint.StableSettlementId => StableId;
        public SettlementKind Kind { get; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public SettlementAutonomyTemplate AutonomyTemplate { get; }
        public bool IsExternalReference { get; }
        public SettlementInventory Inventory { get; }
        public int Population { get; private set; }
        public int PopulationCapacity { get; private set; }
        public int Loyalty { get; private set; }
        public bool IsCommunicationActive { get; private set; }
        public bool IsSupplied { get; private set; }
        public bool IsMaintained { get; private set; }
        public bool CanIssueRemoteCommands => IsCommunicationActive;
        public float AutonomyProgressSeconds => autonomyProgressSeconds;
        public float ResearchContributionMultiplier =>
            Kind == SettlementKind.SecondaryCity &&
            AutonomyTemplate == SettlementAutonomyTemplate.Research &&
            IsCommunicationActive
                ? WorldLayerCatalog.ResearchContributionMultiplier
                : 1f;
        public ulong Revision { get; private set; }

        int ISettlementInventoryEndpoint.GetAmount(string resourceId) =>
            Inventory?.Get(resourceId) ?? 0;
        int ISettlementInventoryEndpoint.AcceptableSpace =>
            Inventory?.FreeSpace ?? 0;
        bool ISettlementInventoryEndpoint.TryExtract(
            IReadOnlyList<ResourceAmount> amounts) =>
            Inventory != null && Inventory.TryExtract(amounts);
        bool ISettlementInventoryEndpoint.TryAccept(
            IReadOnlyList<ResourceAmount> amounts) =>
            Inventory != null && Inventory.TryAccept(amounts);

        public static SettlementRuntime CreatePrimaryReference(int x, int y)
        {
            return new SettlementRuntime(
                WorldLayerCatalog.PrimaryCity,
                x,
                y,
                SettlementAutonomyTemplate.PrimaryReference,
                externalReference: true);
        }

        public static SettlementRuntime CreateSecondary(
            int x,
            int y,
            SettlementAutonomyTemplate autonomyTemplate)
        {
            if (autonomyTemplate != SettlementAutonomyTemplate.Industrial &&
                autonomyTemplate != SettlementAutonomyTemplate.Military &&
                autonomyTemplate != SettlementAutonomyTemplate.Research)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(autonomyTemplate));
            }
            return new SettlementRuntime(
                WorldLayerCatalog.SecondaryCity,
                x,
                y,
                autonomyTemplate,
                externalReference: false);
        }

        public static SettlementRuntime CreateOutpost(int x, int y)
        {
            return new SettlementRuntime(
                WorldLayerCatalog.Outpost,
                x,
                y,
                SettlementAutonomyTemplate.OutpostStone,
                externalReference: false);
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds) || Inventory == null)
                return;
            if (Kind == SettlementKind.Outpost &&
                (!IsCommunicationActive || !IsSupplied || !IsMaintained))
                return;

            string outputId;
            float cycle;
            if (Kind == SettlementKind.Outpost)
            {
                outputId = ResourceIds.Stone;
                cycle = WorldLayerCatalog.OutpostCycleSeconds;
            }
            else if (AutonomyTemplate == SettlementAutonomyTemplate.Industrial)
            {
                outputId = ResourceIds.Alloy;
                cycle = WorldLayerCatalog.AutonomyCycleSeconds;
            }
            else if (AutonomyTemplate == SettlementAutonomyTemplate.Military)
            {
                outputId = ResourceIds.Ammunition;
                cycle = WorldLayerCatalog.AutonomyCycleSeconds;
            }
            else
            {
                return;
            }

            double elapsed = autonomyProgressSeconds + deltaSeconds;
            int cycles = elapsed >= int.MaxValue * (double)cycle
                ? int.MaxValue
                : (int)((elapsed + .000001d) / cycle);
            if (cycles <= 0)
            {
                autonomyProgressSeconds = (float)elapsed;
                return;
            }
            int produced = Inventory.Add(
                outputId,
                Math.Min(cycles, Inventory.FreeSpace));
            if (produced < cycles)
                autonomyProgressSeconds = cycle;
            else
                autonomyProgressSeconds = Math.Max(
                    0f,
                    (float)(elapsed - cycles * cycle));
            if (produced > 0) AdvanceRevision();
        }

        public void SetCommunication(bool active)
        {
            if (IsCommunicationActive == active) return;
            IsCommunicationActive = active;
            AdvanceRevision();
        }

        public void SetOperationalLinks(
            bool communication,
            bool supplied,
            bool maintained)
        {
            if (IsCommunicationActive == communication &&
                IsSupplied == supplied && IsMaintained == maintained)
                return;
            IsCommunicationActive = communication;
            IsSupplied = supplied;
            IsMaintained = maintained;
            AdvanceRevision();
        }

        public void SetLoyalty(int loyalty)
        {
            int next = Math.Max(0, Math.Min(100, loyalty));
            if (Loyalty == next) return;
            Loyalty = next;
            AdvanceRevision();
        }

        public SettlementRuntimeSnapshot Capture()
        {
            return new SettlementRuntimeSnapshot(
                StableId,
                Kind,
                X,
                Y,
                AutonomyTemplate,
                Population,
                PopulationCapacity,
                Loyalty,
                IsCommunicationActive,
                IsSupplied,
                IsMaintained,
                autonomyProgressSeconds,
                Inventory?.CaptureAmounts() ?? Array.Empty<ResourceAmount>(),
                Revision);
        }

        internal static bool TryFromSnapshot(
            SettlementRuntimeSnapshot snapshot,
            out SettlementRuntime runtime,
            out string error)
        {
            runtime = null;
            SettlementDefinition definition = WorldLayerCatalog.Find(
                snapshot?.StableId);
            if (snapshot == null || definition == null ||
                snapshot.Kind != definition.Kind ||
                snapshot.Population < 0 ||
                snapshot.PopulationCapacity < snapshot.Population ||
                snapshot.PopulationCapacity != definition.PopulationCapacity ||
                snapshot.Loyalty < 0 || snapshot.Loyalty > 100 ||
                snapshot.AutonomyProgressSeconds < 0f ||
                float.IsNaN(snapshot.AutonomyProgressSeconds) ||
                float.IsInfinity(snapshot.AutonomyProgressSeconds))
            {
                error = "Settlement snapshot fields are invalid.";
                return false;
            }

            try
            {
                runtime = definition.Kind == SettlementKind.PrimaryCity
                    ? CreatePrimaryReference(snapshot.X, snapshot.Y)
                    : definition.Kind == SettlementKind.SecondaryCity
                        ? CreateSecondary(
                            snapshot.X,
                            snapshot.Y,
                            snapshot.AutonomyTemplate)
                        : CreateOutpost(snapshot.X, snapshot.Y);
            }
            catch (ArgumentOutOfRangeException)
            {
                error = "Settlement autonomy template is invalid.";
                return false;
            }
            if (runtime.AutonomyTemplate != snapshot.AutonomyTemplate ||
                runtime.Population != snapshot.Population)
            {
                runtime = null;
                error = "Settlement snapshot does not match its catalog.";
                return false;
            }
            if (runtime.Inventory != null &&
                !runtime.Inventory.TryRestore(
                    snapshot.InventoryAmounts,
                    out error))
            {
                runtime = null;
                return false;
            }
            if (runtime.Inventory == null && snapshot.InventoryAmounts.Count > 0)
            {
                runtime = null;
                error = "Primary city reference cannot own copied inventory.";
                return false;
            }
            runtime.Loyalty = snapshot.Loyalty;
            runtime.IsCommunicationActive = snapshot.IsCommunicationActive;
            runtime.IsSupplied = snapshot.IsSupplied;
            runtime.IsMaintained = snapshot.IsMaintained;
            runtime.autonomyProgressSeconds = snapshot.AutonomyProgressSeconds;
            runtime.Revision = snapshot.Revision;
            error = string.Empty;
            return true;
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }

    public sealed class WorldLayerRuntimeSnapshot
    {
        public WorldLayerRuntimeSnapshot(
            string focusedSettlementId,
            string controlledCityId,
            IReadOnlyList<SettlementRuntimeSnapshot> settlements)
            : this(
                0ul,
                3,
                focusedSettlementId,
                controlledCityId,
                settlements)
        {
        }

        public WorldLayerRuntimeSnapshot(
            ulong revision,
            int nextSettlementOrdinal,
            string focusedSettlementId,
            string controlledCityId,
            IReadOnlyList<SettlementRuntimeSnapshot> settlements)
        {
            Revision = revision;
            NextSettlementOrdinal = nextSettlementOrdinal;
            FocusedSettlementId = focusedSettlementId;
            ControlledCityId = controlledCityId;
            var copy = new SettlementRuntimeSnapshot[
                settlements?.Count ?? 0];
            if (settlements != null)
            {
                for (var index = 0; index < settlements.Count; index++)
                    copy[index] = settlements[index];
            }
            Settlements = Array.AsReadOnly(copy);
        }

        public ulong Revision { get; }
        public int NextSettlementOrdinal { get; }
        public string FocusedSettlementId { get; }
        public string ControlledCityId { get; }
        public IReadOnlyList<SettlementRuntimeSnapshot> Settlements { get; }
    }

    public sealed class WorldLayerRuntime
    {
        private readonly WorldMapModel map;
        private readonly ISettlementInventoryEndpoint primaryInventory;
        private QuantumEntanglementInventoryNetwork quantumInventoryNetwork;
        private SortedDictionary<string, SettlementRuntime> settlements =
            new SortedDictionary<string, SettlementRuntime>(
                StringComparer.Ordinal);

        public WorldLayerRuntime(
            WorldMapModel map,
            int primaryX,
            int primaryY,
            ISettlementInventoryEndpoint primaryInventory)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.primaryInventory = primaryInventory ??
                throw new ArgumentNullException(nameof(primaryInventory));
            if (!string.Equals(
                    primaryInventory.StableSettlementId,
                    WorldLayerCatalog.PrimaryCity.Id,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Primary inventory must reference the formal primary city.",
                    nameof(primaryInventory));
            if (!IsPassableCell(primaryX, primaryY))
                throw new ArgumentOutOfRangeException(nameof(primaryX));
            SettlementRuntime primary =
                SettlementRuntime.CreatePrimaryReference(primaryX, primaryY);
            settlements.Add(primary.StableId, primary);
            FocusedSettlementId = primary.StableId;
            ControlledCityId = primary.StableId;
        }

        public SettlementRuntime PrimaryCity =>
            settlements[WorldLayerCatalog.PrimaryCity.Id];
        public string FocusedSettlementId { get; private set; }
        public string ControlledCityId { get; private set; }
        public int SettlementCount => settlements.Count;
        public ulong Revision { get; private set; }

        public void ConfigureQuantumEntanglement(
            QuantumEntanglementRuntime runtime)
        {
            quantumInventoryNetwork = runtime == null
                ? null
                : new QuantumEntanglementInventoryNetwork(
                    runtime,
                    ResolveLocalInventoryEndpoint,
                    IsSettlementCommunicationActive,
                    GetOrderedSettlementIds);
        }

        public SettlementRuntime GetSettlement(string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId) &&
                   settlements.TryGetValue(stableId, out SettlementRuntime value)
                ? value
                : null;
        }

        public bool TryGetInventoryEndpoint(
            string settlementId,
            out ISettlementInventoryEndpoint endpoint)
        {
            endpoint = null;
            SettlementRuntime settlement = GetSettlement(settlementId);
            if (settlement == null) return false;
            ISettlementInventoryEndpoint local =
                ResolveLocalInventoryEndpoint(settlementId);
            endpoint = quantumInventoryNetwork == null
                ? local
                : quantumInventoryNetwork.CreateEndpoint(settlementId, local);
            return true;
        }

        private ISettlementInventoryEndpoint ResolveLocalInventoryEndpoint(
            string settlementId)
        {
            SettlementRuntime settlement = GetSettlement(settlementId);
            if (settlement == null) return null;
            return settlement.Kind == SettlementKind.PrimaryCity
                ? primaryInventory
                : settlement;
        }

        private bool IsSettlementCommunicationActive(string settlementId)
        {
            return GetSettlement(settlementId)?.IsCommunicationActive == true;
        }

        private IReadOnlyList<string> GetOrderedSettlementIds()
        {
            var result = new string[settlements.Count];
            var index = 0;
            foreach (KeyValuePair<string, SettlementRuntime> item in settlements)
                result[index++] = item.Key;
            return result;
        }

        public bool TryEstablishSecondary(
            int x,
            int y,
            SettlementAutonomyTemplate autonomyTemplate,
            ISettlementConstructionAccount account,
            out SettlementRuntime settlement,
            out string error)
        {
            settlement = null;
            if (settlements.ContainsKey(WorldLayerCatalog.SecondaryCity.Id))
            {
                error = "次城已经建立";
                return false;
            }
            if (autonomyTemplate != SettlementAutonomyTemplate.Industrial &&
                autonomyTemplate != SettlementAutonomyTemplate.Military &&
                autonomyTemplate != SettlementAutonomyTemplate.Research)
            {
                error = "次城自治模板无效";
                return false;
            }
            if (!TryValidateEstablishmentLocation(x, y, out error) ||
                !TryPay(WorldLayerCatalog.SecondaryCity, account, out error))
                return false;
            settlement = SettlementRuntime.CreateSecondary(
                x, y, autonomyTemplate);
            settlements.Add(settlement.StableId, settlement);
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        public bool TryEstablishOutpost(
            int x,
            int y,
            ISettlementConstructionAccount account,
            out SettlementRuntime settlement,
            out string error)
        {
            settlement = null;
            if (settlements.ContainsKey(WorldLayerCatalog.Outpost.Id))
            {
                error = "前哨已经建立";
                return false;
            }
            if (!TryValidateEstablishmentLocation(x, y, out error) ||
                !TryPay(WorldLayerCatalog.Outpost, account, out error))
                return false;
            settlement = SettlementRuntime.CreateOutpost(x, y);
            settlements.Add(settlement.StableId, settlement);
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        public bool TryFocus(string settlementId)
        {
            if (!settlements.ContainsKey(settlementId)) return false;
            if (string.Equals(
                    FocusedSettlementId,
                    settlementId,
                    StringComparison.Ordinal)) return true;
            FocusedSettlementId = settlementId;
            AdvanceRevision();
            return true;
        }

        public bool TryControlCity(
            string settlementId,
            bool remoteCommandUnlocked,
            bool leaderPresent)
        {
            SettlementRuntime settlement = GetSettlement(settlementId);
            if (settlement == null ||
                settlement.Kind == SettlementKind.Outpost ||
                !settlement.IsCommunicationActive)
                return false;
            if (settlement.Kind != SettlementKind.PrimaryCity &&
                !remoteCommandUnlocked && !leaderPresent)
                return false;
            if (string.Equals(
                    ControlledCityId,
                    settlementId,
                    StringComparison.Ordinal)) return true;
            ControlledCityId = settlementId;
            AdvanceRevision();
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            foreach (KeyValuePair<string, SettlementRuntime> item in settlements)
                item.Value.Tick(deltaSeconds);
        }

        public WorldLayerRuntimeSnapshot Capture()
        {
            var snapshots = new SettlementRuntimeSnapshot[settlements.Count];
            var index = 0;
            foreach (KeyValuePair<string, SettlementRuntime> item in settlements)
                snapshots[index++] = item.Value.Capture();
            return new WorldLayerRuntimeSnapshot(
                Revision,
                3,
                FocusedSettlementId,
                ControlledCityId,
                snapshots);
        }

        public bool TryRestore(
            WorldLayerRuntimeSnapshot snapshot,
            out string error)
        {
            if (snapshot?.Settlements == null ||
                snapshot.NextSettlementOrdinal != 3 ||
                snapshot.Settlements.Count < 1 ||
                snapshot.Settlements.Count > WorldLayerCatalog.All.Count)
            {
                error = "World layer snapshot settlement collection is invalid.";
                return false;
            }
            var candidate = new SortedDictionary<string, SettlementRuntime>(
                StringComparer.Ordinal);
            var occupied = new HashSet<long>();
            for (var index = 0; index < snapshot.Settlements.Count; index++)
            {
                if (!SettlementRuntime.TryFromSnapshot(
                        snapshot.Settlements[index],
                        out SettlementRuntime restored,
                        out error) ||
                    !candidate.TryAdd(restored.StableId, restored) ||
                    !IsPassableCell(restored.X, restored.Y) ||
                    !occupied.Add(CellKey(restored.X, restored.Y)))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "World layer snapshot contains invalid positions or duplicates.";
                    return false;
                }
            }
            if (!candidate.ContainsKey(WorldLayerCatalog.PrimaryCity.Id) ||
                !candidate.TryGetValue(
                    snapshot.FocusedSettlementId,
                    out _) ||
                !candidate.TryGetValue(
                    snapshot.ControlledCityId,
                    out SettlementRuntime controlled) ||
                controlled.Kind == SettlementKind.Outpost)
            {
                error = "World layer focus or controlled city reference is invalid.";
                return false;
            }
            settlements = candidate;
            FocusedSettlementId = snapshot.FocusedSettlementId;
            ControlledCityId = snapshot.ControlledCityId;
            Revision = snapshot.Revision;
            error = string.Empty;
            return true;
        }

        internal bool TryFindPath(
            string sourceSettlementId,
            string destinationSettlementId,
            out WorldGridPoint[] path)
        {
            path = Array.Empty<WorldGridPoint>();
            SettlementRuntime source = GetSettlement(sourceSettlementId);
            SettlementRuntime destination = GetSettlement(
                destinationSettlementId);
            return source != null && destination != null &&
                !ReferenceEquals(source, destination) &&
                CityPathfinder.TryFindPath(
                    map,
                    source.X,
                    source.Y,
                    destination.X,
                    destination.Y,
                    out path) && path.Length > 0;
        }

        private bool TryValidateEstablishmentLocation(
            int x,
            int y,
            out string error)
        {
            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height ||
                !map.IsRevealed(x, y))
            {
                error = "目标格尚未揭示";
                return false;
            }
            if (!IsPassableCell(x, y))
            {
                error = "目标格不可通行";
                return false;
            }
            foreach (KeyValuePair<string, SettlementRuntime> item in settlements)
            {
                if (item.Value.X == x && item.Value.Y == y)
                {
                    error = "目标格已被 settlement 占用";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool TryPay(
            SettlementDefinition definition,
            ISettlementConstructionAccount account,
            out string error)
        {
            if (account == null)
            {
                error = "建造付款账户不能为空";
                return false;
            }
            if (account.Population < definition.PopulationCost)
            {
                error = "人口不足";
                return false;
            }
            for (var index = 0; index < definition.BuildCosts.Count; index++)
            {
                ResourceAmount cost = definition.BuildCosts[index];
                if (account.GetAmount(cost.ResourceId) < cost.Amount)
                {
                    error = "建造材料不足：" + cost.ResourceId;
                    return false;
                }
            }
            if (!account.TryCommit(
                    definition.BuildCosts,
                    definition.PopulationCost))
            {
                error = "建造成本原子提交失败";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool IsPassableCell(int x, int y)
        {
            return x >= 0 && y >= 0 && x < map.Width && y < map.Height &&
                CityTerrainRules.IsPassable(map.Get(x, y));
        }

        private static long CellKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }
}
