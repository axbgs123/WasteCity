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

### 三维建筑会话（复用前审查）

能解决什么：协调当前三维建造过程。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`。怎么复用：用于协调三维建筑会话。在接入新的三维建造流程前先审查状态关系。不能负责什么：不替代领域建造规则。改后跑哪组测试：`GrayboxBuildingSessionTests`。代码名：`GrayboxBuildingSession3D`。

### 三维建筑世界视图（仅限场景）

能解决什么：在当前场景显示建筑。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`。怎么复用：用于场景内显示建筑。只在现有三维场景中绑定。不能负责什么：不作为纯领域模型复用。改后跑哪组测试：`GrayboxBuildingProjectionAndViewTests`。代码名：`GrayboxBuildingWorldView3D`。

## UI 与输入

### 三维建筑输入路由（复用前审查）

能解决什么：把建筑界面的输入送到正确位置。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`。怎么复用：用于路由建筑界面输入。先审查焦点和输入优先级。不能负责什么：不决定建筑放置规则。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`。代码名：`GrayboxBuildingInputRouter3D`。

### 三维建筑菜单视图（复用前审查）

能解决什么：显示建筑菜单。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`。怎么复用：用于显示建筑菜单。先检查当前界面与场景绑定。不能负责什么：不保存建筑数据。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`。代码名：`GrayboxBuildingMenuView3D`。

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

能解决什么：集中提供 15 种正式资源的稳定 ID、中文名称、显示顺序、单格栈上限、图标回退键和正式 3D 初始城市数量。在哪里：`Assets/_Game/Scripts/Economy/ResourceDefinitionCatalog.cs`。怎么复用：提供全部正式资源的稳定标识、中文名称、堆叠上限与基础资源栏顺序。资源 UI、背包和正式 3D 会话接入时统一从目录读取定义；正式城市账本接入时必须通过 `CreateFormalCityInventory` 创建。不能负责什么：只定义资源身份与静态配置；不保存库存数量，不执行转移、生产或界面输入。工厂返回的是允许保留超额数量的 backing ledger，不代表城市拥有无限有效容量；正式入库接线后仍必须经过 `ResourceCapacityPolicy`。当前默认 3D 场景尚未切换到该工厂。改后跑哪组测试：`ResourceDefinitionCatalogTests`。代码名：`ResourceDefinition`、`ResourceDefinitionCatalog`。

### 资源库存（推荐复用）

能解决什么：作为兼容的底层资源数量账本，按稳定资源 ID 保存整数数量，并保留冻结 2D 所需的物理容量和债务行为。在哪里：`Assets/_Game/Scripts/Economy/ResourceInventory.cs`。怎么复用：用于管理资源数量。旧 2D 继续按既有方式使用；正式 3D 城市库存接入时通过 `ResourceDefinitionCatalog.CreateFormalCityInventory` 创建，并由 `ResourceCapacityPolicy` 和 `ResourceTransaction` 约束正式写入。不能负责什么：不驱动生产周期。它也不提供资源显示定义、背包槽位、原子多资源事务、仓库有效容量或降容保留超额；`AddCapacity` 降容会裁切数量，`TrySpend` 在启用债务额度后可产生负数，因此正式 3D 新功能不得直接使用这两个行为实现容量变化或生产扣款。改后跑哪组测试：`FoundationTests`。代码名：`ResourceInventory`。

### 城市资源容量策略（推荐复用）

能解决什么：计算城市每种资源的正式有效容量，并在基础 150、每座有效仓库增加 150 的规则下预检或执行入库。在哪里：`Assets/_Game/Scripts/Economy/ResourceCapacityPolicy.cs`。怎么复用：按基础容量和有效仓库数计算每种城市资源的当前可接收量。正式 3D 城市库存的所有自动和人工入库统一通过该策略；容量降低时只改变有效上限，不修改账本已有数量。不能负责什么：不判定仓库所有权、完成状态或撤离资格，也不拥有资源账本。不改变物流距离，也不裁切超额库存；调用方必须提供已经按正式建筑资格派生的仓库数量。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceCapacityPolicy`。

### 原子资源事务（推荐复用）

能解决什么：在城市账本、建筑账本和玩家背包之间提供多输入扣除、输出预检及守恒转移基础，并统一返回完成、部分完成或失败状态。在哪里：`Assets/_Game/Scripts/Economy/ResourceTransaction.cs`。怎么复用：聚合同资源请求，预检输入与输出，并执行批量提交和允许部分接收的原子转移。生产、研究、合成和人工转移后续接入时必须调用事务入口，不得在 UI 或控制器中自行拼接 `TrySpend`、`Add`、`Remove`；当前已覆盖账本批事务和背包单资源双向转移，背包合成的多输入预留、产出与取消返还仍待后续 TDD。正式事务只允许使用已有非负余额，不借用旧债务额度。不能负责什么：只处理资源数量与容量提交；不决定物流连接、交互距离、建筑资格、配方周期或界面状态。也不统计仓库数量。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceAmount`、`ResourceTransferResult`、`ResourceTransaction`。

### 玩家背包模型（推荐复用）

能解决什么：维护会话级 30 格个人背包，包括同类稳定合并、每格正式栈上限、稳定扣除、拆半、逐个移动、整栈合并与交换。在哪里：`Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs`。怎么复用：管理三十格会话背包及稳定堆叠、拆分、逐个移动、整栈合并与交换。背包 UI 只读取槽位快照，并通过模型或 `ResourceTransaction` 提交操作；资源栈上限继续来自资源定义目录。不能负责什么：只拥有背包槽位状态；不访问城市或建筑库存，不判定交互资格，不处理 Unity 输入和界面表现。背包不进入当前 schema 30 存档。改后跑哪组测试：`PlayerBackpackModelTests`。代码名：`BackpackSlot`、`PlayerBackpackModel`。

### 正式机器生产定义目录（推荐复用）

能解决什么：集中提供采矿、冶炼和装配三条正式机器配方的稳定 ID、依次为 `3`、`6`、`6` 秒的周期、输入输出和内部库存容量。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionDefinitionCatalog.cs`。怎么复用：提供采矿、冶炼和装配三条正式机器配方的稳定标识、周期、输入输出与内部容量。生产状态、研究解锁和后续 UI 必须引用目录条目，不得复制配方数值。不能负责什么：只定义机器生产静态配置；不保存建筑实例状态，不推进周期，也不判断物流连接。改后跑哪组测试：`FormalProductionSimulationTests`。代码名：`FormalProductionDefinition`、`FormalProductionDefinitionCatalog`。

### 逐建筑生产状态（推荐复用）

能解决什么：为每个稳定建筑实例保存独立输入/输出缓存、已取得的输入批次、周期进度、玩家暂停和单一停工原因。在哪里：`Assets/_Game/Scripts/Economy/BuildingProductionState.cs`。怎么复用：按稳定建筑实例保存输入输出缓存、已预留周期、进度、暂停和停工原因。场景适配器后续应按 `GrayboxBuildingInstance3D.StableInstanceId` 持有并清理这些会话状态。不能负责什么：只拥有单座建筑的会话级生产状态；不保存到 schema 30，不自行读取场景或城市范围。当前默认 3D 场景尚未接入。改后跑哪组测试：`FormalProductionSimulationTests`。代码名：`ProductionStopReason`、`BuildingProductionState`。

### 正式生产与物流模拟（推荐复用）

能解决什么：在一个由调用方确定的物流步内，先按稳定实例 ID 卸载旧输出、补足输入，再推进各建筑独立周期，并在采矿完成时调用 `WorldMapModel.Harvest`。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionSimulation.cs`。怎么复用：按稳定实例顺序执行单个确定性物流步、推进独立生产周期并通过世界地图真值完成采矿。调用方必须提供当前世界模型、正式城市账本、容量策略、有效仓库数和已经由权威规则派生的物流连接。不能负责什么：不计算放置合法性、物流距离、建筑生命周期或场景时间；调用方必须提供已确认资格和连接状态。它不替代 `BuildingRangeRules`、`BuildingResourceNodeCompatibilityRules` 或 `WorldMapModel`；当前仍是纯领域层，尚未接入默认 3D 场景。旧 `ProductionModel`、`ResourceExtractionProcess` 和允许建筑链式中继的 `LogisticsNetworkModel` 不得作为本轮正式 3D 运行真值。改后跑哪组测试：`FormalProductionSimulationTests`。代码名：`FormalProductionSimulation`。

### 三维生产建筑资格（复用前审查）

能解决什么：从既有建筑生命周期统一派生仓库是否应计入容量。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionEligibility3D.cs`。怎么复用：从现有三维建筑实例生命周期派生有效仓库资格。不能负责什么：只组合已完成、玩家拥有、未撤离锁定和稳定建筑 ID；不计算容量、物流距离、配方或放置合法性。改后跑哪组测试：`GrayboxProductionLifecycleTests`、`GrayboxProductionRuntimeTests`。代码名：`GrayboxProductionEligibility3D`。

### 三维生产运行时（复用前审查）

能解决什么：让正式生产状态跟随当前三维建筑的完成、撤离、遗弃、移动资格与物流范围变化。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs`。怎么复用：按稳定实例 ID 同步生产状态、可运行集合、物流连接和有效仓库数。不能负责什么：只桥接 GrayboxBuildingInstance3D 与正式领域状态；不推进时间，不执行事务，不进入 schema 30，也不复制放置或节点兼容规则。改后跑哪组测试：`GrayboxProductionRuntimeTests`、`GrayboxProductionLifecycleTests`。代码名：`GrayboxProductionRuntime3D`。

### 三维生产固定时钟（复用前审查）

能解决什么：让不同帧率下的三维生产保持同一固定步结果，并在暂停期间不积累追赶时间。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionClock3D.cs`。怎么复用：用 0.1 秒固定步长驱动运行时与正式生产模拟，保证分帧确定性和暂停无追赶。不能负责什么：只拥有会话级余量并组合运行时、模拟和容量策略；不读取 Unity Time，不决定建筑资格，不处理 UI，也不进入 schema 30。改后跑哪组测试：`GrayboxProductionClockTests`。代码名：`GrayboxProductionClock3D`。

### 三维生产场景控制器（仅限场景）

能解决什么：把默认三维场景的真实建筑、城市、世界与暂停状态送进固定生产时钟。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionController3D.cs`。怎么复用：把当前三维场景的建筑会话、城市模式、世界坐标和 Unity 暂停状态接到固定步生产时钟。不能负责什么：只负责 GrayboxPrototype3D 场景引用与时间输入；不复制生产配方、物流范围、资源节点兼容性、库存事务或界面规则。改后跑哪组测试：`GrayboxProductionControllerTests`、`GrayboxSceneContractTests`。代码名：`GrayboxProductionController3D`。

### 研究模型（推荐复用）

能解决什么：管理研究状态。在哪里：`Assets/_Game/Scripts/Research/ResearchModel.cs`。怎么复用：用于管理研究状态。把研究状态放在这里。不能负责什么：不展示研究界面。改后跑哪组测试：`ResearchTests`。代码名：`ResearchModel`。

### 人口模型（推荐复用）

能解决什么：管理人口容量。在哪里：`Assets/_Game/Scripts/Population/PopulationModel.cs`。怎么复用：用于管理人口容量。由模型维护容量数据。不能负责什么：不控制人口表现。改后跑哪组测试：`PopulationAndCapacityTests`。代码名：`PopulationModel`。

### 正式存档数据（复用前审查）

能解决什么：保存正式存档字段。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveData.cs`。怎么复用：用于保存正式存档字段。变更需要兼容性评审。不能负责什么：不替代存档迁移方案。改后跑哪组测试：`FormalSaveTests`。代码名：`FormalSaveData`。

## 3D 表现与美术

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

能解决什么：采集灰盒性能数据。在哪里：`Assets/_Game/Editor/GrayboxPerformanceProbe.cs`。怎么复用：用于采集灰盒性能数据。只在已定义的性能场景采样。不能负责什么：不作为发布版本逻辑。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`。代码名：`GrayboxPerformanceProbe`。

## 冻结或禁止用于新功能的旧内容

### 正式原型冻结场景（冻结回归）

能解决什么：保留二维旧功能的回归基线。在哪里：`Assets/_Game/Scenes/FormalPrototype.unity` 和 `Assets/_Game/Scripts/Core/FormalGameBootstrap.cs`。怎么复用：用于保留二维回归基线。只用于确认旧行为未倒退。不能负责什么：不得作为新功能起点。改后跑哪组测试：`SceneContractTests`。代码名：`FormalGameBootstrap`。

### 占位建筑控制器（禁止用于新功能）

能解决什么：维持旧回归兼容。在哪里：`Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`。怎么复用：用于旧回归兼容验证。禁止新功能复用。不能负责什么：不能作为新的建筑实现。改后跑哪组测试：`TurretAndBuildingTests`。代码名：`PlaceholderBuildingController`。
