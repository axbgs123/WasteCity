using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class FormalSaveSchema36ContractTests
    {
        [Test]
        public void IDEA0028_CurrentSchemaOwnsNineFateFoundationState()
        {
            Assert.That(FormalSaveEnvelope.CurrentSchemaVersion,
                Is.EqualTo(36));

            var progression = new FormalThreeDProgressionSaveData();
            Assert.That(progression.configurationSignature,
                Is.EqualTo("builtin:progression@2"));
            Assert.That(progression.fate.offerSelectionVersion,
                Is.EqualTo(1));
            Assert.That(progression.quantumEntanglement, Is.Not.Null);
            Assert.That(progression.spatialTemplate, Is.Not.Null);
            Assert.That(progression.localHaste, Is.Not.Null);
            Assert.That(progression.foresightDelay, Is.Not.Null);
            Assert.That(progression.causalTransparency, Is.Not.Null);
            Assert.That(progression.voidChest, Is.Not.Null);
            Assert.That(progression.coordinateLock, Is.Not.Null);
        }

        [Test]
        public void IDEA0028_SchemaThirtyFivePreservesLegacyFateAndAddsCleanState()
        {
            FormalSaveEnvelope historical = LoadMigratedEnvelope();
            historical.saveSchemaVersion = 35;
            historical.formal3D.progression.configurationSignature =
                "builtin:progression@1";
            historical.formal3D.progression.fate.offeredIds = new[]
            {
                FormalFateCatalog.PocketUniverseId,
                FormalFateCatalog.VoidDebtId,
                FormalFateCatalog.RewindAnchorId,
            };
            historical.formal3D.progression.fate.selectedId =
                FormalFateCatalog.VoidDebtId;
            historical.formal3D.progression.fate.level = 1;
            historical.formal3D.progression.fate.revision = 7ul;
            historical.formal3D.progression.fateEffects.voidDebt.debts =
                new[]
                {
                    new FormalThreeDVoidDebtEntrySaveData
                    {
                        resourceId = "core.resource.iron",
                        amount = 4,
                    },
                };
            historical.formal3D.progression.fateEffects.voidDebt.revision =
                3ul;
            ClearSchemaThirtySixFields(historical.formal3D.progression);
            historical.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(
                    historical.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(historical));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalThreeDProgressionSaveData restored =
                decoded.Envelope.formal3D.progression;
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(36));
            Assert.That(restored.fate.offeredIds, Is.EqualTo(
                historical.formal3D.progression.fate.offeredIds));
            Assert.That(restored.fate.selectedId,
                Is.EqualTo(FormalFateCatalog.VoidDebtId));
            Assert.That(restored.fate.level, Is.EqualTo(1));
            Assert.That(restored.fate.revision, Is.EqualTo(7ul));
            Assert.That(restored.fate.offerSelectionVersion, Is.Zero);
            Assert.That(restored.fateEffects.voidDebt.debts[0].amount,
                Is.EqualTo(4));
            Assert.That(restored.fateEffects.voidDebt.revision,
                Is.EqualTo(3ul));
            AssertCleanSchemaThirtySixState(restored);
        }

        [Test]
        public void IDEA0028_CurrentSchemaRoundTripsStateWithoutReordering()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            FormalThreeDProgressionSaveData progression =
                envelope.formal3D.progression;
            progression.quantumEntanglement.committedSynchronizationKeys =
                new[] { "sync.000002", "sync.000001" };
            progression.spatialTemplate.entries = new[]
            {
                new FormalThreeDSpatialTemplateEntrySaveData
                {
                    relativeX = -1,
                    relativeZ = 1,
                    buildingDefinitionId = "core.building.warehouse",
                    quarterTurns = 3,
                },
            };
            progression.localHaste.cycleOrdinal = 2;
            progression.localHaste.remainingBudgetSeconds = 42f;
            progression.localHaste.targetKind = 1;
            progression.localHaste.targetStableId = "production";
            progression.foresightDelay.displayedCycleOrdinals =
                new long[] { 1, 3 };
            progression.foresightDelay.cycleOrdinal = 3;
            progression.causalTransparency.scannedStableEventKeys =
                new[] { "attention.event.000004" };
            progression.voidChest.nextDropOrdinal = 5;
            progression.voidChest.committedDeathEventIds =
                new[] { "enemy.death.000004" };
            progression.coordinateLock.committed = true;
            progression.coordinateLock.stableEventKey =
                CoordinateLockCatalog.StableEventKey;
            progression.coordinateLock.bossPressureScheduled = true;
            progression.coordinateLock.revision = 1ul;
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(envelope));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalThreeDProgressionSaveData restored =
                decoded.Envelope.formal3D.progression;
            Assert.That(restored.quantumEntanglement
                    .committedSynchronizationKeys,
                Is.EqualTo(new[] { "sync.000002", "sync.000001" }));
            Assert.That(restored.spatialTemplate.entries[0].relativeX,
                Is.EqualTo(-1));
            Assert.That(restored.localHaste.remainingBudgetSeconds,
                Is.EqualTo(42f));
            Assert.That(restored.foresightDelay.displayedCycleOrdinals,
                Is.EqualTo(new long[] { 1, 3 }));
            Assert.That(restored.causalTransparency.scannedStableEventKeys,
                Is.EqualTo(new[] { "attention.event.000004" }));
            Assert.That(restored.voidChest.nextDropOrdinal, Is.EqualTo(5));
            Assert.That(restored.coordinateLock.committed, Is.True);
        }

        [Test]
        public void IDEA0028_SchemaThirtyFiveRejectsBadOldHashBeforeMigration()
        {
            FormalSaveEnvelope historical = LoadMigratedEnvelope();
            historical.saveSchemaVersion = 35;
            historical.formal3D.progression.configurationSignature =
                "builtin:progression@1";
            ClearSchemaThirtySixFields(historical.formal3D.progression);
            historical.payloadHashSha256 = new string('0', 64);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(historical));

            Assert.That(decoded.Success, Is.False);
            Assert.That(decoded.Error,
                Is.EqualTo(FormalSaveDecodeError.MalformedJson));
        }

        [Test]
        public void IDEA0028_ValidatorRejectsDuplicateOrUnselectedOffer()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            envelope.formal3D.progression.fate.offeredIds = new[]
            {
                FormalFateCatalog.PocketUniverseId,
                FormalFateCatalog.PocketUniverseId,
                FormalFateCatalog.VoidDebtId,
            };
            envelope.formal3D.progression.fate.selectedId =
                FormalFateCatalog.RewindAnchorId;
            envelope.formal3D.progression.fate.level = 1;

            FormalSaveValidationResult result = ValidateCurrent(envelope);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error,
                Is.EqualTo(FormalSaveValidationError.DuplicateStableId));
        }

        [Test]
        public void IDEA0028_ValidatorRejectsDuplicateTemplateCellAndHalfLock()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            envelope.formal3D.progression.spatialTemplate.entries = new[]
            {
                TemplateEntry(0, 0),
                TemplateEntry(0, 0),
            };
            FormalSaveValidationResult duplicate = ValidateCurrent(envelope);
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(duplicate.Error,
                Is.EqualTo(FormalSaveValidationError.DuplicateStableId));

            envelope.formal3D.progression.spatialTemplate.entries =
                Array.Empty<FormalThreeDSpatialTemplateEntrySaveData>();
            envelope.formal3D.progression.coordinateLock.committed = true;
            FormalSaveValidationResult halfLock = ValidateCurrent(envelope);
            Assert.That(halfLock.IsValid, Is.False);
            Assert.That(halfLock.FieldPath,
                Is.EqualTo(
                    "formal3D.progression.coordinateLock.stableEventKey"));
        }

        [Test]
        public void IDEA0028_CoordinateLockRequiresExactKeyAndBossPressure()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            FormalThreeDProgressionSaveData progression =
                envelope.formal3D.progression;
            progression.coordinateLock.committed = true;
            progression.coordinateLock.stableEventKey = "wrong-lock-key";
            progression.coordinateLock.bossPressureScheduled = true;
            progression.coordinateLock.revision = 1ul;
            progression.pressure.entries = new[]
            {
                new FormalThreeDAttentionPressureEntrySaveData
                {
                    threshold = CoordinateLockCatalog.TargetAttention,
                    state = (int)AttentionPressureState.Queued,
                },
            };
            progression.pressure.revision = 1ul;

            FormalSaveValidationResult wrongKey = ValidateCurrent(envelope);
            Assert.That(wrongKey.IsValid, Is.False);
            Assert.That(wrongKey.FieldPath,
                Is.EqualTo(
                    "formal3D.progression.coordinateLock.stableEventKey"));

            progression.coordinateLock.stableEventKey =
                CoordinateLockCatalog.StableEventKey;
            progression.pressure.entries =
                Array.Empty<FormalThreeDAttentionPressureEntrySaveData>();
            progression.pressure.revision = 0ul;
            FormalSaveValidationResult missingBoss =
                ValidateCurrent(envelope);
            Assert.That(missingBoss.IsValid, Is.False);
            Assert.That(missingBoss.FieldPath,
                Is.EqualTo(
                    "formal3D.progression.coordinateLock.bossPressureScheduled"));

            progression.pressure.entries = new[]
            {
                new FormalThreeDAttentionPressureEntrySaveData
                {
                    threshold = CoordinateLockCatalog.TargetAttention,
                    state = (int)AttentionPressureState.Queued,
                },
            };
            progression.pressure.revision = 1ul;
            Assert.That(ValidateCurrent(envelope).IsValid, Is.True);
        }

        [Test]
        public void IDEA0028_ValidatorRejectsHasteKindTargetMismatch()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            envelope.formal3D.progression.localHaste.targetKind = 2;
            envelope.formal3D.progression.localHaste.targetStableId =
                "production";

            FormalSaveValidationResult result = ValidateCurrent(envelope);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FieldPath,
                Is.EqualTo(
                    "formal3D.progression.localHaste.targetStableId"));
        }

        [Test]
        public void IDEA0028_ValidatorRejectsDanglingForesightPlan()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            envelope.formal3D.progression.foresightDelay.revision = 2ul;
            envelope.formal3D.progression.foresightDelay.cycleOrdinal = 1;
            envelope.formal3D.progression.foresightDelay
                .plannedStableEventId = "event.not-in-pressure";
            envelope.formal3D.progression.foresightDelay
                .remainingDisplaySeconds = 3f;
            envelope.formal3D.progression.foresightDelay
                .displayedCycleOrdinals = new long[] { 1 };

            FormalSaveValidationResult result = ValidateCurrent(envelope);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FieldPath,
                Is.EqualTo(
                    "formal3D.progression.foresightDelay." +
                    "plannedStableEventId"));
        }

        [Test]
        public void IDEA0028_ValidatorAcceptsQueuedAuthoritativeForesightPlan()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            AttentionPressureDefinition pressure =
                AttentionPressureCatalog.FindByThreshold(30);
            envelope.formal3D.progression.pressure.entries = new[]
            {
                new FormalThreeDAttentionPressureEntrySaveData
                {
                    threshold = pressure.Threshold,
                    state = (int)AttentionPressureState.Queued,
                    warningRemainingSeconds = 0f,
                },
            };
            envelope.formal3D.progression.foresightDelay.revision = 2ul;
            envelope.formal3D.progression.foresightDelay.cycleOrdinal = 1;
            envelope.formal3D.progression.foresightDelay
                .plannedStableEventId = pressure.EncounterId.Value;
            envelope.formal3D.progression.foresightDelay
                .remainingDisplaySeconds = 3f;
            envelope.formal3D.progression.foresightDelay
                .displayedCycleOrdinals = new long[] { 1 };

            FormalSaveValidationResult result = ValidateCurrent(envelope);

            Assert.That(result.IsValid, Is.True, result.Message);
        }

        private static FormalThreeDSpatialTemplateEntrySaveData TemplateEntry(
            int x,
            int z)
        {
            return new FormalThreeDSpatialTemplateEntrySaveData
            {
                relativeX = x,
                relativeZ = z,
                buildingDefinitionId = "core.building.warehouse",
                quarterTurns = 0,
            };
        }

        private static FormalSaveValidationResult ValidateCurrent(
            FormalSaveEnvelope envelope)
        {
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
            return FormalSaveValidator.ValidateEnvelope(envelope);
        }

        private static void ClearSchemaThirtySixFields(
            FormalThreeDProgressionSaveData progression)
        {
            progression.quantumEntanglement = null;
            progression.spatialTemplate = null;
            progression.localHaste = null;
            progression.foresightDelay = null;
            progression.causalTransparency = null;
            progression.voidChest = null;
            progression.coordinateLock = null;
        }

        private static void AssertCleanSchemaThirtySixState(
            FormalThreeDProgressionSaveData restored)
        {
            Assert.That(restored.quantumEntanglement
                .committedSynchronizationKeys, Is.Empty);
            Assert.That(restored.spatialTemplate.entries, Is.Empty);
            Assert.That(restored.localHaste.cycleOrdinal, Is.Zero);
            Assert.That(restored.localHaste.remainingBudgetSeconds,
                Is.EqualTo(60f));
            Assert.That(restored.localHaste.targetKind, Is.Zero);
            Assert.That(restored.localHaste.targetStableId, Is.Empty);
            Assert.That(restored.foresightDelay.plannedStableEventId,
                Is.Empty);
            Assert.That(restored.foresightDelay.displayedCycleOrdinals,
                Is.Empty);
            Assert.That(restored.causalTransparency
                .scannedStableEventKeys, Is.Empty);
            Assert.That(restored.voidChest.nextDropOrdinal, Is.EqualTo(1));
            Assert.That(restored.voidChest.pendingChests, Is.Empty);
            Assert.That(restored.voidChest.committedDeathEventIds, Is.Empty);
            Assert.That(restored.voidChest.claimedRewardKeys, Is.Empty);
            Assert.That(restored.coordinateLock.committed, Is.False);
            Assert.That(restored.coordinateLock.stableEventKey, Is.Empty);
            Assert.That(restored.coordinateLock.bossPressureScheduled,
                Is.False);
        }

        private static FormalSaveEnvelope LoadMigratedEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            return decoded.Envelope;
        }

        private static string ReadFixture(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Tests/Fixtures/Persistence",
                fileName));
        }
    }
}
