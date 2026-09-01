using System;
using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class IDEA0027ResearchCatalogAndStatusTests
    {
        private static readonly string[] FormerPreviewResearchIds =
        {
            "core.research.ballistics",
            "core.research.talisman-basics",
            "core.research.spore-dispersal",
            "core.research.mind-spire",
            "core.research.alloy-armor",
            "core.research.unmanned-systems",
            "core.research.energy-weapons",
            "core.research.sword-riding",
            "core.research.puppetry",
            "core.research.behemoth-breeding",
            "core.research.acid-spit",
            "core.research.gene-splicing",
            "core.research.mind-shield",
            "core.research.mind-control",
            "core.research.collective-consciousness",
        };

        [Test]
        public void IDEA0027_AllFormerPreviewNodesAreReleasedWithoutChangingCatalogShape()
        {
            Assert.That(ResearchCatalog.All, Has.Length.EqualTo(44));
            Assert.That(
                ResearchCatalog.All.Sum(value =>
                    value.RequiredResearchIds.Count),
                Is.EqualTo(49));

            foreach (string id in FormerPreviewResearchIds)
            {
                Assert.That(
                    ResearchCatalog.Find(id).ReleaseState,
                    Is.EqualTo(ResearchReleaseState.Researchable),
                    id);
            }

            Assert.That(
                ResearchCatalog.All.Count(value =>
                    value.ReleaseState == ResearchReleaseState.PreviewOnly),
                Is.Zero);
        }

        [TestCase("core.research.alloy-armor")]
        [TestCase("core.research.sword-riding")]
        public void IDEA0027_CivilizationLevelTwoGatesRemainAuthoritative(
            string researchId)
        {
            ResearchDefinition source = ResearchCatalog.Find(researchId);

            Assert.That(
                CivilizationResearchAvailability.Resolve(source, 1)
                    .ReleaseState,
                Is.EqualTo(ResearchReleaseState.PreviewOnly));
            Assert.That(
                CivilizationResearchAvailability.Resolve(source, 2)
                    .ReleaseState,
                Is.EqualTo(ResearchReleaseState.Researchable));
        }

        [Test]
        public void IDEA0027_ReleasedResearchEffectsAreActiveAndExecutable()
        {
            foreach (string researchId in FormerPreviewResearchIds)
            {
                ResearchEffectDefinition[] effects =
                    ResearchEffectCatalog.ForResearch(researchId).ToArray();
                Assert.That(effects, Is.Not.Empty, researchId);
                Assert.That(
                    effects.All(value =>
                        value.Activation == ResearchEffectActivation.Active &&
                        value.IsExecutable),
                    Is.True,
                    researchId);
            }
        }

        [Test]
        public void IDEA0027_ResolvedEffectsExposeStableConsumerQueries()
        {
            ResearchEffectSnapshot effects = ResearchEffectResolver.Resolve(
                FormerPreviewResearchIds);

            Assert.That(
                effects.ResolvePhysicalDamageTakenMultiplier(
                    "core.building.wall"),
                Is.EqualTo(.8f));
            Assert.That(
                effects.ResolveUnitCapacity(
                    "cultivation.building.puppet-workshop", 3),
                Is.EqualTo(4));
            Assert.That(
                effects.ResolveUnitHealthMultiplier(
                    "biological.unit.bred-behemoth"),
                Is.EqualTo(1.1f));
            Assert.That(
                effects.ResolveUnitHealthMultiplier("unit.bred-behemoth"),
                Is.EqualTo(1f),
                "The retired shorthand must not become a second unit ID.");
            Assert.That(
                effects.ResolveHeavyArmorDamageMultiplier(
                    "biological.building.acid-tower"),
                Is.EqualTo(1.3f));
            Assert.That(
                effects.CollectiveConsciousnessInitialProgressFraction,
                Is.EqualTo(.2f));
            Assert.That(effects.HasActiveRule("energy.overload"), Is.True);
            Assert.That(effects.HasActiveRule("unknown.rule"), Is.False);
        }

        [Test]
        public void IDEA0027_StatusCatalogHasStableUniqueAndResolvableDefinitions()
        {
            Assert.That(ResearchStatusCatalog.All, Has.Count.EqualTo(11));
            Assert.That(
                ResearchStatusCatalog.All.Select(value => value.Id)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(ResearchStatusCatalog.All.Count));

            foreach (ResearchStatusDefinition definition in
                     ResearchStatusCatalog.All)
            {
                Assert.That(definition.Id, Is.Not.Empty);
                Assert.That(definition.DisplayName, Is.Not.Empty);
                Assert.That(ResearchCatalog.Find(definition.SourceResearchId),
                    Is.Not.Null, definition.Id);
                Assert.That(definition.AllowedTargets,
                    Is.Not.EqualTo(ResearchStatusTarget.None));
                Assert.That(definition.DurationSeconds,
                    Is.GreaterThanOrEqualTo(0f));
                Assert.That(definition.PeriodSeconds,
                    Is.GreaterThanOrEqualTo(0f));
                Assert.That(definition.MaximumStacks,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(
                    ResearchStatusCatalog.Find(definition.Id),
                    Is.SameAs(definition));
            }

            Assert.That(ResearchStatusCatalog.Find(null), Is.Null);
            Assert.That(ResearchStatusCatalog.Find("unknown.status"), Is.Null);
        }

        [Test]
        public void IDEA0027_StatusCatalogMatchesApprovedDurationsAndBoundaries()
        {
            AssertStatus(ResearchStatusCatalog.TechnologyOverloadId,
                "core.research.energy-weapons", ResearchStatusTarget.Tower,
                ResearchStatusApplication.Singleton, 30f, 0f, 1, 0f);
            AssertStatus(ResearchStatusCatalog.AutomatedRepairId,
                "core.research.unmanned-systems",
                ResearchStatusTarget.Building | ResearchStatusTarget.CityCore,
                ResearchStatusApplication.Singleton, 0f, 5.1f, 1, 20f);
            AssertStatus(ResearchStatusCatalog.SwordIntentId,
                "core.research.sword-array", ResearchStatusTarget.Enemy,
                ResearchStatusApplication.StackAndRefresh, 0f, 1f, 20, 0f);
            AssertStatus(ResearchStatusCatalog.PuppetMaintenanceId,
                "core.research.puppetry", ResearchStatusTarget.ArmyUnit,
                ResearchStatusApplication.Singleton, 0f, 60f, 1, 0f);
            AssertStatus(ResearchStatusCatalog.InfectionId,
                "core.research.spore-dispersal", ResearchStatusTarget.Enemy,
                ResearchStatusApplication.StackAndRefresh, 0f, 1f, 10, 0f);
            AssertStatus(ResearchStatusCatalog.CarapaceRegenerationId,
                "core.research.carapace-growth", ResearchStatusTarget.Building,
                ResearchStatusApplication.Singleton, 0f, 5f, 1, 10f);
            AssertStatus(ResearchStatusCatalog.TissueRegenerationId,
                "core.research.tissue-regeneration",
                ResearchStatusTarget.Building | ResearchStatusTarget.ArmyUnit,
                ResearchStatusApplication.Singleton, 0f, 1f, 1, 1f);
            AssertStatus(ResearchStatusCatalog.GeneSplicingTraitId,
                "core.research.gene-splicing", ResearchStatusTarget.Leader,
                ResearchStatusApplication.Singleton, 300f, 0f, 1, 1.2f);
            AssertStatus(ResearchStatusCatalog.PsionicResonanceId,
                "core.research.mind-spire", ResearchStatusTarget.Enemy,
                ResearchStatusApplication.Refresh, 5f, 0f, 1, 0f);
            AssertStatus(ResearchStatusCatalog.CityShieldId,
                "core.research.mind-shield",
                ResearchStatusTarget.Building | ResearchStatusTarget.CityCore,
                ResearchStatusApplication.Recharge, 0f, 8f, 1, 100f);
            AssertStatus(ResearchStatusCatalog.MindControlId,
                "core.research.mind-control", ResearchStatusTarget.Enemy,
                ResearchStatusApplication.Singleton, 0f, 0f, 1, 0f);
        }

        [Test]
        public void IDEA0027_ResearchStartKeepsLegacyDefaultAndAppliesInheritedProgressOnce()
        {
            var legacyInventory = new ResourceInventory(100);
            legacyInventory.Add(ResourceIds.Iron, 20);
            var inheritedInventory = new ResourceInventory(100);
            inheritedInventory.Add(ResourceIds.Iron, 20);
            ResearchDefinition definition = Definition();

            var legacy = new ResearchModel();
            var inherited = new ResearchModel();
            Assert.That(legacy.Start(definition, legacyInventory), Is.True);
            Assert.That(inherited.Start(definition, inheritedInventory, .2f),
                Is.True);

            Assert.That(legacy.Remaining, Is.EqualTo(60f));
            Assert.That(inherited.Remaining, Is.EqualTo(48f));
            Assert.That(inherited.CaptureForPersistence().RemainingSeconds,
                Is.EqualTo(48f));
        }

        [Test]
        public void IDEA0027_CityStorageResearchStartSupportsInheritedProgress()
        {
            var inventory = new ResourceInventory(150);
            inventory.Add(ResourceIds.Iron, 20);
            using var storage = new CityResourceStorageModel(inventory, 150);
            var model = new ResearchModel();

            Assert.That(model.Start(Definition(), storage, .2f), Is.True);
            Assert.That(model.Remaining, Is.EqualTo(48f));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(10));
        }

        private static ResearchDefinition Definition() =>
            new ResearchDefinition(
                "test.research.idea-0027-inheritance",
                "集体意识继承测试",
                DevelopmentRoute.Psionics,
                ResourceIds.Iron,
                10,
                60f);

        private static void AssertStatus(
            string id,
            string researchId,
            ResearchStatusTarget target,
            ResearchStatusApplication application,
            float duration,
            float period,
            int stacks,
            float maximumValue)
        {
            ResearchStatusDefinition definition = ResearchStatusCatalog.Find(id);
            Assert.That(definition, Is.Not.Null, id);
            Assert.That(definition.SourceResearchId, Is.EqualTo(researchId), id);
            Assert.That(definition.AllowedTargets, Is.EqualTo(target), id);
            Assert.That(definition.Application, Is.EqualTo(application), id);
            Assert.That(definition.DurationSeconds, Is.EqualTo(duration), id);
            Assert.That(definition.PeriodSeconds, Is.EqualTo(period), id);
            Assert.That(definition.MaximumStacks, Is.EqualTo(stacks), id);
            Assert.That(definition.MaximumValue, Is.EqualTo(maximumValue), id);
        }
    }
}
