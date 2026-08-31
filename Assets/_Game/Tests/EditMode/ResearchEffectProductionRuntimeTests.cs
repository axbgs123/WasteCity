using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Research;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class ResearchEffectProductionRuntimeTests
    {
        [Test]
        public void ScrapProcessingMakesMiningCompleteAtTwoPointEightFiveSeconds()
        {
            IFormalProductionResearchModifier modifier = Modifier(
                "core.research.scrap-processing");
            WorldMapModel world = SingleNode(ResourceIds.Iron, 10);
            BuildingProductionState state = Mine("mine.research.001");

            Tick(state, 2.84f, modifier, world);
            Assert.That(state.Output.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(10));

            Tick(state, .01f, modifier, world);
            Assert.That(state.Output.Get(ResourceIds.Iron), Is.EqualTo(1));
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(9));
        }

        [Test]
        public void BasicMetallurgyMakesSmeltingCompleteAtFivePointFourSeconds()
        {
            IFormalProductionResearchModifier modifier = Modifier(
                "core.research.automated-machinery");
            BuildingProductionState state = Smelter("smelter.research.001");
            state.Input.Add(ResourceIds.Iron, 2);

            Tick(state, 5.39f, modifier);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);

            Tick(state, .01f, modifier);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
        }

        [Test]
        public void IncompleteResearchKeepsDefinitionsAtTheirOriginalCycleDurations()
        {
            IFormalProductionResearchModifier modifier = Modifier();
            WorldMapModel world = SingleNode(ResourceIds.Iron, 10);
            BuildingProductionState mine = Mine("mine.baseline.001");
            BuildingProductionState smelter = Smelter("smelter.baseline.001");
            smelter.Input.Add(ResourceIds.Iron, 2);

            Tick(mine, 2.85f, modifier, world);
            Tick(smelter, 5.4f, modifier);

            Assert.That(mine.Output.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(smelter.Output.Get(ResourceIds.Alloy), Is.Zero);

            Tick(mine, .16f, modifier, world);
            Tick(smelter, .61f, modifier);
            Assert.That(mine.Output.Get(ResourceIds.Iron), Is.EqualTo(1));
            Assert.That(smelter.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
        }

        [Test]
        public void RepeatedCompletedResearchDoesNotCompoundTheProductionEffect()
        {
            IFormalProductionResearchModifier modifier = Modifier(
                "core.research.scrap-processing",
                "core.research.scrap-processing");
            WorldMapModel world = SingleNode(ResourceIds.Iron, 10);
            BuildingProductionState state = Mine("mine.duplicate.001");

            Tick(state, 2.85f, modifier, world);

            Assert.That(state.Output.Get(ResourceIds.Iron), Is.EqualTo(1));
            Assert.That(state.CompletionRevision, Is.EqualTo(1ul));
        }

        [Test]
        public void SpiritGatheringCompletesFiveStatelessCyclesInThirtyTwoSeconds()
        {
            IFormalProductionResearchModifier modifier = Modifier(
                "core.research.spirit-gathering");
            Assert.That(FormalProductionDefinitionCatalog.TryResolveRecipe(
                "cultivation.production.gather-spirit-stone",
                BuildingCatalog.SpiritGatheringArray.Id.Value,
                out FormalProductionDefinition definition), Is.True);
            var state = new BuildingProductionState(
                "spirit-gathering.research.001",
                definition);
            state.Input.Add(ResourceIds.EnergyCrystal, 10);
            state.Input.Add(ResourceIds.RefinedStone, 5);

            Tick(state, 32f, modifier);

            Assert.That(state.CompletionRevision, Is.EqualTo(5ul));
            Assert.That(state.Output.Get(ResourceIds.SpiritStone),
                Is.EqualTo(5));
        }

        [Test]
        public void RestoredResearchCompletionRebuildsAnEquivalentModifier()
        {
            var source = new FormalResearchRuntime(new ResearchModel());
            source.Model.GrantCompletedForDevelopment(ResearchCatalog.Find(
                "core.research.automated-machinery"));
            source.Model.GrantCompletedForDevelopment(ResearchCatalog.Find(
                "core.research.spirit-gathering"));
            ResearchPersistenceSnapshot saved = source.CaptureForPersistence();

            var restored = new FormalResearchRuntime(new ResearchModel());
            Assert.That(restored.TryPrepareRestoreForPersistence(
                saved.CompletedResearchIds,
                saved.ActiveResearchId,
                saved.RemainingSeconds,
                out ResearchRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(restored.TryCommitRestoreForPersistence(plan, out error),
                Is.True, error);

            IFormalProductionResearchModifier before =
                new FormalProductionResearchModifierAdapter(
                    ResearchEffectResolver.Resolve(
                        saved.CompletedResearchIds));
            IFormalProductionResearchModifier after =
                new FormalProductionResearchModifierAdapter(
                    ResearchEffectResolver.Resolve(
                        restored.CaptureForPersistence().CompletedResearchIds));

            Assert.That(after.ResolveCycleDurationSeconds(
                    "core.production.smelt-alloy",
                    6f),
                Is.EqualTo(before.ResolveCycleDurationSeconds(
                    "core.production.smelt-alloy",
                    6f)));
            Assert.That(after.ResolveCycleDurationSeconds(
                    "cultivation.production.gather-spirit-stone",
                    8f),
                Is.EqualTo(before.ResolveCycleDurationSeconds(
                    "cultivation.production.gather-spirit-stone",
                    8f)));
        }

        private static IFormalProductionResearchModifier Modifier(
            params string[] completedResearchIds)
        {
            return new FormalProductionResearchModifierAdapter(
                ResearchEffectResolver.Resolve(completedResearchIds));
        }

        private static BuildingProductionState Mine(string stableId)
        {
            return new BuildingProductionState(
                stableId,
                FormalProductionDefinitionCatalog.Extraction,
                "world.resource-node.0.0",
                0,
                0);
        }

        private static BuildingProductionState Smelter(string stableId)
        {
            return new BuildingProductionState(
                stableId,
                FormalProductionDefinitionCatalog.Smelting);
        }

        private static WorldMapModel SingleNode(string resourceId, int amount)
        {
            return new WorldMapModel(new[,]
            {
                { new WorldCell(TerrainKind.Rocky, resourceId, amount) },
            });
        }

        private static void Tick(
            BuildingProductionState state,
            float deltaSeconds,
            IFormalProductionResearchModifier modifier,
            WorldMapModel world = null)
        {
            new FormalProductionSimulation().Tick(
                new[] { state },
                deltaSeconds,
                world,
                new ResourceInventory(1000),
                new ResourceCapacityPolicy(),
                activeWarehouseCount: 0,
                globallyPaused: false,
                outputModifier: null,
                researchModifier: modifier);
        }
    }
}
