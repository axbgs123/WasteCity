#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperModifier3D
    {
        private readonly GrayboxBuildingSession3D session;
        private readonly GrayboxMobileCityController3D city;
        private readonly GrayboxBuildingWorldView3D presentation;
        private bool hasModifiedGameState;

        public bool HasModifiedGameState => hasModifiedGameState;

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
                    message: "科技已经解锁：" + entry.DisplayName);
            }

            session.UnlockResearchForDevelopment(entry.StableId);
            RecordChange(session.IsResearchCompleted(entry.StableId));
            return Result(
                GrayboxDeveloperCommandCode3D.Success,
                true,
                entry,
                affectedCount: 1,
                message: "已解锁科技：" + entry.DisplayName);
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
