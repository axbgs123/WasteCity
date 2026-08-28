using System;
using System.Linq;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ArmyPersistenceModelTests
    {
        [Test]
        public void IDEA0022_CommandCaptureRestorePreservesEveryCommandFact()
        {
            var source = new FriendlyUnitCommandModel();
            source.SetRally(4.5f, -2f);
            source.Guard();
            Assert.That(source.TryExpedition(12, 9, true, true), Is.True);
            source.RecordLoss(FriendlyUnitKind.Puppet);
            source.RecordLoss(FriendlyUnitKind.Behemoth);
            source.RecordLoss(FriendlyUnitKind.Controlled);
            FriendlyUnitCommandPersistenceSnapshot saved =
                source.CaptureForPersistence();
            var target = new FriendlyUnitCommandModel();

            Assert.That(target.TryPrepareRestoreForPersistence(
                saved,
                out FriendlyUnitCommandRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(target.TryCommitRestoreForPersistence(
                plan, out error), Is.True, error);

            AssertCommandEqual(saved, target.CaptureForPersistence());
        }

        [Test]
        public void InvalidCommandAndStalePlanLeaveTargetUnchanged()
        {
            var target = new FriendlyUnitCommandModel();
            target.SetRally(1f, 2f);
            FriendlyUnitCommandPersistenceSnapshot before =
                target.CaptureForPersistence();
            var invalid = new FriendlyUnitCommandPersistenceSnapshot(
                true, float.NaN, 0f,
                FriendlySquadCommandType.Guard,
                false, 0, 0, 0, 0, 0);

            Assert.That(target.TryPrepareRestoreForPersistence(
                invalid, out _, out _), Is.False);
            AssertCommandEqual(before, target.CaptureForPersistence());

            Assert.That(target.TryPrepareRestoreForPersistence(
                before,
                out FriendlyUnitCommandRestorePlan stale,
                out string error), Is.True, error);
            target.FollowLeader();
            FriendlyUnitCommandPersistenceSnapshot changed =
                target.CaptureForPersistence();
            Assert.That(target.TryCommitRestoreForPersistence(
                stale, out _), Is.False);
            AssertCommandEqual(changed, target.CaptureForPersistence());
        }

        [Test]
        public void ArmyRoundTripPreservesProgressUnitsMaintenanceAndCommand()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.Alloy, 1),
                (ResourceIds.SpiritIron, 1),
                (ResourceIds.ControlChip, 1));
            var source = new SingleCityArmyModel();
            Assert.That(source.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f, 1, false, storage), Is.EqualTo(1));
            Assert.That(source.TickManufacturing(
                ArmyUnitCatalog.PsionicMechId,
                30f, 1, false, storage), Is.Zero);
            source.TickMaintenance(60f, false, storage);
            Assert.That(source.Units[0].IsActive, Is.False);
            source.Commands.SetRally(5f, 6f);
            source.Commands.Guard();
            source.SetLeaderAssignment(true, true);
            SingleCityArmyPersistenceSnapshot saved =
                source.CaptureForPersistence();
            var target = new SingleCityArmyModel();

            Assert.That(target.TryPrepareRestoreForPersistence(
                saved,
                out SingleCityArmyRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(target.TryCommitRestoreForPersistence(
                plan, out error), Is.True, error);

            AssertArmyEqual(saved, target.CaptureForPersistence());
            Assert.That(target.ManufacturingProgress(
                ArmyUnitCatalog.PsionicMechId), Is.EqualTo(30f));
            Assert.That(target.Units.Single().MaintenanceElapsed,
                Is.EqualTo(60f));
            Assert.That(target.ResolveSquadDamageMultiplier(),
                Is.EqualTo(1.2f));
        }

        [Test]
        public void InvalidArmySnapshotAndStalePlanAreAtomic()
        {
            var target = new SingleCityArmyModel();
            SingleCityArmyPersistenceSnapshot before =
                target.CaptureForPersistence();
            var invalid = new SingleCityArmyPersistenceSnapshot(
                2,
                new[]
                {
                    new ArmyManufacturingPersistenceState(
                        "unknown.unit", 1f),
                },
                Array.Empty<ArmyUnitPersistenceState>(),
                before.Command,
                false,
                false,
                Array.Empty<ArmyUnitLossPersistenceState>());

            Assert.That(target.TryPrepareRestoreForPersistence(
                invalid, out _, out _), Is.False);
            AssertArmyEqual(before, target.CaptureForPersistence());

            Assert.That(target.TryPrepareRestoreForPersistence(
                before,
                out SingleCityArmyRestorePlan stale,
                out string error), Is.True, error);
            target.SetLeaderAssignment(true, true);
            SingleCityArmyPersistenceSnapshot changed =
                target.CaptureForPersistence();
            Assert.That(target.TryCommitRestoreForPersistence(
                stale, out _), Is.False);
            AssertArmyEqual(changed, target.CaptureForPersistence());
        }

        [Test]
        public void ExpeditionOutboundRoundTripContinuesDeterministically()
        {
            ArmyExpeditionUnit[] units = StrongSquad();
            var source = new ArmyExpeditionModel();
            Assert.That(source.TryStart(
                "session.persistence", 15, 21, 4, 8f,
                units, true), Is.True);
            source.Tick(17f, false);
            ArmyExpeditionPersistenceSnapshot saved =
                source.CaptureForPersistence();
            var restored = new ArmyExpeditionModel();

            Assert.That(restored.TryPrepareRestoreForPersistence(
                saved,
                out ArmyExpeditionRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(restored.TryCommitRestoreForPersistence(
                plan, out error), Is.True, error);
            AssertExpeditionEqual(saved, restored.CaptureForPersistence());

            source.Tick(source.RemainingSeconds, false);
            restored.Tick(restored.RemainingSeconds, false);
            Assert.That(restored.Encounter.EnemyDefinitionIds,
                Is.EqualTo(source.Encounter.EnemyDefinitionIds));
            Assert.That(restored.Resolution.CasualtyStableUnitIds,
                Is.EqualTo(source.Resolution.CasualtyStableUnitIds));
            Assert.That(restored.PendingLoot, Is.EqualTo(source.PendingLoot));
        }

        [Test]
        public void ReturningExpeditionRoundTripPreservesPendingLoot()
        {
            var source = new ArmyExpeditionModel();
            Assert.That(source.TryStart(
                "session.return.persistence", 8, 9, 2, 10f,
                StrongSquad(), true), Is.True);
            source.Tick(source.OutboundDurationSeconds, false);
            Assert.That(source.PendingLoot, Is.Not.Empty);
            ArmyExpeditionPersistenceSnapshot saved =
                source.CaptureForPersistence();
            var restored = new ArmyExpeditionModel();
            Assert.That(restored.TryPrepareRestoreForPersistence(
                saved,
                out ArmyExpeditionRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(restored.TryCommitRestoreForPersistence(
                plan, out error), Is.True, error);

            AssertExpeditionEqual(saved, restored.CaptureForPersistence());
            restored.Tick(restored.ReturnDurationSeconds, false);
            using var storage = new CityResourceStorageModel(
                new ResourceInventory(150));
            Assert.That(restored.TryDepositReturnedLoot(storage), Is.True);
            foreach (ResourceAmount amount in saved.PendingLoot)
                Assert.That(storage.GetNetworkAmount(amount.ResourceId),
                    Is.EqualTo(amount.Amount));
        }

        [Test]
        public void InvalidExpeditionLootAndStalePlanAreAtomic()
        {
            var target = new ArmyExpeditionModel();
            ArmyExpeditionPersistenceSnapshot idle =
                target.CaptureForPersistence();
            var invalid = new ArmyExpeditionPersistenceSnapshot(
                ArmyExpeditionStatus.Returned,
                "session.invalid", 1, 1, 1,
                46.5f, 1.5f, 0f,
                StrongSquad(), true, false,
                new[] { EnemyCatalog.Gnawer.Id.Value },
                true, 100f, 60,
                Array.Empty<string>(),
                new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 999),
                });

            Assert.That(target.TryPrepareRestoreForPersistence(
                invalid, out _, out _), Is.False);
            AssertExpeditionEqual(idle, target.CaptureForPersistence());

            var source = new ArmyExpeditionModel();
            Assert.That(source.TryStart(
                "session.stale", 2, 3, 1, 1f,
                StrongSquad(), true), Is.True);
            ArmyExpeditionPersistenceSnapshot active =
                source.CaptureForPersistence();
            Assert.That(target.TryPrepareRestoreForPersistence(
                active,
                out ArmyExpeditionRestorePlan stale,
                out string error), Is.True, error);
            Assert.That(target.TryStart(
                "session.changed", 3, 4, 2, 1f,
                StrongSquad(), false), Is.True);
            ArmyExpeditionPersistenceSnapshot changed =
                target.CaptureForPersistence();
            Assert.That(target.TryCommitRestoreForPersistence(
                stale, out _), Is.False);
            AssertExpeditionEqual(changed, target.CaptureForPersistence());
        }

        private static void AssertCommandEqual(
            FriendlyUnitCommandPersistenceSnapshot expected,
            FriendlyUnitCommandPersistenceSnapshot actual)
        {
            Assert.That(actual.HasFixedRally, Is.EqualTo(expected.HasFixedRally));
            Assert.That(actual.RallyX, Is.EqualTo(expected.RallyX));
            Assert.That(actual.RallyY, Is.EqualTo(expected.RallyY));
            Assert.That(actual.Command, Is.EqualTo(expected.Command));
            Assert.That(actual.HasExpeditionTarget,
                Is.EqualTo(expected.HasExpeditionTarget));
            Assert.That(actual.ExpeditionTargetX,
                Is.EqualTo(expected.ExpeditionTargetX));
            Assert.That(actual.ExpeditionTargetY,
                Is.EqualTo(expected.ExpeditionTargetY));
            Assert.That(actual.PuppetLosses, Is.EqualTo(expected.PuppetLosses));
            Assert.That(actual.BehemothLosses,
                Is.EqualTo(expected.BehemothLosses));
            Assert.That(actual.ControlledLosses,
                Is.EqualTo(expected.ControlledLosses));
        }

        private static void AssertArmyEqual(
            SingleCityArmyPersistenceSnapshot expected,
            SingleCityArmyPersistenceSnapshot actual)
        {
            Assert.That(actual.NextUnitOrdinal,
                Is.EqualTo(expected.NextUnitOrdinal));
            Assert.That(actual.Manufacturing, Is.EqualTo(expected.Manufacturing));
            Assert.That(actual.Units, Is.EqualTo(expected.Units));
            AssertCommandEqual(expected.Command, actual.Command);
            Assert.That(actual.LeaderAssigned,
                Is.EqualTo(expected.LeaderAssigned));
            Assert.That(actual.LeaderHealthy,
                Is.EqualTo(expected.LeaderHealthy));
            Assert.That(actual.Losses, Is.EqualTo(expected.Losses));
        }

        private static void AssertExpeditionEqual(
            ArmyExpeditionPersistenceSnapshot expected,
            ArmyExpeditionPersistenceSnapshot actual)
        {
            Assert.That(actual.Status, Is.EqualTo(expected.Status));
            Assert.That(actual.SessionId, Is.EqualTo(expected.SessionId));
            Assert.That(actual.TargetX, Is.EqualTo(expected.TargetX));
            Assert.That(actual.TargetY, Is.EqualTo(expected.TargetY));
            Assert.That(actual.ExpeditionOrdinal,
                Is.EqualTo(expected.ExpeditionOrdinal));
            Assert.That(actual.OutboundDurationSeconds,
                Is.EqualTo(expected.OutboundDurationSeconds));
            Assert.That(actual.ReturnDurationSeconds,
                Is.EqualTo(expected.ReturnDurationSeconds));
            Assert.That(actual.RemainingSeconds,
                Is.EqualTo(expected.RemainingSeconds));
            Assert.That(actual.Units, Is.EqualTo(expected.Units));
            Assert.That(actual.LeaderHealthy, Is.EqualTo(expected.LeaderHealthy));
            Assert.That(actual.Retreating, Is.EqualTo(expected.Retreating));
            Assert.That(actual.EnemyDefinitionIds,
                Is.EqualTo(expected.EnemyDefinitionIds));
            Assert.That(actual.HasResolution,
                Is.EqualTo(expected.HasResolution));
            Assert.That(actual.Victory, Is.EqualTo(expected.Victory));
            Assert.That(actual.ArmyPower, Is.EqualTo(expected.ArmyPower));
            Assert.That(actual.EnemyPower, Is.EqualTo(expected.EnemyPower));
            Assert.That(actual.CasualtyStableUnitIds,
                Is.EqualTo(expected.CasualtyStableUnitIds));
            Assert.That(actual.PendingLoot, Is.EqualTo(expected.PendingLoot));
        }

        private static ArmyExpeditionUnit[] StrongSquad()
        {
            var result = new ArmyExpeditionUnit[12];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new ArmyExpeditionUnit(
                    "core.army-unit." + (index + 1).ToString("D6"),
                    ArmyUnitCatalog.BioMechanicalBehemothId,
                    ArmyUnitCatalog.BioMechanicalBehemoth.MaximumHealth,
                    true);
            }
            return result;
        }

        private static CityResourceStorageModel Stock(
            params (string id, int amount)[] values)
        {
            var storage = new CityResourceStorageModel(
                new ResourceInventory(150));
            foreach ((string id, int amount) in values)
                storage.AddToNetwork(id, amount);
            return storage;
        }
    }
}
