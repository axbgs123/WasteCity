using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using WasteCity.Core;
using WasteCity.Graybox3D;
using WasteCity.Persistence;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxSceneContractTests
    {
        private const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        private const string RendererPath =
            "Assets/_Game/Rendering/Graybox3D/" +
            "GrayboxUniversalRenderer.asset";
        private const string PipelinePath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset";
        private const string MaterialPath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat";

        [Test]
        public void Scene_HasIndependentBootstrapHierarchyAndReferences()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("GrayboxPrototype3D");

            Assert.That(scene.path, Is.EqualTo(ScenePath));
            Assert.That(root, Is.Not.Null);
            Assert.That(
                root.transform.Find("GrayboxRenderScope"),
                Is.Not.Null);
            Assert.That(root.transform.Find("GrayboxWorld"), Is.Not.Null);
            Assert.That(
                root.transform.Find("GrayboxWorld/TerrainRoot"),
                Is.Not.Null);
            Assert.That(
                root.transform.Find("GrayboxWorld/ResourceRoot"),
                Is.Not.Null);
            Assert.That(
                root.transform.Find("GrayboxWorld/ObstacleRoot"),
                Is.Not.Null);
            Assert.That(root.transform.Find("GrayboxSystems"), Is.Not.Null);
            Assert.That(
                root.transform.Find(
                    "GrayboxSystems/GrayboxSceneBootstrap"),
                Is.Not.Null);

            GrayboxUrpScope[] scopes =
                Object.FindObjectsOfType<GrayboxUrpScope>(true);
            GrayboxWorldView3D[] views =
                Object.FindObjectsOfType<GrayboxWorldView3D>(true);
            GrayboxSceneBootstrap[] bootstraps =
                Object.FindObjectsOfType<GrayboxSceneBootstrap>(true);
            Assert.That(scopes.Length, Is.EqualTo(1));
            Assert.That(views.Length, Is.EqualTo(1));
            Assert.That(bootstraps.Length, Is.EqualTo(1));

            var bootstrapData = new SerializedObject(bootstraps[0]);
            Assert.That(
                bootstrapData.FindProperty("renderScope")
                    .objectReferenceValue,
                Is.SameAs(scopes[0]));
            Assert.That(
                bootstrapData.FindProperty("worldView")
                    .objectReferenceValue,
                Is.SameAs(views[0]));
        }

        [Test]
        public void Scene_UsesDedicatedUniversalRendererAndLitMaterial()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UniversalRendererData renderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    RendererPath);
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<
                    UniversalRenderPipelineAsset>(PipelinePath);
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            Assert.That(renderer, Is.Not.Null);
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            Assert.That(
                material.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"));

            var pipelineData = new SerializedObject(pipeline);
            SerializedProperty rendererList =
                pipelineData.FindProperty("m_RendererDataList");
            Assert.That(rendererList, Is.Not.Null);
            Assert.That(rendererList.arraySize, Is.EqualTo(1));
            Assert.That(
                rendererList.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.SameAs(renderer));
            Assert.That(
                pipelineData.FindProperty("m_DefaultRendererIndex").intValue,
                Is.Zero);

            GrayboxUrpScope scope =
                Object.FindObjectOfType<GrayboxUrpScope>(true);
            GrayboxWorldView3D view =
                Object.FindObjectOfType<GrayboxWorldView3D>(true);
            var scopeData = new SerializedObject(scope);
            var viewData = new SerializedObject(view);
            Assert.That(
                scopeData.FindProperty("pipelineAsset").objectReferenceValue,
                Is.SameAs(pipeline));
            Assert.That(
                viewData.FindProperty("sharedMaterial").objectReferenceValue,
                Is.SameAs(material));
        }

        [Test]
        public void Scene_ContainsNoFrozen2DRuntimeOrFormalSaveController()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Assert.That(
                Object.FindObjectsOfType<FormalSaveController>(true),
                Is.Empty);
            Assert.That(
                Object.FindObjectsOfType<FormalGameBootstrap>(true),
                Is.Empty);
            Assert.That(
                Object.FindObjectsOfType<PlaceholderWorldView>(true),
                Is.Empty);
        }
    }
}
