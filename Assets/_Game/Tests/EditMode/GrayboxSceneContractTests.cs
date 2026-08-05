using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using WasteCity.City;
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

        [Test]
        public void Scene_WiresPlayableActorsInputProjectionAndCamera()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("GrayboxPrototype3D");

            Assert.That(
                root.transform.Find("CameraRig/Main Camera"),
                Is.Not.Null);
            Assert.That(
                root.transform.Find("GrayboxActors/MobileCity"),
                Is.Not.Null);
            Assert.That(
                root.transform.Find("GrayboxActors/Leader_CenJin"),
                Is.Not.Null);

            Camera camera = Camera.main;
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>(true);
            GrayboxLeaderController3D leader =
                Object.FindObjectOfType<
                    GrayboxLeaderController3D>(true);
            GrayboxDirectControlCoordinator coordinator =
                Object.FindObjectOfType<
                    GrayboxDirectControlCoordinator>(true);
            GrayboxGroundProjector projector =
                Object.FindObjectOfType<GrayboxGroundProjector>(true);
            GrayboxCameraController3D cameraController =
                Object.FindObjectOfType<
                    GrayboxCameraController3D>(true);
            GrayboxInputRouter inputRouter =
                Object.FindObjectOfType<GrayboxInputRouter>(true);
            GrayboxWorldView3D worldView =
                Object.FindObjectOfType<GrayboxWorldView3D>(true);

            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(13f));
            Assert.That(
                camera.transform.localPosition,
                Is.EqualTo(new Vector3(0f, 18f, -14f)));
            Assert.That(
                camera.transform.localEulerAngles.x,
                Is.EqualTo(52f).Within(.01f));
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxMobileCityController3D>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxDirectControlCoordinator>(true).Length,
                Is.EqualTo(1));
            Assert.That(leader, Is.Not.Null);
            Assert.That(projector, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(inputRouter, Is.Not.Null);

            AssertReference(city, "worldView", worldView);
            AssertReference(leader, "worldView", worldView);
            AssertReference(leader, "city", city);
            AssertReference(coordinator, "city", city);
            AssertReference(coordinator, "leader", leader);
            AssertReference(projector, "controlledCamera", camera);
            AssertReference(projector, "worldView", worldView);
            AssertReference(
                cameraController,
                "controlledCamera",
                camera);
            AssertReference(
                cameraController,
                "cameraRig",
                camera.transform.parent);
            AssertReference(cameraController, "city", city);
            AssertReference(cameraController, "leader", leader);
            AssertReference(
                cameraController,
                "directControl",
                coordinator);
            AssertReference(
                cameraController,
                "groundProjector",
                projector);
            AssertReference(inputRouter, "city", city);
            AssertReference(inputRouter, "leader", leader);
            AssertReference(
                inputRouter,
                "directControl",
                coordinator);
            AssertReference(
                inputRouter,
                "groundProjector",
                projector);
            AssertReference(
                inputRouter,
                "cameraController",
                cameraController);

            var leaderData = new SerializedObject(leader);
            SerializedProperty fixture =
                leaderData.FindProperty(
                    "developmentFixtureRecruited");
            Assert.That(fixture, Is.Not.Null);
            Assert.That(fixture.boolValue, Is.True);

            Assert.That(city.transform.position.x, Is.EqualTo(-8f));
            Assert.That(city.transform.position.z, Is.EqualTo(-5f));
            BoxCollider cityCollider = city.GetComponent<BoxCollider>();
            Assert.That(cityCollider, Is.Not.Null);
            Assert.That(
                cityCollider.bounds.min.y,
                Is.EqualTo(0f).Within(.001f));

            GrayboxVisualSlot citySlot =
                city.GetComponentInChildren<GrayboxVisualSlot>(true);
            GrayboxVisualSlot leaderSlot =
                leader.GetComponentInChildren<GrayboxVisualSlot>(true);
            Assert.That(
                citySlot?.StableId,
                Is.EqualTo("core.city.mobile"));
            Assert.That(
                leaderSlot?.StableId,
                Is.EqualTo("core.character.cen-jin"));
        }

        [Test]
        public void BuildSettings_KeepFormalFirstAndGrayboxSecond()
        {
            EditorBuildSettingsScene[] scenes =
                EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(
                scenes[0].path,
                Is.EqualTo(
                    "Assets/_Game/Scenes/FormalPrototype.unity"));
            Assert.That(scenes[1].enabled, Is.True);
            Assert.That(scenes[1].path, Is.EqualTo(ScenePath));
        }

        [Test]
        public void Scene_PlayableObjectsContainNo2DComponents()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Assert.That(
                Object.FindObjectsOfType<SpriteRenderer>(true),
                Is.Empty);
            Assert.That(
                Object.FindObjectsOfType<Rigidbody2D>(true),
                Is.Empty);
            Assert.That(
                Object.FindObjectsOfType<Collider2D>(true),
                Is.Empty);
        }

        private static void AssertReference(
            Object owner,
            string propertyName,
            Object expected)
        {
            var data = new SerializedObject(owner);
            SerializedProperty property =
                data.FindProperty(propertyName);
            Assert.That(
                property,
                Is.Not.Null,
                $"{owner.GetType().Name}.{propertyName}");
            Assert.That(
                property.objectReferenceValue,
                Is.SameAs(expected),
                $"{owner.GetType().Name}.{propertyName}");
        }
    }
}
