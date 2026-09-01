using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingHealthRuntime3D
    {
        private sealed class HealthState
        {
            public HealthState(
                int current,
                int maximum,
                bool destroyed,
                bool isWall,
                string buildingId,
                float x,
                float z)
            {
                Current = current;
                Maximum = maximum;
                Destroyed = destroyed;
                IsWall = isWall;
                BuildingId = buildingId ?? string.Empty;
                X = x;
                Z = z;
                Repair = new AutomatedRepairModel(
                    SingleCityDefenseTechnologyRules
                        .AutomatedRepairPeriodSeconds,
                    SingleCityDefenseTechnologyRules
                        .AutomatedRepairAmount);
                ShieldPulse = new ShieldPulseModel(
                    SingleCityDefenseTechnologyRules.ShieldPeriodSeconds);
                CanRunLocally = true;
                IsLogisticsConnected = true;
            }

            public int Current { get; set; }
            public int Maximum { get; set; }
            public bool Destroyed { get; set; }
            public bool IsWall { get; set; }
            public string BuildingId { get; set; }
            public float X { get; set; }
            public float Z { get; set; }
            public int Shield { get; set; }
            public float TissueRemainder { get; set; }
            public float CarapaceClock { get; set; }
            public AutomatedRepairModel Repair { get; set; }
            public ShieldPulseModel ShieldPulse { get; set; }
            public bool CanRunLocally { get; set; }
            public bool IsLogisticsConnected { get; set; }
            public bool IsPlayerPaused { get; set; }
        }

        private readonly SortedDictionary<string, HealthState> states =
            new SortedDictionary<string, HealthState>(StringComparer.Ordinal);
        private readonly HashSet<string> synchronizedIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removedIds = new List<string>();
        private float wallPhysicalDamageMultiplier = 1f;
        private bool automatedRepair;
        private bool mindShield;

        public GrayboxBuildingTechnologySnapshot3D TechnologySnapshot
        {
            get
            {
                var snapshots = new GrayboxBuildingTechnologyStateSnapshot3D[
                    states.Count];
                var index = 0;
                foreach (KeyValuePair<string, HealthState> item in states)
                {
                    HealthState state = item.Value;
                    snapshots[index++] =
                        new GrayboxBuildingTechnologyStateSnapshot3D(
                            item.Key,
                            state.BuildingId,
                            state.Current,
                            state.Maximum,
                            state.Shield,
                            state.Destroyed,
                            state.TissueRemainder,
                            state.CarapaceClock,
                            state.Repair.Clock,
                            state.ShieldPulse.Clock);
                }
                return new GrayboxBuildingTechnologySnapshot3D(snapshots);
            }
        }

        public void ConfigureTechnologySupport(
            float wallPhysicalDamageMultiplier,
            bool automatedRepair,
            bool mindShield)
        {
            this.wallPhysicalDamageMultiplier = Math.Max(
                0f, Math.Min(1f, wallPhysicalDamageMultiplier));
            this.automatedRepair = automatedRepair;
            this.mindShield = mindShield;
        }

        public bool TryClearTechnologyFixturesForDevelopment()
        {
            bool changed = false;
            foreach (KeyValuePair<string, HealthState> item in states)
            {
                HealthState state = item.Value;
                changed |= state.Shield > 0 || state.TissueRemainder > 0f ||
                    state.CarapaceClock > 0f ||
                    automatedRepair && IsAutomatedRepairBay(state) ||
                    mindShield && IsShieldGenerator(state);
                state.Shield = 0;
                state.TissueRemainder = 0f;
                state.CarapaceClock = 0f;
                state.Repair = new AutomatedRepairModel(
                    SingleCityDefenseTechnologyRules
                        .AutomatedRepairPeriodSeconds,
                    SingleCityDefenseTechnologyRules
                        .AutomatedRepairAmount);
                state.ShieldPulse = new ShieldPulseModel(
                    SingleCityDefenseTechnologyRules.ShieldPeriodSeconds);
            }
            return changed;
        }

        public bool TryRestoreTechnologyState(
            IReadOnlyList<GrayboxBuildingTechnologyStateSnapshot3D> restored,
            out string error)
        {
            if (restored == null)
            {
                error = "建筑科技状态不完整";
                return false;
            }
            var byId = new Dictionary<string,
                GrayboxBuildingTechnologyStateSnapshot3D>(StringComparer.Ordinal);
            for (var index = 0; index < restored.Count; index++)
            {
                GrayboxBuildingTechnologyStateSnapshot3D item = restored[index];
                if (item == null || !states.ContainsKey(item.StableInstanceId) ||
                    !byId.TryAdd(item.StableInstanceId, item) ||
                    item.Shield < 0 ||
                    item.Shield > SingleCityDefenseTechnologyRules.MaximumShield ||
                    item.TissueRemainder < 0f || item.TissueRemainder >= 1f ||
                    item.CarapaceClock < 0f || item.CarapaceClock >= 5f ||
                    item.RepairClock < 0f || item.RepairClock >=
                        SingleCityDefenseTechnologyRules
                            .AutomatedRepairPeriodSeconds ||
                    item.ShieldPulseClock < 0f || item.ShieldPulseClock >=
                        SingleCityDefenseTechnologyRules.ShieldPeriodSeconds)
                {
                    error = "建筑科技状态记录无效";
                    return false;
                }
            }
            foreach (KeyValuePair<string, HealthState> pair in states)
            {
                HealthState state = pair.Value;
                if (!byId.TryGetValue(pair.Key, out var item))
                {
                    state.Shield = 0;
                    state.TissueRemainder = 0f;
                    state.CarapaceClock = 0f;
                    continue;
                }
                state.Shield = item.Shield;
                state.TissueRemainder = item.TissueRemainder;
                state.CarapaceClock = item.CarapaceClock;
                if (!state.Repair.TryRestoreClock(item.RepairClock) ||
                    !state.ShieldPulse.TryRestoreClock(item.ShieldPulseClock))
                {
                    error = "建筑维修或护盾周期状态无效";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances)
        {
            Synchronize(instances, alloyArmorCompleted: false);
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            bool alloyArmorCompleted)
        {
            Synchronize(
                instances,
                alloyArmorCompleted ? 1.3f : 1f);
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            float buildingHealthMultiplier)
        {
            if (instances == null) return;
            synchronizedIds.Clear();
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance != null &&
                    !string.IsNullOrWhiteSpace(instance.StableInstanceId))
                {
                    synchronizedIds.Add(instance.StableInstanceId);
                }
            }
            if (states.Count > 0)
            {
                removedIds.Clear();
                foreach (string stableInstanceId in states.Keys)
                {
                    if (!synchronizedIds.Contains(stableInstanceId))
                        removedIds.Add(stableInstanceId);
                }
                for (var index = 0; index < removedIds.Count; index++)
                    states.Remove(removedIds[index]);
            }

            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (!CanEnterCombat(instance))
                {
                    continue;
                }

                int maximum = ResolveMaximumHealth(
                    instance.Placement.Definition.MaximumHealth,
                    buildingHealthMultiplier);
                if (!states.TryGetValue(
                        instance.StableInstanceId,
                        out HealthState existing))
                {
                    states.Add(instance.StableInstanceId,
                        new HealthState(
                            maximum,
                            maximum,
                            false,
                            IsWall(instance),
                            instance.Placement.Definition.Id.Value,
                            instance.Placement.X,
                            instance.Placement.Y));
                    continue;
                }
                existing.IsWall = IsWall(instance);
                existing.BuildingId =
                    instance.Placement.Definition.Id.Value;
                existing.X = instance.Placement.X;
                existing.Z = instance.Placement.Y;
                if (existing.Maximum == maximum) continue;
                int missing = Math.Max(0, existing.Maximum - existing.Current);
                existing.Maximum = maximum;
                existing.Current = Math.Max(0, maximum - missing);
                existing.Destroyed = existing.Current == 0;
            }
        }

        public void SynchronizeTechnologyOperationalState(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            Func<string, bool> isPlayerPaused)
        {
            foreach (KeyValuePair<string, HealthState> pair in states)
            {
                pair.Value.CanRunLocally = false;
                pair.Value.IsLogisticsConnected = false;
                pair.Value.IsPlayerPaused = false;
            }
            if (instances == null) return;
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance == null || !states.TryGetValue(
                        instance.StableInstanceId,
                        out HealthState state))
                {
                    continue;
                }
                state.CanRunLocally =
                    GrayboxBuildingOperationalAccess3D.CanRunLocally(
                        instance,
                        cityMode);
                state.IsLogisticsConnected = state.CanRunLocally &&
                    GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                        instance,
                        cityMode,
                        cityX,
                        cityY,
                        groundRadius);
                state.IsPlayerPaused =
                    isPlayerPaused?.Invoke(instance.StableInstanceId) == true;
            }
        }

        public bool TryGetHealth(
            string stableInstanceId,
            out int currentHealth,
            out int maximumHealth,
            out bool isDestroyed)
        {
            if (!string.IsNullOrWhiteSpace(stableInstanceId) &&
                states.TryGetValue(stableInstanceId, out HealthState state))
            {
                currentHealth = state.Current;
                maximumHealth = state.Maximum;
                isDestroyed = state.Destroyed;
                return true;
            }

            currentHealth = 0;
            maximumHealth = 0;
            isDestroyed = false;
            return false;
        }

        public bool TryApplyDamage(
            string stableInstanceId,
            int damage,
            out int appliedDamage,
            out bool destroyedNow)
        {
            return TryApplyDamage(
                stableInstanceId,
                damage,
                DamageType.Physical,
                out appliedDamage,
                out destroyedNow);
        }

        public bool TryApplyDamage(
            string stableInstanceId,
            int damage,
            DamageType damageType,
            out int appliedDamage,
            out bool destroyedNow)
        {
            appliedDamage = 0;
            destroyedNow = false;
            if (damage <= 0 || string.IsNullOrWhiteSpace(stableInstanceId) ||
                !states.TryGetValue(stableInstanceId, out HealthState state))
            {
                return false;
            }

            if (state.Destroyed) return true;
            HealthModel health = CreateHealthModel(state);
            if (state.IsWall)
            {
                health.SetPhysicalDamagePercent((int)Math.Round(
                    wallPhysicalDamageMultiplier * 100f,
                    MidpointRounding.AwayFromZero));
            }
            appliedDamage = health.Apply(
                damage,
                damageType,
                ArmorType.Light);
            state.Current = health.Current;
            state.Shield = health.Shield;
            if (state.Current == 0)
            {
                state.Destroyed = true;
                destroyedNow = true;
            }
            return true;
        }

        public int TryGrantShield(
            string stableInstanceId,
            int amount,
            int maximumShield)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(stableInstanceId) ||
                !states.TryGetValue(stableInstanceId, out HealthState state) ||
                state.Destroyed)
            {
                return 0;
            }
            HealthModel health = CreateHealthModel(state);
            int granted = health.GrantShield(amount, maximumShield);
            state.Shield = health.Shield;
            return granted;
        }

        public int AdvanceTechnologySupport(
            float deltaSeconds,
            bool paused,
            float coreX,
            float coreZ,
            out int coreShieldGrant)
        {
            coreShieldGrant = 0;
            if (paused || deltaSeconds <= 0f) return 0;
            var changed = 0;
            foreach (KeyValuePair<string, HealthState> sourceItem in states)
            {
                HealthState source = sourceItem.Value;
                if (!IsTechnologyOperational(source)) continue;
                if (automatedRepair && IsAutomatedRepairBay(source) &&
                    source.Repair.Tick(deltaSeconds))
                {
                    foreach (KeyValuePair<string, HealthState> targetItem in
                             states)
                    {
                        HealthState target = targetItem.Value;
                        if (target.Destroyed || !InRange(
                                source,
                                target.X,
                                target.Z,
                                SingleCityDefenseTechnologyRules
                                    .SupportRadius))
                        {
                            continue;
                        }
                        HealthModel health = CreateHealthModel(target);
                        int healed = source.Repair.Repair(health);
                        target.Current = health.Current;
                        changed += healed;
                    }
                }
                if (!mindShield || !IsShieldGenerator(source) ||
                    !source.ShieldPulse.Tick(deltaSeconds))
                {
                    continue;
                }
                foreach (KeyValuePair<string, HealthState> targetItem in
                         states)
                {
                    HealthState target = targetItem.Value;
                    if (target.Destroyed || !InRange(source, target.X,
                            target.Z,
                            SingleCityDefenseTechnologyRules.SupportRadius))
                    {
                        continue;
                    }
                    HealthModel health = CreateHealthModel(target);
                    int granted = health.GrantShield(
                        SingleCityDefenseTechnologyRules
                            .ShieldRechargeAmount,
                        SingleCityDefenseTechnologyRules.MaximumShield);
                    target.Shield = health.Shield;
                    changed += granted;
                }
                if (InRange(
                        source,
                        coreX,
                        coreZ,
                        SingleCityDefenseTechnologyRules.SupportRadius))
                {
                    coreShieldGrant += SingleCityDefenseTechnologyRules
                        .ShieldRechargeAmount;
                }
            }
            return changed;
        }

        public GrayboxElixirBuildingHealthSnapshot3D[]
            CaptureElixirHealingTargets()
        {
            var result = new GrayboxElixirBuildingHealthSnapshot3D[
                states.Count];
            var index = 0;
            foreach (KeyValuePair<string, HealthState> item in states)
            {
                HealthState state = item.Value;
                result[index++] =
                    new GrayboxElixirBuildingHealthSnapshot3D(
                        state.Destroyed ? 0 : state.Current,
                        state.Maximum);
            }
            return result;
        }

        public int HealAll(int amount)
        {
            if (amount <= 0) return 0;
            var healed = 0;
            foreach (KeyValuePair<string, HealthState> item in states)
            {
                HealthState state = item.Value;
                if (state.Destroyed || state.Current >= state.Maximum)
                    continue;
                int accepted = Math.Min(
                    amount,
                    state.Maximum - state.Current);
                state.Current += accepted;
                healed += accepted;
            }
            return healed;
        }

        public bool TryAdvanceRegeneration(
            string stableInstanceId,
            float deltaSeconds,
            bool tissueRegeneration,
            bool carapaceGrowth,
            ResourceInventory inventory,
            out int healed)
        {
            healed = 0;
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                !states.TryGetValue(stableInstanceId, out HealthState state))
            {
                return false;
            }
            healed = AdvanceRegeneration(
                state,
                deltaSeconds,
                tissueRegeneration,
                carapaceGrowth,
                inventory,
                cityStorage: null);
            return true;
        }

        public int AdvanceRegeneration(
            float deltaSeconds,
            bool tissueRegeneration,
            bool carapaceGrowth,
            CityResourceStorageModel cityStorage)
        {
            if (deltaSeconds <= 0f ||
                !tissueRegeneration && !carapaceGrowth)
            {
                return 0;
            }
            var healed = 0;
            foreach (KeyValuePair<string, HealthState> item in states)
            {
                healed += AdvanceRegeneration(
                    item.Value,
                    deltaSeconds,
                    tissueRegeneration,
                    carapaceGrowth,
                    inventory: null,
                    cityStorage: cityStorage);
            }
            return healed;
        }

        public FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] Capture()
        {
            var result =
                new FormalThreeDDefenseCampaignBuildingHealthStateSaveData[
                    states.Count];
            var index = 0;
            foreach (KeyValuePair<string, HealthState> item in states)
            {
                result[index++] =
                    new FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                    {
                        stableInstanceId = item.Key,
                        currentHealth = item.Value.Current,
                        isDestroyed = item.Value.Destroyed,
                    };
            }
            return result;
        }

        public bool TryRestore(
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] restored,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out string error)
        {
            return TryRestore(
                restored,
                instances,
                alloyArmorCompleted: false,
                out error);
        }

        public bool TryRestore(
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] restored,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            bool alloyArmorCompleted,
            out string error)
        {
            return TryRestore(
                restored,
                instances,
                alloyArmorCompleted ? 1.3f : 1f,
                out error);
        }

        public bool TryRestore(
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] restored,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            float buildingHealthMultiplier,
            out string error)
        {
            if (restored == null || instances == null)
            {
                error = "建筑耐久恢复数据不完整";
                return false;
            }

            var definitions = new Dictionary<string, GrayboxBuildingInstance3D>(
                StringComparer.Ordinal);
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance == null ||
                    string.IsNullOrWhiteSpace(instance.StableInstanceId) ||
                    instance.Placement?.Definition == null ||
                    definitions.ContainsKey(instance.StableInstanceId))
                {
                    error = "建筑耐久引用的建筑实例无效或重复";
                    return false;
                }
                definitions.Add(instance.StableInstanceId, instance);
            }

            var candidate = new SortedDictionary<string, HealthState>(
                StringComparer.Ordinal);
            for (var index = 0; index < restored.Length; index++)
            {
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData entry =
                    restored[index];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.stableInstanceId) ||
                    candidate.ContainsKey(entry.stableInstanceId) ||
                    !definitions.TryGetValue(
                        entry.stableInstanceId,
                        out GrayboxBuildingInstance3D instance))
                {
                    error = "建筑耐久记录为空、重复或引用未知建筑";
                    return false;
                }

                int maximum = ResolveMaximumHealth(
                    instance.Placement.Definition.MaximumHealth,
                    buildingHealthMultiplier);
                if (entry.currentHealth < 0 ||
                    entry.currentHealth > maximum ||
                    entry.isDestroyed != (entry.currentHealth == 0))
                {
                    error = "建筑耐久与摧毁状态不一致";
                    return false;
                }
                candidate.Add(
                    entry.stableInstanceId,
                    new HealthState(
                        entry.currentHealth,
                        maximum,
                        entry.isDestroyed,
                        IsWall(instance),
                        instance.Placement.Definition.Id.Value,
                        instance.Placement.X,
                        instance.Placement.Y));
            }

            states.Clear();
            foreach (KeyValuePair<string, HealthState> item in candidate)
                states.Add(item.Key, item.Value);
            error = string.Empty;
            return true;
        }

        private static int ResolveMaximumHealth(
            int baseHealth,
            float multiplier)
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    Math.Max(1, baseHealth) * Math.Max(1f, multiplier),
                    MidpointRounding.AwayFromZero));
        }

        private static bool CanEnterCombat(GrayboxBuildingInstance3D instance)
        {
            return instance != null &&
                !string.IsNullOrWhiteSpace(instance.StableInstanceId) &&
                instance.Placement?.Definition != null &&
                instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned &&
                !instance.IsEvacuationLocked;
        }

        private static bool IsWall(GrayboxBuildingInstance3D instance)
        {
            return string.Equals(
                instance?.Placement?.Definition?.Id.Value,
                BuildingCatalog.Wall.Id.Value,
                StringComparison.Ordinal);
        }

        private static int AdvanceRegeneration(
            HealthState state,
            float deltaSeconds,
            bool tissueRegeneration,
            bool carapaceGrowth,
            ResourceInventory inventory,
            CityResourceStorageModel cityStorage)
        {
            if (!IsTechnologyOperational(state) ||
                state.Current >= state.Maximum)
            {
                return 0;
            }

            float delta = Math.Max(0f, deltaSeconds);
            var healed = 0;
            if (tissueRegeneration)
            {
                state.TissueRemainder += delta;
                int amount = (int)Math.Floor(state.TissueRemainder);
                if (amount > 0)
                {
                    healed += Heal(state, amount);
                    state.TissueRemainder -= amount;
                }
            }

            if (!state.IsWall || !carapaceGrowth ||
                state.Current >= state.Maximum)
            {
                return healed;
            }

            state.CarapaceClock += delta;
            while (state.CarapaceClock + .0001f >= 5f &&
                   state.Current < state.Maximum)
            {
                bool spent = inventory != null
                    ? inventory.TrySpend(ResourceIds.Biomass, 1)
                    : cityStorage != null && cityStorage.TrySpendFromNetwork(
                        ResourceIds.Biomass, 1);
                if (!spent)
                {
                    state.CarapaceClock = 5f;
                    break;
                }
                state.CarapaceClock = Math.Max(
                    0f,
                    state.CarapaceClock - 5f);
                healed += Heal(state, 10);
            }
            return healed;
        }

        private static int Heal(HealthState state, int amount)
        {
            int accepted = Math.Min(
                Math.Max(0, amount),
                state.Maximum - state.Current);
            state.Current += accepted;
            return accepted;
        }

        private static HealthModel CreateHealthModel(HealthState state)
        {
            var health = new HealthModel(state.Maximum);
            health.Restore(state.Current, state.Shield);
            return health;
        }

        private static bool IsAutomatedRepairBay(HealthState state)
        {
            return string.Equals(
                state.BuildingId,
                BuildingCatalog.AutomatedRepairBay.Id.Value,
                StringComparison.Ordinal);
        }

        private static bool IsShieldGenerator(HealthState state)
        {
            return string.Equals(
                state.BuildingId,
                BuildingCatalog.ShieldGenerator.Id.Value,
                StringComparison.Ordinal);
        }

        private static bool IsTechnologyOperational(HealthState state)
        {
            return state != null && !state.Destroyed &&
                state.CanRunLocally && state.IsLogisticsConnected &&
                !state.IsPlayerPaused;
        }

        private static bool InRange(
            HealthState source,
            float x,
            float z,
            float range)
        {
            float offsetX = source.X - x;
            float offsetZ = source.Z - z;
            return offsetX * offsetX + offsetZ * offsetZ <= range * range;
        }
    }

    public sealed class GrayboxBuildingTechnologyStateSnapshot3D
    {
        internal GrayboxBuildingTechnologyStateSnapshot3D(
            string stableInstanceId,
            string buildingId,
            int currentHealth,
            int maximumHealth,
            int shield,
            bool destroyed,
            float tissueRemainder = 0f,
            float carapaceClock = 0f,
            float repairClock = 0f,
            float shieldPulseClock = 0f)
        {
            StableInstanceId = stableInstanceId ?? string.Empty;
            BuildingId = buildingId ?? string.Empty;
            CurrentHealth = Math.Max(0, currentHealth);
            MaximumHealth = Math.Max(1, maximumHealth);
            Shield = Math.Max(0, shield);
            Destroyed = destroyed;
            TissueRemainder = Math.Max(0f, tissueRemainder);
            CarapaceClock = Math.Max(0f, carapaceClock);
            RepairClock = Math.Max(0f, repairClock);
            ShieldPulseClock = Math.Max(0f, shieldPulseClock);
        }

        public string StableInstanceId { get; }
        public string BuildingId { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public int Shield { get; }
        public bool Destroyed { get; }
        public float TissueRemainder { get; }
        public float CarapaceClock { get; }
        public float RepairClock { get; }
        public float ShieldPulseClock { get; }
    }

    public sealed class GrayboxBuildingTechnologySnapshot3D
    {
        internal GrayboxBuildingTechnologySnapshot3D(
            GrayboxBuildingTechnologyStateSnapshot3D[] buildings)
        {
            Buildings = Array.AsReadOnly(
                buildings ??
                Array.Empty<GrayboxBuildingTechnologyStateSnapshot3D>());
        }

        public IReadOnlyList<GrayboxBuildingTechnologyStateSnapshot3D>
            Buildings { get; }
    }
}
