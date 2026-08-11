# Mobile Research Station and Sparse Map Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让研究站可在移动城市内城建造，并把默认 3D 试玩世界从 32×24 扩展为中央内容不变、外围为空旷普通荒地的 64×48 地图。

**Architecture:** `GrayboxWorldLayout3D` 成为当前 3D 世界尺寸、旧区域偏移和确定性世界创建的唯一真值；Bootstrap、Building Session、Building World View 和 Editor authoring 只消费该合同。研究站只修改既有 Catalog 运行标签，不新增研究 UI 或推进控制器；未来 100%/50% 研究倍率继续保持书面合同。

**Tech Stack:** Unity `2022.3.62f1`、C#、NUnit EditMode/PlayMode、URP `14.0.12`、现有 `WorldMapModel`、`PlanarCoordinateMapper3D`、BuildingGrid、Unity Editor authoring、Windows x86-64 构建。

## Global Constraints

- 关联需求固定为 `IDEA-0005` 与 `IDEA-0006`；实现开始时均为已明确 / 已批准 / 开发中。
- 权威规格为 `Docs/superpowers/specs/2026-08-11-mobile-research-station-and-sparse-map-expansion-design.md`。
- 目标世界固定 `64×48`；旧世界固定 `32×24`；偏移固定 `(16,12)`；默认 seed 固定 `8128`。
- 中央 `768` 格逐字段等于旧 seed 世界；外围 `2304` 格必须为 Wasteland/Open、无资源、amount 0。
- 序列化城市世界位置保持 `(-9,.5,-4)`；扩图后其逻辑格为 `(23,20)`。
- ResearchStation 只把 Operation 改为 MobileAllowed；Placement Either、最低人口 200、成本、占地、时间、生命和稳定 ID 不变。
- 本计划不实现研究菜单、研究选择、研究队列、研究 Tick 或研究存档；未来倍率只写在已批准正式文档中。
- GroundGrid 改为 `64×48`；InnerGrid 继续 `8×6`；GroundBuildRadius 继续 `8`。
- 地形继续使用现有七层数组、共享材质、控制图和单一连续 Mesh/Renderer；不得修改 28 张源 PNG、Shader、Material 或 Texture2DArray。
- 不修改 schema `30`、Persistence、FormalPrototype、PlaceholderWorldView、PlaceholderBuildingController、2D ResearchController、Packages、GraphicsSettings 或 QualitySettings。
- 现有 28 个地形 importer `.meta` 与两个未跟踪 ProjectSettings 是受保护外部改动；不得暂存、清理、重导入覆盖或移动。
- 所有 `-runTests` 命令禁止同时使用 `-quit`；无界面编译和构建允许 `-quit`。
- 每次只 `git add` 任务 Files 列表中的精确路径；禁止 `git add .`、`git add Assets` 或宽泛格式化。
- Unity 可执行文件固定为 `/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity`。
- 本计划设计基线为 `48e23e6`；最终范围与冻结证明从该 SHA 比较。

---

## File and Assembly Map

### New runtime source

```text
Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs
Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs.meta
```

`GrayboxWorldLayout3D` 只负责尺寸、偏移、默认 seed、旧格到扩展格转换和确定性 `WorldMapModel` 创建；不负责表现、路径、建筑、存档或输入。

### Existing runtime changes

```text
Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs
Assets/_Game/Scripts/Building/BuildingGrid.cs
```

- Bootstrap 通过布局工厂创建世界并保留旧常量兼容别名。
- Building Session 的地面 BuildingGrid 消费集中尺寸。
- Building World View 的地面网格线与地面建筑世界坐标消费集中尺寸。
- Building Catalog 只改变 ResearchStation 的 Operation。

### Editor change

```text
Assets/_Game/Editor/GrayboxSceneAuthoring.cs
```

新建城市和部署合法性检查都使用布局工厂；旧批准格 `(7,8)` 转成 `(23,20)`，世界位置不变。

### Tests

```text
Assets/_Game/Tests/EditMode/GrayboxWorldLayout3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxWorldLayout3DTests.cs.meta
Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs
Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs
Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs
Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs
Assets/_Game/Tests/EditMode/BuildingMobilityRulesTests.cs
Assets/_Game/Tests/EditMode/BuildingUnlockTests.cs
Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs
```

不需要新增程序集：新运行时类型进入现有 `WasteCity.Graybox3D`，新 EditMode 测试进入现有测试程序集。

---

### Task 1: Freeze the Deterministic 64×48 Layout Factory

**Files:**
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxWorldLayout3DTests.cs`
- Create: `Assets/_Game/Tests/EditMode/GrayboxWorldLayout3DTests.cs.meta`

**Interfaces:**
- Consumes: `WorldMapModel(int,int,WorldSeed)`, `WorldMapModel(WorldCell[,])`, `WorldCell`, `WorldSeed`, `PlanarCoordinateMapper3D`.
- Produces: `GrayboxWorldLayout3D.DefaultSeed`, `LegacyWidth`, `LegacyHeight`, `WorldWidth`, `WorldHeight`, `LegacyOffsetX`, `LegacyOffsetY`, `CreateDefault()`, `Create(int)`, `ToExpandedX(int)`, `ToExpandedY(int)`.

- [ ] **Step 1: Capture the protected baseline and run fresh tests**

Run:

```bash
export PROJECT_PATH="$(pwd)"
export UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
mkdir -p /tmp/wastecity-sparse-map/baseline
git status --short > /tmp/wastecity-sparse-map/baseline/status.txt
git rev-parse HEAD > /tmp/wastecity-sparse-map/baseline/head.txt
git rev-parse '@{u}' > /tmp/wastecity-sparse-map/baseline/tracking.txt
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testResults /tmp/wastecity-sparse-map/baseline/editmode.xml -logFile /tmp/wastecity-sparse-map/baseline/editmode.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testResults /tmp/wastecity-sparse-map/baseline/playmode.xml -logFile /tmp/wastecity-sparse-map/baseline/playmode.log
```

Expected: both XML files report zero failures. `status.txt` contains only the pre-existing protected terrain `.meta` and ProjectSettings paths. If Unity reports that the project is already open, stop the exact test process, exit Play Mode/Editor normally, and rerun; do not kill unrelated Unity projects.

- [ ] **Step 2: Write the failing layout tests**

Create `GrayboxWorldLayout3DTests.cs` with exact assertions:

```csharp
[Test]
public void Constants_FreezeApprovedDimensionsOffsetsAndSeed()
{
    Assert.That(GrayboxWorldLayout3D.DefaultSeed, Is.EqualTo(8128));
    Assert.That(GrayboxWorldLayout3D.LegacyWidth, Is.EqualTo(32));
    Assert.That(GrayboxWorldLayout3D.LegacyHeight, Is.EqualTo(24));
    Assert.That(GrayboxWorldLayout3D.WorldWidth, Is.EqualTo(64));
    Assert.That(GrayboxWorldLayout3D.WorldHeight, Is.EqualTo(48));
    Assert.That(GrayboxWorldLayout3D.LegacyOffsetX, Is.EqualTo(16));
    Assert.That(GrayboxWorldLayout3D.LegacyOffsetY, Is.EqualTo(12));
}

[Test]
public void CreateDefault_PreservesEveryLegacyCellAndLeavesSparseOuterRing()
{
    var legacy = new WorldMapModel(32, 24, new WorldSeed(8128));
    WorldMapModel expanded = GrayboxWorldLayout3D.CreateDefault();
    var central = 0;
    var outer = 0;
    for (var x = 0; x < expanded.Width; x++)
    for (var y = 0; y < expanded.Height; y++)
    {
        bool isCentral = x >= 16 && x < 48 && y >= 12 && y < 36;
        WorldCell actual = expanded.Get(x, y);
        if (isCentral)
        {
            WorldCell expected = legacy.Get(x - 16, y - 12);
            AssertCellEquals(expected, actual);
            central++;
        }
        else
        {
            Assert.That(actual.Terrain, Is.EqualTo(TerrainKind.Wasteland));
            Assert.That(actual.Traversal, Is.EqualTo(WorldTraversalKind.Open));
            Assert.That(actual.ResourceId, Is.Null);
            Assert.That(actual.ResourceAmount, Is.Zero);
            outer++;
        }
    }
    Assert.That(central, Is.EqualTo(768));
    Assert.That(outer, Is.EqualTo(2304));
    Assert.That(expanded.ResourceNodeCount, Is.EqualTo(legacy.ResourceNodeCount));
}

[Test]
public void ExpandedLegacyCells_KeepExactWorldPositions()
{
    var oldMapper = new PlanarCoordinateMapper3D(32, 24);
    var newMapper = new PlanarCoordinateMapper3D(64, 48);
    for (var x = 0; x < 32; x++)
    for (var y = 0; y < 24; y++)
    {
        Assert.That(oldMapper.TryCellToWorld(x, y, 0f, out Vector3 before), Is.True);
        Assert.That(newMapper.TryCellToWorld(x + 16, y + 12, 0f, out Vector3 after), Is.True);
        Assert.That(after, Is.EqualTo(before));
    }
}
```

Also test two calls with the same seed cell-for-cell, `ToExpandedX(7)==23`, `ToExpandedY(8)==20`, and invalid legacy conversions throw `ArgumentOutOfRangeException` rather than silently producing an out-of-map coordinate.

- [ ] **Step 3: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxWorldLayout3DTests -testResults /tmp/wastecity-sparse-map/task-01-red.xml -logFile /tmp/wastecity-sparse-map/task-01-red.log
```

Expected: compile failure only because `GrayboxWorldLayout3D` does not exist. Any assembly or unrelated compiler error is a stop gate.

- [ ] **Step 4: Implement the minimal factory**

Create:

```csharp
using System;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public static class GrayboxWorldLayout3D
    {
        public const int DefaultSeed = 8128;
        public const int LegacyWidth = 32;
        public const int LegacyHeight = 24;
        public const int WorldWidth = 64;
        public const int WorldHeight = 48;
        public const int LegacyOffsetX = 16;
        public const int LegacyOffsetY = 12;

        public static WorldMapModel CreateDefault() => Create(DefaultSeed);

        public static WorldMapModel Create(int seedValue)
        {
            var legacy = new WorldMapModel(
                LegacyWidth,
                LegacyHeight,
                new WorldSeed(seedValue));
            var cells = new WorldCell[WorldWidth, WorldHeight];
            var openWasteland = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
            for (var x = 0; x < WorldWidth; x++)
            for (var y = 0; y < WorldHeight; y++)
                cells[x, y] = openWasteland;
            for (var x = 0; x < LegacyWidth; x++)
            for (var y = 0; y < LegacyHeight; y++)
                cells[ToExpandedX(x), ToExpandedY(y)] = legacy.Get(x, y);
            return new WorldMapModel(cells);
        }

        public static int ToExpandedX(int legacyX)
        {
            if (legacyX < 0 || legacyX >= LegacyWidth)
                throw new ArgumentOutOfRangeException(nameof(legacyX));
            return legacyX + LegacyOffsetX;
        }

        public static int ToExpandedY(int legacyY)
        {
            if (legacyY < 0 || legacyY >= LegacyHeight)
                throw new ArgumentOutOfRangeException(nameof(legacyY));
            return legacyY + LegacyOffsetY;
        }
    }
}
```

Use a fresh unique Unity meta GUID. Do not run Unity import over the protected terrain directory.

- [ ] **Step 5: Run GREEN and related coordinate tests**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxWorldLayout3DTests|WasteCity.Tests.PlanarCoordinateMapper3DTests' -testResults /tmp/wastecity-sparse-map/task-01-green.xml -logFile /tmp/wastecity-sparse-map/task-01-green.log
```

Expected: all selected tests pass; central count `768`, outer count `2304`, resource count unchanged.

- [ ] **Step 6: Commit Task 1**

```bash
git add -- Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs.meta Assets/_Game/Tests/EditMode/GrayboxWorldLayout3DTests.cs Assets/_Game/Tests/EditMode/GrayboxWorldLayout3DTests.cs.meta
git diff --cached --check
git commit -m "feat: add deterministic sparse graybox world"
```

---

### Task 2: Route Bootstrap and Editor Authoring Through the Layout

**Files:**
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs`
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`

**Interfaces:**
- Consumes: all `GrayboxWorldLayout3D` constants, `CreateDefault()`, `ToExpandedX(int)`, `ToExpandedY(int)` from Task 1.
- Produces: Bootstrap world `64×48`; authoring initial city logical cell `(23,20)` at unchanged world position `(-9,.5,-4)`.

- [ ] **Step 1: Write Bootstrap and scene contract RED tests**

Change `Initialize_GeneratesFrozenWorldAndView` to assert:

```csharp
Assert.That(bootstrap.World.Width, Is.EqualTo(64));
Assert.That(bootstrap.World.Height, Is.EqualTo(48));
AssertWorldEquals(GrayboxWorldLayout3D.CreateDefault(), bootstrap.World);
```

Change scene contract assertions to:

```csharp
Assert.That(city.transform.position, Is.EqualTo(new Vector3(-9f, .5f, -4f)));
var coordinates = new PlanarCoordinateMapper3D(64, 48);
Assert.That(coordinates.TryWorldToCell(city.transform.position, out int x, out int y), Is.True);
Assert.That((x, y), Is.EqualTo((23, 20)));
Assert.That(
    CityDeploymentRules.Validate(GrayboxWorldLayout3D.CreateDefault(), x, y),
    Is.EqualTo(CityDeploymentFailure.None));
```

Add a source contract using the existing `System.IO` and `UnityEngine.Application` imports:

```csharp
[Test]
public void SceneAuthoring_UsesApprovedSparseLayoutFactory()
{
    string source = File.ReadAllText(
        Path.Combine(
            Application.dataPath,
            "_Game/Editor/GrayboxSceneAuthoring.cs"));
    StringAssert.Contains("GrayboxWorldLayout3D.CreateDefault()", source);
    StringAssert.Contains("GrayboxWorldLayout3D.ToExpandedX(7)", source);
    StringAssert.Contains("GrayboxWorldLayout3D.ToExpandedY(8)", source);
    string compact = source.Replace("\r", string.Empty)
        .Replace("\n", string.Empty)
        .Replace(" ", string.Empty);
    StringAssert.DoesNotContain(
        "newWorldMapModel(GrayboxSceneBootstrap.WorldWidth",
        compact);
}
```

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxSceneBootstrapTests|WasteCity.Tests.GrayboxSceneContractTests' -testResults /tmp/wastecity-sparse-map/task-02-red.xml -logFile /tmp/wastecity-sparse-map/task-02-red.log
```

Expected: failures show Bootstrap still returns `32×24`, scene mapping still expects `(7,8)`, and authoring still constructs the old random map.

- [ ] **Step 3: Update Bootstrap**

Keep compatibility constants but route them to the layout:

```csharp
public const int WorldSeedValue = GrayboxWorldLayout3D.DefaultSeed;
public const int WorldWidth = GrayboxWorldLayout3D.WorldWidth;
public const int WorldHeight = GrayboxWorldLayout3D.WorldHeight;
```

Replace the constructor in `Initialize()` with:

```csharp
World = GrayboxWorldLayout3D.CreateDefault();
```

Do not alter URP scope checks, idempotency, terrain presentation, fallback or cleanup.

- [ ] **Step 4: Update authoring without moving the serialized city**

In `CreateMobileCity`, convert the old approved cell before mapping:

```csharp
coordinates.TryCellToWorld(
    GrayboxWorldLayout3D.ToExpandedX(7),
    GrayboxWorldLayout3D.ToExpandedY(8),
    .5f,
    out Vector3 cityPosition);
```

In `EnsurePlayableInitialDeployment`, use:

```csharp
WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
const int approvedLegacyX = 7;
const int approvedLegacyY = 8;
int approvedX = GrayboxWorldLayout3D.ToExpandedX(approvedLegacyX);
int approvedY = GrayboxWorldLayout3D.ToExpandedY(approvedLegacyY);
```

Keep the final world position and all existing scene identities unchanged.

- [ ] **Step 5: Run GREEN**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxSceneBootstrapTests|WasteCity.Tests.GrayboxSceneContractTests|WasteCity.Tests.GrayboxWorldLayout3DTests' -testResults /tmp/wastecity-sparse-map/task-02-green.xml -logFile /tmp/wastecity-sparse-map/task-02-green.log
```

Expected: all selected tests pass; old world positions remain exact.

- [ ] **Step 6: Commit Task 2**

```bash
git add -- Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs Assets/_Game/Editor/GrayboxSceneAuthoring.cs Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs
git diff --cached --check
git commit -m "feat: route graybox scene through sparse layout"
```

---

### Task 3: Expand the Ground Building Grid and Presentation

**Files:**
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs`

**Interfaces:**
- Consumes: `GrayboxWorldLayout3D.WorldWidth` and `.WorldHeight`.
- Produces: `GroundGrid` `64×48`; ground grid mesh and building transforms aligned with final mapper; unchanged `8×6` inner grid.

- [ ] **Step 1: Write RED tests for grid dimensions and coordinate alignment**

Add to `GrayboxBuildingSessionTests`:

```csharp
[Test]
public void DevelopmentFixture_UsesExpandedGroundGridAndUnchangedInnerGrid()
{
    GrayboxBuildingSession3D session = CreateSession();
    Assert.That(session.GroundGrid.Width, Is.EqualTo(64));
    Assert.That(session.GroundGrid.Height, Is.EqualTo(48));
    Assert.That(session.InnerGrid.Width, Is.EqualTo(8));
    Assert.That(session.InnerGrid.Height, Is.EqualTo(6));
    Assert.That(session.GroundBuildRadius, Is.EqualTo(8));
}
```

Add projection/view coverage that initializes the real building world view, places a 1×1 ground preview at `(0,0)` and `(63,47)`, and checks their centers against `PlanarCoordinateMapper3D(64,48)` results. Assert infrastructure renderer count remains `<=8` and there are no `3072` cell objects.

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxBuildingSessionTests|WasteCity.Tests.GrayboxBuildingProjectionAndViewTests' -testResults /tmp/wastecity-sparse-map/task-03-red.xml -logFile /tmp/wastecity-sparse-map/task-03-red.log
```

Expected: dimension and edge world-position assertions fail because both production files still use `32/24`.

- [ ] **Step 3: Replace duplicated ground dimensions**

In `GrayboxBuildingSession3D`, remove `GroundGridWidth/GroundGridHeight` and construct:

```csharp
GroundGrid = new BuildingGrid(
    GrayboxWorldLayout3D.WorldWidth,
    GrayboxWorldLayout3D.WorldHeight);
```

In `GrayboxBuildingWorldView3D`, replace the two constants with aliases or direct reads:

```csharp
private const int GroundWidth = GrayboxWorldLayout3D.WorldWidth;
private const int GroundHeight = GrayboxWorldLayout3D.WorldHeight;
```

Do not change InnerWidth/InnerHeight, preview ownership, stable IDs, material handling or mesh batching.

- [ ] **Step 4: Run GREEN and allocation-sensitive view tests**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxBuildingSessionTests|WasteCity.Tests.GrayboxBuildingProjectionAndViewTests|WasteCity.Tests.GrayboxBuildingUiAndInputTests' -testResults /tmp/wastecity-sparse-map/task-03-green.xml -logFile /tmp/wastecity-sparse-map/task-03-green.log
```

Expected: all selected tests pass and existing zero-allocation preview tests remain green.

- [ ] **Step 5: Commit Task 3**

```bash
git add -- Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs
git diff --cached --check
git commit -m "feat: expand graybox ground building grid"
```

---

### Task 4: Allow Research Station Construction in the Mobile Inner City

**Files:**
- Modify: `Assets/_Game/Scripts/Building/BuildingGrid.cs`
- Modify: `Assets/_Game/Tests/EditMode/BuildingMobilityRulesTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/BuildingUnlockTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

**Interfaces:**
- Consumes: existing `BuildingMobilityRules.CanConstruct/CanOperate`, `GrayboxBuildingSession3D.TryBeginConstruction`, real virtual keyboard/mouse PlayMode fixture.
- Produces: ResearchStation tagged `Either + MobileAllowed`; real Mobile inner-city construction without any research progression runtime.

- [ ] **Step 1: Write catalog and rule RED tests**

Change the CatalogTags row to:

```csharp
yield return Case(
    BuildingCatalog.ResearchStation,
    BuildingPlacement.Either,
    BuildingOperation.MobileAllowed);
```

Add:

```csharp
[TestCase(CityMode.Mobile, BuildingSite.InnerCity, true)]
[TestCase(CityMode.Mobile, BuildingSite.Ground, false)]
[TestCase(CityMode.Deploying, BuildingSite.InnerCity, false)]
[TestCase(CityMode.Fortress, BuildingSite.InnerCity, true)]
[TestCase(CityMode.Fortress, BuildingSite.Ground, true)]
[TestCase(CityMode.Packing, BuildingSite.InnerCity, false)]
public void ResearchStation_UsesApprovedConstructionMatrix(
    CityMode mode,
    BuildingSite site,
    bool expected)
{
    Assert.That(
        BuildingMobilityRules.CanConstruct(
            BuildingCatalog.ResearchStation,
            site,
            mode),
        Is.EqualTo(expected));
}
```

Keep or strengthen `ResearchStationRequiresTwoHundredPopulation` to prove `199` false and `200` true. Add a session test that sets population 200, spends through the real atomic path, creates ResearchStation at an empty InnerGrid position in Mobile, and retains the same stable instance through completion.

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.BuildingMobilityRulesTests|WasteCity.Tests.BuildingUnlockTests|WasteCity.Tests.GrayboxBuildingSessionTests' -testResults /tmp/wastecity-sparse-map/task-04-red.xml -logFile /tmp/wastecity-sparse-map/task-04-red.log
```

Expected: only Mobile/InnerCity ResearchStation expectations fail because Operation remains FortressOnly.

- [ ] **Step 3: Make the one-line production change**

Update the Catalog definition without changing any other argument:

```csharp
public static readonly BuildingDefinition ResearchStation =
    new BuildingDefinition(
        "core.building.research-station",
        "研究站",
        2,
        2,
        ResourceIds.Iron,
        6,
        false,
        10f,
        260,
        200,
        placement: BuildingPlacement.Either,
        operation: BuildingOperation.MobileAllowed);
```

Do not edit `BuildingMobilityRules`, `ResearchModel` or any ResearchController.

- [ ] **Step 4: Run focused GREEN**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.BuildingMobilityRulesTests|WasteCity.Tests.BuildingUnlockTests|WasteCity.Tests.GrayboxBuildingSessionTests|WasteCity.Tests.GrayboxBuildingCatalogTests' -testResults /tmp/wastecity-sparse-map/task-04-green.xml -logFile /tmp/wastecity-sparse-map/task-04-green.log
```

Expected: the full 30-building tag matrix, population boundary and atomic session construction pass.

- [ ] **Step 5: Extend the real formal-scene mobile matrix and place the station**

In `AllDeclaredMobileInnerBuildings_ProjectAsValidInFormalScene`, add `BuildingCatalog.ResearchStation` to `expectedMobileInner`, then remove the old assertion expecting `InvalidCityMode`. After the existing matrix loop, use the test's real virtual mouse helper to commit the preview:

```csharp
interaction.Select(BuildingCatalog.ResearchStation);
yield return MoveToInnerPreview(city);
Assert.That(placement.CurrentHit.Site, Is.EqualTo(BuildingSite.InnerCity));
Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
int countBefore = session.Instances.Count;
yield return ClickMouse(
    MouseButton.Left,
    mouse.position.ReadValue());
Assert.That(session.Instances, Has.Count.EqualTo(countBefore + 1));
GrayboxBuildingInstance3D placed = session.Instances[countBefore];
Assert.That(
    placed.Placement.Definition,
    Is.SameAs(BuildingCatalog.ResearchStation));
Assert.That(placed.Placement.Site, Is.EqualTo(BuildingSite.InnerCity));
Assert.That(
    placed.State,
    Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
```

This test already loads `GrayboxPrototype3D`, creates real virtual devices, injects population/resources through the public Development modifier, moves the real pointer and lets `GrayboxBuildingInputRouter3D.Update` consume the click. Do not add direct calls to `ProcessFrame`, `TickConstruction` or `TryBeginConstruction`, and do not assert research progress because that runtime is excluded.

- [ ] **Step 6: Run PlayMode GREEN**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests -testResults /tmp/wastecity-sparse-map/task-04-playmode.xml -logFile /tmp/wastecity-sparse-map/task-04-playmode.log
```

Expected: all runtime scene tests pass through real scene Update/UGUI input; no direct tick/router bypass scan matches the new test.

- [ ] **Step 7: Commit Task 4**

```bash
git add -- Assets/_Game/Scripts/Building/BuildingGrid.cs Assets/_Game/Tests/EditMode/BuildingMobilityRulesTests.cs Assets/_Game/Tests/EditMode/BuildingUnlockTests.cs Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs
git diff --cached --check
git commit -m "feat: allow mobile inner-city research stations"
```

---

### Task 5: Prove Runtime Integration, Authoring Idempotency, Performance and Builds

**Files:**
- Modify only if a failing approved test demonstrates missing coverage: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Modify only if a failing approved test demonstrates missing coverage: `Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs`
- Modify only if a failing approved test demonstrates missing coverage: `Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs`
- Modify only for the observed 32×24 runtime-contract mismatch: `Assets/_Game/Tests/PlayMode/FirstArtTerrainRuntimeSceneTests.cs`
- Generated by approved authoring only if content changes: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`

**Interfaces:**
- Consumes: Tasks 1–4 completed runtime.
- Produces: evidence that the real scene, terrain, movement, deployment, building, performance and three builds remain valid.

The first focused PlayMode run after Tasks 1–4 produced exactly two
expected stale-contract failures: `GrayboxRuntimeSceneTests` still asserted
`32×24`, while `FirstArtTerrainRuntimeSceneTests` rebuilt a separate
`32×24` seed model instead of comparing the scene against
`GrayboxWorldLayout3D.CreateDefault()`. The latter test file is therefore an
approved minimal Task 5 boundary: update it to validate all `64×48` cells,
including the sparse outer ring, without changing production terrain code.

- [ ] **Step 1: Run the approved authoring twice and capture identities**

Use the public `WasteCity.Editor.GrayboxSceneAuthoring.Configure` method. Before and after each run capture:

```bash
mkdir -p /tmp/wastecity-sparse-map/task-05-authoring
git hash-object Assets/_Game/Scenes/GrayboxPrototype3D.unity > /tmp/wastecity-sparse-map/task-05-authoring/before-scene.txt
git hash-object Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta > /tmp/wastecity-sparse-map/task-05-authoring/before-meta.txt
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_PATH" -quit -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure -logFile /tmp/wastecity-sparse-map/task-05-authoring/pass1.log
git hash-object Assets/_Game/Scenes/GrayboxPrototype3D.unity > /tmp/wastecity-sparse-map/task-05-authoring/after1-scene.txt
git hash-object Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta > /tmp/wastecity-sparse-map/task-05-authoring/after1-meta.txt
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_PATH" -quit -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure -logFile /tmp/wastecity-sparse-map/task-05-authoring/pass2.log
git hash-object Assets/_Game/Scenes/GrayboxPrototype3D.unity > /tmp/wastecity-sparse-map/task-05-authoring/after2-scene.txt
git hash-object Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta > /tmp/wastecity-sparse-map/task-05-authoring/after2-meta.txt
cmp /tmp/wastecity-sparse-map/task-05-authoring/after1-scene.txt /tmp/wastecity-sparse-map/task-05-authoring/after2-scene.txt
cmp /tmp/wastecity-sparse-map/task-05-authoring/after1-meta.txt /tmp/wastecity-sparse-map/task-05-authoring/after2-meta.txt
```

Run authoring once, capture `after1`; run it again, capture `after2`. Expected: scene and meta identities are stable across `after1/after2`; the serialized city world position remains `(-9,.5,-4)`. If authoring changes the scene, stage it only after focused scene contracts pass and the second pass is byte-identical.

- [ ] **Step 2: Run focused integration tests**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter 'WasteCity.Tests.GrayboxWorldLayout3DTests|WasteCity.Tests.GrayboxSceneBootstrapTests|WasteCity.Tests.GrayboxSceneContractTests|WasteCity.Tests.GrayboxBuildingSessionTests|WasteCity.Tests.GrayboxBuildingProjectionAndViewTests|WasteCity.Tests.BuildingMobilityRulesTests|WasteCity.Tests.FirstArtTerrainControlMapTests|WasteCity.Tests.FirstArtTerrainRendererTests|WasteCity.Tests.FirstArtTerrainPerformanceTests' -testResults /tmp/wastecity-sparse-map/task-05-focused-editmode.xml -logFile /tmp/wastecity-sparse-map/task-05-focused-editmode.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter 'WasteCity.Tests.GrayboxRuntimeSceneTests|WasteCity.Tests.GrayboxBuildingRuntimeSceneTests|WasteCity.Tests.FirstArtTerrainRuntimeSceneTests' -testResults /tmp/wastecity-sparse-map/task-05-focused-playmode.xml -logFile /tmp/wastecity-sparse-map/task-05-focused-playmode.log
```

Expected: zero failures; real scene sees 64×48 world, central deployment works, outer Wasteland is visible, city/building input works.

- [ ] **Step 3: Run fresh full regression**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testResults /tmp/wastecity-sparse-map/task-05-full-editmode.xml -logFile /tmp/wastecity-sparse-map/task-05-full-editmode.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testResults /tmp/wastecity-sparse-map/task-05-full-playmode.xml -logFile /tmp/wastecity-sparse-map/task-05-full-playmode.log
```

Expected: zero failures and zero skipped tests. If the known frozen SwordIntent frame-boundary test flakes once, isolate and rerun it, then rerun full PlayMode; do not edit frozen combat unless independently reproducible and separately approved.

- [ ] **Step 4: Run compile and three Windows builds**

```bash
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_PATH" -quit -logFile /tmp/wastecity-sparse-map/task-05-compile.log
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_PATH" -quit -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows -logFile /tmp/wastecity-sparse-map/task-05-build-release-3d.log
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_PATH" -quit -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment -logFile /tmp/wastecity-sparse-map/task-05-build-development-3d.log
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_PATH" -quit -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D -logFile /tmp/wastecity-sparse-map/task-05-build-legacy-2d.log
file Builds/Windows/WasteCity.exe Builds/Windows3DDevelopment/WasteCityGrayboxDev.exe Builds/Windows2D/WasteCity2D.exe
```

Expected: every Unity command exits 0; every artifact is `PE32+ executable (GUI) x86-64, for MS Windows`. Do not run PE files on macOS or claim real Windows smoke completion.

- [ ] **Step 5: Verify scope and protected files**

```bash
git diff --check 48e23e6..HEAD
git diff --exit-code 48e23e6..HEAD -- Assets/_Game/Scripts/Persistence Assets/_Game/Scenes/FormalPrototype.unity Packages ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset
git status --short
```

Expected: commit-to-commit protected paths are unchanged; working status still contains the exact pre-existing protected terrain `.meta` and two ProjectSettings paths only. If their hashes changed from Task 1 baseline, stop before commit/push.

- [ ] **Step 6: Commit any Task 5-only test/scene evidence changes**

If no tracked file changed, skip the commit. Otherwise stage only exact changed approved paths:

```bash
git add -- Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs Assets/_Game/Scenes/GrayboxPrototype3D.unity
git diff --cached --check
git commit -m "test: verify sparse graybox runtime integration"
```

---

### Task 6: Controlled Status Writeback and Push

**Files:**
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`

**Interfaces:**
- Consumes: actual commit SHAs and fresh verification results from Tasks 1–5.
- Produces: truthful statuses and GitHub-visible evidence; no gameplay code.

- [ ] **Step 1: Write only verified facts**

Update `IDEA-0005`:

- keep overall status `开发中`;
- record mobile ResearchStation sub-scope implementation commit and tests;
- explicitly state research UI/progression and 100%/50% runtime remain unimplemented.

Update `IDEA-0006`:

- set status to `已实现待验证` after automation/build completion;
- record layout factory, 64×48, 768/2304 counts, unchanged world position, tests, performance and builds;
- do not mark user validation or real Windows smoke complete unless actually performed.

Update Roadmap baseline and next order only with these same facts. Do not rewrite historical 32×24 or 96×64 performance evidence.

- [ ] **Step 2: Verify document consistency**

```bash
rg -n 'IDEA-0005|IDEA-0006|64×48|研究站|100%|50%' Docs/05-Formal-Development-Roadmap-ZH.md Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --check -- Docs/05-Formal-Development-Roadmap-ZH.md Docs/06-User-Feedback-and-Change-Control-ZH.md
```

Expected: no statement says future research runtime is implemented; no statement replaces the final 96×64 target.

- [ ] **Step 3: Commit documentation separately**

```bash
git add -- Docs/05-Formal-Development-Roadmap-ZH.md Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --cached --check
git commit -m "docs: record sparse map and mobile research station verification"
```

- [ ] **Step 4: Final fresh verification and push**

Before push, confirm:

```bash
git diff --check 48e23e6..HEAD
git status --short
git log --oneline 48e23e6..HEAD
git push origin codex/playtest-fixes
git rev-parse HEAD
git rev-parse '@{u}'
git ls-remote origin refs/heads/codex/playtest-fixes
```

Expected: local, tracking and ls-remote SHAs match; protected working changes remain unstaged; no PR, merge, force-push or Release is created.
