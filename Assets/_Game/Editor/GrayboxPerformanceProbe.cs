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

        public static void SummarizeGuiProfilerCapture()
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
                    "FirstArtTerrainRenderer3D.LateUpdate() [Invoke]")
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
            File.WriteAllText(
                resultPath,
                JsonUtility.ToJson(result, true));
            Debug.Log("GUI Profiler summary result: " + resultPath);
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
