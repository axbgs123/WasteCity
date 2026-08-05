# City Navigation and Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `IDEA-0001` F1A city autopilot, terrain traversal, deployment legality, direct-control switching, and schema 30 persistence without touching the build-menu bug.

**Architecture:** Keep terrain, pathfinding, deployment, and direct-control decisions in focused pure C# rules. `PlaceholderWorldView`, `PlaceholderMobileCity`, and `FormalLeaderController` remain thin Unity adapters for coordinates, input, motion, and placeholder visuals. Save only navigation intent and leader position, then rebuild paths on restore.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Input System `1.7.0`, JSON save schema `30`, Windows Mono x86-64 player build.

## Global Constraints

- Stable requirement: `IDEA-0001`.
- Follow `Docs/superpowers/specs/2026-08-05-city-navigation-deployment-design.md`.
- Do not change `BUG-0001` build-menu opening, visibility, filtering, selection, or placement behavior.
- Preserve the numeric values and generation distribution of the existing `TerrainKind`.
- Existing resource nodes must remain traversal-open.
- Use programmatic 2D placeholders and stable `VisualSlot` IDs; do not import art.
- Do not add NavMesh, Cinemachine, packages, or paid dependencies.
- Schema becomes `30`; schema `29` and older disable autopilot and attach the leader to the city safely.
- Preserve unrelated Package Manager changes and stage only files named by this plan.

---

### Task 1: World traversal and terrain rules

**Files:**
- Modify: `Assets/_Game/Scripts/World/WorldMapModel.cs`
- Modify: `Assets/_Game/Scripts/World/PlaceholderWorldView.cs`
- Create: `Assets/_Game/Scripts/City/CityTerrainRules.cs`
- Create: `Assets/_Game/Scripts/City/CityTerrainRules.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/CityTerrainRulesTests.cs`
- Create: `Assets/_Game/Tests/EditMode/CityTerrainRulesTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/WorldMapTests.cs`

**Interfaces:**
- Extends `WorldCell` with immutable `WorldTraversalKind Traversal`.
- Adds a trailing optional constructor parameter so existing calls remain source-compatible:

```csharp
public WorldCell(
    TerrainKind terrain,
    string resourceId,
    int amount,
    WorldTraversalKind traversal = WorldTraversalKind.Open);
```

- Adds:

```csharp
public enum WorldTraversalKind
{
    Open = 0,
    Ruins = 1,
    DeepWater = 2,
    Cliff = 3
}

public static class CityTerrainRules
{
    public static bool IsPassable(WorldCell cell);
    public static float SpeedMultiplier(WorldCell cell);
    public static bool SupportsDeployment(WorldCell cell);
    public static string TraversalName(WorldTraversalKind traversal);
}
```

- Adds a defensive fixture constructor for real rule tests:

```csharp
public WorldMapModel(WorldCell[,] source);
```

- Adds world-coordinate methods:

```csharp
public bool TryWorldToCell(Vector2 world, out int x, out int y);
public Vector2 CellToWorld(int x, int y);
public bool IsPassableWorld(Vector2 world);
```

- [x] **Step 1: Write failing traversal tests**

Before writing each body, name the break:

- wrong passability branch lets a city enter deep water;
- wrong multiplier makes wetland as fast as wasteland;
- resource overlay creates an unreachable required node;
- coordinate conversion shifts existing map cells by half a tile.

Add literal assertions:

```csharp
[TestCase(WorldTraversalKind.DeepWater)]
[TestCase(WorldTraversalKind.Cliff)]
public void DeepWaterAndCliffBlockCity(WorldTraversalKind traversal)
{
    var cell = new WorldCell(
        TerrainKind.Wasteland,
        null,
        0,
        traversal);

    Assert.That(CityTerrainRules.IsPassable(cell), Is.False);
    Assert.That(CityTerrainRules.SpeedMultiplier(cell), Is.Zero);
}

[Test]
public void WetlandAndRuinsUseApprovedSlowMultipliers()
{
    var wetland = new WorldCell(TerrainKind.Wetland, null, 0);
    var ruins = new WorldCell(
        TerrainKind.Wasteland,
        null,
        0,
        WorldTraversalKind.Ruins);

    Assert.That(CityTerrainRules.SpeedMultiplier(wetland), Is.EqualTo(.55f));
    Assert.That(CityTerrainRules.SpeedMultiplier(ruins), Is.EqualTo(.65f));
    Assert.That(CityTerrainRules.SupportsDeployment(wetland), Is.False);
    Assert.That(CityTerrainRules.SupportsDeployment(ruins), Is.False);
}

[Test]
public void GeneratedResourceNodesAlwaysRemainOpen()
{
    var map = new WorldMapModel(32, 24, new WorldSeed(8128));

    for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            if (map.Get(x, y).HasResource)
                Assert.That(
                    map.Get(x, y).Traversal,
                    Is.EqualTo(WorldTraversalKind.Open),
                    $"resource cell {x},{y}");
}
```

Add a `PlaceholderWorldView` coordinate test using a generated `32×24` view:

```csharp
Assert.That(view.TryWorldToCell(new Vector2(-8f, -5f), out int x, out int y), Is.True);
Assert.That(x, Is.EqualTo(8));
Assert.That(y, Is.EqualTo(7));
Assert.That(view.CellToWorld(8, 7), Is.EqualTo(new Vector2(-8f, -5f)));
```

- [x] **Step 2: Run focused tests and verify RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.CityTerrainRulesTests;WasteCity.Tests.WorldMapTests" \
  -testResults "/tmp/wastecity-city-navigation/red-terrain.xml" \
  -logFile "/tmp/wastecity-city-navigation/red-terrain.log"
```

Expected: compilation fails because traversal types and coordinate methods do not exist.

- [x] **Step 3: Implement traversal and obstacle placeholders**

Use channel `3` without changing the existing terrain roll:

```csharp
string resource = RollResource(seed.Sample(x, y, 1) % 100, terrain);
WorldTraversalKind traversal = WorldTraversalKind.Open;
if (resource == null)
{
    int traversalRoll = seed.Sample(x, y, 3) % 100;
    traversal = traversalRoll < 4
        ? WorldTraversalKind.Cliff
        : traversalRoll < 8
            ? WorldTraversalKind.DeepWater
            : traversalRoll < 18
                ? WorldTraversalKind.Ruins
                : WorldTraversalKind.Open;
}
```

Implement `CityTerrainRules` exactly:

```csharp
public static bool IsPassable(WorldCell cell)
{
    return cell.Traversal != WorldTraversalKind.DeepWater &&
           cell.Traversal != WorldTraversalKind.Cliff;
}

public static float SpeedMultiplier(WorldCell cell)
{
    if (!IsPassable(cell)) return 0f;
    if (cell.Traversal == WorldTraversalKind.Ruins) return .65f;
    if (cell.Terrain == TerrainKind.Wetland) return .55f;
    if (cell.Terrain == TerrainKind.Rocky) return .8f;
    return 1f;
}

public static bool SupportsDeployment(WorldCell cell)
{
    return IsPassable(cell) &&
           cell.Traversal != WorldTraversalKind.Ruins &&
           cell.Terrain != TerrainKind.Wetland;
}
```

When `PlaceholderWorldView.Generate` sees non-open traversal, create a child placeholder with these stable IDs:

```text
world.obstacle.ruins
world.obstacle.deep-water
world.obstacle.cliff
```

Attach `VisualSlot` to the child renderer. Keep rules independent of renderer color and slot ID.

- [x] **Step 4: Run focused tests and verify GREEN**

Use the Step 2 command with `green-terrain.xml`. Require all selected tests to pass.

- [x] **Step 5: Commit traversal foundation**

```bash
git add \
  Assets/_Game/Scripts/World/WorldMapModel.cs \
  Assets/_Game/Scripts/World/PlaceholderWorldView.cs \
  Assets/_Game/Scripts/City/CityTerrainRules.cs \
  Assets/_Game/Scripts/City/CityTerrainRules.cs.meta \
  Assets/_Game/Tests/EditMode/CityTerrainRulesTests.cs \
  Assets/_Game/Tests/EditMode/CityTerrainRulesTests.cs.meta \
  Assets/_Game/Tests/EditMode/WorldMapTests.cs
git commit -m "feat: add city terrain traversal rules"
```

### Task 2: Weighted grid pathfinding

**Files:**
- Create: `Assets/_Game/Scripts/City/CityPathfinder.cs`
- Create: `Assets/_Game/Scripts/City/CityPathfinder.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/CityPathfinderTests.cs`
- Create: `Assets/_Game/Tests/EditMode/CityPathfinderTests.cs.meta`

**Interfaces:**
- Consumes: `WorldMapModel`, `CityTerrainRules`.
- Produces:

```csharp
public readonly struct WorldGridPoint
{
    public int X { get; }
    public int Y { get; }
    public WorldGridPoint(int x, int y);
}

public static class CityPathfinder
{
    public static bool TryFindPath(
        WorldMapModel map,
        int startX,
        int startY,
        int destinationX,
        int destinationY,
        out WorldGridPoint[] path);
}
```

- [x] **Step 1: Write failing path tests**

Breaks named by the tests:

- removing blocked-cell filtering crosses a cliff;
- returning the explored order rather than parent chain gives the wrong path;
- ignoring terrain costs selects two wetland cells over an equal-length open route;
- treating invalid endpoints as valid produces a route outside the world.

Use hand-authored literal fixtures:

```csharp
[Test]
public void PathDetoursAroundCliffAndIncludesDestination()
{
    var open = new WorldCell(TerrainKind.Wasteland, null, 0);
    var cliff = new WorldCell(
        TerrainKind.Wasteland,
        null,
        0,
        WorldTraversalKind.Cliff);
    var cells = new[,]
    {
        { open, open, open },
        { open, cliff, open },
        { open, open, open }
    };

    Assert.That(
        CityPathfinder.TryFindPath(
            new WorldMapModel(cells),
            0,
            1,
            2,
            1,
            out WorldGridPoint[] path),
        Is.True);
    Assert.That(path.Length, Is.EqualTo(4));
    Assert.That(path[path.Length - 1].X, Is.EqualTo(2));
    Assert.That(path[path.Length - 1].Y, Is.EqualTo(1));
    Assert.That(
        System.Array.Exists(path, point => point.X == 1 && point.Y == 1),
        Is.False);
}

[Test]
public void UnreachableDestinationReturnsFalseAndEmptyPath()
{
    var open = new WorldCell(TerrainKind.Wasteland, null, 0);
    var cliff = new WorldCell(
        TerrainKind.Wasteland,
        null,
        0,
        WorldTraversalKind.Cliff);
    var cells = new[,]
    {
        { open, cliff, open },
        { open, cliff, open },
        { open, cliff, open }
    };

    Assert.That(
        CityPathfinder.TryFindPath(
            new WorldMapModel(cells),
            0,
            0,
            2,
            0,
            out WorldGridPoint[] path),
        Is.False);
    Assert.That(path, Is.Empty);
}
```

Add a literal fast-route fixture where both alternatives have the same step count but one crosses wetland; assert the returned points use the open row.

- [x] **Step 2: Run focused test and verify RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.CityPathfinderTests" \
  -testResults "/tmp/wastecity-city-navigation/red-path.xml" \
  -logFile "/tmp/wastecity-city-navigation/red-path.log"
```

Expected: compilation fails because `CityPathfinder` and `WorldGridPoint` do not exist.

- [x] **Step 3: Implement minimal A-star**

Use four literal neighbor offsets. Store best cost, parent, and closed state in arrays sized to the map. Select the open node with minimum:

```csharp
float score = bestCost[x, y] +
              Math.Abs(destinationX - x) +
              Math.Abs(destinationY - y);
```

The tentative cost is:

```csharp
bestCost[currentX, currentY] +
1f / CityTerrainRules.SpeedMultiplier(map.Get(nextX, nextY));
```

On success, follow parents from destination to start, reverse the list, and omit the start node. Reject null maps, out-of-range endpoints, blocked endpoints, and exhausted searches.

- [x] **Step 4: Run focused test and verify GREEN**

Use the Step 2 command with `green-path.xml`; require zero failures.

- [x] **Step 5: Commit pathfinding**

```bash
git add \
  Assets/_Game/Scripts/City/CityPathfinder.cs \
  Assets/_Game/Scripts/City/CityPathfinder.cs.meta \
  Assets/_Game/Tests/EditMode/CityPathfinderTests.cs \
  Assets/_Game/Tests/EditMode/CityPathfinderTests.cs.meta
git commit -m "feat: add weighted city pathfinding"
```

### Task 3: Deployment legality and city autopilot adapter

**Files:**
- Create: `Assets/_Game/Scripts/City/CityDeploymentRules.cs`
- Create: `Assets/_Game/Scripts/City/CityDeploymentRules.cs.meta`
- Modify: `Assets/_Game/Scripts/City/PlaceholderMobileCity.cs`
- Create: `Assets/_Game/Tests/EditMode/CityDeploymentRulesTests.cs`
- Create: `Assets/_Game/Tests/EditMode/CityDeploymentRulesTests.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/PlaceholderMobileCityTests.cs`
- Create: `Assets/_Game/Tests/EditMode/PlaceholderMobileCityTests.cs.meta`

**Interfaces:**
- Consumes: `WorldMapModel`, `PlaceholderWorldView`, `CityPathfinder`, existing `CityDeploymentModel`.
- Produces the deployment types and `PlaceholderMobileCity` public API defined in the design spec.

- [x] **Step 1: Write failing deployment rule tests**

Use explicit `3×3` fixtures and literal expected failures:

```csharp
[Test]
public void ThreeByThreeDeploymentRejectsBlockedCell()
{
    var open = new WorldCell(TerrainKind.Wasteland, null, 0);
    var cliff = new WorldCell(
        TerrainKind.Wasteland,
        null,
        0,
        WorldTraversalKind.Cliff);
    var cells = new[,]
    {
        { open, open, open },
        { open, open, open },
        { open, cliff, open }
    };

    Assert.That(
        CityDeploymentRules.Validate(new WorldMapModel(cells), 1, 1),
        Is.EqualTo(CityDeploymentFailure.Blocked));
}

[Test]
public void ThreeByThreeDeploymentAllowsRockyResourceGround()
{
    var cells = new WorldCell[3, 3];
    for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
            cells[x, y] = new WorldCell(
                TerrainKind.Rocky,
                x == 1 && y == 1 ? ResourceIds.Iron : null,
                x == 1 && y == 1 ? 100 : 0);

    Assert.That(
        CityDeploymentRules.Validate(new WorldMapModel(cells), 1, 1),
        Is.EqualTo(CityDeploymentFailure.None));
}
```

Also assert:

- center at map edge returns `OutsideWorld`;
- one wetland or ruins cell returns `UnstableGround`;
- `FailureReason` returns non-empty Chinese text for every failure and empty text for `None`.

- [x] **Step 2: Run deployment tests and verify RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.CityDeploymentRulesTests" \
  -testResults "/tmp/wastecity-city-navigation/red-deployment.xml" \
  -logFile "/tmp/wastecity-city-navigation/red-deployment.log"
```

Expected: compilation fails because deployment validation types do not exist.

- [x] **Step 3: Implement deployment rules**

Loop the full rectangle from `centerX - radiusX` through `centerX + radiusX` and the equivalent Y range. Use this precedence:

1. any outside cell → `OutsideWorld`;
2. any non-passable cell → `Blocked`;
3. any passable but unsupported cell → `UnstableGround`;
4. otherwise → `None`.

Evaluate the complete area so an earlier unstable cell cannot hide a later blocked cell.

- [x] **Step 4: Run deployment tests and verify GREEN**

Use Step 2 with `green-deployment.xml`; require zero failures.

- [x] **Step 5: Write failing city adapter tests**

Use real GameObjects, `Rigidbody2D`, `PlaceholderWorldView`, and `PlaceholderMobileCity`, destroyed in `finally`. Do not add test-only hooks to production.

Required observable tests:

```csharp
[Test]
public void ReachableDestinationStartsAndManualInputCancelsAutopilot()
{
    // Create a generated world and a real city at a passable cell.
    Assert.That(city.TrySetDestinationCell(targetX, targetY, out _), Is.True);
    Assert.That(city.AutopilotActive, Is.True);

    city.ApplyManualInput(Vector2.right);

    Assert.That(city.AutopilotActive, Is.False);
}

[Test]
public void InvalidDeploymentKeepsMobileModeAndReportsReason()
{
    // Configure a custom 3×3 world with one cliff.
    Assert.That(city.TryToggleDeployment(out string reason), Is.False);
    Assert.That(city.Deployment.Mode, Is.EqualTo(CityMode.Mobile));
    Assert.That(reason, Does.Contain("展开失败"));
}
```

`ApplyManualInput(Vector2)` is a production method because runtime input and future remapping both need one normalized entry point:

```csharp
public void ApplyManualInput(Vector2 value);
```

Add tests for:

- destination rejected outside the world;
- destination rejected outside `Mobile`;
- successful deployment cancels autopilot and enters `Deploying`;
- `RestoreNavigation` safely disables an unreachable saved destination.

- [x] **Step 6: Run city adapter tests and verify RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.PlaceholderMobileCityTests" \
  -testResults "/tmp/wastecity-city-navigation/red-city-adapter.xml" \
  -logFile "/tmp/wastecity-city-navigation/red-city-adapter.log"
```

Expected: compilation fails because the city navigation API does not exist.

- [x] **Step 7: Implement city input and motion**

Keep `Update` responsible for:

1. ticking deployment;
2. translating `F` into `TryToggleDeployment`;
3. translating right mouse into `TrySetDestination`;
4. translating WASD into `ApplyManualInput`.

Keep `FixedUpdate` responsible for one motion step:

```csharp
Vector2 direction = manualInput.sqrMagnitude > 0f
    ? manualInput.normalized
    : DirectionToNextWaypoint();
Vector2 candidate = body.position +
                    direction *
                    moveSpeed *
                    CurrentTerrainMultiplier *
                    Time.fixedDeltaTime;
if (world == null || world.IsPassableWorld(candidate))
    body.MovePosition(candidate);
```

When a waypoint is within `0.08f`, remove it. At the final waypoint, clear autopilot and set `LastMobilityMessage` to “自动驾驶：已到达”.

If `world` or `world.Model` is absent, preserve legacy direct WASD and deployment behavior for isolated existing tests, but reject automatic destinations with “自动驾驶不可用：世界尚未生成”.

- [x] **Step 8: Run city and affected tests**

Run:

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.PlaceholderMobileCityTests;WasteCity.Tests.CityDeploymentRulesTests;WasteCity.Tests.DeploymentAndHarvestTests;WasteCity.Tests.CityOperationalTests;WasteCity.Tests.BuildingRuntimeMobilityTests" \
  -testResults "/tmp/wastecity-city-navigation/green-city-adapter.xml" \
  -logFile "/tmp/wastecity-city-navigation/green-city-adapter.log"
```

Require every selected test passed.

- [x] **Step 9: Commit city navigation adapter**

```bash
git add \
  Assets/_Game/Scripts/City/CityDeploymentRules.cs \
  Assets/_Game/Scripts/City/CityDeploymentRules.cs.meta \
  Assets/_Game/Scripts/City/PlaceholderMobileCity.cs \
  Assets/_Game/Tests/EditMode/CityDeploymentRulesTests.cs \
  Assets/_Game/Tests/EditMode/CityDeploymentRulesTests.cs.meta \
  Assets/_Game/Tests/EditMode/PlaceholderMobileCityTests.cs \
  Assets/_Game/Tests/EditMode/PlaceholderMobileCityTests.cs.meta
git commit -m "feat: enforce city navigation and deployment"
```

### Task 4: Direct-control switching and leader movement

**Files:**
- Create: `Assets/_Game/Scripts/City/DirectControlRules.cs`
- Create: `Assets/_Game/Scripts/City/DirectControlRules.cs.meta`
- Modify: `Assets/_Game/Scripts/Leader/FormalLeaderController.cs`
- Create: `Assets/_Game/Tests/EditMode/DirectControlRulesTests.cs`
- Create: `Assets/_Game/Tests/EditMode/DirectControlRulesTests.cs.meta`
- Modify: `Assets/_Game/Tests/EditMode/LeaderTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum DirectControlTarget
{
    City = 0,
    Leader = 1
}

public static class DirectControlRules
{
    public static DirectControlTarget Resolve(
        CityMode mode,
        bool leaderRecruited);
}
```

- Extends `FormalLeaderController`:

```csharp
public DirectControlTarget ControlTarget { get; }
public Vector2 Position { get; }
public void ConfigureWorld(PlaceholderWorldView world);
public void ApplyManualInput(Vector2 value);
public void TickDirectControl(float delta);
public void RestorePosition(float x, float y, bool hasSavedPosition);
```

- [x] **Step 1: Write failing control rule tests**

Use literal expectations for every state:

```csharp
[TestCase(CityMode.Mobile, true, DirectControlTarget.City)]
[TestCase(CityMode.Deploying, true, DirectControlTarget.City)]
[TestCase(CityMode.Fortress, true, DirectControlTarget.Leader)]
[TestCase(CityMode.Packing, true, DirectControlTarget.City)]
[TestCase(CityMode.Fortress, false, DirectControlTarget.City)]
public void ControlTargetMatchesApprovedState(
    CityMode mode,
    bool recruited,
    DirectControlTarget expected)
{
    Assert.That(
        DirectControlRules.Resolve(mode, recruited),
        Is.EqualTo(expected));
}
```

- [x] **Step 2: Run and verify RED**

Run the `DirectControlRulesTests` fixture. Expected: missing type compilation failure.

- [x] **Step 3: Implement direct-control rules**

Return `Leader` only for `Fortress && leaderRecruited`; return `City` otherwise.

- [x] **Step 4: Run and verify GREEN**

Require the focused fixture passes.

- [x] **Step 5: Write failing leader adapter tests**

Use real `PlaceholderMobileCity`, real leader visual, and real `FormalLeaderController`. `ApplyManualInput` stores the desired direction and `TickDirectControl` advances it with an explicit delta, so the same production path is deterministic in tests. Assert:

- recruited + fortress accepts `ApplyManualInput(Vector2.right)` and `TickDirectControl(.5f)` changes the visual position;
- mobile mode reattaches the visual to `city + (1.8, 1.2)`;
- a candidate deep-water cell is rejected;
- `RestorePosition(..., true)` restores exact coordinates;
- `RestorePosition(..., false)` uses the city attachment point.

Use reflection only to invoke existing Unity lifecycle methods when an EditMode test requires them; do not add production lifecycle hooks used only by tests.

- [x] **Step 6: Run leader adapter tests and verify RED**

Run `LeaderTests`; expected failures are missing control API or unchanged leader position.

- [x] **Step 7: Implement leader movement**

Add serialized `moveSpeed = 5f` and `PlaceholderWorldView world`. In `Update`:

- retain overload, aura, and visual-slot behavior;
- when `ControlTarget == Leader`, read WASD into `ApplyManualInput` and call `TickDirectControl(Time.deltaTime)`;
- reject a candidate that `world.IsPassableWorld` rejects;
- otherwise attach the visual to `city + (1.8, 1.2, -0.2)`.

Keep leader gameplay model, recruitment, damage, skills, and IDs unchanged.

- [x] **Step 8: Run leader and control suites**

Require `DirectControlRulesTests` and `LeaderTests` pass.

- [x] **Step 9: Commit direct control**

```bash
git add \
  Assets/_Game/Scripts/City/DirectControlRules.cs \
  Assets/_Game/Scripts/City/DirectControlRules.cs.meta \
  Assets/_Game/Scripts/Leader/FormalLeaderController.cs \
  Assets/_Game/Tests/EditMode/DirectControlRulesTests.cs \
  Assets/_Game/Tests/EditMode/DirectControlRulesTests.cs.meta \
  Assets/_Game/Tests/EditMode/LeaderTests.cs
git commit -m "feat: switch direct control by city mode"
```

### Task 5: Schema 30, scene wiring, HUD, and runtime contracts

**Files:**
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- Modify: `Assets/_Game/Scripts/UI/FormalPlaceholderHud.cs`
- Modify: `Assets/_Game/Scripts/UI/FormalTitleMenuController.cs`
- Modify: `Assets/_Game/Scenes/FormalPrototype.unity`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/SceneContractTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- `FormalSaveData.schema` defaults to `30`; codec accepts `1..30`.
- Adds the five fields from the design spec.
- Scene assigns `PlaceholderWorldView` to both city and leader.
- `FormalPlaceholderHud` adds a serialized `FormalLeaderController leader` reference and reads city/leader public state only.

- [x] **Step 1: Write failing schema tests**

```csharp
[Test]
public void SchemaThirtyNavigationAndLeaderPositionRoundTrip()
{
    var restored = FormalSaveCodec.Decode(
        FormalSaveCodec.Encode(
            new FormalSaveData
            {
                cityAutopilotActive = true,
                cityDestinationX = 17,
                cityDestinationY = 9,
                leaderPositionSaved = true,
                leaderX = 4.5f,
                leaderY = -2.25f
            }));

    Assert.That(restored.schema, Is.EqualTo(30));
    Assert.That(restored.cityAutopilotActive, Is.True);
    Assert.That(restored.cityDestinationX, Is.EqualTo(17));
    Assert.That(restored.cityDestinationY, Is.EqualTo(9));
    Assert.That(restored.leaderPositionSaved, Is.True);
    Assert.That(restored.leaderX, Is.EqualTo(4.5f));
    Assert.That(restored.leaderY, Is.EqualTo(-2.25f));
}

[Test]
public void SchemaTwentyNineDefaultsNavigationAndLeaderPositionSafely()
{
    var restored = FormalSaveCodec.Decode(
        "{\"schema\":29,\"cityX\":1,\"cityY\":2}");

    Assert.That(restored, Is.Not.Null);
    Assert.That(restored.cityAutopilotActive, Is.False);
    Assert.That(restored.cityDestinationX, Is.EqualTo(-1));
    Assert.That(restored.cityDestinationY, Is.EqualTo(-1));
    Assert.That(restored.leaderPositionSaved, Is.False);
}
```

Because Unity JSON leaves missing integers at field initializers, declare destination fields with `-1` initializers in `FormalSaveData`.

- [x] **Step 2: Run schema tests and verify RED**

Run `FormalSaveTests`; expected missing fields/current schema assertions.

- [x] **Step 3: Implement schema and controller capture/restore**

Capture:

```csharp
cityAutopilotActive = city.AutopilotActive,
cityDestinationX = city.DestinationX,
cityDestinationY = city.DestinationY,
leaderPositionSaved = leader.Model.Recruited,
leaderX = leader.Position.x,
leaderY = leader.Position.y
```

Apply only after city position, deployment, world state, and leader recruitment have been restored:

```csharp
city.RestoreNavigation(
    d.schema >= 30 && d.cityAutopilotActive,
    d.cityDestinationX,
    d.cityDestinationY);
leader.RestorePosition(
    d.leaderX,
    d.leaderY,
    d.schema >= 30 && d.leaderPositionSaved);
```

Keep all schema `29` building-site behavior intact.

- [x] **Step 4: Run schema tests and verify GREEN**

Require `FormalSaveTests` passes.

- [x] **Step 5: Write failing scene and PlayMode contracts**

Add assertions that the loaded formal scene:

- has `city.NavigationReady == true`;
- can find at least one obstacle placeholder with a `VisualSlot` whose stable ID begins with `world.obstacle.`;
- resolves the initial control target to `City`;
- can select a reachable destination at least two cells away and moves closer after physics frames;
- rejects a deliberately invalid deployment fixture without changing mode;
- enters `Deploying` at a legal `3×3` fixture.

For route movement, unpause the session reason used by the title menu and select the first destination for which `TrySetDestinationCell` returns true; assert distance to `CellToWorld` decreases, not an exact frame position.

- [x] **Step 6: Run PlayMode contract and verify RED**

Run the new named `RuntimeSceneTests` methods. Expected: navigation is not wired and obstacle slots do not exist.

- [x] **Step 7: Wire scene and update HUD**

In `FormalPrototype.unity` add:

```yaml
world: {fileID: 458706983}
```

to the serialized `PlaceholderMobileCity` and `FormalLeaderController` components.

Add the existing leader controller to the HUD component:

```yaml
leader: {fileID: 443342350}
```

Update HUD text with:

```text
直接控制：移动城市 / 领袖
地形速度：100%
自动驾驶：目标 (x,y) / 最近移动提示
WASD 直接控制 | 右键自动驾驶 | F 展开/收起
```

Do not edit `PlaceholderBuildingController`.

Update the help page's movement clause from “WASD 驾驶移动城市” to “WASD 控制当前对象｜右键自动驾驶城市”; leave every build-menu clause unchanged.

- [x] **Step 8: Run focused save, scene, and PlayMode suites**

Require `FormalSaveTests`, `SceneContractTests`, and the new PlayMode methods all pass.

- [x] **Step 9: Commit persistence and scene integration**

```bash
git add \
  Assets/_Game/Scripts/Persistence/FormalSaveData.cs \
  Assets/_Game/Scripts/Persistence/FormalSaveController.cs \
  Assets/_Game/Scripts/UI/FormalPlaceholderHud.cs \
  Assets/_Game/Scripts/UI/FormalTitleMenuController.cs \
  Assets/_Game/Scenes/FormalPrototype.unity \
  Assets/_Game/Tests/EditMode/FormalSaveTests.cs \
  Assets/_Game/Tests/EditMode/SceneContractTests.cs \
  Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: persist city navigation state"
```

### Task 6: Full verification, controlled documentation, and GitHub update

**Files:**
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify: `Docs/superpowers/plans/2026-08-05-city-navigation-deployment.md`

**Interfaces:**
- Roadmap marks only city navigation/direct-control/deployment foundation as implemented.
- `IDEA-0001` remains `开发中`.
- Records exact implementation commits, schema `30`, test totals, build result, and pending real-Windows smoke.

- [x] **Step 1: Run fresh full verification**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testResults "/tmp/wastecity-city-navigation/final-editmode.xml" \
  -logFile "/tmp/wastecity-city-navigation/final-editmode.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform PlayMode \
  -testResults "/tmp/wastecity-city-navigation/final-playmode.xml" \
  -logFile "/tmp/wastecity-city-navigation/final-playmode.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -logFile "/tmp/wastecity-city-navigation/final-compile.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows \
  -logFile "/tmp/wastecity-city-navigation/final-windows-build.log"
```

Read both XML roots and require zero failures. Scan compile/build logs for C# errors or unhandled exceptions. Require:

```text
Build Finished, Result: Success
```

Verify:

```bash
file Builds/Windows/WasteCity.exe
```

Expected: `PE32+ executable (GUI) x86-64, for MS Windows`.

- [x] **Step 2: Review exact scope**

```bash
git diff --check
git status --short
git diff -- Assets/_Game/Scripts Assets/_Game/Tests Assets/_Game/Scenes Docs
```

Confirm no Package Manager or machine-specific file is staged and `PlaceholderBuildingController` has no diff.

- [x] **Step 3: Backwrite controlled evidence**

Update:

- roadmap code baseline, schema, exact test counts, Windows build result, and the F1A navigation bullet;
- `IDEA-0001` associated commits, stage progress, evidence, exclusions, and pending real Windows smoke;
- this plan's completed checkboxes.

Do not change `BUG-0001`.

- [x] **Step 4: Commit documentation**

```bash
git add \
  Docs/05-Formal-Development-Roadmap-ZH.md \
  Docs/06-User-Feedback-and-Change-Control-ZH.md \
  Docs/superpowers/plans/2026-08-05-city-navigation-deployment.md
git commit -m "docs: record city navigation milestone"
```

- [x] **Step 5: Push the current branch**

```bash
git push origin codex/fix-foundation
git fetch origin codex/fix-foundation
test "$(git rev-parse HEAD)" = "$(git rev-parse origin/codex/fix-foundation)"
```
