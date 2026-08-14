using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.Content;

namespace WasteCity.Tests
{
    public sealed class FirstArtRuinsCliffCatalogProfileTests
    {
        private const string CatalogTypeName =
            "WasteCity.ArtIntegration3D.FirstArtRuinsCliffCatalog3D, WasteCity.ArtIntegration3D";
        private const string ProfileTypeName =
            "WasteCity.ArtIntegration3D.FirstArtRuinsCliffProfile3D, WasteCity.ArtIntegration3D";
        private const string PrefabBindingTypeName =
            "WasteCity.ArtIntegration3D.FirstArtRuinsCliffPrefabBinding3D, WasteCity.ArtIntegration3D";
        private const string MaterialBindingTypeName =
            "WasteCity.ArtIntegration3D.FirstArtRuinsCliffMaterialBinding3D, WasteCity.ArtIntegration3D";

        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = ownedObjects.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(ownedObjects[index]);
            ownedObjects.Clear();
        }

        [Test]
        public void IDEA0004_CatalogFreezesApprovedMappingsRolesAndCalibration()
        {
            Type catalogType = RequireType(CatalogTypeName);
            IList entries = ReadStaticList(catalogType, "Entries");
            IList roles = ReadStaticList(catalogType, "MaterialRoles");

            ExpectedEntry[] expectedEntries = ExpectedEntries();
            Assert.That(entries.Count, Is.EqualTo(14));
            Assert.That(entries.Count, Is.EqualTo(expectedEntries.Length));
            Assert.That(roles.Count, Is.EqualTo(13));
            Quaternion expectedRotation = new Quaternion(
                -0.7071068f,
                0f,
                0f,
                0.7071067f);
            AssertQuaternion(
                ReadStatic<Quaternion>(catalogType, "SourceImportRotation"),
                expectedRotation,
                0.0000002f,
                "Catalog SourceImportRotation");
            AssertMatrix(
                ReadStatic<Matrix4x4>(catalogType, "SourceImportMatrix"),
                ExpectedSourceImportMatrix(),
                0.0000002f);
            AssertMatrix(
                Matrix4x4.Rotate(expectedRotation),
                ExpectedSourceImportMatrix(),
                0.0000002f,
                "Quaternion-derived SourceImportMatrix");

            for (int index = 0; index < expectedEntries.Length; index++)
            {
                object actual = entries[index];
                ExpectedEntry expected = expectedEntries[index];
                Assert.That(Read<string>(actual, "StableId"), Is.EqualTo(expected.StableId));
                Assert.DoesNotThrow(() => new StableId(expected.StableId));
                Assert.That(Read<string>(actual, "FbxPath"), Is.EqualTo(expected.FbxPath));
                Assert.That(Read<string>(actual, "PrefabPath"), Is.EqualTo(expected.PrefabPath));
                Assert.That(Read<object>(actual, "Family").ToString(), Is.EqualTo(expected.Family));
                Assert.That(Read<object>(actual, "Module").ToString(), Is.EqualTo(expected.Module));
                Assert.That(Read<int>(actual, "BaseRotationYDegrees"), Is.Zero);
                Assert.That(Read<int>(actual, "CanonicalConnectionMask"), Is.EqualTo(expected.Mask));
                AssertVector(Read<Vector3>(actual, "RootScale"), expected.Scale);
                AssertVector(Read<Vector3>(actual, "ChildOffset"), expected.Offset);
                AssertVector(Read<Vector3>(actual, "CalibratedBounds"), expected.Bounds);
                AssertMatrix(
                    Read<Matrix4x4>(actual, "SourceImportMatrix"),
                    ExpectedSourceImportMatrix(),
                    0.0000002f);
                CollectionAssert.AreEqual(
                    expected.MaterialRoles,
                    ToObjects(Read<object>(actual, "MaterialRoles")).Cast<string>());
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "MAT_Ruins_Concrete", "MAT_Ruins_Aggregate",
                    "MAT_Ruins_DustFilm", "MAT_Ruins_Dust",
                    "MAT_Ruins_DarkFloor", "MAT_Ruins_DrainDark",
                    "MAT_Ruins_Rust", "MAT_Ruins_Marking",
                    "MAT_Cliff_Strata", "MAT_Cliff_Fracture",
                    "MAT_Cliff_Dust", "MAT_Cliff_Rubble",
                    "MAT_Cliff_Mineral",
                },
                roles.Cast<object>().Select(value => Read<string>(value, "Name")));
            CollectionAssert.AreEqual(
                Enumerable.Repeat("Ruins", 8).Concat(Enumerable.Repeat("Cliff", 5)),
                roles.Cast<object>().Select(value => Read<object>(value, "Family").ToString()));
        }

        [Test]
        public void IDEA0004_UnityImportedRootsRawMeshesAndSlotsMatchCatalogTruth()
        {
            Type catalogType = RequireType(CatalogTypeName);
            IList entries = ReadStaticList(catalogType, "Entries");
            Quaternion expectedRotation = new Quaternion(
                -0.7071068f,
                0f,
                0f,
                0.7071067f);
            Matrix4x4 expectedMatrix = ExpectedSourceImportMatrix();

            foreach (object entry in entries)
            {
                string stableId = Read<string>(entry, "StableId");
                string fbxPath = Read<string>(entry, "FbxPath");
                GameObject importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                Assert.That(importedRoot, Is.Not.Null, stableId);
                AssertVector(importedRoot.transform.localPosition, Vector3.zero);
                AssertVector(importedRoot.transform.localScale, Vector3.one);
                AssertQuaternion(
                    importedRoot.transform.localRotation,
                    expectedRotation,
                    0.0000002f,
                    stableId);
                AssertMatrix(
                    importedRoot.transform.localToWorldMatrix,
                    expectedMatrix,
                    0.0000002f,
                    stableId);
                AssertMatrix(
                    Read<Matrix4x4>(entry, "SourceImportMatrix"),
                    importedRoot.transform.localToWorldMatrix,
                    0.0000002f,
                    stableId);

                MeshFilter[] filters = importedRoot.GetComponentsInChildren<MeshFilter>(true);
                MeshRenderer[] renderers = importedRoot.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(filters.Length, Is.EqualTo(1), stableId);
                Assert.That(renderers.Length, Is.EqualTo(1), stableId);
                Assert.That(filters[0].gameObject, Is.SameAs(importedRoot), stableId);
                Assert.That(renderers[0].gameObject, Is.SameAs(importedRoot), stableId);
                Assert.That(filters[0].sharedMesh, Is.Not.Null, stableId);

                string[] catalogSlots = ToObjects(
                        Read<object>(entry, "MaterialRoles"))
                    .Cast<string>()
                    .ToArray();
                string[] importedSlots = renderers[0].sharedMaterials
                    .Select(material => material == null ? null : material.name)
                    .ToArray();
                Assert.That(filters[0].sharedMesh.subMeshCount,
                    Is.EqualTo(catalogSlots.Length), stableId);
                CollectionAssert.AreEqual(catalogSlots, importedSlots, stableId);
            }
        }

        [Test]
        public void IDEA0004_ProfileAcceptsOnlyTheCompleteNamedCatalog()
        {
            Type profileType = RequireType(ProfileTypeName);
            Type prefabBindingType = RequireType(PrefabBindingTypeName);
            Type materialBindingType = RequireType(MaterialBindingTypeName);
            Shader shader = Track(CreateTemporaryGeometryShader());
            IList entries = ReadStaticList(RequireType(CatalogTypeName), "Entries");
            IList roles = ReadStaticList(RequireType(CatalogTypeName), "MaterialRoles");
            GameObject[] prefabs = entries.Cast<object>()
                .Select(value => Track(new GameObject(
                    System.IO.Path.GetFileNameWithoutExtension(
                        Read<string>(value, "PrefabPath")))))
                .ToArray();
            Material[] materials = roles.Cast<object>()
                .Select(value => Track(new Material(shader)
                {
                    name = Read<string>(value, "Name"),
                }))
                .ToArray();

            Array validPrefabs = BindPrefabs(prefabBindingType, entries, prefabs);
            Array validMaterials = BindMaterials(materialBindingType, roles, materials);
            ScriptableObject profile = Track(ScriptableObject.CreateInstance(profileType));
            Configure(profile, shader, validPrefabs, validMaterials);
            AssertValid(profile);

            AssertInvalid(
                profileType,
                shader,
                CopyWithoutLast(validPrefabs, prefabBindingType),
                validMaterials,
                "missing");
            AssertInvalid(
                profileType,
                shader,
                ReplaceBinding(
                    validPrefabs,
                    prefabBindingType,
                    validPrefabs.GetValue(0),
                    validPrefabs.Length - 1),
                validMaterials,
                "duplicate");
            AssertInvalid(
                profileType,
                shader,
                ReplaceBinding(
                    validPrefabs,
                    prefabBindingType,
                    Activator.CreateInstance(
                        prefabBindingType,
                        "art.ruins.unknown-module",
                        prefabs[prefabs.Length - 1]),
                    validPrefabs.Length - 1),
                validMaterials,
                "unknown");
            AssertInvalid(
                profileType,
                shader,
                SwapFirstTwo(validPrefabs, prefabBindingType),
                validMaterials,
                "order");

            string firstId = Read<string>(entries[0], "StableId");
            AssertInvalid(
                profileType,
                shader,
                ReplaceBinding(
                    validPrefabs,
                    prefabBindingType,
                    Activator.CreateInstance(prefabBindingType, firstId, prefabs[8]),
                    0),
                validMaterials,
                "name");

            AssertInvalid(
                profileType,
                shader,
                validPrefabs,
                CopyWithoutLast(validMaterials, materialBindingType),
                "missing");
            AssertInvalid(
                profileType,
                shader,
                validPrefabs,
                ReplaceBinding(
                    validMaterials,
                    materialBindingType,
                    validMaterials.GetValue(0),
                    validMaterials.Length - 1),
                "duplicate");
            AssertInvalid(
                profileType,
                shader,
                validPrefabs,
                ReplaceBinding(
                    validMaterials,
                    materialBindingType,
                    Activator.CreateInstance(
                        materialBindingType,
                        "MAT_Ruins_Unknown",
                        materials[materials.Length - 1]),
                    validMaterials.Length - 1),
                "unknown");
            AssertInvalid(
                profileType,
                shader,
                validPrefabs,
                SwapFirstTwo(validMaterials, materialBindingType),
                "order");
            AssertInvalid(
                profileType,
                shader,
                validPrefabs,
                ReplaceBinding(
                    validMaterials,
                    materialBindingType,
                    Activator.CreateInstance(
                        materialBindingType,
                        Read<string>(roles[0], "Name"),
                        materials[8]),
                    0),
                "name");

            Shader wrongShader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(wrongShader, Is.Not.Null);
            AssertInvalid(
                profileType,
                wrongShader,
                validPrefabs,
                validMaterials,
                "shader");
        }

        [Test]
        public void IDEA0004_ProfileSerializesOnlyArtReferences()
        {
            Type profileType = RequireType(ProfileTypeName);
            FieldInfo[] serializedFields = profileType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.GetCustomAttribute<SerializeField>() != null)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[] { "prefabBindings", "materialBindings", "geometryShader" },
                serializedFields.Select(field => field.Name));
            Assert.That(
                serializedFields.Select(field => field.Name),
                Has.None.Matches<string>(name =>
                    name.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("seed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("cell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("collider", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            Assert.That(type, Is.Not.Null, "Required IDEA-0004 type is missing: " + assemblyQualifiedName);
            return type;
        }

        private static IList ReadStaticList(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, type.FullName + "." + propertyName + " is missing.");
            return (IList)property.GetValue(null);
        }

        private static T ReadStatic<T>(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, type.FullName + "." + propertyName + " is missing.");
            return (T)property.GetValue(null);
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, target.GetType().FullName + "." + propertyName + " is missing.");
            return (T)property.GetValue(target);
        }

        private static IEnumerable<object> ToObjects(object sequence)
        {
            return ((IEnumerable)sequence).Cast<object>();
        }

        private static Shader CreateTemporaryGeometryShader()
        {
            const string source =
                "Shader \"WasteCity/Terrain/FirstPassGeometry\" { SubShader { Pass { } } }";
            MethodInfo method = typeof(ShaderUtil)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(candidate => candidate.Name == "CreateShaderAsset")
                .First(candidate =>
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length >= 1 && parameters[0].ParameterType == typeof(string);
                });
            object[] arguments = method.GetParameters()
                .Select(parameter => parameter.ParameterType == typeof(string)
                    ? (object)source
                    : parameter.ParameterType == typeof(bool)
                        ? true
                        : parameter.DefaultValue)
                .ToArray();
            return (Shader)method.Invoke(null, arguments);
        }

        private static Array BindPrefabs(Type bindingType, IList entries, GameObject[] prefabs)
        {
            Array result = Array.CreateInstance(bindingType, entries.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                result.SetValue(
                    Activator.CreateInstance(
                        bindingType,
                        Read<string>(entries[index], "StableId"),
                        prefabs[index]),
                    index);
            }
            return result;
        }

        private static Array BindMaterials(Type bindingType, IList roles, Material[] materials)
        {
            Array result = Array.CreateInstance(bindingType, roles.Count);
            for (int index = 0; index < roles.Count; index++)
            {
                result.SetValue(
                    Activator.CreateInstance(
                        bindingType,
                        Read<string>(roles[index], "Name"),
                        materials[index]),
                    index);
            }
            return result;
        }

        private static Array CopyWithoutLast(Array source, Type elementType)
        {
            Array result = Array.CreateInstance(elementType, source.Length - 1);
            Array.Copy(source, result, result.Length);
            return result;
        }

        private static Array ReplaceBinding(
            Array source,
            Type elementType,
            object replacement,
            int index)
        {
            Array result = Array.CreateInstance(elementType, source.Length);
            Array.Copy(source, result, source.Length);
            result.SetValue(replacement, index);
            return result;
        }

        private static Array SwapFirstTwo(Array source, Type elementType)
        {
            Array result = Array.CreateInstance(elementType, source.Length);
            Array.Copy(source, result, source.Length);
            object first = result.GetValue(0);
            result.SetValue(result.GetValue(1), 0);
            result.SetValue(first, 1);
            return result;
        }

        private static void Configure(
            ScriptableObject profile,
            Shader shader,
            Array prefabs,
            Array materials)
        {
            MethodInfo method = profile.GetType().GetMethod(
                "Configure",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(profile, new object[] { shader, prefabs, materials });
        }

        private void AssertInvalid(
            Type profileType,
            Shader shader,
            Array prefabs,
            Array materials,
            string errorFragment)
        {
            ScriptableObject profile = Track(ScriptableObject.CreateInstance(profileType));
            Configure(profile, shader, prefabs, materials);
            object[] arguments = { null };
            bool valid = (bool)profileType.GetMethod("TryValidate").Invoke(profile, arguments);
            Assert.That(valid, Is.False);
            Assert.That((string)arguments[0], Does.Contain(errorFragment).IgnoreCase);
        }

        private static void AssertValid(ScriptableObject profile)
        {
            object[] arguments = { null };
            bool valid = (bool)profile.GetType().GetMethod("TryValidate").Invoke(profile, arguments);
            Assert.That(valid, Is.True, arguments[0] as string);
            Assert.That(arguments[0], Is.Null);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            if (value != null)
                ownedObjects.Add(value);
            return value;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.000001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.000001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.000001f));
        }

        private static void AssertQuaternion(
            Quaternion actual,
            Quaternion expected,
            float tolerance,
            string context)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), context);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), context);
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), context);
            Assert.That(actual.w, Is.EqualTo(expected.w).Within(tolerance), context);
        }

        private static void AssertMatrix(
            Matrix4x4 actual,
            Matrix4x4 expected,
            float tolerance,
            string context = null)
        {
            for (int index = 0; index < 16; index++)
            {
                Assert.That(
                    actual[index],
                    Is.EqualTo(expected[index]).Within(tolerance),
                    (context ?? "SourceImportMatrix") + " element " + index);
            }
        }

        private static Matrix4x4 ExpectedSourceImportMatrix()
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = 1f;
            matrix.m01 = 0f;
            matrix.m02 = 0f;
            matrix.m03 = 0f;
            matrix.m10 = 0f;
            matrix.m11 = -0.00000011920929f;
            matrix.m12 = 0.99999994f;
            matrix.m13 = 0f;
            matrix.m20 = 0f;
            matrix.m21 = -0.99999994f;
            matrix.m22 = -0.00000011920929f;
            matrix.m23 = 0f;
            matrix.m30 = 0f;
            matrix.m31 = 0f;
            matrix.m32 = 0f;
            matrix.m33 = 1f;
            return matrix;
        }

        private static ExpectedEntry[] ExpectedEntries()
        {
            const string root = "Assets/_Game/Art/FirstPass/Environment/Terrain/";
            return new[]
            {
                E("art.ruins.cracked-floor-slab", "Ruins", "CrackedFloorSlab", "SM_Ruins_CrackedFloorSlab", "PF_Ruins_CrackedFloorSlab", 0, V(0.736612445, 0.736612445, 0.736612445), V(-0.005478553, 0.000000058, -0.033530462), V(0.9, 0.098776901, 0.777492438), "MAT_Ruins_Aggregate", "MAT_Ruins_Concrete", "MAT_Ruins_DrainDark", "MAT_Ruins_Dust", "MAT_Ruins_DustFilm"),
                E("art.ruins.rubble-pile-a", "Ruins", "RubblePileA", "SM_Ruins_RubblePile_A", "PF_Ruins_RubblePile_A", 0, V(0.930280613, 0.930280613, 0.930280613), V(0.004548970, 0.000000063, 0.026104246), V(0.9, 0.232141809, 0.719227462), "MAT_Ruins_Dust", "MAT_Ruins_Aggregate", "MAT_Ruins_Concrete"),
                E("art.ruins.rubble-pile-b", "Ruins", "RubblePileB", "SM_Ruins_RubblePile_B", "PF_Ruins_RubblePile_B", 0, V(0.643474271, 0.643474271, 0.643474271), V(0.001435036, 0.000000033, 0.000106106), V(0.9, 0.159823928, 0.405880371), "MAT_Ruins_Aggregate", "MAT_Ruins_Concrete", "MAT_Ruins_Dust", "MAT_Ruins_Rust"),
                E("art.ruins.rebar-concrete-block", "Ruins", "RebarConcreteBlock", "SM_Ruins_RebarConcreteBlock", "PF_Ruins_RebarConcreteBlock", 0, V(0.856820909, 0.856820909, 0.856820909), V(0.069655344, 0.000000059, 0.017981657), V(0.9, 0.398876840, 0.686297511), "MAT_Ruins_Aggregate", "MAT_Ruins_Concrete", "MAT_Ruins_DustFilm", "MAT_Ruins_Dust", "MAT_Ruins_Rust"),
                E("art.ruins.broken-pipe", "Ruins", "BrokenPipe", "SM_Ruins_BrokenPipe", "PF_Ruins_BrokenPipe", 0, V(0.975516330, 0.975516330, 0.975516330), V(-0.002470289, 0.000000051, -0.032442769), V(0.9, 0.641087333, 0.697020324), "MAT_Ruins_Concrete", "MAT_Ruins_Aggregate", "MAT_Ruins_Rust", "MAT_Ruins_DrainDark", "MAT_Ruins_DustFilm", "MAT_Ruins_Dust"),
                E("art.ruins.drainage-channel", "Ruins", "DrainageChannel", "SM_Ruins_DrainageChannel", "PF_Ruins_DrainageChannel", 0, V(0.818181800, 0.818181800, 0.818181800), V(0, 0.000000043, 0.005614461), V(0.9, 0.182575367, 0.519253806), "MAT_Ruins_DrainDark", "MAT_Ruins_Aggregate", "MAT_Ruins_Concrete", "MAT_Ruins_Dust", "MAT_Ruins_DustFilm"),
                E("art.ruins.boundary-edge", "Ruins", "BoundaryEdge", "SM_Ruins_BoundaryEdge", "PF_Ruins_BoundaryEdge", 0, V(0.743841862, 0.743841862, 0.743841862), V(-0.011133321, 0.000000045, 0.005296984), V(0.9, 0.154912706, 0.541050135), "MAT_Ruins_Aggregate", "MAT_Ruins_DarkFloor", "MAT_Ruins_Concrete", "MAT_Ruins_DrainDark", "MAT_Ruins_Dust", "MAT_Ruins_DustFilm"),
                E("art.ruins.worn-marking-plate", "Ruins", "WornMarkingPlate", "SM_Ruins_WornMarkingPlate", "PF_Ruins_WornMarkingPlate", 0, V(0.813866507, 0.813866507, 0.813866507), V(0.003272742, 0.000000052, -0.012372339), V(0.9, 0.054904969, 0.661674072), "MAT_Ruins_Aggregate", "MAT_Ruins_DarkFloor", "MAT_Ruins_Marking", "MAT_Ruins_DrainDark", "MAT_Ruins_Concrete", "MAT_Ruins_Dust", "MAT_Ruins_DustFilm"),
                E("art.cliff.straight-a", "Cliff", "StraightA", "SM_Cliff_Straight_A", "PF_Cliff_Straight_A", 10, V(0.326633299, 0.600246292, 0.326633299), V(-0.001620335, 0.000000051, -0.047667824), V(0.9, 0.9, 0.432732677), "MAT_Cliff_Strata", "MAT_Cliff_Fracture", "MAT_Cliff_Dust", "MAT_Cliff_Rubble", "MAT_Cliff_Mineral"),
                E("art.cliff.straight-b", "Cliff", "StraightB", "SM_Cliff_Straight_B", "PF_Cliff_Straight_B", 10, V(0.332272719, 0.596549113, 0.332272719), V(-0.010047115, 0.000000051, -0.052382713), V(0.9, 0.9, 0.452821658), "MAT_Cliff_Strata", "MAT_Cliff_Fracture", "MAT_Cliff_Dust", "MAT_Cliff_Rubble", "MAT_Cliff_Mineral"),
                E("art.cliff.inner-corner", "Cliff", "InnerCorner", "SM_Cliff_InnerCorner", "PF_Cliff_InnerCorner", 9, V(0.332957051, 0.599471748, 0.332957051), V(-0.007434510, 0.000000134, 0.093297475), V(0.9, 0.9, 0.724235948), "MAT_Cliff_Strata", "MAT_Cliff_Fracture", "MAT_Cliff_Dust", "MAT_Cliff_Rubble", "MAT_Cliff_Mineral"),
                E("art.cliff.outer-corner", "Cliff", "OuterCorner", "SM_Cliff_OuterCorner", "PF_Cliff_OuterCorner", 9, V(0.391098892, 0.596748804, 0.391098892), V(-0.115721460, 0.000000134, 0.089152067), V(0.867786007, 0.9, 0.9), "MAT_Cliff_Strata", "MAT_Cliff_Fracture", "MAT_Cliff_Dust", "MAT_Cliff_Rubble", "MAT_Cliff_Mineral"),
                E("art.cliff.end-cap", "Cliff", "EndCap", "SM_Cliff_EndCap", "PF_Cliff_EndCap", 8, V(0.369910121, 0.600328434, 0.369910121), V(-0.010333406, 0.000000050, -0.055235103), V(0.9, 0.9, 0.489557935), "MAT_Cliff_Strata", "MAT_Cliff_Fracture", "MAT_Cliff_Dust", "MAT_Cliff_Rubble", "MAT_Cliff_Mineral"),
                E("art.cliff.top-cap", "Cliff", "TopCap", "SM_Cliff_TopCap", "PF_Cliff_TopCap", 15, V(0.327494533, 0.600163695, 0.327494533), V(0.004963322, 0.000000118, -0.053226166), V(0.858200781, 0.9, 0.9), "MAT_Cliff_Strata", "MAT_Cliff_Fracture", "MAT_Cliff_Dust", "MAT_Cliff_Rubble", "MAT_Cliff_Mineral"),
            };

            ExpectedEntry E(string id, string family, string module, string fbx, string prefab, int mask, Vector3 scale, Vector3 offset, Vector3 bounds, params string[] materialRoles)
            {
                string familyDirectory = family == "Ruins" ? "Ruins/" : "Cliff/";
                return new ExpectedEntry(
                    id,
                    family,
                    module,
                    root + familyDirectory + "Models/" + fbx + ".fbx",
                    root + familyDirectory + "Runtime/Prefabs/" + prefab + ".prefab",
                    mask,
                    scale,
                    offset,
                    bounds,
                    materialRoles);
            }

            Vector3 V(double x, double y, double z)
            {
                return new Vector3((float)x, (float)y, (float)z);
            }
        }

        private readonly struct ExpectedEntry
        {
            public ExpectedEntry(
                string stableId,
                string family,
                string module,
                string fbxPath,
                string prefabPath,
                int mask,
                Vector3 scale,
                Vector3 offset,
                Vector3 bounds,
                string[] materialRoles)
            {
                StableId = stableId;
                Family = family;
                Module = module;
                FbxPath = fbxPath;
                PrefabPath = prefabPath;
                Mask = mask;
                Scale = scale;
                Offset = offset;
                Bounds = bounds;
                MaterialRoles = materialRoles;
            }

            public string StableId { get; }
            public string Family { get; }
            public string Module { get; }
            public string FbxPath { get; }
            public string PrefabPath { get; }
            public int Mask { get; }
            public Vector3 Scale { get; }
            public Vector3 Offset { get; }
            public Vector3 Bounds { get; }
            public string[] MaterialRoles { get; }
        }
    }
}
