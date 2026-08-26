# 废土移动城市测试与 Bug 定位指南

## 适合谁看

适合试玩者、项目负责人和需要修复问题的新开发者。这里的 EditMode（不启动游戏画面、在编辑器里检查规则和资料的测试）与 PlayMode（启动游戏流程、检查实际互动的测试）是两种自动检查；组件（挂在场景物体上的一小块功能）、程序集（把相关代码一起编译成可用单元的集合）和稳定 ID（不随改名或资源替换而变化的固定编号）是定位时会见到的词。测试是在回答“哪些已被检查”，不是在承诺“玩家一定不会遇到问题”。

## 测试是什么，不是什么

测试是可以重复执行的检查：它能确认一条规则、一次互动或一组资料在给定条件下符合预期。它不是对手感、易懂程度、画面观感或所有设备情况的代替；这些仍需要人工试玩。自动报告只是排查起点：报告中的功能名、文件和场景只是建议先看的位置，必须结合复现步骤确认。

## 五层检查

1. **快速规则**：先运行最快的规则检查，确认问题没有来自最基础的输入或数据。
2. **单功能**：只检查正在修改的城市、建造、UI、存档或美术功能。
3. **真实场景**：用 PlayMode 或实际场景检查玩家的操作流程。
4. **完整回归**：在相关检查通过后，运行完整的 EditMode 与 PlayMode 回归，确认改动没有伤到别处。
5. **人工试玩**：由人按真实方式操作、观察并记录体验；自动化不能替代这一层。

## 按功能选择测试

先在[项目自动清单](Generated/Project-Inventory-ZH.md)确认功能所属文件和场景。单功能检查优先看失败报告中的“只重跑这个失败”：报告实际会在“建议复跑”下给出一个可直接复制的单类筛选。若还没有失败报告，或要补跑相关类，就在[测试自动清单](Generated/Test-Inventory-ZH.md)的“精确测试文件与测试类”表找到对应类名，手动把一到数个类名用 `|` 连起来。该附录的“可复制的测试筛选命令”目前只有全部测试类的聚合筛选，不能当作单功能命令。建造、城市、UI、地形、美术、存档和 legacy schema 兼容的最低检查并不相同；先跑单功能，再跑相关检查，最后才完整回归。变更批准状态和需要补写的记录，以[用户反馈与变更控制](06-User-Feedback-and-Change-Control-ZH.md)为准。

## IDEA-0018 地图视觉、镜头层级与 UI 比例检查边界

`IDEA-0018` 当前为“已实现待验证”。地图玩法仍固定为 `64×48`、seed `8128`、3072 格和 schema `32`；任何资源节点、通行、放置、建筑坐标或 world signature 变化都不是本轮视觉重制。地形源、四通道生成器或数组变化必须运行 `FirstArtTerrainVisualStyleTests`、`FirstArtTerrainAssetBuilderTests` 和完整 `TerrainAssetDeep`；Shader/材质与七层综合色运行 `FirstArtTerrainShaderTests`。镜头滚轮、模态阻断、Near/Mid/Far 与定向采矿覆盖运行 `GrayboxCameraAndInputTests`、`GrayboxVisualAndWorldTests`、`GrayboxBuildingProjectionAndViewTests` 和 `GrayboxRuntimeSceneTests`。

UI 比例先查 `FormalUiLayoutPolicy3DTests`、`FormalUiResponsiveLayout3DTests` 与 `GrayboxSceneContractTests`；目录、生产、修改器和防御的点击/滚动必须继续通过真实 Input System 的 `GrayboxBuildingRuntimeSceneTests`、`GrayboxProductionObservabilityRuntimeInputTests`、`GrayboxDeveloperModifierRuntimeInputTests` 与 `GrayboxDefenseRuntimeInputTests`。建造栏保持 `620×54` 和既有位置；大型面板使用安全区/滚动，不能用整体缩小绕过字号问题。

发布级证据还要在真实 Unity GUI 运行 `FirstArtTerrainEvidenceCapture.StartAutomatedCapture`，并显式提供仓库外的 `WASTECITY_FIRST_TERRAIN_RUNTIME_RESULT`。工具使用隔离临时存档进入游戏，不得读写用户真实存档；manifest 必须包含 15 张截图、10 帧缩放、Near/Mid/Far、300 连续水面帧、颜色/动态门和逐文件 SHA-256。自动截图只能证明技术合同，最终仍需用户检查地貌区分、接缝、遮挡、字体与比例，并在真实 Windows 10 和 Windows 11 检查 GPU、显存和内存。

## IDEA-0019 地图 v2、资源布局与世界尺度检查边界

`IDEA-0019` 当前为“已批准，开发中”。它明确承接 `IDEA-0018` 当时排除的地图真值变化：地图仍为 `64×48`、3072 格、seed `8128` 和 schema `32`，但 world generation/signature 升为 v2，地形、Traversal、出生/路线与资源节点改为覆盖全图的确定性连片布局。旧 v1 world identity 必须在任何运行时应用前以“存档世界配置与当前正式世界不兼容”明确拒绝，不能把旧坐标、节点余量或建筑绑定猜测迁移到新图。

地图和资源先运行 `WorldMapTests` 与 `GrayboxWorldLayout3DTests`：固定 v2 identity、重复生成、固定 `8×6` 宏格模板、每宏格两个整数扰动通道、一轮清理、出生保护区、关键区双通路和正式目录明确登记的 `24` 个坐标（铁矿 `8`、石料 `4`、能晶 `4`、水 `4`、生物质 `4`）。本版资源不是评分/搜索/地形亲和算法生成，测试应逐项比对目录坐标、类型和储量。A16.3 的两个安全铁矿、至少一个安全石料点和三个裂谷铁矿必须包含在该总数内；铁矿、石料和能晶节点还要通过既有 `BuildingResourceNodeCompatibilityRulesTests` 与 `GrayboxBuildingProjectionAndViewTests` 证明存在完整 `2×2` 采矿锚点。水和生物质保持世界来源，但不因本次地图重排被改成采矿站兼容资源。路径、部署和正式场景继续补跑 `CityPathfinderTests`、`CityTerrainRulesTests`、`CityDeploymentRulesTests`、`GrayboxMobileCityController3DTests` 与 `GrayboxRuntimeSceneTests`。

存档身份运行 `GrayboxFormalSaveWorldCityTests`、`GrayboxFormalSaveRuntimeHostTests` 和正式 3D round-trip PlayMode：generation `2`、signature `core.world.formal-3d.v2.64x48` 与 schema `32` 必须分别断言，v1 拒绝不能改变当前 WorldMap、城市、人口或导航状态。`GrayboxWorldLayout3DTests.IDEA0019_Seed8128V2WorldMatchesStableGoldenHash` 已保存当前 3072 格按 `y/x|terrain|traversal|resource|amount` canonical 编码计算的 SHA-256 `2f0ecd374ad3a1bf6fd50564d949741618c7ce1b72bc6619f67acda632b1e6fd`；地图真值变化必须同步 generation/signature 或经批准更新该 hash。地表控制图、唯一 Renderer 和 Ruins/Cliff 继续运行 `FirstArtTerrainControlMapTests`、`FirstArtTerrainRendererTests`、`FirstArtTerrainSceneContractTests`、`FirstArtTerrainRuntimeSceneTests` 及 Ruins/Cliff 的 Layout、Geometry、Presentation、SceneContract 测试；不得增加第二套地图判断、逐格常驻对象或绕过 14 Prefab、两类合批、13 材质与分类回退。

世界比例先运行 `FormalWorldPresentationScaleProfile3DTests`，再运行 `GrayboxBuildingProjectionAndViewTests`、`GrayboxVisualAndWorldTests`、`FormalUiResponsiveLayout3DTests` 与受影响的真实输入 PlayMode。30 座 `BuildingCatalog` 的逻辑 footprint 和四向旋转必须不变；地面/内城建筑的施工、完成、废墟、预览和选择框只消费正式表现 Profile，三座正式塔的 BuildingWorldView 基础座统一为 74% footprint、精确 `.14` 格高。矿点默认正交尺寸 `13` 为 Mid，只显示图标与储量；当前三档目标分别为 Near Frame `68`、Icon `50`、Text `22`，Mid Frame `56`、Icon `42`、Text `20`，Far Icon `28`，其 Frame、Icon、Text/Shadow Renderer 显隐和物理像素换算必须在 `1280×720`、`1920×1080`、`2560×1440` 与紧凑窗口下读取实际结果。标签避让还必须验证相同输入重复得到相同可见集合、冲突时稳定 ID 决胜、引导/选中优先且至少保留 `6px` 间距。建造栏继续保持 `620×54`，目录 Hero 图标使用正式 `64` 语义尺寸，不允许用散落常量重新放大。

日常实现阶段若没有修改地形源、导入规则、Texture2DArray Builder 或数组本身，仍按普通 EditMode 路由，不因 WorldMap 内容变化反复重建四个 2K 数组；正式收口按本需求批准的发布级验收补跑一次 `TerrainAssetDeep`，随后完成日常完整 EditMode、完整 PlayMode、项目质量门、三项现役构建、GUI 固定证据、文档生成/校验和 `RecordVerification`。自动化不能替代用户对地貌布局、矿区密度、建筑/图标比例、遮挡和真实 Windows GPU/显存/内存的判断。

## IDEA-0011 生产与界面的检查边界

`IDEA-0011` 的生产、背包、应急合成、六节点兼容研究和资源状态栏已经实现待验证；`IDEA-0016` 当前正在把它扩展为 31 种资源、30 条配方、正式研究运行时和 43 节点科技树。排查正式研究规则、初始根、倍率、暂停、退款或 schema `31` 恢复时，优先运行 `FormalResearchRuntimeTests` 与 `GrayboxFormalResearchSaveAdapterTests`；排查节点数、依赖边、确定布局、缩放或视图层级时，运行 `ResearchTreeProjection3DTests` 与 `ResearchTreeUiContractTests`；排查真实 T、搜索字符、双 Esc、拖动、滚轮、Home、面板互斥或点击穿透时，必须补跑 `GrayboxProductionObservabilityRuntimeInputTests`。历史 `DemoResearchRuntimeTests` 继续作为六节点和退役内容兼容回归，不得替代正式目录测试。精确类名和当前归属仍以自动生成的[测试清单](Generated/Test-Inventory-ZH.md)为准。

不要把开发补给夹具当成自然开局证据。正式会话石料为 `0`，而现有冶炼厂施工需要 `6` 石料；自动链测试会先通过显式开发补给搭建 `2 采矿站 → 2 冶炼厂 → 1 装配厂`，再清零铁矿、合金和弹药等生产物资，观察节点采收和机器加工是否自动补出完整链。该测试只验证运行时生产闭环；自然开局的石料路径应由 `IDEA-0012` 的 seed `8128` 原始内容区可采石料节点及其场景测试单独证明。若试玩仍找不到或无法采集石料，应作为 `IDEA-0012` 回归记录，不要在测试里偷偷修改正式开局或建筑成本。

本阶段的聚焦 TDD 证据不能替代最终门：日常完整 EditMode、完整 PlayMode、项目质量检查、正式构建、文档生成和 `RecordVerification` 仍要在收尾时完成。真实 Windows 10 和 Windows 11 机器的视觉、GPU、显存、内存表现和用户试玩只能由实际执行结果确认；macOS 编辑器测试或跨平台构建成功不能替代这些结论。当前正式 3D 存档 schema 必须保持 `31`；已经退役的 2D 运行时不接新 UI 或新功能，schema `1–30` decoder、迁移和固定样本只做兼容回归。敌人、炮塔和弹丸不属于 `IDEA-0016` 本轮测试通过所能证明的范围。

## IDEA-0016 资源、配方、修改器与二维资产检查边界

`IDEA-0016` 当前是“开发中，主体已实现，待本轮完整验证”。聚焦检查已经覆盖部分子系统，但本节列出的类名只是收口入口，不代表完整回归、构建或人工验收已完成。

按责任边界定位：31 种资源定义、来源用途和发现条件先查 `ResourceDefinitionCatalogTests` 与 `ResourceDiscoveryProjectionTests`；30 条配方、机器/手工边界和研究引用先查 `ResourceRecipeCatalogIntegrityTests`；多输入原子预留、多输出容量和停工原因先查 `FormalProductionSimulationTests`；同建筑多配方、切换拒绝、未知配方和生命周期先查 `GrayboxProductionRuntimeTests`；非默认配方及无序预留的 schema `31` 往返先查 `GrayboxFormalSaveProductionTests`；详情中的全部输入/输出通道、暂停与资源转移先查 `GrayboxProductionObservabilityFacadeTests`。不要从界面文字重新推导配方、容量、物流连接或发现状态。

修改器目录、中文搜索和正式命令分别由 `GrayboxDeveloperModifierCatalogTests`、`GrayboxDeveloperModifierTests` 保护；数字键 `0`、文本焦点、双 Esc、世界输入抑制和 Release 无入口必须再跑 `GrayboxDeveloperModifierRuntimeInputTests` 与相关易用性测试。测试必须通过正式 Input System 主循环，不能只直接调用按钮方法。`F10` 继续无行为，stable ID 只用于内部键，主列表必须显示游戏中文名。

二维资产按类别运行 `Production2DItemIconPipelineTests`、`Production2DBuildingIconPipelineTests`、`Production2DTechnologyIconPipelineTests` 和 `Production2DUiCharacterMarkerPipelineTests`；统一视觉目录与 SpriteAtlas 运行 `Production2DVisualCatalogAtlasTests`。当前统一目录/图集的六项聚焦 EditMode 检查已通过，只证明 114 个稳定视觉键、30 条配方视觉投影、六类 Atlas 和重复构建稳定性，不代表本轮完整回归或用户视觉验收。失败时先区分四层：源母版和透明 Alpha、Unity 交付 PNG 与 `.meta`、导入规则、运行时目录/消费者。导入器只能作用于 `Assets/_Game/Art/Production2D/`，不得触碰地形；GUID、尺寸、安全区、九宫格 Border、世界标记中央透明孔和重复构建字节稳定必须由资产测试读取。联络表、Alpha 检查和静态测试不能替代用户对正式图片的视觉判断，也不能推断真实 Windows GPU、显存和内存。

真实 UI 回归统一补跑 `GrayboxProductionObservabilityRuntimeInputTests` 与受影响的建筑运行时输入测试，至少检查全部配方可滚动、只有两条应急手工配方可排队、资源账本筛选、机器配方选择、科技图标、建筑图标和所有文字输入焦点不穿透。最终仍须按顺序完成日常完整 EditMode、完整 PlayMode、项目质量门、三项现役 3D 构建、官方文档生成/验证和 `RecordVerification`；在这些门完成前，不得把 IDEA-0016 写成“已验证”。本轮未修改地形源、地形导入规则、Texture2DArray Builder 或数组生成，因此日常回归不运行 `TerrainAssetDeep`；只有真正进入发布准备时才单独补跑。

## IDEA-0017 终局结算、会话统计与波前重试检查边界

`IDEA-0017` 当前为“已实现待验证/完整自动化与三项构建通过待人工”。测试必须把五层所有权分开：`SessionStatisticsModel` 是完整会话统计真值；生产时钟只发布单 tick 的批次、active 和 eligible 增量；战役模型拥有 terminal revision、胜负与统计冻结；结算 model/view/controller 只投影不可变终局快照并提交命令；内部最近波前 Store 只做完整 schema `32` Formal3D envelope 的文件读写与验证。UI、存档适配器和测试夹具都不得重新计算胜负、效率、修改器使用或迁移历史。

聚焦 EditMode 入口按失败类型选择：

- 会话统计的稳定排序、原子恢复、终局冻结、部分迁移和单向修改器标记：`SessionStatisticsTests`；
- 生产完成批次、有效推进时间、符合运行资格时间以及暂停/停工边界：`GrayboxProductionStatisticsDeltaTests`；
- 唯一胜负结算、完整统计目录顺序、效率无数据、坚守/机动和 terminal revision 幂等：`SingleCityDefenseSettlementTests`；
- 全屏 blocker、标题与完整统计、按钮许可、命令防重、失败保留、关闭释放：`GrayboxDefenseSettlementUi3DTests`；
- DefenseController 组合、正式 Input System 的建造键阻断、继续沙盒、失败重试和返回标题路由：`GrayboxDefenseSettlementRuntimeIntegrationTests`；
- 独立内部路径、schema `32` 完整验证、未来/损坏/空档结构化失败、原子替换和失败保留旧档：`FormalSaveWaveRetryStoreTests`；
- 战役检查点与完整统计持久化：`SingleCityDefenseCampaignCheckpointTests`；schema `32` DTO、codec、validator 和 schema `31→32` 部分统计迁移：`FormalSaveSchema32ContractTests`；防御 DTO 适配和运行时主机重试入口：`GrayboxFormalDefenseCampaignSaveAdapterTests`、`GrayboxFormalSaveRuntimeHostTests`；
- 开发修改器只有成功且实际改变玩法状态才标记本局：`GrayboxDeveloperModifierTests`。

定位顺序：数值重复、总数漂移、恢复后继续累计错误先查 `SessionStatisticsModel` 与 `SingleCityDefenseCampaignModel`；效率分母、玩家暂停或停工资格错误先查 `BuildingProductionState`、`GrayboxProductionClock3D`；标题、条目、按钮或重复点击先查 `SingleCityDefenseSettlement`、`GrayboxDefenseSettlementView3D`、`GrayboxDefenseSettlementController3D`；模态打开后建造/世界输入穿透先查 `GrayboxDefenseController3D` 与 `GrayboxUsabilityInputCoordinator3D`；继续沙盒、重试或返回标题失败先查 `GrayboxFormalSaveEntryController3D`、`GrayboxFormalSaveRuntimeHost3D`；重试档空、损坏、未来 schema 或原子写失败先查 `FormalSaveWaveRetryStore` 的结构化 code，不能从 UI 文案猜测；统计字段往返、hash 或迁移标记错误再查 `GrayboxDefenseSaveAdapter3D`、`FormalSaveCodec`、`FormalSaveValidator` 和 `FormalThreeDSaveData`。

自动化通过仍不证明人工体验。最终验收至少实际操作一次胜利继续沙盒、一次失败读取最近波前、一次失败返回标题，确认结算只出现一次、标题没有写成整个游戏通关、长统计可读、失败反馈保留、关闭后世界输入恢复；再在真实 Windows 10 和 Windows 11 检查输入、视觉、GPU、显存和内存。当前地图、seed、资源节点和三维模型未改变；退役 2D 没有接入结算 UI，只保留 schema `1–30` decoder、迁移与固定样本回归。本阶段未修改地形源、导入规则、Texture2DArray Builder 或数组生成，日常 EditMode 不运行 `TerrainAssetDeep`；只有准备发布时按总门补跑。

## BUG-0007 自然开局研究站与统一放置失败反馈

先用失败测试固定两个真实问题：正式 3D 会话保持人口 `100`、不调用开发人口修改时，研究站当前被目录中的 `200` 人口门槛锁定；可见但锁定的目录卡或快捷栏当前不可点击，因此不会在固定建造反馈条显示原因。最小实现只把研究站首轮最低人口改为 `0`，保留 `PopulationRequired` 和其他建筑既有门槛；同时让所有可见锁定选择和全部蓝图放置失败读取统一解锁/放置评估，并显示到位置不变、背景不接收射线的 `Placement.Status`。隐藏内容不得因本修复提前显示，研究站成本和 schema `30` 不得改变。冻结 2D 不接新 UI 或新功能，但因复用共享稳定 `BuildingCatalog`，研究站解锁配置同步采用门槛 `0` 并只做回归验证。

规则与界面聚焦入口：

```sh
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testFilter 'WasteCity.Tests.BuildingUnlockTests|WasteCity.Tests.GrayboxBuildingCatalogTests|WasteCity.Tests.BuildingPlacementEvaluationTests|WasteCity.Tests.GrayboxBuildingProjectionAndViewTests|WasteCity.Tests.GrayboxBuildingUiAndInputTests' \
  -testResults /tmp/wastecity-bug0007-focused-editmode.xml \
  -logFile /tmp/wastecity-bug0007-focused-editmode.log
```

真实场景必须通过正式 Input System 操作 `B`、目录或快捷栏、鼠标移动和世界点击；至少覆盖人口 `100` 时研究站可选择并开始施工、可见锁定建筑点击显示权威人口/研究/前置原因、各类蓝图失败继续显示同一反馈条，以及建造栏位置和世界点击不回退：

```sh
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform PlayMode \
  -testFilter 'WasteCity.Tests.GrayboxBuildingRuntimeSceneTests|WasteCity.Tests.GrayboxFormalEvacuationVerticalSliceTests' \
  -testResults /tmp/wastecity-bug0007-focused-playmode.xml \
  -logFile /tmp/wastecity-bug0007-focused-playmode.log
```

定位时，研究站稳定门槛先查 `BuildingGrid.cs` 中的 `BuildingCatalog` 与 `BuildingUnlockModel`；蓝图失败顺序和真值查 `BuildingPlacementEvaluation`；可见性与锁定原因查 `GrayboxBuildingCatalogPresenter3D`；固定反馈、锁定点击与不拦射线查 `GrayboxBuildingMenuView3D`、`GrayboxBuildingInputRouter3D` 及对应真实输入测试。不要删除通用人口失败枚举，也不要用开发人口补丁让测试绕过正式开局。

## IDEA-0014 正式撤离与完整垂直切片检查边界

`IDEA-0014` 已按先失败后实现的顺序固定 5 秒展开、8 秒收起、转换取消、第一名敌人生成后的战斗状态、战斗收起 `-30%`、和平/战斗完整拆除、快速拆除、遗弃和确定性退款，并检查建筑内部库存等内部物资、塔内弹药、仓库内容与退款能否在同一原子容量门和容量预检下完整迁移或准确拒绝。清单和队列必须读取不可变视图。该阶段的日常 EditMode 共 1787 项、完整 PlayMode 共 112 项，连同项目质量门、四个正式构建和验证记录均已通过；当前仍为“已实现待验证”，因为用户试玩和真实 Windows 验收尚未完成。不得用 UI 重新计算战斗状态、退款或容量缺口，也不得把这些自动化与构建证据写成用户已经验证。

相关运行时检查至少覆盖 `CityDeploymentRulesTests`、`GrayboxEvacuationTests`、`CityResourceStorageModelTests`、`GrayboxWarehouseStorageIntegrationTests`、`GrayboxProductionRuntimeTests`、`DemoResearchRuntimeTests`、`GrayboxFirstDefenseRuntimeTests`、性能和场景合同。可用下面的精确聚焦入口；测试命令不要加 `-quit`：

```sh
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testFilter 'WasteCity.Tests.CityDeploymentRulesTests|WasteCity.Tests.GrayboxEvacuationTests|WasteCity.Tests.CityResourceStorageModelTests|WasteCity.Tests.GrayboxWarehouseStorageIntegrationTests|WasteCity.Tests.GrayboxProductionRuntimeTests|WasteCity.Tests.DemoResearchRuntimeTests|WasteCity.Tests.GrayboxFirstDefenseRuntimeTests|WasteCity.Tests.GrayboxFormalEvacuationPerformanceTests|WasteCity.Tests.GrayboxSceneContractTests' \
  -testResults /tmp/wastecity-idea0014-focused-editmode.xml \
  -logFile /tmp/wastecity-idea0014-focused-editmode.log
```

玩家界面通过正式场景的真实 Input System 操作 `F`、`B`、`T`、`Space`、世界点击和撤离 UGUI。`GrayboxFormalEvacuationVerticalSliceTests` 连续验证六段：驾驶并展开；建研究站并完成三项研究；建立生产链和机枪塔；真实生产、补弹和防御；敌人存活时打开并确认撤离；完成 Packing、返回 Mobile 并再次驾驶。夹具必须保持正式人口 `100`、不得调用开发人口修改；只可在首次玩法输入前设置确定性资源和规则时间加速，不能直接完成研究、创建建筑、注入产物或弹药、杀敌、切换模式或调用撤离入口/提交。相关真实输入组合入口为：

```sh
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform PlayMode \
  -testFilter 'WasteCity.Tests.GrayboxFormalEvacuationVerticalSliceTests|WasteCity.Tests.GrayboxBuildingRuntimeSceneTests|WasteCity.Tests.GrayboxProductionObservabilityRuntimeInputTests|WasteCity.Tests.GrayboxDefenseRuntimeInputTests' \
  -testResults /tmp/wastecity-idea0014-focused-playmode.xml \
  -logFile /tmp/wastecity-idea0014-focused-playmode.log
```

性能检查必须同时存在活跃生产、八名敌人、防御 HUD 和撤离 UI。外部探针输出路径由 `WASTECITY_FORMAL_EVACUATION_MIXED_PERF_RESULT` 指定，并通过以下正式入口记录 300 次稳定适配、活跃防御、事务预算和全部 Marker：

```sh
WASTECITY_FORMAL_EVACUATION_MIXED_PERF_RESULT=/tmp/wastecity-idea0014-performance.json \
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
  -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.MeasureFormalEvacuationMixedPerformance \
  -logFile /tmp/wastecity-idea0014-performance.log
```

真实 GUI Profiler 仍须在锁定的 WasteCity Unity `2022.3.62f1` Editor PlayMode、`1920×1080`、关闭 Deep Profile、启用 CPU/Rendering/Memory 的条件下保存恰好 300 帧原始 `.data` 和三张模块截图。解析时必须使用专用入口，不能用旧的普通汇总冒充正式心跳捕获：

```sh
WASTECITY_GUI_PROFILER_INPUT=/tmp/wastecity-idea0014-gui-300frames.data \
WASTECITY_GUI_PROFILER_RESULT=/tmp/wastecity-idea0014-gui-summary.json \
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
  -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.SummarizeFormalEvacuationMixedGuiProfilerCapture \
  -logFile /tmp/wastecity-idea0014-gui-summary.log
```

定位失败时按所有权排查：正式时间、转换取消和战斗收起先查 `CityDeploymentModel`、`CityDeploymentRules` 与 `GrayboxMobileCityController3D`；退款和处置先查 `BuildingEvacuationRules`；容量不足、部分写入或重复退款先查 `CityResourceStorageModel` 与 `GrayboxBuildingSession3D`；生产缓存、研究进度或塔内弹药丢失先查对应运行时和 `IsEvacuationLocked`；清单、冻结批次、稳定队列或重试先查 `GrayboxEvacuationController3D`；按钮、暂停透传和点击穿透先查 `GrayboxBuildingInputRouter3D`、`GrayboxUsabilityInputCoordinator3D` 与 `GrayboxBuildingMenuView3D`；场景引用先查 `GrayboxSceneContractTests` 和 `GrayboxSceneAuthoring`；性能再查正式 Marker、混合探针、GUI 原始捕获和汇总 JSON。

`IDEA-0014` 的退役前实现与验证基线保持 schema `30`；当时冻结的 2D `FormalPrototype` 没有接入新 UI 或新功能，只同步复用共享稳定建筑目录的研究站首轮门槛并由 legacy 回归保护。后续 `IDEA-0015` 已独立实现 schema `31`、退出保存、自动检查点与重开恢复，并已通过完整自动化、项目质量门、四个退役前正式构建和正式验证记录，状态为“已实现待验证”。Task 14 已退役 2D 专属运行时，退役后的聚焦和完整自动化、项目质量门、三项现役 3D 构建、官方文档生成/校验和正式验证记录均已通过。用户试玩和真实 Windows 10 与 Windows 11 的路径、权限、视觉、GPU、显存、内存验收仍必须由实际执行结果确认。

## IDEA-0015 正式 3D 存档与 schema 31 检查边界

`IDEA-0015` 当前为“已实现待验证”。统一 Store/Coordinator、独立 3D payload、schema `31` 单槽、开始/继续/保存并退出、事件驱动自动检查点、安全文件事务和五类活动状态往返已经实现；schema `1–30` 继续表示旧 2D 存档，按原语义可读，3D 入口不会把它们静默解释成 3D。实现 HEAD `a8f30af` 的完整自动化、项目质量门、Windows Release 3D、Windows Development 3D、legacy 2D、macOS universal 3D 四个退役前正式构建、官方文档生成/验证和 `RecordVerification` 已通过。Task 14 已退役 `FormalPrototype`、47 个 2D 专属运行时脚本（共 51 个 `MonoBehaviour` 类型）与 legacy build；退役后的聚焦和完整自动化、项目质量门、三项现役 3D 构建、官方文档生成/校验和正式验证记录均已通过。这些证据不代表用户试玩或真实 Windows 10 与 Windows 11 的视觉、GPU、显存和内存验收完成。

实现与定位按以下边界检查：

1. 存档 envelope 与语义校验：覆盖 `gameVersion`、`saveSchemaVersion`、`contentSources`、`createdAt`、`updatedAt`，拒绝空白、损坏、零值、未来 schema、缺失或重复实例 ID、非法枚举/范围和错误数组长度；未知内容定义 ID 必须保留为缺失内容占位或孤立数据，不得静默删除；提交 schema `30` 与 schema `31` 的正式回归 fixture，不再只依赖测试内联 JSON；
2. 迁移与旧档边界：每次结构变化增加新的单向迁移且不回改旧迁移；迁移前备份，失败保留原档并显示可理解错误；旧 ID 改名维护显式映射。schema `1–30` fixture 必须继续由旧 2D decoder 读取，但不得自动进入 3D Restore；
3. 文件事务：使用隔离临时目录和可注入文件边界检查临时写入、刷新、原子替换、主档与备份回退、权限不足、写入中断和迁移失败。测试不得读写玩家真实 `Application.persistentDataPath` 存档；任何失败都不得损坏既有主档或备份；
4. 领域状态：至少覆盖世界 seed 与资源节点余量，城市位置、部署状态和剩余时间，人口，建筑稳定实例 ID、下一序号、位置、方向、施工和所有权，城市核心与真实仓库内容/过滤，背包槽位、合成队列与预留输入，研究，生产内部缓存/进度/暂停，防御波次/敌人/核心/塔内状态。物流连接、停工原因、容量、UI 与表现等派生状态应在加载后重算，不保存第二份真值；
5. 活动流程：必须能保存和恢复 Deploying、Packing、教学战斗以及已经确认并进入处理的撤离批次，覆盖冻结 work、锁、内部载荷、队列当前位置、剩余时间、阻塞与重试，防止资源丢失、复制或重复提交。未确认的撤离菜单、选择、悬停、建造预览和其他 UI 状态不保存；
6. 正式场景和真实输入：通过正式 Input System 验证退出保存、重开恢复、主档损坏后的备份回退和可见错误；捕获—加载—再次捕获必须保持权威状态等价，跨保存点继续模拟在不同帧分块下仍确定。

当前真实输入往返固定五类状态：活动 Deploying、活动 Packing、恰好八名存活敌人的教学战斗、已确认撤离处理中、容量阻塞撤离。测试只允许用开发规则时间倍率和确定性资源缩短搭建；保存、退出、继续、建筑放置、研究选择和撤离命令必须经过正式 Input System 或 UGUI，不得直接调用 restore、切换人口或修改活动状态。容量阻塞用例必须在首次重载前记录临时测试目录中的主档/备份字节、创建时间、修改时间和属性，连续实际卸载与重载 `5` 次并逐次比对；每轮还要证明旧 host 销毁、新 scene/host 身份变化和正式 composition owner 唯一。随后实际 `yield return null` 经过 `300` 个 PlayMode 帧，再确认无自动写盘和场景对象增长。测试存档目录必须重定向到独立临时目录，并在结束时证明玩家真实 `Application.persistentDataPath` 未变化。

性能与稳定性门要求连续 `20` 次完整 coordinator capture 成功且 payload hash 稳定；单次完整快照的托管分配上限为 `1 MiB`，单次完整文件事务上限为 `4 MiB`。Editor 探针中的 `300` 次同步 `LateUpdate` 只叫 callback 稳定观察，不能冒充真实 300 帧；真实帧证据由上述 PlayMode 往返测试提供。正式 Marker 只存在于 Editor 性能探针，不得为了取证把 Profiler Marker 或假计数接入 runtime 生产逻辑。

聚焦类至少包括 `FormalSaveEnvelopeTests`、`FormalSaveValidatorTests`、`FormalSaveFileTransactionTests`、`GrayboxFormalSaveWorldCityTests`、`GrayboxFormalSaveBuildingStorageTests`、`GrayboxFormalSaveEconomyTests`、`GrayboxFormalSaveProductionTests`、`GrayboxFormalSaveDefenseTests`、`GrayboxFormalSaveEvacuationTests`、`GrayboxFormalSaveCoordinatorTests`、`GrayboxFormalSaveCheckpointTests`、`GrayboxFormalSaveRuntimeHostTests`、`GrayboxFormalSaveUiAndInputTests`、`GrayboxFormalSaveRuntimeInputTests` 和 `GrayboxFormalSaveRoundTripTests`。准确类名和当前数量仍以自动生成的测试清单为准。

失败定位先看结构化阶段，不要从 UI 文案猜原因：envelope/旧档身份/未来版本先查 `FormalSaveEnvelope`、`FormalSaveCodec`、`FormalSaveValidator`；`.tmp` 写入、复读校验、`.bak` 更新或主档替换失败先查 `FormalSaveFileTransactionResult.FailedStage`；主档/备份选择、`Legacy2DOnly`、`UnsupportedFutureSchema`、`DiskReadFailed` 和 `CorruptNoBackup` 先查 `FormalSaveStoreResult.Code`；某个领域 capture/apply 或 rollback 失败先查 `GrayboxFormalSaveCoordinatorResult3D` 的 domain/code；自动检查点重复、失败重试或错误轮询写盘先查 `FormalSaveCheckpointPolicy` 和 `GrayboxFormalSaveRuntimeHost3D`；启动页、继续按钮、覆盖确认、保存并退出和中文反馈再查 `GrayboxFormalSaveEntryController3D`、系统菜单视图与真实输入测试。

Task 13 的退役前正式收尾门包括日常完整 EditMode、完整 PlayMode、项目质量门、Windows Release 3D、Windows Development 3D、legacy 2D、macOS universal 3D 四个正式构建、官方文档生成/验证和 `RecordVerification`；实现 HEAD `a8f30af` 已完成并通过这些门。Task 14 退役后当前只剩 Windows Release 3D、Windows Development 3D、macOS universal 3D 三类构建，退役实现已经重新通过完整回归、质量门、三项构建、文档生成/校验和正式验证记录。真实 Windows 机器仍要实际检查路径、权限、首次启动、退出保存、视觉、GPU、显存和内存；macOS 自动化或生成 Windows 产物不能代替这个门。

schema `31` 的 Task 13 完整自动化、四个退役前构建和验证记录已经通过。独立 Task 14 已完成实现：Build Settings 只保留正式 3D 场景，`FormalPrototype`、47 个 2D 专属运行时脚本（共 51 个 `MonoBehaviour` 类型）和 `BuildWindowsLegacy2D` 已退役；共享纯规则以及 schema `1–30` decoder、既有迁移、正式 fixtures 与旧档中文识别继续保留并进入存档回归。Task 14 的聚焦和完整自动化、项目质量门、三项现役 3D 构建和文档生成/校验已通过；`RecordVerification` 将在实现提交后执行，用户试玩和真实 Windows 验收仍未完成。

## 怎样读失败定位报告

报告通常会给出失败测试、功能组、建议检查的文件、场景、需求编号和复跑入口。先确认失败能否复现，再看它属于哪一组；例如场景失败优先检查场景引用，界面失败优先检查输入顺序和相关组件，存档失败还要检查兼容边界。不要把“第一个被报告的文件”当作唯一原因，更不要在没有复现前推断结论。

## 明天试玩记录模板

```text
版本或提交：
场景：
操作步骤：
期望结果：
实际结果：
出现频率：每次 / 偶发 / 还不确定
截图或视频：
存档或随机种子：
是否阻塞继续游玩或推进：是 / 否，原因：
```

## Bug 修复流程

按这个固定顺序工作：**复现失败 → 失败测试 → 最小修复 → 单功能检查 → 相关检查 → 完整回归 → 人工确认**。最小修复只改为解决当前问题所必需的部分；如果发现是新需求、缺少批准或要改变玩法，应回到反馈文档登记，而不是顺手扩张修复范围。

## 偶发失败不能直接忽略

偶发不等于不存在。保留发生时间、频率、场景、输入、存档或种子、截图/视频，并尝试缩小触发条件；暂时不能复现时，也应登记为待查项。只有在人工确认和记录后，才能决定优先级或是否关闭。

## 什么情况下要构建 Windows

改到运行时、场景、输入、资源加载、渲染、平台设置或构建脚本时，除了编辑器内测试，还要构建 Windows 版本并做独立运行冒烟。只改纯文档或不影响运行时的质量映射时，通常不需要构建，但仍应完成相关自动检查和人工阅读确认。

## 日常 EditMode 与地形深度检查

日常开发完成相关单功能检查后，运行完整 EditMode 回归，但排除地形纹理数组的深度套件。着色器、材质、控制图和场景契约检查仍在日常套件中；只有会反复重建七层 2K 真实纹理数组的 `FirstArtTerrainAssetBuilderTests` 被排除。macOS 示例（先按下方技术附录设置 `UNITY_BIN` 和 `PROJECT_ROOT`）：

```sh
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testCategory '!TerrainAssetDeep' \
  -testResults /tmp/wastecity-project-quality/editmode-daily.xml \
  -logFile /tmp/wastecity-project-quality/editmode-daily.log
```

地形源 PNG、其导入策略、`FirstArtTerrainAssetBuilder`、生成的纹理数组或其序列化格式发生变化时，必须运行完整地形深度套件；发布候选版本前也必须运行。它不是每次日常修改都要跑的检查：

```sh
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testCategory 'TerrainAssetDeep' \
  -testResults /tmp/wastecity-project-quality/terrain-asset-deep.xml \
  -logFile /tmp/wastecity-project-quality/terrain-asset-deep.log
```

## 给开发者/AI 的命令入口

<details>
<summary>展开技术附录：测试命令如何查找</summary>

先复制失败报告“建议复跑”中的单类筛选；它就是失败报告中的“只重跑这个失败”。若要把相关检查放进同一次运行，从[测试自动清单](Generated/Test-Inventory-ZH.md)“精确测试文件与测试类”表复制真实类名，用 `|` 连接，并用单引号包住整个筛选值。下面按平台给出入口；把两个类名替换成表中与你的问题对应的类名即可。

### macOS

下面 macOS 命令块只适用于 macOS，需从仓库根目录执行。若 Unity 安装在其他位置，把 `UNITY_BIN` 改成该机器上 Unity 2022.3.62f1 的实际可执行文件路径。

```sh
PROJECT_ROOT="$(git rev-parse --show-toplevel)"
UNITY_BIN=/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity
mkdir -p /tmp/wastecity-project-quality
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testFilter 'WasteCity.Tests.CityPathfinderTests|WasteCity.Tests.CityTerrainRulesTests' \
  -testResults /tmp/wastecity-project-quality/focused.xml \
  -logFile /tmp/wastecity-project-quality/focused.log
```

### Linux

下面 Linux 命令块只适用于 Linux。Unity Hub 的常见安装位置是 `$HOME/Unity/Hub/Editor/2022.3.62f1/Editor/Unity`；如果你的安装位置不同，先运行 `find "$HOME/Unity/Hub/Editor" -type f -path '*/Editor/Unity' -print` 查找，再按实际安装路径替换 `UNITY_BIN`。

```sh
PROJECT_ROOT="$(git rev-parse --show-toplevel)"
UNITY_BIN="$HOME/Unity/Hub/Editor/2022.3.62f1/Editor/Unity"
mkdir -p /tmp/wastecity-project-quality
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testFilter 'WasteCity.Tests.CityPathfinderTests|WasteCity.Tests.CityTerrainRulesTests' \
  -testResults /tmp/wastecity-project-quality/focused.xml \
  -logFile /tmp/wastecity-project-quality/focused.log
```

不要把测试清单末尾的聚合筛选误当成单功能入口；它会运行全部已列出的测试类。流程始终是单功能检查，再相关检查，最后完整回归。

### Windows

Windows 用户不直接运行上面的 `sh` 命令块。请在 Unity Test Runner 中按“精确测试文件与测试类”表搜索并选择失败测试类和相关测试类，再运行所选测试；同样先单功能、再相关、最后完整回归。

</details>

- [用户反馈与变更控制](06-User-Feedback-and-Change-Control-ZH.md)
- [项目自动清单](Generated/Project-Inventory-ZH.md)
- [测试自动清单](Generated/Test-Inventory-ZH.md)
