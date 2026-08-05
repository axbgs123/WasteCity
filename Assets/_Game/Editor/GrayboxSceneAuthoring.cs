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
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Scene scene = BuildScene(pipeline, material);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    $"Failed to save graybox scene at '{ScenePath}'.");
            if (scene.path != ScenePath)
                throw new InvalidOperationException(
                    $"Graybox scene was saved to unexpected path " +
                    $"'{scene.path}'.");
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

            rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue =
                renderer;
            defaultRenderer.intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
            else
            {
                material.shader = litShader;
            }

            return material;
        }

        private static Scene BuildScene(
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

            return scene;
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
