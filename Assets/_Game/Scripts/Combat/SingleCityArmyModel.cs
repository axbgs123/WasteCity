using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Combat
{
    public readonly struct ArmyManufacturingPersistenceState
    {
        public ArmyManufacturingPersistenceState(
            string definitionId,
            float progressSeconds)
        {
            DefinitionId = definitionId;
            ProgressSeconds = progressSeconds;
        }

        public string DefinitionId { get; }
        public float ProgressSeconds { get; }
    }

    public readonly struct ArmyUnitPersistenceState
    {
        public ArmyUnitPersistenceState(
            string stableUnitId,
            string definitionId,
            string squadId,
            int currentHealth,
            float maintenanceElapsed,
            bool isActive)
        {
            StableUnitId = stableUnitId;
            DefinitionId = definitionId;
            SquadId = squadId;
            CurrentHealth = currentHealth;
            MaintenanceElapsed = maintenanceElapsed;
            IsActive = isActive;
        }

        public string StableUnitId { get; }
        public string DefinitionId { get; }
        public string SquadId { get; }
        public int CurrentHealth { get; }
        public float MaintenanceElapsed { get; }
        public bool IsActive { get; }
    }

    public readonly struct ArmyUnitLossPersistenceState
    {
        public ArmyUnitLossPersistenceState(
            string definitionId,
            int count)
        {
            DefinitionId = definitionId;
            Count = count;
        }

        public string DefinitionId { get; }
        public int Count { get; }
    }

    public readonly struct ArmyTechnologyUnitPersistenceState
    {
        public ArmyTechnologyUnitPersistenceState(
            string stableUnitId,
            float regenerationAccumulatorSeconds)
        {
            StableUnitId = stableUnitId;
            RegenerationAccumulatorSeconds = regenerationAccumulatorSeconds;
        }

        public string StableUnitId { get; }
        public float RegenerationAccumulatorSeconds { get; }
    }

    public sealed class ArmyTechnologyPersistenceSnapshot
    {
        public ArmyTechnologyPersistenceSnapshot(
            ArmyTechnologyUnitPersistenceState[] units)
        {
            Units = units == null
                ? null
                : (ArmyTechnologyUnitPersistenceState[])units.Clone();
        }

        public ArmyTechnologyUnitPersistenceState[] Units { get; }
    }

    public sealed class SingleCityArmyPersistenceSnapshot
    {
        public SingleCityArmyPersistenceSnapshot(
            int nextUnitOrdinal,
            ArmyManufacturingPersistenceState[] manufacturing,
            ArmyUnitPersistenceState[] units,
            FriendlyUnitCommandPersistenceSnapshot command,
            bool leaderAssigned,
            bool leaderHealthy,
            ArmyUnitLossPersistenceState[] losses)
        {
            NextUnitOrdinal = nextUnitOrdinal;
            Manufacturing = Clone(manufacturing);
            Units = Clone(units);
            Command = command;
            LeaderAssigned = leaderAssigned;
            LeaderHealthy = leaderHealthy;
            Losses = Clone(losses);
        }

        public int NextUnitOrdinal { get; }
        public ArmyManufacturingPersistenceState[] Manufacturing { get; }
        public ArmyUnitPersistenceState[] Units { get; }
        public FriendlyUnitCommandPersistenceSnapshot Command { get; }
        public bool LeaderAssigned { get; }
        public bool LeaderHealthy { get; }
        public ArmyUnitLossPersistenceState[] Losses { get; }

        private static T[] Clone<T>(T[] values)
        {
            return values == null ? null : (T[])values.Clone();
        }
    }

    public sealed class SingleCityArmyRestorePlan
    {
        internal SingleCityArmyRestorePlan(
            SingleCityArmyModel owner,
            ulong expectedGeneration,
            SingleCityArmyPersistenceSnapshot snapshot,
            FriendlyUnitCommandRestorePlan commandPlan)
        {
            Owner = owner;
            ExpectedGeneration = expectedGeneration;
            Snapshot = snapshot;
            CommandPlan = commandPlan;
        }

        internal SingleCityArmyModel Owner { get; }
        internal ulong ExpectedGeneration { get; }
        internal SingleCityArmyPersistenceSnapshot Snapshot { get; }
        internal FriendlyUnitCommandRestorePlan CommandPlan { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class ArmySquadSnapshot
    {
        internal ArmySquadSnapshot(
            string stableId,
            int maximumUnits,
            bool leaderAssigned,
            bool leaderHealthy)
        {
            StableId = stableId;
            MaximumUnits = maximumUnits;
            LeaderAssigned = leaderAssigned;
            LeaderHealthy = leaderHealthy;
        }

        public string StableId { get; }
        public int MaximumUnits { get; }
        public bool LeaderAssigned { get; }
        public bool LeaderHealthy { get; }
    }

    public sealed class ArmyUnitSnapshot
    {
        internal ArmyUnitSnapshot(
            string stableId,
            string definitionId,
            string squadId,
            int currentHealth,
            int maximumHealth,
            float maintenanceElapsed,
            bool isActive)
        {
            StableId = stableId;
            DefinitionId = definitionId;
            SquadId = squadId;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            MaintenanceElapsed = maintenanceElapsed;
            IsActive = isActive;
        }

        public string StableId { get; }
        public string DefinitionId { get; }
        public string SquadId { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public float MaintenanceElapsed { get; }
        public bool IsActive { get; }
    }

    public sealed class SingleCityArmyModel
    {
        public const string DefaultSquadId = "core.squad.000001";
        public const int DefaultSquadMaximumUnits = 12;

        private sealed class UnitState
        {
            public UnitState(
                string stableId,
                ArmyUnitDefinition definition,
                int maximumHealth)
            {
                StableId = stableId;
                Definition = definition;
                MaximumHealth = Math.Max(1, maximumHealth);
                CurrentHealth = MaximumHealth;
            }

            public string StableId { get; }
            public ArmyUnitDefinition Definition { get; }
            public int CurrentHealth { get; set; }
            public int MaximumHealth { get; set; }
            public float MaintenanceElapsed { get; set; }
            public float RegenerationAccumulatorSeconds { get; set; }
            public bool IsActive { get; set; } = true;
        }

        private readonly List<UnitState> units = new List<UnitState>();
        private readonly Dictionary<string, float> manufacturingProgress =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> lossesByDefinitionId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int nextUnitOrdinal = 1;
        private bool leaderAssigned;
        private bool leaderHealthy;
        private ulong persistenceGeneration;
        private Func<ResearchEffectSnapshot> researchEffectsProvider = () =>
            ResearchEffectResolver.Resolve(Array.Empty<string>());

        public FriendlyUnitCommandModel Commands { get; } =
            new FriendlyUnitCommandModel();
        public ArmySquadSnapshot DefaultSquad => new ArmySquadSnapshot(
            DefaultSquadId,
            DefaultSquadMaximumUnits,
            leaderAssigned,
            leaderHealthy);
        public IReadOnlyList<ArmyUnitSnapshot> Units => CaptureUnits();
        public int NextUnitOrdinal => nextUnitOrdinal;

        public void ConfigureResearchEffects(
            Func<ResearchEffectSnapshot> provider)
        {
            researchEffectsProvider = provider ?? (() =>
                ResearchEffectResolver.Resolve(Array.Empty<string>()));
            RefreshResolvedHealth();
        }

        public int UnitCount(string definitionId)
        {
            var count = 0;
            for (var index = 0; index < units.Count; index++)
            {
                if (string.Equals(
                        units[index].Definition.Id,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        public int LossCount(string definitionId)
        {
            return definitionId != null &&
                   lossesByDefinitionId.TryGetValue(
                       definitionId,
                       out int value)
                ? value
                : 0;
        }

        public float ManufacturingProgress(string definitionId)
        {
            return definitionId != null &&
                   manufacturingProgress.TryGetValue(
                       definitionId,
                       out float value)
                ? value
                : 0f;
        }

        public int TickManufacturing(
            string definitionId,
            float deltaSeconds,
            int operationalSourceBuildings,
            bool globallyPaused,
            CityResourceStorageModel cityStorage)
        {
            ArmyUnitDefinition definition = ArmyUnitCatalog.Find(
                definitionId);
            if (definition == null || cityStorage == null ||
                globallyPaused || operationalSourceBuildings <= 0)
            {
                return 0;
            }

            int typeCapacity = Math.Max(0, operationalSourceBuildings) *
                               ResolveEffects().ResolveUnitCapacity(
                                   definition.SourceBuildingId,
                                   definition.CapacityPerBuilding);
            if (UnitCount(definition.Id) >= typeCapacity ||
                units.Count >= DefaultSquadMaximumUnits)
            {
                manufacturingProgress[definition.Id] = 0f;
                return 0;
            }

            float progress = ManufacturingProgress(definition.Id);
            progress += Math.Max(0f, deltaSeconds) *
                        operationalSourceBuildings;
            int produced = 0;
            while (progress + .00001f >= definition.ManufactureSeconds &&
                   UnitCount(definition.Id) < typeCapacity &&
                   units.Count < DefaultSquadMaximumUnits)
            {
                if (!cityStorage.TryCommitBatch(
                        definition.ManufactureCosts,
                        Array.Empty<ResourceAmount>()))
                {
                    progress = definition.ManufactureSeconds;
                    break;
                }

                units.Add(new UnitState(
                    "core.army-unit." + nextUnitOrdinal.ToString("D6"),
                    definition,
                    ResolveMaximumHealth(definition)));
                nextUnitOrdinal++;
                progress -= definition.ManufactureSeconds;
                produced++;
            }
            manufacturingProgress[definition.Id] = Math.Max(0f, progress);
            if (produced > 0 || deltaSeconds > 0f)
                AdvancePersistenceGeneration();
            return produced;
        }

        public int TickMaintenance(
            float deltaSeconds,
            bool globallyPaused,
            CityResourceStorageModel cityStorage)
        {
            if (globallyPaused || cityStorage == null) return 0;
            int paidCycles = 0;
            float safeDelta = Math.Max(0f, deltaSeconds);
            for (var index = 0; index < units.Count; index++)
            {
                UnitState unit = units[index];
                unit.MaintenanceElapsed += safeDelta;
                while (unit.MaintenanceElapsed + .00001f >=
                       unit.Definition.MaintenanceSeconds)
                {
                    if (!cityStorage.TryCommitBatch(
                            unit.Definition.MaintenanceCosts,
                            Array.Empty<ResourceAmount>()))
                    {
                        unit.MaintenanceElapsed =
                            unit.Definition.MaintenanceSeconds;
                        unit.IsActive = false;
                        break;
                    }
                    unit.MaintenanceElapsed -=
                        unit.Definition.MaintenanceSeconds;
                    unit.IsActive = true;
                    paidCycles++;
                }
            }
            if (safeDelta > 0f || paidCycles > 0)
                AdvancePersistenceGeneration();
            return paidCycles;
        }

        public int TickTechnologyEffects(
            float deltaSeconds,
            bool globallyPaused)
        {
            RefreshResolvedHealth();
            float delta = Math.Max(0f, deltaSeconds);
            if (globallyPaused || delta <= 0f) return 0;
            if (!ResolveEffects().TissueRegeneration)
            {
                ClearRegenerationAccumulators();
                return 0;
            }

            int totalHealed = 0;
            bool stateChanged = false;
            for (var index = 0; index < units.Count; index++)
            {
                UnitState unit = units[index];
                if (!unit.IsActive ||
                    unit.CurrentHealth >= unit.MaximumHealth)
                {
                    if (unit.RegenerationAccumulatorSeconds > 0f)
                    {
                        unit.RegenerationAccumulatorSeconds = 0f;
                        stateChanged = true;
                    }
                    continue;
                }

                unit.RegenerationAccumulatorSeconds += delta;
                stateChanged = true;
                int cycles = (int)Math.Floor(
                    unit.RegenerationAccumulatorSeconds + .00001f);
                if (cycles <= 0) continue;
                int healed = Math.Min(
                    cycles,
                    unit.MaximumHealth - unit.CurrentHealth);
                unit.CurrentHealth += healed;
                totalHealed += healed;
                unit.RegenerationAccumulatorSeconds =
                    unit.CurrentHealth >= unit.MaximumHealth
                        ? 0f
                        : Math.Max(
                            0f,
                            unit.RegenerationAccumulatorSeconds - cycles);
            }
            if (stateChanged) AdvancePersistenceGeneration();
            return totalHealed;
        }

        public ArmyTechnologyPersistenceSnapshot CaptureTechnologyState()
        {
            var result = new ArmyTechnologyUnitPersistenceState[units.Count];
            for (var index = 0; index < units.Count; index++)
            {
                result[index] = new ArmyTechnologyUnitPersistenceState(
                    units[index].StableId,
                    units[index].RegenerationAccumulatorSeconds);
            }
            return new ArmyTechnologyPersistenceSnapshot(result);
        }

        public bool TryRestoreTechnologyState(
            ArmyTechnologyPersistenceSnapshot snapshot,
            out string error)
        {
            if (snapshot?.Units == null)
                return Fail("军队科技状态不完整", out error);
            var byId = new Dictionary<string, float>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Units.Length; index++)
            {
                ArmyTechnologyUnitPersistenceState saved =
                    snapshot.Units[index];
                if (string.IsNullOrWhiteSpace(saved.StableUnitId) ||
                    !IsFinite(saved.RegenerationAccumulatorSeconds) ||
                    saved.RegenerationAccumulatorSeconds < 0f ||
                    saved.RegenerationAccumulatorSeconds >= 1f ||
                    !byId.TryAdd(
                        saved.StableUnitId,
                        saved.RegenerationAccumulatorSeconds))
                {
                    return Fail("军队再生计时状态无效", out error);
                }
            }
            if (byId.Count != units.Count)
                return Fail("军队再生计时与单位集合不匹配", out error);
            for (var index = 0; index < units.Count; index++)
            {
                if (!byId.TryGetValue(
                        units[index].StableId,
                        out float accumulator))
                {
                    return Fail("军队再生计时缺少单位", out error);
                }
                units[index].RegenerationAccumulatorSeconds = accumulator;
            }
            AdvancePersistenceGeneration();
            error = string.Empty;
            return true;
        }

        public void SetLeaderAssignment(
            bool assigned,
            bool leaderHealthy)
        {
            bool healthy = assigned && leaderHealthy;
            if (leaderAssigned == assigned && this.leaderHealthy == healthy)
                return;
            leaderAssigned = assigned;
            this.leaderHealthy = healthy;
            AdvancePersistenceGeneration();
        }

        public float ResolveSquadDamageMultiplier()
        {
            return leaderAssigned && leaderHealthy ? 1.2f : 1f;
        }

        public int ApplyDamage(
            string stableUnitId,
            int rawDamage,
            DamageType damageType)
        {
            RefreshResolvedHealth();
            if (string.IsNullOrWhiteSpace(stableUnitId) || rawDamage <= 0)
                return 0;
            for (var index = 0; index < units.Count; index++)
            {
                UnitState unit = units[index];
                if (!string.Equals(
                        unit.StableId,
                        stableUnitId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                int resolved = DamageMatrix.Apply(
                    rawDamage,
                    damageType,
                    unit.Definition.Armor);
                int applied = Math.Min(unit.CurrentHealth, resolved);
                unit.CurrentHealth -= applied;
                if (unit.CurrentHealth == 0)
                {
                    RecordLoss(unit.Definition.Id);
                    units.RemoveAt(index);
                }
                AdvancePersistenceGeneration();
                return applied;
            }
            return 0;
        }

        public int ApplyExpeditionCasualties(
            IReadOnlyList<string> stableUnitIds)
        {
            if (stableUnitIds == null || stableUnitIds.Count == 0)
                return 0;
            var casualties = new HashSet<string>(
                stableUnitIds,
                StringComparer.Ordinal);
            int removed = 0;
            for (var index = units.Count - 1; index >= 0; index--)
            {
                UnitState unit = units[index];
                if (!casualties.Contains(unit.StableId)) continue;
                RecordLoss(unit.Definition.Id);
                units.RemoveAt(index);
                removed++;
            }
            if (removed > 0) AdvancePersistenceGeneration();
            return removed;
        }

        public SingleCityArmyPersistenceSnapshot CaptureForPersistence()
        {
            RefreshResolvedHealth();
            var manufacturing = new List<
                ArmyManufacturingPersistenceState>();
            for (var index = 0; index < ArmyUnitCatalog.All.Count; index++)
            {
                ArmyUnitDefinition definition = ArmyUnitCatalog.All[index];
                float progress = ManufacturingProgress(definition.Id);
                if (progress > 0f)
                {
                    manufacturing.Add(
                        new ArmyManufacturingPersistenceState(
                            definition.Id,
                            progress));
                }
            }

            var persistedUnits = new ArmyUnitPersistenceState[units.Count];
            for (var index = 0; index < units.Count; index++)
            {
                UnitState unit = units[index];
                persistedUnits[index] = new ArmyUnitPersistenceState(
                    unit.StableId,
                    unit.Definition.Id,
                    DefaultSquadId,
                    unit.CurrentHealth,
                    unit.MaintenanceElapsed,
                    unit.IsActive);
            }

            var losses = new List<ArmyUnitLossPersistenceState>();
            for (var index = 0; index < ArmyUnitCatalog.All.Count; index++)
            {
                ArmyUnitDefinition definition = ArmyUnitCatalog.All[index];
                int count = LossCount(definition.Id);
                if (count > 0)
                    losses.Add(new ArmyUnitLossPersistenceState(
                        definition.Id,
                        count));
            }
            return new SingleCityArmyPersistenceSnapshot(
                nextUnitOrdinal,
                manufacturing.ToArray(),
                persistedUnits,
                Commands.CaptureForPersistence(),
                leaderAssigned,
                leaderHealthy,
                losses.ToArray());
        }

        public bool TryPrepareRestoreForPersistence(
            SingleCityArmyPersistenceSnapshot snapshot,
            out SingleCityArmyRestorePlan plan,
            out string error)
        {
            plan = null;
            if (!TryValidateSnapshot(snapshot, out error)) return false;
            if (!Commands.TryPrepareRestoreForPersistence(
                    snapshot.Command,
                    out FriendlyUnitCommandRestorePlan commandPlan,
                    out error))
            {
                return false;
            }
            SingleCityArmyPersistenceSnapshot copy = CloneSnapshot(snapshot);
            plan = new SingleCityArmyRestorePlan(
                this,
                persistenceGeneration,
                copy,
                commandPlan);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestoreForPersistence(
            SingleCityArmyRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.Consumed ||
                plan.ExpectedGeneration != persistenceGeneration)
                return Fail("军队恢复计划无效或已过期", out error);
            if (!Commands.TryCommitRestoreForPersistence(
                    plan.CommandPlan,
                    out error))
            {
                return false;
            }

            SingleCityArmyPersistenceSnapshot value = plan.Snapshot;
            units.Clear();
            for (var index = 0; index < value.Units.Length; index++)
            {
                ArmyUnitPersistenceState saved = value.Units[index];
                var unit = new UnitState(
                    saved.StableUnitId,
                    ArmyUnitCatalog.Find(saved.DefinitionId),
                    ResolveMaximumHealth(
                        ArmyUnitCatalog.Find(saved.DefinitionId)))
                {
                    CurrentHealth = saved.CurrentHealth,
                    MaintenanceElapsed = saved.MaintenanceElapsed,
                    IsActive = saved.IsActive,
                };
                units.Add(unit);
            }
            manufacturingProgress.Clear();
            for (var index = 0; index < value.Manufacturing.Length; index++)
            {
                manufacturingProgress.Add(
                    value.Manufacturing[index].DefinitionId,
                    value.Manufacturing[index].ProgressSeconds);
            }
            lossesByDefinitionId.Clear();
            for (var index = 0; index < value.Losses.Length; index++)
            {
                lossesByDefinitionId.Add(
                    value.Losses[index].DefinitionId,
                    value.Losses[index].Count);
            }
            nextUnitOrdinal = value.NextUnitOrdinal;
            leaderAssigned = value.LeaderAssigned;
            leaderHealthy = value.LeaderHealthy;
            plan.Consumed = true;
            AdvancePersistenceGeneration();
            error = string.Empty;
            return true;
        }

        private bool TryValidateSnapshot(
            SingleCityArmyPersistenceSnapshot snapshot,
            out string error)
        {
            if (snapshot == null || snapshot.Manufacturing == null ||
                snapshot.Units == null || snapshot.Command == null ||
                snapshot.Losses == null || snapshot.NextUnitOrdinal <= 0)
                return Fail("军队存档不完整", out error);
            if (snapshot.Units.Length > DefaultSquadMaximumUnits)
                return Fail("默认小队超过十二单位", out error);
            if (snapshot.LeaderHealthy && !snapshot.LeaderAssigned)
                return Fail("未带队领袖不能标记为健康带队", out error);

            var manufacturingIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Manufacturing.Length; index++)
            {
                ArmyManufacturingPersistenceState item =
                    snapshot.Manufacturing[index];
                ArmyUnitDefinition definition = ArmyUnitCatalog.Find(
                    item.DefinitionId);
                if (definition == null ||
                    !manufacturingIds.Add(item.DefinitionId) ||
                    !IsFinite(item.ProgressSeconds) ||
                    item.ProgressSeconds < 0f ||
                    item.ProgressSeconds > definition.ManufactureSeconds)
                    return Fail("军队制造进度无效", out error);
            }

            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            int maximumOrdinal = 0;
            for (var index = 0; index < snapshot.Units.Length; index++)
            {
                ArmyUnitPersistenceState item = snapshot.Units[index];
                ArmyUnitDefinition definition = ArmyUnitCatalog.Find(
                    item.DefinitionId);
                if (definition == null ||
                    !unitIds.Add(item.StableUnitId) ||
                    !TryParseUnitOrdinal(item.StableUnitId, out int ordinal) ||
                    !string.Equals(
                        item.SquadId,
                        DefaultSquadId,
                        StringComparison.Ordinal) ||
                    item.CurrentHealth <= 0 ||
                    item.CurrentHealth > ResolveMaximumHealth(definition) ||
                    !IsFinite(item.MaintenanceElapsed) ||
                    item.MaintenanceElapsed < 0f ||
                    item.MaintenanceElapsed > definition.MaintenanceSeconds ||
                    item.IsActive &&
                    item.MaintenanceElapsed >= definition.MaintenanceSeconds)
                    return Fail("军队单位状态无效", out error);
                maximumOrdinal = Math.Max(maximumOrdinal, ordinal);
            }
            if (snapshot.NextUnitOrdinal <= maximumOrdinal)
                return Fail("军队单位高水位无效", out error);

            var lossIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Losses.Length; index++)
            {
                ArmyUnitLossPersistenceState item = snapshot.Losses[index];
                if (ArmyUnitCatalog.Find(item.DefinitionId) == null ||
                    !lossIds.Add(item.DefinitionId) || item.Count <= 0)
                    return Fail("军队损失记录无效", out error);
            }
            error = string.Empty;
            return true;
        }

        private static SingleCityArmyPersistenceSnapshot CloneSnapshot(
            SingleCityArmyPersistenceSnapshot snapshot)
        {
            FriendlyUnitCommandPersistenceSnapshot command = snapshot.Command;
            return new SingleCityArmyPersistenceSnapshot(
                snapshot.NextUnitOrdinal,
                snapshot.Manufacturing,
                snapshot.Units,
                new FriendlyUnitCommandPersistenceSnapshot(
                    command.HasFixedRally,
                    command.RallyX,
                    command.RallyY,
                    command.Command,
                    command.HasExpeditionTarget,
                    command.ExpeditionTargetX,
                    command.ExpeditionTargetY,
                    command.PuppetLosses,
                    command.BehemothLosses,
                    command.ControlledLosses),
                snapshot.LeaderAssigned,
                snapshot.LeaderHealthy,
                snapshot.Losses);
        }

        private static bool TryParseUnitOrdinal(
            string stableUnitId,
            out int ordinal)
        {
            const string prefix = "core.army-unit.";
            ordinal = 0;
            return stableUnitId != null &&
                   stableUnitId.Length == prefix.Length + 6 &&
                   stableUnitId.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(
                       stableUnitId.Substring(prefix.Length),
                       out ordinal) && ordinal > 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private void AdvancePersistenceGeneration()
        {
            unchecked { persistenceGeneration++; }
        }

        private void RecordLoss(string definitionId)
        {
            lossesByDefinitionId.TryGetValue(definitionId, out int before);
            lossesByDefinitionId[definitionId] = before + 1;
            Commands.RecordLoss(LossKind(definitionId));
        }

        private static FriendlyUnitKind LossKind(string definitionId)
        {
            if (string.Equals(
                    definitionId,
                    ArmyUnitCatalog.CombatPuppetId,
                    StringComparison.Ordinal))
            {
                return FriendlyUnitKind.Puppet;
            }
            if (string.Equals(
                    definitionId,
                    ArmyUnitCatalog.PsionicMechId,
                    StringComparison.Ordinal))
            {
                return FriendlyUnitKind.Controlled;
            }
            return FriendlyUnitKind.Behemoth;
        }

        private ArmyUnitSnapshot[] CaptureUnits()
        {
            RefreshResolvedHealth();
            var result = new ArmyUnitSnapshot[units.Count];
            for (var index = 0; index < units.Count; index++)
            {
                UnitState unit = units[index];
                result[index] = new ArmyUnitSnapshot(
                    unit.StableId,
                    unit.Definition.Id,
                    DefaultSquadId,
                    unit.CurrentHealth,
                    unit.MaximumHealth,
                    unit.MaintenanceElapsed,
                    unit.IsActive);
            }
            return result;
        }

        private ResearchEffectSnapshot ResolveEffects()
        {
            return researchEffectsProvider?.Invoke() ??
                ResearchEffectResolver.Resolve(Array.Empty<string>());
        }

        private int ResolveMaximumHealth(ArmyUnitDefinition definition)
        {
            if (definition == null) return 1;
            return Math.Max(
                1,
                (int)Math.Round(
                    definition.MaximumHealth *
                    ResolveEffects().ResolveUnitHealthMultiplier(
                        definition.Id),
                    MidpointRounding.AwayFromZero));
        }

        private void RefreshResolvedHealth()
        {
            for (var index = 0; index < units.Count; index++)
            {
                UnitState unit = units[index];
                int beforeMaximum = Math.Max(1, unit.MaximumHealth);
                int afterMaximum = ResolveMaximumHealth(unit.Definition);
                if (beforeMaximum == afterMaximum) continue;
                unit.CurrentHealth = Math.Max(
                    1,
                    Math.Min(
                        afterMaximum,
                        (int)Math.Round(
                            unit.CurrentHealth *
                            (afterMaximum / (float)beforeMaximum),
                            MidpointRounding.AwayFromZero)));
                unit.MaximumHealth = afterMaximum;
                AdvancePersistenceGeneration();
            }
        }

        private void ClearRegenerationAccumulators()
        {
            bool changed = false;
            for (var index = 0; index < units.Count; index++)
            {
                if (units[index].RegenerationAccumulatorSeconds <= 0f)
                    continue;
                units[index].RegenerationAccumulatorSeconds = 0f;
                changed = true;
            }
            if (changed) AdvancePersistenceGeneration();
        }
    }
}
