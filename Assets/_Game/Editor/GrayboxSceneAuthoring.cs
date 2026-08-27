using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WasteCity.ArtIntegration3D;
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.World;

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
        private const string PreviewMaterialPath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxPreview.mat";
        private const string ResourceIconCatalogPath =
            "Assets/_Game/Rendering/Graybox3D/ResourceIconCatalog3D.asset";
        private const string WorldPresentationScaleProfilePath =
            "Assets/_Game/Resources/Presentation/" +
            "FormalWorldPresentationScaleProfile3D.asset";
        private const string DefaultRendererPath =
            "Packages/com.unity.render-pipelines.universal/" +
            "Runtime/Data/UniversalRendererData.asset";
        private const string LitShaderName =
            "Universal Render Pipeline/Lit";
        private const string TerrainProfileGuid =
            "c2c0273740a844e7bbbee5d059d8af8e";
        private const string TerrainMaterialGuid =
            "aea10c6322a834daf8dd881841c99f7a";
        private const string TerrainShaderGuid =
            "c22a7d023c25475194aa0de160b7543e";
        private const string TerrainBaseColorGuid =
            "b1f08156ff6c64dbca1d51a8132ccc2d";
        private const string TerrainNormalGuid =
            "11955d2e24e2d4226b06b086fe3b072a";
        private const string TerrainMaskGuid =
            "417640ccdd04a4121a7180da2ce71277";
        private const string TerrainHeightGuid =
            "9263e65e9f2694421b9fbdb96b3f49a3";
        private const string RuinsCliffProfileGuid =
            "6b73f8e68c02943658e63225766dd256";
        private const string RuinsCliffShaderGuid =
            "e7ef7b490240461faab128d139bdac74";
        private const string TerrainLightName =
            "FirstArtTerrainDirectionalLight";
        private const float TerrainLightIntensity = .90f;
        private const float TerrainLightAngleTolerance = .01f;

        private static readonly Color TerrainLightColor =
            new Color(1f, .956f, .85f, 1f);
        private static readonly Vector3 TerrainLightEuler =
            new Vector3(50f, -30f, 0f);

        private static readonly string[] RuinsCliffFbxGuids =
        {
            "74e0ae6e3a4e045d1879057e4ea83f5e",
            "a149c94a62a2e4fe2a6e71c76421e675",
            "349cad0c00bf34b86a2750726e7121f7",
            "2744c2b66e972468f8cd87761f6dea3e",
            "592cc0ce77f7346e79d621d6e7c6c849",
            "a1c090a2695a940f2bb810af1f2ed3c9",
            "9ab7b78a7195f4702a6f7989e00eb857",
            "6bb08a29e92924947944d0311102247d",
            "50c1bb78dbd354435ac7c1a0b28628da",
            "e13ec3db45dec4203a5c92378746bf55",
            "c86c3d69e77304a169123123fa0f3b42",
            "54bdbab6d36ec481ba1302edd418e863",
            "45d470060d7674595b6182e9ff11af65",
            "ecf58233b44324484bf41a41fc70b1e9",
        };

        private static readonly string[] RuinsCliffPrefabGuids =
        {
            "f341ced2e61394295b2b2529d92f2df1",
            "291293ada4a99454686c2363f808e10e",
            "127ccec2e38214c14a3bb96549a83016",
            "ba097d85457e7469c9bf81cc6303f906",
            "d90169849218a4597be2b3ed9e6ee9a6",
            "17f6c3887760c41d59e30a6dbc38c15a",
            "f064da28e02d74bdca340008cb26ad7e",
            "531e0a2a01252465c808487770194704",
            "7b37f81a7fdc3406ea9c462d56919bc6",
            "6bb8272b032f0406aa3490694a316c2a",
            "344999e5fa45c4719a7b2f2476cfe351",
            "c5af4a6f4c3cb4f1aae3e82bf94a200f",
            "7f691f28c3d474aadb540a0ff69facce",
            "65d6a7e1ca6b44584b25846b97a663c1",
        };

        private static readonly string[] RuinsCliffMaterialGuids =
        {
            "be01d9d1a28234ddf91a3d8c0707190e",
            "7b30f196df8784365977e5b59648eb80",
            "254a4ae5ae9864b59ab84874141ca44a",
            "926b4e68afe9f47f1abd9fc6e9ac2c25",
            "7c24f356a01d74ff5aeffb91bfbde7ac",
            "fea5ba8f1bcce4af0a9966217773270d",
            "fe3969b76bb10448896d0051363ef6a6",
            "854500339ac804983b0822b173e5c248",
            "507bee9b0ade044d891034434ee7d352",
            "9e115428bbc7540138aea316c67202dc",
            "e896518daa9c546fdbda64e093a3b5d1",
            "125ad5b32ccf9436eb5e9a4064bf023e",
            "8225153fe66424e57b40c90b933930e7",
        };

        private const string FirstBaseColorSourcePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/" +
            "Wasteland/T_Terrain_Wasteland_BaseColor.png";
        private const string FirstNormalSourcePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/" +
            "Wasteland/T_Terrain_Wasteland_Normal.png";
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

        private sealed class ApprovedTerrainAssets
        {
            public FirstArtTerrainProfile3D Profile;
            public Material Material;
            public Shader Shader;
            public Texture2DArray BaseColor;
            public Texture2DArray Normal;
            public Texture2DArray Mask;
            public Texture2DArray Height;
        }

        private sealed class ApprovedRuinsCliffAssets
        {
            public FirstArtRuinsCliffProfile3D Profile;
            public Shader Shader;
            public GameObject[] Fbx;
            public GameObject[] Prefabs;
            public Material[] Materials;
        }

        private sealed class BuildingContractReferences
        {
            public Transform Systems;
            public Transform Ui;
            public Canvas BuildingCanvas;
            public EventSystem EventSystem;
            public GrayboxSceneBootstrap Bootstrap;
            public GrayboxBuildingSession3D Session;
            public GrayboxMobileCityController3D City;
            public GrayboxDirectControlCoordinator DirectControl;
            public GrayboxWorldView3D World;
            public GrayboxLeaderController3D Leader;
            public GrayboxBuildingWorldView3D BuildingPresentation;
            public Material SharedMaterial;
            public GrayboxProductionController3D Production;
            public GrayboxDefenseWorldView3D DefenseWorldView;
            public GrayboxDefenseController3D DefenseController;
            public GrayboxEvacuationController3D Evacuation;
            public GrayboxBuildingInputRouter3D BuildingInput;
            public GrayboxDeveloperModifierBootstrap3D Developer;
        }

        private sealed class AuthoringHooks
        {
            public Action BeforeRuntimeAssetBuilder;
            public Action BeforeSceneMutation;
            public Action BeforeSceneSave;
        }

        private sealed class AuthoringPreflightException : Exception
        {
            public AuthoringPreflightException(
                string message,
                Exception innerException)
                : base(message, innerException)
            {
            }
        }

        private enum SerializedReferenceState
        {
            Null = 0,
            Live = 1,
            Missing = 2,
        }

        public static void Configure()
        {
            ConfigureSceneAtPath(ScenePath, true, null);
        }

        private static void ConfigureSceneAtPath(
            string targetScenePath,
            bool updateBuildSettings,
            AuthoringHooks hooks)
        {
            if (string.IsNullOrEmpty(targetScenePath) ||
                !targetScenePath.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal) ||
                !targetScenePath.EndsWith(
                    ".unity",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Scene path must be a project-relative Assets path " +
                    "ending in .unity.",
                    nameof(targetScenePath));
            }

            LoadApprovedRuinsCliffAssetsOrThrow();
            bool hasExistingScene =
                TryOpenAndValidateFoundation(
                    targetScenePath,
                    out Scene scene,
                    hooks);

            ApprovedTerrainAssets terrainAssets =
                EnsureApprovedTerrainAssets(hooks);
            ApprovedRuinsCliffAssets geometryAssets =
                LoadApprovedRuinsCliffAssetsOrThrow();
            if (hasExistingScene)
            {
                hooks?.BeforeSceneMutation?.Invoke();
                EnsureFirstArtTerrainContract(
                    scene,
                    terrainAssets.Profile,
                    geometryAssets.Profile);
                EnsureFirstArtTerrainLighting(scene);
            }

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
            Material previewMaterial = EnsurePreviewMaterial(litShader);
            ResourceIconCatalog3D resourceIconCatalog =
                EnsureResourceIconCatalog();
            AssetDatabase.SaveAssets();

            if (!hasExistingScene)
            {
                hooks?.BeforeSceneMutation?.Invoke();
                scene = CreateFoundationScene(pipeline, material);
            }
            BuildingContractReferences buildingReferences =
                EnsureBuildingContract(
                    scene,
                    material,
                    previewMaterial,
                    resourceIconCatalog);
            EnsureUsabilityContract(
                scene,
                buildingReferences,
                resourceIconCatalog);
            EnsurePlayableInitialDeployment(scene);
            if (!hasExistingScene)
            {
                EnsureFirstArtTerrainContract(
                    scene,
                    terrainAssets.Profile,
                    geometryAssets.Profile);
                EnsureFirstArtTerrainLighting(scene);
            }
            ValidateFirstArtTerrainContract(scene);

            hooks?.BeforeSceneSave?.Invoke();
            if (!EditorSceneManager.SaveScene(scene, targetScenePath))
                throw new InvalidOperationException(
                    $"Failed to save graybox scene at " +
                    $"'{targetScenePath}'.");
            if (scene.path != targetScenePath)
                throw new InvalidOperationException(
                    $"Graybox scene was saved to unexpected path " +
                    $"'{scene.path}'.");
            scene = NormalizeSceneBytes(targetScenePath);

            if (updateBuildSettings)
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

        private static Material EnsurePreviewMaterial(Shader litShader)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(PreviewMaterialPath);
            if (material == null)
            {
                material = new Material(litShader)
                {
                    name = "GrayboxPreview"
                };
                AssetDatabase.CreateAsset(material, PreviewMaterialPath);
            }
            else if (material.shader != litShader)
            {
                material.shader = litShader;
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat(
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue =
                (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ResourceIconCatalog3D EnsureResourceIconCatalog()
        {
            ResourceIconCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<ResourceIconCatalog3D>(
                    ResourceIconCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    ResourceIconCatalog3D>();
                catalog.name = "ResourceIconCatalog3D";
                AssetDatabase.CreateAsset(catalog, ResourceIconCatalogPath);
            }
            if (!catalog.TryValidate(out string error))
                throw new InvalidOperationException(error);
            return catalog;
        }

        private static bool TryOpenAndValidateFoundation(out Scene scene)
        {
            return TryOpenAndValidateFoundation(ScenePath, out scene, null);
        }

        private static bool TryOpenAndValidateFoundation(
            string scenePath,
            out Scene scene,
            AuthoringHooks hooks)
        {
            scene = default;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                return false;

            scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
            try
            {
                ValidateFoundationContract(scene);
            }
            catch (InvalidOperationException exception)
            {
                throw new AuthoringPreflightException(
                    "Graybox scene authoring preflight/foundation " +
                    $"rejected '{scenePath}': {exception.Message}",
                    exception);
            }
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
            ValidateSceneHasNoMissingScripts(scene);

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
            FirstArtTerrainRenderer3D terrainPresenter =
                ValidateFirstArtTerrainSceneStructure(
                scene,
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
                    FirstArtTerrainAssetBuilder.ProfilePath),
                    true);
            ApprovedRuinsCliffAssets geometryAssets =
                LoadApprovedRuinsCliffAssetsOrThrow();
            ValidateRuinsCliffSceneReference(
                terrainPresenter,
                geometryAssets.Profile,
                true);
            ValidateFirstArtTerrainLightStructure(scene, true);
        }

        private static BuildingContractReferences EnsureBuildingContract(
            Scene scene,
            Material material,
            Material previewMaterial,
            ResourceIconCatalog3D resourceIconCatalog)
        {
            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            Transform building = EnsureChild(root.transform, "GrayboxBuilding");
            Transform systems = RequireChild(
                root.transform,
                "GrayboxSystems");
            Transform sessionTransform =
                EnsureChild(building, "BuildingSession");
            GrayboxBuildingSession3D session =
                EnsureComponent<GrayboxBuildingSession3D>(sessionTransform);
            SetBool(session, "developmentFixtureEnabled", false);

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
            FormalWorldPresentationScaleProfile3D presentationScaleProfile =
                AssetDatabase.LoadAssetAtPath<
                    FormalWorldPresentationScaleProfile3D>(
                        WorldPresentationScaleProfilePath);
            string presentationScaleError = null;
            if (presentationScaleProfile == null ||
                !presentationScaleProfile.TryValidate(
                    out presentationScaleError))
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(presentationScaleError)
                        ? "The formal world presentation scale profile is missing."
                        : presentationScaleError);
            }
            GrayboxDirectControlCoordinator directControl =
                RequireSingle<GrayboxDirectControlCoordinator>(scene);
            GrayboxLeaderController3D leader =
                RequireSingle<GrayboxLeaderController3D>(scene);
            Camera camera = RequireSingle<Camera>(scene);
            BoxCollider innerSurface = EnsureInnerCityPlatform(
                city.transform,
                material);

            Transform ui = EnsureChild(root.transform, "GrayboxUI");
            Transform canvasTransform = EnsureChild(ui, "BuildingCanvas");
            canvasTransform.gameObject.layer = 5;
            Canvas canvas = EnsureComponent<Canvas>(canvasTransform);
            canvasTransform = canvas.transform;
            FormalUiCanvasConfiguration3D.Apply(
                canvas,
                FormalUiLayoutProfile3D.Standard.BuildingSortingOrder);
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
            Transform productionTransform =
                EnsureChild(building, "Production");
            GrayboxProductionController3D production =
                EnsureComponent<GrayboxProductionController3D>(
                    productionTransform);
            Transform defenseViewTransform = EnsureChild(
                building,
                "DefenseWorldView");
            Transform defenseEnemyRoot = EnsureChild(
                defenseViewTransform,
                "EnemyRoot");
            Transform defenseTowerRoot = EnsureChild(
                defenseViewTransform,
                "TowerRoot");
            GrayboxDefenseWorldView3D defenseWorldView =
                EnsureComponent<GrayboxDefenseWorldView3D>(
                    defenseViewTransform);
            Transform defenseControllerTransform = EnsureChild(
                systems,
                "GrayboxDefenseController");
            GrayboxDefenseController3D defenseController =
                EnsureComponent<GrayboxDefenseController3D>(
                    defenseControllerTransform);
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
                ("previewMaterial", previewMaterial),
                ("city", city),
                ("presentationScaleProfile", presentationScaleProfile));
            SetReferences(
                city,
                ("ruleTimeSourceBehaviour", session));
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
                ("placement", placement),
                ("resourceIconCatalog", resourceIconCatalog));
            SetReferences(
                world,
                ("resourceIconCatalog", resourceIconCatalog),
                ("worldPresentationScaleProfile", presentationScaleProfile));
            SetReferences(
                construction,
                ("session", session),
                ("city", city),
                ("presentation", presentation),
                ("interaction", interaction),
                ("controlledCamera", camera),
                ("menu", menu));
            SetReferences(
                production,
                ("session", session),
                ("city", city),
                ("worldView", world));
            SetReferences(
                defenseWorldView,
                ("enemyRoot", defenseEnemyRoot),
                ("towerRoot", defenseTowerRoot),
                ("sharedMaterial", material));
            SetReferences(
                evacuation,
                ("session", session),
                ("city", city),
                ("presentation", presentation),
                ("menu", menu),
                ("production", production),
                ("defense", defenseController));
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
            if (RequireSingle<GrayboxDefenseWorldView3D>(scene) !=
                defenseWorldView)
            {
                throw new InvalidOperationException(
                    "The graybox scene defense world view is not unique.");
            }
            if (RequireSingle<GrayboxDefenseController3D>(scene) !=
                defenseController)
            {
                throw new InvalidOperationException(
                    "The graybox scene defense controller is not unique.");
            }
            return new BuildingContractReferences
            {
                Systems = systems,
                Ui = ui,
                BuildingCanvas = canvas,
                EventSystem = eventSystem,
                Bootstrap = RequireSingle<GrayboxSceneBootstrap>(scene),
                Session = session,
                City = city,
                DirectControl = directControl,
                World = world,
                Leader = leader,
                BuildingPresentation = presentation,
                SharedMaterial = material,
                Production = production,
                DefenseWorldView = defenseWorldView,
                DefenseController = defenseController,
                Evacuation = evacuation,
                BuildingInput = buildingInput,
                Developer = developer
            };
        }

        private static void EnsureUsabilityContract(
            Scene scene,
            BuildingContractReferences buildingReferences,
            ResourceIconCatalog3D resourceIconCatalog)
        {
            if (buildingReferences == null)
                throw new ArgumentNullException(nameof(buildingReferences));

            Transform operationsTransform = EnsureChild(
                buildingReferences.Ui,
                "ProductionObservabilityCanvas");
            operationsTransform.gameObject.layer = 5;
            Canvas operationsCanvas = EnsureComponent<Canvas>(
                operationsTransform);
            operationsTransform = operationsCanvas.transform;
            FormalUiCanvasConfiguration3D.Apply(
                operationsCanvas,
                FormalUiLayoutProfile3D.Standard
                    .OperationsSortingOrder);
            GraphicRaycaster operationsRaycaster =
                EnsureComponent<GraphicRaycaster>(operationsTransform);
            operationsRaycaster.enabled = true;
            GrayboxOperationsView3D operationsView =
                EnsureComponent<GrayboxOperationsView3D>(
                    operationsTransform);
            GrayboxProgressionHudView3D progressionView =
                EnsureComponent<GrayboxProgressionHudView3D>(
                    operationsTransform);
            GrayboxFateSelectionView3D fateSelectionView =
                EnsureComponent<GrayboxFateSelectionView3D>(
                    operationsTransform);
            GrayboxFateOperationsView3D fateOperationsView =
                EnsureComponent<GrayboxFateOperationsView3D>(
                    operationsTransform);
            GrayboxCivilizationAdvancementView3D advancementView =
                EnsureComponent<GrayboxCivilizationAdvancementView3D>(
                    operationsTransform);
            RemoveMissingMonoBehavioursFromExactObject(
                operationsTransform.gameObject);
            GrayboxDefenseHud3D defenseHud =
                EnsureComponent<GrayboxDefenseHud3D>(
                    operationsTransform);
            Transform operationsControllerTransform = EnsureChild(
                buildingReferences.Systems,
                "GrayboxOperationsController");
            GrayboxOperationsController3D operationsController =
                EnsureComponent<GrayboxOperationsController3D>(
                    operationsControllerTransform);

            Transform systemMenuTransform = EnsureChild(
                buildingReferences.Ui,
                "SystemMenuCanvas");
            systemMenuTransform.gameObject.layer = 5;
            Canvas systemMenuCanvas = EnsureComponent<Canvas>(
                systemMenuTransform);
            systemMenuTransform = systemMenuCanvas.transform;
            FormalUiCanvasConfiguration3D.Apply(
                systemMenuCanvas,
                FormalUiLayoutProfile3D.Standard
                    .SystemMenuSortingOrder);
            GraphicRaycaster raycaster =
                EnsureComponent<GraphicRaycaster>(systemMenuTransform);
            raycaster.enabled = true;
            GrayboxSystemMenuView3D view =
                EnsureComponent<GrayboxSystemMenuView3D>(
                    systemMenuTransform);

            Transform controllerTransform = EnsureChild(
                buildingReferences.Systems,
                "GrayboxSystemMenuController");
            GrayboxSystemMenuController3D controller =
                EnsureComponent<GrayboxSystemMenuController3D>(
                    controllerTransform);
            Transform coordinatorTransform = EnsureChild(
                buildingReferences.Systems,
                "GrayboxUsabilityInputCoordinator");
            GrayboxUsabilityInputCoordinator3D coordinator =
                EnsureComponent<GrayboxUsabilityInputCoordinator3D>(
                    coordinatorTransform);
            Transform formalSaveHostTransform = EnsureChild(
                buildingReferences.Systems,
                "GrayboxFormalSaveRuntimeHost");
            GrayboxFormalSaveRuntimeHost3D formalSaveHost =
                EnsureComponent<GrayboxFormalSaveRuntimeHost3D>(
                    formalSaveHostTransform);
            Transform formalSaveEntryTransform = EnsureChild(
                buildingReferences.Systems,
                "GrayboxFormalSaveEntryController");
            GrayboxFormalSaveEntryController3D formalSaveEntry =
                EnsureComponent<GrayboxFormalSaveEntryController3D>(
                    formalSaveEntryTransform);

            SetReferences(
                view,
                ("canvas", systemMenuCanvas),
                ("eventSystem", buildingReferences.EventSystem),
                ("controller", controller),
                ("formalSaveEntry", formalSaveEntry));
            SetReferences(controller, ("view", view));
            SetReferences(
                formalSaveHost,
                ("bootstrap", buildingReferences.Bootstrap),
                ("city", buildingReferences.City),
                ("world", buildingReferences.World),
                ("session", buildingReferences.Session),
                ("buildingPresentation",
                    buildingReferences.BuildingPresentation),
                ("operations", operationsController),
                ("production", buildingReferences.Production),
                ("defense", buildingReferences.DefenseController),
                ("evacuation", buildingReferences.Evacuation),
                ("progressionView", progressionView),
                ("fateSelectionView", fateSelectionView),
                ("fateOperationsView", fateOperationsView),
                ("advancementView", advancementView),
                ("developerModifier", buildingReferences.Developer),
                ("inputCoordinator", coordinator));
            SetReferences(
                formalSaveEntry,
                ("runtimeHost", formalSaveHost),
                ("view", view),
                ("systemMenu", controller),
                ("inputCoordinator", coordinator));
            SetReferences(
                operationsView,
                ("canvas", operationsCanvas),
                ("resourceIconCatalog", resourceIconCatalog));
            SetReferences(progressionView, ("canvas", operationsCanvas));
            SetReferences(fateSelectionView, ("canvas", operationsCanvas));
            SetReferences(fateOperationsView, ("canvas", operationsCanvas));
            SetReferences(advancementView, ("canvas", operationsCanvas));
            SetReferences(
                defenseHud,
                ("canvas", operationsCanvas),
                ("eventSystem", buildingReferences.EventSystem));
            SetReferences(
                buildingReferences.DefenseController,
                ("session", buildingReferences.Session),
                ("city", buildingReferences.City),
                ("world", buildingReferences.World),
                ("buildingPresentation",
                    buildingReferences.BuildingPresentation),
                ("worldView", buildingReferences.DefenseWorldView),
                ("hud", defenseHud),
                ("production", buildingReferences.Production));
            SetReferences(
                operationsController,
                ("session", buildingReferences.Session),
                ("production", buildingReferences.Production),
                ("city", buildingReferences.City),
                ("view", operationsView),
                ("directControl", buildingReferences.DirectControl),
                ("worldView", buildingReferences.World),
                ("leader", buildingReferences.Leader));
            SetReferences(
                buildingReferences.BuildingInput,
                ("productionPresentation",
                    buildingReferences.BuildingPresentation),
                ("operations", operationsController),
                ("defense", buildingReferences.DefenseController));
            SetReferences(
                coordinator,
                ("buildingInput", buildingReferences.BuildingInput),
                ("systemMenu", controller),
                ("developer", buildingReferences.Developer),
                ("operations", operationsController),
                ("defense", buildingReferences.DefenseController),
                ("formalSaveEntry", formalSaveEntry),
                ("progressionView", progressionView),
                ("fateSelectionView", fateSelectionView),
                ("fateOperationsView", fateOperationsView),
                ("advancementView", advancementView));
            SetReferences(
                RequireSingle<GrayboxInputRouter>(scene),
                ("inputInterceptor", coordinator));

            if (RequireSingle<GrayboxDefenseHud3D>(scene) != defenseHud)
            {
                throw new InvalidOperationException(
                    "The graybox scene defense HUD is not unique.");
            }
            if (RequireSingle<GrayboxProgressionHudView3D>(scene) !=
                    progressionView)
            {
                throw new InvalidOperationException(
                    "The graybox scene progression HUD is not unique.");
            }
            if (RequireSingle<GrayboxFateSelectionView3D>(scene) !=
                    fateSelectionView)
            {
                throw new InvalidOperationException(
                    "The graybox scene fate selection view is not unique.");
            }
            if (RequireSingle<GrayboxFateOperationsView3D>(scene) !=
                    fateOperationsView)
            {
                throw new InvalidOperationException(
                    "The graybox scene fate operations view is not unique.");
            }
            if (RequireSingle<GrayboxCivilizationAdvancementView3D>(scene) !=
                    advancementView)
            {
                throw new InvalidOperationException(
                    "The civilization advancement view is not unique.");
            }
            if (RequireSingle<GrayboxFormalSaveRuntimeHost3D>(scene) !=
                    formalSaveHost ||
                RequireSingle<GrayboxFormalSaveEntryController3D>(scene) !=
                    formalSaveEntry)
            {
                throw new InvalidOperationException(
                    "The formal 3D save runtime and entry must be unique.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void EnsureFirstArtTerrainContract(
            Scene scene,
            FirstArtTerrainProfile3D profile,
            FirstArtRuinsCliffProfile3D geometryProfile)
        {
            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            Transform world = RequireChild(root.transform, "GrayboxWorld");
            Transform owner =
                EnsureChild(world, "FirstArtTerrainPresentation");
            FirstArtTerrainRenderer3D presenter =
                EnsureComponent<FirstArtTerrainRenderer3D>(owner);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"The approved terrain profile is missing at " +
                    $"'{FirstArtTerrainAssetBuilder.ProfilePath}'.");
            }
            if (!profile.TryValidate(out string error))
                throw new InvalidOperationException(error);
            if (geometryProfile == null)
            {
                throw new InvalidOperationException(
                    "The approved ruins/cliff profile is missing.");
            }
            if (!geometryProfile.TryValidate(out string geometryError))
                throw new InvalidOperationException(geometryError);

            presenter.Configure(profile, geometryProfile);
            SetReferences(
                RequireSingle<GrayboxSceneBootstrap>(scene),
                ("terrainPresentationBehaviour", presenter));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void EnsureFirstArtTerrainLighting(Scene scene)
        {
            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            Transform owner = EnsureChild(root.transform, TerrainLightName);
            Light light = EnsureComponent<Light>(owner);

            owner.gameObject.SetActive(true);
            owner.localRotation = Quaternion.Euler(TerrainLightEuler);
            light.enabled = true;
            light.type = LightType.Directional;
            light.color = TerrainLightColor;
            light.intensity = TerrainLightIntensity;
            light.shadows = LightShadows.Soft;
            light.cullingMask = ~0;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ValidateFirstArtTerrainContract(Scene scene)
        {
            ValidateFirstArtTerrainContractWithAssets(
                scene,
                LoadApprovedTerrainAssetsOrThrow(),
                LoadApprovedRuinsCliffAssetsOrThrow());
        }

        private static void ValidateFirstArtTerrainContractWithAssets(
            Scene scene,
            ApprovedTerrainAssets assets,
            ApprovedRuinsCliffAssets geometryAssets)
        {
            FirstArtTerrainRenderer3D presenter =
                ValidateFirstArtTerrainSceneStructure(
                    scene,
                    assets.Profile,
                    false);
            RequireReference(presenter, "profile", assets.Profile);
            RequireReference(
                presenter,
                "geometryProfile",
                geometryAssets.Profile);
            ValidateFirstArtTerrainLightStructure(scene, false);
        }

        private static Light ValidateFirstArtTerrainLightStructure(
            Scene scene,
            bool allowRepairableAbsence)
        {
            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            Transform owner = null;
            var namedOwnerCount = 0;
            foreach (GameObject gameObject in FindSceneGameObjects(scene))
            {
                if (!string.Equals(
                        gameObject.name,
                        TerrainLightName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                namedOwnerCount++;
                owner = gameObject.transform;
            }

            List<Light> lights = FindSceneComponents<Light>(scene);
            if (namedOwnerCount > 1 || lights.Count > 1)
            {
                throw new InvalidOperationException(
                    "The first-art terrain scene requires exactly one " +
                    $"{TerrainLightName} Light.");
            }
            if (owner == null)
            {
                if (lights.Count != 0 || !allowRepairableAbsence)
                {
                    throw new InvalidOperationException(
                        $"The first-art terrain scene is missing its " +
                        $"{TerrainLightName} Light.");
                }
                return null;
            }
            if (owner.parent != root.transform)
            {
                throw new InvalidOperationException(
                    $"{TerrainLightName} must be a direct child of " +
                    "GrayboxPrototype3D.");
            }
            if (owner.childCount != 0)
            {
                throw new InvalidOperationException(
                    $"{TerrainLightName} must not contain children.");
            }

            Component[] components = owner.GetComponents<Component>();
            Light terrainLight = null;
            var transformCount = 0;
            var lightCount = 0;
            foreach (Component component in components)
            {
                if (component == null)
                {
                    throw new InvalidOperationException(
                        $"{TerrainLightName} contains a missing script.");
                }
                if (component is Transform)
                {
                    transformCount++;
                    continue;
                }
                if (component is Light candidate)
                {
                    lightCount++;
                    terrainLight = candidate;
                    continue;
                }
                throw new InvalidOperationException(
                    $"{TerrainLightName} may contain only Transform and " +
                    "Light components.");
            }
            if (transformCount != 1 || lightCount > 1 ||
                (!allowRepairableAbsence && lightCount != 1) ||
                lights.Count != lightCount)
            {
                throw new InvalidOperationException(
                    $"{TerrainLightName} component ownership is invalid.");
            }
            if (terrainLight == null)
                return null;

            if (!owner.gameObject.activeSelf || !terrainLight.enabled ||
                terrainLight.type != LightType.Directional ||
                !Approximately(terrainLight.color, TerrainLightColor) ||
                !Mathf.Approximately(
                    terrainLight.intensity,
                    TerrainLightIntensity) ||
                terrainLight.shadows != LightShadows.Soft ||
                terrainLight.cullingMask != ~0 ||
                !ApproximatelyEuler(
                    owner.localEulerAngles,
                    TerrainLightEuler,
                    TerrainLightAngleTolerance))
            {
                if (!allowRepairableAbsence)
                {
                    throw new InvalidOperationException(
                        $"{TerrainLightName} settings do not match the " +
                        "approved first-art lighting contract.");
                }
            }

            return terrainLight;
        }

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) <= .0001f &&
                   Mathf.Abs(left.g - right.g) <= .0001f &&
                   Mathf.Abs(left.b - right.b) <= .0001f &&
                   Mathf.Abs(left.a - right.a) <= .0001f;
        }

        private static bool ApproximatelyEuler(
            Vector3 left,
            Vector3 right,
            float tolerance)
        {
            return Mathf.Abs(Mathf.DeltaAngle(left.x, right.x)) <= tolerance &&
                   Mathf.Abs(Mathf.DeltaAngle(left.y, right.y)) <= tolerance &&
                   Mathf.Abs(Mathf.DeltaAngle(left.z, right.z)) <= tolerance;
        }

        private static FirstArtTerrainRenderer3D
            ValidateFirstArtTerrainSceneStructure(
                Scene scene,
                FirstArtTerrainProfile3D approvedProfile,
                bool allowRepairableAbsence)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "The first-art terrain scene must be loaded.");
            }
            ValidateSceneHasNoMissingScripts(scene);

            GameObject root = RequireRoot(scene, "GrayboxPrototype3D");
            Transform world = RequireChild(root.transform, "GrayboxWorld");
            Transform owner = null;
            var namedOwners = 0;
            foreach (GameObject gameObject in FindSceneGameObjects(scene))
            {
                if (IsRuntimePresentationName(gameObject.name))
                {
                    throw new InvalidOperationException(
                        gameObject.name + " must not be serialized in the " +
                        "graybox scene.");
                }
                if (!string.Equals(
                        gameObject.name,
                        "FirstArtTerrainPresentation",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                namedOwners++;
                owner = gameObject.transform;
            }
            if (namedOwners > 1 ||
                (!allowRepairableAbsence && namedOwners != 1))
            {
                throw new InvalidOperationException(
                    "The scene requires exactly one GameObject named " +
                    "FirstArtTerrainPresentation; found " +
                    $"{namedOwners}.");
            }

            List<FirstArtTerrainRenderer3D> presenters =
                FindSceneComponents<FirstArtTerrainRenderer3D>(scene);
            if (presenters.Count > 1)
            {
                throw new InvalidOperationException(
                    "The scene contains multiple " +
                    "FirstArtTerrainRenderer3D components.");
            }
            GrayboxSceneBootstrap bootstrap =
                RequireSingle<GrayboxSceneBootstrap>(scene);
            UnityEngine.Object bootstrapReference = GetReference(
                bootstrap,
                "terrainPresentationBehaviour");

            if (owner == null)
            {
                if (presenters.Count != 0 || bootstrapReference != null)
                {
                    throw new InvalidOperationException(
                        "A missing FirstArtTerrainPresentation owner may " +
                        "not leave a presenter or Bootstrap reference " +
                        "elsewhere in the scene.");
                }
                return null;
            }
            if (owner.parent != world)
            {
                throw new InvalidOperationException(
                    "FirstArtTerrainPresentation must be a direct child " +
                    "of GrayboxWorld.");
            }

            if (owner.childCount != 0)
            {
                throw new InvalidOperationException(
                    "FirstArtTerrainPresentation must not contain " +
                    "serialized children.");
            }

            Component[] components =
                owner.GetComponents<Component>();
            var transformCount = 0;
            var presenterCount = 0;
            FirstArtTerrainRenderer3D presenter = null;
            foreach (Component component in components)
            {
                if (component == null)
                    throw new InvalidOperationException(
                        "FirstArtTerrainPresentation contains a missing " +
                        "script component.");
                if (component is Transform)
                {
                    transformCount++;
                    continue;
                }
                if (component is FirstArtTerrainRenderer3D candidate)
                {
                    presenterCount++;
                    presenter = candidate;
                    continue;
                }
                throw new InvalidOperationException(
                    "FirstArtTerrainPresentation may contain only its " +
                    "Transform and FirstArtTerrainRenderer3D.");
            }
            if (transformCount != 1 ||
                presenterCount > 1 ||
                (!allowRepairableAbsence && presenterCount != 1))
            {
                throw new InvalidOperationException(
                    "FirstArtTerrainPresentation component ownership is " +
                    "invalid.");
            }

            if (presenters.Count != presenterCount ||
                (presenter != null && presenter.transform != owner))
            {
                throw new InvalidOperationException(
                    "FirstArtTerrainRenderer3D must be attached only to " +
                    "FirstArtTerrainPresentation.");
            }
            if (presenter == null)
            {
                if (bootstrapReference != null)
                {
                    throw new InvalidOperationException(
                        "Bootstrap may not reference terrain presentation " +
                        "behavior when the presenter is absent.");
                }
                return null;
            }

            UnityEngine.Object profileReference =
                GetReference(presenter, "profile");
            if (allowRepairableAbsence)
            {
                if ((profileReference != null &&
                     profileReference != approvedProfile) ||
                    (bootstrapReference != null &&
                     bootstrapReference != presenter))
                {
                    throw new InvalidOperationException(
                        "Existing terrain Profile and Bootstrap references " +
                        "must be absent or already point to the approved " +
                        "contract.");
                }
            }
            else
            {
                if (approvedProfile == null ||
                    profileReference != approvedProfile ||
                    bootstrapReference != presenter)
                {
                    throw new InvalidOperationException(
                        "The final terrain Profile or Bootstrap reference " +
                        "is missing or invalid.");
                }
            }

            return presenter;
        }

        private static bool IsRuntimePresentationName(string name)
        {
            return string.Equals(name, "RuntimeSurface", StringComparison.Ordinal) ||
                   string.Equals(name, "RuntimeGeometry", StringComparison.Ordinal) ||
                   string.Equals(name, "RuinsGeometry", StringComparison.Ordinal) ||
                   string.Equals(name, "CliffGeometry", StringComparison.Ordinal);
        }

        private static void ValidateRuinsCliffSceneReference(
            FirstArtTerrainRenderer3D presenter,
            FirstArtRuinsCliffProfile3D approvedProfile,
            bool allowRepairableAbsence)
        {
            if (presenter == null)
                return;
            SerializedReferenceState state = GetReferenceState(
                presenter,
                "geometryProfile",
                out UnityEngine.Object reference);
            if (state == SerializedReferenceState.Missing)
            {
                throw new InvalidOperationException(
                    "Existing ruins/cliff geometry Profile reference is " +
                    "missing or unresolved and cannot be repaired implicitly.");
            }
            if (state == SerializedReferenceState.Live &&
                reference == approvedProfile)
                return;
            if (allowRepairableAbsence &&
                state == SerializedReferenceState.Null)
                return;
            throw new InvalidOperationException(
                "Existing ruins/cliff geometry Profile reference must be " +
                (allowRepairableAbsence
                    ? "absent or already approved."
                    : "the approved Profile."));
        }

        private static SerializedReferenceState GetReferenceState(
            UnityEngine.Object owner,
            string propertyName,
            out UnityEngine.Object reference)
        {
            var data = new SerializedObject(owner);
            data.UpdateIfRequiredOrScript();
            SerializedProperty property = data.FindProperty(propertyName);
            if (property == null ||
                property.propertyType != SerializedPropertyType.ObjectReference)
            {
                throw new InvalidOperationException(
                    $"Serialized object reference {owner.GetType().Name}." +
                    $"{propertyName} is unavailable.");
            }

            reference = property.objectReferenceValue;
            if (reference != null)
                return SerializedReferenceState.Live;
            if (!ReferenceEquals(reference, null) ||
                property.objectReferenceInstanceIDValue != 0)
                return SerializedReferenceState.Missing;
            return SerializedReferenceState.Null;
        }

        private static ApprovedTerrainAssets EnsureApprovedTerrainAssets(
            AuthoringHooks hooks)
        {
            if (TryLoadApprovedTerrainAssets(
                    out ApprovedTerrainAssets assets,
                    out _))
            {
                return assets;
            }

            hooks?.BeforeRuntimeAssetBuilder?.Invoke();
            FirstArtTerrainAssetBuilder.BuildRuntimeAssets();
            if (!TryLoadApprovedTerrainAssets(out assets, out string error))
            {
                throw new InvalidOperationException(
                    "Terrain runtime asset repair did not restore the " +
                    $"approved contract: {error}");
            }
            return assets;
        }

        private static ApprovedTerrainAssets
            LoadApprovedTerrainAssetsOrThrow()
        {
            if (!TryLoadApprovedTerrainAssets(
                    out ApprovedTerrainAssets assets,
                    out string error))
            {
                throw new InvalidOperationException(
                    $"Approved terrain assets are invalid: {error}");
            }
            return assets;
        }

        private static ApprovedRuinsCliffAssets
            LoadApprovedRuinsCliffAssetsOrThrow()
        {
            if (!TryLoadApprovedRuinsCliffAssets(
                    out ApprovedRuinsCliffAssets assets,
                    out string error))
            {
                throw new InvalidOperationException(
                    "Approved ruins/cliff assets are invalid: " + error);
            }
            return assets;
        }

        private static bool TryLoadApprovedRuinsCliffAssets(
            out ApprovedRuinsCliffAssets assets,
            out string error)
        {
            assets = new ApprovedRuinsCliffAssets
            {
                Fbx = new GameObject[FirstArtRuinsCliffCatalog3D.EntryCount],
                Prefabs = new GameObject[FirstArtRuinsCliffCatalog3D.EntryCount],
                Materials = new Material[FirstArtRuinsCliffCatalog3D.MaterialRoleCount],
            };
            if (RuinsCliffFbxGuids.Length != FirstArtRuinsCliffCatalog3D.EntryCount ||
                RuinsCliffPrefabGuids.Length != FirstArtRuinsCliffCatalog3D.EntryCount ||
                RuinsCliffMaterialGuids.Length !=
                    FirstArtRuinsCliffCatalog3D.MaterialRoleCount)
            {
                error = "Frozen ruins/cliff GUID table counts are invalid.";
                return false;
            }
            if (!TryLoadExactAsset(
                    FirstArtRuinsCliffAssetBuilder.ProfilePath,
                    RuinsCliffProfileGuid,
                    out assets.Profile,
                    out error) ||
                !TryLoadExactAsset(
                    FirstArtRuinsCliffAssetBuilder.ShaderPath,
                    RuinsCliffShaderGuid,
                    out assets.Shader,
                    out error))
                return false;

            for (int index = 0;
                 index < FirstArtRuinsCliffCatalog3D.EntryCount;
                 index++)
            {
                FirstArtRuinsCliffCatalogEntry3D entry =
                    FirstArtRuinsCliffCatalog3D.Entries[index];
                if (!TryLoadExactAsset(
                        entry.FbxPath,
                        RuinsCliffFbxGuids[index],
                        out assets.Fbx[index],
                        out error) ||
                    !TryLoadExactAsset(
                        entry.PrefabPath,
                        RuinsCliffPrefabGuids[index],
                        out assets.Prefabs[index],
                        out error))
                    return false;
            }

            for (int index = 0;
                 index < FirstArtRuinsCliffCatalog3D.MaterialRoleCount;
                 index++)
            {
                FirstArtRuinsCliffMaterialRole3D role =
                    FirstArtRuinsCliffCatalog3D.MaterialRoles[index];
                string path = FirstArtRuinsCliffCatalog3D.GeometryMaterialDirectory +
                    role.Name + ".mat";
                if (!TryLoadExactAsset(
                        path,
                        RuinsCliffMaterialGuids[index],
                        out assets.Materials[index],
                        out error))
                    return false;
            }

            if (!assets.Profile.TryValidate(out error))
                return false;
            if (assets.Profile.GeometryShader != assets.Shader ||
                !string.Equals(
                    assets.Shader.name,
                    FirstArtRuinsCliffCatalog3D.RequiredShaderName,
                    StringComparison.Ordinal))
            {
                error = "Ruins/cliff Profile Shader is not the approved asset.";
                return false;
            }
            for (int index = 0; index < assets.Prefabs.Length; index++)
            {
                FirstArtRuinsCliffPrefabBinding3D binding =
                    assets.Profile.PrefabBindings[index];
                FirstArtRuinsCliffCatalogEntry3D entry =
                    FirstArtRuinsCliffCatalog3D.Entries[index];
                if (!string.Equals(
                        binding.StableId,
                        entry.StableId,
                        StringComparison.Ordinal) ||
                    binding.Prefab != assets.Prefabs[index])
                {
                    error = "Ruins/cliff Profile prefab binding does not match " +
                        entry.StableId + ".";
                    return false;
                }
            }
            for (int index = 0; index < assets.Materials.Length; index++)
            {
                FirstArtRuinsCliffMaterialBinding3D binding =
                    assets.Profile.MaterialBindings[index];
                FirstArtRuinsCliffMaterialRole3D role =
                    FirstArtRuinsCliffCatalog3D.MaterialRoles[index];
                if (!string.Equals(binding.Role, role.Name, StringComparison.Ordinal) ||
                    binding.Material != assets.Materials[index] ||
                    assets.Materials[index].shader != assets.Shader)
                {
                    error = "Ruins/cliff Profile material binding does not match " +
                        role.Name + ".";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryLoadApprovedTerrainAssets(
            out ApprovedTerrainAssets assets,
            out string error)
        {
            assets = new ApprovedTerrainAssets();
            if (!TryLoadExactAsset(
                    FirstArtTerrainAssetBuilder.ProfilePath,
                    TerrainProfileGuid,
                    out assets.Profile,
                    out error) ||
                !TryLoadExactAsset(
                    FirstArtTerrainAssetBuilder.MaterialPath,
                    TerrainMaterialGuid,
                    out assets.Material,
                    out error) ||
                !TryLoadExactAsset(
                    FirstArtTerrainAssetBuilder.ShaderPath,
                    TerrainShaderGuid,
                    out assets.Shader,
                    out error) ||
                !TryLoadExactAsset(
                    FirstArtTerrainAssetBuilder.BaseColorArrayPath,
                    TerrainBaseColorGuid,
                    out assets.BaseColor,
                    out error) ||
                !TryLoadExactAsset(
                    FirstArtTerrainAssetBuilder.NormalArrayPath,
                    TerrainNormalGuid,
                    out assets.Normal,
                    out error) ||
                !TryLoadExactAsset(
                    FirstArtTerrainAssetBuilder.MaskArrayPath,
                    TerrainMaskGuid,
                    out assets.Mask,
                    out error) ||
                !TryLoadExactAsset(
                    FirstArtTerrainAssetBuilder.HeightArrayPath,
                    TerrainHeightGuid,
                    out assets.Height,
                    out error))
            {
                return false;
            }

            if (!string.Equals(
                    assets.Profile.name,
                    "FirstArtTerrainProfile3D",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    assets.Material.name,
                    "MAT_Terrain_FirstPass",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    assets.Shader.name,
                    FirstArtTerrainProfile3D.RequiredShaderName,
                    StringComparison.Ordinal))
            {
                error = "Terrain Profile, Material, or Shader identity " +
                        "name is invalid.";
                return false;
            }
            if (!assets.Profile.TryValidate(out error))
                return false;
            if (assets.Profile.Material != assets.Material ||
                assets.Profile.BaseColorArray != assets.BaseColor ||
                assets.Profile.NormalArray != assets.Normal ||
                assets.Profile.MaskArray != assets.Mask ||
                assets.Profile.HeightArray != assets.Height)
            {
                error = "Terrain Profile references do not match the " +
                        "approved asset identities.";
                return false;
            }
            if (assets.Material.shader != assets.Shader ||
                assets.Material.GetTexture("_BaseColorArray") !=
                    assets.BaseColor ||
                assets.Material.GetTexture("_NormalArray") !=
                    assets.Normal ||
                assets.Material.GetTexture("_MaskArray") != assets.Mask ||
                assets.Material.GetTexture("_HeightArray") != assets.Height)
            {
                error = "Terrain Material references do not match the " +
                        "approved Shader and texture-array identities.";
                return false;
            }

            if (!ValidatePrimaryArray(
                    assets.BaseColor,
                    "TA_Terrain_BaseColor",
                    FirstBaseColorSourcePath,
                    out error) ||
                !ValidatePrimaryArray(
                    assets.Normal,
                    "TA_Terrain_Normal",
                    FirstNormalSourcePath,
                    out error) ||
                !ValidateMaskArray(assets.Mask, out error) ||
                !ValidateHeightArray(assets.Height, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryLoadExactAsset<T>(
            string assetPath,
            string expectedGuid,
            out T asset,
            out string error)
            where T : UnityEngine.Object
        {
            asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            string actualGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (asset == null ||
                AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(T) ||
                !string.Equals(
                    AssetDatabase.GetAssetPath(asset),
                    assetPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actualGuid,
                    expectedGuid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    AssetDatabase.GUIDToAssetPath(expectedGuid),
                    assetPath,
                    StringComparison.Ordinal))
            {
                error = $"Required {typeof(T).Name} at '{assetPath}' " +
                        $"does not have approved GUID {expectedGuid}.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidatePrimaryArray(
            Texture2DArray array,
            string expectedName,
            string formatSourcePath,
            out string error)
        {
            Texture2D source =
                AssetDatabase.LoadAssetAtPath<Texture2D>(formatSourcePath);
            if (!ValidateCommonArray(
                    array,
                    expectedName,
                    FirstArtTerrainProfile3D.PrimaryArraySize,
                    out error) ||
                source == null ||
                array.format != source.format ||
                array.graphicsFormat != source.graphicsFormat ||
                array.mipmapCount != source.mipmapCount)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = $"Terrain array '{expectedName}' does not " +
                            $"match '{formatSourcePath}'.";
                }
                return false;
            }

            return true;
        }

        private static bool ValidateMaskArray(
            Texture2DArray array,
            out string error)
        {
            if (!ValidateCommonArray(
                    array,
                    "TA_Terrain_Mask",
                    FirstArtTerrainProfile3D.PrimaryArraySize,
                    out error) ||
                array.format != TextureFormat.BC7 ||
                array.mipmapCount != MipCount(
                    FirstArtTerrainProfile3D.PrimaryArraySize))
            {
                if (string.IsNullOrEmpty(error))
                    error = "Terrain mask array format is invalid.";
                return false;
            }

            return true;
        }

        private static bool ValidateHeightArray(
            Texture2DArray array,
            out string error)
        {
            if (!ValidateCommonArray(
                    array,
                    "TA_Terrain_Height",
                    FirstArtTerrainProfile3D.HeightArraySize,
                    out error) ||
                array.format != TextureFormat.R8 ||
                array.graphicsFormat != GraphicsFormat.R8_UNorm ||
                array.mipmapCount != MipCount(
                    FirstArtTerrainProfile3D.HeightArraySize))
            {
                if (string.IsNullOrEmpty(error))
                    error = "Terrain height array format is invalid.";
                return false;
            }

            return true;
        }

        private static bool ValidateCommonArray(
            Texture2DArray array,
            string expectedName,
            int expectedSize,
            out string error)
        {
            if (array == null ||
                !string.Equals(
                    array.name,
                    expectedName,
                    StringComparison.Ordinal) ||
                array.width != expectedSize ||
                array.height != expectedSize ||
                array.depth != FirstArtTerrainCatalog3D.LayerCount ||
                array.wrapMode != TextureWrapMode.Repeat ||
                array.filterMode != FilterMode.Bilinear ||
                array.anisoLevel != 4 ||
                array.mipmapCount <= 1 ||
                array.isReadable)
            {
                error = $"Terrain array '{expectedName}' has invalid " +
                        "dimensions, sampling, or readability settings.";
                return false;
            }

            error = null;
            return true;
        }

        private static int MipCount(int size)
        {
            var count = 1;
            while (size > 1)
            {
                size >>= 1;
                count++;
            }
            return count;
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
                GrayboxWorldLayout3D.StartCellX,
                GrayboxWorldLayout3D.StartCellY,
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

        private static void EnsurePlayableInitialDeployment(Scene scene)
        {
            GrayboxMobileCityController3D city =
                RequireSingle<GrayboxMobileCityController3D>(scene);
            var coordinates = new PlanarCoordinateMapper3D(
                GrayboxSceneBootstrap.WorldWidth,
                GrayboxSceneBootstrap.WorldHeight);
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            if (coordinates.TryWorldToCell(
                    city.transform.position,
                    out int currentX,
                    out int currentY) &&
                CityDeploymentRules.Validate(
                    world,
                    currentX,
                    currentY) == CityDeploymentFailure.None)
            {
                return;
            }

            int approvedX = GrayboxWorldLayout3D.StartCellX;
            int approvedY = GrayboxWorldLayout3D.StartCellY;
            if (CityDeploymentRules.Validate(
                    world,
                    approvedX,
                    approvedY) != CityDeploymentFailure.None ||
                !coordinates.TryCellToWorld(
                    approvedX,
                    approvedY,
                    city.transform.position.y,
                    out Vector3 approvedPosition))
            {
                throw new InvalidOperationException(
                    "The approved initial deployment cell is invalid for " +
                    "the serialized graybox seed.");
            }

            Vector3 delta = approvedPosition - city.transform.position;
            city.transform.position = approvedPosition;
            GrayboxLeaderController3D leader =
                RequireSingle<GrayboxLeaderController3D>(scene);
            leader.transform.position += delta;
            Transform cameraRig = RequireChild(
                RequireRoot(scene, "GrayboxPrototype3D").transform,
                "CameraRig");
            cameraRig.position += new Vector3(delta.x, 0f, delta.z);
            EditorSceneManager.MarkSceneDirty(scene);
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

        private static Scene NormalizeSceneBytes(string scenePath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException(
                    "Unity project root could not be resolved.");
            string absolutePath = Path.Combine(projectRoot, scenePath);
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
                scenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            Scene scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
            ValidateFoundationContract(scene);
            ValidateFirstArtTerrainContract(scene);
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

        private static List<GameObject> FindSceneGameObjects(Scene scene)
        {
            var result = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                result.Add(transform.gameObject);
            }
            return result;
        }

        private static void ValidateSceneHasNoMissingScripts(Scene scene)
        {
            foreach (GameObject gameObject in FindSceneGameObjects(scene))
            {
                int missingScriptCount =
                    GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingScriptCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Scene object '{gameObject.name}' contains " +
                        $"{missingScriptCount} missing script(s).");
                }
            }
        }

        private static void RemoveMissingMonoBehavioursFromExactObject(
            GameObject owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    owner) == 0)
            {
                return;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(owner);
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    owner) != 0)
            {
                throw new InvalidOperationException(
                    "Failed to remove missing MonoBehaviour components " +
                    "from " + owner.name + ".");
            }
            EditorUtility.SetDirty(owner);
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

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset '{path}' is missing or has the wrong " +
                    $"type {typeof(T).Name}.");
            }
            return asset;
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
            if (GetReference(owner, propertyName) != expected)
            {
                throw new InvalidOperationException(
                    $"Foundation reference {owner.GetType().Name}." +
                    $"{propertyName} is missing or invalid.");
            }
        }

        private static UnityEngine.Object GetReference(
            UnityEngine.Object owner,
            string propertyName)
        {
            var data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized property {owner.GetType().Name}." +
                    $"{propertyName} is unavailable.");
            }
            return property.objectReferenceValue;
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
