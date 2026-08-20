using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class FormalSaveEnvelopeTests
    {
        [TestCase("schema-01-legacy-2d.json", 1)]
        [TestCase("schema-30-legacy-2d.json", 30)]
        public void LegacyFixturesAreClassifiedWithoutBecomingThreeD(
            string fixtureName,
            int expectedSchema)
        {
            FormalSaveDecodeResult result =
                FormalSaveCodec.DecodeAny(ReadFixture(fixtureName));

            Assert.That(result.Success, Is.True);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Legacy2D));
            Assert.That(result.Legacy2D, Is.Not.Null);
            Assert.That(result.Legacy2D.schema, Is.EqualTo(expectedSchema));
            Assert.That(result.Envelope, Is.Null);
        }

        [Test]
        public void SchemaThirtyOneFixtureHasExplicitFormalThreeDIdentity()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Formal3D));
            FormalSaveEnvelope envelope = result.Envelope;
            Assert.That(envelope, Is.Not.Null);
            Assert.That(envelope.gameVersion, Is.EqualTo("0.1.0"));
            Assert.That(envelope.saveSchemaVersion, Is.EqualTo(31));
            Assert.That(envelope.runtimeKind,
                Is.EqualTo("formal-3d"));
            Assert.That(envelope.createdAt,
                Is.EqualTo("2026-08-20T01:02:03.0000000Z"));
            Assert.That(envelope.updatedAt,
                Is.EqualTo("2026-08-20T04:05:06.0000000Z"));
            Assert.That(envelope.contentSources,
                Is.EqualTo(new[] { "builtin:wastecity@0.1.0" }));
            FormalSaveCheckpointMetadata checkpoint = envelope.checkpoint;
            Assert.That(checkpoint, Is.Not.Null);
            Assert.That(checkpoint.sequence, Is.EqualTo(7L));
            Assert.That(checkpoint.reasonId,
                Is.EqualTo("first-deployment-complete"));
            Assert.That(checkpoint.ruleTimeSeconds,
                Is.EqualTo(123.5f));
            Assert.That(checkpoint.completedMilestoneIds,
                Is.EqualTo(new[] { "first-deployment-complete" }));
            Assert.That(envelope.formal3D, Is.Not.Null);
            Assert.That(ReadFixture("schema-31-formal-3d.json"),
                Does.Not.Contain("\"legacy2D\""));
        }

        [Test]
        public void SchemaThirtyOneEncodingIsStableAndRoundTripsFields()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            FormalSaveEnvelope envelope = decoded.Envelope;

            string first = FormalSaveCodec.EncodeEnvelope(envelope);
            string second = FormalSaveCodec.EncodeEnvelope(envelope);
            FormalSaveDecodeResult roundTrip =
                FormalSaveCodec.DecodeAny(first);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(roundTrip.Success, Is.True);
            FormalSaveEnvelope restored = roundTrip.Envelope;
            Assert.That(restored.gameVersion, Is.EqualTo("0.1.0"));
            Assert.That(restored.runtimeKind,
                Is.EqualTo("formal-3d"));
            Assert.That(restored.checkpoint.sequence, Is.EqualTo(7L));
            Assert.That(restored.formal3D.world.worldSeed, Is.EqualTo(8128));
            Assert.That(restored.formal3D.sessionId,
                Is.EqualTo("fixture.formal-3d"));
        }

        [Test]
        public void RootSchemaMembersConflictEvenWhenEitherValueIsZero()
        {
            string schemaThirtyOne =
                ReadFixture("schema-31-formal-3d.json");
            string zeroLegacy = schemaThirtyOne.Replace(
                "{\n  \"gameVersion\"",
                "{\n  \"schema\": 0,\n  \"gameVersion\"");
            string zeroEnvelope = ReadFixture("schema-01-legacy-2d.json")
                .Replace(
                    "\"schema\": 1",
                    "\"schema\": 1,\n  \"saveSchemaVersion\": 0");

            FormalSaveDecodeResult first =
                FormalSaveCodec.DecodeAny(zeroLegacy);
            FormalSaveDecodeResult second =
                FormalSaveCodec.DecodeAny(zeroEnvelope);

            Assert.That(first.Success, Is.False);
            Assert.That(first.Error,
                Is.EqualTo(FormalSaveDecodeError.PayloadKindMismatch));
            Assert.That(second.Success, Is.False);
            Assert.That(second.Error,
                Is.EqualTo(FormalSaveDecodeError.PayloadKindMismatch));
        }

        [Test]
        public void NestedSchemaMemberDoesNotCreateRootIdentityConflict()
        {
            string json = ReadFixture("schema-31-formal-3d.json")
                .Replace(
                    "\"sessionId\": \"fixture.formal-3d\"",
                    "\"schema\": 1,\n    " +
                    "\"sessionId\": \"fixture.formal-3d\"");

            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(json);

            Assert.That(result.Success, Is.True);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Formal3D));
        }

        [Test]
        public void SchemaThirtyOneFixtureDeclaresEveryThreeDDomainShape()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            FormalThreeDSaveData payload = result.Envelope.formal3D;

            Assert.That(payload.world.resourceNodes, Has.Length.EqualTo(1));
            Assert.That(payload.world.resourceNodes[0].stableNodeId,
                Is.EqualTo("resource.node.iron.000001"));
            Assert.That(payload.world.resourceNodes[0].resourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(payload.city.population, Is.EqualTo(100));
            Assert.That(payload.buildings.nextStableInstanceOrdinal,
                Is.EqualTo(4));
            Assert.That(payload.buildings.instances, Has.Length.EqualTo(3));
            Assert.That(payload.buildings.instances[0].stableInstanceId,
                Is.EqualTo("building.instance.000001"));
            Assert.That(payload.buildings.instances[2].stableInstanceId,
                Is.EqualTo("building.instance.000003"));
            Assert.That(payload.storage.warehouses, Has.Length.EqualTo(1));
            Assert.That(payload.storage.warehouses[0].stableInstanceId,
                Is.EqualTo("building.instance.000002"));
            Assert.That(payload.storage.warehouses[0].amounts[0].resourceId,
                Is.EqualTo(ResourceIds.Stone));
            Assert.That(payload.storage.orphanResources, Is.Empty);
            Assert.That(payload.backpack.slots, Has.Length.EqualTo(30));
            Assert.That(payload.backpack.slots[0].resourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(payload.backpack.slots[1].resourceId,
                Is.EqualTo(ResourceIds.Alloy));
            Assert.That(payload.crafting.nextQueueOrdinal, Is.EqualTo(2));
            Assert.That(payload.crafting.executions, Has.Length.EqualTo(1));
            Assert.That(payload.crafting.executions[0].stableExecutionId,
                Is.EqualTo("craft.execution.000001"));
            Assert.That(payload.crafting.executions[0].recipeId,
                Is.EqualTo(ResourceRecipeCatalog.FieldAlloyId));
            Assert.That(payload.crafting.executions[0].reservedInputs[0]
                .resourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(payload.research.completedResearchIds,
                Is.EqualTo(new[]
                {
                    DemoResearchCatalog.ScrapProcessingId,
                    DemoResearchCatalog.BasicMetallurgyId,
                    DemoResearchCatalog.AmmunitionAssemblyId,
                    DemoResearchCatalog.AutomatedDefenseId,
                }));
            Assert.That(payload.research.activeResearchId, Is.Empty);
            Assert.That(payload.research.remainingSeconds, Is.Zero);
            Assert.That(payload.production.states, Has.Length.EqualTo(1));
            Assert.That(payload.production.states[0].stableInstanceId,
                Is.EqualTo("building.instance.000001"));
            Assert.That(payload.production.states[0].definitionId,
                Is.EqualTo(FormalProductionDefinitionCatalog.Extraction.Id));
            Assert.That(payload.production.states[0].boundResourceNodeId,
                Is.EqualTo("resource.node.iron.000001"));
            Assert.That(payload.defense.nextEnemyOrdinal, Is.EqualTo(1));
            Assert.That(payload.defense.enemies, Has.Length.EqualTo(1));
            Assert.That(payload.defense.towers[0].stableInstanceId,
                Is.EqualTo("building.instance.000003"));
            Assert.That(payload.defense.towers[0].activeAmmunitionSeconds,
                Is.EqualTo(.4f));
            Assert.That(payload.defense.towers[0].damageRemainder,
                Is.EqualTo(.25f));
            Assert.That(payload.defense.enemies[0].stableEnemyId,
                Is.EqualTo("core.enemy.gnawer.tutorial.000"));
            Assert.That(payload.defense.enemies[0].attackDamageRemainder,
                Is.EqualTo(.5f));
            Assert.That(payload.evacuation.nextBatchOrdinal, Is.EqualTo(2));
            Assert.That(payload.evacuation.isProcessing, Is.True);
            Assert.That(payload.evacuation.activeBatchId,
                Is.EqualTo("evacuation.batch.000001"));
            Assert.That(payload.evacuation.currentStableInstanceId,
                Is.EqualTo("building.instance.000003"));
            Assert.That(payload.evacuation.work[0].stableInstanceId,
                Is.EqualTo("building.instance.000003"));
            Assert.That(payload.evacuation.runtimePayloads,
                Has.Length.EqualTo(1));
            Assert.That(payload.evacuation.runtimePayloads[0]
                .towerAmmunitionAmount, Is.EqualTo(5));
            Assert.That(payload.evacuation.runtimePayloads[0]
                .resourcePayload[0].resourceId,
                Is.EqualTo(ResourceIds.Ammunition));
            Assert.That(payload.evacuation.lockedStableInstanceIds,
                Is.EqualTo(new[] { "building.instance.000003" }));
            Assert.That(payload.pause, Is.Not.Null);
        }

        [Test]
        public void MissingContentPauseFlagsRemainDerivedAndAreNotPersisted()
        {
            string fixture = ReadFixture("schema-31-formal-3d.json");

            Assert.That(fixture,
                Does.Not.Contain("\"isMissingContentPaused\""));
            Assert.That(fixture,
                Does.Not.Contain("\"activeMissingContentPaused\""));
            Assert.That(typeof(FormalThreeDCraftingExecutionSaveData)
                .GetField("isMissingContentPaused"), Is.Null);
            Assert.That(typeof(FormalThreeDResearchSaveData)
                .GetField("activeMissingContentPaused"), Is.Null);
        }

        [Test]
        public void CompleteFixtureCanonicalRoundTripIsByteStable()
        {
            FormalSaveDecodeResult firstDecode = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));

            string firstBytes = FormalSaveCodec.EncodeEnvelope(
                firstDecode.Envelope);
            FormalSaveDecodeResult secondDecode =
                FormalSaveCodec.DecodeAny(firstBytes);
            string secondBytes = FormalSaveCodec.EncodeEnvelope(
                secondDecode.Envelope);

            Assert.That(firstDecode.Success, Is.True);
            Assert.That(secondDecode.Success, Is.True);
            Assert.That(firstBytes, Is.EqualTo(secondBytes));
        }

        [Test]
        public void CanonicalEnvelopeEncodingIsCompactSortedAndNullSafe()
        {
            FormalSaveEnvelope left = CanonicalEnvelope(
                new[] { "source.z", "source.a" },
                new[] { "milestone.z", "milestone.a" });
            FormalSaveEnvelope right = CanonicalEnvelope(
                new[] { "source.a", "source.z" },
                new[] { "milestone.a", "milestone.z" });

            string leftJson = FormalSaveCodec.EncodeEnvelope(left);
            string rightJson = FormalSaveCodec.EncodeEnvelope(right);
            left.contentSources = null;
            left.checkpoint.completedMilestoneIds = null;
            string nullJson = FormalSaveCodec.EncodeEnvelope(left);

            Assert.That(leftJson, Is.EqualTo(rightJson));
            Assert.That(leftJson, Does.Not.Contain("\n"));
            Assert.That(leftJson.IndexOf("source.a", StringComparison.Ordinal),
                Is.LessThan(leftJson.IndexOf(
                    "source.z",
                    StringComparison.Ordinal)));
            Assert.That(nullJson, Does.Contain("\"contentSources\":[]"));
            Assert.That(nullJson,
                Does.Contain("\"completedMilestoneIds\":[]"));
        }

        [Test]
        public void DecodeEnvelopeRejectsLegacyFixtureAsPayloadMismatch()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeEnvelope(
                ReadFixture("schema-01-legacy-2d.json"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error,
                Is.EqualTo(FormalSaveDecodeError.PayloadKindMismatch));
        }

        [Test]
        public void UtcTimestampUsesRoundTripUtcForm()
        {
            var value = new DateTime(
                2026,
                8,
                20,
                4,
                5,
                6,
                DateTimeKind.Utc);

            string formatted = FormalSaveCodec.FormatUtcTimestamp(value);

            Assert.That(formatted,
                Is.EqualTo(value.ToString("O", CultureInfo.InvariantCulture)));
            Assert.That(formatted, Does.EndWith("Z"));
        }

        [Test]
        public void FutureSchemaReturnsStructuredVersionError()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-32-future.json"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error,
                Is.EqualTo(FormalSaveDecodeError.UnsupportedFutureSchema));
            Assert.That(result.Message, Is.EqualTo("存档版本过新"));
        }

        [TestCase("")]
        [TestCase("  \r\n")]
        [TestCase("{\"saveSchemaVersion\":31")]
        public void BlankAndTruncatedDocumentsReturnStructuredFailure(string json)
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(json);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.Not.EqualTo(FormalSaveDecodeError.None));
        }

        [Test]
        public void UnknownRuntimeKindReturnsStructuredFailure()
        {
            string json = ReadFixture("schema-31-formal-3d.json")
                .Replace("formal-3d", "unknown-runtime");

            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(json);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error,
                Is.EqualTo(FormalSaveDecodeError.UnknownRuntimeKind));
        }

        [Test]
        public void SchemaAndPayloadIdentityMismatchReturnsStructuredFailure()
        {
            string json = ReadFixture("schema-31-formal-3d.json")
                .Replace("\"formal3D\"", "\"legacy2D\"");

            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(json);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error,
                Is.EqualTo(FormalSaveDecodeError.PayloadKindMismatch));
        }

        [Test]
        public void DecodeAnyLeavesSemanticValidationToValidator()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-invalid-cross-reference.json"));

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Formal3D));
            Assert.That(
                FormalSaveValidator.ValidateDecoded(result).IsValid,
                Is.False);
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

        private static FormalSaveEnvelope CanonicalEnvelope(
            string[] sources,
            string[] milestones)
        {
            return new FormalSaveEnvelope
            {
                gameVersion = "0.1.0",
                saveSchemaVersion = 31,
                contentSources = sources,
                createdAt = "2026-08-20T01:02:03.0000000Z",
                updatedAt = "2026-08-20T04:05:06.0000000Z",
                runtimeKind = "formal-3d",
                payloadHashSha256 = "hash",
                checkpoint = new FormalSaveCheckpointMetadata
                {
                    sequence = 1,
                    reasonId = "test",
                    ruleTimeSeconds = 2f,
                    completedMilestoneIds = milestones,
                },
                formal3D = new FormalThreeDSaveData
                {
                    sessionId = "canonical",
                    world = new FormalThreeDWorldSaveData
                    {
                        worldSeed = 8128,
                    },
                },
            };
        }
    }
}
