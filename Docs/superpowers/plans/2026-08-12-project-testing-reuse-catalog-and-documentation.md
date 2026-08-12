# Project Testing, Reuse Catalog, and Documentation Automation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a repository-local quality system that automatically inventories tests, components, scenes, UI, and reusable project assets; gives plain-Chinese guidance; and turns a failed Unity test into a focused Bug-location report without changing gameplay.

**Architecture:** A machine-readable catalog stores relationships that source scanning cannot infer, while Editor-only scanners discover current files, types, scenes, assemblies, and tests. Deterministic generators write technical appendices only under `Docs/Generated`; a separate read-only validator detects drift, and a NUnit XML analyzer maps failures to feature groups, source paths, scenes, controlled requirement IDs, and focused rerun commands. Human-facing documents remain ordinary Chinese and semantic status changes remain manual.

**Tech Stack:** Unity `2022.3.62f1`, C# Editor assembly, Unity Test Framework/NUnit, `System.Xml.Linq`, Unity `JsonUtility`, `AssetDatabase`, `TypeCache`, `EditorBuildSettings`, Git, Markdown, JSON.

## Global Constraints

- Controlled requirement: `DOC-0001`; read `Docs/06-User-Feedback-and-Change-Control-ZH.md` before every task.
- Default scene remains `Assets/_Game/Scenes/GrayboxPrototype3D.unity`; frozen 2D regression scene remains `Assets/_Game/Scenes/FormalPrototype.unity`.
- Do not change gameplay, scene contents, save schema `30`, packages, rendering settings, or art import settings.
- The three human-facing guides use ordinary Chinese. Explain purpose first; show code/type names only as secondary references.
- Automated tools may update objective inventory and verification facts only. They may not change GDD rules, approval state, implementation state, completion percentages, or human-playtest conclusions.
- `GenerateDocumentation` may write only the four approved files under `Docs/Generated`; `ValidateDocumentation` must be read-only.
- Stable inventory files must not contain current time or automatically read the current Git SHA. `Latest-Verification-ZH.md` may contain only a caller-supplied verified SHA, timestamp, and result paths.
- All production C# files and test classes must map to at least one feature group or an explicit exclusion with a non-empty reason.
- Discovery inventory and recommended reuse catalog are separate. Existing/found does not imply recommended for reuse.
- Frozen 2D and `Placeholder*` entries cannot be marked as recommended foundations for new 3D work.
- Test-result analysis is advisory: it must preserve the original NUnit message and stack trace and must not claim the root cause is known.
- All new public Editor APIs need success, invalid-input, determinism, and no-unapproved-write tests.
- All Unity `-runTests` commands omit `-quit`; compile-only commands may use `-quit`.
- Use `/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity` exactly.
- Use `apply_patch` for authored file changes and exact-path `git add`; never use `git add .` or `git add -A`.
- Preserve the pre-existing 28 modified terrain texture `.meta` files and the untracked `ProjectSettings/PackageManagerSettings.asset` and `ProjectSettings/URPProjectSettings.asset`. Record their SHA-256 values before Task 1 and prove they are unchanged before every batch commit.
- Do not stage, clean, move, normalize, or overwrite those protected files.
- `README.md`, `AGENTS.md`, `Docs/README.md`, `Docs/05`, and `Docs/06` are modified only in the tasks that explicitly list them.
- Do not create a pull request, merge, force-push, or publish a release.

---

## File and Assembly Map

### New Editor quality subsystem

| File | Responsibility |
|---|---|
| `Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs` | Serializable catalog, scan snapshot, issue, verification, and report value types only |
| `Assets/_Game/Editor/ProjectQuality/ProjectQualityCatalogLoader.cs` | Load JSON, normalize slash paths, reject malformed or duplicate records |
| `Assets/_Game/Editor/ProjectQuality/ProjectQualityScanner.cs` | Discover files, assemblies, scenes, MonoBehaviours, ScriptableObjects, Editor entry points, and test classes |
| `Assets/_Game/Editor/ProjectQuality/ProjectQualityValidator.cs` | Read-only relationship, coverage, reuse-level, scene, UI, link, and stale-output checks |
| `Assets/_Game/Editor/ProjectQuality/ProjectDocumentationGenerator.cs` | Deterministically render the four approved generated Markdown files |
| `Assets/_Game/Editor/ProjectQuality/ProjectTestResultAnalyzer.cs` | Parse NUnit XML and produce plain-Chinese failure-location reports |
| `Assets/_Game/Editor/ProjectQuality/ProjectQualityTools.cs` | Public menu/batch entry points and environment-variable boundary |

All seven files compile into the existing `WasteCity.Editor` assembly. Do not create a new asmdef and do not add runtime assembly references.

### Machine-maintained data and output

| File | Responsibility |
|---|---|
| `Docs/Engineering/project-quality-catalog.json` | Human-reviewed relationships, reuse decisions, minimum verification gates, and document-attention rules |
| `Docs/Generated/Project-Inventory-ZH.md` | Generated assemblies, files, types, scenes, UI, tools, and art-integration inventory |
| `Docs/Generated/Test-Inventory-ZH.md` | Generated feature-group/test-class/test-filter inventory |
| `Docs/Generated/Latest-Verification-ZH.md` | Generated from explicitly supplied verification inputs |
| `Docs/Generated/Documentation-Attention-ZH.md` | Generated list of formal documents that a change set requires a human to inspect |

### Human-facing documents

| File | Responsibility |
|---|---|
| `Docs/07-Project-Use-and-Development-Guide-ZH.md` | Plain-Chinese project operation and development entry point |
| `Docs/08-Testing-and-Bug-Location-Guide-ZH.md` | Plain-Chinese test selection, result reading, playtest capture, and Bug workflow |
| `Docs/09-Reusable-Project-Catalog-ZH.md` | Plain-Chinese curated reuse catalog and prohibited-reuse warnings |

### New tests

| File | Responsibility |
|---|---|
| `Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs` | Catalog parsing, duplicate IDs, field validity, and complete current mappings |
| `Assets/_Game/Tests/EditMode/ProjectQualityScannerTests.cs` | Deterministic discovery of source, tests, assemblies, types, scenes, UI, and Editor entry points |
| `Assets/_Game/Tests/EditMode/ProjectDocumentationGeneratorTests.cs` | Deterministic generation, path confinement, stable files, and verification snapshots |
| `Assets/_Game/Tests/EditMode/ProjectQualityValidatorTests.cs` | Drift, unknown files/tests, reuse restrictions, links, scenes, and read-only validation |
| `Assets/_Game/Tests/EditMode/ProjectTestResultAnalyzerTests.cs` | NUnit XML mapping, unknown tests, incomplete results, original-message preservation, and rerun command output |
| `Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs` | Current repository catalog and generated outputs form one valid, up-to-date contract |

Each new `.cs` file has a matching Unity `.meta` file. New folders `Assets/_Game/Editor/ProjectQuality` and `Docs/Generated` also receive Unity `.meta` only when they live under `Assets`; `Docs` folders do not use Unity `.meta`.

For every focused Unity run below, use the exact Task 1 command template: `UNITY_BIN` and `PROJECT_ROOT` remain fixed, `-runTests` never includes `-quit`, `-testPlatform EditMode` is explicit, `-testFilter` is the fully qualified class list printed by the task, and both `-testResults` and `-logFile` point to that task's `/tmp/wastecity-project-quality/task-XX/` directory. A missing XML, nonzero Unity exit, compile error outside the expected missing symbol, failed fixture setup, or unexpected test failure is a stop gate.

---

### Task 1: Freeze workspace protections and add the catalog data contract

**Files:**
- Create: `Assets/_Game/Editor/ProjectQuality.meta`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs.meta`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityCatalogLoader.cs`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityCatalogLoader.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs.meta`
- Create: `Docs/Engineering/project-quality-catalog.json`

**Interfaces:**
- Produces: `ProjectQualityCatalogLoader.LoadFromFile(string absolutePath) : ProjectQualityCatalog`
- Produces: `ProjectQualityCatalogLoader.LoadFromJson(string json, string sourceName) : ProjectQualityCatalog`
- Produces enums `ProjectReuseLevel` and `ProjectVerificationLevel`
- Produces serializable records `ProjectFeatureGroup`, `ProjectReuseEntry`, `ProjectSceneEntry`, `ProjectUiEntry`, `ProjectDocumentationRule`, and `ProjectQualityCatalog`
- Later tasks consume only these public normalized models; they do not reparse JSON.

- [ ] **Step 1: Snapshot protected files and verify the task starts from the approved commit**

Run:

```bash
PROJECT_ROOT=/Users/baiyan1/Documents/WasteCity-first-art-pass-fixes
cd "$PROJECT_ROOT"
git merge-base --is-ancestor \
  b83fda9d9102633151f99f48038533d876a01339 HEAD || {
  echo "STOP: approved specification b83fda9 is not an ancestor" >&2
  exit 1
}
test -f Docs/superpowers/plans/2026-08-12-project-testing-reuse-catalog-and-documentation.md
mkdir -p /tmp/wastecity-project-quality/protected
find Assets/_Game/Art/FirstPass/Environment/Terrain -name '*.png.meta' -print0 \
  | sort -z \
  | xargs -0 shasum -a 256 \
  > /tmp/wastecity-project-quality/protected/terrain-meta.before.sha256
for path in ProjectSettings/PackageManagerSettings.asset ProjectSettings/URPProjectSettings.asset; do
  if test -f "$path"; then shasum -a 256 "$path"; else printf 'MISSING  %s\n' "$path"; fi
done > /tmp/wastecity-project-quality/protected/project-settings.before.sha256
git status --short > /tmp/wastecity-project-quality/protected/status.before.txt
git diff --cached --quiet
```

Expected: the approved specification is an ancestor of the current implementation-plan HEAD; the plan file exists; the index is empty; protected hashes are recorded. Any committed production implementation after the plan commit is a stop gate and must be audited before continuing.

- [ ] **Step 2: Write failing catalog loader tests**

Add tests with these exact contracts:

```csharp
[Test]
public void LoadFromJson_NormalizesPathsAndReturnsApprovedEnums()
{
    string json = CatalogJson(
        featureId: "building",
        sourceGlob: "Assets\\_Game\\Scripts\\Building\\**",
        reuseLevel: "Recommended",
        verificationLevel: "FocusedEditMode");

    ProjectQualityCatalog catalog =
        ProjectQualityCatalogLoader.LoadFromJson(json, "fixture.json");

    Assert.That(catalog.FeatureGroups[0].SourceGlobs[0],
        Is.EqualTo("Assets/_Game/Scripts/Building/**"));
    Assert.That(catalog.ReuseEntries[0].ReuseLevel,
        Is.EqualTo(ProjectReuseLevel.Recommended));
    Assert.That(catalog.FeatureGroups[0].MinimumVerification,
        Is.EqualTo(ProjectVerificationLevel.FocusedEditMode));
}

[TestCase("duplicate feature id")]
[TestCase("duplicate reuse id")]
[TestCase("unknown reuse level")]
[TestCase("empty Chinese name")]
[TestCase("absolute repository path")]
[TestCase("parent traversal")]
public void LoadFromJson_RejectsInvalidCatalogWithSourceName(string caseName)
{
    string json = InvalidCatalogJson(caseName);
    var error = Assert.Throws<InvalidDataException>(() =>
        ProjectQualityCatalogLoader.LoadFromJson(json, "bad-catalog.json"));
    StringAssert.Contains("bad-catalog.json", error.Message);
    StringAssert.Contains(ExpectedFragment(caseName), error.Message);
}
```

The fixture helpers return complete JSON strings; do not add a permissive fallback for missing fields.

- [ ] **Step 3: Run the focused RED test**

Run:

```bash
UNITY_BIN=/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity
PROJECT_ROOT=/Users/baiyan1/Documents/WasteCity-first-art-pass-fixes
mkdir -p /tmp/wastecity-project-quality/task-01
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.ProjectQualityCatalogTests \
  -testResults /tmp/wastecity-project-quality/task-01/red.xml \
  -logFile /tmp/wastecity-project-quality/task-01/red.log
```

Expected: compilation fails only because `ProjectQualityCatalogLoader` and its model types do not exist. Any unrelated compile error is a stop gate.

- [ ] **Step 4: Implement the strict serializable models and loader**

Use this public surface:

```csharp
namespace WasteCity.Editor.ProjectQuality
{
    public enum ProjectReuseLevel
    {
        Recommended,
        ReviewBeforeReuse,
        SceneOnly,
        FrozenRegression,
        ProhibitedForNewWork,
    }

    public enum ProjectVerificationLevel
    {
        FocusedEditMode,
        FocusedPlayMode,
        FullRegression,
        Compile,
        WindowsBuilds,
        Performance,
        HumanPlaytest,
    }

    [Serializable]
    public sealed class ProjectFeatureGroup
    {
        public string Id;
        public string ChineseName;
        public string[] SourceGlobs;
        public string[] TestFileGlobs;
        public string[] ScenePaths;
        public string[] RequirementIds;
        public string[] HumanDocumentPaths;
        public ProjectVerificationLevel MinimumVerification;
    }

    [Serializable]
    public sealed class ProjectReuseEntry
    {
        public string Id;
        public string ChineseName;
        public string[] TypeNames;
        public string[] AssetPaths;
        public string FeatureGroupId;
        public ProjectReuseLevel ReuseLevel;
        public string UseSummary;
        public string BoundarySummary;
        public string[] RequiredTestFiles;
        public string[] RequirementIds;
    }

    [Serializable]
    public sealed class ProjectSceneEntry
    {
        public string Id;
        public string ChineseName;
        public string Path;
        public string Purpose;
        public bool EnabledInBuildSettings;
        public int ExpectedBuildIndex;
        public ProjectReuseLevel ReuseLevel;
    }

    [Serializable]
    public sealed class ProjectUiEntry
    {
        public string Id;
        public string ChineseName;
        public string OwnerTypeName;
        public string SceneId;
        public string InputPrioritySummary;
        public string[] RequiredTestFiles;
    }

    [Serializable]
    public sealed class ProjectDocumentationRule
    {
        public string Id;
        public string[] ChangedPathGlobs;
        public string[] ReviewDocumentPaths;
        public string PlainChineseReason;
    }

    [Serializable]
    public sealed class ProjectQualityCatalog
    {
        public int SchemaVersion;
        public ProjectFeatureGroup[] FeatureGroups;
        public ProjectReuseEntry[] ReuseEntries;
        public ProjectSceneEntry[] Scenes;
        public ProjectUiEntry[] UiEntries;
        public ProjectDocumentationRule[] DocumentationRules;
        public string[] ExplicitSourceExclusions;
        public string[] ExplicitTestExclusions;
    }
}
```

`LoadFromJson` must trim IDs and human text, normalize `\` to `/`, reject rooted paths and `..`, require schema `1`, reject null arrays, and detect duplicates with `StringComparer.Ordinal`. Do not silently repair invalid enum strings: because `JsonUtility` cannot parse enum names robustly from arbitrary JSON, use string DTO fields internally and convert them explicitly.

- [ ] **Step 5: Create the initial complete catalog**

Use schema `1` and these 13 feature IDs exactly:

```text
foundation-clock
world-terrain
city-navigation-deployment
leader-direct-control
building-construction-evacuation
ui-input
economy-production-logistics
research-population
combat-routes
persistence-migration
presentation-art-integration
scene-editor-build-performance
frozen-2d-regression
```

Catalog rules must map source directories by exact repository-relative globs. Use exact-file overrides for shared folders:

- `Graybox3D/Building/GrayboxBuildingMenuView3D.cs`, `GrayboxBuildingInputRouter3D.cs`, `GrayboxUiInputGuard3D.cs`, and `GrayboxDeveloperModifierBootstrap3D.cs` also map to `ui-input`;
- all other `Graybox3D/Building/*.cs` map to `building-construction-evacuation`;
- `ArtIntegration3D/**` maps to `presentation-art-integration`;
- `Editor/**` maps to `scene-editor-build-performance`;
- `Legacy/**`, 2D `Placeholder*`, `Formal*Controller` scene adapters, and `FormalPrototype.unity` map to `frozen-2d-regression` as a secondary group where relevant.

Test globs must cover all current `93` EditMode and `4` PlayMode files without a catch-all that hides future unknown tests. Group file prefixes explicitly, for example:

```json
{
  "Id": "city-navigation-deployment",
  "TestFileGlobs": [
    "Assets/_Game/Tests/EditMode/City*Tests.cs",
    "Assets/_Game/Tests/EditMode/DirectControlRulesTests.cs",
    "Assets/_Game/Tests/EditMode/GrayboxMobileCityController3DTests.cs",
    "Assets/_Game/Tests/EditMode/GrayboxWorldLayout3DTests.cs",
    "Assets/_Game/Tests/PlayMode/GrayboxRuntimeSceneTests.cs"
  ]
}
```

Create at least these curated reuse IDs:

```text
stable-id
world-map-model
world-layout-3d
planar-coordinate-mapper-3d
city-pathfinder
city-terrain-rules
city-deployment-rules
direct-control-rules
building-catalog
building-grid
building-mobility-rules
building-placement-rules
building-unlock-model
construction-progress
construction-refund-rules
building-session-3d
building-input-router-3d
building-menu-view-3d
building-world-view-3d
resource-inventory
research-model
population-model
visual-slot-2d
graybox-visual-slot-3d
first-art-terrain-profile-3d
first-art-terrain-renderer-3d
graybox-scene-authoring
formal-build-tools
graybox-performance-probe
formal-save-data
formal-prototype-frozen
placeholder-building-controller-frozen
```

Mark `formal-prototype-frozen` as `FrozenRegression` and `placeholder-building-controller-frozen` as `ProhibitedForNewWork`. Every entry needs non-empty plain-Chinese `UseSummary` and `BoundarySummary`, at least one existing test file, and the applicable `IDEA/DOC/BUG` IDs.

- [ ] **Step 6: Run Task 1 GREEN and protect external files**

Run the same focused test and expect all catalog tests to pass. Then run:

```bash
find Assets/_Game/Art/FirstPass/Environment/Terrain -name '*.png.meta' -print0 \
  | sort -z \
  | xargs -0 shasum -a 256 \
  > /tmp/wastecity-project-quality/protected/terrain-meta.task-01.sha256
for path in ProjectSettings/PackageManagerSettings.asset ProjectSettings/URPProjectSettings.asset; do
  if test -f "$path"; then shasum -a 256 "$path"; else printf 'MISSING  %s\n' "$path"; fi
done > /tmp/wastecity-project-quality/protected/project-settings.task-01.sha256
cmp /tmp/wastecity-project-quality/protected/terrain-meta.before.sha256 \
    /tmp/wastecity-project-quality/protected/terrain-meta.task-01.sha256
cmp /tmp/wastecity-project-quality/protected/project-settings.before.sha256 \
    /tmp/wastecity-project-quality/protected/project-settings.task-01.sha256
```

Expected: both `cmp` commands exit `0`.

- [ ] **Step 7: Commit Task 1 exact paths**

```bash
git add -- \
  Assets/_Game/Editor/ProjectQuality.meta \
  Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs \
  Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs.meta \
  Assets/_Game/Editor/ProjectQuality/ProjectQualityCatalogLoader.cs \
  Assets/_Game/Editor/ProjectQuality/ProjectQualityCatalogLoader.cs.meta \
  Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs \
  Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs.meta \
  Docs/Engineering/project-quality-catalog.json
git diff --cached --check
git commit -m "feat: define project quality catalog"
```

---

### Task 2: Discover the current project deterministically

**Files:**
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityScanner.cs`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityScanner.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityScannerTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityScannerTests.cs.meta`
- Modify: `Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs`
- Modify: `Assets/_Game/Editor/WasteCity.Editor.asmdef`

**Interfaces:**
- Consumes: normalized `ProjectQualityCatalog`
- Produces: `ProjectQualityScanner.Scan(string projectRoot) : ProjectInventorySnapshot`
- Produces records `ProjectFileRecord`, `ProjectTypeRecord`, `ProjectAssemblyRecord`, `ProjectSceneRecord`, `ProjectTestClassRecord`, and `ProjectEditorEntryPointRecord`

- [ ] **Step 1: Write scanner RED tests**

Test the current repository and a temporary fixture root separately:

```csharp
[Test]
public void Scan_CurrentProjectFindsKnownAssembliesScenesAndTypes()
{
    ProjectInventorySnapshot snapshot =
        ProjectQualityScanner.Scan(ProjectRoot());

    CollectionAssert.Contains(snapshot.AssemblyNames, "WasteCity.Game");
    CollectionAssert.Contains(snapshot.AssemblyNames, "WasteCity.Editor");
    CollectionAssert.Contains(snapshot.ScenePaths,
        "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
    Assert.That(snapshot.TypeRecords.Any(x =>
        x.FullName == "WasteCity.Graybox3D.GrayboxMobileCityController3D" &&
        x.Kind == ProjectTypeKind.MonoBehaviour), Is.True);
    Assert.That(snapshot.TestClasses.Any(x =>
        x.FullName == "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests" &&
        x.Platform == ProjectTestPlatform.PlayMode), Is.True);
}

[Test]
public void Scan_RepeatedRunProducesEqualOrderedSnapshot()
{
    ProjectInventorySnapshot first = ProjectQualityScanner.Scan(ProjectRoot());
    ProjectInventorySnapshot second = ProjectQualityScanner.Scan(ProjectRoot());
    Assert.That(second.ToDeterministicJson(), Is.EqualTo(first.ToDeterministicJson()));
}
```

Add fixture tests proving generated `.cs` files are found, `.meta` and `Library` are excluded, and path order uses `StringComparer.Ordinal`.

- [ ] **Step 2: Run Task 2 RED**

Run the Task 1 command template with `-testFilter WasteCity.Tests.ProjectQualityScannerTests`, `task-02/red.xml`, and `task-02/red.log`. Expected: only the missing scanner/snapshot/type symbols fail compilation.

- [ ] **Step 3: Implement filesystem and Unity discovery**

The scanner must:

- enumerate repository-relative production `.cs` under `Assets/_Game/Scripts` and `Assets/_Game/Editor`;
- enumerate EditMode and PlayMode `.cs` test files;
- parse `.asmdef` names using `JsonUtility` DTOs;
- use `TypeCache.GetTypesDerivedFrom<MonoBehaviour>()` and `TypeCache.GetTypesDerivedFrom<ScriptableObject>()` only for production/editor `WasteCity*` assemblies; exclude the frozen test assembly names `WasteCity.EditModeTests` and `WasteCity.PlayModeTests` so test-only helper components do not enter `ProjectTypeRecords`;
- use `EditorBuildSettings.scenes` for enabled scene order;
- identify Editor public static parameterless methods on `FormalBuildTools`, `GrayboxSceneAuthoring`, `FirstArtTerrainAssetBuilder`, `FirstArtTerrainEvidenceCapture`, `GrayboxPerformanceProbe`, and later `ProjectQualityTools`;
- find test classes from loaded types with NUnit `[Test]`, `[TestCase]`, `[TestCaseSource]`, or Unity `[UnityTest]` methods;
- keep `Mono.Cecil` and `Mono.Cecil.Pdb` private to `WasteCity.Editor`: set `overrideReferences` and add only Unity-owned `Mono.Cecil.dll` and `Mono.Cecil.Pdb.dll` as `precompiledReferences` in `WasteCity.Editor.asmdef`. Do not vendor DLLs, use machine-specific paths, expose Cecil types from public scanner models, modify `WasteCity.EditModeTests.asmdef`, or add Cecil `using` directives to tests;
- build one ordinal deterministic type-to-source index from the loaded `WasteCity*` assembly DLLs and their Unity-generated portable PDB sequence-point documents. Normalize each document path to one repository-relative path under the approved source/test roots, recurse nested Cecil type definitions, and match the exact runtime `Type.FullName`; use `MonoScript.GetClass()` exact identity only as a zero-sequence-point fallback. Multiple top-level types in one file must map independently (including `WasteCity.Building.BuildingRuntime` and `WasteCity.Building.PlaceholderShieldGenerator`). Fail on absent DLL/PDB, missing source, outside-root source, or multiple distinct paths rather than guessing;
- map every production/editor discovered type and each discovered test class through that private index. Test-only `MonoBehaviour` and `ScriptableObject` helpers are deliberately absent from `ProjectTypeRecords`; their owning test class remains in the test inventory. Tests verify only public `ProjectInventorySnapshot` results and never directly construct or inspect Cecil objects;
- delete the handwritten C# declaration lexer/preprocessor and its reflection-based lexical tests after the new current-project RED proves the missing multi-type mapping. Do not retain source-text parsing as a fallback;
- keep all arrays sorted ordinally.

Do not load or save scenes during a general scan. Scene object-tree inspection belongs to existing scene contract tests, not this inventory.

**Source-mapping rationale:** Repeated valid-C# edge cases around comments, conditional branches, interpolated strings, and raw strings show that a handwritten declaration parser cannot be the authoritative mapping boundary. `MonoScript.GetClass()` also represents only one primary class per imported file, while current production file `BuildingRuntime.cs` contains several top-level `MonoBehaviour` types. Unity already produces portable PDB files next to the loaded Editor assemblies, and their compiled sequence-point documents provide a syntax-independent type-to-source boundary. Keep that implementation private to the Editor scanner, retain `MonoScript` only for a compiled type without usable sequence points, and test the scanner's public inventory rather than spreading Cecil into the test assembly.

- [ ] **Step 4: Run Task 2 GREEN**

Run the same Task 2 filter with `task-02/green.xml` and `task-02/green.log`; expect all pass. Record discovered counts in the XML output as test properties, but do not assert the globally changing total `1121`.

- [ ] **Step 5: Commit Task 2**

Stage only the six listed task paths and commit:

```bash
git commit -m "feat: scan project quality inventory"
```

---

### Task 3: Validate classification, reuse boundaries, scenes, UI, and links

**Files:**
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityValidator.cs`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityValidator.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityValidatorTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityValidatorTests.cs.meta`
- Modify: `Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs`
- Modify: `Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs`
- Modify: `Docs/Engineering/project-quality-catalog.json`

**Interfaces:**
- Produces: `ProjectQualityValidator.Validate(ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot, string projectRoot) : IReadOnlyList<ProjectQualityIssue>`
- Produces: `ProjectQualityIssue` with `Code`, `Severity`, `PlainChineseMessage`, and `Path`
- Consumes no mutable Unity scene state and writes no files.

- [ ] **Step 1: Write mutation-sensitive RED tests**

Create a valid in-memory fixture, mutate one fact per case, and assert one stable issue code:

```csharp
[TestCase("unmapped-source", "PQ001")]
[TestCase("unmapped-test", "PQ002")]
[TestCase("missing-reuse-path", "PQ003")]
[TestCase("missing-required-test", "PQ004")]
[TestCase("unknown-feature", "PQ005")]
[TestCase("wrong-scene-index", "PQ006")]
[TestCase("frozen-recommended", "PQ007")]
[TestCase("placeholder-recommended", "PQ008")]
[TestCase("missing-ui-owner", "PQ009")]
[TestCase("missing-human-link", "PQ010")]
public void Validate_ReturnsStableIssueForEachBrokenContract(
    string mutation, string expectedCode)
{
    Fixture fixture = ValidFixture();
    fixture.Apply(mutation);
    IReadOnlyList<ProjectQualityIssue> issues =
        ProjectQualityValidator.Validate(
            fixture.Catalog, fixture.Snapshot, fixture.Root);
    Assert.That(issues.Any(x => x.Code == expectedCode), Is.True,
        string.Join("\n", issues.Select(x => x.PlainChineseMessage)));
}
```

Add a read-only proof that hashes every fixture file before and after `Validate` and gets identical hashes.

- [ ] **Step 2: Run RED and confirm the failure is only the missing validator**

Run the Task 1 command template with `-testFilter WasteCity.Tests.ProjectQualityValidatorTests`, `task-03/red.xml`, and `task-03/red.log`. Expected: only the missing validator/issue symbols fail compilation.

- [ ] **Step 3: Implement exact validation order**

Return issues in this order, then sort by `Code`, `Path`, message:

1. catalog schema/relationship errors;
2. unmapped production files;
3. unmapped test classes/files;
4. missing reuse paths/types/tests;
5. forbidden reuse-level combinations;
6. scene order and enabled-state mismatches;
7. UI owner/scene/test mismatches;
8. missing human-document paths and broken Markdown links;
9. stale generated outputs once Task 4 adds content comparison.

Globs support only `*`, `?`, and terminal `/**`; reject other recursive syntax during catalog loading. Multiple feature mappings are allowed. Explicit exclusions require exact paths and non-empty reasons; change `ExplicitSourceExclusions` and `ExplicitTestExclusions` from string arrays to records with `Path` and `Reason` before implementing exclusions.

- [ ] **Step 4: Make the current catalog complete**

Run the validator against the current snapshot. Amend only the catalog mappings until zero `PQ001`/`PQ002` issues remain. Do not add a broad `Assets/**` or `*Tests.cs` catch-all. Add tests proving a newly introduced fake path would still be rejected.

- [ ] **Step 5: Run Task 1–3 GREEN**

Filter:

```text
WasteCity.Tests.ProjectQualityCatalogTests|WasteCity.Tests.ProjectQualityScannerTests|WasteCity.Tests.ProjectQualityValidatorTests
```

Expected: all pass and current project produces zero validation issues except the not-yet-generated output codes, which remain disabled until Task 4.

Use the Task 1 command template with the displayed filter and `task-03/green.xml` / `task-03/green.log`.

- [ ] **Step 6: Commit Task 3**

Commit exact listed paths with:

```bash
git commit -m "feat: validate project quality boundaries"
```

---

### Task 4: Generate deterministic technical appendices

**Files:**
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectDocumentationGenerator.cs`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectDocumentationGenerator.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ProjectDocumentationGeneratorTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ProjectDocumentationGeneratorTests.cs.meta`
- Create: `Docs/Generated/Project-Inventory-ZH.md`
- Create: `Docs/Generated/Test-Inventory-ZH.md`
- Create: `Docs/Generated/Latest-Verification-ZH.md`
- Create: `Docs/Generated/Documentation-Attention-ZH.md`
- Modify: `Assets/_Game/Editor/ProjectQuality/ProjectQualityValidator.cs`
- Modify: `Docs/Engineering/project-quality-catalog.json`

**Interfaces:**
- Produces: `ProjectDocumentationGenerator.RenderStructuralDocuments(catalog, snapshot) : IReadOnlyDictionary<string,string>`
- Produces: `ProjectDocumentationGenerator.RenderDocumentationAttention(ProjectQualityCatalog catalog, IReadOnlyList<string> changedPaths) : string`
- Produces: `ProjectDocumentationGenerator.RenderVerification(ProjectVerificationSnapshot snapshot) : string`
- Produces: `ProjectDocumentationGenerator.WriteGeneratedFiles(string projectRoot, IReadOnlyDictionary<string,string> files) : void`
- Validator compares rendered content in memory with disk and never calls `WriteGeneratedFiles`.

- [ ] **Step 1: Write RED tests for determinism and confinement**

Required tests:

```csharp
[Test]
public void RenderStructuralDocuments_SameInputIsByteForByteStable()
{
    var first = ProjectDocumentationGenerator.RenderStructuralDocuments(
        CatalogFixture(), SnapshotFixture());
    var second = ProjectDocumentationGenerator.RenderStructuralDocuments(
        CatalogFixture(), SnapshotFixture());
    CollectionAssert.AreEquivalent(first.Keys, second.Keys);
    foreach (string path in first.Keys)
        Assert.That(second[path], Is.EqualTo(first[path]));
}

[Test]
public void StructuralDocuments_DoNotContainCurrentTimeOrGitSha()
{
    string combined = string.Join("\n",
        ProjectDocumentationGenerator.RenderStructuralDocuments(
            CatalogFixture(), SnapshotFixture()).Values);
    StringAssert.DoesNotMatch(@"\b[0-9a-f]{40}\b", combined);
    StringAssert.DoesNotMatch(@"\d{4}-\d{2}-\d{2}T", combined);
}

[TestCase("../README.md")]
[TestCase("Docs/07-Project-Use-and-Development-Guide-ZH.md")]
[TestCase("/tmp/out.md")]
public void WriteGeneratedFiles_RejectsOutsideGeneratedDirectory(string path)
{
    Assert.Throws<InvalidOperationException>(() =>
        ProjectDocumentationGenerator.WriteGeneratedFiles(
            FixtureRoot(), new Dictionary<string,string>{{path, "x"}}));
}
```

Also test LF line endings, UTF-8 without BOM, final newline, ordinal sections, plain-Chinese headings, and two-run file hashes.

- [ ] **Step 2: Run Task 4 RED**

Run the Task 1 command template with `-testFilter WasteCity.Tests.ProjectDocumentationGeneratorTests`, `task-04/red.xml`, and `task-04/red.log`. Expected: missing generator symbols only.

- [ ] **Step 3: Implement four renderers**

`Project-Inventory-ZH.md` sections:

1. auto-generated warning and tool schema/content fingerprint;
2. assemblies;
3. enabled scenes and order;
4. production files by feature group;
5. MonoBehaviour components;
6. ScriptableObject assets;
7. UI owners;
8. Editor/build/performance entry points;
9. art integration and stable presentation paths;
10. explicit exclusions.

`Test-Inventory-ZH.md` sections:

1. warning and fingerprint;
2. explanation of EditMode and PlayMode in ordinary Chinese;
3. each feature group with minimum gate;
4. exact test files/classes;
5. copyable `-testFilter` using fully qualified class names joined by `|`;
6. source paths and controlled requirement IDs used for failure location.

`Documentation-Attention-ZH.md` is rendered by `RenderDocumentationAttention` from a caller-supplied changed-path list. With an empty list it says there are no pending path-based reminders. It lists reminders only; it never says the document was updated or approved. `RenderStructuralDocuments` returns only `Project-Inventory-ZH.md` and `Test-Inventory-ZH.md`; callers combine those two outputs with the independently rendered attention and verification documents before writing.

`Latest-Verification-ZH.md` accepts:

```csharp
public sealed class ProjectVerificationSnapshot
{
    public string VerifiedCommitSha;
    public string VerifiedAtIso8601;
    public ProjectTestRunSummary EditMode;
    public ProjectTestRunSummary PlayMode;
    public ProjectCommandResult Compile;
    public ProjectCommandResult[] Builds;
    public string HumanPlaytestStatus;
}
```

Reject non-40-character lowercase SHA, invalid ISO-8601 with offset, missing XML paths, negative counts, or `passed + failed + skipped != total`. `HumanPlaytestStatus` accepts only `未进行`, `等待用户复验`, or a caller-supplied controlled-record reference such as `BUG-0002 已由用户于 2026-08-13 验证`; it is never inferred.

- [ ] **Step 4: Implement atomic writes**

For each approved path, write UTF-8/LF content to `<target>.tmp`, flush and close, compare with existing target, then replace only when content differs. On any render failure, delete only the task-owned `.tmp` file and leave the old document intact. Do not use a directory-wide delete.

- [ ] **Step 5: Enable stale-output validation and create first generated files**

Validator issue `PQ011` reports a missing or byte-different generated document. The test must prove `Validate` detects stale output but leaves it unchanged. Generate the current structural files twice and require identical SHA-256 values.

Create the initial verification snapshot using the already verified parent baseline facts, not guessed fresh results:

- verified SHA: `81b2f47d1688a72a7ddba36a2ffa04b1025e40f9`;
- EditMode `1121/1121`, failed `0`, skipped `0`;
- PlayMode `82/82`, failed `0`, skipped `0`;
- compile passed;
- three Windows builds passed;
- human playtest: `等待用户复验`.

Label those as recorded prior evidence. Task 9 replaces them with this feature's fresh final verification.

- [ ] **Step 6: Run Task 4 GREEN and commit**

Run the Task 1 command template with `-testFilter 'WasteCity.Tests.ProjectQualityCatalogTests|WasteCity.Tests.ProjectQualityScannerTests|WasteCity.Tests.ProjectQualityValidatorTests|WasteCity.Tests.ProjectDocumentationGeneratorTests'` and `task-04/green.xml` / `task-04/green.log`; then run validator against repository and two generation passes. Commit only listed task paths:

```bash
git commit -m "feat: generate project quality documentation"
```

---

### Task 5: Analyze NUnit XML and produce focused Bug-location reports

**Files:**
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectTestResultAnalyzer.cs`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectTestResultAnalyzer.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ProjectTestResultAnalyzerTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ProjectTestResultAnalyzerTests.cs.meta`
- Modify: `Assets/_Game/Editor/ProjectQuality/ProjectQualityModels.cs`
- Modify: `Docs/Engineering/project-quality-catalog.json`

**Interfaces:**
- Produces: `ProjectTestResultAnalyzer.Analyze(string xmlPath, ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot) : ProjectTestAnalysisReport`
- Produces: `ProjectTestResultAnalyzer.RenderPlainChinese(ProjectTestAnalysisReport report) : string`
- Report contains summaries plus `ProjectFailedTestLocation[]`.

- [ ] **Step 1: Write focused XML fixtures and RED tests**

Use inline XML written to a task-owned temporary directory. Cover:

```csharp
[Test]
public void Analyze_KnownFailureMapsFeatureFilesSceneRequirementAndRerun()
{
    string xml = WriteNUnitXml(
        fullName: "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests." +
            "MobileResearchStation_CanBePlacedInInnerCity",
        message: "Expected Valid but was InvalidCityMode",
        stack: "at WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.cs:410");

    ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(
        xml, CurrentCatalog(), CurrentSnapshot());
    ProjectFailedTestLocation failed = report.Failures.Single();

    Assert.That(failed.FeatureGroupId,
        Is.EqualTo("building-construction-evacuation"));
    CollectionAssert.Contains(failed.RequirementIds, "IDEA-0005");
    CollectionAssert.Contains(failed.ScenePaths,
        "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
    StringAssert.Contains("GrayboxBuildingRuntimeSceneTests", failed.RerunFilter);
    StringAssert.Contains("Expected Valid but was InvalidCityMode",
        failed.OriginalMessage);
}
```

Additional tests:

- unknown test → issue `PQTEST001` and explicit `未归类`, never silently omitted;
- malformed XML → `InvalidDataException` naming the path;
- run with `result="Failed"` but no test-case → report `结果不完整`;
- skipped test is counted but not reported as failed;
- multiple failures sort by full name;
- XML entities and multiline stack traces are preserved;
- report does not contain `根因已确定` or an unproven fix suggestion.

- [ ] **Step 2: Run Task 5 RED**

Run the Task 1 command template with `-testFilter WasteCity.Tests.ProjectTestResultAnalyzerTests`, `task-05/red.xml`, and `task-05/red.log`. Expected: only missing analyzer/report symbols fail compilation.

- [ ] **Step 3: Implement XML parsing without executing tests**

Use `XDocument.Load` with safe local-file input only. Do not resolve external entities and do not accept HTTP URLs. Support Unity's NUnit 3 `test-run`, `test-suite`, and `test-case` elements. Map by exact fully qualified test class before the last method segment; parameterized cases may append argument text and must still resolve to the class.

The Chinese rendering order is:

```text
测试结果摘要
问题区域
失败位置
失败测试
优先检查
相关文件
相关场景
相关需求
建议复跑
原始错误
原始堆栈
```

“优先检查” comes from catalog feature/reuse relationships; it is a starting point, not a root-cause claim.

- [ ] **Step 4: Add feature-specific diagnosis labels**

For each of the 13 feature groups, add a short plain-Chinese `FailureLocationSummary` and ordered `PrimarySourceGlobs` to the catalog. For example, building failures say “先检查建筑定义、建造限制、放置会话和场景接线”; UI failures say “先检查焦点、输入优先级、界面组件和真实场景引用”. Keep each sentence below 45 Chinese characters.

- [ ] **Step 5: Run GREEN and commit**

Run the Task 1 command template with `-testFilter 'WasteCity.Tests.ProjectTestResultAnalyzerTests|WasteCity.Tests.ProjectQualityCatalogTests|WasteCity.Tests.ProjectQualityValidatorTests'` and `task-05/green.xml` / `task-05/green.log`. Commit exact paths:

```bash
git commit -m "feat: map test failures to project areas"
```

---

### Task 6: Add safe menu and batch entry points

**Files:**
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityTools.cs`
- Create: `Assets/_Game/Editor/ProjectQuality/ProjectQualityTools.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs.meta`
- Modify: `Assets/_Game/Editor/ProjectQuality/ProjectQualityValidator.cs`
- Modify: `Assets/_Game/Editor/ProjectQuality/ProjectDocumentationGenerator.cs`

**Interfaces:**
- Produces static methods:
  - `GenerateDocumentation()`
  - `ValidateDocumentation()`
- `AnalyzeTestResults()`
- `RecordVerification()`
- Produces menu items under `WasteCity/Project Quality/`.
- Batch configuration is read from explicit `WASTECITY_QUALITY_*` environment variables only.

- [ ] **Step 1: Write API and no-write RED tests**

Assert all four public static parameterless `void` methods exist. Add source/behavior tests proving:

- `ValidateDocumentation` does not call any generator write method;
- `AnalyzeTestResults` requires `WASTECITY_QUALITY_TEST_RESULTS` and writes only `WASTECITY_QUALITY_ANALYSIS_OUTPUT` after validating it is outside `Assets`, `Packages`, and `ProjectSettings` or under approved `/tmp`/explicit output path;
- `RecordVerification` requires all explicit verified SHA/time/result variables;
- missing environment variables fail with names in the exception;
- `GenerateDocumentation` cannot stage or invoke Git.

- [ ] **Step 2: Run Task 6 RED**

Run the Task 1 command template with `-testFilter WasteCity.Tests.ProjectQualityIntegrationTests`, `task-06/red.xml`, and `task-06/red.log`. Expected: only the missing tool entry-point symbols and their required orchestration behavior fail.

- [ ] **Step 3: Implement orchestration**

Use constants:

```csharp
public const string CatalogPath =
    "Docs/Engineering/project-quality-catalog.json";
public const string GeneratedRoot = "Docs/Generated";
public const string TestResultsEnvironment =
    "WASTECITY_QUALITY_TEST_RESULTS";
public const string AnalysisOutputEnvironment =
    "WASTECITY_QUALITY_ANALYSIS_OUTPUT";
public const string VerifiedShaEnvironment =
    "WASTECITY_QUALITY_VERIFIED_SHA";
public const string VerifiedAtEnvironment =
    "WASTECITY_QUALITY_VERIFIED_AT";
public const string EditModeResultsEnvironment =
    "WASTECITY_QUALITY_EDITMODE_RESULTS";
public const string PlayModeResultsEnvironment =
    "WASTECITY_QUALITY_PLAYMODE_RESULTS";
public const string CompileLogEnvironment =
    "WASTECITY_QUALITY_COMPILE_LOG";
public const string BuildSummaryEnvironment =
    "WASTECITY_QUALITY_BUILD_SUMMARY";
public const string HumanPlaytestEnvironment =
    "WASTECITY_QUALITY_HUMAN_PLAYTEST";
public const string ChangedPathsEnvironment =
    "WASTECITY_QUALITY_CHANGED_PATHS";
```

`WASTECITY_QUALITY_CHANGED_PATHS` names a UTF-8 text file containing one repository-relative changed path per line; an absent variable means an empty change list, not an automatic Git query. `WASTECITY_QUALITY_BUILD_SUMMARY` names a strict JSON file containing build name, status, and evidence-log path, including an explicit `NotRequired` status and reason when runtime/build inputs are unchanged.

`ValidateDocumentation` logs every issue with `[ProjectQuality:<code>]` then throws one `InvalidOperationException` with the issue count. `GenerateDocumentation` first validates catalog and scan relationships excluding stale-output `PQ011`, writes structural output plus the attention document from the explicit changed-path file, then performs full validation. `AnalyzeTestResults` never changes docs. `RecordVerification` only rewrites `Latest-Verification-ZH.md` after parsing actual supplied XML/log summaries and the explicit human-playtest value.

- [ ] **Step 4: Prove path confinement against the real dirty worktree**

Hash all tracked files outside the approved generated root, run `GenerateDocumentation`, and compare. Expected changes are only under `Docs/Generated`. Then run `ValidateDocumentation` and prove no file hash changes anywhere.

- [ ] **Step 5: Run integration GREEN and commit**

Run the Task 1 command template with all six ProjectQuality test classes and `task-06/green.xml` / `task-06/green.log`. Commit exact task files with:

```bash
git commit -m "feat: expose project quality automation"
```

---

### Task 7: Write the three plain-Chinese human guides

**Files:**
- Create: `Docs/07-Project-Use-and-Development-Guide-ZH.md`
- Create: `Docs/08-Testing-and-Bug-Location-Guide-ZH.md`
- Create: `Docs/09-Reusable-Project-Catalog-ZH.md`
- Modify: `Docs/Engineering/project-quality-catalog.json`
- Modify: `Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs`

**Interfaces:**
- Documents link to generated appendices but do not copy changing counts.
- Reuse guide renders curated entries from the catalog as explanatory prose; technical appendices remain the exact inventory.

- [ ] **Step 1: Write failing document-content tests**

The tests read the three files and require:

- each starts with a Chinese title and a one-paragraph “适合谁看”;
- all first uses of `EditMode`, `PlayMode`, `组件`, `程序集`, and `稳定 ID` have plain-Chinese explanations nearby;
- no hard-coded overall test count matching `\d+/\d+` appears;
- each guide links `Docs/06` and the relevant generated appendix;
- the reuse guide contains all five reuse levels in Chinese;
- the Bug guide contains the exact lifecycle `复现失败 → 失败测试 → 最小修复 → 单功能检查 → 相关检查 → 完整回归 → 人工确认`;
- the user guide explains default 3D versus frozen 2D and says the frozen 2D scene is not a new-feature template;
- none uses “一定是”“根因就是” for an automated failure report.

- [ ] **Step 2: Run RED**

Run the Task 1 command template with `-testFilter WasteCity.Tests.ProjectQualityIntegrationTests`, `task-07/red.xml`, and `task-07/red.log`. Expected: only the three human guide content contracts fail because the files are missing.

- [ ] **Step 3: Write `Docs/07` in ordinary Chinese**

Required sections:

1. 这份说明适合谁；
2. 游戏目前能做什么；
3. 明确尚未完成的内容；
4. 怎样打开默认 3D 游戏；
5. 两个场景的区别；
6. 主要按键和界面；
7. 开发修改器只在开发版本出现；
8. 想修改某个功能先看哪里；
9. 新电脑交接最短路径；
10. 出问题时先做什么；
11. 技术清单链接。

Do not repeat the full GDD or write unimplemented research UI as available.

- [ ] **Step 4: Write `Docs/08` in ordinary Chinese**

Required sections:

1. 测试是什么，不是什么；
2. 快速规则、单功能、真实场景、完整回归、人工试玩五层；
3. 按功能选择测试；
4. 怎样读失败定位报告；
5. 明天试玩记录模板；
6. Bug 修复流程；
7. 偶发失败不能直接忽略；
8. 什么情况下要构建 Windows；
9. 给开发者/AI 的命令入口放在折叠式技术附录或末尾。

The playtest template asks for version/commit, scene, steps, expected, actual, frequency, screenshot/video, save or seed, and whether the issue blocks progress.

- [ ] **Step 5: Write `Docs/09` from curated entries**

Group by:

- 内容与稳定编号；
- 世界、城市和坐标；
- 建造与撤离；
- UI 与输入；
- 资源、研究、人口、战斗和存档；
- 3D 表现与美术；
- 场景、构建与检查工具；
- 冻结或禁止用于新功能的旧内容。

For every curated entry explain “能解决什么、在哪里、怎么复用、不能负责什么、改后跑哪组测试”. Code names go after the ordinary-Chinese explanation.

- [ ] **Step 6: Run guide tests, regenerate appendices, validate, and commit**

Run the Task 1 command template with all six ProjectQuality classes and `task-07/green.xml` / `task-07/green.log`; then run generation twice and read-only validation.

Commit exact files:

```bash
git commit -m "docs: add plain Chinese project quality guides"
```

---

### Task 8: Make quality documentation a permanent completion gate

**Files:**
- Modify: `AGENTS.md`
- Modify: `README.md`
- Modify: `Docs/README.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Assets/_Game/Tests/EditMode/ProjectQualityIntegrationTests.cs`
- Regenerate: `Docs/Generated/Project-Inventory-ZH.md`
- Regenerate: `Docs/Generated/Test-Inventory-ZH.md`
- Regenerate: `Docs/Generated/Documentation-Attention-ZH.md`

**Interfaces:**
- `AGENTS.md` becomes the mandatory developer/AI gate.
- README and Docs index become discoverability entry points, not duplicate catalogs.

- [ ] **Step 1: Write RED contract tests for the permanent gate**

Tests require `AGENTS.md` to contain, in ordinary Chinese:

```text
新增或修改的生产文件必须归入功能组
新增公共能力必须更新推荐复用目录或说明为何不适合复用
功能修改必须补充对应测试
完成前运行 ProjectQualityTools.GenerateDocumentation
完成前运行 ProjectQualityTools.ValidateDocumentation
自动工具不得改变玩法审批或人工验收结论
```

README and `Docs/README.md` must link Docs 06–09 and all generated appendices. `Docs/05` must add the quality-system milestone without changing gameplay completion percentages solely because documentation tooling exists.

- [ ] **Step 2: Run RED**

Run the Task 1 command template with `-testFilter WasteCity.Tests.ProjectQualityIntegrationTests`, `task-08/red.xml`, and `task-08/red.log`. Expected: failures name only the missing permanent-gate text and links.

- [ ] **Step 3: Update `AGENTS.md`**

Add a concise “开发完成门” after the current startup gate:

1. stable requirement ID;
2. feature-group classification;
3. reuse decision;
4. focused tests;
5. generated documentation;
6. read-only validation;
7. risk-based full tests/builds;
8. manual semantic status update;
9. exact staging and protected-file check.

Explicitly say generated technical documents are not a substitute for `Docs/06` approval.

- [ ] **Step 4: Update README and Docs index**

Replace stale hard-coded test counts in README with a link to `Latest-Verification-ZH.md`, while keeping fixed environment and build commands. Add a short “项目质量与复用入口” section linking Docs 07–09. In `Docs/README.md`, add Docs 06–09 and Generated appendix descriptions.

- [ ] **Step 5: Update the roadmap minimally**

Add `DOC-0001` to synced requirements and a small quality-infrastructure item under automation. State the tool is not gameplay progress and does not increase main GDD or art percentages. Do not update `DOC-0001` to verified yet.

- [ ] **Step 6: Regenerate and validate**

Run `GenerateDocumentation` twice; SHA-256 structural outputs must match between runs. Run `ValidateDocumentation`; expect zero issues and zero writes. Run the Task 1 command template with all six ProjectQuality classes and `task-08/green.xml` / `task-08/green.log`.

- [ ] **Step 7: Commit Task 8**

Commit only listed human/generated docs and the test change:

```bash
git commit -m "docs: require project quality completion gates"
```

---

### Task 9: Fresh end-to-end verification and controlled status write-back

**Files:**
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Regenerate: `Docs/Generated/Latest-Verification-ZH.md`
- Regenerate if required: other `Docs/Generated/*.md`
- Modify only if a real documentation defect is found: files from Tasks 1–8, after first adding a failing test and keeping the fix in the owning task boundary

**Interfaces:**
- Uses all previous tools.
- Produces the final `DOC-0001` implementation/verification record.

- [ ] **Step 1: Run fresh focused ProjectQuality tests**

```bash
UNITY_BIN=/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity
PROJECT_ROOT=/Users/baiyan1/Documents/WasteCity-first-art-pass-fixes
RESULT_ROOT=/tmp/wastecity-project-quality/final
mkdir -p "$RESULT_ROOT"
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testFilter 'WasteCity.Tests.ProjectQualityCatalogTests|WasteCity.Tests.ProjectQualityScannerTests|WasteCity.Tests.ProjectQualityValidatorTests|WasteCity.Tests.ProjectDocumentationGeneratorTests|WasteCity.Tests.ProjectTestResultAnalyzerTests|WasteCity.Tests.ProjectQualityIntegrationTests' \
  -testResults "$RESULT_ROOT/focused.xml" \
  -logFile "$RESULT_ROOT/focused.log"
```

Expected: all ProjectQuality tests pass with zero skipped.

- [ ] **Step 2: Prove failure location on an intentionally failing fixture, not production tests**

Run `ProjectTestResultAnalyzerTests` and preserve its fixture report. Confirm the report names a feature group, source files, scene, requirement ID, rerun filter, original error, and stack. Do not deliberately break a production test.

- [ ] **Step 3: Run full EditMode and PlayMode**

```bash
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform EditMode \
  -testResults "$RESULT_ROOT/editmode.xml" \
  -logFile "$RESULT_ROOT/editmode.log"
"$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
  -runTests -testPlatform PlayMode \
  -testResults "$RESULT_ROOT/playmode.xml" \
  -logFile "$RESULT_ROOT/playmode.log"
```

Expected: all tests pass. If any failure occurs, use the new analyzer, run the suggested focused filter, and apply the existing stop/review rules; do not call a retry success sufficient without diagnosis.

- [ ] **Step 4: Run headless compile**

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$PROJECT_ROOT" \
  -logFile "$RESULT_ROOT/compile.log"
```

Expected: exit `0`, log contains `Exiting batchmode successfully now!`, and no `error CS`, `Compilation failed`, unhandled exception, or batch abort.

- [ ] **Step 5: Decide build scope from actual diff**

Run:

```bash
git diff --name-only b83fda9..HEAD -- Assets Packages ProjectSettings
```

If the committed diff is limited to Editor quality tools and EditMode tests, and all runtime scripts, scenes, rendering assets, Packages, and ProjectSettings are zero-diff, do not rebuild the three Windows players and record “not required: runtime/build inputs unchanged.” If any runtime/build input changed unexpectedly, stop; do not silently expand Task 9.

- [ ] **Step 6: Generate final verification snapshot from actual results**

Pass the current pre-writeback HEAD, current ISO-8601 time with `+08:00`, fresh EditMode/PlayMode XML, compile log, and the explicit build-scope decision to `RecordVerification`. Set human playtest to `等待用户复验`. Then regenerate structural docs and run read-only validation.

- [ ] **Step 7: Re-check protected external files and exact scope**

Recompute both protected SHA lists and compare to Task 1 originals. Require:

- exact match for all protected files;
- `git diff --check` passes for task-owned files;
- runtime scripts, scenes, Persistence, Packages, and ProjectSettings have no committed diff from `b83fda9`;
- staging area contains only final generated verification and Docs/06 before commit.

The pre-existing dirty paths may remain in `git status`; they are not an error if hashes match.

- [ ] **Step 8: Update `DOC-0001` accurately**

Change implementation state to `已实现待验证`, not `已验证`, because the user plans to test tomorrow. Add exact implementation commits, fresh test totals, compile result, generated-document validation result, failure-location fixture result, build-scope decision, and protected-file proof. Do not claim human playtest completion.

- [ ] **Step 9: Commit final write-back and push**

```bash
git add -- \
  Docs/06-User-Feedback-and-Change-Control-ZH.md \
  Docs/Generated/Latest-Verification-ZH.md \
  Docs/Generated/Project-Inventory-ZH.md \
  Docs/Generated/Test-Inventory-ZH.md \
  Docs/Generated/Documentation-Attention-ZH.md
git diff --cached --check
git commit -m "docs: record project quality automation verification"
git push origin codex/playtest-fixes
```

Before pushing, remove from the `git add` command any generated structural file that is byte-identical and therefore not modified. Never stage protected terrain or ProjectSettings files.

- [ ] **Step 10: Final handoff**

Report:

- final branch and SHA;
- new plain-Chinese guide links;
- exact tool commands for generation, validation, and failure analysis;
- fresh EditMode/PlayMode totals and compile status;
- whether builds were correctly skipped due zero runtime diff;
- `DOC-0001` remains `已实现待验证` until tomorrow's user test;
- protected user/Unity files remain untouched;
- no gameplay or scene behavior changed.

---

## Plan Self-Review Results

### Spec coverage

- Plain-Chinese user guide: Task 7.
- Plain-Chinese test/Bug guide: Task 7.
- Curated reuse catalog with filenames, components, scenes, UI, boundaries, and tests: Tasks 1, 3, and 7.
- Automatic current inventory: Tasks 2 and 4.
- Deterministic outputs and no SHA/time loop: Task 4.
- Read-only drift validation: Tasks 3, 4, and 6.
- Fast test-to-Bug location: Task 5.
- Safe generation/batch entry points: Task 6.
- Permanent future-development gate: Task 8.
- Tool self-tests and mutation sensitivity: Tasks 1–6.
- Full regression and truthful human-playtest boundary: Task 9.
- Protected dirty workspace and exact staging: Global Constraints and Tasks 1/9.

### Scope correction made during planning

The approved specification originally said every generated file should record the current SHA and time, which would make structural output stale immediately after each commit. The approved spec was corrected before this plan: structural inventories use tool version plus deterministic content fingerprint; only the explicit verification snapshot records a caller-supplied verified SHA and timestamp.

### No implementation claims

This plan does not mean the quality tools, guides, catalog, or generated appendices exist. `DOC-0001` remains `开发中` until Tasks 1–9 are implemented and machine-verified, and it remains short of `已验证` until the user's planned manual test is recorded.
