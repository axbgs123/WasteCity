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

能解决什么：用于跨系统保存稳定标识。在哪里：`Assets/_Game/Scripts/Content/StableId.cs`。怎么复用：用于跨系统保存稳定标识。不能负责什么：不生成业务实体。改后跑哪组测试：`FoundationTests`。代码名：`StableId`。

## 世界、城市和坐标

### 世界地图模型（推荐复用）

能解决什么：保存格位地形、Traversal、资源余量与揭示等世界运行时真值。在哪里：`Assets/_Game/Scripts/World/WorldMapModel.cs`。怎么复用：保存格位地形、Traversal、资源余量与揭示等世界运行时真值。不能负责什么：不选择 v2 生成策略，也不处理场景渲染、存档文件或建筑放置。改后跑哪组测试：`WorldMapTests`、`GrayboxWorldLayout3DTests`、`GrayboxFormalSaveWorldCityTests`。代码名：`WorldMapModel`。

### 三维世界布局（复用前审查）

能解决什么：作为正式 64×48 世界入口消费 IDEA-0019 的 v2 确定性生成器。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs`。怎么复用：作为正式 64×48 世界入口消费 IDEA-0019 的 v2 确定性生成器。不能负责什么：只选择正式世界生成边界；不复制地图目录、渲染、存档 IO 或放置合法性，调整布局前需要场景复核。改后跑哪组测试：`GrayboxWorldLayout3DTests`、`GrayboxFormalSaveWorldCityTests`。代码名：`GrayboxWorldLayout3D`。

### 正式三维世界生成目录（复用前审查）

能解决什么：以固定 8×6 宏格模板、每宏格整数扰动、固定 Traversal 区域/走廊和 24 项精确坐标资源目录生成 seed 8128 的正式 64×48 v2 世界。在哪里：`Assets/_Game/Scripts/Graybox3D/FormalWorldGenerationCatalog3D.cs`、`Assets/_Game/Scripts/Graybox3D/FormalWorldGenerator3D.cs`。怎么复用：以固定 8×6 宏格模板、每宏格整数扰动、固定 Traversal 区域/走廊和 24 项精确坐标资源目录生成 seed 8128 的正式 64×48 v2 世界。不能负责什么：只拥有确定性初始地图内容与生成配置；当前不实现评分候选、定点插值或随机 spot，也不持有运行时剩余储量、揭示、渲染、存档文件、城市路径或建筑放置真值。改后跑哪组测试：`GrayboxWorldLayout3DTests`、`WorldMapTests`、`GrayboxFormalSaveWorldCityTests`、`GrayboxFormalSaveRuntimeHostTests`。代码名：`FormalWorldGenerationCatalog3D`、`FormalWorldGenerator3D`、`FormalResourceNodeSpec3D`。

### 平面坐标映射（推荐复用）

能解决什么：用于世界与平面坐标转换。在哪里：`Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs`。怎么复用：用于世界与平面坐标转换。不能负责什么：不决定城市规则。改后跑哪组测试：`PlanarCoordinateMapper3DTests`。代码名：`PlanarCoordinateMapper3D`。

### 城市寻路（推荐复用）

能解决什么：用于城市路径搜索。在哪里：`Assets/_Game/Scripts/City/CityPathfinder.cs`。怎么复用：用于城市路径搜索。不能负责什么：不处理部署消耗。改后跑哪组测试：`CityPathfinderTests`。代码名：`CityPathfinder`。

### 城市地形规则（推荐复用）

能解决什么：用于校验城市地形条件。在哪里：`Assets/_Game/Scripts/City/CityTerrainRules.cs`。怎么复用：用于校验城市地形条件。不能负责什么：不负责路径计算。改后跑哪组测试：`CityTerrainRulesTests`。代码名：`CityTerrainRules`。

### 正式城市部署状态（推荐复用）

能解决什么：作为正式部署状态所有者，维护 Mobile、Deploying、Fortress、Packing、转换取消、规则剩余时间和战斗收起倍率。在哪里：`Assets/_Game/Scripts/City/CityDeploymentModel.cs`。怎么复用：作为正式部署状态所有者，维护 Mobile、Deploying、Fortress、Packing、转换取消、规则剩余时间和战斗收起倍率。不能负责什么：只拥有部署状态和规则时间；不判断地形合法性，不处理 Unity 输入或表现，也不进入 schema 30 或接入冻结 2D。改后跑哪组测试：`CityDeploymentRulesTests`、`GrayboxMobileCityController3DTests`。代码名：`CityDeploymentModel`。

### 城市部署规则（推荐复用）

能解决什么：用于判定城市部署合法性。在哪里：`Assets/_Game/Scripts/City/CityDeploymentRules.cs`。怎么复用：用于判定城市部署合法性。不能负责什么：不渲染部署预览。改后跑哪组测试：`CityDeploymentRulesTests`。代码名：`CityDeploymentRules`。

### 直接控制规则（推荐复用）

能解决什么：用于直接控制状态规则。在哪里：`Assets/_Game/Scripts/City/DirectControlRules.cs`。怎么复用：用于直接控制状态规则。不能负责什么：不处理领袖动画。改后跑哪组测试：`DirectControlRulesTests`。代码名：`DirectControlRules`。

## 建造与撤离

### 建筑目录（复用前审查）

能解决什么：投影正式建筑目录的可见性、统一解锁结果与锁定原因，供快捷栏和分类列表读取。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs`。怎么复用：投影正式建筑目录的可见性、统一解锁结果与锁定原因，供快捷栏和分类列表读取。不能负责什么：不拥有建筑定义、不提交建筑放置，也不自行决定人口、研究或前置条件。改后跑哪组测试：`GrayboxBuildingCatalogTests`。代码名：`GrayboxBuildingCatalogPresenter3D`。

### 建筑网格（推荐复用）

能解决什么：提供建筑格位计算，并承载稳定 BuildingDefinition、BuildingCatalog 与首轮人口门槛配置真值。在哪里：`Assets/_Game/Scripts/Building/BuildingGrid.cs`。怎么复用：提供建筑格位计算，并承载稳定 BuildingDefinition、BuildingCatalog 与首轮人口门槛配置真值。不能负责什么：不负责输入路由或失败反馈，也不在视图层复制建筑解锁规则。改后跑哪组测试：`BuildingGridTests`、`BuildingUnlockTests`、`GrayboxBuildingCatalogTests`。代码名：`BuildingGrid`。

### 建筑移动规则（推荐复用）

能解决什么：用于约束建筑移动。在哪里：`Assets/_Game/Scripts/Building/BuildingMobilityRules.cs`。怎么复用：用于约束建筑移动。不能负责什么：不决定建筑解锁。改后跑哪组测试：`BuildingMobilityRulesTests`。代码名：`BuildingMobilityRules`。

### 建筑放置规则（推荐复用）

能解决什么：用于评估建筑放置。在哪里：`Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs`。怎么复用：用于评估建筑放置。不能负责什么：不管理施工进度。改后跑哪组测试：`BuildingPlacementEvaluationTests`。代码名：`BuildingPlacementEvaluation`。

### 资源节点稳定绑定（推荐复用）

能解决什么：在合法放置结果、建筑实例和生产状态之间传递同一资源节点的稳定 ID 与地图坐标。在哪里：`Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs`。怎么复用：在合法放置结果、建筑实例和生产状态之间传递同一资源节点的稳定 ID 与地图坐标。不能负责什么：只承载权威放置评估确认的节点身份和坐标；不判断兼容性、放置合法性、储量或物流范围。改后跑哪组测试：`GrayboxProductionLifecycleTests`、`GrayboxProductionRuntimeTests`。代码名：`ResourceNodeBinding`。

### 建筑资源节点兼容规则（推荐复用）

能解决什么：供放置评估与采矿引导共同判断建筑和资源节点是否兼容。在哪里：`Assets/_Game/Scripts/Building/BuildingResourceNodeCompatibilityRules.cs`。怎么复用：供放置评估与采矿引导共同判断建筑和资源节点是否兼容。不能负责什么：只回答资源类型兼容性；不复制范围、占地、成本、解锁或城市状态判断。改后跑哪组测试：`BuildingResourceNodeCompatibilityRulesTests`。代码名：`BuildingResourceNodeCompatibilityRules`。

### 建筑解锁模型（推荐复用）

能解决什么：用于保存建筑解锁状态。在哪里：`Assets/_Game/Scripts/Building/BuildingUnlockModel.cs`。怎么复用：用于保存建筑解锁状态。不能负责什么：不计算升级成本。改后跑哪组测试：`BuildingUnlockTests`。代码名：`BuildingUnlockModel`。

### 施工进度（推荐复用）

能解决什么：用于跟踪施工进度。在哪里：`Assets/_Game/Scripts/Building/ConstructionProgress.cs`。怎么复用：用于跟踪施工进度。不能负责什么：不控制建筑视图。改后跑哪组测试：`ConstructionProgressTests`。代码名：`ConstructionProgress`。

### 施工退款规则（复用前审查）

能解决什么：用于计算施工退款。在哪里：`Assets/_Game/Scripts/Building/ConstructionRefundRules.cs`。怎么复用：用于计算施工退款。不能负责什么：不写入资源库存。改后跑哪组测试：`ConstructionProgressTests`。代码名：`ConstructionRefundRules`。

### 正式撤离纯规则（推荐复用）

能解决什么：以纯规则创建单体、分类、全部或混合撤离 work，并在确认批次时冻结和平/战斗上下文、生产力、退款比例与基础耗时。在哪里：`Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs`。怎么复用：以纯规则创建单体、分类、全部或混合撤离 work，并在确认批次时冻结和平/战斗上下文、生产力、退款比例与基础耗时。不能负责什么：不读取场景、UI、城市库存或当前敌人；调用方提供权威上下文并负责原子容量预检。遗弃废墟不是前哨，本规则不进入 schema 30 或接入冻结 2D。改后跑哪组测试：`GrayboxEvacuationTests`。代码名：`EvacuationBatchContext`、`BuildingEvacuationWork`、`BuildingEvacuationRules`。

### 三维建筑会话（复用前审查）

能解决什么：协调三维建筑会话并持有当前会话唯一的 CityResourceStorageModel；除正式施工完成事件外，还提供保持稳定实例 ID、位置、方向、站点、完成状态和建筑数量不变的原位升级提交，并在表现或付款失败时回滚规则、表现与材料。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`。怎么复用：协调三维建筑会话并持有当前会话唯一的 CityResourceStorageModel；除正式施工完成事件外，还提供保持稳定实例 ID、位置、方向、站点、完成状态和建筑数量不变的原位升级提交，并在表现或付款失败时回滚规则、表现与材料。不能负责什么：BuildingCompleted 只陈述真实施工完成，原位升级不得重发完成事件；Configure、存档恢复和重复同步也绝不补发，订阅者异常逐个隔离且不能回滚建筑。会话不判断文明/科技升级资格，不替代目录、输入、放置、物流、撤离或仓库规则，也不从场景搜索建筑。改后跑哪组测试：`GrayboxBuildingSessionTests`、`GrayboxBuildingCombatLifecycleTests`、`GrayboxEvacuationTests`、`GrayboxFormalSaveBuildingStorageTests`、`GrayboxProgressionEventIntegrationTests`、`GrayboxWarehouseStorageIntegrationTests`、`GrayboxBuildingUpgradeControllerTests`。代码名：`GrayboxBuildingRestoreEntry3D`、`GrayboxBuildingSession3D`。

### 三维建筑与仓储存档适配器（复用前审查）

能解决什么：从显式提供的当前建筑会话、建筑表现和当前 WorldMapModel 捕获并恢复 schema 31 的建筑实例、双网格、高水位、城市核心账本、真实仓库、过滤与孤立资源。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingStorageSaveAdapter3D.cs`。怎么复用：从显式提供的当前建筑会话、建筑表现和当前 WorldMapModel 捕获并恢复 schema 31 的建筑实例、双网格、高水位、城市核心账本、真实仓库、过滤与孤立资源。不能负责什么：只映射领域真值与 FormalThreeD DTO；不拥有文件路径、codec、文件事务或恢复总协调，不使用场景发现，也不保存物流连接、容量配置、revision、UI 或表现对象。带资源节点绑定的恢复必须显式提供当前正式世界。改后跑哪组测试：`GrayboxFormalSaveBuildingStorageTests`。代码名：`GrayboxBuildingStorageSaveAdapter3D`。

### 三维背包合成与科技存档适配器（复用前审查）

能解决什么：从显式提供的当前 PlayerBackpackModel、CraftingQueueModel 和 FormalResearchRuntime 捕获 schema 31 背包、合成与正式科技 DTO；兼容重载继续服务历史 Demo 测试。恢复时先通过各模型公开且受验证的 snapshot/prepare 边界完成三域预检，再按背包、研究、合成顺序提交，保留队列稳定执行 ID、高水位、预留输入及未知内容降级状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEconomySaveAdapter3D.cs`。怎么复用：从显式提供的当前 PlayerBackpackModel、CraftingQueueModel 和 FormalResearchRuntime 捕获 schema 31 背包、合成与正式科技 DTO；兼容重载继续服务历史 Demo 测试。恢复时先通过各模型公开且受验证的 snapshot/prepare 边界完成三域预检，再按背包、研究、合成顺序提交，保留队列稳定执行 ID、高水位、预留输入及未知内容降级状态。不能负责什么：只负责领域真值与 FormalThreeD DTO 映射，不拥有文件路径、codec、文件事务、检查点或恢复总协调，不搜索场景，也不重新扣除合成预留或研究成本。背包超栈兼容策略由上层根据内容配置显式传入；研究站资格、城市倍率、UI、派生阻塞原因和表现对象不入档。改后跑哪组测试：`GrayboxFormalSaveEconomyTests`、`PlayerBackpackModelTests`、`CraftingQueueModelTests`、`DemoResearchRuntimeTests`、`GrayboxFormalResearchSaveAdapterTests`。代码名：`GrayboxEconomySaveAdapter3D`。

### 三维逐建筑生产存档适配器（复用前审查）

能解决什么：在 GrayboxProductionRuntime3D 的持久化快照边界与 schema 31 FormalThreeDProduction DTO 之间执行确定性映射，并把恢复委托给运行时的预检与单次提交。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionSaveAdapter3D.cs`。怎么复用：在 GrayboxProductionRuntime3D 的持久化快照边界与 schema 31 FormalThreeDProduction DTO 之间执行确定性映射，并把恢复委托给运行时的预检与单次提交。不能负责什么：只负责生产领域快照与 DTO 映射；不拥有文件 IO、场景搜索、生产 tick、物流、建筑放置或矿点兼容规则，也不复制这些系统的判断。改后跑哪组测试：`GrayboxFormalSaveProductionTests`。代码名：`GrayboxProductionSaveAdapter3D`。

### 正式三维文明进程存档适配器（复用前审查）

能解决什么：在正式 Attention、Fate、三条命轨效果、Civilization 与 AdvancementSequence owner 和 schema 33 progression/effect DTO 之间确定性捕获、预检并原子恢复 Lv.1/Lv.2 当前真值。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalProgressionSaveAdapter3D.cs`。怎么复用：在正式 Attention、Fate、三条命轨效果、Civilization 与 AdvancementSequence owner 和 schema 33 progression/effect DTO 之间确定性捕获、预检并原子恢复 Lv.1/Lv.2 当前真值。不能负责什么：只映射已经验证的 Progression、文明升阶、演出阶段与命轨效果快照，并准备绑定 owner 身份的零写入恢复计划；不计算升阶资格、不提交关注度或奖励、不推进演出、不读写文件或操作 UI。改后跑哪组测试：`GrayboxFormalProgressionSaveAdapterTests`、`GrayboxFormalFateEffectsSaveAdapterTests`、`FormalSaveSchema33ContractTests`、`FormalSaveSchema33MigrationTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxCivilizationAdvancementRuntimeInputTests`。代码名：`GrayboxFormalProgressionRestorePlan3D`、`GrayboxFormalProgressionSaveAdapter3D`。

### 三维首版防御存档适配器（复用前审查）

能解决什么：在正式三维防御运行时、十波战役持久状态与当前 FormalThreeD 防御 DTO 之间执行确定性映射，保留配置签名、出生锚点、波次阶段、完整会话统计、塔攻击余量、活动敌人、建筑生命和核心状态，并通过零写入计划把 schema 31 的可确认统计迁移为显式部分统计后统一提交。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSaveAdapter3D.cs`。怎么复用：在正式三维防御运行时、十波战役持久状态与当前 FormalThreeD 防御 DTO 之间执行确定性映射，保留配置签名、出生锚点、波次阶段、完整会话统计、塔攻击余量、活动敌人、建筑生命和核心状态，并通过零写入计划把 schema 31 的可确认统计迁移为显式部分统计后统一提交。不能负责什么：只负责防御领域快照、迁移兼容与 FormalThreeD DTO 映射；不拥有文件 IO、场景搜索、规则 tick、城市库存、UI 或表现对象。FormalSaveValidator 只验证结构与高价值跨引用；目标缓存、状态文案、攻击事件和表现池不入档，迁移不得伪造完整历史统计。改后跑哪组测试：`GrayboxFormalSaveDefenseTests`、`GrayboxFormalDefenseCampaignSaveAdapterTests`、`FormalSaveSchema32ContractTests`、`SessionStatisticsTests`。代码名：`GrayboxDefenseSaveAdapter3D`。

### 正式防御战役迁移与恢复边界（复用前审查）

能解决什么：schema 32 冻结并完整往返十波战役、塔战斗余量、活动敌人、建筑生命、完整会话统计、出生锚点与规则时钟状态；schema 31 只迁移可确认历史值并显式标为部分统计，所有恢复先生成零写入恢复计划，预检后单次提交。在哪里：`Assets/_Game/Scripts/Defense/SingleCityDefenseCampaignPersistenceState.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalDefenseCampaignPersistence3D.cs`。怎么复用：schema 32 冻结并完整往返十波战役、塔战斗余量、活动敌人、建筑生命、完整会话统计、出生锚点与规则时钟状态；schema 31 只迁移可确认历史值并显式标为部分统计，所有恢复先生成零写入恢复计划，预检后单次提交。不能负责什么：只拥有战役领域持久状态、schema 31 到当前战役结构的兼容迁移和恢复预检；不执行文件 IO，不搜索场景，不保存 HUD、目标缓存、轨迹池或派生连接。迁移缺失的历史信息必须采用受测试的保守默认值并标为部分统计，不得伪造成完整统计。改后跑哪组测试：`SingleCityDefenseCampaignPersistenceTests`、`SingleCityDefenseCampaignCheckpointTests`、`SessionStatisticsTests`、`FormalSaveSchema32ContractTests`、`GrayboxFormalDefenseCampaignSaveAdapterTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`。代码名：`SingleCityDefenseCampaignPersistenceState`、`SingleCityDefenseCampaignRestorePlan`、`GrayboxFormalDefenseCampaignPersistenceState3D`、`GrayboxFormalDefenseCampaignRestorePlan3D`。

### 三维冻结撤离存档适配器（复用前审查）

能解决什么：把正式三维撤离控制器的冻结批次确定性映射到 schema 31，保留 work、建筑锁、生产与防御载荷、稳定队列、批次高水位、剩余时间和容量阻塞身份，并通过绑定 controller generation 与 session revision 的计划执行两阶段恢复。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationSaveAdapter3D.cs`。怎么复用：把正式三维撤离控制器的冻结批次确定性映射到 schema 31，保留 work、建筑锁、生产与防御载荷、稳定队列、批次高水位、剩余时间和容量阻塞身份，并通过绑定 controller generation 与 session revision 的计划执行两阶段恢复。不能负责什么：只负责撤离持久状态、FormalThreeD DTO 映射和恢复边界；不重算 work、退款、容量、运行时载荷、生产、防御、物流或建筑资格，不拥有文件 IO、恢复总协调、场景搜索、UI 或表现，也不接入已经退役的 legacy 2D runtime。未知定义占位可保留聚合资源载荷，已知普通建筑拒绝无所有者载荷。当前为已实现待验证；Task 13 完整自动化与构建已通过，Task 14 退役后的完整自动化、质量门、三项 3D 构建和正式验证记录也已通过，人工试玩和真实 Windows 验收仍未完成。改后跑哪组测试：`GrayboxFormalSaveEvacuationTests`、`GrayboxEvacuationTests`。代码名：`GrayboxEvacuationPayloadPersistenceState3D`、`GrayboxEvacuationPersistenceState3D`、`GrayboxEvacuationRestorePlan3D`、`GrayboxEvacuationSaveAdapter3D`。

### 三维正式撤离协调与只读视图（仅限场景）

能解决什么：在正式 3D 场景编排冻结撤离批次、稳定队列、内部载荷捕获、城市原子提交、运行时完成或遗弃，并发布不可变清单和队列 view。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`。怎么复用：在正式 3D 场景编排冻结撤离批次、稳定队列、内部载荷捕获、城市原子提交、运行时完成或遗弃，并发布不可变清单和队列 view。不能负责什么：只消费生产与防御运行时拥有的内部载荷，不拥有或重算载荷、退款、容量、战斗或物流真值；失败保留原 work 与锁供重试，不进入 schema 30、不接入冻结 2D，遗弃废墟不是前哨。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxFormalSaveEvacuationTests`、`GrayboxBuildingUiAndInputTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`EvacuationManifestItemViewModel`、`EvacuationManifestViewModel`、`EvacuationQueueViewModel`、`GrayboxEvacuationController3D`。

### 三维建筑共享运行与物流资格（推荐复用）

能解决什么：用于从同一建筑生命周期事实分别判断会话状态保留、本地运行和物流连接。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingOperationalAccess3D.cs`。怎么复用：用于从同一建筑生命周期事实分别判断会话状态保留、本地运行和物流连接。不能负责什么：只组合已完成、玩家所有、撤离锁定、建筑站点、城市模式与既有范围规则；不持有库存、生产或防御状态，不复制放置合法性。改后跑哪组测试：`GrayboxProductionRuntimeTests`、`GrayboxFirstDefenseRuntimeTests`。代码名：`GrayboxBuildingOperationalAccess3D`。

### 三维建筑世界视图（仅限场景）

能解决什么：在场景内显示建筑、半透明预览和前向标记，消费正式尺度 Profile，并将 BuildingIconCatalog 的 35 张透明 world Sprite 按占地比例投影到已完成建筑；Sprite 保持竖直、朝向相机并位于屋顶净空之上，施工中和废墟隐藏。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`。怎么复用：在场景内显示建筑、半透明预览和前向标记，消费正式尺度 Profile，并将 BuildingIconCatalog 的 35 张透明 world Sprite 按占地比例投影到已完成建筑；Sprite 保持竖直、朝向相机并位于屋顶净空之上，施工中和废墟隐藏。不能负责什么：只负责 GrayboxPrototype3D 的建筑表现；Sprite 是可替换的 billboard，不代表 3D 建模完成，也不得修改 BuildingCatalog 占地或自行决定锚点、旋转合法性、成本和节点兼容性。改后跑哪组测试：`IDEA0024CityBuildingPresentationTests`、`GrayboxBuildingProjectionAndViewTests`、`FormalWorldPresentationScaleProfile3DTests`、`GrayboxBuildingRuntimeSceneTests`。代码名：`GrayboxBuildingWorldView3D`。

## UI 与输入

### 三维建筑输入路由（复用前审查）

能解决什么：通过正式 Input System 路由建造与撤离输入，并在清单、处理和容量阻塞状态执行 F、Escape、E 与世界输入的模态优先级。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`。怎么复用：通过正式 Input System 路由建造与撤离输入，并在清单、处理和容量阻塞状态执行 F、Escape、E 与世界输入的模态优先级。不能负责什么：真实输入边界只发布界面命令；不决定建筑放置、退款、容量或战斗规则，不直接调用领域提交，也不接入冻结 2D。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`、`GrayboxBuildingRuntimeSceneTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`GrayboxBuildingInputRouter3D`。

### 三维建筑菜单视图（复用前审查）

能解决什么：显示建筑目录、旋转预览，并把所有可见建筑的锁定原因与蓝图放置失败原因固定显示在未改变的建造栏上方；同时显示正式撤离清单、稳定处理队列、内部物资后果和容量阻塞操作。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`。怎么复用：显示建筑目录、旋转预览，并把所有可见建筑的锁定原因与蓝图放置失败原因固定显示在未改变的建造栏上方；同时显示正式撤离清单、稳定处理队列、内部物资后果和容量阻塞操作。不能负责什么：不保存建筑或库存数据，也不计算方向、退款、容量或战斗状态；全部读取控制器提供的不可变 view，图标统一复用 ResourceIconCatalog3D。改后跑哪组测试：`GrayboxBuildingUiAndInputTests`、`GrayboxBuildingProjectionAndViewTests`、`GrayboxBuildingRuntimeSceneTests`、`GrayboxFormalEvacuationVerticalSliceTests`。代码名：`GrayboxBuildingMenuView3D`。

### 三维生产可观察化控制器（仅限场景）

能解决什么：在 GrayboxPrototype3D 内组合背包、33 条配方、44 节点正式研究、派生资源发现、资源状态栏、真实仓库详情、多配方生产与城市库存灵丹使用命令，并把真实输入提交到正式模型。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs`。怎么复用：在 GrayboxPrototype3D 内组合背包、33 条配方、44 节点正式研究、派生资源发现、资源状态栏、真实仓库详情、多配方生产与城市库存灵丹使用命令，并把真实输入提交到正式模型。不能负责什么：只属于当前 3D 场景的会话与 UI 适配；城市与仓库数量必须读取 CityResourceStorageModel，不替代资源、生产、研究、访问资格或输入路由真值，不进入 schema 33 新字段，也不得接入已退役 2D。改后跑哪组测试：`ManualResourceAccessRulesTests`、`GrayboxWarehouseStorageIntegrationTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`GrayboxOperationsController3D`。

### 三维生产可观察化视图（仅限场景）

能解决什么：显示当前 3D 场景的资源栏、账本、背包、33 条配方、仓库和生产通道，并以 reference-fidelity v3 的 1920×1080 蚀刻工业全屏背景、单排顶部工具栏、四路线细框节点、真实科技与材料图标、默认全树概览和底部六段详情舱呈现正式 44 节点科技树；节点和详情额外显示解锁/被动标签、前后数值、范围、叠加与接线状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchTreeView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchTreeViewportInput3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchSearchFocus3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/ResearchTreeConnectionGraphic3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/ResearchEffectPresentationCatalog3D.cs`。怎么复用：让正式科技目录、统一效果 DTO 和共享图标目录共同驱动树与底部详情，不在 View 复制数值或研究状态。不能负责什么：只负责 GrayboxPrototype3D 的 UGUI 结构、表现态与命令事件；不持有库存、队列、研究或解锁真值，不从中文文案推断状态，也不自行扣资源、推进时间或增加 schema 字段；未接线效果必须显示为仅预览。改后跑哪组测试：`IDEA0024ResearchTreePresentationTests`、`GrayboxVisualAndWorldTests`、`ResearchTreeProjection3DTests`、`ResearchTreeUiContractTests`、`ResearchEffectPresentationTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`ResearchNodePresentationState3D`、`ResearchNodePresentation3D`、`ResearchEffectLinePresentation3D`、`ResearchEffectPresentationCatalog3D`、`GrayboxOperationsView3D`、`GrayboxResearchTreeView3D`、`GrayboxResearchTreeViewportInput3D`、`GrayboxResearchSearchFocus3D`、`ResearchTreeConnectionGraphic3D`。

### 三维开发修改器中文目录查询（复用前审查）

能解决什么：从正式资源、研究与文明进程动作目录建立稳定顺序的中文查询快照，支持列表浏览、中文名和 stable ID 的规范化子串搜索，供 Editor/Development 修改器的列表与输入查找共用。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperCatalogQuery3D.cs`。怎么复用：从正式资源、研究与文明进程动作目录建立稳定顺序的中文查询快照，支持列表浏览、中文名和 stable ID 的规范化子串搜索，供 Editor/Development 修改器的列表与输入查找共用。不能负责什么：只负责只读目录投影和查询，不增加资源、不解锁科技、不执行 Attention/Fate/Pressure/Boss/Ascension，也不持有搜索框或选择状态；命令必须经过 Modifier/Facade 公共入口，Release 不得引用修改器 UI 或行为。改后跑哪组测试：`GrayboxDeveloperModifierCatalogTests`、`GrayboxDeveloperModifierTests`、`GrayboxDeveloperModifierRuntimeInputTests`。代码名：`GrayboxDeveloperCatalogQuery3D`、`GrayboxDeveloperCatalogEntry3D`。

### 三维首版防御场景接线与表现（仅限场景）

能解决什么：用于在 GrayboxPrototype3D 中连接统一规则时钟、十波战役、终局强制暂停、结算发布、统一选择 HUD、真实输入以及固定容量的三敌三塔和已结算攻击表现池。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHud3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs`。怎么复用：用于在 GrayboxPrototype3D 中连接统一规则时钟、十波战役、终局强制暂停、结算发布、统一选择 HUD、真实输入以及固定容量的三敌三塔和已结算攻击表现池。不能负责什么：仅限当前正式 3D 场景适配；结算模型与会话统计是规则真值，HUD 和表现对象不持有目标、伤害、耗材、建筑生命、波次或结算真值，攻击轨迹只消费已结算事件且不得重复播放，不接入冻结 2D。改后跑哪组测试：`GrayboxDefenseControllerTests`、`GrayboxDefenseObservabilityTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseSelectionProjectionTests`、`GrayboxDefenseSettledAttackPresentationTests`、`GrayboxDefenseRuntimeInputTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`。代码名：`GrayboxDefenseController3D`、`GrayboxDefenseHud3D`、`GrayboxDefenseHudView3D`、`GrayboxDefenseWorldView3D`。

### 三维防御终局结算模态（仅限场景）

能解决什么：由防御控制器发布一次终局快照，控制器把继续沙盒、最近波前重试、返回标题与关闭失败反馈映射为正式命令，视图只渲染快照和命令状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSettlementController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSettlementView3D.cs`。怎么复用：由防御控制器发布一次终局快照，控制器把继续沙盒、最近波前重试、返回标题与关闭失败反馈映射为正式命令，视图只渲染快照和命令状态。不能负责什么：只拥有 GrayboxPrototype3D 的模态展示与命令路由；不判断胜负、不计算统计、不读写存档、不直接恢复战役，也不接入冻结 2D。当前自动化通过，仍待人工试玩验证文案、焦点与真实操作体验。改后跑哪组测试：`GrayboxDefenseSettlementUi3DTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`。代码名：`GrayboxDefenseSettlementController3D`、`GrayboxDefenseSettlementView3D`。

### 三维防御统一选择详情与 HUD（仅限场景）

能解决什么：从建筑会话、生命、生产和防御不可变快照生成统一 Tower、Enemy、Building、Ruin 详情，并由 HUD 精确显示战役波次、速度、生命、战斗参数、生产配方、内部库存和稳定停工原因。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSelectionProjection3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs`。怎么复用：从建筑会话、生命、生产和防御不可变快照生成统一 Tower、Enemy、Building、Ruin 详情，并由 HUD 精确显示战役波次、速度、生命、战斗参数、生产配方、内部库存和稳定停工原因。不能负责什么：只读投影和显示当前正式快照；不搜索场景、不修改领域状态、不复制放置、物流或生产规则，不伪造废墟库存数量，也不把距核心误写成距当前目标。塔暂停命令只在统一详情明确允许时发布。改后跑哪组测试：`GrayboxDefenseSelectionProjectionTests`、`GrayboxDefenseObservabilityTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseRuntimeInputTests`。代码名：`GrayboxDefenseSelectionSnapshot3D`、`GrayboxDefenseSelectionProjection3D`、`GrayboxDefenseHudView3D`。

### 三维已结算攻击事件与表现池（仅限场景）

能解决什么：把规则层已经结算的稳定攻击事件投影到固定容量的敌人、塔和轨迹表现池，并按事件序号只消费一次机枪 tracer、激光束或孢子抛物线。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs`。怎么复用：把规则层已经结算的稳定攻击事件投影到固定容量的敌人、塔和轨迹表现池，并按事件序号只消费一次机枪 tracer、激光束或孢子抛物线。不能负责什么：事件只携带已结算攻击身份与伤害结果，表现池不得反向决定命中、伤害、目标或耗材；静止快照不得重复播放，池容量与回收仅属表现优化，不能丢失领域真值。改后跑哪组测试：`GrayboxDefenseSettledAttackPresentationTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseSnapshotStabilityTests`。代码名：`GrayboxDefenseSettledAttackEvent3D`、`GrayboxDefenseWorldView3D`。

### 三维灰盒显示设置边界（复用前审查）

能解决什么：在三维灰盒中以 IGrayboxDisplaySettingsStore 和 IGrayboxDisplaySettingsPlatform 分离可测试的显示设置模型、偏好存储和 Unity 平台应用边界。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsModel3D.cs`、`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsAdapters3D.cs`。怎么复用：在三维灰盒中以 IGrayboxDisplaySettingsStore 和 IGrayboxDisplaySettingsPlatform 分离可测试的显示设置模型、偏好存储和 Unity 平台应用边界。不能负责什么：PlayerPrefs 只保存独立显示偏好，明确位于正式存档 schema 30 之外；跨项目复用前需复核键名、版本和平台能力。改后跑哪组测试：`GrayboxUsabilityTests`。代码名：`GrayboxDisplaySettingsModel3D`、`PlayerPrefsGrayboxDisplaySettingsStore3D`、`UnityGrayboxDisplaySettingsPlatform3D`。

### 三维灰盒系统菜单控制器（仅限场景）

能解决什么：协调灰盒场景内菜单页、系统暂停、设置和安全退出。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuController3D.cs`。怎么复用：协调灰盒场景内菜单页、系统暂停、设置和安全退出。不能负责什么：依赖当前灰盒 GameSpeedModel 与场景接线，不是通用前端菜单框架。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxSystemMenuController3D`。

### 三维灰盒系统菜单场景视图（仅限场景）

能解决什么：显示当前三维灰盒场景的模态系统菜单，并只在 Editor/Development 启动页提供验收管理台，以正式继续或新游戏入口进入世界后打开开发修改器。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxFormalSaveEntryController3D.cs`。怎么复用：显示当前三维灰盒场景的模态系统菜单，并只在 Editor/Development 启动页提供验收管理台，以正式继续或新游戏入口进入世界后打开开发修改器。不能负责什么：层级、文案和 UGUI 引用属于 GrayboxPrototype3D；验收入口不得进入 Release，不得跳过正式存档验证、提前打开修改器或成为第二套启动流程。改后跑哪组测试：`IDEA0024AcceptanceAndClickableTabsTests`、`GrayboxUsabilityTests`、`IDEA0024AcceptanceAndTabsRuntimeInputTests`、`GrayboxUsabilityRuntimeSceneTests`。代码名：`GrayboxSystemMenuView3D`、`GrayboxFormalSaveEntryController3D`。

### Development 启动验收管理台（仅限场景）

能解决什么：只在 Editor/Development 启动页提供验收管理台，以正式继续或新游戏入口进入世界，并在成功 `EnterGameplay` 后打开开发修改器。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxFormalSaveEntryController3D.cs`。怎么复用：验收入口必须继续委托正式存档、覆盖确认和进入游戏流程。不能负责什么：不得出现在 Release，不得跳过正式存档验证、提前打开修改器或成为第二套启动流程。改后跑哪组测试：`IDEA0024AcceptanceAndClickableTabsTests`、`IDEA0024AcceptanceAndTabsRuntimeInputTests`、`GrayboxUsabilityTests`。代码名：`GrayboxSystemMenuView3D`、`GrayboxFormalSaveEntryController3D`。

### 三维灰盒易用性输入协调器（仅限场景）

能解决什么：按正式优先级协调启动页、系统菜单、命轨、文明升阶、关注度详情、结算、文本焦点、建造、背包、研究和世界输入；世界空闲时真实 U 走 Host 升阶命令，Results 阶段 U 走继续命令。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`。怎么复用：按正式优先级协调启动页、系统菜单、命轨、文明升阶、关注度详情、结算、文本焦点、建造、背包、研究和世界输入；世界空闲时真实 U 走 Host 升阶命令，Results 阶段 U 走继续命令。不能负责什么：只协调当前场景输入消费者并通过显式回调发布命令，不判断四项升阶条件、不修改 Civilization/Sequence/Attention、不复制建筑或存档规则，也不作为全项目通用输入总线。改后跑哪组测试：`GrayboxUsabilityTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`、`GrayboxCivilizationAdvancementInputCoordinatorTests`、`GrayboxUsabilityRuntimeSceneTests`、`GrayboxCivilizationAdvancementRuntimeInputTests`。代码名：`GrayboxUsabilityInputCoordinator3D`。

## 资源、研究、人口、战斗和存档

### 正式资源定义目录（推荐复用）

能解决什么：提供 31 项正式资源的稳定标识、中文名称、路线层级、来源用途、发现规则、图标 ID、背景简介、显示尺寸与基础资源栏顺序。在哪里：`Assets/_Game/Scripts/Economy/ResourceDefinitionCatalog.cs`。怎么复用：提供 31 项正式资源的稳定标识、中文名称、路线层级、来源用途、发现规则、图标 ID、背景简介、显示尺寸与基础资源栏顺序。不能负责什么：只定义资源身份与不可变静态配置；不保存库存数量，不执行转移、生产、发现状态或界面输入。改后跑哪组测试：`ResourceDefinitionCatalogTests`。代码名：`ResourceDefinition`、`ResourceDefinitionCatalog`。

### 正式资源发现投影（推荐复用）

能解决什么：把城市核心、全部真实仓库、背包、生产输入/输出/预留、合成预留和已完成研究聚合为只读事实，再按 ResourceDefinitionCatalog 的正式发现规则投影当前可见资源。在哪里：`Assets/_Game/Scripts/Economy/ResourceDiscoveryProjection.cs`。怎么复用：把城市核心、全部真实仓库、背包、生产输入/输出/预留、合成预留和已完成研究聚合为只读事实，再按 ResourceDefinitionCatalog 的正式发现规则投影当前可见资源。不能负责什么：发现状态完全从 schema 31 已有权威事实派生，不保存永久发现位；断开物流的仓库仍计入玩家所有资产，但不改变联网可用数量。投影不转移资源、不推进研究，也不持有 UI 筛选状态。改后跑哪组测试：`ResourceDiscoveryProjectionTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`ResourceDiscoveryFacts`、`ResourceDiscoveryProjection`。

### 资源库存（推荐复用）

能解决什么：按稳定资源 ID 管理资源数量，提供覆盖全部已记录资源的确定性正数量快照与受验证的原子全量替换；恢复时可由调用方显式选择资产保守地保留超容量数量。在哪里：`Assets/_Game/Scripts/Economy/ResourceInventory.cs`。怎么复用：按稳定资源 ID 管理资源数量，提供覆盖全部已记录资源的确定性正数量快照与受验证的原子全量替换；恢复时可由调用方显式选择资产保守地保留超容量数量。不能负责什么：只负责资源账本的数量、容量和变更通知；不驱动生产周期，不决定配方、物流、建筑资格、存档兼容策略或界面行为。改后跑哪组测试：`FoundationTests`、`GrayboxFormalSaveProductionTests`、`ResourceInventoryChangeTests`。代码名：`ResourceChangeAttribution`、`ResourceInventory`。

### 旧每资源城市容量策略（复用前审查）

能解决什么：保留 IDEA-0011 的基础容量加每座仓库、且每种资源分别扩容的旧兼容算法，供旧接口和冻结回归使用；IDEA-0012 的正式 3D 仓库不再使用此模型。在哪里：`Assets/_Game/Scripts/Economy/ResourceCapacityPolicy.cs`。怎么复用：保留 IDEA-0011 的基础容量加每座仓库、且每种资源分别扩容的旧兼容算法，供旧接口和冻结回归使用；IDEA-0012 的正式 3D 仓库不再使用此模型。不能负责什么：IDEA-0012 已用每仓库 150 共享总容量替代旧的每资源加仓库模型；正式 3D 必须通过 CityResourceStorageModel 读写城市与仓库库存，不得向 ResourceCapacityPolicy 传仓库数模拟真实仓库。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceCapacityPolicy`。

### 城市与真实仓库库存模型（推荐复用）

能解决什么：作为正式 3D 城市库存唯一聚合入口，按稳定仓库 ID 处理联网数量、确定性存取和不可变快照；撤离及 schema 31 恢复都使用绑定 owner/revision 的预检计划和单次原子提交。恢复可深拷贝保留未知孤立资源，并在配置签名变化时保留超额资产为只出不进。在哪里：`Assets/_Game/Scripts/Economy/CityResourceStorageModel.cs`。怎么复用：作为正式 3D 城市库存唯一聚合入口，按稳定仓库 ID 处理联网数量、确定性存取和不可变快照；撤离及 schema 31 恢复都使用绑定 owner/revision 的预检计划和单次原子提交。恢复可深拷贝保留未知孤立资源，并在配置签名变化时保留超额资产为只出不进。不能负责什么：不决定建筑处理、完成状态、玩家所有权、物流距离或交互资格；战损只提供绑定 revision 的原子损失预检与提交，不决定摧毁目标或损失比例。调用方必须验证建筑交叉引用并在恢复后重算连接；本模型不替代 WorldMapModel、ResourceInventory 或 WarehouseStorageState。改后跑哪组测试：`CityResourceStorageModelTests`、`CityResourceStorageCombatLossTests`、`GrayboxEvacuationTests`、`GrayboxFormalSaveBuildingStorageTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`CityResourceStorageRestorePlan`、`CityResourceEvacuationPlan`、`CityResourceChangeAttributionScope`、`CityResourceStorageModel`、`CityResourceStorageSnapshot`、`CityStorageOrphanResource`、`CityWarehouseRestoreEntry`。

### 单仓库共享容量状态（推荐复用）

能解决什么：按稳定建筑实例 ID 保存一座仓库的 150 共享总容量、真实内容、联网状态与可选单资源过滤，并发布不可变快照；schema 31 恢复可保留未知过滤和占用共享空间的孤立资源，配置变化造成的超额状态保持只出不进。在哪里：`Assets/_Game/Scripts/Economy/WarehouseStorageState.cs`。怎么复用：按稳定建筑实例 ID 保存一座仓库的 150 共享总容量、真实内容、联网状态与可选单资源过滤，并发布不可变快照；schema 31 恢复可保留未知过滤和占用共享空间的孤立资源，配置变化造成的超额状态保持只出不进。不能负责什么：只拥有单仓库会话状态，不聚合城市库存、不计算物流范围、不执行建筑生命周期；恢复仓库先保持断开，连接由正式运行时重算。孤立资源占用容量但不可作为已知资源消费；正式调用必须由 CityResourceStorageModel 统一编排。改后跑哪组测试：`CityResourceStorageModelTests`、`GrayboxFormalSaveBuildingStorageTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`WarehouseStorageState`、`WarehouseStorageSnapshot`。

### 资源缺口规则（推荐复用）

能解决什么：按正式成本顺序计算每种材料的拥有、需要与缺少数量，供放置失败和其他资源不足反馈统一投影。在哪里：`Assets/_Game/Scripts/Economy/ResourceShortfallRules.cs`。怎么复用：按正式成本顺序计算每种材料的拥有、需要与缺少数量，供放置失败和其他资源不足反馈统一投影。不能负责什么：只计算纯缺口数据，不读取 Unity 场景、不执行扣款、不决定放置合法性，也不生成最终 UI 文案；调用方必须传入当前权威库存读取函数。改后跑哪组测试：`ResourceShortfallRulesTests`。代码名：`ResourceShortfall`、`ResourceShortfallRules`。

### 原子资源事务（推荐复用）

能解决什么：聚合同资源请求，预检输入与输出，并执行批量提交和允许部分接收的原子转移。在哪里：`Assets/_Game/Scripts/Economy/ResourceTransaction.cs`。怎么复用：聚合同资源请求，预检输入与输出，并执行批量提交和允许部分接收的原子转移。不能负责什么：只处理资源数量与容量提交；不决定物流连接、交互距离、建筑资格、配方周期或界面状态。改后跑哪组测试：`ResourceTransactionAndCapacityTests`。代码名：`ResourceAmount`、`ResourceTransferResult`、`ResourceTransaction`。

### 玩家背包模型（推荐复用）

能解决什么：管理三十格会话背包及稳定堆叠、拆分、逐个移动、整栈合并与交换；schema 31 通过深拷贝的固定索引 snapshot、零写入 prepare 和 owner-bound 单次 commit 恢复，按各资源正式栈上限验证并可显式保留配置变化后的超栈。在哪里：`Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs`。怎么复用：管理三十格会话背包及稳定堆叠、拆分、逐个移动、整栈合并与交换；schema 31 通过深拷贝的固定索引 snapshot、零写入 prepare 和 owner-bound 单次 commit 恢复，按各资源正式栈上限验证并可显式保留配置变化后的超栈。不能负责什么：只拥有背包槽位状态；未知稳定资源保留在原槽且不可正常存取或移动。模型不访问城市或建筑库存，不判定交互资格，不处理 Unity 输入、UI、文件 IO 或内容配置兼容决策；调用方必须显式决定是否允许超栈恢复。改后跑哪组测试：`PlayerBackpackModelTests`、`GrayboxFormalSaveEconomyTests`。代码名：`BackpackSlot`、`PlayerBackpackRestoreSlot`、`PlayerBackpackRestorePlan`、`PlayerBackpackModel`。

### 正式资源配方目录（推荐复用）

能解决什么：统一提供 33 条正式机器/应急/融合配方的稳定 ID、复数输入输出、周期、建筑适用范围、默认配方、绑定节点动态产出与全量研究前置。在哪里：`Assets/_Game/Scripts/Economy/ResourceRecipeCatalog.cs`。怎么复用：统一提供 33 条正式机器/应急/融合配方的稳定 ID、复数输入输出、周期、建筑适用范围、默认配方、绑定节点动态产出与全量研究前置。不能负责什么：只定义正式配方静态配置；FormalProductionDefinitionCatalog 可按建筑和配方 ID 投影机器定义，但本目录不拥有队列、背包、建筑缓存、选择或进度，也不执行资源事务、自动串联或 UI 手势。改后跑哪组测试：`CraftingQueueModelTests`、`ResourceRecipeCatalogIntegrityTests`。代码名：`ResourceRecipeDefinition`、`ResourceRecipeCatalog`。

### 应急合成队列（推荐复用）

能解决什么：管理最多 20 次执行的 FIFO 应急合成队列，在入队时原子预留背包输入，并处理暂停、产出阻塞和取消返还；schema 31 公开捕获稳定执行 ID、预留输入、活动进度与 nextQueueOrdinal，并以绑定 revision/owner 的 prepare/commit 恢复。在哪里：`Assets/_Game/Scripts/Economy/CraftingQueueModel.cs`。怎么复用：管理最多 20 次执行的 FIFO 应急合成队列，在入队时原子预留背包输入，并处理暂停、产出阻塞和取消返还；schema 31 公开捕获稳定执行 ID、预留输入、活动进度与 nextQueueOrdinal，并以绑定 revision/owner 的 prepare/commit 恢复。不能负责什么：只拥有当前会话的合成队列、预留材料、活动进度和阻塞原因；恢复不会再次扣除已预留输入，未知配方保留为 MissingContent 暂停项并允许取消退款。模型不访问城市或建筑库存，不解释鼠标手势，不拥有文件 IO，也不把当前队列最大 ID 当作历史高水位。改后跑哪组测试：`CraftingQueueModelTests`、`GrayboxFormalSaveEconomyTests`。代码名：`CraftingQueueRestoreEntry`、`CraftingQueueExecutionSnapshot`、`CraftingQueueRestorePlan`、`CraftingQueueModel`。

### 手工资源访问规则（推荐复用）

能解决什么：按当前直接控制目标、领袖招募、两格欧氏距离、footprint 和建筑生命周期事实评估城市或建筑库存的手工访问资格。在哪里：`Assets/_Game/Scripts/Economy/ManualResourceAccessRules.cs`。怎么复用：按当前直接控制目标、领袖招募、两格欧氏距离、footprint 和建筑生命周期事实评估城市或建筑库存的手工访问资格。不能负责什么：只返回纯访问判定；不查找场景对象、不解析旋转尺寸、不执行资源转移，也不缓存资格。调用方必须在每次提交时传入当前事实和权威 footprint。改后跑哪组测试：`ManualResourceAccessRulesTests`。代码名：`ManualResourceAccessRules`。

### 正式机器生产定义目录（推荐复用）

能解决什么：把 ResourceRecipeCatalog 的正式机器配方按建筑投影为运行时生产定义，并保留采矿、冶炼和装配三个兼容入口；输入、输出、周期和内部容量仍以正式配方目录为真值。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionDefinitionCatalog.cs`。怎么复用：把 ResourceRecipeCatalog 的正式机器配方按建筑投影为运行时生产定义，并保留采矿、冶炼和装配三个兼容入口；输入、输出、周期和内部容量仍以正式配方目录为真值。不能负责什么：只负责机器配方到运行时定义的确定性投影与缓存；不保存建筑实例当前配方或库存，不推进周期，不判断物流连接，也不维护第二套配方数值。改后跑哪组测试：`FormalProductionSimulationTests`、`ResourceRecipeCatalogIntegrityTests`。代码名：`FormalProductionDefinition`、`FormalProductionDefinitionCatalog`。

### 逐建筑生产状态（推荐复用）

能解决什么：按稳定建筑实例和当前配方拥有多资源输入、多资源输出、完整批次预留、进度与玩家暂停；schema 31 恢复先以配方输入集合验证无序预留和资源语义，再原子替换单建筑状态。在哪里：`Assets/_Game/Scripts/Economy/BuildingProductionState.cs`。怎么复用：按稳定建筑实例和当前配方拥有多资源输入、多资源输出、完整批次预留、进度与玩家暂停；schema 31 恢复先以配方输入集合验证无序预留和资源语义，再原子替换单建筑状态。不能负责什么：只拥有单座建筑的生产状态，不自行读取场景或城市范围。物流连接与停工原因属于派生状态而不入档；玩家暂停入档，并在恢复时据此重建 PlayerPaused 停工原因。改后跑哪组测试：`FormalProductionSimulationTests`、`GrayboxFormalSaveProductionTests`。代码名：`BuildingProductionState`。

### 正式生产与物流模拟（推荐复用）

能解决什么：按稳定实例顺序通过 CityResourceStorageModel 执行确定性物流步，对全部输入/输出通道进行原子预留、容量预检和批次完成，推进独立生产周期并通过世界地图真值完成采矿。在哪里：`Assets/_Game/Scripts/Economy/FormalProductionSimulation.cs`。怎么复用：按稳定实例顺序通过 CityResourceStorageModel 执行确定性物流步，对全部输入/输出通道进行原子预留、容量预检和批次完成，推进独立生产周期并通过世界地图真值完成采矿。不能负责什么：不选择配方，不计算放置合法性、物流距离、建筑生命周期或场景时间；调用方必须提供已确认资格、连接状态和正式城市仓库聚合模型。保留 ResourceCapacityPolicy 重载只用于旧接口兼容。改后跑哪组测试：`FormalProductionSimulationTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`FormalProductionSimulation`。

### 三维生产建筑资格（复用前审查）

能解决什么：从现有三维建筑实例生命周期派生有效仓库资格。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionEligibility3D.cs`。怎么复用：从现有三维建筑实例生命周期派生有效仓库资格。不能负责什么：只组合已完成、玩家拥有、未撤离锁定和稳定建筑 ID；不计算容量、物流距离、配方或放置合法性。改后跑哪组测试：`GrayboxProductionLifecycleTests`、`GrayboxProductionRuntimeTests`。代码名：`GrayboxProductionEligibility3D`。

### 三维生产运行时（复用前审查）

能解决什么：按稳定实例 ID 同步允许配方列表、当前配方、生产状态、可运行集合和物流连接；只在进度、预留及输入输出缓存均为空时提交安全切换。schema 31 确定性保存非默认配方，未知配方保留为不可运行 orphan，并通过绑定 owner、同步 generation 与内容 fingerprint 的 prepare/commit 恢复。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs`。怎么复用：按稳定实例 ID 同步允许配方列表、当前配方、生产状态、可运行集合和物流连接；只在进度、预留及输入输出缓存均为空时提交安全切换。schema 31 确定性保存非默认配方，未知配方保留为不可运行 orphan，并通过绑定 owner、同步 generation 与内容 fingerprint 的 prepare/commit 恢复。不能负责什么：只桥接 GrayboxBuildingInstance3D、正式配方目录与逐建筑生产状态；不推进时间、不执行城市事务，撤离协调器不能修改载荷快照。不得复制放置、物流范围或节点兼容规则；物流连接、停工原因和 observability revision/hash 仍由当前规则重建而不持久化。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxFormalSaveProductionTests`、`GrayboxProductionCombatLossTests`、`GrayboxProductionRuntimeTests`、`GrayboxProductionLifecycleTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxProductionEvacuationPayload3D`、`GrayboxProductionPersistenceState3D`、`GrayboxProductionRestorePlan3D`、`GrayboxProductionRuntime3D`。

### 三维生产固定时钟（复用前审查）

能解决什么：用 0.1 秒固定步长驱动运行时与正式生产模拟，保证分帧确定性和暂停无追赶；结算统计只消费 `ProductionStatisticsDelta`，玩家暂停时不计生产合格时间。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionClock3D.cs`。怎么复用：用 0.1 秒固定步长驱动运行时与正式生产模拟，保证分帧确定性和暂停无追赶；结算统计只消费 `ProductionStatisticsDelta`，玩家暂停时不计生产合格时间。不能负责什么：只拥有会话级余量、上一轮统计增量并组合运行时、模拟和 CityResourceStorageModel；不读取 Unity Time，不决定建筑资格，不累计完整会话统计，不处理 UI。旧 ResourceInventory 重载仅用于兼容回归。改后跑哪组测试：`GrayboxProductionClockTests`、`GrayboxProductionStatisticsDeltaTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`ProductionStatisticsDelta`、`GrayboxProductionClock3D`。

### 三维生产可观察化只读边界（推荐复用）

能解决什么：发布按稳定实例 ID 排序、包含允许/当前配方和全部输入/输出/预留通道的不可变生产详情；仓库与城市库存变化通过 CityResourceStorageModel.Revision 进入内容哈希。命令门面按 stable ID 提交配方切换、暂停和建筑缓存/背包/权威城市仓库之间的通道转移。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/ProductionObservabilitySnapshot.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionCommandFacade3D.cs`。怎么复用：发布按稳定实例 ID 排序、包含允许/当前配方和全部输入/输出/预留通道的不可变生产详情；仓库与城市库存变化通过 CityResourceStorageModel.Revision 进入内容哈希。命令门面按 stable ID 提交配方切换、暂停和建筑缓存/背包/权威城市仓库之间的通道转移。不能负责什么：快照只读，不暴露 BuildingProductionState 或可变库存；命令不接受 UI 传入仓库数量。访问距离、物流和建筑生命周期资格仍由当前 3D 场景适配器在每次提交前基于权威事实重新验证；本边界不复制资格规则、不接入冻结 2D，也不进入 schema 30。改后跑哪组测试：`GrayboxProductionObservabilityFacadeTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`ProductionResourceObservability`、`ProductionBuildingObservability`、`ProductionObservabilitySnapshot`、`GrayboxProductionCommandFacade3D`。

### 三维生产场景控制器（仅限场景）

能解决什么：把当前三维场景的建筑会话、城市模式、世界坐标、CityResourceStorageModel 和 Unity 暂停状态接到固定步生产时钟。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionController3D.cs`。怎么复用：把当前三维场景的建筑会话、城市模式、世界坐标、CityResourceStorageModel 和 Unity 暂停状态接到固定步生产时钟。不能负责什么：只负责 GrayboxPrototype3D 场景引用与时间输入；不复制生产配方、物流范围、资源节点兼容性、仓库库存事务或界面规则。改后跑哪组测试：`GrayboxProductionControllerTests`、`GrayboxSceneContractTests`、`GrayboxWarehouseStorageIntegrationTests`。代码名：`GrayboxProductionController3D`。

### 正式关注度来源目录（复用前审查）

能解决什么：从 IDEA-0020 的唯一正式目录读取初始值、范围、历史容量、30、60、90 阈值和 GDD A16.6 的 22 项稳定关注度来源。在哪里：`Assets/_Game/Scripts/Progression/FormalAttentionCatalog.cs`。怎么复用：从 IDEA-0020 的唯一正式目录读取初始值、范围、历史容量、30、60、90 阈值和 GDD A16.6 的 22 项稳定关注度来源。不能负责什么：只定义关注度静态身份、固定增量、重复策略和显示键；不保存会话数值、不读取场景或 UI，也不启动压力战斗。改后跑哪组测试：`FormalAttentionCatalogTests`。代码名：`FormalAttentionReasonDefinition`、`FormalAttentionCatalog`。

### 正式关注度运行时（推荐复用）

能解决什么：以稳定原因和事件键原子提交 0–100 关注度，维护 128 条有界历史、最近三条投影、30、60、90 一次性阈值和可缓存不可变快照。在哪里：`Assets/_Game/Scripts/Progression/FormalAttentionRuntime.cs`。怎么复用：以稳定原因和事件键原子提交 0–100 关注度，维护 128 条有界历史、最近三条投影、30、60、90 一次性阈值和可缓存不可变快照。不能负责什么：只拥有关注度数值、历史、幂等事实、阈值锁存和恢复边界；不扫描建筑或科技、不访问 Unity，也不直接生成敌人、写存档文件或操作 UI。改后跑哪组测试：`FormalAttentionRuntimeTests`、`FormalProgressionTests`、`FormalAttentionPerformanceTests`。代码名：`FormalAttentionHistoryEntry`、`FormalAttentionSnapshot`、`FormalAttentionRuntime`。

### 正式关注度压力纯运行时（复用前审查）

能解决什么：以固定 30、60、90 阈值目录维护升序有界压力队列、精确规则计时和串行遭遇，并通过窄控制器接入正式 Defense、schema 33 与 HUD；Warning 规则仍逐 tick 推进，但可见快照最多每 0.1 秒发布一次。在哪里：`Assets/_Game/Scripts/Progression/AttentionPressureCatalog.cs`、`Assets/_Game/Scripts/Progression/AttentionPressureRuntime.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxAttentionPressureDefenseController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxAttentionPressureRuntimeController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxAttentionPressureSaveAdapter3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxAttentionPressurePresentationController3D.cs`、`Assets/_Game/Scripts/Combat/CampaignWaveCatalog.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseCampaignModel.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveRuntimeHost3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProgressionHudView3D.cs`、`Assets/_Game/Scripts/Persistence/ThreeD/FormalThreeDSaveData.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveCodec.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveValidator.cs`。怎么复用：以固定 30、60、90 阈值目录维护升序有界压力队列、精确规则计时和串行遭遇，并通过窄控制器接入正式 Defense、schema 33 与 HUD；Warning 规则仍逐 tick 推进，但可见快照最多每 0.1 秒发布一次。不能负责什么：纯运行时拥有压力队列/计时真值，0.1 秒只限制可见 snapshot/HUD 发布频率，不能量化或拖慢 60、75、90 秒规则；控制器不复制十波战斗算法、不从表现反推敌人或阈值，也不直接改 Attention。改后跑哪组测试：`AttentionPressureCatalogTests`、`AttentionPressureRuntimeTests`、`FormalAttentionRuntimeTests`、`GrayboxAttentionPressureRuntimeControllerTests`、`GrayboxAttentionPressureDefenseControllerTests`、`GrayboxAttentionPressureSaveAdapterTests`、`GrayboxAttentionPressurePresentationTests`、`FormalAttentionPerformanceTests`、`GrayboxAttentionPressureRuntimeInputTests`、`AttentionPressureCampaignCatalogTests`、`SingleCityDefenseCampaignPersistenceTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`、`FormalSaveSchema33ContractTests`、`FormalSaveSchema33MigrationTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxFormalSaveCheckpointTests`。代码名：`AttentionPressureDefinition`、`AttentionPressureCatalog`、`AttentionPressureCommand`、`AttentionPressureEntrySnapshot`、`AttentionPressureSnapshot`、`AttentionPressureRuntime`、`GrayboxAttentionPressureDefenseController3D`、`GrayboxAttentionPressureRuntimeController3D`、`GrayboxAttentionPressureRestorePlan3D`、`GrayboxAttentionPressureSaveAdapter3D`、`GrayboxAttentionPressurePresentationController3D`。

### 晶壳母体纯遭遇规则（复用前审查）

能解决什么：由唯一 CrystalBroodmotherCatalog 定义 Boss 生命、速度、固定步和 70%/35% 阶段增援；AttentionPressureCampaignCatalog 只引用稳定 Boss 原型和正式战役波，不复制数值目录。在哪里：`Assets/_Game/Scripts/Combat/CrystalBroodmotherCatalog.cs`、`Assets/_Game/Scripts/Combat/CrystalBroodmotherEncounter.cs`、`Assets/_Game/Scripts/Combat/AttentionPressureCampaignCatalog.cs`、`Assets/_Game/Scripts/Combat/EnemyDefinition.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs`。怎么复用：由唯一 CrystalBroodmotherCatalog 定义 Boss 生命、速度、固定步和 70%/35% 阶段增援；AttentionPressureCampaignCatalog 只引用稳定 Boss 原型和正式战役波，不复制数值目录。不能负责什么：目录和遭遇模型不拥有刷怪、建筑伤害、存档或 UI；正式 Defense 仍是敌人生命与击败事实唯一 owner。其他 Campaign/Runtime/表现只能消费唯一 Boss 目录，不能再声明第二套生命、速度或阶段阈值。改后跑哪组测试：`CrystalBroodmotherCatalogTests`、`CrystalBroodmotherEncounterTests`、`CrystalBroodmotherPerformanceTests`、`AttentionPressureCampaignCatalogTests`、`GrayboxAttentionPressureDefenseControllerTests`、`GrayboxDefensePresentationTests`、`GrayboxDefenseObservabilityTests`、`GrayboxDefenseSnapshotStabilityTests`、`GrayboxAttentionPressureRuntimeInputTests`。代码名：`CrystalBroodmotherReinforcementDefinition`、`CrystalBroodmotherPhaseDefinition`、`CrystalBroodmotherCatalog`、`CrystalBroodmotherCommand`、`CrystalBroodmotherSnapshot`、`CrystalBroodmotherEncounter`、`AttentionPressureCampaignCatalog`。

### 三维文明进程领域事件路由器（复用前审查）

能解决什么：订阅首次展开、建筑完成和自然研究完成的已提交领域事件，以稳定原因和事件键交给唯一 FormalAttentionRuntime，并把固定命轨选择与 +5 关注度作为可回滚的窄事务提交。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProgressionEventRouter3D.cs`。怎么复用：订阅首次展开、建筑完成和自然研究完成的已提交领域事件，以稳定原因和事件键交给唯一 FormalAttentionRuntime，并把固定命轨选择与 +5 关注度作为可回滚的窄事务提交。不能负责什么：只路由已经发生的领域事实；不扫描建筑或科技、不轮询 revision、不从存档恢复状态追补历史、不计算目录增量、不生成压力战斗、不读写文件或操作 UI。扫描、救援、锁定区离开和干扰遗迹在没有权威发布者时保持未接线，Dispose 必须解除全部订阅。改后跑哪组测试：`GrayboxProgressionEventIntegrationTests`。代码名：`GrayboxProgressionEventRouter3D`。

### 三维文明进程关注度 HUD（复用前审查）

能解决什么：从正式 Attention/Fate 不可变快照投影关注度数值、四阶段、下一阈值、最近三条中文原因和固定命轨候选，并用真实 UGUI 状态按钮打开或关闭详情。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProgressionHudView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxProgressionHudController3D.cs`。怎么复用：从正式 Attention/Fate 不可变快照投影关注度数值、四阶段、下一阈值、最近三条中文原因和固定命轨候选，并用真实 UGUI 状态按钮打开或关闭详情。不能负责什么：只读取不可变快照并更新真实 UGUI；不写 Attention/Fate runtime、不进入 schema、不复制关注度增量或命轨规则，也不得在 EffectsReady=false 时开放或强制弹出命轨选择。相同快照引用必须保持零重复刷新。改后跑哪组测试：`GrayboxProgressionPresentationTests`、`GrayboxProgressionRuntimeInputTests`。代码名：`GrayboxProgressionHudView3D`、`GrayboxProgressionHudController3D`。

### 三维固定命轨强制选择 UI（复用前审查）

能解决什么：以真实 UGUI 全屏阻断世界输入，完整显示固定三命轨目录文案，并通过二次确认和 ProgressionEventRouter 原子提交唯一 Lv.1 选择与 +5 关注度。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFateSelectionView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFateSelectionController3D.cs`。怎么复用：以真实 UGUI 全屏阻断世界输入，完整显示固定三命轨目录文案，并通过二次确认和 ProgressionEventRouter 原子提交唯一 Lv.1 选择与 +5 关注度。不能负责什么：强制选择与 Host/真实输入已接且只负责首次 Lv.1 选择；不直接写 Fate 或 Attention runtime，不提供回溯锚点创建/读取按钮，也不承担文明升阶。Lv.2 由独立文明升阶 UI 与 Host 事务处理。改后跑哪组测试：`GrayboxFateSelectionPresentationTests`、`GrayboxFateSelectionRuntimeInputTests`。代码名：`GrayboxFateSelectionCard3D`、`GrayboxFateSelectionView3D`、`GrayboxFateSelectionController3D`。

### 三维命轨专属详情与操作面板（复用前审查）

能解决什么：以四份不可变快照和引用缓存显示已选命轨、袖珍旗舰与坍缩、虚空债务与结算，以及 Lv.1 单锚点/Lv.2 双锚点稳定槽选择，并提供真实 UGUI Create、Read、Clear 与指定槽读取二次确认。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFateOperationsView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFateOperationsController3D.cs`。怎么复用：以四份不可变快照和引用缓存显示已选命轨、袖珍旗舰与坍缩、虚空债务与结算，以及 Lv.1 单锚点/Lv.2 双锚点稳定槽选择，并提供真实 UGUI Create、Read、Clear 与指定槽读取二次确认。不能负责什么：只读取 Fate/Pocket/Void/Rewind 不可变快照并发布带稳定锚点 ID 的命令事件，不直接写 runtime、schema 或文件；Create/Read/Clear 必须由 Host 绑定到唯一 Rewind Service。未选命轨不开放，读取必须二次确认。改后跑哪组测试：`GrayboxFateOperationsPresentationTests`、`GrayboxFateOperationsRuntimeInputTests`。代码名：`GrayboxFateOperationsView3D`、`GrayboxFateOperationsController3D`。

### 袖珍宇宙命轨纯规则与生产适配边界（复用前审查）

能解决什么：从正式机器配方目录确定永久旗舰，由领域控制器接入生产批次和 +4 关注度，并由坍缩解析器把 Lv.1 3×3 命令接到正式建筑生命与战损提交。在哪里：`Assets/_Game/Scripts/Progression/PocketUniverseFateEffect.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxPocketUniverseFateController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxPocketUniverseCollapseResolver3D.cs`。怎么复用：从正式机器配方目录确定永久旗舰，由领域控制器接入生产批次和 +4 关注度，并由坍缩解析器把 Lv.1 3×3 命令接到正式建筑生命与战损提交。不能负责什么：Lv.1/Lv.2 已由 Host 文明升阶事务组合并进入 schema 33 效果 DTO；升级只改变正式产出倍率和坍缩范围，不修改输入、周期或容量。正式三维建模仍未制作，聚焦自动化不能替代人工视觉验收。改后跑哪组测试：`PocketUniverseFateEffectTests`、`PocketUniverseProductionIntegrationTests`、`GrayboxPocketUniverseFateControllerTests`、`GrayboxPocketUniverseCollapseResolverTests`。代码名：`PocketUniverseBuildingCandidate`、`PocketUniverseFlagshipState`、`PocketUniverseCollapseCommand`、`PocketUniverseFateSnapshot`、`PocketUniverseFateEffect`、`GrayboxPocketUniverseFateController3D`、`GrayboxPocketUniverseCollapseResolver3D`。

### 虚空债纯规则运行时（复用前审查）

能解决什么：按正式资源维护债务与结算时钟，由领域控制器接入施工透支和收入还款，并把稳定周期键逐项原子提交到 Attention。在哪里：`Assets/_Game/Scripts/Progression/FormalVoidDebtRuntime.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxVoidDebtController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxVoidDebtAttentionController3D.cs`。怎么复用：按正式资源维护债务与结算时钟，由领域控制器接入施工透支和收入还款，并把稳定周期键逐项原子提交到 Attention。不能负责什么：Lv.1/Lv.2 已由 Host 文明升阶事务组合并进入 schema 33 效果 DTO；普通消费仍不得透支，升级只把正式结算间隔切换为 60 秒，命轨专属详情继续显示债务与计时。调用方不得直接制造负库存。改后跑哪组测试：`FormalVoidDebtRuntimeTests`、`GrayboxVoidDebtIntegrationTests`、`GrayboxVoidDebtAttentionControllerTests`。代码名：`FormalVoidDebtEntry`、`FormalVoidDebtSnapshot`、`FormalVoidDebtRuntime`、`GrayboxVoidDebtController3D`、`GrayboxVoidDebtAttentionController3D`。

### 正式回溯锚点内部 Store（复用前审查）

能解决什么：通过两个独立隐藏内部槽、元数据 runtime 与 Coordinator 服务捕获和恢复非递归 schema 33 锚点；Lv.1 容量为 1，Lv.2 容量为 2，满槽时按稳定创建序号替换最旧锚点，读取保留当前关注度并原子追加回溯代价。读取升阶前创建的 Lv.1 槽后必须保留当前文明/命轨 Lv.2 与两个现役锚点。在哪里：`Assets/_Game/Scripts/Persistence/FormalRewindAnchorStore.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxRewindAnchorService3D.cs`、`Assets/_Game/Scripts/Progression/FormalRewindAnchorMetadataRuntime.cs`。怎么复用：通过两个独立隐藏内部槽、元数据 runtime 与 Coordinator 服务捕获和恢复非递归 schema 33 锚点；Lv.1 容量为 1，Lv.2 容量为 2，满槽时按稳定创建序号替换最旧锚点，读取保留当前关注度并原子追加回溯代价。读取升阶前创建的 Lv.1 槽后必须保留当前文明/命轨 Lv.2 与两个现役锚点。不能负责什么：Host、schema 33、文明升阶容量切换、跨等级读取、双槽选择 UI 与指定槽 Create/Read/Clear 已接并通过对应聚焦自动化；锚点载荷中的旧 progression 不得把当前 Lv.2 owner 或双槽元数据降级。它仍不是第二个玩家存档槽，按钮只能调用 Host Service。改后跑哪组测试：`FormalRewindAnchorStoreTests`、`GrayboxRewindAnchorServiceTests`、`FormalRewindAnchorMetadataRuntimeTests`、`GrayboxFateOperationsPresentationTests`、`GrayboxFateOperationsRuntimeInputTests`。代码名：`FormalRewindAnchorStoreResult`、`FormalRewindAnchorStore`、`GrayboxRewindAnchorServiceResult3D`、`GrayboxRewindAnchorService3D`、`FormalRewindAnchorMetadata`、`FormalRewindAnchorMetadataSnapshot`、`FormalRewindAnchorMetadataUpsertPlan`、`FormalRewindAnchorMetadataClearPlan`、`FormalRewindAnchorMetadataRuntime`。

### 正式固定三命轨目录（复用前审查）

能解决什么：定义 IDEA-0020 固定三选一的袖珍宇宙、虚空债和回溯锚点稳定身份、顺序、显示键与规则键。在哪里：`Assets/_Game/Scripts/Progression/FormalFateCatalog.cs`。怎么复用：定义 IDEA-0020 固定三选一的袖珍宇宙、虚空债和回溯锚点稳定身份、顺序、显示键与规则键。不能负责什么：只定义正式三条命轨静态内容；不从 LegacyPathCatalog 的九条历史池随机抽取，不保存选择或等级，不执行生产、债务、回溯、关注度、存档或 UI 效果。改后跑哪组测试：`FormalFateCatalogTests`。代码名：`FormalFateDefinition`、`FormalFateCatalog`。

### 正式命轨选择运行时（推荐复用）

能解决什么：维护固定三候选、待选择与已选择状态、命轨等级、Lv.1→Lv.2 单次晋级、revision、缓存快照和原子恢复；Lv.2 静态目录集中提供袖珍宇宙×4 与 4×4、虚空债 60 秒和回溯容量 2。在哪里：`Assets/_Game/Scripts/Progression/FormalFateRuntime.cs`、`Assets/_Game/Scripts/Progression/FormalFateLevelTwoCatalog.cs`。怎么复用：维护固定三候选、待选择与已选择状态、命轨等级、Lv.1→Lv.2 单次晋级、revision、缓存快照和原子恢复；Lv.2 静态目录集中提供袖珍宇宙×4 与 4×4、虚空债 60 秒和回溯容量 2。不能负责什么：只拥有命轨选择与等级真值及 Lv.2 参数；不判断文明升阶条件，不执行袖珍宇宙、虚空债或回溯锚点效果，不增加关注度，不创建检查点，不读写文件、不访问 Unity 或 UI。改后跑哪组测试：`FormalFateRuntimeTests`、`FormalProgressionTests`、`FormalFateLevelTwoCatalogTests`、`FormalFatePerformanceTests`。代码名：`FormalFateSnapshot`、`FormalFateRuntime`。

### 研究模型（推荐复用）

能解决什么：管理已完成科技、活动科技和剩余规则时间，并为 schema 31 提供确定性 snapshot、零写入 prepare 和绑定 revision/owner 的单次 commit；语法有效的未知已完成科技与未知活动科技会原样保留。在哪里：`Assets/_Game/Scripts/Research/ResearchModel.cs`。怎么复用：管理已完成科技、活动科技和剩余规则时间，并为 schema 31 提供确定性 snapshot、零写入 prepare 和绑定 revision/owner 的单次 commit；语法有效的未知已完成科技与未知活动科技会原样保留。不能负责什么：未知已完成科技不授予效果，未知活动科技保持暂停；恢复不重新扣除研究资源，也不保存或伪造研究站资格、城市倍率、UI 或表现。模型不拥有文件 IO，正式 3D 目录解析由调用方显式提供。改后跑哪组测试：`ResearchTests`、`FormalResearchCatalogTests`、`DemoResearchRuntimeTests`、`GrayboxFormalSaveEconomyTests`。代码名：`ResearchPersistenceSnapshot`、`ResearchRestorePlan`、`ResearchModel`。

### 正式四路线科技运行时（推荐复用）

能解决什么：组合统一 ResearchModel 与正式 44 节点 ResearchCatalog，包含 core.research.legacy-analysis；运行时管理研究生命周期，已完成集合由 ResearchEffectCatalog 与 ResearchStatusCatalog 派生正式被动效果和高级状态。在哪里：`Assets/_Game/Scripts/Research/ResearchModel.cs`、`Assets/_Game/Scripts/Research/FormalResearchRuntime.cs`。怎么复用：组合统一 ResearchModel 与正式 44 节点 ResearchCatalog，包含 core.research.legacy-analysis；运行时管理研究生命周期，已完成集合由 ResearchEffectCatalog 与 ResearchStatusCatalog 派生正式被动效果和高级状态。不能负责什么：不持有研究站、城市模式、暂停、库存、战斗目标或跨帧状态真值，不处理 Unity 输入、科技树布局、文件 IO、关注度或效果表现；高级状态必须交由独立战斗/建筑 owner 管理并通过 schema 35 适配。改后跑哪组测试：`FormalResearchCatalogTests`、`FormalResearchRuntimeTests`、`ResearchEffectCatalogTests`、`IDEA0027ResearchCatalogAndStatusTests`、`IDEA0027ResearchRuntimeEffectsTests`、`GrayboxFormalResearchSaveAdapterTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`ResearchCatalog`、`CivilizationResearchAvailability`、`FormalResearchRuntime`。

### 正式科技复合效果目录（推荐复用）

能解决什么：按 44 个正式科技稳定 ID 提供类型化复合效果、前后数值、作用范围、叠加规则与 Executable 接线状态，并从已完成科技集合确定性派生生产、研究、物流、耐久、预警、再生、击杀回收及高级状态配置。在哪里：`Assets/_Game/Scripts/Research/ResearchEffectCatalog.cs`。怎么复用：按 44 个正式科技稳定 ID 提供类型化复合效果、前后数值、作用范围、叠加规则与 Executable 接线状态，并从已完成科技集合确定性派生生产、研究、物流、耐久、预警、再生、击杀回收及高级状态配置。不能负责什么：不持有完成科技、战斗目标或跨帧状态真值，不保存 applied 标志、不处理 UI 或输入；临时特质、护盾、发射器冷却和跨帧减益由正式运行时 owner 与 schema 35 适配器持有。整数吞吐增益继续通过周期缩短表达。改后跑哪组测试：`ResearchEffectCatalogTests`、`ResearchProductionEffectIntegrationTests`、`ResearchEffectProductionRuntimeTests`、`ResearchRuntimeEffectIntegrationTests`、`IDEA0027ResearchCatalogAndStatusTests`。代码名：`ResearchEffectDefinition`、`ResearchEffectCatalog`、`ResearchEffectSnapshot`、`ResearchEffectResolver`、`ResearchKillRewardResolver`、`FormalProductionResearchModifierAdapter`。

### 高级科技战斗与建筑状态运行时（推荐复用）

能解决什么：统一持有超载、剑意、感染、共振、心控、护盾、再生、傀儡、巨兽、领袖特质与来源塔—目标敌人独立冷却等跨帧高级科技状态，并提供确定性 Advance、快照、验证与恢复。在哪里：`Assets/_Game/Scripts/Research/ResearchStatusCatalog.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseTechnologyState.cs`。怎么复用：统一持有超载、剑意、感染、共振、心控、护盾、再生、傀儡、巨兽、领袖特质与来源塔—目标敌人独立冷却等跨帧高级科技状态，并提供确定性 Advance、快照、验证与恢复。不能负责什么：状态运行时不查询 Unity 场景、不决定科技是否完成、不直接写存档或 UI；暂停、命中、死亡、建筑物流和战役切换由各权威 owner 提供。正式磁盘往返必须通过 schema 35 adapter，不能只恢复目标集合而丢失发射器来源与冷却。改后跑哪组测试：`IDEA0027DefenseTechnologyRuntimeTests`、`IDEA0027ArmyTechnologyEffectsTests`。代码名：`ResearchStatusCatalog`、`SingleCityDefenseTechnologyRuntime`、`SingleCityDefenseTechnologyStateSnapshot`、`SingleCityDefenseTechnologyPersistenceSnapshot`。

### schema 35 高级科技状态存档适配器（复用前审查）

能解决什么：把当前战役的高级科技状态、奖励账本和独立 emitter 冷却映射为 schema 35 DTO，提供零写入预检、原子恢复、回滚与 34→35 空状态迁移。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchEffectStateSaveAdapter3D.cs`、`Assets/_Game/Scripts/Persistence/ThreeD/FormalThreeDSaveData.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveCodec.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveValidator.cs`。怎么复用：把当前战役的高级科技状态、奖励账本和独立 emitter 冷却映射为 schema 35 DTO，提供零写入预检、原子恢复、回滚与 34→35 空状态迁移。不能负责什么：只映射经过验证的领域快照，不生成新奖励、不推进时间、不扫描场景或切换战役；main 与 pressure 状态隔离，未激活战役不进入当前存档，未知效果、错误塔型、悬空敌人、非法冷却和重复来源—目标对必须拒绝。改后跑哪组测试：`FormalSaveSchema35ContractTests`、`IDEA0027ResearchEffectStateSaveAdapterTests`、`GrayboxFormalSaveCoordinatorTests`。代码名：`GrayboxResearchEffectStateSaveAdapter3D`、`FormalThreeDResearchEffectStateSaveData`。

### 正式科技复合效果展示投影（复用前审查）

能解决什么：把统一科技效果目录投影为科技树和开发修改器可读的解锁/被动标签、前后值、范围、叠加与已生效/研究后生效/仅预览状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/ResearchEffectPresentationCatalog3D.cs`。怎么复用：把统一科技效果目录投影为科技树和开发修改器可读的解锁/被动标签、前后值、范围、叠加与已生效/研究后生效/仅预览状态。不能负责什么：只格式化正式效果 DTO，不复制数值、不解析中文说明为玩法真值；未接线或 Preview activation 效果不得显示为已生效，已由管理台或文明门完成的可执行节点按 completed 真值显示。改后跑哪组测试：`ResearchEffectPresentationTests`、`ResearchTreeUiContractTests`。代码名：`ResearchEffectLinePresentation3D`、`ResearchEffectPresentationCatalog3D`。

### 正式科技树确定性投影（复用前审查）

能解决什么：把正式科技目录投影为稳定的 44 节点、49 依赖边和 6 个双前置桥；统一 Profile 固定 1920×1080 三段区域、顶部五组槽、底部六舱、四路线泳道/双子列、180×58 普通节点、90×112 桥节点和 350×74 公共根，并保留 Fit/Focus 与指针锚定缩放纯计算。在哪里：`Assets/_Game/Scripts/Graybox3D/ResearchTreeProjection3D.cs`、`Assets/_Game/Scripts/Graybox3D/ResearchTreeVisualLayoutProfile3D.cs`。怎么复用：把正式科技目录投影为稳定的 44 节点、49 依赖边和 6 个双前置桥；统一 Profile 固定 1920×1080 三段区域、顶部五组槽、底部六舱、四路线泳道/双子列、180×58 普通节点、90×112 桥节点和 350×74 公共根，并保留 Fit/Focus 与指针锚定缩放纯计算。不能负责什么：只从 ResearchCatalog 派生图真值与表现坐标，不创建场景对象，不读取输入、库存、研究运行态或存档，也不解析中文状态文案；Profile 不得成为第二套科技目录。改后跑哪组测试：`IDEA0024ResearchTreePresentationTests`、`ResearchTreeProjection3DTests`、`ResearchTreeUiContractTests`。代码名：`ResearchTreeProjection3D`、`ResearchTreeNodeProjection3D`、`ResearchTreeEdgeProjection3D`、`ResearchTreeJunctionProjection3D`、`ResearchTreeViewportState3D`、`ResearchTreeVisualLayoutProfile3D`。

### 三维首版科技目录（推荐复用）

能解决什么：保留 A16.4 历史六节点 release profile 的稳定 ID 与退役内容元数据，供旧测试、旧存档和兼容 facade 使用；正式 3D 科技树与新运行时必须读取 ResearchCatalog。在哪里：`Assets/_Game/Scripts/Research/DemoResearchCatalog.cs`。怎么复用：保留 A16.4 历史六节点 release profile 的稳定 ID 与退役内容元数据，供旧测试、旧存档和兼容 facade 使用；正式 3D 科技树与新运行时必须读取 ResearchCatalog。不能负责什么：只定义历史 Demo 兼容配置；不得重新成为正式枚举源，不保存完成状态、不扣资源、不推进时间，也不向正式 43 节点目录静默映射退役 ID。改后跑哪组测试：`DemoResearchRuntimeTests`。代码名：`DemoResearchCatalog`。

### 三维首版科技运行时（推荐复用）

能解决什么：组合统一研究模型与六节点 release profile，提交研究启动、模式倍率推进、研究站暂停和 80% 原子取消退款，并以该 release profile 显式解析 schema 31 的已知与未知科技恢复状态。在哪里：`Assets/_Game/Scripts/Research/DemoResearchRuntime.cs`。怎么复用：组合统一研究模型与六节点 release profile，提交研究启动、模式倍率推进、研究站暂停和 80% 原子取消退款，并以该 release profile 显式解析 schema 31 的已知与未知科技恢复状态。不能负责什么：只拥有当前 3D 会话的研究规则适配；未知已完成科技不授予效果，未知活动科技暂停且可由持久化快照继续保留。调用方仍须提供合格研究站、城市模式、全局暂停、城市库存与容量事实；它不处理 Unity 输入、UI、文件 IO、关注度或战斗效果。改后跑哪组测试：`DemoResearchRuntimeTests`、`GrayboxFormalSaveEconomyTests`。代码名：`DemoResearchRuntime`。

### 三维统一正式规则时钟（仅限场景）

能解决什么：在正式三维场景把玩家请求速度、系统菜单、战役胜利、失败与文明升阶等独立暂停原因合并为唯一有效规则时间；升阶表现时钟只忽略 Advancement，自身仍受 CampaignVictory/Defeat 等终局原因约束。在哪里：`Assets/_Game/Scripts/Core/GameSpeedModel.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxFormalRuleClock3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCampaignTerminalSpeedGate3D.cs`。怎么复用：在正式三维场景把玩家请求速度、系统菜单、战役胜利、失败与文明升阶等独立暂停原因合并为唯一有效规则时间；升阶表现时钟只忽略 Advancement，自身仍受 CampaignVictory/Defeat 等终局原因约束。不能负责什么：只作为 GrayboxPrototype3D 的规则时钟与暂停组合根；CampaignVictory 与 Advancement 必须由各自 owner 获取和释放，不允许互相解除。它不拥有领域玩法状态，也不允许领域控制器另起 Update 时钟。改后跑哪组测试：`GrayboxUnifiedRuleClockContractTests`、`GameSpeedTests`、`FormalGameSpeedCommandFacadeTests`、`GrayboxFormalSpeedHudAndTerminalTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`。代码名：`GameSpeedModel`、`GrayboxFormalRuleClock3D`、`GrayboxCampaignTerminalSpeedGate3D`。

### 单城市十波防御战役模型（推荐复用）

能解决什么：以正式十波目录推进预警、生成、清场和胜负阶段，按稳定目标与正式敌塔配置结算移动、索敌、攻击、耗材、建筑受击，并由唯一 `SessionStatisticsModel` 聚合生产、防御、战损、暂停和修改器事实；塔战斗余量可独立持久化并确定性恢复。在哪里：`Assets/_Game/Scripts/Combat/CampaignWaveCatalog.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseCampaignModel.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseTowerCombatModel.cs`、`Assets/_Game/Scripts/Defense/SingleCityDefenseTowerPersistenceState.cs`。怎么复用：以正式十波目录推进预警、生成、清场和胜负阶段，按稳定目标与正式敌塔配置结算移动、索敌、攻击、耗材、建筑受击，并由唯一 `SessionStatisticsModel` 聚合生产、防御、战损、暂停和修改器事实；塔战斗余量可独立持久化并确定性恢复。不能负责什么：只拥有纯战役、塔战斗和会话统计规则状态；不读取 Unity 时间、场景对象、城市库存或建筑会话，不直接摧毁建筑、不写 schema DTO，也不创建 HUD、轨迹或伤害表现。调用方必须提供稳定建筑目标、核心位置和已批准配置。改后跑哪组测试：`SingleCityDefenseCampaignCatalogTests`、`SingleCityDefenseCampaignCheckpointTests`、`SingleCityDefenseCampaignModelContractTests`、`SingleCityDefenseEnemyCampaignCombatTests`、`SingleCityDefenseTowerCombatModelTests`、`SingleCityDefenseTowerPersistenceTests`、`SingleCityDefenseTowerTargetingTests`、`SessionStatisticsTests`。代码名：`CampaignWaveDefinition`、`CampaignWaveCatalog`、`DefenseBuildingTargetCandidate`、`DefenseBuildingCombatTarget`、`SingleCityDefenseEnemySnapshot`、`SingleCityDefenseCampaignStatisticsSnapshot`、`SingleCityDefenseCampaignSnapshot`、`SingleCityDefenseCampaignModel`、`SingleCityDefenseTowerCombatModel`、`SingleCityDefenseTowerPersistenceState`。

### 通用会话统计模型（推荐复用）

能解决什么：各正式领域只提交已结算事实增量，模型负责非负验证、原子恢复、终局冻结、部分迁移标记与生产效率所需原始统计；结算层只读快照。在哪里：`Assets/_Game/Scripts/Core/SessionStatisticsModel.cs`。怎么复用：各正式领域只提交已结算事实增量，模型负责非负验证、原子恢复、终局冻结、部分迁移标记与生产效率所需原始统计；结算层只读快照。不能负责什么：不读取 Unity 时间、不推断未发生事件、不拥有战役胜负、UI、DTO 或文件 IO；schema 31 缺失历史不得伪造成完整统计。改后跑哪组测试：`SessionStatisticsTests`、`GrayboxProductionStatisticsDeltaTests`、`SingleCityDefenseCampaignCheckpointTests`。代码名：`SessionStatisticsMetric`、`SessionStatisticsSnapshot`、`SessionStatisticsModel`。

### 单城市防御终局结算模型（推荐复用）

能解决什么：终局 revision 变化时发布一次结算；胜利允许继续沙盒或返回标题，失败允许最近波前重试或返回标题，缺少生产合格时间时明确显示无数据，迁移统计明确标为部分统计。在哪里：`Assets/_Game/Scripts/Defense/SingleCityDefenseSettlement.cs`。怎么复用：终局 revision 变化时发布一次结算；胜利允许继续沙盒或返回标题，失败允许最近波前重试或返回标题，缺少生产合格时间时明确显示无数据，迁移统计明确标为部分统计。不能负责什么：不推进战役、不修改会话统计、不读写重试档、不持有 Unity UI 或输入焦点；人工试玩确认前只能标记为已实现待验证。改后跑哪组测试：`SingleCityDefenseSettlementTests`、`GrayboxDefenseSettlementUi3DTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`。代码名：`SingleCityDefenseSettlementMetric`、`SingleCityDefenseSettlementSessionStatistics`、`SingleCityDefenseSettlementSnapshot`、`SingleCityDefenseSettlementModel`。

### 三维建筑战斗生命运行时（复用前审查）

能解决什么：按稳定建筑实例维护正式最大生命、当前生命与战斗摧毁边界，并从建筑会话同步新建、施工、完成、遗迹和战损废墟状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingHealthRuntime3D.cs`。怎么复用：按稳定建筑实例维护正式最大生命、当前生命与战斗摧毁边界，并从建筑会话同步新建、施工、完成、遗迹和战损废墟状态。不能负责什么：只拥有当前三维会话的建筑生命真值；不决定敌人目标、伤害数值、建筑移除、库存损失、生产状态、撤离规则或视觉样式。摧毁后的跨域提交必须交给统一战斗摧毁协调器。改后跑哪组测试：`GrayboxBuildingHealthRuntime3DTests`、`GrayboxBuildingCombatLifecycleTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`。代码名：`GrayboxBuildingHealthRuntime3D`。

### 三维建筑战损原子结算协调器（复用前审查）

能解决什么：以稳定建筑实例和结算序号协调建筑生命、内部生产或塔库存损失、城市库存转移、会话战损废墟转换与派生状态刷新，重复结算保持幂等。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCombatDestructionCoordinator3D.cs`。怎么复用：以稳定建筑实例和结算序号协调建筑生命、内部生产或塔库存损失、城市库存转移、会话战损废墟转换与派生状态刷新，重复结算保持幂等。不能负责什么：只协调一次已确认的建筑摧毁事务；不计算攻击命中、伤害、索敌、掉落或退款，不绕过 CityResourceStorageModel、生产运行时、塔运行时和建筑会话各自的预检与提交边界。失败结果不得伪装成已摧毁。改后跑哪组测试：`GrayboxCombatDestructionCoordinator3DTests`、`CityResourceStorageCombatLossTests`、`GrayboxProductionCombatLossTests`、`GrayboxDefenseTowerCombatLossTests`。代码名：`GrayboxCombatDestructionResult3D`、`GrayboxCombatDestructionCoordinator3D`。

### 首版防御战斗模型（推荐复用）

能解决什么：用于以确定性规则时间处理机枪塔索敌射击、啃噬者生命与城市核心受击，并通过受验证的持久化状态保留塔弹药、暂停、弹药租约与伤害余量，以及活动敌人的生命、位置和攻击余量。在哪里：`Assets/_Game/Scripts/Defense/FirstDefenseCombatModels.cs`。怎么复用：用于以确定性规则时间处理机枪塔索敌射击、啃噬者生命与城市核心受击，并通过受验证的持久化状态保留塔弹药、暂停、弹药租约与伤害余量，以及活动敌人的生命、位置和攻击余量。不能负责什么：只拥有首版战斗实体规则与实体级持久化状态；不定义 schema DTO、不执行文件 IO、不推进教学波、不访问城市库存，tracer、目标和状态文案不持有命中、伤害或耗弹真值。改后跑哪组测试：`FirstDefenseLoopTests`、`GrayboxFormalSaveDefenseTests`。代码名：`MachineGunTurretPersistenceState`、`MachineGunTurretCombatModel`、`DefenseEnemyPersistenceState`、`DefenseEnemyCombatModel`、`CityCoreCombatModel`。

### 首版教学防御波运行时（推荐复用）

能解决什么：用于推进十五秒预警、八只啃噬者四十秒分批生成、直达核心和核心受击，并通过受验证的持久化状态保留触发与波次阶段、警告和生成时钟、计数高水位、固定步余量、冻结出生点、活动敌人与核心生命。在哪里：`Assets/_Game/Scripts/Defense/FirstDefenseWaveRuntime.cs`。怎么复用：用于推进十五秒预警、八只啃噬者四十秒分批生成、直达核心和核心受击，并通过受验证的持久化状态保留触发与波次阶段、警告和生成时钟、计数高水位、固定步余量、冻结出生点、活动敌人与核心生命。不能负责什么：只拥有首个教学波与城市核心会话状态；不处理建筑发现、塔状态、配置签名、schema DTO、城市库存、Unity 时间、表现对象或正式失败结算。改后跑哪组测试：`FirstDefenseWaveRuntimeTests`、`GrayboxFormalSaveDefenseTests`。代码名：`DefenseEnemyRuntimeSnapshot`、`DefenseRuntimeSnapshot`、`TutorialDefensePersistenceState`、`TutorialDefenseRuntimeModel`。

### 三维首版防御运行时（复用前审查）

能解决什么：按稳定建筑实例同步三类正式塔、塔内耗材、十波战役、活动敌人与已结算攻击事件，并组合建筑生命和原子摧毁边界，按废墟稳定 ID 保留当前运行时会话的实际战损摘要；持久化时确定性保留固定步余量与全部既有防御领域状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`。怎么复用：按稳定建筑实例同步三类正式塔、塔内耗材、十波战役、活动敌人与已结算攻击事件，并组合建筑生命和原子摧毁边界，按废墟稳定 ID 保留当前运行时会话的实际战损摘要；持久化时确定性保留固定步余量与全部既有防御领域状态。不能负责什么：只桥接正式 3D 建筑会话、城市仓储与纯战役模型，并拥有防御运行时事务；不执行文件 IO 或场景搜索，不复制物流范围、建筑资格、库存事务或表现状态。攻击事件只发布已结算事实，表现层不得借此成为第二套战斗真值；废墟损失摘要不进入现有 schema 32，存档恢复后必须明确显示明细不可用。改后跑哪组测试：`GrayboxEvacuationTests`、`GrayboxFirstDefenseRuntimeTests`、`GrayboxDefenseSnapshotStabilityTests`、`GrayboxFormalSaveDefenseTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests`、`GrayboxDefenseSettledAttackPresentationTests`。代码名：`GrayboxDefenseEvacuationPayload3D`、`GrayboxDefenseTowerRuntimeState3D`、`GrayboxDefenseTowerSnapshot3D`、`GrayboxDefenseEnemySnapshot3D`、`GrayboxDefenseSettledAttackEvent3D`、`GrayboxDefenseRuntimeSnapshot3D`、`GrayboxDefensePersistenceState3D`、`GrayboxDefenseRestorePlan3D`、`GrayboxDefenseRuntime3D`。

### 人口模型（推荐复用）

能解决什么：用于管理人口容量。在哪里：`Assets/_Game/Scripts/Population/PopulationModel.cs`。怎么复用：用于管理人口容量。不能负责什么：不控制人口表现。改后跑哪组测试：`PopulationAndCapacityTests`。代码名：`PopulationModel`。

### 正式存档数据（复用前审查）

能解决什么：保留 legacy 2D schema 1–30 的历史字段形状，供统一 codec 解码、验证历史档并执行固定夹具兼容回归。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveData.cs`。怎么复用：保留 legacy 2D schema 1–30 的历史字段形状，供统一 codec 解码、验证历史档并执行固定夹具兼容回归。不能负责什么：这是只读兼容身份，不是现役运行时存档所有者，不承载 schema 31 正式 3D payload，不得作为新 3D 功能起点，也不提供 2D 到 3D 的正式迁移；任何字段变更仍需兼容性评审。改后跑哪组测试：`FormalSaveTests`。代码名：`FormalSaveData`。

### 历史二维存档兼容 DTO（禁止用于新功能）

能解决什么：保留 schema 1–30 历史存档中建筑、敌人和友军快照的原命名空间与 public 字段形状，供旧档解码和验证。在哪里：`Assets/_Game/Scripts/Persistence/Legacy2D/BuildingSnapshot.cs`、`Assets/_Game/Scripts/Persistence/Legacy2D/EnemySnapshot.cs`、`Assets/_Game/Scripts/Persistence/Legacy2D/FriendlyUnitSnapshot.cs`。怎么复用：保留 schema 1–30 历史存档中建筑、敌人和友军快照的原命名空间与 public 字段形状，供旧档解码和验证。不能负责什么：只用于历史格式兼容，不是现役 3D 领域状态，不得新增玩法字段、运行时控制器或 schema 31 payload；变更必须保持 schema 1–30 固定夹具可读。改后跑哪组测试：`FormalSaveTests`、`FormalSaveValidatorTests`。代码名：`BuildingSnapshot`、`EnemySnapshot`、`FriendlyUnitSnapshot`。

### 回溯锚点纯规则（复用前审查）

能解决什么：保留历史回溯锚点读取后观测值增加并封顶的确定性纯规则。在哪里：`Assets/_Game/Scripts/Legacy/RewindAnchorRules.cs`。怎么复用：保留历史回溯锚点读取后观测值增加并封顶的确定性纯规则。不能负责什么：不拥有输入、场景控制器、存档捕获或恢复应用，只允许作为历史规则兼容边界复用。改后跑哪组测试：`FormalSaveTests`。代码名：`RewindAnchorRules`。

### 正式三维存档信封、编码与语义验证（复用前审查）

能解决什么：以统一信封区分 legacy 2D schema 1 至 30、正式 3D schema 31、32、33、34、当前 schema 35 和未来版本，提供确定性编码、逐版本历史 payload hash、结构与跨引用验证，以及 31→32→33→34→35 单向迁移。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveEnvelope.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveCodec.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveValidator.cs`、`Assets/_Game/Scripts/Persistence/ThreeD/FormalThreeDSaveData.cs`。怎么复用：以统一信封区分 legacy 2D schema 1 至 30、正式 3D schema 31、32、33、34、当前 schema 35 和未来版本，提供确定性编码、逐版本历史 payload hash、结构与跨引用验证，以及 31→32→33→34→35 单向迁移。不能负责什么：只定义存档身份、DTO 信封、codec、纯验证与已批准迁移，不执行文件 IO、领域捕获、恢复应用、派生状态重建或 UI；legacy 2D 不升级为正式 3D，历史迁移不反推关注度、军队、多城、角色政治或高级科技战斗历史，34→35 只建立空高级状态。改后跑哪组测试：`FormalSaveEnvelopeTests`、`FormalSaveValidatorTests`、`FormalSaveSchema32ContractTests`、`FormalSaveDestroyedRuinSchema32Tests`、`FormalSaveSchema33ContractTests`、`FormalSaveSchema33MigrationTests`、`FormalSaveSchema34ContractTests`、`FormalSaveSchema35ContractTests`、`IDEA0027ResearchEffectStateSaveAdapterTests`、`GrayboxFormalDefenseCampaignSaveAdapterTests`。代码名：`FormalSaveCheckpointMetadata`、`FormalSaveEnvelope`、`FormalSaveDecodeResult`、`FormalSaveCodec`、`FormalSaveValidationResult`、`FormalSaveValidator`、`FormalThreeDProgressionSaveData`、`FormalThreeDAttentionSaveData`、`FormalThreeDFateSaveData`、`FormalThreeDCivilizationSaveData`、`FormalThreeDWorldSaveData`、`FormalThreeDBuildingsSaveData`、`FormalThreeDStorageSaveData`、`FormalThreeDWarehouseSaveData`、`FormalThreeDBackpackSaveData`、`FormalThreeDCraftingSaveData`、`FormalThreeDCraftingExecutionSaveData`、`FormalThreeDResearchSaveData`、`FormalThreeDResearchEffectStateSaveData`、`FormalThreeDProductionSaveData`、`FormalThreeDProductionStateSaveData`、`FormalThreeDDefenseSaveData`、`FormalThreeDDefenseCampaignSaveData`、`FormalThreeDDefenseCampaignStatisticsSaveData`、`FormalThreeDEvacuationSaveData`、`FormalThreeDEvacuationRuntimePayloadSaveData`。

### 正式单槽存档与文件事务（复用前审查）

能解决什么：通过统一单槽 formal-world.json、有效主档与 .bak 回退、同目录临时文件复读验证和原子替换提交正式 3D 存档，并把旧 2D、未来 schema、损坏和磁盘故障映射为稳定结构化结果。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveFileTransaction.cs`、`Assets/_Game/Scripts/Persistence/FormalSaveStore.cs`。怎么复用：通过统一单槽 formal-world.json、有效主档与 .bak 回退、同目录临时文件复读验证和原子替换提交正式 3D 存档，并把旧 2D、未来 schema、损坏和磁盘故障映射为稳定结构化结果。不能负责什么：只拥有路径、时间戳、编码后的字节和文件事务；不捕获或应用领域状态，不决定自动检查点，不把 legacy 2D 直接当作 schema 31，也不向 UI 硬编码玩法文案。改后跑哪组测试：`FormalSaveFileTransactionTests`、`GrayboxFormalSaveRuntimeInputTests`。代码名：`FormalSaveFileTransactionResult`、`FormalSaveFileTransaction`、`SystemFormalSaveFileSystem`、`FormalSaveStoreResult`、`FormalSaveStore`。

### 正式最近波前内部重试档（复用前审查）

能解决什么：正式运行时在受控波前边界保存内部快照，失败结算只通过结构化结果加载并恢复最近有效波前；损坏、缺失或不兼容时返回稳定失败而不破坏玩家主档。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveWaveRetryStore.cs`。怎么复用：正式运行时在受控波前边界保存内部快照，失败结算只通过结构化结果加载并恢复最近有效波前；损坏、缺失或不兼容时返回稳定失败而不破坏玩家主档。不能负责什么：不是第二个玩家可见存档槽，不接受 schema 31 或 legacy 2D，不决定何时可重试、不生成结算文案，也不绕过领域协调器。改后跑哪组测试：`FormalSaveWaveRetryStoreTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`。代码名：`FormalSaveWaveRetryStoreResult`、`FormalSaveWaveRetryStore`。

### 正式三维自动检查点策略（复用前审查）

能解决什么：按稳定原因与事件键合并自动检查点，区分一次性里程碑与命轨选择、锚点创建/读取/清理等可重复转换事件，维护 sequence、失败保留和明确 Flush 边界，并允许恢复检查点基线。在哪里：`Assets/_Game/Scripts/Persistence/FormalSaveCheckpointPolicy.cs`。怎么复用：按稳定原因与事件键合并自动检查点，区分一次性里程碑与命轨选择、锚点创建/读取/清理等可重复转换事件，维护 sequence、失败保留和明确 Flush 边界，并允许恢复检查点基线。不能负责什么：只拥有检查点意图与历史高水位，不自行 capture、写盘、重试每帧或决定玩家文案；保存回调、规则时间和事件订阅必须由正式运行时显式注入。改后跑哪组测试：`GrayboxFormalSaveCheckpointTests`、`GrayboxFormalSaveRuntimeHostTests`。代码名：`FormalSaveCheckpointPolicy`。

### 三维世界与移动城市存档适配器（复用前审查）

能解决什么：捕获并恢复 schema 32 的 v2 世界身份、节点余量、城市位置、模式、活动转换和规则时间，并明确拒绝 v1 world identity。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxWorldCitySaveAdapter3D.cs`。怎么复用：捕获并恢复 schema 32 的 v2 世界身份、节点余量、城市位置、模式、活动转换和规则时间，并明确拒绝 v1 world identity。不能负责什么：只映射世界与城市权威状态，不拥有文件 IO、领域总协调、建筑/库存/生产真值、路径缓存、物流连接、表现或 UI；不猜测迁移旧地图坐标。改后跑哪组测试：`GrayboxFormalSaveWorldCityTests`。代码名：`GrayboxWorldCitySaveAdapter3D`。

### 正式三维存档领域协调器（复用前审查）

能解决什么：按 world/city、building/storage、economy、production、defense、researchEffectState、progression、evacuation、civilizationExpansion、pause 固定依赖顺序协调 schema 35 捕获与恢复；先完整验证，再事务式应用，失败时回滚十个权威领域并在成功提交后统一重建派生状态。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs`。怎么复用：按 world/city、building/storage、economy、production、defense、researchEffectState、progression、evacuation、civilizationExpansion、pause 固定依赖顺序协调 schema 35 捕获与恢复；先完整验证，再事务式应用，失败时回滚十个权威领域并在成功提交后统一重建派生状态。不能负责什么：只协调显式注入的领域和派生重建，不搜索场景、不执行文件 IO、不复制领域规则，也不把连接、路径、临时表现或 UI 入档；研究效果状态是独立事务域，回滚失败会保留安全屏障，调用方不得伪装成成功。改后跑哪组测试：`GrayboxFormalSaveCoordinatorTests`、`GrayboxFormalSaveCheckpointTests`、`GrayboxFormalProgressionSaveAdapterTests`、`GrayboxBuildAndPerformanceTests`、`FormalSaveSchema34ContractTests`、`FormalSaveSchema35ContractTests`。代码名：`GrayboxFormalControllerRebuilder3D`、`GrayboxFormalPauseSaveDomain3D`、`GrayboxFormalSaveCoordinatorResult3D`、`GrayboxFormalSaveCoordinator3D`。

### 正式三维存档运行时主机（仅限场景）

能解决什么：在 GrayboxPrototype3D 组合 schema 35 十领域 coordinator、Attention/Fate/Civilization、文明扩展与高级科技状态唯一 owner、规则效率、UGUI/真实 U/M/N/P/0、检查点与保存恢复。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveRuntimeHost3D.cs`。怎么复用：在 GrayboxPrototype3D 组合 schema 35 十领域 coordinator、Attention/Fate/Civilization、文明扩展与高级科技状态唯一 owner、规则效率、UGUI/真实 U/M/N/P/0、检查点与保存恢复。不能负责什么：只作为正式 3D 场景组合根；资格从 Research/Building/Pressure/Production 权威快照采集，UI 只发命令。它不用于冻结 2D、不拥有 DTO 或战斗规则；主战役与压力战役状态隔离，内部重试档和回溯锚点都不是第二玩家存档槽。改后跑哪组测试：`GrayboxFormalSaveRuntimeHostTests`、`GrayboxFormalProgressionSaveAdapterTests`、`GrayboxCivilizationAdvancementControllerTests`、`GrayboxCivilizationAdvancementPresentationTests`、`GrayboxCivilizationAdvancementInputCoordinatorTests`、`FormalSaveWaveRetryStoreTests`、`GrayboxFormalSaveRuntimeInputTests`、`GrayboxFormalSaveRoundTripTests`、`GrayboxCivilizationAdvancementRuntimeInputTests`、`GrayboxCivilizationExpansionRuntimeInputTests`。代码名：`GrayboxFormalSaveRuntimeHost3D`。

### 正式三维存档启动与退出入口（仅限场景）

能解决什么：把正式 store/coordinator/runtime host 的结构化结果映射为启动页继续、新游戏覆盖确认、自动存档警告、保存退出、最近波前重试和返回标题反馈，并通过既有系统菜单与输入协调器阻断未进入游戏时的世界输入。在哪里：`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxFormalSaveEntryController3D.cs`。怎么复用：把正式 store/coordinator/runtime host 的结构化结果映射为启动页继续、新游戏覆盖确认、自动存档警告、保存退出、最近波前重试和返回标题反馈，并通过既有系统菜单与输入协调器阻断未进入游戏时的世界输入。不能负责什么：只拥有 GrayboxPrototype3D 的玩家入口状态与中文反馈，不读写文件、不持有 schema DTO、不绕过 runtime host，也不接入冻结 2D；继续、覆盖、重试与退出必须经过真实 UGUI 输入主循环验证。改后跑哪组测试：`GrayboxFormalSaveUiAndInputTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxDefenseSettlementRuntimeIntegrationTests`、`GrayboxFormalSaveRuntimeInputTests`、`GrayboxFormalSaveRoundTripTests`。代码名：`GrayboxFormalSaveEntryController3D`。

### 三维正式建筑原位升级命令（复用前审查）

能解决什么：在文明 Lv.2、对应研究完成且城市网络材料充足时，把机枪塔原位升级为重机枪塔，或把剑阵塔原位升级为御剑台；Host 把缓存的可用性投影接到 Defense 选中建筑 HUD，真实按钮只发布稳定实例升级命令并显示反馈。在哪里：`Assets/_Game/Scripts/Building/BuildingUpgradeModel.cs`、`Assets/_Game/Scripts/Building/BuildingGrid.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingUpgradeController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveRuntimeHost3D.cs`。怎么复用：在文明 Lv.2、对应研究完成且城市网络材料充足时，把机枪塔原位升级为重机枪塔，或把剑阵塔原位升级为御剑台；Host 把缓存的可用性投影接到 Defense 选中建筑 HUD，真实按钮只发布稳定实例升级命令并显示反馈。不能负责什么：Controller/Session 保持稳定实例、占格、站点、朝向、完成状态和建筑数量，并在失败时零写或完整回滚；HUD 已接且不重算规则。两项科技在基础目录仍为 PreviewOnly，仅由文明 Lv.2 白名单投影自然开放；不得在 Lv.1 提前开放。改后跑哪组测试：`BuildingUpgradeTests`、`GrayboxBuildingUpgradeControllerTests`、`GrayboxDefensePresentationTests`、`FormalResearchCatalogTests`、`ResearchTests`、`GrayboxDefenseRuntimeInputTests`。代码名：`BuildingUpgradeDefinition`、`BuildingUpgradeCatalog`、`GrayboxBuildingUpgradeAvailability3D`、`GrayboxBuildingUpgradeResult3D`、`GrayboxBuildingUpgradeController3D`、`GrayboxDefenseHudView3D`、`GrayboxDefenseController3D`。

### 三维开发修改器文明进程命令边界（复用前审查）

能解决什么：在 Editor/Development 许可内，以 24 个稳定中文动作查询 Attention、Fate、Pressure、Boss、锚点和首次文明升阶，并把玩家同源命令与明确测试夹具委托给唯一 Host/领域 owner；Bootstrap 同时提供列表、中文搜索与数值/资源/阈值/锚点参数。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperProgressionFacade3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifier3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperCatalogQuery3D.cs`。怎么复用：在 Editor/Development 许可内，以 24 个稳定中文动作查询 Attention、Fate、Pressure、Boss、锚点和首次文明升阶，并把玩家同源命令与明确测试夹具委托给唯一 Host/领域 owner；Bootstrap 同时提供列表、中文搜索与数值/资源/阈值/锚点参数。不能负责什么：只作为开发诊断入口；查询、失败和无变化不得标记修改器使用，真实改变才单向标记。强制命轨选择前仅 dev-only 真实 0 可置顶打开，系统菜单仍最高；Release 不包含 Facade、命令或面板行为。改后跑哪组测试：`GrayboxDeveloperProgressionCommandTests`、`GrayboxDeveloperModifierCatalogTests`、`GrayboxDeveloperModifierTests`、`GrayboxDeveloperModifierRuntimeInputTests`。代码名：`GrayboxDeveloperProgressionQuery3D`、`GrayboxDeveloperProgressionFacade3D`、`GrayboxDeveloperModifier3D`、`GrayboxDeveloperModifierBootstrap3D`。

### 首次文明升阶纯规则运行时（复用前审查）

能解决什么：校验遗产解析完成、至少两座已完成玩家机枪塔、晶壳母体已击败和当前生产运行四项条件，并以绑定 owner、预期快照和单次消费计划提交首次文明 1→2、命轨 1→2 命令；升阶本身无额外资源费。在哪里：`Assets/_Game/Scripts/Progression/FormalCivilizationAscension.cs`。怎么复用：校验遗产解析完成、至少两座已完成玩家机枪塔、晶壳母体已击败和当前生产运行四项条件，并以绑定 owner、预期快照和单次消费计划提交首次文明 1→2、命轨 1→2 命令；升阶本身无额外资源费。不能负责什么：只拥有升阶条件投影、等级快照和事务命令；不自行查询研究、塔、Boss 或生产真值，不直接晋级 FormalFateRuntime 或三条效果运行时，也不提交关注度 +25、检查点、Host、UI、动画或存档。调用方必须将这些跨 owner 操作做成可回滚的统一事务。改后跑哪组测试：`FormalCivilizationAscensionRuntimeTests`。代码名：`FormalCivilizationAscensionRequirements`、`FormalCivilizationAscensionRequirementStatus`、`FormalCivilizationAscensionPlan`、`FormalCivilizationAscensionCommand`、`FormalCivilizationAscensionSnapshot`、`FormalCivilizationAscensionRuntime`。

### 三维文明升阶原子协调与演出序列（复用前审查）

能解决什么：在四项资格通过后，把 Civilization、Fate、Attention 与所选 Lv.2 效果作为可回滚的单次事务提交，并用缓存不可变快照推进 Scanning 2.5 秒、Confirmed 3 秒、Warning 4 秒、Results 与 Continued。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationAdvancementController3D.cs`、`Assets/_Game/Scripts/Progression/AdvancementSequenceModel.cs`。怎么复用：在四项资格通过后，把 Civilization、Fate、Attention 与所选 Lv.2 效果作为可回滚的单次事务提交，并用缓存不可变快照推进 Scanning 2.5 秒、Confirmed 3 秒、Warning 4 秒、Results 与 Continued。不能负责什么：协调器不自行查询研究、塔、Boss 或生产，Sequence 不提交奖励或保存文件；权威资格采集、检查点、规则时间、暂停、UI 和 schema 往返由 Host/Adapter 组合。建筑原位升级由独立 UpgradeController/Session 命令负责；对应科技仍为 PreviewOnly，不能从本运行时推断自然玩家升级入口已开放。改后跑哪组测试：`GrayboxCivilizationAdvancementControllerTests`、`FormalProgressionTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxFormalProgressionSaveAdapterTests`、`GrayboxCivilizationAdvancementRuntimeInputTests`。代码名：`GrayboxCivilizationAdvancementResult3D`、`GrayboxCivilizationAdvancementController3D`、`AdvancementSequenceSnapshot`、`AdvancementSequenceModel`。

### 三维文明升阶清单、演出与真实输入界面（仅限场景）

能解决什么：读取 Requirements/Civilization/Fate/Sequence 不可变状态，并直接消费 Civilization 的 CanPrepareAscension、目标文明/命轨等级、Attention reason/reward 投影，显示四项清单与四阶段结果；真实 U 仍路由到 Host。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationAdvancementPresentationController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationAdvancementView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`。怎么复用：读取 Requirements/Civilization/Fate/Sequence 不可变状态，并直接消费 Civilization 的 CanPrepareAscension、目标文明/命轨等级、Attention reason/reward 投影，显示四项清单与四阶段结果；真实 U 仍路由到 Host。不能负责什么：View/Presenter 不复制文明 1→2、命轨 1→2 或 +25 等领域常量，只发布 AdvanceRequested/ContinueRequested；输入协调器服从正式模态优先级。场景接线仅适用于 GrayboxPrototype3D。改后跑哪组测试：`GrayboxCivilizationAdvancementPresentationTests`、`FormalFatePerformanceTests`、`GrayboxCivilizationAdvancementInputCoordinatorTests`、`GrayboxSceneContractTests`、`GrayboxCivilizationAdvancementRuntimeInputTests`。代码名：`GrayboxCivilizationAdvancementPresentation3D`、`GrayboxCivilizationAdvancementPresentationController3D`、`GrayboxCivilizationAdvancementView3D`。

## 3D 表现与美术

### 共享视觉定义库（复用前审查）

能解决什么：用于按稳定内容 ID 保存和查询可替换的共享视觉定义。在哪里：`Assets/_Game/Scripts/Presentation/VisualLibrary.cs`、`Assets/_Game/Scripts/Presentation/VisualDefinition.cs`。怎么复用：用于按稳定内容 ID 保存和查询可替换的共享视觉定义。不能负责什么：只拥有视觉定义与目录查询，不创建场景槽位、运行时 Provider 或第二套玩法身份；正式 3D 表现应通过自身展示适配器消费该边界。改后跑哪组测试：`VisualSlotTests`。代码名：`VisualLibrary`、`VisualDefinition`。

### 三维共享资源图标目录（推荐复用）

能解决什么：为全部正式资源提供稳定 Sprite 解析、可替换资产覆盖和确定性占位图标，供矿点、资源栏、仓库、背包、配方、科技与生产 UI 共享。在哪里：`Assets/_Game/Scripts/Graybox3D/ResourceIconCatalog3D.cs`。怎么复用：为全部正式资源提供稳定 Sprite 解析、可替换资产覆盖和确定性占位图标，供矿点、资源栏、仓库、背包、配方、科技与生产 UI 共享。不能负责什么：只负责资源 ID 到图标的表现映射，不拥有资源定义、数量或矿点真值；消费者必须使用同一目录资产或确定性 fallback，不得各自生成第二套资源身份和颜色语义。改后跑哪组测试：`GrayboxVisualAndWorldTests`、`GrayboxSceneContractTests`、`Production2DItemIconPipelineTests`。代码名：`ResourceIconCatalog3D`。

### 三维共享建筑图标目录（推荐复用）

能解决什么：按 BuildingCatalog 稳定建筑 ID 解析 35 张正式建筑 Sprite 和确定性类别回退，供建造栏、建筑详情、世界 billboard 与撤离列表共享。在哪里：`Assets/_Game/Scripts/Graybox3D/BuildingIconCatalog3D.cs`。怎么复用：按 BuildingCatalog 稳定建筑 ID 解析 35 张正式建筑 Sprite 和确定性类别回退，供建造栏、建筑详情、世界 billboard 与撤离列表共享。不能负责什么：只拥有建筑 ID 到 Sprite 的表现映射；不决定建筑可见性、解锁、成本、占地或放置合法性，不把二维图写成三维模型完成。新消费者不得维护平行建筑图标字典。改后跑哪组测试：`Production2DBuildingIconPipelineTests`、`GrayboxBuildingRuntimeSceneTests`。代码名：`BuildingIconOverride3D`、`BuildingIconCatalog3D`。

### 三维共享科技图标目录（推荐复用）

能解决什么：按 ResearchCatalog 的 44 个稳定科技 ID 解析正式科技 Sprite，并提供路线/层级确定性回退，供科技树节点与详情共享。在哪里：`Assets/_Game/Scripts/Graybox3D/ResearchIconCatalog3D.cs`。怎么复用：按 ResearchCatalog 的 44 个稳定科技 ID 解析正式科技 Sprite，并提供路线/层级确定性回退，供科技树节点与详情共享。不能负责什么：只拥有科技 ID 到 Sprite 的表现映射；不决定研究前置、成本、状态、布局或效果是否已接入。缺图回退不能授予科技效果，UI 不得用文件名建立第二套目录。改后跑哪组测试：`Production2DTechnologyIconPipelineTests`、`ResearchTreeUiContractTests`、`GrayboxProductionObservabilityRuntimeInputTests`。代码名：`ResearchIconOverride3D`、`ResearchIconCatalog3D`。

### 统一二维视觉目录（推荐复用）

能解决什么：用稳定三元组 visualClass/contentId/variant 统一解析 141 个视觉条目、33 条配方视觉投影及每张图的归一化 Alpha 主体边界；尺度策略按物品/科技/建筑/人物/单位/世界标记语义把不同透明留白归一为一致可见主体占比。在哪里：`Assets/_Game/Scripts/Graybox3D/Production2DVisualCatalog3D.cs`、`Assets/_Game/Scripts/Graybox3D/Production2DVisualScalePolicy3D.cs`。怎么复用：用稳定三元组 visualClass/contentId/variant 统一解析 141 个视觉条目、33 条配方视觉投影及每张图的归一化 Alpha 主体边界；尺度策略按物品/科技/建筑/人物/单位/世界标记语义把不同透明留白归一为一致可见主体占比。不能负责什么：运行时资产只保存稳定视觉键、Sprite、可见边界、配方视觉投影和类别回退；尺度策略只修正表现，不拥有资源数量、配方数值、科技状态、建筑规则或 UI 布局。UI 面板/边框不按内容图标裁切，未知键只能安全回退。改后跑哪组测试：`IDEA0024ResearchTreePresentationTests`、`IDEA0025Production2DVisualScaleTests`、`Production2DVisualCatalogAtlasTests`、`Production2DCivilizationExpansionVisualPipelineTests`、`GrayboxCivilizationExpansionVisualIntegrationTests`。代码名：`Production2DVisualEntry3D`、`Production2DRecipeVisualEntry3D`、`Production2DVisualCatalog3D`、`Production2DVisualFraming3D`、`Production2DVisualScalePolicy3D`。

### 三维资源矿点标识与图标标记（仅限场景）

能解决什么：在 GrayboxPrototype3D 中以世界坐标生成稳定矿点 ID，并把 WorldMapModel 的真实资源节点投影为复用共享资源图标的可回收标记。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeIdentity3D.cs`、`Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeMarker3D.cs`。怎么复用：在 GrayboxPrototype3D 中以世界坐标生成稳定矿点 ID，并把 WorldMapModel 的真实资源节点投影为复用共享资源图标的可回收标记。不能负责什么：只属于当前 3D 世界表现与对象复用层；不创建资源节点、不决定节点类型、储量、采矿合法性或枯竭规则，所有真值必须继续来自 WorldMapModel。改后跑哪组测试：`GrayboxVisualAndWorldTests`。代码名：`GrayboxResourceNodeIdentity3D`、`GrayboxResourceNodeMarker3D`。

### 正式三维地图导航与矿点层级（复用前审查）

能解决什么：统一正式三维地图的正交缩放边界和滚轮步长，并向世界表现提供当前观察距离。在哪里：`Assets/_Game/Scripts/Graybox3D/FormalMapNavigationProfile3D.cs`。怎么复用：统一正式三维地图的正交缩放边界和滚轮步长，并向世界表现提供当前观察距离。不能负责什么：只拥有导航配置；矿点的物理像素尺寸和实际 Near/Mid/Far 显隐由正式世界表现尺度配置负责。它不修改地图、节点、储量或输入焦点真值，模态抑制必须继续经过正式输入协调器。改后跑哪组测试：`GrayboxCameraAndInputTests`、`GrayboxVisualAndWorldTests`、`GrayboxRuntimeSceneTests`。代码名：`FormalMapNavigationProfile3D`。

### 正式三维世界表现尺度（复用前审查）

能解决什么：按 BuildingDefinition.Id 解析 35 项表现 archetype，并以唯一 8×6、锚点 (-4,-3)、单格 1×1 的内城策略统一地面/内城格尺度、旋转投影、移动城视觉体量与建筑屋顶图标净空。在哪里：`Assets/_Game/Scripts/Graybox3D/FormalWorldPresentationScaleProfile3D.cs`、`Assets/_Game/Scripts/Graybox3D/FormalInnerCityPresentationPolicy3D.cs`。怎么复用：按 BuildingDefinition.Id 解析 35 项表现 archetype，并以唯一 8×6、锚点 (-4,-3)、单格 1×1 的内城策略统一地面/内城格尺度、旋转投影、移动城视觉体量与建筑屋顶图标净空。不能负责什么：只拥有世界表现参数、内城坐标与纯换算；不得修改 BuildingCatalog 占地、放置合法性、移动城玩法碰撞体、资源节点、地图、Sprite 身份、相机输入或存档真值。改后跑哪组测试：`IDEA0024CityBuildingPresentationTests`、`FormalWorldPresentationScaleProfile3DTests`、`GrayboxBuildingProjectionAndViewTests`、`GrayboxVisualAndWorldTests`、`GrayboxSceneContractTests`。代码名：`FormalWorldPresentationScaleProfile3D`、`FormalWorldPresentationScalePolicy3D`、`FormalInnerCityPresentationPolicy3D`、`FormalBuildingVisualMetrics3D`、`FormalWorldMarkerMetrics3D`。

### 正式内城表现坐标策略（复用前审查）

能解决什么：以唯一 `8×6`、锚点 `(-4,-3)`、单格 `1×1` 的策略计算内城单格/占地中心、旋转投影和边界，并让内城格与外部地面格保持 1:1。在哪里：`Assets/_Game/Scripts/Graybox3D/FormalInnerCityPresentationPolicy3D.cs`、`Assets/_Game/Scripts/Graybox3D/FormalWorldPresentationScaleProfile3D.cs`。怎么复用：建筑世界视图、表面投影和场景 Authoring 共同消费同一策略与 Profile。不能负责什么：它只拥有表现坐标与尺寸，不改变 BuildingCatalog 占地、放置合法性、移动城玩法碰撞体、地图或存档。改后跑哪组测试：`IDEA0024CityBuildingPresentationTests`、`FormalWorldPresentationScaleProfile3DTests`、`GrayboxBuildingProjectionAndViewTests`、`GrayboxSceneContractTests`。代码名：`FormalInnerCityPresentationPolicy3D`、`FormalWorldPresentationScaleProfile3D`。

### 正式三维响应式界面布局（复用前审查）

能解决什么：统一 1920×1080 参考分辨率、Expand 缩放、安全区、语义槽位、间距、图标语义尺寸、大面板尺寸上限和运行时可刷新的 12 物理像素字体下限。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/FormalUiLayoutProfile3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/FormalUiLayoutPolicy3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/FormalUiCanvasConfiguration3D.cs`。怎么复用：统一 1920×1080 参考分辨率、Expand 缩放、安全区、语义槽位、间距、图标语义尺寸、大面板尺寸上限和运行时可刷新的 12 物理像素字体下限。不能负责什么：只负责 UGUI 画布与布局表现；布局以 Canvas 本地矩形为真值，字体下限才读取物理像素缩放；不拥有玩法数据、输入命令或窗口生命周期，既有建造栏继续保持 620×54 的批准尺寸与位置。改后跑哪组测试：`FormalUiLayoutPolicy3DTests`、`FormalUiResponsiveLayout3DTests`、`GrayboxSceneContractTests`、`GrayboxDeveloperModifierRuntimeInputTests`、`GrayboxDefenseRuntimeInputTests`。代码名：`FormalUiLayoutProfile3D`、`FormalUiLayoutPolicy3D`、`FormalUiLayout3D`、`FormalUiCanvasConfiguration3D`、`FormalUiCanvasMetrics3D`、`FormalUiReadableText3D`。

### 三维灰盒视觉槽位（仅限场景）

能解决什么：用于场景内灰盒视觉绑定。在哪里：`Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs`。怎么复用：用于场景内灰盒视觉绑定。不能负责什么：不作为二维槽位替代。改后跑哪组测试：`GrayboxVisualAndWorldTests`。代码名：`GrayboxVisualSlot`。

### 首版三维地形配置（复用前审查）

能解决什么：用于定义首版地形参数。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`。怎么复用：用于定义首版地形参数。不能负责什么：修改前需要导入策略复核。改后跑哪组测试：`FirstArtTerrainProfileTests`。代码名：`FirstArtTerrainProfile3D`。

### 文明式三维地形视觉样式（复用前审查）

能解决什么：冻结七类地形的制图式综合色、宏观变化强度和首轮原创建议色板，供资产生成器与 Shader 共同消费。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainVisualStyle3D.cs`。怎么复用：冻结七类地形的制图式综合色、宏观变化强度和首轮原创建议色板，供资产生成器与 Shader 共同消费。不能负责什么：只拥有地形表现参数，不改变 WorldMapModel、控制图、可通行性、资源节点或 Ruins/Cliff 几何；替换底色仍须运行 TerrainAssetDeep。改后跑哪组测试：`FirstArtTerrainVisualStyleTests`、`FirstArtTerrainShaderTests`。代码名：`FirstArtTerrainVisualStyleCatalog3D`。

### 首版三维地形渲染（仅限场景）

能解决什么：用于在场景渲染首版地形。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`。怎么复用：用于在场景渲染首版地形。不能负责什么：不管理地形资源导入。改后跑哪组测试：`FirstArtTerrainRendererTests`。代码名：`FirstArtTerrainRenderer3D`。

### 废墟与悬崖三维配置（复用前审查）

能解决什么：用于冻结废墟与悬崖稳定标识、模块语义和批准资源引用。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffCatalog3D.cs`、`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffProfile3D.cs`。怎么复用：用于冻结废墟与悬崖稳定标识、模块语义和批准资源引用。不能负责什么：不复制地形规则真值；修改目录、材质槽或 Prefab 绑定前需要资产合同复核。改后跑哪组测试：`FirstArtRuinsCliffAssetBuilderTests`、`FirstArtRuinsCliffCatalogProfileTests`。代码名：`FirstArtRuinsCliffCatalog3D`、`FirstArtRuinsCliffProfile3D`。

### 废墟与悬崖三维布局合批（复用前审查）

能解决什么：用于从既有世界地图确定性投影并合批废墟与悬崖几何。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffGeometry3D.cs`、`Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffLayout3D.cs`。怎么复用：用于从既有世界地图确定性投影并合批废墟与悬崖几何。不能负责什么：只消费既有地图与配置，不创建第二套地形判断或逐格常驻对象。改后跑哪组测试：`FirstArtRuinsCliffGeometryTests`、`FirstArtRuinsCliffLayoutTests`。代码名：`FirstArtRuinsCliffGeometry3D`、`FirstArtRuinsCliffLayout3D`。

### 废墟与悬崖三维呈现（仅限场景）

能解决什么：用于在正式三维地形 presenter 中呈现废墟与悬崖并保留分类回退。在哪里：`Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`。怎么复用：用于在正式三维地形 presenter 中呈现废墟与悬崖并保留分类回退。不能负责什么：必须复用唯一地形 presenter；不得增加第二个场景 owner 或绕过分类回退。改后跑哪组测试：`FirstArtRuinsCliffEvidenceCaptureTests`、`FirstArtRuinsCliffPresentationTests`、`FirstArtRuinsCliffSceneContractTests`。代码名：`FirstArtTerrainRenderer3D`。

## 场景、构建与检查工具

### 文明式地形四通道生成器（复用前审查）

能解决什么：从七份不可变概念源确定性重采样、平滑接缝并派生 BaseColor、Height、Normal、Mask，再事务式重建四个 Texture2DArray。在哪里：`Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs`。怎么复用：从七份不可变概念源确定性重采样、平滑接缝并派生 BaseColor、Height、Normal、Mask，再事务式重建四个 Texture2DArray。不能负责什么：仅供 Editor 生成已批准的正式地形表现；不得读取已生成 BaseColor 作为再次生成输入，不改变控制图、地图真值或玩法规则，改后必须运行 TerrainAssetDeep。改后跑哪组测试：`FirstArtTerrainVisualStyleTests`、`FirstArtTerrainAssetBuilderTests`。代码名：`FirstArtTerrainAssetBuilder`。

### 首版三维地形与界面固定证据捕获（复用前审查）

能解决什么：在唯一正式 3D 场景采集地图总览、地形交界、资源标记三档、主 HUD、科技树、动态水与十帧缩放轨迹，并写入固定分辨率、相机、LOD、UI 状态和 SHA-256 清单。在哪里：`Assets/_Game/Editor/FirstArtTerrainEvidenceCapture.cs`。怎么复用：在唯一正式 3D 场景采集地图总览、地形交界、资源标记三档、主 HUD、科技树、动态水与十帧缩放轨迹，并写入固定分辨率、相机、LOD、UI 状态和 SHA-256 清单。不能负责什么：仅用于 Editor 验证；必须恢复相机、Canvas、矿点 LOD 和面板状态，不修改地图或 UI 真值，也不把自动截图写成人工视觉验收。改后跑哪组测试：`FirstArtTerrainEvidenceCaptureTests`。代码名：`FirstArtTerrainEvidenceCapture`。

### 正式二维图类级导入规则（复用前审查）

能解决什么：只在 Assets/_Game/Art/Production2D 的七类批准目录内统一 Sprite、Alpha、sRGB、Clamp、无 mipmap、FullRect、Pivot 和 UI Border 导入合同；Unit importer 只接管 Units 根目录顶层四张透明 PNG。在哪里：`Assets/_Game/Editor/Production2DItemImportPolicy.cs`、`Assets/_Game/Editor/Production2DBuildingImportPolicy.cs`、`Assets/_Game/Editor/Production2DTechnologyImportPolicy.cs`、`Assets/_Game/Editor/Production2DUiCharacterMarkerImportPolicy.cs`、`Assets/_Game/Editor/Production2DUnitImportPolicy.cs`。怎么复用：只在 Assets/_Game/Art/Production2D 的七类批准目录内统一 Sprite、Alpha、sRGB、Clamp、无 mipmap、FullRect、Pivot 和 UI Border 导入合同；Unit importer 只接管 Units 根目录顶层四张透明 PNG。不能负责什么：只管理限定二维交付目录的 TextureImporter；不生成内容简介、中文名、运行时映射或视觉验收结论，绝不能扫描或修改地形及其它既有纹理。扩展目录前必须同步 manifest 和导入测试。改后跑哪组测试：`Production2DItemIconPipelineTests`、`Production2DBuildingIconPipelineTests`、`Production2DTechnologyIconPipelineTests`、`Production2DUiCharacterMarkerPipelineTests`、`Production2DCivilizationExpansionVisualPipelineTests`。代码名：`Production2DItemImportPolicy`、`Production2DBuildingImportPolicy`、`Production2DTechnologyImportPolicy`、`Production2DUiCharacterMarkerImportPolicy`、`Production2DUnitImportPolicy`。

### 正式二维目录与图集生成器（复用前审查）

能解决什么：从 IDEA-0016 基础 manifest、IDEA-0023 增量 manifest 与 IDEA-0024 科技树背景 manifest 按稳定 ID 生成统一 141 项视觉目录、33 条配方视觉投影和七类 SpriteAtlas；Units=4、Characters=3、WorldMarkers=5、UI=19，内容相同的重复构建保持资产与 meta 字节不变。在哪里：`Assets/_Game/Editor/Production2DItemIconCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DBuildingIconCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DTechnologyIconCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DVisualCatalogBuilder.cs`、`Assets/_Game/Editor/Production2DSpriteAtlasBuilder.cs`。怎么复用：从 IDEA-0016 基础 manifest、IDEA-0023 增量 manifest 与 IDEA-0024 科技树背景 manifest 按稳定 ID 生成统一 141 项视觉目录、33 条配方视觉投影和七类 SpriteAtlas；Units=4、Characters=3、WorldMarkers=5、UI=19，内容相同的重复构建保持资产与 meta 字节不变。不能负责什么：仅供 Editor 生成和验证已批准的表现资产；不从文件名猜玩法内容，不改场景、配方、研究或建筑规则，不替代 Alpha/缩小联络表与用户视觉验收。Atlas 打包顺序不构成运行时身份。改后跑哪组测试：`IDEA0024ResearchTreePresentationTests`、`Production2DItemIconPipelineTests`、`Production2DBuildingIconPipelineTests`、`Production2DTechnologyIconPipelineTests`、`Production2DUiCharacterMarkerPipelineTests`、`Production2DVisualCatalogAtlasTests`、`Production2DCivilizationExpansionVisualPipelineTests`。代码名：`Production2DItemIconCatalogBuilder`、`Production2DBuildingIconCatalogBuilder`、`Production2DTechnologyIconCatalogBuilder`、`Production2DVisualCatalogBuilder`、`Production2DSpriteAtlasBuilder`。

### 废墟与悬崖视觉证据捕获（复用前审查）

能解决什么：用于在批准的 GrayboxPrototype3D 场景中以固定 1280×720 视角自动采集 Ruins/Cliff 正常、单件和分类回退证据，并生成带资产 GUID、相机矩阵与 SHA-256 的清单。在哪里：`Assets/_Game/Editor/FirstArtRuinsCliffEvidenceCapture.cs`。怎么复用：用于在批准的 GrayboxPrototype3D 场景中以固定 1280×720 视角自动采集 Ruins/Cliff 正常、单件和分类回退证据，并生成带资产 GUID、相机矩阵与 SHA-256 的清单。不能负责什么：仅供编辑器验证；输出必须是项目外的空绝对目录，必须消费既有场景、profile 和唯一地形 presenter，不修改玩法真值，也不替代用户视觉验收；正式说明路径为 Docs/09-Reusable-Project-Catalog-ZH.md。改后跑哪组测试：`FirstArtRuinsCliffEvidenceCaptureTests`。代码名：`FirstArtRuinsCliffEvidenceCapture`。

### 灰盒场景编写（仅限场景）

能解决什么：用于编辑器生成正式灰盒场景，并保证文明升阶 View、Host、输入协调器及既有命轨/存档组件为稳定单实例引用。在哪里：`Assets/_Game/Editor/GrayboxSceneAuthoring.cs`。怎么复用：用于编辑器生成正式灰盒场景，并保证文明升阶 View、Host、输入协调器及既有命轨/存档组件为稳定单实例引用。不能负责什么：只在编辑器场景生成与修复时调用，不执行升阶领域命令、不保存运行时状态，也不替代 GrayboxSceneContractTests 的单实例和引用验证。改后跑哪组测试：`GrayboxSceneContractTests`。代码名：`GrayboxSceneAuthoring`。

### 文明扩展组合运行时（复用前审查）

能解决什么：组合唯一军队、远征、世界层、运输、角色、内政和外交 owner，并把四单位、三角色、次城、前哨、运输队、三个 M/N/P 页签和八个状态徽记投影到 UI 与有权威坐标的世界公告板；M/N/P 既支持快捷键，也支持同一面板内真实可点击页签。在哪里：`Assets/_Game/Scripts/CivilizationExpansion/CivilizationExpansionRuntime.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionController3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionVisualPresenter3D.cs`。怎么复用：组合唯一军队、远征、世界层、运输、角色、内政和外交 owner，并把四单位、三角色、次城、前哨、运输队、三个 M/N/P 页签和八个状态徽记投影到 UI 与有权威坐标的世界公告板；M/N/P 既支持快捷键，也支持同一面板内真实可点击页签。不能负责什么：不复制主城库存、地图、放置、敌人、单位位置或运输状态真值；页签只发布页面命令并复用同一面板，点击不得创建地图目标；表现从统一 141 项目录读取，Sprite 不等于正式 3D 建模。改后跑哪组测试：`IDEA0024AcceptanceAndClickableTabsTests`、`GrayboxCivilizationExpansionUiInputTests`、`Production2DCivilizationExpansionVisualPipelineTests`、`GrayboxCivilizationExpansionVisualIntegrationTests`、`GrayboxCivilizationExpansionRuntimeInputTests`、`IDEA0024AcceptanceAndTabsRuntimeInputTests`。代码名：`CivilizationExpansionRuntime`、`GrayboxCivilizationExpansionController3D`、`GrayboxCivilizationExpansionView3D`、`GrayboxCivilizationExpansionVisualPresenter3D`。

### 文明扩展透明图与公告板表现（仅限场景）

能解决什么：以 `GrayboxCivilizationExpansionVisualPresenter3D` 从统一 141 项目录解析四单位、三角色、次城、前哨、运输队、M/N/P 页签与八个状态徽记 Sprite，并由 View/Controller 将其显示为 UI 图标及可替换世界公告板。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionVisualPresenter3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionView3D.cs`、`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionController3D.cs`。怎么复用：始终使用正式内容 ID 解析 Sprite；单位显示按小队组成选择图标，settlement/convoy 坐标继续来自 WorldLayer/Transport，角色只在没有独立坐标时作为政务头像，状态徽记由领域快照选择。不能负责什么：Sprite、缩放比例、世界高度、锚点、排序层、面向相机、状态染色和 fallback 全部只属表现，不进入 schema `34`，不创建第二套单位位置、运输进度、角色状态、忠诚或选择真值；透明公告板不等于正式 3D 建模、骨骼或动画完成。改后跑哪组测试：`Production2DCivilizationExpansionVisualPipelineTests`、`GrayboxCivilizationExpansionVisualIntegrationTests`、`GrayboxCivilizationExpansionUiInputTests`、`GrayboxCivilizationExpansionRuntimeInputTests`。代码名：`GrayboxCivilizationExpansionVisualPresenter3D`、`GrayboxCivilizationExpansionView3D`、`GrayboxCivilizationExpansionController3D`。

### 军队制造与远征运行时（推荐复用）

能解决什么：提供四类配置化单位、单稳定小队、原子制造与维护休眠、五类命令、确定性远征和返城战利品。在哪里：`Assets/_Game/Scripts/Combat/ArmyUnitCatalog.cs`、`Assets/_Game/Scripts/Combat/SingleCityArmyModel.cs`、`Assets/_Game/Scripts/Combat/ArmyExpeditionModel.cs`。怎么复用：提供四类配置化单位、单稳定小队、原子制造与维护休眠、五类命令、确定性远征和返城战利品。不能负责什么：只消费城市库存与既有敌人目录；不拥有建筑完成、地图揭示、Defense敌人或Unity表现真值。改后跑哪组测试：`ArmyUnitCatalogTests`、`SingleCityArmyModelTests`、`ArmyExpeditionModelTests`、`ArmyPersistenceModelTests`。代码名：`ArmyUnitCatalog`、`SingleCityArmyModel`、`ArmyExpeditionModel`。

### 多城市世界层与运输运行时（推荐复用）

能解决什么：提供主城引用、一次城、一前哨、独立库存/自治、查看与控制权以及存在时间、货物和风险的实体运输队。在哪里：`Assets/_Game/Scripts/World/CivilizationExpansion/SettlementRuntime.cs`、`Assets/_Game/Scripts/World/CivilizationExpansion/TransportRuntime.cs`、`Assets/_Game/Scripts/World/CivilizationExpansion/WorldLayerCatalog.cs`。怎么复用：提供主城引用、一次城、一前哨、独立库存/自治、查看与控制权以及存在时间、货物和风险的实体运输队。不能负责什么：主城仍引用既有库存，位置和路径只消费WorldMapModel与CityPathfinder；不创建第二张地图或第二套主城建筑网格。改后跑哪组测试：`IDEA0022WorldLayerSettlementTransportTests`。代码名：`WorldLayerRuntime`、`SettlementRuntime`、`TransportRuntime`。

### 角色生命、继承与外交运行时（推荐复用）

能解决什么：提供三角色倒地/救援/恢复/死亡遗体、议会/继承/政变和两外部势力接触/报价/协议状态。在哪里：`Assets/_Game/Scripts/Leader/CivilizationExpansion/CharacterLifeRuntime.cs`、`Assets/_Game/Scripts/Leader/CivilizationExpansion/LeadershipPoliticsRuntime.cs`、`Assets/_Game/Scripts/Leader/CivilizationExpansion/DiplomacyRuntime.cs`。怎么复用：提供三角色倒地/救援/恢复/死亡遗体、议会/继承/政变和两外部势力接触/报价/协议状态。不能负责什么：资源代价和城市忠诚通过显式authority提交；不拥有城市库存、settlement或UI，不实现实体内战和完整AI文明。改后跑哪组测试：`IDEA0022CharacterLifeRuntimeTests`、`IDEA0022LeadershipPoliticsRuntimeTests`、`IDEA0022DiplomacyRuntimeTests`。代码名：`CharacterLifeRuntime`、`LeadershipPoliticsRuntime`、`DiplomacyRuntime`。

### schema 34 文明扩展存档适配器（复用前审查）

能解决什么：在文明扩展各领域不可变快照与schema 34单一聚合DTO之间执行确定性映射，并由正式存档协调器参与回滚。在哪里：`Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionSaveAdapter3D.cs`、`Assets/_Game/Scripts/Persistence/ThreeD/FormalThreeDExpansionSaveData.cs`。怎么复用：在文明扩展各领域不可变快照与schema 34单一聚合DTO之间执行确定性映射，并由正式存档协调器参与回滚。不能负责什么：不拥有文件IO、领域tick或场景发现；schema 33只迁移为空的显式初态，不反推不存在的历史。改后跑哪组测试：`FormalSaveSchema34ContractTests`、`GrayboxCivilizationExpansionRuntimeInputTests`。代码名：`GrayboxCivilizationExpansionSaveAdapter3D`、`FormalThreeDCivilizationExpansionSaveData`。

### 正式构建工具（复用前审查）

能解决什么：用于三类现役 3D 构建：Windows Release、Windows Development 和 universal x86_64+arm64 macOS；构建期间临时登记批准的 URP 管线，带 -quit 的正式命令行构建还会在编辑器最终退出时恢复受保护文件。在哪里：`Assets/_Game/Editor/FormalBuildTools.cs`。怎么复用：用于三类现役 3D 构建：Windows Release、Windows Development 和 universal x86_64+arm64 macOS；构建期间临时登记批准的 URP 管线，带 -quit 的正式命令行构建还会在编辑器最终退出时恢复受保护文件。不能负责什么：不修改游戏规则，也不提供已退役的 2D 构建入口；构建作用域与命令行最终退出恢复必须还原进入构建前的渲染管线、Quality 序列化状态和四个受保护文件的精确字节：Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset、ProjectSettings/GraphicsSettings.asset、ProjectSettings/QualitySettings.asset、ProjectSettings/ProjectSettings.asset。普通 GUI 构建不得遗留最终退出标记或备份；改后必须运行 GrayboxBuildAndPerformanceTests 中的 universal macOS 与 final-exit 合同，并执行受影响平台的真实构建后哈希检查。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`。代码名：`FormalBuildTools`、`GrayboxRenderPipelineBuildScope`。

### 灰盒性能探针（仅限场景）

能解决什么：用于采集灰盒性能数据，并执行 IDEA-0014 活跃生产、八敌、防御 HUD、撤离 UI 的 300 稳定帧混合探针、GUI 捕获和正式汇总。在哪里：`Assets/_Game/Editor/GrayboxPerformanceProbe.cs`。怎么复用：用于采集灰盒性能数据，并执行 IDEA-0014 活跃生产、八敌、防御 HUD、撤离 UI 的 300 稳定帧混合探针、GUI 捕获和正式汇总。不能负责什么：只用于可重复验证和正式 Marker 取证，不改变玩法真值、不作为发布版本逻辑，也不替代用户试玩或真实 Windows GPU、显存和内存验收。改后跑哪组测试：`GrayboxBuildAndPerformanceTests`、`GrayboxFormalEvacuationPerformanceTests`。代码名：`GrayboxPerformanceProbe`。
