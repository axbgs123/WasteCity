using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxSceneBootstrapTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private RenderPipelineAsset originalGraphics;
        private RenderPipelineAsset originalQuality;

        [SetUp]
        public void SetUp()
        {
            originalGraphics = GraphicsSettings.defaultRenderPipeline;
            originalQuality = QualitySettings.renderPipeline;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GrayboxUrpScope scope in
                     UnityEngine.Object.FindObjectsOfType<GrayboxUrpScope>(
                         true))
                scope.Exit();

            GraphicsSettings.defaultRenderPipeline = originalGraphics;
            QualitySettings.renderPipeline = originalQuality;

            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void Initialize_WithoutAppliedScope_DoesNotGenerateWorld()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            GrayboxSceneBootstrap bootstrap = NewBootstrap(scope, view);

            Assert.That(bootstrap.Initialize(), Is.False);

            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.World, Is.Null);
            Assert.That(view.Model, Is.Null);
        }

        [Test]
        public void Initialize_GeneratesFrozenWorldAndView()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            GrayboxSceneBootstrap bootstrap = NewBootstrap(scope, view);
            Assert.That(scope.Enter(), Is.True);

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(bootstrap.World, Is.SameAs(view.Model));
            Assert.That(bootstrap.World.Width, Is.EqualTo(32));
            Assert.That(bootstrap.World.Height, Is.EqualTo(24));
            AssertWorldEquals(
                new WorldMapModel(32, 24, new WorldSeed(8128)),
                bootstrap.World);
        }

        [Test]
        public void Initialize_WhenCalledTwice_PreservesGeneratedObjects()
        {
            GrayboxUrpScope scope = NewScope(NewPipeline());
            GrayboxWorldView3D view = NewWorldView();
            GrayboxSceneBootstrap bootstrap = NewBootstrap(scope, view);
            Assert.That(scope.Enter(), Is.True);
            Assert.That(bootstrap.Initialize(), Is.True);
            WorldMapModel firstWorld = bootstrap.World;
            GrayboxVisualSlot[] firstSlots =
                view.GetComponentsInChildren<GrayboxVisualSlot>(true);

            Assert.That(bootstrap.Initialize(), Is.True);

            Assert.That(bootstrap.World, Is.SameAs(firstWorld));
            Assert.That(
                view.GetComponentsInChildren<GrayboxVisualSlot>(true),
                Is.EqualTo(firstSlots));
        }

        [Test]
        public void Initialize_WithMissingAssetOrView_DoesNotGenerateWorld()
        {
            GrayboxUrpScope missingAssetScope = NewScope(null);
            GrayboxWorldView3D unusedView = NewWorldView();
            GrayboxSceneBootstrap missingAsset =
                NewBootstrap(missingAssetScope, unusedView);
            Assert.That(missingAssetScope.Enter(), Is.False);

            Assert.That(missingAsset.Initialize(), Is.False);
            Assert.That(missingAsset.World, Is.Null);
            Assert.That(unusedView.Model, Is.Null);

            GrayboxUrpScope validScope = NewScope(NewPipeline());
            Assert.That(validScope.Enter(), Is.True);
            GrayboxSceneBootstrap missingView =
                NewBootstrap(validScope, null);

            Assert.That(missingView.Initialize(), Is.False);
            Assert.That(missingView.World, Is.Null);
        }

        private GrayboxUrpScope NewScope(
            UniversalRenderPipelineAsset pipeline)
        {
            var owner = Track(new GameObject("GrayboxUrpScope"));
            owner.SetActive(false);
            GrayboxUrpScope scope = owner.AddComponent<GrayboxUrpScope>();
            scope.Configure(pipeline);
            return scope;
        }

        private UniversalRenderPipelineAsset NewPipeline()
        {
            return Track(
                ScriptableObject.CreateInstance<
                    UniversalRenderPipelineAsset>());
        }

        private GrayboxWorldView3D NewWorldView()
        {
            var root = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = Track(new Material(shader));
            GrayboxWorldView3D view =
                root.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            return view;
        }

        private GrayboxSceneBootstrap NewBootstrap(
            GrayboxUrpScope scope,
            GrayboxWorldView3D view)
        {
            var owner = Track(new GameObject("GrayboxSceneBootstrap"));
            owner.SetActive(false);
            GrayboxSceneBootstrap bootstrap =
                owner.AddComponent<GrayboxSceneBootstrap>();
            bootstrap.Configure(scope, view);
            return bootstrap;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void AssertWorldEquals(
            WorldMapModel expected,
            WorldMapModel actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            for (int x = 0; x < expected.Width; x++)
            for (int y = 0; y < expected.Height; y++)
            {
                WorldCell expectedCell = expected.Get(x, y);
                WorldCell actualCell = actual.Get(x, y);
                Assert.That(actualCell.Terrain, Is.EqualTo(expectedCell.Terrain));
                Assert.That(
                    actualCell.ResourceId,
                    Is.EqualTo(expectedCell.ResourceId));
                Assert.That(
                    actualCell.ResourceAmount,
                    Is.EqualTo(expectedCell.ResourceAmount));
                Assert.That(
                    actualCell.Traversal,
                    Is.EqualTo(expectedCell.Traversal));
            }
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }
    }
}
