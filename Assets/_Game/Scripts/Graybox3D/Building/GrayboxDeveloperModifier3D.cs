#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Content;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Research;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxDeveloperTechnologyStateAction3D
    {
        Apply,
        SetOneStack,
        FillStacks,
        Clear,
        Expire,
        TriggerOverload,
    }

    public sealed class GrayboxDeveloperTechnologyStateFacade3D
    {
        private readonly GrayboxDefenseController3D defense;
        private readonly GrayboxCivilizationExpansionController3D expansion;
        private readonly GrayboxBuildingSession3D session;

        public GrayboxDeveloperTechnologyStateFacade3D(
            GrayboxDefenseController3D defense,
            GrayboxCivilizationExpansionController3D expansion,
            GrayboxBuildingSession3D session = null)
        {
            this.defense = defense;
            this.expansion = expansion;
            this.session = session;
        }

        public bool TryActivateSelectedOverload(out string feedback)
        {
            if (defense == null ||
                defense.SelectedKind != GrayboxDefenseSelectionKind3D.Tower ||
                string.IsNullOrWhiteSpace(defense.SelectedStableId))
            {
                feedback = "请先选中一座己方激光塔";
                return false;
            }
            bool activated = defense.TryActivateTechnologyOverload(
                defense.SelectedStableId);
            feedback = activated
                ? "选中激光塔已启动能量过载"
                : "能量过载未就绪，或当前目标不是可用激光塔";
            return activated;
        }

        public bool TryApplyLeaderGeneSplicing(out string feedback)
        {
            if (expansion == null)
            {
                feedback = "文明与领袖运行时尚未连接";
                return false;
            }
            return expansion.TryApplyGeneSplicingFixtureForDevelopment(
                out feedback);
        }

        public bool TrySetSelectedEnemyStatus(
            string statusId,
            bool fillStacks,
            out string feedback)
        {
            ResearchStatusDefinition status = ResearchStatusCatalog.Find(
                statusId);
            if (defense == null ||
                defense.SelectedKind != GrayboxDefenseSelectionKind3D.Enemy ||
                string.IsNullOrWhiteSpace(defense.SelectedStableId))
            {
                feedback = "请先选中一名敌人";
                return false;
            }
            bool changed = defense
                .TrySetSelectedEnemyTechnologyStatusForDevelopment(
                    statusId,
                    fillStacks);
            feedback = changed
                ? "已为选中敌人" +
                    (fillStacks ? "补满" : "设置") +
                    (status?.DisplayName ?? statusId)
                : (status?.DisplayName ?? statusId) +
                    "不支持当前动作，或敌人已离开战场";
            return changed;
        }

        public bool TryClearSelectedStatus(
            string statusId,
            out string feedback)
        {
            if (string.Equals(
                    statusId,
                    ResearchStatusCatalog.TechnologyOverloadId,
                    StringComparison.Ordinal))
            {
                bool cleared = defense != null &&
                    defense.TryClearSelectedOverloadForDevelopment();
                feedback = cleared
                    ? "已清除选中激光塔的能量过载"
                    : "请先选中一座拥有过载状态的激光塔";
                return cleared;
            }
            if (string.Equals(
                    statusId,
                    ResearchStatusCatalog.GeneSplicingTraitId,
                    StringComparison.Ordinal))
            {
                if (expansion == null)
                {
                    feedback = "文明与领袖运行时尚未连接";
                    return false;
                }
                return expansion
                    .TryClearCurrentLeaderGeneSplicingForDevelopment(
                        out feedback);
            }
            bool enemyCleared = defense != null &&
                defense.TryClearSelectedEnemyTechnologyStatusForDevelopment(
                    statusId);
            ResearchStatusDefinition status = ResearchStatusCatalog.Find(
                statusId);
            feedback = enemyCleared
                ? "已清除选中敌人的" +
                    (status?.DisplayName ?? statusId)
                : "选中目标没有可清除的" +
                    (status?.DisplayName ?? statusId);
            return enemyCleared;
        }

        public bool TryExpireSelectedStatus(
            string statusId,
            out string feedback)
        {
            if (string.Equals(
                    statusId,
                    ResearchStatusCatalog.TechnologyOverloadId,
                    StringComparison.Ordinal))
            {
                bool expired = defense != null &&
                    defense.TryExpireSelectedOverloadForDevelopment();
                feedback = expired
                    ? "选中激光塔的能量过载已立即到期"
                    : "选中激光塔没有可到期的过载阶段";
                return expired;
            }
            if (string.Equals(
                    statusId,
                    ResearchStatusCatalog.GeneSplicingTraitId,
                    StringComparison.Ordinal))
            {
                if (expansion == null)
                {
                    feedback = "文明与领袖运行时尚未连接";
                    return false;
                }
                return expansion
                    .TryExpireCurrentLeaderGeneSplicingForDevelopment(
                        out feedback);
            }
            bool enemyExpired = defense != null &&
                defense.TryExpireSelectedEnemyTechnologyStatusForDevelopment(
                    statusId);
            ResearchStatusDefinition status = ResearchStatusCatalog.Find(
                statusId);
            feedback = enemyExpired
                ? "选中敌人的" +
                    (status?.DisplayName ?? statusId) + "已立即到期"
                : (status?.DisplayName ?? statusId) +
                    "没有可到期的倒计时";
            return enemyExpired;
        }

        public IReadOnlyList<string> ListActiveStatusNames()
        {
            var names = new List<string>();
            SingleCityDefenseTechnologyStateSnapshot defenseState =
                defense?.TechnologyState;
            if (defenseState?.Overloads != null)
            {
                for (var index = 0;
                     index < defenseState.Overloads.Count;
                     index++)
                {
                    if (defenseState.Overloads[index].Phase ==
                        WasteCity.Combat.TechnologyOverloadPhase.Ready)
                        continue;
                    names.Add("能量过载：" +
                        defenseState.Overloads[index].TowerStableId);
                }
            }
            if (defenseState?.Enemies != null)
            {
                for (var index = 0; index < defenseState.Enemies.Count;
                     index++)
                {
                    SingleCityDefenseEnemyTechnologySnapshot enemy =
                        defenseState.Enemies[index];
                    if (enemy.SwordIntentStacks > 0)
                        names.Add("剑意：" + enemy.StableEnemyId + " " +
                            enemy.SwordIntentStacks + "层");
                    if (enemy.InfectionStacks > 0)
                        names.Add("感染：" + enemy.StableEnemyId + " " +
                            enemy.InfectionStacks + "层");
                    if (enemy.ResonanceRemaining > 0f)
                        names.Add("灵能共鸣：" + enemy.StableEnemyId + " " +
                            enemy.ResonanceRemaining.ToString("0.0") + "秒");
                    if (enemy.Controlled)
                        names.Add("精神操控：" + enemy.StableEnemyId);
                }
            }
            AddBuildingPassiveStatuses(names);
            AddArmyPassiveStatuses(names);
            CharacterLifeRuntime current = expansion?.Runtime?.FindCharacter(
                expansion.Runtime.Politics.CurrentLeaderId);
            if (current?.HasGeneSplicingTrait == true)
                names.Add("基因强化：" + current.Definition.DisplayName +
                    " " + current.GeneSplicingRemainingSeconds.ToString("0.0") +
                    "秒");
            return names.AsReadOnly();
        }

        private void AddBuildingPassiveStatuses(List<string> names)
        {
            IReadOnlyList<GrayboxBuildingTechnologyStateSnapshot3D> buildings =
                defense?.BuildingTechnologyState?.Buildings;
            if (buildings == null || buildings.Count == 0 || session == null)
                return;
            bool anyBuilding = false;
            bool repairBay = false;
            bool shieldGenerator = false;
            int shield = 0;
            for (var index = 0; index < buildings.Count; index++)
            {
                GrayboxBuildingTechnologyStateSnapshot3D building =
                    buildings[index];
                if (building.Destroyed) continue;
                anyBuilding = true;
                repairBay |= string.Equals(
                    building.BuildingId,
                    BuildingCatalog.AutomatedRepairBay.Id.Value,
                    StringComparison.Ordinal);
                shieldGenerator |= string.Equals(
                    building.BuildingId,
                    BuildingCatalog.ShieldGenerator.Id.Value,
                    StringComparison.Ordinal);
                shield += building.Shield;
            }
            if (repairBay && IsCompleted(
                    ResearchStatusCatalog.AutomatedRepairId))
                names.Add("自动维修：自动维修机甲站正在运行");
            if (anyBuilding && IsCompleted(
                    ResearchStatusCatalog.CarapaceRegenerationId))
                names.Add("甲壳再生：建筑被动生效");
            if (anyBuilding && IsCompleted(
                    ResearchStatusCatalog.TissueRegenerationId))
                names.Add("组织再生：建筑被动生效");
            if (shieldGenerator && IsCompleted(
                    ResearchStatusCatalog.CityShieldId))
                names.Add("城市护盾：护盾发生器正在运行，建筑护盾 " +
                    shield);
        }

        private void AddArmyPassiveStatuses(List<string> names)
        {
            IReadOnlyList<ArmyUnitSnapshot> units = expansion?.Runtime?.Army
                ?.Units;
            if (units == null || units.Count == 0 || session == null) return;
            var activeUnits = 0;
            var puppets = 0;
            for (var index = 0; index < units.Count; index++)
            {
                if (!units[index].IsActive) continue;
                activeUnits++;
                if (string.Equals(
                        units[index].DefinitionId,
                        ArmyUnitCatalog.CombatPuppetId,
                        StringComparison.Ordinal))
                    puppets++;
            }
            if (puppets > 0 && IsCompleted(
                    ResearchStatusCatalog.PuppetMaintenanceId))
                names.Add("傀儡维护：活动战斗傀儡 " + puppets);
            if (activeUnits > 0 && IsCompleted(
                    ResearchStatusCatalog.TissueRegenerationId))
                names.Add("组织再生：活动军队单位 " + activeUnits);
        }

        private bool IsCompleted(string statusId)
        {
            ResearchStatusDefinition status = ResearchStatusCatalog.Find(
                statusId);
            return status != null &&
                session.IsResearchCompleted(status.SourceResearchId);
        }

        public bool TryClearAll(out string feedback)
        {
            bool defenseCleared =
                defense?.TryClearTechnologyFixturesForDevelopment() == true;
            bool leaderCleared =
                expansion?.TryClearTechnologyFixturesForDevelopment() == true;
            bool cleared = defenseCleared || leaderCleared;
            feedback = cleared
                ? "已清理全部 Development 科技状态夹具"
                : "当前战役无可清理状态";
            return cleared;
        }
    }

    public sealed class GrayboxDeveloperModifier3D
    {
        private readonly GrayboxBuildingSession3D session;
        private readonly GrayboxMobileCityController3D city;
        private readonly GrayboxBuildingWorldView3D presentation;
        private GrayboxDeveloperProgressionFacade3D progression;
        private GrayboxDeveloperTechnologyStateFacade3D technologyStates;
        private bool hasModifiedGameState;

        public bool HasModifiedGameState => hasModifiedGameState;

        public void ConfigureProgressionFacade(
            GrayboxDeveloperProgressionFacade3D facade)
        {
            progression = facade;
        }

        public void ConfigureTechnologyStateFacade(
            GrayboxDeveloperTechnologyStateFacade3D facade)
        {
            technologyStates = facade;
        }

        public IReadOnlyList<string> ListActiveTechnologyStates()
        {
            return technologyStates?.ListActiveStatusNames() ??
                Array.Empty<string>();
        }

        public GrayboxDeveloperCommandResult3D
            ApplyTechnologyStatusFixtureWithFeedback(
                string statusIdOrChineseName)
        {
            return ExecuteTechnologyStatusActionWithFeedback(
                statusIdOrChineseName,
                GrayboxDeveloperTechnologyStateAction3D.Apply);
        }

        public GrayboxDeveloperCommandResult3D
            ExecuteTechnologyStatusActionWithFeedback(
                string statusIdOrChineseName,
                GrayboxDeveloperTechnologyStateAction3D action)
        {
            if (!GrayboxDeveloperCatalogQuery3D.TryResolveTechnologyStatus(
                    statusIdOrChineseName,
                    out GrayboxDeveloperCatalogEntry3D entry))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.UnknownAction,
                    false,
                    message: "未找到科技状态：" +
                        (statusIdOrChineseName ?? string.Empty));
            }
            if (technologyStates == null)
                return Result(
                    GrayboxDeveloperCommandCode3D.ProgressionUnavailable,
                    false,
                    entry,
                    message: "科技状态运行时尚未连接");
            ResearchStatusDefinition status =
                ResearchStatusCatalog.Find(entry.StableId);
            bool requiresUnlock =
                action != GrayboxDeveloperTechnologyStateAction3D.Clear &&
                action != GrayboxDeveloperTechnologyStateAction3D.Expire;
            if (requiresUnlock && status != null &&
                !session.IsResearchCompleted(status.SourceResearchId))
            {
                ResearchDefinition source = ResearchCatalog.Find(
                    status.SourceResearchId);
                return Result(
                    GrayboxDeveloperCommandCode3D.CommandFailed,
                    false,
                    entry,
                    message: "请先解锁" +
                        (source?.Name ?? status.SourceResearchId) + "科技");
            }

            bool applied;
            string feedback;
            switch (action)
            {
                case GrayboxDeveloperTechnologyStateAction3D.TriggerOverload:
                    if (!string.Equals(
                            entry.StableId,
                            ResearchStatusCatalog.TechnologyOverloadId,
                            StringComparison.Ordinal))
                    {
                        applied = false;
                        feedback = "只有能量过载支持“触发过载”";
                        break;
                    }
                    applied = technologyStates.TryActivateSelectedOverload(
                        out feedback);
                    break;
                case GrayboxDeveloperTechnologyStateAction3D.SetOneStack:
                case GrayboxDeveloperTechnologyStateAction3D.FillStacks:
                    applied = technologyStates.TrySetSelectedEnemyStatus(
                        entry.StableId,
                        action ==
                            GrayboxDeveloperTechnologyStateAction3D.FillStacks,
                        out feedback);
                    break;
                case GrayboxDeveloperTechnologyStateAction3D.Clear:
                    applied = technologyStates.TryClearSelectedStatus(
                        entry.StableId,
                        out feedback);
                    break;
                case GrayboxDeveloperTechnologyStateAction3D.Expire:
                    applied = technologyStates.TryExpireSelectedStatus(
                        entry.StableId,
                        out feedback);
                    break;
                default:
                    if (string.Equals(
                            entry.StableId,
                            ResearchStatusCatalog.TechnologyOverloadId,
                            StringComparison.Ordinal))
                    {
                        applied = technologyStates.TryActivateSelectedOverload(
                            out feedback);
                    }
                    else if (string.Equals(
                            entry.StableId,
                            ResearchStatusCatalog.GeneSplicingTraitId,
                            StringComparison.Ordinal))
                    {
                        applied = technologyStates.TryApplyLeaderGeneSplicing(
                            out feedback);
                    }
                    else if (string.Equals(
                                 entry.StableId,
                                 ResearchStatusCatalog.SwordIntentId,
                                 StringComparison.Ordinal) ||
                             string.Equals(
                                 entry.StableId,
                                 ResearchStatusCatalog.InfectionId,
                                 StringComparison.Ordinal) ||
                             string.Equals(
                                 entry.StableId,
                                 ResearchStatusCatalog.PsionicResonanceId,
                                 StringComparison.Ordinal))
                    {
                        applied = technologyStates.TrySetSelectedEnemyStatus(
                            entry.StableId,
                            fillStacks: false,
                            out feedback);
                    }
                    else
                    {
                        applied = false;
                        feedback = entry.DisplayName +
                            "是由正式建筑、军队或战斗规则产生的只读被动状态";
                    }
                    break;
            }
            RecordChange(applied);
            return Result(
                applied
                    ? GrayboxDeveloperCommandCode3D.Success
                    : GrayboxDeveloperCommandCode3D.CommandFailed,
                applied,
                entry,
                affectedCount: applied ? 1 : 0,
                message: feedback);
        }

        public GrayboxDeveloperCommandResult3D
            ClearTechnologyStatusFixturesWithFeedback()
        {
            if (technologyStates == null)
                return Result(
                    GrayboxDeveloperCommandCode3D.ProgressionUnavailable,
                    false,
                    message: "科技状态运行时尚未连接");
            bool cleared = technologyStates.TryClearAll(out string feedback);
            RecordChange(cleared);
            return Result(
                cleared
                    ? GrayboxDeveloperCommandCode3D.Success
                    : GrayboxDeveloperCommandCode3D.NoChange,
                true,
                affectedCount: cleared ? 1 : 0,
                message: feedback);
        }

        public GrayboxDeveloperProgressionQuery3D QueryProgression()
        {
            return progression?.Query();
        }

        public GrayboxDeveloperCommandResult3D ExecuteProgressionAction(
            string actionIdOrChineseName,
            string argument = null,
            int amount = 0)
        {
            if (!GrayboxDeveloperCatalogQuery3D.TryResolveProgressionAction(
                    actionIdOrChineseName,
                    out GrayboxDeveloperCatalogEntry3D entry))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.UnknownAction,
                    false,
                    message: "未找到领域动作：" +
                        (actionIdOrChineseName ?? string.Empty));
            }
            if (progression == null)
                return Result(
                    GrayboxDeveloperCommandCode3D.ProgressionUnavailable,
                    false,
                    entry,
                    amount,
                    message: "正式进度命令尚未接入");
            if (!ValidateProgressionArguments(
                    entry.StableId,
                    argument,
                    amount,
                    out string validationError))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.InvalidAmount,
                    false,
                    entry,
                    amount,
                    message: validationError);
            }

            bool changed = ExecuteProgressionCore(
                entry.StableId,
                argument,
                amount);
            RecordChange(changed);
            if (changed)
                return Result(
                    GrayboxDeveloperCommandCode3D.Success,
                    true,
                    entry,
                    amount,
                    affectedCount: 1,
                    message: entry.DisplayName + "：已执行");
            if (entry.StableId == "developer.query.fate-domain-states")
            {
                GrayboxDeveloperProgressionQuery3D query =
                    progression.Query();
                return Result(
                    GrayboxDeveloperCommandCode3D.NoChange,
                    true,
                    entry,
                    amount,
                    message: "命轨领域状态：" +
                        string.Join("；", query.FateDomainStates));
            }
            bool failed = IsFailureOnlyAction(entry.StableId);
            return Result(
                failed
                    ? GrayboxDeveloperCommandCode3D.CommandFailed
                    : GrayboxDeveloperCommandCode3D.NoChange,
                !failed,
                entry,
                amount,
                message: failed
                    ? entry.DisplayName + "：执行失败"
                    : entry.DisplayName + "：状态未改变");
        }

        public GrayboxDeveloperModifier3D(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            this.city = city ?? throw new ArgumentNullException(nameof(city));
            this.presentation = presentation ?? throw new ArgumentNullException(
                nameof(presentation));
        }

        private bool ExecuteProgressionCore(
            string actionId,
            string argument,
            int amount)
        {
            string selectedFateId = ResolveFateSelectionAction(actionId);
            if (selectedFateId != null)
                return progression.SelectFate(selectedFateId);

            switch (actionId)
            {
                case "developer.attention.increase":
                    return progression.IncreaseAttention(amount);
                case "developer.attention.decrease":
                    return progression.DecreaseAttention(amount);
                case "developer.attention.set":
                    return progression.SetAttentionFixture(amount);
                case "developer.fate.upgrade-level-two":
                    return progression.UpgradeSelectedFateToLevelTwo();
                case "developer.rewind.create":
                    return progression.CreateRewindAnchor();
                case "developer.rewind.read":
                    return progression.ReadRewindAnchor(argument);
                case "developer.rewind.clear":
                    return progression.ClearRewindAnchors();
                case "developer.void-debt.add":
                    return progression.AddVoidDebt(
                        ResolveResourceArgument(argument), amount);
                case "developer.void-debt.repay":
                    return progression.RepayVoidDebt(
                        ResolveResourceArgument(argument), amount);
                case "developer.pressure.trigger":
                    return progression.TriggerPressure(amount);
                case "developer.pressure.complete":
                    return progression.CompletePressureFixture(amount);
                case "developer.pressure.reset":
                    return progression.ResetPressureFixture();
                case "developer.boss.set-defeated":
                    return progression.SetBossDefeatedFixture(true);
                case "developer.boss.clear-defeated":
                    return progression.SetBossDefeatedFixture(false);
                case "developer.ascension.requirements-satisfy":
                    return progression.SatisfyAscensionRequirementsFixture();
                case "developer.ascension.requirements-clear":
                    return progression.ClearAscensionRequirementsFixture();
                case "developer.civilization.first-ascension":
                    return progression.ExecuteFirstCivilizationAscension();
                case "developer.query.committed-ids":
                case "developer.query.thresholds":
                case "developer.query.pressure-queue":
                case "developer.query.configuration-signature":
                case "developer.query.fate-domain-states":
                    progression.Query();
                    return false;
                default:
                    return false;
            }
        }

        private static string ResolveFateSelectionAction(string actionId)
        {
            const string actionPrefix = "developer.fate.select-";
            if (string.IsNullOrWhiteSpace(actionId) ||
                !actionId.StartsWith(actionPrefix, StringComparison.Ordinal))
                return null;
            string fateId = "core.legacy." +
                actionId.Substring(actionPrefix.Length);
            return FormalFateCatalog.Find(fateId) == null ? null : fateId;
        }

        private static bool ValidateProgressionArguments(
            string actionId,
            string argument,
            int amount,
            out string error)
        {
            bool positiveAmount = actionId == "developer.attention.increase" ||
                actionId == "developer.attention.decrease" ||
                actionId == "developer.void-debt.add" ||
                actionId == "developer.void-debt.repay";
            if (positiveAmount && amount <= 0)
            {
                error = "动作数量必须大于 0";
                return false;
            }
            if (actionId == "developer.attention.set" &&
                (amount < FormalAttentionCatalog.MinimumValue ||
                 amount > FormalAttentionCatalog.MaximumValue))
            {
                error = "关注度设置值必须位于 0–100";
                return false;
            }
            if ((actionId == "developer.void-debt.add" ||
                 actionId == "developer.void-debt.repay") &&
                !GrayboxDeveloperCatalogQuery3D.TryResolveResource(
                    argument,
                    out _))
            {
                error = "虚空债动作需要正式资源 ID";
                return false;
            }
            if ((actionId == "developer.pressure.trigger" ||
                 actionId == "developer.pressure.complete") &&
                AttentionPressureCatalog.FindByThreshold(amount) == null)
            {
                error = "压力阈值必须是 30、60 或 90";
                return false;
            }
            if (actionId == "developer.rewind.read" &&
                string.IsNullOrWhiteSpace(argument))
            {
                error = "读取锚点需要稳定 anchor ID";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool IsFailureOnlyAction(string actionId)
        {
            return actionId == "developer.rewind.create" ||
                actionId == "developer.rewind.read" ||
                actionId == "developer.rewind.clear" ||
                actionId == "developer.fate.upgrade-level-two" ||
                actionId == "developer.civilization.first-ascension";
        }

        private static string ResolveResourceArgument(string argument)
        {
            return GrayboxDeveloperCatalogQuery3D.TryResolveResource(
                argument,
                out GrayboxDeveloperCatalogEntry3D entry)
                    ? entry.StableId
                    : argument;
        }

        public bool AddResource(string resourceId, int amount)
        {
            if (!IsKnownResource(resourceId) || amount <= 0)
                return false;
            RecordChange(session.Inventory.Add(resourceId, amount) > 0);
            return true;
        }

        public GrayboxDeveloperCommandResult3D AddResourceWithFeedback(
            string resourceIdOrChineseName,
            int amount)
        {
            if (!GrayboxDeveloperCatalogQuery3D.TryResolveResource(
                    resourceIdOrChineseName,
                    out GrayboxDeveloperCatalogEntry3D entry))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.UnknownResource,
                    false,
                    message: "未找到物品：" +
                        (resourceIdOrChineseName ?? string.Empty));
            }
            if (amount <= 0)
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.InvalidAmount,
                    false,
                    entry,
                    amount,
                    message: "增加数量必须大于 0");
            }

            int applied = session.Inventory.Add(entry.StableId, amount);
            RecordChange(applied > 0);
            if (applied == amount)
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.Success,
                    true,
                    entry,
                    amount,
                    applied,
                    message: entry.DisplayName + " 已增加 " + applied);
            }
            if (applied > 0)
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.PartialCapacity,
                    true,
                    entry,
                    amount,
                    applied,
                    message: entry.DisplayName + " 容量不足，实际增加 " +
                        applied);
            }
            return Result(
                GrayboxDeveloperCommandCode3D.CapacityFull,
                false,
                entry,
                amount,
                message: entry.DisplayName + " 容量已满，未增加");
        }

        public bool SetResource(string resourceId, int amount)
        {
            if (!IsKnownResource(resourceId) || amount < 0)
                return false;
            int before = session.Inventory.Get(resourceId);
            session.Inventory.Set(resourceId, amount);
            RecordChange(session.Inventory.Get(resourceId) != before);
            return true;
        }

        public GrayboxDeveloperCommandResult3D SetResourceWithFeedback(
            string resourceIdOrChineseName,
            int amount)
        {
            if (!GrayboxDeveloperCatalogQuery3D.TryResolveResource(
                    resourceIdOrChineseName,
                    out GrayboxDeveloperCatalogEntry3D entry))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.UnknownResource,
                    false,
                    message: "未找到物品：" +
                        (resourceIdOrChineseName ?? string.Empty));
            }
            if (amount < 0)
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.InvalidAmount,
                    false,
                    entry,
                    amount,
                    message: "设置数量必须是非负整数");
            }

            int before = session.Inventory.Get(entry.StableId);
            session.Inventory.Set(entry.StableId, amount);
            int actual = session.Inventory.Get(entry.StableId);
            RecordChange(actual != before);
            if (actual == amount)
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.Success,
                    true,
                    entry,
                    amount,
                    actual,
                    message: entry.DisplayName + " 已设置为 " + actual);
            }
            return Result(
                GrayboxDeveloperCommandCode3D.PartialCapacity,
                true,
                entry,
                amount,
                actual,
                message: entry.DisplayName + " 超过容量上限，实际设为 " +
                    actual);
        }

        public bool ClearResource(string resourceId)
        {
            if (!IsKnownResource(resourceId))
                return false;
            int before = session.Inventory.Get(resourceId);
            session.Inventory.Set(resourceId, 0);
            RecordChange(before != session.Inventory.Get(resourceId));
            return true;
        }

        public bool UnlockResearch(string researchId)
        {
            if (ResearchCatalog.Find(researchId) == null)
                return false;
            bool wasCompleted = session.IsResearchCompleted(researchId);
            session.UnlockResearchForDevelopment(researchId);
            RecordChange(
                !wasCompleted && session.IsResearchCompleted(researchId));
            return true;
        }

        public GrayboxDeveloperCommandResult3D UnlockResearchWithFeedback(
            string researchIdOrChineseName)
        {
            if (!GrayboxDeveloperCatalogQuery3D.TryResolveResearch(
                    researchIdOrChineseName,
                    out GrayboxDeveloperCatalogEntry3D entry))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.UnknownResearch,
                    false,
                    message: "未找到科技：" +
                        (researchIdOrChineseName ?? string.Empty));
            }
            if (session.IsResearchCompleted(entry.StableId))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.AlreadyCompleted,
                    true,
                    entry,
                    affectedCount: 0,
                    message: "科技已经解锁：" + entry.DisplayName +
                        EffectFeedback(entry.StableId));
            }

            session.UnlockResearchForDevelopment(entry.StableId);
            RecordChange(session.IsResearchCompleted(entry.StableId));
            return Result(
                GrayboxDeveloperCommandCode3D.Success,
                true,
                entry,
                affectedCount: 1,
                message: "已解锁科技：" + entry.DisplayName +
                    EffectFeedback(entry.StableId));
        }

        private static string EffectFeedback(string researchId)
        {
            string names = string.Empty;
            foreach (ResearchEffectDefinition effect in
                     ResearchEffectCatalog.ForResearch(researchId))
            {
                if (!effect.IsExecutable ||
                    effect.Activation != ResearchEffectActivation.Active ||
                    effect.Kind == ResearchEffectKind.UnlockContent)
                {
                    continue;
                }
                names += string.IsNullOrEmpty(names)
                    ? effect.DisplayName
                    : "、" + effect.DisplayName;
            }
            return string.IsNullOrEmpty(names)
                ? string.Empty
                : "；已生效：" + names;
        }

        public bool UnlockRoute(ContentRoute route)
        {
            if (route != ContentRoute.Technology &&
                route != ContentRoute.Cultivation &&
                route != ContentRoute.BiologicalAscension &&
                route != ContentRoute.Psionics)
                return false;
            uint revision = session.CatalogRevision;
            session.UnlockRouteForDevelopment(route);
            RecordChange(session.CatalogRevision != revision);
            return true;
        }

        public GrayboxDeveloperCommandResult3D UnlockRouteWithFeedback(
            ContentRoute route)
        {
            if (!IsUnlockableRoute(route))
            {
                return Result(
                    GrayboxDeveloperCommandCode3D.InvalidRoute,
                    false,
                    message: "无法解锁未知路线");
            }

            uint revision = session.CatalogRevision;
            int before = session.Research.CompletedCount;
            session.UnlockRouteForDevelopment(route);
            int affected = Math.Max(
                0,
                session.Research.CompletedCount - before);
            RecordChange(session.CatalogRevision != revision);
            return Result(
                affected > 0
                    ? GrayboxDeveloperCommandCode3D.Success
                    : GrayboxDeveloperCommandCode3D.NoChange,
                true,
                affectedCount: affected,
                message: RouteName(route) + "路线已解锁，新增 " +
                    affected + " 项科技");
        }

        public void UnlockAllResearch()
        {
            uint revision = session.CatalogRevision;
            session.UnlockAllResearchForDevelopment();
            RecordChange(session.CatalogRevision != revision);
        }

        public GrayboxDeveloperCommandResult3D
            UnlockAllResearchWithFeedback()
        {
            uint revision = session.CatalogRevision;
            int before = session.Research.CompletedCount;
            session.UnlockAllResearchForDevelopment();
            int affected = Math.Max(
                0,
                session.Research.CompletedCount - before);
            RecordChange(session.CatalogRevision != revision);
            return Result(
                affected > 0
                    ? GrayboxDeveloperCommandCode3D.Success
                    : GrayboxDeveloperCommandCode3D.NoChange,
                true,
                affectedCount: affected,
                message: "全部科技已解锁，新增 " + affected + " 项");
        }

        public bool SetCityMode(CityMode mode)
        {
            if (mode != CityMode.Mobile && mode != CityMode.Fortress)
                return false;
            CityMode beforeMode = city.Mode;
            float beforeRemaining = city.Deployment.Remaining;
            bool changed = city.RestoreDeploymentForDevelopment(mode);
            RecordChange(
                changed &&
                (city.Mode != beforeMode ||
                 city.Deployment.Remaining != beforeRemaining));
            return changed;
        }

        public bool CompleteCityTransition()
        {
            bool changed = city.CompleteDeploymentTransitionForDevelopment();
            RecordChange(changed);
            return changed;
        }

        public bool SetPopulation(int value)
        {
            if (value < 0)
                return false;
            int before = session.Population;
            session.SetPopulationForDevelopment(value);
            RecordChange(session.Population != before);
            return true;
        }

        public bool SetConstructionSpeed(DevelopmentConstructionSpeed speed)
        {
            if (speed != DevelopmentConstructionSpeed.Normal &&
                speed != DevelopmentConstructionSpeed.Fast10 &&
                speed != DevelopmentConstructionSpeed.Fast100)
                return false;
            float before = session.ConstructionMultiplier;
            session.SetConstructionMultiplierForDevelopment((float)speed);
            RecordChange(session.ConstructionMultiplier != before);
            return true;
        }

        public void CompleteAllConstruction()
        {
            uint revision = session.CatalogRevision;
            session.CompleteAllConstructionForDevelopment(presentation);
            RecordChange(session.CatalogRevision != revision);
        }

        private void RecordChange(bool changed)
        {
            if (changed) hasModifiedGameState = true;
        }

        private static bool IsKnownResource(string resourceId)
        {
            return ResourceDefinitionCatalog.TryGet(resourceId, out _);
        }

        private static bool IsUnlockableRoute(ContentRoute route)
        {
            return route == ContentRoute.Technology ||
                route == ContentRoute.Cultivation ||
                route == ContentRoute.BiologicalAscension ||
                route == ContentRoute.Psionics;
        }

        private static string RouteName(ContentRoute route)
        {
            switch (route)
            {
                case ContentRoute.Technology: return "科技";
                case ContentRoute.Cultivation: return "修仙";
                case ContentRoute.BiologicalAscension: return "血肉";
                case ContentRoute.Psionics: return "灵能";
                default: return "未知";
            }
        }

        private static GrayboxDeveloperCommandResult3D Result(
            GrayboxDeveloperCommandCode3D code,
            bool succeeded,
            GrayboxDeveloperCatalogEntry3D entry = null,
            int requestedAmount = 0,
            int appliedAmount = 0,
            int affectedCount = 0,
            string message = null)
        {
            return new GrayboxDeveloperCommandResult3D(
                code,
                succeeded,
                entry?.StableId,
                entry?.DisplayName,
                requestedAmount,
                appliedAmount,
                affectedCount,
                message);
        }
    }
}
#endif
