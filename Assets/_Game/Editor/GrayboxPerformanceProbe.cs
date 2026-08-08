using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
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
        private const string GuiProfilerInputEnvironmentVariable =
            "WASTECITY_GUI_PROFILER_INPUT";
        private const string GuiProfilerResultEnvironmentVariable =
            "WASTECITY_GUI_PROFILER_RESULT";
        private const int RunCount = 5;
        private const int BuildingInstanceCount = 128;
        private const int CompletedBuildingCount = 43;
        private const int ConstructionBuildingCount = 43;

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
                    "GrayboxEvacuationController3D.Update() [Invoke]")
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
