# Route Content Display Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan.

**Goal:** Complete IDEA-0001 F1 roadmap item 7 by giving every current resource, building, and research node a consistent Chinese route/name/cost/function/unlock display, while preserving the existing placeholder art and gameplay rules.

**Architecture:** Add one pure C# display catalog under `WasteCity.Content` as the single presentation source for route names, resource names, building summaries, research summaries, proxy-resource wording, and friendly unlock requirements. Existing HUD/controllers consume this catalog; catalogs and gameplay models remain authoritative for actual values and unlock checks.

**Tech Stack:** Unity 2022.3.62f1, C# runtime assembly `WasteCity.Game`, NUnit EditMode tests, Unity Test Framework.

## Global Constraints

- Scope is stable requirement `IDEA-0001`, F1 roadmap item 7 only.
- Do not implement or change `BUG-0001` build-menu visibility/filtering behavior.
- Do not change production rates, research costs, unlock rules, placement rules, or save data.
- Use existing art placeholders; this milestone adds no art dependency.
- Preserve unrelated local Package Manager changes and stage only files named by this plan.

### Task 1: Lock the display contract with failing tests

**Files:**
- Create: `Assets/_Game/Tests/EditMode/RouteContentDisplayTests.cs`
- Create: `Assets/_Game/Tests/EditMode/RouteContentDisplayTests.cs.meta`

**Step 1: Write tests against the intended API**

Add `RouteContentDisplayTests` covering:

```csharp
RouteContentDisplayCatalog.ResourceName(resourceId);
RouteContentDisplayCatalog.ResourceRoute(resourceId);
RouteContentDisplayCatalog.InventorySummary(inventory);
RouteContentDisplayCatalog.BuildingSummary(definition);
RouteContentDisplayCatalog.ResearchListLine(definition, completed, blocked);
RouteContentDisplayCatalog.ResearchDetail(definition);
RouteContentDisplayCatalog.FriendlyUnlockReason(
    definition, population, researchCompleted, completedBuildingIds);
```

The tests must iterate all `ResourceIds.All`, `BuildingCatalog.BuildMenu`, and `ResearchCatalog.All`. Assertions require friendly Chinese route/resource text and reject raw stable IDs from final UI strings. Separate assertions lock the four current proxy descriptions:

- 发电站: `能晶` and `当前代理：能源币`
- 聚灵阵: `能晶` and `当前代理：灵石`
- 代谢炉: `生物质` to `能晶` and `当前代理：能源币`
- 意识网络: `灵能增幅器` and `当前代理：精神力结晶`

**Step 2: Run the focused test and observe RED**

Run:

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.RouteContentDisplayTests" \
  -testResults "$RESULT_DIR/route-content-red.xml" \
  -logFile "$RESULT_DIR/route-content-red.log"
```

Expected failure: `RouteContentDisplayCatalog` does not exist. A different compile failure must be corrected before proceeding.

### Task 2: Add the shared display catalog

**Files:**
- Create: `Assets/_Game/Scripts/Content/RouteContentDisplayCatalog.cs`
- Create: `Assets/_Game/Scripts/Content/RouteContentDisplayCatalog.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/RouteContentDisplayTests.cs`

**Step 1: Implement the minimal public display API**

Use explicit dictionaries and switches so missing catalog content fails visibly:

```csharp
public enum ContentRoute
{
    Core,
    Technology,
    Cultivation,
    BiologicalAscension,
    Psionics
}

public static class RouteContentDisplayCatalog
{
    public static string RouteName(ContentRoute route);
    public static string RouteName(DevelopmentRoute route);
    public static string ResourceName(string resourceId);
    public static ContentRoute ResourceRoute(string resourceId);
    public static ContentRoute BuildingRoute(BuildingDefinition definition);
    public static string InventorySummary(ResourceInventory inventory);
    public static string BuildingSummary(BuildingDefinition definition);
    public static string ResearchListLine(
        ResearchDefinition definition, bool completed, bool blocked);
    public static string ResearchDetail(ResearchDefinition definition);
    public static string FriendlyUnlockReason(
        BuildingDefinition definition,
        int population,
        Func<string, bool> researchCompleted,
        IEnumerable<string> completedBuildingIds);
}
```

`BuildingSummary` derives cost and unlock values from `BuildingDefinition`, derives research effect/cost from `ResearchDefinition`, and uses exact current gameplay rates from existing production/defense catalogs. Unknown IDs are displayed as `未知资源` or `未登记功能`, never as raw IDs.

**Step 2: Run the focused tests and reach GREEN**

Use the Task 1 command with `route-content-green.xml`. All `RouteContentDisplayTests` must pass.

### Task 3: Replace scattered UI naming with the shared catalog

**Files:**
- Modify: `Assets/_Game/Scripts/UI/FormalPlaceholderHud.cs`
- Modify: `Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- Modify: `Assets/_Game/Scripts/Research/ResearchController.cs`
- Modify: `Assets/_Game/Scripts/Economy/TechnologyProductionController.cs`
- Modify: `Assets/_Game/Tests/EditMode/RouteContentDisplayTests.cs`

**Step 1: Extend tests for controller-facing formatting**

Assert that inventory output is grouped as `基础 / 科技 / 修仙 / 生物飞升 / 灵能`, research list/detail output uses the same route and resource names, and locked buildings show friendly research/building names plus population numbers.

**Step 2: Run focused tests and observe RED for each new assertion**

The failure must identify the missing grouping or friendly text.

**Step 3: Integrate the formatter**

- `FormalPlaceholderHud` uses `InventorySummary`.
- `PlaceholderBuildingController` uses `BuildingSummary` and `FriendlyUnlockReason` for selected building information without filtering the menu.
- `ResearchController` removes private duplicate route/resource name switches and uses `ResearchListLine`/`ResearchDetail`.
- `TechnologyProductionController` uses shared route/resource names for its monitoring text while preserving runtime production state.

**Step 4: Run focused tests and reach GREEN**

All `RouteContentDisplayTests` pass after production changes.

### Task 4: Verify, backwrite, commit, and push

**Files:**
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify only if an evidence link is missing: `Docs/03-Roadmap-and-Backlog-ZH.md`

**Step 1: Run full automated verification**

Run fresh EditMode and PlayMode suites:

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testResults "$RESULT_DIR/editmode.xml" \
  -logFile "$RESULT_DIR/editmode.log"

"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform PlayMode \
  -testResults "$RESULT_DIR/playmode.xml" \
  -logFile "$RESULT_DIR/playmode.log"
```

Then run batch compilation and the repository's documented Windows build command. Record exact pass counts and generated build path. Do not claim a real Windows smoke test from macOS.

**Step 2: Inspect the diff**

Run:

```bash
git diff --check
git diff -- Assets/_Game/Scripts Assets/_Game/Tests/EditMode Docs
git status --short
```

Confirm no Package Manager files are staged.

**Step 3: Backwrite requirement status**

Update `IDEA-0001` to `已实现待验证` if automated tests/build pass but real Windows smoke is pending. Add implementation files, test evidence, and the remaining Windows verification gate. Do not change `BUG-0001`.

**Step 4: Commit and push**

Stage only the files in this plan, commit with:

```bash
git commit -m "feat: unify route content display"
git push origin codex/fix-foundation
```

If the requirement document needs the resulting implementation commit hash, add a second documentation commit and push it.
