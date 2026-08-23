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
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxDeveloperModifierRuntimeInputTests
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
        public IEnumerator LoadScene()
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
        public IEnumerator UnloadScene()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene(
                    "GrayboxDeveloperModifierRuntimeInputEmpty");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            GrayboxFormalPlayModeEntryFixture.CleanupIsolatedStore();
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
            GrayboxFormalPlayModeEntryFixture.AssertRealSaveFilesUnchanged();
            yield return null;
        }

        [UnityTest]
        public IEnumerator
            IDEA0016_DeveloperPanelUsesRealInputAndOwnsGameplayModally()
        {
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>();
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxSystemMenuController3D systemMenu =
                Object.FindObjectOfType<GrayboxSystemMenuController3D>();

            Assert.That(developer, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            Assert.That(interaction, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(systemMenu, Is.Not.Null);
            Assert.That(developer.IsPanelOpen, Is.False);

            yield return TapKey(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.True);
            yield return TapKey(Key.F10);
            Assert.That(developer.IsPanelOpen, Is.True);
            RectTransform developerPanel = GameObject
                .Find("Graybox Developer Modifier")
                .GetComponent<RectTransform>();
            Canvas developerCanvas = developerPanel.GetComponentInParent<Canvas>();
            const float panelMargin = 16f;
            float expectedScale = Mathf.Clamp(Mathf.Min(
                Mathf.Max(0f, developerCanvas.pixelRect.width - panelMargin) /
                    developerPanel.rect.width,
                Mathf.Max(0f, developerCanvas.pixelRect.height - panelMargin) /
                    developerPanel.rect.height),
                .25f,
                1f);
            Assert.That(developerPanel.localScale.x,
                Is.EqualTo(expectedScale).Within(.0001f));
            Assert.That(developerPanel.rect.height * developerPanel.localScale.y,
                Is.LessThanOrEqualTo(developerCanvas.pixelRect.height - 16f));
            yield return null;

            yield return TapKey(Key.B);
            yield return TapKey(Key.E);
            yield return TapKey(Key.T);
            yield return TapKey(Key.Space);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(operations.IsAnyPanelOpen, Is.False);
            Assert.That(systemMenu.IsTacticalPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            InputField resourceSearch = FindInput("Resource Search");
            yield return FocusInput(resourceSearch);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(resourceSearch.gameObject));
            yield return TypeText("融合");
            Assert.That(resourceSearch.text, Is.EqualTo("融合"));
            yield return TapKey(Key.B);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(developer.IsPanelOpen, Is.True);

            Button resource = FindButton(
                "Developer.Resource." + ResourceIds.HybridCore);
            Assert.That(resource.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                resource.GetComponentInChildren<Text>().text,
                Is.EqualTo("融合核心"));
            int resourceBefore =
                session.Inventory.Get(ResourceIds.HybridCore);
            yield return ClickUi(resource.GetComponent<RectTransform>());
            yield return ClickUi(
                FindButton("Resource +100").GetComponent<RectTransform>());
            Assert.That(
                session.Inventory.Get(ResourceIds.HybridCore),
                Is.EqualTo(resourceBefore + 100));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.Not.Null,
                "A clicked development button remains selected until the " +
                "modal coordinator classifies it correctly.");
            yield return TapKey(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.False,
                "Button selection must not consume the development toggle.");
            yield return TapKey(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.True);

            InputField researchSearch = FindInput("Research Search");
            yield return FocusInput(researchSearch);
            yield return TypeText("灵火");
            Assert.That(researchSearch.text, Is.EqualTo("灵火"));
            const string researchId = "core.research.spirit-sensing";
            Assert.That(session.IsResearchCompleted(researchId), Is.False);
            Button research = FindButton(
                "Developer.Research." + researchId);
            Assert.That(research.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                research.GetComponentInChildren<Text>().text,
                Is.EqualTo("灵火淬炼"));
            yield return ClickUi(research.GetComponent<RectTransform>());
            yield return ClickUi(
                FindButton("Unlock Research").GetComponent<RectTransform>());
            Assert.That(session.IsResearchCompleted(researchId), Is.True);

            yield return FocusInput(researchSearch);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(researchSearch.gameObject));
            yield return TapKey(Key.Escape);
            Assert.That(developer.IsPanelOpen, Is.True);
            Assert.That(systemMenu.IsOpen, Is.False);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.Null);

            yield return TapKey(Key.Escape);
            Assert.That(developer.IsPanelOpen, Is.False);
            Assert.That(systemMenu.IsOpen, Is.False);

            yield return TapKey(Key.B);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));
        }

        private IEnumerator TapKey(Key key)
        {
            QueueKeyboard(key);
            yield return null;
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator FocusInput(InputField input)
        {
            yield return ClickUi(input.GetComponent<RectTransform>());
            if (!input.isFocused)
                yield return TapKey(Key.Enter);
            Assert.That(input.isFocused, Is.True, input.name);
        }

        private IEnumerator TypeText(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                InputSystem.QueueStateEvent(
                    keyboard,
                    new KeyboardState(Key.A));
                InputSystem.QueueTextEvent(keyboard, value[index]);
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(
                    keyboard,
                    new KeyboardState());
                InputSystem.Update();
                yield return null;
            }
        }

        private IEnumerator ClickUi(RectTransform rect)
        {
            Assert.That(rect, Is.Not.Null);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            GraphicRaycaster raycaster =
                canvas.GetComponent<GraphicRaycaster>();
            Assert.That(
                raycaster,
                Is.Not.Null,
                "Developer canvas must support real pointer input.");
            Assert.That(raycaster.isActiveAndEnabled, Is.True);
            Assert.That(EventSystem.current, Is.Not.Null);
            Camera eventCamera = canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                rect.position);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = screen,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            bool targetHit = false;
            string hitNames = string.Empty;
            for (var index = 0; index < hits.Count; index++)
            {
                GameObject hit = hits[index].gameObject;
                hitNames += (index == 0 ? string.Empty : ", ") + hit.name;
                if (hit == rect.gameObject ||
                    hit.transform.IsChildOf(rect))
                    targetHit = true;
            }
            Assert.That(
                targetHit,
                Is.True,
                rect.name + " at " + screen + " raycast hits: " + hitNames);
            QueueMouse(screen);
            yield return null;
            QueueMouse(screen, MouseButton.Left);
            yield return null;
            QueueMouse(screen);
            yield return null;
        }

        private void QueueKeyboard(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
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
            Assert.That(Mouse.current, Is.SameAs(mouse));
        }

        private static InputField FindInput(string name)
        {
            InputField[] values = Object.FindObjectsOfType<InputField>(true);
            for (var index = 0; index < values.Length; index++)
                if (values[index].name == name)
                    return values[index];
            Assert.Fail("Missing input " + name + ".");
            return null;
        }

        private static Button FindButton(string name)
        {
            Button[] values = Object.FindObjectsOfType<Button>(true);
            for (var index = 0; index < values.Length; index++)
                if (values[index].name == name)
                    return values[index];
            Assert.Fail("Missing button " + name + ".");
            return null;
        }
    }
}
