# Biological Infection Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the GDD-defined biological infection loop so spore towers, acid towers, and behemoths apply persistent percentage damage, ten-stack bursts, three-unit propagation, save restoration, and replaceable placeholder feedback.

**Architecture:** Keep infection arithmetic and target selection in ordinary C# models. Attach one `EnemyInfectionStatus` adapter to each runtime enemy, and let biological attack sources use a shared one-second emitter cadence before calling the enemy's infection entry point. Persist only enemy infection layers and tick progress in schema 25.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Input System `1.7.0`, URP 2D placeholders, Windows Mono player build.

## Global Constraints

- Core GDD values are fixed: `2%` maximum-health biological damage per second, burst threshold `10`, propagation radius `3`, propagated layers `5`.
- Infection is persistent; it has no natural decay.
- A continuous-DPS attack source applies at most one layer per second, with the first valid biological damage applying immediately.
- Infection affects only living hostile enemies and clears on mind control.
- Every new visual uses `VisualSlot`; infection feedback uses stable ID `biological.status.infection`.
- Do not change existing tower or behemoth base damage, range, speed, resource cost, or consumption.
- Do not add formal art, audio, infection resistance, cleansing, research upgrades, or route-bridge features.
- Schema 24 and older saves remain readable and restore with no infection.
- On this macOS machine, run EditMode, PlayMode, headless compile, and Windows build. Record Windows executable smoke as pending for a real Windows 10/11 machine.
- Keep `Packages/manifest.json` and `Packages/packages-lock.json` on registry versions in commits. Any temporary local cache path used for Unity execution must be restored before review.

---

### Task 1: Infection State and Source Cadence Models

**Files:**
- Create: `Assets/_Game/Scripts/Combat/InfectionModel.cs`
- Create: `Assets/_Game/Scripts/Combat/InfectionModel.cs.meta`
- Create: `Assets/_Game/Scripts/Combat/InfectionEmitterModel.cs`
- Create: `Assets/_Game/Scripts/Combat/InfectionEmitterModel.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/InfectionModelTests.cs`
- Create: `Assets/_Game/Tests/EditMode/InfectionModelTests.cs.meta`

**Interfaces:**
- Produces: `InfectionModel.Stacks`, `InfectionModel.Elapsed`, `bool InfectionModel.AddStacks(int amount)`, `int InfectionModel.Tick(float deltaSeconds, int maximumHealth)`, `void InfectionModel.Clear()`, and `void InfectionModel.Restore(int stacks, float elapsed)`.
- Produces: `bool InfectionEmitterModel.Tick(float deltaSeconds, bool dealtBiologicalDamage)`.
- `AddStacks` returns `true` when the threshold is reached and resets stacks and elapsed to zero.
- `Tick` returns total raw biological damage due during the supplied interval.

- [ ] **Step 1: Write failing model tests**

```csharp
[Test]
public void FirstValidBiologicalDamageAppliesImmediatelyThenWaitsOneSecond()
{
    var emitter = new InfectionEmitterModel();
    Assert.That(emitter.Tick(.1f, true), Is.True);
    Assert.That(emitter.Tick(.9f, true), Is.False);
    Assert.That(emitter.Tick(.1f, true), Is.True);
}

[Test]
public void TenLayersBurstAndResetTheStatus()
{
    var infection = new InfectionModel();
    Assert.That(infection.AddStacks(9), Is.False);
    Assert.That(infection.AddStacks(1), Is.True);
    Assert.That(infection.Stacks, Is.Zero);
    Assert.That(infection.Elapsed, Is.Zero);
}

[Test]
public void InfectionDealsTwoPercentMaximumHealthEachSecond()
{
    var infection = new InfectionModel();
    infection.AddStacks(1);
    Assert.That(infection.Tick(.99f, 250), Is.Zero);
    Assert.That(infection.Tick(.01f, 250), Is.EqualTo(5));
    Assert.That(infection.Tick(2f, 250), Is.EqualTo(10));
}
```

Also add literal-value tests proving:

- no stacks means no damage;
- non-positive deltas do nothing;
- `250 × 2%` produces `5`;
- maximum health `1` still produces `1`;
- `Restore(-1, -2f)` becomes `0` stacks and `0` elapsed;
- `Restore(20, 1.25f)` becomes `9` stacks and `.25f` elapsed;
- frames with no dealt damage never produce emitter events.

- [ ] **Step 2: Run the focused EditMode tests and verify RED**

Run Unity without `-quit`:

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.InfectionModelTests" \
  -testResults "/tmp/wastecity-infection-model-red.xml" \
  -logFile "/tmp/wastecity-infection-model-red.log"
```

Expected: compile failure because `InfectionModel` and `InfectionEmitterModel` do not exist.

- [ ] **Step 3: Implement the minimal ordinary C# models**

Use these contracts:

```csharp
public sealed class InfectionModel
{
    public const int BurstThreshold = 10;
    public const float TickSeconds = 1f;
    public const float MaximumHealthFraction = .02f;
    public int Stacks { get; private set; }
    public float Elapsed { get; private set; }
    public bool AddStacks(int amount);
    public int Tick(float deltaSeconds, int maximumHealth);
    public void Clear();
    public void Restore(int stacks, float elapsed);
}

public sealed class InfectionEmitterModel
{
    public const float IntervalSeconds = 1f;
    public bool Tick(float deltaSeconds, bool dealtBiologicalDamage);
}
```

Derive damage with `Math.Max(1, (int)Math.Ceiling(Math.Max(1, maximumHealth) * .02f))`. Do not reference `UnityEngine`.

- [ ] **Step 4: Re-run the focused tests and verify GREEN**

Expected XML: all `InfectionModelTests` pass with zero failures.

- [ ] **Step 5: Commit the state models**

```bash
git add Assets/_Game/Scripts/Combat/InfectionModel.cs \
  Assets/_Game/Scripts/Combat/InfectionModel.cs.meta \
  Assets/_Game/Scripts/Combat/InfectionEmitterModel.cs \
  Assets/_Game/Scripts/Combat/InfectionEmitterModel.cs.meta \
  Assets/_Game/Tests/EditMode/InfectionModelTests.cs \
  Assets/_Game/Tests/EditMode/InfectionModelTests.cs.meta
git commit -m "feat: add biological infection state rules"
```

---

### Task 2: Infection Propagation Rules

**Files:**
- Create: `Assets/_Game/Scripts/Combat/InfectionSpreadRules.cs`
- Create: `Assets/_Game/Scripts/Combat/InfectionSpreadRules.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/InfectionSpreadRulesTests.cs`
- Create: `Assets/_Game/Tests/EditMode/InfectionSpreadRulesTests.cs.meta`

**Interfaces:**
- Produces: `InfectionSpreadCandidate`.
- Produces: `int[] InfectionSpreadRules.SelectTargets(int sourceId, float sourceX, float sourceY, float radius, InfectionSpreadCandidate[] candidates)`.

- [ ] **Step 1: Write failing spread-selection tests**

```csharp
[Test]
public void BurstSelectsLivingHostilesInsideInclusiveThreeUnitRadius()
{
    var candidates = new[]
    {
        new InfectionSpreadCandidate(1, 0f, 0f, true, false),
        new InfectionSpreadCandidate(2, 3f, 0f, true, false),
        new InfectionSpreadCandidate(3, 3.01f, 0f, true, false),
        new InfectionSpreadCandidate(4, 1f, 0f, false, false),
        new InfectionSpreadCandidate(5, 1f, 0f, true, true)
    };

    int[] selected = InfectionSpreadRules.SelectTargets(1, 0f, 0f, 3f, candidates);

    Assert.That(selected, Is.EqualTo(new[] { 2 }));
}
```

Add tests for `null` candidates, negative radius clamping to zero, and deterministic candidate-order output.

- [ ] **Step 2: Run the focused tests and verify RED**

Use `-testFilter "WasteCity.Tests.InfectionSpreadRulesTests"`.

Expected: compile failure because the spread types do not exist.

- [ ] **Step 3: Implement candidate filtering**

```csharp
public readonly struct InfectionSpreadCandidate
{
    public InfectionSpreadCandidate(int id, float x, float y, bool isAlive, bool isFriendly);
    public int Id { get; }
    public float X { get; }
    public float Y { get; }
    public bool IsAlive { get; }
    public bool IsFriendly { get; }
}
```

Return IDs in input order. Exclude `sourceId`, dead candidates, friendly candidates, and candidates whose squared distance is greater than `radius²`.

- [ ] **Step 4: Re-run the focused tests and verify GREEN**

Expected: all spread-rule tests pass.

- [ ] **Step 5: Commit propagation rules**

```bash
git add Assets/_Game/Scripts/Combat/InfectionSpreadRules.cs \
  Assets/_Game/Scripts/Combat/InfectionSpreadRules.cs.meta \
  Assets/_Game/Tests/EditMode/InfectionSpreadRulesTests.cs \
  Assets/_Game/Tests/EditMode/InfectionSpreadRulesTests.cs.meta
git commit -m "feat: add infection propagation rules"
```

---

### Task 3: Runtime Enemy Infection Status and Placeholder Feedback

**Files:**
- Create: `Assets/_Game/Scripts/Combat/EnemyInfectionStatus.cs`
- Create: `Assets/_Game/Scripts/Combat/EnemyInfectionStatus.cs.meta`
- Modify: `Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Consumes: `InfectionModel` and `InfectionSpreadRules.SelectTargets`.
- Produces: `EnemyInfectionStatus Infection`, `void ApplyInfection(int stacks)`, `void RestoreInfection(int stacks, float elapsed)`, and `void ClearInfection()` on `PlaceholderEnemy`.
- Produces: read-only `EnemyInfectionStatus.Stacks`, `EnemyInfectionStatus.Elapsed`, and `EnemyInfectionStatus.Marker`.

- [ ] **Step 1: Write failing PlayMode tests**

Add tests that instantiate real `PlaceholderEnemy` objects and prove:

```csharp
enemy.ApplyInfection(1);
Assert.That(enemy.Infection.Stacks, Is.EqualTo(1));
Assert.That(enemy.Infection.Marker.GetComponent<VisualSlot>().StableId,
    Is.EqualTo("biological.status.infection"));
```

Also prove:

- after one second, a `250` maximum-health light-armored enemy loses `5` additional health from infection;
- the tenth layer resets the source and gives exactly five layers to a hostile enemy at distance `3`;
- a hostile at `3.01`, a dead enemy, and a controlled enemy receive no spread;
- converting an infected enemy clears layers and hides the marker;
- a three-enemy propagation chain terminates and no enemy bursts more than once for the same direct application.

- [ ] **Step 2: Run the focused PlayMode tests and verify RED**

Use a unique test-name prefix `BiologicalInfection_` and:

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -runTests -testPlatform PlayMode \
  -testFilter "WasteCity.Tests.PlayMode.RuntimeSceneTests.BiologicalInfection_" \
  -testResults "/tmp/wastecity-infection-runtime-red.xml" \
  -logFile "/tmp/wastecity-infection-runtime-red.log"
```

Expected: compile failure because runtime infection APIs do not exist.

- [ ] **Step 3: Implement `EnemyInfectionStatus`**

Use one component per enemy:

```csharp
public sealed class EnemyInfectionStatus : MonoBehaviour
{
    public int Stacks => model.Stacks;
    public float Elapsed => model.Elapsed;
    public GameObject Marker { get; private set; }
    public void Configure(PlaceholderEnemy enemy, HealthComponent health);
    public void Apply(int stacks);
    public void Restore(int stacks, float elapsed);
    public void Clear();
}
```

Implementation requirements:

- create the marker lazily on first non-zero infection;
- attach `VisualSlot` stable ID `biological.status.infection`;
- update marker scale and alpha from literal stack count;
- tick infection damage with `health.Value.Apply(raw, DamageType.Biological, health.Armor)`;
- build real enemy candidates and call `InfectionSpreadRules`;
- pass one `HashSet<int>` through a propagation chain and allow each target ID to burst at most once;
- never spread to or tick a controlled enemy.

- [ ] **Step 4: Integrate the component with `PlaceholderEnemy`**

During `Configure`, get or add `EnemyInfectionStatus` and configure it after `HealthComponent.Configure`. During `TryConvert`, clear infection before adding `FriendlyUnitAgent`.

- [ ] **Step 5: Re-run focused PlayMode tests and verify GREEN**

Expected: all `BiologicalInfection_` runtime status tests pass with no unexpected Unity log messages.

- [ ] **Step 6: Commit runtime status**

```bash
git add Assets/_Game/Scripts/Combat/EnemyInfectionStatus.cs \
  Assets/_Game/Scripts/Combat/EnemyInfectionStatus.cs.meta \
  Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs \
  Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: add enemy infection runtime status"
```

---

### Task 4: Apply Infection from Spore Towers, Acid Towers, and Behemoths

**Files:**
- Modify: `Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- Modify: `Assets/_Game/Scripts/Combat/FriendlyUnitAgent.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Consumes: `InfectionEmitterModel.Tick` and `PlaceholderEnemy.ApplyInfection`.
- Maintains one emitter instance per `PlaceholderTurret` and one per `FriendlyUnitAgent`.

- [ ] **Step 1: Write failing source-integration tests**

Create real PlayMode objects and assert:

- a configured spore tower with one `BiologicalWeapon` damages a hostile and applies one infection layer;
- a configured acid tower does the same;
- a machine-gun turret damages but does not infect;
- a behemoth damages and infects a hostile;
- repeated continuous damage for less than one second does not add a second layer;
- damage after the one-second cadence adds the next layer;
- no ammunition means no damage and no infection layer.

Use real `FormalEconomyController`, `BuildingRuntime`, `PlaceholderTurret`, `FriendlyUnitAgent`, `PlaceholderEnemy`, and `HealthComponent` instances. Assert target state, not private emitter calls.

- [ ] **Step 2: Run focused PlayMode tests and verify RED**

Expected: biological sources deal damage but infection stack assertions fail at zero.

- [ ] **Step 3: Integrate tower infection**

In `PlaceholderTurret.Update`:

```csharp
bool biological = profile.DamageType == DamageType.Biological;
bool applyInfection = infectionEmitter.Tick(Time.deltaTime, biological && dealt > 0);
if (applyInfection) nearest.ApplyInfection(1);
```

Keep existing mind-control logic unchanged.

- [ ] **Step 4: Integrate behemoth infection**

In `FriendlyUnitAgent.Attack`, store the actual result from `HealthModel.Apply`. Feed `damageType == DamageType.Biological && dealt > 0` into the agent emitter and apply one layer only when it returns `true`.

- [ ] **Step 5: Re-run source tests and all infection tests**

Expected: spore, acid, and behemoth tests pass; physical source remains uninfected.

- [ ] **Step 6: Commit source integration**

```bash
git add Assets/_Game/Scripts/Building/BuildingRuntime.cs \
  Assets/_Game/Scripts/Combat/FriendlyUnitAgent.cs \
  Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: connect biological attacks to infection"
```

---

### Task 5: Schema 25 Infection Persistence

**Files:**
- Modify: `Assets/_Game/Scripts/Combat/FormalCombatController.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Extends `EnemySnapshot` with `int infectionStacks` and `float infectionElapsed`.
- Raises `FormalSaveData.schema` and codec maximum from `24` to `25`.
- Changes combat restoration to `void Restore(WaveDirectorSnapshot wave, EnemySnapshot[] enemies, int schema)` so migration behavior uses the loaded save's explicit schema.
- Restores infection after enemy configuration and after optional mind-control restoration.

- [ ] **Step 1: Write failing schema tests**

```csharp
[Test]
public void VersionTwentyFiveEnemyInfectionRoundTrips()
{
    var data = new FormalSaveData
    {
        enemies = new[]
        {
            new EnemySnapshot { infectionStacks = 7, infectionElapsed = .4f }
        }
    };
    var restored = FormalSaveCodec.Decode(FormalSaveCodec.Encode(data));
    Assert.That(restored.schema, Is.EqualTo(25));
    Assert.That(restored.enemies[0].infectionStacks, Is.EqualTo(7));
    Assert.That(restored.enemies[0].infectionElapsed, Is.EqualTo(.4f));
}
```

Add tests proving schema 24 JSON remains readable with zero infection and controlled snapshots restore without infection.

- [ ] **Step 2: Run focused save tests and verify RED**

Expected: compile failure for missing snapshot fields or assertion failure because schema remains 24.

- [ ] **Step 3: Add schema and snapshot fields**

Capture `enemy.Infection.Stacks` and `enemy.Infection.Elapsed`. During restore:

1. configure the enemy;
2. restore health and shield;
3. restore controlled state if requested;
4. only if schema 25 data represents a hostile enemy, call `RestoreInfection`.

Change `FormalSaveController.Apply` to call `combat.Restore(d.wave, d.enemies, d.schema)`. Do not infer schema from infection values.

- [ ] **Step 4: Add PlayMode save/restore behavior test**

Load `FormalPrototype`, infect one real hostile, capture the complete save, restore it, and assert the recreated hostile has the same layers and elapsed progress within `.001f`.

- [ ] **Step 5: Re-run save and infection suites**

Expected: all focused EditMode and PlayMode tests pass.

- [ ] **Step 6: Commit schema 25**

```bash
git add Assets/_Game/Scripts/Combat/FormalCombatController.cs \
  Assets/_Game/Scripts/Persistence/FormalSaveController.cs \
  Assets/_Game/Scripts/Persistence/FormalSaveData.cs \
  Assets/_Game/Tests/EditMode/FormalSaveTests.cs \
  Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: persist enemy infection status"
```

---

### Task 6: Full Verification, Windows Build, and Accurate Documentation

**Files:**
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `README.md`

**Interfaces:**
- Records exact code baseline hash, test totals, schema `25`, successful macOS-accessible gates, and pending real-Windows smoke.

- [ ] **Step 1: Run static checks**

```bash
git diff --check
git status --short
```

Expected: no whitespace errors and only infection-milestone files.

- [ ] **Step 2: Run all Unity EditMode tests**

Run without `-quit`, write XML to `/tmp/wastecity-editmode-results.xml`, and require `failed="0"`.

- [ ] **Step 3: Run all Unity PlayMode tests**

Run without `-quit`, write XML to `/tmp/wastecity-playmode-results.xml`, and require `failed="0"`.

- [ ] **Step 4: Run headless compile**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -logFile "/tmp/wastecity-infection-compile.log" -quit
```

Expected: exit code `0`, no `error CS`, no unhandled exception.

- [ ] **Step 5: Build Windows 64-bit player**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindows \
  -logFile "/tmp/wastecity-infection-windows-build.log" -quit
```

Expected: exit code `0`, `Build Finished, Result: Success`, and a PE32+ `Builds/Windows/WasteCity.exe`.

- [ ] **Step 6: Confirm the deferred smoke gate**

Verify no CrossOver/Wine dependency was installed and record:

```text
Windows 10/11 independent executable smoke: pending.
Reason: macOS development machine intentionally has no Windows compatibility layer.
Release effect: milestone may remain an internal development baseline but is not a candidate release.
```

- [ ] **Step 7: Commit feature code if Tasks 1-5 were not already committed**

Use only infection-related source and test paths. Do not stage `Builds/`, `Library/`, `Logs/`, `TestResults/`, local package paths, or license files.

- [ ] **Step 8: Update roadmap and README**

Record:

- infection/parasitism implemented;
- exact feature commit hash;
- exact EditMode and PlayMode totals from XML;
- schema `25`;
- Windows build success;
- Windows executable smoke still pending for a real Windows machine.

- [ ] **Step 9: Commit documentation**

```bash
git add Docs/05-Formal-Development-Roadmap-ZH.md README.md
git commit -m "docs: record biological infection baseline"
```

- [ ] **Step 10: Final inspection**

```bash
git status --short
git log -8 --oneline
git diff HEAD~1 --check
```

Expected: clean worktree, separate implementation and documentation commits, no generated artifacts tracked.
