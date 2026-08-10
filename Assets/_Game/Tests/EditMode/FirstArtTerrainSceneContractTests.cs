using System;
using System.IO;
using System.Reflection;
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
            Assert.That(
                File.ReadAllText(
                    Path.Combine(
                        Application.dataPath,
                        "_Game/Scenes/GrayboxPrototype3D.unity")),
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
        public void BuildSettings_KeepGrayboxFirstAndFormalSecond()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2));
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
            MethodInfo validation = RequireValidationMethod();

            Assert.That(
                () => validation.Invoke(null, new object[] { scene }),
                Throws.Nothing);
        }

        [Test]
        public void AuthoringValidation_RejectsAnUnapprovedProfileReference()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FirstArtTerrainRenderer3D presenter =
                Object.FindObjectOfType<FirstArtTerrainRenderer3D>(true);
            MethodInfo validation = RequireValidationMethod();
            Assert.That(presenter, Is.Not.Null);

            try
            {
                presenter.Configure(null);
                TargetInvocationException exception =
                    Assert.Throws<TargetInvocationException>(
                        () => validation.Invoke(
                            null,
                            new object[] { scene }));
                Assert.That(
                    exception.InnerException,
                    Is.TypeOf<InvalidOperationException>());
            }
            finally
            {
                EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Single);
            }
        }

        private static MethodInfo RequireValidationMethod()
        {
            MethodInfo method = typeof(GrayboxSceneAuthoring).GetMethod(
                "ValidateFirstArtTerrainContract",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static T LoadRequired<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
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
    }
}
