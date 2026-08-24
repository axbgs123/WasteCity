using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.City;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxWorldCitySaveAdapter3D
    {
        public const string WorldDefinitionId = "core.world.formal-3d";
        public const int WorldGenerationVersion =
            FormalWorldGenerationCatalog3D.WorldGenerationVersion;
        public const string WorldConfigurationSignature =
            FormalWorldGenerationCatalog3D.WorldConfigurationSignature;

        private readonly GrayboxSceneBootstrap bootstrap;
        private readonly GrayboxMobileCityController3D city;
        private readonly GrayboxBuildingSession3D session;

        public GrayboxWorldCitySaveAdapter3D(
            GrayboxSceneBootstrap bootstrap,
            GrayboxMobileCityController3D city,
            GrayboxBuildingSession3D session)
        {
            this.bootstrap = bootstrap ??
                throw new ArgumentNullException(nameof(bootstrap));
            this.city = city ?? throw new ArgumentNullException(nameof(city));
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
        }

        public FormalThreeDWorldSaveData CaptureWorld()
        {
            WorldMapModel world = bootstrap.World ??
                throw new InvalidOperationException(
                    "正式 3D 世界尚未初始化");
            var nodes = new List<FormalThreeDResourceNodeSaveData>(
                world.ResourceNodeCount);
            for (var x = 0; x < world.Width; x++)
            for (var y = 0; y < world.Height; y++)
            {
                WorldCell cell = world.Get(x, y);
                if (!cell.HasResource) continue;
                nodes.Add(new FormalThreeDResourceNodeSaveData
                {
                    stableNodeId =
                        GrayboxResourceNodeIdentity3D.Create(x, y),
                    x = x,
                    y = y,
                    resourceId = cell.ResourceId,
                    remainingAmount = cell.ResourceAmount,
                    isDepleted = cell.ResourceAmount == 0,
                });
            }
            nodes.Sort((left, right) => string.CompareOrdinal(
                left.stableNodeId,
                right.stableNodeId));
            WorldOrphanResource[] worldOrphans =
                world.CaptureOrphanResources();
            var orphans = new FormalThreeDOrphanResourceSaveData[
                worldOrphans.Length];
            for (var index = 0; index < worldOrphans.Length; index++)
            {
                WorldOrphanResource orphan = worldOrphans[index];
                orphans[index] = new FormalThreeDOrphanResourceSaveData
                {
                    resourceId = orphan.ResourceId,
                    amount = orphan.Amount,
                    ownerKind = orphan.OwnerKind,
                    ownerStableId = orphan.OwnerStableId,
                };
            }
            Array.Sort(orphans, CompareOrphans);
            return new FormalThreeDWorldSaveData
            {
                worldDefinitionId = WorldDefinitionId,
                worldGenerationVersion = WorldGenerationVersion,
                worldSeed = bootstrap.CurrentWorldSeed,
                width = world.Width,
                height = world.Height,
                configurationSignature = WorldConfigurationSignature,
                resourceNodes = nodes.ToArray(),
                orphanResources = orphans,
            };
        }

        public FormalThreeDCitySaveData CaptureCity()
        {
            if (!city.TryGetCurrentCell(out int cellX, out int cellY))
                throw new InvalidOperationException(
                    "正式 3D 城市不在有效世界格位内");
            Vector3 position = city.WorldPosition;
            WorldGridPoint? destination = city.Destination;
            return new FormalThreeDCitySaveData
            {
                positionX = position.x,
                positionZ = position.z,
                cellX = cellX,
                cellY = cellY,
                autopilotActive = city.AutopilotActive,
                destinationX = destination?.X ?? 0,
                destinationY = destination?.Y ?? 0,
                cityMode = (int)city.Mode,
                transitionReturnMode =
                    (int)city.Deployment.TransitionReturnMode,
                transitionRemainingSeconds = city.Deployment.Remaining,
                population = session.Population,
                populationCapacity = session.PopulationCapacity,
            };
        }

        public bool TryRestore(
            FormalThreeDWorldSaveData worldData,
            FormalThreeDCitySaveData cityData,
            out string error)
        {
            if (!bootstrap.CanRestoreWorld(out error) ||
                !city.CanRestoreForPersistence(bootstrap.World, out error) ||
                !session.CanRestorePopulation(out error))
                return false;
            if (!TryBuildWorld(worldData, out WorldMapModel world, out error))
                return false;
            if (!TryValidateCity(world, cityData, out error))
                return false;

            if (!bootstrap.TryRestoreWorld(
                    world,
                    worldData.worldSeed,
                    out error))
                return false;
            if (!session.TryRestorePopulation(
                    cityData.population,
                    cityData.populationCapacity,
                    out error))
                return false;

            Vector3 position = new Vector3(
                cityData.positionX,
                city.WorldPosition.y,
                cityData.positionZ);
            return city.TryRestoreForPersistence(
                position,
                (CityMode)cityData.cityMode,
                (CityMode)cityData.transitionReturnMode,
                cityData.transitionRemainingSeconds,
                cityData.autopilotActive,
                cityData.destinationX,
                cityData.destinationY,
                out error);
        }

        private static bool TryBuildWorld(
            FormalThreeDWorldSaveData data,
            out WorldMapModel world,
            out string error)
        {
            world = null;
            if (data == null)
            {
                error = "世界存档数据为空";
                return false;
            }
            if (data.worldDefinitionId != WorldDefinitionId ||
                data.worldGenerationVersion != WorldGenerationVersion ||
                data.configurationSignature != WorldConfigurationSignature)
            {
                error = "存档世界配置与当前正式世界不兼容";
                return false;
            }
            if (data.width != GrayboxWorldLayout3D.WorldWidth ||
                data.height != GrayboxWorldLayout3D.WorldHeight)
            {
                error = "存档世界尺寸与当前正式世界不一致";
                return false;
            }
            if (data.resourceNodes == null || data.orphanResources == null)
            {
                error = "世界资源节点数据缺失";
                return false;
            }
            WorldMapModel rebuilt = GrayboxWorldLayout3D.Create(
                data.worldSeed);
            if (data.resourceNodes.Length != rebuilt.ResourceNodeCount)
            {
                error = "存档资源节点数量与正式世界不一致";
                return false;
            }
            var savedById =
                new Dictionary<string, FormalThreeDResourceNodeSaveData>(
                    StringComparer.Ordinal);
            for (var index = 0; index < data.resourceNodes.Length; index++)
            {
                FormalThreeDResourceNodeSaveData node =
                    data.resourceNodes[index];
                if (node == null ||
                    string.IsNullOrWhiteSpace(node.stableNodeId) ||
                    savedById.ContainsKey(node.stableNodeId))
                {
                    error = "存档资源节点身份为空或重复";
                    return false;
                }
                savedById.Add(node.stableNodeId, node);
            }

            int[] amounts = rebuilt.CaptureResourceAmounts();
            for (var x = 0; x < rebuilt.Width; x++)
            for (var y = 0; y < rebuilt.Height; y++)
            {
                WorldCell cell = rebuilt.Get(x, y);
                if (!cell.HasResource) continue;
                string stableId =
                    GrayboxResourceNodeIdentity3D.Create(x, y);
                if (!savedById.TryGetValue(
                        stableId,
                        out FormalThreeDResourceNodeSaveData saved) ||
                    saved.x != x || saved.y != y ||
                    !string.Equals(
                        saved.resourceId,
                        cell.ResourceId,
                        StringComparison.Ordinal) ||
                    saved.remainingAmount < 0 ||
                    saved.isDepleted != (saved.remainingAmount == 0))
                {
                    error = "存档资源节点与正式世界定义不一致：" +
                            stableId;
                    return false;
                }
                amounts[y * rebuilt.Width + x] = saved.remainingAmount;
            }
            if (!rebuilt.TryRestoreResourceAmounts(amounts, out error))
                return false;
            var orphans = new WorldOrphanResource[
                data.orphanResources.Length];
            for (var index = 0;
                 index < data.orphanResources.Length;
                 index++)
            {
                FormalThreeDOrphanResourceSaveData orphan =
                    data.orphanResources[index];
                if (orphan == null)
                {
                    error = "孤立资源记录不能为空";
                    return false;
                }
                orphans[index] = new WorldOrphanResource(
                    orphan.resourceId,
                    orphan.amount,
                    orphan.ownerKind,
                    orphan.ownerStableId);
            }
            if (!rebuilt.TryRestoreOrphanResources(orphans, out error))
                return false;
            world = rebuilt;
            error = string.Empty;
            return true;
        }

        private static bool TryValidateCity(
            WorldMapModel world,
            FormalThreeDCitySaveData data,
            out string error)
        {
            if (data == null)
            {
                error = "城市存档数据为空";
                return false;
            }
            if (!IsFinite(data.positionX) || !IsFinite(data.positionZ) ||
                data.population < 0 || data.populationCapacity < 0)
            {
                error = "城市位置或人口数据无效";
                return false;
            }
            var mapper = new PlanarCoordinateMapper3D(
                world.Width,
                world.Height);
            var position = new Vector3(
                data.positionX,
                0f,
                data.positionZ);
            if (!mapper.TryWorldToCell(
                    position,
                    out int cellX,
                    out int cellY) ||
                cellX != data.cellX || cellY != data.cellY)
            {
                error = "城市位置与权威格位不一致";
                return false;
            }

            var deployment = new CityDeploymentModel(
                CityDeploymentRules.FormalDeployDurationSeconds,
                CityDeploymentRules.FormalPackDurationSeconds);
            if (!deployment.TryRestore(
                    (CityMode)data.cityMode,
                    (CityMode)data.transitionReturnMode,
                    data.transitionRemainingSeconds,
                    out error))
                return false;
            if (data.autopilotActive)
            {
                if ((CityMode)data.cityMode != CityMode.Mobile ||
                    !CityPathfinder.TryFindPath(
                        world,
                        cellX,
                        cellY,
                        data.destinationX,
                        data.destinationY,
                        out WorldGridPoint[] route) ||
                    route.Length == 0)
                {
                    error = "存档中的自动驾驶目标不可达";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int CompareOrphans(
            FormalThreeDOrphanResourceSaveData left,
            FormalThreeDOrphanResourceSaveData right)
        {
            int comparison = string.CompareOrdinal(
                left.ownerKind,
                right.ownerKind);
            if (comparison != 0) return comparison;
            comparison = string.CompareOrdinal(
                left.ownerStableId,
                right.ownerStableId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.resourceId, right.resourceId);
        }
    }
}
