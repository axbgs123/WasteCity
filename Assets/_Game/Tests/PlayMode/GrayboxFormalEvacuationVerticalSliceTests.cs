using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;
using WasteCity.World;

namespace WasteCity.Tests
{
    /// <summary>
    /// IDEA-0014 formal six-stage vertical slice. The fixture may only set
    /// deterministic starting resources/population and accelerate rule time;
    /// every gameplay transition below must still use real input and Update.
    /// </summary>
    public sealed class GrayboxFormalEvacuationVerticalSliceTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private const float RuleTimeScale = 10f;

        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator LoadFormalSceneAndApplyAuthorizedFixture()
        {
            previousTimeScale = Time.timeScale;
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;

            Time.timeScale = RuleTimeScale;
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
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(mouse);
            Time.timeScale = RuleTimeScale;

            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            Assert.That(session, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            var fixture = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);

            // IDEA-0014 Task 8 authorization: exact raw starting resources
            // before the first gameplay input. Population remains the formal
            // natural-opening value and is not adjusted by the fixture.
            for (var index = 0; index < ResourceIds.All.Length; index++)
                Assert.That(
                    fixture.SetResource(ResourceIds.All[index], 0),
                    Is.True,
                    ResourceIds.All[index]);
            Assert.That(fixture.SetResource(ResourceIds.Iron, 16), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Alloy, 48), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Biomass, 10), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Stone, 12), Is.True);
            Assert.That(fixture.SetResource(ResourceIds.Ammunition, 0), Is.True);

            Assert.That(session.Population, Is.EqualTo(100));
            Assert.That(session.PopulationCapacity, Is.EqualTo(150));
            Assert.That(session.Inventory.Get(ResourceIds.Iron), Is.EqualTo(16));
            Assert.That(session.Inventory.Get(ResourceIds.Alloy), Is.EqualTo(48));
            Assert.That(session.Inventory.Get(ResourceIds.Biomass), Is.EqualTo(10));
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.EqualTo(12));
            Assert.That(session.Inventory.Get(ResourceIds.Ammunition), Is.Zero);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadFormalSceneAndRestoreFixture()
        {
            Time.timeScale = 1f;
            try
            {
                Scene graybox = SceneManager.GetSceneByName(SceneName);
                if (graybox.IsValid() && graybox.isLoaded)
                {
                    Scene empty = SceneManager.CreateScene(
                        "GrayboxFormalEvacuationVerticalSliceEmpty");
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
                try
                {
                    if (keyboard != null && keyboard.added)
                        InputSystem.RemoveDevice(keyboard);
                }
                finally
                {
                    try
                    {
                        if (mouse != null && mouse.added)
                            InputSystem.RemoveDevice(mouse);
                    }
                    finally
                    {
                        InputSystem.settings.updateMode = previousUpdateMode;
                        InputSystem.settings.backgroundBehavior =
                            previousBackgroundBehavior;
                        InputSystem.settings.editorInputBehaviorInPlayMode =
                            previousEditorInputBehavior;
                        Time.timeScale = previousTimeScale;
                    }
                }
                    GrayboxFormalPlayModeEntryFixture
                        .AssertRealSaveFilesUnchanged();
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0014_FormalSixStageRealInputVerticalSlice()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            Assert.That(session, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(interaction, Is.Not.Null);
            Assert.That(placement, Is.Not.Null);
            Rigidbody body = city.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);

            // Stage 1: real right-click navigation followed by real F deploy.
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(world.TryWorldToCell(
                city.transform.position,
                out int startX,
                out int startY), Is.True);
            Assert.That(
                CityDeploymentRules.Validate(world.Model, startX, startY),
                Is.EqualTo(CityDeploymentFailure.None));
            FindReachableRoundTripCell(
                world.Model,
                startX,
                startY,
                out int targetX,
                out int targetY);
            Assert.That(world.Coordinates.TryCellToWorld(
                targetX,
                targetY,
                0f,
                out Vector3 targetCorner), Is.True);
            yield return ClickMouse(
                MouseButton.Right,
                Camera.main.WorldToScreenPoint(
                    targetCorner + new Vector3(.5f, 0f, .5f)));
            Assert.That(city.AutopilotActive, Is.True);
            Assert.That(city.Destination.HasValue, Is.True);
            Assert.That(city.Destination.Value.X, Is.EqualTo(targetX));
            Assert.That(city.Destination.Value.Y, Is.EqualTo(targetY));

            float deadline = Time.realtimeSinceStartup + 5f;
            while (city.AutopilotActive &&
                   Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(city.AutopilotActive, Is.False,
                "Real right-click path must reach its legal destination.");
            Assert.That(
                Vector2.Distance(
                    new Vector2(
                        city.transform.position.x,
                        city.transform.position.z),
                    new Vector2(targetCorner.x, targetCorner.z)),
                Is.LessThanOrEqualTo(.081f),
                "The real controller must stop inside its arrival tolerance.");

            Assert.That(world.Coordinates.TryCellToWorld(
                startX,
                startY,
                0f,
                out Vector3 deploymentCorner), Is.True);
            yield return ClickMouse(
                MouseButton.Right,
                Camera.main.WorldToScreenPoint(
                    deploymentCorner + new Vector3(.5f, 0f, .5f)));
            Assert.That(city.AutopilotActive, Is.True);
            Assert.That(city.Destination.HasValue, Is.True);
            Assert.That(city.Destination.Value.X, Is.EqualTo(startX));
            Assert.That(city.Destination.Value.Y, Is.EqualTo(startY));
            deadline = Time.realtimeSinceStartup + 5f;
            while (city.AutopilotActive &&
                   Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(city.AutopilotActive, Is.False,
                "Real right-click must return to the legal deployment cell.");
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(world.TryWorldToCell(
                body.position,
                out int deploymentX,
                out int deploymentY), Is.True);
            Assert.That(deploymentX, Is.EqualTo(startX));
            Assert.That(deploymentY, Is.EqualTo(startY));
            Assert.That(
                CityDeploymentRules.Validate(
                    world.Model,
                    deploymentX,
                    deploymentY),
                Is.EqualTo(CityDeploymentFailure.None));

            yield return TapKey(Key.F);
            Assert.That(
                city.Mode,
                Is.EqualTo(CityMode.Deploying)
                    .Or.EqualTo(CityMode.Fortress),
                "F remained Mobile. LastDeploymentFailure=" +
                city.LastDeploymentFailure + " LastFailureReason=" +
                city.LastFailureReason);
            deadline = Time.realtimeSinceStartup + 3f;
            while (city.Mode != CityMode.Fortress &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(city.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(city.LastDeploymentFailure,
                Is.EqualTo(CityDeploymentFailure.None));

            // Stage 2 (first half): real B + quickbar input builds the formal
            // research station; T must open the real research-tree UGUI.
            int beforeCount = session.Instances.Count;
            yield return TapKey(Key.B);
            Assert.That(interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            yield return TapKey(Key.Digit5);
            Assert.That(interaction.Selected,
                Is.SameAs(BuildingCatalog.ResearchStation));
            yield return MoveToInnerCell(city, 3, 2);
            Assert.That(placement.CurrentEvaluation.IsValid,
                Is.True,
                placement.CurrentEvaluation.PrimaryFailure.ToString());
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(session.Instances.Count,
                Is.EqualTo(beforeCount + 1));
            GrayboxBuildingInstance3D station = session.Instances[beforeCount];
            Assert.That(station.Placement.Definition,
                Is.SameAs(BuildingCatalog.ResearchStation));
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(10));
            yield return TapKey(Key.Escape);

            deadline = Time.realtimeSinceStartup + 3f;
            while (station.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(station.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));

            yield return TapKey(Key.T);
            GameObject researchPanel = RequireSceneObject(
                "ResearchTreePanel",
                includeInactive: true);
            Assert.That(researchPanel.activeInHierarchy, Is.True);
            Assert.That(RequireSceneObject(
                "Research.Node.core.research.automated-machinery"),
                Is.Not.Null);
            Assert.That(RequireSceneObject("Research.Start"), Is.Not.Null);

            yield return ResearchThroughUi(
                session,
                DemoResearchCatalog.BasicMetallurgyId);
            yield return ResearchThroughUi(
                session,
                DemoResearchCatalog.AmmunitionAssemblyId);
            yield return ResearchThroughUi(
                session,
                DemoResearchCatalog.AutomatedDefenseId);
            Assert.That(session.Inventory.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(26));
            Assert.That(session.Inventory.Get(ResourceIds.Biomass), Is.Zero);

            yield return TapKey(Key.T);
            Assert.That(researchPanel.activeInHierarchy, Is.False);

            // Stage 3: build the formal production and defense chain through
            // B, the quickbar, real world pointers and natural construction.
            yield return TapKey(Key.B);
            Assert.That(interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            yield return TapKey(Key.Digit1);
            Assert.That(interaction.Selected,
                Is.SameAs(BuildingCatalog.MiningStation));
            var miningNodeIds = new string[2];
            var miningNodeAmountsBefore = new int[2];
            for (var index = 0; index < miningNodeIds.Length; index++)
            {
                yield return MoveToCompatibleIronNode(world, placement);
                miningNodeIds[index] = placement.CurrentEvaluation
                    .CompatibleResourceNodeId;
                Assert.That(miningNodeIds[index], Is.Not.Null.And.Not.Empty);
                if (index > 0)
                    Assert.That(miningNodeIds[index],
                        Is.Not.EqualTo(miningNodeIds[0]));
                int count = session.Instances.Count;
                yield return ClickMouse(
                    MouseButton.Left,
                    mouse.position.ReadValue());
                Assert.That(session.Instances.Count, Is.EqualTo(count + 1));
                GrayboxBuildingInstance3D mine = session.Instances[count];
                Assert.That(mine.Placement.Definition,
                    Is.SameAs(BuildingCatalog.MiningStation));
                miningNodeAmountsBefore[index] = world.Model.Get(
                    mine.BoundResourceNode.X,
                    mine.BoundResourceNode.Y).ResourceAmount;
                yield return WaitForCompletion(mine, 3f);
            }
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.MiningStation.Id.Value), Is.EqualTo(2));

            yield return TapKey(Key.Digit6);
            Assert.That(interaction.Selected,
                Is.SameAs(BuildingCatalog.Smelter));
            for (var index = 0; index < 2; index++)
            {
                yield return MoveToValidGroundPreview(
                    city,
                    world,
                    placement,
                    "smelter " + (index + 1),
                    session);
                int count = session.Instances.Count;
                yield return ClickMouse(
                    MouseButton.Left,
                    mouse.position.ReadValue());
                Assert.That(session.Instances.Count, Is.EqualTo(count + 1));
                GrayboxBuildingInstance3D smelter =
                    session.Instances[count];
                Assert.That(smelter.Placement.Definition,
                    Is.SameAs(BuildingCatalog.Smelter));
                yield return WaitForCompletion(smelter, 3f);
            }
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.Smelter.Id.Value), Is.EqualTo(2));

            GrayboxProductionController3D production =
                Object.FindObjectOfType<GrayboxProductionController3D>();
            Assert.That(production, Is.Not.Null);
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.GreaterThanOrEqualTo(18),
                "The exact fixture leaves an 18-alloy construction reserve; " +
                "real smelting may already have added to it.");
            int requiredAlloyBeforeAssembler =
                BuildingCatalog.Assembler.Cost +
                FormalProductionDefinitionCatalog.Assembly.InputCapacity +
                BuildingCatalog.MachineGunTurret.Cost;
            Assert.That(requiredAlloyBeforeAssembler,
                Is.GreaterThanOrEqualTo(30));
            deadline = Time.realtimeSinceStartup + 15f;
            while (session.Inventory.Get(ResourceIds.Alloy) <
                       requiredAlloyBeforeAssembler &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.GreaterThanOrEqualTo(requiredAlloyBeforeAssembler),
                CaptureProductionDiagnostics(session, placement));

            ProductionBuildingObservability[] mineStates = production.Snapshot
                .Entries.Where(value => value.BuildingDefinitionId ==
                    BuildingCatalog.MiningStation.Id.Value).ToArray();
            ProductionBuildingObservability[] smelterStates = production.Snapshot
                .Entries.Where(value => value.BuildingDefinitionId ==
                    BuildingCatalog.Smelter.Id.Value).ToArray();
            Assert.That(mineStates, Has.Length.EqualTo(2));
            Assert.That(smelterStates, Has.Length.EqualTo(2));
            Assert.That(mineStates.All(value =>
                value.IsLogisticsConnected &&
                !string.IsNullOrWhiteSpace(value.BoundResourceNodeId) &&
                value.BoundResourceId == ResourceIds.Iron), Is.True);
            Assert.That(smelterStates.All(value =>
                value.IsLogisticsConnected &&
                !value.IsPlayerPaused), Is.True);
            for (var index = 0; index < miningNodeIds.Length; index++)
            {
                GrayboxBuildingInstance3D mine = session.Instances.Single(
                    value => value.BoundResourceNode.StableId ==
                        miningNodeIds[index]);
                Assert.That(world.Model.Get(
                        mine.BoundResourceNode.X,
                        mine.BoundResourceNode.Y).ResourceAmount,
                    Is.LessThan(miningNodeAmountsBefore[index]),
                    miningNodeIds[index]);
            }

            yield return TapKey(Key.Digit7);
            Assert.That(interaction.Selected,
                Is.SameAs(BuildingCatalog.Assembler));
            yield return MoveToValidInnerPreview(city, placement);
            int assemblerIndex = session.Instances.Count;
            int assemblerAlloySpend = 0;
            System.Action<string, int, ResourceChangeAttribution>
                captureAssemblerSpend = (resourceId, delta, attribution) =>
                {
                    if (resourceId == ResourceIds.Alloy && delta < 0 &&
                        attribution.Kind ==
                            ResourceChangeAttributionKind.Unspecified)
                        assemblerAlloySpend -= delta;
                };
            session.CityStorage.AttributedChanged += captureAssemblerSpend;
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            session.CityStorage.AttributedChanged -= captureAssemblerSpend;
            Assert.That(session.Instances.Count,
                Is.EqualTo(assemblerIndex + 1));
            Assert.That(assemblerAlloySpend,
                Is.EqualTo(BuildingCatalog.Assembler.Cost));
            GrayboxBuildingInstance3D assembler =
                session.Instances[assemblerIndex];
            Assert.That(assembler.Placement.Definition,
                Is.SameAs(BuildingCatalog.Assembler));
            yield return WaitForCompletion(assembler, 3f);
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.Assembler.Id.Value), Is.EqualTo(1));

            deadline = Time.realtimeSinceStartup + 3f;
            while (session.Inventory.Get(ResourceIds.Alloy) <
                       BuildingCatalog.MachineGunTurret.Cost &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.GreaterThanOrEqualTo(
                    BuildingCatalog.MachineGunTurret.Cost),
                CaptureProductionDiagnostics(session, placement));

            yield return TapKey(Key.Digit8);
            Assert.That(interaction.Selected,
                Is.SameAs(BuildingCatalog.MachineGunTurret));
            yield return MoveToValidGroundPreview(
                city,
                world,
                placement,
                "machine-gun turret",
                session);
            int turretIndex = session.Instances.Count;
            int turretAlloySpend = 0;
            System.Action<string, int, ResourceChangeAttribution>
                captureTurretSpend = (resourceId, delta, attribution) =>
                {
                    if (resourceId == ResourceIds.Alloy && delta < 0 &&
                        attribution.Kind ==
                            ResourceChangeAttributionKind.Unspecified)
                        turretAlloySpend -= delta;
                };
            session.CityStorage.AttributedChanged += captureTurretSpend;
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            session.CityStorage.AttributedChanged -= captureTurretSpend;
            Assert.That(session.Instances.Count,
                Is.EqualTo(turretIndex + 1));
            Assert.That(turretAlloySpend,
                Is.EqualTo(BuildingCatalog.MachineGunTurret.Cost));
            GrayboxBuildingInstance3D turret = session.Instances[turretIndex];
            Assert.That(turret.Placement.Definition,
                Is.SameAs(BuildingCatalog.MachineGunTurret));
            yield return WaitForCompletion(turret, 3f);
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.MachineGunTurret.Id.Value), Is.EqualTo(1));
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.Zero);

            // Stage 4: observe real production feeding the real defense loop.
            GrayboxDefenseController3D defense =
                Object.FindObjectOfType<GrayboxDefenseController3D>();
            Assert.That(production, Is.Not.Null);
            Assert.That(defense, Is.Not.Null);
            Assert.That(production.Snapshot.TryGet(
                assembler.StableInstanceId,
                out ProductionBuildingObservability assemblerProduction),
                Is.True);
            Assert.That(assemblerProduction.OutputResourceId,
                Is.EqualTo(ResourceIds.Ammunition));

            deadline = Time.realtimeSinceStartup + 5f;
            int visibleProducedAmmunition = VisibleProducedAmmunition(
                session,
                production,
                assembler.StableInstanceId);
            while (visibleProducedAmmunition <= 0 &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                visibleProducedAmmunition = VisibleProducedAmmunition(
                    session,
                    production,
                    assembler.StableInstanceId);
            }
            Assert.That(visibleProducedAmmunition, Is.GreaterThan(0),
                "The real assembler must expose produced ammunition in its " +
                "output or the connected city network.");

            int ammunitionBaseline = 0;
            GrayboxDefenseTowerSnapshot3D firingTower = null;
            GrayboxDefenseEnemySnapshot3D damagedEnemy = null;
            deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                GrayboxDefenseRuntimeSnapshot3D defenseSnapshot =
                    defense.Snapshot;
                if (defenseSnapshot != null)
                {
                    GrayboxDefenseTowerSnapshot3D observedTower =
                        defenseSnapshot.Towers.FirstOrDefault(value =>
                            value.StableId == turret.StableInstanceId);
                    if (observedTower != null)
                    {
                        ammunitionBaseline = Mathf.Max(
                            ammunitionBaseline,
                            observedTower.Ammo);
                        GrayboxDefenseEnemySnapshot3D observedEnemy =
                            string.IsNullOrWhiteSpace(observedTower.TargetId)
                                ? null
                                : defenseSnapshot.Enemies.FirstOrDefault(
                                    value => value.StableId ==
                                        observedTower.TargetId);
                        if (defenseSnapshot.TutorialWaveTriggerCount > 0 &&
                            defenseSnapshot.AliveEnemyCount > 0 &&
                            observedTower.Status ==
                                GrayboxDefenseTowerStatus3D.Firing &&
                            observedEnemy != null &&
                            observedEnemy.CurrentHealth > 0 &&
                            observedEnemy.CurrentHealth <
                                EnemyCatalog.Gnawer.MaximumHealth &&
                            observedTower.Ammo < ammunitionBaseline)
                        {
                            firingTower = observedTower;
                            damagedEnemy = observedEnemy;
                            break;
                        }
                    }
                }
                yield return null;
            }

            Assert.That(defense.Snapshot.TutorialWaveTriggerCount,
                Is.GreaterThan(0));
            Assert.That(defense.Snapshot.AliveEnemyCount, Is.GreaterThan(0));
            Assert.That(firingTower, Is.Not.Null,
                "The real tower must acquire and fire on a living tutorial " +
                "enemy while consuming produced ammunition.");
            Assert.That(firingTower.TargetId, Is.Not.Null.And.Not.Empty);
            Assert.That(firingTower.Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(damagedEnemy, Is.Not.Null);
            Assert.That(damagedEnemy.CurrentHealth,
                Is.GreaterThan(0)
                    .And.LessThan(EnemyCatalog.Gnawer.MaximumHealth));
            Assert.That(firingTower.Ammo, Is.LessThan(ammunitionBaseline));

            // Stage 5: while combat is still live, real F must open the
            // manifest. Real UGUI assigns every ground asset atomically.
            GrayboxEvacuationController3D evacuation =
                Object.FindObjectOfType<GrayboxEvacuationController3D>();
            Assert.That(evacuation, Is.Not.Null);
            int groundAssetCount = session.Instances.Count(value =>
                value.IsPlayerOwned &&
                value.Placement.Site == BuildingSite.Ground);
            Assert.That(groundAssetCount, Is.EqualTo(5));

            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(evacuation.IsManifestOpen, Is.True);
            Assert.That(defense.Snapshot.AliveEnemyCount, Is.GreaterThan(0));
            EvacuationManifestViewModel combatManifest =
                evacuation.CaptureManifestView();
            Assert.That(combatManifest.IsInCombat, Is.True);
            Assert.That(combatManifest.Items, Has.Count.EqualTo(
                groundAssetCount));
            Assert.That(combatManifest.Items.Any(value =>
                value.Input.Count > 0 ||
                value.Output.Count > 0 ||
                value.AmmunitionAmount > 0), Is.True,
                "The combat manifest must expose real production payloads.");

            yield return SubmitButton("Evacuation.All.FullDismantle");
            EvacuationManifestViewModel assignedManifest =
                evacuation.CaptureManifestView();
            Assert.That(assignedManifest.CanConfirm, Is.True,
                assignedManifest.FailureReason);
            Assert.That(assignedManifest.Items.All(value =>
                value.Treatment ==
                    BuildingEvacuationTreatment.FullDismantle), Is.True);
            yield return SubmitButton("Evacuation.Confirm");
            Assert.That(evacuation.IsManifestOpen, Is.False);
            Assert.That(evacuation.IsProcessing, Is.True);
            EvacuationQueueViewModel pausedQueue =
                evacuation.CaptureQueueView();
            Assert.That(pausedQueue.BatchId, Is.Not.Null.And.Not.Empty);
            Assert.That(pausedQueue.BatchIsInCombat, Is.True);
            Assert.That(pausedQueue.BatchProductivityMultiplier,
                Is.EqualTo(session.ProductivityMultiplier).Within(.0001f));
            Assert.That(pausedQueue.IsPaused, Is.False);
            Assert.That(evacuation.Work, Has.Count.EqualTo(groundAssetCount));
            Assert.That(evacuation.Work.All(value =>
                value.Treatment ==
                    BuildingEvacuationTreatment.FullDismantle), Is.True);

            deadline = Time.realtimeSinceStartup + 12f;
            while (evacuation.IsProcessing &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(evacuation.IsProcessing, Is.False,
                evacuation.CaptureQueueView().LastFailureReason);
            Assert.That(evacuation.IsBlocked, Is.False);
            Assert.That(session.HasPlayerOwnedGroundInstances, Is.False);

            // Stage 6: only after atomic ground evacuation may packing finish;
            // a final real right-click proves the city remains driveable.
            bool observedPacking = city.Mode == CityMode.Packing;
            deadline = Time.realtimeSinceStartup + 5f;
            while (city.Mode != CityMode.Mobile &&
                   Time.realtimeSinceStartup < deadline)
            {
                observedPacking |= city.Mode == CityMode.Packing;
                yield return null;
            }
            Assert.That(observedPacking, Is.True);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(evacuation.IsProcessing, Is.False);
            for (var closeAttempt = 0;
                 closeAttempt < 2 &&
                 interaction.State !=
                    GrayboxBuildingInteractionState.Inactive;
                 closeAttempt++)
                yield return TapKey(Key.Escape);
            Assert.That(interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));

            Assert.That(world.TryWorldToCell(
                body.position,
                out int mobileX,
                out int mobileY), Is.True);
            FindReachableRoundTripCell(
                world.Model,
                mobileX,
                mobileY,
                out int driveX,
                out int driveY);
            Assert.That(world.Coordinates.TryCellToWorld(
                driveX,
                driveY,
                0f,
                out Vector3 driveCorner), Is.True);
            Vector3 driveStart = body.position;
            yield return ClickMouse(
                MouseButton.Right,
                Camera.main.WorldToScreenPoint(
                    driveCorner + new Vector3(.5f, 0f, .5f)));
            Assert.That(
                city.AutopilotActive ||
                Vector3.Distance(body.position, driveStart) > .5f,
                Is.True,
                "The real right-click must start or already complete movement.");
            deadline = Time.realtimeSinceStartup + 5f;
            while (city.AutopilotActive &&
                   Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(city.AutopilotActive, Is.False);
            Assert.That(Vector3.Distance(body.position, driveStart),
                Is.GreaterThan(.5f));
        }

        private static int VisibleProducedAmmunition(
            GrayboxBuildingSession3D session,
            GrayboxProductionController3D production,
            string assemblerStableId)
        {
            int amount = session.CityStorage.GetNetworkAmount(
                ResourceIds.Ammunition);
            if (production.Snapshot.TryGet(
                    assemblerStableId,
                    out ProductionBuildingObservability assembler) &&
                assembler.OutputResourceId == ResourceIds.Ammunition)
            {
                amount += assembler.OutputAmount;
            }
            return amount;
        }

        private IEnumerator ResearchThroughUi(
            GrayboxBuildingSession3D session,
            string researchId)
        {
            ResearchDefinition definition =
                DemoResearchCatalog.Find(researchId);
            Assert.That(definition, Is.Not.Null, researchId);
            var amountsBefore = new int[definition.Costs.Count];
            for (var index = 0; index < definition.Costs.Count; index++)
            {
                ResourceAmount cost = definition.Costs[index];
                amountsBefore[index] =
                    session.Inventory.Get(cost.ResourceId);
            }

            yield return ClickUiElement(RequireSceneObject(
                "Research.Node." + researchId));
            yield return ClickUiElement(RequireSceneObject("Research.Start"));

            Assert.That(session.Research.Active, Is.SameAs(definition));
            for (var index = 0; index < definition.Costs.Count; index++)
            {
                ResourceAmount cost = definition.Costs[index];
                Assert.That(
                    session.Inventory.Get(cost.ResourceId),
                    Is.EqualTo(amountsBefore[index] - cost.Amount),
                    researchId + " " + cost.ResourceId);
            }

            float deadline = Time.realtimeSinceStartup + 6f;
            while (!session.IsResearchCompleted(researchId) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(session.IsResearchCompleted(researchId), Is.True,
                researchId);
            Assert.That(session.Research.Active, Is.Null, researchId);
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

        private IEnumerator SubmitButton(string name)
        {
            UnityEngine.UI.Button button = Object
                .FindObjectsOfType<UnityEngine.UI.Button>(true)
                .SingleOrDefault(value => value.name == name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(button.gameObject), name);
            yield return TapKey(Key.Enter);
        }

        private IEnumerator MoveToInnerCell(
            GrayboxMobileCityController3D city,
            int x,
            int y)
        {
            Transform platform = city.transform.Find("InnerCityPlatform");
            Assert.That(platform, Is.Not.Null);
            BoxCollider surface = platform.GetComponent<BoxCollider>();
            Assert.That(surface, Is.Not.Null);
            Vector3 worldPoint = city.transform.TransformPoint(new Vector3(
                -1.28f + (x + .5f) * .32f,
                0f,
                -.96f + (y + .5f) * .32f));
            worldPoint.y = surface.bounds.max.y;
            QueueMouse(Camera.main.WorldToScreenPoint(worldPoint));
            yield return null;
        }

        private IEnumerator MoveToValidInnerPreview(
            GrayboxMobileCityController3D city,
            GrayboxBuildingPlacementController3D placement)
        {
            for (var y = 0; y < 6; y++)
            for (var x = 0; x < 8; x++)
            {
                yield return MoveToInnerCell(city, x, y);
                if (placement.CurrentHit.Site == BuildingSite.InnerCity &&
                    placement.CurrentEvaluation.IsValid)
                    yield break;
            }
            Assert.Fail("The formal inner city must expose a valid preview.");
        }

        private IEnumerator MoveToCompatibleIronNode(
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement)
        {
            for (var x = 0; x < world.Model.Width; x++)
            for (var y = 0; y < world.Model.Height; y++)
            {
                WorldCell cell = world.Model.Get(x, y);
                if (!cell.HasResource || cell.ResourceId != ResourceIds.Iron)
                    continue;
                yield return MoveToGroundCell(world, x, y);
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.IsValid &&
                    !string.IsNullOrEmpty(placement.CurrentEvaluation
                        .CompatibleResourceNodeId))
                    yield break;
            }
            Assert.Fail("The formal seed must expose an unused iron node.");
        }

        private IEnumerator MoveToValidGroundPreview(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement,
            string definitionLabel,
            GrayboxBuildingSession3D session)
        {
            Assert.That(world.TryWorldToCell(
                city.transform.position,
                out int cityX,
                out int cityY), Is.True);
            for (var radius = 2;
                 radius <= Object.FindObjectOfType<
                     GrayboxBuildingSession3D>().GroundBuildRadius;
                 radius++)
            for (var x = cityX - radius; x <= cityX + radius; x++)
            for (var y = cityY - radius; y <= cityY + radius; y++)
            {
                if (!world.Coordinates.TryCellToWorld(
                    x,
                    y,
                    0f,
                    out Vector3 corner))
                    continue;
                QueueMouse(Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f)));
                yield return null;
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.IsValid)
                    yield break;
            }
            Assert.Fail(
                "No valid ground preview for " + definitionLabel + ". " +
                CaptureProductionDiagnostics(session, placement));
        }

        private static string CaptureProductionDiagnostics(
            GrayboxBuildingSession3D session,
            GrayboxBuildingPlacementController3D placement)
        {
            var result = new StringBuilder();
            result.Append("hit=")
                .Append(placement.CurrentHit.Site)
                .Append('(')
                .Append(placement.CurrentHit.X)
                .Append(',')
                .Append(placement.CurrentHit.Y)
                .Append(") failure=")
                .Append(placement.CurrentEvaluation.PrimaryFailure)
                .Append(" cityIron=")
                .Append(session.Inventory.Get(ResourceIds.Iron))
                .Append(" cityAlloy=")
                .Append(session.Inventory.Get(ResourceIds.Alloy));

            GrayboxProductionController3D production =
                Object.FindObjectOfType<GrayboxProductionController3D>();
            if (production?.Snapshot == null)
                return result.Append(" production=<null>").ToString();

            result.Append(" production=[");
            for (var index = 0;
                 index < production.Snapshot.Entries.Count;
                 index++)
            {
                ProductionBuildingObservability entry =
                    production.Snapshot.Entries[index];
                if (index > 0) result.Append("; ");
                result.Append(entry.BuildingDefinitionId)
                    .Append('#')
                    .Append(entry.StableInstanceId)
                    .Append(" stop=")
                    .Append(entry.StopReason)
                    .Append(" node=")
                    .Append(entry.BoundResourceNodeId ?? "-")
                    .Append(" nodeResource=")
                    .Append(entry.BoundResourceId ?? "-")
                    .Append(" nodeRemaining=")
                    .Append(entry.BoundResourceRemaining)
                    .Append(" input=")
                    .Append(entry.InputResourceId ?? "-")
                    .Append(':')
                    .Append(entry.InputAmount)
                    .Append(" output=")
                    .Append(entry.OutputResourceId ?? "-")
                    .Append(':')
                    .Append(entry.OutputAmount)
                    .Append(" progress=")
                    .Append(entry.ProgressSeconds.ToString("0.00"));
            }
            return result.Append(']').ToString();
        }

        private IEnumerator MoveToGroundCell(
            GrayboxWorldView3D world,
            int x,
            int y)
        {
            Assert.That(world.Coordinates.TryCellToWorld(
                x,
                y,
                0f,
                out Vector3 corner), Is.True);
            QueueMouse(Camera.main.WorldToScreenPoint(
                corner + new Vector3(.5f, 0f, .5f)));
            yield return null;
        }

        private IEnumerator ClickUiElement(GameObject value)
        {
            InputSystemUIInputModule module =
                Object.FindObjectOfType<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.enabled, Is.True);
            Assert.That(module.point?.action?.enabled, Is.True);
            Assert.That(module.leftClick?.action?.enabled, Is.True);
            Assert.That(value.activeInHierarchy, Is.True, value.name);
            RectTransform rect = value.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, value.name);
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

        private static IEnumerator WaitForCompletion(
            GrayboxBuildingInstance3D instance,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (instance.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed),
                instance.Placement.Definition.Id.Value);
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

        private static void FindReachableRoundTripCell(
            WorldMapModel map,
            int startX,
            int startY,
            out int targetX,
            out int targetY)
        {
            for (var radius = 2; radius <= 8; radius++)
            for (var x = Mathf.Max(0, startX - radius);
                 x <= Mathf.Min(map.Width - 1, startX + radius);
                 x++)
            for (var y = Mathf.Max(0, startY - radius);
                 y <= Mathf.Min(map.Height - 1, startY + radius);
                 y++)
            {
                if (Mathf.Max(Mathf.Abs(x - startX), Mathf.Abs(y - startY)) !=
                    radius ||
                    !CityPathfinder.TryFindPath(
                        map,
                        startX,
                        startY,
                        x,
                        y,
                        out WorldGridPoint[] outbound) ||
                    outbound.Length <= 1 ||
                    !CityPathfinder.TryFindPath(
                        map,
                        x,
                        y,
                        startX,
                        startY,
                        out WorldGridPoint[] inbound) ||
                    inbound.Length == 0)
                {
                    continue;
                }

                WorldGridPoint previous = inbound.Length == 1
                    ? new WorldGridPoint(x, y)
                    : inbound[inbound.Length - 2];
                if (!((previous.X > startX && previous.Y == startY) ||
                      (previous.Y > startY && previous.X == startX)))
                {
                    continue;
                }

                targetX = x;
                targetY = y;
                return;
            }

            throw new AssertionException(
                "Seed 8128 must expose a reachable round trip to the legal " +
                "deployment cell.");
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
    }
}
