using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseRuntimeInputTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private const string DetailsPanelName = "DefenseDetailsPanel";
        private const string WarningName = "DefenseWaveWarning";
        private const string TowerPauseButtonName =
            "DefenseDetails.TowerPauseButton";

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
                        "GrayboxDefenseRuntimeInputEmpty");
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
        public IEnumerator FormalSceneOwnsOneDefenseLoopAndShowsTutorialWarning()
        {
            GrayboxDefenseController3D[] controllers =
                Object.FindObjectsOfType<GrayboxDefenseController3D>(true);
            GrayboxDefenseWorldView3D[] worldViews =
                Object.FindObjectsOfType<GrayboxDefenseWorldView3D>(true);
            GrayboxDefenseHud3D[] huds =
                Object.FindObjectsOfType<GrayboxDefenseHud3D>(true);
            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(worldViews, Has.Length.EqualTo(1));
            Assert.That(huds, Has.Length.EqualTo(1));
            Assert.That(controllers[0].WorldView, Is.SameAs(worldViews[0]));
            Assert.That(controllers[0].Hud, Is.SameAs(huds[0]));
            Assert.That(RequireSceneObject(WarningName, true), Is.Not.Null);
            Assert.That(RequireSceneObject(DetailsPanelName, true), Is.Not.Null);
            Assert.That(Object.FindObjectsOfType<InputSystemUIInputModule>(true),
                Has.Length.EqualTo(1));

            GrayboxBuildingInstance3D turret = CreateDefenseChain();
            yield return WaitForTower(controllers[0], turret.StableInstanceId);

            Assert.That(controllers[0].Snapshot.TutorialWaveTriggerCount,
                Is.EqualTo(1));
            Assert.That(controllers[0].Snapshot.WarningRemainingSeconds,
                Is.GreaterThan(0f).And.LessThanOrEqualTo(15f));
            Assert.That(huds[0].WarningVisible, Is.True);
            Assert.That(RequireSceneObject(WarningName).activeInHierarchy,
                Is.True);
        }

        [UnityTest]
        public IEnumerator RealWorldClicksAndModalKeysCoordinateDefenseDetails()
        {
            GrayboxDefenseController3D controller =
                Object.FindObjectOfType<GrayboxDefenseController3D>();
            GrayboxDefenseWorldView3D worldView =
                Object.FindObjectOfType<GrayboxDefenseWorldView3D>();
            GrayboxDefenseHud3D hud =
                Object.FindObjectOfType<GrayboxDefenseHud3D>();
            GrayboxSystemMenuController3D systemMenu =
                Object.FindObjectOfType<GrayboxSystemMenuController3D>();
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxOperationsView3D operationsView =
                Object.FindObjectOfType<GrayboxOperationsView3D>();
            GrayboxProductionController3D production =
                Object.FindObjectOfType<GrayboxProductionController3D>();
            GrayboxBuildingInstance3D turret = CreateDefenseChain();
            yield return WaitForTower(controller, turret.StableInstanceId);
            Assert.That(production.Tick(.1f, paused: false), Is.True);
            Assert.That(worldView.TryGetTowerObject(
                turret.StableInstanceId,
                out GameObject towerObject), Is.True);

            yield return ClickWorldObject(towerObject);
            Assert.That(controller.HasSelection, Is.True);
            Assert.That(controller.SelectedKind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.Tower));
            Assert.That(RequireSceneObject(DetailsPanelName).activeInHierarchy,
                Is.True);
            Assert.That(hud.SelectionText.text, Does.Contain("射程 10 格"));

            GrayboxBuildingInstance3D smelter = session.Instances.Single(
                value => value.Placement.Definition == BuildingCatalog.Smelter);
            GrayboxBuildingInstance3D warehouse = session.Instances.Single(
                value => value.Placement.Definition == BuildingCatalog.Warehouse);
            yield return ClickBuildingObject(
                RequireSceneObject(smelter.StableInstanceId),
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>(),
                worldView,
                smelter.StableInstanceId);
            Assert.That(operationsView.IsLedgerOpen, Is.True);
            Assert.That(controller.HasSelection, Is.False);
            Assert.That(hud.DetailsVisible, Is.False);

            yield return TapKey(Key.Escape);
            Assert.That(operationsView.IsLedgerOpen, Is.False);
            yield return ClickWorldObject(towerObject);
            Assert.That(controller.HasSelection, Is.True);
            Assert.That(operationsView.IsLedgerOpen, Is.False);
            yield return ClickBuildingObject(
                RequireSceneObject(warehouse.StableInstanceId),
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>(),
                worldView,
                warehouse.StableInstanceId);
            Assert.That(operationsView.IsLedgerOpen, Is.True);
            Assert.That(controller.HasSelection, Is.False);
            Assert.That(hud.DetailsVisible, Is.False);

            yield return TapKey(Key.Escape);
            yield return ClickWorldObject(towerObject);
            Assert.That(controller.HasSelection, Is.True);
            Assert.That(operationsView.IsLedgerOpen, Is.False);

            yield return TapKey(Key.Escape);
            Assert.That(controller.HasSelection, Is.False);
            Assert.That(systemMenu.IsOpen, Is.False);
            yield return TapKey(Key.Escape);
            Assert.That(systemMenu.IsOpen, Is.True);
            yield return TapKey(Key.Escape);
            Assert.That(systemMenu.IsOpen, Is.False);

            yield return ClickWorldObject(towerObject);
            yield return TapKey(Key.E);
            Assert.That(controller.HasSelection, Is.False);
            Assert.That(RequireSceneObject("InventoryCraftingPanel")
                .activeInHierarchy, Is.True);
            yield return TapKey(Key.E);

            yield return ClickWorldObject(towerObject);
            yield return TapKey(Key.T);
            Assert.That(controller.HasSelection, Is.False);
            Assert.That(RequireSceneObject("ResearchTreePanel")
                .activeInHierarchy, Is.True);
            yield return TapKey(Key.T);

            yield return ClickWorldObject(towerObject);
            yield return TapKey(Key.B);
            Assert.That(controller.HasSelection, Is.False);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            yield return TapKey(Key.Escape);

            controller.Tick(20.1f, paused: false);
            yield return null;
            Assert.That(controller.Snapshot.Enemies, Is.Not.Empty);
            string enemyId = controller.Snapshot.Enemies[0].StableId;
            Assert.That(worldView.TryGetEnemyObject(
                enemyId,
                out GameObject enemyObject), Is.True);
            yield return ClickWorldObject(enemyObject);
            Assert.That(controller.SelectedKind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.Enemy));
            Assert.That(controller.SelectedStableId, Is.EqualTo(enemyId));
            Assert.That(hud.SelectionText.text, Does.Contain("目标 城市核心"));
            Assert.That(hud.SelectionText.text, Does.Contain("距离 "));
            Assert.That(hud.SelectionText.text, Does.Contain("格"));
        }

        [UnityTest]
        public IEnumerator PausedTowerLetsCoreFallAndFormalHudShowsLoss()
        {
            GrayboxDefenseController3D controller =
                Object.FindObjectOfType<GrayboxDefenseController3D>();
            GrayboxDefenseWorldView3D worldView =
                Object.FindObjectOfType<GrayboxDefenseWorldView3D>();
            GrayboxDefenseHud3D hud =
                Object.FindObjectOfType<GrayboxDefenseHud3D>();
            GrayboxBuildingInstance3D turret = CreateDefenseChain();
            yield return WaitForTower(controller, turret.StableInstanceId);
            Assert.That(worldView.TryGetTowerObject(
                turret.StableInstanceId,
                out GameObject towerObject), Is.True);

            yield return ClickWorldObject(towerObject);
            yield return ClickUiElement(
                RequireSceneObject(TowerPauseButtonName));
            Assert.That(controller.Snapshot.Towers.Single().PlayerPaused,
                Is.True);

            Assert.That(controller.Tick(120f, paused: false), Is.True);
            yield return null;
            Assert.That(controller.Snapshot.CoreCurrentHealth, Is.Zero);
            Assert.That(controller.Snapshot.IsCoreDestroyed, Is.True);
            Assert.That(hud.SummaryText.text, Does.Contain("核心 0/2000"));
            Assert.That(hud.SummaryText.text,
                Does.Contain("城市核心失守"));
        }

        [UnityTest]
        public IEnumerator RealUiPauseDoesNotLeakAndSpaceFreezesSimulationButNotSelection()
        {
            GrayboxDefenseController3D controller =
                Object.FindObjectOfType<GrayboxDefenseController3D>();
            GrayboxDefenseWorldView3D worldView =
                Object.FindObjectOfType<GrayboxDefenseWorldView3D>();
            GrayboxSystemMenuController3D systemMenu =
                Object.FindObjectOfType<GrayboxSystemMenuController3D>();
            GrayboxBuildingInstance3D turret = CreateDefenseChain();
            yield return WaitForTower(controller, turret.StableInstanceId);
            Assert.That(worldView.TryGetTowerObject(
                turret.StableInstanceId,
                out GameObject towerObject), Is.True);
            yield return ClickWorldObject(towerObject);

            GameObject pauseButton = RequireSceneObject(TowerPauseButtonName);
            string selectedBeforeUi = controller.SelectedStableId;
            yield return ClickUiElement(pauseButton);
            Assert.That(controller.SelectedStableId,
                Is.EqualTo(selectedBeforeUi),
                "A UI click must not leak into world selection.");
            Assert.That(controller.Snapshot.Towers.Single().PlayerPaused,
                Is.True);

            controller.Tick(20.1f, paused: false);
            yield return null;
            Assert.That(controller.Snapshot.Enemies, Is.Not.Empty);
            string enemyId = controller.Snapshot.Enemies[0].StableId;
            Assert.That(worldView.TryGetEnemyObject(
                enemyId,
                out GameObject enemyObject), Is.True);
            yield return TapKey(Key.Escape);
            if (controller.HasSelection)
                yield return TapKey(Key.Escape);
            Assert.That(controller.HasSelection, Is.False);
            if (!systemMenu.IsOpen)
                yield return TapKey(Key.Escape);
            Assert.That(systemMenu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            DefenseSimulationSample systemPaused = CaptureSimulation(
                controller,
                enemyId);
            yield return null;
            yield return null;
            AssertSimulationUnchanged(
                systemPaused,
                CaptureSimulation(controller, enemyId));

            yield return TapKey(Key.Escape);
            Assert.That(systemMenu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            yield return WaitForSimulationChange(
                controller,
                enemyId,
                systemPaused);

            yield return ClickWorldObject(enemyObject);
            Assert.That(controller.SelectedKind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.Enemy));
            Assert.That(controller.SelectedStableId, Is.EqualTo(enemyId));

            yield return TapKey(Key.Space);
            Assert.That(Time.timeScale, Is.Zero);
            DefenseSimulationSample tacticalPaused = CaptureSimulation(
                controller,
                enemyId);
            yield return null;
            yield return null;
            AssertSimulationUnchanged(
                tacticalPaused,
                CaptureSimulation(controller, enemyId));
            Assert.That(controller.SelectedStableId, Is.EqualTo(enemyId));
            yield return TapKey(Key.Space);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            yield return WaitForSimulationChange(
                controller,
                enemyId,
                tacticalPaused);
            Assert.That(controller.SelectedStableId, Is.EqualTo(enemyId));
        }

        private GrayboxBuildingInstance3D CreateDefenseChain()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            Assert.That(session, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            modifier.UnlockAllResearch();
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Stone, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Ammunition, 40),
                Is.True);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(world.Coordinates.TryWorldToCell(
                city.transform.position,
                out int cityX,
                out int cityY), Is.True);

            BeginBuilding(
                session,
                presentation,
                BuildingCatalog.Smelter,
                cityX - 3,
                cityY,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            BeginBuilding(
                session,
                presentation,
                BuildingCatalog.Assembler,
                cityX + 4,
                cityY + 2,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            BeginBuilding(
                session,
                presentation,
                BuildingCatalog.Warehouse,
                cityX - 5,
                cityY,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            GrayboxBuildingInstance3D turret = BeginBuilding(
                session,
                presentation,
                BuildingCatalog.MachineGunTurret,
                cityX + 2,
                cityY,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            return turret;
        }

        private static GrayboxBuildingInstance3D BeginBuilding(
            GrayboxBuildingSession3D session,
            GrayboxBuildingWorldView3D presentation,
            BuildingDefinition definition,
            int x,
            int y,
            int cityX,
            int cityY)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                cityX,
                cityY,
                session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                compatibleResourceNode: ResourceNodeBinding.None,
                contentVisible: true,
                unlock: unlock,
                canAfford: true);
            Assert.That(session.TryBeginConstruction(
                request,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static IEnumerator WaitForTower(
            GrayboxDefenseController3D controller,
            string stableId)
        {
            float deadline = Time.realtimeSinceStartup + 1f;
            while ((controller.Snapshot == null ||
                    controller.Snapshot.Towers.All(value =>
                        value.StableId != stableId)) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(controller.Snapshot, Is.Not.Null);
            Assert.That(controller.Snapshot.Towers.Any(value =>
                value.StableId == stableId), Is.True);
        }

        private static IEnumerator WaitForSimulationChange(
            GrayboxDefenseController3D controller,
            string enemyId,
            DefenseSimulationSample before)
        {
            float deadline = Time.realtimeSinceStartup + 1f;
            DefenseSimulationSample current = CaptureSimulation(
                controller,
                enemyId);
            while (SamplesEqual(before, current) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                current = CaptureSimulation(controller, enemyId);
            }
            Assert.That(
                SamplesEqual(before, current),
                Is.False,
                "Resuming through real input must advance the same defense " +
                "simulation that was frozen.");
        }

        private static DefenseSimulationSample CaptureSimulation(
            GrayboxDefenseController3D controller,
            string enemyId)
        {
            GrayboxDefenseEnemySnapshot3D enemy = controller.Snapshot.Enemies
                .Single(value => value.StableId == enemyId);
            return new DefenseSimulationSample(
                controller.Snapshot.CoreCurrentHealth,
                enemy.X,
                enemy.Z);
        }

        private static void AssertSimulationUnchanged(
            DefenseSimulationSample expected,
            DefenseSimulationSample actual)
        {
            Assert.That(actual.CoreHealth, Is.EqualTo(expected.CoreHealth));
            Assert.That(actual.EnemyX, Is.EqualTo(expected.EnemyX));
            Assert.That(actual.EnemyZ, Is.EqualTo(expected.EnemyZ));
        }

        private static bool SamplesEqual(
            DefenseSimulationSample left,
            DefenseSimulationSample right)
        {
            return left.CoreHealth == right.CoreHealth &&
                   left.EnemyX == right.EnemyX &&
                   left.EnemyZ == right.EnemyZ;
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(key));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
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

        private IEnumerator ClickBuildingObject(
            GameObject target,
            GrayboxBuildingWorldView3D presentation,
            GrayboxDefenseWorldView3D defenseWorldView,
            string stableId)
        {
            Assert.That(target, Is.Not.Null);
            Collider targetCollider = target.GetComponent<Collider>();
            Assert.That(targetCollider, Is.Not.Null, target.name);
            Bounds bounds = targetCollider.bounds;
            var candidates = new[]
            {
                bounds.center,
                bounds.center + Vector3.left * bounds.extents.x * .7f,
                bounds.center + Vector3.right * bounds.extents.x * .7f,
                bounds.center + Vector3.forward * bounds.extents.z * .7f,
                bounds.center + Vector3.back * bounds.extents.z * .7f,
            };
            var uiHits = new List<RaycastResult>();
            Vector2 position = default;
            bool found = false;
            for (int index = 0; index < candidates.Length; index++)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(
                    candidates[index]);
                if (screen.z <= 0f)
                    continue;
                Vector2 candidate = new Vector2(screen.x, screen.y);
                Ray ray = Camera.main.ScreenPointToRay(candidate);
                if (!presentation.TryPickInstance(
                        ray,
                        out string pickedId) ||
                    pickedId != stableId)
                {
                    continue;
                }
                if (defenseWorldView.TryPick(ray, out _, out _))
                    continue;
                uiHits.Clear();
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = candidate,
                };
                EventSystem.current.RaycastAll(pointer, uiHits);
                if (uiHits.Count > 0)
                    continue;
                position = candidate;
                found = true;
                break;
            }
            Assert.That(found, Is.True, stableId);
            QueueMouse(position);
            yield return null;
            QueueMouse(position, MouseButton.Left);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator ClickUiElement(GameObject target)
        {
            Assert.That(target.activeInHierarchy, Is.True, target.name);
            RectTransform rect = target.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, target.name);
            Canvas.ForceUpdateCanvases();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
            QueueMouse(screen);
            yield return null;
            QueueMouse(screen, MouseButton.Left);
            yield return null;
            QueueMouse(screen);
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

        private readonly struct DefenseSimulationSample
        {
            public DefenseSimulationSample(
                int coreHealth,
                float enemyX,
                float enemyZ)
            {
                CoreHealth = coreHealth;
                EnemyX = enemyX;
                EnemyZ = enemyZ;
            }

            public int CoreHealth { get; }
            public float EnemyX { get; }
            public float EnemyZ { get; }
        }
    }
}
