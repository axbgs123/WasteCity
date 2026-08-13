# 3D 易用性后续设计规格

> 日期：2026-08-13
> 状态：书面设计已获用户批准，生产代码尚未修改
> 受控需求：`IDEA-0007`、`IDEA-0008`、`IDEA-0009`、`IDEA-0010`
> 目标场景：`Assets/_Game/Scenes/GrayboxPrototype3D.unity`
> 精确基线：`d10d0eb797071bb71c4eb06df97a515a923f67e8`
> 工作分支：`codex/3d-usability-followup`
> 关联正式文档：`Docs/01-Game-Design-Document-ZH.md`、`Docs/05-Formal-Development-Roadmap-ZH.md`、`Docs/06-User-Feedback-and-Change-Control-ZH.md`

## 1. 目的与阶段边界

本阶段完善 3D `GrayboxPrototype3D` 的三个易用性技术包：

1. 住房规则对齐：确认 Housing 同时支持内城和外城，补齐需求级自动化验证；
2. 建造空间引导：显示真实外城可建范围，并在选择采矿站时显示兼容资源节点和完整合法性结果；
3. 系统菜单：提供 Esc 取消链、模态暂停菜单、显示设置、操作说明和退出确认。

四项需求的产品规则已经在 `Docs/06-User-Feedback-and-Change-Control-ZH.md` 登记并获用户确认；本文件把规则转换为可实施、可测试的架构设计。用户确认本文件前，不修改生产代码、场景或测试。

本阶段不包含：

- 冻结的 `FormalPrototype` 2D 功能开发；
- schema 30 变更或 3D 正式存档接入；
- 外城住房的低生命值、低人口容量等未来差异；
- 音量、震屏和其他未接入真实消费者的设置；
- 新建造规则、第二套范围真值或第二套资源兼容性真值；
- 地形源贴图、导入规则、Texture2DArray、Shader 或发布流程变更。

## 2. 已批准产品规则

### 2.1 `IDEA-0009`：住房允许放置在外城

- Housing 保留内城放置能力，同时允许放置在外城；
- Mobile 状态只允许在内城建造 Housing；
- Fortress 状态允许在内城或外城建造 Housing；
- Deploying、Packing 状态继续禁止新建造；
- 外城 Housing 当前与内城 Housing 使用相同生命值、人口容量和运行效果；
- 未来可以引入“外城生命值更低、人口容量更少”的差异，但必须另行登记、确认、设计和迁移；
- 施工、占格、资源扣除、撤离清单和城市状态限制必须经过现有统一系统，不写 Housing 特例绕过。

### 2.2 `IDEA-0008`：外城可建造区域可视化

- 进入任意建造目录或预览状态时显示外城可建范围；
- 离开建造流程时隐藏；
- 视觉采用淡色格网加清晰边界；
- 当前预览格继续用绿色/红色表达本次放置是否合法；
- 范围表现必须逐格消费 `BuildingRangeRules.IsGroundCellInRange`，不得复制半径数学；
- 只生成少量合并网格，不按格创建 GameObject；
- 表现不改变射线、占格、寻路、地形和放置结果。

### 2.3 `IDEA-0010`：采矿站合法位置高亮

- 只在 MiningStation 被选择或预览时显示；
- 只显示当前外城范围内、与 MiningStation 兼容的资源节点；
- 对每个兼容节点枚举当前朝向下所有能覆盖该节点的锚点；
- 每个锚点必须通过完整 `BuildingPlacementEvaluation`；
- 真正合法的锚点以绿色半透明占地显示；
- 有至少一个合法锚点的兼容节点显示绿色节点环；
- 兼容但当前没有任何合法锚点的节点显示暗黄色节点环；
- 范围外兼容节点不显示；非兼容节点不显示；
- UI 图例明确解释绿色与暗黄色，不把“资源兼容”误写为“可以建造”。

完整合法性包括目录解锁、城市状态、表面、范围、占格、资源节点兼容、人口、库存和当前系统已有的其他校验。因此，资源不足或城市模式不合法时，兼容节点可以全部呈暗黄色，这是预期行为。

### 2.4 `IDEA-0007`：游戏内退出与设置菜单

- Esc 遵循取消优先级，不会从建造预览直接跳到系统菜单；
- 建造确认框、撤离清单、文本输入焦点、建造预览和建造目录先消费 Esc；
- 当没有更高优先级的交互状态时，Esc 打开系统菜单；
- 系统菜单为模态层：暂停模拟并阻止建造、部署、目的地、相机移动和相机拖拽；
- 再按 Esc 或点击“继续”关闭系统菜单并恢复打开前的有效速度；
- 设置只包含真实有效的分辨率、窗口模式和操作说明；窗口模式只提供无边框全屏与窗口化；
- 设置通过独立 `PlayerPrefs` 键保存，不进入 schema 30；
- 退出必须二次确认，并明确当前 3D 灰盒进度不会保存；
- Editor 中确认退出停止 Play Mode；Player 中确认退出调用 `Application.Quit()`；
- Development 与 Release 的玩家菜单相同，Development 只允许增加诊断日志，不出现额外玩家按钮。

## 3. 推荐实施顺序

固定顺序如下：

1. `IDEA-0009` 住房规则对齐与需求级回归；
2. `IDEA-0008` 外城范围表现；
3. `IDEA-0010` 采矿站引导；
4. `IDEA-0007` 系统菜单、设置和退出；
5. 正式场景接线、完整验证、目录与生成文档回写。

原因：住房规则已经存在，先用测试固定现状；范围表现为采矿引导提供统一空间语义；系统菜单最后接管输入，避免前面两项调试期间同时改变全局输入路由。

## 4. 总体架构

### 4.1 三个技术包

```text
Building core
  BuildingMobilityRules
  BuildingRangeRules
  BuildingPlacementEvaluation
  BuildingResourceNodeCompatibilityRules (新增唯一兼容性规则)
             │
             ▼
Graybox3D.Building
  GrayboxBuildingSession3D
  GrayboxBuildingPlacementController3D
  GrayboxBuildingWorldView3D
             │
             ▼
Graybox3D.Usability (新增程序集)
  GrayboxUsabilityInputCoordinator3D
  GrayboxSystemMenuController3D
  GrayboxSystemMenuView3D
  GrayboxDisplaySettingsModel3D / adapters
```

`Graybox3D.Usability` 引用 `WasteCity.Game`、`WasteCity.Graybox3D`、`WasteCity.Graybox3D.Building`、Input System 和 UGUI。系统菜单不放入 Building 程序集，避免把全局应用生命周期反向归属到建造模块。

### 4.2 单一真值边界

| 领域 | 唯一真值 | 表现消费者 |
|---|---|---|
| 外城范围 | `BuildingRangeRules.IsGroundCellInRange` | 范围格网、范围边界、放置评估、采矿节点筛选 |
| 资源兼容性 | `BuildingResourceNodeCompatibilityRules` | 放置请求、采矿节点引导 |
| 锚点合法性 | 现有 `BuildingPlacementEvaluation` | 预览颜色、采矿合法锚点 |
| 暂停状态 | `GameSpeedModel` + `GamePauseReason.SystemMenu` | `Time.timeScale`、菜单暂停标记 |
| 设置持久化 | 独立 PlayerPrefs settings store | 系统菜单设置页 |
| 正式游戏存档 | schema 30 | 本阶段不读写 |

## 5. 技术包一：Housing 规则对齐

### 5.1 现状与结论

现有 Catalog 已把 Housing 声明为：

- `BuildingPlacement.Either`；
- `BuildingOperation.MobileAllowed`。

现有 `BuildingMobilityRules` 因而已经表达批准矩阵。此项首先只增加明确标注 `IDEA-0009` 的测试，不能为了“有实现量”重写已经正确的规则。

如果 RED 测试揭示生产行为与该合同不一致，实施必须先报告具体边界，再只修复统一规则路径；不得添加 Housing 专用控制器、直接占格或直接扣资源。

### 5.2 必测矩阵

| CityMode | InnerCity | Ground |
|---|---:|---:|
| Mobile | 合法 | 非法 |
| Deploying | 非法 | 非法 |
| Fortress | 合法 | 合法 |
| Packing | 非法 | 非法 |

测试还必须证明：

- 内外城使用同一个 `BuildingDefinition`，当前生命值和容量没有表面分叉；
- Fortress 外城 Housing 经统一请求扣资源、进入施工、完成并占据地面格；
- Mobile 内城 Housing 经同一路径可建；
- 外城 Housing 出现在现有撤离清单并通过现有撤离系统处理；
- Mobile 外城失败原因来自统一放置评估，而不是 UI 隐藏或特殊判断。

## 6. 技术包二：外城范围表现

### 6.1 复用现有表现槽

现有稳定根 `building.grid.ground` 保留，职责从“整张地图格网”收窄为“范围内淡色格网”。新增一个合并网格表现槽 `building.range.ground-boundary` 专门绘制较亮边界。

不创建逐格对象。无论范围覆盖多少格，运行时对象数量固定为：

- 一个范围内格网 MeshFilter/MeshRenderer；
- 一个范围边界 MeshFilter/MeshRenderer。

内城格网保持现状，不与外城范围网格合并。

### 6.2 网格生成

输入为：

- 世界宽高；
- 当前移动城市映射后的逻辑中心格；
- `GrayboxBuildingSession3D.GroundBuildRadius`。

算法：

1. 遍历世界格；
2. 对每格调用 `BuildingRangeRules.IsGroundCellInRange`；
3. 对范围内格写入淡色格线；
4. 检查四个相邻方向；若邻格越界或不在范围内，把该边写入边界网格；
5. 合并重复内部线，边界线不重复；
6. 只有中心格、半径或世界尺寸改变时重建 Mesh；其余帧复用；
7. 建造流程隐藏时只关闭 Renderer，不销毁 Mesh 或 GameObject。

范围判断不得改写为 Manhattan、Chebyshev、圆形距离或局部 `Mathf` 公式。即使当前规则实现简单，表现也必须通过正式规则入口得到结果。

### 6.3 显示时机与层级

- `CatalogOpen`、`Previewing`、`CancelConfirmation` 显示范围；
- `Inactive` 隐藏范围；
- 当前预览继续覆盖在范围格网上方；
- 网格与边界使用微小 Y 偏移避免地形 Z-fighting；
- 材质为清楚、可替换的透明占位材质；
- Renderer 不写入规则状态，关闭表现不会影响放置合法性。

### 6.4 接线

`GrayboxBuildingPlacementController3D` 已持有 session、城市和坐标映射能力，因此由它在显示范围前计算中心格，并把中心、半径和世界尺寸交给 `GrayboxBuildingWorldView3D`。WorldView 只构建表现，不自行推断城市规则。

## 7. 技术包二：采矿站合法位置引导

### 7.1 提取兼容性规则

当前 MiningStation 与 Iron、EnergyCrystal 的兼容判断位于放置控制器私有逻辑中。实施时将其提取为 Building core 的纯规则：

```csharp
public static class BuildingResourceNodeCompatibilityRules
{
    public static bool IsCompatible(BuildingDefinition definition, string resourceId);
}
```

放置请求和高亮扫描都调用此规则。稳定建筑 ID 和资源 ID 不改变。该类不访问 Unity API，不缓存场景对象，可由 EditMode 直接验证。

### 7.2 候选锚点枚举

对范围内每个兼容资源节点：

1. 读取当前 MiningStation 朝向后的宽高；
2. 枚举所有可能覆盖该节点的左下锚点：
   - `anchorX ∈ [nodeX-width+1, nodeX]`；
   - `anchorY ∈ [nodeY-height+1, nodeY]`；
3. 对世界外锚点仍交给正式评估返回非法，不自行复制边界规则；
4. 用坐标和朝向去重；同一锚点覆盖多个节点时只显示一次；
5. 为每个候选构建与鼠标预览相同的请求，并执行完整 `BuildingPlacementEvaluation`；
6. 只把 `IsValid` 的锚点加入绿色集合；
7. 如果某节点至少关联一个有效锚点，其节点环为绿色，否则为暗黄色。

禁止只检查“占地内有资源”就标绿，也禁止为高亮另写库存、人口、城市状态、表面、范围或占格判断。

### 7.3 表现与复用

`GrayboxBuildingWorldView3D` 增加两类池化表现：

- 资源节点环：按资源节点稳定逻辑坐标复用；
- 合法锚点框：按锚点坐标、朝向和占地复用。

每次刷新更新颜色和可见性，不销毁后重建。离开 MiningStation 选择、关闭建造流程或打开系统菜单时全部隐藏。

建议占位色：

- 绿色：RGBA 约 `(0.20, 0.90, 0.35, 0.45)`；
- 暗黄色：RGBA 约 `(0.85, 0.62, 0.12, 0.55)`。

最终实现可按现有可读性测试微调数值，但绿色/暗黄色语义不能交换。

### 7.4 刷新条件与性能

高亮不应在鼠标静止时每帧重扫整个地图。刷新键至少包含：

- 当前选择的建筑稳定 ID；
- 当前朝向和占地；
- 城市逻辑中心、模式和外城半径；
- session 的放置状态修订号；
- 当前建筑成本对应的库存值；
- 当前人口和目录解锁状态。

`GrayboxBuildingSession3D` 增加非持久化 `Revision`，在成功建造、取消/撤离完成、开发夹具或其他会改变放置结果的受控 session 操作后递增。控制器同时比较库存、人口和解锁输入，避免把可能由外部模型修改的值错误缓存。

未变化时刷新不得分配托管内存、不得重建 Mesh、不得创建或销毁 GameObject。地图扫描只发生在 MiningStation 活跃且刷新键改变时。

### 7.5 UI 说明

建造菜单在 MiningStation 被选择时显示固定短图例：

```text
绿色：当前可建造位置
暗黄色：资源兼容，但当前条件不满足
```

现有红色预览继续显示指针所在具体位置的失败原因。黄色节点不替代失败原因文本。

## 8. 技术包三：系统菜单、设置与退出

### 8.1 输入协调器

新增 `GrayboxUsabilityInputCoordinator3D` 实现现有 `IGrayboxInputInterceptor`，并成为 `GrayboxInputRouter` 的唯一输入拦截器。它组合现有 `GrayboxBuildingInputRouter3D` 和系统菜单控制器，不复制建造输入。

Esc 处理顺序固定为：

1. 已打开的系统菜单处理返回、关闭或退出确认取消；
2. 文本输入焦点/现有建造模态层；
3. 建造取消确认；
4. 撤离清单或处理状态；
5. 建造预览；
6. 建造目录；
7. 无更高状态时打开系统菜单。

撤离清单尚未开始处理时，Esc 通过撤离控制器的受控取消入口关闭清单，清空未提交分配并释放施工取消锁；完整拆除已经开始后，Esc 只由撤离流程消费，不中断或回滚已经提交的原子处理，也不打开系统菜单。

协调器每帧只调用一次现有建造输入路由。它通过调用前后的公开交互状态判断 Esc 是否已被消费，不在协调器内重写建造状态机。

系统菜单打开时返回全抑制：

- Move；
- Deployment；
- Destination；
- CameraDrag；
- Home；
- 建造输入本身。

如果 Development 面板已打开，系统菜单打开时关闭该面板且关闭后不自动恢复，避免两个模态层重叠。该动作仍走开发面板已有公开切换入口。

### 8.2 暂停模型

复用 `GameSpeedModel`，向 `GamePauseReason` 增加非序列化枚举值 `SystemMenu`。系统菜单控制器：

1. 打开前记录当前有效请求速度；
2. `SetPaused(SystemMenu, true)` 并把有效速度应用到 `Time.timeScale`；
3. 关闭时清除同一暂停原因；
4. 恢复打开前的有效请求速度，而不是无条件设为 `1`；
5. 销毁或场景卸载时清理自己拥有的暂停原因，防止 Play Mode 后续场景遗留 `timeScale=0`。

当前 3D 场景不接入旧 `GameSpeedController` 的 Space、`[`、`]` 快捷键，本阶段不会顺带开放战术变速。未来若 3D 接入战术暂停，应共享同一个 `GameSpeedModel`，不同暂停原因可叠加。

### 8.3 菜单视图

新增独立高排序层 `SystemMenuCanvas`，包含一个 `GrayboxSystemMenuView3D`。视图可沿用项目现有运行时 UGUI 生成方式，但场景中必须有稳定根和序列化控制器引用。

页面状态：

- Main：继续、设置、退出；
- Settings：分辨率、窗口模式、应用、取消、恢复默认、操作说明；
- OperationGuide：显示真实已实现的 3D 控制说明并可返回；
- ExitConfirm：明确“当前 3D 灰盒进度不会保存”，取消或确认退出。

模态背景必须阻挡 Pointer 事件。打开时将 EventSystem 焦点设到首个可操作项；切页时更新焦点；关闭时清空菜单焦点。所有按钮仍必须可由真实 Input System UI 输入触发。

### 8.4 显示设置模型

显示设置拆成可测试的纯模型与 Unity adapter：

```text
GrayboxDisplaySettingsModel3D
  ├─ IGrayboxDisplaySettingsStore
  └─ IGrayboxDisplaySettingsPlatform
```

模型支持：

- 从 store 读取上次应用值；
- 枚举 platform 提供的有效分辨率并按宽、高去重、排序；
- 暂存用户选择；
- Apply：应用到 platform，成功后写入 store；
- Cancel：放弃暂存值并恢复最后应用值；
- Restore Defaults：把暂存值设为默认值，用户仍需点击 Apply；
- 损坏、过期或当前平台不支持的保存值回退到当前平台设置，不抛异常。

默认值优先为 `1920×1080 + FullScreenWindow`；若平台不支持该分辨率，使用最接近且不超过当前显示器能力的可用值；无分辨率列表的 headless/测试环境使用当前有效值。

窗口模式只暴露：

- `Windowed`；
- `FullScreenWindow`。

明确不暴露 Exclusive Fullscreen 和 Maximized Window。

### 8.5 PlayerPrefs 合同

稳定键名：

```text
wastecity.settings.version
wastecity.display.width
wastecity.display.height
wastecity.display.window-mode
```

设置版本初始为 `1`。只有 Apply 成功后调用 `PlayerPrefs.Save()`。这些键不写进 `SaveService`、`GameSaveData` 或 schema 30，不触发游戏存档迁移。

### 8.6 退出适配器

控制器依赖可替换接口：

```csharp
public interface IGrayboxApplicationExit
{
    void Exit();
}
```

生产 adapter：

- `UNITY_EDITOR`：设置 `UnityEditor.EditorApplication.isPlaying = false`；
- Player：调用 `Application.Quit()`。

测试使用 fake 记录调用次数。打开确认框不调用退出；取消不调用退出；连续输入或重复点击最多触发一次确认退出。退出前不调用保存 API。

## 9. 场景与 Editor authoring

`GrayboxSceneAuthoring` 增加幂等的易用性合同，至少确保：

- `SystemMenuCanvas` 和菜单视图存在且排序高于建造 UI；
- `GrayboxSystemMenuController3D`、`GrayboxUsabilityInputCoordinator3D` 存在；
- coordinator 引用现有 building input router 和系统菜单；
- `GrayboxInputRouter` 的 interceptor 引用 coordinator，而不是直接引用 building input router；
- 建造 WorldView 拥有范围边界与采矿引导所需的稳定表现根；
- EventSystem 和 UI Input Module 仍唯一且有效；
- 两次 authoring 的场景内容和 GlobalObjectId 稳定。

场景接线前必须再次检查 Unity MCP 实例。如果只有其他项目实例，禁止连接；应打开或等待此独立工作树对应的 WasteCity Unity 工程，并确认锁定正确项目后再 authoring 和 PlayMode 验证。

## 10. 计划内文件边界

### 10.1 允许新增的生产文件

以下是当前设计允许的生产文件上限；实施计划可减少或合并，但不得未经报告新增计划外生产文件：

- `Assets/_Game/Scripts/Building/BuildingResourceNodeCompatibilityRules.cs`
- `Assets/_Game/Scripts/Graybox3D/Usability/WasteCity.Graybox3D.Usability.asmdef`
- `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsModel3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsAdapters3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuController3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`
- 对应 Unity `.meta` 文件。

### 10.2 预计修改的生产文件

- `Assets/_Game/Scripts/Core/GameSpeedModel.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- `Assets/_Game/Editor/WasteCity.Editor.asmdef`
- `Assets/_Game/Scenes/GrayboxPrototype3D.unity`

如果 RED 测试证明 Housing 规则不一致，才允许修改统一 Building Catalog 或 mobility rule 文件，并必须先单独报告测试证据。

### 10.3 测试、程序集与目录文件

实施计划可以新增或修改：

- Building core EditMode 兼容性与 Housing 规则测试；
- Graybox3D Building EditMode/PlayMode 范围、采矿引导与真实输入测试；
- Graybox3D Usability EditMode/PlayMode 菜单、设置和退出测试及测试 asmdef；
- 现有测试 asmdef 对新增程序集的引用；
- `Docs/Engineering/project-quality-catalog.json`；
- 设计、计划、测试结果和生成文档。

任何不在以上范围内的生产文件修改都属于真实边界问题，实施必须暂停并向用户说明原因和替代方案。

## 11. TDD 与验证设计

### 11.1 RED 顺序

每个切片先提交能够精确失败的测试，再做最小实现：

1. `IDEA-0009` Housing 模式/表面矩阵与真实放置/撤离；
2. `IDEA-0008` 范围格集合、边界边集合、显示状态和对象预算；
3. `IDEA-0010` 兼容规则、候选锚点、完整评估、颜色语义和缓存；
4. `IDEA-0007` Esc 优先级、暂停恢复、设置模型、PlayerPrefs adapter、退出 seam；
5. 场景真实输入和 authoring 幂等。

测试名或 category 必须包含需求 ID，失败消息指出功能、组件、场景和预期状态。

### 11.2 EditMode

最低覆盖：

- Housing 四状态两表面矩阵；
- Housing 统一施工和撤离路径；
- 资源兼容性正反矩阵；
- 2×2 及旋转占地的锚点枚举与去重；
- 每个绿色锚点对应正式 evaluation 的 `IsValid=true`；
- 黄色节点没有有效锚点，且能保留正式失败原因；
- 范围格和边界逐格与 `BuildingRangeRules` 对照；
- 范围更新前后对象身份不变；
- Esc 状态转移表；
- 系统菜单叠加暂停原因后正确恢复速度；
- 设置的 load/stage/apply/cancel/default/corrupt fallback；
- 退出 fake 的 0 次/1 次调用合同；
- Scene authoring 两次幂等及引用唯一性。

### 11.3 PlayMode 真实输入

不得只直接调用内部方法冒充玩家。至少使用 Input System 测试设备执行：

- B 打开建造目录，选择 Housing，Mobile 内城成功、Mobile 外城失败；
- 切换 Fortress 后外城 Housing 成功，并可从撤离清单进入现有流程；
- 建造模式显示范围，退出后隐藏；
- 选择 MiningStation 显示节点与锚点，改变库存/占格/城市状态后颜色刷新；
- Previewing 首次 Esc 只取消预览，第二次 Esc 才打开系统菜单；
- 菜单打开时 WASD、鼠标拖拽、Home、部署和建造不改变世界；
- 设置 UI 通过真实导航/点击选择并 Apply，关闭重开后值仍存在；
- 退出先出现确认，取消返回，测试替身确认只调用一次。

人工试玩结果只能记为“待用户确认”；自动化通过不能改写成“人工已验证”。

### 11.4 性能与对象预算

- 范围和采矿引导使用合并 Mesh/池化对象；
- 稳态 300 次未变化刷新在预热后托管分配为 0；
- 不按 `64×48` 地图格创建 GameObject；
- 不因鼠标移动重建范围 Mesh；
- MiningStation 未选择时不扫描资源节点；
- 显示/隐藏 20 次后对象数量和稳定身份不增长。

### 11.5 最终验证矩阵

本阶段不触发 `TerrainAssetDeep`。最终依次运行：

1. 每个需求的 focused EditMode RED/GREEN；
2. 快速日常 EditMode；
3. 完整 EditMode，但排除 `TerrainAssetDeep`；
4. 完整 PlayMode；
5. 无界面编译；
6. 默认 3D Windows 构建；
7. Development 3D Windows 构建；
8. Unity 正式场景结构、输入、画面和 Console 验证；
9. 独立代码与测试审查；
10. 文档生成与验证工具。

Unity 场景验证必须在锁定正确 WasteCity 工程实例后执行。若无法获得正确实例，代码和 CLI 测试结果可以记录，但状态必须保持“已实现待 Unity 场景验证”，不得冒充已验证。

## 12. 质量目录与可复用目录

实施完成后，`Docs/Engineering/project-quality-catalog.json` 至少更新：

- `IDEA-0007` 至 `IDEA-0010` 与相关 source/test/scene 的 requirement 关联；
- 新 Usability 程序集、系统菜单 UI、显示设置模型及测试入口；
- `BuildingResourceNodeCompatibilityRules` 登记为 Building core 可复用规则；
- 系统菜单输入协调器登记为 3D 场景级输入组件；
- PlayerPrefs 显示设置 store 登记为 schema 30 之外的应用设置边界；
- 新增生产文件、测试文件和场景组件全部进入质量目录；
- 只有通过复用评审的通用入口进入可复用目录，场景专用 View 不因“新增”自动登记为通用组件。

随后运行生成器回写 `Docs/Generated/Latest-Verification-ZH.md` 和其他受控生成物，不手工伪造通过结果。

## 13. 失败处理与回退

- 设置加载失败：回退当前显示模式，菜单仍可打开；
- `Screen.SetResolution` 无法在测试/headless 环境确认：adapter 返回可诊断结果，模型不写入成功状态；
- 范围 Mesh 构建失败：关闭该表现并记录 Development 诊断，不改变放置规则；
- 采矿高亮构建失败：关闭引导，鼠标预览和正式 placement 继续使用原评估路径；
- 系统菜单 UI 初始化失败：Esc 不得永久锁死输入或把 `Time.timeScale` 留在 0；
- 退出 adapter 异常：确认页保持可返回，不循环调用；
- 场景接线异常：Editor authoring 失败并停止，不用运行时 `FindObjectOfType` 静默连接错误对象。

Git 回退以普通 revert 或后续修复提交完成；不 force-push、不重写基线、不合并已有 PR、不创建 Release。

## 14. 验收定义

本设计完成后的产品验收标准：

1. Housing 在批准的模式/表面矩阵中经统一系统表现正确，当前内外城无数值差异；
2. 进入建造流程能看见与真实 `BuildingRangeRules` 一致的淡色外城格网和清晰边界；
3. 选择 MiningStation 时只显示范围内兼容节点，所有绿色锚点均通过完整正式评估，兼容但非法节点为暗黄色；
4. Esc 严格遵守取消链，空闲时打开模态暂停菜单；
5. 分辨率和窗口模式真实生效并通过独立 PlayerPrefs 保存，schema 30 不变；
6. 退出有明确二次确认，Editor 与 Player 使用正确出口且不伪造存档；
7. 默认 3D 场景真实输入、完整测试、构建和质量目录验证通过；
8. 人工试玩仍由用户给出最终结论。
