using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxSpatialTemplateControllerTests
    {
        private const string TemplateId =
            GrayboxFormalProgressionSaveAdapter3D
                .FormalSpatialTemplateSlotId;

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
        public void IDEA0028_RecordGroundRegionCapturesStableThreeByThreeLayout()
        {
            Fixture fixture = CreateFixture();
            const int anchorX = 36;
            const int anchorY = 27;
            GrayboxBuildingInstance3D first = Begin(
                fixture,
                BuildingCatalog.Wall,
                BuildingOrientation.North,
                anchorX - 1,
                anchorY - 1);
            GrayboxBuildingInstance3D second = Begin(
                fixture,
                BuildingCatalog.Housing,
                BuildingOrientation.East,
                anchorX,
                anchorY);
            _ = Begin(
                fixture,
                BuildingCatalog.Wall,
                BuildingOrientation.North,
                anchorX + 4,
                anchorY);

            Assert.That(fixture.Controller.TryRecordGroundRegion(
                TemplateId,
                anchorX,
                anchorY,
                fixture.Session.Instances,
                out string error), Is.True, error);

            SpatialTemplateDefinition saved = fixture.Runtime.Capture()
                .Templates.Single();
            Assert.That(saved.Width, Is.EqualTo(3));
            Assert.That(saved.Height, Is.EqualTo(3));
            Assert.That(saved.Cells.Select(value => (
                    value.X,
                    value.Y,
                    value.BuildingDefinitionId,
                    value.RotationQuarterTurns)),
                Is.EqualTo(new[]
                {
                    (-1, -1, first.Placement.Definition.Id.Value, 0),
                    (0, 0, second.Placement.Definition.Id.Value, 1)
                }));
        }

        [Test]
        public void IDEA0028_RecordRejectsFootprintOutsideThreeByThreeRegion()
        {
            Fixture fixture = CreateFixture();
            const int anchorX = 36;
            const int anchorY = 27;
            _ = Begin(
                fixture,
                BuildingCatalog.Housing,
                BuildingOrientation.North,
                anchorX + 1,
                anchorY + 1);

            Assert.That(fixture.Controller.TryRecordGroundRegion(
                TemplateId,
                anchorX,
                anchorY,
                fixture.Session.Instances,
                out string error), Is.False);
            Assert.That(error, Does.Contain(BuildingCatalog.Housing.Name));
            Assert.That(error, Does.Contain("3×3"));
            Assert.That(fixture.Runtime.Capture().Templates, Is.Empty);
        }

        [Test]
        public void IDEA0028_ValidTemplatePreflightsThenStartsEveryConstruction()
        {
            Fixture fixture = CreateFixture();
            Record(
                fixture.Runtime,
                new SpatialTemplateCell(
                    -1, -1, BuildingCatalog.Wall.Id.Value, 0),
                new SpatialTemplateCell(
                    1, -1, BuildingCatalog.Wall.Id.Value, 3),
                new SpatialTemplateCell(
                    0, 1, BuildingCatalog.Wall.Id.Value, 1));
            const int anchorX = 36;
            const int anchorY = 27;
            int stoneBefore = fixture.Session.GetCityResourceAmount(
                ResourceIds.Stone);

            Assert.That(fixture.Controller.TryPrepareDeployment(
                TemplateId,
                anchorX,
                anchorY,
                out GrayboxSpatialTemplateDeploymentPlan3D plan,
                out GrayboxSpatialTemplateFailure3D failure),
                Is.True,
                failure.Reason);
            Assert.That(fixture.Session.Instances, Is.Empty,
                "Preflight must not pay or occupy any cell.");
            Assert.That(fixture.Controller.TryCommitDeployment(
                plan,
                out IReadOnlyList<GrayboxBuildingInstance3D> created,
                out failure),
                Is.True,
                failure.Reason);

            Assert.That(created.Count, Is.EqualTo(3));
            Assert.That(created.Select(value => (
                    value.Placement.X,
                    value.Placement.Y,
                    value.Placement.Orientation)),
                Is.EqualTo(new[]
                {
                    (anchorX - 1, anchorY - 1, BuildingOrientation.North),
                    (anchorX + 1, anchorY - 1, BuildingOrientation.West),
                    (anchorX, anchorY + 1, BuildingOrientation.East)
                }));
            Assert.That(created.All(value =>
                value.State == GrayboxBuildingInstanceState.UnderConstruction),
                Is.True);
            Assert.That(fixture.Session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(stoneBefore - BuildingCatalog.Wall.Cost * 3));
        }

        [Test]
        public void IDEA0028_FormalPlacementFailureRejectsWholeTemplateWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            Record(
                fixture.Runtime,
                new SpatialTemplateCell(
                    -1, 0, BuildingCatalog.Wall.Id.Value, 0),
                new SpatialTemplateCell(
                    1, 0, BuildingCatalog.Wall.Id.Value, 0));
            const int anchorX = 36;
            const int anchorY = 27;
            _ = Begin(
                fixture,
                BuildingCatalog.Wall,
                BuildingOrientation.North,
                anchorX + 1,
                anchorY);
            int stoneBefore = fixture.Session.GetCityResourceAmount(
                ResourceIds.Stone);
            int countBefore = fixture.Session.Instances.Count;

            Assert.That(fixture.Controller.TryPrepareDeployment(
                TemplateId,
                anchorX,
                anchorY,
                out _,
                out GrayboxSpatialTemplateFailure3D failure), Is.False);
            Assert.That(failure.BuildingDefinitionId,
                Is.EqualTo(BuildingCatalog.Wall.Id.Value));
            Assert.That(failure.WorldX, Is.EqualTo(anchorX + 1));
            Assert.That(failure.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.Overlap));
            Assert.That(fixture.Session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(stoneBefore));
            Assert.That(fixture.Session.Instances.Count, Is.EqualTo(countBefore));
            Assert.That(fixture.Session.GroundGrid.IsOccupied(anchorX - 1, anchorY),
                Is.False);
        }

        [Test]
        public void IDEA0028_TemplateInternalOverlapIsRejectedDuringPreflight()
        {
            Fixture fixture = CreateFixture();
            Record(
                fixture.Runtime,
                new SpatialTemplateCell(
                    -1, -1, BuildingCatalog.Warehouse.Id.Value, 0),
                new SpatialTemplateCell(
                    0, -1, BuildingCatalog.Warehouse.Id.Value, 0));
            int alloyBefore = fixture.Session.GetCityResourceAmount(
                ResourceIds.Alloy);

            Assert.That(fixture.Controller.TryPrepareDeployment(
                TemplateId,
                36,
                27,
                out _,
                out GrayboxSpatialTemplateFailure3D failure), Is.False);
            Assert.That(failure.Kind,
                Is.EqualTo(GrayboxSpatialTemplateFailureKind3D.TemplateOverlap));
            Assert.That(failure.BuildingDefinitionId,
                Is.EqualTo(BuildingCatalog.Warehouse.Id.Value));
            Assert.That(fixture.Session.GetCityResourceAmount(ResourceIds.Alloy),
                Is.EqualTo(alloyBefore));
            Assert.That(fixture.Session.Instances, Is.Empty);
        }

        [Test]
        public void IDEA0028_AggregateMaterialsFailBeforeAnySpendOrOccupancy()
        {
            Fixture fixture = CreateFixture();
            fixture.Session.Inventory.Set(ResourceIds.Stone, 3);
            Record(
                fixture.Runtime,
                new SpatialTemplateCell(
                    -1, 0, BuildingCatalog.Wall.Id.Value, 0),
                new SpatialTemplateCell(
                    1, 0, BuildingCatalog.Wall.Id.Value, 0));

            Assert.That(fixture.Controller.TryPrepareDeployment(
                TemplateId,
                36,
                27,
                out _,
                out GrayboxSpatialTemplateFailure3D failure), Is.False);
            Assert.That(failure.Kind,
                Is.EqualTo(GrayboxSpatialTemplateFailureKind3D.InvalidPlacement));
            Assert.That(failure.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.InsufficientMaterials));
            Assert.That(failure.BuildingDefinitionId,
                Is.EqualTo(BuildingCatalog.Wall.Id.Value));
            Assert.That(fixture.Session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(3));
            Assert.That(fixture.Session.Instances, Is.Empty);
            Assert.That(fixture.Session.GroundGrid.Count, Is.Zero);
        }

        [Test]
        public void IDEA0028_MidCommitRecheckFailureRollsBackEarlierConstruction()
        {
            Fixture fixture = CreateFixture();
            Record(
                fixture.Runtime,
                new SpatialTemplateCell(
                    -1, 0, BuildingCatalog.Wall.Id.Value, 0),
                new SpatialTemplateCell(
                    1, 0, BuildingCatalog.Wall.Id.Value, 0));
            const int anchorX = 36;
            const int anchorY = 27;
            Assert.That(fixture.Controller.TryPrepareDeployment(
                TemplateId,
                anchorX,
                anchorY,
                out GrayboxSpatialTemplateDeploymentPlan3D plan,
                out GrayboxSpatialTemplateFailure3D failure),
                Is.True,
                failure.Reason);
            _ = Begin(
                fixture,
                BuildingCatalog.Wall,
                BuildingOrientation.North,
                anchorX + 1,
                anchorY);
            int stoneBeforeCommit = fixture.Session.GetCityResourceAmount(
                ResourceIds.Stone);
            int countBeforeCommit = fixture.Session.Instances.Count;

            Assert.That(fixture.Controller.TryCommitDeployment(
                plan,
                out IReadOnlyList<GrayboxBuildingInstance3D> created,
                out failure), Is.False);
            Assert.That(failure.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.Overlap));
            Assert.That(failure.WorldX, Is.EqualTo(anchorX + 1));
            Assert.That(created, Is.Empty);
            Assert.That(fixture.Session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(stoneBeforeCommit),
                "Every construction started by this commit must be fully refunded.");
            Assert.That(fixture.Session.Instances.Count,
                Is.EqualTo(countBeforeCommit));
            Assert.That(fixture.Session.GroundGrid.IsOccupied(anchorX - 1, anchorY),
                Is.False);
            Assert.That(
                fixture.Session.GroundGrid.IsOccupied(anchorX + 1, anchorY),
                Is.True,
                "The external blocker must survive rollback.");
        }

        [Test]
        public void IDEA0028_CommitExceptionRollsBackEarlierConstructionAndReportsBuilding()
        {
            Fixture fixture = CreateFixture();
            Record(
                fixture.Runtime,
                new SpatialTemplateCell(
                    -1, 0, BuildingCatalog.Wall.Id.Value, 0),
                new SpatialTemplateCell(
                    1, 0, BuildingCatalog.Wall.Id.Value, 0));
            Assert.That(fixture.Controller.TryPrepareDeployment(
                TemplateId,
                36,
                27,
                out GrayboxSpatialTemplateDeploymentPlan3D plan,
                out GrayboxSpatialTemplateFailure3D failure),
                Is.True,
                failure.Reason);
            fixture.Session.ConfigureConstructionPaymentPolicy(
                new ThrowOnSecondPaymentPolicy());
            int stoneBefore = fixture.Session.GetCityResourceAmount(
                ResourceIds.Stone);

            Assert.That(fixture.Controller.TryCommitDeployment(
                plan,
                out IReadOnlyList<GrayboxBuildingInstance3D> created,
                out failure), Is.False);
            Assert.That(created, Is.Empty);
            Assert.That(failure.Kind,
                Is.EqualTo(GrayboxSpatialTemplateFailureKind3D.CommitFailed));
            Assert.That(failure.BuildingDefinitionId,
                Is.EqualTo(BuildingCatalog.Wall.Id.Value));
            Assert.That(failure.WorldX, Is.EqualTo(37));
            Assert.That(failure.Reason, Does.Contain("synthetic payment failure"));
            Assert.That(fixture.Session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(stoneBefore));
            Assert.That(fixture.Session.Instances, Is.Empty);
            Assert.That(fixture.Session.GroundGrid.Count, Is.Zero);
        }

        [Test]
        public void IDEA0028_DeploymentPlanIsOwnerBoundAndSingleUse()
        {
            Fixture fixture = CreateFixture();
            Fixture other = CreateFixture();
            Record(
                fixture.Runtime,
                new SpatialTemplateCell(
                    0, 0, BuildingCatalog.Wall.Id.Value, 0));
            Assert.That(fixture.Controller.TryPrepareDeployment(
                TemplateId,
                36,
                27,
                out GrayboxSpatialTemplateDeploymentPlan3D plan,
                out GrayboxSpatialTemplateFailure3D failure),
                Is.True,
                failure.Reason);

            Assert.That(other.Controller.TryCommitDeployment(
                plan, out _, out failure), Is.False);
            Assert.That(failure.Kind,
                Is.EqualTo(GrayboxSpatialTemplateFailureKind3D.InvalidPlan));
            Assert.That(fixture.Controller.TryCommitDeployment(
                plan, out _, out failure), Is.True, failure.Reason);
            Assert.That(fixture.Controller.TryCommitDeployment(
                plan, out _, out failure), Is.False);
            Assert.That(failure.Kind,
                Is.EqualTo(GrayboxSpatialTemplateFailureKind3D.InvalidPlan));
        }

        private Fixture CreateFixture()
        {
            GameObject worldObject = Track(new GameObject("world"));
            Transform terrain = NewChild(worldObject.transform, "terrain");
            Transform resources = NewChild(worldObject.transform, "resources");
            Transform obstacles = NewChild(worldObject.transform, "obstacles");
            Material material = Track(CreateTestMaterial());
            GrayboxWorldView3D world =
                worldObject.AddComponent<GrayboxWorldView3D>();
            world.Configure(terrain, resources, obstacles, material);
            world.Generate(new WorldMapModel(OpenCells()));

            GameObject cityObject = Track(new GameObject("city"));
            cityObject.transform.position = new Vector3(0f, .5f, 0f);
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider collider = cityObject.AddComponent<BoxCollider>();
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, collider);
            city.Deployment.Restore(CityMode.Fortress, 0f);

            GameObject sessionObject = Track(new GameObject("session"));
            GrayboxBuildingSession3D session =
                sessionObject.AddComponent<GrayboxBuildingSession3D>();
            session.Configure(true);
            session.ConfigureDevelopmentFixture();

            GameObject presentationObject =
                Track(new GameObject("building-presentation"));
            Transform instanceRoot =
                NewChild(presentationObject.transform, "instances");
            Transform infrastructureRoot =
                NewChild(presentationObject.transform, "infrastructure");
            GrayboxBuildingWorldView3D presentation =
                presentationObject.AddComponent<GrayboxBuildingWorldView3D>();
            presentation.Configure(
                instanceRoot,
                infrastructureRoot,
                material,
                city);

            GameObject interactionObject = Track(new GameObject("interaction"));
            GrayboxBuildingInteractionModel3D interaction = interactionObject
                .AddComponent<GrayboxBuildingInteractionModel3D>();
            GameObject placementObject = Track(new GameObject("placement"));
            GrayboxBuildingPlacementController3D placement = placementObject
                .AddComponent<GrayboxBuildingPlacementController3D>();
            placement.Configure(
                session,
                city,
                world,
                null,
                presentation,
                interaction);

            var runtime = new SpatialTemplateRuntime();
            var controller = new GrayboxSpatialTemplateController3D(
                runtime,
                placement);
            return new Fixture(
                session,
                placement,
                runtime,
                controller);
        }

        private static GrayboxBuildingInstance3D Begin(
            Fixture fixture,
            BuildingDefinition definition,
            BuildingOrientation orientation,
            int x,
            int y)
        {
            Assert.That(fixture.Placement.TryBeginGroundConstruction(
                definition,
                orientation,
                x,
                y,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static void Record(
            SpatialTemplateRuntime runtime,
            params SpatialTemplateCell[] cells)
        {
            Assert.That(runtime.TryPrepareRecord(
                TemplateId,
                cells,
                out SpatialTemplateRecordPlan plan,
                out string error), Is.True, error);
            Assert.That(runtime.TryCommit(plan, out error), Is.True, error);
        }

        private static WorldCell[,] OpenCells()
        {
            var result = new WorldCell[
                GrayboxWorldLayout3D.WorldWidth,
                GrayboxWorldLayout3D.WorldHeight];
            for (var x = 0; x < result.GetLength(0); x++)
            for (var y = 0; y < result.GetLength(1); y++)
                result[x, y] = new WorldCell(
                    TerrainKind.Wasteland,
                    null,
                    0,
                    WorldTraversalKind.Open);
            return result;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Material CreateTestMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private readonly struct Fixture
        {
            public Fixture(
                GrayboxBuildingSession3D session,
                GrayboxBuildingPlacementController3D placement,
                SpatialTemplateRuntime runtime,
                GrayboxSpatialTemplateController3D controller)
            {
                Session = session;
                Placement = placement;
                Runtime = runtime;
                Controller = controller;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingPlacementController3D Placement { get; }
            public SpatialTemplateRuntime Runtime { get; }
            public GrayboxSpatialTemplateController3D Controller { get; }
        }

        private sealed class ThrowOnSecondPaymentPolicy :
            IGrayboxConstructionPaymentPolicy3D
        {
            private int commits;

            public bool CanFundConstruction(
                CityResourceStorageModel storage,
                string resourceId,
                int amount)
            {
                return storage.CanSpendFromNetwork(resourceId, amount);
            }

            public bool TryCommitConstructionCost(
                CityResourceStorageModel storage,
                string resourceId,
                int amount,
                out GrayboxConstructionPaymentReceipt3D receipt,
                out string error)
            {
                commits++;
                if (commits == 2)
                    throw new InvalidOperationException(
                        "synthetic payment failure");
                receipt = null;
                error = string.Empty;
                return storage.TrySpendFromNetwork(resourceId, amount);
            }

            public bool TryRollbackConstructionCost(
                GrayboxConstructionPaymentReceipt3D receipt,
                out string error)
            {
                error = string.Empty;
                return true;
            }
        }
    }
}
