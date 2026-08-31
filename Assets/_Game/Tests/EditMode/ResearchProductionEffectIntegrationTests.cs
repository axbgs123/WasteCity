using System;
using NUnit.Framework;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class ResearchProductionEffectIntegrationTests
    {
        private const float Tolerance = .0001f;

        [Test]
        public void NoCompletedResearchLeavesEveryProductionMultiplierNeutral()
        {
            ResearchEffectSnapshot effects =
                ResearchEffectResolver.Resolve(Array.Empty<string>());

            Assert.That(effects.ProductionCycleMultiplier(
                "core.production.extract-node-resource"), Is.EqualTo(1f));
            Assert.That(effects.ProductionCycleMultiplier(
                "core.production.smelt-alloy"), Is.EqualTo(1f));
            Assert.That(effects.ProductionCycleMultiplier(
                "core.production.assemble-ammunition"), Is.EqualTo(1f));
            Assert.That(effects.ProductionCycleMultiplier(
                "cultivation.production.gather-spirit-stone"),
                Is.EqualTo(1f));
            Assert.That(effects.ProductionCycleMultiplier(
                "fusion.production.high-frequency-flying-sword"),
                Is.EqualTo(1f));
            Assert.That(effects.ResolveLogisticsRange(8), Is.EqualTo(8));
        }

        [Test]
        public void CompletedResearchAppliesConfiguredRouteProductionEffects()
        {
            ResearchEffectSnapshot effects = ResearchEffectResolver.Resolve(
                new[]
                {
                    "core.research.scrap-processing",
                    "core.research.automated-machinery",
                    "core.research.precision-assembly",
                    "core.research.thermal-engineering",
                    "core.research.spirit-sensing",
                    "core.research.artifact-crafting",
                    "core.research.spirit-gathering",
                    "core.research.alchemy",
                    "core.research.adaptive-tissue",
                    "core.research.bio-cultivation",
                    "core.research.mind-resonance",
                    "core.research.psionic-workshop",
                    "core.research.consciousness-network",
                    "core.research.bridge.psionic-mech",
                    "core.research.bridge.high-frequency-sword",
                    "core.research.bridge.bio-hangar",
                    "core.research.bridge.spirit-plant",
                });

            AssertCycle(effects, "core.production.extract-node-resource", .95f);
            AssertCycle(effects, "core.production.smelt-alloy", .90f);
            AssertCycle(effects, "core.production.assemble-ammunition", .90f);
            AssertCycle(effects, "technology.production.energy-cell", .80f);

            AssertCycle(
                effects,
                "cultivation.production.refine-spirit-iron",
                .90f);
            AssertCycle(
                effects,
                "cultivation.production.flying-sword",
                .85f);
            AssertCycle(
                effects,
                "cultivation.production.gather-spirit-stone",
                .80f);
            AssertCycle(effects, "cultivation.production.elixir", .85f);

            AssertCycle(
                effects,
                "biological.production.biomass-concentrate",
                .90f);
            AssertCycle(effects, "biological.production.weapon", .85f);
            AssertCycle(
                effects,
                "psionics.production.resonance-metal",
                .90f);
            AssertCycle(effects, "psionics.production.amplifier", .85f);
            AssertCycle(
                effects,
                "psionics.production.consciousness-shard",
                .90f);

            AssertCycle(
                effects,
                "fusion.production.psionic-mech-components",
                .85f);
            AssertCycle(
                effects,
                "fusion.production.high-frequency-flying-sword",
                .80f);
            AssertCycle(
                effects,
                "fusion.production.bio-hangar-weapons",
                .85f);
            AssertCycle(
                effects,
                "fusion.production.spirit-plant-extract",
                .80f);
        }

        [Test]
        public void ProductionEffectsAreOrderIndependentAndDuplicateResearchIsIdempotent()
        {
            ResearchEffectSnapshot forward = ResearchEffectResolver.Resolve(
                new[]
                {
                    "core.research.spirit-gathering",
                    "core.research.formation-reinforcement",
                    "core.research.spirit-gathering",
                });
            ResearchEffectSnapshot reverse = ResearchEffectResolver.Resolve(
                new[]
                {
                    "core.research.formation-reinforcement",
                    "core.research.spirit-gathering",
                });

            const string recipeId =
                "cultivation.production.gather-spirit-stone";
            Assert.That(
                forward.ProductionCycleMultiplier(recipeId),
                Is.EqualTo(.8f / 1.5f).Within(Tolerance));
            Assert.That(
                reverse.ProductionCycleMultiplier(recipeId),
                Is.EqualTo(.8f / 1.5f).Within(Tolerance));
        }

        [Test]
        public void OrbitalSupplyOverridesFormationRangeRegardlessOfCompletionOrder()
        {
            ResearchEffectSnapshot formation = ResearchEffectResolver.Resolve(
                new[] { "core.research.formation-reinforcement" });
            ResearchEffectSnapshot orbital = ResearchEffectResolver.Resolve(
                new[] { "core.research.orbital-supply" });
            ResearchEffectSnapshot formationThenOrbital =
                ResearchEffectResolver.Resolve(new[]
                {
                    "core.research.formation-reinforcement",
                    "core.research.orbital-supply",
                });
            ResearchEffectSnapshot orbitalThenFormation =
                ResearchEffectResolver.Resolve(new[]
                {
                    "core.research.orbital-supply",
                    "core.research.formation-reinforcement",
                });

            Assert.That(formation.ResolveLogisticsRange(8), Is.EqualTo(12));
            Assert.That(orbital.ResolveLogisticsRange(8), Is.EqualTo(24));
            Assert.That(
                formationThenOrbital.ResolveLogisticsRange(8),
                Is.EqualTo(24));
            Assert.That(
                orbitalThenFormation.ResolveLogisticsRange(8),
                Is.EqualTo(24));
        }

        private static void AssertCycle(
            ResearchEffectSnapshot effects,
            string recipeId,
            float expected)
        {
            Assert.That(
                effects.ProductionCycleMultiplier(recipeId),
                Is.EqualTo(expected).Within(Tolerance),
                recipeId);
        }

    }
}
