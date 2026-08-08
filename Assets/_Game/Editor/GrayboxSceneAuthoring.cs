using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Editor
{
    public static class GrayboxSceneAuthoring
    {
        public const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        public const string PipelinePath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset";

        private const string RendererPath =
            "Assets/_Game/Rendering/Graybox3D/" +
            "GrayboxUniversalRenderer.asset";
        private const string MaterialPath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxLit.mat";
        private const string DefaultRendererPath =
            "Packages/com.unity.render-pipelines.universal/" +
            "Runtime/Data/UniversalRendererData.asset";
        private const string LitShaderName =
            "Universal Render Pipeline/Lit";

        [Serializable]
        private sealed class FoundationIdentity
        {
            public string sceneGuid;
            public List<FoundationIdentityEntry> objects;
        }

        [Serializable]
        private sealed class FoundationIdentityEntry
        {
            public string name;
            public string globalObjectId;
        }

        public static void Configure()
        {
            bool hasExistingScene =
                TryOpenAndValidateFoundation(out Scene scene);

            Shader litShader = Shader.Find(LitShaderName);
            if (litShader == null)
                throw new InvalidOperationException(
                    $"Required shader '{LitShaderName}' was not found.");

            EnsureFolder("Assets/_Game", "Rendering");
            EnsureFolder("Assets/_Game/Rendering", "Graybox3D");

            UniversalRendererData renderer = EnsureRenderer();
            UniversalRenderPipelineAsset pipeline =
                EnsurePipeline(renderer);
            Material material = EnsureMaterial(litShader);
            AssetDatabase.SaveAssets();

            if (!hasExistingScene)
                scene = CreateFoundationScene(pipeline, material);
            EnsureBuildingContract(scene, material);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    $"Failed to save graybox scene at '{ScenePath}'.");
            if (scene.path != ScenePath)
                throw new InvalidOperationException(
                    $"Graybox scene was saved to unexpected path " +
                    $"'{scene.path}'.");
            scene = NormalizeSceneBytes();

            EnsureBuildSettings();
        }

        public static void CaptureFoundationIdentity()
        {
            string outputPath = Environment.GetEnvironmentVariable(
                "WASTECITY_GRAYBOX_IDENTITY_RESULT");
            if (string.IsNullOrEmpty(outputPath) ||
                !Path.IsPathRooted(outputPath))
            {
                throw new InvalidOperationException(
                    "WASTECITY_GRAYBOX_IDENTITY_RESULT must be an " +
                    "absolute output path.");
            }

            if (!TryOpenAndValidateFoundation(out Scene scene))
                throw new InvalidOperationException(
                    $"Graybox scene does not exist at '{ScenePath}'.");

            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            GrayboxMobileCityController3D city =
                RequireSingle<GrayboxMobileCityController3D>(scene);
            GrayboxLeaderController3D leader =
                RequireSingle<GrayboxLeaderController3D>(scene);
            Camera camera = RequireSingle<Camera>(scene);
            Transform cameraRig = RequireChild(root.transform, "CameraRig");
            var entries = new List<FoundationIdentityEntry>
            {
                Identity("root.gameObject", root),
                Identity(
                    "renderScope.component",
                    RequireSingle<GrayboxUrpScope>(scene)),
                Identity(
                    "worldView.component",
                    RequireSingle<GrayboxWorldView3D>(scene)),
                Identity(
                    "sceneBootstrap.component",
                    RequireSingle<GrayboxSceneBootstrap>(scene)),
                Identity("mobileCity.gameObject", city.gameObject),
                Identity("mobileCity.controller", city),
                Identity(
                    "mobileCity.rigidbody",
                    RequireComponent<Rigidbody>(city.gameObject)),
                Identity("leader.gameObject", leader.gameObject),
                Identity("leader.controller", leader),
                Identity("cameraRig.gameObject", cameraRig.gameObject),
                Identity("mainCamera.gameObject", camera.gameObject),
                Identity("mainCamera.component", camera),
                Identity(
                    "inputRouter.component",
                    RequireSingle<GrayboxInputRouter>(scene)),
                Identity(
                    "groundProjector.component",
                    RequireSingle<GrayboxGroundProjector>(scene)),
                Identity(
                    "directControl.component",
                    RequireSingle<GrayboxDirectControlCoordinator>(scene)),
                Identity(
                    "cameraController.component",
                    RequireSingle<GrayboxCameraController3D>(scene))
            };
            entries.Sort(
                (left, right) => string.CompareOrdinal(
                    left.name,
                    right.name));
            var payload = new FoundationIdentity
            {
                sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath),
                objects = entries
            };
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                outputPath,
                JsonUtility.ToJson(payload, true));
        }

        private static UniversalRendererData EnsureRenderer()
        {
            UniversalRendererData renderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    RendererPath);
            if (renderer != null)
                return renderer;

            UniversalRendererData packageDefault =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    DefaultRendererPath);
            if (packageDefault == null)
                throw new InvalidOperationException(
                    $"URP Universal Renderer template was not found at " +
                    $"'{DefaultRendererPath}'.");

            renderer = UnityEngine.Object.Instantiate(packageDefault);
            renderer.name = "GrayboxUniversalRenderer";
            AssetDatabase.CreateAsset(renderer, RendererPath);
            return renderer;
        }

        private static UniversalRenderPipelineAsset EnsurePipeline(
            UniversalRendererData renderer)
        {
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<
                    UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "GrayboxURP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            var serialized = new SerializedObject(pipeline);
            SerializedProperty rendererList =
                serialized.FindProperty("m_RendererDataList");
            SerializedProperty defaultRenderer =
                serialized.FindProperty("m_DefaultRendererIndex");
            if (rendererList == null || defaultRenderer == null)
                throw new InvalidOperationException(
                    "URP renderer serialization contract is unavailable.");

            bool changed =
                rendererList.arraySize != 1 ||
                rendererList.GetArrayElementAtIndex(0)
                    .objectReferenceValue != renderer ||
                defaultRenderer.intValue != 0;
            if (changed)
            {
                rendererList.arraySize = 1;
                rendererList.GetArrayElementAtIndex(0)
                    .objectReferenceValue = renderer;
                defaultRenderer.intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }
            return pipeline;
        }

        private static Material EnsureMaterial(Shader litShader)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(litShader)
                {
                    name = "GrayboxLit"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != litShader)
            {
                material.shader = litShader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static bool TryOpenAndValidateFoundation(out Scene scene)
        {
            scene = default;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                return false;

            scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            ValidateFoundationContract(scene);
            return true;
        }

        private static Scene CreateFoundationScene(
            UniversalRenderPipelineAsset pipeline,
            Material material)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("GrayboxPrototype3D");

            Transform renderScopeTransform =
                NewChild(root.transform, "GrayboxRenderScope");
            GrayboxUrpScope renderScope =
                renderScopeTransform.gameObject
                    .AddComponent<GrayboxUrpScope>();
            renderScope.Configure(pipeline);

            Transform world = NewChild(root.transform, "GrayboxWorld");
            Transform terrain = NewChild(world, "TerrainRoot");
            Transform resources = NewChild(world, "ResourceRoot");
            Transform obstacles = NewChild(world, "ObstacleRoot");
            GrayboxWorldView3D worldView =
                world.gameObject.AddComponent<GrayboxWorldView3D>();
            worldView.Configure(terrain, resources, obstacles, material);

            Transform systems = NewChild(root.transform, "GrayboxSystems");
            Transform bootstrapTransform =
                NewChild(systems, "GrayboxSceneBootstrap");
            GrayboxSceneBootstrap bootstrap =
                bootstrapTransform.gameObject
                    .AddComponent<GrayboxSceneBootstrap>();
            bootstrap.Configure(renderScope, worldView);

            Transform actors =
                NewChild(root.transform, "GrayboxActors");
            GrayboxMobileCityController3D city =
                CreateMobileCity(actors, worldView, material);
            GrayboxLeaderController3D leader =
                CreateLeader(actors, worldView, city, material);

            Transform cameraRig =
                NewChild(root.transform, "CameraRig");
            cameraRig.position = new Vector3(
                city.transform.position.x,
                0f,
                city.transform.position.z);
            Transform cameraTransform =
                NewChild(cameraRig, "Main Camera");
            cameraTransform.localPosition =
                new Vector3(0f, 18f, -14f);
            cameraTransform.localEulerAngles =
                new Vector3(52f, 0f, 0f);
            Camera camera =
                cameraTransform.gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 13f;
            cameraTransform.gameObject.tag = "MainCamera";

            Transform directControlTransform =
                NewChild(systems, "GrayboxDirectControl");
            GrayboxDirectControlCoordinator directControl =
                directControlTransform.gameObject.AddComponent<
                    GrayboxDirectControlCoordinator>();
            directControl.Configure(city, leader);

            Transform projectorTransform =
                NewChild(systems, "GrayboxGroundProjector");
            GrayboxGroundProjector projector =
                projectorTransform.gameObject
                    .AddComponent<GrayboxGroundProjector>();
            projector.Configure(camera, worldView);

            GrayboxCameraController3D cameraController =
                cameraRig.gameObject.AddComponent<
                    GrayboxCameraController3D>();
            cameraController.Configure(
                camera,
                cameraRig,
                city,
                leader,
                directControl,
                projector);

            Transform inputTransform =
                NewChild(systems, "GrayboxInputRouter");
            GrayboxInputRouter inputRouter =
                inputTransform.gameObject
                    .AddComponent<GrayboxInputRouter>();
            inputRouter.Configure(
                city,
                leader,
                directControl,
                projector,
                cameraController);

            return scene;
        }

        private static void ValidateFoundationContract(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException(
                    "The graybox foundation scene must be loaded.");

            UniversalRenderPipelineAsset approvedPipeline =
                AssetDatabase.LoadAssetAtPath<
                    UniversalRenderPipelineAsset>(PipelinePath);
            Material approvedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (approvedPipeline == null)
                throw new InvalidOperationException(
                    $"The approved graybox pipeline is missing at " +
                    $"'{PipelinePath}'.");
            if (approvedMaterial == null)
                throw new InvalidOperationException(
                    $"The approved graybox material is missing at " +
                    $"'{MaterialPath}'.");

            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            Transform renderScopeTransform =
                RequireChild(root.transform, "GrayboxRenderScope");
            Transform worldTransform =
                RequireChild(root.transform, "GrayboxWorld");
            Transform terrain =
                RequireChild(worldTransform, "TerrainRoot");
            Transform resources =
                RequireChild(worldTransform, "ResourceRoot");
            Transform obstacles =
                RequireChild(worldTransform, "ObstacleRoot");
            Transform systems =
                RequireChild(root.transform, "GrayboxSystems");
            Transform bootstrapTransform =
                RequireChild(systems, "GrayboxSceneBootstrap");
            Transform actors =
                RequireChild(root.transform, "GrayboxActors");
            Transform cityTransform = RequireChild(actors, "MobileCity");
            Transform leaderTransform =
                RequireChild(actors, "Leader_CenJin");
            Transform cameraRig =
                RequireChild(root.transform, "CameraRig");
            Transform cameraTransform =
                RequireChild(cameraRig, "Main Camera");
            Transform directControlTransform =
                RequireChild(systems, "GrayboxDirectControl");
            Transform projectorTransform =
                RequireChild(systems, "GrayboxGroundProjector");
            Transform inputTransform =
                RequireChild(systems, "GrayboxInputRouter");

            GrayboxUrpScope renderScope =
                RequireSingle<GrayboxUrpScope>(scene);
            GrayboxWorldView3D worldView =
                RequireSingle<GrayboxWorldView3D>(scene);
            GrayboxSceneBootstrap bootstrap =
                RequireSingle<GrayboxSceneBootstrap>(scene);
            GrayboxMobileCityController3D city =
                RequireSingle<GrayboxMobileCityController3D>(scene);
            GrayboxLeaderController3D leader =
                RequireSingle<GrayboxLeaderController3D>(scene);
            Camera camera = RequireSingle<Camera>(scene);
            GrayboxDirectControlCoordinator directControl =
                RequireSingle<GrayboxDirectControlCoordinator>(scene);
            GrayboxGroundProjector projector =
                RequireSingle<GrayboxGroundProjector>(scene);
            GrayboxCameraController3D cameraController =
                RequireSingle<GrayboxCameraController3D>(scene);
            GrayboxInputRouter inputRouter =
                RequireSingle<GrayboxInputRouter>(scene);
            Rigidbody body = RequireComponent<Rigidbody>(city.gameObject);
            BoxCollider bodyCollider =
                RequireComponent<BoxCollider>(city.gameObject);

            RequireOwner(renderScope, renderScopeTransform);
            RequireOwner(worldView, worldTransform);
            RequireOwner(bootstrap, bootstrapTransform);
            RequireOwner(city, cityTransform);
            RequireOwner(leader, leaderTransform);
            RequireOwner(camera, cameraTransform);
            RequireOwner(directControl, directControlTransform);
            RequireOwner(projector, projectorTransform);
            RequireOwner(inputRouter, inputTransform);
            RequireOwner(cameraController, cameraRig);

            RequireReference(bootstrap, "renderScope", renderScope);
            RequireReference(bootstrap, "worldView", worldView);
            RequireReference(
                renderScope,
                "pipelineAsset",
                approvedPipeline);
            RequireReference(worldView, "terrainRoot", terrain);
            RequireReference(worldView, "resourceRoot", resources);
            RequireReference(worldView, "obstacleRoot", obstacles);
            RequireReference(
                worldView,
                "sharedMaterial",
                approvedMaterial);
            RequireReference(city, "worldView", worldView);
            RequireReference(city, "body", body);
            RequireReference(city, "bodyCollider", bodyCollider);
            RequireReference(leader, "worldView", worldView);
            RequireReference(leader, "city", city);
            RequireReference(directControl, "city", city);
            RequireReference(directControl, "leader", leader);
            RequireReference(projector, "controlledCamera", camera);
            RequireReference(projector, "worldView", worldView);
            RequireReference(
                cameraController,
                "controlledCamera",
                camera);
            RequireReference(cameraController, "cameraRig", cameraRig);
            RequireReference(cameraController, "city", city);
            RequireReference(cameraController, "leader", leader);
            RequireReference(
                cameraController,
                "directControl",
                directControl);
            RequireReference(
                cameraController,
                "groundProjector",
                projector);
            RequireReference(inputRouter, "city", city);
            RequireReference(inputRouter, "leader", leader);
            RequireReference(inputRouter, "directControl", directControl);
            RequireReference(inputRouter, "groundProjector", projector);
            RequireReference(
                inputRouter,
                "cameraController",
                cameraController);
        }

        private static void EnsureBuildingContract(
            Scene scene,
            Material material)
        {
            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            Transform building = EnsureChild(root.transform, "GrayboxBuilding");
            Transform sessionTransform =
                EnsureChild(building, "BuildingSession");
            GrayboxBuildingSession3D session =
                EnsureComponent<GrayboxBuildingSession3D>(sessionTransform);
            SetBool(session, "developmentFixtureEnabled", true);

            Transform interactionTransform =
                EnsureChild(building, "BuildingInteraction");
            GrayboxBuildingInteractionModel3D interaction =
                EnsureComponent<GrayboxBuildingInteractionModel3D>(
                    interactionTransform);

            Transform presentationTransform =
                EnsureChild(building, "BuildingPresentation");
            Transform instanceRoot =
                EnsureChild(presentationTransform, "InstanceRoot");
            Transform infrastructureRoot =
                EnsureChild(presentationTransform, "InfrastructureRoot");
            GrayboxBuildingWorldView3D presentation =
                EnsureComponent<GrayboxBuildingWorldView3D>(
                    presentationTransform);

            GrayboxMobileCityController3D city =
                RequireSingle<GrayboxMobileCityController3D>(scene);
            GrayboxWorldView3D world =
                RequireSingle<GrayboxWorldView3D>(scene);
            Camera camera = RequireSingle<Camera>(scene);
            BoxCollider innerSurface = EnsureInnerCityPlatform(
                city.transform,
                material);

            Transform ui = EnsureChild(root.transform, "GrayboxUI");
            Transform canvasTransform = EnsureChild(ui, "BuildingCanvas");
            canvasTransform.gameObject.layer = 5;
            Canvas canvas = EnsureComponent<Canvas>(canvasTransform);
            canvasTransform = canvas.transform;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            EnsureComponent<CanvasScaler>(canvasTransform);
            GraphicRaycaster raycaster =
                EnsureComponent<GraphicRaycaster>(canvasTransform);
            raycaster.enabled = true;

            EventSystem eventSystem = EnsureSingleEventSystem(scene, ui);
            InputSystemUIInputModule uiModule =
                EnsureSingleUiInputModule(scene, eventSystem);
            if (!HasUsableUiActions(uiModule))
            {
                uiModule.AssignDefaultActions();
                EditorUtility.SetDirty(uiModule);
            }
            if (!HasUsableUiActions(uiModule))
                throw new InvalidOperationException(
                    "InputSystemUIInputModule default public actions are " +
                    "not usable after AssignDefaultActions().");

            GrayboxBuildingMenuView3D menu =
                EnsureComponent<GrayboxBuildingMenuView3D>(canvasTransform);

            Transform projectorTransform =
                EnsureChild(building, "BuildingSurfaceProjector");
            GrayboxBuildingSurfaceProjector3D surfaceProjector =
                EnsureComponent<GrayboxBuildingSurfaceProjector3D>(
                    projectorTransform);
            Transform placementTransform =
                EnsureChild(building, "BuildingPlacement");
            GrayboxBuildingPlacementController3D placement =
                EnsureComponent<GrayboxBuildingPlacementController3D>(
                    placementTransform);
            Transform constructionTransform =
                EnsureChild(building, "Construction");
            GrayboxConstructionController3D construction =
                EnsureComponent<GrayboxConstructionController3D>(
                    constructionTransform);
            Transform evacuationTransform =
                EnsureChild(building, "Evacuation");
            GrayboxEvacuationController3D evacuation =
                EnsureComponent<GrayboxEvacuationController3D>(
                    evacuationTransform);
            Transform inputTransform = EnsureChild(building, "BuildingInput");
            GrayboxBuildingInputRouter3D buildingInput =
                EnsureComponent<GrayboxBuildingInputRouter3D>(inputTransform);
            Transform developerTransform =
                EnsureChild(building, "DeveloperModifierBootstrap");
            GrayboxDeveloperModifierBootstrap3D developer =
                EnsureComponent<GrayboxDeveloperModifierBootstrap3D>(
                    developerTransform);

            SetReferences(
                presentation,
                ("instanceRoot", instanceRoot),
                ("infrastructureRoot", infrastructureRoot),
                ("sharedMaterial", material),
                ("city", city));
            SetReferences(
                surfaceProjector,
                ("controlledCamera", camera),
                ("worldView", world),
                ("city", city),
                ("innerCitySurface", innerSurface));
            SetReferences(
                placement,
                ("session", session),
                ("city", city),
                ("world", world),
                ("projector", surfaceProjector),
                ("presentation", presentation),
                ("interaction", interaction));
            SetReferences(
                menu,
                ("canvas", canvas),
                ("eventSystem", eventSystem),
                ("session", session),
                ("interaction", interaction),
                ("placement", placement));
            SetReferences(
                construction,
                ("session", session),
                ("city", city),
                ("presentation", presentation),
                ("interaction", interaction),
                ("controlledCamera", camera),
                ("menu", menu));
            SetReferences(
                evacuation,
                ("session", session),
                ("city", city),
                ("presentation", presentation),
                ("menu", menu));
            SetReferences(
                developer,
                ("session", session),
                ("city", city),
                ("presentation", presentation),
                ("canvas", canvas));
            SetReferences(
                buildingInput,
                ("menu", menu),
                ("interaction", interaction),
                ("placement", placement),
                ("construction", construction),
                ("evacuation", evacuation),
                ("developer", developer));
            SetReferences(
                RequireSingle<GrayboxInputRouter>(scene),
                ("inputInterceptor", buildingInput));

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GrayboxMobileCityController3D CreateMobileCity(
            Transform actors,
            GrayboxWorldView3D worldView,
            Material material)
        {
            Transform cityTransform =
                NewChild(actors, "MobileCity");
            var coordinates = new PlanarCoordinateMapper3D(
                GrayboxSceneBootstrap.WorldWidth,
                GrayboxSceneBootstrap.WorldHeight);
            coordinates.TryCellToWorld(
                8,
                7,
                .5f,
                out Vector3 cityPosition);
            cityTransform.position = cityPosition;

            Rigidbody body =
                cityTransform.gameObject.AddComponent<Rigidbody>();
            BoxCollider bodyCollider =
                cityTransform.gameObject.AddComponent<BoxCollider>();
            bodyCollider.size = new Vector3(3f, 1f, 2f);

            GameObject visual =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "MobileCityVisual";
            visual.transform.SetParent(cityTransform, false);
            visual.transform.localScale =
                new Vector3(3f, 1f, 2f);
            UnityEngine.Object.DestroyImmediate(
                visual.GetComponent<Collider>());
            MeshRenderer renderer =
                visual.GetComponent<MeshRenderer>();
            var slot = visual.AddComponent<GrayboxVisualSlot>();
            slot.Configure(
                "core.city.mobile",
                renderer,
                new Color(.9f, .48f, .1f));
            slot.ApplyFallback(material);

            GrayboxMobileCityController3D city =
                cityTransform.gameObject.AddComponent<
                    GrayboxMobileCityController3D>();
            city.Configure(worldView, body, bodyCollider);
            return city;
        }

        private static GrayboxLeaderController3D CreateLeader(
            Transform actors,
            GrayboxWorldView3D worldView,
            GrayboxMobileCityController3D city,
            Material material)
        {
            GameObject leaderObject =
                GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leaderObject.name = "Leader_CenJin";
            leaderObject.transform.SetParent(actors, false);
            leaderObject.transform.position =
                city.transform.position +
                new Vector3(1.8f, .5f, 1.2f);
            MeshRenderer renderer =
                leaderObject.GetComponent<MeshRenderer>();
            var slot =
                leaderObject.AddComponent<GrayboxVisualSlot>();
            slot.Configure(
                "core.character.cen-jin",
                renderer,
                new Color(.2f, .68f, .92f));
            slot.ApplyFallback(material);

            GrayboxLeaderController3D leader =
                leaderObject.AddComponent<
                    GrayboxLeaderController3D>();
            leader.Configure(worldView, city, true);
            return leader;
        }

        private static BoxCollider EnsureInnerCityPlatform(
            Transform city,
            Material material)
        {
            Transform platform = FindDirectChild(city, "InnerCityPlatform");
            if (platform == null)
            {
                GameObject primitive =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);
                primitive.name = "InnerCityPlatform";
                primitive.transform.SetParent(city, false);
                platform = primitive.transform;
            }

            BoxCollider cityCollider =
                RequireComponent<BoxCollider>(city.gameObject);
            const float platformThickness = .01f;
            float cityBodyTop =
                cityCollider.center.y + cityCollider.size.y * .5f;
            platform.localPosition = new Vector3(
                0f,
                cityBodyTop + platformThickness * .5f,
                0f);
            platform.localRotation = Quaternion.identity;
            platform.localScale = new Vector3(
                2.56f,
                platformThickness,
                1.92f);
            BoxCollider collider = EnsureComponent<BoxCollider>(platform);
            collider.center = Vector3.zero;
            collider.size = Vector3.one;
            MeshRenderer renderer =
                EnsureComponent<MeshRenderer>(platform);
            renderer.sharedMaterial = material;
            return collider;
        }

        private static Scene NormalizeSceneBytes()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException(
                    "Unity project root could not be resolved.");
            string absolutePath = Path.Combine(projectRoot, ScenePath);
            byte[] source = File.ReadAllBytes(absolutePath);
            var normalized = new byte[source.Length];
            int count = 0;
            for (var index = 0; index < source.Length; index++)
            {
                byte value = source[index];
                if (value == (byte)'\r' || value == (byte)'\n')
                {
                    while (count > 0 &&
                           (normalized[count - 1] == (byte)' ' ||
                            normalized[count - 1] == (byte)'\t'))
                        count--;
                }
                normalized[count++] = value;
            }
            while (count > 0 &&
                   (normalized[count - 1] == (byte)' ' ||
                    normalized[count - 1] == (byte)'\t'))
                count--;

            if (count != source.Length)
            {
                var finalBytes = new byte[count];
                Buffer.BlockCopy(normalized, 0, finalBytes, 0, count);
                File.WriteAllBytes(absolutePath, finalBytes);
            }

            AssetDatabase.ImportAsset(
                ScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            ValidateFoundationContract(scene);
            return scene;
        }

        private static EventSystem EnsureSingleEventSystem(
            Scene scene,
            Transform ui)
        {
            List<EventSystem> systems = FindSceneComponents<EventSystem>(scene);
            if (systems.Count > 1)
                throw new InvalidOperationException(
                    "The graybox scene contains multiple EventSystem " +
                    "components.");
            if (systems.Count == 1)
            {
                EventSystem existing = systems[0];
                existing.gameObject.name = "EventSystem";
                existing.transform.SetParent(ui, false);
                return existing;
            }

            return EnsureComponent<EventSystem>(
                EnsureChild(ui, "EventSystem"));
        }

        private static InputSystemUIInputModule EnsureSingleUiInputModule(
            Scene scene,
            EventSystem eventSystem)
        {
            List<InputSystemUIInputModule> modules =
                FindSceneComponents<InputSystemUIInputModule>(scene);
            if (modules.Count > 1)
                throw new InvalidOperationException(
                    "The graybox scene contains multiple " +
                    "InputSystemUIInputModule components.");
            if (modules.Count == 1)
            {
                if (modules[0].gameObject != eventSystem.gameObject)
                    throw new InvalidOperationException(
                        "InputSystemUIInputModule must share the EventSystem " +
                        "GameObject.");
                return modules[0];
            }
            return eventSystem.gameObject.AddComponent<
                InputSystemUIInputModule>();
        }

        private static bool HasUsableUiActions(
            InputSystemUIInputModule module)
        {
            return HasUsableAction(module.point) &&
                   HasUsableAction(module.leftClick) &&
                   HasUsableAction(module.move) &&
                   HasUsableAction(module.submit) &&
                   HasUsableAction(module.cancel);
        }

        private static bool HasUsableAction(InputActionReference reference)
        {
            return reference != null &&
                   reference.action != null &&
                   reference.action.bindings.Count > 0;
        }

        private static FoundationIdentityEntry Identity(
            string name,
            UnityEngine.Object value)
        {
            return new FoundationIdentityEntry
            {
                name = name,
                globalObjectId =
                    GlobalObjectId.GetGlobalObjectIdSlow(value).ToString()
            };
        }

        private static GameObject RequireRoot(Scene scene, string name)
        {
            GameObject result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!string.Equals(
                        root.name,
                        name,
                        StringComparison.Ordinal))
                    continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"Foundation root '{name}' is duplicated.");
                result = root;
            }
            if (result == null)
                throw new InvalidOperationException(
                    $"Foundation root '{name}' is missing.");
            return result;
        }

        private static Transform RequireChild(
            Transform parent,
            string name)
        {
            Transform child = FindDirectChild(parent, name);
            if (child == null)
                throw new InvalidOperationException(
                    $"Foundation child '{parent.name}/{name}' is missing.");
            return child;
        }

        private static Transform FindDirectChild(
            Transform parent,
            string name)
        {
            Transform result = null;
            for (var index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (!string.Equals(
                        child.name,
                        name,
                        StringComparison.Ordinal))
                    continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"Child '{parent.name}/{name}' is duplicated.");
                result = child;
            }
            return result;
        }

        private static Transform EnsureChild(
            Transform parent,
            string name)
        {
            Transform existing = FindDirectChild(parent, name);
            return existing ?? NewChild(parent, name);
        }

        private static T RequireSingle<T>(Scene scene)
            where T : Component
        {
            List<T> values = FindSceneComponents<T>(scene);
            if (values.Count != 1)
                throw new InvalidOperationException(
                    $"Foundation requires exactly one {typeof(T).Name}; " +
                    $"found {values.Count}.");
            return values[0];
        }

        private static List<T> FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        private static T RequireComponent<T>(GameObject owner)
            where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException(
                    $"Foundation object '{owner.name}' is missing " +
                    $"{typeof(T).Name}.");
            return component;
        }

        private static T EnsureComponent<T>(Transform owner)
            where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null
                ? component
                : owner.gameObject.AddComponent<T>();
        }

        private static void RequireOwner(
            Component component,
            Transform expected)
        {
            if (component.transform != expected)
                throw new InvalidOperationException(
                    $"Foundation {component.GetType().Name} is attached to " +
                    $"'{component.gameObject.name}', expected " +
                    $"'{expected.name}'.");
        }

        private static void RequireReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object expected)
        {
            var data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(propertyName);
            if (property == null ||
                property.objectReferenceValue != expected)
            {
                throw new InvalidOperationException(
                    $"Foundation reference {owner.GetType().Name}." +
                    $"{propertyName} is missing or invalid.");
            }
        }

        private static void SetReferences(
            UnityEngine.Object owner,
            params (string propertyName, UnityEngine.Object value)[] values)
        {
            var data = new SerializedObject(owner);
            foreach ((string propertyName, UnityEngine.Object value) in values)
            {
                SerializedProperty property =
                    data.FindProperty(propertyName);
                if (property == null)
                    throw new InvalidOperationException(
                        $"Serialized property {owner.GetType().Name}." +
                        $"{propertyName} is unavailable.");
                property.objectReferenceValue = value;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void SetBool(
            UnityEngine.Object owner,
            string propertyName,
            bool value)
        {
            var data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    $"Serialized property {owner.GetType().Name}." +
                    $"{propertyName} is unavailable.");
            property.boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void EnsureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(
                    "Assets/_Game/Scenes/FormalPrototype.unity",
                    true)
            };
        }

        private static Transform NewChild(
            Transform parent,
            string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void EnsureFolder(
            string parent,
            string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
