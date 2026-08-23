using System;
using System.Collections.Generic;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingHealthRuntime3D
    {
        private sealed class HealthState
        {
            public HealthState(int current, int maximum, bool destroyed)
            {
                Current = current;
                Maximum = maximum;
                Destroyed = destroyed;
            }

            public int Current { get; set; }
            public int Maximum { get; }
            public bool Destroyed { get; set; }
        }

        private readonly SortedDictionary<string, HealthState> states =
            new SortedDictionary<string, HealthState>(StringComparer.Ordinal);

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances)
        {
            if (instances == null) return;
            var liveIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance != null &&
                    !string.IsNullOrWhiteSpace(instance.StableInstanceId))
                {
                    liveIds.Add(instance.StableInstanceId);
                }
            }
            if (states.Count > 0)
            {
                var removedIds = new List<string>();
                foreach (string stableInstanceId in states.Keys)
                {
                    if (!liveIds.Contains(stableInstanceId))
                        removedIds.Add(stableInstanceId);
                }
                for (var index = 0; index < removedIds.Count; index++)
                    states.Remove(removedIds[index]);
            }

            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (!CanEnterCombat(instance) ||
                    states.ContainsKey(instance.StableInstanceId))
                {
                    continue;
                }

                int maximum = instance.Placement.Definition.MaximumHealth;
                states.Add(
                    instance.StableInstanceId,
                    new HealthState(maximum, maximum, destroyed: false));
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

                int maximum = instance.Placement.Definition.MaximumHealth;
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
                        entry.isDestroyed));
            }

            states.Clear();
            foreach (KeyValuePair<string, HealthState> item in candidate)
                states.Add(item.Key, item.Value);
            error = string.Empty;
            return true;
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
    }
}
