using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Progression;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class PocketUniverseProductionIntegrationTests
    {
        [Test]
        public void IDEA0020_FlagshipDoublesACompleteBatchWithoutChangingInputOrCycle()
        {
            PocketUniverseFateEffect effect = Flagship(
                "building.instance.000001",
                BuildingCatalog.Smelter.Id.Value);
            var state = new BuildingProductionState(
                "building.instance.000001",
                FormalProductionDefinitionCatalog.Smelting);
            state.Input.Add(ResourceIds.Iron, 2);

            Tick(state, 6f, effect);

            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(2));
            Assert.That(state.ProgressSeconds, Is.Zero);
            Assert.That(state.HasReservedInputs, Is.False);
            Assert.That(state.CompletionRevision, Is.EqualTo(1ul));
        }

        [Test]
        public void IDEA0020_MultipliedOutputCapacityStopsBeforeInputReservation()
        {
            PocketUniverseFateEffect effect = Flagship(
                "building.instance.000001",
                BuildingCatalog.Smelter.Id.Value);
            var state = new BuildingProductionState(
                "building.instance.000001",
                FormalProductionDefinitionCatalog.Smelting);
            state.Input.Add(ResourceIds.Iron, 2);
            state.Output.Add(ResourceIds.Alloy, 9);

            Tick(state, 6f, effect);

            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(9));
            Assert.That(state.HasReservedInputs, Is.False);
            Assert.That(state.ProgressSeconds, Is.Zero);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutputFull));
        }

        [Test]
        public void IDEA0020_NonFlagshipRemainsBaselineAndMiningConsumesMultipliedNodeOutput()
        {
            PocketUniverseFateEffect effect = Flagship(
                "building.instance.000001",
                BuildingCatalog.MiningStation.Id.Value);
            var ordinary = new BuildingProductionState(
                "building.instance.000002",
                FormalProductionDefinitionCatalog.Smelting);
            ordinary.Input.Add(ResourceIds.Iron, 2);
            Tick(ordinary, 6f, effect);
            Assert.That(ordinary.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));

            var mine = new BuildingProductionState(
                "building.instance.000001",
                FormalProductionDefinitionCatalog.Extraction,
                "world.resource-node.0.0",
                0,
                0);
            var world = new WorldMapModel(new[,]
            {
                { new WorldCell(TerrainKind.Rocky, ResourceIds.Iron, 10) },
            });
            Tick(mine, 3f, effect, world);

            Assert.That(mine.Output.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(8));
        }

        private static PocketUniverseFateEffect Flagship(
            string stableId,
            string definitionId)
        {
            var effect = new PocketUniverseFateEffect();
            Assert.That(effect.SelectFlagships(new[]
            {
                new PocketUniverseBuildingCandidate(
                    stableId,
                    definitionId,
                    isCompleted: true,
                    isPlayerOwned: true),
            }), Is.EqualTo(1));
            return effect;
        }

        private static void Tick(
            BuildingProductionState state,
            float deltaSeconds,
            PocketUniverseFateEffect effect,
            WorldMapModel world = null)
        {
            Type modifierType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "WasteCity.Economy.IFormalProductionOutputModifier",
                    false))
                .FirstOrDefault(value => value != null);
            Assert.That(modifierType, Is.Not.Null);
            Assert.That(modifierType.IsInstanceOfType(effect), Is.True);
            MethodInfo tick = typeof(FormalProductionSimulation).GetMethod(
                "Tick",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(System.Collections.Generic.IReadOnlyList<
                        BuildingProductionState>),
                    typeof(float),
                    typeof(WorldMapModel),
                    typeof(ResourceInventory),
                    typeof(ResourceCapacityPolicy),
                    typeof(int),
                    typeof(bool),
                    modifierType,
                },
                null);
            Assert.That(tick, Is.Not.Null);
            tick.Invoke(new FormalProductionSimulation(), new object[]
            {
                new[] { state },
                deltaSeconds,
                world,
                new ResourceInventory(1000),
                new ResourceCapacityPolicy(),
                0,
                false,
                effect,
            });
        }
    }
}
