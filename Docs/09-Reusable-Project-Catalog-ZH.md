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

能解决什么：保存格位地形、Traversal、资源类型与剩余储量、揭示和孤立资源等世界运行时真值。在哪里：`Assets/_Game/Scripts/World/WorldMapModel.cs`。怎么复用：保存格位地形、Traversal、资源余量与揭示等世界运行时真值。不能负责什么：不选择 v2 生成策略，也不处理场景渲染、存档文件或建筑放置。改后跑哪组测试：`WorldMapTests`、`GrayboxWorldLayout3DTests`、`GrayboxFormalSaveWorldCityTests`。代码名：`WorldMapModel`。

### 三维世界布局（复用前审查）

能解决什么：作为正式 `64×48` 世界入口调用 IDEA-0019 的 v2 确定性生成器。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs`。怎么复用：作为正式 64×48 世界入口消费 IDEA-0019 的 v2 确定性生成器。不能负责什么：只选择正式世界生成边界；不复制地图目录、渲染、存档 IO 或放置合法性，调整布局前需要场景复核。改后跑哪组测试：`GrayboxWorldLayout3DTests`、`GrayboxFormalSaveWorldCityTests`。代码名：`GrayboxWorldLayout3D`。

### 正式三维世界生成目录（复用前审查）

能解决什么：定义并生成 IDEA-0019 的 v2 固定宏格地貌、Traversal 区域、出生保护区、关键走廊、精确 `24` 个固定坐标资源节点和正式 world identity。在哪里：`Assets/_Game/Scripts/Graybox3D/FormalWorldGenerationCatalog3D.cs`、`Assets/_Game/Scripts/Graybox3D/FormalWorldGenerator3D.cs`。怎么复用：以固定 8×6 宏格模板、每宏格整数扰动、固定 Traversal 区域/走廊和 24 项精确坐标资源目录生成 seed 8128 的正式 64×48 v2 世界。当前 canonical golden SHA-256 为 `2f0ecd374ad3a1bf6fd50564d949741618c7ce1b72bc6619f67acda632b1e6fd`。不能负责什么：只拥有确定性初始地图内容与生成配置；当前不实现评分候选、定点插值或随机 spot，也不持有运行时剩余储量、揭示、渲染、存档文件、城市路径或建筑放置真值。改后跑哪组测试：`GrayboxWorldLayout3DTests`、`WorldMapTests`、`GrayboxFormalSaveWorldCityTests`、`GrayboxFormalSaveRuntimeHostTests`。代码名：`FormalWorldGenerationCatalog3D`、`FormalWorldGenerator3D`、`FormalResourceNodeSpec3D`。

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

能解决什么：展示可见的建筑目录。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs`。怎么复用：投影正式建筑目录的可见性、统一解锁结果与锁定原因，供快捷栏和分类列表读取。先审查它与当前三维场景的关系再接入。不能负责什么：不拥有建筑定义、不提交建筑放置，也不自行决定人口、研究或前置条件。改后跑哪组测试：`GrayboxBuildingCatalogTests`。代码名：`GrayboxBuildingCatalogPresenter3D`。

### 建筑网格（推荐复用）

能解决什么：计算建筑可用格位。在哪里：`Assets/_Game/Scripts/Building/BuildingGrid.cs`。怎么复用：提供建筑格位计算，并承载稳定 BuildingDefinition、BuildingCatalog 与首轮人口门槛配置真值。把格位计算集中在这里。不能负责什么：不负责输入路由或失败反馈，也不在视图层复制建筑解锁规则。改后跑哪组测试：`BuildingGridTests`、`BuildingUnlockTests`、`GrayboxBuildingCatalogTests`。代码名：`BuildingGrid`。

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

能解决什么：协调当前三维建造、schema 31 批量恢复和正式撤离提交边界。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`。怎么复用：协调三维建筑会话，并持有当前会话唯一的 CityResourceStorageModel；schema 31 恢复时先在临时双网格验证稳定实例、方向、占格、资源点绑定与 nextStableInstanceOrdinal 高水位，再统一替换实例和表现。正式撤离仍只在原子仓储提交成功后移除建筑。。不能负责什么：不替代领域建造、物流距离、撤离纯规则或仓库过滤规则；不从场景搜索建筑、不重新支付建造成本，也不从当前实例推导并复用历史高水位。仓库内容由 CityResourceStorageModel 和 WarehouseStorageState 拥有；schema 31 只保存权威建筑状态，连接和表现仍为派生状态。。改后跑哪组测试：`GrayboxBuildingSessionTests`、`GrayboxBuildingCombatLifecycleTests`、`GrayboxEvacuationTests`、`GrayboxFormalSaveBuildingStorageTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxBuildingRestoreEntry3D`、`GrayboxBuildingSession3D`。

### 三维建筑与仓储存档适配器（复用前审查）

能解决什么：把正式三维建筑、双网格、城市核心账本与真实仓库映射到 schema 31。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingStorageSaveAdapter3D.cs`。怎么复用：从显式提供的当前建筑会话、建筑表现和当前 WorldMapModel 捕获并恢复 schema 31 的建筑实例、双网格、高水位、城市核心账本、真实仓库、过滤与孤立资源。不能负责什么：只映射领域真值与 FormalThreeD DTO；不拥有文件路径、codec、文件事务或恢复总协调，不使用场景发现，也不保存物流连接、容量配置、revision、UI 或表现对象。带资源节点绑定的恢复必须显式提供当前正式世界。改后跑哪组测试：`GrayboxFormalSaveBuildingStorageTests`。代码名：`GrayboxBuildingStorageSaveAdapter3D`。

### 三维背包合成与科技存档适配器（复用前审查）

能解决什么：把正式 3D 背包、应急合成队列与 43 节点正式科技状态映射到 schema 31。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEconomySaveAdapter3D.cs`。怎么复用：从显式提供的当前 PlayerBackpackModel、CraftingQueueModel 和 FormalResearchRuntime 捕获 schema 31 背包、合成与正式科技 DTO；兼容重载继续服务历史 Demo 测试。恢复时先通过各模型公开且受验证的 snapshot/prepare 边界完成三域预检，再按背包、研究、合成顺序提交，保留队列稳定执行 ID、高水位、预留输入及未知内容降级状态。不能负责什么：只负责领域真值与 FormalThreeD DTO 映射，不拥有文件路径、codec、文件事务、检查点或恢复总协调，不搜索场景，也不重新扣除合成预留或研究成本。背包超栈兼容策略由上层根据内容配置显式传入；研究站资格、城市倍率、UI、派生阻塞原因和表现对象不入档。改后跑哪组测试：`GrayboxFormalSaveEconomyTests`、`GrayboxFormalResearchSaveAdapterTests`、`PlayerBackpackModelTests`、`CraftingQueueModelTests`、`DemoResearchRuntimeTests`。代码名：`GrayboxEconomySaveAdapter3D`。

### 三维逐建筑生产存档适配器（复用前审查）

能解决什么：在正式三维生产运行时的持久化快照与 schema 31 逐建筑生产 DTO 之间执行确定性映射。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionSaveAdapter3D.cs`。怎么复用：在 GrayboxProductionRuntime3D 的持久化快照边界与 schema 31 FormalThreeDProduction DTO 之间执行确定性映射，并把恢复委托给运行时的预检与单次提交。不能负责什么：只负责生产领域快照与 DTO 映射；不拥有文件 IO、场景搜索、生产 tick、物流、建筑放置或矿点兼容规则，也不复制这些系统的判断。改后跑哪组测试：`GrayboxFormalSaveProductionTests`。代码名：`GrayboxProductionSaveAdapter3D`。

### 三维首版防御存档适配器（复用前审查）

能解决什么：在正式三维防御运行时的当前十波战役快照与 schema 32 防御 DTO 之间执行确定性映射，并保留 schema 31 教学波旧档迁移入口。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSaveAdapter3D.cs`。怎么复用：在正式三维防御运行时、十波战役持久状态与当前 FormalThreeD 防御 DTO 之间执行确定性映射，保留配置签名、出生锚点、波次阶段、完整会话统计、塔攻击余量、活动敌人、建筑生命和核心状态，并通过零写入计划把 schema 31 的可确认统计迁移为显式部分统计后统一提交。不能负责什么：只负责防御领域快照、迁移兼容与 FormalThreeD DTO 映射；不拥有文件 IO、场景搜索、规则 tick、城市库存、UI 或表现对象。FormalSaveValidator 只验证结构与高价值跨引用；目标缓存、状态文案、攻击事件和表现池不入档，迁移不得伪造完整历史统计。改后跑哪组测试：`GrayboxFormalSaveDefenseTests`、`GrayboxFormalDefenseCampaignSaveAdapterTests`、`FormalSaveSchema32ContractTests`、`SessionStatisticsTests`。代码名：`GrayboxDefenseSaveAdapter3D`。

### 正式防御战役迁移与恢复边界（复用前审查）

能解决什么：把纯十波战役与正式三维防御运行时状态转换为可预检、可一次提交的持久化状态，并安全接收 schema 31 教学波兼容数据。在哪里：`Assets/_Game/Scripts/Defense/SingleCityDefenseCampaignPersistenceState.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalDefenseCampaignPersistence3D.cs`。怎么复用：schema 32 冻结并完整往返十波战役、塔战斗余量、活动敌人、建筑生命、完整会话统计、出生锚点与规则时钟状态；schema 31 只迁移可确认历史值并显式标为部分统计，所有恢复先生成零写入恢复计划，预检后单次提交。不能负责什么：只拥有战役领域持久状态、schema 31 到当前战役结构的兼容迁移和恢复预检；不执行文件 IO，不搜索场景，不保存 HUD、目标缓存、轨迹池或派生连接。迁移缺失的历史信息必须采用受测试的保守默认值并标为部分统计，不得伪造成完整统计。改后跑哪组测试：`SingleCityDefenseCampaignPersistenceTests`、`SingleCityDefenseCampaignCheckpointTests`、`SessionStatisticsTests`、`FormalSaveSchema32ContractTests`、`GrayboxFormalDefenseCampaignSaveAdapterTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`。代码名：`SingleCityDefenseCampaignPersistenceState`、`SingleCityDefenseCampaignRestorePlan`、`GrayboxFormalDefenseCampaignPersistenceState3D`、`GrayboxFormalDefenseCampaignRestorePlan3D`。

### 三维冻结撤离存档适配器（复用前审查）

能解决什么：把正式三维撤离控制器的冻结批次映射到 schema 31，并安全恢复 work、建筑锁、运行时载荷、稳定队列、批次高水位、剩余时间和容量阻塞身份。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationSaveAdapter3D.cs`。怎么复用：把正式三维撤离控制器的冻结批次确定性映射到 schema 31，保留 work、建筑锁、生产与防御载荷、稳定队列、批次高水位、剩余时间和容量阻塞身份，并通过绑定 controller generation 与 session revision 的计划执行两阶段恢复。不能负责什么：只负责撤离持久状态、FormalThreeD DTO 映射和恢复边界；不重算 work、退款、容量、运行时载荷、生产、防御、物流或建筑资格，不拥有文件 IO、恢复总协调、场景搜索、UI 或表现，也不接入已经退役的 legacy 2D runtime。未知定义占位可保留聚合资源载荷，已知普通建筑拒绝无所有者载荷。当前为已实现待验证；Task 13 完整自动化与构建已通过，Task 14 退役后的完整自动化、质量门、三项 3D 构建和正式验证记录也已通过，人工试玩和真实 Windows 验收仍未完成。改后跑哪组测试：`GrayboxFormalSaveEvacuationTests`、`GrayboxEvacuationTests`。代码名：`GrayboxEvacuationPayloadPersistenceState3D`、`GrayboxEvacuationPersistenceState3D`、`GrayboxEvacuationRestorePlan3D`、`GrayboxEvacuationSaveAdapter3D`。

### 三维正式撤离协调与只读视图（仅限场景）

能解决什么：在正式三维场景中协调冻结撤离批次、内部物资迁移、稳定队列和只读清单。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`。怎么复用：在正式 3D 场景编排冻结撤离批次、稳定队列、内部载荷捕获、城市原子提交、运行时完成或遗弃，并发布不可变清单和队列 view。不能负责什么：只消费生产与防御运行时拥有的内部载荷，不拥有或重算载荷、退款、容量、战斗或物流真值；失败保留原 work 与锁供重试，不进入 schema 30、不接入冻结 2D，遗弃废墟不是前哨。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxFormalSaveEvacuationTests`、`GrayboxBuildingUiAndInputTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`EvacuationManifestItemViewModel`、`EvacuationManifestViewModel`、`EvacuationQueueViewModel`、`GrayboxEvacuationController3D`。

### 三维建筑共享运行与物流资格（推荐复用）

能解决什么：让生产与防御从同一建筑生命周期事实分别判断状态保留、本地运行和物流连接。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingOperationalAccess3D.cs`。怎么复用：用于从同一建筑生命周期事实分别判断会话状态保留、本地运行和物流连接。不能负责什么：只组合已完成、玩家所有、撤离锁定、建筑站点、城市模式与既有范围规则；不持有库存、生产或防御状态，不复制放置合法性。改后跑哪组测试：`GrayboxProductionRuntimeTests`、`GrayboxFirstDefenseRuntimeTests`。代码名：`GrayboxBuildingOperationalAccess3D`。

### 三维建筑世界视图（仅限场景）

能解决什么：在当前场景显示建筑、半透明放置预览和稳定前向标记，并消费正式世界尺度 Profile 统一地面/内城、施工/完成/废墟的表现包围。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs` 与 `Assets/_Game/Rendering/Graybox3D/GrayboxPreview.mat`。怎么复用：在场景内显示建筑、半透明预览和前向标记，并消费正式尺度 Profile 统一地面/内城与各生命周期表现。不能负责什么：只负责 GrayboxPrototype3D 的建筑表现；不得修改 BuildingCatalog 占地或自行决定锚点、旋转合法性、成本和节点兼容性。改后跑哪组测试：`GrayboxBuildingProjectionAndViewTests`、`FormalWorldPresentationScaleProfile3DTests`。代码名：`GrayboxBuildingWorldView3D`。

## UI 与输入

### 三维建筑输入路由（复用前审查）

能解决什么：把建造和撤离的正式输入送到正确界面，并在撤离期间保护模态优先级。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`。怎么复用：通过正式 Input System 路由建造与撤离输入，并在清单、处理和容量阻塞状态执行 F、Escape、E 与世界输入的模态优先级。不能负责什么：真实输入边界只发布界面命令；不决定建筑放置、退款、容量或战斗规则，不直接调用领域提交，也不接入冻结 2D。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`、`GrayboxBuildingRuntimeSceneTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`GrayboxBuildingInputRouter3D`。

### 三维建筑菜单视图（复用前审查）

能解决什么：显示建筑目录、放置反馈，以及正式撤离清单、队列、内部物资后果和容量阻塞操作。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`。怎么复用：显示建筑目录、旋转预览，并把所有可见建筑的锁定原因与蓝图放置失败原因固定显示在未改变的建造栏上方；同时显示正式撤离清单、稳定处理队列、内部物资后果和容量阻塞操作。不能负责什么：不保存建筑或库存数据，也不计算方向、退款、容量或战斗状态；全部读取控制器提供的不可变 view，图标统一复用 ResourceIconCatalog3D。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`、`GrayboxBuildingProjectionAndViewTests`、`GrayboxBuildingRuntimeSceneTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`GrayboxBuildingMenuView3D`。

### 三维生产可观察化控制器（仅限场景）

能解决什么：把当前 3D 会话的背包、30 条配方目录与两条可执行应急合成、43 节点正式研究、派生资源发现、资源状态栏、真实仓库详情、多配方生产和面板命令接到正式模型。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs`。怎么复用：在 GrayboxPrototype3D 内组合背包、30 条配方目录与两条可执行应急合成、43 节点正式研究、派生资源发现、资源状态栏、真实仓库详情、多配方生产与面板命令，并把真实输入提交到正式模型。不能负责什么：只属于当前 3D 场景的会话与 UI 适配；城市与仓库数量必须读取 CityResourceStorageModel，不替代资源、生产、研究、访问资格或输入路由真值，不进入 schema 31，也不得接入冻结 2D。改后跑哪组测试：`ManualResourceAccessRulesTests`、`GrayboxWarehouseStorageIntegrationTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`GrayboxOperationsController3D`。

### 三维生产可观察化视图（仅限场景）

能解决什么：呈现当前 3D 场景的文明式横向资源栏、路线/层级账本、背包、30 条配方合成页、43 节点正式科技树、真实仓库、多配方生产通道和共享图标。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchTreeView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchTreeViewportInput3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchSearchFocus3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/ResearchTreeConnectionGraphic3D.cs`。怎么复用：显示当前 3D 场景的文明式横向资源栏、路线/层级可筛选账本、背包与 30 条配方合成面板、43 节点自下向上正式科技树、真实仓库内容、多配方生产通道与共享资源图标；科技树提供确定连线、搜索、路线/状态筛选、拖动、指针中心缩放、全树/进行中定位和模态焦点。不能负责什么：只负责 GrayboxPrototype3D 的 UGUI 结构、表现态与命令事件；图标必须复用 ResourceIconCatalog3D，视图和输入表面不持有库存、队列、研究或解锁真值，不自行扣资源、推进时间、判断访问资格或进入 schema 31。改后跑哪组测试：`GrayboxVisualAndWorldTests`、`ResearchTreeUiContractTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`GrayboxOperationsView3D`、`GrayboxResearchTreeView3D`、`GrayboxResearchTreeViewportInput3D`、`GrayboxResearchSearchFocus3D`、`ResearchTreeConnectionGraphic3D`。

### 三维开发修改器中文目录查询（复用前审查）

能解决什么：让 Editor/Development 修改器用正式中文名浏览和搜索 31 种资源与 43 项科技。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperCatalogQuery3D.cs`。怎么复用：从正式资源与研究目录建立稳定顺序的中文查询快照，支持中文名和 stable ID 的规范化子串搜索，供 Editor/Development 修改器的列表与输入查找共用。不能负责什么：只负责只读目录投影和查询，不增加资源、不解锁科技、不持有搜索框或选择状态；命令仍须经过正式库存和研究入口，Release 不得引用修改器 UI 或行为。改后跑哪组测试：`GrayboxDeveloperModifierCatalogTests`、`GrayboxDeveloperModifierTests`、`GrayboxDeveloperModifierRuntimeInputTests`。代码名：`GrayboxDeveloperCatalogQuery3D`、`GrayboxDeveloperCatalogEntry3D`。

### 三维首版防御场景接线与表现（仅限场景）

能解决什么：把统一正式规则时钟、十波战役、全局暂停、唯一终局结算、完整会话统计、统一选择详情、HUD、真实输入和只消费已结算攻击事件的表现池接入默认三维场景。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHud3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs`。怎么复用：用于在 GrayboxPrototype3D 中连接统一规则时钟、十波战役、终局强制暂停、结算发布、统一选择 HUD、真实输入以及固定容量的三敌三塔和已结算攻击表现池。不能负责什么：仅限当前正式 3D 场景适配；结算模型与会话统计是规则真值，HUD 和表现对象不持有目标、伤害、耗材、建筑生命、波次或结算真值，攻击轨迹只消费已结算事件且不得重复播放，不接入冻结 2D。改后跑哪组测试：`GrayboxDefenseControllerTests`、`GrayboxDefenseObservabilityTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseSelectionProjectionTests`、`GrayboxDefenseSettledAttackPresentationTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`、`GrayboxDefenseRuntimeInputTests`。代码名：`GrayboxDefenseController3D`、`GrayboxDefenseHud3D`、`GrayboxDefenseHudView3D`、`GrayboxDefenseWorldView3D`。

### 三维防御终局结算模态（仅限场景）

能解决什么：把唯一胜负结算、完整统计、生产效率、修改器标记和胜败分流动作显示为阻断世界输入的正式三维模态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSettlementController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSettlementView3D.cs`。怎么复用：由防御控制器发布一次终局快照，控制器把继续沙盒、最近波前重试、返回标题与关闭失败反馈映射为正式命令，视图只渲染快照和命令状态。不能负责什么：只拥有 GrayboxPrototype3D 的模态展示与命令路由；不判断胜负、不计算统计、不读写存档、不直接恢复战役，也不接入冻结 2D。当前自动化通过，仍待人工试玩验证文案、焦点与真实操作体验。改后跑哪组测试：`GrayboxDefenseSettlementUi3DTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`。代码名：`GrayboxDefenseSettlementController3D`、`GrayboxDefenseSettlementView3D`。

### 三维防御统一选择详情与 HUD（仅限场景）

能解决什么：把敌人、机枪塔、城市核心、可受击建筑和废墟投影为同一份只读选择详情，并交给防御 HUD 显示。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSelectionProjection3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs`。怎么复用：从建筑会话、生命、生产和防御不可变快照生成统一 Tower、Enemy、Building、Ruin 详情，并由 HUD 精确显示战役波次、速度、生命、战斗参数、生产配方、内部库存和稳定停工原因。。不能负责什么：只读投影和显示当前正式快照；不搜索场景、不修改领域状态、不复制放置、物流或生产规则，不伪造废墟库存数量，也不把距核心误写成距当前目标。塔暂停命令只在统一详情明确允许时发布。。改后跑哪组测试：`GrayboxDefenseSelectionProjectionTests`、`GrayboxDefenseObservabilityTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseRuntimeInputTests`。代码名：`GrayboxDefenseSelectionSnapshot3D`、`GrayboxDefenseSelectionProjection3D`、`GrayboxDefenseHudView3D`。

### 三维已结算攻击事件与表现池（仅限场景）

能解决什么：把防御运行时已经完成规则结算的攻击事实送入可复用的命中、轨迹与伤害表现池。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs`。怎么复用：把规则层已经结算的稳定攻击事件投影到固定容量的敌人、塔和轨迹表现池，并按事件序号只消费一次机枪 tracer、激光束或孢子抛物线。。不能负责什么：事件只携带已结算攻击身份与伤害结果，表现池不得反向决定命中、伤害、目标或耗材；静止快照不得重复播放，池容量与回收仅属表现优化，不能丢失领域真值。。改后跑哪组测试：`GrayboxDefenseSettledAttackPresentationTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseSnapshotStabilityTests`。代码名：`GrayboxDefenseSettledAttackEvent3D`、`GrayboxDefenseWorldView3D`。

### 三维灰盒显示设置边界（复用前审查）

能解决什么：把显示设置规则、偏好存储和 Unity 平台应用拆成可测试边界。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsModel3D.cs` 和 `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsAdapters3D.cs`。怎么复用：在三维灰盒中以 IGrayboxDisplaySettingsStore 和 IGrayboxDisplaySettingsPlatform 分离可测试的显示设置模型、偏好存储和 Unity 平台应用边界。不能负责什么：PlayerPrefs 只保存独立显示偏好，明确位于正式存档 schema 30 之外；跨项目复用前需复核键名、版本和平台能力。改后跑哪组测试：`GrayboxUsabilityTests`。代码名：`GrayboxDisplaySettingsModel3D`、`PlayerPrefsGrayboxDisplaySettingsStore3D`、`UnityGrayboxDisplaySettingsPlatform3D`。

### 三维灰盒系统菜单控制器（仅限场景）

能解决什么：协调当前灰盒场景的菜单页面、系统暂停、设置和安全退出。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuController3D.cs`。怎么复用：协调灰盒场景内菜单页、系统暂停、设置和安全退出。不能负责什么：依赖当前灰盒 GameSpeedModel 与场景接线，不是通用前端菜单框架。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxSystemMenuController3D`。

### 三维灰盒系统菜单场景视图（仅限场景）

能解决什么：显示当前 GrayboxPrototype3D 的模态系统菜单层级和文案。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs`。怎么复用：显示当前三维灰盒场景的模态系统菜单。不能负责什么：层级、文案和 UGUI 引用属于 GrayboxPrototype3D；不得未经复核提升为通用可复用视图。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxSystemMenuView3D`。

### 三维灰盒易用性输入协调器（仅限场景）

能解决什么：按终局结算模态、既有取消链、开发面板和系统菜单优先级协调当前灰盒场景输入。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`。怎么复用：结算打开时先阻断全部世界输入并只分发继续沙盒、最近波前重试、返回标题和允许的关闭命令；无结算时再按既有取消链、开发面板和系统菜单优先级分发输入。不能负责什么：只协调当前场景输入消费者，不判断结算动作是否合法、不复制建筑或存档规则，也不作为全项目通用输入总线。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxUsabilityInputCoordinator3D`。

## 资源、研究、人口、战斗和存档

### 正式资源定义目录（推荐复用）

能解决什么：集中提供 31 种正式资源的稳定 ID、路线层级、来源用途、发现规则、图标与正式 3D 初始城市数量。在哪里：`Assets/_Game/Scripts/Economy/ResourceDefinitionCatalog.cs`。怎么复用：提供 31 项正式资源的稳定标识、中文名称、路线层级、来源用途、发现规则、图标 ID、背景简介、显示尺寸与基础资源栏顺序。资源 UI、背包和正式 3D 会话接入时统一从目录读取定义；正式城市底层账本通过 `CreateFormalCityInventory` 创建。不能负责什么：只定义资源身份与不可变静态配置；不保存库存数量，不执行转移、生产、发现状态或界面输入。工厂返回的是允许保留超额数量的 backing ledger，不代表城市拥有无限有效容量；正式 3D 入库与读取必须经过 `CityResourceStorageModel`。改后跑哪组测试：`ResourceDefinitionCatalogTests`。代码名：`ResourceDefinition`、`ResourceDefinitionCatalog`。

### 正式资源发现投影（推荐复用）

能解决什么：从玩家已经拥有或研究到的权威事实，确定当前资源栏与账本应显示哪些材料。在哪里：`Assets/_Game/Scripts/Economy/ResourceDiscoveryProjection.cs`。怎么复用：把城市核心、全部真实仓库、背包、生产输入/输出/预留、合成预留和已完成研究聚合为只读事实，再按 ResourceDefinitionCatalog 的正式发现规则投影当前可见资源。不能负责什么：发现状态完全从 schema 31 已有权威事实派生，不保存永久发现位；断开物流的仓库仍计入玩家所有资产，但不改变联网可用数量。投影不转移资源、不推进研究，也不持有 UI 筛选状态。改后跑哪组测试：`ResourceDiscoveryProjectionTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`ResourceDiscoveryFacts`、`ResourceDiscoveryProjection`。

### 资源库存（推荐复用）

能解决什么：作为兼容的底层资源数量账本，按稳定资源 ID 保存整数数量，并提供覆盖全部已记录资源的确定性正数量快照与受验证的原子全量替换。在哪里：`Assets/_Game/Scripts/Economy/ResourceInventory.cs`。怎么复用：按稳定资源 ID 管理资源数量，提供覆盖全部已记录资源的确定性正数量快照与受验证的原子全量替换；恢复时可由调用方显式选择资产保守地保留超容量数量。不能负责什么：只负责资源账本的数量、容量和变更通知；不驱动生产周期，不决定配方、物流、建筑资格、存档兼容策略或界面行为。改后跑哪组测试：`FoundationTests`、`GrayboxFormalSaveProductionTests`、`ResourceInventoryChangeTests`。代码名：`ResourceChangeAttribution`、`ResourceInventory`。

### 旧每资源城市容量策略（复用前审查）

能解决什么：保留 `IDEA-0011` 的基础容量加有效仓库数、且每种资源分别增加 150 的旧兼容算法。在哪里：`Assets/_Game/Scripts/Economy/ResourceCapacityPolicy.cs`。怎么复用：保留 IDEA-0011 的基础容量加每座仓库、且每种资源分别扩容的旧兼容算法，供旧接口和冻结回归使用；IDEA-0012 的正式 3D 仓库不再使用此模型。不能负责什么：IDEA-0012 已用每仓库 150 共享总容量替代旧的每资源加仓库模型；正式 3D 必须通过 CityResourceStorageModel 读写城市与仓库库存，不得向 ResourceCapacityPolicy 传仓库数模拟真实仓库。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceCapacityPolicy`。

### 城市与真实仓库库存模型（推荐复用）

能解决什么：提供正式 3D 城市库存唯一聚合入口，并让撤离与 schema 31 恢复共享原子计划边界。在哪里：`Assets/_Game/Scripts/Economy/CityResourceStorageModel.cs`。怎么复用：作为正式 3D 城市库存唯一聚合入口，按稳定仓库 ID 处理联网数量、确定性存取和不可变快照；撤离及 schema 31 恢复都使用绑定 owner/revision 的预检计划和单次原子提交。恢复可深拷贝保留未知孤立资源，并在配置签名变化时保留超额资产为只出不进。。不能负责什么：不决定建筑处理、完成状态、玩家所有权、物流距离或交互资格；战损只提供绑定 revision 的原子损失预检与提交，不决定摧毁目标或损失比例。调用方必须验证建筑交叉引用并在恢复后重算连接；本模型不替代 WorldMapModel、ResourceInventory 或 WarehouseStorageState。。改后跑哪组测试：`CityResourceStorageModelTests`、`CityResourceStorageCombatLossTests`、`GrayboxEvacuationTests`、`GrayboxFormalSaveBuildingStorageTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`CityResourceStorageRestorePlan`、`CityResourceEvacuationPlan`、`CityResourceChangeAttributionScope`、`CityResourceStorageModel`、`CityResourceStorageSnapshot`、`CityStorageOrphanResource`、`CityWarehouseRestoreEntry`。

### 单仓库共享容量状态（推荐复用）

能解决什么：保存一座仓库的真实内容、150 共享总容量、过滤和兼容恢复状态。在哪里：`Assets/_Game/Scripts/Economy/WarehouseStorageState.cs`。怎么复用：按稳定建筑实例 ID 保存一座仓库的 150 共享总容量、真实内容、联网状态与可选单资源过滤，并发布不可变快照；schema 31 恢复可保留未知过滤和占用共享空间的孤立资源，配置变化造成的超额状态保持只出不进。不能负责什么：只拥有单仓库会话状态，不聚合城市库存、不计算物流范围、不执行建筑生命周期；恢复仓库先保持断开，连接由正式运行时重算。孤立资源占用容量但不可作为已知资源消费；正式调用必须由 CityResourceStorageModel 统一编排。改后跑哪组测试：`CityResourceStorageModelTests`、`GrayboxFormalSaveBuildingStorageTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`WarehouseStorageState`、`WarehouseStorageSnapshot`。

### 资源缺口规则（推荐复用）

能解决什么：按正式成本顺序给出每种材料的拥有、需要和缺少数量，让“材料不足”变成精确可读反馈。在哪里：`Assets/_Game/Scripts/Economy/ResourceShortfallRules.cs`。怎么复用：按正式成本顺序计算每种材料的拥有、需要与缺少数量，供放置失败和其他资源不足反馈统一投影。不能负责什么：只计算纯缺口数据，不读取 Unity 场景、不执行扣款、不决定放置合法性，也不生成最终 UI 文案；调用方必须传入当前权威库存读取函数。改后跑哪组测试：`ResourceShortfallRulesTests`。代码名：`ResourceShortfall`、`ResourceShortfallRules`。

### 原子资源事务（推荐复用）

能解决什么：在城市账本、建筑账本和玩家背包之间提供多输入扣除、输出预检及守恒转移基础，并统一返回完成、部分完成或失败状态。在哪里：`Assets/_Game/Scripts/Economy/ResourceTransaction.cs`。怎么复用：聚合同资源请求，预检输入与输出，并执行批量提交和允许部分接收的原子转移。生产、研究和人工转移必须使用事务入口，不得在 UI 或控制器中自行拼接 `TrySpend`、`Add`、`Remove`；当前已覆盖账本批事务和背包单资源双向转移。背包合成的多输入预留、产出与取消返还已由 `CraftingQueueModel` 通过槽位快照和完整回滚实现，并由 `CraftingQueueModelTests` 保护。正式事务只允许使用已有非负余额，不借用旧债务额度。不能负责什么：只处理资源数量与容量提交；不决定物流连接、交互距离、建筑资格、配方周期或界面状态。也不统计仓库数量。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceAmount`、`ResourceTransferResult`、`ResourceTransaction`。

### 玩家背包模型（推荐复用）

能解决什么：维护会话级 30 格个人背包，并为 schema 31 提供受验证的两阶段恢复边界。在哪里：`Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs`。怎么复用：管理三十格会话背包及稳定堆叠、拆分、逐个移动、整栈合并与交换；schema 31 通过深拷贝的固定索引 snapshot、零写入 prepare 和 owner-bound 单次 commit 恢复，按各资源正式栈上限验证并可显式保留配置变化后的超栈。不能负责什么：只拥有背包槽位状态；未知稳定资源保留在原槽且不可正常存取或移动。模型不访问城市或建筑库存，不判定交互资格，不处理 Unity 输入、UI、文件 IO 或内容配置兼容决策；调用方必须显式决定是否允许超栈恢复。改后跑哪组测试：`PlayerBackpackModelTests`、`GrayboxFormalSaveEconomyTests`。代码名：`BackpackSlot`、`PlayerBackpackRestoreSlot`、`PlayerBackpackRestorePlan`、`PlayerBackpackModel`。

### 正式资源配方目录（推荐复用）

能解决什么：用一份正式目录统一 30 条机器、应急与融合配方，避免 UI、生产和合成各自复制数值。在哪里：`Assets/_Game/Scripts/Economy/ResourceRecipeCatalog.cs`。怎么复用：统一提供 30 条正式机器/应急/融合配方的稳定 ID、复数输入输出、周期、建筑适用范围、默认配方、绑定节点动态产出与全量研究前置。不能负责什么：只定义正式配方静态配置；FormalProductionDefinitionCatalog 可按建筑和配方 ID 投影机器定义，但本目录不拥有队列、背包、建筑缓存、选择或进度，也不执行资源事务、自动串联或 UI 手势。改后跑哪组测试：`CraftingQueueModelTests`、`ResourceRecipeCatalogIntegrityTests`。代码名：`ResourceRecipeDefinition`、`ResourceRecipeCatalog`。

### 应急合成队列（推荐复用）

能解决什么：维护最多 20 次执行的 FIFO 应急合成队列，并让 schema 31 精确保留执行身份、预留与历史高水位。在哪里：`Assets/_Game/Scripts/Economy/CraftingQueueModel.cs`。怎么复用：管理最多 20 次执行的 FIFO 应急合成队列，在入队时原子预留背包输入，并处理暂停、产出阻塞和取消返还；schema 31 公开捕获稳定执行 ID、预留输入、活动进度与 nextQueueOrdinal，并以绑定 revision/owner 的 prepare/commit 恢复。不能负责什么：只拥有当前会话的合成队列、预留材料、活动进度和阻塞原因；恢复不会再次扣除已预留输入，未知配方保留为 MissingContent 暂停项并允许取消退款。模型不访问城市或建筑库存，不解释鼠标手势，不拥有文件 IO，也不把当前队列最大 ID 当作历史高水位。改后跑哪组测试：`CraftingQueueModelTests`、`GrayboxFormalSaveEconomyTests`。代码名：`CraftingQueueRestoreEntry`、`CraftingQueueExecutionSnapshot`、`CraftingQueueRestorePlan`、`CraftingQueueModel`。

### 手工资源访问规则（推荐复用）

能解决什么：统一判断当前直接控制对象是否可以手工访问城市库存或某座建筑库存。在哪里：`Assets/_Game/Scripts/Economy/ManualResourceAccessRules.cs`。怎么复用：按当前直接控制目标、领袖招募、两格欧氏距离、footprint 和建筑生命周期事实评估城市或建筑库存的手工访问资格。调用方应在每次资源操作提交前重新传入当前权威事实。不能负责什么：只返回纯访问判定；不查找场景对象、不解析旋转尺寸、不执行资源转移，也不缓存资格。调用方必须在每次提交时传入当前事实和权威 footprint。改后跑哪组测试：`ManualResourceAccessRulesTests`。代码名：`ManualResourceAccessRules`。

### 正式机器生产定义目录（推荐复用）

能解决什么：把正式配方目录中的机器配方转换为生产运行时统一使用的定义，并保留采矿、冶炼、装配三个旧兼容入口。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionDefinitionCatalog.cs`。怎么复用：把 ResourceRecipeCatalog 的正式机器配方按建筑投影为运行时生产定义，并保留采矿、冶炼和装配三个兼容入口；输入、输出、周期和内部容量仍以正式配方目录为真值。不能负责什么：只负责机器配方到运行时定义的确定性投影与缓存；不保存建筑实例当前配方或库存，不推进周期，不判断物流连接，也不维护第二套配方数值。改后跑哪组测试：`FormalProductionSimulationTests`、`ResourceRecipeCatalogIntegrityTests`。代码名：`FormalProductionDefinition`、`FormalProductionDefinitionCatalog`。

### 逐建筑生产状态（推荐复用）

能解决什么：为每个稳定建筑实例和当前配方拥有独立的多资源输入、输出、完整批次预留、进度与玩家暂停，并支持 schema 31 原子恢复。在哪里：`Assets/_Game/Scripts/Economy/BuildingProductionState.cs`。怎么复用：按稳定建筑实例和当前配方拥有多资源输入、多资源输出、完整批次预留、进度与玩家暂停；schema 31 恢复先以配方输入集合验证无序预留和资源语义，再原子替换单建筑状态。不能负责什么：只拥有单座建筑的生产状态，不自行读取场景或城市范围。物流连接与停工原因属于派生状态而不入档；玩家暂停入档，并在恢复时据此重建 PlayerPaused 停工原因。改后跑哪组测试：`FormalProductionSimulationTests`、`GrayboxFormalSaveProductionTests`。代码名：`BuildingProductionState`。

### 正式生产与物流模拟（推荐复用）

能解决什么：在一个由调用方确定的物流步内，按稳定实例 ID 卸载所有旧输出、补足所有输入，原子预留完整批次并在全部输出可容纳时完成生产；采矿仍只调用 `WorldMapModel.Harvest`。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionSimulation.cs`。怎么复用：按稳定实例顺序通过 CityResourceStorageModel 执行确定性物流步，对全部输入/输出通道进行原子预留、容量预检和批次完成，推进独立生产周期并通过世界地图真值完成采矿。不能负责什么：不选择配方，不计算放置合法性、物流距离、建筑生命周期或场景时间；调用方必须提供已确认资格、连接状态和正式城市仓库聚合模型。保留 ResourceCapacityPolicy 重载只用于旧接口兼容。改后跑哪组测试：`FormalProductionSimulationTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`FormalProductionSimulation`。

### 三维生产建筑资格（复用前审查）

能解决什么：从既有建筑生命周期统一派生仓库是否应计入容量。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionEligibility3D.cs`。怎么复用：从现有三维建筑实例生命周期派生有效仓库资格。不能负责什么：只组合已完成、玩家拥有、未撤离锁定和稳定建筑 ID；不计算容量、物流距离、配方或放置合法性。改后跑哪组测试：`GrayboxProductionLifecycleTests`、`GrayboxProductionRuntimeTests`。代码名：`GrayboxProductionEligibility3D`。

### 三维生产运行时（复用前审查）

能解决什么：让允许配方、当前配方、生产状态与物流连接跟随三维建筑生命周期，并为 schema 31 提供确定性持久化快照和安全恢复边界。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs`。怎么复用：按稳定实例 ID 同步允许配方列表、当前配方、生产状态、可运行集合和物流连接；只在进度、预留及输入输出缓存均为空时提交安全切换。schema 31 确定性保存非默认配方，未知配方保留为不可运行 orphan，并通过绑定 owner、同步 generation 与内容 fingerprint 的 prepare/commit 恢复。。不能负责什么：只桥接 GrayboxBuildingInstance3D、正式配方目录与逐建筑生产状态；不推进时间、不执行城市事务，撤离协调器不能修改载荷快照。不得复制放置、物流范围或节点兼容规则；物流连接、停工原因和 observability revision/hash 仍由当前规则重建而不持久化。。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxFormalSaveProductionTests`、`GrayboxProductionCombatLossTests`、`GrayboxProductionRuntimeTests`、`GrayboxProductionLifecycleTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxProductionEvacuationPayload3D`、`GrayboxProductionPersistenceState3D`、`GrayboxProductionRestorePlan3D`、`GrayboxProductionRuntime3D`。

### 三维生产固定时钟（复用前审查）

能解决什么：让不同帧率下的三维生产保持同一固定步结果，在暂停期间不积累追赶时间，并发布可原子消费的生产活跃、合格与完成批次统计增量。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionClock3D.cs`。怎么复用：用 0.1 秒固定步长驱动运行时与正式生产模拟，保证分帧确定性和暂停无追赶；结算统计只消费 `ProductionStatisticsDelta`，玩家暂停时不计生产合格时间。不能负责什么：只拥有会话级余量、上一轮统计增量并组合运行时、模拟和 CityResourceStorageModel；不读取 Unity Time，不决定建筑资格，不累计完整会话统计，不处理 UI。旧 ResourceInventory 重载仅用于兼容回归。改后跑哪组测试：`GrayboxProductionClockTests`、`GrayboxProductionStatisticsDeltaTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`ProductionStatisticsDelta`、`GrayboxProductionClock3D`。

### 三维生产可观察化只读边界（推荐复用）

能解决什么：向资源状态栏和生产面板提供按稳定实例 ID 排序、包含允许/当前配方及全部输入/输出/预留通道的不可变详情，并把配方切换、暂停和逐通道转移收口到命令门面。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/ProductionObservabilitySnapshot.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionCommandFacade3D.cs`。怎么复用：发布按稳定实例 ID 排序、包含允许/当前配方和全部输入/输出/预留通道的不可变生产详情；仓库与城市库存变化通过 CityResourceStorageModel.Revision 进入内容哈希。命令门面按 stable ID 提交配方切换、暂停和建筑缓存/背包/权威城市仓库之间的通道转移。不能负责什么：快照只读，不暴露 BuildingProductionState 或可变库存；命令不接受 UI 传入仓库数量。访问距离、物流和建筑生命周期资格仍由当前 3D 场景适配器在每次提交前基于权威事实重新验证；本边界不复制资格规则、不接入冻结 2D，也不进入 schema 30。改后跑哪组测试：`GrayboxProductionObservabilityFacadeTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`ProductionResourceObservability`、`ProductionBuildingObservability`、`ProductionObservabilitySnapshot`、`GrayboxProductionCommandFacade3D`。

### 三维生产场景控制器（仅限场景）

能解决什么：把默认三维场景的真实建筑、城市、世界与暂停状态送进固定生产时钟。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionController3D.cs`。怎么复用：把当前三维场景的建筑会话、城市模式、世界坐标、CityResourceStorageModel 和 Unity 暂停状态接到固定步生产时钟。不能负责什么：只负责 GrayboxPrototype3D 场景引用与时间输入；不复制生产配方、物流范围、资源节点兼容性、仓库库存事务或界面规则。改后跑哪组测试：`GrayboxProductionControllerTests`、`GrayboxSceneContractTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxProductionController3D`。

### 研究模型（推荐复用）

能解决什么：管理研究状态，并在 schema 31 恢复时区分正式科技、退役兼容内容与可保留的缺失内容。在哪里：`Assets/_Game/Scripts/Research/ResearchModel.cs`。怎么复用：管理已完成科技、活动科技和剩余规则时间，并为 schema 31 提供确定性 snapshot、零写入 prepare 和绑定 revision/owner 的单次 commit；语法有效的未知已完成科技与未知活动科技会原样保留。不能负责什么：未知已完成科技不授予效果，未知活动科技保持暂停；恢复不重新扣除研究资源，也不保存或伪造研究站资格、城市倍率、UI 或表现。模型不拥有文件 IO，正式 3D 目录解析由调用方显式提供。改后跑哪组测试：`ResearchTests`、`FormalResearchCatalogTests`、`DemoResearchRuntimeTests`、`GrayboxFormalSaveEconomyTests`。代码名：`ResearchPersistenceSnapshot`、`ResearchRestorePlan`、`ResearchModel`。

### 正式四路线科技运行时（推荐复用）

能解决什么：用正式 43 节点目录统一当前 3D 研究启动、推进、暂停、退款和 schema 31 恢复。在哪里：`Assets/_Game/Scripts/Research/FormalResearchRuntime.cs`。怎么复用：组合统一 ResearchModel 与正式 43 节点 ResearchCatalog，提交稳定 ID 研究启动、城市形态/思维加速倍率推进、研究站与全局暂停、80% 原子退款，以及 schema 31 正式目录恢复。不能负责什么：不持有研究站、城市模式、暂停或库存真值，不处理 Unity 输入、科技树布局、文件 IO 或效果表现；未知活动科技冻结并继续通过持久化往返，初始根科技在新会话和恢复后由正式运行时修复。改后跑哪组测试：`FormalResearchRuntimeTests`、`GrayboxFormalResearchSaveAdapterTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`FormalResearchRuntime`。

### 正式科技树确定性投影（复用前审查）

能解决什么：把正式目录变成稳定的 43 节点、48 条依赖边、自下向上坐标和可复用视口计算。在哪里：`Assets/_Game/Scripts/Graybox3D/ResearchTreeProjection3D.cs`。怎么复用：把正式科技目录投影为稳定的 43 节点、48 依赖边、自下向上固定坐标与图空间边界，并提供筛选保持布局、最新可研究选择、Fit/Focus 和指针锚定缩放纯计算。不能负责什么：只拥有不可变图空间投影，不创建 Unity 场景对象，不读取输入、库存、研究运行态或存档，也不解析中文状态文案；路线布局常量变更必须同步设计规格与非重叠测试。改后跑哪组测试：`ResearchTreeProjection3DTests`、`ResearchTreeUiContractTests`。代码名：`ResearchTreeProjection3D`、`ResearchTreeNodeProjection3D`、`ResearchTreeEdgeProjection3D`、`ResearchTreeViewportState3D`。

### 三维首版科技目录（推荐复用）

能解决什么：保留 A16.4 历史六节点 release profile 的稳定 ID 与退役内容元数据。在哪里：`Assets/_Game/Scripts/Research/DemoResearchCatalog.cs`。怎么复用：保留 A16.4 历史六节点 release profile 的稳定 ID 与退役内容元数据，供旧测试、旧存档和兼容 facade 使用；正式 3D 科技树与新运行时必须读取 ResearchCatalog。不能负责什么：只定义历史 Demo 兼容配置；不得重新成为正式枚举源，不保存完成状态、不扣资源、不推进时间，也不向正式 43 节点目录静默映射退役 ID。改后跑哪组测试：`DemoResearchRuntimeTests`。代码名：`DemoResearchCatalog`。

### 三维首版科技运行时（推荐复用）

能解决什么：在当前 3D 会话中统一提交科技运行规则，并为 schema 31 提供六节点目录解析边界。在哪里：`Assets/_Game/Scripts/Research/DemoResearchRuntime.cs`。怎么复用：组合统一研究模型与六节点 release profile，提交研究启动、模式倍率推进、研究站暂停和 80% 原子取消退款，并以该 release profile 显式解析 schema 31 的已知与未知科技恢复状态。不能负责什么：只拥有当前 3D 会话的研究规则适配；未知已完成科技不授予效果，未知活动科技暂停且可由持久化快照继续保留。调用方仍须提供合格研究站、城市模式、全局暂停、城市库存与容量事实；它不处理 Unity 输入、UI、文件 IO、关注度或战斗效果。改后跑哪组测试：`DemoResearchRuntimeTests`、`GrayboxFormalSaveEconomyTests`。代码名：`DemoResearchRuntime`。

### 三维统一正式规则时钟（仅限场景）

能解决什么：把正式三维会话中的速度档位、暂停和 Unity 帧时间统一换算为唯一有效规则时间。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxFormalRuleClock3D.cs`。怎么复用：在正式三维场景把玩家请求速度、系统菜单暂停、终局强制暂停和固定步余量合并为唯一有效规则时间，再按固定顺序推进城市、施工、生产、防御和撤离。。不能负责什么：只作为 GrayboxPrototype3D 的规则时钟组合根；不拥有各领域玩法状态，不使用 Unity 表现时间替代规则时间，不允许领域控制器另起 Update 时钟。暂停和速度输入仍由正式命令门发布。。改后跑哪组测试：`GrayboxUnifiedRuleClockContractTests`、`FormalGameSpeedCommandFacadeTests`、`GrayboxFormalSpeedHudAndTerminalTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`。代码名：`GrayboxFormalRuleClock3D`。

### 单城市十波防御战役模型（推荐复用）

能解决什么：用正式十波目录确定性推进预警、生成、清场、胜负、稳定目标选择、敌塔攻防和完整会话统计。在哪里：`Assets/_Game/Scripts/Combat/CampaignWaveCatalog.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseCampaignModel.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseTowerCombatModel.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseTowerPersistenceState.cs`。怎么复用：以正式十波目录推进预警、生成、清场和胜负阶段，按稳定目标与正式敌塔配置结算移动、索敌、攻击、耗材、建筑受击，并由唯一 `SessionStatisticsModel` 聚合生产、防御、战损、暂停和修改器事实；塔战斗余量可独立持久化并确定性恢复。不能负责什么：只拥有纯战役、塔战斗和会话统计规则状态；不读取 Unity 时间、场景对象、城市库存或建筑会话，不直接摧毁建筑、不写 schema DTO，也不创建 HUD、轨迹或伤害表现。调用方必须提供稳定建筑目标、核心位置和已批准配置。改后跑哪组测试：`SingleCityDefenseCampaignCatalogTests`、`SingleCityDefenseCampaignModelContractTests`、`SingleCityDefenseCampaignCheckpointTests`、`SingleCityDefenseEnemyCampaignCombatTests`、`SingleCityDefenseTowerCombatModelTests`、`SingleCityDefenseTowerPersistenceTests`、`SingleCityDefenseTowerTargetingTests`、`SessionStatisticsTests`。代码名：`CampaignWaveDefinition`、`CampaignWaveCatalog`、`DefenseBuildingTargetCandidate`、`DefenseBuildingCombatTarget`、`SingleCityDefenseEnemySnapshot`、`SingleCityDefenseCampaignStatisticsSnapshot`、`SingleCityDefenseCampaignSnapshot`、`SingleCityDefenseCampaignModel`、`SingleCityDefenseTowerCombatModel`、`SingleCityDefenseTowerPersistenceState`。

### 通用会话统计模型（推荐复用）

能解决什么：以稳定不可变快照聚合十波会话的规则时间、敌人、塔攻击、资源消耗、建筑战损、生产活跃/合格时间、完成批次、暂停时间和修改器使用标记。在哪里：`Assets/_Game/Scripts/Core/SessionStatisticsModel.cs`。怎么复用：各正式领域只提交已结算事实增量，模型负责非负验证、原子恢复、终局冻结、部分迁移标记与生产效率所需原始统计；结算层只读快照。不能负责什么：不读取 Unity 时间、不推断未发生事件、不拥有战役胜负、UI、DTO 或文件 IO；schema 31 缺失历史不得伪造成完整统计。改后跑哪组测试：`SessionStatisticsTests`、`GrayboxProductionStatisticsDeltaTests`、`SingleCityDefenseCampaignCheckpointTests`。代码名：`SessionStatisticsMetric`、`SessionStatisticsSnapshot`、`SessionStatisticsModel`。

### 单城市防御终局结算模型（推荐复用）

能解决什么：从唯一战役终局与会话统计生成一次性胜负结算快照、生产效率、统计可用性、修改器标记和允许动作。在哪里：`Assets/_Game/Scripts/Defense/SingleCityDefenseSettlement.cs`。怎么复用：终局 revision 变化时发布一次结算；胜利允许继续沙盒或返回标题，失败允许最近波前重试或返回标题，缺少生产合格时间时明确显示无数据，迁移统计明确标为部分统计。不能负责什么：不推进战役、不修改会话统计、不读写重试档、不持有 Unity UI 或输入焦点；人工试玩确认前只能标记为已实现待验证。改后跑哪组测试：`SingleCityDefenseSettlementTests`、`GrayboxDefenseSettlementUi3DTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`。代码名：`SingleCityDefenseSettlementMetric`、`SingleCityDefenseSettlementSessionStatistics`、`SingleCityDefenseSettlementSnapshot`、`SingleCityDefenseSettlementModel`。

### 三维建筑战斗生命运行时（复用前审查）

能解决什么：以稳定建筑实例维护正式三维建筑的最大生命、当前生命和废墟状态，并提供可恢复快照。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingHealthRuntime3D.cs`。怎么复用：按稳定建筑实例维护正式最大生命、当前生命与战斗摧毁边界，并从建筑会话同步新建、施工、完成、遗迹和战损废墟状态。。不能负责什么：只拥有当前三维会话的建筑生命真值；不决定敌人目标、伤害数值、建筑移除、库存损失、生产状态、撤离规则或视觉样式。摧毁后的跨域提交必须交给统一战斗摧毁协调器。。改后跑哪组测试：`GrayboxBuildingHealthRuntime3DTests`、`GrayboxBuildingCombatLifecycleTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`。代码名：`GrayboxBuildingHealthRuntime3D`。

### 三维建筑战损原子结算协调器（复用前审查）

能解决什么：在建筑生命归零后按稳定实例一次性协调城市库存、生产、防御和建筑会话的战损清理。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCombatDestructionCoordinator3D.cs`。怎么复用：以稳定建筑实例和结算序号协调建筑生命、内部生产或塔库存损失、城市库存转移、会话战损废墟转换与派生状态刷新，重复结算保持幂等。。不能负责什么：只协调一次已确认的建筑摧毁事务；不计算攻击命中、伤害、索敌、掉落或退款，不绕过 CityResourceStorageModel、生产运行时、塔运行时和建筑会话各自的预检与提交边界。失败结果不得伪装成已摧毁。。改后跑哪组测试：`GrayboxCombatDestructionCoordinator3DTests`、`CityResourceStorageCombatLossTests`、`GrayboxProductionCombatLossTests`、`GrayboxDefenseTowerCombatLossTests`。代码名：`GrayboxCombatDestructionResult3D`、`GrayboxCombatDestructionCoordinator3D`。

### 首版防御战斗模型（推荐复用）

能解决什么：以确定性规则时间处理机枪塔索敌射击、啃噬者生命和城市核心受击，并保留实体级持久化状态。在哪里：`Assets/_Game/Scripts/Defense/FirstDefenseCombatModels.cs`。怎么复用：用于以确定性规则时间处理机枪塔索敌射击、啃噬者生命与城市核心受击，并通过受验证的持久化状态保留塔弹药、暂停、弹药租约与伤害余量，以及活动敌人的生命、位置和攻击余量。不能负责什么：只拥有首版战斗实体规则与实体级持久化状态；不定义 schema DTO、不执行文件 IO、不推进教学波、不访问城市库存，tracer、目标和状态文案不持有命中、伤害或耗弹真值。改后跑哪组测试：`FirstDefenseLoopTests`、`GrayboxFormalSaveDefenseTests`。代码名：`MachineGunTurretPersistenceState`、`MachineGunTurretCombatModel`、`DefenseEnemyPersistenceState`、`DefenseEnemyCombatModel`、`CityCoreCombatModel`。

### 首版教学防御波运行时（推荐复用）

能解决什么：推进十五秒预警、八只啃噬者四十秒分批生成、直达核心和核心受击，并保留教学波持久化状态。在哪里：`Assets/_Game/Scripts/Defense/FirstDefenseWaveRuntime.cs`。怎么复用：用于推进十五秒预警、八只啃噬者四十秒分批生成、直达核心和核心受击，并通过受验证的持久化状态保留触发与波次阶段、警告和生成时钟、计数高水位、固定步余量、冻结出生点、活动敌人与核心生命。不能负责什么：只拥有首个教学波与城市核心会话状态；不处理建筑发现、塔状态、配置签名、schema DTO、城市库存、Unity 时间、表现对象或正式失败结算。改后跑哪组测试：`FirstDefenseWaveRuntimeTests`、`GrayboxFormalSaveDefenseTests`。代码名：`DefenseEnemyRuntimeSnapshot`、`DefenseRuntimeSnapshot`、`TutorialDefensePersistenceState`、`TutorialDefenseRuntimeModel`。

### 三维首版防御运行时（复用前审查）

能解决什么：按稳定建筑实例同步机枪塔与塔内弹药，桥接正式十波战役、建筑生命和战损结算，并以事务式边界持久化全部防御领域状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`。怎么复用：按稳定建筑实例同步三类正式塔、塔内耗材、十波战役、活动敌人与已结算攻击事件，并组合建筑生命和原子摧毁边界，按废墟稳定 ID 保留当前运行时会话的实际战损摘要；持久化时确定性保留固定步余量与全部既有防御领域状态。。不能负责什么：只桥接正式 3D 建筑会话、城市仓储与纯战役模型，并拥有防御运行时事务；不执行文件 IO 或场景搜索，不复制物流范围、建筑资格、库存事务或表现状态。攻击事件只发布已结算事实，表现层不得借此成为第二套战斗真值；废墟损失摘要不进入现有 schema 32，存档恢复后必须明确显示明细不可用。。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxFirstDefenseRuntimeTests`、`GrayboxDefenseSnapshotStabilityTests`、`GrayboxFormalSaveDefenseTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`、`GrayboxDefenseSettledAttackPresentationTests`。代码名：`GrayboxDefenseEvacuationPayload3D`、`GrayboxDefenseTowerRuntimeState3D`、`GrayboxDefenseTowerSnapshot3D`、`GrayboxDefenseEnemySnapshot3D`、`GrayboxDefenseSettledAttackEvent3D`、`GrayboxDefenseRuntimeSnapshot3D`、`GrayboxDefensePersistenceState3D`、`GrayboxDefenseRestorePlan3D`、`GrayboxDefenseRuntime3D`。

### 人口模型（推荐复用）

能解决什么：管理人口容量。在哪里：`Assets/_Game/Scripts/Population/PopulationModel.cs`。怎么复用：用于管理人口容量。由模型维护容量数据。不能负责什么：不控制人口表现。改后跑哪组测试：`PopulationAndCapacityTests`。代码名：`PopulationModel`。

### 正式存档数据（复用前审查）

能解决什么：保持 schema `1–30` 旧 2D payload 的历史身份与兼容回归能力。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveData.cs`。怎么复用：保留 legacy 2D schema 1–30 的历史字段形状，供统一 codec 解码、验证历史档并执行固定夹具兼容回归。不能负责什么：这是只读兼容身份，不是现役运行时存档所有者，不承载 schema 31 正式 3D payload，不得作为新 3D 功能起点，也不提供 2D 到 3D 的正式迁移；任何字段变更仍需兼容性评审。改后跑哪组测试：`FormalSaveTests`、`FormalSaveEnvelopeTests`、`FormalSaveValidatorTests`。代码名：`FormalSaveData`。

### 历史二维存档兼容 DTO（禁止用于新功能）

能解决什么：保留 schema 1–30 历史存档中建筑、敌人和友军快照的原命名空间与 public 字段形状，供旧档解码和验证。在哪里：`Assets/_Game/Scripts/Persistence/Legacy2D/BuildingSnapshot.cs`、`Assets/_Game/Scripts/Persistence/Legacy2D/EnemySnapshot.cs`、`Assets/_Game/Scripts/Persistence/Legacy2D/FriendlyUnitSnapshot.cs`。怎么复用：保留 schema 1–30 历史存档中建筑、敌人和友军快照的原命名空间与 public 字段形状，供旧档解码和验证。不能负责什么：只用于历史格式兼容，不是现役 3D 领域状态，不得新增玩法字段、运行时控制器或 schema 31 payload；变更必须保持 schema 1–30 固定夹具可读。改后跑哪组测试：`FormalSaveTests`、`FormalSaveValidatorTests`。代码名：`BuildingSnapshot`、`EnemySnapshot`、`FriendlyUnitSnapshot`。

### 回溯锚点纯规则（复用前审查）

能解决什么：保留历史回溯锚点读取后观测值增加并封顶的确定性纯规则。在哪里：`Assets/_Game/Scripts/Legacy/RewindAnchorRules.cs`。怎么复用：保留历史回溯锚点读取后观测值增加并封顶的确定性纯规则。不能负责什么：不拥有输入、场景控制器、存档捕获或恢复应用，只允许作为历史规则兼容边界复用。改后跑哪组测试：`FormalSaveTests`。代码名：`RewindAnchorRules`。

### 正式三维存档信封、编码与语义验证（复用前审查）

能解决什么：统一识别 legacy 2D、正式 3D 和未来版本，并对当前 schema `32` 做确定性编码、十波战役及完整会话统计承载与语义验证。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveEnvelope.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveCodec.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveValidator.cs`、`Assets/_Game/Scripts/Persistence/ThreeD/FormalThreeDSaveData.cs`。怎么复用：以统一信封区分 legacy 2D schema 1 与 30、正式 3D schema 31、当前 schema 32 和未来版本，提供确定性编码、payload hash、结构校验、高价值跨引用语义校验、schema 32 完整统计往返及 schema 31 的受控部分统计迁移。不能负责什么：只定义存档身份、DTO 信封、codec、纯验证与已批准迁移，不执行文件 IO、领域捕获、恢复应用、派生状态重建或 UI；legacy 2D 不升级为正式 3D，schema 31 迁移不得伪造完整历史统计。改后跑哪组测试：`FormalSaveEnvelopeTests`、`FormalSaveValidatorTests`、`FormalSaveSchema32ContractTests`、`FormalSaveDestroyedRuinSchema32Tests`、`GrayboxFormalDefenseCampaignSaveAdapterTests`。代码名：`FormalSaveCheckpointMetadata`、`FormalSaveEnvelope`、`FormalSaveDecodeResult`、`FormalSaveCodec`、`FormalSaveValidationResult`、`FormalSaveValidator`、`FormalThreeDWorldSaveData`、`FormalThreeDBuildingsSaveData`、`FormalThreeDStorageSaveData`、`FormalThreeDWarehouseSaveData`、`FormalThreeDBackpackSaveData`、`FormalThreeDCraftingSaveData`、`FormalThreeDCraftingExecutionSaveData`、`FormalThreeDResearchSaveData`、`FormalThreeDProductionSaveData`、`FormalThreeDProductionStateSaveData`、`FormalThreeDDefenseSaveData`、`FormalThreeDDefenseCampaignSaveData`、`FormalThreeDDefenseCampaignStatisticsSaveData`、`FormalThreeDEvacuationSaveData`、`FormalThreeDEvacuationRuntimePayloadSaveData`。

### 正式单槽存档与文件事务（复用前审查）

能解决什么：通过单槽、备份和原子文件事务保护正式 3D 存档，并返回稳定的结构化故障。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveFileTransaction.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveStore.cs`。怎么复用：通过统一单槽 formal-world.json、有效主档与 .bak 回退、同目录临时文件复读验证和原子替换提交正式 3D 存档，并把旧 2D、未来 schema、损坏和磁盘故障映射为稳定结构化结果。不能负责什么：只拥有路径、时间戳、编码后的字节和文件事务；不捕获或应用领域状态，不决定自动检查点，不把 legacy 2D 直接当作 schema 31，也不向 UI 硬编码玩法文案。改后跑哪组测试：`FormalSaveFileTransactionTests`、`GrayboxFormalSaveRuntimeInputTests`。代码名：`FormalSaveFileTransactionResult`、`FormalSaveFileTransaction`、`SystemFormalSaveFileSystem`、`FormalSaveStoreResult`、`FormalSaveStore`。

### 正式最近波前内部重试档（复用前审查）

能解决什么：为失败结算提供独立于玩家单槽存档的最近波前起点快照，并以 schema 32、统一 codec、验证器和原子事务读写。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveWaveRetryStore.cs`。怎么复用：正式运行时在受控波前边界保存内部快照，失败结算只通过结构化结果加载并恢复最近有效波前；损坏、缺失或不兼容时返回稳定失败而不破坏玩家主档。不能负责什么：不是第二个玩家可见存档槽，不接受 schema 31 或 legacy 2D，不决定何时可重试、不生成结算文案，也不绕过领域协调器。改后跑哪组测试：`FormalSaveWaveRetryStoreTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`。代码名：`FormalSaveWaveRetryStoreResult`、`FormalSaveWaveRetryStore`。

### 正式三维自动检查点策略（复用前审查）

能解决什么：把正式玩法事件合并成可持久恢复、失败可保留的有序检查点请求。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveCheckpointPolicy.cs`。怎么复用：按稳定原因与事件键合并自动检查点，维护 sequence、已完成一次性里程碑、失败保留和明确 Flush 边界，并允许恢复检查点基线。不能负责什么：只拥有检查点意图与历史高水位，不自行 capture、写盘、重试每帧或决定玩家文案；保存回调、规则时间和事件订阅必须由正式运行时显式注入。改后跑哪组测试：`GrayboxFormalSaveCheckpointTests`、`GrayboxFormalSaveRuntimeHostTests`。代码名：`FormalSaveCheckpointPolicy`。

### 三维世界与移动城市存档适配器（复用前审查）

能解决什么：在 schema `32` 与既有三维世界、移动城市权威状态之间做确定性捕获和恢复。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxWorldCitySaveAdapter3D.cs`。怎么复用：捕获并恢复 schema 32 的 v2 世界身份、节点余量、城市位置、模式、活动转换和规则时间，并明确拒绝 v1 world identity。不能负责什么：只映射世界与城市权威状态，不拥有文件 IO、领域总协调、建筑/库存/生产真值、路径缓存、物流连接、表现或 UI；不猜测迁移旧地图坐标。改后跑哪组测试：`GrayboxFormalSaveWorldCityTests`。代码名：`GrayboxWorldCitySaveAdapter3D`。

### 正式三维存档领域协调器（复用前审查）

能解决什么：按七领域固定顺序协调完整快照和带回滚的事务式恢复。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs`。怎么复用：按 world/city、building/storage、economy、production、defense、evacuation、pause 固定顺序协调 schema 31 capture 与恢复，先完整验证，再事务式应用，失败时回滚权威领域并在成功提交后统一重建派生状态。不能负责什么：只协调显式注入的领域和派生重建，不搜索场景、不执行文件 IO、不复制领域规则，也不把连接、路径、目标或 UI 入档；回滚失败会保留安全屏障，调用方不得伪装成成功。改后跑哪组测试：`GrayboxFormalSaveCoordinatorTests`、`GrayboxFormalSaveCheckpointTests`、`GrayboxBuildAndPerformanceTests`。代码名：`GrayboxFormalControllerRebuilder3D`、`GrayboxFormalPauseSaveDomain3D`、`GrayboxFormalSaveCoordinatorResult3D`、`GrayboxFormalSaveCoordinator3D`。

### 正式三维存档运行时主机（仅限场景）

能解决什么：在正式三维场景组合唯一存档运行时、事件驱动检查点与最近波前内部重试档。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveRuntimeHost3D.cs`。怎么复用：在 GrayboxPrototype3D 组合统一玩家 store、七领域 coordinator、自动检查点、继续游戏、新游戏、保存退出和波前起点内部快照，并在无待处理检查点时保持 LateUpdate 零写盘。不能负责什么：只作为正式 3D 场景组合根，不用于冻结 2D，也不拥有玩家文案或领域 DTO 规则；内部波前重试档不是第二玩家槽，失败不能伪装成已保存或已恢复。改后跑哪组测试：`GrayboxFormalSaveRuntimeHostTests`、`FormalSaveWaveRetryStoreTests`、`GrayboxFormalSaveRuntimeInputTests`、`GrayboxFormalSaveRoundTripTests`。代码名：`GrayboxFormalSaveRuntimeHost3D`。

### 正式三维存档启动与退出入口（仅限场景）

能解决什么：把正式存档结果转为启动页、覆盖确认、检查点警告、退出反馈、失败波前重试和返回标题命令，并正确阻断世界输入。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxFormalSaveEntryController3D.cs`。怎么复用：把正式 store/coordinator/runtime host 的结构化结果映射为启动页继续、新游戏覆盖确认、自动存档警告、保存退出、最近波前重试和返回标题反馈，并通过既有系统菜单与输入协调器阻断未进入游戏时的世界输入。不能负责什么：只拥有 GrayboxPrototype3D 的玩家入口状态与中文反馈，不读写文件、不持有 schema DTO、不绕过 runtime host，也不接入冻结 2D；继续、覆盖、重试与退出必须经过真实 UGUI 输入主循环验证。改后跑哪组测试：`GrayboxFormalSaveUiAndInputTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`、`GrayboxFormalSaveRuntimeInputTests`、`GrayboxFormalSaveRoundTripTests`。代码名：`GrayboxFormalSaveEntryController3D`。

## 3D 表现与美术

### 共享视觉定义库（复用前审查）

能解决什么：用于按稳定内容 ID 保存和查询可替换的共享视觉定义。在哪里：`Assets/_Game/Scripts/Presentation/VisualLibrary.cs`、`Assets/_Game/Scripts/Presentation/VisualDefinition.cs`。怎么复用：用于按稳定内容 ID 保存和查询可替换的共享视觉定义。不能负责什么：只拥有视觉定义与目录查询，不创建场景槽位、运行时 Provider 或第二套玩法身份；正式 3D 表现应通过自身展示适配器消费该边界。改后跑哪组测试：`VisualSlotTests`。代码名：`VisualLibrary`、`VisualDefinition`。

### 三维共享资源图标目录（推荐复用）

能解决什么：为全部 31 种正式资源提供稳定 Sprite 解析、正式透明图覆盖和确定性占位图标。在哪里：`Assets/_Game/Scripts/Graybox3D/ResourceIconCatalog3D.cs`、`Assets/_Game/Rendering/Graybox3D/ResourceIconCatalog3D.asset`。怎么复用：为全部正式资源提供稳定 Sprite 解析、可替换资产覆盖和确定性占位图标，供矿点、资源栏、仓库、背包、配方、科技与生产 UI 共享。不能负责什么：只负责资源 ID 到图标的表现映射，不拥有资源定义、数量或矿点真值；消费者必须使用同一目录资产或确定性 fallback，不得各自生成第二套资源身份和颜色语义。改后跑哪组测试：`GrayboxVisualAndWorldTests`、`GrayboxSceneContractTests`、`Production2DItemIconPipelineTests`。代码名：`ResourceIconCatalog3D`。

### 三维共享建筑图标目录（推荐复用）

能解决什么：按 30 个正式建筑稳定 ID 提供统一建筑 Sprite。在哪里：`Assets/_Game/Scripts/Graybox3D/BuildingIconCatalog3D.cs`、`Assets/_Game/Resources/Production2D/BuildingIconCatalog3D.asset`。怎么复用：按 BuildingCatalog 稳定建筑 ID 解析 30 张正式建筑 Sprite 和确定性类别回退，供建造栏、建筑详情与撤离列表共享。不能负责什么：只拥有建筑 ID 到 Sprite 的表现映射；不决定建筑可见性、解锁、成本、占地或放置合法性，不把二维图写成三维模型完成。新消费者不得维护平行建筑图标字典。改后跑哪组测试：`Production2DBuildingIconPipelineTests`、`GrayboxBuildingRuntimeSceneTests`。代码名：`BuildingIconOverride3D`、`BuildingIconCatalog3D`。

### 三维共享科技图标目录（推荐复用）

能解决什么：按正式 43 节点研究 ID 提供统一科技 Sprite 和路线/层级回退。在哪里：`Assets/_Game/Scripts/Graybox3D/ResearchIconCatalog3D.cs`、`Assets/_Game/Resources/Production2D/ResearchIconCatalog3D.asset`。怎么复用：按 ResearchCatalog 的 43 个稳定科技 ID 解析正式科技 Sprite，并提供路线/层级确定性回退，供科技树节点与详情共享。不能负责什么：只拥有科技 ID 到 Sprite 的表现映射；不决定研究前置、成本、状态、布局或效果是否已接入。缺图回退不能授予科技效果，UI 不得用文件名建立第二套目录。改后跑哪组测试：`Production2DTechnologyIconPipelineTests`、`ResearchTreeUiContractTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`ResearchIconOverride3D`、`ResearchIconCatalog3D`。

### 统一二维视觉目录（推荐复用）

能解决什么：用一套稳定视觉键统一六类正式二维内容与配方的图标组合。在哪里：`Assets/_Game/Scripts/Graybox3D/Production2DVisualCatalog3D.cs`、`Assets/_Game/Resources/Production2D/Production2DVisualCatalog3D.asset`、`Docs/Engineering/idea-0016-visual-assets.json`。怎么复用：用稳定三元组 visualClass/contentId/variant 统一解析 31 物品、43 科技、30 建筑、7 UI、1 人物和 2 世界标记共 114 个视觉条目，并登记 30 条配方的主产物与机器/手工角标投影。不能负责什么：运行时资产只保存稳定视觉键、Sprite、配方视觉投影和类别回退；机器清单另行保存制作、导入与审核元数据。二者都不拥有资源数量、配方数值、科技状态、建筑规则或 UI 布局；未知键只能回退，不能改写玩法 ID。改后跑哪组测试：`Production2DVisualCatalogAtlasTests`。代码名：`Production2DVisualClass`、`Production2DRecipeBadgeRule`、`Production2DVisualEntry3D`、`Production2DRecipeVisualEntry3D`、`Production2DVisualCatalog3D`。

### 三维资源矿点标识与图标标记（仅限场景）

能解决什么：把 `WorldMapModel` 的真实资源节点投影为带稳定坐标 ID、共享资源图标和 Profile 驱动 Near/Mid/Far 尺度的可回收场景标记。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeIdentity3D.cs` 与 `Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeMarker3D.cs`。怎么复用：在 GrayboxPrototype3D 中以世界坐标生成稳定矿点 ID，并把 WorldMapModel 的真实资源节点投影为复用共享资源图标的可回收标记。同屏标签由世界视图按稳定优先级、稳定 ID 和 `6px` 间距确定性避让。不能负责什么：只属于当前 3D 世界表现与对象复用层；不创建资源节点、不决定节点类型、储量、采矿合法性或枯竭规则，所有真值必须继续来自 WorldMapModel。改后跑哪组测试：`GrayboxVisualAndWorldTests`、`FormalWorldPresentationScaleProfile3DTests`。代码名：`GrayboxResourceNodeIdentity3D`、`GrayboxResourceNodeMarker3D`。

### 正式三维地图导航与矿点层级（复用前审查）

能解决什么：统一正式三维地图的正交缩放边界和滚轮步长，并向世界表现提供当前观察距离。在哪里：`Assets/_Game/Scripts/Graybox3D/FormalMapNavigationProfile3D.cs`、`Assets/_Game/Resources/Presentation/FormalMapNavigationProfile3D.asset`。怎么复用：统一正式三维地图的正交缩放边界和滚轮步长，并向世界表现提供当前观察距离。不能负责什么：只拥有导航配置；矿点的物理像素尺寸和实际 Near/Mid/Far 显隐由正式世界表现尺度配置负责。它不修改地图、节点、储量或输入焦点真值，模态抑制必须继续经过正式输入协调器。改后跑哪组测试：`GrayboxCameraAndInputTests`、`GrayboxVisualAndWorldTests`、`GrayboxRuntimeSceneTests`。代码名：`FormalMapNavigationProfile3D`。

### 正式三维世界表现尺度（复用前审查）

能解决什么：统一 30 座建筑的表现 archetype、Ground/InnerCity 格尺度、footprint 填充率与高度带，以及矿点 Near/Mid/Far 的参考像素、显隐和世界单位换算。在哪里：`Assets/_Game/Scripts/Graybox3D/FormalWorldPresentationScaleProfile3D.cs`、`Assets/_Game/Resources/Presentation/FormalWorldPresentationScaleProfile3D.asset`。怎么复用：按 BuildingDefinition.Id 解析 30 项表现 archetype，统一地面/内城格尺度、精确 .14 DefenseFoundation 和矿点 Near 68、50、22，Mid 56、42、20，Far Icon 28 的世界表现尺度。不能负责什么：只拥有世界表现参数与纯换算；不得定义或修改 BuildingCatalog 占地、资源节点、地图、Sprite 身份、相机输入、正式 UI 图标 token 或存档真值，UI token 继续由 FormalUiLayoutProfile3D 持有。改后跑哪组测试：`FormalWorldPresentationScaleProfile3DTests`、`GrayboxBuildingProjectionAndViewTests`、`GrayboxVisualAndWorldTests`。代码名：`FormalWorldPresentationScaleProfile3D`、`FormalWorldPresentationScalePolicy3D`、`FormalBuildingVisualMetrics3D`、`FormalWorldMarkerMetrics3D`、`FormalBuildingVisualArchetype3D`。

### 正式三维响应式界面布局（复用前审查）

能解决什么：统一 1920×1080 参考分辨率、`ScaleWithScreenSize + Expand`、安全区、语义槽位、间距、图标语义尺寸、大面板尺寸上限和运行时可刷新的 12 物理像素字体下限。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/FormalUiLayoutProfile3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/FormalUiLayoutPolicy3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/FormalUiCanvasConfiguration3D.cs`。怎么复用：统一 1920×1080 参考分辨率、Expand 缩放、安全区、语义槽位、间距、图标语义尺寸、大面板尺寸上限和运行时可刷新的 12 物理像素字体下限。不能负责什么：只负责 UGUI 画布与布局表现；布局以 Canvas 本地矩形为真值，字体下限才读取物理像素缩放；不拥有玩法数据、输入命令或窗口生命周期，既有建造栏继续保持 620×54 的批准尺寸与位置。改后跑哪组测试：`FormalUiLayoutPolicy3DTests`、`FormalUiResponsiveLayout3DTests`、`GrayboxSceneContractTests`、`GrayboxDeveloperModifierRuntimeInputTests`、`GrayboxDefenseRuntimeInputTests`。代码名：`FormalUiLayoutProfile3D`、`FormalUiLayoutPolicy3D`、`FormalUiLayout3D`、`FormalUiCanvasConfiguration3D`、`FormalUiCanvasMetrics3D`、`FormalUiReadableText3D`。

### 三维灰盒视觉槽位（仅限场景）

能解决什么：把灰盒视觉绑定到当前场景。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs`。怎么复用：用于场景内灰盒视觉绑定。只在该三维场景内使用。不能负责什么：不作为二维槽位替代。改后跑哪组测试：`GrayboxVisualAndWorldTests`。代码名：`GrayboxVisualSlot`。

### 首版三维地形配置（复用前审查）

能解决什么：定义首版地形参数。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`。怎么复用：用于定义首版地形参数。修改前需要导入策略复核。不能负责什么：不直接决定资源导入。改后跑哪组测试：`FirstArtTerrainProfileTests`。代码名：`FirstArtTerrainProfile3D`。

### 文明式三维地形视觉样式（复用前审查）

能解决什么：冻结七类地形的制图式综合色、宏观变化强度和首轮原创色板。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainVisualStyle3D.cs`。怎么复用：冻结七类地形的制图式综合色、宏观变化强度和首轮原创建议色板，供资产生成器与 Shader 共同消费。不能负责什么：只拥有地形表现参数，不改变 WorldMapModel、控制图、可通行性、资源节点或 Ruins/Cliff 几何；替换底色仍须运行 TerrainAssetDeep。改后跑哪组测试：`FirstArtTerrainVisualStyleTests`、`FirstArtTerrainShaderTests`。代码名：`FirstArtTerrainVisualStyleCatalog3D`。

### 首版三维地形渲染（仅限场景）

能解决什么：在场景渲染首版地形。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`。怎么复用：用于在场景渲染首版地形。仅在已批准的三维场景接入。不能负责什么：不管理地形资源导入。改后跑哪组测试：`FirstArtTerrainRendererTests`。代码名：`FirstArtTerrainRenderer3D`。

### 废墟与悬崖三维配置（复用前审查）

能解决什么：冻结废墟与悬崖的稳定标识、模块语义和批准资源引用。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffCatalog3D.cs`、`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffProfile3D.cs`。怎么复用：用于冻结废墟与悬崖稳定标识、模块语义和批准资源引用。不能负责什么：不复制地形规则真值；修改目录、材质槽或 Prefab 绑定前需要资产合同复核。改后跑哪组测试：`FirstArtRuinsCliffAssetBuilderTests`、`FirstArtRuinsCliffCatalogProfileTests`。代码名：`FirstArtRuinsCliffCatalog3D`、`FirstArtRuinsCliffProfile3D`。

### 废墟与悬崖三维布局合批（复用前审查）

能解决什么：从 IDEA-0019 v2 世界地图确定性投影并合批 Ruins/Cliff 几何。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffGeometry3D.cs`、`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffLayout3D.cs`。怎么复用：用于从既有世界地图确定性投影并合批废墟与悬崖几何。不能负责什么：只消费既有地图与配置，不创建第二套地形判断或逐格常驻对象。改后跑哪组测试：`FirstArtRuinsCliffGeometryTests`、`FirstArtRuinsCliffLayoutTests`、`FormalWorldPresentationScaleProfile3DTests`。代码名：`FirstArtRuinsCliffGeometry3D`、`FirstArtRuinsCliffLayout3D`。

### 废墟与悬崖三维呈现（仅限场景）

能解决什么：在唯一正式三维地形 presenter 中呈现 Ruins/Cliff，并在类别失败时恢复对应灰盒。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`。怎么复用：用于在正式三维地形 presenter 中呈现废墟与悬崖并保留分类回退。不能负责什么：必须复用唯一地形 presenter；不得增加第二个场景 owner 或绕过分类回退。改后跑哪组测试：`FirstArtRuinsCliffEvidenceCaptureTests`、`FirstArtRuinsCliffPresentationTests`、`FirstArtRuinsCliffSceneContractTests`。代码名：`FirstArtTerrainRenderer3D`。

## 场景、构建与检查工具

### 文明式地形四通道生成器（复用前审查）

能解决什么：从七份不可变概念源确定性重采样、平滑接缝并派生 BaseColor、Height、Normal、Mask，再事务式重建四个 Texture2DArray。在哪里：`Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs`。怎么复用：从七份不可变概念源确定性重采样、平滑接缝并派生 BaseColor、Height、Normal、Mask，再事务式重建四个 Texture2DArray。不能负责什么：仅供 Editor 生成已批准的正式地形表现；不得读取已生成 BaseColor 作为再次生成输入，不改变控制图、地图真值或玩法规则，改后必须运行 TerrainAssetDeep。改后跑哪组测试：`FirstArtTerrainVisualStyleTests`、`FirstArtTerrainAssetBuilderTests`。代码名：`FirstArtTerrainAssetBuilder`。

### 首版三维地形与界面固定证据捕获（复用前审查）

能解决什么：采集地图总览、地形交界、资源标记三档、主 HUD、科技树、动态水与十帧缩放轨迹。在哪里：`Assets/_Game/Editor/FirstArtTerrainEvidenceCapture.cs`。怎么复用：在唯一正式 3D 场景采集地图总览、地形交界、资源标记三档、主 HUD、科技树、动态水与十帧缩放轨迹，并写入固定分辨率、相机、LOD、UI 状态和 SHA-256 清单。不能负责什么：仅用于 Editor 验证；必须恢复相机、Canvas、矿点 LOD 和面板状态，不修改地图或 UI 真值，也不把自动截图写成人工视觉验收。改后跑哪组测试：`FirstArtTerrainEvidenceCaptureTests`。代码名：`FirstArtTerrainEvidenceCapture`。

### 正式二维图类级导入规则（复用前审查）

能解决什么：只在批准的正式二维目录内统一透明 Sprite 的 Unity 导入参数。在哪里：`Assets/_Game/Editor/Production2DItemImportPolicy.cs`、`Assets/_Game/Editor/Production2DBuildingImportPolicy.cs`、`Assets/_Game/Editor/Production2DTechnologyImportPolicy.cs`、`Assets/_Game/Editor/Production2DUiCharacterMarkerImportPolicy.cs`。怎么复用：只在 Assets/_Game/Art/Production2D 的六类批准目录内统一 Sprite、Alpha、sRGB、Clamp、无 mipmap、FullRect、Pivot 和 UI Border 导入合同。不能负责什么：只管理限定二维交付目录的 TextureImporter；不生成内容简介、中文名、运行时映射或视觉验收结论，绝不能扫描或修改地形及其它既有纹理。扩展目录前必须同步 manifest 和导入测试。改后跑哪组测试：`Production2DItemIconPipelineTests`、`Production2DBuildingIconPipelineTests`、`Production2DTechnologyIconPipelineTests`、`Production2DUiCharacterMarkerPipelineTests`。代码名：`Production2DItemImportPolicy`、`Production2DBuildingImportPolicy`、`Production2DTechnologyImportPolicy`、`Production2DUiCharacterMarkerImportPolicy`。

### 正式二维目录与图集生成器（复用前审查）

能解决什么：从已复核 manifest 确定性生成类别目录、统一视觉目录和六类 SpriteAtlas。在哪里：`Assets/_Game/Editor/Production2DItemIconCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DBuildingIconCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DTechnologyIconCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DVisualCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DSpriteAtlasBuilder.cs`。怎么复用：从已复核的类别 manifest 以稳定 ID 顺序生成资源、建筑、科技及统一 114 项视觉目录、30 条配方视觉投影和六类 SpriteAtlas；内容相同的重复构建保持资产与 meta 字节不变。不能负责什么：仅供 Editor 生成和验证已批准的表现资产；不从文件名猜玩法内容，不改场景、配方、研究或建筑规则，不替代 Alpha/缩小联络表与用户视觉验收。Atlas 打包顺序不构成运行时身份。改后跑哪组测试：`Production2DItemIconPipelineTests`、`Production2DBuildingIconPipelineTests`、`Production2DTechnologyIconPipelineTests`、`Production2DUiCharacterMarkerPipelineTests`、`Production2DVisualCatalogAtlasTests`。代码名：`Production2DItemIconCatalogBuilder`、`Production2DBuildingIconCatalogBuilder`、`Production2DTechnologyIconCatalogBuilder`、`Production2DVisualCatalogBuilder`、`Production2DSpriteAtlasBuilder`。

### 废墟与悬崖视觉证据捕获（复用前审查）

能解决什么：为 Ruins/Cliff 运行时接入留下可核对的固定画面和清单。在哪里：`Assets/_Game/Editor/FirstArtRuinsCliffEvidenceCapture.cs`。怎么复用：用于在批准的 GrayboxPrototype3D 场景中以固定 1280×720 视角自动采集 Ruins/Cliff 正常、单件和分类回退证据，并生成带资产 GUID、相机矩阵与 SHA-256 的清单。不能负责什么：仅供编辑器验证；输出必须是项目外的空绝对目录，必须消费既有场景、profile 和唯一地形 presenter，不修改玩法真值，也不替代用户视觉验收；正式说明路径为 Docs/09-Reusable-Project-Catalog-ZH.md。改后跑哪组测试：`FirstArtRuinsCliffEvidenceCaptureTests`。代码名：`FirstArtRuinsCliffEvidenceCapture`。

### 灰盒场景编写（仅限场景）

能解决什么：在编辑器生成灰盒场景。在哪里：`Assets/_Game/Editor/GrayboxSceneAuthoring.cs`。怎么复用：用于编辑器生成灰盒场景。只用于编辑器和指定场景。不能负责什么：不在运行时调用。改后跑哪组测试：`GrayboxSceneContractTests`。代码名：`GrayboxSceneAuthoring`。

### 正式构建工具（复用前审查）

能解决什么：执行 Windows Release 3D、Windows Development 3D 和 universal macOS 三项正式构建，并让正式 3D Player 在 Shader stripping 前识别批准的 URP 管线。在哪里：`Assets/_Game/Editor/FormalBuildTools.cs`。怎么复用：用于三类现役 3D 构建：Windows Release、Windows Development 和 universal x86_64+arm64 macOS；构建期间临时登记批准的 URP 管线，带 -quit 的正式命令行构建还会在编辑器最终退出时恢复受保护文件。不能负责什么：不修改游戏规则，也不提供已退役的 2D 构建入口；构建作用域与命令行最终退出恢复必须还原进入构建前的渲染管线、Quality 序列化状态和四个受保护文件的精确字节：Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset、ProjectSettings/GraphicsSettings.asset、ProjectSettings/QualitySettings.asset、ProjectSettings/ProjectSettings.asset。普通 GUI 构建不得遗留最终退出标记或备份；改后必须运行 GrayboxBuildAndPerformanceTests 中的 universal macOS 与 final-exit 合同，并执行受影响平台的真实构建后哈希检查。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`，并执行受影响平台的真实 Player 构建与退出后哈希检查。代码名：`FormalBuildTools`、`GrayboxRenderPipelineBuildScope`。

### 灰盒性能探针（仅限场景）

能解决什么：采集灰盒性能数据，并为正式撤离混合负载与 schema `31` 存档建立可重复的分配、事务和 Marker 证据。在哪里：`Assets/_Game/Editor/GrayboxPerformanceProbe.cs`。怎么复用：用于采集灰盒性能数据，并执行 IDEA-0014 活跃生产、八敌、防御 HUD、撤离 UI 的 300 稳定帧混合探针、GUI 捕获和正式汇总。IDEA-0015 继续通过该探针采集连续 20 次完整 capture、单次快照/文件事务分配预算、五类存档操作 Marker 与 300 次 idle callback 稳定观察。不能负责什么：只用于可重复验证和正式 Marker 取证，不改变玩法真值、不作为发布版本逻辑，也不替代用户试玩或真实 Windows GPU、显存和内存验收。同步 callback 观察不能冒充真实 300 PlayMode 帧，真实帧、场景重载和写盘稳定性由 `GrayboxFormalSaveRoundTripTests` 证明。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`、`GrayboxFormalEvacuationPerformanceTests`、`GrayboxFormalSaveRoundTripTests`。代码名：`GrayboxPerformanceProbe`。
