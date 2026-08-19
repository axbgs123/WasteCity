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

先在[项目自动清单](Generated/Project-Inventory-ZH.md)确认功能所属文件和场景。单功能检查优先看失败报告中的“只重跑这个失败”：报告实际会在“建议复跑”下给出一个可直接复制的单类筛选。若还没有失败报告，或要补跑相关类，就在[测试自动清单](Generated/Test-Inventory-ZH.md)的“精确测试文件与测试类”表找到对应类名，手动把一到数个类名用 `|` 连起来。该附录的“可复制的测试筛选命令”目前只有全部测试类的聚合筛选，不能当作单功能命令。建造、城市、UI、地形、美术、存档和冻结 2D 的最低检查并不相同；先跑单功能，再跑相关检查，最后才完整回归。变更批准状态和需要补写的记录，以[用户反馈与变更控制](06-User-Feedback-and-Change-Control-ZH.md)为准。

## IDEA-0011 生产与界面的检查边界

`IDEA-0011` 的生产、背包、应急合成、六节点研究和资源状态栏已经实现待验证。排查规则或数据问题时，可从 `ResourceDefinitionCatalogTests`、`ResourceTransactionAndCapacityTests`、`FormalProductionSimulationTests`、`CraftingQueueModelTests`、`DemoResearchRuntimeTests`、`ManualResourceAccessRulesTests` 和 `ResourceInventoryChangeTests` 中选择最小相关类；排查 3D 生产适配与时间推进时，再看 `GrayboxProductionRuntimeTests` 和 `GrayboxProductionClockTests`；排查玩家操作、面板互斥或点击穿透时，必须补跑 `GrayboxProductionObservabilityRuntimeInputTests`，真实建造生产链则由 `GrayboxBuildingRuntimeSceneTests` 覆盖。精确类名和当前归属仍以自动生成的[测试清单](Generated/Test-Inventory-ZH.md)为准。

不要把开发补给夹具当成自然开局证据。正式会话石料为 `0`，而现有冶炼厂施工需要 `6` 石料；自动链测试会先通过显式开发补给搭建 `2 采矿站 → 2 冶炼厂 → 1 装配厂`，再清零铁矿、合金和弹药等生产物资，观察节点采收和机器加工是否自动补出完整链。该测试只验证运行时生产闭环；自然开局的石料路径应由 `IDEA-0012` 的 seed `8128` 原始内容区可采石料节点及其场景测试单独证明。若试玩仍找不到或无法采集石料，应作为 `IDEA-0012` 回归记录，不要在测试里偷偷修改正式开局或建筑成本。

本阶段的聚焦 TDD 证据不能替代最终门：日常完整 EditMode、完整 PlayMode、项目质量检查、正式构建、文档生成和 `RecordVerification` 仍要在收尾时完成。真实 Windows 10 和 Windows 11 机器的视觉、GPU、显存、内存表现和用户试玩只能由实际执行结果确认；macOS 编辑器测试或跨平台构建成功不能替代这些结论。schema 必须保持 `30`；冻结 2D 不接新 UI 或新功能并只做回归，但共享稳定 `BuildingCatalog` 的首轮研究站人口门槛 `0` 会同步生效。敌人、炮塔和弹丸不属于本轮测试通过所能证明的范围。

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

`IDEA-0014` 已按先失败后实现的顺序固定 5 秒展开、8 秒收起、转换取消、第一名敌人生成后的战斗状态、战斗收起 `-30%`、和平/战斗完整拆除、快速拆除、遗弃和确定性退款，并检查建筑内部库存等内部物资、塔内弹药、仓库内容与退款能否在同一原子容量门和容量预检下完整迁移或准确拒绝。清单和队列必须读取不可变视图。当前状态是“已实现待验证”；不得用 UI 重新计算战斗状态、退款或容量缺口，也不得把聚焦通过写成最终完整回归已经通过。

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

schema 继续保持 `30`；冻结 2D `FormalPrototype` 没有接入本功能。正式 3D 存档、前哨、迷雾、新敌人和新炮塔都不属于本里程碑测试通过能够证明的范围。Task 11 的日常完整 EditMode、完整 PlayMode、项目质量门、四个正式构建和 `RecordVerification` 仍未完成；用户试玩和真实 Windows 10 与 Windows 11 的视觉、GPU、显存、内存验收也必须由实际执行结果确认。

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
