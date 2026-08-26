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
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class GrayboxProgressionRuntimeInputTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private const string AttentionStatusName =
            "Progression.AttentionStatus";
        private const string AttentionValueName =
            "Progression.AttentionStatus.Value";
        private const string AttentionDetailsName =
            "Progression.AttentionDetails";
        private const string RecentReasonsName =
            "Progression.AttentionDetails.RecentReasons";

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
                        "GrayboxProgressionRuntimeInputEmpty");
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
        public IEnumerator IDEA0020_RealAttentionDetailsOwnInputUntilEscape()
        {
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxSystemMenuController3D systemMenu =
                Object.FindObjectOfType<GrayboxSystemMenuController3D>();
            Assert.That(building, Is.Not.Null);
            Assert.That(systemMenu, Is.Not.Null);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));

            GameObject status = RequireSceneObject(AttentionStatusName);
            Assert.That(status.GetComponent<Button>(), Is.Not.Null,
                "The persistent attention status must be a real UGUI button.");
            Assert.That(RequireSceneObject(AttentionValueName)
                    .GetComponent<Text>().text,
                Does.Contain("15").And.Contain("100"));
            GameObject details = RequireSceneObject(
                AttentionDetailsName,
                includeInactive: true);
            Assert.That(details.activeSelf, Is.False);

            int pointerProbeClicks = 0;
            GameObject pointerProbe = CreatePointerProbe(
                status,
                () => pointerProbeClicks++);
            yield return ClickUiElement(pointerProbe);
            Assert.That(pointerProbeClicks, Is.EqualTo(1));
            pointerProbeClicks = 0;

            yield return ClickUiElement(status);

            Assert.That(details.activeInHierarchy, Is.True);
            Text recentReasons = RequireSceneObject(RecentReasonsName)
                .GetComponent<Text>();
            Assert.That(recentReasons, Is.Not.Null);
            Assert.That(recentReasons.text,
                Does.Contain("最近").And.Contain("选择命轨").And.Contain("+5"));

            yield return ClickUiElement(pointerProbe);
            Assert.That(pointerProbeClicks, Is.Zero,
                "The attention details blocker must own real pointer input " +
                "outside the visible panel.");

            yield return TapKey(Key.B);
            Assert.That(details.activeInHierarchy, Is.True,
                "Build input must not close or pass through the details panel.");
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));

            yield return TapKey(Key.Escape);
            Assert.That(details.activeSelf, Is.False);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive),
                "The Escape used to close details cannot reach build input.");
            Assert.That(systemMenu.IsOpen, Is.False,
                "The same Escape cannot also open the system menu.");

            yield return TapKey(Key.B);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen),
                "Build input must resume on the next real input frame.");
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator ClickUiElement(GameObject target)
        {
            InputSystemUIInputModule module =
                Object.FindObjectOfType<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.enabled, Is.True);
            Assert.That(module.point?.action?.enabled, Is.True);
            Assert.That(module.leftClick?.action?.enabled, Is.True);
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

        private static GameObject CreatePointerProbe(
            GameObject status,
            UnityEngine.Events.UnityAction onClick)
        {
            Transform canvas = status.transform.parent.parent;
            var probe = new GameObject(
                "Progression.PointerModalProbe",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            RectTransform rect = probe.GetComponent<RectTransform>();
            rect.SetParent(canvas, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(60f, 60f);
            rect.sizeDelta = new Vector2(120f, 60f);
            Image image = probe.GetComponent<Image>();
            image.color = Color.magenta;
            image.raycastTarget = true;
            Button button = probe.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            return probe;
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
