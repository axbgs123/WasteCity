using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class FormalSaveSchema37ContractTests
    {
        [Test]
        public void IDEA0029_CurrentSchemaOwnsExplorationAggregate()
        {
            Assert.That(FormalSaveEnvelope.CurrentSchemaVersion, Is.EqualTo(37));

            var exploration = new FormalThreeDExplorationSaveData();
            Assert.That(exploration.configurationSignature,
                Is.EqualTo(FormalThreeDExplorationSaveData.ConfigurationSignature));
            Assert.That(exploration.configurationVersion, Is.EqualTo(1));
            Assert.That(exploration.exploredCells, Is.Not.Null);
            Assert.That(exploration.scanZones, Is.Not.Null);
            Assert.That(exploration.intel, Is.Not.Null);
            Assert.That(exploration.leader, Is.Not.Null);
            Assert.That(exploration.cenJinDistress, Is.Not.Null);
            Assert.That(exploration.outpostAlerts, Is.Not.Null);
        }

        [Test]
        public void IDEA0029_CurrentSchemaRoundTripsEveryExplorationOwner()
        {
            FormalSaveEnvelope envelope = LoadCurrentEnvelope();
            envelope.formal3D.exploration = ValidExploration(envelope);
            envelope.formal3D.exploration.exploredCells[4] = true;
            envelope.formal3D.exploration.scanZones = new[]
            {
                new FormalThreeDScanZoneSaveData
                {
                    zoneId = "core.exploration.zone.safe-mining",
                    committedEventKey =
                        "exploration.scan:formal.session.default:" +
                        "core.exploration.zone.safe-mining",
                },
            };
            envelope.formal3D.exploration.intel = new[]
            {
                new FormalThreeDIntelSaveData
                {
                    stableIntelId = "core.intel.resource.000001",
                    ownerKind = 0,
                    ownerStableId = envelope.formal3D.world.resourceNodes[0]
                        .stableNodeId,
                    x = envelope.formal3D.world.resourceNodes[0].x,
                    y = envelope.formal3D.world.resourceNodes[0].y,
                    remainingFreshSeconds = 42f,
                    remainingExpirySeconds = 162f,
                    hasMutableValue = true,
                    mutableValue = 77,
                },
            };
            envelope.formal3D.exploration.leader.requestedControlMode = 1;
            envelope.formal3D.exploration.leader.manualGather.active = true;
            envelope.formal3D.exploration.leader.manualGather.targetNodeId =
                envelope.formal3D.world.resourceNodes[0].stableNodeId;
            envelope.formal3D.exploration.leader.manualGather
                .remainingCycleSeconds = 3f;
            envelope.formal3D.exploration.cenJinDistress.state = 2;
            envelope.formal3D.exploration.cenJinDistress
                .elapsedSinceDiscoverySeconds = 25f;
            envelope.formal3D.exploration.cenJinDistress
                .rescueRemainingSeconds = 8f;
            envelope.formal3D.exploration.cenJinDistress.reservedBiomass = 10;
            envelope.formal3D.exploration.outpostAlerts = new[]
            {
                new FormalThreeDOutpostAlertSaveData
                {
                    stableAlertId = "core.outpost-alert.000001",
                    settlementId = FindOutpostId(envelope),
                    attackFactId = "core.attack-fact.000001",
                    severity = 2,
                    x = 30,
                    y = 20,
                    threatSummary = "掠夺者接近",
                    estimatedLossRiskPercent = 40,
                    estimatedSecondsToLoss = 35f,
                    firstRuleTimeSeconds = 12d,
                    latestRuleTimeSeconds = 20d,
                    acknowledged = true,
                    resolved = false,
                },
            };
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(envelope));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalThreeDExplorationSaveData restored =
                decoded.Envelope.formal3D.exploration;
            Assert.That(restored.exploredCells[4], Is.True);
            Assert.That(restored.scanZones[0].zoneId,
                Is.EqualTo("core.exploration.zone.safe-mining"));
            Assert.That(restored.intel[0].mutableValue, Is.EqualTo(77));
            Assert.That(restored.leader.requestedControlMode, Is.EqualTo(1));
            Assert.That(restored.leader.manualGather.remainingCycleSeconds,
                Is.EqualTo(3f));
            Assert.That(restored.cenJinDistress.reservedBiomass, Is.EqualTo(10));
            Assert.That(restored.outpostAlerts[0].acknowledged, Is.True);
        }

        [Test]
        public void IDEA0029_SchemaThirtySixMigratesAfterOldHashValidation()
        {
            FormalSaveEnvelope historical = LoadCurrentEnvelope();
            historical.saveSchemaVersion = 36;
            historical.formal3D.exploration = null;
            string schemaThirtySixHash =
                FormalSaveCodec.ComputePayloadHashSha256(historical.formal3D);
            historical.payloadHashSha256 = schemaThirtySixHash;

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(historical));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(37));
            Assert.That(decoded.Envelope.formal3D.exploration, Is.Not.Null);
            Assert.That(decoded.Envelope.formal3D.exploration.scanZones, Is.Empty);
            Assert.That(decoded.Envelope.formal3D.exploration.intel, Is.Empty);
            Assert.That(decoded.Envelope.formal3D.exploration.outpostAlerts,
                Is.Empty);
            Assert.That(decoded.Envelope.formal3D.exploration.exploredCells,
                Has.Some.True);
            Assert.That(decoded.Envelope.formal3D.exploration.cenJinDistress.state,
                Is.EqualTo(5));
            Assert.That(schemaThirtySixHash, Has.Length.EqualTo(64));
        }

        [Test]
        public void IDEA0029_SchemaThirtySixRejectsTamperingBeforeMigration()
        {
            FormalSaveEnvelope historical = LoadCurrentEnvelope();
            historical.saveSchemaVersion = 36;
            historical.formal3D.exploration = null;
            historical.payloadHashSha256 = new string('0', 64);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(historical));

            Assert.That(decoded.Success, Is.False);
            Assert.That(decoded.Error, Is.EqualTo(
                FormalSaveDecodeError.MalformedJson));
        }

        [Test]
        public void IDEA0029_CurrentHashIncludesExplorationState()
        {
            FormalSaveEnvelope envelope = LoadCurrentEnvelope();
            envelope.formal3D.exploration = ValidExploration(envelope);
            string before = FormalSaveCodec.ComputePayloadHashSha256(
                envelope.formal3D);
            envelope.formal3D.exploration.exploredCells[1] = true;
            string after = FormalSaveCodec.ComputePayloadHashSha256(
                envelope.formal3D);

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void IDEA0029_ValidatorRejectsMalformedExplorationState()
        {
            FormalSaveEnvelope envelope = LoadCurrentEnvelope();
            envelope.formal3D.exploration = ValidExploration(envelope);
            envelope.formal3D.exploration.scanZones = new[]
            {
                new FormalThreeDScanZoneSaveData
                {
                    zoneId = "core.exploration.zone.safe-mining",
                    committedEventKey = "core.scan.event.000001",
                },
                new FormalThreeDScanZoneSaveData
                {
                    zoneId = "core.exploration.zone.safe-mining",
                    committedEventKey = "core.scan.event.000002",
                },
            };
            AssertInvalid(envelope, FormalSaveValidationError.DuplicateStableId,
                "formal3D.exploration.scanZones[1].zoneId");

            envelope.formal3D.exploration = ValidExploration(envelope);
            envelope.formal3D.exploration.scanZones = new[]
            {
                new FormalThreeDScanZoneSaveData
                {
                    zoneId = "core.exploration.zone.safe-mining",
                    committedEventKey =
                        "exploration.scan:formal.session.default:" +
                        "core.exploration.zone.safe-mining",
                },
            };
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
            Assert.That(FormalSaveValidator.ValidateEnvelope(envelope).IsValid,
                Is.True);

            envelope.formal3D.exploration = ValidExploration(envelope);
            envelope.formal3D.exploration.exploredCells = new bool[1];
            AssertInvalid(envelope, FormalSaveValidationError.InvalidArray,
                "formal3D.exploration.exploredCells");

            envelope.formal3D.exploration = ValidExploration(envelope);
            envelope.formal3D.exploration.leader.requestedControlMode = 9;
            AssertInvalid(envelope, FormalSaveValidationError.InvalidEnumValue,
                "formal3D.exploration.leader.requestedControlMode");
        }

        private static void AssertInvalid(
            FormalSaveEnvelope envelope,
            FormalSaveValidationError error,
            string fieldPath)
        {
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.FieldPath, Is.EqualTo(fieldPath));
        }

        private static FormalThreeDExplorationSaveData ValidExploration(
            FormalSaveEnvelope envelope)
        {
            int width = envelope.formal3D.world.width;
            int height = envelope.formal3D.world.height;
            return new FormalThreeDExplorationSaveData
            {
                worldConfigurationSignature =
                    envelope.formal3D.world.configurationSignature,
                width = width,
                height = height,
                exploredCells = new bool[width * height],
            };
        }

        private static string FindOutpostId(FormalSaveEnvelope envelope)
        {
            FormalThreeDSettlementSaveData[] settlements = envelope.formal3D
                .civilizationExpansion.worldLayer.settlements;
            for (int index = 0; index < settlements.Length; index++)
                if (settlements[index].kind == 2)
                    return settlements[index].stableSettlementId;
            return "core.outpost.000001";
        }

        private static FormalSaveEnvelope LoadCurrentEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "_Game/Tests/Fixtures/Persistence/schema-31-formal-3d.json")));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            return decoded.Envelope;
        }
    }
}
