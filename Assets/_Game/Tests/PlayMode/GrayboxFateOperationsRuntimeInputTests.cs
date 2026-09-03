using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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

        [UnityTest]
        public IEnumerator IDEA0020_LevelTwoSelectsSlotTwoAndReadsThroughConfirmation()
        {
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
            ConfigureLevelTwoRewindOwners(host);

            yield return OpenFateOperations();
            yield return Click(RequireSceneObject(
                "FateOperations.CreateAnchor"));
            Assert.That(host.RewindAnchorMetadata.Capture().Entries,
                Has.Count.EqualTo(1));
            yield return CloseFateOperationsAndDetails();

            yield return TapKey(Key.F);
            city.Deployment.Tick(10f);
            yield return null;
            CityMode slotTwoMode = city.Mode;

            yield return OpenFateOperations();
            yield return Click(RequireSceneObject(
                "FateOperations.CreateAnchor"));
            Assert.That(host.RewindAnchorMetadata.Capture().Entries,
                Has.Count.EqualTo(2));
            string[] ids = host.RewindAnchorMetadata.Capture().Entries
                .Select(value => value.AnchorId).ToArray();
            Assert.That(ids, Does.Contain(
                GrayboxRewindAnchorService3D.StableAnchorId));
            Assert.That(ids, Does.Contain(
                GrayboxRewindAnchorService3D.SecondStableAnchorId));
            Assert.That(RequireSceneObject(
                "FateOperations.RewindSlot.2").activeInHierarchy, Is.True);
            yield return CloseFateOperationsAndDetails();

            yield return TapKey(Key.F);
            city.Deployment.Tick(10f);
            yield return null;
            Assert.That(city.Mode, Is.Not.EqualTo(slotTwoMode));
            int attentionBeforeRead = host.AttentionRuntime.Value;

            yield return OpenFateOperations();
            yield return Click(RequireSceneObject(
                "FateOperations.RewindSlot.2"));
            yield return Click(RequireSceneObject(
                "FateOperations.ReadAnchor"));
            GameObject confirmation = RequireSceneObject(
                "FateOperations.Confirmation");
            Assert.That(confirmation.activeInHierarchy, Is.True);
            yield return AssertModalBlocksGameplay(
                confirmation,
                building,
                operations,
                city);
            yield return Click(RequireSceneObject("FateOperations.Confirm"));

            Assert.That(city.Mode, Is.EqualTo(slotTwoMode));
            Assert.That(host.AttentionRuntime.Value,
                Is.EqualTo(attentionBeforeRead + 12));
            Assert.That(host.RewindAnchorMetadata.Capture().Entries,
                Has.Count.EqualTo(2));
            Assert.That(host.RewindAnchorMetadata.Capture().Entries
                    .Select(value => value.AnchorId),
                Does.Contain(
                    GrayboxRewindAnchorService3D.SecondStableAnchorId));
        }

        [UnityTest]
        public IEnumerator IDEA0028_RealGenericActionStartsOnlyLocalHasteDomain()
        {
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);

            yield return SelectFate(FormalFateCatalog.LocalHasteId);
            yield return OpenFateOperations();
            GameObject action = RequireSceneObject(
                "FateOperations.GenericAction");
            Assert.That(action.activeInHierarchy, Is.True);
            Assert.That(action.GetComponent<Button>(), Is.Not.Null);

            float timeScaleBefore = Time.timeScale;
            yield return Click(action);

            Assert.That(host.LocalHasteRuntime.Capture().Active, Is.False,
                "首次真实点击只能打开二次确认，不能直接执行命轨动作。");
            GameObject confirmation = RequireSceneObject(
                "FateOperations.Confirmation");
            Assert.That(confirmation.activeInHierarchy, Is.True);
            yield return Click(RequireSceneObject("FateOperations.Confirm"));

            LocalHasteSnapshot state = host.LocalHasteRuntime.Capture();
            Assert.That(state.Active, Is.True);
            Assert.That(state.TargetId, Is.EqualTo("production"));
            Assert.That(state.CurrentCycleOrdinal, Is.GreaterThan(0ul));
            Assert.That(state.RemainingBudgetSeconds,
                Is.GreaterThan(0f).And.LessThanOrEqualTo(
                    LocalHasteRuntime.LevelOneBudgetSeconds));
            Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore),
                "局部时加不得修改全局 Time.timeScale。");
        }

        [UnityTest]
        public IEnumerator IDEA0028_FailedGenericActionShowsConcreteReason()
        {
            yield return SelectFate(FormalFateCatalog.VoidChestId);
            yield return OpenFateOperations();
            yield return Click(RequireSceneObject(
                "FateOperations.GenericAction"));
            yield return Click(RequireSceneObject("FateOperations.Confirm"));

            Text details = RequireSceneObject("FateOperations.Details")
                .GetComponent<Text>();
            Assert.That(details, Is.Not.Null);
            Assert.That(details.text,
                Does.Contain("反馈：").And.Contain("待领取"),
                "失败动作必须在正式命轨面板显示具体可行动原因。");
        }

        private static void ConfigureLevelTwoRewindOwners(
            GrayboxFormalSaveRuntimeHost3D host)
        {
            Assert.That(host.FateRuntime.TryPromoteToLevelTwo(
                out string error), Is.True, error);
            Assert.That(host.RewindAnchorMetadata.TrySetFateLevel(
                2, out error), Is.True, error);
            FormalCivilizationAscensionRuntime civilization =
                FindOwnedRuntime<FormalCivilizationAscensionRuntime>(host);
            AdvancementSequenceModel sequence =
                FindOwnedRuntime<AdvancementSequenceModel>(host);
            Assert.That(civilization, Is.Not.Null,
                "Host must own the selected fate's civilization runtime.");
            Assert.That(sequence, Is.Not.Null,
                "Host must own the restorable advancement sequence.");
            Assert.That(civilization.TryRestore(
                new FormalCivilizationAscensionSnapshot(
                    2,
                    FormalFateCatalog.RewindAnchorId,
                    2,
                    true,
                    1ul),
                out error), Is.True, error);
            sequence.Restore(
                (int)AdvancementSequenceStage.Continued,
                0f);
        }

        private static T FindOwnedRuntime<T>(object owner)
            where T : class
        {
            const BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = owner.GetType().GetProperties(flags)
                .FirstOrDefault(value => typeof(T).IsAssignableFrom(
                    value.PropertyType) && value.GetIndexParameters().Length == 0);
            if (property?.GetValue(owner) is T fromProperty)
                return fromProperty;
            FieldInfo field = owner.GetType().GetFields(flags)
                .FirstOrDefault(value => typeof(T).IsAssignableFrom(
                    value.FieldType));
            return field?.GetValue(owner) as T;
        }

        private IEnumerator CloseFateOperationsAndDetails()
        {
            yield return TapKey(Key.Escape);
            GameObject details = RequireSceneObject(
                "Progression.AttentionDetails",
                includeInactive: true);
            Assert.That(details.activeInHierarchy, Is.True);
            yield return TapKey(Key.Escape);
            Assert.That(details.activeSelf, Is.False);
        }

        private IEnumerator SelectRewindFate()
        {
            yield return SelectFate(FormalFateCatalog.RewindAnchorId);
        }

        private IEnumerator SelectFate(string fateId)
        {
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            FormalFateSnapshot pending = host.FateRuntime.Capture();
            if (!pending.OfferedIds.Contains(
                    fateId))
            {
                Assert.That(host.FateRuntime.TryRestore(
                    new FormalFateSnapshot(
                        pending.Revision,
                        new[]
                        {
                            fateId,
                            FormalFateCatalog.PocketUniverseId,
                            FormalFateCatalog.VoidDebtId,
                        },
                        string.Empty,
                        0,
                        pending.OfferSelectionVersion),
                    out string restoreError), Is.True, restoreError);
                Assert.That(host.FateSelectionController.RefreshIfChanged(),
                    Is.True);
            }
            yield return Click(RequireSceneObject(
                "FateSelection.Card." + fateId));
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
            Assert.That(
                Vector3.Distance(city.transform.position, before),
                Is.LessThan(.0001f),
                "The modal blocks meaningful city movement; ignore only " +
                "sub-pixel transform noise.");
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
