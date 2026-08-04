# Cultivation Sword Intent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the cultivation-route sword-intent execution, sword-riding platform upgrade, and puppet maintenance loop with schema 27 persistence.

**Architecture:** Keep sword-intent cadence, stack/execution rules, and puppet upkeep in pure C# models. Adapt existing enemy, turret, building-upgrade, puppet, and save entry points without adding projectile, resource, or combat systems. Runtime visuals remain replaceable `VisualSlot` placeholders.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Input System `1.7.0`, URP 2D placeholders, Windows Mono player build.

## Global Constraints

- Follow `Docs/01-Game-Design-Document-ZH.md`, `Docs/05-Formal-Development-Roadmap-ZH.md`, and `Docs/superpowers/specs/2026-08-04-cultivation-sword-intent-design.md`.
- Sword intent has maximum `20` stacks; the twentieth flying-sword hit deals `100%` maximum-health true damage and clears stacks.
- True damage bypasses armor, damage matrices, physical reductions, and shield while preserving damage/death events.
- Each flying-sword tower source adds at most one stack per second; first actual damage adds immediately.
- Sword intent clears on death, conversion, or execution and does not decay.
- Sword-riding platform upgrade requires civilization level `2`, `core.research.sword-riding`, and `20` spirit iron.
- Sword-riding range multiplier is `1.3` for sword-array tower and sword-riding platform; no additional damage or rate bonus.
- Puppets consume `1` energy crystal every `60` seconds; shortage causes reversible dormancy.
- Use stable IDs `cultivation.status.sword-intent`, `cultivation.building.sword-riding-platform`, `cultivation.unit.puppet`, and `cultivation.unit.puppet.dormant`.
- Save schema becomes `27`; schema `26` and older use zero sword intent and a fresh active puppet-maintenance cycle.
- Do not add projectiles, spirit-stone resources, new recipes, high-frequency swords, formal art, animation, or audio.
- Do not commit local package paths or generated `Library/`, `Logs/`, `Builds/`, `TestResults/`, license, or credential files.
- Real Windows executable smoke remains pending for Windows 10/11.

---

### Task 1: Sword Intent, Hit Cadence, and True Damage Rules

**Files:**
- Create: `Assets/_Game/Scripts/Combat/SwordIntentModel.cs`
- Test: `Assets/_Game/Tests/EditMode/SwordIntentTests.cs`
- Modify: `Assets/_Game/Scripts/Combat/HealthModel.cs`
- Modify: `Assets/_Game/Tests/EditMode/HealthModelTests.cs`

**Interfaces:**
- Produces: `SwordIntentModel.Stacks`, `AddHit(int maximumHealth)`, `Clear()`, and `Restore(int)`.
- Produces: `SwordIntentHitResult.Executed` and `TrueDamage`.
- Produces: `SwordIntentEmitterModel.Tick(float,bool)`.
- Produces: `HealthModel.ApplyTrueDamage(int)`.

- [ ] **Step 1: Write failing sword-intent tests**

Test that 19 hits do not execute, hit 20 returns `Executed=true`, `TrueDamage=maximumHealth`, and stacks clear. Test restore clamping to `0..19`, clear, dead/invalid maximum handling, first emitter hit immediate, no-damage frames ignored, and one layer per second.

```csharp
var model = new SwordIntentModel();
for (int i = 0; i < 19; i++) Assert.That(model.AddHit(500).Executed, Is.False);
SwordIntentHitResult result = model.AddHit(500);
Assert.That(result.Executed, Is.True);
Assert.That(result.TrueDamage, Is.EqualTo(500));
Assert.That(model.Stacks, Is.Zero);
```

- [ ] **Step 2: Run focused EditMode tests to verify RED**

Filter `WasteCity.Tests.SwordIntentTests`.

Expected: compilation fails because sword-intent types do not exist.

- [ ] **Step 3: Implement the pure models**

`SwordIntentModel.AddHit` increments to 20, calculates `ceil(maximumHealth * stacks * .05)`, clears, and returns the immutable result. `SwordIntentEmitterModel` mirrors the one-second continuous-hit cadence already used by infection.

- [ ] **Step 4: Write failing true-damage tests**

Create health `100`, grant shield `200`, apply physical mitigation, call `ApplyTrueDamage(100)`, and assert current `0`, shield unchanged, damage event `100`, death event once.

- [ ] **Step 5: Implement true damage**

Add a method that clamps input and current health, bypasses shield and armor, emits `Damaged`, then emits `Died` exactly once when current reaches zero.

- [ ] **Step 6: Run focused tests to verify GREEN**

Expected: all sword-intent and true-damage tests pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Game/Scripts/Combat/SwordIntentModel.cs* Assets/_Game/Scripts/Combat/HealthModel.cs Assets/_Game/Tests/EditMode/SwordIntentTests.cs* Assets/_Game/Tests/EditMode/HealthModelTests.cs
git commit -m "feat: add sword intent execution rules"
```

---

### Task 2: Sword-Riding Platform Upgrade

**Files:**
- Modify: `Assets/_Game/Scripts/Building/BuildingGrid.cs`
- Modify: `Assets/_Game/Scripts/Building/BuildingUpgradeModel.cs`
- Modify: `Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs`
- Modify: `Assets/_Game/Scripts/Combat/DefenseTowerCatalog.cs`
- Modify: `Assets/_Game/Scripts/Research/RouteTechnologyEffects.cs`
- Modify: `Assets/_Game/Tests/EditMode/BuildingUpgradeTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/RouteTechnologyEffectTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/RouteDefenseTowerTests.cs`

**Interfaces:**
- Produces: `BuildingCatalog.SwordRidingPlatform`.
- Changes: `BuildingUpgradeCatalog.For(BuildingDefinition,int,bool,bool)` where the booleans are alloy armor and sword riding.

- [ ] **Step 1: Write failing upgrade tests**

Prove the target ID/name/size, exclusion from direct build menu, inclusion in all definitions, true-essence tower profile, `1.3` range for base and upgrade, and recipe requirements:

```csharp
Assert.That(BuildingUpgradeCatalog.For(
    BuildingCatalog.SwordArrayTower, 2, false, true).Target,
    Is.SameAs(BuildingCatalog.SwordRidingPlatform));
Assert.That(BuildingUpgradeCatalog.For(
    BuildingCatalog.SwordArrayTower, 2, false, false), Is.Null);
```

Assert cost ID `SpiritIron`, cost `20`, and no regression to the heavy-turret recipe.

- [ ] **Step 2: Run focused tests to verify RED**

Filter building-upgrade, route-effect, and defense-tower tests.

Expected: missing platform definition/signature failures.

- [ ] **Step 3: Implement catalog and recipe**

Add the upgrade-only building definition with health `300`, build time `12`, and profile matching the sword-array tower. Extend the range rule to both building IDs. Extend upgrade selection without changing heavy-turret requirements.

- [ ] **Step 4: Connect mouse upgrade**

Read `research.HasSwordRiding`, pass both research flags to `BuildingUpgradeCatalog.For`, and make the lock/failure message name the selected route material rather than always saying alloy.

- [ ] **Step 5: Run focused tests to verify GREEN**

Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Building Assets/_Game/Scripts/Combat/DefenseTowerCatalog.cs Assets/_Game/Scripts/Research/RouteTechnologyEffects.cs Assets/_Game/Tests/EditMode
git commit -m "feat: add sword-riding platform upgrade"
```

---

### Task 3: Runtime Enemy Sword Intent

**Files:**
- Create: `Assets/_Game/Scripts/Combat/EnemySwordIntentStatus.cs`
- Modify: `Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs`
- Modify: `Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Consumes: Task 1 models and true damage.
- Produces: `PlaceholderEnemy.SwordIntent`, `ApplySwordIntent()`, `RestoreSwordIntent(int)`, and `ClearSwordIntent()`.

- [ ] **Step 1: Write failing PlayMode tests**

Prove:

- sword-array and sword-riding towers add one layer after actual damage;
- physical and non-flying true-essence sources do not;
- one continuous tower adds at most one layer per second;
- marker stable ID is `cultivation.status.sword-intent`;
- restoring 19 then applying one kills a full-health, shielded enemy and clears;
- successful mind control clears layers and hides marker.

- [ ] **Step 2: Run focused PlayMode tests to verify RED**

Filter `RuntimeSceneTests.SwordIntent_`.

Expected: missing runtime status API.

- [ ] **Step 3: Implement enemy status**

Attach `EnemySwordIntentStatus` during `PlaceholderEnemy.Configure`, expose its model/marker, and clear it during `TryConvert`. The status calls `ApplyTrueDamage` on execution.

- [ ] **Step 4: Connect flying-sword towers**

Give each `PlaceholderTurret` a `SwordIntentEmitterModel`. Advance it every active update and call `nearest.ApplySwordIntent()` only after dealt damage when `profile.ConsumableId == ResourceIds.FlyingSword`.

- [ ] **Step 5: Run focused tests to verify GREEN**

Expected: all `SwordIntent_` tests pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Combat/EnemySwordIntentStatus.cs* Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs Assets/_Game/Scripts/Building/BuildingRuntime.cs Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: connect flying swords to sword intent"
```

---

### Task 4: Puppet Maintenance and Dormancy

**Files:**
- Create: `Assets/_Game/Scripts/Combat/PuppetMaintenanceModel.cs`
- Modify: `Assets/_Game/Scripts/Combat/PlaceholderPuppet.cs`
- Modify: `Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs`
- Create: `Assets/_Game/Tests/EditMode/PuppetMaintenanceTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Produces: `PuppetMaintenanceModel.Elapsed`, `Active`, `Tick(float,ResourceInventory)`, and `Restore(float,bool)`.
- Changes: `PlaceholderPuppet.Configure(..., ResourceInventory maintenanceInventory=null, float maintenanceElapsed=0f, bool maintenanceActive=true)`.

- [ ] **Step 1: Write failing maintenance tests**

Prove a new model is active, `.Tick(59.9)` spends nothing, crossing `60` spends one energy crystal, shortage sets inactive and holds elapsed at `60`, replenishment reactivates and resets elapsed, negative time is ignored, and restore clamps values.

- [ ] **Step 2: Run focused EditMode tests to verify RED**

Filter `WasteCity.Tests.PuppetMaintenanceTests`.

Expected: missing model.

- [ ] **Step 3: Implement the pure model**

When inventory is null, maintenance is disabled and the model remains active. When due with inventory, spend exactly one `EnergyCrystal`; on failure retain due elapsed and set inactive.

- [ ] **Step 4: Write failing PlayMode tests**

Create a puppet with an empty inventory, advance through 60 seconds using a restored due state, assert `FriendlyUnitAgent.enabled=false` and dormant stable ID; add one energy crystal, tick a frame, and assert enabled plus active stable ID.

- [ ] **Step 5: Connect runtime maintenance**

`PlaceholderPuppet` owns the model and original renderer, ticks independently of the agent, toggles the agent, and reconfigures its existing `VisualSlot`. Formal spawns pass `economy.Inventory`; compatibility callers omit it.

- [ ] **Step 6: Run focused tests to verify GREEN**

Expected: all maintenance EditMode and PlayMode tests pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Game/Scripts/Combat/PuppetMaintenanceModel.cs* Assets/_Game/Scripts/Combat/PlaceholderPuppet.cs Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs Assets/_Game/Tests/EditMode/PuppetMaintenanceTests.cs* Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: add puppet energy maintenance"
```

---

### Task 5: Schema 27 Persistence

**Files:**
- Modify: `Assets/_Game/Scripts/Combat/FormalCombatController.cs`
- Modify: `Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Adds: `EnemySnapshot.swordIntentStacks`.
- Adds: `FriendlyUnitSnapshot.maintenanceElapsed` and `maintenanceActive`.
- Produces: schema `27`.

- [ ] **Step 1: Write failing save tests**

Update current schema expectation to 27. Prove JSON round-trips enemy sword intent and puppet maintenance; explicit schema 26 fixtures default to zero intent and active maintenance.

- [ ] **Step 2: Write failing scene restore tests**

Load `FormalPrototype` and prove schema 27 restores hostile intent and puppet maintenance, controlled enemies restore without intent, and schema 26 puppets get an active fresh cycle.

- [ ] **Step 3: Run focused tests to verify RED**

Expected: missing snapshot fields/current schema failures.

- [ ] **Step 4: Implement schema 27**

Capture and restore enemy intent only for schema 27. Capture puppet maintenance fields; pass schema-aware defaults through `FormalFriendlyUnitController.Restore`. Keep behemoth behavior unchanged.

- [ ] **Step 5: Run focused tests to verify GREEN**

Expected: all formal save and cultivation restore tests pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Combat Assets/_Game/Scripts/Persistence Assets/_Game/Tests/EditMode/FormalSaveTests.cs Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: persist cultivation combat state"
```

---

### Task 6: Full Verification and Accurate Baseline

**Files:**
- Modify: `README.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`

**Interfaces:**
- Records exact feature commit, test totals, schema 27, successful compile/build, stable slots, and pending Windows smoke.

- [ ] **Step 1: Run static checks**

Run `git diff --check`, `git status --short`, and scan tracked project files for local `/Users/baiyan1` paths.

- [ ] **Step 2: Run all EditMode tests**

Write fresh XML and require zero failures.

- [ ] **Step 3: Run all PlayMode tests**

Write fresh XML and require zero failures.

- [ ] **Step 4: Run headless compile**

Require exit code zero, no `error CS`, and successful batchmode exit.

- [ ] **Step 5: Build Windows 64-bit**

Run `WasteCity.Editor.FormalBuildTools.BuildWindows`, require exit zero, and verify `Builds/Windows/WasteCity.exe` is PE32+ x86-64.

- [ ] **Step 6: Restore portable packages**

If cached local Input System `1.7.0` was used because of `packages.unity.com` network failure, restore `manifest.json` and lock file to registry form before staging.

- [ ] **Step 7: Update and commit baseline docs**

Record exact feature hash, exact test totals, schema `27`, completed cultivation feature, successful Windows build, and pending real Windows 10/11 executable smoke.

```bash
git add README.md Docs/05-Formal-Development-Roadmap-ZH.md
git commit -m "docs: record cultivation route baseline"
```

- [ ] **Step 8: Final inspection**

Require clean status, separate implementation/doc commits, no generated artifacts, no local paths, and no release-candidate claim.
