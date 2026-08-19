# 正式撤离与完整 3D 垂直切片实施计划

> 日期：2026-08-19<br>
> 状态：已批准需求的可执行 TDD 计划，尚未实施<br>
> 受控需求：`IDEA-0014`<br>
> 权威规则：`Docs/06-User-Feedback-and-Change-Control-ZH.md` 的 `IDEA-0014`、`Docs/01-Game-Design-Document-ZH.md` A4.2/A4.5/A7.2/A16.1/A16.2/A16.10、`Docs/05-Formal-Development-Roadmap-ZH.md` F1F<br>
> 精确计划基线：`e2c23e1`（`docs: approve formal evacuation vertical slice`）<br>
> 工作分支：`codex/formal-evacuation-vertical-slice`<br>
> Unity：`2022.3.62f1`

## 1. 目标、所有权与硬边界

本阶段只扩展默认正式 3D 主循环：移动到合法地点，按 `F` 展开，使用真实 `B/T` 输入完成研究、生产与防御，在敌人存活时按 `F` 进入撤离，原子处理地面资产，完成收起后再次移动。

所有权固定如下：

- `CityDeploymentModel` 只拥有 `Mobile → Deploying → Fortress → Packing → Mobile` 状态、进度与取消恢复；正式 `5/8` 秒来自 `CityDeploymentRules`，实际推进只消费外部提供的生产力、开发规则时间和战斗倍率。
- `PopulationModel` 继续唯一拥有 GDD 的人口生产力公式；3D 正式会话使用初始人口 `100`、容量 `150`，并公开统一只读倍率。现有开发施工加速改为独立规则时间倍率，不得冒充玩法生产力。
- `GrayboxDefenseRuntimeSnapshot3D` 是战斗上下文真值；`AliveEnemyCount > 0` 才是战斗，15 秒警告不是战斗。
- `BuildingEvacuationRules` 是纯规则所有者，生成不可变的处理上下文、退款、基础时长、未完成比例和物资后果，不访问 Unity、库存或 UI。
- `CityResourceStorageModel` 是城市网络容量与原子批量接收所有者；`GrayboxBuildingSession3D` 是建筑生命周期与仓库迁移所有者；生产缓存和炮塔弹药仍由各自运行时拥有，通过捕获/提交边界参与撤离，UI 不直接修改它们。
- `GrayboxEvacuationController3D` 只编排清单、冻结批次战斗上下文、稳定队列、逐项提交、阻塞与重试；每项提交前重新读取真实容量。
- `GrayboxBuildingMenuView3D` 只渲染不可变撤离视图并提交命令，不计算退款、容量、战斗或进度。
- `GrayboxUsabilityInputCoordinator3D` 继续是正式 Input System 总入口；撤离清单不得阻断合法 `Space` 战术暂停。

硬边界：

- schema 保持 `30`，不新增正式存档字段、迁移器或兼容适配；新增撤离状态全部为 3D 会话态。
- 冻结 2D `FormalPrototype` 不接新功能，只跑既有回归和 legacy 2D 构建。
- 不新增敌人、炮塔、波次、资源、电力、传送带、工人、跨城运输、前哨、建筑受伤或正式失败结算。
- 不复制容量、施工、生产、研究、防御或战斗真值；不把自动化、macOS 或跨平台构建写成用户试玩或真实 Windows 验收。
- 本计划不触碰地形源、导入器、Texture2DArray Builder 或数组生成，因此日常 EditMode 排除 `TerrainAssetDeep`，本里程碑不运行该类深测。
- 每个实现任务必须先保存预期范围内的真实 RED，再写最小 GREEN；出现 schema 变更、计划外生产文件或权威规则冲突时立即停止并报告。

## 2. 统一命令约定

开始实现前在仓库根目录建立本轮证据目录：

```bash
export WASTECITY_PROJECT_ROOT="$(git rev-parse --show-toplevel)"
export WASTECITY_UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
export WASTECITY_EVIDENCE_ROOT="/tmp/wastecity-formal-evacuation-20260819"
mkdir -p "$WASTECITY_EVIDENCE_ROOT"
git status --short --branch
git lfs fsck
"$WASTECITY_UNITY_BIN" -version
```

所有测试命令都显式提供平台、过滤器、XML 与日志，且 `-runTests` **不得**带 `-quit`。RED 只有计划所述的新断言失败才可继续；编译错误、夹具错误、缺 XML 或无关回归均为停止门。每个 GREEN 后先检查 XML 的 `failed="0"`，再提交该任务列出的精确路径。

## 3. TDD 实施任务

### Task 1：正式部署状态机、生产力与转换取消

**预计文件**

- Modify: `Assets/_Game/Scripts/City/CityDeploymentRules.cs`
- Modify: `Assets/_Game/Scripts/City/CityDeploymentModel.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/PopulationAndCapacityTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/CityDeploymentRulesTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs`

**RED**

新增并先失败：正式基础时长恰为展开 `5s`、收起 `8s`；GDD 正式 `100/150` 人口得到 `100%` 生产力且开发规则时间倍率与之分离；实际推进为 `delta × 正式生产力 × 可选开发规则时间倍率`；暂停不推进；Deploying/Packing 再按 `F` 回到先前稳定态并清除进度，重新开始必须走完整时长且无资源变化；大 delta 与分帧结果一致。把旧 `3/5` 测试改为正式 `5/8` 断言。战斗收起倍率留到 Task 5 从权威防御快照接线，不能在本任务伪造第二个战斗状态。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.PopulationAndCapacityTests|WasteCity.Tests.CityDeploymentRulesTests|WasteCity.Tests.GrayboxMobileCityController3DTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-01-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-01-red.log"
```

预期 RED：仅正式时长、倍率和转换取消断言失败。

**最小 GREEN**

把正式 `5/8` 基础时长集中在 `CityDeploymentRules`；3D 正式会话复用 `PopulationModel(100, 150)` 并分别公开正式生产力和开发规则时间倍率，施工、部署和完整拆除共用该组合但不改自动生产周期；让模型接收非负有效推进量并保存“转换前稳定态”，继续由模型唯一改变模式。开发加速只改变规则时间，不改变基础值、人口或 UI 生产力。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.PopulationAndCapacityTests|WasteCity.Tests.CityDeploymentRulesTests|WasteCity.Tests.GrayboxMobileCityController3DTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-01-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-01-green.log"
```

**提交点**：`feat: formalize deployment and packing transitions`；只暂存上述生产与 EditMode 测试路径。

### Task 2：撤离纯规则与冻结批次上下文

**预计文件**

- Modify: `Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`

**RED**

新增纯规则用例：和平完整拆除 `80%`、基础时长为原施工时间 `50%`；战斗完整拆除 `60%`、基础 `5s`；快速 `50%/0s`；遗弃 `0/0s`。未完成施工点先乘剩余施工比例再乘处理比例，并继续复用 `ConstructionRefundRules` 的确定性取整。不可变 work/view 同时包含处理方式、冻结的和平/战斗上下文、正式人口生产力、原始比例、退款、基础/有效时长和物资后果；开发规则时间加速不写入玩法批次；15 秒警告生成和平结果；确认后的战斗结果不因敌人后来死亡改变。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxEvacuationTests -testResults "$WASTECITY_EVIDENCE_ROOT/task-02-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-02-red.log"
```

预期 RED：仅缺少战斗上下文、正式时长/退款和不可变投影断言失败。

**最小 GREEN**

在现有规则文件中增加只读上下文与视图值；`Create` 必须显式接收上下文和生产力，严禁规则层读取防御控制器、库存或 Unity 时间。稳定队列仍按 `StableInstanceId` 排序。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxEvacuationTests -testResults "$WASTECITY_EVIDENCE_ROOT/task-02-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-02-green.log"
```

**提交点**：`feat: formalize evacuation treatment rules`；只暂存本任务两个路径。

### Task 3：原子容量计划、内部物资与失败零写入

**预计文件**

- Modify: `Assets/_Game/Scripts/Economy/ResourceTransaction.cs`
- Modify: `Assets/_Game/Scripts/Economy/CityResourceStorageModel.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/CityResourceStorageModelTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxProductionRuntimeTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxFirstDefenseRuntimeTests.cs`

**RED**

先覆盖：

- 完整/快速拆除把建筑输入、输出、炮塔内部弹药、仓库内容和建筑退款聚合成一次精确接收计划；全部容纳才提交。
- 容量不足返回稳定、精确的额外容量和相关资源，不移动建筑、不迁移仓库、不增加退款、不清空缓存或弹药。
- 计划通过后、提交前容量变化会丢弃旧计划并重新预检；资格、容量、占格或表现 prepare 拒绝发生在领域写入前，城市网络、仓库、缓存、弹药、网格和建筑所有权均保持不变。
- 遗弃丢弃生产缓存和炮塔弹药，但非空仓库内容仍必须原子迁移；迁移失败则遗弃整体失败。
- 城市路由保持稳定仓库 ID 顺序，旧单笔/批量事务和过滤仓库行为不回退。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.CityResourceStorageModelTests|WasteCity.Tests.GrayboxWarehouseStorageIntegrationTests|WasteCity.Tests.GrayboxProductionRuntimeTests|WasteCity.Tests.GrayboxFirstDefenseRuntimeTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-03-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-03-red.log"
```

预期 RED：仅精确批量接收、跨运行时载荷和整体回滚断言失败。

**最小 GREEN**

扩展统一城市存储为“计划 → revision 重验 → 单次完整 commit”接口；生产与防御只提供按稳定建筑 ID 捕获和确定性清除的内部载荷，不把缓存所有权迁给 session；session 在 prepare 阶段统一组合仓库迁移、内部载荷、退款、占格和表现可处理性。仓储 commit 成功后只执行已验证、无失败的内存收口并单调增加 revision；表现异常由会话真值重建，不承诺倒退已发布 revision。不得用多次 `AddToNetwork` 的部分成功拼接原子操作。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.CityResourceStorageModelTests|WasteCity.Tests.GrayboxWarehouseStorageIntegrationTests|WasteCity.Tests.GrayboxProductionRuntimeTests|WasteCity.Tests.GrayboxFirstDefenseRuntimeTests|WasteCity.Tests.GrayboxEvacuationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-03-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-03-green.log"
```

**提交点**：`feat: make evacuation inventory commits atomic`；只暂存本任务列出的生产与测试路径。

### Task 4：生产、研究与防御的撤离锁

**预计文件**

- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingOperationalAccess3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs`
- Modify: `Assets/_Game/Scripts/Research/DemoResearchRuntime.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxProductionRuntimeTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/DemoResearchRuntimeTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxFirstDefenseRuntimeTests.cs`

**RED**

锁定完整拆除队列时，生产实例暂停但缓存/周期不被替换，炮塔停止射击/补弹但保存弹药与目标状态，最后一座合格研究站被锁时研究暂停但不取消、不退款；回滚解锁后都从原状态继续。快速/遗弃只在原子提交成功时移除运行态。清单打开但尚未确认不锁任何系统。战术/系统暂停同时冻结撤离、部署、生产、研究和战斗。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxProductionRuntimeTests|WasteCity.Tests.DemoResearchRuntimeTests|WasteCity.Tests.GrayboxFirstDefenseRuntimeTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-04-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-04-red.log"
```

预期 RED：仅跨系统锁定、保留和恢复断言失败。

**最小 GREEN**

所有资格判断继续复用 `IsEvacuationLocked`/`GrayboxBuildingOperationalAccess3D`；只补足缺失的保留与恢复接口，不建立第二套暂停布尔值，不让清单状态自动暂停世界。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxProductionRuntimeTests|WasteCity.Tests.DemoResearchRuntimeTests|WasteCity.Tests.GrayboxFirstDefenseRuntimeTests|WasteCity.Tests.GrayboxProductionLifecycleTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-04-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-04-green.log"
```

**提交点**：`feat: preserve runtime state behind evacuation locks`。

### Task 5：撤离编排、逐项重验与容量阻塞重试

**预计文件**

- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseController3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs`

**RED**

新增：打开清单时预览随权威 `AliveEnemyCount` 刷新；确认时冻结一次批次和平/战斗上下文与正式人口生产力；完整拆除按稳定 ID 串行，以 `baseSeconds / productivity` 推进，开发规则时间倍率只加速 Tick；战斗批次使用已冻结的 `60%/5s`，敌人后来死亡不改变它。Packing 每个推进片段从同一防御快照读取实时战斗态，敌人存活时在正式生产力与可选开发规则时间推进后乘 `0.7`，警告期或敌人归零时不降速。每项提交前重新预检真实存储；容量不足保持当前建筑、资源、work、批次上下文和未提交撤离锁，进入 Blocked 并显示精确原因；玩家通过 `E` 把城市物资移入背包腾出空间后，只重验当前容量计划并从原 work 继续，不得重新确认新和平批次或重复已提交退款。暂停不推进，处理开始后不能取消；完成全部地面资产后才请求 Packing。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxEvacuationTests|WasteCity.Tests.GrayboxWarehouseStorageIntegrationTests|WasteCity.Tests.GrayboxMobileCityController3DTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-05-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-05-red.log"
```

预期 RED：仅动态预览、冻结批次、当前项原子容量阻塞/重试和生产力时钟失败。

**最小 GREEN**

控制器保存不可变批次 context、稳定 work 队列、当前索引/剩余时间和 Blocked 原因；容量失败保持原 context/work/锁，重试只请求城市存储用最新 revision 重建当前项目计划。控制器清理或场景退出才恢复未提交锁；清单和队列不持有库存副本。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxEvacuationTests|WasteCity.Tests.GrayboxWarehouseStorageIntegrationTests|WasteCity.Tests.GrayboxMobileCityController3DTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-05-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-05-green.log"
```

**提交点**：`feat: orchestrate formal evacuation batches`。

### Task 6：不可变撤离视图与正式可观察性

**预计文件**

- Modify: `Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`

**RED**

UI 快照逐项显示：处理方式、和平/战斗标签、预期退款、基础/有效时长、未完成比例、生产输入/输出与炮塔弹药后果、仓库必须迁移、精确额外容量和失败原因；处理页稳定显示队列、当前项和剩余时间，Blocked 页保留原批次标签、缺口、“按 E 腾出城市容量”提示与“重新检查容量”命令。旧快照在模型变化后保持不变；反射/源合同证明 view 不引用城市存储、防御运行时或 `ConstructionRefundRules`，不重新计算退款、容量或战斗状态；无 revision 变化不重建行或监听器。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxBuildingUiAndInputTests|WasteCity.Tests.GrayboxEvacuationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-06-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-06-red.log"
```

预期 RED：仅新视图字段、不可变边界、失败提示和稳定刷新断言失败。

**最小 GREEN**

控制器发布 revision 驱动的只读 view；menu 只格式化已提供字段并发出 assignment/confirm/retry/cancel 命令。遗弃行必须明确警告生产缓存与炮塔弹药会丢失，仓库内容不会静默删除。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxBuildingUiAndInputTests|WasteCity.Tests.GrayboxEvacuationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-06-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-06-green.log"
```

**提交点**：`feat: expose immutable evacuation observability`。

### Task 7：真实 Input System、暂停优先级与场景合同

**预计文件**

- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

**RED**

EditMode 锁定输入优先级：`F` 继续是唯一展开/收起键；转换中 `F` 取消；清单/处理状态消费 `F/B/E/T/世界点击`，但 `Space` 仍到达战术暂停；Blocked 状态只额外允许 `E` 打开既有背包/城市库存，关闭后回到同一冻结批次；系统菜单暂停同样冻结规则；Escape 只按既有清单取消/处理不可取消规则执行。PlayMode 使用 `InputTestFixture`、虚拟 Keyboard/Mouse 与真实 `InputSystemUIInputModule` 验证 F、Space、Blocked→E 腾容量→UGUI 重新检查、处理按钮和点击防穿透，不直接调用控制器命令。Scene contract 要求唯一序列化引用，authoring 连跑两次不重复对象/监听器。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxBuildingUiAndInputTests|WasteCity.Tests.GrayboxSceneContractTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-07-red-edit.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-07-red-edit.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests -testResults "$WASTECITY_EVIDENCE_ROOT/task-07-red-play.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-07-red-play.log"
```

预期 RED：只失败于正式撤离输入、暂停透传、真实 UGUI 和新场景引用。

**最小 GREEN**

复用总输入协调器现有先处理 Space 的路径；建筑拦截器只声明其余通道所有权。通过 authoring 接齐已有 session/city/production/defense/operations/evacuation 实例，不创建平行控制器。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxBuildingUiAndInputTests|WasteCity.Tests.GrayboxSceneContractTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-07-green-edit.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-07-green-edit.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testFilter 'WasteCity.Tests.GrayboxBuildingRuntimeSceneTests|WasteCity.Tests.GrayboxDefenseRuntimeInputTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-07-green-play.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-07-green-play.log"
```

**提交点**：`feat: wire formal evacuation input and scene`。

### Task 8：六段真实输入 E2E

**预计文件**

- Create: `Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs`
- Create: `Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify through the official authoring command only: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Modify only if a reusable fixture seam is required: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

**RED**

先增加场景合同断言，固定 `GrayboxMobileCityController3D.ruleTimeSourceBehaviour` 必须序列化指向场景中唯一的 `GrayboxBuildingSession3D`。该断言应先因当前空引用精确 RED；随后由 `GrayboxSceneAuthoring` 正式补齐引用，运行官方场景重写并验证连续两次生成幂等。此项 GREEN 完成前不得运行下述 E2E；E2E 不得在测试内调用 `ConfigureRuleTimeSource` 掩盖接线缺口。

新增单一可读的六段场景用例并在每段保存明确断言：

1. 真实右键寻路到合法位置，真实 `F` 完成展开；
2. 真实 `B` 建研究站，真实 `T` 顺序研究基础冶金、弹药装配、自动防御；
3. 真实 `B` 建成 `2 采矿站 → 2 冶炼厂 → 1 装配厂 → 1 机枪塔`；
4. 等待真实生产物流产出弹药，等待教程敌人实际生成并由炮塔消耗弹药射击；
5. 敌人仍存活时真实 `F`，通过真实 UGUI 分配地面资产、确认并完成原子撤离；
6. 等待 `8s / productivity` 收起结束，再用真实移动/右键输入证明城市可继续行驶。

夹具只允许在首次玩法输入前提供确定性初始资源、将测试会话人口确定性设为 `200`，以及设置规则时间加速。人口 `200` 只用于跨越研究站既有最低人口门槛；不得改变正式默认人口/上限 `100/150`、研究站门槛或人口生产力语义。仍不得直接解锁研究、直接完成建筑、直接注入产物或弹药、直接杀敌、直接切换城市模式，或直接调用撤离入口/提交。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxFormalEvacuationVerticalSliceTests -testResults "$WASTECITY_EVIDENCE_ROOT/task-08-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-08-red.log"
```

预期 RED 分两段：前置场景合同先精确失败于空的 `ruleTimeSourceBehaviour`；完成正式接线和幂等 GREEN 后，E2E 才沿真实链路在第一个尚未接通的撤离步骤失败，而不是夹具、场景加载或规则时间接线失败。

**最小 GREEN**

只补 E2E 暴露出的正式接线缺口；不得为测试新增生产旁路。修复后同时复跑三条既有真实输入主循环。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testFilter 'WasteCity.Tests.GrayboxFormalEvacuationVerticalSliceTests|WasteCity.Tests.GrayboxBuildingRuntimeSceneTests|WasteCity.Tests.GrayboxProductionObservabilityRuntimeInputTests|WasteCity.Tests.GrayboxDefenseRuntimeInputTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-08-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-08-green.log"
```

**提交点**：`test: prove the formal evacuation vertical slice`；只暂存新 E2E 测试及为真实缺口所需、已在前序任务列明的生产路径。

### Task 9：活跃生产、八敌人、防御 HUD 与撤离 UI 混合性能

**预计文件**

- Modify: `Assets/_Game/Editor/GrayboxPerformanceProbe.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxDefenseSnapshotStabilityTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalEvacuationPerformanceTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxFormalEvacuationPerformanceTests.cs.meta`

**RED**

构造正式生产链持续运行、8 名既有教程敌人存活、防御 HUD 可见、撤离清单或处理页可见的混合夹具。冻结：稳定快照不重复刷新 UI；暂停帧不推进任何规则；预热后 300 次无状态变化的输入/撤离/UI 适配调用分配 `0 B`；活跃防御固定步快照继续不超过既有 `64 KB/300` 样本预算；对象/监听器数量有界；外部探针公开 `MeasureFormalEvacuationMixedPerformance()` 并记录 300 帧时间、GC、生产/防御/HUD/撤离标记。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalEvacuationPerformanceTests|WasteCity.Tests.GrayboxDefenseSnapshotStabilityTests|WasteCity.Tests.GrayboxBuildAndPerformanceTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-09-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-09-red.log"
```

预期 RED：仅缺少混合夹具、探针入口、撤离 UI 稳定性与相应预算断言失败。

**最小 GREEN**

复用生产与防御现有快照，撤离 view 按 revision 更新；热路径不得使用 `FindObjectsOfType`、LINQ 或逐帧临时集合。探针只测量，不改变玩法配置。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxFormalEvacuationPerformanceTests|WasteCity.Tests.GrayboxDefenseSnapshotStabilityTests|WasteCity.Tests.GrayboxBuildAndPerformanceTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-09-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-09-green.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.MeasureFormalEvacuationMixedPerformance -logFile "$WASTECITY_EVIDENCE_ROOT/task-09-probe.log"
```

**真实 GUI Profiler 门（不可由上述命令替代）**

锁定当前 WasteCity 工程的 Unity `2022.3.62f1` 实例，打开 `GrayboxPrototype3D`；由本任务新增的开发/Editor-only 准备入口建立同一个混合工作量，Game View 固定 `1920×1080`，Profiler Target 选择当前 Editor PlayMode，关闭 Deep Profile，启用 CPU Usage、Rendering、Memory。预热后清空历史并连续录制恰好 `300` 帧，保存：

```text
$WASTECITY_EVIDENCE_ROOT/task-09-gui-300frames.data
$WASTECITY_EVIDENCE_ROOT/task-09-gui-cpu.png
$WASTECITY_EVIDENCE_ROOT/task-09-gui-rendering.png
$WASTECITY_EVIDENCE_ROOT/task-09-gui-memory.png
$WASTECITY_EVIDENCE_ROOT/task-09-gui-notes.md
```

notes 必须记录 Unity/机器、分辨率、帧范围、平均/最小/最大帧时、FPS、GC、SetPass、Draw Calls/Batches、长期对象数，以及生产、八敌战斗、防御 HUD、撤离 Tick/view 构造/容量预检/提交 Marker。平均帧时门为 `<=16.67 ms`，不得存在持续对象增长或逐帧 GC。随后用现有汇总入口解析原始 `.data`，并将本轮新增 Marker 纳入输出：

```bash
WASTECITY_GUI_PROFILER_INPUT="$WASTECITY_EVIDENCE_ROOT/task-09-gui-300frames.data" WASTECITY_GUI_PROFILER_RESULT="$WASTECITY_EVIDENCE_ROOT/task-09-gui-summary.json" "$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.SummarizeGuiProfilerCapture -logFile "$WASTECITY_EVIDENCE_ROOT/task-09-gui-summary.log"
```

缺任一原始 `.data`、三张模块截图、notes 或 summary，Task 9 不得标记完成；若当前机器无法可靠执行 GUI 捕获，停止并报告环境边界。

**提交点**：`perf: add formal evacuation mixed workload gate`。

### Task 10：质量目录、回归边界与正式文档

**预计文件**

- Modify: `Docs/Engineering/project-quality-catalog.json`
- Modify: `Docs/09-Reusable-Project-Catalog-ZH.md`
- Modify as required by implementation truth: `Docs/01-Game-Design-Document-ZH.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify: `Docs/07-Project-Use-and-Development-Guide-ZH.md`
- Modify: `Docs/08-Testing-and-Bug-Location-Guide-ZH.md`
- Modify: `Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs`
- Generated only by official tool: `Docs/Generated/*`

**RED**

先扩展质量合同：所有新增生产/测试/场景路径都有 feature/reuse/test 归属；复用目录写明部署状态、纯规则、原子事务、内部载荷、不可变 view 和真实输入边界；schema 文本仍为 `30`；冻结 2D 不出现新接线；指南包含聚焦命令、六段 E2E、混合探针和故障定位；Docs/06 在全部自动化前不得超过“开发中”。

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.ProjectQualityCatalogTests|WasteCity.Tests.ProjectQualityIntegrationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-10-red.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-10-red.log"
```

预期 RED：只失败于新增路径未登记、复用/指南文本和 schema/冻结边界合同。

**最小 GREEN**

按真实实现更新目录和人工文档，再由官方工具生成 `Docs/Generated`；不得手改生成清单。状态只能写“已实现待验证”，真实 Windows 与用户试玩继续未完成。

```bash
git diff --name-only e2c23e1 HEAD | sort -u > "$WASTECITY_EVIDENCE_ROOT/changed-paths.txt"
WASTECITY_QUALITY_CHANGED_PATHS="$WASTECITY_EVIDENCE_ROOT/changed-paths.txt" "$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.GenerateDocumentation -logFile "$WASTECITY_EVIDENCE_ROOT/task-10-generate.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation -logFile "$WASTECITY_EVIDENCE_ROOT/task-10-validate.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.ProjectQualityCatalogTests|WasteCity.Tests.ProjectQualityScannerTests|WasteCity.Tests.ProjectQualityValidatorTests|WasteCity.Tests.ProjectDocumentationGeneratorTests|WasteCity.Tests.ProjectTestResultAnalyzerTests|WasteCity.Tests.ProjectQualityIntegrationTests' -testResults "$WASTECITY_EVIDENCE_ROOT/task-10-green.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/task-10-green.log"
```

**提交点**：`docs: catalog formal evacuation vertical slice`；提交人工文档、质量目录/测试和工具真实生成的 `Docs/Generated`，不得把临时证据入库。

### Task 11：完整回归、质量门、四构建与验证记录

**预计文件**

- Modify only after all evidence exists: `Docs/Generated/Latest-Verification-ZH.md`
- No production changes are allowed during evidence recording;若失败，回到对应最小任务修复并重新开始本任务。

**完整测试与编译**

```bash
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform EditMode -testCategory '!TerrainAssetDeep' -testResults "$WASTECITY_EVIDENCE_ROOT/full-editmode.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/full-editmode.log"
"$WASTECITY_UNITY_BIN" -batchmode -projectPath "$WASTECITY_PROJECT_ROOT" -runTests -testPlatform PlayMode -testResults "$WASTECITY_EVIDENCE_ROOT/full-playmode.xml" -logFile "$WASTECITY_EVIDENCE_ROOT/full-playmode.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -logFile "$WASTECITY_EVIDENCE_ROOT/compile.log"
```

完整 EditMode/PlayMode 必须 `failed=0` 且 `skipped=0`；确认 schema30 合同、`GrayboxSceneContractTests`、`SceneContractTests` 和冻结 2D 回归实际进入 EditMode XML。Terrain 未触碰，因此只记录 `TerrainAssetDeep` 按日常规则排除，不冒充通过。

**四个正式构建**

```bash
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows -logFile "$WASTECITY_EVIDENCE_ROOT/build-windows-release-3d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment -logFile "$WASTECITY_EVIDENCE_ROOT/build-windows-development-3d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D -logFile "$WASTECITY_EVIDENCE_ROOT/build-windows-legacy-2d.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.FormalBuildTools.BuildMacOSGraybox3D -logFile "$WASTECITY_EVIDENCE_ROOT/build-macos-universal-3d.log"
```

核对四份日志、目标格式、场景入口和构建设置恢复；同时再次核对 Task 9 的 GUI Profiler 原始 `.data`、三图、notes 与 summary 全部存在且达到门限。任一失败不记录验证。不得自动启动或声称真实 Windows 验收。

**测试分析、文档与 RecordVerification**

```bash
WASTECITY_QUALITY_TEST_RESULTS="$WASTECITY_EVIDENCE_ROOT/full-editmode.xml" WASTECITY_QUALITY_ANALYSIS_OUTPUT="$WASTECITY_EVIDENCE_ROOT/full-editmode-analysis.txt" "$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.AnalyzeTestResults -logFile "$WASTECITY_EVIDENCE_ROOT/analyze-editmode.log"
WASTECITY_QUALITY_TEST_RESULTS="$WASTECITY_EVIDENCE_ROOT/full-playmode.xml" WASTECITY_QUALITY_ANALYSIS_OUTPUT="$WASTECITY_EVIDENCE_ROOT/full-playmode-analysis.txt" "$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.AnalyzeTestResults -logFile "$WASTECITY_EVIDENCE_ROOT/analyze-playmode.log"
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation -logFile "$WASTECITY_EVIDENCE_ROOT/final-doc-validate.log"
```

创建包含四个实际结果的 `build-summary.txt`，在**最终实现提交 SHA** 上设置 `WASTECITY_QUALITY_VERIFIED_SHA`、ISO-8601 时间、完整测试 XML、编译日志、构建摘要和 `WASTECITY_QUALITY_HUMAN_PLAYTEST='未执行；等待用户人工试玩'`，再执行：

```bash
"$WASTECITY_UNITY_BIN" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT_ROOT" -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.RecordVerification -logFile "$WASTECITY_EVIDENCE_ROOT/record-verification.log"
git diff --check
git status --short --branch
```

**最终提交点**：先提交全部实现与文档生成结果；在该实现提交上运行 `RecordVerification` 后，再以 `docs: record formal evacuation verification` 单独提交 `Docs/Generated/Latest-Verification-ZH.md`。最后普通 push 当前分支；不 force-push、不创建 Release、不合并 PR。

## 4. 阶段报告门

主智能体在以下节点审查子任务文件、RED/GREEN XML 和日志后再继续：

1. Task 1–2：部署状态机与纯撤离规则 GREEN；
2. Task 3–5：原子库存、跨系统锁和批次编排 GREEN；
3. Task 6–8：不可变 UI、真实输入和六段 E2E GREEN；
4. Task 9：混合性能门 GREEN；
5. Task 10–11：完整测试、质量、四构建、文档和验证记录完成。

每次报告必须明确区分“已实现”“聚焦自动化已通过”“完整回归已通过”“运行时已观察”“待用户试玩”和“待真实 Windows 验收”。
