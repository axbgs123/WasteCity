using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalProgressionSaveAdapterTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxFormalProgressionSaveAdapter3D, WasteCity.Game";
        private const string PlanTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxFormalProgressionRestorePlan3D, WasteCity.Game";

        private static readonly string[] FateIds =
        {
            "core.legacy.pocket-universe",
            "core.legacy.void-debt",
            "core.legacy.rewind-anchor",
        };

        [Test]
        public void IDEA0020_AdapterRequiresAllExplicitProgressionOwners()
        {
            Type adapter = RequireType(AdapterTypeName);
            ConstructorInfo constructor = adapter.GetConstructor(new[]
            {
                typeof(FormalAttentionRuntime),
                typeof(FormalFateRuntime),
                typeof(PocketUniverseFateEffect),
                typeof(FormalVoidDebtRuntime),
                typeof(FormalRewindAnchorMetadataRuntime),
                RequireType(
                    "WasteCity.Graybox3D.Building." +
                    "GrayboxAttentionPressureSaveAdapter3D, WasteCity.Game"),
                typeof(FormalCivilizationAscensionRuntime),
                typeof(AdvancementSequenceModel),
            });
            Assert.That(constructor, Is.Not.Null);
            Assert.That(adapter.GetConstructors(), Has.Length.EqualTo(2));
            Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke(new object[]
                {
                    null,
                    new FormalFateRuntime(),
                    new PocketUniverseFateEffect(),
                    new FormalVoidDebtRuntime(),
                    new FormalRewindAnchorMetadataRuntime(),
                    null,
                    null,
                    null,
                }));
            Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke(new object[]
                {
                    new FormalAttentionRuntime(),
                    null,
                    new PocketUniverseFateEffect(),
                    new FormalVoidDebtRuntime(),
                    new FormalRewindAnchorMetadataRuntime(),
                    null,
                    null,
                    null,
                }));
            Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke(new object[]
                {
                    new FormalAttentionRuntime(),
                    new FormalFateRuntime(),
                    null,
                    new FormalVoidDebtRuntime(),
                    new FormalRewindAnchorMetadataRuntime(),
                    null,
                    null,
                    null,
                }));
            Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke(new object[]
                {
                    new FormalAttentionRuntime(),
                    new FormalFateRuntime(),
                    new PocketUniverseFateEffect(),
                    null,
                    new FormalRewindAnchorMetadataRuntime(),
                    null,
                    null,
                    null,
                }));
            Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke(new object[]
                {
                    new FormalAttentionRuntime(),
                    new FormalFateRuntime(),
                    new PocketUniverseFateEffect(),
                    new FormalVoidDebtRuntime(),
                    null,
                    null,
                    null,
                    null,
                }));
        }

        [Test]
        public void IDEA0028_SevenNewOwnersRoundTripThroughSchemaThirtySix()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.adapter",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);
            PopulateIdea0028Owners(
                pressure, quantum, spatial, haste, foresight, causal, chests,
                coordinate);
            var source = CreateIdea0028Adapter(
                attention, pressure, quantum, spatial, haste, foresight,
                causal, chests, coordinate);

            FormalThreeDProgressionSaveData saved = source.Capture();
            Assert.That(saved.quantumEntanglement
                    .committedSynchronizationKeys,
                Does.Contain(
                    QuantumEntanglementRuntime.FirstSynchronizationKey));
            Assert.That(saved.quantumEntanglement
                    .committedSynchronizationKeys,
                Has.None.EqualTo("core.resource.iron")
                    .And.None.EqualTo("core.resource.water"));
            Assert.That(saved.spatialTemplate.entries, Has.Length.EqualTo(2));
            Assert.That(saved.localHaste.active, Is.True);
            Assert.That(saved.localHaste.cycleOrdinal, Is.EqualTo(2L));
            Assert.That(saved.foresightDelay.cycleOrdinal, Is.EqualTo(4L));
            Assert.That(saved.foresightDelay.remainingDisplaySeconds,
                Is.EqualTo(1.75f).Within(.0001f));
            Assert.That(saved.causalTransparency.scannedStableEventKeys,
                Is.Not.Empty);
            Assert.That(saved.voidChest.committedDeathEventIds,
                Has.Length.EqualTo(2));
            Assert.That(saved.coordinateLock.committed, Is.True);

            var targetAttention = new FormalAttentionRuntime();
            var targetPressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                targetAttention,
                targetPressure,
                "session.adapter",
                out QuantumEntanglementRuntime targetQuantum,
                out SpatialTemplateRuntime targetSpatial,
                out LocalHasteRuntime targetHaste,
                out ForesightDelayRuntime targetForesight,
                out CausalTransparencyRuntime targetCausal,
                out VoidChestRuntime targetChests,
                out CoordinateLockRuntime targetCoordinate);
            var target = CreateIdea0028Adapter(
                targetAttention, targetPressure, targetQuantum,
                targetSpatial, targetHaste, targetForesight, targetCausal,
                targetChests, targetCoordinate);

            Assert.That(target.TryRestore(saved, out string error),
                Is.True, error);
            Assert.That(targetQuantum.Capture().Connected, Is.False);
            Assert.That(targetQuantum.Capture().SharedResourceIds,
                Is.EqualTo(quantum.Capture().SharedResourceIds));
            Assert.That(targetQuantum.Capture().CommittedSynchronizationKeys,
                Is.EqualTo(
                    quantum.Capture().CommittedSynchronizationKeys));
            Assert.That(targetSpatial.Capture().Templates.Single().Id,
                Is.EqualTo(GrayboxFormalProgressionSaveAdapter3D
                    .FormalSpatialTemplateSlotId));
            Assert.That(targetSpatial.Capture().Templates.Single().Cells
                    .Select(value =>
                        (value.X, value.Y, value.BuildingDefinitionId,
                            value.RotationQuarterTurns)),
                Is.EqualTo(spatial.Capture().Templates.Single().Cells
                    .Select(value =>
                        (value.X, value.Y, value.BuildingDefinitionId,
                            value.RotationQuarterTurns))));
            Assert.That(targetHaste.Capture().TargetId,
                Is.EqualTo(haste.Capture().TargetId));
            Assert.That(targetHaste.Capture().RemainingBudgetSeconds,
                Is.EqualTo(haste.Capture().RemainingBudgetSeconds));
            Assert.That(targetHaste.Capture().CurrentCycleOrdinal,
                Is.EqualTo(2ul));
            Assert.That(targetForesight.Capture().LastConsumedCycleOrdinal,
                Is.EqualTo(4ul));
            Assert.That(targetForesight.Capture().CurrentCycleOrdinal,
                Is.EqualTo(4ul));
            Assert.That(targetForesight.Capture().LastProjection.EventId,
                Is.EqualTo(AttentionPressureCatalog.FindByThreshold(90)
                    .EncounterId.Value));
            Assert.That(targetForesight.Capture().DisplayRemainingSeconds,
                Is.EqualTo(1.75f).Within(.0001f));
            Assert.That(targetCausal.Capture().FullReasonAccess, Is.True);
            Assert.That(targetChests.Capture().Evaluations.Select(value =>
                    (value.DeathId, value.SequenceOrdinal, value.Dropped,
                        value.Claimed, value.ResourceId, value.Amount,
                        value.NarrativeFragmentId)),
                Is.EqualTo(chests.Capture().Evaluations.Select(value =>
                    (value.DeathId, value.SequenceOrdinal, value.Dropped,
                        value.Claimed, value.ResourceId, value.Amount,
                        value.NarrativeFragmentId))));
            Assert.That(targetCoordinate.Capture().Committed, Is.True);
        }

        [Test]
        public void IDEA0028_CenteredTemplateEdgesCaptureIntoValidEnvelope()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.centered-template",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);
            Assert.That(spatial.TryPrepareRecord(
                GrayboxFormalProgressionSaveAdapter3D
                    .FormalSpatialTemplateSlotId,
                new[]
                {
                    new SpatialTemplateCell(
                        -1, -1, BuildingCatalog.Wall.Id.Value, 0),
                    new SpatialTemplateCell(
                        1, 1, BuildingCatalog.Warehouse.Id.Value, 3),
                },
                out SpatialTemplateRecordPlan record,
                out string error), Is.True, error);
            Assert.That(spatial.TryCommit(record, out error), Is.True, error);
            var adapter = CreateIdea0028Adapter(
                attention,
                pressure,
                quantum,
                spatial,
                haste,
                foresight,
                causal,
                chests,
                coordinate);
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "_Game",
                    "Tests",
                    "Fixtures",
                    "Persistence",
                    "schema-31-formal-3d.json")));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveEnvelope envelope = decoded.Envelope;
            envelope.formal3D.progression = adapter.Capture();
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);

            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(validation.IsValid, Is.True, validation.Message);
            Assert.That(envelope.formal3D.progression.spatialTemplate.entries
                    .Select(value => (value.relativeX, value.relativeZ)),
                Is.EqualTo(new[] { (-1, -1), (1, 1) }));
        }

        [Test]
        public void IDEA0028_InvalidCycleOrdinalsFailDuringZeroWritePrepare()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.invalid-cycle",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);
            var adapter = CreateIdea0028Adapter(
                attention, pressure, quantum, spatial, haste, foresight,
                causal, chests, coordinate);
            FormalThreeDProgressionSaveData invalid = adapter.Capture();
            invalid.localHaste.cycleOrdinal = -1L;
            LocalHasteSnapshot hasteBefore = haste.Capture();
            ForesightDelaySnapshot foresightBefore = foresight.Capture();

            Assert.That(adapter.TryPrepareRestore(
                invalid,
                out _,
                out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(haste.Capture(), Is.SameAs(hasteBefore));
            Assert.That(foresight.Capture(), Is.SameAs(foresightBefore));

            invalid = adapter.Capture();
            invalid.foresightDelay.cycleOrdinal = 2L;
            invalid.foresightDelay.displayedCycleOrdinals = new[] { 3L };
            Assert.That(adapter.TryPrepareRestore(
                invalid, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(haste.Capture(), Is.SameAs(hasteBefore));
            Assert.That(foresight.Capture(), Is.SameAs(foresightBefore));
        }

        [Test]
        public void IDEA0028_HasteKindsCaptureAndRestoreByFormalDomain()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.haste-kind",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);
            Assert.That(haste.TrySelectTarget("research", out string error),
                Is.True, error);
            GrayboxFormalProgressionSaveAdapter3D adapter =
                CreateIdea0028Adapter(
                    attention,
                    pressure,
                    quantum,
                    spatial,
                    haste,
                    foresight,
                    causal,
                    chests,
                    coordinate);

            FormalThreeDProgressionSaveData saved = adapter.Capture();

            Assert.That(saved.localHaste.targetKind, Is.EqualTo(2));
            Assert.That(saved.localHaste.targetStableId,
                Is.EqualTo("research"));
            Assert.That(adapter.TryPrepareRestore(
                saved, out _, out error), Is.True, error);

            saved.localHaste.targetKind = 1;
            Assert.That(adapter.TryPrepareRestore(
                saved, out _, out error), Is.False);
            saved.localHaste.targetKind = 2;
            saved.localHaste.targetStableId = "building:research-01";
            Assert.That(adapter.TryPrepareRestore(
                saved, out _, out error), Is.False);
        }

        [Test]
        public void IDEA0028_AdapterRejectsDanglingForesightPlan()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.dangling-foresight",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);
            GrayboxFormalProgressionSaveAdapter3D adapter =
                CreateIdea0028Adapter(
                    attention,
                    pressure,
                    quantum,
                    spatial,
                    haste,
                    foresight,
                    causal,
                    chests,
                    coordinate);
            FormalThreeDProgressionSaveData invalid = adapter.Capture();
            invalid.foresightDelay.revision = 2ul;
            invalid.foresightDelay.cycleOrdinal = 1;
            invalid.foresightDelay.plannedStableEventId =
                "event.not-in-pressure";
            invalid.foresightDelay.remainingDisplaySeconds = 3f;
            invalid.foresightDelay.displayedCycleOrdinals =
                new long[] { 1 };

            Assert.That(adapter.TryPrepareRestore(
                invalid, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("压力"));
        }

        [Test]
        public void IDEA0028_AdapterRejectsCoordinateLockWithoutBossPressure()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.coordinate-half-commit",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);
            GrayboxFormalProgressionSaveAdapter3D adapter =
                CreateIdea0028Adapter(
                    attention,
                    pressure,
                    quantum,
                    spatial,
                    haste,
                    foresight,
                    causal,
                    chests,
                    coordinate);
            FormalThreeDProgressionSaveData invalid = adapter.Capture();
            invalid.coordinateLock.committed = true;
            invalid.coordinateLock.stableEventKey =
                CoordinateLockCatalog.StableEventKey;
            invalid.coordinateLock.bossPressureScheduled = true;
            invalid.coordinateLock.revision = 1ul;

            Assert.That(adapter.TryPrepareRestore(
                invalid, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("90").And.Contain("压力"));
        }

        [Test]
        public void IDEA0028_DownstreamPressureFailureRollsBackSevenOwners()
        {
            var sourceAttention = new FormalAttentionRuntime();
            var sourcePressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                sourceAttention,
                sourcePressure,
                "session.rollback",
                out QuantumEntanglementRuntime sourceQuantum,
                out SpatialTemplateRuntime sourceSpatial,
                out LocalHasteRuntime sourceHaste,
                out ForesightDelayRuntime sourceForesight,
                out CausalTransparencyRuntime sourceCausal,
                out VoidChestRuntime sourceChests,
                out CoordinateLockRuntime sourceCoordinate);
            PopulateIdea0028Owners(
                sourcePressure, sourceQuantum, sourceSpatial, sourceHaste,
                sourceForesight, sourceCausal, sourceChests,
                sourceCoordinate);
            FormalThreeDProgressionSaveData saved = CreateIdea0028Adapter(
                sourceAttention, sourcePressure, sourceQuantum, sourceSpatial,
                sourceHaste, sourceForesight, sourceCausal, sourceChests,
                sourceCoordinate).Capture();

            var targetAttention = new FormalAttentionRuntime();
            var targetPressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                targetAttention,
                targetPressure,
                "session.rollback",
                out QuantumEntanglementRuntime targetQuantum,
                out SpatialTemplateRuntime targetSpatial,
                out LocalHasteRuntime targetHaste,
                out ForesightDelayRuntime targetForesight,
                out CausalTransparencyRuntime targetCausal,
                out VoidChestRuntime targetChests,
                out CoordinateLockRuntime targetCoordinate);
            var target = CreateIdea0028Adapter(
                targetAttention, targetPressure, targetQuantum,
                targetSpatial, targetHaste, targetForesight, targetCausal,
                targetChests, targetCoordinate);
            Assert.That(target.TryPrepareRestore(
                saved,
                out GrayboxFormalProgressionRestorePlan3D plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(targetPressure.TryQueueThreshold(60, out _), Is.True);
            string before = Idea0028Fingerprint(
                targetQuantum, targetSpatial, targetHaste, targetForesight,
                targetCausal, targetChests, targetCoordinate);

            Assert.That(target.TryCommitRestore(plan, out _), Is.False);
            Assert.That(Idea0028Fingerprint(
                    targetQuantum, targetSpatial, targetHaste,
                    targetForesight, targetCausal, targetChests,
                    targetCoordinate),
                Is.EqualTo(before));
        }

        [Test]
        public void IDEA0028_CleanAdapterCanRebindSessionScopedOwners()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                attention,
                new FormalFateRuntime(),
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                new GrayboxAttentionPressureSaveAdapter3D(
                    pressure,
                    new GrayboxDefenseRuntime3D(0f, 0f, 20, 0f)));
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.configured",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);

            Assert.That(adapter.ConfigureIdea0028Owners(
                quantum, spatial, haste, foresight, causal, chests,
                coordinate, out string error), Is.True, error);
            var replacementQuantum =
                new QuantumEntanglementRuntime(new string[0]);
            Assert.That(adapter.ConfigureIdea0028Owners(
                replacementQuantum,
                new SpatialTemplateRuntime(),
                new LocalHasteRuntime(),
                new ForesightDelayRuntime(),
                new CausalTransparencyRuntime(),
                new VoidChestRuntime("session.replacement", 1),
                new CoordinateLockRuntime(attention, pressure),
                out error), Is.True, error);
            Assert.That(replacementQuantum.TrySetConnected(false), Is.True);
            Assert.That(adapter.ConfigureIdea0028Owners(
                new QuantumEntanglementRuntime(new string[0]),
                new SpatialTemplateRuntime(),
                new LocalHasteRuntime(),
                new ForesightDelayRuntime(),
                new CausalTransparencyRuntime(),
                new VoidChestRuntime("session.forbidden", 1),
                new CoordinateLockRuntime(attention, pressure),
                out _), Is.False,
                "Dirty session truth cannot be replaced.");
        }

        [Test]
        public void IDEA0028_QuantumDtoNeverSerializesFixedResourceConfigAsEvents()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            CreateIdea0028Owners(
                attention,
                pressure,
                "session.quantum.dto",
                out QuantumEntanglementRuntime quantum,
                out SpatialTemplateRuntime spatial,
                out LocalHasteRuntime haste,
                out ForesightDelayRuntime foresight,
                out CausalTransparencyRuntime causal,
                out VoidChestRuntime chests,
                out CoordinateLockRuntime coordinate);
            GrayboxFormalProgressionSaveAdapter3D adapter =
                CreateIdea0028Adapter(
                    attention,
                    pressure,
                    quantum,
                    spatial,
                    haste,
                    foresight,
                    causal,
                    chests,
                    coordinate);

            Assert.That(adapter.Capture().quantumEntanglement
                .committedSynchronizationKeys, Is.Empty);
            Assert.That(quantum.TryCommitSynchronization(
                QuantumEntanglementRuntime.FirstSynchronizationKey), Is.True);
            Assert.That(adapter.Capture().quantumEntanglement
                    .committedSynchronizationKeys,
                Is.EqualTo(new[]
                {
                    QuantumEntanglementRuntime.FirstSynchronizationKey,
                }));
        }

        [Test]
        public void IDEA0020_CaptureMapsBothRuntimesToIndependentSchema33Dto()
        {
            var attention = new FormalAttentionRuntime();
            Assert.That(attention.TryApply(
                "core.attention.fate.first-activation",
                "fate-selection-complete",
                out string attentionError), Is.True, attentionError);
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FateIds[1],
                out _,
                out _,
                out string fateError), Is.True, fateError);
            object adapter = CreateAdapter(attention, fate);

            FormalThreeDProgressionSaveData first = Capture(adapter);
            Assert.That(first.configurationSignature,
                Is.EqualTo("builtin:progression@2"));
            Assert.That(first.attention.value, Is.EqualTo(15));
            Assert.That(first.attention.revision, Is.EqualTo(1ul));
            Assert.That(first.attention.history, Has.Length.EqualTo(1));
            Assert.That(first.attention.history[0].reasonId,
                Is.EqualTo("core.attention.fate.first-activation"));
            Assert.That(first.attention.history[0].stableEventKey,
                Is.EqualTo("fate-selection-complete"));
            Assert.That(first.attention.committedStableEventKeys,
                Is.EqualTo(new[] { "fate-selection-complete" }));
            Assert.That(first.attention.completedOneShotReasonIds,
                Is.EqualTo(new[]
                {
                    "core.attention.fate.first-activation",
                }));
            Assert.That(first.fate.offeredIds, Is.EqualTo(FateIds));
            Assert.That(first.fate.selectedId, Is.EqualTo(FateIds[1]));
            Assert.That(first.fate.level, Is.EqualTo(1));
            Assert.That(first.fate.revision, Is.EqualTo(1ul));
            Assert.That(first.civilization.level, Is.EqualTo(1));

            first.attention.history[0].reasonId = "mutated.reason.id";
            first.attention.committedStableEventKeys[0] = "mutated.event.key";
            first.fate.offeredIds[0] = FateIds[2];
            FormalThreeDProgressionSaveData second = Capture(adapter);
            Assert.That(second.attention.history[0].reasonId,
                Is.EqualTo("core.attention.fate.first-activation"));
            Assert.That(second.attention.committedStableEventKeys[0],
                Is.EqualTo("fate-selection-complete"));
            Assert.That(second.fate.offeredIds, Is.EqualTo(FateIds));
        }

        [Test]
        public void IDEA0020_PendingCivilizationOwnerSurvivesFreshRestore()
        {
            var fate = new FormalFateRuntime();
            var civilization = new FormalCivilizationAscensionRuntime();
            var sequence = new AdvancementSequenceModel();
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                new FormalAttentionRuntime(),
                fate,
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                null,
                civilization,
                sequence);

            Assert.That(adapter.TryRestore(
                new FormalThreeDProgressionSaveData(),
                out string error), Is.True, error);
            Assert.That(civilization.Capture().FateId, Is.Empty);
            Assert.That(civilization.Capture().FateLevel, Is.Zero);
            Assert.That(sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.None));
            FormalThreeDProgressionSaveData captured = adapter.Capture();
            Assert.That(captured.civilization.level, Is.EqualTo(1));
            Assert.That(captured.civilization.ascensionCompleted, Is.False);
        }

        [Test]
        public void IDEA0020_SameProcessSelectedOwnersResetToFreshProgress()
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.PocketUniverseId,
                out _, out _, out string error), Is.True, error);
            var civilization = new FormalCivilizationAscensionRuntime(
                FormalFateCatalog.PocketUniverseId);
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                new FormalAttentionRuntime(),
                fate,
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                null,
                civilization,
                new AdvancementSequenceModel());

            Assert.That(adapter.TryRestore(
                new FormalThreeDProgressionSaveData(),
                out error), Is.True, error);
            Assert.That(fate.Capture().HasSelection, Is.False);
            Assert.That(civilization.Capture().FateId, Is.Empty);
            Assert.That(civilization.Capture().FateLevel, Is.Zero);
        }

        [Test]
        public void IDEA0020_SameProcessAscendedOwnersResetToFreshProgress()
        {
            CreateLevelTwoOwners(
                FormalFateCatalog.RewindAnchorId,
                out FormalAttentionRuntime attention,
                out FormalFateRuntime fate,
                out PocketUniverseFateEffect pocket,
                out FormalVoidDebtRuntime debt,
                out FormalRewindAnchorMetadataRuntime rewind,
                out FormalCivilizationAscensionRuntime civilization,
                out AdvancementSequenceModel sequence);
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                attention, fate, pocket, debt, rewind, null,
                civilization, sequence);

            Assert.That(adapter.TryRestore(
                new FormalThreeDProgressionSaveData(),
                out string error), Is.True, error);
            Assert.That(fate.Capture().HasSelection, Is.False);
            Assert.That(civilization.Capture().FateId, Is.Empty);
            Assert.That(civilization.Capture().Ascended, Is.False);
            Assert.That(sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.None));
            Assert.That(rewind.MaximumAnchors,
                Is.EqualTo(
                    FormalRewindAnchorMetadataRuntime
                        .MaximumAnchorsAtLevelOne));
            Assert.That(rewind.Capture().Entries, Is.Empty);
        }

        [Test]
        public void IDEA0020_PressureAdapterIsSixthAtomicProgressionOwner()
        {
            var pressure = new AttentionPressureRuntime();
            var pressureAdapter = new GrayboxAttentionPressureSaveAdapter3D(
                pressure,
                new GrayboxDefenseRuntime3D(0f, 0f, 20, 0f));
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                new FormalAttentionRuntime(),
                new FormalFateRuntime(),
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                pressureAdapter);
            FormalThreeDProgressionSaveData data = adapter.Capture();
            data.pressure.entries = new[]
            {
                new FormalThreeDAttentionPressureEntrySaveData
                {
                    threshold = 30,
                    state = (int)AttentionPressureState.Queued,
                },
            };
            data.pressure.revision = 1ul;
            AttentionPressureSnapshot before = pressure.Capture();
            Assert.That(adapter.TryPrepareRestore(
                data,
                out GrayboxFormalProgressionRestorePlan3D plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(pressure.Capture(), Is.SameAs(before));
            Assert.That(adapter.TryCommitRestore(plan, out string error),
                Is.True, error);
            Assert.That(pressure.Capture().Entries.Single().Threshold,
                Is.EqualTo(30));
        }

        [TestCase("core.legacy.pocket-universe")]
        [TestCase("core.legacy.void-debt")]
        [TestCase("core.legacy.rewind-anchor")]
        public void IDEA0020_LevelTwoOwnersAndAscensionSequenceRoundTrip(
            string fateId)
        {
            CreateLevelTwoOwners(
                fateId,
                out FormalAttentionRuntime attention,
                out FormalFateRuntime fate,
                out PocketUniverseFateEffect pocket,
                out FormalVoidDebtRuntime debt,
                out FormalRewindAnchorMetadataRuntime rewind,
                out FormalCivilizationAscensionRuntime civilization,
                out AdvancementSequenceModel sequence);
            var pressure = new AttentionPressureRuntime();
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                attention,
                fate,
                pocket,
                debt,
                rewind,
                new GrayboxAttentionPressureSaveAdapter3D(
                    pressure,
                    new GrayboxDefenseRuntime3D(0f, 0f, 20, 0f)),
                civilization,
                sequence);

            FormalThreeDProgressionSaveData data = adapter.Capture();
            Assert.That(data.fate.level, Is.EqualTo(2));
            Assert.That(data.civilization.level, Is.EqualTo(2));
            Assert.That(data.civilization.revision, Is.EqualTo(1ul));
            Assert.That(data.civilization.ascensionCompleted, Is.True);
            Assert.That(data.civilization.ascensionId,
                Is.EqualTo("first-civilization-ascension"));
            Assert.That(data.civilization.sequenceStage,
                Is.EqualTo((int)AdvancementSequenceStage.Scanning));
            Assert.That(data.civilization.remainingRuleSeconds,
                Is.EqualTo(1.25f));
            Assert.That(data.civilization.committedAscensionIds,
                Is.EqualTo(new[] { "first-civilization-ascension" }));
            if (fateId == FormalFateCatalog.PocketUniverseId)
                Assert.That(data.fateEffects.pocketUniverse.level,
                    Is.EqualTo(2));
            else if (fateId == FormalFateCatalog.VoidDebtId)
                Assert.That(data.fateEffects.voidDebt.level, Is.EqualTo(2));
            else
                Assert.That(data.fateEffects.rewindAnchors.anchors,
                    Has.Length.EqualTo(2));

            CreateLevelTwoTargets(
                fateId,
                out FormalAttentionRuntime targetAttention,
                out FormalFateRuntime targetFate,
                out PocketUniverseFateEffect targetPocket,
                out FormalVoidDebtRuntime targetDebt,
                out FormalRewindAnchorMetadataRuntime targetRewind,
                out FormalCivilizationAscensionRuntime targetCivilization,
                out AdvancementSequenceModel targetSequence);
            var targetPressure = new AttentionPressureRuntime();
            var target = new GrayboxFormalProgressionSaveAdapter3D(
                targetAttention,
                targetFate,
                targetPocket,
                targetDebt,
                targetRewind,
                new GrayboxAttentionPressureSaveAdapter3D(
                    targetPressure,
                    new GrayboxDefenseRuntime3D(0f, 0f, 20, 0f)),
                targetCivilization,
                targetSequence);
            Assert.That(target.TryRestore(data, out string error), Is.True,
                error);
            Assert.That(targetFate.Capture().Level, Is.EqualTo(2));
            Assert.That(targetCivilization.Capture().Ascended, Is.True);
            Assert.That(targetSequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.Scanning));
            Assert.That(targetSequence.Remaining, Is.EqualTo(1.25f));
            Assert.That(targetRewind.Capture().Entries.Count,
                Is.EqualTo(fateId == FormalFateCatalog.RewindAnchorId ? 2 : 0));
        }

        [Test]
        public void IDEA0020_DefaultLevelOneOwnerRestoresLevelTwoDoubleSlots()
        {
            CreateLevelTwoOwners(
                FormalFateCatalog.RewindAnchorId,
                out FormalAttentionRuntime attention,
                out FormalFateRuntime fate,
                out PocketUniverseFateEffect pocket,
                out FormalVoidDebtRuntime debt,
                out FormalRewindAnchorMetadataRuntime rewind,
                out FormalCivilizationAscensionRuntime civilization,
                out AdvancementSequenceModel sequence);
            var source = new GrayboxFormalProgressionSaveAdapter3D(
                attention,
                fate,
                pocket,
                debt,
                rewind,
                null,
                civilization,
                sequence);
            FormalThreeDProgressionSaveData data = source.Capture();

            var targetFate = new FormalFateRuntime();
            var targetRewind = new FormalRewindAnchorMetadataRuntime();
            var target = new GrayboxFormalProgressionSaveAdapter3D(
                new FormalAttentionRuntime(),
                targetFate,
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                targetRewind,
                null,
                new FormalCivilizationAscensionRuntime(
                    FormalFateCatalog.RewindAnchorId),
                new AdvancementSequenceModel());
            Assert.That(target.TryRestore(data, out string error), Is.True,
                error);
            Assert.That(targetFate.Capture().Level, Is.EqualTo(2));
            Assert.That(targetRewind.MaximumAnchors,
                Is.EqualTo(
                    FormalRewindAnchorMetadataRuntime.MaximumAnchorsAtLevelTwo));
            Assert.That(targetRewind.Capture().Entries, Has.Count.EqualTo(2));
        }

        private static void CreateLevelTwoOwners(
            string fateId,
            out FormalAttentionRuntime attention,
            out FormalFateRuntime fate,
            out PocketUniverseFateEffect pocket,
            out FormalVoidDebtRuntime debt,
            out FormalRewindAnchorMetadataRuntime rewind,
            out FormalCivilizationAscensionRuntime civilization,
            out AdvancementSequenceModel sequence)
        {
            attention = new FormalAttentionRuntime();
            fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(fateId, out _, out _, out string error),
                Is.True, error);
            Assert.That(fate.TryPromoteToLevelTwo(out error), Is.True, error);
            pocket = new PocketUniverseFateEffect();
            debt = fateId == FormalFateCatalog.VoidDebtId
                ? new FormalVoidDebtRuntime(2)
                : new FormalVoidDebtRuntime();
            rewind = fateId == FormalFateCatalog.RewindAnchorId
                ? new FormalRewindAnchorMetadataRuntime(2)
                : new FormalRewindAnchorMetadataRuntime();
            if (fateId == FormalFateCatalog.PocketUniverseId)
                Assert.That(pocket.TrySetLevel(2, out error), Is.True, error);
            if (fateId == FormalFateCatalog.RewindAnchorId)
            {
                AddAnchor(rewind, "anchor.one", 1f);
                AddAnchor(rewind, "anchor.two", 2f);
            }
            civilization = new FormalCivilizationAscensionRuntime(fateId);
            Assert.That(civilization.TryRestore(
                new FormalCivilizationAscensionSnapshot(
                    2, fateId, 2, true, 1ul), out error), Is.True, error);
            sequence = new AdvancementSequenceModel();
            sequence.Restore((int)AdvancementSequenceStage.Scanning, 1.25f);
        }

        private static void CreateLevelTwoTargets(
            string fateId,
            out FormalAttentionRuntime attention,
            out FormalFateRuntime fate,
            out PocketUniverseFateEffect pocket,
            out FormalVoidDebtRuntime debt,
            out FormalRewindAnchorMetadataRuntime rewind,
            out FormalCivilizationAscensionRuntime civilization,
            out AdvancementSequenceModel sequence)
        {
            attention = new FormalAttentionRuntime();
            fate = new FormalFateRuntime();
            pocket = new PocketUniverseFateEffect();
            debt = new FormalVoidDebtRuntime();
            rewind = fateId == FormalFateCatalog.RewindAnchorId
                ? new FormalRewindAnchorMetadataRuntime(2)
                : new FormalRewindAnchorMetadataRuntime();
            civilization = new FormalCivilizationAscensionRuntime(fateId);
            sequence = new AdvancementSequenceModel();
        }

        private static void AddAnchor(
            FormalRewindAnchorMetadataRuntime rewind,
            string id,
            float ruleTime)
        {
            Assert.That(rewind.TryPrepareUpsert(
                id,
                "internal." + id,
                "session.000001",
                new string('a', 64),
                new FormalSaveCheckpointMetadata
                {
                    sequence = (long)ruleTime,
                    reasonId = FormalSaveCheckpointReasonIds.NewGameReady,
                    ruleTimeSeconds = ruleTime,
                    completedMilestoneIds = Array.Empty<string>(),
                },
                out FormalRewindAnchorMetadataUpsertPlan plan,
                out string error), Is.True, error);
            Assert.That(rewind.TryCommitUpsert(plan, out error), Is.True,
                error);
        }

        [Test]
        public void IDEA0020_PrepareIsZeroWriteAndDeepCopiesUnknownHistory()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object adapter = CreateAdapter(attention, fate);
            FormalThreeDProgressionSaveData source = UnknownHistoryPayload(
                FateIds[2],
                fateRevision: 7ul);
            string before = RuntimeFingerprint(attention, fate);

            Assert.That(TryPrepare(
                adapter,
                source,
                out object plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(before),
                "Prepare must validate and clone without writing either runtime.");

            source.attention.value = 100;
            source.attention.history[0].reasonId = "mutated.reason.id";
            source.attention.committedStableEventKeys[0] =
                "mutated.event.key";
            source.fate.selectedId = "core.legacy.unknown";
            Assert.That(TryCommit(
                adapter,
                plan,
                out string commitError), Is.True, commitError);

            FormalThreeDProgressionSaveData restored = Capture(adapter);
            Assert.That(restored.attention.value, Is.EqualTo(15));
            Assert.That(restored.attention.history[0].reasonId,
                Is.EqualTo("removed.attention.reason"),
                "Unknown historical reasons are preserved as orphan evidence.");
            Assert.That(restored.attention.committedStableEventKeys[0],
                Is.EqualTo("removed.attention.event"));
            Assert.That(restored.fate.selectedId, Is.EqualTo(FateIds[2]));
            Assert.That(restored.fate.level, Is.EqualTo(1));
            Assert.That(restored.fate.revision, Is.EqualTo(7ul));
        }

        [Test]
        public void IDEA0020_RestorePlanIsOwnerRevisionBoundAndSingleUse()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object adapter = CreateAdapter(attention, fate);
            FormalThreeDProgressionSaveData source = UnknownHistoryPayload(
                FateIds[0],
                fateRevision: 3ul);

            Assert.That(TryPrepare(adapter, source, out object stale, out _),
                Is.True);
            Assert.That(attention.TryApply(
                "core.attention.scan.safe-mining-zone",
                "runtime.changed.after-prepare",
                out _), Is.True);
            string changed = RuntimeFingerprint(attention, fate);
            Assert.That(TryCommit(adapter, stale, out _), Is.False);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(changed));

            Assert.That(TryPrepare(adapter, source, out object foreign, out _),
                Is.True);
            var otherAttention = new FormalAttentionRuntime();
            var otherFate = new FormalFateRuntime();
            object other = CreateAdapter(otherAttention, otherFate);
            string otherBefore = RuntimeFingerprint(otherAttention, otherFate);
            Assert.That(TryCommit(other, foreign, out _), Is.False);
            Assert.That(RuntimeFingerprint(otherAttention, otherFate),
                Is.EqualTo(otherBefore));

            Assert.That(TryPrepare(adapter, source, out object valid, out _),
                Is.True);
            Assert.That(TryCommit(adapter, valid, out string error),
                Is.True, error);
            string committed = RuntimeFingerprint(attention, fate);
            Assert.That(TryCommit(adapter, valid, out _), Is.False);
            Assert.That(RuntimeFingerprint(attention, fate),
                Is.EqualTo(committed));
        }

        [Test]
        public void IDEA0020_InvalidAttentionOrFateFailsWithoutPartialMutation()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object adapter = CreateAdapter(attention, fate);
            string before = RuntimeFingerprint(attention, fate);

            FormalThreeDProgressionSaveData invalidAttention =
                UnknownHistoryPayload(FateIds[0], 1ul);
            invalidAttention.attention.value = 101;
            Assert.That(TryPrepare(
                adapter,
                invalidAttention,
                out _,
                out string attentionError), Is.False);
            Assert.That(attentionError, Is.Not.Empty);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(before));

            FormalThreeDProgressionSaveData invalidFate =
                UnknownHistoryPayload(FateIds[0], 1ul);
            invalidFate.fate.selectedId = "core.legacy.quantum-entanglement";
            Assert.That(TryPrepare(
                adapter,
                invalidFate,
                out _,
                out string fateError), Is.False);
            Assert.That(fateError, Is.Not.Empty);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(before));
        }

        [Test]
        public void IDEA0020_ProgressionRemainsInExpandedTransactionalOrder()
        {
            Type coordinator = RequireType(
                "WasteCity.Graybox3D.Building." +
                "GrayboxFormalSaveCoordinator3D, WasteCity.Game");
            Type domainId = RequireType(
                "WasteCity.Graybox3D.Building." +
                "GrayboxFormalSaveDomainId3D, WasteCity.Game");
            Assert.That(Enum.GetNames(domainId), Does.Contain("Progression"));
            PropertyInfo order = coordinator.GetProperty(
                "DomainOrder",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(order, Is.Not.Null);
            string[] names = ((IEnumerable)order.GetValue(null))
                .Cast<object>()
                .Select(value => value.ToString())
                .ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                "WorldCity",
                "BuildingStorage",
                "Economy",
                "Production",
                "Defense",
                "ResearchEffectState",
                "Progression",
                "Evacuation",
                "CivilizationExpansion",
                "Exploration",
                "Pause",
            }));

            string source = File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs")));
            StringAssert.Contains("GrayboxFormalProgressionSaveAdapter3D", source);
            StringAssert.Contains("GrayboxFormalSaveDomainId3D.Progression", source);
            StringAssert.Contains("destination.progression", source);
            StringAssert.Contains("source.progression", source);
            StringAssert.Contains("for (var index = 0; index < domains.Length; index++)",
                source,
                "Capture, apply and rollback must include every domain " +
                "through the single ordered domain array.");
        }

        private static FormalThreeDProgressionSaveData UnknownHistoryPayload(
            string selectedFateId,
            ulong fateRevision)
        {
            return new FormalThreeDProgressionSaveData
            {
                attention = new FormalThreeDAttentionSaveData
                {
                    value = 15,
                    revision = 1ul,
                    history = new[]
                    {
                        new FormalThreeDAttentionHistorySaveData
                        {
                            reasonId = "removed.attention.reason",
                            stableEventKey = "removed.attention.event",
                            requestedDelta = 5,
                            appliedDelta = 5,
                            valueAfter = 15,
                            revision = 1ul,
                            ruleTimeSeconds = 12.5f,
                            sourceInstanceId = "building.instance.000001",
                        },
                    },
                    reachedThresholds = Array.Empty<int>(),
                    committedStableEventKeys = new[]
                    {
                        "removed.attention.event",
                    },
                    completedOneShotReasonIds = Array.Empty<string>(),
                },
                fate = new FormalThreeDFateSaveData
                {
                    offeredIds = FateIds.ToArray(),
                    selectedId = selectedFateId,
                    level = 1,
                    revision = fateRevision,
                },
                civilization = new FormalThreeDCivilizationSaveData(),
            };
        }

        private static void CreateIdea0028Owners(
            FormalAttentionRuntime attention,
            AttentionPressureRuntime pressure,
            string sessionId,
            out QuantumEntanglementRuntime quantum,
            out SpatialTemplateRuntime spatial,
            out LocalHasteRuntime haste,
            out ForesightDelayRuntime foresight,
            out CausalTransparencyRuntime causal,
            out VoidChestRuntime chests,
            out CoordinateLockRuntime coordinate)
        {
            quantum = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.iron",
                "core.resource.water",
            });
            spatial = new SpatialTemplateRuntime();
            haste = new LocalHasteRuntime();
            foresight = new ForesightDelayRuntime();
            causal = new CausalTransparencyRuntime();
            chests = new VoidChestRuntime(sessionId, 3);
            coordinate = new CoordinateLockRuntime(attention, pressure);
        }

        private static void PopulateIdea0028Owners(
            AttentionPressureRuntime pressure,
            QuantumEntanglementRuntime quantum,
            SpatialTemplateRuntime spatial,
            LocalHasteRuntime haste,
            ForesightDelayRuntime foresight,
            CausalTransparencyRuntime causal,
            VoidChestRuntime chests,
            CoordinateLockRuntime coordinate)
        {
            Assert.That(quantum.TryCommitSynchronization(
                QuantumEntanglementRuntime.FirstSynchronizationKey), Is.True);
            Assert.That(quantum.TrySetConnected(false), Is.True);
            Assert.That(spatial.TryPrepareRecord(
                GrayboxFormalProgressionSaveAdapter3D
                    .FormalSpatialTemplateSlotId,
                new[]
                {
                    new SpatialTemplateCell(
                        -1, -1, "core.building.mining-station", 0),
                    new SpatialTemplateCell(
                        1, 0, "core.building.warehouse", 3),
                },
                out SpatialTemplateRecordPlan templatePlan,
                out string error), Is.True, error);
            Assert.That(spatial.TryCommit(templatePlan, out error),
                Is.True, error);
            Assert.That(haste.TryEnterCycle(2, out error), Is.True, error);
            Assert.That(haste.TrySelectTarget(
                "production", out error), Is.True, error);
            Assert.That(haste.TryStart(out error), Is.True, error);
            Assert.That(haste.Tick(12f, false, out _, out error),
                Is.True, error);
            Assert.That(pressure.TryRestore(
                new AttentionPressureSnapshot(
                    3ul,
                    new[]
                    {
                        new AttentionPressureEntrySnapshot(
                            30,
                            AttentionPressureState.Completed,
                            0f),
                        new AttentionPressureEntrySnapshot(
                            60,
                            AttentionPressureState.Completed,
                            0f),
                        new AttentionPressureEntrySnapshot(
                            90,
                            AttentionPressureState.Queued,
                            0f),
                    }),
                out error), Is.True, error);
            Assert.That(foresight.TryEnterCycle(4, out error), Is.True, error);
            Assert.That(foresight.TryReveal(
                4,
                100f,
                new[]
                {
                    new ForesightAuthoritativePlan(
                        AttentionPressureCatalog.FindByThreshold(90)
                            .EncounterId.Value,
                        140f,
                        "attention.pressure.90.summary"),
                },
                out _,
                out error), Is.True, error);
            Assert.That(foresight.TickDisplay(1.25f, false, out error),
                Is.True, error);
            Assert.That(causal.TrySetFullReasonAccess(true), Is.True);

            ulong claimedOrdinal = Enumerable.Range(1, 100)
                .Select(value => (ulong)value)
                .Single(value => VoidChestRuntime.ShouldDrop(
                    chests.SessionId,
                    chests.SelectionVersion,
                    "enemy.adapter.claimed",
                    value));
            ulong pendingOrdinal = Enumerable.Range(1, 100)
                .Select(value => (ulong)value)
                .Single(value => VoidChestRuntime.ShouldDrop(
                    chests.SessionId,
                    chests.SelectionVersion,
                    "enemy.adapter.pending",
                    value));
            Assert.That(chests.TryEvaluateDeath(
                "enemy.adapter.claimed",
                claimedOrdinal,
                out VoidChestEvaluation claimed,
                out error), Is.True, error);
            Assert.That(chests.TryClaim(claimed.ChestId, out error),
                Is.True, error);
            Assert.That(chests.TryEvaluateDeath(
                "enemy.adapter.pending",
                pendingOrdinal,
                out _,
                out error), Is.True, error);
            Assert.That(coordinate.TryRestore(
                new CoordinateLockSnapshot(true, 1),
                out error), Is.True, error);
        }

        private static GrayboxFormalProgressionSaveAdapter3D
            CreateIdea0028Adapter(
                FormalAttentionRuntime attention,
                AttentionPressureRuntime pressure,
                QuantumEntanglementRuntime quantum,
                SpatialTemplateRuntime spatial,
                LocalHasteRuntime haste,
                ForesightDelayRuntime foresight,
                CausalTransparencyRuntime causal,
                VoidChestRuntime chests,
                CoordinateLockRuntime coordinate)
        {
            return new GrayboxFormalProgressionSaveAdapter3D(
                attention,
                new FormalFateRuntime(),
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                new GrayboxAttentionPressureSaveAdapter3D(
                    pressure,
                    new GrayboxDefenseRuntime3D(0f, 0f, 20, 0f)),
                null,
                null,
                quantum,
                spatial,
                haste,
                foresight,
                causal,
                chests,
                coordinate);
        }

        private static string Idea0028Fingerprint(
            QuantumEntanglementRuntime quantum,
            SpatialTemplateRuntime spatial,
            LocalHasteRuntime haste,
            ForesightDelayRuntime foresight,
            CausalTransparencyRuntime causal,
            VoidChestRuntime chests,
            CoordinateLockRuntime coordinate)
        {
            QuantumEntanglementSnapshot q = quantum.Capture();
            SpatialTemplateSnapshot s = spatial.Capture();
            LocalHasteSnapshot h = haste.Capture();
            ForesightDelaySnapshot f = foresight.Capture();
            CausalTransparencySnapshot c = causal.Capture();
            VoidChestSnapshot v = chests.Capture();
            CoordinateLockSnapshot l = coordinate.Capture();
            return q.Connected + ":" + q.Revision + ":" +
                string.Join(",", q.SharedResourceIds) + ":" +
                string.Join(",", q.CommittedSynchronizationKeys) + "|" +
                s.Revision + ":" + string.Join(",", s.Templates.SelectMany(
                    template => template.Cells.Select(cell =>
                        template.Id + ":" + cell.X + ":" + cell.Y + ":" +
                        cell.BuildingDefinitionId + ":" +
                        cell.RotationQuarterTurns))) + "|" +
                h.TargetId + ":" + h.Active + ":" +
                h.RemainingBudgetSeconds + ":" + h.Revision + ":" +
                h.CurrentCycleOrdinal + "|" +
                f.CurrentCycleOrdinal + ":" +
                f.LastConsumedCycleOrdinal + ":" +
                f.LastProjection?.EventId + ":" +
                f.LastProjection?.SecondsUntilEvent + ":" + f.Revision + "|" +
                f.DisplayRemainingSeconds + "|" +
                c.FullReasonAccess + ":" + c.Revision + "|" +
                v.Revision + ":" + string.Join(",", v.Evaluations.Select(
                    value => value.DeathId + ":" + value.SequenceOrdinal +
                        ":" + value.Dropped + ":" + value.Claimed)) + "|" +
                l.Committed + ":" + l.Revision;
        }

        private static object CreateAdapter(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            return Activator.CreateInstance(
                RequireType(AdapterTypeName),
                attention,
                fate,
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                null,
                null,
                null);
        }

        private static FormalThreeDProgressionSaveData Capture(object adapter)
        {
            MethodInfo method = adapter.GetType().GetMethod(
                "Capture",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType,
                Is.EqualTo(typeof(FormalThreeDProgressionSaveData)));
            return (FormalThreeDProgressionSaveData)method.Invoke(adapter, null);
        }

        private static bool TryPrepare(
            object adapter,
            FormalThreeDProgressionSaveData source,
            out object plan,
            out string error)
        {
            Type planType = RequireType(PlanTypeName);
            MethodInfo method = adapter.GetType().GetMethod(
                "TryPrepareRestore",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(FormalThreeDProgressionSaveData),
                    planType.MakeByRefType(),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { source, null, null };
            bool result = (bool)method.Invoke(adapter, arguments);
            plan = arguments[1];
            error = arguments[2] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static bool TryCommit(
            object adapter,
            object plan,
            out string error)
        {
            Type planType = RequireType(PlanTypeName);
            MethodInfo method = adapter.GetType().GetMethod(
                "TryCommitRestore",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    planType,
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { plan, null };
            bool result = (bool)method.Invoke(adapter, arguments);
            error = arguments[1] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static string RuntimeFingerprint(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            FormalAttentionSnapshot attentionState = attention.Capture();
            FormalFateSnapshot fateState = fate.Capture();
            return attentionState.Value + "|" + attentionState.Revision + "|" +
                string.Join(",", attentionState.History.Select(
                    value => value.ReasonId + ":" + value.StableEventKey)) +
                "|" + fateState.SelectedId + "|" + fateState.Level + "|" +
                fateState.Revision;
        }

        private static Type RequireType(string name)
        {
            string fullName = name.Split(',')[0].Trim();
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    fullName,
                    throwOnError: false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
    }
}
