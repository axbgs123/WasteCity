using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.CivilizationExpansion;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.World;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Tests
{
    public sealed class IDEA0022WorldLayerSettlementTransportTests
    {
        [Test]
        public void CatalogFreezesF4AIdentitiesCostsAutonomyAndTransportRules()
        {
            Assert.That(WorldLayerCatalog.PrimaryCity.Id,
                Is.EqualTo("core.city.000001"));
            Assert.That(WorldLayerCatalog.SecondaryCity.Id,
                Is.EqualTo("core.city.000002"));
            Assert.That(WorldLayerCatalog.Outpost.Id,
                Is.EqualTo("core.outpost.000001"));
            Assert.That(WorldLayerCatalog.All.Select(value => value.Id),
                Is.EqualTo(new[]
                {
                    "core.city.000001",
                    "core.city.000002",
                    "core.outpost.000001",
                }));

            AssertAmounts(WorldLayerCatalog.SecondaryCity.BuildCosts,
                (ResourceIds.Alloy, 40),
                (ResourceIds.RefinedStone, 30),
                (ResourceIds.ControlChip, 10));
            Assert.That(WorldLayerCatalog.SecondaryCity.PopulationCost,
                Is.EqualTo(50));
            Assert.That(WorldLayerCatalog.SecondaryCity.InventoryCapacity,
                Is.EqualTo(150));
            Assert.That(WorldLayerCatalog.SecondaryCity.InitialPopulation,
                Is.EqualTo(50));
            Assert.That(WorldLayerCatalog.SecondaryCity.PopulationCapacity,
                Is.EqualTo(100));

            AssertAmounts(WorldLayerCatalog.Outpost.BuildCosts,
                (ResourceIds.Alloy, 12),
                (ResourceIds.Stone, 12));
            Assert.That(WorldLayerCatalog.Outpost.InventoryCapacity,
                Is.EqualTo(150));
            Assert.That(WorldLayerCatalog.AutonomyCycleSeconds,
                Is.EqualTo(10f));
            Assert.That(WorldLayerCatalog.OutpostCycleSeconds,
                Is.EqualTo(12f));
            Assert.That(WorldLayerCatalog.ResearchContributionMultiplier,
                Is.EqualTo(1.2f));
            Assert.That(WorldLayerCatalog.ConvoySecondsPerCell,
                Is.EqualTo(1.5f));
            Assert.That(WorldLayerCatalog.UnescortedInterceptionPercent,
                Is.EqualTo(25));
            Assert.That(WorldLayerCatalog.EscortedInterceptionPercent,
                Is.EqualTo(5));
        }

        [Test]
        public void EstablishmentRequiresRevealedPassableUnoccupiedCellAndExactCost()
        {
            WorldMapModel map = OpenWorld(7, 3, blockedX: 5, blockedY: 1);
            var primary = new ExternalInventoryEndpoint(
                WorldLayerCatalog.PrimaryCity.Id, 150);
            var layer = new WorldLayerRuntime(map, 0, 1, primary);
            ConstructionAccount account = FullConstructionAccount();

            Assert.That(layer.TryEstablishSecondary(
                2, 1,
                SettlementAutonomyTemplate.Industrial,
                account,
                out SettlementRuntime secondary,
                out string error), Is.True, error);
            Assert.That(account.CommitCount, Is.EqualTo(1));
            Assert.That(account.Population, Is.Zero);
            Assert.That(account.GetAmount(ResourceIds.Alloy), Is.Zero);
            Assert.That(account.GetAmount(ResourceIds.RefinedStone), Is.Zero);
            Assert.That(account.GetAmount(ResourceIds.ControlChip), Is.Zero);
            Assert.That(secondary.StableId,
                Is.EqualTo(WorldLayerCatalog.SecondaryCity.Id));

            account.Set(ResourceIds.Alloy, 24);
            account.Set(ResourceIds.Stone, 24);
            Assert.That(layer.TryEstablishOutpost(
                2, 1, account, out _, out error), Is.False);
            Assert.That(error, Does.Contain("占用"));
            Assert.That(account.CommitCount, Is.EqualTo(1),
                "Invalid placement must not spend construction materials.");

            Assert.That(layer.TryEstablishOutpost(
                5, 1, account, out _, out error), Is.False);
            Assert.That(error, Does.Contain("通行"));
            Assert.That(account.CommitCount, Is.EqualTo(1));

            WorldMapModel hidden = OpenWorld(7, 3, reveal: false);
            var hiddenLayer = new WorldLayerRuntime(
                hidden, 0, 1,
                new ExternalInventoryEndpoint(
                    WorldLayerCatalog.PrimaryCity.Id, 150));
            ConstructionAccount hiddenAccount = FullConstructionAccount();
            Assert.That(hiddenLayer.TryEstablishSecondary(
                2, 1,
                SettlementAutonomyTemplate.Military,
                hiddenAccount,
                out _,
                out error), Is.False);
            Assert.That(error, Does.Contain("揭示"));
            Assert.That(hiddenAccount.CommitCount, Is.Zero);

            hiddenLayer.ConfigureExplorationQuery(
                (x, y) => x == 2 && y == 1);
            Assert.That(hiddenLayer.TryEstablishSecondary(
                2, 1,
                SettlementAutonomyTemplate.Military,
                hiddenAccount,
                out _,
                out error), Is.True, error);
        }

        [Test]
        public void PrimaryCityRemainsExternalReferenceAndRemoteInventoriesAreIndependent()
        {
            WorldMapModel map = OpenWorld(7, 3);
            var primary = new ExternalInventoryEndpoint(
                WorldLayerCatalog.PrimaryCity.Id, 150);
            primary.Inventory.Add(ResourceIds.Iron, 10);
            var layer = new WorldLayerRuntime(map, 0, 1, primary);
            ConstructionAccount account = FullConstructionAccount();
            Assert.That(layer.TryEstablishSecondary(
                2, 1,
                SettlementAutonomyTemplate.Industrial,
                account,
                out SettlementRuntime secondary,
                out string error), Is.True, error);
            account.Set(ResourceIds.Alloy, 12);
            account.Set(ResourceIds.Stone, 12);
            Assert.That(layer.TryEstablishOutpost(
                4, 1,
                account,
                out SettlementRuntime outpost,
                out error), Is.True, error);

            Assert.That(layer.PrimaryCity.IsExternalReference, Is.True);
            Assert.That(layer.PrimaryCity.Inventory, Is.Null);
            Assert.That(layer.TryGetInventoryEndpoint(
                WorldLayerCatalog.PrimaryCity.Id,
                out ISettlementInventoryEndpoint resolved), Is.True);
            Assert.That(resolved, Is.SameAs(primary));

            Assert.That(secondary.Inventory.Add(ResourceIds.Iron, 6),
                Is.EqualTo(6));
            Assert.That(outpost.Inventory.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(primary.Inventory.Get(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(secondary.Inventory.Get(ResourceIds.Iron), Is.EqualTo(6));
        }

        [Test]
        public void AutonomousSettlementsUseExactCadenceAndOperationalLinks()
        {
            SettlementRuntime industrial = SettlementRuntime.CreateSecondary(
                2, 1, SettlementAutonomyTemplate.Industrial);
            industrial.Tick(9.9f);
            Assert.That(industrial.Inventory.Get(ResourceIds.Alloy), Is.Zero);
            industrial.Tick(.1f);
            Assert.That(industrial.Inventory.Get(ResourceIds.Alloy), Is.EqualTo(1));
            industrial.Tick(20f);
            Assert.That(industrial.Inventory.Get(ResourceIds.Alloy), Is.EqualTo(3));

            SettlementRuntime military = SettlementRuntime.CreateSecondary(
                3, 1, SettlementAutonomyTemplate.Military);
            military.Tick(10f);
            Assert.That(military.Inventory.Get(ResourceIds.Ammunition),
                Is.EqualTo(1));

            SettlementRuntime research = SettlementRuntime.CreateSecondary(
                4, 1, SettlementAutonomyTemplate.Research);
            Assert.That(research.ResearchContributionMultiplier,
                Is.EqualTo(1.2f));
            research.SetCommunication(false);
            Assert.That(research.ResearchContributionMultiplier,
                Is.EqualTo(1f));

            SettlementRuntime outpost = SettlementRuntime.CreateOutpost(5, 1);
            outpost.SetOperationalLinks(
                communication: true,
                supplied: true,
                maintained: false);
            outpost.Tick(24f);
            Assert.That(outpost.Inventory.Get(ResourceIds.Stone), Is.Zero);
            outpost.SetOperationalLinks(true, true, true);
            outpost.Tick(12f);
            Assert.That(outpost.Inventory.Get(ResourceIds.Stone), Is.EqualTo(1));
        }

        [Test]
        public void FocusControlCommunicationAndLoyaltyRemainSeparateTruths()
        {
            WorldMapModel map = OpenWorld(7, 3);
            var layer = NewLayer(map);
            ConstructionAccount account = FullConstructionAccount();
            Assert.That(layer.TryEstablishSecondary(
                2, 1,
                SettlementAutonomyTemplate.Research,
                account,
                out SettlementRuntime secondary,
                out string error), Is.True, error);
            account.Set(ResourceIds.Alloy, 12);
            account.Set(ResourceIds.Stone, 12);
            Assert.That(layer.TryEstablishOutpost(
                4, 1, account, out SettlementRuntime outpost, out error),
                Is.True, error);

            Assert.That(layer.TryFocus(secondary.StableId), Is.True);
            Assert.That(layer.FocusedSettlementId, Is.EqualTo(secondary.StableId));
            Assert.That(layer.ControlledCityId,
                Is.EqualTo(WorldLayerCatalog.PrimaryCity.Id));
            Assert.That(layer.TryControlCity(
                secondary.StableId,
                remoteCommandUnlocked: false,
                leaderPresent: false), Is.False);
            Assert.That(layer.TryControlCity(
                secondary.StableId,
                remoteCommandUnlocked: false,
                leaderPresent: true), Is.True);
            Assert.That(layer.TryControlCity(
                outpost.StableId,
                remoteCommandUnlocked: true,
                leaderPresent: true), Is.False);

            secondary.SetCommunication(false);
            Assert.That(secondary.CanIssueRemoteCommands, Is.False);
            Assert.That(layer.TryFocus(secondary.StableId), Is.True,
                "Disconnected settlements retain viewable last intelligence.");
            Assert.That(layer.TryControlCity(
                secondary.StableId,
                remoteCommandUnlocked: true,
                leaderPresent: true), Is.False);
            secondary.SetLoyalty(-20);
            Assert.That(secondary.Loyalty, Is.Zero);
            secondary.SetLoyalty(120);
            Assert.That(secondary.Loyalty, Is.EqualTo(100));
        }

        [Test]
        public void ConvoyAtomicallyLoadsTravelsAtOnePointFiveSecondsAndUnloads()
        {
            WorldMapModel map = OpenWorld(7, 3);
            ExternalInventoryEndpoint primary;
            WorldLayerRuntime layer = LayerWithSecondary(
                map, out primary, secondaryX: 2);
            primary.Inventory.Add(ResourceIds.Iron, 10);
            string sessionId = FindSessionForRoll(25, 99);
            var transport = new TransportRuntime(map, layer);

            Assert.That(transport.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Iron, 4) },
                escortSquadId: null,
                out string convoyId,
                out string error), Is.True, error);
            Assert.That(primary.Inventory.Get(ResourceIds.Iron), Is.EqualTo(6));
            Assert.That(transport.GetConvoy(convoyId).CargoAmount(
                ResourceIds.Iron), Is.EqualTo(4));

            transport.Tick(2.99f);
            Assert.That(transport.GetConvoy(convoyId).Status,
                Is.EqualTo(ConvoyStatus.InTransit));
            transport.Tick(.01f);
            Assert.That(transport.GetConvoy(convoyId).Status,
                Is.EqualTo(ConvoyStatus.Delivered));
            Assert.That(layer.GetSettlement(
                WorldLayerCatalog.SecondaryCity.Id).Inventory.Get(
                    ResourceIds.Iron), Is.EqualTo(4));
            Assert.That(transport.GetConvoy(convoyId).CargoTotal, Is.Zero);
        }

        [Test]
        public void DiplomacyInterceptionImmunityIsConsumedBeforeRiskRoll()
        {
            WorldMapModel map = OpenWorld(7, 3);
            WorldLayerRuntime layer = LayerWithSecondary(
                map,
                out ExternalInventoryEndpoint primary,
                secondaryX: 2);
            primary.Inventory.Add(ResourceIds.Stone, 10);
            var immunity = new ImmunityEscortProvider(1);
            var transport = new TransportRuntime(map, layer, immunity);
            string sessionId = FindSessionForRoll(0, 24);
            Assert.That(transport.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Stone, 4) },
                string.Empty,
                out string convoyId,
                out string error), Is.True, error);

            transport.Tick(.1f);

            ConvoySnapshot convoy = transport.GetConvoy(convoyId);
            Assert.That(immunity.Charges, Is.Zero);
            Assert.That(convoy.RiskResolved, Is.True);
            Assert.That(convoy.AppliedRiskPercent, Is.Zero);
            Assert.That(convoy.Status, Is.Not.EqualTo(ConvoyStatus.Destroyed));
        }

        [Test]
        public void SafeDeterministicRollDoesNotConsumeInterceptionImmunity()
        {
            WorldMapModel map = OpenWorld(7, 3);
            WorldLayerRuntime layer = LayerWithSecondary(
                map,
                out ExternalInventoryEndpoint primary,
                secondaryX: 2);
            primary.Inventory.Add(ResourceIds.Stone, 4);
            var immunity = new ImmunityEscortProvider(1);
            var transport = new TransportRuntime(map, layer, immunity);
            Assert.That(transport.TryDispatch(
                FindSessionForRoll(25, 99),
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Stone, 4) },
                string.Empty,
                out string convoyId,
                out string error), Is.True, error);

            transport.Tick(.1f);

            ConvoySnapshot convoy = transport.GetConvoy(convoyId);
            Assert.That(immunity.Charges, Is.EqualTo(1));
            Assert.That(convoy.RiskResolved, Is.True);
            Assert.That(convoy.AppliedRiskPercent,
                Is.EqualTo(WorldLayerCatalog.UnescortedInterceptionPercent));
            Assert.That(convoy.Status, Is.EqualTo(ConvoyStatus.InTransit));
        }

        [Test]
        public void UnfinishedConvoyExclusivelyOwnsItsEscortSquad()
        {
            const string squadId = "core.squad.000001";
            WorldMapModel map = OpenWorld(5, 3);
            WorldLayerRuntime layer = LayerWithSecondary(
                map,
                out ExternalInventoryEndpoint primary,
                secondaryX: 1);
            primary.Inventory.Add(ResourceIds.Iron, 8);
            var provider = new EscortStatusProvider(squadId, active: true);
            var transport = new TransportRuntime(map, layer, provider);
            string sessionId = FindSessionForRoll(25, 99);
            ResourceAmount[] cargo =
            {
                new ResourceAmount(ResourceIds.Iron, 4),
            };
            Assert.That(transport.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                cargo,
                squadId,
                out string firstId,
                out string error), Is.True, error);

            Assert.That(transport.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                cargo,
                squadId,
                out _,
                out error), Is.False);
            Assert.That(error, Does.Contain("护送"));
            Assert.That(primary.Inventory.Get(ResourceIds.Iron), Is.EqualTo(4));

            transport.Tick(1.5f);
            Assert.That(transport.GetConvoy(firstId).Status,
                Is.EqualTo(ConvoyStatus.Delivered));
            Assert.That(transport.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                cargo,
                squadId,
                out _,
                out error), Is.True, error);
        }

        [Test]
        public void ExpeditionSquadCannotBeAssignedAsConvoyEscort()
        {
            const string squadId = SingleCityArmyModel.DefaultSquadId;
            WorldMapModel map = OpenWorld(7, 3);
            var primary = new ExternalInventoryEndpoint(
                WorldLayerCatalog.PrimaryCity.Id, 150);
            primary.Inventory.Add(ResourceIds.Iron, 4);
            var expansion = new CivilizationExpansionRuntime(
                map, 0, 1, primary);
            ConstructionAccount account = FullConstructionAccount();
            Assert.That(expansion.WorldLayer.TryEstablishSecondary(
                2,
                1,
                SettlementAutonomyTemplate.Industrial,
                account,
                out _,
                out string error), Is.True, error);
            using var storage = new CityResourceStorageModel(
                new ResourceInventory(150));
            Assert.That(storage.AddToNetwork(ResourceIds.Alloy, 1),
                Is.EqualTo(1));
            Assert.That(storage.AddToNetwork(ResourceIds.SpiritIron, 1),
                Is.EqualTo(1));
            Assert.That(expansion.Army.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f,
                1,
                false,
                storage), Is.EqualTo(1));
            Assert.That(expansion.TryStartExpedition(
                "test.expedition.session",
                4,
                1,
                out error), Is.True, error);
            Assert.That(expansion.Expedition.Status,
                Is.EqualTo(ArmyExpeditionStatus.Outbound));

            Assert.That(expansion.Transport.TryDispatch(
                FindSessionForRoll(25, 99),
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Iron, 4) },
                squadId,
                out _,
                out error), Is.False);
            Assert.That(error, Does.Contain("护送"));
            Assert.That(primary.Inventory.Get(ResourceIds.Iron), Is.EqualTo(4));
        }

        [Test]
        public void ConvoyEscortSquadCannotStartExpeditionUntilReleased()
        {
            const string squadId = SingleCityArmyModel.DefaultSquadId;
            WorldMapModel map = OpenWorld(7, 3);
            var primary = new ExternalInventoryEndpoint(
                WorldLayerCatalog.PrimaryCity.Id, 150);
            primary.Inventory.Add(ResourceIds.Iron, 4);
            var expansion = new CivilizationExpansionRuntime(
                map, 0, 1, primary);
            ConstructionAccount account = FullConstructionAccount();
            Assert.That(expansion.WorldLayer.TryEstablishSecondary(
                2, 1, SettlementAutonomyTemplate.Industrial, account,
                out _, out string error), Is.True, error);
            using var storage = new CityResourceStorageModel(
                new ResourceInventory(150));
            storage.AddToNetwork(ResourceIds.Alloy, 1);
            storage.AddToNetwork(ResourceIds.SpiritIron, 1);
            Assert.That(expansion.Army.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f, 1, false, storage), Is.EqualTo(1));
            Assert.That(expansion.Transport.TryDispatch(
                FindSessionForRoll(25, 99),
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Iron, 4) },
                squadId,
                out _,
                out error), Is.True, error);

            Assert.That(expansion.TryStartExpedition(
                "test.reverse-occupancy.session",
                4,
                1,
                out error), Is.False);
            Assert.That(error, Does.Contain("护送"));
            Assert.That(expansion.Expedition.Status,
                Is.EqualTo(ArmyExpeditionStatus.Idle));
        }

        [Test]
        public void FullDestinationWaitsWithCargoUntilAtomicUnloadFits()
        {
            WorldMapModel map = OpenWorld(7, 3);
            ExternalInventoryEndpoint primary;
            WorldLayerRuntime layer = LayerWithSecondary(
                map, out primary, secondaryX: 2);
            SettlementRuntime secondary = layer.GetSettlement(
                WorldLayerCatalog.SecondaryCity.Id);
            secondary.Inventory.Add(ResourceIds.Iron, 150);
            primary.Inventory.Add(ResourceIds.Stone, 4);
            var transport = new TransportRuntime(map, layer);
            string sessionId = FindSessionForRoll(25, 99);
            Assert.That(transport.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                secondary.StableId,
                new[] { new ResourceAmount(ResourceIds.Stone, 4) },
                null,
                out string convoyId,
                out string error), Is.True, error);

            transport.Tick(3f);
            Assert.That(transport.GetConvoy(convoyId).Status,
                Is.EqualTo(ConvoyStatus.WaitingForCapacity));
            Assert.That(transport.GetConvoy(convoyId).CargoAmount(
                ResourceIds.Stone), Is.EqualTo(4));
            Assert.That(secondary.Inventory.TryExtract(
                new[] { new ResourceAmount(ResourceIds.Iron, 10) }), Is.True);
            transport.Tick(.1f);
            Assert.That(transport.GetConvoy(convoyId).Status,
                Is.EqualTo(ConvoyStatus.Delivered));
            Assert.That(secondary.Inventory.Get(ResourceIds.Stone), Is.EqualTo(4));
        }

        [Test]
        public void ActiveEscortReducesDeterministicInterceptionFromTwentyFiveToFive()
        {
            string sessionId = FindSessionForRoll(5, 24);
            const string squadId = "core.squad.000001";

            WorldMapModel exposedMap = OpenWorld(5, 3);
            ExternalInventoryEndpoint exposedPrimary;
            WorldLayerRuntime exposedLayer = LayerWithSecondary(
                exposedMap, out exposedPrimary, secondaryX: 1);
            exposedPrimary.Inventory.Add(ResourceIds.Iron, 4);
            var exposed = new TransportRuntime(exposedMap, exposedLayer);
            Assert.That(exposed.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Iron, 4) },
                null,
                out string exposedId,
                out string error), Is.True, error);
            exposed.Tick(.1f);
            Assert.That(exposed.GetConvoy(exposedId).Status,
                Is.EqualTo(ConvoyStatus.Destroyed));
            Assert.That(exposed.GetConvoy(exposedId).CargoTotal, Is.Zero);
            Assert.That(exposedPrimary.Inventory.Get(ResourceIds.Iron), Is.Zero,
                "Destroyed cargo must not return to the source.");

            WorldMapModel escortedMap = OpenWorld(5, 3);
            ExternalInventoryEndpoint escortedPrimary;
            WorldLayerRuntime escortedLayer = LayerWithSecondary(
                escortedMap, out escortedPrimary, secondaryX: 1);
            escortedPrimary.Inventory.Add(ResourceIds.Iron, 4);
            var escort = new EscortStatusProvider(squadId, active: true);
            var escorted = new TransportRuntime(
                escortedMap, escortedLayer, escort);
            Assert.That(escorted.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Iron, 4) },
                squadId,
                out string escortedId,
                out error), Is.True, error);
            Assert.That(escortedId, Is.EqualTo(exposedId));
            escorted.Tick(1.5f);
            Assert.That(escorted.GetConvoy(escortedId).Status,
                Is.EqualTo(ConvoyStatus.Delivered));
            Assert.That(escortedLayer.GetSettlement(
                WorldLayerCatalog.SecondaryCity.Id).Inventory.Get(
                    ResourceIds.Iron), Is.EqualTo(4));
        }

        [Test]
        public void WorldAndTransportSnapshotsRestoreProgressLinksInventoryAndFocus()
        {
            WorldMapModel map = OpenWorld(8, 3);
            ExternalInventoryEndpoint primary;
            WorldLayerRuntime layer = LayerWithSecondary(
                map, out primary, secondaryX: 3);
            SettlementRuntime secondary = layer.GetSettlement(
                WorldLayerCatalog.SecondaryCity.Id);
            secondary.Inventory.Add(ResourceIds.Alloy, 7);
            secondary.Tick(4f);
            secondary.SetCommunication(false);
            secondary.SetLoyalty(42);
            Assert.That(layer.TryFocus(secondary.StableId), Is.True);
            WorldLayerRuntimeSnapshot layerSnapshot = layer.Capture();

            var restoredPrimary = new ExternalInventoryEndpoint(
                WorldLayerCatalog.PrimaryCity.Id, 150);
            var restoredLayer = new WorldLayerRuntime(map, 0, 1, restoredPrimary);
            Assert.That(restoredLayer.TryRestore(
                layerSnapshot, out string error), Is.True, error);
            SettlementRuntime restoredSecondary = restoredLayer.GetSettlement(
                secondary.StableId);
            Assert.That(restoredSecondary.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(7));
            Assert.That(restoredSecondary.AutonomyProgressSeconds,
                Is.EqualTo(4f));
            Assert.That(restoredSecondary.IsCommunicationActive, Is.False);
            Assert.That(restoredSecondary.Loyalty, Is.EqualTo(42));
            Assert.That(restoredLayer.FocusedSettlementId,
                Is.EqualTo(secondary.StableId));

            primary.Inventory.Add(ResourceIds.Iron, 5);
            string sessionId = FindSessionForRoll(25, 99);
            var transport = new TransportRuntime(map, layer);
            Assert.That(transport.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                secondary.StableId,
                new[] { new ResourceAmount(ResourceIds.Iron, 5) },
                null,
                out string convoyId,
                out error), Is.True, error);
            transport.Tick(1.75f);
            TransportRuntimeSnapshot snapshot = transport.Capture();
            var restoredTransport = new TransportRuntime(map, layer);
            Assert.That(restoredTransport.TryRestore(snapshot, out error),
                Is.True, error);
            ConvoySnapshot convoy = restoredTransport.GetConvoy(convoyId);
            Assert.That(convoy.CompletedPathCells, Is.EqualTo(1));
            Assert.That(convoy.SegmentProgressSeconds, Is.EqualTo(.25f)
                .Within(.0001f));
            Assert.That(convoy.RiskResolved, Is.True);
            restoredTransport.Tick(2.75f);
            Assert.That(restoredTransport.GetConvoy(convoyId).Status,
                Is.EqualTo(ConvoyStatus.Delivered));
            Assert.That(secondary.Inventory.Get(ResourceIds.Iron), Is.EqualTo(5));
        }

        [Test]
        public void InvalidSnapshotRestoreIsAtomic()
        {
            WorldMapModel map = OpenWorld(7, 3);
            WorldLayerRuntime layer = NewLayer(map);
            WorldLayerRuntimeSnapshot before = layer.Capture();
            var invalid = new WorldLayerRuntimeSnapshot(
                before.FocusedSettlementId,
                "missing.city",
                before.Settlements);
            Assert.That(layer.TryRestore(invalid, out _), Is.False);
            Assert.That(layer.Capture().ControlledCityId,
                Is.EqualTo(before.ControlledCityId));

            var transport = new TransportRuntime(map, layer);
            TransportRuntimeSnapshot transportBefore = transport.Capture();
            var invalidTransport = new TransportRuntimeSnapshot(
                nextConvoyOrdinal: 0,
                Array.Empty<ConvoySnapshot>());
            Assert.That(transport.TryRestore(invalidTransport, out _), Is.False);
            Assert.That(transport.Capture().NextConvoyOrdinal,
                Is.EqualTo(transportBefore.NextConvoyOrdinal));
        }

        [Test]
        public void SnapshotRoundTripPreservesRevisionsAndFormalOrdinals()
        {
            WorldMapModel map = OpenWorld(7, 3);
            WorldLayerRuntime layer = NewLayer(map);
            ConstructionAccount account = FullConstructionAccount();
            Assert.That(layer.TryEstablishSecondary(
                2,
                1,
                SettlementAutonomyTemplate.Industrial,
                account,
                out SettlementRuntime secondary,
                out string error), Is.True, error);
            secondary.SetLoyalty(73);
            Assert.That(layer.TryFocus(secondary.StableId), Is.True);
            WorldLayerRuntimeSnapshot captured = layer.Capture();

            var restored = NewLayer(map);
            Assert.That(restored.TryRestore(captured, out error), Is.True, error);
            WorldLayerRuntimeSnapshot roundTrip = restored.Capture();
            Assert.That(roundTrip.Revision, Is.EqualTo(captured.Revision));
            Assert.That(roundTrip.NextSettlementOrdinal,
                Is.EqualTo(captured.NextSettlementOrdinal).And.EqualTo(3));
            Assert.That(roundTrip.Settlements.Select(value => value.Revision),
                Is.EqualTo(captured.Settlements.Select(value => value.Revision)));

            var transport = new TransportRuntime(map, layer);
            TransportRuntimeSnapshot transportCaptured = transport.Capture();
            var restoredTransport = new TransportRuntime(map, layer);
            Assert.That(restoredTransport.TryRestore(
                transportCaptured, out error), Is.True, error);
            Assert.That(restoredTransport.Capture().Revision,
                Is.EqualTo(transportCaptured.Revision));
        }

        [Test]
        public void TransportRestoreRejectsBrokenSettlementCrossReferenceAtomically()
        {
            WorldMapModel map = OpenWorld(6, 3);
            ExternalInventoryEndpoint primary;
            WorldLayerRuntime layer = LayerWithSecondary(
                map, out primary, secondaryX: 2);
            primary.Inventory.Add(ResourceIds.Iron, 3);
            var source = new TransportRuntime(map, layer);
            Assert.That(source.TryDispatch(
                FindSessionForRoll(25, 99),
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Iron, 3) },
                null,
                out _,
                out string error), Is.True, error);
            TransportRuntimeSnapshot captured = source.Capture();
            ConvoySnapshot valid = captured.Convoys.Single();
            var broken = new ConvoySnapshot(
                valid.StableId,
                valid.SessionId,
                valid.SourceSettlementId,
                "missing.settlement.000001",
                valid.Cargo,
                valid.Path,
                valid.CompletedPathCells,
                valid.SegmentProgressSeconds,
                valid.EscortSquadId,
                valid.RiskResolved,
                valid.AppliedRiskPercent,
                valid.Status);
            var brokenSnapshot = new TransportRuntimeSnapshot(
                captured.Revision,
                captured.NextConvoyOrdinal,
                new[] { broken });
            var target = new TransportRuntime(map, layer);
            TransportRuntimeSnapshot before = target.Capture();

            Assert.That(target.TryRestore(brokenSnapshot, out _), Is.False);
            Assert.That(target.ConvoyCount, Is.Zero);
            Assert.That(target.Capture().Revision, Is.EqualTo(before.Revision));
            Assert.That(target.Capture().NextConvoyOrdinal,
                Is.EqualTo(before.NextConvoyOrdinal));
        }

        [Test]
        public void UnresolvedEscortedConvoyRequiresKnownProviderOnRestore()
        {
            const string squadId = "core.squad.000001";
            WorldMapModel map = OpenWorld(5, 3);
            ExternalInventoryEndpoint primary;
            WorldLayerRuntime layer = LayerWithSecondary(
                map, out primary, secondaryX: 1);
            primary.Inventory.Add(ResourceIds.Iron, 4);
            string sessionId = FindSessionForRoll(5, 24);
            var provider = new EscortStatusProvider(squadId, active: true);
            var source = new TransportRuntime(map, layer, provider);
            Assert.That(source.TryDispatch(
                sessionId,
                WorldLayerCatalog.PrimaryCity.Id,
                WorldLayerCatalog.SecondaryCity.Id,
                new[] { new ResourceAmount(ResourceIds.Iron, 4) },
                squadId,
                out string convoyId,
                out string error), Is.True, error);
            TransportRuntimeSnapshot snapshot = source.Capture();
            Assert.That(snapshot.Convoys.Single().RiskResolved, Is.False);

            var missingProvider = new TransportRuntime(map, layer);
            Assert.That(missingProvider.TryRestore(snapshot, out _), Is.False);
            Assert.That(missingProvider.ConvoyCount, Is.Zero);

            var unknownProvider = new EscortStatusProvider(
                "core.squad.999999", active: true);
            Assert.That(missingProvider.TryRestore(
                snapshot, unknownProvider, out _), Is.False);
            Assert.That(missingProvider.ConvoyCount, Is.Zero);

            Assert.That(missingProvider.TryRestore(
                snapshot, provider, out error), Is.True, error);
            missingProvider.Tick(1.5f);
            Assert.That(missingProvider.GetConvoy(convoyId).Status,
                Is.EqualTo(ConvoyStatus.Delivered));
            Assert.That(missingProvider.GetConvoy(convoyId).AppliedRiskPercent,
                Is.EqualTo(5));
        }

        private static WorldLayerRuntime LayerWithSecondary(
            WorldMapModel map,
            out ExternalInventoryEndpoint primary,
            int secondaryX)
        {
            primary = new ExternalInventoryEndpoint(
                WorldLayerCatalog.PrimaryCity.Id, 150);
            var layer = new WorldLayerRuntime(map, 0, 1, primary);
            ConstructionAccount account = FullConstructionAccount();
            Assert.That(layer.TryEstablishSecondary(
                secondaryX,
                1,
                SettlementAutonomyTemplate.Industrial,
                account,
                out _,
                out string error), Is.True, error);
            return layer;
        }

        private static WorldLayerRuntime NewLayer(WorldMapModel map)
        {
            return new WorldLayerRuntime(
                map,
                0,
                1,
                new ExternalInventoryEndpoint(
                    WorldLayerCatalog.PrimaryCity.Id, 150));
        }

        private static ConstructionAccount FullConstructionAccount()
        {
            var result = new ConstructionAccount(population: 50);
            result.Set(ResourceIds.Alloy, 40);
            result.Set(ResourceIds.RefinedStone, 30);
            result.Set(ResourceIds.ControlChip, 10);
            return result;
        }

        private static WorldMapModel OpenWorld(
            int width,
            int height,
            bool reveal = true,
            int blockedX = -1,
            int blockedY = -1)
        {
            var cells = new WorldCell[width, height];
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                cells[x, y] = new WorldCell(
                    TerrainKind.Wasteland,
                    null,
                    0,
                    x == blockedX && y == blockedY
                        ? WorldTraversalKind.Cliff
                        : WorldTraversalKind.Open);
            }
            var map = new WorldMapModel(cells);
            if (reveal) map.Reveal(width / 2, height / 2, width + height);
            return map;
        }

        private static string FindSessionForRoll(int minimum, int maximum)
        {
            for (var index = 0; index < 10000; index++)
            {
                string session = "test.session." + index.ToString("D4");
                int roll = TransportRuntime.DeterministicRiskRoll(
                    session,
                    "core.convoy.000001");
                if (roll >= minimum && roll <= maximum)
                    return session;
            }
            Assert.Fail("Could not find deterministic convoy risk fixture.");
            return null;
        }

        private static void AssertAmounts(
            IReadOnlyList<ResourceAmount> actual,
            params (string ResourceId, int Amount)[] expected)
        {
            Assert.That(actual.Select(value =>
                    (value.ResourceId, value.Amount)),
                Is.EqualTo(expected));
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
            public int CommitCount { get; private set; }

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
                if (populationCost < 0 || Population < populationCost ||
                    costs == null || costs.Any(value =>
                        value.Amount < 0 ||
                        GetAmount(value.ResourceId) < value.Amount))
                    return false;
                CommitCount++;
                Population -= populationCost;
                foreach (ResourceAmount cost in costs)
                    amounts[cost.ResourceId] -= cost.Amount;
                return true;
            }
        }

        private sealed class ExternalInventoryEndpoint :
            ISettlementInventoryEndpoint
        {
            public ExternalInventoryEndpoint(string stableSettlementId, int capacity)
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

        private sealed class EscortStatusProvider : IConvoyEscortStatusProvider
        {
            private readonly string squadId;
            private readonly bool active;

            public EscortStatusProvider(string squadId, bool active)
            {
                this.squadId = squadId;
                this.active = active;
            }

            public bool IsNonDormant(string stableSquadId)
            {
                return active && string.Equals(
                    squadId, stableSquadId, StringComparison.Ordinal);
            }

            public bool IsKnownSquad(string stableSquadId)
            {
                return string.Equals(
                    squadId, stableSquadId, StringComparison.Ordinal);
            }
        }

        private sealed class ImmunityEscortProvider :
            IConvoyEscortStatusProvider,
            IConvoyInterceptionImmunityProvider
        {
            public ImmunityEscortProvider(int charges)
            {
                Charges = charges;
            }

            public int Charges { get; private set; }

            public bool IsNonDormant(string stableSquadId) => false;

            public bool IsKnownSquad(string stableSquadId) => false;

            public bool TryConsumeConvoyInterceptionImmunity()
            {
                if (Charges <= 0) return false;
                Charges--;
                return true;
            }
        }
    }
}
