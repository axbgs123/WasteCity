using System;
using NUnit.Framework;
using WasteCity.Graybox3D.Production;

namespace WasteCity.Tests
{
    public sealed class GrayboxResourceNodeIdentity3DTests
    {
        [TestCase(0, 0)]
        [TestCase(14, 9)]
        [TestCase(63, 47)]
        public void IDEA0011_ResourceNodeIdentity_RoundTripsWorldCoordinates(
            int expectedX,
            int expectedY)
        {
            string stableId = GrayboxResourceNodeIdentity3D.Create(
                expectedX,
                expectedY);

            Assert.That(
                GrayboxResourceNodeIdentity3D.TryParse(
                    stableId,
                    64,
                    48,
                    out int actualX,
                    out int actualY),
                Is.True);
            Assert.That(actualX, Is.EqualTo(expectedX));
            Assert.That(actualY, Is.EqualTo(expectedY));
        }

        [Test]
        public void IDEA0011_ResourceNodeIdentity_PreservesExistingVisualIdContract()
        {
            Assert.That(
                GrayboxResourceNodeIdentity3D.Create(14, 9),
                Is.EqualTo("world.resource-node.14.9"),
                "IDEA0011 must not invalidate IDEA0010 stable node identities.");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("world.resource-node")]
        [TestCase("world.resource-node.1")]
        [TestCase("world.resource-node.1.2.3")]
        [TestCase("world.resource.1.2")]
        [TestCase("world.resource-node.x.2")]
        [TestCase("world.resource-node.1.y")]
        [TestCase("world.resource-node.-1.2")]
        [TestCase("world.resource-node.1.-2")]
        [TestCase("world.resource-node.999999999999999999999.2")]
        public void IDEA0011_ResourceNodeIdentity_RejectsMalformedIds(
            string stableId)
        {
            Assert.That(
                GrayboxResourceNodeIdentity3D.TryParse(
                    stableId,
                    64,
                    48,
                    out int x,
                    out int y),
                Is.False);
            Assert.That(x, Is.Zero);
            Assert.That(y, Is.Zero);
        }

        [TestCase("world.resource-node.64.0")]
        [TestCase("world.resource-node.0.48")]
        [TestCase("world.resource-node.64.48")]
        public void IDEA0011_ResourceNodeIdentity_RejectsCoordinatesOutsideWorld(
            string stableId)
        {
            Assert.That(
                GrayboxResourceNodeIdentity3D.TryParse(
                    stableId,
                    64,
                    48,
                    out int x,
                    out int y),
                Is.False);
            Assert.That(x, Is.Zero);
            Assert.That(y, Is.Zero);
        }

        [TestCase(0, 48)]
        [TestCase(64, 0)]
        [TestCase(-1, 48)]
        public void IDEA0011_ResourceNodeIdentity_RejectsInvalidWorldDimensions(
            int width,
            int height)
        {
            Assert.That(
                GrayboxResourceNodeIdentity3D.TryParse(
                    "world.resource-node.0.0",
                    width,
                    height,
                    out int x,
                    out int y),
                Is.False);
            Assert.That(x, Is.Zero);
            Assert.That(y, Is.Zero);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(-1, -1)]
        public void IDEA0011_ResourceNodeIdentity_CreateRejectsNegativeCoordinates(
            int x,
            int y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GrayboxResourceNodeIdentity3D.Create(x, y));
        }
    }
}
