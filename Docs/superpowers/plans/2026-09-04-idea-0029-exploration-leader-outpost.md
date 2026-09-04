# IDEA-0029 探索、领袖与前哨实施计划

## 0. 交付边界与不可破坏项

- 稳定需求 ID：`IDEA-0029`。实现顺序必须保持 RED→GREEN，不允许先写生产代码再补测试。
- 本轮只实现：三态迷雾、扫描与最后情报；领袖 AI/手动接管与低效率手工采集；前哨三状态和可点击受袭警报；岑烬招募前求救；schema `37`；正式 3D UI 和真实 Input System 输入。
- 继续复用现役 `64×48` v2 地图、seed `8128`、资源节点、角色/军队/城市/库存/关注度真值，不重排地图，不新增矿物，不制作新模型、动画、VFX、SFX，也不向冻结 2D 接线。
- 当前 `CivilizationExpansionRuntime.Characters + Politics.CurrentLeaderId` 是正式角色身份、生命和继承真值；`WorldMapModel` 是地形、节点和节点储量真值；`SettlementRuntime` 是前哨身份、库存、补给、维护和通信真值；`PlayerBackpackModel` 是手采输出真值。新系统只能引用或发起事务，不得复制这些状态。
- `GrayboxLeaderController3D` 只负责正式 3D 位置、移动和表现执行。旧 `LeaderModel` 不再扩展为第二份招募、受伤或角色生命真值；需要兼容时建立明确的只读桥接并由测试保护。
- 三态迷雾定义为 `Unexplored / Explored / Visible`：`Visible` 由当前视野源实时派生；只持久化探索历史与最后情报，不把瞬时可见性写进存档。离开视野后保留最后一次权威快照及更新时间，不继续读取实时对象。
- 默认视野半径全部进入正式配置：主城 `7` 格、次城 `5` 格、当前领袖 `4` 格、前哨 `3` 格、既有侦察无人机 `6` 格。格子首次进入实时视野时自动扫描，不增加平行的手动扫描按钮；离开视野后最后情报满 `60` 秒标记陈旧，满 `180` 秒标记过期。
- 领袖手采只允许目标距领袖不超过 `1.5` 格，每 `6` 秒取得 `1` 单位；这些数值由 `LeaderInteractionCatalog` 提供，不能写在 Controller 或 UI。
- 前哨的三态必须由现有补给、维护、通信和受袭事实统一投影，不允许 UI 自己推断。受袭警报可点击聚焦但不得自动抢镜头或直接改变 `ControlledCityId`。
- 前哨受袭警报固定为三个等级；等级升级、持续、解除和显示文案由正式目录与权威攻击事实决定。
- schema `36→37` 只做清洁、单向迁移；不根据旧游玩历史反推扫描、求救或警报已完成。恢复必须保持 prepare/apply/rollback 原子边界。
- 人工试玩、真实 Windows 10/11、GPU、显存和内存结论只能记录为待验，不能由自动化替代。

## 1. 先冻结领域契约和稳定目录

### RED

新增以下 EditMode 契约测试，先令其因类型、目录或规则缺失而失败：

- `Assets/_Game/Tests/EditMode/IDEA0029ExplorationCatalogTests.cs`
  - 固定三态迷雾枚举、自动扫描来源、主城/次城/领袖/前哨/侦察无人机 `7/5/4/3/6` 格视野，以及最后情报 `60` 秒陈旧、`180` 秒过期和允许字段。
  - 校验所有数值来自正式目录，不允许 Controller/View 散落魔法数。
- `Assets/_Game/Tests/EditMode/IDEA0029LeaderInteractionCatalogTests.cs`
  - 固定 AI/手动模式、手采允许资源、相邻 `1.5` 格、`6` 秒周期、每次 `1` 单位和背包限制。
  - 固定岑烬求救 stable ID、东南求救区域、即时/延迟分支、资源代价、人口与关注度结果。
- `Assets/_Game/Tests/EditMode/IDEA0029OutpostStateCatalogTests.cs`
  - 固定前哨三状态、状态优先级、三个警报等级、去重键和可点击目标。
- 扩展项目目录测试，要求新 stable ID、功能组和公共规则均可追踪：
  - `Assets/_Game/Tests/EditMode/ProjectQualityScannerTests.cs`
  - `Assets/_Game/Tests/EditMode/Production2DVisualCatalog3DTests.cs`（只有新增正式状态/操作图标时才修改；不得为测试伪造临时资源）。

### GREEN

新增纯目录与值对象，名称可在实现时按现有命名空间微调，但不得把它们放入 UI：

- `Assets/_Game/Scripts/World/Exploration/ExplorationCatalog.cs`
- `Assets/_Game/Scripts/Leader/Exploration/LeaderInteractionCatalog.cs`
- `Assets/_Game/Scripts/World/Exploration/OutpostAlertCatalog.cs`

完成目录聚焦测试后提交第一批领域契约。若正式文档中的数值仍未冻结，只允许先提交结构和失败测试，不得自行填入与 GDD 不一致的临时数值。

## 2. 三态迷雾、视野源与最后情报

### RED

新增：

- `Assets/_Game/Tests/EditMode/IDEA0029WorldVisibilityRuntimeTests.cs`
  - 新地图全部 `Unexplored`；进入任一合法视野源变为 `Visible`；离开后变为 `Explored`。
  - 主城、次城、当前领袖、前哨和既有侦察无人机分别使用 `7/5/4/3/6` 格目录半径，边界格、地图边缘和阻挡规则确定性一致。
  - 暂停不改变当前视野投影；同一组视野源无论输入顺序如何都得到相同结果。
  - 可见时刷新资源节点、敌人、城市/前哨等已批准情报；不可见时只返回最后快照和年龄。
  - 未探索格不得泄漏地形、资源、敌人或建筑；过期情报必须显式标记，不能伪装成实时状态。
- `Assets/_Game/Tests/EditMode/IDEA0029WorldIntelRuntimeTests.cs`
  - stable owner ID 去重、对象消失的最后已知状态、再次看见后的覆盖更新、非法/未知引用拒绝。
  - 离开实时视野后未满 `60` 秒仍为最近情报，满 `60` 秒为陈旧，满 `180` 秒为过期；精确边界和暂停时钟规则确定性一致。
  - 捕获/恢复只保存探索历史和最后情报；恢复失败不部分改写运行时。

### GREEN

新增纯领域实现：

- `Assets/_Game/Scripts/World/Exploration/WorldVisibilityRuntime.cs`
- `Assets/_Game/Scripts/World/Exploration/WorldIntelRuntime.cs`
- `Assets/_Game/Scripts/World/Exploration/WorldVisionSource.cs`

必要时只通过窄接口扩展：

- `Assets/_Game/Scripts/World/WorldMapModel.cs`
- `Assets/_Game/Scripts/CivilizationExpansion/CivilizationExpansionRuntime.cs`
- `Assets/_Game/Scripts/World/CivilizationExpansion/SettlementRuntime.cs`

`WorldVisibilityRuntime` 只消费坐标和视野半径；`WorldIntelRuntime` 只在格子可见时从权威领域快照复制允许字段。不得让 Renderer、公告板或 UI 成为探索事实来源。

完成后运行上述两个聚焦测试以及既有 `DeploymentAndHarvestTests`、`TerritoryNetworkTests`、`IDEA0022WorldLayerSettlementTransportTests`，确认没有改变节点储量、路径或 settlement 真值。

## 3. 自动扫描与探索奖励

### RED

新增 `Assets/_Game/Tests/EditMode/IDEA0029ScanRuntimeTests.cs`：

- 格子第一次由 `Unexplored` 进入 `Visible` 时自动扫描并写入探索历史；不要求额外按键、不创建扫描消耗或冷却。
- 同一格持续可见不重复发出首次扫描事件；离开后重入只刷新最后情报，不重复首次探索提交。
- 城市、领袖、前哨和侦察无人机使用同一扫描入口；视野源失效后只改变可见性，不撤销已经完成的探索。
- 首次扫描安全矿区/结晶裂谷时，通过现有关注度事件接口只提交一次正式来源；重复进入视野不得重复领取奖励。
- 探索历史、最后情报和首次奖励任一步准备失败时整体不发生；不同视野源同帧重叠也只提交一次。

### GREEN

新增：

- `Assets/_Game/Scripts/World/Exploration/ScanRuntime.cs`
- `Assets/_Game/Scripts/World/Exploration/ScanCommandResult.cs`

通过现有接口组合：

- `WorldVisibilityRuntime` / `WorldIntelRuntime`：揭示与刷新；
- `FormalAttentionRuntime` 或现役关注度入口：只提交已有 stable source ID；
- `CivilizationExpansionRuntime`：提供受控对象与正式坐标，不由扫描系统改写角色或城市。

`ScanRuntime` 只消费 `WorldVisibilityRuntime` 的“首次进入 Visible”领域事件，不读取键盘或 UI。自动扫描完成后运行关注度聚焦测试，验证首次幂等和来源账本没有旁路。

## 4. 统一领袖真值与 AI/手动控制状态机

### RED

新增：

- `Assets/_Game/Tests/EditMode/IDEA0029LeaderControlRuntimeTests.cs`
  - 移动态默认城市控制、岑烬由 AI 在内城活动；展开态且当前领袖 Active 时默认领袖手动控制。
  - 玩家可明确接管/释放；释放后进入 AI，接管时清除 AI 输入，不允许一帧同时应用两种意图。
  - 未招募、Downed、Recovering、Dead、无合法继任者或城市转换中时回退到合法目标并给出明确原因。
  - 暂停冻结移动和 AI 规则时间，但允许切换控制模式；保存/恢复后不得重放一次性输入。
- `Assets/_Game/Tests/EditMode/IDEA0029LeaderAiRulesTests.cs`
  - 最小 AI 只含休息、维修、防守和返回内城停靠点；按受伤、附近可维修目标、受袭事实确定稳定优先级。
  - 不可达目标、目标消失和切换为手动时安全回到 Idle/Return，不引入复杂行为树或 NavMesh。
  - 路径复用 `CityPathfinder` 与 `CityTerrainRules`，不得复制通行判断。
- 扩展 `Assets/_Game/Tests/EditMode/GrayboxLeaderControlTests.cs`，先固定正式角色真值桥接，禁止仅通过旧 `LeaderModel.Recruited` 决定正式控制权。

### GREEN

新增纯规则：

- `Assets/_Game/Scripts/Leader/Exploration/LeaderControlRuntime.cs`
- `Assets/_Game/Scripts/Leader/Exploration/LeaderAiRules.cs`
- `Assets/_Game/Scripts/Leader/Exploration/LeaderIntent.cs`

修改窄适配：

- `Assets/_Game/Scripts/City/DirectControlRules.cs`
- `Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs`
- `Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs`
- `Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs`
- `Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs`
- `Assets/_Game/Scripts/CivilizationExpansion/CivilizationExpansionRuntime.cs`

`LeaderControlRuntime` 产生控制目标和意图；`GrayboxLeaderController3D` 只执行合法位移并回报逻辑坐标。每次位移提交后同步正式 `CharacterLifeRuntime` 当前领袖坐标，使军队 FollowLeader、救援、库存访问和镜头读取同一位置。旧 `LeaderModel` 仅保留已有过载/装配兼容所需部分，招募与生命状态必须由正式角色域驱动。

完成后运行 DirectControl、Leader、Camera、Inventory Access 和 CivilizationExpansion 聚焦回归。

## 5. 低效率手工采集

### RED

新增：

- `Assets/_Game/Tests/EditMode/IDEA0029ManualGatherRuntimeTests.cs`
  - 只有已招募且 Active 的当前领袖距兼容且未枯竭节点不超过 `1.5` 格时可采集。
  - 每 `6` 秒完成 `1` 单位；暂停、释放手动控制、离开范围、节点枯竭或角色失效时停止并报告单一原因。
  - `WorldMapModel.Harvest` 扣减与 `PlayerBackpackModel` 入包必须构成原子事务；背包容量不足时不得损失节点资源。
  - 不读取城市库存，不获得自动物流加成，不长期超过采矿建筑基线；分帧推进与一次推进一致。
  - 两名调用者不能对同一节点重复提交同一采集批次。
- 扩展 `Assets/_Game/Tests/EditMode/ResourceTransactionAndCapacityTests.cs` 与 `DeploymentAndHarvestTests.cs`，固定容量失败回滚和节点枯竭兼容。

### GREEN

新增：

- `Assets/_Game/Scripts/Leader/Exploration/ManualGatherRuntime.cs`
- `Assets/_Game/Scripts/Economy/WorldHarvestTransaction.cs`

窄接入：

- `Assets/_Game/Scripts/World/WorldMapModel.cs`：提供 prepare/commit 或等价可回滚采收接口，继续保持节点唯一真值；
- `Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs` 与 `ResourceTransaction.cs`：复用容量、堆叠与原子转移规则；
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs`：只显示进度/失败原因和发出正式命令，不自行扣资源。

完成后运行生产、采矿站、背包、仓库和正式存档经济聚焦回归，确保手采没有旁路自动生产与物流。

## 6. 前哨三状态与受袭警报

### RED

新增：

- `Assets/_Game/Tests/EditMode/IDEA0029OutpostRuntimeTests.cs`
  - 从现有 `IsSupplied / IsMaintained / IsCommunicationActive` 与受袭事实投影且只投影一个当前主状态；按批准优先级覆盖所有组合。
  - 状态改变不清空库存、不瞬移物资、不改变地图坐标；恢复正常后继续既有自治 Tick。
  - 失联时停止接收远程命令并保留最后情报，重新通信后刷新实时状态。
- `Assets/_Game/Tests/EditMode/IDEA0029OutpostAlertRuntimeTests.cs`
  - 受袭事件使用 stable attack/settlement ID 幂等；三个警报等级的升级、刷新、解除和重复事件确定性一致。
  - 警报保存攻击等级、目标、发生时间和确认状态；点击只返回聚焦请求，不改变受控城市、不暂停游戏、不重复结算伤害。
  - 通信中断时只显示最后已知警报并标记陈旧，不能把隐藏攻击实时泄漏给玩家。
- 扩展 `IDEA0022WorldLayerSettlementTransportTests.cs` 和 Defense settlement 测试，固定库存/运输/自治不回归。

### GREEN

新增：

- `Assets/_Game/Scripts/World/Exploration/OutpostOperationalState.cs`
- `Assets/_Game/Scripts/World/Exploration/OutpostAlertRuntime.cs`

修改：

- `Assets/_Game/Scripts/World/CivilizationExpansion/SettlementRuntime.cs`
- `Assets/_Game/Scripts/CivilizationExpansion/CivilizationExpansionRuntime.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSettlementController3D.cs`

Defense 只发布受袭事实；OutpostAlertRuntime 负责幂等、等级和确认；WorldIntelRuntime 决定玩家看到实时还是最后情报；SettlementRuntime 保留前哨库存和自治所有权。

## 7. 岑烬招募前求救事件

### RED

新增 `Assets/_Game/Tests/EditMode/IDEA0029CenJinRescueRuntimeTests.cs`：

- 求救事件只引用 `CharacterCatalog.CenJinId`，在正式东南求救区生成一次；未探索时不可见，扫描/视野揭示后进入可追踪状态。
- 及时救援、延迟救援和危急提示按批准时间窗产生不同岑烬状态；危急不会永久删除角色，不得复用“已招募角色倒地 60 秒救援”冒充招募前事件。
- 成功原子提交生物质代价、人口 `+40`、岑烬招募、关注度 `+5` 和对应一次性来源；资源预留或任一跨领域事务失败时全部不发生。
- 即时/延迟只改变已批准的受伤与领袖效果，不创建第二个岑烬；重复交互、重新载入和检查点重放均不重复奖励。
- 求救超过及时窗口后按受伤结果落地，超过危急窗口仍可救援；同时保持 IDEA-0022 已招募角色的 Downed/Dead/继承状态机不变。

### GREEN

新增：

- `Assets/_Game/Scripts/Leader/Exploration/CenJinRescueRuntime.cs`
- `Assets/_Game/Scripts/Leader/Exploration/CenJinRescueTransaction.cs`

复用并窄改：

- `Assets/_Game/Scripts/World/RescueSiteModel.cs`：只保留或迁移通用位置/完成状态；不得继续作为孤立的平行求救真值。
- `Assets/_Game/Scripts/Leader/CivilizationExpansion/CharacterLifeRuntime.cs`
- `Assets/_Game/Scripts/Leader/CivilizationExpansion/LeadershipPoliticsRuntime.cs`
- `Assets/_Game/Scripts/Population/PopulationModel.cs`
- `Assets/_Game/Scripts/Progression/FormalAttentionRuntime.cs`
- `Assets/_Game/Scripts/CivilizationExpansion/CivilizationExpansionRuntime.cs`

招募事务必须由组合根协调：资源账户、人口、角色、政治和关注度各自仍由原 owner 提供 prepare/apply/rollback，不得由 UI 连续调用五个不可回滚方法。

## 8. schema 37 RED→GREEN

### RED

新增：

- `Assets/_Game/Tests/EditMode/FormalSaveSchema37ContractTests.cs`
- `Assets/_Game/Tests/EditMode/FormalSaveSchema37MigrationTests.cs`
- `Assets/_Game/Tests/EditMode/IDEA0029ExplorationSaveAdapterTests.cs`

扩展：

- `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`
- `Assets/_Game/Tests/EditMode/FormalSaveValidatorTests.cs`
- `Assets/_Game/Tests/EditMode/GrayboxFormalSaveCoordinatorTests.cs`
- `Assets/_Game/Tests/EditMode/FormalSaveWaveRetryStoreTests.cs`
- 既有 schema `31–36` 迁移链、Rewind Anchor 和 PlayMode round-trip 测试。

必须覆盖：

- `36→37` 清洁默认：保留既有地图资源量、旧 revealed 历史、角色、settlement、命轨和科技；新扫描进度、警报和岑烬求救不得按旧历史补发。
- 三态迷雾只保存 explored 位与最后情报；载入后 Visible 从当前视野源重建。
- LeaderControl 保存正式控制模式与必要的 AI/手采进度；不保存按键瞬态和 Transform 表现缓存。
- 前哨状态保存其权威事实与活动警报，投影枚举可重建时不重复保存。
- 岑烬求救保存 stable site/event ID、阶段、计时和一次性提交键。
- 未知/重复 stable ID、越界格子、非法枚举、负计时、未来时间、悬空对象、互斥阶段、篡改 hash 全部拒绝且不部分恢复。
- prepare/apply 任意阶段失败时完整回滚世界探索、领袖交互、前哨警报、角色、人口、库存和关注度。

### GREEN

修改：

- `Assets/_Game/Scripts/Persistence/FormalSaveEnvelope.cs`
- `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- `Assets/_Game/Scripts/Persistence/FormalSaveCodec.cs`
- `Assets/_Game/Scripts/Persistence/FormalSaveValidator.cs`
- `Assets/_Game/Scripts/Persistence/FormalSaveStore.cs`
- `Assets/_Game/Scripts/Persistence/FormalSaveWaveRetryStore.cs`
- `Assets/_Game/Scripts/Persistence/FormalRewindAnchorStore.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs`

新增：

- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxExplorationLeaderOutpostSaveAdapter3D.cs`

为 schema 36 保留专用旧 hash 投影；先让 36→37 单步迁移通过，再验证 31→32→33→34→35→36→37 完整链，不改写历史 schema 语义。

## 9. 正式 3D UI、世界表现和真实输入

### RED

新增 EditMode：

- `Assets/_Game/Tests/EditMode/IDEA0029ExplorationUiProjectionTests.cs`
- `Assets/_Game/Tests/EditMode/IDEA0029LeaderInteractionUiTests.cs`
- `Assets/_Game/Tests/EditMode/IDEA0029OutpostAlertUiTests.cs`

新增 PlayMode：

- `Assets/_Game/Tests/PlayMode/IDEA0029ExplorationLeaderOutpostRuntimeInputTests.cs`

PlayMode 必须从正式启动页进入，不直接调用内部 handler，并用真实 Input System 覆盖：

1. 移动城市/展开，验证控制对象默认切换；
2. 玩家接管和释放领袖，AI/手动状态徽记、镜头与移动输入同帧一致；
3. 对矿点发起手采，看到进度、背包入账、满包/离开/枯竭原因；
4. 移动视野源使区域首次进入实时视野并自动扫描，地图从未探索→可见→离开视野后的已探索/最后情报，并验证 `60/180` 秒状态；
5. 点击前哨警报后只聚焦目标，控制权不变，模态与鼠标不穿透；
6. 发现岑烬求救、选择分支、观察资源/人口/关注度/角色状态；
7. 已产生探索/最后情报且手采、警报或求救活动中保存退出并继续，验证 schema 37 恢复；
8. `Esc`、暂停、文本焦点和现有 `M/N/P/T/0` 面板优先级无回归。

### GREEN

修改或新增薄表现层：

- `Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs`
- `Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionController3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionView3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationExpansionVisualPresenter3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveRuntimeHost3D.cs`
- 正式场景 Authoring/Setup 源与必要视觉目录映射。

UI 必须显示：三态迷雾差异、各视野源范围、自动扫描反馈、最后情报年龄及陈旧/过期状态、AI/手动控制状态、手采目标与进度、前哨主状态/三档警报、岑烬求救阶段和选择结果。所有名称使用正式中文显示名，不暴露代码枚举或函数名；缺少新美术时使用清楚可替换占位，不扩大生图或建模范围。

## 10. 组合、性能与稳定重建

### RED

新增或扩展：

- `Assets/_Game/Tests/EditMode/IDEA0029RuntimeCompositionTests.cs`
- `Assets/_Game/Tests/EditMode/IDEA0029ExplorationPerformanceTests.cs`
- `Assets/_Game/Tests/EditMode/GrayboxRuntimeAllocationTests.cs`

覆盖：重复初始化/销毁不重复订阅；固定 seed 和同输入产生相同探索、警报、AI 和求救结果；持续 300 Tick 不产生每帧集合分配；视野与最后情报只在源/格子/事实变化或跨越 `60/180` 秒情报阈值时提升 revision；保存恢复后的状态签名一致。

### GREEN

在现有组合根和 Scene Authoring 中完成一次性装配。禁止使用每帧全场景 `Find*`、反复 LINQ、为每格创建 GameObject 或为每次 UI 刷新复制全地图；使用稳定数组/位集、revision 和现有合批世界表现。

## 11. 分段验证顺序

必须按以下顺序执行，前一层失败时先定位，不用全量结果掩盖聚焦问题：

1. 目录/稳定 ID 契约测试。
2. WorldVisibility + WorldIntel 聚焦测试。
3. Scan 聚焦测试及关注度回归。
4. LeaderControl + LeaderAI + 既有 DirectControl/Camera/Inventory Access 回归。
5. ManualGather + Harvest/Backpack/Production 回归。
6. Outpost state/alert + Settlement/Transport/Defense 回归。
7. CenJinRescue + Character/Politics/Population/Attention 回归。
8. schema 37 contract、36→37、31→37 链、validator、rollback、rewind、wave retry。
9. UI projection 与真实 Input System PlayMode 聚焦。
10. IDEA-0029 组合/稳定重建/性能测试。
11. 日常完整 EditMode，按规则排除 `TerrainAssetDeep`；本轮不触发 TerrainAssetDeep。
12. 完整 PlayMode、`ProjectQualityTools.AnalyzeTestResults`、无界面编译。
13. Windows Release 3D、Windows Development 3D、macOS universal 3D。
14. 设置精确 `WASTECITY_QUALITY_CHANGED_PATHS`，运行 GenerateDocumentation、ValidateDocumentation 和 RecordVerification。
15. 独立静态审查 P0–P2；修复后从相应聚焦层重新向下验证。

只有测试、质量门和构建均成功后，才把 IDEA-0029 写为“已实现待验证”；不得写“已验证”，不得声称用户试玩或真实 Windows 已完成。

## 12. 并行开发与冲突边界

领域契约和 stable ID 冻结后，可按以下独占文件组并行；同一时刻一个文件只允许一个任务写入：

| 工作流 | 可独立修改 | 必须等待/禁止并行修改 |
|---|---|---|
| A：迷雾/扫描 | `World/Exploration/**`、对应 IDEA0029 Visibility/Intel/Scan 测试 | `WorldMapModel.cs`、`CivilizationExpansionRuntime.cs` 由组合负责人统一接线 |
| B：领袖控制/手采 | `Leader/Exploration/**`、`WorldHarvestTransaction.cs`、对应 Leader/Gather 测试 | `GrayboxInputRouter.cs`、`GrayboxLeaderController3D.cs`、`OperationsController3D.cs` 由 3D 集成人员统一修改 |
| C：前哨状态/警报 | `OutpostOperationalState.cs`、`OutpostAlertRuntime.cs`、对应 Outpost 测试 | `SettlementRuntime.cs`、Defense controller、CivilizationExpansion controller 不能与组合任务同时写 |
| D：岑烬求救 | `CenJinRescueRuntime.cs`、`CenJinRescueTransaction.cs`、对应 Rescue 测试 | Character/Politics/Population/Attention 公共文件由事务集成人员串行接入 |
| E：schema 37 | schema37 新测试、新 SaveAdapter | `FormalSaveEnvelope/Data/Codec/Validator/Store/Coordinator` 全部视为一个原子文件组，只能由一名负责人连续修改 |
| F：正式 3D UI/输入 | IDEA0029 UI/PlayMode 新测试 | `GrayboxInputRouter`、CivilizationExpansion Controller/View/Presenter、Operations、RuntimeHost、Scene Authoring 是单一集成冲突组 |
| G：质量与文档 | 功能组草案、测试定位清单 | 只在生产文件稳定后统一生成；不得与 Generate/Validate/RecordVerification 同时写生成文档 |

推荐合并顺序为 A→B→C→D→E→F→G。A/B/C/D 的纯领域测试可并行，但它们不得各自修改组合根、输入路由、正式 View、存档核心或项目质量生成文件。E 完成并通过迁移测试后 F 才能编写保存/继续 PlayMode；F 完成后 G 才能生成目录和验证证据。

## 13. 提交与交付检查点

建议保持可回退的小提交：

1. IDEA-0029 目录与 RED；
2. 三态迷雾/最后情报/扫描 GREEN；
3. 领袖真值桥接、AI/手动和手采 GREEN；
4. 前哨状态/警报 GREEN；
5. 岑烬求救事务 GREEN；
6. schema 37 与迁移 GREEN；
7. 正式 3D UI/真实输入/场景接线；
8. 性能、独立审查、质量目录、完整验证和正式记录。

每次暂存前核对精确文件清单，不带入共享工作区的无关修改；只普通 push 当前开发分支，不 force-push、不合并 PR、不创建 Release。
