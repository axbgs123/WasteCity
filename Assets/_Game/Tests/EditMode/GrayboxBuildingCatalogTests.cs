using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Content;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingCatalogTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        private static readonly CatalogExpectation[] Expectations =
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

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void Catalog_ProjectsExactlyTheApprovedBuildMenuClassification()
        {
            var presenter = new GrayboxBuildingCatalogPresenter3D();
            var context = new CatalogContext(population: 1000, allResearch: true, allPrerequisites: true, contactedRoutes: AllRoutes());
            string[] expectedIds = Expectations.Select(value => value.Definition.Id.Value).ToArray();

            TestContext.WriteLine("Task3CatalogIds=" + string.Join(",", expectedIds));
            Assert.That(GrayboxBuildingCatalogPresenter3D.BuildMenuCount, Is.EqualTo(28));
            Assert.That(expectedIds, Has.Length.EqualTo(28));
            Assert.That(expectedIds.Distinct().Count(), Is.EqualTo(28));
            Assert.That(BuildingCatalog.BuildMenu.Select(value => value.Id.Value), Is.EquivalentTo(expectedIds));
            Assert.That(expectedIds, Does.Not.Contain(BuildingCatalog.HeavyMachineGunTurret.Id.Value));
            Assert.That(expectedIds, Does.Not.Contain(BuildingCatalog.SwordRidingPlatform.Id.Value));
            Assert.That(
                presenter.Query(context, null, null, string.Empty)
                    .Select(value => value.Definition.Id.Value),
                Is.EqualTo(BuildingCatalog.BuildMenu.Select(value => value.Id.Value)));

            foreach (CatalogExpectation expected in Expectations)
            {
                Assert.That(GrayboxBuildingCatalogPresenter3D.CategoryOf(expected.Definition), Is.EqualTo(expected.Category));
                Assert.That(GrayboxBuildingCatalogPresenter3D.RouteOf(expected.Definition), Is.EqualTo(expected.Route));

                GrayboxBuildingCatalogItem3D card = presenter.Describe(context, expected.Definition);
                Assert.That(card.Category, Is.EqualTo(expected.Category));
                Assert.That(card.Route, Is.EqualTo(expected.Route));

                IReadOnlyList<GrayboxBuildingCatalogItem3D> queried = presenter.Query(
                    context,
                    expected.Category,
                    expected.Category == BuildingMenuCategory.Route ? expected.Route : (ContentRoute?)null,
                    string.Empty);
                Assert.That(queried.Select(value => value.Definition), Does.Contain(expected.Definition));
            }
        }

        [Test]
        public void Catalog_RejectsDefinitionsOutsideTheBuildMenu()
        {
            var presenter = new GrayboxBuildingCatalogPresenter3D();
            var context = new CatalogContext();
            var outside = new BuildingDefinition("core.building.outside", "外部建筑", 1, 1, "core.resource.stone", 1);

            Assert.Throws<ArgumentException>(() => GrayboxBuildingCatalogPresenter3D.CategoryOf(BuildingCatalog.HeavyMachineGunTurret));
            Assert.Throws<ArgumentException>(() => GrayboxBuildingCatalogPresenter3D.RouteOf(BuildingCatalog.SwordRidingPlatform));
            Assert.Throws<ArgumentException>(() => presenter.Describe(context, outside));
        }

        [Test]
        public void Quickbar_UsesTheExactFixedTenSlotOrder()
        {
            string[] expected =
            {
                BuildingCatalog.MiningStation.Id.Value,
                BuildingCatalog.Housing.Id.Value,
                BuildingCatalog.Warehouse.Id.Value,
                BuildingCatalog.Wall.Id.Value,
                BuildingCatalog.ResearchStation.Id.Value,
                BuildingCatalog.Smelter.Id.Value,
                BuildingCatalog.Assembler.Id.Value,
                BuildingCatalog.MachineGunTurret.Id.Value,
                BuildingCatalog.AutomatedRepairBay.Id.Value,
                BuildingCatalog.LaserTower.Id.Value
            };

            Assert.That(GrayboxBuildingCatalogPresenter3D.Quickbar.Select(value => value.Id.Value), Is.EqualTo(expected));
        }

        [Test]
        public void Catalog_StoresTheStableIdClassificationOnlyInThePresenter()
        {
            string buildingDirectory = Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building");
            string presenterPath = Path.Combine(
                buildingDirectory,
                "GrayboxBuildingCatalogPresenter3D.cs");
            string[] stableIds = Expectations
                .Select(value => value.Definition.Id.Value)
                .ToArray();

            foreach (string sourcePath in Directory.GetFiles(
                         buildingDirectory,
                         "*.cs",
                         SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(sourcePath, presenterPath, StringComparison.Ordinal))
                    continue;
                string source = File.ReadAllText(sourcePath);
                Assert.That(
                    stableIds.Any(source.Contains),
                    Is.False,
                    "Duplicate catalog ID table in " + Path.GetFileName(sourcePath));
            }
        }

        [Test]
        public void Query_SearchesOnlyVisibleContentWithCaseInsensitiveStableIdMatching()
        {
            var presenter = new GrayboxBuildingCatalogPresenter3D();
            var untouched = new CatalogContext();
            var contactedTechnology = new CatalogContext(contactedRoutes: new[] { ContentRoute.Technology });

            Assert.That(
                presenter.Query(untouched, null, null, "POWER-PLANT"),
                Is.Empty);
            Assert.That(
                presenter.Query(contactedTechnology, BuildingMenuCategory.Route, ContentRoute.Technology, "POWER-PLANT")
                    .Select(value => value.Definition),
                Is.EqualTo(new[] { BuildingCatalog.PowerPlant }));
        }

        [Test]
        public void Query_HidesUntouchedRoutesWithoutLeakingNamesOrLockReasons()
        {
            var presenter = new GrayboxBuildingCatalogPresenter3D();
            var untouched = new CatalogContext();

            GrayboxBuildingCatalogItem3D hidden = presenter.Describe(untouched, BuildingCatalog.PowerPlant);
            Assert.That(hidden.Visibility, Is.EqualTo(BuildingCatalogVisibility.Hidden));
            Assert.That(hidden.PrimaryLockReason, Is.Null);
            Assert.That(hidden.LockReasons, Is.Empty);
            Assert.That(presenter.Query(untouched, null, null, string.Empty)
                .Select(value => value.Definition.Id.Value), Does.Not.Contain(BuildingCatalog.PowerPlant.Id.Value));
        }

        [Test]
        public void Describe_ExposesContactedLockedCardsAndClearsReasonsWhenBuildable()
        {
            var presenter = new GrayboxBuildingCatalogPresenter3D();
            var lockedContext = new CatalogContext(contactedRoutes: new[] { ContentRoute.Technology });
            GrayboxBuildingCatalogItem3D locked = presenter.Describe(lockedContext, BuildingCatalog.PowerPlant);

            Assert.That(locked.Visibility, Is.EqualTo(BuildingCatalogVisibility.Locked));
            Assert.That(locked.PrimaryLockReason, Is.EqualTo(locked.LockReasons[0]));
            Assert.That(locked.LockReasons, Has.Count.EqualTo(2));

            var unlockedContext = new CatalogContext(
                population: 1000,
                allResearch: true,
                allPrerequisites: true,
                contactedRoutes: new[] { ContentRoute.Technology });
            GrayboxBuildingCatalogItem3D buildable = presenter.Describe(unlockedContext, BuildingCatalog.PowerPlant);
            Assert.That(buildable.Visibility, Is.EqualTo(BuildingCatalogVisibility.Buildable));
            Assert.That(buildable.PrimaryLockReason, Is.Null);
            Assert.That(buildable.LockReasons, Is.Empty);
        }

        [Test]
        public void Interaction_StartsInactiveAndCapturesCatalogOrigin()
        {
            GrayboxBuildingInteractionModel3D interaction = CreateInteraction();
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(interaction.Selected, Is.Null);
            Assert.That(interaction.Orientation, Is.EqualTo(BuildingOrientation.North));

            interaction.ToggleCatalog();
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            interaction.CloseCatalog();
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
        }

        [Test]
        public void Interaction_RetainsPreviewSelectionAndOrientationAcrossCatalogAndReplacesCards()
        {
            GrayboxBuildingInteractionModel3D interaction = CreateInteraction();
            interaction.Select(BuildingCatalog.MiningStation);
            interaction.RotateClockwise();
            interaction.ToggleCatalog();

            Assert.That(interaction.CatalogReturnState, Is.EqualTo(GrayboxBuildingInteractionState.Previewing));
            interaction.CloseCatalog();
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.Previewing));
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.MiningStation));
            Assert.That(interaction.Orientation, Is.EqualTo(BuildingOrientation.East));

            interaction.ToggleCatalog();
            interaction.Select(BuildingCatalog.Housing);
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.Previewing));
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Housing));
        }

        [Test]
        public void Interaction_ResolvesCancelConfirmationDeterministically()
        {
            GrayboxBuildingInteractionModel3D interaction = CreateInteraction();
            interaction.Select(BuildingCatalog.MiningStation);
            interaction.RequestCancelConstruction();
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.CancelConfirmation));

            interaction.ResolveCancelConfirmation(false);
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.Previewing));
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.MiningStation));

            interaction.RequestCancelConstruction();
            interaction.ResolveCancelConfirmation(true);
            Assert.That(interaction.State, Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(interaction.Selected, Is.Null);
        }

        private GrayboxBuildingInteractionModel3D CreateInteraction()
        {
            var gameObject = new GameObject("graybox-building-interaction-test");
            cleanup.Add(gameObject);
            return gameObject.AddComponent<GrayboxBuildingInteractionModel3D>();
        }

        private static ContentRoute[] AllRoutes()
        {
            return new[]
            {
                ContentRoute.Technology,
                ContentRoute.Cultivation,
                ContentRoute.BiologicalAscension,
                ContentRoute.Psionics
            };
        }

        private readonly struct CatalogExpectation
        {
            public CatalogExpectation(BuildingDefinition definition, BuildingMenuCategory category, ContentRoute route)
            {
                Definition = definition;
                Category = category;
                Route = route;
            }

            public BuildingDefinition Definition { get; }
            public BuildingMenuCategory Category { get; }
            public ContentRoute Route { get; }
        }

        private sealed class CatalogContext : IGrayboxBuildingCatalogContext3D
        {
            private readonly bool allResearch;
            private readonly bool allPrerequisites;
            private readonly HashSet<ContentRoute> contactedRoutes;

            public CatalogContext(
                int population = 0,
                bool allResearch = false,
                bool allPrerequisites = false,
                IEnumerable<ContentRoute> contactedRoutes = null)
            {
                Population = population;
                this.allResearch = allResearch;
                this.allPrerequisites = allPrerequisites;
                this.contactedRoutes = new HashSet<ContentRoute>(contactedRoutes ?? Array.Empty<ContentRoute>());
            }

            public int Population { get; }

            public bool IsResearchCompleted(string id)
            {
                return allResearch;
            }

            public int CompletedBuildingCount(string id)
            {
                return allPrerequisites ? 1 : 0;
            }

            public bool HasContactedRoute(ContentRoute route)
            {
                return contactedRoutes.Contains(route);
            }
        }
    }
}
