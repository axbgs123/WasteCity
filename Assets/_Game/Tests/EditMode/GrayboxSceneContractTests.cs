using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Persistence;
using WasteCity.World;
using Object = UnityEngine.Object;

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
        private const string PreviewMaterialPath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxPreview.mat";
        private const string ResourceIconCatalogPath =
            "Assets/_Game/Rendering/Graybox3D/ResourceIconCatalog3D.asset";

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
            GrayboxBuildingWorldView3D buildingView =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>(true);
            Assert.That(buildingView, Is.Not.Null);
            var scopeData = new SerializedObject(scope);
            var viewData = new SerializedObject(view);
            Assert.That(
                scopeData.FindProperty("pipelineAsset").objectReferenceValue,
                Is.SameAs(pipeline));
            Assert.That(
                viewData.FindProperty("sharedMaterial").objectReferenceValue,
                Is.SameAs(material));

            Material previewMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(PreviewMaterialPath);
            Assert.That(previewMaterial, Is.Not.Null);
            Assert.That(previewMaterial, Is.Not.SameAs(material));
            Assert.That(
                previewMaterial.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(previewMaterial.GetFloat("_Surface"), Is.EqualTo(1f));
            Assert.That(previewMaterial.GetFloat("_ZWrite"), Is.Zero);
            Assert.That(
                previewMaterial.renderQueue,
                Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            Assert.That(
                new SerializedObject(buildingView)
                    .FindProperty("previewMaterial")
                    .objectReferenceValue,
                Is.SameAs(previewMaterial));
        }

        [Test]
        public void IDEA0012_SceneConsumersShareApprovedResourceIconCatalog()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ResourceIconCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<ResourceIconCatalog3D>(
                    ResourceIconCatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string error), Is.True, error);
            AssertReference(
                Object.FindObjectOfType<GrayboxWorldView3D>(true),
                "resourceIconCatalog",
                catalog);
            AssertReference(
                Object.FindObjectOfType<GrayboxBuildingMenuView3D>(true),
                "resourceIconCatalog",
                catalog);
            AssertReference(
                Object.FindObjectOfType<GrayboxOperationsView3D>(true),
                "resourceIconCatalog",
                catalog);
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

            Assert.That(city.transform.position.x, Is.EqualTo(-9f));
            Assert.That(city.transform.position.y, Is.EqualTo(.5f));
            Assert.That(city.transform.position.z, Is.EqualTo(-4f));
            var coordinates = new PlanarCoordinateMapper3D(
                GrayboxSceneBootstrap.WorldWidth,
                GrayboxSceneBootstrap.WorldHeight);
            Assert.That(
                coordinates.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY),
                Is.True);
            Assert.That((cityX, cityY), Is.EqualTo((23, 20)));
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            Assert.That(
                CityDeploymentRules.Validate(world, cityX, cityY),
                Is.EqualTo(CityDeploymentFailure.None));
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
        public void BuildSettings_KeepGrayboxFirstAndFormalSecond()
        {
            EditorBuildSettingsScene[] scenes =
                EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(
                scenes[0].path,
                Is.EqualTo(ScenePath));
            Assert.That(scenes[1].enabled, Is.True);
            Assert.That(
                scenes[1].path,
                Is.EqualTo(
                    "Assets/_Game/Scenes/FormalPrototype.unity"));
        }

        [Test]
        public void SceneAuthoring_UsesApprovedSparseLayoutFactory()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxSceneAuthoring.cs"));
            StringAssert.Contains(
                "GrayboxWorldLayout3D.CreateDefault()",
                source);
            StringAssert.Contains(
                "GrayboxWorldLayout3D.ToExpandedX(7)",
                source);
            StringAssert.Contains(
                "GrayboxWorldLayout3D.ToExpandedY(8)",
                source);
            string compact = source.Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace(" ", string.Empty);
            StringAssert.DoesNotContain(
                "newWorldMapModel(GrayboxSceneBootstrap.WorldWidth",
                compact);
        }

        [TestCase("GrayboxSceneAuthoring.cs")]
        [TestCase("FormalProjectSetup.cs")]
        public void SceneAuthoringPaths_KeepGrayboxFirstAndFormalSecond(
            string editorFileName)
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor",
                    editorFileName));
            string assignment =
                ExtractBuildSettingsAssignment(source);
            int grayboxIndex = assignment.IndexOf(
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                System.StringComparison.Ordinal);
            if (grayboxIndex < 0)
            {
                grayboxIndex = assignment.IndexOf(
                    "ScenePath",
                    System.StringComparison.Ordinal);
            }
            int formalIndex = assignment.IndexOf(
                "Assets/_Game/Scenes/FormalPrototype.unity",
                System.StringComparison.Ordinal);

            Assert.That(
                grayboxIndex,
                Is.GreaterThanOrEqualTo(0),
                editorFileName);
            Assert.That(
                formalIndex,
                Is.GreaterThan(grayboxIndex),
                editorFileName);
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

        [Test]
        public void Scene_HasCompleteSerializedBuildingContract()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("GrayboxPrototype3D");

            Transform building = RequiredChild(root, "GrayboxBuilding");
            GrayboxBuildingSession3D session =
                RequiredComponent<GrayboxBuildingSession3D>(
                    building, "BuildingSession");
            GrayboxBuildingInteractionModel3D interaction =
                RequiredComponent<GrayboxBuildingInteractionModel3D>(
                    building, "BuildingInteraction");
            Transform presentationRoot =
                RequiredChild(building.gameObject, "BuildingPresentation");
            Transform instanceRoot =
                RequiredChild(presentationRoot.gameObject, "InstanceRoot");
            Transform infrastructureRoot =
                RequiredChild(
                    presentationRoot.gameObject,
                    "InfrastructureRoot");
            GrayboxBuildingWorldView3D presentation =
                presentationRoot.GetComponent<GrayboxBuildingWorldView3D>();
            Assert.That(presentation, Is.Not.Null);
            GrayboxBuildingSurfaceProjector3D surfaceProjector =
                RequiredComponent<GrayboxBuildingSurfaceProjector3D>(
                    building, "BuildingSurfaceProjector");
            GrayboxBuildingPlacementController3D placement =
                RequiredComponent<GrayboxBuildingPlacementController3D>(
                    building, "BuildingPlacement");
            GrayboxConstructionController3D construction =
                RequiredComponent<GrayboxConstructionController3D>(
                    building, "Construction");
            GrayboxProductionController3D production =
                RequiredComponent<GrayboxProductionController3D>(
                    building, "Production");
            GrayboxEvacuationController3D evacuation =
                RequiredComponent<GrayboxEvacuationController3D>(
                    building, "Evacuation");
            GrayboxBuildingInputRouter3D buildingInput =
                RequiredComponent<GrayboxBuildingInputRouter3D>(
                    building, "BuildingInput");
            GrayboxDeveloperModifierBootstrap3D developer =
                RequiredComponent<GrayboxDeveloperModifierBootstrap3D>(
                    building, "DeveloperModifierBootstrap");

            Transform ui = RequiredChild(root, "GrayboxUI");
            Transform canvasTransform =
                RequiredChild(ui.gameObject, "BuildingCanvas");
            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            GraphicRaycaster raycaster =
                canvasTransform.GetComponent<GraphicRaycaster>();
            GrayboxBuildingMenuView3D menu =
                canvasTransform.GetComponent<GrayboxBuildingMenuView3D>();
            Transform eventSystemTransform =
                RequiredChild(ui.gameObject, "EventSystem");
            EventSystem eventSystem =
                eventSystemTransform.GetComponent<EventSystem>();
            InputSystemUIInputModule inputModule =
                eventSystemTransform.GetComponent<
                    InputSystemUIInputModule>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(raycaster, Is.Not.Null);
            Assert.That(raycaster.enabled, Is.True);
            Assert.That(menu, Is.Not.Null);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(
                Object.FindObjectsOfType<Canvas>(true).Length,
                Is.EqualTo(3));
            Assert.That(
                Object.FindObjectsOfType<GraphicRaycaster>(true).Length,
                Is.EqualTo(3));
            Assert.That(
                Object.FindObjectsOfType<EventSystem>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    InputSystemUIInputModule>(true).Length,
                Is.EqualTo(1));

            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>(true);
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>(true);
            Assert.That(
                Object.FindObjectsOfType<GrayboxProductionController3D>(true),
                Has.Length.EqualTo(1));
            Camera camera = Camera.main;
            Transform platform = RequiredChild(
                city.gameObject,
                "InnerCityPlatform");
            BoxCollider innerSurface =
                platform.GetComponent<BoxCollider>();
            MeshFilter platformFilter =
                platform.GetComponent<MeshFilter>();
            MeshRenderer platformRenderer =
                platform.GetComponent<MeshRenderer>();
            Assert.That(innerSurface, Is.Not.Null);
            Assert.That(platformFilter, Is.Not.Null);
            Assert.That(platformFilter.sharedMesh, Is.Not.Null);
            Assert.That(platformRenderer, Is.Not.Null);
            Assert.That(
                platform.GetComponentsInChildren<MeshRenderer>(true).Length,
                Is.EqualTo(1));
            Vector3 absoluteScale = new Vector3(
                Mathf.Abs(platform.localScale.x),
                Mathf.Abs(platform.localScale.y),
                Mathf.Abs(platform.localScale.z));
            Vector3 visibleSize = Vector3.Scale(
                platformFilter.sharedMesh.bounds.size,
                absoluteScale);
            Vector3 selectionSize = Vector3.Scale(
                innerSurface.size,
                absoluteScale);
            Assert.That(visibleSize.x, Is.EqualTo(2.56f).Within(.001f));
            Assert.That(visibleSize.z, Is.EqualTo(1.92f).Within(.001f));
            Assert.That(selectionSize.x, Is.EqualTo(2.56f).Within(.001f));
            Assert.That(selectionSize.z, Is.EqualTo(1.92f).Within(.001f));
            Assert.That(
                platformRenderer.sharedMaterial,
                Is.SameAs(
                    AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)));
            BoxCollider cityCollider = city.GetComponent<BoxCollider>();
            float cityBodyTop =
                cityCollider.center.y + cityCollider.size.y * .5f;
            float platformTop =
                platform.localPosition.y +
                absoluteScale.y *
                (innerSurface.center.y + innerSurface.size.y * .5f);
            Assert.That(
                platformTop,
                Is.EqualTo(cityBodyTop + .01f).Within(.001f));

            AssertReference(presentation, "instanceRoot", instanceRoot);
            AssertReference(
                presentation,
                "infrastructureRoot",
                infrastructureRoot);
            AssertReference(
                presentation,
                "sharedMaterial",
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            AssertReference(
                presentation,
                "previewMaterial",
                AssetDatabase.LoadAssetAtPath<Material>(PreviewMaterialPath));
            AssertReference(presentation, "city", city);
            AssertReference(surfaceProjector, "controlledCamera", camera);
            AssertReference(surfaceProjector, "worldView", world);
            AssertReference(surfaceProjector, "city", city);
            AssertReference(surfaceProjector, "innerCitySurface", innerSurface);
            AssertReference(placement, "session", session);
            AssertReference(placement, "city", city);
            AssertReference(placement, "world", world);
            AssertReference(placement, "projector", surfaceProjector);
            AssertReference(placement, "presentation", presentation);
            AssertReference(placement, "interaction", interaction);
            AssertReference(construction, "session", session);
            AssertReference(construction, "city", city);
            AssertReference(construction, "presentation", presentation);
            AssertReference(construction, "interaction", interaction);
            AssertReference(construction, "controlledCamera", camera);
            AssertReference(construction, "menu", menu);
            AssertReference(production, "session", session);
            AssertReference(production, "city", city);
            AssertReference(production, "worldView", world);
            AssertReference(evacuation, "session", session);
            AssertReference(evacuation, "city", city);
            AssertReference(evacuation, "presentation", presentation);
            AssertReference(evacuation, "menu", menu);
            AssertReference(menu, "canvas", canvas);
            AssertReference(menu, "eventSystem", eventSystem);
            AssertReference(menu, "session", session);
            AssertReference(menu, "interaction", interaction);
            AssertReference(menu, "placement", placement);
            AssertReference(buildingInput, "menu", menu);
            AssertReference(buildingInput, "interaction", interaction);
            AssertReference(buildingInput, "placement", placement);
            AssertReference(buildingInput, "construction", construction);
            AssertReference(buildingInput, "evacuation", evacuation);
            AssertReference(buildingInput, "developer", developer);
            AssertReference(developer, "session", session);
            AssertReference(developer, "city", city);
            AssertReference(developer, "presentation", presentation);
            AssertReference(developer, "canvas", canvas);
            AssertReference(
                Object.FindObjectOfType<GrayboxInputRouter>(true),
                "inputInterceptor",
                Object.FindObjectOfType<
                    GrayboxUsabilityInputCoordinator3D>(true));

            var sessionData = new SerializedObject(session);
            Assert.That(
                sessionData.FindProperty("developmentFixtureEnabled")
                    .boolValue,
                Is.False);
            var developerData = new SerializedObject(developer);
            Assert.That(developerData.FindProperty("modifier"), Is.Null);
            Assert.That(developerData.FindProperty("panelRoot"), Is.Null);

            foreach (GameObject gameObject in
                     Object.FindObjectsOfType<GameObject>(true))
            {
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        gameObject),
                    Is.Zero,
                    gameObject.name);
            }
            Assert.That(
                Object.FindObjectsOfType<GrayboxVisualSlot>(true),
                Has.None.Matches<GrayboxVisualSlot>(
                    slot => slot.StableId != null &&
                            slot.StableId.StartsWith(
                                "building.",
                                StringComparison.Ordinal)));
            Assert.That(
                canvasTransform.GetComponentsInChildren<Button>(true),
                Is.Empty,
                "Catalog and quickbar buttons must be runtime generated.");
            Assert.That(instanceRoot.childCount, Is.Zero);
            Assert.That(infrastructureRoot.childCount, Is.Zero);
            Assert.That(canvasTransform.childCount, Is.Zero);
        }

        [Test]
        public void IDEA0007_SceneHasSerializedUsabilityInputContract()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("GrayboxPrototype3D");
            Transform ui = RequiredChild(root, "GrayboxUI");
            Canvas buildingCanvas = RequiredChild(
                    ui.gameObject,
                    "BuildingCanvas")
                .GetComponent<Canvas>();
            Transform systemMenuTransform = RequiredChild(
                ui.gameObject,
                "SystemMenuCanvas");
            Canvas systemMenuCanvas =
                systemMenuTransform.GetComponent<Canvas>();
            GraphicRaycaster systemMenuRaycaster =
                systemMenuTransform.GetComponent<GraphicRaycaster>();
            GrayboxSystemMenuView3D view =
                systemMenuTransform.GetComponent<
                    GrayboxSystemMenuView3D>();
            Transform systems = RequiredChild(root, "GrayboxSystems");
            GrayboxSystemMenuController3D controller =
                RequiredComponent<GrayboxSystemMenuController3D>(
                    systems,
                    "GrayboxSystemMenuController");
            GrayboxUsabilityInputCoordinator3D coordinator =
                RequiredComponent<GrayboxUsabilityInputCoordinator3D>(
                    systems,
                    "GrayboxUsabilityInputCoordinator");
            GrayboxBuildingInputRouter3D buildingInput =
                Object.FindObjectOfType<
                    GrayboxBuildingInputRouter3D>(true);
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>(true);
            EventSystem eventSystem =
                Object.FindObjectOfType<EventSystem>(true);

            Assert.That(buildingCanvas, Is.Not.Null);
            Assert.That(systemMenuCanvas, Is.Not.Null);
            Assert.That(systemMenuCanvas.renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(systemMenuCanvas.sortingOrder,
                Is.GreaterThan(buildingCanvas.sortingOrder));
            Assert.That(systemMenuRaycaster, Is.Not.Null);
            Assert.That(systemMenuRaycaster.enabled, Is.True);
            Assert.That(view, Is.Not.Null);
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxSystemMenuView3D>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxSystemMenuController3D>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxUsabilityInputCoordinator3D>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<EventSystem>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    InputSystemUIInputModule>(true),
                Has.Length.EqualTo(1));

            AssertReference(view, "canvas", systemMenuCanvas);
            AssertReference(view, "eventSystem", eventSystem);
            AssertReference(view, "controller", controller);
            AssertReference(controller, "view", view);
            AssertReference(coordinator, "buildingInput", buildingInput);
            AssertReference(coordinator, "systemMenu", controller);
            AssertReference(coordinator, "developer", developer);
            AssertReference(
                Object.FindObjectOfType<GrayboxInputRouter>(true),
                "inputInterceptor",
                coordinator);
        }

        [Test]
        public void IDEA0011_SceneHasSerializedOperationsUiContract()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("GrayboxPrototype3D");
            Transform ui = RequiredChild(root, "GrayboxUI");
            Canvas buildingCanvas = RequiredChild(
                    ui.gameObject,
                    "BuildingCanvas")
                .GetComponent<Canvas>();
            Transform operationsTransform = RequiredChild(
                ui.gameObject,
                "ProductionObservabilityCanvas");
            Canvas operationsCanvas =
                operationsTransform.GetComponent<Canvas>();
            Canvas systemMenuCanvas = RequiredChild(
                    ui.gameObject,
                    "SystemMenuCanvas")
                .GetComponent<Canvas>();
            CanvasScaler scaler =
                operationsTransform.GetComponent<CanvasScaler>();
            GrayboxOperationsView3D view =
                operationsTransform.GetComponent<GrayboxOperationsView3D>();
            Transform systems = RequiredChild(root, "GrayboxSystems");
            GrayboxOperationsController3D controller =
                RequiredComponent<GrayboxOperationsController3D>(
                    systems,
                    "GrayboxOperationsController");
            GrayboxUsabilityInputCoordinator3D coordinator =
                RequiredComponent<GrayboxUsabilityInputCoordinator3D>(
                    systems,
                    "GrayboxUsabilityInputCoordinator");
            GrayboxBuildingInputRouter3D buildingInput =
                Object.FindObjectOfType<GrayboxBuildingInputRouter3D>(true);

            Assert.That(operationsCanvas, Is.Not.Null);
            Assert.That(operationsCanvas.renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(operationsCanvas.sortingOrder,
                Is.GreaterThan(buildingCanvas.sortingOrder));
            Assert.That(operationsCanvas.sortingOrder,
                Is.LessThan(systemMenuCanvas.sortingOrder));
            Assert.That(
                operationsTransform.GetComponent<GraphicRaycaster>(),
                Is.Not.Null);
            Assert.That(scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(view, Is.Not.Null);
            Assert.That(
                Object.FindObjectsOfType<GrayboxOperationsView3D>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxOperationsController3D>(true),
                Has.Length.EqualTo(1));

            AssertReference(view, "canvas", operationsCanvas);
            AssertReference(
                controller,
                "session",
                Object.FindObjectOfType<GrayboxBuildingSession3D>(true));
            AssertReference(
                controller,
                "production",
                Object.FindObjectOfType<
                    GrayboxProductionController3D>(true));
            AssertReference(
                controller,
                "city",
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>(true));
            AssertReference(controller, "view", view);
            AssertReference(
                controller,
                "directControl",
                Object.FindObjectOfType<
                    GrayboxDirectControlCoordinator>(true));
            AssertReference(
                controller,
                "worldView",
                Object.FindObjectOfType<GrayboxWorldView3D>(true));
            AssertReference(
                controller,
                "leader",
                Object.FindObjectOfType<GrayboxLeaderController3D>(true));
            AssertReference(coordinator, "operations", controller);
            AssertReference(
                buildingInput,
                "productionPresentation",
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>(true));
            AssertReference(buildingInput, "operations", controller);
            Assert.That(Object.FindObjectsOfType<EventSystem>(true),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void IDEA0013_SceneHasOneSerializedDefenseContractWithoutMissingScripts()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("GrayboxPrototype3D");
            Transform building = RequiredChild(root, "GrayboxBuilding");
            Transform defenseTransform = RequiredChild(
                building.gameObject,
                "DefenseWorldView");
            Transform enemyRoot = RequiredChild(
                defenseTransform.gameObject,
                "EnemyRoot");
            Transform towerRoot = RequiredChild(
                defenseTransform.gameObject,
                "TowerRoot");
            GrayboxDefenseWorldView3D worldView =
                defenseTransform.GetComponent<GrayboxDefenseWorldView3D>();
            Transform systems = RequiredChild(root, "GrayboxSystems");
            GrayboxDefenseController3D controller =
                RequiredComponent<GrayboxDefenseController3D>(
                    systems,
                    "GrayboxDefenseController");
            Transform ui = RequiredChild(root, "GrayboxUI");
            Transform operationsTransform = RequiredChild(
                ui.gameObject,
                "ProductionObservabilityCanvas");
            Canvas operationsCanvas =
                operationsTransform.GetComponent<Canvas>();
            GrayboxDefenseHud3D hud =
                operationsTransform.GetComponent<GrayboxDefenseHud3D>();
            EventSystem eventSystem =
                Object.FindObjectOfType<EventSystem>(true);
            GrayboxBuildingInputRouter3D buildingInput =
                Object.FindObjectOfType<GrayboxBuildingInputRouter3D>(true);
            GrayboxUsabilityInputCoordinator3D coordinator =
                Object.FindObjectOfType<
                    GrayboxUsabilityInputCoordinator3D>(true);
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            Assert.That(scene.path, Is.EqualTo(ScenePath));
            Assert.That(worldView, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(operationsCanvas, Is.Not.Null);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            Assert.That(
                Object.FindObjectsOfType<GrayboxDefenseController3D>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<GrayboxDefenseWorldView3D>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<GrayboxDefenseHud3D>(true),
                Has.Length.EqualTo(1));

            AssertReference(worldView, "enemyRoot", enemyRoot);
            AssertReference(worldView, "towerRoot", towerRoot);
            AssertReference(worldView, "sharedMaterial", material);
            AssertReference(
                controller,
                "session",
                Object.FindObjectOfType<GrayboxBuildingSession3D>(true));
            AssertReference(
                controller,
                "city",
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>(true));
            AssertReference(
                controller,
                "world",
                Object.FindObjectOfType<GrayboxWorldView3D>(true));
            AssertReference(
                controller,
                "buildingPresentation",
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>(true));
            AssertReference(controller, "worldView", worldView);
            AssertReference(controller, "hud", hud);
            AssertReference(hud, "canvas", operationsCanvas);
            AssertReference(hud, "eventSystem", eventSystem);
            AssertReference(buildingInput, "defense", controller);
            AssertReference(coordinator, "defense", controller);

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Transform transform in
                     sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(
                    GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(
                            transform.gameObject),
                    Is.Zero,
                    transform.gameObject.name);
            }
        }

        [Test]
        public void IDEA0014_SceneHasOneExplicitEvacuationRuntimeAndInputContract()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GrayboxEvacuationController3D[] evacuations =
                Object.FindObjectsOfType<GrayboxEvacuationController3D>(true);
            GrayboxProductionController3D production =
                Object.FindObjectOfType<GrayboxProductionController3D>(true);
            GrayboxDefenseController3D defense =
                Object.FindObjectOfType<GrayboxDefenseController3D>(true);
            GrayboxBuildingInputRouter3D buildingInput =
                Object.FindObjectOfType<GrayboxBuildingInputRouter3D>(true);
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>(true);
            GrayboxUsabilityInputCoordinator3D coordinator =
                Object.FindObjectOfType<
                    GrayboxUsabilityInputCoordinator3D>(true);

            Assert.That(evacuations, Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<GrayboxBuildingMenuView3D>(true),
                Has.Length.EqualTo(1));
            Assert.That(production, Is.Not.Null);
            Assert.That(defense, Is.Not.Null);
            Assert.That(buildingInput, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(coordinator, Is.Not.Null);

            GrayboxEvacuationController3D evacuation = evacuations[0];
            AssertReference(evacuation, "production", production);
            AssertReference(evacuation, "defense", defense);
            AssertReference(buildingInput, "evacuation", evacuation);
            AssertReference(coordinator, "operations", operations);
            Assert.That(
                CountSerializedReferencesTo(evacuation),
                Is.EqualTo(1),
                "Only the formal building input router may own the " +
                "serialized evacuation command reference.");
        }

        [Test]
        public void IDEA0014_SceneCityUsesTheUniqueBuildingSessionAsRuleTimeSource()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            AssertUniqueCityRuleTimeSourceContract();
        }

        [Test]
        public void IDEA0014_RuleTimeSourceAuthoringTwiceIsByteAndOwnerIdempotent()
        {
            const string temporaryScenePath =
                "Assets/_Game/Scenes/__Task8RuleTimeAuthoring.unity";
            string absoluteTemporaryPath = Path.Combine(
                Application.dataPath,
                "_Game/Scenes/__Task8RuleTimeAuthoring.unity");
            AssetDatabase.DeleteAsset(temporaryScenePath);
            Assert.That(
                AssetDatabase.CopyAsset(ScenePath, temporaryScenePath),
                Is.True);
            try
            {
                EditorSceneManager.OpenScene(
                    temporaryScenePath,
                    OpenSceneMode.Single);
                GrayboxMobileCityController3D city =
                    Object.FindObjectOfType<
                        GrayboxMobileCityController3D>(true);
                Assert.That(city, Is.Not.Null);
                var serializedCity = new SerializedObject(city);
                serializedCity.FindProperty("ruleTimeSourceBehaviour")
                    .objectReferenceValue = null;
                serializedCity.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(EditorSceneManager.SaveOpenScenes(), Is.True);

                InvokeAuthoringAtPath(temporaryScenePath);
                AssertUniqueCityRuleTimeSourceContract();
                byte[] firstBytes = File.ReadAllBytes(absoluteTemporaryPath);
                string[] firstOwners = CaptureRuleTimeOwnerIds();

                InvokeAuthoringAtPath(temporaryScenePath);

                CollectionAssert.AreEqual(
                    firstBytes,
                    File.ReadAllBytes(absoluteTemporaryPath));
                Assert.That(
                    CaptureRuleTimeOwnerIds(),
                    Is.EqualTo(firstOwners));
                AssertUniqueCityRuleTimeSourceContract();
            }
            finally
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                AssetDatabase.DeleteAsset(temporaryScenePath);
            }
        }

        [Test]
        public void IDEA0014_EvacuationAuthoringTwiceIsByteAndOwnerIdempotent()
        {
            const string temporaryScenePath =
                "Assets/_Game/Scenes/__Task7EvacuationAuthoring.unity";
            string absoluteTemporaryPath = Path.Combine(
                Application.dataPath,
                "_Game/Scenes/__Task7EvacuationAuthoring.unity");
            AssetDatabase.DeleteAsset(temporaryScenePath);
            Assert.That(
                AssetDatabase.CopyAsset(ScenePath, temporaryScenePath),
                Is.True);
            try
            {
                InvokeAuthoringAtPath(temporaryScenePath);
                byte[] firstBytes = File.ReadAllBytes(absoluteTemporaryPath);
                string[] firstOwners = CaptureEvacuationOwnerIds();

                InvokeAuthoringAtPath(temporaryScenePath);

                CollectionAssert.AreEqual(
                    firstBytes,
                    File.ReadAllBytes(absoluteTemporaryPath));
                Assert.That(
                    CaptureEvacuationOwnerIds(),
                    Is.EqualTo(firstOwners));
                Assert.That(
                    Object.FindObjectsOfType<
                        GrayboxEvacuationController3D>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    Object.FindObjectsOfType<
                        GrayboxUsabilityInputCoordinator3D>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    Object.FindObjectsOfType<EventSystem>(true),
                    Has.Length.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                AssetDatabase.DeleteAsset(temporaryScenePath);
            }
        }

        [Test]
        public void IDEA0007_AuthoringUsesDedicatedIdempotentUsabilityContract()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxSceneAuthoring.cs"));
            StringAssert.Contains(
                "EnsureUsabilityContract(",
                source);
            string method = ExtractMethod(
                source,
                "private static void EnsureUsabilityContract",
                "private static void EnsureFirstArtTerrainContract");
            StringAssert.Contains("SystemMenuCanvas", method);
            StringAssert.Contains("GrayboxUsabilityInputCoordinator", method);
            StringAssert.Contains("inputInterceptor", method);
            StringAssert.DoesNotContain("FindObjectOfType", method);
        }

        [Test]
        public void IDEA0007_UsabilityAssemblyIsReferencedBySceneConsumers()
        {
            Assert.That(
                typeof(GrayboxUsabilityInputCoordinator3D).Assembly
                    .GetName().Name,
                Is.EqualTo("WasteCity.Graybox3D.Usability"));
            string[] consumers =
            {
                "_Game/Editor/WasteCity.Editor.asmdef",
                "_Game/Tests/EditMode/WasteCity.EditModeTests.asmdef",
                "_Game/Tests/PlayMode/WasteCity.PlayModeTests.asmdef"
            };
            for (var index = 0; index < consumers.Length; index++)
            {
                string source = File.ReadAllText(
                    Path.Combine(Application.dataPath, consumers[index]));
                StringAssert.Contains(
                    "WasteCity.Graybox3D.Usability",
                    source,
                    consumers[index]);
            }
        }

        [UnityTest]
        public IEnumerator Scene_ReopenedUiModuleHasUsablePublicActions()
        {
            byte[] sceneBytesBefore = File.ReadAllBytes(ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            yield return null;

            InputSystemUIInputModule module =
                Object.FindObjectOfType<InputSystemUIInputModule>(true);
            Assert.That(module, Is.Not.Null);
            AssertUsableAction(module.point, "point");
            AssertUsableAction(module.leftClick, "leftClick");
            AssertUsableAction(module.move, "move");
            AssertUsableAction(module.submit, "submit");
            AssertUsableAction(module.cancel, "cancel");
            CollectionAssert.AreEqual(
                sceneBytesBefore,
                File.ReadAllBytes(ScenePath),
                "EditMode runtime rehydrate must not save lifecycle UI " +
                "objects into the formal scene asset.");
        }

        [Test]
        public void Authoring_RejectsAnExistingSceneWithMissingFoundation()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            new GameObject("GrayboxPrototype3D");
            Type authoring = Type.GetType(
                "WasteCity.Editor.GrayboxSceneAuthoring, WasteCity.Editor");
            Assert.That(authoring, Is.Not.Null);
            MethodInfo validate = authoring.GetMethod(
                "ValidateFoundationContract",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(validate, Is.Not.Null);

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => validate.Invoke(null, new object[] { scene }));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Authoring_RejectsWrongPipelineBeforeMutation()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GrayboxUrpScope scope =
                Object.FindObjectOfType<GrayboxUrpScope>(true);
            UniversalRenderPipelineAsset expected =
                AssetDatabase.LoadAssetAtPath<
                    UniversalRenderPipelineAsset>(PipelinePath);
            try
            {
                SetSerializedReference(scope, "pipelineAsset", null);
                AssertValidationThrows(scene);
            }
            finally
            {
                SetSerializedReference(scope, "pipelineAsset", expected);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void Authoring_RejectsWrongWorldMaterialBeforeMutation()
        {
            Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>(true);
            Material expected =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            try
            {
                SetSerializedReference(world, "sharedMaterial", null);
                AssertValidationThrows(scene);
            }
            finally
            {
                SetSerializedReference(world, "sharedMaterial", expected);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void Authoring_ExistingScenePathValidatesBeforeMutation()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxSceneAuthoring.cs"));
            string existingPath = ExtractMethod(
                source,
                "private static bool TryOpenAndValidateFoundation",
                "private static Scene CreateFoundationScene");
            Assert.That(existingPath, Does.Not.Contain("NewScene"));
            string configure = ExtractMethod(
                source,
                "public static void Configure",
                "public static void CaptureFoundationIdentity");
            Assert.That(
                configure.IndexOf(
                    "TryOpenAndValidateFoundation",
                    StringComparison.Ordinal),
                Is.LessThan(
                    configure.IndexOf(
                        "EnsureRenderer",
                        StringComparison.Ordinal)));
            Assert.That(
                configure.IndexOf(
                    "NormalizeSceneBytes",
                    StringComparison.Ordinal),
                Is.GreaterThan(
                    configure.IndexOf(
                        "SaveScene",
                        StringComparison.Ordinal)));
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

        private static int CountSerializedReferencesTo(Object target)
        {
            var count = 0;
            MonoBehaviour[] owners =
                Object.FindObjectsOfType<MonoBehaviour>(true);
            for (var ownerIndex = 0;
                 ownerIndex < owners.Length;
                 ownerIndex++)
            {
                var serialized = new SerializedObject(owners[ownerIndex]);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType ==
                            SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == target)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static void AssertUniqueCityRuleTimeSourceContract()
        {
            GrayboxMobileCityController3D[] cities =
                Object.FindObjectsOfType<GrayboxMobileCityController3D>(true);
            GrayboxBuildingSession3D[] sessions =
                Object.FindObjectsOfType<GrayboxBuildingSession3D>(true);

            Assert.That(cities, Has.Length.EqualTo(1));
            Assert.That(sessions, Has.Length.EqualTo(1));
            AssertReference(
                cities[0],
                "ruleTimeSourceBehaviour",
                sessions[0]);
        }

        private static string[] CaptureRuleTimeOwnerIds()
        {
            Component[] owners =
            {
                Object.FindObjectOfType<GrayboxMobileCityController3D>(true),
                Object.FindObjectOfType<GrayboxBuildingSession3D>(true),
            };
            var ids = new string[owners.Length];
            for (var index = 0; index < owners.Length; index++)
            {
                Assert.That(owners[index], Is.Not.Null, index.ToString());
                ids[index] = GlobalObjectId.GetGlobalObjectIdSlow(
                    owners[index]).ToString();
            }
            return ids;
        }

        private static void InvokeAuthoringAtPath(string scenePath)
        {
            Type authoring = Type.GetType(
                "WasteCity.Editor.GrayboxSceneAuthoring, WasteCity.Editor");
            Assert.That(authoring, Is.Not.Null);
            MethodInfo configure = authoring.GetMethod(
                "ConfigureSceneAtPath",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(configure, Is.Not.Null);
            configure.Invoke(null, new object[] { scenePath, false, null });
        }

        private static string[] CaptureEvacuationOwnerIds()
        {
            Component[] owners =
            {
                Object.FindObjectOfType<
                    GrayboxEvacuationController3D>(true),
                Object.FindObjectOfType<
                    GrayboxBuildingInputRouter3D>(true),
                Object.FindObjectOfType<
                    GrayboxUsabilityInputCoordinator3D>(true),
                Object.FindObjectOfType<
                    GrayboxOperationsController3D>(true),
                Object.FindObjectOfType<
                    GrayboxProductionController3D>(true),
                Object.FindObjectOfType<
                    GrayboxDefenseController3D>(true),
            };
            var ids = new string[owners.Length];
            for (var index = 0; index < owners.Length; index++)
            {
                Assert.That(owners[index], Is.Not.Null, index.ToString());
                ids[index] = GlobalObjectId.GetGlobalObjectIdSlow(
                    owners[index]).ToString();
            }
            return ids;
        }

        private static string ExtractBuildSettingsAssignment(
            string source)
        {
            const string assignmentStart =
                "EditorBuildSettings.scenes";
            int start = source.LastIndexOf(
                assignmentStart,
                System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int end = source.IndexOf(
                ';',
                start);
            Assert.That(end, Is.GreaterThan(start));
            return source.Substring(start, end - start + 1);
        }

        private static Transform RequiredChild(
            GameObject parent,
            string path)
        {
            Assert.That(parent, Is.Not.Null, path);
            Transform child = parent.transform.Find(path);
            Assert.That(child, Is.Not.Null, path);
            return child;
        }

        private static T RequiredComponent<T>(
            Transform parent,
            string childName)
            where T : Component
        {
            Transform child = RequiredChild(parent.gameObject, childName);
            T component = child.GetComponent<T>();
            Assert.That(component, Is.Not.Null, childName);
            return component;
        }

        private static void AssertUsableAction(
            InputActionReference reference,
            string propertyName)
        {
            Assert.That(reference, Is.Not.Null, propertyName);
            Assert.That(reference.action, Is.Not.Null, propertyName);
            Assert.That(
                reference.action.bindings.Count,
                Is.GreaterThan(0),
                propertyName);
        }

        private static string ExtractMethod(
            string source,
            string startMarker,
            string endMarker)
        {
            int start = source.IndexOf(
                startMarker,
                StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            int end = source.IndexOf(
                endMarker,
                start + startMarker.Length,
                StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
        }

        private static void AssertValidationThrows(Scene scene)
        {
            Type authoring = Type.GetType(
                "WasteCity.Editor.GrayboxSceneAuthoring, WasteCity.Editor");
            MethodInfo validate = authoring.GetMethod(
                "ValidateFoundationContract",
                BindingFlags.NonPublic | BindingFlags.Static);
            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => validate.Invoke(null, new object[] { scene }));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<InvalidOperationException>());
        }

        private static void SetSerializedReference(
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
    }
}
