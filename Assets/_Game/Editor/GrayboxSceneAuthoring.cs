using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using WasteCity.Graybox3D;

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

        public static void Configure()
        {
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

            Scene scene = BuildScene(pipeline, material);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    $"Failed to save graybox scene at '{ScenePath}'.");
            if (scene.path != ScenePath)
                throw new InvalidOperationException(
                    $"Graybox scene was saved to unexpected path " +
                    $"'{scene.path}'.");

            EnsureBuildSettings();
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

        private static Scene BuildScene(
            UniversalRenderPipelineAsset pipeline,
            Material material)
        {
            if (TryOpenCompleteScene(out Scene existing))
                return existing;

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

        private static bool TryOpenCompleteScene(out Scene scene)
        {
            scene = default;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    ScenePath) == null)
                return false;

            scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            GameObject root =
                GameObject.Find("GrayboxPrototype3D");
            return root != null &&
                   root.transform.Find(
                       "GrayboxRenderScope") != null &&
                   root.transform.Find(
                       "GrayboxWorld/TerrainRoot") != null &&
                   root.transform.Find(
                       "GrayboxActors/MobileCity") != null &&
                   root.transform.Find(
                       "GrayboxActors/Leader_CenJin") != null &&
                   root.transform.Find(
                       "CameraRig/Main Camera") != null &&
                   UnityEngine.Object.FindObjectsOfType<
                       GrayboxInputRouter>(true).Length == 1 &&
                   UnityEngine.Object.FindObjectsOfType<
                       GrayboxGroundProjector>(true).Length == 1 &&
                   UnityEngine.Object.FindObjectsOfType<
                       GrayboxDirectControlCoordinator>(true).Length == 1;
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

        private static void EnsureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(
                    "Assets/_Game/Scenes/FormalPrototype.unity",
                    true),
                new EditorBuildSettingsScene(ScenePath, true)
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
