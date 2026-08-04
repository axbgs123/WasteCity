# Technology Overload and Drones Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the documented technology-route overload and make unmanned systems deploy scouting and repair placeholders through existing technology buildings.

**Architecture:** Keep all time, multiplier, composition, deployment, and patrol rules in small pure C# models. Add separate runtime controllers for technology overload and scout drones, then adapt existing turret, repair-bay, formal-scene, and save entry points. Reuse the current world fog, building logistics, leader overload, research, and `VisualSlot` systems.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Input System `1.7.0`, URP 2D programmatic placeholders, Windows Mono player build.

## Global Constraints

- Follow `Docs/01-Game-Design-Document-ZH.md`, `Docs/05-Formal-Development-Roadmap-ZH.md`, and `Docs/superpowers/specs/2026-08-04-technology-overload-drones-design.md`.
- Preserve Cen Jin overload values: healthy `1.75x`, injured `1.35x`, boost `5s`, lockout `3s`, cooldown `30s`.
- Technology overload is unlocked by `core.research.energy-weapons`: fire rate `2x`, energy damage `1.3x`, boost `5s`, lockout `3s`, cooldown `30s`.
- Technology and leader fire-rate boosts use the maximum rather than multiplication; either lockout produces final fire rate `0`.
- Scout drones require completed `core.research.unmanned-systems` and completed, logistics-connected automated repair bays.
- Scout reveal radius is `2` and reveal cadence is once per second.
- Use stable IDs `technology.status.overload`, `technology.unit.scout-drone`, and `technology.unit.repair-mech`.
- Save schema becomes `26`; schema `25` and older default technology overload to ready.
- Do not add production recipes, resource costs, health, weapons, maintenance, manual drone commands, transport, formal art, audio, or a new research node.
- Do not commit local package paths, `Library/`, `Logs/`, `Builds/`, `TestResults/`, license files, or credentials.
- Real Windows executable smoke remains pending until a Windows 10/11 machine is available.

---

### Task 1: Pure Technology Overload and Modifier Composition

**Files:**
- Create: `Assets/_Game/Scripts/Combat/TechnologyOverloadModel.cs`
- Create: `Assets/_Game/Scripts/Combat/TechnologyOverloadModel.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/TechnologyOverloadTests.cs`
- Create: `Assets/_Game/Tests/EditMode/TechnologyOverloadTests.cs.meta`

**Interfaces:**
- Produces: `TechnologyOverloadPhase`, `TechnologyOverloadModel.TryActivate(bool)`, `Tick(float)`, `Restore(bool,float,float,float)`, `FireRateMultiplier`, `DamageMultiplier(DamageType)`.
- Produces: `TurretCombatModifierRules.ResolveFireRate(float, float)` and `ResolveDamage(DamageType,float)`.

- [ ] **Step 1: Write failing overload state tests**

Add tests proving:

```csharp
[Test]
public void LockedTechnologyOverloadCannotActivate()
{
    var model = new TechnologyOverloadModel();
    Assert.That(model.TryActivate(false), Is.False);
    Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Ready));
}

[Test]
public void TechnologyOverloadBoostsThenLocksAndCoolsDown()
{
    var model = new TechnologyOverloadModel();
    Assert.That(model.TryActivate(true), Is.True);
    Assert.That(model.FireRateMultiplier, Is.EqualTo(2f));
    Assert.That(model.DamageMultiplier(DamageType.Energy), Is.EqualTo(1.3f));
    Assert.That(model.DamageMultiplier(DamageType.Physical), Is.EqualTo(1f));
    model.Tick(5f);
    Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Lockout));
    Assert.That(model.FireRateMultiplier, Is.Zero);
    model.Tick(3f);
    Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Cooldown));
    model.Tick(22f);
    Assert.That(model.Phase, Is.EqualTo(TechnologyOverloadPhase.Ready));
}
```

Also test negative deltas, a `30f` single tick, locked restore clearing state, unlocked restore preserving state, and rejected repeat activation.

- [ ] **Step 2: Run the focused tests to verify RED**

Run EditMode with filter `WasteCity.Tests.TechnologyOverloadTests`.

Expected: compilation fails because `TechnologyOverloadModel` does not exist.

- [ ] **Step 3: Implement the state model**

Implement:

```csharp
public enum TechnologyOverloadPhase { Ready, Boosting, Lockout, Cooldown }

public sealed class TechnologyOverloadModel
{
    public float CooldownRemaining { get; private set; }
    public float BoostRemaining { get; private set; }
    public float LockoutRemaining { get; private set; }
    public TechnologyOverloadPhase Phase => BoostRemaining > 0f
        ? TechnologyOverloadPhase.Boosting
        : LockoutRemaining > 0f
            ? TechnologyOverloadPhase.Lockout
            : CooldownRemaining > 0f
                ? TechnologyOverloadPhase.Cooldown
                : TechnologyOverloadPhase.Ready;
    public float FireRateMultiplier => Phase == TechnologyOverloadPhase.Boosting
        ? 2f
        : Phase == TechnologyOverloadPhase.Lockout ? 0f : 1f;
    public float DamageMultiplier(DamageType type) =>
        Phase == TechnologyOverloadPhase.Boosting && type == DamageType.Energy ? 1.3f : 1f;
}
```

`TryActivate(true)` sets cooldown `30`, boost `5`, and lockout `0`. `Tick` must consume a large delta across boost and lockout while reducing the total cooldown once. `Restore(false,...)` clears all timers; `Restore(true,...)` clamps them to non-negative values.

- [ ] **Step 4: Add and test modifier composition**

Add:

```csharp
public static float ResolveFireRate(float leaderMultiplier, float technologyMultiplier)
{
    if (leaderMultiplier <= 0f || technologyMultiplier <= 0f) return 0f;
    return Math.Max(1f, Math.Max(leaderMultiplier, technologyMultiplier));
}

public static float ResolveDamage(DamageType type, float technologyMultiplier) =>
    type == DamageType.Energy ? Math.Max(1f, technologyMultiplier) : 1f;
```

Test leader `1.75` plus technology `2` returns `2`, either lockout returns `0`, and non-energy damage remains `1`.

- [ ] **Step 5: Run focused tests to verify GREEN**

Expected: all `TechnologyOverloadTests` pass with zero failures.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Combat/TechnologyOverloadModel.cs* Assets/_Game/Tests/EditMode/TechnologyOverloadTests.cs*
git commit -m "feat: add technology overload rules"
```

---

### Task 2: Pure Scout Drone Deployment and Patrol Rules

**Files:**
- Create: `Assets/_Game/Scripts/World/ScoutDroneModel.cs`
- Create: `Assets/_Game/Scripts/World/ScoutDroneModel.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ScoutDroneTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ScoutDroneTests.cs.meta`

**Interfaces:**
- Produces: `ScoutDroneDeploymentRules.ActiveCount(bool,IReadOnlyList<DroneBayState>)`.
- Produces: `DroneBayState(bool completed,bool hasLogistics)`.
- Produces: `ScoutDronePatrolModel.Tick(float)`, `Position(float,float,int,int)`, and `RevealDue`.

- [ ] **Step 1: Write failing deployment and patrol tests**

Add tests proving:

```csharp
[Test]
public void DeploymentRequiresResearchCompletionAndLogistics()
{
    var bays = new[]
    {
        new DroneBayState(true, true),
        new DroneBayState(true, false),
        new DroneBayState(false, true)
    };
    Assert.That(ScoutDroneDeploymentRules.ActiveCount(false, bays), Is.Zero);
    Assert.That(ScoutDroneDeploymentRules.ActiveCount(true, bays), Is.EqualTo(1));
}

[Test]
public void PatrolUsesStableSeparatedPhasesAndOneSecondRevealCadence()
{
    var first = new ScoutDronePatrolModel();
    var second = new ScoutDronePatrolModel();
    Assert.That(first.Position(10f, 20f, 0, 2), Is.Not.EqualTo(second.Position(10f, 20f, 1, 2)));
    Assert.That(first.Tick(.99f), Is.False);
    Assert.That(first.Tick(.01f), Is.True);
    Assert.That(first.Tick(2.1f), Is.True);
}
```

Also test zero bays, null lists, negative delta, a single drone, and city-center movement.

- [ ] **Step 2: Run focused tests to verify RED**

Run EditMode with filter `WasteCity.Tests.ScoutDroneTests`.

Expected: compilation fails because scout drone types do not exist.

- [ ] **Step 3: Implement minimal pure rules**

`DroneBayState` exposes `Completed` and `HasLogistics`. `ActiveCount` returns zero without research and otherwise counts states with both flags.

`ScoutDronePatrolModel` stores elapsed patrol and reveal time. Use radius `6`, angular speed `30` degrees per second, and evenly divide a full circle by `count` for the index phase. `Tick` returns true if at least one one-second reveal boundary was crossed and retains the fractional remainder.

- [ ] **Step 4: Run focused tests to verify GREEN**

Expected: all `ScoutDroneTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/World/ScoutDroneModel.cs* Assets/_Game/Tests/EditMode/ScoutDroneTests.cs*
git commit -m "feat: define scout drone deployment rules"
```

---

### Task 3: Runtime Overload, Turret Integration, and Placeholder Feedback

**Files:**
- Create: `Assets/_Game/Scripts/Combat/FormalTechnologyRouteController.cs`
- Create: `Assets/_Game/Scripts/Combat/FormalTechnologyRouteController.cs.meta`
- Modify: `Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- Modify: `Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- Modify: `Assets/_Game/Scripts/Leader/FormalLeaderController.cs`
- Modify: `Assets/_Game/Scripts/Research/ResearchController.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Consumes: Task 1 overload and composition models.
- Produces: `ITurretCombatModifierSource.FireRateMultiplier` and `DamageMultiplier(DamageType)`.
- Produces: `FormalTechnologyRouteController.Model`, `TryActivate()`, and `Restore(float,float,float)`.

- [ ] **Step 1: Write failing PlayMode tests**

Add tests proving:

- activation is rejected before `core.research.energy-weapons`;
- activation after research exposes fire rate `2` and energy multiplier `1.3`;
- a healthy recruited leader plus technology boost resolves to `2`, not `3.5`;
- leader or technology lockout resolves to zero;
- an energy turret deals more damage during technology boost;
- a physical turret receives fire-rate timing but not a damage multiplier;
- the runtime marker has stable ID `technology.status.overload`.

Use isolated positions at least `100` world units from the formal scene city to prevent ambient city defense from affecting health assertions.

- [ ] **Step 2: Run focused PlayMode tests to verify RED**

Filter `RuntimeSceneTests.TechnologyOverload_`.

Expected: compilation fails because runtime controller and combat modifier interface do not exist.

- [ ] **Step 3: Add the shared turret modifier interface**

Replace `ITurretFireRateSource` with:

```csharp
public interface ITurretCombatModifierSource
{
    float FireRateMultiplier { get; }
    float DamageMultiplier(DamageType damageType);
}
```

Make `FormalLeaderController` implement the interface and return `1f` from `DamageMultiplier`.

- [ ] **Step 4: Implement the runtime technology controller**

The controller owns `TechnologyOverloadModel`, reads `research.HasEnergyWeapons`, handles `T`, and composes its multiplier with the serialized leader reference via `TurretCombatModifierRules`. `TryActivate()` delegates with the current unlock flag.

Create its marker lazily as a child of the city, attach a `SpriteRenderer`, and call:

```csharp
VisualSlot.Attach(marker, "technology.status.overload", renderer, renderer.color);
```

Enable the marker only while boosting and tint it orange during boost.

- [ ] **Step 5: Apply energy damage multiplier in turrets**

In `PlaceholderTurret.Update`, calculate:

```csharp
float attackRate = modifier?.FireRateMultiplier ?? 1f;
float routeDamage = modifier?.DamageMultiplier(profile.DamageType) ?? 1f;
float researchDamage = physical ? research?.TurretDamageMultiplier ?? 1f : 1f;
int dealt = weapon.Tick(delta * attackRate, inventory, health, armor, researchDamage * routeDamage);
```

Do not alter infection cadence or mind-control behavior.

- [ ] **Step 6: Update building-controller injection**

Store the technology controller and pass it to restored/new turrets. Keep a fallback to the leader modifier so older test setups and scenes remain valid:

```csharp
ITurretCombatModifierSource TurretModifier => technology != null ? technology : leader;
```

- [ ] **Step 7: Run focused PlayMode tests to verify GREEN**

Expected: all `TechnologyOverload_` tests pass.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Game/Scripts/Combat/FormalTechnologyRouteController.cs* Assets/_Game/Scripts/Building/BuildingRuntime.cs Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs Assets/_Game/Scripts/Leader/FormalLeaderController.cs Assets/_Game/Scripts/Research/ResearchController.cs Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: connect technology overload to turrets"
```

---

### Task 4: Runtime Scout and Repair Placeholders

**Files:**
- Create: `Assets/_Game/Scripts/World/FormalDroneController.cs`
- Create: `Assets/_Game/Scripts/World/FormalDroneController.cs.meta`
- Modify: `Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- Modify: `Assets/_Game/Scripts/Research/ResearchController.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Consumes: Task 2 deployment and patrol models.
- Produces: `FormalDroneController.ActiveDroneCount` and `RefreshDeployment()`.
- Consumes: `PlaceholderWorldView.RevealAroundWorld(Vector2,int)`.

- [ ] **Step 1: Write failing runtime tests**

Add PlayMode tests proving:

- no research means zero active scout drones;
- completed research plus one completed, logistics-connected repair bay means one drone;
- losing logistics removes or disables the drone;
- two active bays produce two different patrol positions;
- a reveal tick reveals a tile outside the city exploration radius;
- scout and repair placeholders expose their exact stable IDs.

- [ ] **Step 2: Run focused tests to verify RED**

Filter `RuntimeSceneTests.UnmannedSystems_`.

Expected: compilation fails because `FormalDroneController` does not exist.

- [ ] **Step 3: Implement runtime deployment**

Each update, collect `BuildingRuntime` instances whose definition is `BuildingCatalog.AutomatedRepairBay`, project them to `DroneBayState`, and compare the active count with the current placeholder list. Create or destroy only the difference.

Each drone owns a `ScoutDronePatrolModel`, updates its position around the city, and calls `world.RevealAroundWorld(position, 2)` whenever `Tick` returns true. Attach `technology.unit.scout-drone`.

- [ ] **Step 4: Add repair-mech feedback**

In `PlaceholderAutomatedRepairBay.Configure`, create one child square marker, attach `technology.unit.repair-mech`, and orbit it within `0.8` world units. Keep repair gameplay at `20` health every `6` seconds inside radius `6`.

- [ ] **Step 5: Run focused tests to verify GREEN**

Expected: all `UnmannedSystems_` PlayMode tests pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/World/FormalDroneController.cs* Assets/_Game/Scripts/Building/BuildingRuntime.cs Assets/_Game/Scripts/Research/ResearchController.cs Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: deploy scout and repair drone placeholders"
```

---

### Task 5: Formal Scene Wiring and Schema 26 Persistence

**Files:**
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- Modify: `Assets/_Game/Editor/FormalProjectSetup.cs`
- Modify: `Assets/_Game/Scenes/FormalPrototype.unity`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Consumes: `FormalTechnologyRouteController.Model` and `Restore`.
- Produces: schema `26` fields `technologyOverloadCooldown`, `technologyOverloadBoost`, and `technologyOverloadLockout`.

- [ ] **Step 1: Write failing schema tests**

Update current-schema expectations to `26` and add tests proving schema 26 JSON round-trips all three technology overload fields. Add an explicit schema 25 fixture with absent fields and assert all values deserialize as zero.

- [ ] **Step 2: Write failing formal-scene restore tests**

Load `FormalPrototype` and prove:

- schema 26 plus completed energy-weapons research restores the three timers;
- schema 25 restores ready;
- schema 26 without energy-weapons research clears injected timers;
- `FormalTechnologyRouteController` and `FormalDroneController` are present with non-null required references.

- [ ] **Step 3: Run focused EditMode and PlayMode tests to verify RED**

Expected: current schema assertion fails and the formal scene lacks both controllers.

- [ ] **Step 4: Implement schema 26**

Set `FormalSaveData.schema=26`, accept decoder schemas through 26, capture the three timers, and restore them only for schema 26. Call:

```csharp
technology.Restore(
    data.schema >= 26 ? data.technologyOverloadCooldown : 0f,
    data.schema >= 26 ? data.technologyOverloadBoost : 0f,
    data.schema >= 26 ? data.technologyOverloadLockout : 0f);
```

The controller itself must clear timers when energy weapons is not completed.

- [ ] **Step 5: Update scene generator wiring**

After creating leader and research:

- add `FormalTechnologyRouteController` to `FormalGameBootstrap`;
- assign research, leader, and city;
- set it on `PlaceholderBuildingController`;
- assign it to `FormalSaveController`;
- add `FormalDroneController`;
- assign research, city, world, and buildings.

- [ ] **Step 6: Regenerate the formal scene**

Run:

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -executeMethod WasteCity.Editor.FormalProjectSetup.Configure \
  -logFile /tmp/wastecity-technology-scene.log \
  -quit
```

If `packages.unity.com` returns `ECONNRESET`, temporarily point only `com.unity.inputsystem` to the exact cached package `1.7.0`, run the command, then restore both package files to the registry form before staging.

- [ ] **Step 7: Run focused tests to verify GREEN**

Expected: save tests and formal-scene technology tests pass.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Game/Scripts/Persistence/FormalSaveData.cs Assets/_Game/Scripts/Persistence/FormalSaveController.cs Assets/_Game/Editor/FormalProjectSetup.cs Assets/_Game/Scenes/FormalPrototype.unity Assets/_Game/Tests/EditMode/FormalSaveTests.cs Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: persist technology route runtime"
```

---

### Task 6: Full Verification, Windows Build, and Baseline Documentation

**Files:**
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `README.md`

**Interfaces:**
- Records the exact feature commit, schema `26`, test totals, successful build gates, and pending real-Windows smoke.

- [ ] **Step 1: Run static checks**

```bash
git diff --check
git status --short
rg -n "file:/Users|/Users/baiyan1" Packages Assets README.md Docs/05-Formal-Development-Roadmap-ZH.md
```

Expected: no whitespace errors, no tracked local path, and only current milestone changes.

- [ ] **Step 2: Run all EditMode tests**

Run without `-quit`, write XML to `/tmp/wastecity-technology-editmode.xml`, and require `failed="0"`.

- [ ] **Step 3: Run all PlayMode tests**

Run without `-quit`, write XML to `/tmp/wastecity-technology-playmode.xml`, and require `failed="0"`.

- [ ] **Step 4: Run headless compile**

Run Unity `-batchmode -nographics -quit` and require exit code `0`, no `error CS`, and no unhandled exception.

- [ ] **Step 5: Build Windows 64-bit player**

Run `WasteCity.Editor.FormalBuildTools.BuildWindows`, require exit code `0`, and verify:

```bash
file Builds/Windows/WasteCity.exe
```

Expected: `PE32+ executable (GUI) x86-64, for MS Windows`.

- [ ] **Step 6: Record deferred Windows smoke accurately**

Record:

```text
Windows 10/11 independent executable smoke: pending.
Reason: macOS development machine has no Windows compatibility layer.
Release effect: internal development baseline only; not a candidate release.
```

- [ ] **Step 7: Update roadmap and README**

Record the exact feature commit, exact XML totals, schema `26`, implemented overload/drone behavior, Windows build success, stable placeholder IDs, and pending real-Windows smoke.

- [ ] **Step 8: Commit documentation**

```bash
git add Docs/05-Formal-Development-Roadmap-ZH.md README.md
git commit -m "docs: record technology route baseline"
```

- [ ] **Step 9: Final inspection**

```bash
git status --short
git log -10 --oneline
git diff HEAD~1 --check
```

Expected: clean worktree, separate implementation/documentation commits, and no generated or local-only artifacts tracked.
