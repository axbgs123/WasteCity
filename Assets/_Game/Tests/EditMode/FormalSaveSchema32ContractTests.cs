using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class FormalSaveSchema32ContractTests
    {
        private const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        [Test]
        public void CurrentFormalSchemaIsThirtyTwo()
        {
            Assert.That(
                FormalSaveEnvelope.CurrentSchemaVersion,
                Is.EqualTo(32),
                "IDEA-0017 requires schema 32 before any new campaign " +
                "payload can be written.");
        }

        [Test]
        public void EveryHistoricalSchemaOneThroughThirtyRemainsLegacyTwoD()
        {
            for (int schema = 1; schema <= 30; schema++)
            {
                FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                    "{\"schema\":" + schema +
                    ",\"worldSeed\":8128,\"iron\":5}");

                Assert.That(result.Success, Is.True,
                    "Historical schema " + schema +
                    " must remain readable: " + result.Message);
                Assert.That(
                    result.PayloadKind,
                    Is.EqualTo(FormalSavePayloadKind.Legacy2D),
                    "Historical schema " + schema +
                    " must retain its legacy 2D identity.");
                Assert.That(result.Legacy2D, Is.Not.Null);
                Assert.That(result.Legacy2D.schema, Is.EqualTo(schema));
                Assert.That(result.Envelope, Is.Null,
                    "Schemas 1-30 must not be relabeled as formal 3D.");
            }
        }

        [Test]
        public void DecodeAnyIsTheSchemaThirtyOneToThirtyTwoMigrationEntry()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Formal3D));
            Assert.That(result.Envelope, Is.Not.Null);
            Assert.That(result.Envelope.saveSchemaVersion, Is.EqualTo(32),
                "DecodeAny must migrate a valid schema 31 envelope to the " +
                "current schema 32 contract.");
            Assert.That(result.Envelope.formal3D, Is.Not.Null);

            FieldInfo campaignField = RequireCampaignField();
            Assert.That(
                campaignField.GetValue(result.Envelope.formal3D),
                Is.Not.Null,
                "A successful schema 31 migration must materialize the " +
                "schema 32 defense campaign state.");
        }

        [Test]
        public void DefenseCampaignStateDeclaresAllPersistentRuleTruth()
        {
            Type campaignType = RequireCampaignField().FieldType;

            RequireField(campaignType, "campaignId");
            RequireField(campaignType, "phase");
            RequireField(campaignType, "currentWaveNumber");
            RequireField(campaignType, "fixedStepAccumulatorSeconds");
            RequireField(campaignType, "requestedSpeed");
            RequireField(campaignType, "lastNonZeroSpeed");
            RequireField(campaignType, "statistics");

            FieldInfo towerStates = RequireArrayField(
                campaignType,
                "towerCombatStates");
            Type towerStateType = towerStates.FieldType.GetElementType();
            RequireField(towerStateType, "stableInstanceId");
            RequireField(towerStateType, "consumableId");
            RequireField(towerStateType, "amount");

            FieldInfo enemyStates = RequireArrayField(
                campaignType,
                "enemyStates");
            Type enemyStateType = enemyStates.FieldType.GetElementType();
            RequireField(enemyStateType, "stableEnemyId");
            RequireField(enemyStateType, "archetypeId");
            RequireField(enemyStateType, "currentHealth");

            FieldInfo buildingHealthStates = RequireArrayField(
                campaignType,
                "buildingHealthStates");
            Type buildingHealthType =
                buildingHealthStates.FieldType.GetElementType();
            RequireField(buildingHealthType, "stableInstanceId");
            RequireField(buildingHealthType, "currentHealth");
            RequireField(buildingHealthType, "isDestroyed");
        }

        [Test]
        public void SchemaThirtyOneMigrationDefaultsBothSpeedValuesToOne()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Envelope, Is.Not.Null);
            Assert.That(result.Envelope.saveSchemaVersion, Is.EqualTo(32),
                "The schema 31 fixture must pass through the schema 32 " +
                "migration before speed defaults are observed.");

            object campaign = RequireCampaignField().GetValue(
                result.Envelope.formal3D);
            Assert.That(campaign, Is.Not.Null);
            Assert.That(
                ReadSingle(campaign, "requestedSpeed"),
                Is.EqualTo(1f),
                "Schema 31 did not persist requested speed, so migration " +
                "must default it to 1x.");
            Assert.That(
                ReadSingle(campaign, "lastNonZeroSpeed"),
                Is.EqualTo(1f),
                "Schema 31 did not persist last non-zero speed, so " +
                "migration must default it to 1x.");
        }

        [Test]
        public void CampaignCollectionsEncodeInStableIdentityOrder()
        {
            FieldInfo campaignField = RequireCampaignField();
            Type campaignType = campaignField.FieldType;
            object unsorted = CreateCampaignState(
                campaignType,
                new[]
                {
                    "building.instance.tower-030",
                    "building.instance.tower-010",
                    "building.instance.tower-020",
                },
                new[]
                {
                    "enemy.wave-001.030",
                    "enemy.wave-001.010",
                    "enemy.wave-001.020",
                },
                new[]
                {
                    "building.instance.health-030",
                    "building.instance.health-010",
                    "building.instance.health-020",
                });
            object sorted = CreateCampaignState(
                campaignType,
                new[]
                {
                    "building.instance.tower-010",
                    "building.instance.tower-020",
                    "building.instance.tower-030",
                },
                new[]
                {
                    "enemy.wave-001.010",
                    "enemy.wave-001.020",
                    "enemy.wave-001.030",
                },
                new[]
                {
                    "building.instance.health-010",
                    "building.instance.health-020",
                    "building.instance.health-030",
                });

            string first = FormalSaveCodec.EncodeEnvelope(
                EnvelopeWithCampaign(campaignField, unsorted));
            string second = FormalSaveCodec.EncodeEnvelope(
                EnvelopeWithCampaign(campaignField, sorted));

            Assert.That(first, Is.EqualTo(second),
                "Schema 32 encoding must canonicalize tower, enemy, and " +
                "building-health collections by stable identity.");
            AssertOrdered(first,
                "building.instance.tower-010",
                "building.instance.tower-020",
                "building.instance.tower-030");
            AssertOrdered(first,
                "enemy.wave-001.010",
                "enemy.wave-001.020",
                "enemy.wave-001.030");
            AssertOrdered(first,
                "building.instance.health-010",
                "building.instance.health-020",
                "building.instance.health-030");
        }

        private static FieldInfo RequireCampaignField()
        {
            FieldInfo field = typeof(FormalThreeDSaveData).GetField(
                "defenseCampaign",
                PublicInstance);
            Assert.That(field, Is.Not.Null,
                "Schema 32 requires FormalThreeDSaveData.defenseCampaign " +
                "as the new campaign truth; the schema 31 defense DTO " +
                "remains migration input only.");
            return field;
        }

        private static FieldInfo RequireField(Type owner, string fieldName)
        {
            Assert.That(owner, Is.Not.Null,
                "A schema 32 nested state type is missing.");
            FieldInfo field = owner.GetField(fieldName, PublicInstance);
            Assert.That(field, Is.Not.Null,
                owner.FullName + " must declare public field " +
                fieldName + ".");
            return field;
        }

        private static FieldInfo RequireArrayField(
            Type owner,
            string fieldName)
        {
            FieldInfo field = RequireField(owner, fieldName);
            Assert.That(field.FieldType.IsArray, Is.True,
                owner.FullName + "." + fieldName +
                " must be an array with deterministic encoding order.");
            Assert.That(field.FieldType.GetElementType(), Is.Not.Null);
            return field;
        }

        private static float ReadSingle(object owner, string fieldName)
        {
            FieldInfo field = RequireField(owner.GetType(), fieldName);
            Assert.That(field.FieldType, Is.EqualTo(typeof(float)),
                owner.GetType().FullName + "." + fieldName +
                " must be a float rule-speed value.");
            return (float)field.GetValue(owner);
        }

        private static object CreateCampaignState(
            Type campaignType,
            string[] towerIds,
            string[] enemyIds,
            string[] buildingIds)
        {
            object state = Activator.CreateInstance(campaignType);
            Assert.That(state, Is.Not.Null,
                campaignType.FullName +
                " must have a default constructor for JsonUtility.");
            SetIdentityArray(
                state,
                "towerCombatStates",
                "stableInstanceId",
                towerIds);
            SetIdentityArray(
                state,
                "enemyStates",
                "stableEnemyId",
                enemyIds);
            SetIdentityArray(
                state,
                "buildingHealthStates",
                "stableInstanceId",
                buildingIds);
            return state;
        }

        private static void SetIdentityArray(
            object owner,
            string arrayFieldName,
            string identityFieldName,
            string[] identities)
        {
            FieldInfo arrayField = RequireArrayField(
                owner.GetType(),
                arrayFieldName);
            Type elementType = arrayField.FieldType.GetElementType();
            FieldInfo identityField = RequireField(
                elementType,
                identityFieldName);
            Assert.That(identityField.FieldType, Is.EqualTo(typeof(string)));

            Array values = Array.CreateInstance(elementType, identities.Length);
            for (int index = 0; index < identities.Length; index++)
            {
                object value = Activator.CreateInstance(elementType);
                Assert.That(value, Is.Not.Null,
                    elementType.FullName +
                    " must have a default constructor for JsonUtility.");
                identityField.SetValue(value, identities[index]);
                values.SetValue(value, index);
            }
            arrayField.SetValue(owner, values);
        }

        private static FormalSaveEnvelope EnvelopeWithCampaign(
            FieldInfo campaignField,
            object campaign)
        {
            var payload = new FormalThreeDSaveData();
            campaignField.SetValue(payload, campaign);
            return new FormalSaveEnvelope
            {
                gameVersion = "test.idea-0017",
                saveSchemaVersion = 32,
                contentSources = Array.Empty<string>(),
                createdAt = "2026-08-24T00:00:00.0000000Z",
                updatedAt = "2026-08-24T00:00:00.0000000Z",
                runtimeKind = FormalSaveEnvelope.FormalThreeDRuntimeKind,
                checkpoint = new FormalSaveCheckpointMetadata
                {
                    completedMilestoneIds = Array.Empty<string>(),
                },
                formal3D = payload,
            };
        }

        private static void AssertOrdered(
            string encoded,
            string first,
            string second,
            string third)
        {
            int firstIndex = encoded.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = encoded.IndexOf(second, StringComparison.Ordinal);
            int thirdIndex = encoded.IndexOf(third, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(secondIndex, Is.GreaterThan(firstIndex));
            Assert.That(thirdIndex, Is.GreaterThan(secondIndex));
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
