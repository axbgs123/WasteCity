using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProductionPersistenceState3D
    {
        private readonly ReadOnlyCollection<ResourceAmount> input;
        private readonly ReadOnlyCollection<ResourceAmount> reservedInput;
        private readonly ReadOnlyCollection<ResourceAmount> output;

        public GrayboxProductionPersistenceState3D(
            string stableInstanceId,
            string definitionId,
            IReadOnlyList<ResourceAmount> input,
            bool hasReservedInputs,
            IReadOnlyList<ResourceAmount> reservedInput,
            IReadOnlyList<ResourceAmount> output,
            float progressSeconds,
            bool isPlayerPaused,
            string boundResourceNodeId,
            int boundNodeX,
            int boundNodeY)
        {
            StableInstanceId = stableInstanceId;
            DefinitionId = definitionId;
            this.input = Array.AsReadOnly(CopyAmounts(input));
            HasReservedInputs = hasReservedInputs;
            this.reservedInput = Array.AsReadOnly(CopyAmounts(reservedInput));
            this.output = Array.AsReadOnly(CopyAmounts(output));
            ProgressSeconds = progressSeconds;
            IsPlayerPaused = isPlayerPaused;
            BoundResourceNodeId = boundResourceNodeId;
            BoundNodeX = boundNodeX;
            BoundNodeY = boundNodeY;
        }

        public string StableInstanceId { get; }
        public string DefinitionId { get; }
        public IReadOnlyList<ResourceAmount> Input => input;
        public bool HasReservedInputs { get; }
        public IReadOnlyList<ResourceAmount> ReservedInput => reservedInput;
        public IReadOnlyList<ResourceAmount> Output => output;
        public float ProgressSeconds { get; }
        public bool IsPlayerPaused { get; }
        public string BoundResourceNodeId { get; }
        public int BoundNodeX { get; }
        public int BoundNodeY { get; }

        private static ResourceAmount[] CopyAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ResourceAmount>();
            var copy = new ResourceAmount[source.Count];
            for (var index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return copy;
        }
    }

    public sealed class GrayboxProductionRestorePlan3D
    {
        internal GrayboxProductionRestorePlan3D(
            GrayboxProductionRuntime3D owner,
            ulong expectedFingerprint,
            ulong expectedSynchronizationGeneration,
            IReadOnlyList<GrayboxProductionRestoreEntry3D> entries,
            IReadOnlyList<GrayboxProductionPersistenceState3D> orphans)
        {
            Owner = owner;
            ExpectedFingerprint = expectedFingerprint;
            ExpectedSynchronizationGeneration =
                expectedSynchronizationGeneration;
            Entries = entries;
            Orphans = orphans;
        }

        internal GrayboxProductionRuntime3D Owner { get; }
        internal ulong ExpectedFingerprint { get; }
        internal ulong ExpectedSynchronizationGeneration { get; }
        internal IReadOnlyList<GrayboxProductionRestoreEntry3D> Entries { get; }
        internal IReadOnlyList<GrayboxProductionPersistenceState3D> Orphans { get; }
        internal bool IsConsumed { get; set; }
    }

    internal readonly struct GrayboxProductionRestoreEntry3D
    {
        public GrayboxProductionRestoreEntry3D(
            BuildingProductionState state,
            GrayboxProductionPersistenceState3D snapshot)
        {
            State = state;
            Snapshot = snapshot;
        }

        public BuildingProductionState State { get; }
        public GrayboxProductionPersistenceState3D Snapshot { get; }
    }

    public sealed class GrayboxProductionEvacuationPayload3D
    {
        private readonly ReadOnlyCollection<ResourceAmount> input;
        private readonly ReadOnlyCollection<ResourceAmount> reservedInput;
        private readonly ReadOnlyCollection<ResourceAmount> output;

        internal GrayboxProductionEvacuationPayload3D(
            BuildingProductionState sourceState,
            ResourceAmount[] input,
            ResourceAmount[] reservedInput,
            ResourceAmount[] output)
        {
            SourceState = sourceState ??
                throw new ArgumentNullException(nameof(sourceState));
            StableInstanceId = sourceState.StableInstanceId;
            DefinitionId = sourceState.Definition.Id;
            ProgressSeconds = sourceState.ProgressSeconds;
            HasReservedInputs = sourceState.HasReservedInputs;
            IsPlayerPaused = sourceState.IsPlayerPaused;
            BoundResourceNodeId = sourceState.BoundResourceNodeId;
            BoundNodeX = sourceState.BoundNodeX;
            BoundNodeY = sourceState.BoundNodeY;
            this.input = Array.AsReadOnly(
                input ?? Array.Empty<ResourceAmount>());
            this.reservedInput = Array.AsReadOnly(
                reservedInput ?? Array.Empty<ResourceAmount>());
            this.output = Array.AsReadOnly(
                output ?? Array.Empty<ResourceAmount>());
        }

        public string StableInstanceId { get; }
        public IReadOnlyList<ResourceAmount> Input => input;
        public IReadOnlyList<ResourceAmount> ReservedInput => reservedInput;
        public IReadOnlyList<ResourceAmount> Output => output;

        internal BuildingProductionState SourceState { get; }
        internal string DefinitionId { get; }
        internal float ProgressSeconds { get; }
        internal bool HasReservedInputs { get; }
        internal bool IsPlayerPaused { get; }
        internal string BoundResourceNodeId { get; }
        internal int BoundNodeX { get; }
        internal int BoundNodeY { get; }
    }

    public sealed class GrayboxProductionRuntime3D
    {
        private readonly Dictionary<string, BuildingProductionState> stateById =
            new Dictionary<string, BuildingProductionState>(StringComparer.Ordinal);
        private readonly List<GrayboxBuildingInstance3D> orderedInstances =
            new List<GrayboxBuildingInstance3D>();
        private readonly List<BuildingProductionState> states =
            new List<BuildingProductionState>();
        private readonly List<BuildingProductionState> runnableStates =
            new List<BuildingProductionState>();
        private readonly HashSet<string> retainedStateIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removedStateIds = new List<string>();
        private readonly Dictionary<string, GrayboxProductionPersistenceState3D>
            orphanStateById =
                new Dictionary<string, GrayboxProductionPersistenceState3D>(
                    StringComparer.Ordinal);
        private readonly HashSet<string> retainedOrphanIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> retainedWarehouseIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> warehouseIds = new List<string>();
        private readonly ReadOnlyCollection<BuildingProductionState> readOnlyStates;
        private readonly ReadOnlyCollection<BuildingProductionState>
            readOnlyRunnableStates;
        private ulong synchronizationGeneration;

        public GrayboxProductionRuntime3D()
        {
            readOnlyStates = new ReadOnlyCollection<BuildingProductionState>(states);
            readOnlyRunnableStates =
                new ReadOnlyCollection<BuildingProductionState>(runnableStates);
        }

        public IReadOnlyList<BuildingProductionState> States => readOnlyStates;
        public IReadOnlyList<BuildingProductionState> RunnableStates =>
            readOnlyRunnableStates;
        public int ActiveWarehouseCount { get; private set; }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius)
        {
            SynchronizeCore(
                instances,
                cityMode,
                cityX,
                cityY,
                groundRadius,
                cityStorage: null);
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            CityResourceStorageModel cityStorage)
        {
            if (cityStorage == null)
                throw new ArgumentNullException(nameof(cityStorage));
            SynchronizeCore(
                instances,
                cityMode,
                cityX,
                cityY,
                groundRadius,
                cityStorage);
        }

        private void SynchronizeCore(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            CityResourceStorageModel cityStorage)
        {
            synchronizationGeneration++;
            orderedInstances.Clear();
            states.Clear();
            runnableStates.Clear();
            retainedStateIds.Clear();
            retainedOrphanIds.Clear();
            retainedWarehouseIds.Clear();
            ActiveWarehouseCount = 0;

            if (instances != null)
            {
                for (int index = 0; index < instances.Count; index++)
                {
                    if (instances[index] != null)
                        orderedInstances.Add(instances[index]);
                }
            }

            orderedInstances.Sort((left, right) => string.Compare(
                left.StableInstanceId,
                right.StableInstanceId,
                StringComparison.Ordinal));

            for (int index = 0; index < orderedInstances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = orderedInstances[index];
                bool retainsOrphan =
                    GrayboxBuildingOperationalAccess3D.CanRetainState(instance) &&
                    orphanStateById.ContainsKey(instance.StableInstanceId);
                if (cityStorage == null)
                {
                    if (GrayboxProductionEligibility3D.IsActiveWarehouse(instance))
                        ActiveWarehouseCount++;
                }
                else
                {
                    SynchronizeWarehouse(
                        instance,
                        cityMode,
                        cityX,
                        cityY,
                        groundRadius,
                        cityStorage);
                }

                if (retainsOrphan)
                {
                    retainedOrphanIds.Add(instance.StableInstanceId);
                    continue;
                }

                if (!GrayboxBuildingOperationalAccess3D.CanRetainState(instance) ||
                    !FormalProductionDefinitionCatalog.TryGetByBuildingId(
                        instance.Placement.Definition.Id.Value,
                        out FormalProductionDefinition definition) ||
                    (definition.UsesBoundResourceNode &&
                     !instance.BoundResourceNode.IsValid))
                {
                    continue;
                }

                retainedStateIds.Add(instance.StableInstanceId);
                if (!stateById.TryGetValue(
                        instance.StableInstanceId,
                        out BuildingProductionState state))
                {
                    ResourceNodeBinding binding = instance.BoundResourceNode;
                    state = new BuildingProductionState(
                        instance.StableInstanceId,
                        definition,
                        binding.StableId,
                        binding.X,
                        binding.Y);
                    stateById.Add(instance.StableInstanceId, state);
                }
                else if (!StateMatchesInstance(state, definition, instance))
                {
                    ResourceNodeBinding binding = instance.BoundResourceNode;
                    state = new BuildingProductionState(
                        instance.StableInstanceId,
                        definition,
                        binding.StableId,
                        binding.X,
                        binding.Y);
                    stateById[instance.StableInstanceId] = state;
                }

                bool canRun =
                    GrayboxBuildingOperationalAccess3D.CanRunLocally(
                        instance,
                        cityMode);
                state.SetLogisticsConnected(
                    canRun &&
                    GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                        instance,
                        cityMode,
                        cityX,
                        cityY,
                        groundRadius));
                states.Add(state);
                if (canRun)
                    runnableStates.Add(state);
            }

            removedStateIds.Clear();
            foreach (string stableInstanceId in stateById.Keys)
            {
                if (!retainedStateIds.Contains(stableInstanceId))
                    removedStateIds.Add(stableInstanceId);
            }
            for (int index = 0; index < removedStateIds.Count; index++)
                stateById.Remove(removedStateIds[index]);

            removedStateIds.Clear();
            foreach (string stableInstanceId in orphanStateById.Keys)
            {
                if (!retainedOrphanIds.Contains(stableInstanceId))
                    removedStateIds.Add(stableInstanceId);
            }
            for (int index = 0; index < removedStateIds.Count; index++)
                orphanStateById.Remove(removedStateIds[index]);

            if (cityStorage != null)
            {
                cityStorage.CopyWarehouseIds(warehouseIds);
                for (int index = 0; index < warehouseIds.Count; index++)
                {
                    string stableInstanceId = warehouseIds[index];
                    if (retainedWarehouseIds.Contains(stableInstanceId))
                        continue;
                    cityStorage.TrySetWarehouseConnected(
                        stableInstanceId,
                        connected: false);
                    cityStorage.TryRemoveWarehouse(stableInstanceId);
                }
            }
        }

        private void SynchronizeWarehouse(
            GrayboxBuildingInstance3D instance,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            CityResourceStorageModel cityStorage)
        {
            if (!IsWarehouse(instance)) return;
            bool canOwnStorage =
                instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned;
            if (canOwnStorage &&
                !cityStorage.ContainsWarehouse(instance.StableInstanceId))
            {
                cityStorage.TryRegisterWarehouse(
                    instance.StableInstanceId,
                    connected: false);
            }
            if (!cityStorage.ContainsWarehouse(instance.StableInstanceId))
                return;

            retainedWarehouseIds.Add(instance.StableInstanceId);
            bool connected =
                GrayboxProductionEligibility3D.IsActiveWarehouse(instance) &&
                GrayboxBuildingOperationalAccess3D.CanRunLocally(
                    instance,
                    cityMode) &&
                GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                    instance,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius);
            cityStorage.TrySetWarehouseConnected(
                instance.StableInstanceId,
                connected);
            if (connected) ActiveWarehouseCount++;
        }

        private static bool IsWarehouse(GrayboxBuildingInstance3D instance)
        {
            return instance?.Placement?.Definition != null &&
                string.Equals(
                    instance.Placement.Definition.Id.Value,
                    BuildingCatalog.Warehouse.Id.Value,
                    StringComparison.Ordinal);
        }

        public bool TryGetState(
            string stableInstanceId,
            out BuildingProductionState state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                stateById.TryGetValue(stableInstanceId, out state);
        }

        public bool TryCaptureEvacuationPayload(
            string stableInstanceId,
            out GrayboxProductionEvacuationPayload3D payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                !stateById.TryGetValue(
                    stableInstanceId,
                    out BuildingProductionState state))
            {
                return false;
            }

            payload = new GrayboxProductionEvacuationPayload3D(
                state,
                state.Input.CapturePositiveAmounts(),
                CopyAmounts(state.ReservedInputs),
                state.Output.CapturePositiveAmounts());
            return true;
        }

        public GrayboxProductionPersistenceState3D[] CaptureForPersistence()
        {
            var captured = new List<GrayboxProductionPersistenceState3D>(
                stateById.Count + orphanStateById.Count);
            foreach (BuildingProductionState state in stateById.Values)
                captured.Add(CaptureState(state));
            foreach (GrayboxProductionPersistenceState3D orphan in
                     orphanStateById.Values)
            {
                captured.Add(Clone(orphan));
            }
            captured.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
            return captured.ToArray();
        }

        public bool TryPrepareRestore(
            IReadOnlyList<GrayboxProductionPersistenceState3D> snapshots,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            WorldMapModel world,
            out GrayboxProductionRestorePlan3D plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (snapshots == null || instances == null)
            {
                error = "生产状态或建筑集合不能为空";
                return false;
            }

            var instanceById = new Dictionary<string, GrayboxBuildingInstance3D>(
                StringComparer.Ordinal);
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance == null ||
                    string.IsNullOrWhiteSpace(instance.StableInstanceId) ||
                    instanceById.ContainsKey(instance.StableInstanceId))
                {
                    error = "建筑实例为空、ID 为空或重复";
                    return false;
                }
                instanceById.Add(instance.StableInstanceId, instance);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<GrayboxProductionRestoreEntry3D>();
            var orphans = new List<GrayboxProductionPersistenceState3D>();
            for (var index = 0; index < snapshots.Count; index++)
            {
                GrayboxProductionPersistenceState3D snapshot = snapshots[index];
                if (snapshot == null ||
                    string.IsNullOrWhiteSpace(snapshot.StableInstanceId) ||
                    !seen.Add(snapshot.StableInstanceId) ||
                    !instanceById.TryGetValue(
                        snapshot.StableInstanceId,
                        out GrayboxBuildingInstance3D instance) ||
                    !GrayboxBuildingOperationalAccess3D.CanRetainState(instance))
                {
                    error = "生产状态为空、重复或引用了不可保留的建筑";
                    return false;
                }

                string buildingId = instance.Placement.Definition.Id.Value;
                if (!FormalProductionDefinitionCatalog.TryGetByBuildingId(
                        buildingId,
                        out FormalProductionDefinition definition))
                {
                    if (IsKnownBuilding(buildingId) ||
                        !ValidateOpaqueSnapshot(snapshot, out error))
                    {
                        if (string.IsNullOrEmpty(error))
                            error = "非生产建筑不能拥有生产状态";
                        return false;
                    }
                    orphans.Add(Clone(snapshot));
                    continue;
                }

                if (!FormalProductionDefinitionCatalog.TryGet(
                        snapshot.DefinitionId,
                        out _))
                {
                    if (!ValidateOpaqueSnapshot(snapshot, out error))
                        return false;
                    orphans.Add(Clone(snapshot));
                    continue;
                }

                if (!string.Equals(
                        definition.Id,
                        snapshot.DefinitionId,
                        StringComparison.Ordinal) ||
                    !stateById.TryGetValue(
                        snapshot.StableInstanceId,
                        out BuildingProductionState state) ||
                    !StateMatchesInstance(state, definition, instance) ||
                    !ValidateBinding(snapshot, instance, definition, world,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "生产定义或资源节点绑定与建筑不一致";
                    return false;
                }

                var candidate = new BuildingProductionState(
                    state.StableInstanceId,
                    definition,
                    state.BoundResourceNodeId,
                    state.BoundNodeX,
                    state.BoundNodeY);
                if (!candidate.TryRestoreForPersistence(
                        snapshot.Input,
                        snapshot.HasReservedInputs,
                        snapshot.ReservedInput,
                        snapshot.Output,
                        snapshot.ProgressSeconds,
                        snapshot.IsPlayerPaused,
                        out error))
                {
                    return false;
                }
                entries.Add(new GrayboxProductionRestoreEntry3D(
                    state,
                    Clone(snapshot)));
            }

            foreach (BuildingProductionState state in stateById.Values)
            {
                if (!seen.Contains(state.StableInstanceId))
                {
                    error = "已恢复的生产建筑缺少生产状态";
                    return false;
                }
            }

            entries.Sort((left, right) => string.CompareOrdinal(
                left.State.StableInstanceId,
                right.State.StableInstanceId));
            orphans.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
            plan = new GrayboxProductionRestorePlan3D(
                this,
                ComputePersistenceFingerprint(),
                synchronizationGeneration,
                entries,
                orphans);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            GrayboxProductionRestorePlan3D plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.IsConsumed)
            {
                error = "生产恢复计划无效或已使用";
                return false;
            }
            if (plan.ExpectedFingerprint != ComputePersistenceFingerprint())
            {
                error = "生产状态在恢复提交前已经改变";
                return false;
            }
            if (plan.ExpectedSynchronizationGeneration !=
                synchronizationGeneration)
            {
                error = "生产建筑集合在恢复提交前已经重新同步";
                return false;
            }
            for (var index = 0; index < plan.Entries.Count; index++)
            {
                BuildingProductionState prepared = plan.Entries[index].State;
                if (!stateById.TryGetValue(
                        prepared.StableInstanceId,
                        out BuildingProductionState current) ||
                    !ReferenceEquals(current, prepared))
                {
                    error = "生产建筑状态在恢复提交前已经替换";
                    return false;
                }
            }

            var rollback = new GrayboxProductionPersistenceState3D[
                plan.Entries.Count];
            for (var index = 0; index < plan.Entries.Count; index++)
                rollback[index] = CaptureState(plan.Entries[index].State);

            for (var index = 0; index < plan.Entries.Count; index++)
            {
                GrayboxProductionRestoreEntry3D entry = plan.Entries[index];
                GrayboxProductionPersistenceState3D snapshot = entry.Snapshot;
                if (entry.State.TryRestoreForPersistence(
                        snapshot.Input,
                        snapshot.HasReservedInputs,
                        snapshot.ReservedInput,
                        snapshot.Output,
                        snapshot.ProgressSeconds,
                        snapshot.IsPlayerPaused,
                        out error))
                {
                    continue;
                }

                for (var rollbackIndex = 0;
                     rollbackIndex < index;
                     rollbackIndex++)
                {
                    GrayboxProductionPersistenceState3D before =
                        rollback[rollbackIndex];
                    plan.Entries[rollbackIndex].State.TryRestoreForPersistence(
                        before.Input,
                        before.HasReservedInputs,
                        before.ReservedInput,
                        before.Output,
                        before.ProgressSeconds,
                        before.IsPlayerPaused,
                        out _);
                }
                return false;
            }

            orphanStateById.Clear();
            for (var index = 0; index < plan.Orphans.Count; index++)
            {
                GrayboxProductionPersistenceState3D orphan =
                    Clone(plan.Orphans[index]);
                if (stateById.TryGetValue(
                        orphan.StableInstanceId,
                        out BuildingProductionState liveState))
                {
                    RemoveState(orphan.StableInstanceId, liveState);
                }
                orphanStateById.Add(orphan.StableInstanceId, orphan);
            }
            plan.IsConsumed = true;
            error = string.Empty;
            return true;
        }

        public bool TryFinalizeEvacuationPayload(
            string stableInstanceId,
            GrayboxProductionEvacuationPayload3D payload)
        {
            if (!PayloadMatches(stableInstanceId, payload, out var state))
                return false;
            RemoveState(stableInstanceId, state);
            return true;
        }

        public bool TryDiscardEvacuationPayload(string stableInstanceId)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                !stateById.TryGetValue(
                    stableInstanceId,
                    out BuildingProductionState state))
            {
                return false;
            }

            RemoveState(stableInstanceId, state);
            return true;
        }

        public ProductionObservabilitySnapshot CaptureObservability(
            ulong revision,
            WorldMapModel world)
        {
            return ProductionObservabilitySnapshot.Capture(
                revision,
                readOnlyStates,
                world,
                ActiveWarehouseCount);
        }

        public ulong ComputeObservabilityContentHash(WorldMapModel world)
        {
            ulong value = 1469598103934665603ul;
            Mix(ref value, ActiveWarehouseCount);
            Mix(ref value, states.Count);
            for (var index = 0; index < states.Count; index++)
            {
                BuildingProductionState state = states[index];
                FormalProductionDefinition definition = state.Definition;
                string outputResourceId =
                    ProductionObservabilitySnapshot.ResolveOutputResourceId(
                        state,
                        world);
                Mix(ref value, state.StableInstanceId);
                Mix(ref value, definition.Id);
                Mix(ref value, definition.BuildingId);
                Mix(ref value, definition.DurationSeconds.GetHashCode());
                Mix(ref value, definition.InputResourceId);
                Mix(ref value, definition.InputAmount);
                Mix(ref value, definition.OutputAmount);
                Mix(ref value, definition.InputCapacity);
                Mix(ref value, definition.OutputCapacity);
                Mix(ref value, outputResourceId);
                Mix(ref value, string.IsNullOrEmpty(definition.InputResourceId)
                    ? 0
                    : state.Input.Get(definition.InputResourceId));
                Mix(ref value, string.IsNullOrEmpty(outputResourceId)
                    ? 0
                    : state.Output.Get(outputResourceId));
                Mix(ref value, state.ProgressSeconds.GetHashCode());
                Mix(ref value, state.HasReservedInputs);
                Mix(ref value, state.IsLogisticsConnected);
                Mix(ref value, state.IsPlayerPaused);
                Mix(ref value, (int)state.StopReason);
                Mix(ref value, state.BoundResourceNodeId);
                Mix(ref value, state.BoundNodeX);
                Mix(ref value, state.BoundNodeY);
                if (definition.UsesBoundResourceNode && world != null &&
                    state.BoundNodeX >= 0 && state.BoundNodeY >= 0 &&
                    state.BoundNodeX < world.Width &&
                    state.BoundNodeY < world.Height)
                {
                    WorldCell cell = world.Get(
                        state.BoundNodeX,
                        state.BoundNodeY);
                    Mix(ref value, cell.ResourceId);
                    Mix(ref value, Math.Max(0, cell.ResourceAmount));
                }
            }
            return value;
        }

        private static GrayboxProductionPersistenceState3D CaptureState(
            BuildingProductionState state)
        {
            return new GrayboxProductionPersistenceState3D(
                state.StableInstanceId,
                state.Definition.Id,
                state.Input.CapturePositiveAmounts(),
                state.HasReservedInputs,
                state.ReservedInputs,
                state.Output.CapturePositiveAmounts(),
                state.ProgressSeconds,
                state.IsPlayerPaused,
                state.BoundResourceNodeId,
                state.BoundNodeX,
                state.BoundNodeY);
        }

        private static GrayboxProductionPersistenceState3D Clone(
            GrayboxProductionPersistenceState3D source)
        {
            return new GrayboxProductionPersistenceState3D(
                source.StableInstanceId,
                source.DefinitionId,
                source.Input,
                source.HasReservedInputs,
                source.ReservedInput,
                source.Output,
                source.ProgressSeconds,
                source.IsPlayerPaused,
                source.BoundResourceNodeId,
                source.BoundNodeX,
                source.BoundNodeY);
        }

        private static ResourceAmount[] CopyAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ResourceAmount>();
            var values = new ResourceAmount[source.Count];
            for (var index = 0; index < source.Count; index++)
                values[index] = source[index];
            return values;
        }

        private bool PayloadMatches(
            string stableInstanceId,
            GrayboxProductionEvacuationPayload3D payload,
            out BuildingProductionState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                payload == null ||
                !string.Equals(
                    stableInstanceId,
                    payload.StableInstanceId,
                    StringComparison.Ordinal) ||
                !stateById.TryGetValue(stableInstanceId, out state) ||
                !ReferenceEquals(state, payload.SourceState) ||
                !string.Equals(
                    state.Definition.Id,
                    payload.DefinitionId,
                    StringComparison.Ordinal) ||
                state.ProgressSeconds != payload.ProgressSeconds ||
                state.HasReservedInputs != payload.HasReservedInputs ||
                state.IsPlayerPaused != payload.IsPlayerPaused ||
                !string.Equals(
                    state.BoundResourceNodeId,
                    payload.BoundResourceNodeId,
                    StringComparison.Ordinal) ||
                state.BoundNodeX != payload.BoundNodeX ||
                state.BoundNodeY != payload.BoundNodeY)
            {
                return false;
            }

            return AmountsEqual(
                    state.Input.CapturePositiveAmounts(),
                    payload.Input) &&
                AmountsEqual(
                    state.ReservedInputs,
                    payload.ReservedInput) &&
                AmountsEqual(
                    state.Output.CapturePositiveAmounts(),
                    payload.Output);
        }

        private ulong ComputePersistenceFingerprint()
        {
            ulong value = 1469598103934665603ul;
            GrayboxProductionPersistenceState3D[] captured =
                CaptureForPersistence();
            Mix(ref value, captured.Length);
            for (var index = 0; index < captured.Length; index++)
            {
                GrayboxProductionPersistenceState3D state = captured[index];
                Mix(ref value, state.StableInstanceId);
                Mix(ref value, state.DefinitionId);
                MixAmounts(ref value, state.Input);
                Mix(ref value, state.HasReservedInputs);
                MixAmounts(ref value, state.ReservedInput);
                MixAmounts(ref value, state.Output);
                Mix(ref value, state.ProgressSeconds.GetHashCode());
                Mix(ref value, state.IsPlayerPaused);
                Mix(ref value, state.BoundResourceNodeId);
                Mix(ref value, state.BoundNodeX);
                Mix(ref value, state.BoundNodeY);
            }
            return value;
        }

        private static void MixAmounts(
            ref ulong value,
            IReadOnlyList<ResourceAmount> amounts)
        {
            Mix(ref value, amounts?.Count ?? -1);
            if (amounts == null) return;
            for (var index = 0; index < amounts.Count; index++)
            {
                Mix(ref value, amounts[index].ResourceId);
                Mix(ref value, amounts[index].Amount);
            }
        }

        private static bool ValidateOpaqueSnapshot(
            GrayboxProductionPersistenceState3D snapshot,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(snapshot.DefinitionId) ||
                !ValidateAmounts(snapshot.Input) ||
                !ValidateAmounts(snapshot.ReservedInput) ||
                !ValidateAmounts(snapshot.Output) ||
                float.IsNaN(snapshot.ProgressSeconds) ||
                float.IsInfinity(snapshot.ProgressSeconds) ||
                snapshot.ProgressSeconds < 0f ||
                (!snapshot.HasReservedInputs &&
                 (snapshot.ReservedInput.Count != 0 ||
                  snapshot.ProgressSeconds != 0f)))
            {
                error = "缺失内容生产状态结构无效";
                return false;
            }
            bool hasBinding = !string.IsNullOrWhiteSpace(
                snapshot.BoundResourceNodeId);
            bool coordinatesMatch = hasBinding
                ? snapshot.BoundNodeX >= 0 && snapshot.BoundNodeY >= 0
                : snapshot.BoundNodeX == -1 && snapshot.BoundNodeY == -1;
            if (!coordinatesMatch)
            {
                error = "缺失内容生产状态的节点绑定无效";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool ValidateAmounts(IReadOnlyList<ResourceAmount> source)
        {
            if (source == null) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Count; index++)
            {
                ResourceAmount amount = source[index];
                if (string.IsNullOrWhiteSpace(amount.ResourceId) ||
                    amount.Amount < 0 || !ids.Add(amount.ResourceId))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateBinding(
            GrayboxProductionPersistenceState3D snapshot,
            GrayboxBuildingInstance3D instance,
            FormalProductionDefinition definition,
            WorldMapModel world,
            out string error)
        {
            ResourceNodeBinding binding = instance.BoundResourceNode;
            if (!definition.UsesBoundResourceNode)
            {
                bool empty = string.IsNullOrWhiteSpace(
                    snapshot.BoundResourceNodeId) &&
                    snapshot.BoundNodeX == -1 && snapshot.BoundNodeY == -1;
                error = empty ? string.Empty :
                    "普通生产建筑不能保存资源节点绑定";
                return empty;
            }
            if (!binding.IsValid ||
                !string.Equals(
                    binding.StableId,
                    snapshot.BoundResourceNodeId,
                    StringComparison.Ordinal) ||
                binding.X != snapshot.BoundNodeX ||
                binding.Y != snapshot.BoundNodeY ||
                world == null || binding.X < 0 || binding.Y < 0 ||
                binding.X >= world.Width || binding.Y >= world.Height)
            {
                error = "采矿生产状态与建筑节点绑定不一致";
                return false;
            }
            WorldCell cell = world.Get(binding.X, binding.Y);
            bool compatible = cell.HasResource &&
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    BuildingCatalog.MiningStation,
                    cell.ResourceId);
            error = compatible ? string.Empty : "采矿节点内容不兼容";
            return compatible;
        }

        private static bool IsKnownBuilding(string buildingId)
        {
            IReadOnlyList<BuildingDefinition> all = BuildingCatalog.All;
            for (var index = 0; index < all.Count; index++)
            {
                if (string.Equals(
                        all[index].Id.Value,
                        buildingId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool StateMatchesInstance(
            BuildingProductionState state,
            FormalProductionDefinition definition,
            GrayboxBuildingInstance3D instance)
        {
            if (!string.Equals(
                    state.Definition.Id,
                    definition.Id,
                    StringComparison.Ordinal))
            {
                return false;
            }
            ResourceNodeBinding binding = instance.BoundResourceNode;
            if (!definition.UsesBoundResourceNode)
            {
                return string.IsNullOrWhiteSpace(state.BoundResourceNodeId) &&
                    state.BoundNodeX == -1 && state.BoundNodeY == -1;
            }
            return binding.IsValid &&
                string.Equals(
                    state.BoundResourceNodeId,
                    binding.StableId,
                    StringComparison.Ordinal) &&
                state.BoundNodeX == binding.X &&
                state.BoundNodeY == binding.Y;
        }

        private static bool AmountsEqual(
            IReadOnlyList<ResourceAmount> left,
            IReadOnlyList<ResourceAmount> right)
        {
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!string.Equals(
                        left[index].ResourceId,
                        right[index].ResourceId,
                        StringComparison.Ordinal) ||
                    left[index].Amount != right[index].Amount)
                {
                    return false;
                }
            }
            return true;
        }

        private void RemoveState(
            string stableInstanceId,
            BuildingProductionState state)
        {
            stateById.Remove(stableInstanceId);
            retainedStateIds.Remove(stableInstanceId);
            states.Remove(state);
            runnableStates.Remove(state);
        }

        private static void Mix(ref ulong value, bool item)
        {
            Mix(ref value, item ? 1 : 0);
        }

        private static void Mix(ref ulong value, int item)
        {
            unchecked
            {
                value ^= (uint)item;
                value *= 1099511628211ul;
            }
        }

        private static void Mix(ref ulong value, string item)
        {
            Mix(ref value,
                string.IsNullOrEmpty(item) ? 0 : item.GetHashCode());
        }

    }
}
