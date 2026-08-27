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
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class GrayboxAttentionPressureRuntimeInputTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode updateMode;
        private InputSettings.BackgroundBehavior backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            editorInputBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            updateMode = InputSystem.settings.updateMode;
            backgroundBehavior = InputSystem.settings.backgroundBehavior;
            editorInputBehavior =
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
                "GrayboxPrototype3D", LoadSceneMode.Single);
            yield return null;
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(mouse);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene scene = SceneManager.GetSceneByName("GrayboxPrototype3D");
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("Pressure.Empty");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            GrayboxFormalPlayModeEntryFixture.CleanupIsolatedStore();
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = updateMode;
            InputSystem.settings.backgroundBehavior = backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                editorInputBehavior;
            GrayboxFormalPlayModeEntryFixture.AssertRealSaveFilesUnchanged();
        }

        [UnityTest]
        public IEnumerator IDEA0020_RealPressureHudModalAndBossPlaceholderAreVisible()
        {
            GrayboxFormalSaveRuntimeHost3D host =
                Object.FindObjectOfType<GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.AttentionPressureRuntime.TryRestore(
                new AttentionPressureSnapshot(9UL, new[]
                {
                    new AttentionPressureEntrySnapshot(
                        30, AttentionPressureState.Completed, 0f),
                    new AttentionPressureEntrySnapshot(
                        60, AttentionPressureState.Completed, 0f),
                    new AttentionPressureEntrySnapshot(
                        90, AttentionPressureState.Warning, .1f),
                }), out string error), Is.True, error);
            Assert.That(host.AttentionPressureRuntime.Tick(
                .1f, false, true, true,
                out AttentionPressureCommand start,
                out error), Is.True, error);
            GrayboxDefenseController3D defense =
                Object.FindObjectOfType<GrayboxDefenseController3D>();
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            Assert.That(defense, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            SetMainCampaign(defense.Runtime, WonCampaign());
            using (var pressureDefense =
                   new GrayboxAttentionPressureDefenseController3D(
                       host.AttentionPressureRuntime,
                       defense.Runtime))
            {
                Assert.That(pressureDefense.TryHandle(start, out error),
                    Is.True, error);
                defense.Runtime.Tick(5.1f, false, session.CityStorage);
            }
            yield return null;

            Text status = Require("Progression.AttentionPressure.Status")
                .GetComponentInChildren<Text>();
            Assert.That(status.text, Does.Contain("进行中"));
            GameObject boss = Require("CrystalBroodmother.Placeholder");
            Assert.That(boss.activeInHierarchy, Is.True);
            Assert.That(Require("CrystalBroodmother.Outline"), Is.Not.Null);
            Assert.That(Require("CrystalBroodmother.WorldHealthBar"), Is.Not.Null);
            Assert.That(Require("CrystalBroodmother.Phase").GetComponent<TextMesh>()
                .text, Does.Contain("阶段"));

            yield return Click(Require("Progression.AttentionPressure.Status"));
            GameObject details = Require("Progression.AttentionPressure.Details");
            Assert.That(details.activeInHierarchy, Is.True);
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            yield return Tap(Key.B);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(details.activeInHierarchy, Is.True);
            yield return Tap(Key.Escape);
            Assert.That(details.activeSelf, Is.False);
        }

        private static SingleCityDefenseCampaignModel WonCampaign()
        {
            var definition = new SingleCityDefenseCampaignDefinition(
                "test.pressure-play.main-victory",
                new CampaignWaveDefinition(1, 0f, .1f,
                    new[] { CampaignSpawnDirection.East },
                    new WaveEntry(EnemyArchetype.Gnawer, 1)));
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f,
                definition);
            campaign.TryStartAfterExternalWarning();
            campaign.Advance(.2f, 1);
            campaign.DefeatEnemy(
                campaign.Snapshot.Enemies.Single().StableId,
                BuildingCatalog.MachineGunTurret.Id.Value);
            campaign.Advance(.1f, 1);
            Assert.That(campaign.Snapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
            return campaign;
        }

        private static void SetMainCampaign(
            GrayboxDefenseRuntime3D runtime,
            SingleCityDefenseCampaignModel campaign)
        {
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "campaign", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(runtime, campaign);
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update(); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update(); yield return null;
        }

        private IEnumerator Click(GameObject target)
        {
            Assert.That(Object.FindObjectOfType<InputSystemUIInputModule>(),
                Is.Not.Null);
            RectTransform rect = target.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null, rect.TransformPoint(rect.rect.center));
            Queue(position); yield return null;
            Queue(position, MouseButton.Left); yield return null;
            Queue(position); yield return null;
        }

        private void Queue(Vector2 position, MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue) state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update();
        }

        private static GameObject Require(string name)
        {
            GameObject value = Object.FindObjectsOfType<Transform>(true)
                .Select(item => item.gameObject)
                .FirstOrDefault(item => item.name == name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }
    }
}
