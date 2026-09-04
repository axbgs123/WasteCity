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

## IDEA-0029 探索、领袖与前哨检查边界

- 三态迷雾与自动扫描：先跑 `IDEA0029ExplorationCatalogTests`、`IDEA0029WorldVisibilityRuntimeTests`、`IDEA0029WorldIntelRuntimeTests`、`IDEA0029WorldExplorationRuntimeTests`、`IDEA0029ScanRuntimeTests`。失败先检查 `Assets/_Game/Scripts/World/Exploration/`；`WorldMapModel` 仍是地图与资源节点真值，当前 `Visible` 可重建且不入档，首次可见扫描只能通过既有 Attention 稳定事件结算一次。
- 领袖控制、基础 AI 与手采：跑 `IDEA0029LeaderInteractionCatalogTests`、`IDEA0029LeaderControlRuntimeTests`、`IDEA0029LeaderAiRulesTests`、`IDEA0029ManualGatherRuntimeTests`、`GrayboxLeaderControlTests`、`PlayerBackpackModelTests`、`DeploymentAndHarvestTests`。失败先区分控制资格、AI 意图、距离/视野/暂停中断与背包容量预检；节点扣减必须来自同一 `WorldMapModel`，不能出现扣矿后未入包。
- 岑烬求救：跑 `IDEA0029CenJinRescueRuntimeTests` 与 `IDEA0029CenJinDistressPresentationTests`。重点检查发现、`10` 生物质预留、`12` 秒读条、及时/延迟结果、人口 `+40`、关注度 `+5`、取消回滚和重复加载幂等；领域失败先查 `CenJinDistressRuntime`，显示或按钮失败再查 `GrayboxCenJinDistressPresenter3D` 与探索 View。
- 前哨状态与警报：跑 `IDEA0029OutpostStateCatalogTests`、`IDEA0029OutpostRuntimeTests`、`IDEA0029OutpostAlertRuntimeTests`。通信、补给、维护继续读取 `SettlementRuntime`，断联不自动等于停产；警戒/受袭/危急只能消费权威威胁事实，确认不等于解决，点击只定位而不强制切换控制。
- schema `37`：跑 `FormalSaveSchema37ContractTests`、`IDEA0029ExplorationSaveAdapterTests`、`GrayboxFormalSaveCoordinatorTests`、`GrayboxFormalSaveRuntimeHostTests`。先查旧 schema 哈希与 `36→37` 迁移，再查 exploration DTO、十一领域预检/提交/回滚；旧档岑烬不得补扣或补发，实时视野、临时遮罩和 UI 状态不得入档。
- 正式 3D 场景与真实输入：EditMode 跑 `IDEA0029ExplorationController3DTests`、`IDEA0029ExplorationUiProjectionTests`、`IDEA0029FogPresentationTests`、`IDEA0029CenJinDistressPresentationTests`；PlayMode 以 `GrayboxProductionObservabilityRuntimeInputTests` 内的 `IDEA0029_` 用例为当前入口，必须经过真实 `L`、按钮和世界选择主循环。目录登记或直接调用内部方法都不等于运行时验证。
- 完成本阶段聚焦检查后，仍需日常完整 EditMode、完整 PlayMode、项目质量门、无界面编译、三项现役 3D 构建、官方文档生成/校验与 `RecordVerification`；人工试玩和真实 Windows 验收只能由实际结果确认。

## IDEA-0027 高级科技状态与 schema 35 检查边界

目录与效果先运行 `IDEA0027ResearchCatalogAndStatusTests`、`IDEA0027ResearchRuntimeEffectsTests` 和 `IDEA0027DefenseTowerCatalogTests`，确认 44 节点、49 边、15 个原预览节点、文明 Lv.2 双门、状态配置与研究继承没有漂移。防御规则运行 `IDEA0027DefenseTechnologyRuntimeTests`：过载阶段、来源—目标一秒窗口、剑意满层、感染周期/稳定连锁、共鸣非递归、精神操控、主/压力战役隔离、建筑运行资格、核心护盾精确恢复和暂停都必须通过。军队与角色运行 `IDEA0027ArmyTechnologyEffectsTests`，检查傀儡容量、巨兽生命比例、组织再生、基因特质、死亡清理和恢复顺序。

正式存档重点运行 `FormalSaveSchema35ContractTests`、`IDEA0027ResearchEffectStateSaveAdapterTests`、`GrayboxFormalSaveCoordinatorTests` 和既有 schema `31→32→33→34→35` 回归。至少覆盖：`34→35` 空迁移；多座来源塔对同一敌人的周期余量往返；未知效果、错误塔、悬空目标、重复 pair、非法阶段/层数/时间；受控友军跨波但不污染敌方计数；死亡角色活动特质；未知或未完成研究的奖励键；后续领域失败时全域回滚。schema `34` 的旧哈希必须先按旧投影验证，不能把新字段混入旧哈希。

真实 UI 运行 `IDEA0027TechnologyStateRuntimeInputTests` 以及科技树、M/P 和开发管理台既有输入夹具。主动过载必须通过正式 Input System 与 UGUI 按钮触发；状态搜索和中文动作必须操作权威 owner；输入焦点、Esc 和模态不得穿透。Release 编译与 Windows Release 构建继续确认管理入口不可达。

本阶段没有修改地形源、地形导入规则、Texture2DArray Builder 或数组内容，日常完整 EditMode 使用 `-testCategory '!TerrainAssetDeep'`。最终还要完成完整 PlayMode、项目质量门、Windows Release 3D、Windows Development 3D、macOS universal 3D、官方文档生成/校验和 `RecordVerification`。这些自动化不能写成用户已经试玩，也不能代替真实 Windows 10 和 11 的视觉、GPU、显存和内存验收。

## IDEA-0018 地图视觉、镜头层级与 UI 比例检查边界

`IDEA-0018` 当前为“已实现待验证”。地图玩法仍固定为 `64×48`、seed `8128`、3072 格和 schema `32`；任何资源节点、通行、放置、建筑坐标或 world signature 变化都不是本轮视觉重制。地形源、四通道生成器或数组变化必须运行 `FirstArtTerrainVisualStyleTests`、`FirstArtTerrainAssetBuilderTests` 和完整 `TerrainAssetDeep`；Shader/材质与七层综合色运行 `FirstArtTerrainShaderTests`。镜头滚轮、模态阻断、Near/Mid/Far 与定向采矿覆盖运行 `GrayboxCameraAndInputTests`、`GrayboxVisualAndWorldTests`、`GrayboxBuildingProjectionAndViewTests` 和 `GrayboxRuntimeSceneTests`。

UI 比例先查 `FormalUiLayoutPolicy3DTests`、`FormalUiResponsiveLayout3DTests` 与 `GrayboxSceneContractTests`；目录、生产、修改器和防御的点击/滚动必须继续通过真实 Input System 的 `GrayboxBuildingRuntimeSceneTests`、`GrayboxProductionObservabilityRuntimeInputTests`、`GrayboxDeveloperModifierRuntimeInputTests` 与 `GrayboxDefenseRuntimeInputTests`。建造栏保持 `620×54` 和既有位置；大型面板使用安全区/滚动，不能用整体缩小绕过字号问题。

发布级证据还要在真实 Unity GUI 运行 `FirstArtTerrainEvidenceCapture.StartAutomatedCapture`，并显式提供仓库外的 `WASTECITY_FIRST_TERRAIN_RUNTIME_RESULT`。工具使用隔离临时存档进入游戏，不得读写用户真实存档；manifest 必须包含 15 张截图、10 帧缩放、Near/Mid/Far、300 连续水面帧、颜色/动态门和逐文件 SHA-256。自动截图只能证明技术合同，最终仍需用户检查地貌区分、接缝、遮挡、字体与比例，并在真实 Windows 10 和 Windows 11 检查 GPU、显存和内存。

## IDEA-0019 地图 v2、资源布局与世界尺度检查边界

`IDEA-0019` 当前为“已实现待验证”。它明确承接 `IDEA-0018` 当时排除的地图真值变化：地图仍为 `64×48`、3072 格、seed `8128` 和 schema `32`，但 world generation/signature 升为 v2，地形、Traversal、出生/路线与资源节点改为覆盖全图的确定性连片布局。旧 v1 world identity 必须在任何运行时应用前以“存档世界配置与当前正式世界不兼容”明确拒绝，不能把旧坐标、节点余量或建筑绑定猜测迁移到新图。

地图和资源先运行 `WorldMapTests` 与 `GrayboxWorldLayout3DTests`：固定 v2 identity、重复生成、固定 `8×6` 宏格模板、每宏格两个整数扰动通道、一轮清理、出生保护区、关键区双通路和正式目录明确登记的 `24` 个坐标（铁矿 `8`、石料 `4`、能晶 `4`、水 `4`、生物质 `4`）。本版资源不是评分/搜索/地形亲和算法生成，测试应逐项比对目录坐标、类型和储量。A16.3 的两个安全铁矿、至少一个安全石料点和三个裂谷铁矿必须包含在该总数内；铁矿、石料和能晶节点还要通过既有 `BuildingResourceNodeCompatibilityRulesTests` 与 `GrayboxBuildingProjectionAndViewTests` 证明存在完整 `2×2` 采矿锚点。水和生物质保持世界来源，但不因本次地图重排被改成采矿站兼容资源。路径、部署和正式场景继续补跑 `CityPathfinderTests`、`CityTerrainRulesTests`、`CityDeploymentRulesTests`、`GrayboxMobileCityController3DTests` 与 `GrayboxRuntimeSceneTests`。

存档身份运行 `GrayboxFormalSaveWorldCityTests`、`GrayboxFormalSaveRuntimeHostTests` 和正式 3D round-trip PlayMode：generation `2`、signature `core.world.formal-3d.v2.64x48` 与 schema `32` 必须分别断言，v1 拒绝不能改变当前 WorldMap、城市、人口或导航状态。`GrayboxWorldLayout3DTests.IDEA0019_Seed8128V2WorldMatchesStableGoldenHash` 已保存当前 3072 格按 `y/x|terrain|traversal|resource|amount` canonical 编码计算的 SHA-256 `2f0ecd374ad3a1bf6fd50564d949741618c7ce1b72bc6619f67acda632b1e6fd`；地图真值变化必须同步 generation/signature 或经批准更新该 hash。地表控制图、唯一 Renderer 和 Ruins/Cliff 继续运行 `FirstArtTerrainControlMapTests`、`FirstArtTerrainRendererTests`、`FirstArtTerrainSceneContractTests`、`FirstArtTerrainRuntimeSceneTests` 及 Ruins/Cliff 的 Layout、Geometry、Presentation、SceneContract 测试；不得增加第二套地图判断、逐格常驻对象或绕过 14 Prefab、两类合批、13 材质与分类回退。

世界比例先运行 `FormalWorldPresentationScaleProfile3DTests`，再运行 `GrayboxBuildingProjectionAndViewTests`、`GrayboxVisualAndWorldTests`、`FormalUiResponsiveLayout3DTests` 与受影响的真实输入 PlayMode。30 座 `BuildingCatalog` 的逻辑 footprint 和四向旋转必须不变；地面/内城建筑的施工、完成、废墟、预览和选择框只消费正式表现 Profile，三座正式塔的 BuildingWorldView 基础座统一为 74% footprint、精确 `.14` 格高。矿点默认正交尺寸 `13` 为 Mid，只显示图标与储量；当前三档目标分别为 Near Frame `68`、Icon `50`、Text `22`，Mid Frame `56`、Icon `42`、Text `20`，Far Icon `28`，其 Frame、Icon、Text/Shadow Renderer 显隐和物理像素换算必须在 `1280×720`、`1920×1080`、`2560×1440` 与紧凑窗口下读取实际结果。标签避让还必须验证相同输入重复得到相同可见集合、冲突时稳定 ID 决胜、引导/选中优先且至少保留 `6px` 间距。建造栏继续保持 `620×54`，目录 Hero 图标使用正式 `64` 语义尺寸，不允许用散落常量重新放大。

日常实现阶段若没有修改地形源、导入规则、Texture2DArray Builder 或数组本身，仍按普通 EditMode 路由，不因 WorldMap 内容变化反复重建四个 2K 数组；正式收口按本需求批准的发布级验收补跑一次 `TerrainAssetDeep`，随后完成日常完整 EditMode、完整 PlayMode、项目质量门、三项现役构建、GUI 固定证据、文档生成/校验和 `RecordVerification`。自动化不能替代用户对地貌布局、矿区密度、建筑/图标比例、遮挡和真实 Windows GPU/显存/内存的判断。

## IDEA-0022：军队、多城市与外交继承

F3 聚焦运行 `ArmyUnitCatalogTests`、`SingleCityArmyModelTests`、`ArmyExpeditionModelTests`、`ArmyPersistenceModelTests` 与旧 `FriendlyUnitTacticalRulesTests`，检查四单位数值、制造/维护、休眠、五类命令、领袖 1.2 倍、确定性远征、伤亡和返城战利品。F4A 运行 `IDEA0022WorldLayerSettlementTransportTests`，检查主城引用、一次城、一前哨、独立库存/自治、查看与控制权、1.5 秒/格运输、5%/25% 风险和一次外交免疫。F5A 运行 `IDEA0022CharacterCatalogTests`、`IDEA0022CharacterLifeRuntimeTests`、`IDEA0022LeadershipPoliticsRuntimeTests`、`IDEA0022DiplomacyRuntimeTests`，检查倒地救援/预约退款、恢复伤势、死亡遗体、议会、继承/政变、派系支持、接触/报价/协议和关系门。

schema `34` 必须运行 `FormalSaveSchema34ContractTests`、既有 schema `31→32→33→34` 迁移回归、`GrayboxFormalSaveCoordinatorTests` 和真实 `GrayboxCivilizationExpansionRuntimeInputTests`。后者必须从正式启动页开始，以真实键盘 `M/N/P/Esc` 和真实 UGUI 指针操作面板，并至少保存/继续一次已改变的小队命令；只直接调用内部方法不能替代该输入证据。地图仍固定 `64×48`、seed `8128` 和 24 个资源节点，本轮未修改地形源、导入规则、数组 Builder 或数组内容，因此日常完整 EditMode 使用 `-testCategory '!TerrainAssetDeep'`；自动化不能写成用户已验收单位标记比例、次城/前哨辨识度或政治信息可读性。

## IDEA-0023：文明扩展透明图与公告板

资产与管线聚焦运行 `Production2DCivilizationExpansionVisualPipelineTests`：增量 manifest 必须精确包含 Unit 4、Character 2、WorldMarker 3、UI 页签 3 和状态徽记 8，共 20 项；母版和交付图必须为真 Alpha PNG，符合精确尺寸、安全区和透明四角，并逐项锁定 Unity GUID、源图/交付图 SHA-256、Pivot 与 Border；`Production2DUnitImportPolicy` 只能接管 `Assets/_Game/Art/Production2D/Units/` 顶层四张 PNG。`Production2DVisualClass.Unit` 只能追加在旧枚举末尾，不能重排既有序列化值；统一目录最终精确为 140 项，七个 Atlas 的扩展计数为 Units 4、Characters 3、WorldMarkers 5、UI 18。

消费者接线运行 `GrayboxCivilizationExpansionVisualIntegrationTests`，并补跑 `Production2DVisualCatalogAtlasTests`、`Production2DUiCharacterMarkerPipelineTests`、`GrayboxCivilizationExpansionUiInputTests` 与真实 `GrayboxCivilizationExpansionRuntimeInputTests`。Presenter/View/Controller 只按稳定内容 ID 解析 Sprite，将单位与角色投影为 UI 图像，把有权威坐标的小队、次城、前哨和运输队投影为垂直世界公告板，并由当前领域状态选择徽记；没有独立坐标的角色不得伪造世界位置。坐标、单位组成、生命、运输进度、通信、忠诚、选择和命令仍来自 CivilizationExpansion 权威快照。比例、世界高度、锚点、排序、运行时染色和 fallback 都是表现配置，不得写入 schema `34` 或反向改变领域状态。

本轮的 20 张透明图和公告板只是“无需建模即可完成”的 F6 表现层，不代表单位、角色、城市或车辆的正式 3D 模型、骨骼、动画、VFX、SFX 已完成。自动化检查 Alpha、尺寸、目录键、导入、Atlas、fallback、稳定对象数和真实输入；用户仍需试玩确认人物风格、单位轮廓、近中远比例、遮挡和信息密度，真实 Windows 10 与 Windows 11 仍需实际检查 GPU、显存和内存。只有修改地形源、导入规则、Texture2DArray Builder、数组生成或准备发布时才运行 `TerrainAssetDeep`；新增 Units/Characters/WorldMarkers/UI 图片不触发地形深度套件。

## IDEA-0024：科技树、开发验收台、城市比例与建筑世界图

`IDEA-0024` 当前为“已实现待用户复验”，没有修改 `64×48` v2 地图、ResearchCatalog 真值或正式 schema `34`。用户先后否决了只复刻三段结构的 v1 和质感偏空黑的 v2，因此科技树聚焦必须运行 `IDEA0024ResearchTreePresentationTests`、`ResearchTreeProjection3DTests` 和 `ResearchTreeUiContractTests` 的 reference-fidelity v3 几何门：背景 manifest、v3 母版和交付图保持同一 `1920×1080` 不透明面板 PNG 与稳定 GUID/SHA-256；视图必须全屏且位于普通 HUD 之上、系统菜单之下，顶部五组槽在同一行，底部六舱逐一存在，普通/桥/公共根三种尺寸分别为 `180×58`、`90×112`、`350×74`。Production2D 目录仍为 `141` 项、UI Atlas `19` 项，背景不能烘焙文字、科技/材料图标、节点或按钮；运行时节点必须复用项目已有科技 Sprite、材料 Sprite 与真实数量。投影继续派生正式 `44` 节点、`49` 边和 `6` 个双前置桥，两张桥卡按真实前置路线语义占据三个路线间沟槽与两层货架，所有连线仍连接真实前置并保持外描边/内层、桥虚线、向上方向且不拦截 raycast。状态只能消费 `Locked/Researchable/Active/Completed` DTO，不能解析中文文案；打开时选中最新可研究项但保持全树概览。

Development 验收入口和可点击页签先运行 `IDEA0024AcceptanceAndClickableTabsTests`，再运行真实 Input System 的 `IDEA0024AcceptanceAndTabsRuntimeInputTests`。Release 的启动控件必须保持原集合；只有 Editor/Development 可以追加“验收管理台、继续、新游戏、返回”四个控件。验收命令必须复用正式继续、新游戏、覆盖确认和 EnterGameplay，只有成功进入游戏后才可打开开发修改器。M/N/P 仍可由键盘互斥打开，面板内三枚真实 UGUI 页签必须复用同一个面板，每次点击只发布一次页变更，持续阻断世界点击，且不得改变城市目的地或创建待提交地图目标。

城市与建筑表现运行 `IDEA0024CityBuildingPresentationTests`，并补跑 `FormalWorldPresentationScaleProfile3DTests`、`GrayboxBuildingProjectionAndViewTests`、`GrayboxMobileCityController3DTests`、`GrayboxSceneContractTests` 和真实 `GrayboxBuildingRuntimeSceneTests`。正式 Profile 必须冻结 `8×6` 内城平台、`(-4,-3)` 锚点、`8×6` 平台尺寸和地面/内城均为 `1` 世界单位的格尺度；`MobileCity`、`MobileCityVisual`、`InnerCityPlatform` 的场景身份保持稳定，视觉体量与玩法碰撞体分离。BuildingCatalog 全部 `35` 项已完成建筑必须只有一个可见、屋顶净空、竖直且朝向相机的 world Sprite；施工中与废墟隐藏，重复 300 次更新不得增长子物体或监听。Sprite 是 billboard 回退，不等于正式建筑 3D 模型。

以上自动化只证明目录、布局、输入、坐标和表现合同已经接线；本轮正式建模、用户对科技树密度、背景、城市比例、建筑轮廓与 M-N-P 点击手感的人工视觉验收，以及真实 Windows 10 和 11 的视觉、GPU、显存和内存验证均未完成。没有修改地形源、地形导入规则、Texture2DArray Builder 或数组内容时，日常检查不运行 `TerrainAssetDeep`；准备发布时仍按发布门执行。任何自动化或 Development 验收台结果都不得写成上述人工与 Windows 验收已完成。

## IDEA-0025：透明主体比例定位

先运行 `IDEA0025Production2DVisualScaleTests` 和 `Production2DVisualCatalogAtlasTests`。目录 141 项必须全部具有 `0..1` 内的非零 `visibleBoundsNormalized`；同一语义槽中，不同透明留白的图经过 `Production2DVisualScalePolicy3D` 后必须得到相同可见长边占比。若图标仍忽大忽小，先检查目录是否重建、稳定 ID 是否解析到正式 Sprite，再检查消费者是否调用统一 framing；不要先改 PNG、PPU 或在单个界面追加倍率。

矿点比例先运行 `GrayboxVisualAndWorldTests` 检查既有 Near/Mid/Far 和 Marker 稳定性，再在 Player 截图中确认 Item Sprite 的 Quad 只显示 Alpha 主体、frame 保持完整，并确认三档投影高度对应实际主体；当前自动化不单独证明最终屏幕像素观感。建筑运行 `IDEA0024CityBuildingPresentationTests`，用可见主体底边而不是完整透明 Sprite bounds 检查屋顶净空。科技和其余 UI 运行 `IDEA0024ResearchTreePresentationTests`、`ResearchTreeUiContractTests`、`Production2DTechnologyIconPipelineTests`、`GrayboxCivilizationExpansionVisualIntegrationTests`，并用真实 `GrayboxProductionObservabilityRuntimeInputTests` 检查背包、合成、仓库与科技树输入链。背景必须 `preserveAspect=true`。这些自动化不能代替用户对 1920×1080、1280×720、2560×1440 和 16:10 的实际视觉判断。

## IDEA-0020 关注度、命轨与文明升阶检查边界

`IDEA-0020` 当前为“已实现待验证”。第一片只建立纯 C# 正式关注度来源目录与运行时，不接场景、HUD、命轨选择、压力战斗或存档 schema。来源配置先运行 `FormalAttentionCatalogTests`：必须精确登记 GDD A16.6 的 22 项稳定来源、初始 `10`、范围 `0–100`、历史容量 `128`、最近原因 `3` 条和 `30`、`60`、`90` 三个阈值，未知 ID 不得回退到任意默认项。

数值、历史和恢复运行 `FormalAttentionRuntimeTests` 与旧 `FormalProgressionTests`：一次性来源按原因锁存，可重复来源按稳定事件键防重；正负变化都夹在 `0–100`，即使封顶后的实际变化为零也要消费并记录事件；完整历史只保留最近 `128` 条，HUD 所需投影只取最后三条；阈值降低后不撤销、再跨越不重复。恢复必须保留语法有效但当前未知的历史原因为只读孤儿证据，非法快照失败时保持原对象、revision 和缓存快照身份不变。静止状态连续 `300` 次 `Capture` 必须返回同一不可变快照且托管分配为 `0 B`。

固定三命轨状态运行 `FormalFateCatalogTests`、`FormalFateRuntimeTests` 和旧 `LegacySelectionTests`、`LegacyEffectTests`。正式目录必须始终按袖珍宇宙、虚空债、回溯锚点顺序只给三项，继续使用稳定 `core.legacy.*` 字符串但不得引用 `WasteCity.Legacy` 历史代码；新运行时初始为未选择、等级 `0`，只允许一次正式选择并变为等级 `1`。恢复只接受固定候选顺序以及“未选等级 0”或“已选等级 1”，非法状态不得改变 revision 或缓存快照。本片 `EffectsReady` 必须保持 `false`，也不得公开升级命令。

schema `33` 核心合同运行 `FormalSaveSchema33ContractTests`、`FormalSaveSchema33MigrationTests`，并补跑 `FormalSaveEnvelopeTests`、`FormalSaveValidatorTests`、`FormalSaveFileTransactionTests`、历史 schema `32` 战役/废墟测试、波前重试、Coordinator 和 RuntimeHost。schema `31` 必须先按原格式验证 hash，再固定迁到 `32` 的战役结构，最后生成 `33` 的清洁 progression；schema `32` 同样先按不含 progression 的历史 payload 验证 hash，再生成关注度 `10`、固定三候选待选、文明等级 `1`。迁移不得从旧建筑、科技、战斗、旧 observation 或 legacyPath 字段反推历史。当前 schema `33` 源文档必须显式包含 progression 各数组，缺失不能依赖 JsonUtility 字段初始值伪装成有效。

正式三维进程适配器运行 `GrayboxFormalProgressionSaveAdapterTests`、`GrayboxFormalSaveCoordinatorTests` 与 `GrayboxFormalSaveRuntimeHostTests`。Capture 必须从唯一 Attention/Fate 运行时深拷贝 DTO；Prepare 用临时运行时规范化验证而不写真实状态；恢复计划绑定 adapter owner、Prepare 时两个缓存快照身份并只能提交一次。Coordinator 固定八域顺序为 World/City、Building/Storage、Economy、Production、Progression、Defense、Evacuation、Pause，capture、apply 和 rollback 必须共用同一有序数组。RuntimeHost 只创建唯一纯 C# 运行时与适配器，不接 HUD 或命轨效果；开始新进度必须恢复清洁 progression，避免同一 Host 串档。

关注度领域事件接线运行 `GrayboxProgressionEventIntegrationTests`，并补跑 `GrayboxBuildingSessionTests`、`CityDeploymentRulesTests`、`FormalResearchRuntimeTests` 和 RuntimeHost 回归。事件路由器只能订阅城市首次展开的正式 checkpoint、建筑会话已提交的 `BuildingCompleted` 事实与研究运行时的自然 `Completed` 事实；不得扫描场景、在 `Update` 轮询或从 UI/表现对象反推完成。建筑恢复、配置和重复绑定不发布完成事件；首次采矿站、冶炼厂、装配厂靠一次性 reason 去重，每一座机枪塔靠稳定实例键独立记账。

正式命轨首次选择必须由同一路由命令协调命轨状态与关注度：任一关注度提交失败时回滚命轨选择，不能留下“已选命轨但缺少关注度历史”的半提交状态。此片仍未接 HUD、真实玩家输入、三条命轨实际效果、压力遭遇或文明升阶，`EffectsReady` 必须继续为 `false`。

关注度 HUD 运行 `GrayboxProgressionPresentationTests`、`FormalAttentionCatalogTests`、`FormalUiLayoutPolicy3DTests` 和 `FormalUiResponsiveLayout3DTests`。正式目录拥有 22 项中文原因、未知历史原因回退、四个阶段与下一未锁存阈值计算；降低关注度后不得重新指向已经锁存的阈值。HUD 只读取 Attention/Fate 的不可变快照，静止快照不重复刷新；顶部关注度槽与资源栏、速度栏使用正式布局槽，不能靠覆盖或缩小资源数字接入。

真实输入运行 `GrayboxProgressionRuntimeInputTests`、`GrayboxUsabilityRuntimeSceneTests` 与 `GrayboxSceneContractTests`：必须从正式新游戏入口进入，用真实 UGUI 指针点击关注度状态打开最近三条详情；详情打开时建造、背包、研究、修改器和世界输入不得穿透，`Escape` 只关闭详情并清理 UI 焦点，下一输入帧恢复正常。`EffectsReady=false` 时可以准备固定三命轨展示资料，但不得强制打开选择界面，也不得向玩家声称命轨效果已经启用。

三命轨效果的基础片分别运行 `PocketUniverseFateEffectTests`、`PocketUniverseProductionIntegrationTests`、`GrayboxPocketUniverseFateControllerTests`、`FormalVoidDebtRuntimeTests`、`GrayboxVoidDebtIntegrationTests`、`FormalRewindAnchorStoreTests` 与 `GrayboxRewindAnchorServiceTests`。袖珍宇宙必须从正式机器生产目录推导类别，旗舰资格稳定且不转移，完整批次按 Lv.1×2/Lv.2×4 在写入前检查容量，输入和周期不变；首次真实旗舰批次只提交一次 `+4`，Attention 失败时回滚首次生产事实；纯坍缩命令只能按旗舰稳定 ID 生成一次。虚空债只有正式建造付款策略可以现金加债务，格子或表现失败必须同时回滚现金和债务；同资源收入在城市库存提交前优先偿债，生产、研究、合成和转移仍不能借。回溯锚点 Store 必须使用独立隐藏内部槽和正式 codec/validator/原子文件事务，服务层读取前保留当前完整 Attention 并增加 `12`，再经 Coordinator 单次事务恢复；绝不复用玩家存档或波前重试槽，也不能递归保存锚点载荷。

三项 Lv.1 规则、Host、schema 33 effect DTO、虚空债规则时钟/关注度结算、坍缩伤害和事务回溯服务全部接线后，才允许 `EffectsReady=true`。随后运行 `GrayboxFateSelectionPresentationTests` 与 `GrayboxFateSelectionRuntimeInputTests`：正式新游戏与 schema 32 迁移后的待选状态必须显示三卡强制模态，卡片完整显示中文简介、Lv.1、Lv.2 与代价；第一次点击只进入二次确认，确认才经统一 Router 原子提交命轨 Lv.1 和关注度 `+5`。系统菜单优先于该模态，建造、背包、研究、移动和世界指针都不得穿透。

命轨专属详情运行 `GrayboxFateOperationsPresentationTests` 与 `GrayboxFateOperationsRuntimeInputTests`：关注度详情中的“命轨详情”必须按已选命轨显示旗舰/坍缩、分资源债务/结算或锚点槽；回溯锚点的创建、读取二次确认和清除按钮只能调用 Host 持有的正式 Service。真实读取必须恢复锚点世界、保留创建后的当前关注度并额外增加 `12`；面板与确认层逐层消费 `Escape`，且建造、背包、研究和世界输入不穿透。

文明升阶与命轨 Lv.2 先运行 `FormalFateLevelTwoCatalogTests`、`FormalFateRuntimeTests`、`FormalCivilizationAscensionRuntimeTests`、`GrayboxCivilizationAdvancementControllerTests`、`FormalRewindAnchorMetadataRuntimeTests`、`FormalRewindAnchorStoreTests` 与 `GrayboxRewindAnchorServiceTests`。静态目录固定袖珍宇宙产出×4/坍缩 4×4、虚空债 60 秒结算和回溯容量 2；纯升阶运行时仍只拥有四项条件投影与绑定 owner 的单次计划，跨 Civilization/Fate/Attention/所选效果的提交和失败回滚只能由 `GrayboxCivilizationAdvancementController3D` 负责。任一 owner 失败必须恢复六份提交前快照，不能留下文明 2、命轨 1、关注度缺失或效果等级不一致的半提交状态。

正式 Host 与保存边界运行 `GrayboxFormalSaveRuntimeHostTests`、`GrayboxFormalProgressionSaveAdapterTests`、`FormalSaveSchema33ContractTests`、`FormalSaveSchema33MigrationTests`、`FormalSaveValidatorTests`、Coordinator 和 checkpoint 回归。Host 只能从已完成研究、玩家已完成机枪塔、Pressure 的晶壳母体完成事实和 Production 不可变观测快照收集四项资格；真实 U 与按钮必须调用同一 `TryAdvanceCivilization`，一次性提交文明 `1→2`、命轨 `1→2`、所选效果 Lv.2、关注度 `+25` 和首次升阶检查点。`AdvancementSequenceModel` 单独拥有 `Scanning 2.5s → Confirmed 3s → Warning 4s → Results → Continued`，Host 使用 `GamePauseReason.Advancement` 获取/释放暂停；十波胜利必须使用独立 `CampaignVictory`，两者不得互相解除。schema `33` 保存 Civilization 完成锁、revision、已提交升阶 ID、Sequence 阶段/剩余规则秒数和三项 Lv.2 效果，Warning 与 Results 恢复后不得重放奖励。

升阶可观察化运行 `GrayboxCivilizationAdvancementPresentationTests`、`GrayboxCivilizationAdvancementInputCoordinatorTests`、`GrayboxSceneContractTests` 与 `GrayboxCivilizationAdvancementRuntimeInputTests`。View/Presenter 只读取 Requirements/Civilization/Fate/Sequence 不可变状态，显示四项清单、文明 Lv.1→Lv.2、所选命轨 Lv.2、关注度 `+25`、U 提示和四阶段文案，并只发布 Advance/Continue 请求。系统菜单、命轨选择/操作、关注度详情、文本输入、建造、背包、研究和结算优先于 U；演出模态打开后 B/E/T/W 与世界输入不得穿透。真实 PlayMode 必须从隔离正式新游戏 UGUI 进入，以公开 owner/开发测试夹具建立四项条件，再用真实 U 完成一次升阶；Warning 和 Results 各自保存、场景重载并真实点击 `Start.Continue`，确认文明、命轨、关注度和阶段不重复。

回溯锚点 Lv.2 还要补跑 `GrayboxFateOperationsPresentationTests`、`GrayboxFateOperationsRuntimeInputTests`、`GrayboxRewindAnchorServiceTests` 与 `GrayboxFormalSaveCheckpointTests`：元数据保持两个稳定槽，两个隐藏 Store 文件相互独立，第三次创建按稳定创建序号替换最旧槽；UI 必须显示并选择两个槽，以稳定锚点 ID 经过读取二次确认和 Host Service 恢复指定槽。跨等级读取必须覆盖“先在 Lv.1 创建槽一、升至 Lv.2 再创建槽二、读取旧槽一”：世界回到旧锚点，但当前文明/命轨继续为 Lv.2，两个现役锚点均保留，随后 Coordinator 捕获仍通过 schema 校验。命轨选择、锚点创建、读取和清理必须在 `FormalSaveCheckpointPolicy` 登记为可重复转换事件；相同稳定事件键在 pending 和 committed 后均拒绝重放，不同稳定键可再次入队，且这些事件不得写入 `CompletedMilestoneIds`。该检查点专项当前为 `23 项全部通过`。

文明 Lv.2 建筑升级运行 `BuildingUpgradeTests`、`GrayboxBuildingUpgradeControllerTests`、`GrayboxDefensePresentationTests` 与 `GrayboxDefenseRuntimeInputTests`。`BuildingUpgradeCatalog` 只定义机枪塔→重机枪塔（20 合金）和剑阵塔→御剑台（20 灵铁）两条正式升级；Controller 必须同时读取文明等级、Session 已完成研究和城市网络材料，再委托 Session 原位提交。成功后稳定实例 ID、占格、站点、朝向、完成状态和建筑总数不变，也不重发 `BuildingCompleted`；不满足条件时零写失败，表现替换或付款失败时必须把网格、实例定义、表现和材料全部回滚。Host 已把缓存的升级可用性投影接入 Defense 选中建筑 HUD，按钮只发布稳定实例命令、失败显示中文原因且点击不得泄漏为世界选择；当前专项证据为升级 EditMode `23 项全部通过`、真实升级 PlayMode `1 项通过`、Defense 回归 `7 项全部通过`。`core.research.alloy-armor` 和 `core.research.sword-riding` 仍是 `PreviewOnly`，所以“命令/HUD 已接”不等于自然玩家已能研究并使用升级；开发研究授予只用于到达命令和 UI 边界。

以上聚焦自动化证明 Task 8 的 Host、UI、真实输入、schema 33 往返、双槽操作以及建筑升级命令/HUD 已经实现；仍不能写成 IDEA-0020 全部完成。Task 9 修改器与 Task 10 性能稳定性已经具备下述聚焦检查边界，但升级科技的自然研究发布、完整 EditMode、完整 PlayMode、项目质量门、正式构建、用户试玩和真实 Windows 10 与 11 验收仍未完成。

Task 9 开发修改器运行 `GrayboxDeveloperProgressionCommandTests`、`GrayboxDeveloperModifierCatalogTests`、`GrayboxDeveloperModifierTests` 与 `GrayboxDeveloperModifierRuntimeInputTests`。Facade 只在 `UNITY_EDITOR || DEVELOPMENT_BUILD` 中存在，并通过中文名称或 stable ID 查询 24 个 Attention、Fate、Pressure、Boss、双锚点和首次文明升阶动作；Bootstrap 同时保留列表浏览、中文输入搜索和数值/当前资源/压力阈值/锚点参数，但只调用 Modifier/Facade 公共命令。查询、失败和无变化不得标记 `DeveloperModifierUsed`，真实改变才单向标记；强制命轨选择前仅 dev-only 真实 `0` 可置顶打开修改器，系统菜单仍最高，文本焦点 U 和 0、双 `Escape` 和世界输入抑制必须走真实 Input System。当前真实输入专项为 `2 项全部通过`，包含 `29→30`、`59→60`、`89→90`、命轨、升阶、正式 HUD 和保存重载；Release Bootstrap 仍只能返回不可用，修改器状态不能冒充自然流程证据。

Task 10 性能与稳定性运行 `FormalAttentionPerformanceTests`、`FormalFatePerformanceTests` 与 `CrystalBroodmotherPerformanceTests`，并保留既有 Defense 稳定、构建性能和场景合同。静止 Attention、Pressure Controller/HUD 与升阶 Presenter 预热后连续 300 次必须保持 `0 B`；60、75、90 秒 Warning 规则时间仍逐 tick 精确推进，但可见 snapshot/HUD 最多按 `0.1` 秒（10 Hz）发布，300 个 60 FPS 样本最多刷新 50 次且受 `64 KiB` 预算约束。三条命轨各 20 次 capture/restore 不得增长监听器、债务、锚点、旗舰或稳定 ID；Boss 继续复用 46 槽敌池和正式 `0.1` 秒固定步，所有生命、速度和 70% 和 35% 阶段数值只能来自唯一 `CrystalBroodmotherCatalog`，Campaign/Runtime/表现不得再声明第二目录。升阶 Presenter 必须消费 Civilization 的 `CanPrepareAscension`、目标等级和 Attention reason/reward 投影，不能在 UI 复制 `1→2` 或 `+25`。上述是本机聚焦门，不替代完整回归、Profiler 原始证据、正式构建或 Windows 实机验收。

压力遭遇当前已经接入正式 Defense、Host、schema `33`、HUD 与真实输入，但仍须按所有权分层验证。`AttentionPressureCatalogTests`、`AttentionPressureRuntimeTests` 和 `GrayboxAttentionPressureRuntimeControllerTests` 固定 30、60、90、60、75、90 秒警告、容量 3、十波活动时只排队、30 的教学波/首塔门槛、60 与 90 前序完成门槛及唯一 warning/active owner。`AttentionPressureCampaignCatalogTests` 证明三场压力使用可注入的 `SingleCityDefenseCampaignDefinition`，复用既有 `SingleCityDefenseCampaignModel` 固定步、生成、索敌、塔伤、敌人攻击、建筑伤害和统计算法；旧构造仍委托十波 Default，不能复制 `WaveDirectorModel` 另建战斗真值。

正式 Defense 串行接线运行 `GrayboxAttentionPressureDefenseControllerTests`、`GrayboxFormalDefenseCampaignRuntimeIntegrationTests` 和既有 Campaign persistence/checkpoint 回归：十波未胜利时拒绝压力，同一时刻最多一个 `activePressureCampaign`，现役塔、建筑生命和战损协调器继续是唯一 owner；压力清场后回写 `AttentionPressureRuntime.TryCompleteActive` 并恢复十波胜利沙盒。active pressure 的 capture/restore 必须复用 CampaignModel prepare/commit，定义 ID 不匹配、主战役未胜利或坏状态都零写失败。当前聚焦证据中 Campaign Definition 与旧 Campaign 回归为 `61 项全部通过`，压力 Defense/旧真实塔伤和建筑伤害回归为 `22 项全部通过2`，active pressure persistence 相关回归为 `25 项全部通过`；这些是本机聚焦自动化，不是完整发布回归。

压力存档运行 `GrayboxAttentionPressureSaveAdapterTests`、`FormalSaveSchema33ContractTests`、`FormalSaveSchema33MigrationTests`、`FormalSaveValidatorTests`、Coordinator、RuntimeHost、checkpoint 和波前重试相关测试。schema `33` progression 保存队列 entry、警告余量、active encounter、单波 Campaign 状态和已注入阶段事件；没有 active entry 时 active campaign 必须为空，ID、组成、敌人、阶段与队列交叉引用不一致必须拒绝。schema `32→33` 仍生成空压力默认值，不得从旧十波、关注度或敌人反推压力历史。Host 只用统一规则时钟推进压力，并在真实新游戏保存成功后才开放输入。

晶壳母体规则运行 `CrystalBroodmotherCatalogTests` 与 `CrystalBroodmotherEncounterTests`：4000 生命、0.6 格/秒、0.1 秒固定步，70% 的 4 晶壳兽、35% 的 6 啃噬者+2 啸叫者和唯一击败命令按稳定 ID 幂等。Boss 生命仍由 Defense Campaign EnemyState 唯一拥有，阶段逻辑只能观察权威生命并注入同一 Campaign，不能维护第二份可反写生命。`GrayboxDefensePresentationTests` 进一步验证 46 槽预建敌池、Burrower 可辨识占位和晶壳母体的明显比例、独立轮廓色、按正式最大生命计算的颜色/血条、中文阶段标记；普通敌比例和共享材质合同不变，刷新不得逐帧创建对象。该专项当前为 `29 项全部通过`，相关 Presentation/Observability/Snapshot 回归曾为 `40 项全部通过`。

压力 HUD 运行 `GrayboxAttentionPressurePresentationTests` 与 `GrayboxAttentionPressureRuntimeInputTests`。EditMode 必须用合法串行快照分三次覆盖 Queued、Completed+Warning、Completed+Completed+Active，不能为了同时展示所有文案构造两个 owner。HUD 显示排队、预警倒计时、进行中、已完成、晶壳母体状态和阶段，同一 snapshot 引用不重复刷新；详情使用真实全屏 UGUI 并复用统一模态输入门。PlayMode 必须从正式新游戏入口进入，经 Pressure command、正式 Pressure Defense Controller 和 DefenseRuntime 实际生成 Boss，再断言 `CrystalBroodmother.Placeholder`、`Outline`、`WorldHealthBar`、`Phase` 与详情点击/世界输入阻断，不能只把纯 PressureRuntime 恢复成 Active 来伪造 Boss。当前聚焦证据为 Pressure EditMode `1 项通过`、Pressure PlayMode `1 项通过`、HUD/WorldView 相关回归 `20 项全部通过`；新游戏入口阻断修复属于同一真实输入证据链。

遗产解析节点运行 `FormalResearchCatalogTests`、`ResearchTests`、`ResearchTreeProjection3DTests` 与 `ResearchTreeUiContractTests`：正式科技总数为 44、依赖边 49；`core.research.legacy-analysis` 为 Technology tier 3 / row 4，前置自动防御，成本 30 合金+20 生物质，耗时 60 秒并保持自下向上布局与最新可研究聚焦。完成后的 `+12` 继续由自然 Research Completed 事件路由，恢复已完成节点不得重放。

上述聚焦证据与最终日常完整 EditMode 2803 项、完整 PlayMode 96 项、项目质量门和三项现役构建共同证明 Task 7 至 Task 10 的自动化与构建门已经完成；这仍不得写成用户试玩或真实 Windows 验收已经完成。晶壳母体和升阶界面的比例、轮廓、文案节奏与操作理解仍需用户人工试玩确认；真实 Windows 10 与 11 的 GPU、显存、内存、字体、输入和视觉结果必须保持未完成。后续仍按[IDEA-0020 设计规格](superpowers/specs/2026-08-26-idea-0020-progression-attention-fate-ascension-design.md)继续升级科技自然发布、完整回归和发布门。

## IDEA-0021 Lv.2、六桥节点与建筑贴图检查边界

文明 Lv.2 自然研究先运行 `CivilizationResearchAvailabilityTests`、`CivilizationResearchOperationsTests`、`FormalResearchRuntimeTests` 和 `FormalSaveSchema33ContractTests`：只允许合金装甲/御剑术在 Lv.2 投影为可研究，其余 PreviewOnly 不开放；Operations 状态、Latest 定位、实际扣料和存档交叉验证使用同一可用性规则。真实 `T`、搜索、节点选择和 Start 按钮运行 `GrayboxProductionObservabilityRuntimeInputTests`，不能用 Development grant 代替自然入口。

F2 核心运行 `IDEA0021BridgeCombatTests`、`FormalResearchCatalogTests`、`ResourceRecipeCatalogIntegrityTests`、`GrayboxProductionRuntimeTests`、`GrayboxBuildingCatalogTests` 与正式 Defense 集成测试：固定 44 科技、35 建筑、33 建造卡、33 配方；六桥必须有双前置和 90 秒时长。机甲厂与生物机库只是 F3 前驱产线，不得把资源产出写成已部署机甲/巨兽。EMP 只抑制明确 Mechanical 目标的下一次移动；血肉灵丹的三倍治疗、20% 反噬和失败不扣必须运行 `GrayboxElixirUseCommand3DTests`，并用真实 `E → 城市库存灵丹 → 使用灵丹` 鼠标路径验证。

科技树运行 `ResearchTreeProjection3DTests`与 `ResearchTreeUiContractTests`：44 节点/49 依赖边继续自下向上，共享主干不按子节点重复过绘，junction、分支、向上箭头和金色双路线桥稳定。建筑贴图运行 `Production2DBuildingIconPipelineTests`、`Production2DVisualCatalogAtlasTests`、`GrayboxBuildingProjectionAndViewTests` 和正式场景 PlayMode：30 张旧图 + 5 张新图覆盖 `BuildingCatalog.All`，1024 母版保留 10% 透明安全区，256 交付图进入 Buildings Atlas；完成建筑显示稳定 billboard，施工/遗迹隐藏，原位升级不增加子对象。这些证据不代表正式 3D 建模、用户视觉验收或真实 Windows 验收完成。

## IDEA-0011 生产与界面的检查边界

`IDEA-0011` 的生产、背包、应急合成、六节点兼容研究和资源状态栏已经实现待验证；`IDEA-0016` 当前正在把它扩展为 31 种资源、30 条配方、正式研究运行时和 43 节点科技树。排查正式研究规则、初始根、倍率、暂停、退款或 schema `31` 恢复时，优先运行 `FormalResearchRuntimeTests` 与 `GrayboxFormalResearchSaveAdapterTests`；排查节点数、依赖边、确定布局、缩放或视图层级时，运行 `ResearchTreeProjection3DTests` 与 `ResearchTreeUiContractTests`；排查真实 T、搜索字符、双 Esc、拖动、滚轮、Home、面板互斥或点击穿透时，必须补跑 `GrayboxProductionObservabilityRuntimeInputTests`。历史 `DemoResearchRuntimeTests` 继续作为六节点和退役内容兼容回归，不得替代正式目录测试。精确类名和当前归属仍以自动生成的[测试清单](Generated/Test-Inventory-ZH.md)为准。

不要把开发补给夹具当成自然开局证据。正式会话石料为 `0`，而现有冶炼厂施工需要 `6` 石料；自动链测试会先通过显式开发补给搭建 `2 采矿站 → 2 冶炼厂 → 1 装配厂`，再清零铁矿、合金和弹药等生产物资，观察节点采收和机器加工是否自动补出完整链。该测试只验证运行时生产闭环；自然开局的石料路径应由 `IDEA-0012` 的 seed `8128` 原始内容区可采石料节点及其场景测试单独证明。若试玩仍找不到或无法采集石料，应作为 `IDEA-0012` 回归记录，不要在测试里偷偷修改正式开局或建筑成本。

本阶段的聚焦 TDD 证据不能替代最终门：日常完整 EditMode、完整 PlayMode、项目质量检查、正式构建、文档生成和 `RecordVerification` 仍要在收尾时完成。真实 Windows 10 和 Windows 11 机器的视觉、GPU、显存、内存表现和用户试玩只能由实际执行结果确认；macOS 编辑器测试或跨平台构建成功不能替代这些结论。当前正式 3D 存档 schema 必须保持 `31`；已经退役的 2D 运行时不接新 UI 或新功能，schema `1–30` decoder、迁移和固定样本只做兼容回归。敌人、炮塔和弹丸不属于 `IDEA-0016` 本轮测试通过所能证明的范围。

## IDEA-0016 资源、配方、修改器与二维资产检查边界

`IDEA-0016` 当前是“开发中，主体已实现，待本轮完整验证”。聚焦检查已经覆盖部分子系统，但本节列出的类名只是收口入口，不代表完整回归、构建或人工验收已完成。

按责任边界定位：31 种资源定义、来源用途和发现条件先查 `ResourceDefinitionCatalogTests` 与 `ResourceDiscoveryProjectionTests`；30 条配方、机器/手工边界和研究引用先查 `ResourceRecipeCatalogIntegrityTests`；多输入原子预留、多输出容量和停工原因先查 `FormalProductionSimulationTests`；同建筑多配方、切换拒绝、未知配方和生命周期先查 `GrayboxProductionRuntimeTests`；非默认配方及无序预留的 schema `31` 往返先查 `GrayboxFormalSaveProductionTests`；详情中的全部输入/输出通道、暂停与资源转移先查 `GrayboxProductionObservabilityFacadeTests`。不要从界面文字重新推导配方、容量、物流连接或发现状态。

修改器目录、中文搜索和正式命令分别由 `GrayboxDeveloperModifierCatalogTests`、`GrayboxDeveloperModifierTests` 保护；数字键 `0`、文本焦点、双 Esc、世界输入抑制和 Release 无入口必须再跑 `GrayboxDeveloperModifierRuntimeInputTests` 与相关易用性测试。测试必须通过正式 Input System 主循环，不能只直接调用按钮方法。`F10` 继续无行为，stable ID 只用于内部键，主列表必须显示游戏中文名。

二维资产按类别运行 `Production2DItemIconPipelineTests`、`Production2DBuildingIconPipelineTests`、`Production2DTechnologyIconPipelineTests`、`Production2DUiCharacterMarkerPipelineTests` 和 `Production2DCivilizationExpansionVisualPipelineTests`；统一视觉目录与 SpriteAtlas 运行 `Production2DVisualCatalogAtlasTests`，文明扩展消费者运行 `GrayboxCivilizationExpansionVisualIntegrationTests`。当前目标目录为 140 个稳定视觉键、33 条配方视觉投影和七类 Atlas；失败时先区分四层：源母版和透明 Alpha、Unity 交付 PNG 与 `.meta`、导入规则、运行时目录/消费者。导入器只能作用于 `Assets/_Game/Art/Production2D/` 的批准子目录，不得触碰地形；GUID、尺寸、安全区、九宫格 Border、公告板锚点/比例、世界标记中央透明孔和重复构建字节稳定必须由资产测试读取。联络表、Alpha 检查和静态测试不能替代用户对正式图片的视觉判断，也不能推断真实 Windows GPU、显存和内存。

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
