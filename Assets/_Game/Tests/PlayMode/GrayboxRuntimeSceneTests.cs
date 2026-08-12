using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxRuntimeSceneTests
    {
        private const string SceneName = "GrayboxPrototype3D";

        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousInputUpdateMode;
        private InputSettings.BackgroundBehavior
            previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private RenderPipelineAsset previousGraphics;
        private RenderPipelineAsset previousQuality;

        [UnitySetUp]
        public IEnumerator LoadGrayboxScene()
        {
            Time.timeScale = 1f;
            previousGraphics =
                GraphicsSettings.defaultRenderPipeline;
            previousQuality =
                QualitySettings.renderPipeline;
            previousInputUpdateMode =
                InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode =
                InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();

            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadGrayboxScene()
        {
            try
            {
                Time.timeScale = 1f;
                yield return LoadEmptyScene();
            }
            finally
            {
                try
                {
                    if (keyboard != null && keyboard.added)
                        InputSystem.RemoveDevice(keyboard);
                }
                finally
                {
                    try
                    {
                        if (mouse != null && mouse.added)
                            InputSystem.RemoveDevice(mouse);
                    }
                    finally
                    {
                        try
                        {
                            InputSystem.settings.updateMode =
                                previousInputUpdateMode;
                        }
                        finally
                        {
                            try
                            {
                                InputSystem.settings
                                    .editorInputBehaviorInPlayMode =
                                    previousEditorInputBehavior;
                            }
                            finally
                            {
                                InputSystem.settings
                                    .backgroundBehavior =
                                    previousBackgroundBehavior;
                            }
                        }
                    }
                }

                Assert.That(
                    InputSystem.settings.updateMode,
                    Is.EqualTo(previousInputUpdateMode));
                Assert.That(
                    InputSystem.settings
                        .editorInputBehaviorInPlayMode,
                    Is.EqualTo(previousEditorInputBehavior));
                Assert.That(
                    InputSystem.settings.backgroundBehavior,
                    Is.EqualTo(previousBackgroundBehavior));
            }
        }

        [UnityTest]
        public IEnumerator SceneReload_InitializesWorldUrpAnd3DContracts()
        {
            GrayboxSceneBootstrap bootstrap =
                Object.FindObjectOfType<GrayboxSceneBootstrap>();
            GrayboxWorldView3D worldView =
                Object.FindObjectOfType<GrayboxWorldView3D>();

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(worldView.Model.Width, Is.EqualTo(64));
            Assert.That(worldView.Model.Height, Is.EqualTo(48));
            Assert.That(
                GraphicsSettings.currentRenderPipeline,
                Is.TypeOf<UniversalRenderPipelineAsset>());
            Assert.That(
                Object.FindObjectsOfType<GrayboxUrpScope>().Length,
                Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxMobileCityController3D>().Length,
                Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<
                    GrayboxDirectControlCoordinator>().Length,
                Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<SpriteRenderer>(),
                Is.Empty);
            Assert.That(
                Object.FindObjectsOfType<Rigidbody2D>(),
                Is.Empty);
            Assert.That(
                Object.FindObjectsOfType<Collider2D>(),
                Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneReload_PersistsProjectionAndLeaderFixture()
        {
            GrayboxGroundProjector projector =
                Object.FindObjectOfType<GrayboxGroundProjector>();
            GrayboxLeaderController3D leader =
                Object.FindObjectOfType<GrayboxLeaderController3D>();
            GrayboxWorldView3D worldView =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            Camera camera = Camera.main;

            Assert.That(projector, Is.Not.Null);
            Assert.That(leader, Is.Not.Null);
            Assert.That(
                leader.DevelopmentFixtureRecruited,
                Is.True);
            Assert.That(leader.Model.Recruited, Is.True);
            Assert.That(
                worldView.Coordinates.TryCellToWorld(
                    8,
                    7,
                    0f,
                    out Vector3 world),
                Is.True);
            Vector2 screen = camera.WorldToScreenPoint(world);
            Assert.That(
                projector.TryProjectToCell(
                    screen,
                    out _,
                    out int cellX,
                    out int cellY),
                Is.True);
            Assert.That(cellX, Is.EqualTo(8));
            Assert.That(cellY, Is.EqualTo(7));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VirtualKeyboard_MovesMobileCityThroughRuntimeLoop()
        {
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            Vector3 before = city.transform.position;

            yield return HoldKey(Key.W, 3);

            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(city.transform.position.z, Is.GreaterThan(before.z));
            Assert.That(city.transform.position.y, Is.EqualTo(before.y));
        }

        [UnityTest]
        public IEnumerator VirtualRightClick_UsesProjectorAndPreservesPathOnFailure()
        {
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            GrayboxWorldView3D worldView =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            Camera camera = Camera.main;
            Assert.That(
                worldView.TryWorldToCell(
                    city.transform.position,
                    out int startX,
                    out int startY),
                Is.True);
            FindReachableDestination(
                worldView.Model,
                startX,
                startY,
                out int targetX,
                out int targetY);
            worldView.Coordinates.TryCellToWorld(
                targetX,
                targetY,
                0f,
                out Vector3 destinationWorld);
            Vector3 clickWorld =
                destinationWorld + new Vector3(.5f, 0f, .5f);

            yield return ClickMouse(
                MouseButton.Right,
                camera.WorldToScreenPoint(clickWorld));

            Assert.That(city.AutopilotActive, Is.True);
            Assert.That(city.Destination.HasValue, Is.True);
            Assert.That(city.Destination.Value.X, Is.EqualTo(targetX));
            Assert.That(city.Destination.Value.Y, Is.EqualTo(targetY));
            Rigidbody body = city.GetComponent<Rigidbody>();
            float distanceBefore = Vector2.Distance(
                new Vector2(body.position.x, body.position.z),
                new Vector2(destinationWorld.x, destinationWorld.z));

            for (int step = 0; step < 3; step++)
                yield return new WaitForFixedUpdate();

            float distanceAfter = Vector2.Distance(
                new Vector2(body.position.x, body.position.z),
                new Vector2(destinationWorld.x, destinationWorld.z));
            Assert.That(distanceAfter, Is.LessThan(distanceBefore));
            Assert.That(city.Destination.HasValue, Is.True);
            Assert.That(city.Destination.Value.X, Is.EqualTo(targetX));
            Assert.That(city.Destination.Value.Y, Is.EqualTo(targetY));

            yield return ClickMouse(
                MouseButton.Right,
                camera.WorldToScreenPoint(
                    new Vector3(100f, 0f, 100f)));

            Assert.That(city.AutopilotActive, Is.True);
            Assert.That(city.Destination.HasValue, Is.True);
            Assert.That(city.Destination.Value.X, Is.EqualTo(targetX));
            Assert.That(city.Destination.Value.Y, Is.EqualTo(targetY));
        }

        [UnityTest]
        public IEnumerator VirtualF_RejectsIllegalAndCompletesLegalDeployment()
        {
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            GrayboxWorldView3D worldView =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            Rigidbody body = city.GetComponent<Rigidbody>();
            FindDeploymentCell(
                worldView.Model,
                CityDeploymentFailure.None,
                out int validX,
                out int validY);
            FindAnyInvalidDeploymentCell(
                worldView.Model,
                out int invalidX,
                out int invalidY);

            MoveCityToCell(
                city,
                body,
                worldView.Coordinates,
                invalidX,
                invalidY);
            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(
                city.LastDeploymentFailure,
                Is.Not.EqualTo(CityDeploymentFailure.None));

            MoveCityToCell(
                city,
                body,
                worldView.Coordinates,
                validX,
                validY);
            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Deploying));
            city.Deployment.Restore(CityMode.Deploying, .001f);
            for (int frame = 0;
                 frame < 60 && city.Mode != CityMode.Fortress;
                 frame++)
                yield return null;
            Assert.That(city.Mode, Is.EqualTo(CityMode.Fortress));
        }

        [UnityTest]
        public IEnumerator VirtualKeyboard_DrivesLeaderAndPackingReturnsCity()
        {
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            GrayboxLeaderController3D leader =
                Object.FindObjectOfType<
                    GrayboxLeaderController3D>();
            GrayboxDirectControlCoordinator coordinator =
                Object.FindObjectOfType<
                    GrayboxDirectControlCoordinator>();
            GrayboxCameraController3D cameraController =
                Object.FindObjectOfType<
                    GrayboxCameraController3D>();
            Transform rig = Camera.main.transform.parent;
            Vector2 dragStart = new Vector2(
                Screen.width * .5f,
                Screen.height * .5f);
            Vector2 dragEnd =
                dragStart + new Vector2(100f, 40f);

            yield return DragMouse(dragStart, dragEnd);
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));
            Assert.That(
                cameraController.CurrentTarget,
                Is.EqualTo(DirectControlTarget.City));

            city.Deployment.Restore(CityMode.Fortress, 0f);
            leader.transform.position = city.transform.position;
            yield return null;
            Assert.That(
                coordinator.ControlTarget,
                Is.EqualTo(DirectControlTarget.Leader));
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(
                cameraController.CurrentTarget,
                Is.EqualTo(DirectControlTarget.Leader));
            Assert.That(
                rig.position.x,
                Is.EqualTo(leader.transform.position.x).Within(.001f));
            Assert.That(
                rig.position.z,
                Is.EqualTo(leader.transform.position.z).Within(.001f));
            Vector3 cityBefore = city.transform.position;
            Vector3 leaderBefore = leader.transform.position;

            yield return HoldKey(Key.W, 2);

            Assert.That(
                leader.transform.position.z,
                Is.GreaterThan(leaderBefore.z));
            Assert.That(city.transform.position, Is.EqualTo(cityBefore));

            yield return DragMouse(dragStart, dragEnd);
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));
            Assert.That(
                cameraController.CurrentTarget,
                Is.EqualTo(DirectControlTarget.Leader));

            city.Deployment.Restore(CityMode.Packing, .001f);
            for (int frame = 0;
                 frame < 60 &&
                 (city.Mode != CityMode.Mobile ||
                  coordinator.ControlTarget != DirectControlTarget.City ||
                  cameraController.Mode != CameraFollowMode.Following ||
                  cameraController.CurrentTarget != DirectControlTarget.City);
                 frame++)
                yield return null;
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(
                coordinator.ControlTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(
                cameraController.CurrentTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(
                rig.position.x,
                Is.EqualTo(city.transform.position.x).Within(.001f));
            Assert.That(
                rig.position.z,
                Is.EqualTo(city.transform.position.z).Within(.001f));
        }

        [UnityTest]
        public IEnumerator Pause_StopsGameplayButAllowsFreeDragAndHome()
        {
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            GrayboxCameraController3D cameraController =
                Object.FindObjectOfType<
                    GrayboxCameraController3D>();
            Transform rig = Camera.main.transform.parent;
            Vector3 cityBefore = city.transform.position;
            bool autopilotBefore = city.AutopilotActive;
            CityMode modeBefore = city.Mode;
            float remainingBefore = city.Deployment.Remaining;
            Vector2 start = new Vector2(
                Screen.width * .5f,
                Screen.height * .5f);
            Vector2 end = start + new Vector2(100f, 40f);
            Time.timeScale = 0f;

            QueueKeyboard(Key.W);
            yield return null;
            yield return DragMouse(start, end);
            QueueKeyboard();
            yield return null;

            Assert.That(city.transform.position, Is.EqualTo(cityBefore));
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));
            Assert.That(
                new Vector2(rig.position.x, rig.position.z),
                Is.Not.EqualTo(
                    new Vector2(cityBefore.x, cityBefore.z)));

            yield return TapKey(Key.Home);

            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(rig.position.x, Is.EqualTo(city.transform.position.x));
            Assert.That(rig.position.z, Is.EqualTo(city.transform.position.z));
            Assert.That(city.AutopilotActive, Is.EqualTo(autopilotBefore));
            Assert.That(city.Mode, Is.EqualTo(modeBefore));
            Assert.That(
                city.Deployment.Remaining,
                Is.EqualTo(remainingBefore));
        }

        [UnityTest]
        public IEnumerator SceneUnload_RestoresGraphicsAndQualitySeparately()
        {
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.Not.SameAs(previousGraphics));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.Not.SameAs(previousQuality));

            yield return LoadEmptyScene();

            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.SameAs(previousGraphics));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(previousQuality));
        }

        private IEnumerator HoldKey(Key key, int fixedSteps)
        {
            QueueKeyboard(key);
            yield return null;
            for (int step = 0; step < fixedSteps; step++)
                yield return new WaitForFixedUpdate();
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator TapKey(Key key)
        {
            QueueKeyboard(key);
            yield return null;
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator ClickMouse(
            MouseButton button,
            Vector2 position)
        {
            QueueMouse(position, button);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator DragMouse(Vector2 start, Vector2 end)
        {
            QueueMouse(start, MouseButton.Middle);
            yield return null;
            QueueMouse(
                end,
                MouseButton.Middle,
                expectPressedThisFrame: false);
            yield return null;
            QueueMouse(end);
            yield return null;
        }

        private void QueueKeyboard(params Key[] keys)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(keys));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(
                keyboard.anyKey.isPressed,
                Is.EqualTo(keys.Length > 0));
            for (int index = 0; index < keys.Length; index++)
            {
                Assert.That(
                    keyboard[keys[index]].isPressed,
                    Is.True,
                    keys[index].ToString());
                Assert.That(
                    keyboard[keys[index]].wasPressedThisFrame,
                    Is.True,
                    keys[index].ToString());
            }
        }

        private void QueueMouse(
            Vector2 position,
            MouseButton? button = null,
            bool expectPressedThisFrame = true)
        {
            var state = new MouseState
            {
                position = position
            };
            if (button.HasValue)
                state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(
                mouse.position.ReadValue(),
                Is.EqualTo(position));
            if (button == MouseButton.Right)
            {
                Assert.That(mouse.rightButton.isPressed, Is.True);
                Assert.That(
                    mouse.rightButton.wasPressedThisFrame,
                    Is.EqualTo(expectPressedThisFrame));
            }
            else if (button == MouseButton.Middle)
            {
                Assert.That(mouse.middleButton.isPressed, Is.True);
                Assert.That(
                    mouse.middleButton.wasPressedThisFrame,
                    Is.EqualTo(expectPressedThisFrame));
            }
            else
            {
                Assert.That(mouse.rightButton.isPressed, Is.False);
                Assert.That(mouse.middleButton.isPressed, Is.False);
            }
        }

        private static IEnumerator LoadEmptyScene()
        {
            Scene graybox =
                SceneManager.GetSceneByName(SceneName);
            if (!graybox.IsValid() || !graybox.isLoaded)
            {
                yield return null;
                yield break;
            }

            Scene empty = SceneManager.CreateScene(
                "GrayboxRuntimeEmpty");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(graybox);
            yield return null;
        }

        private static void MoveCityToCell(
            GrayboxMobileCityController3D city,
            Rigidbody body,
            PlanarCoordinateMapper3D coordinates,
            int cellX,
            int cellY)
        {
            coordinates.TryCellToWorld(
                cellX,
                cellY,
                city.transform.position.y,
                out Vector3 world);
            body.position = world;
            city.transform.position = world;
            Physics.SyncTransforms();
        }

        private static void FindReachableDestination(
            WorldMapModel map,
            int startX,
            int startY,
            out int targetX,
            out int targetY)
        {
            for (int radius = 1; radius <= 5; radius++)
            for (int x = Mathf.Max(0, startX - radius);
                 x <= Mathf.Min(map.Width - 1, startX + radius);
                 x++)
            for (int y = Mathf.Max(0, startY - radius);
                 y <= Mathf.Min(map.Height - 1, startY + radius);
                 y++)
            {
                if (CityPathfinder.TryFindPath(
                        map,
                        startX,
                        startY,
                        x,
                        y,
                        out WorldGridPoint[] path) &&
                    path.Length > 0)
                {
                    targetX = x;
                    targetY = y;
                    return;
                }
            }

            throw new AssertionException(
                "Seed 8128 must provide a nearby reachable cell.");
        }

        private static void FindDeploymentCell(
            WorldMapModel map,
            CityDeploymentFailure expected,
            out int cellX,
            out int cellY)
        {
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                if (CityDeploymentRules.Validate(map, x, y) != expected)
                    continue;
                cellX = x;
                cellY = y;
                return;
            }

            throw new AssertionException(
                $"Seed 8128 must provide deployment result {expected}.");
        }

        private static void FindAnyInvalidDeploymentCell(
            WorldMapModel map,
            out int cellX,
            out int cellY)
        {
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                if (CityDeploymentRules.Validate(
                        map,
                        x,
                        y) == CityDeploymentFailure.None)
                    continue;
                cellX = x;
                cellY = y;
                return;
            }

            throw new AssertionException(
                "Seed 8128 must provide an illegal deployment cell.");
        }
    }
}
