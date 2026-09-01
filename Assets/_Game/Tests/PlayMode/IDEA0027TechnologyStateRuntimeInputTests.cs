using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Tests
{
    public sealed class IDEA0027TechnologyStateRuntimeInputTests
    {
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private readonly List<GameObject> owned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode =
                InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null)
                    UnityEngine.Object.Destroy(owned[index]);
            owned.Clear();
            if (mouse != null && mouse.added)
                InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior =
                previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                previousEditorInputBehavior;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverloadButtonUsesInputSystemAndUguiClick()
        {
            var canvasRoot = new GameObject(
                "IDEA0027.Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            owned.Add(canvasRoot);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var eventRoot = new GameObject(
                "IDEA0027.EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            owned.Add(eventRoot);
            EventSystem eventSystem = eventRoot.GetComponent<EventSystem>();
            EventSystem.current = eventSystem;
            InputSystemUIInputModule inputModule =
                eventRoot.GetComponent<InputSystemUIInputModule>();
            inputModule.enabled = false;
            inputModule.AssignDefaultActions();
            inputModule.enabled = true;
            Assert.That(inputModule.point?.action?.enabled, Is.True);
            Assert.That(inputModule.leftClick?.action?.enabled, Is.True);
            var hudRoot = new GameObject("IDEA0027.Hud");
            owned.Add(hudRoot);
            GrayboxDefenseHudView3D hud =
                hudRoot.AddComponent<GrayboxDefenseHudView3D>();
            hud.Configure(canvas, eventSystem);

            GrayboxBuildingInstance3D laser = CompletedBuilding(
                "building.instance.idea0027-input-laser",
                BuildingCatalog.LaserTower);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { laser });
            var tower = new GrayboxDefenseTowerSnapshot3D(
                laser.StableInstanceId,
                5,
                10,
                12f,
                true,
                true,
                false,
                null,
                GrayboxDefenseTowerStatus3D.NoTarget);
            var defense = new GrayboxDefenseRuntimeSnapshot3D(
                0,
                WavePhase.Idle,
                0f,
                0,
                0,
                0,
                2000,
                2000,
                new[] { tower },
                Array.Empty<GrayboxDefenseEnemySnapshot3D>());
            GrayboxDefenseSelectionSnapshot3D selected =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Tower,
                    laser.StableInstanceId,
                    defense,
                    new[] { laser },
                    health,
                    ProductionObservabilitySnapshot.Empty,
                    technologyState: null,
                    energyOverloadUnlocked: true);
            string requested = null;
            hud.TechnologyOverloadRequested += value => requested = value;
            hud.Apply(
                defense,
                GrayboxDefenseSelectionKind3D.Tower,
                laser.StableInstanceId,
                selected);
            yield return null;

            Button button = GameObject.Find(
                    "DefenseDetails.TechnologyOverloadButton")
                .GetComponent<Button>();
            Assert.That(button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                null,
                button.GetComponent<RectTransform>().TransformPoint(
                    button.GetComponent<RectTransform>().rect.center));
            var pointer = new PointerEventData(eventSystem)
            {
                position = screen,
            };
            var raycasts = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, raycasts);
            Assert.That(raycasts.Exists(value =>
                value.gameObject == button.gameObject), Is.True,
                "真实 uGUI 射线必须命中过载按钮；实际：" +
                string.Join(",", raycasts.ConvertAll(value =>
                    value.gameObject?.name ?? "null")));
            Assert.That(
                raycasts[0].gameObject == button.gameObject ||
                raycasts[0].gameObject.transform.IsChildOf(button.transform),
                Is.True,
                "过载按钮被其他 uGUI 控件遮挡：" +
                string.Join(",", raycasts.ConvertAll(value =>
                    value.gameObject?.name ?? "null")));
            Queue(screen);
            yield return null;
            Queue(screen, MouseButton.Left);
            yield return null;
            Queue(screen);
            yield return null;

            Assert.That(requested, Is.EqualTo(laser.StableInstanceId));
        }

        private void Queue(Vector2 position, MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue) state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static GrayboxBuildingInstance3D CompletedBuilding(
            string stableId,
            BuildingDefinition definition)
        {
            System.Reflection.ConstructorInfo constructor =
                typeof(GrayboxBuildingInstance3D).GetConstructor(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableId,
                    new PlacedBuilding(definition, 1, 1),
                    new ConstructionProgress(definition.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            System.Reflection.MethodInfo complete =
                typeof(GrayboxBuildingInstance3D).GetMethod(
                    "Complete",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, null);
            return instance;
        }
    }

    public sealed class IDEA0027TechnologyStateDeveloperRuntimeInputTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
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
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene(
                    "IDEA0027.TechnologyState.Empty");
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
            GrayboxFormalPlayModeEntryFixture.AssertRealSaveFilesUnchanged();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeveloperStateSearchAppliesAndClearsLeaderTrait()
        {
            yield return TapKey(Key.Digit0);
            GrayboxDeveloperModifierBootstrap3D developer =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>();
            GrayboxCivilizationExpansionController3D expansion =
                UnityEngine.Object.FindObjectOfType<
                    GrayboxCivilizationExpansionController3D>();
            Assert.That(developer.IsPanelOpen, Is.True);
            Assert.That(expansion, Is.Not.Null);
            Assert.That(expansion.TryInitialize(out string error), Is.True,
                error);

            InputField researchSearch = FindInput("Research Search");
            yield return FocusAndType(researchSearch, "基因剪接");
            Button research = FindButton(
                "Developer.Research.core.research.gene-splicing");
            Assert.That(research.GetComponentInChildren<Text>().text,
                Is.EqualTo("基因剪接"));
            yield return Click(research);
            yield return Click(FindButton("Unlock Research"));

            ScrollRect panelScroll = GameObject.Find(
                    "Developer.Panel.Scroll")
                .GetComponent<ScrollRect>();
            panelScroll.verticalNormalizedPosition = .55f;
            Canvas.ForceUpdateCanvases();
            InputField statusSearch = FindInput("Technology Status Search");
            yield return FocusAndType(statusSearch, "基因强化");
            Button status = FindButton(
                "Developer.TechnologyStatus.biological.trait.gene-splicing");
            Assert.That(status.GetComponentInChildren<Text>().text,
                Is.EqualTo("基因强化"));
            yield return Click(status);
            Assert.That(FindButton("Set Technology Status One Stack")
                .GetComponentInChildren<Text>().text, Is.EqualTo("设为 1 层"));
            Assert.That(FindButton("Fill Technology Status Stacks")
                .GetComponentInChildren<Text>().text, Is.EqualTo("补满状态层数"));
            Assert.That(FindButton("Clear Selected Technology Status")
                .GetComponentInChildren<Text>().text, Is.EqualTo("清除当前状态"));
            Assert.That(FindButton("Expire Selected Technology Status")
                .GetComponentInChildren<Text>().text,
                Is.EqualTo("当前状态立即到期"));
            Assert.That(FindButton("Trigger Technology Overload")
                .GetComponentInChildren<Text>().text,
                Is.EqualTo("触发选中激光塔过载"));
            yield return Click(FindButton("Apply Technology Status Fixture"));

            CharacterLifeRuntime current = expansion.Runtime.FindCharacter(
                expansion.Runtime.Politics.CurrentLeaderId);
            Assert.That(current.HasGeneSplicingTrait, Is.True);
            yield return Click(FindButton("List Technology Status Fixtures"));
            Assert.That(GameObject.Find("Developer Feedback")
                .GetComponentInChildren<Text>().text,
                Does.Contain("基因强化"));
            yield return Click(FindButton(
                "Expire Selected Technology Status"));
            Assert.That(current.HasGeneSplicingTrait, Is.False);
            yield return Click(FindButton("Apply Technology Status Fixture"));
            Assert.That(current.HasGeneSplicingTrait, Is.True);
            yield return Click(FindButton("Clear Selected Technology Status"));
            Assert.That(current.HasGeneSplicingTrait, Is.False);
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

        private IEnumerator FocusAndType(InputField input, string value)
        {
            yield return Click(input);
            for (var index = 0; index < value.Length; index++)
            {
                InputSystem.QueueTextEvent(keyboard, value[index]);
                InputSystem.Update();
                yield return null;
            }
            Assert.That(input.text, Is.EqualTo(value));
        }

        private IEnumerator Click(Selectable selectable)
        {
            Assert.That(selectable, Is.Not.Null);
            RectTransform rect = selectable.GetComponent<RectTransform>();
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            Queue(position);
            yield return null;
            Queue(position, MouseButton.Left);
            yield return null;
            Queue(position);
            yield return null;
        }

        private void Queue(Vector2 position, MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue) state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static Button FindButton(string name)
        {
            GameObject value = GameObject.Find(name);
            Assert.That(value, Is.Not.Null, name);
            return value.GetComponent<Button>();
        }

        private static InputField FindInput(string name)
        {
            GameObject value = GameObject.Find(name);
            Assert.That(value, Is.Not.Null, name);
            return value.GetComponent<InputField>();
        }
    }
}
