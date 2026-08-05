# WasteCity 3D 建造选择、双网格放置、撤离处理与开发修改器设计

## 1. 文档状态与基线

- 需求记录：`IDEA-0003`
- 需求状态：已明确、已批准
- 实现状态：未实现
- 文档性质：已批准设计规格
- 运行时状态：尚未实现
- 设计日期：2026-08-05
- 权威代码基线：`5539823eae8c0308762e50aeae0f0c9dc8b96211`
- 目标场景：`Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- 默认逻辑坐标：二维整数网格 `(x, y)`
- Unity 表现坐标：XZ 平面，Y 仅表示视觉高度
- 存档边界：schema 保持 `30`，本里程碑不读写正式存档

本文只固化用户已经批准的产品与架构决定，不表示建造运行时、撤离处理或开发修改器已经完成。实施必须另行编写并批准计划，遵循测试驱动开发和完整回归门。

## 2. 决策背景

### 2.1 `BUG-0001` 与新 3D 功能的边界

`BUG-0001` 记录的是冻结 2D 场景 `FormalPrototype` 中“按 `B` 后建造菜单不显示可建造内容”的历史试玩问题。用户已明确决定：

1. 不修复冻结 2D 场景中的该问题；
2. 不为该问题产生修复提交；
3. 不得把 3D 建造功能描述成 `BUG-0001` 的修复；
4. `BUG-0001` 的实现状态为“不适用”，含义是决策无需实现，不代表问题已经解决；
5. 原始描述、运行环境、复现步骤、预期结果和实际结果继续保留为历史事实。

默认 3D 场景当前尚无建造系统。因此，本规格定义的是新的 3D 建造选择、放置、施工与撤离处理能力，而不是将 2D `PlaceholderBuildingController` 移植或修补到 3D。

### 2.2 目标

本里程碑要在现有可操作 3D 基础上形成最小但完整的建造交互：

> `B` 打开目录 → 选择建筑 → 指向内城或外城表面 → 查看合法性预览 → 确认并扣除材料 → 观察施工 → 完成建筑 → 收起前处理外城资产

开发修改器用于可靠构造测试状态，不改变正式发行构建的规则或资源约束。

## 3. 已批准方案

采用方案 A：独立 3D 建造适配层。

### 3.1 复用的规则与数据

以下现有类型继续作为唯一或基础真值：

- `BuildingCatalog`：建筑定义和普通建造目录；
- `BuildingGrid`：格子占用、放置和移除；
- `BuildingMobilityRules`：建筑位置支持、城市模式下能否施工和运行；
- `BuildingUnlockModel`：人口、研究和前置建筑门；
- `ConstructionProgress`：施工基础时长、剩余时间和进度；
- `ResourceInventory`：资源余额、容量和扣除；
- `RouteContentDisplayCatalog`：基础建筑与四条路线的显示说明；
- `CityDeploymentModel` 与 `CityMode`：城市 Mobile、Deploying、Fortress、Packing 状态；
- `PlanarCoordinateMapper3D`、`GrayboxWorldView3D` 与现有地形规则：世界坐标、地图格和通行信息；
- `GrayboxVisualSlot`：稳定视觉 ID 和程序化占位表现。

### 3.2 新增的兼容能力

只为本规格补充以下能力：

- 方向与 90 度旋转后的占地；
- 外城世界网格和内城局部网格的统一查询结果；
- 3D 表面命中、预览和选择；
- 完整合法性原因集合；
- 施工会话、确定性退款和撤离清单；
- 仅 Editor/Development Build 可见的开发修改器。

旧 API 默认方向为 `0°`。既有 2D 调用不传方向时，行为、占地和存档语义保持不变。

### 3.3 禁止的结构

- 不把 `PlaceholderBuildingController` 改成 2D/3D 双模式组件；
- 不让 3D 适配器复制 `BuildingCatalog`、解锁规则或城市模式表；
- 不以 Collider、Renderer、Transform 或 UI 状态作为玩法真值；
- 不接入正式存档、生产、物流、炮塔、敌人或战斗；
- 不新增 Unity 包，不引入正式美术。

## 4. 程序集与文件边界

### 4.1 依赖方向

```text
WasteCity.Game
    ↑
WasteCity.Graybox3D
    ↑
WasteCity.Graybox3D.Building
    ↑
WasteCity.Editor / EditModeTests / PlayModeTests
```

`WasteCity.Game` 不引用任何 Graybox 程序集。`WasteCity.Graybox3D` 不引用新的建造适配程序集。建造适配层可以读取现有 3D 城市、世界、输入和视觉接口，但不能把新建造行为反向塞入冻结 2D 控制器。

### 4.2 纯规则边界

纯规则文件位于 `Assets/_Game/Scripts/Building/`，属于 `WasteCity.Game`：

- `BuildingOrientation.cs`：四方向值、宽高旋转和旧 API 的 `0°` 默认值；
- `BuildingPlacementEvaluation.cs`：合法性原因、主原因和完整原因集合；
- `BuildingRangeRules.cs`：外城切比雪夫范围和 `8/12/24` 半径档位；
- `ConstructionRefundRules.cs`：主动取消、完整拆除和快速拆除的确定性退款；
- `BuildingEvacuationRules.cs`：撤离选择、状态和稳定处理顺序；
- `BuildingGrid.cs`：在保留旧签名的前提下增加方向重载及旋转占地。

这些文件不依赖 Unity 物理、相机、Renderer 或输入设备。

### 4.3 3D 适配边界

新增目录 `Assets/_Game/Scripts/Graybox3D/Building/` 和程序集 `WasteCity.Graybox3D.Building`。该程序集直接引用 `WasteCity.Game`、`WasteCity.Graybox3D`、`Unity.InputSystem` 和工程中已经存在的 `Unity.ugui`，不增加 Package。其唯一职责如下：

- `GrayboxBuildingSession3D`：持有本次运行会话的资源、解锁、人口、两张网格、施工点和已完成实例；
- `GrayboxBuildingCatalogPresenter3D`：产生分类、路线筛选、搜索和锁定原因的只读目录数据；
- `GrayboxBuildingSurfaceProjector3D`：将指针命中解析为外城格或内城局部格；
- `GrayboxBuildingPlacementController3D`：维护选择、方向、连续放置和完整合法性评估；
- `GrayboxConstructionController3D`：扣款、创建施工点、推进和取消施工；
- `GrayboxEvacuationController3D`：拦截收起请求、维护撤离清单并在全部处理后继续收起；
- `GrayboxBuildingInputRouter3D`：按批准的优先级消费建造输入；
- `GrayboxBuildingMenuView3D`：底部两层菜单、搜索、详情、错误和撤离清单；
- `GrayboxBuildingWorldView3D`：网格、预览、施工框架、完成占位和遗迹表现；
- `GrayboxDeveloperModifierBootstrap3D`：所有构建中均存在且可序列化的场景入口；Release 中惰性无行为；
- `GrayboxDeveloperModifier3D`：仅由 Editor/Development 编译边界创建的开发面板、输入路由与会话命令，不被场景直接序列化引用。

### 4.4 Editor 与测试边界

- `GrayboxSceneAuthoring` 只增加新组件和稳定引用，继续保持幂等和资产 GUID；
- `FormalBuildTools` 只增加显式 3D Development Windows 构建入口；
- 默认 `BuildWindows()` 保持非 Development 的 3D 正式构建；
- `BuildWindowsLegacy2D()` 继续作为冻结 2D 回归入口；
- EditMode 与 PlayMode 测试程序集直接引用所需程序集，不依赖 asmdef 传递引用。

所有新增 Unity 资产都必须提交配套 `.meta`。不得修改 `FormalPrototype.unity`、Packages、GraphicsSettings、QualitySettings 或 schema。

## 5. 逻辑坐标与双网格契约

### 5.1 外城世界网格

外城放置使用现有地图二维整数格和 `PlanarCoordinateMapper3D`：

```text
逻辑格 (x, y) → Unity (x - worldWidth × 0.5, visualY, y - worldHeight × 0.5)
```

格锚点不增加 `0.5`。建筑灰盒中心按旋转后占地锚点的平均值计算。城市中心格必须由 `PlanarCoordinateMapper3D.TryWorldToCell(cityBody.position)` 得到，转换失败时外城放置整体不可用，不钳制到地图边缘。

外城范围以该城市中心格为中心，使用切比雪夫距离：

```text
max(abs(cell.x - city.x), abs(cell.y - city.y)) <= buildRadius
```

首版 `buildRadius = 8`。规则接口保留 `12` 和 `24` 两个扩展档，但本里程碑不提供升级入口。

外城合法性还要检查地图边界、地形、障碍、城市当前 3×3 世界占地、其他建筑/施工点/遗迹和资源节点。

### 5.2 内城局部网格

内城是 `8×6` 的局部压缩网格：

- 原点和轴向随移动城市平台；
- 只保存局部整数格，不随城市移动改写为世界格；
- 每格 X/Z 尺寸固定为 `0.32` Unity 单位，完整逻辑区尺寸为 `2.56×1.92`；
- 局部左下锚点为 `(-1.28,-0.96)`，格 `(x,y)` 的中心为 `(-1.28 + (x + 0.5) × 0.32, -0.96 + (y + 0.5) × 0.32)`；
- 映射到 Unity XZ 时乘城市平台 Transform，平台表面 Y 始终取当前城市灰盒顶面上方 `0.01`，只用于命中与表现；
- Y 只用于平台和预览表现，不进入规则；
- 内城网格不改变城市在世界地图上的 3×3 占地；
- 城市移动、展开和收起时，内城建筑随城市整体移动。

### 5.3 表面自动选择

指针命中结果是值类型 `BuildingSurfaceHit`，至少包含：

- `BuildingSite.Ground` 或 `BuildingSite.InnerCity`；
- 对应逻辑格；
- 世界预览位置；
- 是否命中可用表面；
- 表面标签“外城”或“内城”。

内城平台命中优先于同屏下方的地面命中。Collider 只提供表面候选；最终格、边界和合法性由规则计算。未命中、平台外或地图外必须返回明确失败结果，不得退回 `(0,0)`。

## 6. 旋转与占地

方向只有四种：

```text
North = 0°
East  = 90°
South = 180°
West  = 270°
```

每按一次 `R` 顺时针增加 `90°`。`0°/180°` 使用定义的 `Width×Height`，`90°/270°` 使用 `Height×Width`。

`BuildingGrid` 增加带方向的 `CanPlace`、`TryPlace` 和 `TryRestore` 重载；原签名委托到 `North`。`PlacedBuilding` 增加只读方向，旧构造方式默认 `North`。升级型建筑仍必须满足占地兼容，但本里程碑不提供升级交互。

旋转只影响二维占地和 3D 占位朝向，不改变稳定建筑 ID、成本、施工时长或 schema。冻结 2D 路径不传方向，因此保持现状。

## 7. 菜单与可见性

### 7.1 底部两层结构

底层快捷栏始终可见，初期使用固定核心建筑。上层完整目录由 `B` 向上展开，再按 `B` 收起。展开目录不暂停世界。

灰盒 UI 使用运行时生成的 Unity UGUI `Canvas`、`GraphicRaycaster` 和既有 Input System 的 UI 模块，不创建正式图集或下载资源。目录、详情、搜索、确认框、撤离清单和开发面板共用一个 UI 根；世界输入在处理指针前查询该根的命中结果。场景中只允许一个 EventSystem，authoring 已有则复用、缺失则创建。

完整目录提供五类：

1. 基础；
2. 生产；
3. 物流；
4. 防御；
5. 路线。

路线类内再提供科技、修仙、生物、灵能四条路线筛选。分类和路线映射以稳定 ID 建立，不以显示名称判断。

### 7.2 目录来源

普通菜单只枚举 `BuildingCatalog.BuildMenu` 的 28 项。下列两项只作为升级目标保留，不进入普通目录：

- `HeavyMachineGunTurret`
- `SwordRidingPlatform`

固定分类如下：

| 一级分类 | 路线筛选 | 建筑 |
|---|---|---|
| 基础 | — | `Housing`、`Wall`、`ResearchStation` |
| 生产 | — | `MiningStation`、`Smelter`、`Assembler` |
| 物流 | — | `Warehouse`、`AutomatedRepairBay` |
| 防御 | — | `MachineGunTurret`、`LaserTower` |
| 路线 | 科技 | `PowerPlant` |
| 路线 | 修仙 | `SpiritFireFurnace`、`ArtifactWorkshop`、`SwordArrayTower`、`SpiritGatheringArray`、`AlchemyChamber`、`PuppetWorkshop` |
| 路线 | 生物 | `ColonyPool`、`BreedingChamber`、`SporeTower`、`MetabolicFurnace`、`AcidTower`、`BehemothPen` |
| 路线 | 灵能 | `ResonanceFurnace`、`PsionicWorkshop`、`MindSpire`、`ConsciousnessNetwork`、`ShieldGenerator` |

表内共 28 个唯一稳定建筑 ID；测试必须同时证明无缺失、无重复、无两个升级型建筑。

28 项必须全部具备：

- 稳定选择项；
- 原始与旋转占地；
- 内城/外城位置标签；
- 成本；
- 施工时长；
- 灰盒预览、施工框架和完成占位。

生产、物流和战斗效果不在本里程碑实现。

### 7.3 显示规则

目录状态分三类：

- 可建造：正常显示并可选择；
- 已有线索但条件不足：显示、置灰，卡片显示主原因，悬停详情列出全部原因；
- 隐藏：未发现内容、未接触路线、特殊/唯一建筑不进入普通目录和搜索结果。

搜索只检索当前可见集合，不得通过名称、稳定 ID、结果数量或锁定文案泄露隐藏内容。

卡片常驻显示主要成本；悬停详情显示名称、类别、路线、占地、位置、施工时长、完整成本、解锁条件和锁定原因。选择建筑后完整目录自动收起，快捷栏保留选择状态。

首版固定快捷栏映射为：

| 按键 | 建筑 |
|---|---|
| `1` | `MiningStation` |
| `2` | `Housing` |
| `3` | `Warehouse` |
| `4` | `Wall` |
| `5` | `ResearchStation` |
| `6` | `Smelter` |
| `7` | `Assembler` |
| `8` | `MachineGunTurret` |
| `9` | `AutomatedRepairBay` |
| `0` | `LaserTower` |

快捷栏同样先应用可见性规则。固定建筑仍处于隐藏状态时，对应槽显示为空，不显示名称、图标轮廓或锁定条件；内容变为可见后才出现。

## 8. 输入优先级

### 8.1 建造输入

进入建造选择后：

- `1–0`：选择当前快捷栏槽；
- `R`：顺时针旋转 90 度；
- 左键：在合法预览处确认；
- 右键或 `Esc`：取消当前建造选择；
- `Delete`：请求取消当前选中的施工点；
- 中键拖动与 `Home`：继续使用既有镜头语义；
- `WASD`：继续路由给当前直接控制对象。

默认连续放置。成功放置后保持建筑选择和方向；材料耗尽时保留红色预览，直到玩家切换、取消或资源恢复。

退出建造模式后，数字键不被建造系统消费，留给角色技能。

### 8.2 消费顺序

每帧输入按以下顺序处理：

1. UI 命中检测；
2. 开发面板；
3. 撤离清单；
4. 建造菜单与放置；
5. 既有城市/领袖玩法输入；
6. 镜头输入按既有暂停例外处理。

建造模式中的右键必须由建造路由消费，不能继续触发城市自动驾驶。指针位于 UI 上时，左键、右键和滚动不得穿透到世界。`WASD` 只有在现有规则允许时才产生移动或内城施工并行效果；建造系统不替城市或领袖执行移动 tick。

暂停时，目录、选择、预览、撤离清单、中键拖动和 `Home` 可用；施工、完整拆除、城市移动和其他玩法推进停止。

### 8.3 UI 键盘焦点

当搜索框、数值输入框或任何 UGUI 可编辑/键盘导航控件获得焦点时，UI 拥有当帧全部键盘事件的优先权：

- 文字输入、方向导航、提交和取消先由当前 UI 控件处理；
- `W/A/S/D/B/R/1–0/F/F10/Home/Delete/Esc/Enter` 等键不得同时进入城市、领袖、建造、部署、镜头或开发面板路由；
- 输入搜索文字中的 `W/A/S/D/B/R` 和数字不能移动对象、开关目录、旋转预览或选择快捷栏；
- UI 输入层向玩法层输出中性键盘帧，但仍按指针命中规则决定鼠标是否可进入世界；
- 当前控件明确失去焦点后，从下一帧开始恢复玩法键盘输入。

`Esc` 遵循两级消费：搜索框或其他可编辑控件有焦点时，第一次按键只执行该控件的取消/结束编辑语义并被 UI 消费；控件失焦后再次按 `Esc`，才交给目录或建造状态机。

### 8.4 建造交互状态机

```text
Inactive
  ├─ B → CatalogOpen(returnState = Inactive)
  └─ 可见快捷键 → Previewing

CatalogOpen
  ├─ B / Esc / 右键空白 → returnState
  └─ 选择已解锁卡片 → Previewing，目录收起

Previewing
  ├─ R → Previewing，方向顺时针 90°
  ├─ 左键合法位置 → ConfirmPlacement → Previewing
  ├─ 左键非法位置 → Previewing，显示原因
  ├─ B → CatalogOpen(returnState = Previewing)，保留当前选择与方向
  └─ 右键 / Esc → Inactive，清除选择与预览

ConfirmPlacement
  ├─ 原子提交成功 → 创建施工点
  └─ 重新评估或提交失败 → 不改变会话
```

`CatalogOpen` 必须记录打开来源。从 `Inactive` 打开的目录关闭后回到 `Inactive`；从 `Previewing` 打开的目录关闭后回到 `Previewing`，并保留原建筑选择与方向。因此从预览中按 `B` 打开目录后，第一次 `Esc` 只关闭目录，返回世界后再次 `Esc` 才取消建造选择。选择新卡片始终进入 `Previewing`。

搜索框有焦点时，`Esc` 先按第 8.3 节由 UGUI 消费；只有失焦后的下一次 `Esc` 才执行上述目录返回。人口、研究或前置不足的可见卡片置灰，只能悬停查看原因，不能进入 `Previewing`。已经解锁但当前材料不足的卡片仍可选择，进入红色预览但不能确认。隐藏卡片不会产生输入目标。

## 9. 合法性评估

### 9.1 单一评估结果

`BuildingPlacementEvaluation` 返回：

- `IsValid`；
- 稳定主原因；
- 有序的全部原因；
- 站点、方向和旋转后占地；
- 可选资源节点 ID；
- 可用于表现的合法格集合。

主原因按以下稳定优先级选择：

1. 引用或定义缺失；
2. 投影/表面失败；
3. 地图或平台边界；
4. 不支持当前位置；
5. 城市模式不允许；
6. 建造范围外；
7. 占地重叠；
8. 城市本体占用；
9. 地形不允许；
10. 障碍；
11. 资源节点不兼容；
12. 隐藏或未解锁；
13. 人口不足；
14. 前置建筑不足；
15. 材料不足。

详情可同时显示所有失败原因，不因已有主原因停止收集。

### 9.2 规则来源

- 占地、边界和重叠：`BuildingGrid`；
- 位置支持和模式：`BuildingMobilityRules`；
- 解锁、人口和前置：`BuildingUnlockModel` 及其多原因适配；
- 地形和障碍：现有世界地图数据；
- 城市本体：城市当前逻辑中心和固定 3×3 占地；
- 材料：`ResourceInventory.CanSpend`；
- 资源节点：地图资源稳定 ID 和建筑定义。

Collider 不能决定建筑是否合法，Renderer 颜色不能决定能否确认。

`BuildingUnlockModel` 增加向后兼容的多原因评估 API，由它统一检查人口、研究和前置建筑；旧 `IsUnlocked(..., out reason)` 保留并返回新评估的首个原因。3D 放置层不得再次复制这三项判断。

### 9.3 预览

- 合法：绿色；
- 非法：红色，并显示主原因；
- 采矿站：额外高亮当前占地内兼容资源节点；
- 非采矿建筑覆盖资源节点时，不把节点当作自动满足条件；
- 预览和高亮使用共享材质与 `MaterialPropertyBlock`。

## 10. 城市模式与施工

### 10.1 施工位置门

现有 `BuildingMobilityRules.CanConstruct` 是唯一模式门：

- `Fortress`：支持该站点的建筑可施工；
- `Mobile`：只有 `InnerCity` 且 `MobileAllowed` 的建筑可施工；
- `Deploying`、`Packing`：不能开始新的施工；
- 外城建筑、重型建筑和长周期建筑必须在稳定展开态施工。

“重型”和“长周期”不引入独立物理判断，按建筑现有 `Placement`、`Operation` 与批准的目录元数据归类。

### 10.2 会话资源

`GrayboxBuildingSession3D` 使用真正的 `ResourceInventory`、`BuildingUnlockModel` 和 `ConstructionProgress`，但仅存在于当前 3D 运行会话。开发夹具模拟正常早期开局，使用以下集中常量并由测试逐项锁定：

| 项目 | 初始值 |
|---|---:|
| 每种资源容量 | 5000 |
| 人口 | 200 |
| 铁 | 30 |
| 能晶 | 10 |
| 石料 | 30 |
| 生物质 | 20 |
| 水 | 20 |
| 合金 | 30 |
| 弹药、灵铁、飞剑、骨钢、生物质浓缩液、生物武器、共振金属、灵能增幅器、灵丹 | 0 |
| 已完成研究 | 0 项 |
| 已接触路线 | 0 条 |
| 已完成前置建筑 | 0 座 |

夹具不能使用无限资源、负成本或默认全解锁。自动化验证 28 项目录时，通过可控会话夹具依次设置“已接触路线”输入，并通过同一开发修改器显式注入资源、解锁研究和建立所需前置，不另建绕过规则的测试目录。该夹具不代表正式平衡数值。

### 10.3 确认与施工点

合法确认必须原子完成：

1. 再次评估全部规则；
2. 从 `ResourceInventory` 扣除全额材料；
3. 在对应网格占用旋转后的全部格；
4. 创建稳定施工实例 ID；
5. 创建 `ConstructionProgress`；
6. 显示半透明地基/框架、进度和 `GrayboxVisualSlot`。

任何一步失败都不得留下半扣资源、残留占地或孤立表现对象。

施工只在未暂停且 `BuildingMobilityRules.CanConstruct` 仍为真时推进。城市模式或条件暂时不满足时保留施工点并暂停进度，不销毁、不退款。完成后将同一稳定实例切换为完成灰盒表现。

### 10.4 施工状态机

```text
UnderConstruction
  ├─ 未暂停且条件满足 → Progressing
  ├─ 暂停或条件不满足 → Suspended
  └─ 取消请求 → CancelConfirmation

Progressing
  ├─ 未完成 → UnderConstruction
  └─ Remaining = 0 → Completed

Suspended
  ├─ 条件恢复 → UnderConstruction
  └─ 取消请求 → CancelConfirmation

CancelConfirmation
  ├─ 确认 → 计算退款、释放占格、移除实例
  └─ 返回 → 原施工状态
```

## 11. 取消施工与退款

选中未完成施工点后，可按 `Delete` 或点击按钮取消。进度大于零时必须二次确认；零进度也通过同一退款函数计算。

退款公式为：

```text
remainingRatio = clamp(remaining / baseDuration, 0, 1)
rawRefund = max(0, originalCost × remainingRatio × handlingRatio)
roundedRefund = Math.Round(rawRefund, MidpointRounding.AwayFromZero)
refund = clamp((int)roundedRefund, 0, originalCost)
```

舍入规则固定为普通四舍五入，恰好 `0.5` 时远离零。原成本、剩余比例和处理比例先用非负 `double` 中间值相乘，再以 `MidpointRounding.AwayFromZero` 转为整数，并按 `0..originalCost` 限幅。每种资源独立计算。

处理比例：

- 普通主动取消施工：`handlingRatio = 1.00`，即只返还尚未消耗部分；
- 撤离完整拆除未完成施工点：`handlingRatio = 0.80`；
- 撤离快速拆除未完成施工点：`handlingRatio = 0.50`；
- 遗弃：`handlingRatio = 0`。

退款成功后释放所属网格占用并移除施工实例。资源容量不足时，只接收 `ResourceInventory.Add` 能容纳的数量，超出容量的部分不进入隐藏库存。

## 12. 收起与撤离处理

### 12.1 收起拦截

当城市处于 `Fortress` 且玩家请求收起：

- 没有玩家拥有的外城建筑或施工点：继续调用既有收起流程；
- 存在外城建筑或施工点：城市保持 `Fortress`，打开撤离清单；
- 内城建筑不进入清单，随城市移动；
- 清单处理完全部玩家拥有的外城项目后，自动继续既有收起流程。

建造适配层只拦截请求和决定何时转交，不能复制 `CityDeploymentModel` 的 5 秒 Packing 状态。

### 12.2 三种处理

| 处理 | 时间 | 完成建筑返还 | 未完成施工点返还 | 结果 |
|---|---:|---:|---:|---|
| 遗弃 | 立即 | 0% | 0% | 失去所有权，留下阻挡格子的无功能遗迹 |
| 完整拆除 | 原施工时间 50% | 80% | 剩余比例后再乘 80% | 计时完成后释放格子 |
| 快速拆除 | 立即 | 50% | 剩余比例后再乘 50% | 立即释放格子 |

完成建筑的“原成本”和“原施工时间”来自 `BuildingDefinition`。未完成施工点先按第 11 节剩余比例计算。

### 12.3 清单与批处理

清单支持：

- 单个项目选择处理方式；
- 按类别批量赋值；
- 对全部项目批量赋值；
- 在批量结果后重新修改任意单项；
- 同一清单混合三种处理。

完整拆除任务按稳定施工/建筑实例 ID 排序，单队列依次推进，保证结果可复现并遵循当前单施工槽边界。快速拆除和遗弃在确认时立即处理。计时完整拆除在暂停时停止。所有即时处理和队列完成后自动再次检查清单；没有玩家拥有的外城项目时才发起 Packing。

### 12.4 遗迹

遗弃会：

- 从玩家建筑/施工集合移除所有权；
- 在原网格登记中立遗迹占用；
- 保留原旋转占地和稳定来源 ID；
- 使用独立稳定视觉 ID 和灰暗程序化占位；
- 继续阻挡后续放置；
- 不进入玩家资源、施工或撤离清单。

本阶段遗迹没有生产、物流、战斗、回收或前哨功能。完整前哨和完成建筑的常规拆除留给后续里程碑。

### 12.5 撤离状态机

```text
Normal
  └─ 收起请求且有外城资产 → ManifestOpen

ManifestOpen
  ├─ 遗弃/快速拆除 → 立即解析项目
  ├─ 完整拆除确认 → DismantlingQueue
  └─ 清单仍有未选择项目 → 保持 ManifestOpen

DismantlingQueue
  ├─ 未暂停 → 顺序推进
  ├─ 暂停 → 保持
  └─ 队列完成 → ResolveManifest

ResolveManifest
  ├─ 仍有玩家外城项目 → ManifestOpen
  └─ 已全部处理 → RequestPacking

RequestPacking
  └─ 调用既有城市收起 API → Normal
```

## 13. 开发修改器

### 13.1 可用边界

场景只序列化引用 `GrayboxDeveloperModifierBootstrap3D`。该 MonoBehaviour 在 Editor、Development 和 Release 中都必须存在、可加载且不产生 Missing Script；它不能持有仅条件编译类型的序列化字段。

Bootstrap 在所有构建中默认惰性无行为。只有以下编译边界成立时，才可在运行时创建非序列化的开发 UI、`F10` 路由和命令服务：

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
```

- Unity Editor 中始终可用；
- Development Build 中可用；
- 默认正式 `BuildWindows()` 中 Bootstrap 保持无行为，不创建开发 UI、不注册 `F10`、不构造或调用开发命令；
- 显式 2D 回归构建不接入该面板；
- 面板必须醒目显示“开发模式”；
- 所有修改在退出运行会话时丢弃。

Release 场景加载和构建日志必须同时证明：没有可用修改器、没有开发 UI/命令入口、没有 Missing Script/MonoBehaviour 警告。不能通过从场景删除 Bootstrap 来区分构建类型，也不能让 authoring 在不同构建间重写场景。

### 13.2 命令

资源：

- 当前资源 `+100`；
- 当前资源 `+1000`；
- 清零；
- 输入非负整数指定数量。

研究：

- 解锁单项；
- 解锁一条路线；
- 解锁全部；
- 不提供重新锁定。

“解锁一条路线”先把该路线标记为已接触，再通过研究模型允许的恢复/完成入口完成该路线全部研究；“解锁全部”对四条路线执行相同行为。“解锁单项”不自动接触其他路线；若该研究所属路线尚未接触，则只完成研究真值，普通目录仍按可见性规则隐藏该路线内容。

城市：

- 安全切换到 `Mobile`；
- 安全切换到 `Fortress`；
- 完成当前 Deploying/Packing 转换；
- 不直接写 Transform、Collider 或 `CityDeploymentModel` 私有字段。

施工：

- `1×`；
- `10×`；
- `100×`；
- 立即完成。

修改器只改变同一会话中正式模型可接受的输入，例如 `ResourceInventory.Set/Add`、研究解锁 API、城市安全状态 API 和施工倍率。它不能绕过建造范围、占地、节点、位置或模式合法性，也不能直接制造非法 `PlacedBuilding`。

### 13.3 Development Windows 入口

新增显式入口：

```text
FormalBuildTools.BuildWindowsGraybox3DDevelopment()
```

合同：

- 只构建 `GrayboxPrototype3D`；
- `BuildOptions.Development`；
- 输出 `Builds/Windows3DDevelopment/WasteCityGrayboxDev.exe`；
- 目标 `StandaloneWindows64`。

默认 `BuildWindows()` 继续输出不含开发修改器的 `Builds/Windows/WasteCity.exe`。现有显式 3D 和 2D 回归构建入口保持职责。

## 14. 视觉与交互表现

### 14.1 稳定视觉 ID

所有程序化对象必须使用稳定 ID：

```text
building.preview.<building-id>
building.construction.foundation.<instance-id>
building.construction.frame.<instance-id>
building.complete.<instance-id>
building.ruin.<source-instance-id>
building.grid.ground
building.grid.inner-city
building.node-highlight.<node-id>
```

表现对象只读取玩法状态，不拥有成本、进度、所有权、占用或解锁真值。

### 14.2 灰盒约定

- 预览：半透明体块；
- 施工：低矮地基加线框/框架；
- 完成：按建筑类别区分基础几何体组合；
- 遗迹：去饱和灰褐色破损体块；
- 合法：绿色；
- 非法：红色；
- 兼容资源节点：高亮描边或地面环。

只使用 Plane、Cube、Cylinder、基础材质、共享材质和 `MaterialPropertyBlock`。不导入正式图标、模型、动画或特效。

### 14.3 快捷栏

首版快捷栏为固定核心常用建筑集合。快捷栏自定义、持久化、跨存档同步和蓝图绑定均不在本里程碑。

## 15. 数据流

### 15.1 目录与选择

```text
BuildingCatalog.BuildMenu
  → 可见性/解锁查询
  → 分类、路线、搜索
  → 菜单只读项
  → 选择 BuildingDefinition
  → PlacementController 保存选择与方向
```

### 15.2 预览与确认

```text
指针
  → UI 穿透门
  → SurfaceProjector
  → Ground/InnerCity 逻辑格
  → PlacementEvaluation
  → WorldView 绿色/红色预览
  → 左键再次评估
  → ResourceInventory + BuildingGrid + ConstructionProgress 原子提交
```

### 15.3 施工

```text
未暂停时间
  + BuildingMobilityRules.CanConstruct
  + 开发施工倍率
  → ConstructionProgress.Tick
  → 进度表现
  → 完成状态
```

### 15.4 收起

```text
F 收起请求
  → EvacuationController 检查玩家外城资产
  → 无资产：交给既有城市控制器
  → 有资产：Fortress + 撤离清单
  → 解析三类处理
  → 所有权集合为空
  → 交给既有城市控制器开始 Packing
```

## 16. 错误处理

关键引用缺失时，建造输入整体禁用并显示具体引用名称；不得退回世界原点、创建空定义或静默继续。

下列失败必须有独立稳定原因：

- 相机或表面投影失败；
- 地图外；
- 内城平台外；
- 占地重叠；
- 地形不允许；
- 障碍阻挡；
- 城市本体占用；
- 资源节点缺失或不兼容；
- 材料不足；
- 研究未完成；
- 人口不足；
- 前置建筑不足；
- 城市模式不允许；
- 建造范围外；
- 当前内容隐藏；
- UI 已消费指针。

异常不能被空 `catch` 吞掉。原子提交失败必须回滚本次资源、占地和实例变更，并记录可定位的错误。

## 17. 测试与验收门

### 17.1 纯规则测试

- 四方向轮换和旋转宽高；
- 旧 `BuildingGrid` API 默认 `0°` 且现有 2D 行为不变；
- 外城切比雪夫半径 `8` 边界和 `12/24` 扩展参数；
- 内城 `8×6` 边界；
- 位置、模式、地形、障碍、城市 3×3、重叠、人口、研究、前置、材料和节点原因；
- 主原因优先级与完整原因集合；
- 28 项普通目录唯一映射，两个升级型建筑排除；
- 搜索不泄露隐藏内容；
- 主动取消、80%、50%、0% 退款，包含小数部分低于、等于和高于 `0.5` 的确定性四舍五入与限幅；
- 未完成施工先乘剩余比例；
- 遗弃占格但失去所有权；
- 单体、类别、全部与混合撤离；
- 所有项目处理后才请求 Packing。

### 17.2 EditMode 适配测试

- 外城 XY→XZ 与内城局部→城市平台映射；
- 表面自动选择与投影失败；
- Collider 结果不能绕过规则；
- 原子扣款/占格/施工点创建与失败回滚；
- 暂停和模式变化停止施工；
- 连续放置和材料耗尽保留红预览；
- 稳定 VisualSlot、共享材质和 MPB；
- 缺引用禁用输入并报告；
- 开发修改器只调用会话模型 API；
- 场景序列化的开发 Bootstrap 在所有构建中类型可用，且不含对条件编译类型的序列化字段；
- 非 Development 编译边界下 Bootstrap 惰性无行为，修改器 UI、输入和命令不可用。

### 17.3 PlayMode 场景测试

使用虚拟 Keyboard/Mouse 驱动真实 Update/FixedUpdate/LateUpdate，至少覆盖：

- `B → 选择 → 预览 → 施工 → 完成`；
- `1–0`、`R`、左键、右键、`Esc`、`Delete`；
- 从 `Inactive` 与 `Previewing` 打开目录后，`B/Esc/右键空白` 分别返回正确来源；从预览打开时保留建筑选择与方向；
- 搜索框或其他 UGUI 键盘控件获得焦点后，用虚拟键盘输入 `W/A/S/D/B/R/1–0/F/F10/Home/Delete/Esc/Enter`，断言 UI 消费文字、导航、提交和取消，城市/领袖不移动、目录不切换、预览不旋转、快捷栏不变化、部署/镜头/开发面板不响应；失焦后的下一帧才恢复玩法输入；
- 搜索框有焦点时第一次 `Esc` 只结束编辑，下一次 `Esc` 才关闭目录；若目录来自 `Previewing`，再下一次 `Esc` 才取消选择；
- 建造右键不触发自动驾驶；
- UI 点击不穿透；
- WASD 与中键/`Home` 继续按既有语义工作；
- Mobile 内城合法施工和外城拒绝；
- Fortress 外城施工；
- 采矿站节点高亮和非法节点拒绝；
- 暂停时 UI/镜头可用、施工不推进；
- 收起请求打开清单，遗弃/完整拆除/快速拆除均可完成；
- 全部处理后真实进入 Packing；
- 场景 authoring 连续两次幂等，场景内容和相关 `.meta` GUID 稳定。

测试不得直接调用玩法 tick 或相机方法伪造主循环。

### 17.4 构建与回归

必须通过：

- 完整 EditMode；
- 完整 PlayMode；
- 无界面编译；
- 默认 3D Windows 构建；
- 显式 3D Development Windows 构建；
- 显式 2D 回归 Windows 构建；
- 三个 Windows 产物的 `file` 格式检查；
- 非 Development 默认构建中开发修改器不可用，场景无开发 UI/命令入口，构建与场景加载日志无 Missing Script/MonoBehaviour 警告；
- schema、正式存档、冻结 2D 场景和测试零差异。

真实 Windows 10/11 独立运行冒烟仍需在真实 Windows 环境补验，macOS 交叉构建和文件格式检查不能替代。

### 17.5 最小产品验收流程

1. 按 `B` 展开目录；
2. 从普通 28 项中选择建筑；
3. 指向内城/外城并看到正确标签；
4. 旋转并看到占地变化；
5. 合法预览确认、扣款、施工、完成；
6. 材料不足时不能确认；
7. 收起时打开撤离清单；
8. 分别验证遗弃、完整拆除、快速拆除；
9. 全部处理后自动进入 Packing。

## 18. 性能预算

- 32×24 世界格和 8×6 内城格均不得创建逐格 GameObject；
- 外城网格、内城网格、预览、资源节点高亮和撤离高亮各使用合并网格或单一批次；
- 首版必须稳定容纳合计 128 个完成建筑、施工点和遗迹；
- 每个建筑/施工点/遗迹最多一个逻辑表现根和一个 Renderer，建造基础设施额外常驻 Renderer 不超过 8；
- 共享材质，不在帧循环中实例化材质；
- 目录、搜索和原因文本只在状态变化时重建，不在每帧使用 LINQ 或字符串拼接；
- 指针预览、输入路由、施工推进和撤离队列预热后连续 300 次显式 tick 的 managed allocation 差值为 `0 B`；
- 场景 authoring 不为目录 28 项预创建 28 套隐藏场景对象；
- 1920×1080、关闭 Deep Profile 的开发机目标维持 60 FPS；
- 性能测量记录放入 `/tmp`，不得提交 Profiler 数据、截图或构建产物。

自动测试负责对象结构、材质共享、逐格对象禁令和显式 tick 分配。FPS 与 Timeline 样本属于开发机实测，不伪装成稳定 NUnit 断言。

## 19. 回退策略

该功能通过独立程序集、组件和场景接线进入 `GrayboxPrototype3D`：

1. 移除新建造程序集引用和场景组件即可回到已验收的可操作 3D 基础；
2. 纯规则扩展保留旧签名和 `0°` 默认值，可单独回退新重载而不改 schema；
3. 默认 3D、显式 3D 和显式 2D 构建入口继续存在；
4. `FormalPrototype`、2D 控制器、正式存档和 schema 不参与迁移；
5. 开发修改器只存在于 Editor/Development 编译边界，不能污染正式发行状态；
6. 场景 authoring 必须幂等，所有新增引用可由单独组件撤销。

任何回退都不得删除现有二维纯规则、稳定 ID、存档和回归测试。

## 20. 风险与控制

| 风险 | 后果 | 控制 |
|---|---|---|
| 建造右键与自动驾驶冲突 | 取消选择时城市误移动 | 建造输入优先消费并用 PlayMode 虚拟鼠标证明 |
| 搜索框键盘穿透 | 输入文字时城市移动或预览旋转 | UI 焦点输出中性玩法键盘帧并用虚拟键盘覆盖保留键 |
| 双网格坐标混淆 | 建筑漂移、占用错误 | 值类型表面命中显式携带站点和逻辑格 |
| Collider 成为玩法真值 | 表现变化破坏规则 | Collider 只生成候选，规则层重新验证 |
| 扣款与占格非原子 | 丢资源或幽灵占地 | 单一会话提交和失败回滚测试 |
| 旋转破坏 2D | 旧占地或存档变化 | 旧 API 委托 `0°`，冻结回归零差异 |
| 撤离绕过城市状态机 | 非法 Packing | 只拦截请求，最终调用既有城市 API |
| 遗迹不再占格 | 玩家无成本清场 | 中立遗迹注册表继续参与重叠规则 |
| 开发面板进入正式版 | 发行作弊入口 | 双编译门、独立 Development 构建、非 Development 测试 |
| 条件编译组件造成 Missing Script | Release 场景加载警告或引用丢失 | 常驻可序列化 Bootstrap，条件内只创建非序列化开发服务 |
| 搜索泄露隐藏内容 | 提前暴露路线或特殊建筑 | 先构造可见集合，再搜索 |
| 每格/每卡帧分配 | UI 和预览卡顿 | 合批网格、变更时刷新、300 tick 零分配门 |
| 夹具被当作正式平衡 | 资源体验失真 | 明确开发标签、会话丢弃、数值集中 |

## 21. 明确排除项

本里程碑不包含：

- 正式存档读写或 schema 修改；
- 完整经济生产与配方执行；
- 物流观察、运输网络和阻塞可视化；
- 建筑实际生产、维修、防御和容量效果；
- 炮塔、敌人、战斗和伤害；
- 前哨所有权、补给、远程操作或回收；
- 正式撤离系统和战斗中拆除；
- 正式模型、图标、材质、动画和复杂特效；
- 快捷栏持久化或自定义；
- 蓝图、复制设置、拖动城墙和区域规划；
- 完成建筑的常规拆除；
- 完成建筑升级交互；升级型建筑只保留目录边界；
- 高度、坡度或 NavMesh 玩法；
- `BUG-0001` 的代码诊断或修复；
- 冻结 2D 场景和控制器重构。

## 22. 后续顺序

本设计获批后的开发顺序为：

1. 另行编写并审批可执行实施计划；
2. 实现 3D 建造选择、双网格放置、施工、撤离处理和开发修改器；
3. 在已完成建造基础上实现生产和物流观察；
4. 实现敌人、炮塔和防御；
5. 实现撤离、正式存档适配和完整垂直切片；
6. 垂直切片通过后再评审正式美术。

本文件不授权第 3–6 项提前实施。
