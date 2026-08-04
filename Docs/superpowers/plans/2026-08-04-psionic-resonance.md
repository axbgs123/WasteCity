# Psionic Resonance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the GDD psionic resonance loop, the single-city collective-consciousness research equivalent, stable placeholder feedback, and schema 28 persistence.

**Architecture:** Keep timing and percentage calculations in pure C# models. Add one enemy status adapter that owns the visual marker and finds other active enemy statuses only when primary psionic damage is dealt. Extend the existing research start API with an optional inherited-progress ratio and derive the collective-consciousness effect from completed research.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Input System `1.7.0`, URP 2D programmatic placeholders, Windows Mono player build.

## Global Constraints

- Follow `Docs/01-Game-Design-Document-ZH.md`, `Docs/05-Formal-Development-Roadmap-ZH.md`, and `Docs/superpowers/specs/2026-08-04-psionic-resonance-collective-consciousness-design.md`.
- Resonance duration is exactly `5` seconds, the global mark cap is `10`, and synchronized raw damage is `30%` of primary applied damage with floor `1`.
- Synchronized damage remains `DamageType.Psionic`, uses recipient armor and shield, and never recursively propagates.
- Collective consciousness gives newly started research exactly `20%` inherited progress; costs remain unchanged.
- Save schema becomes `28`; schema `27` and older default enemy resonance to zero.
- New visuals are programmatic placeholders connected through stable `VisualSlot` ID `psionics.status.resonance`.
- Do not add consciousness-network buildings, cross-route bridge content, formal art, or multi-city systems in this milestone.

---

### Task 1: Pure Resonance Timing and Damage Rules

**Files:**
- Create: `Assets/_Game/Scripts/Combat/PsionicResonanceModel.cs`
- Create: `Assets/_Game/Scripts/Combat/PsionicResonanceModel.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/PsionicResonanceTests.cs`
- Create: `Assets/_Game/Tests/EditMode/PsionicResonanceTests.cs.meta`

**Interfaces:**
- Produces: `PsionicResonanceModel` with `Remaining`, `Active`, `Apply()`, `Tick(float)`, `Clear()`, and `Restore(float)`.
- Produces: `PsionicResonanceRules.CanMark(bool alreadyMarked, int activeMarkerCount)` and `SynchronizedRawDamage(int primaryAppliedDamage)`.

- [ ] **Step 1: Write failing model tests**

Add NUnit tests proving:

```csharp
var model = new PsionicResonanceModel();
model.Apply();
Assert.That(model.Remaining, Is.EqualTo(5f));
model.Tick(2f);
model.Apply();
Assert.That(model.Remaining, Is.EqualTo(5f));
model.Tick(5f);
Assert.That(model.Active, Is.False);

Assert.That(PsionicResonanceRules.CanMark(false, 9), Is.True);
Assert.That(PsionicResonanceRules.CanMark(false, 10), Is.False);
Assert.That(PsionicResonanceRules.CanMark(true, 10), Is.True);
Assert.That(PsionicResonanceRules.SynchronizedRawDamage(10), Is.EqualTo(3));
Assert.That(PsionicResonanceRules.SynchronizedRawDamage(1), Is.EqualTo(1));
Assert.That(PsionicResonanceRules.SynchronizedRawDamage(0), Is.Zero);
```

Also assert negative Tick values are ignored and Restore clamps into `0..5`.

- [ ] **Step 2: Run focused EditMode tests to verify RED**

Run the `PsionicResonanceTests` fixture.

Expected: compilation fails because both production types are missing.

- [ ] **Step 3: Implement the pure types**

Implement constants `DurationSeconds=5f`, `MaximumTargets=10`, and `SynchronizedPercent=30`. Use `Math.Max`, `Math.Min`, integer multiplication and division so the calculations remain deterministic.

- [ ] **Step 4: Run focused EditMode tests to verify GREEN**

Expected: all resonance pure-rule tests pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Combat/PsionicResonanceModel.cs Assets/_Game/Scripts/Combat/PsionicResonanceModel.cs.meta Assets/_Game/Tests/EditMode/PsionicResonanceTests.cs Assets/_Game/Tests/EditMode/PsionicResonanceTests.cs.meta
git commit -m "feat: define psionic resonance rules"
```

### Task 2: Collective Consciousness Research Inheritance

**Files:**
- Modify: `Assets/_Game/Scripts/Research/ResearchModel.cs`
- Modify: `Assets/_Game/Scripts/Research/ResearchController.cs`
- Modify: `Assets/_Game/Tests/EditMode/ResearchTests.cs`

**Interfaces:**
- Produces: `CollectiveConsciousnessRules.InheritedProgressRatio(bool unlocked)`.
- Changes: `ResearchModel.Start(ResearchDefinition definition, ResourceInventory inventory, float inheritedProgressRatio = 0f)`.
- Consumes: completed research ID `core.research.collective-consciousness`.

- [ ] **Step 1: Write failing research tests**

Add tests that restore collective consciousness as completed, start a `60` second research with ratio `0.2`, and assert `Remaining == 48`. Assert the full resource cost is spent, ratio `0` leaves `60`, ratios below zero clamp to zero, and ratios at or above one leave the minimum `.001` seconds rather than completing immediately.

- [ ] **Step 2: Run focused tests to verify RED**

Run `ResearchTests`.

Expected: the three-argument `Start` and rules type are absent.

- [ ] **Step 3: Implement inheritance**

Add:

```csharp
public static class CollectiveConsciousnessRules
{
    public const float SharedProgressRatio = .2f;
    public static float InheritedProgressRatio(bool unlocked) =>
        unlocked ? SharedProgressRatio : 0f;
}
```

After normal prerequisite and inventory checks, initialize:

```csharp
float ratio = Math.Max(0f, Math.Min(.999f, inheritedProgressRatio));
Remaining = Math.Max(.001f, definition.Duration * (1f - ratio));
```

Expose `ResearchController.HasCollectiveConsciousness` and pass the derived ratio from the keyboard research-start path. Extend the research UI status with `集体意识：新研究继承20%进度` when unlocked.

- [ ] **Step 4: Run focused tests to verify GREEN**

Expected: all `ResearchTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Research/ResearchModel.cs Assets/_Game/Scripts/Research/ResearchController.cs Assets/_Game/Tests/EditMode/ResearchTests.cs
git commit -m "feat: add collective research inheritance"
```

### Task 3: Runtime Enemy Resonance and Placeholder Feedback

**Files:**
- Create: `Assets/_Game/Scripts/Combat/EnemyPsionicResonanceStatus.cs`
- Create: `Assets/_Game/Scripts/Combat/EnemyPsionicResonanceStatus.cs.meta`
- Modify: `Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs`
- Modify: `Assets/_Game/Scripts/Building/BuildingRuntime.cs`
- Modify: `Assets/_Game/Scripts/Combat/FriendlyUnitAgent.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Produces: `EnemyPsionicResonanceStatus.Remaining`, `Active`, `Marker`, `ReceivePrimaryHit(int)`, `Restore(float)`, and `Clear()`.
- Produces: `PlaceholderEnemy.PsionicResonance`, `ApplyPsionicResonance(int)`, `RestorePsionicResonance(float)`, and `ClearPsionicResonance()`.
- Consumes: `PsionicResonanceRules` from Task 1.

- [ ] **Step 1: Write failing PlayMode tests**

Add scene tests proving:

- a mind spire with psionic amplifiers damages and marks its target;
- the marker uses `psionics.status.resonance`;
- a physical tower does not mark;
- two manually marked hostiles cause the secondary target to take synchronized damage after a primary psionic hit;
- synchronized damage does not recursively damage the first target;
- mind control clears the state and disables its marker;
- eleven attempted marks leave exactly ten active statuses.

- [ ] **Step 2: Run the runtime fixture to verify RED**

Expected: missing resonance component and enemy APIs.

- [ ] **Step 3: Implement enemy status**

Create a component holding `PsionicResonanceModel`. `ReceivePrimaryHit` must:

1. reject dead or controlled owners and non-positive primary damage;
2. count active hostile `EnemyPsionicResonanceStatus` components;
3. apply or refresh only when `CanMark` allows it;
4. synchronize `PsionicResonanceRules.SynchronizedRawDamage(primaryDamage)` to other active hostile status owners using `Health.Value.Apply(raw, DamageType.Psionic, Health.Armor)`;
5. never call another status's `ReceivePrimaryHit`.

Create a purple/cyan programmatic marker and attach `VisualSlot` ID `psionics.status.resonance`. Tick the timer in `Update`; clear on expiry.

- [ ] **Step 4: Connect damage sources**

After `PlaceholderTurret` obtains positive `dealt`, call `nearest.ApplyPsionicResonance(dealt)` only when the tower profile damage type is psionic. Do the same in `FriendlyUnitAgent.Attack` for controlled units with psionic damage.

Configure the status in `PlaceholderEnemy.Configure`; clear it in `OnDied` and before `TryConvert` sets `IsControlled`.

- [ ] **Step 5: Run runtime tests to verify GREEN**

Run `RuntimeSceneTests`.

Expected: all existing and new PlayMode tests pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Combat/EnemyPsionicResonanceStatus.cs Assets/_Game/Scripts/Combat/EnemyPsionicResonanceStatus.cs.meta Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs Assets/_Game/Scripts/Building/BuildingRuntime.cs Assets/_Game/Scripts/Combat/FriendlyUnitAgent.cs Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: connect psionic resonance combat"
```

### Task 4: Schema 28 Persistence

**Files:**
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Modify: `Assets/_Game/Scripts/Combat/FormalCombatController.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Interfaces:**
- Produces: `EnemySnapshot.psionicResonanceRemaining`.
- Changes: current and maximum accepted save schema to `28`.
- Consumes: runtime status restore API from Task 3.

- [ ] **Step 1: Write failing save tests**

Update the current-schema assertion to `28`. Add codec tests proving schema 28 round-trips `psionicResonanceRemaining=3.5` and schema 27 JSON defaults the field to zero.

- [ ] **Step 2: Write failing scene restore tests**

Add PlayMode tests proving:

- schema 28 restores and captures hostile resonance remaining time;
- schema 27 ignores an injected resonance value;
- controlled snapshots restore without resonance.

- [ ] **Step 3: Run focused tests to verify RED**

Expected: snapshot field/current schema failures.

- [ ] **Step 4: Implement schema 28**

Add the snapshot field, capture current remaining time, and restore it only for schema 28 and non-controlled enemies. Keep all schema 27 cultivation defaults intact.

- [ ] **Step 5: Run focused tests to verify GREEN**

Run `FormalSaveTests` and `RuntimeSceneTests`.

Expected: all save and runtime tests pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Persistence/FormalSaveData.cs Assets/_Game/Scripts/Combat/FormalCombatController.cs Assets/_Game/Tests/EditMode/FormalSaveTests.cs Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git commit -m "feat: persist psionic resonance state"
```

### Task 5: Full Verification and Baseline

**Files:**
- Modify: `README.md`
- Modify: `Docs/00-README-ZH.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`

**Interfaces:**
- Consumes: test counts, build result, schema number, and feature commit produced by Tasks 1-4.
- Produces: an accurate psionic-route baseline while leaving the missing-buildings milestone as the next task.

- [ ] **Step 1: Run static checks**

Run `git diff --check` and inspect `git status --short`.

- [ ] **Step 2: Run all EditMode tests**

Use Unity batch mode without `-quit` so the test runner writes XML. Record total/passed/failed.

- [ ] **Step 3: Run all PlayMode tests**

Use Unity batch mode without `-quit`. Record total/passed/failed.

- [ ] **Step 4: Run headless compile**

Run Unity batch mode with `-quit` and confirm the log contains no C# compiler errors.

- [ ] **Step 5: Build Windows 64-bit**

Execute `WasteCity.Editor.FormalBuildTools.BuildWindows`; require exit code zero.

- [ ] **Step 6: Restore portable packages**

If `com.unity.inputsystem` temporarily uses a local cache path, restore manifest version `1.7.0`, lock source `registry`, and URL `https://packages.unity.com`.

- [ ] **Step 7: Update and commit baseline docs**

Record schema `28`, exact test totals, psionic implementation commit, successful compile/build, and the still-pending real Windows smoke test.

```bash
git add README.md Docs/00-README-ZH.md Docs/05-Formal-Development-Roadmap-ZH.md
git commit -m "docs: record psionic route baseline"
```

- [ ] **Step 8: Final inspection**

Require a clean worktree and inspect recent commits before starting the separate missing-route-buildings design.
