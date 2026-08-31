using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class ResearchRuntimeEffectIntegrationTests
    {
        private const BindingFlags InstanceAny =
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic;

        [Test]
        public void IDEA0026_FormalCampaignAppliesPrecognitiveWarningToNewWaves()
        {
            ResearchEffectSnapshot effects = ResearchEffectResolver.Resolve(
                new[] { "core.research.precognitive-sense" });
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);

            campaign.SetWarningMultiplier(
                effects.WarningDurationMultiplier);
            Assert.That(
                campaign.NotifyDefenseTowerCompleted(
                    "building.instance.precognitive-tower",
                    BuildingCatalog.MachineGunTurret.Id.Value,
                    isCompleted: true,
                    isPlayerOwned: true),
                Is.True);

            Assert.That(
                campaign.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(22.5f).Within(.0001f),
                "The formal first wave is 15 seconds, so precognition must " +
                "publish a 22.5-second warning when that wave starts.");
        }

        [Test]
        public void IDEA0026_TissueRegenerationHealsTrackedBuildingOnePointPerSecond()
        {
            GrayboxBuildingInstance3D building = CompletedInstance(
                "building.instance.tissue-regeneration",
                BuildingCatalog.Warehouse);
            var runtime = new GrayboxBuildingHealthRuntime3D();
            runtime.Synchronize(new[] { building });
            Assert.That(runtime.TryApplyDamage(
                building.StableInstanceId,
                10,
                out _,
                out _), Is.True);

            Assert.That(
                runtime.TryAdvanceRegeneration(
                    building.StableInstanceId,
                    .5f,
                    tissueRegeneration: true,
                    carapaceGrowth: false,
                    inventory: null,
                    out int firstHealing),
                Is.True);
            Assert.That(firstHealing, Is.Zero);
            Assert.That(
                runtime.TryAdvanceRegeneration(
                    building.StableInstanceId,
                    .5f,
                    tissueRegeneration: true,
                    carapaceGrowth: false,
                    inventory: null,
                    out int secondHealing),
                Is.True);
            Assert.That(secondHealing, Is.EqualTo(1));
            AssertHealth(
                runtime,
                building.StableInstanceId,
                BuildingCatalog.Warehouse.MaximumHealth - 9);
        }

        [Test]
        public void IDEA0026_CarapaceWallConsumesOneBiomassAndHealsTenEveryFiveSeconds()
        {
            GrayboxBuildingInstance3D wall = CompletedInstance(
                "building.instance.carapace-regeneration",
                BuildingCatalog.Wall);
            var runtime = new GrayboxBuildingHealthRuntime3D();
            runtime.Synchronize(new[] { wall });
            Assert.That(runtime.TryApplyDamage(
                wall.StableInstanceId,
                30,
                out _,
                out _), Is.True);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Biomass, 2);

            Assert.That(
                runtime.TryAdvanceRegeneration(
                    wall.StableInstanceId,
                    4.9f,
                    tissueRegeneration: false,
                    carapaceGrowth: true,
                    inventory,
                    out int earlyHealing),
                Is.True);
            Assert.That(earlyHealing, Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.EqualTo(2));

            Assert.That(
                runtime.TryAdvanceRegeneration(
                    wall.StableInstanceId,
                    .1f,
                    tissueRegeneration: false,
                    carapaceGrowth: true,
                    inventory,
                    out int firstHealing),
                Is.True);
            Assert.That(firstHealing, Is.EqualTo(10));
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.EqualTo(1));

            Assert.That(
                runtime.TryAdvanceRegeneration(
                    wall.StableInstanceId,
                    5f,
                    tissueRegeneration: false,
                    carapaceGrowth: true,
                    inventory,
                    out int secondHealing),
                Is.True);
            Assert.That(secondHealing, Is.EqualTo(10));
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.Zero);
            AssertHealth(
                runtime,
                wall.StableInstanceId,
                BuildingCatalog.Wall.MaximumHealth - 10);

            Assert.That(
                runtime.TryAdvanceRegeneration(
                    wall.StableInstanceId,
                    5f,
                    tissueRegeneration: false,
                    carapaceGrowth: true,
                    inventory,
                    out int healingWithoutBiomass),
                Is.True);
            Assert.That(healingWithoutBiomass, Is.Zero);
            AssertHealth(
                runtime,
                wall.StableInstanceId,
                BuildingCatalog.Wall.MaximumHealth - 10);
        }

        [Test]
        public void IDEA0026_KillRewardResolverKeepsBaseAndAppliesMetabolicFiftyPercent()
        {
            ResearchEffectSnapshot baseline = ResearchEffectResolver.Resolve(
                Array.Empty<string>());
            ResearchEffectSnapshot metabolic = ResearchEffectResolver.Resolve(
                new[] { "core.research.metabolic-acceleration" });

            Assert.That(
                ResearchKillRewardResolver.ResolveBiomassDrop(
                    baseDrop: 8,
                    qualityMultiplier: 1f,
                    baseline),
                Is.EqualTo(8));
            Assert.That(
                ResearchKillRewardResolver.ResolveBiomassDrop(
                    baseDrop: 8,
                    qualityMultiplier: 1f,
                    metabolic),
                Is.EqualTo(12));
            Assert.That(
                ResearchKillRewardResolver.ResolveBiomassDrop(
                    baseDrop: -10,
                    qualityMultiplier: 1f,
                    metabolic),
                Is.Zero);
        }

        private static void AssertHealth(
            GrayboxBuildingHealthRuntime3D runtime,
            string stableInstanceId,
            int expectedCurrent)
        {
            Assert.That(
                runtime.TryGetHealth(
                    stableInstanceId,
                    out int current,
                    out _,
                    out bool destroyed),
                Is.True);
            Assert.That(current, Is.EqualTo(expectedCurrent));
            Assert.That(destroyed, Is.False);
        }

        private static GrayboxBuildingInstance3D CompletedInstance(
            string stableInstanceId,
            BuildingDefinition definition)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    InstanceAny,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableInstanceId,
                    new PlacedBuilding(definition, 1, 1),
                    new ConstructionProgress(definition.BuildSeconds),
                    default(ResourceNodeBinding),
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                InstanceAny);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, null);
            return instance;
        }
    }
}
