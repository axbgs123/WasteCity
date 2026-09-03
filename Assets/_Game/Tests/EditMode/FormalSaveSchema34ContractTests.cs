using System;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class FormalSaveSchema34ContractTests
    {
        [Test]
        public void IDEA0022_CurrentSchemaOwnsOneCivilizationExpansionPayload()
        {
            Assert.That(FormalSaveEnvelope.CurrentSchemaVersion, Is.EqualTo(36));
            FieldInfo field = typeof(FormalThreeDSaveData).GetField(
                "civilizationExpansion",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType.Name,
                Is.EqualTo("FormalThreeDCivilizationExpansionSaveData"));

            var payload = new FormalThreeDSaveData();
            Assert.That(field.GetValue(payload), Is.Not.Null);
        }

        [Test]
        public void IDEA0022_ExpansionDtoKeepsThreeOwnedSubdomains()
        {
            Type expansion = typeof(FormalThreeDSaveData).Assembly.GetType(
                "WasteCity.Persistence.ThreeD." +
                "FormalThreeDCivilizationExpansionSaveData");
            Assert.That(expansion, Is.Not.Null);
            foreach (string name in new[]
                     {
                         "armyLeader",
                         "worldLayer",
                         "charactersPolitics",
                     })
            {
                FieldInfo field = expansion.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(field, Is.Not.Null, name);
                Assert.That(field.GetValue(Activator.CreateInstance(expansion)),
                    Is.Not.Null, name);
            }
        }

        [Test]
        public void IDEA0022_ExpansionPayloadRoundTripsThroughCurrentCodec()
        {
            Type expansion = typeof(FormalThreeDSaveData).Assembly.GetType(
                "WasteCity.Persistence.ThreeD." +
                "FormalThreeDCivilizationExpansionSaveData");
            Assert.That(expansion, Is.Not.Null);
            var payload = new FormalThreeDSaveData();
            FieldInfo field = typeof(FormalThreeDSaveData).GetField(
                "civilizationExpansion");
            field.SetValue(payload, Activator.CreateInstance(expansion));
            var envelope = new FormalSaveEnvelope
            {
                gameVersion = "idea-0022-test",
                saveSchemaVersion = FormalSaveEnvelope.CurrentSchemaVersion,
                runtimeKind = FormalSaveEnvelope.FormalThreeDRuntimeKind,
                checkpoint = new FormalSaveCheckpointMetadata
                {
                    sequence = 1,
                    reasonId = "idea-0022-test",
                    completedMilestoneIds = Array.Empty<string>(),
                },
                formal3D = payload,
            };
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(payload);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(envelope));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(field.GetValue(decoded.Envelope.formal3D), Is.Not.Null);
        }

        [Test]
        public void IDEA0022_SchemaThirtyThreeMigratesToCleanExpansion()
        {
            var payload = new FormalThreeDSaveData
            {
                sessionId = "schema-33-migration",
                defenseCampaign =
                    new FormalThreeDDefenseCampaignSaveData
                    {
                        campaignId = "schema-33-migration-campaign",
                    },
                civilizationExpansion = null,
            };
            var envelope = new FormalSaveEnvelope
            {
                gameVersion = "idea-0021-history",
                saveSchemaVersion = 33,
                runtimeKind = FormalSaveEnvelope.FormalThreeDRuntimeKind,
                checkpoint = new FormalSaveCheckpointMetadata
                {
                    sequence = 7,
                    reasonId = "schema-33-history",
                    completedMilestoneIds = Array.Empty<string>(),
                },
                formal3D = payload,
            };
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(payload);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(envelope));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(36));
            Assert.That(decoded.Envelope.formal3D.civilizationExpansion,
                Is.Not.Null);
            Assert.That(decoded.Envelope.formal3D.civilizationExpansion
                .worldLayer.primaryCityId, Is.EqualTo("core.city.000001"));
            Assert.That(decoded.Envelope.formal3D.civilizationExpansion
                .armyLeader.units, Is.Empty);
            Assert.That(decoded.Envelope.formal3D.civilizationExpansion
                .charactersPolitics.currentLeaderId,
                Is.EqualTo("core.character.cen-jin"));
        }

        [Test]
        public void IDEA0022_ValidatorRejectsUnknownArmyManufacturingDefinition()
        {
            var expansion = new FormalThreeDCivilizationExpansionSaveData();
            expansion.armyLeader.manufacturing = new[]
            {
                new FormalThreeDArmyManufacturingSaveData
                {
                    definitionId = "unknown.unit",
                    progressSeconds = 1f,
                },
            };

            FormalSaveValidationResult result = ValidateExpansion(expansion);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FieldPath, Does.Contain("manufacturing"));
        }

        [Test]
        public void IDEA0022_ValidatorRejectsConvoyWithoutSettlementReferences()
        {
            var expansion = new FormalThreeDCivilizationExpansionSaveData();
            expansion.worldLayer.convoys = new[]
            {
                new FormalThreeDConvoySaveData
                {
                    stableConvoyId = "core.convoy.000001",
                    sourceSettlementId = "core.city.000001",
                    destinationSettlementId = "core.city.000002",
                    path = Array.Empty<FormalThreeDGridPointSaveData>(),
                    cargo = Array.Empty<FormalThreeDResourceAmountSaveData>(),
                },
            };

            FormalSaveValidationResult result = ValidateExpansion(expansion);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FieldPath, Does.Contain("convoys"));
        }

        [Test]
        public void IDEA0022_ValidatorRejectsArmyPoliticsLeaderMismatch()
        {
            var expansion = new FormalThreeDCivilizationExpansionSaveData();
            expansion.armyLeader.leader.characterId =
                "core.character.lin-xi";

            FormalSaveValidationResult result = ValidateExpansion(expansion);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FieldPath, Does.Contain("leader.characterId"));
        }

        [Test]
        public void IDEA0022_ValidatorRejectsContradictorySquadLeaderFlags()
        {
            var expansion = new FormalThreeDCivilizationExpansionSaveData();
            expansion.armyLeader.leaderAssigned = true;
            expansion.armyLeader.leaderHealthy = true;
            expansion.armyLeader.squads = new[]
            {
                new FormalThreeDArmySquadSaveData
                {
                    stableSquadId = "core.squad.000001",
                    leaderAssigned = false,
                    leaderHealthy = false,
                },
            };

            FormalSaveValidationResult result = ValidateExpansion(expansion);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FieldPath, Does.Contain("squads[0]"));
        }

        [Test]
        public void IDEA0022_ValidatorRejectsSettlementOutsideFormalMap()
        {
            var expansion = new FormalThreeDCivilizationExpansionSaveData();
            expansion.worldLayer.settlements = new[]
            {
                new FormalThreeDSettlementSaveData
                {
                    stableSettlementId = "core.city.000001",
                    kind = 0,
                    autonomousTemplate = 0,
                    x = 64,
                    y = 0,
                    population = 0,
                    populationCapacity = 0,
                    loyalty = 70,
                    inventory = Array.Empty<
                        FormalThreeDResourceAmountSaveData>(),
                },
            };

            FormalSaveValidationResult result = ValidateExpansion(expansion);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FieldPath, Does.Contain("settlements[0]"));
        }

        [Test]
        public void IDEA0022_ValidatorRejectsUnapprovedExpeditionLoot()
        {
            var expansion = new FormalThreeDCivilizationExpansionSaveData();
            expansion.armyLeader.expedition =
                new FormalThreeDArmyExpeditionSaveData
                {
                    phase = 1,
                    squadId = "core.squad.000001",
                    sessionId = "test.session",
                    expeditionOrdinal = 1,
                    outboundDurationSeconds = 45f,
                    returnDurationSeconds = 0f,
                    remainingSeconds = 1f,
                    units = Array.Empty<
                        FormalThreeDArmyExpeditionUnitSaveData>(),
                    enemyDefinitionIds = Array.Empty<string>(),
                    casualtyStableUnitIds = Array.Empty<string>(),
                    pendingLoot = new[]
                    {
                        new FormalThreeDResourceAmountSaveData
                        {
                            resourceId = "core.resource.iron",
                            amount = 1,
                        },
                    },
                };

            FormalSaveValidationResult result = ValidateExpansion(expansion);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FieldPath, Does.Contain("pendingLoot"));
        }

        private static FormalSaveValidationResult ValidateExpansion(
            FormalThreeDCivilizationExpansionSaveData expansion)
        {
            MethodInfo method = typeof(FormalSaveValidator).GetMethod(
                "ValidateCivilizationExpansion",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (FormalSaveValidationResult)method.Invoke(
                null,
                new object[] { expansion });
        }
    }
}
