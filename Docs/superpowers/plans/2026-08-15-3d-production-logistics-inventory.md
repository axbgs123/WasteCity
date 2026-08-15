# 3D 生产、物流、建筑缓存与玩家背包实施计划

> 日期：2026-08-15<br>
> 状态：已批准需求的可执行 TDD 计划<br>
> 受控需求：`IDEA-0011`<br>
> 权威规格：`Docs/superpowers/specs/2026-08-15-3d-production-logistics-inventory-design.md`<br>
> 精确基线：`486e874c8d345f8751695f01cc21160f85be357d`<br>
> 工作分支：`codex/production-logistics-observability`

## Goal

只在默认正式 3D 场景中完成采矿 → 冶炼 → 装配闭环、逐建筑缓存、城市物流访问、仓库容量、生产观察、12 格玩家背包、真实拖拽和七种资源占位表现；保持 `IDEA-0010` 的铁矿/能晶兼容、`CityOperationalRules` 倍率、schema 30、现有放置/施工/撤离和冻结 2D 不变。

## Global Constraints

- 每个实现任务先写并运行失败测试，保存 RED XML/log，再做最小实现并保存 GREEN；
- 2D `FormalPrototype`、`TechnologyProductionController`、`LogisticsNetworkModel` 不增加功能或接线；
- 不复制稳定 ID、放置合法性、资源兼容性、城市模式、范围或节点余量判断；
- 不修改 `FormalSaveData`、`FormalSaveController`、schema `30`；
- 不写敌人、炮塔、电力、传送带、工人、跨城运输或新资源；
- 有效时间必须调用 `CityOperationalRules.ProductionMultiplier`；
- 采矿兼容继续允许铁矿与能晶；能晶只要求采集、缓存、背包和占位，不扩展消费链；
- 工作区出现计划外修改时停止，不覆盖其他代理文件；
- 新 Unity 文件和文件夹创建稳定 `.meta`；不运行会重写无关资产的广泛导入；
- 本阶段不运行 `TerrainAssetDeep`，除非实际修改地形源/importer/Builder/数组或进入发布候选；
- 自动化通过不得写成人工试玩通过；最终状态保持待用户验收；
- 不 force-push、不合并 PR、不创建 Release。

## Planned File Map

实现前可按程序集现状微调文件名，但任何超出以下职责的生产文件必须先报告。

### New 3D domain/runtime

```text
Assets/_Game/Scripts/Graybox3D/Production/GrayboxProductionDefinition3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxBuildingCache3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxBuildingProductionState3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxProductionSimulation3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxPlayerInventory3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxInventoryTransfer3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxProductionController3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxInventoryView3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxResourcePresentationCatalog3D.cs
Assets/_Game/Scripts/Graybox3D/Production/GrayboxResourceNodeIdentity3D.cs
```

对应 folder/file `.meta` 一并新增。优先放入现有 `WasteCity.Graybox3D.Building` 可引用的程序集；若需要新 asmdef，先验证无循环引用。

### Expected existing production changes

```text
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingPlacementController3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingMenuView3D.cs
Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs
Assets/_Game/Scripts/Graybox3D/Usability/GrayboxUsabilityInputCoordinator3D.cs
Assets/_Game/Editor/GrayboxSceneAuthoring.cs
Assets/_Game/Scenes/GrayboxPrototype3D.unity
```

### New/updated tests

```text
Assets/_Game/Tests/EditMode/GrayboxProductionCatalog3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxBuildingCache3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxProductionLogistics3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxProductionSimulation3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxPlayerInventory3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxInventoryTransfer3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxProductionSession3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxProductionUiAndInput3DTests.cs
Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs
Assets/_Game/Tests/PlayMode/GrayboxProductionInventoryRuntimeSceneTests.cs
```

旧 `ProductionTests.cs` 保留为共享回归，不把新 3D 语义塞回旧聚合控制器。

### Assets and documentation

```text
ArtSource/FirstPass/UI/ResourceIcons/References/Generated/**
ArtSource/FirstPass/UI/ResourceIcons/Masters/**
Assets/_Game/Art/FirstPass/UI/ResourceIcons/**
Docs/Art/FirstPass/UI/ResourceIcons/ResourceIcons_7Item_AssetRecord.md
Docs/01-Game-Design-Document-ZH.md
Docs/05-Formal-Development-Roadmap-ZH.md
Docs/06-User-Feedback-and-Change-Control-ZH.md
Docs/07-Project-Use-and-Development-Guide-ZH.md
Docs/08-Testing-and-Bug-Location-Guide-ZH.md
Docs/09-Reusable-Project-Catalog-ZH.md
Docs/Engineering/project-quality-catalog.json
Docs/Generated/**
```

---

## Task 0: Lock Baseline and Start `IDEA-0011`

### Step 1: Verify repository and protected boundaries

Run and record：

```powershell
git status --short --branch
git rev-parse HEAD
git rev-parse origin/codex/3d-usability-followup
git lfs status
git lfs fsck --pointers
Get-Content ProjectSettings/ProjectVersion.txt
```

Expected: branch is `codex/production-logistics-observability`, ancestry begins at exact handoff, no unexpected worktree modifications, LFS materialized, Unity `2022.3.62f1`.

### Step 2: Inspect Unity ownership

If Unity MCP exists, require its resolved project path to equal this exact worktree before any call. If only another project is present, do not operate it. For batch runs, ensure no GUI process owns this project lock.

### Step 3: Mark development start

After the independently pushed approval commit, change Docs/06 to `开发中` in the isolated design-stage commit that adds the specification and implementation plan. Link both documents, but do not claim implementation or validation.

### Step 4: Commit and push documentation stage

Stage only approved human-authored requirement/spec/plan files, validate whitespace, commit separately and ordinary-push current branch. Recheck clean status before source work.

---

## Task 1: Production Configuration and Resource Identity (`IDEA-0011`)

### Step 1: Write RED tests

Create `GrayboxProductionCatalog3DTests` and resource identity tests proving:

- exact stable IDs map to 3/6/6-second definitions and 20/20+10/20+30 capacities;
- smelter is 2 iron → 1 alloy; assembler is 2 alloy → 2 ammunition;
- turret has no production definition;
- unknown or forged IDs fail closed;
- mining output comes from the bound node resource;
- both Iron and EnergyCrystal remain compatible through `BuildingResourceNodeCompatibilityRules`;
- stable node ID create/parse round-trips coordinates and rejects malformed/out-of-range values.

Run the focused fixture and retain a genuine failure before implementation.

### Step 2: Implement minimal immutable catalog

Add the definition/catalog and shared node identity. Replace placement controller's private ID formatting with the shared creator without changing the resulting strings. Do not add recipe values to controllers or UI.

### Step 3: GREEN and regressions

Run the new fixture plus existing `BuildingResourceNodeCompatibilityRulesTests`, placement evaluation and mining guidance tests. Confirm `IDEA-0010` remains unchanged.

---

## Task 2: Per-Building Cache and Lifecycle Ownership

### Step 1: Write RED cache tests

Prove allowed resource restrictions, capacity, partial acceptance, atomic spending, no debt, no negative values and conservation across failed operations.

### Step 2: Write RED session lifecycle tests

Prove:

- under-construction buildings do not produce and expose no usable transfer target;
- completed production buildings own independent state;
- the mining instance retains the exact approved node binding;
- cancellation leaves no cache;
- evacuation lock, abandonment and removal stop operation and remove logistics visibility;
- non-production buildings have no production state;
- repeated setup creates no duplicate runtime state.

### Step 3: Implement cache and state ownership

Wrap `ResourceInventory` behind resource-specific methods. Extend `GrayboxBuildingInstance3D` and session creation/completion minimally. Preserve all construction rollback paths: presentation failure, grid failure and refund failure must also restore production-state ownership.

### Step 4: GREEN

Run cache/session fixtures and existing session, construction and evacuation suites.

---

## Task 3: Logistics Connectivity, Fair Access and Warehouse Capacity

### Step 1: Write RED connectivity tests

Cover:

- inner-city connection;
- ground connection requires Fortress and every footprint cell inside existing range;
- city movement/mode transition disconnects without deleting cache;
- disconnected buildings cannot see city inventory or other caches;
- reconnection restores access;
- distant buildings inside range share without adjacency;
- an outside building remains disconnected even when adjacent to a connected building;
- warehouse increases each city resource capacity by 150 but never changes range.

### Step 2: Write RED allocation tests

Use two mines, two smelters and one assembler. Prove source-cache-first behavior, city-inventory fallback, stable source ordering, per-unit round-robin fairness and total conservation.

### Step 3: Implement derived connection and logistics broker

Connection must call `BuildingRangeRules`; do not import `LogisticsNetworkModel`. The broker receives eligible instance snapshots and city inventory, transfers only through cache APIs and allocates no per-frame collections after warm-up.

### Step 4: Implement safe warehouse capacity

Derive desired capacity from completed eligible warehouses. If decreasing capacity would strand city inventory above the target, reject removal/evacuation before mutating grid or presentation; never use destructive clamping. Add visible failure text through the existing evacuation path.

### Step 5: GREEN

Run logistics, warehouse, placement range, mobility and evacuation fixtures.

---

## Task 4: Deterministic Production Simulation

### Step 1: Write RED state-machine tests

Cover exact mining, smelting and assembly cycles; independent progress; input consumption at cycle start; output at completion; output reservation; node depletion; node non-overharvest; manual pause; global pause; mobility; disconnected local continuation; all five stop reasons and reconnect recovery.

### Step 2: Write RED determinism tests

Prove:

- `Tick(12)` equals twelve `Tick(1)` calls;
- `Tick(2.9)+Tick(0.1)` completes the expected base event after multiplier-neutral context;
- Fortress uses `CityOperationalRules.ProductionMultiplier` and therefore advances 25% faster;
- changing input instance list order does not change results;
- blocked buildings do not accumulate unlimited instant-completion debt;
- stable `2 mines → 2 smelters → 1 assembler` produces expected long-run flow.

### Step 3: Implement event-boundary simulation

Use double/float-safe boundary comparison and a guarded event loop. Resolve simultaneous events in dependency order: extraction, smelting, assembly; stable instance ID is the tie-breaker. At every event boundary run logistics allocation before starting new cycles.

### Step 4: Integrate controller

The MonoBehaviour reads existing 3D world model, city mode/center, session radius and scaled rule time, then calls the pure simulator. It must not use `FindObjectsOfType`, LINQ or the old 2D production controller.

### Step 5: GREEN and soak

Run focused tests, then a long deterministic simulation and allocation/per-frame memory checks.

---

## Task 5: Player Inventory and Atomic Transfers

### Step 1: Write RED inventory tests

Cover 12 slots, 99 stack limit, merge-before-empty ordering, partial add, split across slots, full inventory, removal, invalid resource rejection and snapshot stability.

### Step 2: Write RED transfer tests

Cover cache output → backpack, backpack → valid input/output cache, wrong resource, target full, partial acceptance, source invalidation, evacuation lock and rollback after injected target failure. Assert source plus target total is unchanged for all failures.

### Step 3: Implement pure models

Keep UI out of inventory logic. Expose read-only slot snapshots and a revision counter; all mutations go through `GrayboxInventoryTransfer3D`.

### Step 4: GREEN

Run inventory and transfer fixtures, including randomized conservation sequences with a fixed seed.

---

## Task 6: 3D Building Details, `E` Input and Real Drag

### Step 1: Write RED EditMode UI/input tests

Prove completed building selection by stable collider ID; details display recipe, caches, progress, multiplier, logistics and stop reason; pause button mutates the selected state; `E` toggles inventory only when input priority permits; inventory panel blocks world click-through; closing cancels drag without transfer.

### Step 2: Implement UI with existing UGUI style

Extend the building view for completed selection and add a dedicated inventory view. Pool slots and drag ghost; refresh from revision, not every frame reconstruction. Keep gameplay truth in pure models.

### Step 3: Write RED PlayMode real-input tests

In `GrayboxPrototype3D`, use Input System virtual keyboard/mouse to:

1. open/close with real `E`;
2. select a completed mine through the world collider;
3. drag a cache stack into the backpack;
4. drag a valid resource back;
5. attempt a full/invalid transfer and see failure text;
6. verify camera/building action does not fire through UI;
7. pause/resume a building and observe progress.

Direct calls to internal click/transfer handlers do not satisfy this task.

### Step 4: GREEN

Run focused EditMode and PlayMode, then existing building/usability real-input suites.

---

## Task 7: Resource Placeholder Art and Runtime Presentation

### Step 1: Freeze asset contract with RED tests

Tests must require seven stable resource presentation entries, five world-node entries, non-null fallback, valid Texture2D import settings, no duplicate GUID and scene lookup through the catalog rather than filename guesses.

### Step 2: Generate source art

Use the built-in image-generation tool once per resource to create eight coherent chroma-key source icons, then use the installed deterministic chroma-key removal helper to produce transparent masters. Do not substitute one multi-cell sheet for the eight distinct generation requests. Record:

- model/tool and date;
- full prompts and any reference images;
- generated source files;
- crop, alpha, resize and color adjustments;
- final Unity paths and import settings.

Do not call AI output final art or user-approved art.

### Step 3: Prepare Unity assets

Create seven readable 64×64 icons plus fallback. Ensure no white fringe, alpha clipping or unreadable small detail. World nodes reuse efficient placeholder geometry/materials and remain driven by `WorldMapModel` resource IDs.

### Step 4: Runtime and visual verification

Run asset contract tests, capture inventory/building/world screenshots and inspect them at target resolution. Verify depletion refresh does not create new materials or GameObjects.

---

## Task 8: Scene Authoring and Contract

### Step 1: Write RED scene contract tests

Require exactly one production controller, inventory view/model owner, correct session/world/city/UI/input references, stable object names, expected slot count, fallback resources and no 2D production component.

### Step 2: Update authoring and regenerate scene

Modify `GrayboxSceneAuthoring` first, run the official authoring entry, then inspect the scene diff. Do not hand-maintain a scene state authoring cannot reproduce.

### Step 3: Stability checks

Run authoring twice and compare hashes. Assert no duplicate EventSystem, Canvas, controller, material, mesh or inventory slots. Run focused scene contract and runtime smoke tests.

---

## Task 9: Quality Catalog, User Docs and Reuse Catalog

### Step 1: Update controlled human docs

- GDD/roadmap: link `IDEA-0011`, 3D implementation boundary and approved backpack additions;
- Docs/07: explain E backpack, selecting working buildings, dragging and stop reasons in plain Chinese;
- Docs/08: map failures to focused tests, source owners and repro commands;
- Docs/09: register catalog, cache/inventory transfer, node identity and presentation catalog with reuse restrictions;
- art reference: record AI provenance and replacement workflow.

### Step 2: Update project quality catalog

Map all new production, tests, UI, scene, assets and docs into `economy-production-logistics` or an explicitly approved inventory sub-group. Add scene/component/UI records and minimal rerun filters. Run focused Project Quality tests.

### Step 3: Generate documentation attention

Create an exact UTF-8 changed-path list, set `WASTECITY_QUALITY_CHANGED_PATHS`, and run the official attention generator. Do not use an empty change list.

---

## Task 10: Full Verification, Builds and Handoff

### Step 1: Focused final suites

Run all new EditMode fixtures, existing placement/node compatibility/evacuation/usability fixtures, new PlayMode real-input fixture and scene contracts. Analyze any failure before broader runs.

### Step 2: Full automation

Run daily complete EditMode with `!TerrainAssetDeep`, then complete PlayMode. Keep XML/log files outside the repository. Confirm zero failures and no unexpected skips.

### Step 3: Static and runtime review

Independently inspect:

- no old 2D controller or BFS logistics use;
- no recipe/capacity/range/multiplier duplication;
- no schema/Persistence changes;
- all transfer paths conserve resources;
- no frame-order or instance-order dependency;
- no per-frame allocations/object/material growth;
- icon/world placeholder fallbacks work.

### Step 4: Build matrix

After compilation succeeds, run official Windows Release 3D and Development 3D entry points plus required macOS universal entry if available. Legacy 2D is only a build regression, not a functional acceptance target. Do not claim real Windows GPU/VRAM/RAM validation unless actually performed on Windows 10/11.

### Step 5: Official docs and verification

Run GenerateDocumentation, ValidateDocumentation and AnalyzeTestResults. Only after evidence is complete run RecordVerification. Re-run generation/validation to prove stable output.

### Step 6: Completion audit

For every `IDEA-0011` acceptance item identify direct evidence: test XML, scene contract, build log, screenshot, file/catalog entry or runtime capture. Missing or indirect evidence remains incomplete.

### Step 7: Status, commit and push

Update Docs/06 to `已实现待验证`, list exact implementation commits and automated evidence, and explicitly retain pending user visual/interaction acceptance and real Windows checks. Stage exact files only, commit coherent phases, ordinary-push current branch. Do not merge or create a Release.

## Stop Gates

Stop and report before continuing if any of the following occurs：

- schema 30 or Persistence must change;
- production cannot consume `CityOperationalRules` without changing existing approved city semantics;
- maintaining Iron + EnergyCrystal compatibility would require contradicting `IDEA-0010`;
- an evacuation path can only reduce warehouse capacity by destroying resources;
- Unity tooling points to another project;
- planned implementation needs 2D functional changes;
- new files fall outside approved scope/quality directory;
- source asset/importer/Texture2DArray changes would trigger `TerrainAssetDeep`;
- deterministic/conservation tests reveal unresolved duplication, loss or order dependence;
- required automated or build evidence cannot be produced.
