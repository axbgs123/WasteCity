# IDEA-0020 关注度、命轨与文明升阶实施计划

> 日期：2026-08-26
> 状态：可执行 TDD 计划；不得据此声称功能已经实现或验证
> 受控需求：`IDEA-0020`
> Unity：`2022.3.62f1`
> 当前正式存档：schema `32`；本计划批准升级为 schema `33`
> Git：每个任务使用小型普通提交并普通 push；不 force-push、不创建 Release、不自动合并 PR

## 1. 交付目标

在正式 `GrayboxPrototype3D` 主循环中形成一条可保存、可观察、可测试的进程链：

```text
领域事件
  → 关注度 0–100、原因和一次性阈值
  → 新进度三选一命轨及三条 Level 1 真实效果
  → 30/60 压力攻击与 90 晶壳母体
  → 遗产解析 + 四项升阶条件
  → U 主动执行文明 1→2
  → Level 2 奖励与关注度 +25
```

首轮完成时，玩家必须能回答：

- 当前关注度、阶段和最近三条变化原因是什么；
- 当前命轨是什么、实际改变了哪条规则、代价是什么；
- 下一次压力阈值是否已经触发或待处理；
- 文明升阶还缺哪项条件，升阶后实际解锁了什么；
- 保存、读档和最近波前重试后，上述事实是否保持一致。

## 2. 不可越过的边界

- 不复用 `Legacy` namespace、schema `1–30` 字段或退役 2D 控制器作为现役所有者；旧档身份、decoder、fixtures 和历史规则测试继续保留。
- 不让 UI、开发修改器、存档适配器或战斗表现成为关注度、命轨、Boss 或文明等级的第二真值。
- 不把现有十波战役胜利静默等同于“击败晶壳母体”；关注度压力链使用独立稳定目录和明确协调器。
- 不在三条命轨效果全部可用前，把命轨选择接入正式新进度入口。
- 不在遗产解析、晶壳母体和四项条件全部真实可达前，显示可提交的升阶按钮。
- 不新增正式三维模型或动画；Boss、命轨和升阶演出使用清楚、可替换且登记过的占位表现。
- 不修改 IDEA-0019 地图尺寸、节点、world signature、地形源、Texture2DArray 或 BuildingCatalog 逻辑 footprint。
- 不把自动化、构建或开发机截图写成用户试玩或真实 Windows 10/11 验收。

## 3. 通用 TDD 与提交规则

每个任务都按以下顺序执行：

1. 只读核对当前生产消费者、质量目录和工作区；
2. 写行为 RED，先确认测试编译成功，再确认只因缺失行为失败；
3. 保存 RED XML 与日志到仓库外 `/tmp/wastecity-idea0020/task-N-red/`；
4. 实现通过该 RED 的最小生产代码；
5. 运行同一筛选得到 GREEN；
6. 运行直接相邻的既有回归；
7. `git diff --check`、精确文件清单、独立静态审查；
8. 更新质量目录/复用条目后形成一个普通提交并 push；
9. 上一提交远端可见后才进入下一任务。

通用环境示例：

```sh
UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
PROJECT_ROOT="/absolute/path/to/WasteCity"

"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testFilter 'WasteCity.Tests.ClassA|WasteCity.Tests.ClassB' \
  -testResults /tmp/wastecity-idea0020/task-N/results.xml \
  -logFile /tmp/wastecity-idea0020/task-N/unity.log
```

PlayMode 必须使用真实 Input System，不允许用 `Button.onClick.Invoke()`、直接控制器方法或修改私有字段冒充玩家输入。

## 4. Task 0：安全基线、需求状态与冲突冻结

### 只读确认

- 精确 HEAD、分支、LFS、工作区、Unity 版本和唯一工程实例；
- `ObservationModel`、`CivilizationModel`、`AdvancementSequenceModel`、`LegacyPathCatalog` 当前只有纯规则/legacy 消费者；
- Formal3D schema `32` 中没有 progression domain；
- `core.research.legacy-analysis` 当前为 `RetiredCompatibility`；
- `GamePauseReason.Advancement` 当前被胜利结算 gate 使用，不把它误当现役文明升阶所有权。

### 文档门

- Docs/06 中 `IDEA-0020` 必须已批准；
- GDD 明确首轮三命轨、关注度数值、晶壳母体和文明 1→2 条件；
- 正式规格必须明确：新增遗产解析后科技树节点数、schema `32→33` 默认值、Level 2 奖励和现有十波战役与关注度压力战役的关系。

### 提交门

- 本任务只提交需求、规格和本计划；不提交生产代码；
- 推荐提交：`docs(idea-0020): approve progression vertical slices`。

## 5. Task 1：Attention 纯模型与正式目录

### 预计生产文件

- 新建 `Assets/_Game/Scripts/Progression/FormalAttentionCatalog.cs`；
- 新建 `Assets/_Game/Scripts/Progression/FormalAttentionRuntime.cs`；
- 必要时将旧 `ObservationModel` 降为兼容 facade，但正式消费者只能使用新 runtime。

目录至少定义：稳定原因 ID、本地化键、变化量、是否一次性、来源类别，以及 `30/60/90` 阈值。首轮初始值精确为 `10`。

### 预计测试文件

- 新建 `FormalAttentionCatalogTests.cs`；
- 新建 `FormalAttentionRuntimeTests.cs`；
- 保留并扩展 `FormalProgressionTests.cs` 作为旧纯规则兼容回归。

### 先写 RED

- 初始 `10`、范围 `0–100`、增加和减少限幅；
- 完整结构化历史按已提交顺序保存稳定 reason ID、delta 和 revision，容量固定为 `128`；HUD snapshot 只投影最近三条；
- 同一个 one-shot event key 只能提交一次；
- 30/60/90 每个阈值只发布一次，降低后再升高不重发；
- Restore 不重放历史阈值、不改 revision、不丢未知 reason；
- snapshot 不可变、稳定排序、静止读取不分配；
- GDD A16.6 本轮接入来源全部来自目录，不在 runtime 写数值。

### 最小 GREEN

- `FormalAttentionRuntime` 只接收显式命令，不搜索场景、不轮询建筑；
- 发布不可变 `FormalAttentionSnapshot` 和已结算 `AttentionThresholdEvent`；
- 原因历史使用容量 `128` 的有界环形记录，UI snapshot 只投影最近三条；完整幂等事实由独立 event key 高水位持有，不能因历史淘汰而允许重放。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.FormalAttentionCatalogTests|WasteCity.Tests.FormalAttentionRuntimeTests|WasteCity.Tests.FormalProgressionTests'
```

### 验证与提交门

- `300` 次无变化 snapshot/读取为 `0 B`；
- 不出现 UnityEngine、UI、Building、Research 或 Defense 引用；
- 推荐提交：`feat(idea-0020): add formal attention model`。

## 6. Task 2：固定三命轨状态与选择合同

### 预计生产文件

- 新建 `Assets/_Game/Scripts/Progression/FormalFateCatalog.cs`；
- 新建 `Assets/_Game/Scripts/Progression/FormalFateRuntime.cs`。

首轮正式目录只包含：

- `core.legacy.pocket-universe` / 袖珍宇宙；
- `core.legacy.void-debt` / 虚空债；
- `core.legacy.rewind-anchor` / 回溯锚点。

三条正式命轨沿用既有稳定 `core.legacy.*` 内容 ID，但新 Formal3D 目录和运行时不得引用 `WasteCity.Legacy` 的历史代码；字符串身份复用不等于复活旧实现。

### 预计测试文件

- 新建 `FormalFateCatalogTests.cs`；
- 新建 `FormalFateRuntimeTests.cs`；
- 继续运行 `LegacySelectionTests.cs`、`LegacyEffectTests.cs`，证明旧语义未被改写。

### 先写 RED

- 三项且仅三项、稳定顺序、名称/简介/效果适配器 ID 完整；
- 新进度状态为 `Unselected`，只能选择一次；
- 选择成功后 Level=`1`、revision 增加一次、产生 `+5` 关注度命令；
- 失败/重复/未知 ID 不改变状态、不增加关注度；
- Level 上限 `9`，本任务只允许保存/恢复 Level 1，不开放升级；
- 选择 snapshot 不包含具体库存、生产或世界副本。

### 最小 GREEN

- 只建立正式状态和命令，不接新游戏 UI，不启用三项效果；
- 明确 `EffectsReady=false`，防止未完成效果时正式入口开放选择。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.FormalFateCatalogTests|WasteCity.Tests.FormalFateRuntimeTests|WasteCity.Tests.LegacySelectionTests|WasteCity.Tests.LegacyEffectTests'
```

### 验证与提交门

- 正式命轨文件不得引用 `WasteCity.Legacy`；
- 推荐提交：`feat(idea-0020): add formal fate state`。

## 7. Task 3：schema 33 与 progression 存档领域

### 预计生产文件

- 扩展 `FormalSaveEnvelope.cs`、`FormalSaveCodec.cs`、`FormalSaveValidator.cs`；
- 扩展 `Persistence/ThreeD/FormalThreeDSaveData.cs`；
- 新建 `GrayboxProgressionSaveAdapter3D.cs`；
- 扩展 `GrayboxFormalSaveCoordinator3D.cs`、runtime host 和迁移器；
- 新增 schema `33` fixture，保留所有 schema `1–32` fixture。

### schema 33 最小字段

- Attention：值、revision、已触发阈值、one-shot event keys、最近三条原因；
- Fate：选择状态、正式 ID、等级、选择 revision；
- Fate effects：债务账本、锚点元数据与袖珍宇宙稳定强化实例 ID 的可扩展容器；
- Civilization：等级、已提交升阶 ID；
- Advancement sequence：阶段、剩余规则秒数、结果 revision；
- 可选 Era track：只有正式规格确认首片需要时才入档，否则不顺带保存占位模型。

### 预计测试文件

- 新建 `FormalSaveSchema33ContractTests.cs`；
- 新建 `GrayboxFormalSaveProgressionTests.cs`；
- 扩展 Envelope、Codec、Validator、FileTransaction、Coordinator、RoundTrip 测试。

### 先写 RED

- 当前 schema 精确为 `33`；schema `32` 单向迁移；schema `1–30` 仍为 Legacy2D；schema `31` 迁移链不破坏；
- schema `32→33` 默认 Attention=`10`、无已触发阈值、命轨未选、文明 Level=`1`；
- migration 明确标记历史关注度原因不可追溯，不伪造事件；
- progression 全字段 round-trip、稳定 hash、未知命轨 ID 保留或结构化拒绝；
- restore 先完整验证再原子提交，失败后所有运行时 fingerprint 不变；
- 波前重试保存同一完整 schema `33` progression，不创建平行槽。

### 最小 GREEN

- progression 成为正式 coordinator 新领域；
- schema `32` 不回写，迁移只向前；
- `FormalSaveCheckpointReasonIds.FateSelectionComplete` 仍只在实际选择提交后使用。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.FormalSaveSchema33ContractTests|WasteCity.Tests.GrayboxFormalSaveProgressionTests|WasteCity.Tests.FormalSaveEnvelopeTests|WasteCity.Tests.FormalSaveValidatorTests|WasteCity.Tests.FormalSaveFileTransactionTests|WasteCity.Tests.GrayboxFormalSaveCoordinatorTests|WasteCity.Tests.GrayboxFormalSaveRoundTripTests'
```

### 验证与提交门

- 固定 schema `1–32` 样本全部通过；
- 新增 DTO/adapter 进入 persistence 质量组和复用目录；
- 推荐提交：`feat(idea-0020): add schema 33 progression domain`。

## 8. Task 4：正式领域事件接入 Attention

### 预计生产文件

- 新建 `GrayboxProgressionController3D.cs`；
- 扩展部署、建筑会话、研究、战役控制器的已结算事件表面；
- 扩展 scene authoring，但不让 controller 搜索场景。

### 首轮事件接线

- 首次展开 `+5`；
- 首座采矿站 `+2`、首座冶炼厂 `+3`、首座装配厂 `+4`；
- 每座完成机枪塔 `+5`，按稳定建筑实例 ID 幂等；
- 基础冶金 `+3`、弹药装配 `+4`、自动防御 `+5`、加固结构 `+5`、遗产解析 `+12`；
- 命轨选择 `+5` 由 Task 2 命令接入；
- 其它 A16.6 来源只在对应系统存在时接入，不用开发夹具伪造。

### 预计测试文件

- 新建 `GrayboxProgressionEventIntegrationTests.cs`；
- 扩展部署、建筑 lifecycle、正式研究和 schema round-trip 测试。

### 先写 RED

- 每种事件精确加一次；施工取消、恢复、重复 authoring、读档重建不加；
- 建筑摧毁后重建的新稳定实例可按正式“每座”规则再次加；
- 研究恢复已完成状态不重复加；
- 同一规则步多个事件按稳定 event key 排序；
- 暂停只冻结时间来源，不阻止已结算离散事件提交。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.GrayboxProgressionEventIntegrationTests|WasteCity.Tests.GrayboxBuildingSessionTests|WasteCity.Tests.FormalResearchRuntimeTests|WasteCity.Tests.CityDeploymentRulesTests|WasteCity.Tests.GrayboxFormalSaveProgressionTests'
```

### 验证与提交门

- 生产事件发布者不引用 Attention runtime；只发布领域事实；
- 推荐提交：`feat(idea-0020): wire attention domain events`。

## 9. Task 5：关注度 HUD、原因详情与真实输入

### 预计生产文件

- 新建 `GrayboxProgressionHudView3D.cs`、`GrayboxProgressionHudController3D.cs`；
- 扩展正式 UI Layout Policy 的 danger/attention 语义槽；
- 扩展 `GrayboxUsabilityInputCoordinator3D` 和 scene authoring。

### 预计测试文件

- 新建 `GrayboxProgressionPresentationTests.cs`；
- 新建 `GrayboxProgressionRuntimeInputTests.cs`；
- 扩展 responsive layout 与 scene contract 测试。

### 先写 RED

- HUD 显示精确值、`未锁定/异常回波/定向观测/坐标锁定` 和最近变化；
- 点击 HUD 打开最近三条原因详情，关闭后输入恢复；
- 颜色之外还用文字/形状区分阶段；
- 真实指针点击、Escape、系统菜单、撤离、结算、背包和科技模态优先级不穿透；
- HUD 只消费 snapshot，静止 revision 不重建对象/文本；
- `1280×720`、`1920×1080`、`2560×1440` 不遮挡资源栏、速度、建造栏。

### 命轨 UI 预备

- 可实现三卡选择 View/Controller 和真实输入测试，但在 `EffectsReady=false` 时正式新游戏入口不得打开；
- 测试通过不等于命轨已经对玩家开放。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.GrayboxProgressionPresentationTests|WasteCity.Tests.GrayboxProgressionRuntimeInputTests|WasteCity.Tests.FormalUiResponsiveLayout3DTests|WasteCity.Tests.GrayboxSceneContractTests|WasteCity.Tests.GrayboxUsabilityRuntimeSceneTests'
```

### 验证与提交门

- PlayMode 必须使用虚拟 Keyboard/Mouse 与 `InputSystemUIInputModule`；
- 推荐提交：`feat(idea-0020): add attention HUD and real input`。

## 10. Task 6：三条命轨 Level 1 真实效果

本任务内部继续拆成三个独立小提交；每条命轨转绿前不得设置 `EffectsReady=true`。

### Task 6A：袖珍宇宙

**预计生产文件**：`PocketUniverseFateEffect.cs`，生产 runtime/clock 的窄效果适配器。

**RED**：

- 按正式机器配方目录引用的建筑定义 ID 区分类别，选择首座已完成、玩家所有、非撤离锁的生产建筑；稳定实例 ID 决胜，不维护平行生产建筑清单；
- 该建筑完成批次产量×2，不缩短周期、不复制输入、不越过输出容量；
- 强化建筑被毁只产生一次坍缩事件；范围、伤害/损失由正式配置决定；
- 保存强化实例、全局首次运行 `+4` 高水位与坍缩结算高水位，恢复不重选、不重复关注度或坍缩。

**聚焦**：`PocketUniverseFateEffectTests|FormalProductionSimulationTests|GrayboxProductionRuntimeTests|GrayboxBuildingCombatLifecycleTests`。

**提交**：`feat(idea-0020): implement pocket universe fate`。

### Task 6B：虚空债

**预计生产文件**：`VoidDebtFateEffect.cs`，扩展正式城市仓储命令而不是直接修改 `ResourceInventory`。

**RED**：

- 只有建造资源不足时允许正式债务额度；研究、生产、背包转移不借债；
- 债务按资源稳定 ID 保存，获得同资源时先自动还债，再入库存；
- 每满 `10` 未还债务，每 `30` 规则秒产生 `+1` 关注度；分帧结果确定；暂停不推进；
- 容量、退款、撤离和摧毁事务不把债务当正库存；未知资源债务保留或拒绝策略明确；
- 债务失败不部分扣款、不绕过建造合法性。

**聚焦**：`VoidDebtFateEffectTests|CityResourceStorageModelTests|ResourceTransactionAndCapacityTests|BuildingPlacementEvaluationTests|GrayboxFormalSaveProgressionTests`。

**提交**：`feat(idea-0020): implement void debt fate`。

### Task 6C：回溯锚点

**预计生产文件**：`RewindAnchorFateEffect.cs`、独立内存/内部 Store 和 coordinator 命令；不得复用最近波前文件为第二玩家槽。

**RED**：

- 玩家在允许状态设置唯一锚点；记录完整 schema `33` Formal3D envelope 与规则时间；
- 活动文件事务、恢复中、结算模态和未提交撤离事务禁止设置/回溯；
- 回溯经过正式 codec、validator、coordinator 两阶段恢复；任一失败保持当前世界不变且锚点不丢；
- 回溯成功后关注度不回退，并按 GDD 额外 `+12`；已触发阈值不撤销；
- 不重复应用 checkpoint、统计、建筑摧毁或命轨选择；
- 锚点损坏/未来 schema/配置不兼容显示结构化中文失败。

**聚焦**：`RewindAnchorFateEffectTests|GrayboxFormalSaveCoordinatorTests|FormalSaveFileTransactionTests|GrayboxFormalSaveRoundTripTests|GrayboxProgressionRuntimeInputTests`。

**提交**：`feat(idea-0020): implement rewind anchor fate`。

### Task 6 总门

- 三项效果均通过后，`FormalFateCatalog.EffectsReady=true`；
- 正式新进度才接入三选一并在成功选择后触发 `fate-selection-complete` 检查点；
- 运行一次三条命轨各自真实输入 PlayMode；
- 推荐汇总提交：`feat(idea-0020): enable formal fate selection`。

## 11. Task 7：关注度压力攻击与晶壳母体

### 预计生产文件

- 新建 `AttentionPressureCatalog.cs`、`AttentionPressureRuntime.cs`；
- 新建或扩展晶壳母体目录、纯战斗状态和占位表现；
- 扩展 `GrayboxDefenseController3D` 的窄压力战役适配器；
- 扩展 progression save adapter。

### 预计测试文件

- 新建 `AttentionPressureRuntimeTests.cs`；
- 新建 `CrystalBroodmotherEncounterTests.cs`；
- 新建/扩展压力战役真实输入测试。

### 先写 RED

- 30 首次安排定向攻击，60 首次安排高危攻击，90 首次安排晶壳母体；每档一次；
- 30 等待教学波/首塔条件，60 等待 30 攻击完成，压力事件不与现有十波阶段机重叠；
- 降低关注度不撤销已安排攻击；读档不重复安排；
- 90 的“坐标锁定最低值”与 Boss pending/active/defeated 分开保存；
- 晶壳母体拥有稳定 ID、生命、阶段、攻击、生成物和唯一击败事件；表现失败不改变战斗；
- Boss 击败只提交一次升阶条件和统计；
- 全局暂停、系统菜单、结算和回溯锚点边界确定。

### 最小 GREEN

- 压力 runtime 只消费 Attention threshold event；Attention model 不直接启动敌人；
- 复用正式伤害、目标、对象池和建筑生命，不复制十波战斗算法；
- 使用清楚占位，不制作新模型。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.AttentionPressureRuntimeTests|WasteCity.Tests.CrystalBroodmotherEncounterTests|WasteCity.Tests.SingleCityDefenseCampaignRuntimeTests|WasteCity.Tests.GrayboxDefenseRuntimeInputTests|WasteCity.Tests.GrayboxFormalSaveProgressionTests'
```

### 验证与提交门

- 30/60/90 三档分别保存 RED/GREEN 与独立运行证据；
- 推荐提交：`feat(idea-0020): add attention pressure and broodmother`。

## 12. Task 8：遗产解析与文明 1→2

### 预计生产文件

- 更新 `ResearchModel.cs` 与正式科技树投影；
- 重构 `FormalProgressionModels.cs`，形成唯一文明 runtime/requirements；
- 扩展 `AdvancementSequenceModel.cs` 或新建正式 sequence runtime；
- 新建 `GrayboxCivilizationAdvancementController3D.cs` 与 View；
- 扩展正式 Input Actions、输入协调器、存档适配和奖励消费者。

### 科技目录门

- `core.research.legacy-analysis` 使用既有稳定 ID、A16 成本 `30` 合金 + `20` 生物质、`60` 秒、前置自动防御；
- 由批准规格明确正式节点总数。本计划推荐从 `43` 增至 `44`，不得用删除既有正式节点的方式偷换；
- 从 `RetiredCompatibility` 转为 `Researchable` 后，schema `1–32` 的旧 ID 识别继续兼容。

### 预计测试文件

- 新建 `FormalCivilizationAdvancementTests.cs`；
- 新建 `GrayboxCivilizationAdvancementRuntimeInputTests.cs`；
- 扩展 ResearchCatalog、ResearchRuntime、BuildingUpgrade、EnemyQuality、save round-trip 与 UI layout 测试。

### 先写 RED

- 四项条件全部满足才允许：遗产解析完成、至少两座已完成玩家机枪塔、晶壳母体已击败、至少一座生产建筑当前真实运行；
- 每项缺失原因稳定排序并可见；预览/施工/残骸/暂停但仍可运行的含义按正式规则确定；
- `U` 只在世界空闲且资格满足时开始；文本输入、建造、撤离、系统菜单、命轨/结算模态不穿透；
- `Scanning 2.5s → Confirmed 3s → Warning 4s → Results` 使用规则时间并可保存恢复；
- 完成只提交一次：文明 Level `1→2`、关注度 `+25`、Level 2 奖励、Guidance完成、checkpoint；重复输入/读档不重复；
- Level 2 解锁现有重机枪塔/御剑台升级只经过正式升级命令；不直接替换建筑定义；
- 升阶模态使用独立 pause owner。先把胜利结算当前占用的 `GamePauseReason.Advancement` 重命名/迁移为终局原因，避免互相释放暂停。

### 最小 GREEN

- 用一个 `FormalCivilizationRuntime` 取代 `TryAdvance`/`TryAdvanceFormal` 两套平行判断；
- requirements 只消费 Research、Building、Boss、Production 的不可变 snapshot；
- UI 只发布 U/按钮命令，sequence 只管理演出状态，奖励由原子提交器拥有。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.FormalCivilizationAdvancementTests|WasteCity.Tests.GrayboxCivilizationAdvancementRuntimeInputTests|WasteCity.Tests.FormalResearchCatalogTests|WasteCity.Tests.FormalResearchRuntimeTests|WasteCity.Tests.BuildingUpgradeTests|WasteCity.Tests.EnemyQualityTests|WasteCity.Tests.GrayboxFormalSaveProgressionTests|WasteCity.Tests.GuidanceFlowTests'
```

### 验证与提交门

- 真实输入完成一次完整 1→2，并读档恢复至少 Scanning、Warning、Results 三状态；
- 推荐提交：`feat(idea-0020): implement civilization advancement`。

## 13. Task 9：开发修改器与诊断入口

### 预计生产文件

- 扩展 `GrayboxDeveloperModifier3D.cs`、目录和 Bootstrap View；
- 不新增 Release 入口。

### 预计测试文件

- 扩展 `GrayboxDeveloperModifierTests.cs`、CatalogTests 和真实输入 PlayMode。

### 先写 RED

- 中文列表/搜索支持：设置或增减关注度、选择指定命轨、清除命轨仅限新会话重置、完成压力事件、满足/撤销升阶条件、执行文明升阶；
- 命令只调用正式 runtime API，不改 UI、DTO、Transform、私有集合或存档文件；
- 成功且实际改变玩法才标记“使用过修改器”；查询、失败、无变化不标记；
- `0` 键、文本焦点、双 Escape、模态抑制和 Release 无入口继续成立。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.GrayboxDeveloperModifierTests|WasteCity.Tests.GrayboxDeveloperModifierCatalogTests|WasteCity.Tests.GrayboxDeveloperModifierRuntimeInputTests'
```

### 验证与提交门

- 用修改器分别到达 29→30、59→60、89→90、三命轨和升阶Results，验证正式HUD/存档而非修改器自画状态；
- 推荐提交：`feat(idea-0020): add progression modifier commands`。

## 14. Task 10：性能、稳定性与独立审查

### 性能 RED 与门

- 关注度无事件的连续 `300` 帧：runtime、controller、HUD 合计持续托管分配 `0 B`；
- `300` 个离散事件按 revision 有界分配，不逐帧重建原因列表；
- 三个命轨各运行 `20` 次保存/恢复，监听器、对象、稳定 ID、债务/锚点和强化实例无界增长为零；
- 压力 60 与 Boss 最大活动量下维持现有战斗快照/池预算；
- 升阶模态连续开关、读档和重复提交不会重复奖励或累积 UI；
- 同一事件序列按不同帧分块、1×/2×与保存断点得到相同 Attention/Fate/Civilization/Boss 状态。

### 独立静态审查

- 搜索中文/数值硬编码、平行 reason/fate/civilization 目录、直接库存债务、UI 重算资格、Attention 直接启动战斗、legacy 字段复用、schema 32 写回、逐帧 LINQ/场景搜索和 pause owner 冲突；
- 审查三命轨效果是否真实接入，而非只有文案和图标；
- 审查旧 schema、IDEA-0017 十波、IDEA-0019 地图和正式资源/科技没有回退。

### 聚焦命令

```sh
testFilter='WasteCity.Tests.FormalAttentionPerformanceTests|WasteCity.Tests.FormalFatePerformanceTests|WasteCity.Tests.CrystalBroodmotherPerformanceTests|WasteCity.Tests.GrayboxBuildAndPerformanceTests|WasteCity.Tests.GrayboxSceneContractTests'
```

### 提交门

- P0–P2 审查项全部闭环；
- 推荐提交：`perf(idea-0020): lock progression stability budgets`。

## 15. Task 11：完整回归、构建与正式文档

### 先生成精确改动清单

- 从本阶段审查范围导出 UTF-8 项目相对路径；
- 设置 `WASTECITY_QUALITY_CHANGED_PATHS`；
- 更新 project quality catalog、Docs/08、Docs/09 和新增复用条目；
- 不用空改动清单绕过提醒。

### 验证顺序

1. IDEA-0020 全部聚焦 EditMode；
2. Attention/Fate/Pressure/Boss/Ascension/Modifier 真实 Input PlayMode；
3. schema `1–33`、文件事务、最近波前和完整 round-trip；
4. 日常完整 EditMode；本阶段不修改地形源、导入规则、Builder 或数组，按规则排除 `TerrainAssetDeep`；
5. 完整 PlayMode；
6. Project Quality、无界面编译、场景合同和文档验证；
7. Windows Release 3D；
8. Windows Development 3D；
9. macOS universal 3D；
10. 与风险相称的 Player 启动、存档、命轨、压力/Boss 和升阶冒烟；
11. `GenerateDocumentation`、`ValidateDocumentation`、`AnalyzeTestResults`；
12. 在准确实现提交上执行 `RecordVerification`。

只有准备发布候选或同时修改地形触发项时才运行 `TerrainAssetDeep`；不能把本阶段普通回归写成地形深度套件通过。

### 必须更新的正式文档

- `Docs/01-Game-Design-Document-ZH.md`：只同步最终实现与已批准差异；
- `Docs/05-Formal-Development-Roadmap-ZH.md`：更新阶段顺序和真实完成度；
- `Docs/06-User-Feedback-and-Change-Control-ZH.md`：按证据推进 IDEA-0020，登记 schema `33`；
- `Docs/07-Project-Use-and-Development-Guide-ZH.md`：关注度、三命轨、Boss、U 升阶与旧档边界；
- `Docs/08-Testing-and-Bug-Location-Guide-ZH.md`：责任文件、聚焦筛选、迁移和人工验收；
- `Docs/09-Reusable-Project-Catalog-ZH.md`：Attention/Fate/Civilization/Pressure/Save/UI 公共边界；
- `Docs/Generated/**`：只通过官方生成器更新。

### 最终人工验收说明

自动化完成后仍要求用户至少试玩：

- 三种命轨各一局或使用正式验证存档体验实际效果；
- 观察多个关注度来源、最近原因、30/60/90 阶段和阈值不重发；
- 完成晶壳母体、检查四项升阶条件、按 U 完成 1→2；
- 在命轨效果、压力战斗和升阶演出中保存/退出/继续；
- 在真实 Windows 10/11 检查输入、排版、性能、GPU、显存和内存。

人工结论只能由用户记录；自动工具不得把“实现待验证”升级为“已验证”。

### 最终提交门

- 精确实现提交、验证提交和普通 push 均成功；
- `git diff --check`、受保护设置/资产哈希和工作区状态已核对；
- 不创建 Release、不合并 PR、不 force-push；
- 推荐提交：`docs(idea-0020): close progression verification`。

## 16. 预计新增或重点修改文件总表

### 纯规则与配置

- `Progression/FormalAttentionCatalog.cs`
- `Progression/FormalAttentionRuntime.cs`
- `Progression/FormalFateCatalog.cs`
- `Progression/FormalFateRuntime.cs`
- `Progression/PocketUniverseFateEffect.cs`
- `Progression/VoidDebtFateEffect.cs`
- `Progression/RewindAnchorFateEffect.cs`
- `Progression/AttentionPressureCatalog.cs`
- `Progression/AttentionPressureRuntime.cs`
- `Progression/FormalCivilizationRuntime.cs`
- `Progression/AdvancementSequenceModel.cs`

### 正式 3D 与存档

- `Graybox3D/Building/GrayboxProgressionController3D.cs`
- `Graybox3D/Building/GrayboxProgressionHudController3D.cs`
- `Graybox3D/Building/GrayboxProgressionHudView3D.cs`
- `Graybox3D/Building/GrayboxCivilizationAdvancementController3D.cs`
- `Graybox3D/Building/GrayboxDeveloperModifier*.cs`
- `Graybox3D/Building/GrayboxProgressionSaveAdapter3D.cs`
- `Persistence/ThreeD/FormalThreeDSaveData.cs`
- `Persistence/FormalSaveEnvelope.cs`
- `Persistence/FormalSaveCodec.cs`
- `Persistence/FormalSaveValidator.cs`
- `Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs`
- `Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`
- `Editor/GrayboxSceneAuthoring.cs`

### 重点测试

- `FormalAttentionCatalogTests.cs`
- `FormalAttentionRuntimeTests.cs`
- `FormalFateCatalogTests.cs`
- `FormalFateRuntimeTests.cs`
- `FormalSaveSchema33ContractTests.cs`
- `GrayboxFormalSaveProgressionTests.cs`
- `GrayboxProgressionEventIntegrationTests.cs`
- `GrayboxProgressionPresentationTests.cs`
- `GrayboxProgressionRuntimeInputTests.cs`
- `PocketUniverseFateEffectTests.cs`
- `VoidDebtFateEffectTests.cs`
- `RewindAnchorFateEffectTests.cs`
- `AttentionPressureRuntimeTests.cs`
- `CrystalBroodmotherEncounterTests.cs`
- `FormalCivilizationAdvancementTests.cs`
- `GrayboxCivilizationAdvancementRuntimeInputTests.cs`
- `FormalAttentionPerformanceTests.cs`
- `FormalFatePerformanceTests.cs`
