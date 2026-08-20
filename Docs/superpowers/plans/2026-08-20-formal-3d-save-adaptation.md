# 正式 3D 存档适配实施计划

> 日期：2026-08-20<br>
> 状态：已批准需求的可执行 TDD 计划，尚未实施<br>
> 受控需求：`IDEA-0015`<br>
> 权威规则：`Docs/06-User-Feedback-and-Change-Control-ZH.md` 的 `IDEA-0015`、`Docs/01-Game-Design-Document-ZH.md` A4.11/A21.1/A21.2、`Docs/superpowers/specs/2026-08-20-formal-3d-save-adaptation-design.md`<br>
> 精确计划基线：`e248d70221f93e84688ef567d4409d16dedb1db8`（`docs: approve formal 3d save adaptation`）<br>
> 工作分支：`codex/formal-3d-save-adaptation`<br>
> Unity：`2022.3.62f1`

## 1. 目标、依赖方向与停止门

本阶段建立唯一正式存档基础设施，以 schema `31` 的独立 3D payload 保存当前垂直切片全部持久真值，并提供单槽“新游戏 / 继续”、事件驱动自动检查点、“保存并退出”、损坏主档回退和中文结果。schema `1–30` 继续作为冻结 2D payload 解码、验证和回归，不静默转换为 3D。

所有权固定如下：

- `WasteCity.Game/Persistence` 拥有 envelope、codec、validator、路径、文件事务、备份和结构化结果；2D/3D 只能做适配器，不得另建文件入口。
- `WasteCity.Game/Persistence/ThreeD` 只定义无 Unity 对象的 schema `31` DTO；稳定 ID、数值和必要活动状态是数据，UI、缓存、监听器和表现对象不是数据。
- `GrayboxFormalSaveCoordinator3D` 只编排各权威模型的 capture/restore、恢复顺序、回滚和检查点；各领域适配器不得互相修改真值。
- 加载必须先 decode + 完整语义验证，再冻结规则时间并事务性 apply。任何领域失败都恢复加载前快照，不得留下半加载状态。
- 派生状态在权威事实恢复后统一重算：建筑网格、路径、物流连接/容量、停工原因、研究站资格、塔目标、撤离容量缺口和只读投影不得存成影子真值。
- `FormalSaveController` 在过渡期仅是旧 2D 场景适配器；`FormalSaveData` 和 schema `1–30` codec/validator/fixtures 必须保留。只有 Task 13 的完整验证成功后，才执行 Task 14 的独立 2D 专属退役。

硬停止门：

- RED 只能是本任务新增断言的预期失败；编译错误、夹具不可读、缺 XML 或无关回归失败必须先处理。
- 发现 schema `31` 无法无损表达活动转换、战斗或已确认撤离，或需要新增玩法规则/资源时，停止并报告，不以默认值掩盖。
- 任何加载路径在验证前修改运行时、任何写盘失败覆盖最近有效主档/备份、任何旧档被自动转换，均为 P0 阻断。
- 新建 Unity 资产必须带 Unity 生成的 `.meta`；提交点所列新文件默认包含其配对 `.meta`。
- 类型名可因 asmdef 编译边界在同一任务列出的路径内微调，但“单一基础设施 → 领域适配器 → UI”的依赖方向和提交边界不可改变。
- 本计划不修改地形源、导入规则、Texture2DArray Builder 或生成数组，日常 EditMode 排除 `TerrainAssetDeep`。

## 2. 统一命令与证据规则

```bash
export WASTECITY_PROJECT_ROOT="$(git rev-parse --show-toplevel)"
export WASTECITY_UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
export WASTECITY_EVIDENCE_ROOT="/tmp/wastecity-formal-save-20260820"
mkdir -p "$WASTECITY_EVIDENCE_ROOT"
git status --short --branch
git lfs fsck
"$WASTECITY_UNITY_BIN" -version
```

所有 `-runTests` 命令显式指定平台、过滤器、XML 和日志，且不得带 `-quit`。每次 RED/GREEN 后必须确认 XML 已生成；GREEN 要求 `failed="0"`、`skipped="0"`。新类型尚不存在时，首个 RED 使用反射、源码合同或既有公开行为保持测试程序集可编译；不得把找不到类型导致的编译失败冒充有效 RED。每个任务完成 REFACTOR 后复跑同一 GREEN 过滤器，再只暂存该任务列出的精确路径。

## 3. RED → GREEN → REFACTOR 任务

### Task 1：schema 31 envelope、codec 与固定 fixtures

**预计文件**

- Create: `Assets/_Game/Scripts/Persistence/FormalSaveEnvelope.cs`
- Create: `Assets/_Game/Scripts/Persistence/FormalSaveCodec.cs`
- Create: `Assets/_Game/Scripts/Persistence/ThreeD/FormalThreeDSaveData.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Create: `Assets/_Game/Tests/EditMode/FormalSaveEnvelopeTests.cs`
- Create: `Assets/_Game/Tests/Fixtures/Persistence/schema-01-legacy-2d.json`
- Create: `Assets/_Game/Tests/Fixtures/Persistence/schema-30-legacy-2d.json`
- Create: `Assets/_Game/Tests/Fixtures/Persistence/schema-31-formal-3d.json`
- Create: `Assets/_Game/Tests/Fixtures/Persistence/schema-32-future.json`

**RED**

先固定以下失败用例：schema `31` envelope 包含 `gameVersion/saveSchemaVersion/contentSources/createdAt/updatedAt/runtimeKind` 和且仅含 `formal3D` payload；schema `1/30` 被分类为 `Legacy2D`；schema `31` 完整编码字节稳定且往返不丢字段；schema `32` 返回“存档版本过新”；空白、截断、未知 runtime kind 和 schema/payload 身份不一致均返回结构化失败，不能返回 `null` 混淆原因。固定 fixtures 必须由文件实际读取，不在测试代码中重造同义 JSON。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FormalSaveEnvelopeTests -testResults "$WASTECITY_EVIDENCE_ROOT/task-01-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-01-red.log"
```

**GREEN**

把旧 `FormalSaveCodec` 从 `FormalSaveData.cs` 搬到独立文件并保持 schema `1–30` 入口行为；新增显式 `FormalSaveEnvelope`、payload 类型枚举、decode result 和 schema `31` DTO。旧字段不挪入 3D DTO，3D payload 不借用旧 2D 默认值。

**REFACTOR**

收敛版本常量、UTC ISO-8601 和确定性排序；复跑 `FormalSaveEnvelopeTests|FormalSaveTests`，证明旧 codec 调用仍通过。

**提交点**：`feat: define schema 31 save envelope`；只提交上述路径。

### Task 2：schema 31 语义 validator 与旧档兼容门

**预计文件**

- Create: `Assets/_Game/Scripts/Persistence/FormalSaveValidator.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveCodec.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveEnvelopeTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FormalSaveValidatorTests.cs`
- Create: `Assets/_Game/Tests/Fixtures/Persistence/schema-31-invalid-cross-reference.json`

**RED**

新增失败测试：必填数组缺失、非有限时间、负资源、世界尺寸/资源数组不一致、空白或重复稳定实例 ID、生产/塔/撤离引用不存在的建筑实例、活动项与完成集合冲突、背包不是 30 格、仓库过滤 ID 语法非法、撤离 work 顺序重复或当前项不在批次中均拒绝；未知但语法有效的建筑、资源、配方或科技定义 ID 必须保留为缺失内容占位或孤立数据，不能静默删除，也不能仅因 catalog 当前不认识就拒绝整档；schema `1–30` 走冻结 validator 且不产生 `FormalThreeDSaveData`；未来 schema 有独立错误码；fixture 错误必须带稳定中文摘要和字段路径。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.FormalSaveEnvelopeTests|WasteCity.Tests.FormalSaveValidatorTests|WasteCity.Tests.FormalSaveTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-02-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-02-red.log"
```

**GREEN**

实现无 Unity 场景依赖的 validator；codec 只负责解析和身份分类，validator 负责语义。不得修补坏数据或给 schema `31` 缺失真值补默认值。

**REFACTOR**

把重复的稳定 ID、数值范围和交叉引用检查收敛为私有纯函数；复跑上述过滤器。

**提交点**：`feat: validate schema 31 save semantics`。

### Task 3：单槽 store 与安全文件事务

**预计文件**

- Create: `Assets/_Game/Scripts/Persistence/FormalSaveFileTransaction.cs`
- Create: `Assets/_Game/Scripts/Persistence/FormalSaveStore.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- Create: `Assets/_Game/Tests/EditMode/FormalSaveFileTransactionTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`

**RED**

以临时目录和故障注入固定：同目录 `.tmp` 写入 → flush → 重新 decode/validate → 更新已验证 `.bak` → 原子替换主档；在每一步抛错时主档和备份仍是最近有效内容；损坏主档被保留或隔离并回退有效备份；无有效档、旧 2D 档、版本过新和主档损坏回退都有不同结果与中文消息；`HasSave` 必须验证内容，不能只看文件存在；重复保存保留原 `createdAt` 并更新 `updatedAt`。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.FormalSaveFileTransactionTests|WasteCity.Tests.FormalSaveTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-03-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-03-red.log"
```

**GREEN**

实现唯一 `formal-world.json` 单槽 store、结构化 `Save/Load/Probe` 结果和可替换文件系统/时钟边界。旧控制器改用同一 store，但仍只接旧 2D adapter；不得保留 `File.WriteAllText` 旁路。

**REFACTOR**

收敛路径解析、异常到结果码映射和备份选择；真实文件测试只写系统临时目录并清理。复跑上述过滤器。

**提交点**：`feat: add transactional formal save store`。

### Task 4：世界、城市、人口 capture/restore

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxWorldCitySaveAdapter3D.cs`
- Modify: `Assets/_Game/Scripts/World/WorldMapModel.cs`
- Modify: `Assets/_Game/Scripts/City/CityDeploymentModel.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveWorldCityTests.cs`

**RED**

先测试 capture → 扰动 → restore：固定 world seed、资源节点余量、城市世界位置、自动驾驶目的地、人口、`Mobile/Fortress` 以及活动 `Deploying/Packing` 的转换前稳定态和剩余规则时间全部精确恢复；路径本身不保存而由目的地重算；非法/不可达目的地得到确定性恢复结果；restore 后资源 marker 和城市表现从模型刷新。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveWorldCityTests|WasteCity.Tests.WorldMapTests|WasteCity.Tests.CityDeploymentRulesTests|WasteCity.Tests.GrayboxMobileCityController3DTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-04-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-04-red.log"
```

**GREEN**

给权威模型补最小、验证后的 capture/restore API；adapter 只映射 DTO，不保存路径、坐标缓存、marker 或开发时间倍率。恢复顺序固定为 seed/world → 资源余量 → 城市位置/人口 → 部署/导航意图 → 表现刷新。

**REFACTOR**

把坐标、有限浮点和数组长度验证留在 validator，领域 restore 不静默截断。复跑上述过滤器。

**提交点**：`feat: persist formal 3d world and city state`。

### Task 5：建筑、网格、城市账本与真实仓库

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingStorageSaveAdapter3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Economy/CityResourceStorageModel.cs`
- Modify: `Assets/_Game/Scripts/Building/BuildingGrid.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveBuildingStorageTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs`

**RED**

固定稳定实例 ID、建筑 ID、站点、格位、方向、施工状态/剩余、资源点绑定、所有权、撤离锁基础身份、城市核心逐资源账本、每座仓库共享内容与单资源过滤的往返；恢复时重复占格、语法损坏的定义 ID、非法方向/矿点绑定，以及同一配置签名下结构性不可能的容量状态必须在 apply 前拒绝。语法有效但当前 catalog 未知的建筑/资源进入占位或孤立状态；若配置签名变化导致既有仓库超额，完整保留内容并恢复为只出不进。物流连接、聚合容量和 UI 内容不入档，恢复后由现有规则重算。`nextStableInstanceOrdinal` 必须作为权威高水位保存并原值恢复，同时验证它严格大于所有已恢复的正式实例序号；不得仅从当前实例推导后重复使用历史 ID。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveBuildingStorageTests|WasteCity.Tests.GrayboxBuildingSessionTests|WasteCity.Tests.GrayboxWarehouseStorageIntegrationTests|WasteCity.Tests.BuildingGridTests|WasteCity.Tests.CityResourceStorageModelTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-05-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-05-red.log"
```

**GREEN**

通过统一建筑放置/网格恢复 API 重建实例和 presentation；禁止直接写 Grid 私有集合或复制放置合法性。先重建实例，再恢复核心/仓库内容和过滤，最后同步连接。

**REFACTOR**

合并仓库迁移与存档 restore 共用的原子容量检查，避免第二套库存规则。复跑上述过滤器。

**提交点**：`feat: persist formal 3d buildings and storage`。

### Task 6：背包、应急合成与科技

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEconomySaveAdapter3D.cs`
- Modify: `Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs`
- Modify: `Assets/_Game/Scripts/Economy/CraftingQueueModel.cs`
- Modify: `Assets/_Game/Scripts/Research/ResearchModel.cs`
- Modify: `Assets/_Game/Scripts/Research/DemoResearchRuntime.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveEconomyTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/PlayerBackpackModelTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/CraftingQueueModelTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/DemoResearchRuntimeTests.cs`

**RED**

固定 30 格槽位顺序、资源 ID/数量、合成稳定配方 ID 队列顺序、每项稳定执行序号、已预留输入、活动进度、输出阻塞与 `nextQueueOrdinal` 权威高水位，以及六节点已完成集合、活动研究 ID/剩余时间的往返；`nextQueueOrdinal` 原值恢复并严格大于所有已存在或记录的执行序号，不得仅从当前队列推导后复用历史 ID。已预留合成材料不得重复扣除；研究资源不得再次扣除；恢复时不保存/伪造研究站资格、城市倍率或面板状态。空白或语法损坏的配方/科技 ID、重复完成项和同配置签名下的超上限堆叠在验证阶段拒绝；语法有效但 catalog 未知的 recipe 必须连同稳定执行序号、暂停队列项和可退款预留原样保留，未知已完成科技原样保留但不授予效果，未知活动科技暂停并显示内容缺失。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveEconomyTests|WasteCity.Tests.PlayerBackpackModelTests|WasteCity.Tests.CraftingQueueModelTests|WasteCity.Tests.DemoResearchRuntimeTests|WasteCity.Tests.ResearchTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-06-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-06-red.log"
```

**GREEN**

给各模型增加公开但受验证约束的 snapshot API；队列保存 recipe ID，不序列化目录对象。恢复后用目录重新解析并重算 block reason/研究资格。

**REFACTOR**

复用现有背包原子快照做队列 restore 回滚，删除测试专用旁路。复跑上述过滤器。

**提交点**：`feat: persist backpack crafting and research`。

### Task 7：逐建筑生产持久化

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionSaveAdapter3D.cs`
- Modify: `Assets/_Game/Scripts/Economy/BuildingProductionState.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveProductionTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxProductionRuntimeTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxProductionLifecycleTests.cs`

**RED**

固定每个稳定建筑实例的 definition ID、输入、已预留输入、输出、周期进度、玩家暂停和绑定节点身份；有预留输入的半周期恢复后不得重复访问城市库存；脱离物流建筑保留内部库存但连接状态重算；矿点余量与生产状态组合恢复不重复采集；删除/撤离锁定实例的状态不得游离；停工原因和 observability revision/hash 不入档。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveProductionTests|WasteCity.Tests.GrayboxProductionRuntimeTests|WasteCity.Tests.GrayboxProductionLifecycleTests|WasteCity.Tests.FormalProductionSimulationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-07-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-07-red.log"
```

**GREEN**

先由已恢复建筑同步创建生产 state，再按稳定 ID apply 持久字段；只通过现有库存模型恢复内部库存和预留，不复制配方/物流判断。

**REFACTOR**

让撤离 payload 与存档 snapshot 共用只读资源捕获助手，但保持撤离提交所有权不变。复跑上述过滤器。

**提交点**：`feat: persist formal 3d production state`。

### Task 8：教学防御与活动战斗持久化

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSaveAdapter3D.cs`
- Modify: `Assets/_Game/Scripts/Defense/FirstDefenseCombatModels.cs`
- Modify: `Assets/_Game/Scripts/Defense/FirstDefenseWaveRuntime.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveDefenseTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxFirstDefenseRuntimeTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxDefenseSnapshotStabilityTests.cs`

**RED**

固定机枪塔本地弹药/开火进度/玩家暂停、教学波触发次数与 phase/剩余警告、已生成/已击败计数、核心耐久、存活敌人稳定 ID/生成顺序/位置/耐久/攻击进度以及 fixed-step accumulator 的往返；活动战斗保存后恢复必须继续而非重开波次、重复生成或重复耗弹。目标 ID、状态文案和表现 tracer 不保存，首个规则 tick 后稳定重建。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveDefenseTests|WasteCity.Tests.GrayboxFirstDefenseRuntimeTests|WasteCity.Tests.GrayboxDefenseSnapshotStabilityTests|WasteCity.Tests.FirstDefenseWaveRuntimeTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-08-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-08-red.log"
```

**GREEN**

先同步已恢复塔实例，再恢复塔局部真值和 tutorial runtime；敌人按稳定生成顺序重建。禁止序列化 `GameObject`、对象池、target 引用或 HUD snapshot。

**REFACTOR**

存档 snapshot 与现有只读防御 snapshot 共用不可变值构造，不让 UI snapshot 变成 apply DTO。复跑上述过滤器。

**提交点**：`feat: persist active tutorial defense`。

### Task 9：已确认撤离批次与容量阻塞持久化

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationSaveAdapter3D.cs`
- Modify: `Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveEvacuationTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`

**RED**

固定已确认批次 ID、`nextBatchOrdinal` 权威高水位、冻结和平/战斗上下文、处理方式、稳定 work 顺序、当前索引/项目、剩余规则时间、退款与内部载荷、生产/炮塔载荷、仓库迁移、容量阻塞代码/项目身份和每座建筑撤离锁；`nextBatchOrdinal` 原值恢复并严格大于所有已存在或记录的批次序号，不得从当前活动批次推导后复用历史 ID。容量缺口数值从恢复后的仓储真值重算，不保存为第二份真值。活动项恢复后恰继续剩余时间，阻塞项可在腾出容量后原批次重试；保存期间不得重新规划退款、重新捕获载荷或解锁建筑；批次 work 引用的稳定建筑实例存在但 definition ID 当前 catalog 未知时，随 Task 5 的缺失内容占位体保留冻结 work、锁和载荷，只有引用的稳定建筑实例根本不存在才拒绝；manifest 未确认选择和 UI view 不保存。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveEvacuationTests|WasteCity.Tests.GrayboxEvacuationTests|WasteCity.Tests.GrayboxFormalEvacuationPerformanceTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-09-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-09-red.log"
```

**GREEN**

持久化已经冻结的不可变 work 和 runtime payload，恢复锁后再恢复 controller 队列；容量缺口从当前仓储事实重算并与保存的阻塞身份核对，不保存 view cache。

**REFACTOR**

把运行中捕获和存档映射从 1500 行控制器抽到 adapter，但不改变 controller 的批次提交所有权。复跑上述过滤器。

**提交点**：`feat: persist confirmed evacuation batches`。

### Task 10：事务性 3D 总协调器与全量往返

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveCoordinatorTests.cs`

**RED**

构建全部领域非默认状态，执行 capture → 逐域扰动 → restore，要求字节语义等价；按 world/city、building/storage、economy/research、production、defense、evacuation 的每一个 apply 边界注入失败，要求加载前快照完整回滚、全局暂停恢复、无重复事件订阅/表现对象。decode/validate 失败时任何领域 apply 调用次数都为 `0`；成功后只执行一次派生状态重建。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveCoordinatorTests|WasteCity.Tests.GrayboxFormalSaveWorldCityTests|WasteCity.Tests.GrayboxFormalSaveBuildingStorageTests|WasteCity.Tests.GrayboxFormalSaveEconomyTests|WasteCity.Tests.GrayboxFormalSaveProductionTests|WasteCity.Tests.GrayboxFormalSaveDefenseTests|WasteCity.Tests.GrayboxFormalSaveEvacuationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-10-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-10-red.log"
```

**GREEN**

总协调器捕获当前完整快照作为 rollback，暂停规则时间，按固定顺序 apply；失败时反向清理并重放 rollback；成功后统一 synchronize/refresh，再恢复原暂停状态。保存和加载均返回结构化结果，不抛出到 UI。

**REFACTOR**

用明确 `IFormalThreeDSaveDomain` 顺序表消除条件链；接口只传 DTO 分区和 restore context。复跑上述过滤器。

**提交点**：`feat: coordinate transactional 3d save restore`。

### Task 11：事件驱动自动检查点

**预计文件**

- Create: `Assets/_Game/Scripts/Persistence/FormalSaveCheckpointPolicy.cs`
- Modify: `Assets/_Game/Scripts/City/CityDeploymentModel.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveCheckpointTests.cs`

**RED**

逐项固定设计规格 9.1 的当前触发器：`new-game-ready`、`first-deployment-complete`、`first-machine-gun-complete`、`tutorial-combat-started`、`evacuation-batch-confirmed`、`evacuation-work-committed`、`packing-complete`。进入容量阻塞是当前撤离 work 的稳定转换，由 `evacuation-work-committed` 原因族合并记录，不另存 UI 缺口。相同一次性里程碑不重复写盘；生产 tick、研究完成、UI refresh 和逐帧 Update 不触发写盘；保存请求在 Deploying/Packing、活动战斗或已确认撤离中仍捕获完整 schema `31`；失败保留有效档并允许下一次事件重试。GDD 的命轨选择和 Boss 事件尚未接入当前 3D，不伪造当前事件，只预留以后通过同一 policy 增加稳定原因 ID 的扩展点。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveCheckpointTests|WasteCity.Tests.CityDeploymentRulesTests|WasteCity.Tests.GrayboxBuildingSessionTests|WasteCity.Tests.GrayboxEvacuationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-11-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-11-red.log"
```

**GREEN**

策略只接领域完成事件并同步写单槽；不存在的命轨/Boss 玩法只保留稳定原因 ID 和未来事件入口，不为本阶段新增玩法。防抖依据事件身份/里程碑，不按时间轮询。

**REFACTOR**

统一订阅/退订生命周期，测试重复 Configure/场景卸载不积累监听器。复跑上述过滤器。

**提交点**：`feat: add event driven formal checkpoints`。

### Task 12：正式 3D 新游戏/继续、保存并退出与真实输入 UI

**预计文件**

- Create: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxFormalSaveEntryController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalSaveUiAndInputTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxUsabilityTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Create: `Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRuntimeInputTests.cs`

**RED**

真实 UI/Input System 测试：启动页只有有效 schema `31` 才启用“继续”；旧 2D 档显示明确兼容说明，不启动 3D；“新游戏”不读取旧档且覆盖前二次确认；Esc 菜单把旧“不保存”改为“保存并退出”；真实鼠标点击保存成功后才调用退出，失败则保持运行并在固定反馈区显示中文原因；损坏主档回退显示“主存档损坏，已恢复备份”；菜单打开期间输入不穿透建造、研究、移动和撤离。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveUiAndInputTests|WasteCity.Tests.GrayboxUsabilityTests|WasteCity.Tests.GrayboxSceneContractTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-12-edit-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-12-edit-red.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxFormalSaveRuntimeInputTests -testResults "$WASTECITY_EVIDENCE_ROOT/task-12-play-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-12-play-red.log"
```

**GREEN**

Usability 只消费 coordinator 的结构化结果；按钮通过现有 EventSystem 和 `GrayboxUsabilityInputCoordinator3D` 主循环进入命令。场景 authoring 生成稳定引用，禁止测试直接调用按钮背后的内部保存方法来冒充真实输入。

**REFACTOR**

统一启动页、系统菜单和自动检查点的中文结果映射；复跑两条 GREEN 命令。

**提交点**：`feat: wire formal 3d save user flow`。

### Task 13：场景重载、性能与退役前完整验证

**预计文件**

- Create: `Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRoundTripTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRuntimeInputTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs`
- Modify: `Assets/_Game/Editor/GrayboxPerformanceProbe.cs`
- Modify: `Docs/Engineering/project-quality-catalog.json`
- Modify: `Docs/09-Reusable-Project-Catalog-ZH.md`
- Modify as required by implementation truth: `Docs/01-Game-Design-Document-ZH.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify: `Docs/07-Project-Use-and-Development-Guide-ZH.md`
- Modify: `Docs/08-Testing-and-Bug-Location-Guide-ZH.md`
- Modify: `Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs`
- Generated only by official tools: `Docs/Generated/*`

**RED**

通过正式 3D 真实输入建立状态并实际卸载/重载 `GrayboxPrototype3D`：分别覆盖活动 Deploying、活动 Packing、8 敌活动战斗、已确认撤离进行中、容量阻塞撤离；继续后比较所有权威字段并验证路径/物流/停工/目标/UI 重算。连续 20 次 capture、5 次场景重载和 300 帧无自动写盘对象增长；单次完整快照和事务分配建立明确上限，不能逐帧分配或写盘。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testFilter 'WasteCity.Tests.GrayboxFormalSaveRuntimeInputTests|WasteCity.Tests.GrayboxFormalSaveRoundTripTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-13-play-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-play-red.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxBuildAndPerformanceTests -testResults "$WASTECITY_EVIDENCE_ROOT/task-13-perf-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-perf-red.log"
```

**GREEN**

只补正式测试夹具需要的确定性资源和规则时间加速；不得调用开发人口修改、直接 restore 内部方法或跳过真实输入。性能 Marker 只包 capture、validate、write transaction、apply 和 rebuild。

**REFACTOR**

清理临时存档、恢复 `Application.persistentDataPath` 测试替身和事件订阅；复跑两条 GREEN 命令。

**质量目录 RED**

先让质量合同因新增 Persistence/ThreeD、adapter、UI、fixtures 和测试路径未登记而失败；文档必须写明单槽位置、自动检查点、备份回退、错误定位、旧档身份、schema `31` 与人工/Windows 未验收边界。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.ProjectQualityCatalogTests|WasteCity.Tests.ProjectQualityIntegrationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-13-doc-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-doc-red.log"
```

**质量目录 GREEN / 完整 REFACTOR 门**

先更新人工文档和质量目录，并把实现、E2E、人工文档和质量目录提交到同一个待验证实现 HEAD；生成 changed-paths 前确认没有尚未提交的本阶段生产/测试/人工文档/目录路径。随后用审批基线到该实现 HEAD 的仓库相对路径生成清单，再运行官方生成器、日常完整 EditMode、完整 PlayMode、编译和四个退役前正式构建。此时 legacy 2D 入口仍必须存在并通过，作为 Task 14 的前置证据。

```bash
test -z "$(git status --short --untracked-files=all | rg -v '^.. Docs/Generated/')"
git diff --name-only e248d70221f93e84688ef567d4409d16dedb1db8 HEAD | sort -u > "$WASTECITY_EVIDENCE_ROOT/changed-paths.txt"
WASTECITY_QUALITY_CHANGED_PATHS="$WASTECITY_EVIDENCE_ROOT/changed-paths.txt" "$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.GenerateDocumentation -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-generate.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-validate.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testCategory '!TerrainAssetDeep' -testResults "$WASTECITY_EVIDENCE_ROOT/task-13-editmode.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-editmode.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testResults "$WASTECITY_EVIDENCE_ROOT/task-13-playmode.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-playmode.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-compile.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-build-windows-release-3d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-build-windows-development-3d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-build-windows-legacy-2d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildMacOSGraybox3D -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-build-macos-universal-3d.log"
```

完整测试必须零失败、零跳过；确认 schema `1/30/31` fixtures、事务故障注入、活动转换/战斗/撤离和真实输入 E2E 实际进入 XML。四个构建日志必须实际存在并证明成功；用 `apply_patch` 创建严格 JSON `$WASTECITY_EVIDENCE_ROOT/build-summary.json`，只允许以下字段，并把示例路径替换为本轮真实绝对日志路径：

```json
{"Builds":[
  {"Name":"Windows Release 3D","Status":"Succeeded","EvidenceLogPath":"/tmp/wastecity-formal-save-20260820/task-13-build-windows-release-3d.log"},
  {"Name":"Windows Development 3D","Status":"Succeeded","EvidenceLogPath":"/tmp/wastecity-formal-save-20260820/task-13-build-windows-development-3d.log"},
  {"Name":"Windows legacy 2D","Status":"Succeeded","EvidenceLogPath":"/tmp/wastecity-formal-save-20260820/task-13-build-windows-legacy-2d.log"},
  {"Name":"macOS universal 3D","Status":"Succeeded","EvidenceLogPath":"/tmp/wastecity-formal-save-20260820/task-13-build-macos-universal-3d.log"}
]}
```

在该待验证实现 HEAD 上显式设置 `RecordVerification` 的七个必需证据变量和 changed-paths，再执行官方入口：

```bash
export WASTECITY_QUALITY_CHANGED_PATHS="$WASTECITY_EVIDENCE_ROOT/changed-paths.txt"
export WASTECITY_QUALITY_VERIFIED_SHA="$(git rev-parse HEAD)"
export WASTECITY_QUALITY_VERIFIED_AT="$(date '+%Y-%m-%dT%H:%M:%S%z' | sed -E 's/([+-][0-9]{2})([0-9]{2})$/\1:\2/')"
export WASTECITY_QUALITY_EDITMODE_RESULTS="$WASTECITY_EVIDENCE_ROOT/task-13-editmode.xml"
export WASTECITY_QUALITY_PLAYMODE_RESULTS="$WASTECITY_EVIDENCE_ROOT/task-13-playmode.xml"
export WASTECITY_QUALITY_COMPILE_LOG="$WASTECITY_EVIDENCE_ROOT/task-13-compile.log"
export WASTECITY_QUALITY_BUILD_SUMMARY="$WASTECITY_EVIDENCE_ROOT/build-summary.json"
export WASTECITY_QUALITY_HUMAN_PLAYTEST="未执行；等待用户人工试玩"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.RecordVerification -logFile "$WASTECITY_EVIDENCE_ROOT/task-13-record-verification.log"
```

自动化、macOS 和跨平台构建不得写成用户试玩或真实 Windows 验收。

**提交点**：先以 `test: verify formal 3d save round trips` 提交 E2E 与性能测试，再以 `docs: catalog formal 3d save adaptation` 提交质量目录、人工文档和官方生成结果；在最终实现 SHA 上记录证据，再以 `docs: record formal 3d save verification` 单独提交 `Docs/Generated/Latest-Verification-ZH.md`。Task 13 未全部通过时禁止进入 Task 14。

### Task 14：独立退役 2D 专属场景、控制器与构建入口

**前置门**

Task 13 的 schema `31` 往返、损坏恢复、旧档兼容、自动检查点、场景重载、玩家可见错误、四构建和正式验证必须全部完成；否则本任务保持未开始。先由主智能体逐文件确认“仅服务冻结 2D”，任何被 3D 或共享规则引用的文件不得删除。

**预计文件**

- Delete: `Assets/_Game/Scenes/FormalPrototype.unity`
- Delete only after引用审计: `Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/City/PlaceholderMobileCity.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Combat/FormalCombatController.cs`, `FormalFriendlyUnitController.cs`, `FormalTechnologyRouteController.cs`, `PlaceholderBehemoth.cs`, `PlaceholderBossEncounter.cs`, `PlaceholderEnemy.cs`, `PlaceholderPuppet.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Core/FormalGameClockController.cs`, `FormalSessionController.cs`, `FormalSessionStatisticsController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Economy/FormalEconomyController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Leader/FormalLeaderController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Narrative/FormalGuidanceController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Population/FormalPopulationController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/Progression/FormalAdvancementController.cs`, `FormalProgressionController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/UI/FormalPlaceholderHud.cs`, `FormalTitleMenuController.cs`
- Delete only after引用审计: `Assets/_Game/Scripts/World/FormalCameraController.cs`, `FormalDroneController.cs`, `PlaceholderWorldView.cs`
- Modify: `Assets/_Game/Editor/FormalBuildTools.cs`
- Modify: `Assets/_Game/Editor/FormalProjectSetup.cs`
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Delete/Modify after test ownership split: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`, `Assets/_Game/Tests/EditMode/SceneContractTests.cs`, `Assets/_Game/Tests/EditMode/TitleMenuTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainEvidenceCaptureTests.cs`
- Modify: `Docs/Engineering/project-quality-catalog.json`, `Docs/05-Formal-Development-Roadmap-ZH.md`, `Docs/06-User-Feedback-and-Change-Control-ZH.md`, `Docs/07-Project-Use-and-Development-Guide-ZH.md`, `Docs/08-Testing-and-Bug-Location-Guide-ZH.md`, `Docs/09-Reusable-Project-Catalog-ZH.md`
- Generated only by official tools: `Docs/Generated/*`

`FormalSaveData.cs`、`FormalSaveCodec.cs`、`FormalSaveValidator.cs`、schema `1–30` 迁移/兼容代码、固定 fixtures、稳定 ID、资源/研究/战斗纯模型和共享规则明确不在删除范围。

**RED**

先改合同测试使其要求：Build Settings 只有正式 3D；`BuildWindowsLegacy2D` 不再存在；`FormalPrototype` 和专属 controller 不在可发布/可复用目录；schema `1/30` fixtures 仍可 decode/validate 且默认 3D 入口显示兼容说明。此时预期仅因旧入口仍存在失败。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxBuildAndPerformanceTests|WasteCity.Tests.GrayboxSceneContractTests|WasteCity.Tests.FormalSaveEnvelopeTests|WasteCity.Tests.FormalSaveValidatorTests|WasteCity.Tests.FormalSaveTests|WasteCity.Tests.ProjectQualityCatalogTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-14-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-14-red.log"
```

**GREEN**

按 `rg` 引用审计逐个删除专属适配器及 `.meta`，移除 legacy build 方法/菜单/scene entry；若文件仍被 3D 或共享模型引用，则先将共享规则保留在原文件或独立纯规则文件，本任务不得复制实现。更新质量目录并由官方工具重生成文档。

**REFACTOR / 最终门**

```bash
rg -n 'FormalPrototype|BuildWindowsLegacy2D|FormalSaveController' Assets ProjectSettings Docs/Engineering Docs/Generated
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testCategory '!TerrainAssetDeep' -testResults "$WASTECITY_EVIDENCE_ROOT/task-14-editmode.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-14-editmode.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testResults "$WASTECITY_EVIDENCE_ROOT/task-14-playmode.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-14-playmode.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows -logFile "$WASTECITY_EVIDENCE_ROOT/task-14-build-windows-release-3d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment -logFile "$WASTECITY_EVIDENCE_ROOT/task-14-build-windows-development-3d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildMacOSGraybox3D -logFile "$WASTECITY_EVIDENCE_ROOT/task-14-build-macos-universal-3d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation -logFile "$WASTECITY_EVIDENCE_ROOT/task-14-doc-validate.log"
git diff --check
```

首条 `rg` 允许命中历史需求、旧 schema 兼容说明和 fixtures，但不得命中发布场景、专属 controller 或 legacy build 入口。完整测试仍须零失败、零跳过；schema `1/30/31` 回归必须在 XML 中。重新记录退役后的验证证据，仍不得声称用户试玩或真实 Windows 验收。

**提交点**：`refactor: retire legacy 2d runtime entry`；独立提交全部审计确认的删除、Build Settings、测试和质量目录。随后单独提交官方验证记录。普通 push 当前分支；不 force-push、不创建 Release、不合并 PR。

## 4. 主智能体阶段审查门

1. Task 1–3：codec/validator/fixtures 与文件事务 GREEN，核对坏档不覆盖有效档。
2. Task 4–6：世界、城市、建筑、仓储、背包、合成、科技 GREEN，核对无影子真值。
3. Task 7–9：生产、活动战斗、已确认撤离 GREEN，核对预留/载荷不重复结算。
4. Task 10–12：事务协调、检查点与正式 UI/真实输入 GREEN，核对失败回滚和输入不穿透。
5. Task 13：场景重载、完整回归、质量门、四构建、官方文档和验证记录完成。
6. Task 14：仅在前一门完整通过后独立退役 2D 专属内容，再完成旧 schema 回归和三项现役 3D 构建。

每次报告必须区分“RED 已保存”“最小 GREEN 已通过”“完整回归已通过”“构建已通过”“待用户试玩”和“待真实 Windows 10/11 验收”。
