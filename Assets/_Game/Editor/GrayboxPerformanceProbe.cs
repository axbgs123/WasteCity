using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.ArtIntegration3D;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;
using Debug = UnityEngine.Debug;

namespace WasteCity.Editor
{
    public static class GrayboxPerformanceProbe
    {
        private const string ResultEnvironmentVariable =
            "WASTECITY_GRAYBOX_PERF_RESULT";
        private const string BuildingResultEnvironmentVariable =
            "WASTECITY_BUILDING_PERF_RESULT";
        private const string FirstTerrainResultEnvironmentVariable =
            "WASTECITY_FIRST_TERRAIN_PERF_RESULT";
        private const string FirstTerrainRuntimeResultEnvironmentVariable =
            "WASTECITY_FIRST_TERRAIN_RUNTIME_RESULT";
        private const string RuinsCliffResultEnvironmentVariable =
            "WASTECITY_RUINS_CLIFF_PERF_RESULT";
        private const string FormalEvacuationMixedResultEnvironmentVariable =
            "WASTECITY_FORMAL_EVACUATION_MIXED_PERF_RESULT";
        private const string FormalEvacuationMixedGuiProfilerEnvironmentVariable =
            "WASTECITY_FORMAL_EVACUATION_MIXED_GUI_PROFILER_RESULT";
        private const string DefaultFormalEvacuationMixedGuiDirectoryName =
            "wastecity-idea0014-task9-gui";
        private const string DefaultFormalEvacuationMixedGuiFileName =
            "task-09-gui-300frames.data";
        private const string GuiProfilerInputEnvironmentVariable =
            "WASTECITY_GUI_PROFILER_INPUT";
        private const string GuiProfilerResultEnvironmentVariable =
            "WASTECITY_GUI_PROFILER_RESULT";
        private const int RunCount = 5;
        private const int BuildingInstanceCount = 128;
        private const int CompletedBuildingCount = 43;
        private const int ConstructionBuildingCount = 43;
        private const int FirstTerrainWidth = 96;
        private const int FirstTerrainHeight = 64;
        private const int FirstTerrainSeed = 8128;
        private const double FirstTerrainMaximumMedianMilliseconds = 250d;
        private const int RuinsCliffRunCount = 5;
        private const int RuinsCliffStableObservationCount = 300;
        private const int FormalEvacuationMixedSampleCount = 300;
        private const long FormalDefenseAllocationBudgetBytes = 64L * 1024L;
        private const long FormalTransactionAllocationBaselineBytes =
            64L * 1024L;
        private const long FormalTransactionAllocationPerManifestItemBytes =
            8L * 1024L;
        private const string FormalMixedWorkloadFrameMarkerName =
            "WasteCity.Formal.MixedWorkload.Frame";
        private static readonly string[] FormalEvacuationMarkerNames =
        {
            "WasteCity.Formal.Production.Tick",
            "WasteCity.Formal.Defense.Tick",
            "WasteCity.Formal.DefenseHud.Apply",
            "WasteCity.Formal.Evacuation.Tick",
            "WasteCity.Formal.Evacuation.ManifestView.Build",
            "WasteCity.Formal.Evacuation.CapacityPreflight",
            "WasteCity.Formal.Evacuation.Commit"
        };
        private const double
            RuinsCliffLayoutAndBatchingMaximumMedianMilliseconds = 100d;
        private const double
            RuinsCliffTotalInitializationMaximumMedianMilliseconds = 250d;
        private const int RuinsCliffMaximumRendererCount = 2;
        private const int RuinsCliffMaximumPersistentObjectCount = 3;
        private const int RuinsCliffMaximumMaterialSlotCount = 13;
        private static readonly ProfilerMarker RuinsCliffLayoutAndBatchingMarker =
            new ProfilerMarker("WasteCity.RuinsCliff.LayoutAndBatching");
        private static readonly ProfilerMarker RuinsCliffTotalInitializationMarker =
            new ProfilerMarker("WasteCity.RuinsCliff.TotalInitialization");
        private static FormalEvacuationMixedFixture
            liveFormalEvacuationMixedFixture;
        private static bool formalMixedGuiCaptureActive;
        private static bool formalMixedGuiCapturePulsed;
        private static string formalMixedGuiCapturePath;

        static GrayboxPerformanceProbe()
        {
            EditorApplication.playModeStateChanged -=
                HandleFormalProfilerPlayModeStateChanged;
            EditorApplication.playModeStateChanged +=
                HandleFormalProfilerPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -=
                CleanupFormalEvacuationMixedProfilerCapture;
            AssemblyReloadEvents.beforeAssemblyReload +=
                CleanupFormalEvacuationMixedProfilerCapture;
            AssemblyReloadEvents.beforeAssemblyReload -=
                CancelFormalEvacuationMixedGuiCapture;
            AssemblyReloadEvents.beforeAssemblyReload +=
                CancelFormalEvacuationMixedGuiCapture;
        }

        [Serializable]
        private sealed class Result
        {
            public int seed;
            public int width;
            public int height;
            public double[] generationMilliseconds;
            public double medianMilliseconds;
            public int rendererCount;
            public int persistentGeneratedObjectCount;
        }

        [Serializable]
        private sealed class BuildingResult
        {
            public int seed;
            public int width;
            public int height;
            public int instanceCount;
            public int completedCount;
            public int constructionCount;
            public int ruinCount;
            public double[] generationMilliseconds;
            public double medianMilliseconds;
            public int instanceRendererCount;
            public int infrastructureRendererCount;
            public int persistentBuildingObjectCount;
        }

        [Serializable]
        private sealed class FirstTerrainResult
        {
            public int seed;
            public int width;
            public int height;
            public double[] generationMilliseconds;
            public double medianMilliseconds;
            public int formalRendererCount;
            public int formalPersistentObjectCount;
            public int controlWidth;
            public int controlHeight;
            public long managedAllocationBytesAfterWarmup;
        }

        [Serializable]
        private sealed class RuinsCliffPerformanceResult
        {
            public int seed;
            public int width;
            public int height;
            public int placementCount;
            public int ruinsPlacementCount;
            public int cliffPlacementCount;
            public double[] layoutAndBatchingMilliseconds;
            public double layoutAndBatchingMedianMilliseconds;
            public double[] totalInitializationMilliseconds;
            public double totalInitializationMedianMilliseconds;
            public int stableObservationCount;
            public long managedAllocationBytesAcrossStableObservations;
            public int rendererCount;
            public int persistentObjectCount;
            public int vertexCount;
            public int triangleCount;
            public int materialSlotCount;
        }

        [Serializable]
        private sealed class FirstTerrainRuntimeArrayResult
        {
            public string label;
            public string assetPath;
            public string assetGuid;
            public int width;
            public int height;
            public int depth;
            public int mipCount;
            public string format;
            public bool isReadable;
            public long compressedPayloadBytes;
            public long editorNativeMemoryBytes;
        }

        [Serializable]
        private sealed class FirstTerrainRuntimeEvidenceResult
        {
            public string activeScene;
            public string worktreePath;
            public int gameViewTargetWidth;
            public int gameViewTargetHeight;
            public int formalRendererCount;
            public string profileAssetPath;
            public string profileGuid;
            public string materialAssetPath;
            public string materialGuid;
            public string pipelineAssetPath;
            public string pipelineGuid;
            public FirstTerrainRuntimeArrayResult[] arrays;
            public long compressedPayloadBytes;
            public long editorNativeMemoryBytes;
            public double editorNativeToPayloadRatio;
            public long editorObservedDuplicateDifferenceBytes;
            public bool editorNativeIsEditorObserved;
            public bool editorNativeIsGpuMemory;
            public bool presenterDeclaresUpdate;
            public bool presenterDeclaresLateUpdate;
            public bool windowsDevelopmentPlayerMemoryResolved;
        }

        [Serializable]
        private sealed class GuiProfilerResult
        {
            public string inputPath;
            public int firstFrameIndex;
            public int lastFrameIndex;
            public int frameCount;
            public double averageFrameMilliseconds;
            public double averageFramesPerSecond;
            public double minimumFrameMilliseconds;
            public double maximumFrameMilliseconds;
            public GuiProfilerMarkerResult[] adapterMarkers;
        }

        [Serializable]
        private sealed class GuiProfilerMarkerResult
        {
            public string label;
            public string[] acceptedSampleNames;
            public int frameOccurrences;
            public int sampleOccurrences;
            public long descendantGcAllocationBytes;
        }

        [Serializable]
        private sealed class FormalEvacuationMixedPerformanceResult
        {
            public int sampleCount;
            public int productionStateCount;
            public int runnableProductionStateCount;
            public int towerCount;
            public int aliveEnemyCount;
            public bool defenseHudVisible;
            public int defenseHudRefreshCount;
            public bool evacuationManifestVisible;
            public bool evacuationInCombat;
            public int evacuationManifestItemCount;
            public int outerCityEvacuationItemCount;
            public string[] productionStateIds;
            public float[] productionProgressBefore;
            public float[] productionProgressAfter;
            public string[] productionStopReasonsBefore;
            public string[] productionStopReasonsAfter;
            public int[] productionStateAdvanceFrameCounts;
            public int miningNodeAmountBefore;
            public int miningNodeAmountAfter;
            public int ironAmountBefore;
            public int ironAmountAfter;
            public int alloyAmountBefore;
            public int alloyAmountAfter;
            public int cityAmmunitionAmountBefore;
            public int cityAmmunitionAmountAfter;
            public int towerAmmunitionAmountBefore;
            public int towerAmmunitionAmountAfter;
            public int ammunitionAmountBefore;
            public int ammunitionAmountAfter;
            public int productionAdvancedStageCount;
            public int stableAdapterUiObjectCount;
            public int finalUiObjectCount;
            public long stableAdapterManagedAllocationBytes;
            public long stableAdapterProfiledAllocationBytes;
            public long activeDefenseManagedAllocationBytes;
            public long activeDefenseProfiledAllocationBytes;
            public long activeDefenseMeasuredAllocationBytes;
            public long activeDefenseAllocationBudgetBytes;
            public long mixedFrameManagedAllocationBytes;
            public long mixedFrameProfiledAllocationBytes;
            public long transactionManagedAllocationBytes;
            public long transactionProfiledAllocationBytes;
            public long transactionMeasuredAllocationBytes;
            public long transactionAllocationBaselineBytes;
            public long transactionAllocationPerManifestItemBytes;
            public long transactionAllocationBudgetBytes;
            public int transactionCommittedItemCount;
            public double[] mixedFrameMilliseconds;
            public double mixedFrameAverageMilliseconds;
            public double mixedFrameMedianMilliseconds;
            public double mixedFrameMaximumMilliseconds;
            public FormalProfilerMarkerResult[] markers;
        }

        [Serializable]
        private sealed class FormalProfilerMarkerResult
        {
            public string name;
            public long occurrenceCount;
            public long totalNanoseconds;
            public long maximumNanoseconds;
        }

        private readonly struct FormalTransactionAllocationMetrics
        {
            public FormalTransactionAllocationMetrics(
                long currentThreadBytes,
                long profiledBytes,
                int manifestItemCount,
                int committedItemCount)
            {
                CurrentThreadBytes = currentThreadBytes;
                ProfiledBytes = profiledBytes;
                MeasuredBytes = Math.Max(currentThreadBytes, profiledBytes);
                ManifestItemCount = manifestItemCount;
                CommittedItemCount = committedItemCount;
                BudgetBytes = checked(
                    FormalTransactionAllocationBaselineBytes +
                    FormalTransactionAllocationPerManifestItemBytes *
                    manifestItemCount);
            }

            public long CurrentThreadBytes { get; }
            public long ProfiledBytes { get; }
            public long MeasuredBytes { get; }
            public int ManifestItemCount { get; }
            public int CommittedItemCount { get; }
            public long BudgetBytes { get; }
        }

        private sealed class FormalEvacuationMixedFixture
        {
            public GameObject Root;
            public Material Material;
            public GrayboxBuildingSession3D Session;
            public WorldMapModel World;
            public GrayboxProductionController3D ProductionController;
            public GrayboxProductionRuntime3D Production;
            public GrayboxDefenseController3D DefenseController;
            public GrayboxDefenseRuntime3D Defense;
            public GrayboxDefenseHudView3D Hud;
            public GrayboxEvacuationController3D Evacuation;
            public GrayboxBuildingMenuView3D Menu;
            public GrayboxBuildingInputRouter3D Input;
            public GrayboxFormalMixedProfilerHeartbeat3D Heartbeat;
            public Canvas Canvas;

            public int UiObjectCount => Canvas == null
                ? 0
                : Canvas.GetComponentsInChildren<Transform>(true).Length;

            public void TickStableAdapters()
            {
                Input.ProcessCurrentInput();
                Evacuation.Tick(0f, false);
                Menu.ShowEvacuationManifest(
                    Evacuation.CaptureManifestView());
            }
        }

        private sealed class FormalFortressDeploymentRequest :
            IGrayboxDeploymentRequest3D
        {
            public CityMode Mode => CityMode.Fortress;

            public bool TryToggleDeployment(out string failureReason)
            {
                failureReason = string.Empty;
                return true;
            }
        }

        private sealed class FormalNullBuildingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public static FormalNullBuildingPresentation Instance { get; } =
                new FormalNullBuildingPresentation();

            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }

        private readonly struct BuildingRunMetrics
        {
            public BuildingRunMetrics(
                double milliseconds,
                int instanceCount,
                int completedCount,
                int constructionCount,
                int ruinCount,
                int instanceRendererCount,
                int infrastructureRendererCount,
                int persistentBuildingObjectCount)
            {
                Milliseconds = milliseconds;
                InstanceCount = instanceCount;
                CompletedCount = completedCount;
                ConstructionCount = constructionCount;
                RuinCount = ruinCount;
                InstanceRendererCount = instanceRendererCount;
                InfrastructureRendererCount =
                    infrastructureRendererCount;
                PersistentBuildingObjectCount =
                    persistentBuildingObjectCount;
            }

            public double Milliseconds { get; }
            public int InstanceCount { get; }
            public int CompletedCount { get; }
            public int ConstructionCount { get; }
            public int RuinCount { get; }
            public int InstanceRendererCount { get; }
            public int InfrastructureRendererCount { get; }
            public int PersistentBuildingObjectCount { get; }
        }

        private readonly struct RuinsCliffMetrics
        {
            public RuinsCliffMetrics(
                int placementCount,
                int ruinsPlacementCount,
                int cliffPlacementCount,
                int rendererCount,
                int persistentObjectCount,
                int vertexCount,
                int triangleCount,
                int materialSlotCount)
            {
                PlacementCount = placementCount;
                RuinsPlacementCount = ruinsPlacementCount;
                CliffPlacementCount = cliffPlacementCount;
                RendererCount = rendererCount;
                PersistentObjectCount = persistentObjectCount;
                VertexCount = vertexCount;
                TriangleCount = triangleCount;
                MaterialSlotCount = materialSlotCount;
            }

            public int PlacementCount { get; }
            public int RuinsPlacementCount { get; }
            public int CliffPlacementCount { get; }
            public int RendererCount { get; }
            public int PersistentObjectCount { get; }
            public int VertexCount { get; }
            public int TriangleCount { get; }
            public int MaterialSlotCount { get; }
        }

        public static void MeasureWorldGeneration()
        {
            string resultPath = ResolveResultPath();
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
                throw new InvalidOperationException(
                    "Hidden/InternalErrorShader is unavailable.");

            var root = new GameObject("GrayboxPerformanceProbe");
            var material = new Material(shader);
            try
            {
                Transform terrain = NewChild(
                    root.transform,
                    "TerrainRoot");
                Transform resources = NewChild(
                    root.transform,
                    "ResourceRoot");
                Transform obstacles = NewChild(
                    root.transform,
                    "ObstacleRoot");
                GrayboxWorldView3D view =
                    root.AddComponent<GrayboxWorldView3D>();
                view.Configure(
                    terrain,
                    resources,
                    obstacles,
                    material);
                var model = new WorldMapModel(
                    GrayboxSceneBootstrap.WorldWidth,
                    GrayboxSceneBootstrap.WorldHeight,
                    new WorldSeed(
                        GrayboxSceneBootstrap.WorldSeedValue));
                var generationMilliseconds = new double[RunCount];
                int rendererCount = 0;
                int persistentObjectCount = 0;

                for (int run = 0; run < RunCount; run++)
                {
                    long before = Stopwatch.GetTimestamp();
                    view.Generate(model);
                    long after = Stopwatch.GetTimestamp();
                    generationMilliseconds[run] =
                        (after - before) * 1000d /
                        Stopwatch.Frequency;
                    rendererCount = view.WorldRendererCount;
                    persistentObjectCount =
                        view.PersistentGeneratedObjectCount;
                    view.ClearGenerated();
                }

                var sorted = (double[])generationMilliseconds.Clone();
                Array.Sort(sorted);
                var result = new Result
                {
                    seed = GrayboxSceneBootstrap.WorldSeedValue,
                    width = GrayboxSceneBootstrap.WorldWidth,
                    height = GrayboxSceneBootstrap.WorldHeight,
                    generationMilliseconds = generationMilliseconds,
                    medianMilliseconds = sorted[RunCount / 2],
                    rendererCount = rendererCount,
                    persistentGeneratedObjectCount =
                        persistentObjectCount
                };
                File.WriteAllText(
                    resultPath,
                    JsonUtility.ToJson(result, true));
                Debug.Log(
                    "Graybox performance result: " +
                    resultPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        public static void MeasureBuildingPerformance()
        {
            string resultPath = ResolveBuildingResultPath();
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
                throw new InvalidOperationException(
                    "Hidden/InternalErrorShader is unavailable.");

            var samples = new double[RunCount];
            BuildingRunMetrics last = default(BuildingRunMetrics);
            for (var run = 0; run < RunCount; run++)
            {
                last = MeasureBuildingRun(shader);
                samples[run] = last.Milliseconds;
            }

            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);
            var result = new BuildingResult
            {
                seed = GrayboxSceneBootstrap.WorldSeedValue,
                width = GrayboxSceneBootstrap.WorldWidth,
                height = GrayboxSceneBootstrap.WorldHeight,
                instanceCount = last.InstanceCount,
                completedCount = last.CompletedCount,
                constructionCount = last.ConstructionCount,
                ruinCount = last.RuinCount,
                generationMilliseconds = samples,
                medianMilliseconds = sorted[RunCount / 2],
                instanceRendererCount = last.InstanceRendererCount,
                infrastructureRendererCount =
                    last.InfrastructureRendererCount,
                persistentBuildingObjectCount =
                    last.PersistentBuildingObjectCount
            };
            File.WriteAllText(
                resultPath,
                JsonUtility.ToJson(result, true));
            Debug.Log(
                "Graybox building performance result: " +
                resultPath);
        }

        public static void MeasureFirstArtTerrainPerformance()
        {
            string resultPath = ResolveExternalPath(
                FirstTerrainResultEnvironmentVariable,
                true);
            FirstArtTerrainProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
                    FirstArtTerrainAssetBuilder.ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Approved first-art terrain profile is unavailable.");
            }
            if (!profile.TryValidate(out string profileError))
            {
                throw new InvalidOperationException(
                    "Approved first-art terrain profile is invalid: " +
                    profileError);
            }

            Shader fallbackShader = Shader.Find("Hidden/InternalErrorShader");
            if (fallbackShader == null)
            {
                throw new InvalidOperationException(
                    "Hidden/InternalErrorShader is unavailable.");
            }

            var root = new GameObject("FirstArtTerrainPerformanceProbe");
            var fallbackMaterial = new Material(fallbackShader);
            var presenterObject = new GameObject("FirstArtTerrainRenderer");
            GrayboxWorldView3D world = null;
            FirstArtTerrainRenderer3D presenter = null;
            try
            {
                Transform terrain = NewChild(root.transform, "TerrainRoot");
                Transform resources = NewChild(root.transform, "ResourceRoot");
                Transform obstacles = NewChild(root.transform, "ObstacleRoot");
                world = root.AddComponent<GrayboxWorldView3D>();
                world.Configure(
                    terrain,
                    resources,
                    obstacles,
                    fallbackMaterial);
                presenter = presenterObject.AddComponent<
                    FirstArtTerrainRenderer3D>();
                presenter.runInEditMode = true;
                presenter.Configure(profile);
                var model = new WorldMapModel(
                    FirstTerrainWidth,
                    FirstTerrainHeight,
                    new WorldSeed(FirstTerrainSeed));
                var samples = new double[RunCount];
                int rendererCount = 0;
                int persistentObjectCount = 0;
                int controlWidth = 0;
                int controlHeight = 0;

                for (int run = 0; run < RunCount; run++)
                {
                    long before = Stopwatch.GetTimestamp();
                    try
                    {
                        world.Generate(model);
                        if (!presenter.TryPresent(world, false))
                        {
                            throw new InvalidOperationException(
                                "First-art terrain presentation failed: " +
                                presenter.LastPresentationError);
                        }

                        rendererCount = presenter
                            .GetComponentsInChildren<MeshRenderer>(true)
                            .Length;
                        persistentObjectCount = presenter
                            .GetComponentsInChildren<Transform>(true)
                            .Length - 1;
                        controlWidth = presenter.ControlMaps.Width;
                        controlHeight = presenter.ControlMaps.Height;
                    }
                    finally
                    {
                        presenter.ClearPresentation();
                    }
                    long after = Stopwatch.GetTimestamp();
                    samples[run] =
                        (after - before) * 1000d / Stopwatch.Frequency;
                }

                long stableAllocation = MeasureStableTerrainAllocation(
                    world,
                    presenter,
                    model);
                var sorted = (double[])samples.Clone();
                Array.Sort(sorted);
                var result = new FirstTerrainResult
                {
                    seed = FirstTerrainSeed,
                    width = FirstTerrainWidth,
                    height = FirstTerrainHeight,
                    generationMilliseconds = samples,
                    medianMilliseconds = sorted[RunCount / 2],
                    formalRendererCount = rendererCount,
                    formalPersistentObjectCount = persistentObjectCount,
                    controlWidth = controlWidth,
                    controlHeight = controlHeight,
                    managedAllocationBytesAfterWarmup = stableAllocation
                };
                File.WriteAllText(
                    resultPath,
                    JsonUtility.ToJson(result, true));
                ValidateFirstTerrainResult(result);
                Debug.Log(
                    "First-art terrain performance result: " +
                    resultPath);
            }
            finally
            {
                if (presenter != null)
                    presenter.ReleasePresentationSource();
                if (world != null)
                    world.ClearGenerated();
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(fallbackMaterial);
            }
        }

        public static void MeasureRuinsCliffPerformance()
        {
            string resultPath = ResolveExternalPath(
                RuinsCliffResultEnvironmentVariable,
                true);
            string temporaryPath = resultPath + ".tmp";
            DeleteIfPresent(resultPath);
            DeleteIfPresent(temporaryPath);
            try
            {
                FirstArtRuinsCliffProfile3D geometryProfile =
                    AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(
                        FirstArtRuinsCliffAssetBuilder.ProfilePath);
                string geometryError = null;
                if (geometryProfile == null ||
                    !geometryProfile.TryValidate(out geometryError))
                {
                    throw new InvalidOperationException(
                        "Approved ruins/cliff profile is invalid: " +
                        geometryError);
                }

                FirstArtTerrainProfile3D terrainProfile =
                    AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
                        FirstArtTerrainAssetBuilder.ProfilePath);
                string terrainError = null;
                if (terrainProfile == null ||
                    !terrainProfile.TryValidate(out terrainError))
                {
                    throw new InvalidOperationException(
                        "Approved first-art terrain profile is invalid: " +
                        terrainError);
                }

                var layoutSamples = new double[RuinsCliffRunCount];
                RuinsCliffMetrics frozenMetrics = default;
                for (int run = 0; run < RuinsCliffRunCount; run++)
                {
                    RuinsCliffMetrics metrics =
                        MeasureRuinsCliffLayoutAndBatching(
                            geometryProfile,
                            out layoutSamples[run]);
                    if (run == 0)
                        frozenMetrics = metrics;
                    else
                        EnsureMatchingMetrics(frozenMetrics, metrics);
                }

                var totalSamples = new double[RuinsCliffRunCount];
                for (int run = 0; run < RuinsCliffRunCount; run++)
                {
                    RuinsCliffMetrics metrics =
                        MeasureRuinsCliffTotalInitialization(
                            terrainProfile,
                            geometryProfile,
                            out totalSamples[run]);
                    EnsureMatchingMetrics(frozenMetrics, metrics);
                }

                long stableObservationAllocation =
                    MeasureStableRuinsCliffObservationAllocation(
                        terrainProfile,
                        geometryProfile);
                var result = new RuinsCliffPerformanceResult
                {
                    seed = GrayboxSceneBootstrap.WorldSeedValue,
                    width = GrayboxSceneBootstrap.WorldWidth,
                    height = GrayboxSceneBootstrap.WorldHeight,
                    placementCount = frozenMetrics.PlacementCount,
                    ruinsPlacementCount = frozenMetrics.RuinsPlacementCount,
                    cliffPlacementCount = frozenMetrics.CliffPlacementCount,
                    layoutAndBatchingMilliseconds = layoutSamples,
                    layoutAndBatchingMedianMilliseconds = Median(layoutSamples),
                    totalInitializationMilliseconds = totalSamples,
                    totalInitializationMedianMilliseconds = Median(totalSamples),
                    stableObservationCount =
                        RuinsCliffStableObservationCount,
                    managedAllocationBytesAcrossStableObservations =
                        stableObservationAllocation,
                    rendererCount = frozenMetrics.RendererCount,
                    persistentObjectCount = frozenMetrics.PersistentObjectCount,
                    vertexCount = frozenMetrics.VertexCount,
                    triangleCount = frozenMetrics.TriangleCount,
                    materialSlotCount = frozenMetrics.MaterialSlotCount,
                };
                Debug.Log(
                    "Ruins/cliff performance candidate: " +
                    JsonUtility.ToJson(result, true));
                ValidateRuinsCliffResult(result);
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(result, true));
                File.Move(temporaryPath, resultPath);
                Debug.Log(
                    "Ruins/cliff performance result: " + resultPath);
            }
            finally
            {
                DeleteIfPresent(temporaryPath);
            }
        }

        private static RuinsCliffMetrics
            MeasureRuinsCliffLayoutAndBatching(
                FirstArtRuinsCliffProfile3D profile,
                out double milliseconds)
        {
            WorldMapModel model = GrayboxWorldLayout3D.CreateDefault();
            var mapper = new PlanarCoordinateMapper3D(
                model.Width,
                model.Height);
            CountRuinsCliffCells(
                model,
                out int ruinsCount,
                out int cliffCount);
            var root = new GameObject("RuinsCliffPerformanceGeometry");
            FirstArtRuinsCliffCategoryGeometry3D ruins = null;
            FirstArtRuinsCliffCategoryGeometry3D cliffs = null;
            try
            {
                long before = Stopwatch.GetTimestamp();
                using (RuinsCliffLayoutAndBatchingMarker.Auto())
                {
                    IReadOnlyList<FirstArtRuinsCliffPlacement3D> projected =
                        FirstArtRuinsCliffLayout3D.Project(model, mapper);
                    SplitRuinsCliffPlacements(
                        projected,
                        out List<FirstArtRuinsCliffPlacement3D>
                            ruinsPlacements,
                        out List<FirstArtRuinsCliffPlacement3D>
                            cliffPlacements);
                    if (!FirstArtRuinsCliffGeometry3D.TryBuild(
                            profile,
                            ruinsPlacements,
                            root.transform,
                            out ruins,
                            out string ruinsError))
                    {
                        throw new InvalidOperationException(
                            "Ruins layout/batching probe failed: " +
                            ruinsError);
                    }
                    if (!FirstArtRuinsCliffGeometry3D.TryBuild(
                            profile,
                            cliffPlacements,
                            root.transform,
                            out cliffs,
                            out string cliffError))
                    {
                        throw new InvalidOperationException(
                            "Cliff layout/batching probe failed: " +
                            cliffError);
                    }
                }
                long after = Stopwatch.GetTimestamp();
                milliseconds =
                    (after - before) * 1000d / Stopwatch.Frequency;
                return ReadRuinsCliffMetrics(
                    root.transform,
                    ruinsCount + cliffCount,
                    ruinsCount,
                    cliffCount);
            }
            finally
            {
                ruins?.Dispose();
                cliffs?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RuinsCliffMetrics
            MeasureRuinsCliffTotalInitialization(
                FirstArtTerrainProfile3D terrainProfile,
                FirstArtRuinsCliffProfile3D geometryProfile,
                out double milliseconds)
        {
            var root = new GameObject("RuinsCliffTotalPerformanceProbe");
            var presenterObject =
                new GameObject("FirstArtTerrainRenderer");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException(
                    "Hidden/InternalErrorShader is unavailable.");
            }
            var fallbackMaterial = new Material(shader);
            GrayboxWorldView3D world = null;
            FirstArtTerrainRenderer3D presenter = null;
            try
            {
                world = CreatePerformanceWorld(root, fallbackMaterial);
                presenter = presenterObject.AddComponent<
                    FirstArtTerrainRenderer3D>();
                presenter.runInEditMode = true;
                presenter.Configure(terrainProfile, geometryProfile);
                WorldMapModel model = GrayboxWorldLayout3D.CreateDefault();
                CountRuinsCliffCells(
                    model,
                    out int ruinsCount,
                    out int cliffCount);

                long before = Stopwatch.GetTimestamp();
                using (RuinsCliffTotalInitializationMarker.Auto())
                {
                    world.Generate(model);
                    if (!presenter.TryPresent(world, false))
                    {
                        throw new InvalidOperationException(
                            "Ruins/cliff total initialization failed: " +
                            presenter.LastPresentationError);
                    }
                    EnsureBothCategoriesPresented(presenter);
                }
                long after = Stopwatch.GetTimestamp();
                milliseconds =
                    (after - before) * 1000d / Stopwatch.Frequency;
                Transform geometryRoot =
                    presenter.transform.Find("RuntimeGeometry");
                return ReadRuinsCliffMetrics(
                    geometryRoot,
                    ruinsCount + cliffCount,
                    ruinsCount,
                    cliffCount);
            }
            finally
            {
                presenter?.ReleasePresentationSource();
                world?.ClearGenerated();
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(fallbackMaterial);
            }
        }

        private static long MeasureStableRuinsCliffObservationAllocation(
            FirstArtTerrainProfile3D terrainProfile,
            FirstArtRuinsCliffProfile3D geometryProfile)
        {
            var root = new GameObject("RuinsCliffStableAllocationProbe");
            var presenterObject =
                new GameObject("FirstArtTerrainRenderer");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException(
                    "Hidden/InternalErrorShader is unavailable.");
            }
            var fallbackMaterial = new Material(shader);
            GrayboxWorldView3D world = null;
            FirstArtTerrainRenderer3D presenter = null;
            try
            {
                world = CreatePerformanceWorld(root, fallbackMaterial);
                presenter = presenterObject.AddComponent<
                    FirstArtTerrainRenderer3D>();
                presenter.runInEditMode = true;
                presenter.Configure(terrainProfile, geometryProfile);
                world.Generate(GrayboxWorldLayout3D.CreateDefault());
                if (!presenter.TryPresent(world, false))
                {
                    throw new InvalidOperationException(
                        "Ruins/cliff allocation setup failed: " +
                        presenter.LastPresentationError);
                }
                EnsureBothCategoriesPresented(presenter);
                Transform geometryRoot =
                    presenter.transform.Find("RuntimeGeometry");
                MeshRenderer[] renderers = geometryRoot
                    .GetComponentsInChildren<MeshRenderer>(true);
                MeshFilter[] filters = geometryRoot
                    .GetComponentsInChildren<MeshFilter>(true);
                if (renderers.Length != 2 || filters.Length != 2)
                    throw new InvalidOperationException(
                        "Stable allocation setup requires two category batches.");

                int perObservation = ObserveRuinsCliff(
                    presenter,
                    renderers[0],
                    renderers[1],
                    filters[0].sharedMesh,
                    filters[1].sharedMesh);
                int observable = perObservation;
                observable += ObserveRuinsCliff(
                    presenter,
                    renderers[0],
                    renderers[1],
                    filters[0].sharedMesh,
                    filters[1].sharedMesh);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int observation = 0;
                     observation < RuinsCliffStableObservationCount;
                     observation++)
                {
                    observable += ObserveRuinsCliff(
                        presenter,
                        renderers[0],
                        renderers[1],
                        filters[0].sharedMesh,
                        filters[1].sharedMesh);
                }
                long allocation =
                    GC.GetAllocatedBytesForCurrentThread() - before;
                if (observable !=
                    perObservation *
                        (RuinsCliffStableObservationCount + 2))
                {
                    throw new InvalidOperationException(
                        "Ruins/cliff stable observation was incomplete.");
                }
                return allocation;
            }
            finally
            {
                presenter?.ReleasePresentationSource();
                world?.ClearGenerated();
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(fallbackMaterial);
            }
        }

        private static GrayboxWorldView3D CreatePerformanceWorld(
            GameObject root,
            Material fallbackMaterial)
        {
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            GrayboxWorldView3D world =
                root.AddComponent<GrayboxWorldView3D>();
            world.Configure(
                terrain,
                resources,
                obstacles,
                fallbackMaterial);
            return world;
        }

        private static void SplitRuinsCliffPlacements(
            IReadOnlyList<FirstArtRuinsCliffPlacement3D> projected,
            out List<FirstArtRuinsCliffPlacement3D> ruins,
            out List<FirstArtRuinsCliffPlacement3D> cliffs)
        {
            ruins = new List<FirstArtRuinsCliffPlacement3D>();
            cliffs = new List<FirstArtRuinsCliffPlacement3D>();
            for (int index = 0; index < projected.Count; index++)
            {
                FirstArtRuinsCliffPlacement3D placement = projected[index];
                if (placement.Family == FirstArtRuinsCliffFamily3D.Ruins)
                    ruins.Add(placement);
                else if (placement.Family == FirstArtRuinsCliffFamily3D.Cliff)
                    cliffs.Add(placement);
                else
                    throw new InvalidOperationException(
                        "Projected placement has an unknown family.");
            }
        }

        private static void CountRuinsCliffCells(
            WorldMapModel model,
            out int ruins,
            out int cliffs)
        {
            ruins = 0;
            cliffs = 0;
            for (int x = 0; x < model.Width; x++)
            for (int y = 0; y < model.Height; y++)
            {
                WorldTraversalKind traversal = model.Get(x, y).Traversal;
                if (traversal == WorldTraversalKind.Ruins)
                    ruins++;
                else if (traversal == WorldTraversalKind.Cliff)
                    cliffs++;
            }
        }

        private static RuinsCliffMetrics ReadRuinsCliffMetrics(
            Transform geometryRoot,
            int placementCount,
            int ruinsPlacementCount,
            int cliffPlacementCount)
        {
            if (geometryRoot == null)
                throw new InvalidOperationException(
                    "Ruins/cliff geometry root is missing.");
            MeshRenderer[] renderers =
                geometryRoot.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] filters =
                geometryRoot.GetComponentsInChildren<MeshFilter>(true);
            int vertexCount = 0;
            long indexCount = 0;
            for (int index = 0; index < filters.Length; index++)
            {
                Mesh mesh = filters[index].sharedMesh;
                if (mesh == null)
                    throw new InvalidOperationException(
                        "Ruins/cliff category mesh is missing.");
                vertexCount = checked(vertexCount + mesh.vertexCount);
                for (int subMesh = 0;
                     subMesh < mesh.subMeshCount;
                     subMesh++)
                    indexCount = checked(
                        indexCount + mesh.GetIndexCount(subMesh));
            }
            int materialSlotCount = 0;
            for (int index = 0; index < renderers.Length; index++)
                materialSlotCount = checked(
                    materialSlotCount +
                    renderers[index].sharedMaterials.Length);
            if (indexCount % 3L != 0L || indexCount / 3L > int.MaxValue)
                throw new InvalidOperationException(
                    "Ruins/cliff triangle count is invalid.");
            return new RuinsCliffMetrics(
                placementCount,
                ruinsPlacementCount,
                cliffPlacementCount,
                renderers.Length,
                geometryRoot.GetComponentsInChildren<Transform>(true).Length,
                vertexCount,
                checked((int)(indexCount / 3L)),
                materialSlotCount);
        }

        private static void EnsureBothCategoriesPresented(
            FirstArtTerrainRenderer3D presenter)
        {
            if (presenter.RuinsStatus !=
                    FirstArtRuinsCliffPresentationStatus3D.Presented ||
                presenter.CliffStatus !=
                    FirstArtRuinsCliffPresentationStatus3D.Presented)
            {
                throw new InvalidOperationException(
                    "Both ruins and cliff categories must be presented. " +
                    "Ruins=" + presenter.RuinsStatus + ":" +
                    presenter.RuinsError + ", Cliff=" +
                    presenter.CliffStatus + ":" + presenter.CliffError);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int ObserveRuinsCliff(
            FirstArtTerrainRenderer3D presenter,
            MeshRenderer ruinsRenderer,
            MeshRenderer cliffRenderer,
            Mesh ruinsMesh,
            Mesh cliffMesh)
        {
            int value = presenter.IsPresented ? 1 : 0;
            value += presenter.RuinsStatus ==
                FirstArtRuinsCliffPresentationStatus3D.Presented ? 2 : 0;
            value += presenter.CliffStatus ==
                FirstArtRuinsCliffPresentationStatus3D.Presented ? 4 : 0;
            value += ruinsRenderer != null && ruinsRenderer.enabled ? 8 : 0;
            value += cliffRenderer != null && cliffRenderer.enabled ? 16 : 0;
            value += ruinsMesh != null ? ruinsMesh.vertexCount : 0;
            value += cliffMesh != null ? cliffMesh.vertexCount : 0;
            value += string.IsNullOrEmpty(presenter.RuinsError) ? 32 : 0;
            value += string.IsNullOrEmpty(presenter.CliffError) ? 64 : 0;
            return value;
        }

        private static void EnsureMatchingMetrics(
            RuinsCliffMetrics expected,
            RuinsCliffMetrics actual)
        {
            if (expected.PlacementCount != actual.PlacementCount ||
                expected.RuinsPlacementCount != actual.RuinsPlacementCount ||
                expected.CliffPlacementCount != actual.CliffPlacementCount ||
                expected.RendererCount != actual.RendererCount ||
                expected.PersistentObjectCount != actual.PersistentObjectCount ||
                expected.VertexCount != actual.VertexCount ||
                expected.TriangleCount != actual.TriangleCount ||
                expected.MaterialSlotCount != actual.MaterialSlotCount)
            {
                throw new InvalidOperationException(
                    "Ruins/cliff performance metrics changed between runs.");
            }
        }

        private static double Median(double[] samples)
        {
            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);
            return sorted[sorted.Length / 2];
        }

        private static void ValidateRuinsCliffResult(
            RuinsCliffPerformanceResult result)
        {
            if (result.layoutAndBatchingMilliseconds == null ||
                result.layoutAndBatchingMilliseconds.Length !=
                    RuinsCliffRunCount ||
                result.totalInitializationMilliseconds == null ||
                result.totalInitializationMilliseconds.Length !=
                    RuinsCliffRunCount)
            {
                throw new InvalidOperationException(
                    "Ruins/cliff performance evidence requires five samples " +
                    "for both measurements.");
            }
            if (result.layoutAndBatchingMedianMilliseconds >
                RuinsCliffLayoutAndBatchingMaximumMedianMilliseconds)
            {
                throw new InvalidOperationException(
                    "Ruins/cliff layout and batching median exceeded 100 ms: " +
                    result.layoutAndBatchingMedianMilliseconds);
            }
            if (result.totalInitializationMedianMilliseconds >
                RuinsCliffTotalInitializationMaximumMedianMilliseconds)
            {
                throw new InvalidOperationException(
                    "Ruins/cliff total initialization median exceeded 250 ms: " +
                    result.totalInitializationMedianMilliseconds);
            }
            if (result.stableObservationCount !=
                    RuinsCliffStableObservationCount ||
                result.managedAllocationBytesAcrossStableObservations != 0L)
            {
                throw new InvalidOperationException(
                    "The 300 stable ruins/cliff observations allocated " +
                    result.managedAllocationBytesAcrossStableObservations +
                    " managed bytes.");
            }
            if (result.placementCount <= 0 ||
                result.ruinsPlacementCount <= 0 ||
                result.cliffPlacementCount <= 0 ||
                result.placementCount !=
                    result.ruinsPlacementCount + result.cliffPlacementCount)
            {
                throw new InvalidOperationException(
                    "Ruins/cliff placement counts are incomplete.");
            }
            if (result.rendererCount != RuinsCliffMaximumRendererCount)
                throw new InvalidOperationException(
                    "Ruins/cliff presentation must use two Renderers.");
            if (result.persistentObjectCount >
                    RuinsCliffMaximumPersistentObjectCount ||
                result.persistentObjectCount < result.rendererCount)
                throw new InvalidOperationException(
                    "Ruins/cliff persistent object count is outside budget.");
            if (result.vertexCount <= 0 || result.triangleCount <= 0)
                throw new InvalidOperationException(
                    "Ruins/cliff geometry counts must be positive.");
            if (result.materialSlotCount <= 0 ||
                result.materialSlotCount >
                    RuinsCliffMaximumMaterialSlotCount)
                throw new InvalidOperationException(
                    "Ruins/cliff material slot count exceeded its budget of 13.");
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        [MenuItem("WasteCity/Performance/Record First Terrain Runtime Evidence")]
        public static void RecordFirstArtTerrainRuntimeEvidence()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "First terrain runtime evidence requires Play Mode.");
            }

            string resultPath = ResolveExternalPath(
                FirstTerrainRuntimeResultEnvironmentVariable,
                true);
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(
                    scene.path,
                    "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "First terrain runtime evidence requires GrayboxPrototype3D.");
            }

            FirstArtTerrainRenderer3D[] presenters =
                UnityEngine.Object.FindObjectsOfType<FirstArtTerrainRenderer3D>(true);
            if (presenters.Length != 1 || !presenters[0].IsPresented)
            {
                throw new InvalidOperationException(
                    "First terrain runtime evidence requires one live formal presenter.");
            }

            FirstArtTerrainRenderer3D presenter = presenters[0];
            FirstArtTerrainProfile3D profile = presenter.Profile;
            string profileError = null;
            if (profile == null || !profile.TryValidate(out profileError))
            {
                throw new InvalidOperationException(
                    "First terrain runtime profile is invalid: " + profileError);
            }

            Texture2DArray[] arrays =
            {
                profile.BaseColorArray,
                profile.NormalArray,
                profile.MaskArray,
                profile.HeightArray,
            };
            string[] labels = { "BaseColor", "Normal", "Mask", "Height" };
            var arrayResults = new FirstTerrainRuntimeArrayResult[arrays.Length];
            long compressedPayload = 0L;
            long editorNativeMemory = 0L;
            for (int index = 0; index < arrays.Length; index++)
            {
                Texture2DArray array = arrays[index];
                string arrayPath = AssetDatabase.GetAssetPath(array);
                long payload =
                    FirstArtTerrainEvidenceCapture.CalculateCompressedPayloadBytes(array);
                long native = Profiler.GetRuntimeMemorySizeLong(array);
                compressedPayload += payload;
                editorNativeMemory += native;
                arrayResults[index] = new FirstTerrainRuntimeArrayResult
                {
                    label = labels[index],
                    assetPath = arrayPath,
                    assetGuid = AssetDatabase.AssetPathToGUID(arrayPath),
                    width = array.width,
                    height = array.height,
                    depth = array.depth,
                    mipCount = array.mipmapCount,
                    format = array.format.ToString(),
                    isReadable = array.isReadable,
                    compressedPayloadBytes = payload,
                    editorNativeMemoryBytes = native,
                };
            }

            string profilePath = AssetDatabase.GetAssetPath(profile);
            string materialPath = AssetDatabase.GetAssetPath(profile.Material);
            RenderPipelineAsset pipeline = GraphicsSettings.renderPipelineAsset;
            string pipelinePath = AssetDatabase.GetAssetPath(pipeline);
            const BindingFlags declaredInstance =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;
            bool declaresUpdate =
                typeof(FirstArtTerrainRenderer3D).GetMethod("Update", declaredInstance) != null;
            bool declaresLateUpdate =
                typeof(FirstArtTerrainRenderer3D).GetMethod("LateUpdate", declaredInstance) != null;
            long duplicateDifference = Math.Abs(
                editorNativeMemory - compressedPayload * 2L);
            var result = new FirstTerrainRuntimeEvidenceResult
            {
                activeScene = scene.path,
                worktreePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                gameViewTargetWidth = 1920,
                gameViewTargetHeight = 1080,
                formalRendererCount = presenter
                    .GetComponentsInChildren<MeshRenderer>(true).Length,
                profileAssetPath = profilePath,
                profileGuid = AssetDatabase.AssetPathToGUID(profilePath),
                materialAssetPath = materialPath,
                materialGuid = AssetDatabase.AssetPathToGUID(materialPath),
                pipelineAssetPath = pipelinePath,
                pipelineGuid = AssetDatabase.AssetPathToGUID(pipelinePath),
                arrays = arrayResults,
                compressedPayloadBytes = compressedPayload,
                editorNativeMemoryBytes = editorNativeMemory,
                editorNativeToPayloadRatio =
                    compressedPayload == 0L
                        ? 0d
                        : editorNativeMemory / (double)compressedPayload,
                editorObservedDuplicateDifferenceBytes = duplicateDifference,
                editorNativeIsEditorObserved = true,
                editorNativeIsGpuMemory = false,
                presenterDeclaresUpdate = declaresUpdate,
                presenterDeclaresLateUpdate = declaresLateUpdate,
                windowsDevelopmentPlayerMemoryResolved = false,
            };
            ValidateFirstTerrainRuntimeEvidence(result);
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("First terrain runtime evidence result: " + resultPath);
        }

        [MenuItem(
            "WasteCity/Performance/Prepare Formal Evacuation Mixed Profiler")]
        public static void PrepareFormalEvacuationMixedProfilerCapture()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Formal evacuation mixed Profiler preparation requires Play Mode.");

            CleanupFormalEvacuationMixedProfilerCapture();
            FormalEvacuationMixedFixture fixture =
                CreateFormalEvacuationMixedFixture();
            fixture.Root.name = "FormalEvacuationMixedProfiler.LiveFixture";
            fixture.Root.hideFlags = HideFlags.DontSave;
            liveFormalEvacuationMixedFixture = fixture;

            EvacuationManifestViewModel manifest =
                fixture.Evacuation.CaptureManifestView();
            Debug.Log(
                "Formal evacuation mixed Profiler fixture is live: " +
                fixture.Production.States.Count +
                " production states, " +
                fixture.Defense.Snapshot.AliveEnemyCount +
                " enemies, " + manifest.Items.Count +
                " manifest items. Required capture order: warm up first; " +
                "then start a capture of exactly 300 frames; while that " +
                "window is recording run Pulse Formal Evacuation Transaction " +
                "Markers exactly once; save the capture; finally run Cleanup " +
                "Formal Evacuation Mixed Profiler.");
        }

        [MenuItem(
            "WasteCity/Performance/Cleanup Formal Evacuation Mixed Profiler")]
        public static void CleanupFormalEvacuationMixedProfilerCapture()
        {
            CancelFormalEvacuationMixedGuiCapture();
            FormalEvacuationMixedFixture fixture =
                liveFormalEvacuationMixedFixture;
            liveFormalEvacuationMixedFixture = null;
            if (fixture == null)
                return;
            if (fixture.Root != null)
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            if (fixture.Material != null)
                UnityEngine.Object.DestroyImmediate(fixture.Material);
            Debug.Log("Formal evacuation mixed Profiler fixture cleaned up.");
        }

        [MenuItem(
            "WasteCity/Performance/Capture Formal Evacuation Mixed 300 Frames")]
        public static void CaptureFormalEvacuationMixedProfiler300Frames()
        {
            if (!EditorApplication.isPlaying ||
                liveFormalEvacuationMixedFixture == null ||
                liveFormalEvacuationMixedFixture.Root == null)
                throw new InvalidOperationException(
                    "Prepare the live formal evacuation mixed Profiler fixture in Play Mode first.");
            if (formalMixedGuiCaptureActive)
                throw new InvalidOperationException(
                    "Formal evacuation mixed 300-frame capture is already running.");

            string configured = Environment.GetEnvironmentVariable(
                FormalEvacuationMixedGuiProfilerEnvironmentVariable);
            string outputPath;
            if (string.IsNullOrWhiteSpace(configured))
            {
                outputPath = Path.GetFullPath(Path.Combine(
                    Path.GetTempPath(),
                    DefaultFormalEvacuationMixedGuiDirectoryName,
                    DefaultFormalEvacuationMixedGuiFileName));
                Directory.CreateDirectory(
                    Path.GetDirectoryName(outputPath));
            }
            else
            {
                outputPath = ResolveExternalPath(
                    FormalEvacuationMixedGuiProfilerEnvironmentVariable,
                    true);
            }
            DeleteIfPresent(outputPath);

            liveFormalEvacuationMixedFixture.Heartbeat.ResetPulseTracking();
            ProfilerDriver.enabled = false;
            ProfilerDriver.ClearAllFrames();
            formalMixedGuiCapturePath = outputPath;
            formalMixedGuiCapturePulsed = false;
            formalMixedGuiCaptureActive = true;
            EditorApplication.update -= TickFormalEvacuationMixedGuiCapture;
            EditorApplication.update += TickFormalEvacuationMixedGuiCapture;
            ProfilerDriver.enabled = true;
            Debug.Log(
                "Formal evacuation mixed automatic Profiler capture started; " +
                "it will record exactly 300 frames and pulse the isolated " +
                "transaction once near frame 100: " + outputPath);
        }

        private static void TickFormalEvacuationMixedGuiCapture()
        {
            if (!formalMixedGuiCaptureActive)
                return;
            try
            {
                if (!EditorApplication.isPlaying ||
                    liveFormalEvacuationMixedFixture == null ||
                    liveFormalEvacuationMixedFixture.Root == null)
                    throw new InvalidOperationException(
                        "Play Mode or the live formal mixed fixture ended during capture.");

                int frameCount = CurrentFormalProfilerFrameCount();
                GrayboxFormalMixedProfilerHeartbeat3D heartbeat =
                    liveFormalEvacuationMixedFixture.Heartbeat;
                if (heartbeat.PulseCompletionCount > 1)
                    throw new InvalidOperationException(
                        "Formal mixed PlayMode heartbeat pulsed more than once.");
                if (!formalMixedGuiCapturePulsed && frameCount >= 100)
                {
                    if (heartbeat.PulseCompletionCount == 0)
                        heartbeat.RequestPulse();
                    else
                        formalMixedGuiCapturePulsed = true;
                }
                if (frameCount < FormalEvacuationMixedSampleCount)
                    return;

                ProfilerDriver.enabled = false;
                frameCount = CurrentFormalProfilerFrameCount();
                if (frameCount != FormalEvacuationMixedSampleCount ||
                    !formalMixedGuiCapturePulsed ||
                    heartbeat.PulseCompletionCount != 1)
                    throw new InvalidOperationException(
                        "Formal mixed GUI Profiler capture must stop at exactly " +
                        "300 frames after one transaction pulse; found " +
                        frameCount + ".");
                ProfilerDriver.SaveProfile(formalMixedGuiCapturePath);
                if (!File.Exists(formalMixedGuiCapturePath))
                    throw new InvalidOperationException(
                        "Formal mixed GUI Profiler data was not saved.");

                string completedPath = formalMixedGuiCapturePath;
                EndFormalEvacuationMixedGuiCapture();
                Debug.Log(
                    "Formal evacuation mixed automatic Profiler capture " +
                    "completed by the PlayMode heartbeat: 300 frames, " +
                    "transaction pulse exactly once, " +
                    "saved to " + completedPath +
                    ". The live fixture remains available for screenshots; " +
                    "run Cleanup Formal Evacuation Mixed Profiler afterward.");
            }
            catch (Exception exception)
            {
                CancelFormalEvacuationMixedGuiCapture();
                Debug.LogException(exception);
            }
        }

        private static int CurrentFormalProfilerFrameCount()
        {
            int firstFrame = ProfilerDriver.firstFrameIndex;
            int lastFrame = ProfilerDriver.lastFrameIndex;
            return lastFrame >= firstFrame
                ? lastFrame - firstFrame + 1
                : 0;
        }

        private static void CancelFormalEvacuationMixedGuiCapture()
        {
            if (!formalMixedGuiCaptureActive)
                return;
            ProfilerDriver.enabled = false;
            EndFormalEvacuationMixedGuiCapture();
            Debug.Log(
                "Formal evacuation mixed automatic Profiler capture cancelled; " +
                "Profiler recording was disabled.");
        }

        private static void EndFormalEvacuationMixedGuiCapture()
        {
            EditorApplication.update -= TickFormalEvacuationMixedGuiCapture;
            formalMixedGuiCaptureActive = false;
            formalMixedGuiCapturePulsed = false;
            formalMixedGuiCapturePath = null;
        }

        [MenuItem(
            "WasteCity/Performance/Pulse Formal Evacuation Transaction Markers")]
        public static void PulseFormalEvacuationTransactionalMarkersForProfiler()
        {
            if (!EditorApplication.isPlaying ||
                liveFormalEvacuationMixedFixture == null)
                throw new InvalidOperationException(
                    "Prepare the live formal evacuation mixed Profiler fixture in Play Mode first.");

            FormalEvacuationMixedFixture transactionFixture = null;
            try
            {
                transactionFixture = CreateFormalEvacuationMixedFixture();
                FormalTransactionAllocationMetrics transaction =
                    ExecuteFormalQuickDismantleTransaction(
                        transactionFixture);
                Debug.Log(
                    "Formal evacuation preflight/commit markers pulsed once " +
                    "from an isolated fixture; this single-frame transaction " +
                    "is not part of the stable per-frame GC window. " +
                    "Measured allocation=" + transaction.MeasuredBytes +
                    " B, linear budget=" + transaction.BudgetBytes +
                    " B, committed=" + transaction.CommittedItemCount + ".");
            }
            finally
            {
                if (transactionFixture?.Root != null)
                    UnityEngine.Object.DestroyImmediate(transactionFixture.Root);
                if (transactionFixture?.Material != null)
                    UnityEngine.Object.DestroyImmediate(
                        transactionFixture.Material);
            }
        }

        private static void HandleFormalProfilerPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                CancelFormalEvacuationMixedGuiCapture();
                CleanupFormalEvacuationMixedProfilerCapture();
            }
        }

        public static void MeasureFormalEvacuationMixedPerformance()
        {
            string resultPath = ResolveFormalEvacuationMixedResultPath();
            string temporaryPath = resultPath + ".tmp";
            DeleteIfPresent(resultPath);
            DeleteIfPresent(temporaryPath);
            FormalEvacuationMixedFixture fixture = null;
            ProfilerRecorder[] markerRecorders = null;
            try
            {
                fixture = CreateFormalEvacuationMixedFixture();
                EvacuationManifestViewModel manifest =
                    fixture.Evacuation.CaptureManifestView();
                int initialProductionStateCount =
                    fixture.Production.States.Count;
                int initialRunnableProductionStateCount =
                    fixture.Production.RunnableStates.Count;
                GrayboxDefenseRuntimeSnapshot3D initialDefense =
                    fixture.Defense.Snapshot;
                int outerCityItemCount = 0;
                for (int index = 0;
                     index < fixture.Session.Instances.Count;
                     index++)
                {
                    if (fixture.Session.Instances[index].Placement.Site ==
                        BuildingSite.Ground)
                        outerCityItemCount++;
                }
                ValidateFormalEvacuationMixedFixture(
                    fixture,
                    manifest,
                    initialDefense,
                    outerCityItemCount);
                CaptureFormalProductionStateEvidence(
                    fixture,
                    out string[] productionStateIds,
                    out float[] productionProgressBefore,
                    out string[] productionStopReasonsBefore);
                int miningNodeAmountBefore =
                    CaptureFormalMiningNodeAmount(fixture.World);
                int ironAmountBefore = fixture.Session.GetCityResourceAmount(
                    ResourceIds.Iron);
                int alloyAmountBefore = fixture.Session.GetCityResourceAmount(
                    ResourceIds.Alloy);
                int cityAmmunitionAmountBefore =
                    fixture.Session.GetCityResourceAmount(
                        ResourceIds.Ammunition);
                int towerAmmunitionAmountBefore =
                    CaptureFormalTowerAmmunition(fixture.Defense);
                int ammunitionAmountBefore = checked(
                    cityAmmunitionAmountBefore +
                    towerAmmunitionAmountBefore);

                for (int warmup = 0; warmup < 12; warmup++)
                    fixture.TickStableAdapters();
                int stableUiObjectCount = fixture.UiObjectCount;

                markerRecorders = StartFormalMarkerRecorders();
                MeasureManagedAllocations(
                    FormalEvacuationMixedSampleCount,
                    fixture.TickStableAdapters,
                    out long stableCurrentThreadBytes,
                    out long stableProfiledBytes);

                int defenseObservable = 0;
                MeasureManagedAllocations(
                    FormalEvacuationMixedSampleCount,
                    () =>
                    {
                        fixture.Defense.Tick(
                            .1f,
                            false,
                            fixture.Session.CityStorage);
                        GrayboxDefenseRuntimeSnapshot3D snapshot =
                            fixture.Defense.Snapshot;
                        defenseObservable += snapshot.AliveEnemyCount;
                        if (snapshot.Enemies.Count > 0)
                            defenseObservable +=
                                snapshot.Enemies[0].CurrentHealth;
                    },
                    out long defenseCurrentThreadBytes,
                    out long defenseProfiledBytes);
                long defenseMeasuredBytes = Math.Max(
                    defenseCurrentThreadBytes,
                    defenseProfiledBytes);
                if (defenseObservable <= 0)
                    throw new InvalidOperationException(
                        "Formal active-defense observation was incomplete.");

                var frameMilliseconds = new double[
                    FormalEvacuationMixedSampleCount];
                var productionProgressAtFrameStart = new float[
                    productionStateIds.Length];
                var productionStateAdvanceFrameCounts = new int[
                    productionStateIds.Length];
                ProfilerRecorder mixedGcRecorder = StartGcAllocationRecorder();
                int mixedObservable = 0;
                long mixedCurrentThreadBytes = 0L;
                long mixedProfiledBytes = 0L;
                try
                {
                    long mixedBefore = GC.GetAllocatedBytesForCurrentThread();
                    for (int sample = 0;
                         sample < FormalEvacuationMixedSampleCount;
                         sample++)
                    {
                        long before = Stopwatch.GetTimestamp();
                        for (int index = 0;
                             index < productionStateIds.Length;
                             index++)
                        {
                            BuildingProductionState state =
                                fixture.Production.States[index];
                            if (!string.Equals(
                                    state.StableInstanceId,
                                    productionStateIds[index],
                                    StringComparison.Ordinal))
                                throw new InvalidOperationException(
                                    "Formal production state ordering changed during measurement.");
                            productionProgressAtFrameStart[index] =
                                state.ProgressSeconds;
                        }
                        if (!fixture.ProductionController.Tick(.1f, false))
                            throw new InvalidOperationException(
                                "Formal production controller rejected a mixed frame.");
                        if (!fixture.DefenseController.Tick(.1f, false))
                            throw new InvalidOperationException(
                                "Formal defense controller rejected a mixed frame.");
                        GrayboxDefenseRuntimeSnapshot3D defense =
                            fixture.DefenseController.Snapshot;
                        fixture.TickStableAdapters();
                        for (int index = 0;
                             index < productionStateIds.Length;
                             index++)
                        {
                            BuildingProductionState state =
                                fixture.Production.States[index];
                            if (state.StopReason == ProductionStopReason.None &&
                                Math.Abs(
                                    state.ProgressSeconds -
                                    productionProgressAtFrameStart[index]) >
                                .000001f)
                                productionStateAdvanceFrameCounts[index]++;
                        }
                        long after = Stopwatch.GetTimestamp();
                        frameMilliseconds[sample] =
                            (after - before) * 1000d / Stopwatch.Frequency;
                        mixedObservable += defense.AliveEnemyCount;
                        mixedObservable += fixture.Production.States.Count;
                    }
                    mixedCurrentThreadBytes =
                        GC.GetAllocatedBytesForCurrentThread() - mixedBefore;
                }
                finally
                {
                    mixedGcRecorder.Stop();
                    mixedProfiledBytes = SumGcAllocationBytes(
                        mixedGcRecorder);
                    mixedGcRecorder.Dispose();
                }
                if (mixedObservable <= 0)
                    throw new InvalidOperationException(
                        "Formal mixed-frame observation was incomplete.");
                int finalStableUiObjectCount = fixture.UiObjectCount;
                CaptureFormalProductionStateEvidence(
                    fixture,
                    out _,
                    out float[] productionProgressAfter,
                    out string[] productionStopReasonsAfter);
                int miningNodeAmountAfter =
                    CaptureFormalMiningNodeAmount(fixture.World);
                int ironAmountAfter = fixture.Session.GetCityResourceAmount(
                    ResourceIds.Iron);
                int alloyAmountAfter = fixture.Session.GetCityResourceAmount(
                    ResourceIds.Alloy);
                int cityAmmunitionAmountAfter =
                    fixture.Session.GetCityResourceAmount(
                        ResourceIds.Ammunition);
                int towerAmmunitionAmountAfter =
                    CaptureFormalTowerAmmunition(fixture.Defense);
                int ammunitionAmountAfter = checked(
                    cityAmmunitionAmountAfter +
                    towerAmmunitionAmountAfter);
                int productionAdvancedStageCount = 0;
                if (miningNodeAmountAfter < miningNodeAmountBefore)
                    productionAdvancedStageCount++;
                // The fixture starts with four alloy, which can yield at most
                // four ammunition. Any larger total across city + tower proves
                // that smelters supplied additional alloy to the assembler.
                if (ammunitionAmountAfter - ammunitionAmountBefore > 4)
                    productionAdvancedStageCount++;
                // Any positive city + tower ammunition delta proves that the
                // assembler itself completed at least one production cycle.
                if (ammunitionAmountAfter > ammunitionAmountBefore)
                    productionAdvancedStageCount++;

                // Exercise the formal preflight and commit paths only after
                // the stable manifest and active-frame samples are frozen.
                FormalTransactionAllocationMetrics transaction =
                    ExecuteFormalQuickDismantleTransaction(fixture);

                FormalProfilerMarkerResult[] markerResults =
                    StopAndReadFormalMarkerRecorders(markerRecorders);
                markerRecorders = null;
                double frameTotal = 0d;
                double frameMaximum = 0d;
                for (int index = 0; index < frameMilliseconds.Length; index++)
                {
                    frameTotal += frameMilliseconds[index];
                    frameMaximum = Math.Max(
                        frameMaximum,
                        frameMilliseconds[index]);
                }
                var result = new FormalEvacuationMixedPerformanceResult
                {
                    sampleCount = FormalEvacuationMixedSampleCount,
                    productionStateCount = initialProductionStateCount,
                    runnableProductionStateCount =
                        initialRunnableProductionStateCount,
                    towerCount = initialDefense.Towers.Count,
                    aliveEnemyCount = initialDefense.AliveEnemyCount,
                    defenseHudVisible =
                        fixture.Hud.SummaryRect != null &&
                        fixture.Hud.SummaryRect.gameObject.activeInHierarchy,
                    defenseHudRefreshCount = fixture.Hud.RefreshCount,
                    evacuationManifestVisible = true,
                    evacuationInCombat = manifest.IsInCombat,
                    evacuationManifestItemCount = manifest.Items.Count,
                    outerCityEvacuationItemCount = outerCityItemCount,
                    productionStateIds = productionStateIds,
                    productionProgressBefore = productionProgressBefore,
                    productionProgressAfter = productionProgressAfter,
                    productionStopReasonsBefore =
                        productionStopReasonsBefore,
                    productionStopReasonsAfter = productionStopReasonsAfter,
                    productionStateAdvanceFrameCounts =
                        productionStateAdvanceFrameCounts,
                    miningNodeAmountBefore = miningNodeAmountBefore,
                    miningNodeAmountAfter = miningNodeAmountAfter,
                    ironAmountBefore = ironAmountBefore,
                    ironAmountAfter = ironAmountAfter,
                    alloyAmountBefore = alloyAmountBefore,
                    alloyAmountAfter = alloyAmountAfter,
                    cityAmmunitionAmountBefore =
                        cityAmmunitionAmountBefore,
                    cityAmmunitionAmountAfter = cityAmmunitionAmountAfter,
                    towerAmmunitionAmountBefore =
                        towerAmmunitionAmountBefore,
                    towerAmmunitionAmountAfter = towerAmmunitionAmountAfter,
                    ammunitionAmountBefore = ammunitionAmountBefore,
                    ammunitionAmountAfter = ammunitionAmountAfter,
                    productionAdvancedStageCount =
                        productionAdvancedStageCount,
                    stableAdapterUiObjectCount = stableUiObjectCount,
                    finalUiObjectCount = finalStableUiObjectCount,
                    stableAdapterManagedAllocationBytes =
                        stableCurrentThreadBytes,
                    stableAdapterProfiledAllocationBytes = stableProfiledBytes,
                    activeDefenseManagedAllocationBytes =
                        defenseCurrentThreadBytes,
                    activeDefenseProfiledAllocationBytes = defenseProfiledBytes,
                    activeDefenseMeasuredAllocationBytes = defenseMeasuredBytes,
                    activeDefenseAllocationBudgetBytes =
                        FormalDefenseAllocationBudgetBytes,
                    mixedFrameManagedAllocationBytes = mixedCurrentThreadBytes,
                    mixedFrameProfiledAllocationBytes = mixedProfiledBytes,
                    transactionManagedAllocationBytes =
                        transaction.CurrentThreadBytes,
                    transactionProfiledAllocationBytes =
                        transaction.ProfiledBytes,
                    transactionMeasuredAllocationBytes =
                        transaction.MeasuredBytes,
                    transactionAllocationBaselineBytes =
                        FormalTransactionAllocationBaselineBytes,
                    transactionAllocationPerManifestItemBytes =
                        FormalTransactionAllocationPerManifestItemBytes,
                    transactionAllocationBudgetBytes =
                        transaction.BudgetBytes,
                    transactionCommittedItemCount =
                        transaction.CommittedItemCount,
                    mixedFrameMilliseconds = frameMilliseconds,
                    mixedFrameAverageMilliseconds =
                        frameTotal / frameMilliseconds.Length,
                    mixedFrameMedianMilliseconds = Median(frameMilliseconds),
                    mixedFrameMaximumMilliseconds = frameMaximum,
                    markers = markerResults
                };
                Debug.Log(
                    "Formal evacuation mixed performance candidate: " +
                    JsonUtility.ToJson(result, true));
                ValidateFormalEvacuationMixedResult(result);
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(result, true));
                File.Move(temporaryPath, resultPath);
                Debug.Log(
                    "Formal evacuation mixed performance result: " +
                    resultPath);
            }
            finally
            {
                if (markerRecorders != null)
                    DisposeRecorders(markerRecorders);
                if (fixture?.Root != null)
                    UnityEngine.Object.DestroyImmediate(fixture.Root);
                if (fixture?.Material != null)
                    UnityEngine.Object.DestroyImmediate(fixture.Material);
                DeleteIfPresent(temporaryPath);
            }
        }

        private static FormalEvacuationMixedFixture
            CreateFormalEvacuationMixedFixture()
        {
            var root = new GameObject("FormalEvacuationMixedProbe");
            Material material = null;
            try
            {
                GrayboxBuildingSession3D session = NewChild(
                        root.transform,
                        "Session")
                    .gameObject.AddComponent<GrayboxBuildingSession3D>();
                session.Configure(true);
                session.ConfigureDevelopmentFixture();
                session.UnlockAllResearchForDevelopment();
                session.Inventory.Set(ResourceIds.Iron, 5000);
                session.Inventory.Set(ResourceIds.Alloy, 5000);
                session.Inventory.Set(ResourceIds.Stone, 5000);
                session.Inventory.Set(ResourceIds.Ammunition, 5000);

                WorldMapModel world = CreateFormalProductionWorld();
                BuildFormalMixedPopulation(session);
                // Prime both smelter input buffers so the 2:2:1 formal chain
                // starts as an active mixed workload rather than allowing the
                // stable-ID-first smelter to consume the entire startup buffer.
                session.Inventory.Set(ResourceIds.Iron, 40);
                session.Inventory.Set(ResourceIds.Alloy, 4);
                session.Inventory.Set(ResourceIds.Ammunition, 0);
                session.Inventory.Set(ResourceIds.Stone, 0);
                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                if (shader == null)
                    throw new InvalidOperationException(
                        "Hidden/InternalErrorShader is unavailable.");
                material = new Material(shader);
                Transform worldRoot = NewChild(root.transform, "World");
                GrayboxWorldView3D worldView = worldRoot.gameObject
                    .AddComponent<GrayboxWorldView3D>();
                worldView.Configure(
                    NewChild(worldRoot, "Terrain"),
                    NewChild(worldRoot, "Resources"),
                    NewChild(worldRoot, "Obstacles"),
                    material);
                worldView.Generate(world);
                Transform cityTransform = NewChild(root.transform, "City");
                if (!worldView.Coordinates.TryCellToWorld(
                        12,
                        12,
                        .5f,
                        out Vector3 cityPosition))
                    throw new InvalidOperationException(
                        "Formal mixed city coordinate is unavailable.");
                cityTransform.position = cityPosition;
                Rigidbody cityBody = cityTransform.gameObject
                    .AddComponent<Rigidbody>();
                BoxCollider cityCollider = cityTransform.gameObject
                    .AddComponent<BoxCollider>();
                GrayboxMobileCityController3D city = cityTransform.gameObject
                    .AddComponent<GrayboxMobileCityController3D>();
                city.Configure(worldView, cityBody, cityCollider);
                if (!city.RestoreDeploymentForDevelopment(CityMode.Fortress))
                    throw new InvalidOperationException(
                        "Formal mixed city could not enter Fortress mode.");
                GrayboxProductionController3D productionController = NewChild(
                        root.transform,
                        "Production")
                    .gameObject.AddComponent<GrayboxProductionController3D>();
                productionController.Configure(session, city, worldView);
                if (!productionController.Tick(.5f, false))
                    throw new InvalidOperationException(
                        "Formal mixed production setup failed.");
                GrayboxProductionRuntime3D production =
                    productionController.Clock.Runtime;

                var defense = new GrayboxDefenseRuntime3D(
                    12f,
                    12f,
                    1000f,
                    12f);
                defense.Synchronize(
                    session.Instances,
                    CityMode.Fortress,
                    12,
                    12,
                    session.GroundBuildRadius);
                defense.Tick(55f, false, session.CityStorage);

                Canvas canvas = NewChild(root.transform, "Canvas")
                    .gameObject.AddComponent<Canvas>();
                EventSystem eventSystem = NewChild(
                        root.transform,
                        "EventSystem")
                    .gameObject.AddComponent<EventSystem>();
                GrayboxBuildingInteractionModel3D interaction = NewChild(
                        root.transform,
                        "Interaction")
                    .gameObject.AddComponent<
                        GrayboxBuildingInteractionModel3D>();
                GrayboxBuildingMenuView3D menu = NewChild(
                        root.transform,
                        "BuildingMenu")
                    .gameObject.AddComponent<GrayboxBuildingMenuView3D>();
                menu.Configure(canvas, eventSystem, session, interaction);
                GrayboxDefenseHudView3D hud = NewChild(
                        root.transform,
                        "DefenseHud")
                    .gameObject.AddComponent<GrayboxDefenseHudView3D>();
                hud.Configure(canvas, eventSystem);
                hud.Apply(
                    defense.Snapshot,
                    GrayboxDefenseSelectionKind3D.None,
                    null);
                GrayboxBuildingWorldView3D buildingPresentation = NewChild(
                        root.transform,
                        "BuildingPresentation")
                    .gameObject.AddComponent<GrayboxBuildingWorldView3D>();
                buildingPresentation.Configure(
                    NewChild(buildingPresentation.transform, "Instances"),
                    NewChild(
                        buildingPresentation.transform,
                        "Infrastructure"),
                    material,
                    city);
                GrayboxDefenseWorldView3D defenseWorldView = NewChild(
                        root.transform,
                        "DefenseWorld")
                    .gameObject.AddComponent<GrayboxDefenseWorldView3D>();
                defenseWorldView.Configure(
                    NewChild(defenseWorldView.transform, "Enemies"),
                    NewChild(defenseWorldView.transform, "Towers"),
                    material,
                    worldView.Coordinates);
                GrayboxDefenseController3D defenseController = NewChild(
                        root.transform,
                        "Defense")
                    .gameObject.AddComponent<GrayboxDefenseController3D>();
                defenseController.Configure(
                    session,
                    city,
                    worldView,
                    buildingPresentation,
                    defenseWorldView,
                    hud);
                SetPrivateInstanceField(
                    defenseController,
                    "runtime",
                    defense);
                SetPrivateInstanceField(
                    defenseController,
                    "snapshot",
                    defense.Snapshot);

                GrayboxEvacuationController3D evacuation = NewChild(
                        root.transform,
                        "Evacuation")
                    .gameObject.AddComponent<GrayboxEvacuationController3D>();
                evacuation.Configure(
                    session,
                    new FormalFortressDeploymentRequest(),
                    FormalNullBuildingPresentation.Instance,
                    menu);
                evacuation.ConfigureOperationalRuntimes(production, defense);
                if (!evacuation.TryHandleDeploymentRequest())
                    throw new InvalidOperationException(
                        "Formal mixed probe could not open evacuation.");
                menu.ShowEvacuationManifest(
                    evacuation.CaptureManifestView());

                GrayboxBuildingInputRouter3D input = NewChild(
                        root.transform,
                        "BuildingInput")
                    .gameObject.AddComponent<GrayboxBuildingInputRouter3D>();
                input.Configure(
                    menu,
                    interaction,
                    null,
                    null,
                    evacuation,
                    null);
                GrayboxFormalMixedProfilerHeartbeat3D heartbeat = NewChild(
                        root.transform,
                        "ProfilerHeartbeat")
                    .gameObject.AddComponent<
                        GrayboxFormalMixedProfilerHeartbeat3D>();
                heartbeat.Configure(
                    PulseFormalEvacuationTransactionalMarkersForProfiler);
                return new FormalEvacuationMixedFixture
                {
                    Root = root,
                    Material = material,
                    Session = session,
                    World = world,
                    ProductionController = productionController,
                    Production = production,
                    DefenseController = defenseController,
                    Defense = defense,
                    Hud = hud,
                    Evacuation = evacuation,
                    Menu = menu,
                    Input = input,
                    Heartbeat = heartbeat,
                    Canvas = canvas
                };
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null)
                    UnityEngine.Object.DestroyImmediate(material);
                throw;
            }
        }

        private static WorldMapModel CreateFormalProductionWorld()
        {
            var cells = new WorldCell[24, 24];
            var open = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
            for (int x = 0; x < 24; x++)
            for (int y = 0; y < 24; y++)
                cells[x, y] = open;
            cells[5, 9] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                1000);
            cells[5, 13] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                1000);
            return new WorldMapModel(cells);
        }

        private static int CaptureFormalMiningNodeAmount(WorldMapModel world)
        {
            return checked(
                world.Get(5, 9).ResourceAmount +
                world.Get(5, 13).ResourceAmount);
        }

        private static int CaptureFormalTowerAmmunition(
            GrayboxDefenseRuntime3D defense)
        {
            int amount = 0;
            for (int index = 0; index < defense.Towers.Count; index++)
                amount = checked(
                    amount + defense.Towers[index].Combat.Ammo);
            return amount;
        }

        private static void CaptureFormalProductionStateEvidence(
            FormalEvacuationMixedFixture fixture,
            out string[] stableIds,
            out float[] progress,
            out string[] stopReasons)
        {
            int count = fixture.Production.States.Count;
            stableIds = new string[count];
            progress = new float[count];
            stopReasons = new string[count];
            for (int index = 0; index < count; index++)
            {
                BuildingProductionState state =
                    fixture.Production.States[index];
                stableIds[index] = state.StableInstanceId;
                progress[index] = state.ProgressSeconds;
                stopReasons[index] = state.StopReason.ToString();
            }
        }

        private static void SetPrivateInstanceField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            field.SetValue(target, value);
        }

        private static FormalTransactionAllocationMetrics
            ExecuteFormalQuickDismantleTransaction(
            FormalEvacuationMixedFixture fixture)
        {
            ProfilerRecorder recorder = StartGcAllocationRecorder();
            long currentThreadBytes = 0L;
            long profiledBytes = 0L;
            int committedItemCount = 0;
            const int manifestItemCount = 16;
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < ResourceIds.All.Length; index++)
                    fixture.Session.Inventory.Set(ResourceIds.All[index], 0);

                string warehouseId = null;
                for (int index = 0;
                     index < fixture.Session.Instances.Count;
                     index++)
                {
                    GrayboxBuildingInstance3D instance =
                        fixture.Session.Instances[index];
                    if (instance.Placement.Definition.Id.Value ==
                        BuildingCatalog.Warehouse.Id.Value)
                    {
                        warehouseId = instance.StableInstanceId;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(warehouseId) ||
                    fixture.Session.CityStorage.AddToWarehouse(
                        warehouseId,
                        ResourceIds.Biomass,
                        3) != 3)
                    throw new InvalidOperationException(
                        "Formal transaction could not seed warehouse migration payload.");

                if (fixture.Evacuation.AssignAll(
                        BuildingEvacuationTreatment.QuickDismantle) !=
                    manifestItemCount)
                    throw new InvalidOperationException(
                        "Formal transaction could not assign 16 quick dismantles.");
                EvacuationManifestViewModel planned =
                    fixture.Evacuation.CaptureManifestView();
                int payloadItems = 0;
                for (int index = 0; index < planned.Items.Count; index++)
                {
                    EvacuationManifestItemViewModel item = planned.Items[index];
                    if (item.ExpectedRefunds.Count > 0 ||
                        item.Input.Count > 0 ||
                        item.ReservedInput.Count > 0 ||
                        item.Output.Count > 0 ||
                        item.AmmunitionAmount > 0 ||
                        item.WarehouseContents.Count > 0)
                        payloadItems++;
                }
                if (planned.Items.Count != manifestItemCount ||
                    payloadItems <= 0 ||
                    !planned.CanConfirm ||
                    !fixture.Evacuation.ConfirmManifest())
                    throw new InvalidOperationException(
                        "Formal quick-dismantle preflight was empty or could not commit.");
                for (int index = 0;
                     index < fixture.Session.Instances.Count;
                     index++)
                {
                    if (fixture.Session.Instances[index].Placement.Site ==
                        BuildingSite.Ground)
                        committedItemCount++;
                }
                committedItemCount = manifestItemCount - committedItemCount;
                if (fixture.Evacuation.IsProcessing ||
                    fixture.Evacuation.IsBlocked ||
                    committedItemCount != manifestItemCount ||
                    fixture.Session.HasPlayerOwnedGroundInstances)
                    throw new InvalidOperationException(
                        "Formal quick-dismantle transaction did not atomically commit all 16 outer-city items.");
                currentThreadBytes =
                    GC.GetAllocatedBytesForCurrentThread() - before;
            }
            finally
            {
                recorder.Stop();
                profiledBytes = SumGcAllocationBytes(recorder);
                recorder.Dispose();
            }
            return new FormalTransactionAllocationMetrics(
                currentThreadBytes,
                profiledBytes,
                manifestItemCount,
                committedItemCount);
        }

        private static void BuildFormalMixedPopulation(
            GrayboxBuildingSession3D session)
        {
            BeginFormalMixedBuilding(
                session,
                BuildingCatalog.MiningStation,
                BuildingSite.Ground,
                5,
                9,
                new ResourceNodeBinding("world.resource-node.5.9", 5, 9));
            BeginFormalMixedBuilding(
                session,
                BuildingCatalog.MiningStation,
                BuildingSite.Ground,
                5,
                13,
                new ResourceNodeBinding("world.resource-node.5.13", 5, 13));
            session.CompleteAllConstructionForDevelopment(
                FormalNullBuildingPresentation.Instance);
            BeginFormalMixedBuilding(
                session, BuildingCatalog.Smelter,
                BuildingSite.Ground, 8, 8);
            BeginFormalMixedBuilding(
                session, BuildingCatalog.Smelter,
                BuildingSite.Ground, 8, 14);
            session.CompleteAllConstructionForDevelopment(
                FormalNullBuildingPresentation.Instance);
            BeginFormalMixedBuilding(
                session, BuildingCatalog.Assembler,
                BuildingSite.InnerCity, 0, 0);
            session.CompleteAllConstructionForDevelopment(
                FormalNullBuildingPresentation.Instance);
            BeginFormalMixedBuilding(
                session, BuildingCatalog.MachineGunTurret,
                BuildingSite.Ground, 10, 12);
            session.CompleteAllConstructionForDevelopment(
                FormalNullBuildingPresentation.Instance);
            BeginFormalMixedBuilding(
                session, BuildingCatalog.Warehouse,
                BuildingSite.Ground, 14, 10);
            BeginFormalMixedBuilding(
                session, BuildingCatalog.ResearchStation,
                BuildingSite.Ground, 14, 14);
            session.CompleteAllConstructionForDevelopment(
                FormalNullBuildingPresentation.Instance);

            int[,] walls =
            {
                { 4, 6 }, { 6, 6 }, { 8, 6 }, { 10, 6 },
                { 12, 6 }, { 14, 6 }, { 16, 6 }, { 18, 6 },
                { 18, 8 }
            };
            for (int index = 0; index < walls.GetLength(0) - 1; index++)
            {
                BeginFormalMixedBuilding(
                    session,
                    BuildingCatalog.Wall,
                    BuildingSite.Ground,
                    walls[index, 0],
                    walls[index, 1]);
            }
            session.CompleteAllConstructionForDevelopment(
                FormalNullBuildingPresentation.Instance);
            int finalWall = walls.GetLength(0) - 1;
            BeginFormalMixedBuilding(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                walls[finalWall, 0],
                walls[finalWall, 1]);
        }

        private static void BeginFormalMixedBuilding(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            ResourceNodeBinding resourceNode = default)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                definition,
                site == BuildingSite.Ground
                    ? session.GroundGrid
                    : session.InnerGrid,
                site,
                BuildingOrientation.North,
                x,
                y,
                12,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
                true,
                false,
                true,
                true,
                resourceNode,
                true,
                unlock,
                true);
            if (!session.TryBeginConstruction(
                    request,
                    FormalNullBuildingPresentation.Instance,
                    out _,
                    out BuildingPlacementEvaluation evaluation))
            {
                throw new InvalidOperationException(
                    "Formal mixed placement failed for " +
                    definition.Name + ": " + evaluation.PrimaryFailure);
            }
        }

        private static void ValidateFormalEvacuationMixedFixture(
            FormalEvacuationMixedFixture fixture,
            EvacuationManifestViewModel manifest,
            GrayboxDefenseRuntimeSnapshot3D defense,
            int outerCityItemCount)
        {
            if (fixture.Production.States.Count != 5 ||
                fixture.Production.RunnableStates.Count != 5)
                throw new InvalidOperationException(
                    "Formal mixed probe requires the canonical five-building production chain.");
            if (defense.AliveEnemyCount != 8 || defense.Towers.Count != 1)
                throw new InvalidOperationException(
                    "Formal mixed probe requires one tower and eight live enemies.");
            if (fixture.Hud.SummaryRect == null ||
                !fixture.Hud.SummaryRect.gameObject.activeInHierarchy)
                throw new InvalidOperationException(
                    "Formal mixed probe requires the defense HUD to be visible.");
            int mines = 0;
            int smelters = 0;
            int assemblers = 0;
            int warehouses = 0;
            int researchStations = 0;
            int turrets = 0;
            int completed = 0;
            int underConstruction = 0;
            for (int index = 0;
                 index < fixture.Session.Instances.Count;
                 index++)
            {
                GrayboxBuildingInstance3D instance =
                    fixture.Session.Instances[index];
                string definitionId =
                    instance.Placement.Definition.Id.Value;
                if (definitionId == BuildingCatalog.MiningStation.Id.Value)
                    mines++;
                else if (definitionId == BuildingCatalog.Smelter.Id.Value)
                    smelters++;
                else if (definitionId == BuildingCatalog.Assembler.Id.Value)
                    assemblers++;
                else if (definitionId == BuildingCatalog.Warehouse.Id.Value)
                    warehouses++;
                else if (definitionId ==
                    BuildingCatalog.ResearchStation.Id.Value)
                    researchStations++;
                else if (definitionId ==
                    BuildingCatalog.MachineGunTurret.Id.Value)
                    turrets++;
                if (instance.State ==
                    GrayboxBuildingInstanceState.Completed)
                    completed++;
                else if (instance.State ==
                    GrayboxBuildingInstanceState.UnderConstruction)
                    underConstruction++;
            }
            if (mines != 2 || smelters != 2 || assemblers != 1 ||
                warehouses != 1 || researchStations != 1 || turrets != 1)
                throw new InvalidOperationException(
                    "Formal mixed probe is missing a required production, warehouse, research, or defense category.");
            if (completed != 16 || underConstruction != 1)
                throw new InvalidOperationException(
                    "Formal mixed probe requires 16 completed buildings and one under-construction wall.");
            if (!fixture.Evacuation.IsManifestOpen ||
                !fixture.Menu.EvacuationVisible ||
                !manifest.IsInCombat ||
                outerCityItemCount != 16 ||
                manifest.Items.Count != 16)
                throw new InvalidOperationException(
                    "Formal mixed probe requires an in-combat manifest with exactly 16 outer-city items.");
        }

        private static void ValidateFormalEvacuationMixedResult(
            FormalEvacuationMixedPerformanceResult result)
        {
            if (result.sampleCount != FormalEvacuationMixedSampleCount ||
                result.mixedFrameMilliseconds == null ||
                result.mixedFrameMilliseconds.Length !=
                    FormalEvacuationMixedSampleCount)
                throw new InvalidOperationException(
                    "Formal mixed probe must record exactly 300 frames.");
            if (result.stableAdapterManagedAllocationBytes != 0L ||
                result.stableAdapterProfiledAllocationBytes != 0L)
                throw new InvalidOperationException(
                    "Stable formal input/evacuation/UI adapters allocated managed memory.");
            if (result.activeDefenseMeasuredAllocationBytes >
                FormalDefenseAllocationBudgetBytes)
                throw new InvalidOperationException(
                    "Active defense snapshots exceeded the 64 KB/300 sample budget: " +
                    result.activeDefenseMeasuredAllocationBytes);
            if (result.stableAdapterUiObjectCount != result.finalUiObjectCount)
                throw new InvalidOperationException(
                    "Formal mixed UI object count changed during stable observation.");
            if (result.evacuationManifestItemCount != 16 ||
                result.outerCityEvacuationItemCount != 16)
                throw new InvalidOperationException(
                    "Formal mixed result must preserve exactly 16 outer-city evacuation items.");
            if (result.productionStateIds == null ||
                result.productionProgressBefore == null ||
                result.productionProgressAfter == null ||
                result.productionStopReasonsBefore == null ||
                result.productionStopReasonsAfter == null ||
                result.productionStateAdvanceFrameCounts == null ||
                result.productionStateIds.Length != 5 ||
                result.productionProgressBefore.Length != 5 ||
                result.productionProgressAfter.Length != 5 ||
                result.productionStopReasonsBefore.Length != 5 ||
                result.productionStopReasonsAfter.Length != 5 ||
                result.productionStateAdvanceFrameCounts.Length != 5)
                throw new InvalidOperationException(
                    "Formal mixed result must record before/after evidence for all five production states.");
            for (int index = 0;
                 index < result.productionStopReasonsBefore.Length;
                 index++)
            {
                ValidateFormalProductionStopReason(
                    result.productionStopReasonsBefore[index]);
                ValidateFormalProductionStopReason(
                    result.productionStopReasonsAfter[index]);
                if (result.productionStateAdvanceFrameCounts[index] <= 0)
                    throw new InvalidOperationException(
                        "Formal production state never advanced while active: " +
                        result.productionStateIds[index]);
            }
            if (result.miningNodeAmountAfter >=
                    result.miningNodeAmountBefore ||
                result.ammunitionAmountAfter <=
                    result.ammunitionAmountBefore + 4 ||
                result.productionAdvancedStageCount != 3)
                throw new InvalidOperationException(
                    "Formal mixed workload did not advance mining, smelting, and assembly through the full chain.");
            long expectedTransactionBudget = checked(
                FormalTransactionAllocationBaselineBytes +
                FormalTransactionAllocationPerManifestItemBytes *
                result.evacuationManifestItemCount);
            if (result.transactionAllocationBaselineBytes !=
                    FormalTransactionAllocationBaselineBytes ||
                result.transactionAllocationPerManifestItemBytes !=
                    FormalTransactionAllocationPerManifestItemBytes ||
                result.transactionAllocationBudgetBytes !=
                    expectedTransactionBudget ||
                result.transactionCommittedItemCount != 16 ||
                result.transactionMeasuredAllocationBytes >
                    expectedTransactionBudget)
                throw new InvalidOperationException(
                    "Formal evacuation transaction exceeded its linear allocation budget: " +
                    result.transactionMeasuredAllocationBytes + "/" +
                    expectedTransactionBudget + " B.");
            if (result.markers == null ||
                result.markers.Length != FormalEvacuationMarkerNames.Length)
                throw new InvalidOperationException(
                    "Formal mixed marker evidence is incomplete.");
            for (int index = 0; index < result.markers.Length; index++)
            {
                if (!string.Equals(
                        result.markers[index].name,
                        FormalEvacuationMarkerNames[index],
                        StringComparison.Ordinal) ||
                    result.markers[index].occurrenceCount <= 0L)
                {
                    throw new InvalidOperationException(
                        "Formal mixed marker was not observed: " +
                        FormalEvacuationMarkerNames[index]);
                }
                if (string.Equals(
                        result.markers[index].name,
                        "WasteCity.Formal.Evacuation.Commit",
                        StringComparison.Ordinal) &&
                    result.markers[index].occurrenceCount != 16L)
                    throw new InvalidOperationException(
                        "Formal mixed probe must observe exactly 16 evacuation commits.");
            }
        }

        private static void ValidateFormalProductionStopReason(string reason)
        {
            // MissingInput is a normal instantaneous boundary between recipe
            // cycles. Every structural stop must fail this active-chain gate.
            if (reason == ProductionStopReason.None.ToString() ||
                reason == ProductionStopReason.MissingInput.ToString())
                return;

            throw new InvalidOperationException(
                "Formal mixed production entered a structural stop state: " +
                reason);
        }

        private static void MeasureManagedAllocations(
            int sampleCount,
            Action operation,
            out long currentThreadBytes,
            out long profiledBytes)
        {
            ProfilerRecorder recorder = StartGcAllocationRecorder();
            currentThreadBytes = 0L;
            profiledBytes = 0L;
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int sample = 0; sample < sampleCount; sample++)
                    operation();
                currentThreadBytes =
                    GC.GetAllocatedBytesForCurrentThread() - before;
            }
            finally
            {
                recorder.Stop();
                profiledBytes = SumGcAllocationBytes(recorder);
                recorder.Dispose();
            }
        }

        private static ProfilerRecorder StartGcAllocationRecorder()
        {
            return ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                16384,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
        }

        private static long SumGcAllocationBytes(ProfilerRecorder recorder)
        {
            long bytes = 0L;
            for (int sample = 0; sample < recorder.Count; sample++)
            {
                ProfilerRecorderSample allocation = recorder.GetSample(sample);
                bytes += allocation.Value * allocation.Count;
            }
            return bytes;
        }

        private static ProfilerRecorder[] StartFormalMarkerRecorders()
        {
            var recorders = new ProfilerRecorder[
                FormalEvacuationMarkerNames.Length];
            try
            {
                for (int index = 0; index < recorders.Length; index++)
                {
                    recorders[index] = ProfilerRecorder.StartNew(
                        ProfilerCategory.Scripts,
                        FormalEvacuationMarkerNames[index],
                        4096,
                        ProfilerRecorderOptions.StartImmediately |
                        ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                        ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
                }
                return recorders;
            }
            catch
            {
                DisposeRecorders(recorders);
                throw;
            }
        }

        private static FormalProfilerMarkerResult[]
            StopAndReadFormalMarkerRecorders(ProfilerRecorder[] recorders)
        {
            var results = new FormalProfilerMarkerResult[recorders.Length];
            for (int index = 0; index < recorders.Length; index++)
            {
                ProfilerRecorder recorder = recorders[index];
                recorder.Stop();
                long occurrences = 0L;
                long totalNanoseconds = 0L;
                long maximumNanoseconds = 0L;
                for (int sample = 0; sample < recorder.Count; sample++)
                {
                    ProfilerRecorderSample value = recorder.GetSample(sample);
                    occurrences += value.Count;
                    totalNanoseconds += value.Value;
                    maximumNanoseconds = Math.Max(
                        maximumNanoseconds,
                        value.Value);
                }
                results[index] = new FormalProfilerMarkerResult
                {
                    name = FormalEvacuationMarkerNames[index],
                    occurrenceCount = occurrences,
                    totalNanoseconds = totalNanoseconds,
                    maximumNanoseconds = maximumNanoseconds
                };
                recorder.Dispose();
                recorders[index] = default;
            }
            return results;
        }

        private static void DisposeRecorders(ProfilerRecorder[] recorders)
        {
            for (int index = 0; index < recorders.Length; index++)
                recorders[index].Dispose();
        }

        private static string ResolveFormalEvacuationMixedResultPath()
        {
            string configured = Environment.GetEnvironmentVariable(
                FormalEvacuationMixedResultEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return ResolveExternalPath(
                    FormalEvacuationMixedResultEnvironmentVariable,
                    true);
            string resultPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "wastecity-formal-evacuation-mixed-performance.json"));
            string directory = Path.GetDirectoryName(resultPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "Formal mixed performance result directory is unavailable.");
            Directory.CreateDirectory(directory);
            return resultPath;
        }

        public static void SummarizeGuiProfilerCapture()
        {
            SummarizeGuiProfilerCaptureCore(false);
        }

        public static void SummarizeFormalEvacuationMixedGuiProfilerCapture()
        {
            SummarizeGuiProfilerCaptureCore(true);
        }

        private static void SummarizeGuiProfilerCaptureCore(
            bool formalRequired)
        {
            string inputPath = ResolveExternalPath(
                GuiProfilerInputEnvironmentVariable,
                false);
            string resultPath = ResolveExternalPath(
                GuiProfilerResultEnvironmentVariable,
                true);
            if (!File.Exists(inputPath))
                throw new FileNotFoundException(
                    "GUI Profiler capture is unavailable.",
                    inputPath);

            ProfilerDriver.LoadProfile(inputPath, false);
            int firstFrame = ProfilerDriver.firstFrameIndex;
            int lastFrame = ProfilerDriver.lastFrameIndex;
            if (firstFrame < 0 || lastFrame < firstFrame)
                throw new InvalidOperationException(
                    "GUI Profiler capture contains no frames.");

            var markers = new[]
            {
                NewMarkerResult(
                    "building-input",
                    "WasteCity.Graybox3D.dll!WasteCity.Graybox3D::" +
                    "GrayboxInputRouter.Update() [Invoke]",
                    "GrayboxInputRouter.Update() [Invoke]"),
                NewMarkerResult(
                    "building-session",
                    "GrayboxBuildingSession3D.TickConstruction"),
                NewMarkerResult(
                    "building-construction",
                    "WasteCity.Graybox3D.Building.dll!" +
                    "WasteCity.Graybox3D.Building::" +
                    "GrayboxConstructionController3D.Update() [Invoke]",
                    "GrayboxConstructionController3D.Update() [Invoke]"),
                NewMarkerResult(
                    "building-evacuation",
                    "WasteCity.Graybox3D.Building.dll!" +
                    "WasteCity.Graybox3D.Building::" +
                    "GrayboxEvacuationController3D.Update() [Invoke]",
                    "GrayboxEvacuationController3D.Update() [Invoke]"),
                NewMarkerResult(
                    "first-art-terrain-presenter",
                    "FirstArtTerrainRenderer3D.Update() [Invoke]",
                    "FirstArtTerrainRenderer3D.LateUpdate() [Invoke]"),
                NewMarkerResult(
                    "formal-mixed-workload-frame",
                    FormalMixedWorkloadFrameMarkerName),
                NewMarkerResult(
                    "formal-production-tick",
                    "WasteCity.Formal.Production.Tick"),
                NewMarkerResult(
                    "formal-defense-tick",
                    "WasteCity.Formal.Defense.Tick"),
                NewMarkerResult(
                    "formal-defense-hud-apply",
                    "WasteCity.Formal.DefenseHud.Apply"),
                NewMarkerResult(
                    "formal-evacuation-tick",
                    "WasteCity.Formal.Evacuation.Tick"),
                NewMarkerResult(
                    "formal-evacuation-manifest-build",
                    "WasteCity.Formal.Evacuation.ManifestView.Build"),
                NewMarkerResult(
                    "formal-evacuation-capacity-preflight",
                    "WasteCity.Formal.Evacuation.CapacityPreflight"),
                NewMarkerResult(
                    "formal-evacuation-commit",
                    "WasteCity.Formal.Evacuation.Commit")
            };

            double totalFrameMilliseconds = 0d;
            double minimumFrameMilliseconds = double.MaxValue;
            double maximumFrameMilliseconds = 0d;
            int validFrameCount = 0;
            for (int frame = firstFrame; frame <= lastFrame; frame++)
            {
                using (RawFrameDataView raw =
                       ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!raw.valid)
                        continue;
                    double frameMilliseconds = raw.frameTimeMs;
                    totalFrameMilliseconds += frameMilliseconds;
                    minimumFrameMilliseconds = Math.Min(
                        minimumFrameMilliseconds,
                        frameMilliseconds);
                    maximumFrameMilliseconds = Math.Max(
                        maximumFrameMilliseconds,
                        frameMilliseconds);
                    validFrameCount++;
                    AccumulateAdapterAllocations(raw, markers);
                }
            }
            if (validFrameCount == 0)
                throw new InvalidOperationException(
                    "GUI Profiler capture has no valid main-thread frames.");

            double averageFrameMilliseconds =
                totalFrameMilliseconds / validFrameCount;
            var result = new GuiProfilerResult
            {
                inputPath = inputPath,
                firstFrameIndex = firstFrame,
                lastFrameIndex = lastFrame,
                frameCount = validFrameCount,
                averageFrameMilliseconds = averageFrameMilliseconds,
                averageFramesPerSecond =
                    averageFrameMilliseconds > 0d
                        ? 1000d / averageFrameMilliseconds
                        : 0d,
                minimumFrameMilliseconds = minimumFrameMilliseconds,
                maximumFrameMilliseconds = maximumFrameMilliseconds,
                adapterMarkers = markers
            };
            ValidateFormalGuiProfilerWindow(result, formalRequired);
            File.WriteAllText(
                resultPath,
                JsonUtility.ToJson(result, true));
            Debug.Log(
                (formalRequired
                    ? "Formal evacuation mixed GUI Profiler summary result: "
                    : "GUI Profiler summary result: ") +
                resultPath);
        }

        private static void ValidateFormalGuiProfilerWindow(
            GuiProfilerResult result,
            bool formalRequired)
        {
            int formalMarkerCount = 0;
            GuiProfilerMarkerResult activation = null;
            for (int index = 0; index < result.adapterMarkers.Length; index++)
            {
                GuiProfilerMarkerResult marker = result.adapterMarkers[index];
                if (string.Equals(
                        marker.label,
                        "formal-mixed-workload-frame",
                        StringComparison.Ordinal))
                {
                    activation = marker;
                    continue;
                }
                if (!marker.label.StartsWith(
                        "formal-",
                        StringComparison.Ordinal))
                    continue;
                formalMarkerCount++;
            }
            if (activation == null || activation.sampleOccurrences <= 0)
            {
                if (formalRequired)
                    throw new InvalidOperationException(
                        "Formal mixed GUI capture is missing the PlayMode heartbeat activation marker.");
                return;
            }
            if (result.frameCount != FormalEvacuationMixedSampleCount)
                throw new InvalidOperationException(
                    "Formal mixed GUI capture must contain exactly 300 frames.");
            if (result.averageFrameMilliseconds > 16.67d)
                throw new InvalidOperationException(
                    "Formal mixed GUI capture exceeded 16.67 ms average frame time: " +
                    result.averageFrameMilliseconds);
            if (formalMarkerCount != FormalEvacuationMarkerNames.Length)
                throw new InvalidOperationException(
                    "Formal mixed GUI marker contract is incomplete.");
            for (int index = 0; index < result.adapterMarkers.Length; index++)
            {
                GuiProfilerMarkerResult marker = result.adapterMarkers[index];
                if (marker.label.StartsWith(
                        "formal-",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        marker.label,
                        "formal-mixed-workload-frame",
                        StringComparison.Ordinal) &&
                    marker.sampleOccurrences <= 0)
                    throw new InvalidOperationException(
                        "Formal mixed GUI marker was not observed: " +
                        marker.label);
            }
            if (activation.frameOccurrences != result.frameCount)
                throw new InvalidOperationException(
                    "Formal mixed workload activation marker must occur in every captured frame.");
        }

        private static GuiProfilerMarkerResult NewMarkerResult(
            string label,
            params string[] acceptedSampleNames)
        {
            return new GuiProfilerMarkerResult
            {
                label = label,
                acceptedSampleNames = acceptedSampleNames
            };
        }

        private static void AccumulateAdapterAllocations(
            RawFrameDataView raw,
            GuiProfilerMarkerResult[] markers)
        {
            string[] names = new string[raw.sampleCount];
            for (var index = 0; index < raw.sampleCount; index++)
                names[index] = raw.GetSampleName(index);

            for (var markerIndex = 0;
                 markerIndex < markers.Length;
                 markerIndex++)
            {
                GuiProfilerMarkerResult marker = markers[markerIndex];
                bool occurredThisFrame = false;
                for (var sampleIndex = 0;
                     sampleIndex < names.Length;
                     sampleIndex++)
                {
                    if (!MatchesAny(
                            names[sampleIndex],
                            marker.acceptedSampleNames))
                        continue;
                    occurredThisFrame = true;
                    marker.sampleOccurrences++;
                    int end = Math.Min(
                        names.Length,
                        sampleIndex +
                        raw.GetSampleChildrenCountRecursive(sampleIndex) + 1);
                    for (var descendant = sampleIndex + 1;
                         descendant < end;
                         descendant++)
                    {
                        if (!string.Equals(
                                names[descendant],
                                "GC.Alloc",
                                StringComparison.Ordinal))
                            continue;
                        if (raw.GetSampleMetadataCount(descendant) > 0)
                            marker.descendantGcAllocationBytes +=
                                raw.GetSampleMetadataAsLong(descendant, 0);
                    }
                }
                if (occurredThisFrame)
                    marker.frameOccurrences++;
            }
        }

        private static bool MatchesAny(
            string sampleName,
            string[] acceptedNames)
        {
            for (var index = 0; index < acceptedNames.Length; index++)
                if (string.Equals(
                        sampleName,
                        acceptedNames[index],
                        StringComparison.Ordinal) ||
                    sampleName.EndsWith(
                        acceptedNames[index],
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static BuildingRunMetrics MeasureBuildingRun(
            Shader shader)
        {
            var root = new GameObject("GrayboxBuildingPerformanceProbe");
            var material = new Material(shader);
            try
            {
                long before = Stopwatch.GetTimestamp();

                Transform terrain = NewChild(root.transform, "TerrainRoot");
                Transform resources =
                    NewChild(root.transform, "ResourceRoot");
                Transform obstacles =
                    NewChild(root.transform, "ObstacleRoot");
                GrayboxWorldView3D world =
                    root.AddComponent<GrayboxWorldView3D>();
                world.Configure(
                    terrain,
                    resources,
                    obstacles,
                    material);
                world.Generate(
                    new WorldMapModel(
                        GrayboxSceneBootstrap.WorldWidth,
                        GrayboxSceneBootstrap.WorldHeight,
                        new WorldSeed(
                            GrayboxSceneBootstrap.WorldSeedValue)));

                Transform cityTransform =
                    NewChild(root.transform, "MobileCity");
                world.Coordinates.TryCellToWorld(
                    16,
                    12,
                    .5f,
                    out Vector3 cityPosition);
                cityTransform.position = cityPosition;
                Rigidbody body =
                    cityTransform.gameObject.AddComponent<Rigidbody>();
                BoxCollider bodyCollider =
                    cityTransform.gameObject.AddComponent<BoxCollider>();
                GrayboxMobileCityController3D city =
                    cityTransform.gameObject.AddComponent<
                        GrayboxMobileCityController3D>();
                city.Configure(world, body, bodyCollider);
                if (!city.RestoreDeploymentForDevelopment(
                        CityMode.Fortress))
                    throw new InvalidOperationException(
                        "Building probe could not enter Fortress mode.");

                Transform sessionTransform =
                    NewChild(root.transform, "BuildingSession");
                GrayboxBuildingSession3D session =
                    sessionTransform.gameObject.AddComponent<
                        GrayboxBuildingSession3D>();
                session.ConfigureDevelopmentFixture();
                session.Inventory.Set(ResourceIds.Stone, 5000);

                Transform presentationTransform =
                    NewChild(root.transform, "BuildingPresentation");
                Transform instanceRoot =
                    NewChild(presentationTransform, "InstanceRoot");
                Transform infrastructureRoot =
                    NewChild(presentationTransform, "InfrastructureRoot");
                GrayboxBuildingWorldView3D presentation =
                    presentationTransform.gameObject.AddComponent<
                        GrayboxBuildingWorldView3D>();
                presentation.Configure(
                    instanceRoot,
                    infrastructureRoot,
                    material,
                    city);

                List<BuildingCell> cells = CreateBuildingCells();
                for (var index = 0;
                     index < CompletedBuildingCount;
                     index++)
                    BeginWall(
                        session,
                        presentation,
                        cells[index].X,
                        cells[index].Y);
                session.TickConstruction(
                    10f,
                    CityMode.Fortress,
                    false,
                    presentation);

                for (var index = CompletedBuildingCount;
                     index < CompletedBuildingCount +
                     ConstructionBuildingCount;
                     index++)
                    BeginWall(
                        session,
                        presentation,
                        cells[index].X,
                        cells[index].Y);

                for (var index = CompletedBuildingCount +
                     ConstructionBuildingCount;
                     index < BuildingInstanceCount;
                     index++)
                {
                    GrayboxBuildingInstance3D ruin = BeginWall(
                        session,
                        presentation,
                        cells[index].X,
                        cells[index].Y);
                    BuildingEvacuationWork work =
                        BuildingEvacuationRules.Create(
                            ruin.StableInstanceId,
                            ruin.Placement.Definition.Cost,
                            ruin.Progress.BaseDuration,
                            1d,
                            BuildingEvacuationTreatment.Abandon);
                    if (!session.TryCaptureEvacuationWork(
                            new[] { work },
                            out string captureFailure))
                        throw new InvalidOperationException(
                            "Building probe capture failed: " +
                            captureFailure);
                    if (!session.TryCommitEvacuation(
                            work,
                            presentation,
                            out _,
                            out string commitFailure))
                        throw new InvalidOperationException(
                            "Building probe abandon failed: " +
                            commitFailure);
                }

                int completed = 0;
                int construction = 0;
                int ruins = 0;
                for (var index = 0;
                     index < session.Instances.Count;
                     index++)
                {
                    switch (session.Instances[index].State)
                    {
                        case GrayboxBuildingInstanceState.Completed:
                            completed++;
                            break;
                        case GrayboxBuildingInstanceState.UnderConstruction:
                            construction++;
                            break;
                        case GrayboxBuildingInstanceState.AbandonedRuin:
                            ruins++;
                            break;
                    }
                }

                long after = Stopwatch.GetTimestamp();
                double milliseconds =
                    (after - before) * 1000d /
                    Stopwatch.Frequency;
                int persistentObjectCount =
                    presentationTransform.GetComponentsInChildren<
                        Transform>(true).Length;
                return new BuildingRunMetrics(
                    milliseconds,
                    session.Instances.Count,
                    completed,
                    construction,
                    ruins,
                    presentation.InstanceRendererCount,
                    presentation.InfrastructureRendererCount,
                    persistentObjectCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static List<BuildingCell> CreateBuildingCells()
        {
            var cells = new List<BuildingCell>(BuildingInstanceCount);
            for (var y = 4;
                 y <= 20 && cells.Count < BuildingInstanceCount;
                 y++)
            {
                for (var x = 8;
                     x <= 24 && cells.Count < BuildingInstanceCount;
                     x++)
                {
                    if (Math.Abs(x - 16) <= 1 &&
                        Math.Abs(y - 12) <= 1)
                        continue;
                    cells.Add(new BuildingCell(x, y));
                }
            }
            if (cells.Count != BuildingInstanceCount)
                throw new InvalidOperationException(
                    "Building probe could not reserve 128 cells.");
            return cells;
        }

        private static GrayboxBuildingInstance3D BeginWall(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation,
            int x,
            int y)
        {
            BuildingDefinition definition = BuildingCatalog.Wall;
            BuildingUnlockEvaluation unlock =
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                16,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
                true,
                false,
                true,
                true,
                true,
                null,
                true,
                unlock,
                session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost));
            if (!session.TryBeginConstruction(
                    request,
                    presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation))
                throw new InvalidOperationException(
                    "Building probe placement failed: " +
                    evaluation.PrimaryFailure);
            return instance;
        }

        private static long MeasureStableTerrainAllocation(
            GrayboxWorldView3D world,
            FirstArtTerrainRenderer3D presenter,
            WorldMapModel model)
        {
            try
            {
                world.Generate(model);
                if (!presenter.TryPresent(world, false))
                {
                    throw new InvalidOperationException(
                        "First-art terrain allocation setup failed: " +
                        presenter.LastPresentationError);
                }

                int observable = ObserveFirstTerrain(presenter);
                observable += ObserveFirstTerrain(presenter);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < 300; frame++)
                    observable += ObserveFirstTerrain(presenter);
                long allocation =
                    GC.GetAllocatedBytesForCurrentThread() - before;
                if (observable != 212306)
                {
                    throw new InvalidOperationException(
                        "First-art terrain allocation observation was " +
                        "not executed completely.");
                }
                return allocation;
            }
            finally
            {
                presenter.ClearPresentation();
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int ObserveFirstTerrain(
            FirstArtTerrainRenderer3D presenter)
        {
            int value = presenter.IsPresented ? 1 : 0;
            value += presenter.SurfaceRenderer != null ? 2 : 0;
            value += presenter.SurfaceRenderer.enabled ? 4 : 0;
            value += presenter.ControlMaps != null ? 8 : 0;
            value += presenter.ControlMaps.Width;
            value += presenter.ControlMaps.Height;
            value += presenter.Profile != null ? 16 : 0;
            value += string.IsNullOrEmpty(presenter.LastPresentationError)
                ? 32
                : 0;
            return value;
        }

        private static void ValidateFirstTerrainResult(
            FirstTerrainResult result)
        {
            if (result.generationMilliseconds == null ||
                result.generationMilliseconds.Length != RunCount)
            {
                throw new InvalidOperationException(
                    "First-art terrain probe must contain exactly five runs.");
            }
            if (result.medianMilliseconds >
                FirstTerrainMaximumMedianMilliseconds)
            {
                throw new InvalidOperationException(
                    "First-art terrain generation median exceeded 250 ms: " +
                    result.medianMilliseconds);
            }
            if (result.formalRendererCount != 1)
            {
                throw new InvalidOperationException(
                    "First-art terrain must use exactly one formal Renderer.");
            }
            if (result.formalPersistentObjectCount > 1)
            {
                throw new InvalidOperationException(
                    "First-art terrain created too many persistent objects.");
            }
            if (result.controlWidth != FirstTerrainWidth * 4 ||
                result.controlHeight != FirstTerrainHeight * 4)
            {
                throw new InvalidOperationException(
                    "First-art terrain control dimensions are incorrect.");
            }
            if (result.managedAllocationBytesAfterWarmup != 0)
            {
                throw new InvalidOperationException(
                    "Stable first-art terrain observation allocated " +
                    result.managedAllocationBytesAfterWarmup +
                    " managed bytes.");
            }
        }

        private static void ValidateFirstTerrainRuntimeEvidence(
            FirstTerrainRuntimeEvidenceResult result)
        {
            if (result.arrays == null || result.arrays.Length != 4)
                throw new InvalidOperationException("Runtime evidence must contain four arrays.");
            if (result.formalRendererCount != 1)
                throw new InvalidOperationException("Runtime evidence requires one formal Renderer.");
            if (result.compressedPayloadBytes != 127227779L ||
                result.compressedPayloadBytes > 128L * 1024L * 1024L)
            {
                throw new InvalidOperationException(
                    "First terrain compressed payload exceeded its approved contract: " +
                    result.compressedPayloadBytes);
            }
            if (result.editorNativeMemoryBytes > 256L * 1024L * 1024L ||
                result.editorObservedDuplicateDifferenceBytes > 64L * 1024L)
            {
                throw new InvalidOperationException(
                    "Editor-observed terrain native memory is outside its explained 2x envelope: " +
                    result.editorNativeMemoryBytes + ", difference " +
                    result.editorObservedDuplicateDifferenceBytes);
            }
            for (int index = 0; index < result.arrays.Length; index++)
            {
                if (result.arrays[index].isReadable)
                {
                    throw new InvalidOperationException(
                        "Persistent terrain runtime arrays must be non-readable: " +
                        result.arrays[index].label);
                }
            }
            if (result.presenterDeclaresUpdate || result.presenterDeclaresLateUpdate)
            {
                throw new InvalidOperationException(
                    "First-art terrain water must not declare a CPU Update loop.");
            }
            if (!result.editorNativeIsEditorObserved ||
                result.editorNativeIsGpuMemory ||
                result.windowsDevelopmentPlayerMemoryResolved)
            {
                throw new InvalidOperationException(
                    "Runtime memory evidence labels are not conservative.");
            }
        }

        private static string ResolveBuildingResultPath()
        {
            return ResolveExternalPath(
                BuildingResultEnvironmentVariable,
                true);
        }

        private static string ResolveExternalPath(
            string environmentVariable,
            bool createParentDirectory)
        {
            string configured =
                Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(configured) ||
                !Path.IsPathRooted(configured))
                throw new InvalidOperationException(
                    environmentVariable +
                    " must specify an absolute repository-external path.");

            string fullPath = Path.GetFullPath(configured);
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string projectPrefix = projectRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (string.Equals(
                    fullPath,
                    projectRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(
                    projectPrefix,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    environmentVariable +
                    " must remain outside the repository.");

            if (!createParentDirectory)
                return fullPath;
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "External result directory is unavailable.");
            Directory.CreateDirectory(directory);
            return fullPath;
        }

        private static string ResolveResultPath()
        {
            string configured =
                Environment.GetEnvironmentVariable(
                    ResultEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException(
                    ResultEnvironmentVariable +
                    " must specify an absolute /tmp JSON path.");

            string fullPath = Path.GetFullPath(configured);
            if (!Path.IsPathRooted(configured) ||
                !fullPath.StartsWith(
                    "/tmp/",
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    ResultEnvironmentVariable +
                    " must specify an absolute /tmp JSON path.");

            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "Performance result directory is unavailable.");
            Directory.CreateDirectory(directory);
            return fullPath;
        }

        private static Transform NewChild(
            Transform parent,
            string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }
    }
}
