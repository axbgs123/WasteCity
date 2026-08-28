using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Combat;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class GrayboxCivilizationExpansionRuntimeInputTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInput =
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
                SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(mouse);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                Scene scene = SceneManager.GetSceneByName(SceneName);
                if (scene.IsValid() && scene.isLoaded)
                {
                    Scene empty = SceneManager.CreateScene(
                        "CivilizationExpansion.Empty");
                    SceneManager.SetActiveScene(empty);
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            finally
            {
                GrayboxFormalPlayModeEntryFixture.CleanupIsolatedStore();
                if (keyboard != null && keyboard.added)
                    InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added)
                    InputSystem.RemoveDevice(mouse);
                InputSystem.settings.updateMode = previousUpdateMode;
                InputSystem.settings.backgroundBehavior =
                    previousBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    previousEditorInput;
                Time.timeScale = previousTimeScale;
                GrayboxFormalPlayModeEntryFixture.AssertRealSaveFilesUnchanged();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0022_RealMNPDriveOneMutuallyExclusiveRuntime()
        {
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            GrayboxCivilizationExpansionView3D view =
                Object.FindObjectOfType<
                    GrayboxCivilizationExpansionView3D>();
            GrayboxWorldView3D world = Object.FindObjectOfType<
                GrayboxWorldView3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(view, Is.Not.Null);
            Assert.That(host.CivilizationExpansionController, Is.Not.Null);
            Assert.That(
                host.CivilizationExpansionController.IsInitialized,
                Is.True);
            Assert.That(world.Model.Width, Is.EqualTo(64));
            Assert.That(world.Model.Height, Is.EqualTo(48));
            Assert.That(world.Model.ResourceNodeCount, Is.EqualTo(24));

            yield return Tap(Key.M);
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Army));
            Assert.That(view.HeadingText.text, Does.Contain("军队"));

            yield return Click(view.PrimaryButton);
            Assert.That(host.CivilizationExpansionController.Runtime.Army
                    .Commands.Command,
                Is.EqualTo(FriendlySquadCommandType.Guard));

            yield return Tap(Key.N);
            Assert.That(view.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.World));
            Assert.That(view.HeadingText.text, Does.Contain("世界"));

            yield return Tap(Key.P);
            Assert.That(view.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Politics));
            Assert.That(view.DetailsText.text,
                Does.Contain("岑烬").And.Contain("灰烬商团"));

            yield return Tap(Key.Escape);
            Assert.That(view.IsOpen, Is.False);

            GrayboxFormalSaveEntryController3D entry =
                Object.FindObjectOfType<
                    GrayboxFormalSaveEntryController3D>();
            GrayboxFormalSaveUiResult3D saved = entry.SaveAndExit();
            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(host.LastStoreResult.Envelope.formal3D
                    .civilizationExpansion.armyLeader.squads[0].command,
                Is.EqualTo((int)FriendlySquadCommandType.Guard),
                "The written schema 34 envelope must own the live command.");
            yield return SceneManager.LoadSceneAsync(
                SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ClickNamed("Start.Continue");
            yield return null;
            host = Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host.LastCoordinatorResult.Success, Is.True,
                host.LastCoordinatorResult.Message + " domain=" +
                host.LastCoordinatorResult.FailedDomain);
            Assert.That(host.LastStoreResult.Envelope.formal3D
                    .civilizationExpansion.armyLeader.squads[0].command,
                Is.EqualTo((int)FriendlySquadCommandType.Guard),
                "The continued envelope must decode the saved command.");
            Assert.That(host.CivilizationExpansionController.Runtime.Army
                    .Commands.Command,
                Is.EqualTo(FriendlySquadCommandType.Guard),
                "Schema 34 must restore the real army command after continue.");
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = position,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            QueueMouse(position);
            yield return null;
            QueueMouse(position, MouseButton.Left);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator ClickNamed(string name)
        {
            Canvas.ForceUpdateCanvases();
            Button[] buttons = Object.FindObjectsOfType<Button>(true);
            Button match = null;
            for (var index = 0; index < buttons.Length; index++)
            {
                if (buttons[index].name == name)
                {
                    match = buttons[index];
                    break;
                }
            }
            Assert.That(match, Is.Not.Null, name);
            yield return Click(match);
        }

        private void QueueMouse(
            Vector2 position,
            MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue) state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }
    }
}
