using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WasteCity.ArtIntegration3D;

namespace WasteCity.Tests
{
    public sealed class FirstArtRuinsCliffGeometryTests
    {
        private const string GeometryTypeName =
            "WasteCity.ArtIntegration3D.FirstArtRuinsCliffGeometry3D, WasteCity.ArtIntegration3D";
        private const string ProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset";

        [TearDown]
        public void TearDown()
        {
            FirstArtRuinsCliffGeometry3D.ResetTestConfiguration();
        }

        [Test]
        public void IDEA0004_GeometryExposesCategoryTransactionAndOwnedResult()
        {
            Type geometryType = RequireGeometryType();
            MethodInfo tryBuild = geometryType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(method =>
                    method.Name == "TryBuild" &&
                    method.GetParameters().Length == 5);
            Assert.That(tryBuild, Is.Not.Null,
                "Task 5 requires the category-level five-argument TryBuild contract.");
            ParameterInfo[] parameters = tryBuild.GetParameters();
            Assert.That(parameters[0].ParameterType,
                Is.EqualTo(typeof(FirstArtRuinsCliffProfile3D)));
            Assert.That(parameters[1].ParameterType.IsGenericType, Is.True);
            Assert.That(parameters[1].ParameterType.GetGenericTypeDefinition(),
                Is.EqualTo(typeof(System.Collections.Generic.IReadOnlyList<>)));
            Assert.That(parameters[1].ParameterType.GetGenericArguments()[0],
                Is.EqualTo(typeof(FirstArtRuinsCliffPlacement3D)));
            Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(Transform)));
            Assert.That(parameters[3].IsOut, Is.True);
            Assert.That(parameters[4].IsOut, Is.True);
            Assert.That(parameters[4].ParameterType, Is.EqualTo(typeof(string).MakeByRefType()));
            Type ownedType = parameters[3].ParameterType.GetElementType();
            Assert.That(ownedType, Is.Not.Null);
            Assert.That(typeof(IDisposable).IsAssignableFrom(ownedType), Is.True);
            Assert.That(ownedType.GetProperty("GameObject"), Is.Not.Null);
            Assert.That(ownedType.GetProperty("Mesh"), Is.Not.Null);
            Assert.That(ownedType.GetProperty("Family"), Is.Not.Null);
        }

        [Test]
        public void IDEA0004_IndexBoundaryAndRestorableCapabilityHooksExist()
        {
            Type geometryType = RequireGeometryType();
            Assert.That(
                geometryType.GetMethod(
                    "TrySelectIndexFormatForTests",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                geometryType.GetMethod(
                    "OverrideTestConfiguration",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                geometryType.GetMethod(
                    "ResetTestConfiguration",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                geometryType.GetProperty(
                    "IsUsingSystemIndexCapability",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
        }

        [TestCase(65535L, IndexFormat.UInt16)]
        [TestCase(65536L, IndexFormat.UInt32)]
        public void IDEA0004_IndexSelectorHonorsExactVertexBoundary(
            long vertexCount,
            IndexFormat expected)
        {
            Assert.That(FirstArtRuinsCliffGeometry3D.TrySelectIndexFormatForTests(
                vertexCount, 3, true, out IndexFormat actual,
                out int checkedVertices, out int checkedIndices, out string error), Is.True, error);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(checkedVertices, Is.EqualTo(vertexCount));
            Assert.That(checkedIndices, Is.EqualTo(3));
        }

        [Test]
        public void IDEA0004_IndexSelectorRejectsOverflowAndUnsupportedUInt32()
        {
            Assert.That(FirstArtRuinsCliffGeometry3D.TrySelectIndexFormatForTests(
                (long)int.MaxValue + 1, 0, true, out _, out _, out _, out _), Is.False);
            Assert.That(FirstArtRuinsCliffGeometry3D.TrySelectIndexFormatForTests(
                65536, 3, false, out _, out _, out _, out string error), Is.False);
            StringAssert.Contains("unsupported", error.ToLowerInvariant());
        }

        [TestCase(65535, IndexFormat.UInt16)]
        [TestCase(65536, IndexFormat.UInt32)]
        public void IDEA0004_BuildUsesRequiredIndexFormatWithoutTruncation(
            int vertexCount,
            IndexFormat expected)
        {
            Mesh source = CreateMesh(vertexCount);
            using (var fixture = new ProfileFixture(source))
            using (FirstArtRuinsCliffGeometry3D.OverrideTestConfiguration(true))
            {
                var parent = new GameObject("GeometryParent");
                try
                {
                    Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                        fixture.Profile,
                        OnePlacement(Matrix4x4.identity),
                        parent.transform,
                        out FirstArtRuinsCliffCategoryGeometry3D geometry,
                        out string error), Is.True, error);
                    using (geometry)
                    {
                        Assert.That(geometry.Mesh.vertexCount, Is.EqualTo(vertexCount));
                        Assert.That(geometry.Mesh.indexFormat, Is.EqualTo(expected));
                        Assert.That(geometry.Mesh.subMeshCount, Is.EqualTo(8));
                        int last = vertexCount - 1;
                        AssertVector(source.vertices[last], geometry.Mesh.vertices[last]);
                        AssertVector(source.normals[last], geometry.Mesh.normals[last]);
                        AssertVector(
                            (Vector3)source.tangents[last],
                            (Vector3)geometry.Mesh.tangents[last]);
                        Assert.That(geometry.Mesh.tangents[last].w,
                            Is.EqualTo(source.tangents[last].w));
                        Assert.That(geometry.Mesh.uv[last], Is.EqualTo(source.uv[last]));
                        Assert.That(geometry.Mesh.GetTriangles(1),
                            Is.EqualTo(new[] { 0, 1, last }),
                            "Boundary triangle must preserve its final vertex index without wrap.");
                        for (int role = 0; role < 8; role++)
                        {
                            SubMeshDescriptor range = geometry.Mesh.GetSubMesh(role);
                            int expectedCount = role == 1 ? 3 : 0;
                            int expectedStart = role <= 1 ? 0 : 3;
                            Assert.That(range.indexCount, Is.EqualTo(expectedCount),
                                "Unexpected index count for fixed ruins role " + role + ".");
                            Assert.That(range.indexStart, Is.EqualTo(expectedStart),
                                "Unexpected index start for fixed ruins role " + role + ".");
                            if (expectedCount > 0)
                            {
                                Assert.That(range.firstVertex, Is.EqualTo(0),
                                    "Non-empty role must report its first referenced vertex.");
                                Assert.That(range.vertexCount, Is.EqualTo(vertexCount),
                                    "Non-empty role must span through its final referenced vertex.");
                                int[] referenced = { 0, 1, last };
                                foreach (int referencedVertex in referenced)
                                {
                                    Assert.That(range.bounds.SqrDistance(
                                        source.vertices[referencedVertex]), Is.LessThan(0.0001f),
                                        "Non-empty role bounds must contain every referenced vertex.");
                                }
                            }
                        }
                        AssertVector(source.bounds.center, geometry.Mesh.bounds.center);
                        AssertVector(source.bounds.size, geometry.Mesh.bounds.size);
                        Assert.That(parent.transform.childCount, Is.EqualTo(1));
                        Assert.That(geometry.GameObject.GetComponents<Component>().Length,
                            Is.EqualTo(3), "Category output must only contain Transform, MeshFilter, MeshRenderer.");
                    }
                    Assert.That(parent.transform.childCount, Is.Zero);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }

        [Test]
        public void IDEA0004_TransformsChannelsIgnoresPrefabTransformAndPreservesSlots()
        {
            Mesh source = CreateMesh(3);
            Vector3[] sourceVertices =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
            };
            Vector3 sourceNormal = new Vector3(0f, 1f, 1f).normalized;
            Vector4 sourceTangent = new Vector4(1f, 1f, 0f, -1f);
            Vector3 prefabPosition = new Vector3(13f, 17f, 19f);
            source.vertices = sourceVertices;
            source.normals = Enumerable.Repeat(sourceNormal, 3).ToArray();
            source.tangents = Enumerable.Repeat(sourceTangent, 3).ToArray();
            source.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            source.SetTriangles(new[] { 0, 1, 2 }, 0);
            using (var fixture = new ProfileFixture(source, prefabPosition))
            {
                Matrix4x4 placement = Matrix4x4.TRS(
                    new Vector3(3f, 4f, 5f),
                    Quaternion.Euler(11f, 31f, 7f),
                    new Vector3(2f, 3f, 4f));
                var parent = new GameObject("GeometryParent");
                try
                {
                    Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                        fixture.Profile, OnePlacement(placement), parent.transform,
                        out FirstArtRuinsCliffCategoryGeometry3D geometry,
                        out string error), Is.True, error);
                    using (geometry)
                    {
                        AssertVector(placement.MultiplyPoint3x4(sourceVertices[1]),
                            geometry.Mesh.vertices[1]);
                        Vector3 expectedNormal =
                            placement.inverse.transpose.MultiplyVector(sourceNormal).normalized;
                        AssertVector(expectedNormal, geometry.Mesh.normals[0]);
                        Vector3 incorrectDirectNormal =
                            placement.MultiplyVector(sourceNormal).normalized;
                        Assert.That(Vector3.Distance(
                            incorrectDirectNormal,
                            geometry.Mesh.normals[0]), Is.GreaterThan(0.05f));
                        Vector3 incorrectlyDoubleTransformed = placement.MultiplyPoint3x4(
                            Matrix4x4.Translate(prefabPosition).MultiplyPoint3x4(sourceVertices[1]));
                        Assert.That(Vector3.Distance(
                            incorrectlyDoubleTransformed,
                            geometry.Mesh.vertices[1]), Is.GreaterThan(1f));
                        Vector3 expectedTangent = placement.MultiplyVector(
                            new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z));
                        expectedTangent = (expectedTangent - expectedNormal *
                            Vector3.Dot(expectedNormal, expectedTangent)).normalized;
                        AssertVector(expectedTangent, (Vector3)geometry.Mesh.tangents[0]);
                        Assert.That(geometry.Mesh.tangents[0].w, Is.EqualTo(-1f));
                        Assert.That(geometry.Mesh.uv, Is.EqualTo(source.uv));
                        Assert.That(geometry.Mesh.GetTriangles(1),
                            Is.EqualTo(new[] { 0, 1, 2 }),
                            "Aggregate is fixed ruins role 1 and winding must be preserved.");
                        Assert.That(geometry.GameObject.GetComponent<MeshRenderer>().sharedMaterials.Length,
                            Is.EqualTo(8));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }

        [Test]
        public void IDEA0004_UInt32CapabilityOverrideFailsAtomicallyAndRestores()
        {
            Mesh source = CreateMesh(65536);
            using (var fixture = new ProfileFixture(source))
            {
                var parent = new GameObject("GeometryParent");
                try
                {
                    Assert.That(FirstArtRuinsCliffGeometry3D.IsUsingSystemIndexCapability, Is.True);
                    using (FirstArtRuinsCliffGeometry3D.OverrideTestConfiguration(false))
                    {
                        Assert.That(FirstArtRuinsCliffGeometry3D.IsUsingSystemIndexCapability, Is.False);
                        Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                            fixture.Profile, OnePlacement(Matrix4x4.identity), parent.transform,
                            out FirstArtRuinsCliffCategoryGeometry3D geometry,
                            out string error), Is.False);
                        Assert.That(geometry, Is.Null);
                        StringAssert.Contains("unsupported", error.ToLowerInvariant());
                        Assert.That(parent.transform.childCount, Is.Zero);
                    }
                    Assert.That(FirstArtRuinsCliffGeometry3D.IsUsingSystemIndexCapability, Is.True);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }

        [TestCase("AfterPreflight")]
        [TestCase("AfterWritableAllocated")]
        [TestCase("AfterMeshApplied")]
        [TestCase("AfterGameObjectCreated")]
        public void IDEA0004_InjectedFailureReleasesAllCategoryOutputs(string checkpoint)
        {
            Mesh source = CreateMesh(3);
            using (var fixture = new ProfileFixture(source))
            using (FirstArtRuinsCliffGeometry3D.OverrideTestConfiguration(true, checkpoint))
            {
                var parent = new GameObject("GeometryParent");
                try
                {
                    int meshBaseline = Resources.FindObjectsOfTypeAll<Mesh>().Count(
                        mesh => mesh.name == "FirstArtRuinsCliffCombinedGeometry");
                    int objectBaseline = Resources.FindObjectsOfTypeAll<GameObject>().Count(
                        value => value.name == "RuinsGeometry");
                    Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                        fixture.Profile, OnePlacement(Matrix4x4.identity), parent.transform,
                        out FirstArtRuinsCliffCategoryGeometry3D geometry,
                        out string error), Is.False);
                    Assert.That(geometry, Is.Null);
                    StringAssert.Contains(checkpoint, error);
                    Assert.That(parent.transform.childCount, Is.Zero);
                    Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(
                        mesh => mesh.name == "FirstArtRuinsCliffCombinedGeometry"),
                        Is.EqualTo(meshBaseline), "Injected failure leaked its owned Mesh.");
                    Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(
                        value => value.name == "RuinsGeometry"),
                        Is.EqualTo(objectBaseline), "Injected failure leaked its category GameObject.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }

        [Test]
        public void IDEA0004_RealProfileBuildsAndMirroredPlacementIsRejected()
        {
            FirstArtRuinsCliffProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            var parent = new GameObject("GeometryParent");
            try
            {
                Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                    profile, OnePlacement(Matrix4x4.identity), parent.transform,
                    out FirstArtRuinsCliffCategoryGeometry3D geometry,
                    out string error), Is.True, error);
                using (geometry)
                {
                    Assert.That(geometry.Mesh.vertexCount, Is.GreaterThan(0));
                    Assert.That(geometry.Mesh.subMeshCount, Is.EqualTo(8));
                    Assert.That(geometry.GameObject.GetComponent<MeshRenderer>()
                        .sharedMaterials.Length, Is.EqualTo(8));
                }
                Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                    profile,
                    OnePlacement(Matrix4x4.identity, FirstArtRuinsCliffFamily3D.Cliff, 8),
                    parent.transform,
                    out geometry,
                    out error), Is.True, error);
                using (geometry)
                {
                    Assert.That(geometry.Mesh.subMeshCount, Is.EqualTo(5));
                    Assert.That(geometry.GameObject.GetComponent<MeshRenderer>()
                        .sharedMaterials.Length, Is.EqualTo(5));
                    Assert.That(8 + geometry.Mesh.subMeshCount, Is.EqualTo(13));
                }
                Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                    profile,
                    OnePlacement(Matrix4x4.Scale(new Vector3(-1f, 1f, 1f))),
                    parent.transform,
                    out geometry,
                    out error), Is.False);
                Assert.That(geometry, Is.Null);
                StringAssert.Contains("determinant", error);
                Assert.That(parent.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void IDEA0004_MissingChannelUnsupportedFormatAndMaterialMismatchFailAtomically()
        {
            Mesh missingNormal = CreateMesh(3);
            missingNormal.normals = Array.Empty<Vector3>();
            AssertRejected(missingNormal, null, "Normal");

            Mesh unsupportedUv = CreateUnsupportedUvMesh();
            AssertRejected(unsupportedUv, null, "TexCoord0");

            Mesh valid = CreateMesh(3);
            AssertRejected(valid, fixture => fixture.SwapFirstTwoSelectedMaterials(), "material");
        }

        [Test]
        public void IDEA0004_MixedFamiliesFailBeforeCreatingCategoryObject()
        {
            Mesh source = CreateMesh(3);
            using (var fixture = new ProfileFixture(source))
            {
                var parent = new GameObject("GeometryParent");
                try
                {
                    var placements = new[]
                    {
                        CreatePlacement(Matrix4x4.identity, FirstArtRuinsCliffFamily3D.Ruins, 0),
                        CreatePlacement(Matrix4x4.identity, FirstArtRuinsCliffFamily3D.Cliff, 8),
                    };
                    Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                        fixture.Profile, placements, parent.transform,
                        out FirstArtRuinsCliffCategoryGeometry3D geometry,
                        out string error), Is.False);
                    Assert.That(geometry, Is.Null);
                    StringAssert.Contains("mix", error.ToLowerInvariant());
                    Assert.That(parent.transform.childCount, Is.Zero);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }

        private static Type RequireGeometryType()
        {
            Type type = Type.GetType(GeometryTypeName, false);
            Assert.That(type, Is.Not.Null,
                "IDEA-0004 combined geometry type is not implemented.");
            return type;
        }

        private static IReadOnlyList<FirstArtRuinsCliffPlacement3D> OnePlacement(
            Matrix4x4 matrix,
            FirstArtRuinsCliffFamily3D family = FirstArtRuinsCliffFamily3D.Ruins,
            int catalogIndex = 0)
        {
            return new[] { CreatePlacement(matrix, family, catalogIndex) };
        }

        private static FirstArtRuinsCliffPlacement3D CreatePlacement(
            Matrix4x4 matrix,
            FirstArtRuinsCliffFamily3D family,
            int catalogIndex)
        {
            ConstructorInfo constructor = typeof(FirstArtRuinsCliffPlacement3D)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            var placement = (FirstArtRuinsCliffPlacement3D)constructor.Invoke(new object[]
            {
                family,
                catalogIndex,
                0,
                0,
                0,
                0,
                matrix,
            });
            return placement;
        }

        private static Mesh CreateMesh(int vertexCount)
        {
            var mesh = new Mesh { name = "Task5SyntheticSource" };
            if (vertexCount > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var tangents = new Vector4[vertexCount];
            var uvs = new Vector2[vertexCount];
            for (int index = 0; index < vertexCount; index++)
            {
                vertices[index] = new Vector3(index % 17, index / 17, 0f);
                normals[index] = Vector3.forward;
                tangents[index] = new Vector4(1f, 0f, 0f, 1f);
                uvs[index] = new Vector2(index % 2, (index / 2) % 2);
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            mesh.subMeshCount = 5;
            mesh.SetTriangles(new[] { 0, 1, vertexCount - 1 }, 0, false);
            for (int slot = 1; slot < 5; slot++)
                mesh.SetTriangles(Array.Empty<int>(), slot, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateUnsupportedUvMesh()
        {
            var mesh = new Mesh { name = "Task5UnsupportedUv" };
            mesh.SetVertexBufferParams(
                3,
                new VertexAttributeDescriptor(
                    VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(
                    VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
                new VertexAttributeDescriptor(
                    VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 2),
                new VertexAttributeDescriptor(
                    VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2, 3));
            mesh.SetIndexBufferParams(3, IndexFormat.UInt16);
            mesh.subMeshCount = 5;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, 3, MeshTopology.Triangles));
            for (int slot = 1; slot < 5; slot++)
                mesh.SetSubMesh(slot, new SubMeshDescriptor(3, 0, MeshTopology.Triangles));
            return mesh;
        }

        private static void AssertRejected(
            Mesh source,
            Action<ProfileFixture> mutate,
            string expectedError)
        {
            using (var fixture = new ProfileFixture(source))
            {
                mutate?.Invoke(fixture);
                var parent = new GameObject("GeometryParent");
                try
                {
                    Assert.That(FirstArtRuinsCliffGeometry3D.TryBuild(
                        fixture.Profile, OnePlacement(Matrix4x4.identity), parent.transform,
                        out FirstArtRuinsCliffCategoryGeometry3D geometry,
                        out string error), Is.False);
                    Assert.That(geometry, Is.Null);
                    StringAssert.Contains(expectedError.ToLowerInvariant(), error.ToLowerInvariant());
                    Assert.That(parent.transform.childCount, Is.Zero);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.0001f),
                "Expected " + expected + " but was " + actual + ".");
        }

        private sealed class ProfileFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> owned =
                new List<UnityEngine.Object>();

            public ProfileFixture(Mesh selectedMesh, Vector3? prefabPosition = null)
            {
                FirstArtRuinsCliffProfile3D realProfile =
                    AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(ProfilePath);
                Assert.That(realProfile, Is.Not.Null);
                Shader shader = realProfile.GeometryShader;
                var materials = new Material[FirstArtRuinsCliffCatalog3D.MaterialRoleCount];
                var materialBindings = new FirstArtRuinsCliffMaterialBinding3D[materials.Length];
                for (int index = 0; index < materials.Length; index++)
                {
                    string role = FirstArtRuinsCliffCatalog3D.MaterialRoles[index].Name;
                    materials[index] = new Material(shader) { name = role };
                    owned.Add(materials[index]);
                    materialBindings[index] =
                        new FirstArtRuinsCliffMaterialBinding3D(role, materials[index]);
                }

                var prefabBindings = new FirstArtRuinsCliffPrefabBinding3D[
                    FirstArtRuinsCliffCatalog3D.EntryCount];
                for (int index = 0; index < prefabBindings.Length; index++)
                {
                    FirstArtRuinsCliffCatalogEntry3D entry =
                        FirstArtRuinsCliffCatalog3D.Entries[index];
                    var prefab = new GameObject(
                        Path.GetFileNameWithoutExtension(entry.PrefabPath));
                    owned.Add(prefab);
                    if (index == 0)
                    {
                        prefab.transform.position = prefabPosition ?? Vector3.zero;
                        prefab.AddComponent<MeshFilter>().sharedMesh = selectedMesh;
                        MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
                        SelectedRenderer = renderer;
                        renderer.sharedMaterials = entry.MaterialRoles
                            .Select(role => materials[RoleIndex(role)])
                            .ToArray();
                    }
                    prefabBindings[index] = new FirstArtRuinsCliffPrefabBinding3D(
                        entry.StableId,
                        prefab);
                }

                Profile = ScriptableObject.CreateInstance<FirstArtRuinsCliffProfile3D>();
                owned.Add(Profile);
                owned.Add(selectedMesh);
                Profile.Configure(shader, prefabBindings, materialBindings);
                Assert.That(Profile.TryValidate(out string error), Is.True, error);
            }

            public FirstArtRuinsCliffProfile3D Profile { get; }

            public MeshRenderer SelectedRenderer { get; private set; }

            public void SwapFirstTwoSelectedMaterials()
            {
                Material[] values = SelectedRenderer.sharedMaterials;
                (values[0], values[1]) = (values[1], values[0]);
                SelectedRenderer.sharedMaterials = values;
            }

            public void Dispose()
            {
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    if (owned[index] != null)
                        UnityEngine.Object.DestroyImmediate(owned[index]);
                }
            }

            private static int RoleIndex(string role)
            {
                for (int index = 0;
                     index < FirstArtRuinsCliffCatalog3D.MaterialRoles.Count;
                     index++)
                {
                    if (FirstArtRuinsCliffCatalog3D.MaterialRoles[index].Name == role)
                        return index;
                }
                throw new InvalidOperationException("Unknown test role " + role + ".");
            }
        }
    }
}
