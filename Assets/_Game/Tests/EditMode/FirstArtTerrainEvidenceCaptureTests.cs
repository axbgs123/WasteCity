using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.Editor;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainEvidenceCaptureTests
    {
        [Test]
        public void ConsecutiveFrameValidator_RejectsGapDuplicateAndWrongCount()
        {
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateConsecutiveFrames(
                    new[] { 100, 102, 103 },
                    3),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateConsecutiveFrames(
                    new[] { 100, 101, 101 },
                    3),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateConsecutiveFrames(
                    new[] { 100, 101, 102 },
                    300),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateConsecutiveFrames(
                    new[] { 100, 101, 102 },
                    3),
                Throws.Nothing);
        }

        [Test]
        public void CaptureCompletion_WaitsForProfilerWithoutAddingVideoFrames()
        {
            Assert.That(
                FirstArtTerrainEvidenceCapture.ShouldCaptureVideoFrame(299),
                Is.True);
            Assert.That(
                FirstArtTerrainEvidenceCapture.ShouldCaptureVideoFrame(300),
                Is.False);
            Assert.That(
                FirstArtTerrainEvidenceCapture.ShouldFinalizeCapture(299, 300),
                Is.False);
            Assert.That(
                FirstArtTerrainEvidenceCapture.ShouldFinalizeCapture(300, 298),
                Is.False);
            Assert.That(
                FirstArtTerrainEvidenceCapture.ShouldFinalizeCapture(300, 300),
                Is.True);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ShouldFinalizeCapture(
                    300,
                    301),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TerrainLuminanceGate_RequiresLitCoverageInsideTheCameraCenter()
        {
            const int width = 8;
            const int height = 8;
            Color32[] blackCenter = Enumerable.Repeat(
                new Color32(180, 180, 180, 255),
                width * height).ToArray();
            for (var y = 2; y < 6; y++)
            for (var x = 2; x < 6; x++)
                blackCenter[y * width + x] = new Color32(0, 0, 0, 255);

            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateNonBlackTerrainCoverage(
                        blackCenter,
                        width,
                        height,
                        "synthetic black terrain"),
                Throws.TypeOf<InvalidOperationException>());

            Color32[] litCenter = Enumerable.Repeat(
                new Color32(0, 0, 0, 255),
                width * height).ToArray();
            for (var y = 2; y < 6; y++)
            for (var x = 2; x < 6; x++)
                litCenter[y * width + x] =
                    new Color32(90, 70, 45, 255);

            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateNonBlackTerrainCoverage(
                        litCenter,
                        width,
                        height,
                        "synthetic lit terrain"),
                Throws.Nothing);
        }

        [Test]
        public void UiLuminanceGate_AcceptsDarkPanelsWithReadableContrast()
        {
            const int width = 8;
            const int height = 8;
            Color32[] black = Enumerable.Repeat(
                new Color32(0, 0, 0, 255),
                width * height).ToArray();
            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateUiEvidenceCoverage(
                        black,
                        width,
                        height,
                        "synthetic black UI"),
                Throws.TypeOf<InvalidOperationException>());

            Color32[] darkPanel = Enumerable.Repeat(
                new Color32(18, 18, 20, 255),
                width * height).ToArray();
            darkPanel[width * 3 + 3] = new Color32(220, 220, 220, 255);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateUiEvidenceCoverage(
                        darkPanel,
                        width,
                        height,
                        "synthetic readable UI"),
                Throws.Nothing);
        }

        [Test]
        public void Seed8128BoundarySearch_FindsEveryNamedCaptureSite()
        {
            var model = new WorldMapModel(32, 24, new WorldSeed(8128));
            IReadOnlyList<FirstArtTerrainEvidenceCapture.CaptureSite> sites =
                FirstArtTerrainEvidenceCapture.FindRequiredCaptureSites(model);

            string[] expectedNames =
            {
                "wasteland-rocky",
                "wasteland-wetland",
                "wasteland-crystal",
                "three-way-junction",
                "ruins-edge",
                "deep-water-shore",
                "cliff-edge",
            };
            Assert.That(sites.Select(site => site.Name), Is.EquivalentTo(expectedNames));
            Assert.That(sites.Select(site => site.Name), Is.Unique);

            foreach (FirstArtTerrainEvidenceCapture.CaptureSite site in sites)
            {
                Assert.That(site.PrimaryX, Is.InRange(0, model.Width - 1), site.Name);
                Assert.That(site.PrimaryY, Is.InRange(0, model.Height - 1), site.Name);
                Assert.That(site.SecondaryX, Is.InRange(0, model.Width - 1), site.Name);
                Assert.That(site.SecondaryY, Is.InRange(0, model.Height - 1), site.Name);
                Assert.That(
                    FirstArtTerrainCatalog3D.LayerOf(
                        model.Get(site.PrimaryX, site.PrimaryY)),
                    Is.EqualTo(site.PrimaryLayer),
                    site.Name);
                Assert.That(
                    FirstArtTerrainCatalog3D.LayerOf(
                        model.Get(site.SecondaryX, site.SecondaryY)),
                    Is.EqualTo(site.SecondaryLayer),
                    site.Name);
            }

            FirstArtTerrainEvidenceCapture.CaptureSite junction =
                sites.Single(site => site.Name == "three-way-junction");
            var layers = new HashSet<FirstArtTerrainLayer3D>();
            for (int x = junction.PrimaryX; x <= junction.PrimaryX + 1; x++)
            for (int y = junction.PrimaryY; y <= junction.PrimaryY + 1; y++)
                layers.Add(FirstArtTerrainCatalog3D.LayerOf(model.Get(x, y)));
            Assert.That(layers.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void PayloadCalculator_ReportsApprovedFourArrayPayload()
        {
            const string root =
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/";
            string[] paths =
            {
                root + "TA_Terrain_BaseColor.asset",
                root + "TA_Terrain_Normal.asset",
                root + "TA_Terrain_Mask.asset",
                root + "TA_Terrain_Height.asset",
            };
            long sum = 0L;
            foreach (string path in paths)
            {
                Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
                Assert.That(array, Is.Not.Null, path);
                sum += FirstArtTerrainEvidenceCapture.CalculateCompressedPayloadBytes(array);
            }

            Assert.That(sum, Is.EqualTo(127227779L));
            Assert.That(sum, Is.LessThanOrEqualTo(128L * 1024L * 1024L));
        }

        [Test]
        public void CaptureAll_RefusesToRunOutsidePlayMode()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.CaptureAll(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void BuildGridDiagnosticGate_RequiresRealOpenAndRestoredClosedStates()
        {
            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateBuildGridDiagnosticState(
                        GrayboxBuildingInteractionState.CatalogOpen,
                        true,
                        true),
                Throws.Nothing);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateBuildGridDiagnosticState(
                        GrayboxBuildingInteractionState.Inactive,
                        false,
                        false),
                Throws.Nothing);

            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateBuildGridDiagnosticState(
                        GrayboxBuildingInteractionState.Inactive,
                        true,
                        true),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateBuildGridDiagnosticState(
                        GrayboxBuildingInteractionState.CatalogOpen,
                        false,
                        true),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture
                    .ValidateBuildGridDiagnosticState(
                        GrayboxBuildingInteractionState.CatalogOpen,
                        true,
                        false),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void WaterRoi_InsetIsExactAndIncludesBoundaryPixelCenters()
        {
            var corners = new[]
            {
                new Vector2(1f, 1f),
                new Vector2(9f, 1f),
                new Vector2(9f, 9f),
                new Vector2(1f, 9f),
            };
            Vector2[] inset = FirstArtTerrainEvidenceCapture.InsetWaterCell(
                corners,
                12,
                12);

            Assert.That(inset[0].x, Is.EqualTo(2.4f).Within(1e-6f));
            Assert.That(inset[0].y, Is.EqualTo(2.4f).Within(1e-6f));
            Assert.That(inset[1].x, Is.EqualTo(7.6f).Within(1e-6f));
            Assert.That(inset[1].y, Is.EqualTo(2.4f).Within(1e-6f));
            Assert.That(inset[2].x, Is.EqualTo(7.6f).Within(1e-6f));
            Assert.That(inset[2].y, Is.EqualTo(7.6f).Within(1e-6f));
            Assert.That(inset[3].x, Is.EqualTo(2.4f).Within(1e-6f));
            Assert.That(inset[3].y, Is.EqualTo(7.6f).Within(1e-6f));

            int[] indices = FirstArtTerrainEvidenceCapture.BuildWaterRoiIndices(
                new[]
                {
                    new Vector2(.5f, .5f),
                    new Vector2(8.5f, .5f),
                    new Vector2(8.5f, 8.5f),
                    new Vector2(.5f, 8.5f),
                },
                10,
                10);
            Assert.That(indices, Does.Contain(0));
            Assert.That(indices, Does.Contain(8));
            Assert.That(indices, Does.Contain(80));
            Assert.That(indices, Does.Contain(88));
            Assert.That(indices.Length, Is.EqualTo(81));
        }

        [Test]
        public void WaterRoi_RejectsInvalidClippedAndUnder64PixelProjection()
        {
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.InsetWaterCell(
                    new[]
                    {
                        new Vector2(-10f, 1f),
                        new Vector2(9f, 1f),
                        new Vector2(9f, 9f),
                        new Vector2(1f, 9f),
                    },
                    12,
                    12),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.InsetWaterCell(
                    new[]
                    {
                        new Vector2(float.NaN, 1f),
                        new Vector2(9f, 1f),
                        new Vector2(9f, 9f),
                        new Vector2(1f, 9f),
                    },
                    12,
                    12),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.BuildWaterRoiIndices(
                    new[]
                    {
                        new Vector2(1f, 1f),
                        new Vector2(6f, 1f),
                        new Vector2(6f, 6f),
                        new Vector2(1f, 6f),
                    },
                    12,
                    12),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("at least 64"));
        }

        [Test]
        public void WaterRoi_ProjectsLogicalCellCornersThroughTheFrozenCamera()
        {
            var cameraObject = new GameObject("water-roi-camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var mapper = new WasteCity.Graybox3D.PlanarCoordinateMapper3D(32, 24);
            try
            {
                Vector2[] inset =
                    FirstArtTerrainEvidenceCapture.ProjectInsetWaterCell(
                        camera,
                        mapper,
                        16,
                        12,
                        1000,
                        1000);
                Vector2 centroid =
                    (inset[0] + inset[1] + inset[2] + inset[3]) / 4f;
                Assert.That(centroid.x, Is.EqualTo(500f).Within(.001f));
                Assert.That(centroid.y, Is.EqualTo(500f).Within(.001f));
                Assert.That(
                    Vector2.Distance(inset[0], inset[1]),
                    Is.EqualTo(65f).Within(.001f));
                Assert.That(
                    FirstArtTerrainEvidenceCapture.BuildWaterRoiIndices(
                        inset,
                        1000,
                        1000).Length,
                    Is.GreaterThanOrEqualTo(64));

                camera.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                Assert.That(
                    () => FirstArtTerrainEvidenceCapture.ProjectInsetWaterCell(
                        camera,
                        mapper,
                        16,
                        12,
                        1000,
                        1000),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void WaterMetrics_UseUnsignedEightBitSrgbRgbAndRec709Luminance()
        {
            var pixels = new[]
            {
                new Color32(255, 0, 0, 1),
                new Color32(0, 255, 0, 99),
                new Color32(0, 0, 255, 255),
                new Color32(255, 255, 255, 0),
            };
            FirstArtTerrainEvidenceCapture.WaterColorMetrics metrics =
                FirstArtTerrainEvidenceCapture.CalculateWaterColorMetrics(
                    pixels,
                    new[] { 0, 1, 2, 3 });

            Assert.That(metrics.MeanR, Is.EqualTo(.5d).Within(1e-12));
            Assert.That(metrics.MeanG, Is.EqualTo(.5d).Within(1e-12));
            Assert.That(metrics.MeanB, Is.EqualTo(.5d).Within(1e-12));
            Assert.That(metrics.MeanLuminance, Is.EqualTo(.5d).Within(1e-12));
        }

        [Test]
        public void WaterMotion_UsesAllThreeExactPairsAndNearestRankP95()
        {
            FirstArtTerrainEvidenceCapture.WaterFramePair[] pairs =
                FirstArtTerrainEvidenceCapture.ExactWaterFramePairs();
            Assert.That(
                pairs.Select(pair => new Vector2Int(pair.First, pair.Second)),
                Is.EqualTo(new[]
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 2),
                    new Vector2Int(0, 2),
                }));

            var first = Enumerable.Repeat(new Color32(0, 0, 0, 255), 20).ToArray();
            var oneHigh = first.ToArray();
            oneHigh[19] = new Color32(255, 255, 255, 0);
            FirstArtTerrainEvidenceCapture.WaterMotionMetrics one =
                FirstArtTerrainEvidenceCapture.CalculateWaterMotionMetrics(
                    first,
                    oneHigh,
                    Enumerable.Range(0, 20).ToArray());
            Assert.That(one.MeanDelta, Is.EqualTo(.05d).Within(1e-12));
            Assert.That(one.P95Delta, Is.Zero);

            var twoHigh = oneHigh.ToArray();
            twoHigh[18] = new Color32(255, 255, 255, 255);
            FirstArtTerrainEvidenceCapture.WaterMotionMetrics two =
                FirstArtTerrainEvidenceCapture.CalculateWaterMotionMetrics(
                    first,
                    twoHigh,
                    Enumerable.Range(0, 20).ToArray());
            Assert.That(two.MeanDelta, Is.EqualTo(.1d).Within(1e-12));
            Assert.That(two.P95Delta, Is.EqualTo(1d));
        }

        [Test]
        public void WaterColorGate_AcceptsEveryInclusiveBoundaryAndRejectsBelow()
        {
            double minimum = 15d / 255d;
            double maximum = 90d / 255d;
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterColorMetrics(
                    new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                        .08d,
                        .10d / 1.10d,
                        .10d,
                        minimum)),
                Throws.Nothing);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterColorMetrics(
                    new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                        .08d,
                        .10d / 1.10d,
                        .10d,
                        maximum)),
                Throws.Nothing);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterColorMetrics(
                    new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                        .080001d,
                        .09d,
                        .10d,
                        minimum)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterColorMetrics(
                    new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                        .08d,
                        .09091d,
                        .10d,
                        minimum)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterColorMetrics(
                    new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                        .08d,
                        .09d,
                        .10d,
                        minimum - 1e-9)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterColorMetrics(
                    new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                        .08d,
                        .09d,
                        .10d,
                        maximum + 1e-9)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void WaterMotionGate_RequiresBothInclusiveThresholdsInOnePair()
        {
            double mean = 1d / 255d;
            double p95 = 3d / 255d;
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterMotionMetrics(
                    new[]
                    {
                        new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(mean, p95),
                        new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(0d, 0d),
                        new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(0d, 0d),
                    }),
                Throws.Nothing);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateWaterMotionMetrics(
                    new[]
                    {
                        new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(mean - 1e-9, p95),
                        new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(mean, p95 - 1e-9),
                        new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(0d, 0d),
                    }),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void AcceptedDeviation_RequiresExactDecisionAndOnlyWaivesBlueBlackRatios()
        {
            var disclosedColor = new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                .137634d,
                .168758d,
                .163840d,
                .161786d);
            var passingMotion = new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(
                1d / 255d,
                3d / 255d);

            Assert.That(
                () => FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    null,
                    new[] { disclosedColor, disclosedColor, disclosedColor },
                    new[] { passingMotion, passingMotion, passingMotion }),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    "accepted-current-first-version",
                    new[] { disclosedColor, disclosedColor, disclosedColor },
                    new[] { passingMotion, passingMotion, passingMotion }),
                Throws.TypeOf<InvalidOperationException>());

            FirstArtTerrainEvidenceCapture.AcceptedWaterEvidence result =
                FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    "user-accepted-known-visual-deviation",
                    new[] { disclosedColor, disclosedColor, disclosedColor },
                    new[] { passingMotion, passingMotion, passingMotion });

            Assert.That(result.TechnicalVisualGatePassed, Is.False);
            Assert.That(result.UserVisualDecision, Is.EqualTo("accepted-current-first-version"));
            Assert.That(result.FailedThresholds, Has.Length.EqualTo(3));
            StringAssert.Contains("R=0.137634", result.FailedThresholds[0]);
            StringAssert.Contains("G=0.168758", result.FailedThresholds[0]);
            StringAssert.Contains("B=0.163840", result.FailedThresholds[0]);
            StringAssert.Contains("luminance=0.161786", result.FailedThresholds[0]);
        }

        [Test]
        public void AcceptedDeviation_CannotWaiveLuminanceOrMotion()
        {
            const string decision = "user-accepted-known-visual-deviation";
            var invalidLuminance = new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                .04d,
                .05d,
                .07d,
                14d / 255d);
            var disclosedColor = new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                .137634d,
                .168758d,
                .163840d,
                .161786d);
            var passingMotion = new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(
                1d / 255d,
                3d / 255d);
            var failingMotion = new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(0d, 0d);

            Assert.That(
                () => FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    decision,
                    new[] { invalidLuminance, invalidLuminance, invalidLuminance },
                    new[] { passingMotion, passingMotion, passingMotion }),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("luminance"));
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    decision,
                    new[] { disclosedColor, disclosedColor, disclosedColor },
                    new[] { failingMotion, failingMotion, failingMotion }),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("motion"));
        }

        [Test]
        public void CombinedAcceptedDeviation_RecordsExactMotionFailureAndOldTokenStillRejects()
        {
            var disclosedColor = new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                .137634d,
                .168758d,
                .163840d,
                .161786d);
            var failingMotion = new[]
            {
                new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(.001d, .002d),
                new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(.004d, .003d),
                new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(0d, .012d),
            };

            Assert.That(
                () => FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    "user-accepted-known-visual-deviation",
                    new[] { disclosedColor, disclosedColor, disclosedColor },
                    failingMotion),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("motion"));

            FirstArtTerrainEvidenceCapture.AcceptedWaterEvidence result =
                FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    "user-accepted-known-visual-and-motion-deviation",
                    new[] { disclosedColor, disclosedColor, disclosedColor },
                    failingMotion);

            Assert.That(result.TechnicalVisualGatePassed, Is.False);
            Assert.That(
                result.UserVisualDecision,
                Is.EqualTo("accepted-current-first-version-including-motion"));
            Assert.That(result.MotionThresholdPassed, Is.False);
            Assert.That(result.MotionFailures, Has.Length.EqualTo(3));
            StringAssert.Contains("t0-t5", result.MotionFailures[0]);
            StringAssert.Contains("meanDelta=0.001000", result.MotionFailures[0]);
            StringAssert.Contains("nearestRankP95Delta=0.002000", result.MotionFailures[0]);
            StringAssert.Contains("missed mean=true", result.MotionFailures[0]);
            StringAssert.Contains("missed P95=true", result.MotionFailures[0]);
            StringAssert.Contains("t5-t10", result.MotionFailures[1]);
            StringAssert.Contains("missed mean=false", result.MotionFailures[1]);
            StringAssert.Contains("missed P95=true", result.MotionFailures[1]);
            StringAssert.Contains("t0-t10", result.MotionFailures[2]);
            StringAssert.Contains("missed mean=true", result.MotionFailures[2]);
            StringAssert.Contains("missed P95=false", result.MotionFailures[2]);
            Assert.That(result.FailedThresholds, Has.Length.EqualTo(6));
        }

        [Test]
        public void CombinedAcceptedDeviation_RequiresAnActualMotionFailure()
        {
            var disclosedColor = new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                .137634d,
                .168758d,
                .163840d,
                .161786d);
            var passingMotion = new FirstArtTerrainEvidenceCapture.WaterMotionMetrics(
                1d / 255d,
                3d / 255d);

            Assert.That(
                () => FirstArtTerrainEvidenceCapture.EvaluateAcceptedWaterEvidence(
                    "user-accepted-known-visual-and-motion-deviation",
                    new[] { disclosedColor, disclosedColor, disclosedColor },
                    new[] { passingMotion, passingMotion, passingMotion }),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("combined"));
        }

        [Test]
        public void AcceptedDeviation_DoesNotChangeStrictColorOrEvidenceIntegrityGates()
        {
            string previous = Environment.GetEnvironmentVariable(
                "WASTECITY_FIRST_ART_VISUAL_DECISION");
            try
            {
                Environment.SetEnvironmentVariable(
                    "WASTECITY_FIRST_ART_VISUAL_DECISION",
                    "user-accepted-known-visual-deviation");
                Assert.That(
                    () => FirstArtTerrainEvidenceCapture.ValidateWaterColorMetrics(
                        new FirstArtTerrainEvidenceCapture.WaterColorMetrics(
                            .137634d,
                            .168758d,
                            .163840d,
                            .161786d)),
                    Throws.TypeOf<InvalidOperationException>());
                Assert.That(
                    () => FirstArtTerrainEvidenceCapture.ValidateConsecutiveFrames(
                        new[] { 10, 12, 13 },
                        3),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "WASTECITY_FIRST_ART_VISUAL_DECISION",
                    previous);
            }
        }

        [TestCase("user-accepted-known-visual-deviation")]
        [TestCase("user-accepted-known-visual-and-motion-deviation")]
        public void AcceptedDeviationTokens_CannotBypassEvidenceIntegrity(
            string decision)
        {
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateAcceptedEvidenceIntegrity(
                    decision,
                    "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                    true,
                    new[] { 10, 12, 13 },
                    3),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("consecutive"));
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateAcceptedEvidenceIntegrity(
                    decision,
                    "Assets/_Game/Scenes/UnapprovedEvidenceScene.unity",
                    true,
                    new[] { 10, 11, 12 },
                    3),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("scene"));
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateAcceptedEvidenceIntegrity(
                    decision,
                    "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                    false,
                    new[] { 10, 11, 12 },
                    3),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("Profiler"));
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.ValidateAcceptedEvidenceIntegrity(
                    decision,
                    "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                    true,
                    new[] { 10, 11, 12 },
                    3),
                Throws.Nothing);
        }

        [Test]
        public void CaptureTool_ExposesRealGuiAutomationAndProfilerDataContract()
        {
            Assert.That(
                typeof(FirstArtTerrainEvidenceCapture).GetMethod(
                    "StartAutomatedCapture",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                typeof(FirstArtTerrainEvidenceCapture).GetMethod(
                    "CaptureAllAcceptedDeviation",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                typeof(FirstArtTerrainEvidenceCapture).GetMethod(
                    "CaptureAllAcceptedDeviationFromEnvironment",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static),
                Is.Not.Null);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FirstArtTerrainEvidenceCapture.cs"));
            StringAssert.Contains("Application.isBatchMode", source);
            StringAssert.Contains("ProfilerDriver.ClearAllFrames", source);
            StringAssert.Contains("ProfilerDriver.SaveProfile", source);
            StringAssert.Contains(
                "GrayboxPerformanceProbe.RecordFirstArtTerrainRuntimeEvidence()",
                source);
            StringAssert.Contains("SessionState.SetBool", source);
            StringAssert.Contains("[InitializeOnLoadMethod]", source);
            StringAssert.Contains("ResumeAutomationAfterDomainReload", source);
            StringAssert.Contains(
                "WASTECITY_FIRST_ART_VISUAL_DECISION",
                source);
            StringAssert.Contains(
                "user-accepted-known-visual-deviation",
                source);
            StringAssert.Contains(
                "user-accepted-known-visual-and-motion-deviation",
                source);
            StringAssert.Contains(
                "technicalVisualGatePassed",
                source);
            StringAssert.Contains(
                "accepted-current-first-version",
                source);
            StringAssert.Contains(
                "RenderPipelineManager.endCameraRendering += OnEndCameraRendering",
                source);
            StringAssert.Contains(
                "RenderPipelineManager.endCameraRendering -= OnEndCameraRendering",
                source);
            StringAssert.DoesNotContain(
                "EditorApplication.update += TickVideoCapture",
                source);
            StringAssert.Contains(
                "/tmp/wastecity-first-terrain/task-09-first-terrain-300-frames.data",
                source);
        }

        [Test]
        public void CaptureTool_ProfilesAfterVideoEncodingWithoutPngReadback()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FirstArtTerrainEvidenceCapture.cs"));
            int captureFlowIndex = source.IndexOf(
                "completedVideoPath = Path.Combine",
                StringComparison.Ordinal);
            int encodeIndex = source.IndexOf(
                "EncodeVideo(",
                captureFlowIndex,
                StringComparison.Ordinal);
            int finalClearIndex = source.LastIndexOf(
                "ProfilerDriver.ClearAllFrames",
                StringComparison.Ordinal);

            Assert.That(encodeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                finalClearIndex,
                Is.GreaterThan(encodeIndex),
                "the exact 300-frame Profiler window must start only " +
                "after synchronous PNG capture and video encoding finish");
        }

        [Test]
        public void AutomatedRuntimeWait_FailsFastAndCannotWaitForever()
        {
            Assert.That(
                FirstArtTerrainEvidenceCapture.IsAutomatedRuntimeReady(
                    true,
                    true,
                    true,
                    true,
                    null,
                    0d),
                Is.True);
            Assert.That(
                FirstArtTerrainEvidenceCapture.IsAutomatedRuntimeReady(
                    true,
                    false,
                    true,
                    false,
                    null,
                    1d),
                Is.False);
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.IsAutomatedRuntimeReady(
                    true,
                    true,
                    true,
                    false,
                    "synthetic presentation failure",
                    1d),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("synthetic presentation failure"));
            Assert.That(
                () => FirstArtTerrainEvidenceCapture.IsAutomatedRuntimeReady(
                    false,
                    false,
                    false,
                    false,
                    null,
                    120d),
                Throws.TypeOf<TimeoutException>()
                    .With.Message.Contains("bootstrapExists=False")
                    .And.Message.Contains("presenterExists=False"));
        }

        [Test]
        public void RuntimeContext_ValidatesTheFormalSurfaceWithoutRejectingRuinsCliffBatches()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FirstArtTerrainEvidenceCapture.cs"));

            StringAssert.Contains("presenter.SurfaceRenderer", source);
            StringAssert.DoesNotContain(
                "GetComponentsInChildren<MeshRenderer>(true).Length != 1",
                source);

            string performanceSource = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxPerformanceProbe.cs"));
            StringAssert.Contains(
                "presenter.SurfaceRenderer != null ? 1 : 0",
                performanceSource);
        }

        [Test]
        public void AutomatedCapture_EntersGameplayThroughAnIsolatedTemporarySave()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FirstArtTerrainEvidenceCapture.cs"));

            StringAssert.Contains("EnterIsolatedGameplayForEvidence", source);
            StringAssert.Contains("new FormalSaveStore(root)", source);
            StringAssert.Contains("new FormalSaveWaveRetryStore(root)", source);
            StringAssert.Contains("slotRequiresOverwriteConfirmation", source);
            StringAssert.Contains("RequestNewGame()", source);
            StringAssert.Contains("DeleteEvidenceSaveRoot", source);
        }

        [Test]
        public void Idea0018ZoomEvidence_UsesFormalProfileAndCrossesEveryLodMonotonically()
        {
            var profile = ScriptableObject.CreateInstance<
                FormalMapNavigationProfile3D>();
            try
            {
                profile.Configure(
                    8f,
                    13f,
                    26f,
                    1f / 120f,
                    15f,
                    21f);

                IReadOnlyList<FirstArtTerrainEvidenceCapture.ZoomFrameSpec>
                    frames = FirstArtTerrainEvidenceCapture
                        .BuildZoomFrameSpecs(profile);

                Assert.That(frames.Count, Is.EqualTo(10));
                Assert.That(frames[0].Index, Is.EqualTo(0));
                Assert.That(frames[0].ScrollDeltaY, Is.EqualTo(0f));
                Assert.That(frames[0].OrthographicSize, Is.EqualTo(13f));
                Assert.That(frames[frames.Count - 1].OrthographicSize,
                    Is.EqualTo(22f));
                Assert.That(
                    frames.Select(frame => frame.Lod),
                    Is.EqualTo(new[]
                    {
                        ResourceNodeMarkerLod3D.Near,
                        ResourceNodeMarkerLod3D.Near,
                        ResourceNodeMarkerLod3D.Near,
                        ResourceNodeMarkerLod3D.Mid,
                        ResourceNodeMarkerLod3D.Mid,
                        ResourceNodeMarkerLod3D.Mid,
                        ResourceNodeMarkerLod3D.Mid,
                        ResourceNodeMarkerLod3D.Mid,
                        ResourceNodeMarkerLod3D.Mid,
                        ResourceNodeMarkerLod3D.Far,
                    }));
                for (var index = 1; index < frames.Count; index++)
                {
                    Assert.That(frames[index].Index, Is.EqualTo(index));
                    Assert.That(frames[index].ScrollDeltaY, Is.EqualTo(-120f));
                    Assert.That(frames[index].OrthographicSize,
                        Is.GreaterThan(frames[index - 1].OrthographicSize));
                }

                Assert.That(
                    () => FirstArtTerrainEvidenceCapture
                        .ValidateZoomFrameSpecs(profile, frames),
                    Throws.Nothing);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Idea0018ZoomEvidence_RejectsMissingLodAndConfirmsCameraRestoration()
        {
            var profile = ScriptableObject.CreateInstance<
                FormalMapNavigationProfile3D>();
            try
            {
                profile.Configure(
                    8f,
                    13f,
                    26f,
                    1f / 120f,
                    15f,
                    21f);
                var invalid = new[]
                {
                    new FirstArtTerrainEvidenceCapture.ZoomFrameSpec(
                        0,
                        0f,
                        13f,
                        ResourceNodeMarkerLod3D.Near),
                    new FirstArtTerrainEvidenceCapture.ZoomFrameSpec(
                        1,
                        -120f,
                        14f,
                        ResourceNodeMarkerLod3D.Near),
                };

                Assert.That(
                    () => FirstArtTerrainEvidenceCapture
                        .ValidateZoomFrameSpecs(profile, invalid),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("Near, Mid and Far"));
                Assert.That(
                    () => FirstArtTerrainEvidenceCapture
                        .ValidateZoomRestoration(
                            13f,
                            13f,
                            ResourceNodeMarkerLod3D.Near,
                            ResourceNodeMarkerLod3D.Near),
                    Throws.Nothing);
                Assert.That(
                    () => FirstArtTerrainEvidenceCapture
                        .ValidateZoomRestoration(
                            13f,
                            14f,
                            ResourceNodeMarkerLod3D.Near,
                            ResourceNodeMarkerLod3D.Near),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("restore"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Idea0018UiRenderScope_RestoresOverlayCanvasAndCameraState()
        {
            var cameraObject = new GameObject("idea0018-ui-camera");
            var canvasObject = new GameObject("idea0018-overlay-canvas");
            Camera camera = cameraObject.AddComponent<Camera>();
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            camera.cullingMask = 1 << 0;
            canvasObject.layer = 5;
            int originalMask = camera.cullingMask;
            try
            {
                using (FirstArtTerrainEvidenceCapture
                           .PrepareUiCanvasesForCameraRender(
                               camera,
                               new[] { canvas }))
                {
                    Assert.That(canvas.renderMode,
                        Is.EqualTo(RenderMode.ScreenSpaceCamera));
                    Assert.That(canvas.worldCamera, Is.SameAs(camera));
                    Assert.That(camera.cullingMask & (1 << canvasObject.layer),
                        Is.Not.Zero);
                }

                Assert.That(canvas.renderMode,
                    Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.worldCamera, Is.Null);
                Assert.That(canvas.sortingOrder, Is.EqualTo(50));
                Assert.That(camera.cullingMask, Is.EqualTo(originalMask));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Idea0018CaptureManifest_RecordsLodUiStateResolutionAndHashes()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FirstArtTerrainEvidenceCapture.cs"));

            StringAssert.Contains("13-resource-marker-near.png", source);
            StringAssert.Contains("14-resource-marker-mid.png", source);
            StringAssert.Contains("15-resource-marker-far.png", source);
            StringAssert.Contains("20-ui-main-hud.png", source);
            StringAssert.Contains("21-ui-research-tree.png", source);
            StringAssert.Contains("zoom-frames", source);
            StringAssert.Contains("resourceMarkerLod", source);
            StringAssert.Contains("uiPanelState", source);
            StringAssert.Contains("orthographicSize", source);
            StringAssert.Contains("sha256", source);
            StringAssert.Contains("SetResearchOpen(true)", source);
            StringAssert.Contains("PrepareUiCanvasesForCameraRender", source);
            StringAssert.Contains("AwaitClosedPresentation", source);
            StringAssert.Contains(
                "RefreshResourceNodeMarkerLod(\n                    OverviewOrthographicSize)",
                source);
        }
    }
}
