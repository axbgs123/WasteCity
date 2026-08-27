using System;
using WasteCity.Building;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingUpgradeAvailability3D
    {
        internal GrayboxBuildingUpgradeAvailability3D(
            string stableInstanceId,
            string targetBuildingId,
            bool visible,
            bool canUpgrade,
            string buttonLabel,
            string feedback)
        {
            StableInstanceId = stableInstanceId ?? string.Empty;
            TargetBuildingId = targetBuildingId ?? string.Empty;
            IsVisible = visible;
            CanUpgrade = canUpgrade;
            ButtonLabel = buttonLabel ?? string.Empty;
            Feedback = feedback ?? string.Empty;
        }
        public string StableInstanceId { get; }
        public string TargetBuildingId { get; }
        public bool IsVisible { get; }
        public bool CanUpgrade { get; }
        public string ButtonLabel { get; }
        public string Feedback { get; }
    }

    public enum GrayboxBuildingUpgradeCode3D
    {
        Upgraded,
        UnknownInstance,
        InvalidBuildingState,
        RequirementsLocked,
        InsufficientResources,
        PresentationFailed,
        CommitFailed,
    }

    public sealed class GrayboxBuildingUpgradeResult3D
    {
        internal GrayboxBuildingUpgradeResult3D(
            GrayboxBuildingUpgradeCode3D code,
            string message,
            string sourceBuildingId = null,
            string targetBuildingId = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            SourceBuildingId = sourceBuildingId ?? string.Empty;
            TargetBuildingId = targetBuildingId ?? string.Empty;
        }

        public GrayboxBuildingUpgradeCode3D Code { get; }
        public bool Success => Code == GrayboxBuildingUpgradeCode3D.Upgraded;
        public string Message { get; }
        public string SourceBuildingId { get; }
        public string TargetBuildingId { get; }
    }

    public sealed class GrayboxBuildingUpgradeController3D
    {
        public const string AlloyArmorResearchId =
            "core.research.alloy-armor";
        public const string SwordRidingResearchId =
            "core.research.sword-riding";

        private readonly GrayboxBuildingSession3D session;
        private readonly Func<int> civilizationLevelProvider;
        private readonly IGrayboxBuildingPresentation3D presentation;

        public GrayboxBuildingUpgradeController3D(
            GrayboxBuildingSession3D session,
            Func<int> civilizationLevelProvider,
            IGrayboxBuildingPresentation3D presentation)
        {
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.civilizationLevelProvider = civilizationLevelProvider ??
                throw new ArgumentNullException(
                    nameof(civilizationLevelProvider));
            this.presentation = presentation ??
                throw new ArgumentNullException(nameof(presentation));
        }

        public GrayboxBuildingUpgradeResult3D TryUpgrade(
            string stableInstanceId)
        {
            GrayboxBuildingInstance3D instance = Find(stableInstanceId);
            if (instance == null)
                return Result(
                    GrayboxBuildingUpgradeCode3D.UnknownInstance,
                    "未找到要升级的建筑");
            string sourceId = instance.Placement.Definition.Id.Value;
            if (instance.State != GrayboxBuildingInstanceState.Completed ||
                !instance.IsPlayerOwned || instance.IsEvacuationLocked)
            {
                return Result(
                    GrayboxBuildingUpgradeCode3D.InvalidBuildingState,
                    "只有已完成、归玩家且未进入撤离的建筑可以升级",
                    sourceId);
            }

            bool alloyArmor = session.IsResearchCompleted(
                AlloyArmorResearchId);
            bool swordRiding = session.IsResearchCompleted(
                SwordRidingResearchId);
            BuildingUpgradeDefinition upgrade = BuildingUpgradeCatalog.For(
                instance.Placement.Definition,
                civilizationLevelProvider(),
                alloyArmor,
                swordRiding);
            if (upgrade == null)
                return Result(
                    GrayboxBuildingUpgradeCode3D.RequirementsLocked,
                    "文明等级或对应研究尚未满足建筑升级条件",
                    sourceId);
            string targetId = upgrade.Target.Id.Value;
            if (!session.CityStorage.CanSpendFromNetwork(
                    upgrade.CostId,
                    upgrade.Cost))
            {
                return Result(
                    GrayboxBuildingUpgradeCode3D.InsufficientResources,
                    "城市库存缺少升级所需材料",
                    sourceId,
                    targetId);
            }

            if (!session.TryUpgradeCompletedBuilding(
                    instance,
                    upgrade,
                    presentation,
                    out string error))
            {
                GrayboxBuildingUpgradeCode3D code =
                    error.IndexOf("表现", StringComparison.Ordinal) >= 0
                        ? GrayboxBuildingUpgradeCode3D.PresentationFailed
                        : GrayboxBuildingUpgradeCode3D.CommitFailed;
                return Result(code, error, sourceId, targetId);
            }
            return Result(
                GrayboxBuildingUpgradeCode3D.Upgraded,
                "建筑升级完成",
                sourceId,
                targetId);
        }

        public GrayboxBuildingUpgradeAvailability3D CaptureAvailability(
            string stableInstanceId)
        {
            GrayboxBuildingInstance3D instance = Find(stableInstanceId);
            if (instance == null ||
                instance.State != GrayboxBuildingInstanceState.Completed ||
                !instance.IsPlayerOwned || instance.IsEvacuationLocked)
                return Hidden(stableInstanceId);
            BuildingDefinition source = instance.Placement.Definition;
            bool machineGun = ReferenceEquals(
                source, BuildingCatalog.MachineGunTurret);
            bool swordArray = ReferenceEquals(
                source, BuildingCatalog.SwordArrayTower);
            if (!machineGun && !swordArray) return Hidden(stableInstanceId);
            BuildingUpgradeDefinition formal = BuildingUpgradeCatalog.For(
                source, 2, alloyArmorCompleted: true,
                swordRidingCompleted: true);
            string label = "升级为 " + formal.Target.Name;
            int level = civilizationLevelProvider();
            if (level < 2)
                return Available(instance, formal, false, label,
                    "需要文明 Lv.2");
            bool research = session.IsResearchCompleted(machineGun
                ? AlloyArmorResearchId
                : SwordRidingResearchId);
            if (!research)
                return Available(instance, formal, false, label,
                    machineGun ? "需要完成合金装甲" : "需要完成御剑术");
            int available = session.CityStorage.GetNetworkAmount(formal.CostId);
            if (available < formal.Cost)
                return Available(instance, formal, false, label,
                    "材料不足：需要 " + formal.Cost + "，当前 " + available);
            return Available(instance, formal, true, label,
                "消耗 " + formal.Cost + " 材料");
        }

        private GrayboxBuildingInstance3D Find(string stableInstanceId)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId)) return null;
            for (var index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = session.Instances[index];
                if (string.Equals(
                        instance.StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal)) return instance;
            }
            return null;
        }

        private static GrayboxBuildingUpgradeAvailability3D Hidden(
            string stableId) => new GrayboxBuildingUpgradeAvailability3D(
                stableId, null, false, false, null, null);

        private static GrayboxBuildingUpgradeAvailability3D Available(
            GrayboxBuildingInstance3D instance,
            BuildingUpgradeDefinition upgrade,
            bool canUpgrade,
            string label,
            string feedback) => new GrayboxBuildingUpgradeAvailability3D(
                instance.StableInstanceId,
                upgrade.Target.Id.Value,
                true,
                canUpgrade,
                label,
                feedback);

        private static GrayboxBuildingUpgradeResult3D Result(
            GrayboxBuildingUpgradeCode3D code,
            string message,
            string sourceId = null,
            string targetId = null)
        {
            return new GrayboxBuildingUpgradeResult3D(
                code,
                message,
                sourceId,
                targetId);
        }
    }
}
