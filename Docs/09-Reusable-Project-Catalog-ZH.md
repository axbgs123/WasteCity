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

能解决什么：让同一内容跨系统仍能被认出。在哪里：`Assets/_Game/Scripts/Content/StableId.cs`。怎么复用：用于跨系统保存稳定标识。为需要长期引用的内容使用稳定 ID。不能负责什么：不生成业务实体。改后跑哪组测试：`FoundationTests`。代码名：`StableId`。

## 世界、城市和坐标

### 世界地图模型（推荐复用）

能解决什么：保存世界地图状态。在哪里：`Assets/_Game/Scripts/World/WorldMapModel.cs`。怎么复用：用于保存世界地图状态。把地图状态放进此模型而不是塞进显示层。不能负责什么：不处理场景渲染。改后跑哪组测试：`WorldMapTests`。代码名：`WorldMapModel`。

### 三维世界布局（复用前审查）

能解决什么：安排三维灰盒世界的位置。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs`。怎么复用：用于灰盒世界布局。调整布局前需要场景复核。不能负责什么：不能替你判断新玩法规则。改后跑哪组测试：`GrayboxWorldLayout3DTests`。代码名：`GrayboxWorldLayout3D`。

### 平面坐标映射（推荐复用）

能解决什么：在世界位置和地图平面位置之间换算。在哪里：`Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs`。怎么复用：用于世界与平面坐标转换。统一通过它转换坐标。不能负责什么：不决定城市规则。改后跑哪组测试：`PlanarCoordinateMapper3DTests`。代码名：`PlanarCoordinateMapper3D`。

### 城市寻路（推荐复用）

能解决什么：搜索城市可走的路线。在哪里：`Assets/_Game/Scripts/City/CityPathfinder.cs`。怎么复用：用于城市路径搜索。把路线搜索交给它。不能负责什么：不处理部署消耗。改后跑哪组测试：`CityPathfinderTests`。代码名：`CityPathfinder`。

### 城市地形规则（推荐复用）

能解决什么：检查城市是否符合地形条件。在哪里：`Assets/_Game/Scripts/City/CityTerrainRules.cs`。怎么复用：用于校验城市地形条件。在行动前调用它校验地形。不能负责什么：不负责路径计算。改后跑哪组测试：`CityTerrainRulesTests`。代码名：`CityTerrainRules`。

### 正式城市部署状态（推荐复用）

能解决什么：统一保存正式城市的移动、展开中、要塞和收起中状态，以及转换取消和规则剩余时间。在哪里：`Assets/_Game/Scripts/City/CityDeploymentModel.cs`。怎么复用：作为正式部署状态所有者，维护 Mobile、Deploying、Fortress、Packing、转换取消、规则剩余时间和战斗收起倍率。不能负责什么：只拥有部署状态和规则时间；不判断地形合法性，不处理 Unity 输入或表现，也不进入 schema 30 或接入冻结 2D。改后跑哪组测试：`CityDeploymentRulesTests`、`GrayboxMobileCityController3DTests`。代码名：`CityDeploymentModel`。

### 城市部署规则（推荐复用）

能解决什么：判定城市能否部署。在哪里：`Assets/_Game/Scripts/City/CityDeploymentRules.cs`。怎么复用：用于判定城市部署合法性。将合法性判断交给它。不能负责什么：不渲染部署预览。改后跑哪组测试：`CityDeploymentRulesTests`。代码名：`CityDeploymentRules`。

### 直接控制规则（推荐复用）

能解决什么：维持直接控制状态的规则。在哪里：`Assets/_Game/Scripts/City/DirectControlRules.cs`。怎么复用：用于直接控制状态规则。由协调者调用规则，不把状态判断散落在界面里。不能负责什么：不处理领袖动画。改后跑哪组测试：`DirectControlRulesTests`。代码名：`DirectControlRules`。

## 建造与撤离

### 建筑目录（复用前审查）

能解决什么：展示可见的建筑目录。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs`。怎么复用：用于展示建筑目录。先审查它与当前三维场景的关系再接入。不能负责什么：不提交建筑放置。改后跑哪组测试：`GrayboxBuildingCatalogTests`。代码名：`GrayboxBuildingCatalogPresenter3D`。

### 建筑网格（推荐复用）

能解决什么：计算建筑可用格位。在哪里：`Assets/_Game/Scripts/Building/BuildingGrid.cs`。怎么复用：用于建筑格位计算。把格位计算集中在这里。不能负责什么：不负责输入路由。改后跑哪组测试：`BuildingGridTests`。代码名：`BuildingGrid`。

### 建筑移动规则（推荐复用）

能解决什么：约束建筑何时可随城市移动。在哪里：`Assets/_Game/Scripts/Building/BuildingMobilityRules.cs`。怎么复用：用于约束建筑移动。用它做移动前判断。不能负责什么：不决定建筑解锁。改后跑哪组测试：`BuildingMobilityRulesTests`。代码名：`BuildingMobilityRules`。

### 建筑放置规则（推荐复用）

能解决什么：评估建筑能否放下。在哪里：`Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs`。怎么复用：用于评估建筑放置。由它给出放置判断。不能负责什么：不管理施工进度。改后跑哪组测试：`BuildingPlacementEvaluationTests`。代码名：`BuildingPlacementEvaluation`。

### 资源节点稳定绑定（推荐复用）

能解决什么：让采矿建筑从放置评估到运行时始终持有同一个资源节点身份与地图坐标。在哪里：`Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs`。怎么复用：在合法放置结果、建筑实例和生产状态之间传递同一资源节点的稳定 ID 与地图坐标。不能负责什么：只承载权威放置评估确认的节点身份和坐标；不判断兼容性、放置合法性、储量或物流范围。改后跑哪组测试：`GrayboxProductionLifecycleTests`、`GrayboxProductionRuntimeTests`。代码名：`ResourceNodeBinding`。

### 建筑资源节点兼容规则（推荐复用）

能解决什么：让放置评估与采矿引导共享同一套建筑和资源节点兼容关系。在哪里：`Assets/_Game/Scripts/Building/BuildingResourceNodeCompatibilityRules.cs`。怎么复用：供放置评估与采矿引导共同判断建筑和资源节点是否兼容。不能负责什么：只回答资源类型兼容性；不复制范围、占地、成本、解锁或城市状态判断。改后跑哪组测试：`BuildingResourceNodeCompatibilityRulesTests`。代码名：`BuildingResourceNodeCompatibilityRules`。

### 建筑解锁模型（推荐复用）

能解决什么：保存建筑解锁状态。在哪里：`Assets/_Game/Scripts/Building/BuildingUnlockModel.cs`。怎么复用：用于保存建筑解锁状态。在规则层读取和更新解锁状态。不能负责什么：不计算升级成本。改后跑哪组测试：`BuildingUnlockTests`。代码名：`BuildingUnlockModel`。

### 施工进度（推荐复用）

能解决什么：跟踪施工是否完成。在哪里：`Assets/_Game/Scripts/Building/ConstructionProgress.cs`。怎么复用：用于跟踪施工进度。把施工进度交给它记录。不能负责什么：不控制建筑视图。改后跑哪组测试：`ConstructionProgressTests`。代码名：`ConstructionProgress`。

### 施工退款规则（复用前审查）

能解决什么：计算取消施工时的退款。在哪里：`Assets/_Game/Scripts/Building/ConstructionRefundRules.cs`。怎么复用：用于计算施工退款。先审查资源写入边界后再调用。不能负责什么：不写入资源库存。改后跑哪组测试：`ConstructionProgressTests`。代码名：`ConstructionRefundRules`。

### 正式撤离纯规则（推荐复用）

能解决什么：根据单体、分类、全部或混合选择生成确定性的正式撤离工作，并冻结已确认批次的环境。在哪里：`Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs`。怎么复用：以纯规则创建单体、分类、全部或混合撤离 work，并在确认批次时冻结和平/战斗上下文、生产力、退款比例与基础耗时。不能负责什么：不读取场景、UI、城市库存或当前敌人；调用方提供权威上下文并负责原子容量预检。遗弃废墟不是前哨，本规则不进入 schema 30 或接入冻结 2D。改后跑哪组测试：`GrayboxEvacuationTests`。代码名：`EvacuationBatchContext`、`BuildingEvacuationWork`、`BuildingEvacuationRules`。

### 三维建筑会话（复用前审查）

能解决什么：协调当前三维建造过程、唯一城市仓储聚合模型和正式撤离提交边界。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`。怎么复用：协调三维建筑会话，并持有当前会话唯一的 CityResourceStorageModel；正式撤离时保存稳定 work 锁、组合退款与内部载荷预检，并只在原子提交成功后移除建筑。不能负责什么：不替代领域建造、物流距离、撤离纯规则或仓库过滤规则；仓库内容由 CityResourceStorageModel 和 WarehouseStorageState 拥有，不进入 schema 30。改后跑哪组测试：`GrayboxBuildingSessionTests`、`GrayboxEvacuationTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxBuildingSession3D`。

### 三维正式撤离协调与只读视图（仅限场景）

能解决什么：在正式三维场景中协调冻结撤离批次、内部物资迁移、稳定队列和只读清单。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`。怎么复用：在正式 3D 场景编排冻结撤离批次、稳定队列、内部载荷捕获、城市原子提交、运行时完成或遗弃，并发布不可变清单和队列 view。不能负责什么：只消费生产与防御运行时拥有的内部载荷，不拥有或重算载荷、退款、容量、战斗或物流真值；失败保留原 work 与锁供重试，不进入 schema 30、不接入冻结 2D，遗弃废墟不是前哨。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxBuildingUiAndInputTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`EvacuationManifestItemViewModel`、`EvacuationManifestViewModel`、`EvacuationQueueViewModel`、`GrayboxEvacuationController3D`。

### 三维建筑共享运行与物流资格（推荐复用）

能解决什么：让生产与防御从同一建筑生命周期事实分别判断状态保留、本地运行和物流连接。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingOperationalAccess3D.cs`。怎么复用：用于从同一建筑生命周期事实分别判断会话状态保留、本地运行和物流连接。不能负责什么：只组合已完成、玩家所有、撤离锁定、建筑站点、城市模式与既有范围规则；不持有库存、生产或防御状态，不复制放置合法性。改后跑哪组测试：`GrayboxProductionRuntimeTests`、`GrayboxFirstDefenseRuntimeTests`。代码名：`GrayboxBuildingOperationalAccess3D`。

### 三维建筑世界视图（仅限场景）

能解决什么：在当前场景显示建筑、半透明放置预览和稳定前向标记。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs` 与 `Assets/_Game/Rendering/Graybox3D/GrayboxPreview.mat`。怎么复用：在场景内显示建筑与半透明放置预览，并用稳定前向标记同步四向旋转、旋转后占地和模型朝向。不能负责什么：只负责 GrayboxPrototype3D 的建筑表现，不作为纯领域模型复用；不得自行决定锚点、旋转合法性、成本或资源节点兼容性。改后跑哪组测试：`GrayboxBuildingProjectionAndViewTests`。代码名：`GrayboxBuildingWorldView3D`。

## UI 与输入

### 三维建筑输入路由（复用前审查）

能解决什么：把建造和撤离的正式输入送到正确界面，并在撤离期间保护模态优先级。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`。怎么复用：通过正式 Input System 路由建造与撤离输入，并在清单、处理和容量阻塞状态执行 F、Escape、E 与世界输入的模态优先级。不能负责什么：真实输入边界只发布界面命令；不决定建筑放置、退款、容量或战斗规则，不直接调用领域提交，也不接入冻结 2D。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`、`GrayboxBuildingRuntimeSceneTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`GrayboxBuildingInputRouter3D`。

### 三维建筑菜单视图（复用前审查）

能解决什么：显示建筑目录、放置反馈，以及正式撤离清单、队列、内部物资后果和容量阻塞操作。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`。怎么复用：显示建筑目录、旋转预览、资源缺口，以及正式撤离清单、稳定处理队列、内部物资后果和容量阻塞操作。不能负责什么：不保存建筑或库存数据，也不计算方向、退款、容量或战斗状态；全部读取控制器提供的不可变 view，图标统一复用 ResourceIconCatalog3D。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`、`GrayboxBuildingProjectionAndViewTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`GrayboxBuildingMenuView3D`。

### 三维生产可观察化控制器（仅限场景）

能解决什么：把当前 3D 会话的背包、应急合成、六节点研究、资源状态栏、真实仓库详情和面板命令接到正式模型。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs`。怎么复用：在 GrayboxPrototype3D 内组合背包、应急合成、六节点研究、资源状态栏、真实仓库详情与面板命令，并把真实输入提交到正式模型。不能负责什么：只属于当前 3D 场景的会话与 UI 适配；城市与仓库数量必须读取 CityResourceStorageModel，不替代资源、生产、研究、访问资格或输入路由真值，不进入 schema 30，也不得接入冻结 2D。改后跑哪组测试：`ManualResourceAccessRulesTests`、`GrayboxWarehouseStorageIntegrationTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`GrayboxOperationsController3D`。

### 三维生产可观察化视图（仅限场景）

能解决什么：呈现当前 3D 场景的资源栏、账本、背包、合成、科技树、真实仓库内容和共享材料图标，并把点击转换为命令事件。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsView3D.cs`。怎么复用：显示当前 3D 场景的资源栏、完整账本、背包与合成面板、六节点科技树、真实仓库内容与共享资源图标，并把 UGUI 操作发布为命令事件。不能负责什么：只负责 GrayboxPrototype3D 的 UGUI 结构、文案投影和点击事件；图标必须复用 ResourceIconCatalog3D，视图不持有库存、队列、研究或解锁真值，不自行扣资源、推进时间或判断访问资格。改后跑哪组测试：`GrayboxVisualAndWorldTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`GrayboxOperationsView3D`。

### 三维首版防御场景接线与表现（仅限场景）

能解决什么：把首版防御规则时钟、全局暂停、HUD、真实选择输入、敌塔表现池和 tracer 接入默认三维场景。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHud3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs`。怎么复用：用于在 GrayboxPrototype3D 中连接规则时钟、全局暂停、HUD、真实选择输入、敌塔表现池与 tracer。不能负责什么：仅限当前正式 3D 场景适配；UI 只读快照，表现对象不持有目标、伤害、耗弹或波次真值，不接入冻结 2D。改后跑哪组测试：`GrayboxDefenseControllerTests`、`GrayboxDefenseObservabilityTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseRuntimeInputTests`。代码名：`GrayboxDefenseController3D`、`GrayboxDefenseHud3D`、`GrayboxDefenseHudView3D`、`GrayboxDefenseWorldView3D`。

### 三维灰盒显示设置边界（复用前审查）

能解决什么：把显示设置规则、偏好存储和 Unity 平台应用拆成可测试边界。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsModel3D.cs` 和 `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsAdapters3D.cs`。怎么复用：在三维灰盒中以 IGrayboxDisplaySettingsStore 和 IGrayboxDisplaySettingsPlatform 分离可测试的显示设置模型、偏好存储和 Unity 平台应用边界。不能负责什么：PlayerPrefs 只保存独立显示偏好，明确位于正式存档 schema 30 之外；跨项目复用前需复核键名、版本和平台能力。改后跑哪组测试：`GrayboxUsabilityTests`。代码名：`GrayboxDisplaySettingsModel3D`、`PlayerPrefsGrayboxDisplaySettingsStore3D`、`UnityGrayboxDisplaySettingsPlatform3D`。

### 三维灰盒系统菜单控制器（仅限场景）

能解决什么：协调当前灰盒场景的菜单页面、系统暂停、设置和安全退出。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuController3D.cs`。怎么复用：协调灰盒场景内菜单页、系统暂停、设置和安全退出。不能负责什么：依赖当前灰盒 GameSpeedModel 与场景接线，不是通用前端菜单框架。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxSystemMenuController3D`。

### 三维灰盒系统菜单场景视图（仅限场景）

能解决什么：显示当前 GrayboxPrototype3D 的模态系统菜单层级和文案。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs`。怎么复用：显示当前三维灰盒场景的模态系统菜单。不能负责什么：层级、文案和 UGUI 引用属于 GrayboxPrototype3D；不得未经复核提升为通用可复用视图。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxSystemMenuView3D`。

### 三维灰盒易用性输入协调器（仅限场景）

能解决什么：按既有取消链、开发面板和系统菜单优先级协调当前灰盒场景输入。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`。怎么复用：在灰盒场景内按既有取消链、开发面板和系统菜单优先级分发输入。不能负责什么：只协调当前场景输入消费者，不复制建筑规则，也不作为全项目通用输入总线。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxUsabilityInputCoordinator3D`。

## 资源、研究、人口、战斗和存档

### 正式资源定义目录（推荐复用）

能解决什么：集中提供 15 种正式资源的稳定 ID、中文名称、显示顺序、单格栈上限、图标回退键和正式 3D 初始城市数量。在哪里：`Assets/_Game/Scripts/Economy/ResourceDefinitionCatalog.cs`。怎么复用：提供全部正式资源的稳定标识、中文名称、堆叠上限与基础资源栏顺序。资源 UI、背包和正式 3D 会话接入时统一从目录读取定义；正式城市底层账本通过 `CreateFormalCityInventory` 创建。不能负责什么：只定义资源身份与静态配置；不保存库存数量，不执行转移、生产或界面输入。工厂返回的是允许保留超额数量的 backing ledger，不代表城市拥有无限有效容量；正式 3D 入库与读取必须经过 `CityResourceStorageModel`。改后跑哪组测试：`ResourceDefinitionCatalogTests`。代码名：`ResourceDefinition`、`ResourceDefinitionCatalog`。

### 资源库存（推荐复用）

能解决什么：作为兼容的底层资源数量账本，按稳定资源 ID 保存整数数量，并保留冻结 2D 所需的物理容量和债务行为。在哪里：`Assets/_Game/Scripts/Economy/ResourceInventory.cs`。怎么复用：用于管理资源数量。正式 3D 只把它作为 `CityResourceStorageModel` 的城市核心 backing ledger。不能负责什么：不驱动生产周期。它也不提供真实仓库共享容量；正式 3D 新功能不得直接使用债务行为实现生产扣款。改后跑哪组测试：`FoundationTests`、`ResourceInventoryChangeTests`。代码名：`ResourceChangeAttribution`、`ResourceInventory`。

### 旧每资源城市容量策略（复用前审查）

能解决什么：保留 `IDEA-0011` 的基础容量加有效仓库数、且每种资源分别增加 150 的旧兼容算法。在哪里：`Assets/_Game/Scripts/Economy/ResourceCapacityPolicy.cs`。怎么复用：保留 IDEA-0011 的基础容量加每座仓库、且每种资源分别扩容的旧兼容算法，供旧接口和冻结回归使用；IDEA-0012 的正式 3D 仓库不再使用此模型。不能负责什么：IDEA-0012 已用每仓库 150 共享总容量替代旧的每资源加仓库模型；正式 3D 必须通过 CityResourceStorageModel 读写城市与仓库库存，不得向 ResourceCapacityPolicy 传仓库数模拟真实仓库。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceCapacityPolicy`。

### 城市与真实仓库库存模型（推荐复用）

能解决什么：提供正式 3D 城市库存唯一聚合入口，并以绑定版本的计划保证撤离容量预检和提交原子一致。在哪里：`Assets/_Game/Scripts/Economy/CityResourceStorageModel.cs`。怎么复用：作为正式 3D 城市库存唯一聚合入口，按稳定仓库 ID 处理联网数量、确定性存取、不可变快照；撤离使用绑定 revision 的容量预检计划和单次原子提交，并明确 StalePlan 与 AlreadyCommitted。不能负责什么：不决定建筑处理、完成状态、玩家所有权、物流距离或交互资格；调用方必须提供权威连接和完整内部载荷。它不进入 schema 30，也不替代 WorldMapModel、ResourceInventory 或 WarehouseStorageState。改后跑哪组测试：`CityResourceStorageModelTests`、`GrayboxEvacuationTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`CityResourceEvacuationPlan`、`CityResourceChangeAttributionScope`、`CityResourceStorageModel`、`CityResourceStorageSnapshot`。

### 单仓库共享容量状态（推荐复用）

能解决什么：保存一座仓库的真实内容、150 共享总容量、联网状态和可选单资源过滤。在哪里：`Assets/_Game/Scripts/Economy/WarehouseStorageState.cs`。怎么复用：按稳定建筑实例 ID 保存一座仓库的 150 共享总容量、真实内容、联网状态与可选单资源过滤，并发布不可变快照。不能负责什么：只拥有单仓库会话状态，不聚合城市库存、不计算物流范围、不执行建筑生命周期；正式调用必须由 CityResourceStorageModel 统一编排，不能绕过聚合模型直接充当城市账本。改后跑哪组测试：`CityResourceStorageModelTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`WarehouseStorageState`、`WarehouseStorageSnapshot`。

### 资源缺口规则（推荐复用）

能解决什么：按正式成本顺序给出每种材料的拥有、需要和缺少数量，让“材料不足”变成精确可读反馈。在哪里：`Assets/_Game/Scripts/Economy/ResourceShortfallRules.cs`。怎么复用：按正式成本顺序计算每种材料的拥有、需要与缺少数量，供放置失败和其他资源不足反馈统一投影。不能负责什么：只计算纯缺口数据，不读取 Unity 场景、不执行扣款、不决定放置合法性，也不生成最终 UI 文案；调用方必须传入当前权威库存读取函数。改后跑哪组测试：`ResourceShortfallRulesTests`。代码名：`ResourceShortfall`、`ResourceShortfallRules`。

### 原子资源事务（推荐复用）

能解决什么：在城市账本、建筑账本和玩家背包之间提供多输入扣除、输出预检及守恒转移基础，并统一返回完成、部分完成或失败状态。在哪里：`Assets/_Game/Scripts/Economy/ResourceTransaction.cs`。怎么复用：聚合同资源请求，预检输入与输出，并执行批量提交和允许部分接收的原子转移。生产、研究和人工转移必须使用事务入口，不得在 UI 或控制器中自行拼接 `TrySpend`、`Add`、`Remove`；当前已覆盖账本批事务和背包单资源双向转移。背包合成的多输入预留、产出与取消返还已由 `CraftingQueueModel` 通过槽位快照和完整回滚实现，并由 `CraftingQueueModelTests` 保护。正式事务只允许使用已有非负余额，不借用旧债务额度。不能负责什么：只处理资源数量与容量提交；不决定物流连接、交互距离、建筑资格、配方周期或界面状态。也不统计仓库数量。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceAmount`、`ResourceTransferResult`、`ResourceTransaction`。

### 玩家背包模型（推荐复用）

能解决什么：维护会话级 30 格个人背包，包括同类稳定合并、每格正式栈上限、稳定扣除、拆半、逐个移动、整栈合并与交换。在哪里：`Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs`。怎么复用：管理三十格会话背包及稳定堆叠、拆分、逐个移动、整栈合并与交换。背包 UI 只读取槽位快照，并通过模型或 `ResourceTransaction` 提交操作；资源栈上限继续来自资源定义目录。不能负责什么：只拥有背包槽位状态；不访问城市或建筑库存，不判定交互资格，不处理 Unity 输入和界面表现。背包不进入当前 schema 30 存档。改后跑哪组测试：`PlayerBackpackModelTests`。代码名：`BackpackSlot`、`PlayerBackpackModel`。

### 正式资源配方目录（推荐复用）

能解决什么：用一份正式目录统一三条机器配方与两条应急手工配方，避免 UI、生产和合成各自复制数值。在哪里：`Assets/_Game/Scripts/Economy/ResourceRecipeCatalog.cs`。怎么复用：统一提供三条机器配方和两条应急手工配方的稳定 ID、输入输出、周期、绑定节点动态产出与研究解锁条件。机器条目继续从正式机器生产定义投影；科技树和合成 UI 只引用稳定配方 ID。不能负责什么：只定义正式配方静态配置并复用既有机器生产定义；不拥有队列、背包、建筑缓存或进度，也不执行资源事务、自动串联或 UI 手势。改后跑哪组测试：`CraftingQueueModelTests`。代码名：`ResourceRecipeDefinition`、`ResourceRecipeCatalog`。

### 应急合成队列（推荐复用）

能解决什么：维护最多 20 次执行的 FIFO 应急合成队列，保证入队预留、顺序推进、产出阻塞和取消返还不丢失资源。在哪里：`Assets/_Game/Scripts/Economy/CraftingQueueModel.cs`。怎么复用：管理最多 20 次执行的 FIFO 应急合成队列，在入队时原子预留背包输入，并处理暂停、产出阻塞和取消返还。界面应把左 1、右 5 和 Shift 最大请求转换为模型命令，不自行扣除材料或推进时间。不能负责什么：只拥有当前会话的合成队列、预留材料、活动进度和阻塞原因；不访问城市或建筑库存，不自动合成前置材料，不解释鼠标手势，也不进入 schema 30 存档。改后跑哪组测试：`CraftingQueueModelTests`。代码名：`CraftingQueueModel`。

### 手工资源访问规则（推荐复用）

能解决什么：统一判断当前直接控制对象是否可以手工访问城市库存或某座建筑库存。在哪里：`Assets/_Game/Scripts/Economy/ManualResourceAccessRules.cs`。怎么复用：按当前直接控制目标、领袖招募、两格欧氏距离、footprint 和建筑生命周期事实评估城市或建筑库存的手工访问资格。调用方应在每次资源操作提交前重新传入当前权威事实。不能负责什么：只返回纯访问判定；不查找场景对象、不解析旋转尺寸、不执行资源转移，也不缓存资格。调用方必须在每次提交时传入当前事实和权威 footprint。改后跑哪组测试：`ManualResourceAccessRulesTests`。代码名：`ManualResourceAccessRules`。

### 正式机器生产定义目录（推荐复用）

能解决什么：集中提供采矿、冶炼和装配三条正式机器配方的稳定 ID、依次为 `3`、`6`、`6` 秒的周期、输入输出和内部库存容量。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionDefinitionCatalog.cs`。怎么复用：提供采矿、冶炼和装配三条正式机器配方的稳定标识、周期、输入输出与内部容量。生产状态、研究解锁和后续 UI 必须引用目录条目，不得复制配方数值。不能负责什么：只定义机器生产静态配置；不保存建筑实例状态，不推进周期，也不判断物流连接。改后跑哪组测试：`FormalProductionSimulationTests`。代码名：`FormalProductionDefinition`、`FormalProductionDefinitionCatalog`。

### 逐建筑生产状态（推荐复用）

能解决什么：为每个稳定建筑实例保存独立输入/输出缓存、已取得的输入批次、周期进度、玩家暂停和单一停工原因。在哪里：`Assets/_Game/Scripts/Economy/BuildingProductionState.cs`。怎么复用：按稳定建筑实例保存输入输出缓存、已预留周期、进度、暂停和停工原因。默认 3D 场景已由 `GrayboxProductionRuntime3D` 按 `GrayboxBuildingInstance3D.StableInstanceId` 持有并清理这些会话状态；其他场景适配器应复用同一所有权方式。不能负责什么：只拥有单座建筑的会话级生产状态；不保存到 schema 30，不自行读取场景或城市范围。改后跑哪组测试：`FormalProductionSimulationTests`。代码名：`ProductionStopReason`、`BuildingProductionState`。

### 正式生产与物流模拟（推荐复用）

能解决什么：在一个由调用方确定的物流步内，先按稳定实例 ID 通过 `CityResourceStorageModel` 卸载旧输出、补足输入，再推进各建筑独立周期，并在采矿完成时调用 `WorldMapModel.Harvest`。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionSimulation.cs`。怎么复用：按稳定实例顺序通过 CityResourceStorageModel 执行单个确定性物流步、推进独立生产周期并通过世界地图真值完成采矿。不能负责什么：不计算放置合法性、物流距离、建筑生命周期或场景时间；调用方必须提供已确认资格、连接状态和正式城市仓库聚合模型。保留 ResourceCapacityPolicy 重载只用于旧接口兼容。改后跑哪组测试：`FormalProductionSimulationTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`FormalProductionSimulation`。

### 三维生产建筑资格（复用前审查）

能解决什么：从既有建筑生命周期统一派生仓库是否应计入容量。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionEligibility3D.cs`。怎么复用：从现有三维建筑实例生命周期派生有效仓库资格。不能负责什么：只组合已完成、玩家拥有、未撤离锁定和稳定建筑 ID；不计算容量、物流距离、配方或放置合法性。改后跑哪组测试：`GrayboxProductionLifecycleTests`、`GrayboxProductionRuntimeTests`。代码名：`GrayboxProductionEligibility3D`。

### 三维生产运行时（复用前审查）

能解决什么：让正式生产状态和物流连接跟随三维建筑生命周期，并在撤离时完整保存或明确丢弃内部生产物资。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs`。怎么复用：按稳定实例 ID 同步生产状态、可运行集合和物流连接；撤离时由本运行时拥有并捕获 input、reserved input、output 内部载荷，匹配后完成迁移或在遗弃时明确丢弃。不能负责什么：只桥接 GrayboxBuildingInstance3D 与正式生产状态；不推进时间、不执行城市事务，撤离协调器不能修改载荷快照。不进入 schema 30，也不复制放置、物流范围或节点兼容规则。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxProductionRuntimeTests`、`GrayboxProductionLifecycleTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxProductionEvacuationPayload3D`、`GrayboxProductionRuntime3D`。

### 三维生产固定时钟（复用前审查）

能解决什么：让不同帧率下的三维生产保持同一固定步结果，并在暂停期间不积累追赶时间。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionClock3D.cs`。怎么复用：用 0.1 秒固定步长驱动运行时与正式生产模拟，保证分帧确定性和暂停无追赶。不能负责什么：只拥有会话级余量并组合运行时、模拟和 CityResourceStorageModel；不读取 Unity Time，不决定建筑资格，不处理 UI，也不进入 schema 30。旧 ResourceInventory 重载仅用于兼容回归。改后跑哪组测试：`GrayboxProductionClockTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxProductionClock3D`。

### 三维生产可观察化只读边界（推荐复用）

能解决什么：向资源状态栏和生产面板提供按稳定实例 ID 排序、内容变化后才换版的不可变生产详情，并把暂停、输入补给和输出提取收口到生产命令门面。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/ProductionObservabilitySnapshot.cs` 和 `Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionCommandFacade3D.cs`。怎么复用：发布按稳定实例 ID 排序的不可变生产详情；仓库与城市库存变化通过 CityResourceStorageModel.Revision 进入内容哈希。命令门面按 stable ID 在建筑缓存、背包和权威城市仓库聚合模型之间提交转移。不能负责什么：快照只读，不暴露 BuildingProductionState 或可变库存；命令不接受 UI 传入仓库数量。访问距离、物流和建筑生命周期资格仍由当前 3D 场景适配器在每次提交前基于权威事实重新验证；本边界不复制资格规则、不接入冻结 2D，也不进入 schema 30。改后跑哪组测试：`GrayboxProductionObservabilityFacadeTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`ProductionBuildingObservability`、`ProductionObservabilitySnapshot`、`GrayboxProductionCommandFacade3D`。

### 三维生产场景控制器（仅限场景）

能解决什么：把默认三维场景的真实建筑、城市、世界与暂停状态送进固定生产时钟。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionController3D.cs`。怎么复用：把当前三维场景的建筑会话、城市模式、世界坐标、CityResourceStorageModel 和 Unity 暂停状态接到固定步生产时钟。不能负责什么：只负责 GrayboxPrototype3D 场景引用与时间输入；不复制生产配方、物流范围、资源节点兼容性、仓库库存事务或界面规则。改后跑哪组测试：`GrayboxProductionControllerTests`、`GrayboxSceneContractTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxProductionController3D`。

### 研究模型（推荐复用）

能解决什么：管理研究状态。在哪里：`Assets/_Game/Scripts/Research/ResearchModel.cs`。怎么复用：用于管理研究状态。把研究状态放在这里。不能负责什么：不展示研究界面。改后跑哪组测试：`ResearchTests`。代码名：`ResearchModel`。

### 三维首版科技目录（推荐复用）

能解决什么：集中提供 GDD A16.4 六节点科技树的稳定顺序、稳定 ID、前置、成本、时长、效果与正式发布状态。在哪里：`Assets/_Game/Scripts/Research/DemoResearchCatalog.cs`。怎么复用：提供 A16.4 六节点的稳定顺序、稳定 ID、前置、成本、时长、效果与发布状态，3D 科技树和解锁投影必须读取该目录。不能负责什么：只定义 3D Demo release profile 的静态配置；不保存完成状态、不扣资源、不推进时间，也不回写冻结 2D 的 43 节点目录。改后跑哪组测试：`DemoResearchRuntimeTests`。代码名：`DemoResearchCatalog`。

### 三维首版科技运行时（推荐复用）

能解决什么：在当前 3D 会话中统一提交科技启动、不同城市形态下的推进、研究站失效暂停和取消退款。在哪里：`Assets/_Game/Scripts/Research/DemoResearchRuntime.cs`。怎么复用：组合统一研究模型与六节点 release profile，提交研究启动、模式倍率推进、研究站暂停和 80% 原子取消退款。不能负责什么：只拥有当前 3D 会话的研究规则适配；调用方仍须提供合格研究站、城市模式、全局暂停、城市库存与容量事实。它不处理 Unity 输入、UI、关注度、战斗效果或 schema 30 存档。改后跑哪组测试：`DemoResearchRuntimeTests`。代码名：`DemoResearchRuntime`。

### 首版防御战斗模型（推荐复用）

能解决什么：以确定性规则时间处理机枪塔索敌射击、啃噬者生命和城市核心受击。在哪里：`Assets/_Game/Scripts/Defense/FirstDefenseCombatModels.cs`。怎么复用：用于以确定性规则时间处理机枪塔索敌射击、啃噬者生命与城市核心受击。不能负责什么：只拥有首版战斗实体规则状态；不读取 Unity 场景、不推进教学波、不访问城市库存，tracer 不持有命中、伤害或耗弹真值。改后跑哪组测试：`FirstDefenseLoopTests`。代码名：`MachineGunTurretCombatModel`、`DefenseEnemyCombatModel`、`CityCoreCombatModel`。

### 首版教学防御波运行时（推荐复用）

能解决什么：推进十五秒预警、八只啃噬者四十秒分批生成、直达核心和核心受击。在哪里：`Assets/_Game/Scripts/Defense/FirstDefenseWaveRuntime.cs`。怎么复用：用于推进十五秒预警、八只啃噬者四十秒分批生成、直达核心和核心受击。不能负责什么：只拥有首个教学波与城市核心会话状态；不处理建筑发现、城市库存、Unity 时间、表现对象或正式失败结算。改后跑哪组测试：`FirstDefenseWaveRuntimeTests`。代码名：`DefenseEnemyRuntimeSnapshot`、`DefenseRuntimeSnapshot`、`TutorialDefenseRuntimeModel`。

### 三维首版防御运行时（复用前审查）

能解决什么：按稳定建筑实例同步机枪塔、塔内弹药和教学波，并在撤离时完整保存或明确丢弃塔内弹药。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`。怎么复用：按稳定建筑实例同步机枪塔、补给本地弹药并组合教学波；撤离时由本运行时拥有并捕获塔内弹药载荷，匹配后完成迁移或在遗弃时明确丢弃。不能负责什么：只桥接正式 3D 建筑会话、城市仓储和首版防御领域；撤离协调器不拥有塔内载荷，不复制物流范围、建筑资格或库存事务，不进入 schema 30。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxFirstDefenseRuntimeTests`、`GrayboxDefenseSnapshotStabilityTests`。代码名：`GrayboxDefenseEvacuationPayload3D`、`GrayboxDefenseTowerRuntimeState3D`、`GrayboxDefenseTowerSnapshot3D`、`GrayboxDefenseEnemySnapshot3D`、`GrayboxDefenseRuntimeSnapshot3D`、`GrayboxDefenseRuntime3D`。

### 人口模型（推荐复用）

能解决什么：管理人口容量。在哪里：`Assets/_Game/Scripts/Population/PopulationModel.cs`。怎么复用：用于管理人口容量。由模型维护容量数据。不能负责什么：不控制人口表现。改后跑哪组测试：`PopulationAndCapacityTests`。代码名：`PopulationModel`。

### 正式存档数据（复用前审查）

能解决什么：保存正式存档字段。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveData.cs`。怎么复用：用于保存正式存档字段。变更需要兼容性评审。不能负责什么：不替代存档迁移方案。改后跑哪组测试：`FormalSaveTests`。代码名：`FormalSaveData`。

## 3D 表现与美术

### 三维共享资源图标目录（推荐复用）

能解决什么：为全部正式资源提供稳定 Sprite 解析、可替换资产覆盖和确定性占位图标。在哪里：`Assets/_Game/Scripts/Graybox3D/ResourceIconCatalog3D.cs` 与共享资产 `Assets/_Game/Rendering/Graybox3D/ResourceIconCatalog3D.asset`。怎么复用：为全部正式资源提供稳定 Sprite 解析、可替换资产覆盖和确定性占位图标，供矿点、资源栏、仓库、背包、配方、科技与生产 UI 共享。不能负责什么：只负责资源 ID 到图标的表现映射，不拥有资源定义、数量或矿点真值；消费者必须使用同一目录资产或确定性 fallback，不得各自生成第二套资源身份和颜色语义。改后跑哪组测试：`GrayboxVisualAndWorldTests`、`GrayboxSceneContractTests`。代码名：`ResourceIconCatalog3D`。

### 三维资源矿点标识与图标标记（仅限场景）

能解决什么：把 `WorldMapModel` 的真实资源节点投影为带稳定 ID 和共享资源图标的可回收场景标记。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeIdentity3D.cs` 与 `Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeMarker3D.cs`。怎么复用：在 GrayboxPrototype3D 中以世界坐标生成稳定矿点 ID，并把 WorldMapModel 的真实资源节点投影为复用共享资源图标的可回收标记。不能负责什么：只属于当前 3D 世界表现与对象复用层；不创建资源节点、不决定节点类型、储量、采矿合法性或枯竭规则，所有真值必须继续来自 WorldMapModel。改后跑哪组测试：`GrayboxVisualAndWorldTests`。代码名：`GrayboxResourceNodeIdentity3D`、`GrayboxResourceNodeMarker3D`。

### 二维视觉槽位（复用前审查）

能解决什么：挂接二维视觉定义。在哪里：`Assets/_Game/Scripts/Presentation/VisualSlot.cs`。怎么复用：用于挂接二维视觉定义。先确认项目目标仍是二维显示。不能负责什么：不适配三维地形。改后跑哪组测试：`VisualSlotTests`。代码名：`VisualSlot`。

### 三维灰盒视觉槽位（仅限场景）

能解决什么：把灰盒视觉绑定到当前场景。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs`。怎么复用：用于场景内灰盒视觉绑定。只在该三维场景内使用。不能负责什么：不作为二维槽位替代。改后跑哪组测试：`GrayboxVisualAndWorldTests`。代码名：`GrayboxVisualSlot`。

### 首版三维地形配置（复用前审查）

能解决什么：定义首版地形参数。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`。怎么复用：用于定义首版地形参数。修改前需要导入策略复核。不能负责什么：不直接决定资源导入。改后跑哪组测试：`FirstArtTerrainProfileTests`。代码名：`FirstArtTerrainProfile3D`。

### 首版三维地形渲染（仅限场景）

能解决什么：在场景渲染首版地形。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`。怎么复用：用于在场景渲染首版地形。仅在已批准的三维场景接入。不能负责什么：不管理地形资源导入。改后跑哪组测试：`FirstArtTerrainRendererTests`。代码名：`FirstArtTerrainRenderer3D`。

### 废墟与悬崖三维配置（复用前审查）

能解决什么：冻结废墟与悬崖的稳定标识、模块语义和批准资源引用。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffCatalog3D.cs`、`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffProfile3D.cs`。怎么复用：用于冻结废墟与悬崖稳定标识、模块语义和批准资源引用。不能负责什么：不复制地形规则真值；修改目录、材质槽或 Prefab 绑定前需要资产合同复核。改后跑哪组测试：`FirstArtRuinsCliffAssetBuilderTests`、`FirstArtRuinsCliffCatalogProfileTests`。代码名：`FirstArtRuinsCliffCatalog3D`、`FirstArtRuinsCliffProfile3D`。

### 废墟与悬崖三维布局合批（复用前审查）

能解决什么：从既有世界地图确定性投影并合批 Ruins/Cliff 几何。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffGeometry3D.cs`、`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffLayout3D.cs`。怎么复用：用于从既有世界地图确定性投影并合批废墟与悬崖几何。不能负责什么：只消费既有地图与配置，不创建第二套地形判断或逐格常驻对象。改后跑哪组测试：`FirstArtRuinsCliffGeometryTests`、`FirstArtRuinsCliffLayoutTests`。代码名：`FirstArtRuinsCliffGeometry3D`、`FirstArtRuinsCliffLayout3D`。

### 废墟与悬崖三维呈现（仅限场景）

能解决什么：在唯一正式三维地形 presenter 中呈现 Ruins/Cliff，并在类别失败时恢复对应灰盒。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`。怎么复用：用于在正式三维地形 presenter 中呈现废墟与悬崖并保留分类回退。不能负责什么：必须复用唯一地形 presenter；不得增加第二个场景 owner 或绕过分类回退。改后跑哪组测试：`FirstArtRuinsCliffEvidenceCaptureTests`、`FirstArtRuinsCliffPresentationTests`、`FirstArtRuinsCliffSceneContractTests`。代码名：`FirstArtTerrainRenderer3D`。

## 场景、构建与检查工具

### 废墟与悬崖视觉证据捕获（复用前审查）

能解决什么：为 Ruins/Cliff 运行时接入留下可核对的固定画面和清单。在哪里：`Assets/_Game/Editor/FirstArtRuinsCliffEvidenceCapture.cs`。怎么复用：用于在批准的 GrayboxPrototype3D 场景中以固定 1280×720 视角自动采集 Ruins/Cliff 正常、单件和分类回退证据，并生成带资产 GUID、相机矩阵与 SHA-256 的清单。不能负责什么：仅供编辑器验证；输出必须是项目外的空绝对目录，必须消费既有场景、profile 和唯一地形 presenter，不修改玩法真值，也不替代用户视觉验收；正式说明路径为 Docs/09-Reusable-Project-Catalog-ZH.md。改后跑哪组测试：`FirstArtRuinsCliffEvidenceCaptureTests`。代码名：`FirstArtRuinsCliffEvidenceCapture`。

### 灰盒场景编写（仅限场景）

能解决什么：在编辑器生成灰盒场景。在哪里：`Assets/_Game/Editor/GrayboxSceneAuthoring.cs`。怎么复用：用于编辑器生成灰盒场景。只用于编辑器和指定场景。不能负责什么：不在运行时调用。改后跑哪组测试：`GrayboxSceneContractTests`。代码名：`GrayboxSceneAuthoring`。

### 正式构建工具（复用前审查）

能解决什么：执行正式 Windows、冻结 2D 回归和 universal macOS 构建，并让包含三维灰盒场景的 Player 构建在 Shader stripping 前识别批准的 URP 管线。在哪里：`Assets/_Game/Editor/FormalBuildTools.cs`。怎么复用：用于执行正式 Windows、冻结 2D 回归和 universal x86_64+arm64 macOS 构建，并在包含 3D 灰盒场景的 Player 构建期间临时登记批准的 URP 管线；带 -quit 的正式命令行构建还会在编辑器最终退出时恢复受保护文件。通过 `FormalBuildTools` 选择正式构建入口；`GrayboxRenderPipelineBuildScope` 只按实际场景列表临时登记 `GrayboxURP`。不能负责什么：不修改游戏规则；构建作用域与命令行最终退出恢复必须还原进入构建前的渲染管线、Quality 序列化状态和四个受保护文件的精确字节：Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset、ProjectSettings/GraphicsSettings.asset、ProjectSettings/QualitySettings.asset、ProjectSettings/ProjectSettings.asset。冻结 2D 构建不得获得 3D 管线覆盖，普通 GUI 构建不得遗留最终退出标记或备份；改后必须运行 GrayboxBuildAndPerformanceTests 中的 universal macOS 与 final-exit 合同，并执行受影响平台的真实构建后哈希检查。改后跑哪组测试：`GrayboxBuildAndPerformanceTests` 中的 `Bug0005_BuildTools_ExposeRestorableUniversalMacOSGrayboxTarget`、`Bug0005_FinalExitRestoreActivatesOnlyForQuitFormalBuilds`、`Bug0005_FinalExitRestoreSynchronizesRuntimeBeforeExactBytes`，并执行受影响平台的真实 Player 构建与退出后哈希检查。代码名：`FormalBuildTools`、`GrayboxRenderPipelineBuildScope`。

### 灰盒性能探针（仅限场景）

能解决什么：采集灰盒性能数据，并对生产、防御和撤离同时运行的正式混合负载留下可重复证据。在哪里：`Assets/_Game/Editor/GrayboxPerformanceProbe.cs`。怎么复用：用于采集灰盒性能数据，并执行 IDEA-0014 活跃生产、八敌、防御 HUD、撤离 UI 的 300 稳定帧混合探针、GUI 捕获和正式汇总。不能负责什么：只用于可重复验证和正式 Marker 取证，不改变玩法真值、不作为发布版本逻辑，也不替代用户试玩或真实 Windows GPU、显存和内存验收。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`、`GrayboxFormalEvacuationPerformanceTests`。代码名：`GrayboxPerformanceProbe`。

## 冻结或禁止用于新功能的旧内容

### 正式原型冻结场景（冻结回归）

能解决什么：保留二维旧功能的回归基线。在哪里：`Assets/_Game/Scenes/FormalPrototype.unity` 和 `Assets/_Game/Scripts/Core/FormalGameBootstrap.cs`。怎么复用：用于保留二维回归基线。只用于确认旧行为未倒退。不能负责什么：不得作为新功能起点。改后跑哪组测试：`SceneContractTests`。代码名：`FormalGameBootstrap`。

### 占位建筑控制器（禁止用于新功能）

能解决什么：维持旧回归兼容。在哪里：`Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`。怎么复用：用于旧回归兼容验证。禁止新功能复用。不能负责什么：不能作为新的建筑实现。改后跑哪组测试：`TurretAndBuildingTests`。代码名：`PlaceholderBuildingController`。
