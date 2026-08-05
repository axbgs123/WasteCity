# Camera Follow, Free Drag, and Quick Return Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved `IDEA-0001` camera milestone: exact following of the existing direct-control target, persistent free drag, same-frame `Home` return, and automatic reacquisition after target changes.

**Architecture:** Keep camera mode and effective-target transitions in a pure C# `CameraFollowModel`. Add a thin `FormalCameraController` that reads the existing `FormalLeaderController.ControlTarget`, translates existing Input System mouse/keyboard state, and moves only the X/Y coordinates of the existing orthographic `Main Camera`.

**Tech Stack:** Unity `2022.3.62f1`, C#, NUnit EditMode tests, Unity Test Framework PlayMode tests, Input System `1.7.0`, 2D placeholders, save schema `30`, Windows Mono x86-64 player build.

## Global Constraints

- Follow `Docs/superpowers/specs/2026-08-05-camera-control-design.md`.
- Work only on `codex/camera-control`, based on `7d6a502e35c3e8bde09d49fd3620b3a8f26c78f1`.
- Never run `FormalProjectSetup.Configure`; edit only the existing `Main Camera` YAML connection.
- Reuse `FormalLeaderController.ControlTarget`; do not duplicate or modify direct-control rules.
- Do not modify `PlaceholderMobileCity`, `FormalLeaderController`, `DirectControlRules`, the Building directory, build-menu behavior, or `BUG-0001`.
- Do not modify `Packages/manifest.json`, `Packages/packages-lock.json`, or `ProjectSettings/PackageManagerSettings.asset`.
- Do not add packages, art, Cinemachine, `.inputactions`, persistence fields, zoom, bounds, edge scrolling, smoothing, 3D, pause/speed changes, outposts, inner-city placement, or autopilot technology.
- Preserve schema `30`.
- Stage only the files named by the active task.

---

### Task 1: Pure camera-follow state model

**Files:**

- Create: `Assets/_Game/Scripts/World/CameraFollowModel.cs`
- Create: `Assets/_Game/Scripts/World/CameraFollowModel.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/CameraFollowModelTests.cs`
- Create: `Assets/_Game/Tests/EditMode/CameraFollowModelTests.cs.meta`

- [x] **Step 1: Write failing model tests**

Cover:

```text
new model => Following + City
BeginFreeDrag => Free
EndFreeDrag => remains Free
ReturnToTarget => Following
City request => City
Leader request + available => Leader
Leader request + unavailable => City
effective target change while Free => Following
same effective target while Free => remains Free
```

- [x] **Step 2: Run focused EditMode test and confirm RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-camera-control" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.CameraFollowModelTests" \
  -testResults "/tmp/wastecity-camera-control/red-model.xml" \
  -logFile "/tmp/wastecity-camera-control/red-model.log"
```

Expected RED: compilation fails only because `CameraFollowModel` / `CameraFollowMode` do not exist.

- [x] **Step 3: Implement the minimum pure model**

Implement only the approved transitions. `ObserveTarget` resolves a requested leader to city when `leaderTargetAvailable == false`, returns whether the effective target changed, and restores `Following` only when it changed.

- [x] **Step 4: Run focused test and confirm GREEN**

Repeat Step 2 with `green-model.xml` / `green-model.log`; require all selected tests to pass.

- [x] **Step 5: Verify and commit**

```bash
git diff --check
git status --short
git add \
  Assets/_Game/Scripts/World/CameraFollowModel.cs \
  Assets/_Game/Scripts/World/CameraFollowModel.cs.meta \
  Assets/_Game/Tests/EditMode/CameraFollowModelTests.cs \
  Assets/_Game/Tests/EditMode/CameraFollowModelTests.cs.meta
git diff --cached --name-only
git commit -m "feat: add camera follow state rules"
```

---

### Task 2: Thin Unity camera adapter

**Files:**

- Create: `Assets/_Game/Scripts/World/FormalCameraController.cs`
- Create: `Assets/_Game/Scripts/World/FormalCameraController.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FormalCameraControllerTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FormalCameraControllerTests.cs.meta`

- [x] **Step 1: Write failing adapter tests**

Cover:

- following copies target X/Y and preserves camera Z;
- free drag applies the inverse screen delta using orthographic world units per pixel;
- free drag preserves Z;
- `Time.timeScale == 0` does not block drag or `ReturnToTarget`;
- `ReturnToTarget` snaps in the same call;
- missing leader controller or target safely follows the city;
- a city-to-leader effective target switch exits `Free` and snaps to the leader.

- [x] **Step 2: Run focused EditMode test and confirm RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-camera-control" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.FormalCameraControllerTests" \
  -testResults "/tmp/wastecity-camera-control/red-adapter.xml" \
  -logFile "/tmp/wastecity-camera-control/red-adapter.log"
```

Expected RED: compilation fails only because `FormalCameraController` does not exist.

- [x] **Step 3: Implement the minimum adapter**

Add:

```csharp
public CameraFollowMode Mode { get; }
public DirectControlTarget CurrentTarget { get; }
public bool ReferencesReady { get; }
public void Configure(
    Camera camera,
    PlaceholderMobileCity cityController,
    FormalLeaderController leaderController,
    Transform leaderFollowTarget);
public void BeginFreeDrag();
public void EndFreeDrag();
public void ProcessPointerState(
    Vector2 screenPosition,
    bool pressedThisFrame,
    bool releasedThisFrame,
    float screenHeight);
public void ApplyPointerDelta(Vector2 screenDelta, float screenHeight);
public void ReturnToTarget();
public void TickCamera();
```

`Update` translates `Mouse.current.middleButton`, pointer-position snapshots, and `Keyboard.current.homeKey`. Press establishes a position baseline; hold and release apply position differences so pre-press movement is ignored and the final pre-release segment is retained. `LateUpdate` calls `TickCamera`. Do not read scaled or unscaled delta time.

- [x] **Step 4: Run focused test and confirm GREEN**

Repeat Step 2 with `green-adapter.xml` / `green-adapter.log`; require all selected tests to pass.

---

### Task 3: Formal scene connection and runtime contracts

**Files:**

- Modify: `Assets/_Game/Scenes/FormalPrototype.unity`
- Modify: `Assets/_Game/Tests/EditMode/SceneContractTests.cs`
- Modify: `Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs`

- [x] **Step 1: Write failing scene-contract and PlayMode tests**

Before editing the scene, add tests for:

- `FormalCameraController` exists in `FormalPrototype`;
- `ReferencesReady` is true;
- the controller is attached to `Camera.main`;
- `Mobile` follows the city;
- `Fortress + recruited` follows the leader;
- ending free drag remains free and does not snap;
- `Home` behavior returns and snaps;
- target switching while free returns and snaps;
- camera operations preserve city autopilot and deployment state.

- [x] **Step 2: Run focused tests and confirm RED**

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-camera-control" \
  -runTests -testPlatform EditMode \
  -testFilter "WasteCity.Tests.SceneContractTests" \
  -testResults "/tmp/wastecity-camera-control/red-scene-editmode.xml" \
  -logFile "/tmp/wastecity-camera-control/red-scene-editmode.log"

"/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/baiyan1/Documents/WasteCity-camera-control" \
  -runTests -testPlatform PlayMode \
  -testFilter "WasteCity.Tests.PlayMode.RuntimeSceneTests" \
  -testResults "/tmp/wastecity-camera-control/red-scene-playmode.xml" \
  -logFile "/tmp/wastecity-camera-control/red-scene-playmode.log"
```

Expected RED: scene contract fails because the formal `Main Camera` has no adapter. Runtime camera assertions fail for the same missing connection.

- [x] **Step 3: Connect only the existing Main Camera**

Edit `FormalPrototype.unity` YAML to add `FormalCameraController` to the existing `Main Camera` and assign the existing Camera, city, leader controller, and leader visual Transform file IDs. Do not run project setup or rewrite unrelated scene YAML.

- [x] **Step 4: Run focused scene tests and confirm GREEN**

Repeat Step 2 with `green-scene-editmode.xml` and `green-scene-playmode.xml`; require all selected tests to pass.

- [x] **Step 5: Verify and commit adapter/scene**

```bash
git diff --check
git status --short
git add \
  Assets/_Game/Scripts/World/FormalCameraController.cs \
  Assets/_Game/Scripts/World/FormalCameraController.cs.meta \
  Assets/_Game/Tests/EditMode/FormalCameraControllerTests.cs \
  Assets/_Game/Tests/EditMode/FormalCameraControllerTests.cs.meta \
  Assets/_Game/Scenes/FormalPrototype.unity \
  Assets/_Game/Tests/EditMode/SceneContractTests.cs \
  Assets/_Game/Tests/PlayMode/RuntimeSceneTests.cs
git diff --cached --name-only
git commit -m "feat: wire formal camera control"
```

---

### Task 4: Full verification and controlled documentation backwrite

**Files:**

- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify: `Docs/superpowers/plans/2026-08-05-camera-control.md` (checkbox evidence only)

- [x] **Step 1: Run full EditMode**

Write results to:

```text
/tmp/wastecity-camera-control/final-editmode.xml
/tmp/wastecity-camera-control/final-editmode.log
```

- [x] **Step 2: Run full PlayMode**

Write results to:

```text
/tmp/wastecity-camera-control/final-playmode.xml
/tmp/wastecity-camera-control/final-playmode.log
```

- [x] **Step 3: Run headless compile and Windows x86-64 build**

Use the repository's documented compile/build method. Write logs to:

```text
/tmp/wastecity-camera-control/final-compile.log
/tmp/wastecity-camera-control/final-windows-build.log
```

Require a successful Mono x86-64 player build, then run:

```bash
file Builds/Windows/WasteCity.exe
```

- [x] **Step 4: Prove protected areas are unchanged**

Compare against `7d6a502e35c3e8bde09d49fd3620b3a8f26c78f1`:

```bash
git diff --exit-code 7d6a502 -- Assets/_Game/Scripts/Building
git diff --exit-code 7d6a502 -- Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs
git diff --exit-code 7d6a502 -- \
  Packages/manifest.json \
  Packages/packages-lock.json \
  ProjectSettings/PackageManagerSettings.asset
```

Extract the complete `### BUG-0001` section from the baseline and worktree, hash both, and require identical SHA-256 values.

- [x] **Step 5: Backwrite only verified evidence**

Update:

- `Docs/05` with the exact implementation commit, exact final test totals, compile/build evidence, milestone progress, and real Windows smoke still pending;
- `Docs/06` `IDEA-0001` with the implementation commit and exact verified evidence while preserving overall status `开发中`;
- no byte in the `BUG-0001` section.

- [x] **Step 6: Verify and commit documentation**

```bash
git diff --check
git status --short
git add \
  Docs/05-Formal-Development-Roadmap-ZH.md \
  Docs/06-User-Feedback-and-Change-Control-ZH.md \
  Docs/superpowers/plans/2026-08-05-camera-control.md
git diff --cached --name-only
git commit -m "docs: record camera control milestone"
```

---

### Task 5: Push and final consistency proof

- [x] **Step 1: Final clean verification**

```bash
git diff --check
git status --short
git log --oneline 7d6a502..HEAD
```

- [x] **Step 2: Push only the feature branch**

```bash
git push -u origin codex/camera-control
```

- [x] **Step 3: Prove local/remote identity**

```bash
git rev-parse HEAD
git rev-parse origin/codex/camera-control
git status --short
```

Do not merge, create a PR, force-push, or mark real Windows 10/11 standalone smoke as complete.
