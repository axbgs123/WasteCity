using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingRuntimeSceneTests
    {
        private const string SceneName = "GrayboxPrototype3D";

        private Keyboard keyboard;
        private Keyboard replacementKeyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousInputUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private float previousTimeScale;
        private RenderPipelineAsset previousGraphics;
        private RenderPipelineAsset previousQuality;

        private static readonly CatalogExpectation[] CatalogExpectations =
        {
            new CatalogExpectation(BuildingCatalog.Housing, BuildingMenuCategory.Basic, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.Wall, BuildingMenuCategory.Basic, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.ResearchStation, BuildingMenuCategory.Basic, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.MiningStation, BuildingMenuCategory.Production, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.Smelter, BuildingMenuCategory.Production, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.Assembler, BuildingMenuCategory.Production, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.Warehouse, BuildingMenuCategory.Logistics, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.AutomatedRepairBay, BuildingMenuCategory.Logistics, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.MachineGunTurret, BuildingMenuCategory.Defense, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.LaserTower, BuildingMenuCategory.Defense, ContentRoute.Core),
            new CatalogExpectation(BuildingCatalog.PowerPlant, BuildingMenuCategory.Route, ContentRoute.Technology),
            new CatalogExpectation(BuildingCatalog.SpiritFireFurnace, BuildingMenuCategory.Route, ContentRoute.Cultivation),
            new CatalogExpectation(BuildingCatalog.ArtifactWorkshop, BuildingMenuCategory.Route, ContentRoute.Cultivation),
            new CatalogExpectation(BuildingCatalog.SwordArrayTower, BuildingMenuCategory.Route, ContentRoute.Cultivation),
            new CatalogExpectation(BuildingCatalog.SpiritGatheringArray, BuildingMenuCategory.Route, ContentRoute.Cultivation),
            new CatalogExpectation(BuildingCatalog.AlchemyChamber, BuildingMenuCategory.Route, ContentRoute.Cultivation),
            new CatalogExpectation(BuildingCatalog.PuppetWorkshop, BuildingMenuCategory.Route, ContentRoute.Cultivation),
            new CatalogExpectation(BuildingCatalog.ColonyPool, BuildingMenuCategory.Route, ContentRoute.BiologicalAscension),
            new CatalogExpectation(BuildingCatalog.BreedingChamber, BuildingMenuCategory.Route, ContentRoute.BiologicalAscension),
            new CatalogExpectation(BuildingCatalog.SporeTower, BuildingMenuCategory.Route, ContentRoute.BiologicalAscension),
            new CatalogExpectation(BuildingCatalog.MetabolicFurnace, BuildingMenuCategory.Route, ContentRoute.BiologicalAscension),
            new CatalogExpectation(BuildingCatalog.AcidTower, BuildingMenuCategory.Route, ContentRoute.BiologicalAscension),
            new CatalogExpectation(BuildingCatalog.BehemothPen, BuildingMenuCategory.Route, ContentRoute.BiologicalAscension),
            new CatalogExpectation(BuildingCatalog.ResonanceFurnace, BuildingMenuCategory.Route, ContentRoute.Psionics),
            new CatalogExpectation(BuildingCatalog.PsionicWorkshop, BuildingMenuCategory.Route, ContentRoute.Psionics),
            new CatalogExpectation(BuildingCatalog.MindSpire, BuildingMenuCategory.Route, ContentRoute.Psionics),
            new CatalogExpectation(BuildingCatalog.ConsciousnessNetwork, BuildingMenuCategory.Route, ContentRoute.Psionics),
            new CatalogExpectation(BuildingCatalog.ShieldGenerator, BuildingMenuCategory.Route, ContentRoute.Psionics)
        };

        [UnitySetUp]
        public IEnumerator LoadGrayboxScene()
        {
            previousTimeScale = Time.timeScale;
            previousGraphics = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            previousInputUpdateMode = InputSystem.settings.updateMode;
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

            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadGrayboxScene()
        {
            try
            {
                Time.timeScale = 1f;
                yield return LoadEmptyScene();
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
                        if (replacementKeyboard != null &&
                            replacementKeyboard.added)
                            InputSystem.RemoveDevice(replacementKeyboard);
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
                            try
                            {
                                InputSystem.settings.updateMode =
                                    previousInputUpdateMode;
                            }
                            finally
                            {
                                try
                                {
                                    InputSystem.settings
                                        .editorInputBehaviorInPlayMode =
                                        previousEditorInputBehavior;
                                }
                                finally
                                {
                                    try
                                    {
                                        InputSystem.settings
                                            .backgroundBehavior =
                                            previousBackgroundBehavior;
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            GraphicsSettings.defaultRenderPipeline =
                                                previousGraphics;
                                        }
                                        finally
                                        {
                                            try
                                            {
                                                QualitySettings.renderPipeline =
                                                    previousQuality;
                                            }
                                            finally
                                            {
                                                Time.timeScale =
                                                    previousTimeScale;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                Assert.That(keyboard == null || !keyboard.added, Is.True);
                Assert.That(
                    replacementKeyboard == null ||
                    !replacementKeyboard.added,
                    Is.True);
                Assert.That(mouse == null || !mouse.added, Is.True);
                Assert.That(
                    InputSystem.settings.updateMode,
                    Is.EqualTo(previousInputUpdateMode));
                Assert.That(
                    InputSystem.settings.editorInputBehaviorInPlayMode,
                    Is.EqualTo(previousEditorInputBehavior));
                Assert.That(
                    InputSystem.settings.backgroundBehavior,
                    Is.EqualTo(previousBackgroundBehavior));
                Assert.That(
                    GraphicsSettings.defaultRenderPipeline,
                    Is.SameAs(previousGraphics));
                Assert.That(
                    QualitySettings.renderPipeline,
                    Is.SameAs(previousQuality));
                Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale));
                Debug.Log(
                    "Task11InputSettingsRestored updateMode=" +
                    previousInputUpdateMode +
                    " backgroundBehavior=" + previousBackgroundBehavior +
                    " editorInputBehaviorInPlayMode=" +
                    previousEditorInputBehavior +
                    " virtualDevicesRemaining=0");
            }
        }

        [UnityTest]
        public IEnumerator SceneReload_RehydratesSerializedBuildingRuntime()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingSurfaceProjector3D projector =
                Object.FindObjectOfType<
                    GrayboxBuildingSurfaceProjector3D>();
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            Transform platform = city == null
                ? null
                : city.transform.Find("InnerCityPlatform");

            Assert.That(session, Is.Not.Null);
            Assert.That(session.DevelopmentFixtureEnabled, Is.False,
                "Committed GrayboxPrototype3D must boot with formal resources; developer grants remain explicit test setup only.");
            Assert.That(session.Inventory, Is.Not.Null);
            Assert.That(session.Research, Is.Not.Null);
            Assert.That(session.GroundGrid, Is.Not.Null);
            Assert.That(session.InnerGrid, Is.Not.Null);
            Assert.That(session.InnerGrid.Width, Is.EqualTo(8));
            Assert.That(session.InnerGrid.Height, Is.EqualTo(6));
            Assert.That(session.GroundBuildRadius, Is.EqualTo(8));
            Assert.That(projector, Is.Not.Null);
            Assert.That(platform, Is.Not.Null);
            Assert.That(platform.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(
                FindSlot("building.range.ground-boundary"),
                Is.Not.Null,
                "IDEA0008 stable range slot survives runtime rehydrate.");
            Assert.That(developer, Is.Not.Null);
#if UNITY_EDITOR
            Assert.That(developer.IsRuntimeAvailable, Is.True);
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneReload_EnablesSerializedUiActions()
        {
            yield return null;
            InputSystemUIInputModule module =
                Object.FindObjectOfType<InputSystemUIInputModule>(true);

            Assert.That(module, Is.Not.Null);
            AssertEnabled(module.point, "point");
            AssertEnabled(module.leftClick, "leftClick");
            AssertEnabled(module.move, "move");
            AssertEnabled(module.submit, "submit");
            AssertEnabled(module.cancel, "cancel");
        }

        [UnityTest]
        public IEnumerator VirtualF_FromDefaultSpawn_DeploysCityAndCompletesTransition()
        {
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();

            Assert.That(city, Is.Not.Null);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Deploying));

            float deadline = Time.realtimeSinceStartup + 4f;
            while (city.Mode == CityMode.Deploying &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(city.Mode, Is.EqualTo(CityMode.Fortress));
        }

        [UnityTest]
        public IEnumerator AllDeclaredMobileInnerBuildings_ProjectAsValidInFormalScene()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            var expectedMobileInner = new[]
            {
                BuildingCatalog.Housing,
                BuildingCatalog.Warehouse,
                BuildingCatalog.ResearchStation,
                BuildingCatalog.Assembler,
                BuildingCatalog.PsionicWorkshop,
                BuildingCatalog.ConsciousnessNetwork,
                BuildingCatalog.ShieldGenerator,
                BuildingCatalog.AutomatedRepairBay,
                BuildingCatalog.AlchemyChamber,
                BuildingCatalog.PuppetWorkshop
            };
            BuildingDefinition[] declaredMobileInner =
                BuildingCatalog.BuildMenu.Where(definition =>
                    BuildingMobilityRules.CanConstruct(
                        definition,
                        BuildingSite.InnerCity,
                        CityMode.Mobile)).ToArray();

            CollectionAssert.AreEquivalent(
                expectedMobileInner,
                declaredMobileInner);
            modifier.UnlockAllResearch();
            Assert.That(modifier.SetPopulation(2000), Is.True);
            Assert.That(session.Population, Is.EqualTo(2000));
            for (var index = 0; index < ResourceIds.All.Length; index++)
                Assert.That(
                    modifier.AddResource(ResourceIds.All[index], 1000),
                    Is.True,
                    ResourceIds.All[index]);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            yield return null;

            var prerequisiteOrder = new[]
            {
                BuildingCatalog.Smelter,
                BuildingCatalog.ResonanceFurnace,
                BuildingCatalog.SpiritFireFurnace,
                BuildingCatalog.ArtifactWorkshop,
                BuildingCatalog.Assembler,
                BuildingCatalog.PsionicWorkshop
            };
            for (var index = 0; index < prerequisiteOrder.Length; index++)
            {
                BuildingDefinition prerequisite = prerequisiteOrder[index];
                interaction.Select(prerequisite);
                yield return MoveToValidGroundPreview(
                    city,
                    world,
                    placement);
                Assert.That(
                    placement.CurrentEvaluation.IsValid,
                    Is.True,
                    prerequisite.Id.Value + ": " +
                    placement.CurrentEvaluation.PrimaryFailure);
                yield return ClickMouse(
                    MouseButton.Left,
                    mouse.position.ReadValue());
                modifier.CompleteAllConstruction();
                Assert.That(
                    session.CompletedBuildingCount(
                        prerequisite.Id.Value),
                    Is.EqualTo(1),
                    prerequisite.Id.Value);
            }

            Assert.That(modifier.SetCityMode(CityMode.Mobile), Is.True);
            yield return null;
            for (var index = 0; index < expectedMobileInner.Length; index++)
            {
                BuildingDefinition definition = expectedMobileInner[index];
                interaction.Select(definition);
                yield return MoveToInnerPreview(city);
                Assert.That(
                    placement.CurrentHit.Site,
                    Is.EqualTo(BuildingSite.InnerCity),
                    definition.Id.Value);
                Assert.That(
                    placement.CurrentEvaluation.IsValid,
                    Is.True,
                    definition.Id.Value + ": " +
                    placement.CurrentEvaluation.PrimaryFailure);
            }

            interaction.Select(BuildingCatalog.ResearchStation);
            yield return MoveToInnerPreview(city);
            Assert.That(
                placement.CurrentHit.Site,
                Is.EqualTo(BuildingSite.InnerCity));
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            int countBefore = session.Instances.Count;
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(
                session.Instances,
                Has.Count.EqualTo(countBefore + 1));
            GrayboxBuildingInstance3D placed =
                session.Instances[countBefore];
            Assert.That(
                placed.Placement.Definition,
                Is.SameAs(BuildingCatalog.ResearchStation));
            Assert.That(
                placed.Placement.Site,
                Is.EqualTo(BuildingSite.InnerCity));
            Assert.That(
                placed.State,
                Is.EqualTo(
                    GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));

            modifier.SetResource(ResourceIds.Alloy, 0);
            interaction.Select(BuildingCatalog.Housing);
            yield return MoveToInnerCell(city, 0, 0);
            Assert.That(
                placement.CurrentEvaluation.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.InsufficientMaterials));
        }

        [UnityTest]
        public IEnumerator IDEA0011_RealInputBuildsTwoTwoOneProductionChain()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            Assert.That(modifier.UnlockResearch(
                DemoResearchCatalog.BasicMetallurgyId), Is.True);
            Assert.That(modifier.UnlockResearch(
                DemoResearchCatalog.AmmunitionAssemblyId), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Stone, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 100), Is.True);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit1);
            for (var index = 0; index < 2; index++)
            {
                yield return MoveToCompatibleResourceNode(world, placement);
                yield return ClickMouse(
                    MouseButton.Left,
                    mouse.position.ReadValue());
                modifier.CompleteAllConstruction();
                yield return null;
            }

            yield return TapKey(Key.Digit6);
            for (var index = 0; index < 2; index++)
            {
                yield return MoveToValidGroundPreview(
                    city,
                    world,
                    placement);
                yield return ClickMouse(
                    MouseButton.Left,
                    mouse.position.ReadValue());
                modifier.CompleteAllConstruction();
                yield return null;
            }

            yield return TapKey(Key.Digit7);
            yield return MoveToInnerCell(city, 3, 2);
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True,
                placement.CurrentEvaluation.PrimaryFailure.ToString());
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            modifier.CompleteAllConstruction();
            yield return TapKey(Key.Escape);

            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.MiningStation.Id.Value), Is.EqualTo(2));
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.Smelter.Id.Value), Is.EqualTo(2));
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.Assembler.Id.Value), Is.EqualTo(1));
            Assert.That(modifier.SetResource(ResourceIds.Iron, 0), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 0), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Ammunition, 0), Is.True);

            Time.timeScale = 20f;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (session.Inventory.Get(ResourceIds.Ammunition) < 2 &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;

            Assert.That(session.Inventory.Get(ResourceIds.Ammunition),
                Is.GreaterThanOrEqualTo(2));
            Text amount = Object.FindObjectsOfType<Text>(true)
                .First(value => value.name ==
                    "ResourceStatus.Item." + ResourceIds.Ammunition +
                    ".Amount");
            Assert.That(amount.transform.parent.gameObject.activeInHierarchy,
                Is.True);
            Assert.That(amount.text,
                Does.Contain(session.Inventory.Get(ResourceIds.Ammunition)
                    .ToString()));
        }

        [UnityTest]
        public IEnumerator
            IDEA0009_VirtualInput_HousingCoversInnerGroundAndEvacuation()
        {
            const string requirement = "IDEA0009 Housing";
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxEvacuationController3D evacuation =
                Object.FindObjectOfType<GrayboxEvacuationController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            Assert.That(
                modifier.AddResource(ResourceIds.Alloy, 1000),
                Is.True,
                requirement + " fixture resources");
            Assert.That(
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast100),
                Is.True,
                requirement + " fixture construction speed");
            Assert.That(
                city.Mode,
                Is.EqualTo(CityMode.Mobile),
                requirement + " starts Mobile");

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit2);
            Assert.That(
                interaction.Selected,
                Is.SameAs(BuildingCatalog.Housing),
                requirement + " must be selected through virtual keyboard");
            yield return MoveToInnerPreview(city);
            Assert.That(
                placement.CurrentHit.Site,
                Is.EqualTo(BuildingSite.InnerCity),
                requirement + " Mobile InnerCity projection");
            Assert.That(
                placement.CurrentEvaluation.IsValid,
                Is.True,
                requirement + " Mobile InnerCity evaluation: " +
                placement.CurrentEvaluation.PrimaryFailure);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(
                session.Instances,
                Has.Count.EqualTo(1),
                requirement + " Mobile InnerCity virtual placement");
            GrayboxBuildingInstance3D inner = session.Instances[0];
            Assert.That(
                inner.Placement.Definition,
                Is.SameAs(BuildingCatalog.Housing),
                requirement + " Mobile InnerCity canonical definition");
            Assert.That(
                inner.Placement.Site,
                Is.EqualTo(BuildingSite.InnerCity),
                requirement + " Mobile InnerCity instance site");
            yield return WaitForCompletion(inner, 2f);

            yield return MoveToGroundPreviewContaining(
                city,
                world,
                placement,
                BuildingPlacementFailure.InvalidCityMode);
            Assert.That(
                placement.CurrentHit.Site,
                Is.EqualTo(BuildingSite.Ground),
                requirement + " Mobile Ground projection");
            Assert.That(
                placement.CurrentEvaluation.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.InvalidCityMode),
                requirement + " Mobile Ground unified evaluation");
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(
                session.Instances,
                Is.EqualTo(new[] { inner }),
                requirement + " Mobile Ground virtual click must be rejected");

            yield return TapKey(Key.F);
            Assert.That(
                city.Mode,
                Is.EqualTo(CityMode.Deploying),
                requirement + " virtual F begins deployment");
            float deploymentDeadline = Time.realtimeSinceStartup + 4f;
            while (city.Mode == CityMode.Deploying &&
                   Time.realtimeSinceStartup < deploymentDeadline)
                yield return null;
            Assert.That(
                city.Mode,
                Is.EqualTo(CityMode.Fortress),
                requirement + " virtual F completes Fortress transition");

            yield return MoveToValidGroundPreview(
                city,
                world,
                placement);
            Assert.That(
                placement.CurrentHit.Site,
                Is.EqualTo(BuildingSite.Ground),
                requirement + " Fortress Ground projection");
            Assert.That(
                placement.CurrentEvaluation.IsValid,
                Is.True,
                requirement + " Fortress Ground evaluation: " +
                placement.CurrentEvaluation.PrimaryFailure);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(
                session.Instances,
                Has.Count.EqualTo(2),
                requirement + " Fortress Ground virtual placement");
            GrayboxBuildingInstance3D ground = session.Instances[1];
            Assert.That(
                ground.Placement.Definition,
                Is.SameAs(BuildingCatalog.Housing),
                requirement + " Fortress Ground canonical definition");
            Assert.That(
                ground.Placement.Site,
                Is.EqualTo(BuildingSite.Ground),
                requirement + " Fortress Ground instance site");
            yield return WaitForCompletion(ground, 2f);

            yield return TapKey(Key.F);
            Assert.That(
                city.Mode,
                Is.EqualTo(CityMode.Fortress),
                requirement + " evacuation entry holds Fortress");
            Assert.That(
                evacuation.IsManifestOpen,
                Is.True,
                requirement + " virtual F opens evacuation manifest");
            Assert.That(
                FindButton(
                    "Evacuation.Item." + ground.StableInstanceId +
                    ".FullDismantle"),
                Is.Not.Null,
                requirement + " Fortress Ground appears in manifest");
            Assert.That(
                FindButton(
                    "Evacuation.Item." + inner.StableInstanceId +
                    ".FullDismantle"),
                Is.Null,
                requirement + " InnerCity stays outside manifest");
            yield return SubmitButton(
                "Evacuation.Item." + ground.StableInstanceId +
                ".FullDismantle");
            yield return SubmitButton("Evacuation.Confirm");
            Assert.That(
                evacuation.IsManifestOpen,
                Is.False,
                requirement + " evacuation manifest confirms");
            Assert.That(
                evacuation.Work,
                Has.Count.EqualTo(1),
                requirement + " evacuation work count");
            Assert.That(
                evacuation.Work[0].StableInstanceId,
                Is.EqualTo(ground.StableInstanceId),
                requirement + " evacuation work stable ID");
            Assert.That(
                evacuation.Work[0].Treatment,
                Is.EqualTo(
                    BuildingEvacuationTreatment.FullDismantle),
                requirement + " evacuation treatment");
            Assert.That(
                evacuation.IsProcessing,
                Is.True,
                requirement + " evacuation processing starts");
            float evacuationDeadline = Time.realtimeSinceStartup + 6f;
            while (evacuation.IsProcessing &&
                   Time.realtimeSinceStartup < evacuationDeadline)
                yield return null;
            Assert.That(
                evacuation.IsProcessing,
                Is.False,
                requirement + " evacuation processing completes");
            Assert.That(
                session.Instances.Contains(inner),
                Is.True,
                requirement + " InnerCity survives evacuation");
            Assert.That(
                session.Instances.Contains(ground),
                Is.False,
                requirement + " Fortress Ground uses evacuation removal");
        }

        [UnityTest]
        public IEnumerator DeveloperInjectedCatalog_MapsAllTwentyEightRuntimeCards()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            modifier.UnlockAllResearch();
            for (var index = 0; index < ResourceIds.All.Length; index++)
                Assert.That(
                    modifier.AddResource(ResourceIds.All[index], 1000),
                    Is.True,
                    ResourceIds.All[index]);

            yield return TapKey(Key.B);
            var presenter = new GrayboxBuildingCatalogPresenter3D();
            IReadOnlyList<GrayboxBuildingCatalogItem3D> items =
                presenter.Query(session, null, null, string.Empty);
            Assert.That(items, Has.Count.EqualTo(28));
            Assert.That(CatalogExpectations, Has.Length.EqualTo(28));
            for (var index = 0; index < CatalogExpectations.Length; index++)
            {
                CatalogExpectation expected = CatalogExpectations[index];
                GrayboxBuildingCatalogItem3D actual = default;
                bool found = false;
                for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
                    if (ReferenceEquals(
                            items[itemIndex].Definition,
                            expected.Definition))
                    {
                        actual = items[itemIndex];
                        found = true;
                        break;
                    }
                Assert.That(found, Is.True, expected.Definition.Id.Value);
                Assert.That(actual.Category, Is.EqualTo(expected.Category));
                Assert.That(actual.Route, Is.EqualTo(expected.Route));
                Assert.That(
                    FindButton("Catalog.Card." + expected.Definition.Id.Value),
                    Is.Not.Null,
                    expected.Definition.Id.Value);
                Image costIcon = FindTransform(
                        "Catalog.Card." + expected.Definition.Id.Value +
                        ".Cost.Icon")
                    .GetComponent<Image>();
                Assert.That(costIcon, Is.Not.Null, expected.Definition.Id.Value);
                Assert.That(
                    costIcon.sprite,
                    Is.SameAs(ResourceIconCatalog3D.Resolve(
                        expected.Definition.CostId)),
                    expected.Definition.Id.Value);
            }
        }

        [UnityTest]
        public IEnumerator FocusedSearch_OwnsKeyboardThenF10TogglesExactlyOnce()
        {
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingMenuView3D menu =
                Object.FindObjectOfType<GrayboxBuildingMenuView3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxLeaderController3D leader =
                Object.FindObjectOfType<GrayboxLeaderController3D>();
            GrayboxCameraController3D cameraController =
                Object.FindObjectOfType<GrayboxCameraController3D>();
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>();
            EventSystem eventSystem = EventSystem.current;

            yield return TapKey(Key.B);
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            InputField search = FindInput("Catalog.Search");
            Assert.That(search, Is.Not.Null);
            yield return ClickUi(search.GetComponent<RectTransform>());
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(search.gameObject));
            Assert.That(menu.HasKeyboardFocus(), Is.True);
            Assert.That(search.isFocused, Is.True);

            Vector3 cityBefore = city.transform.position;
            Vector3 leaderBefore = leader.transform.position;
            BuildingOrientation orientationBefore = interaction.Orientation;
            GrayboxBuildingInteractionState returnBefore =
                interaction.CatalogReturnState;
            CityMode modeBefore = city.Mode;
            bool autopilotBefore = city.AutopilotActive;
            CameraFollowMode cameraModeBefore = cameraController.Mode;
            DirectControlTarget cameraTargetBefore =
                cameraController.CurrentTarget;
            bool developerBefore = developer.IsPanelOpen;

            yield return TapTextKey(Key.W, 'w');
            Assert.That(search.text, Is.EqualTo("w"));
            Assert.That(menu.SearchText, Is.EqualTo("w"));
            yield return TapTextKey(Key.A, 'a');
            yield return TapTextKey(Key.S, 's');
            yield return TapTextKey(Key.D, 'd');
            yield return TapTextKey(Key.B, 'b');
            yield return TapTextKey(Key.R, 'r');
            yield return TapTextKey(Key.E, 'e');
            yield return TapTextKey(Key.T, 't');
            yield return TapTextKey(Key.Digit1, '1');
            yield return TapTextKey(Key.Digit2, '2');
            yield return TapTextKey(Key.Digit3, '3');
            yield return TapTextKey(Key.Digit4, '4');
            yield return TapTextKey(Key.Digit5, '5');
            yield return TapTextKey(Key.Digit6, '6');
            yield return TapTextKey(Key.Digit7, '7');
            yield return TapTextKey(Key.Digit8, '8');
            yield return TapTextKey(Key.Digit9, '9');
            yield return TapTextKey(Key.Digit0, '0');
            yield return TapTextKey(Key.F, 'f');
            yield return TapKey(Key.F10);
            yield return TapKey(Key.Home);
            yield return TapKey(Key.Delete);
            yield return TapKey(Key.Enter);

            Assert.That(menu.SearchText, Is.Not.Empty);
            AssertPositionUnchanged(cityBefore, city.transform.position);
            AssertPositionUnchanged(leaderBefore, leader.transform.position);
            Assert.That(interaction.Selected, Is.Null);
            Assert.That(interaction.Orientation, Is.EqualTo(orientationBefore));
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(returnBefore));
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(city.Mode, Is.EqualTo(modeBefore));
            Assert.That(city.AutopilotActive, Is.EqualTo(autopilotBefore));
            Assert.That(cameraController.Mode, Is.EqualTo(cameraModeBefore));
            Assert.That(cameraController.CurrentTarget, Is.EqualTo(cameraTargetBefore));
            Assert.That(developer.IsPanelOpen, Is.EqualTo(developerBefore));
            Assert.That(FindTransform("InventoryCraftingPanel").gameObject
                    .activeSelf,
                Is.False);
            Assert.That(FindTransform("ResearchTreePanel").gameObject
                    .activeSelf,
                Is.False);

            yield return TapKey(Key.Escape);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            yield return null;
            yield return TapKey(Key.Escape);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));

            eventSystem.SetSelectedGameObject(null);
            yield return null;
            yield return TapKey(Key.F10);
#if UNITY_EDITOR
            Assert.That(developer.IsPanelOpen, Is.Not.EqualTo(developerBefore));
#endif
            yield return null;
#if UNITY_EDITOR
            Assert.That(developer.IsPanelOpen, Is.Not.EqualTo(developerBefore));
#endif
        }

        [UnityTest]
        public IEnumerator SearchTextBridge_TracksMenuAndKeyboardLifecycles()
        {
            GrayboxBuildingMenuView3D menu =
                Object.FindObjectOfType<GrayboxBuildingMenuView3D>();

            yield return TapKey(Key.B);
            InputField search = FindInput("Catalog.Search");
            Assert.That(search, Is.Not.Null);
            yield return ClickUi(search.GetComponent<RectTransform>());
            Assert.That(search.isFocused, Is.True);
            Assert.That(search.text, Is.Empty);

            menu.enabled = false;
            yield return TapTextKey(Key.X, 'x');
            Assert.That(search.text, Is.Empty);

            menu.enabled = true;
            yield return null;
            Assert.That(search.isFocused, Is.True);
            yield return TapTextKey(Key.Y, 'y');
            Assert.That(search.text, Is.EqualTo("y"));

            replacementKeyboard = InputSystem.AddDevice<Keyboard>();
            replacementKeyboard.MakeCurrent();
            yield return TapTextKey(replacementKeyboard, Key.Z, 'z');
            Assert.That(search.text, Is.EqualTo("yz"));

            InputSystem.RemoveDevice(replacementKeyboard);
            Assert.That(replacementKeyboard.added, Is.False);
            keyboard.MakeCurrent();
            yield return TapTextKey(keyboard, Key.Q, 'q');
            Assert.That(search.text, Is.EqualTo("yzq"));
        }

        [UnityTest]
        public IEnumerator VirtualBuildInput_CoversCatalogRotationSurfacesContinuityAndMovement()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxLeaderController3D leader =
                Object.FindObjectOfType<GrayboxLeaderController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            for (var index = 0; index < ResourceIds.All.Length; index++)
                modifier.AddResource(ResourceIds.All[index], 1000);

            Assert.That(presentation.IsBuildGridVisible, Is.False);
            yield return TapKey(Key.B);
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            Assert.That(
                FindSlot("building.range.ground-boundary")
                    .gameObject.activeInHierarchy,
                Is.True,
                "IDEA0008 real B input shows the canonical ground range.");
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            yield return TapKey(Key.B);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(presentation.IsBuildGridVisible, Is.False);

            yield return TapKey(Key.B);
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return TapKey(Key.Digit2);
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Housing));
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return TapKey(Key.B);
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return TapKey(Key.B);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Housing));
            Assert.That(presentation.IsBuildGridVisible, Is.True);

            interaction.Select(BuildingCatalog.BehemothPen);
            yield return TapKey(Key.R);
            Assert.That(interaction.Orientation, Is.EqualTo(
                BuildingOrientation.East));
            yield return MoveToAnyGroundPreview(city, world);
            Assert.That(placement.CurrentEvaluation.RotatedWidth, Is.EqualTo(2));
            Assert.That(placement.CurrentEvaluation.RotatedHeight, Is.EqualTo(3));

            Assert.That(modifier.SetCityMode(CityMode.Mobile), Is.True);
            interaction.Select(BuildingCatalog.Housing);
            yield return MoveToInnerPreview(city);
            Assert.That(placement.CurrentHit.Site, Is.EqualTo(
                BuildingSite.InnerCity));
            Assert.That(placement.CurrentHit.X, Is.InRange(0, 7));
            Assert.That(placement.CurrentHit.Y, Is.InRange(0, 5));
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            yield return HoldMovementAndAssertPreview(
                city.transform,
                Key.W,
                Vector2.up,
                interaction,
                placement);
            yield return HoldMovementAndAssertPreview(
                city.transform,
                Key.S,
                Vector2.down,
                interaction,
                placement);
            yield return HoldMovementAndAssertPreview(
                city.transform,
                Key.A,
                Vector2.left,
                interaction,
                placement);
            yield return HoldMovementAndAssertPreview(
                city.transform,
                Key.D,
                Vector2.right,
                interaction,
                placement);

            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            modifier.SetResource(ResourceIds.Stone, BuildingCatalog.Wall.Cost);
            interaction.Select(BuildingCatalog.Wall);
            yield return null;
            yield return MoveToValidGroundPreview(city, world, placement);
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            Assert.That(
                FindSlot("building.preview." + BuildingCatalog.Wall.Id.Value)
                    .FallbackColor,
                Is.EqualTo(new Color(.18f, .85f, .32f, .55f)));
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Wall));
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return MoveToGroundPreviewContaining(
                city,
                world,
                placement,
                BuildingPlacementFailure.InsufficientMaterials,
                requirePrimary: true);
            Assert.That(
                placement.CurrentEvaluation.Failures,
                Does.Contain(BuildingPlacementFailure.InsufficientMaterials));
            Assert.That(
                FindSlot("building.preview." + BuildingCatalog.Wall.Id.Value)
                    .FallbackColor,
                Is.EqualTo(new Color(.9f, .16f, .12f, .55f)));
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Text shortage = FindTransform("Placement.Status.Text")
                .GetComponent<Text>();
            Assert.That(shortage, Is.Not.Null);
            Assert.That(shortage.text,
                Does.Contain(
                    "无法建造城墙：缺少石料 2（拥有 0，需要 2）"));
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(presentation.IsBuildGridVisible, Is.True);

            modifier.SetResource(ResourceIds.Stone, 1000);
            yield return MoveToValidGroundPreview(city, world, placement);
            yield return HoldMovementAndAssertPreview(
                leader.transform,
                Key.W,
                Vector2.up,
                interaction,
                placement);
            yield return HoldMovementAndAssertPreview(
                leader.transform,
                Key.S,
                Vector2.down,
                interaction,
                placement);
            yield return HoldMovementAndAssertPreview(
                leader.transform,
                Key.A,
                Vector2.left,
                interaction,
                placement);
            yield return HoldMovementAndAssertPreview(
                leader.transform,
                Key.D,
                Vector2.right,
                interaction,
                placement);
        }

        [UnityTest]
        public IEnumerator VirtualPointerOwnership_CancelsBuildAndPreservesCameraControls()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxCameraController3D cameraController =
                Object.FindObjectOfType<GrayboxCameraController3D>();

            Assert.That(presentation.IsBuildGridVisible, Is.False);
            yield return TapKey(Key.B);
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return TapKey(Key.Digit2);
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return MoveToInnerPreview(city);
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            Button quickbar = FindButton("QuickbarSlot.1");
            Assert.That(quickbar, Is.Not.Null);
            Vector2 uiPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                quickbar.transform.position);
            yield return ClickUi(quickbar.GetComponent<RectTransform>());
            Assert.That(session.Instances, Is.Empty);

            yield return DragMouse(uiPoint, uiPoint + new Vector2(50f, 20f));
            Assert.That(cameraController.Mode, Is.EqualTo(
                CameraFollowMode.Following));
            Assert.That(session.Instances, Is.Empty);

            Assert.That(city.AutopilotActive, Is.False);
            Vector2 worldPoint = new Vector2(
                Screen.width * .5f,
                Screen.height * .5f);
            yield return ClickMouse(MouseButton.Right, worldPoint);
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(presentation.IsBuildGridVisible, Is.False);
            Assert.That(city.AutopilotActive, Is.False);

            Vector2 dragEnd = worldPoint + new Vector2(100f, 40f);
            yield return DragMouse(worldPoint, dragEnd);
            Assert.That(cameraController.Mode, Is.EqualTo(CameraFollowMode.Free));
            yield return TapKey(Key.Home);
            Assert.That(cameraController.Mode, Is.EqualTo(
                CameraFollowMode.Following));
        }

        [UnityTest]
        public IEnumerator VirtualCancelUi_ReturnStateControlsBuildGridVisibility()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxConstructionController3D construction =
                Object.FindObjectOfType<
                    GrayboxConstructionController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            modifier.AddResource(ResourceIds.Stone, 1000);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);

            interaction.Select(BuildingCatalog.Wall);
            yield return MoveToValidGroundPreview(city, world, placement);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            GrayboxBuildingInstance3D instance = session.Instances[0];
            yield return null;
            Assert.That(instance.Progress.Normalized, Is.GreaterThan(0f));
            Assert.That(
                construction.SelectInstance(instance.StableInstanceId),
                Is.True);

            yield return ClickButton("Construction.Cancel");
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CancelConfirmation));
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return ClickButton("Construction.Confirm.No");
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(presentation.IsBuildGridVisible, Is.True);

            yield return ClickMouse(
                MouseButton.Right,
                new Vector2(Screen.width * .5f, Screen.height * .5f));
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(presentation.IsBuildGridVisible, Is.False);

            yield return ClickButton("Construction.Cancel");
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CancelConfirmation));
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return ClickButton("Construction.Confirm.No");
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(presentation.IsBuildGridVisible, Is.False);

            interaction.Select(BuildingCatalog.Wall);
            yield return null;
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return ClickButton("Construction.Cancel");
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CancelConfirmation));
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            yield return ClickButton("Construction.Confirm.Yes");
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(presentation.IsBuildGridVisible, Is.True);
            Assert.That(session.Instances, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DeveloperPanel_RealButtonsMutateSharedSessionModels()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxDeveloperModifierBootstrap3D developer =
                Object.FindObjectOfType<
                    GrayboxDeveloperModifierBootstrap3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit2);
            yield return MoveToInnerPreview(city);
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            GrayboxBuildingInstance3D instance = session.Instances[0];
            Assert.That(instance.State, Is.EqualTo(
                GrayboxBuildingInstanceState.UnderConstruction));

            int ironBefore = session.Inventory.Get(ResourceIds.Iron);
            yield return TapKey(Key.F10);
#if UNITY_EDITOR
            Assert.That(developer.IsPanelOpen, Is.True);
            GameObject panel = GameObject.Find("Graybox Developer Modifier");
            Assert.That(panel, Is.Not.Null);
            Text[] labels = panel.GetComponentsInChildren<Text>(true);
            Assert.That(labels, Has.Some.Matches<Text>(
                value => value.text == "开发模式"));
            yield return SubmitButton("Complete Construction");
            Assert.That(instance.State, Is.EqualTo(
                GrayboxBuildingInstanceState.Completed));
            yield return ClickButton("Resource +100");
            Assert.That(
                session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(ironBefore + 100));
            yield return SubmitButton("Unlock All");
            Assert.That(
                session.HasContactedRoute(ContentRoute.Technology),
                Is.True);
            Assert.That(
                session.IsResearchCompleted(
                    BuildingCatalog.PowerPlant.RequiredResearchId),
                Is.True);
            yield return SubmitButton("Set Fortress");
            Assert.That(city.Mode, Is.EqualTo(CityMode.Fortress));
            yield return SubmitButton("Multiplier 10x");
            Assert.That(session.ConstructionMultiplier, Is.EqualTo(10f));
#else
            Assert.Ignore("Editor-only developer behavior.");
#endif
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
        }

        [UnityTest]
        public IEnumerator VirtualPlacement_ExposesEveryFailureAndMiningHighlight()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            for (var index = 0; index < ResourceIds.All.Length; index++)
                modifier.AddResource(ResourceIds.All[index], 1000);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);

            interaction.Select(BuildingCatalog.Housing);
            yield return MoveToValidGroundPreview(city, world, placement);
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);

            yield return MoveMouse(new Vector2(-10000f, -10000f));
            AssertFailure(placement, BuildingPlacementFailure.ProjectionFailed);

            interaction.Select(BuildingCatalog.BehemothPen);
            yield return MoveToInnerCell(city, 7, 5);
            AssertFailure(placement, BuildingPlacementFailure.OutOfBounds);

            interaction.Select(BuildingCatalog.Wall);
            yield return MoveToInnerCell(city, 3, 2);
            AssertFailure(placement, BuildingPlacementFailure.UnsupportedSite);

            Assert.That(modifier.SetCityMode(CityMode.Mobile), Is.True);
            yield return MoveToAnyGroundPreview(city, world);
            AssertFailure(placement, BuildingPlacementFailure.InvalidCityMode);

            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            yield return MoveToGroundRadius(city, world, placement, 8, false);
            Assert.That(
                placement.CurrentEvaluation.Failures.Contains(
                    BuildingPlacementFailure.OutsideBuildRange),
                Is.False);
            yield return MoveToGroundRadius(city, world, placement, 9, true);
            AssertFailure(placement, BuildingPlacementFailure.OutsideBuildRange);

            Assert.That(
                world.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY),
                Is.True);
            yield return MoveToGroundCell(world, cityX + 1, cityY);
            AssertFailure(placement, BuildingPlacementFailure.CityOccupied);

            yield return MoveToTraversal(
                world,
                placement,
                WorldTraversalKind.DeepWater,
                WorldTraversalKind.Cliff);
            AssertFailure(placement, BuildingPlacementFailure.InvalidTerrain);
            yield return MoveToTraversal(
                world,
                placement,
                WorldTraversalKind.Ruins);
            AssertFailure(placement, BuildingPlacementFailure.Obstacle);

            interaction.Select(BuildingCatalog.MiningStation);
            yield return MoveToGroundPreviewContaining(
                city,
                world,
                placement,
                BuildingPlacementFailure.IncompatibleResourceNode);
            AssertFailure(
                placement,
                BuildingPlacementFailure.IncompatibleResourceNode);

            interaction.Select(BuildingCatalog.PowerPlant);
            yield return MoveToAnyGroundPreview(city, world);
            AssertFailure(placement, BuildingPlacementFailure.ContentUnavailable);

            modifier.UnlockAllResearch();
            yield return null;
            yield return MoveToAnyGroundPreview(city, world);
            AssertFailure(placement, BuildingPlacementFailure.PopulationRequired);

            interaction.Select(BuildingCatalog.Assembler);
            yield return MoveToInnerCell(city, 3, 2);
            AssertFailure(
                placement,
                BuildingPlacementFailure.PrerequisiteBuildingRequired);

            interaction.Select(BuildingCatalog.MiningStation);
            yield return MoveToCompatibleResourceNode(world, placement);
            Assert.That(
                placement.CurrentEvaluation.CompatibleResourceNodeId,
                Is.Not.Null.And.Not.Empty);
            Assert.That(
                presentation.ActiveMiningNodeHighlightCount,
                Is.GreaterThan(0),
                "IDEA0010 shows every compatible resource node state.");
            Assert.That(
                presentation.ActiveMiningAnchorHighlightCount,
                Is.GreaterThan(0),
                "IDEA0010 shows truly legal anchors from evaluations.");
            string compatibleNodeId =
                placement.CurrentEvaluation.CompatibleResourceNodeId;
            GrayboxVisualSlot compatibleNode = FindSlot(
                "building.node-highlight." + compatibleNodeId);
            Assert.That(compatibleNode, Is.Not.Null);
            Assert.That(
                compatibleNode.FallbackColor,
                Is.EqualTo(new Color(.2f, .9f, .35f, .45f)));
            modifier.SetResource(
                BuildingCatalog.MiningStation.CostId,
                0);
            yield return MoveMouse(mouse.position.ReadValue());
            Assert.That(
                FindSlot("building.node-highlight." + compatibleNodeId)
                    .FallbackColor,
                Is.EqualTo(new Color(.85f, .62f, .12f, .55f)),
                "IDEA0010 real input refreshes colors after inventory mutation.");
            Assert.That(
                presentation.ActiveMiningAnchorHighlightCount,
                Is.Zero);

            interaction.Select(BuildingCatalog.Wall);
            modifier.SetResource(ResourceIds.Stone, 1000);
            yield return MoveToValidGroundPreview(city, world, placement);
            Vector2 occupied = mouse.position.ReadValue();
            yield return ClickMouse(MouseButton.Left, occupied);
            yield return MoveMouse(occupied);
            AssertFailure(placement, BuildingPlacementFailure.Overlap);

            interaction.Select(BuildingCatalog.Housing);
            modifier.SetResource(ResourceIds.Alloy, 0);
            yield return MoveToInnerCell(city, 3, 2);
            AssertFailure(
                placement,
                BuildingPlacementFailure.InsufficientMaterials);

            placement.Configure(
                session,
                city,
                world,
                null,
                presentation,
                interaction);
            yield return MoveMouse(new Vector2(
                Screen.width * .5f,
                Screen.height * .5f));
            AssertFailure(placement, BuildingPlacementFailure.MissingReference);
        }

        [UnityTest]
        public IEnumerator BUG0006_RealRRotatesSquarePreviewWithoutMovingItsAnchor()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            modifier.AddResource(ResourceIds.Alloy, 1000);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            interaction.Select(BuildingCatalog.Housing);
            yield return MoveToAnyGroundPreview(city, world);

            int anchorX = placement.CurrentHit.X;
            int anchorY = placement.CurrentHit.Y;
            GrayboxVisualSlot preview = FindSlot(
                "building.preview." + BuildingCatalog.Housing.Id.Value);
            GameObject previewRoot = preview.gameObject;

            for (var turn = 1; turn <= 4; turn++)
            {
                yield return TapKey(Key.R);
                Assert.That(placement.CurrentHit.X, Is.EqualTo(anchorX));
                Assert.That(placement.CurrentHit.Y, Is.EqualTo(anchorY));
                GrayboxVisualSlot rotated = FindSlot(
                    "building.preview." + BuildingCatalog.Housing.Id.Value);
                Assert.That(rotated.gameObject, Is.SameAs(previewRoot));
                Assert.That(
                    Mathf.DeltaAngle(
                        rotated.transform.eulerAngles.y,
                        turn % 4 * 90f),
                    Is.EqualTo(0f).Within(.01f));
            }
        }

        [UnityTest]
        public IEnumerator VirtualEvacuation_MixesTreatmentsPausesAndKeepsInnerCity()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxEvacuationController3D evacuation =
                Object.FindObjectOfType<GrayboxEvacuationController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            for (var index = 0; index < ResourceIds.All.Length; index++)
                modifier.AddResource(ResourceIds.All[index], 1000);
            modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast100);

            interaction.Select(BuildingCatalog.Housing);
            yield return MoveToInnerPreview(city);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            GrayboxBuildingInstance3D inner = session.Instances[0];
            yield return WaitForCompletion(inner, 2f);
            GrayboxVisualSlot innerSlot = FindSlot(
                "building.complete." + inner.StableInstanceId);
            Vector3 cityBefore = city.transform.position;
            Vector3 innerBefore = innerSlot.transform.position;
            yield return HoldKey(Key.W, 2);
            yield return WaitForInnerCityPresentation(
                city.transform,
                innerSlot.transform,
                cityBefore,
                innerBefore);
            Vector3 cityDelta = city.transform.position - cityBefore;
            Vector3 innerDelta = innerSlot.transform.position - innerBefore;
            Assert.That(innerDelta.x, Is.EqualTo(cityDelta.x).Within(.001f));
            Assert.That(innerDelta.z, Is.EqualTo(cityDelta.z).Within(.001f));

            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            yield return null;
            interaction.Select(BuildingCatalog.Wall);
            yield return MoveToValidGroundPreview(city, world, placement);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            GrayboxBuildingInstance3D wall = session.Instances[1];
            yield return WaitForCompletion(wall, 2f);

            interaction.Select(BuildingCatalog.Warehouse);
            yield return MoveToValidGroundPreview(city, world, placement);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            GrayboxBuildingInstance3D warehouseFull = session.Instances[2];
            yield return WaitForCompletion(warehouseFull, 2f);
            yield return MoveToValidGroundPreview(city, world, placement);
            yield return ClickMouse(
                MouseButton.Left,
                mouse.position.ReadValue());
            GrayboxBuildingInstance3D warehouseQuick = session.Instances[3];
            yield return WaitForCompletion(warehouseQuick, 2f);

            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(evacuation.IsManifestOpen, Is.True);
            Assert.That(
                FindButton(
                    "Evacuation.Item." + inner.StableInstanceId +
                    ".Abandon"),
                Is.Null);

            yield return SubmitButton("Evacuation.All.QuickDismantle");
            yield return SubmitButton(
                "Evacuation.Category.Basic.Abandon");
            yield return SubmitButton(
                "Evacuation.Item." + warehouseFull.StableInstanceId +
                ".FullDismantle");
            yield return SubmitButton("Evacuation.Confirm");
            Assert.That(evacuation.IsManifestOpen, Is.False);
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(evacuation.Work, Has.Count.EqualTo(3));
            Assert.That(
                evacuation.Work.Single(value =>
                    value.StableInstanceId == wall.StableInstanceId).Treatment,
                Is.EqualTo(BuildingEvacuationTreatment.Abandon));
            Assert.That(
                evacuation.Work.Single(value =>
                    value.StableInstanceId ==
                    warehouseFull.StableInstanceId).Treatment,
                Is.EqualTo(BuildingEvacuationTreatment.FullDismantle));
            Assert.That(
                evacuation.Work.Single(value =>
                    value.StableInstanceId ==
                    warehouseQuick.StableInstanceId).Treatment,
                Is.EqualTo(BuildingEvacuationTreatment.QuickDismantle));

            Time.timeScale = 0f;
            for (var frame = 0; frame < 20; frame++)
                yield return null;
            Assert.That(evacuation.IsProcessing, Is.True);
            Assert.That(
                session.Instances.Contains(warehouseFull),
                Is.True);

            Time.timeScale = 1f;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (evacuation.IsProcessing &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(evacuation.IsProcessing, Is.False);
            Assert.That(session.HasPlayerOwnedGroundInstances, Is.False);
            Assert.That(inner.IsPlayerOwned, Is.True);
            Assert.That(inner.Placement.Site, Is.EqualTo(
                BuildingSite.InnerCity));
            Assert.That(wall.State, Is.EqualTo(
                GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(
                session.Instances.Contains(warehouseFull),
                Is.False);
            Assert.That(
                session.Instances.Contains(warehouseQuick),
                Is.False);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Packing));
        }

        [UnityTest]
        public IEnumerator VirtualInput_CompletesMinimumBuildingAndEvacuationFlow()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxEvacuationController3D evacuation =
                Object.FindObjectOfType<GrayboxEvacuationController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast100),
                Is.True);
            yield return null;

            yield return TapKey(Key.B);
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            yield return TapKey(Key.Digit2);
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Housing));
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Previewing));

            yield return MoveToValidGroundPreview(
                city,
                world,
                placement);
            Assert.That(placement.CurrentHit.Site, Is.EqualTo(BuildingSite.Ground));
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            Vector2 buildPosition = mouse.position.ReadValue();
            yield return ClickMouse(MouseButton.Left, buildPosition);

            Assert.That(session.Instances, Has.Count.EqualTo(1));
            GrayboxBuildingInstance3D instance = session.Instances[0];
            Assert.That(
                instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            float constructionDeadline = Time.realtimeSinceStartup + 2f;
            while (instance.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < constructionDeadline)
                yield return null;
            Assert.That(
                instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));

            yield return TapKey(Key.F);
            Assert.That(evacuation.IsManifestOpen, Is.True);
            yield return ClickButton("Evacuation.All.FullDismantle");
            yield return ClickButton("Evacuation.Confirm");
            Assert.That(evacuation.IsManifestOpen, Is.False);
            Assert.That(evacuation.IsProcessing, Is.True);
            float evacuationDeadline = Time.realtimeSinceStartup + 6f;
            while (evacuation.IsProcessing &&
                   Time.realtimeSinceStartup < evacuationDeadline)
                yield return null;
            Assert.That(evacuation.IsProcessing, Is.False);
            Assert.That(session.HasPlayerOwnedGroundInstances, Is.False);
            Assert.That(
                city.Mode,
                Is.EqualTo(CityMode.Packing).Or.EqualTo(CityMode.Mobile));
        }

        private IEnumerator MoveToValidGroundPreview(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement)
        {
            Assert.That(
                world.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY),
                Is.True);
            for (var radius = 2; radius <= sessionRadius(placement); radius++)
            for (var x = cityX - radius; x <= cityX + radius; x++)
            for (var y = cityY - radius; y <= cityY + radius; y++)
            {
                if (!world.Coordinates.TryCellToWorld(
                        x,
                        y,
                        0f,
                        out Vector3 corner))
                    continue;
                Vector2 screen = Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f));
                yield return MoveMouse(screen);
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.IsValid)
                    yield break;
            }

            Assert.Fail("The serialized seed must expose a valid ground preview.");
        }

        private IEnumerator MoveToAnyGroundPreview(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world)
        {
            Assert.That(
                world.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY),
                Is.True);
            for (var radius = 2; radius <= 8; radius++)
            for (var x = cityX - radius; x <= cityX + radius; x++)
            for (var y = cityY - radius; y <= cityY + radius; y++)
            {
                if (!world.Coordinates.TryCellToWorld(
                        x,
                        y,
                        0f,
                        out Vector3 corner))
                    continue;
                yield return MoveMouse(Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f)));
                GrayboxBuildingPlacementController3D placement =
                    Object.FindObjectOfType<
                        GrayboxBuildingPlacementController3D>();
                if (placement.CurrentHit.IsValid &&
                    placement.CurrentHit.Site == BuildingSite.Ground)
                    yield break;
            }
            Assert.Fail("The serialized seed must expose a ground preview.");
        }

        private IEnumerator MoveToInnerPreview(
            GrayboxMobileCityController3D city)
        {
            yield return MoveToInnerCell(city, 3, 2);
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
            yield return MoveMouse(Camera.main.WorldToScreenPoint(
                worldPoint));
        }

        private IEnumerator MoveToGroundCell(
            GrayboxWorldView3D world,
            int x,
            int y)
        {
            Assert.That(
                world.Coordinates.TryCellToWorld(
                    x,
                    y,
                    0f,
                    out Vector3 corner),
                Is.True,
                x + "," + y);
            yield return MoveMouse(Camera.main.WorldToScreenPoint(
                corner + new Vector3(.5f, 0f, .5f)));
        }

        private IEnumerator MoveToGroundRadius(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement,
            int radius,
            bool expectOutside)
        {
            Assert.That(
                world.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY),
                Is.True);
            for (var x = cityX - radius; x <= cityX + radius; x++)
            for (var y = cityY - radius; y <= cityY + radius; y++)
            {
                if (Mathf.Max(Mathf.Abs(x - cityX), Mathf.Abs(y - cityY)) !=
                    radius ||
                    !world.Coordinates.ContainsCell(x, y))
                    continue;
                yield return MoveToGroundCell(world, x, y);
                if (placement.CurrentHit.Site != BuildingSite.Ground)
                    continue;
                bool outside =
                    placement.CurrentEvaluation.Failures != null &&
                    placement.CurrentEvaluation.Failures.Contains(
                        BuildingPlacementFailure.OutsideBuildRange);
                if (outside == expectOutside)
                    yield break;
            }
            Assert.Fail("No ground radius " + radius + " preview.");
        }

        private IEnumerator MoveToTraversal(
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement,
            params WorldTraversalKind[] traversal)
        {
            for (var x = 0; x < world.Model.Width; x++)
            for (var y = 0; y < world.Model.Height; y++)
            {
                WorldTraversalKind value = world.Model.Get(x, y).Traversal;
                if (System.Array.IndexOf(traversal, value) < 0)
                    continue;
                yield return MoveToGroundCell(world, x, y);
                if (placement.CurrentHit.Site == BuildingSite.Ground)
                    yield break;
            }
            Assert.Fail("The serialized seed lacks traversal " +
                string.Join(",", traversal));
        }

        private IEnumerator MoveToCompatibleResourceNode(
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement)
        {
            for (var x = 0; x < world.Model.Width; x++)
            for (var y = 0; y < world.Model.Height; y++)
            {
                WorldCell cell = world.Model.Get(x, y);
                if (!cell.HasResource ||
                    (cell.ResourceId != ResourceIds.Iron &&
                     cell.ResourceId != ResourceIds.EnergyCrystal))
                    continue;
                yield return MoveToGroundCell(world, x, y);
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.IsValid &&
                    !string.IsNullOrEmpty(
                        placement.CurrentEvaluation
                            .CompatibleResourceNodeId))
                    yield break;
            }
            Assert.Fail("The serialized seed lacks a compatible node.");
        }

        private IEnumerator MoveToGroundPreviewContaining(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement,
            BuildingPlacementFailure failure,
            bool requirePrimary = false)
        {
            Assert.That(
                world.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY),
                Is.True);
            for (var radius = 2; radius <= 8; radius++)
            for (var x = cityX - radius; x <= cityX + radius; x++)
            for (var y = cityY - radius; y <= cityY + radius; y++)
            {
                if (!world.Coordinates.TryCellToWorld(
                        x,
                        y,
                        0f,
                        out Vector3 corner))
                    continue;
                yield return MoveMouse(Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f)));
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.Failures != null &&
                    placement.CurrentEvaluation.Failures.Contains(failure) &&
                    (!requirePrimary ||
                     placement.CurrentEvaluation.PrimaryFailure == failure))
                    yield break;
            }
            Assert.Fail("No real ground preview exposed " + failure + ".");
        }

        private static int sessionRadius(
            GrayboxBuildingPlacementController3D placement)
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            Assert.That(placement, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            return session.GroundBuildRadius;
        }

        private IEnumerator TapKey(Key key)
        {
            QueueKeyboard(key);
            yield return null;
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator HoldKey(Key key, int fixedSteps)
        {
            QueueKeyboard(key);
            yield return null;
            for (var step = 0; step < fixedSteps; step++)
                yield return new WaitForFixedUpdate();
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator HoldMovementAndAssertPreview(
            Transform actor,
            Key key,
            Vector2 expectedDirection,
            GrayboxBuildingInteractionModel3D interaction,
            GrayboxBuildingPlacementController3D placement)
        {
            BuildingDefinition selected = interaction.Selected;
            BuildingOrientation orientation = interaction.Orientation;
            yield return new WaitForFixedUpdate();
            yield return null;
            Vector3 before = actor.position;

            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            yield return HoldKey(key, 2);

            Vector3 delta = actor.position - before;
            if (expectedDirection.x > 0f)
                Assert.That(delta.x, Is.GreaterThan(.001f), key.ToString());
            else if (expectedDirection.x < 0f)
                Assert.That(delta.x, Is.LessThan(-.001f), key.ToString());
            else
                Assert.That(
                    delta.x,
                    Is.EqualTo(0f).Within(.001f),
                    key.ToString());

            if (expectedDirection.y > 0f)
                Assert.That(delta.z, Is.GreaterThan(.001f), key.ToString());
            else if (expectedDirection.y < 0f)
                Assert.That(delta.z, Is.LessThan(-.001f), key.ToString());
            else
                Assert.That(
                    delta.z,
                    Is.EqualTo(0f).Within(.001f),
                    key.ToString());

            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(interaction.Selected, Is.SameAs(selected));
            Assert.That(interaction.Orientation, Is.EqualTo(orientation));
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
        }

        private static void AssertPositionUnchanged(
            Vector3 expected,
            Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(.001f));
        }

        private static IEnumerator WaitForInnerCityPresentation(
            Transform city,
            Transform inner,
            Vector3 cityBefore,
            Vector3 innerBefore)
        {
            for (var frame = 0; frame < 4; frame++)
            {
                yield return null;
                yield return null;
                Vector3 cityDelta = city.position - cityBefore;
                Vector3 innerDelta = inner.position - innerBefore;
                if (Mathf.Abs(innerDelta.x - cityDelta.x) <= .001f &&
                    Mathf.Abs(innerDelta.z - cityDelta.z) <= .001f)
                    yield break;
            }
        }

        private static IEnumerator WaitForCompletion(
            GrayboxBuildingInstance3D instance,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (instance.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(instance.State, Is.EqualTo(
                GrayboxBuildingInstanceState.Completed));
        }

        private IEnumerator TapTextKey(Key key, char character)
        {
            yield return TapTextKey(keyboard, key, character);
        }

        private IEnumerator TapTextKey(
            Keyboard device,
            Key key,
            char character)
        {
            InputSystem.QueueStateEvent(
                device,
                new KeyboardState(key));
            InputSystem.QueueTextEvent(device, character);
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(device));
            Assert.That(device[key].isPressed, Is.True, key.ToString());
            Assert.That(
                device[key].wasPressedThisFrame,
                Is.True,
                key.ToString());
            yield return null;
            InputSystem.QueueStateEvent(device, new KeyboardState());
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(device));
            Assert.That(device.anyKey.isPressed, Is.False);
            yield return null;
        }

        private IEnumerator MoveMouse(Vector2 position)
        {
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator ClickMouse(
            MouseButton button,
            Vector2 position)
        {
            QueueMouse(position, button);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator DragMouse(Vector2 start, Vector2 end)
        {
            QueueMouse(start, MouseButton.Middle);
            yield return null;
            QueueMouse(
                end,
                MouseButton.Middle,
                expectPressedThisFrame: false);
            yield return null;
            QueueMouse(end);
            yield return null;
        }

        private IEnumerator ClickButton(string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            yield return ClickUi(button.GetComponent<RectTransform>());
        }

        private IEnumerator SubmitButton(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null;
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.SameAs(button.gameObject));
            yield return TapKey(Key.Enter);
        }

        private IEnumerator ClickUi(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.position);
            yield return MoveMouse(screen);
            yield return ClickMouse(MouseButton.Left, screen);
        }

        private static Button FindButton(string name)
        {
            Button[] buttons = Object.FindObjectsOfType<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
                if (buttons[index].name == name)
                    return buttons[index];
            return null;
        }

        private static InputField FindInput(string name)
        {
            InputField[] inputs = Object.FindObjectsOfType<InputField>(true);
            for (var index = 0; index < inputs.Length; index++)
                if (inputs[index].name == name)
                    return inputs[index];
            return null;
        }

        private static Transform FindTransform(string name)
        {
            Transform[] values = Object.FindObjectsOfType<Transform>(true);
            for (var index = 0; index < values.Length; index++)
                if (values[index].name == name)
                    return values[index];
            Assert.Fail("Missing transform " + name + ".");
            return null;
        }

        private void QueueKeyboard(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(
                keyboard.anyKey.isPressed,
                Is.EqualTo(keys.Length > 0));
            for (var index = 0; index < keys.Length; index++)
            {
                Assert.That(keyboard[keys[index]].isPressed, Is.True);
                Assert.That(
                    keyboard[keys[index]].wasPressedThisFrame,
                    Is.True);
            }
        }

        private void QueueMouse(
            Vector2 position,
            MouseButton? button = null,
            bool expectPressedThisFrame = true)
        {
            var state = new MouseState { position = position };
            if (button.HasValue)
                state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(mouse.position.ReadValue(), Is.EqualTo(position));
            if (button == MouseButton.Left)
            {
                Assert.That(mouse.leftButton.isPressed, Is.True);
                Assert.That(mouse.leftButton.wasPressedThisFrame, Is.True);
            }
            else if (button == MouseButton.Right)
            {
                Assert.That(mouse.rightButton.isPressed, Is.True);
                Assert.That(
                    mouse.rightButton.wasPressedThisFrame,
                    Is.EqualTo(expectPressedThisFrame));
            }
            else if (button == MouseButton.Middle)
            {
                Assert.That(mouse.middleButton.isPressed, Is.True);
                Assert.That(
                    mouse.middleButton.wasPressedThisFrame,
                    Is.EqualTo(expectPressedThisFrame));
            }
            else
            {
                Assert.That(mouse.leftButton.isPressed, Is.False);
                Assert.That(mouse.rightButton.isPressed, Is.False);
                Assert.That(mouse.middleButton.isPressed, Is.False);
            }
        }

        private static GrayboxVisualSlot FindSlot(string stableId)
        {
            GrayboxVisualSlot[] slots =
                Object.FindObjectsOfType<GrayboxVisualSlot>(true);
            for (var index = 0; index < slots.Length; index++)
                if (slots[index].StableId == stableId)
                    return slots[index];
            Assert.Fail("Missing visual slot " + stableId + ".");
            return null;
        }

        private static void AssertFailure(
            GrayboxBuildingPlacementController3D placement,
            BuildingPlacementFailure failure)
        {
            Assert.That(
                placement.CurrentEvaluation.IsValid,
                Is.False,
                failure.ToString());
            Assert.That(
                placement.CurrentEvaluation.Failures,
                Does.Contain(failure));
        }

        private static void AssertEnabled(
            InputActionReference reference,
            string label)
        {
            Assert.That(reference, Is.Not.Null, label);
            Assert.That(reference.action, Is.Not.Null, label);
            Assert.That(reference.action.enabled, Is.True, label);
        }

        private static IEnumerator LoadEmptyScene()
        {
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            if (!graybox.IsValid() || !graybox.isLoaded)
            {
                yield return null;
                yield break;
            }

            Scene empty = SceneManager.CreateScene(
                "GrayboxBuildingRuntimeEmpty");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(graybox);
            yield return null;
        }

        private sealed class CatalogExpectation
        {
            public CatalogExpectation(
                BuildingDefinition definition,
                BuildingMenuCategory category,
                ContentRoute route)
            {
                Definition = definition;
                Category = category;
                Route = route;
            }

            public BuildingDefinition Definition { get; }
            public BuildingMenuCategory Category { get; }
            public ContentRoute Route { get; }
        }
    }
}
