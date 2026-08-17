# 3D 生产物流、背包合成、首版科技树与资源观察实施计划

> 日期：2026-08-17<br>
> 状态：已批准需求的可执行 TDD 计划，尚未开始生产代码实现<br>
> 受控需求：`IDEA-0011`<br>
> 权威规格：`Docs/superpowers/specs/2026-08-17-3d-production-logistics-inventory-crafting-research-observability-design.md`<br>
> 精确基线：`486e874c8d345f8751695f01cc21160f85be357d`<br>
> 工作分支：`codex/production-logistics-observability-v2`<br>
> Unity：`2022.3.62f1`

## 1. 目标与不可越界项

本阶段只扩展默认正式 3D 主循环，交付以下相互连接的能力：

1. 采矿站、冶炼厂、装配厂的逐实例生产与建筑内部缓存；
2. 城市库存访问、物流范围、仓库容量、节点枯竭和五类停工原因；
3. 玩家 30 格背包、城市/建筑/背包原子转移和两条应急手工合成；
4. A16.4 首版科技树的 3D 研究界面与基础冶金、弹药装配可玩链；
5. 已完成建筑详情、常驻资源状态栏、完整资源账本与真实输入；
6. 正式配置、质量目录、可复用目录、自动化、构建和验证文档。

不可越界：

- 不实现敌人、炮塔、弹丸、电力、传送带、工人、跨城市运输或新资源 ID；
- 不把建筑建造成可合成物品，不复制现有放置、施工、撤离、节点兼容或范围规则；
- 不为冻结 2D `FormalPrototype` 新增功能或 UI；
- 不修改 schema `30`，背包、合成队列、3D 生产和 3D 研究均为会话态；
- 不把“已实现待验证”写成“已验证”，不代替用户人工试玩结论；
- 不运行 `TerrainAssetDeep`，除非实际触发地形源、导入器、数组 Builder、数组生成或发布候选规则；
- 所有新数值只来自 GDD、已批准 `IDEA-0011` 或正式配置目录；
- 每个实现任务先保存真实 RED，再做最小 GREEN；相同小决策按规格和长期维护性处理，不追逐无现实影响的极低概率边缘情况。

## 2. 预期所有权与文件地图

文件名可在不改变职责的前提下按程序集依赖微调；若需要计划外生产文件，先报告。

### 2.1 共享经济领域

```text
Assets/_Game/Scripts/Economy/ResourceDefinitionCatalog.cs
Assets/_Game/Scripts/Economy/ResourceTransaction.cs
Assets/_Game/Scripts/Economy/ResourceFlowLedger.cs
Assets/_Game/Scripts/Economy/ResourceRecipeCatalog.cs
Assets/_Game/Scripts/Economy/CraftingQueueModel.cs
Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs
Assets/_Game/Scripts/Economy/ProductionDefinitionCatalog.cs
Assets/_Game/Scripts/Economy/BuildingResourceCache.cs
Assets/_Game/Scripts/Economy/BuildingProductionState.cs
Assets/_Game/Scripts/Economy/ProductionSimulation.cs
```

- `ResourceIds` 继续是 15 个稳定 ID 真值；定义目录只补显示与堆叠配置。
- `ResourceInventory` 继续是城市数量账本；新增能力必须向后兼容旧 2D 债务和容量测试。
- 通用原子事务不能由 UI 逐项调用 `TrySpend`/`Add` 拼接。
- 合成队列只拥有预留材料、顺序、进度和阻塞状态，不读取 Unity 输入或时间。

### 2.2 正式 3D 建筑会话适配

```text
Assets/_Game/Scripts/Graybox3D/Building/Production/GrayboxProductionController3D.cs
Assets/_Game/Scripts/Graybox3D/Building/Production/GrayboxResourceTransferService3D.cs
Assets/_Game/Scripts/Graybox3D/Building/Production/GrayboxResourceNodeIdentity3D.cs
```

- 建筑身份来自 `BuildingCatalog`；节点来自 `WorldMapModel`；兼容性来自 `BuildingResourceNodeCompatibilityRules`。
- 物流连接由城市状态、建筑 footprint 与 `BuildingRangeRules` 派生，不复用旧 2D BFS 网络。
- 背包是 30 格、每格同资源最多 100 的玩家会话对象；自动生产和研究不隐式访问背包。
- 完成建筑实例独立拥有缓存、周期、暂停、节点绑定和停工原因。

### 2.3 3D 研究运行时

```text
Assets/_Game/Scripts/Research/DemoResearchCatalog.cs
Assets/_Game/Scripts/Research/DemoResearchRuntime.cs
Assets/_Game/Scripts/Graybox3D/Building/Research/GrayboxResearchController3D.cs
```

- 保留现有 43 节点长期目录和冻结 2D 行为；3D Demo release profile 执行 A16.4 六节点。
- 本轮仅基础冶金、弹药装配可启动；后三节点只读预览并显示后续里程碑原因。
- 开始和推进都要求至少一座已完成、玩家拥有、未撤离锁定的研究站；失去最后一座时只暂停、不取消、不退款，恢复后继续。

### 2.4 3D UI、输入与场景

```text
Assets/_Game/Scripts/Graybox3D/Usability/Production/GrayboxInventoryCraftingView3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/Production/GrayboxResearchTreeView3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/Production/GrayboxResourceStatusBar3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/Production/GrayboxBuildingDetailView3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/Production/GrayboxPanelCoordinator3D.cs
Assets/_Game/Editor/GrayboxSceneAuthoring.cs
Assets/_Game/Scenes/GrayboxPrototype3D.unity
```

- `E` 打开背包/合成同一面板；`T` 打开科技树；两者与 `B` 目录互斥。
- 输入统一进入现有 `GrayboxUsabilityInputCoordinator3D`；面板不得自行轮询键盘。
- 常驻资源栏不拦截世界输入；可点击的账本入口使用独立明确命中区。
- UI 只读不可变快照并提交命令，不自行推导物流、产量或研究完成。

## 3. TDD 实施任务

### Task 0：文档门与基线记录

1. 更新 GDD、Docs/05、Docs/06、权威规格和本计划；状态只能是已批准、设计中或未实现。
2. 运行文档差异审查、链接检查和项目文档验证。
3. 独立提交文档阶段，普通 push `codex/production-logistics-observability-v2`。
4. 再次确认 HEAD、工作区、LFS、Unity 版本和无错误工程实例操作。
5. 记录既有 `BuildingCatalog` 冶炼厂/研究站施工成本差异与科技关注度效果均不在本里程碑改动，禁止借实现顺手修正。

### Task 1：资源定义与城市账本安全边界

RED：

- 15 个 `ResourceIds.All` 均有唯一、稳定的定义、名称、顺序、栈上限与图标回退键；
- 未登记或空 ID 失败关闭；
- 多输入扣除、输出预检和回滚保持总量；
- 正式基础容量 150，仓库每座 +150；容量降低后允许超额保留但禁止继续入库；
- 正式新 3D 会话初始库存为铁矿 20、合金 20、弹药 30、生物质 10，其余 11 种资源为 0，场景不得继续使用开发夹具库存；
- 旧 2D `ResourceInventory` 默认行为与债务测试不回退。

GREEN：实现最小目录、原子事务和 3D 安全容量策略；不在控制器/UI 散落资源数字。

### Task 2：30 格背包与原子转移

RED：

- 30 格、每格单一资源、每格最多 100；
- 同类稳定合并、稳定空格顺序、拆半、逐个、整堆、部分接收；
- 城市↔背包、建筑缓存↔背包转移总量守恒；
- 目标失效、容量改变、资源错误或事务中断时完全回滚；
- 任意建筑缓存都只有在控制目标处于正式配置交互半径、且建筑完成/所有权/撤离锁定资格通过时允许人工访问；联网不能绕过该门槛。

GREEN：实现纯 C# 背包和统一转移服务；禁止 UI 直接修改城市或缓存。

### Task 3：应急手工合成

正式配方：

- `core.crafting.field-alloy`：4 铁矿 → 1 合金，12 秒，基础冶金解锁；
- `core.crafting.field-ammunition`：4 合金 → 2 弹药，12 秒，弹药装配解锁。

RED：

- 配方稳定 ID、输入、输出、时长与科技门；
- 左键 1、右键必须完整 5 次否则拒绝、Shift+左键最大可承担量，FIFO 总上限 20 次；
- 入队原子预留；取消 100% 返还；返还无空间时拒绝取消；
- 输出满时停在完成边界，不丢失也不重复；
- 关闭面板继续，战术/系统暂停冻结，恢复后从精确进度继续；
- 不自动链式合成，机器配方不能误排手工队列；
- 大 delta 与分帧推进结果一致。

GREEN：实现通用配方目录和会话队列；建筑施工成本仍由现有施工系统直接支付。

### Task 4：生产配置、建筑缓存与节点身份

RED：

- 采矿站无输入、3 秒产 1；冶炼厂 2 铁→1 合金/6 秒；装配厂 2 合金→2 弹药/6 秒；
- 三个机器配方 ID 分别为 `core.production.extract-node-resource`、`core.production.smelt-alloy`、`core.production.assemble-ammunition`，与两条应急配方共用正式资源配方目录但数值和用途独立；
- `3/6/6` 秒为最终有效周期，不叠加冻结 2D Fortress `1.25` 倍率；
- 缓存容量分别为矿站输出 20、冶炼输入 20/输出 10、装配输入 20/输出 30；
- 每实例状态隔离，施工中/撤离锁定/废弃/移除实例不生产；
- 矿站保存放置批准时的稳定节点 ID，铁矿和能晶兼容继续由统一规则决定；
- 完成周期才通过 `WorldMapModel.Harvest` 扣节点，枯竭无重生。
- 冶炼/装配在周期开始即原子取得输入；随后脱网仍完成已持有批次；完成边界输出满时保持输入与 100% 进度，空间恢复后只提交一次输出。

GREEN：实现不可变生产目录、缓存包装器、节点 ID 和 session 生命周期接线。

### Task 5：物流、仓库与确定性模拟

RED：

- 内城实例自动联网；外城只有 Fortress 且 footprint 全部在既有范围时联网；
- 不要求相邻、不能建筑中继，脱离后保留缓存且不能访问城市/其它缓存；
- 已开始周期可完成；矿站脱网仍可采集至枯竭或输出满；
- 每个物流步严格先卸既有输出、再补内部输入、最后推进生产；本步新产物等下一物流步卸载；
- 两矿、两冶炼、一装配的供给公平、顺序稳定、总量守恒；
- 仓库只扩容、不扩距；脱网不降容，废弃、移除、撤离锁定或失去所有权才降容，且不截断超额库存；
- `Tick(12)` 与十二次 `Tick(1)`、实例枚举乱序结果一致；
- 全局暂停、城市运行/物流资格、建筑暂停分别有单一所有者；新 3D 不消费冻结 2D 生产倍率。

GREEN：使用事件边界推进和稳定实例顺序；正常 tick 不使用 `FindObjectsOfType`、LINQ 或逐帧临时集合。

### Task 6：停工原因与建筑详情快照

RED：

- 玩家暂停、矿脉枯竭、输出已满、不在物流范围、缺少输入五类主原因按批准优先级显示；
- 全局暂停不是建筑停工原因；
- 运行快照包含基础/有效周期、进度、缓存、连接、节点余量和原因；
- UI 输入不自行重复计算状态。

GREEN：生产层发布 revision 驱动的不可变快照；完成建筑选择与施工选择职责分离。

### Task 7：首版科技树运行时

RED：

- 废料加工初始完成；基础冶金铁 10/20 秒；弹药装配合金 10/30 秒；
- 自动防御、加固结构、遗产解析显示 A16.4 数据但不可启动；
- 基础冶金解锁冶炼厂与应急合金，弹药装配解锁装配厂与应急弹药；
- 四个既有研究 ID 保持稳定，新增 `core.research.scrap-processing` 与 `core.research.reinforced-structures`，不得为已有语义建平行 ID；
- 单活动研究；开始原子扣费；取消返还 80% 并清零进度，完整退款空间不足时拒绝取消；
- Fortress 100%，Mobile/Deploying/Packing 50%；暂停冻结；开始和推进都要求已完成、玩家拥有、未撤离锁定的研究站，失去最后一座时暂停不取消；
- 不新增队列或 schema 字段。

GREEN：向后兼容扩展研究成本表达，增加 3D release profile 和薄运行时；不接冻结 2D `ResearchController`。

### Task 8：资源流水与 Civilization 风格状态栏

RED：

- 五基础资源常驻；合金/弹药在获得或解锁后加入；
- 显示数量/有效容量和真实净流速；
- 悬停快照包含收入、消耗、容量来源、预计满仓/耗尽；
- 点击打开 15 资源完整账本；未发现路线资源不污染常驻栏；
- 账本和 HUD 不修改库存、不逐帧重建、不遮挡世界输入。

GREEN：账本记录正式事务来源；UI 按 revision 更新，流速窗口和显示精度来自配置。

### Task 9：正式 3D UI 与真实输入

RED EditMode：

- E/T/B 互斥；输入焦点、施工确认、撤离、面板和系统菜单 Esc 顺序确定；
- 背包拖放、拆分、快速转移、配方可制作原因、队列阻塞与取消投影正确；
- 已完成建筑可选，施工中实例仍由原控制器拥有；
- 常驻资源栏非交互区域 `raycastTarget=false`；
- 重复配置/销毁没有重复监听器。

RED PlayMode：必须使用真实 `InputSystem`/`InputSystemUIInputModule` 覆盖：

- E 打开背包并切换合成页，T 打开科技树；
- 真实鼠标拖拽、左右键、Shift、队列取消与点击防穿透；
- 面板打开时世界规则继续，Space/系统菜单暂停时规则冻结；
- 真实点击三类完成建筑显示并刷新生产详情；
- 五类停工状态、研究推进和资源栏由真实场景状态驱动。

GREEN：由一个 3D 面板协调器统一输入所有权；不新增并行键盘轮询。

### Task 10：场景 authoring、资产与质量目录

1. 为七种常用资源提供清楚、可替换的程序化/占位图标和统一未知图标；不声称正式美术验收。
2. `GrayboxSceneAuthoring` 生成唯一稳定组件、Canvas、EventSystem 引用；连续运行两次不累积对象或监听器。
3. 更新 `Docs/Engineering/project-quality-catalog.json` 的经济、研究、UI、场景、测试和资源映射。
4. 更新 `Docs/09-Reusable-Project-Catalog-ZH.md`，明确目录、事务、队列、快照和 UI 边界。
5. 添加 schema30、冻结 2D、稳定 GUID/localFileID 和无计划外资产保护。

### Task 11：验证与独立审查

按风险逐层运行：

1. 每个 RED/GREEN 聚焦 fixture；
2. 生产、背包、合成、研究、UI、场景合同联合测试；
3. 完整日常 EditMode（按批准规则排除 `TerrainAssetDeep`）；
4. 完整 PlayMode；
5. 无界面编译；
6. 独立静态审查：资源守恒、状态所有权、输入优先级、无重复规则、schema30、冻结 2D；
7. 运行时验证：真实输入闭环、长期规则时间、重连/暂停/满仓/枯竭、空闲及活动 UI 性能；
8. 仅当涉及发布候选时按文档规则补相应深度测试。

任何失败先定位到最小 fixture；不得通过放宽断言、跳过用例或修改冻结规则掩盖问题。

### Task 12：构建、文档生成与交付

1. 运行官方文档生成、验证、Documentation Attention、测试结果分析和 `RecordVerification`；
2. 构建 Windows Release 3D、Development 3D、Legacy 2D 与 macOS universal；
3. 不把本机 macOS 或自动化结果冒充真实 Windows 10/11 GPU/显存/视觉验收；
4. 更新 Latest Verification，只记录实际产生且可核对的证据；
5. 更新 IDEA-0011 为“已实现待用户验证”，保留 Ruins/Cliff 与 Windows 人工复验未完成状态；
6. 分阶段提交，普通 push；不 force-push、不创建 Release、不自动合并 PR。

## 4. 阶段报告门

主智能体在以下节点向用户报告后再继续：

1. 正式需求、规格和实施计划完成并验证；
2. 资源/背包/合成领域 GREEN；
3. 生产/物流/科技领域 GREEN；
4. 3D UI 与真实输入 GREEN；
5. 完整测试、构建、文档和最终静态审查完成。

报告必须区分：已设计、已实现、自动化已验证、运行时已观察、待用户试玩、待真实 Windows 验收。
