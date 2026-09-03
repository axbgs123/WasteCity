using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Progression;
using WasteCity.World;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Tests
{
    public sealed class QuantumEntanglementInventoryNetworkTests
    {
        [Test]
        public void IDEA0028_ConnectedBasicResourcesExposeOneSharedAmount()
        {
            Fixture fixture = Create();
            fixture.Primary.Inventory.Add(ResourceIds.Iron, 10);
            fixture.Secondary.Inventory.Add(ResourceIds.Iron, 6);
            fixture.Outpost.Inventory.Add(ResourceIds.Iron, 4);
            QuantumEntanglementRuntime quantum = ConnectedIron();
            fixture.Layer.ConfigureQuantumEntanglement(quantum);

            ISettlementInventoryEndpoint primary = Endpoint(
                fixture.Layer, WorldLayerCatalog.PrimaryCity.Id);
            ISettlementInventoryEndpoint secondary = Endpoint(
                fixture.Layer, WorldLayerCatalog.SecondaryCity.Id);
            ISettlementInventoryEndpoint outpost = Endpoint(
                fixture.Layer, WorldLayerCatalog.Outpost.Id);

            Assert.That(primary.GetAmount(ResourceIds.Iron), Is.EqualTo(20));
            Assert.That(secondary.GetAmount(ResourceIds.Iron), Is.EqualTo(20));
            Assert.That(outpost.GetAmount(ResourceIds.Iron), Is.EqualTo(20));
            Assert.That(quantum.Capture().CommittedSynchronizationKeys,
                Is.Empty, "Read-only shared projections are not commits.");

            Assert.That(outpost.TryExtract(new[]
            {
                new ResourceAmount(ResourceIds.Iron, 15),
            }), Is.True);
            Assert.That(UnderlyingTotal(fixture, ResourceIds.Iron),
                Is.EqualTo(5), "Shared extraction must move truth, not copy it.");
            Assert.That(primary.GetAmount(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(quantum.Capture().CommittedSynchronizationKeys,
                Is.EqualTo(new[]
                {
                    QuantumEntanglementRuntime.FirstSynchronizationKey,
                }));

            Assert.That(secondary.TryAccept(new[]
            {
                new ResourceAmount(ResourceIds.Iron, 9),
            }), Is.True);
            Assert.That(UnderlyingTotal(fixture, ResourceIds.Iron),
                Is.EqualTo(14));
            Assert.That(outpost.GetAmount(ResourceIds.Iron), Is.EqualTo(14));
        }

        [Test]
        public void IDEA0028_DisconnectedSettlementKeepsItsLocalBasicResources()
        {
            Fixture fixture = Create();
            fixture.Primary.Inventory.Add(ResourceIds.Iron, 10);
            fixture.Secondary.Inventory.Add(ResourceIds.Iron, 6);
            fixture.Outpost.Inventory.Add(ResourceIds.Iron, 4);
            fixture.Secondary.SetCommunication(false);
            QuantumEntanglementRuntime quantum = ConnectedIron();
            fixture.Layer.ConfigureQuantumEntanglement(quantum);

            ISettlementInventoryEndpoint primary = Endpoint(
                fixture.Layer, WorldLayerCatalog.PrimaryCity.Id);
            ISettlementInventoryEndpoint secondary = Endpoint(
                fixture.Layer, WorldLayerCatalog.SecondaryCity.Id);

            Assert.That(primary.GetAmount(ResourceIds.Iron), Is.EqualTo(14));
            Assert.That(quantum.Capture().CommittedSynchronizationKeys,
                Is.Empty);
            Assert.That(secondary.GetAmount(ResourceIds.Iron), Is.EqualTo(6));
            Assert.That(secondary.TryExtract(new[]
            {
                new ResourceAmount(ResourceIds.Iron, 5),
            }), Is.True);
            Assert.That(fixture.Secondary.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(1));
            Assert.That(primary.GetAmount(ResourceIds.Iron), Is.EqualTo(14));
        }

        [Test]
        public void IDEA0028_NonBasicResourcesStayLocalAndMixedFailureIsAtomic()
        {
            Fixture fixture = Create();
            fixture.Primary.Inventory.Add(ResourceIds.Iron, 10);
            fixture.Secondary.Inventory.Add(ResourceIds.Alloy, 2);
            QuantumEntanglementRuntime quantum = ConnectedIron();
            fixture.Layer.ConfigureQuantumEntanglement(quantum);
            ISettlementInventoryEndpoint secondary = Endpoint(
                fixture.Layer, WorldLayerCatalog.SecondaryCity.Id);

            Assert.That(secondary.GetAmount(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(secondary.GetAmount(ResourceIds.Alloy), Is.EqualTo(2));
            Assert.That(Endpoint(
                    fixture.Layer,
                    WorldLayerCatalog.PrimaryCity.Id).GetAmount(
                    ResourceIds.Alloy),
                Is.Zero);

            Assert.That(secondary.TryExtract(new[]
            {
                new ResourceAmount(ResourceIds.Iron, 8),
                new ResourceAmount(ResourceIds.Alloy, 3),
            }), Is.False);
            Assert.That(fixture.Primary.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(10));
            Assert.That(fixture.Secondary.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(2));
            Assert.That(quantum.Capture().CommittedSynchronizationKeys,
                Is.Empty);
        }

        [Test]
        public void IDEA0028_RequesterLocalSharedCommitDoesNotRecordSynchronization()
        {
            Fixture fixture = Create();
            fixture.Secondary.Inventory.Add(ResourceIds.Iron, 5);
            QuantumEntanglementRuntime quantum = ConnectedIron();
            fixture.Layer.ConfigureQuantumEntanglement(quantum);
            ISettlementInventoryEndpoint secondary = Endpoint(
                fixture.Layer, WorldLayerCatalog.SecondaryCity.Id);

            Assert.That(secondary.TryExtract(new[]
            {
                new ResourceAmount(ResourceIds.Iron, 2),
            }), Is.True);
            Assert.That(quantum.Capture().CommittedSynchronizationKeys,
                Is.Empty);
            Assert.That(quantum.Capture().Revision, Is.Zero);
        }

        [Test]
        public void IDEA0028_RuntimeDisconnectAndConfigurationRemovalRestoreLocalRoutes()
        {
            Fixture fixture = Create();
            fixture.Primary.Inventory.Add(ResourceIds.Iron, 10);
            fixture.Secondary.Inventory.Add(ResourceIds.Iron, 6);
            QuantumEntanglementRuntime quantum = ConnectedIron();
            fixture.Layer.ConfigureQuantumEntanglement(quantum);
            ISettlementInventoryEndpoint secondary = Endpoint(
                fixture.Layer, WorldLayerCatalog.SecondaryCity.Id);
            Assert.That(secondary.GetAmount(ResourceIds.Iron), Is.EqualTo(16));

            Assert.That(quantum.TrySetConnected(false), Is.True);
            Assert.That(secondary.GetAmount(ResourceIds.Iron), Is.EqualTo(6));

            Assert.That(quantum.TrySetConnected(true), Is.True);
            Assert.That(secondary.GetAmount(ResourceIds.Iron), Is.EqualTo(16));
            fixture.Layer.ConfigureQuantumEntanglement(null);
            ISettlementInventoryEndpoint restored = Endpoint(
                fixture.Layer, WorldLayerCatalog.SecondaryCity.Id);
            Assert.That(restored, Is.SameAs(fixture.Secondary));
            Assert.That(restored.GetAmount(ResourceIds.Iron), Is.EqualTo(6));
        }

        private static QuantumEntanglementRuntime ConnectedIron() =>
            new QuantumEntanglementRuntime(new[] { ResourceIds.Iron });

        private static int UnderlyingTotal(Fixture fixture, string resourceId)
        {
            return fixture.Primary.Inventory.Get(resourceId) +
                fixture.Secondary.Inventory.Get(resourceId) +
                fixture.Outpost.Inventory.Get(resourceId);
        }

        private static ISettlementInventoryEndpoint Endpoint(
            WorldLayerRuntime layer,
            string settlementId)
        {
            Assert.That(layer.TryGetInventoryEndpoint(
                settlementId, out ISettlementInventoryEndpoint endpoint),
                Is.True);
            return endpoint;
        }

        private static Fixture Create()
        {
            var cells = new WorldCell[7, 3];
            for (var x = 0; x < 7; x++)
            for (var y = 0; y < 3; y++)
                cells[x, y] = new WorldCell(
                    TerrainKind.Wasteland,
                    null,
                    0,
                    WorldTraversalKind.Open);
            var map = new WorldMapModel(cells);
            map.Reveal(3, 1, 10);
            var primary = new EndpointFixture(
                WorldLayerCatalog.PrimaryCity.Id,
                150);
            var layer = new WorldLayerRuntime(map, 0, 1, primary);

            ConstructionAccount secondaryCost = SecondaryCost();
            Assert.That(layer.TryEstablishSecondary(
                2,
                1,
                SettlementAutonomyTemplate.Industrial,
                secondaryCost,
                out SettlementRuntime secondary,
                out string error), Is.True, error);
            ConstructionAccount outpostCost = OutpostCost();
            Assert.That(layer.TryEstablishOutpost(
                4,
                1,
                outpostCost,
                out SettlementRuntime outpost,
                out error), Is.True, error);
            return new Fixture(layer, primary, secondary, outpost);
        }

        private static ConstructionAccount SecondaryCost()
        {
            var account = new ConstructionAccount(50);
            account.Set(ResourceIds.Alloy, 40);
            account.Set(ResourceIds.RefinedStone, 30);
            account.Set(ResourceIds.ControlChip, 10);
            return account;
        }

        private static ConstructionAccount OutpostCost()
        {
            var account = new ConstructionAccount(0);
            account.Set(ResourceIds.Alloy, 12);
            account.Set(ResourceIds.Stone, 12);
            return account;
        }

        private sealed class Fixture
        {
            public Fixture(
                WorldLayerRuntime layer,
                EndpointFixture primary,
                SettlementRuntime secondary,
                SettlementRuntime outpost)
            {
                Layer = layer;
                Primary = primary;
                Secondary = secondary;
                Outpost = outpost;
            }

            public WorldLayerRuntime Layer { get; }
            public EndpointFixture Primary { get; }
            public SettlementRuntime Secondary { get; }
            public SettlementRuntime Outpost { get; }
        }

        private sealed class EndpointFixture : ISettlementInventoryEndpoint
        {
            public EndpointFixture(string stableSettlementId, int capacity)
            {
                StableSettlementId = stableSettlementId;
                Inventory = new SettlementInventory(capacity);
            }

            public string StableSettlementId { get; }
            public SettlementInventory Inventory { get; }
            public int GetAmount(string resourceId) => Inventory.Get(resourceId);
            public int AcceptableSpace => Inventory.FreeSpace;
            public bool TryExtract(IReadOnlyList<ResourceAmount> amounts) =>
                Inventory.TryExtract(amounts);
            public bool TryAccept(IReadOnlyList<ResourceAmount> amounts) =>
                Inventory.TryAccept(amounts);
        }

        private sealed class ConstructionAccount :
            ISettlementConstructionAccount
        {
            private readonly Dictionary<string, int> amounts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public ConstructionAccount(int population)
            {
                Population = population;
            }

            public int Population { get; private set; }

            public int GetAmount(string resourceId)
            {
                return amounts.TryGetValue(resourceId, out int value)
                    ? value
                    : 0;
            }

            public void Set(string resourceId, int amount)
            {
                amounts[resourceId] = Math.Max(0, amount);
            }

            public bool TryCommit(
                IReadOnlyList<ResourceAmount> costs,
                int populationCost)
            {
                if (costs == null || populationCost < 0 ||
                    Population < populationCost ||
                    costs.Any(value =>
                        value.Amount < 0 ||
                        GetAmount(value.ResourceId) < value.Amount))
                    return false;
                Population -= populationCost;
                foreach (ResourceAmount cost in costs)
                    amounts[cost.ResourceId] -= cost.Amount;
                return true;
            }
        }
    }
}
