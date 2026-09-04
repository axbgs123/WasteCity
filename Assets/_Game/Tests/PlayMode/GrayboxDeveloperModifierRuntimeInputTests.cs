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
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Progression;
using WasteCity.Leader.Exploration;

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
            bool progressionModifierTest = TestContext.CurrentContext.Test.Name
                .Contains("IDEA0020");
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(
                    mouse,
                    completeFateSelection: !progressionModifierTest);
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
            Assert.That(developerPanel.localScale, Is.EqualTo(Vector3.one),
                "The developer drawer must preserve readable text size.");
            Assert.That(developerPanel.anchorMin.x, Is.EqualTo(1f));
            Assert.That(developerPanel.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(
                developerPanel.rect.height,
                Is.LessThanOrEqualTo(
                    developerCanvas.GetComponent<RectTransform>()
                        .rect.height - 32f));
            Transform developerScrollRoot = developerPanel.Find(
                "Developer.Panel.Scroll");
            Assert.That(developerScrollRoot, Is.Not.Null);
            ScrollRect developerScroll = developerScrollRoot
                .GetComponent<ScrollRect>();
            Assert.That(developerScroll, Is.Not.Null);
            Assert.That(developerScroll.vertical, Is.True);
            Assert.That(developerScroll.content, Is.Not.Null);
            Assert.That(developerScroll.viewport, Is.Not.Null);
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

        [UnityTest]
        public IEnumerator
            IDEA0020_ProgressionActionsUseRealZeroSearchParametersAndFormalOwners()
        {
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>();
            GrayboxFormalSaveRuntimeHost3D host =
                Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(developer, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(host.FateRuntime.Capture().HasSelection, Is.False);

            yield return TapKey(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.True,
                "The Development/Editor modifier must open through real 0.");
            InputField actionSearch = FindInput("Progression Action Search");
            yield return ScrollDeveloperTargetIntoView(
                actionSearch.GetComponent<RectTransform>());
            yield return FocusInput(actionSearch);
            yield return TapKey(Key.U);
            yield return TapKey(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.True,
                "U and 0 cannot leak through a focused text parameter.");
            Assert.That(host.Sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.None));

            yield return SelectProgressionAction(
                "查询进度配置签名",
                "developer.query.configuration-signature");
            yield return ExecuteProgressionAction();
            Assert.That(developer.HasModifiedGameState, Is.False,
                "Read-only query actions never mark the run modified.");
            Assert.That(FindText("Developer Feedback").text,
                Does.Contain("配置签名"));

            yield return SetProgressionAmount(29);
            yield return SelectProgressionAction(
                "设置关注度", "developer.attention.set");
            yield return ExecuteProgressionAction();
            yield return SetProgressionAmount(1);
            yield return SelectProgressionAction(
                "增加关注度", "developer.attention.increase");
            yield return ExecuteProgressionAction();
            yield return AssertAttentionThreshold(host, 30);
            yield return SetAttentionThenCross(host, 59, 60);
            yield return SetAttentionThenCross(host, 89, 90);
            Assert.That(developer.HasModifiedGameState, Is.True);
            Assert.That(FindSceneText(
                    "Progression.AttentionStatus.Value").text,
                Does.Contain("90").And.Contain("100"));

            yield return FocusInput(actionSearch);
            yield return TapKey(Key.Escape);
            Assert.That(developer.IsPanelOpen, Is.True,
                "First Escape clears text focus only.");
            yield return TapKey(Key.Escape);
            Assert.That(developer.IsPanelOpen, Is.False,
                "Second Escape closes the modifier without opening menu.");
            yield return TapKey(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.True);
            yield return ScrollDeveloperTargetIntoView(
                actionSearch.GetComponent<RectTransform>());

            yield return SelectProgressionAction(
                "选择袖珍宇宙命轨",
                "developer.fate.select-pocket-universe");
            yield return ExecuteProgressionAction();
            Assert.That(host.FateRuntime.Capture().SelectedId,
                Is.EqualTo(FormalFateCatalog.PocketUniverseId));
            yield return SelectProgressionAction(
                "满足升阶测试条件",
                "developer.ascension.requirements-satisfy");
            yield return ExecuteProgressionAction();
            Assert.That(host.CaptureRequirements().CanAscend, Is.True);
            yield return SelectProgressionAction(
                "执行首次文明升阶",
                "developer.civilization.first-ascension");
            yield return ExecuteProgressionAction();
            Assert.That(host.Civilization.Capture().CivilizationLevel,
                Is.EqualTo(2));
            Assert.That(host.FateRuntime.Capture().Level, Is.EqualTo(2));
            bool[] exploredBeforeSave = host.ExplorationController
                .Capture().Exploration.ExploredCells;

            GrayboxFormalSaveEntryController3D entry =
                Object.FindObjectOfType<
                    GrayboxFormalSaveEntryController3D>(true);
            Assert.That(entry, Is.Not.Null);
            GrayboxFormalSaveUiResult3D saved = entry.SaveAndExit();
            Assert.That(saved.Success, Is.True, saved.Message);
            CollectionAssert.AreEqual(
                exploredBeforeSave,
                host.LastCoordinatorResult.Envelope.formal3D
                    .exploration.exploredCells,
                "The progression save must preserve the authoritative fog " +
                "snapshot without assuming an arbitrary map cell is visible.");
            yield return SceneManager.LoadSceneAsync(
                SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            entry = Object.FindObjectOfType<
                GrayboxFormalSaveEntryController3D>(true);
            Assert.That(entry.CanContinue, Is.True, entry.FeedbackMessage);
            yield return ClickUi(
                FindButton("Start.Continue").GetComponent<RectTransform>());
            Assert.That(entry.IsRuntimeReady, Is.True, entry.FeedbackMessage);
            Assert.That(entry.FeedbackMessage, Is.EqualTo("已继续最近进度"));
            host = Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.AttentionRuntime.Value, Is.EqualTo(100),
                "The saved value includes the one-time ascension +25 cap.");
            Assert.That(host.FateRuntime.Capture().Level, Is.EqualTo(2));
            Assert.That(host.Civilization.Capture().CivilizationLevel,
                Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator
            IDEA0029_ExplorationActionsUseRealZeroChineseSearchAndFormalOwners()
        {
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>();
            GrayboxFormalSaveRuntimeHost3D host =
                Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(developer, Is.Not.Null);
            Assert.That(host, Is.Not.Null);

            yield return TapKey(Key.Digit0);
            Assert.That(developer.IsPanelOpen, Is.True);
            yield return SelectProgressionAction(
                "探索整张地图",
                "developer.exploration.reveal-all");
            yield return ExecuteProgressionAction();
            for (var y = 0; y < host.ExplorationController.Exploration.Height;
                 y++)
            for (var x = 0; x < host.ExplorationController.Exploration.Width;
                 x++)
            {
                Assert.That(host.ExplorationController.Exploration
                    .IsExplored(x, y), Is.True, x + "," + y);
            }

            yield return SelectProgressionAction(
                "准备领袖手采验收",
                "developer.exploration.gather-ready");
            yield return ExecuteProgressionAction();

            Assert.That(host.ExplorationController.CenJinDistress.IsCompleted,
                Is.True);
            Assert.That(host.ExplorationController.LeaderControl.RequestedMode,
                Is.EqualTo(LeaderControlMode.Manual));
            Assert.That(FindText("Developer Feedback").text,
                Does.Contain("准备领袖手采验收").And.Contain("已执行"));
            Assert.That(developer.HasModifiedGameState, Is.True);

            GrayboxFormalSaveEntryController3D entry =
                Object.FindObjectOfType<
                    GrayboxFormalSaveEntryController3D>(true);
            Assert.That(entry, Is.Not.Null);
            GrayboxFormalSaveUiResult3D saved = entry.SaveAndExit();
            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That(host.LastCoordinatorResult.Envelope.formal3D
                .exploration.exploredCells[0], Is.True);
            Assert.That(host.LastCoordinatorResult.Envelope.formal3D
                .exploration.exploredCells[
                    host.ExplorationController.Exploration.Width *
                    host.ExplorationController.Exploration.Height - 1],
                Is.True);
            yield return SceneManager.LoadSceneAsync(
                SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            entry = Object.FindObjectOfType<
                GrayboxFormalSaveEntryController3D>(true);
            Assert.That(entry.CanContinue, Is.True, entry.FeedbackMessage);
            yield return ClickUi(
                FindButton("Start.Continue").GetComponent<RectTransform>());
            Assert.That(entry.IsRuntimeReady, Is.True, entry.FeedbackMessage);
            Assert.That(entry.FeedbackMessage, Is.EqualTo("已继续最近进度"));

            host = Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.ExplorationController.Exploration.IsExplored(0, 0),
                Is.True);
            Assert.That(host.ExplorationController.Exploration.IsExplored(
                host.ExplorationController.Exploration.Width - 1,
                host.ExplorationController.Exploration.Height - 1), Is.True);
            Assert.That(host.ExplorationController.CenJinDistress.IsCompleted,
                Is.True);
            Assert.That(host.ExplorationController.LeaderControl.RequestedMode,
                Is.EqualTo(LeaderControlMode.Manual));
        }

        private IEnumerator SetAttentionThenCross(
            GrayboxFormalSaveRuntimeHost3D host,
            int before,
            int threshold)
        {
            yield return SetProgressionAmount(before);
            yield return SelectProgressionAction(
                "设置关注度", "developer.attention.set");
            yield return ExecuteProgressionAction();
            yield return SetProgressionAmount(1);
            yield return SelectProgressionAction(
                "增加关注度", "developer.attention.increase");
            yield return ExecuteProgressionAction();
            yield return AssertAttentionThreshold(host, threshold);
        }

        private IEnumerator ScrollDeveloperTargetIntoView(
            RectTransform target)
        {
            ScrollRect scroll = GameObject.Find("Developer.Panel.Scroll")
                .GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.Not.Null);
            Assert.That(scroll.viewport, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Canvas canvas = scroll.viewport.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            for (var attempt = 0; attempt < 48; attempt++)
            {
                Bounds bounds = RectTransformUtility
                    .CalculateRelativeRectTransformBounds(
                        scroll.viewport, target);
                Rect viewport = scroll.viewport.rect;
                if (bounds.min.y >= viewport.yMin &&
                    bounds.max.y <= viewport.yMax)
                    yield break;
                float wheel = bounds.center.y < viewport.center.y
                    ? -120f
                    : 120f;
                QueueMouseScroll(screen, wheel);
                yield return null;
                QueueMouse(screen);
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            Assert.Fail(target.name +
                " did not enter the developer viewport through mouse wheel input.");
        }

        private IEnumerator AssertAttentionThreshold(
            GrayboxFormalSaveRuntimeHost3D host,
            int threshold)
        {
            yield return null;
            Assert.That(host.AttentionRuntime.Capture().ReachedThresholds,
                Does.Contain(threshold));
            Assert.That(host.AttentionPressureRuntime.Capture().Entries
                    .Select(value => value.Threshold),
                Does.Contain(threshold));
        }

        private IEnumerator SelectProgressionAction(
            string chineseName,
            string stableId)
        {
            InputField search = FindInput("Progression Action Search");
            yield return ScrollDeveloperTargetIntoView(
                search.GetComponent<RectTransform>());
            yield return ReplaceInputText(search, chineseName);
            Button action = FindButton("Developer.Progression." + stableId);
            Assert.That(action.gameObject.activeInHierarchy, Is.True,
                chineseName);
            Assert.That(action.GetComponentInChildren<Text>().text,
                Is.EqualTo(chineseName));
            yield return ScrollDeveloperTargetIntoView(
                action.GetComponent<RectTransform>());
            yield return ClickUi(action.GetComponent<RectTransform>());
        }

        private IEnumerator SetProgressionAmount(int value)
        {
            InputField amount = FindInput("Progression Amount");
            yield return ScrollDeveloperTargetIntoView(
                amount.GetComponent<RectTransform>());
            yield return ReplaceInputText(amount, value.ToString());
        }

        private IEnumerator ExecuteProgressionAction()
        {
            RectTransform execute = FindButton("Execute Progression Action")
                .GetComponent<RectTransform>();
            yield return ScrollDeveloperTargetIntoView(execute);
            yield return ClickUi(execute);
        }

        private IEnumerator ReplaceInputText(
            InputField input,
            string value)
        {
            yield return FocusInput(input);
            int characters = input.text?.Length ?? 0;
            for (var index = 0; index < characters; index++)
                yield return TapKey(Key.Backspace);
            yield return TypeText(value);
            Assert.That(input.text, Is.EqualTo(value));
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

        private void QueueMouseScroll(Vector2 position, float wheel)
        {
            var state = new MouseState
            {
                position = position,
                scroll = new Vector2(0f, wheel),
            };
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

        private static Text FindText(string rootName)
        {
            GameObject root = GameObject.Find(rootName);
            Assert.That(root, Is.Not.Null, rootName);
            Text text = root.GetComponentInChildren<Text>(true);
            Assert.That(text, Is.Not.Null, rootName);
            return text;
        }

        private static Text FindSceneText(string name)
        {
            Text[] values = Object.FindObjectsOfType<Text>(true);
            for (var index = 0; index < values.Length; index++)
                if (values[index].name == name)
                    return values[index];
            Assert.Fail("Missing text " + name + ".");
            return null;
        }
    }
}
