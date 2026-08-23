using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Combat;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class FormalSaveValidatorTests
    {
        [Test]
        public void CompleteSchemaThirtyOneFixtureIsValid()
        {
            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(LoadValidEnvelope());

            Assert.That(result.IsValid, Is.True, result.Message);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Formal3D));
            Assert.That(result.Error,
                Is.EqualTo(FormalSaveValidationError.None));
        }

        [TestCase("schema-01-legacy-2d.json", 1)]
        [TestCase("schema-30-legacy-2d.json", 30)]
        public void LegacyFixturesValidateAsHistoricalTwoDWithoutThreeDPayload(
            string fixtureName,
            int expectedSchema)
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture(fixtureName));

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateDecoded(decoded);

            Assert.That(result.IsValid, Is.True, result.Message);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Legacy2D));
            Assert.That(decoded.Legacy2D, Is.Not.Null);
            Assert.That(decoded.Legacy2D.schema, Is.EqualTo(expectedSchema));
            Assert.That(decoded.Envelope, Is.Null);
        }

        [Test]
        public void FutureSchemaKeepsDistinctValidationError()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-32-future.json").Replace(
                    "\"saveSchemaVersion\": 32",
                    "\"saveSchemaVersion\": " +
                    (FormalSaveEnvelope.CurrentSchemaVersion + 1)));

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateDecoded(decoded);

            AssertInvalid(
                result,
                FormalSaveValidationError.UnsupportedFutureSchema,
                "saveSchemaVersion");
        }

        [Test]
        public void EnvelopeUtcTimestampsAndPayloadHashAreValidated()
        {
            FormalSaveEnvelope invalidCreated = LoadValidEnvelope();
            invalidCreated.createdAt = "today";
            FormalSaveEnvelope backwards = LoadValidEnvelope();
            backwards.updatedAt = "2026-08-19T04:05:06.0000000Z";
            FormalSaveEnvelope wrongHash = LoadValidEnvelope();
            wrongHash.formal3D.city.population++;

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(invalidCreated),
                FormalSaveValidationError.InvalidTimestamp,
                "createdAt");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(backwards),
                FormalSaveValidationError.InvalidTimestamp,
                "updatedAt");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(wrongHash),
                FormalSaveValidationError.PayloadHashMismatch,
                "payloadHashSha256");
        }

        [Test]
        public void FixtureHashMatchesCanonicalFormalThreeDPayload()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();

            Assert.That(
                envelope.payloadHashSha256,
                Is.EqualTo(FormalSaveCodec.ComputePayloadHashSha256(
                    envelope.formal3D)));
        }

        [Test]
        public void RequiredDomainAndArrayCannotBeNull()
        {
            FormalSaveEnvelope missingDomain = LoadValidEnvelope();
            missingDomain.formal3D.production = null;
            FormalSaveEnvelope missingArray = LoadValidEnvelope();
            missingArray.formal3D.storage.warehouses = null;

            FormalSaveValidationResult domain =
                FormalSaveValidator.ValidateEnvelope(missingDomain);
            FormalSaveValidationResult array =
                FormalSaveValidator.ValidateEnvelope(missingArray);

            AssertInvalid(
                domain,
                FormalSaveValidationError.MissingRequiredValue,
                "formal3D.production");
            AssertInvalid(
                array,
                FormalSaveValidationError.InvalidArray,
                "formal3D.storage.warehouses");
        }

        [Test]
        public void MissingRequiredArrayMemberInSourceJsonIsRejected()
        {
            string json = FormalSaveCodec.EncodeEnvelope(LoadValidEnvelope())
                .Replace("\"warehouses\":[", "\"removedWarehouses\":[");
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(json);

            Assert.That(decoded.Success, Is.True, decoded.Message);
            AssertInvalid(
                FormalSaveValidator.ValidateDecoded(decoded),
                FormalSaveValidationError.MissingRequiredValue,
                "formal3D.storage.warehouses");
        }

        [Test]
        public void MissingNestedArrayMemberInSourceJsonIsRejected()
        {
            string json = FormalSaveCodec.EncodeEnvelope(LoadValidEnvelope())
                .Replace("\"amounts\":[", "\"removedAmounts\":[")
                .Replace(
                    "\"gameVersion\":",
                    "\"amounts\": [],\n  \"gameVersion\":");
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(json);

            Assert.That(decoded.Success, Is.True, decoded.Message);
            AssertInvalid(
                FormalSaveValidator.ValidateDecoded(decoded),
                FormalSaveValidationError.MissingRequiredValue,
                "formal3D.storage.warehouses[0].amounts");
        }

        [Test]
        public void NonFiniteRuleValuesAreRejectedWithFieldPath()
        {
            FormalSaveEnvelope checkpoint = LoadValidEnvelope();
            checkpoint.checkpoint.ruleTimeSeconds = float.NaN;
            FormalSaveEnvelope production = LoadValidEnvelope();
            production.formal3D.production.states[0].progressSeconds =
                float.PositiveInfinity;
            FormalSaveEnvelope evacuation = LoadValidEnvelope();
            evacuation.formal3D.evacuation.work[0].remainingRatio =
                double.NaN;

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(checkpoint),
                FormalSaveValidationError.NonFiniteNumber,
                "checkpoint.ruleTimeSeconds");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(production),
                FormalSaveValidationError.NonFiniteNumber,
                "formal3D.production.states[0].progressSeconds");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(evacuation),
                FormalSaveValidationError.NonFiniteNumber,
                "formal3D.evacuation.work[0].remainingRatio");
        }

        [Test]
        public void DefenseConfigurationSignatureIsRequired()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            SetDefenseField(
                envelope,
                "configurationSignature",
                string.Empty);
            RefreshHash(envelope);

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(envelope),
                FormalSaveValidationError.MissingRequiredValue,
                "formal3D.defense.configurationSignature");
        }

        [TestCase("spawnOriginX")]
        [TestCase("spawnOriginZ")]
        public void DefenseSpawnOriginMustBeFinite(string fieldName)
        {
            FormalSaveEnvelope envelope = PrepareDefenseEnvelope();
            SetDefenseField(envelope, fieldName, float.NaN);
            RefreshHash(envelope);

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(envelope),
                FormalSaveValidationError.NonFiniteNumber,
                "formal3D.defense." + fieldName);
        }

        public enum DefenseSemanticFault
        {
            TriggerCountAboveOne,
            TriggeredWithZeroCount,
            UntriggeredActivePhase,
            UntriggeredWithPersistedWaveState,
            TriggeredIdleBeforeTutorialCompletion,
            ActiveAfterTutorialCompletion,
            DefeatedAboveSpawned,
            SpawnedAboveTutorialTotal,
            LivingEnemyCountMismatch,
            DuplicateSpawnOrder,
            AmmunitionAboveCapacity,
            ActiveAmmunitionAboveDuration,
            TowerDamageRemainderAtOne,
            EnemyMovementRemainderAtOne,
            EnemyMovementRemainderNonZero,
            EnemyAttackRemainderAtOne,
            LivingEnemyWithZeroHealth,
            LivingEnemyAboveDefinitionMaximumHealth,
            UnknownTutorialEnemyArchetype,
            UnsupportedDefenseRandomState,
            WarningPhaseWithSpawnedState,
            SpawningWithWarningRemaining,
            SpawningAtSpawnCadence,
            ActiveBeforeAllEnemiesSpawned,
            CompletedIdleWithWarningRemaining,
            FixedStepAccumulatorAtStep,
            CoreHealthAboveMaximum,
            NextEnemyOrdinalBelowHistoricalSpawnedCount,
        }

        [TestCase(
            DefenseSemanticFault.TriggerCountAboveOne,
            "formal3D.defense.tutorialWaveTriggerCount")]
        [TestCase(
            DefenseSemanticFault.TriggeredWithZeroCount,
            "formal3D.defense.tutorialWaveTriggerCount")]
        [TestCase(
            DefenseSemanticFault.UntriggeredActivePhase,
            "formal3D.defense.wavePhase")]
        [TestCase(
            DefenseSemanticFault.UntriggeredWithPersistedWaveState,
            "formal3D.defense.spawnedEnemyCount")]
        [TestCase(
            DefenseSemanticFault.TriggeredIdleBeforeTutorialCompletion,
            "formal3D.defense.wavePhase")]
        [TestCase(
            DefenseSemanticFault.ActiveAfterTutorialCompletion,
            "formal3D.defense.wavePhase")]
        [TestCase(
            DefenseSemanticFault.DefeatedAboveSpawned,
            "formal3D.defense.defeatedEnemyCount")]
        [TestCase(
            DefenseSemanticFault.SpawnedAboveTutorialTotal,
            "formal3D.defense.spawnedEnemyCount")]
        [TestCase(
            DefenseSemanticFault.LivingEnemyCountMismatch,
            "formal3D.defense.enemies")]
        [TestCase(
            DefenseSemanticFault.DuplicateSpawnOrder,
            "formal3D.defense.enemies[1].spawnOrder")]
        [TestCase(
            DefenseSemanticFault.AmmunitionAboveCapacity,
            "formal3D.defense.towers[0].ammunitionAmount")]
        [TestCase(
            DefenseSemanticFault.ActiveAmmunitionAboveDuration,
            "formal3D.defense.towers[0].activeAmmunitionSeconds")]
        [TestCase(
            DefenseSemanticFault.TowerDamageRemainderAtOne,
            "formal3D.defense.towers[0].damageRemainder")]
        [TestCase(
            DefenseSemanticFault.EnemyMovementRemainderAtOne,
            "formal3D.defense.enemies[0].movementRemainder")]
        [TestCase(
            DefenseSemanticFault.EnemyMovementRemainderNonZero,
            "formal3D.defense.enemies[0].movementRemainder")]
        [TestCase(
            DefenseSemanticFault.EnemyAttackRemainderAtOne,
            "formal3D.defense.enemies[0].attackDamageRemainder")]
        [TestCase(
            DefenseSemanticFault.LivingEnemyWithZeroHealth,
            "formal3D.defense.enemies[0].currentHealth")]
        [TestCase(
            DefenseSemanticFault.LivingEnemyAboveDefinitionMaximumHealth,
            "formal3D.defense.enemies[0].currentHealth")]
        [TestCase(
            DefenseSemanticFault.UnknownTutorialEnemyArchetype,
            "formal3D.defense.enemies[0].archetypeId")]
        [TestCase(
            DefenseSemanticFault.UnsupportedDefenseRandomState,
            "formal3D.defense.randomState")]
        [TestCase(
            DefenseSemanticFault.WarningPhaseWithSpawnedState,
            "formal3D.defense.wavePhase")]
        [TestCase(
            DefenseSemanticFault.SpawningWithWarningRemaining,
            "formal3D.defense.warningRemainingSeconds")]
        [TestCase(
            DefenseSemanticFault.SpawningAtSpawnCadence,
            "formal3D.defense.spawnClockSeconds")]
        [TestCase(
            DefenseSemanticFault.ActiveBeforeAllEnemiesSpawned,
            "formal3D.defense.wavePhase")]
        [TestCase(
            DefenseSemanticFault.CompletedIdleWithWarningRemaining,
            "formal3D.defense.warningRemainingSeconds")]
        [TestCase(
            DefenseSemanticFault.FixedStepAccumulatorAtStep,
            "formal3D.defense.fixedStepAccumulatorSeconds")]
        [TestCase(
            DefenseSemanticFault.CoreHealthAboveMaximum,
            "formal3D.defense.coreCurrentHealth")]
        [TestCase(
            DefenseSemanticFault.NextEnemyOrdinalBelowHistoricalSpawnedCount,
            "formal3D.defense.nextEnemyOrdinal")]
        public void DefenseSemanticContradictionsAreRejected(
            DefenseSemanticFault fault,
            string expectedPath)
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            ApplyDefenseFault(envelope.formal3D.defense, fault);
            RefreshHash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False, result.Message);
            Assert.That(result.FieldPath, Is.EqualTo(expectedPath));
            Assert.That(result.Message, Is.Not.Empty);
        }

        [Test]
        public void NegativeResourceAmountsAreRejected()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            envelope.formal3D.storage.coreAmounts[0].amount = -1;

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            AssertInvalid(
                result,
                FormalSaveValidationError.NegativeValue,
                "formal3D.storage.coreAmounts[0].amount");
        }

        [Test]
        public void BackpackRequiresExactlyThirtyUniqueOrderedSlots()
        {
            FormalSaveEnvelope wrongCount = LoadValidEnvelope();
            Array.Resize(ref wrongCount.formal3D.backpack.slots, 29);
            FormalSaveEnvelope duplicateIndex = LoadValidEnvelope();
            duplicateIndex.formal3D.backpack.slots[29].slotIndex = 28;

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(wrongCount),
                FormalSaveValidationError.InvalidBackpack,
                "formal3D.backpack.slots");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(duplicateIndex),
                FormalSaveValidationError.InvalidBackpack,
                "formal3D.backpack.slots[29].slotIndex");
        }

        [Test]
        public void DuplicateStableInstanceIdIsRejected()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            envelope.formal3D.buildings.instances[1].stableInstanceId =
                envelope.formal3D.buildings.instances[0].stableInstanceId;

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            AssertInvalid(
                result,
                FormalSaveValidationError.DuplicateStableId,
                "formal3D.buildings.instances[1].stableInstanceId");
        }

        [Test]
        public void MissingStableInstanceCrossReferenceFixtureIsRejected()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-invalid-cross-reference.json"));

            Assert.That(decoded.Success, Is.True);
            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(decoded.Envelope);

            AssertInvalid(
                result,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.storage.warehouses[0].stableInstanceId");
        }

        [Test]
        public void InvalidCrossReferenceFixtureHasOnlyItsDeclaredSemanticFault()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-invalid-cross-reference.json"));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(
                decoded.Envelope.payloadHashSha256,
                Is.EqualTo(FormalSaveCodec.ComputePayloadHashSha256(
                    decoded.Envelope.formal3D)));
        }

        [Test]
        public void EveryRuntimeBuildingReferenceMustResolve()
        {
            AssertMissingBuildingReference(
                envelope => envelope.formal3D.production.states[0]
                    .stableInstanceId = "building.instance.999999",
                "formal3D.production.states[0].stableInstanceId");
            AssertMissingBuildingReference(
                envelope => envelope.formal3D.defense.towers[0]
                    .stableInstanceId = "building.instance.999999",
                "formal3D.defense.towers[0].stableInstanceId");
            AssertMissingBuildingReference(
                envelope =>
                {
                    const string missing = "building.instance.999999";
                    envelope.formal3D.evacuation.work[0]
                        .stableInstanceId = missing;
                    envelope.formal3D.evacuation
                        .fullQueueStableInstanceIds[0] = missing;
                    envelope.formal3D.evacuation
                        .currentStableInstanceId = missing;
                    envelope.formal3D.evacuation
                        .lockedStableInstanceIds[0] = missing;
                    envelope.formal3D.evacuation
                        .pendingRollbackStableInstanceIds[0] = missing;
                    envelope.formal3D.evacuation.runtimePayloads[0]
                        .stableInstanceId = missing;
                },
                "formal3D.evacuation.currentStableInstanceId");
            AssertMissingBuildingReference(
                envelope => envelope.formal3D.evacuation
                    .lockedStableInstanceIds[0] =
                        "building.instance.999999",
                "formal3D.evacuation.lockedStableInstanceIds[0]");
        }

        [Test]
        public void BoundResourceNodeReferenceMustResolve()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            envelope.formal3D.production.states[0].boundResourceNodeId =
                "resource.node.missing.000001";

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            AssertInvalid(
                result,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.production.states[0].boundResourceNodeId");
        }

        [Test]
        public void WorldDimensionsAndNodeCoordinatesMustBeConsistent()
        {
            FormalSaveEnvelope dimensions = LoadValidEnvelope();
            dimensions.formal3D.world.width = 0;
            FormalSaveEnvelope coordinate = LoadValidEnvelope();
            coordinate.formal3D.world.resourceNodes[0].x =
                coordinate.formal3D.world.width;

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(dimensions),
                FormalSaveValidationError.InvalidWorld,
                "formal3D.world.width");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(coordinate),
                FormalSaveValidationError.InvalidWorld,
                "formal3D.world.resourceNodes[0].x");
        }

        [Test]
        public void StableContentIdsMustUsePublishedSyntax()
        {
            FormalSaveEnvelope building = LoadValidEnvelope();
            building.formal3D.buildings.instances[0].definitionId =
                "Invalid Building";
            FormalSaveEnvelope filter = LoadValidEnvelope();
            filter.formal3D.storage.warehouses[0].filterResourceId =
                "bad filter";

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(building),
                FormalSaveValidationError.InvalidStableId,
                "formal3D.buildings.instances[0].definitionId");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(filter),
                FormalSaveValidationError.InvalidStableId,
                "formal3D.storage.warehouses[0].filterResourceId");
        }

        [Test]
        public void ActiveResearchCannotAlsoBeCompleted()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            envelope.formal3D.research.activeResearchId =
                envelope.formal3D.research.completedResearchIds[0];
            envelope.formal3D.research.remainingSeconds = 1f;

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(envelope),
                FormalSaveValidationError.InvalidResearch,
                "formal3D.research.activeResearchId");
        }

        [Test]
        public void EvacuationWorkOrderAndCurrentItemMustBeConsistent()
        {
            FormalSaveEnvelope duplicateWork = LoadValidEnvelope();
            Array.Resize(ref duplicateWork.formal3D.evacuation.work, 2);
            duplicateWork.formal3D.evacuation.work[1] =
                duplicateWork.formal3D.evacuation.work[0];
            FormalSaveEnvelope missingCurrent = LoadValidEnvelope();
            missingCurrent.formal3D.evacuation.currentStableInstanceId =
                "building.instance.000002";

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(duplicateWork),
                FormalSaveValidationError.DuplicateStableId,
                "formal3D.evacuation.work[1].stableInstanceId");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(missingCurrent),
                FormalSaveValidationError.InvalidEvacuation,
                "formal3D.evacuation.currentStableInstanceId");
        }

        [Test]
        public void StableOrdinalHighWaterMarksMustExceedExistingValues()
        {
            FormalSaveEnvelope building = LoadValidEnvelope();
            building.formal3D.buildings.nextStableInstanceOrdinal = 3;
            FormalSaveEnvelope crafting = LoadValidEnvelope();
            crafting.formal3D.crafting.nextQueueOrdinal = 1;
            FormalSaveEnvelope enemy = LoadValidEnvelope();
            enemy.formal3D.defense.nextEnemyOrdinal = 0;
            FormalSaveEnvelope evacuation = LoadValidEnvelope();
            evacuation.formal3D.evacuation.nextBatchOrdinal = 1;

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(building),
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.buildings.nextStableInstanceOrdinal");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(crafting),
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.crafting.nextQueueOrdinal");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(enemy),
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.defense.nextEnemyOrdinal");
            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(evacuation),
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.evacuation.nextBatchOrdinal");
        }

        [Test]
        public void InactiveEvacuationRejectsNegativeBatchHighWater()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            envelope.formal3D.evacuation.isProcessing = false;
            envelope.formal3D.evacuation.activeBatchId = "";
            envelope.formal3D.evacuation.currentStableInstanceId = "";
            envelope.formal3D.evacuation.nextBatchOrdinal = -1;

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(envelope),
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.evacuation.nextBatchOrdinal");
        }

        [Test]
        public void EnumBackedIntegersMustBeWithinPublishedRanges()
        {
            AssertInvalidEnum(
                envelope => envelope.formal3D.city.cityMode = 99,
                "formal3D.city.cityMode");
            AssertInvalidEnum(
                envelope => envelope.formal3D.buildings.instances[0].site = 99,
                "formal3D.buildings.instances[0].site");
            AssertInvalidEnum(
                envelope => envelope.formal3D.buildings.instances[0]
                    .orientation = 99,
                "formal3D.buildings.instances[0].orientation");
            AssertInvalidEnum(
                envelope => envelope.formal3D.buildings.instances[0].state = 99,
                "formal3D.buildings.instances[0].state");
            AssertInvalidEnum(
                envelope => envelope.formal3D.defense.wavePhase = 99,
                "formal3D.defense.wavePhase");
            AssertInvalidEnum(
                envelope => envelope.formal3D.evacuation.work[0].treatment = 99,
                "formal3D.evacuation.work[0].treatment");
        }

        [Test]
        public void UnknownButSyntacticallyValidContentIdsRemainLoadable()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            envelope.formal3D.buildings.instances[0].definitionId =
                "mod.example.building";
            envelope.formal3D.world.resourceNodes[0].resourceId =
                "mod.example.resource";
            envelope.formal3D.crafting.executions[0].recipeId =
                "mod.example.recipe";
            envelope.formal3D.research.completedResearchIds[0] =
                "mod.example.research";
            envelope.formal3D.production.states[0].definitionId =
                "mod.example.production";
            RefreshHash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.True, result.Message);
        }

        [Test]
        public void ChangedConfigurationCanPreserveExistingOverCapacityAssets()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            envelope.formal3D.storage.configurationSignature =
                "mod.example.changed-config";
            envelope.formal3D.storage.coreAmounts[0].amount = int.MaxValue;
            envelope.formal3D.storage.warehouses[0].amounts[0].amount =
                int.MaxValue;
            envelope.formal3D.backpack.slots[0].amount = int.MaxValue;
            envelope.formal3D.production.states[0].outputAmounts[0].amount =
                int.MaxValue;
            RefreshHash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.True, result.Message);
        }

        [Test]
        public void CommittedEvacuationHistoryMayReferenceRemovedBuilding()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            FormalThreeDEvacuationWorkSaveData active =
                envelope.formal3D.evacuation.work[0];
            envelope.formal3D.evacuation.work = new[]
            {
                new FormalThreeDEvacuationWorkSaveData
                {
                    stableInstanceId = "building.instance.999999",
                    treatment = 1,
                    remainingRatio = 0d,
                    baseDismantleSeconds = 5f,
                    dismantleSeconds = 5f,
                    refund = 0,
                },
                active,
            };
            envelope.formal3D.evacuation.fullQueueStableInstanceIds =
                new[]
                {
                    "building.instance.999999",
                    active.stableInstanceId,
                };
            envelope.formal3D.evacuation.currentQueueIndex = 1;
            RefreshHash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.True, result.Message);
        }

        [Test]
        public void MixedEvacuationQueueMayOmitCommittedQuickBuilding()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            FormalThreeDEvacuationWorkSaveData activeFull =
                envelope.formal3D.evacuation.work[0];
            var committedQuick = new FormalThreeDEvacuationWorkSaveData
            {
                stableInstanceId = "building.instance.999999",
                treatment = 3,
                remainingRatio = 1d,
                baseDismantleSeconds = 0f,
                dismantleSeconds = 0f,
                refund = 1,
            };
            envelope.formal3D.evacuation.work = new[]
            {
                activeFull,
                committedQuick,
            };
            envelope.formal3D.evacuation.fullQueueStableInstanceIds =
                new[]
                {
                    committedQuick.stableInstanceId,
                    activeFull.stableInstanceId,
                };
            envelope.formal3D.evacuation.currentQueueIndex = 1;
            envelope.formal3D.evacuation.currentStableInstanceId =
                activeFull.stableInstanceId;
            RefreshHash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.True, result.Message);
        }

        private static void AssertMissingBuildingReference(
            Action<FormalSaveEnvelope> mutate,
            string expectedPath)
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            mutate(envelope);

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(envelope),
                FormalSaveValidationError.MissingStableReference,
                expectedPath);
        }

        private static void AssertInvalidEnum(
            Action<FormalSaveEnvelope> mutate,
            string expectedPath)
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            mutate(envelope);

            AssertInvalid(
                FormalSaveValidator.ValidateEnvelope(envelope),
                FormalSaveValidationError.InvalidEnumValue,
                expectedPath);
        }

        private static void AssertInvalid(
            FormalSaveValidationResult result,
            FormalSaveValidationError expectedError,
            string expectedPath)
        {
            Assert.That(result.IsValid, Is.False, result.Message);
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.FieldPath, Is.EqualTo(expectedPath));
            Assert.That(result.Message, Is.Not.Empty);
        }

        private static FormalSaveEnvelope LoadValidEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            return decoded.Envelope;
        }

        private static FormalSaveEnvelope PrepareDefenseEnvelope()
        {
            FormalSaveEnvelope envelope = LoadValidEnvelope();
            SetDefenseField(
                envelope,
                "configurationSignature",
                "builtin:first-defense@1");
            SetDefenseField(envelope, "spawnOriginX", 30f);
            SetDefenseField(envelope, "spawnOriginZ", 28f);
            return envelope;
        }

        private static void SetDefenseField(
            FormalSaveEnvelope envelope,
            string fieldName,
            object value)
        {
            var field = typeof(FormalThreeDDefenseSaveData).GetField(
                fieldName);
            Assert.That(
                field,
                Is.Not.Null,
                "Schema 31 defense DTO is missing " + fieldName + ".");
            field.SetValue(envelope.formal3D.defense, value);
        }

        private static void ApplyDefenseFault(
            FormalThreeDDefenseSaveData defense,
            DefenseSemanticFault fault)
        {
            switch (fault)
            {
                case DefenseSemanticFault.TriggerCountAboveOne:
                    defense.tutorialWaveTriggerCount = 2;
                    return;
                case DefenseSemanticFault.TriggeredWithZeroCount:
                    defense.tutorialWaveTriggerCount = 0;
                    return;
                case DefenseSemanticFault.UntriggeredActivePhase:
                    defense.tutorialTriggered = false;
                    defense.tutorialWaveTriggerCount = 0;
                    return;
                case DefenseSemanticFault.UntriggeredWithPersistedWaveState:
                    defense.tutorialTriggered = false;
                    defense.tutorialWaveTriggerCount = 0;
                    defense.wavePhase = 0;
                    defense.warningRemainingSeconds = 0f;
                    defense.spawnClockSeconds = 0f;
                    return;
                case DefenseSemanticFault.TriggeredIdleBeforeTutorialCompletion:
                    defense.wavePhase = 0;
                    return;
                case DefenseSemanticFault.ActiveAfterTutorialCompletion:
                    defense.wavePhase = 3;
                    defense.spawnedEnemyCount = 8;
                    defense.defeatedEnemyCount = 8;
                    defense.nextEnemyOrdinal = 8;
                    defense.enemies =
                        Array.Empty<FormalThreeDDefenseEnemySaveData>();
                    return;
                case DefenseSemanticFault.DefeatedAboveSpawned:
                    defense.defeatedEnemyCount =
                        defense.spawnedEnemyCount + 1;
                    return;
                case DefenseSemanticFault.SpawnedAboveTutorialTotal:
                    defense.spawnedEnemyCount = 9;
                    defense.nextEnemyOrdinal = 9;
                    return;
                case DefenseSemanticFault.LivingEnemyCountMismatch:
                    defense.defeatedEnemyCount = 1;
                    return;
                case DefenseSemanticFault.DuplicateSpawnOrder:
                    FormalThreeDDefenseEnemySaveData original =
                        defense.enemies[0];
                    defense.enemies = new[]
                    {
                        original,
                        new FormalThreeDDefenseEnemySaveData
                        {
                            stableEnemyId =
                                "core.enemy.gnawer.tutorial.001",
                            archetypeId = original.archetypeId,
                            spawnOrder = original.spawnOrder,
                            positionX = original.positionX,
                            positionZ = original.positionZ,
                            currentHealth = original.currentHealth,
                            movementRemainder =
                                original.movementRemainder,
                            attackDamageRemainder =
                                original.attackDamageRemainder,
                        },
                    };
                    defense.spawnedEnemyCount = 2;
                    defense.nextEnemyOrdinal = 2;
                    return;
                case DefenseSemanticFault.AmmunitionAboveCapacity:
                    defense.towers[0].ammunitionAmount = 31;
                    return;
                case DefenseSemanticFault.ActiveAmmunitionAboveDuration:
                    defense.towers[0].activeAmmunitionSeconds = 3.001f;
                    return;
                case DefenseSemanticFault.TowerDamageRemainderAtOne:
                    defense.towers[0].damageRemainder = 1f;
                    return;
                case DefenseSemanticFault.EnemyMovementRemainderAtOne:
                    defense.enemies[0].movementRemainder = 1f;
                    return;
                case DefenseSemanticFault.EnemyMovementRemainderNonZero:
                    defense.enemies[0].movementRemainder = .5f;
                    return;
                case DefenseSemanticFault.EnemyAttackRemainderAtOne:
                    defense.enemies[0].attackDamageRemainder = 1f;
                    return;
                case DefenseSemanticFault.LivingEnemyWithZeroHealth:
                    defense.enemies[0].currentHealth = 0;
                    return;
                case DefenseSemanticFault.LivingEnemyAboveDefinitionMaximumHealth:
                    defense.enemies[0].currentHealth =
                        EnemyCatalog.Gnawer.MaximumHealth + 1;
                    return;
                case DefenseSemanticFault.UnknownTutorialEnemyArchetype:
                    defense.enemies[0].archetypeId = "mod.example.enemy";
                    return;
                case DefenseSemanticFault.UnsupportedDefenseRandomState:
                    defense.randomState = "future.random-state";
                    return;
                case DefenseSemanticFault.WarningPhaseWithSpawnedState:
                    defense.wavePhase = (int)WavePhase.Warning;
                    defense.warningRemainingSeconds = 1f;
                    defense.spawnClockSeconds = 0f;
                    return;
                case DefenseSemanticFault.SpawningWithWarningRemaining:
                    defense.warningRemainingSeconds = 1f;
                    return;
                case DefenseSemanticFault.SpawningAtSpawnCadence:
                    defense.spawnClockSeconds =
                        WaveCatalog.Tutorial.SpawnSeconds /
                        WaveCatalog.Tutorial.TotalCount;
                    return;
                case DefenseSemanticFault.ActiveBeforeAllEnemiesSpawned:
                    defense.wavePhase = (int)WavePhase.Active;
                    return;
                case DefenseSemanticFault.CompletedIdleWithWarningRemaining:
                    defense.wavePhase = (int)WavePhase.Idle;
                    defense.warningRemainingSeconds = 1f;
                    defense.spawnClockSeconds = 0f;
                    defense.spawnedEnemyCount =
                        WaveCatalog.Tutorial.TotalCount;
                    defense.defeatedEnemyCount =
                        WaveCatalog.Tutorial.TotalCount;
                    defense.nextEnemyOrdinal =
                        WaveCatalog.Tutorial.TotalCount;
                    defense.enemies =
                        Array.Empty<FormalThreeDDefenseEnemySaveData>();
                    return;
                case DefenseSemanticFault.FixedStepAccumulatorAtStep:
                    defense.fixedStepAccumulatorSeconds = .1f;
                    return;
                case DefenseSemanticFault.CoreHealthAboveMaximum:
                    defense.coreCurrentHealth = 2001;
                    return;
                case DefenseSemanticFault.NextEnemyOrdinalBelowHistoricalSpawnedCount:
                    defense.enemies =
                        Array.Empty<FormalThreeDDefenseEnemySaveData>();
                    defense.defeatedEnemyCount = 1;
                    defense.nextEnemyOrdinal = 0;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(fault),
                        fault,
                        "Unknown defense semantic fault.");
            }
        }

        private static void RefreshHash(FormalSaveEnvelope envelope)
        {
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(
                    envelope.formal3D);
        }

        private static string ReadFixture(string fileName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            string path = Path.Combine(
                projectRoot,
                "Assets/_Game/Tests/Fixtures/Persistence",
                fileName);
            Assert.That(File.Exists(path), Is.True,
                "Missing fixture: " + path);
            return File.ReadAllText(path);
        }
    }
}
