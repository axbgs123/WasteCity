using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Progression;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class GrayboxFateSelectionRuntimeInputTests
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
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(
                    mouse,
                    completeFateSelection: false);
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
                        "GrayboxFateSelectionRuntimeInputEmpty");
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
        public IEnumerator IDEA0028_RealFateModalSelectsOfferedCardWithoutInputLeak()
        {
            GameObject modal = RequireSceneObject("FateSelection.Modal");
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxSystemMenuController3D menu =
                Object.FindObjectOfType<GrayboxSystemMenuController3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(building, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(menu, Is.Not.Null);
            Assert.That(modal.activeInHierarchy, Is.True,
                "Pending fate plus EffectsReady must block the new game.");
            Assert.That(host.FateRuntime.Capture().HasSelection, Is.False);
            Assert.That(host.AttentionRuntime.Value, Is.EqualTo(10));

            Vector3 cityBefore = city.transform.position;
            yield return TapKey(Key.B);
            yield return TapKey(Key.E);
            yield return TapKey(Key.T);
            yield return TapKey(Key.W);
            QueueMouse(new Vector2(64f, Screen.height * .5f),
                MouseButton.Left);
            yield return null;
            QueueMouse(new Vector2(64f, Screen.height * .5f));
            yield return null;
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(operations.IsAnyPanelOpen, Is.False);
            Assert.That(
                Vector3.Distance(city.transform.position, cityBefore),
                Is.LessThan(.0001f),
                "Fate modal must block meaningful city movement; ignore only " +
                "sub-pixel floating-point transform noise.");

            yield return TapKey(Key.Escape);
            Assert.That(menu.IsOpen, Is.True,
                "Escape opens the higher-priority system menu.");
            Assert.That(modal.activeInHierarchy, Is.True);
            yield return TapKey(Key.Escape);
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(modal.activeInHierarchy, Is.True,
                "Closing the system menu returns to the fate modal.");

            FormalFateSnapshot pendingFate = host.FateRuntime.Capture();
            Assert.That(pendingFate.OfferedIds, Has.Count.EqualTo(3));
            Assert.That(pendingFate.OfferedIds.Distinct().Count(),
                Is.EqualTo(3));
            string offeredFateId = pendingFate.OfferedIds[0];
            GameObject card = RequireSceneObject(
                "FateSelection.Card." + offeredFateId);
            Assert.That(card.GetComponent<Button>(), Is.Not.Null);
            yield return Click(card);
            GameObject confirmation = RequireSceneObject(
                "FateSelection.Confirmation");
            Assert.That(confirmation.activeInHierarchy, Is.True);
            Assert.That(host.FateRuntime.Capture().HasSelection, Is.False,
                "Card click alone cannot commit fate.");

            yield return Click(RequireSceneObject("FateSelection.Confirm"));
            Assert.That(host.FateRuntime.Capture().SelectedId,
                Is.EqualTo(offeredFateId));
            Assert.That(host.FateRuntime.Capture().Level, Is.EqualTo(1));
            Assert.That(host.AttentionRuntime.Value, Is.EqualTo(15));
            Assert.That(modal.activeSelf, Is.False);
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator Click(GameObject target)
        {
            InputSystemUIInputModule module = Object.FindObjectOfType<
                InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = target.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, target.name);
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

        private void QueueMouse(
            Vector2 position,
            MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue) state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static GameObject RequireSceneObject(string name)
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            GameObject value = Object.FindObjectsOfType<Transform>(true)
                .Where(candidate => candidate.gameObject.scene == scene)
                .Select(candidate => candidate.gameObject)
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }
    }
}
