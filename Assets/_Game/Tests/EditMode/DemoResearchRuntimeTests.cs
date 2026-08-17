using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class DemoResearchRuntimeTests
    {
        private const string ScrapProcessing =
            "core.research.scrap-processing";
        private const string BasicMetallurgy =
            "core.research.automated-machinery";
        private const string AmmunitionAssembly =
            "core.research.precision-assembly";
        private const string AutomatedDefense =
            "core.research.automated-defense";
        private const string ReinforcedStructures =
            "core.research.reinforced-structures";
        private const string LegacyAnalysis =
            "core.research.legacy-analysis";

        [Test]
        public void DemoCatalogContainsTheExactSixNodeProfileWithoutChangingLongCatalog()
        {
            AssertDefinition(
                DemoResearchCatalog.All[0],
                ScrapProcessing,
                "废料加工",
                required: null,
                duration: 0f);
            AssertDefinition(
                DemoResearchCatalog.All[1],
                BasicMetallurgy,
                "基础冶金",
                ScrapProcessing,
                20f,
                new ResourceAmount(ResourceIds.Iron, 10));
            AssertDefinition(
                DemoResearchCatalog.All[2],
                AmmunitionAssembly,
                "弹药装配",
                BasicMetallurgy,
                30f,
                new ResourceAmount(ResourceIds.Alloy, 10));
            AssertDefinition(
                DemoResearchCatalog.All[3],
                AutomatedDefense,
                "自动防御",
                AmmunitionAssembly,
                35f,
                new ResourceAmount(ResourceIds.Alloy, 12),
                new ResourceAmount(ResourceIds.Biomass, 10));
            AssertDefinition(
                DemoResearchCatalog.All[4],
                ReinforcedStructures,
                "加固结构",
                AutomatedDefense,
                45f,
                new ResourceAmount(ResourceIds.Alloy, 20),
                new ResourceAmount(ResourceIds.Biomass, 10));
            AssertDefinition(
                DemoResearchCatalog.All[5],
                LegacyAnalysis,
                "遗产解析",
                AutomatedDefense,
                60f,
                new ResourceAmount(ResourceIds.Alloy, 30),
                new ResourceAmount(ResourceIds.Biomass, 20));

            Assert.That(DemoResearchCatalog.All, Has.Count.EqualTo(6));
            Assert.That(
                DemoResearchCatalog.All.Select(value => value.Id.Value),
                Is.EqualTo(new[]
                {
                    ScrapProcessing,
                    BasicMetallurgy,
                    AmmunitionAssembly,
                    AutomatedDefense,
                    ReinforcedStructures,
                    LegacyAnalysis,
                }));
            Assert.That(ResearchCatalog.All, Has.Length.EqualTo(43));
            Assert.That(
                ResearchCatalog.All.Select(value => value.Id.Value)
                    .Distinct()
                    .ToArray(),
                Has.Length.EqualTo(43));
            ResearchDefinition longAssembly =
                ResearchCatalog.Find(AmmunitionAssembly);
            Assert.That(longAssembly.Name, Is.EqualTo("精密装配"));
            Assert.That(longAssembly.CostId, Is.EqualTo(ResourceIds.Alloy));
            Assert.That(longAssembly.Cost, Is.EqualTo(20));
            Assert.That(longAssembly.Duration, Is.EqualTo(40f));
            AssertCosts(
                longAssembly.Costs,
                new ResourceAmount(ResourceIds.Alloy, 20));
        }

        [Test]
        public void RootStartsCompletedOnlyTwoPlayableNodesCanStartAndPreviewsNeverSpend()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            city.Add(ResourceIds.Iron, 10);
            city.Add(ResourceIds.Alloy, 22);
            city.Add(ResourceIds.Biomass, 9);

            Assert.That(
                model.IsCompleted(new StableId(ScrapProcessing)),
                Is.True);
            Assert.That(
                runtime.TryStart(
                    ScrapProcessing,
                    city,
                    hasEligibleResearchStation: true),
                Is.False);
            Assert.That(
                runtime.TryStart(
                    BasicMetallurgy,
                    city,
                    hasEligibleResearchStation: false),
                Is.False);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(
                runtime.TryStart(
                    BasicMetallurgy,
                    city,
                    hasEligibleResearchStation: true),
                Is.True);
            Assert.That(city.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(runtime.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);

            Assert.That(runtime.TryStart(
                AmmunitionAssembly,
                city,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(12));
            Assert.That(runtime.Tick(
                30f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);

            int alloyBefore = city.Get(ResourceIds.Alloy);
            int biomassBefore = city.Get(ResourceIds.Biomass);
            Assert.That(runtime.TryStart(
                AutomatedDefense,
                city,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.TryStart(
                ReinforcedStructures,
                city,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.TryStart(
                LegacyAnalysis,
                city,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(alloyBefore));
            Assert.That(city.Get(ResourceIds.Biomass), Is.EqualTo(biomassBefore));
            Assert.That(model.Active, Is.Null);
        }

        [Test]
        public void StartIsAtomicAndOnlyOneResearchCanBeActive()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            city.Add(ResourceIds.Iron, 20);
            city.Add(ResourceIds.Alloy, 10);

            Assert.That(runtime.TryStart(
                BasicMetallurgy,
                city,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(runtime.TryStart(
                BasicMetallurgy,
                city,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.TryStart(
                AmmunitionAssembly,
                city,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(10));
            Assert.That(model.Active.Id.Value, Is.EqualTo(BasicMetallurgy));
        }

        [TestCase(CityMode.Fortress, 10f)]
        [TestCase(CityMode.Mobile, 15f)]
        [TestCase(CityMode.Deploying, 15f)]
        [TestCase(CityMode.Packing, 15f)]
        public void CityModeControlsTheApprovedResearchRate(
            CityMode cityMode,
            float expectedRemaining)
        {
            DemoResearchRuntime runtime = StartBasic(out ResearchModel model);

            Assert.That(runtime.Tick(
                10f,
                cityMode,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);

            Assert.That(model.Remaining,
                Is.EqualTo(expectedRemaining).Within(.0001f));
        }

        [Test]
        public void PauseOrMissingResearchStationFreezesWithoutCancellingOrRefunding()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            city.Add(ResourceIds.Iron, 10);
            Assert.That(runtime.TryStart(
                BasicMetallurgy,
                city,
                hasEligibleResearchStation: true), Is.True);
            ResearchDefinition active = model.Active;

            Assert.That(runtime.Tick(
                100f,
                CityMode.Fortress,
                globallyPaused: true,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.Tick(
                100f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: false), Is.False);

            Assert.That(model.Active, Is.SameAs(active));
            Assert.That(model.Remaining, Is.EqualTo(20f));
            Assert.That(city.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(runtime.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
        }

        [Test]
        public void CancelRefundsEightyPercentAndFailsAtomicallyWhenCapacityIsInsufficient()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            var capacity = new ResourceCapacityPolicy();
            city.Add(ResourceIds.Iron, 10);
            Assert.That(runtime.TryStart(
                BasicMetallurgy,
                city,
                hasEligibleResearchStation: true), Is.True);
            runtime.Tick(
                5f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true);
            city.Add(ResourceIds.Iron, 149);

            Assert.That(runtime.TryCancel(
                city,
                capacity,
                activeWarehouseCount: 0), Is.False);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(149));
            Assert.That(model.Active.Id.Value, Is.EqualTo(BasicMetallurgy));
            Assert.That(model.Remaining, Is.EqualTo(15f));

            Assert.That(city.TrySpend(ResourceIds.Iron, 7), Is.True);
            Assert.That(runtime.TryCancel(
                city,
                capacity,
                activeWarehouseCount: 0), Is.True);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(model.Active, Is.Null);
            Assert.That(model.Remaining, Is.Zero);

            var assemblyModel = new ResearchModel();
            var assemblyRuntime = new DemoResearchRuntime(assemblyModel);
            var assemblyCity = new ResourceInventory(500);
            assemblyCity.Add(ResourceIds.Iron, 10);
            assemblyCity.Add(ResourceIds.Alloy, 10);
            Assert.That(assemblyRuntime.TryStart(
                BasicMetallurgy,
                assemblyCity,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(assemblyRuntime.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(assemblyRuntime.TryStart(
                AmmunitionAssembly,
                assemblyCity,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(assemblyRuntime.TryCancel(
                assemblyCity,
                capacity,
                activeWarehouseCount: 0), Is.True);
            Assert.That(assemblyCity.Get(ResourceIds.Alloy), Is.EqualTo(8));
        }

        [Test]
        public void CompletionUsesTheProvidedResearchModelAndUnlocksExistingBuildingRules()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            city.Add(ResourceIds.Iron, 10);

            Assert.That(runtime.Model, Is.SameAs(model));
            Assert.That(runtime.TryStart(
                BasicMetallurgy,
                city,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(runtime.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);

            Assert.That(
                model.IsCompleted(new StableId(BasicMetallurgy)),
                Is.True);
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                BuildingCatalog.Smelter,
                population: 200,
                researchCompleted: id =>
                    model.IsCompleted(new StableId(id)),
                completedBuildings: _ => 0);
            Assert.That(unlock.IsUnlocked, Is.True);
        }

        [Test]
        public void RuntimeNeverAdvancesOrCancelsResearchOutsideItsPlayableProfile()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            var capacity = new ResourceCapacityPolicy();
            city.Add(ResourceIds.Water, 10);
            ResearchDefinition longCatalogResearch =
                ResearchCatalog.Find("core.research.mind-resonance");
            Assert.That(model.Start(longCatalogResearch, city), Is.True);
            float remaining = model.Remaining;

            Assert.That(runtime.Tick(
                100f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.TryCancel(city, capacity, 0), Is.False);
            Assert.That(model.Active, Is.SameAs(longCatalogResearch));
            Assert.That(model.Remaining, Is.EqualTo(remaining));
            Assert.That(city.Get(ResourceIds.Water), Is.Zero);

            model = new ResearchModel();
            model.Restore(new[] { AmmunitionAssembly }, null, 0f);
            runtime = new DemoResearchRuntime(model);
            city = new ResourceInventory(500);
            city.Add(ResourceIds.Alloy, 12);
            city.Add(ResourceIds.Biomass, 10);
            ResearchDefinition preview =
                DemoResearchCatalog.Find(AutomatedDefense);
            Assert.That(model.Start(preview, city), Is.True);
            remaining = model.Remaining;

            Assert.That(runtime.Tick(
                100f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.TryCancel(city, capacity, 0), Is.False);
            Assert.That(model.Active, Is.SameAs(preview));
            Assert.That(model.Remaining, Is.EqualTo(remaining));
            Assert.That(city.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(city.Get(ResourceIds.Biomass), Is.Zero);
        }

        [Test]
        public void DevelopmentUnlockPreservesDemoRootOnTheSharedSessionModel()
        {
            var root = new GameObject("Research Session Test");
            try
            {
                GrayboxBuildingSession3D session =
                    root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureDevelopmentFixture();
                var runtime = new DemoResearchRuntime(session.Research);

                session.UnlockResearchForDevelopment(
                    "core.research.spirit-sensing");

                Assert.That(runtime.IsCompleted(ScrapProcessing), Is.True);
                Assert.That(
                    session.Research.IsCompleted(
                        ResearchCatalog.Find(
                            "core.research.spirit-sensing").Id),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static DemoResearchRuntime StartBasic(
            out ResearchModel model)
        {
            model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            city.Add(ResourceIds.Iron, 10);
            Assert.That(runtime.TryStart(
                BasicMetallurgy,
                city,
                hasEligibleResearchStation: true), Is.True);
            return runtime;
        }

        private static void AssertDefinition(
            ResearchDefinition definition,
            string id,
            string name,
            string required,
            float duration,
            params ResourceAmount[] costs)
        {
            Assert.That(definition.Id.Value, Is.EqualTo(id));
            Assert.That(definition.Name, Is.EqualTo(name));
            Assert.That(definition.RequiredResearchIds,
                Is.EqualTo(string.IsNullOrEmpty(required)
                    ? new string[0]
                    : new[] { required }));
            Assert.That(definition.Duration, Is.EqualTo(duration));
            AssertCosts(definition.Costs, costs);
        }

        private static void AssertCosts(
            IReadOnlyList<ResourceAmount> actual,
            params ResourceAmount[] expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].ResourceId,
                    Is.EqualTo(expected[index].ResourceId));
                Assert.That(actual[index].Amount,
                    Is.EqualTo(expected[index].Amount));
            }
        }
    }
}
