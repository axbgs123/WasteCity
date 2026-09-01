using System;
using NUnit.Framework;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class FormalResearchRuntimeTests
    {
        private const string Root = "core.research.scrap-processing";
        private const string AutomatedMachinery =
            "core.research.automated-machinery";
        private const string SpiritSensing =
            "core.research.spirit-sensing";
        private const string AdaptiveTissue =
            "core.research.adaptive-tissue";
        private const string MindResonance =
            "core.research.mind-resonance";
        private const string ThoughtAcceleration =
            "core.research.thought-acceleration";
        private const string Ballistics = "core.research.ballistics";

        [Test]
        public void NewRuntimeUsesFortyFourNodeCatalogAndCompletesFormalRoot()
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);

            Assert.That(ResearchCatalog.All, Has.Length.EqualTo(44));
            Assert.That(runtime.Model, Is.SameAs(model));
            Assert.That(runtime.IsCompleted(Root), Is.True);
            Assert.That(model.CompletedCount, Is.EqualTo(1));
            Assert.That(model.Active, Is.Null);
        }

        [Test]
        public void FormalNodeOutsideSixNodeProfileStartsSpendsAndCompletes()
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.EnergyCrystal, 8);
            inventory.Add(ResourceIds.Iron, 4);

            Assert.That(runtime.TryStart(
                SpiritSensing,
                inventory,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(model.Active,
                Is.SameAs(ResearchCatalog.Find(SpiritSensing)));

            Assert.That(runtime.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(runtime.IsCompleted(SpiritSensing), Is.True);
        }

        [Test]
        public void ReleasedBallisticsStartsAndSpendsWithPrerequisiteAndCost()
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);
            var inventory = new ResourceInventory(100);
            model.GrantCompletedForDevelopment(
                ResearchCatalog.Find(AutomatedMachinery));
            inventory.Add(ResourceIds.Iron, 12);
            inventory.Add(ResourceIds.Alloy, 10);

            Assert.That(
                ResearchCatalog.Find(Ballistics).ReleaseState,
                Is.EqualTo(ResearchReleaseState.Researchable));
            Assert.That(runtime.TryStart(
                Ballistics,
                inventory,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(model.Active.Id.Value, Is.EqualTo(Ballistics));
        }

        [TestCase("core.research.alloy-armor", "core.research.precision-assembly")]
        [TestCase("core.research.sword-riding", "core.research.sword-array")]
        public void IDEA0021_CivilizationLevelTwoUnlocksApprovedResearchOnly(
            string researchId,
            string prerequisiteId)
        {
            var level = 1;
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(
                model, null, () => level);
            model.GrantCompletedForDevelopment(
                ResearchCatalog.Find(prerequisiteId));
            ResearchDefinition definition = ResearchCatalog.Find(researchId);
            var inventory = new ResourceInventory(100);
            foreach (ResourceAmount cost in definition.Costs)
                inventory.Add(cost.ResourceId, cost.Amount);

            Assert.That(runtime.IsAvailable(researchId), Is.False);
            Assert.That(runtime.TryStart(
                researchId, inventory, true), Is.False);
            foreach (ResourceAmount cost in definition.Costs)
                Assert.That(inventory.Get(cost.ResourceId),
                    Is.EqualTo(cost.Amount));

            level = 2;
            Assert.That(runtime.IsAvailable(researchId), Is.True);
            Assert.That(runtime.TryStart(
                researchId, inventory, true), Is.True);
            foreach (ResourceAmount cost in definition.Costs)
                Assert.That(inventory.Get(cost.ResourceId), Is.Zero);
            Assert.That(runtime.Tick(
                60f, CityMode.Fortress, false, true), Is.True);
            Assert.That(runtime.IsCompleted(researchId), Is.True);
        }

        [Test]
        public void IDEA0021_PersistenceResolverPreparesLevelTwoActiveBeforeOwnerRestore()
        {
            var sourceLevel = 2;
            var sourceModel = new ResearchModel();
            var source = new FormalResearchRuntime(
                sourceModel, null, () => sourceLevel);
            sourceModel.GrantCompletedForDevelopment(ResearchCatalog.Find(
                "core.research.precision-assembly"));
            ResearchDefinition alloy = ResearchCatalog.Find(
                "core.research.alloy-armor");
            var inventory = new ResourceInventory(100);
            foreach (ResourceAmount cost in alloy.Costs)
                inventory.Add(cost.ResourceId, cost.Amount);
            Assert.That(source.TryStart(alloy.Id.Value, inventory, true),
                Is.True);
            ResearchPersistenceSnapshot saved = source.CaptureForPersistence();

            var targetLevel = 1;
            var target = new FormalResearchRuntime(
                new ResearchModel(), null, () => targetLevel);
            Assert.That(target.TryPrepareRestoreForPersistence(
                saved.CompletedResearchIds,
                saved.ActiveResearchId,
                saved.RemainingSeconds,
                out ResearchRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(target.TryCommitRestoreForPersistence(plan, out error),
                Is.True, error);
            Assert.That(target.Model.Active.Id.Value,
                Is.EqualTo(alloy.Id.Value));
        }

        [Test]
        public void ResearchableNodeWithTwoPrerequisitesRequiresBothBeforeSpending()
        {
            const string dualRequirementId =
                "test.research.formal-dual-requirement";
            var dualRequirement = new ResearchDefinition(
                dualRequirementId,
                "双路线验证",
                DevelopmentRoute.Bridge,
                ResourceIds.Alloy,
                5,
                30f,
                Root,
                3,
                "验证全部前置均由正式运行时执行",
                AutomatedMachinery);
            ResearchDefinition Resolve(string id) =>
                string.Equals(id, dualRequirementId,
                    StringComparison.Ordinal)
                    ? dualRequirement
                    : ResearchCatalog.Find(id);
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model, Resolve);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Alloy, 10);

            Assert.That(runtime.TryStart(
                dualRequirementId,
                inventory,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(10));

            model.GrantCompletedForDevelopment(
                ResearchCatalog.Find(AutomatedMachinery));
            Assert.That(runtime.TryStart(
                dualRequirementId,
                inventory,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(5));
            Assert.That(model.Active, Is.SameAs(dualRequirement));
        }

        [TestCase(CityMode.Fortress, false, 1f, 12f)]
        [TestCase(CityMode.Mobile, false, .5f, 16f)]
        [TestCase(CityMode.Deploying, false, .5f, 16f)]
        [TestCase(CityMode.Packing, false, .5f, 16f)]
        [TestCase(CityMode.Fortress, true, 1.25f, 10f)]
        [TestCase(CityMode.Mobile, true, .625f, 15f)]
        public void CityModeAndThoughtAccelerationComposeIntoTickRate(
            CityMode cityMode,
            bool thoughtAccelerationCompleted,
            float expectedMultiplier,
            float expectedRemaining)
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Iron, 10);
            if (thoughtAccelerationCompleted)
            {
                model.GrantCompletedForDevelopment(
                    ResearchCatalog.Find(ThoughtAcceleration));
            }

            Assert.That(FormalResearchRuntime.SpeedMultiplier(
                cityMode,
                thoughtAccelerationCompleted),
                Is.EqualTo(expectedMultiplier));
            Assert.That(runtime.TryStart(
                AutomatedMachinery,
                inventory,
                hasEligibleResearchStation: true), Is.True);

            Assert.That(runtime.Tick(
                8f,
                cityMode,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(model.Remaining,
                Is.EqualTo(expectedRemaining).Within(.0001f));
        }

        [Test]
        public void GlobalPauseOrMissingResearchStationFreezesCurrentWork()
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Iron, 10);
            Assert.That(runtime.TryStart(
                AutomatedMachinery,
                inventory,
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
            Assert.That(inventory.Get(ResourceIds.Iron), Is.Zero);
        }

        [Test]
        public void CancelRefundsEightyPercentOfEveryFormalCost()
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.EnergyCrystal, 8);
            inventory.Add(ResourceIds.Iron, 4);
            Assert.That(runtime.TryStart(
                SpiritSensing,
                inventory,
                hasEligibleResearchStation: true), Is.True);

            Assert.That(runtime.TryCancel(
                inventory,
                new ResourceCapacityPolicy(),
                activeWarehouseCount: 0), Is.True);

            Assert.That(inventory.Get(ResourceIds.EnergyCrystal),
                Is.EqualTo(6));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(3));
            Assert.That(model.Active, Is.Null);
            Assert.That(model.Remaining, Is.Zero);
        }

        [Test]
        public void SchemaThirtyOneRestoreUsesFormalResolverForCompletedAndActive()
        {
            var schemaThirtyOne = new FormalThreeDResearchSaveData
            {
                completedResearchIds = new[] { Root, SpiritSensing },
                activeResearchId = AdaptiveTissue,
                remainingSeconds = 9.5f,
            };
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);

            Assert.That(runtime.TryPrepareRestoreForPersistence(
                schemaThirtyOne.completedResearchIds,
                schemaThirtyOne.activeResearchId,
                schemaThirtyOne.remainingSeconds,
                out ResearchRestorePlan plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                plan,
                out string commitError), Is.True, commitError);

            Assert.That(runtime.IsCompleted(SpiritSensing), Is.True);
            Assert.That(model.Active,
                Is.SameAs(ResearchCatalog.Find(AdaptiveTissue)));
            Assert.That(model.Remaining, Is.EqualTo(9.5f));
            Assert.That(runtime.Tick(
                9.5f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(runtime.IsCompleted(AdaptiveTissue), Is.True);
        }

        [Test]
        public void SchemaThirtyOneRestoreRepairsMissingInitiallyCompletedRoot()
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);

            Assert.That(runtime.TryPrepareRestoreForPersistence(
                Array.Empty<string>(),
                activeResearchId: null,
                remainingSeconds: 0f,
                out ResearchRestorePlan plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                plan,
                out string commitError), Is.True, commitError);

            Assert.That(runtime.IsCompleted(Root), Is.True,
                "schema 31 derives the initially completed root after restore");
            CollectionAssert.Contains(
                runtime.CaptureForPersistence().CompletedResearchIds,
                Root);
        }

        [Test]
        public void SchemaThirtyOneUnknownActiveStaysFrozenAndRoundTrips()
        {
            const string unknownCompleted =
                "mod.example.completed-research";
            const string unknownActive = "mod.example.active-research";
            var schemaThirtyOne = new FormalThreeDResearchSaveData
            {
                completedResearchIds = new[] { Root, unknownCompleted },
                activeResearchId = unknownActive,
                remainingSeconds = 13.75f,
            };
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model);

            Assert.That(runtime.TryPrepareRestoreForPersistence(
                schemaThirtyOne.completedResearchIds,
                schemaThirtyOne.activeResearchId,
                schemaThirtyOne.remainingSeconds,
                out ResearchRestorePlan plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestoreForPersistence(
                plan,
                out string commitError), Is.True, commitError);

            Assert.That(runtime.HasMissingActiveResearch, Is.True);
            Assert.That(runtime.MissingActiveResearchId,
                Is.EqualTo(unknownActive));
            Assert.That(runtime.MissingActiveRemainingSeconds,
                Is.EqualTo(13.75f));
            Assert.That(model.Active, Is.Null);
            Assert.That(runtime.Tick(
                100f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.False);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.EnergyCrystal, 8);
            inventory.Add(ResourceIds.Water, 6);
            Assert.That(runtime.TryStart(
                MindResonance,
                inventory,
                hasEligibleResearchStation: true), Is.False);
            Assert.That(inventory.Get(ResourceIds.EnergyCrystal),
                Is.EqualTo(8));
            Assert.That(inventory.Get(ResourceIds.Water), Is.EqualTo(6));
            Assert.That(runtime.TryCancel(
                inventory,
                new ResourceCapacityPolicy(),
                activeWarehouseCount: 0), Is.False);

            ResearchPersistenceSnapshot recaptured =
                runtime.CaptureForPersistence();
            CollectionAssert.Contains(
                recaptured.CompletedResearchIds,
                unknownCompleted);
            Assert.That(recaptured.ActiveResearchId,
                Is.EqualTo(unknownActive));
            Assert.That(recaptured.RemainingSeconds, Is.EqualTo(13.75f));
        }
    }
}
