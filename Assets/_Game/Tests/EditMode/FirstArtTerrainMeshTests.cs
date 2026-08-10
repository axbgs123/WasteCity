using System;
using NUnit.Framework;
using UnityEngine;
using WasteCity.ArtIntegration3D;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainMeshTests
    {
        [TestCase(32, 24, -16.5f, 15.5f, -12.5f, 11.5f)]
        [TestCase(96, 64, -48.5f, 47.5f, -32.5f, 31.5f)]
        public void Build_CoversCellCentersPlusHalfCell(
            int width,
            int height,
            float minX,
            float maxX,
            float minZ,
            float maxZ)
        {
            Mesh mesh = FirstArtTerrainMeshBuilder3D.Build(width, height);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Assert.That(mesh.bounds.min.x, Is.EqualTo(minX).Within(.0001f));
                Assert.That(mesh.bounds.max.x, Is.EqualTo(maxX).Within(.0001f));
                Assert.That(mesh.bounds.min.z, Is.EqualTo(minZ).Within(.0001f));
                Assert.That(mesh.bounds.max.z, Is.EqualTo(maxZ).Within(.0001f));
                foreach (Vector3 vertex in mesh.vertices)
                    Assert.That(vertex.y, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Build_AssignsStableSurfaceAttributes()
        {
            Mesh mesh = FirstArtTerrainMeshBuilder3D.Build(32, 24);
            try
            {
                Assert.That(mesh.name, Is.EqualTo("first-art.terrain.surface"));
                CollectionAssert.AreEqual(new[] { 0, 2, 1, 2, 3, 1 }, mesh.triangles);
                CollectionAssert.AreEqual(
                    new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                    mesh.normals);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        new Vector4(1f, 0f, 0f, -1f),
                        new Vector4(1f, 0f, 0f, -1f),
                        new Vector4(1f, 0f, 0f, -1f),
                        new Vector4(1f, 0f, 0f, -1f)
                    },
                    mesh.tangents);
                CollectionAssert.AreEqual(
                    new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                    mesh.uv);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(0, 1)]
        [TestCase(-1, 1)]
        [TestCase(1, 0)]
        [TestCase(1, -1)]
        public void Build_NonPositiveDimensionsThrowWithoutCreatingMesh(int width, int height)
        {
            int meshCountBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;

            Assert.That(
                () => FirstArtTerrainMeshBuilder3D.Build(width, height),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Length, Is.EqualTo(meshCountBefore));
        }
    }
}
