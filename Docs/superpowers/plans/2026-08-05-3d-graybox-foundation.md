# WasteCity 可操作 3D 灰盒基础实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. 本计划只能串行执行；每一批完成后必须停下并等待调度方验收，不使用子代理执行。

**Goal:** 在冻结的 2D 基线旁新增独立、可回退的 `GrayboxPrototype3D`，用同一二维规则数据完成可操作的世界、移动城市、展开/收起、领袖直接控制与倾斜正交镜头，并提供不覆盖 2D 产物的独立 Windows x86-64 构建。

**Architecture:** 新程序集 `WasteCity.Graybox3D` 单向引用 `WasteCity.Game`，将逻辑二维 `(x,y)` 映射到 Unity XZ；3D MonoBehaviour 只转译输入、物理位置和灰盒表现，通行、速度、A*、展开、直接控制与镜头状态继续以既有纯规则为唯一真值。`FormalPrototype` 和现有 2D 运行时保持不变，URP 只在 3D 场景生命周期内临时接管并按属性独立安全恢复。

**Tech Stack:** Unity `2022.3.62f1`、C#、NUnit/Unity Test Framework、Input System `1.7.0`、URP `14.0.12`（已安装但当前未启用）、Unity 原生 Plane/Cube/Capsule、Windows Mono x86-64。

## 全局约束

- 权威规格：`Docs/superpowers/specs/2026-08-05-3d-graybox-foundation-design.md`，批准提交为 `d3c10ec91b00107e61fd32e7bec7a9dcff2be247`。
- 执行分支：`codex/3d-graybox-foundation`；冻结 2D 比较基线：`e7911800491c75fdc33978982cfd3a52e11ab732`。
- 执行 worktree 固定为 `/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation`。
- 使用 `superpowers:executing-plans` 串行执行；调度方批准下一批之前不得跨批继续。
- 所有功能任务执行真实 TDD：测试先落盘并运行得到预期 RED，再写最小实现，随后运行 GREEN。
- Unity 测试命令不得带 `-quit`；`-executeMethod`、无界面编译和构建命令允许带 `-quit`。
- 每个 Unity 结果文件和日志使用独立 `/tmp/wastecity-3d-graybox-foundation/<task>/` 路径，不复用旧结果。
- 不运行 `FormalProjectSetup.Configure`。场景生成只允许调用新的 `WasteCity.Editor.GrayboxSceneAuthoring.Configure`，且该方法只能写 `GrayboxPrototype3D` 和灰盒渲染资产。
- 不修改 `FormalPrototype.unity`、现有 2D MonoBehaviour、纯规则、稳定 ID、schema 30 或正式存档读写。
- 不修改 `Assets/_Game/Scripts/Building`，不处理 `BUG-0001`，不改变建造菜单。
- 不修改 `Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/PackageManagerSettings.asset`、`ProjectSettings/GraphicsSettings.asset` 或 `ProjectSettings/QualitySettings.asset`。
- 不新增 Unity 包、正式美术、NavMesh、Cinemachine、`.inputactions`、第三人称、高度玩法或默认入口切换。
- 首阶段不得读取或写入 `formal-world.json`，不得给 schema 增加字段。
- 每次提交只暂存当前任务列出的精确路径；禁止 `git add .`、目录外顺带提交和构建产物提交。
- 不预先写入未来测试总数、实现提交 SHA、构建成功结论或性能测量值；所有结果在执行时从实际 XML、日志和产物读取。
- 真实 Windows 10/11 独立程序冒烟始终记录为待补；macOS 上生成 PE 文件和执行 `file` 不能替代它。

## 公共命令约定

以下绝对路径用于全部任务；每个命令块仍写出完整命令，避免执行者依赖隐式 shell 状态：

```text
Unity:
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity

Project:
/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation

Frozen 2D baseline:
e7911800491c75fdc33978982cfd3a52e11ab732
```

测试结果判定：

```bash
xmllint --xpath 'string(/test-run/@total)' "/tmp/result.xml"
xmllint --xpath 'string(/test-run/@passed)' "/tmp/result.xml"
xmllint --xpath 'string(/test-run/@failed)' "/tmp/result.xml"
xmllint --xpath 'string(/test-run/@skipped)' "/tmp/result.xml"
```

每次 Unity 命令返回后还必须检查对应日志；XML 不存在、`failed != 0`、Unity 进程非零退出或日志包含编译错误均视为失败。

## 精确文件地图

### 新增运行时目录与程序集

| 路径 | 唯一职责 | 程序集依赖 | `.meta` 处理 |
|---|---|---|---|
| `Assets/_Game/Scripts/Graybox3D/` | 隔离所有 3D 适配运行时代码 | 无文件级依赖 | Unity 首次导入生成 `Assets/_Game/Scripts/Graybox3D.meta`，随 Task 1 提交 |
| `Assets/_Game/Scripts/Graybox3D/WasteCity.Graybox3D.asmdef` | 定义单向 3D 运行时程序集 | `WasteCity.Game`、`Unity.InputSystem`、`Unity.RenderPipelines.Core.Runtime`、`Unity.RenderPipelines.Universal.Runtime` | Unity 导入生成同名 `.meta`，随 Task 1 提交 |
| `Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs` | 整数格/连续二维平面与 Unity XZ 互转 | `UnityEngine` | 同名 `.meta` 随 Task 1 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxMeshBuilder.cs` | 将同一稳定视觉 ID 的原生几何体实例矩阵合并成单 Mesh | `UnityEngine` | 同名 `.meta` 随 Task 2 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs` | 保存稳定视觉 ID、Renderer 与回退颜色，不持有玩法真值 | `UnityEngine` | 同名 `.meta` 随 Task 2 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs` | 从只读 `WorldMapModel` 生成按稳定 ID 合并的地形/资源/障碍表现 | `WasteCity.Game`、`UnityEngine` | 同名 `.meta` 随 Task 2 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxUrpScope.cs` | 场景进入时设置两处 URP 引用，退出时逐属性条件恢复 | URP Runtime、Core Runtime、`UnityEngine` | 同名 `.meta` 随 Task 3 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs` | 以 seed `8128` 创建 `32×24` 规则世界并启动世界表现 | `WasteCity.Game`、`UnityEngine` | 同名 `.meta` 随 Task 4 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs` | 3D 城市 WASD、A* 路点、规则阻挡/减速和展开模型适配 | `WasteCity.Game`、`UnityEngine` | 同名 `.meta` 随 Task 5 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs` | 既有 `LeaderModel` 的开发招募夹具、平面移动和城市停靠 | `WasteCity.Game`、`UnityEngine` | 同名 `.meta` 随 Task 6 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs` | 只调用 `DirectControlRules.Resolve` 并发布目标变化 | `WasteCity.Game`、`UnityEngine` | 同名 `.meta` 随 Task 6 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs` | 屏幕射线与 `Y=0` 数学平面求交并转换逻辑格 | `UnityEngine` | 同名 `.meta` 随 Task 7 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs` | 将 Keyboard/Mouse 薄输入帧路由到城市、领袖和镜头 | Input System、`UnityEngine` | 同名 `.meta` 随 Task 7 提交 |
| `Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs` | 复用 `CameraFollowModel` 驱动倾斜正交 Following/Free | `WasteCity.Game`、`UnityEngine` | 同名 `.meta` 随 Task 7 提交 |

### 新增场景与渲染资产

| 路径 | 唯一职责 | 程序集依赖 | `.meta` 处理 |
|---|---|---|---|
| `Assets/_Game/Rendering/Graybox3D/` | 仅容纳 3D 灰盒场景作用域渲染资产 | 无 | Unity 生成 `Assets/_Game/Rendering.meta`（若目录尚不存在）和 `Assets/_Game/Rendering/Graybox3D.meta`，随 Task 4 提交 |
| `Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset` | Universal Renderer Data，不使用 2D Renderer | URP | Unity `AssetDatabase` 创建并生成 `.meta`，随 Task 4 提交 |
| `Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset` | 灰盒专用 `UniversalRenderPipelineAsset` | URP | Unity `AssetDatabase` 创建并生成 `.meta`，随 Task 4 提交 |
| `Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat` | 所有灰盒 Renderer 共享的 URP/Lit 基础材质；颜色用 PropertyBlock | URP Shader | Unity `AssetDatabase` 创建并生成 `.meta`，随 Task 4 提交 |
| `Assets/_Game/Scenes/GrayboxPrototype3D.unity` | 独立 3D 场景与序列化引用 | `WasteCity.Graybox3D` | Unity 保存场景并生成 `.meta`，Task 4 初建、Task 8 完成接线 |

### 新增 Editor 工具

| 路径 | 唯一职责 | 程序集依赖 | `.meta` 处理 |
|---|---|---|---|
| `Assets/_Game/Editor/GrayboxSceneAuthoring.cs` | 幂等创建灰盒渲染资产与唯一 3D 场景；拒绝写其他场景 | `WasteCity.Graybox3D`、URP Editor/Runtime、UnityEditor | 同名 `.meta` 随 Task 4 提交 |
| `Assets/_Game/Editor/GrayboxPerformanceProbe.cs` | 在开发机执行五次世界生成并把原始耗时写到 `/tmp` | `WasteCity.Graybox3D`、UnityEditor | 同名 `.meta` 随 Task 9 提交 |

### 新增测试

| 路径 | 覆盖职责 | 测试程序集 | `.meta` 处理 |
|---|---|---|---|
| `Assets/_Game/Tests/EditMode/PlanarCoordinateMapper3DTests.cs` | 四个坐标样例、连续坐标、越界、不钳制、Y 隔离 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 1 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs` | 视觉槽、同 seed 数据、稳定 ID、合并 Renderer/Object 数 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 2 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxUrpScopeTests.cs` | 两属性进入/退出、逐属性外部变更保护、唯一所有权 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 3 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs` | seed/尺寸、初始化前置、无正式存档控制器 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 4 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs` | 场景路径、静态对象树、URP/材质/初始入口契约 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 4 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs` | WASD、A*、阻挡、减速、Y、展开和转换 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 5 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxLeaderControlTests.cs` | 领袖夹具、停靠、通行与 `DirectControlRules` 协调 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 6 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxCameraAndInputTests.cs` | 地面投影、输入路由、倾斜拖动、Home、暂停、分配 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 7 提交 |
| `Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs` | Single 加载正式 3D 场景并验证可操作主循环 | `WasteCity.PlayModeTests` | 同名 `.meta` 随 Task 8 提交 |
| `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs` | 独立构建契约、Renderer/GameObject 和无分配结构门 | `WasteCity.EditModeTests` | 同名 `.meta` 随 Task 9 提交 |

### 修改文件

| 路径 | 修改边界 | 发生任务 |
|---|---|---|
| `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef` | 只增加 `WasteCity.Graybox3D` 引用 | Task 1 |
| `Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef` | 只增加 `WasteCity.Graybox3D` 引用 | Task 8 |
| `Assets/_Game/Editor/WasteCity.Editor.asmdef` | 只增加 `WasteCity.Graybox3D` 引用 | Task 4 |
| `Assets/_Game/Editor/FormalBuildTools.cs` | 保留 `BuildWindows()` 原样，只新增独立 `BuildWindowsGraybox3D()` | Task 9 |
| `ProjectSettings/EditorBuildSettings.asset` | 保持 `FormalPrototype` 第一项启用，增加 `GrayboxPrototype3D` 为第二项启用 | Task 8 |
| `Docs/05-Formal-Development-Roadmap-ZH.md` | 验证全部通过后回写实际里程碑进度与证据 | Task 10 |
| `Docs/06-User-Feedback-and-Change-Control-ZH.md` | 只更新 `IDEA-0001` 的实际进展/证据；`BUG-0001` 字节不变 | Task 10 |

### 首里程碑明确不得修改

`Assets/_Game/Scenes/FormalPrototype.unity`、`Assets/_Game/Scripts/City/PlaceholderMobileCity.cs`、`Assets/_Game/Scripts/Leader/FormalLeaderController.cs`、`Assets/_Game/Scripts/World/FormalCameraController.cs`、`Assets/_Game/Scripts/World/PlaceholderWorldView.cs`、`Assets/_Game/Scripts/Core/FormalGameBootstrap.cs`、`Assets/_Game/Scripts/Persistence/`、`Assets/_Game/Scripts/Building/`、全部既有纯规则文件、`Packages/`、`ProjectSettings/GraphicsSettings.asset`、`ProjectSettings/QualitySettings.asset`、`ProjectSettings/PackageManagerSettings.asset`。

## 冻结接口签名

实现允许增加 `private` 方法和序列化字段，不得改变以下公开测试缝：

```csharp
namespace WasteCity.Graybox3D
{
    public sealed class PlanarCoordinateMapper3D
    {
        public int Width { get; }
        public int Height { get; }
        public PlanarCoordinateMapper3D(int width, int height);
        public bool TryCellToWorld(int cellX, int cellY, float visualY, out Vector3 world);
        public bool TryWorldToCell(Vector3 world, out int cellX, out int cellY);
        public Vector3 PlaneToWorld(Vector2 plane, float visualY);
        public Vector2 WorldToPlane(Vector3 world);
        public bool ContainsCell(int cellX, int cellY);
    }

    public static class GrayboxMeshBuilder
    {
        public static Mesh CombinePrimitive(
            PrimitiveType primitiveType,
            IReadOnlyList<Matrix4x4> instances,
            string meshName);
    }

    public sealed class GrayboxVisualSlot : MonoBehaviour
    {
        public string StableId { get; }
        public MeshRenderer Renderer { get; }
        public Color FallbackColor { get; }
        public void Configure(string stableId, MeshRenderer renderer, Color fallbackColor);
        public void ApplyFallback(Material sharedMaterial);
    }

    public sealed class GrayboxWorldView3D : MonoBehaviour
    {
        public WorldMapModel Model { get; }
        public PlanarCoordinateMapper3D Coordinates { get; }
        public int WorldRendererCount { get; }
        public int PersistentGeneratedObjectCount { get; }
        public void Configure(
            Transform terrainRoot,
            Transform resourceRoot,
            Transform obstacleRoot,
            Material sharedMaterial);
        public void Generate(WorldMapModel model);
        public void ClearGenerated();
        public bool TryWorldToCell(Vector3 world, out int cellX, out int cellY);
    }

    public sealed class GrayboxUrpScope : MonoBehaviour
    {
        public UniversalRenderPipelineAsset PipelineAsset { get; }
        public bool IsApplied { get; }
        public void Configure(UniversalRenderPipelineAsset pipelineAsset);
        public bool Enter();
        public void Exit();
    }

    public sealed class GrayboxSceneBootstrap : MonoBehaviour
    {
        public const int WorldSeedValue = 8128;
        public const int WorldWidth = 32;
        public const int WorldHeight = 24;
        public bool IsInitialized { get; }
        public WorldMapModel World { get; }
        public void Configure(GrayboxUrpScope renderScope, GrayboxWorldView3D worldView);
        public bool Initialize();
    }

    public sealed class GrayboxMobileCityController3D : MonoBehaviour
    {
        public CityDeploymentModel Deployment { get; }
        public CityMode Mode { get; }
        public bool AutopilotActive { get; }
        public WorldGridPoint? Destination { get; }
        public CityDeploymentFailure LastDeploymentFailure { get; }
        public string LastFailureReason { get; }
        public void Configure(GrayboxWorldView3D worldView, Rigidbody body, BoxCollider bodyCollider);
        public void ApplyManualInput(Vector2 input);
        public bool TrySetDestinationCell(int cellX, int cellY, out string failureReason);
        public bool TryToggleDeployment(out string failureReason);
        public void TickMovement(float fixedDeltaTime);
        public void TickDeployment(float deltaTime);
    }

    public sealed class GrayboxLeaderController3D : MonoBehaviour
    {
        public LeaderModel Model { get; }
        public bool DevelopmentFixtureRecruited { get; }
        public void Configure(
            GrayboxWorldView3D worldView,
            GrayboxMobileCityController3D city,
            bool developmentFixtureRecruited);
        public void ApplyManualInput(Vector2 input);
        public void TickControl(DirectControlTarget target, float deltaTime);
        public void SnapToCityDock();
    }

    public sealed class GrayboxDirectControlCoordinator : MonoBehaviour
    {
        public DirectControlTarget ControlTarget { get; }
        public event Action<DirectControlTarget> TargetChanged;
        public void Configure(
            GrayboxMobileCityController3D city,
            GrayboxLeaderController3D leader);
        public bool Refresh();
    }

    public sealed class GrayboxGroundProjector : MonoBehaviour
    {
        public void Configure(Camera camera, PlanarCoordinateMapper3D coordinates);
        public void Configure(Camera camera, GrayboxWorldView3D worldView);
        public bool TryProjectToPlane(Vector2 screenPosition, out Vector3 worldPoint);
        public bool TryProjectToCell(
            Vector2 screenPosition,
            out Vector3 worldPoint,
            out int cellX,
            out int cellY);
    }

    public readonly struct GrayboxInputFrame
    {
        public Vector2 Move { get; }
        public Vector2 PointerPosition { get; }
        public bool ToggleDeploymentPressed { get; }
        public bool DestinationPressed { get; }
        public bool MiddlePressed { get; }
        public bool MiddleHeld { get; }
        public bool MiddleReleased { get; }
        public bool HomePressed { get; }
        public GrayboxInputFrame(
            Vector2 move,
            Vector2 pointerPosition,
            bool toggleDeploymentPressed,
            bool destinationPressed,
            bool middlePressed,
            bool middleHeld,
            bool middleReleased,
            bool homePressed);
    }

    public sealed class GrayboxInputRouter : MonoBehaviour
    {
        public void Configure(
            GrayboxMobileCityController3D city,
            GrayboxLeaderController3D leader,
            GrayboxDirectControlCoordinator directControl,
            GrayboxGroundProjector groundProjector,
            GrayboxCameraController3D cameraController);
        public GrayboxInputFrame ReadCurrentFrame();
        public void ProcessFrame(GrayboxInputFrame frame);
        public void TickGameplay(float deltaTime);
    }

    public sealed class GrayboxCameraController3D : MonoBehaviour
    {
        public CameraFollowMode Mode { get; }
        public DirectControlTarget CurrentTarget { get; }
        public bool ReferencesReady { get; }
        public void Configure(
            Camera camera,
            Transform cameraRig,
            GrayboxMobileCityController3D city,
            GrayboxLeaderController3D leader,
            GrayboxDirectControlCoordinator directControl,
            GrayboxGroundProjector groundProjector);
        public void BeginFreeDrag(Vector2 screenPosition);
        public void ContinueFreeDrag(Vector2 screenPosition);
        public void EndFreeDrag();
        public void ReturnToTarget();
        public void TickCamera();
    }
}

namespace WasteCity.Editor
{
    public static class GrayboxSceneAuthoring
    {
        public const string ScenePath = "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        public const string PipelinePath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset";
        public static void Configure();
    }

    public static class FormalBuildTools
    {
        public static void BuildWindows();
        public static void BuildWindowsGraybox3D();
    }
}
```

`GrayboxSceneAuthoring.Configure()` 是幂等 authoring 工具，不是运行时 bootstrap。它必须先拒绝 `ScenePath` 之外的保存路径，不调用 `FormalProjectSetup.Configure`，不打开或保存 `FormalPrototype`。

---

### Task 1：程序集边界与 `PlanarCoordinateMapper3D`

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/WasteCity.Graybox3D.asmdef`
- Create: `Assets/_Game/Scripts/Graybox3D/WasteCity.Graybox3D.asmdef.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef`
- Create: `Assets/_Game/Tests/EditMode/PlanarCoordinateMapper3DTests.cs`
- Create: `Assets/_Game/Tests/EditMode/PlanarCoordinateMapper3DTests.cs.meta`

- [ ] **Step 1：创建空 3D 程序集边界和失败测试**

先创建 asmdef，暂不创建 `PlanarCoordinateMapper3D.cs`：

```json
{
  "name": "WasteCity.Graybox3D",
  "rootNamespace": "WasteCity.Graybox3D",
  "references": [
    "WasteCity.Game",
    "Unity.InputSystem",
    "Unity.RenderPipelines.Core.Runtime",
    "Unity.RenderPipelines.Universal.Runtime"
  ],
  "autoReferenced": true
}
```

只给 `WasteCity.EditModeTests.asmdef` 的 `references` 增加 `"WasteCity.Graybox3D"`。新增测试至少包含：

```csharp
[Test]
public void CellToWorld_UsesFrozen32By24Contract()
{
    var mapper = new PlanarCoordinateMapper3D(32, 24);
    Assert.That(mapper.TryCellToWorld(0, 0, 0f, out Vector3 a), Is.True);
    Assert.That(a, Is.EqualTo(new Vector3(-16f, 0f, -12f)));
    mapper.TryCellToWorld(8, 7, 0f, out Vector3 b);
    mapper.TryCellToWorld(16, 12, 0f, out Vector3 c);
    mapper.TryCellToWorld(31, 23, 0f, out Vector3 d);
    Assert.That(b, Is.EqualTo(new Vector3(-8f, 0f, -5f)));
    Assert.That(c, Is.EqualTo(Vector3.zero));
    Assert.That(d, Is.EqualTo(new Vector3(15f, 0f, 11f)));
}

[Test]
public void WorldToCell_RejectsOutsideWithoutClamping()
{
    var mapper = new PlanarCoordinateMapper3D(32, 24);
    Assert.That(mapper.TryWorldToCell(new Vector3(-16.01f, 99f, 0f), out _, out _), Is.False);
    Assert.That(mapper.TryWorldToCell(new Vector3(16f, -7f, 0f), out _, out _), Is.False);
    Assert.That(mapper.TryWorldToCell(new Vector3(0f, 3f, 12f), out _, out _), Is.False);
}

[Test]
public void ContinuousPlaneRoundTrip_IgnoresVisualY()
{
    var mapper = new PlanarCoordinateMapper3D(32, 24);
    Vector2 plane = new Vector2(-8.25f, 4.75f);
    Vector3 world = mapper.PlaneToWorld(plane, 6.5f);
    Assert.That(world, Is.EqualTo(new Vector3(-8.25f, 6.5f, 4.75f)));
    Assert.That(mapper.WorldToPlane(world), Is.EqualTo(plane));
}
```

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-01"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.PlanarCoordinateMapper3DTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-01/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-01/red.log"
```

预期 RED：测试程序集仅因 `PlanarCoordinateMapper3D` 类型不存在而编译失败；若是程序集引用、URP 包解析或其他既有错误，停止并报告。

- [ ] **Step 3：写最小坐标实现**

核心实现严格使用：

```csharp
public bool TryCellToWorld(
    int cellX,
    int cellY,
    float visualY,
    out Vector3 world)
{
    if (!ContainsCell(cellX, cellY))
    {
        world = default;
        return false;
    }
    world = new Vector3(
        cellX - Width * .5f,
        visualY,
        cellY - Height * .5f);
    return true;
}

public bool TryWorldToCell(
    Vector3 world,
    out int cellX,
    out int cellY)
{
    cellX = Mathf.FloorToInt(world.x + Width * .5f);
    cellY = Mathf.FloorToInt(world.z + Height * .5f);
    return ContainsCell(cellX, cellY);
}

public Vector3 PlaneToWorld(Vector2 plane, float visualY) =>
    new Vector3(plane.x, visualY, plane.y);

public Vector2 WorldToPlane(Vector3 world) =>
    new Vector2(world.x, world.z);
```

构造器对非正宽高抛出 `ArgumentOutOfRangeException`；不得钳制输入，不得引入高度提供器。

- [ ] **Step 4：运行 GREEN 并验证程序集方向**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.PlanarCoordinateMapper3DTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-01/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-01/green.log"
```

要求 focused tests 全部通过，并用以下检查证明 `WasteCity.Game` 没有反向引用：

```bash
rg -n "WasteCity.Graybox3D" \
  "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation/Assets/_Game/Scripts/WasteCity.Game.asmdef"
```

预期无输出。

- [ ] **Step 5：检查并提交**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Scripts/Graybox3D.meta \
  Assets/_Game/Scripts/Graybox3D/WasteCity.Graybox3D.asmdef \
  Assets/_Game/Scripts/Graybox3D/WasteCity.Graybox3D.asmdef.meta \
  Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs \
  Assets/_Game/Scripts/Graybox3D/PlanarCoordinateMapper3D.cs.meta \
  Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef \
  Assets/_Game/Tests/EditMode/PlanarCoordinateMapper3DTests.cs \
  Assets/_Game/Tests/EditMode/PlanarCoordinateMapper3DTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add graybox 3d coordinate boundary"
```

---

### Task 2：`GrayboxVisualSlot` 与合并网格世界表现

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxMeshBuilder.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxMeshBuilder.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs.meta`

- [ ] **Step 1：写视觉与世界生成失败测试**

测试用 `new WorldMapModel(32, 24, new WorldSeed(8128))`，必须覆盖：

```csharp
[Test]
public void Generate_UsesModelWithoutChangingAnyCell()
{
    WorldMapModel model = new WorldMapModel(32, 24, new WorldSeed(8128));
    WorldCell[,] before = Capture(model);
    GrayboxWorldView3D view = CreateView();
    view.Generate(model);
    Assert.That(view.Model, Is.SameAs(model));
    for (int x = 0; x < model.Width; x++)
    for (int y = 0; y < model.Height; y++)
        AssertCellEqual(before[x, y], model.Get(x, y));
}

[Test]
public void Generate_CombinesByStableIdWithinStructuralBudget()
{
    GrayboxWorldView3D view = CreateView();
    view.Generate(new WorldMapModel(32, 24, new WorldSeed(8128)));
    Assert.That(view.WorldRendererCount, Is.LessThanOrEqualTo(16));
    Assert.That(view.PersistentGeneratedObjectCount, Is.LessThanOrEqualTo(16));
    Assert.That(view.PersistentGeneratedObjectCount, Is.Not.EqualTo(32 * 24));
}

[Test]
public void VisualSlot_AppliesPropertyBlockWithoutInstantiatingMaterial()
{
    GameObject go = NewMeshObject();
    var renderer = go.GetComponent<MeshRenderer>();
    Material shared = CreateSharedUrpOrFallbackMaterial();
    var slot = go.AddComponent<GrayboxVisualSlot>();
    slot.Configure("world.terrain.wasteland", renderer, new Color(.2f, .22f, .18f));
    slot.ApplyFallback(shared);
    Assert.That(slot.StableId, Is.EqualTo("world.terrain.wasteland"));
    Assert.That(renderer.sharedMaterial, Is.SameAs(shared));
    Assert.That(renderer.GetComponent<GrayboxVisualSlot>(), Is.SameAs(slot));
}
```

另加参数化断言，逐一检查 4 个地形 ID、3 个障碍 ID、5 个 `ResourceIds` 映射的稳定 ID、颜色和 PrimitiveType；`Configure` 必须用既有 `WasteCity.Content.StableId` 校验 ID，非法或空白 ID 抛 `ArgumentException`。

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-02"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxVisualAndWorldTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-02/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-02/red.log"
```

预期 RED：仅缺少 `GrayboxMeshBuilder`、`GrayboxVisualSlot` 和 `GrayboxWorldView3D`。

- [ ] **Step 3：实现稳定 ID 分组与合并 Mesh**

`GrayboxWorldView3D.Generate` 只遍历模型一次，将实例矩阵加入固定字典；每个稳定 ID 只创建一个长期 GameObject：

```csharp
private void AddInstance(
    string stableId,
    PrimitiveType primitive,
    Color color,
    Matrix4x4 matrix,
    Transform parent)
{
    if (!groups.TryGetValue(stableId, out Group group))
    {
        group = new Group(stableId, primitive, color, parent);
        groups.Add(stableId, group);
    }
    group.Instances.Add(matrix);
}

private GameObject BuildGroup(Group group)
{
    var go = new GameObject(group.StableId);
    go.transform.SetParent(group.Parent, false);
    var filter = go.AddComponent<MeshFilter>();
    var renderer = go.AddComponent<MeshRenderer>();
    filter.sharedMesh = GrayboxMeshBuilder.CombinePrimitive(
        group.Primitive,
        group.Instances,
        group.StableId);
    var slot = go.AddComponent<GrayboxVisualSlot>();
    slot.Configure(group.StableId, renderer, group.Color);
    slot.ApplyFallback(sharedMaterial);
    generatedMeshes.Add(filter.sharedMesh);
    generatedObjects.Add(go);
    return go;
}
```

`CombinePrimitive` 可以在生成阶段临时创建一个隐藏原生 primitive 取得共享 Mesh，立即销毁临时对象，再用 `CombineInstance[]` 合并；临时对象不得留在层级。Terrain Plane 使用 `.1` 的 X/Z 基准缩放抵消 Unity Plane 的 10 单位尺寸。资源 Capsule 和 Cube 必须按规格映射，不能全部降级为 Cube。

`ClearGenerated` 和 `OnDestroy` 必须销毁运行时 Mesh 与生成对象，清空 MaterialPropertyBlock 引用；不得销毁共享材质或 `WorldMapModel`。

- [ ] **Step 4：运行 GREEN**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxVisualAndWorldTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-02/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-02/green.log"
```

要求所有模型逐格一致断言、稳定 ID 映射和 `Renderer <= 16`、长期生成对象 `<= 16` 全部通过。

- [ ] **Step 5：检查并提交**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Scripts/Graybox3D/GrayboxMeshBuilder.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxMeshBuilder.cs.meta \
  Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs.meta \
  Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add combined graybox world visuals"
```

---

### Task 3：场景作用域 URP 与安全恢复

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxUrpScope.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxUrpScope.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxUrpScopeTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxUrpScopeTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef`

- [ ] **Step 1：写两个属性独立恢复的失败测试**

给 `WasteCity.EditModeTests.asmdef` 增加对 `Unity.RenderPipelines.Universal.Runtime` 的直接引用；asmdef 引用不传递，测试直接使用 `UniversalRenderPipelineAsset` 时不能只依赖 `WasteCity.Graybox3D` 的引用。

每个测试 `SetUp` 记录原 Graphics/Quality 引用，`TearDown` 无条件恢复测试前引用并销毁临时 Asset。覆盖：

```csharp
[Test]
public void Enter_SetsGraphicsAndQualityToGrayboxPipeline()
{
    UniversalRenderPipelineAsset graybox = NewPipeline();
    GrayboxUrpScope scope = NewDisabledScope(graybox);
    Assert.That(scope.Enter(), Is.True);
    Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(graybox));
    Assert.That(QualitySettings.renderPipeline, Is.SameAs(graybox));
}

[Test]
public void Exit_RestoresBothPropertiesWhenStillOwned()
{
    RenderPipelineAsset oldGraphics = GraphicsSettings.defaultRenderPipeline;
    RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
    GrayboxUrpScope scope = NewDisabledScope(NewPipeline());
    scope.Enter();
    scope.Exit();
    Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(oldGraphics));
    Assert.That(QualitySettings.renderPipeline, Is.SameAs(oldQuality));
}

[Test]
public void Exit_PreservesExternalGraphicsChangeAndRestoresOwnedQuality()
{
    RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
    UniversalRenderPipelineAsset graybox = NewPipeline();
    UniversalRenderPipelineAsset external = NewPipeline();
    GrayboxUrpScope scope = NewDisabledScope(graybox);
    scope.Enter();
    GraphicsSettings.defaultRenderPipeline = external;
    scope.Exit();
    Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(external));
    Assert.That(QualitySettings.renderPipeline, Is.SameAs(oldQuality));
}

[Test]
public void Exit_PreservesExternalQualityChangeAndRestoresOwnedGraphics()
{
    RenderPipelineAsset oldGraphics = GraphicsSettings.defaultRenderPipeline;
    UniversalRenderPipelineAsset graybox = NewPipeline();
    UniversalRenderPipelineAsset external = NewPipeline();
    GrayboxUrpScope scope = NewDisabledScope(graybox);
    scope.Enter();
    QualitySettings.renderPipeline = external;
    scope.Exit();
    Assert.That(QualitySettings.renderPipeline, Is.SameAs(external));
    Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(oldGraphics));
}
```

再覆盖：缺失 Asset 时 `Enter == false` 且两属性不变；第二个作用域在第一个仍持有时 `Enter == false`；第一个退出后第二个可进入。

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-03"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxUrpScopeTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-03/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-03/red.log"
```

预期 RED：仅缺少 `GrayboxUrpScope`。

- [ ] **Step 3：实现所有权与逐属性恢复**

类标注 `[DefaultExecutionOrder(-10000)]`。核心逻辑：

```csharp
public bool Enter()
{
    if (pipelineAsset == null || (activeOwner != null && activeOwner != this))
        return false;
    if (IsApplied)
        return true;
    previousGraphics = GraphicsSettings.defaultRenderPipeline;
    previousQuality = QualitySettings.renderPipeline;
    GraphicsSettings.defaultRenderPipeline = pipelineAsset;
    QualitySettings.renderPipeline = pipelineAsset;
    activeOwner = this;
    IsApplied = true;
    return true;
}

public void Exit()
{
    if (!IsApplied)
        return;
    if (QualitySettings.renderPipeline == pipelineAsset)
        QualitySettings.renderPipeline = previousQuality;
    if (GraphicsSettings.defaultRenderPipeline == pipelineAsset)
        GraphicsSettings.defaultRenderPipeline = previousGraphics;
    if (activeOwner == this)
        activeOwner = null;
    IsApplied = false;
    previousGraphics = null;
    previousQuality = null;
}
```

`OnEnable` 调用 `Enter`，`OnDisable` 和 `OnDestroy` 调用幂等 `Exit`。不得写 `GraphicsSettings.asset` 或 `QualitySettings.asset`。

- [ ] **Step 4：运行 GREEN**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxUrpScopeTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-03/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-03/green.log"
```

要求两个外部变更保护测试分别通过，证明不是将两属性作为一个整体恢复。

- [ ] **Step 5：检查并提交**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Scripts/Graybox3D/GrayboxUrpScope.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxUrpScope.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxUrpScopeTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxUrpScopeTests.cs.meta \
  Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef
git diff --cached --name-only
git commit -m "feat: add scene scoped urp ownership"
```

---

### Task 4：Bootstrap、灰盒渲染资产与静态场景契约

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs.meta`
- Create: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Create: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs.meta`
- Modify: `Assets/_Game/Editor/WasteCity.Editor.asmdef`
- Create: `Assets/_Game/Rendering.meta`
- Create: `Assets/_Game/Rendering/Graybox3D.meta`
- Create: `Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset`
- Create: `Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset.meta`
- Create: `Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset`
- Create: `Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset.meta`
- Create: `Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat`
- Create: `Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat.meta`
- Create: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Create: `Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs.meta`

- [ ] **Step 1：先写 bootstrap 与场景契约失败测试**

bootstrap 测试覆盖：必须先有成功进入的 scope；使用精确 seed/尺寸；重复 `Initialize` 不重复生成；缺 URP Asset 或 world view 时失败且不生成。

场景契约测试在 `EditorSceneManager.OpenScene(..., OpenSceneMode.Single)` 后断言：

```csharp
Assert.That(GameObject.Find("GrayboxPrototype3D"), Is.Not.Null);
Assert.That(Object.FindObjectsOfType<GrayboxUrpScope>(true).Length, Is.EqualTo(1));
Assert.That(Object.FindObjectsOfType<GrayboxSceneBootstrap>(true).Length, Is.EqualTo(1));
Assert.That(Object.FindObjectsOfType<GrayboxWorldView3D>(true).Length, Is.EqualTo(1));
Assert.That(GameObject.Find("TerrainRoot"), Is.Not.Null);
Assert.That(GameObject.Find("ResourceRoot"), Is.Not.Null);
Assert.That(GameObject.Find("ObstacleRoot"), Is.Not.Null);
Assert.That(Object.FindObjectsOfType<FormalSaveController>(true), Is.Empty);
Assert.That(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
    "Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset"), Is.Not.Null);
Assert.That(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
    "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset"), Is.Not.Null);
```

另断言 `GrayboxLit.mat` 的 Shader 名为 `Universal Render Pipeline/Lit`，3D 场景中没有 `PlaceholderWorldView` 和 `FormalGameBootstrap`。

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-04"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxSceneBootstrapTests;WasteCity.Tests.GrayboxSceneContractTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-04/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-04/red.log"
```

预期 RED：`GrayboxSceneBootstrap`、灰盒资源和 `GrayboxPrototype3D` 尚不存在。若测试意外打开或保存 `FormalPrototype`，立即停止。

- [ ] **Step 3：实现 bootstrap 与幂等 authoring**

`GrayboxSceneBootstrap` 标注 `[DefaultExecutionOrder(-9000)]`：

```csharp
public bool Initialize()
{
    if (IsInitialized)
        return true;
    if (renderScope == null || worldView == null || !renderScope.IsApplied)
        return false;
    World = new WorldMapModel(
        WorldWidth,
        WorldHeight,
        new WorldSeed(WorldSeedValue));
    worldView.Generate(World);
    IsInitialized = true;
    return true;
}
```

`GrayboxSceneAuthoring.Configure` 必须：

1. 用 `AssetDatabase.CreateAsset` 创建 `UniversalRendererData`。
2. 用 `UniversalRenderPipelineAsset` 和 `SerializedObject` 将 `m_RendererDataList` 的唯一元素设置为该 Universal Renderer Data，并把 `m_DefaultRendererIndex` 设置为 `0`。
3. 用 `Shader.Find("Universal Render Pipeline/Lit")` 创建共享材质；找不到 shader 时抛明确异常并不保存场景。
4. 以 `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` 创建独立场景。
5. 创建 `GrayboxPrototype3D/GrayboxRenderScope`、`GrayboxWorld/{TerrainRoot,ResourceRoot,ObstacleRoot}`、`GrayboxSystems/GrayboxSceneBootstrap`。
6. 序列化配置 `GrayboxUrpScope`、`GrayboxWorldView3D` 和 bootstrap 引用。
7. 只调用 `EditorSceneManager.SaveScene(scene, ScenePath)`。
8. 保存后检查 `scene.path == ScenePath`，不得打开或保存 `FormalPrototype`。

每次运行必须用 `AssetDatabase.LoadAssetAtPath` 先查找固定路径：资产不存在时创建，存在时在原对象上更新；不得删除再创建已经存在的 Asset 或 `.meta`，从而保持 GUID 稳定。场景内容可以用空场景重新构造并覆盖固定 `ScenePath`，但必须保留现有场景 `.meta`。不得使用宽目录递归删除。

- [ ] **Step 4：运行 authoring，然后运行 GREEN**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-04/authoring.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxSceneBootstrapTests;WasteCity.Tests.GrayboxSceneContractTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-04/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-04/green.log"
```

随后验证未改 2D 正式场景和全局管线设置：

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --exit-code e7911800491c75fdc33978982cfd3a52e11ab732 -- \
  Assets/_Game/Scenes/FormalPrototype.unity \
  ProjectSettings/GraphicsSettings.asset \
  ProjectSettings/QualitySettings.asset
```

- [ ] **Step 5：检查并提交**

Unity 若确认仓库原先已有 `Assets/_Game/Rendering.meta`，从暂存命令移除该单项；不得重复生成或覆盖既有 meta。

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs.meta \
  Assets/_Game/Editor/GrayboxSceneAuthoring.cs \
  Assets/_Game/Editor/GrayboxSceneAuthoring.cs.meta \
  Assets/_Game/Editor/WasteCity.Editor.asmdef \
  Assets/_Game/Rendering.meta \
  Assets/_Game/Rendering/Graybox3D.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset \
  Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset \
  Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset.meta \
  Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat \
  Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat.meta \
  Assets/_Game/Scenes/GrayboxPrototype3D.unity \
  Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta \
  Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs.meta
git diff --cached --name-only
git commit -m "feat: author independent graybox 3d scene"
```

---

### Task 5：3D 城市移动、A*、地形规则与展开

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs.meta`

- [ ] **Step 1：写城市适配器失败测试**

使用显式 `WorldCell[,]` 构建小地图，并用 kinematic Rigidbody fixture。至少覆盖：

- `Configure` 创建 `CityDeploymentModel(3f, 5f)`，不读取正式存档。
- WASD 归一化，只改 X/Z，保持既定 Y。
- 手动输入非零时取消已存在的自动驾驶。
- `TrySetDestinationCell` 调用现有四方向 `CityPathfinder`；不可达、越界、深水、悬崖不改变旧路径。
- 自动驾驶用 `0.08f` 容差完成。
- 当前格的 Ruins `.65`、Wetland `.55`、Rocky `.8`、普通 `1` 倍率影响位移。
- Deploying/Fortress/Packing 不移动。
- 展开前调用现有 `CityDeploymentRules.Validate`；OutsideWorld、Blocked、UnstableGround 原枚举和原文字保持。
- 成功 `Mobile -> Deploying`，3 秒后 Fortress；`Fortress -> Packing`，4.99 秒时仍为 Packing，再推进 `.02` 秒后为 Mobile。
- 形状和 BoxCollider 随 Progress 插值，但 Mode 只来自 `CityDeploymentModel`。
- 城市在四种 Mode 下始终保留 `core.city.mobile` 稳定视觉 ID。

EditMode 的 Rigidbody 测试夹具必须在 `SetUp` 保存当前
`Physics.simulationMode` 并切换为 `SimulationMode.Script`，在
`TearDown` 中无条件恢复原值。所有需要观察 `Rigidbody.MovePosition`
实际提交结果的正向移动、阻挡和非 Mobile 测试，在每次
`TickMovement` 后统一调用夹具 helper：先执行
`Physics.SyncTransforms()`，再以固定物理小步
`Physics.Simulate(Time.fixedDeltaTime)`（当前为 `.02` 秒）推进一次。
物理模拟步长不得使用待测 `TickMovement` 的逻辑 delta，且
`Physics.Simulate` 不得进入生产组件。

关键规则唯一性测试使用规则输出作期望：

```csharp
[TestCase(WorldTraversalKind.Ruins, TerrainKind.Wasteland)]
[TestCase(WorldTraversalKind.Open, TerrainKind.Wetland)]
[TestCase(WorldTraversalKind.Open, TerrainKind.Rocky)]
public void TickMovement_UsesExistingTerrainMultiplier(
    WorldTraversalKind traversal,
    TerrainKind terrain)
{
    WorldCell cell = new WorldCell(terrain, null, 0, traversal);
    ControllerFixture fixture = CreateFixture(FilledMap(cell));
    float expected = fixture.Speed * CityTerrainRules.SpeedMultiplier(cell);
    fixture.Controller.ApplyManualInput(Vector2.right);
    fixture.Controller.TickMovement(1f);
    fixture.SimulateFixedStep();
    Assert.That(fixture.Body.position.x - fixture.Start.x,
        Is.EqualTo(expected).Within(.0001f));
    Assert.That(fixture.Body.position.y, Is.EqualTo(fixture.Start.y));
}
```

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-05"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxMobileCityController3DTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-05/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-05/red.log"
```

预期 RED：仅缺少 `GrayboxMobileCityController3D`。

- [ ] **Step 3：实现最小城市适配**

`Configure` 将 Rigidbody 设置为 `isKinematic=true`、`useGravity=false`、`interpolation=Interpolate`，constraints 为 FreezePositionY 与 FreezeRotationX/Y/Z。

移动核心只使用显式 tick，便于测试：

```csharp
public void ApplyManualInput(Vector2 input)
{
    manualInput = Vector2.ClampMagnitude(input, 1f);
    if (manualInput.sqrMagnitude > 0f)
        CancelAutopilot();
}

public void TickMovement(float fixedDeltaTime)
{
    if (Mode != CityMode.Mobile || worldView?.Model == null || body == null)
        return;
    Vector2 direction = manualInput.sqrMagnitude > 0f
        ? manualInput.normalized
        : ResolveAutopilotDirection();
    if (direction.sqrMagnitude == 0f)
        return;
    Vector2 plane = worldView.Coordinates.WorldToPlane(body.position);
    if (!worldView.Coordinates.TryWorldToCell(body.position, out int x, out int y))
        return;
    float multiplier = CityTerrainRules.SpeedMultiplier(worldView.Model.Get(x, y));
    Vector2 candidate = plane + direction * moveSpeed * multiplier *
        Mathf.Max(0f, fixedDeltaTime);
    Vector3 candidateWorld = worldView.Coordinates.PlaneToWorld(candidate, body.position.y);
    if (!worldView.Coordinates.TryWorldToCell(candidateWorld, out int nextX, out int nextY))
        return;
    if (!CityTerrainRules.IsPassable(worldView.Model.Get(nextX, nextY)))
        return;
    body.MovePosition(candidateWorld);
}
```

`TrySetDestinationCell` 必须先找出当前位置格，再调用 `CityPathfinder.TryFindPath`；失败不替换已有路径。自动驾驶终点、路径数组和索引只在 3D 组件内存中保存。`TryToggleDeployment` 先用城市所在格调用 `CityDeploymentRules.Validate(map, x, y)`，成功才调用 `Deployment.Toggle()`。

`Update` 仅在 `Time.timeScale > 0` 时 `TickDeployment(Time.deltaTime)`；`FixedUpdate` 仅在 `Time.timeScale > 0` 时 `TickMovement(Time.fixedDeltaTime)`。组件本身不读 Keyboard/Mouse。

- [ ] **Step 4：运行 GREEN**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxMobileCityController3DTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-05/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-05/green.log"
```

GREEN 必须包含阻挡、四种倍率、A*、3×3 三类失败和两段转换时长，不接受只测平地直行。

- [ ] **Step 5：检查并提交**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs.meta
git diff --cached --name-only
git commit -m "feat: adapt city navigation and deployment to 3d"
```

---

### Task 6：3D 领袖与直接控制协调

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxLeaderControlTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxLeaderControlTests.cs.meta`

- [ ] **Step 1：写领袖与目标协调失败测试**

覆盖冻结表全部八种组合，但期望值必须直接调用 `DirectControlRules.Resolve`：

```csharp
[TestCase(CityMode.Mobile, false)]
[TestCase(CityMode.Mobile, true)]
[TestCase(CityMode.Deploying, false)]
[TestCase(CityMode.Deploying, true)]
[TestCase(CityMode.Fortress, false)]
[TestCase(CityMode.Fortress, true)]
[TestCase(CityMode.Packing, false)]
[TestCase(CityMode.Packing, true)]
public void Refresh_MatchesDirectControlRules(
    CityMode mode,
    bool recruited)
{
    Fixture f = CreateFixture(mode, recruited);
    f.Coordinator.Refresh();
    Assert.That(f.Coordinator.ControlTarget,
        Is.EqualTo(DirectControlRules.Resolve(mode, recruited)));
}
```

另覆盖：

- 开发夹具只调用 `LeaderModel.Restore(true, false, 0, 0, 0)`，不写存档。
- 默认不启用开发夹具时保持未招募。
- Fortress+已招募且 Leader 为目标时 WASD 只移动领袖 X/Z，保持 Y。
- 候选格 DeepWater/Cliff 时拒绝领袖位移。
- 非 Leader 目标时 `SnapToCityDock` 对准城市平面偏移 `(+1.8,+1.2)`。
- `TargetChanged` 只在实际目标变化时触发一次。
- 领袖引用缺失时协调器安全返回 City。

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-06"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxLeaderControlTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-06/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-06/red.log"
```

预期 RED：仅缺少 3D 领袖和协调器类型。

- [ ] **Step 3：实现最小领袖适配和规则协调**

协调器不得写 `mode == Fortress`：

```csharp
public bool Refresh()
{
    DirectControlTarget requested = DirectControlRules.Resolve(
        city?.Deployment?.Mode ?? CityMode.Mobile,
        leader != null && leader.Model.Recruited);
    if (requested == ControlTarget)
        return false;
    ControlTarget = requested;
    TargetChanged?.Invoke(ControlTarget);
    return true;
}
```

领袖 `Configure` 在 `developmentFixtureRecruited` 为 true 时只执行：

```csharp
Model.Restore(
    recruited: true,
    injured: false,
    cooldown: 0f,
    boost: 0f,
    lockout: 0f);
```

领袖移动将当前 Transform X/Z 还原为二维平面，查询目标格的 `CityTerrainRules.IsPassable`，只在 Leader 目标时应用。非 Leader 目标每帧停靠，但不得修改城市或 `LeaderModel`。

- [ ] **Step 4：运行 GREEN**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxLeaderControlTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-06/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-06/green.log"
```

要求八种目标组合、缺引用回退、停靠偏移和通行阻挡全部通过。

- [ ] **Step 5：检查并提交**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs.meta \
  Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxLeaderControlTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxLeaderControlTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add graybox leader direct control"
```

---

### Task 7：地面投影、输入路由与倾斜正交镜头

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxCameraAndInputTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxCameraAndInputTests.cs.meta`

- [ ] **Step 1：写投影、输入和镜头失败测试**

创建真实倾斜正交 Camera fixture：子相机 localPosition `(0,18,-14)`、localEulerAngles `(52,0,0)`、orthographicSize `13`。覆盖：

- 屏幕中心射线与 `Y=0` 平面相交；平行、背向和地图外失败；不需要 Collider。
- City 目标时 WASD 路由到城市，Leader 目标时路由到领袖。
- F 始终路由到城市；右键只在 Mobile 且投影成功时设置目的地。
- 中键按下进入 Free；后续平面命中差使用 `previous-current`；松开仍 Free。
- Home 同一 `ProcessFrame` 返回 Following 并对准目标。
- 目标变化无论此前是否 Free 都同帧恢复 Following。
- 缺领袖引用回退城市；缺城市引用保持当前 rig 位置，不回原点。
- CameraRig 只改 X/Z，保持 Y；相机局部位置、旋转、orthographicSize 不变。
- `Time.timeScale = 0` 时中键和 Home 仍工作；城市/领袖玩法 tick 不由镜头调用。
- Fortress+已招募时，先由 `ProcessFrame` 路由 WASD，再调用
  `TickGameplay(.1f)`，领袖移动且城市不动。
- City 目标时 `TickGameplay` 使领袖停靠，但不推进城市；暂停时即使
  领袖此前已有非零输入，也不移动或停靠。
- `ProcessFrame`、`TickGameplay` 和 `TickCamera` 在两次预热后连续
  300 次不分配托管内存。

反向拖动断言使用真实平面投影：

```csharp
Vector2 start = new Vector2(640f, 360f);
Vector2 end = new Vector2(740f, 410f);
Assert.That(projector.TryProjectToPlane(start, out Vector3 previous), Is.True);
Assert.That(projector.TryProjectToPlane(end, out Vector3 current), Is.True);
Vector3 before = rig.position;
cameraController.BeginFreeDrag(start);
cameraController.ContinueFreeDrag(end);
Vector3 expected = before + new Vector3(
    previous.x - current.x,
    0f,
    previous.z - current.z);
Assert.That(rig.position, Is.EqualTo(expected).Using(Vector3ComparerWithEqualsOperator.Instance));
```

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-07"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxCameraAndInputTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-07/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-07/red.log"
```

预期 RED：仅缺少 projector、input router 和 camera controller。

- [ ] **Step 3：实现数学平面投影、值类型输入帧和镜头状态**

投影核心：

```csharp
public bool TryProjectToPlane(Vector2 screenPosition, out Vector3 worldPoint)
{
    worldPoint = default;
    if (camera == null)
        return false;
    Ray ray = camera.ScreenPointToRay(screenPosition);
    var plane = new Plane(Vector3.up, Vector3.zero);
    if (!plane.Raycast(ray, out float distance) || distance < 0f)
        return false;
    worldPoint = ray.GetPoint(distance);
    worldPoint.y = 0f;
    return true;
}
```

输入路由 `Update` 先构造并处理 `GrayboxInputFrame`，再推进领袖：

```csharp
Vector2 move = new Vector2(
    (Keyboard.current?.dKey.isPressed == true ? 1f : 0f) -
    (Keyboard.current?.aKey.isPressed == true ? 1f : 0f),
    (Keyboard.current?.wKey.isPressed == true ? 1f : 0f) -
    (Keyboard.current?.sKey.isPressed == true ? 1f : 0f));
```

`ProcessFrame` 不使用 delta time。它先处理中键与 Home，使镜头在 `Time.timeScale == 0` 时仍可操作；只有 `Time.timeScale > 0` 时才处理 WASD、F 和右键，确保暂停不会改变玩法位置、路径或部署 Mode。F 调用城市 `TryToggleDeployment`；右键先投影再调用 `TrySetDestinationCell`；WASD 按协调器当前目标路由。

`Update()` 的固定顺序为：

```csharp
ProcessFrame(ReadCurrentFrame());
TickGameplay(Time.deltaTime);
```

`TickGameplay(float deltaTime)` 在 `Time.timeScale <= 0` 或领袖引用缺失
时直接返回；否则刷新 `directControl`，以其当前目标（协调器缺失时为
City）调用 `leader.TickControl(target, Mathf.Max(0f, deltaTime))`。
该方法不得调用城市的 `TickMovement` 或 `TickDeployment`；城市继续由
自身的 `Update`/`FixedUpdate` 驱动，镜头也不得调用任何玩法 tick。

镜头内部持有既有 `CameraFollowModel`。`TickCamera` 先调用 `directControl.Refresh()`，再：

```csharp
bool targetChanged = followModel.ObserveTarget(
    directControl.ControlTarget,
    leader != null && leader.Model.Recruited);
if (targetChanged || followModel.Mode == CameraFollowMode.Following)
    SnapRigToEffectiveTarget();
```

`BeginFreeDrag` 先记录第一次有效平面命中再 `BeginFreeDrag()`；`ContinueFreeDrag` 只在有前一命中时应用 `previous-current`；`EndFreeDrag` 调用空吸回语义的 `CameraFollowModel.EndFreeDrag()`；`ReturnToTarget` 调用模型后立即 `SnapRigToEffectiveTarget()`。这些方法不依赖 scaled/unscaled delta。

分配测试使用 `GC.GetAllocatedBytesForCurrentThread()`，先调用两次预热，
再在同线程连续调用 `ProcessFrame`、`TickGameplay` 和 `TickCamera`
300 次，只接受差值 `0`；测试输入、数组、delegate 和 comparer 在计量前
创建，避免把测试夹具分配归因给适配器。

- [ ] **Step 4：运行 GREEN**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxCameraAndInputTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-07/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-07/green.log"
```

GREEN 必须包括领袖运行时推进、City 目标停靠、暂停不推进、反向拖动、
释放保持、Home、目标变化和包含 `TickGameplay` 的 300 次适配器调用
零分配。

- [ ] **Step 5：检查并提交**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs.meta \
  Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs.meta \
  Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxCameraController3D.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxCameraAndInputTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxCameraAndInputTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add graybox input and camera adapters"
```

---

### Task 8：正式 3D 场景接线与 PlayMode 可操作主循环

**Files:**

- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Modify: `Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef`
- Create: `Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs`
- Create: `Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`

- [ ] **Step 1：先写正式场景失败测试**

给 PlayMode asmdef 增加直接引用 `WasteCity.Graybox3D`、
`Unity.InputSystem` 和 `Unity.RenderPipelines.Universal.Runtime`；测试不得依赖
程序集引用传递。PlayMode fixture 每次：

```csharp
yield return SceneManager.LoadSceneAsync(
    "GrayboxPrototype3D",
    LoadSceneMode.Single);
yield return null;
```

batchmode/headless 输入验证使用明确的 manual update 边界：

- Unity Editor 无 Game View 焦点时，无参 `InputSystem.Update()` 的默认
  update type 会落到 Editor buffer；Editor buffer 翻转不更新设备的 player
  update step，因此 `isPressed` 可以为 true 而 `wasPressedThisFrame` 为
  false。fixture 必须用公开设置把 headless 输入路由到 player/manual update，
  不得修改生产输入链。
- SetUp 先保存 `InputSystem.settings.updateMode`、
  `backgroundBehavior` 和 `editorInputBehaviorInPlayMode`，再依次设置
  `ProcessEventsManually`、`BackgroundBehavior.IgnoreFocus` 和
  `EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView`；完成后
  创建虚拟 Keyboard/Mouse 并分别调用 `MakeCurrent()`。不得依赖自动
  PlayerLoop 输入更新或 `InputSystem.onAfterUpdate` 维持 current。
- 每个输入 helper 严格执行 `QueueStateEvent` →
  `InputSystem.Update()` → 在 yield 前立即断言 `Keyboard.current` /
  `Mouse.current` 为对应虚拟设备，并断言目标 key/button 的
  `isPressed`、`wasPressedThisFrame` 等所需状态可见 → `yield return null`，
  让真实 `GrayboxInputRouter.Update` 消费该输入。
- WASD 按下事件只手动处理一次，跨真实 `FixedUpdate` 保持按下；释放时再
  排队空状态、调用 `InputSystem.Update()` 并 yield，不得逐帧调用玩法方法。
- TearDown 使用 `try/finally`：恢复 `Time.timeScale = 1f`、卸载灰盒并加载
  空测试场景；finally 无条件移除仅由 fixture 创建的设备，并恢复原
  `updateMode`、`editorInputBehaviorInPlayMode` 和 `backgroundBehavior`。
  测试必须断言正常清理后的三项值，finally 结构必须保证异常路径也逐项恢复，
  且不得把运行时测试设置写入项目资产。
- 测试不得直接调用 `ProcessFrame`、`ReadCurrentFrame`、`TickGameplay`、
  城市/领袖 tick 或相机控制方法来绕过运行时主循环。

覆盖：

1. 对象树和全部序列化引用完整；恰好一个启用 scope、Main Camera、城市控制器、协调器。
2. URP 生效且 renderer data 是 Universal Renderer。
3. `32×24`、seed `8128` 世界生成，核心对象无 `SpriteRenderer`、`Rigidbody2D`、`Collider2D`。
4. Mobile 城市 WASD XZ 移动且 Y 不变。
5. 可达右键目的地沿 A* 接近终点；非法目标不改变现有路径。
6. 非法展开不变；合法位置完成 Deploying→Fortress。
7. Fortress+开发招募夹具后 WASD 只移动领袖。
8. Packing 完成后直接控制恢复 City。
9. Following、Free、释放保持、Home、控制目标切换恢复。
10. `timeScale == 0` 时玩法停止、镜头仍可拖动和返回。
11. 镜头操作前后城市 Autopilot、Mode、Remaining 不变。
12. 卸载 3D 场景后 Graphics/Quality 两属性按各自所有权恢复。
13. 真正重新加载场景后，ground projector 能通过序列化的 world view
    完成屏幕点到地图格投影；不得依赖只存在于 authoring 进程内的 mapper。
14. 真正重新加载场景后，领袖的开发夹具开关和 `Model.Recruited` 均为
    true。

EditMode 场景契约扩展断言：

```csharp
Camera camera = Camera.main;
Assert.That(camera, Is.Not.Null);
Assert.That(camera.orthographic, Is.True);
Assert.That(camera.orthographicSize, Is.EqualTo(13f));
Assert.That(camera.transform.localPosition, Is.EqualTo(new Vector3(0f, 18f, -14f)));
Assert.That(camera.transform.localEulerAngles.x, Is.EqualTo(52f).Within(.01f));
Assert.That(Object.FindObjectsOfType<GrayboxMobileCityController3D>(true).Length, Is.EqualTo(1));
Assert.That(Object.FindObjectsOfType<GrayboxDirectControlCoordinator>(true).Length, Is.EqualTo(1));
```

- [ ] **Step 2：运行 focused EditMode 和 PlayMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-08"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxSceneContractTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-08/red-edit.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-08/red-edit.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform PlayMode \
  -testFilter "WasteCity.Tests.GrayboxRuntimeSceneTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-08/red-play.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-08/red-play.log"
```

预期 RED：现有初始 3D 场景尚未接入城市、领袖、输入、相机和可重载的
projector/领袖夹具序列化引用；不接受程序集错误或 2D 场景测试失败。

- [ ] **Step 3：扩展 authoring 并生成最终首阶段场景**

先修复两个运行时序列化边界：

- `GrayboxGroundProjector` 序列化 `GrayboxWorldView3D worldView` 引用，并保留
  非序列化的注入 mapper。运行时坐标源严格使用
  `injectedCoordinates ?? worldView?.Coordinates`。mapper overload 仅供纯测试
  注入；新增的 world view overload 供 authoring 使用并持久化场景引用。
- `GrayboxLeaderController3D` 使用
  `[SerializeField] bool developmentFixtureRecruited` 持久化开发夹具开关；
  `Awake` 应用夹具，`Configure` 设置开关后在 authoring 当次也应用。启用时
  只调用既有 `Model.Restore(true, false, 0, 0, 0)`，不得读写正式存档。

`GrayboxSceneAuthoring.Configure` 增加：

- `CameraRig/Main Camera`：rig 初始 XZ 对准城市；相机 local `(0,18,-14)`、Euler `(52,0,0)`、Orthographic、size `13`、tag `MainCamera`。
- `GrayboxActors/MobileCity`：Cube Mesh、共享材质、`GrayboxVisualSlot("core.city.mobile")`、kinematic Rigidbody、BoxCollider、城市控制器；底面 Y=0。
- `GrayboxActors/Leader_CenJin`：Capsule Mesh、共享材质、`GrayboxVisualSlot("core.character.cen-jin")`、CapsuleCollider、领袖控制器，开发夹具开关为 true。
- `GrayboxSystems`：bootstrap、input router、direct coordinator、ground projector。
- 所有 `Configure` 引用完整序列化；ground projector 必须调用 world view
  overload，不得把 `worldView.Coordinates` 作为 authoring 时瞬态对象传入。
- 场景中不增加 FormalSaveController、NavMeshSurface、Cinemachine、2D 物理或 SpriteRenderer。

更新 `EditorBuildSettings.asset` 后场景顺序必须是：

```text
0 enabled Assets/_Game/Scenes/FormalPrototype.unity
1 enabled Assets/_Game/Scenes/GrayboxPrototype3D.unity
```

运行 authoring：

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-08/authoring.log"
```

- [ ] **Step 4：运行 focused GREEN**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxSceneContractTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-08/green-edit.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-08/green-edit.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform PlayMode \
  -testFilter "WasteCity.Tests.GrayboxRuntimeSceneTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-08/green-play.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-08/green-play.log"
```

PlayMode 测试结束必须在 `TearDown` 恢复 `Time.timeScale = 1f` 并加载空测试场景，避免 URP scope 或全局时间污染既有测试。

- [ ] **Step 5：检查并提交**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Editor/GrayboxSceneAuthoring.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxGroundProjector.cs \
  Assets/_Game/Scripts/Graybox3D/GrayboxLeaderController3D.cs \
  Assets/_Game/Scenes/GrayboxPrototype3D.unity \
  ProjectSettings/EditorBuildSettings.asset \
  Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef \
  Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs \
  Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs
git diff --cached --name-only
git commit -m "feat: wire playable graybox 3d scene"
```

---

### Task 9：独立 Windows 构建、性能门与完整回归

**Files:**

- Modify: `Assets/_Game/Editor/FormalBuildTools.cs`
- Create: `Assets/_Game/Editor/GrayboxPerformanceProbe.cs`
- Create: `Assets/_Game/Editor/GrayboxPerformanceProbe.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs.meta`

- [ ] **Step 1：写构建与稳定性能结构失败测试**

构建契约测试用反射要求存在 `FormalBuildTools.BuildWindowsGraybox3D`，并用源码/常量契约断言：

- `BuildWindows()` 仍只含 `FormalPrototype`，输出 `Builds/Windows/WasteCity.exe`。
- 新方法只含 `GrayboxPrototype3D`，输出 `Builds/Windows3D/WasteCityGraybox.exe`。
- 两者 target 都是 `StandaloneWindows64`。

性能自动化测试覆盖适合稳定断言的项目：

```csharp
[Test]
public void GeneratedWorld_StaysWithinStructuralBudgets()
{
    GrayboxWorldView3D view = CreateAndGenerate();
    Assert.That(view.WorldRendererCount, Is.LessThanOrEqualTo(16));
    Assert.That(view.PersistentGeneratedObjectCount, Is.LessThanOrEqualTo(16));
    Assert.That(GameObject.FindObjectsOfType<MeshRenderer>().Length,
        Is.LessThan(768));
}

[Test]
public void AdapterTicks_AllocateNoManagedBytesAcross300Calls()
{
    AdapterFixture f = CreateAdapterFixture();
    f.TickAll();
    f.TickAll();
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int frame = 0; frame < 300; frame++)
        f.TickAll();
    long after = GC.GetAllocatedBytesForCurrentThread();
    Assert.That(after - before, Is.Zero);
}
```

`TickAll` 只调用城市、领袖、协调器、input frame 处理与镜头的显式方法，不调用 test runner yield，不在计量区创建输入帧、字符串、集合或 delegate。

- [ ] **Step 2：运行 focused EditMode，确认 RED**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/task-09"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxBuildAndPerformanceTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-09/red.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-09/red.log"
```

预期 RED：构建方法和 performance probe 尚不存在；若结构或分配断言同时失败，记录实际差值，在本任务内只优化新 3D 适配层，不修改规则或 2D 运行时。

- [ ] **Step 3：实现独立构建方法和开发机生成测量器**

保留现有 `BuildWindows()` 方法字节内容不变，在同类新增：

```csharp
public static void BuildWindowsGraybox3D()
{
    Directory.CreateDirectory("Builds/Windows3D");
    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
    {
        scenes = new[] { "Assets/_Game/Scenes/GrayboxPrototype3D.unity" },
        locationPathName = "Builds/Windows3D/WasteCityGraybox.exe",
        target = BuildTarget.StandaloneWindows64
    });
    if (report.summary.result != BuildResult.Succeeded)
        throw new InvalidOperationException(report.summary.result.ToString());
}
```

`GrayboxPerformanceProbe.MeasureWorldGeneration()` 固定 seed/尺寸，连续生成和清理 5 次，用 `Stopwatch.GetTimestamp()` 记录每次毫秒，排序后取第三个值，并把以下 JSON 写到环境变量 `WASTECITY_GRAYBOX_PERF_RESULT` 指定的绝对 `/tmp` 文件：

```json
{
  "seed": 8128,
  "width": 32,
  "height": 24,
  "generationMilliseconds": [0, 0, 0, 0, 0],
  "medianMilliseconds": 0,
  "rendererCount": 0,
  "persistentGeneratedObjectCount": 0
}
```

上例中的零只是 JSON 字段形状，运行时必须写实际观测值。测量器不把机器阈值写成 NUnit 断言；执行者根据真实 `medianMilliseconds <= 250` 判定开发机验收。

若自动 300 调用分配门失败，只允许：

- 缓存路径/列表/PropertyBlock；
- 避免 LINQ、闭包、字符串拼接和每帧数组；
- 把 Mesh/Material 生成移出 Update/LateUpdate；
- 移除每帧 `FindObjectOfType`/`FindObjectsOfType`。

不得降低规则更新频率或跳过规则查询来通过性能门。

- [ ] **Step 4：运行 focused GREEN 与开发机性能测量**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.GrayboxBuildAndPerformanceTests" \
  -testResults "/tmp/wastecity-3d-graybox-foundation/task-09/green.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-09/green.log"

WASTECITY_GRAYBOX_PERF_RESULT="/tmp/wastecity-3d-graybox-foundation/task-09/generation.json" \
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.MeasureWorldGeneration \
  -logFile "/tmp/wastecity-3d-graybox-foundation/task-09/generation.log"

cat "/tmp/wastecity-3d-graybox-foundation/task-09/generation.json"
```

自动化门：核心世界 Renderer `<=16`、长期世界生成对象 `<=16`、无 768 格对象、适配器显式 tick 预热两次后 300 次托管分配差值为 0。

开发机测量门：

1. 五次生成中位数从 `generation.json` 读取，要求 `<=250 ms`。
2. 在 Unity Game View 固定 1920×1080、Development 配置加载 `GrayboxPrototype3D`，Profiler 关闭 Deep Profile，预热 2 帧后记录连续 300 帧 Timeline；Adapter 自有样本不得出现 GC Alloc。
3. 同一 300 帧窗口记录平均 FPS/帧时，目标稳定 60 FPS。
4. 将 Profiler `.data`、Game View 分辨率截图和文字读数存放到 `/tmp/wastecity-3d-graybox-foundation/task-09/`，不提交仓库。

生成中位数与 1920×1080 FPS 依赖开发机和 Editor/Profiler 环境，因此作为人工读取的本机门；不写成跨机器 NUnit 断言。300 帧真实 Profiler 门也不由 Unity Test Runner 自身的 GC 噪声代替；自动 300 次显式调用断言只提供稳定的代码级无分配保护。

- [ ] **Step 5：运行完整 EditMode 与 PlayMode**

```bash
mkdir -p "/tmp/wastecity-3d-graybox-foundation/regression"
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform EditMode \
  -testResults "/tmp/wastecity-3d-graybox-foundation/regression/editmode.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/regression/editmode.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -runTests -testPlatform PlayMode \
  -testResults "/tmp/wastecity-3d-graybox-foundation/regression/playmode.xml" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/regression/playmode.log"

xmllint --xpath 'string(/test-run/@total)' \
  "/tmp/wastecity-3d-graybox-foundation/regression/editmode.xml"
xmllint --xpath 'string(/test-run/@passed)' \
  "/tmp/wastecity-3d-graybox-foundation/regression/editmode.xml"
xmllint --xpath 'string(/test-run/@failed)' \
  "/tmp/wastecity-3d-graybox-foundation/regression/editmode.xml"
xmllint --xpath 'string(/test-run/@total)' \
  "/tmp/wastecity-3d-graybox-foundation/regression/playmode.xml"
xmllint --xpath 'string(/test-run/@passed)' \
  "/tmp/wastecity-3d-graybox-foundation/regression/playmode.xml"
xmllint --xpath 'string(/test-run/@failed)' \
  "/tmp/wastecity-3d-graybox-foundation/regression/playmode.xml"
```

要求原有 391 个 EditMode 和 50 个 PlayMode 连同所有新增测试均通过；实际新增数量和总数只从本次 XML 读取。

- [ ] **Step 6：无界面编译、两套 Windows 构建与格式检查**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -logFile "/tmp/wastecity-3d-graybox-foundation/regression/compile.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows \
  -logFile "/tmp/wastecity-3d-graybox-foundation/regression/build-windows-2d.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3D \
  -logFile "/tmp/wastecity-3d-graybox-foundation/regression/build-windows-3d.log"

file \
  "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation/Builds/Windows/WasteCity.exe"
file \
  "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation/Builds/Windows3D/WasteCityGraybox.exe"
```

两条 `file` 都必须报告 PE32+ Windows x86-64 GUI 可执行格式。真实 Windows 运行冒烟仍写“待补”，不得在 macOS 启动 `.exe` 或安装兼容层。

- [ ] **Step 7：检查并提交 Task 9 文件**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git diff --check
git add \
  Assets/_Game/Editor/FormalBuildTools.cs \
  Assets/_Game/Editor/GrayboxPerformanceProbe.cs \
  Assets/_Game/Editor/GrayboxPerformanceProbe.cs.meta \
  Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs \
  Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs.meta
git diff --cached --name-only
git commit -m "build: add independent graybox windows target"
```

构建目录、`Library`、`Logs`、`UserSettings` 和 `/tmp` 证据不得暂存。

---

### Task 10：范围零差异证明、受控文档回写与 GitHub 推送

**Files:**

- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`

- [ ] **Step 1：在文档修改前执行完整范围保护**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
BASE="e7911800491c75fdc33978982cfd3a52e11ab732"

git diff --exit-code "$BASE"..HEAD -- \
  Assets/_Game/Scenes/FormalPrototype.unity

git diff --exit-code "$BASE"..HEAD -- \
  Assets/_Game/Scripts/City/PlaceholderMobileCity.cs \
  Assets/_Game/Scripts/Leader/FormalLeaderController.cs \
  Assets/_Game/Scripts/World/FormalCameraController.cs \
  Assets/_Game/Scripts/World/PlaceholderWorldView.cs \
  Assets/_Game/Scripts/Core/FormalGameBootstrap.cs

git diff --exit-code "$BASE"..HEAD -- \
  Assets/_Game/Scripts/City/CityDeploymentModel.cs \
  Assets/_Game/Scripts/City/CityDeploymentRules.cs \
  Assets/_Game/Scripts/City/CityOperationalRules.cs \
  Assets/_Game/Scripts/City/CityPathfinder.cs \
  Assets/_Game/Scripts/City/CityTerrainRules.cs \
  Assets/_Game/Scripts/City/DirectControlRules.cs \
  Assets/_Game/Scripts/World/WorldMapModel.cs \
  Assets/_Game/Scripts/World/WorldSeed.cs \
  Assets/_Game/Scripts/World/CameraFollowModel.cs \
  Assets/_Game/Scripts/Leader/LeaderModel.cs

EXISTING_SCRIPT_DIFFS="$(
  git diff --name-only "$BASE"..HEAD -- Assets/_Game/Scripts |
    rg -v '^Assets/_Game/Scripts/Graybox3D(\.meta|/)' || true
)"
test -z "$EXISTING_SCRIPT_DIFFS"

git diff --exit-code "$BASE"..HEAD -- \
  Assets/_Game/Scripts/Persistence

git diff --exit-code "$BASE"..HEAD -- \
  Assets/_Game/Scripts/Building

git diff --exit-code "$BASE"..HEAD -- \
  Packages \
  ProjectSettings/PackageManagerSettings.asset \
  ProjectSettings/GraphicsSettings.asset \
  ProjectSettings/QualitySettings.asset
```

所有命令必须零输出、退出码 0。任一失败都停止，定位越界提交并向调度方报告；不得用 reset、覆盖或修改受保护文件来掩盖差异。

在修改 Docs/06 前保存 `BUG-0001` 基线段：

```bash
awk '
  /^### BUG-0001/ { capture=1 }
  capture && /^### / && $0 !~ /^### BUG-0001/ { exit }
  capture { print }
' <(git show e7911800491c75fdc33978982cfd3a52e11ab732:Docs/06-User-Feedback-and-Change-Control-ZH.md) \
  > "/tmp/wastecity-3d-graybox-foundation/regression/bug-0001-baseline.txt"

shasum -a 256 \
  "/tmp/wastecity-3d-graybox-foundation/regression/bug-0001-baseline.txt"
```

- [ ] **Step 2：只用实际证据回写 Docs/05 和 IDEA-0001**

只有 Task 9 的完整 EditMode、PlayMode、编译、两个 Windows 构建、两个 `file` 和全部范围保护通过后才可修改文档。

`Docs/05` 写入：

- 首个“可操作 3D 基础”实际完成范围；
- 从 XML 读取的实际 EditMode/PlayMode 总数和通过数；
- 两套构建的真实产物路径与格式；
- 性能自动化门和开发机实测的真实数据；
- 2D 默认入口仍保留，3D 默认入口切换是下一独立变更；
- 真实 Windows 10/11 冒烟待补。

`Docs/06` 只更新 `IDEA-0001`：

- 状态继续保持 `已明确 / 已批准 / 开发中`；
- 加入实际实现提交范围、实际验证数据和独立 3D 构建证据；
- 说明未实现建造/生产/战斗/撤离/存档以及下一里程碑；
- 不把 macOS 构建写成真实 Windows 冒烟。

不得触碰 `BUG-0001` 标题、正文或三状态。

- [ ] **Step 3：验证 BUG 段字节一致并检查文档差异**

```bash
awk '
  /^### BUG-0001/ { capture=1 }
  capture && /^### / && $0 !~ /^### BUG-0001/ { exit }
  capture { print }
' Docs/06-User-Feedback-and-Change-Control-ZH.md \
  > "/tmp/wastecity-3d-graybox-foundation/regression/bug-0001-current.txt"

cmp \
  "/tmp/wastecity-3d-graybox-foundation/regression/bug-0001-baseline.txt" \
  "/tmp/wastecity-3d-graybox-foundation/regression/bug-0001-current.txt"

rg -n \
  "需求明确状态：待澄清|审批状态：待确认|实现状态：未实现" \
  Docs/06-User-Feedback-and-Change-Control-ZH.md

git diff --check
git diff -- Docs/05-Formal-Development-Roadmap-ZH.md \
  Docs/06-User-Feedback-and-Change-Control-ZH.md
```

`cmp` 必须无输出且退出码 0；状态检查必须只证明原状态仍存在，不以搜索结果替代字节比较。

- [ ] **Step 4：提交受控文档**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git add \
  Docs/05-Formal-Development-Roadmap-ZH.md \
  Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --cached --name-only
git commit -m "docs: record verified graybox 3d foundation"
```

- [ ] **Step 5：在推送前重新执行最终零差异与状态检查**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
BASE="e7911800491c75fdc33978982cfd3a52e11ab732"

git diff --exit-code "$BASE"..HEAD -- \
  Assets/_Game/Scenes/FormalPrototype.unity \
  Assets/_Game/Scripts/Building \
  Assets/_Game/Scripts/Persistence \
  Packages \
  ProjectSettings/PackageManagerSettings.asset \
  ProjectSettings/GraphicsSettings.asset \
  ProjectSettings/QualitySettings.asset

git diff --exit-code "$BASE"..HEAD -- \
  Assets/_Game/Scripts/City/PlaceholderMobileCity.cs \
  Assets/_Game/Scripts/Leader/FormalLeaderController.cs \
  Assets/_Game/Scripts/World/FormalCameraController.cs \
  Assets/_Game/Scripts/World/PlaceholderWorldView.cs \
  Assets/_Game/Scripts/Core/FormalGameBootstrap.cs \
  Assets/_Game/Scripts/City/CityDeploymentModel.cs \
  Assets/_Game/Scripts/City/CityDeploymentRules.cs \
  Assets/_Game/Scripts/City/CityOperationalRules.cs \
  Assets/_Game/Scripts/City/CityPathfinder.cs \
  Assets/_Game/Scripts/City/CityTerrainRules.cs \
  Assets/_Game/Scripts/City/DirectControlRules.cs \
  Assets/_Game/Scripts/World/WorldMapModel.cs \
  Assets/_Game/Scripts/World/WorldSeed.cs \
  Assets/_Game/Scripts/World/CameraFollowModel.cs \
  Assets/_Game/Scripts/Leader/LeaderModel.cs

EXISTING_SCRIPT_DIFFS="$(
  git diff --name-only "$BASE"..HEAD -- Assets/_Game/Scripts |
    rg -v '^Assets/_Game/Scripts/Graybox3D(\.meta|/)' || true
)"
test -z "$EXISTING_SCRIPT_DIFFS"

cmp \
  "/tmp/wastecity-3d-graybox-foundation/regression/bug-0001-baseline.txt" \
  "/tmp/wastecity-3d-graybox-foundation/regression/bug-0001-current.txt"

git diff --check
git status --short
git log --oneline --decorate \
  d3c10ec91b00107e61fd32e7bec7a9dcff2be247..HEAD
```

此时 `git status --short` 必须为空。范围命令允许且只允许新增 3D、测试、EditorBuildSettings、FormalBuildTools 和受控文档差异。

- [ ] **Step 6：推送并证明远端一致**

```bash
cd "/Users/baiyan1/Documents/WasteCity-3d-graybox-foundation"
git push origin codex/3d-graybox-foundation
git fetch origin codex/3d-graybox-foundation
git rev-parse HEAD
git rev-parse origin/codex/3d-graybox-foundation
git status --short
```

本地和远端 SHA 必须逐字相同，最终 `git status --short` 必须为空。不得创建 PR、合并、force-push 或创建后续分支。

## 最终交付记录要求

执行完成后报告：

1. 分支、HEAD、相对 `d3c10ec…` 和冻结 `e791180…` 的关系；
2. 每个实际提交 SHA 与唯一用途；
3. 实际新增/修改文件；
4. 每项 RED 的失败原因与日志路径、GREEN 的 XML/日志路径；
5. 实际 EditMode/PlayMode 总数、通过数、失败数；
6. 无界面编译、2D Windows 构建、3D Windows 构建的实际结果与日志；
7. 两个 PE32+ x86-64 GUI 产物的 `file` 原样输出；
8. Renderer、长期 GameObject、300 次调用分配、五次生成中位数、300 帧 Profiler、1920×1080 FPS 的实际证据；
9. `FormalPrototype`、2D 运行时、纯规则、schema/存档、Building、`BUG-0001`、Packages、GraphicsSettings、QualitySettings 的零差异证据；
10. 真实 Windows 冒烟、默认 3D 入口、建造/生产/战斗/撤离/存档等仍未完成或明确排除内容；
11. 本地/远端 HEAD 一致性；
12. `git status --short` 原样输出。

完成上述交付后立即停止等待调度验收，不开始默认入口切换、3D 建造、战斗、撤离或任何下一里程碑。
