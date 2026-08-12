# 项目自动清单

本附录为自动生成内容，由目录和项目快照生成；请不要手工修改。工具架构版本：1，内容指纹：`ec891a3279f9cbfd66d900ddf41a9dceafe11b1dd4724a1e4d1b53e578df66e1`。

## 1. 生成说明与内容指纹
- 指纹只来自已提供的目录和项目快照，不读取当前时间、Git 提交或机器路径。
## 2. 程序集
- `WasteCity.ArtIntegration3D`：`Assets/_Game/Scripts/ArtIntegration3D/WasteCity.ArtIntegration3D.asmdef`
- `WasteCity.EditModeTests`：`Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef`
- `WasteCity.Editor`：`Assets/_Game/Editor/WasteCity.Editor.asmdef`
- `WasteCity.Game`：`Assets/_Game/Scripts/WasteCity.Game.asmdef`
- `WasteCity.Graybox3D`：`Assets/_Game/Scripts/Graybox3D/WasteCity.Graybox3D.asmdef`
- `WasteCity.Graybox3D.Building`：`Assets/_Game/Scripts/Graybox3D/Building/WasteCity.Graybox3D.Building.asmdef`
- `WasteCity.PlayModeTests`：`Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef`
## 3. 启用场景与顺序
- 0：`Assets/_Game/Scenes/GrayboxPrototype3D.unity`（三维灰盒原型）
- 1：`Assets/_Game/Scenes/FormalPrototype.unity`（正式二维原型）
## 4. 按功能分组的生产文件
- 建筑建造与疏散（`building-construction-evacuation`）：`Assets/_Game/Scripts/Building/**`、`Assets/_Game/Scripts/Graybox3D/Building/*.cs`
- 城市导航与部署（`city-navigation-deployment`）：`Assets/_Game/Scripts/City/**`、`Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs`、`Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs`
- 战斗与路线（`combat-routes`）：`Assets/_Game/Scripts/Combat/**`、`Assets/_Game/Scripts/Content/RouteContentDisplayCatalog.cs`
- 经济生产与物流（`economy-production-logistics`）：`Assets/_Game/Scripts/Building/LogisticsNetworkModel.cs`、`Assets/_Game/Scripts/Economy/**`
- 基础时钟与会话（`foundation-clock`）：`Assets/_Game/Scripts/Content/StableId.cs`、`Assets/_Game/Scripts/Core/**`
- 冻结二维回归（`frozen-2d-regression`）：`Assets/_Game/Scenes/FormalPrototype.unity`、`Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`、`Assets/_Game/Scripts/City/PlaceholderMobileCity.cs`、`Assets/_Game/Scripts/Combat/FormalCombatController.cs`、`Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs`、`Assets/_Game/Scripts/Combat/FormalTechnologyRouteController.cs`、`Assets/_Game/Scripts/Combat/PlaceholderBehemoth.cs`、`Assets/_Game/Scripts/Combat/PlaceholderBossEncounter.cs`、`Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs`、`Assets/_Game/Scripts/Combat/PlaceholderPuppet.cs`、`Assets/_Game/Scripts/Core/FormalGameClockController.cs`、`Assets/_Game/Scripts/Core/FormalSessionController.cs`、`Assets/_Game/Scripts/Core/FormalSessionStatisticsController.cs`、`Assets/_Game/Scripts/Economy/FormalEconomyController.cs`、`Assets/_Game/Scripts/Leader/FormalLeaderController.cs`、`Assets/_Game/Scripts/Legacy/**`、`Assets/_Game/Scripts/Narrative/FormalGuidanceController.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveController.cs`、`Assets/_Game/Scripts/Population/FormalPopulationController.cs`、`Assets/_Game/Scripts/Progression/FormalAdvancementController.cs`、`Assets/_Game/Scripts/Progression/FormalProgressionController.cs`、`Assets/_Game/Scripts/UI/FormalPlaceholderHud.cs`、`Assets/_Game/Scripts/UI/FormalTitleMenuController.cs`、`Assets/_Game/Scripts/World/FormalCameraController.cs`、`Assets/_Game/Scripts/World/FormalDroneController.cs`、`Assets/_Game/Scripts/World/PlaceholderWorldView.cs`
- 领袖直接控制（`leader-direct-control`）：`Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs`、`Assets/_Game/Scripts/Leader/**`
- 持久化与迁移（`persistence-migration`）：`Assets/_Game/Scripts/Persistence/**`
- 展示与美术整合（`presentation-art-integration`）：`Assets/_Game/Scripts/ArtIntegration3D/**`、`Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxMeshBuilder.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs`、`Assets/_Game/Scripts/Graybox3D/IGrayboxInputInterceptor.cs`、`Assets/_Game/Scripts/Graybox3D/IGrayboxTerrainPresentation3D.cs`、`Assets/_Game/Scripts/Presentation/**`
- 研究与人口（`research-population`）：`Assets/_Game/Scripts/Narrative/**`、`Assets/_Game/Scripts/Population/**`、`Assets/_Game/Scripts/Progression/**`、`Assets/_Game/Scripts/Research/**`
- 场景编辑构建与性能（`scene-editor-build-performance`）：`Assets/_Game/Editor/FirstArtPassImportPolicy.cs`、`Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs`、`Assets/_Game/Editor/FirstArtTerrainEvidenceCapture.cs`、`Assets/_Game/Editor/FormalBuildTools.cs`、`Assets/_Game/Editor/FormalProjectSetup.cs`、`Assets/_Game/Editor/GrayboxPerformanceProbe.cs`、`Assets/_Game/Editor/GrayboxSceneAuthoring.cs`、`Assets/_Game/Editor/ProjectQuality/ProjectDocumentationGenerator.cs`、`Assets/_Game/Editor/ProjectQuality/ProjectQualityCatalogLoader.cs`、`Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs`、`Assets/_Game/Editor/ProjectQuality/ProjectQualityScanner.cs`、`Assets/_Game/Editor/ProjectQuality/ProjectQualityValidator.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxUrpScope.cs`
- 界面与输入（`ui-input`）：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxUiInputGuard3D.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs`、`Assets/_Game/Scripts/UI/**`
- 世界地图与地形（`world-terrain`）：`Assets/_Game/Art/FirstPass/Environment/Terrain/**`、`Assets/_Game/Scripts/World/**`
## 5. MonoBehaviour 组件
- `WasteCity.ArtIntegration3D.FirstArtTerrainRenderer3D`：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`
- `WasteCity.Building.BuildingRuntime`：`Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- `WasteCity.Building.PlaceholderAutomatedRepairBay`：`Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- `WasteCity.Building.PlaceholderBuildingController`：`Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- `WasteCity.Building.PlaceholderShieldGenerator`：`Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- `WasteCity.Building.PlaceholderTurret`：`Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- `WasteCity.City.PlaceholderMobileCity`：`Assets/_Game/Scripts/City/PlaceholderMobileCity.cs`
- `WasteCity.Combat.EnemyInfectionStatus`：`Assets/_Game/Scripts/Combat/EnemyInfectionStatus.cs`
- `WasteCity.Combat.EnemyPsionicResonanceStatus`：`Assets/_Game/Scripts/Combat/EnemyPsionicResonanceStatus.cs`
- `WasteCity.Combat.EnemySwordIntentStatus`：`Assets/_Game/Scripts/Combat/EnemySwordIntentStatus.cs`
- `WasteCity.Combat.FormalCombatController`：`Assets/_Game/Scripts/Combat/FormalCombatController.cs`
- `WasteCity.Combat.FormalFriendlyUnitController`：`Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs`
- `WasteCity.Combat.FormalTechnologyRouteController`：`Assets/_Game/Scripts/Combat/FormalTechnologyRouteController.cs`
- `WasteCity.Combat.FriendlyUnitAgent`：`Assets/_Game/Scripts/Combat/FriendlyUnitAgent.cs`
- `WasteCity.Combat.HealthComponent`：`Assets/_Game/Scripts/Combat/HealthComponent.cs`
- `WasteCity.Combat.PlaceholderBehemoth`：`Assets/_Game/Scripts/Combat/PlaceholderBehemoth.cs`
- `WasteCity.Combat.PlaceholderBossEncounter`：`Assets/_Game/Scripts/Combat/PlaceholderBossEncounter.cs`
- `WasteCity.Combat.PlaceholderEnemy`：`Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs`
- `WasteCity.Combat.PlaceholderPuppet`：`Assets/_Game/Scripts/Combat/PlaceholderPuppet.cs`
- `WasteCity.Combat.PlaceholderTimedVisual`：`Assets/_Game/Scripts/Combat/PlaceholderBossEncounter.cs`
- `WasteCity.Core.FormalGameBootstrap`：`Assets/_Game/Scripts/Core/FormalGameBootstrap.cs`
- `WasteCity.Core.FormalGameClockController`：`Assets/_Game/Scripts/Core/FormalGameClockController.cs`
- `WasteCity.Core.FormalSessionController`：`Assets/_Game/Scripts/Core/FormalSessionController.cs`
- `WasteCity.Core.FormalSessionStatisticsController`：`Assets/_Game/Scripts/Core/FormalSessionStatisticsController.cs`
- `WasteCity.Core.GameSpeedController`：`Assets/_Game/Scripts/Core/GameSpeedController.cs`
- `WasteCity.Economy.ElixirController`：`Assets/_Game/Scripts/Economy/ElixirController.cs`
- `WasteCity.Economy.FormalEconomyController`：`Assets/_Game/Scripts/Economy/FormalEconomyController.cs`
- `WasteCity.Economy.TechnologyProductionController`：`Assets/_Game/Scripts/Economy/TechnologyProductionController.cs`
- `WasteCity.Graybox3D.Building.GrayboxBuildingInputRouter3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxBuildingInteractionModel3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInteractionModel3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxBuildingMenuView3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxBuildingPlacementController3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxBuildingSession3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxBuildingSurfaceProjector3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSurfaceProjector3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxBuildingWorldView3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxConstructionController3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxConstructionController3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxDeveloperModifierBootstrap3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs`
- `WasteCity.Graybox3D.Building.GrayboxEvacuationController3D`：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- `WasteCity.Graybox3D.GrayboxCameraController3D`：`Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs`
- `WasteCity.Graybox3D.GrayboxDirectControlCoordinator`：`Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs`
- `WasteCity.Graybox3D.GrayboxGroundProjector`：`Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs`
- `WasteCity.Graybox3D.GrayboxInputRouter`：`Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs`
- `WasteCity.Graybox3D.GrayboxLeaderController3D`：`Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs`
- `WasteCity.Graybox3D.GrayboxMobileCityController3D`：`Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs`
- `WasteCity.Graybox3D.GrayboxSceneBootstrap`：`Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs`
- `WasteCity.Graybox3D.GrayboxUrpScope`：`Assets/_Game/Scripts/Graybox3D/GrayboxUrpScope.cs`
- `WasteCity.Graybox3D.GrayboxVisualSlot`：`Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs`
- `WasteCity.Graybox3D.GrayboxWorldView3D`：`Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs`
- `WasteCity.Leader.FormalLeaderController`：`Assets/_Game/Scripts/Leader/FormalLeaderController.cs`
- `WasteCity.Legacy.ForesightFlashController`：`Assets/_Game/Scripts/Legacy/ForesightFlashController.cs`
- `WasteCity.Legacy.LegacyEffectsController`：`Assets/_Game/Scripts/Legacy/LegacyEffectsController.cs`
- `WasteCity.Legacy.LegacySelectionController`：`Assets/_Game/Scripts/Legacy/LegacySelectionController.cs`
- `WasteCity.Legacy.LocalHasteController`：`Assets/_Game/Scripts/Legacy/LocalHasteController.cs`
- `WasteCity.Legacy.RewindAnchorController`：`Assets/_Game/Scripts/Legacy/RewindAnchorController.cs`
- `WasteCity.Legacy.SpatialTemplateController`：`Assets/_Game/Scripts/Legacy/SpatialTemplateController.cs`
- `WasteCity.Legacy.TerritoryCacheController`：`Assets/_Game/Scripts/Legacy/TerritoryCacheController.cs`
- `WasteCity.Narrative.FormalGuidanceController`：`Assets/_Game/Scripts/Narrative/FormalGuidanceController.cs`
- `WasteCity.Persistence.FormalSaveController`：`Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- `WasteCity.Population.FormalPopulationController`：`Assets/_Game/Scripts/Population/FormalPopulationController.cs`
- `WasteCity.Presentation.VisualLibraryProvider`：`Assets/_Game/Scripts/Presentation/VisualLibraryProvider.cs`
- `WasteCity.Presentation.VisualSlot`：`Assets/_Game/Scripts/Presentation/VisualSlot.cs`
- `WasteCity.Progression.FormalAdvancementController`：`Assets/_Game/Scripts/Progression/FormalAdvancementController.cs`
- `WasteCity.Progression.FormalProgressionController`：`Assets/_Game/Scripts/Progression/FormalProgressionController.cs`
- `WasteCity.Research.ResearchController`：`Assets/_Game/Scripts/Research/ResearchController.cs`
- `WasteCity.UI.FormalPlaceholderHud`：`Assets/_Game/Scripts/UI/FormalPlaceholderHud.cs`
- `WasteCity.UI.FormalTitleMenuController`：`Assets/_Game/Scripts/UI/FormalTitleMenuController.cs`
- `WasteCity.UI.OnboardingGuideController`：`Assets/_Game/Scripts/UI/OnboardingGuideController.cs`
- `WasteCity.World.FormalCameraController`：`Assets/_Game/Scripts/World/FormalCameraController.cs`
- `WasteCity.World.FormalDroneController`：`Assets/_Game/Scripts/World/FormalDroneController.cs`
- `WasteCity.World.PlaceholderWorldView`：`Assets/_Game/Scripts/World/PlaceholderWorldView.cs`
- `WasteCity.World.RescueSiteController`：`Assets/_Game/Scripts/World/RescueSiteController.cs`
- `WasteCity.World.WorldExplorationController`：`Assets/_Game/Scripts/World/WorldExplorationController.cs`
## 6. ScriptableObject 资源
- `WasteCity.ArtIntegration3D.FirstArtTerrainProfile3D`：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`
- `WasteCity.Presentation.VisualDefinition`：`Assets/_Game/Scripts/Presentation/VisualDefinition.cs`
- `WasteCity.Presentation.VisualLibrary`：`Assets/_Game/Scripts/Presentation/VisualLibrary.cs`
## 7. 界面所有者
- 灰盒建筑菜单：`GrayboxBuildingMenuView3D`，场景 `graybox-prototype-3d`
- 灰盒输入路由：`GrayboxInputRouter`，场景 `graybox-prototype-3d`
## 8. 编辑器、构建与性能入口
- `WasteCity.Editor.FirstArtTerrainAssetBuilder.BuildRuntimeAssets`
- `WasteCity.Editor.FirstArtTerrainAssetBuilder.BuildTextureArrays`
- `WasteCity.Editor.FirstArtTerrainEvidenceCapture.CancelCapture`
- `WasteCity.Editor.FirstArtTerrainEvidenceCapture.CaptureAll`
- `WasteCity.Editor.FirstArtTerrainEvidenceCapture.CaptureAllAcceptedDeviationFromEnvironment`
- `WasteCity.Editor.FirstArtTerrainEvidenceCapture.StartAutomatedCapture`
- `WasteCity.Editor.FormalBuildTools.BuildWindows`
- `WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3D`
- `WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment`
- `WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D`
- `WasteCity.Editor.GrayboxPerformanceProbe.MeasureBuildingPerformance`
- `WasteCity.Editor.GrayboxPerformanceProbe.MeasureFirstArtTerrainPerformance`
- `WasteCity.Editor.GrayboxPerformanceProbe.MeasureWorldGeneration`
- `WasteCity.Editor.GrayboxPerformanceProbe.RecordFirstArtTerrainRuntimeEvidence`
- `WasteCity.Editor.GrayboxPerformanceProbe.SummarizeGuiProfilerCapture`
- `WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity`
- `WasteCity.Editor.GrayboxSceneAuthoring.Configure`
## 9. 美术接入与稳定展示路径
- `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`
- `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`
- `Assets/_Game/Scripts/Presentation/VisualSlot.cs`
## 10. 明确排除项
- 无。
