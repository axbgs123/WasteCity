# Route Capstone Buildings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the missing power plant, metabolic furnace, and consciousness network, normalize the existing spirit-gathering array, and complete the four-route terminal-building loop.

**Architecture:** Extend the existing building catalog and unlock model without adding a parallel construction system. Keep exact capstone production recipes behind small factories consumed by the existing production controller. Gate remote rescue through a pure consciousness-network rule and the building controller's operational count.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Input System `1.7.0`, URP 2D programmatic placeholders, Windows Mono player build.

## Global Constraints

- Follow `Docs/01-Game-Design-Document-ZH.md`, `Docs/05-Formal-Development-Roadmap-ZH.md`, and `Docs/superpowers/specs/2026-08-04-route-capstone-buildings-design.md`.
- Add exactly three buildings: `technology.building.power-plant`, `biological.building.metabolic-furnace`, and `psionics.building.consciousness-network`.
- Treat the existing `cultivation.building.spirit-gathering-array` as the fourth route terminal building.
- All four buildings require population `1000` and their exact route research.
- Use existing proxy resources: energy crystal for energy credits/spirit stones and psionic amplifier for psionic crystals.
- Do not add formal resources, cross-route bridges, multi-city behavior, formal art, or the next description-unification milestone.
- Current save schema remains `28`; append production progress compatibly.

---

### Task 1: Catalog and Unlock Rules

**Files:**
- Modify: `Assets/_Game/Scripts/Building/BuildingGrid.cs`
- Modify: `Assets/_Game/Tests/EditMode/BuildingUnlockTests.cs`
- Create: `Assets/_Game/Tests/EditMode/RouteCapstoneBuildingTests.cs`
- Create: `Assets/_Game/Tests/EditMode/RouteCapstoneBuildingTests.cs.meta`

**Interfaces:**
- Produces: `BuildingCatalog.PowerPlant`, `MetabolicFurnace`, and `ConsciousnessNetwork`.
- Changes: `BuildingCatalog.SpiritGatheringArray.MinimumPopulation` to `1000`.
- Produces: all four capstones in `BuildingCatalog.All` and `BuildMenu`.

- [x] **Step 1: Write failing catalog tests**

Add tests with literal expected IDs proving:

```csharp
Assert.That(BuildingCatalog.PowerPlant.Id.Value,
    Is.EqualTo("technology.building.power-plant"));
Assert.That(BuildingCatalog.MetabolicFurnace.Id.Value,
    Is.EqualTo("biological.building.metabolic-furnace"));
Assert.That(BuildingCatalog.ConsciousnessNetwork.Id.Value,
    Is.EqualTo("psionics.building.consciousness-network"));
```

Assert the four capstones are unique, `2×2`, population `1000`, present once in `All`, and present once in `BuildMenu`. Assert the exact research IDs from the design.

- [x] **Step 2: Write failing unlock tests**

For each capstone, assert `BuildingUnlockModel.IsUnlocked` rejects population `999`, rejects missing research at population `1000`, and accepts population `1000` with its exact research.

Update the existing spirit-gathering test from population `200` to the `999/1000` boundary.

- [x] **Step 3: Run focused EditMode tests to verify RED**

Run `RouteCapstoneBuildingTests` and `BuildingUnlockTests`.

Expected: three definitions are missing and the existing array has no population threshold.

- [x] **Step 4: Implement catalog definitions**

Add exact definitions:

```csharp
PowerPlant = new BuildingDefinition(
    "technology.building.power-plant", "发电站", 2, 2,
    ResourceIds.Alloy, 14, false, 12f, 320, 1000,
    "core.research.thermal-engineering");

MetabolicFurnace = new BuildingDefinition(
    "biological.building.metabolic-furnace", "代谢炉", 2, 2,
    ResourceIds.BoneSteel, 12, false, 12f, 360, 1000,
    "core.research.metabolic-acceleration");

ConsciousnessNetwork = new BuildingDefinition(
    "psionics.building.consciousness-network", "意识网络", 2, 2,
    ResourceIds.ResonanceMetal, 12, false, 14f, 300, 1000,
    "core.research.consciousness-network");
```

Set `SpiritGatheringArray` minimum population to `1000`. Add the three definitions once to both catalog arrays.

- [x] **Step 5: Run focused tests to verify GREEN**

Expected: all capstone catalog and unlock tests pass.

- [x] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Building/BuildingGrid.cs Assets/_Game/Tests/EditMode/BuildingUnlockTests.cs Assets/_Game/Tests/EditMode/RouteCapstoneBuildingTests.cs Assets/_Game/Tests/EditMode/RouteCapstoneBuildingTests.cs.meta
git commit -m "feat: add route capstone building catalog"
```

### Task 2: Capstone Production Recipes and Progress

**Files:**
- Create: `Assets/_Game/Scripts/Economy/RouteCapstoneProductionCatalog.cs`
- Create: `Assets/_Game/Scripts/Economy/RouteCapstoneProductionCatalog.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/RouteCapstoneProductionTests.cs`
- Create: `Assets/_Game/Tests/EditMode/RouteCapstoneProductionTests.cs.meta`
- Modify: `Assets/_Game/Scripts/Economy/TechnologyProductionController.cs`

**Interfaces:**
- Produces: `RouteCapstoneProductionCatalog.CreatePowerPlant()`, `CreateMetabolicFurnace()`, and `CreateConsciousnessNetwork()`.
- Changes: `TechnologyProductionController.CaptureProgress()` length from `11` to `14`.
- Keeps: existing indices `0..10`; appends power plant at `11`, metabolic furnace at `12`, consciousness network at `13`.

- [x] **Step 1: Write failing recipe tests**

Exercise real process behavior:

```csharp
var inventory = new ResourceInventory(100);
var power = RouteCapstoneProductionCatalog.CreatePowerPlant();
Assert.That(power.Tick(5.9f, inventory, 1), Is.Zero);
Assert.That(power.Tick(.1f, inventory, 1), Is.EqualTo(1));
Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.EqualTo(1));
```

For the metabolic furnace, stock `2` biomass, tick `8` seconds, and assert biomass becomes `0` and energy crystal becomes `1`. For consciousness network, tick `10` seconds and assert one psionic amplifier. Assert zero building count produces nothing.

- [x] **Step 2: Run recipe tests to verify RED**

Expected: the capstone production catalog is missing.

- [x] **Step 3: Implement recipe factories**

Return fresh stateful processes:

```csharp
public static PassiveProductionProcess CreatePowerPlant() =>
    new PassiveProductionProcess(ResourceIds.EnergyCrystal, 1, 6f);

public static ProductionProcess CreateMetabolicFurnace() =>
    new ProductionProcess(new ProductionRecipe(
        ResourceIds.Biomass, 2, ResourceIds.EnergyCrystal, 1, 8f));

public static PassiveProductionProcess CreateConsciousnessNetwork() =>
    new PassiveProductionProcess(ResourceIds.PsionicAmplifier, 1, 10f);
```

- [x] **Step 4: Run recipe tests to verify GREEN**

Expected: all capstone production tests pass.

- [x] **Step 5: Connect the production controller**

Instantiate the three processes through the factories. Count completed, logistics-online buildings by exact catalog IDs. Tick them with city, haste, and legacy production multipliers following existing processes.

Include all three in `HasRunningProduction` and the GUI monitor. Append their progress to `CaptureProgress`; restore only when the array contains the corresponding index.

- [x] **Step 6: Run all EditMode tests**

Expected: all EditMode tests pass without changing existing production progress indices.

- [x] **Step 7: Commit**

```bash
git add Assets/_Game/Scripts/Economy/RouteCapstoneProductionCatalog.cs Assets/_Game/Scripts/Economy/RouteCapstoneProductionCatalog.cs.meta Assets/_Game/Scripts/Economy/TechnologyProductionController.cs Assets/_Game/Tests/EditMode/RouteCapstoneProductionTests.cs Assets/_Game/Tests/EditMode/RouteCapstoneProductionTests.cs.meta
git commit -m "feat: add route capstone production"
```

### Task 3: Consciousness Network Remote Link

**Files:**
- Create: `Assets/_Game/Scripts/World/ConsciousnessNetworkRules.cs`
- Create: `Assets/_Game/Scripts/World/ConsciousnessNetworkRules.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ConsciousnessNetworkTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ConsciousnessNetworkTests.cs.meta`
- Modify: `Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- Modify: `Assets/_Game/Scripts/World/RescueSiteController.cs`

**Interfaces:**
- Produces: `ConsciousnessNetworkRules.RemoteLinkAvailable(bool researchCompleted, int operationalNetworks)`.
- Produces: `PlaceholderBuildingController.OperationalCount(string id)`.
- Changes: remote `[J]` rescue requires both completed research and one operational consciousness network.

- [x] **Step 1: Write failing pure-rule tests**

Assert all truth-table branches:

```csharp
Assert.That(ConsciousnessNetworkRules.RemoteLinkAvailable(true, 1), Is.True);
Assert.That(ConsciousnessNetworkRules.RemoteLinkAvailable(false, 1), Is.False);
Assert.That(ConsciousnessNetworkRules.RemoteLinkAvailable(true, 0), Is.False);
Assert.That(ConsciousnessNetworkRules.RemoteLinkAvailable(true, -1), Is.False);
```

- [x] **Step 2: Run focused tests to verify RED**

Expected: the rules type is missing.

- [x] **Step 3: Implement rule and operational count**

`OperationalCount` counts only runtimes whose construction is complete, `HasLogistics` is true, and definition ID matches.

In `RescueSiteController`, cache or lazily find `PlaceholderBuildingController`, then replace both keyboard and GUI research-only checks with one `RemoteLinkAvailable` property derived from research plus operational count.

- [x] **Step 4: Run focused and existing rescue tests**

Expected: all consciousness and rescue rules pass.

- [x] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/World/ConsciousnessNetworkRules.cs Assets/_Game/Scripts/World/ConsciousnessNetworkRules.cs.meta Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs Assets/_Game/Scripts/World/RescueSiteController.cs Assets/_Game/Tests/EditMode/ConsciousnessNetworkTests.cs Assets/_Game/Tests/EditMode/ConsciousnessNetworkTests.cs.meta
git commit -m "feat: gate remote rescue through consciousness network"
```

### Task 4: Formal Scene, Save, and Placeholder Integration

**Files:**
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`

**Interfaces:**
- Consumes: catalog, production, and remote-link APIs from Tasks 1-3.
- Proves: all four terminal buildings restore into the formal scene, retain stable visual IDs, produce resources, and round-trip progress.

- [x] **Step 1: Write failing formal scene test**

Load `FormalPrototype`, apply a schema 28 save with population/capacity `1000`, fortress city mode, the four research IDs, and four completed building snapshots at non-overlapping connected coordinates.

Clear title pause, use an accelerated time scale, and advance at least `10` scaled seconds. Assert:

- all four `BuildingRuntime` objects exist and have logistics;
- each runtime's `VisualSlot.StableId` equals its definition ID;
- energy crystals increased by at least `3` from power plant, spirit gathering, and metabolic furnace;
- biomass decreased by at least `2`;
- psionic amplifier increased by at least `1`;
- `CaptureProgress()` length is `14`;
- `CaptureComplete().buildings` contains all four IDs.

- [x] **Step 2: Write progress compatibility test**

On the formal production controller, call `RestoreProgress` with a literal `11`-value array and assert `CaptureProgress()` preserves indices `0..10` while indices `11..13` are zero.

- [x] **Step 3: Run the runtime fixture to verify RED**

Expected: production is not wired and progress length remains `11`.

- [x] **Step 4: Fix only integration gaps revealed by the tests**

Do not add new systems. Ensure formal building restoration creates runtime placeholders through the existing catalog and construction completion callback.

- [x] **Step 5: Run formal save and runtime tests to verify GREEN**

Expected: all tests pass and schema remains `28`.

- [x] **Step 6: Commit**

```bash
git add Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs Assets/_Game/Tests/EditMode/FormalSaveTests.cs
git commit -m "test: verify route capstone integration"
```

### Task 5: Final Quality Gate, Baseline, and GitHub Update

**Files:**
- Modify: `README.md`
- Modify: `Docs/00-README-ZH.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`

**Interfaces:**
- Consumes: exact test totals, compile/build result, and capstone feature commit.
- Produces: the final requested local baseline and pushed GitHub branch.

- [x] **Step 1: Run static checks**

Run `git diff --check`, inspect all current changes, and confirm package files contain no local absolute path.

- [x] **Step 2: Run all EditMode and PlayMode tests**

Use Unity batch mode without `-quit`; require zero failed, skipped, or inconclusive tests.

- [x] **Step 3: Run headless compile and Windows build**

Require zero C# errors and a successful `WasteCity.Editor.FormalBuildTools.BuildWindows` exit.

- [x] **Step 4: Restore portable package configuration**

Set Input System manifest version to `1.7.0`, lock source to `registry`, and URL to `https://packages.unity.com`.

- [x] **Step 5: Update baseline documents**

Record the capstone milestone as implemented, exact test counts, schema `28`, compile/build result, proxy-resource limitation, and still-pending real Windows smoke.

- [x] **Step 6: Commit documentation**

```bash
git add README.md Docs/00-README-ZH.md Docs/05-Formal-Development-Roadmap-ZH.md
git commit -m "docs: record route capstone baseline"
```

- [x] **Step 7: Verify final tree**

Re-run the complete tests on the exact committed tree, inspect recent commits, and require a clean worktree.

- [x] **Step 8: Push GitHub branch**

Resolve the configured `origin`, confirm it is the requested WasteCity repository, and push `codex/fix-foundation` without force. Do not merge to the default branch or create a public release.

- [x] **Step 9: Stop**

Report the pushed branch and remaining real-Windows smoke limitation. Do not begin route descriptions or any later roadmap item.
