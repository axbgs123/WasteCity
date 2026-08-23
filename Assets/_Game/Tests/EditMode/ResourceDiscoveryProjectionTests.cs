using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ResourceDiscoveryProjectionTests
    {
        private const string AutomatedMachinery =
            "core.research.automated-machinery";
        private const string ArtifactCrafting =
            "core.research.artifact-crafting";
        private const string BioCultivation =
            "core.research.bio-cultivation";
        private const string FleshElixir =
            "core.research.bridge.flesh-elixir";

        [Test]
        public void AlwaysResourcesRemainDiscoveredWithoutOwnedOrResearchFacts()
        {
            ResourceDiscoveryFacts facts = EmptyFacts();

            string[] discovered = ResourceDiscoveryProjection.Project(facts)
                .Select(value => value.Id)
                .ToArray();

            CollectionAssert.AreEqual(
                ResourceDefinitionCatalog.BaseHudResourceIds,
                discovered);
        }

        [TestCase(OwnershipSource.CityNetwork)]
        [TestCase(OwnershipSource.Backpack)]
        [TestCase(OwnershipSource.ProductionInput)]
        [TestCase(OwnershipSource.ProductionOutput)]
        [TestCase(OwnershipSource.ProductionReserved)]
        [TestCase(OwnershipSource.CraftingReserved)]
        public void EveryAuthoritativeOwnershipBoundaryCanRevealAResource(
            OwnershipSource source)
        {
            ResourceDiscoveryFacts facts = FactsWithOwnedResource(
                ResourceIds.AcidGland,
                source);

            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    Definition(ResourceIds.AcidGland),
                    facts),
                Is.True,
                source.ToString());
        }

        [Test]
        public void DisconnectedWarehouseContentsRemainOwnedDiscoveryFacts()
        {
            var core = new ResourceInventory(150);
            var storage = new CityResourceStorageModel(core, 150);
            Assert.That(storage.TryRegisterWarehouse(
                "warehouse.detached",
                connected: true), Is.True);
            Assert.That(storage.AddToWarehouse(
                "warehouse.detached",
                ResourceIds.AcidGland,
                7), Is.EqualTo(7));
            Assert.That(storage.TrySetWarehouseConnected(
                "warehouse.detached",
                connected: false), Is.True);
            CityResourceStorageSnapshot snapshot = storage.CaptureSnapshot();
            Assert.That(snapshot.GetNetworkAmount(ResourceIds.AcidGland),
                Is.Zero,
                "This fact must not restore logistics access.");

            IReadOnlyList<ResourceAmount> ownedStorage =
                ResourceDiscoveryProjection.ProjectOwnedStorageAmounts(
                    snapshot);
            var facts = new ResourceDiscoveryFacts(
                cityNetwork: ownedStorage,
                backpack: null,
                productionInput: null,
                productionOutput: null,
                productionReserved: null,
                craftingReserved: null,
                completedResearchIds: null);

            Assert.That(ownedStorage.Single(value => string.Equals(
                    value.ResourceId,
                    ResourceIds.AcidGland,
                    StringComparison.Ordinal)).Amount,
                Is.EqualTo(7));
            Assert.That(ResourceDiscoveryProjection.IsDiscovered(
                Definition(ResourceIds.AcidGland),
                facts), Is.True);
            Assert.That(snapshot.GetNetworkAmount(ResourceIds.AcidGland),
                Is.Zero,
                "Discovery projection must not mutate logistics access.");
        }

        [Test]
        public void OwnedOrResearchAcceptsAnyDeclaredResearchFact()
        {
            ResourceDefinition alloy = Definition(ResourceIds.Alloy);

            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    alloy,
                    EmptyFacts()),
                Is.False);
            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    alloy,
                    EmptyFacts(AutomatedMachinery)),
                Is.True);
        }

        [Test]
        public void OwnedOrRecipeAcceptsAnyAlternativeRecipeResearchFact()
        {
            ResourceDefinition elixir = Definition(ResourceIds.Elixir);

            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    elixir,
                    EmptyFacts()),
                Is.False);
            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    elixir,
                    EmptyFacts(FleshElixir)),
                Is.True);
        }

        [Test]
        public void OwnedOrAllRequirementsNeedsEveryDeclaredResearchFact()
        {
            ResourceDefinition extract = Definition(
                ResourceIds.SpiritPlantExtract);

            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    extract,
                    EmptyFacts(ArtifactCrafting)),
                Is.False);
            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    extract,
                    EmptyFacts(BioCultivation)),
                Is.False);
            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    extract,
                    EmptyFacts(ArtifactCrafting, BioCultivation)),
                Is.True);
        }

        [Test]
        public void DepletedOwnershipRecomputesToHiddenWithoutRememberingHistory()
        {
            ResourceDefinition acidGland = Definition(ResourceIds.AcidGland);
            ResourceDiscoveryFacts owned = FactsWithOwnedResource(
                ResourceIds.AcidGland,
                OwnershipSource.ProductionOutput);

            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(acidGland, owned),
                Is.True);
            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    acidGland,
                    EmptyFacts()),
                Is.False);
        }

        [Test]
        public void ZeroAndNegativeAmountsDoNotCountAsOwnership()
        {
            var facts = new ResourceDiscoveryFacts(
                cityNetwork: new[]
                {
                    new ResourceAmount(ResourceIds.ControlChip, 0),
                },
                backpack: new[]
                {
                    new ResourceAmount(ResourceIds.ControlChip, -1),
                },
                productionInput: null,
                productionOutput: null,
                productionReserved: null,
                craftingReserved: null,
                completedResearchIds: null);

            Assert.That(
                ResourceDiscoveryProjection.IsDiscovered(
                    Definition(ResourceIds.ControlChip),
                    facts),
                Is.False);
        }

        [Test]
        public void ProjectPreservesFormalCatalogOrderAndReturnsANewSnapshot()
        {
            ResourceDiscoveryFacts facts = EmptyFacts(
                AutomatedMachinery,
                ArtifactCrafting,
                BioCultivation);

            var first = ResourceDiscoveryProjection.Project(facts);
            var second = ResourceDiscoveryProjection.Project(facts);
            string[] expected = ResourceDefinitionCatalog.All
                .Where(value => ResourceDiscoveryProjection.IsDiscovered(
                    value,
                    facts))
                .Select(value => value.Id)
                .ToArray();

            CollectionAssert.AreEqual(
                expected,
                first.Select(value => value.Id).ToArray());
            Assert.That(first, Is.Not.SameAs(second));
        }

        private static ResourceDiscoveryFacts EmptyFacts(
            params string[] completedResearchIds)
        {
            return new ResourceDiscoveryFacts(
                cityNetwork: null,
                backpack: null,
                productionInput: null,
                productionOutput: null,
                productionReserved: null,
                craftingReserved: null,
                completedResearchIds: completedResearchIds);
        }

        private static ResourceDiscoveryFacts FactsWithOwnedResource(
            string resourceId,
            OwnershipSource source)
        {
            var amount = new[] { new ResourceAmount(resourceId, 1) };
            return new ResourceDiscoveryFacts(
                cityNetwork: source == OwnershipSource.CityNetwork
                    ? amount
                    : null,
                backpack: source == OwnershipSource.Backpack ? amount : null,
                productionInput: source == OwnershipSource.ProductionInput
                    ? amount
                    : null,
                productionOutput: source == OwnershipSource.ProductionOutput
                    ? amount
                    : null,
                productionReserved:
                    source == OwnershipSource.ProductionReserved
                        ? amount
                        : null,
                craftingReserved: source == OwnershipSource.CraftingReserved
                    ? amount
                    : null,
                completedResearchIds: null);
        }

        private static ResourceDefinition Definition(string resourceId)
        {
            Assert.That(ResourceDefinitionCatalog.TryGet(
                resourceId,
                out ResourceDefinition definition), Is.True, resourceId);
            return definition;
        }

        public enum OwnershipSource
        {
            CityNetwork,
            Backpack,
            ProductionInput,
            ProductionOutput,
            ProductionReserved,
            CraftingReserved,
        }
    }
}
