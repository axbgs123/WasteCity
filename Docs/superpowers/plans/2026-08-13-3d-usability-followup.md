# 3D 易用性后续实施计划

> 日期：2026-08-13
> 状态：待用户批准执行
> 受控需求：`IDEA-0007`、`IDEA-0008`、`IDEA-0009`、`IDEA-0010`
> 权威规格：`Docs/superpowers/specs/2026-08-13-3d-usability-followup-design.md`
> 精确基线：`d10d0eb797071bb71c4eb06df97a515a923f67e8`
> 工作分支：`codex/3d-usability-followup`

## Goal

在默认 `GrayboxPrototype3D` 中完成住房规则需求化验证、真实外城范围表现、采矿站合法锚点引导，以及带暂停、显示设置、操作说明和退出确认的 Esc 系统菜单；保持 schema 30、冻结 2D、稳定 ID 和统一规则真值不变。

## Architecture

- Building core 新增纯 `BuildingResourceNodeCompatibilityRules`，成为放置请求与采矿引导的唯一兼容性入口；
- `Graybox3D.Building` 继续拥有放置请求、完整 `BuildingPlacementEvaluation`、范围 Mesh、采矿引导和 session 修订号；
- 新 `WasteCity.Graybox3D.Usability` 程序集拥有系统菜单、显示设置、退出 adapter 和输入协调器；
- `GrayboxInputRouter` 保持通用，只把唯一 interceptor 从 building input 改接 usability coordinator；
- `GameSpeedModel` 继续是暂停原因与恢复速度真值；
- 设置通过独立 PlayerPrefs 保存，不进入 `SaveService`、`GameSaveData` 或 schema 30。

## Tech Stack

Unity `2022.3.62f1`、C#、NUnit EditMode/PlayMode、Input System、UGUI、URP `14.0.12`、现有 Building/World/Graybox3D 程序集、Project Quality 文档生成与验证工具、Windows x86-64 构建。

## Global Constraints

- 只在 `/Users/baiyan1/Documents/WasteCity-3d-usability-followup` 和分支 `codex/3d-usability-followup` 工作；
- 开始执行前再次 `fetch` 并证明准确基线为 `d10d0eb797071bb71c4eb06df97a515a923f67e8`；
- 不修改、合并或重写 `master`、已有 PR #1 或 `origin/codex/playtest-fixes`；
- 不 force-push、不创建 Release；
- 开始每个任务前检查工作区，发现计划外改动立即停止；
- Unity MCP 已安装时，必须确认实例项目路径精确指向本工作树后才连接和锁定；只有其他项目实例时不得操作；
- 运行 batch Unity 前先正常释放同一工作树的 Editor/MCP 锁，禁止杀死或操作其他 Unity 项目；
- 所有 `-runTests` 命令不加 `-quit`；编译、authoring 和构建命令可以加 `-quit`；
- 新增测试先运行并保留 RED XML/log，再写最小生产实现；不得先补实现后伪造 RED；
- `IDEA-0009` 是基线已存在行为，只做 characterization/requirement 验证；若新增测试立即通过，必须如实记录，不制造假失败；
- 若 `IDEA-0009` 测试揭示生产缺陷，先报告边界，再决定是否修改 Catalog 或 mobility rule；
- 不修改 schema 30、Persistence、正式 2D 运行时、地形源资产、地形 importer、Texture2DArray、Shader、Packages、GraphicsSettings 或 QualitySettings；
- Unity `2022.3.62f1` 运行后若受保护 YAML 只出现空标量行尾空格，必须先用 `git diff --ignore-space-at-eol` 证明语义零差异，再恢复该序列化噪音；GUID、引用、数值、字段、资源或场景结构的任何变化都必须保留并停线，禁止以“Unity 自动生成”为由清除；
- 本阶段不运行 `TerrainAssetDeep`；完整 EditMode 明确使用 `-testCategory '!TerrainAssetDeep'`；
- 不把自动化通过写成人工试玩通过；最终人工状态保持“待用户确认”；
- 每完成一个 Task 报告 RED/GREEN、变更文件与边界检查结果；
- 新生产文件或修改文件超出本计划 File Map 时立即停止并请求批准；
- 每次只暂存精确路径，禁止 `git add .`、`git add Assets` 或宽泛暂存；
- Unity 可执行文件固定为 `/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity`。

## File and Assembly Map

### New production source

```text
Assets/_Game/Scripts/Building/BuildingResourceNodeCompatibilityRules.cs
Assets/_Game/Scripts/Graybox3D/Usability.meta
Assets/_Game/Scripts/Graybox3D/Usability/WasteCity.Graybox3D.Usability.asmdef
Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsModel3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsAdapters3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuController3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs
```

每个 Unity 文件都有独立、稳定、唯一 `.meta`；新增文件夹也有 folder `.meta`。

### Existing production changes

```text
Assets/_Game/Scripts/Core/GameSpeedModel.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs
Assets/_Game/Editor/GrayboxSceneAuthoring.cs
Assets/_Game/Editor/WasteCity.Editor.asmdef
Assets/_Game/Scenes/GrayboxPrototype3D.unity
```

`Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs` 不修改；只通过场景序列化把现有 interceptor 引用改为新 coordinator。

### New tests

```text
Assets/_Game/Tests/EditMode/BuildingResourceNodeCompatibilityRulesTests.cs
Assets/_Game/Tests/EditMode/GrayboxUsabilityTests.cs
Assets/_Game/Tests/PlayMode/GrayboxUsabilityRuntimeSceneTests.cs
```

每个测试文件都有对应 `.meta`。不新增测试程序集，使用现有 EditMode/PlayMode asmdef。

### Existing tests and assembly definitions

```text
Assets/_Game/Tests/EditMode/BuildingMobilityRulesTests.cs
Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs
Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs
Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs
Assets/_Game/Tests/EditMode/GameSpeedTests.cs
Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs
Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef
Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs
Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef
```

### Documentation and quality catalog

```text
Docs/01-Game-Design-Document-ZH.md
Docs/05-Formal-Development-Roadmap-ZH.md
Docs/06-User-Feedback-and-Change-Control-ZH.md
Docs/Engineering/project-quality-catalog.json
Docs/superpowers/specs/2026-08-13-3d-usability-followup-design.md
Docs/superpowers/plans/2026-08-13-3d-usability-followup.md
Docs/Generated/Project-Inventory-ZH.md
Docs/Generated/Test-Inventory-ZH.md
Docs/Generated/Documentation-Attention-ZH.md
Docs/Generated/Latest-Verification-ZH.md
```

只有生成器实际判断需要变化的 `Docs/Generated` 文件才写回。

---

## Task 0: Revalidate the Safe Baseline and Start the Approved Change

**Files:**

- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Commit existing approved docs/spec/plan only.

### Step 1: Revalidate Git and remote baseline

Run:

```bash
export WASTECITY_PROJECT="/Users/baiyan1/Documents/WasteCity-3d-usability-followup"
export WASTECITY_UNITY="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
export WASTECITY_EVIDENCE="/tmp/wastecity-3d-usability-followup"
mkdir -p "$WASTECITY_EVIDENCE"
git fetch origin --prune
git status --short --branch
git rev-parse HEAD
git rev-parse origin/codex/playtest-fixes
git merge-base HEAD origin/codex/playtest-fixes
git worktree list --porcelain
```

Expected:

- current branch is exactly `codex/3d-usability-followup`;
- `HEAD` and remote baseline resolve to the approved base until the documentation commit is created;
- only the already reviewed Docs changes are present;
- no other worktree is touched.

Any unexpected source, scene, settings or asset change is a stop gate.

### Step 2: Lock the correct Unity project instance

- Query Unity MCP instances;
- require the project path to resolve to this exact worktree;
- lock only that instance;
- if only `SwordVFXLab` or any other project is available, do not connect or issue Unity actions;
- ask for/open the correct WasteCity instance, then recheck the path.

Record the instance identity and project path under the evidence directory. This is required before scene inspection or Unity-run tests.

### Step 3: Capture fresh baseline tests

After the correct project is available and no duplicate process owns it, run daily EditMode and full PlayMode:

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode -testCategory '!TerrainAssetDeep' \
  -testResults "$WASTECITY_EVIDENCE/baseline-editmode.xml" \
  -logFile "$WASTECITY_EVIDENCE/baseline-editmode.log"

"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testResults "$WASTECITY_EVIDENCE/baseline-playmode.xml" \
  -logFile "$WASTECITY_EVIDENCE/baseline-playmode.log"
```

Expected: zero failures. A pre-existing failure is reported before any production implementation.

### Step 4: Mark implementation start without overstating validation

In Docs/06:

- `IDEA-0007`、`IDEA-0008`、`IDEA-0010` change from `未实现` to `开发中`;
- `IDEA-0009` remains `已实现待验证`;
- add approved design and plan links;
- do not add verification evidence or commits that do not yet exist.

### Step 5: Commit the approved documentation stage

Stage only the five approved human-authored docs/spec/plan paths, run `git diff --cached --check`, and commit:

```text
docs: approve 3d usability follow-up design
```

Do not push yet. Report the commit SHA and clean source/scene state.

---

## Task 1: Characterize Housing Across Inner City, Ground, Construction and Evacuation (`IDEA-0009`)

**Files:**

- Modify: `Assets/_Game/Tests/EditMode/BuildingMobilityRulesTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

### Step 1: Add requirement-specific tests before production changes

Add test names containing `IDEA0009` and messages naming Housing, surface and city mode:

- exact four-mode/two-surface matrix;
- one canonical Housing definition with unchanged health/cost/footprint on both surfaces;
- Mobile + InnerCity construction succeeds through `TryBeginConstruction`;
- Mobile + Ground fails through `BuildingPlacementEvaluation` with city-mode failure;
- Fortress + Ground succeeds, spends once, occupies once, completes in place;
- Fortress ground Housing enters the existing evacuation manifest and can use an approved existing treatment;
- real virtual keyboard/mouse scene path exercises Mobile inner placement, Mobile ground rejection, Fortress ground placement and evacuation entry.

### Step 2: Run characterization tests against untouched production code

Run the three focused classes. Expected: the approved baseline behavior should pass. This is intentionally not labeled RED if it passes immediately.

If any test fails:

- preserve the failing XML/log;
- report the exact mismatch;
- do not modify Building Catalog or mobility rules until the user sees the boundary report.

### Step 3: Commit tests only

After focused EditMode and PlayMode pass, commit exact test paths:

```text
test: verify exterior housing workflow
```

No production file should be present in this commit.

---

## Task 2: Extract the Single Mining Resource Compatibility Rule (`IDEA-0010`)

**Files:**

- Create: `Assets/_Game/Scripts/Building/BuildingResourceNodeCompatibilityRules.cs`
- Create: matching `.meta`
- Create: `Assets/_Game/Tests/EditMode/BuildingResourceNodeCompatibilityRulesTests.cs`
- Create: matching `.meta`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs`

### Step 1: Write RED tests

Freeze this compatibility matrix:

- MiningStation + Iron => true;
- MiningStation + EnergyCrystal => true;
- MiningStation + null/empty/Stone/other resources => false;
- Housing and every other catalog definition + Iron/EnergyCrystal => false;
- an equivalent non-canonical definition reusing the MiningStation stable ID must follow the stable-ID contract, not object reference identity;
- null definition never matches.

Also add a source/behavior regression proving placement requests call the new rule and retain coordinate-based node IDs.

### Step 2: Run RED

Expected failure: only the missing `BuildingResourceNodeCompatibilityRules` type/API. Unrelated compilation failures stop the task.

### Step 3: Implement the minimal pure rule

- no Unity API;
- compare the stable building ID and stable resource IDs with ordinal semantics;
- no scene cache, no duplicated list in placement controller;
- replace the old private hardcoded compatibility method with the new core call.

### Step 4: Run GREEN

Run:

- `BuildingResourceNodeCompatibilityRulesTests`;
- `GrayboxBuildingProjectionAndViewTests`;
- `BuildingPlacementEvaluationTests`.

Expected: all pass and existing node visual IDs remain coordinate-based.

### Step 5: Commit

```text
refactor: centralize mining node compatibility
```

---

## Task 3: Build the Rule-Driven Exterior Range Overlay (`IDEA-0008`)

**Files:**

- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`

### Step 1: Write behavioral RED tests

Tests must prove:

- `building.grid.ground` contains only cells accepted by `BuildingRangeRules.IsGroundCellInRange`;
- new stable slot `building.range.ground-boundary` contains exactly the exposed four-neighbor edges of that same set;
- a center near world edge clips only presentation geometry, not the rule result;
- initial radius 8 and each existing supported future radius generate the expected set without copied distance formulas in tests; expected sets are produced by calling the rule per cell;
- `Inactive` hides ground grid, boundary and inner grid;
- CatalogOpen/Previewing/CancelConfirmation show them;
- changing pointer does not rebuild range Mesh;
- unchanged center/radius/world repeated 300 times allocates 0 B after warm-up;
- moving city to a new logical center replaces Mesh once while preserving roots, Renderer, VisualSlot and material identity;
- object count is fixed and no per-cell GameObject appears.

### Step 2: Run RED

Expected failures: current ground grid covers all 64×48 cells and there is no boundary slot/range configuration contract.

### Step 3: Implement minimal range presentation

Add to `GrayboxBuildingWorldView3D`:

- stable `groundBoundary` visual;
- cached center/radius/world key;
- `SetGroundBuildRange(int cityX, int cityY, int radius, int worldWidth, int worldHeight)`;
- rule-driven combined internal grid Mesh and combined boundary Mesh;
- fixed Y offsets and two replaceable transparent colors;
- renderer-only show/hide, with Mesh reuse when key is unchanged.

Update `GrayboxBuildingPlacementController3D.SetBuildGridVisible` so a visible request maps the city through `world.Coordinates`, passes the exact session radius/model size, then toggles presentation. Mapping failure hides the ground overlay and must not guess a center.

Do not change `BuildingRangeRules` or placement evaluation.

### Step 4: Run GREEN and performance tests

Run both modified EditMode classes and existing performance/build tests. Verify `git diff --check`.

### Step 5: Commit

```text
feat: visualize exterior building range
```

---

## Task 4: Highlight All Compatible Mining Nodes and Truly Legal Anchors (`IDEA-0010`)

**Files:**

- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingSessionTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingProjectionAndViewTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`

### Step 1: Write RED tests for session invalidation

Add a non-persisted monotonic `PlacementRevision` contract:

- failed/rolled-back operations do not advance it;
- successful construction commit, cancellation, evacuation commit and construction completion advance it exactly once;
- fixture reset, population/research/route changes that affect evaluation advance it;
- unchecked wrap remains monotonic modulo `uint` as the existing CatalogRevision convention permits;
- it is not serialized and does not affect schema 30.

Run RED: missing property and revision behavior only.

### Step 2: Write RED tests for guidance semantics

Create worlds covering:

- multiple Iron and EnergyCrystal nodes in range;
- compatible nodes outside range;
- incompatible resources;
- a node with multiple legal 2×2 anchors;
- anchors blocked by occupancy, terrain, city footprint, boundary, insufficient inventory, lock or city mode;
- one anchor covering two compatible nodes, requiring de-duplication;
- orientation changes, including a synthetic non-square resource-requiring definition for enumerator correctness while only canonical MiningStation is displayed at runtime.

Assertions:

- every green anchor has a complete formal evaluation with `IsValid=true`;
- a node is green if at least one associated anchor is legal;
- a compatible node with zero legal anchors is dark yellow;
- out-of-range and incompatible nodes have no active highlight;
- leaving MiningStation selection hides all guidance;
- unchanged refresh preserves roots/Mesh/slots and allocates 0 B over 300 calls;
- changing inventory, grid, city mode/center, orientation or session revision refreshes on the next real input frame.

Run RED: current code highlights only the node under the pointer and destroys its visual on hide.

### Step 3: Implement the session revision

Add `PlacementRevision` and increment only after committed changes that can alter placement evaluation. Keep `CatalogRevision` semantics intact; do not substitute one revision for the other.

### Step 4: Implement candidate evaluation through the existing request path

In placement controller:

- retain reusable node, anchor, dedupe and evaluation workspaces for configured lifetime;
- enumerate compatible in-range nodes only while canonical MiningStation is active;
- enumerate footprint-covering anchors from current orientation;
- construct each request through the same request builder used by pointer preview;
- execute `BuildingPlacementRules.Evaluate` with reusable workspace;
- store only presentation data after each evaluation;
- compare the complete refresh key before scanning;
- expose `RefreshMiningGuidance()` for the building input router; do not add a second Update loop;
- clear guidance on cancel, disable, destroy or non-mining selection.

### Step 5: Implement pooled presentation and UI legend

In WorldView:

- replace destroy-on-hide node behavior with keyed inactive reuse;
- use green/dark-yellow node state;
- pool legal anchor footprint visuals by stable coordinate/orientation ID;
- expose diagnostic active counts for tests without exposing rule authority;
- keep preview red/green behavior unchanged.

In MenuView:

- add one MiningStation-only legend root/text;
- exact meaning: `绿色：当前可建造位置` and `暗黄色：资源兼容，但当前条件不满足`;
- hide it for other definitions and inactive state;
- no new building legality calculation in UI.

### Step 6: Run GREEN

Run compatibility, projection/view, session and UI/input classes. Confirm existing pointer-specific red/green preview tests still pass.

### Step 7: Commit

```text
feat: guide legal mining station placement
```

---

## Task 5: Create the Usability Assembly and TDD the Display Settings Model (`IDEA-0007`)

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/Usability.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Usability/WasteCity.Graybox3D.Usability.asmdef`
- Create: matching `.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsModel3D.cs`
- Create: matching `.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsAdapters3D.cs`
- Create: matching `.meta`
- Create: `Assets/_Game/Tests/EditMode/GrayboxUsabilityTests.cs`
- Create: matching `.meta`
- Modify: `Assets/_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef`
- Modify: `Assets/_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef`
- Modify: `Assets/_Game/Editor/WasteCity.Editor.asmdef`

### Step 1: Write RED tests for pure settings behavior

Freeze:

- only Windowed and FullScreenWindow are public player choices;
- available resolutions are positive, de-duplicated and deterministically sorted;
- current resolution is included if platform enumeration omits it;
- load accepts only version 1 and valid enum/dimensions;
- corrupt/unknown/unsupported stored values fall back to current platform state;
- staged changes do not call platform or store;
- Apply calls platform once, then persists exact applied values and calls Save once;
- platform failure writes nothing and preserves last-applied state;
- Cancel restores staged state to last applied without platform call;
- Restore Defaults stages 1920×1080 + FullScreenWindow or the deterministic nearest supported fallback, but does not apply until Apply;
- no type references `SaveService` or `GameSaveData`.

Use fakes for store/platform. Separately test real PlayerPrefs adapter with only the four approved keys and delete exactly those keys in teardown.

### Step 2: Run RED

Expected: new assembly/model/adapters do not exist. Any unrelated compile error is a stop gate.

### Step 3: Add the assembly and minimal model/adapters

Assembly references:

- `WasteCity.Game`;
- `WasteCity.Graybox3D`;
- `WasteCity.Graybox3D.Building`;
- `Unity.InputSystem`;
- `Unity.ugui`.

Add test/editor references to `WasteCity.Graybox3D.Usability`; add `Unity.ugui` to PlayMode tests if their new runtime UI test directly uses UGUI types.

Implement:

- immutable settings value;
- two-value window-mode enum;
- store and platform interfaces;
- pure staged model;
- PlayerPrefs store with approved keys/version;
- Unity Screen platform adapter using `Screen.resolutions`, `Screen.currentResolution` and `Screen.SetResolution`;
- deterministic fallbacks and Development-only diagnostics, without Development-only UI.

### Step 4: Run GREEN

Run `GrayboxUsabilityTests` and `GameSpeedTests`; run no-headless Screen assertions that depend on actual display changes—those remain adapter/scene validation with fakes for deterministic logic.

### Step 5: Commit

```text
feat: add graybox display settings model
```

---

## Task 6: TDD the Modal System Menu, Pause Ownership and Exit Confirmation (`IDEA-0007`)

**Files:**

- Modify: `Assets/_Game/Scripts/Core/GameSpeedModel.cs`
- Create: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuController3D.cs`
- Create: matching `.meta`
- Create: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxSystemMenuView3D.cs`
- Create: matching `.meta`
- Modify: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxDisplaySettingsAdapters3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GameSpeedTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxUsabilityTests.cs`

### Step 1: Write RED tests

Pause tests:

- `GamePauseReason.SystemMenu` is independent from User/Title/Session/Defeat/Advancement;
- opening at requested speed 0.5/1/2 produces effective 0;
- closing restores the prior requested speed;
- another active pause reason keeps speed at 0 after menu closes;
- disable/destroy releases only the SystemMenu reason and cannot leave `Time.timeScale=0` from its own ownership.

Menu/controller tests:

- Main/Settings/OperationGuide/ExitConfirm transitions are deterministic;
- Continue closes;
- Settings Cancel discards staging;
- Settings Apply uses the model;
- Restore Default only stages;
- Quit opens confirmation and shows no-save warning;
- cancellation returns to Main;
- confirmation invokes `IGrayboxApplicationExit` exactly once;
- opening/canceling never invokes save or exit;
- Development/Release resolve identical visible controls;
- pointer blocker and keyboard focus are active only while menu is open;
- generated UI survives disable/enable without duplicate listeners or roots.

### Step 2: Run RED

Expected failures: missing SystemMenu pause reason, controller and view.

### Step 3: Implement minimal menu controller and view

Controller owns:

- menu page state;
- one `GameSpeedModel` instance and SystemMenu pause reason;
- last valid requested speed;
- settings model;
- exit adapter seam and one-shot guard;
- fail-safe cleanup on disable/destroy.

View owns only UGUI creation, visibility, labels, fields, event focus and callbacks. It must create:

- modal dimmer/blocker;
- visible paused title;
- Continue/Settings/Quit;
- resolution dropdown, Windowed/FullScreenWindow selector, Apply/Cancel/Restore Default;
- Operation Guide with actual B/F/R/Delete/right-click/WASD/mouse/Home/Esc controls;
- explicit `当前 3D 灰盒进度不会保存` exit warning;
- confirm/cancel exit buttons.

Production exit adapter uses Editor stop-play under `UNITY_EDITOR` and `Application.Quit()` in Player. No test calls the real exit adapter.

### Step 4: Run GREEN

Run `GrayboxUsabilityTests` and `GameSpeedTests`; verify test teardown always restores `Time.timeScale=1` and deletes only approved PlayerPrefs keys.

### Step 5: Commit

```text
feat: add modal graybox system menu
```

---

## Task 7: Integrate Esc Cancellation Priority and Global Input Suppression (`IDEA-0007`)

**Files:**

- Create: `Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs`
- Create: matching `.meta`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs`
- Modify: `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildingUiAndInputTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxUsabilityTests.cs`

### Step 1: Write RED tests for evacuation cancellation

Freeze `TryCancelManifest()`:

- false when no manifest or processing has begun;
- true for an open unprocessed manifest;
- clears manifest assignments and local work;
- sets `IsManifestOpen=false`;
- hides evacuation UI;
- releases `SetConstructionCancellationBlocked(false)`;
- does not mutate buildings, inventory, city mode or deployment request;
- repeated cancellation is idempotent.

Run RED before implementation.

### Step 2: Write RED tests for Esc and suppression

Using a real virtual Keyboard where possible and controlled fakes only for collaborators, test one press at a time:

1. focused building input consumes Escape;
2. construction confirmation rejects/cancels and consumes;
3. open evacuation manifest calls `TryCancelManifest` and consumes;
4. evacuation processing consumes without interruption;
5. Previewing cancels preview only;
6. CatalogOpen closes/returns only;
7. next idle Escape opens system menu;
8. menu Escape closes or goes back one page;
9. opening with Development panel visible closes that panel;
10. menu-open suppression blocks Move/Deployment/Destination/CameraDrag/Home and does not delegate building input;
11. closing returns suppression ownership on the following frame without replaying the Escape.

### Step 3: Implement truthful Escape consumption in building input

Add a read-only per-call `LastEscapeConsumed` diagnostic reset at the start of each `ProcessCurrentInput`. Set it only when the building/focus/evacuation path actually owns the current Escape.

Add the approved evacuation cancellation method. Processing remains non-cancelable; Esc is consumed without opening system menu.

### Step 4: Implement the coordinator

`GrayboxUsabilityInputCoordinator3D`:

- implements `IGrayboxInputInterceptor`;
- if menu open, delegates only to system menu input and returns SuppressAll;
- otherwise calls building input exactly once;
- opens menu only when Escape was pressed and building input reports it was not consumed;
- closes an open Development panel before opening the menu;
- never polls or duplicates placement, deployment, camera or evacuation rules;
- on teardown closes its menu-owned pause safely.

### Step 5: Run GREEN and existing input regressions

Run:

- `GrayboxUsabilityTests`;
- `GrayboxBuildingUiAndInputTests`;
- `GrayboxCameraAndInputTests`;
- `GrayboxEvacuationTests`.

### Step 6: Commit

```text
feat: coordinate escape and system menu input
```

---

## Task 8: Author the Formal 3D Scene and Prove Real Player Input

**Files:**

- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs`
- Create: `Assets/_Game/Tests/PlayMode/GrayboxUsabilityRuntimeSceneTests.cs`
- Create: matching `.meta`
- Modify: `Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs`

### Step 1: Reconfirm Unity instance and capture scene identity

Before mutation:

- reconnect/lock the exact WasteCity worktree instance;
- record scene GUID, current scene file SHA-256 and protected foundation GlobalObjectIds;
- verify zero Missing Script and one EventSystem;
- do not operate any other open project.

### Step 2: Write scene contract RED tests

Require:

- one `SystemMenuCanvas` with higher sorting order than BuildingCanvas;
- one View, Controller and Coordinator;
- coordinator serialized references to building input and system menu;
- base `GrayboxInputRouter` interceptor points to coordinator;
- one EventSystem/InputSystemUIInputModule;
- no FormalPrototype/2D controllers;
- stable `building.range.ground-boundary` infrastructure slot after runtime rehydrate;
- Usability assembly present in Editor and both test assembly references;
- authoring source calls a dedicated idempotent usability contract.

Run RED against the current scene.

### Step 3: Extend idempotent authoring

- refactor `EnsureBuildingContract` only as needed to return explicit references;
- add `EnsureUsabilityContract` using those references;
- create stable hierarchy and serialized links;
- configure SystemMenuCanvas sorting/raycast behavior;
- change the existing input interceptor reference to coordinator;
- preserve all existing foundation objects, terrain identities, city position and stable IDs;
- do not use runtime `FindObjectOfType` to repair missing serialized links.

Run authoring twice. Require second-run scene bytes/hash to equal first-run output and protected GlobalObjectIds unchanged.

### Step 4: Write PlayMode real-input RED tests before scene GREEN

Use Input System virtual keyboard/mouse and the actual loaded scene:

- Previewing Escape cancels only; next Escape opens system menu;
- CatalogOpen Escape closes only; next Escape opens;
- evacuation manifest Escape closes and releases lock; processing Escape does not interrupt;
- menu open freezes construction/evacuation/city/leader time and blocks WASD, F, right-click destination, middle drag and Home;
- menu close restores previous requested speed;
- buttons are reached through real UGUI pointer/navigation dispatch;
- Settings selection calls a scene test platform/store seam without changing schema save;
- Quit confirmation uses injected fake and never closes Test Runner;
- build mode toggles exact range visuals;
- MiningStation selection shows all approved compatible nodes and legal anchors, then changes colors after a real input-frame inventory/occupancy/mode mutation;
- Housing real-input scenario from Task 1 remains green.

### Step 5: Run scene GREEN

Run focused EditMode scene contract and both PlayMode building/usability runtime classes. Inspect Console for exceptions, Missing Script, Missing Shader and duplicated EventSystem/listeners.

### Step 6: Commit

```text
feat: wire 3d usability into graybox scene
```

---

## Task 9: Update Quality Catalog, Controlled Status and Generated Documentation

**Files:**

- Modify: `Docs/Engineering/project-quality-catalog.json`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify generated Docs only through tools.
- Modify Project Quality tests only if the catalog schema requires explicit current-file fixtures.

### Step 1: Add catalog RED expectations

Before catalog edits, update existing catalog/integration tests if needed to require:

- `IDEA-0007`–`IDEA-0010` feature-group mappings;
- new source, tests, assembly, scene and UI entries;
- `BuildingResourceNodeCompatibilityRules` as Building-core reusable rule;
- display settings model/store/platform boundary;
- system menu/coordinator as scene-scoped UI/input components;
- PlayerPrefs setting store explicitly outside schema 30;
- no scene-specific View promoted to a general reusable component without review.

Run Project Quality RED; expect only missing/stale catalog mappings.

### Step 2: Update catalog and statuses truthfully

- set automated implementation status to `已实现待验证` only after focused scene tests pass;
- keep user playtest explicitly pending;
- record actual commit SHAs only after they exist;
- add machine evidence paths/counts only after final tests complete;
- do not claim Windows launch smoke on macOS.

### Step 3: Generate deterministic docs

Create an explicit changed-path file outside the repo, then run:

```bash
WASTECITY_QUALITY_CHANGED_PATHS="$WASTECITY_EVIDENCE/changed-paths.txt" \
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.GenerateDocumentation \
  -logFile "$WASTECITY_EVIDENCE/generate-docs.log"

"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation \
  -logFile "$WASTECITY_EVIDENCE/validate-docs.log"
```

Run generation twice and require structural generated hashes to be identical. Validation must be read-only.

### Step 4: Run Project Quality GREEN and commit

Run all six Project Quality test classes. Commit exact catalog, Docs/06 and generated paths:

```text
docs: catalog 3d usability follow-up
```

---

## Task 10: Full Regression, Builds, Scene Review and Independent Review

### Step 1: Focused final verification

Run all affected classes together and save XML/logs. Use the failure analyzer for any failure; rerun the exact suggested class before broader retries. Do not suppress flaky failures—record first failure and subsequent result.

### Step 2: Full EditMode excluding TerrainAssetDeep

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode -testCategory '!TerrainAssetDeep' \
  -testResults "$WASTECITY_EVIDENCE/final-editmode.xml" \
  -logFile "$WASTECITY_EVIDENCE/final-editmode.log"
```

Expected: zero failed, zero incomplete. Record actual counts.

### Step 3: Full PlayMode

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testResults "$WASTECITY_EVIDENCE/final-playmode.xml" \
  -logFile "$WASTECITY_EVIDENCE/final-playmode.log"
```

Expected: zero failed, zero incomplete. Record actual counts.

### Step 4: Headless compile

Run Unity batchmode with `-quit` and no tests, save log, require exit 0 and no compiler errors.

### Step 5: Build the two affected 3D Windows variants

Run:

- `WasteCity.Editor.FormalBuildTools.BuildWindows`;
- `WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment`.

Verify each executable exists and `file` reports PE32+ x86-64. The frozen legacy 2D build is not required because no 2D production/scene/build setting changed; if any validation reveals otherwise, stop as scope expansion.

macOS may record build success and PE structure only, not a Windows 10/11 launch smoke.

### Step 6: Unity scene validation

With the correct WasteCity instance locked:

- open `GrayboxPrototype3D`;
- inspect hierarchy, serialized references and Canvas order;
- enter Play Mode;
- exercise Esc cancellation chain, menu pause/continue, settings Apply/Cancel, operation guide, exit cancel using safe path;
- exercise build range, Housing and MiningStation guidance;
- inspect the real scene camera for legibility, terrain Z-fighting and modal occlusion;
- require clean Console.

Record this as developer scene verification, not user playtest.

### Step 7: Independent review pass

Perform a separate review from the exact base SHA to HEAD:

- correctness and failure paths;
- no duplicate range/compatibility/evaluation truth;
- input priority and pause ownership;
- PlayerPrefs/schema separation;
- pooled object and allocation behavior;
- lifecycle cleanup and listener duplication;
- stable IDs/metas/scene identity;
- test quality, especially real input versus direct method calls;
- catalog/document status accuracy;
- protected 2D/terrain/Persistence scope unchanged.

Resolve findings with focused RED/GREEN tests. Any required production file outside the File Map stops the plan.

### Step 8: Record verification snapshot

After final implementation commit exists, run `RecordVerification` with:

- actual verified commit SHA;
- ISO-8601 timestamp;
- final EditMode/PlayMode XML paths;
- compile log;
- build summary file;
- human playtest value explicitly set to pending/not confirmed.

Then run documentation validation again and commit only truthful final verification output:

```text
test: verify 3d usability follow-up
```

---

## Task 11: Final Scope Audit and Ordinary Push

### Step 1: Audit the complete diff

Run:

```bash
git status --short
git diff --check origin/codex/playtest-fixes...HEAD
git diff --name-status origin/codex/playtest-fixes...HEAD
git log --oneline --decorate origin/codex/playtest-fixes..HEAD
```

Compare every changed production path to this plan. Confirm:

- no schema/Persistence changes;
- no FormalPrototype or frozen 2D production changes;
- no terrain/deep asset changes;
- no Packages/ProjectSettings changes except none expected;
- every new production/test file has a `.meta` and catalog entry;
- no test evidence or build products were accidentally added;
- worktree is clean after final commit.

### Step 2: Push normally

```bash
git push -u origin codex/3d-usability-followup
```

Do not force, merge, create Release or automatically merge/open/modify PR #1. Report branch, final SHA, commit list, test/build evidence, unresolved Windows launch/user-playtest gates and any known limitations.

## Stop Gates

Stop immediately and report if any of the following occurs:

- exact remote baseline changes unexpectedly or branch ancestry is wrong;
- correct WasteCity Unity instance cannot be identified/locked;
- unrecognized worktree modification appears;
- baseline tests fail before implementation;
- Housing characterization contradicts the approved “baseline already implemented” finding;
- implementation needs a production file outside the File Map;
- schema 30, Persistence, frozen 2D, terrain source/import/array/Shader or ProjectSettings would need modification;
- range or mining feature cannot consume the existing rule/evaluation truth without duplication;
- system menu requires a setting with no real runtime consumer;
- scene authoring second run is not byte-stable or protected GlobalObjectIds change;
- tests can pass only by directly invoking internals instead of the required real input path;
- full EditMode/PlayMode, compile or either affected Windows build fails;
- documentation generator/validator detects unplanned drift;
- independent review finds a correctness issue whose fix exceeds approved scope.
