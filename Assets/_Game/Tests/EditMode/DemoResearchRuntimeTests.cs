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
        public void RootStartsCompletedFirstThreePlayableNodesCanStartAndPreviewsNeverSpend()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(500);
            city.Add(ResourceIds.Iron, 10);
            city.Add(ResourceIds.Alloy, 52);
            city.Add(ResourceIds.Biomass, 30);

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
            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(42));
            Assert.That(runtime.Tick(
                30f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);

            Assert.That(runtime.TryStart(
                AutomatedDefense,
                city,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(30));
            Assert.That(city.Get(ResourceIds.Biomass), Is.EqualTo(20));
            Assert.That(runtime.Tick(
                34f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.Tick(
                1f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(
                model.IsCompleted(new StableId(AutomatedDefense)),
                Is.True);

            int alloyBefore = city.Get(ResourceIds.Alloy);
            int biomassBefore = city.Get(ResourceIds.Biomass);
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

        [Test]
        public void ResearchSpendsAndRefundsThroughConnectedWarehouse()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var core = new ResourceInventory(500);
            using var city = new CityResourceStorageModel(core, 150);
            const string warehouseId = "building.instance.research-storage";
            Assert.That(city.TryRegisterWarehouse(
                warehouseId,
                connected: true), Is.True);
            Assert.That(city.AddToWarehouse(
                warehouseId,
                ResourceIds.Iron,
                10), Is.EqualTo(10));

            Assert.That(runtime.TryStart(
                BasicMetallurgy,
                city,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(city.GetNetworkAmount(ResourceIds.Iron), Is.Zero);
            Assert.That(runtime.TryCancel(city), Is.True);
            Assert.That(city.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(8));
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
            float expectedMultiplier = cityMode == CityMode.Fortress
                ? 1f
                : .5f;

            Assert.That(DemoResearchRuntime.SpeedMultiplier(cityMode),
                Is.EqualTo(expectedMultiplier));

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
        public void LastResearchStationEvacuationLockFreezesAndRollbackResumesWithoutRefund()
        {
            var root = new GameObject("research-evacuation-lock-test");
            try
            {
                GrayboxBuildingSession3D session =
                    CreateDevelopmentSession(root);
                GrayboxBuildingInstance3D station =
                    CompleteResearchStation(session, 10, 10);
                var runtime = new DemoResearchRuntime(session.Research);
                Assert.That(runtime.TryStart(
                    BasicMetallurgy,
                    session.Inventory,
                    hasEligibleResearchStation: true), Is.True);
                ResearchDefinition active = session.Research.Active;
                int ironAfterInvestment =
                    session.Inventory.Get(ResourceIds.Iron);

                Assert.That(runtime.Tick(
                    5f,
                    CityMode.Fortress,
                    globallyPaused: false,
                    hasEligibleResearchStation:
                        HasEligibleResearchStation(session)), Is.False);
                float remainingBeforeManifest = session.Research.Remaining;
                BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                    station.StableInstanceId,
                    station.Placement.Definition.Cost,
                    station.Progress.BaseDuration,
                    1d,
                    BuildingEvacuationTreatment.FullDismantle);

                Assert.That(station.IsEvacuationLocked, Is.False);
                Assert.That(runtime.Tick(
                    1f,
                    CityMode.Fortress,
                    globallyPaused: false,
                    hasEligibleResearchStation:
                        HasEligibleResearchStation(session)), Is.False);
                Assert.That(session.Research.Remaining,
                    Is.EqualTo(remainingBeforeManifest - 1f).Within(.0001f));
                Assert.That(session.TryCaptureEvacuationWork(
                    new[] { work }, out string captureFailure),
                    Is.True,
                    captureFailure);
                Assert.That(session.TryLockEvacuationWork(
                    new[] { work }, out string lockFailure),
                    Is.True,
                    lockFailure);
                float remainingAtLock = session.Research.Remaining;

                Assert.That(HasEligibleResearchStation(session), Is.False);
                Assert.That(runtime.Tick(
                    100f,
                    CityMode.Fortress,
                    globallyPaused: false,
                    hasEligibleResearchStation:
                        HasEligibleResearchStation(session)), Is.False);
                Assert.That(session.Research.Active, Is.SameAs(active));
                Assert.That(session.Research.Remaining,
                    Is.EqualTo(remainingAtLock).Within(.0001f));
                Assert.That(session.Inventory.Get(ResourceIds.Iron),
                    Is.EqualTo(ironAfterInvestment));

                session.RollbackEvacuationLocksAfterFailure(new[] { work });

                Assert.That(HasEligibleResearchStation(session), Is.True);
                Assert.That(runtime.Tick(
                    1f,
                    CityMode.Fortress,
                    globallyPaused: false,
                    hasEligibleResearchStation:
                        HasEligibleResearchStation(session)), Is.False);
                Assert.That(session.Research.Active, Is.SameAs(active));
                Assert.That(session.Research.Remaining,
                    Is.EqualTo(remainingAtLock - 1f).Within(.0001f));
                Assert.That(session.Inventory.Get(ResourceIds.Iron),
                    Is.EqualTo(ironAfterInvestment));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AnotherUnlockedResearchStationContinuesWhilePauseSignalStillFreezes()
        {
            var root = new GameObject("research-evacuation-eligibility-test");
            try
            {
                GrayboxBuildingSession3D session =
                    CreateDevelopmentSession(root);
                GrayboxBuildingInstance3D lockedStation =
                    CompleteResearchStation(session, 10, 10);
                CompleteResearchStation(session, 13, 10);
                var runtime = new DemoResearchRuntime(session.Research);
                Assert.That(runtime.TryStart(
                    BasicMetallurgy,
                    session.Inventory,
                    hasEligibleResearchStation: true), Is.True);
                BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                    lockedStation.StableInstanceId,
                    lockedStation.Placement.Definition.Cost,
                    lockedStation.Progress.BaseDuration,
                    1d,
                    BuildingEvacuationTreatment.FullDismantle);
                Assert.That(session.TryCaptureEvacuationWork(
                    new[] { work }, out string captureFailure),
                    Is.True,
                    captureFailure);
                Assert.That(session.TryLockEvacuationWork(
                    new[] { work }, out string lockFailure),
                    Is.True,
                    lockFailure);

                Assert.That(HasEligibleResearchStation(session), Is.True);
                Assert.That(runtime.Tick(
                    1f,
                    CityMode.Fortress,
                    globallyPaused: false,
                    hasEligibleResearchStation:
                        HasEligibleResearchStation(session)), Is.False);
                Assert.That(session.Research.Remaining,
                    Is.EqualTo(19f).Within(.0001f));

                Assert.That(runtime.Tick(
                    100f,
                    CityMode.Fortress,
                    globallyPaused: true,
                    hasEligibleResearchStation:
                        HasEligibleResearchStation(session)), Is.False);
                Assert.That(session.Research.Remaining,
                    Is.EqualTo(19f).Within(.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
            model.Restore(
                new[] { AmmunitionAssembly, AutomatedDefense },
                null,
                0f);
            runtime = new DemoResearchRuntime(model);
            city = new ResourceInventory(500);
            city.Add(ResourceIds.Alloy, 20);
            city.Add(ResourceIds.Biomass, 10);
            ResearchDefinition preview =
                DemoResearchCatalog.Find(ReinforcedStructures);
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
        public void FormalRestorePreservesSortedKnownAndUnknownCompletedIds()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            string unknown = "mod.example.research";

            Assert.That(runtime.TryPrepareRestoreForPersistence(
                new[]
                {
                    unknown,
                    DemoResearchCatalog.ScrapProcessingId,
                    DemoResearchCatalog.BasicMetallurgyId,
                },
                null,
                0f,
                out ResearchRestorePlan plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                plan, out string commitError), Is.True, commitError);

            Assert.That(runtime.IsCompleted(
                DemoResearchCatalog.BasicMetallurgyId), Is.True);
            Assert.That(runtime.IsCompleted(unknown), Is.False,
                "Unknown completed content must not grant runtime effects.");
            ResearchPersistenceSnapshot snapshot =
                runtime.CaptureForPersistence();
            Assert.That(snapshot.CompletedResearchIds, Is.EqualTo(new[]
            {
                DemoResearchCatalog.BasicMetallurgyId,
                DemoResearchCatalog.ScrapProcessingId,
                unknown,
            }));
            Assert.That(snapshot.ActiveResearchId, Is.Null);
            Assert.That(snapshot.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void FormalKnownActiveRestoreUsesDemoObjectAndNeverChargesAgain()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(100);
            city.Add(ResourceIds.Iron, 10);

            Assert.That(runtime.TryPrepareRestoreForPersistence(
                new[] { DemoResearchCatalog.ScrapProcessingId },
                DemoResearchCatalog.BasicMetallurgyId,
                7.25f,
                out ResearchRestorePlan plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                plan, out string commitError), Is.True, commitError);

            Assert.That(model.Active, Is.SameAs(DemoResearchCatalog.Find(
                DemoResearchCatalog.BasicMetallurgyId)));
            Assert.That(model.Remaining, Is.EqualTo(7.25f));
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(10),
                "Persistence restore must not call Start or charge costs.");
            Assert.That(runtime.Tick(
                7.25f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(runtime.IsCompleted(
                DemoResearchCatalog.BasicMetallurgyId), Is.True);
        }

        [Test]
        public void FormalUnknownActiveIsObservableFrozenAndRoundTripsExactly()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            var city = new ResourceInventory(100);
            city.Add(ResourceIds.Iron, 10);
            const string unknown = "mod.example.active-research";

            Assert.That(runtime.TryPrepareRestoreForPersistence(
                new[] { DemoResearchCatalog.ScrapProcessingId },
                unknown,
                13.75f,
                out ResearchRestorePlan plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                plan, out string commitError), Is.True, commitError);

            Assert.That(runtime.HasMissingActiveResearch, Is.True);
            Assert.That(runtime.MissingActiveResearchId, Is.EqualTo(unknown));
            Assert.That(runtime.MissingActiveRemainingSeconds,
                Is.EqualTo(13.75f));
            Assert.That(model.Active, Is.Null);
            Assert.That(runtime.TryStart(
                DemoResearchCatalog.BasicMetallurgyId,
                city,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.Tick(
                100f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(runtime.TryCancel(
                city,
                new ResourceCapacityPolicy(),
                activeWarehouseCount: 0), Is.False);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(runtime.MissingActiveRemainingSeconds,
                Is.EqualTo(13.75f));
            ResearchPersistenceSnapshot snapshot =
                runtime.CaptureForPersistence();
            Assert.That(snapshot.ActiveResearchId, Is.EqualTo(unknown));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(13.75f));
        }

        [Test]
        public void FormalPrepareRejectsInvalidTruthWithoutMutation()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            ResearchPersistenceSnapshot before =
                runtime.CaptureForPersistence();

            AssertInvalidFormalRestore(
                runtime,
                new[] { "bad id" },
                null,
                0f);
            AssertInvalidFormalRestore(
                runtime,
                new[]
                {
                    DemoResearchCatalog.ScrapProcessingId,
                    DemoResearchCatalog.ScrapProcessingId,
                },
                null,
                0f);
            AssertInvalidFormalRestore(
                runtime,
                new[] { DemoResearchCatalog.BasicMetallurgyId },
                DemoResearchCatalog.BasicMetallurgyId,
                1f);
            AssertInvalidFormalRestore(runtime, new string[0], null, 1f);
            AssertInvalidFormalRestore(
                runtime,
                new string[0],
                DemoResearchCatalog.BasicMetallurgyId,
                float.NaN);

            ResearchPersistenceSnapshot after =
                runtime.CaptureForPersistence();
            Assert.That(after.CompletedResearchIds,
                Is.EqualTo(before.CompletedResearchIds));
            Assert.That(after.ActiveResearchId,
                Is.EqualTo(before.ActiveResearchId));
            Assert.That(after.RemainingSeconds,
                Is.EqualTo(before.RemainingSeconds));
        }

        [Test]
        public void FormalRestorePlanRejectsStaleAndRepeatedCommit()
        {
            var model = new ResearchModel();
            var runtime = new DemoResearchRuntime(model);
            Assert.That(runtime.TryPrepareRestoreForPersistence(
                new[] { DemoResearchCatalog.ScrapProcessingId },
                null,
                0f,
                out ResearchRestorePlan stale,
                out string prepareError), Is.True, prepareError);

            model.GrantCompletedForDevelopment(
                DemoResearchCatalog.Find(
                    DemoResearchCatalog.BasicMetallurgyId));

            Assert.That(runtime.TryCommitRestoreForPersistence(
                stale, out string staleError), Is.False);
            Assert.That(staleError, Is.Not.Empty);
            Assert.That(runtime.IsCompleted(
                DemoResearchCatalog.BasicMetallurgyId), Is.True);

            Assert.That(runtime.TryPrepareRestoreForPersistence(
                new[] { DemoResearchCatalog.ScrapProcessingId },
                null,
                0f,
                out ResearchRestorePlan current,
                out prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                current, out string commitError), Is.True, commitError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                current, out string repeatedError), Is.False);
            Assert.That(repeatedError, Is.Not.Empty);
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

        private static void AssertInvalidFormalRestore(
            DemoResearchRuntime runtime,
            IReadOnlyList<string> completed,
            string active,
            float remaining)
        {
            Assert.That(runtime.TryPrepareRestoreForPersistence(
                completed,
                active,
                remaining,
                out ResearchRestorePlan plan,
                out string error), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        private static GrayboxBuildingSession3D CreateDevelopmentSession(
            GameObject root)
        {
            GrayboxBuildingSession3D session =
                root.AddComponent<GrayboxBuildingSession3D>();
            session.Configure(true);
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static GrayboxBuildingInstance3D CompleteResearchStation(
            GrayboxBuildingSession3D session,
            int x,
            int y)
        {
            var presentation = new PassiveBuildingPresentation();
            BuildingDefinition definition = BuildingCatalog.ResearchStation;
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
                12,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
                true,
                false,
                true,
                true,
                true,
                null,
                true,
                unlock,
                session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost));
            Assert.That(session.TryBeginConstruction(
                request,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(
                .1f,
                CityMode.Fortress,
                paused: false,
                presentation: presentation);
            Assert.That(instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            return instance;
        }

        private static bool HasEligibleResearchStation(
            GrayboxBuildingSession3D session)
        {
            return session.CompletedBuildingCount(
                BuildingCatalog.ResearchStation.Id.Value) > 0;
        }

        private sealed class PassiveBuildingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
            }
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
