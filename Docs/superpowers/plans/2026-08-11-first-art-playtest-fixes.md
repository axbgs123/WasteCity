# First-Art 3D Playtest Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复默认 3D 场景首次按 `F` 无法展开和第一版地表光照过亮的问题，并以正式场景逐项验证移动城市内城建筑矩阵、补足可读的限制说明。

**Architecture:** 保留 `CityDeploymentRules`、`BuildingMobilityRules` 和 `BuildingPlacementRules` 作为唯一玩法真值；authoring 只把默认出生点移动到 seed `8128` 最近的合法 3×3 格并校准单一方向光。部署失败信息通过既有共享建造 Canvas 显示，内城问题通过真实场景投影与正式定义矩阵验证，只有观察到真实规则/适配错误时才扩展生产修复。

**Tech Stack:** Unity `2022.3.62f1`、C#、URP 14、Input System 1.7、UGUI、NUnit EditMode/PlayMode、Git LFS。

## Global Constraints

- 所有开发必须遵守 `AGENTS.md` 与 `Docs/06-User-Feedback-and-Change-Control-ZH.md`；本计划对应已批准的 `BUG-0002`、`BUG-0003`、`BUG-0004`。
- 不修改 `CityDeploymentRules`、`CityTerrainRules`、`BuildingMobilityRules` 的产品语义，不让湿地、遗迹、深水或悬崖变成可展开区域。
- 不修改七类已验收源贴图、Texture2DArray、地形 Shader、控制图、地图规则、寻路、资源节点、schema `30` 或正式存档。
- 方向光从 `1.25` 调整为 `0.90`；颜色 `(1.0, 0.956, 0.85, 1.0)`、旋转 `(50, -30, 0)`、Soft Shadows、环境光保持不变。
- 默认出生中心从逻辑格 `(8,7)` 调整到 seed `8128` 距离最近且按 X/Y 稳定排序的合法格 `(7,8)`；其世界坐标为 `(-9, 0.5, -4)`。
- 正式移动内城成功矩阵精确为 `Housing`、`Warehouse`、`Assembler`、`PsionicWorkshop`、`ConsciousnessNetwork`、`ShieldGenerator`、`AutomatedRepairBay`、`AlchemyChamber`、`PuppetWorkshop`；`ResearchStation` 是 `FortressOnly` 对照项。
- 所有 Unity `-runTests` 命令禁止同时添加 `-quit`；无界面编译命令可以使用 `-quit`。
- 现有 28 个地表 `.png.meta` 修改及 `ProjectSettings/PackageManagerSettings.asset`、`ProjectSettings/URPProjectSettings.asset` 是受保护的外部 Unity 改动，不暂存、不清理、不覆盖。
- 每个行为必须先取得预期 RED，再写最小生产实现；如果 BUG-0004 的矩阵失败原因超出本计划列出的生产路径，先停止并修订计划，不复制规则或用测试专用后门绕过。

---

## File Map

- `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`：authoring 的默认出生格与第一版地表方向光唯一配置源。
- `Assets/_Game/Scenes/GrayboxPrototype3D.unity`：由批准 authoring 重建的正式默认 3D 场景；禁止手工编辑 YAML。
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`：共享 UGUI 中的部署失败提示与建筑位置/运行限制说明。
- `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`：现有 `F` 仲裁者；只转发部署调用真实返回的失败文本，不复制失败判断。
- `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`：默认出生格、世界坐标和基础场景引用合同。
- `Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs`：方向光的 authoring、验证和幂等合同。
- `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`：部署失败文本从真实请求结果到菜单的转发合同。
- `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`：共享状态提示可见性和建筑详情“位置/运行”信息合同。
- `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`：真实虚拟键盘 `F`、部署完成、内城九建筑矩阵和限制对照。
- `Docs/06-User-Feedback-and-Change-Control-ZH.md`：最终提交、RED/GREEN、矩阵结论与人工复验状态回写。

### Task 1: Freeze Baseline And Reproduce The Three Reports

**Files:**
- Test: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Test: `Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs`
- Test: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`
- Test: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Test: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

**Interfaces:**
- Consumes: `CityDeploymentRules.Validate(WorldMapModel,int,int,int,int)`, `PlanarCoordinateMapper3D.TryWorldToCell(Vector3,out int,out int)`, `GrayboxEvacuationController3D.TryHandleDeploymentRequest()`, `GrayboxBuildingPlacementController3D.CurrentEvaluation`.
- Produces: RED evidence for the default deployment cell, deployment feedback, `0.90` light contract, nine-building mobile-inner matrix, and operation-label clarity.

- [ ] **Step 1: Capture a clean committed baseline without staging protected Unity changes**

```bash
export PROJECT_PATH="/Users/baiyan1/Documents/WasteCity-first-art-pass-fixes"
export UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
mkdir -p /tmp/wastecity-playtest-fixes/baseline
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxSceneContractTests -testResults /tmp/wastecity-playtest-fixes/baseline/scene.xml -logFile /tmp/wastecity-playtest-fixes/baseline/scene.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests -testResults /tmp/wastecity-playtest-fixes/baseline/building-runtime.xml -logFile /tmp/wastecity-playtest-fixes/baseline/building-runtime.log
```

Expected: both suites pass on the committed baseline; `git status --short` still contains only the protected Unity paths.

- [ ] **Step 2: Add the default deployment RED**

In `GrayboxSceneContractTests`, replace the old literal `(-8,-5)` assertion with a behavior assertion that maps the serialized city position to a cell in a fresh `WorldMapModel(32,24,new WorldSeed(8128))` and expects `CityDeploymentRules.Validate(...) == CityDeploymentFailure.None`; separately assert the approved literal target `(7,8)` and world position `(-9,0.5,-4)`.

In `GrayboxBuildingRuntimeSceneTests`, add `VirtualF_FromDefaultSpawn_DeploysCityAndCompletesTransition`: press `F` through the existing virtual `Keyboard` helper, assert `Mobile -> Deploying`, wait real frames until the three-second transition completes, then assert `Fortress` without direct router/city tick calls.

- [ ] **Step 3: Add the deployment feedback RED**

In `GrayboxEvacuationTests`, configure a deployment spy that returns `false` and `"展开失败：地面不稳定或有大型废墟"`; assert `TryHandleDeploymentRequest()` consumes the `F` request once and the menu displays exactly that returned reason. In `GrayboxBuildingUiAndInputTests`, assert the shared status owner is active and contains the returned text even while interaction state is `Inactive`.

- [ ] **Step 4: Add the lighting RED**

In `FirstArtTerrainSceneContractTests`, change both the approved-light fixture and serialized-scene assertion from `1.25f` to literal `0.90f`; keep color, rotation, shadow and culling expectations unchanged. The existing wrong-intensity mutation must still fail validation.

- [ ] **Step 5: Add the mobile-inner matrix and label RED**

In `GrayboxBuildingRuntimeSceneTests`, add `AllDeclaredMobileInnerBuildings_ProjectAsValidInFormalScene`. Use `GrayboxDeveloperModifier3D.UnlockAllResearch()`, add `1000` of every `ResourceIds.All` entry, create and complete required prerequisite buildings through the public session/presentation API, restore `CityMode.Mobile`, then for each of these literal definitions select it, move the real virtual mouse to inner cell `(3,2)`, and assert valid preview with `BuildingSite.InnerCity`: Housing, Warehouse, Assembler, PsionicWorkshop, ConsciousnessNetwork, ShieldGenerator, AutomatedRepairBay, AlchemyChamber, PuppetWorkshop. Do not place all nine into the same grid.

Add two counterexamples: `ResearchStation` at the same cell in `Mobile` must have primary failure `InvalidCityMode`, while `Housing` with insufficient Alloy must have `InsufficientMaterials`. In `GrayboxBuildingUiAndInputTests`, assert card details include both `位置 两者皆可` and `运行 移动可运行`, and that ResearchStation details include `运行 仅展开运行`.

- [ ] **Step 6: Run focused RED suites and record exact failures**

```bash
mkdir -p /tmp/wastecity-playtest-fixes/red
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxSceneContractTests -testResults /tmp/wastecity-playtest-fixes/red/scene.xml -logFile /tmp/wastecity-playtest-fixes/red/scene.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainSceneContractTests -testResults /tmp/wastecity-playtest-fixes/red/light.xml -logFile /tmp/wastecity-playtest-fixes/red/light.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxEvacuationTests -testResults /tmp/wastecity-playtest-fixes/red/evacuation.xml -logFile /tmp/wastecity-playtest-fixes/red/evacuation.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxBuildingUiAndInputTests -testResults /tmp/wastecity-playtest-fixes/red/ui.xml -logFile /tmp/wastecity-playtest-fixes/red/ui.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests -testResults /tmp/wastecity-playtest-fixes/red/runtime.xml -logFile /tmp/wastecity-playtest-fixes/red/runtime.log
```

Expected: scene spawn/light/feedback/operation-label tests fail for the named missing behavior. The nine-building matrix outcome is diagnostic: if any of the nine fails after all prerequisites are established, capture its exact `PrimaryFailure` and stop before editing an unlisted production path.

### Task 2: Make The Default Spawn Deployable And Show Failure Reasons

**Files:**
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity` through authoring only
- Test: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Test: `Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs`
- Test: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Test: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

**Interfaces:**
- Consumes: `IGrayboxDeploymentRequest3D.TryToggleDeployment(out string failureReason)` and the existing shared `GrayboxBuildingMenuView3D` Canvas.
- Produces: `GrayboxBuildingMenuView3D.ShowDeploymentFailure(string message)` and `ClearDeploymentFailure()`, plus the authored cell `(7,8)`.

- [ ] **Step 1: Add the minimal UI feedback API**

Add a deployment-status string to `GrayboxBuildingMenuView3D`. `ShowDeploymentFailure(string)` rejects null/empty by clearing, writes the exact rule message into the existing `Placement.Status.Text`, and activates `Placement.Status`; `ClearDeploymentFailure()` removes only that deployment override. `RefreshPlacementStatus()` gives active placement-preview failures priority, otherwise shows the deployment override while interaction is `Inactive`. No new Canvas, polling component, or duplicated Chinese failure map is allowed.

- [ ] **Step 2: Forward the real deployment result exactly once**

In both no-ground-instance paths of `GrayboxEvacuationController3D`, capture the `bool` and `out string` from `deploymentRequest.TryToggleDeployment`. On failure call `menu.ShowDeploymentFailure(failureReason)`; on success call `menu.ClearDeploymentFailure()`. Continue returning `true` so the outer `GrayboxInputRouter` does not issue a second `F` request.

- [ ] **Step 3: Author the approved playable spawn**

Change `CreateMobileCity` to call `coordinates.TryCellToWorld(7,8,.5f,out cityPosition)`. Add an existing-scene authoring repair method that validates seed `8128` at the current serialized cell; if invalid, moves city to `(7,8)`, preserves Y `.5`, moves the leader by the same XZ delta, and moves the camera rig to the new city XZ. It must use `CityDeploymentRules.Validate` rather than duplicating terrain checks.

- [ ] **Step 4: Run authoring twice and prove identity stability**

```bash
mkdir -p /tmp/wastecity-playtest-fixes/authoring
WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-playtest-fixes/authoring/before.json "$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity -logFile /tmp/wastecity-playtest-fixes/authoring/identity-before.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure -logFile /tmp/wastecity-playtest-fixes/authoring/pass1.log
WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-playtest-fixes/authoring/after1.json "$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity -logFile /tmp/wastecity-playtest-fixes/authoring/identity-after1.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure -logFile /tmp/wastecity-playtest-fixes/authoring/pass2.log
WASTECITY_GRAYBOX_IDENTITY_RESULT=/tmp/wastecity-playtest-fixes/authoring/after2.json "$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.CaptureFoundationIdentity -logFile /tmp/wastecity-playtest-fixes/authoring/identity-after2.log
cmp /tmp/wastecity-playtest-fixes/authoring/before.json /tmp/wastecity-playtest-fixes/authoring/after1.json
cmp /tmp/wastecity-playtest-fixes/authoring/after1.json /tmp/wastecity-playtest-fixes/authoring/after2.json
```

Expected: both `cmp` commands return zero; scene bytes after pass 1 and pass 2 have the same SHA-256.

- [ ] **Step 5: Run deployment GREEN suites**

Run the four focused scene/evacuation/UI/runtime commands from Task 1. Expected: default virtual `F` completes deployment, invalid request shows the exact returned rule reason, and no test calls deployment twice.

- [ ] **Step 6: Commit only approved deployment paths**

```bash
git add Assets/_Game/Editor/GrayboxSceneAuthoring.cs Assets/_Game/Scenes/GrayboxPrototype3D.unity Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs
git diff --cached --check
git commit -m "fix: restore playable city deployment flow"
```

### Task 3: Calibrate First-Art Lighting With One Variable

**Files:**
- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity` through authoring only
- Test: `Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs`

**Interfaces:**
- Consumes: the existing `TerrainLightIntensity` authoring/validation contract.
- Produces: approved first-pass directional intensity `0.90f` while all other light fields remain byte-for-byte equivalent in behavior.

- [ ] **Step 1: Change only the approved intensity constant**

Set `TerrainLightIntensity = .90f` in `GrayboxSceneAuthoring`. Do not alter `TerrainLightColor`, `TerrainLightEuler`, `RenderSettings.ambientIntensity`, source textures, arrays, material, Shader, control map, or exposure.

- [ ] **Step 2: Run authoring twice**

Reuse Task 2's authoring commands and identity checks. Expected: the Light's GlobalObjectId is unchanged, pass 1/pass 2 scene hashes match, and only the serialized Light intensity changes among lighting fields.

- [ ] **Step 3: Run lighting GREEN and source-asset freeze checks**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.FirstArtTerrainSceneContractTests -testResults /tmp/wastecity-playtest-fixes/green/light.xml -logFile /tmp/wastecity-playtest-fixes/green/light.log
git diff --exit-code HEAD -- Assets/_Game/Art/FirstPass/Environment/Terrain ':!Assets/_Game/Art/FirstPass/Environment/Terrain/**/*.meta'
git diff --exit-code HEAD -- Assets/_Game/Rendering/FirstArtTerrain Assets/_Game/Scripts/ArtIntegration3D
```

Expected: focused suite passes; committed source image and terrain runtime asset/code paths are unchanged. Protected `.meta` working-tree changes remain untracked from this commit.

- [ ] **Step 4: Commit lighting calibration**

```bash
git add Assets/_Game/Editor/GrayboxSceneAuthoring.cs Assets/_Game/Scenes/GrayboxPrototype3D.unity Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs
git diff --cached --check
git commit -m "fix: reduce first art terrain lighting"
```

### Task 4: Close The Mobile Inner-City Building Report

**Files:**
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- Test: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Test: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

**Interfaces:**
- Consumes: `BuildingCatalog.BuildMenu`, `BuildingMobilityRules.SupportsSite`, `BuildingMobilityRules.CanConstruct`, `GrayboxDeveloperModifier3D`, and `GrayboxBuildingPlacementController3D.CurrentEvaluation`.
- Produces: an executable nine-building evidence matrix and visible `运行 <OperationName>` card detail.

- [ ] **Step 1: Evaluate the Task 1 matrix result before production changes**

If all nine definitions produce valid inner previews and both counterexamples produce their exact expected failure, record that the placement engine is correct and proceed with the label-only clarity fix. If a listed success definition fails for a reason other than a test fixture's missing prerequisite, stop, preserve the failing XML, and add the exact owning production path to this plan before editing it.

- [ ] **Step 2: Add operation information to every catalog detail card**

In `BuildDetails`, insert `"运行 " + BuildingMobilityRules.OperationName(definition.Operation)` immediately after the existing `位置` line. Do not change definition placement/operation values, research IDs, costs, prerequisites, population, or category visibility.

- [ ] **Step 3: Run matrix and UI GREEN suites**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testFilter WasteCity.Tests.GrayboxBuildingUiAndInputTests -testResults /tmp/wastecity-playtest-fixes/green/ui.xml -logFile /tmp/wastecity-playtest-fixes/green/ui.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests -testResults /tmp/wastecity-playtest-fixes/green/runtime.xml -logFile /tmp/wastecity-playtest-fixes/green/runtime.log
```

Expected: nine literal mobile-inner definitions are valid after prerequisites, ResearchStation is rejected in Mobile with `InvalidCityMode`, insufficient materials remain `InsufficientMaterials`, and UI details expose both location and operation.

- [ ] **Step 4: Commit the matrix evidence and clarity fix**

```bash
git add Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs
git diff --cached --check
git commit -m "fix: clarify mobile inner city building limits"
```

### Task 5: Full Verification, Controlled Documentation, And Push

**Files:**
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Verify: all production/test files changed by Tasks 1–4

**Interfaces:**
- Consumes: focused GREEN XML, full regressions, compilation, authoring identity/hash, matrix result, and visual screenshot.
- Produces: verified bug records and a pushed `codex/playtest-fixes` branch.

- [ ] **Step 1: Run full fresh regressions and compilation**

```bash
mkdir -p /tmp/wastecity-playtest-fixes/final
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform EditMode -testResults /tmp/wastecity-playtest-fixes/final/editmode.xml -logFile /tmp/wastecity-playtest-fixes/final/editmode.log
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform PlayMode -testResults /tmp/wastecity-playtest-fixes/final/playmode.xml -logFile /tmp/wastecity-playtest-fixes/final/playmode.log
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" -logFile /tmp/wastecity-playtest-fixes/final/compile.log
```

Expected: zero failed/skipped tests and compile log ends successfully without `error CS`, `Compilation failed`, unhandled exception, or batch abort.

- [ ] **Step 2: Verify range and protected paths**

```bash
git diff --check origin/codex/first-art-pass-delivery-fixes..HEAD
git diff --exit-code origin/codex/first-art-pass-delivery-fixes..HEAD -- Assets/_Game/Scripts/City Assets/_Game/Scripts/World Assets/_Game/Scripts/Persistence Packages ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset
git status --short
```

Expected: no committed changes to rules/persistence/packages/project rendering settings; working status still lists the protected external Unity paths but they are absent from the index.

- [ ] **Step 3: Launch the fixed scene for manual screenshot evidence**

Open `GrayboxPrototype3D`, enter Play, capture the same camera view after stabilization, and record: visible reduced brightness, successful default `F` deployment, and visible invalid-deployment reason. This is user-review evidence, not a substitute for automated GREEN.

- [ ] **Step 4: Update controlled bug records**

Set `BUG-0002` and `BUG-0003` to `已实现待验证` until the user visually confirms. Set `BUG-0004` according to the matrix: `已验证` only if the nine-success/two-counterexample automated contract passes and the finding is a condition/clarity issue rather than an unresolved engine fault. Add exact commit SHAs, test counts, matrix names, authoring hashes, compile result and screenshot location without claiming Windows smoke.

- [ ] **Step 5: Commit docs, push, and stop**

```bash
git add Docs/06-User-Feedback-and-Change-Control-ZH.md Docs/superpowers/plans/2026-08-11-first-art-playtest-fixes.md
git diff --cached --check
git commit -m "docs: record first art playtest verification"
git push origin codex/playtest-fixes
```

Expected: local HEAD, tracking branch and `git ls-remote origin refs/heads/codex/playtest-fixes` match; no new task starts after handoff.
