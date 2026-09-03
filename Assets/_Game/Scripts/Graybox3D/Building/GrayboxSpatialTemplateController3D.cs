using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxSpatialTemplateFailureKind3D
    {
        None = 0,
        TemplateUnavailable = 1,
        UnknownBuilding = 2,
        InvalidPlacement = 3,
        TemplateOverlap = 4,
        InvalidPlan = 5,
        CommitFailed = 6
    }

    public readonly struct GrayboxSpatialTemplateFailure3D
    {
        internal GrayboxSpatialTemplateFailure3D(
            GrayboxSpatialTemplateFailureKind3D kind,
            string buildingDefinitionId,
            int worldX,
            int worldY,
            BuildingPlacementFailure primaryFailure,
            string reason)
        {
            Kind = kind;
            BuildingDefinitionId = buildingDefinitionId ?? string.Empty;
            WorldX = worldX;
            WorldY = worldY;
            PrimaryFailure = primaryFailure;
            Reason = reason ?? string.Empty;
        }

        public GrayboxSpatialTemplateFailureKind3D Kind { get; }
        public string BuildingDefinitionId { get; }
        public int WorldX { get; }
        public int WorldY { get; }
        public BuildingPlacementFailure PrimaryFailure { get; }
        public string Reason { get; }
    }

    public sealed class GrayboxSpatialTemplateDeploymentPlan3D
    {
        internal GrayboxSpatialTemplateDeploymentPlan3D(
            GrayboxSpatialTemplateController3D owner,
            SpatialTemplateDeploymentEntry3D[] entries)
        {
            Owner = owner;
            Entries = entries;
        }

        internal GrayboxSpatialTemplateController3D Owner { get; }
        internal SpatialTemplateDeploymentEntry3D[] Entries { get; }
        internal bool Consumed { get; set; }
    }

    internal readonly struct SpatialTemplateDeploymentEntry3D
    {
        public SpatialTemplateDeploymentEntry3D(
            BuildingDefinition definition,
            BuildingOrientation orientation,
            int worldX,
            int worldY)
        {
            Definition = definition;
            Orientation = orientation;
            WorldX = worldX;
            WorldY = worldY;
        }

        public BuildingDefinition Definition { get; }
        public BuildingOrientation Orientation { get; }
        public int WorldX { get; }
        public int WorldY { get; }
    }

    public sealed class GrayboxSpatialTemplateController3D
    {
        private readonly SpatialTemplateRuntime runtime;
        private readonly GrayboxBuildingPlacementController3D placement;

        public GrayboxSpatialTemplateController3D(
            SpatialTemplateRuntime runtime,
            GrayboxBuildingPlacementController3D placement)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
            this.placement = placement ??
                throw new ArgumentNullException(nameof(placement));
        }

        public bool TryRecordGroundRegion(
            string templateId,
            int anchorX,
            int anchorY,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out string error)
        {
            if (!string.Equals(
                    templateId,
                    GrayboxFormalProgressionSaveAdapter3D
                        .FormalSpatialTemplateSlotId,
                    StringComparison.Ordinal))
            {
                error = "空间模板只能写入正式唯一模板槽";
                return false;
            }
            if (instances == null)
            {
                error = "录制空间模板需要建筑列表";
                return false;
            }

            var cells = new List<SpatialTemplateCell>();
            int regionMinX = anchorX - SpatialTemplateRuntime.TemplateRadius;
            int regionMaxX = anchorX + SpatialTemplateRuntime.TemplateRadius;
            int regionMinY = anchorY - SpatialTemplateRuntime.TemplateRadius;
            int regionMaxY = anchorY + SpatialTemplateRuntime.TemplateRadius;
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance?.Placement?.Definition == null ||
                    !instance.IsPlayerOwned ||
                    instance.IsEvacuationLocked ||
                    instance.Placement.Site != BuildingSite.Ground ||
                    (instance.State !=
                         GrayboxBuildingInstanceState.UnderConstruction &&
                     instance.State != GrayboxBuildingInstanceState.Completed))
                    continue;

                PlacedBuilding source = instance.Placement;
                int width = BuildingOrientationRules.Width(
                    source.Definition,
                    source.Orientation);
                int height = BuildingOrientationRules.Height(
                    source.Definition,
                    source.Orientation);
                int footprintMaxX = source.X + width - 1;
                int footprintMaxY = source.Y + height - 1;
                bool intersectsRegion = source.X <= regionMaxX &&
                                        footprintMaxX >= regionMinX &&
                                        source.Y <= regionMaxY &&
                                        footprintMaxY >= regionMinY;
                if (!intersectsRegion)
                    continue;
                if (source.X < regionMinX ||
                    footprintMaxX > regionMaxX ||
                    source.Y < regionMinY ||
                    footprintMaxY > regionMaxY)
                {
                    error = source.Definition.Name +
                            " 的完整占格超出 3×3 空间模板范围";
                    return false;
                }

                cells.Add(new SpatialTemplateCell(
                    source.X - anchorX,
                    source.Y - anchorY,
                    source.Definition.Id.Value,
                    (int)source.Orientation));
            }

            if (!runtime.TryPrepareRecord(
                    templateId,
                    cells,
                    out SpatialTemplateRecordPlan plan,
                    out error))
                return false;
            return runtime.TryCommit(plan, out error);
        }

        public bool TryPrepareDeployment(
            string templateId,
            int anchorX,
            int anchorY,
            out GrayboxSpatialTemplateDeploymentPlan3D plan,
            out GrayboxSpatialTemplateFailure3D failure)
        {
            plan = null;
            SpatialTemplateDefinition template = FindTemplate(templateId);
            if (template == null)
            {
                failure = Failure(
                    GrayboxSpatialTemplateFailureKind3D.TemplateUnavailable,
                    null,
                    anchorX,
                    anchorY,
                    BuildingPlacementFailure.None,
                    "没有可部署的 3×3 空间模板");
                return false;
            }

            var entries = new SpatialTemplateDeploymentEntry3D[
                template.Cells.Count];
            var candidateFootprint = new HashSet<long>();
            var cumulativeCosts = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (var index = 0; index < template.Cells.Count; index++)
            {
                SpatialTemplateCell cell = template.Cells[index];
                BuildingDefinition definition = FindBuilding(
                    cell.BuildingDefinitionId);
                int worldX = anchorX + cell.X;
                int worldY = anchorY + cell.Y;
                if (definition == null)
                {
                    failure = Failure(
                        GrayboxSpatialTemplateFailureKind3D.UnknownBuilding,
                        cell.BuildingDefinitionId,
                        worldX,
                        worldY,
                        BuildingPlacementFailure.MissingReference,
                        "空间模板包含未知建筑：" +
                        cell.BuildingDefinitionId);
                    return false;
                }

                var orientation =
                    (BuildingOrientation)cell.RotationQuarterTurns;
                BuildingPlacementEvaluation evaluation =
                    placement.EvaluateGroundPlacement(
                        definition,
                        orientation,
                        worldX,
                        worldY);
                if (!evaluation.IsValid)
                {
                    failure = PlacementFailure(
                        definition,
                        worldX,
                        worldY,
                        evaluation.PrimaryFailure);
                    return false;
                }

                for (var footprintIndex = 0;
                     footprintIndex < evaluation.Footprint.Count;
                     footprintIndex++)
                {
                    BuildingCell footprint =
                        evaluation.Footprint[footprintIndex];
                    if (!candidateFootprint.Add(
                            CellKey(footprint.X, footprint.Y)))
                    {
                        failure = Failure(
                            GrayboxSpatialTemplateFailureKind3D.TemplateOverlap,
                            definition.Id.Value,
                            worldX,
                            worldY,
                            BuildingPlacementFailure.Overlap,
                            definition.Name + " 与模板内其它建筑占格重叠");
                        return false;
                    }
                }

                cumulativeCosts.TryGetValue(
                    definition.CostId,
                    out int previousCost);
                long totalCost = (long)previousCost + definition.Cost;
                if (totalCost > int.MaxValue ||
                    totalCost > placement.GetAvailableConstructionMaterialAmount(
                        definition.CostId))
                {
                    failure = PlacementFailure(
                        definition,
                        worldX,
                        worldY,
                        BuildingPlacementFailure.InsufficientMaterials);
                    return false;
                }
                cumulativeCosts[definition.CostId] = (int)totalCost;
                entries[index] = new SpatialTemplateDeploymentEntry3D(
                    definition,
                    orientation,
                    worldX,
                    worldY);
            }

            plan = new GrayboxSpatialTemplateDeploymentPlan3D(this, entries);
            failure = default;
            return true;
        }

        public bool TryCommitDeployment(
            GrayboxSpatialTemplateDeploymentPlan3D plan,
            out IReadOnlyList<GrayboxBuildingInstance3D> created,
            out GrayboxSpatialTemplateFailure3D failure)
        {
            created = Array.Empty<GrayboxBuildingInstance3D>();
            if (plan == null ||
                !ReferenceEquals(plan.Owner, this) ||
                plan.Consumed)
            {
                failure = Failure(
                    GrayboxSpatialTemplateFailureKind3D.InvalidPlan,
                    null,
                    0,
                    0,
                    BuildingPlacementFailure.None,
                    "空间模板部署计划无效、已消费或属于其它控制器");
                return false;
            }

            plan.Consumed = true;
            var committed = new List<GrayboxBuildingInstance3D>(
                plan.Entries.Length);
            for (var index = 0; index < plan.Entries.Length; index++)
            {
                SpatialTemplateDeploymentEntry3D entry = plan.Entries[index];
                try
                {
                    if (!placement.TryBeginGroundConstruction(
                            entry.Definition,
                            entry.Orientation,
                            entry.WorldX,
                            entry.WorldY,
                            out GrayboxBuildingInstance3D instance,
                            out BuildingPlacementEvaluation evaluation))
                    {
                        Rollback(committed);
                        failure = PlacementFailure(
                            entry.Definition,
                            entry.WorldX,
                            entry.WorldY,
                            evaluation.PrimaryFailure);
                        return false;
                    }
                    committed.Add(instance);
                }
                catch (Exception exception)
                {
                    try
                    {
                        Rollback(committed);
                    }
                    catch (Exception rollbackFailure)
                    {
                        throw new AggregateException(
                            "空间模板提交失败且无法完整回滚",
                            exception,
                            rollbackFailure);
                    }
                    failure = Failure(
                        GrayboxSpatialTemplateFailureKind3D.CommitFailed,
                        entry.Definition.Id.Value,
                        entry.WorldX,
                        entry.WorldY,
                        BuildingPlacementFailure.None,
                        entry.Definition.Name + " 提交异常：" +
                        exception.Message);
                    return false;
                }
            }

            created = committed.ToArray();
            failure = default;
            return true;
        }

        public bool TryDeploy(
            string templateId,
            int anchorX,
            int anchorY,
            out IReadOnlyList<GrayboxBuildingInstance3D> created,
            out GrayboxSpatialTemplateFailure3D failure)
        {
            created = Array.Empty<GrayboxBuildingInstance3D>();
            return TryPrepareDeployment(
                       templateId,
                       anchorX,
                       anchorY,
                       out GrayboxSpatialTemplateDeploymentPlan3D plan,
                       out failure) &&
                   TryCommitDeployment(plan, out created, out failure);
        }

        private void Rollback(
            IReadOnlyList<GrayboxBuildingInstance3D> committed)
        {
            for (var index = committed.Count - 1; index >= 0; index--)
            {
                GrayboxBuildingInstance3D instance = committed[index];
                int expectedRefund = instance.Placement.Definition.Cost;
                if (!placement.TryCancelUnderConstruction(
                        instance.StableInstanceId,
                        out int acceptedRefund) ||
                    acceptedRefund != expectedRefund)
                {
                    throw new InvalidOperationException(
                        "空间模板回滚未能移除并全额退款：" +
                        instance.Placement.Definition.Name);
                }
            }
        }

        private SpatialTemplateDefinition FindTemplate(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                return null;
            IReadOnlyList<SpatialTemplateDefinition> templates =
                runtime.Capture().Templates;
            for (var index = 0; index < templates.Count; index++)
                if (string.Equals(
                        templates[index].Id,
                        templateId,
                        StringComparison.Ordinal))
                    return templates[index];
            return null;
        }

        private static BuildingDefinition FindBuilding(string stableId)
        {
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition candidate = BuildingCatalog.All[index];
                if (string.Equals(
                        candidate.Id.Value,
                        stableId,
                        StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private static GrayboxSpatialTemplateFailure3D PlacementFailure(
            BuildingDefinition definition,
            int worldX,
            int worldY,
            BuildingPlacementFailure primaryFailure)
        {
            return Failure(
                GrayboxSpatialTemplateFailureKind3D.InvalidPlacement,
                definition.Id.Value,
                worldX,
                worldY,
                primaryFailure,
                definition.Name + "：" +
                GrayboxBuildingMenuView3D.PlacementFailureMessage(
                    primaryFailure));
        }

        private static GrayboxSpatialTemplateFailure3D Failure(
            GrayboxSpatialTemplateFailureKind3D kind,
            string buildingDefinitionId,
            int worldX,
            int worldY,
            BuildingPlacementFailure primaryFailure,
            string reason)
        {
            return new GrayboxSpatialTemplateFailure3D(
                kind,
                buildingDefinitionId,
                worldX,
                worldY,
                primaryFailure,
                reason);
        }

        private static long CellKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }
    }
}
