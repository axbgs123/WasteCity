using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WasteCity.ArtIntegration3D;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;
using Debug = UnityEngine.Debug;

namespace WasteCity.Editor
{
    public static class FirstArtTerrainEvidenceCapture
    {
        public const string OutputRoot =
            "/tmp/wastecity-first-terrain/visual-review";
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;
        public const int VideoFrameCount = 300;
        public const int VideoFramesPerSecond = 30;
        public const int MinimumTerrainLuminance = 24;
        public const float MinimumLitTerrainCoverage = .2f;

        private const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        private const float DetailOrthographicSize = 4.5f;
        private const float OverviewOrthographicSize = 18f;
        private const string ProfilerDataPath =
            "/tmp/wastecity-first-terrain/task-09-first-terrain-300-frames.data";
        private const string AutomationSessionKey =
            "WasteCity.FirstArtTerrainEvidenceCapture.AutomationActive";
        private const string AcceptedAutomationSessionKey =
            "WasteCity.FirstArtTerrainEvidenceCapture.AcceptedAutomationActive";
        private const string AcceptedDecisionSessionKey =
            "WasteCity.FirstArtTerrainEvidenceCapture.AcceptedDecision";
        private const string AcceptedDecisionEnvironmentVariable =
            "WASTECITY_FIRST_ART_VISUAL_DECISION";
        private const string AcceptedDecisionToken =
            "user-accepted-known-visual-deviation";
        private const string CombinedAcceptedDecisionToken =
            "user-accepted-known-visual-and-motion-deviation";
        private const string AcceptedManifestDecision =
            "accepted-current-first-version";
        private const string CombinedAcceptedManifestDecision =
            "accepted-current-first-version-including-motion";
        private const double AutomationWaitTimeoutSeconds = 120d;
        private static CaptureSession activeSession;
        private static bool automationActive;
        private static bool acceptedAutomationActive;
        private static string acceptedAutomationDecision;
        private static double automationStartedAt;

        [Serializable]
        private sealed class EvidenceManifest
        {
            public int seed;
            public string scene;
            public string pipelineAssetPath;
            public string pipelineGuid;
            public string materialAssetPath;
            public string materialGuid;
            public int width;
            public int height;
            public CaptureRecord[] captures;
            public ZoomEvidenceRecord zoom;
            public VideoRecord video;
            public WaterEvidenceRecord water;
            public bool technicalVisualGatePassed;
            public string userVisualDecision;
            public string visualReviewResult;
            public string[] failedVisualThresholds;
            public string[] unresolvedFirstArtPassLimitations;
            public string approvalScope;
        }

        [Serializable]
        private sealed class AcceptedDeviationSummary
        {
            public bool technicalVisualGatePassed;
            public string userVisualDecision;
            public string visualReviewResult;
            public string[] failedVisualThresholds;
            public string[] unresolvedFirstArtPassLimitations;
            public string approvalScope;
        }

        [Serializable]
        private sealed class CaptureRecord
        {
            public string filename;
            public string siteName;
            public int primaryX;
            public int primaryY;
            public string primaryLayer;
            public int secondaryX;
            public int secondaryY;
            public string secondaryLayer;
            public Vector3 rigPosition;
            public Quaternion rigRotation;
            public Vector3 cameraLocalPosition;
            public Quaternion cameraLocalRotation;
            public float orthographicSize;
            public string resourceMarkerLod;
            public string uiPanelState;
            public int width;
            public int height;
            public string sha256;
        }

        [Serializable]
        private sealed class ZoomEvidenceRecord
        {
            public string frameDirectory;
            public int frameCount;
            public int width;
            public int height;
            public float originalOrthographicSize;
            public string originalResourceMarkerLod;
            public float restoredOrthographicSize;
            public string restoredResourceMarkerLod;
            public bool frameHashesVary;
            public ZoomFrameRecord[] frames;
        }

        [Serializable]
        private sealed class ZoomFrameRecord
        {
            public int index;
            public float scrollDeltaY;
            public float orthographicSize;
            public string resourceMarkerLod;
            public string filename;
            public int width;
            public int height;
            public string sha256;
        }

        [Serializable]
        private sealed class VideoRecord
        {
            public string filename;
            public string frameDirectory;
            public int framesPerSecond;
            public int frameCount;
            public int firstFrameNumber;
            public int lastFrameNumber;
            public int[] frameNumbers;
            public string cameraMatrix;
            public bool frameHashesVary;
        }

        [Serializable]
        private sealed class WaterEvidenceRecord
        {
            public int cellX;
            public int cellY;
            public Vector2[] insetVertices;
            public int roiPixelCount;
            public WaterColorRecord[] frames;
            public WaterMotionRecord[] motionPairs;
            public bool colorThresholdsPassed;
            public bool motionThresholdPassed;
        }

        [Serializable]
        private sealed class WaterColorRecord
        {
            public string label;
            public int capturedFrameOffset;
            public double meanR;
            public double meanG;
            public double meanB;
            public double meanLuminance;
        }

        [Serializable]
        private sealed class WaterMotionRecord
        {
            public string pair;
            public int firstCapturedFrameOffset;
            public int secondCapturedFrameOffset;
            public double meanDelta;
            public double nearestRankP95Delta;
        }

        public readonly struct CaptureSite
        {
            public CaptureSite(
                string name,
                int primaryX,
                int primaryY,
                FirstArtTerrainLayer3D primaryLayer,
                int secondaryX,
                int secondaryY,
                FirstArtTerrainLayer3D secondaryLayer)
            {
                Name = name;
                PrimaryX = primaryX;
                PrimaryY = primaryY;
                PrimaryLayer = primaryLayer;
                SecondaryX = secondaryX;
                SecondaryY = secondaryY;
                SecondaryLayer = secondaryLayer;
            }

            public string Name { get; }
            public int PrimaryX { get; }
            public int PrimaryY { get; }
            public FirstArtTerrainLayer3D PrimaryLayer { get; }
            public int SecondaryX { get; }
            public int SecondaryY { get; }
            public FirstArtTerrainLayer3D SecondaryLayer { get; }
        }

        internal readonly struct ZoomFrameSpec
        {
            public ZoomFrameSpec(
                int index,
                float scrollDeltaY,
                float orthographicSize,
                ResourceNodeMarkerLod3D lod)
            {
                Index = index;
                ScrollDeltaY = scrollDeltaY;
                OrthographicSize = orthographicSize;
                Lod = lod;
            }

            public int Index { get; }
            public float ScrollDeltaY { get; }
            public float OrthographicSize { get; }
            public ResourceNodeMarkerLod3D Lod { get; }
        }

        internal static IReadOnlyList<ZoomFrameSpec> BuildZoomFrameSpecs(
            FormalMapNavigationProfile3D profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (!profile.TryValidate(out string error))
                throw new ArgumentException(error, nameof(profile));

            const int frameCount = 10;
            var frames = new ZoomFrameSpec[frameCount];
            float size = profile.DefaultSize;
            for (var index = 0; index < frames.Length; index++)
            {
                float scrollDeltaY = index == 0 ? 0f : -120f;
                if (index > 0)
                {
                    size = profile.ResolveOrthographicSize(
                        size,
                        scrollDeltaY);
                }
                frames[index] = new ZoomFrameSpec(
                    index,
                    scrollDeltaY,
                    size,
                    profile.ResolveMarkerLod(size));
            }
            return frames;
        }

        internal static void ValidateZoomFrameSpecs(
            FormalMapNavigationProfile3D profile,
            IReadOnlyList<ZoomFrameSpec> frames)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (frames == null || frames.Count == 0)
                throw new ArgumentException(
                    "A non-empty zoom evidence trace is required.",
                    nameof(frames));

            var lods = new HashSet<ResourceNodeMarkerLod3D>();
            float previous = 0f;
            for (var index = 0; index < frames.Count; index++)
            {
                ZoomFrameSpec frame = frames[index];
                lods.Add(frame.Lod);
                if (frame.Index != index ||
                    frame.Lod != profile.ResolveMarkerLod(
                        frame.OrthographicSize))
                {
                    throw new InvalidOperationException(
                        "Zoom evidence indices and LOD must match the formal profile.");
                }
                if (index == 0)
                {
                    if (!Mathf.Approximately(frame.ScrollDeltaY, 0f) ||
                        !Mathf.Approximately(
                            frame.OrthographicSize,
                            profile.DefaultSize))
                    {
                        throw new InvalidOperationException(
                            "Zoom evidence must start at the formal default size.");
                    }
                }
                else
                {
                    float expected = profile.ResolveOrthographicSize(
                        previous,
                        frame.ScrollDeltaY);
                    if (frame.ScrollDeltaY >= 0f ||
                        frame.OrthographicSize <= previous ||
                        !Mathf.Approximately(
                            frame.OrthographicSize,
                            expected))
                    {
                        throw new InvalidOperationException(
                            "Zoom evidence sizes must increase monotonically " +
                            "through formal scroll steps.");
                    }
                }
                previous = frame.OrthographicSize;
            }

            if (!lods.Contains(ResourceNodeMarkerLod3D.Near) ||
                !lods.Contains(ResourceNodeMarkerLod3D.Mid) ||
                !lods.Contains(ResourceNodeMarkerLod3D.Far))
            {
                throw new InvalidOperationException(
                    "Zoom evidence must cross Near, Mid and Far marker LODs.");
            }
            if (frames.Count != 10)
            {
                throw new InvalidOperationException(
                    "Zoom evidence requires exactly ten fixed frames.");
            }
        }

        internal static void ValidateZoomRestoration(
            float originalOrthographicSize,
            float restoredOrthographicSize,
            ResourceNodeMarkerLod3D originalLod,
            ResourceNodeMarkerLod3D restoredLod)
        {
            if (!Mathf.Approximately(
                    originalOrthographicSize,
                    restoredOrthographicSize) ||
                originalLod != restoredLod)
            {
                throw new InvalidOperationException(
                    "Zoom evidence failed to restore the camera size and marker LOD.");
            }
        }

        internal readonly struct AcceptedWaterEvidence
        {
            public AcceptedWaterEvidence(
                string userVisualDecision,
                string[] failedThresholds,
                string[] motionFailures,
                bool motionThresholdPassed)
            {
                UserVisualDecision = userVisualDecision;
                FailedThresholds = failedThresholds ?? Array.Empty<string>();
                MotionFailures = motionFailures ?? Array.Empty<string>();
                MotionThresholdPassed = motionThresholdPassed;
            }

            public bool TechnicalVisualGatePassed => false;
            public string UserVisualDecision { get; }
            public string[] FailedThresholds { get; }
            public string[] MotionFailures { get; }
            public bool MotionThresholdPassed { get; }
        }

        [MenuItem("WasteCity/Art/Capture First Terrain Evidence")]
        public static void CaptureAll()
        {
            CaptureAllCore(false, null);
        }

        public static void CaptureAllAcceptedDeviation(string decision)
        {
            ValidateAcceptedDecision(decision);
            CaptureAllCore(true, decision);
        }

        private static void CaptureAllCore(bool acceptedDeviation, string decision)
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "First terrain evidence capture requires Play Mode.");
            }
            if (activeSession != null)
            {
                throw new InvalidOperationException(
                    "First terrain evidence capture is already running.");
            }

            CaptureContext context = ResolveAndValidateContext();
            GrayboxPerformanceProbe.RecordFirstArtTerrainRuntimeEvidence();
            activeSession = new CaptureSession(context, acceptedDeviation, decision);
            try
            {
                activeSession.Begin();
            }
            catch
            {
                CaptureSession failed = activeSession;
                activeSession = null;
                failed.Dispose(false);
                throw;
            }
        }

        public static void StartAutomatedCapture()
        {
            StartAutomatedCaptureCore(false, null);
        }

        public static void CaptureAllAcceptedDeviationFromEnvironment()
        {
            string decision = Environment.GetEnvironmentVariable(
                AcceptedDecisionEnvironmentVariable);
            ValidateAcceptedDecision(decision);
            StartAutomatedCaptureCore(true, decision);
        }

        private static void StartAutomatedCaptureCore(
            bool acceptedDeviation,
            string decision)
        {
            if (Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "Automated first terrain evidence requires the real Unity GUI.");
            }
            if (automationActive || activeSession != null || EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Automated first terrain evidence is already active or Play Mode has started.");
            }

            automationActive = true;
            acceptedAutomationActive = acceptedDeviation;
            acceptedAutomationDecision = acceptedDeviation ? decision : null;
            automationStartedAt = EditorApplication.timeSinceStartup;
            if (acceptedDeviation)
            {
                SessionState.SetBool(AcceptedAutomationSessionKey, true);
                SessionState.SetString(AcceptedDecisionSessionKey, decision);
            }
            else
            {
                SessionState.SetBool(AutomationSessionKey, true);
            }
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.playModeStateChanged += OnAutomationPlayModeStateChanged;
                EditorApplication.isPlaying = true;
            }
            catch
            {
                StopAutomationCallbacks();
                throw;
            }
        }

        [InitializeOnLoadMethod]
        private static void ResumeAutomationAfterDomainReload()
        {
            bool strictAutomation = SessionState.GetBool(AutomationSessionKey, false);
            if (strictAutomation)
            {
                acceptedAutomationActive = false;
                acceptedAutomationDecision = null;
            }
            else
            {
                bool acceptedAutomation =
                    SessionState.GetBool(AcceptedAutomationSessionKey, false);
                if (!acceptedAutomation)
                    return;
                string decision = SessionState.GetString(
                    AcceptedDecisionSessionKey,
                    string.Empty);
                ValidateAcceptedDecision(decision);
                acceptedAutomationActive = true;
                acceptedAutomationDecision = decision;
            }

            automationActive = true;
            automationStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.playModeStateChanged -= OnAutomationPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnAutomationPlayModeStateChanged;
            EditorApplication.update -= WaitForAutomatedRuntime;
            if (EditorApplication.isPlaying)
                EditorApplication.update += WaitForAutomatedRuntime;
        }

        [MenuItem("WasteCity/Art/Cancel First Terrain Evidence Capture")]
        public static void CancelCapture()
        {
            if (activeSession == null)
                return;

            CaptureSession cancelled = activeSession;
            activeSession = null;
            cancelled.Dispose(false);
            Debug.LogWarning("First terrain evidence capture cancelled.");
        }

        internal static void ValidateConsecutiveFrames(
            IReadOnlyList<int> frameNumbers,
            int expectedCount)
        {
            if (frameNumbers == null)
                throw new ArgumentNullException(nameof(frameNumbers));
            if (expectedCount < 1 || frameNumbers.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedCount} captured frames, found {frameNumbers.Count}.");
            }

            for (int index = 1; index < frameNumbers.Count; index++)
            {
                if (frameNumbers[index] != frameNumbers[index - 1] + 1)
                {
                    throw new InvalidOperationException(
                        "Captured frame numbers must be strictly consecutive; " +
                        $"found {frameNumbers[index - 1]}, {frameNumbers[index]} at index {index}.");
                }
            }
        }

        internal static bool ShouldCaptureVideoFrame(int capturedFrameCount)
        {
            if (capturedFrameCount < 0 ||
                capturedFrameCount > VideoFrameCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capturedFrameCount));
            }
            return capturedFrameCount < VideoFrameCount;
        }

        internal static bool ShouldFinalizeCapture(
            int capturedFrameCount,
            int profilerFrameCount)
        {
            if (capturedFrameCount < 0 ||
                capturedFrameCount > VideoFrameCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capturedFrameCount));
            }
            if (profilerFrameCount < 0 ||
                profilerFrameCount > VideoFrameCount)
            {
                throw new InvalidOperationException(
                    "Real GUI Profiler exceeded the exact 300-frame " +
                    $"capture window: {profilerFrameCount} frames.");
            }
            return capturedFrameCount == VideoFrameCount &&
                   profilerFrameCount == VideoFrameCount;
        }

        internal static void ValidateBuildGridDiagnosticState(
            GrayboxBuildingInteractionState state,
            bool gridVisible,
            bool expectedOpen)
        {
            GrayboxBuildingInteractionState expectedState = expectedOpen
                ? GrayboxBuildingInteractionState.CatalogOpen
                : GrayboxBuildingInteractionState.Inactive;
            if (state != expectedState || gridVisible != expectedOpen)
            {
                throw new InvalidOperationException(
                    "Build-grid diagnostic expected interaction " +
                    expectedState + " and grid visible=" + expectedOpen +
                    ", found interaction " + state +
                    " and grid visible=" + gridVisible + ".");
            }
        }

        public readonly struct WaterColorMetrics
        {
            public WaterColorMetrics(
                double meanR,
                double meanG,
                double meanB,
                double meanLuminance)
            {
                MeanR = meanR;
                MeanG = meanG;
                MeanB = meanB;
                MeanLuminance = meanLuminance;
            }

            public double MeanR { get; }
            public double MeanG { get; }
            public double MeanB { get; }
            public double MeanLuminance { get; }
        }

        public readonly struct WaterMotionMetrics
        {
            public WaterMotionMetrics(double meanDelta, double p95Delta)
            {
                MeanDelta = meanDelta;
                P95Delta = p95Delta;
            }

            public double MeanDelta { get; }
            public double P95Delta { get; }
        }

        public readonly struct WaterFramePair
        {
            public WaterFramePair(int first, int second)
            {
                First = first;
                Second = second;
            }

            public int First { get; }
            public int Second { get; }
        }

        internal static Vector2[] InsetWaterCell(
            IReadOnlyList<Vector2> projectedCorners,
            int width,
            int height)
        {
            ValidateImageDimensions(width, height);
            if (projectedCorners == null || projectedCorners.Count != 4)
            {
                throw new ArgumentException(
                    "A projected water cell requires exactly four corners.",
                    nameof(projectedCorners));
            }

            Vector2 centroid = Vector2.zero;
            for (int index = 0; index < projectedCorners.Count; index++)
            {
                if (!IsFinite(projectedCorners[index]))
                    throw new InvalidOperationException("Projected water cell contains a non-finite corner.");
                centroid += projectedCorners[index];
            }
            centroid /= projectedCorners.Count;

            var inset = new Vector2[4];
            for (int index = 0; index < inset.Length; index++)
            {
                inset[index] =
                    centroid + .65f * (projectedCorners[index] - centroid);
                if (!IsFinite(inset[index]) ||
                    inset[index].x < 0f || inset[index].x >= width ||
                    inset[index].y < 0f || inset[index].y >= height)
                {
                    throw new InvalidOperationException(
                        "Inset water ROI is clipped or outside the native image.");
                }
            }
            return inset;
        }

        internal static Vector2[] ProjectInsetWaterCell(
            Camera camera,
            PlanarCoordinateMapper3D mapper,
            int cellX,
            int cellY,
            int width,
            int height)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));
            ValidateImageDimensions(width, height);
            if (!mapper.TryCellToWorld(cellX, cellY, 0f, out Vector3 center))
                throw new ArgumentOutOfRangeException("DeepWater cell is outside the world mapper.");

            var worldCorners = new[]
            {
                center + new Vector3(-.5f, 0f, -.5f),
                center + new Vector3(.5f, 0f, -.5f),
                center + new Vector3(.5f, 0f, .5f),
                center + new Vector3(-.5f, 0f, .5f),
            };
            var projected = new Vector2[4];
            for (int index = 0; index < worldCorners.Length; index++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(worldCorners[index]);
                if (!IsFinite(new Vector2(viewport.x, viewport.y)) ||
                    float.IsNaN(viewport.z) || float.IsInfinity(viewport.z) ||
                    viewport.z <= 0f)
                {
                    throw new InvalidOperationException(
                        "DeepWater cell corner has an invalid camera projection.");
                }
                projected[index] = new Vector2(
                    viewport.x * width,
                    viewport.y * height);
            }
            return InsetWaterCell(projected, width, height);
        }

        internal static int[] BuildWaterRoiIndices(
            IReadOnlyList<Vector2> insetVertices,
            int width,
            int height)
        {
            ValidateImageDimensions(width, height);
            if (insetVertices == null || insetVertices.Count != 4)
            {
                throw new ArgumentException(
                    "A water ROI requires exactly four inset vertices.",
                    nameof(insetVertices));
            }

            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            for (int index = 0; index < insetVertices.Count; index++)
            {
                Vector2 vertex = insetVertices[index];
                if (!IsFinite(vertex) ||
                    vertex.x < 0f || vertex.x >= width ||
                    vertex.y < 0f || vertex.y >= height)
                {
                    throw new InvalidOperationException(
                        "Water ROI vertex is non-finite, clipped or outside the native image.");
                }
                minimumX = Mathf.Min(minimumX, vertex.x);
                minimumY = Mathf.Min(minimumY, vertex.y);
                maximumX = Mathf.Max(maximumX, vertex.x);
                maximumY = Mathf.Max(maximumY, vertex.y);
            }

            int firstX = Mathf.Max(0, Mathf.FloorToInt(minimumX - .5f));
            int firstY = Mathf.Max(0, Mathf.FloorToInt(minimumY - .5f));
            int lastX = Mathf.Min(width - 1, Mathf.CeilToInt(maximumX - .5f));
            int lastY = Mathf.Min(height - 1, Mathf.CeilToInt(maximumY - .5f));
            var indices = new List<int>((lastX - firstX + 1) * (lastY - firstY + 1));
            for (int y = firstY; y <= lastY; y++)
            for (int x = firstX; x <= lastX; x++)
            {
                if (IsInsideOrOnConvexPolygon(
                        new Vector2(x + .5f, y + .5f),
                        insetVertices))
                    indices.Add(y * width + x);
            }

            if (indices.Count < 64)
            {
                throw new InvalidOperationException(
                    "Water ROI must contain at least 64 native pixels; found " +
                    indices.Count + ".");
            }
            return indices.ToArray();
        }

        internal static WaterColorMetrics CalculateWaterColorMetrics(
            IReadOnlyList<Color32> pixels,
            IReadOnlyList<int> roiIndices)
        {
            ValidateMetricInputs(pixels, roiIndices);
            long red = 0L;
            long green = 0L;
            long blue = 0L;
            double luminance = 0d;
            for (int index = 0; index < roiIndices.Count; index++)
            {
                Color32 pixel = pixels[roiIndices[index]];
                red += pixel.r;
                green += pixel.g;
                blue += pixel.b;
                luminance +=
                    .2126d * pixel.r + .7152d * pixel.g + .0722d * pixel.b;
            }

            double denominator = roiIndices.Count * 255d;
            return new WaterColorMetrics(
                red / denominator,
                green / denominator,
                blue / denominator,
                luminance / denominator);
        }

        internal static WaterMotionMetrics CalculateWaterMotionMetrics(
            IReadOnlyList<Color32> first,
            IReadOnlyList<Color32> second,
            IReadOnlyList<int> roiIndices)
        {
            ValidateMetricInputs(first, roiIndices);
            ValidateMetricInputs(second, roiIndices);
            if (first.Count != second.Count)
                throw new ArgumentException("Water motion frames must have identical dimensions.");

            var deltas = new double[roiIndices.Count];
            double sum = 0d;
            for (int index = 0; index < roiIndices.Count; index++)
            {
                int pixelIndex = roiIndices[index];
                Color32 a = first[pixelIndex];
                Color32 b = second[pixelIndex];
                double delta =
                    (Math.Abs(a.r - b.r) +
                     Math.Abs(a.g - b.g) +
                     Math.Abs(a.b - b.b)) /
                    (3d * 255d);
                deltas[index] = delta;
                sum += delta;
            }

            Array.Sort(deltas);
            int nearestRankIndex =
                Math.Max(0, (int)Math.Ceiling(.95d * deltas.Length) - 1);
            return new WaterMotionMetrics(
                sum / deltas.Length,
                deltas[nearestRankIndex]);
        }

        internal static WaterFramePair[] ExactWaterFramePairs()
        {
            return new[]
            {
                new WaterFramePair(0, 1),
                new WaterFramePair(1, 2),
                new WaterFramePair(0, 2),
            };
        }

        internal static void ValidateWaterColorMetrics(WaterColorMetrics metrics)
        {
            const double minimumLuminance = 15d / 255d;
            const double maximumLuminance = 90d / 255d;
            if (metrics.MeanB < metrics.MeanR * 1.25d ||
                metrics.MeanB < metrics.MeanG * 1.10d ||
                metrics.MeanLuminance < minimumLuminance ||
                metrics.MeanLuminance > maximumLuminance)
            {
                throw new InvalidOperationException(
                    "DeepWater ROI failed blue-black color thresholds: " +
                    "R=" + metrics.MeanR.ToString("F6", CultureInfo.InvariantCulture) +
                    ", G=" + metrics.MeanG.ToString("F6", CultureInfo.InvariantCulture) +
                    ", B=" + metrics.MeanB.ToString("F6", CultureInfo.InvariantCulture) +
                    ", luminance=" + metrics.MeanLuminance.ToString("F6", CultureInfo.InvariantCulture) +
                    ", B/R=" + Ratio(metrics.MeanB, metrics.MeanR).ToString("F6", CultureInfo.InvariantCulture) +
                    " (required >= 1.250000), B/G=" +
                    Ratio(metrics.MeanB, metrics.MeanG).ToString("F6", CultureInfo.InvariantCulture) +
                    " (required >= 1.100000).");
            }
        }

        private static double Ratio(double numerator, double denominator)
        {
            return denominator == 0d ? double.PositiveInfinity : numerator / denominator;
        }

        internal static void ValidateWaterMotionMetrics(
            IReadOnlyList<WaterMotionMetrics> pairs)
        {
            if (pairs == null || pairs.Count != 3)
                throw new ArgumentException("Exactly three water frame pairs are required.", nameof(pairs));
            const double minimumMean = 1d / 255d;
            const double minimumP95 = 3d / 255d;
            for (int index = 0; index < pairs.Count; index++)
            {
                if (pairs[index].MeanDelta >= minimumMean &&
                    pairs[index].P95Delta >= minimumP95)
                    return;
            }
            throw new InvalidOperationException(
                "DeepWater ROI motion did not meet the mean and nearest-rank P95 thresholds in one exact pair.");
        }

        internal static AcceptedWaterEvidence EvaluateAcceptedWaterEvidence(
            string decision,
            IReadOnlyList<WaterColorMetrics> colors,
            IReadOnlyList<WaterMotionMetrics> motionPairs)
        {
            ValidateAcceptedDecision(decision);
            if (colors == null || colors.Count != 3)
            {
                throw new ArgumentException(
                    "Exactly three DeepWater color measurements are required.",
                    nameof(colors));
            }

            const double minimumLuminance = 15d / 255d;
            const double maximumLuminance = 90d / 255d;
            var failures = new List<string>(3);
            for (int index = 0; index < colors.Count; index++)
            {
                WaterColorMetrics metrics = colors[index];
                bool ratioFailed =
                    metrics.MeanB < metrics.MeanR * 1.25d ||
                    metrics.MeanB < metrics.MeanG * 1.10d;
                bool luminanceFailed =
                    metrics.MeanLuminance < minimumLuminance ||
                    metrics.MeanLuminance > maximumLuminance;
                if (luminanceFailed)
                {
                    ValidateWaterColorMetrics(metrics);
                    throw new InvalidOperationException(
                        "Unreachable accepted-evidence luminance validation state.");
                }
                if (!ratioFailed)
                    continue;

                try
                {
                    ValidateWaterColorMetrics(metrics);
                }
                catch (InvalidOperationException exception)
                {
                    failures.Add("t" + (index == 0 ? "0" : index == 1 ? "5" : "10") +
                                 ": " + exception.Message);
                }
            }

            if (failures.Count == 0)
            {
                throw new InvalidOperationException(
                    "Accepted-deviation capture requires the disclosed DeepWater blue-black ratio failure; use the strict capture when it passes.");
            }

            if (IsColorOnlyAcceptedDecision(decision))
            {
                ValidateWaterMotionMetrics(motionPairs);
                return new AcceptedWaterEvidence(
                    AcceptedManifestDecision,
                    failures.ToArray(),
                    Array.Empty<string>(),
                    true);
            }

            string[] motionFailures = DescribeFailedWaterMotionMetrics(motionPairs);
            if (motionFailures.Length == 0)
            {
                throw new InvalidOperationException(
                    "The combined accepted-deviation token requires an actual DeepWater motion failure; use the color-only or strict path when motion passes.");
            }
            failures.AddRange(motionFailures);
            return new AcceptedWaterEvidence(
                CombinedAcceptedManifestDecision,
                failures.ToArray(),
                motionFailures,
                false);
        }

        private static string[] DescribeFailedWaterMotionMetrics(
            IReadOnlyList<WaterMotionMetrics> pairs)
        {
            if (pairs == null || pairs.Count != 3)
                throw new ArgumentException("Exactly three water frame pairs are required.", nameof(pairs));
            const double minimumMean = 1d / 255d;
            const double minimumP95 = 3d / 255d;
            for (int index = 0; index < pairs.Count; index++)
            {
                if (pairs[index].MeanDelta >= minimumMean &&
                    pairs[index].P95Delta >= minimumP95)
                    return Array.Empty<string>();
            }

            string[] labels = { "t0-t5", "t5-t10", "t0-t10" };
            var failures = new string[pairs.Count];
            for (int index = 0; index < pairs.Count; index++)
            {
                WaterMotionMetrics metrics = pairs[index];
                bool missedMean = metrics.MeanDelta < minimumMean;
                bool missedP95 = metrics.P95Delta < minimumP95;
                failures[index] = labels[index] +
                    ": meanDelta=" + metrics.MeanDelta.ToString("F6", CultureInfo.InvariantCulture) +
                    " (required >= " + minimumMean.ToString("F6", CultureInfo.InvariantCulture) +
                    ", missed mean=" + missedMean.ToString().ToLowerInvariant() + ")" +
                    "; nearestRankP95Delta=" +
                    metrics.P95Delta.ToString("F6", CultureInfo.InvariantCulture) +
                    " (required >= " + minimumP95.ToString("F6", CultureInfo.InvariantCulture) +
                    ", missed P95=" + missedP95.ToString().ToLowerInvariant() + ").";
            }
            return failures;
        }

        private static void ValidateImageDimensions(int width, int height)
        {
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException("Native image dimensions must be positive.");
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private static bool IsInsideOrOnConvexPolygon(
            Vector2 point,
            IReadOnlyList<Vector2> vertices)
        {
            var hasPositive = false;
            var hasNegative = false;
            for (int index = 0; index < vertices.Count; index++)
            {
                Vector2 a = vertices[index];
                Vector2 b = vertices[(index + 1) % vertices.Count];
                float cross =
                    (b.x - a.x) * (point.y - a.y) -
                    (b.y - a.y) * (point.x - a.x);
                if (cross > .000001f)
                    hasPositive = true;
                else if (cross < -.000001f)
                    hasNegative = true;
                if (hasPositive && hasNegative)
                    return false;
            }
            return true;
        }

        private static void ValidateMetricInputs(
            IReadOnlyList<Color32> pixels,
            IReadOnlyList<int> roiIndices)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (roiIndices == null || roiIndices.Count == 0)
                throw new ArgumentException("A non-empty water ROI is required.", nameof(roiIndices));
            for (int index = 0; index < roiIndices.Count; index++)
            {
                if (roiIndices[index] < 0 || roiIndices[index] >= pixels.Count)
                    throw new ArgumentOutOfRangeException(nameof(roiIndices));
            }
        }

        internal static void ValidateNonBlackTerrainCoverage(
            IReadOnlyList<Color32> pixels,
            int width,
            int height,
            string label)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (width < 4 || height < 4 || pixels.Count != width * height)
            {
                throw new ArgumentException(
                    "Terrain luminance validation requires a complete " +
                    "image at least 4x4 pixels.");
            }

            int startX = width / 4;
            int endX = width - startX;
            int startY = height / 4;
            int endY = height - startY;
            long luminanceSum = 0L;
            var litPixelCount = 0;
            var sampleCount = 0;
            for (int y = startY; y < endY; y++)
            for (int x = startX; x < endX; x++)
            {
                Color32 pixel = pixels[y * width + x];
                int luminance =
                    (54 * pixel.r + 183 * pixel.g + 19 * pixel.b) >> 8;
                luminanceSum += luminance;
                if (luminance >= MinimumTerrainLuminance)
                    litPixelCount++;
                sampleCount++;
            }

            double averageLuminance =
                sampleCount > 0 ? (double)luminanceSum / sampleCount : 0d;
            double litCoverage =
                sampleCount > 0 ? (double)litPixelCount / sampleCount : 0d;
            if (averageLuminance < MinimumTerrainLuminance ||
                litCoverage < MinimumLitTerrainCoverage)
            {
                throw new InvalidOperationException(
                    $"{label} failed the non-black terrain gate: " +
                    $"average luminance {averageLuminance:F2}, lit " +
                    $"coverage {litCoverage:P1}.");
            }
        }

        internal static IReadOnlyList<CaptureSite> FindRequiredCaptureSites(
            WorldMapModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var sites = new List<CaptureSite>(7)
            {
                FindAdjacentPair(
                    model,
                    "wasteland-rocky",
                    FirstArtTerrainLayer3D.Wasteland,
                    FirstArtTerrainLayer3D.Rocky),
                FindAdjacentPair(
                    model,
                    "wasteland-wetland",
                    FirstArtTerrainLayer3D.Wasteland,
                    FirstArtTerrainLayer3D.Wetland),
                FindAdjacentPair(
                    model,
                    "wasteland-crystal",
                    FirstArtTerrainLayer3D.Wasteland,
                    FirstArtTerrainLayer3D.Crystal),
                FindThreeWayJunction(model),
                FindSpecialEdge(
                    model,
                    "ruins-edge",
                    FirstArtTerrainLayer3D.Ruins),
                FindSpecialEdge(
                    model,
                    "deep-water-shore",
                    FirstArtTerrainLayer3D.DeepWater),
                FindSpecialEdge(
                    model,
                    "cliff-edge",
                    FirstArtTerrainLayer3D.Cliff),
            };
            return sites;
        }

        internal static long CalculateCompressedPayloadBytes(Texture2DArray array)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            long bytesPerSlice = 0L;
            int width = array.width;
            int height = array.height;
            for (int mip = 0; mip < array.mipmapCount; mip++)
            {
                switch (array.format)
                {
                    case TextureFormat.BC7:
                        bytesPerSlice +=
                            ((width + 3L) / 4L) *
                            ((height + 3L) / 4L) *
                            16L;
                        break;
                    case TextureFormat.R8:
                        bytesPerSlice += (long)width * height;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Unsupported first-terrain runtime array format: " +
                            array.format);
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
            }

            return bytesPerSlice * array.depth;
        }

        internal static IDisposable PrepareUiCanvasesForCameraRender(
            Camera camera,
            IReadOnlyList<Canvas> canvases)
        {
            return new UiCanvasRenderScope(camera, canvases);
        }

        private sealed class UiCanvasRenderScope : IDisposable
        {
            private readonly Camera camera;
            private readonly int originalCullingMask;
            private readonly CanvasRenderState[] states;
            private bool disposed;

            public UiCanvasRenderScope(
                Camera camera,
                IReadOnlyList<Canvas> canvases)
            {
                this.camera = camera != null
                    ? camera
                    : throw new ArgumentNullException(nameof(camera));
                if (canvases == null)
                    throw new ArgumentNullException(nameof(canvases));

                originalCullingMask = camera.cullingMask;
                states = canvases
                    .Where(canvas => canvas != null)
                    .Distinct()
                    .Select(canvas => new CanvasRenderState(canvas))
                    .ToArray();
                if (states.Length == 0)
                {
                    throw new InvalidOperationException(
                        "UI evidence requires at least one formal Canvas.");
                }

                for (var index = 0; index < states.Length; index++)
                {
                    Canvas canvas = states[index].Canvas;
                    foreach (Transform child in
                             canvas.GetComponentsInChildren<Transform>(true))
                    {
                        camera.cullingMask |= 1 << child.gameObject.layer;
                    }
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Math.Max(
                        1f,
                        camera.nearClipPlane + .1f);
                }
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                try
                {
                    for (var index = states.Length - 1; index >= 0; index--)
                        states[index].Restore();
                }
                finally
                {
                    camera.cullingMask = originalCullingMask;
                    Canvas.ForceUpdateCanvases();
                }
            }
        }

        private readonly struct CanvasRenderState
        {
            private readonly RenderMode renderMode;
            private readonly Camera worldCamera;
            private readonly float planeDistance;

            public CanvasRenderState(Canvas canvas)
            {
                Canvas = canvas;
                renderMode = canvas.renderMode;
                worldCamera = canvas.worldCamera;
                planeDistance = canvas.planeDistance;
            }

            public Canvas Canvas { get; }

            public void Restore()
            {
                if (Canvas == null)
                    return;
                Canvas.renderMode = renderMode;
                Canvas.worldCamera = worldCamera;
                Canvas.planeDistance = planeDistance;
            }
        }

        private static CaptureContext ResolveAndValidateContext()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "First terrain evidence requires the loaded GrayboxPrototype3D scene.");
            }

            GrayboxSceneBootstrap[] bootstraps =
                UnityEngine.Object.FindObjectsOfType<GrayboxSceneBootstrap>(true);
            GrayboxWorldView3D[] worlds =
                UnityEngine.Object.FindObjectsOfType<GrayboxWorldView3D>(true);
            FirstArtTerrainRenderer3D[] presenters =
                UnityEngine.Object.FindObjectsOfType<FirstArtTerrainRenderer3D>(true);
            GrayboxUrpScope[] scopes =
                UnityEngine.Object.FindObjectsOfType<GrayboxUrpScope>(true);
            GrayboxBuildingInteractionModel3D[] buildingInteractions =
                UnityEngine.Object.FindObjectsOfType<GrayboxBuildingInteractionModel3D>(true);
            GrayboxBuildingWorldView3D[] buildingWorlds =
                UnityEngine.Object.FindObjectsOfType<GrayboxBuildingWorldView3D>(true);
            GrayboxBuildingInputRouter3D[] buildingRouters =
                UnityEngine.Object.FindObjectsOfType<GrayboxBuildingInputRouter3D>(true);
            GrayboxOperationsView3D[] operationsViews =
                UnityEngine.Object.FindObjectsOfType<GrayboxOperationsView3D>(true);
            Canvas[] canvases =
                UnityEngine.Object.FindObjectsOfType<Canvas>(true);
            if (bootstraps.Length != 1 || worlds.Length != 1 ||
                presenters.Length != 1 || scopes.Length != 1 ||
                buildingInteractions.Length != 1 || buildingWorlds.Length != 1 ||
                buildingRouters.Length != 1 || operationsViews.Length != 1 ||
                canvases.Length < 3)
            {
                throw new InvalidOperationException(
                    "Evidence capture requires exactly one bootstrap, world, presenter, " +
                    "URP scope, operations view and serialized building runtime chain, " +
                    "plus the three formal Canvas roots.");
            }

            GrayboxSceneBootstrap bootstrap = bootstraps[0];
            GrayboxWorldView3D world = worlds[0];
            FirstArtTerrainRenderer3D presenter = presenters[0];
            if (!bootstrap.IsInitialized || bootstrap.World == null ||
                bootstrap.World.Width != GrayboxSceneBootstrap.WorldWidth ||
                bootstrap.World.Height != GrayboxSceneBootstrap.WorldHeight ||
                world.Model != bootstrap.World || world.Coordinates == null)
            {
                throw new InvalidOperationException(
                    "Evidence capture requires the initialized seed-8128 32x24 world.");
            }
            if (!scopes[0].IsApplied || GraphicsSettings.renderPipelineAsset == null)
                throw new InvalidOperationException("Approved URP pipeline is not active.");
            string profileError = null;
            if (!presenter.IsPresented || presenter.Profile == null ||
                !presenter.Profile.TryValidate(out profileError))
            {
                throw new InvalidOperationException(
                    "Approved first terrain profile is not presented: " + profileError);
            }
            if (presenter.SurfaceRenderer == null ||
                presenter.SurfaceRenderer.transform.parent !=
                presenter.transform)
            {
                throw new InvalidOperationException(
                    "Evidence capture requires the formal terrain surface Renderer.");
            }

            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic || camera.transform.parent == null)
                throw new InvalidOperationException("Approved orthographic Game camera and rig are required.");

            GrayboxCameraController3D cameraController =
                camera.transform.parent.GetComponent<GrayboxCameraController3D>();
            FormalMapNavigationProfile3D mapNavigationProfile =
                cameraController != null
                    ? cameraController.MapNavigationProfile
                    : null;
            if (mapNavigationProfile == null)
            {
                mapNavigationProfile = Resources.Load<
                    FormalMapNavigationProfile3D>(
                    FormalMapNavigationProfile3D.ResourcesPath);
            }
            string navigationError = string.Empty;
            if (mapNavigationProfile == null ||
                !mapNavigationProfile.TryValidate(out navigationError))
            {
                throw new InvalidOperationException(
                    "Formal map navigation profile is unavailable: " +
                    navigationError);
            }
            return new CaptureContext(
                bootstrap,
                world,
                presenter,
                camera,
                camera.transform.parent,
                cameraController,
                mapNavigationProfile,
                buildingInteractions[0],
                buildingWorlds[0],
                operationsViews[0],
                canvases,
                FindRequiredCaptureSites(bootstrap.World));
        }

        private static void OnAutomationPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.update -= WaitForAutomatedRuntime;
                EditorApplication.update += WaitForAutomatedRuntime;
                return;
            }
            if (change == PlayModeStateChange.ExitingPlayMode)
                StopAutomationCallbacks();
        }

        private static void WaitForAutomatedRuntime()
        {
            GrayboxSceneBootstrap bootstrap =
                UnityEngine.Object.FindObjectOfType<GrayboxSceneBootstrap>();
            FirstArtTerrainRenderer3D presenter =
                UnityEngine.Object.FindObjectOfType<FirstArtTerrainRenderer3D>();
            try
            {
                double elapsedSeconds =
                    EditorApplication.timeSinceStartup - automationStartedAt;
                if (!IsAutomatedRuntimeReady(
                        bootstrap != null,
                        bootstrap != null && bootstrap.IsInitialized,
                        presenter != null,
                        presenter != null && presenter.IsPresented,
                        presenter != null ? presenter.LastPresentationError : null,
                        elapsedSeconds))
                    return;

                bool acceptedDeviation = acceptedAutomationActive;
                string decision = acceptedAutomationDecision;
                StopAutomationCallbacks();
                if (acceptedDeviation)
                    CaptureAllAcceptedDeviation(decision);
                else
                    CaptureAll();
            }
            catch (Exception exception)
            {
                StopAutomationCallbacks();
                Debug.LogException(exception);
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
            }
        }

        internal static bool IsAutomatedRuntimeReady(
            bool bootstrapExists,
            bool bootstrapInitialized,
            bool presenterExists,
            bool presenterPresented,
            string presentationError,
            double elapsedSeconds)
        {
            if (bootstrapInitialized && presenterExists &&
                !presenterPresented && !string.IsNullOrEmpty(presentationError))
            {
                throw new InvalidOperationException(
                    "Automated first terrain presentation failed: " +
                    presentationError);
            }

            if (bootstrapExists && bootstrapInitialized &&
                presenterExists && presenterPresented)
                return true;

            if (elapsedSeconds >= AutomationWaitTimeoutSeconds)
            {
                throw new TimeoutException(
                    "Automated first terrain runtime timed out after " +
                    elapsedSeconds.ToString("F1", CultureInfo.InvariantCulture) +
                    " seconds: bootstrapExists=" + bootstrapExists +
                    ", bootstrapInitialized=" + bootstrapInitialized +
                    ", presenterExists=" + presenterExists +
                    ", presenterPresented=" + presenterPresented +
                    ".");
            }

            return false;
        }

        private static void StopAutomationCallbacks()
        {
            automationActive = false;
            acceptedAutomationActive = false;
            acceptedAutomationDecision = null;
            automationStartedAt = 0d;
            SessionState.SetBool(AutomationSessionKey, false);
            SessionState.SetBool(AcceptedAutomationSessionKey, false);
            SessionState.EraseString(AcceptedDecisionSessionKey);
            EditorApplication.update -= WaitForAutomatedRuntime;
            EditorApplication.playModeStateChanged -= OnAutomationPlayModeStateChanged;
        }

        private static void ValidateAcceptedDecision(string decision)
        {
            if (!IsColorOnlyAcceptedDecision(decision) &&
                !IsCombinedAcceptedDecision(decision))
            {
                throw new InvalidOperationException(
                    "Accepted first-terrain evidence requires the exact user decision token.");
            }
        }

        private static bool IsColorOnlyAcceptedDecision(string decision)
        {
            return string.Equals(
                decision,
                AcceptedDecisionToken,
                StringComparison.Ordinal);
        }

        private static bool IsCombinedAcceptedDecision(string decision)
        {
            return string.Equals(
                decision,
                CombinedAcceptedDecisionToken,
                StringComparison.Ordinal);
        }

        internal static void ValidateAcceptedEvidenceIntegrity(
            string decision,
            string scenePath,
            bool profilerOutputExists,
            IReadOnlyList<int> capturedFrameNumbers,
            int expectedFrameCount)
        {
            ValidateAcceptedDecision(decision);
            ValidateConsecutiveFrames(capturedFrameNumbers, expectedFrameCount);
            if (!string.Equals(scenePath, ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("Accepted evidence scene is not GrayboxPrototype3D.");
            if (!profilerOutputExists)
                throw new InvalidOperationException("Accepted evidence requires the real GUI Profiler output.");
        }

        private static CaptureSite FindAdjacentPair(
            WorldMapModel model,
            string name,
            FirstArtTerrainLayer3D first,
            FirstArtTerrainLayer3D second)
        {
            foreach (CellPair pair in AdjacentPairs(model))
            {
                FirstArtTerrainLayer3D a = LayerAt(model, pair.Ax, pair.Ay);
                FirstArtTerrainLayer3D b = LayerAt(model, pair.Bx, pair.By);
                if (a == first && b == second)
                    return new CaptureSite(name, pair.Ax, pair.Ay, a, pair.Bx, pair.By, b);
                if (a == second && b == first)
                    return new CaptureSite(name, pair.Bx, pair.By, b, pair.Ax, pair.Ay, a);
            }

            throw new InvalidOperationException($"Seed world has no required {name} boundary.");
        }

        private static CaptureSite FindSpecialEdge(
            WorldMapModel model,
            string name,
            FirstArtTerrainLayer3D special)
        {
            foreach (CellPair pair in AdjacentPairs(model))
            {
                FirstArtTerrainLayer3D a = LayerAt(model, pair.Ax, pair.Ay);
                FirstArtTerrainLayer3D b = LayerAt(model, pair.Bx, pair.By);
                if (a == special && IsPassablePresentationLayer(b))
                    return new CaptureSite(name, pair.Ax, pair.Ay, a, pair.Bx, pair.By, b);
                if (b == special && IsPassablePresentationLayer(a))
                    return new CaptureSite(name, pair.Bx, pair.By, b, pair.Ax, pair.Ay, a);
            }

            throw new InvalidOperationException($"Seed world has no required {name} boundary.");
        }

        private static CaptureSite FindThreeWayJunction(WorldMapModel model)
        {
            for (int y = 0; y < model.Height - 1; y++)
            for (int x = 0; x < model.Width - 1; x++)
            {
                var layers = new HashSet<FirstArtTerrainLayer3D>
                {
                    LayerAt(model, x, y),
                    LayerAt(model, x + 1, y),
                    LayerAt(model, x, y + 1),
                    LayerAt(model, x + 1, y + 1),
                };
                if (layers.Count < 3)
                    continue;

                FirstArtTerrainLayer3D primary = LayerAt(model, x, y);
                FirstArtTerrainLayer3D secondary = LayerAt(model, x + 1, y + 1);
                return new CaptureSite(
                    "three-way-junction",
                    x,
                    y,
                    primary,
                    x + 1,
                    y + 1,
                    secondary);
            }

            throw new InvalidOperationException("Seed world has no three-way 2x2 junction.");
        }

        private static IEnumerable<CellPair> AdjacentPairs(WorldMapModel model)
        {
            for (int y = 0; y < model.Height; y++)
            for (int x = 0; x < model.Width; x++)
            {
                if (x + 1 < model.Width)
                    yield return new CellPair(x, y, x + 1, y);
                if (y + 1 < model.Height)
                    yield return new CellPair(x, y, x, y + 1);
            }
        }

        private static FirstArtTerrainLayer3D LayerAt(WorldMapModel model, int x, int y)
        {
            return FirstArtTerrainCatalog3D.LayerOf(model.Get(x, y));
        }

        private static bool IsPassablePresentationLayer(FirstArtTerrainLayer3D layer)
        {
            return layer != FirstArtTerrainLayer3D.DeepWater &&
                   layer != FirstArtTerrainLayer3D.Cliff;
        }

        private readonly struct CellPair
        {
            public CellPair(int ax, int ay, int bx, int by)
            {
                Ax = ax;
                Ay = ay;
                Bx = bx;
                By = by;
            }

            public int Ax { get; }
            public int Ay { get; }
            public int Bx { get; }
            public int By { get; }
        }

        private sealed class CaptureContext
        {
            public CaptureContext(
                GrayboxSceneBootstrap bootstrap,
                GrayboxWorldView3D world,
                FirstArtTerrainRenderer3D presenter,
                Camera camera,
                Transform rig,
                GrayboxCameraController3D cameraController,
                FormalMapNavigationProfile3D mapNavigationProfile,
                GrayboxBuildingInteractionModel3D buildingInteraction,
                GrayboxBuildingWorldView3D buildingWorld,
                GrayboxOperationsView3D operationsView,
                IReadOnlyList<Canvas> canvases,
                IReadOnlyList<CaptureSite> sites)
            {
                Bootstrap = bootstrap;
                World = world;
                Presenter = presenter;
                Camera = camera;
                Rig = rig;
                CameraController = cameraController;
                MapNavigationProfile = mapNavigationProfile;
                BuildingInteraction = buildingInteraction;
                BuildingWorld = buildingWorld;
                OperationsView = operationsView;
                Canvases = canvases;
                Sites = sites;
            }

            public GrayboxSceneBootstrap Bootstrap { get; }
            public GrayboxWorldView3D World { get; }
            public FirstArtTerrainRenderer3D Presenter { get; }
            public Camera Camera { get; }
            public Transform Rig { get; }
            public GrayboxCameraController3D CameraController { get; }
            public FormalMapNavigationProfile3D MapNavigationProfile { get; }
            public GrayboxBuildingInteractionModel3D BuildingInteraction { get; }
            public GrayboxBuildingWorldView3D BuildingWorld { get; }
            public GrayboxOperationsView3D OperationsView { get; }
            public IReadOnlyList<Canvas> Canvases { get; }
            public IReadOnlyList<CaptureSite> Sites { get; }
        }

        private sealed class CaptureSession
        {
            private readonly CaptureContext context;
            private readonly bool acceptedDeviation;
            private readonly string acceptedDecision;
            private readonly Vector3 originalRigPosition;
            private readonly Quaternion originalRigRotation;
            private readonly Vector3 originalCameraLocalPosition;
            private readonly Quaternion originalCameraLocalRotation;
            private readonly float originalOrthographicSize;
            private readonly RenderTexture originalTargetTexture;
            private readonly bool originalPresenterEnabled;
            private readonly bool originalFallbackVisibility;
            private readonly bool originalCameraControllerEnabled;
            private readonly int originalCaptureFramerate;
            private readonly bool originalProfilerEnabled;
            private readonly RenderTexture originalActiveRenderTexture;
            private readonly InputSettings.UpdateMode originalInputUpdateMode;
            private readonly InputSettings.BackgroundBehavior originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode
                originalEditorInputBehavior;
            private readonly bool originalInventoryOpen;
            private readonly bool originalResearchOpen;
            private readonly bool originalLedgerOpen;
            private readonly List<CaptureRecord> captures = new List<CaptureRecord>(16);
            private readonly List<int> frameNumbers = new List<int>(VideoFrameCount);
            private readonly HashSet<string> frameHashes = new HashSet<string>(StringComparer.Ordinal);
            private readonly string frameDirectory = Path.Combine(OutputRoot, "deep-water-frames");
            private readonly string zoomFrameDirectory =
                Path.Combine(OutputRoot, "zoom-frames");
            private readonly Color32[][] waterMetricFrames = new Color32[3][];
            private Matrix4x4 firstWorldToCamera;
            private Matrix4x4 firstProjection;
            private RenderTexture videoTargetTexture;
            private int lastObservedFrame;
            private string completedVideoPath;
            private bool profilerOnlyCaptureActive;
            private Keyboard diagnosticKeyboard;
            private int diagnosticQueuedAtFrame;
            private double diagnosticStartedAt;
            private BuildGridDiagnosticPhase diagnosticPhase;
            private bool videoCallbacksRegistered;
            private bool diagnosticCallbackRegistered;
            private bool playModeCallbackRegistered;
            private bool disposed;
            private int waterCellX;
            private int waterCellY;
            private int[] waterRoiIndices;
            private Vector2[] waterInsetVertices;
            private WaterEvidenceRecord waterEvidence;
            private AcceptedWaterEvidence acceptedWaterEvidence;
            private ZoomEvidenceRecord zoomEvidence;

            private enum BuildGridDiagnosticPhase
            {
                None,
                AwaitOpen,
                AwaitOpenRelease,
                AwaitClose,
                Complete,
            }

            public CaptureSession(
                CaptureContext context,
                bool acceptedDeviation,
                string acceptedDecision)
            {
                this.context = context;
                this.acceptedDeviation = acceptedDeviation;
                this.acceptedDecision = acceptedDecision;
                originalRigPosition = context.Rig.position;
                originalRigRotation = context.Rig.rotation;
                originalCameraLocalPosition = context.Camera.transform.localPosition;
                originalCameraLocalRotation = context.Camera.transform.localRotation;
                originalOrthographicSize = context.Camera.orthographicSize;
                originalTargetTexture = context.Camera.targetTexture;
                originalPresenterEnabled = context.Presenter.enabled;
                originalFallbackVisibility = context.World.SurfaceFallbackVisible;
                originalCameraControllerEnabled =
                    context.CameraController != null && context.CameraController.enabled;
                originalCaptureFramerate = Time.captureFramerate;
                originalProfilerEnabled = ProfilerDriver.enabled;
                originalActiveRenderTexture = RenderTexture.active;
                originalInputUpdateMode = InputSystem.settings.updateMode;
                originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior =
                    InputSystem.settings.editorInputBehaviorInPlayMode;
                originalInventoryOpen = context.OperationsView.IsInventoryOpen;
                originalResearchOpen = context.OperationsView.IsResearchOpen;
                originalLedgerOpen = context.OperationsView.IsLedgerOpen;
            }

            public void Begin()
            {
                PrepareOutputDirectory();
                if (context.CameraController != null)
                    context.CameraController.enabled = false;

                ValidateBuildGridDiagnosticState(
                    context.BuildingInteraction.State,
                    context.BuildingWorld.IsBuildGridVisible,
                    false);
                StartBuildGridDiagnostic();
            }

            private void StartVideoEvidence()
            {
                CaptureStills();
                CaptureSite shore = context.Sites.Single(site =>
                    string.Equals(site.Name, "deep-water-shore", StringComparison.Ordinal));
                FocusSite(shore, DetailOrthographicSize);
                firstWorldToCamera = context.Camera.worldToCameraMatrix;
                firstProjection = context.Camera.projectionMatrix;
                videoTargetTexture = new RenderTexture(
                    CaptureWidth,
                    CaptureHeight,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);
                videoTargetTexture.Create();
                context.Camera.targetTexture = videoTargetTexture;
                waterCellX = shore.PrimaryX;
                waterCellY = shore.PrimaryY;
                waterInsetVertices = ProjectInsetWaterCell(
                    context.Camera,
                    context.World.Coordinates,
                    waterCellX,
                    waterCellY,
                    CaptureWidth,
                    CaptureHeight);
                waterRoiIndices = BuildWaterRoiIndices(
                    waterInsetVertices,
                    CaptureWidth,
                    CaptureHeight);
                Time.captureFramerate = VideoFramesPerSecond;
                ProfilerDriver.enabled = false;
                ProfilerDriver.ClearAllFrames();
                lastObservedFrame = Time.frameCount;
                RegisterVideoCallbacks();
                Debug.Log("First terrain evidence video capture started: " + OutputRoot);
            }

            public void Dispose(bool completed)
            {
                if (disposed)
                    return;
                disposed = true;
                UnregisterCallbacks();
                RemoveDiagnosticKeyboard();

                try
                {
                    Time.captureFramerate = originalCaptureFramerate;
                    ProfilerDriver.enabled = originalProfilerEnabled;
                    context.Camera.targetTexture = originalTargetTexture;
                    RenderTexture.active = originalActiveRenderTexture;
                    if (videoTargetTexture != null)
                    {
                        videoTargetTexture.Release();
                        UnityEngine.Object.Destroy(videoTargetTexture);
                        videoTargetTexture = null;
                    }
                    context.Camera.orthographicSize = originalOrthographicSize;
                    context.Camera.transform.localPosition = originalCameraLocalPosition;
                    context.Camera.transform.localRotation = originalCameraLocalRotation;
                    context.Rig.position = originalRigPosition;
                    context.Rig.rotation = originalRigRotation;
                    context.World.RefreshResourceNodeMarkerLod(
                        originalOrthographicSize);
                    RestoreUiPanelState();
                    context.Presenter.enabled = originalPresenterEnabled;
                    if (context.CameraController != null)
                        context.CameraController.enabled = originalCameraControllerEnabled;
                    RestorePresentation();
                    context.World.SetSurfaceFallbackVisible(originalFallbackVisibility);
                    if (diagnosticPhase == BuildGridDiagnosticPhase.Complete || completed)
                    {
                        ValidateBuildGridDiagnosticState(
                            context.BuildingInteraction.State,
                            context.BuildingWorld.IsBuildGridVisible,
                            false);
                    }
                }
                finally
                {
                    try
                    {
                        InputSystem.settings.updateMode = originalInputUpdateMode;
                    }
                    finally
                    {
                        try
                        {
                            InputSystem.settings.editorInputBehaviorInPlayMode =
                                originalEditorInputBehavior;
                        }
                        finally
                        {
                            InputSystem.settings.backgroundBehavior =
                                originalBackgroundBehavior;
                            if (!completed && Directory.Exists(OutputRoot))
                                Directory.Delete(OutputRoot, true);
                        }
                    }
                }
            }

            private void CaptureStills()
            {
                RestoreDefaultCamera();
                CapturePng("02-default-game-camera.png", null);

                FocusMapOverview();
                CapturePng("01-map-overview.png", null);

                CaptureNamedSite("03-wasteland-rocky.png", "wasteland-rocky");
                CaptureNamedSite("04-wasteland-wetland.png", "wasteland-wetland");
                CaptureNamedSite("05-wasteland-crystal.png", "wasteland-crystal");
                CaptureNamedSite("06-three-way-junction.png", "three-way-junction");
                CaptureNamedSite("07-ruins-edge.png", "ruins-edge");
                CaptureNamedSite("08-deep-water-shore.png", "deep-water-shore");
                CaptureNamedSite("09-cliff-edge.png", "cliff-edge");

                RestoreDefaultCamera();
                CaptureComparison("10-graybox-formal-comparison.png");

                CaptureResourceMarkerLodStills();
                CaptureZoomEvidence();
                CaptureUiEvidence();
            }

            private void StartBuildGridDiagnostic()
            {
                InputSystem.settings.updateMode =
                    InputSettings.UpdateMode.ProcessEventsManually;
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode
                        .AllDeviceInputAlwaysGoesToGameView;
                diagnosticKeyboard = InputSystem.AddDevice<Keyboard>();
                diagnosticKeyboard.MakeCurrent();
                diagnosticStartedAt = EditorApplication.timeSinceStartup;
                diagnosticPhase = BuildGridDiagnosticPhase.AwaitOpen;
                QueueDiagnosticB(true);
                diagnosticQueuedAtFrame = Time.frameCount;
                diagnosticCallbackRegistered = true;
                EditorApplication.update += TickBuildGridDiagnostic;
                playModeCallbackRegistered = true;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                Debug.Log("First terrain build-grid diagnostic started through virtual B input.");
            }

            private void TickBuildGridDiagnostic()
            {
                try
                {
                    if (!EditorApplication.isPlaying)
                    {
                        throw new InvalidOperationException(
                            "Play Mode ended during the build-grid diagnostic.");
                    }
                    if (EditorApplication.timeSinceStartup - diagnosticStartedAt > 30d)
                    {
                        throw new TimeoutException(
                            "Build-grid diagnostic did not complete within 30 seconds; " +
                            "phase=" + diagnosticPhase + ".");
                    }
                    if (Time.frameCount <= diagnosticQueuedAtFrame)
                        return;

                    switch (diagnosticPhase)
                    {
                        case BuildGridDiagnosticPhase.AwaitOpen:
                            ValidateBuildGridDiagnosticState(
                                context.BuildingInteraction.State,
                                context.BuildingWorld.IsBuildGridVisible,
                                true);
                            RestoreDefaultCamera();
                            byte[] diagnosticBytes = RenderCameraPng(
                                context.Camera,
                                CaptureWidth,
                                CaptureHeight);
                            ValidateNonBlackTerrainCoverage(
                                DecodePng(diagnosticBytes),
                                CaptureWidth,
                                CaptureHeight,
                                "12-build-mode-grid-diagnostic.png");
                            File.WriteAllBytes(
                                Path.Combine(
                                    OutputRoot,
                                    "12-build-mode-grid-diagnostic.png"),
                                diagnosticBytes);
                            QueueDiagnosticB(false);
                            diagnosticQueuedAtFrame = Time.frameCount;
                            diagnosticPhase = BuildGridDiagnosticPhase.AwaitOpenRelease;
                            return;

                        case BuildGridDiagnosticPhase.AwaitOpenRelease:
                            QueueDiagnosticB(true);
                            diagnosticQueuedAtFrame = Time.frameCount;
                            diagnosticPhase = BuildGridDiagnosticPhase.AwaitClose;
                            return;

                        case BuildGridDiagnosticPhase.AwaitClose:
                            ValidateBuildGridDiagnosticState(
                                context.BuildingInteraction.State,
                                context.BuildingWorld.IsBuildGridVisible,
                                false);
                            QueueDiagnosticB(false);
                            RemoveDiagnosticKeyboard();
                            diagnosticPhase = BuildGridDiagnosticPhase.Complete;
                            EditorApplication.update -= TickBuildGridDiagnostic;
                            diagnosticCallbackRegistered = false;
                            StartVideoEvidence();
                            return;
                    }
                }
                catch (Exception exception)
                {
                    CaptureSession failed = activeSession;
                    activeSession = null;
                    failed?.Dispose(false);
                    Debug.LogException(exception);
                }
            }

            private void QueueDiagnosticB(bool pressed)
            {
                if (diagnosticKeyboard == null || !diagnosticKeyboard.added)
                {
                    throw new InvalidOperationException(
                        "The virtual build-grid diagnostic keyboard is unavailable.");
                }
                InputSystem.QueueStateEvent(
                    diagnosticKeyboard,
                    pressed ? new KeyboardState(Key.B) : new KeyboardState());
                InputSystem.Update();
            }

            private void RemoveDiagnosticKeyboard()
            {
                if (diagnosticKeyboard == null)
                    return;
                if (diagnosticKeyboard.added)
                    InputSystem.RemoveDevice(diagnosticKeyboard);
                diagnosticKeyboard = null;
            }

            private void CaptureNamedSite(string filename, string siteName)
            {
                CaptureSite site = context.Sites.Single(candidate =>
                    string.Equals(candidate.Name, siteName, StringComparison.Ordinal));
                FocusSite(site, DetailOrthographicSize);
                CapturePng(filename, site);
            }

            private void CapturePng(
                string filename,
                CaptureSite? site,
                string uiPanelState = "world-only")
            {
                byte[] bytes = RenderCameraPng(context.Camera, CaptureWidth, CaptureHeight);
                ValidateNonBlackTerrainCoverage(
                    DecodePng(bytes),
                    CaptureWidth,
                    CaptureHeight,
                    filename);
                File.WriteAllBytes(Path.Combine(OutputRoot, filename), bytes);
                captures.Add(NewCaptureRecord(
                    filename,
                    site,
                    bytes,
                    uiPanelState));
            }

            private void CaptureResourceMarkerLodStills()
            {
                RestoreDefaultCamera();
                CaptureResourceMarkerLod(
                    "13-resource-marker-near.png",
                    context.MapNavigationProfile.DefaultSize,
                    ResourceNodeMarkerLod3D.Near);
                CaptureResourceMarkerLod(
                    "14-resource-marker-mid.png",
                    (context.MapNavigationProfile.NearMarkerMaximumSize +
                     context.MapNavigationProfile.MidMarkerMaximumSize) * .5f,
                    ResourceNodeMarkerLod3D.Mid);
                CaptureResourceMarkerLod(
                    "15-resource-marker-far.png",
                    (context.MapNavigationProfile.MidMarkerMaximumSize +
                     context.MapNavigationProfile.MaximumOrthographicSize) * .5f,
                    ResourceNodeMarkerLod3D.Far);
                RestoreDefaultCamera();
                context.World.RefreshResourceNodeMarkerLod(
                    context.Camera.orthographicSize);
            }

            private void CaptureResourceMarkerLod(
                string filename,
                float orthographicSize,
                ResourceNodeMarkerLod3D expectedLod)
            {
                context.Camera.orthographicSize = orthographicSize;
                ResourceNodeMarkerLod3D actual = context.MapNavigationProfile
                    .ResolveMarkerLod(orthographicSize);
                if (actual != expectedLod)
                {
                    throw new InvalidOperationException(
                        filename + " resolved unexpected marker LOD " +
                        actual + ".");
                }
                context.World.RefreshResourceNodeMarkerLod(orthographicSize);
                CapturePng(filename, null, "world-markers:" + actual);
            }

            private void CaptureZoomEvidence()
            {
                RestoreDefaultCamera();
                float originalSize = context.Camera.orthographicSize;
                ResourceNodeMarkerLod3D originalLod =
                    context.MapNavigationProfile.ResolveMarkerLod(originalSize);
                IReadOnlyList<ZoomFrameSpec> specs = BuildZoomFrameSpecs(
                    context.MapNavigationProfile);
                ValidateZoomFrameSpecs(context.MapNavigationProfile, specs);
                var records = new ZoomFrameRecord[specs.Count];
                var hashes = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    for (var index = 0; index < specs.Count; index++)
                    {
                        ZoomFrameSpec spec = specs[index];
                        context.Camera.orthographicSize = spec.OrthographicSize;
                        context.World.RefreshResourceNodeMarkerLod(
                            spec.OrthographicSize);
                        byte[] bytes = RenderCameraPng(
                            context.Camera,
                            CaptureWidth,
                            CaptureHeight);
                        ValidateNonBlackTerrainCoverage(
                            DecodePng(bytes),
                            CaptureWidth,
                            CaptureHeight,
                            "zoom frame " + index);
                        string relativePath = Path.Combine(
                            "zoom-frames",
                            "frame-" + index.ToString("000") + ".png");
                        File.WriteAllBytes(
                            Path.Combine(OutputRoot, relativePath),
                            bytes);
                        string hash = Sha256(bytes);
                        hashes.Add(hash);
                        records[index] = new ZoomFrameRecord
                        {
                            index = spec.Index,
                            scrollDeltaY = spec.ScrollDeltaY,
                            orthographicSize = spec.OrthographicSize,
                            resourceMarkerLod = spec.Lod.ToString(),
                            filename = relativePath,
                            width = CaptureWidth,
                            height = CaptureHeight,
                            sha256 = hash,
                        };
                    }
                    if (hashes.Count < 2)
                    {
                        throw new InvalidOperationException(
                            "Zoom evidence frame hashes do not vary.");
                    }
                }
                finally
                {
                    context.Camera.orthographicSize = originalSize;
                    context.World.RefreshResourceNodeMarkerLod(originalSize);
                }

                ResourceNodeMarkerLod3D restoredLod =
                    context.MapNavigationProfile.ResolveMarkerLod(
                        context.Camera.orthographicSize);
                ValidateZoomRestoration(
                    originalSize,
                    context.Camera.orthographicSize,
                    originalLod,
                    restoredLod);
                zoomEvidence = new ZoomEvidenceRecord
                {
                    frameDirectory = zoomFrameDirectory,
                    frameCount = records.Length,
                    width = CaptureWidth,
                    height = CaptureHeight,
                    originalOrthographicSize = originalSize,
                    originalResourceMarkerLod = originalLod.ToString(),
                    restoredOrthographicSize = context.Camera.orthographicSize,
                    restoredResourceMarkerLod = restoredLod.ToString(),
                    frameHashesVary = true,
                    frames = records,
                };
            }

            private void CaptureUiEvidence()
            {
                bool inventoryOpen = context.OperationsView.IsInventoryOpen;
                bool researchOpen = context.OperationsView.IsResearchOpen;
                bool ledgerOpen = context.OperationsView.IsLedgerOpen;
                RestoreDefaultCamera();
                context.World.RefreshResourceNodeMarkerLod(
                    context.Camera.orthographicSize);
                try
                {
                    context.OperationsView.SetInventoryOpen(false);
                    context.OperationsView.SetResearchOpen(false);
                    context.OperationsView.SetLedgerOpen(false);
                    Canvas.ForceUpdateCanvases();
                    using (PrepareUiCanvasesForCameraRender(
                               context.Camera,
                               context.Canvases))
                    {
                        CapturePng(
                            "20-ui-main-hud.png",
                            null,
                            "hud:main;inventory:closed;research:closed;ledger:closed");

                        context.OperationsView.SetResearchOpen(true);
                        context.OperationsView.FitResearchTree();
                        Canvas.ForceUpdateCanvases();
                        CapturePng(
                            "21-ui-research-tree.png",
                            null,
                            "hud:main;inventory:closed;research:open;ledger:closed");
                    }
                }
                finally
                {
                    context.OperationsView.SetInventoryOpen(inventoryOpen);
                    context.OperationsView.SetResearchOpen(researchOpen);
                    context.OperationsView.SetLedgerOpen(ledgerOpen);
                    Canvas.ForceUpdateCanvases();
                }
            }

            private void RestoreUiPanelState()
            {
                context.OperationsView.SetInventoryOpen(originalInventoryOpen);
                context.OperationsView.SetResearchOpen(originalResearchOpen);
                context.OperationsView.SetLedgerOpen(originalLedgerOpen);
                Canvas.ForceUpdateCanvases();
            }

            private void CaptureComparison(string filename)
            {
                RestorePresentation();
                byte[] formalBytes = RenderCameraPng(context.Camera, CaptureWidth, CaptureHeight);
                Color32[] formal = DecodePng(formalBytes);
                ValidateNonBlackTerrainCoverage(
                    formal,
                    CaptureWidth,
                    CaptureHeight,
                    filename + " formal half");

                context.Presenter.ClearPresentation();
                context.World.SetSurfaceFallbackVisible(true);
                byte[] fallbackBytes = RenderCameraPng(context.Camera, CaptureWidth, CaptureHeight);
                Color32[] fallback = DecodePng(fallbackBytes);
                ValidateNonBlackTerrainCoverage(
                    fallback,
                    CaptureWidth,
                    CaptureHeight,
                    filename + " fallback half");
                if (formal.SequenceEqual(fallback))
                    throw new InvalidOperationException("Formal and fallback comparison renders are identical.");

                var comparison = new Texture2D(
                    CaptureWidth,
                    CaptureHeight,
                    TextureFormat.RGBA32,
                    false,
                    false);
                try
                {
                    var pixels = new Color32[formal.Length];
                    for (int y = 0; y < CaptureHeight; y++)
                    {
                        int row = y * CaptureWidth;
                        for (int x = 0; x < CaptureWidth; x++)
                        {
                            pixels[row + x] = x < CaptureWidth / 2
                                ? formal[row + x]
                                : fallback[row + x];
                        }
                    }
                    comparison.SetPixels32(pixels);
                    comparison.Apply(false, false);
                    byte[] comparisonBytes = comparison.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(OutputRoot, filename), comparisonBytes);
                    captures.Add(NewCaptureRecord(
                        filename,
                        null,
                        comparisonBytes,
                        "world-comparison"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(comparison);
                    RestorePresentation();
                }
            }

            private void OnEndCameraRendering(
                ScriptableRenderContext renderContext,
                Camera renderedCamera)
            {
                if (renderedCamera != context.Camera)
                    return;
                TickVideoCapture();
            }

            private void TickVideoCapture()
            {
                try
                {
                    if (!EditorApplication.isPlaying)
                        throw new InvalidOperationException("Play Mode ended during terrain evidence capture.");
                    int frame = Time.frameCount;
                    if (frame == lastObservedFrame)
                        return;
                    if (frame != lastObservedFrame + 1)
                    {
                        throw new InvalidOperationException(
                            $"Skipped runtime frame during capture: {lastObservedFrame} -> {frame}.");
                    }
                    lastObservedFrame = frame;
                    VerifyCameraUnchanged();

                    if (profilerOnlyCaptureActive)
                    {
                        if (!ShouldFinalizeCapture(
                                frameNumbers.Count,
                                CurrentProfilerFrameCount()))
                        {
                            return;
                        }

                        SaveProfilerData();
                        if (acceptedDeviation)
                        {
                            ValidateAcceptedEvidenceIntegrity(
                                acceptedDecision,
                                ScenePath,
                                File.Exists(ProfilerDataPath),
                                frameNumbers,
                                VideoFrameCount);
                        }
                        WriteManifest(completedVideoPath);
                        CaptureSession completed = activeSession;
                        activeSession = null;
                        completed.Dispose(true);
                        Debug.Log(
                            "First terrain evidence capture completed: " +
                            OutputRoot);
                        return;
                    }

                    if (ShouldCaptureVideoFrame(frameNumbers.Count))
                    {
                        int outputIndex = frameNumbers.Count;
                        byte[] bytes = ReadRenderTexturePng(
                            videoTargetTexture,
                            CaptureWidth,
                            CaptureHeight);
                        string framePath = Path.Combine(
                            frameDirectory,
                            $"frame-{outputIndex:D3}.png");
                        File.WriteAllBytes(framePath, bytes);
                        frameNumbers.Add(frame);
                        frameHashes.Add(Sha256(bytes));
                        int metricSlot = WaterMetricSlot(outputIndex);
                        if (metricSlot >= 0)
                            waterMetricFrames[metricSlot] = DecodePng(bytes);
                    }
                    if (frameNumbers.Count < VideoFrameCount)
                        return;

                    ValidateConsecutiveFrames(frameNumbers, VideoFrameCount);
                    if (frameNumbers[VideoFrameCount - 1] - frameNumbers[0] != VideoFrameCount - 1)
                        throw new InvalidOperationException("Captured frame range is not exactly 299.");
                    if (frameHashes.Count < 2)
                        throw new InvalidOperationException("DeepWater capture frames are all identical.");

                    waterEvidence = AnalyzeWaterEvidence();

                    completedVideoPath = Path.Combine(
                        OutputRoot,
                        "11-deep-water-motion.mp4");
                    EncodeVideo(frameDirectory, completedVideoPath);
                    ProfilerDriver.enabled = false;
                    ProfilerDriver.ClearAllFrames();
                    ProfilerDriver.enabled = true;
                    profilerOnlyCaptureActive = true;
                    Debug.Log(
                        "First terrain evidence Profiler-only capture " +
                        "started after video encoding.");
                }
                catch (Exception exception)
                {
                    CaptureSession failed = activeSession;
                    activeSession = null;
                    failed?.Dispose(false);
                    Debug.LogException(exception);
                }
            }

            private void OnPlayModeStateChanged(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.ExitingPlayMode)
                    return;

                CaptureSession failed = activeSession;
                activeSession = null;
                failed?.Dispose(false);
            }

            private void RegisterVideoCallbacks()
            {
                if (videoCallbacksRegistered)
                    return;
                videoCallbacksRegistered = true;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                if (!playModeCallbackRegistered)
                {
                    playModeCallbackRegistered = true;
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                }
            }

            private void UnregisterCallbacks()
            {
                if (videoCallbacksRegistered)
                {
                    videoCallbacksRegistered = false;
                    RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                }
                if (diagnosticCallbackRegistered)
                {
                    diagnosticCallbackRegistered = false;
                    EditorApplication.update -= TickBuildGridDiagnostic;
                }
                if (playModeCallbackRegistered)
                {
                    playModeCallbackRegistered = false;
                    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                }
            }

            private void RestoreDefaultCamera()
            {
                context.Rig.position = originalRigPosition;
                context.Rig.rotation = originalRigRotation;
                context.Camera.transform.localPosition = originalCameraLocalPosition;
                context.Camera.transform.localRotation = originalCameraLocalRotation;
                context.Camera.orthographicSize = originalOrthographicSize;
            }

            private void FocusMapOverview()
            {
                context.Rig.position = new Vector3(
                    -.5f,
                    originalRigPosition.y,
                    -.5f);
                context.Rig.rotation = originalRigRotation;
                context.Camera.orthographicSize = OverviewOrthographicSize;
            }

            private void FocusSite(CaptureSite site, float orthographicSize)
            {
                PlanarCoordinateMapper3D mapper = context.World.Coordinates;
                if (!mapper.TryCellToWorld(
                        site.PrimaryX,
                        site.PrimaryY,
                        0f,
                        out Vector3 primary) ||
                    !mapper.TryCellToWorld(
                        site.SecondaryX,
                        site.SecondaryY,
                        0f,
                        out Vector3 secondary))
                {
                    throw new InvalidOperationException("Capture site is outside the world mapper.");
                }

                Vector3 midpoint = (primary + secondary) * .5f;
                context.Rig.position = new Vector3(
                    midpoint.x,
                    originalRigPosition.y,
                    midpoint.z);
                context.Rig.rotation = originalRigRotation;
                context.Camera.orthographicSize = orthographicSize;
            }

            private void RestorePresentation()
            {
                context.Presenter.enabled = originalPresenterEnabled;
                if (!context.Presenter.IsPresented && originalPresenterEnabled)
                {
                    if (!context.Presenter.TryPresent(context.World))
                    {
                        throw new InvalidOperationException(
                            "Failed to restore formal terrain after evidence capture.");
                    }
                }
            }

            private void VerifyCameraUnchanged()
            {
                if (context.Camera.worldToCameraMatrix != firstWorldToCamera ||
                    context.Camera.projectionMatrix != firstProjection)
                {
                    throw new InvalidOperationException("Evidence camera moved during DeepWater capture.");
                }
            }

            private CaptureRecord NewCaptureRecord(
                string filename,
                CaptureSite? site,
                byte[] bytes,
                string uiPanelState)
            {
                CaptureSite value = site.GetValueOrDefault();
                return new CaptureRecord
                {
                    filename = filename,
                    siteName = site.HasValue ? value.Name : string.Empty,
                    primaryX = site.HasValue ? value.PrimaryX : -1,
                    primaryY = site.HasValue ? value.PrimaryY : -1,
                    primaryLayer = site.HasValue ? value.PrimaryLayer.ToString() : string.Empty,
                    secondaryX = site.HasValue ? value.SecondaryX : -1,
                    secondaryY = site.HasValue ? value.SecondaryY : -1,
                    secondaryLayer = site.HasValue ? value.SecondaryLayer.ToString() : string.Empty,
                    rigPosition = context.Rig.position,
                    rigRotation = context.Rig.rotation,
                    cameraLocalPosition = context.Camera.transform.localPosition,
                    cameraLocalRotation = context.Camera.transform.localRotation,
                    orthographicSize = context.Camera.orthographicSize,
                    resourceMarkerLod = context.MapNavigationProfile
                        .ResolveMarkerLod(
                            context.Camera.orthographicSize)
                        .ToString(),
                    uiPanelState = uiPanelState ?? string.Empty,
                    width = CaptureWidth,
                    height = CaptureHeight,
                    sha256 = Sha256(bytes),
                };
            }

            private void WriteManifest(string videoPath)
            {
                RenderPipelineAsset pipeline = GraphicsSettings.renderPipelineAsset;
                string pipelinePath = AssetDatabase.GetAssetPath(pipeline);
                string materialPath = AssetDatabase.GetAssetPath(context.Presenter.Profile.Material);
                var manifest = new EvidenceManifest
                {
                    seed = GrayboxSceneBootstrap.WorldSeedValue,
                    scene = ScenePath,
                    pipelineAssetPath = pipelinePath,
                    pipelineGuid = AssetDatabase.AssetPathToGUID(pipelinePath),
                    materialAssetPath = materialPath,
                    materialGuid = AssetDatabase.AssetPathToGUID(materialPath),
                    width = CaptureWidth,
                    height = CaptureHeight,
                    captures = captures.OrderBy(capture => capture.filename).ToArray(),
                    zoom = zoomEvidence ?? throw new InvalidOperationException(
                        "IDEA-0018 zoom evidence is incomplete."),
                    video = new VideoRecord
                    {
                        filename = Path.GetFileName(videoPath),
                        frameDirectory = frameDirectory,
                        framesPerSecond = VideoFramesPerSecond,
                        frameCount = frameNumbers.Count,
                        firstFrameNumber = frameNumbers[0],
                        lastFrameNumber = frameNumbers[frameNumbers.Count - 1],
                        frameNumbers = frameNumbers.ToArray(),
                        cameraMatrix = MatrixToString(firstWorldToCamera) + "|" +
                                       MatrixToString(firstProjection),
                        frameHashesVary = frameHashes.Count > 1,
                    },
                    water = waterEvidence,
                    technicalVisualGatePassed = !acceptedDeviation,
                    userVisualDecision = acceptedDeviation
                        ? acceptedWaterEvidence.UserVisualDecision
                        : string.Empty,
                    visualReviewResult = acceptedDeviation
                        ? "accepted known deviation"
                        : "technical visual gate passed",
                    failedVisualThresholds = acceptedDeviation
                        ? acceptedWaterEvidence.FailedThresholds
                        : Array.Empty<string>(),
                    unresolvedFirstArtPassLimitations = acceptedDeviation
                        ? AcceptedLimitations(!acceptedWaterEvidence.MotionThresholdPassed)
                        : Array.Empty<string>(),
                    approvalScope = acceptedDeviation
                        ? AcceptedApprovalScope()
                        : string.Empty,
                };
                string json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(Path.Combine(OutputRoot, "manifest.json"), json);
                if (acceptedDeviation)
                {
                    var summary = new AcceptedDeviationSummary
                    {
                        technicalVisualGatePassed = false,
                        userVisualDecision = acceptedWaterEvidence.UserVisualDecision,
                        visualReviewResult = "accepted known deviation",
                        failedVisualThresholds = acceptedWaterEvidence.FailedThresholds,
                        unresolvedFirstArtPassLimitations =
                            AcceptedLimitations(!acceptedWaterEvidence.MotionThresholdPassed),
                        approvalScope = AcceptedApprovalScope(),
                    };
                    File.WriteAllText(
                        Path.Combine(OutputRoot, "accepted-deviation-summary.json"),
                        JsonUtility.ToJson(summary, true));
                }
            }

            private static string[] AcceptedLimitations(bool includesMotion)
            {
                var limitations = new List<string>
                {
                    "DeepWater does not meet the blue-black color-ratio target.",
                    "DeepWater can read as a gray-black pit and does not meet the readability target.",
                    "Several first-art-pass terrain classes remain weakly differentiated.",
                };
                if (includesMotion)
                    limitations.Add("DeepWater motion is below the perceptibility target.");
                return limitations.ToArray();
            }

            private static string AcceptedApprovalScope()
            {
                return "User accepted only the current first-version visual deviation; no source PNG, gameplay, collision, or hidden tuning was changed to obtain approval.";
            }

            private static void SaveProfilerData()
            {
                ProfilerDriver.enabled = false;
                int frameCount = CurrentProfilerFrameCount();
                int firstFrame = ProfilerDriver.firstFrameIndex;
                int lastFrame = ProfilerDriver.lastFrameIndex;
                if (frameCount != VideoFrameCount)
                {
                    throw new InvalidOperationException(
                        $"Real GUI Profiler must contain exactly 300 frames; found {frameCount} " +
                        $"({firstFrame}..{lastFrame}).");
                }
                ProfilerDriver.SaveProfile(ProfilerDataPath);
                if (!File.Exists(ProfilerDataPath))
                    throw new InvalidOperationException("Real GUI Profiler data was not saved.");
            }

            private static int CurrentProfilerFrameCount()
            {
                int firstFrame = ProfilerDriver.firstFrameIndex;
                int lastFrame = ProfilerDriver.lastFrameIndex;
                return lastFrame >= firstFrame
                    ? lastFrame - firstFrame + 1
                    : 0;
            }

            private static void PrepareOutputDirectory()
            {
                if (Directory.Exists(OutputRoot))
                    Directory.Delete(OutputRoot, true);
                Directory.CreateDirectory(OutputRoot);
                Directory.CreateDirectory(Path.Combine(OutputRoot, "deep-water-frames"));
                Directory.CreateDirectory(Path.Combine(OutputRoot, "zoom-frames"));
            }

            private static int WaterMetricSlot(int capturedFrameOffset)
            {
                if (capturedFrameOffset == 0)
                    return 0;
                if (capturedFrameOffset == 150)
                    return 1;
                return capturedFrameOffset == VideoFrameCount - 1 ? 2 : -1;
            }

            private WaterEvidenceRecord AnalyzeWaterEvidence()
            {
                if (waterRoiIndices == null || waterInsetVertices == null ||
                    waterMetricFrames.Any(frame => frame == null))
                {
                    throw new InvalidOperationException(
                        "DeepWater t0/t5/t10 ROI evidence is incomplete.");
                }

                string[] labels = { "t0", "t5", "t10" };
                int[] offsets = { 0, 150, VideoFrameCount - 1 };
                var colorRecords = new WaterColorRecord[3];
                var colorMetrics = new WaterColorMetrics[3];
                for (int index = 0; index < waterMetricFrames.Length; index++)
                {
                    WaterColorMetrics metrics = CalculateWaterColorMetrics(
                        waterMetricFrames[index],
                        waterRoiIndices);
                    colorMetrics[index] = metrics;
                    if (!acceptedDeviation)
                        ValidateWaterColorMetrics(metrics);
                    colorRecords[index] = new WaterColorRecord
                    {
                        label = labels[index],
                        capturedFrameOffset = offsets[index],
                        meanR = metrics.MeanR,
                        meanG = metrics.MeanG,
                        meanB = metrics.MeanB,
                        meanLuminance = metrics.MeanLuminance,
                    };
                }

                WaterFramePair[] pairs = ExactWaterFramePairs();
                var motionMetrics = new WaterMotionMetrics[pairs.Length];
                var motionRecords = new WaterMotionRecord[pairs.Length];
                for (int index = 0; index < pairs.Length; index++)
                {
                    WaterFramePair pair = pairs[index];
                    WaterMotionMetrics metrics = CalculateWaterMotionMetrics(
                        waterMetricFrames[pair.First],
                        waterMetricFrames[pair.Second],
                        waterRoiIndices);
                    motionMetrics[index] = metrics;
                    motionRecords[index] = new WaterMotionRecord
                    {
                        pair = labels[pair.First] + "-" + labels[pair.Second],
                        firstCapturedFrameOffset = offsets[pair.First],
                        secondCapturedFrameOffset = offsets[pair.Second],
                        meanDelta = metrics.MeanDelta,
                        nearestRankP95Delta = metrics.P95Delta,
                    };
                }
                if (acceptedDeviation)
                {
                    acceptedWaterEvidence = EvaluateAcceptedWaterEvidence(
                        acceptedDecision,
                        colorMetrics,
                        motionMetrics);
                }
                else
                {
                    ValidateWaterMotionMetrics(motionMetrics);
                }

                return new WaterEvidenceRecord
                {
                    cellX = waterCellX,
                    cellY = waterCellY,
                    insetVertices = waterInsetVertices.ToArray(),
                    roiPixelCount = waterRoiIndices.Length,
                    frames = colorRecords,
                    motionPairs = motionRecords,
                    colorThresholdsPassed = !acceptedDeviation,
                    motionThresholdPassed = !acceptedDeviation ||
                                            acceptedWaterEvidence.MotionThresholdPassed,
                };
            }
        }

        private static byte[] RenderCameraPng(Camera camera, int width, int height)
        {
            var renderTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            var readback = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);
                return readback.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(readback);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static byte[] ReadRenderTexturePng(
            RenderTexture renderTexture,
            int width,
            int height)
        {
            if (renderTexture == null || !renderTexture.IsCreated() ||
                renderTexture.width != width || renderTexture.height != height)
            {
                throw new InvalidOperationException(
                    "The real camera render target is not available at 1920x1080.");
            }

            var readback = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                false);
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);
                return readback.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        private static Color32[] DecodePng(byte[] bytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!texture.LoadImage(bytes, false) ||
                    texture.width != CaptureWidth || texture.height != CaptureHeight)
                {
                    throw new InvalidOperationException("Captured PNG could not be decoded at 1920x1080.");
                }
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void EncodeVideo(string frameDirectory, string outputPath)
        {
            string ffmpeg = ResolveFfmpeg();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-framerate");
            startInfo.ArgumentList.Add(VideoFramesPerSecond.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-start_number");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(Path.Combine(frameDirectory, "frame-%03d.png"));
            startInfo.ArgumentList.Add("-frames:v");
            startInfo.ArgumentList.Add(VideoFrameCount.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-c:v");
            startInfo.ArgumentList.Add("libx264");
            startInfo.ArgumentList.Add("-pix_fmt");
            startInfo.ArgumentList.Add("yuv420p");
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add("+faststart");
            startInfo.ArgumentList.Add(outputPath);

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new InvalidOperationException("Failed to start ffmpeg.");
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0 || !File.Exists(outputPath))
                {
                    throw new InvalidOperationException(
                        $"ffmpeg failed with exit {process.ExitCode}: {standardError}");
                }
            }
        }

        private static string ResolveFfmpeg()
        {
            var candidates = new List<string>();
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(directory))
                    candidates.Add(Path.Combine(directory, "ffmpeg"));
            }
            candidates.Add("/opt/homebrew/bin/ffmpeg");
            candidates.Add("/usr/local/bin/ffmpeg");
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local/bin/ffmpeg"));
            string result = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(result))
                throw new FileNotFoundException("ffmpeg is required for evidence encoding.");
            return result;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static string MatrixToString(Matrix4x4 matrix)
        {
            var values = new string[16];
            for (int index = 0; index < values.Length; index++)
                values[index] = matrix[index].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(",", values);
        }
    }
}
