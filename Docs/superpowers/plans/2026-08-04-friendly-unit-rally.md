# Unified Friendly Unit Rally Implementation Plan

> **For Codex:** Implement this plan task-by-task with strict red-green-refactor TDD. Keep the previously completed foundation fixes in a separate commit.

**Goal:** Make puppets, behemoths, and mind-controlled enemies obey one rally point, tactical targeting policy, regeneration rule, and loss counter while retaining replaceable placeholder visuals.

**Architecture:** Add pure C# command and tactical rule models, then connect all three runtime unit types through a shared `FriendlyUnitAgent`. Keep existing unit identity components as compatibility adapters, and persist the command state in save schema 24.

**Tech Stack:** Unity 2022.3.62f1, C#, Unity Input System 1.7.0, NUnit EditMode/PlayMode tests.

---

## Task 0: Preserve the completed foundation milestone

**Files:**
- Commit the currently modified foundation scripts, settings, and tests already present in the worktree.

**Step 1: Verify the existing evidence**

Confirm the prior test reports still show 192 EditMode and 7 PlayMode passes, and run:

```bash
git diff --check
git status --short
```

**Step 2: Commit only the foundation files**

```bash
git add Assets/_Game/Scripts Assets/_Game/Tests ProjectSettings/ProjectSettings.asset
git commit -m "fix: stabilize formal prototype foundation"
```

## Task 1: Define command state and loss statistics

**Files:**
- Create: `Assets/_Game/Scripts/Combat/FriendlyUnitCommandModel.cs`
- Create: `Assets/_Game/Scripts/Combat/FriendlyUnitCommandModel.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FriendlyUnitCommandTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FriendlyUnitCommandTests.cs.meta`

**Step 1: Write failing tests**

Cover:

- default mode follows the supplied city position;
- setting a rally point makes it fixed;
- clearing the point resumes following the city;
- losses increment independently for puppet, behemoth, and controlled units;
- restore clamps negative loss values to zero.

**Step 2: Run the focused EditMode test and confirm RED**

Run Unity EditMode tests filtered to `FriendlyUnitCommandTests`. Confirm failure is caused by the missing model/API.

**Step 3: Implement the minimal model**

Add `FriendlyUnitKind`, fixed point state, rally resolution, loss recording, and restore/capture-facing properties.

**Step 4: Run the focused test and confirm GREEN**

Re-run the same filter and confirm all command tests pass.

## Task 2: Define tactical targeting and return behavior

**Files:**
- Create: `Assets/_Game/Scripts/Combat/FriendlyUnitTacticalRules.cs`
- Create: `Assets/_Game/Scripts/Combat/FriendlyUnitTacticalRules.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FriendlyUnitTacticalRulesTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FriendlyUnitTacticalRulesTests.cs.meta`

**Step 1: Write failing tests**

Cover:

- nearest living hostile inside the 9-unit guard radius is selected;
- dead and friendly candidates are ignored;
- an existing target remains valid inside the 13-unit leash;
- a target outside the leash is dropped;
- no target outside rally tolerance returns `ReturnToRally`;
- no target inside tolerance returns `Hold`;
- a target inside attack range returns `Attack`;
- a target outside attack range returns `Chase`.

Expected values must be literal and independently derived.

**Step 2: Run the focused EditMode test and confirm RED**

Confirm the test fails because the tactical rule types do not exist.

**Step 3: Implement minimal pure rules**

Use small immutable candidate/profile inputs without Unity scene lookup. Return a decision enum plus selected candidate ID.

**Step 4: Run the focused test and confirm GREEN**

Re-run the filter and keep all existing EditMode tests green.

## Task 3: Introduce the shared runtime agent

**Files:**
- Create: `Assets/_Game/Scripts/Combat/FriendlyUnitAgent.cs`
- Create: `Assets/_Game/Scripts/Combat/FriendlyUnitAgent.cs.meta`
- Modify: `Assets/_Game/Scripts/Combat/PlaceholderPuppet.cs`
- Modify: `Assets/_Game/Scripts/Combat/PlaceholderBehemoth.cs`
- Modify: `Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs`
- Modify: `Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Step 1: Write a failing PlayMode test for shared behavior**

Create real GameObjects and prove:

- a puppet and behemoth use the controller rally point;
- a converted ordinary enemy receives a shared agent;
- all three return toward the same rally point when no hostile is present;
- all three damage a nearby hostile using their configured damage type;
- tissue regeneration heals all three;
- a unit death increments the matching loss counter once.

Split behaviors into focused tests where one test would otherwise have multiple failure reasons.

**Step 2: Run the focused PlayMode tests and confirm RED**

Confirm failures are due to absent shared-agent behavior.

**Step 3: Implement the minimal runtime adapter**

- Move duplicated movement, target selection, attacking, regeneration, and death reporting into `FriendlyUnitAgent`.
- Let puppet and behemoth identity components configure the shared agent.
- On conversion, configure the same agent on `PlaceholderEnemy`.
- Make hostile enemies scan `FriendlyUnitAgent` instances rather than hard-coded friendly classes.
- Preserve existing public health accessors and combat numbers.

**Step 4: Run focused PlayMode tests and confirm GREEN**

Then run all EditMode and PlayMode tests to catch compatibility regressions.

## Task 4: Add player rally input and placeholder marker

**Files:**
- Modify: `Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs`
- Modify: `Assets/_Game/Scripts/UI/FormalTitleMenuController.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

**Step 1: Write a failing PlayMode test**

Exercise controller methods directly to prove:

- setting a fixed point moves/shows the marker;
- clearing the point hides fixed-state presentation and resolves rally from the city;
- marker owns `VisualSlot` stable ID `core.command.rally-point`.

**Step 2: Run focused test and confirm RED**

**Step 3: Implement minimal input and presentation**

- `Y` at mouse world position sets the point.
- `Shift+Y` clears it.
- Add a small programmatic flag/diamond marker with a replaceable `VisualSlot`.
- Extend HUD/help with the active rally mode and loss totals.

**Step 4: Run focused test and confirm GREEN**

## Task 5: Persist schema 24

**Files:**
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveData.cs`
- Modify: `Assets/_Game/Scripts/Persistence/FormalSaveController.cs`
- Modify: `Assets/_Game/Tests/EditMode/FormalSaveTests.cs`

**Step 1: Write failing save tests**

Cover:

- schema 24 round-trips fixed rally coordinates and three loss counts;
- schema 23 loads with follow-city mode and zero losses;
- controlled enemy state round-trips without being reported as a new defeat.

**Step 2: Run focused EditMode tests and confirm RED**

**Step 3: Implement minimal persistence**

- Bump default schema to 24.
- Add rally and loss fields.
- Add a controlled flag to enemy snapshots.
- Restore controller state before restoring friendly/controlled unit behavior.
- Keep schema 23 defaults backward compatible.

**Step 4: Run focused tests and confirm GREEN**

## Task 6: Wire project setup and update roadmap

**Files:**
- Modify: `Assets/_Game/Editor/FormalProjectSetup.cs`
- Modify if serialization requires it: `Assets/_Game/Scenes/FormalPrototype.unity`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `README.md`

**Step 1: Verify scene wiring**

Open/regenerate the formal prototype through the editor setup path and confirm the controller references remain assigned.

**Step 2: Update documentation**

Record:

- unified friendly rules and rally point completed;
- actual EditMode/PlayMode totals;
- schema 24;
- new baseline commit placeholder until final commit;
- next roadmap item is blood infection/parasitism.

## Task 7: Full quality gate

**Step 1: Static checks**

```bash
git diff --check
git status --short
```

**Step 2: Unity tests**

Run all EditMode and PlayMode tests and inspect XML totals and failures.

**Step 3: Compile**

Run Unity in batch mode without tests and require exit code 0 with no compiler errors.

**Step 4: Windows build**

Build the Windows 64-bit player to the ignored build directory and require success.

**Step 5: Smoke test**

Launch the Windows build through the established local smoke path for at least 12 seconds. Require zero exceptions and zero errors in the player log.

**Step 6: Final inspection and commit**

Review the complete diff for scope, verify no generated artifacts or local package paths are tracked, then:

```bash
git add Assets/_Game Docs/05-Formal-Development-Roadmap-ZH.md README.md
git commit -m "feat: unify friendly unit rally behavior"
```
