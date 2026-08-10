using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WasteCity.ArtIntegration3D;
using WasteCity.Editor;
using WasteCity.Graybox3D;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainSceneContractTests
    {
        private const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        private const string FormalScenePath =
            "Assets/_Game/Scenes/FormalPrototype.unity";

        private static readonly string[] TerrainNames =
        {
            "Wasteland",
            "Rocky",
            "Wetland",
            "DeepWater",
            "Ruins",
            "Cliff",
            "Crystal"
        };

        private static readonly string[] TerrainChannels =
        {
            "BaseColor",
            "Normal",
            "Mask",
            "Height"
        };

        [TearDown]
        public void TearDown()
        {
            FirstArtTerrainAssetBuilder.HeightSourceReadableCheckpoint = null;
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }

        [Test]
        public void Scene_HasOneSerializedFirstArtTerrainPresentation()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FirstArtTerrainRenderer3D[] presenters =
                Object.FindObjectsOfType<FirstArtTerrainRenderer3D>(true);

            Assert.That(presenters.Length, Is.EqualTo(1));
            Assert.That(
                presenters[0].transform.parent.name,
                Is.EqualTo("GrayboxWorld"));
            Assert.That(
                presenters[0].name,
                Is.EqualTo("FirstArtTerrainPresentation"));
            Assert.That(presenters[0].transform.childCount, Is.Zero);
            AssertOwnerHasOnlyApprovedComponents(presenters[0]);
            Assert.That(
                presenters[0].Profile,
                Is.SameAs(
                    AssetDatabase.LoadAssetAtPath<
                        FirstArtTerrainProfile3D>(
                            FirstArtTerrainAssetBuilder.ProfilePath)));
        }

        [Test]
        public void Scene_WiresBootstrapWithoutSerializedRuntimeArtifacts()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FirstArtTerrainRenderer3D presenter =
                Object.FindObjectOfType<FirstArtTerrainRenderer3D>(true);
            GrayboxSceneBootstrap bootstrap =
                Object.FindObjectOfType<GrayboxSceneBootstrap>(true);

            Assert.That(presenter, Is.Not.Null);
            Assert.That(bootstrap, Is.Not.Null);
            AssertReference(
                bootstrap,
                "terrainPresentationBehaviour",
                presenter);
            Assert.That(
                presenter.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                Object.FindObjectsOfType<Transform>(true),
                Has.None.Matches<Transform>(
                    value => string.Equals(
                        value.name,
                        "RuntimeSurface",
                        StringComparison.Ordinal)));
            AssertSceneHasNoMissingScripts(scene);
            Assert.That(
                File.ReadAllText(ProjectAbsolutePath(ScenePath)),
                Does.Not.Contain("m_Name: RuntimeSurface"));
        }

        [Test]
        public void SceneAndProfile_ReferenceApprovedTerrainAssets()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FirstArtTerrainRenderer3D presenter =
                Object.FindObjectOfType<FirstArtTerrainRenderer3D>(true);
            FirstArtTerrainProfile3D profile =
                LoadRequired<FirstArtTerrainProfile3D>(
                    FirstArtTerrainAssetBuilder.ProfilePath);
            Material material = LoadRequired<Material>(
                FirstArtTerrainAssetBuilder.MaterialPath);
            Shader shader = LoadRequired<Shader>(
                FirstArtTerrainAssetBuilder.ShaderPath);
            Texture2DArray baseColor = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.BaseColorArrayPath);
            Texture2DArray normal = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.NormalArrayPath);
            Texture2DArray mask = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.MaskArrayPath);
            Texture2DArray height = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.HeightArrayPath);

            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.Profile, Is.SameAs(profile));
            Assert.That(profile.TryValidate(out string error), Is.True, error);
            Assert.That(profile.Material, Is.SameAs(material));
            Assert.That(profile.BaseColorArray, Is.SameAs(baseColor));
            Assert.That(profile.NormalArray, Is.SameAs(normal));
            Assert.That(profile.MaskArray, Is.SameAs(mask));
            Assert.That(profile.HeightArray, Is.SameAs(height));
            Assert.That(material.shader, Is.SameAs(shader));
            Assert.That(
                material.shader.name,
                Is.EqualTo(FirstArtTerrainProfile3D.RequiredShaderName));
            Assert.That(material.GetTexture("_BaseColorArray"), Is.SameAs(baseColor));
            Assert.That(material.GetTexture("_NormalArray"), Is.SameAs(normal));
            Assert.That(material.GetTexture("_MaskArray"), Is.SameAs(mask));
            Assert.That(material.GetTexture("_HeightArray"), Is.SameAs(height));
        }

        [Test]
        public void BuildSettings_ContainExactlyTheApprovedScenesInOrder()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.That(scenes.Length, Is.EqualTo(2));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[0].path, Is.EqualTo(ScenePath));
            Assert.That(scenes[1].enabled, Is.True);
            Assert.That(scenes[1].path, Is.EqualTo(FormalScenePath));
        }

        [Test]
        public void AuthoringValidation_AcceptsTheSerializedSceneContract()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            AssertValidationAccepts(scene);
        }

        [Test]
        public void AuthoringValidation_RejectsMissingPresenter()
        {
            SceneFixture fixture = CreateValidFixture();
            Object.DestroyImmediate(fixture.Presenter);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsUnapprovedProfileReference()
        {
            SceneFixture fixture = CreateValidFixture();
            fixture.Presenter.Configure(null);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsDuplicatePresenter()
        {
            SceneFixture fixture = CreateValidFixture();
            GameObject duplicate = new GameObject("OtherPresentation");
            SceneManager.MoveGameObjectToScene(duplicate, fixture.Scene);
            duplicate.AddComponent<FirstArtTerrainRenderer3D>()
                .Configure(fixture.Profile);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsDuplicateNamedOwnerWithoutRenderer()
        {
            SceneFixture fixture = CreateValidFixture();
            GameObject duplicate =
                new GameObject("FirstArtTerrainPresentation");
            SceneManager.MoveGameObjectToScene(duplicate, fixture.Scene);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsNullBootstrapReference()
        {
            SceneFixture fixture = CreateValidFixture();
            fixture.Bootstrap.Configure(null, null, null);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsAlternateBootstrapReference()
        {
            SceneFixture fixture = CreateValidFixture();
            GrayboxUrpScope alternate =
                fixture.Root.AddComponent<GrayboxUrpScope>();
            fixture.Bootstrap.Configure(null, null, alternate);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsRenamedOwner()
        {
            SceneFixture fixture = CreateValidFixture();
            fixture.Owner.name = "RenamedTerrainPresentation";

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsOwnerChild()
        {
            SceneFixture fixture = CreateValidFixture();
            var child = new GameObject("UnexpectedSerializedChild");
            child.transform.SetParent(fixture.Owner.transform, false);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsMeshFilter()
        {
            SceneFixture fixture = CreateValidFixture();
            fixture.Owner.AddComponent<MeshFilter>();

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsMeshRendererAndMaterial()
        {
            SceneFixture fixture = CreateValidFixture();
            MeshRenderer renderer = fixture.Owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadRequired<Material>(
                FirstArtTerrainAssetBuilder.MaterialPath);

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsCollider()
        {
            SceneFixture fixture = CreateValidFixture();
            fixture.Owner.AddComponent<BoxCollider>();

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsOtherOwnerComponent()
        {
            SceneFixture fixture = CreateValidFixture();
            fixture.Owner.AddComponent<Light>();

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void AuthoringValidation_RejectsMissingScriptAnywhereInScene()
        {
            SceneFixture fixture = CreateValidFixture();
            GameObject broken = new GameObject("BrokenScriptOwner");
            SceneManager.MoveGameObjectToScene(broken, fixture.Scene);
            GrayboxUrpScope component =
                broken.AddComponent<GrayboxUrpScope>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("m_Script").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    broken),
                Is.EqualTo(1));

            AssertValidationRejects(fixture.Scene);
        }

        [Test]
        public void ConfigureTwice_SkipsTerrainBuilderAndPreservesProtectedState()
        {
            Dictionary<string, ProtectedFileState> before =
                CaptureProtectedFileStates();
            string[] idsBefore = CaptureSceneGlobalIds();
            var builderCalled = false;
            FirstArtTerrainAssetBuilder.HeightSourceReadableCheckpoint =
                ignored =>
                {
                    builderCalled = true;
                    throw new InvalidOperationException(
                        "Valid approved assets must bypass the terrain builder.");
                };

            GrayboxSceneAuthoring.Configure();
            Dictionary<string, ProtectedFileState> afterFirst =
                CaptureProtectedFileStates();
            string[] idsAfterFirst = CaptureSceneGlobalIds();
            GrayboxSceneAuthoring.Configure();
            Dictionary<string, ProtectedFileState> afterSecond =
                CaptureProtectedFileStates();
            string[] idsAfterSecond = CaptureSceneGlobalIds();

            Assert.That(builderCalled, Is.False);
            AssertProtectedStatesEqual(before, afterFirst, "first run");
            AssertProtectedStatesEqual(before, afterSecond, "second run");
            Assert.That(idsAfterFirst, Is.EqualTo(idsBefore));
            Assert.That(idsAfterSecond, Is.EqualTo(idsBefore));
        }

        private static SceneFixture CreateValidFixture()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("GrayboxPrototype3D");
            var world = new GameObject("GrayboxWorld");
            world.transform.SetParent(root.transform, false);
            var owner = new GameObject("FirstArtTerrainPresentation");
            owner.transform.SetParent(world.transform, false);
            FirstArtTerrainProfile3D profile =
                LoadRequired<FirstArtTerrainProfile3D>(
                    FirstArtTerrainAssetBuilder.ProfilePath);
            FirstArtTerrainRenderer3D presenter =
                owner.AddComponent<FirstArtTerrainRenderer3D>();
            presenter.Configure(profile);
            var bootstrapObject = new GameObject("GrayboxSceneBootstrap");
            bootstrapObject.transform.SetParent(root.transform, false);
            GrayboxSceneBootstrap bootstrap =
                bootstrapObject.AddComponent<GrayboxSceneBootstrap>();
            bootstrap.Configure(null, null, presenter);
            return new SceneFixture(
                scene,
                root,
                owner,
                presenter,
                bootstrap,
                profile);
        }

        private static void AssertValidationAccepts(Scene scene)
        {
            MethodInfo validation = RequireValidationMethod();
            Assert.That(
                () => validation.Invoke(null, new object[] { scene }),
                Throws.Nothing);
        }

        private static void AssertValidationRejects(Scene scene)
        {
            MethodInfo validation = RequireValidationMethod();
            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => validation.Invoke(null, new object[] { scene }));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<InvalidOperationException>());
        }

        private static MethodInfo RequireValidationMethod()
        {
            MethodInfo method = typeof(GrayboxSceneAuthoring).GetMethod(
                "ValidateFirstArtTerrainContract",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static T LoadRequired<T>(string assetPath)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            return asset;
        }

        private static void AssertReference(
            Object owner,
            string propertyName,
            Object expected)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(
                property.objectReferenceValue,
                Is.SameAs(expected),
                propertyName);
        }

        private static void AssertOwnerHasOnlyApprovedComponents(
            FirstArtTerrainRenderer3D presenter)
        {
            Component[] components = presenter.GetComponents<Component>();
            Assert.That(components.Length, Is.EqualTo(2));
            Assert.That(components[0], Is.TypeOf<Transform>());
            Assert.That(components[1], Is.SameAs(presenter));
        }

        private static void AssertSceneHasNoMissingScripts(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject),
                    Is.Zero,
                    transform.name);
            }
        }

        private static Dictionary<string, ProtectedFileState>
            CaptureProtectedFileStates()
        {
            var result = new Dictionary<string, ProtectedFileState>();
            foreach (string protectedPath in ProtectedPaths())
            {
                string absolutePath = ProjectAbsolutePath(protectedPath);
                Assert.That(File.Exists(absolutePath), Is.True, protectedPath);
                string assetPath = protectedPath.EndsWith(
                    ".meta",
                    StringComparison.Ordinal)
                    ? protectedPath.Substring(0, protectedPath.Length - 5)
                    : protectedPath;
                string guid = assetPath.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal)
                    ? AssetDatabase.AssetPathToGUID(assetPath)
                    : string.Empty;
                using (FileStream stream = File.OpenRead(absolutePath))
                using (SHA256 sha = SHA256.Create())
                {
                    result.Add(
                        protectedPath,
                        new ProtectedFileState(
                            guid,
                            BitConverter.ToString(
                                    sha.ComputeHash(stream))
                                .Replace("-", string.Empty)));
                }
            }
            return result;
        }

        private static void AssertProtectedStatesEqual(
            Dictionary<string, ProtectedFileState> expected,
            Dictionary<string, ProtectedFileState> actual,
            string run)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), run);
            foreach (KeyValuePair<string, ProtectedFileState> pair in expected)
            {
                Assert.That(actual.ContainsKey(pair.Key), Is.True, pair.Key);
                Assert.That(
                    actual[pair.Key].Guid,
                    Is.EqualTo(pair.Value.Guid),
                    $"{run}: GUID changed for {pair.Key}");
                Assert.That(
                    actual[pair.Key].Hash,
                    Is.EqualTo(pair.Value.Hash),
                    $"{run}: bytes changed for {pair.Key}");
            }
        }

        private static string[] CaptureSceneGlobalIds()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var values = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                string hierarchy = HierarchyPath(transform);
                values.Add(
                    $"{hierarchy}|GameObject|" +
                    GlobalObjectId.GetGlobalObjectIdSlow(
                        transform.gameObject));
                Component[] components =
                    transform.GetComponents<Component>();
                for (var index = 0; index < components.Length; index++)
                {
                    Component component = components[index];
                    string type = component == null
                        ? "MissingScript"
                        : component.GetType().FullName;
                    values.Add(
                        $"{hierarchy}|{index}|{type}|" +
                        GlobalObjectId.GetGlobalObjectIdSlow(component));
                }
            }
            values.Sort(StringComparer.Ordinal);
            return values.ToArray();
        }

        private static string HierarchyPath(Transform transform)
        {
            string result = transform.name;
            for (Transform parent = transform.parent;
                 parent != null;
                 parent = parent.parent)
            {
                result = parent.name + "/" + result;
            }
            return result;
        }

        private static IEnumerable<string> ProtectedPaths()
        {
            const string terrainRoot =
                "Assets/_Game/Art/FirstPass/Environment/Terrain";
            foreach (string terrainName in TerrainNames)
            foreach (string channel in TerrainChannels)
            {
                yield return
                    $"{terrainRoot}/{terrainName}/" +
                    $"T_Terrain_{terrainName}_{channel}.png.meta";
            }

            string[] assetPaths =
            {
                FirstArtTerrainAssetBuilder.MaterialPath,
                FirstArtTerrainAssetBuilder.ProfilePath,
                FirstArtTerrainAssetBuilder.ShaderPath,
                FirstArtTerrainAssetBuilder.BaseColorArrayPath,
                FirstArtTerrainAssetBuilder.NormalArrayPath,
                FirstArtTerrainAssetBuilder.MaskArrayPath,
                FirstArtTerrainAssetBuilder.HeightArrayPath,
                ScenePath,
                "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset",
                "Assets/_Game/Rendering/Graybox3D/" +
                "GrayboxUniversalRenderer.asset",
                "Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat"
            };
            foreach (string assetPath in assetPaths)
            {
                yield return assetPath;
                yield return assetPath + ".meta";
            }
            yield return "ProjectSettings/EditorBuildSettings.asset";
        }

        private static string ProjectAbsolutePath(string projectPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                .FullName;
            return Path.Combine(projectRoot, projectPath);
        }

        private sealed class SceneFixture
        {
            public SceneFixture(
                Scene scene,
                GameObject root,
                GameObject owner,
                FirstArtTerrainRenderer3D presenter,
                GrayboxSceneBootstrap bootstrap,
                FirstArtTerrainProfile3D profile)
            {
                Scene = scene;
                Root = root;
                Owner = owner;
                Presenter = presenter;
                Bootstrap = bootstrap;
                Profile = profile;
            }

            public Scene Scene { get; }
            public GameObject Root { get; }
            public GameObject Owner { get; }
            public FirstArtTerrainRenderer3D Presenter { get; }
            public GrayboxSceneBootstrap Bootstrap { get; }
            public FirstArtTerrainProfile3D Profile { get; }
        }

        private sealed class ProtectedFileState
        {
            public ProtectedFileState(string guid, string hash)
            {
                Guid = guid;
                Hash = hash;
            }

            public string Guid { get; }
            public string Hash { get; }
        }
    }
}
