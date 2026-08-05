using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
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
                NodeSlots(fixture.Presentation).Select(value => value.StableId),
                Is.EqualTo(new[]
                {
                    "building.node-highlight.world.resource-node.19.15"
                }));

            fixture.Interaction.Select(BuildingCatalog.Wall);
            fixture.Placement.UpdatePointer(ScreenCenter);
            Assert.That(NodeSlots(fixture.Presentation), Is.Empty);
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

            Assert.That(fixture.Placement.CurrentEvaluation.IsValid, Is.False);
            Assert.That(
                fixture.Placement.CurrentEvaluation.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.InsufficientMaterials));
            GrayboxVisualSlot preview = Slot(
                fixture.Presentation,
                "building.preview.core.building.wall");
            Color shown = PropertyColor(preview.Renderer);
            Assert.That(shown.r, Is.GreaterThan(shown.g));
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
            CityMode cityMode = CityMode.Mobile)
        {
            GameObject worldObject = Track(new GameObject("world"));
            Transform terrain = NewChild(worldObject.transform, "terrain");
            Transform resources = NewChild(worldObject.transform, "resources");
            Transform obstacles = NewChild(worldObject.transform, "obstacles");
            Material worldMaterial = Track(CreateTestMaterial());
            GrayboxWorldView3D world =
                worldObject.AddComponent<GrayboxWorldView3D>();
            world.Configure(terrain, resources, obstacles, worldMaterial);
            world.Generate(new WorldMapModel(cells ?? OpenCells()));

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
            GrayboxBuildingPlacementController3D placement =
                placementObject
                    .AddComponent<GrayboxBuildingPlacementController3D>();
            placement.Configure(
                session,
                city,
                world,
                projector,
                presentation,
                interaction);

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
            var result = new WorldCell[32, 24];
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
            bool obstacleFree = true)
        {
            return new BuildingPlacementRequest(
                definition,
                site == BuildingSite.InnerCity
                    ? session.InnerGrid
                    : session.GroundGrid,
                site,
                BuildingOrientation.North,
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
            CityMode mode)
        {
            Assert.That(
                fixture.Session.TryBeginConstruction(
                    Request(
                        fixture.Session,
                        definition,
                        site,
                        x,
                        y,
                        mode),
                    fixture.Presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
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
    }
}
