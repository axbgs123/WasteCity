using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDefenseObservedStatus3D
    {
        internal GrayboxDefenseObservedStatus3D(
            string statusId,
            string displayName,
            string sourceResearchName,
            int stacks,
            float currentValue,
            float remainingSeconds,
            string phaseText)
        {
            StatusId = statusId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SourceResearchName = sourceResearchName ?? string.Empty;
            Stacks = Math.Max(0, stacks);
            CurrentValue = Math.Max(0f, currentValue);
            RemainingSeconds = Math.Max(0f, remainingSeconds);
            PhaseText = phaseText ?? string.Empty;
        }

        public string StatusId { get; }
        public string DisplayName { get; }
        public string SourceResearchName { get; }
        public int Stacks { get; }
        public float CurrentValue { get; }
        public float RemainingSeconds { get; }
        public string PhaseText { get; }
    }

    public sealed class GrayboxDefenseSelectionSnapshot3D
    {
        internal GrayboxDefenseSelectionSnapshot3D(
            GrayboxDefenseSelectionKind3D kind,
            string stableId,
            string definitionId,
            string displayName,
            int currentHealth,
            int maximumHealth,
            string statusText,
            string targetStableId,
            string targetDisplayName,
            bool canToggleTowerPause,
            GrayboxDefenseTowerSnapshot3D tower,
            GrayboxDefenseEnemySnapshot3D enemy,
            ProductionBuildingObservability production,
            IReadOnlyList<ResourceAmount> lostResources,
            IReadOnlyList<GrayboxDefenseObservedStatus3D>
                technologyStatuses,
            bool isTechnologyOverloadVisible,
            bool canActivateTechnologyOverload,
            string technologyOverloadButtonLabel)
        {
            Kind = kind;
            StableId = stableId;
            DefinitionId = definitionId;
            DisplayName = displayName;
            CurrentHealth = Math.Max(0, currentHealth);
            MaximumHealth = Math.Max(0, maximumHealth);
            StatusText = statusText ?? string.Empty;
            TargetStableId = targetStableId;
            TargetDisplayName = targetDisplayName;
            CanToggleTowerPause = canToggleTowerPause;
            Tower = tower;
            Enemy = enemy;
            Production = production;
            LostResources = lostResources ?? Array.Empty<ResourceAmount>();
            TechnologyStatuses = technologyStatuses ??
                Array.Empty<GrayboxDefenseObservedStatus3D>();
            IsTechnologyOverloadVisible = isTechnologyOverloadVisible;
            CanActivateTechnologyOverload =
                isTechnologyOverloadVisible &&
                canActivateTechnologyOverload;
            TechnologyOverloadButtonLabel =
                technologyOverloadButtonLabel ?? string.Empty;
        }

        public GrayboxDefenseSelectionKind3D Kind { get; }
        public string StableId { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public string StatusText { get; }
        public string TargetStableId { get; }
        public string TargetDisplayName { get; }
        public bool CanToggleTowerPause { get; }
        public GrayboxDefenseTowerSnapshot3D Tower { get; }
        public GrayboxDefenseEnemySnapshot3D Enemy { get; }
        public ProductionBuildingObservability Production { get; }
        public IReadOnlyList<ResourceAmount> LostResources { get; }
        public IReadOnlyList<GrayboxDefenseObservedStatus3D>
            TechnologyStatuses { get; }
        public bool IsTechnologyOverloadVisible { get; }
        public bool CanActivateTechnologyOverload { get; }
        public string TechnologyOverloadButtonLabel { get; }
    }

    public static class GrayboxDefenseSelectionProjection3D
    {
        public static GrayboxDefenseSelectionSnapshot3D Capture(
            GrayboxDefenseSelectionKind3D kind,
            string stableId,
            GrayboxDefenseRuntimeSnapshot3D defense,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            GrayboxBuildingHealthRuntime3D health,
            ProductionObservabilitySnapshot production,
            bool globallyPaused = false,
            GrayboxCombatDestructionResult3D destructionResult = null,
            SingleCityDefenseTechnologyStateSnapshot technologyState = null,
            GrayboxBuildingTechnologySnapshot3D buildingTechnologyState =
                null,
            bool energyOverloadUnlocked = false)
        {
            if (kind == GrayboxDefenseSelectionKind3D.None ||
                string.IsNullOrWhiteSpace(stableId))
            {
                return null;
            }

            if (kind == GrayboxDefenseSelectionKind3D.Enemy)
                return CaptureEnemy(
                    stableId,
                    defense,
                    globallyPaused,
                    technologyState);

            GrayboxBuildingInstance3D instance = FindInstance(
                instances,
                stableId);
            if (instance?.Placement?.Definition == null)
                return null;

            bool isRuin =
                instance.State == GrayboxBuildingInstanceState.AbandonedRuin ||
                instance.State == GrayboxBuildingInstanceState.DestroyedRuin;
            if (kind == GrayboxDefenseSelectionKind3D.Ruin && !isRuin)
                return null;
            if (kind != GrayboxDefenseSelectionKind3D.Ruin && isRuin)
                return null;

            GrayboxDefenseTowerSnapshot3D tower = kind ==
                GrayboxDefenseSelectionKind3D.Tower
                ? FindTower(defense, stableId)
                : null;
            if (kind == GrayboxDefenseSelectionKind3D.Tower &&
                DefenseTowerCatalog.For(
                    instance.Placement.Definition.Id.Value) == null)
            {
                return null;
            }
            if (kind == GrayboxDefenseSelectionKind3D.Tower &&
                tower == null &&
                instance.State !=
                    GrayboxBuildingInstanceState.UnderConstruction)
            {
                return null;
            }

            ResolveHealth(instance, health, out int current, out int maximum);
            ProductionBuildingObservability productionDetails = null;
            production?.TryGet(stableId, out productionDetails);
            string status = ResolveStatus(
                instance,
                tower,
                productionDetails,
                globallyPaused);
            string displayName = instance.Placement.Definition.Name +
                (isRuin ? "废墟" : string.Empty);
            bool canPause = tower != null &&
                instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned &&
                !instance.IsEvacuationLocked &&
                current > 0;
            IReadOnlyList<GrayboxDefenseObservedStatus3D> statuses =
                CollectStatuses(
                    kind,
                    stableId,
                    technologyState,
                    buildingTechnologyState,
                    energyOverloadUnlocked &&
                    string.Equals(
                        instance.Placement.Definition.Id.Value,
                        BuildingCatalog.LaserTower.Id.Value,
                        StringComparison.Ordinal),
                    out bool overloadVisible,
                    out bool canActivateOverload,
                    out string overloadButtonLabel);

            return new GrayboxDefenseSelectionSnapshot3D(
                kind,
                stableId,
                instance.Placement.Definition.Id.Value,
                displayName,
                current,
                maximum,
                status,
                tower?.TargetId,
                ResolveTowerTargetDisplayName(defense, tower),
                canPause,
                tower,
                enemy: null,
                productionDetails,
                ResolveLostResources(stableId, destructionResult),
                statuses,
                overloadVisible,
                canActivateOverload,
                overloadButtonLabel);
        }

        public static string ProductionStopReasonText(
            ProductionStopReason reason)
        {
            switch (reason)
            {
                case ProductionStopReason.MissingInput:
                    return "缺少输入";
                case ProductionStopReason.OutputFull:
                    return "输出已满";
                case ProductionStopReason.OutOfLogistics:
                    return "不在物流范围";
                case ProductionStopReason.Depleted:
                    return "矿脉已枯竭";
                case ProductionStopReason.PlayerPaused:
                    return "玩家暂停运行";
                default:
                    return "正常运行";
            }
        }

        public static string TowerStatusText(
            GrayboxDefenseTowerStatus3D status)
        {
            switch (status)
            {
                case GrayboxDefenseTowerStatus3D.Firing:
                    return "射击";
                case GrayboxDefenseTowerStatus3D.MissingAmmunition:
                    return "缺少弹药";
                case GrayboxDefenseTowerStatus3D.OutOfLogistics:
                    return "不在物流范围";
                case GrayboxDefenseTowerStatus3D.PlayerPaused:
                    return "玩家暂停运行";
                case GrayboxDefenseTowerStatus3D.Unavailable:
                    return "建筑未运行";
                default:
                    return "等待目标";
            }
        }

        private static GrayboxDefenseSelectionSnapshot3D CaptureEnemy(
            string stableId,
            GrayboxDefenseRuntimeSnapshot3D defense,
            bool globallyPaused,
            SingleCityDefenseTechnologyStateSnapshot technologyState)
        {
            GrayboxDefenseEnemySnapshot3D enemy = FindEnemy(defense, stableId);
            if (enemy == null) return null;
            string displayName = enemy.EnemyDefinitionId;
            for (int index = 0; index < EnemyCatalog.All.Length; index++)
            {
                if (!string.Equals(
                        EnemyCatalog.All[index].Id.Value,
                        enemy.EnemyDefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                displayName = EnemyCatalog.All[index].Name;
                break;
            }
            return new GrayboxDefenseSelectionSnapshot3D(
                GrayboxDefenseSelectionKind3D.Enemy,
                stableId,
                enemy.EnemyDefinitionId,
                string.IsNullOrWhiteSpace(displayName)
                    ? "未知敌人"
                    : displayName,
                enemy.CurrentHealth,
                enemy.MaximumHealth,
                globallyPaused
                    ? "游戏暂停"
                    : enemy.IsAttackingTarget
                        ? "攻击目标"
                        : "接近目标",
                enemy.TargetStableId,
                enemy.TargetDisplayName,
                canToggleTowerPause: false,
                tower: null,
                enemy,
                production: null,
                lostResources: null,
                technologyStatuses: CollectEnemyStatuses(
                    stableId,
                    technologyState),
                isTechnologyOverloadVisible: false,
                canActivateTechnologyOverload: false,
                technologyOverloadButtonLabel: string.Empty);
        }

        private static IReadOnlyList<GrayboxDefenseObservedStatus3D>
            CollectStatuses(
                GrayboxDefenseSelectionKind3D kind,
                string stableId,
                SingleCityDefenseTechnologyStateSnapshot technologyState,
                GrayboxBuildingTechnologySnapshot3D buildingTechnologyState,
                bool energyOverloadUnlocked,
                out bool overloadVisible,
                out bool canActivateOverload,
                out string overloadButtonLabel)
        {
            overloadVisible = false;
            canActivateOverload = false;
            overloadButtonLabel = string.Empty;
            if (kind == GrayboxDefenseSelectionKind3D.Tower)
            {
                return CollectTowerStatuses(
                    stableId,
                    technologyState,
                    energyOverloadUnlocked,
                    out overloadVisible,
                    out canActivateOverload,
                    out overloadButtonLabel);
            }
            if (kind == GrayboxDefenseSelectionKind3D.Building)
                return CollectBuildingStatuses(
                    stableId,
                    buildingTechnologyState);
            return Array.Empty<GrayboxDefenseObservedStatus3D>();
        }

        private static IReadOnlyList<GrayboxDefenseObservedStatus3D>
            CollectTowerStatuses(
                string stableId,
                SingleCityDefenseTechnologyStateSnapshot technologyState,
                bool energyOverloadUnlocked,
                out bool overloadVisible,
                out bool canActivateOverload,
                out string overloadButtonLabel)
        {
            overloadVisible = energyOverloadUnlocked;
            canActivateOverload = energyOverloadUnlocked;
            overloadButtonLabel = energyOverloadUnlocked
                ? "启动能量过载"
                : string.Empty;
            if (!energyOverloadUnlocked)
                return Array.Empty<GrayboxDefenseObservedStatus3D>();

            SingleCityDefenseOverloadSnapshot overload = null;
            if (technologyState?.Overloads != null)
            {
                for (var index = 0;
                     index < technologyState.Overloads.Count;
                     index++)
                {
                    if (string.Equals(
                            technologyState.Overloads[index].TowerStableId,
                            stableId,
                            StringComparison.Ordinal))
                    {
                        overload = technologyState.Overloads[index];
                        break;
                    }
                }
            }

            TechnologyOverloadPhase phase = overload?.Phase ??
                TechnologyOverloadPhase.Ready;
            float remaining = OverloadRemaining(overload);
            string phaseText = OverloadPhaseText(phase);
            canActivateOverload = phase == TechnologyOverloadPhase.Ready;
            overloadButtonLabel = canActivateOverload
                ? "启动能量过载"
                : phaseText + " " + remaining.ToString("0.0") + "秒";
            return Array.AsReadOnly(new[]
            {
                ObservedStatus(
                    ResearchStatusCatalog.TechnologyOverloadId,
                    stacks: 1,
                    currentValue: 0f,
                    remainingSeconds: remaining,
                    phaseText: phaseText),
            });
        }

        private static IReadOnlyList<GrayboxDefenseObservedStatus3D>
            CollectEnemyStatuses(
                string stableId,
                SingleCityDefenseTechnologyStateSnapshot technologyState)
        {
            if (technologyState?.Enemies == null)
                return Array.Empty<GrayboxDefenseObservedStatus3D>();
            SingleCityDefenseEnemyTechnologySnapshot enemy = null;
            for (var index = 0; index < technologyState.Enemies.Count; index++)
            {
                if (!string.Equals(
                        technologyState.Enemies[index].StableEnemyId,
                        stableId,
                        StringComparison.Ordinal))
                    continue;
                enemy = technologyState.Enemies[index];
                break;
            }
            if (enemy == null)
                return Array.Empty<GrayboxDefenseObservedStatus3D>();

            var result = new List<GrayboxDefenseObservedStatus3D>(4);
            if (enemy.SwordIntentStacks > 0)
                result.Add(ObservedStatus(
                    ResearchStatusCatalog.SwordIntentId,
                    enemy.SwordIntentStacks,
                    0f,
                    0f,
                    "叠加中"));
            if (enemy.InfectionStacks > 0)
                result.Add(ObservedStatus(
                    ResearchStatusCatalog.InfectionId,
                    enemy.InfectionStacks,
                    0f,
                    Math.Max(
                        0f,
                        InfectionModel.TickSeconds - enemy.InfectionElapsed),
                    "持续伤害"));
            if (enemy.ResonanceRemaining > 0f)
                result.Add(ObservedStatus(
                    ResearchStatusCatalog.PsionicResonanceId,
                    1,
                    0f,
                    enemy.ResonanceRemaining,
                    "已标记"));
            if (enemy.Controlled)
                result.Add(ObservedStatus(
                    ResearchStatusCatalog.MindControlId,
                    1,
                    0f,
                    0f,
                    "已控制"));
            return result.Count == 0
                ? Array.Empty<GrayboxDefenseObservedStatus3D>()
                : result.AsReadOnly();
        }

        private static IReadOnlyList<GrayboxDefenseObservedStatus3D>
            CollectBuildingStatuses(
                string stableId,
                GrayboxBuildingTechnologySnapshot3D technologyState)
        {
            if (technologyState?.Buildings == null)
                return Array.Empty<GrayboxDefenseObservedStatus3D>();
            for (var index = 0;
                 index < technologyState.Buildings.Count;
                 index++)
            {
                GrayboxBuildingTechnologyStateSnapshot3D building =
                    technologyState.Buildings[index];
                if (!string.Equals(
                        building.StableInstanceId,
                        stableId,
                        StringComparison.Ordinal) ||
                    building.Shield <= 0)
                    continue;
                return Array.AsReadOnly(new[]
                {
                    ObservedStatus(
                        ResearchStatusCatalog.CityShieldId,
                        stacks: 1,
                        currentValue: building.Shield,
                        remainingSeconds: 0f,
                        phaseText: "护盾生效"),
                });
            }
            return Array.Empty<GrayboxDefenseObservedStatus3D>();
        }

        private static GrayboxDefenseObservedStatus3D ObservedStatus(
            string statusId,
            int stacks,
            float currentValue,
            float remainingSeconds,
            string phaseText)
        {
            ResearchStatusDefinition definition =
                ResearchStatusCatalog.Find(statusId);
            ResearchDefinition source = definition == null
                ? null
                : ResearchCatalog.Find(definition.SourceResearchId);
            return new GrayboxDefenseObservedStatus3D(
                statusId,
                definition?.DisplayName ?? statusId,
                source?.Name ?? definition?.SourceResearchId ?? string.Empty,
                stacks,
                currentValue,
                remainingSeconds,
                phaseText);
        }

        private static float OverloadRemaining(
            SingleCityDefenseOverloadSnapshot overload)
        {
            if (overload == null) return 0f;
            switch (overload.Phase)
            {
                case TechnologyOverloadPhase.Boosting:
                    return overload.BoostRemaining;
                case TechnologyOverloadPhase.Lockout:
                    return overload.LockoutRemaining;
                case TechnologyOverloadPhase.Cooldown:
                    return overload.CooldownRemaining;
                default:
                    return 0f;
            }
        }

        private static string OverloadPhaseText(
            TechnologyOverloadPhase phase)
        {
            switch (phase)
            {
                case TechnologyOverloadPhase.Boosting:
                    return "强化";
                case TechnologyOverloadPhase.Lockout:
                    return "停火锁定";
                case TechnologyOverloadPhase.Cooldown:
                    return "冷却";
                default:
                    return "就绪";
            }
        }

        private static IReadOnlyList<ResourceAmount> ResolveLostResources(
            string stableId,
            GrayboxCombatDestructionResult3D destructionResult)
        {
            return destructionResult != null &&
                destructionResult.IsCommitted &&
                string.Equals(
                    stableId,
                    destructionResult.StableInstanceId,
                    StringComparison.Ordinal)
                    ? destructionResult.TotalLostResources
                    : Array.Empty<ResourceAmount>();
        }

        private static string ResolveTowerTargetDisplayName(
            GrayboxDefenseRuntimeSnapshot3D defense,
            GrayboxDefenseTowerSnapshot3D tower)
        {
            if (tower == null || string.IsNullOrWhiteSpace(tower.TargetId))
                return string.Empty;
            GrayboxDefenseEnemySnapshot3D enemy = FindEnemy(
                defense,
                tower.TargetId);
            if (enemy == null) return tower.TargetId;
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                if (string.Equals(
                        EnemyCatalog.All[index].Id.Value,
                        enemy.EnemyDefinitionId,
                        StringComparison.Ordinal))
                {
                    return EnemyCatalog.All[index].Name;
                }
            }
            return tower.TargetId;
        }

        private static string ResolveStatus(
            GrayboxBuildingInstance3D instance,
            GrayboxDefenseTowerSnapshot3D tower,
            ProductionBuildingObservability production,
            bool globallyPaused)
        {
            if (instance.State == GrayboxBuildingInstanceState.DestroyedRuin)
                return "战损废墟";
            if (instance.State == GrayboxBuildingInstanceState.AbandonedRuin)
                return "撤离遗迹";
            if (instance.State == GrayboxBuildingInstanceState.UnderConstruction)
                return "施工中";
            if (instance.IsEvacuationLocked)
                return "撤离处理中";
            if (tower != null)
                return globallyPaused
                    ? "游戏暂停"
                    : TowerStatusText(tower.Status);
            if (production != null)
            {
                return ProductionStopReasonText(
                    production.IsPlayerPaused
                        ? ProductionStopReason.PlayerPaused
                        : production.StopReason);
            }
            if (globallyPaused)
                return "游戏暂停";
            return "正常运行";
        }

        private static void ResolveHealth(
            GrayboxBuildingInstance3D instance,
            GrayboxBuildingHealthRuntime3D health,
            out int current,
            out int maximum)
        {
            maximum = instance.Placement.Definition.MaximumHealth;
            current = instance.State == GrayboxBuildingInstanceState.DestroyedRuin
                ? 0
                : maximum;
            if (health == null || !health.TryGetHealth(
                    instance.StableInstanceId,
                    out int liveCurrent,
                    out int liveMaximum,
                    out _))
            {
                return;
            }
            current = liveCurrent;
            maximum = liveMaximum;
            if (instance.State == GrayboxBuildingInstanceState.DestroyedRuin)
                current = 0;
        }

        private static GrayboxBuildingInstance3D FindInstance(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            string stableId)
        {
            if (instances == null) return null;
            for (int index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance != null && string.Equals(
                        instance.StableInstanceId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return instance;
                }
            }
            return null;
        }

        private static GrayboxDefenseTowerSnapshot3D FindTower(
            GrayboxDefenseRuntimeSnapshot3D defense,
            string stableId)
        {
            if (defense == null) return null;
            for (int index = 0; index < defense.Towers.Count; index++)
            {
                GrayboxDefenseTowerSnapshot3D tower = defense.Towers[index];
                if (string.Equals(
                        tower.StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return tower;
                }
            }
            return null;
        }

        private static GrayboxDefenseEnemySnapshot3D FindEnemy(
            GrayboxDefenseRuntimeSnapshot3D defense,
            string stableId)
        {
            if (defense == null) return null;
            for (int index = 0; index < defense.Enemies.Count; index++)
            {
                GrayboxDefenseEnemySnapshot3D enemy = defense.Enemies[index];
                if (string.Equals(
                        enemy.StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return enemy;
                }
            }
            return null;
        }
    }
}
