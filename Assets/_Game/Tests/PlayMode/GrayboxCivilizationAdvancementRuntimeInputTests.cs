using System.Collections;
using System.Linq;
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
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Progression;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class GrayboxCivilizationAdvancementRuntimeInputTests
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
                        "CivilizationAdvancement.Empty");
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
                GrayboxFormalPlayModeEntryFixture
                    .AssertRealSaveFilesUnchanged();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0020_RealUAdvancesOnceAndWarningResultsRoundTrip()
        {
            GrayboxFormalSaveRuntimeHost3D host = RequireHost();
            yield return EstablishFourRequirements(host);
            FormalCivilizationAscensionRequirements requirements =
                host.CaptureRequirements();
            Assert.That(requirements.CanAscend, Is.True,
                string.Join(" | ", requirements.Statuses
                    .Where(value => !value.IsMet)
                    .Select(value => value.MissingText)));
            int attentionBefore = host.AttentionRuntime.Value;

            yield return TapKey(Key.U);
            Assert.That(host.Sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.Scanning));
            Assert.That(host.Civilization.Capture().CivilizationLevel,
                Is.EqualTo(2));
            Assert.That(host.FateRuntime.Capture().Level, Is.EqualTo(2));
            Assert.That(host.AttentionRuntime.Value,
                Is.EqualTo(System.Math.Min(
                    FormalAttentionCatalog.MaximumValue,
                    attentionBefore + 25)));
            Assert.That(Require(
                "CivilizationAdvancement.Modal").activeInHierarchy,
                Is.True);
            host.RuleClock.SetDevelopmentAcceleration(20f);

            int committedAttention = host.AttentionRuntime.Value;
            yield return TapKey(Key.U);
            Assert.That(host.AttentionRuntime.Value,
                Is.EqualTo(committedAttention),
                "Repeated U during the sequence cannot commit rewards twice.");

            yield return AssertAdvancementBlocksGameplay();
            yield return WaitForStage(
                host, AdvancementSequenceStage.Confirmed);
            yield return WaitForStage(
                host, AdvancementSequenceStage.Warning);
            Assert.That(Require("CivilizationAdvancement.Stage")
                    .GetComponentInChildren<Text>().text,
                Does.Contain("警告"));

            GrayboxFormalSaveEntryController3D entry = RequireEntry();
            GrayboxFormalSaveUiResult3D warningSave = entry.SaveAndExit();
            Assert.That(warningSave.Success, Is.True, warningSave.Message);
            yield return ReloadAndContinue();
            host = RequireHost();
            Assert.That(host.Sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.Warning));
            AssertCommittedOnce(host, committedAttention);
            host.RuleClock.SetDevelopmentAcceleration(20f);

            yield return WaitForStage(
                host, AdvancementSequenceStage.Results);
            Assert.That(Require("CivilizationAdvancement.Continue")
                    .activeInHierarchy,
                Is.True);
            entry = RequireEntry();
            GrayboxFormalSaveUiResult3D resultsSave = entry.SaveAndExit();
            Assert.That(resultsSave.Success, Is.True, resultsSave.Message);
            yield return ReloadAndContinue();
            host = RequireHost();
            Assert.That(host.Sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.Results));
            AssertCommittedOnce(host, committedAttention);

            yield return TapKey(Key.U);
            Assert.That(host.Sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.Continued));
            Assert.That(host.Speed.IsPaused(
                WasteCity.Core.GamePauseReason.Advancement), Is.False);
            Assert.That(Require(
                "CivilizationAdvancement.Modal").activeSelf, Is.False);
        }

        private IEnumerator EstablishFourRequirements(
            GrayboxFormalSaveRuntimeHost3D host)
        {
            GrayboxBuildingSession3D session = Object.FindObjectOfType<
                GrayboxBuildingSession3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxProductionController3D production =
                Object.FindObjectOfType<GrayboxProductionController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            Assert.That(session, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(production, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(world, Is.Not.Null);

            session.UnlockResearchForDevelopment(
                FormalCivilizationAscensionRuntime.LegacyAnalysisResearchId);
            Assert.That(city.RestoreDeploymentForDevelopment(
                CityMode.Fortress), Is.True);
            Assert.That(world.Coordinates.TryWorldToCell(
                city.transform.position, out int cityX, out int cityY),
                Is.True);
            var entries = new[]
            {
                Completed(1, BuildingCatalog.MachineGunTurret,
                    cityX - 5, cityY),
                Completed(2, BuildingCatalog.MachineGunTurret,
                    cityX, cityY - 5),
                Completed(3, BuildingCatalog.Assembler,
                    cityX + 4, cityY),
            };
            Assert.That(session.TryRestoreBuildings(
                entries, 4, presentation, out string error), Is.True, error);
            Assert.That(production.TryRebuildAfterPersistenceRestore(
                out error), Is.True, error);
            Assert.That(production.Snapshot.HasCurrentlyRunnableBuilding,
                Is.True, "The restored assembler must be currently runnable.");
            Assert.That(host.AttentionPressureRuntime.TryRestore(
                new AttentionPressureSnapshot(3UL, new[]
                {
                    new AttentionPressureEntrySnapshot(
                        30, AttentionPressureState.Completed, 0f),
                    new AttentionPressureEntrySnapshot(
                        60, AttentionPressureState.Completed, 0f),
                    new AttentionPressureEntrySnapshot(
                        90, AttentionPressureState.Completed, 0f),
                }), out error), Is.True, error);
            yield return null;
        }

        private IEnumerator AssertAdvancementBlocksGameplay()
        {
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            Assert.That(building, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Vector3 before = city.transform.position;
            yield return TapKey(Key.B);
            yield return TapKey(Key.E);
            yield return TapKey(Key.T);
            yield return HoldKey(Key.W, 2);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(operations.IsAnyPanelOpen, Is.False);
            Assert.That(city.transform.position, Is.EqualTo(before));
        }

        private static GrayboxBuildingRestoreEntry3D Completed(
            int ordinal,
            BuildingDefinition definition,
            int x,
            int y)
        {
            return new GrayboxBuildingRestoreEntry3D(
                "building.instance." + ordinal.ToString("D6"),
                definition,
                BuildingSite.Ground,
                x,
                y,
                BuildingOrientation.North,
                GrayboxBuildingInstanceState.Completed,
                0f,
                isPlayerOwned: true,
                isEvacuationLocked: false,
                boundResourceNode: default);
        }

        private static void AssertCommittedOnce(
            GrayboxFormalSaveRuntimeHost3D host,
            int expectedAttention)
        {
            Assert.That(host.AttentionRuntime.Value,
                Is.EqualTo(expectedAttention));
            Assert.That(host.Civilization.Capture().CivilizationLevel,
                Is.EqualTo(2));
            Assert.That(host.Civilization.Capture().Ascended, Is.True);
            Assert.That(host.FateRuntime.Capture().Level, Is.EqualTo(2));
        }

        private IEnumerator WaitForStage(
            GrayboxFormalSaveRuntimeHost3D host,
            AdvancementSequenceStage expected)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while ((int)host.Sequence.Stage < (int)expected &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(host.Sequence.Stage, Is.EqualTo(expected));
        }

        private IEnumerator ReloadAndContinue()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return Click("Start.Continue");
            yield return null;
        }

        private IEnumerator HoldKey(Key key, int frames)
        {
            for (var index = 0; index < frames; index++)
            {
                InputSystem.QueueStateEvent(
                    keyboard, new KeyboardState(key));
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

        private IEnumerator Click(string name)
        {
            Button button = Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(value => value.name == name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.interactable, Is.True, name);
            InputSystemUIInputModule module = Object.FindObjectOfType<
                InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null, rect.TransformPoint(rect.rect.center));
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

        private static GrayboxFormalSaveRuntimeHost3D RequireHost()
        {
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.IsInitialized, Is.True);
            return host;
        }

        private static GrayboxFormalSaveEntryController3D RequireEntry()
        {
            GrayboxFormalSaveEntryController3D entry =
                Object.FindObjectOfType<
                    GrayboxFormalSaveEntryController3D>(true);
            Assert.That(entry, Is.Not.Null);
            return entry;
        }

        private static GameObject Require(string name)
        {
            GameObject value = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(candidate => candidate.name == name &&
                    candidate.scene.IsValid());
            Assert.That(value, Is.Not.Null, name);
            return value;
        }
    }
}
