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
        private const string TemporaryScenePath =
            "Assets/_Game/Tests/EditMode/" +
            "TempFirstArtTerrainSceneContract.unity";
        private const string TemporaryProfilePath =
            "Assets/_Game/Tests/EditMode/" +
            "TempFirstArtTerrainProfile.asset";

        public enum RepairableSceneMutation
        {
            OwnerMissingPresenter,
            PresenterMissingProfile,
            MissingBootstrapReference
        }

        public enum MalformedSceneMutation
        {
            DuplicateNamedOwner,
            PresenterElsewhere,
            WrongParent,
            RenamedOwner,
            RuntimeSurfaceChild,
            RenamedChild,
            MeshFilter,
            MeshRendererAndMaterial,
            Collider,
            OtherOwnerComponent,
            DuplicateBootstrap,
            MissingScript,
            AlternateBootstrapReference,
            UnapprovedProfileReference
        }

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

        private ProtectedFileSnapshot activeProtectedSnapshot;

        [TearDown]
        public void TearDown()
        {
            if (activeProtectedSnapshot != null)
            {
                try
                {
                    activeProtectedSnapshot.RestoreAndDispose();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "Failed to restore the protected authoring snapshot " +
                        $"during teardown: {exception}");
                }
                activeProtectedSnapshot = null;
            }
            FirstArtTerrainAssetBuilder.HeightSourceReadableCheckpoint = null;
            try
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Failed to close the temporary terrain test scene: " +
                    exception);
            }
            try
            {
                AssetDatabase.DeleteAsset(TemporaryScenePath);
                AssetDatabase.DeleteAsset(TemporaryProfilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Failed to delete temporary terrain test assets: " +
                    exception);
            }
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
        public void ConfigureAtPath_AuthorsThePreTask7FoundationIncrementally()
        {
            CreateTemporaryScene(scene =>
            {
                FirstArtTerrainRenderer3D presenter =
                    FindSingle<FirstArtTerrainRenderer3D>(scene);
                GrayboxSceneBootstrap bootstrap =
                    FindSingle<GrayboxSceneBootstrap>(scene);
                SetReference(
                    bootstrap,
                    "terrainPresentationBehaviour",
                    null);
                Object.DestroyImmediate(presenter.gameObject);
            });
            Scene beforeScene = EditorSceneManager.OpenScene(
                TemporaryScenePath,
                OpenSceneMode.Single);
            Assert.That(
                FindAll<FirstArtTerrainRenderer3D>(beforeScene),
                Is.Empty);
            Assert.That(
                FindNamedObjects(
                    beforeScene,
                    "FirstArtTerrainPresentation"),
                Is.Empty);
            AssertReference(
                FindSingle<GrayboxSceneBootstrap>(beforeScene),
                "terrainPresentationBehaviour",
                null);
            Dictionary<string, ProtectedFileState> protectedBefore =
                CaptureProtectedFileStates();
            var firstHooks = new AuthoringHookProbe();
            var builderCalled = false;
            FirstArtTerrainAssetBuilder.HeightSourceReadableCheckpoint =
                ignored =>
                {
                    builderCalled = true;
                    throw new InvalidOperationException(
                        "Valid approved assets must bypass the terrain builder.");
                };

            InvokeConfigureAtPath(firstHooks);
            Assert.That(firstHooks.RuntimeAssetBuilderEntries, Is.Zero);
            Assert.That(firstHooks.SceneMutationEntries, Is.EqualTo(1));
            Assert.That(firstHooks.SceneSaveEntries, Is.EqualTo(1));
            string firstSceneHash = FileHash(TemporaryScenePath);
            string firstSceneGuid =
                AssetDatabase.AssetPathToGUID(TemporaryScenePath);
            string[] firstGlobalIds =
                CaptureSceneGlobalIds(TemporaryScenePath);
            Dictionary<string, ProtectedFileState> protectedAfterFirst =
                CaptureProtectedFileStates();
            Scene authoredScene = EditorSceneManager.OpenScene(
                TemporaryScenePath,
                OpenSceneMode.Single);
            AssertValidationAccepts(authoredScene);
            FirstArtTerrainRenderer3D authoredPresenter =
                FindSingle<FirstArtTerrainRenderer3D>(authoredScene);
            Assert.That(
                FindNamedObjects(
                    authoredScene,
                    "FirstArtTerrainPresentation").Count,
                Is.EqualTo(1));
            AssertReference(
                FindSingle<GrayboxSceneBootstrap>(authoredScene),
                "terrainPresentationBehaviour",
                authoredPresenter);

            var secondHooks = new AuthoringHookProbe();
            InvokeConfigureAtPath(secondHooks);

            Assert.That(builderCalled, Is.False);
            Assert.That(secondHooks.RuntimeAssetBuilderEntries, Is.Zero);
            Assert.That(secondHooks.SceneMutationEntries, Is.EqualTo(1));
            Assert.That(secondHooks.SceneSaveEntries, Is.EqualTo(1));
            Assert.That(
                FileHash(TemporaryScenePath),
                Is.EqualTo(firstSceneHash));
            Assert.That(
                AssetDatabase.AssetPathToGUID(TemporaryScenePath),
                Is.EqualTo(firstSceneGuid));
            Assert.That(
                CaptureSceneGlobalIds(TemporaryScenePath),
                Is.EqualTo(firstGlobalIds));
            AssertProtectedStatesEqual(
                protectedBefore,
                protectedAfterFirst,
                "incremental first pass");
            AssertProtectedStatesEqual(
                protectedBefore,
                CaptureProtectedFileStates(),
                "incremental second pass");
        }

        [TestCase(RepairableSceneMutation.OwnerMissingPresenter)]
        [TestCase(RepairableSceneMutation.PresenterMissingProfile)]
        [TestCase(RepairableSceneMutation.MissingBootstrapReference)]
        public void ConfigureAtPath_RepairsOnlyOwnedAbsentState(
            RepairableSceneMutation mutation)
        {
            CreateTemporaryScene(
                scene => ApplyRepairableMutation(scene, mutation));
            Dictionary<string, ProtectedFileState> protectedBefore =
                CapturePreMutationProtectedFileStates();
            var builderCalled = false;
            FirstArtTerrainAssetBuilder.HeightSourceReadableCheckpoint =
                ignored =>
                {
                    builderCalled = true;
                    throw new InvalidOperationException(
                        "Valid approved assets must bypass the terrain builder.");
                };

            InvokeConfigureAtPath();

            Scene repaired = EditorSceneManager.OpenScene(
                TemporaryScenePath,
                OpenSceneMode.Single);
            AssertValidationAccepts(repaired);
            FirstArtTerrainRenderer3D presenter =
                FindSingle<FirstArtTerrainRenderer3D>(repaired);
            Assert.That(
                FindNamedObjects(
                    repaired,
                    "FirstArtTerrainPresentation").Count,
                Is.EqualTo(1));
            AssertReference(
                FindSingle<GrayboxSceneBootstrap>(repaired),
                "terrainPresentationBehaviour",
                presenter);
            Assert.That(builderCalled, Is.False, mutation.ToString());
            AssertProtectedStatesEqual(
                protectedBefore,
                CapturePreMutationProtectedFileStates(),
                mutation.ToString());
        }

        [TestCase(MalformedSceneMutation.DuplicateNamedOwner)]
        [TestCase(MalformedSceneMutation.PresenterElsewhere)]
        [TestCase(MalformedSceneMutation.WrongParent)]
        [TestCase(MalformedSceneMutation.RenamedOwner)]
        [TestCase(MalformedSceneMutation.RuntimeSurfaceChild)]
        [TestCase(MalformedSceneMutation.RenamedChild)]
        [TestCase(MalformedSceneMutation.MeshFilter)]
        [TestCase(MalformedSceneMutation.MeshRendererAndMaterial)]
        [TestCase(MalformedSceneMutation.Collider)]
        [TestCase(MalformedSceneMutation.OtherOwnerComponent)]
        [TestCase(MalformedSceneMutation.DuplicateBootstrap)]
        [TestCase(MalformedSceneMutation.MissingScript)]
        [TestCase(MalformedSceneMutation.AlternateBootstrapReference)]
        [TestCase(MalformedSceneMutation.UnapprovedProfileReference)]
        public void ConfigureAtPath_RejectsMalformedSceneBeforeMutation(
            MalformedSceneMutation mutation)
        {
            CreateTemporaryScene(
                scene => ApplyMalformedMutation(scene, mutation));
            byte[] sceneBytesBefore =
                File.ReadAllBytes(ProjectAbsolutePath(TemporaryScenePath));
            byte[] sceneMetaBytesBefore = File.ReadAllBytes(
                ProjectAbsolutePath(TemporaryScenePath + ".meta"));
            string sceneGuidBefore =
                AssetDatabase.AssetPathToGUID(TemporaryScenePath);
            SceneStructuralSignature sceneBefore =
                CaptureSceneStructuralSignature(
                    SceneManager.GetSceneByPath(TemporaryScenePath));
            var hooks = new AuthoringHookProbe();
            ProtectedFileSnapshot protectedSnapshot =
                ProtectedFileSnapshot.Capture();
            activeProtectedSnapshot = protectedSnapshot;
            Exception originalFailure = null;
            try
            {
                TargetInvocationException exception =
                    Assert.Throws<TargetInvocationException>(
                        () => InvokeConfigureAtPath(hooks));

                Assert.That(
                    new[]
                    {
                        hooks.RuntimeAssetBuilderEntries,
                        hooks.SceneMutationEntries,
                        hooks.SceneSaveEntries
                    },
                    Is.EqualTo(new[] { 0, 0, 0 }),
                    $"{mutation}: builder/mutation/save entry counts");
                Assert.That(exception.InnerException, Is.Not.Null);
                Assert.That(
                    exception.InnerException.GetType().Name,
                    Is.EqualTo("AuthoringPreflightException"),
                    "Malformed scenes must fail the explicit authoring " +
                    "preflight, not a later InvalidOperationException.");
                Assert.That(
                    exception.InnerException.Message,
                    Does.Contain("preflight").IgnoreCase);
                Assert.That(
                    exception.InnerException.InnerException,
                    Is.TypeOf<InvalidOperationException>());
                Assert.That(
                    File.ReadAllBytes(ProjectAbsolutePath(TemporaryScenePath)),
                    Is.EqualTo(sceneBytesBefore),
                    mutation.ToString());
                Assert.That(
                    File.ReadAllBytes(
                        ProjectAbsolutePath(TemporaryScenePath + ".meta")),
                    Is.EqualTo(sceneMetaBytesBefore),
                    mutation.ToString());
                Assert.That(
                    AssetDatabase.AssetPathToGUID(TemporaryScenePath),
                    Is.EqualTo(sceneGuidBefore),
                    mutation.ToString());
                AssertSceneSignaturesEqual(
                    sceneBefore,
                    CaptureSceneStructuralSignature(
                        SceneManager.GetSceneByPath(TemporaryScenePath)),
                    mutation.ToString());
                protectedSnapshot.AssertUnchanged(mutation.ToString());
            }
            catch (Exception exception)
            {
                originalFailure = exception;
                throw;
            }
            finally
            {
                RestoreProtectedSnapshot(
                    protectedSnapshot,
                    originalFailure);
            }
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

        private static void CreateTemporaryScene(Action<Scene> mutate)
        {
            AssetDatabase.DeleteAsset(TemporaryScenePath);
            Scene source =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            mutate(source);
            Assert.That(
                EditorSceneManager.SaveScene(
                    source,
                    TemporaryScenePath,
                    true),
                Is.True);
            AssetDatabase.ImportAsset(
                TemporaryScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(
                TemporaryScenePath,
                OpenSceneMode.Single);
        }

        private static void ApplyMalformedMutation(
            Scene scene,
            MalformedSceneMutation mutation)
        {
            FirstArtTerrainRenderer3D presenter =
                FindSingle<FirstArtTerrainRenderer3D>(scene);
            GameObject owner = presenter.gameObject;
            GrayboxSceneBootstrap bootstrap =
                FindSingle<GrayboxSceneBootstrap>(scene);
            GameObject root = FindNamedObjects(
                scene,
                "GrayboxPrototype3D")[0];
            switch (mutation)
            {
                case MalformedSceneMutation.DuplicateNamedOwner:
                    MoveNewObjectToScene(
                        "FirstArtTerrainPresentation",
                        scene);
                    break;
                case MalformedSceneMutation.PresenterElsewhere:
                {
                    GameObject alternate =
                        MoveNewObjectToScene("OtherPresentation", scene);
                    alternate.AddComponent<FirstArtTerrainRenderer3D>()
                        .Configure(presenter.Profile);
                    break;
                }
                case MalformedSceneMutation.WrongParent:
                    owner.transform.SetParent(root.transform, false);
                    break;
                case MalformedSceneMutation.RenamedOwner:
                    owner.name = "RenamedTerrainPresentation";
                    break;
                case MalformedSceneMutation.RuntimeSurfaceChild:
                    new GameObject("RuntimeSurface").transform.SetParent(
                        owner.transform,
                        false);
                    break;
                case MalformedSceneMutation.RenamedChild:
                    new GameObject("UnexpectedSerializedChild")
                        .transform.SetParent(owner.transform, false);
                    break;
                case MalformedSceneMutation.MeshFilter:
                    owner.AddComponent<MeshFilter>();
                    break;
                case MalformedSceneMutation.MeshRendererAndMaterial:
                {
                    MeshRenderer renderer =
                        owner.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = LoadRequired<Material>(
                        FirstArtTerrainAssetBuilder.MaterialPath);
                    break;
                }
                case MalformedSceneMutation.Collider:
                    owner.AddComponent<BoxCollider>();
                    break;
                case MalformedSceneMutation.OtherOwnerComponent:
                    owner.AddComponent<Light>();
                    break;
                case MalformedSceneMutation.DuplicateBootstrap:
                    MoveNewObjectToScene("DuplicateBootstrap", scene)
                        .AddComponent<GrayboxSceneBootstrap>();
                    break;
                case MalformedSceneMutation.MissingScript:
                {
                    GameObject broken =
                        MoveNewObjectToScene("BrokenScriptOwner", scene);
                    GrayboxUrpScope component =
                        broken.AddComponent<GrayboxUrpScope>();
                    var serialized = new SerializedObject(component);
                    serialized.FindProperty("m_Script")
                        .objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(
                        GameObjectUtility
                            .GetMonoBehavioursWithMissingScriptCount(broken),
                        Is.EqualTo(1));
                    break;
                }
                case MalformedSceneMutation.AlternateBootstrapReference:
                {
                    GrayboxUrpScope alternate = MoveNewObjectToScene(
                            "AlternateTerrainPresentation",
                            scene)
                        .AddComponent<GrayboxUrpScope>();
                    SetReference(
                        bootstrap,
                        "terrainPresentationBehaviour",
                        alternate);
                    break;
                }
                case MalformedSceneMutation.UnapprovedProfileReference:
                {
                    AssetDatabase.DeleteAsset(TemporaryProfilePath);
                    FirstArtTerrainProfile3D alternate =
                        ScriptableObject.CreateInstance<
                            FirstArtTerrainProfile3D>();
                    AssetDatabase.CreateAsset(
                        alternate,
                        TemporaryProfilePath);
                    presenter.Configure(alternate);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mutation),
                        mutation,
                        null);
            }
        }

        private static void ApplyRepairableMutation(
            Scene scene,
            RepairableSceneMutation mutation)
        {
            FirstArtTerrainRenderer3D presenter =
                FindSingle<FirstArtTerrainRenderer3D>(scene);
            GrayboxSceneBootstrap bootstrap =
                FindSingle<GrayboxSceneBootstrap>(scene);
            switch (mutation)
            {
                case RepairableSceneMutation.OwnerMissingPresenter:
                    SetReference(
                        bootstrap,
                        "terrainPresentationBehaviour",
                        null);
                    Object.DestroyImmediate(presenter);
                    break;
                case RepairableSceneMutation.PresenterMissingProfile:
                    presenter.Configure(null);
                    break;
                case RepairableSceneMutation.MissingBootstrapReference:
                    SetReference(
                        bootstrap,
                        "terrainPresentationBehaviour",
                        null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mutation),
                        mutation,
                        null);
            }
        }

        private static GameObject MoveNewObjectToScene(
            string name,
            Scene scene)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        private static void InvokeConfigureAtPath(
            AuthoringHookProbe hooks = null)
        {
            Type hooksType = typeof(GrayboxSceneAuthoring).GetNestedType(
                "AuthoringHooks",
                BindingFlags.NonPublic);
            Assert.That(
                hooksType,
                Is.Not.Null,
                "GrayboxSceneAuthoring must expose per-call nonpublic hooks.");
            object hookOptions = Activator.CreateInstance(hooksType, true);
            hooks = hooks ?? new AuthoringHookProbe();
            SetHookMember(
                hooksType,
                hookOptions,
                "BeforeRuntimeAssetBuilder",
                new Action(() => hooks.RuntimeAssetBuilderEntries++));
            SetHookMember(
                hooksType,
                hookOptions,
                "BeforeSceneMutation",
                new Action(() => hooks.SceneMutationEntries++));
            SetHookMember(
                hooksType,
                hookOptions,
                "BeforeSceneSave",
                new Action(() => hooks.SceneSaveEntries++));
            MethodInfo method = null;
            foreach (MethodInfo candidate in
                     typeof(GrayboxSceneAuthoring).GetMethods(
                         BindingFlags.NonPublic | BindingFlags.Static))
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                if (candidate.Name == "ConfigureSceneAtPath" &&
                    parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType == typeof(bool) &&
                    parameters[2].ParameterType == hooksType)
                {
                    method = candidate;
                    break;
                }
            }
            Assert.That(method, Is.Not.Null);
            method.Invoke(
                null,
                new[] { (object)TemporaryScenePath, false, hookOptions });
        }

        private static void SetHookMember(
            Type hooksType,
            object hooks,
            string memberName,
            object value)
        {
            FieldInfo field = hooksType.GetField(
                memberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, memberName);
            field.SetValue(hooks, value);
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

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            List<T> values = FindAll<T>(scene);
            Assert.That(values.Count, Is.EqualTo(1), typeof(T).Name);
            return values[0];
        }

        private static List<T> FindAll<T>(Scene scene)
            where T : Component
        {
            var values = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                values.AddRange(root.GetComponentsInChildren<T>(true));
            return values;
        }

        private static List<GameObject> FindNamedObjects(
            Scene scene,
            string name)
        {
            var values = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(
                        transform.name,
                        name,
                        StringComparison.Ordinal))
                {
                    values.Add(transform.gameObject);
                }
            }
            return values;
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

        private static void SetReference(
            Object owner,
            string propertyName,
            Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static SceneStructuralSignature
            CaptureSceneStructuralSignature(Scene scene)
        {
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            var rows = new List<string>();
            var objectCount = 0;
            var componentCount = 0;
            var parentReferenceCount = 0;
            var objectReferenceSlotCount = 0;
            var objectReferenceCount = 0;
            var missingScriptCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                objectCount++;
                string identity = HierarchyIdentity(transform);
                string parentIdentity = transform.parent == null
                    ? "<root>"
                    : HierarchyIdentity(transform.parent);
                if (transform.parent != null)
                    parentReferenceCount++;
                rows.Add(
                    $"Object|{identity}|Parent={parentIdentity}|" +
                    $"Active={transform.gameObject.activeSelf}");

                Component[] components =
                    transform.GetComponents<Component>();
                componentCount += components.Length;
                for (var componentIndex = 0;
                     componentIndex < components.Length;
                     componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component == null)
                    {
                        missingScriptCount++;
                        rows.Add(
                            $"Component|{identity}|{componentIndex}|" +
                            "MissingScript");
                        continue;
                    }

                    string componentIdentity =
                        $"{identity}|{componentIndex}|" +
                        component.GetType().FullName;
                    rows.Add("Component|" + componentIdentity);
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    if (!property.Next(true))
                        continue;
                    do
                    {
                        if (property.propertyType !=
                            SerializedPropertyType.ObjectReference)
                        {
                            continue;
                        }

                        objectReferenceSlotCount++;
                        Object reference = property.objectReferenceValue;
                        if (reference != null)
                            objectReferenceCount++;
                        rows.Add(
                            $"Reference|{componentIdentity}|" +
                            $"{property.propertyPath}|" +
                            DescribeReference(reference, scene));
                    } while (property.Next(false));
                }
            }
            rows.Sort(StringComparer.Ordinal);
            return new SceneStructuralSignature(
                objectCount,
                componentCount,
                parentReferenceCount,
                objectReferenceSlotCount,
                objectReferenceCount,
                missingScriptCount,
                rows.ToArray());
        }

        private static string DescribeReference(Object reference, Scene scene)
        {
            if (reference == null)
                return "<null>";
            if (reference is GameObject gameObject &&
                gameObject.scene == scene)
            {
                return "SceneGameObject:" +
                       HierarchyIdentity(gameObject.transform);
            }
            if (reference is Component component &&
                component.gameObject.scene == scene)
            {
                Component[] components =
                    component.gameObject.GetComponents<Component>();
                var componentIndex = Array.IndexOf(components, component);
                return "SceneComponent:" +
                       HierarchyIdentity(component.transform) + "|" +
                       componentIndex + "|" +
                       component.GetType().FullName;
            }

            string assetPath = AssetDatabase.GetAssetPath(reference);
            if (!string.IsNullOrEmpty(assetPath) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    reference,
                    out string guid,
                    out long localId))
            {
                return $"Asset:{assetPath}|{guid}|{localId}|" +
                       reference.GetType().FullName;
            }
            return $"Other:{reference.GetType().FullName}|{reference.name}";
        }

        private static string HierarchyIdentity(Transform transform)
        {
            string result =
                $"{transform.name}[{transform.GetSiblingIndex()}]";
            for (Transform parent = transform.parent;
                 parent != null;
                 parent = parent.parent)
            {
                result = $"{parent.name}[{parent.GetSiblingIndex()}]/" + result;
            }
            return result;
        }

        private static void AssertSceneSignaturesEqual(
            SceneStructuralSignature expected,
            SceneStructuralSignature actual,
            string context)
        {
            Assert.That(
                actual.ObjectCount,
                Is.EqualTo(expected.ObjectCount),
                $"{context}: scene object count changed");
            Assert.That(
                actual.ComponentCount,
                Is.EqualTo(expected.ComponentCount),
                $"{context}: scene component count changed");
            Assert.That(
                actual.ParentReferenceCount,
                Is.EqualTo(expected.ParentReferenceCount),
                $"{context}: scene parent relationship count changed");
            Assert.That(
                actual.ObjectReferenceSlotCount,
                Is.EqualTo(expected.ObjectReferenceSlotCount),
                $"{context}: serialized reference slot count changed");
            Assert.That(
                actual.ObjectReferenceCount,
                Is.EqualTo(expected.ObjectReferenceCount),
                $"{context}: serialized non-null reference count changed");
            Assert.That(
                actual.MissingScriptCount,
                Is.EqualTo(expected.MissingScriptCount),
                $"{context}: missing script count changed");
            Assert.That(
                actual.Rows,
                Is.EqualTo(expected.Rows),
                $"{context}: in-memory scene structure changed");
        }

        private void RestoreProtectedSnapshot(
            ProtectedFileSnapshot snapshot,
            Exception originalFailure)
        {
            try
            {
                snapshot.RestoreAndDispose();
            }
            catch (Exception restorationFailure)
            {
                if (originalFailure == null)
                    throw;
                originalFailure.Data["ProtectedSnapshotRestoreFailure"] =
                    restorationFailure.ToString();
                Debug.LogError(
                    "Protected snapshot restoration also failed; preserving " +
                    $"the original assertion failure: {restorationFailure}");
            }
            finally
            {
                if (snapshot.IsDisposed &&
                    ReferenceEquals(activeProtectedSnapshot, snapshot))
                {
                    activeProtectedSnapshot = null;
                }
            }
        }

        private static Dictionary<string, ProtectedFileState>
            CaptureProtectedFileStates()
        {
            return CaptureProtectedFileStates(ProtectedPaths());
        }

        private static Dictionary<string, ProtectedFileState>
            CapturePreMutationProtectedFileStates()
        {
            return CaptureProtectedFileStates(
                PreMutationProtectedPaths());
        }

        private static Dictionary<string, ProtectedFileState>
            CaptureProtectedFileStates(IEnumerable<string> protectedPaths)
        {
            var result = new Dictionary<string, ProtectedFileState>();
            foreach (string protectedPath in protectedPaths)
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

        private static Dictionary<string, ProtectedFileState>
            CaptureProtectedFileStatesForRecovery(
                IEnumerable<string> protectedPaths)
        {
            var result = new Dictionary<string, ProtectedFileState>();
            foreach (string protectedPath in protectedPaths)
            {
                string absolutePath = ProjectAbsolutePath(protectedPath);
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
                if (!File.Exists(absolutePath))
                {
                    result.Add(
                        protectedPath,
                        new ProtectedFileState(guid, "<missing>"));
                    continue;
                }
                using (FileStream stream = File.OpenRead(absolutePath))
                using (SHA256 sha = SHA256.Create())
                {
                    result.Add(
                        protectedPath,
                        new ProtectedFileState(
                            guid,
                            BitConverter.ToString(sha.ComputeHash(stream))
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
            return CaptureSceneGlobalIds(ScenePath);
        }

        private static string[] CaptureSceneGlobalIds(string scenePath)
        {
            Scene scene =
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
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

        private static string FileHash(string projectPath)
        {
            using (FileStream stream =
                   File.OpenRead(ProjectAbsolutePath(projectPath)))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
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

        private static IEnumerable<string> PreMutationProtectedPaths()
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

            string[] protectedAssetPaths =
            {
                FirstArtTerrainAssetBuilder.MaterialPath,
                FirstArtTerrainAssetBuilder.MaterialPath + ".meta",
                FirstArtTerrainAssetBuilder.ProfilePath,
                FirstArtTerrainAssetBuilder.ProfilePath + ".meta",
                FirstArtTerrainAssetBuilder.ShaderPath,
                FirstArtTerrainAssetBuilder.ShaderPath + ".meta",
                FirstArtTerrainAssetBuilder.BaseColorArrayPath + ".meta",
                FirstArtTerrainAssetBuilder.NormalArrayPath + ".meta",
                FirstArtTerrainAssetBuilder.MaskArrayPath + ".meta",
                FirstArtTerrainAssetBuilder.HeightArrayPath + ".meta",
                ScenePath,
                ScenePath + ".meta",
                "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset",
                "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset.meta",
                "Assets/_Game/Rendering/Graybox3D/" +
                "GrayboxUniversalRenderer.asset",
                "Assets/_Game/Rendering/Graybox3D/" +
                "GrayboxUniversalRenderer.asset.meta",
                "Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat",
                "Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat.meta",
                "ProjectSettings/EditorBuildSettings.asset"
            };
            foreach (string protectedPath in protectedAssetPaths)
                yield return protectedPath;
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

        private sealed class SceneStructuralSignature
        {
            public SceneStructuralSignature(
                int objectCount,
                int componentCount,
                int parentReferenceCount,
                int objectReferenceSlotCount,
                int objectReferenceCount,
                int missingScriptCount,
                string[] rows)
            {
                ObjectCount = objectCount;
                ComponentCount = componentCount;
                ParentReferenceCount = parentReferenceCount;
                ObjectReferenceSlotCount = objectReferenceSlotCount;
                ObjectReferenceCount = objectReferenceCount;
                MissingScriptCount = missingScriptCount;
                Rows = rows;
            }

            public int ObjectCount { get; }
            public int ComponentCount { get; }
            public int ParentReferenceCount { get; }
            public int ObjectReferenceSlotCount { get; }
            public int ObjectReferenceCount { get; }
            public int MissingScriptCount { get; }
            public string[] Rows { get; }
        }

        private sealed class AuthoringHookProbe
        {
            public int RuntimeAssetBuilderEntries;
            public int SceneMutationEntries;
            public int SceneSaveEntries;
        }

        private sealed class ProtectedFileSnapshot
        {
            private readonly string backupDirectory;
            private readonly Dictionary<string, string> backupPaths;
            private readonly Dictionary<string, ProtectedFileState> expected;
            private Dictionary<string, ProtectedFileState> lastObserved;
            private bool disposed;

            private ProtectedFileSnapshot(
                string backupDirectory,
                Dictionary<string, string> backupPaths,
                Dictionary<string, ProtectedFileState> expected)
            {
                this.backupDirectory = backupDirectory;
                this.backupPaths = backupPaths;
                this.expected = expected;
            }

            public static ProtectedFileSnapshot Capture()
            {
                var paths = new List<string>(ProtectedPaths());
                Assert.That(
                    paths.Count,
                    Is.EqualTo(51),
                    "The complete protected set must contain 51 files.");
                Dictionary<string, ProtectedFileState> expected =
                    CaptureProtectedFileStates(paths);
                string backupDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "WasteCityTerrainProtected-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backupDirectory);
                var backups = new Dictionary<string, string>();
                try
                {
                    for (var index = 0; index < paths.Count; index++)
                    {
                        string protectedPath = paths[index];
                        string backupPath = Path.Combine(
                            backupDirectory,
                            index.ToString("D2") + ".bytes");
                        File.Copy(
                            ProjectAbsolutePath(protectedPath),
                            backupPath,
                            true);
                        backups.Add(protectedPath, backupPath);
                    }
                    return new ProtectedFileSnapshot(
                        backupDirectory,
                        backups,
                        expected);
                }
                catch
                {
                    Directory.Delete(backupDirectory, true);
                    throw;
                }
            }

            public void AssertUnchanged(string context)
            {
                lastObserved =
                    CaptureProtectedFileStatesForRecovery(expected.Keys);
                AssertProtectedStatesEqual(expected, lastObserved, context);
            }

            public bool IsDisposed => disposed;

            public void RestoreAndDispose()
            {
                if (disposed)
                    return;
                Dictionary<string, ProtectedFileState> actual =
                    lastObserved ??
                    CaptureProtectedFileStatesForRecovery(expected.Keys);
                var imports = new HashSet<string>();
                foreach (KeyValuePair<string, ProtectedFileState> pair in
                         expected)
                {
                    if (actual.TryGetValue(
                            pair.Key,
                            out ProtectedFileState observed) &&
                        observed.Guid == pair.Value.Guid &&
                        observed.Hash == pair.Value.Hash)
                    {
                        continue;
                    }

                    File.Copy(
                        backupPaths[pair.Key],
                        ProjectAbsolutePath(pair.Key),
                        true);
                    if (pair.Key.StartsWith(
                            "Assets/",
                            StringComparison.Ordinal))
                    {
                        imports.Add(
                            pair.Key.EndsWith(
                                ".meta",
                                StringComparison.Ordinal)
                                ? pair.Key.Substring(
                                    0,
                                    pair.Key.Length - 5)
                                : pair.Key);
                    }
                }

                if (imports.Count > 0)
                {
                    foreach (string assetPath in imports)
                    {
                        AssetDatabase.ImportAsset(
                            assetPath,
                            ImportAssetOptions.ForceSynchronousImport |
                            ImportAssetOptions.ForceUpdate);
                    }

                    Dictionary<string, ProtectedFileState> afterImports =
                        CaptureProtectedFileStatesForRecovery(expected.Keys);
                    foreach (KeyValuePair<string, ProtectedFileState> pair in
                             expected)
                    {
                        ProtectedFileState observed = afterImports[pair.Key];
                        if (observed.Guid == pair.Value.Guid &&
                            observed.Hash == pair.Value.Hash)
                        {
                            continue;
                        }
                        File.Copy(
                            backupPaths[pair.Key],
                            ProjectAbsolutePath(pair.Key),
                            true);
                    }
                }

                disposed = true;
                try
                {
                    Directory.Delete(backupDirectory, true);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "Protected bytes were restored, but the temporary " +
                        $"snapshot directory could not be deleted: {exception}");
                }
            }
        }
    }
}
