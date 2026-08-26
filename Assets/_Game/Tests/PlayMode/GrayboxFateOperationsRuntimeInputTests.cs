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
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Progression;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class GrayboxFateOperationsRuntimeInputTests
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
                        "GrayboxFateOperationsRuntimeInputEmpty");
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
        public IEnumerator IDEA0020_RealRewindOperationsCreateReadAndClear()
        {
            GameObject operationsModal = RequireSceneObject(
                "FateOperations.Modal",
                includeInactive: true);
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(building, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(city, Is.Not.Null);

            yield return SelectRewindFate();
            Assert.That(host.FateRuntime.Capture().SelectedId,
                Is.EqualTo(FormalFateCatalog.RewindAnchorId));
            Assert.That(host.AttentionRuntime.Value, Is.EqualTo(15));

            yield return OpenFateOperations();
            Assert.That(operationsModal.activeInHierarchy, Is.True);
            yield return AssertModalBlocksGameplay(
                operationsModal,
                building,
                operations,
                city);
            yield return Click(RequireSceneObject(
                "FateOperations.CreateAnchor"));
            Assert.That(host.RewindAnchorMetadata.Capture().Entries,
                Has.Count.EqualTo(1));
            Assert.That(RequireSceneObject("FateOperations.AnchorStatus")
                    .GetComponent<Text>().text,
                Does.Contain("1").Or.Contain("已创建"));

            yield return TapKey(Key.Escape);
            Assert.That(operationsModal.activeSelf, Is.False);
            GameObject attentionDetails = RequireSceneObject(
                "Progression.AttentionDetails",
                includeInactive: true);
            Assert.That(attentionDetails.activeInHierarchy, Is.True,
                "Closing fate operations returns to attention details.");
            yield return TapKey(Key.Escape);
            Assert.That(attentionDetails.activeSelf, Is.False);

            CityMode anchorMode = city.Mode;
            yield return TapKey(Key.F);
            city.Deployment.Tick(10f);
            yield return null;
            Assert.That(city.Mode, Is.Not.EqualTo(anchorMode));
            int attentionBeforeRead = host.AttentionRuntime.Value;

            yield return OpenFateOperations();
            yield return Click(RequireSceneObject("FateOperations.ReadAnchor"));
            GameObject confirmation = RequireSceneObject(
                "FateOperations.Confirmation");
            Assert.That(confirmation.activeInHierarchy, Is.True);
            yield return AssertModalBlocksGameplay(
                confirmation,
                building,
                operations,
                city);
            yield return TapKey(Key.Escape);
            Assert.That(confirmation.activeSelf, Is.False);
            Assert.That(operationsModal.activeInHierarchy, Is.True,
                "Escape closes only the top confirmation layer.");

            yield return Click(RequireSceneObject("FateOperations.ReadAnchor"));
            yield return Click(RequireSceneObject("FateOperations.Confirm"));
            Assert.That(city.Mode, Is.EqualTo(anchorMode));
            Assert.That(host.AttentionRuntime.Value,
                Is.EqualTo(attentionBeforeRead + 12));

            yield return OpenFateOperations();
            yield return Click(RequireSceneObject("FateOperations.ClearAnchors"));
            Assert.That(host.RewindAnchorMetadata.Capture().Entries, Is.Empty);
        }

        private IEnumerator SelectRewindFate()
        {
            yield return Click(RequireSceneObject(
                "FateSelection.Card." + FormalFateCatalog.RewindAnchorId));
            yield return Click(RequireSceneObject("FateSelection.Confirm"));
        }

        private IEnumerator OpenFateOperations()
        {
            GameObject attentionDetails = RequireSceneObject(
                "Progression.AttentionDetails",
                includeInactive: true);
            if (!attentionDetails.activeInHierarchy)
                yield return Click(RequireSceneObject(
                    "Progression.AttentionStatus"));
            yield return Click(RequireSceneObject(
                "Progression.AttentionDetails.Fate"));
        }

        private IEnumerator AssertModalBlocksGameplay(
            GameObject modal,
            GrayboxBuildingInteractionModel3D building,
            GrayboxOperationsController3D operations,
            GrayboxMobileCityController3D city)
        {
            Vector3 before = city.transform.position;
            yield return TapKey(Key.B);
            yield return TapKey(Key.E);
            yield return TapKey(Key.T);
            yield return TapKey(Key.W);
            QueueMouse(new Vector2(64f, Screen.height * .5f),
                MouseButton.Left);
            yield return null;
            QueueMouse(new Vector2(64f, Screen.height * .5f));
            yield return null;
            Assert.That(modal.activeInHierarchy, Is.True);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(operations.IsAnyPanelOpen, Is.False);
            Assert.That(city.transform.position, Is.EqualTo(before));
        }

        private IEnumerator HoldKey(Key key, int frames)
        {
            for (var index = 0; index < frames; index++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.Update();
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
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
            Assert.That(target.GetComponent<Button>(), Is.Not.Null, target.name);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = target.GetComponent<RectTransform>();
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

        private static GameObject RequireSceneObject(
            string name,
            bool includeInactive = false)
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            GameObject value = Object.FindObjectsOfType<Transform>(
                    includeInactive)
                .Where(candidate => candidate.gameObject.scene == scene)
                .Select(candidate => candidate.gameObject)
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }
    }
}
