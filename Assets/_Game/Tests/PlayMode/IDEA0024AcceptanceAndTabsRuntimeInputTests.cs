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
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Progression;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class IDEA0024AcceptanceAndTabsRuntimeInputTests
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
                        "IDEA0024.Acceptance.Empty");
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
        public IEnumerator
            IDEA0024_RealAcceptanceLaunchThenClickableTabsDoNotHitWorld()
        {
            GrayboxFormalSaveEntryController3D entry = Object.FindObjectOfType<
                GrayboxFormalSaveEntryController3D>(true);
            GrayboxSystemMenuView3D systemView = Object.FindObjectOfType<
                GrayboxSystemMenuView3D>(true);
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>(true);
            Assert.That(entry, Is.Not.Null);
            Assert.That(systemView, Is.Not.Null);
            Assert.That(developer, Is.Not.Null);
            Assert.That(entry.IsStartPageOpen, Is.True);
            Assert.That(entry.IsRuntimeReady, Is.False);

            yield return ClickNamed("Start.AcceptanceConsole");
            Assert.That(systemView.IsAcceptancePageOpen, Is.True);
            Assert.That(FindButton("Acceptance.Continue").interactable,
                Is.False,
                "The empty isolated slot must preserve formal Continue gating.");
            yield return ClickNamed("Acceptance.Back");
            Assert.That(systemView.IsAcceptancePageOpen, Is.False);
            Assert.That(entry.IsStartPageOpen, Is.True);

            yield return ClickNamed("Start.AcceptanceConsole");
            yield return ClickNamed("Acceptance.NewGame");
            Assert.That(entry.IsStartPageOpen, Is.False, entry.FeedbackMessage);
            Assert.That(entry.IsRuntimeReady, Is.True, entry.FeedbackMessage);
            Assert.That(developer.IsPanelOpen, Is.True,
                "The modifier opens only after formal EnterGameplay succeeds.");

            yield return Tap(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.False);
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            FormalFateSnapshot offered = host.FateRuntime.Capture();
            Assert.That(offered.OfferedIds, Is.Not.Empty);
            yield return ClickNamed(
                "FateSelection.Card." +
                offered.OfferedIds[0]);
            yield return ClickNamed("FateSelection.Confirm");

            GrayboxCivilizationExpansionView3D expansionView =
                Object.FindObjectOfType<
                    GrayboxCivilizationExpansionView3D>(true);
            GrayboxMobileCityController3D city = Object.FindObjectOfType<
                GrayboxMobileCityController3D>();
            GrayboxCivilizationExpansionController3D expansion =
                Object.FindObjectOfType<
                    GrayboxCivilizationExpansionController3D>();
            Assert.That(expansionView, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(expansion, Is.Not.Null);
            WorldGridPoint? destinationBefore = city.Destination;

            yield return Tap(Key.M);
            Assert.That(expansionView.IsOpen, Is.True);
            Assert.That(expansionView.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Army));
            yield return ClickNamed("CivilizationExpansion.Tab.World");
            Assert.That(expansionView.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.World));
            Assert.That(city.Destination, Is.EqualTo(destinationBefore));
            Assert.That(expansion.HasPendingMapTarget, Is.False);

            yield return ClickNamed("CivilizationExpansion.Tab.Politics");
            Assert.That(expansionView.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Politics));
            Assert.That(city.Destination, Is.EqualTo(destinationBefore));
            Assert.That(expansion.HasPendingMapTarget, Is.False);

            yield return ClickNamed("CivilizationExpansion.Tab.Army");
            Assert.That(expansionView.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Army));
            Assert.That(city.Destination, Is.EqualTo(destinationBefore));
            Assert.That(expansion.HasPendingMapTarget, Is.False);
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

        private IEnumerator ClickNamed(string name)
        {
            Button button = FindButton(name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.interactable, Is.True, name);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null, name);
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null,
                name);
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Assert.That(TryFindRaycastablePoint(
                    button,
                    rect,
                    eventCamera,
                    out Vector2 position,
                    out string hitNames),
                Is.True,
                name + " must be hit inside its real RectTransform. Hits: " +
                hitNames + " rect=" + rect.rect + " canvas=" + canvas.name);

            QueueMouse(position);
            yield return null;
            QueueMouse(position, MouseButton.Left);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private static bool TryFindRaycastablePoint(
            Button button,
            RectTransform rect,
            Camera eventCamera,
            out Vector2 position,
            out string lastHitNames)
        {
            float[] samples = { .5f, .2f, .8f };
            lastHitNames = string.Empty;
            for (var yIndex = 0; yIndex < samples.Length; yIndex++)
            for (var xIndex = 0; xIndex < samples.Length; xIndex++)
            {
                Rect bounds = rect.rect;
                Vector3 local = new Vector3(
                    Mathf.Lerp(bounds.xMin, bounds.xMax, samples[xIndex]),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, samples[yIndex]),
                    0f);
                position = RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    rect.TransformPoint(local));
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = position,
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                lastHitNames = string.Join(", ", hits.ConvertAll(hit =>
                    hit.gameObject.name));
                if (hits.Exists(hit =>
                        hit.gameObject == button.gameObject ||
                        hit.gameObject.transform.IsChildOf(button.transform)))
                    return true;
            }
            position = default;
            return false;
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

        private static Button FindButton(string name)
        {
            Button[] values = Object.FindObjectsOfType<Button>(true);
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index].name == name) return values[index];
            }
            Assert.Fail("Missing button " + name + ".");
            return null;
        }
    }
}
