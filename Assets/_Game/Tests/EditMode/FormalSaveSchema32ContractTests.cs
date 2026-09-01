using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Core;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class FormalSaveSchema32ContractTests
    {
        private const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        [Test]
        public void CurrentFormalSchemaIsThirtyFive()
        {
            Assert.That(
                FormalSaveEnvelope.CurrentSchemaVersion,
                Is.EqualTo(35),
                "IDEA-0027 advances the current schema to 35 while " +
                "preserving the earlier campaign, progression, and " +
                "civilization expansion payloads.");
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
            Assert.That(result.Envelope.saveSchemaVersion, Is.EqualTo(35),
                "DecodeAny must migrate a valid schema 31 envelope to the " +
                "current schema 35 contract.");
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
            RequireField(campaignType, "result");
            RequireField(campaignType, "statistics");
            RequireEnemyCountArray(
                campaignType,
                "plannedEnemyCountsByEnemyId");
            RequireEnemyCountArray(
                campaignType,
                "spawnedEnemyCountsByEnemyId");
            RequireEnemyCountArray(
                campaignType,
                "defeatedEnemyCountsByEnemyId");

            FieldInfo frozenAnchors = RequireArrayField(
                campaignType,
                "frozenSpawnAnchors");
            Type anchorType = frozenAnchors.FieldType.GetElementType();
            RequireField(anchorType, "direction");
            RequireField(anchorType, "positionX");
            RequireField(anchorType, "positionZ");

            FieldInfo towerStates = RequireArrayField(
                campaignType,
                "towerCombatStates");
            Type towerStateType = towerStates.FieldType.GetElementType();
            RequireField(towerStateType, "stableInstanceId");
            RequireField(towerStateType, "consumableId");
            RequireField(towerStateType, "amount");
            RequireField(towerStateType, "activeConsumableSeconds");
            RequireField(towerStateType, "targetStableEnemyId");

            FieldInfo enemyStates = RequireArrayField(
                campaignType,
                "enemyStates");
            Type enemyStateType = enemyStates.FieldType.GetElementType();
            RequireField(enemyStateType, "stableEnemyId");
            RequireField(enemyStateType, "archetypeId");
            RequireField(enemyStateType, "currentHealth");
            RequireField(enemyStateType, "targetStableId");

            FieldInfo buildingHealthStates = RequireArrayField(
                campaignType,
                "buildingHealthStates");
            Type buildingHealthType =
                buildingHealthStates.FieldType.GetElementType();
            RequireField(buildingHealthType, "stableInstanceId");
            RequireField(buildingHealthType, "currentHealth");
            RequireField(buildingHealthType, "isDestroyed");

            Type statisticsType = RequireField(
                campaignType,
                "statistics").FieldType;
            RequireField(statisticsType, "elapsedRuleSeconds");
            RequireField(statisticsType, "completedWaveCount");
            RequireField(statisticsType, "killsByEnemyId");
            RequireField(statisticsType, "highestAliveEnemyCount");
            RequireField(statisticsType, "coreDamageTaken");
            RequireField(statisticsType, "buildingLossesByBuildingId");
            RequireField(statisticsType, "damageByTowerBuildingId");
            RequireField(statisticsType, "killsByTowerBuildingId");
            RequireField(statisticsType, "consumablesSpentByResourceId");
            RequireField(statisticsType, "completedProductionBatchCount");
            RequireField(statisticsType, "productionActiveProgressSeconds");
            RequireField(statisticsType, "productionEligibleSeconds");
            RequireField(statisticsType, "cityWasPackedAfterCampaignStart");
            RequireField(statisticsType, "developmentModifierUsed");
            RequireField(statisticsType, "partialFromMigration");
        }

        [Test]
        public void SchemaThirtyOnePayloadHashDamageCannotBeWashedByMigration()
        {
            string valid = ReadFixture("schema-31-formal-3d.json");
            string tampered = valid.Replace(
                "\"worldSeed\": 8128",
                "\"worldSeed\": 8129");
            Assert.That(tampered, Is.Not.EqualTo(valid));

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                tampered);
            bool rejected = !decoded.Success ||
                !FormalSaveValidator.ValidateDecoded(decoded).IsValid;

            Assert.That(rejected, Is.True,
                "A schema 31 payload must be checked against its original " +
                "payloadHashSha256 before migration mutates or re-hashes it.");
        }

        [Test]
        public void SchemaThirtyOneMigrationPreservesAnchorsHealthTowerCachesAndPartialFlag()
        {
            FormalSaveEnvelope source = JsonUtility.FromJson<FormalSaveEnvelope>(
                ReadFixture("schema-31-formal-3d.json"));
            AddCompletedTower(
                source,
                "building.instance.000004",
                BuildingCatalog.LaserTower,
                x: 15,
                y: 11);
            AddCompletedTower(
                source,
                "building.instance.000005",
                BuildingCatalog.SporeTower,
                x: 16,
                y: 11);
            source.formal3D.buildings.nextStableInstanceOrdinal = 6;
            source.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(source.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(source));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            object campaign = RequireCampaignField().GetValue(
                decoded.Envelope.formal3D);
            Assert.That(campaign, Is.Not.Null);

            object eastAnchor = FindItemByEnumName(
                ReadArray(campaign, "frozenSpawnAnchors"),
                "direction",
                "East");
            Assert.That(eastAnchor, Is.Not.Null,
                "An active schema 31 tutorial must freeze its original " +
                "spawn origin as the schema 32 east anchor.");
            Assert.That(ReadSingle(eastAnchor, "positionX"), Is.EqualTo(30f));
            Assert.That(ReadSingle(eastAnchor, "positionZ"), Is.EqualTo(28f));

            Array healthStates = ReadArray(campaign, "buildingHealthStates");
            AssertHealth(
                healthStates,
                "building.instance.000001",
                BuildingCatalog.MiningStation.MaximumHealth);
            AssertHealth(
                healthStates,
                "building.instance.000002",
                BuildingCatalog.Warehouse.MaximumHealth);
            AssertHealth(
                healthStates,
                "building.instance.000003",
                BuildingCatalog.MachineGunTurret.MaximumHealth);
            AssertHealth(
                healthStates,
                "building.instance.000004",
                BuildingCatalog.LaserTower.MaximumHealth);
            AssertHealth(
                healthStates,
                "building.instance.000005",
                BuildingCatalog.SporeTower.MaximumHealth);

            Array towerStates = ReadArray(campaign, "towerCombatStates");
            AssertTowerCache(
                towerStates,
                "building.instance.000003",
                "technology.resource.ammunition",
                5);
            AssertTowerCache(
                towerStates,
                "building.instance.000004",
                "core.resource.energy-crystal",
                0);
            AssertTowerCache(
                towerStates,
                "building.instance.000005",
                "biological.resource.weapon",
                0);

            object statistics = ReadField(campaign, "statistics");
            Assert.That(ReadBoolean(statistics, "partialFromMigration"), Is.True,
                "Unavailable pre-schema-32 combat statistics must be marked " +
                "partial instead of being presented as complete zeroes.");
            Assert.That(
                ReadArray(statistics, "killsByTowerBuildingId").Length,
                Is.Zero);
            Assert.That(
                ReadInteger(statistics, "completedProductionBatchCount"),
                Is.Zero);
            Assert.That(
                ReadSingle(statistics, "productionActiveProgressSeconds"),
                Is.Zero);
            Assert.That(
                ReadSingle(statistics, "productionEligibleSeconds"),
                Is.Zero);
            Assert.That(
                ReadBoolean(statistics, "cityWasPackedAfterCampaignStart"),
                Is.False);
            Assert.That(
                ReadBoolean(statistics, "developmentModifierUsed"),
                Is.False);
        }

        [Test]
        public void SchemaThirtyOneMigrationExcludesCompletedNonPlayerBuildingsFromCombatTopology()
        {
            FormalSaveEnvelope source = JsonUtility.FromJson<FormalSaveEnvelope>(
                ReadFixture("schema-31-formal-3d.json"));
            FormalThreeDBuildingInstanceSaveData abandonedBuilding =
                Array.Find(
                    source.formal3D.buildings.instances,
                    item => item.stableInstanceId ==
                        "building.instance.000001");
            FormalThreeDBuildingInstanceSaveData abandonedTower =
                Array.Find(
                    source.formal3D.buildings.instances,
                    item => item.stableInstanceId ==
                        "building.instance.000003");
            Assert.That(abandonedBuilding, Is.Not.Null);
            Assert.That(abandonedTower, Is.Not.Null);
            Assert.That(abandonedBuilding.state, Is.EqualTo(1));
            Assert.That(abandonedTower.state, Is.EqualTo(1));
            abandonedBuilding.isPlayerOwned = false;
            abandonedTower.isPlayerOwned = false;
            Assert.That(source.formal3D.defense.towers.Any(
                    item => item.stableInstanceId ==
                        abandonedTower.stableInstanceId), Is.True,
                "The schema 31 mutation must retain the obsolete tower cache " +
                "so migration proves it is filtered by building ownership.");
            source.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(source.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(source));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalThreeDDefenseCampaignSaveData campaign =
                decoded.Envelope.formal3D.defenseCampaign;

            Assert.That(campaign.towerCombatStates.Any(
                    item => item.stableInstanceId ==
                        abandonedTower.stableInstanceId), Is.False,
                "A completed but non-player tower is absent from formal runtime " +
                "topology and must not make schema 31 restore fail.");
            Assert.That(campaign.buildingHealthStates.Any(
                    item => item.stableInstanceId ==
                        abandonedBuilding.stableInstanceId), Is.False);
            Assert.That(campaign.buildingHealthStates.Any(
                    item => item.stableInstanceId ==
                        abandonedTower.stableInstanceId), Is.False);
            Assert.That(campaign.buildingHealthStates.Any(
                    item => item.stableInstanceId ==
                        "building.instance.000002"), Is.True,
                "Player-owned completed buildings must remain in health truth.");

            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            Assert.That(validation.IsValid, Is.True, validation.Message);
            var restored = new SingleCityDefenseCampaignModel(28f, 28f);
            Assert.That(restored.TryPrepareRestore(
                ToCampaignPersistence(campaign),
                out SingleCityDefenseCampaignRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(plan, Is.Not.Null,
                "Filtered schema 31 campaign truth must remain restorable.");
        }

        [Test]
        public void ActiveSchemaThirtyOneEnemyAndTowerLockMigrateToRestorableFormalIdentity()
        {
            FormalSaveEnvelope source = JsonUtility.FromJson<FormalSaveEnvelope>(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(source.formal3D.defense.enemies, Has.Length.EqualTo(1));
            Assert.That(
                source.formal3D.defense.enemies[0].stableEnemyId,
                Is.EqualTo("core.enemy.gnawer.tutorial.000"),
                "The fixture must exercise a real schema 31 tutorial ID.");
            source.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(source.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(source));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalThreeDDefenseCampaignSaveData campaign =
                decoded.Envelope.formal3D.defenseCampaign;
            const string expectedEnemyId =
                "campaign.enemy.wave-01.0000";

            Assert.That(campaign.enemyStates, Has.Length.EqualTo(1));
            Assert.That(campaign.enemyStates[0].stableEnemyId,
                Is.EqualTo(expectedEnemyId));
            Assert.That(campaign.enemyStates[0].targetStableId,
                Is.EqualTo(SingleCityDefenseCampaignModel.CityCoreTargetId));
            Assert.That(campaign.towerCombatStates, Has.Length.EqualTo(1));
            Assert.That(campaign.towerCombatStates[0].targetStableEnemyId,
                Is.Null.Or.Empty,
                "Schema 31 did not persist a tower target. Migration must " +
                "leave it empty for the next formal fixed step to reacquire.");
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            Assert.That(validation.IsValid, Is.True, validation.Message);

            var restored = new SingleCityDefenseCampaignModel(28f, 28f);
            Assert.That(restored.TryPrepareRestore(
                ToCampaignPersistence(campaign),
                out SingleCityDefenseCampaignRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(plan, Is.Not.Null,
                "An active survivor is one of the five schema 31 migration " +
                "states and must be accepted by the formal restore model.");
        }

        [Test]
        public void CompletedSchemaThirtyOneTutorialStartsCleanWaveTwoWarning()
        {
            FormalSaveEnvelope source = JsonUtility.FromJson<FormalSaveEnvelope>(
                ReadFixture("schema-31-formal-3d.json"));
            FormalThreeDDefenseSaveData legacy = source.formal3D.defense;
            legacy.tutorialTriggered = true;
            legacy.tutorialWaveTriggerCount = 1;
            legacy.wavePhase = 0;
            legacy.warningRemainingSeconds = 0f;
            legacy.spawnClockSeconds = 0f;
            legacy.spawnedEnemyCount = 8;
            legacy.defeatedEnemyCount = 8;
            legacy.nextEnemyOrdinal = 8;
            legacy.enemies = Array.Empty<FormalThreeDDefenseEnemySaveData>();
            source.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(source.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(source));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            object campaign = RequireCampaignField().GetValue(
                decoded.Envelope.formal3D);
            Assert.That(ReadInteger(campaign, "phase"), Is.EqualTo(1),
                "Completed tutorial migration must enter Warning.");
            Assert.That(ReadInteger(campaign, "currentWaveNumber"),
                Is.EqualTo(2));
            Assert.That(ReadSingle(campaign, "warningRemainingSeconds"),
                Is.EqualTo(20f));
            Assert.That(
                ReadEnemyCount(
                    campaign,
                    "plannedEnemyCountsByEnemyId",
                    "core.enemy.gnawer"),
                Is.EqualTo(10));
            Assert.That(
                ReadEnemyCount(
                    campaign,
                    "spawnedEnemyCountsByEnemyId",
                    "core.enemy.gnawer"),
                Is.Zero,
                "Wave-one cumulative spawns must not pollute wave two.");
            Assert.That(
                ReadEnemyCount(
                    campaign,
                    "defeatedEnemyCountsByEnemyId",
                    "core.enemy.gnawer"),
                Is.Zero,
                "Wave-one cumulative defeats belong only to statistics.");
            Assert.That(ReadInteger(campaign, "nextEnemyOrdinal"), Is.Zero,
                "Wave two has not spawned an enemy yet.");

            object statistics = ReadField(campaign, "statistics");
            Assert.That(ReadInteger(statistics, "spawnedEnemyCount"),
                Is.EqualTo(8));
            Assert.That(ReadInteger(statistics, "defeatedEnemyCount"),
                Is.EqualTo(8));
            Assert.That(
                ReadMetric(
                    statistics,
                    "killsByEnemyId",
                    "core.enemy.gnawer"),
                Is.EqualTo(8));
        }

        [TestCase("not-triggered", 0, 0, 0, 0, 0)]
        [TestCase("warning", 1, 1, 0, 0, 0)]
        [TestCase("spawning", 2, 1, 1, 0, 1)]
        [TestCase("combat-cleanup", 3, 1, 8, 7, 1)]
        [TestCase("completed", 1, 2, 0, 0, 0)]
        public void EverySchemaThirtyOneTutorialStateMigratesThroughFormalPrepare(
            string legacyState,
            int expectedPhase,
            int expectedWave,
            int expectedWaveSpawned,
            int expectedWaveDefeated,
            int expectedAlive)
        {
            FormalSaveEnvelope source = JsonUtility.FromJson<FormalSaveEnvelope>(
                ReadFixture("schema-31-formal-3d.json"));
            ConfigureLegacyTutorialState(source.formal3D.defense, legacyState);
            source.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(source.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(source));

            Assert.That(decoded.Success, Is.True,
                legacyState + ": " + decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(35));
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            Assert.That(validation.IsValid, Is.True,
                legacyState + ": " + validation.Message);

            FormalThreeDDefenseCampaignSaveData campaign =
                decoded.Envelope.formal3D.defenseCampaign;
            Assert.That(campaign, Is.Not.Null, legacyState);
            Assert.That(campaign.campaignId,
                Is.EqualTo("campaign.single-city-defense.v1"), legacyState);
            Assert.That(campaign.phase, Is.EqualTo(expectedPhase), legacyState);
            Assert.That(campaign.currentWaveNumber,
                Is.EqualTo(expectedWave), legacyState);
            Assert.That(campaign.requestedSpeed, Is.EqualTo(1f), legacyState);
            Assert.That(campaign.lastNonZeroSpeed,
                Is.EqualTo(1f), legacyState);
            Assert.That(campaign.statistics, Is.Not.Null, legacyState);
            Assert.That(campaign.statistics.partialFromMigration, Is.True,
                legacyState +
                " must disclose that schema 31 has only partial history.");
            Assert.That(campaign.enemyStates, Has.Length.EqualTo(expectedAlive),
                legacyState);
            Assert.That(
                ReadEnemyCount(
                    campaign,
                    "spawnedEnemyCountsByEnemyId",
                    "core.enemy.gnawer"),
                Is.EqualTo(expectedWaveSpawned), legacyState);
            Assert.That(
                ReadEnemyCount(
                    campaign,
                    "defeatedEnemyCountsByEnemyId",
                    "core.enemy.gnawer"),
                Is.EqualTo(expectedWaveDefeated), legacyState);
            for (var index = 0; index < campaign.enemyStates.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyStateSaveData enemy =
                    campaign.enemyStates[index];
                Assert.That(
                    enemy.stableEnemyId,
                    Is.EqualTo(
                        "campaign.enemy.wave-01." +
                        enemy.spawnOrder.ToString("0000")),
                    legacyState + " must not retain a tutorial-local ID.");
                Assert.That(enemy.targetStableId,
                    Is.EqualTo(
                        SingleCityDefenseCampaignModel.CityCoreTargetId),
                    legacyState);
            }

            var restored = new SingleCityDefenseCampaignModel(28f, 28f);
            Assert.That(restored.TryPrepareRestore(
                ToCampaignPersistence(campaign),
                out SingleCityDefenseCampaignRestorePlan plan,
                out string error), Is.True,
                legacyState + ": " + error);
            Assert.That(plan, Is.Not.Null,
                legacyState +
                " must reach the formal campaign's transactional prepare.");
            Assert.That(restored.TryCommitRestore(plan, out error), Is.True,
                legacyState + ": " + error);
            SingleCityDefenseCampaignPersistenceState recaptured =
                restored.CaptureForPersistence();
            Assert.That(recaptured.Statistics.PartialFromMigration, Is.True,
                legacyState +
                " must retain partial statistics after formal prepare.");
        }

        [Test]
        public void ValidatorRejectsMissingDefenseCampaign()
        {
            FormalSaveEnvelope envelope = MigratedFixtureEnvelope();
            RequireCampaignField().SetValue(envelope.formal3D, null);
            Rehash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False,
                "Schema 32 must require formal3D.defenseCampaign.");
        }

        [Test]
        public void ValidatorRejectsInvalidCampaignSpeed()
        {
            FormalSaveEnvelope envelope = MigratedFixtureEnvelope();
            object campaign = RequireCampaignField().GetValue(
                envelope.formal3D);
            RequireField(campaign.GetType(), "requestedSpeed").SetValue(
                campaign,
                3f);
            Rehash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False,
                "Requested speed must be normalized to 0, 1, or 2.");
        }

        [Test]
        public void ValidatorRejectsProductionActiveTimeAboveEligibleTime()
        {
            FormalSaveEnvelope envelope = MigratedFixtureEnvelope();
            FormalThreeDDefenseCampaignStatisticsSaveData statistics =
                envelope.formal3D.defenseCampaign.statistics;
            statistics.productionActiveProgressSeconds = 3f;
            statistics.productionEligibleSeconds = 2f;
            Rehash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False,
                "Production active time cannot exceed eligible time.");
        }

        [Test]
        public void ValidatorRejectsCompleteTowerKillTotalMismatch()
        {
            FormalSaveEnvelope envelope = MigratedFixtureEnvelope();
            FormalThreeDDefenseCampaignStatisticsSaveData statistics =
                envelope.formal3D.defenseCampaign.statistics;
            statistics.partialFromMigration = false;
            statistics.defeatedEnemyCount = 2;
            statistics.killsByTowerBuildingId = new[]
            {
                new FormalThreeDDefenseCampaignMetricSaveData
                {
                    stableId = BuildingCatalog.MachineGunTurret.Id.Value,
                    amount = 1,
                },
            };
            Rehash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False,
                "Complete schema 32 statistics cannot claim a tower kill " +
                "total different from total defeated enemies.");
        }

        [Test]
        public void ValidatorRejectsDuplicateCampaignStableIdentity()
        {
            FormalSaveEnvelope envelope = MigratedFixtureEnvelope();
            object campaign = RequireCampaignField().GetValue(
                envelope.formal3D);
            FieldInfo towerField = RequireArrayField(
                campaign.GetType(),
                "towerCombatStates");
            Array source = (Array)towerField.GetValue(campaign);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.Length, Is.GreaterThan(0));
            Array duplicate = Array.CreateInstance(
                towerField.FieldType.GetElementType(),
                2);
            duplicate.SetValue(source.GetValue(0), 0);
            duplicate.SetValue(source.GetValue(0), 1);
            towerField.SetValue(campaign, duplicate);
            Rehash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False,
                "Campaign collections must reject duplicate stable IDs.");
        }

        [TestCase("fixed-remainder")]
        [TestCase("defeated-over-spawned")]
        [TestCase("spawned-over-planned")]
        [TestCase("alive-count-mismatch")]
        [TestCase("next-ordinal-too-low")]
        public void ValidatorRejectsInconsistentCampaignProgress(
            string mutation)
        {
            FormalSaveEnvelope envelope = MigratedFixtureEnvelope();
            FormalThreeDDefenseCampaignSaveData campaign =
                envelope.formal3D.defenseCampaign;
            switch (mutation)
            {
                case "fixed-remainder":
                    campaign.fixedStepAccumulatorSeconds = .1f;
                    break;
                case "defeated-over-spawned":
                    campaign.defeatedEnemyCountsByEnemyId = new[]
                    {
                        new FormalThreeDDefenseCampaignEnemyCountSaveData
                        {
                            enemyId = "core.enemy.gnawer",
                            count = 2,
                        },
                    };
                    break;
                case "spawned-over-planned":
                    campaign.spawnedEnemyCountsByEnemyId[0].count = 9;
                    break;
                case "alive-count-mismatch":
                    campaign.enemyStates = Array.Empty<
                        FormalThreeDDefenseCampaignEnemyStateSaveData>();
                    break;
                case "next-ordinal-too-low":
                    campaign.nextEnemyOrdinal = 0;
                    break;
                default:
                    Assert.Fail("Unknown campaign mutation: " + mutation);
                    break;
            }
            Rehash(envelope);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False,
                "Validator accepted inconsistent campaign state: " +
                mutation);
        }

        [TestCase("plannedEnemyCountsByEnemyId")]
        [TestCase("spawnedEnemyCountsByEnemyId")]
        [TestCase("defeatedEnemyCountsByEnemyId")]
        [TestCase("frozenSpawnAnchors")]
        [TestCase("towerCombatStates")]
        [TestCase("enemyStates")]
        [TestCase("buildingHealthStates")]
        public void CurrentSchemaSourceRequiresEveryCampaignRootArray(
            string fieldName)
        {
            FormalSaveEnvelope envelope = MigratedFixtureEnvelope();
            string encoded = FormalSaveCodec.EncodeEnvelope(envelope);
            string missing = encoded.Replace(
                "\"" + fieldName + "\":[",
                "\"removed" + fieldName + "\":[");
            Assert.That(missing, Is.Not.EqualTo(encoded),
                "The encoded schema 32 fixture did not contain " + fieldName);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(missing);
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateDecoded(decoded);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Error,
                Is.EqualTo(FormalSaveValidationError.MissingRequiredValue),
                "A current-schema campaign array must be explicit even when " +
                "its value is empty: " + fieldName);
            Assert.That(
                result.FieldPath,
                Is.EqualTo("formal3D.defenseCampaign." + fieldName));
        }

        [Test]
        public void SchemaThirtyOneMigrationDefaultsBothSpeedValuesToOne()
        {
            FormalSaveDecodeResult result = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Envelope, Is.Not.Null);
            Assert.That(result.Envelope.saveSchemaVersion, Is.EqualTo(35),
                "The schema 31 fixture must pass through the schema 32 " +
                "campaign migration and schema 33 progression migration " +
                "and schema 34/35 migrations before speed defaults " +
                "are observed.");

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
        public void PausedSchemaThirtyOneMigrationRestoresItsDefaultResumeSpeed()
        {
            FormalSaveEnvelope source = JsonUtility.FromJson<FormalSaveEnvelope>(
                ReadFixture("schema-31-formal-3d.json"));
            source.formal3D.pause.tacticalPaused = true;
            source.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(source.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(source));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            Assert.That(validation.IsValid, Is.True, validation.Message);
            Assert.That(decoded.Envelope.formal3D.defenseCampaign
                .requestedSpeed, Is.EqualTo(1f));
            Assert.That(decoded.Envelope.formal3D.defenseCampaign
                .lastNonZeroSpeed, Is.EqualTo(1f));
            Assert.That(decoded.Envelope.formal3D.defenseCampaign.statistics
                .partialFromMigration, Is.True);

            var speed = new GameSpeedModel();
            var commands = new GrayboxGameSpeedCommandFacade3D(speed);
            var pauseDomain = new GrayboxFormalPauseSaveDomain3D(speed);
            Assert.That(pauseDomain.TryApply(
                decoded.Envelope.formal3D,
                out string error), Is.True, error);
            Assert.That(commands.RequestedSpeed, Is.Zero);
            Assert.That(commands.LastNonZeroSpeed, Is.EqualTo(1f));
            commands.ToggleTacticalPause();
            Assert.That(commands.RequestedSpeed, Is.EqualTo(1f));
            Assert.That(commands.EffectiveSpeed, Is.EqualTo(1f));
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

        private static void RequireEnemyCountArray(
            Type campaignType,
            string fieldName)
        {
            FieldInfo field = RequireArrayField(campaignType, fieldName);
            Type itemType = field.FieldType.GetElementType();
            RequireField(itemType, "enemyId");
            RequireField(itemType, "count");
        }

        private static float ReadSingle(object owner, string fieldName)
        {
            FieldInfo field = RequireField(owner.GetType(), fieldName);
            Assert.That(field.FieldType, Is.EqualTo(typeof(float)),
                owner.GetType().FullName + "." + fieldName +
                " must be a float rule-speed value.");
            return (float)field.GetValue(owner);
        }

        private static bool ReadBoolean(object owner, string fieldName)
        {
            FieldInfo field = RequireField(owner.GetType(), fieldName);
            Assert.That(field.FieldType, Is.EqualTo(typeof(bool)));
            return (bool)field.GetValue(owner);
        }

        private static int ReadInteger(object owner, string fieldName)
        {
            FieldInfo field = RequireField(owner.GetType(), fieldName);
            Assert.That(field.FieldType, Is.EqualTo(typeof(int)));
            return (int)field.GetValue(owner);
        }

        private static object ReadField(object owner, string fieldName)
        {
            Assert.That(owner, Is.Not.Null);
            return RequireField(owner.GetType(), fieldName).GetValue(owner);
        }

        private static Array ReadArray(object owner, string fieldName)
        {
            object value = ReadField(owner, fieldName);
            Assert.That(value, Is.InstanceOf<Array>());
            return (Array)value;
        }

        private static object FindItem(
            Array values,
            string identityField,
            string identity)
        {
            Assert.That(values, Is.Not.Null);
            foreach (object value in values)
            {
                if (value != null && string.Equals(
                    ReadField(value, identityField) as string,
                    identity,
                    StringComparison.Ordinal))
                {
                    return value;
                }
            }
            return null;
        }

        private static object FindItemByEnumName(
            Array values,
            string fieldName,
            string expectedName)
        {
            Assert.That(values, Is.Not.Null);
            foreach (object value in values)
            {
                if (value == null) continue;
                object fieldValue = ReadField(value, fieldName);
                if (fieldValue != null && string.Equals(
                    fieldValue.ToString(),
                    expectedName,
                    StringComparison.Ordinal))
                {
                    return value;
                }
            }
            return null;
        }

        private static int ReadEnemyCount(
            object campaign,
            string arrayFieldName,
            string enemyId)
        {
            object value = FindItem(
                ReadArray(campaign, arrayFieldName),
                "enemyId",
                enemyId);
            return value == null ? 0 : ReadInteger(value, "count");
        }

        private static int ReadMetric(
            object statistics,
            string arrayFieldName,
            string stableId)
        {
            object value = FindItem(
                ReadArray(statistics, arrayFieldName),
                "stableId",
                stableId);
            return value == null ? 0 : ReadInteger(value, "amount");
        }

        private static void AssertHealth(
            Array values,
            string stableInstanceId,
            int expectedHealth)
        {
            object value = FindItem(
                values,
                "stableInstanceId",
                stableInstanceId);
            Assert.That(value, Is.Not.Null,
                "Missing migrated health for " + stableInstanceId + ".");
            Assert.That(
                Convert.ToInt32(ReadField(value, "currentHealth")),
                Is.EqualTo(expectedHealth));
            Assert.That(
                Convert.ToBoolean(ReadField(value, "isDestroyed")),
                Is.False);
        }

        private static void AssertTowerCache(
            Array values,
            string stableInstanceId,
            string expectedResourceId,
            int expectedAmount)
        {
            object value = FindItem(
                values,
                "stableInstanceId",
                stableInstanceId);
            Assert.That(value, Is.Not.Null,
                "Missing migrated tower cache for " + stableInstanceId + ".");
            Assert.That(
                ReadField(value, "consumableId"),
                Is.EqualTo(expectedResourceId));
            Assert.That(
                Convert.ToInt32(ReadField(value, "amount")),
                Is.EqualTo(expectedAmount));
        }

        private static SingleCityDefenseCampaignPersistenceState
            ToCampaignPersistence(
                FormalThreeDDefenseCampaignSaveData source)
        {
            FormalThreeDDefenseCampaignStatisticsSaveData statistics =
                source.statistics;
            return new SingleCityDefenseCampaignPersistenceState(
                source.campaignId,
                (SingleCityDefenseCampaignPhase)source.phase,
                source.currentWaveNumber,
                source.warningRemainingSeconds,
                source.spawnClockSeconds,
                source.fixedStepAccumulatorSeconds,
                source.nextEnemyOrdinal,
                source.coreCurrentHealth,
                (SingleCityDefenseCampaignResult)source.result,
                ToCounts(source.plannedEnemyCountsByEnemyId),
                ToCounts(source.spawnedEnemyCountsByEnemyId),
                ToCounts(source.defeatedEnemyCountsByEnemyId),
                ToAnchors(source.frozenSpawnAnchors),
                ToEnemies(source.enemyStates),
                ToCampaignStatisticsPersistence(statistics));
        }

        private static SingleCityDefenseCampaignStatisticsPersistenceState
            ToCampaignStatisticsPersistence(
                FormalThreeDDefenseCampaignStatisticsSaveData source)
        {
            return new SingleCityDefenseCampaignStatisticsPersistenceState(
                source.elapsedRuleSeconds,
                source.spawnedEnemyCount,
                source.defeatedEnemyCount,
                source.completedWaveCount,
                ToMetrics(source.killsByEnemyId),
                source.highestAliveEnemyCount,
                source.coreDamageTaken,
                ToMetrics(source.damageByTowerBuildingId),
                Array.Empty<
                    SingleCityDefenseCampaignMetricPersistenceState>(),
                ToMetrics(source.consumablesSpentByResourceId),
                SumMetrics(source.buildingLossesByBuildingId),
                ToMetrics(source.buildingLossesByBuildingId),
                source.partialFromMigration);
        }

        private static
            SingleCityDefenseCampaignEnemyCountPersistenceState[] ToCounts(
                FormalThreeDDefenseCampaignEnemyCountSaveData[] source)
        {
            var result =
                new SingleCityDefenseCampaignEnemyCountPersistenceState[
                    source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = new
                    SingleCityDefenseCampaignEnemyCountPersistenceState(
                        source[index].enemyId,
                        source[index].count);
            }
            return result;
        }

        private static
            SingleCityDefenseCampaignSpawnAnchorPersistenceState[] ToAnchors(
                FormalThreeDDefenseCampaignSpawnAnchorSaveData[] source)
        {
            var result =
                new SingleCityDefenseCampaignSpawnAnchorPersistenceState[
                    source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = new
                    SingleCityDefenseCampaignSpawnAnchorPersistenceState(
                        source[index].direction,
                        source[index].positionX,
                        source[index].positionZ);
            }
            return result;
        }

        private static SingleCityDefenseCampaignEnemyPersistenceState[]
            ToEnemies(FormalThreeDDefenseCampaignEnemyStateSaveData[] source)
        {
            var result = new SingleCityDefenseCampaignEnemyPersistenceState[
                source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyStateSaveData enemy =
                    source[index];
                result[index] = new
                    SingleCityDefenseCampaignEnemyPersistenceState(
                        enemy.stableEnemyId,
                        enemy.archetypeId,
                        enemy.spawnOrder,
                        enemy.positionX,
                        enemy.positionZ,
                        enemy.currentHealth,
                        enemy.movementRemainder,
                        enemy.attackDamageRemainder,
                        enemy.targetStableId);
            }
            return result;
        }

        private static SingleCityDefenseCampaignMetricPersistenceState[]
            ToMetrics(FormalThreeDDefenseCampaignMetricSaveData[] source)
        {
            var result =
                new SingleCityDefenseCampaignMetricPersistenceState[
                    source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = new
                    SingleCityDefenseCampaignMetricPersistenceState(
                        source[index].stableId,
                        source[index].amount);
            }
            return result;
        }

        private static int SumMetrics(
            FormalThreeDDefenseCampaignMetricSaveData[] source)
        {
            var total = 0;
            for (var index = 0; index < source.Length; index++)
                total += source[index].amount;
            return total;
        }

        private static FormalSaveEnvelope MigratedFixtureEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope, Is.Not.Null);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(35));
            return decoded.Envelope;
        }

        private static void Rehash(FormalSaveEnvelope envelope)
        {
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
        }

        private static void ConfigureLegacyTutorialState(
            FormalThreeDDefenseSaveData legacy,
            string state)
        {
            legacy.coreCurrentHealth = 1985;
            legacy.fixedStepAccumulatorSeconds = .05f;
            legacy.warningRemainingSeconds = 0f;
            legacy.spawnClockSeconds = 0f;
            legacy.spawnedEnemyCount = 0;
            legacy.defeatedEnemyCount = 0;
            legacy.nextEnemyOrdinal = 0;
            legacy.enemies =
                Array.Empty<FormalThreeDDefenseEnemySaveData>();

            switch (state)
            {
                case "not-triggered":
                    legacy.tutorialTriggered = false;
                    legacy.tutorialWaveTriggerCount = 0;
                    legacy.wavePhase = 0;
                    return;
                case "warning":
                    legacy.tutorialTriggered = true;
                    legacy.tutorialWaveTriggerCount = 1;
                    legacy.wavePhase = 1;
                    legacy.warningRemainingSeconds = 9f;
                    return;
                case "spawning":
                    legacy.tutorialTriggered = true;
                    legacy.tutorialWaveTriggerCount = 1;
                    legacy.wavePhase = 2;
                    legacy.spawnClockSeconds = 1f;
                    legacy.spawnedEnemyCount = 1;
                    legacy.nextEnemyOrdinal = 1;
                    legacy.enemies = new[]
                    {
                        LegacyGnawer(spawnOrder: 0),
                    };
                    return;
                case "combat-cleanup":
                    legacy.tutorialTriggered = true;
                    legacy.tutorialWaveTriggerCount = 1;
                    legacy.wavePhase = 3;
                    legacy.spawnedEnemyCount = 8;
                    legacy.defeatedEnemyCount = 7;
                    legacy.nextEnemyOrdinal = 8;
                    legacy.enemies = new[]
                    {
                        LegacyGnawer(spawnOrder: 7),
                    };
                    return;
                case "completed":
                    legacy.tutorialTriggered = true;
                    legacy.tutorialWaveTriggerCount = 1;
                    legacy.wavePhase = 0;
                    legacy.spawnedEnemyCount = 8;
                    legacy.defeatedEnemyCount = 8;
                    legacy.nextEnemyOrdinal = 8;
                    return;
                default:
                    Assert.Fail("Unknown schema 31 tutorial state: " + state);
                    return;
            }
        }

        private static FormalThreeDDefenseEnemySaveData LegacyGnawer(
            int spawnOrder)
        {
            return new FormalThreeDDefenseEnemySaveData
            {
                stableEnemyId =
                    "core.enemy.gnawer.tutorial." +
                    spawnOrder.ToString("000"),
                archetypeId = "core.enemy.gnawer",
                spawnOrder = spawnOrder,
                positionX = 29.5f,
                positionZ = 28f,
                currentHealth = 60,
                movementRemainder = 0f,
                attackDamageRemainder = .5f,
            };
        }

        private static void AddCompletedTower(
            FormalSaveEnvelope envelope,
            string stableInstanceId,
            BuildingDefinition definition,
            int x,
            int y)
        {
            FormalThreeDBuildingInstanceSaveData[] source =
                envelope.formal3D.buildings.instances;
            var expanded = new FormalThreeDBuildingInstanceSaveData[
                source.Length + 1];
            Array.Copy(source, expanded, source.Length);
            expanded[source.Length] = new FormalThreeDBuildingInstanceSaveData
            {
                stableInstanceId = stableInstanceId,
                definitionId = definition.Id.Value,
                site = 0,
                x = x,
                y = y,
                orientation = 0,
                state = 1,
                constructionRemainingSeconds = 0f,
                isPlayerOwned = true,
                boundResourceNodeId = string.Empty,
                boundNodeX = -1,
                boundNodeY = -1,
                footprintWidth = definition.Width,
                footprintHeight = definition.Height,
                evacuationLockedCrossCheck = false,
            };
            envelope.formal3D.buildings.instances = expanded;
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
