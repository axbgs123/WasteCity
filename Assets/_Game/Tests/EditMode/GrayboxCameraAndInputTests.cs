using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxCameraAndInputTests
    {
        private const string InputFrameTypeName =
            "WasteCity.Graybox3D.GrayboxInputFrame";

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private readonly List<Camera> fixtureCameras =
            new List<Camera>();
        private SimulationMode originalSimulationMode;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            originalSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                foreach (Camera camera in fixtureCameras)
                {
                    if (camera != null)
                        camera.targetTexture = null;
                }
                fixtureCameras.Clear();

                for (int index = cleanup.Count - 1; index >= 0; index--)
                {
                    if (cleanup[index] != null)
                        UnityEngine.Object.DestroyImmediate(cleanup[index]);
                }

                cleanup.Clear();
            }
            finally
            {
                Time.timeScale = 1f;
                Physics.simulationMode = originalSimulationMode;
            }
        }

        [Test]
        public void ProjectToPlane_UsesInclinedCameraAndMathematicalGround()
        {
            ProjectionFixture fixture = CreateProjectionFixture();

            bool projected = fixture.Projector.TryProjectToPlane(
                new Vector2(640f, 360f),
                out Vector3 point);

            Assert.That(projected, Is.True);
            Assert.That(point.x, Is.EqualTo(0f).Within(.0001f));
            Assert.That(point.y, Is.EqualTo(0f).Within(.0001f));
            Assert.That(point.z, Is.EqualTo(.0631413f).Within(.001f));
            Assert.That(
                fixture.CameraObject.GetComponent<Collider>(),
                Is.Null);
        }

        [TestCase(0f)]
        [TestCase(-52f)]
        public void ProjectToPlane_RejectsParallelAndBackwardRays(
            float cameraPitch)
        {
            ProjectionFixture fixture = CreateProjectionFixture();
            fixture.Camera.transform.localEulerAngles =
                new Vector3(cameraPitch, 0f, 0f);

            bool projected = fixture.Projector.TryProjectToPlane(
                new Vector2(640f, 360f),
                out _);

            Assert.That(projected, Is.False);
        }

        [Test]
        public void ProjectToCell_RejectsProjectedPointOutsideMap()
        {
            ProjectionFixture fixture = CreateProjectionFixture();
            Vector3 outsideWorld = new Vector3(40f, 0f, 0f);
            Vector3 outsideScreen =
                fixture.Camera.WorldToScreenPoint(outsideWorld);
            Assert.That(
                fixture.Projector.TryProjectToPlane(
                    outsideScreen,
                    out Vector3 projected),
                Is.True);
            Assert.That(projected.x, Is.EqualTo(40f).Within(.001f));

            bool inside = fixture.Projector.TryProjectToCell(
                outsideScreen,
                out _,
                out _,
                out _);

            Assert.That(inside, Is.False);
        }

        [Test]
        public void InputFrame_IsReadonlyValueAndPreservesInputs()
        {
            Type frameType = GetInputFrameType();
            Assert.That(frameType.IsValueType, Is.True);
            FieldInfo[] fields = frameType.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
                Assert.That(field.IsInitOnly, Is.True, field.Name);

            object frame = CreateInputFrame(
                new Vector2(.25f, -.75f),
                new Vector2(320f, 180f),
                toggleDeploymentPressed: true,
                destinationPressed: false,
                middlePressed: true,
                middleHeld: true,
                middleReleased: false,
                homePressed: true);

            AssertFrameProperty(
                frame,
                "Move",
                new Vector2(.25f, -.75f));
            AssertFrameProperty(
                frame,
                "PointerPosition",
                new Vector2(320f, 180f));
            AssertFrameProperty(frame, "ToggleDeploymentPressed", true);
            AssertFrameProperty(frame, "DestinationPressed", false);
            AssertFrameProperty(frame, "MiddlePressed", true);
            AssertFrameProperty(frame, "MiddleHeld", true);
            AssertFrameProperty(frame, "MiddleReleased", false);
            AssertFrameProperty(frame, "HomePressed", true);
        }

        [Test]
        public void ProcessFrame_DoesNotTickCityOrLeader()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            Vector3 cityStart = fixture.CityBody.position;
            Vector3 leaderStart = fixture.Leader.transform.position;

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.right,
                    new Vector2(640f, 360f)));

            Assert.That(fixture.CityBody.position, Is.EqualTo(cityStart));
            Assert.That(
                fixture.Leader.transform.position,
                Is.EqualTo(leaderStart));
        }

        [Test]
        public void ProcessFrame_CityTargetRoutesNormalizedWASDToCity()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            Vector3 start = fixture.CityBody.position;

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    new Vector2(3f, 4f),
                    new Vector2(640f, 360f)));
            fixture.City.TickMovement(.1f);
            fixture.SimulateFixedStep();

            Assert.That(
                fixture.CityBody.position.x - start.x,
                Is.EqualTo(.24f).Within(.0001f));
            Assert.That(
                fixture.CityBody.position.z - start.z,
                Is.EqualTo(.32f).Within(.0001f));
            Assert.That(
                fixture.CityBody.position.y,
                Is.EqualTo(start.y));
        }

        [Test]
        public void ProcessFrameThenTickGameplay_LeaderTargetMovesLeaderOnly()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            Assert.That(fixture.DirectControl.Refresh(), Is.True);
            Vector3 cityStart = fixture.CityBody.position;
            Vector3 leaderStart = fixture.Leader.transform.position;

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    new Vector2(3f, 4f),
                    new Vector2(640f, 360f)));
            fixture.Router.TickGameplay(.1f);

            Assert.That(
                fixture.Leader.transform.position.x - leaderStart.x,
                Is.EqualTo(.3f).Within(.0001f));
            Assert.That(
                fixture.Leader.transform.position.z - leaderStart.z,
                Is.EqualTo(.4f).Within(.0001f));
            Assert.That(
                fixture.Leader.transform.position.y,
                Is.EqualTo(leaderStart.y));
            Assert.That(fixture.CityBody.position, Is.EqualTo(cityStart));
        }

        [Test]
        public void TickGameplay_CityTargetDocksLeaderWithoutMovingCity()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.Leader.transform.position =
                new Vector3(
                    10f,
                    fixture.Leader.transform.position.y,
                    9f);
            Vector3 cityStart = fixture.CityBody.position;

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.right,
                    new Vector2(640f, 360f)));
            fixture.Router.TickGameplay(.1f);

            Assert.That(
                fixture.Leader.transform.position,
                Is.EqualTo(
                    new Vector3(
                        cityStart.x + 1.8f,
                        fixture.Leader.transform.position.y,
                        cityStart.z + 1.2f)));
            Assert.That(fixture.CityBody.position, Is.EqualTo(cityStart));
        }

        [Test]
        public void TickGameplay_PauseDoesNotMoveLeaderWithLatchedInput()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            fixture.DirectControl.Refresh();
            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.right,
                    new Vector2(640f, 360f)));
            Vector3 leaderStart = fixture.Leader.transform.position;
            Vector3 cityStart = fixture.CityBody.position;
            Time.timeScale = 0f;

            fixture.Router.TickGameplay(.1f);

            Assert.That(
                fixture.Leader.transform.position,
                Is.EqualTo(leaderStart));
            Assert.That(fixture.CityBody.position, Is.EqualTo(cityStart));
        }

        [Test]
        public void TickGameplay_PauseDoesNotDockLeaderForCityTarget()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            fixture.DirectControl.Refresh();
            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.right,
                    new Vector2(640f, 360f)));
            fixture.City.Deployment.Restore(CityMode.Mobile, 0f);
            fixture.Leader.transform.position =
                new Vector3(
                    10f,
                    fixture.Leader.transform.position.y,
                    9f);
            Vector3 leaderStart = fixture.Leader.transform.position;
            Time.timeScale = 0f;

            fixture.Router.TickGameplay(.1f);

            Assert.That(
                fixture.Leader.transform.position,
                Is.EqualTo(leaderStart));
        }

        [Test]
        public void TickGameplay_NegativeDeltaDoesNotMoveLeader()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            fixture.DirectControl.Refresh();
            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.right,
                    new Vector2(640f, 360f)));
            Vector3 leaderStart = fixture.Leader.transform.position;

            fixture.Router.TickGameplay(-1f);

            Assert.That(
                fixture.Leader.transform.position,
                Is.EqualTo(leaderStart));
        }

        [Test]
        public void ProcessFrame_ToggleDeploymentAlwaysRoutesToCity()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            fixture.DirectControl.Refresh();
            Assert.That(
                fixture.DirectControl.ControlTarget,
                Is.EqualTo(DirectControlTarget.Leader));

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.zero,
                    new Vector2(640f, 360f),
                    toggleDeploymentPressed: true));

            Assert.That(
                fixture.City.Mode,
                Is.EqualTo(CityMode.Packing));
        }

        [Test]
        public void ProcessFrame_RightClickSetsMobileCityDestination()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            Assert.That(
                fixture.World.Coordinates.TryCellToWorld(
                    18,
                    12,
                    0f,
                    out Vector3 targetWorld),
                Is.True);
            Vector3 targetScreen =
                fixture.Camera.WorldToScreenPoint(targetWorld);

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.zero,
                    targetScreen,
                    destinationPressed: true));

            Assert.That(fixture.City.AutopilotActive, Is.True);
            Assert.That(fixture.City.Destination.HasValue, Is.True);
            Assert.That(fixture.City.Destination.Value.X, Is.EqualTo(18));
            Assert.That(fixture.City.Destination.Value.Y, Is.EqualTo(12));
        }

        [Test]
        public void ProcessFrame_RightClickIsIgnoredOutsideMobileMode()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            fixture.World.Coordinates.TryCellToWorld(
                18,
                12,
                0f,
                out Vector3 targetWorld);

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.zero,
                    fixture.Camera.WorldToScreenPoint(targetWorld),
                    destinationPressed: true));

            Assert.That(fixture.City.AutopilotActive, Is.False);
            Assert.That(fixture.City.Destination, Is.Null);
        }

        [Test]
        public void ProcessFrame_RightClickIsIgnoredWhenProjectionIsOutside()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            Vector3 outsideScreen =
                fixture.Camera.WorldToScreenPoint(
                    new Vector3(40f, 0f, 0f));

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.zero,
                    outsideScreen,
                    destinationPressed: true));

            Assert.That(fixture.City.AutopilotActive, Is.False);
            Assert.That(fixture.City.Destination, Is.Null);
        }

        [Test]
        public void ProcessFrame_PauseBlocksGameplayButAllowsDragAndHome()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.CameraController.TickCamera();
            Vector3 cityStart = fixture.CityBody.position;
            Vector3 leaderStart = fixture.Leader.transform.position;
            Vector3 targetScreen =
                fixture.Camera.WorldToScreenPoint(
                    new Vector3(2f, 0f, 0f));
            Time.timeScale = 0f;

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.right,
                    targetScreen,
                    toggleDeploymentPressed: true,
                    destinationPressed: true,
                    middlePressed: true,
                    middleHeld: true));

            Assert.That(
                fixture.CameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.City.AutopilotActive, Is.False);
            Assert.That(fixture.CityBody.position, Is.EqualTo(cityStart));
            Assert.That(
                fixture.Leader.transform.position,
                Is.EqualTo(leaderStart));

            fixture.SetCityPosition(
                new Vector3(4f, cityStart.y, -3f));
            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.zero,
                    targetScreen,
                    homePressed: true));

            Assert.That(
                fixture.CameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            AssertRigXZ(
                fixture.CameraRig,
                fixture.CityBody.position.x,
                fixture.CityBody.position.z,
                fixture.CameraRigStartY);

            Time.timeScale = 1f;
            fixture.City.TickMovement(.1f);
            fixture.SimulateFixedStep();
            Assert.That(fixture.CityBody.position.x, Is.EqualTo(4f));
            Assert.That(fixture.CityBody.position.z, Is.EqualTo(-3f));
        }

        [Test]
        public void TickCamera_FollowsCityXZAndPreservesCameraContract()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.SetCityPosition(
                new Vector3(
                    5f,
                    fixture.CityBody.position.y,
                    -4f));

            fixture.CameraController.TickCamera();

            AssertRigXZ(
                fixture.CameraRig,
                5f,
                -4f,
                fixture.CameraRigStartY);
            AssertCameraContract(fixture);
        }

        [Test]
        public void FreeDrag_UsesProjectedPreviousMinusCurrentAndReleaseStaysFree()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.CameraController.TickCamera();
            Vector2 start = new Vector2(640f, 360f);
            Vector2 end = new Vector2(740f, 410f);
            Assert.That(
                fixture.Projector.TryProjectToPlane(
                    start,
                    out Vector3 previous),
                Is.True);
            Assert.That(
                fixture.Projector.TryProjectToPlane(
                    end,
                    out Vector3 current),
                Is.True);
            Vector3 before = fixture.CameraRig.position;

            fixture.CameraController.BeginFreeDrag(start);
            fixture.CameraController.ContinueFreeDrag(end);
            fixture.CameraController.EndFreeDrag();
            Vector3 expected =
                before +
                new Vector3(
                    previous.x - current.x,
                    0f,
                    previous.z - current.z);

            AssertVector3(
                fixture.CameraRig.position,
                expected,
                .0001f);
            Assert.That(
                fixture.CameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));
            fixture.CameraController.TickCamera();
            AssertVector3(
                fixture.CameraRig.position,
                expected,
                .0001f);
            AssertCameraContract(fixture);
        }

        [Test]
        public void ProcessFrame_HomeReturnsAndSnapsInSameCall()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.SetCityPosition(
                new Vector3(
                    6f,
                    fixture.CityBody.position.y,
                    -5f));
            fixture.CameraController.TickCamera();
            fixture.CameraController.BeginFreeDrag(
                new Vector2(640f, 360f));
            fixture.CameraController.ContinueFreeDrag(
                new Vector2(740f, 410f));
            Assert.That(
                fixture.CameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));

            ProcessFrame(
                fixture.Router,
                CreateInputFrame(
                    Vector2.zero,
                    new Vector2(740f, 410f),
                    homePressed: true));

            Assert.That(
                fixture.CameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            AssertRigXZ(
                fixture.CameraRig,
                6f,
                -5f,
                fixture.CameraRigStartY);
        }

        [Test]
        public void TickCamera_TargetChangeRestoresFollowingAndSnapsSameCall()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.CameraController.TickCamera();
            fixture.CameraController.BeginFreeDrag(
                new Vector2(640f, 360f));
            fixture.CameraController.ContinueFreeDrag(
                new Vector2(740f, 410f));
            fixture.Leader.transform.position =
                new Vector3(7f, fixture.Leader.transform.position.y, 8f);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);

            fixture.CameraController.TickCamera();

            Assert.That(
                fixture.CameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(
                fixture.CameraController.CurrentTarget,
                Is.EqualTo(DirectControlTarget.Leader));
            AssertRigXZ(
                fixture.CameraRig,
                7f,
                8f,
                fixture.CameraRigStartY);
        }

        [Test]
        public void TickCamera_MissingLeaderReferenceFallsBackToCity()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            fixture.DirectControl.Refresh();
            fixture.SetCityPosition(
                new Vector3(
                    -5f,
                    fixture.CityBody.position.y,
                    4f));
            fixture.CameraController.Configure(
                fixture.Camera,
                fixture.CameraRig,
                fixture.City,
                null,
                fixture.DirectControl,
                fixture.Projector);

            fixture.CameraController.TickCamera();

            Assert.That(fixture.CameraController.ReferencesReady, Is.True);
            Assert.That(
                fixture.CameraController.CurrentTarget,
                Is.EqualTo(DirectControlTarget.City));
            AssertRigXZ(
                fixture.CameraRig,
                -5f,
                4f,
                fixture.CameraRigStartY);
        }

        [Test]
        public void TickCamera_MissingCityReferencePreservesRigPosition()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            fixture.CameraRig.position = new Vector3(9f, 3f, -8f);
            Vector3 before = fixture.CameraRig.position;
            fixture.CameraController.Configure(
                fixture.Camera,
                fixture.CameraRig,
                null,
                fixture.Leader,
                fixture.DirectControl,
                fixture.Projector);

            fixture.CameraController.TickCamera();

            Assert.That(fixture.CameraController.ReferencesReady, Is.False);
            Assert.That(fixture.CameraRig.position, Is.EqualTo(before));
        }

        [Test]
        public void AdapterFrameProcessing_AllocatesZeroBytesAcross300Calls()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(true);
            var warmupFrameA = fixture.Router.ReadCurrentFrame();
            fixture.Router.ProcessFrame(warmupFrameA);
            fixture.Router.TickGameplay(.016f);
            fixture.CameraController.TickCamera();
            var warmupFrameB = fixture.Router.ReadCurrentFrame();
            fixture.Router.ProcessFrame(warmupFrameB);
            fixture.Router.TickGameplay(.016f);
            fixture.CameraController.TickCamera();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frameIndex = 0; frameIndex < 300; frameIndex++)
            {
                var frame = fixture.Router.ReadCurrentFrame();
                fixture.Router.ProcessFrame(frame);
                fixture.Router.TickGameplay(.016f);
                fixture.CameraController.TickCamera();
            }
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "Task7AllocationDifference=" + difference);
            Assert.That(difference, Is.Zero);
        }

        private ProjectionFixture CreateProjectionFixture()
        {
            var rigObject = Track(new GameObject("CameraRig"));
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(
                rigObject.transform,
                false);
            Camera camera = cameraObject.AddComponent<Camera>();
            ConfigureFrozenCamera(camera);

            var projectorObject =
                Track(new GameObject("GroundProjector"));
            GrayboxGroundProjector projector =
                projectorObject.AddComponent<GrayboxGroundProjector>();
            projector.Configure(
                camera,
                new PlanarCoordinateMapper3D(32, 24));

            return new ProjectionFixture(
                cameraObject,
                camera,
                projector);
        }

        private RuntimeFixture CreateRuntimeFixture(
            bool developmentFixtureRecruited)
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = Track(new Material(shader));

            var worldRoot = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(
                worldRoot.transform,
                "TerrainRoot");
            Transform resources = NewChild(
                worldRoot.transform,
                "ResourceRoot");
            Transform obstacles = NewChild(
                worldRoot.transform,
                "ObstacleRoot");
            GrayboxWorldView3D world =
                worldRoot.AddComponent<GrayboxWorldView3D>();
            world.Configure(terrain, resources, obstacles, material);
            world.Generate(
                new WorldMapModel(
                    FilledMap(32, 24, OpenCell())));

            var cityObject = Track(new GameObject("MobileCity"));
            world.Coordinates.TryCellToWorld(
                16,
                12,
                .5f,
                out Vector3 cityStart);
            cityObject.transform.position = cityStart;
            Rigidbody cityBody =
                cityObject.AddComponent<Rigidbody>();
            BoxCollider cityCollider =
                cityObject.AddComponent<BoxCollider>();
            Transform cityVisual =
                NewChild(cityObject.transform, "Visual");
            MeshRenderer cityRenderer =
                cityVisual.gameObject.AddComponent<MeshRenderer>();
            cityRenderer.sharedMaterial = material;
            GrayboxVisualSlot citySlot =
                cityVisual.gameObject.AddComponent<GrayboxVisualSlot>();
            citySlot.Configure(
                "core.city.mobile",
                cityRenderer,
                new Color(.9f, .48f, .1f));
            citySlot.ApplyFallback(material);
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, cityBody, cityCollider);

            var leaderObject = Track(new GameObject("Leader"));
            world.Coordinates.TryCellToWorld(
                18,
                14,
                .9f,
                out Vector3 leaderStart);
            leaderObject.transform.position = leaderStart;
            GrayboxLeaderController3D leader =
                leaderObject.AddComponent<GrayboxLeaderController3D>();
            leader.Configure(
                world,
                city,
                developmentFixtureRecruited);

            var directObject =
                Track(new GameObject("DirectControl"));
            GrayboxDirectControlCoordinator directControl =
                directObject
                    .AddComponent<GrayboxDirectControlCoordinator>();
            directControl.Configure(city, leader);

            var rigObject = Track(new GameObject("CameraRig"));
            rigObject.transform.position = new Vector3(0f, 3f, 0f);
            float cameraRigStartY = rigObject.transform.position.y;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(
                rigObject.transform,
                false);
            Camera camera = cameraObject.AddComponent<Camera>();
            ConfigureFrozenCamera(camera);

            var projectorObject =
                Track(new GameObject("GroundProjector"));
            GrayboxGroundProjector projector =
                projectorObject.AddComponent<GrayboxGroundProjector>();
            projector.Configure(camera, world.Coordinates);

            var cameraControllerObject =
                Track(new GameObject("CameraController"));
            GrayboxCameraController3D cameraController =
                cameraControllerObject
                    .AddComponent<GrayboxCameraController3D>();
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

            return new RuntimeFixture(
                world,
                city,
                cityBody,
                leader,
                directControl,
                rigObject.transform,
                camera,
                projector,
                cameraController,
                router,
                cameraRigStartY);
        }

        private void ConfigureFrozenCamera(Camera camera)
        {
            fixtureCameras.Add(camera);
            var targetTexture =
                Track(new RenderTexture(1280, 720, 0));
            camera.targetTexture = targetTexture;
            camera.transform.localPosition =
                new Vector3(0f, 18f, -14f);
            camera.transform.localEulerAngles =
                new Vector3(52f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 13f;
        }

        private static object CreateInputFrame(
            Vector2 move,
            Vector2 pointerPosition,
            bool toggleDeploymentPressed = false,
            bool destinationPressed = false,
            bool middlePressed = false,
            bool middleHeld = false,
            bool middleReleased = false,
            bool homePressed = false)
        {
            Type frameType = GetInputFrameType();
            return Activator.CreateInstance(
                frameType,
                move,
                pointerPosition,
                toggleDeploymentPressed,
                destinationPressed,
                middlePressed,
                middleHeld,
                middleReleased,
                homePressed);
        }

        private static Type GetInputFrameType()
        {
            Type frameType =
                typeof(GrayboxInputRouter).Assembly.GetType(
                    InputFrameTypeName);
            Assert.That(frameType, Is.Not.Null);
            return frameType;
        }

        private static void ProcessFrame(
            GrayboxInputRouter router,
            object frame)
        {
            MethodInfo method =
                typeof(GrayboxInputRouter).GetMethod(
                    "ProcessFrame",
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            method.Invoke(router, new[] { frame });
        }

        private static void AssertFrameProperty(
            object frame,
            string propertyName,
            object expected)
        {
            PropertyInfo property =
                frame.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.GetValue(frame), Is.EqualTo(expected));
        }

        private static void AssertCameraContract(
            RuntimeFixture fixture)
        {
            AssertVector3(
                fixture.Camera.transform.localPosition,
                new Vector3(0f, 18f, -14f),
                .0001f);
            Assert.That(
                fixture.Camera.transform.localEulerAngles.x,
                Is.EqualTo(52f).Within(.0001f));
            Assert.That(
                fixture.Camera.transform.localEulerAngles.y,
                Is.EqualTo(0f).Within(.0001f));
            Assert.That(
                fixture.Camera.transform.localEulerAngles.z,
                Is.EqualTo(0f).Within(.0001f));
            Assert.That(fixture.Camera.orthographic, Is.True);
            Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(13f));
        }

        private static void AssertRigXZ(
            Transform rig,
            float expectedX,
            float expectedZ,
            float expectedY)
        {
            Assert.That(rig.position.x, Is.EqualTo(expectedX).Within(.0001f));
            Assert.That(rig.position.z, Is.EqualTo(expectedZ).Within(.0001f));
            Assert.That(rig.position.y, Is.EqualTo(expectedY));
        }

        private static void AssertVector3(
            Vector3 actual,
            Vector3 expected,
            float tolerance)
        {
            Assert.That(
                actual.x,
                Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(
                actual.y,
                Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(
                actual.z,
                Is.EqualTo(expected.z).Within(tolerance));
        }

        private static WorldCell[,] FilledMap(
            int width,
            int height,
            WorldCell cell)
        {
            var cells = new WorldCell[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = cell;
            return cells;
        }

        private static WorldCell OpenCell()
        {
            return new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
        }

        private static Transform NewChild(
            Transform parent,
            string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class ProjectionFixture
        {
            public GameObject CameraObject { get; }
            public Camera Camera { get; }
            public GrayboxGroundProjector Projector { get; }

            public ProjectionFixture(
                GameObject cameraObject,
                Camera camera,
                GrayboxGroundProjector projector)
            {
                CameraObject = cameraObject;
                Camera = camera;
                Projector = projector;
            }
        }

        private sealed class RuntimeFixture
        {
            public GrayboxWorldView3D World { get; }
            public GrayboxMobileCityController3D City { get; }
            public Rigidbody CityBody { get; }
            public GrayboxLeaderController3D Leader { get; }
            public GrayboxDirectControlCoordinator DirectControl { get; }
            public Transform CameraRig { get; }
            public Camera Camera { get; }
            public GrayboxGroundProjector Projector { get; }
            public GrayboxCameraController3D CameraController { get; }
            public GrayboxInputRouter Router { get; }
            public float CameraRigStartY { get; }

            public RuntimeFixture(
                GrayboxWorldView3D world,
                GrayboxMobileCityController3D city,
                Rigidbody cityBody,
                GrayboxLeaderController3D leader,
                GrayboxDirectControlCoordinator directControl,
                Transform cameraRig,
                Camera camera,
                GrayboxGroundProjector projector,
                GrayboxCameraController3D cameraController,
                GrayboxInputRouter router,
                float cameraRigStartY)
            {
                World = world;
                City = city;
                CityBody = cityBody;
                Leader = leader;
                DirectControl = directControl;
                CameraRig = cameraRig;
                Camera = camera;
                Projector = projector;
                CameraController = cameraController;
                Router = router;
                CameraRigStartY = cameraRigStartY;
            }

            public void SimulateFixedStep()
            {
                Physics.SyncTransforms();
                Physics.Simulate(.02f);
            }

            public void SetCityPosition(Vector3 position)
            {
                City.transform.position = position;
                Physics.SyncTransforms();
            }
        }
    }
}
