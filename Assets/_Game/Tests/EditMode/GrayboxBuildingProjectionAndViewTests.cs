using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingProjectionAndViewTests
    {
        private static readonly Vector2 ScreenCenter = new Vector2(50f, 50f);
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAfterLifecycleTest()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [Test]
        public void Projector_InnerColliderWinsAndUsesFrozenLocalGrid()
        {
            WorldFixture fixture = CreateWorldFixture();
            InnerSurfaceFixture inner = CreateInnerSurface(fixture.City);
            GrayboxBuildingSurfaceProjector3D projector =
                CreateProjector(fixture, inner.Collider);
            Vector3 localCenter = new Vector3(-1.12f, 1.06f, -.8f);
            PositionCameraOver(fixture.Camera, inner.Root.TransformPoint(localCenter));
            Physics.SyncTransforms();

            bool projected = projector.TryProject(
                ScreenCenter,
                out BuildingSurfaceHit hit);

            Assert.That(projected, Is.True);
            Assert.That(hit.IsValid, Is.True);
            Assert.That(hit.Site, Is.EqualTo(BuildingSite.InnerCity));
            Assert.That(hit.X, Is.Zero);
            Assert.That(hit.Y, Is.Zero);
            Assert.That(hit.SurfaceLabel, Is.EqualTo("内城"));
            Assert.That(
                inner.Root.InverseTransformPoint(hit.WorldPoint).x,
                Is.EqualTo(-1.12f).Within(.001f));
            Assert.That(
                inner.Root.InverseTransformPoint(hit.WorldPoint).z,
                Is.EqualTo(-.8f).Within(.001f));
        }

        [Test]
        public void Projector_InnerGridUsesEightBySixBoundsWithoutGroundFallback()
        {
            WorldFixture fixture = CreateWorldFixture();
            InnerSurfaceFixture inner = CreateInnerSurface(fixture.City);
            inner.Collider.size = new Vector3(2.8f, .1f, 2.2f);
            GrayboxBuildingSurfaceProjector3D projector =
                CreateProjector(fixture, inner.Collider);
            PositionCameraOver(
                fixture.Camera,
                inner.Root.TransformPoint(new Vector3(1.3f, 1.06f, 0f)));
            Physics.SyncTransforms();

            Assert.That(
                projector.TryProject(ScreenCenter, out BuildingSurfaceHit hit),
                Is.False);
            Assert.That(hit.IsValid, Is.False);
        }

        [Test]
        public void Projector_CityMotionMovesInnerWorldPointButKeepsLogicalCell()
        {
            WorldFixture fixture = CreateWorldFixture();
            InnerSurfaceFixture inner = CreateInnerSurface(fixture.City);
            GrayboxBuildingSurfaceProjector3D projector =
                CreateProjector(fixture, inner.Collider);
            Vector3 localCenter = new Vector3(.16f, 1.06f, .16f);
            PositionCameraOver(fixture.Camera, inner.Root.TransformPoint(localCenter));
            Physics.SyncTransforms();
            Assert.That(
                projector.TryProject(ScreenCenter, out BuildingSurfaceHit before),
                Is.True);

            Vector3 motion = new Vector3(3f, 0f, 2f);
            fixture.City.transform.position += motion;
            fixture.Camera.transform.position += motion;
            Physics.SyncTransforms();
            Assert.That(
                projector.TryProject(ScreenCenter, out BuildingSurfaceHit after),
                Is.True);

            Assert.That(after.Site, Is.EqualTo(BuildingSite.InnerCity));
            Assert.That(after.X, Is.EqualTo(before.X));
            Assert.That(after.Y, Is.EqualTo(before.Y));
            Assert.That(after.WorldPoint, Is.EqualTo(before.WorldPoint + motion));
        }

        [Test]
        public void Projector_GroundUsesMathematicalPlaneAndCoordinateMapper()
        {
            WorldFixture fixture = CreateWorldFixture();
            InnerSurfaceFixture inner = CreateInnerSurface(fixture.City);
            inner.Collider.enabled = false;
            GrayboxBuildingSurfaceProjector3D projector =
                CreateProjector(fixture, inner.Collider);
            Assert.That(
                fixture.World.Coordinates.TryCellToWorld(
                    20,
                    15,
                    0f,
                    out Vector3 target),
                Is.True);
            PositionCameraOver(fixture.Camera, target + new Vector3(.1f, 0f, .1f));
            Physics.SyncTransforms();

            bool projected = projector.TryProject(
                ScreenCenter,
                out BuildingSurfaceHit hit);

            Assert.That(projected, Is.True);
            Assert.That(hit.Site, Is.EqualTo(BuildingSite.Ground));
            Assert.That(hit.X, Is.EqualTo(20));
            Assert.That(hit.Y, Is.EqualTo(15));
            Assert.That(hit.WorldPoint, Is.EqualTo(target));
            Assert.That(hit.SurfaceLabel, Is.EqualTo("外城"));
        }

        [Test]
        public void Projector_ParallelBackwardAndOutsideWorldRaysFail()
        {
            WorldFixture fixture = CreateWorldFixture();
            InnerSurfaceFixture inner = CreateInnerSurface(fixture.City);
            inner.Collider.enabled = false;
            GrayboxBuildingSurfaceProjector3D projector =
                CreateProjector(fixture, inner.Collider);

            fixture.Camera.transform.position = new Vector3(0f, 2f, 0f);
            fixture.Camera.transform.rotation = Quaternion.identity;
            Assert.That(projector.TryProject(ScreenCenter, out _), Is.False);

            fixture.Camera.transform.position = new Vector3(0f, -2f, 0f);
            fixture.Camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Assert.That(projector.TryProject(ScreenCenter, out _), Is.False);

            PositionCameraOver(fixture.Camera, new Vector3(100f, 0f, 100f));
            Assert.That(projector.TryProject(ScreenCenter, out _), Is.False);
        }

        [Test]
        public void Placement_AllFootprintCellsFeedOrderedWorldAndGridChecks()
        {
            WorldCell[,] cells = OpenCells();
            cells[18, 13] = Cell(traversal: WorldTraversalKind.DeepWater);
            cells[18, 14] = Cell(
                ResourceIds.Iron,
                WorldTraversalKind.Ruins);
            WorldFixture fixture = CreateWorldFixture(cells, CityMode.Fortress);
            Assert.That(
                fixture.Session.GroundGrid.TryRestore(
                    BuildingCatalog.Wall,
                    17,
                    14,
                    out _),
                Is.True);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 17, 13);

            fixture.Placement.UpdatePointer(ScreenCenter);
            GrayboxBuildingMenuView3D menu =
                CreatePlacementStatusMenu(fixture);
            menu.SetPlacementController(fixture.Placement);

            Assert.That(fixture.Placement.CurrentHit.IsValid, Is.True);
            Assert.That(fixture.Placement.CurrentEvaluation.IsValid, Is.False);
            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.Overlap,
                    BuildingPlacementFailure.CityOccupied,
                    BuildingPlacementFailure.InvalidTerrain,
                    BuildingPlacementFailure.Obstacle
                }));
            Assert.That(
                fixture.Placement.CurrentEvaluation.CompatibleResourceNodeId,
                Is.EqualTo("world.resource-node.18.14"));
            Assert.That(
                fixture.Placement.CurrentEvaluation.CompatibleResourceNodeId,
                Is.Not.EqualTo(ResourceIds.Iron));
            Assert.That(
                PlacementStatusText(menu).text,
                Does.Contain("重叠"));
        }

        [Test]
        public void Placement_EastNonSquareFootprintFeedsRotationOnlyCellsToEveryCheck()
        {
            WorldCell[,] cells = OpenCells();
            cells[17, 12] = Cell(
                traversal: WorldTraversalKind.DeepWater);
            cells[18, 12] = Cell(
                traversal: WorldTraversalKind.Ruins);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress);
            fixture.Session.SetRouteContact(
                WasteCity.Content.ContentRoute.BiologicalAscension,
                true);
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.BehemothPen.RequiredResearchId);
            fixture.Session.Inventory.Set(
                ResourceIds.BoneSteel,
                BuildingCatalog.BehemothPen.Cost);
            Assert.That(
                fixture.Session.GroundGrid.TryRestore(
                    BuildingCatalog.Wall,
                    17,
                    12,
                    out _),
                Is.True);
            fixture.Interaction.Select(BuildingCatalog.BehemothPen);
            fixture.Interaction.RotateClockwise();
            PositionCameraAtCell(fixture, 17, 10);

            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(
                fixture.Interaction.Orientation,
                Is.EqualTo(BuildingOrientation.East));
            Assert.That(
                fixture.Placement.CurrentEvaluation.RotatedWidth,
                Is.EqualTo(2));
            Assert.That(
                fixture.Placement.CurrentEvaluation.RotatedHeight,
                Is.EqualTo(3));
            Assert.That(
                fixture.Placement.CurrentEvaluation.Footprint
                    .Select(value => new Vector2Int(value.X, value.Y)),
                Is.EqualTo(new[]
                {
                    new Vector2Int(17, 10),
                    new Vector2Int(17, 11),
                    new Vector2Int(17, 12),
                    new Vector2Int(18, 10),
                    new Vector2Int(18, 11),
                    new Vector2Int(18, 12)
                }));
            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures.Take(4),
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.Overlap,
                    BuildingPlacementFailure.CityOccupied,
                    BuildingPlacementFailure.InvalidTerrain,
                    BuildingPlacementFailure.Obstacle
                }));
            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Does.Contain(
                    BuildingPlacementFailure.PrerequisiteBuildingRequired));
        }

        [Test]
        public void Placement_MiningHighlightsOnlyCompatibleCoordinateNode()
        {
            WorldCell[,] cells = OpenCells();
            cells[19, 15] = Cell(ResourceIds.Iron);
            WorldFixture fixture = CreateWorldFixture(cells, CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 18, 14);

            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(fixture.Placement.CurrentEvaluation.IsValid, Is.True);
            Assert.That(
                ActiveGuidanceSlots(
                        fixture.Presentation,
                        "building.node-highlight.")
                    .Select(value => value.StableId),
                Is.EqualTo(new[]
                {
                    "building.node-highlight.world.resource-node.19.15"
                }));

            fixture.Interaction.Select(BuildingCatalog.Wall);
            fixture.Placement.UpdatePointer(ScreenCenter);
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.node-highlight."),
                Is.Empty);
        }

        [Test]
        public void Placement_MiningAcceptsEnergyCrystalWithCoordinateNodeId()
        {
            WorldCell[,] cells = OpenCells();
            cells[19, 15] = Cell(ResourceIds.EnergyCrystal);
            WorldFixture fixture = CreateWorldFixture(cells, CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 18, 14);

            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(fixture.Placement.CurrentEvaluation.IsValid, Is.True);
            Assert.That(
                fixture.Placement.CurrentEvaluation.CompatibleResourceNodeId,
                Is.EqualTo("world.resource-node.19.15"));
        }

        [Test]
        public void IDEA0010_MiningGuidanceShowsCompatibleNodesAndFormalLegalAnchors()
        {
            WorldCell[,] cells = OpenCells();
            cells[20, 16] = Cell(ResourceIds.Iron);
            cells[21, 16] = Cell(ResourceIds.EnergyCrystal);
            cells[16, 12] = Cell(ResourceIds.Iron);
            cells[27, 12] = Cell(ResourceIds.Iron);
            cells[18, 16] = Cell(ResourceIds.Stone);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);

            InvokeRefreshMiningGuidance(fixture.Placement);

            GrayboxVisualSlot[] activeNodes = ActiveGuidanceSlots(
                fixture.Presentation,
                "building.node-highlight.");
            Assert.That(
                activeNodes.Select(value => value.StableId),
                Is.EquivalentTo(new[]
                {
                    "building.node-highlight.world.resource-node.20.16",
                    "building.node-highlight.world.resource-node.21.16",
                    "building.node-highlight.world.resource-node.16.12",
                    "building.node-highlight.world.resource-node.18.16"
                }));
            Assert.That(
                activeNodes.Select(value => value.StableId),
                Does.Not.Contain(
                    "building.node-highlight.world.resource-node.27.12"));
            Color legalNodeColor = PropertyColor(
                activeNodes.Single(value => value.StableId.EndsWith(
                    ".20.16",
                    StringComparison.Ordinal)).Renderer);
            Color blockedNodeColor = PropertyColor(
                activeNodes.Single(value => value.StableId.EndsWith(
                    ".16.12",
                    StringComparison.Ordinal)).Renderer);
            Assert.That(legalNodeColor.g, Is.GreaterThan(legalNodeColor.r));
            Assert.That(blockedNodeColor.r, Is.GreaterThan(blockedNodeColor.g));

            GrayboxVisualSlot[] anchors = ActiveGuidanceSlots(
                fixture.Presentation,
                "building.anchor-highlight.");
            Assert.That(anchors, Is.Not.Empty);
            Assert.That(
                anchors.Select(value => value.StableId).Distinct().Count(),
                Is.EqualTo(anchors.Length),
                "An anchor covering two compatible nodes is shown once.");
            for (var index = 0; index < anchors.Length; index++)
            {
                BuildingPlacementEvaluation evaluation =
                    EvaluateGuidanceAnchor(
                        fixture.Placement,
                        anchors[index].StableId);
                Assert.That(
                    evaluation.IsValid,
                    Is.True,
                    anchors[index].StableId + ": " +
                    evaluation.PrimaryFailure);
            }
        }

        [Test]
        public void IDEA0010_MiningGuidancePoolsAndCachesUnchangedRefresh()
        {
            WorldCell[,] cells = OpenCells();
            cells[20, 16] = Cell(ResourceIds.Iron);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            InvokeRefreshMiningGuidance(fixture.Placement);
            GrayboxVisualSlot node = ActiveGuidanceSlots(
                fixture.Presentation,
                "building.node-highlight.").Single();
            GrayboxVisualSlot anchor = ActiveGuidanceSlots(
                fixture.Presentation,
                "building.anchor-highlight.").First();
            Mesh nodeMesh = node.GetComponent<MeshFilter>().sharedMesh;
            Mesh anchorMesh = anchor.GetComponent<MeshFilter>().sharedMesh;
            Renderer nodeRenderer = node.Renderer;
            Renderer anchorRenderer = anchor.Renderer;
            int pooledNodeCount = GuidanceSlotCount(
                fixture.Presentation,
                "building.node-highlight.");
            int pooledAnchorCount = GuidanceSlotCount(
                fixture.Presentation,
                "building.anchor-highlight.");
            int infrastructureTransformCount = fixture.Presentation
                .transform.Find("infrastructure")
                .GetComponentsInChildren<Transform>(true).Length;

            Action warmedRefresh =
                MiningGuidanceRefreshAction(fixture.Placement);
            warmedRefresh();
            warmedRefresh();
            AllocationMeasurement measurement =
                Profile300Calls(warmedRefresh);
            TestContext.WriteLine(
                "IDEA0010GuidanceProfilerBytes=" +
                measurement.ProfiledBytes);
            TestContext.WriteLine(
                "IDEA0010GuidanceCurrentThreadBytes=" +
                measurement.CurrentThreadBytes);
            TestContext.WriteLine(
                "IDEA0010PooledNodeCount=" + pooledNodeCount);
            TestContext.WriteLine(
                "IDEA0010PooledAnchorCount=" + pooledAnchorCount);
            TestContext.WriteLine(
                "IDEA0010InfrastructureTransformCount=" +
                infrastructureTransformCount);
            Assert.That(measurement.ProfiledBytes, Is.Zero);
            Assert.That(measurement.CurrentThreadBytes, Is.Zero);
            Assert.That(
                GuidanceSlotCount(
                    fixture.Presentation,
                    "building.node-highlight."),
                Is.EqualTo(pooledNodeCount));
            Assert.That(
                GuidanceSlotCount(
                    fixture.Presentation,
                    "building.anchor-highlight."),
                Is.EqualTo(pooledAnchorCount));
            Assert.That(
                fixture.Presentation.transform.Find("infrastructure")
                    .GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(infrastructureTransformCount));
            Assert.That(node.Renderer, Is.SameAs(nodeRenderer));
            Assert.That(anchor.Renderer, Is.SameAs(anchorRenderer));
            Assert.That(
                node.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(nodeMesh));
            Assert.That(
                anchor.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(anchorMesh));

            fixture.Interaction.Select(BuildingCatalog.Wall);
            InvokeRefreshMiningGuidance(fixture.Placement);
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.node-highlight."),
                Is.Empty);
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.anchor-highlight."),
                Is.Empty);
            Assert.That(node != null, Is.True);
            Assert.That(anchor != null, Is.True);
        }

        [Test]
        public void IDEA0018_MiningGuidanceOverridesAndRestoresMarkerLod()
        {
            WorldCell[,] cells = OpenCells();
            cells[20, 16] = Cell(ResourceIds.Iron);
            cells[18, 16] = Cell(ResourceIds.Stone);
            cells[19, 16] = Cell(ResourceIds.Biomass);
            cells[27, 12] = Cell(ResourceIds.Iron);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress);
            fixture.World.RefreshResourceNodeMarkerLod(24f);
            Assert.That(fixture.World.TryGetResourceNodeMarker(
                    20,
                    16,
                    out GrayboxResourceNodeMarker3D marker),
                Is.True);
            Assert.That(marker.DisplayLod,
                Is.EqualTo(ResourceNodeMarkerLod3D.Far));
            Assert.That(fixture.World.TryGetResourceNodeMarker(
                    18,
                    16,
                    out GrayboxResourceNodeMarker3D stone),
                Is.True);
            Assert.That(fixture.World.TryGetResourceNodeMarker(
                    19,
                    16,
                    out GrayboxResourceNodeMarker3D incompatible),
                Is.True);
            Assert.That(fixture.World.TryGetResourceNodeMarker(
                    27,
                    12,
                    out GrayboxResourceNodeMarker3D outsideRange),
                Is.True);

            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            fixture.Placement.RefreshMiningGuidance();

            Assert.That(marker.GuidanceOverride, Is.True);
            Assert.That(marker.DisplayLod,
                Is.EqualTo(ResourceNodeMarkerLod3D.Near));
            Assert.That(stone.GuidanceOverride, Is.True);
            Assert.That(stone.DisplayLod,
                Is.EqualTo(ResourceNodeMarkerLod3D.Near));
            Assert.That(incompatible.GuidanceOverride, Is.False);
            Assert.That(incompatible.DisplayLod,
                Is.EqualTo(ResourceNodeMarkerLod3D.Far));
            Assert.That(outsideRange.GuidanceOverride, Is.False);
            Assert.That(outsideRange.DisplayLod,
                Is.EqualTo(ResourceNodeMarkerLod3D.Far));
            fixture.Interaction.Select(BuildingCatalog.Wall);
            fixture.Placement.RefreshMiningGuidance();
            Assert.That(marker.GuidanceOverride, Is.False);
            Assert.That(marker.DisplayLod,
                Is.EqualTo(ResourceNodeMarkerLod3D.Far));
            Assert.That(stone.GuidanceOverride, Is.False);
            Assert.That(stone.DisplayLod,
                Is.EqualTo(ResourceNodeMarkerLod3D.Far));
        }

        [Test]
        public void IDEA0010_NonSquareCandidateEnumeratorUsesRotatedFootprint()
        {
            var synthetic = new BuildingDefinition(
                BuildingCatalog.MiningStation.Id.Value,
                "synthetic mining enumerator",
                3,
                2,
                ResourceIds.Alloy,
                1,
                true,
                operation: BuildingOperation.TerrainDependent);
            var anchors = new List<Vector2Int>();
            MethodInfo method =
                typeof(GrayboxBuildingPlacementController3D).GetMethod(
                    "CopyFootprintCoveringAnchors",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(
                method,
                Is.Not.Null,
                "IDEA-0010 requires one orientation-aware enumerator.");

            method.Invoke(
                null,
                new object[]
                {
                    synthetic,
                    BuildingOrientation.East,
                    10,
                    20,
                    anchors
                });

            Assert.That(
                anchors,
                Is.EquivalentTo(new[]
                {
                    new Vector2Int(9, 18),
                    new Vector2Int(9, 19),
                    new Vector2Int(9, 20),
                    new Vector2Int(10, 18),
                    new Vector2Int(10, 19),
                    new Vector2Int(10, 20)
                }));
        }

        [TestCase("occupancy", BuildingPlacementFailure.Overlap)]
        [TestCase("terrain", BuildingPlacementFailure.InvalidTerrain)]
        [TestCase("city", BuildingPlacementFailure.CityOccupied)]
        [TestCase("boundary", BuildingPlacementFailure.OutOfBounds)]
        [TestCase("inventory", BuildingPlacementFailure.InsufficientMaterials)]
        [TestCase("lock", BuildingPlacementFailure.Overlap)]
        [TestCase("city-mode", BuildingPlacementFailure.InvalidCityMode)]
        public void IDEA0010_GuidanceCandidatesExposeEachFormalFailure(
            string scenario,
            BuildingPlacementFailure expectedFailure)
        {
            WorldCell[,] cells = OpenCells();
            int nodeX = scenario == "boundary" ? 0 : 20;
            int nodeY = scenario == "boundary" ? 0 : 16;
            if (scenario == "city")
            {
                nodeX = 16;
                nodeY = 12;
            }
            cells[nodeX, nodeY] = Cell(
                ResourceIds.Iron,
                scenario == "terrain"
                    ? WorldTraversalKind.DeepWater
                    : WorldTraversalKind.Open);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                scenario == "city-mode"
                    ? CityMode.Mobile
                    : CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            int anchorX = scenario == "boundary" ? -1 : nodeX;
            int anchorY = scenario == "boundary" ? -1 : nodeY;
            if (scenario == "occupancy")
            {
                Assert.That(
                    fixture.Session.GroundGrid.TryRestore(
                        BuildingCatalog.Wall,
                        nodeX,
                        nodeY,
                        out _),
                    Is.True);
            }
            if (scenario == "lock")
            {
                GrayboxBuildingInstance3D locked = Begin(
                    fixture,
                    BuildingCatalog.Wall,
                    BuildingSite.Ground,
                    nodeX,
                    nodeY,
                    CityMode.Fortress);
                var work = BuildingEvacuationRules.Create(
                    locked.StableInstanceId,
                    locked.Placement.Definition.Cost,
                    locked.Progress.BaseDuration,
                    locked.Progress.Remaining /
                    locked.Progress.BaseDuration,
                    BuildingEvacuationTreatment.FullDismantle);
                Assert.That(
                    fixture.Session.TryCaptureEvacuationWork(
                        new[] { work },
                        out string captureFailure),
                    Is.True,
                    captureFailure);
                Assert.That(
                    fixture.Session.TryLockEvacuationWork(
                        new[] { work },
                        out string lockFailure),
                    Is.True,
                    lockFailure);
                Assert.That(locked.IsEvacuationLocked, Is.True);
            }
            if (scenario == "inventory")
                fixture.Session.Inventory.Set(ResourceIds.Alloy, 0);

            BuildingPlacementEvaluation evaluation =
                EvaluateGuidanceAnchor(
                    fixture.Placement,
                    MiningAnchorStableId(
                        anchorX,
                        anchorY,
                        BuildingOrientation.North));

            Assert.That(
                evaluation.Failures,
                Does.Contain(expectedFailure),
                "IDEA-0010 formal failure scenario: " + scenario);
            InvokeRefreshMiningGuidance(fixture.Placement);
            if (scenario != "boundary")
                Assert.That(
                    ActiveGuidanceSlots(
                            fixture.Presentation,
                            "building.node-highlight.")
                        .Single(value => value.StableId.EndsWith(
                            "." + nodeX + "." + nodeY,
                            StringComparison.Ordinal))
                        .Renderer,
                    Is.Not.Null,
                    "Compatible blocked node remains visible: " + scenario);
        }

        [TestCase(ResourceIds.Biomass)]
        [TestCase(ResourceIds.Water)]
        public void Placement_MiningRejectsIncompatibleResourceTypes(
            string resourceId)
        {
            WorldCell[,] cells = OpenCells();
            cells[19, 15] = Cell(resourceId);
            WorldFixture fixture = CreateWorldFixture(cells, CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 18, 14);

            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Does.Contain(BuildingPlacementFailure.IncompatibleResourceNode));
            Assert.That(
                fixture.Placement.CurrentEvaluation.CompatibleResourceNodeId,
                Is.Null);
        }

        [Test]
        public void Placement_NonMiningIgnoresResourceCellsAndOmitsNodeId()
        {
            WorldCell[,] cells = OpenCells();
            cells[20, 15] = Cell(ResourceIds.Stone);
            WorldFixture fixture = CreateWorldFixture(cells, CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.Wall);
            PositionCameraAtCell(fixture, 20, 15);

            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(fixture.Placement.CurrentEvaluation.IsValid, Is.True);
            Assert.That(
                fixture.Placement.CurrentEvaluation.CompatibleResourceNodeId,
                Is.Null);
        }

        [Test]
        public void Placement_ResourceNodeIdentityUsesCoordinatesNotResourceType()
        {
            string first = GrayboxBuildingPlacementController3D
                .CreateResourceNodeVisualId(2, 3);
            string second = GrayboxBuildingPlacementController3D
                .CreateResourceNodeVisualId(4, 5);

            Assert.That(first, Is.EqualTo("world.resource-node.2.3"));
            Assert.That(second, Is.EqualTo("world.resource-node.4.5"));
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(ResourceIds.Iron));
        }

        [Test]
        public void Placement_ConfirmationReevaluatesCityModeInsteadOfTrustingPreview()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.Wall);
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            Assert.That(fixture.Placement.CurrentEvaluation.IsValid, Is.True);
            fixture.City.Deployment.Restore(CityMode.Packing, 1f);

            bool confirmed = fixture.Placement.ConfirmCurrentPlacement(
                out GrayboxBuildingInstance3D instance);

            Assert.That(confirmed, Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(
                fixture.Placement.CurrentEvaluation.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.InvalidCityMode));
            Assert.That(fixture.Session.Instances, Is.Empty);
            Assert.That(fixture.Session.GroundGrid.Count, Is.Zero);
        }

        [Test]
        public void Placement_ContinuousSelectionSurvivesSuccessAndExhaustionStaysRed()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            fixture.Session.Inventory.Set(ResourceIds.Stone, 2);
            fixture.Interaction.Select(BuildingCatalog.Wall);
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            GrayboxBuildingMenuView3D menu =
                CreatePlacementStatusMenu(fixture);
            menu.SetPlacementController(fixture.Placement);
            Assert.That(
                PlacementStatusText(menu).gameObject.activeInHierarchy,
                Is.True);
            Assert.That(
                PlacementStatusText(menu).text,
                Does.Contain("方向 北").And.Contain("占地 1×1"));

            Assert.That(
                fixture.Placement.ConfirmCurrentPlacement(out _),
                Is.True);
            Assert.That(
                fixture.Interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Previewing));
            Assert.That(
                fixture.Interaction.Selected,
                Is.SameAs(BuildingCatalog.Wall));

            PositionCameraAtCell(fixture, 21, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            menu.SetPlacementController(fixture.Placement);

            Assert.That(fixture.Placement.CurrentEvaluation.IsValid, Is.False);
            Assert.That(
                fixture.Placement.CurrentEvaluation.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.InsufficientMaterials));
            GrayboxVisualSlot preview = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            Color shown = PropertyColor(preview.Renderer);
            Assert.That(shown.r, Is.GreaterThan(shown.g));
            Assert.That(
                PlacementStatusText(menu).text,
                Does.Contain("缺少石料 2"));
            Assert.That(
                PlacementStatusText(menu).text,
                Does.Not.Contain("重叠"));
            Assert.That(
                PlacementStatusText(menu).gameObject.activeInHierarchy,
                Is.True);
            fixture.Interaction.CancelPreview();
            menu.SetPlacementController(fixture.Placement);
            Assert.That(
                PlacementStatusText(menu).gameObject.activeInHierarchy,
                Is.False);
        }

        [Test]
        public void Menu_PlacementFailureMessagesAreStableDistinctAndComplete()
        {
            string[] messages = Enum
                .GetValues(typeof(BuildingPlacementFailure))
                .Cast<BuildingPlacementFailure>()
                .Select(GrayboxBuildingMenuView3D.PlacementFailureMessage)
                .ToArray();

            Assert.That(messages, Has.All.Not.Empty);
            Assert.That(messages.Distinct().Count(), Is.EqualTo(
                messages.Length));
        }

        [Test]
        public void WorldView_TwoSameTypeNodesKeepDistinctVisualSlots()
        {
            WorldFixture fixture = CreateWorldFixture();
            string first = GrayboxBuildingPlacementController3D
                .CreateResourceNodeVisualId(2, 3);
            string second = GrayboxBuildingPlacementController3D
                .CreateResourceNodeVisualId(4, 5);

            fixture.Presentation.ShowCompatibleResourceNode(
                first,
                2,
                3,
                true);
            fixture.Presentation.ShowCompatibleResourceNode(
                second,
                4,
                5,
                true);

            GrayboxVisualSlot firstSlot = Slot(
                fixture.Presentation,
                "building.node-highlight." + first);
            GrayboxVisualSlot secondSlot = Slot(
                fixture.Presentation,
                "building.node-highlight." + second);
            Assert.That(firstSlot, Is.Not.SameAs(secondSlot));
            Assert.That(firstSlot.Renderer, Is.Not.SameAs(secondSlot.Renderer));
        }

        [Test]
        public void WorldView_BuildGridDefaultsHiddenAndTogglesOnlyStableGridRoots()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxVisualSlot ground = Slot(
                fixture.Presentation,
                "building.grid.ground");
            GrayboxVisualSlot boundary = Slot(
                fixture.Presentation,
                "building.range.ground-boundary");
            GrayboxVisualSlot inner = Slot(
                fixture.Presentation,
                "building.grid.inner-city");
            Mesh innerMesh =
                inner.GetComponent<MeshFilter>().sharedMesh;

            Assert.That(
                fixture.Presentation.IsBuildGridVisible,
                Is.False);
            Assert.That(ground.gameObject.activeSelf, Is.False);
            Assert.That(boundary.gameObject.activeSelf, Is.False);
            Assert.That(inner.gameObject.activeSelf, Is.False);

            fixture.Presentation.ShowCompatibleResourceNode(
                "world.resource-node.20.15",
                20,
                15,
                true);
            GrayboxVisualSlot node = Slot(
                fixture.Presentation,
                "building.node-highlight.world.resource-node.20.15");
            fixture.Placement.SetBuildGridVisible(true);

            Assert.That(
                fixture.Presentation.IsBuildGridVisible,
                Is.True);
            Assert.That(ground.gameObject.activeSelf, Is.True);
            Assert.That(boundary.gameObject.activeSelf, Is.True);
            Assert.That(inner.gameObject.activeSelf, Is.True);
            Assert.That(node.gameObject.activeSelf, Is.True);
            Mesh groundMesh =
                ground.GetComponent<MeshFilter>().sharedMesh;
            Mesh boundaryMesh =
                boundary.GetComponent<MeshFilter>().sharedMesh;

            fixture.Presentation.SetBuildGridVisible(false);

            Assert.That(
                fixture.Presentation.IsBuildGridVisible,
                Is.False);
            Assert.That(ground.gameObject.activeSelf, Is.False);
            Assert.That(boundary.gameObject.activeSelf, Is.False);
            Assert.That(inner.gameObject.activeSelf, Is.False);
            Assert.That(node.gameObject.activeSelf, Is.True);
            Assert.That(
                Slot(fixture.Presentation, "building.grid.ground"),
                Is.SameAs(ground));
            Assert.That(
                Slot(fixture.Presentation, "building.grid.inner-city"),
                Is.SameAs(inner));
            Assert.That(
                ground.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(groundMesh));
            Assert.That(
                boundary.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(boundaryMesh));
            Assert.That(
                inner.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(innerMesh));
        }

        [Test]
        public void IDEA0008_VisibleGroundGridMatchesTheFormalInitialRange()
        {
            const int width = 32;
            const int height = 24;
            const int cityX = 16;
            const int cityY = 12;
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(width, height),
                CityMode.Fortress);

            fixture.Placement.SetBuildGridVisible(true);

            Mesh mesh = Slot(
                    fixture.Presentation,
                    "building.grid.ground")
                .GetComponent<MeshFilter>().sharedMesh;
            Assert.That(
                MeshGridEdges(mesh, width, height),
                Is.EquivalentTo(
                    ExpectedGridEdges(
                        cityX,
                        cityY,
                        BuildingRangeRules.InitialGroundRadius,
                        width,
                        height)),
                "IDEA-0008 ground grid must not imply that rejected " +
                "world cells are buildable.");
        }

        [TestCase(8)]
        [TestCase(12)]
        [TestCase(24)]
        public void IDEA0008_SupportedRadiusMeshesUseTheFormalRule(
            int radius)
        {
            const int width = 64;
            const int height = 48;
            const int cityX = 31;
            const int cityY = 23;
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(width, height),
                CityMode.Fortress);

            InvokeSetGroundBuildRange(
                fixture.Presentation,
                cityX,
                cityY,
                radius,
                width,
                height);

            Assert.That(
                MeshGridEdges(
                    Slot(fixture.Presentation, "building.grid.ground")
                        .GetComponent<MeshFilter>().sharedMesh,
                    width,
                    height),
                Is.EquivalentTo(
                    ExpectedGridEdges(
                        cityX,
                        cityY,
                        radius,
                        width,
                        height)));
            Assert.That(
                MeshGridEdges(
                    Slot(
                            fixture.Presentation,
                            "building.range.ground-boundary")
                        .GetComponent<MeshFilter>().sharedMesh,
                    width,
                    height),
                Is.EquivalentTo(
                    ExpectedBoundaryEdges(
                        cityX,
                        cityY,
                        radius,
                        width,
                        height)));
        }

        [Test]
        public void IDEA0008_NearWorldEdgeClipsBothMeshesToTheWorld()
        {
            const int width = 32;
            const int height = 24;
            const int cityX = 0;
            const int cityY = 0;
            const int radius = 8;
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(width, height),
                CityMode.Fortress);

            InvokeSetGroundBuildRange(
                fixture.Presentation,
                cityX,
                cityY,
                radius,
                width,
                height);

            Assert.That(
                MeshGridEdges(
                    Slot(fixture.Presentation, "building.grid.ground")
                        .GetComponent<MeshFilter>().sharedMesh,
                    width,
                    height),
                Is.EquivalentTo(
                    ExpectedGridEdges(
                        cityX,
                        cityY,
                        radius,
                        width,
                        height)));
            Assert.That(
                MeshGridEdges(
                    Slot(
                            fixture.Presentation,
                            "building.range.ground-boundary")
                        .GetComponent<MeshFilter>().sharedMesh,
                    width,
                    height),
                Is.EquivalentTo(
                    ExpectedBoundaryEdges(
                        cityX,
                        cityY,
                        radius,
                        width,
                        height)));
        }

        [Test]
        public void IDEA0008_RangeCachePreservesObjectsAndAllocatesZeroBytes()
        {
            WorldFixture fixture = CreateWorldFixture();
            fixture.Placement.SetBuildGridVisible(true);
            GrayboxVisualSlot ground = Slot(
                fixture.Presentation,
                "building.grid.ground");
            GrayboxVisualSlot boundary = Slot(
                fixture.Presentation,
                "building.range.ground-boundary");
            Mesh groundMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Mesh boundaryMesh = boundary.GetComponent<MeshFilter>().sharedMesh;
            Renderer groundRenderer = ground.Renderer;
            Renderer boundaryRenderer = boundary.Renderer;
            Material material = ground.Renderer.sharedMaterial;
            int infrastructureObjects = fixture.Presentation
                .transform.Find("infrastructure")
                .GetComponentsInChildren<Transform>(true).Length;

            fixture.Placement.UpdatePointer(ScreenCenter);
            fixture.Placement.UpdatePointer(ScreenCenter + Vector2.one);
            Assert.That(
                ground.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(groundMesh),
                "Pointer changes must not rebuild the range grid.");
            Assert.That(
                boundary.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(boundaryMesh),
                "Pointer changes must not rebuild the range boundary.");

            Action warmedSet = () =>
                fixture.Placement.SetBuildGridVisible(true);
            warmedSet();
            warmedSet();
            AllocationMeasurement measurement = Profile300Calls(warmedSet);
            TestContext.WriteLine(
                "IDEA0008RangeCacheProfilerBytes=" +
                measurement.ProfiledBytes);
            TestContext.WriteLine(
                "IDEA0008RangeCacheCurrentThreadBytes=" +
                measurement.CurrentThreadBytes);
            Assert.That(measurement.ProfiledBytes, Is.Zero);
            Assert.That(measurement.CurrentThreadBytes, Is.Zero);

            Assert.That(
                fixture.World.Coordinates.TryCellToWorld(
                    17,
                    12,
                    .5f,
                    out Vector3 movedCity),
                Is.True);
            fixture.City.transform.position = movedCity;
            fixture.Placement.SetBuildGridVisible(true);

            Assert.That(
                ground.GetComponent<MeshFilter>().sharedMesh,
                Is.Not.SameAs(groundMesh));
            Assert.That(
                boundary.GetComponent<MeshFilter>().sharedMesh,
                Is.Not.SameAs(boundaryMesh));
            Mesh movedGroundMesh =
                ground.GetComponent<MeshFilter>().sharedMesh;
            Mesh movedBoundaryMesh =
                boundary.GetComponent<MeshFilter>().sharedMesh;
            fixture.Placement.SetBuildGridVisible(true);
            Assert.That(
                ground.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(movedGroundMesh));
            Assert.That(
                boundary.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(movedBoundaryMesh));
            Assert.That(Slot(fixture.Presentation, ground.StableId),
                Is.SameAs(ground));
            Assert.That(Slot(fixture.Presentation, boundary.StableId),
                Is.SameAs(boundary));
            Assert.That(ground.Renderer, Is.SameAs(groundRenderer));
            Assert.That(boundary.Renderer, Is.SameAs(boundaryRenderer));
            Assert.That(ground.Renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(boundary.Renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(
                fixture.Presentation.transform.Find("infrastructure")
                    .GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(infrastructureObjects),
                "IDEA-0008 uses fixed combined visuals, not per-cell roots.");
            TestContext.WriteLine(
                "IDEA0008InfrastructureTransformCount=" +
                infrastructureObjects);
        }

        [Test]
        public void IDEA0008_UnmappableCityHidesOnlyExteriorRangeVisuals()
        {
            WorldFixture fixture = CreateWorldFixture();
            fixture.Placement.SetBuildGridVisible(true);
            fixture.City.transform.position = new Vector3(1000f, .5f, 1000f);

            fixture.Placement.SetBuildGridVisible(true);

            Assert.That(
                Slot(fixture.Presentation, "building.grid.ground")
                    .gameObject.activeSelf,
                Is.False);
            Assert.That(
                Slot(
                        fixture.Presentation,
                        "building.range.ground-boundary")
                    .gameObject.activeSelf,
                Is.False);
            Assert.That(
                Slot(fixture.Presentation, "building.grid.inner-city")
                    .gameObject.activeSelf,
                Is.True,
                "Inner-city grid keeps the existing build-mode behavior.");
        }

        [Test]
        public void WorldView_RequestedBuildGridStateSurvivesRehydrateAtZeroAllocation()
        {
            WorldFixture fixture = CreateWorldFixture();
            Transform infrastructure =
                fixture.Presentation.transform.Find("infrastructure");
            fixture.Placement.SetBuildGridVisible(true);

            fixture.Presentation.Configure(
                fixture.InstanceRoot,
                infrastructure,
                fixture.Material,
                fixture.City);

            Assert.That(
                fixture.Presentation.IsBuildGridVisible,
                Is.True);
            Assert.That(
                Slot(fixture.Presentation, "building.grid.ground")
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                Slot(
                        fixture.Presentation,
                        "building.range.ground-boundary")
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                Slot(fixture.Presentation, "building.grid.inner-city")
                    .gameObject.activeSelf,
                Is.True);

            Action warmedSet = () =>
                fixture.Presentation.SetBuildGridVisible(true);
            warmedSet();
            warmedSet();
            AllocationMeasurement measurement =
                Profile300Calls(warmedSet);

            TestContext.WriteLine(
                "BuildGridVisibilityProfilerSamples=" +
                measurement.Samples);
            TestContext.WriteLine(
                "BuildGridVisibilityProfilerBytes=" +
                measurement.ProfiledBytes);
            TestContext.WriteLine(
                "BuildGridVisibilityCurrentThreadBytes=" +
                measurement.CurrentThreadBytes);
            Assert.That(measurement.ProfiledBytes, Is.Zero);
            Assert.That(measurement.CurrentThreadBytes, Is.Zero);

            fixture.Placement.SetBuildGridVisible(false);
            fixture.Presentation.Configure(
                fixture.InstanceRoot,
                infrastructure,
                fixture.Material,
                fixture.City);

            Assert.That(
                fixture.Presentation.IsBuildGridVisible,
                Is.False);
            Assert.That(
                Slot(fixture.Presentation, "building.grid.ground")
                    .gameObject.activeSelf,
                Is.False);
            Assert.That(
                Slot(fixture.Presentation, "building.grid.inner-city")
                    .gameObject.activeSelf,
                Is.False);
        }

        [Test]
        public void WorldView_UsesStableGridPreviewAndNodeIdsWithSharedMaterial()
        {
            WorldFixture fixture = CreateWorldFixture();
            BuildingPlacementEvaluation valid = ValidEvaluation(
                fixture.Session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);
            BuildingSurfaceHit hit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                20,
                15,
                new Vector3(4f, 0f, 3f),
                "外城");
            fixture.Presentation.ShowPreview(
                BuildingCatalog.Wall,
                hit,
                BuildingOrientation.North,
                valid);
            fixture.Presentation.ShowCompatibleResourceNode(
                "world.resource-node.20.15",
                20,
                15,
                true);

            string[] ids = fixture.Presentation
                .GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Select(value => value.StableId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Assert.That(ids, Does.Contain("building.grid.ground"));
            Assert.That(
                ids,
                Does.Contain("building.range.ground-boundary"));
            Assert.That(ids, Does.Contain("building.grid.inner-city"));
            Assert.That(ids, Does.Contain("building.preview.core.building.wall"));
            Assert.That(
                ids,
                Does.Contain(
                    "building.node-highlight.world.resource-node.20.15"));
            foreach (Renderer renderer in fixture.Presentation
                         .GetComponentsInChildren<Renderer>(true))
                Assert.That(
                    renderer.sharedMaterial,
                    Is.SameAs(fixture.Material));
            Assert.That(
                fixture.Presentation.InfrastructureRendererCount,
                Is.LessThanOrEqualTo(8));
            Assert.That(
                fixture.Presentation
                    .transform.Find("infrastructure")
                    .GetComponentsInChildren<Transform>(true)
                    .Length,
                Is.LessThan(20));
        }

        [Test]
        public void BUG0006_SquarePreviewShowsAllFourDirectionsWithOneStableVisual()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            var hit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                20,
                15,
                new Vector3(4f, 0f, 3f),
                "外城");
            BuildingPlacementEvaluation evaluation = ValidEvaluation(
                fixture.Session,
                BuildingCatalog.Housing,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);
            GameObject stableRoot = null;
            Mesh stableMesh = null;

            foreach (BuildingOrientation orientation in new[]
                     {
                         BuildingOrientation.North,
                         BuildingOrientation.East,
                         BuildingOrientation.South,
                         BuildingOrientation.West
                     })
            {
                fixture.Presentation.ShowPreview(
                    BuildingCatalog.Housing,
                    hit,
                    orientation,
                    evaluation);
                GrayboxVisualSlot preview = Slot(
                    fixture.Presentation,
                    "building.preview." + BuildingCatalog.Housing.Id.Value);
                if (stableRoot == null)
                {
                    stableRoot = preview.gameObject;
                    stableMesh = preview.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(
                        stableMesh.vertexCount,
                        Is.GreaterThan(24),
                        "BUG-0006 preview must include a visible direction marker, not only one cube.");
                }
                else
                {
                    Assert.That(preview.gameObject, Is.SameAs(stableRoot));
                    Assert.That(
                        preview.GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(stableMesh));
                }

                Assert.That(
                    Mathf.DeltaAngle(
                        preview.transform.eulerAngles.y,
                        (int)orientation * 90f),
                    Is.EqualTo(0f).Within(.01f),
                    orientation.ToString());
            }
        }

        [Test]
        public void BUG0006_RectangularRotationKeepsAnchorAndUpdatesFootprintAndYaw()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            fixture.Session.SetRouteContact(
                WasteCity.Content.ContentRoute.BiologicalAscension,
                true);
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.BehemothPen.RequiredResearchId);
            fixture.Session.Inventory.Set(
                BuildingCatalog.BehemothPen.CostId,
                BuildingCatalog.BehemothPen.Cost);
            fixture.Interaction.Select(BuildingCatalog.BehemothPen);
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            int anchorX = fixture.Placement.CurrentHit.X;
            int anchorY = fixture.Placement.CurrentHit.Y;
            GrayboxVisualSlot preview = Slot(
                fixture.Presentation,
                "building.preview." + BuildingCatalog.BehemothPen.Id.Value);
            GameObject previewRoot = preview.gameObject;

            fixture.Interaction.RotateClockwise();
            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(fixture.Placement.CurrentHit.X, Is.EqualTo(anchorX));
            Assert.That(fixture.Placement.CurrentHit.Y, Is.EqualTo(anchorY));
            Assert.That(
                fixture.Placement.CurrentEvaluation.RotatedWidth,
                Is.EqualTo(2));
            Assert.That(
                fixture.Placement.CurrentEvaluation.RotatedHeight,
                Is.EqualTo(3));
            GrayboxVisualSlot rotated = Slot(
                fixture.Presentation,
                "building.preview." + BuildingCatalog.BehemothPen.Id.Value);
            Assert.That(rotated.gameObject, Is.SameAs(previewRoot));
            Assert.That(
                Mathf.DeltaAngle(rotated.transform.eulerAngles.y, 90f),
                Is.EqualTo(0f).Within(.01f));
            Physics.SyncTransforms();
            Bounds previewBounds = rotated.Renderer.bounds;
            Assert.That(previewBounds.size.x, Is.EqualTo(1.92f).Within(.02f));
            Assert.That(previewBounds.size.z, Is.EqualTo(2.92f).Within(.02f));

            var rectangular = new BuildingDefinition(
                "test.building.bug0006-three-by-two",
                "方向测试建筑",
                3,
                2,
                ResourceIds.Stone,
                1);
            fixture.Session.Inventory.Set(ResourceIds.Stone, 1);
            GrayboxBuildingInstance3D instance = Begin(
                fixture,
                rectangular,
                BuildingSite.Ground,
                10,
                10,
                CityMode.Fortress,
                BuildingOrientation.East);
            Transform instanceRoot = fixture.InstanceRoot
                .Cast<Transform>()
                .Single(value => value.name == instance.StableInstanceId);
            Physics.SyncTransforms();
            Bounds instanceBounds =
                instanceRoot.GetComponent<MeshRenderer>().bounds;
            Assert.That(instanceBounds.size.x, Is.EqualTo(1.92f).Within(.02f));
            Assert.That(instanceBounds.size.z, Is.EqualTo(2.92f).Within(.02f));
            Assert.That(
                Mathf.DeltaAngle(instanceRoot.eulerAngles.y, 90f),
                Is.EqualTo(0f).Within(.01f));
        }

        [Test]
        public void BUG0006_ValidPreviewStatusNamesBuildingDirectionFootprintAndRotateKey()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.BehemothPen);
            fixture.Interaction.RotateClockwise();
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            GrayboxBuildingMenuView3D menu =
                CreatePlacementStatusMenu(fixture);

            typeof(GrayboxBuildingMenuView3D)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(menu, null);

            Text status = PlacementStatusText(menu);
            Assert.That(status.gameObject.activeInHierarchy, Is.True);
            Assert.That(status.text, Does.Contain("巨兽栏"));
            Assert.That(status.text, Does.Contain("方向 东"));
            Assert.That(status.text, Does.Contain("占地 2×3"));
            Assert.That(status.text, Does.Contain("R"));
        }

        [Test]
        public void WorldView_ExpandedGroundPreviewAlignsCornerCellCenters()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(
                    GrayboxWorldLayout3D.WorldWidth,
                    GrayboxWorldLayout3D.WorldHeight),
                CityMode.Fortress);
            var mapper = new PlanarCoordinateMapper3D(64, 48);
            var corners = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(63, 47)
            };
            foreach (Vector2Int corner in corners)
            {
                Assert.That(
                    mapper.TryCellToWorld(
                        corner.x,
                        corner.y,
                        0f,
                        out Vector3 expected),
                    Is.True);
                var hit = new BuildingSurfaceHit(
                    true,
                    BuildingSite.Ground,
                    corner.x,
                    corner.y,
                    expected,
                    "外城");
                fixture.Presentation.ShowPreview(
                    BuildingCatalog.Wall,
                    hit,
                    BuildingOrientation.North,
                    default);
                GrayboxVisualSlot preview = Slot(
                    fixture.Presentation,
                    "building.preview.core.building.wall");
                Assert.That(
                    preview.transform.position,
                    Is.EqualTo(expected + Vector3.up * .06f));
            }

            Assert.That(
                fixture.Presentation.InfrastructureRendererCount,
                Is.LessThanOrEqualTo(8));
            Assert.That(
                fixture.Presentation.transform.Find("infrastructure")
                    .GetComponentsInChildren<Transform>(true).Length,
                Is.LessThan(20));
        }

        [Test]
        public void WorldView_ExplicitReconfigureReplacesOnlyOwnedVisuals()
        {
            WorldFixture fixture = CreateWorldFixture();
            Transform infrastructureRoot =
                fixture.Presentation.transform.Find("infrastructure");
            Transform oldGround = Slot(
                fixture.Presentation,
                "building.grid.ground").transform;
            Mesh unrelatedMesh = Track(CreateSentinelMesh());
            Transform instanceSentinel = AddMeshSentinel(
                fixture.InstanceRoot,
                "unrelated-instance-sentinel",
                unrelatedMesh);
            Transform infrastructureSentinel = AddMeshSentinel(
                infrastructureRoot,
                "unrelated-infrastructure-sentinel",
                unrelatedMesh);

            fixture.Presentation.Configure(
                fixture.InstanceRoot,
                infrastructureRoot,
                fixture.Material,
                fixture.City);

            Assert.That(oldGround == null, Is.True);
            Assert.That(instanceSentinel != null, Is.True);
            Assert.That(infrastructureSentinel != null, Is.True);
            Assert.That(
                instanceSentinel.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(unrelatedMesh));
            Assert.That(
                infrastructureSentinel.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(unrelatedMesh));
            Assert.That(
                infrastructureRoot
                    .GetComponentsInChildren<GrayboxVisualSlot>(true)
                    .Count(
                        value => value.StableId ==
                        "building.grid.ground"),
                Is.EqualTo(1));
            Assert.That(
                infrastructureRoot
                    .GetComponentsInChildren<GrayboxVisualSlot>(true)
                    .Count(
                        value => value.StableId ==
                        "building.range.ground-boundary"),
                Is.EqualTo(1));
            Assert.That(
                infrastructureRoot
                    .GetComponentsInChildren<GrayboxVisualSlot>(true)
                    .Count(
                        value => value.StableId ==
                        "building.grid.inner-city"),
                Is.EqualTo(1));
            Assert.That(
                fixture.Presentation.InfrastructureRendererCount,
                Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator WorldView_ActiveSerializedCloneRehydratesOwnedGridsAndFollowsCity()
        {
            yield return new EnterPlayMode();
            GameObject source = CreatePrefabLikePresentationRoot(
                out Material material,
                out Mesh unrelatedMesh);
            GameObject clone = Track(
                UnityEngine.Object.Instantiate(source));
            clone.name = "serialized-presentation-clone";
            yield return null;

            GrayboxBuildingWorldView3D view =
                clone.GetComponentInChildren<GrayboxBuildingWorldView3D>();
            GrayboxMobileCityController3D city =
                clone.GetComponentInChildren<GrayboxMobileCityController3D>();
            Transform infrastructureRoot =
                view.transform.Find("infrastructure");
            Transform instanceRoot = view.transform.Find("instances");
            GrayboxVisualSlot[] slots =
                infrastructureRoot
                    .GetComponentsInChildren<GrayboxVisualSlot>(true);
            GrayboxVisualSlot ground = slots.Single(
                value => value.StableId == "building.grid.ground");
            GrayboxVisualSlot boundary = slots.Single(
                value => value.StableId ==
                    "building.range.ground-boundary");
            GrayboxVisualSlot inner = slots.Single(
                value => value.StableId == "building.grid.inner-city");

            Assert.That(
                slots.Count(
                    value => value.StableId ==
                    "building.grid.ground"),
                Is.EqualTo(1));
            Assert.That(
                slots.Count(
                    value => value.StableId ==
                    "building.range.ground-boundary"),
                Is.EqualTo(1));
            Assert.That(
                slots.Count(
                    value => value.StableId ==
                    "building.grid.inner-city"),
                Is.EqualTo(1));
            Assert.That(view.InfrastructureRendererCount, Is.EqualTo(3));
            Assert.That(instanceRoot, Is.Not.Null);
            Transform instanceSentinel =
                instanceRoot.Find("unrelated-instance-sentinel");
            Transform infrastructureSentinel =
                infrastructureRoot.Find(
                    "unrelated-infrastructure-sentinel");
            Assert.That(instanceSentinel != null, Is.True);
            Assert.That(infrastructureSentinel != null, Is.True);
            Assert.That(
                instanceSentinel.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(unrelatedMesh));
            Assert.That(
                infrastructureSentinel.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(unrelatedMesh));
            Assert.That(
                ground.Renderer.sharedMaterial,
                Is.SameAs(material));
            Assert.That(
                boundary.Renderer.sharedMaterial,
                Is.SameAs(material));
            Assert.That(
                inner.Renderer.sharedMaterial,
                Is.SameAs(material));

            Vector3 innerBefore = inner.transform.position;
            Vector3 motion = new Vector3(3f, 0f, 2f);
            city.transform.position += motion;
            yield return null;

            Assert.That(
                inner.transform.position,
                Is.EqualTo(innerBefore + motion));

            view.ShowCompatibleResourceNode(
                "world.resource-node.2.3",
                2,
                3,
                true);
            GrayboxVisualSlot node = infrastructureRoot
                .GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Single(
                    value => value.StableId ==
                    "building.node-highlight.world.resource-node.2.3");
            Assert.That(
                node.Renderer.sharedMaterial,
                Is.SameAs(material));
            Assert.That(view.InfrastructureRendererCount, Is.EqualTo(4));
            view.ShowCompatibleResourceNode(
                "world.resource-node.2.3",
                2,
                3,
                false);
            Assert.That(view.InfrastructureRendererCount, Is.EqualTo(4));

            UnityEngine.Object.Destroy(clone);
            UnityEngine.Object.Destroy(source);
            yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WorldView_ActiveSerializedClonePreservesSourceGeneratedMesh()
        {
            yield return new EnterPlayMode();
            GameObject source = CreatePrefabLikePresentationRoot(
                out _,
                out _);
            GrayboxBuildingWorldView3D sourceView =
                source.GetComponentInChildren<GrayboxBuildingWorldView3D>();
            sourceView.SetGroundBuildRange(16, 12, 8, 32, 24);
            GrayboxVisualSlot sourceGround = Slot(
                sourceView,
                "building.grid.ground");
            Mesh sourceGroundMesh =
                sourceGround.GetComponent<MeshFilter>().sharedMesh;

            GameObject clone = Track(
                UnityEngine.Object.Instantiate(source));
            yield return null;

            Assert.That(sourceGroundMesh != null, Is.True);
            Assert.That(
                sourceGround.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(sourceGroundMesh));
            Assert.That(sourceGroundMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(
                clone.GetComponentInChildren<GrayboxBuildingWorldView3D>()
                    .GetComponentsInChildren<GrayboxVisualSlot>(true)
                    .Count(
                        value => value.StableId ==
                            "building.grid.ground"),
                Is.EqualTo(1));

            UnityEngine.Object.Destroy(clone);
            UnityEngine.Object.Destroy(source);
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void WorldView_PreviewUpdatesSameRendererThroughGreenAndOrderedRed()
        {
            WorldFixture fixture = CreateWorldFixture();
            BuildingSurfaceHit hit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                20,
                15,
                new Vector3(4f, 0f, 3f),
                "外城");
            BuildingPlacementEvaluation valid = ValidEvaluation(
                fixture.Session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);
            fixture.Presentation.ShowPreview(
                BuildingCatalog.Wall,
                hit,
                BuildingOrientation.North,
                valid);
            GrayboxVisualSlot preview = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            Renderer renderer = preview.Renderer;
            Color green = PropertyColor(renderer);

            BuildingPlacementEvaluation invalid = BuildingPlacementRules.Evaluate(
                Request(
                    fixture.Session,
                    BuildingCatalog.Wall,
                    BuildingSite.Ground,
                    20,
                    15,
                    CityMode.Fortress,
                    terrainPassable: false,
                    obstacleFree: false));
            fixture.Presentation.ShowPreview(
                BuildingCatalog.Wall,
                hit,
                BuildingOrientation.North,
                invalid);
            GrayboxVisualSlot redPreview = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            Color red = PropertyColor(redPreview.Renderer);

            Assert.That(redPreview.Renderer, Is.SameAs(renderer));
            Assert.That(green.g, Is.GreaterThan(green.r));
            Assert.That(red.r, Is.GreaterThan(red.g));
            Assert.That(red, Is.Not.EqualTo(green));
            Assert.That(
                invalid.Failures,
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.InvalidTerrain,
                    BuildingPlacementFailure.Obstacle
                }));
        }

        [Test]
        public void Placement_RepeatedUpdatePointerKeepsPreviewMeshAndSlotIdentity()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.Wall);
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            GrayboxVisualSlot slot = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            GameObject root = slot.gameObject;
            MeshFilter filter = root.GetComponent<MeshFilter>();
            Mesh mesh = filter.sharedMesh;

            fixture.Placement.UpdatePointer(ScreenCenter);

            GrayboxVisualSlot refreshed = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            Assert.That(refreshed.gameObject, Is.SameAs(root));
            Assert.That(
                refreshed.GetComponent<MeshFilter>(),
                Is.SameAs(filter));
            Assert.That(filter.sharedMesh, Is.SameAs(mesh));
            Assert.That(refreshed, Is.SameAs(slot));
            Assert.That(
                root.GetComponents<GrayboxVisualSlot>()
                    .Count(
                        value => value.StableId ==
                        "building.preview.core.building.wall"),
                Is.EqualTo(1));
        }

        [Test]
        public void PlacementController_OwnsOneWorkspaceAndCachedRuleDelegates()
        {
            Type workspaceType =
                typeof(BuildingPlacementRules).Assembly.GetType(
                    "WasteCity.Building." +
                    "BuildingPlacementEvaluationWorkspace");
            Assert.That(workspaceType, Is.Not.Null);
            FieldInfo[] fields =
                typeof(GrayboxBuildingPlacementController3D).GetFields(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                fields.Count(field =>
                    field.FieldType == workspaceType),
                Is.EqualTo(2));
            Assert.That(
                fields.Count(field =>
                    field.FieldType == typeof(Func<string, bool>)),
                Is.EqualTo(1));
            Assert.That(
                fields.Count(field =>
                    field.FieldType == typeof(Func<string, int>)),
                Is.EqualTo(2));
        }

        [Test]
        public void PlacementController_ConfigureStartsNewWorkspaceAndPreservesOldEvaluation()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            BuildingPlacementEvaluation oldEvaluation =
                fixture.Placement.CurrentEvaluation;
            BuildingPlacementFailure[] oldFailures =
                oldEvaluation.Failures.ToArray();
            Vector2Int[] oldFootprint =
                FootprintSnapshot(oldEvaluation);
            object oldWorkspace =
                PlacementWorkspace(fixture.Placement);
            Assert.That(
                oldFailures,
                Does.Contain(
                    BuildingPlacementFailure.IncompatibleResourceNode));
            Assert.That(oldFootprint.Length, Is.EqualTo(4));

            fixture.Placement.Configure(
                fixture.Session,
                fixture.City,
                fixture.World,
                PlacementProjector(fixture.Placement),
                fixture.Presentation,
                fixture.Interaction);

            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Is.Null);
            Assert.That(
                fixture.Placement.CurrentEvaluation.Footprint,
                Is.Null);
            Assert.That(
                fixture.Placement.CurrentHit.IsValid,
                Is.False);
            Assert.That(
                PlacementWorkspace(fixture.Placement),
                Is.Not.SameAs(oldWorkspace));

            fixture.Interaction.Select(BuildingCatalog.Wall);
            PositionCameraAtCell(fixture, 21, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(
                oldEvaluation.Failures,
                Is.EqualTo(oldFailures));
            Assert.That(
                FootprintSnapshot(oldEvaluation),
                Is.EqualTo(oldFootprint));
            Assert.That(
                fixture.Placement.CurrentEvaluation.Footprint.Count,
                Is.EqualTo(1));
        }

        [TestCase("OnDisable")]
        [TestCase("OnDestroy")]
        public void PlacementController_TeardownDiscardsWorkspaceResultAndPreviewIdempotently(
            string lifecycleMethod)
        {
            WorldCell[,] cells = OpenCells();
            cells[20, 15] = Cell(ResourceIds.Iron);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);
            InvokeRefreshMiningGuidance(fixture.Placement);
            object oldWorkspace =
                PlacementWorkspace(fixture.Placement);
            GrayboxVisualSlot preview = Slot(
                fixture.Presentation,
                "building.preview.core.building.mining-station");
            Assert.That(preview.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.node-highlight."),
                Is.Not.Empty);
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.anchor-highlight."),
                Is.Not.Empty);

            InvokePlacementLifecycle(
                fixture.Placement,
                lifecycleMethod);
            InvokePlacementLifecycle(
                fixture.Placement,
                lifecycleMethod);

            Assert.That(
                PlacementWorkspace(fixture.Placement),
                Is.Null);
            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Is.Null);
            Assert.That(
                fixture.Placement.CurrentEvaluation.Footprint,
                Is.Null);
            Assert.That(
                fixture.Placement.CurrentHit.IsValid,
                Is.False);
            Assert.That(preview.gameObject.activeInHierarchy, Is.False);
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.node-highlight."),
                Is.Empty,
                "IDEA-0010 guidance must hide during teardown.");
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.anchor-highlight."),
                Is.Empty,
                "IDEA-0010 guidance must hide during teardown.");

            fixture.Placement.Configure(
                fixture.Session,
                fixture.City,
                fixture.World,
                PlacementProjector(fixture.Placement),
                fixture.Presentation,
                fixture.Interaction);

            Assert.That(
                PlacementWorkspace(fixture.Placement),
                Is.Not.Null.And.Not.SameAs(oldWorkspace));
        }

        [TestCase(GrayboxBuildingInteractionState.CatalogOpen)]
        [TestCase(GrayboxBuildingInteractionState.CancelConfirmation)]
        public void IDEA0010_MiningGuidanceRemainsVisibleDuringBuildingFlowModalState(
            GrayboxBuildingInteractionState modalState)
        {
            WorldCell[,] cells = OpenCells();
            cells[20, 15] = Cell(ResourceIds.Iron);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            InvokeRefreshMiningGuidance(fixture.Placement);

            if (modalState == GrayboxBuildingInteractionState.CatalogOpen)
                fixture.Interaction.ToggleCatalog();
            else
                fixture.Interaction.RequestCancelConstruction();
            InvokeRefreshMiningGuidance(fixture.Placement);

            Assert.That(fixture.Interaction.State, Is.EqualTo(modalState));
            Assert.That(
                fixture.Interaction.Selected,
                Is.SameAs(BuildingCatalog.MiningStation));
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.node-highlight."),
                Is.Not.Empty,
                "Both states remain inside the selected MiningStation " +
                "building flow; only closing it hides guidance.");
            Assert.That(
                ActiveGuidanceSlots(
                    fixture.Presentation,
                    "building.anchor-highlight."),
                Is.Not.Empty);
        }

        [Test]
        public void
            PlacementController_OnEnableStartsNewWorkspaceWithoutConfigureAndPreservesOldEvaluation()
        {
            WorldCell[,] cells = OpenCells();
            cells[19, 15] = Cell(ResourceIds.Iron);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress);
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.Smelter.RequiredResearchId);
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.Assembler.RequiredResearchId);
            Begin(
                fixture,
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                12,
                15,
                CityMode.Fortress);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Assert.That(
                fixture.Session.CompletedBuildingCount(
                    BuildingCatalog.Smelter.Id.Value),
                Is.EqualTo(1));
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 18, 14);
            fixture.Placement.UpdatePointer(ScreenCenter);
            BuildingPlacementEvaluation oldEvaluation =
                fixture.Placement.CurrentEvaluation;
            BuildingPlacementFailure[] oldFailures =
                oldEvaluation.Failures.ToArray();
            Vector2Int[] oldFootprint =
                FootprintSnapshot(oldEvaluation);
            object oldWorkspace =
                PlacementWorkspace(fixture.Placement);
            Assert.That(oldFootprint.Length, Is.EqualTo(4));
            Assert.That(
                oldFailures,
                Is.Empty);
            Assert.That(
                oldEvaluation.CompatibleResourceNodeId,
                Is.EqualTo("world.resource-node.19.15"));

            InvokePlacementLifecycle(
                fixture.Placement,
                "OnDisable");
            ClearTransientPlacementCaches(fixture.Placement);
            InvokePlacementLifecycle(
                fixture.Placement,
                "OnEnable");

            Assert.That(
                PlacementWorkspace(fixture.Placement),
                Is.Not.Null.And.Not.SameAs(oldWorkspace));
            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Is.Null);
            Assert.That(
                fixture.Placement.CurrentEvaluation.Footprint,
                Is.Null);
            Assert.That(
                fixture.Placement.CurrentHit.IsValid,
                Is.False);

            fixture.Interaction.Select(BuildingCatalog.Assembler);
            PositionCameraAtCell(fixture, 20, 15);
            fixture.Placement.UpdatePointer(ScreenCenter);

            Assert.That(
                fixture.Placement.CurrentEvaluation.IsValid,
                Is.True,
                fixture.Placement.CurrentEvaluation
                    .PrimaryFailure.ToString());
            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Is.Empty);

            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(fixture, 18, 14);
            fixture.Placement.UpdatePointer(ScreenCenter);
            string firstNodeId =
                fixture.Placement.CurrentEvaluation
                    .CompatibleResourceNodeId;
            fixture.Placement.UpdatePointer(ScreenCenter);
            string secondNodeId =
                fixture.Placement.CurrentEvaluation
                    .CompatibleResourceNodeId;

            Assert.That(
                firstNodeId,
                Is.EqualTo("world.resource-node.19.15"));
            Assert.That(secondNodeId, Is.SameAs(firstNodeId));
            Assert.That(
                oldEvaluation.Failures,
                Is.EqualTo(oldFailures));
            Assert.That(
                FootprintSnapshot(oldEvaluation),
                Is.EqualTo(oldFootprint));
        }

        [TestCase(
            ResourceIds.Iron,
            35,
            27,
            "world.resource-node.35.27",
            3f,
            3f)]
        [TestCase(
            ResourceIds.EnergyCrystal,
            34,
            26,
            "world.resource-node.34.26",
            2f,
            2f)]
        public void
            PlacementController_SceneEnableBeforeWorldGenerateLazilyBuildsStableNodeIdentity(
                string resourceId,
                int nodeX,
                int nodeY,
                string expectedNodeId,
                float expectedHighlightX,
                float expectedHighlightZ)
        {
            WorldCell[,] cells = OpenCells(
                GrayboxWorldLayout3D.WorldWidth,
                GrayboxWorldLayout3D.WorldHeight);
            cells[nodeX, nodeY] = Cell(resourceId);
            WorldFixture fixture = CreateWorldFixture(
                cells,
                CityMode.Fortress,
                true);
            fixture.Interaction.Select(BuildingCatalog.MiningStation);
            PositionCameraAtCell(
                fixture,
                GrayboxWorldLayout3D.ToExpandedX(18),
                GrayboxWorldLayout3D.ToExpandedY(14));

            fixture.Placement.UpdatePointer(ScreenCenter);

            string firstNodeId =
                fixture.Placement.CurrentEvaluation
                    .CompatibleResourceNodeId;
            Assert.That(
                fixture.Placement.CurrentEvaluation.IsValid,
                Is.True,
                fixture.Placement.CurrentEvaluation
                    .PrimaryFailure.ToString());
            Assert.That(firstNodeId, Is.EqualTo(expectedNodeId));
            GrayboxVisualSlot[] highlights =
                NodeSlots(fixture.Presentation).ToArray();
            Assert.That(highlights, Has.Length.EqualTo(1));
            GrayboxVisualSlot highlight = highlights[0];
            Assert.That(
                highlight.StableId,
                Is.EqualTo(
                    "building.node-highlight." +
                    expectedNodeId));
            Assert.That(
                highlight.transform.position,
                Is.EqualTo(new Vector3(
                    expectedHighlightX,
                    .035f,
                    expectedHighlightZ))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));

            fixture.Placement.UpdatePointer(ScreenCenter);
            string secondNodeId =
                fixture.Placement.CurrentEvaluation
                    .CompatibleResourceNodeId;
            Assert.That(secondNodeId, Is.SameAs(firstNodeId));

            Action warmedUpdate = () =>
                fixture.Placement.UpdatePointer(ScreenCenter);
            warmedUpdate();
            warmedUpdate();
            AllocationMeasurement measurement =
                Profile300Calls(warmedUpdate);

            TestContext.WriteLine(
                "SceneOrderNodeIdentityProfilerSamples=" +
                measurement.Samples);
            TestContext.WriteLine(
                "SceneOrderNodeIdentityProfilerBytes=" +
                measurement.ProfiledBytes);
            TestContext.WriteLine(
                "SceneOrderNodeIdentityCurrentThreadBytes=" +
                measurement.CurrentThreadBytes);
            Assert.That(
                fixture.Placement.CurrentEvaluation
                    .CompatibleResourceNodeId,
                Is.SameAs(firstNodeId));
            Assert.That(measurement.ProfiledBytes, Is.Zero);
            Assert.That(measurement.CurrentThreadBytes, Is.Zero);
        }

        [Test]
        public void WorldView_DoesNotRetainDynamicEvaluationOrColorVerdicts()
        {
            FieldInfo[] fields =
                typeof(GrayboxBuildingWorldView3D).GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            Assert.That(
                fields.Where(field =>
                    field.FieldType ==
                        typeof(BuildingPlacementEvaluation) ||
                    field.FieldType ==
                        typeof(BuildingPlacementFailure) ||
                    field.FieldType == typeof(Color)),
                Is.Empty);
            Assert.That(
                fields.Where(field =>
                    field.Name.IndexOf(
                        "legality",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    field.Name.IndexOf(
                        "validity",
                        StringComparison.OrdinalIgnoreCase) >= 0),
                Is.Empty);
        }

        [Test]
        public void WorldView_SameGeometryRefreshChangesOnlyTransformAndPropertyBlock()
        {
            WorldFixture fixture = CreateWorldFixture();
            BuildingSurfaceHit firstHit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                20,
                15,
                new Vector3(4f, 0f, 3f),
                "外城");
            BuildingPlacementEvaluation valid = ValidEvaluation(
                fixture.Session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);
            fixture.Presentation.ShowPreview(
                BuildingCatalog.Wall,
                firstHit,
                BuildingOrientation.North,
                valid);
            GrayboxVisualSlot slot = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            GameObject root = slot.gameObject;
            MeshFilter filter = root.GetComponent<MeshFilter>();
            Mesh mesh = filter.sharedMesh;
            Vector3 position = root.transform.position;
            Color green = PropertyColor(slot.Renderer);
            BuildingPlacementEvaluation invalid =
                BuildingPlacementRules.Evaluate(
                    Request(
                        fixture.Session,
                        BuildingCatalog.Wall,
                        BuildingSite.Ground,
                        21,
                        15,
                        CityMode.Fortress,
                        terrainPassable: false));
            BuildingSurfaceHit secondHit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                21,
                15,
                new Vector3(5f, 0f, 3f),
                "外城");

            fixture.Presentation.ShowPreview(
                BuildingCatalog.Wall,
                secondHit,
                BuildingOrientation.North,
                invalid);

            GrayboxVisualSlot refreshed = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            Color red = PropertyColor(refreshed.Renderer);
            Assert.That(refreshed.gameObject, Is.SameAs(root));
            Assert.That(
                refreshed.GetComponent<MeshFilter>(),
                Is.SameAs(filter));
            Assert.That(filter.sharedMesh, Is.SameAs(mesh));
            Assert.That(refreshed, Is.SameAs(slot));
            Assert.That(root.transform.position, Is.Not.EqualTo(position));
            Assert.That(red, Is.Not.EqualTo(green));
            Assert.That(red.r, Is.GreaterThan(red.g));
            Assert.That(
                root.GetComponents<GrayboxVisualSlot>()
                    .Count(
                        value => value.StableId ==
                        "building.preview.core.building.wall"),
                Is.EqualTo(1));
        }

        [Test]
        public void WorldView_GeometryChangeReusesSlotAndReleasesOldMesh()
        {
            WorldFixture fixture = CreateWorldFixture();
            BuildingSurfaceHit hit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                20,
                15,
                new Vector3(4f, 0f, 3f),
                "外城");
            fixture.Presentation.ShowPreview(
                BuildingCatalog.Wall,
                hit,
                BuildingOrientation.North,
                ValidEvaluation(
                    fixture.Session,
                    BuildingCatalog.Wall,
                    BuildingSite.Ground,
                    20,
                    15,
                    CityMode.Fortress));
            GrayboxVisualSlot slot = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            MeshFilter filter = slot.GetComponent<MeshFilter>();
            Mesh oldMesh = filter.sharedMesh;

            fixture.Presentation.ShowPreview(
                BuildingCatalog.Housing,
                hit,
                BuildingOrientation.North,
                ValidEvaluation(
                    fixture.Session,
                    BuildingCatalog.Housing,
                    BuildingSite.Ground,
                    20,
                    15,
                    CityMode.Fortress));

            GrayboxVisualSlot refreshed = Slot(
                fixture.Presentation,
                "building.preview.core.building.housing");
            Assert.That(refreshed, Is.SameAs(slot));
            Assert.That(
                refreshed.GetComponent<MeshFilter>(),
                Is.SameAs(filter));
            Assert.That(filter.sharedMesh, Is.Not.SameAs(oldMesh));
            Assert.That(oldMesh == null, Is.True);
            Assert.That(
                refreshed.gameObject
                    .GetComponents<GrayboxVisualSlot>().Length,
                Is.EqualTo(1));
        }

        [Test]
        public void IDEA0019_InnerAndGroundHousingShareProfileProportions()
        {
            WorldFixture ground = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            GrayboxBuildingInstance3D groundInstance = Begin(
                ground,
                BuildingCatalog.Housing,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);
            ground.Session.CompleteAllConstructionForDevelopment(
                ground.Presentation);
            Bounds groundBounds = InstanceBounds(ground, groundInstance);

            WorldFixture inner = CreateWorldFixture(
                OpenCells(),
                CityMode.Mobile);
            GrayboxBuildingInstance3D innerInstance = Begin(
                inner,
                BuildingCatalog.Housing,
                BuildingSite.InnerCity,
                0,
                0,
                CityMode.Mobile);
            inner.Session.CompleteAllConstructionForDevelopment(
                inner.Presentation);
            Bounds innerBounds = InstanceBounds(inner, innerInstance);
            FormalBuildingVisualMetrics3D metrics = BuildingMetrics(
                BuildingCatalog.Housing);

            float expectedGroundWidth =
                BuildingCatalog.Housing.Width * metrics.FootprintFillRatio;
            float expectedGroundHeight = metrics.VisualHeightInCells;
            float expectedInnerWidth = expectedGroundWidth * .32f;
            float expectedInnerHeight = expectedGroundHeight * .32f * 1.15f;
            Assert.That(
                groundBounds.size.x,
                Is.EqualTo(expectedGroundWidth).Within(.02f));
            Assert.That(
                groundBounds.size.z,
                Is.EqualTo(expectedGroundWidth).Within(.02f));
            Assert.That(
                groundBounds.size.y,
                Is.EqualTo(expectedGroundHeight).Within(.02f));
            Assert.That(
                innerBounds.size.x,
                Is.EqualTo(expectedInnerWidth).Within(.02f));
            Assert.That(
                innerBounds.size.z,
                Is.EqualTo(expectedInnerWidth).Within(.02f));
            Assert.That(
                innerBounds.size.y,
                Is.EqualTo(expectedInnerHeight).Within(.02f));
            Assert.That(
                innerBounds.size.y / groundBounds.size.y,
                Is.EqualTo(.32f * 1.15f).Within(.02f));
        }

        [Test]
        public void IDEA0019_CanonicalArchetypesProduceDistinctWorldBounds()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            PrepareFormalVisualFixture(fixture);
            var definitions = new[]
            {
                BuildingCatalog.Housing,
                BuildingCatalog.Warehouse,
                BuildingCatalog.Smelter,
                BuildingCatalog.HeavyMachineGunTurret,
                BuildingCatalog.BehemothPen,
            };
            var anchors = new[]
            {
                new Vector2Int(10, 9),
                new Vector2Int(13, 9),
                new Vector2Int(18, 9),
                new Vector2Int(12, 12),
                new Vector2Int(18, 12),
            };
            var instances = new GrayboxBuildingInstance3D[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                instances[index] = Begin(
                    fixture,
                    definitions[index],
                    BuildingSite.Ground,
                    anchors[index].x,
                    anchors[index].y,
                    CityMode.Fortress);
            }
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);

            var heights = new HashSet<int>();
            for (var index = 0; index < definitions.Length; index++)
            {
                BuildingDefinition definition = definitions[index];
                Bounds bounds = InstanceBounds(fixture, instances[index]);
                FormalBuildingVisualMetrics3D metrics =
                    BuildingMetrics(definition);
                Assert.That(
                    bounds.size.x,
                    Is.EqualTo(
                        definition.Width * metrics.FootprintFillRatio)
                        .Within(.02f),
                    definition.Name);
                Assert.That(
                    bounds.size.z,
                    Is.EqualTo(
                        definition.Height * metrics.FootprintFillRatio)
                        .Within(.02f),
                    definition.Name);
                Assert.That(
                    bounds.size.y,
                    Is.EqualTo(metrics.VisualHeightInCells).Within(.02f),
                    definition.Name);
                heights.Add(Mathf.RoundToInt(bounds.size.y * 100f));
            }
            Assert.That(
                heights,
                Has.Count.EqualTo(definitions.Length),
                "Residential, storage, processor, tower, and large " +
                "archetypes require distinct heights.");
        }

        [Test]
        public void IDEA0019_FormalDefenseTowersUseOnlyLowBuildingFoundation()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            PrepareFormalVisualFixture(fixture);
            var definitions = new[]
            {
                BuildingCatalog.MachineGunTurret,
                BuildingCatalog.LaserTower,
                BuildingCatalog.SporeTower,
            };
            var instances = new GrayboxBuildingInstance3D[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                instances[index] = Begin(
                    fixture,
                    definitions[index],
                    BuildingSite.Ground,
                    13 + index * 3,
                    9,
                    CityMode.Fortress);
            }
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);

            for (var index = 0; index < definitions.Length; index++)
            {
                FormalBuildingVisualMetrics3D metrics =
                    BuildingMetrics(definitions[index]);
                Bounds bounds = InstanceBounds(fixture, instances[index]);
                Assert.That(metrics.DefenseOwnsSuperstructure, Is.True);
                Assert.That(
                    bounds.size.x,
                    Is.EqualTo(metrics.FootprintFillRatio).Within(.02f));
                Assert.That(
                    bounds.size.z,
                    Is.EqualTo(metrics.FootprintFillRatio).Within(.02f));
                Assert.That(
                    bounds.size.y,
                    Is.EqualTo(metrics.VisualHeightInCells).Within(.02f));
                Assert.That(bounds.size.y, Is.LessThanOrEqualTo(.16f));
            }
        }

        [Test]
        public void IDEA0019_RotatedLargeBuildingKeepsHeightCenterAndFootprint()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            PrepareFormalVisualFixture(fixture);
            BuildingOrientation[] orientations =
            {
                BuildingOrientation.North,
                BuildingOrientation.East,
                BuildingOrientation.South,
                BuildingOrientation.West,
            };
            Vector2Int[] anchors =
            {
                new Vector2Int(10, 8),
                new Vector2Int(14, 8),
                new Vector2Int(17, 8),
                new Vector2Int(21, 8),
            };
            var instances = new GrayboxBuildingInstance3D[orientations.Length];
            for (var index = 0; index < orientations.Length; index++)
            {
                instances[index] = Begin(
                    fixture,
                    BuildingCatalog.BehemothPen,
                    BuildingSite.Ground,
                    anchors[index].x,
                    anchors[index].y,
                    CityMode.Fortress,
                    orientations[index]);
            }
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            FormalBuildingVisualMetrics3D metrics = BuildingMetrics(
                BuildingCatalog.BehemothPen);

            for (var index = 0; index < orientations.Length; index++)
            {
                int width = BuildingOrientationRules.Width(
                    BuildingCatalog.BehemothPen,
                    orientations[index]);
                int height = BuildingOrientationRules.Height(
                    BuildingCatalog.BehemothPen,
                    orientations[index]);
                Bounds bounds = InstanceBounds(fixture, instances[index]);
                float expectedCenterX = anchors[index].x -
                    GrayboxWorldLayout3D.WorldWidth * .5f +
                    (width - 1) * .5f;
                float expectedCenterZ = anchors[index].y -
                    GrayboxWorldLayout3D.WorldHeight * .5f +
                    (height - 1) * .5f;
                Assert.That(
                    bounds.center.x,
                    Is.EqualTo(expectedCenterX).Within(.02f),
                    orientations[index].ToString());
                Assert.That(
                    bounds.center.z,
                    Is.EqualTo(expectedCenterZ).Within(.02f),
                    orientations[index].ToString());
                Assert.That(
                    bounds.size.x,
                    Is.EqualTo(width * metrics.FootprintFillRatio)
                        .Within(.02f));
                Assert.That(
                    bounds.size.z,
                    Is.EqualTo(height * metrics.FootprintFillRatio)
                        .Within(.02f));
                Assert.That(
                    bounds.size.y,
                    Is.EqualTo(metrics.VisualHeightInCells).Within(.02f));

                for (var offsetX = 0; offsetX < width; offsetX++)
                for (var offsetY = 0; offsetY < height; offsetY++)
                {
                    Assert.That(
                        fixture.Session.GroundGrid.IsOccupied(
                            anchors[index].x + offsetX,
                            anchors[index].y + offsetY),
                        Is.True,
                        orientations[index] + " logical footprint");
                }
            }
        }

        [Test]
        public void WorldView_InstanceKeepsOneRootAndRendererAcrossAllStableStates()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            GrayboxBuildingInstance3D instance = Begin(
                fixture,
                BuildingCatalog.Housing,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);
            Transform instanceRoot = fixture.InstanceRoot.GetChild(0);
            Renderer renderer =
                instanceRoot.GetComponentInChildren<Renderer>(true);

            Assert.That(fixture.InstanceRoot.childCount, Is.EqualTo(1));
            Assert.That(
                instanceRoot.GetComponentsInChildren<Renderer>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                InstanceSlotIds(instanceRoot),
                Is.EquivalentTo(new[]
                {
                    "building.construction.foundation." +
                    instance.StableInstanceId,
                    "building.construction.frame." +
                    instance.StableInstanceId
                }));

            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Assert.That(fixture.InstanceRoot.GetChild(0), Is.SameAs(instanceRoot));
            Assert.That(
                instanceRoot.GetComponentInChildren<Renderer>(true),
                Is.SameAs(renderer));
            Assert.That(
                InstanceSlotIds(instanceRoot),
                Is.EqualTo(new[]
                {
                    "building.complete." + instance.StableInstanceId
                }));

            SetInstanceState(
                instance,
                GrayboxBuildingInstanceState.AbandonedRuin);
            fixture.Presentation.UpdateInstance(instance);
            Assert.That(fixture.InstanceRoot.GetChild(0), Is.SameAs(instanceRoot));
            Assert.That(
                instanceRoot.GetComponentInChildren<Renderer>(true),
                Is.SameAs(renderer));
            Assert.That(
                InstanceSlotIds(instanceRoot),
                Is.EqualTo(new[]
                {
                    "building.ruin." + instance.StableInstanceId
                }));
            Assert.That(fixture.Presentation.InstanceRendererCount, Is.EqualTo(1));
        }

        [Test]
        public void WorldView_CombinesFootprintWithoutPerCellGameObjects()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);

            Begin(
                fixture,
                BuildingCatalog.Housing,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);

            Assert.That(fixture.InstanceRoot.childCount, Is.EqualTo(1));
            Transform root = fixture.InstanceRoot.GetChild(0);
            Assert.That(root.childCount, Is.Zero);
            Assert.That(
                root.GetComponentsInChildren<Renderer>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                root.GetComponentsInChildren<GrayboxVisualSlot>(true).Length,
                Is.EqualTo(2));
        }

        [Test]
        public void WorldView_ColliderPickingReturnsOnlyStableInstanceId()
        {
            WorldFixture fixture = CreateWorldFixture(
                OpenCells(),
                CityMode.Fortress);
            GrayboxBuildingInstance3D instance = Begin(
                fixture,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                20,
                15,
                CityMode.Fortress);
            Transform root = fixture.InstanceRoot.GetChild(0);
            Physics.SyncTransforms();
            var ray = new Ray(
                root.position + Vector3.up * 10f,
                Vector3.down);

            Assert.That(
                fixture.Presentation.TryPickInstance(
                    ray,
                    out string stableInstanceId),
                Is.True);
            Assert.That(stableInstanceId, Is.EqualTo(instance.StableInstanceId));
            Assert.That(
                fixture.Presentation.TryPickInstance(
                    new Ray(new Vector3(100f, 10f, 100f), Vector3.down),
                    out string missing),
                Is.False);
            Assert.That(missing, Is.Null);
        }

        private WorldFixture CreateWorldFixture(
            WorldCell[,] cells = null,
            CityMode cityMode = CityMode.Mobile,
            bool generateAfterPlacementOnEnable = false)
        {
            GameObject worldObject = Track(new GameObject("world"));
            Transform terrain = NewChild(worldObject.transform, "terrain");
            Transform resources = NewChild(worldObject.transform, "resources");
            Transform obstacles = NewChild(worldObject.transform, "obstacles");
            Material worldMaterial = Track(CreateTestMaterial());
            GrayboxWorldView3D world =
                worldObject.AddComponent<GrayboxWorldView3D>();
            world.Configure(terrain, resources, obstacles, worldMaterial);
            var model = new WorldMapModel(cells ?? OpenCells());
            if (!generateAfterPlacementOnEnable)
                world.Generate(model);

            GameObject cityObject = Track(new GameObject("city"));
            cityObject.transform.position = new Vector3(0f, .5f, 0f);
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider bodyCollider = cityObject.AddComponent<BoxCollider>();
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, bodyCollider);
            city.Deployment.Restore(cityMode, 0f);

            GameObject cameraObject = Track(new GameObject("camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 100f, 100f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;

            GameObject surface = Track(new GameObject("inner-surface-disabled"));
            surface.transform.SetParent(cityObject.transform, false);
            BoxCollider disabledInner = surface.AddComponent<BoxCollider>();
            disabledInner.enabled = false;
            GrayboxBuildingSurfaceProjector3D projector =
                CreateProjector(world, city, camera, disabledInner);

            GameObject sessionObject = Track(new GameObject("session"));
            GrayboxBuildingSession3D session =
                sessionObject.AddComponent<GrayboxBuildingSession3D>();
            session.Configure(true);
            session.ConfigureDevelopmentFixture();

            GameObject interactionObject = Track(new GameObject("interaction"));
            GrayboxBuildingInteractionModel3D interaction =
                interactionObject
                    .AddComponent<GrayboxBuildingInteractionModel3D>();

            GameObject presentationObject =
                Track(new GameObject("building-presentation"));
            Transform instanceRoot =
                NewChild(presentationObject.transform, "instances");
            Transform infrastructureRoot =
                NewChild(presentationObject.transform, "infrastructure");
            Material buildingMaterial = Track(CreateTestMaterial());
            GrayboxBuildingWorldView3D presentation =
                presentationObject.AddComponent<GrayboxBuildingWorldView3D>();
            presentation.Configure(
                instanceRoot,
                infrastructureRoot,
                buildingMaterial,
                city);

            GameObject placementObject = Track(new GameObject("placement"));
            if (generateAfterPlacementOnEnable)
                placementObject.SetActive(false);
            GrayboxBuildingPlacementController3D placement =
                placementObject
                    .AddComponent<GrayboxBuildingPlacementController3D>();
            if (generateAfterPlacementOnEnable)
            {
                SetPlacementField(placement, "session", session);
                SetPlacementField(placement, "city", city);
                SetPlacementField(placement, "world", world);
                SetPlacementField(placement, "projector", projector);
                SetPlacementField(
                    placement,
                    "presentation",
                    presentation);
                SetPlacementField(
                    placement,
                    "interaction",
                    interaction);
                placementObject.SetActive(true);
                InvokePlacementLifecycle(placement, "OnEnable");
                world.Generate(model);
            }
            else
            {
                placement.Configure(
                    session,
                    city,
                    world,
                    projector,
                    presentation,
                    interaction);
            }

            return new WorldFixture(
                world,
                city,
                camera,
                session,
                interaction,
                presentation,
                placement,
                instanceRoot,
                buildingMaterial);
        }

        private GrayboxBuildingMenuView3D CreatePlacementStatusMenu(
            WorldFixture fixture)
        {
            GameObject menuObject = Track(new GameObject("status-menu"));
            GameObject eventObject = Track(new GameObject("status-event"));
            EventSystem eventSystem =
                eventObject.AddComponent<EventSystem>();
            GameObject canvasObject = Track(new GameObject(
                "status-canvas",
                typeof(RectTransform)));
            canvasObject.transform.SetParent(menuObject.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.GetComponent<RectTransform>().sizeDelta =
                new Vector2(640f, 480f);
            GrayboxBuildingMenuView3D menu =
                menuObject.AddComponent<GrayboxBuildingMenuView3D>();
            menu.Configure(
                canvas,
                eventSystem,
                fixture.Session,
                fixture.Interaction,
                fixture.Placement);
            return menu;
        }

        private static Text PlacementStatusText(
            GrayboxBuildingMenuView3D menu)
        {
            return menu.GetComponentsInChildren<Text>(true)
                .Single(value => value.name == "Placement.Status.Text");
        }

        private GameObject CreatePrefabLikePresentationRoot(
            out Material material,
            out Mesh unrelatedMesh)
        {
            GameObject root =
                Track(new GameObject("serialized-presentation-source"));
            Transform cityRoot = NewChild(root.transform, "city");
            GrayboxMobileCityController3D city =
                cityRoot.gameObject
                    .AddComponent<GrayboxMobileCityController3D>();
            Transform presentationRoot =
                NewChild(root.transform, "presentation");
            Transform instances =
                NewChild(presentationRoot, "instances");
            Transform infrastructure =
                NewChild(presentationRoot, "infrastructure");
            material = Track(CreateTestMaterial());
            GrayboxBuildingWorldView3D view =
                presentationRoot.gameObject
                    .AddComponent<GrayboxBuildingWorldView3D>();
            view.Configure(
                instances,
                infrastructure,
                material,
                city);
            unrelatedMesh = Track(CreateSentinelMesh());
            AddMeshSentinel(
                instances,
                "unrelated-instance-sentinel",
                unrelatedMesh);
            AddMeshSentinel(
                infrastructure,
                "unrelated-infrastructure-sentinel",
                unrelatedMesh);
            return root;
        }

        private static Transform AddMeshSentinel(
            Transform parent,
            string name,
            Mesh sharedMesh)
        {
            Transform sentinel = NewChild(parent, name);
            sentinel.gameObject.AddComponent<MeshFilter>().sharedMesh =
                sharedMesh;
            return sentinel;
        }

        private static Mesh CreateSentinelMesh()
        {
            var mesh = new Mesh
            {
                name = "unrelated-shared-mesh",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.forward
                },
                triangles = new[] { 0, 2, 1 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private GrayboxBuildingSurfaceProjector3D CreateProjector(
            WorldFixture fixture,
            Collider inner)
        {
            return CreateProjector(
                fixture.World,
                fixture.City,
                fixture.Camera,
                inner);
        }

        private GrayboxBuildingSurfaceProjector3D CreateProjector(
            GrayboxWorldView3D world,
            GrayboxMobileCityController3D city,
            Camera camera,
            Collider inner)
        {
            GameObject projectorObject = Track(new GameObject("projector"));
            GrayboxBuildingSurfaceProjector3D projector =
                projectorObject
                    .AddComponent<GrayboxBuildingSurfaceProjector3D>();
            projector.Configure(camera, world, city, inner);
            return projector;
        }

        private InnerSurfaceFixture CreateInnerSurface(
            GrayboxMobileCityController3D city)
        {
            GameObject surface = Track(new GameObject("inner-surface"));
            surface.transform.SetParent(city.transform, false);
            surface.transform.localPosition = new Vector3(0f, 1f, 0f);
            BoxCollider collider = surface.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.56f, .1f, 1.92f);
            return new InnerSurfaceFixture(surface.transform, collider);
        }

        private void PositionCameraAtCell(
            WorldFixture fixture,
            int x,
            int y)
        {
            Assert.That(
                fixture.World.Coordinates.TryCellToWorld(
                    x,
                    y,
                    0f,
                    out Vector3 target),
                Is.True);
            PositionCameraOver(
                fixture.Camera,
                target + new Vector3(.1f, 0f, .1f));
            Physics.SyncTransforms();
        }

        private static void PositionCameraOver(Camera camera, Vector3 point)
        {
            camera.transform.position =
                new Vector3(point.x, point.y + 10f, point.z);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static WorldCell[,] OpenCells()
        {
            return OpenCells(32, 24);
        }

        private static WorldCell[,] OpenCells(int width, int height)
        {
            var result = new WorldCell[width, height];
            for (var x = 0; x < result.GetLength(0); x++)
            for (var y = 0; y < result.GetLength(1); y++)
                result[x, y] = Cell();
            return result;
        }

        private static WorldCell Cell(
            string resourceId = null,
            WorldTraversalKind traversal = WorldTraversalKind.Open)
        {
            return new WorldCell(
                TerrainKind.Wasteland,
                resourceId,
                string.IsNullOrEmpty(resourceId) ? 0 : 100,
                traversal);
        }

        private static BuildingPlacementEvaluation ValidEvaluation(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            CityMode mode)
        {
            return BuildingPlacementRules.Evaluate(
                Request(session, definition, site, x, y, mode));
        }

        private static BuildingPlacementRequest Request(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            CityMode mode,
            bool terrainPassable = true,
            bool obstacleFree = true,
            BuildingOrientation orientation = BuildingOrientation.North)
        {
            return new BuildingPlacementRequest(
                definition,
                site == BuildingSite.InnerCity
                    ? session.InnerGrid
                    : session.GroundGrid,
                site,
                orientation,
                x,
                y,
                16,
                12,
                session.GroundBuildRadius,
                mode,
                true,
                false,
                terrainPassable,
                obstacleFree,
                !definition.RequiresResourceNode,
                definition.RequiresResourceNode
                    ? "world.resource-node.20.15"
                    : null,
                true,
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount),
                session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost));
        }

        private static GrayboxBuildingInstance3D Begin(
            WorldFixture fixture,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            CityMode mode,
            BuildingOrientation orientation = BuildingOrientation.North)
        {
            Assert.That(
                fixture.Session.TryBeginConstruction(
                    Request(
                        fixture.Session,
                        definition,
                        site,
                        x,
                        y,
                        mode,
                        orientation: orientation),
                    fixture.Presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static Bounds InstanceBounds(
            WorldFixture fixture,
            GrayboxBuildingInstance3D instance)
        {
            Transform root = fixture.InstanceRoot
                .Cast<Transform>()
                .Single(value => value.name == instance.StableInstanceId);
            Physics.SyncTransforms();
            return root.GetComponent<MeshRenderer>().bounds;
        }

        private static FormalBuildingVisualMetrics3D BuildingMetrics(
            BuildingDefinition definition)
        {
            FormalWorldPresentationScaleProfile3D profile = Resources.Load<
                FormalWorldPresentationScaleProfile3D>(
                FormalWorldPresentationScaleProfile3D.ResourcesPath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(
                profile.TryResolveBuilding(definition, out var metrics),
                Is.True,
                definition.Id.Value);
            return metrics;
        }

        private static void PrepareFormalVisualFixture(WorldFixture fixture)
        {
            fixture.Session.UnlockAllResearchForDevelopment();
            fixture.Session.SetPopulationForDevelopment(2000);
            fixture.Session.Inventory.Set(ResourceIds.Stone, 150);
            fixture.Session.Inventory.Set(ResourceIds.Alloy, 150);
            fixture.Session.Inventory.Set(ResourceIds.Biomass, 150);
            fixture.Session.Inventory.Set(ResourceIds.BoneSteel, 150);
            fixture.Session.Inventory.Set(
                ResourceIds.BiomassConcentrate,
                150);
            fixture.Session.Inventory.Set(ResourceIds.SpiritIron, 150);

            Begin(
                fixture,
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                16,
                CityMode.Fortress);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Begin(
                fixture,
                BuildingCatalog.Assembler,
                BuildingSite.Ground,
                13,
                16,
                CityMode.Fortress);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Begin(
                fixture,
                BuildingCatalog.ColonyPool,
                BuildingSite.Ground,
                16,
                16,
                CityMode.Fortress);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Begin(
                fixture,
                BuildingCatalog.BreedingChamber,
                BuildingSite.Ground,
                19,
                16,
                CityMode.Fortress);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
        }

        private static IEnumerable<GrayboxVisualSlot> NodeSlots(
            GrayboxBuildingWorldView3D presentation)
        {
            return presentation
                .GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Where(
                    value => value.StableId.StartsWith(
                        "building.node-highlight.",
                        StringComparison.Ordinal));
        }

        private static GrayboxVisualSlot[] ActiveGuidanceSlots(
            GrayboxBuildingWorldView3D presentation,
            string prefix)
        {
            return presentation
                .GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Where(value =>
                    value.gameObject.activeSelf &&
                    value.StableId.StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                .ToArray();
        }

        private static int GuidanceSlotCount(
            GrayboxBuildingWorldView3D presentation,
            string prefix)
        {
            return presentation
                .GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Count(value => value.StableId.StartsWith(
                    prefix,
                    StringComparison.Ordinal));
        }

        private static void InvokeRefreshMiningGuidance(
            GrayboxBuildingPlacementController3D placement)
        {
            MiningGuidanceRefreshAction(placement)();
        }

        private static Action MiningGuidanceRefreshAction(
            GrayboxBuildingPlacementController3D placement)
        {
            MethodInfo method =
                typeof(GrayboxBuildingPlacementController3D).GetMethod(
                    "RefreshMiningGuidance",
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                method,
                Is.Not.Null,
                "IDEA-0010 requires the input-driven refresh seam.");
            return (Action)Delegate.CreateDelegate(
                typeof(Action),
                placement,
                method);
        }

        private static BuildingPlacementEvaluation EvaluateGuidanceAnchor(
            GrayboxBuildingPlacementController3D placement,
            string stableId)
        {
            string[] parts = stableId.Split('.');
            Assert.That(parts.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(
                int.TryParse(parts[parts.Length - 3], out int x),
                Is.True,
                stableId);
            Assert.That(
                int.TryParse(parts[parts.Length - 2], out int y),
                Is.True,
                stableId);
            Assert.That(
                Enum.TryParse(
                    parts[parts.Length - 1],
                    true,
                    out BuildingOrientation orientation),
                Is.True,
                stableId);
            MethodInfo method =
                typeof(GrayboxBuildingPlacementController3D).GetMethod(
                    "CreateGroundRequest",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var request = (BuildingPlacementRequest)method.Invoke(
                placement,
                new object[]
                {
                    BuildingCatalog.MiningStation,
                    orientation,
                    x,
                    y
                });
            return BuildingPlacementRules.Evaluate(request);
        }

        private static string MiningAnchorStableId(
            int x,
            int y,
            BuildingOrientation orientation)
        {
            return "building.anchor-highlight." + x + "." + y + "." +
                   orientation;
        }

        private static void InvokeSetGroundBuildRange(
            GrayboxBuildingWorldView3D presentation,
            int cityX,
            int cityY,
            int radius,
            int worldWidth,
            int worldHeight)
        {
            MethodInfo method = typeof(GrayboxBuildingWorldView3D).GetMethod(
                "SetGroundBuildRange",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                method,
                Is.Not.Null,
                "IDEA-0008 requires the public range configuration seam.");
            method.Invoke(
                presentation,
                new object[]
                {
                    cityX,
                    cityY,
                    radius,
                    worldWidth,
                    worldHeight
                });
        }

        private static HashSet<GridEdge> ExpectedGridEdges(
            int cityX,
            int cityY,
            int radius,
            int worldWidth,
            int worldHeight)
        {
            HashSet<Vector2Int> cells = ExpectedRangeCells(
                cityX,
                cityY,
                radius,
                worldWidth,
                worldHeight);
            var edges = new HashSet<GridEdge>();
            foreach (Vector2Int cell in cells)
            {
                edges.Add(GridEdge.Vertical(cell.x, cell.y));
                edges.Add(GridEdge.Vertical(cell.x + 1, cell.y));
                edges.Add(GridEdge.Horizontal(cell.x, cell.y));
                edges.Add(GridEdge.Horizontal(cell.x, cell.y + 1));
            }
            return edges;
        }

        private static HashSet<GridEdge> ExpectedBoundaryEdges(
            int cityX,
            int cityY,
            int radius,
            int worldWidth,
            int worldHeight)
        {
            HashSet<Vector2Int> cells = ExpectedRangeCells(
                cityX,
                cityY,
                radius,
                worldWidth,
                worldHeight);
            var edges = new HashSet<GridEdge>();
            foreach (Vector2Int cell in cells)
            {
                if (!cells.Contains(new Vector2Int(cell.x - 1, cell.y)))
                    edges.Add(GridEdge.Vertical(cell.x, cell.y));
                if (!cells.Contains(new Vector2Int(cell.x + 1, cell.y)))
                    edges.Add(GridEdge.Vertical(cell.x + 1, cell.y));
                if (!cells.Contains(new Vector2Int(cell.x, cell.y - 1)))
                    edges.Add(GridEdge.Horizontal(cell.x, cell.y));
                if (!cells.Contains(new Vector2Int(cell.x, cell.y + 1)))
                    edges.Add(GridEdge.Horizontal(cell.x, cell.y + 1));
            }
            return edges;
        }

        private static HashSet<Vector2Int> ExpectedRangeCells(
            int cityX,
            int cityY,
            int radius,
            int worldWidth,
            int worldHeight)
        {
            var cells = new HashSet<Vector2Int>();
            for (var x = 0; x < worldWidth; x++)
            for (var y = 0; y < worldHeight; y++)
                if (BuildingRangeRules.IsGroundCellInRange(
                        cityX,
                        cityY,
                        x,
                        y,
                        radius))
                    cells.Add(new Vector2Int(x, y));
            return cells;
        }

        private static HashSet<GridEdge> MeshGridEdges(
            Mesh mesh,
            int worldWidth,
            int worldHeight)
        {
            Assert.That(mesh, Is.Not.Null);
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Assert.That(cube, Is.Not.Null);
            Assert.That(
                mesh.vertexCount % cube.vertexCount,
                Is.Zero,
                "Range meshes must contain only combined line cubes.");
            Vector3[] vertices = mesh.vertices;
            var edges = new HashSet<GridEdge>();
            for (var start = 0;
                 start < vertices.Length;
                 start += cube.vertexCount)
            {
                Vector3 minimum = vertices[start];
                Vector3 maximum = vertices[start];
                int end = start + cube.vertexCount;
                for (var index = start + 1; index < end; index++)
                {
                    minimum = Vector3.Min(minimum, vertices[index]);
                    maximum = Vector3.Max(maximum, vertices[index]);
                }

                Vector3 center = (minimum + maximum) * .5f;
                Vector3 size = maximum - minimum;
                if (size.z > size.x)
                {
                    edges.Add(GridEdge.Vertical(
                        Mathf.RoundToInt(
                            center.x + worldWidth * .5f + .5f),
                        Mathf.RoundToInt(
                            center.z + worldHeight * .5f)));
                }
                else
                {
                    edges.Add(GridEdge.Horizontal(
                        Mathf.RoundToInt(
                            center.x + worldWidth * .5f),
                        Mathf.RoundToInt(
                            center.z + worldHeight * .5f + .5f)));
                }
            }
            Assert.That(
                vertices.Length / cube.vertexCount,
                Is.EqualTo(edges.Count),
                "IDEA-0008 combined meshes must not duplicate grid edges.");
            return edges;
        }

        private static GrayboxVisualSlot Slot(
            GrayboxBuildingWorldView3D presentation,
            string stableId)
        {
            return presentation
                .GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Single(value => value.StableId == stableId);
        }

        private static Color PropertyColor(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor(Shader.PropertyToID("_BaseColor"));
        }

        private static Vector2Int[] FootprintSnapshot(
            in BuildingPlacementEvaluation evaluation)
        {
            return evaluation.Footprint
                .Select(cell => new Vector2Int(cell.X, cell.Y))
                .ToArray();
        }

        private static object PlacementWorkspace(
            GrayboxBuildingPlacementController3D placement)
        {
            FieldInfo field =
                typeof(GrayboxBuildingPlacementController3D)
                    .GetField(
                        "evaluationWorkspace",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(placement);
        }

        private static GrayboxBuildingSurfaceProjector3D PlacementProjector(
            GrayboxBuildingPlacementController3D placement)
        {
            FieldInfo field =
                typeof(GrayboxBuildingPlacementController3D)
                    .GetField(
                        "projector",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (GrayboxBuildingSurfaceProjector3D)
                field.GetValue(placement);
        }

        private static void InvokePlacementLifecycle(
            GrayboxBuildingPlacementController3D placement,
            string methodName)
        {
            MethodInfo method =
                typeof(GrayboxBuildingPlacementController3D)
                    .GetMethod(
                        methodName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(placement, null);
        }

        private static void ClearTransientPlacementCaches(
            GrayboxBuildingPlacementController3D placement)
        {
            SetPlacementField(
                placement,
                "researchCompleted",
                null);
            SetPlacementField(
                placement,
                "completedBuildings",
                null);
            SetPlacementField(
                placement,
                "resourceNodeVisualIds",
                null);
            SetPlacementField(
                placement,
                "resourceNodeVisualWidth",
                0);
            SetPlacementField(
                placement,
                "resourceNodeVisualHeight",
                0);
        }

        private static void SetPlacementField(
            GrayboxBuildingPlacementController3D placement,
            string fieldName,
            object value)
        {
            FieldInfo field =
                typeof(GrayboxBuildingPlacementController3D)
                    .GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(placement, value);
        }

        private static AllocationMeasurement Profile300Calls(Action action)
        {
            ProfilerRecorder recorder =
                ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC.Alloc",
                    2048,
                    ProfilerRecorderOptions.StartImmediately |
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                action();
            long currentThreadBytes =
                GC.GetAllocatedBytesForCurrentThread() - before;
            recorder.Stop();
            int samples = recorder.Count;
            long profiledBytes = 0;
            for (var index = 0; index < recorder.Count; index++)
            {
                ProfilerRecorderSample sample =
                    recorder.GetSample(index);
                profiledBytes += sample.Value * sample.Count;
            }
            recorder.Dispose();
            return new AllocationMeasurement(
                samples,
                profiledBytes,
                currentThreadBytes);
        }

        private static string[] InstanceSlotIds(Transform root)
        {
            return root
                .GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Select(value => value.StableId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void SetInstanceState(
            GrayboxBuildingInstance3D instance,
            GrayboxBuildingInstanceState state)
        {
            FieldInfo field = typeof(GrayboxBuildingInstance3D).GetField(
                "<State>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(instance, state);
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Material CreateTestMaterial()
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private readonly struct InnerSurfaceFixture
        {
            public Transform Root { get; }
            public BoxCollider Collider { get; }

            public InnerSurfaceFixture(
                Transform root,
                BoxCollider collider)
            {
                Root = root;
                Collider = collider;
            }
        }

        private readonly struct WorldFixture
        {
            public GrayboxWorldView3D World { get; }
            public GrayboxMobileCityController3D City { get; }
            public Camera Camera { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingInteractionModel3D Interaction { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public GrayboxBuildingPlacementController3D Placement { get; }
            public Transform InstanceRoot { get; }
            public Material Material { get; }

            public WorldFixture(
                GrayboxWorldView3D world,
                GrayboxMobileCityController3D city,
                Camera camera,
                GrayboxBuildingSession3D session,
                GrayboxBuildingInteractionModel3D interaction,
                GrayboxBuildingWorldView3D presentation,
                GrayboxBuildingPlacementController3D placement,
                Transform instanceRoot,
                Material material)
            {
                World = world;
                City = city;
                Camera = camera;
                Session = session;
                Interaction = interaction;
                Presentation = presentation;
                Placement = placement;
                InstanceRoot = instanceRoot;
                Material = material;
            }
        }

        private readonly struct AllocationMeasurement
        {
            public AllocationMeasurement(
                int samples,
                long profiledBytes,
                long currentThreadBytes)
            {
                Samples = samples;
                ProfiledBytes = profiledBytes;
                CurrentThreadBytes = currentThreadBytes;
            }

            public int Samples { get; }
            public long ProfiledBytes { get; }
            public long CurrentThreadBytes { get; }
        }

        private readonly struct GridEdge : IEquatable<GridEdge>
        {
            private GridEdge(int x, int y, bool vertical)
            {
                X = x;
                Y = y;
                IsVertical = vertical;
            }

            private int X { get; }
            private int Y { get; }
            private bool IsVertical { get; }

            public static GridEdge Vertical(int x, int y)
            {
                return new GridEdge(x, y, true);
            }

            public static GridEdge Horizontal(int x, int y)
            {
                return new GridEdge(x, y, false);
            }

            public bool Equals(GridEdge other)
            {
                return X == other.X &&
                       Y == other.Y &&
                       IsVertical == other.IsVertical;
            }

            public override bool Equals(object value)
            {
                return value is GridEdge other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X;
                    hash = hash * 397 ^ Y;
                    hash = hash * 397 ^ IsVertical.GetHashCode();
                    return hash;
                }
            }

            public override string ToString()
            {
                return (IsVertical ? "V" : "H") +
                       "(" + X + "," + Y + ")";
            }
        }
    }
}
