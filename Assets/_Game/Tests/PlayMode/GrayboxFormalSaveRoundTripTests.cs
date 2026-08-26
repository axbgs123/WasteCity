using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;
using WasteCity.Research;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    /// <summary>
    /// Task 13 formal 3D disk/scene round trips. Deterministic resource and
    /// rule-time fixtures may shorten setup, but every state transition,
    /// building placement, research choice, evacuation command, save and
    /// continue below still travels through the real Input System or UGUI.
    /// </summary>
    public sealed class GrayboxFormalSaveRoundTripTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private const float FastRuleTimeScale = 20f;
        private const float TransitionDriftTolerance = .35f;
        private const int FormalSaveIdleFrameCount = 300;
        private const string TowerPauseButtonName =
            "DefenseDetails.TowerPauseButton";

        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private float previousTimeScale;
        private string saveDirectory;
        private string realSaveDirectory;
        private Dictionary<string, FormalSaveFileSnapshot>
            originalRealSaveFiles;
        private MethodInfo configureStoreRootForTesting;
        private int emptySceneOrdinal;
        private FakeExit configuredExit;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            realSaveDirectory = Application.persistentDataPath;
            originalRealSaveFiles = CaptureFormalSaveFileSnapshots(
                realSaveDirectory);
            configureStoreRootForTesting =
                RequireConfigureStoreRootForTesting();
            saveDirectory = Path.Combine(
                Path.GetTempPath(),
                "wastecity-formal-save-round-trip-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            configureStoreRootForTesting.Invoke(
                null,
                new object[] { saveDirectory });

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
            yield return LoadScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadScene();
            if (configureStoreRootForTesting != null)
            {
                configureStoreRootForTesting.Invoke(
                    null,
                    new object[] { null });
            }
            if (!string.IsNullOrWhiteSpace(saveDirectory) &&
                Directory.Exists(saveDirectory))
            {
                Directory.Delete(saveDirectory, true);
            }
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

            Dictionary<string, FormalSaveFileSnapshot> actualRealSaveFiles =
                CaptureFormalSaveFileSnapshots(realSaveDirectory);
            AssertFormalSaveFilesUnchanged(
                originalRealSaveFiles,
                actualRealSaveFiles);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0015_ActiveDeployingRoundTripsThroughRealSave()
        {
            yield return StartNewGame();
            GrayboxMobileCityController3D city = RequireCity();
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));

            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Deploying));
            ConfigureFakeExit();
            yield return EnsureSystemMenuOpen();
            CityTransitionFingerprint before =
                CityTransitionFingerprint.Capture(city);
            FormalThreeDSaveData beforeAuthority = CaptureFullAuthority();

            yield return SaveThroughRealMenu();
            yield return ReloadAndContinue();
            yield return EnsureSystemMenuOpen();
            city = RequireCity();
            CityTransitionFingerprint after =
                CityTransitionFingerprint.Capture(city);

            AssertTransitionRoundTrip(before, after, CityMode.Deploying);
            AssertFullAuthorityEquivalent(beforeAuthority,
                CaptureFullAuthority(), "deploying");
            AssertTransitionDerivedState(city);
        }

        [UnityTest]
        public IEnumerator IDEA0015_ActivePackingRoundTripsThroughRealSave()
        {
            yield return StartNewGame();
            GrayboxMobileCityController3D city = RequireCity();
            SetDevelopmentRuleTime(FastRuleTimeScale);
            yield return TapKey(Key.F);
            yield return WaitForCityMode(city, CityMode.Fortress, 4f);
            SetDevelopmentRuleTime(1f);

            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Packing));
            ConfigureFakeExit();
            yield return EnsureSystemMenuOpen();
            CityTransitionFingerprint before =
                CityTransitionFingerprint.Capture(city);
            FormalThreeDSaveData beforeAuthority = CaptureFullAuthority();

            yield return SaveThroughRealMenu();
            yield return ReloadAndContinue();
            yield return EnsureSystemMenuOpen();
            city = RequireCity();
            CityTransitionFingerprint after =
                CityTransitionFingerprint.Capture(city);

            AssertTransitionRoundTrip(before, after, CityMode.Packing);
            AssertFullAuthorityEquivalent(beforeAuthority,
                CaptureFullAuthority(), "packing");
            AssertTransitionDerivedState(city);
        }

        [UnityTest]
        public IEnumerator IDEA0015_ExactEightEnemyCombatRoundTripsAndRebuildsTargets()
        {
            yield return StartNewGame();
            yield return PrepareFormalCombatThroughRealInput();
            GrayboxDefenseController3D defense = RequireDefense();
            GrayboxDefenseWorldView3D worldView = defense.WorldView;
            Assert.That(defense.Snapshot.SpawnedEnemyCount, Is.EqualTo(8));
            Assert.That(defense.Snapshot.AliveEnemyCount, Is.EqualTo(8));

            ConfigureFakeExit();
            yield return EnsureSystemMenuOpen();
            GrayboxDefensePersistenceState3D before =
                defense.Runtime.CaptureForPersistence();
            GrayboxFormalDefenseCampaignPersistenceState3D beforeCampaign =
                defense.Runtime.CaptureFormalCampaignForPersistence();
            FormalThreeDSaveData beforeAuthority = CaptureFullAuthority();
            Assert.That(beforeCampaign.Campaign.Enemies,
                Has.Count.EqualTo(8));

            yield return SaveThroughRealMenu();
            yield return ReloadAndContinue();
            yield return EnsureSystemMenuOpen();
            defense = RequireDefense();
            GrayboxDefensePersistenceState3D after =
                defense.Runtime.CaptureForPersistence();
            GrayboxFormalDefenseCampaignPersistenceState3D afterCampaign =
                defense.Runtime.CaptureFormalCampaignForPersistence();

            AssertDefenseAuthorityEquivalent(before, after);
            AssertFullAuthorityEquivalent(beforeAuthority,
                CaptureFullAuthority(), "combat");
            Assert.That(defense.Snapshot.SpawnedEnemyCount, Is.EqualTo(8));
            Assert.That(defense.Snapshot.AliveEnemyCount, Is.EqualTo(8));
            Assert.That(defense.HasSelection, Is.False,
                "Transient world selection must be rebuilt, not persisted.");
            worldView = defense.WorldView;
            Assert.That(afterCampaign.Campaign.Enemies,
                Has.Count.EqualTo(beforeCampaign.Campaign.Enemies.Count));
            foreach (SingleCityDefenseCampaignEnemyPersistenceState enemy in
                     afterCampaign.Campaign.Enemies)
            {
                Assert.That(worldView.TryGetEnemyObject(
                    enemy.StableId,
                    out GameObject enemyObject), Is.True, enemy.StableId);
                Assert.That(enemyObject, Is.Not.Null, enemy.StableId);
            }
            Assert.That(defense.Hud, Is.Not.Null);
            Assert.That(defense.Hud.SummaryText, Is.Not.Null);
            Assert.That(defense.Hud.SummaryText.text,
                Does.Contain("敌人 8"));
            GrayboxProductionController3D production = Object.FindObjectOfType<
                GrayboxProductionController3D>();
            Assert.That(production, Is.Not.Null);
            ProductionBuildingObservability[] restoredProduction =
                production.Snapshot.Entries.Where(value =>
                    value.BuildingDefinitionId ==
                        BuildingCatalog.Smelter.Id.Value ||
                    value.BuildingDefinitionId ==
                        BuildingCatalog.Assembler.Id.Value).ToArray();
            Assert.That(restoredProduction, Has.Length.EqualTo(2));
            Assert.That(restoredProduction.All(value =>
                value.IsLogisticsConnected), Is.True,
                "Logistics connectivity must be rebuilt after Continue.");
            Assert.That(restoredProduction.All(value =>
                value.StopReason != ProductionStopReason.OutOfLogistics),
                Is.True,
                "Production stop reasons must be recomputed from rebuilt " +
                "logistics, not loaded as stale UI state.");

            yield return ClickButton("Main.Continue");
            GrayboxDefenseTowerSnapshot3D tower =
                defense.Snapshot.Towers.Single();
            Assert.That(worldView.TryGetTowerObject(
                tower.StableId,
                out GameObject towerObject), Is.True);
            yield return ClickWorldObject(towerObject);
            yield return ClickUiElement(RequireSceneObject(
                TowerPauseButtonName));
            float deadline = Time.realtimeSinceStartup + 2f;
            while ((defense.Snapshot.Towers.Single().Status !=
                        GrayboxDefenseTowerStatus3D.Firing ||
                    string.IsNullOrWhiteSpace(
                        defense.Snapshot.Towers.Single().TargetId)) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            tower = defense.Snapshot.Towers.Single();
            Assert.That(tower.Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(
                afterCampaign.Campaign.Enemies.Select(
                    value => value.StableId),
                Does.Contain(tower.TargetId),
                "The target is derived again from restored combat truth.");
            Assert.That(
                RequireSceneObject("DefenseDetailsPanel").activeInHierarchy,
                Is.True);
        }

        [UnityTest]
        public IEnumerator IDEA0015_ConfirmedEvacuationProcessingRoundTripsAndResumes()
        {
            yield return StartNewGame();
            GrayboxBuildingInstance3D wall = null;
            yield return PrepareSingleWallThroughRealInput(
                value => wall = value);
            GrayboxEvacuationController3D evacuation = RequireEvacuation();

            SetDevelopmentRuleTime(1f);
            yield return TapKey(Key.F);
            Assert.That(evacuation.IsManifestOpen, Is.True);
            yield return SubmitButton(
                "Evacuation.Item." + wall.StableInstanceId +
                ".FullDismantle");
            EvacuationManifestViewModel assigned =
                evacuation.CaptureManifestView();
            Assert.That(assigned.CanConfirm, Is.True,
                assigned.FailureReason);
            Assert.That(assigned.Items.Single().Treatment,
                Is.EqualTo(BuildingEvacuationTreatment.FullDismantle));
            yield return SubmitButton("Evacuation.Confirm");
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(wall.IsEvacuationLocked, Is.True);
            ConfigureFakeExit();
            yield return EnsureSystemMenuOpen();
            GrayboxEvacuationPersistenceState3D before =
                evacuation.CaptureForPersistence();
            FormalThreeDSaveData beforeAuthority = CaptureFullAuthority();
            Assert.That(before.IsProcessing, Is.True);
            Assert.That(before.IsBlocked, Is.False);

            yield return SaveThroughRealMenu();
            yield return ReloadAndContinue();
            yield return EnsureSystemMenuOpen();
            evacuation = RequireEvacuation();
            GrayboxEvacuationPersistenceState3D after =
                evacuation.CaptureForPersistence();

            AssertEvacuationAuthorityEquivalent(before, after);
            AssertFullAuthorityEquivalent(beforeAuthority,
                CaptureFullAuthority(), "evacuation-processing");
            Assert.That(after.IsProcessing, Is.True);
            Assert.That(after.IsBlocked, Is.False);
            AssertEvacuationQueueUi(after, blocked: false);
            GrayboxBuildingInstance3D restoredWall = RequireSession()
                .Instances.Single(value =>
                    value.StableInstanceId == after.CurrentStableInstanceId);
            Assert.That(restoredWall.IsEvacuationLocked, Is.True);

            float remainingBeforeResume = after.RemainingSeconds;
            yield return ClickButton("Main.Continue");
            float deadline = Time.realtimeSinceStartup + 1f;
            while (evacuation.IsProcessing &&
                   evacuation.CaptureForPersistence().RemainingSeconds >=
                       remainingBeforeResume &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(
                evacuation.CaptureForPersistence().RemainingSeconds,
                Is.LessThan(remainingBeforeResume),
                "Restored evacuation must resume the saved active item.");
        }

        [UnityTest]
        public IEnumerator IDEA0015_CapacityBlockedEvacuationSurvivesFiveRealReloads()
        {
            yield return StartNewGame();
            GrayboxBuildingInstance3D wall = null;
            yield return PrepareSingleWallThroughRealInput(
                value => wall = value);
            GrayboxBuildingSession3D session = RequireSession();
            GrayboxEvacuationController3D evacuation = RequireEvacuation();
            GrayboxDeveloperModifier3D fixture = RequireModifier();

            SetDevelopmentRuleTime(1f);
            yield return TapKey(Key.F);
            Assert.That(evacuation.IsManifestOpen, Is.True);
            yield return SubmitButton(
                "Evacuation.Item." + wall.StableInstanceId +
                ".FullDismantle");
            EvacuationManifestViewModel assigned =
                evacuation.CaptureManifestView();
            Assert.That(assigned.CanConfirm, Is.True,
                assigned.FailureReason);
            Assert.That(assigned.Items.Single().Treatment,
                Is.EqualTo(BuildingEvacuationTreatment.FullDismantle));
            yield return SubmitButton("Evacuation.Confirm");
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(fixture.SetResource(
                ResourceIds.Stone,
                session.CityStorage.GetNetworkCapacityLimit(
                    ResourceIds.Stone)), Is.True);

            float deadline = Time.realtimeSinceStartup + 4f;
            while (!evacuation.IsBlocked &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(evacuation.IsBlocked, Is.True);
            Assert.That(wall.IsEvacuationLocked, Is.True);
            ConfigureFakeExit();
            yield return EnsureSystemMenuOpen();
            GrayboxEvacuationPersistenceState3D expected =
                evacuation.CaptureForPersistence();
            Assert.That(expected.IsBlocked, Is.True);
            Assert.That(expected.BlockedStableInstanceId,
                Is.EqualTo(wall.StableInstanceId));
            EvacuationQueueViewModel expectedView =
                evacuation.CaptureQueueView();
            Assert.That(expectedView.CapacityShortfalls, Is.Not.Empty);
            FormalThreeDSaveData expectedAuthority = CaptureFullAuthority();

            yield return SaveThroughRealMenu();
            Dictionary<string, FormalSaveFileSnapshot> reloadSaveFiles =
                CaptureFormalSaveFileSnapshots(saveDirectory);

            for (var reload = 1; reload <= 5; reload++)
            {
                yield return ReloadAndContinue();
                AssertFormalSaveFilesUnchanged(
                    reloadSaveFiles,
                    CaptureFormalSaveFileSnapshots(saveDirectory));
                evacuation = RequireEvacuation();
                GrayboxEvacuationPersistenceState3D actual =
                    evacuation.CaptureForPersistence();
                AssertEvacuationAuthorityEquivalent(expected, actual);
                AssertFullAuthorityEquivalent(
                    expectedAuthority,
                    CaptureFullAuthority(),
                    "capacity-blocked reload " + reload);
                Assert.That(actual.IsBlocked, Is.True,
                    "reload " + reload);
                EvacuationQueueViewModel actualView =
                    evacuation.CaptureQueueView();
                Assert.That(actualView.IsBlocked, Is.True,
                    "reload " + reload);
                Assert.That(actualView.CanRetry, Is.True,
                    "reload " + reload);
                CollectionAssert.AreEqual(
                    expectedView.CapacityShortfalls,
                    actualView.CapacityShortfalls,
                    "Derived capacity shortfalls must be recomputed from " +
                    "the restored storage on reload " + reload + ".");
                AssertEvacuationQueueUi(actual, blocked: true);
                Button retry = FindButton("Evacuation.Retry");
                Assert.That(retry, Is.Not.Null, "reload " + reload);
                Assert.That(
                    retry.interactable,
                    Is.True,
                    "reload " + reload);
                Assert.That(
                    RequireSession().Instances.Single(value =>
                        value.StableInstanceId ==
                            actual.CurrentStableInstanceId)
                        .IsEvacuationLocked,
                    Is.True,
                    "reload " + reload);
            }

            Dictionary<string, FormalSaveFileSnapshot> idleSaveFiles =
                CaptureFormalSaveFileSnapshots(saveDirectory);
            int idleSceneObjectCount = CountFormalSceneObjects();
            for (var frame = 0;
                 frame < FormalSaveIdleFrameCount;
                 frame++)
            {
                yield return null;
            }
            AssertFormalSaveFilesUnchanged(
                idleSaveFiles,
                CaptureFormalSaveFileSnapshots(saveDirectory));
            Assert.That(
                CountFormalSceneObjects(),
                Is.EqualTo(idleSceneObjectCount),
                "The blocked formal save scene grew persistent objects " +
                "across 300 real PlayMode frames.");
            Assert.That(RequireEvacuation().IsBlocked, Is.True);
        }

        private IEnumerator StartNewGame()
        {
            GrayboxFormalSaveEntryController3D entry = RequireEntry();
            Assert.That(entry.IsStartPageOpen, Is.True);
            yield return ClickButton("Start.NewGame");
            if (entry.IsNewGameConfirmationOpen)
                yield return ClickButton("Start.NewGameConfirm");
            Assert.That(entry.IsRuntimeReady, Is.True, entry.FeedbackMessage);
            Assert.That(entry.IsStartPageOpen, Is.False);
            yield return ClickButton(
                "FateSelection.Card." +
                FormalFateCatalog.RewindAnchorId);
            yield return ClickButton("FateSelection.Confirm");
            SetDevelopmentRuleTime(1f);
        }

        private IEnumerator PrepareSingleWallThroughRealInput(
            Action<GrayboxBuildingInstance3D> capture)
        {
            GrayboxBuildingSession3D session = RequireSession();
            GrayboxDeveloperModifier3D fixture = RequireModifier();
            Assert.That(fixture.SetResource(ResourceIds.Stone, 100), Is.True);
            Assert.That(fixture.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast100), Is.True);
            yield return EnterFortressThroughRealInput();

            GrayboxMobileCityController3D city = RequireCity();
            GrayboxWorldView3D world = RequireWorld();
            GrayboxBuildingPlacementController3D placement =
                RequirePlacement();
            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit4);
            Assert.That(
                RequireInteraction().Selected,
                Is.SameAs(BuildingCatalog.Wall));
            yield return MoveToValidGroundPreview(
                city,
                world,
                placement);
            int beforeCount = session.Instances.Count;
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(session.Instances.Count, Is.EqualTo(beforeCount + 1));
            GrayboxBuildingInstance3D wall = session.Instances[beforeCount];
            yield return WaitForCompletion(wall, 3f);
            Assert.That(fixture.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Normal), Is.True);
            GrayboxBuildingInteractionModel3D interaction =
                RequireInteraction();
            for (var close = 0;
                 close < 2 && interaction.State !=
                     GrayboxBuildingInteractionState.Inactive;
                 close++)
            {
                yield return TapKey(Key.Escape);
            }
            Assert.That(interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            capture?.Invoke(wall);
        }

        private IEnumerator PrepareFormalCombatThroughRealInput()
        {
            GrayboxBuildingSession3D session = RequireSession();
            GrayboxDeveloperModifier3D fixture = RequireModifier();
            for (var index = 0; index < ResourceIds.All.Length; index++)
            {
                Assert.That(
                    fixture.SetResource(ResourceIds.All[index], 0),
                    Is.True,
                    ResourceIds.All[index]);
            }
            Assert.That(fixture.SetResource(ResourceIds.Iron, 16), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Alloy, 100), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Stone, 20), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Biomass, 10), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Ammunition, 40),
                Is.True);
            Assert.That(fixture.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast100), Is.True);
            yield return EnterFortressThroughRealInput();
            SetDevelopmentRuleTime(FastRuleTimeScale);

            GrayboxMobileCityController3D city = RequireCity();
            GrayboxWorldView3D world = RequireWorld();
            GrayboxBuildingPlacementController3D placement =
                RequirePlacement();
            GrayboxBuildingInteractionModel3D interaction =
                RequireInteraction();

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit5);
            Assert.That(interaction.Selected,
                Is.SameAs(BuildingCatalog.ResearchStation));
            yield return MoveToValidInnerPreview(city, placement);
            int stationIndex = session.Instances.Count;
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            GrayboxBuildingInstance3D station =
                session.Instances[stationIndex];
            yield return WaitForCompletion(station, 3f);
            yield return TapKey(Key.Escape);

            yield return TapKey(Key.T);
            yield return ResearchThroughUi(
                session,
                ResearchCatalog.AutomatedMachineryId);
            yield return ResearchThroughUi(
                session,
                ResearchCatalog.PrecisionAssemblyId);
            yield return ResearchThroughUi(
                session,
                ResearchCatalog.AutomatedDefenseId);
            yield return TapKey(Key.T);

            yield return TapKey(Key.B);
            GrayboxBuildingInstance3D smelter = null;
            yield return PlaceQuickbarBuilding(
                Key.Digit6,
                BuildingCatalog.Smelter,
                innerCity: false,
                value => smelter = value);
            Assert.That(smelter, Is.Not.Null);
            GrayboxBuildingInstance3D assembler = null;
            yield return PlaceQuickbarBuilding(
                Key.Digit7,
                BuildingCatalog.Assembler,
                innerCity: true,
                value => assembler = value);
            Assert.That(assembler, Is.Not.Null);
            GrayboxBuildingInstance3D turret = null;
            yield return PlaceQuickbarBuilding(
                Key.Digit8,
                BuildingCatalog.MachineGunTurret,
                innerCity: false,
                value => turret = value);
            Assert.That(turret, Is.Not.Null);

            for (var close = 0;
                 close < 2 && interaction.State !=
                     GrayboxBuildingInteractionState.Inactive;
                 close++)
            {
                yield return TapKey(Key.Escape);
            }
            Assert.That(interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));

            GrayboxDefenseController3D defense = RequireDefense();
            float deadline = Time.realtimeSinceStartup + 2f;
            while ((defense.Snapshot == null ||
                    defense.Snapshot.Towers.All(value =>
                        value.StableId != turret.StableInstanceId)) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(defense.WorldView.TryGetTowerObject(
                turret.StableInstanceId,
                out GameObject towerObject), Is.True);
            yield return ClickWorldObject(towerObject);
            yield return ClickUiElement(RequireSceneObject(
                TowerPauseButtonName));
            Assert.That(
                defense.Snapshot.Towers.Single(value =>
                    value.StableId == turret.StableInstanceId).PlayerPaused,
                Is.True);

            deadline = Time.realtimeSinceStartup + 12f;
            while ((defense.Snapshot.SpawnedEnemyCount < 8 ||
                    defense.Snapshot.AliveEnemyCount < 8) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(defense.Snapshot.SpawnedEnemyCount, Is.EqualTo(8));
            Assert.That(defense.Snapshot.AliveEnemyCount, Is.EqualTo(8));
            SetDevelopmentRuleTime(1f);
        }

        private IEnumerator EnterFortressThroughRealInput()
        {
            GrayboxMobileCityController3D city = RequireCity();
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            SetDevelopmentRuleTime(FastRuleTimeScale);
            yield return TapKey(Key.F);
            Assert.That(city.Mode,
                Is.EqualTo(CityMode.Deploying).Or.EqualTo(CityMode.Fortress));
            yield return WaitForCityMode(city, CityMode.Fortress, 4f);
        }

        private IEnumerator PlaceQuickbarBuilding(
            Key key,
            BuildingDefinition definition,
            bool innerCity,
            Action<GrayboxBuildingInstance3D> capture)
        {
            GrayboxBuildingSession3D session = RequireSession();
            GrayboxBuildingPlacementController3D placement =
                RequirePlacement();
            yield return TapKey(key);
            Assert.That(RequireInteraction().Selected, Is.SameAs(definition));
            if (innerCity)
                yield return MoveToValidInnerPreview(RequireCity(), placement);
            else
                yield return MoveToValidGroundPreview(
                    RequireCity(),
                    RequireWorld(),
                    placement);
            int beforeCount = session.Instances.Count;
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(session.Instances.Count, Is.EqualTo(beforeCount + 1),
                definition.Id.Value);
            GrayboxBuildingInstance3D instance =
                session.Instances[beforeCount];
            Assert.That(instance.Placement.Definition, Is.SameAs(definition));
            yield return WaitForCompletion(instance, 3f);
            capture?.Invoke(instance);
        }

        private IEnumerator ResearchThroughUi(
            GrayboxBuildingSession3D session,
            string researchId)
        {
            ResearchDefinition definition =
                ResearchCatalog.Find(researchId);
            Assert.That(definition, Is.Not.Null, researchId);
            InputField search = RequireSceneObject("Research.Search")
                .GetComponent<InputField>();
            yield return ClickUiElement(search.gameObject);
            search.selectionAnchorPosition = 0;
            search.selectionFocusPosition = search.text.Length;
            for (var index = 0; index < researchId.Length; index++)
                InputSystem.QueueTextEvent(keyboard, researchId[index]);
            InputSystem.Update();
            yield return null;
            Assert.That(search.text, Is.EqualTo(researchId));
            yield return ClickUiElement(RequireSceneObject(
                "Research.Node." + researchId));
            yield return ClickUiElement(RequireSceneObject("Research.Start"));
            Assert.That(session.Research.Active, Is.SameAs(definition));
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!session.IsResearchCompleted(researchId) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(session.IsResearchCompleted(researchId), Is.True,
                researchId);
        }

        private IEnumerator SaveThroughRealMenu()
        {
            yield return EnsureSystemMenuOpen();
            Assert.That(configuredExit, Is.Not.Null,
                "The fake process exit must be installed before opening " +
                "the real system menu.");
            yield return ClickButton("Main.Quit");
            yield return ClickButton("Exit.SaveAndQuit");
            Assert.That(configuredExit.Count, Is.EqualTo(1));
            configuredExit = null;
            Assert.That(File.Exists(PrimaryPath), Is.True);
        }

        private IEnumerator ReloadAndContinue()
        {
            yield return ReloadScene();
            GrayboxFormalSaveEntryController3D entry = RequireEntry();
            Assert.That(entry.CanContinue, Is.True, entry.FeedbackMessage);
            yield return ClickButton("Start.Continue");
            Assert.That(entry.IsRuntimeReady, Is.True, entry.FeedbackMessage);
            Assert.That(entry.IsStartPageOpen, Is.False);
        }

        private IEnumerator EnsureSystemMenuOpen()
        {
            GrayboxSystemMenuController3D menu = RequireSystemMenu();
            for (var attempt = 0; attempt < 3 && !menu.IsOpen; attempt++)
                yield return TapKey(Key.Escape);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
        }

        private void ConfigureFakeExit()
        {
            GrayboxSystemMenuController3D menu = RequireSystemMenu();
            GrayboxFormalSaveRuntimeHost3D host = RequireSaveHost();
            configuredExit = new FakeExit();
            menu.ConfigureRuntimeServices(
                host.Speed,
                configuredExit,
                Object.FindObjectOfType<GrayboxSystemMenuView3D>());
        }

        private string PrimaryPath => Path.Combine(
            saveDirectory,
            FormalSaveStore.FileName);

        private IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private IEnumerator ReloadScene()
        {
            Scene previousScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(previousScene.IsValid() && previousScene.isLoaded,
                Is.True);
            int previousSceneHandle = previousScene.handle;
            GrayboxFormalSaveRuntimeHost3D previousHost = RequireSaveHost();
            int previousHostId = previousHost.GetInstanceID();
            AssertSingleFormalComposition();
            yield return UnloadScene();
            Assert.That(previousHost == null, Is.True,
                "The previous formal save host survived scene unload.");
            yield return LoadScene();
            Scene currentScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(currentScene.handle,
                Is.Not.EqualTo(previousSceneHandle));
            GrayboxFormalSaveRuntimeHost3D currentHost = RequireSaveHost();
            Assert.That(currentHost.GetInstanceID(),
                Is.Not.EqualTo(previousHostId));
            AssertSingleFormalComposition();
        }

        private IEnumerator UnloadScene()
        {
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            if (graybox.IsValid() && graybox.isLoaded)
            {
                Scene empty = SceneManager.CreateScene(
                    "GrayboxFormalSaveRoundTripEmpty" +
                    emptySceneOrdinal++);
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(graybox);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        private IEnumerator TapKey(Key key)
        {
            QueueKeyboard(key);
            yield return null;
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator ClickMouse(MouseButton button, Vector2 position)
        {
            QueueMouse(position, button);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator ClickButton(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.interactable, Is.True, name);
            yield return ClickUiElement(button.gameObject);
        }

        private IEnumerator SubmitButton(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.interactable, Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(button.gameObject));
            yield return TapKey(Key.Enter);
        }

        private IEnumerator ClickUiElement(GameObject target)
        {
            InputSystemUIInputModule module = Object.FindObjectOfType<
                InputSystemUIInputModule>();
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

        private IEnumerator ClickWorldObject(GameObject target)
        {
            Assert.That(target, Is.Not.Null);
            Vector3 screen = Camera.main.WorldToScreenPoint(
                target.transform.position);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Vector2 position = new Vector2(screen.x, screen.y);
            QueueMouse(position);
            yield return null;
            QueueMouse(position, MouseButton.Left);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator MoveToValidInnerPreview(
            GrayboxMobileCityController3D city,
            GrayboxBuildingPlacementController3D placement)
        {
            Transform platform = city.transform.Find("InnerCityPlatform");
            Assert.That(platform, Is.Not.Null);
            BoxCollider surface = platform.GetComponent<BoxCollider>();
            Assert.That(surface, Is.Not.Null);
            for (var y = 0; y < 6; y++)
            for (var x = 0; x < 8; x++)
            {
                Vector3 worldPoint = city.transform.TransformPoint(
                    new Vector3(
                        -1.28f + (x + .5f) * .32f,
                        0f,
                        -.96f + (y + .5f) * .32f));
                worldPoint.y = surface.bounds.max.y;
                QueueMouse(Camera.main.WorldToScreenPoint(worldPoint));
                yield return null;
                if (placement.CurrentHit.Site == BuildingSite.InnerCity &&
                    placement.CurrentEvaluation.IsValid)
                {
                    yield break;
                }
            }
            Assert.Fail("The formal inner city must expose a valid preview.");
        }

        private IEnumerator MoveToValidGroundPreview(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement)
        {
            Assert.That(world.TryWorldToCell(
                city.transform.position,
                out int cityX,
                out int cityY), Is.True);
            for (var radius = 2;
                 radius <= RequireSession().GroundBuildRadius;
                 radius++)
            for (var x = cityX - radius; x <= cityX + radius; x++)
            for (var y = cityY - radius; y <= cityY + radius; y++)
            {
                if (!world.Coordinates.TryCellToWorld(
                    x,
                    y,
                    0f,
                    out Vector3 corner))
                {
                    continue;
                }
                QueueMouse(Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f)));
                yield return null;
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.IsValid)
                {
                    yield break;
                }
            }
            Assert.Fail("The formal scene must expose a valid ground preview.");
        }

        private static IEnumerator WaitForCompletion(
            GrayboxBuildingInstance3D instance,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (instance.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed),
                instance.Placement.Definition.Id.Value);
        }

        private static IEnumerator WaitForCityMode(
            GrayboxMobileCityController3D city,
            CityMode expected,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (city.Mode != expected &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(city.Mode, Is.EqualTo(expected));
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

        private void QueueKeyboard(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private static FormalThreeDSaveData CaptureFullAuthority()
        {
            GrayboxFormalSaveRuntimeHost3D host = RequireSaveHost();
            FieldInfo field = typeof(GrayboxFormalSaveRuntimeHost3D)
                .GetField(
                    "coordinator",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var coordinator = field.GetValue(host) as
                GrayboxFormalSaveCoordinator3D;
            Assert.That(coordinator, Is.Not.Null);
            GrayboxFormalSaveCoordinatorResult3D result =
                coordinator.CaptureEnvelope(
                    "task13-round-trip-session",
                    "0.1.0-test",
                    new[] { "builtin:wastecity@0.1.0-test" },
                    new FormalSaveCheckpointMetadata
                    {
                        sequence = 1L,
                        reasonId =
                            FormalSaveCheckpointReasonIds.NewGameReady,
                        ruleTimeSeconds = 0f,
                        completedMilestoneIds = Array.Empty<string>(),
                    },
                    new DateTime(
                        2026, 8, 21, 0, 0, 0, DateTimeKind.Utc));
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Envelope?.formal3D, Is.Not.Null);
            return CloneAuthority(result.Envelope.formal3D);
        }

        private static void AssertFullAuthorityEquivalent(
            FormalThreeDSaveData expectedSource,
            FormalThreeDSaveData actualSource,
            string context)
        {
            FormalThreeDSaveData expected = CloneAuthority(expectedSource);
            FormalThreeDSaveData actual = CloneAuthority(actualSource);
            NormalizeAdvancingAuthority(expected, actual, context);
            Assert.That(
                JsonUtility.ToJson(actual, false),
                Is.EqualTo(JsonUtility.ToJson(expected, false)),
                context + " full schema 31 authority");
        }

        private static FormalThreeDSaveData CloneAuthority(
            FormalThreeDSaveData source)
        {
            Assert.That(source, Is.Not.Null);
            return JsonUtility.FromJson<FormalThreeDSaveData>(
                JsonUtility.ToJson(source, false));
        }

        private static void NormalizeAdvancingAuthority(
            FormalThreeDSaveData expected,
            FormalThreeDSaveData actual,
            string context)
        {
            NormalizeAdvancingFloat(
                expected.city.transitionRemainingSeconds,
                ref actual.city.transitionRemainingSeconds,
                context + " city transition");
            NormalizeAdvancingFloat(
                expected.crafting.activeProgressSeconds,
                ref actual.crafting.activeProgressSeconds,
                context + " crafting progress");
            NormalizeAdvancingFloat(
                expected.research.remainingSeconds,
                ref actual.research.remainingSeconds,
                context + " research remaining");

            Assert.That(actual.production.states.Length,
                Is.EqualTo(expected.production.states.Length), context);
            for (var index = 0;
                 index < expected.production.states.Length;
                 index++)
            {
                Assert.That(actual.production.states[index].stableInstanceId,
                    Is.EqualTo(expected.production.states[index]
                        .stableInstanceId), context);
                NormalizeAdvancingFloat(
                    expected.production.states[index].progressSeconds,
                    ref actual.production.states[index].progressSeconds,
                    context + " production " + index);
            }

            NormalizeAdvancingFloat(
                expected.defense.warningRemainingSeconds,
                ref actual.defense.warningRemainingSeconds,
                context + " defense warning");
            NormalizeAdvancingFloat(
                expected.defense.spawnClockSeconds,
                ref actual.defense.spawnClockSeconds,
                context + " defense spawn clock");
            NormalizeAdvancingFloat(
                expected.defense.fixedStepAccumulatorSeconds,
                ref actual.defense.fixedStepAccumulatorSeconds,
                context + " defense accumulator");
            Assert.That(actual.defense.towers.Length,
                Is.EqualTo(expected.defense.towers.Length), context);
            for (var index = 0;
                 index < expected.defense.towers.Length;
                 index++)
            {
                Assert.That(actual.defense.towers[index].stableInstanceId,
                    Is.EqualTo(expected.defense.towers[index]
                        .stableInstanceId), context);
                NormalizeAdvancingFloat(
                    expected.defense.towers[index]
                        .activeAmmunitionSeconds,
                    ref actual.defense.towers[index]
                        .activeAmmunitionSeconds,
                    context + " tower ammunition clock " + index);
                NormalizeAdvancingFloat(
                    expected.defense.towers[index].damageRemainder,
                    ref actual.defense.towers[index].damageRemainder,
                    context + " tower damage remainder " + index);
            }
            Assert.That(actual.defense.enemies.Length,
                Is.EqualTo(expected.defense.enemies.Length), context);
            for (var index = 0;
                 index < expected.defense.enemies.Length;
                 index++)
            {
                Assert.That(actual.defense.enemies[index].stableEnemyId,
                    Is.EqualTo(expected.defense.enemies[index]
                        .stableEnemyId), context);
                NormalizeAdvancingFloat(
                    expected.defense.enemies[index].positionX,
                    ref actual.defense.enemies[index].positionX,
                    context + " enemy x " + index);
                NormalizeAdvancingFloat(
                    expected.defense.enemies[index].positionZ,
                    ref actual.defense.enemies[index].positionZ,
                    context + " enemy z " + index);
                NormalizeAdvancingFloat(
                    expected.defense.enemies[index].movementRemainder,
                    ref actual.defense.enemies[index].movementRemainder,
                    context + " enemy movement " + index);
                NormalizeAdvancingFloat(
                    expected.defense.enemies[index]
                        .attackDamageRemainder,
                    ref actual.defense.enemies[index]
                        .attackDamageRemainder,
                    context + " enemy attack " + index);
            }
            NormalizeAdvancingFloat(
                expected.evacuation.remainingSeconds,
                ref actual.evacuation.remainingSeconds,
                context + " evacuation remaining");
        }

        private static void NormalizeAdvancingFloat(
            float expected,
            ref float actual,
            string context)
        {
            Assert.That(actual,
                Is.EqualTo(expected).Within(TransitionDriftTolerance),
                context);
            actual = expected;
        }

        private static void AssertTransitionRoundTrip(
            CityTransitionFingerprint expected,
            CityTransitionFingerprint actual,
            CityMode mode)
        {
            Assert.That(actual.Mode, Is.EqualTo(mode));
            Assert.That(actual.ReturnMode, Is.EqualTo(expected.ReturnMode));
            Assert.That(actual.Remaining, Is.GreaterThan(0f));
            Assert.That(actual.Remaining,
                Is.LessThanOrEqualTo(expected.Remaining + .001f));
            Assert.That(actual.Remaining,
                Is.GreaterThanOrEqualTo(
                    expected.Remaining - TransitionDriftTolerance));
            Assert.That(actual.WorldPosition.x,
                Is.EqualTo(expected.WorldPosition.x).Within(.001f));
            Assert.That(actual.WorldPosition.z,
                Is.EqualTo(expected.WorldPosition.z).Within(.001f));
            AssertTransitionColliderIsDerived(expected);
            AssertTransitionColliderIsDerived(actual);
        }

        private static void AssertTransitionColliderIsDerived(
            CityTransitionFingerprint fingerprint)
        {
            float fortressFactor = fingerprint.Mode == CityMode.Deploying
                ? 1f - fingerprint.Remaining /
                  CityDeploymentRules.FormalDeployDurationSeconds
                : fingerprint.Remaining /
                  CityDeploymentRules.FormalPackDurationSeconds;
            Vector3 expectedSize = Vector3.Lerp(
                new Vector3(3f, 1f, 2f),
                new Vector3(3f, 1.5f, 3f),
                fortressFactor);
            Assert.That(fingerprint.ColliderSize.x,
                Is.EqualTo(expectedSize.x).Within(.001f));
            Assert.That(fingerprint.ColliderSize.y,
                Is.EqualTo(expectedSize.y).Within(.001f));
            Assert.That(fingerprint.ColliderSize.z,
                Is.EqualTo(expectedSize.z).Within(.001f));
        }

        private static void AssertTransitionDerivedState(
            GrayboxMobileCityController3D city)
        {
            Assert.That(Object.FindObjectOfType<
                    GrayboxDirectControlCoordinator>().ControlTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(Object.FindObjectOfType<
                    GrayboxCameraController3D>().CurrentTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>().State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(city.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(RequireWorld().SurfaceFallbackVisible, Is.False);
        }

        private static void AssertDefenseAuthorityEquivalent(
            GrayboxDefensePersistenceState3D expected,
            GrayboxDefensePersistenceState3D actual)
        {
            Assert.That(actual.TutorialWaveTriggerCount,
                Is.EqualTo(expected.TutorialWaveTriggerCount));
            Assert.That(actual.FixedStepAccumulatorSeconds,
                Is.EqualTo(expected.FixedStepAccumulatorSeconds)
                    .Within(.101f));
            Assert.That(actual.Tutorial.TutorialTriggered,
                Is.EqualTo(expected.Tutorial.TutorialTriggered));
            Assert.That(actual.Tutorial.WavePhase,
                Is.EqualTo(expected.Tutorial.WavePhase));
            Assert.That(actual.Tutorial.SpawnedEnemyCount,
                Is.EqualTo(expected.Tutorial.SpawnedEnemyCount));
            Assert.That(actual.Tutorial.DefeatedEnemyCount,
                Is.EqualTo(expected.Tutorial.DefeatedEnemyCount));
            Assert.That(actual.Tutorial.NextEnemyOrdinal,
                Is.EqualTo(expected.Tutorial.NextEnemyOrdinal));
            Assert.That(actual.Tutorial.SpawnOriginX,
                Is.EqualTo(expected.Tutorial.SpawnOriginX).Within(.0001f));
            Assert.That(actual.Tutorial.SpawnOriginZ,
                Is.EqualTo(expected.Tutorial.SpawnOriginZ).Within(.0001f));
            Assert.That(actual.Tutorial.CoreCurrentHealth,
                Is.EqualTo(expected.Tutorial.CoreCurrentHealth));
            Assert.That(actual.Tutorial.Enemies,
                Has.Count.EqualTo(expected.Tutorial.Enemies.Count));
            for (var index = 0;
                 index < expected.Tutorial.Enemies.Count;
                 index++)
            {
                DefenseEnemyPersistenceState left =
                    expected.Tutorial.Enemies[index];
                DefenseEnemyPersistenceState right =
                    actual.Tutorial.Enemies[index];
                Assert.That(right.StableId, Is.EqualTo(left.StableId));
                Assert.That(right.ArchetypeId, Is.EqualTo(left.ArchetypeId));
                Assert.That(right.SpawnOrder, Is.EqualTo(left.SpawnOrder));
                Assert.That(right.X, Is.EqualTo(left.X).Within(.21f));
                Assert.That(right.Z, Is.EqualTo(left.Z).Within(.21f));
                Assert.That(right.CurrentHealth,
                    Is.EqualTo(left.CurrentHealth));
                Assert.That(right.MovementRemainder,
                    Is.EqualTo(left.MovementRemainder).Within(.0001f));
                Assert.That(right.AttackDamageRemainder,
                    Is.EqualTo(left.AttackDamageRemainder).Within(.11f));
            }
            Assert.That(actual.Towers,
                Has.Count.EqualTo(expected.Towers.Count));
            for (var index = 0; index < expected.Towers.Count; index++)
            {
                MachineGunTurretPersistenceState left =
                    expected.Towers[index];
                MachineGunTurretPersistenceState right =
                    actual.Towers[index];
                Assert.That(right.StableId, Is.EqualTo(left.StableId));
                Assert.That(right.AmmunitionAmount,
                    Is.EqualTo(left.AmmunitionAmount));
                Assert.That(right.IsPlayerPaused,
                    Is.EqualTo(left.IsPlayerPaused));
                Assert.That(right.ActiveAmmunitionSeconds,
                    Is.EqualTo(left.ActiveAmmunitionSeconds).Within(.0001f));
                Assert.That(right.DamageRemainder,
                    Is.EqualTo(left.DamageRemainder).Within(.0001f));
            }
        }

        private static void AssertEvacuationAuthorityEquivalent(
            GrayboxEvacuationPersistenceState3D expected,
            GrayboxEvacuationPersistenceState3D actual)
        {
            Assert.That(actual.NextBatchOrdinal,
                Is.EqualTo(expected.NextBatchOrdinal));
            Assert.That(actual.ActiveBatchId,
                Is.EqualTo(expected.ActiveBatchId));
            Assert.That(actual.IsProcessing,
                Is.EqualTo(expected.IsProcessing));
            Assert.That(actual.BatchContext,
                Is.EqualTo(expected.BatchContext));
            CollectionAssert.AreEqual(expected.Work, actual.Work);
            CollectionAssert.AreEqual(
                expected.FullQueueStableInstanceIds,
                actual.FullQueueStableInstanceIds);
            Assert.That(actual.CurrentQueueIndex,
                Is.EqualTo(expected.CurrentQueueIndex));
            Assert.That(actual.CurrentStableInstanceId,
                Is.EqualTo(expected.CurrentStableInstanceId));
            Assert.That(actual.RemainingSeconds,
                Is.LessThanOrEqualTo(expected.RemainingSeconds + .001f));
            Assert.That(actual.RemainingSeconds,
                Is.GreaterThanOrEqualTo(
                    expected.RemainingSeconds - TransitionDriftTolerance));
            Assert.That(actual.IsBlocked, Is.EqualTo(expected.IsBlocked));
            Assert.That(actual.BlockedCode,
                Is.EqualTo(expected.BlockedCode));
            Assert.That(actual.BlockedStableInstanceId,
                Is.EqualTo(expected.BlockedStableInstanceId));
            CollectionAssert.AreEqual(
                expected.LockedStableInstanceIds,
                actual.LockedStableInstanceIds);
            CollectionAssert.AreEqual(
                expected.PendingRollbackStableInstanceIds,
                actual.PendingRollbackStableInstanceIds);
            Assert.That(actual.Payloads.Count,
                Is.EqualTo(expected.Payloads.Count));
            for (var index = 0; index < expected.Payloads.Count; index++)
            {
                GrayboxEvacuationPayloadPersistenceState3D left =
                    expected.Payloads[index];
                GrayboxEvacuationPayloadPersistenceState3D right =
                    actual.Payloads[index];
                Assert.That(right.StableInstanceId,
                    Is.EqualTo(left.StableInstanceId));
                Assert.That(right.HasDefensePayload,
                    Is.EqualTo(left.HasDefensePayload));
                Assert.That(right.TowerAmmunitionAmount,
                    Is.EqualTo(left.TowerAmmunitionAmount));
                CollectionAssert.AreEqual(
                    left.ProductionInput,
                    right.ProductionInput);
                CollectionAssert.AreEqual(
                    left.ProductionReservedInput,
                    right.ProductionReservedInput);
                CollectionAssert.AreEqual(
                    left.ProductionOutput,
                    right.ProductionOutput);
                CollectionAssert.AreEqual(left.Resources, right.Resources);
            }
        }

        private static void AssertEvacuationQueueUi(
            GrayboxEvacuationPersistenceState3D state,
            bool blocked)
        {
            Text batch = RequireText("Evacuation.Queue.Batch");
            Assert.That(batch.gameObject.activeInHierarchy, Is.True);
            Assert.That(batch.text, Does.Contain(state.ActiveBatchId));
            Assert.That(
                RequireText("Evacuation.Queue.Progress")
                    .gameObject.activeInHierarchy,
                Is.True);
            if (blocked)
            {
                Assert.That(
                    RequireText("Evacuation.Queue.Blocked")
                        .gameObject.activeInHierarchy,
                    Is.True);
                Assert.That(
                    RequireText("Evacuation.Queue.CapacityHint").text,
                    Is.Not.Empty);
            }
        }

        private static GrayboxFormalSaveEntryController3D RequireEntry()
        {
            GrayboxFormalSaveEntryController3D value =
                Object.FindObjectOfType<
                    GrayboxFormalSaveEntryController3D>(true);
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxFormalSaveRuntimeHost3D RequireSaveHost()
        {
            GrayboxFormalSaveRuntimeHost3D value = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static void SetDevelopmentRuleTime(float targetMultiplier)
        {
            GrayboxFormalSaveRuntimeHost3D host = RequireSaveHost();
            float formalSpeed = targetMultiplier > 1f ? 2f : 1f;
            host.Speed.Set(formalSpeed);
            host.RuleClock.SetDevelopmentAcceleration(
                targetMultiplier > 1f
                    ? targetMultiplier / formalSpeed
                    : 1f);
            Time.timeScale = host.RuleClock.EffectiveSpeed;
        }

        private static GrayboxSystemMenuController3D RequireSystemMenu()
        {
            GrayboxSystemMenuController3D value = Object.FindObjectOfType<
                GrayboxSystemMenuController3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxBuildingSession3D RequireSession()
        {
            GrayboxBuildingSession3D value = Object.FindObjectOfType<
                GrayboxBuildingSession3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxMobileCityController3D RequireCity()
        {
            GrayboxMobileCityController3D value = Object.FindObjectOfType<
                GrayboxMobileCityController3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxWorldView3D RequireWorld()
        {
            GrayboxWorldView3D value = Object.FindObjectOfType<
                GrayboxWorldView3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxBuildingPlacementController3D RequirePlacement()
        {
            GrayboxBuildingPlacementController3D value =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxBuildingInteractionModel3D RequireInteraction()
        {
            GrayboxBuildingInteractionModel3D value = Object.FindObjectOfType<
                GrayboxBuildingInteractionModel3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxDefenseController3D RequireDefense()
        {
            GrayboxDefenseController3D value = Object.FindObjectOfType<
                GrayboxDefenseController3D>();
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Runtime, Is.Not.Null);
            Assert.That(value.Snapshot, Is.Not.Null);
            return value;
        }

        private static GrayboxEvacuationController3D RequireEvacuation()
        {
            GrayboxEvacuationController3D value = Object.FindObjectOfType<
                GrayboxEvacuationController3D>();
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static GrayboxDeveloperModifier3D RequireModifier()
        {
            return new GrayboxDeveloperModifier3D(
                RequireSession(),
                RequireCity(),
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>());
        }

        private static Button FindButton(string name)
        {
            return Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static Text RequireText(string name)
        {
            Text value = Object.FindObjectsOfType<Text>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        private static GameObject RequireSceneObject(
            string name,
            bool includeInactive = false)
        {
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            GameObject value = Object.FindObjectsOfType<Transform>(
                    includeInactive)
                .Where(transform => transform.gameObject.scene == graybox)
                .Select(transform => transform.gameObject)
                .FirstOrDefault(gameObject => gameObject.name == name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        private static int CountFormalSceneObjects()
        {
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            Assert.That(graybox.IsValid() && graybox.isLoaded, Is.True);
            return Object.FindObjectsOfType<Transform>(true).Count(value =>
                value.gameObject.scene == graybox);
        }

        private static void AssertSingleFormalComposition()
        {
            AssertSingleLoadedObject<GrayboxSceneBootstrap>();
            AssertSingleLoadedObject<GrayboxFormalSaveRuntimeHost3D>();
            AssertSingleLoadedObject<GrayboxBuildingSession3D>();
            AssertSingleLoadedObject<GrayboxProductionController3D>();
            AssertSingleLoadedObject<GrayboxDefenseController3D>();
            AssertSingleLoadedObject<GrayboxEvacuationController3D>();
            AssertSingleLoadedObject<GrayboxUsabilityInputCoordinator3D>();
        }

        private static void AssertSingleLoadedObject<T>()
            where T : Component
        {
            T[] values = Resources.FindObjectsOfTypeAll<T>()
                .Where(value => value != null &&
                    value.gameObject.scene.IsValid() &&
                    value.gameObject.scene.isLoaded)
                .ToArray();
            Assert.That(values, Has.Length.EqualTo(1), typeof(T).Name);
        }

        private static MethodInfo RequireConfigureStoreRootForTesting()
        {
            MethodInfo method = typeof(GrayboxFormalSaveRuntimeHost3D)
                .GetMethod(
                    "ConfigureStoreRootForTesting",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static Dictionary<string, FormalSaveFileSnapshot>
            CaptureFormalSaveFileSnapshots(string directory)
        {
            var snapshot = new Dictionary<string, FormalSaveFileSnapshot>(
                StringComparer.Ordinal);
            if (!Directory.Exists(directory)) return snapshot;
            foreach (string path in Directory.GetFiles(
                         directory,
                         "formal-world*",
                         SearchOption.TopDirectoryOnly))
            {
                snapshot[Path.GetFileName(path)] =
                    new FormalSaveFileSnapshot(
                        File.ReadAllBytes(path),
                        File.GetCreationTimeUtc(path),
                        File.GetLastWriteTimeUtc(path),
                        File.GetAttributes(path));
            }
            return snapshot;
        }

        private static void AssertFormalSaveFilesUnchanged(
            IReadOnlyDictionary<string, FormalSaveFileSnapshot> expected,
            IReadOnlyDictionary<string, FormalSaveFileSnapshot> actual)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (KeyValuePair<string, FormalSaveFileSnapshot> pair in
                     expected)
            {
                Assert.That(actual.TryGetValue(
                    pair.Key,
                    out FormalSaveFileSnapshot observed), Is.True);
                CollectionAssert.AreEqual(pair.Value.Bytes, observed.Bytes);
                Assert.That(observed.CreationTimeUtc,
                    Is.EqualTo(pair.Value.CreationTimeUtc));
                Assert.That(observed.LastWriteTimeUtc,
                    Is.EqualTo(pair.Value.LastWriteTimeUtc));
                Assert.That(observed.Attributes,
                    Is.EqualTo(pair.Value.Attributes));
            }
        }

        private sealed class CityTransitionFingerprint
        {
            private CityTransitionFingerprint(
                CityMode mode,
                CityMode returnMode,
                float remaining,
                Vector3 worldPosition,
                Vector3 colliderSize)
            {
                Mode = mode;
                ReturnMode = returnMode;
                Remaining = remaining;
                WorldPosition = worldPosition;
                ColliderSize = colliderSize;
            }

            public CityMode Mode { get; }
            public CityMode ReturnMode { get; }
            public float Remaining { get; }
            public Vector3 WorldPosition { get; }
            public Vector3 ColliderSize { get; }

            public static CityTransitionFingerprint Capture(
                GrayboxMobileCityController3D city)
            {
                BoxCollider collider = city.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null);
                return new CityTransitionFingerprint(
                    city.Mode,
                    city.Deployment.TransitionReturnMode,
                    city.Deployment.Remaining,
                    city.WorldPosition,
                    collider.size);
            }
        }

        private sealed class FormalSaveFileSnapshot
        {
            public FormalSaveFileSnapshot(
                byte[] bytes,
                DateTime creationTimeUtc,
                DateTime lastWriteTimeUtc,
                FileAttributes attributes)
            {
                Bytes = bytes;
                CreationTimeUtc = creationTimeUtc;
                LastWriteTimeUtc = lastWriteTimeUtc;
                Attributes = attributes;
            }

            public byte[] Bytes { get; }
            public DateTime CreationTimeUtc { get; }
            public DateTime LastWriteTimeUtc { get; }
            public FileAttributes Attributes { get; }
        }

        private sealed class FakeExit : IGrayboxApplicationExit
        {
            public int Count { get; private set; }
            public void Exit() => Count++;
        }

    }
}
