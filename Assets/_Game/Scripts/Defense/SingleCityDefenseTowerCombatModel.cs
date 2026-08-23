using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Defense
{
    /// <summary>
    /// Deterministic local combat state shared by the three formal campaign
    /// towers. Building placement, logistics reach and campaign ownership stay
    /// with their existing authoritative systems; this model only owns the
    /// tower's consumable lease, damage remainder and target lock.
    /// </summary>
    public sealed class SingleCityDefenseTowerCombatModel
    {
        private const float TimeEpsilon = .00001f;
        private const float DamageBoundaryEpsilon = .00001f;

        private readonly DefenseTowerDefinition definition;
        private float activeConsumableSeconds;
        private float damageRemainder;
        private string targetStableEnemyId;

        public SingleCityDefenseTowerCombatModel(
            string stableInstanceId,
            string buildingId,
            float x,
            float z)
            : this(stableInstanceId, buildingId, x, z, 0)
        {
        }

        public SingleCityDefenseTowerCombatModel(
            string stableInstanceId,
            string buildingId,
            float x,
            float z,
            int initialConsumable)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId))
            {
                throw new ArgumentException(
                    "A stable tower instance ID is required.",
                    nameof(stableInstanceId));
            }
            if (!IsFinite(x) || !IsFinite(z))
                throw new ArgumentOutOfRangeException(nameof(x));
            if (!IsFormalTower(buildingId))
            {
                throw new ArgumentException(
                    "The building is not a formal campaign tower.",
                    nameof(buildingId));
            }

            definition = DefenseTowerCatalog.For(buildingId) ??
                throw new InvalidOperationException(
                    "The formal tower combat definition is missing.");
            if (definition.LocalCapacity <= 0 ||
                definition.SecondsPerConsumable <= 0f)
            {
                throw new InvalidOperationException(
                    "The formal tower combat definition is incomplete.");
            }

            StableInstanceId = stableInstanceId;
            X = x;
            Z = z;
            LocalConsumableAmount = Math.Min(
                definition.LocalCapacity,
                Math.Max(0, initialConsumable));
        }

        public string StableInstanceId { get; }
        public string BuildingId => definition.BuildingId;
        public DamageType DamageType => definition.DamageType;
        public float DamagePerSecond => definition.DamagePerSecond;
        public float Range => definition.Range;
        public string ConsumableId => definition.ConsumableId;
        public float SecondsPerConsumable =>
            definition.SecondsPerConsumable;
        public int LocalCapacity => definition.LocalCapacity;
        public int LocalConsumableAmount { get; private set; }
        public float ActiveConsumableSeconds => activeConsumableSeconds;
        public float DamageRemainder => damageRemainder;
        public string TargetStableEnemyId => targetStableEnemyId;
        public float X { get; }
        public float Z { get; }
        public bool IsLogisticsConnected { get; private set; } = true;
        public bool IsPlayerPaused { get; private set; }

        public SingleCityDefenseTowerPersistenceState CaptureForPersistence()
        {
            return new SingleCityDefenseTowerPersistenceState(
                StableInstanceId,
                BuildingId,
                X,
                Z,
                LocalConsumableAmount,
                activeConsumableSeconds,
                damageRemainder,
                targetStableEnemyId,
                IsLogisticsConnected,
                IsPlayerPaused);
        }

        public static bool TryCreateForPersistence(
            SingleCityDefenseTowerPersistenceState state,
            string expectedStableInstanceId,
            out SingleCityDefenseTowerCombatModel restored,
            out string error)
        {
            restored = null;
            if (state == null)
            {
                error = "防御塔持久化状态不能为空";
                return false;
            }
            if (string.IsNullOrWhiteSpace(expectedStableInstanceId) ||
                string.IsNullOrWhiteSpace(state.StableInstanceId) ||
                !string.Equals(
                    state.StableInstanceId,
                    expectedStableInstanceId,
                    StringComparison.Ordinal))
            {
                error = "防御塔稳定实例 ID 与恢复目标不一致";
                return false;
            }
            if (!IsFormalTower(state.BuildingId) ||
                DefenseTowerCatalog.For(state.BuildingId) == null)
            {
                error = "防御塔建筑 ID 不属于正式三塔";
                return false;
            }
            if (!IsFinite(state.X) || !IsFinite(state.Z))
            {
                error = "防御塔位置必须为有限数值";
                return false;
            }

            DefenseTowerDefinition restoredDefinition =
                DefenseTowerCatalog.For(state.BuildingId);
            if (state.LocalConsumableAmount < 0 ||
                state.LocalConsumableAmount >
                restoredDefinition.LocalCapacity)
            {
                error = "防御塔本地耗材超出正式容量";
                return false;
            }
            if (!IsFinite(state.ActiveConsumableSeconds) ||
                state.ActiveConsumableSeconds < 0f ||
                state.ActiveConsumableSeconds >
                restoredDefinition.SecondsPerConsumable)
            {
                error = "防御塔活动耗材租约超出有效范围";
                return false;
            }
            if (!IsFinite(state.DamageRemainder) ||
                state.DamageRemainder < 0f ||
                state.DamageRemainder >= 1f)
            {
                error = "防御塔伤害余量必须处于 [0, 1)";
                return false;
            }
            if (state.TargetStableEnemyId != null &&
                string.IsNullOrWhiteSpace(state.TargetStableEnemyId))
            {
                error = "防御塔目标锁定 ID 不能为空白字符串";
                return false;
            }

            var candidate = new SingleCityDefenseTowerCombatModel(
                state.StableInstanceId,
                state.BuildingId,
                state.X,
                state.Z,
                state.LocalConsumableAmount)
            {
                activeConsumableSeconds = state.ActiveConsumableSeconds,
                damageRemainder = state.DamageRemainder,
                targetStableEnemyId = state.TargetStableEnemyId,
                IsLogisticsConnected = state.IsLogisticsConnected,
                IsPlayerPaused = state.IsPlayerPaused,
            };
            restored = candidate;
            error = string.Empty;
            return true;
        }

        public void SetLogisticsConnected(bool connected)
        {
            IsLogisticsConnected = connected;
        }

        public void SetPlayerPaused(bool paused)
        {
            IsPlayerPaused = paused;
        }

        public int RefillFrom(
            CityResourceStorageModel cityStorage,
            bool connected)
        {
            SetLogisticsConnected(connected);
            return RefillFrom(cityStorage);
        }

        public int RefillFrom(CityResourceStorageModel cityStorage)
        {
            if (!IsLogisticsConnected || cityStorage == null)
                return 0;

            int missing = Math.Max(
                0,
                definition.LocalCapacity - LocalConsumableAmount);
            int moved = Math.Min(
                missing,
                cityStorage.GetNetworkAmount(definition.ConsumableId));
            if (moved <= 0)
                return 0;

            using (cityStorage.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Defense,
                           StableInstanceId)))
            {
                if (!cityStorage.TrySpendFromNetwork(
                        definition.ConsumableId,
                        moved))
                {
                    return 0;
                }
            }

            LocalConsumableAmount += moved;
            return moved;
        }

        public string AcquireTarget(
            IReadOnlyList<SingleCityDefenseEnemySnapshot> candidates)
        {
            SingleCityDefenseEnemySnapshot locked = FindValidLockedTarget(
                candidates);
            if (locked != null)
                return locked.StableId;

            SingleCityDefenseEnemySnapshot selected = null;
            float selectedDistanceSquared = float.MaxValue;
            if (candidates != null)
            {
                float rangeSquared = definition.Range * definition.Range;
                for (var index = 0; index < candidates.Count; index++)
                {
                    SingleCityDefenseEnemySnapshot candidate =
                        candidates[index];
                    if (candidate == null || candidate.CurrentHealth <= 0)
                        continue;

                    float distanceSquared = DistanceSquared(
                        candidate.X,
                        candidate.Z);
                    if (distanceSquared > rangeSquared)
                        continue;

                    if (selected == null ||
                        distanceSquared < selectedDistanceSquared ||
                        distanceSquared == selectedDistanceSquared &&
                        string.Compare(
                            candidate.StableId,
                            selected.StableId,
                            StringComparison.Ordinal) < 0)
                    {
                        selected = candidate;
                        selectedDistanceSquared = distanceSquared;
                    }
                }
            }

            targetStableEnemyId = selected?.StableId;
            return targetStableEnemyId;
        }

        public int Tick(
            float deltaSeconds,
            SingleCityDefenseCampaignModel campaign,
            bool globallyPaused)
        {
            if (globallyPaused || IsPlayerPaused || deltaSeconds <= 0f ||
                campaign == null || campaign.IsTerminal)
            {
                return 0;
            }

            float remainingSeconds = deltaSeconds;
            int totalAppliedDamage = 0;
            while (remainingSeconds > TimeEpsilon && !campaign.IsTerminal)
            {
                string targetId = campaign.AcquireTowerTarget(
                    targetStableEnemyId,
                    X,
                    Z,
                    definition.Range);
                targetStableEnemyId = targetId;
                if (string.IsNullOrEmpty(targetId))
                    break;

                if (activeConsumableSeconds <= TimeEpsilon)
                {
                    activeConsumableSeconds = 0f;
                    if (LocalConsumableAmount <= 0)
                        break;
                    LocalConsumableAmount--;
                    activeConsumableSeconds =
                        definition.SecondsPerConsumable;
                    campaign.RegisterConsumableSpent(
                        definition.ConsumableId,
                        1);
                }

                float activeSeconds = Math.Min(
                    remainingSeconds,
                    activeConsumableSeconds);
                activeSeconds = Math.Min(
                    activeSeconds,
                    SingleCityDefenseCampaignModel.FormalFixedStepSeconds);
                remainingSeconds -= activeSeconds;
                activeConsumableSeconds -= activeSeconds;
                if (activeConsumableSeconds < TimeEpsilon)
                    activeConsumableSeconds = 0f;

                float multiplier = campaign.ResolveTowerDamageMultiplier(
                    targetId,
                    definition.BuildingId);
                if (multiplier <= 0f)
                {
                    targetStableEnemyId = null;
                    continue;
                }
                damageRemainder += definition.DamagePerSecond * multiplier *
                    activeSeconds;
                int resolvedDamage = WholeDamage(ref damageRemainder);
                if (resolvedDamage <= 0)
                    continue;

                totalAppliedDamage += campaign.ApplyResolvedTowerDamage(
                    targetId,
                    definition.BuildingId,
                    resolvedDamage);
            }

            return totalAppliedDamage;
        }

        public int Tick(
            float deltaSeconds,
            DefenseEnemyCombatModel target,
            bool globallyPaused)
        {
            if (globallyPaused || IsPlayerPaused || deltaSeconds <= 0f ||
                target == null || target.IsDead || !IsInRange(target.X, target.Z))
            {
                return 0;
            }

            float remainingSeconds = deltaSeconds;
            int totalAppliedDamage = 0;
            while (remainingSeconds > TimeEpsilon && !target.IsDead)
            {
                if (activeConsumableSeconds <= TimeEpsilon)
                {
                    activeConsumableSeconds = 0f;
                    if (LocalConsumableAmount <= 0)
                        break;
                    LocalConsumableAmount--;
                    activeConsumableSeconds =
                        definition.SecondsPerConsumable;
                }

                float activeSeconds = Math.Min(
                    remainingSeconds,
                    activeConsumableSeconds);
                remainingSeconds -= activeSeconds;
                activeConsumableSeconds -= activeSeconds;
                if (activeConsumableSeconds < TimeEpsilon)
                    activeConsumableSeconds = 0f;

                damageRemainder += definition.DamagePerSecond * activeSeconds;
                int rawDamage = WholeDamage(ref damageRemainder);
                if (rawDamage <= 0)
                    continue;

                totalAppliedDamage += target.ApplyDamage(
                    rawDamage,
                    definition.DamageType);
            }

            return totalAppliedDamage;
        }

        private SingleCityDefenseEnemySnapshot FindValidLockedTarget(
            IReadOnlyList<SingleCityDefenseEnemySnapshot> candidates)
        {
            if (string.IsNullOrEmpty(targetStableEnemyId) ||
                candidates == null)
            {
                return null;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                SingleCityDefenseEnemySnapshot candidate = candidates[index];
                if (candidate != null &&
                    candidate.CurrentHealth > 0 &&
                    string.Equals(
                        candidate.StableId,
                        targetStableEnemyId,
                        StringComparison.Ordinal) &&
                    IsInRange(candidate.X, candidate.Z))
                {
                    return candidate;
                }
            }

            targetStableEnemyId = null;
            return null;
        }

        private bool IsInRange(float targetX, float targetZ)
        {
            float rangeSquared = definition.Range * definition.Range;
            return DistanceSquared(targetX, targetZ) <= rangeSquared;
        }

        private float DistanceSquared(float targetX, float targetZ)
        {
            float offsetX = targetX - X;
            float offsetZ = targetZ - Z;
            return offsetX * offsetX + offsetZ * offsetZ;
        }

        private static int WholeDamage(ref float remainder)
        {
            int whole = (int)remainder;
            float fraction = remainder - whole;
            if (fraction > 0f && 1f - fraction <= DamageBoundaryEpsilon)
            {
                whole++;
                fraction = 0f;
            }
            remainder = fraction;
            return whole;
        }

        private static bool IsFormalTower(string buildingId)
        {
            return string.Equals(
                       buildingId,
                       BuildingCatalog.MachineGunTurret.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       buildingId,
                       BuildingCatalog.LaserTower.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       buildingId,
                       BuildingCatalog.SporeTower.Id.Value,
                       StringComparison.Ordinal);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
