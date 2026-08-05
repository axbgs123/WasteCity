# Building Mobility Tags Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `IDEA-0001` F1A building placement/operation tags, persist each placed building's actual site, and make all current building effects obey mobile versus fortress rules.

**Architecture:** `BuildingDefinition` owns immutable placement and operation tags; `PlacedBuilding`/`BuildingRuntime` own the actual site. A pure `BuildingMobilityRules` class is the only authority for site support, construction, and operation. Runtime consumers use `BuildingRuntime.IsOperational`, while current player placement remains explicitly ground-only until the separate inner-city interaction milestone.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, JSON save schema `29`, Windows Mono x86-64 player build.

## Global Constraints

- Stable requirement: `IDEA-0001`.
- Follow `Docs/superpowers/specs/2026-08-05-building-mobility-tags-design.md`.
- Do not change `BUG-0001` build-menu opening, visibility, filtering, or selection.
- Do not add an inner-city UI, outpost inventory, remote control, pathfinding, or new resources.
- Existing mouse placement, templates, and schema `28` or older buildings restore as `BuildingSite.Ground`.
- Preserve unrelated local Package Manager changes and stage only files named by this plan.
- Use existing 2D placeholders and stable IDs.

---

### Task 1: Building tags and pure mobility rules

**Files:**
- Create: `Assets/_Game/Scripts/Building/BuildingMobilityRules.cs`
- Create: `Assets/_Game/Scripts/Building/BuildingMobilityRules.cs.meta`
- Modify: `Assets/_Game/Scripts/Building/BuildingGrid.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingMobilityRulesTests.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingMobilityRulesTests.cs.meta`

**Interfaces:**
- Consumes: `WasteCity.City.CityMode`, current `BuildingCatalog`.
- Produces:

```csharp
public enum BuildingPlacement { Ground = 0, InnerCity = 1, Either = 2 }
public enum BuildingOperation { MobileAllowed = 0, FortressOnly = 1, TerrainDependent = 2 }
public enum BuildingSite { Ground = 0, InnerCity = 1 }

public static class BuildingMobilityRules
{
    public static bool SupportsSite(BuildingDefinition definition, BuildingSite site);
    public static bool CanConstruct(BuildingDefinition definition, BuildingSite site, CityMode mode);
    public static bool CanOperate(BuildingDefinition definition, BuildingSite site, CityMode mode);
    public static string PlacementName(BuildingPlacement placement);
    public static string OperationName(BuildingOperation operation);
}
```

- Extends `BuildingDefinition` constructor with optional trailing parameters:

```csharp
BuildingPlacement placement = BuildingPlacement.Ground,
BuildingOperation operation = BuildingOperation.FortressOnly
```

- Adds immutable `Placement` and `Operation` properties.

- [x] **Step 1: Write failing rule and catalog tests**

Add literal expectations that catch wrong site branches, transition-state leakage, and missing catalog assignments:

```csharp
[TestCase(CityMode.Mobile, true)]
[TestCase(CityMode.Deploying, false)]
[TestCase(CityMode.Fortress, true)]
[TestCase(CityMode.Packing, false)]
public void InnerCityMobileBuildingOnlyRunsInStableSupportedModes(
    CityMode mode,
    bool expected)
{
    Assert.That(
        BuildingMobilityRules.CanOperate(
            BuildingCatalog.Housing,
            BuildingSite.InnerCity,
            mode),
        Is.EqualTo(expected));
}

[Test]
public void GroundFactoryOnlyRunsAndConstructsInFortress()
{
    Assert.That(
        BuildingMobilityRules.CanConstruct(
            BuildingCatalog.Smelter,
            BuildingSite.Ground,
            CityMode.Mobile),
        Is.False);
    Assert.That(
        BuildingMobilityRules.CanOperate(
            BuildingCatalog.Smelter,
            BuildingSite.Ground,
            CityMode.Mobile),
        Is.False);
    Assert.That(
        BuildingMobilityRules.CanOperate(
            BuildingCatalog.Smelter,
            BuildingSite.Ground,
            CityMode.Fortress),
        Is.True);
}
```

Use a literal table for all 30 `BuildingCatalog.All` entries and their exact tags from the design spec. Do not derive expected tags from IDs or the code under test.

- [x] **Step 2: Run the focused test and verify RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.BuildingMobilityRulesTests" \
  -testResults "/tmp/wastecity-building-mobility/red-rules.xml" \
  -logFile "/tmp/wastecity-building-mobility/red-rules.log"
```

Expected: compiler failure because `BuildingMobilityRules`, `BuildingPlacement`, `BuildingOperation`, and `BuildingSite` do not exist.

- [x] **Step 3: Implement the minimal rules and assign every catalog tag**

Rules:

```csharp
public static bool SupportsSite(BuildingDefinition definition, BuildingSite site)
{
    if (definition == null || !Enum.IsDefined(typeof(BuildingSite), site)) return false;
    return definition.Placement == BuildingPlacement.Either ||
           (definition.Placement == BuildingPlacement.Ground && site == BuildingSite.Ground) ||
           (definition.Placement == BuildingPlacement.InnerCity && site == BuildingSite.InnerCity);
}

public static bool CanConstruct(BuildingDefinition definition, BuildingSite site, CityMode mode)
{
    if (!SupportsSite(definition, site)) return false;
    if (mode == CityMode.Fortress) return true;
    return mode == CityMode.Mobile &&
           site == BuildingSite.InnerCity &&
           definition.Operation == BuildingOperation.MobileAllowed;
}

public static bool CanOperate(BuildingDefinition definition, BuildingSite site, CityMode mode)
    => CanConstruct(definition, site, mode);
```

Assign the exact table from the design spec through the optional constructor arguments. `PlacementName` returns `地面 / 内城 / 两者皆可`; `OperationName` returns `移动可运行 / 仅展开运行 / 地形依赖`.

- [x] **Step 4: Run the focused test and verify GREEN**

Use the Step 2 command with `green-rules.xml`; require all cases passed.

### Task 2: Persist actual building site through grid operations

**Files:**
- Modify: `Assets/_Game/Scripts/Building/BuildingGrid.cs`
- Modify: `Assets/_Game/Tests/EditMode/BuildingGridTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/BuildingUpgradeTests.cs`

**Interfaces:**
- `PlacedBuilding.Site` returns its immutable `BuildingSite`.
- Existing methods remain source-compatible through trailing optional parameters:

```csharp
public bool TryPlace(
    BuildingDefinition definition,
    int x,
    int y,
    ResourceInventory inventory,
    bool coversResource,
    out PlacedBuilding placed,
    BuildingSite site = BuildingSite.Ground);

public bool TryRestore(
    BuildingDefinition definition,
    int x,
    int y,
    out PlacedBuilding placed,
    BuildingSite site = BuildingSite.Ground);

public bool CanPlace(
    BuildingDefinition definition,
    int x,
    int y,
    BuildingSite site = BuildingSite.Ground);
```

- `TryUpgrade` preserves `placed.Site` and rejects a target that does not support that site before spending resources.

- [x] **Step 1: Write failing grid and upgrade tests**

```csharp
[Test]
public void GridDefaultsExistingPlacementCallsToGround()
{
    var grid = new BuildingGrid(8, 8);
    Assert.That(
        grid.TryRestore(BuildingCatalog.Housing, 1, 1, out var placed),
        Is.True);
    Assert.That(placed.Site, Is.EqualTo(BuildingSite.Ground));
}

[Test]
public void GridPreservesInnerCitySiteAcrossUpgrade()
{
    var source = new BuildingDefinition(
        "test.building.source", "测试源", 1, 1, ResourceIds.Iron, 0,
        placement: BuildingPlacement.Either,
        operation: BuildingOperation.MobileAllowed);
    var target = new BuildingDefinition(
        "test.building.target", "测试目标", 1, 1, ResourceIds.Iron, 0,
        placement: BuildingPlacement.Either,
        operation: BuildingOperation.MobileAllowed);
    var grid = new BuildingGrid(2, 2);
    var inventory = new ResourceInventory(10);
    grid.TryRestore(source, 0, 0, out var placed, BuildingSite.InnerCity);

    Assert.That(
        grid.TryUpgrade(placed, target, inventory, ResourceIds.Iron, 0, out var upgraded),
        Is.True);
    Assert.That(upgraded.Site, Is.EqualTo(BuildingSite.InnerCity));
}
```

Add a separate test where an `InnerCity` placed source upgrades to a `Ground`-only target; assert false and unchanged inventory.

- [x] **Step 2: Run focused tests and verify RED**

Run:

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.BuildingGridTests;WasteCity.Tests.BuildingUpgradeTests" \
  -testResults "/tmp/wastecity-building-mobility/red-grid.xml" \
  -logFile "/tmp/wastecity-building-mobility/red-grid.log"
```

Expected: compilation fails because `PlacedBuilding.Site` and site parameters do not exist.

- [x] **Step 3: Implement site-aware grid operations**

Reject unsupported sites before overlap checks or resource spending. Preserve the current resource-node, overlap, removal, and cell replacement behavior.

- [x] **Step 4: Run focused tests and verify GREEN**

Use the Step 2 command with `green-grid.xml`; require all selected tests passed.

### Task 3: Make runtime consumers obey the shared operation state

**Files:**
- Modify: `Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- Modify: `Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- Modify: `Assets/_Game/Scripts/Economy/TechnologyProductionController.cs`
- Modify: `Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs`
- Modify: `Assets/_Game/Scripts/World/FormalDroneController.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingRuntimeMobilityTests.cs`
- Create: `Assets/_Game/Tests/EditMode/BuildingRuntimeMobilityTests.cs.meta`

**Interfaces:**
- `BuildingRuntime.Site` returns the configured instance site.
- `BuildingRuntime.IsOperational` is true only when construction is complete, logistics is connected, and the mobility rule allows operation for the current city mode.
- `BuildingRuntime.Configure(...)` gains trailing `BuildingSite site = BuildingSite.Ground`.
- `PlaceholderBuildingController.OperationalCount` counts `runtime.IsOperational`.

- [x] **Step 1: Write failing runtime state tests**

Create real Unity components in EditMode and destroy them in `finally`:

```csharp
[Test]
public void CompletedGroundBuildingStopsWhenCityBecomesMobile()
{
    var cityObject = new GameObject("City");
    cityObject.AddComponent<Rigidbody2D>();
    var city = cityObject.AddComponent<PlaceholderMobileCity>();
    city.RestoreDeployment(CityMode.Fortress, 0f);
    var buildingObject = new GameObject("Building");
    buildingObject.AddComponent<HealthComponent>();
    var runtime = buildingObject.AddComponent<BuildingRuntime>();
    runtime.Configure(BuildingCatalog.Smelter, city: city, site: BuildingSite.Ground);
    runtime.RestoreState(BuildingCatalog.Smelter.MaximumHealth, 0f);

    Assert.That(runtime.IsOperational, Is.True);
    city.RestoreDeployment(CityMode.Mobile, 0f);
    Assert.That(runtime.IsOperational, Is.False);
}
```

Add a second test that a completed `Housing` configured at `InnerCity` remains operational in `Mobile`, but is false during `Deploying`.

- [x] **Step 2: Run the focused test and verify RED**

Expected: compilation fails because `Site`, `IsOperational`, and the configure parameter do not exist.

- [x] **Step 3: Implement runtime site and operation synchronization**

`BuildingRuntime.Update` must:

1. synchronize research-derived health;
2. synchronize capacity effects against the current `IsOperational`;
3. run regeneration only when operational;
4. advance incomplete construction only when `CanConstruct`;
5. advance repair only when operational.

`FinishConstruction`, `SetLogistics`, restore, upgrade, and destroy must keep `effectApplied` balanced exactly once.

`PlaceholderBuildingController` passes `PlacedBuilding.Site`, writes new placements/templates as `Ground`, and uses `IsOperational` in `OperationalCount`.

The production controller filters `BuildingRuntime.IsOperational`. Turrets, shield pulses, automated repair, scout-drone bays, puppet workshops, and behemoth pens use operational state/count instead of construction-plus-logistics or completed count.

- [x] **Step 4: Run runtime and affected focused suites**

Run the new fixture plus `CityOperationalTests`, `PassiveProductionTests`, `RouteCapstoneProductionTests`, `FriendlyUnitCommandTests`, and `TechnologyOverloadTests`. Require all passed.

### Task 4: Save schema 29 and player-facing tag display

**Files:**
- Modify: `Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Modify: `Assets/_Game/Scripts/Content/RouteContentDisplayCatalog.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/RouteContentDisplayTests.cs`

**Interfaces:**
- `BuildingSnapshot.site` stores `(int)BuildingSite`.
- `FormalSaveData.schema` defaults to `29`.
- `FormalSaveCodec.Decode` accepts schemas `1` through `29`.
- `RestoreSnapshots` validates `site` with `Enum.IsDefined`; invalid values restore as `BuildingSite.Ground`.

- [x] **Step 1: Write failing save and display tests**

```csharp
[Test]
public void SchemaTwentyNineBuildingSiteRoundTrips()
{
    var data = new FormalSaveData
    {
        buildings = new[]
        {
            new BuildingSnapshot
            {
                definitionId = BuildingCatalog.Housing.Id.Value,
                site = (int)BuildingSite.InnerCity
            }
        }
    };

    var restored = FormalSaveCodec.Decode(FormalSaveCodec.Encode(data));

    Assert.That(restored.schema, Is.EqualTo(29));
    Assert.That(restored.buildings[0].site, Is.EqualTo((int)BuildingSite.InnerCity));
}

[Test]
public void SchemaTwentyEightMissingBuildingSiteDefaultsToGround()
{
    var restored = FormalSaveCodec.Decode(
        "{\"schema\":28,\"buildings\":[{\"definitionId\":\"core.building.housing\"}]}");

    Assert.That(restored, Is.Not.Null);
    Assert.That(restored.buildings[0].site, Is.EqualTo((int)BuildingSite.Ground));
}
```

Extend every-building display coverage to require `位置：` plus the literal friendly placement name and `运行：` plus the literal friendly operation name.

- [x] **Step 2: Run focused tests and verify RED**

Run `FormalSaveTests` and `RouteContentDisplayTests`. Expected failures: schema remains 28, snapshot has no site, and building summary lacks tag lines.

- [x] **Step 3: Implement schema and display changes**

Capture `pair.Value.Site`; restore the validated snapshot site. Add to `BuildingSummary`:

```csharp
$"位置：{BuildingMobilityRules.PlacementName(definition.Placement)} · " +
$"运行：{BuildingMobilityRules.OperationName(definition.Operation)}\n"
```

- [x] **Step 4: Run focused tests and verify GREEN**

Require both fixtures passed with zero failures.

### Task 5: Full verification, evidence, and GitHub update

**Files:**
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`

**Interfaces:**
- Roadmap records this as the first implemented F1A foundation milestone, not as all of F1A completed.
- Feedback remains `开发中` and adds the feature commit, schema `29`, exact test counts, build evidence, and pending real-Windows smoke.

- [x] **Step 1: Run fresh full verification**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testResults "/tmp/wastecity-building-mobility/final-editmode.xml" \
  -logFile "/tmp/wastecity-building-mobility/final-editmode.log"

"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform PlayMode \
  -testResults "/tmp/wastecity-building-mobility/final-playmode.xml" \
  -logFile "/tmp/wastecity-building-mobility/final-playmode.log"

"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -logFile "/tmp/wastecity-building-mobility/final-compile.log" -quit

"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows \
  -logFile "/tmp/wastecity-building-mobility/final-windows-build.log" -quit
```

Read both XML roots, require zero failed tests, scan compile/build logs for C# errors or exceptions, require `Build Finished, Result: Success`, and verify:

```bash
file Builds/Windows/WasteCity.exe
```

Expected: `PE32+ executable (GUI) x86-64, for MS Windows`.

- [x] **Step 2: Review exact scope**

```bash
git diff --check
git status --short
git diff -- Assets/_Game/Scripts Assets/_Game/Tests Docs
```

Confirm no Package Manager or machine-specific files are staged and no build-menu visibility/filter code changed.

- [ ] **Step 3: Commit implementation**

Stage only implementation, tests, and this plan:

```bash
git commit -m "feat: enforce building mobility rules"
```

- [ ] **Step 4: Backwrite evidence**

Update the two controlled documents with the implementation hash and exact fresh verification totals. Keep `IDEA-0001` as `开发中`; state that inner-city placement UI, outposts, and real Windows smoke remain pending.

- [ ] **Step 5: Commit documentation and push**

```bash
git commit -m "docs: record building mobility milestone"
git push origin codex/fix-foundation
```
