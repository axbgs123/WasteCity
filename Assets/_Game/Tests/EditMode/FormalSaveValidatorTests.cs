using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
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

        [Test]
        public void LegacyFixturesValidateAsFrozenTwoDWithoutThreeDPayload()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-30-legacy-2d.json"));

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateDecoded(decoded);

            Assert.That(result.IsValid, Is.True, result.Message);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Legacy2D));
            Assert.That(decoded.Legacy2D, Is.Not.Null);
            Assert.That(decoded.Envelope, Is.Null);
        }

        [Test]
        public void FutureSchemaKeepsDistinctValidationError()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-32-future.json"));

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
            string json = ReadFixture("schema-31-formal-3d.json")
                .Replace("\"warehouses\": [", "\"removedWarehouses\": [");
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
            string json = ReadFixture("schema-31-formal-3d.json")
                .Replace("\"amounts\": [", "\"removedAmounts\": [")
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
                envelope => envelope.formal3D.evacuation.work[0]
                    .stableInstanceId = "building.instance.999999",
                "formal3D.evacuation.work[0].stableInstanceId");
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
            envelope.formal3D.defense.enemies[0].archetypeId =
                "mod.example.enemy";
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
            envelope.formal3D.defense.towers[0].ammunitionAmount =
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
