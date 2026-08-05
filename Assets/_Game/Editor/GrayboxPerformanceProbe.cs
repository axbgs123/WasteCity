using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.World;
using Debug = UnityEngine.Debug;

namespace WasteCity.Editor
{
    public static class GrayboxPerformanceProbe
    {
        private const string ResultEnvironmentVariable =
            "WASTECITY_GRAYBOX_PERF_RESULT";
        private const int RunCount = 5;

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
