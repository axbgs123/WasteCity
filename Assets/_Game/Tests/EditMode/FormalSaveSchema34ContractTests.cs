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
            Assert.That(FormalSaveEnvelope.CurrentSchemaVersion, Is.EqualTo(34));
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
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(34));
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
    }
}
