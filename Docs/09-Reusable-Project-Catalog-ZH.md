# 废土移动城市可复用项目目录

## 适合谁看

适合想在不破坏现有项目的前提下复用内容的策划、美术、试玩协作者和新开发者。EditMode（不启动游戏画面、在编辑器里检查规则和资料的测试）与 PlayMode（启动游戏流程、检查实际互动的测试）说明改后该跑哪类检查；组件（挂在场景物体上的一小块功能）、程序集（把相关代码一起编译成可用单元的集合）和稳定 ID（不随改名或资源替换而变化的固定编号）帮助你在项目里找到对应内容。

本页只把经过挑选的复用入口翻译成日常语言；完整文件和精确技术库存以[项目自动清单](Generated/Project-Inventory-ZH.md)为准，测试类以[测试自动清单](Generated/Test-Inventory-ZH.md)为准，批准状态以[用户反馈与变更控制](06-User-Feedback-and-Change-Control-ZH.md)为准。

## 五级复用说明

- **推荐复用**：适合优先选用，仍要跑列出的测试。
- **复用前审查**：能用，但先检查场景、兼容性或现有约束。
- **仅限场景**：只适合当前场景内部，不能抽成通用规则。
- **冻结回归**：保留来确认旧功能没有倒退，不作为新功能起点。
- **禁止用于新功能**：只为旧回归兼容保留，新功能不得使用。

## 内容与稳定编号

### 稳定标识（推荐复用）

能解决什么：让同一内容跨系统仍能被认出。在哪里：`Assets/_Game/Scripts/Content/StableId.cs`。怎么复用：为需要长期引用的内容使用稳定 ID。不能负责什么：不生成业务实体。改后跑哪组测试：`FoundationTests`。代码名：`StableId`。

## 世界、城市和坐标

### 世界地图模型（推荐复用）

能解决什么：保存世界地图状态。在哪里：`Assets/_Game/Scripts/World/WorldMapModel.cs`。怎么复用：把地图状态放进此模型而不是塞进显示层。不能负责什么：不处理场景渲染。改后跑哪组测试：`WorldMapTests`。代码名：`WorldMapModel`。

### 三维世界布局（复用前审查）

能解决什么：安排三维灰盒世界的位置。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs`。怎么复用：在修改布局前先复核现有场景。不能负责什么：不能替你判断新玩法规则。改后跑哪组测试：`GrayboxWorldLayout3DTests`。代码名：`GrayboxWorldLayout3D`。

### 平面坐标映射（推荐复用）

能解决什么：在世界位置和地图平面位置之间换算。在哪里：`Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs`。怎么复用：统一通过它转换坐标。不能负责什么：不决定城市规则。改后跑哪组测试：`PlanarCoordinateMapper3DTests`。代码名：`PlanarCoordinateMapper3D`。

### 城市寻路（推荐复用）

能解决什么：搜索城市可走的路线。在哪里：`Assets/_Game/Scripts/City/CityPathfinder.cs`。怎么复用：把路线搜索交给它。不能负责什么：不处理部署消耗。改后跑哪组测试：`CityPathfinderTests`。代码名：`CityPathfinder`。

### 城市地形规则（推荐复用）

能解决什么：检查城市是否符合地形条件。在哪里：`Assets/_Game/Scripts/City/CityTerrainRules.cs`。怎么复用：在行动前调用它校验地形。不能负责什么：不负责路径计算。改后跑哪组测试：`CityTerrainRulesTests`。代码名：`CityTerrainRules`。

### 城市部署规则（推荐复用）

能解决什么：判定城市能否部署。在哪里：`Assets/_Game/Scripts/City/CityDeploymentRules.cs`。怎么复用：将合法性判断交给它。不能负责什么：不渲染部署预览。改后跑哪组测试：`CityDeploymentRulesTests`。代码名：`CityDeploymentRules`。

### 直接控制规则（推荐复用）

能解决什么：维持直接控制状态的规则。在哪里：`Assets/_Game/Scripts/City/DirectControlRules.cs`。怎么复用：由协调者调用规则，不把状态判断散落在界面里。不能负责什么：不处理领袖动画。改后跑哪组测试：`DirectControlRulesTests`。代码名：`DirectControlRules`。

## 建造与撤离

### 建筑目录（复用前审查）

能解决什么：展示可见的建筑目录。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs`。怎么复用：先审查它与当前三维场景的关系再接入。不能负责什么：不提交建筑放置。改后跑哪组测试：`GrayboxBuildingCatalogTests`。代码名：`GrayboxBuildingCatalogPresenter3D`。

### 建筑网格（推荐复用）

能解决什么：计算建筑可用格位。在哪里：`Assets/_Game/Scripts/Building/BuildingGrid.cs`。怎么复用：把格位计算集中在这里。不能负责什么：不负责输入路由。改后跑哪组测试：`BuildingGridTests`。代码名：`BuildingGrid`。

### 建筑移动规则（推荐复用）

能解决什么：约束建筑何时可随城市移动。在哪里：`Assets/_Game/Scripts/Building/BuildingMobilityRules.cs`。怎么复用：用它做移动前判断。不能负责什么：不决定建筑解锁。改后跑哪组测试：`BuildingMobilityRulesTests`。代码名：`BuildingMobilityRules`。

### 建筑放置规则（推荐复用）

能解决什么：评估建筑能否放下。在哪里：`Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs`。怎么复用：由它给出放置判断。不能负责什么：不管理施工进度。改后跑哪组测试：`BuildingPlacementEvaluationTests`。代码名：`BuildingPlacementEvaluation`。

### 建筑解锁模型（推荐复用）

能解决什么：保存建筑解锁状态。在哪里：`Assets/_Game/Scripts/Building/BuildingUnlockModel.cs`。怎么复用：在规则层读取和更新解锁状态。不能负责什么：不计算升级成本。改后跑哪组测试：`BuildingUnlockTests`。代码名：`BuildingUnlockModel`。

### 施工进度（推荐复用）

能解决什么：跟踪施工是否完成。在哪里：`Assets/_Game/Scripts/Building/ConstructionProgress.cs`。怎么复用：把施工进度交给它记录。不能负责什么：不控制建筑视图。改后跑哪组测试：`ConstructionProgressTests`。代码名：`ConstructionProgress`。

### 施工退款规则（复用前审查）

能解决什么：计算取消施工时的退款。在哪里：`Assets/_Game/Scripts/Building/ConstructionRefundRules.cs`。怎么复用：先审查资源写入边界后再调用。不能负责什么：不写入资源库存。改后跑哪组测试：`ConstructionProgressTests`。代码名：`ConstructionRefundRules`。

### 三维建筑会话（复用前审查）

能解决什么：协调当前三维建造过程。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`。怎么复用：在接入新的三维建造流程前先审查状态关系。不能负责什么：不替代领域建造规则。改后跑哪组测试：`GrayboxBuildingSessionTests`。代码名：`GrayboxBuildingSession3D`。

### 三维建筑世界视图（仅限场景）

能解决什么：在当前场景显示建筑。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`。怎么复用：只在现有三维场景中绑定。不能负责什么：不能当作纯规则模型复用。改后跑哪组测试：`GrayboxBuildingProjectionAndViewTests`。代码名：`GrayboxBuildingWorldView3D`。

## UI 与输入

### 三维建筑输入路由（复用前审查）

能解决什么：把建筑界面的输入送到正确位置。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`。怎么复用：先审查焦点和输入优先级。不能负责什么：不决定建筑放置规则。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`。代码名：`GrayboxBuildingInputRouter3D`。

### 三维建筑菜单视图（复用前审查）

能解决什么：显示建筑菜单。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`。怎么复用：先检查当前界面与场景绑定。不能负责什么：不保存建筑数据。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`。代码名：`GrayboxBuildingMenuView3D`。

## 资源、研究、人口、战斗和存档

### 资源库存（推荐复用）

能解决什么：管理资源数量。在哪里：`Assets/_Game/Scripts/Economy/ResourceInventory.cs`。怎么复用：统一由库存读取和改变数量。不能负责什么：不驱动生产周期。改后跑哪组测试：`FoundationTests`。代码名：`ResourceInventory`。

### 研究模型（推荐复用）

能解决什么：管理研究状态。在哪里：`Assets/_Game/Scripts/Research/ResearchModel.cs`。怎么复用：把研究状态放在这里。不能负责什么：不展示研究界面。改后跑哪组测试：`ResearchTests`。代码名：`ResearchModel`。

### 人口模型（推荐复用）

能解决什么：管理人口容量。在哪里：`Assets/_Game/Scripts/Population/PopulationModel.cs`。怎么复用：由模型维护容量数据。不能负责什么：不控制人口表现。改后跑哪组测试：`PopulationAndCapacityTests`。代码名：`PopulationModel`。

### 正式存档数据（复用前审查）

能解决什么：保存正式存档字段。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveData.cs`。怎么复用：变更前先做兼容性审查。不能负责什么：不替代存档迁移方案。改后跑哪组测试：`FormalSaveTests`。代码名：`FormalSaveData`。

## 3D 表现与美术

### 二维视觉槽位（复用前审查）

能解决什么：挂接二维视觉定义。在哪里：`Assets/_Game/Scripts/Presentation/VisualSlot.cs`。怎么复用：先确认项目目标仍是二维显示。不能负责什么：不适配三维地形。改后跑哪组测试：`VisualSlotTests`。代码名：`VisualSlot`。

### 三维灰盒视觉槽位（仅限场景）

能解决什么：把灰盒视觉绑定到当前场景。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs`。怎么复用：只在该三维场景内使用。不能负责什么：不替代二维槽位。改后跑哪组测试：`GrayboxVisualAndWorldTests`。代码名：`GrayboxVisualSlot`。

### 首版三维地形配置（复用前审查）

能解决什么：定义首版地形参数。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`。怎么复用：先复核导入策略和性能边界。不能负责什么：不直接决定资源导入。改后跑哪组测试：`FirstArtTerrainProfileTests`。代码名：`FirstArtTerrainProfile3D`。

### 首版三维地形渲染（仅限场景）

能解决什么：在场景渲染首版地形。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`。怎么复用：仅在已批准的三维场景接入。不能负责什么：不管理地形资源导入。改后跑哪组测试：`FirstArtTerrainRendererTests`。代码名：`FirstArtTerrainRenderer3D`。

## 场景、构建与检查工具

### 灰盒场景编写（仅限场景）

能解决什么：在编辑器生成灰盒场景。在哪里：`Assets/_Game/Editor/GrayboxSceneAuthoring.cs`。怎么复用：只用于编辑器和指定场景。不能负责什么：不在运行时调用。改后跑哪组测试：`GrayboxSceneContractTests`。代码名：`GrayboxSceneAuthoring`。

### 正式构建工具（复用前审查）

能解决什么：执行正式构建检查。在哪里：`Assets/_Game/Editor/FormalBuildTools.cs`。怎么复用：先审查目标平台和构建配置。不能负责什么：不修改游戏规则。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`。代码名：`FormalBuildTools`。

### 灰盒性能探针（仅限场景）

能解决什么：采集灰盒性能数据。在哪里：`Assets/_Game/Editor/GrayboxPerformanceProbe.cs`。怎么复用：只在已定义的性能场景采样。不能负责什么：不作为发布版逻辑。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`。代码名：`GrayboxPerformanceProbe`。

## 冻结或禁止用于新功能的旧内容

### 正式原型冻结场景（冻结回归）

能解决什么：保留二维旧功能的回归基线。在哪里：`Assets/_Game/Scenes/FormalPrototype.unity` 和 `Assets/_Game/Scripts/Core/FormalGameBootstrap.cs`。怎么复用：只用于确认旧行为未倒退。不能负责什么：不是新功能模板。改后跑哪组测试：`SceneContractTests`。代码名：`FormalGameBootstrap`。

### 占位建筑控制器（禁止用于新功能）

能解决什么：维持旧回归兼容。在哪里：`Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`。怎么复用：不复用到新功能，只在修复旧回归时谨慎维护。不能负责什么：不能作为新的建筑实现。改后跑哪组测试：`TurretAndBuildingTests`。代码名：`PlaceholderBuildingController`。
