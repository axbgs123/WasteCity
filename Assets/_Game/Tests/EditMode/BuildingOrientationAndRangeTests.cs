using System;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class BuildingOrientationAndRangeTests
    {
        private static readonly BuildingDefinition ThreeByTwo =
            new BuildingDefinition("test.building.three-by-two", "Three by two", 3, 2, ResourceIds.Alloy, 1);

        [TestCase(BuildingOrientation.North, 3, 2)]
        [TestCase(BuildingOrientation.East, 2, 3)]
        [TestCase(BuildingOrientation.South, 3, 2)]
        [TestCase(BuildingOrientation.West, 2, 3)]
        public void OrientationUsesExpectedFootprintDimensions(
            BuildingOrientation orientation,
            int expectedWidth,
            int expectedHeight)
        {
            Assert.That(BuildingOrientationRules.Width(ThreeByTwo, orientation), Is.EqualTo(expectedWidth));
            Assert.That(BuildingOrientationRules.Height(ThreeByTwo, orientation), Is.EqualTo(expectedHeight));
        }

        [TestCase(BuildingOrientation.North, BuildingOrientation.East)]
        [TestCase(BuildingOrientation.East, BuildingOrientation.South)]
        [TestCase(BuildingOrientation.South, BuildingOrientation.West)]
        [TestCase(BuildingOrientation.West, BuildingOrientation.North)]
        public void ClockwiseRotationWrapsAfterWest(
            BuildingOrientation orientation,
            BuildingOrientation expected)
        {
            Assert.That(BuildingOrientationRules.RotateClockwise(orientation), Is.EqualTo(expected));
        }

        [Test]
        public void OrientationHelpersRejectValuesOutsideTheFourApprovedDirections()
        {
            var invalid = (BuildingOrientation)99;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BuildingOrientationRules.RotateClockwise(invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BuildingOrientationRules.Width(ThreeByTwo, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BuildingOrientationRules.Height(ThreeByTwo, invalid));
        }

        [Test]
        public void OrientationAwareGridApisRejectInvalidValuesWithoutMutation()
        {
            var invalid = (BuildingOrientation)99;
            var placementGrid = new BuildingGrid(5, 5);
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.Alloy, 1);

            Assert.That(
                placementGrid.ContainsFootprint(ThreeByTwo, 1, 1, invalid),
                Is.False);
            Assert.That(
                placementGrid.CanPlace(ThreeByTwo, 1, 1, BuildingSite.Ground, invalid),
                Is.False);
            Assert.That(
                placementGrid.TryPlace(
                    ThreeByTwo,
                    1,
                    1,
                    inventory,
                    false,
                    out var placed,
                    BuildingSite.Ground,
                    invalid),
                Is.False);
            Assert.That(placed, Is.Null);
            Assert.That(placementGrid.Count, Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(1));

            var restoreGrid = new BuildingGrid(5, 5);
            Assert.That(
                restoreGrid.TryRestore(
                    ThreeByTwo,
                    1,
                    1,
                    out var restored,
                    BuildingSite.Ground,
                    invalid),
                Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(restoreGrid.Count, Is.Zero);
        }

        [Test]
        public void InnerRangeRejectsValuesOutsideTheFourApprovedDirections()
        {
            Assert.That(
                BuildingRangeRules.IsInnerFootprintInBounds(
                    ThreeByTwo,
                    0,
                    0,
                    (BuildingOrientation)99),
                Is.False);
        }

        [Test]
        public void RotatedRestoreOccupiesItsRotatedFootprint()
        {
            var grid = new BuildingGrid(4, 4);

            Assert.That(
                grid.TryRestore(
                    ThreeByTwo,
                    1,
                    1,
                    out var placed,
                    BuildingSite.Ground,
                    BuildingOrientation.East),
                Is.True);

            Assert.That(placed.Orientation, Is.EqualTo(BuildingOrientation.East));
            Assert.That(grid.IsOccupied(1, 1), Is.True);
            Assert.That(grid.IsOccupied(2, 3), Is.True);
            Assert.That(grid.IsOccupied(3, 3), Is.False);
            Assert.That(grid.CanPlace(ThreeByTwo, 2, 1, BuildingSite.Ground, BuildingOrientation.East), Is.False);
        }

        [Test]
        public void RotatedPlacementRejectsFootprintsOutsideTheGrid()
        {
            var grid = new BuildingGrid(4, 4);
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.Alloy, 1);

            Assert.That(
                grid.TryPlace(
                    ThreeByTwo,
                    3,
                    1,
                    inventory,
                    false,
                    out _,
                    BuildingSite.Ground,
                    BuildingOrientation.East),
                Is.False);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(grid.Count, Is.Zero);
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void GridCanPlaceRejectsExtremeCoordinates(int x)
        {
            var grid = new BuildingGrid(5, 5);

            Assert.That(
                grid.CanPlace(
                    ThreeByTwo,
                    x,
                    0,
                    BuildingSite.Ground,
                    BuildingOrientation.North),
                Is.False);
            Assert.That(grid.Count, Is.Zero);
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void GridTryPlaceRejectsExtremeCoordinatesWithoutSpendingOrPersisting(int x)
        {
            var grid = new BuildingGrid(5, 5);
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.Alloy, 1);

            Assert.That(
                grid.TryPlace(
                    ThreeByTwo,
                    x,
                    0,
                    inventory,
                    false,
                    out var placed,
                    BuildingSite.Ground,
                    BuildingOrientation.North),
                Is.False);
            Assert.That(placed, Is.Null);
            Assert.That(grid.Count, Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(1));
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void GridTryRestoreRejectsExtremeCoordinatesWithoutPersisting(int x)
        {
            var grid = new BuildingGrid(5, 5);

            Assert.That(
                grid.TryRestore(
                    ThreeByTwo,
                    x,
                    0,
                    out var placed,
                    BuildingSite.Ground,
                    BuildingOrientation.North),
                Is.False);
            Assert.That(placed, Is.Null);
            Assert.That(grid.Count, Is.Zero);
        }

        [Test]
        public void ExistingNoOrientationApisKeepNorthCoordinatesAndCounts()
        {
            var placementInventory = new ResourceInventory(10);
            placementInventory.Add(ResourceIds.Alloy, 1);
            var placementGrid = new BuildingGrid(5, 5);

            Assert.That(placementGrid.CanPlace(ThreeByTwo, 1, 2), Is.True);
            Assert.That(placementGrid.TryPlace(ThreeByTwo, 1, 2, placementInventory, false, out var placed), Is.True);
            Assert.That(placed.Orientation, Is.EqualTo(BuildingOrientation.North));
            Assert.That(placed.X, Is.EqualTo(1));
            Assert.That(placed.Y, Is.EqualTo(2));
            Assert.That(placementGrid.Count, Is.EqualTo(1));

            var restoreGrid = new BuildingGrid(5, 5);
            Assert.That(restoreGrid.TryRestore(ThreeByTwo, 1, 2, out var restored), Is.True);
            Assert.That(restored.Orientation, Is.EqualTo(BuildingOrientation.North));
            Assert.That(restored.X, Is.EqualTo(1));
            Assert.That(restored.Y, Is.EqualTo(2));
            Assert.That(restoreGrid.Count, Is.EqualTo(1));

            var constructed = new PlacedBuilding(ThreeByTwo, 1, 2);
            Assert.That(constructed.Orientation, Is.EqualTo(BuildingOrientation.North));
        }

        [Test]
        public void GroundRangeIncludesEveryChebyshevBoundaryCellAtRadiusEight()
        {
            Assert.That(BuildingRangeRules.IsSupportedGroundRadius(8), Is.True);
            Assert.That(BuildingRangeRules.IsGroundCellInRange(10, -3, 18, 5, 8), Is.True);
            Assert.That(BuildingRangeRules.IsGroundCellInRange(10, -3, 2, -11, 8), Is.True);
            Assert.That(BuildingRangeRules.IsGroundCellInRange(10, -3, 19, 5, 8), Is.False);
        }

        [Test]
        public void GroundRangeRejectsNineAndOtherUnsupportedRadiusValues()
        {
            Assert.That(BuildingRangeRules.IsSupportedGroundRadius(9), Is.False);
            Assert.That(BuildingRangeRules.IsGroundCellInRange(0, 0, 0, 0, 9), Is.False);
            Assert.That(BuildingRangeRules.IsSupportedGroundRadius(0), Is.False);
            Assert.That(BuildingRangeRules.IsSupportedGroundRadius(10), Is.False);
            Assert.That(BuildingRangeRules.IsSupportedGroundRadius(25), Is.False);
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void GroundRangeRejectsExtremeCoordinatesWithoutThrowing(int cellX)
        {
            Assert.That(
                BuildingRangeRules.IsGroundCellInRange(0, 0, cellX, 0, 8),
                Is.False);
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InnerRangeRejectsExtremeCoordinatesWithoutThrowing(int x)
        {
            Assert.That(
                BuildingRangeRules.IsInnerFootprintInBounds(
                    ThreeByTwo,
                    x,
                    0,
                    BuildingOrientation.North),
                Is.False);
        }

        [TestCase(12)]
        [TestCase(24)]
        public void GroundRangeSupportsApprovedExtensionHooks(int radius)
        {
            Assert.That(BuildingRangeRules.IsSupportedGroundRadius(radius), Is.True);
            Assert.That(BuildingRangeRules.IsGroundCellInRange(4, 7, 4 + radius, 7 - radius, radius), Is.True);
            Assert.That(BuildingRangeRules.IsGroundCellInRange(4, 7, 5 + radius, 7, radius), Is.False);
        }

        [TestCase(BuildingOrientation.North, 3, 2)]
        [TestCase(BuildingOrientation.East, 2, 3)]
        [TestCase(BuildingOrientation.South, 3, 2)]
        [TestCase(BuildingOrientation.West, 2, 3)]
        public void InnerGridAcceptsEveryEdgeAndCornerForEachOrientation(
            BuildingOrientation orientation,
            int footprintWidth,
            int footprintHeight)
        {
            var maximumX = 8 - footprintWidth;
            var maximumY = 6 - footprintHeight;

            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, 0, 0, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, maximumX, 0, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, 0, maximumY, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, maximumX, maximumY, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, maximumX / 2, 0, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, maximumX / 2, maximumY, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, 0, maximumY / 2, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, maximumX, maximumY / 2, orientation), Is.True);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, -1, 0, orientation), Is.False);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, maximumX + 1, 0, orientation), Is.False);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, 0, -1, orientation), Is.False);
            Assert.That(BuildingRangeRules.IsInnerFootprintInBounds(ThreeByTwo, 0, maximumY + 1, orientation), Is.False);
        }
    }
}
