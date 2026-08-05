using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildAndPerformanceTests
    {
        private const string FormalBuildToolsTypeName =
            "WasteCity.Editor.FormalBuildTools";
        private const string PerformanceProbeTypeName =
            "WasteCity.Editor.GrayboxPerformanceProbe";

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void BuildTools_ExposeSeparateFrozen2DAndGraybox3DTargets()
        {
            Type buildTools = FindLoadedType(FormalBuildToolsTypeName);
            Assert.That(buildTools, Is.Not.Null);
            Assert.That(
                buildTools.GetMethod(
                    "BuildWindows",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                buildTools.GetMethod(
                    "BuildWindowsGraybox3D",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FormalBuildTools.cs"));
            string frozen2D = ExtractMethodBlock(source, "BuildWindows");
            string graybox3D =
                ExtractMethodBlock(source, "BuildWindowsGraybox3D");

            StringAssert.Contains(
                "Assets/_Game/Scenes/FormalPrototype.unity",
                frozen2D);
            StringAssert.Contains(
                "Builds/Windows/WasteCity.exe",
                frozen2D);
            StringAssert.DoesNotContain("GrayboxPrototype3D", frozen2D);
            StringAssert.Contains(
                "BuildTarget.StandaloneWindows64",
                frozen2D);

            StringAssert.Contains(
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                graybox3D);
            StringAssert.Contains(
                "Builds/Windows3D/WasteCityGraybox.exe",
                graybox3D);
            StringAssert.DoesNotContain("FormalPrototype", graybox3D);
            StringAssert.Contains(
                "BuildTarget.StandaloneWindows64",
                graybox3D);
        }

        [Test]
        public void PerformanceProbe_ExposesFiveRunWorldGenerationEntryPoint()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);
            MethodInfo method = probe?.GetMethod(
                "MeasureWorldGeneration",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);
        }

        [Test]
        public void GeneratedWorld_StaysWithinStructuralBudgets()
        {
            int rendererCountBefore =
                UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Length;
            GrayboxWorldView3D view = CreateWorldView();

            view.Generate(
                new WorldMapModel(
                    GrayboxSceneBootstrap.WorldWidth,
                    GrayboxSceneBootstrap.WorldHeight,
                    new WorldSeed(
                        GrayboxSceneBootstrap.WorldSeedValue)));

            int generatedRendererCount =
                UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Length -
                rendererCountBefore;
            TestContext.WriteLine(
                "WorldRendererCount=" + view.WorldRendererCount);
            TestContext.WriteLine(
                "PersistentGeneratedObjectCount=" +
                view.PersistentGeneratedObjectCount);
            TestContext.WriteLine(
                "GeneratedMeshRendererCount=" +
                generatedRendererCount);
            Assert.That(view.WorldRendererCount, Is.LessThanOrEqualTo(16));
            Assert.That(
                view.PersistentGeneratedObjectCount,
                Is.LessThanOrEqualTo(16));
            Assert.That(generatedRendererCount, Is.LessThan(32 * 24));
        }

        [Test]
        public void AdapterTicks_AllocateNoManagedBytesAcross300Calls()
        {
            AdapterFixture fixture = CreateAdapterFixture();
            fixture.TickAll();
            fixture.TickAll();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 300; frame++)
                fixture.TickAll();
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "Task9AdapterAllocationDifference=" + difference);
            Assert.That(difference, Is.Zero);
        }

        private AdapterFixture CreateAdapterFixture()
        {
            GrayboxWorldView3D world = CreateWorldView();
            world.Generate(
                new WorldMapModel(
                    GrayboxSceneBootstrap.WorldWidth,
                    GrayboxSceneBootstrap.WorldHeight,
                    new WorldSeed(
                        GrayboxSceneBootstrap.WorldSeedValue)));

            Material material = Track(CreateTestMaterial());
            var cityObject = Track(new GameObject("MobileCity"));
            world.Coordinates.TryCellToWorld(
                8,
                7,
                .5f,
                out Vector3 cityPosition);
            cityObject.transform.position = cityPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider bodyCollider =
                cityObject.AddComponent<BoxCollider>();
            var cityVisual = new GameObject("Visual");
            cityVisual.transform.SetParent(cityObject.transform, false);
            MeshRenderer cityRenderer =
                cityVisual.AddComponent<MeshRenderer>();
            cityRenderer.sharedMaterial = material;
            GrayboxVisualSlot citySlot =
                cityVisual.AddComponent<GrayboxVisualSlot>();
            citySlot.Configure(
                "core.city.mobile",
                cityRenderer,
                new Color(.9f, .48f, .1f));
            citySlot.ApplyFallback(material);
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, bodyCollider);

            var leaderObject = Track(new GameObject("Leader_CenJin"));
            leaderObject.transform.position =
                cityPosition + new Vector3(1.8f, .5f, 1.2f);
            GrayboxLeaderController3D leader =
                leaderObject.AddComponent<GrayboxLeaderController3D>();
            leader.Configure(world, city, true);

            var directObject = Track(new GameObject("DirectControl"));
            GrayboxDirectControlCoordinator directControl =
                directObject.AddComponent<
                    GrayboxDirectControlCoordinator>();
            directControl.Configure(city, leader);

            var rigObject = Track(new GameObject("CameraRig"));
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(rigObject.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.localPosition = new Vector3(0f, 18f, -14f);
            camera.transform.localEulerAngles =
                new Vector3(52f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 13f;

            var projectorObject =
                Track(new GameObject("GroundProjector"));
            GrayboxGroundProjector projector =
                projectorObject.AddComponent<GrayboxGroundProjector>();
            projector.Configure(camera, world.Coordinates);

            var cameraControllerObject =
                Track(new GameObject("CameraController"));
            GrayboxCameraController3D cameraController =
                cameraControllerObject.AddComponent<
                    GrayboxCameraController3D>();
            cameraController.Configure(
                camera,
                rigObject.transform,
                city,
                leader,
                directControl,
                projector);

            var routerObject = Track(new GameObject("InputRouter"));
            GrayboxInputRouter router =
                routerObject.AddComponent<GrayboxInputRouter>();
            router.Configure(
                city,
                leader,
                directControl,
                projector,
                cameraController);

            return new AdapterFixture(
                city,
                directControl,
                router,
                cameraController);
        }

        private GrayboxWorldView3D CreateWorldView()
        {
            var root = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Material material = Track(CreateTestMaterial());
            GrayboxWorldView3D view =
                root.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            return view;
        }

        private static Material CreateTestMaterial()
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        private static Transform NewChild(
            Transform parent,
            string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static string ExtractMethodBlock(
            string source,
            string methodName)
        {
            string signature = "void " + methodName + "()";
            int start = source.IndexOf(
                signature,
                StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int openingBrace = source.IndexOf('{', start);
            Assert.That(openingBrace, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{')
                    depth++;
                else if (source[index] == '}')
                    depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }

            throw new AssertionException(
                "Unbalanced method block for " + methodName + ".");
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class AdapterFixture
        {
            private readonly GrayboxMobileCityController3D city;
            private readonly GrayboxDirectControlCoordinator directControl;
            private readonly GrayboxInputRouter router;
            private readonly GrayboxCameraController3D cameraController;
            private readonly GrayboxInputFrame inputFrame =
                new GrayboxInputFrame(
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false);

            public AdapterFixture(
                GrayboxMobileCityController3D city,
                GrayboxDirectControlCoordinator directControl,
                GrayboxInputRouter router,
                GrayboxCameraController3D cameraController)
            {
                this.city = city;
                this.directControl = directControl;
                this.router = router;
                this.cameraController = cameraController;
            }

            public void TickAll()
            {
                city.TickMovement(.02f);
                city.TickDeployment(.016f);
                directControl.Refresh();
                router.ProcessFrame(inputFrame);
                router.TickGameplay(.016f);
                cameraController.TickCamera();
            }
        }
    }
}
