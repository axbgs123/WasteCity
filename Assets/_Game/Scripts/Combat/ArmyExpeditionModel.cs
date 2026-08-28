using System;
using System.Collections.Generic;
using WasteCity.Economy;

namespace WasteCity.Combat
{
    public enum ArmyExpeditionStatus
    {
        Idle,
        Outbound,
        Returning,
        Returned,
        Retreated,
        Completed,
    }

    public readonly struct ArmyExpeditionUnit
    {
        public ArmyExpeditionUnit(
            string stableUnitId,
            string definitionId,
            int currentHealth,
            bool isActive)
        {
            StableUnitId = stableUnitId;
            DefinitionId = definitionId;
            CurrentHealth = Math.Max(0, currentHealth);
            IsActive = isActive;
        }

        public string StableUnitId { get; }
        public string DefinitionId { get; }
        public int CurrentHealth { get; }
        public bool IsActive { get; }
    }

    public sealed class ArmyExpeditionEncounter
    {
        internal ArmyExpeditionEncounter(string[] enemyDefinitionIds)
        {
            EnemyDefinitionIds = enemyDefinitionIds ?? Array.Empty<string>();
        }

        public string[] EnemyDefinitionIds { get; }
    }

    public sealed class ArmyExpeditionResolution
    {
        internal ArmyExpeditionResolution(
            bool victory,
            float armyPower,
            int enemyPower,
            string[] casualtyStableUnitIds)
        {
            Victory = victory;
            ArmyPower = armyPower;
            EnemyPower = enemyPower;
            CasualtyStableUnitIds = casualtyStableUnitIds ??
                Array.Empty<string>();
        }

        public bool Victory { get; }
        public float ArmyPower { get; }
        public int EnemyPower { get; }
        public string[] CasualtyStableUnitIds { get; }
    }

    public sealed class ArmyExpeditionPersistenceSnapshot
    {
        public ArmyExpeditionPersistenceSnapshot(
            ArmyExpeditionStatus status,
            string sessionId,
            int targetX,
            int targetY,
            int expeditionOrdinal,
            float outboundDurationSeconds,
            float returnDurationSeconds,
            float remainingSeconds,
            ArmyExpeditionUnit[] units,
            bool leaderHealthy,
            bool retreating,
            string[] enemyDefinitionIds,
            bool hasResolution,
            float armyPower,
            int enemyPower,
            string[] casualtyStableUnitIds,
            ResourceAmount[] pendingLoot,
            bool victory = false)
        {
            Status = status;
            SessionId = sessionId;
            TargetX = targetX;
            TargetY = targetY;
            ExpeditionOrdinal = expeditionOrdinal;
            OutboundDurationSeconds = outboundDurationSeconds;
            ReturnDurationSeconds = returnDurationSeconds;
            RemainingSeconds = remainingSeconds;
            Units = Clone(units);
            LeaderHealthy = leaderHealthy;
            Retreating = retreating;
            EnemyDefinitionIds = Clone(enemyDefinitionIds);
            HasResolution = hasResolution;
            Victory = victory;
            ArmyPower = armyPower;
            EnemyPower = enemyPower;
            CasualtyStableUnitIds = Clone(casualtyStableUnitIds);
            PendingLoot = Clone(pendingLoot);
        }

        public ArmyExpeditionStatus Status { get; }
        public string SessionId { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public int ExpeditionOrdinal { get; }
        public float OutboundDurationSeconds { get; }
        public float ReturnDurationSeconds { get; }
        public float RemainingSeconds { get; }
        public ArmyExpeditionUnit[] Units { get; }
        public bool LeaderHealthy { get; }
        public bool Retreating { get; }
        public string[] EnemyDefinitionIds { get; }
        public bool HasResolution { get; }
        public bool Victory { get; }
        public float ArmyPower { get; }
        public int EnemyPower { get; }
        public string[] CasualtyStableUnitIds { get; }
        public ResourceAmount[] PendingLoot { get; }

        private static T[] Clone<T>(T[] values)
        {
            return values == null ? null : (T[])values.Clone();
        }
    }

    public sealed class ArmyExpeditionRestorePlan
    {
        internal ArmyExpeditionRestorePlan(
            ArmyExpeditionModel owner,
            ulong expectedGeneration,
            ArmyExpeditionPersistenceSnapshot snapshot)
        {
            Owner = owner;
            ExpectedGeneration = expectedGeneration;
            Snapshot = snapshot;
        }

        internal ArmyExpeditionModel Owner { get; }
        internal ulong ExpectedGeneration { get; }
        internal ArmyExpeditionPersistenceSnapshot Snapshot { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class ArmyExpeditionModel
    {
        private const float BaseDurationSeconds = 45f;
        private const float SecondsPerPathCost = 1.5f;
        private const float Epsilon = .00001f;

        private string sessionId;
        private int targetX;
        private int targetY;
        private int expeditionOrdinal;
        private ArmyExpeditionUnit[] units =
            Array.Empty<ArmyExpeditionUnit>();
        private bool leaderHealthy;
        private bool retreating;
        private ResourceAmount[] pendingLoot =
            Array.Empty<ResourceAmount>();
        private ulong persistenceGeneration;

        public ArmyExpeditionStatus Status { get; private set; } =
            ArmyExpeditionStatus.Idle;
        public float OutboundDurationSeconds { get; private set; }
        public float ReturnDurationSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public ArmyExpeditionEncounter Encounter { get; private set; }
        public ArmyExpeditionResolution Resolution { get; private set; }
        public IReadOnlyList<ResourceAmount> PendingLoot =>
            (ResourceAmount[])pendingLoot.Clone();

        public bool TryStart(
            string sessionId,
            int targetX,
            int targetY,
            int expeditionOrdinal,
            float pathCost,
            IReadOnlyList<ArmyExpeditionUnit> units,
            bool leaderHealthy)
        {
            if (Status != ArmyExpeditionStatus.Idle ||
                string.IsNullOrWhiteSpace(sessionId) ||
                expeditionOrdinal <= 0 || !IsFinite(pathCost) ||
                pathCost < 0f || units == null || units.Count == 0)
            {
                return false;
            }

            var valid = new List<ArmyExpeditionUnit>(units.Count);
            for (var index = 0; index < units.Count; index++)
            {
                ArmyExpeditionUnit unit = units[index];
                ArmyUnitDefinition definition = ArmyUnitCatalog.Find(
                    unit.DefinitionId);
                if (!unit.IsActive || unit.CurrentHealth <= 0 ||
                    string.IsNullOrWhiteSpace(unit.StableUnitId) ||
                    definition == null ||
                    unit.CurrentHealth > definition.MaximumHealth)
                {
                    continue;
                }
                valid.Add(unit);
            }
            if (valid.Count == 0) return false;
            valid.Sort((left, right) => string.CompareOrdinal(
                left.StableUnitId,
                right.StableUnitId));

            this.sessionId = sessionId;
            this.targetX = targetX;
            this.targetY = targetY;
            this.expeditionOrdinal = expeditionOrdinal;
            this.units = valid.ToArray();
            this.leaderHealthy = leaderHealthy;
            OutboundDurationSeconds = BaseDurationSeconds +
                                      pathCost * SecondsPerPathCost;
            ReturnDurationSeconds = pathCost * SecondsPerPathCost;
            RemainingSeconds = OutboundDurationSeconds;
            Status = ArmyExpeditionStatus.Outbound;
            AdvancePersistenceGeneration();
            return true;
        }

        public bool Tick(float deltaSeconds, bool globallyPaused)
        {
            if (globallyPaused || !IsFinite(deltaSeconds) ||
                deltaSeconds <= 0f ||
                Status != ArmyExpeditionStatus.Outbound &&
                Status != ArmyExpeditionStatus.Returning)
            {
                return false;
            }

            float remainingDelta = deltaSeconds;
            bool changed = false;
            while (remainingDelta > Epsilon &&
                   (Status == ArmyExpeditionStatus.Outbound ||
                    Status == ArmyExpeditionStatus.Returning))
            {
                float used = Math.Min(remainingDelta, RemainingSeconds);
                RemainingSeconds -= used;
                remainingDelta -= used;
                changed |= used > 0f;
                if (RemainingSeconds > Epsilon) break;
                RemainingSeconds = 0f;

                if (Status == ArmyExpeditionStatus.Outbound)
                {
                    ResolveEncounter();
                    Status = ArmyExpeditionStatus.Returning;
                    RemainingSeconds = ReturnDurationSeconds;
                    if (RemainingSeconds <= Epsilon)
                    {
                        Status = retreating
                            ? ArmyExpeditionStatus.Retreated
                            : ArmyExpeditionStatus.Returned;
                    }
                }
                else
                {
                    Status = retreating
                        ? ArmyExpeditionStatus.Retreated
                        : ArmyExpeditionStatus.Returned;
                }
            }
            if (changed) AdvancePersistenceGeneration();
            return changed;
        }

        public bool Retreat()
        {
            if (Status != ArmyExpeditionStatus.Outbound &&
                Status != ArmyExpeditionStatus.Returning)
            {
                return false;
            }
            retreating = true;
            pendingLoot = Array.Empty<ResourceAmount>();
            Status = ArmyExpeditionStatus.Returning;
            RemainingSeconds = ReturnDurationSeconds;
            if (RemainingSeconds <= Epsilon)
                Status = ArmyExpeditionStatus.Retreated;
            AdvancePersistenceGeneration();
            return true;
        }

        public bool TryClaimReturnedLoot(out ResourceAmount[] loot)
        {
            loot = Array.Empty<ResourceAmount>();
            if (Status != ArmyExpeditionStatus.Returned ||
                pendingLoot.Length == 0)
            {
                return false;
            }
            loot = (ResourceAmount[])pendingLoot.Clone();
            pendingLoot = Array.Empty<ResourceAmount>();
            Status = ArmyExpeditionStatus.Completed;
            AdvancePersistenceGeneration();
            return true;
        }

        public bool TryDepositReturnedLoot(
            CityResourceStorageModel cityStorage)
        {
            if (Status != ArmyExpeditionStatus.Returned ||
                cityStorage == null || pendingLoot.Length == 0 ||
                !cityStorage.TryCommitBatch(
                    Array.Empty<ResourceAmount>(),
                    pendingLoot))
            {
                return false;
            }
            pendingLoot = Array.Empty<ResourceAmount>();
            Status = ArmyExpeditionStatus.Completed;
            AdvancePersistenceGeneration();
            return true;
        }

        public ArmyExpeditionPersistenceSnapshot CaptureForPersistence()
        {
            return new ArmyExpeditionPersistenceSnapshot(
                Status,
                sessionId,
                targetX,
                targetY,
                expeditionOrdinal,
                OutboundDurationSeconds,
                ReturnDurationSeconds,
                RemainingSeconds,
                units,
                leaderHealthy,
                retreating,
                Encounter?.EnemyDefinitionIds ?? Array.Empty<string>(),
                Resolution != null,
                Resolution?.ArmyPower ?? 0f,
                Resolution?.EnemyPower ?? 0,
                Resolution?.CasualtyStableUnitIds ?? Array.Empty<string>(),
                pendingLoot,
                Resolution?.Victory ?? false);
        }

        public bool TryPrepareRestoreForPersistence(
            ArmyExpeditionPersistenceSnapshot snapshot,
            out ArmyExpeditionRestorePlan plan,
            out string error)
        {
            plan = null;
            if (!TryValidateSnapshot(snapshot, out error)) return false;
            plan = new ArmyExpeditionRestorePlan(
                this,
                persistenceGeneration,
                CloneSnapshot(snapshot));
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestoreForPersistence(
            ArmyExpeditionRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.Consumed ||
                plan.ExpectedGeneration != persistenceGeneration)
                return Fail("远征恢复计划无效或已过期", out error);
            ArmyExpeditionPersistenceSnapshot value = plan.Snapshot;
            Status = value.Status;
            sessionId = value.SessionId;
            targetX = value.TargetX;
            targetY = value.TargetY;
            expeditionOrdinal = value.ExpeditionOrdinal;
            OutboundDurationSeconds = value.OutboundDurationSeconds;
            ReturnDurationSeconds = value.ReturnDurationSeconds;
            RemainingSeconds = value.RemainingSeconds;
            units = (ArmyExpeditionUnit[])value.Units.Clone();
            leaderHealthy = value.LeaderHealthy;
            retreating = value.Retreating;
            pendingLoot = (ResourceAmount[])value.PendingLoot.Clone();
            Encounter = value.EnemyDefinitionIds.Length == 0
                ? null
                : new ArmyExpeditionEncounter(
                    (string[])value.EnemyDefinitionIds.Clone());
            Resolution = value.HasResolution
                ? new ArmyExpeditionResolution(
                    value.Victory,
                    value.ArmyPower,
                    value.EnemyPower,
                    (string[])value.CasualtyStableUnitIds.Clone())
                : null;
            plan.Consumed = true;
            AdvancePersistenceGeneration();
            error = string.Empty;
            return true;
        }

        private static bool TryValidateSnapshot(
            ArmyExpeditionPersistenceSnapshot snapshot,
            out string error)
        {
            if (snapshot == null || snapshot.Units == null ||
                snapshot.EnemyDefinitionIds == null ||
                snapshot.CasualtyStableUnitIds == null ||
                snapshot.PendingLoot == null ||
                !Enum.IsDefined(typeof(ArmyExpeditionStatus), snapshot.Status))
                return Fail("远征存档不完整", out error);
            if (snapshot.Status == ArmyExpeditionStatus.Idle)
            {
                if (!string.IsNullOrEmpty(snapshot.SessionId) ||
                    snapshot.ExpeditionOrdinal != 0 ||
                    snapshot.Units.Length != 0 ||
                    snapshot.HasResolution ||
                    snapshot.PendingLoot.Length != 0)
                    return Fail("空闲远征包含活动数据", out error);
                error = string.Empty;
                return true;
            }
            if (string.IsNullOrWhiteSpace(snapshot.SessionId) ||
                snapshot.ExpeditionOrdinal <= 0 ||
                !IsFinite(snapshot.OutboundDurationSeconds) ||
                !IsFinite(snapshot.ReturnDurationSeconds) ||
                !IsFinite(snapshot.RemainingSeconds) ||
                snapshot.ReturnDurationSeconds < 0f ||
                Math.Abs(snapshot.OutboundDurationSeconds -
                         (BaseDurationSeconds +
                          snapshot.ReturnDurationSeconds)) > .001f ||
                snapshot.RemainingSeconds < 0f)
                return Fail("远征时钟或身份无效", out error);
            float phaseLimit = snapshot.Status == ArmyExpeditionStatus.Outbound
                ? snapshot.OutboundDurationSeconds
                : snapshot.ReturnDurationSeconds;
            if (snapshot.RemainingSeconds > phaseLimit + .001f)
                return Fail("远征剩余时间超出阶段", out error);
            if ((snapshot.Status == ArmyExpeditionStatus.Returned ||
                 snapshot.Status == ArmyExpeditionStatus.Retreated ||
                 snapshot.Status == ArmyExpeditionStatus.Completed) &&
                snapshot.RemainingSeconds != 0f)
                return Fail("已返程远征仍有剩余时间", out error);

            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Units.Length; index++)
            {
                ArmyExpeditionUnit unit = snapshot.Units[index];
                ArmyUnitDefinition definition = ArmyUnitCatalog.Find(
                    unit.DefinitionId);
                if (!unit.IsActive || definition == null ||
                    string.IsNullOrWhiteSpace(unit.StableUnitId) ||
                    !unitIds.Add(unit.StableUnitId) ||
                    unit.CurrentHealth <= 0 ||
                    unit.CurrentHealth > definition.MaximumHealth)
                    return Fail("远征单位状态无效", out error);
            }
            if (snapshot.Units.Length == 0)
                return Fail("活动远征没有单位", out error);

            var enemyIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 index < snapshot.EnemyDefinitionIds.Length;
                 index++)
            {
                string id = snapshot.EnemyDefinitionIds[index];
                if (FindEnemy(id) == null || !enemyIds.Add(id))
                    return Fail("远征敌人记录无效", out error);
            }
            if (snapshot.HasResolution)
            {
                if (snapshot.EnemyDefinitionIds.Length == 0 ||
                    !IsFinite(snapshot.ArmyPower) ||
                    snapshot.ArmyPower < 0f || snapshot.EnemyPower <= 0)
                    return Fail("远征结算数值无效", out error);
                var casualties = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0;
                     index < snapshot.CasualtyStableUnitIds.Length;
                     index++)
                {
                    string id = snapshot.CasualtyStableUnitIds[index];
                    if (!unitIds.Contains(id) || !casualties.Add(id))
                        return Fail("远征伤亡引用无效", out error);
                }
            }
            else if (snapshot.EnemyDefinitionIds.Length != 0 ||
                     snapshot.CasualtyStableUnitIds.Length != 0 ||
                     snapshot.ArmyPower != 0f || snapshot.EnemyPower != 0)
            {
                return Fail("未结算远征包含结算数据", out error);
            }

            if (snapshot.Status == ArmyExpeditionStatus.Outbound &&
                (snapshot.HasResolution || snapshot.Retreating))
                return Fail("出发阶段不应已有结算或撤退", out error);
            if (!snapshot.HasResolution && snapshot.Victory)
                return Fail("未结算远征不能标记胜利", out error);
            if ((snapshot.Status == ArmyExpeditionStatus.Returned ||
                 snapshot.Status == ArmyExpeditionStatus.Completed) &&
                !snapshot.HasResolution)
                return Fail("正常返城缺少远征结算", out error);
            if (snapshot.Status == ArmyExpeditionStatus.Returning &&
                !snapshot.Retreating && !snapshot.HasResolution)
                return Fail("正常返程缺少远征结算", out error);
            if (snapshot.Retreating && snapshot.PendingLoot.Length != 0)
                return Fail("撤退远征不能保留战利品", out error);
            if ((snapshot.Status == ArmyExpeditionStatus.Returned ||
                 snapshot.Status == ArmyExpeditionStatus.Completed) &&
                snapshot.Retreating)
                return Fail("正常返城状态不能标记撤退", out error);
            if (snapshot.Status == ArmyExpeditionStatus.Retreated &&
                !snapshot.Retreating)
                return Fail("撤退完成状态缺少撤退标记", out error);
            if (!TryValidateLoot(snapshot, out error)) return false;
            error = string.Empty;
            return true;
        }

        private static bool TryValidateLoot(
            ArmyExpeditionPersistenceSnapshot snapshot,
            out string error)
        {
            if (snapshot.PendingLoot.Length == 0)
            {
                error = string.Empty;
                return true;
            }
            if (!snapshot.HasResolution || !snapshot.Victory ||
                snapshot.Retreating ||
                snapshot.Status != ArmyExpeditionStatus.Returning &&
                snapshot.Status != ArmyExpeditionStatus.Returned ||
                snapshot.PendingLoot.Length != 3)
                return Fail("远征战利品与阶段不一致", out error);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.PendingLoot.Length; index++)
            {
                ResourceAmount item = snapshot.PendingLoot[index];
                if (!seen.Add(item.ResourceId) ||
                    !LootRange(item.ResourceId, out int minimum, out int maximum) ||
                    item.Amount < minimum || item.Amount > maximum)
                    return Fail("远征战利品数值无效", out error);
            }
            error = string.Empty;
            return true;
        }

        private static bool LootRange(
            string resourceId,
            out int minimum,
            out int maximum)
        {
            if (resourceId == ResourceIds.Alloy)
            {
                minimum = 10;
                maximum = 24;
                return true;
            }
            if (resourceId == ResourceIds.Biomass)
            {
                minimum = 8;
                maximum = 20;
                return true;
            }
            if (resourceId == ResourceIds.EnergyCrystal)
            {
                minimum = 4;
                maximum = 12;
                return true;
            }
            minimum = 0;
            maximum = 0;
            return false;
        }

        private static EnemyDefinition FindEnemy(string id)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                if (string.Equals(
                        EnemyCatalog.All[index].Id.Value,
                        id,
                        StringComparison.Ordinal))
                    return EnemyCatalog.All[index];
            }
            return null;
        }

        private static ArmyExpeditionPersistenceSnapshot CloneSnapshot(
            ArmyExpeditionPersistenceSnapshot snapshot)
        {
            return new ArmyExpeditionPersistenceSnapshot(
                snapshot.Status,
                snapshot.SessionId,
                snapshot.TargetX,
                snapshot.TargetY,
                snapshot.ExpeditionOrdinal,
                snapshot.OutboundDurationSeconds,
                snapshot.ReturnDurationSeconds,
                snapshot.RemainingSeconds,
                snapshot.Units,
                snapshot.LeaderHealthy,
                snapshot.Retreating,
                snapshot.EnemyDefinitionIds,
                snapshot.HasResolution,
                snapshot.ArmyPower,
                snapshot.EnemyPower,
                snapshot.CasualtyStableUnitIds,
                snapshot.PendingLoot,
                snapshot.Victory);
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

        private void ResolveEncounter()
        {
            uint hash = StableHash(
                sessionId,
                targetX,
                targetY,
                expeditionOrdinal);
            EnemyDefinition[] candidates = ExpeditionEnemies();
            int enemyCount = 1 + (int)(hash % 3u);
            var enemyIds = new string[enemyCount];
            var enemyPower = 0;
            for (var index = 0; index < enemyCount; index++)
            {
                EnemyDefinition enemy = candidates[
                    (int)((hash + (uint)(index * 17)) %
                          (uint)candidates.Length)];
                enemyIds[index] = enemy.Id.Value;
                enemyPower += enemy.MaximumHealth;
            }
            Encounter = new ArmyExpeditionEncounter(enemyIds);

            float armyPower = 0f;
            for (var index = 0; index < units.Length; index++)
            {
                ArmyUnitDefinition definition = ArmyUnitCatalog.Find(
                    units[index].DefinitionId);
                armyPower += definition.Damage * 10f *
                    Math.Max(.25f,
                        units[index].CurrentHealth /
                        (float)definition.MaximumHealth);
            }
            if (leaderHealthy) armyPower *= 1.2f;
            bool victory = armyPower >= enemyPower;
            int casualties = CasualtyCount(
                units.Length,
                armyPower,
                enemyPower,
                victory);
            string[] casualtyIds = SelectCasualties(
                units,
                casualties,
                hash >> 8);
            Resolution = new ArmyExpeditionResolution(
                victory,
                armyPower,
                enemyPower,
                casualtyIds);
            pendingLoot = victory
                ? new[]
                {
                    new ResourceAmount(
                        ResourceIds.Alloy,
                        Range(hash >> 3, 10, 24)),
                    new ResourceAmount(
                        ResourceIds.Biomass,
                        Range(hash >> 11, 8, 20)),
                    new ResourceAmount(
                        ResourceIds.EnergyCrystal,
                        Range(hash >> 19, 4, 12)),
                }
                : Array.Empty<ResourceAmount>();
        }

        private static int CasualtyCount(
            int unitCount,
            float armyPower,
            int enemyPower,
            bool victory)
        {
            if (unitCount <= 0 || enemyPower <= 0) return 0;
            float ratio = enemyPower / Math.Max(1f, armyPower + enemyPower);
            int losses = victory
                ? (int)Math.Floor(unitCount * ratio * .5f)
                : (int)Math.Ceiling(unitCount * ratio);
            return Math.Max(0, Math.Min(unitCount, losses));
        }

        private static string[] SelectCasualties(
            ArmyExpeditionUnit[] units,
            int count,
            uint sample)
        {
            if (count <= 0) return Array.Empty<string>();
            var result = new string[count];
            int start = (int)(sample % (uint)units.Length);
            for (var index = 0; index < count; index++)
            {
                result[index] =
                    units[(start + index) % units.Length].StableUnitId;
            }
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static EnemyDefinition[] ExpeditionEnemies()
        {
            var result = new List<EnemyDefinition>();
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                EnemyDefinition definition = EnemyCatalog.All[index];
                if (definition.MaximumHealth <= 1000)
                    result.Add(definition);
            }
            return result.ToArray();
        }

        private static int Range(uint sample, int minimum, int maximum)
        {
            return minimum +
                   (int)(sample % (uint)(maximum - minimum + 1));
        }

        private static uint StableHash(
            string sessionId,
            int targetX,
            int targetY,
            int ordinal)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (var index = 0; index < sessionId.Length; index++)
                {
                    char character = sessionId[index];
                    hash = (hash ^ (byte)character) * 16777619u;
                    hash = (hash ^ (byte)(character >> 8)) * 16777619u;
                }
                hash = (hash ^ (uint)targetX) * 16777619u;
                hash = (hash ^ (uint)targetY) * 16777619u;
                hash = (hash ^ (uint)ordinal) * 16777619u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return hash;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
