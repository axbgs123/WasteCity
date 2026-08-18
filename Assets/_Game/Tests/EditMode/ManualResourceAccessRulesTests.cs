using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class ManualResourceAccessRulesTests
    {
        private static readonly BuildingDefinition ThreeByOne =
            new BuildingDefinition(
                "test.building.manual-access-three-by-one",
                "Manual access footprint",
                3,
                1,
                ResourceIds.Iron,
                1);

        [Test]
        public void FormalInteractionRadiusIsExactlyTwoGridUnits()
        {
            Assert.That(
                ManualResourceAccessRules.FormalInteractionRadius,
                Is.EqualTo(2f));
        }

        [Test]
        public void DirectCityControlAlwaysAccessesItsOwnCityInventory()
        {
            Assert.That(
                EvaluateCity(
                    DirectControlTarget.City,
                    leaderRecruited: false,
                    controlledX: 1000f,
                    controlledY: -1000f,
                    footprintX: 10,
                    footprintY: 20,
                    footprintWidth: 2,
                    footprintHeight: 2),
                Is.True);
        }

        [Test]
        public void UnrecruitedLeaderCannotAccessCityOrBuildingInventory()
        {
            Assert.That(
                EvaluateCity(
                    DirectControlTarget.Leader,
                    leaderRecruited: false,
                    controlledX: 11f,
                    controlledY: 21f,
                    footprintX: 10,
                    footprintY: 20,
                    footprintWidth: 2,
                    footprintHeight: 2),
                Is.False);
            Assert.That(
                EvaluateBuilding(
                    DirectControlTarget.Leader,
                    leaderRecruited: false,
                    controlledX: 11f,
                    controlledY: 21f,
                    footprintX: 10,
                    footprintY: 20,
                    footprintWidth: 2,
                    footprintHeight: 2),
                Is.False);
        }

        [Test]
        public void RecruitedLeaderUsesInclusiveTwoGridCityFootprintDistance()
        {
            Assert.That(
                EvaluateCity(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: 13.5f,
                    controlledY: 21f,
                    footprintX: 10,
                    footprintY: 20,
                    footprintWidth: 2,
                    footprintHeight: 2),
                Is.True,
                "A point exactly two units from the city edge is accessible.");
            Assert.That(
                EvaluateCity(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: 13.501f,
                    controlledY: 21f,
                    footprintX: 10,
                    footprintY: 20,
                    footprintWidth: 2,
                    footprintHeight: 2),
                Is.False,
                "A point slightly beyond two units is inaccessible.");
        }

        [Test]
        public void BuildingAccessReevaluatesCurrentLifecycleFactsOnEveryCall()
        {
            Assert.That(
                EvaluateBuilding(
                    DirectControlTarget.City,
                    leaderRecruited: false,
                    completed: true,
                    playerOwned: true,
                    evacuationLocked: false),
                Is.True);
            Assert.That(
                EvaluateBuilding(
                    DirectControlTarget.City,
                    leaderRecruited: false,
                    completed: false,
                    playerOwned: true,
                    evacuationLocked: false),
                Is.False);
            Assert.That(
                EvaluateBuilding(
                    DirectControlTarget.City,
                    leaderRecruited: false,
                    completed: true,
                    playerOwned: false,
                    evacuationLocked: false),
                Is.False);
            Assert.That(
                EvaluateBuilding(
                    DirectControlTarget.City,
                    leaderRecruited: false,
                    completed: true,
                    playerOwned: true,
                    evacuationLocked: true),
                Is.False);
            Assert.That(
                EvaluateBuilding(
                    DirectControlTarget.City,
                    leaderRecruited: false,
                    completed: true,
                    playerOwned: true,
                    evacuationLocked: false),
                Is.True,
                "Restored current facts must be accepted without cached denial.");
        }

        [Test]
        public void BothControlTargetsUseInclusiveEuclideanBuildingDistance()
        {
            AssertBuildingDistanceBoundary(
                DirectControlTarget.City,
                leaderRecruited: false);
            AssertBuildingDistanceBoundary(
                DirectControlTarget.Leader,
                leaderRecruited: true);
        }

        private static void AssertBuildingDistanceBoundary(
            DirectControlTarget controlTarget,
            bool leaderRecruited)
        {
            Assert.That(
                EvaluateBuilding(
                    controlTarget,
                    leaderRecruited,
                    controlledX: 13.7f,
                    controlledY: 22.1f,
                    footprintX: 10,
                    footprintY: 20,
                    footprintWidth: 3,
                    footprintHeight: 1),
                Is.True,
                "Offsets 1.2 and 1.6 form an exact Euclidean distance of two.");
            Assert.That(
                EvaluateBuilding(
                    controlTarget,
                    leaderRecruited,
                    controlledX: 13.7006f,
                    controlledY: 22.1008f,
                    footprintX: 10,
                    footprintY: 20,
                    footprintWidth: 3,
                    footprintHeight: 1),
                Is.False,
                "Proportionally larger offsets are slightly beyond two.");
        }

        [Test]
        public void RotatedBuildingUsesDimensionsResolvedByOrientationRules()
        {
            int rotatedWidth = BuildingOrientationRules.Width(
                ThreeByOne,
                BuildingOrientation.East);
            int rotatedHeight = BuildingOrientationRules.Height(
                ThreeByOne,
                BuildingOrientation.East);

            Assert.That(rotatedWidth, Is.EqualTo(1));
            Assert.That(rotatedHeight, Is.EqualTo(3));
            Assert.That(
                EvaluateBuilding(
                    DirectControlTarget.City,
                    leaderRecruited: false,
                    controlledX: .5f,
                    controlledY: 4.5f,
                    footprintX: 0,
                    footprintY: 0,
                    footprintWidth: rotatedWidth,
                    footprintHeight: rotatedHeight),
                Is.True,
                "The caller-provided east-facing footprint edge is exactly two units away.");
        }

        [Test]
        public void GroundCellCenterMappingUsesSymmetricTwoUnitEdges()
        {
            var coordinates = new PlanarCoordinateMapper3D(20, 20);
            Assert.That(coordinates.TryCellToWorld(
                10,
                10,
                0f,
                out Vector3 buildingCenter), Is.True);

            float footprintCenterX = coordinates.WorldToPlane(
                    buildingCenter).x + coordinates.Width * .5f;
            float footprintCenterY = coordinates.WorldToPlane(
                    buildingCenter).y + coordinates.Height * .5f;
            Assert.That(footprintCenterX, Is.EqualTo(10f));
            Assert.That(footprintCenterY, Is.EqualTo(10f));

            float leftBoundaryX = GroundGridX(
                coordinates,
                buildingCenter + Vector3.left * 2.5f);
            float rightBoundaryX = GroundGridX(
                coordinates,
                buildingCenter + Vector3.right * 2.5f);
            float controlledY = GroundGridY(coordinates, buildingCenter);

            Assert.That(EvaluateBuilding(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: leftBoundaryX,
                    controlledY: controlledY,
                    footprintX: 10,
                    footprintY: 10,
                    footprintWidth: 1,
                    footprintHeight: 1),
                Is.True,
                "The left edge is cell center minus one half, then two units.");
            Assert.That(EvaluateBuilding(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: rightBoundaryX,
                    controlledY: controlledY,
                    footprintX: 10,
                    footprintY: 10,
                    footprintWidth: 1,
                    footprintHeight: 1),
                Is.True,
                "The right edge is cell center plus one half, then two units.");
            Assert.That(EvaluateBuilding(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: GroundGridX(
                        coordinates,
                        buildingCenter + Vector3.left * 2.501f),
                    controlledY: controlledY,
                    footprintX: 10,
                    footprintY: 10,
                    footprintWidth: 1,
                    footprintHeight: 1),
                Is.False);
            Assert.That(EvaluateBuilding(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: GroundGridX(
                        coordinates,
                        buildingCenter + Vector3.right * 2.501f),
                    controlledY: controlledY,
                    footprintX: 10,
                    footprintY: 10,
                    footprintWidth: 1,
                    footprintHeight: 1),
                Is.False);
        }

        private static float GroundGridX(
            PlanarCoordinateMapper3D coordinates,
            Vector3 world)
        {
            return coordinates.WorldToPlane(world).x +
                coordinates.Width * .5f;
        }

        private static float GroundGridY(
            PlanarCoordinateMapper3D coordinates,
            Vector3 world)
        {
            return coordinates.WorldToPlane(world).y +
                coordinates.Height * .5f;
        }

        [Test]
        public void InnerCityBuildingUsesMobileCityGroundFootprintForLeaderAccess()
        {
            Assert.That(
                GrayboxOperationsController3D.EvaluateManualBuildingAccess(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: 13.5f,
                    controlledY: 11f,
                    BuildingSite.InnerCity,
                    placementX: 7,
                    placementY: 5,
                    footprintWidth: 1,
                    footprintHeight: 1,
                    cityX: 10,
                    cityY: 10,
                    completed: true,
                    playerOwned: true,
                    evacuationLocked: false),
                Is.True,
                "The inner-grid coordinates must not replace the city's physical ground footprint.");
            Assert.That(
                GrayboxOperationsController3D.EvaluateManualBuildingAccess(
                    DirectControlTarget.Leader,
                    leaderRecruited: true,
                    controlledX: 13.501f,
                    controlledY: 11f,
                    BuildingSite.InnerCity,
                    placementX: 7,
                    placementY: 5,
                    footprintWidth: 1,
                    footprintHeight: 1,
                    cityX: 10,
                    cityY: 10,
                    completed: true,
                    playerOwned: true,
                    evacuationLocked: false),
                Is.False);
        }

        private static bool EvaluateCity(
            DirectControlTarget controlTarget,
            bool leaderRecruited,
            float controlledX,
            float controlledY,
            int footprintX,
            int footprintY,
            int footprintWidth,
            int footprintHeight)
        {
            return ManualResourceAccessRules.EvaluateCityInventory(
                controlTarget,
                leaderRecruited,
                controlledX,
                controlledY,
                footprintX,
                footprintY,
                footprintWidth,
                footprintHeight);
        }

        private static bool EvaluateBuilding(
            DirectControlTarget controlTarget,
            bool leaderRecruited,
            float controlledX = 11f,
            float controlledY = 21f,
            int footprintX = 10,
            int footprintY = 20,
            int footprintWidth = 2,
            int footprintHeight = 2,
            bool completed = true,
            bool playerOwned = true,
            bool evacuationLocked = false)
        {
            return ManualResourceAccessRules.EvaluateBuildingInventory(
                controlTarget,
                leaderRecruited,
                controlledX,
                controlledY,
                footprintX,
                footprintY,
                footprintWidth,
                footprintHeight,
                completed,
                playerOwned,
                evacuationLocked);
        }
    }
}
