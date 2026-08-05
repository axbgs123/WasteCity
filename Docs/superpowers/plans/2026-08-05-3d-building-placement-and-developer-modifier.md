# WasteCity 3D Building Placement and Developer Modifier Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改写冻结 2D 运行时、不改变 schema 30、也不接入正式存档的前提下，为默认 `GrayboxPrototype3D` 实现已批准的 28 项建造目录、旋转与内外城双网格放置、施工和确定性退款、撤离处理，以及仅 Editor/Development Build 可用的开发修改器。

**Architecture:** 纯规则继续位于 `WasteCity.Game`；新的 `WasteCity.Graybox3D.Building` 程序集单向引用规则层与既有 3D 适配层。建造会话以二维整数格和正式资源/研究/施工模型为唯一真值，Unity Collider、UGUI、Renderer 和 Input System 仅作为命中、输入与表现代理。既有 `WasteCity.Graybox3D` 通过一个通用输入拦截接口接纳建造层，不反向引用建造程序集；场景始终序列化一个 Release 中惰性无行为的开发修改器 bootstrap，真正命令和 UI 只在 `UNITY_EDITOR || DEVELOPMENT_BUILD` 中创建。

**Tech Stack:** Unity 2022.3.62f1、C#、NUnit/Unity Test Framework、Input System 1.7.0、UGUI、URP、Windows Standalone x86-64、Git。

## Global Constraints

- 执行 worktree 固定为 `/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation`，分支固定为 `codex/3d-building-design`；计划执行前必须确认父提交为计划提交本身且其父链包含 `f6bbed8cf33bf183dd11504d46546d26bd9649f7`。
- 每个实现任务严格执行 RED → 核对失败原因 → 最小 GREEN → focused 回归 → 范围检查 → 精确提交。任何 RED 若包含未预期的程序集、Unity API、序列化、物理或输入错误，立即停止并报告。
- Unity 测试命令固定使用 `/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity`，`-runTests` 命令不得带 `-quit`，结果与日志写入独立 `/tmp/wastecity-3d-building/` 子目录。
- 新增 `.cs`、`.asmdef`、场景组件或目录时，必须由 Unity 生成或显式保留配套 `.meta`；禁止复制其他资源的 GUID。重新 authoring 前后必须比较所有既有 `.meta` GUID。
- 不修改 `Assets/_Game/Scenes/FormalPrototype.unity`、`PlaceholderBuildingController`、冻结 2D 控制器、正式存档读写、schema 30、`Packages/`、`ProjectSettings/GraphicsSettings.asset`、`ProjectSettings/QualitySettings.asset`、`ProjectSettings/PackageManagerSettings.asset`。
- `BuildingCatalog.BuildMenu` 是普通目录唯一来源；`HeavyMachineGunTurret` 与 `SwordRidingPlatform` 只保留升级边界，绝不进入普通菜单。
- 逻辑坐标始终为二维 `(x,y)`；外城映射到 Unity XZ，内城为随城市移动的 8×6 局部压缩网格。表现高度 Y、Collider 和 Physics 不得成为占格、合法性、施工、退款或存档真值。
- 首阶段不接完整生产、物流观察、敌人、炮塔效果、战斗、前哨、正式撤离、正式存档、正式美术、快捷栏持久化或完成建筑常规拆除/升级交互。
- 默认 Release 构建不得出现开发 UI、F10 命令入口或 Missing Script/MonoBehaviour；Editor 与 Development Build 才能创建开发命令服务。
- `IDEA-0003` 在本计划编写阶段保持 `未实现`。只有实现与完整验证事实得到调度方批准后，最后一个任务才能按当批指令回写状态；没有明确状态变更指令时保持 `未实现`。`BUG-0001` 保持 `已明确 / 已批准 / 不适用`，原始描述与复现事实不改写。
- 每个提交前运行 `git diff --check`，逐项核对暂存文件；禁止 `git add .`、通配整个目录或混入构建产物、Profiler 数据、截图、日志、`Library/`、`Logs/`、`UserSettings/`。
- 每项性能门区分自动化结构/分配测试和开发机 GUI 实测；无界面环境不能用 NUnit 分配断言替代真实 1920×1080 Profiler 证据。

---

## 1. Complete File Map and Dependency Direction

### 1.1 Pure rules in `WasteCity.Game`

| Path | Change | Single responsibility |
|---|---|---|
| `Assets/_Game/Scripts/Building/BuildingOrientation.cs` + `.meta` | Create | `BuildingOrientation`、旋转后的宽高与 footprint 枚举；旧调用默认 North/0° |
| `Assets/_Game/Scripts/Building/BuildingGrid.cs` | Modify | 为放置/恢复/查询增加方向参数和明确的边界/占用查询；所有旧签名继续委托 North |
| `Assets/_Game/Scripts/Building/BuildingRangeRules.cs` + `.meta` | Create | 外城切比雪夫半径 8/12/24 与内城 8×6 边界判断 |
| `Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs` + `.meta` | Create | 有序合法性失败原因、请求与结果值类型；不依赖 Unity |
| `Assets/_Game/Scripts/Building/BuildingUnlockModel.cs` | Modify | 新增多原因 `Evaluate`，旧 `IsUnlocked` 保持首要原因兼容 |
| `Assets/_Game/Scripts/Building/ConstructionRefundRules.cs` + `.meta` | Create | 每资源 `AwayFromZero` 退款和 `0..originalCost` 限幅 |
| `Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs` + `.meta` | Create | 遗弃、完整拆除、快速拆除、未完成施工剩余比例与稳定队列规则 |

### 1.2 Generic input seam in `WasteCity.Graybox3D`

| Path | Change | Single responsibility |
|---|---|---|
| `Assets/_Game/Scripts/Graybox3D/IGrayboxInputInterceptor.cs` + `.meta` | Create | 无建造类型依赖的同步输入消费接口和值类型抑制掩码 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs` | Modify | 每帧先调用可选拦截器，再路由未被消费的移动、部署、目的地和镜头输入 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs` | Modify | 提供只通过 `CityDeploymentModel.Restore/Tick` 并立即刷新表现的开发状态适配方法 |

`WasteCity.Graybox3D` 不引用 `WasteCity.Graybox3D.Building`。场景通过序列化 `MonoBehaviour` 并在运行时校验 `IGrayboxInputInterceptor` 完成接线。

### 1.3 New `WasteCity.Graybox3D.Building` assembly

| Path | Change | Single responsibility |
|---|---|---|
| `Assets/_Game/Scripts/Graybox3D/Building.meta` | Create | Unity folder GUID for the new adapter assembly directory |
| `Assets/_Game/Scripts/Graybox3D/Building/WasteCity.Graybox3D.Building.asmdef` + `.meta` | Create | 直接引用 `WasteCity.Game`、`WasteCity.Graybox3D`、`Unity.InputSystem`、`Unity.ugui` |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs` + `.meta` | Create | 28 项分类、路线、搜索、可见/置灰与固定快捷栏的只读投影 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs` + `.meta` | Create | 会话资源、研究、路线接触、双网格、实例、稳定 ID 与原子事务 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSurfaceProjector3D.cs` + `.meta` | Create | Camera 射线、内城 Collider 代理、地面数学平面与双网格命中 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs` + `.meta` | Create | 选择、方向、合法性重评估、预览与确认 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxConstructionController3D.cs` + `.meta` | Create | 未暂停且可施工时推进，取消确认和退款 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs` + `.meta` | Create | 单实例单 Renderer 灰盒、预览、网格、节点高亮、施工/完成/遗迹 VisualSlot |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInteractionModel3D.cs` + `.meta` | Create | Inactive/CatalogOpen/Previewing/CancelConfirm 状态和 `returnState`；在目录任务先建立，供后续放置/施工复用 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs` + `.meta` | Create | UGUI 快捷栏、目录、搜索、卡片、悬停、退款确认和撤离清单 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxUiInputGuard3D.cs` + `.meta` | Create | 键盘焦点、指针落在 UI、编辑控件 Esc 分级消费 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs` + `.meta` | Create | Packing 拦截、单体/类别/全部处理、完整拆除稳定队列、完成后恢复 Packing |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs` + `.meta` | Create | 全构建可序列化；Release 惰性；条件编译区域内创建开发 UI/服务 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifier3D.cs` + `.meta` | Create | 仅 Editor/Development 编译的资源、研究、模式和施工命令 |
| `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs` + `.meta` | Create | 建造/UI 优先输入与 `GrayboxInputSuppression` 生成 |

### 1.4 Editor, scene, tests, build and controlled facts

| Path | Change | Single responsibility |
|---|---|---|
| `Assets/_Game/Editor/WasteCity.Editor.asmdef` | Modify | 直接引用新建造程序集、`Unity.ugui` 与 `Unity.InputSystem` |
| `Assets/_Game/Editor/GrayboxSceneAuthoring.cs` | Modify | 幂等创建并接线建造系统、内城表面、Canvas/EventSystem、开发 bootstrap |
| `Assets/_Game/Editor/FormalBuildTools.cs` | Modify | 新增独立 3D Development Windows 构建入口 |
| `Assets/_Game/Editor/GrayboxPerformanceProbe.cs` | Modify | 五次 128 实例生成/清理测量并写 `/tmp` JSON |
| `Assets/_Game/Scenes/GrayboxPrototype3D.unity` | Modify | 仅增加建造适配对象和序列化引用；不改变既有移动/领袖/镜头合同 |
| `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef` | Modify | 直接引用新建造程序集、Input System、UGUI |
| `Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef` | Modify | 直接引用新建造程序集与 UGUI；保留现有直接依赖 |
| `Assets/_Game/Tests/EditMode/BuildingOrientationAndRangeTests.cs` + `.meta` | Create | 旋转、旧 API、半径和内城边界 |
| `Assets/_Game/Tests/EditMode/BuildingPlacementEvaluationTests.cs` + `.meta` | Create | 有序合法性与多重解锁原因 |
| `Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs` + `.meta` | Create | 28 项唯一映射、分类/路线/搜索/快捷栏/隐藏 |
| `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs` + `.meta` | Create | fixture、原子扣款、施工、取消和退款 |
| `Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs` + `.meta` | Create | 双网格投影、VisualSlot、共享材质和对象预算 |
| `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs` + `.meta` | Create | `returnState`、UGUI 焦点与基础输入拦截 |
| `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs` + `.meta` | Create | 三类处理、混合批次、稳定队列、Packing 恢复 |
| `Assets/_Game/Tests/EditMode/GrayboxDeveloperModifierTests.cs` + `.meta` | Create | Release 可用性函数、命令只走模型 API |
| `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs` | Modify | 正式场景建造层完整接线、无 Missing Script、无 28 套隐藏对象 |
| `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs` | Modify | Development 构建合同、128 实例结构与 300 tick 零分配 |
| `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs` + `.meta` | Create | 虚拟键鼠经真实 Update 的菜单、放置、施工、撤离、焦点和开发入口 |
| `Docs/05-Formal-Development-Roadmap-ZH.md` | Final facts only | 全量验证后记录真实数量、构建与未完成项 |
| `Docs/06-User-Feedback-and-Change-Control-ZH.md` | Final facts only | 只按调度指令更新 IDEA-0003 事实；保护 BUG-0001 |

### 1.5 Assembly references

`WasteCity.Graybox3D.Building.asmdef`:

```json
{
  "name": "WasteCity.Graybox3D.Building",
  "rootNamespace": "WasteCity.Graybox3D.Building",
  "references": [
    "WasteCity.Game",
    "WasteCity.Graybox3D",
    "Unity.InputSystem",
    "Unity.ugui"
  ],
  "autoReferenced": true
}
```

`WasteCity.EditModeTests`、`WasteCity.PlayModeTests` 和 `WasteCity.Editor` 必须直接引用 `WasteCity.Graybox3D.Building`；凡源文件直接使用 `UnityEngine.UI` 或 `EventSystems` 的程序集必须直接引用 `Unity.ugui`，不得依赖传递引用。`GrayboxSceneAuthoring` 直接使用 `InputSystemUIInputModule`，因此 `WasteCity.Editor.asmdef` 还必须直接引用 `Unity.InputSystem`，不能依赖 `WasteCity.Graybox3D.Building` 的传递引用。

---

## 2. Frozen Public Contracts

以下签名是任务间依赖合同。实现时若 Unity 2022.3 API 证明签名无法编译，必须停在对应 RED 报告，不能自行改成第二套架构。

### 2.1 Orientation, grid, range and evaluation

```csharp
namespace WasteCity.Building
{
    public enum BuildingOrientation
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class BuildingOrientationRules
    {
        public static BuildingOrientation RotateClockwise(
            BuildingOrientation value);
        public static int Width(
            BuildingDefinition definition,
            BuildingOrientation orientation);
        public static int Height(
            BuildingDefinition definition,
            BuildingOrientation orientation);
    }

    public sealed class PlacedBuilding
    {
        public BuildingOrientation Orientation { get; }
    }

    public sealed class BuildingGrid
    {
        public int Width { get; }
        public int Height { get; }
        public bool ContainsFootprint(
            BuildingDefinition definition,
            int x,
            int y,
            BuildingOrientation orientation = BuildingOrientation.North);
        public bool IsOccupied(int x, int y);
        public bool CanPlace(
            BuildingDefinition definition,
            int x,
            int y,
            BuildingSite site,
            BuildingOrientation orientation);
        public bool TryPlace(
            BuildingDefinition definition,
            int x,
            int y,
            ResourceInventory inventory,
            bool coversResource,
            out PlacedBuilding placed,
            BuildingSite site,
            BuildingOrientation orientation);
        public bool TryRestore(
            BuildingDefinition definition,
            int x,
            int y,
            out PlacedBuilding placed,
            BuildingSite site,
            BuildingOrientation orientation);
    }

    public static class BuildingRangeRules
    {
        public const int InitialGroundRadius = 8;
        public static bool IsSupportedGroundRadius(int radius);
        public static bool IsGroundCellInRange(
            int cityX,
            int cityY,
            int cellX,
            int cellY,
            int radius);
        public static bool IsInnerFootprintInBounds(
            BuildingDefinition definition,
            int x,
            int y,
            BuildingOrientation orientation);
    }

    public enum BuildingPlacementFailure
    {
        None = 0,
        MissingReference,
        ProjectionFailed,
        OutOfBounds,
        UnsupportedSite,
        InvalidCityMode,
        OutsideBuildRange,
        Overlap,
        CityOccupied,
        InvalidTerrain,
        Obstacle,
        IncompatibleResourceNode,
        ContentUnavailable,
        PopulationRequired,
        PrerequisiteBuildingRequired,
        InsufficientMaterials
    }

    public readonly struct BuildingCell
    {
        public int X { get; }
        public int Y { get; }
        public BuildingCell(int x, int y);
    }

    public readonly struct BuildingPlacementRequest
    {
        public BuildingDefinition Definition { get; }
        public BuildingGrid Grid { get; }
        public BuildingSite Site { get; }
        public BuildingOrientation Orientation { get; }
        public int X { get; }
        public int Y { get; }
        public int CityX { get; }
        public int CityY { get; }
        public int GroundRadius { get; }
        public CityMode CityMode { get; }
        public bool ProjectionSucceeded { get; }
        public bool FootprintTouchesCity { get; }
        public bool TerrainPassable { get; }
        public bool ObstacleFree { get; }
        public bool CoversCompatibleResourceNode { get; }
        public string CompatibleResourceNodeId { get; }
        public bool ContentVisible { get; }
        public BuildingUnlockEvaluation Unlock { get; }
        public bool CanAfford { get; }

        public BuildingPlacementRequest(
            BuildingDefinition definition,
            BuildingGrid grid,
            BuildingSite site,
            BuildingOrientation orientation,
            int x,
            int y,
            int cityX,
            int cityY,
            int groundRadius,
            CityMode cityMode,
            bool projectionSucceeded,
            bool footprintTouchesCity,
            bool terrainPassable,
            bool obstacleFree,
            bool coversCompatibleResourceNode,
            string compatibleResourceNodeId,
            bool contentVisible,
            BuildingUnlockEvaluation unlock,
            bool canAfford);
    }

    public readonly struct BuildingPlacementEvaluation
    {
        public bool IsValid { get; }
        public BuildingPlacementFailure PrimaryFailure { get; }
        public IReadOnlyList<BuildingPlacementFailure> Failures { get; }
        public BuildingSite Site { get; }
        public BuildingOrientation Orientation { get; }
        public int RotatedWidth { get; }
        public int RotatedHeight { get; }
        public string CompatibleResourceNodeId { get; }
        public IReadOnlyList<BuildingCell> Footprint { get; }
    }

    public static class BuildingPlacementRules
    {
        public static BuildingPlacementEvaluation Evaluate(
            in BuildingPlacementRequest request);
    }

    public enum BuildingUnlockFailure
    {
        None = 0,
        InvalidDefinition,
        Population,
        Research,
        RequiredBuilding
    }

    public readonly struct BuildingUnlockEvaluation
    {
        public bool IsUnlocked { get; }
        public BuildingUnlockFailure PrimaryFailure { get; }
        public IReadOnlyList<BuildingUnlockFailure> Failures { get; }
        public string PrimaryReason { get; }
        public IReadOnlyList<string> Reasons { get; }
    }

    public static class BuildingUnlockModel
    {
        public static BuildingUnlockEvaluation Evaluate(
            BuildingDefinition definition,
            int population,
            Func<string, bool> researchCompleted,
            Func<string, int> completedBuildings);
    }
}
```

旧 `BuildingGrid.TryPlace`、`TryRestore`、`CanPlace` 和 `PlacedBuilding` 构造函数继续存在，并委托 `BuildingOrientation.North`。旧 `BuildingUnlockModel.IsUnlocked` 继续返回 `Evaluate(...).PrimaryReason`。

Value construction rules:

- `BuildingCell(int x, int y)` assigns both coordinates without clamping.
- `BuildingPlacementRequest(...)` assigns every argument in the listed order and performs no mutation. It deliberately accepts `null` `definition`/`grid`, invalid enum values and a `null` node ID so `BuildingPlacementRules.Evaluate` can return the approved failure instead of constructor exceptions.
- `BuildingPlacementEvaluation` and `BuildingUnlockEvaluation` are created only by their public `Evaluate` entry points; later tasks do not call unlisted constructors.
- `BuildingSurfaceHit(...)` is the public fixture/controller construction entry; `Invalid` returns `isValid=false`, zero coordinates/world point, Ground site and an empty label.
- `GrayboxBuildingCatalogItem3D` is created only by `Describe`/`Query`; `BuildingEvacuationWork` is created only by `BuildingEvacuationRules.Create`; no later task invents constructors for them.

### 2.2 Generic input interception

```csharp
namespace WasteCity.Graybox3D
{
    public readonly struct GrayboxInputSuppression
    {
        public bool Move { get; }
        public bool Deployment { get; }
        public bool Destination { get; }
        public bool CameraDrag { get; }
        public bool Home { get; }

        public GrayboxInputSuppression(
            bool move,
            bool deployment,
            bool destination,
            bool cameraDrag,
            bool home);
    }

    public interface IGrayboxInputInterceptor
    {
        GrayboxInputSuppression ProcessCurrentInput();
    }
}
```

`GrayboxInputRouter` 新增：

```csharp
public void ConfigureInputInterceptor(MonoBehaviour value);
public void ProcessFrame(
    GrayboxInputFrame frame,
    GrayboxInputSuppression suppression);
```

既有精确签名 `public void ProcessFrame(GrayboxInputFrame frame)` 保持按值参数且原调用者无需修改；它委托 `ProcessFrame(frame, default(GrayboxInputSuppression))`。不得将旧签名替换成 `in` 参数。`Update()` 的顺序固定为：

```text
interceptor.ProcessCurrentInput()
→ ReadCurrentFrame()
→ ProcessFrame(frame, suppression)
→ TickGameplay(Time.deltaTime)
```

建造路由不调用城市、领袖、镜头或施工的 gameplay tick；它只消费本帧事件、更新建造状态并返回抑制掩码。

### 2.3 Catalog and interaction

```csharp
namespace WasteCity.Graybox3D.Building
{
    public enum BuildingMenuCategory
    {
        Basic,
        Production,
        Logistics,
        Defense,
        Route
    }

    public enum BuildingCatalogVisibility
    {
        Hidden,
        Locked,
        Buildable
    }

    public readonly struct GrayboxBuildingCatalogItem3D
    {
        public BuildingDefinition Definition { get; }
        public BuildingMenuCategory Category { get; }
        public ContentRoute Route { get; }
        public BuildingCatalogVisibility Visibility { get; }
        public string PrimaryLockReason { get; }
        public IReadOnlyList<string> LockReasons { get; }
    }

    public interface IGrayboxBuildingCatalogContext3D
    {
        int Population { get; }
        bool IsResearchCompleted(string id);
        int CompletedBuildingCount(string id);
        bool HasContactedRoute(ContentRoute route);
    }

    public sealed class GrayboxBuildingCatalogPresenter3D
    {
        public const int BuildMenuCount = 28;
        public static IReadOnlyList<BuildingDefinition> Quickbar { get; }
        public static BuildingMenuCategory CategoryOf(
            BuildingDefinition definition);
        public static ContentRoute RouteOf(
            BuildingDefinition definition);
        public IReadOnlyList<GrayboxBuildingCatalogItem3D> Query(
            IGrayboxBuildingCatalogContext3D context,
            BuildingMenuCategory? category,
            ContentRoute? route,
            string visibleSearchText);
        public GrayboxBuildingCatalogItem3D Describe(
            IGrayboxBuildingCatalogContext3D context,
            BuildingDefinition definition);
    }

    public enum GrayboxBuildingInteractionState
    {
        Inactive,
        CatalogOpen,
        Previewing,
        CancelConfirmation
    }

    public sealed class GrayboxBuildingInteractionModel3D : MonoBehaviour
    {
        public GrayboxBuildingInteractionState State { get; }
        public GrayboxBuildingInteractionState CatalogReturnState { get; }
        public BuildingDefinition Selected { get; }
        public BuildingOrientation Orientation { get; }
        public void ToggleCatalog();
        public void CloseCatalog();
        public void Select(BuildingDefinition definition);
        public void RotateClockwise();
        public void RequestCancelConstruction();
        public void ResolveCancelConfirmation(bool confirmed);
        public void CancelPreview();
    }
}
```

`CatalogOpen` 从 `Inactive` 打开时关闭回 `Inactive`；从 `Previewing` 打开时关闭回 `Previewing`，保留选择和方向。搜索框有焦点时第一次 Esc 只执行 UGUI 编辑语义；失焦后下一次 Esc 才关闭目录；回到 Previewing 后再一次 Esc 才取消选择。

The catalog classification is fixed and must be encoded by stable definition identity:

| Category | Route filter | Definitions |
|---|---|---|
| Basic | none | `Housing`, `Wall`, `ResearchStation` |
| Production | none | `MiningStation`, `Smelter`, `Assembler` |
| Logistics | none | `Warehouse`, `AutomatedRepairBay` |
| Defense | none | `MachineGunTurret`, `LaserTower` |
| Route | Technology | `PowerPlant` |
| Route | Cultivation | `SpiritFireFurnace`, `ArtifactWorkshop`, `SwordArrayTower`, `SpiritGatheringArray`, `AlchemyChamber`, `PuppetWorkshop` |
| Route | BiologicalAscension | `ColonyPool`, `BreedingChamber`, `SporeTower`, `MetabolicFurnace`, `AcidTower`, `BehemothPen` |
| Route | Psionics | `ResonanceFurnace`, `PsionicWorkshop`, `MindSpire`, `ConsciousnessNetwork`, `ShieldGenerator` |

The quickbar order is fixed:

| Key | Definition |
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

### 2.4 Session, placement, construction and presentation

```csharp
namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxBuildingInstanceState
    {
        UnderConstruction,
        Completed,
        AbandonedRuin
    }

    public sealed class GrayboxBuildingInstance3D
    {
        public string StableInstanceId { get; }
        public PlacedBuilding Placement { get; }
        public ConstructionProgress Progress { get; }
        public GrayboxBuildingInstanceState State { get; }
        public bool IsPlayerOwned { get; }
        public bool IsEvacuationLocked { get; }
    }

    public interface IGrayboxBuildingPresentation3D
    {
        bool TryCreate(GrayboxBuildingInstance3D instance);
        void UpdateInstance(GrayboxBuildingInstance3D instance);
        void Remove(GrayboxBuildingInstance3D instance);
    }

    public sealed class GrayboxBuildingSession3D :
        MonoBehaviour,
        IGrayboxBuildingCatalogContext3D
    {
        public const int ResourceCapacity = 5000;
        public bool DevelopmentFixtureEnabled { get; }
        public ResourceInventory Inventory { get; }
        public ResearchModel Research { get; }
        public BuildingGrid GroundGrid { get; }
        public BuildingGrid InnerGrid { get; }
        public int Population { get; }
        public int GroundBuildRadius { get; }
        public float ConstructionMultiplier { get; }
        public uint CatalogRevision { get; }
        public IReadOnlyList<GrayboxBuildingInstance3D> Instances { get; }
        public int CompletedBuildingCount(string id);

        public void Configure(bool developmentFixtureEnabled);
        public void ConfigureDevelopmentFixture();
        public bool TryBeginConstruction(
            in BuildingPlacementRequest request,
            IGrayboxBuildingPresentation3D presentation,
            out GrayboxBuildingInstance3D instance,
            out BuildingPlacementEvaluation evaluation);
        public bool TryCancelConstruction(
            string stableInstanceId,
            double handlingRatio,
            IGrayboxBuildingPresentation3D presentation,
            out int acceptedRefund);
        public void TickConstruction(
            float unscaledDeltaTime,
            CityMode mode,
            bool paused,
            IGrayboxBuildingPresentation3D presentation);
        public bool HasPlayerOwnedGroundInstances { get; }
        public void CopyPlayerOwnedGroundInstances(
            List<GrayboxBuildingInstance3D> destination);
        public bool TryLockEvacuationWork(
            IReadOnlyList<BuildingEvacuationWork> fullDismantleWork,
            out string failureReason);
        public void RollbackEvacuationLocksAfterFailure(
            IReadOnlyList<BuildingEvacuationWork> fullDismantleWork);
        public bool TryCommitEvacuation(
            in BuildingEvacuationWork work,
            IGrayboxBuildingPresentation3D presentation,
            out int acceptedRefund,
            out string failureReason);
        public void SetRouteContact(ContentRoute route, bool contacted);
        public void UnlockResearchForDevelopment(string researchId);
        public void UnlockRouteForDevelopment(ContentRoute route);
        public void UnlockAllResearchForDevelopment();
        public void SetConstructionMultiplierForDevelopment(float value);
        public void CompleteAllConstructionForDevelopment(
            IGrayboxBuildingPresentation3D presentation);
    }

    public readonly struct BuildingSurfaceHit
    {
        public bool IsValid { get; }
        public BuildingSite Site { get; }
        public int X { get; }
        public int Y { get; }
        public Vector3 WorldPoint { get; }
        public string SurfaceLabel { get; }

        public static BuildingSurfaceHit Invalid { get; }
        public BuildingSurfaceHit(
            bool isValid,
            BuildingSite site,
            int x,
            int y,
            Vector3 worldPoint,
            string surfaceLabel);
    }

    public sealed class GrayboxBuildingSurfaceProjector3D : MonoBehaviour
    {
        public void Configure(
            Camera controlledCamera,
            GrayboxWorldView3D worldView,
            GrayboxMobileCityController3D city,
            Collider innerCitySurface);
        public bool TryProject(
            Vector2 screenPosition,
            out BuildingSurfaceHit hit);
    }

    public sealed class GrayboxBuildingPlacementController3D :
        MonoBehaviour
    {
        public BuildingPlacementEvaluation CurrentEvaluation { get; }
        public BuildingSurfaceHit CurrentHit { get; }
        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingSurfaceProjector3D projector,
            GrayboxBuildingWorldView3D presentation,
            GrayboxBuildingInteractionModel3D interaction);
        public static string CreateResourceNodeVisualId(
            int worldX,
            int worldY);
        public void UpdatePointer(Vector2 screenPosition);
        public bool ConfirmCurrentPlacement(
            out GrayboxBuildingInstance3D instance);
        public void HidePreview();
    }

    public sealed class GrayboxConstructionController3D : MonoBehaviour
    {
        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation,
            GrayboxBuildingInteractionModel3D interaction,
            Camera controlledCamera,
            GrayboxBuildingMenuView3D menu);
        public bool SelectAt(Vector2 screenPosition);
        public bool SelectInstance(string stableInstanceId);
        public ConstructionCancelResult RequestCancelSelected();
        public bool ResolveCancelSelected(bool confirmed);
        public void TickConstruction(float unscaledDeltaTime);
    }

    public enum ConstructionCancelResult
    {
        NotFound,
        Cancelled,
        ConfirmationRequired
    }

    public sealed class GrayboxBuildingWorldView3D :
        MonoBehaviour,
        IGrayboxBuildingPresentation3D
    {
        public int InfrastructureRendererCount { get; }
        public int InstanceRendererCount { get; }
        public void Configure(
            Transform instanceRoot,
            Transform infrastructureRoot,
            Material sharedMaterial,
            GrayboxMobileCityController3D city);
        public bool TryCreate(GrayboxBuildingInstance3D instance);
        public void UpdateInstance(GrayboxBuildingInstance3D instance);
        public void Remove(GrayboxBuildingInstance3D instance);
        public void ShowPreview(
            BuildingDefinition definition,
            in BuildingSurfaceHit hit,
            BuildingOrientation orientation,
            in BuildingPlacementEvaluation evaluation);
        public void HidePreview();
        public void ShowCompatibleResourceNode(
            string stableNodeVisualId,
            int worldX,
            int worldY,
            bool visible);
        public bool TryPickInstance(
            Ray ray,
            out string stableInstanceId);
    }
}
```

`ConfigureDevelopmentFixture()` 固定创建容量 5000、人口 200，并设置 Iron 30、EnergyCrystal 10、Stone 30、Biomass 20、Water 20、Alloy 30，其余正式资源为 0；研究、路线接触、前置建筑均为 0。它不提供无限资源或默认全解锁。

`CatalogRevision` is the frozen public `uint` catalog-projection revision. It advances with unchecked arithmetic and never resets during the lifetime of a session component. It increments exactly once after a fixture/model rebuild, an actual route-contact set change, each newly completed research ID, and each successfully committed building completion (both a normal `TickConstruction` completion and each individual successful `CompleteAllConstructionForDevelopment` completion). The completion increment occurs only after its presentation update has succeeded. No-op calls, partial construction progress, failed presentation updates that roll construction back, and any other rolled-back mutation do not advance it. `UnlockRouteForDevelopment` and `UnlockAllResearchForDevelopment` therefore use the same actual-change rules: each route/contact or research ID that was already present is a no-op, while each newly added research ID advances the revision once.

原子放置顺序固定为：重新评估 → 正式库存 `TrySpend` → 对应 `BuildingGrid.TryRestore` 占格 → 创建稳定实例 → `presentation.TryCreate`。任何一步返回失败都按相反顺序释放占格并返还本次实际扣款；`TryCreate` 抛出异常时先回滚再重新抛出，不吞异常。只有全部成功才递增稳定实例序号并对外返回实例。

施工实例 Collider 只作为 `TryPickInstance` 的选择代理。`RequestCancelSelected` 对零进度实例立即调用同一退款函数并返回 `Cancelled`；已有进度时进入 `CancelConfirmation` 并返回 `ConfirmationRequired`，只有 `ResolveCancelSelected(true)` 才执行退款和移除。

Evacuation session rules:

- `CompletedBuildingCount(id)` returns zero for a null, empty or unknown stable definition ID. Otherwise it counts both Ground and InnerCity instances only when the definition ID matches, `IsPlayerOwned == true`, `State == Completed`, and `IsEvacuationLocked == false`. `UnderConstruction`, `AbandonedRuin`, every locked full-dismantle item, and an item currently being dismantled never satisfy a prerequisite.
- Task 7 owns the future extension of this revision contract: every successfully committed owner, state, evacuation-lock, or evacuation-commit mutation that changes `CompletedBuildingCount` increments `CatalogRevision` once; validation failures, presentation failures, and rollbacks do not. Task 4 does not add those Task 7 mutation paths.
- `CopyPlayerOwnedGroundInstances` requires a non-null caller-owned list, clears it, then appends only player-owned Ground instances in ordinal stable-instance-ID order; it never exposes the mutable backing list.
- `HasPlayerOwnedGroundInstances` performs the same ownership/site predicate without allocation.
- `TryLockEvacuationWork` accepts only non-null, non-empty, duplicate-free `FullDismantle` work for currently owned Ground instances. It first validates every item and calculates no new refund or duration, then copies the exact immutable structs into a private stable-ID lock table and marks all matching instances locked. A validation failure returns false with no locks; an exception rolls back every lock created by that call before rethrowing. Thus confirmation can never leave a partial lock set.
- `RollbackEvacuationLocksAfterFailure` accepts only the controller's current suffix/list of still-locked exact work snapshots and exists solely for confirmation/commit failure cleanup before processing can continue. The controller removes a work item from that rollback buffer only after a successful commit. The method is not exposed through UI and does not create a player cancellation path after a manifest has been confirmed.
- `TryCommitEvacuation(in work, ...)` rejects an empty/unknown ID, InnerCity or non-owned instance, `Unassigned`, and a presentation mismatch with a non-empty failure reason and no mutation. For `FullDismantle`, it additionally requires the instance to be locked and every field of `work` to equal the private snapshot captured by `TryLockEvacuationWork`; it consumes that snapshot on success and never calls `BuildingEvacuationRules.Create` again.
- Abandon changes ownership/state to `AbandonedRuin`, preserves the original `PlacedBuilding` and occupied cells, gives zero refund, then updates the same presentation.
- QuickDismantle commits its freshly created work immediately. A locked FullDismantle commits only after its stable queue timer reaches zero. Both use the work snapshot's refund and duration, remove the exact grid placement, credit only the amount accepted by `ResourceInventory.Add`, remove the session instance, and remove its presentation.
- `TickConstruction` skips every `IsEvacuationLocked` instance before calling `ConstructionProgress.Tick`, including full-dismantle work waiting later in the queue. Pausing stops the evacuation timer; it cannot allow locked construction to progress or complete.
- A mutation/presentation exception rolls back ownership/state or list position, grid occupancy, accepted refund, and the current lock snapshot before rethrowing. If presentation recreation during rollback fails, throw an `InvalidOperationException` containing both failures. The controller catches confirmation/commit failure, calls `RollbackEvacuationLocksAfterFailure` for every remaining queued work item, clears its queue, and returns to the open manifest without leaving a lock. A normal validation failure never partially mutates.
- The evacuation controller may call `TryCommitEvacuation(in fullWork, ...)` only after that exact stable queue item's timer reaches zero. Session methods do not own or advance the timer.
- The controller allocates its manifest/work buffers once during `Configure` and reuses them with `CopyPlayerOwnedGroundInstances`; normal `Tick` performs no LINQ, list creation or category-map construction.

Ground evaluation details are fixed:

- derive the city logical center with existing public Unity state `PlanarCoordinateMapper3D.TryWorldToCell(city.transform.position, out cityX, out cityY)`; the controller's Rigidbody is private and must not be accessed by reflection; conversion failure invalidates ground placement instead of clamping;
- the city occupies the 3×3 cells whose offsets from that center are each `-1..1`;
- `DeepWater` and `Cliff` fail terrain, while `Ruins` fails the independent obstacle reason;
- only `MiningStation` requires a resource node in this scope. It is compatible only when its footprint contains a `WorldCell.HasResource` node whose `ResourceId` is the resource type `ResourceIds.Iron` or `ResourceIds.EnergyCrystal`; `Stone`, `Biomass`, `Water`, and every other `HasResource` type are incompatible. `ResourceId` is used only for this type-compatibility check and is never node identity; a building that does not require a node neither depends on nor exposes a compatible node ID;
- `CreateResourceNodeVisualId(x,y)` returns the unique deterministic adapter ID `world.resource-node.<x>.<y>`, the only stable resource-node identity in this adapter; `BuildingPlacementRequest.CompatibleResourceNodeId`, `BuildingPlacementEvaluation.CompatibleResourceNodeId` and `building.node-highlight.<node-id>` use that coordinate ID without modifying `WorldMapModel`;
- stable session instance IDs use `building.instance.000001` ordinal formatting, are never persisted, and advance only after a complete atomic commit.

### 2.5 Refund and evacuation

```csharp
namespace WasteCity.Building
{
    public enum BuildingEvacuationTreatment
    {
        Unassigned,
        Abandon,
        FullDismantle,
        QuickDismantle
    }

    public static class ConstructionRefundRules
    {
        public static int Calculate(
            int originalCost,
            double remainingRatio,
            double handlingRatio);
    }

    public readonly struct BuildingEvacuationWork
    {
        public string StableInstanceId { get; }
        public BuildingEvacuationTreatment Treatment { get; }
        public double RemainingRatio { get; }
        public float DismantleSeconds { get; }
        public int Refund { get; }
    }

    public static class BuildingEvacuationRules
    {
        public static BuildingEvacuationWork Create(
            string stableInstanceId,
            int originalCost,
            float originalBuildSeconds,
            double remainingRatio,
            BuildingEvacuationTreatment treatment);
        public static IReadOnlyList<BuildingEvacuationWork>
            CreateStableFullDismantleQueue(
                IEnumerable<BuildingEvacuationWork> work);
    }
}
```

`ConstructionRefundRules.Calculate` 使用非负 `double` 中间值，`Math.Round(raw, MidpointRounding.AwayFromZero)`，最后 clamp 到 `0..originalCost`。遗弃处理比例 0；完整拆除比例 0.8、耗时为原施工时长 0.5；快速拆除比例 0.5 且立即完成。未完成施工先乘 `Remaining/BaseDuration`，每种资源独立计算。

```csharp
namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxEvacuationController3D : MonoBehaviour
    {
        public bool IsManifestOpen { get; }
        public bool IsProcessing { get; }
        public IReadOnlyList<BuildingEvacuationWork> Work { get; }
        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation,
            GrayboxBuildingMenuView3D menu);
        public bool TryHandleDeploymentRequest();
        public bool Assign(
            string stableInstanceId,
            BuildingEvacuationTreatment treatment);
        public int AssignCategory(
            BuildingMenuCategory category,
            BuildingEvacuationTreatment treatment);
        public int AssignAll(BuildingEvacuationTreatment treatment);
        public bool ConfirmManifest();
        public void Tick(float unscaledDeltaTime, bool paused);
    }
}
```

只有 `Fortress` 收起且存在玩家拥有的外城完成建筑或施工点时打开清单。遗弃立即变为无功能、非玩家所有但仍占格的遗迹；快速拆除立即移除；完整拆除按稳定实例 ID 排序逐个推进。所有玩家拥有的外城实例处理完成后，仅调用既有精确 API `city.TryToggleDeployment(out _)` 继续 Packing。内城实例不进入清单并随城市移动。

`ConfirmManifest()` 先从当前实例状态为每个已分配条目创建一次不可变 `BuildingEvacuationWork`；其中所有 `FullDismantle` work 按稳定实例 ID 排序，并在任何遗弃/快速提交或计时开始前用一次 `TryLockEvacuationWork` 原子锁定。锁定中途失败时确认返回 false、清空临时 work 且零实例留锁。锁成功后 Abandon/Quick 仍在确认阶段立即提交，Full work 使用原快照逐项计时和提交；队列后项从确认成功起也已锁定。异常或提交失败会进入唯一的失败回滚路径，释放当前及所有尚未处理 work 的锁并回到清单，不增加“取消已确认撤离”操作。

### 2.6 UGUI, input and developer modifier

```csharp
namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxUiInputGuard3D
    {
        public bool HasKeyboardFocus(EventSystem eventSystem);
        public bool IsPointerOverUi(
            EventSystem eventSystem,
            Vector2 screenPosition);
        public bool ConsumeFocusedEscape(EventSystem eventSystem);
    }

    public sealed class GrayboxBuildingMenuView3D : MonoBehaviour
    {
        public bool CatalogVisible { get; }
        public bool EvacuationVisible { get; }
        public string SearchText { get; }
        public event Action CancelSelectedConstructionRequested;
        public event Action<bool>
            CancelConstructionConfirmationResolved;
        public event Action<
            string,
            BuildingEvacuationTreatment>
            EvacuationItemTreatmentRequested;
        public event Action<
            BuildingMenuCategory,
            BuildingEvacuationTreatment>
            EvacuationCategoryTreatmentRequested;
        public event Action<BuildingEvacuationTreatment>
            EvacuationAllTreatmentRequested;
        public event Action EvacuationConfirmationRequested;
        public void Configure(
            Canvas canvas,
            EventSystem eventSystem,
            GrayboxBuildingSession3D session,
            GrayboxBuildingInteractionModel3D interaction);
        public void RefreshCatalog();
        public void SetCategory(BuildingMenuCategory category);
        public void SetRouteFilter(ContentRoute? route);
        public void SetSearchText(string value);
        public bool TrySelectQuickbarSlot(int zeroBasedIndex);
        public bool TrySelectCatalogItem(string stableBuildingId);
        public bool HasKeyboardFocus();
        public bool IsPointerOverUi(Vector2 screenPosition);
        public bool ConsumeFocusedEscape();
        public void ShowEvacuation(
            IReadOnlyList<GrayboxBuildingInstance3D> instances);
        public void HideEvacuation();
    }

    public sealed class GrayboxBuildingInputRouter3D :
        MonoBehaviour,
        IGrayboxInputInterceptor
    {
        public void Configure(
            GrayboxBuildingMenuView3D menu,
            GrayboxBuildingInteractionModel3D interaction,
            GrayboxBuildingPlacementController3D placement,
            GrayboxConstructionController3D construction,
            GrayboxEvacuationController3D evacuation,
            GrayboxDeveloperModifierBootstrap3D developer);
        public GrayboxInputSuppression ProcessCurrentInput();
    }

    public sealed class GrayboxDeveloperModifierBootstrap3D :
        MonoBehaviour
    {
        public bool IsRuntimeAvailable { get; }
        public bool IsPanelOpen { get; }
        public static bool ResolveRuntimeAvailability(
            bool isEditor,
            bool isDevelopmentBuild);
        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation,
            Canvas canvas);
        public bool TryTogglePanel();
    }

    public enum DevelopmentConstructionSpeed
    {
        Normal = 1,
        Fast10 = 10,
        Fast100 = 100
    }
}
```

UI pointer classification is intentionally limited to UGUI graphics. `GrayboxUiInputGuard3D.IsPointerOverUi` calls `EventSystem.RaycastAll`, but counts a returned `RaycastResult` only when `result.module` is a `GraphicRaycaster` that is active and enabled and whose owning `Canvas` is active and enabled. Results from `PhysicsRaycaster`, `Physics2DRaycaster`, or any other world raycaster—including results against world Colliders—never establish UI capture. If no qualifying EventSystem result exists, retain the `GraphicRegistry` fallback over active, enabled `GraphicRaycaster`/Canvas pairs and raycastable active graphics; that fallback may establish UI capture but must not inspect physics results.

`GrayboxDeveloperModifier3D.cs` 中仅在条件编译区域定义：

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperModifier3D
    {
        public GrayboxDeveloperModifier3D(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation);
        public bool AddResource(string resourceId, int amount);
        public bool SetResource(string resourceId, int amount);
        public bool ClearResource(string resourceId);
        public bool UnlockResearch(string researchId);
        public bool UnlockRoute(ContentRoute route);
        public void UnlockAllResearch();
        public bool SetCityMode(CityMode mode);
        public bool CompleteCityTransition();
        public bool SetConstructionSpeed(
            DevelopmentConstructionSpeed speed);
        public void CompleteAllConstruction();
    }
}
#endif
```

`GrayboxDeveloperModifierBootstrap3D` 的类型、序列化字段和 `ResolveRuntimeAvailability` 在所有构建存在。它在任何构建的 `Awake`、`Update` 或其他生命周期方法中都不读取 `Keyboard.current`/F10；它只提供 `TryTogglePanel` 和受条件编译保护的服务创建。Release 的 `Awake`、`TryTogglePanel` 不创建 UI、不创建命令服务，并返回不可用。唯一 F10 读取者是 `GrayboxBuildingInputRouter3D.ProcessCurrentInput`，且读取发生在 UGUI 键盘焦点与模态输入处理之后。条件编译类型绝不出现在场景序列化字段、Prefab 或 ScriptableObject 中。

Menu and callback ownership:

- `GrayboxBuildingMenuView3D.Awake` creates exactly one non-serialized presenter and uses its serialized session for every selection check. `TrySelectQuickbarSlot` validates `0..9`, resolves `Quickbar[index]`, then requires `Describe(...).Visibility == Buildable`. `TrySelectCatalogItem` searches only the current `Query(session, category, route, searchText)` result and requires `Buildable`. Hidden, locked, filtered-out and unknown IDs return false; success calls `interaction.Select(definition)`. Input code calls these menu APIs and never reads the 28-item mapping.
- The menu owns a cached last-seen `uint` revision. An explicit `RefreshCatalog()` rebuilds the quickbar/catalog projection and synchronizes that cache to `session.CatalogRevision` only after the refresh succeeds. In `Update`, it calls `RefreshCatalog()` only when the configured session revision differs from the cached revision, then performs the existing catalog visibility and placement-status synchronization. An unchanged revision must not rebuild cards, quickbar slots, or their GameObjects/Transforms.
- Every Build Details projection shows `最低人口：<definition.MinimumPopulation>` whenever `MinimumPopulation > 0`, whether or not the current population already satisfies it. It also retains the applicable research and prerequisite-building requirements plus the primary/all current lock reasons; satisfying population does not hide its numeric requirement.
- `CategoryOf`/`RouteOf` are the single stable mapping used by catalog queries, cards, quickbar checks and evacuation category batches. A definition outside `BuildingCatalog.BuildMenu` throws `ArgumentException`; the input, UI and evacuation controller contain no duplicate mapping tables.
- UGUI button listeners only raise the listed menu events. `GrayboxConstructionController3D.Configure` subscribes to the two cancel events; `GrayboxEvacuationController3D.Configure` subscribes to the four evacuation events. Each controller unsubscribes in `OnDestroy`. The menu never calls session mutation or deployment APIs directly, eliminating controller/UI circular dependencies.
- `Normal/Fast10/Fast100` set the persistent current-session multiplier to 1/10/100. The “立即完成” button calls `CompleteAllConstruction()` → `session.CompleteAllConstructionForDevelopment(presentation)` in the same frame and leaves the multiplier unchanged for later construction.
- `GrayboxDeveloperModifier3D(session, city, presentation)` throws `ArgumentNullException` for any missing dependency; the always-compiled bootstrap constructs it only inside the approved compile guard and never serializes it.

Runtime serialization rules:

- `GrayboxBuildingInteractionModel3D` is a serialized MonoBehaviour and is shared by reference; authoring never injects a transient plain C# state object.
- `GrayboxBuildingSession3D.Configure(true)` serializes `developmentFixtureEnabled`; `Awake` creates the finite session models by calling `ConfigureDevelopmentFixture`. Resource/model objects themselves are not assumed to survive scene YAML serialization.
- `GrayboxBuildingMenuView3D.Awake` creates its non-serialized `GrayboxBuildingCatalogPresenter3D`; authoring serializes only Canvas, EventSystem, session and interaction references.
- Construction and evacuation controllers own their normal `Update` loops and call their public tick methods with `Time.deltaTime`; the building input router never drives gameplay ticks.

---

## 3. Specification Coverage Self-Review

| Approved contract | Implementing tasks | Verification gate |
|---|---|---|
| 独立 3D 建造适配层，冻结 2D | 1, 9, 10 | asmdef 依赖方向；`FormalPrototype`、`PlaceholderBuildingController` 零差异 |
| 28 项唯一普通目录，排除两个升级目标 | 3, 11 | EditMode 全集合相等/唯一；PlayMode 真实目录遍历 |
| 五类、四路线、可见搜索、置灰原因 | 2, 3, 6, 11 | 多原因规则、隐藏不泄露、真实 UGUI 搜索 |
| 固定十槽快捷栏 | 3, 6, 9, 11 | 精确顺序、隐藏空槽、数字键优先级 |
| 旋转兼容旧 API | 1, 2, 5, 11 | 四方向 footprint；旧 API North；真实 R 输入 |
| 外城 8/12/24 与内城 8×6 | 2, 5, 10, 11 | 边界/坐标单测；场景平台；真实鼠标自动选面 |
| 有序合法性和节点高亮 | 2, 5, 11 | 每个失败原因独立；预览重评估；采矿站仅 Iron/EnergyCrystal、坐标节点 ID |
| 正式有限资源、研究、前置、人口 | 3, 4, 7, 8, 11 | fixture 精确值；Completed-only 前置计数；同一模型命令；耗尽红预览 |
| CatalogRevision 驱动目录投影 | 4, 6 | 实际变化单调版本；研究/路线/前置完成自动刷新；未变化 UI 身份稳定 |
| 原子扣款、占格、施工和连续放置 | 4, 5, 11 | 回滚故障注入；真实施工完成；连续选择保留 |
| AwayFromZero 退款 | 4, 7 | 小于/等于/大于 0.5；每资源限幅 |
| 遗弃/完整/快速与混合撤离 | 7, 9, 11 | 原子快照锁、施工跳过、稳定队列、单体/类别/全部真实流程 |
| 全部处理后恢复 Packing | 7, 9, 11 | 只调用既有城市切换；真实控制目标回 City |
| Catalog `returnState` | 3, 6, 9, 11 | 两种来源、选择/方向保留、多级 Esc |
| UGUI 键盘焦点不穿透 | 6, 9, 10, 11 | UI action 重载合同；F10 单一读取者；真实 EventSystem/headless 虚拟全键集合 |
| 建造输入优先且不破坏移动/镜头 | 9, 11 | 右键不自动驾驶；WASD/middle/Home 合同 |
| Release 惰性、Editor/Development 修改器 | 8, 10, 11, 12 | 条件编译、无条件序列化 bootstrap、三构建、无 Missing Script |
| 增量 authoring 与既有身份保护 | 10 | 基础合同先验证；首次前后 GlobalObjectId/GUID；第二次内容 hash 幂等 |
| VisualSlot/共享材质/程序化占位 | 5, 10, 12 | 稳定 ID、MPB、Renderer 和 GameObject 预算 |
| 性能与完整回归 | 9, 12 | 128 实例、300 tick 零分配、五次生成、GUI 300 帧、全量测试与三构建 |
| schema/存档/2D/BUG 边界 | 10, 12, 13 | 冻结路径 diff、schema 搜索、BUG-0001 字节比较 |
| 排除生产/物流/战斗/前哨/正式存档 | Global, 12, 13 | 精确文件范围与受控文档未完成项 |

Dependency review:

- Task 1 creates the new assembly before any adapter type is referenced.
- Task 3 creates the serialized interaction component before Tasks 4 and 5 compile controllers that consume it.
- Task 4 exposes `IGrayboxBuildingPresentation3D` for atomic session tests; Task 5 creates the concrete view; Task 6 creates the construction controller after both the view and menu callback contract exist.
- Task 5 creates projector/view/placement before UI, evacuation, developer and input layers consume them.
- Task 6 creates the evacuation treatment enum with the UGUI event contract; Task 7 extends that rule file and adds evacuation behavior.
- Task 8 creates the always-serializable developer bootstrap before Task 9 routes F10.
- Task 9 creates the base generic interceptor before Task 10 serializes the building router into the scene.
- Task 10 completes scene serialization before Task 11 loads the formal scene through PlayMode.
- Task 12 is the first task allowed to add the Development build method or claim performance/build facts.
- Task 13 is the only task allowed to write controlled implementation facts, and only after independent authorization.

Signature review:

- Every type named by a later task is defined in §2 and assigned to an earlier or same task.
- `WasteCity.Game` signatures contain no Unity types.
- `WasteCity.Graybox3D` generic input contracts contain no building-adapter types.
- Conditional `GrayboxDeveloperModifier3D` is referenced only inside the bootstrap's matching compile guards and never in serialized fields.
- Old `BuildingGrid`, `BuildingUnlockModel` and base `GrayboxInputRouter` overloads remain callable.

Existing API audit against the approved parent source:

| Existing type | Exact reused signature |
|---|---|
| `GrayboxMobileCityController3D` | `bool TryToggleDeployment(out string failureReason)`; `bool TrySetDestinationCell(int cellX, int cellY, out string failureReason)`; `void TickDeployment(float deltaTime)` |
| `GrayboxInputFrame` | `GrayboxInputFrame(Vector2 move, Vector2 pointerPosition, bool toggleDeploymentPressed, bool destinationPressed, bool middlePressed, bool middleHeld, bool middleReleased, bool homePressed)` |
| `GrayboxInputRouter` | `GrayboxInputFrame ReadCurrentFrame()`; `void ProcessFrame(GrayboxInputFrame frame)`; `void TickGameplay(float deltaTime)` |
| `CityDeploymentModel` | `bool Toggle()`; `void Tick(float delta)`; `void Restore(CityMode mode, float remainingSeconds)` |
| `PlanarCoordinateMapper3D` | `bool TryCellToWorld(int cellX, int cellY, float visualY, out Vector3 world)`; `bool TryWorldToCell(Vector3 world, out int cellX, out int cellY)`; `Vector3 PlaneToWorld(Vector2 plane, float visualY)`; `Vector2 WorldToPlane(Vector3 world)` |
| `GrayboxWorldView3D` | `void Generate(WorldMapModel model)`; `bool TryWorldToCell(Vector3 world, out int cellX, out int cellY)`; public `Model` and `Coordinates` getters |
| `ResourceInventory` | `int Add(string id, int amount)`; `bool TrySpend(string id, int amount)`; `bool CanSpend(string id, int amount)`; `void Set(string id, int amount)` |
| `ResearchModel` | `bool IsCompleted(StableId id)`; `string[] CaptureCompleted()`; `void Restore(string[] completedIds, string activeId, float remaining)` |
| `ConstructionProgress` | `bool Tick(float delta, float productivity)`; `void Restore(float remaining)`; public `BaseDuration`, `Remaining`, `IsComplete`, `Normalized` getters |
| `GrayboxVisualSlot` | `void Configure(string stableId, MeshRenderer renderer, Color fallbackColor)`; `void ApplyFallback(Material sharedMaterial)` |
| `InputSystemUIInputModule` 1.7.0 | public `InputActionReference point`, `leftClick`, `move`, `submit`, `cancel` properties; public `void AssignDefaultActions()`; `OnEnable` assigns defaults when all actions are absent and enables the referenced actions |

Any implementation discovery that contradicts this table is a stop gate requiring a plan correction before production edits.

---

## Task 1: Create the assembly boundary and backward-compatible rotation

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/Building.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/WasteCity.Graybox3D.Building.asmdef`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/WasteCity.Graybox3D.Building.asmdef.meta`
- Modify: `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef`
- Create: `Assets/_Game/Scripts/Building/BuildingOrientation.cs`
- Create: `Assets/_Game/Scripts/Building/BuildingOrientation.cs.meta`
- Modify: `Assets/_Game/Scripts/Building/BuildingGrid.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingOrientationAndRangeTests.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingOrientationAndRangeTests.cs.meta`

- [ ] Confirm the branch, HEAD, clean status, and protected baseline:

```bash
git branch --show-current
git rev-parse HEAD
git status --short
git diff --name-only f6bbed8cf33bf183dd11504d46546d26bd9649f7
```

Expected: branch `codex/3d-building-design`; only the approved plan commit differs from `f6bbed8`; no working-tree output.

- [ ] Create the new asmdef and its Unity-generated `.meta`; add direct `WasteCity.Graybox3D.Building`, `Unity.InputSystem`, and `Unity.ugui` references to the EditMode asmdef before compiling tests.

- [ ] Write RED tests in `BuildingOrientationAndRangeTests` for four rotations of a 3×2 definition, clockwise wraparound, rotated placement occupancy, rotated boundary rejection, and all existing no-orientation overloads producing North placements with unchanged coordinates and counts.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.BuildingOrientationAndRangeTests \
  -testResults /tmp/wastecity-3d-building/task-01-red.xml \
  -logFile /tmp/wastecity-3d-building/task-01-red.log
```

Expected RED: compile/test failures only because `BuildingOrientation`, orientation-aware overloads, and `PlacedBuilding.Orientation` do not exist. Any asmdef reference failure is a stop gate.

- [ ] Implement `BuildingOrientationRules`; modify `BuildingGrid` so the new overloads use rotated dimensions while old methods and constructor delegate North. Do not change catalog definitions, costs, BuildMenu membership, site rules, resource deduction, upgrade semantics or 2D callers.

- [ ] Run focused GREEN with the same command, changing result/log names to `task-01-green.*`; then run existing `BuildingGridTests` and `BuildingMobilityRulesTests`.

- [ ] Prove old API source compatibility with:

```bash
rg -n "TryPlace\\(|TryRestore\\(|CanPlace\\(" Assets/_Game/Scripts Assets/_Game/Tests
git diff --check
git diff --name-only
```

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Graybox3D/Building/WasteCity.Graybox3D.Building.asmdef
git add Assets/_Game/Scripts/Graybox3D/Building/WasteCity.Graybox3D.Building.asmdef.meta
git add Assets/_Game/Scripts/Graybox3D/Building.meta
git add Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef
git add Assets/_Game/Scripts/Building/BuildingOrientation.cs
git add Assets/_Game/Scripts/Building/BuildingOrientation.cs.meta
git add Assets/_Game/Scripts/Building/BuildingGrid.cs
git add Assets/_Game/Tests/EditMode/BuildingOrientationAndRangeTests.cs
git add Assets/_Game/Tests/EditMode/BuildingOrientationAndRangeTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add backward compatible building rotation"
```

---

## Task 2: Add range, ordered placement evaluation, and multi-reason unlocks

**Files:**

- Create: `Assets/_Game/Scripts/Building/BuildingRangeRules.cs`
- Create: `Assets/_Game/Scripts/Building/BuildingRangeRules.cs.meta`
- Create: `Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs`
- Create: `Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs.meta`
- Modify: `Assets/_Game/Scripts/Building/BuildingUnlockModel.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingPlacementEvaluationTests.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingPlacementEvaluationTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/BuildingOrientationAndRangeTests.cs`

- [ ] Add RED range tests for Chebyshev radius 8 inclusive, radius 9 rejection, supported extension hooks 12/24, unsupported radii rejection, and every edge/corner of the 8×6 inner grid under all four orientations.

- [ ] Add RED placement tests that independently trigger every `BuildingPlacementFailure` and a combined request proving the exact priority order from MissingReference through InsufficientMaterials. Add multi-reason unlock tests where population, research and prerequisite building all fail, while the old `IsUnlocked` still reports the same primary reason as before.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.BuildingOrientationAndRangeTests;WasteCity.Tests.BuildingPlacementEvaluationTests;WasteCity.Tests.BuildingUnlockTests" \
  -testResults /tmp/wastecity-3d-building/task-02-red.xml \
  -logFile /tmp/wastecity-3d-building/task-02-red.log
```

Expected RED: missing range/evaluation types and `BuildingUnlockModel.Evaluate`; existing unlock tests must still compile.

- [ ] Implement the two pure rule files and the backward-compatible unlock overload. `Failures` and `Reasons` must be deterministic read-only snapshots; no Unity types, Physics queries, Renderer state or localization lookup enter these rules.

- [ ] Run focused GREEN. Verify `BuildingPlacementRules.Evaluate` does not mutate grid, inventory, research or city state.

- [ ] Run range and scope checks:

```bash
rg -n "UnityEngine|Physics|Collider|Renderer|Camera" \
  Assets/_Game/Scripts/Building/BuildingRangeRules.cs \
  Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs \
  Assets/_Game/Scripts/Building/BuildingUnlockModel.cs
git diff --check
```

Expected: no Unity coupling matches.

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Building/BuildingRangeRules.cs
git add Assets/_Game/Scripts/Building/BuildingRangeRules.cs.meta
git add Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs
git add Assets/_Game/Scripts/Building/BuildingPlacementEvaluation.cs.meta
git add Assets/_Game/Scripts/Building/BuildingUnlockModel.cs
git add Assets/_Game/Tests/EditMode/BuildingPlacementEvaluationTests.cs
git add Assets/_Game/Tests/EditMode/BuildingPlacementEvaluationTests.cs.meta
git add Assets/_Game/Tests/EditMode/BuildingOrientationAndRangeTests.cs
git diff --cached --name-only
git commit -m "feat: evaluate building placement rules"
```

---

## Task 3: Project the exact 28-item catalog and fixed quickbar

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInteractionModel3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInteractionModel3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs.meta`

- [ ] Encode in RED tests the approved stable-ID classification table from specification §7.2, including all five top-level categories and four route filters. Assert exactly 28 unique IDs, equality with `BuildingCatalog.BuildMenu`, explicit exclusion of `HeavyMachineGunTurret` and `SwordRidingPlatform`, `CategoryOf`/`RouteOf` agreement with every query/card, and `ArgumentException` for definitions outside BuildMenu.

- [ ] Add RED tests for the exact ten-slot quickbar order, visible-content-only case-insensitive search, hidden untouched routes not leaking names or lock reasons, contacted route items becoming visible, locked cards exposing primary plus all reasons, buildable cards exposing no stale reason, and no category table outside the presenter source.

- [ ] Add RED interaction-model tests for Inactive initial state, catalog origin capture, Previewing selection/orientation retention, new-card replacement, and deterministic cancel-confirmation transitions. This creates the scene-serializable state dependency, with no Renderer/Physics/Input/Persistence responsibility, before placement and construction controllers compile.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxBuildingCatalogTests \
  -testResults /tmp/wastecity-3d-building/task-03-red.xml \
  -logFile /tmp/wastecity-3d-building/task-03-red.log
```

Expected RED: only `GrayboxBuildingCatalogPresenter3D` and `GrayboxBuildingInteractionModel3D` types/behavior are missing.

- [ ] Implement a stable-ID dictionary with one entry per BuildMenu item. Query order must follow `BuildingCatalog.BuildMenu`, never display-name sorting. Use `RouteContentDisplayCatalog.BuildingRoute`, `BuildingUnlockModel.Evaluate`, and the injected context; do not duplicate building definitions or alter `BuildingCatalog`. Implement the interaction model as a serializable MonoBehaviour with no Renderer, Physics, input-device or persistence responsibility so Tasks 4–6 consume one scene-stable state truth.

- [ ] Run focused GREEN and inspect the test output listing all 28 IDs once.

- [ ] Verify source and scope:

```bash
rg -n "HeavyMachineGunTurret|SwordRidingPlatform|BuildMenuCount|Quickbar" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs \
  Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs
git diff --check
```

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingCatalogPresenter3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInteractionModel3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInteractionModel3D.cs.meta
git add Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs.meta
git diff --cached --name-only
git commit -m "feat: project graybox building catalog"
```

---

## Task 4: Implement the session, atomic construction, progress, and refunds

**Files:**

- Create: `Assets/_Game/Scripts/Building/ConstructionRefundRules.cs`
- Create: `Assets/_Game/Scripts/Building/ConstructionRefundRules.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs`

- [ ] Write RED tests for the exact development fixture values, ground 32×24 and inner 8×6 grids, radius 8, no initial research/routes/prerequisites, and finite inventory.

- [ ] Write RED `CatalogRevision` tests for the frozen `public uint CatalogRevision { get; }` contract: a fixture/model rebuild advances it once with unchecked arithmetic; an actual route-contact addition/removal and each newly completed research ID advance it once; repeated/no-op route or research calls do not. Prove a normal `TickConstruction` completion advances once only after `presentation.UpdateInstance` succeeds, a presentation failure rollback leaves it unchanged, and `CompleteAllConstructionForDevelopment` advances once per successfully committed instance while already-completed items do not advance it.

- [ ] Write RED transaction tests for: successful one-time spend; stable instance ID; one occupied footprint; presentation success; insufficient material leaves all state unchanged; grid failure leaves inventory unchanged; presentation false rolls back grid and full actual spend; presentation exception rolls back then rethrows; retry receives deterministic next stable ID without orphan.

- [ ] Write RED construction tests for paused no progress, illegal mode no progress, legal inner Mobile progress, legal Fortress progress, multiplier 1/10/100, completion retains same stable ID and changes presentation state once.

- [ ] Write RED session/catalog integration tests for prerequisite counts available before evacuation exists. Prove matching Completed Ground and InnerCity instances count, while UnderConstruction, a different definition, and null/empty/unknown IDs return zero and cannot make `GrayboxBuildingCatalogPresenter3D.Describe` unlock a dependent card. Prove the revision changes on the completed prerequisite's successful commit, so Task 6 can refresh the dependent card from the same session state. Task 7 extends the same tests with non-owned, ruin, and evacuation-lock cases.

- [ ] Write RED refund tests for raw fractions below 0.5, exactly 0.5, and above 0.5; verify `MidpointRounding.AwayFromZero`, handling ratios 1.0/0.8/0.5/0, remaining ratio clamp, original cost clamp, accepted amount limited by `ResourceInventory.Add`, grid release and view removal.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxBuildingSessionTests;WasteCity.Tests.GrayboxBuildingCatalogTests" \
  -testResults /tmp/wastecity-3d-building/task-04-red.xml \
  -logFile /tmp/wastecity-3d-building/task-04-red.log
```

Expected RED: missing refund/session types and session construction behavior only.

- [ ] Implement `ConstructionRefundRules.Calculate` exactly:

```csharp
double raw = Math.Max(0d, originalCost) *
             Math.Max(0d, Math.Min(1d, remainingRatio)) *
             Math.Max(0d, handlingRatio);
int rounded = (int)Math.Round(
    raw,
    MidpointRounding.AwayFromZero);
return Math.Max(0, Math.Min(originalCost, rounded));
```

- [ ] Implement the Task 4 session subset: `Configure`, `ConfigureDevelopmentFixture`, `TryBeginConstruction`, `TryCancelConstruction`, `TickConstruction`, exact `CompletedBuildingCount` semantics, route/research development methods, construction multiplier, complete-all, and the frozen `CatalogRevision` contract in §2.4. At this stage the lock flag is false for every instance; Task 7 adds its only mutation path. Do not add `HasPlayerOwnedGroundInstances`, `CopyPlayerOwnedGroundInstances`, `TryLockEvacuationWork`, `RollbackEvacuationLocksAfterFailure`, or `TryCommitEvacuation` until Task 7 creates the evacuation rule behavior and modifies the session. Use the `IGrayboxBuildingPresentation3D` transaction seam. `Configure(true)` persists only the fixture switch, and `Awake` rebuilds the finite models on every real scene load. Use `ResourceInventory`, `ResearchModel`, `BuildingGrid`, `ConstructionProgress`, and `BuildingMobilityRules.CanConstruct` directly. Do not add a parallel currency store, timer, occupancy array, unlock table, or a second catalog invalidation mechanism.

- [ ] Run focused GREEN; then run existing `ConstructionProgressTests`, `BuildingGridTests`, `BuildingUnlockTests`, and `ResearchTests`.

- [ ] Inspect atomicity and allocation-sensitive paths:

```bash
rg -n "TrySpend|TryRestore|Rollback|Math.Round|AwayFromZero|TickConstruction" \
  Assets/_Game/Scripts/Building/ConstructionRefundRules.cs \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs
git diff --check
```

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Building/ConstructionRefundRules.cs
git add Assets/_Game/Scripts/Building/ConstructionRefundRules.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs.meta
git add Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs.meta
git add Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs
git diff --cached --name-only
git commit -m "feat: add atomic graybox construction session"
```

---

## Task 5: Add dual-grid projection, placement preview, and graybox presentation

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSurfaceProjector3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSurfaceProjector3D.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs.meta`

- [ ] Write RED projector tests for: inner surface wins when its Collider is hit; inner local anchor `(-1.28,-0.96)`, cell size 0.32 and 8×6 bounds; ground uses Camera ray plus mathematical `Y=0` plane and existing `PlanarCoordinateMapper3D`; parallel/backward rays fail; outside world/platform fails; city motion changes inner world point without changing inner logical cell.

- [ ] In projector tests, create an isolated Physics scene or explicit Collider fixture and call `Physics.SyncTransforms()` before ray assertions. If Unity 2022.3 Collider.Raycast behavior differs from the approved contract, stop rather than replacing logical evaluation with Physics truth.

- [ ] Write RED placement/view tests proving all footprint cells feed terrain, obstacle, city-body, compatible-node and grid checks. For `MiningStation`, Iron and EnergyCrystal nodes must each pass with their own coordinate `world.resource-node.<x>.<y>` IDs; Stone, Biomass, Water, and other `HasResource` types must reject with `IncompatibleResourceNode` and no `CompatibleResourceNodeId` (explicitly assert the Stone null-ID case). A non-node-requiring definition remains node-independent and exposes no compatible node ID even when its footprint includes a resource cell. Two cells holding the same compatible `WorldCell.ResourceId` produce distinct coordinate IDs and distinct VisualSlots; `CompatibleResourceNodeId` never equals a resource type ID. Prove green/red preview exposes the ordered reason; confirmation reevaluates instead of trusting the cached preview; selection remains active after success for continuous placement; material exhaustion leaves a red preview; Collider ray selection resolves only a stable instance ID.

- [ ] Write RED presentation tests for stable IDs:

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

Assert one shared material, MaterialPropertyBlock color changes, at most one Renderer per instance, no GameObject per footprint cell, and infrastructure Renderer count at most 8.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxBuildingProjectionAndViewTests \
  -testResults /tmp/wastecity-3d-building/task-05-red.xml \
  -logFile /tmp/wastecity-3d-building/task-05-red.log
```

Expected RED: missing projector/placement/view types only.

- [ ] Implement inner projection using the serialized inner surface Collider solely to identify and locate the platform hit. Convert that point through `city.transform.InverseTransformPoint`; compute the 8×6 cell using the frozen anchor and cell size. Implement ground projection with the math plane and `GrayboxWorldView3D.Coordinates`; do not call `Physics.Raycast` for ground truth.

- [ ] Implement visual meshes with primitives or `GrayboxMeshBuilder`, shared `GrayboxLit`, `GrayboxVisualSlot`, and MPB. Completed, construction and ruin state must update the same instance object instead of destroying/recreating stable identity.

- [ ] Run focused GREEN and existing `PlanarCoordinateMapper3DTests`, `GrayboxVisualAndWorldTests`, and `GrayboxMobileCityController3DTests`.

- [ ] Run structure checks:

```bash
rg -n "new GameObject|AddComponent<MeshRenderer>|sharedMaterial|MaterialPropertyBlock|GrayboxVisualSlot" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs
rg -n "Physics\\.Raycast|Collider\\.Raycast|Plane" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSurfaceProjector3D.cs
git diff --check
```

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSurfaceProjector3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSurfaceProjector3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs.meta
git add Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add graybox dual grid placement view"
```

---

## Task 6: Implement the two-layer UGUI menu and return-state model

**Files:**

- Create: `Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs`
- Create: `Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxUiInputGuard3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxUiInputGuard3D.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxConstructionController3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxConstructionController3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs.meta`

- [ ] Write RED state tests for every transition in specification §8.4, especially both Catalog origins, retained selection/orientation, new-card selection returning Previewing, first Esc consumed by focused InputField, next Esc closing to returnState, and a further Esc canceling preview.

- [ ] Write RED menu tests for always-visible ten-slot quickbar, B catalog vertical layer, five categories, four route filters, visible-only search, cost text, hover detail fields, hidden empty shortcut slots, locked disabled cards with primary/all reasons, `TrySelectQuickbarSlot(0..9)` and `TrySelectCatalogItem(stableId)` rejecting hidden/locked items and selecting only buildable definitions through interaction, selecting a card auto-closing catalog, and world continuing while catalog is open. Build Details must show `最低人口：<N>` for every definition with `MinimumPopulation > 0` even when the fixture already meets it, while retaining research, prerequisite-building, and current lock-reason detail.

- [ ] Add RED revision-driven menu tests. `RefreshCatalog()` must synchronize the cached session revision. Without an explicit refresh, one later `Update` after an actual route contact, research unlock, or successful prerequisite completion must rebuild the affected catalog/quickbar projection from the same session and expose the newly visible/unlocked card. Repeated `Update` calls with an unchanged revision must preserve the exact existing quickbar/card `GameObject` and `Transform` identities; they may still perform visibility and placement-status sync.

- [ ] Write RED UGUI guard tests using a real `EventSystem`, `InputSystemUIInputModule`, `Canvas`, `GraphicRaycaster`, `InputField`, `Button`, and pointer event data. Assert editable/keyboard controls own focus, text input consumes W/A/S/D/B/R/digits, and pointer-over-UI reports true without relying on object names. Add a real active `PhysicsRaycaster`/world-Collider negative case proving an `EventSystem.RaycastAll` physics result alone does not capture UI, and a real active `GraphicRaycaster` positive case proving its result does; retain a separate `GraphicRegistry` fallback-positive case.

- [ ] Add RED callback tests proving construction cancel/confirmation buttons and evacuation item/category/all/confirm buttons raise exactly the public events in §2.6, never mutate session/deployment directly, and do not retain duplicate listeners after a view/controller is destroyed and recreated.

- [ ] Add RED construction-controller tests proving Collider selection resolves a stable instance ID; zero-progress Delete cancels immediately through the refund function; progressed construction requires confirmation; menu cancel events route to the same methods; `Update` delegates construction progress once per frame; `OnDestroy` removes menu listeners; input router remains uninvolved.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxBuildingUiAndInputTests \
  -testResults /tmp/wastecity-3d-building/task-06-red.xml \
  -logFile /tmp/wastecity-3d-building/task-06-red.log
```

Expected RED: missing `BuildingEvacuationTreatment`, `GrayboxBuildingMenuView3D`, `GrayboxUiInputGuard3D`, and `GrayboxConstructionController3D` types/behavior only; the Task 3 interaction component must compile and retain its GREEN tests. A missing `Unity.ugui` or Input System UI reference is a stop gate.

- [ ] Define `BuildingEvacuationTreatment` in `BuildingEvacuationRules.cs` so the UGUI event contract compiles; Task 7 will add work/rule behavior to the same file. Reuse the Task 3 scene-serialized interaction component unchanged. Implement UGUI view construction from stable procedural elements; do not add icons, TMP, prefabs, textures or packages. UI events call its public methods; the component has no Renderer/Physics/Input/Persistence responsibility and remains the state source of truth. Implement the construction controller now that its concrete view and menu event contracts both exist.

- [ ] Implement focus precedence: when an editable/keyboard UGUI control is selected, UI consumes text/navigation/submit/cancel. `ConsumeFocusedEscape` uses EventSystem deselection/end-edit semantics, and gameplay is eligible only on the following frame.

- [ ] Implement the §2.6 pointer filter exactly: inspect every `EventSystem.RaycastAll` result's `module`, accept only an active/enabled `GraphicRaycaster` with an active/enabled owning `Canvas`, and ignore Physics/Physics2D/world raycasters. Preserve the active `GraphicRegistry` fallback without introducing physics checks. Make `RefreshCatalog()` synchronize the menu's cached `CatalogRevision` after a successful rebuild; `Update` refreshes only when that revision changes before its existing visibility/status synchronization. Render the explicit minimum-population line independently of the current unlock result.

- [ ] Run focused GREEN. Verify no hidden item text is present in active or inactive card GameObjects and no 28-card scene pool is serialized.

- [ ] Run:

```bash
rg -n "TextMeshPro|TMP_|Resources\\.Load|AssetDatabase|Time\\.timeScale" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxUiInputGuard3D.cs
git diff --check
```

Expected: no new package/asset loading and no menu pause behavior.

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs
git add Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxUiInputGuard3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxUiInputGuard3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxConstructionController3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxConstructionController3D.cs.meta
git add Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add graybox building menu state"
```

---

## Task 7: Add evacuation rules and Packing interception

**Files:**

- Modify: `Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs.meta`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs`

- [ ] Write RED pure-rule tests for completed and incomplete instances under Abandon, FullDismantle and QuickDismantle; assert 0/80/50 handling, 50% duration, remaining-ratio-first calculation, AwayFromZero rounding, clamping, and ordinal stable-ID queue order.

- [ ] Write RED controller tests for: `CopyPlayerOwnedGroundInstances` filtering and stable ordering; `HasPlayerOwnedGroundInstances`; no ground ownership delegates exactly to `city.TryToggleDeployment(out _)`; Fortress with owned ground instances remains Fortress and opens manifest; inner instances never enter manifest; single/category/all assignments can mix; `AssignCategory` agrees with `GrayboxBuildingCatalogPresenter3D.CategoryOf`; unassigned blocks confirmation; abandonment creates a non-owned blocking ruin with zero refund; quick removes immediately; all Full work is snapshotted and atomically locked at confirmation; a failure while validating/locking item N leaves items 1..N-1 unlocked; full processes sequentially and pauses at timeScale 0; locked current and later queue items never advance construction; work refund/remaining ratio/duration stay equal to the confirmation snapshot even across elapsed frames; `TryCommitEvacuation(in work, ...)` rejects a mismatched snapshot without recomputing; successful commit consumes its lock; validation or presentation exceptions roll back all remaining queue locks and state or raise the specified compound failure; all resolved invokes existing packing once; Packing later returns city control through existing coordinator behavior. Assert no user action or menu callback cancels a successfully confirmed queue.

- [ ] Extend `GrayboxBuildingSessionTests` and `GrayboxBuildingCatalogTests` to prove non-owned Completed, AbandonedRuin, evacuation-locked Completed, and the currently dismantling item never contribute to `CompletedBuildingCount` or unlock a dependent catalog card; both owned, unlocked Completed Ground and InnerCity instances do count. Include tests that an abandoned ruin still makes `BuildingGrid.IsOccupied` true and cannot produce/operate through the session state, while no new outpost, salvage or recovery semantics appear.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxEvacuationTests;WasteCity.Tests.GrayboxBuildingSessionTests;WasteCity.Tests.GrayboxBuildingCatalogTests" \
  -testResults /tmp/wastecity-3d-building/task-07-red.xml \
  -logFile /tmp/wastecity-3d-building/task-07-red.log
```

Expected RED: the Task 6 treatment enum exists, but `BuildingEvacuationWork`, rule methods, lock/snapshot/commit session APIs, lock-aware construction behavior, and `GrayboxEvacuationController3D` behavior are missing.

- [ ] Implement pure rules using `ConstructionRefundRules`. At `ConfirmManifest`, create every immutable work snapshot once, sort Full work by ordinal stable ID, and call session `TryLockEvacuationWork` once before any timer or immediate commit. Session prevalidates the whole list, copies exact work values into its private lock table, marks instances, and makes `TickConstruction` skip all locks. Implement controller as a request interceptor around exact existing API `GrayboxMobileCityController3D.TryToggleDeployment(out string failureReason)` and the session's `CopyPlayerOwnedGroundInstances`, `HasPlayerOwnedGroundInstances`, `TryLockEvacuationWork`, `RollbackEvacuationLocksAfterFailure`, and `TryCommitEvacuation(in BuildingEvacuationWork, ...)` APIs. Category batches call only presenter `CategoryOf`; the controller must not copy category mappings or write `CityDeploymentModel.Mode`, Transform, grid private cells or inventory internals.

- [ ] Keep Abandon and QuickDismantle immediate after a successful lock phase. FullDismantle advances only the controller's current queue timer, uses the captured `DismantleSeconds`, and commits the exact captured work. On any confirmation/commit failure or exception, use the single failure-only rollback API to release every remaining lock before returning to the open manifest; do not add an input, button, or public gameplay flow for cancelling a confirmed queue.

- [ ] Implement menu manifest rows and single/category/all controls using stable instance IDs. The controller owns work state; UI is projection only.

- [ ] In `Configure`, subscribe once to the menu evacuation events; map them to `Assign`, `AssignCategory`, `AssignAll`, and `ConfirmManifest`, and unsubscribe all four in `OnDestroy`. Re-run Task 6 callback tests to protect the already-implemented construction listener pair.

- [ ] Run focused GREEN and existing `CityDeploymentRulesTests`, `GrayboxMobileCityController3DTests`, and `GrayboxLeaderControlTests`.

- [ ] Inspect for forbidden mode/grid writes:

```bash
rg -n "Deployment\\.Restore|transform\\.position|cells\\[|Inventory\\.Restore" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs \
  Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs
git diff --check
```

Expected: no direct bypass matches.

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs
git add Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs.meta
git add Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxBuildingCatalogTests.cs
git diff --cached --name-only
git commit -m "feat: add graybox evacuation handling"
```

---

## Task 8: Add the serializable Release-safe developer modifier

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifier3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifier3D.cs.meta`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxDeveloperModifierTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxDeveloperModifierTests.cs.meta`

- [ ] Write RED tests for `ResolveRuntimeAvailability(false,false) == false`, and true for Editor or Development. Reflect serialized fields and assert none reference a conditionally compiled type. Assert the bootstrap declares no `Update` input loop and source-audit its `Awake`/lifecycle code for zero `Keyboard.current`, `f10Key`, or Input System polling; `TryTogglePanel` is the only panel-toggle entry.

- [ ] Under the Editor test compilation, write RED command tests for resource +100/+1000/clear/set with capacity rules; single research/route/all unlock without relocking; safe Mobile/Fortress set; completing Deploying/Packing; construction multiplier 1×/10×/100×; immediate completion finishing every existing site in the same frame through `CompleteAllConstructionForDevelopment`; immediate completion preserving the prior multiplier so the next site is not accelerated; exit/session recreation discarding all changes.

- [ ] Add spy/model-state assertions proving commands only call `ResourceInventory`, `ResearchModel`, session methods and the city's dedicated development adapter. Assert no direct Transform assignment, grid field reflection or bypassed placement confirmation.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxDeveloperModifierTests \
  -testResults /tmp/wastecity-3d-building/task-08-red.xml \
  -logFile /tmp/wastecity-3d-building/task-08-red.log
```

Expected RED: missing bootstrap/modifier and city development adapter only.

- [ ] Implement the always-compiled bootstrap with only normal serializable Unity/component fields. Place command service construction, panel creation, and `TryTogglePanel` behavior inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; Release method bodies remain inert and deterministic. Do not declare an `Update` input reader and do not read Keyboard/F10 in `Awake`, `Update`, or any bootstrap lifecycle callback: Task 9's building input router is the sole reader.

- [ ] Add to `GrayboxMobileCityController3D`:

```csharp
public bool RestoreDeploymentForDevelopment(CityMode mode);
public bool CompleteDeploymentTransitionForDevelopment();
```

`RestoreDeploymentForDevelopment` accepts only `Mobile` or `Fortress`, calls exact existing `CityDeploymentModel.Restore(mode, 0f)`, refreshes presentation, and returns true; transitional/undefined values return false without mutation. `CompleteDeploymentTransitionForDevelopment` returns false unless the current mode is Deploying/Packing, otherwise calls existing `CityDeploymentModel.Tick(float.MaxValue)`, refreshes presentation, and returns true. Neither method sets Transform, Collider or renderer as gameplay truth.

- [ ] Run focused GREEN, then existing mobile city/deployment tests. Inspect the compiled-source boundary:

```bash
rg -n "#if|#endif|SerializeField|GrayboxDeveloperModifier3D|TryTogglePanel" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifier3D.cs
if rg -n "Keyboard\\.current|f10Key|void Update\\s*\\(" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs; then
  echo "bootstrap must not poll F10" >&2
  exit 1
fi
git diff --check
```

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifier3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifier3D.cs.meta
git add Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs
git add Assets/_Game/Tests/EditMode/GrayboxDeveloperModifierTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxDeveloperModifierTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add release safe graybox developer modifier"
```

---

## Task 9: Integrate build-mode input without leaking through UGUI

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/IGrayboxInputInterceptor.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/IGrayboxInputInterceptor.cs.meta`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxCameraAndInputTests.cs`

- [ ] Write RED base-router tests proving an interceptor is called before base frame processing and can independently suppress move, F deployment, right-click destination, middle drag and Home while unsuppressed channels retain existing behavior. Existing no-interceptor tests must remain unchanged.

- [ ] Write RED building-router tests for:

```text
B toggles catalog
1–0 selects fixed visible quickbar
R rotates 90°
left click confirms only valid preview
right click/Esc cancels according to state
Delete cancels zero-progress work immediately and opens confirmation for progressed work
F delegates to evacuation before base deployment
right click in build mode never sets city destination
WASD remains unsuppressed in build mode when UI has no focus
middle drag and Home remain unsuppressed when pointer/focus permits
UI pointer blocks world click and camera drag
focused keyboard UGUI blocks W/A/S/D/B/R/1–0/F/F10/Home/Delete/Esc/Enter
focus loss restores gameplay on the next frame
F10 after focus/modal handling calls developer.TryTogglePanel exactly once
bootstrap and router cannot both read F10
outside build mode digits remain unconsumed for future skills
```

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxBuildingUiAndInputTests;WasteCity.Tests.GrayboxCameraAndInputTests" \
  -testResults /tmp/wastecity-3d-building/task-09-red.xml \
  -logFile /tmp/wastecity-3d-building/task-09-red.log
```

Expected RED: missing generic interceptor/suppression and building router behavior only. Any existing camera/input regression is a stop gate.

- [ ] Implement `IGrayboxInputInterceptor` in the base assembly and a serialized optional `MonoBehaviour` field in `GrayboxInputRouter`; validate it implements the interface in `ConfigureInputInterceptor`. Preserve all existing overloads and runtime tick order.

- [ ] Implement building input priority:

```text
focused editable/keyboard UGUI
→ open modal/evacuation confirmation
→ one guarded F10 read and developer.TryTogglePanel
→ pointer-over-UGUI for pointer channels
→ catalog/build selection actions
→ unconsumed base city/leader/camera actions
```

When paused, building construction and gameplay actions remain disabled; UGUI navigation and already-approved camera Home/middle behavior remain available unless UI specifically owns that input.

Digits and catalog card actions call only `menu.TrySelectQuickbarSlot`/`menu.TrySelectCatalogItem`; the input router has no session/presenter field and cannot inspect visibility, lock reasons or the 28-item mapping.

`ProcessCurrentInput` is the only method in the project that reads `Keyboard.current.f10Key.wasPressedThisFrame`. It performs that read only after returning for focused editable/keyboard UGUI and after an open modal has consumed its input. In Editor/Development it calls the serialized bootstrap's `TryTogglePanel` once; in Release the same call is harmless and returns false, while compile guards prevent creating developer UI/commands. A focused search field therefore blocks F10; after focus loss a fresh F10 edge toggles the panel exactly once.

- [ ] Run focused GREEN. Add a 300-call warmed allocation assertion for `ProcessCurrentInput` with unchanged device state; record the byte difference and require zero.

- [ ] Run existing `GrayboxCameraAndInputTests` and `GrayboxLeaderControlTests`, then:

```bash
rg -n "TickMovement|TickDeployment|TickControl|TickCamera|ProcessFrame" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs
test "$(rg -l "f10Key" \
  Assets/_Game/Scripts | wc -l | tr -d ' ')" = "1"
if rg -n "Keyboard\\.current|f10Key|void Update\\s*\\(" \
  Assets/_Game/Scripts/Graybox3D/Building/GrayboxDeveloperModifierBootstrap3D.cs; then
  echo "bootstrap must not poll F10" >&2
  exit 1
fi
git diff --check
```

Expected: building router does not directly drive any gameplay tick.

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Scripts/Graybox3D/IGrayboxInputInterceptor.cs
git add Assets/_Game/Scripts/Graybox3D/IGrayboxInputInterceptor.cs.meta
git add Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs.meta
git add Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs
git add Assets/_Game/Tests/EditMode/GrayboxCameraAndInputTests.cs
git diff --cached --name-only
git commit -m "feat: route graybox building input"
```

---

## Task 10: Author and verify the complete static 3D scene contract

**Files:**

- Modify: `Assets/_Game/Editor/WasteCity.Editor.asmdef`
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`

**Interfaces:**

```csharp
public static class GrayboxSceneAuthoring
{
    public static void Configure();
    public static void CaptureFoundationIdentity();
    private static bool TryOpenAndValidateFoundation(
        out Scene scene);
    private static Scene CreateFoundationScene(
        UniversalRenderPipelineAsset pipeline,
        Material material);
    private static void ValidateFoundationContract(Scene scene);
    private static void EnsureBuildingContract(
        Scene scene,
        Material material);
}
```

`CaptureFoundationIdentity` requires the absolute output path in `WASTECITY_GRAYBOX_IDENTITY_RESULT` and writes deterministic, name-sorted JSON with no timestamp, process ID or machine-dependent field. It contains the scene GUID plus `GlobalObjectId.GetGlobalObjectIdSlow(...).ToString()` for the existing root, `GrayboxUrpScope`, `GrayboxWorldView3D`, `GrayboxSceneBootstrap`, MobileCity GameObject/controller/Rigidbody, Leader GameObject/controller, CameraRig, Main Camera GameObject/component, `GrayboxInputRouter`, `GrayboxGroundProjector`, `GrayboxDirectControlCoordinator`, and `GrayboxCameraController3D`. It opens the scene read-only and never saves it.

- [ ] Add RED scene-contract tests that open the real scene and require:

```text
GrayboxPrototype3D/GrayboxBuilding/BuildingSession
GrayboxPrototype3D/GrayboxBuilding/BuildingInteraction
GrayboxPrototype3D/GrayboxBuilding/BuildingPresentation/InstanceRoot
GrayboxPrototype3D/GrayboxBuilding/BuildingPresentation/InfrastructureRoot
GrayboxPrototype3D/GrayboxBuilding/BuildingSurfaceProjector
GrayboxPrototype3D/GrayboxBuilding/BuildingPlacement
GrayboxPrototype3D/GrayboxBuilding/Construction
GrayboxPrototype3D/GrayboxBuilding/Evacuation
GrayboxPrototype3D/GrayboxBuilding/BuildingInput
GrayboxPrototype3D/GrayboxBuilding/DeveloperModifierBootstrap
GrayboxPrototype3D/GrayboxUI/BuildingCanvas
GrayboxPrototype3D/GrayboxUI/EventSystem
GrayboxPrototype3D/GrayboxActors/MobileCity/InnerCityPlatform
```

Assert one EventSystem/InputSystemUIInputModule, one enabled GraphicRaycaster, all serialized references, base `GrayboxInputRouter` interceptor reference, 8×6 platform dimensions, no conditional modifier type serialization, no Missing Script, no 28 precreated card/building objects, and unchanged camera/city/leader/world contracts. Use an EditMode `UnityTest` to save, close, and reopen the real scene, yield one Editor update for the public enable lifecycle, then inspect only the module's public `point`, `leftClick`, `move`, `submit`, and `cancel` properties: every reference and `.action` must be non-null and each action must have at least one binding. The assertion accepts either serialized references or public default assignment during lifecycle; it verifies only the usable post-reload contract. Add a behavior test that builds an unsaved in-memory scene missing one foundation object and invokes `ValidateFoundationContract` through reflection, expecting `InvalidOperationException`; the test must not delete or alter the real scene asset. Add a source assertion that the existing-scene path cannot enter `NewScene`.

- [ ] Run focused RED. It must fail only because the existing scene lacks new components/references:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxSceneContractTests \
  -testResults /tmp/wastecity-3d-building/task-10-red.xml \
  -logFile /tmp/wastecity-3d-building/task-10-red.log
```

- [ ] Add direct `WasteCity.Graybox3D.Building`, `Unity.ugui`, and `Unity.InputSystem` references to the Editor asmdef.

- [ ] Refactor authoring with this exact safety split:

```text
Scene asset absent
  → after rendering assets are available, CreateFoundationScene may execute the existing foundation creation path

Scene asset present
  → Configure first opens existing scene before EnsureRenderer/EnsurePipeline/EnsureMaterial
  → ValidateFoundationContract checks every frozen world/city/leader/camera/input object and serialized reference
  → any missing/duplicate/broken foundation item throws InvalidOperationException before scene/rendering-asset modification or save
  → only after validation may Configure ensure rendering assets
  → EnsureBuildingContract incrementally EnsureChild/EnsureComponent/configures only the new building/UI subtree and new references
```

`TryOpenAndValidateFoundation` returns false only when the scene asset does not exist; an invalid existing scene throws. For an existing scene, neither it nor `EnsureBuildingContract` may call `EditorSceneManager.NewScene`, destroy/recreate the root, world, city, leader, camera, base systems, or replace existing components. The old `TryOpenCompleteScene == false → NewScene` branch must not be reused for a missing building contract.

- [ ] `EnsureBuildingContract` must use the existing city object as the inner platform parent. The inner surface bottom/top alignment keeps gameplay grid local, and its Collider only serves projector selection. Reuse the single existing EventSystem when present; otherwise create one. Reuse or add exactly one `InputSystemUIInputModule`; if any public `point`, `leftClick`, `move`, `submit`, or `cancel` reference/action is absent during authoring, call the verified Input System 1.7.0 public API `AssignDefaultActions()` before saving. Do not create a project `.inputactions` asset or read/write private fields. Reopen the saved scene, allow the public enable lifecycle one Editor update, and run the five-reference/action/binding contract before accepting authoring; the final contract does not depend on whether a usable action came from serialization or public runtime default assignment. If public lifecycle/serialization does not retain or create usable actions, stop and report the public API result instead of reaching into private fields or inventing a package workaround. No scene object represents individual catalog entries or 768 world cells. Retain Build Settings order `GrayboxPrototype3D` index 0, `FormalPrototype` index 1.

- [ ] After implementing but before the first `Configure`, close GUI Unity, confirm project lock removal, then capture the existing identity/GUID baseline:

```bash
WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-3d-building/task-10-foundation-before.json \
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity \
  -logFile /tmp/wastecity-3d-building/task-10-identity-before.log

shasum -a 256 \
  Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat.meta \
  > /tmp/wastecity-3d-building/task-10-guids-before.sha256

/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure \
  -logFile /tmp/wastecity-3d-building/task-10-authoring-1.log

WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-3d-building/task-10-foundation-after-1.json \
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity \
  -logFile /tmp/wastecity-3d-building/task-10-identity-after-1.log

shasum -a 256 \
  Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat.meta \
  > /tmp/wastecity-3d-building/task-10-guids-after-1.sha256

shasum -a 256 Assets/_Game/Scenes/GrayboxPrototype3D.unity \
  > /tmp/wastecity-3d-building/task-10-scene-after-1.sha256

/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure \
  -logFile /tmp/wastecity-3d-building/task-10-authoring-2.log

WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-3d-building/task-10-foundation-after-2.json \
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity \
  -logFile /tmp/wastecity-3d-building/task-10-identity-after-2.log

shasum -a 256 \
  Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat.meta \
  > /tmp/wastecity-3d-building/task-10-guids-after-2.sha256

shasum -a 256 Assets/_Game/Scenes/GrayboxPrototype3D.unity \
  > /tmp/wastecity-3d-building/task-10-scene-after-2.sha256

cmp /tmp/wastecity-3d-building/task-10-foundation-before.json \
    /tmp/wastecity-3d-building/task-10-foundation-after-1.json
cmp /tmp/wastecity-3d-building/task-10-foundation-before.json \
    /tmp/wastecity-3d-building/task-10-foundation-after-2.json
cmp /tmp/wastecity-3d-building/task-10-guids-before.sha256 \
    /tmp/wastecity-3d-building/task-10-guids-after-1.sha256
cmp /tmp/wastecity-3d-building/task-10-guids-before.sha256 \
    /tmp/wastecity-3d-building/task-10-guids-after-2.sha256
cmp /tmp/wastecity-3d-building/task-10-scene-after-1.sha256 \
    /tmp/wastecity-3d-building/task-10-scene-after-2.sha256
```

Any first-pass change to a captured foundation `GlobalObjectId`/scene GUID/rendering GUID is a stop gate. Any second-pass scene-content change is also a stop gate. Do not normalize by regenerating `.meta` or accept changed local fileIDs because the final scene looks equivalent.

- [ ] Run focused GREEN and inspect `EditorBuildSettings.asset` remains 3D index 0 and 2D index 1.

- [ ] Run frozen checks:

```bash
git diff --check
git diff --exit-code f6bbed8 -- Assets/_Game/Scenes/FormalPrototype.unity
git diff --exit-code f6bbed8 -- ProjectSettings/GraphicsSettings.asset
git diff --exit-code f6bbed8 -- ProjectSettings/QualitySettings.asset
git diff --exit-code f6bbed8 -- Packages
```

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Editor/WasteCity.Editor.asmdef
git add Assets/_Game/Editor/GrayboxSceneAuthoring.cs
git add Assets/_Game/Scenes/GrayboxPrototype3D.unity
git add Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs
git diff --cached --name-only
git commit -m "feat: wire graybox building scene"
```

---

## Task 11: Prove real Update-loop interaction with virtual keyboard and mouse

**Files:**

- Modify: `Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef`
- Create: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`
- Create: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs.meta`

- [ ] Build the fixture by copying the proven lifecycle from `GrayboxRuntimeSceneTests`: save `updateMode`, `backgroundBehavior`, `editorInputBehaviorInPlayMode`, `timeScale`, Graphics pipeline and Quality pipeline; set `ProcessEventsManually`, `IgnoreFocus`, `AllDeviceInputAlwaysGoesToGameView`; add/MakeCurrent virtual Keyboard and Mouse; load the real scene. TearDown must use nested `try/finally` to remove only test devices and restore every setting, timeScale and an empty scene.

- [ ] For each helper, use:

```text
QueueStateEvent
→ InputSystem.Update()
→ assert current virtual device and immediate key/button state
→ yield return null so real MonoBehaviour.Update consumes it
→ retain held state across real FixedUpdate when movement is required
→ queue release
→ InputSystem.Update()
→ yield return null
```

Tests must not call `ProcessCurrentInput`, `ProcessFrame`, `ReadCurrentFrame`, `TickGameplay`, `TickMovement`, `TickDeployment`, `TickConstruction`, `GrayboxEvacuationController3D.Tick` or camera methods directly.

- [ ] Add initial RED tests for scene reload, serialized session/inner platform/developer bootstrap, and the minimum flow B → choose a normal BuildMenu item → surface preview → left-click construction → real frames complete it → evacuation processing. After scene load and at least one real frame, assert the public `InputSystemUIInputModule.point`, `leftClick`, `move`, `submit`, and `cancel` references/actions are non-null and each action is enabled; this is the runtime half of Task 10's serialized action contract. The first RED must be a missing PlayMode behavior or scene connection, not an asmdef error. If the public Input System lifecycle does not enable those actions, stop rather than using private-field access.

- [ ] Add real-input tests covering:

```text
all 28 catalog mappings after route/research/resources are injected through the same Editor developer modifier
catalog B open/close with both return states
R rotation and rotated footprint
ground radius 8 and inner 8×6 automatic surface selection
valid green and each independent invalid red reason
compatible mining node highlight
continuous placement and exhausted-material red preview
WASD city/leader movement while legal construction preview remains active
build-mode right click cancels without starting city autopilot
UI pointer does not place or drag camera
middle drag and Home still work outside UI capture
F opens evacuation rather than Packing when ground ownership exists
abandon/full/quick and a mixed single/category/all manifest
full queue pauses at timeScale 0 and resumes
all handled ground instances automatically continue Packing
inner instances follow city and do not enter manifest
```

- [ ] Add the focused UGUI keyboard test. Focus the real search InputField and virtually press/type `W/A/S/D/B/R/1–0/F/F10/Home/Delete/Esc/Enter`. Assert UI owns text/navigation/submit/cancel; city/leader positions, catalog origin, orientation, quickbar selection, deployment mode, camera mode/target and developer panel do not change. First Esc ends editing; only after a real next frame and second Esc may the catalog state machine act. Then clear keyboard focus, send one fresh F10 edge through the same real Update loop, and assert the developer panel changes state exactly once; source and behavior together prove the bootstrap did not also poll F10.

- [ ] Add developer availability behavior in Editor: F10 opens a visibly marked development panel; resource/research/mode/construction buttons mutate the same session models. Do not claim this test proves Release behavior; Release is proved by compile/build contract in Task 12.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform PlayMode \
  -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests \
  -testResults /tmp/wastecity-3d-building/task-11-red.xml \
  -logFile /tmp/wastecity-3d-building/task-11-red.log
```

Expected RED: only newly asserted runtime behavior not yet wired. If immediate `wasPressedThisFrame` diagnostics fail, stop and inspect the three saved InputSettings; do not bypass with direct router/controller calls.

- [ ] If any Task 11 RED exposes a production behavior defect or requires any production-file modification, stop immediately and report the failing assertion, actual runtime chain and exact required path. Revise and approve this plan before touching production; Task 11 authorizes only its PlayMode asmdef and the new PlayMode test/meta paths.

- [ ] Run focused GREEN, then existing `GrayboxRuntimeSceneTests` focused. Confirm TearDown logs show all three InputSettings restored and no virtual devices remain.

- [ ] Run:

```bash
rg -n "ProcessCurrentInput|ProcessFrame|ReadCurrentFrame|TickGameplay|TickMovement|TickDeployment|TickConstruction|TickCamera" \
  Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs
git diff --check
```

Expected: no prohibited direct calls in the new PlayMode test.

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef
git add Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs
git add Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs.meta
git diff --cached --name-only
git commit -m "test: cover graybox building runtime flow"
```

---

## Task 12: Add Development build, performance gates, and complete verification

**Files:**

- Modify: `Assets/_Game/Editor/FormalBuildTools.cs`
- Modify: `Assets/_Game/Editor/GrayboxPerformanceProbe.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs`

- [ ] Add RED build tests requiring:

```csharp
public static void BuildWindowsGraybox3DDevelopment();
```

The method must build only `Assets/_Game/Scenes/GrayboxPrototype3D.unity`, output `Builds/Windows3DDevelopment/WasteCityGrayboxDev.exe`, target `StandaloneWindows64`, and include `BuildOptions.Development`. Existing `BuildWindows`, `BuildWindowsGraybox3D`, and `BuildWindowsLegacy2D` method bodies and outputs remain unchanged.

- [ ] Add RED automated performance tests that create a real session and 128 mixed completed/construction/ruin instances. Assert:

```text
InstanceRendererCount <= instance count
InfrastructureRendererCount <= 8
no 768 per-cell GameObjects
no 28 precreated catalog GameObjects
two warmup ticks
300 unchanged input/session/construction/evacuation adapter ticks
managed allocation difference == 0
```

The 300-call loop may call explicit adapter methods in EditMode for allocation measurement; it must not remove catalog filtering, placement evaluation, construction checks or evacuation state updates to reach zero.

- [ ] Extend `GrayboxPerformanceProbe` RED contract with `MeasureBuildingPerformance()`. It must use a fixed 32×24 seed 8128 world, create/clean 128 mixed instances five times, measure each real generation in milliseconds, count renderers/persistent objects, and write JSON to the absolute path from `WASTECITY_BUILDING_PERF_RESULT`. It must refuse a missing/non-absolute path and never write repository state.

- [ ] Run focused RED:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxBuildAndPerformanceTests \
  -testResults /tmp/wastecity-3d-building/task-12-red.xml \
  -logFile /tmp/wastecity-3d-building/task-12-red.log
```

Expected RED: missing Development build/probe contracts and performance behavior only.

- [ ] Implement the Development build method and performance probe. Do not change default Release options, scene lists or output paths of existing build methods.

- [ ] Run focused GREEN and record renderer/object/allocation values from XML/log.

- [ ] Run the five-sample probe:

```bash
WASTECITY_BUILDING_PERF_RESULT=/tmp/wastecity-3d-building/task-12-building-performance.json \
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.MeasureBuildingPerformance \
  -logFile /tmp/wastecity-3d-building/task-12-building-performance.log
```

Record all five values and median. The median gate is ≤250 ms, infrastructure ≤8 Renderers, and 128 instances ≤128 instance Renderers. If the probe cannot create and clean real state, stop; do not substitute constants.

- [ ] Run complete EditMode:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform EditMode \
  -testResults /tmp/wastecity-3d-building/task-12-editmode.xml \
  -logFile /tmp/wastecity-3d-building/task-12-editmode.log
```

Record the actual total and zero failures. Do not prefill a future count.

- [ ] Run complete PlayMode:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/wastecity-3d-building/task-12-playmode.xml \
  -logFile /tmp/wastecity-3d-building/task-12-playmode.log
```

Record the actual total and zero failures. If the frozen `SwordIntent` WaitForSeconds boundary flakes once with zero relevant diff, rerun the single test and one full suite, report both outcomes, and do not edit frozen combat code/tests.

- [ ] Run headless compile:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -logFile /tmp/wastecity-3d-building/task-12-compile.log
```

- [ ] Build default Release 3D, Development 3D, and legacy 2D after ensuring no GUI Unity lock:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows \
  -logFile /tmp/wastecity-3d-building/task-12-build-release3d.log

/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment \
  -logFile /tmp/wastecity-3d-building/task-12-build-development3d.log

/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D \
  -logFile /tmp/wastecity-3d-building/task-12-build-legacy2d.log

file Builds/Windows/WasteCity.exe
file Builds/Windows3DDevelopment/WasteCityGrayboxDev.exe
file Builds/Windows2D/WasteCity2D.exe
```

All three must be `PE32+ executable (GUI) x86-64`. macOS must not run them and no compatibility layer may be installed. Real Windows 10/11 smoke remains pending.

- [ ] Prove Release/Development separation:

```bash
rg -n "BuildOptions.Development" Assets/_Game/Editor/FormalBuildTools.cs
rg -n "Missing (Script|MonoBehaviour)|The referenced script.*missing" \
  /tmp/wastecity-3d-building/task-12-build-release3d.log \
  /tmp/wastecity-3d-building/task-12-build-development3d.log
```

Expected: Development option occurs only in the new Development method; no Missing Script/MonoBehaviour warnings. Combine this with `ResolveRuntimeAvailability(false,false) == false`, scene serialization inspection, and default build options to prove Release has no modifier UI/command entry. Runtime confirmation on actual Windows remains part of the pending smoke.

- [ ] Collect real GUI Profiler evidence. Launch the exact worktree directly:

```bash
open -na /Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app --args \
  -projectPath /Users/baiyan1/Documents/WasteCity-3d-graybox-foundation
```

Open `GrayboxPrototype3D`, set Game View to Full HD 1920×1080, enter Play, open Profiler Timeline with Deep Profile off, wait at least two frames, then record 300 consecutive frames. Save `.data`, Game View screenshot and Profiler screenshot under `/tmp/wastecity-3d-building/task-12-profiler/`. Record average FPS/frame time and the building input/session/construction/evacuation samples' GC Alloc. Gates: 60 FPS target on the development machine and adapter-owned samples 0 B GC Alloc. If reliable GUI capture is unavailable, stop and report the environment boundary; NUnit allocation results do not replace it.

- [ ] Close GUI Unity normally. If GUI startup generates untracked `ProjectSettings/PackageManagerSettings.asset`, move it to `/tmp/wastecity-3d-building/task-12-profiler/gui-generated-PackageManagerSettings.asset` after closing Unity and confirm the repository path returns to its baseline state. Do not delete or submit it.

- [ ] Run final frozen-range checks:

```bash
git diff --check
git diff --exit-code f6bbed8 -- Assets/_Game/Scenes/FormalPrototype.unity
git diff --exit-code f6bbed8 -- Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs
git diff --exit-code f6bbed8 -- Assets/_Game/Scripts/Persistence
git diff --exit-code f6bbed8 -- Assets/_Game/Scripts/Save
git diff --exit-code f6bbed8 -- Packages
git diff --exit-code f6bbed8 -- ProjectSettings/GraphicsSettings.asset
git diff --exit-code f6bbed8 -- ProjectSettings/QualitySettings.asset
git diff --exit-code f6bbed8 -- ProjectSettings/PackageManagerSettings.asset
rg -n "CurrentSchema|schema" Assets/_Game/Scripts/Persistence Assets/_Game/Scripts/Save
```

If a listed directory is absent, record that fact and use `rg --files` to identify the actual persistence paths before comparing; do not broaden edits.

- [ ] Stage exactly and commit:

```bash
git add Assets/_Game/Editor/FormalBuildTools.cs
git add Assets/_Game/Editor/GrayboxPerformanceProbe.cs
git add Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs
git diff --cached --name-only
git commit -m "build: add graybox building development target"
```

Build products, logs, XML, JSON, Profiler data and screenshots remain untracked/ignored and must not be staged.

---

## Task 13: Record verified facts, prove frozen scope, and push

**Files:**

- Modify only after verification and dispatch approval: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify only after verification and dispatch approval: `Docs/06-User-Feedback-and-Change-Control-ZH.md`

- [ ] Confirm all Task 12 evidence exists and contains actual pass/build/performance values. If any gate is incomplete, do not edit completion facts.

- [ ] Before touching Docs/06, extract the entire `BUG-0001` section from the frozen plan base and current HEAD using the same deterministic section-boundary script already used in prior milestones. Save both files under `/tmp/wastecity-3d-building/task-13/` and record SHA-256.

- [ ] Ask the dispatching thread for the exact allowed `IDEA-0003` implementation-status transition after independent verification. If no explicit transition is supplied, keep `未实现` and only record evidence in wording explicitly approved for this batch. Never infer `开发中` or `已实现` from local test success.

- [ ] Update Docs/05 only with verified facts:

```text
implemented 3D building/menu/dual-grid/construction/evacuation/developer scope
actual EditMode and PlayMode totals
headless compile result
default Release 3D, Development 3D, legacy 2D outputs and PE32+ formats
actual 128-instance structure, five generation samples/median, 300-tick allocation, GUI FPS/frame time
default 3D entry remains
formal save/schema 30 unchanged
production/logistics/combat/outpost/formal evacuation/formal save remain excluded
real Windows 10/11 standalone smoke remains pending
```

- [ ] Update Docs/06 only within IDEA-0003 as explicitly approved: add actual implementation commits and verification evidence, preserve approved product rules and exclusions, and use the dispatcher-approved status. Do not modify IDEA-0001, any other idea/bug, or BUG-0001.

- [ ] Extract current BUG-0001 with the identical script and prove byte identity:

```bash
shasum -a 256 \
  /tmp/wastecity-3d-building/task-13/bug-0001-baseline.txt \
  /tmp/wastecity-3d-building/task-13/bug-0001-current.txt
cmp \
  /tmp/wastecity-3d-building/task-13/bug-0001-baseline.txt \
  /tmp/wastecity-3d-building/task-13/bug-0001-current.txt
```

Any mismatch is a stop gate; do not explain it as semantically equivalent.

- [ ] Scan controlled documents for correct state and scope:

```bash
rg -n "IDEA-0003|BUG-0001|需求明确状态|审批状态|实现状态|Windows 10|Windows 11|schema 30" \
  Docs/05-Formal-Development-Roadmap-ZH.md \
  Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --check
git diff --stat
```

- [ ] Stage exactly and commit:

```bash
git add Docs/05-Formal-Development-Roadmap-ZH.md
git add Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --cached --name-only
git commit -m "docs: record verified 3d building milestone"
```

- [ ] Run final scope proof relative to `f6bbed8`:

```bash
git diff --exit-code f6bbed8 -- Assets/_Game/Scenes/FormalPrototype.unity
git diff --exit-code f6bbed8 -- Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs
git diff --exit-code f6bbed8 -- Assets/_Game/Scripts/Persistence
git diff --exit-code f6bbed8 -- Assets/_Game/Scripts/Save
git diff --exit-code f6bbed8 -- Packages
git diff --exit-code f6bbed8 -- ProjectSettings/GraphicsSettings.asset
git diff --exit-code f6bbed8 -- ProjectSettings/QualitySettings.asset
git diff --exit-code f6bbed8 -- ProjectSettings/PackageManagerSettings.asset
git diff --check f6bbed8..HEAD
git status --short
```

Expected: all protected comparisons exit 0, diff check clean, status empty.

- [ ] Push normally and prove all three SHAs:

```bash
git push origin codex/3d-building-design
git fetch origin codex/3d-building-design
git rev-parse HEAD
git rev-parse origin/codex/3d-building-design
git ls-remote --heads origin codex/3d-building-design
git status --short
```

Local HEAD, tracking ref and `ls-remote` must match. Do not create a PR, merge, force-push, change default entry again, or start production/logistics/combat/evacuation/save follow-up work.

---

## Final Execution Report Checklist

The executor must stop after Task 13 and report:

1. Parent, branch, final HEAD, and relationship to `f6bbed8`.
2. Every commit SHA and its single purpose.
3. Exact added/modified paths and `.meta` handling.
4. RED evidence and expected failure reason for every task.
5. Focused GREEN results and actual test counts.
6. Complete EditMode/PlayMode totals, compile result, and all `/tmp` result/log paths.
7. Default Release 3D, Development 3D and legacy 2D build commands, paths and `file` output.
8. 128-instance renderer/object counts, 300-tick allocation delta, five raw generation values/median, and real 300-frame Profiler/FPS evidence.
9. Release modifier absence proof and explicit note that real Windows 10/11 smoke remains pending.
10. `FormalPrototype`, frozen 2D runtime, persistence/schema, Packages, Graphics/Quality/PackageManager settings and BUG-0001 zero-difference evidence.
11. Controlled document exact changes and IDEA-0003 status authorization.
12. Local/tracking/`ls-remote` equality and raw `git status --short`.
