using System;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D;

namespace WasteCity.Tests
{
    public sealed class PlanarCoordinateMapper3DTests
    {
        [Test]
        public void Constructor_RejectsNonPositiveDimensions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlanarCoordinateMapper3D(0, 24));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlanarCoordinateMapper3D(32, 0));
        }

        [Test]
        public void CellToWorld_UsesFrozen32By24Contract()
        {
            var mapper = new PlanarCoordinateMapper3D(32, 24);

            Assert.That(
                mapper.TryCellToWorld(0, 0, 0f, out Vector3 minimum),
                Is.True);
            Assert.That(
                mapper.TryCellToWorld(8, 7, 0f, out Vector3 cityStart),
                Is.True);
            Assert.That(
                mapper.TryCellToWorld(16, 12, 0f, out Vector3 center),
                Is.True);
            Assert.That(
                mapper.TryCellToWorld(31, 23, 0f, out Vector3 maximum),
                Is.True);

            Assert.That(minimum, Is.EqualTo(new Vector3(-16f, 0f, -12f)));
            Assert.That(cityStart, Is.EqualTo(new Vector3(-8f, 0f, -5f)));
            Assert.That(center, Is.EqualTo(Vector3.zero));
            Assert.That(maximum, Is.EqualTo(new Vector3(15f, 0f, 11f)));
        }

        [Test]
        public void CellToWorld_RejectsOutsideWithoutClamping()
        {
            var mapper = new PlanarCoordinateMapper3D(32, 24);

            Assert.That(
                mapper.TryCellToWorld(-1, 0, 2f, out Vector3 left),
                Is.False);
            Assert.That(left, Is.EqualTo(default(Vector3)));
            Assert.That(
                mapper.TryCellToWorld(32, 23, 2f, out Vector3 right),
                Is.False);
            Assert.That(right, Is.EqualTo(default(Vector3)));
            Assert.That(
                mapper.TryCellToWorld(31, 24, 2f, out Vector3 top),
                Is.False);
            Assert.That(top, Is.EqualTo(default(Vector3)));
        }

        [Test]
        public void WorldToCell_UsesFloorAndIgnoresVisualY()
        {
            var mapper = new PlanarCoordinateMapper3D(32, 24);

            Assert.That(
                mapper.TryWorldToCell(
                    new Vector3(-7.01f, 999f, -4.01f),
                    out int x,
                    out int y),
                Is.True);
            Assert.That(x, Is.EqualTo(8));
            Assert.That(y, Is.EqualTo(7));
        }

        [Test]
        public void WorldToCell_RejectsOutsideWithoutClamping()
        {
            var mapper = new PlanarCoordinateMapper3D(32, 24);

            Assert.That(
                mapper.TryWorldToCell(
                    new Vector3(-16.01f, 99f, 0f),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                mapper.TryWorldToCell(
                    new Vector3(16f, -7f, 0f),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                mapper.TryWorldToCell(
                    new Vector3(0f, 3f, 12f),
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void ContinuousPlaneRoundTrip_IgnoresVisualY()
        {
            var mapper = new PlanarCoordinateMapper3D(32, 24);
            Vector2 plane = new Vector2(-8.25f, 4.75f);

            Vector3 world = mapper.PlaneToWorld(plane, 6.5f);

            Assert.That(
                world,
                Is.EqualTo(new Vector3(-8.25f, 6.5f, 4.75f)));
            Assert.That(mapper.WorldToPlane(world), Is.EqualTo(plane));
        }
    }
}
