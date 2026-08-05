using System;
using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class BuildingPlacementEvaluationTests
    {
        private static readonly BuildingDefinition DefaultDefinition =
            new BuildingDefinition(
                "test.building.default",
                "Default",
                2,
                2,
                ResourceIds.Alloy,
                1,
                placement: BuildingPlacement.Either,
                operation: BuildingOperation.MobileAllowed);

        [Test]
        public void MissingDefinitionOrGridReportsMissingReference()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(useDefaultDefinition: false)),
                BuildingPlacementFailure.MissingReference);
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(useDefaultGrid: false)),
                BuildingPlacementFailure.MissingReference);
        }

        [Test]
        public void InvalidOrientationReportsMissingReferenceInsteadOfNorthPlacement()
        {
            var evaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(orientation: (BuildingOrientation)99));

            Assert.That(evaluation.IsValid, Is.False);
            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(BuildingPlacementFailure.MissingReference));
            Assert.That(evaluation.Failures, Is.EqualTo(new[] { BuildingPlacementFailure.MissingReference }));
            Assert.That(evaluation.RotatedWidth, Is.Zero);
            Assert.That(evaluation.RotatedHeight, Is.Zero);
            Assert.That(evaluation.Footprint, Is.Empty);
        }

        [Test]
        public void FailedProjectionReportsProjectionFailure()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(projectionSucceeded: false)),
                BuildingPlacementFailure.ProjectionFailed);
        }

        [Test]
        public void FootprintOutsideGridReportsOutOfBounds()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(x: 31, y: 31, cityX: 30, cityY: 30)),
                BuildingPlacementFailure.OutOfBounds);
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void ExtremeGroundCoordinatesReturnApprovedBoundaryFailures(int x)
        {
            var evaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(x: x, y: 0, cityX: 0, cityY: 0));

            Assert.That(evaluation.IsValid, Is.False);
            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(BuildingPlacementFailure.OutOfBounds));
            Assert.That(
                evaluation.Failures,
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.OutOfBounds,
                    BuildingPlacementFailure.OutsideBuildRange
                }));
        }

        [Test]
        public void UnsupportedSiteReportsUnsupportedSite()
        {
            var groundOnly = new BuildingDefinition("test.building.ground", "Ground", 2, 2, ResourceIds.Alloy, 1);

            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(definition: groundOnly, site: BuildingSite.InnerCity, x: 1, y: 1)),
                BuildingPlacementFailure.UnsupportedSite);
        }

        [Test]
        public void MobileGroundConstructionReportsInvalidCityMode()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(cityMode: CityMode.Mobile)),
                BuildingPlacementFailure.InvalidCityMode);
        }

        [Test]
        public void GroundFootprintOutsideSupportedRadiusReportsOutsideBuildRange()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(x: 20, y: 10)),
                BuildingPlacementFailure.OutsideBuildRange);
        }

        [Test]
        public void OccupiedFootprintReportsOverlapWithoutMutatingTheGrid()
        {
            var grid = new BuildingGrid(32, 32);
            Assert.That(grid.TryRestore(DefaultDefinition, 11, 11, out _), Is.True);

            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(grid: grid)),
                BuildingPlacementFailure.Overlap);
            Assert.That(grid.Count, Is.EqualTo(1));
            Assert.That(grid.IsOccupied(11, 11), Is.True);
        }

        [Test]
        public void CityFootprintReportsCityOccupied()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(footprintTouchesCity: true)),
                BuildingPlacementFailure.CityOccupied);
        }

        [Test]
        public void ImpassableTerrainReportsInvalidTerrain()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(terrainPassable: false)),
                BuildingPlacementFailure.InvalidTerrain);
        }

        [Test]
        public void ObstacleReportsObstacleFailure()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(obstacleFree: false)),
                BuildingPlacementFailure.Obstacle);
        }

        [Test]
        public void MiningWithoutCompatibleNodeReportsNodeFailure()
        {
            var mining = new BuildingDefinition("test.building.mining", "Mining", 2, 2, ResourceIds.Alloy, 1, true);

            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(definition: mining, coversCompatibleResourceNode: false)),
                BuildingPlacementFailure.IncompatibleResourceNode);
        }

        [TestCase(null)]
        [TestCase("")]
        public void MiningWithMissingStableNodeIdReportsNodeFailure(string compatibleResourceNodeId)
        {
            var mining = new BuildingDefinition(
                "test.building.missing-node-id",
                "Missing Node Id",
                2,
                2,
                ResourceIds.Alloy,
                1,
                true);

            BuildingPlacementEvaluation evaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(
                    definition: mining,
                    coversCompatibleResourceNode: true,
                    compatibleResourceNodeId: compatibleResourceNodeId));

            AssertFailures(evaluation, BuildingPlacementFailure.IncompatibleResourceNode);
            Assert.That(evaluation.CompatibleResourceNodeId, Is.Null);
        }

        [Test]
        public void HiddenContentReportsContentUnavailable()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(contentVisible: false)),
                BuildingPlacementFailure.ContentUnavailable);
        }

        [Test]
        public void PopulationLockedContentReportsPopulationRequired()
        {
            var definition = new BuildingDefinition("test.building.population", "Population", 2, 2, ResourceIds.Alloy, 1, minimumPopulation: 10);
            var unlock = BuildingUnlockModel.Evaluate(definition, 9, _ => true, _ => 1);

            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(definition: definition, unlock: unlock)),
                BuildingPlacementFailure.PopulationRequired);
        }

        [Test]
        public void PrerequisiteLockedContentReportsPrerequisiteBuildingRequired()
        {
            var definition = new BuildingDefinition(
                "test.building.prerequisite",
                "Prerequisite",
                2,
                2,
                ResourceIds.Alloy,
                1,
                requiredBuildingId: "test.required-building");
            var unlock = BuildingUnlockModel.Evaluate(definition, 0, _ => true, _ => 0);

            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(definition: definition, unlock: unlock)),
                BuildingPlacementFailure.PrerequisiteBuildingRequired);
        }

        [Test]
        public void UnaffordableBuildingReportsInsufficientMaterials()
        {
            AssertFailures(
                BuildingPlacementRules.Evaluate(CreateRequest(canAfford: false)),
                BuildingPlacementFailure.InsufficientMaterials);
        }

        [Test]
        public void CombinedFailuresUseTheApprovedStablePriorityOrder()
        {
            var definition = new BuildingDefinition(
                "test.building.combined",
                "Combined",
                2,
                2,
                ResourceIds.Alloy,
                1,
                true,
                minimumPopulation: 10,
                requiredResearchId: "test.research",
                requiredBuildingId: "test.required-building");
            var grid = new BuildingGrid(32, 32);
            Assert.That(grid.TryRestore(definition, 20, 10, out _), Is.True);
            var unlock = BuildingUnlockModel.Evaluate(definition, 0, _ => false, _ => 0);

            var evaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(
                    definition: definition,
                    grid: grid,
                    x: 20,
                    y: 10,
                    cityMode: CityMode.Mobile,
                    projectionSucceeded: false,
                    footprintTouchesCity: true,
                    terrainPassable: false,
                    obstacleFree: false,
                    coversCompatibleResourceNode: false,
                    contentVisible: false,
                    unlock: unlock,
                    canAfford: false));

            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(BuildingPlacementFailure.ProjectionFailed));
            Assert.That(
                evaluation.Failures,
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.ProjectionFailed,
                    BuildingPlacementFailure.InvalidCityMode,
                    BuildingPlacementFailure.OutsideBuildRange,
                    BuildingPlacementFailure.Overlap,
                    BuildingPlacementFailure.CityOccupied,
                    BuildingPlacementFailure.InvalidTerrain,
                    BuildingPlacementFailure.Obstacle,
                    BuildingPlacementFailure.IncompatibleResourceNode,
                    BuildingPlacementFailure.ContentUnavailable,
                    BuildingPlacementFailure.PopulationRequired,
                    BuildingPlacementFailure.PrerequisiteBuildingRequired,
                    BuildingPlacementFailure.InsufficientMaterials
                }));
        }

        [Test]
        public void MissingReferencePrecedesProjectionAndEveryCompatibleLaterFailure()
        {
            var evaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(
                    useDefaultDefinition: false,
                    projectionSucceeded: false,
                    footprintTouchesCity: true,
                    terrainPassable: false,
                    obstacleFree: false,
                    contentVisible: false,
                    canAfford: false));

            Assert.That(
                evaluation.Failures,
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.MissingReference,
                    BuildingPlacementFailure.ProjectionFailed,
                    BuildingPlacementFailure.CityOccupied,
                    BuildingPlacementFailure.InvalidTerrain,
                    BuildingPlacementFailure.Obstacle,
                    BuildingPlacementFailure.ContentUnavailable,
                    BuildingPlacementFailure.InsufficientMaterials
                }));
        }

        [Test]
        public void ProjectionOutOfBoundsAndUnsupportedSiteUseTheirApprovedOrder()
        {
            var groundOnly = new BuildingDefinition("test.building.boundary", "Boundary", 2, 2, ResourceIds.Alloy, 1);

            var evaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(
                    definition: groundOnly,
                    site: BuildingSite.InnerCity,
                    x: 7,
                    y: 5,
                    projectionSucceeded: false,
                    footprintTouchesCity: true,
                    terrainPassable: false,
                    obstacleFree: false,
                    contentVisible: false,
                    canAfford: false));

            Assert.That(
                evaluation.Failures,
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.ProjectionFailed,
                    BuildingPlacementFailure.OutOfBounds,
                    BuildingPlacementFailure.UnsupportedSite,
                    BuildingPlacementFailure.CityOccupied,
                    BuildingPlacementFailure.InvalidTerrain,
                    BuildingPlacementFailure.Obstacle,
                    BuildingPlacementFailure.ContentUnavailable,
                    BuildingPlacementFailure.InsufficientMaterials
                }));
        }

        [Test]
        public void OutOfBoundsPrecedesInvalidCityModeForMobileGroundConstruction()
        {
            var evaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(x: 31, y: 31, cityX: 30, cityY: 30, cityMode: CityMode.Mobile));

            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(BuildingPlacementFailure.OutOfBounds));
            Assert.That(
                evaluation.Failures,
                Is.EqualTo(new[]
                {
                    BuildingPlacementFailure.OutOfBounds,
                    BuildingPlacementFailure.InvalidCityMode
                }));
        }

        [Test]
        public void CompatibleMiningNodeIsExposedOnlyForCompatibleMiningPlacement()
        {
            var mining = new BuildingDefinition("test.building.node", "Node", 2, 2, ResourceIds.Alloy, 1, true);

            var miningEvaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(definition: mining, compatibleResourceNodeId: "world.resource-node.11.11"));
            var ordinaryEvaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(compatibleResourceNodeId: "world.resource-node.11.11"));

            Assert.That(miningEvaluation.CompatibleResourceNodeId, Is.EqualTo("world.resource-node.11.11"));
            Assert.That(ordinaryEvaluation.CompatibleResourceNodeId, Is.Null);
        }

        [Test]
        public void EvaluationFailuresAreReadOnlySnapshots()
        {
            var evaluation = BuildingPlacementRules.Evaluate(CreateRequest(canAfford: false));
            var failures = evaluation.Failures as IList<BuildingPlacementFailure>;

            Assert.That(failures, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => failures.Add(BuildingPlacementFailure.Obstacle));
        }

        [Test]
        public void DefaultUnlockEvaluationDoesNotPreventPlacementEvaluation()
        {
            var request = new BuildingPlacementRequest(
                DefaultDefinition,
                new BuildingGrid(32, 32),
                BuildingSite.Ground,
                BuildingOrientation.North,
                11,
                11,
                10,
                10,
                8,
                CityMode.Fortress,
                true,
                false,
                true,
                true,
                true,
                "world.resource-node.11.11",
                true,
                default(BuildingUnlockEvaluation),
                true);

            var evaluation = BuildingPlacementRules.Evaluate(request);

            Assert.That(evaluation.IsValid, Is.True);
            Assert.That(evaluation.Failures, Is.Empty);
        }

        [Test]
        public void UnlockEvaluationReportsAllFailuresWhileLegacyApiKeepsPrimaryReason()
        {
            var definition = new BuildingDefinition(
                "test.unlock-all",
                "Unlock all",
                2,
                2,
                ResourceIds.Alloy,
                1,
                minimumPopulation: 10,
                requiredResearchId: "test.research",
                requiredBuildingId: "test.required-building");

            var evaluation = BuildingUnlockModel.Evaluate(definition, 9, _ => false, _ => 0);

            Assert.That(evaluation.IsUnlocked, Is.False);
            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(BuildingUnlockFailure.Population));
            Assert.That(
                evaluation.Failures,
                Is.EqualTo(new[]
                {
                    BuildingUnlockFailure.Population,
                    BuildingUnlockFailure.Research,
                    BuildingUnlockFailure.RequiredBuilding
                }));
            Assert.That(
                BuildingUnlockModel.IsUnlocked(definition, 9, _ => false, _ => 0, out var reason),
                Is.False);
            Assert.That(reason, Is.EqualTo(evaluation.PrimaryReason));
            Assert.That(reason, Is.EqualTo("需要人口 10"));

            var reasons = evaluation.Reasons as IList<string>;
            Assert.That(reasons, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => reasons.Add("unexpected"));
        }

        private static BuildingPlacementRequest CreateRequest(
            BuildingDefinition definition = null,
            BuildingGrid grid = null,
            bool useDefaultDefinition = true,
            bool useDefaultGrid = true,
            BuildingSite site = BuildingSite.Ground,
            BuildingOrientation orientation = BuildingOrientation.North,
            int x = 11,
            int y = 11,
            int cityX = 10,
            int cityY = 10,
            int groundRadius = 8,
            CityMode cityMode = CityMode.Fortress,
            bool projectionSucceeded = true,
            bool footprintTouchesCity = false,
            bool terrainPassable = true,
            bool obstacleFree = true,
            bool coversCompatibleResourceNode = true,
            string compatibleResourceNodeId = "world.resource-node.11.11",
            bool contentVisible = true,
            BuildingUnlockEvaluation unlock = default(BuildingUnlockEvaluation),
            bool canAfford = true)
        {
            var selectedDefinition = useDefaultDefinition ? definition ?? DefaultDefinition : null;
            var selectedGrid = useDefaultGrid ? grid ?? new BuildingGrid(32, 32) : null;
            if (unlock.PrimaryFailure == BuildingUnlockFailure.None && unlock.IsUnlocked == false)
            {
                unlock = BuildingUnlockModel.Evaluate(selectedDefinition, 0, _ => true, _ => 1);
            }

            return new BuildingPlacementRequest(
                selectedDefinition,
                selectedGrid,
                site,
                orientation,
                x,
                y,
                cityX,
                cityY,
                groundRadius,
                cityMode,
                projectionSucceeded,
                footprintTouchesCity,
                terrainPassable,
                obstacleFree,
                coversCompatibleResourceNode,
                compatibleResourceNodeId,
                contentVisible,
                unlock,
                canAfford);
        }

        private static void AssertFailures(BuildingPlacementEvaluation evaluation, params BuildingPlacementFailure[] expected)
        {
            Assert.That(evaluation.IsValid, Is.False);
            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(expected[0]));
            Assert.That(evaluation.Failures, Is.EqualTo(expected));
        }
    }
}
