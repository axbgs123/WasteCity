using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
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
            GrayboxFormalPlayModeEntryFixture.BeginIsolatedStore();
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(mouse);
        }

        [UnityTearDown]
        public IEnumerator UnloadGrayboxScene()
        {
            Time.timeScale = 1f;
            try
            {
                Scene graybox = SceneManager.GetSceneByName(SceneName);
                if (graybox.IsValid() && graybox.isLoaded)
                {
                    Scene empty = SceneManager.CreateScene(
                        "GrayboxUsabilityRuntimeEmpty");
                    SceneManager.SetActiveScene(empty);
                    yield return SceneManager.UnloadSceneAsync(graybox);
                }
            }
            finally
            {
                try
                {
                    GrayboxFormalPlayModeEntryFixture.CleanupIsolatedStore();
                }
                finally
                {
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
                    GrayboxFormalPlayModeEntryFixture
                        .AssertRealSaveFilesUnchanged();
                }
            }
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
        public IEnumerator IDEA0017_RealSpeedButtonsShareFormalCommands()
        {
            GrayboxFormalSaveRuntimeHost3D host =
                Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);

            yield return ClickButton("Speed.2x");
            Assert.That(host.Speed.RequestedSpeed, Is.EqualTo(2f));
            Assert.That(host.Speed.Speed, Is.EqualTo(2f));
            Assert.That(Time.timeScale, Is.EqualTo(2f));

            yield return ClickButton("Speed.Pause");
            Assert.That(host.Speed.IsPaused(GamePauseReason.User), Is.True);
            Assert.That(host.Speed.Speed, Is.Zero);
            Assert.That(Time.timeScale, Is.Zero);

            yield return ClickButton("Speed.1x");
            Assert.That(host.Speed.IsPaused(GamePauseReason.User), Is.False);
            Assert.That(host.Speed.RequestedSpeed, Is.EqualTo(1f));
            Assert.That(host.Speed.Speed, Is.EqualTo(1f));
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
            Vector3 cityBefore = city.transform.position;
            Vector3 leaderBefore = leader.transform.position;
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
            GrayboxFormalSaveRuntimeHost3D host =
                Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            GameSpeedModel speed = host.Speed;
            speed.Set(2f);
            menu.Configure(
                speed,
                new GrayboxDisplaySettingsModel3D(platform, store),
                exit,
                view);
            GrayboxDisplaySettings3D expected =
                new GrayboxDisplaySettings3D(
                    1920,
                    1080,
                    GrayboxWindowMode3D.FullScreenWindow);

            yield return TapKey(Key.Escape);
            yield return ClickButton("Main.Settings");
            Assert.That(menu.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Settings));
            yield return SelectDropdownOption(
                "Settings.Resolution",
                "1920×1080");
            Assert.That(
                menu.Settings.Staged,
                Is.EqualTo(new GrayboxDisplaySettings3D(
                    1920,
                    1080,
                    GrayboxWindowMode3D.Windowed)));
            yield return SelectDropdownOption(
                "Settings.WindowMode",
                "FullScreenWindow");
            Assert.That(menu.Settings.Staged, Is.EqualTo(expected));
            yield return ClickButton("Settings.Apply");
            Assert.That(platform.ApplyCount, Is.EqualTo(1));
            Assert.That(platform.LastApplied, Is.EqualTo(expected));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Version,
                Is.EqualTo(GrayboxDisplaySettingsModel3D.CurrentVersion));
            Assert.That(store.Settings, Is.EqualTo(expected));
            yield return ClickButton("Settings.Cancel");
            yield return ClickButton("Main.Settings");
            Assert.That(menu.Settings.Staged, Is.EqualTo(expected));
            Assert.That(
                FindDropdown("Settings.Resolution").options[
                    FindDropdown("Settings.Resolution").value].text,
                Is.EqualTo("1920×1080"));
            Assert.That(
                FindDropdown("Settings.WindowMode").options[
                    FindDropdown("Settings.WindowMode").value].text,
                Is.EqualTo("FullScreenWindow"));
            yield return ClickButton("Settings.Cancel");
            yield return ClickButton("Main.Quit");
            Assert.That(menu.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.ExitConfirm));
            yield return ClickButton("Exit.Cancel");
            Assert.That(exit.Count, Is.Zero);
            Assert.That(menu.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Main));
            yield return ClickButton("Main.Quit");
            Assert.That(
                FindButton("Exit.QuitWithoutSaving").gameObject
                    .activeInHierarchy,
                Is.False,
                "The destructive fallback stays hidden while the formal " +
                "save path is healthy.");
            yield return ClickButton("Exit.SaveAndQuit");
            Assert.That(exit.Count, Is.EqualTo(1));
            Assert.That(Application.isPlaying, Is.True);
            yield return ClickButton("Exit.Cancel");
            yield return ClickButton("Main.Continue");
            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        [UnityTest]
        public IEnumerator IDEA0007_IDEA0014_ManifestEscapeCancelsButProcessingOpensSystemPause()
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
            Assert.That(
                modifier.SetResource(BuildingCatalog.Housing.CostId, 100),
                Is.True);
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
            Assert.That(modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Normal), Is.True);

            yield return TapKey(Key.F);
            Assert.That(evacuation.IsManifestOpen, Is.True);
            yield return TapKey(Key.Escape);
            Assert.That(evacuation.IsManifestOpen, Is.False);
            Assert.That(menu.IsOpen, Is.False);

            yield return TapKey(Key.F);
            yield return ClickButton(
                "Evacuation.Item." + ground.StableInstanceId +
                ".FullDismantle");
            EvacuationManifestViewModel manifestView =
                evacuation.CaptureManifestView();
            Assert.That(
                manifestView.Items.Single().Treatment,
                Is.EqualTo(BuildingEvacuationTreatment.FullDismantle));
            Assert.That(
                manifestView.CanConfirm,
                Is.True,
                manifestView.FailureReason);
            Assert.That(
                FindButton("Evacuation.Confirm").interactable,
                Is.True);
            yield return ClickButton("Evacuation.Confirm");
            Assert.That(evacuation.IsProcessing, Is.True);
            yield return TapKey(Key.Escape);
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(ground.IsEvacuationLocked, Is.True);
            EvacuationQueueViewModel pausedQueue =
                evacuation.CaptureQueueView();
            Assert.That(pausedQueue.IsPaused, Is.True);
            float remainingBeforePause = pausedQueue.RemainingActualSeconds;

            for (var frame = 0; frame < 10; frame++)
                yield return null;
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(session.Instances.Contains(ground), Is.True);
            Assert.That(
                evacuation.CaptureQueueView().RemainingActualSeconds,
                Is.EqualTo(remainingBeforePause).Within(.0001f));
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
            yield return ClickUiElement(button.gameObject);
        }

        private IEnumerator SelectDropdownOption(
            string dropdownName,
            string optionLabel)
        {
            Dropdown dropdown = FindDropdown(dropdownName);
            Assert.That(dropdown, Is.Not.Null, dropdownName);
            yield return ClickUiElement(dropdown.gameObject);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Toggle option = Object.FindObjectsOfType<Toggle>(true)
                .FirstOrDefault(value =>
                    value.gameObject.activeInHierarchy &&
                    value.GetComponentInChildren<Text>(true)?.text ==
                    optionLabel);
            string activeOptions = string.Join(
                "|",
                Object.FindObjectsOfType<Toggle>(true)
                    .Where(value => value.gameObject.activeInHierarchy)
                    .Select(value =>
                        value.GetComponentInChildren<Text>(true)?.text ??
                        "<no text>"));
            Assert.That(
                option,
                Is.Not.Null,
                optionLabel + "; active options=" + activeOptions +
                "; dropdown value=" + dropdown.value);
            yield return ClickUiElement(option.gameObject);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(
                Object.FindObjectsOfType<Toggle>(true).Any(value =>
                    value.gameObject.activeInHierarchy &&
                    value.GetComponentInChildren<Text>(true)?.text ==
                    optionLabel),
                Is.False,
                dropdownName + " did not close after selection.");
        }

        private IEnumerator ClickUiElement(GameObject target)
        {
            InputSystemUIInputModule module =
                Object.FindObjectOfType<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.enabled, Is.True);
            Assert.That(module.leftClick, Is.Not.Null);
            Assert.That(module.leftClick.action.enabled, Is.True);
            Assert.That(target.activeInHierarchy, Is.True, target.name);
            RectTransform rect = target.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, target.name);
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));

            QueueMouse(position);
            yield return null;
            QueueMouse(position, MouseButton.Left);
            yield return null;
            QueueMouse(position);
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
            Vector3 worldPoint = city.transform.TransformPoint(
                FormalInnerCityPresentationPolicy3D.CellCenterLocal(
                    x, y, 0f));
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

        private static Dropdown FindDropdown(string name)
        {
            Dropdown[] dropdowns = Object.FindObjectsOfType<Dropdown>(true);
            for (var index = 0; index < dropdowns.Length; index++)
                if (dropdowns[index].name == name)
                    return dropdowns[index];
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
            public GrayboxDisplaySettings3D LastApplied { get; private set; }

            public bool TryApply(GrayboxDisplaySettings3D settings)
            {
                ApplyCount++;
                LastApplied = settings;
                return true;
            }
        }

        private sealed class FakeDisplayStore :
            IGrayboxDisplaySettingsStore
        {
            public int SaveCount { get; private set; }
            public int Version { get; private set; }
            public GrayboxDisplaySettings3D Settings { get; private set; }

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
                Settings = settings;
            }
        }

        private sealed class FakeExit : IGrayboxApplicationExit
        {
            public int Count { get; private set; }
            public void Exit() => Count++;
        }
    }
}
