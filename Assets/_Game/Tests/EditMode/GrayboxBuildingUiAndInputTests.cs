using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingUiAndInputTests
    {
        private static readonly Vector2 ScreenCenter =
            new Vector2(320f, 240f);

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private float originalTimeScale;
        private Keyboard testKeyboard;
        private Mouse testMouse;
        private Vector2 testPointer = ScreenCenter;
        private object inputTestFixture;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            testPointer = ScreenCenter;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            if (testKeyboard != null && testKeyboard.added)
                InputSystem.RemoveDevice(testKeyboard);
            if (testMouse != null && testMouse.added)
                InputSystem.RemoveDevice(testMouse);
            testKeyboard = null;
            testMouse = null;
            for (var index = 0; index < cleanup.Count; index++)
                if (cleanup[index] != null &&
                    cleanup[index] is GameObject gameObject)
                {
                    Camera camera = gameObject.GetComponent<Camera>();
                    if (camera != null)
                        camera.targetTexture = null;
                }
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
            if (inputTestFixture != null)
            {
                inputTestFixture.GetType().GetMethod("TearDown")
                    .Invoke(inputTestFixture, null);
                inputTestFixture = null;
            }
        }

        [Test]
        public void Interaction_CatalogReturnsToItsOriginAndRetainsPreview()
        {
            GrayboxBuildingInteractionModel3D interaction =
                Create<GrayboxBuildingInteractionModel3D>("Interaction");

            interaction.ToggleCatalog();
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            interaction.CloseCatalog();
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));

            interaction.Select(BuildingCatalog.Wall);
            interaction.RotateClockwise();
            interaction.ToggleCatalog();
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            interaction.CloseCatalog();

            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Wall));
            Assert.That(interaction.Orientation, Is.EqualTo(
                BuildingOrientation.East));
            interaction.CancelPreview();
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(interaction.Selected, Is.Null);
            Assert.That(interaction.Orientation, Is.EqualTo(
                BuildingOrientation.North));
        }

        [Test]
        public void InputRouter_ImplementsTheGenericGrayboxInterceptor()
        {
            Type router = typeof(GrayboxBuildingMenuView3D).Assembly.GetType(
                "WasteCity.Graybox3D.Building." +
                "GrayboxBuildingInputRouter3D");

            Assert.That(router, Is.Not.Null);
            Assert.That(
                typeof(GrayboxInputRouter).Assembly.GetType(
                    "WasteCity.Graybox3D.IGrayboxInputInterceptor")
                    .IsAssignableFrom(router),
                Is.True);
            Assert.That(
                router.GetMethod(
                    "ProcessCurrentInput",
                    Type.EmptyTypes),
                Is.Not.Null);
        }

        [Test]
        public void InputRouter_BQuickbarRotateAndCancelFollowBuildState()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();

            GrayboxInputSuppression catalog =
                PressKey(fixture.Router, Key.B);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(catalog.Move, Is.False);

            PressKey(fixture.Router, Key.Digit4);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(fixture.Interaction.Selected, Is.SameAs(
                BuildingCatalog.Wall));

            PressKey(fixture.Router, Key.R);
            Assert.That(fixture.Interaction.Orientation, Is.EqualTo(
                BuildingOrientation.East));

            GrayboxInputSuppression cancelled =
                PressMouse(fixture.Router, MouseButton.Right);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(fixture.Interaction.Selected, Is.Null);
            Assert.That(cancelled.Destination, Is.True);
        }

        [Test]
        public void InputRouter_EscapeClosesCatalogThenCancelsPreview()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            PressKey(fixture.Router, Key.B);

            PressKey(fixture.Router, Key.Escape);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));

            PressKey(fixture.Router, Key.B);
            PressKey(fixture.Router, Key.Digit2);
            Assert.That(fixture.Interaction.Selected, Is.SameAs(
                BuildingCatalog.Housing));
            PressKey(fixture.Router, Key.Escape);

            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(fixture.Interaction.Selected, Is.Null);
        }

        [Test]
        public void InputRouter_LeftClickConfirmsOnlyAValidPreview()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            fixture.Interaction.Select(BuildingCatalog.Wall);
            PositionInputCameraAtCell(fixture, 20, 15);
            SetPointer(ScreenCenter);

            PressMouse(fixture.Router, MouseButton.Left);

            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));

            fixture.Interaction.CancelPreview();
            PositionInputCameraOver(
                fixture.Camera,
                new Vector3(100f, 0f, 100f));
            fixture.Interaction.Select(BuildingCatalog.Wall);
            PressMouse(fixture.Router, MouseButton.Left);

            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
        }

        [Test]
        public void InputRouter_DeleteUsesConstructionCancellationRules()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            GrayboxBuildingInstance3D zeroProgress =
                BeginGroundConstruction(
                    fixture.Session,
                    BuildingCatalog.Wall,
                    20,
                    15,
                    fixture.Presentation);
            Assert.That(
                fixture.Construction.SelectInstance(
                    zeroProgress.StableInstanceId),
                Is.True);

            PressKey(fixture.Router, Key.Delete);

            Assert.That(fixture.Session.Instances, Is.Empty);

            GrayboxBuildingInstance3D progressed =
                BeginGroundConstruction(
                    fixture.Session,
                    BuildingCatalog.Wall,
                    20,
                    15,
                    fixture.Presentation);
            fixture.Construction.TickConstruction(.5f);
            Assert.That(
                fixture.Construction.SelectInstance(
                    progressed.StableInstanceId),
                Is.True);
            PressKey(fixture.Router, Key.Delete);

            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CancelConfirmation));
        }

        [Test]
        public void InputRouter_FDelegatesToEvacuationBeforeBaseDeployment()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            BeginGroundConstruction(
                fixture.Session,
                BuildingCatalog.Wall,
                20,
                15,
                fixture.Presentation);

            GrayboxInputSuppression suppression =
                PressKey(fixture.Router, Key.F);

            Assert.That(fixture.Evacuation.IsManifestOpen, Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(suppression.Deployment, Is.True);
        }

        [Test]
        public void InputRouter_BuildModeKeepsMovementAndCameraButOwnsDestination()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.Interaction.Select(BuildingCatalog.Wall);
            SetPointer(ScreenCenter);

            GrayboxInputSuppression keyboard =
                PressKey(fixture.Router, Key.W);
            GrayboxInputSuppression pointer =
                PressMouse(fixture.Router, MouseButton.Right);

            Assert.That(keyboard.Move, Is.False);
            Assert.That(keyboard.Home, Is.False);
            Assert.That(pointer.CameraDrag, Is.False);
            Assert.That(pointer.Destination, Is.True);
        }

        [Test]
        public void InputRouter_UiPointerBlocksWorldClickAndCameraDrag()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            Button quickbar = FindComponent<Button>(
                fixture.Canvas.transform,
                "QuickbarSlot.0");
            ForceCanvasLayout(fixture.Canvas);
            SetPointer(RectCenter(fixture.Ui, quickbar));

            GrayboxInputSuppression left =
                PressMouse(fixture.Router, MouseButton.Left);
            GrayboxInputSuppression middle =
                PressMouse(fixture.Router, MouseButton.Middle);

            Assert.That(left.Destination, Is.True);
            Assert.That(middle.CameraDrag, Is.True);
            Assert.That(fixture.Session.Instances, Is.Empty);
        }

        [Test]
        public void InputRouter_HeldPointerTracksLiveGraphicVisibility()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            CreateCountingGraphicTarget(
                out GameObject pointerTarget);
            pointerTarget.SetActive(false);
            ForceCanvasLayout(fixture.Canvas);
            QueueMouseState(ScreenCenter, middleHeld: true);

            GrayboxInputSuppression noHit =
                fixture.Router.ProcessCurrentInput();

            Assert.That(noHit.CameraDrag, Is.False);

            pointerTarget.SetActive(true);
            ForceCanvasLayout(fixture.Canvas);
            QueueMouseState(ScreenCenter, middleHeld: true);

            GrayboxInputSuppression opened =
                fixture.Router.ProcessCurrentInput();

            Assert.That(opened.CameraDrag, Is.True);

            pointerTarget.SetActive(false);
            ForceCanvasLayout(fixture.Canvas);
            QueueMouseState(ScreenCenter, middleHeld: true);

            GrayboxInputSuppression closed =
                fixture.Router.ProcessCurrentInput();

            Assert.That(closed.CameraDrag, Is.False);
        }

        [Test]
        public void InputRouter_UiReleaseEndsWorldDragWithoutStaleContinuation()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            Button quickbar = FindComponent<Button>(
                fixture.Canvas.transform,
                "QuickbarSlot.0");
            ForceCanvasLayout(fixture.Canvas);
            Vector2 uiPointer = RectCenter(fixture.Ui, quickbar);
            Vector2 worldStart = ScreenCenter + Vector2.right * 100f;

            Transform cameraRig =
                NewObject("InputCameraRig").transform;
            GrayboxGroundProjector groundProjector =
                Create<GrayboxGroundProjector>(
                    "InputGroundProjector");
            groundProjector.Configure(
                fixture.Camera,
                fixture.World.Coordinates);
            GrayboxCameraController3D cameraController =
                Create<GrayboxCameraController3D>(
                    "InputCameraController");
            cameraController.Configure(
                fixture.Camera,
                cameraRig,
                fixture.City,
                null,
                null,
                groundProjector);
            GrayboxInputRouter baseRouter =
                Create<GrayboxInputRouter>("InputBaseRouter");
            baseRouter.Configure(
                fixture.City,
                null,
                null,
                groundProjector,
                cameraController);
            baseRouter.ConfigureInputInterceptor(fixture.Router);

            RouteMiddleInput(
                fixture.Router,
                baseRouter,
                worldStart,
                held: true);
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));

            GrayboxInputSuppression release = RouteMiddleInput(
                fixture.Router,
                baseRouter,
                uiPointer,
                held: false);
            Assert.That(release.CameraDrag, Is.True);

            GrayboxInputSuppression uiPress = RouteMiddleInput(
                fixture.Router,
                baseRouter,
                uiPointer,
                held: true);
            Assert.That(uiPress.CameraDrag, Is.True);
            Vector3 beforeLeavingUi = cameraRig.position;

            GrayboxInputSuppression worldHold = RouteMiddleInput(
                fixture.Router,
                baseRouter,
                ScreenCenter,
                held: true);

            Assert.That(worldHold.CameraDrag, Is.False);
            Assert.That(cameraRig.position, Is.EqualTo(beforeLeavingUi));
        }

        [Test]
        public void InputRouter_FocusedKeyboardStillClassifiesUiPointerChannels()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Ui.Menu.RefreshCatalog();
            InputField input = FindComponent<InputField>(
                fixture.Canvas.transform,
                "Catalog.Search");
            fixture.EventSystem.GetComponent<InputSystemUIInputModule>()
                .enabled = true;
            fixture.EventSystem.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            Button quickbar = FindComponent<Button>(
                fixture.Canvas.transform,
                "QuickbarSlot.0");
            ForceCanvasLayout(fixture.Canvas);
            SetPointer(RectCenter(fixture.Ui, quickbar));

            GrayboxInputSuppression suppression =
                PressMouse(fixture.Router, MouseButton.Middle);

            Assert.That(suppression.Move, Is.True);
            Assert.That(suppression.Destination, Is.True);
            Assert.That(suppression.CameraDrag, Is.True);
            Assert.That(suppression.Home, Is.True);
        }

        [Test]
        public void InputRouter_KeyboardFocusBlocksGameplayAndDeveloperKeys()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Ui.Menu.RefreshCatalog();
            InputField input = FindComponent<InputField>(
                fixture.Canvas.transform,
                "Catalog.Search");
            fixture.EventSystem.GetComponent<InputSystemUIInputModule>()
                .enabled = true;
            fixture.EventSystem.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            SetPointer(ScreenCenter);
            Assert.That(fixture.Ui.Menu.HasKeyboardFocus(), Is.True);
            Assert.That(fixture.Developer.IsRuntimeAvailable, Is.False);
            Assert.That(
                fixture.Canvas.transform.Find("Graybox Developer Modifier"),
                Is.Null);
            BuildingOrientation before = fixture.Interaction.Orientation;

            Key[] blocked =
            {
                Key.W, Key.A, Key.S, Key.D, Key.B, Key.R,
                Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
                Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8,
                Key.Digit9, Key.Digit0, Key.F, Key.F10,
                Key.Home, Key.Delete, Key.Enter
            };
            for (var index = 0; index < blocked.Length; index++)
            {
                GrayboxInputSuppression suppression =
                    PressKey(fixture.Router, blocked[index]);
                Assert.That(
                    suppression.Move,
                    Is.True,
                    blocked[index].ToString());
                Assert.That(
                    suppression.Deployment,
                    Is.True,
                    blocked[index].ToString());
                Assert.That(
                    suppression.Home,
                    Is.True,
                    blocked[index].ToString());
            }

            GrayboxInputSuppression escape =
                PressKey(fixture.Router, Key.Escape);

            Assert.That(escape.Move, Is.True);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(fixture.Interaction.Selected, Is.Null);
            Assert.That(fixture.Interaction.Orientation, Is.EqualTo(before));
            Assert.That(fixture.Evacuation.IsManifestOpen, Is.False);
            Assert.That(
                fixture.Canvas.transform.Find("Graybox Developer Modifier"),
                Is.Null);
        }

        [Test]
        public void InputRouter_FocusLossRestoresGameplayOnNextInputFrame()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Ui.Menu.RefreshCatalog();
            InputField input = FindComponent<InputField>(
                fixture.Canvas.transform,
                "Catalog.Search");
            InputSystemUIInputModule inputModule =
                fixture.EventSystem.GetComponent<InputSystemUIInputModule>();
            inputModule.enabled = true;
            fixture.EventSystem.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            Assert.That(fixture.Ui.Menu.HasKeyboardFocus(), Is.True);
            PressKey(fixture.Router, Key.B);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));

            input.DeactivateInputField();
            fixture.EventSystem.SetSelectedGameObject(null);
            inputModule.enabled = false;
            InputSystem.Update();
            PressKey(fixture.Router, Key.B);

            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
        }

        [Test]
        public void
            InputRouter_F10RemainsInertOutsidePlayModeAfterFocusAndModalHandling()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            Assert.That(fixture.Developer.IsRuntimeAvailable, Is.False);

            PressKey(fixture.Router, Key.F10);
            Assert.That(
                fixture.Canvas.transform.Find("Graybox Developer Modifier"),
                Is.Null);
            PressKey(fixture.Router, Key.F10);
            Assert.That(
                fixture.Canvas.transform.Find("Graybox Developer Modifier"),
                Is.Null);

            fixture.Interaction.RequestCancelConstruction();
            PressKey(fixture.Router, Key.F10);

            Assert.That(
                fixture.Canvas.transform.Find("Graybox Developer Modifier"),
                Is.Null);
        }

        [Test]
        public void InputRouter_OutsideBuildModeLeavesDigitsUnconsumed()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();

            GrayboxInputSuppression suppression =
                PressKey(fixture.Router, Key.Digit1);

            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(fixture.Interaction.Selected, Is.Null);
            Assert.That(suppression.Move, Is.False);
            Assert.That(suppression.Deployment, Is.False);
            Assert.That(suppression.Destination, Is.False);
            Assert.That(suppression.CameraDrag, Is.False);
            Assert.That(suppression.Home, Is.False);
        }

        [Test]
        public void InputRouter_StablePreviewReevaluatesInventoryMutation()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            PrepareStableWallPreview(fixture);
            fixture.Session.Inventory.Set(
                BuildingCatalog.Wall.CostId,
                0);

            fixture.Router.ProcessCurrentInput();

            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Does.Contain(
                    BuildingPlacementFailure.InsufficientMaterials));

            fixture.Session.Inventory.Set(
                BuildingCatalog.Wall.CostId,
                BuildingCatalog.Wall.Cost);

            fixture.Router.ProcessCurrentInput();

            Assert.That(
                fixture.Placement.CurrentEvaluation.IsValid,
                Is.True);
        }

        [Test]
        public void InputRouter_StablePreviewReevaluatesCityModeMutation()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            PrepareStableWallPreview(fixture);

            fixture.Router.ProcessCurrentInput();
            Assert.That(
                fixture.Placement.CurrentEvaluation.IsValid,
                Is.True);

            fixture.City.Deployment.Restore(CityMode.Mobile, 0f);

            fixture.Router.ProcessCurrentInput();

            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Does.Contain(BuildingPlacementFailure.InvalidCityMode));
        }

        [Test]
        public void InputRouter_StablePreviewReevaluatesGridOccupancyMutation()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            PrepareStableWallPreview(fixture);

            fixture.Router.ProcessCurrentInput();
            Assert.That(
                fixture.Placement.CurrentEvaluation.IsValid,
                Is.True);
            BuildingSurfaceHit hit =
                fixture.Placement.CurrentHit;
            Assert.That(hit.IsValid, Is.True);
            Assert.That(hit.Site, Is.EqualTo(BuildingSite.Ground));
            Assert.That(
                fixture.Session.GroundGrid.TryRestore(
                    BuildingCatalog.Wall,
                    hit.X,
                    hit.Y,
                    out _),
                Is.True);

            fixture.Router.ProcessCurrentInput();

            Assert.That(
                fixture.Placement.CurrentEvaluation.Failures,
                Does.Contain(BuildingPlacementFailure.Overlap));
        }

        [Test]
        public void InputRouter_ProcessCurrentInputAllocatesZeroAcross300Calls()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.Router.ProcessCurrentInput();
            fixture.Router.ProcessCurrentInput();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                fixture.Router.ProcessCurrentInput();
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "Task9ProcessCurrentInputAllocationDifference=" +
                difference);
            Assert.That(difference, Is.Zero);
        }

        [Test]
        public void InputRouter_UiMiddleHoldAllocatesZeroAcross300Calls()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            CountingGraphicRaycaster raycaster =
                CreateCountingGraphicTarget(out _);
            QueueMouseState(
                ScreenCenter,
                middleHeld: true);
            QueueMouseState(
                ScreenCenter,
                middleHeld: true);
            Assert.That(
                fixture.Router.ProcessCurrentInput().CameraDrag,
                Is.True);
            fixture.Router.ProcessCurrentInput();

            int raycastsBefore = raycaster.RaycastCalls;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                fixture.Router.ProcessCurrentInput();
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;
            int raycastDifference =
                raycaster.RaycastCalls - raycastsBefore;

            TestContext.WriteLine(
                "Task9UiMiddleHoldAllocationDifference=" +
                difference);
            TestContext.WriteLine(
                "Task9UiMiddleHoldRaycastDifference=" +
                raycastDifference);
            Assert.That(raycastDifference, Is.EqualTo(300));
            Assert.That(difference, Is.Zero);
        }

        [Test]
        public void InputRouter_WorldMiddleHoldAllocatesZeroAcross300Calls()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            CountingGraphicRaycaster raycaster =
                CreateCountingGraphicTarget(
                    out GameObject pointerTarget);
            pointerTarget.SetActive(false);
            ForceCanvasLayout(fixture.Canvas);
            QueueMouseState(ScreenCenter, middleHeld: true);
            QueueMouseState(ScreenCenter, middleHeld: true);
            Assert.That(
                fixture.Router.ProcessCurrentInput().CameraDrag,
                Is.False);
            fixture.Router.ProcessCurrentInput();

            GrayboxInputSuppression lastSuppression = default;
            int raycastsBefore = raycaster.RaycastCalls;
            ProfilerRecorder allocationRecorder =
                ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC.Alloc",
                    2048,
                    ProfilerRecorderOptions.StartImmediately |
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
            {
                lastSuppression =
                    fixture.Router.ProcessCurrentInput();
            }
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;
            int raycastDifference =
                raycaster.RaycastCalls - raycastsBefore;
            allocationRecorder.Stop();
            long profiledDifference = 0;
            for (var index = 0;
                 index < allocationRecorder.Count;
                 index++)
            {
                ProfilerRecorderSample sample =
                    allocationRecorder.GetSample(index);
                profiledDifference +=
                    sample.Value * sample.Count;
            }
            allocationRecorder.Dispose();

            TestContext.WriteLine(
                "Task9WorldMiddleHoldAllocationDifference=" +
                difference);
            TestContext.WriteLine(
                "Task9WorldMiddleHoldProfiledAllocationDifference=" +
                profiledDifference);
            TestContext.WriteLine(
                "Task9WorldMiddleHoldRaycastDifference=" +
                raycastDifference);
            Assert.That(lastSuppression.CameraDrag, Is.False);
            Assert.That(raycastDifference, Is.EqualTo(300));
            Assert.That(difference, Is.Zero);
            Assert.That(profiledDifference, Is.Zero);
        }

        [Test]
        public void InputRouter_StablePreviewAllocatesZeroAcross300Calls()
        {
            InputRouterFixture fixture = CreateInputRouterFixture();
            fixture.Interaction.Select(BuildingCatalog.Wall);
            SetPointer(ScreenCenter);
            fixture.Router.ProcessCurrentInput();
            fixture.Router.ProcessCurrentInput();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                fixture.Router.ProcessCurrentInput();
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "Task9StablePreviewAllocationDifference=" +
                difference);
            Assert.That(difference, Is.Zero);
        }

        [Test]
        public void Menu_CreatesTenQuickbarFiveCategoryAndFourRouteControls()
        {
            UiFixture fixture = CreateMenuFixture();

            Assert.That(
                NamedComponents<Button>(fixture.Canvas.transform, "QuickbarSlot.").Count,
                Is.EqualTo(10));
            Assert.That(
                NamedComponents<Button>(fixture.Canvas.transform, "Category.").Count,
                Is.EqualTo(5));
            Assert.That(
                NamedComponents<Button>(fixture.Canvas.transform, "Route.").Count,
                Is.EqualTo(4));
            Assert.That(
                FindComponent<InputField>(
                    fixture.Canvas.transform,
                    "Catalog.Search"),
                Is.Not.Null);
            Assert.That(fixture.Menu.CatalogVisible, Is.False);
            Assert.That(fixture.Menu.EvacuationVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator
            Menu_SerializedCloneLifecycleBuildsExactlyOneRuntimeTree()
        {
            yield return new EnterPlayMode();
            UiFixture fixture = CreateMenuFixture(true);
            yield return null;

            Assert.That(
                NamedTransforms(
                    fixture.Canvas.transform,
                    "GrayboxBuildingUi.Root").Count,
                Is.EqualTo(1));
            Assert.That(
                NamedComponents<Button>(
                    fixture.Canvas.transform,
                    "QuickbarSlot.").Count,
                Is.EqualTo(10));

            fixture.Menu.enabled = false;
            fixture.Menu.enabled = true;
            fixture.Menu.Configure(
                fixture.Canvas,
                fixture.EventSystem,
                fixture.Session,
                fixture.Interaction);
            yield return null;

            Assert.That(
                NamedTransforms(
                    fixture.Canvas.transform,
                    "GrayboxBuildingUi.Root").Count,
                Is.EqualTo(1));
            yield return new ExitPlayMode();
        }

        [Test]
        public void Menu_CatalogDoesNotPauseWorldAndSearchesOnlyVisibleItems()
        {
            UiFixture fixture = CreateMenuFixture();
            float timeScaleBefore = Time.timeScale;

            fixture.Interaction.ToggleCatalog();
            fixture.Menu.RefreshCatalog();
            fixture.Menu.SetSearchText("power-plant");

            Assert.That(fixture.Menu.CatalogVisible, Is.True);
            Assert.That(fixture.Menu.SearchText, Is.EqualTo("power-plant"));
            Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore));
            Assert.That(AllText(fixture.Canvas.transform), Does.Not.Contain(
                BuildingCatalog.PowerPlant.Name));
            Assert.That(AllText(fixture.Canvas.transform), Does.Not.Contain(
                BuildingCatalog.PowerPlant.Id.Value));
            Assert.That(FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.PowerPlant.Id.Value),
                Is.Null);
        }

        [Test]
        public void Menu_LockedCardsAreDisabledAndExposePrimaryAndAllReasons()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);

            Transform card = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Smelter.Id.Value);
            Assert.That(card, Is.Not.Null);
            Button button = card.GetComponent<Button>();
            Assert.That(button.interactable, Is.False);

            GrayboxBuildingCatalogItem3D item =
                new GrayboxBuildingCatalogPresenter3D().Describe(
                    fixture.Session,
                    BuildingCatalog.Smelter);
            string text = AllText(card);
            Assert.That(text, Does.Contain(item.PrimaryLockReason));
            foreach (string reason in item.LockReasons)
                Assert.That(text, Does.Contain(reason));
            Assert.That(text, Does.Contain(
                BuildingCatalog.Smelter.Cost.ToString()));
        }

        [Test]
        public void Menu_SelectionRejectsInvalidHiddenLockedAndFilteredItems()
        {
            UiFixture fixture = CreateMenuFixture();

            Assert.That(fixture.Menu.TrySelectQuickbarSlot(-1), Is.False);
            Assert.That(fixture.Menu.TrySelectQuickbarSlot(10), Is.False);
            Assert.That(fixture.Menu.TrySelectQuickbarSlot(0), Is.True);
            Assert.That(fixture.Interaction.Selected, Is.SameAs(
                BuildingCatalog.MiningStation));

            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Basic);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    BuildingCatalog.Smelter.Id.Value),
                Is.False);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    BuildingCatalog.PowerPlant.Id.Value),
                Is.False);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    "missing.building"),
                Is.False);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    BuildingCatalog.Housing.Id.Value),
                Is.True);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(fixture.Interaction.Selected, Is.SameAs(
                BuildingCatalog.Housing));
            Assert.That(fixture.Menu.CatalogVisible, Is.False);
        }

        [Test]
        public void Menu_EmptyQuickbarSlotsAndHiddenCatalogItemsCreateNoText()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Session.SetRouteContact(ContentRoute.Technology, false);
            fixture.Menu.RefreshCatalog();

            Transform hiddenCard = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.PowerPlant.Id.Value);
            Assert.That(hiddenCard, Is.Null);

            string text = AllText(fixture.Canvas.transform);
            Assert.That(text, Does.Not.Contain(BuildingCatalog.PowerPlant.Name));
            Assert.That(text, Does.Not.Contain(
                BuildingCatalog.PowerPlant.Id.Value));
            Assert.That(
                NamedComponents<Button>(
                    fixture.Canvas.transform,
                    "Catalog.Card.").Count,
                Is.LessThan(GrayboxBuildingCatalogPresenter3D.BuildMenuCount));
        }

        [Test]
        public void Menu_UpdateRefreshesCatalogOnlyAfterSessionRevisionChanges()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.RefreshCatalog();
            Transform oldQuickbar = FindTransform(
                fixture.Canvas.transform, "QuickbarSlot.0");
            Assert.That(FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.PowerPlant.Id.Value), Is.Null);

            fixture.Session.SetRouteContact(ContentRoute.Technology, true);
            InvokeLifecycle(fixture.Menu, "Update");
            Transform card = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.PowerPlant.Id.Value);
            Assert.That(card, Is.Not.Null);
            Transform refreshedQuickbar = FindTransform(
                fixture.Canvas.transform, "QuickbarSlot.0");
            InvokeLifecycle(fixture.Menu, "Update");

            Assert.That(FindTransform(
                fixture.Canvas.transform, "QuickbarSlot.0"), Is.SameAs(refreshedQuickbar));
            Assert.That(FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.PowerPlant.Id.Value), Is.SameAs(card));
            Assert.That(oldQuickbar, Is.Not.SameAs(refreshedQuickbar));
        }

        [Test]
        public void Menu_UpdateMakesResearchLockedCardInteractableWithoutExplicitRefresh()
        {
            UiFixture fixture = CreateMenuFixture();
            InvokeLifecycle(fixture.Menu, "Update");
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);
            Button card = FindComponent<Button>(fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Smelter.Id.Value);
            Assert.That(card.interactable, Is.False);

            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.Smelter.RequiredResearchId);
            InvokeLifecycle(fixture.Menu, "Update");

            Assert.That(FindComponent<Button>(fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Smelter.Id.Value).interactable,
                Is.True);
        }

        [Test]
        public void Menu_UpdateMakesCompletedPrerequisiteDependentCardInteractable()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.Smelter.RequiredResearchId);
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.Assembler.RequiredResearchId);
            InvokeLifecycle(fixture.Menu, "Update");
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);
            Button before = FindComponent<Button>(fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Assembler.Id.Value);
            Assert.That(before.interactable, Is.False);

            var presentation = new RecordingPresentation();
            BeginGroundConstruction(
                fixture.Session,
                BuildingCatalog.Smelter,
                10,
                10,
                presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                presentation);
            InvokeLifecycle(fixture.Menu, "Update");

            Assert.That(FindComponent<Button>(fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Assembler.Id.Value).interactable,
                Is.True);
        }

        [Test]
        public void Menu_DetailsAlwaysShowMinimumPopulationWhenSatisfiedOrLocked()
        {
            UiFixture fixture = CreateMenuFixture();
            var presenter = new GrayboxBuildingCatalogPresenter3D();
            string satisfied = BuildDetails(presenter.Describe(
                fixture.Session,
                BuildingCatalog.ResearchStation));

            fixture.Session.SetRouteContact(ContentRoute.Technology, true);
            string locked = BuildDetails(presenter.Describe(
                fixture.Session,
                BuildingCatalog.PowerPlant));

            Assert.That(satisfied, Does.Contain("最低人口：200"));
            Assert.That(locked, Does.Contain("最低人口：1000"));
            Assert.That(locked, Does.Contain("锁定原因 "));
        }

        [Test]
        public void UiGuard_RealSelectableFocusOwnsKeyboardUntilFollowingFrame()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.RefreshCatalog();
            InputField input = FindComponent<InputField>(
                fixture.Canvas.transform,
                "Catalog.Search");
            fixture.EventSystem.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            var guard = new GrayboxUiInputGuard3D();

            Assert.That(guard.HasKeyboardFocus(fixture.EventSystem), Is.True);
            input.text = "WASDBR1230";
            Assert.That(input.text, Is.EqualTo("WASDBR1230"));
            Assert.That(guard.ConsumeFocusedEscape(fixture.EventSystem), Is.True);
            Assert.That(fixture.EventSystem.currentSelectedGameObject, Is.Null);
            Assert.That(guard.HasKeyboardFocus(fixture.EventSystem), Is.True);
        }

        [Test]
        public void UiGuard_RealButtonAndRaycasterOwnKeyboardAndPointer()
        {
            UiFixture fixture = CreateMenuFixture();
            Camera uiCamera = Create<Camera>("UiCamera");
            uiCamera.orthographic = true;
            uiCamera.transform.position = new Vector3(0f, 0f, -10f);
            uiCamera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            fixture.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            fixture.Canvas.worldCamera = uiCamera;
            fixture.Canvas.planeDistance = 1f;
            GameObject pointerTarget = NewObject("PointerTarget");
            var rect = pointerTarget.AddComponent<RectTransform>();
            rect.SetParent(fixture.Canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            pointerTarget.AddComponent<Image>();
            Button button = pointerTarget.AddComponent<Button>();
            fixture.EventSystem.SetSelectedGameObject(button.gameObject);
            var guard = new GrayboxUiInputGuard3D();

            Canvas.ForceUpdateCanvases();
            Assert.That(guard.HasKeyboardFocus(fixture.EventSystem), Is.True);
            Vector2 pointerPosition = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                rect.TransformPoint(rect.rect.center));
            Assert.That(
                guard.IsPointerOverUi(
                    fixture.EventSystem,
                    pointerPosition),
                Is.True);
            Assert.That(guard.ConsumeFocusedEscape(fixture.EventSystem), Is.True);
        }

        [Test]
        public void UiGuard_UsesQualifyingEventSystemGraphicAfterEmptyFallbackCache()
        {
            var guard = new GrayboxUiInputGuard3D();
            EventSystem emptyEventSystem =
                Create<EventSystem>("EmptyEventSystem");
            Canvas emptyCanvas = Create<Canvas>("EmptyFallbackCanvas");
            emptyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GraphicRaycaster emptyRaycaster =
                emptyCanvas.gameObject.AddComponent<GraphicRaycaster>();
            RegisterHeadlessEditModeRaycaster(emptyRaycaster);
            Assert.That(
                guard.IsPointerOverUi(emptyEventSystem, ScreenCenter),
                Is.False);

            UiFixture fixture = CreateMenuFixture();
            Button button = FindComponent<Button>(
                fixture.Canvas.transform,
                "QuickbarSlot.0");
            Assert.That(button, Is.Not.Null);
            ForceCanvasLayout(fixture.Canvas);
            Vector2 pointerPosition = RectCenter(fixture, button);
            GraphicRaycaster raycaster =
                button.GetComponentInParent<GraphicRaycaster>();
            var results = new List<RaycastResult>();
            fixture.EventSystem.RaycastAll(
                new PointerEventData(fixture.EventSystem)
                {
                    position = pointerPosition
                },
                results);

            Assert.That(
                results.Any(result => result.module == raycaster),
                Is.True);
            Assert.That(
                guard.IsPointerOverUi(
                    fixture.EventSystem,
                    pointerPosition),
                Is.True);
        }

        [Test]
        public void UiGuard_UsesGraphicRegistryWhenEventSystemGraphicReturnsNoResults()
        {
            EventSystem eventSystem =
                Create<EventSystem>("FallbackEventSystem");
            Canvas canvas = Create<Canvas>("FallbackCanvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<RectTransform>().sizeDelta =
                new Vector2(640f, 480f);
            NoResultsGraphicRaycaster raycaster =
                canvas.gameObject.AddComponent<NoResultsGraphicRaycaster>();
            RegisterHeadlessEditModeRaycaster(raycaster);
            GameObject target = NewObject("FallbackGraphicTarget");
            RectTransform rect = target.AddComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = target.AddComponent<Image>();
            image.raycastTarget = true;
            ForceCanvasLayout(canvas);

            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem)
            {
                position = ScreenCenter
            }, results);
            Assert.That(
                results.Any(result =>
                    result.module is GraphicRaycaster graphicRaycaster &&
                    graphicRaycaster.isActiveAndEnabled &&
                    graphicRaycaster.GetComponent<Canvas>()
                        .isActiveAndEnabled),
                Is.False);
            Assert.That(
                new GrayboxUiInputGuard3D().IsPointerOverUi(
                    eventSystem,
                    ScreenCenter),
                Is.True);
        }

        [Test]
        public void UiGuard_PhysicsRaycastResultDoesNotCapturePointerAsUi()
        {
            UiFixture fixture = CreateMenuFixture();
            Camera worldCamera = Create<Camera>("WorldPhysicsCamera");
            worldCamera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            worldCamera.transform.position = new Vector3(0f, 0f, -10f);
            PhysicsRaycaster physicsRaycaster =
                worldCamera.gameObject.AddComponent<PhysicsRaycaster>();
            RegisterHeadlessEditModeRaycaster(physicsRaycaster);
            GameObject worldTarget = NewObject("WorldCollider");
            worldTarget.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            var pointer = new PointerEventData(fixture.EventSystem)
            {
                position = ScreenCenter
            };
            var results = new List<RaycastResult>();
            fixture.EventSystem.RaycastAll(pointer, results);
            Assert.That(
                results.Any(result => result.module == physicsRaycaster),
                Is.True);
            Assert.That(
                results.Any(result => result.module is GraphicRaycaster),
                Is.False);

            Assert.That(
                new GrayboxUiInputGuard3D().IsPointerOverUi(
                    fixture.EventSystem,
                    ScreenCenter),
                Is.False);
        }

        [Test]
        public void Menu_ButtonsRaiseOnlyTheirFrozenCallbacks()
        {
            UiFixture fixture = CreateMenuFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                new RecordingPresentation());
            fixture.Menu.ShowEvacuation(new[] { instance });
            var cancelCount = 0;
            var cancelResolutionCount = 0;
            var itemCount = 0;
            var categoryCount = 0;
            var allCount = 0;
            var confirmationCount = 0;
            fixture.Menu.CancelSelectedConstructionRequested +=
                () => cancelCount++;
            fixture.Menu.CancelConstructionConfirmationResolved +=
                _ => cancelResolutionCount++;
            fixture.Menu.EvacuationItemTreatmentRequested +=
                (_, __) => itemCount++;
            fixture.Menu.EvacuationCategoryTreatmentRequested +=
                (_, __) => categoryCount++;
            fixture.Menu.EvacuationAllTreatmentRequested +=
                _ => allCount++;
            fixture.Menu.EvacuationConfirmationRequested +=
                () => confirmationCount++;

            int instanceCountBefore = fixture.Session.Instances.Count;
            Click(fixture.Canvas.transform, "Construction.Cancel");
            Click(fixture.Canvas.transform, "Construction.Confirm.Yes");
            Click(fixture.Canvas.transform, "Construction.Confirm.No");
            Click(
                fixture.Canvas.transform,
                "Evacuation.Item." + instance.StableInstanceId + ".Abandon");
            Click(
                fixture.Canvas.transform,
                "Evacuation.Category.Basic.FullDismantle");
            Click(
                fixture.Canvas.transform,
                "Evacuation.All.QuickDismantle");
            Click(fixture.Canvas.transform, "Evacuation.Confirm");

            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(cancelResolutionCount, Is.EqualTo(2));
            Assert.That(itemCount, Is.EqualTo(1));
            Assert.That(categoryCount, Is.EqualTo(1));
            Assert.That(allCount, Is.EqualTo(1));
            Assert.That(confirmationCount, Is.EqualTo(1));
            Assert.That(fixture.Session.Instances.Count, Is.EqualTo(
                instanceCountBefore));
        }

        [Test]
        public void Menu_RealPointerDispatchReachesEveryConstructionAction()
        {
            UiFixture fixture = CreateMenuFixture();
            var cancelCount = 0;
            var confirmedCount = 0;
            var declinedCount = 0;
            fixture.Menu.CancelSelectedConstructionRequested +=
                () => cancelCount++;
            fixture.Menu.CancelConstructionConfirmationResolved +=
                confirmed =>
                {
                    if (confirmed) confirmedCount++;
                    else declinedCount++;
                };

            Button cancel = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Cancel");
            Button yes = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Confirm.Yes");
            Button no = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Confirm.No");

            AssertReadableAndSeparate(cancel, yes, no);
            PointerClick(fixture, cancel);
            PointerClick(fixture, yes);
            PointerClick(fixture, no);

            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(confirmedCount, Is.EqualTo(1));
            Assert.That(declinedCount, Is.EqualTo(1));
        }

        [Test]
        public void Menu_RealPointerDispatchReachesAllEvacuationRowTreatments()
        {
            UiFixture fixture = CreateMenuFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                new RecordingPresentation());
            fixture.Menu.ShowEvacuation(new[] { instance });
            var treatments = new List<BuildingEvacuationTreatment>();
            fixture.Menu.EvacuationItemTreatmentRequested +=
                (stableId, treatment) =>
                {
                    Assert.That(
                        stableId,
                        Is.EqualTo(instance.StableInstanceId));
                    treatments.Add(treatment);
                };
            string prefix =
                "Evacuation.Item." + instance.StableInstanceId + ".";
            Button abandon = FindComponent<Button>(
                fixture.Canvas.transform,
                prefix + "Abandon");
            Button full = FindComponent<Button>(
                fixture.Canvas.transform,
                prefix + "FullDismantle");
            Button quick = FindComponent<Button>(
                fixture.Canvas.transform,
                prefix + "QuickDismantle");

            AssertReadableAndSeparate(abandon, full, quick);
            PointerClick(fixture, abandon);
            PointerClick(fixture, full);
            PointerClick(fixture, quick);

            Assert.That(
                treatments,
                Is.EqualTo(new[]
                {
                    BuildingEvacuationTreatment.Abandon,
                    BuildingEvacuationTreatment.FullDismantle,
                    BuildingEvacuationTreatment.QuickDismantle
                }));
        }

        [Test]
        public void Menu_CatalogFieldsAndHoverDetailsHaveReadableNonOverlappingRects()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);
            Assert.That(fixture.Menu.CatalogVisible, Is.True);
            Transform card = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Smelter.Id.Value);
            Transform name = FindTransform(card, "Name");
            Transform cost = FindTransform(card, "Cost");
            Transform reason = FindTransform(card, "PrimaryReason");
            Transform details = FindTransform(card, "Details");
            Assert.That(card, Is.Not.Null);
            Assert.That(name, Is.Not.Null);
            Assert.That(cost, Is.Not.Null);
            Assert.That(reason, Is.Not.Null);
            Assert.That(details, Is.Not.Null);

            var pointer = new PointerEventData(fixture.EventSystem);
            ExecuteEvents.Execute(
                card.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);
            ForceCanvasLayout(fixture.Canvas);

            Rect nameRect = ScreenRect(
                fixture,
                (RectTransform)name);
            Rect costRect = ScreenRect(
                fixture,
                (RectTransform)cost);
            Rect reasonRect = ScreenRect(
                fixture,
                (RectTransform)reason);
            Rect detailsRect = ScreenRect(
                fixture,
                (RectTransform)details);
            AssertReadable(nameRect, "name");
            AssertReadable(costRect, "cost");
            AssertReadable(reasonRect, "reason");
            AssertReadable(detailsRect, "details");
            AssertNoAreaOverlap(nameRect, costRect);
            AssertNoAreaOverlap(nameRect, reasonRect);
            AssertNoAreaOverlap(costRect, reasonRect);
            AssertNoAreaOverlap(detailsRect, nameRect);
            AssertNoAreaOverlap(detailsRect, costRect);
            AssertNoAreaOverlap(detailsRect, reasonRect);
        }

        [TestCase("core.building.smelter")]
        [TestCase("core.building.assembler")]
        public void Menu_HoverDetailsFitEveryPromisedFieldWithoutClipping(
            string stableBuildingId)
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);
            Assert.That(fixture.Menu.CatalogVisible, Is.True);
            BuildingDefinition definition = BuildingCatalog.BuildMenu.Single(
                candidate => candidate.Id.Value == stableBuildingId);
            GrayboxBuildingCatalogItem3D item =
                new GrayboxBuildingCatalogPresenter3D().Describe(
                    fixture.Session,
                    definition);
            Transform card = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + stableBuildingId);
            Transform details = FindTransform(card, "Details");
            Text detailsText = FindComponent<Text>(
                details,
                "Details.Text");
            Assert.That(item.Visibility, Is.EqualTo(
                BuildingCatalogVisibility.Locked));

            var pointer = new PointerEventData(fixture.EventSystem);
            ExecuteEvents.Execute(
                card.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);
            ForceCanvasLayout(fixture.Canvas);

            Assert.That(details.gameObject.activeSelf, Is.True);
            Assert.That(
                detailsText.preferredHeight,
                Is.LessThanOrEqualTo(
                    detailsText.rectTransform.rect.height + .01f),
                detailsText.text);
            Assert.That(
                detailsText.verticalOverflow,
                Is.EqualTo(VerticalWrapMode.Truncate));
            Assert.That(detailsText.text, Does.Contain(definition.Name));
            Assert.That(detailsText.text, Does.Contain("类别 生产"));
            Assert.That(detailsText.text, Does.Contain("路线 核心"));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "占地 " + definition.Width + "×" +
                    definition.Height));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "位置 " + BuildingMobilityRules.PlacementName(
                        definition.Placement)));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "施工 " + definition.BuildSeconds + " 秒"));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "完整成本 " + definition.Cost + " " +
                    definition.CostId));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "研究 " +
                    (definition.RequiredResearchId ?? "无")));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "前置 " +
                    (definition.RequiredBuildingId ?? "无")));
            Assert.That(detailsText.text, Does.Contain("锁定原因 "));
            foreach (string lockReason in item.LockReasons)
                Assert.That(detailsText.text, Does.Contain(lockReason));

            Rect detailsRect = ScreenRect(
                fixture,
                (RectTransform)details);
            Rect cardRect = ScreenRect(
                fixture,
                (RectTransform)card);
            Assert.That(
                RectContains(cardRect, detailsRect),
                Is.True,
                "card " + cardRect + " details " + detailsRect);
            Transform sibling = card.parent.GetChild(
                card.GetSiblingIndex() == 0 ? 1 :
                card.GetSiblingIndex() - 1);
            AssertNoAreaOverlap(
                cardRect,
                ScreenRect(fixture, (RectTransform)sibling));
        }

        [Test]
        public void Menu_CatalogScrollMakesFirstAndLastOfTenAndTwentyEightReachable()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.RefreshCatalog();
            ScrollRect scroll = FindComponent<ScrollRect>(
                fixture.Canvas.transform,
                "Catalog.Scroll");

            Assert.That(scroll, Is.Not.Null);
            Assert.That(
                scroll.viewport.GetComponent<RectMask2D>(),
                Is.Not.Null);
            Assert.That(scroll.content.childCount, Is.EqualTo(10));
            AssertScrollEndpointsReachable(fixture, scroll);

            fixture.Session.SetRouteContact(ContentRoute.Technology, true);
            fixture.Session.SetRouteContact(ContentRoute.Cultivation, true);
            fixture.Session.SetRouteContact(
                ContentRoute.BiologicalAscension,
                true);
            fixture.Session.SetRouteContact(ContentRoute.Psionics, true);
            fixture.Menu.RefreshCatalog();

            Assert.That(
                scroll.content.childCount,
                Is.EqualTo(
                    GrayboxBuildingCatalogPresenter3D.BuildMenuCount));
            AssertScrollEndpointsReachable(fixture, scroll);

            scroll.verticalNormalizedPosition = 1f;
            scroll.Rebuild(CanvasUpdate.PostLayout);
            ForceCanvasLayout(fixture.Canvas);
            Button first = FindComponent<Button>(
                fixture.Canvas.transform,
                "Catalog.Card." +
                BuildingCatalog.MiningStation.Id.Value);
            PointerClick(fixture, first);
            Assert.That(
                fixture.Interaction.Selected,
                Is.SameAs(BuildingCatalog.MiningStation));
        }

        [Test]
        public void ConstructionController_ZeroProgressCancelsImmediatelyWithRefund()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            int alloyAfterSpend = fixture.Session.Inventory.Get(
                BuildingCatalog.Housing.CostId);
            Assert.That(
                fixture.Controller.SelectInstance(instance.StableInstanceId),
                Is.True);

            ConstructionCancelResult result =
                fixture.Controller.RequestCancelSelected();

            Assert.That(result, Is.EqualTo(
                ConstructionCancelResult.Cancelled));
            Assert.That(fixture.Session.Instances, Is.Empty);
            Assert.That(
                fixture.Session.Inventory.Get(BuildingCatalog.Housing.CostId),
                Is.EqualTo(alloyAfterSpend + BuildingCatalog.Housing.Cost));
        }

        [Test]
        public void ConstructionController_ProgressRequiresConfirmationAndMenuRoutesIt()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            fixture.Controller.TickConstruction(1f);
            Assert.That(instance.Progress.Normalized, Is.GreaterThan(0f));
            Assert.That(
                fixture.Controller.SelectInstance(instance.StableInstanceId),
                Is.True);

            Click(fixture.Canvas.transform, "Construction.Cancel");
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CancelConfirmation));
            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
            Click(fixture.Canvas.transform, "Construction.Confirm.No");
            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));

            Click(fixture.Canvas.transform, "Construction.Cancel");
            Click(fixture.Canvas.transform, "Construction.Confirm.Yes");
            Assert.That(fixture.Session.Instances, Is.Empty);
        }

        [Test]
        public void ConstructionController_DirectRequestRejectsLockedZeroProgress()
        {
            ControllerFixture fixture = CreateControllerFixture();
            var routePresentation = new CancellationRoutePresentation();
            SetCancellationPresentation(
                fixture.Controller,
                routePresentation);
            GrayboxBuildingInstance3D instance = BeginGroundConstruction(
                fixture.Session,
                BuildingCatalog.Wall,
                20,
                15,
                fixture.Presentation);
            Assert.That(fixture.Controller.SelectInstance(
                instance.StableInstanceId), Is.True);
            LockForFullEvacuation(fixture.Session, instance);
            int inventory = fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId);
            uint revision = fixture.Session.CatalogRevision;
            int delegations = CancellationDelegationCount(fixture.Controller);

            ConstructionCancelResult result =
                fixture.Controller.RequestCancelSelected();

            Assert.That(result, Is.EqualTo(ConstructionCancelResult.NotFound));
            Assert.That(fixture.Session.Instances, Is.EqualTo(new[] { instance }));
            Assert.That(fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId), Is.EqualTo(inventory));
            Assert.That(instance.Progress.Remaining,
                Is.EqualTo(instance.Progress.BaseDuration));
            Assert.That(instance.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Session.CatalogRevision, Is.EqualTo(revision));
            Assert.That(CancellationDelegationCount(fixture.Controller),
                Is.EqualTo(delegations));
            Assert.That(routePresentation.TotalCalls, Is.Zero);
            Assert.That(SelectedStableInstanceId(fixture.Controller), Is.Null);
        }

        [Test]
        public void ConstructionController_ConfirmationCallbackRejectsNewlyLockedItem()
        {
            ControllerFixture fixture = CreateControllerFixture();
            var routePresentation = new CancellationRoutePresentation();
            SetCancellationPresentation(
                fixture.Controller,
                routePresentation);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            GrayboxBuildingInstance3D instance = BeginGroundConstruction(
                fixture.Session,
                BuildingCatalog.Wall,
                20,
                15,
                fixture.Presentation);
            fixture.Controller.TickConstruction(.5f);
            Assert.That(fixture.Controller.SelectInstance(
                instance.StableInstanceId), Is.True);
            Assert.That(fixture.Controller.RequestCancelSelected(),
                Is.EqualTo(ConstructionCancelResult.ConfirmationRequired));
            LockForFullEvacuation(fixture.Session, instance);
            float remaining = instance.Progress.Remaining;
            int inventory = fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId);
            uint revision = fixture.Session.CatalogRevision;
            int delegations = CancellationDelegationCount(fixture.Controller);

            bool cancelled = fixture.Controller.ResolveCancelSelected(true);

            Assert.That(cancelled, Is.False);
            Assert.That(fixture.Session.Instances, Is.EqualTo(new[] { instance }));
            Assert.That(fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId), Is.EqualTo(inventory));
            Assert.That(instance.Progress.Remaining, Is.EqualTo(remaining));
            Assert.That(instance.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Session.CatalogRevision, Is.EqualTo(revision));
            Assert.That(CancellationDelegationCount(fixture.Controller),
                Is.EqualTo(delegations));
            Assert.That(routePresentation.TotalCalls, Is.Zero);
            Assert.That(SelectedStableInstanceId(fixture.Controller), Is.Null);
            Assert.That(fixture.Interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
        }

        [Test]
        public void ConstructionController_UguiCancelEventRejectsLockedItem()
        {
            ControllerFixture fixture = CreateControllerFixture();
            var routePresentation = new CancellationRoutePresentation();
            SetCancellationPresentation(
                fixture.Controller,
                routePresentation);
            GrayboxBuildingInstance3D instance = BeginGroundConstruction(
                fixture.Session,
                BuildingCatalog.Wall,
                20,
                15,
                fixture.Presentation);
            Assert.That(fixture.Controller.SelectInstance(
                instance.StableInstanceId), Is.True);
            LockForFullEvacuation(fixture.Session, instance);
            int inventory = fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId);
            uint revision = fixture.Session.CatalogRevision;
            int delegations = CancellationDelegationCount(fixture.Controller);

            Click(fixture.Canvas.transform, "Construction.Cancel");

            Assert.That(fixture.Session.Instances, Is.EqualTo(new[] { instance }));
            Assert.That(fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId), Is.EqualTo(inventory));
            Assert.That(instance.Progress.Remaining,
                Is.EqualTo(instance.Progress.BaseDuration));
            Assert.That(instance.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Session.CatalogRevision, Is.EqualTo(revision));
            Assert.That(CancellationDelegationCount(fixture.Controller),
                Is.EqualTo(delegations));
            Assert.That(routePresentation.TotalCalls, Is.Zero);
            Assert.That(SelectedStableInstanceId(fixture.Controller), Is.Null);
        }

        [Test]
        public void ConstructionController_SelectAtResolvesColliderStableId()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            Transform visual = FindTransform(
                fixture.Presentation.transform,
                instance.StableInstanceId);
            fixture.Camera.transform.position =
                visual.position + Vector3.up * 10f;
            fixture.Camera.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);
            Physics.SyncTransforms();

            Assert.That(fixture.Controller.SelectAt(ScreenCenter), Is.True);
            Assert.That(
                fixture.Controller.RequestCancelSelected(),
                Is.EqualTo(ConstructionCancelResult.Cancelled));
        }

        [Test]
        public void ConstructionController_TickDelegatesExactlyTheRequestedDelta()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            float before = instance.Progress.Remaining;

            fixture.Controller.TickConstruction(.75f);

            Assert.That(
                before - instance.Progress.Remaining,
                Is.EqualTo(.75f).Within(.0001f));
        }

        [Test]
        public void ConstructionController_DestroyRemovesMenuListeners()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            fixture.Controller.TickConstruction(.5f);
            fixture.Controller.SelectInstance(instance.StableInstanceId);
            typeof(GrayboxConstructionController3D)
                .GetMethod(
                    "OnDestroy",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(fixture.Controller, null);
            UnityEngine.Object.DestroyImmediate(
                fixture.Controller.gameObject);

            Click(fixture.Canvas.transform, "Construction.Cancel");

            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
        }

        [Test]
        public void Menu_DestroyAndRecreateRemovesOwnedRootAndOldCallbacks()
        {
            UiFixture fixture = CreateMenuFixture();
            GrayboxBuildingMenuView3D oldMenu = fixture.Menu;
            Button oldCancel = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Cancel");
            var oldCallbackCount = 0;
            oldMenu.CancelSelectedConstructionRequested +=
                () => oldCallbackCount++;

            InvokeLifecycle(oldMenu, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(oldMenu.gameObject);

            Assert.That(oldCancel == null, Is.True);
            Assert.That(
                NamedTransforms(
                    fixture.Canvas.transform,
                    "GrayboxBuildingUi.Root").Count,
                Is.Zero);

            GrayboxBuildingMenuView3D replacement =
                Create<GrayboxBuildingMenuView3D>("ReplacementMenu");
            replacement.Configure(
                fixture.Canvas,
                fixture.EventSystem,
                fixture.Session,
                fixture.Interaction);
            Assert.That(
                NamedComponents<Button>(
                    fixture.Canvas.transform,
                    "QuickbarSlot.").Count,
                Is.EqualTo(10));
            PointerClick(
                fixture.WithMenu(replacement),
                FindComponent<Button>(
                    fixture.Canvas.transform,
                    "Construction.Cancel"));
            Assert.That(oldCallbackCount, Is.Zero);
        }

        [Test]
        public void ConstructionController_ReconfigureAfterDestroyedViewKeepsOneReplacementDelegate()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingMenuView3D oldMenu = fixture.Menu;
            UnityEngine.Object.DestroyImmediate(oldMenu.gameObject);
            GrayboxBuildingMenuView3D replacement =
                Create<GrayboxBuildingMenuView3D>("ReplacementMenu");
            replacement.Configure(
                fixture.Canvas,
                fixture.EventSystem,
                fixture.Session,
                fixture.Interaction);

            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.Presentation,
                fixture.Interaction,
                fixture.Camera,
                replacement);
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.Presentation,
                fixture.Interaction,
                fixture.Camera,
                replacement);

            Assert.That(
                EventSubscriberCount(
                    replacement,
                    "CancelSelectedConstructionRequested"),
                Is.EqualTo(1));
            Assert.That(
                EventSubscriberCount(
                    replacement,
                    "CancelConstructionConfirmationResolved"),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator
            ConstructionController_SerializedCloneLifecycleKeepsOneMenuDelegate()
        {
            yield return new EnterPlayMode();
            ControllerFixture fixture = CreateControllerFixture(true);
            yield return null;

            Assert.That(
                EventSubscriberCount(
                    fixture.Menu,
                    "CancelSelectedConstructionRequested"),
                Is.EqualTo(1));
            Assert.That(
                EventSubscriberCount(
                    fixture.Menu,
                    "CancelConstructionConfirmationResolved"),
                Is.EqualTo(1));

            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.Presentation,
                fixture.Interaction,
                fixture.Camera,
                fixture.Menu);
            yield return null;

            Assert.That(
                EventSubscriberCount(
                    fixture.Menu,
                    "CancelSelectedConstructionRequested"),
                Is.EqualTo(1));
            Assert.That(
                EventSubscriberCount(
                    fixture.Menu,
                    "CancelConstructionConfirmationResolved"),
                Is.EqualTo(1));
            yield return new ExitPlayMode();
        }

        [Test]
        public void ConstructionController_UpdateUsesScaledDeltaTime()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building/" +
                "GrayboxConstructionController3D.cs"));

            Assert.That(
                source,
                Does.Contain("TickConstruction(Time.deltaTime);"));
            Assert.That(
                source,
                Does.Not.Contain(
                    "TickConstruction(Time.unscaledDeltaTime);"));
        }

        private InputRouterFixture CreateInputRouterFixture()
        {
            UiFixture ui = CreateMenuFixture();
            Material worldMaterial = new Material(
                Shader.Find("Hidden/InternalErrorShader"));
            cleanup.Add(worldMaterial);
            GameObject worldObject = NewObject("InputWorld");
            Transform terrain =
                NewChildObject(worldObject.transform, "Terrain");
            Transform resources =
                NewChildObject(worldObject.transform, "Resources");
            Transform obstacles =
                NewChildObject(worldObject.transform, "Obstacles");
            GrayboxWorldView3D world =
                worldObject.AddComponent<GrayboxWorldView3D>();
            world.Configure(
                terrain,
                resources,
                obstacles,
                worldMaterial);
            world.Generate(new WorldMapModel(OpenInputCells()));

            GameObject cityObject = NewObject("InputCity");
            Assert.That(
                world.Coordinates.TryCellToWorld(
                    16,
                    12,
                    .5f,
                    out Vector3 cityPosition),
                Is.True);
            cityObject.transform.position = cityPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider bodyCollider =
                cityObject.AddComponent<BoxCollider>();
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, bodyCollider);

            Camera camera = Create<Camera>("InputWorldCamera");
            camera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 640f / 480f;
            PositionInputCameraAtCell(world, camera, 20, 15);

            GameObject innerObject = NewObject("InputInnerSurface");
            innerObject.transform.SetParent(cityObject.transform, false);
            BoxCollider inner = innerObject.AddComponent<BoxCollider>();
            inner.enabled = false;
            GrayboxBuildingSurfaceProjector3D projector =
                Create<GrayboxBuildingSurfaceProjector3D>(
                    "InputBuildingProjector");
            projector.Configure(camera, world, city, inner);

            GrayboxBuildingWorldView3D presentation =
                Create<GrayboxBuildingWorldView3D>(
                    "InputBuildingPresentation");
            Transform instances =
                NewChildObject(presentation.transform, "Instances");
            Transform infrastructure =
                NewChildObject(
                    presentation.transform,
                    "Infrastructure");
            Material buildingMaterial = new Material(
                Shader.Find("Hidden/InternalErrorShader"));
            cleanup.Add(buildingMaterial);
            presentation.Configure(
                instances,
                infrastructure,
                buildingMaterial,
                city);

            GrayboxBuildingPlacementController3D placement =
                Create<GrayboxBuildingPlacementController3D>(
                    "InputPlacement");
            placement.Configure(
                ui.Session,
                city,
                world,
                projector,
                presentation,
                ui.Interaction);
            GrayboxConstructionController3D construction =
                Create<GrayboxConstructionController3D>(
                    "InputConstruction");
            construction.Configure(
                ui.Session,
                city,
                presentation,
                ui.Interaction,
                camera,
                ui.Menu);
            GrayboxEvacuationController3D evacuation =
                Create<GrayboxEvacuationController3D>(
                    "InputEvacuation");
            evacuation.Configure(
                ui.Session,
                city,
                presentation,
                ui.Menu);
            GrayboxDeveloperModifierBootstrap3D developer =
                Create<GrayboxDeveloperModifierBootstrap3D>(
                    "InputDeveloper");
            developer.Configure(
                ui.Session,
                city,
                presentation,
                ui.Canvas);
            GrayboxBuildingInputRouter3D router =
                Create<GrayboxBuildingInputRouter3D>("BuildingInput");
            router.Configure(
                ui.Menu,
                ui.Interaction,
                placement,
                construction,
                evacuation,
                developer);
            EnsureInputDevices();
            SetPointer(ScreenCenter);
            ui.EventSystem.SetSelectedGameObject(null);
            InputSystemUIInputModule inputModule =
                ui.EventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule != null)
                inputModule.enabled = false;
            return new InputRouterFixture(
                ui,
                world,
                city,
                camera,
                presentation,
                placement,
                construction,
                evacuation,
                developer,
                router);
        }

        private void EnsureInputDevices()
        {
            if (inputTestFixture == null)
            {
                Type fixtureType = Type.GetType(
                    "UnityEngine.InputSystem.InputTestFixture, " +
                    "Unity.InputSystem.TestFramework",
                    true);
                inputTestFixture = Activator.CreateInstance(fixtureType);
                fixtureType.GetMethod("Setup")
                    .Invoke(inputTestFixture, null);
            }
            if (testKeyboard == null)
                testKeyboard = InputSystem.AddDevice<Keyboard>();
            if (testMouse == null)
                testMouse = InputSystem.AddDevice<Mouse>();
            testKeyboard.MakeCurrent();
            testMouse.MakeCurrent();
        }

        private GrayboxInputSuppression PressKey(
            GrayboxBuildingInputRouter3D router,
            Key key)
        {
            EnsureInputDevices();
            InputSystem.QueueStateEvent(
                testKeyboard,
                new KeyboardState(key));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(testKeyboard));
            Assert.That(
                testKeyboard[key].wasPressedThisFrame,
                Is.True,
                key.ToString());
            GrayboxInputSuppression suppression =
                router.ProcessCurrentInput();
            InputSystem.QueueStateEvent(
                testKeyboard,
                new KeyboardState());
            InputSystem.Update();
            return suppression;
        }

        private GrayboxInputSuppression PressMouse(
            GrayboxBuildingInputRouter3D router,
            MouseButton button)
        {
            EnsureInputDevices();
            InputSystem.QueueStateEvent(
                testMouse,
                new MouseState
                {
                    position = testPointer
                }.WithButton(button));
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(testMouse));
            Assert.That(
                ButtonFor(testMouse, button).wasPressedThisFrame,
                Is.True,
                button.ToString());
            GrayboxInputSuppression suppression =
                router.ProcessCurrentInput();
            InputSystem.QueueStateEvent(
                testMouse,
                new MouseState
                {
                    position = testPointer
                });
            InputSystem.Update();
            return suppression;
        }

        private GrayboxInputSuppression RouteMiddleInput(
            GrayboxBuildingInputRouter3D buildingRouter,
            GrayboxInputRouter baseRouter,
            Vector2 position,
            bool held)
        {
            QueueMouseState(position, held);
            GrayboxInputSuppression suppression =
                buildingRouter.ProcessCurrentInput();
            baseRouter.ProcessFrame(
                baseRouter.ReadCurrentFrame(),
                suppression);
            return suppression;
        }

        private void QueueMouseState(
            Vector2 position,
            bool middleHeld)
        {
            EnsureInputDevices();
            testPointer = position;
            MouseState state = new MouseState
            {
                position = position
            };
            if (middleHeld)
                state = state.WithButton(MouseButton.Middle);
            InputSystem.QueueStateEvent(testMouse, state);
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(testMouse));
        }

        private void SetPointer(Vector2 position)
        {
            EnsureInputDevices();
            testPointer = position;
            InputSystem.QueueStateEvent(
                testMouse,
                new MouseState
                {
                    position = position
                });
            InputSystem.Update();
        }

        private static UnityEngine.InputSystem.Controls.ButtonControl ButtonFor(
            Mouse mouse,
            MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return mouse.leftButton;
                case MouseButton.Right:
                    return mouse.rightButton;
                case MouseButton.Middle:
                    return mouse.middleButton;
                case MouseButton.Forward:
                    return mouse.forwardButton;
                case MouseButton.Back:
                    return mouse.backButton;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(button),
                        button,
                        null);
            }
        }

        private static WorldCell[,] OpenInputCells()
        {
            var result = new WorldCell[32, 24];
            for (var x = 0; x < result.GetLength(0); x++)
            for (var y = 0; y < result.GetLength(1); y++)
            {
                result[x, y] = new WorldCell(
                    TerrainKind.Wasteland,
                    null,
                    0,
                    WorldTraversalKind.Open);
            }
            return result;
        }

        private static void PrepareStableWallPreview(
            InputRouterFixture fixture)
        {
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            PositionInputCameraAtCell(fixture, 20, 15);
            fixture.Interaction.Select(BuildingCatalog.Wall);
        }

        private CountingGraphicRaycaster CreateCountingGraphicTarget(
            out GameObject target)
        {
            Canvas canvas = Create<Canvas>("CountingGraphicCanvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CountingGraphicRaycaster raycaster =
                canvas.gameObject.AddComponent<CountingGraphicRaycaster>();
            RegisterHeadlessEditModeRaycaster(raycaster);
            target = NewObject("CountingGraphicTarget");
            RectTransform rect =
                target.AddComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = target.AddComponent<Image>();
            image.raycastTarget = true;
            ForceCanvasLayout(canvas);
            return raycaster;
        }

        private static Transform NewChildObject(
            Transform parent,
            string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void PositionInputCameraAtCell(
            InputRouterFixture fixture,
            int x,
            int y)
        {
            PositionInputCameraAtCell(
                fixture.World,
                fixture.Camera,
                x,
                y);
        }

        private static void PositionInputCameraAtCell(
            GrayboxWorldView3D world,
            Camera camera,
            int x,
            int y)
        {
            Assert.That(
                world.Coordinates.TryCellToWorld(
                    x,
                    y,
                    0f,
                    out Vector3 point),
                Is.True);
            PositionInputCameraOver(camera, point);
        }

        private static void PositionInputCameraOver(
            Camera camera,
            Vector3 point)
        {
            camera.transform.position =
                new Vector3(point.x, point.y + 10f, point.z);
            camera.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);
            Physics.SyncTransforms();
        }

        private UiFixture CreateMenuFixture(
            bool cloneSerializedSource = false)
        {
            EventSystem eventSystem = Create<EventSystem>("EventSystem");
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            Canvas canvas = Create<Canvas>("Canvas");
            Camera uiCamera = Create<Camera>("MenuUiCamera");
            uiCamera.orthographic = true;
            uiCamera.transform.position = new Vector3(0f, 0f, -10f);
            uiCamera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            var target = new RenderTexture(640, 480, 0);
            cleanup.Add(target);
            uiCamera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            GraphicRaycaster raycaster =
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
            raycaster.enabled = true;
            canvas.GetComponent<RectTransform>().sizeDelta =
                new Vector2(640f, 480f);
            GrayboxBuildingSession3D session =
                Create<GrayboxBuildingSession3D>("Session");
            session.ConfigureDevelopmentFixture();
            GrayboxBuildingInteractionModel3D interaction =
                Create<GrayboxBuildingInteractionModel3D>("Interaction");
            GrayboxBuildingMenuView3D menu;
            if (cloneSerializedSource)
            {
                GameObject source = NewObject("SerializedMenuSource");
                GrayboxBuildingMenuView3D serializedMenu =
                    source.AddComponent<GrayboxBuildingMenuView3D>();
                serializedMenu.Configure(
                    canvas,
                    eventSystem,
                    session,
                    interaction);
                source.SetActive(false);
                GameObject clone = UnityEngine.Object.Instantiate(source);
                clone.name = "SerializedMenuClone";
                cleanup.Add(clone);
                clone.SetActive(true);
                menu = clone.GetComponent<GrayboxBuildingMenuView3D>();
            }
            else
            {
                menu = Create<GrayboxBuildingMenuView3D>("Menu");
                menu.Configure(canvas, eventSystem, session, interaction);
            }
            canvas.enabled = false;
            canvas.enabled = true;
            raycaster.enabled = false;
            raycaster.enabled = true;
            RegisterHeadlessEditModeRaycaster(raycaster);
            ForceCanvasLayout(canvas);
            uiCamera.Render();
            return new UiFixture(
                canvas,
                eventSystem,
                uiCamera,
                session,
                interaction,
                menu);
        }

        private ControllerFixture CreateControllerFixture(
            bool cloneSerializedSource = false)
        {
            UiFixture ui = CreateMenuFixture(cloneSerializedSource);
            GrayboxMobileCityController3D city =
                Create<GrayboxMobileCityController3D>("City");
            GrayboxBuildingWorldView3D presentation =
                Create<GrayboxBuildingWorldView3D>("Presentation");
            var instanceRoot = NewObject("Instances");
            instanceRoot.transform.SetParent(presentation.transform, false);
            var infrastructureRoot = NewObject("Infrastructure");
            infrastructureRoot.transform.SetParent(
                presentation.transform,
                false);
            var material = new Material(Shader.Find(
                "Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color"));
            cleanup.Add(material);
            presentation.Configure(
                instanceRoot.transform,
                infrastructureRoot.transform,
                material,
                city);
            Camera camera = Create<Camera>("Camera");
            camera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            GrayboxConstructionController3D controller;
            if (cloneSerializedSource)
            {
                GameObject source =
                    NewObject("SerializedConstructionSource");
                source.SetActive(false);
                GrayboxConstructionController3D serializedController =
                    source.AddComponent<GrayboxConstructionController3D>();
                serializedController.Configure(
                    ui.Session,
                    city,
                    presentation,
                    ui.Interaction,
                    camera,
                    ui.Menu);
                GameObject clone = UnityEngine.Object.Instantiate(source);
                clone.name = "SerializedConstructionClone";
                cleanup.Add(clone);
                clone.SetActive(true);
                controller =
                    clone.GetComponent<GrayboxConstructionController3D>();
            }
            else
            {
                controller =
                    Create<GrayboxConstructionController3D>("Construction");
                controller.Configure(
                    ui.Session,
                    city,
                    presentation,
                    ui.Interaction,
                    camera,
                    ui.Menu);
            }
            return new ControllerFixture(
                ui,
                city,
                presentation,
                camera,
                controller);
        }

        private GrayboxBuildingInstance3D BeginInnerConstruction(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation)
        {
            BuildingDefinition definition = BuildingCatalog.Housing;
            var request = new BuildingPlacementRequest(
                definition,
                session.InnerGrid,
                BuildingSite.InnerCity,
                BuildingOrientation.North,
                0,
                0,
                0,
                0,
                session.GroundBuildRadius,
                CityMode.Mobile,
                true,
                false,
                true,
                true,
                true,
                null,
                true,
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount),
                true);
            Assert.That(
                session.TryBeginConstruction(
                    request,
                    presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static GrayboxBuildingInstance3D BeginGroundConstruction(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            int x,
            int y,
            IGrayboxBuildingPresentation3D presentation)
        {
            var request = new BuildingPlacementRequest(
                definition, session.GroundGrid, BuildingSite.Ground,
                BuildingOrientation.North, x, y, 12, 12,
                session.GroundBuildRadius, CityMode.Fortress, true, false,
                true, true, !definition.RequiresResourceNode,
                definition.RequiresResourceNode ? "test.node" : null, true,
                BuildingUnlockModel.Evaluate(definition, session.Population,
                    session.IsResearchCompleted, session.CompletedBuildingCount),
                session.Inventory.CanSpend(definition.CostId, definition.Cost));
            Assert.That(session.TryBeginConstruction(request, presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation), Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static void LockForFullEvacuation(
            GrayboxBuildingSession3D session,
            GrayboxBuildingInstance3D instance)
        {
            var work = BuildingEvacuationRules.Create(
                instance.StableInstanceId,
                instance.Placement.Definition.Cost,
                instance.Progress.BaseDuration,
                instance.Progress.Remaining / instance.Progress.BaseDuration,
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string captureFailure),
                Is.True, captureFailure);
            Assert.That(session.TryLockEvacuationWork(
                new[] { work }, out string lockFailure),
                Is.True, lockFailure);
        }

        private static void SetCancellationPresentation(
            GrayboxConstructionController3D controller,
            IGrayboxBuildingPresentation3D presentation)
        {
            var field = typeof(GrayboxConstructionController3D).GetField(
                "cancellationPresentation",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "Controller must expose the cancellation delegation seam.");
            field.SetValue(controller, presentation);
        }

        private static int CancellationDelegationCount(
            GrayboxConstructionController3D controller)
        {
            var field = typeof(GrayboxConstructionController3D).GetField(
                "cancellationDelegationCount",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "Controller must count session cancellation delegations.");
            return (int)field.GetValue(controller);
        }

        private static string SelectedStableInstanceId(
            GrayboxConstructionController3D controller)
        {
            var field = typeof(GrayboxConstructionController3D).GetField(
                "selectedStableInstanceId",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (string)field.GetValue(controller);
        }

        private T Create<T>(string name) where T : Component
        {
            GameObject gameObject = NewObject(name);
            return gameObject.AddComponent<T>();
        }

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            cleanup.Add(gameObject);
            return gameObject;
        }

        private static void Click(Transform root, string name)
        {
            Button button = FindComponent<Button>(root, name);
            Assert.That(button, Is.Not.Null, "Missing button " + name);
            button.onClick.Invoke();
        }

        private static void PointerClick(UiFixture fixture, Button button)
        {
            Assert.That(button, Is.Not.Null);
            ForceCanvasLayout(fixture.Canvas);
            Vector2 position = RectCenter(fixture, button);
            var pointer = new PointerEventData(fixture.EventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = position
            };
            var results = new List<RaycastResult>();
            var directResults = new List<RaycastResult>();
            button.GetComponentInParent<GraphicRaycaster>().Raycast(
                pointer,
                directResults);
            Assert.That(
                directResults,
                Is.Not.Empty,
                "Direct GraphicRaycaster missed " + position +
                " in " + fixture.UiCamera.pixelRect);
            fixture.EventSystem.RaycastAll(pointer, results);
            Assert.That(results, Is.Not.Empty);
            RaycastResult hit = results.First(result =>
                result.gameObject != null &&
                result.gameObject.activeInHierarchy);
            Assert.That(hit.gameObject, Is.Not.Null);
            GameObject resolved =
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                    hit.gameObject);
            Assert.That(
                resolved,
                Is.EqualTo(button.gameObject),
                "The top real pointer hit must resolve to the expected button.");
            GameObject handled = ExecuteEvents.ExecuteHierarchy(
                hit.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
            Assert.That(handled, Is.EqualTo(button.gameObject));
        }

        private static void AssertReadableAndSeparate(params Button[] buttons)
        {
            Assert.That(buttons, Has.All.Not.Null);
            Canvas canvas = buttons[0].GetComponentInParent<Canvas>();
            ForceCanvasLayout(canvas);
            var rects = buttons
                .Select(button => ScreenRect(
                    canvas,
                    (RectTransform)button.transform))
                .ToArray();
            for (var index = 0; index < rects.Length; index++)
            {
                AssertReadable(rects[index]);
                for (var other = index + 1;
                     other < rects.Length;
                     other++)
                    Assert.That(
                        OverlapArea(rects[index], rects[other]),
                        Is.EqualTo(0f).Within(.01f));
            }
        }

        private static void AssertReadable(Rect rect)
        {
            AssertReadable(rect, "control");
        }

        private static void AssertReadable(Rect rect, string label)
        {
            Assert.That(
                rect.width,
                Is.GreaterThanOrEqualTo(40f),
                label + " width " + rect);
            Assert.That(
                rect.height,
                Is.GreaterThanOrEqualTo(12f),
                label + " height " + rect);
        }

        private static void AssertNoAreaOverlap(Rect left, Rect right)
        {
            Assert.That(
                OverlapArea(left, right),
                Is.EqualTo(0f).Within(.01f));
        }

        private static void AssertScrollEndpointsReachable(
            UiFixture fixture,
            ScrollRect scroll)
        {
            scroll.StopMovement();
            ForceCanvasLayout(fixture.Canvas);
            scroll.verticalNormalizedPosition = 1f;
            scroll.Rebuild(CanvasUpdate.PostLayout);
            ForceCanvasLayout(fixture.Canvas);
            Rect topViewport = ScreenRect(fixture, scroll.viewport);
            Rect first = ScreenRect(
                fixture,
                (RectTransform)scroll.content.GetChild(0));
            Assert.That(
                RectContains(topViewport, first),
                Is.True,
                "viewport " + topViewport + " first " + first);

            scroll.verticalNormalizedPosition = 0f;
            scroll.Rebuild(CanvasUpdate.PostLayout);
            ForceCanvasLayout(fixture.Canvas);
            Rect bottomViewport = ScreenRect(fixture, scroll.viewport);
            Rect last = ScreenRect(
                fixture,
                (RectTransform)scroll.content.GetChild(
                    scroll.content.childCount - 1));
            Assert.That(
                RectContains(bottomViewport, last),
                Is.True,
                "viewport " + bottomViewport + " last " + last);
        }

        private static float OverlapArea(Rect left, Rect right)
        {
            float width = Mathf.Max(
                0f,
                Mathf.Min(left.xMax, right.xMax) -
                Mathf.Max(left.xMin, right.xMin));
            float height = Mathf.Max(
                0f,
                Mathf.Min(left.yMax, right.yMax) -
                Mathf.Max(left.yMin, right.yMin));
            return width * height;
        }

        private static bool RectContains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - .01f &&
                   inner.yMin >= outer.yMin - .01f &&
                   inner.xMax <= outer.xMax + .01f &&
                   inner.yMax <= outer.yMax + .01f;
        }

        private static void ForceCanvasLayout(Canvas canvas)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutGroup[] groups =
                    canvas.GetComponentsInChildren<LayoutGroup>(true);
                for (var index = groups.Length - 1;
                     index >= 0;
                     index--)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(
                        (RectTransform)groups[index].transform);
            }
            Canvas.ForceUpdateCanvases();
            if (canvas.worldCamera != null &&
                canvas.worldCamera.targetTexture != null)
                canvas.worldCamera.Render();
        }

        private static Vector2 RectCenter(
            UiFixture fixture,
            Button button)
        {
            RectTransform rect = (RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(
                fixture.UiCamera,
                rect.TransformPoint(rect.rect.center));
        }

        private static Rect ScreenRect(
            UiFixture fixture,
            RectTransform rect)
        {
            return ScreenRect(fixture.Canvas, rect);
        }

        private static Rect ScreenRect(
            Canvas canvas,
            RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Camera camera = canvas.renderMode ==
                            RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 minimum =
                RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 maximum =
                RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(
                Mathf.Min(minimum.x, maximum.x),
                Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x),
                Mathf.Max(minimum.y, maximum.y));
        }

        private static int EventSubscriberCount(
            GrayboxBuildingMenuView3D menu,
            string eventName)
        {
            var field = typeof(GrayboxBuildingMenuView3D).GetField(
                eventName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            var callback = (Delegate)field.GetValue(menu);
            return callback == null
                ? 0
                : callback.GetInvocationList().Length;
        }

        private static void InvokeLifecycle(
            MonoBehaviour behaviour,
            string methodName)
        {
            typeof(GrayboxBuildingMenuView3D)
                .GetMethod(
                    methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(behaviour, null);
        }

        private static string BuildDetails(GrayboxBuildingCatalogItem3D item)
        {
            return (string)typeof(GrayboxBuildingMenuView3D).GetMethod(
                "BuildDetails",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic).Invoke(
                null,
                new object[] { item });
        }

        private static void RegisterHeadlessEditModeRaycaster(
            BaseRaycaster raycaster)
        {
            Type manager = typeof(BaseRaycaster).Assembly.GetType(
                "UnityEngine.EventSystems.RaycasterManager");
            manager.GetMethod(
                    "AddRaycaster",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { raycaster });
        }

        private static List<T> NamedComponents<T>(
            Transform root,
            string prefix) where T : Component
        {
            return root.GetComponentsInChildren<T>(true)
                .Where(component => component.name.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
                .ToList();
        }

        private static List<Transform> NamedTransforms(
            Transform root,
            string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == name)
                .ToList();
        }

        private static T FindComponent<T>(
            Transform root,
            string name) where T : Component
        {
            Transform transform = FindTransform(root, name);
            return transform == null ? null : transform.GetComponent<T>();
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
                if (transforms[index].name == name)
                    return transforms[index];
            return null;
        }

        private static string AllText(Transform root)
        {
            return string.Join(
                "\n",
                root.GetComponentsInChildren<Text>(true)
                    .Select(text => text.text)
                    .ToArray());
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
            }
        }

        private sealed class CancellationRoutePresentation :
            IGrayboxBuildingPresentation3D
        {
            public int TotalCalls { get; private set; }

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                TotalCalls++;
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                TotalCalls++;
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                TotalCalls++;
            }
        }

        private sealed class NoResultsGraphicRaycaster : GraphicRaycaster
        {
            public override void Raycast(
                PointerEventData eventData,
                List<RaycastResult> resultAppendList)
            {
            }
        }

        private sealed class CountingGraphicRaycaster : GraphicRaycaster
        {
            public int RaycastCalls { get; private set; }

            public override void Raycast(
                PointerEventData eventData,
                List<RaycastResult> resultAppendList)
            {
                RaycastCalls++;
                base.Raycast(eventData, resultAppendList);
            }
        }

        private sealed class InputRouterFixture
        {
            public InputRouterFixture(
                UiFixture ui,
                GrayboxWorldView3D world,
                GrayboxMobileCityController3D city,
                Camera camera,
                GrayboxBuildingWorldView3D presentation,
                GrayboxBuildingPlacementController3D placement,
                GrayboxConstructionController3D construction,
                GrayboxEvacuationController3D evacuation,
                GrayboxDeveloperModifierBootstrap3D developer,
                GrayboxBuildingInputRouter3D router)
            {
                Ui = ui;
                World = world;
                City = city;
                Camera = camera;
                Presentation = presentation;
                Placement = placement;
                Construction = construction;
                Evacuation = evacuation;
                Developer = developer;
                Router = router;
            }

            public UiFixture Ui { get; }
            public Canvas Canvas => Ui.Canvas;
            public EventSystem EventSystem => Ui.EventSystem;
            public GrayboxBuildingSession3D Session => Ui.Session;
            public GrayboxBuildingInteractionModel3D Interaction =>
                Ui.Interaction;
            public GrayboxWorldView3D World { get; }
            public GrayboxMobileCityController3D City { get; }
            public Camera Camera { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public GrayboxBuildingPlacementController3D Placement { get; }
            public GrayboxConstructionController3D Construction { get; }
            public GrayboxEvacuationController3D Evacuation { get; }
            public GrayboxDeveloperModifierBootstrap3D Developer { get; }
            public GrayboxBuildingInputRouter3D Router { get; }
        }

        private sealed class UiFixture
        {
            public UiFixture(
                Canvas canvas,
                EventSystem eventSystem,
                Camera uiCamera,
                GrayboxBuildingSession3D session,
                GrayboxBuildingInteractionModel3D interaction,
                GrayboxBuildingMenuView3D menu)
            {
                Canvas = canvas;
                EventSystem = eventSystem;
                UiCamera = uiCamera;
                Session = session;
                Interaction = interaction;
                Menu = menu;
            }

            public Canvas Canvas { get; }
            public EventSystem EventSystem { get; }
            public Camera UiCamera { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingInteractionModel3D Interaction { get; }
            public GrayboxBuildingMenuView3D Menu { get; }

            public UiFixture WithMenu(
                GrayboxBuildingMenuView3D replacement)
            {
                return new UiFixture(
                    Canvas,
                    EventSystem,
                    UiCamera,
                    Session,
                    Interaction,
                    replacement);
            }
        }

        private sealed class ControllerFixture
        {
            public ControllerFixture(
                UiFixture ui,
                GrayboxMobileCityController3D city,
                GrayboxBuildingWorldView3D presentation,
                Camera camera,
                GrayboxConstructionController3D controller)
            {
                Canvas = ui.Canvas;
                EventSystem = ui.EventSystem;
                Session = ui.Session;
                Interaction = ui.Interaction;
                Menu = ui.Menu;
                City = city;
                Presentation = presentation;
                Camera = camera;
                Controller = controller;
            }

            public Canvas Canvas { get; }
            public EventSystem EventSystem { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingInteractionModel3D Interaction { get; }
            public GrayboxBuildingMenuView3D Menu { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public Camera Camera { get; }
            public GrayboxConstructionController3D Controller { get; }
        }
    }
}
