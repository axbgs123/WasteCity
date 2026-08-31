using System;
using System.Collections.Generic;
using WasteCity.Building;
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
                bool isWall)
            {
                Current = current;
                Maximum = maximum;
                Destroyed = destroyed;
                IsWall = isWall;
            }

            public int Current { get; set; }
            public int Maximum { get; set; }
            public bool Destroyed { get; set; }
            public bool IsWall { get; set; }
            public float TissueRemainder { get; set; }
            public float CarapaceClock { get; set; }
        }

        private readonly SortedDictionary<string, HealthState> states =
            new SortedDictionary<string, HealthState>(StringComparer.Ordinal);
        private readonly HashSet<string> synchronizedIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removedIds = new List<string>();

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
                            IsWall(instance)));
                    continue;
                }
                existing.IsWall = IsWall(instance);
                if (existing.Maximum == maximum) continue;
                int missing = Math.Max(0, existing.Maximum - existing.Current);
                existing.Maximum = maximum;
                existing.Current = Math.Max(0, maximum - missing);
                existing.Destroyed = existing.Current == 0;
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
            appliedDamage = 0;
            destroyedNow = false;
            if (damage <= 0 || string.IsNullOrWhiteSpace(stableInstanceId) ||
                !states.TryGetValue(stableInstanceId, out HealthState state))
            {
                return false;
            }

            if (state.Destroyed) return true;
            appliedDamage = Math.Min(damage, state.Current);
            state.Current -= appliedDamage;
            if (state.Current == 0)
            {
                state.Destroyed = true;
                destroyedNow = true;
            }
            return true;
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
                        IsWall(instance)));
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
            if (state == null || state.Destroyed ||
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
    }
}
