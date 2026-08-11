# First Terrain Runtime Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在默认 3D 场景中用一块连续网格、两张控制图、四个七层纹理数组和一个 URP 主材质显示已批准的七类正式地表，并在任何失败时原子恢复现有灰盒。

**Architecture:** `WorldMapModel` 和 `PlanarCoordinateMapper3D` 继续提供唯一地图真值；独立 `WasteCity.ArtIntegration3D` 表现程序集读取同一个模型，生成控制图和单一连续地表。现有 `GrayboxSceneBootstrap` 只通过可选表现接口调用正式地表，`GrayboxWorldView3D` 只增加按稳定 ID 显隐灰盒表面的能力，不反向依赖正式美术程序集。

**Tech Stack:** Unity `2022.3.62f1`、C#、URP `14.0.12`、ShaderLab/HLSL、`Texture2DArray`、NUnit EditMode/PlayMode、Unity Editor authoring、Git LFS、Windows x86-64 构建。

## Global Constraints

- 关联需求固定为 `IDEA-0004`；需求状态为已明确 / 已批准 / 开发中，不得提前写成已验证。
- 权威设计为 `Docs/superpowers/specs/2026-08-10-first-terrain-runtime-integration-design.md`，并继续服从两份父规格中未被该规格明确细化的内容。
- 七层固定顺序为 Wasteland、Rocky、Wetland、Crystal、Ruins、DeepWater、Cliff；不得依赖文件枚举顺序。
- 原始 28 张 PNG 和现有 `.meta` GUID 不得改写；BaseColor 为 sRGB，其余为 Linear；Mask 固定 R Metallic / G Occlusion / B Detail Mask / A Smoothness。
- 一张 2048 主贴图覆盖约 `4×4` 外城格；Height 运行副本为确定性 1024/R8，但 2048/16-bit 源文件保持不变。
- 正式地表只有 1 Mesh、1 MeshFilter、1 MeshRenderer 和 1 shared Material；禁止逐格对象、逐格材质或运行时 `renderer.material`。
- 不新增 Collider、NavMesh、Unity Terrain、顶点位移、透明水面、水体模型或 CPU 水面 Update。
- 不修改 `TerrainKind`、`WorldTraversalKind`、地图生成、寻路、移动倍率、建造规则、Persistence、schema `30`、默认 Build Settings 顺序或全局 Graphics/Quality 设置。
- `Ruins` 八件和 `Cliff` 六件模型只保留资源，不在本计划中创建 Prefab、分布或场景引用。
- 任一步正式表现失败必须保留或恢复全部灰盒；资源节点、城市、领袖、建筑和其他占位符不得被隐藏。
- 所有 Unity `-runTests` 命令禁止同时添加 `-quit`；无界面编译和构建命令可以使用 `-quit`。
- 每个任务只暂存其 Files 列表中的精确路径；不得使用宽泛 `git add .`、`git add Assets` 或清理无关工作区改动。
- 开始 Task 1 前记录 `git status --short`、HEAD、tracking、EditMode、PlayMode 基线；出现非本计划文件改动时先确认所有权，不覆盖。
- 执行时使用当前隔离 worktree 根作为 `PROJECT_PATH`，Unity 可执行文件固定为 `/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity`。
- 本计划的精确设计基线为 `2baf131a5e69f8931883547ff9b22d61f2652d11`；最终范围和冻结路径都从该 SHA 比较。

---

## File and Assembly Map

### New runtime assembly

```text
Assets/_Game/Scripts/ArtIntegration3D/
├── WasteCity.ArtIntegration3D.asmdef
├── FirstArtTerrainLayer3D.cs
├── FirstArtTerrainProfile3D.cs
├── FirstArtTerrainControlMap3D.cs
├── FirstArtTerrainControlMapGenerator3D.cs
├── FirstArtTerrainMeshBuilder3D.cs
└── FirstArtTerrainRenderer3D.cs
```

- `FirstArtTerrainLayer3D.cs`：七层固定顺序、稳定 ID 和 `WorldCell` 到表现层的唯一映射。
- `FirstArtTerrainProfile3D.cs`：数组、材质和调参的序列化合同与完整校验。
- `FirstArtTerrainControlMap3D.cs`：拥有两张控制纹理和可测试的原始权重数据，负责释放运行时纹理。
- `FirstArtTerrainControlMapGenerator3D.cs`：从只读地图确定性生成软/硬边界权重。
- `FirstArtTerrainMeshBuilder3D.cs`：只生成一个无 Collider 的连续四边形网格。
- `FirstArtTerrainRenderer3D.cs`：原子创建正式表现、绑定 property block、隐藏/恢复灰盒并清理运行时所有权。

### Minimal existing runtime changes

```text
Assets/_Game/Scripts/Graybox3D/IGrayboxTerrainPresentation3D.cs
Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs
Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs
```

- 接口和可选 Bootstrap 接线位于 Graybox3D，避免 Graybox3D 反向引用 ArtIntegration3D。
- `GrayboxWorldView3D` 只管理七个已有表面稳定槽的 Renderer 显隐，不修改模型和规则。

### Editor and generated assets

```text
Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs
Assets/_Game/Editor/GrayboxSceneAuthoring.cs
Assets/_Game/Editor/GrayboxPerformanceProbe.cs
Assets/_Game/Editor/WasteCity.Editor.asmdef
.gitattributes

Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/
├── Generated/
│   ├── TA_Terrain_BaseColor.asset
│   ├── TA_Terrain_Normal.asset
│   ├── TA_Terrain_Mask.asset
│   └── TA_Terrain_Height.asset
├── Materials/MAT_Terrain_FirstPass.mat
├── Profiles/FirstArtTerrainProfile3D.asset
└── Shaders/WasteCityFirstPassTerrain.shader
```

- 四个生成数组使用精确 Git LFS 路径规则；Material、Profile、Shader 保持普通 Unity 文本资产。
- Asset builder 负责数组层序、Height 降采样、已有资产原位更新和 GUID 稳定。
- Graybox scene authoring 只增量保证一个正式表现对象及序列化引用。

### Tests

```text
Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs
Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs
Assets/_Game/Tests/EditMode/FirstArtTerrainMeshTests.cs
Assets/_Game/Tests/EditMode/FirstArtTerrainAssetBuilderTests.cs
Assets/_Game/Tests/EditMode/FirstArtTerrainShaderTests.cs
Assets/_Game/Tests/EditMode/FirstArtTerrainRendererTests.cs
Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs
Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs
Assets/_Game/Tests/PlayMode/FirstArtTerrainRuntimeSceneTests.cs
```

EditMode、PlayMode、Editor asmdef 分别增加 `WasteCity.ArtIntegration3D` 的直接引用；Editor 继续直接引用 URP Runtime。

---

### Task 1: Freeze Layer Catalog and Profile Contract

**Files:**
- Create: `Assets/_Game/Scripts/ArtIntegration3D.meta`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/WasteCity.ArtIntegration3D.asmdef`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/WasteCity.ArtIntegration3D.asmdef.meta`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainLayer3D.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainLayer3D.cs.meta`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef`

**Interfaces:**
- Consumes: `WasteCity.World.WorldCell`, `TerrainKind`, `WorldTraversalKind`, Unity `Material`, `Texture2DArray`.
- Produces: `FirstArtTerrainLayer3D`, `FirstArtTerrainCatalog3D.LayerOf(WorldCell)`, `StableIdOf(FirstArtTerrainLayer3D)`, `FirstArtTerrainProfile3D.Configure(Material, Texture2DArray, Texture2DArray, Texture2DArray, Texture2DArray)`, `TryValidateControlSettings(out string)` and `TryValidate(out string)`.

- [ ] **Step 1: Capture the protected baseline**

Run:

```bash
export PROJECT_PATH="$(pwd)"
export UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
mkdir -p /tmp/wastecity-first-terrain/baseline
git status --short > /tmp/wastecity-first-terrain/baseline/status.txt
git rev-parse HEAD > /tmp/wastecity-first-terrain/baseline/head.txt
git rev-parse '@{u}' > /tmp/wastecity-first-terrain/baseline/tracking.txt
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testResults /tmp/wastecity-first-terrain/baseline/editmode.xml -logFile /tmp/wastecity-first-terrain/baseline/editmode.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testResults /tmp/wastecity-first-terrain/baseline/playmode.xml -logFile /tmp/wastecity-first-terrain/baseline/playmode.log
```

Expected: both test processes produce XML with zero failures; repository status matches the turn-start snapshot. If unrelated changes appear, stop before writing files.

- [ ] **Step 2: Write the failing catalog/profile tests**

Create tests with these assertions:

```csharp
[TestCase(WorldTraversalKind.Open, TerrainKind.Wasteland, FirstArtTerrainLayer3D.Wasteland)]
[TestCase(WorldTraversalKind.Open, TerrainKind.Rocky, FirstArtTerrainLayer3D.Rocky)]
[TestCase(WorldTraversalKind.Open, TerrainKind.Wetland, FirstArtTerrainLayer3D.Wetland)]
[TestCase(WorldTraversalKind.Open, TerrainKind.Crystal, FirstArtTerrainLayer3D.Crystal)]
[TestCase(WorldTraversalKind.Ruins, TerrainKind.Crystal, FirstArtTerrainLayer3D.Ruins)]
[TestCase(WorldTraversalKind.DeepWater, TerrainKind.Rocky, FirstArtTerrainLayer3D.DeepWater)]
[TestCase(WorldTraversalKind.Cliff, TerrainKind.Wetland, FirstArtTerrainLayer3D.Cliff)]
public void LayerOf_UsesTraversalAsVisualOverride(
    WorldTraversalKind traversal,
    TerrainKind terrain,
    FirstArtTerrainLayer3D expected)
{
    var cell = new WorldCell(terrain, null, 0, traversal);
    Assert.That(FirstArtTerrainCatalog3D.LayerOf(cell), Is.EqualTo(expected));
}

[Test]
public void Catalog_HasFrozenSevenLayerOrderAndStableIds()
{
    Assert.That(FirstArtTerrainCatalog3D.LayerCount, Is.EqualTo(7));
    Assert.That((int)FirstArtTerrainLayer3D.Wasteland, Is.Zero);
    Assert.That((int)FirstArtTerrainLayer3D.Cliff, Is.EqualTo(6));
    Assert.That(
        FirstArtTerrainCatalog3D.StableIdOf(FirstArtTerrainLayer3D.DeepWater),
        Is.EqualTo("world.obstacle.deep-water"));
}

[Test]
public void Profile_RejectsMissingArraysWrongDepthAndWrongShader()
{
    FirstArtTerrainProfile3D profile =
        ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();
    Assert.That(profile.TryValidate(out string error), Is.False);
    Assert.That(error, Does.Contain("Material"));
}
```

Also verify exact defaults: control pixels per cell `4`, cells per texture `4f`, Wasteland/Rocky `1.25f`, Wasteland/Wetland `1.15f`, Wasteland/Crystal `1f`, Ruins `0.75f`, DeepWater `0.425f`, Cliff `0.35f`, Height blend strength non-negative, and two non-parallel water velocities.

- [ ] **Step 3: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainProfileTests -testResults /tmp/wastecity-first-terrain/task-01-red.xml -logFile /tmp/wastecity-first-terrain/task-01-red.log
```

Expected: compile failure only because `WasteCity.ArtIntegration3D`, `FirstArtTerrainLayer3D`, `FirstArtTerrainCatalog3D` and `FirstArtTerrainProfile3D` do not exist. Any unrelated compiler error is a stop gate.

- [ ] **Step 4: Add the runtime assembly and exact catalog API**

Create the asmdef with direct references:

```json
{
  "name": "WasteCity.ArtIntegration3D",
  "rootNamespace": "WasteCity.ArtIntegration3D",
  "references": [
    "WasteCity.Game",
    "WasteCity.Graybox3D",
    "Unity.RenderPipelines.Core.Runtime",
    "Unity.RenderPipelines.Universal.Runtime"
  ],
  "autoReferenced": true
}
```

Define the catalog without reflection or filesystem discovery:

```csharp
public enum FirstArtTerrainLayer3D
{
    Wasteland = 0,
    Rocky = 1,
    Wetland = 2,
    Crystal = 3,
    Ruins = 4,
    DeepWater = 5,
    Cliff = 6
}

public static class FirstArtTerrainCatalog3D
{
    public const int LayerCount = 7;
    public static FirstArtTerrainLayer3D LayerOf(WorldCell cell);
    public static string StableIdOf(FirstArtTerrainLayer3D layer);
    public static bool IsSurfaceStableId(string stableId);
}
```

`LayerOf` must check traversal first, then map `TerrainKind`; invalid enum values throw `ArgumentOutOfRangeException` rather than silently becoming Wasteland.

- [ ] **Step 5: Implement the serializable Profile contract**

Use these exact public members:

```csharp
[CreateAssetMenu(menuName = "WasteCity/Art/First Terrain Profile")]
public sealed class FirstArtTerrainProfile3D : ScriptableObject
{
    public const string RequiredShaderName =
        "WasteCity/Terrain/FirstPassBlend";
    public const int DefaultControlPixelsPerCell = 4;
    public const float DefaultCellsPerTexture = 4f;

    public Material Material { get; }
    public Texture2DArray BaseColorArray { get; }
    public Texture2DArray NormalArray { get; }
    public Texture2DArray MaskArray { get; }
    public Texture2DArray HeightArray { get; }
    public int ControlPixelsPerCell { get; }
    public float CellsPerTexture { get; }
    public float HeightBlendStrength { get; }
    public Vector2 WaterNormalVelocityA { get; }
    public Vector2 WaterNormalVelocityB { get; }
    public float BlendWidth(
        FirstArtTerrainLayer3D left,
        FirstArtTerrainLayer3D right);
    public void Configure(
        Material material,
        Texture2DArray baseColorArray,
        Texture2DArray normalArray,
        Texture2DArray maskArray,
        Texture2DArray heightArray);
    public bool TryValidateControlSettings(out string error);
    public bool TryValidate(out string error);
}
```

`TryValidateControlSettings` checks only control pixels, scale, all blend widths, Height strength and non-parallel water velocities, so Tasks 2–3 can test pure generation before runtime assets exist. Full validation order must be stable: control settings, Material, Shader name, BaseColor, Normal, Mask, Height, depth `7` and channel dimensions. Neither method may call `renderer.material` or mutate assets.

`BlendWidth` uses an unordered pair: same-layer returns `0`; Wasteland/Rocky returns `1.25`; Wasteland/Wetland `1.15`; Wasteland/Crystal `1.0`; a pair of two non-Wasteland base layers returns the smaller of their respective Wasteland widths; any pair containing Ruins, DeepWater or Cliff returns that special layer's `0.75`, `0.425` or `0.35`, and a pair of two special layers returns the smaller special width. Invalid enum values throw.

- [ ] **Step 6: Run GREEN and related regression**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainProfileTests -testResults /tmp/wastecity-first-terrain/task-01-green.xml -logFile /tmp/wastecity-first-terrain/task-01-green.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtPassImportPolicyTests -testResults /tmp/wastecity-first-terrain/task-01-import-regression.xml -logFile /tmp/wastecity-first-terrain/task-01-import-regression.log
```

Expected: profile tests and all 43 existing import-policy cases pass.

- [ ] **Step 7: Commit Task 1**

```bash
git add Assets/_Game/Scripts/ArtIntegration3D.meta Assets/_Game/Scripts/ArtIntegration3D/WasteCity.ArtIntegration3D.asmdef Assets/_Game/Scripts/ArtIntegration3D/WasteCity.ArtIntegration3D.asmdef.meta Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainLayer3D.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainLayer3D.cs.meta Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs.meta Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs.meta Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef
git diff --cached --check
git commit -m "feat: define first art terrain profile"
```

---

### Task 2: Generate Deterministic Two-Map Terrain Weights

**Files:**
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMap3D.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMap3D.cs.meta`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMapGenerator3D.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMapGenerator3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs.meta`

**Interfaces:**
- Consumes: Task 1 `FirstArtTerrainCatalog3D`, `FirstArtTerrainProfile3D`, `WorldMapModel`.
- Produces: owned `FirstArtTerrainControlMap3D` and `FirstArtTerrainControlMapGenerator3D.Generate(WorldMapModel, FirstArtTerrainProfile3D)`.

- [ ] **Step 1: Write failing deterministic/control tests**

Use hand-authored `WorldCell[,]` maps, not only seed 8128. Cover:

```csharp
[Test]
public void Generate_UsesFourPixelsPerCellAndFrozenChannels()
{
    WorldMapModel map = CreateSevenStripeMap();
    using FirstArtTerrainControlMap3D result =
        FirstArtTerrainControlMapGenerator3D.Generate(map, profile);
    Assert.That(result.Width, Is.EqualTo(map.Width * 4));
    Assert.That(result.Height, Is.EqualTo(map.Height * 4));
    Assert.That(result.ControlA.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
    Assert.That(result.ControlB.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
    Assert.That(result.ControlA.filterMode, Is.EqualTo(FilterMode.Bilinear));
}

[Test]
public void Generate_NormalizesAndKeepsAtMostThreeWeights()
{
    using FirstArtTerrainControlMap3D result = GenerateThreeWayJunction();
    for (int y = 0; y < result.Height; y++)
    for (int x = 0; x < result.Width; x++)
    {
        TerrainControlWeights3D weights = result.GetWeights(x, y);
        Assert.That(weights.Sum, Is.EqualTo(1f).Within(2f / 255f));
        Assert.That(weights.NonZeroCount, Is.LessThanOrEqualTo(3));
    }
}

[Test]
public void Generate_SameMapProducesSameEncodedBytes()
{
    using FirstArtTerrainControlMap3D first = GenerateSeed8128();
    using FirstArtTerrainControlMap3D second = GenerateSeed8128();
    CollectionAssert.AreEqual(first.ControlABytes, second.ControlABytes);
    CollectionAssert.AreEqual(first.ControlBBytes, second.ControlBBytes);
}
```

Also assert: one-cell centers keep the declared layer highest; Wasteland/Rocky, Wasteland/Wetland and Wasteland/Crystal transitions stay within approved ranges; Ruins, DeepWater and Cliff use their narrower ranges; a special traversal overrides the underlying TerrainKind; map borders never sample an opposite edge.

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainControlMapTests -testResults /tmp/wastecity-first-terrain/task-02-red.xml -logFile /tmp/wastecity-first-terrain/task-02-red.log
```

Expected: compile failure only for the two missing control-map types and `TerrainControlWeights3D`.

- [ ] **Step 3: Implement owned control-map data**

Use exact ownership and access signatures:

```csharp
public readonly struct TerrainControlWeights3D
{
    public Vector4 Base { get; }
    public Vector4 Special { get; }
    public float Sum { get; }
    public int NonZeroCount { get; }
}

public sealed class FirstArtTerrainControlMap3D : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public Texture2D ControlA { get; }
    public Texture2D ControlB { get; }
    public byte[] ControlABytes { get; }
    public byte[] ControlBBytes { get; }
    public TerrainControlWeights3D GetWeights(int x, int y);
    public void Dispose();
}
```

Construct both textures as RGBA32, Linear, no mipmaps, Clamp, Bilinear. `Dispose` destroys only owned runtime textures, using `DestroyImmediate` outside play mode and `Destroy` in play mode; calling it twice is harmless.

- [ ] **Step 4: Implement the exact deterministic generator**

Expose one entry point:

```csharp
public static FirstArtTerrainControlMap3D Generate(
    WorldMapModel model,
    FirstArtTerrainProfile3D profile);
```

For each control texel:

1. Convert pixel center to continuous cell coordinates `(px + .5f) / 4f - .5f` and `(py + .5f) / 4f - .5f`.
2. Clamp the owning cell to the map; gather candidate cells only inside `ceil(maxBlendWidth) + 1` cells.
3. Resolve each candidate through `FirstArtTerrainCatalog3D.LayerOf`.
4. Measure Euclidean distance from the sample point to the candidate cell rectangle.
5. Calculate `t = 1 - SmoothStep(0, profile.BlendWidth(ownerLayer, candidateLayer), distance + edgeNoise)`; owner starts at weight `1`.
6. Use the exact pure integer hash and bilinear lattice interpolation below to derive continuous low-frequency edge noise bounded to ±0.12 cell; never call `UnityEngine.Random` or time APIs.
7. Merge duplicate layer candidates by maximum weight.
8. Keep only the three highest positive layers, using lower enum index as deterministic tie-breaker.
9. Normalize, quantize to bytes with `Mathf.RoundToInt(weight * 255f)`, and assign any rounding remainder to the highest layer so encoded channel sum is exactly 255.
10. Encode layers 0–3 in ControlA RGBA and 4–6 in ControlB RGB; ControlB alpha is exactly 0.

Invalid model/profile throws before allocating textures. A post-quantization zero sum must become Wasteland byte 255 and log one explicit error.

Use these deterministic helpers, with `Smooth01(t) = t * t * (3f - 2f * t)` before bilinear interpolation:

```csharp
private static uint Hash(int x, int y, int layer)
{
    uint value = unchecked((uint)x * 0x8DA6B343u);
    value ^= unchecked((uint)y * 0xD8163841u);
    value ^= unchecked((uint)layer * 0xCB1AB31Fu);
    value ^= value >> 13;
    value *= 0x85EBCA6Bu;
    return value ^ (value >> 16);
}

private static float EdgeNoise(float x, float y, int layer)
{
    float latticeX = x * .25f;
    float latticeY = y * .25f;
    int x0 = Mathf.FloorToInt(latticeX);
    int y0 = Mathf.FloorToInt(latticeY);
    float tx = Smooth01(latticeX - x0);
    float ty = Smooth01(latticeY - y0);
    float h00 = Hash01(Hash(x0, y0, layer));
    float h10 = Hash01(Hash(x0 + 1, y0, layer));
    float h01 = Hash01(Hash(x0, y0 + 1, layer));
    float h11 = Hash01(Hash(x0 + 1, y0 + 1, layer));
    float value = Mathf.Lerp(
        Mathf.Lerp(h00, h10, tx),
        Mathf.Lerp(h01, h11, tx),
        ty);
    return (value * 2f - 1f) * .12f;
}
```

`Hash01` returns the lower 24 bits divided by `16777215f`. `Generate` calls `profile.TryValidateControlSettings` rather than full asset validation.

- [ ] **Step 5: Run GREEN and seed regression**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainControlMapTests -testResults /tmp/wastecity-first-terrain/task-02-green.xml -logFile /tmp/wastecity-first-terrain/task-02-green.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.WorldMapTests -testResults /tmp/wastecity-first-terrain/task-02-world-regression.xml -logFile /tmp/wastecity-first-terrain/task-02-world-regression.log
```

Expected: all focused tests pass and existing WorldMap behavior is unchanged.

- [ ] **Step 6: Commit Task 2**

```bash
git add Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMap3D.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMap3D.cs.meta Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMapGenerator3D.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMapGenerator3D.cs.meta Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs.meta
git diff --cached --check
git commit -m "feat: generate deterministic terrain control maps"
```

---

### Task 3: Build the Single Continuous Terrain Mesh

**Files:**
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainMeshBuilder3D.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainMeshBuilder3D.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainMeshTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainMeshTests.cs.meta`

**Interfaces:**
- Consumes: positive map width/height and existing XY→XZ mapping contract.
- Produces: `FirstArtTerrainMeshBuilder3D.Build(int width, int height)` returning an owned four-vertex `Mesh`.

- [ ] **Step 1: Write the failing mesh tests**

```csharp
[TestCase(32, 24, -16.5f, 15.5f, -12.5f, 11.5f)]
[TestCase(96, 64, -48.5f, 47.5f, -32.5f, 31.5f)]
public void Build_CoversCellCentersPlusHalfCell(
    int width,
    int height,
    float minX,
    float maxX,
    float minZ,
    float maxZ)
{
    Mesh mesh = FirstArtTerrainMeshBuilder3D.Build(width, height);
    Assert.That(mesh.vertexCount, Is.EqualTo(4));
    Assert.That(mesh.bounds.min.x, Is.EqualTo(minX).Within(.0001f));
    Assert.That(mesh.bounds.max.x, Is.EqualTo(maxX).Within(.0001f));
    Assert.That(mesh.bounds.min.z, Is.EqualTo(minZ).Within(.0001f));
    Assert.That(mesh.bounds.max.z, Is.EqualTo(maxZ).Within(.0001f));
}
```

Also assert six triangle indices, all normals `Vector3.up`, all tangents `(1,0,0,-1)`, UV corners `(0,0)` through `(1,1)`, Y exactly `0`, mesh name stable, and non-positive sizes throw `ArgumentOutOfRangeException` without creating a Mesh.

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainMeshTests -testResults /tmp/wastecity-first-terrain/task-03-red.xml -logFile /tmp/wastecity-first-terrain/task-03-red.log
```

Expected: only `FirstArtTerrainMeshBuilder3D` is missing.

- [ ] **Step 3: Implement the four-vertex mesh**

```csharp
public static class FirstArtTerrainMeshBuilder3D
{
    public static Mesh Build(int width, int height)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height));

        float minX = -width * .5f - .5f;
        float maxX = width * .5f - .5f;
        float minZ = -height * .5f - .5f;
        float maxZ = height * .5f - .5f;
        var mesh = new Mesh { name = "first-art.terrain.surface" };
        mesh.vertices = new[]
        {
            new Vector3(minX, 0f, minZ),
            new Vector3(maxX, 0f, minZ),
            new Vector3(minX, 0f, maxZ),
            new Vector3(maxX, 0f, maxZ)
        };
        mesh.normals = new[]
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up
        };
        mesh.tangents = new[]
        {
            new Vector4(1f, 0f, 0f, -1f),
            new Vector4(1f, 0f, 0f, -1f),
            new Vector4(1f, 0f, 0f, -1f),
            new Vector4(1f, 0f, 0f, -1f)
        };
        mesh.uv = new[]
        {
            Vector2.zero, Vector2.right, Vector2.up, Vector2.one
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
```

Do not add a Collider, grid subdivisions or saved Mesh assets.

- [ ] **Step 4: Run GREEN**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainMeshTests -testResults /tmp/wastecity-first-terrain/task-03-green.xml -logFile /tmp/wastecity-first-terrain/task-03-green.log
```

Expected: all mesh tests pass.

- [ ] **Step 5: Commit Task 3**

```bash
git add Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainMeshBuilder3D.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainMeshBuilder3D.cs.meta Assets/_Game/Tests/EditMode/FirstArtTerrainMeshTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainMeshTests.cs.meta
git diff --cached --check
git commit -m "feat: build continuous first art terrain mesh"
```

---

### Task 4: Generate Stable Texture Arrays and Preserve Source Assets

**Files:**
- Modify: `.gitattributes`
- Modify: `Assets/_Game/Editor/FirstArtPassImportPolicy.cs`
- Create: `Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs`
- Create: `Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs.meta`
- Modify: `Assets/_Game/Editor/WasteCity.Editor.asmdef`
- Modify: `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainAssetBuilderTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainAssetBuilderTests.cs.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset.meta`

**Interfaces:**
- Consumes: Task 1 layer catalog and the 28 existing imported PNG assets.
- Produces: `FirstArtTerrainAssetBuilder.BuildTextureArrays()`, four fixed asset paths, deterministic array content and stable GUIDs.

- [ ] **Step 1: Add focused LFS rules before generating large arrays**

Append exact path rules after the generic `.asset` rule:

```gitattributes
Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset filter=lfs diff=lfs merge=lfs -text
Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset filter=lfs diff=lfs merge=lfs -text
Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset filter=lfs diff=lfs merge=lfs -text
Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset filter=lfs diff=lfs merge=lfs -text
Docs/Art/FirstPass/Terrain/RuntimeIntegration/11-deep-water-motion.mp4 filter=lfs diff=lfs merge=lfs -text
```

Run the exact check and require five `filter: lfs` results before creating arrays or review media:

```bash
git check-attr filter -- Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset Docs/Art/FirstPass/Terrain/RuntimeIntegration/11-deep-water-motion.mp4
```

Do not add a broad `*.asset` LFS rule because pipeline, material and profile assets must remain normal Unity YAML.

- [ ] **Step 2: Write failing asset-builder tests**

Tests must capture all 28 source `.meta` GUIDs before and after two builds and assert equality. Core assertions:

```csharp
[Test]
public void BuildTextureArrays_UsesFrozenLayerOrderAndFormats()
{
    FirstArtTerrainAssetBuilder.BuildTextureArrays();
    Texture2DArray baseColor = LoadArray(
        FirstArtTerrainAssetBuilder.BaseColorArrayPath);
    Texture2DArray normal = LoadArray(
        FirstArtTerrainAssetBuilder.NormalArrayPath);
    Texture2DArray mask = LoadArray(
        FirstArtTerrainAssetBuilder.MaskArrayPath);
    Texture2DArray height = LoadArray(
        FirstArtTerrainAssetBuilder.HeightArrayPath);

    Assert.That(baseColor.depth, Is.EqualTo(7));
    Assert.That(baseColor.width, Is.EqualTo(2048));
    Assert.That(normal.depth, Is.EqualTo(7));
    Assert.That(mask.depth, Is.EqualTo(7));
    Assert.That(height.depth, Is.EqualTo(7));
    Assert.That(height.width, Is.EqualTo(1024));
    Assert.That(height.format, Is.EqualTo(TextureFormat.R8));
}
```

Also test: missing source aborts before modifying any existing generated asset; seven representative slice center pixels correspond to the matching source; Height 2×2 average and ushort→byte rounding are deterministic; generated arrays use Repeat and mipmaps; source importers end with `isReadable == false`; two builds preserve the four array GUIDs and `AssetDatabase.GetAssetDependencyHash` values.

- [ ] **Step 3: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainAssetBuilderTests -testResults /tmp/wastecity-first-terrain/task-04-red.xml -logFile /tmp/wastecity-first-terrain/task-04-red.log
```

Expected: compiler errors only for missing `FirstArtTerrainAssetBuilder` and its path constants.

- [ ] **Step 4: Freeze importer readability and add Editor assembly reference**

Add `WasteCity.ArtIntegration3D` to `WasteCity.Editor.asmdef` and add `WasteCity.Editor` to `WasteCity.EditModeTests.asmdef`, because the focused tests call the public editor builder directly.

In `ConfigureCommonTexture`, explicitly set readability from an exact-path temporary scope:

```csharp
importer.isReadable = TemporaryReadablePaths.Contains(assetPath);
```

Expose only inside the Editor assembly:

```csharp
internal static IDisposable AllowTemporaryReadability(string exactAssetPath);
```

The returned scope adds one approved Height path to a static `HashSet<string>` and removes it exactly once on Dispose. Static state starts empty after reload, so normal imports always end non-readable. Do not add new packages or change platform texture settings in the source `.meta` files.

- [ ] **Step 5: Implement the exact builder surface**

```csharp
public static class FirstArtTerrainAssetBuilder
{
    public const string BaseColorArrayPath =
        "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset";
    public const string NormalArrayPath =
        "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset";
    public const string MaskArrayPath =
        "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset";
    public const string HeightArrayPath =
        "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset";

    [MenuItem("WasteCity/Art/Build First Terrain Texture Arrays")]
    public static void BuildTextureArrays();
}
```

Implementation order:

1. Resolve 28 exact paths from the fixed seven names, not `FindAssets` order.
2. Validate all sources/importers and capture GUID, importer readability and dependency hash before mutation.
3. For BaseColor/Normal/Mask, require each channel's seven imported textures to share width, height, format and mip count; create a temporary `Texture2DArray` with the first source's `TextureFormat`, correct Linear flag and mipmaps, then copy every mip/slice using `Graphics.CopyTexture`.
4. For Height only, wrap one exact path at a time in `FirstArtPassImportPolicy.AllowTemporaryReadability(path)`, synchronously reimport, require imported format `R16`, read mip 0 via `GetPixelData<ushort>(0)`, average each non-overlapping 2×2 block with `(a + b + c + d + 2) / 4`, quantize with `(value + 128) / 257`, write a 1024 R8 slice and generate mipmaps. Dispose the scope in `finally`, synchronously reimport, and require `isReadable == false` before processing the next layer.
5. Restore all importers before touching persistent generated assets; assert the 28 GUIDs and final importer states match the captured values.
6. For a missing output asset use `AssetDatabase.CreateAsset`. For an existing output load it and use `EditorUtility.CopySerialized` from the temporary array so the `.meta` GUID remains unchanged.
7. Save assets, reimport four outputs, reload and validate dimensions/depth/order.
8. Destroy all temporary arrays/textures in `finally`.

Do not use `Texture2D.EncodeToPNG`, runtime `Resources.Load`, filesystem order or lossy Height interpolation.

- [ ] **Step 6: Run GREEN twice and verify LFS pointers**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainAssetBuilderTests -testResults /tmp/wastecity-first-terrain/task-04-green.xml -logFile /tmp/wastecity-first-terrain/task-04-green.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtPassImportPolicyTests -testResults /tmp/wastecity-first-terrain/task-04-import-regression.xml -logFile /tmp/wastecity-first-terrain/task-04-import-regression.log
git lfs status
git diff --check
```

Expected: builder tests and all import tests pass; only four generated `.asset` files are new LFS objects; existing 28 PNG and `.meta` paths have no diff.

- [ ] **Step 7: Commit Task 4**

```bash
git add .gitattributes Assets/_Game/Editor/FirstArtPassImportPolicy.cs Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs.meta Assets/_Game/Editor/WasteCity.Editor.asmdef Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef Assets/_Game/Tests/EditMode/FirstArtTerrainAssetBuilderTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainAssetBuilderTests.cs.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset.meta
git diff --cached --check
git lfs status
git commit -m "build: generate first terrain texture arrays"
```

---

### Task 5: Add the URP Master Shader, Material and Profile Asset

**Files:**
- Modify for approved review correction: `Docs/superpowers/specs/2026-08-10-first-terrain-runtime-integration-design.md`
- Modify for approved review correction: `Docs/superpowers/plans/2026-08-10-first-terrain-runtime-integration.md`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/MAT_Terrain_FirstPass.mat`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/MAT_Terrain_FirstPass.mat.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders.meta`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader.meta`
- Modify: `Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs`
- Modify: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainShaderTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainShaderTests.cs.meta`

**Interfaces:**
- Consumes: Task 1 Profile and Task 4 arrays.
- Produces: Shader `WasteCity/Terrain/FirstPassBlend`, `FirstArtTerrainAssetBuilder.BuildRuntimeAssets()`, material/profile paths and a valid serialized runtime profile.

- [ ] **Step 1: Write failing Shader/material/profile tests**

```csharp
[Test]
public void MasterShader_CompilesAndExposesFrozenProperties()
{
    Shader shader = Shader.Find(FirstArtTerrainProfile3D.RequiredShaderName);
    Assert.That(shader, Is.Not.Null);
    Assert.That(
        ShaderUtil.GetShaderMessages(shader)
            .Where(message => message.severity == ShaderCompilerMessageSeverity.Error),
        Is.Empty);
    Assert.That(shader.FindPropertyIndex("_BaseColorArray"), Is.GreaterThanOrEqualTo(0));
    Assert.That(shader.FindPropertyIndex("_NormalArray"), Is.GreaterThanOrEqualTo(0));
    Assert.That(shader.FindPropertyIndex("_MaskArray"), Is.GreaterThanOrEqualTo(0));
    Assert.That(shader.FindPropertyIndex("_HeightArray"), Is.GreaterThanOrEqualTo(0));
    Assert.That(shader.FindPropertyIndex("_ControlA"), Is.GreaterThanOrEqualTo(0));
    Assert.That(shader.FindPropertyIndex("_ControlB"), Is.GreaterThanOrEqualTo(0));
}

[Test]
public void BuildRuntimeAssets_CreatesValidMaterialAndProfileInPlace()
{
    FirstArtTerrainAssetBuilder.BuildRuntimeAssets();
    FirstArtTerrainProfile3D profile = AssetDatabase.LoadAssetAtPath<
        FirstArtTerrainProfile3D>(FirstArtTerrainAssetBuilder.ProfilePath);
    Assert.That(profile.TryValidate(out string error), Is.True, error);
    Assert.That(profile.Material.shader.name,
        Is.EqualTo(FirstArtTerrainProfile3D.RequiredShaderName));
}
```

Run builder twice and assert material/profile/Shader GUIDs and dependency hashes are stable. Add a negative test that a material using `Universal Render Pipeline/Lit` fails Profile validation with a precise Shader-name error.

Extend `FirstArtTerrainProfileTests` with three dimension-contract cases. Create all arrays with depth `FirstArtTerrainCatalog3D.LayerCount`, configure a Material using `FirstArtTerrainProfile3D.RequiredShaderName`, and assert these exact outcomes after all earlier validation gates pass:

```csharp
[Test]
public void Profile_AcceptsApprovedPrimaryAndHeightArrayDimensions()
{
    using (ConfiguredProfileScope scope = CreateConfiguredProfile(
        primarySize: 2048,
        heightSize: 1024))
    {
        Assert.That(scope.Profile.TryValidate(out string error), Is.True, error);
    }
}

[Test]
public void Profile_RejectsWrongHeightArrayDimensionsWithDeterministicError()
{
    using (ConfiguredProfileScope scope = CreateConfiguredProfile(
        primarySize: 2048,
        heightSize: 2048))
    {
        Assert.That(scope.Profile.TryValidate(out string error), Is.False);
        Assert.That(error, Is.EqualTo("Height array must be 1024x1024."));
    }
}

[Test]
public void Profile_RejectsMismatchedPrimaryArrayDimensionsWithDeterministicError()
{
    using (ConfiguredProfileScope scope = CreateConfiguredProfile(
        baseColorSize: 2048,
        normalSize: 1024,
        maskSize: 2048,
        heightSize: 1024))
    {
        Assert.That(scope.Profile.TryValidate(out string error), Is.False);
        Assert.That(
            error,
            Is.EqualTo("BaseColor, Normal, and Mask arrays must each be 2048x2048."));
    }
}
```

The test scope owns and destroys the transient Profile, Material, and four Texture2DArrays. It must exercise the public `Configure`/`TryValidate` contract with real Unity objects, not a mock or source-text assertion.

For review fix round 1, add mutation-sensitive Shader tests before production edits. Use real Shader compilation, Material pass discovery and Shader variants wherever Unity exposes the behavior. A narrowly scoped Shader-source contract is permitted only for fragment-path relationships that cannot be isolated through the Editor API. The tests must fail if any of these contracts is removed:

1. final `UniversalFragmentPBR` RGB is passed through URP `MixFog` while alpha is preserved;
2. Mask B gates only the sampled layer's tangent-space normal detail;
3. `_ADDITIONAL_LIGHTS_VERTEX` computes `VertexLighting` in the vertex stage, carries it through varyings and assigns `InputData.vertexLighting`;
4. the Material exposes a functional opaque `ShadowCaster` pass that compiles as a `PassType.ShadowCaster` variant;
5. the final weighted tangent-space normal uses finite/length checks and falls back to `(0,0,1)` rather than directly normalizing a zero or opposing blend.

Freeze the Mask B acceptance fixtures with decoded tangent-space normal `(0.6,0,0.8)`:

```text
B = 0.0 -> (0, 0, 1)
B = 1.0 -> (0.6, 0, 0.8)
B = 0.5 -> normalize(0.3, 0, 0.9) ~= (0.31622777, 0, 0.9486833)
```

The intermediate result must differ from both endpoints. Apply B after DeepWater's two moving normals are combined. No test or implementation may add a Detail texture, a new Shader property, or let B affect albedo, Metallic, AO, Smoothness, Height/control weights or any other channel.

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainShaderTests -testResults /tmp/wastecity-first-terrain/task-05-red.xml -logFile /tmp/wastecity-first-terrain/task-05-red.log
```

Expected: missing Shader and `BuildRuntimeAssets`/path constants; once those compile blockers are minimally resolved, the approved `2048/2048/2048 + 1024` Profile case still fails on the old all-dimensions-match rule. Record both RED causes before changing Profile validation. There must be no unrelated import errors.

- [ ] **Step 3: Implement the exact Shader property surface**

Declare these properties and URP includes:

```hlsl
Shader "WasteCity/Terrain/FirstPassBlend"
{
    Properties
    {
        _BaseColorArray("Base Color Array", 2DArray) = "" {}
        _NormalArray("Normal Array", 2DArray) = "" {}
        _MaskArray("Mask Array", 2DArray) = "" {}
        _HeightArray("Height Array", 2DArray) = "" {}
        _ControlA("Control A", 2D) = "white" {}
        _ControlB("Control B", 2D) = "black" {}
        _WorldOriginXZ("World Origin XZ", Vector) = (0,0,0,0)
        _WorldSizeXZ("World Size XZ", Vector) = (1,1,0,0)
        _CellsPerTexture("Cells Per Texture", Float) = 4
        _HeightBlendStrength("Height Blend Strength", Float) = 1
        _MacroVariation("Macro Variation", Float) = 0.05
        _WaterVelocityA("Water Velocity A", Vector) = (0.006,0.002,0,0)
        _WaterVelocityB("Water Velocity B", Vector) = (-0.003,0.005,0,0)
        _WaterNormalScaleB("Water Normal Scale B", Float) = 1.17
        _WaterHighlightStrength("Water Highlight Strength", Float) = 0.12
    }
```

Use `Core.hlsl` and `Lighting.hlsl`, a URP forward pass with `UniversalFragmentPBR`, opaque surface, ZWrite On, back-face culling, no transparency/refraction and no vertex displacement.

Update `FirstArtTerrainProfile3D.TryValidate` without changing its existing gate order: control settings, Material presence, exact Shader name, each array's presence, then the shared depth check. After those gates, require BaseColor, Normal, and Mask to each be exactly `2048x2048`; preserve the established Task 4 BaseColor sRGB and Normal/Mask linear format semantics. Validate Height independently as exactly `1024x1024`, linear `R8`, with depth `7` already covered by the shared depth gate. Return deterministic errors in this order:

```text
BaseColor, Normal, and Mask arrays must each be 2048x2048.
Height array must be 1024x1024.
```

The approved runtime combination is therefore BaseColor/Normal/Mask `2048x2048`, Height `1024x1024`, and every array depth `7`; do not restore the superseded requirement that Height dimensions match the three primary arrays.

- [ ] **Step 4: Implement bounded layer sampling and PBR blending**

The fragment path must:

```hlsl
float2 mapUV = saturate(
    (input.positionWS.xz - _WorldOriginXZ.xy) /
    max(_WorldSizeXZ.xy, float2(0.0001, 0.0001)));
float4 baseWeights = SAMPLE_TEXTURE2D(_ControlA, sampler_ControlA, mapUV);
float4 specialWeights = SAMPLE_TEXTURE2D(_ControlB, sampler_ControlB, mapUV);
```

Then extract exactly seven weights, normalize, select the highest three with deterministic index tie-breaking, and sample only those three array slices. For each selected layer sample BaseColor, Normal, Mask and Height with world UV `input.positionWS.xz / _CellsPerTexture`. Multiply each weight by a bounded Height term, normalize again, blend all PBR channels, and unpack tangent-space normals before combining them.

Mask B is not discarded after Mask blending. For each selected layer, safely normalize `lerp(float3(0,0,1), decodedNormalTS, saturate(mask.b))`, then include that gated per-layer normal in weighted blending. For DeepWater, combine the two moving decoded normals first and apply the same B gate to that combined layer normal. `B=0` is flat, `B=1` is full decoded/animated detail, and intermediate B is the continuous safely normalized interpolation. B has no effect on any non-normal output and introduces no new texture or property.

When a selected index equals `5` (DeepWater), sample that same normal slice again at `worldUV * _WaterNormalScaleB + _Time.y * _WaterVelocityB`; the first water sample uses `_Time.y * _WaterVelocityA`. Blend the two normals and apply only a small Smoothness/highlight modulation. No branch may sample a seventh full PBR set.

Add this low-frequency deterministic macro tint, with `_MacroVariation` clamped to `[0,.05]`; it must not alter control weights or gameplay boundaries:

```hlsl
float macroWave =
    sin(input.positionWS.x * 0.071) * 0.5 +
    sin(input.positionWS.z * 0.053) * 0.35 +
    sin((input.positionWS.x + input.positionWS.z) * 0.031) * 0.15;
float macroTint = 1.0 + macroWave * saturate(_MacroVariation);
blendedBaseColor.rgb *= macroTint;
```

- [ ] **Step 5: Extend the builder for material/profile in-place creation**

Add constants:

```csharp
public const string MaterialPath =
    "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/MAT_Terrain_FirstPass.mat";
public const string ProfilePath =
    "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset";
public const string ShaderPath =
    "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader";

[MenuItem("WasteCity/Art/Build First Terrain Runtime Assets")]
public static void BuildRuntimeAssets();
```

`BuildRuntimeAssets` calls `BuildTextureArrays`, loads the exact Shader, updates an existing Material in place or creates it, assigns all four arrays, updates/creates the Profile in place via `Configure`, validates, saves, reloads and validates again. No generated Material instance is kept outside the asset.

- [ ] **Step 6: Run GREEN and a batchmode Shader compilation**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainShaderTests -testResults /tmp/wastecity-first-terrain/task-05-green.xml -logFile /tmp/wastecity-first-terrain/task-05-green.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -logFile /tmp/wastecity-first-terrain/task-05-compile.log
```

Expected: focused tests pass; compile exits 0; logs contain no `Shader error`, `error CS`, `Compilation failed` or pink fallback warning.

- [ ] **Step 7: Commit Task 5**

```bash
git add Docs/superpowers/specs/2026-08-10-first-terrain-runtime-integration-design.md Docs/superpowers/plans/2026-08-10-first-terrain-runtime-integration.md Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/MAT_Terrain_FirstPass.mat Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/MAT_Terrain_FirstPass.mat.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader.meta Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainShaderTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainShaderTests.cs.meta
git diff --cached --check
git commit -m "feat: add first terrain URP master material"
```

---

### Task 6: Add Atomic Formal Presentation and Graybox Fallback

**Files:**
- Create: `Assets/_Game/Scripts/Graybox3D/IGrayboxTerrainPresentation3D.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/IGrayboxTerrainPresentation3D.cs.meta`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs.meta`
- Modify: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainLayer3D.cs`
- Modify: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMap3D.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainRendererTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainRendererTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs`

**Interfaces:**
- Consumes: Tasks 1–5 runtime data, arrays and Shader; existing `GrayboxWorldView3D.Model/Coordinates`.
- Produces: optional `IGrayboxTerrainPresentation3D`, `GrayboxWorldView3D.SetSurfaceFallbackVisible(bool)`, overloaded Bootstrap configuration and `FirstArtTerrainRenderer3D` atomic lifecycle.

- [ ] **Step 1: Write failing graybox visibility tests**

First freeze the shared catalog boundary with TDD: the existing four-only
`FirstArtTerrainCatalog3D.IsSurfaceStableId` behavior must RED for `Ruins`,
`DeepWater`, and `Cliff`, then GREEN for the exact seven IDs returned by
`StableIdOf` layers `0..6`. It must remain false for representative resource
and unrelated IDs, plus null and empty IDs.

Generate the 12-slot catalog map and assert:

```csharp
[Test]
public void SetSurfaceFallbackVisible_OnlyChangesSevenSurfaceRenderers()
{
    GrayboxWorldView3D view = CreateCatalogView();
    view.SetSurfaceFallbackVisible(false);
    foreach (GrayboxVisualSlot slot in view.GetComponentsInChildren<GrayboxVisualSlot>(true))
    {
        bool isSurface = FirstArtTerrainCatalog3D.IsSurfaceStableId(slot.StableId);
        Assert.That(slot.Renderer.enabled, Is.EqualTo(!isSurface));
    }

    view.SetSurfaceFallbackVisible(true);
    Assert.That(
        view.GetComponentsInChildren<GrayboxVisualSlot>(true)
            .All(slot => slot.Renderer.enabled),
        Is.True);
}
```

Also test calling visibility before Generate is safe, Generate after hidden applies the requested state to new surface slots, `ClearGenerated` discards slot tracking without touching unrelated renderers, and disabling then re-enabling a real presenter restores exactly one formal surface.

- [ ] **Step 2: Write failing presenter/bootstrap tests**

Use a fake `MonoBehaviour, IGrayboxTerrainPresentation3D` to prove:

- optional null presenter keeps current initialization behavior;
- successful presenter is called exactly once after `worldView.Generate` and sees non-null Model/Coordinates;
- false/throwing presenter leaves graybox visible and Bootstrap initialized;
- calling Initialize twice does not rebuild or call presenter twice;
- disabled/destroyed real `FirstArtTerrainRenderer3D` restores all seven graybox renderers;
- missing profile, wrong Shader, wrong array depth and control generation exception create no persistent formal object.

Also freeze the review-fix contracts with focused failing tests:

- a false or throwing presenter that owns a partial GameObject/resource is
  best-effort cleared by Bootstrap before fallback restoration and completed
  initialization; cleanup failure is combined with the presentation failure in
  one contextual error and fallback is still restored;
- a real presenter failure routed through Bootstrap emits exactly one
  contextual error per attempt, with one explicit logging owner and no Update
  logging;
- changing a presenter's Profile clears its old owned presentation and restores
  fallback; replacing Bootstrap's world/presenter clears and detaches the old
  lifecycle owner, resets stale initialization state, and allows the new
  configuration to initialize;
- after a successful formal presentation, an external `worldView.Generate`
  clears the old Mesh/control maps before replacing graybox slots, keeps
  fallback visible during the rebuild, and re-presents exactly one formal
  surface only after successful generation; Bootstrap's initial Generate must
  not duplicate presentation;
- inject a constructor fault immediately after Control A texture allocation and
  assert the partial texture is destroyed, texture counts return to baseline,
  and normal generation remains unchanged.

- [ ] **Step 3: Run RED**

```bash
for test_class in FirstArtTerrainRendererTests GrayboxSceneBootstrapTests GrayboxVisualAndWorldTests; do
  "$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter "WasteCity.Tests.$test_class" -testResults "/tmp/wastecity-first-terrain/task-06-red-$test_class.xml" -logFile "/tmp/wastecity-first-terrain/task-06-red-$test_class.log"
done
```

Expected: missing interface, renderer and public visibility API; existing unrelated tests still compile once new tests are excluded.

- [ ] **Step 4: Add the minimal Graybox boundary**

```csharp
public interface IGrayboxTerrainPresentation3D
{
    bool TryPresent(GrayboxWorldView3D worldView);
    void ClearPresentation();
}
```

`GrayboxWorldView3D` stores generated surface slots in a private list only when their ID passes its own exact seven-ID switch; it exposes:

```csharp
public bool SurfaceFallbackVisible { get; }
public void SetSurfaceFallbackVisible(bool visible);
```

Do not make `GrayboxWorldView3D` reference `FirstArtTerrainCatalog3D`; duplicate only the frozen exact seven stable ID strings in a private `IsSurfaceSlot` switch that must match the catalog, preserving assembly direction. Tests compare it against the Task 1 catalog.

Add an assembly-safe explicit presentation lifecycle/transaction registration
owned by `GrayboxWorldView3D`. A successfully attached presentation is cleared
before `Generate` replaces slots, retained locally only for the rebuild
transaction, and re-presented after generation succeeds. Initial Bootstrap
generation has no attached presenter, so Bootstrap performs the one initial
presentation attempt without duplication. Detach on explicit clear,
reconfiguration, disable, and destroy.

Extend Bootstrap without breaking old callers:

```csharp
[SerializeField] private MonoBehaviour terrainPresentationBehaviour;

public void Configure(
    GrayboxUrpScope renderScope,
    GrayboxWorldView3D worldView);

public void Configure(
    GrayboxUrpScope renderScope,
    GrayboxWorldView3D worldView,
    MonoBehaviour terrainPresentationBehaviour);
```

After `worldView.Generate(World)`, cast the optional behavior to the interface. On false or exception, force `SetSurfaceFallbackVisible(true)`, log once with component context, then still set `IsInitialized = true`.

On false or exception, Bootstrap first best-effort calls
`ClearPresentation`, then restores fallback and completes initialization. If
cleanup also throws, preserve both the attempt and cleanup information in one
contextual error. Bootstrap is the single logging owner for Bootstrap-routed
attempts; the real presenter exposes a narrow assembly-safe suppressed-log
attempt path and last error, while direct/runtime lifecycle attempts retain one
presenter-owned error. Replacing configured dependencies performs the same
best-effort detach/clear, resets `World` and `IsInitialized`, then permits the
new configuration to initialize.

- [ ] **Step 5: Implement the formal renderer with atomic ownership**

Use exact public surface:

```csharp
public sealed class FirstArtTerrainRenderer3D : MonoBehaviour,
    IGrayboxTerrainPresentation3D
{
    public FirstArtTerrainProfile3D Profile { get; }
    public bool IsPresented { get; }
    public MeshRenderer SurfaceRenderer { get; }
    public FirstArtTerrainControlMap3D ControlMaps { get; }

    public void Configure(FirstArtTerrainProfile3D profile);
    public bool TryPresent(GrayboxWorldView3D worldView);
    public void ClearPresentation();
}
```

`TryPresent` order is fixed:

1. `ClearPresentation` current owned state;
2. validate component enabled, Profile, `worldView.Model` and Coordinates;
3. generate Mesh and control maps into locals;
4. create child `RuntimeSurface`, add MeshFilter/MeshRenderer, assign `sharedMaterial = Profile.Material`;
5. set arrays, controls, world origin/size, cells-per-texture, Height and water parameters in one reusable `MaterialPropertyBlock`;
6. verify renderer/material/mesh/control references;
7. transfer local ownership to fields;
8. set `worldView.SetSurfaceFallbackVisible(false)` last;
9. return true.

On any failure destroy locals, leave no child, restore graybox and return false after one explicit error. `OnDisable` and `OnDestroy` call `ClearPresentation`; it destroys only the owned Mesh, control maps and generated child, clears the property block, restores graybox and is idempotent. Never instantiate a Material.

Retain the most recently supplied `GrayboxWorldView3D` across `OnDisable`. `OnEnable` re-runs `TryPresent` only when that retained view still has non-null Model and Coordinates. `OnDestroy` restores graybox and clears the retained source so a destroyed component cannot resurrect. The first scene enable before Bootstrap has supplied a view is a no-op.

`Configure` changing the Profile must call `ClearPresentation` before storing
the replacement so stale Mesh/control-map ownership cannot survive dependency
changes. Successful presentation attaches to the Graybox lifecycle transaction;
every clear path detaches while retaining only the source needed for legal
disable/re-enable behavior.

Strengthen `FirstArtTerrainControlMap3D` construction with local texture
ownership and catch cleanup: if any exception occurs after Control A allocation
but before both texture references transfer to the completed wrapper, destroy
every local texture and rethrow. Provide one test-only static fault seam
immediately after Control A allocation; it is null in normal runtime, changes no
public runtime behavior, and introduces no per-frame work or allocation.

- [ ] **Step 6: Run GREEN and allocation/lifecycle checks**

```bash
for test_class in FirstArtTerrainRendererTests GrayboxSceneBootstrapTests GrayboxVisualAndWorldTests; do
  "$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter "WasteCity.Tests.$test_class" -testResults "/tmp/wastecity-first-terrain/task-06-green-$test_class.xml" -logFile "/tmp/wastecity-first-terrain/task-06-green-$test_class.log"
done
```

Expected: all focused tests pass; disabling/re-enabling and external world
rebuild do not duplicate `RuntimeSurface`; stale Mesh/control textures are
destroyed; reconfiguration restores fallback and resets Bootstrap state;
partial Control A construction leaks zero textures; real Bootstrap-routed
failure logs exactly once; resource renderers never disable; and
`Renderer.sharedMaterial` remains the profile asset.

- [ ] **Step 7: Commit Task 6**

```bash
git add Assets/_Game/Scripts/Graybox3D/IGrayboxTerrainPresentation3D.cs Assets/_Game/Scripts/Graybox3D/IGrayboxTerrainPresentation3D.cs.meta Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs.meta Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainLayer3D.cs Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMap3D.cs Assets/_Game/Tests/EditMode/FirstArtTerrainRendererTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainRendererTests.cs.meta Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs Assets/_Game/Tests/EditMode/GrayboxSceneBootstrapTests.cs Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs
git diff --cached --check
git commit -m "feat: present formal terrain with graybox fallback"
```

---

### Task 7: Author and Validate the Formal Terrain Scene Contract

**Files:**
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs.meta`

**Interfaces:**
- Consumes: Task 5 `FirstArtTerrainAssetBuilder.BuildRuntimeAssets()` and Task 6 presenter/Bootstrap interfaces.
- Produces: one serialized `GrayboxWorld/FirstArtTerrainPresentation`, Profile reference, optional Bootstrap reference and idempotent authoring validation.

- [ ] **Step 1: Capture pre-authoring identity and write failing scene tests**

Before any authoring run, save:

```bash
mkdir -p /tmp/wastecity-first-terrain/task-07
git rev-parse HEAD:Assets/_Game/Scenes/GrayboxPrototype3D.unity > /tmp/wastecity-first-terrain/task-07/scene-before.txt
for path in Assets/_Game/Scenes/GrayboxPrototype3D.unity.meta Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset.meta Assets/_Game/Rendering/Graybox3D/GrayboxUniversalRenderer.asset.meta Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat.meta; do
  shasum -a 256 "$path"
done > /tmp/wastecity-first-terrain/task-07/foundation-meta-before.txt
```

Create EditMode tests that open the real scene and require:

```csharp
[Test]
public void Scene_HasOneSerializedFirstArtTerrainPresentation()
{
    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    FirstArtTerrainRenderer3D[] presenters =
        Object.FindObjectsOfType<FirstArtTerrainRenderer3D>(true);
    Assert.That(presenters.Length, Is.EqualTo(1));
    Assert.That(
        presenters[0].transform.parent.name,
        Is.EqualTo("GrayboxWorld"));
    Assert.That(presenters[0].name,
        Is.EqualTo("FirstArtTerrainPresentation"));
    Assert.That(presenters[0].Profile,
        Is.SameAs(AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
            FirstArtTerrainAssetBuilder.ProfilePath)));
}
```

Also require the Bootstrap serialized `terrainPresentationBehaviour` points to that component; no serialized `RuntimeSurface` exists; no Collider is added under the presenter; Build Settings stay Graybox index 0 and Formal index 1; both scene and profile reference the approved Shader/material/arrays.

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainSceneContractTests -testResults /tmp/wastecity-first-terrain/task-07-red.xml -logFile /tmp/wastecity-first-terrain/task-07-red.log
```

Expected: scene hierarchy/reference assertions fail because the presenter is not authored; existing foundation contract remains green.

- [ ] **Step 3: Add an incremental authoring method**

Inside `GrayboxSceneAuthoring.Configure()` call `FirstArtTerrainAssetBuilder.BuildRuntimeAssets()` before scene mutation, then after `EnsureBuildingContract` call:

```csharp
private static void EnsureFirstArtTerrainContract(Scene scene)
{
    GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
    Transform world = RequireChild(root.transform, "GrayboxWorld");
    Transform owner = EnsureChild(world, "FirstArtTerrainPresentation");
    FirstArtTerrainRenderer3D presenter =
        EnsureComponent<FirstArtTerrainRenderer3D>(owner);
    FirstArtTerrainProfile3D profile =
        AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
            FirstArtTerrainAssetBuilder.ProfilePath);
    if (profile == null || !profile.TryValidate(out string error))
        throw new InvalidOperationException(error);
    presenter.Configure(profile);
    SetReferences(
        RequireSingle<GrayboxSceneBootstrap>(scene),
        ("terrainPresentationBehaviour", presenter));
    EditorSceneManager.MarkSceneDirty(scene);
}
```

The actual implementation must use existing authoring helpers and validate the foundation before mutation. It must not rebuild the scene, remove Building objects, change the camera, URP pipeline, GrayboxLit or Build Settings.

- [ ] **Step 4: Extend scene validation without weakening foundation validation**

Add `ValidateFirstArtTerrainContract(Scene)` after authoring and on the existing-scene path only after runtime assets are ensured. Validate exact object count, owner, Profile, Material, four arrays, Shader name, Bootstrap reference, no RuntimeSurface serialization and no Collider. Do not make the original foundation scene creation depend on pre-existing art assets before the builder has a chance to create them.

- [ ] **Step 5: Run authoring twice and compare identities**

```bash
export WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-first-terrain/task-07/identity-before.json
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity -logFile /tmp/wastecity-first-terrain/task-07/identity-before.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure -logFile /tmp/wastecity-first-terrain/task-07/authoring-1.log
shasum -a 256 Assets/_Game/Scenes/GrayboxPrototype3D.unity > /tmp/wastecity-first-terrain/task-07/scene-after-1.sha
export WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-first-terrain/task-07/identity-after-1.json
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity -logFile /tmp/wastecity-first-terrain/task-07/identity-after-1.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure -logFile /tmp/wastecity-first-terrain/task-07/authoring-2.log
shasum -a 256 Assets/_Game/Scenes/GrayboxPrototype3D.unity > /tmp/wastecity-first-terrain/task-07/scene-after-2.sha
export WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-first-terrain/task-07/identity-after-2.json
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity -logFile /tmp/wastecity-first-terrain/task-07/identity-after-2.log
cmp /tmp/wastecity-first-terrain/task-07/identity-before.json /tmp/wastecity-first-terrain/task-07/identity-after-1.json
cmp /tmp/wastecity-first-terrain/task-07/identity-after-1.json /tmp/wastecity-first-terrain/task-07/identity-after-2.json
cmp /tmp/wastecity-first-terrain/task-07/scene-after-1.sha /tmp/wastecity-first-terrain/task-07/scene-after-2.sha
```

Expected: existing foundation GlobalObjectIds remain unchanged, and the second authoring run produces identical final scene bytes. If adding the one new object makes the pre-authoring identity payload differ because the capture method enumerates it, compare only the pre-existing named entries and freeze the new presenter's GlobalObjectId between pass 1 and pass 2.

- [ ] **Step 6: Run GREEN and existing scene contracts**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainSceneContractTests -testResults /tmp/wastecity-first-terrain/task-07-green.xml -logFile /tmp/wastecity-first-terrain/task-07-green.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxSceneContractTests -testResults /tmp/wastecity-first-terrain/task-07-foundation-regression.xml -logFile /tmp/wastecity-first-terrain/task-07-foundation-regression.log
git diff --check
```

Expected: both focused classes pass; the scene contains one serialized presenter and zero serialized runtime surfaces; foundation GUID list is stable.

- [ ] **Step 7: Commit Task 7**

```bash
git add Assets/_Game/Editor/GrayboxSceneAuthoring.cs Assets/_Game/Scenes/GrayboxPrototype3D.unity Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs.meta
git diff --cached --check
git commit -m "feat: author first terrain presentation scene"
```

---

### Task 8: Prove the Real Runtime Scene and Gameplay Remain Intact

**Files:**
- Create: `Assets/_Game/Tests/PlayMode/FirstArtTerrainRuntimeSceneTests.cs`
- Create: `Assets/_Game/Tests/PlayMode/FirstArtTerrainRuntimeSceneTests.cs.meta`
- Modify: `Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef`

**Interfaces:**
- Consumes: authored `GrayboxPrototype3D`, real Bootstrap Update lifecycle, existing virtual keyboard/mouse fixture pattern.
- Produces: real scene proof for one formal Renderer, seven hidden graybox surface groups, resource preservation, disable/re-enable fallback and unchanged movement/building projection.

- [ ] **Step 1: Add the direct ArtIntegration reference and failing PlayMode scene tests**

Add `WasteCity.ArtIntegration3D` to PlayMode asmdef. Reuse the public Input System setup/restore pattern from `GrayboxRuntimeSceneTests`; do not invoke private ticks, `ProcessFrame`, `TickMovement`, `TickCamera` or direct building placement methods.

Core runtime assertion:

```csharp
[UnityTest]
public IEnumerator Scene_AtomicallyShowsOneFormalTerrainAndKeepsResources()
{
    FirstArtTerrainRenderer3D presenter =
        Object.FindObjectOfType<FirstArtTerrainRenderer3D>();
    GrayboxWorldView3D world =
        Object.FindObjectOfType<GrayboxWorldView3D>();
    Assert.That(presenter.IsPresented, Is.True);
    Assert.That(presenter.SurfaceRenderer, Is.Not.Null);
    Assert.That(presenter.SurfaceRenderer.sharedMaterial,
        Is.SameAs(presenter.Profile.Material));
    Assert.That(
        Object.FindObjectsOfType<FirstArtTerrainRenderer3D>().Length,
        Is.EqualTo(1));
    Assert.That(
        presenter.GetComponentsInChildren<MeshRenderer>().Length,
        Is.EqualTo(1));
    Assert.That(
        presenter.GetComponentsInChildren<Collider>(),
        Is.Empty);
    Assert.That(world.SurfaceFallbackVisible, Is.False);
    Assert.That(VisibleResourceSlots(world), Is.Not.Empty);
    yield return null;
}
```

Add tests for: disable presenter → formal child removed and all seven graybox renderers visible in the next frame; re-enable presenter → exactly one formal surface returns and graybox surfaces hide; map controls match all seed 8128 cells; unload scene restores no leaked runtime Mesh/Texture; shared material does not change across frames.

- [ ] **Step 2: Add real input and mathematical-ground regression tests**

Through virtual devices and real frames prove:

- Mobile WASD changes city XZ while formal terrain remains at Y=0 and has no Collider;
- right-click A* reduces distance to destination across real FixedUpdate frames;
- `F` deployment and Packing behavior remain unchanged;
- `B` opens the real building catalog and a valid exterior preview still resolves from the existing mathematical ground projector;
- invalid DeepWater/Cliff placement remains rejected by rules, not by the formal Mesh;
- camera middle drag/Home continues to work while formal terrain is active.

Assertions must observe public state and transforms; they must not call router or controller tick methods directly.

- [ ] **Step 3: Prove the new scene tests detect broken lifecycle wiring**

Temporarily change only `FirstArtTerrainRenderer3D.OnEnable` to return before re-presentation, then run the new class:

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.FirstArtTerrainRuntimeSceneTests -testResults /tmp/wastecity-first-terrain/task-08-red.xml -logFile /tmp/wastecity-first-terrain/task-08-red.log
```

Expected: the disable/re-enable test fails while unrelated runtime tests pass. Revert that exact temporary line with `apply_patch`, confirm the production file hash equals its pre-mutation hash, and do not commit the mutation.

- [ ] **Step 4: Run the unmodified production class and enforce the file boundary**

Run the new class again after restoring the production hash. Expected: all tests pass. If any genuine failure remains, stop and revise Task 6/8 file boundaries before editing production; Task 8 is otherwise test-only. The forbidden direct-call scan must find no use of:

```text
ProcessFrame(
TickMovement(
TickDeployment(
TickControl(
TickCamera(
UpdatePointer(
```

- [ ] **Step 5: Run GREEN and existing real-scene suites**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.FirstArtTerrainRuntimeSceneTests -testResults /tmp/wastecity-first-terrain/task-08-green.xml -logFile /tmp/wastecity-first-terrain/task-08-green.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxRuntimeSceneTests -testResults /tmp/wastecity-first-terrain/task-08-graybox-runtime.xml -logFile /tmp/wastecity-first-terrain/task-08-graybox-runtime.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests -testResults /tmp/wastecity-first-terrain/task-08-building-runtime.xml -logFile /tmp/wastecity-first-terrain/task-08-building-runtime.log
```

Expected: all three classes pass with zero failures and no Missing Shader/Script, NullReferenceException or input-settings restoration failure.

- [ ] **Step 6: Commit Task 8**

Stage only the test files and asmdef:

```bash
git add Assets/_Game/Tests/PlayMode/FirstArtTerrainRuntimeSceneTests.cs Assets/_Game/Tests/PlayMode/FirstArtTerrainRuntimeSceneTests.cs.meta Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef
git diff --cached --check
git commit -m "test: verify first terrain runtime scene"
```

---

### Task 9: Enforce Performance, Builds and Visual Acceptance

**Files:**
- Modify: `Assets/_Game/Editor/GrayboxPerformanceProbe.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs.meta`
- Modify after the measured 96×64 probe exceeds 250 ms and profiling isolates control-map generation as the bottleneck: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMapGenerator3D.cs`
- Modify with that performance correction: `Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs`
- Modify after rejected visual review only: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainProfile3D.cs`
- Modify after rejected visual review only: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader`
- Modify after rejected visual review only: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset`
- Modify after rejected visual review only: `Assets/_Game/Tests/EditMode/FirstArtTerrainProfileTests.cs`
- Modify after rejected visual review only: `Assets/_Game/Tests/EditMode/FirstArtTerrainShaderTests.cs`
- Modify after the real GUI memory capture exceeds the approved budget: `Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs`
- Modify with that runtime-array correction: `Assets/_Game/Tests/EditMode/FirstArtTerrainAssetBuilderTests.cs`
- Regenerate in place with the same GUID and non-readable runtime storage: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset`
- Regenerate in place with the same GUID and non-readable runtime storage: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset`
- Regenerate in place with the same GUID: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset`
- Regenerate in place with the same GUID and non-readable runtime storage: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset`
- Create for deterministic native-resolution evidence: `Assets/_Game/Editor/FirstArtTerrainEvidenceCapture.cs`
- Create: `Assets/_Game/Editor/FirstArtTerrainEvidenceCapture.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainEvidenceCaptureTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtTerrainEvidenceCaptureTests.cs.meta`
- Create after capture: `Docs/Art/FirstPass/Terrain/RuntimeIntegration/` approved PNG screenshots and one MP4 recording only after the user approves committing them

**Interfaces:**
- Consumes: complete runtime scene and existing four Windows build methods.
- Produces: `GrayboxPerformanceProbe.MeasureFirstArtTerrainPerformance()`, JSON generation metrics, real GUI Profiler evidence, three build logs and user-facing visual evidence.

- [ ] **Step 1: Write failing structural/allocation/performance tests**

```csharp
[Test]
public void PerformanceProbe_ExposesFirstArtTerrainEntryPoint()
{
    Type probe = FindLoadedType("WasteCity.Editor.GrayboxPerformanceProbe");
    MethodInfo method = probe.GetMethod(
        "MeasureFirstArtTerrainPerformance",
        BindingFlags.Public | BindingFlags.Static);
    Assert.That(method, Is.Not.Null);
    Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
    Assert.That(method.GetParameters(), Is.Empty);
}

[Test]
public void ExpandedMap_UsesOneFormalRendererAndNoPerCellObjects()
{
    TerrainPerformanceFixture fixture = CreateFixture(96, 64, 8128);
    Assert.That(fixture.Presenter.IsPresented, Is.True);
    Assert.That(fixture.Presenter.GetComponentsInChildren<MeshRenderer>().Length,
        Is.EqualTo(1));
    Assert.That(fixture.Presenter.GetComponentsInChildren<Transform>().Length,
        Is.LessThanOrEqualTo(2));
    Assert.That(fixture.Presenter.GetComponentsInChildren<Collider>(), Is.Empty);
}
```

After warm-up, use both `GC.GetAllocatedBytesForCurrentThread` and `ProfilerRecorder` to verify 300 stable frames/calls allocate 0 B for presenter lifecycle idle and Shader-driven water has no CPU Update sample. Include a positive-control allocation so a broken recorder cannot report false zero.

- [ ] **Step 2: Run RED**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainPerformanceTests -testResults /tmp/wastecity-first-terrain/task-09-red.xml -logFile /tmp/wastecity-first-terrain/task-09-red.log
```

Expected: only missing performance probe entry point fails; existing structure may already pass.

- [ ] **Step 3: Add a five-run 96×64 performance probe**

Add environment variable and JSON contract:

```csharp
private const string FirstTerrainResultEnvironmentVariable =
    "WASTECITY_FIRST_TERRAIN_PERF_RESULT";

public static void MeasureFirstArtTerrainPerformance();
```

The method loads the approved Profile, creates an isolated configured Graybox world for `96×64`, measures exactly five full `world.Generate + presenter.TryPresent + presenter.ClearPresentation` runs, sorts a clone to report the median, and records:

```text
seed
width
height
generationMilliseconds[5]
medianMilliseconds
formalRendererCount
formalPersistentObjectCount
controlWidth
controlHeight
managedAllocationBytesAfterWarmup
```

Destroy all temporary objects/assets in `finally`. Require median ≤250 ms, formal renderer `1`, persistent formal object count ≤1, control dimensions `384×256`, stable allocation `0 B`.

- [ ] **Step 4: Run focused GREEN and the five-run probe**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainPerformanceTests -testResults /tmp/wastecity-first-terrain/task-09-green.xml -logFile /tmp/wastecity-first-terrain/task-09-green.log
export WASTECITY_FIRST_TERRAIN_PERF_RESULT=/tmp/wastecity-first-terrain/task-09-performance.json
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.MeasureFirstArtTerrainPerformance -logFile /tmp/wastecity-first-terrain/task-09-performance.log
```

Expected: focused tests pass, JSON contains five samples and median ≤250 ms.

- [ ] **Step 4A: Correct the measured control-map allocation bottleneck if and only if the probe exceeds 250 ms**

This boundary is active only after all of the following evidence exists:

```text
the 96×64 probe has five real samples and median >250 ms;
world.Generate, presenter.TryPresent and presenter.ClearPresentation have been timed separately;
direct mesh and control-map generation have been timed separately;
control-map generation is the dominant measured cost;
temporary diagnostic instrumentation has been removed before production edits.
```

The accepted evidence for this run is a `393.7867 ms` median, with world
generation approximately `21–32 ms`, presentation approximately `371–415 ms`,
clear approximately `0.02–0.12 ms`, direct mesh `0.0299 ms`, and direct
control-map generation `422.5676 ms`. The generator currently creates a new
`float[7]`, `int[3]`, and `int[7]` for each of `384×256` pixels.

First add a mutation-sensitive allocation/parity test to
`FirstArtTerrainControlMapTests.cs`. It must:

```text
generate the 96×64 seed-8128 map with the unmodified generator and record the
SHA-256 of ControlABytes followed by ControlBBytes as a literal frozen baseline;
require that literal digest plus byte-for-byte equality across repeated runs;
verify the existing seven-layer, transition-byte, normalization, three-weight,
border and deterministic contracts remain unchanged;
warm the generator, then measure one complete 96×64 Generate call with both
GC.GetAllocatedBytesForCurrentThread and a ProfilerRecorder;
assert a fixed upper bound that includes the required layer grid, two encoded
byte arrays, two Texture2D objects and result wrapper, but is far below the
original roughly 98,304×3 temporary-array allocation count;
assert the ProfilerRecorder allocation sample count is bounded by a small
constant independent of pixel count, and prove the recorder with a separate
positive-control allocation.
```

Choose the numeric byte/sample bounds from a recorded unoptimized 32×24 and
96×64 diagnostic so the original 96×64 path fails and the bound still permits
all required output allocations. Record those diagnostics and the frozen digest
in the Task 9 report. Obtain RED by adding the bounds before the optimization;
the unmodified generator must fail the allocation bound while its digest passes.
The failure must not be a compile or fixture failure. Do not commit diagnostic
instrumentation.

Then change only `FirstArtTerrainControlMapGenerator3D.cs`: allocate fixed
workspace buffers once per `Generate` call and clear/reuse them for each pixel.
Preserve layer priority, blend math, noise, maximum-three-layer selection,
rounding, fallback logging, exact byte encoding and public API. Do not cache
across calls, introduce static mutable workspace, change Profile/Shader/assets,
lower the 250 ms target, reduce dimensions, skip pixels, or move work outside
the measured operation.

Run the complete `FirstArtTerrainControlMapTests` and
`FirstArtTerrainPerformanceTests`, then rerun the same five-run 96×64 probe.
Expected: all focused tests pass, encoded bytes remain deterministic, and the
probe median is ≤250 ms. If the same semantic-preserving workspace correction
does not meet the threshold, stop and revise the plan again before touching any
additional production path.

- [ ] **Step 4B: Collapse repeated same-layer weight calculations after the workspace-only correction remains above 250 ms**

This second correction is authorized by the recorded post-workspace result:

```text
literal ControlA+ControlB SHA-256 remained
de9d52dcb0e37180b47bc3f55a79c1e47151699cd53ef2743a3c5d2314f90d47;
Profiler allocation samples fell from 294,944 to 35;
all control-map tests passed 20/20 and performance tests passed 6/6;
the five-run 96×64 median remained 399.7145 ms.
```

The remaining hot loop evaluates `DistanceToCellRectangle`, `EdgeNoise`,
`BlendWidth`, and `SmoothStep` separately for every candidate cell even when
many candidates share one layer. For a fixed output pixel and candidate layer,
`EdgeNoise(sampleX, sampleY, layer)` and
`BlendWidth(ownerLayer, candidateLayer)` are constant. The candidate weight is
monotonically non-increasing with nonnegative distance, so the maximum weight
for that layer is exactly the weight of its nearest candidate cell.

Before the production edit, add a focused frozen-reference test with multiple
same-layer candidates at different distances and at equal distance around one
owner cell. Record the original generator's exact ControlA/ControlB bytes as
literal expected data, not by reimplementing the algorithm in the test. Keep
the existing full 96×64 frozen SHA-256 test. Temporarily introduce a deliberate
wrong nearest-candidate choice in the uncommitted implementation and prove that
at least the focused literal bytes or the full digest fails; then restore it.

Change only `FirstArtTerrainControlMapGenerator3D.cs` and its existing test:

```text
allocate per-Generate nearest-distance and nearest-cell workspaces for all seven layers;
for each output pixel, clear those workspaces and scan the exact same bounded candidate rectangle;
retain the nearest candidate cell independently for each non-owner layer;
compare candidates using nonnegative squared rectangle distance, but call the existing
DistanceToCellRectangle on the retained real candidate coordinates for the final weight;
compute the existing EdgeNoise, ordered Profile.BlendWidth and SmoothStep exactly once
for each retained layer, then feed the unchanged highest-three/quantization/encoding path.
```

Do not shrink `candidateRadius`, skip candidate cells, approximate distance or
noise, reorder layer priority, parallelize, add static mutable caches, cache
between calls, change Profile/Shader/assets, lower the threshold, or reduce map
or control dimensions. Exact candidate bounds, border behavior, ordered layer
iteration, fallback logs and public API remain frozen.

Run the new literal test, complete `FirstArtTerrainControlMapTests`, complete
`FirstArtTerrainPerformanceTests`, and the same five-run 96×64 probe. Require
the full frozen digest and all existing transition bytes to remain identical,
all tests to pass, and the median to be ≤250 ms. If this nearest-per-layer
equivalent correction still misses the threshold, stop again; do not touch a
third production path or relax the target without a new controlled decision.

- [ ] **Step 5: Run fresh full regression and headless compile**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testResults /tmp/wastecity-first-terrain/task-09-full-editmode.xml -logFile /tmp/wastecity-first-terrain/task-09-full-editmode.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testResults /tmp/wastecity-first-terrain/task-09-full-playmode.xml -logFile /tmp/wastecity-first-terrain/task-09-full-playmode.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -logFile /tmp/wastecity-first-terrain/task-09-compile.log
```

Expected: all tests zero failures/zero skips; compile exits 0; scan logs for `error CS`, `Shader error`, `Compilation failed`, `Missing Script`, `Missing Shader`, `NullReferenceException` and `Aborting batchmode` and require no unexpected result.

- [ ] **Step 6: Build all three Windows targets**

```bash
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows -logFile /tmp/wastecity-first-terrain/task-09-build-default-3d.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment -logFile /tmp/wastecity-first-terrain/task-09-build-development-3d.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D -logFile /tmp/wastecity-first-terrain/task-09-build-legacy-2d.log
file Builds/Windows/WasteCity.exe Builds/Windows3DDevelopment/WasteCityGrayboxDev.exe Builds/Windows2D/WasteCity2D.exe
```

Expected: all exit 0 and all three are `PE32+ executable (GUI) x86-64, for MS Windows`. Build outputs stay untracked.

- [ ] **Step 7: Record real GUI Profiler evidence**

Open this exact worktree in Unity 2022.3.62f1, load `GrayboxPrototype3D`, set Game View to 1920×1080, disable Deep Profile, enter Play, warm at least two frames, and save exactly 300 consecutive frames to:

```text
/tmp/wastecity-first-terrain/task-09-first-terrain-300-frames.data
```

Record CPU/GPU frame time, FPS, Draw Calls, SetPass, Renderer count, total texture memory, four array memory, and GC allocation for `FirstArtTerrainRenderer3D`/terrain adapters. Target is ≥60 FPS, one formal terrain Renderer, approximately ≤120 MB terrain runtime texture memory, and 0 B per-frame managed allocation after warm-up. Do not use batchmode or NUnit data as a substitute.

- [ ] **Step 7A: Correct the measured Mask-array memory overrun without changing source art**

This correction is active because the real loaded four-array payload measured
`233.33 MiB`: BaseColor and Normal are each BC7-sized, Height is 1024/R8, but
the 2048 linear Mask is RGBA32 and alone consumes `149.33 MiB`. The approved
design explicitly requires adjusting runtime-array format/resolution before
visual approval when the array budget is materially exceeded.

Add a failing builder test before changing production. It must require the real
Mask to be `TextureFormat.BC7` and the four loaded arrays to remain within a
`128 MiB` hard ceiling when measured with
`Profiler.GetRuntimeMemorySizeLong`; RED must show that the current Mask is
RGBA32 and the current readable CPU+GPU sum is `489,337,982 B`, above the
ceiling. The GREEN contract also requires `isReadable == false` for all four
generated runtime arrays; this releases their Editor/player CPU copies after
generation rather than counting both CPU and GPU storage. Preserve the existing exact 2048 size,
seven-slice order, 12 mip levels, linear color space, Repeat wrap, source GUID,
source PNG bytes, source `.meta` bytes, importer readability/platform settings,
generated Mask GUID and two-build deterministic identity contracts.

Change only `FirstArtTerrainAssetBuilder.cs` to build the runtime Mask as 2048
linear BC7 at `TextureCompressionQuality.Best` and to finalize BaseColor,
Normal, Mask and Height runtime arrays as non-readable after their complete mip
data is populated. For each final `Texture2DArray`, write every slice and every
mip into its CPU backing with `SetPixelData`; only after the array is complete,
call `Apply(updateMipmaps: false, makeNoLongerReadable: true)` exactly once.
After that call, do not call `SetPixelData` or `Apply` again: persist, save,
reimport and reload the asset directly. It is explicitly forbidden to populate
a final array with GPU `Graphics.CopyTexture` and then call `Apply`, because the
CPU upload may overwrite the already-copied GPU content. GPU copy/readback is
allowed only for temporary staging before bytes are written to the final array.
The focused pixel tests must run against the reloaded persistent non-readable
assets, not a readable staging object. Keep the seven
source Mask PNGs and their import policy uncompressed and untouched. Use
per-slice temporary readable linear RGBA32 staging textures, render/read mip 0
without color-space conversion, generate the mip chain, call
`EditorUtility.CompressTexture`, copy all compressed mips into one BC7
`Texture2DArray`, and destroy every temporary Texture/RenderTexture in
`finally`. The existing all-arrays preflight and
persist-after-success transaction remains in force; a compression/readback/copy
failure must leave all four persistent arrays and all source assets byte- and
GUID-identical to their pre-call state.

The GREEN test must require:

```text
Mask: 2048×2048×7, 12 mips, linear BC7, Repeat;
BaseColor/Normal/Height formats and dimensions unchanged;
BaseColor/Normal/Mask/Height persistent arrays all report isReadable=false;
sum of Profiler.GetRuntimeMemorySizeLong for the four loaded arrays ≤128 MiB;
expected loaded sum reported near `127,227,775 B` (`121.33 MiB`) and the
approved approximately-120-MB target;
64 deterministic mip-0 sample coordinates per slice against the source Mask,
with each channel absolute error ≤16 and mean absolute channel error ≤4;
two builds preserve all source states, all four array GUIDs and identical second-build contents;
the Mask `.meta` GUID is unchanged and the regenerated `.asset` remains Git LFS-backed.
```

Replace the old readable-array test assumptions as part of the same approved
test-file change. `AssertArrayContract` must require `isReadable == false` after
`SaveAssets`/reimport/reload. Every BaseColor, Normal, Mask and Height generated
array pixel assertion must use GPU readback (`RenderTexture` + `ReadPixels` or
`AsyncGPUReadback`) and must not call `GetPixelData`, `GetPixels` or another CPU
read API on a reloaded generated array. The existing temporary-readable source
Height `GetPixelData<ushort>` path remains allowed because it reads the source
inside the protected importer scope, not a persistent runtime array.

Regenerate all four array assets in place only after the focused tests pass;
BaseColor/Normal/Height pixel content, formats, dimensions, mip counts and GUIDs
must remain unchanged apart from the serialized non-readable storage flag.
Then rerun asset-builder, profile, shader, scene-contract, performance and real
runtime scene focused suites. Any source asset/meta change, unsupported BC7
result, missing Mask channel, >128 MiB loaded sum or visual shader failure is a
stop gate; do not reduce source resolution or alter the Shader/Profile to hide it.

- [ ] **Step 7B: Replace the incomplete GUI and visual evidence with deterministic native captures**

Create the Editor-only `FirstArtTerrainEvidenceCapture` tool and focused tests.
It must refuse to run outside Play Mode, outside `GrayboxPrototype3D`, without
seed 8128, without the approved Profile/pipeline/material, or without exactly
one presented formal terrain Renderer. It writes only beneath
`/tmp/wastecity-first-terrain/`; it never edits or saves the scene, Profile,
Material, arrays, camera prefab, gameplay state, Packages or ProjectSettings.

The tool must snapshot and restore in nested `try/finally`: camera and rig
transforms, orthographic size, target texture, presenter enabled state,
fallback visibility, `Time.captureFramerate`, render target and every registered
Editor update callback. A failure or cancellation leaves zero callbacks,
temporary textures or changed runtime state.

For stills, search the real 32×24 model rather than assuming screen positions:

```text
find and record exact adjacent cell coordinates for Wasteland–Rocky,
Wasteland–Wetland, Wasteland–Crystal, Ruins–non-Ruins,
DeepWater–passable shore and Cliff–passable edge;
find and record one 2×2 neighborhood containing at least three distinct approved layers;
center the unchanged 52° orthographic camera on each recorded midpoint;
render directly from the real Game camera into a 1920×1080 RenderTexture;
write raw PNGs with no Editor chrome, cursor, selection or Gizmos;
write a JSON manifest containing seed, scene, pipeline/material IDs, filename,
cell coordinates/layers, camera/rig transform, orthographic size and 1920×1080 dimensions.
```

`01-map-overview` must show the complete map; `02-default-game-camera` must use
the untouched default gameplay framing; `03`–`09` must use their recorded named
boundaries rather than a shared center/zoom sequence. `10` must be a genuine
same-camera side-by-side comparison: left formal terrain, right the seven
graybox fallback surface groups after a temporary presenter disable, with all
state restored afterward. The two halves and the final comparison must be
distinct and the final PNG exactly 1920×1080.

For DeepWater, lock the same camera on the recorded shore, set
`Time.captureFramerate = 30`, and capture one new lossless 1920×1080 camera frame
on each of 300 strictly consecutive `Time.frameCount` values. After the first
captured frame, every next frame must equal the previous frame number plus one;
the final frame number minus the first must equal `299`. Record the full ordered
frame-number list and first/last range in the manifest. A duplicate, skipped or
out-of-order frame aborts the capture, deletes the incomplete frame set and
requires a fresh retry. Add a focused sequence-validator test whose gapped input
`100, 102, 103` fails before GUI capture. Encode those exact 300 consecutive PNG
frames with ffmpeg to H.264, 30 fps, exactly 10 seconds. Verify the camera/rig
matrices are identical for all frames, frame hashes are not all identical, the
shoreline occupies a readable portion of the image, and no frame contains
Editor UI. Do not synthesize repeats from sparse screenshots.

Extend `GrayboxPerformanceProbe` and its focused tests so the real GUI evidence
also writes a terrain-runtime JSON containing: active scene/worktree, Game View
target `1920×1080`, formal Renderer count, Profile/Material identity, each loaded
array's dimensions/depth/format and
`Profiler.GetRuntimeMemorySizeLong`, summed array memory, and the fact that
`FirstArtTerrainRenderer3D` declares no `Update`/`LateUpdate` CPU water loop.
The 300-frame Profiler summary must include an explicit terrain-presenter marker
entry; zero occurrences/zero GC is accepted only together with the structural
no-Update proof and the live one-renderer runtime JSON. Continue to record Draw
Calls/SetPass and total Editor texture memory from the real GUI Stats/Profiler.

On this macOS Metal Editor, a displayed GPU value of `-- ms` may be recorded as
**unavailable**, not zero and not passed. Preserve the screenshot and defer a
numerical GPU-time gate to the already-unresolved real Windows 10/11 GPU smoke;
CPU frame time, ≥60 FPS, exact 300-frame range, one formal Renderer, ≤128 MiB
four-array loaded sum and terrain-adapter 0 B remain mandatory now.

After this correction, regenerate the Profiler `.data`, terrain-runtime JSON,
summary, Stats/Profiler screenshots, all ten stills, manifest and continuous
MP4. Delete/replace the earlier invalid `/tmp` evidence package so it cannot be
mistaken for the approved set, then repeat the independent pre-visual technical
review before asking the user to judge aesthetics.

- [ ] **Step 8: Capture the fixed visual acceptance set**

With seed 8128, the default 52° orthographic camera, the same URP pipeline, 1920×1080 and unchanged exposure, capture:

```text
01-map-overview.png
02-default-game-camera.png
03-wasteland-rocky.png
04-wasteland-wetland.png
05-wasteland-crystal.png
06-three-way-junction.png
07-ruins-edge.png
08-deep-water-shore.png
09-cliff-edge.png
10-graybox-formal-comparison.png
11-deep-water-motion.mp4
```

The MP4 is about 10 seconds and must show subtle two-direction normal motion without camera motion. Keep initial captures in `/tmp/wastecity-first-terrain/visual-review/`; do not commit them before user approval.

- [ ] **Step 9: Stop for user visual approval**

Present all ten stills and the DeepWater recording. Approval requires the seven classes to be readable, basic boundaries seamless, special boundaries clear, DeepWater recognizably water-like, no Crystal emission, no resource-node coverage and no visual/collider offset. If rejected, use `superpowers:receiving-code-review`; change only the Profile, Shader or control-generator paths listed in this task, add the matching focused regression test, commit the visual correction separately, regenerate all affected evidence and repeat this gate. Feedback requiring source PNG, models, Collider, gameplay or a different scene path is a plan stop gate.

- [ ] **Step 10: Commit Task 9 after approval**

Always commit the performance code/tests:

```bash
git add Assets/_Game/Editor/GrayboxPerformanceProbe.cs Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs Assets/_Game/Editor/FirstArtTerrainEvidenceCapture.cs Assets/_Game/Editor/FirstArtTerrainEvidenceCapture.cs.meta Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainControlMapGenerator3D.cs Assets/_Game/Tests/EditMode/FirstArtTerrainAssetBuilderTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainControlMapTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainEvidenceCaptureTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainEvidenceCaptureTests.cs.meta Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs.meta Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset
git diff --cached --check
git commit -m "test: verify first terrain performance and builds"
```

Only if the user explicitly approves repository archival of evidence, copy the approved PNG/MP4 set into `Docs/Art/FirstPass/Terrain/RuntimeIntegration/`, verify LFS attributes for PNG/MP4 before staging, then use a separate commit `art: archive first terrain runtime review`. Files under `Docs/` do not receive Unity `.meta` files.

---

### Task 10: Controlled Documentation, Final Verification and Push

**Files:**
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`

**Interfaces:**
- Consumes: Task 9 commit SHAs, exact test/build/performance results and explicit user visual decision.
- Produces: truthful `IDEA-0004` progress, linked implementation evidence and a clean pushed branch; overall first-art package remains `开发中`.

- [ ] **Step 1: Write exact verified facts before editing docs**

Create a temporary fact sheet outside the repository containing:

```text
branch
base SHA
final implementation SHA list
EditMode total/pass/fail/skip
PlayMode total/pass/fail/skip
compile exit
three build exits and file outputs
96x64 five generation samples and median
formal renderer/object/control dimensions
300-frame FPS/CPU/GPU/GC/texture memory
user visual approval wording and date
real Windows smoke status
```

Do not infer a missing value. Real Windows smoke remains “待补” unless actually run on Windows 10/11.

- [ ] **Step 2: Update the roadmap without marking the entire art pass complete**

In `Docs/05-Formal-Development-Roadmap-ZH.md`:

- replace “正式 Material/Shader/场景映射未开始” with the exact first-terrain runtime result;
- list seven texture arrays/material/Shader/control-map/scene facts;
- state `Ruins`/`Cliff` models, city, leader, buildings, UI, VFX and SFX remain outside this milestone;
- record exact tests/builds/performance and user visual approval;
- keep overall formal art percentage and next sequence truthful rather than automatically declaring 100%.

- [ ] **Step 3: Update only the IDEA-0004 record and references**

In `Docs/06-User-Feedback-and-Change-Control-ZH.md`:

- add `Docs/superpowers/specs/2026-08-10-first-terrain-runtime-integration-design.md` and this plan to the design links;
- add implementation/performance commit SHAs;
- add exact automatic and visual evidence;
- keep requirement and approval `已明确 / 已批准`;
- keep overall implementation `开发中`, because the rest of the first art sample package is not complete;
- do not modify `BUG-0001`, `IDEA-0001`–`0003` text except a directly required cross-reference.

- [ ] **Step 4: Run final protected-range and documentation checks**

```bash
git diff --check
git diff --name-only 2baf131a5e69f8931883547ff9b22d61f2652d11..HEAD
git diff --exit-code 2baf131a5e69f8931883547ff9b22d61f2652d11..HEAD -- Assets/_Game/Scripts/Persistence Assets/_Game/Scenes/FormalPrototype.unity Packages ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset ProjectSettings/EditorBuildSettings.asset README.md
git status --short
```

Also compare SHA-256 for all 28 source PNGs and `.meta` files against the Task 1 snapshot; require exact equality. Confirm all new large generated arrays and approved visual PNG/MP4 evidence, if archived, are LFS pointers in Git rather than normal blobs.

- [ ] **Step 5: Run a final fresh acceptance after documentation**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testResults /tmp/wastecity-first-terrain/final-editmode.xml -logFile /tmp/wastecity-first-terrain/final-editmode.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testResults /tmp/wastecity-first-terrain/final-playmode.xml -logFile /tmp/wastecity-first-terrain/final-playmode.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -logFile /tmp/wastecity-first-terrain/final-compile.log
```

Expected: zero failures/skips and successful compile. Do not reuse earlier XML as final evidence.

- [ ] **Step 6: Commit controlled documentation**

```bash
git add Docs/05-Formal-Development-Roadmap-ZH.md Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --cached --check
git commit -m "docs: record first terrain runtime verification"
```

`README.md` remains zero-diff because the default scene and launch/build commands do not change in this milestone.

- [ ] **Step 7: Final independent review and ordinary push**

Review the full range from the Task 1 parent through HEAD for Critical/Important/Minor findings, with special focus on control-map semantics, array layer order, Shader channel packing, graybox restoration, source-asset immutability, scene idempotence and false verification claims. Resolve Critical/Important findings with RED/GREEN and new commits; do not hide them in the documentation commit.

Then:

```bash
git diff --check 2baf131a5e69f8931883547ff9b22d61f2652d11..HEAD
git status --short
git push origin codex/first-art-pass-delivery-fixes
git rev-parse HEAD
git rev-parse '@{u}'
git ls-remote origin refs/heads/codex/first-art-pass-delivery-fixes
```

Expected: local HEAD, tracking and ls-remote match; worktree is clean; no force-push, merge, PR or Release is created without separate user authorization.

---

## Spec Coverage Audit

| Design sections | Implemented and verified by |
|---|---|
| 1–4 Goal, decisions, parent-spec differences, architecture | Global Constraints, file map, Tasks 1–10 |
| 5 Authoritative data and stable mapping | Task 1 catalog/profile tests |
| 6 Assembly and component boundary | Tasks 1 and 6 |
| 7 Continuous mesh and coordinates | Task 3 |
| 8 Control maps | Task 2 |
| 9 Texture assets and arrays | Task 4 |
| 10 Shader and DeepWater | Task 5 plus Task 9 visual gate |
| 11 Graybox visibility and atomic fallback | Task 6 plus Task 8 real scene |
| 12 Scene and authoring | Task 7 |
| 13 Performance | Task 9 automated and GUI evidence |
| 14 Tests and builds | Tasks 1–9, with final rerun in Task 10 |
| 15 Visual acceptance | Task 9 fixed stills/video and user gate |
| 16 Error handling | Tasks 1, 2, 4, 5, 6 and 8 negative tests |
| 17 Exclusions | Global Constraints and Task 10 frozen-path audit |
| 18 Rollback | Task 6 lifecycle and Task 8 disable/re-enable proof |
| 19 Implementation segmentation and stop gates | Tasks 1–10 individual RED/GREEN/commit boundaries |
| 20 Completion definition | Task 10 final facts, review, documentation and push |

Fresh self-review found no uncovered design section. Requirements that depend on human perception remain explicitly gated by Task 9 user approval rather than being misrepresented as unit-test coverage.

---

## Plan Completion Gate

The implementation is complete only when:

- Tasks 1–10 each have their own RED/GREEN evidence and reviewable commit boundary;
- four generated arrays are correct, stable and stored through exact Git LFS rules;
- the real scene shows one formal terrain Renderer and preserves resource/actor/building placeholders;
- disabling, destroying or breaking formal presentation restores all seven graybox surface groups;
- controls, mesh, Shader, scene, PlayMode, full regression, compile, three builds and real GUI Profiler pass;
- source PNGs, source `.meta`, Persistence, schema, rules, frozen 2D, Build Settings and global URP settings remain unchanged;
- the user explicitly approves the fixed screenshots and DeepWater recording;
- documentation records only verified facts and keeps the overall first art package `开发中`;
- the current branch is ordinarily pushed and remote-consistent.

If any one item is missing, report the exact remaining gate and do not describe the first terrain runtime integration as complete.
