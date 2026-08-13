using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxUsabilityRuntimeSceneTests
    {
        private const string SceneName = "GrayboxPrototype3D";

        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator LoadGrayboxScene()
        {
            previousTimeScale = Time.timeScale;
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.timeScale = 1f;
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
            Time.timeScale = 1f;
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            if (graybox.IsValid() && graybox.isLoaded)
            {
                Scene empty = SceneManager.CreateScene(
                    "GrayboxUsabilityRuntimeEmpty");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(graybox);
            }
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added)
                InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior =
                previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                previousEditorInputBehavior;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0007_RealEscapeCancelsPreviewThenOpensMenu()
        {
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxSystemMenuController3D menu =
                Object.FindObjectOfType<
                    GrayboxSystemMenuController3D>();
            GrayboxUsabilityInputCoordinator3D coordinator =
                Object.FindObjectOfType<
                    GrayboxUsabilityInputCoordinator3D>();
            GrayboxInputRouter baseRouter =
                Object.FindObjectOfType<GrayboxInputRouter>();

            Assert.That(interaction, Is.Not.Null);
            Assert.That(menu, Is.Not.Null);
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(baseRouter, Is.Not.Null);

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit1);
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Previewing));

            yield return TapKey(Key.Escape);
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(menu.IsOpen, Is.False);

            yield return TapKey(Key.Escape);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            yield return TapKey(Key.Escape);
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator IDEA0007_RealEscapeClosesCatalogThenOpensMenu()
        {
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxSystemMenuController3D menu =
                Object.FindObjectOfType<
                    GrayboxSystemMenuController3D>();

            yield return TapKey(Key.B);
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            yield return TapKey(Key.Escape);
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(menu.IsOpen, Is.False);
            yield return TapKey(Key.Escape);
            Assert.That(menu.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator IDEA0007_MenuSuppressesGameplayAndRestoresSpeed()
        {
            GrayboxSystemMenuController3D menu =
                Object.FindObjectOfType<
                    GrayboxSystemMenuController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            GrayboxCameraController3D cameraController =
                Object.FindObjectOfType<
                    GrayboxCameraController3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxLeaderController3D leader =
                Object.FindObjectOfType<GrayboxLeaderController3D>();
            Vector3 cityBefore = city.transform.position;
            Vector3 leaderBefore = leader.transform.position;

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit2);
            yield return MoveToInnerCell(city, 3, 2);
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            yield return ClickMouse(MouseButton.Left);
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            GrayboxBuildingInstance3D construction = session.Instances[0];
            Assert.That(construction.State,
                Is.EqualTo(
                    GrayboxBuildingInstanceState.UnderConstruction));

            yield return TapKey(Key.Escape);
            Assert.That(interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            yield return TapKey(Key.Escape);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            float constructionRemaining =
                construction.Progress.Remaining;

            yield return HoldPausedKey(Key.W, 2);
            Assert.That(
                city.transform.position,
                Is.EqualTo(cityBefore));
            Assert.That(
                leader.transform.position,
                Is.EqualTo(leaderBefore));
            Assert.That(
                construction.Progress.Remaining,
                Is.EqualTo(constructionRemaining));
            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            yield return ClickMouse(MouseButton.Right);
            Assert.That(city.AutopilotActive, Is.False);
            yield return DragMouse();
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            yield return TapKey(Key.Home);
            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));

            yield return ClickButton("Main.Continue");
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator IDEA0007_SettingsAndQuitButtonsUseInjectedSeams()
        {
            GrayboxSystemMenuController3D menu =
                Object.FindObjectOfType<
                    GrayboxSystemMenuController3D>();
            GrayboxSystemMenuView3D view =
                Object.FindObjectOfType<GrayboxSystemMenuView3D>();
            var platform = new FakeDisplayPlatform();
            var store = new FakeDisplayStore();
            var exit = new FakeExit();
            var speed = new GameSpeedModel();
            speed.Set(2f);
            menu.Configure(
                speed,
                new GrayboxDisplaySettingsModel3D(platform, store),
                exit,
                view);

            yield return TapKey(Key.Escape);
            yield return ClickButton("Main.Settings");
            Assert.That(menu.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Settings));
            yield return ClickButton("Settings.Apply");
            Assert.That(platform.ApplyCount, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Version,
                Is.EqualTo(GrayboxDisplaySettingsModel3D.CurrentVersion));
            yield return ClickButton("Settings.Cancel");
            yield return ClickButton("Main.Quit");
            Assert.That(menu.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.ExitConfirm));
            yield return ClickButton("Exit.Confirm");
            Assert.That(exit.Count, Is.EqualTo(1));
            Assert.That(Application.isPlaying, Is.True);
            menu.Close();
            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        [UnityTest]
        public IEnumerator IDEA0007_ManifestEscapeCancelsButProcessingConsumes()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxEvacuationController3D evacuation =
                Object.FindObjectOfType<GrayboxEvacuationController3D>();
            GrayboxSystemMenuController3D menu =
                Object.FindObjectOfType<
                    GrayboxSystemMenuController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            modifier.AddResource(BuildingCatalog.Housing.CostId, 1000);
            modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast100);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            interaction.Select(BuildingCatalog.Housing);
            yield return MoveToValidGround(placement);
            yield return ClickMouse(MouseButton.Left);
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            GrayboxBuildingInstance3D ground = session.Instances[0];
            float completionDeadline = Time.realtimeSinceStartup + 2f;
            while (ground.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < completionDeadline)
                yield return null;
            Assert.That(ground.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));

            yield return TapKey(Key.F);
            Assert.That(evacuation.IsManifestOpen, Is.True);
            yield return TapKey(Key.Escape);
            Assert.That(evacuation.IsManifestOpen, Is.False);
            Assert.That(menu.IsOpen, Is.False);

            yield return TapKey(Key.F);
            yield return ClickButton(
                "Evacuation.Item." + ground.StableInstanceId +
                ".FullDismantle");
            yield return ClickButton("Evacuation.Confirm");
            Assert.That(evacuation.IsProcessing, Is.True);
            yield return TapKey(Key.Escape);
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(ground.IsEvacuationLocked, Is.True);

            menu.Open();
            Assert.That(Time.timeScale, Is.Zero);
            for (var frame = 0; frame < 10; frame++)
                yield return null;
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(session.Instances.Contains(ground), Is.True);
            menu.Close();
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(key));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True);
            yield return null;
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator HoldKey(Key key, int fixedSteps)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(key));
            InputSystem.Update();
            yield return null;
            for (var index = 0; index < fixedSteps; index++)
                yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator HoldPausedKey(Key key, int frames)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(key));
            InputSystem.Update();
            for (var index = 0; index < frames; index++)
                yield return null;
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator ClickMouse(MouseButton button)
        {
            Vector2 position = mouse.position.ReadValue();
            QueueMouse(position, button);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator DragMouse()
        {
            Vector2 start = new Vector2(
                Screen.width * .5f,
                Screen.height * .5f);
            QueueMouse(start, MouseButton.Middle);
            yield return null;
            QueueMouse(start + new Vector2(100f, 40f),
                MouseButton.Middle);
            yield return null;
            QueueMouse(start + new Vector2(100f, 40f));
            yield return null;
        }

        private IEnumerator ClickButton(string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            var eventData = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(
                button.gameObject,
                eventData,
                ExecuteEvents.submitHandler);
            yield return null;
        }

        private IEnumerator MoveToValidGround(
            GrayboxBuildingPlacementController3D placement)
        {
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            for (var x = 0; x < world.Model.Width; x++)
            for (var y = 0; y < world.Model.Height; y++)
            {
                if (!world.Coordinates.TryCellToWorld(
                        x,
                        y,
                        0f,
                        out Vector3 corner))
                    continue;
                Vector2 point = Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f));
                QueueMouse(point);
                yield return null;
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.IsValid)
                    yield break;
            }
            Assert.Fail("No valid ground placement found.");
        }

        private IEnumerator MoveToInnerCell(
            GrayboxMobileCityController3D city,
            int x,
            int y)
        {
            Transform platform = city.transform.Find("InnerCityPlatform");
            Assert.That(platform, Is.Not.Null);
            BoxCollider surface = platform.GetComponent<BoxCollider>();
            Assert.That(surface, Is.Not.Null);
            Vector3 worldPoint = city.transform.TransformPoint(new Vector3(
                -1.28f + (x + .5f) * .32f,
                0f,
                -.96f + (y + .5f) * .32f));
            worldPoint.y = surface.bounds.max.y;
            QueueMouse(Camera.main.WorldToScreenPoint(worldPoint));
            yield return null;
        }

        private void QueueMouse(
            Vector2 position,
            MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue)
                state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static Button FindButton(string name)
        {
            Button[] buttons = Object.FindObjectsOfType<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
                if (buttons[index].name == name)
                    return buttons[index];
            return null;
        }

        private sealed class FakeDisplayPlatform :
            IGrayboxDisplaySettingsPlatform
        {
            private static readonly IReadOnlyList<
                GrayboxDisplayResolution3D> Resolutions =
                new[]
                {
                    new GrayboxDisplayResolution3D(1280, 720),
                    new GrayboxDisplayResolution3D(1920, 1080)
                };

            public IReadOnlyList<GrayboxDisplayResolution3D>
                AvailableResolutions => Resolutions;
            public GrayboxDisplaySettings3D Current =>
                new GrayboxDisplaySettings3D(
                    1280,
                    720,
                    GrayboxWindowMode3D.Windowed);
            public int ApplyCount { get; private set; }

            public bool TryApply(GrayboxDisplaySettings3D settings)
            {
                ApplyCount++;
                return true;
            }
        }

        private sealed class FakeDisplayStore :
            IGrayboxDisplaySettingsStore
        {
            public int SaveCount { get; private set; }
            public int Version { get; private set; }

            public bool TryLoad(
                out int version,
                out GrayboxDisplaySettings3D settings)
            {
                version = 0;
                settings = default;
                return false;
            }

            public void Save(
                int version,
                GrayboxDisplaySettings3D settings)
            {
                SaveCount++;
                Version = version;
            }
        }

        private sealed class FakeExit : IGrayboxApplicationExit
        {
            public int Count { get; private set; }
            public void Exit() => Count++;
        }
    }
}
