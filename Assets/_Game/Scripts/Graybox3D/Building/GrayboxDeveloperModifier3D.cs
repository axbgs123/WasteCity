#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Research;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperModifier3D
    {
        private readonly GrayboxBuildingSession3D session;
        private readonly GrayboxMobileCityController3D city;
        private readonly GrayboxBuildingWorldView3D presentation;
        private GrayboxDeveloperProgressionFacade3D progression;
        private bool hasModifiedGameState;

        public bool HasModifiedGameState => hasModifiedGameState;

        public void ConfigureProgressionFacade(
            GrayboxDeveloperProgressionFacade3D facade)
        {
            progression = facade;
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
            switch (actionId)
            {
                case "developer.attention.increase":
                    return progression.IncreaseAttention(amount);
                case "developer.attention.decrease":
                    return progression.DecreaseAttention(amount);
                case "developer.attention.set":
                    return progression.SetAttentionFixture(amount);
                case "developer.fate.select-pocket-universe":
                    return progression.SelectFate(
                        FormalFateCatalog.PocketUniverseId);
                case "developer.fate.select-void-debt":
                    return progression.SelectFate(
                        FormalFateCatalog.VoidDebtId);
                case "developer.fate.select-rewind-anchor":
                    return progression.SelectFate(
                        FormalFateCatalog.RewindAnchorId);
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
                    progression.Query();
                    return false;
                default:
                    return false;
            }
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
