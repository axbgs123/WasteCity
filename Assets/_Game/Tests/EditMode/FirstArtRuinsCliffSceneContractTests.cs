using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WasteCity.ArtIntegration3D;
using WasteCity.Editor;

namespace WasteCity.Tests
{
    public sealed class FirstArtRuinsCliffSceneContractTests
    {
        private const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        private const string TemporaryScenePath =
            "Assets/_Game/Tests/EditMode/TempFirstArtRuinsCliffSceneContract.unity";
        private const string GeometryProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset";

        private static readonly IReadOnlyDictionary<string, string> ApprovedAssets =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_CrackedFloorSlab.fbx"] = "74e0ae6e3a4e045d1879057e4ea83f5e",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_RubblePile_A.fbx"] = "a149c94a62a2e4fe2a6e71c76421e675",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_RubblePile_B.fbx"] = "349cad0c00bf34b86a2750726e7121f7",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_RebarConcreteBlock.fbx"] = "2744c2b66e972468f8cd87761f6dea3e",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_BrokenPipe.fbx"] = "592cc0ce77f7346e79d621d6e7c6c849",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_DrainageChannel.fbx"] = "a1c090a2695a940f2bb810af1f2ed3c9",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_BoundaryEdge.fbx"] = "9ab7b78a7195f4702a6f7989e00eb857",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_WornMarkingPlate.fbx"] = "6bb08a29e92924947944d0311102247d",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_Straight_A.fbx"] = "50c1bb78dbd354435ac7c1a0b28628da",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_Straight_B.fbx"] = "e13ec3db45dec4203a5c92378746bf55",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_InnerCorner.fbx"] = "c86c3d69e77304a169123123fa0f3b42",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_OuterCorner.fbx"] = "54bdbab6d36ec481ba1302edd418e863",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_EndCap.fbx"] = "45d470060d7674595b6182e9ff11af65",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_TopCap.fbx"] = "ecf58233b44324484bf41a41fc70b1e9",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_CrackedFloorSlab.prefab"] = "f341ced2e61394295b2b2529d92f2df1",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_RubblePile_A.prefab"] = "291293ada4a99454686c2363f808e10e",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_RubblePile_B.prefab"] = "127ccec2e38214c14a3bb96549a83016",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_RebarConcreteBlock.prefab"] = "ba097d85457e7469c9bf81cc6303f906",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_BrokenPipe.prefab"] = "d90169849218a4597be2b3ed9e6ee9a6",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_DrainageChannel.prefab"] = "17f6c3887760c41d59e30a6dbc38c15a",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_BoundaryEdge.prefab"] = "f064da28e02d74bdca340008cb26ad7e",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/PF_Ruins_WornMarkingPlate.prefab"] = "531e0a2a01252465c808487770194704",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/PF_Cliff_Straight_A.prefab"] = "7b37f81a7fdc3406ea9c462d56919bc6",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/PF_Cliff_Straight_B.prefab"] = "6bb8272b032f0406aa3490694a316c2a",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/PF_Cliff_InnerCorner.prefab"] = "344999e5fa45c4719a7b2f2476cfe351",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/PF_Cliff_OuterCorner.prefab"] = "c5af4a6f4c3cb4f1aae3e82bf94a200f",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/PF_Cliff_EndCap.prefab"] = "7f691f28c3d474aadb540a0ff69facce",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/PF_Cliff_TopCap.prefab"] = "65d6a7e1ca6b44584b25846b97a663c1",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_Concrete.mat"] = "be01d9d1a28234ddf91a3d8c0707190e",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_Aggregate.mat"] = "7b30f196df8784365977e5b59648eb80",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_DustFilm.mat"] = "254a4ae5ae9864b59ab84874141ca44a",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_Dust.mat"] = "926b4e68afe9f47f1abd9fc6e9ac2c25",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_DarkFloor.mat"] = "7c24f356a01d74ff5aeffb91bfbde7ac",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_DrainDark.mat"] = "fea5ba8f1bcce4af0a9966217773270d",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_Rust.mat"] = "fe3969b76bb10448896d0051363ef6a6",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Ruins_Marking.mat"] = "854500339ac804983b0822b173e5c248",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Cliff_Strata.mat"] = "507bee9b0ade044d891034434ee7d352",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Cliff_Fracture.mat"] = "9e115428bbc7540138aea316c67202dc",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Cliff_Dust.mat"] = "e896518daa9c546fdbda64e093a3b5d1",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Cliff_Rubble.mat"] = "125ad5b32ccf9436eb5e9a4064bf023e",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/MAT_Cliff_Mineral.mat"] = "8225153fe66424e57b40c90b933930e7",
                ["Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassGeometry.shader"] = "e7ef7b490240461faab128d139bdac74",
                [GeometryProfilePath] = "6b73f8e68c02943658e63225766dd256",
            };

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TemporaryScenePath);
        }

        [Test]
        public void IDEA0004_Task7_ApprovedGeometryInventoryHasExactPathGuidAndType()
        {
            Assert.That(ApprovedAssets.Count, Is.EqualTo(43));
            foreach (KeyValuePair<string, string> expected in ApprovedAssets)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(expected.Key),
                    Is.EqualTo(expected.Value), expected.Key);
                Assert.That(AssetDatabase.GUIDToAssetPath(expected.Value),
                    Is.EqualTo(expected.Key), expected.Value);
                Type expectedType = ExpectedType(expected.Key);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(
                    expected.Key,
                    expectedType);
                Assert.That(asset, Is.Not.Null, expected.Key);
                Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(expected.Key));
            }
        }

        [Test]
        public void IDEA0004_Task7_SceneReferencesGeometryProfileWithoutRuntimeArtifacts()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FirstArtTerrainRenderer3D[] presenters =
                UnityEngine.Object.FindObjectsOfType<FirstArtTerrainRenderer3D>(true);
            FirstArtRuinsCliffProfile3D approved =
                AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(
                    GeometryProfilePath);

            Assert.That(presenters.Length, Is.EqualTo(1));
            Assert.That(presenters[0].GeometryProfile, Is.SameAs(approved));
            Assert.That(presenters[0].transform.childCount, Is.Zero);
            string yaml = File.ReadAllText(ProjectAbsolutePath(ScenePath));
            foreach (string runtimeName in new[]
                     {
                         "RuntimeSurface", "RuntimeGeometry",
                         "RuinsGeometry", "CliffGeometry",
                     })
            {
                Assert.That(yaml, Does.Not.Contain("m_Name: " + runtimeName));
            }
        }

        [Test]
        public void IDEA0004_Task7_AuthoringTwicePreservesHashGlobalIdsAndApprovedGuids()
        {
            CreateTemporaryCopy();
            IReadOnlyDictionary<string, string> guidsBefore = CaptureApprovedGuids();

            InvokeConfigureAtPath();
            string firstHash = FileHash(TemporaryScenePath);
            string[] firstIds = CaptureGlobalIds();
            Scene firstScene = EditorSceneManager.OpenScene(
                TemporaryScenePath,
                OpenSceneMode.Single);
            FirstArtTerrainRenderer3D presenter =
                FindSingle<FirstArtTerrainRenderer3D>(firstScene);
            Assert.That(presenter.GeometryProfile, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(
                    GeometryProfilePath)));
            Assert.That(presenter.transform.childCount, Is.Zero);

            InvokeConfigureAtPath();

            Assert.That(FileHash(TemporaryScenePath), Is.EqualTo(firstHash));
            Assert.That(CaptureGlobalIds(), Is.EqualTo(firstIds));
            Assert.That(CaptureApprovedGuids(), Is.EqualTo(guidsBefore));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IDEA0004_Task7_BrokenOrDuplicateOwnerFailsBeforeMutation(
            bool duplicateOwner)
        {
            CreateTemporaryCopy();
            Scene scene = EditorSceneManager.OpenScene(
                TemporaryScenePath,
                OpenSceneMode.Single);
            FirstArtTerrainRenderer3D presenter = FindSingle<FirstArtTerrainRenderer3D>(scene);
            if (duplicateOwner)
            {
                var duplicate = new GameObject("FirstArtTerrainPresentation");
                duplicate.transform.SetParent(presenter.transform.parent, false);
                duplicate.AddComponent<FirstArtTerrainRenderer3D>();
            }
            else
            {
                new GameObject("RuntimeGeometry").transform.SetParent(
                    presenter.transform,
                    false);
            }
            Assert.That(EditorSceneManager.SaveScene(scene, TemporaryScenePath), Is.True);
            string hashBefore = FileHash(TemporaryScenePath);

            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                () => InvokeConfigureAtPath());

            Assert.That(thrown.InnerException, Is.Not.Null);
            StringAssert.Contains("preflight/foundation", thrown.InnerException.Message);
            Assert.That(FileHash(TemporaryScenePath), Is.EqualTo(hashBefore));
        }

        [Test]
        public void IDEA0004_Task7_MissingGeometryPPtrFailsBeforeMutationOrSave()
        {
            CreateTemporaryCopy();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string absolutePath = ProjectAbsolutePath(TemporaryScenePath);
            string source = File.ReadAllText(absolutePath);
            const string approvedGuid = "6b73f8e68c02943658e63225766dd256";
            const string missingGuid = "ffffffffffffffffffffffffffffffff";
            string broken = source.Replace(approvedGuid, missingGuid);
            Assert.That(broken, Is.Not.EqualTo(source));
            File.WriteAllText(absolutePath, broken);
            AssetDatabase.ImportAsset(
                TemporaryScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            string hashBefore = FileHash(TemporaryScenePath);
            var hooks = new HookProbe();

            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                () => InvokeConfigureAtPath(hooks));

            Assert.That(thrown.InnerException, Is.Not.Null);
            StringAssert.Contains("geometry Profile", thrown.InnerException.Message);
            Assert.That(hooks.SceneMutationEntries, Is.Zero);
            Assert.That(hooks.SceneSaveEntries, Is.Zero);
            Assert.That(FileHash(TemporaryScenePath), Is.EqualTo(hashBefore));
        }

        private static Type ExpectedType(string path)
        {
            if (path.EndsWith(".mat", StringComparison.Ordinal))
                return typeof(Material);
            if (path.EndsWith(".shader", StringComparison.Ordinal))
                return typeof(Shader);
            if (path.EndsWith(".asset", StringComparison.Ordinal))
                return typeof(FirstArtRuinsCliffProfile3D);
            return typeof(GameObject);
        }

        private static void InvokeConfigureAtPath(HookProbe hooks = null)
        {
            Type hooksType = typeof(GrayboxSceneAuthoring).GetNestedType(
                "AuthoringHooks",
                BindingFlags.NonPublic);
            Assert.That(hooksType, Is.Not.Null);
            object hookOptions = null;
            if (hooks != null)
            {
                hookOptions = Activator.CreateInstance(hooksType, true);
                SetHook(hooksType, hookOptions, "BeforeSceneMutation",
                    new Action(() => hooks.SceneMutationEntries++));
                SetHook(hooksType, hookOptions, "BeforeSceneSave",
                    new Action(() => hooks.SceneSaveEntries++));
            }
            MethodInfo method = typeof(GrayboxSceneAuthoring).GetMethod(
                "ConfigureSceneAtPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new[] { (object)TemporaryScenePath, false, hookOptions });
        }

        private static void SetHook(
            Type hooksType,
            object hooks,
            string name,
            Delegate callback)
        {
            FieldInfo field = hooksType.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(hooks, callback);
        }

        private static void CreateTemporaryCopy()
        {
            AssetDatabase.DeleteAsset(TemporaryScenePath);
            Assert.That(AssetDatabase.CopyAsset(ScenePath, TemporaryScenePath), Is.True);
            AssetDatabase.ImportAsset(TemporaryScenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static string[] CaptureGlobalIds()
        {
            Scene scene = EditorSceneManager.OpenScene(
                TemporaryScenePath,
                OpenSceneMode.Single);
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SelectMany(transform => transform.GetComponents<Component>())
                .Where(component => component != null)
                .Select(component => GlobalObjectId.GetGlobalObjectIdSlow(component).ToString())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Single();
        }

        private static IReadOnlyDictionary<string, string> CaptureApprovedGuids()
        {
            return ApprovedAssets.ToDictionary(
                pair => pair.Key,
                pair => AssetDatabase.AssetPathToGUID(pair.Key),
                StringComparer.Ordinal);
        }

        private static string FileHash(string assetPath)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(ProjectAbsolutePath(assetPath)))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static string ProjectAbsolutePath(string assetPath)
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class HookProbe
        {
            public int SceneMutationEntries;
            public int SceneSaveEntries;
        }
    }
}
