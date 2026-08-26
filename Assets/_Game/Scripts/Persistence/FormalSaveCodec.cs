using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Persistence
{
    public static class FormalSaveCodec
    {
        [Serializable]
        private sealed class IdentityProbe
        {
            public int schema;
            public int saveSchemaVersion;
            public string runtimeKind;
        }

        [Serializable]
        private sealed class SchemaThirtyOnePayload
        {
            public string sessionId;
            public FormalThreeDWorldSaveData world;
            public FormalThreeDCitySaveData city;
            public FormalThreeDBuildingsSaveData buildings;
            public FormalThreeDStorageSaveData storage;
            public FormalThreeDBackpackSaveData backpack;
            public FormalThreeDCraftingSaveData crafting;
            public FormalThreeDResearchSaveData research;
            public FormalThreeDProductionSaveData production;
            public FormalThreeDDefenseSaveData defense;
            public FormalThreeDEvacuationSaveData evacuation;
            public FormalThreeDPauseSaveData pause;
        }

        [Serializable]
        private sealed class SchemaThirtyTwoPayload
        {
            public string sessionId;
            public FormalThreeDWorldSaveData world;
            public FormalThreeDCitySaveData city;
            public FormalThreeDBuildingsSaveData buildings;
            public FormalThreeDStorageSaveData storage;
            public FormalThreeDBackpackSaveData backpack;
            public FormalThreeDCraftingSaveData crafting;
            public FormalThreeDResearchSaveData research;
            public FormalThreeDProductionSaveData production;
            public FormalThreeDDefenseSaveData defense;
            public FormalThreeDDefenseCampaignSaveData defenseCampaign;
            public FormalThreeDEvacuationSaveData evacuation;
            public FormalThreeDPauseSaveData pause;
        }

        public static string Encode(FormalSaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static FormalSaveData Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                FormalSaveData data =
                    JsonUtility.FromJson<FormalSaveData>(json);
                return data != null && data.schema >= 1 && data.schema <= 30
                    ? data
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public static string EncodeEnvelope(FormalSaveEnvelope envelope)
        {
            if (envelope == null) return null;
            var normalized = new FormalSaveEnvelope
            {
                gameVersion = envelope.gameVersion,
                saveSchemaVersion = envelope.saveSchemaVersion,
                contentSources = SortedCopy(envelope.contentSources),
                createdAt = envelope.createdAt,
                updatedAt = envelope.updatedAt,
                runtimeKind = envelope.runtimeKind,
                payloadHashSha256 = envelope.payloadHashSha256,
                checkpoint = CopyCheckpoint(envelope.checkpoint),
                formal3D = CopyPayloadWithCanonicalCampaign(
                    envelope.formal3D),
            };
            return JsonUtility.ToJson(normalized, false);
        }

        public static string ComputePayloadHashSha256(
            FormalThreeDSaveData payload)
        {
            if (payload == null) return null;
            if (payload.defenseCampaign == null ||
                string.IsNullOrWhiteSpace(
                    payload.defenseCampaign.campaignId))
                return ComputeSchemaThirtyOnePayloadHash(payload);
            if (payload.progression == null)
                return ComputeSchemaThirtyTwoPayloadHash(payload);
            return ComputeSchemaThirtyThreePayloadHash(payload);
        }

        private static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                    builder.Append(digest[index].ToString("x2"));
                return builder.ToString();
            }
        }

        public static FormalSaveDecodeResult DecodeEnvelope(string json)
        {
            FormalSaveDecodeResult result = DecodeAny(json);
            if (!result.Success ||
                result.PayloadKind == FormalSavePayloadKind.Formal3D)
            {
                return result;
            }
            return FormalSaveDecodeResult.Failed(
                FormalSaveDecodeError.PayloadKindMismatch,
                "存档不是正式 3D 类型");
        }

        public static FormalSaveDecodeResult DecodeAny(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.BlankDocument,
                    "存档内容为空");
            }

            IdentityProbe probe;
            try
            {
                probe = JsonUtility.FromJson<IdentityProbe>(json);
            }
            catch
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.MalformedJson,
                    "存档内容已损坏");
            }
            if (probe == null)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.MalformedJson,
                    "存档内容已损坏");
            }

            bool hasLegacySchema = ContainsRootMember(json, "schema");
            bool hasEnvelopeSchema =
                ContainsRootMember(json, "saveSchemaVersion");
            if (hasLegacySchema && hasEnvelopeSchema)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.PayloadKindMismatch,
                    "存档类型与数据不一致");
            }
            if (probe.schema > FormalSaveEnvelope.CurrentSchemaVersion ||
                probe.saveSchemaVersion >
                FormalSaveEnvelope.CurrentSchemaVersion)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.UnsupportedFutureSchema,
                    "存档版本过新");
            }
            if (hasLegacySchema && probe.schema >= 1 && probe.schema <= 30)
            {
                FormalSaveData legacy = Decode(json);
                return legacy == null
                    ? FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.MalformedJson,
                        "旧版存档内容已损坏")
                    : FormalSaveDecodeResult.Legacy(legacy, json);
            }
            if (hasEnvelopeSchema &&
                (probe.saveSchemaVersion == 31 ||
                 probe.saveSchemaVersion == 32 ||
                 probe.saveSchemaVersion ==
                    FormalSaveEnvelope.CurrentSchemaVersion))
            {
                if (!string.Equals(
                        probe.runtimeKind,
                        FormalSaveEnvelope.FormalThreeDRuntimeKind,
                        StringComparison.Ordinal))
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.UnknownRuntimeKind,
                        "无法识别存档运行时类型");
                }
                if (!ContainsRootMember(json, "formal3D") ||
                    ContainsRootMember(json, "legacy2D"))
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.PayloadKindMismatch,
                        "存档类型与数据不一致");
                }

                FormalSaveEnvelope envelope;
                try
                {
                    envelope = JsonUtility.FromJson<FormalSaveEnvelope>(json);
                }
                catch
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.MalformedJson,
                        "存档内容已损坏");
                }
                if (envelope == null || envelope.formal3D == null)
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.PayloadKindMismatch,
                        "存档类型与数据不一致");
                }
                if (probe.saveSchemaVersion == 31)
                {
                    string legacyHash = ComputeSchemaThirtyOnePayloadHash(
                        envelope.formal3D);
                    if (!string.Equals(
                            legacyHash,
                            envelope.payloadHashSha256,
                            StringComparison.Ordinal))
                    {
                        return FormalSaveDecodeResult.Failed(
                            FormalSaveDecodeError.MalformedJson,
                            "旧版存档校验失败");
                    }
                    envelope = MigrateSchemaThirtyOneToThirtyTwo(envelope);
                    envelope = MigrateSchemaThirtyTwoToThirtyThree(envelope);
                }
                else if (probe.saveSchemaVersion == 32)
                {
                    string legacyHash = ComputeSchemaThirtyTwoPayloadHash(
                        envelope.formal3D);
                    if (!string.Equals(
                            legacyHash,
                            envelope.payloadHashSha256,
                            StringComparison.Ordinal))
                    {
                        return FormalSaveDecodeResult.Failed(
                            FormalSaveDecodeError.MalformedJson,
                            "旧版存档校验失败");
                    }
                    envelope = MigrateSchemaThirtyTwoToThirtyThree(envelope);
                }
                return FormalSaveDecodeResult.ThreeD(envelope, json);
            }

            if (probe.schema == FormalSaveEnvelope.CurrentSchemaVersion ||
                probe.saveSchemaVersion != 0)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.PayloadKindMismatch,
                    "存档类型与数据不一致");
            }
            return FormalSaveDecodeResult.Failed(
                FormalSaveDecodeError.UnsupportedSchema,
                "无法识别存档版本");
        }

        public static string FormatUtcTimestamp(DateTime value)
        {
            return value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture);
        }

        public static void EnsureCurrentCampaignState(
            FormalThreeDSaveData payload,
            FormalSaveCheckpointMetadata checkpoint)
        {
            if (payload == null ||
                (payload.defenseCampaign != null &&
                 !string.IsNullOrWhiteSpace(
                     payload.defenseCampaign.campaignId)))
            {
                return;
            }

            EnsureCampaignState(payload, checkpoint);
        }

        public static FormalThreeDDefenseCampaignSaveData CloneCampaignState(
            FormalThreeDDefenseCampaignSaveData source)
        {
            return CopyCampaign(source);
        }

        private static string[] SortedCopy(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();
            var result = (string[])values.Clone();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static string ComputeSchemaThirtyOnePayloadHash(
            FormalThreeDSaveData source)
        {
            var legacy = new SchemaThirtyOnePayload
            {
                sessionId = source.sessionId,
                world = source.world,
                city = source.city,
                buildings = source.buildings,
                storage = source.storage,
                backpack = source.backpack,
                crafting = source.crafting,
                research = source.research,
                production = source.production,
                defense = source.defense,
                evacuation = source.evacuation,
                pause = source.pause,
            };
            return ComputeSha256(JsonUtility.ToJson(legacy, false));
        }

        private static string ComputeSchemaThirtyTwoPayloadHash(
            FormalThreeDSaveData source)
        {
            FormalThreeDSaveData canonical =
                CopyPayloadWithCanonicalCampaign(source);
            var legacy = new SchemaThirtyTwoPayload
            {
                sessionId = canonical.sessionId,
                world = canonical.world,
                city = canonical.city,
                buildings = canonical.buildings,
                storage = canonical.storage,
                backpack = canonical.backpack,
                crafting = canonical.crafting,
                research = canonical.research,
                production = canonical.production,
                defense = canonical.defense,
                defenseCampaign = canonical.defenseCampaign,
                evacuation = canonical.evacuation,
                pause = canonical.pause,
            };
            return ComputeSha256(JsonUtility.ToJson(legacy, false));
        }

        private static string ComputeSchemaThirtyThreePayloadHash(
            FormalThreeDSaveData source)
        {
            return ComputeSha256(JsonUtility.ToJson(
                CopyPayloadWithCanonicalCampaign(source),
                false));
        }

        private static FormalSaveCheckpointMetadata CopyCheckpoint(
            FormalSaveCheckpointMetadata source)
        {
            return source == null
                ? null
                : new FormalSaveCheckpointMetadata
                {
                    sequence = source.sequence,
                    reasonId = source.reasonId,
                    ruleTimeSeconds = source.ruleTimeSeconds,
                    completedMilestoneIds = SortedCopy(
                        source.completedMilestoneIds),
                };
        }

        private static FormalThreeDSaveData CopyPayloadWithCanonicalCampaign(
            FormalThreeDSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDSaveData
            {
                sessionId = source.sessionId,
                world = source.world,
                city = source.city,
                buildings = source.buildings,
                storage = source.storage,
                backpack = source.backpack,
                crafting = source.crafting,
                research = source.research,
                production = source.production,
                defense = source.defense,
                defenseCampaign = CopyCampaign(source.defenseCampaign),
                evacuation = source.evacuation,
                pause = source.pause,
                progression = CopyProgression(source.progression),
            };
        }

        private static FormalThreeDProgressionSaveData CopyProgression(
            FormalThreeDProgressionSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDProgressionSaveData
            {
                configurationSignature = source.configurationSignature,
                attention = CopyAttention(source.attention),
                fate = CopyFate(source.fate),
                fateEffects = CopyFateEffects(source.fateEffects),
                civilization = CopyCivilization(source.civilization),
            };
        }

        private static FormalThreeDAttentionSaveData CopyAttention(
            FormalThreeDAttentionSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDAttentionSaveData
            {
                value = source.value,
                revision = source.revision,
                history = CopyAttentionHistory(source.history),
                reachedThresholds = SortedCopy(source.reachedThresholds),
                committedStableEventKeys = SortedCopy(
                    source.committedStableEventKeys),
                completedOneShotReasonIds = SortedCopy(
                    source.completedOneShotReasonIds),
            };
        }

        private static FormalThreeDAttentionHistorySaveData[]
            CopyAttentionHistory(
                FormalThreeDAttentionHistorySaveData[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<FormalThreeDAttentionHistorySaveData>();
            var result =
                new FormalThreeDAttentionHistorySaveData[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDAttentionHistorySaveData item = source[index];
                result[index] = item == null
                    ? null
                    : new FormalThreeDAttentionHistorySaveData
                    {
                        reasonId = item.reasonId,
                        stableEventKey = item.stableEventKey,
                        requestedDelta = item.requestedDelta,
                        appliedDelta = item.appliedDelta,
                        valueAfter = item.valueAfter,
                        revision = item.revision,
                        ruleTimeSeconds = item.ruleTimeSeconds,
                        sourceInstanceId = item.sourceInstanceId,
                    };
            }
            return result;
        }

        private static FormalThreeDFateSaveData CopyFate(
            FormalThreeDFateSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDFateSaveData
            {
                offeredIds = source.offeredIds == null
                    ? Array.Empty<string>()
                    : (string[])source.offeredIds.Clone(),
                selectedId = source.selectedId,
                level = source.level,
                revision = source.revision,
            };
        }

        private static FormalThreeDCivilizationSaveData CopyCivilization(
            FormalThreeDCivilizationSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDCivilizationSaveData
            {
                level = source.level,
                committedAscensionIds = SortedCopy(
                    source.committedAscensionIds),
            };
        }

        private static FormalThreeDFateEffectsSaveData CopyFateEffects(
            FormalThreeDFateEffectsSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDFateEffectsSaveData
            {
                pocketUniverse = CopyPocketUniverse(source.pocketUniverse),
                voidDebt = CopyVoidDebt(source.voidDebt),
                rewindAnchors = CopyRewindAnchors(source.rewindAnchors),
            };
        }

        private static FormalThreeDPocketUniverseSaveData CopyPocketUniverse(
            FormalThreeDPocketUniverseSaveData source)
        {
            if (source == null) return null;
            FormalThreeDPocketUniverseFlagshipSaveData[] flagships =
                source.flagships == null
                    ? null
                    : new FormalThreeDPocketUniverseFlagshipSaveData[
                        source.flagships.Length];
            if (flagships != null)
            {
                for (var index = 0; index < flagships.Length; index++)
                {
                    FormalThreeDPocketUniverseFlagshipSaveData item =
                        source.flagships[index];
                    flagships[index] = item == null
                        ? null
                        : new FormalThreeDPocketUniverseFlagshipSaveData
                        {
                            buildingDefinitionId = item.buildingDefinitionId,
                            stableInstanceId = item.stableInstanceId,
                        };
                }
            }
            return new FormalThreeDPocketUniverseSaveData
            {
                level = source.level,
                revision = source.revision,
                flagships = flagships,
                collapsedFlagshipIds = source.collapsedFlagshipIds == null
                    ? null
                    : (string[])source.collapsedFlagshipIds.Clone(),
                firstProductionFlagshipId =
                    source.firstProductionFlagshipId,
            };
        }

        private static FormalThreeDVoidDebtSaveData CopyVoidDebt(
            FormalThreeDVoidDebtSaveData source)
        {
            if (source == null) return null;
            FormalThreeDVoidDebtEntrySaveData[] debts = source.debts == null
                ? null
                : new FormalThreeDVoidDebtEntrySaveData[source.debts.Length];
            if (debts != null)
            {
                for (var index = 0; index < debts.Length; index++)
                {
                    FormalThreeDVoidDebtEntrySaveData item =
                        source.debts[index];
                    debts[index] = item == null
                        ? null
                        : new FormalThreeDVoidDebtEntrySaveData
                        {
                            resourceId = item.resourceId,
                            amount = item.amount,
                        };
                }
            }
            return new FormalThreeDVoidDebtSaveData
            {
                level = source.level,
                settlementRemainingSeconds =
                    source.settlementRemainingSeconds,
                nextSettlementOrdinal = source.nextSettlementOrdinal,
                revision = source.revision,
                debts = debts,
            };
        }

        private static FormalThreeDRewindAnchorMetadataSaveData
            CopyRewindAnchors(FormalThreeDRewindAnchorMetadataSaveData source)
        {
            if (source == null) return null;
            FormalThreeDRewindAnchorEntrySaveData[] anchors =
                source.anchors == null
                    ? null
                    : new FormalThreeDRewindAnchorEntrySaveData[
                        source.anchors.Length];
            if (anchors != null)
            {
                for (var index = 0; index < anchors.Length; index++)
                {
                    FormalThreeDRewindAnchorEntrySaveData item =
                        source.anchors[index];
                    anchors[index] = item == null
                        ? null
                        : new FormalThreeDRewindAnchorEntrySaveData
                        {
                            stableAnchorId = item.stableAnchorId,
                            internalKey = item.internalKey,
                            creationOrdinal = item.creationOrdinal,
                            sessionId = item.sessionId,
                            payloadHashSha256 = item.payloadHashSha256,
                            checkpointSequence = item.checkpointSequence,
                            checkpointReasonId = item.checkpointReasonId,
                            checkpointRuleTimeSeconds =
                                item.checkpointRuleTimeSeconds,
                            completedMilestoneIds =
                                item.completedMilestoneIds == null
                                    ? null
                                    : (string[])item.completedMilestoneIds
                                        .Clone(),
                        };
                }
            }
            return new FormalThreeDRewindAnchorMetadataSaveData
            {
                revision = source.revision,
                nextCreationOrdinal = source.nextCreationOrdinal,
                anchors = anchors,
            };
        }

        private static int[] SortedCopy(int[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<int>();
            var result = (int[])values.Clone();
            Array.Sort(result);
            return result;
        }

        private static FormalThreeDDefenseCampaignSaveData CopyCampaign(
            FormalThreeDDefenseCampaignSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDDefenseCampaignSaveData
            {
                campaignId = source.campaignId,
                phase = source.phase,
                currentWaveNumber = source.currentWaveNumber,
                plannedEnemyCountsByEnemyId = SortedEnemyCounts(
                    source.plannedEnemyCountsByEnemyId),
                spawnedEnemyCountsByEnemyId = SortedEnemyCounts(
                    source.spawnedEnemyCountsByEnemyId),
                defeatedEnemyCountsByEnemyId = SortedEnemyCounts(
                    source.defeatedEnemyCountsByEnemyId),
                frozenSpawnAnchors = SortedSpawnAnchors(
                    source.frozenSpawnAnchors),
                warningRemainingSeconds = source.warningRemainingSeconds,
                spawnClockSeconds = source.spawnClockSeconds,
                fixedStepAccumulatorSeconds =
                    source.fixedStepAccumulatorSeconds,
                nextEnemyOrdinal = source.nextEnemyOrdinal,
                coreCurrentHealth = source.coreCurrentHealth,
                requestedSpeed = source.requestedSpeed,
                lastNonZeroSpeed = source.lastNonZeroSpeed,
                result = source.result,
                statistics = CopyStatistics(source.statistics),
                towerCombatStates = SortedTowerCombatStates(
                    source.towerCombatStates),
                enemyStates = SortedEnemyStates(source.enemyStates),
                buildingHealthStates = SortedBuildingHealthStates(
                    source.buildingHealthStates),
            };
        }

        private static FormalThreeDDefenseCampaignStatisticsSaveData
            CopyStatistics(
                FormalThreeDDefenseCampaignStatisticsSaveData source)
        {
            if (source == null) return null;
            return new FormalThreeDDefenseCampaignStatisticsSaveData
            {
                elapsedRuleSeconds = source.elapsedRuleSeconds,
                spawnedEnemyCount = source.spawnedEnemyCount,
                defeatedEnemyCount = source.defeatedEnemyCount,
                completedWaveCount = source.completedWaveCount,
                killsByEnemyId = SortedMetrics(source.killsByEnemyId),
                highestAliveEnemyCount = source.highestAliveEnemyCount,
                coreDamageTaken = source.coreDamageTaken,
                buildingLossesByBuildingId = SortedMetrics(
                    source.buildingLossesByBuildingId),
                damageByTowerBuildingId = SortedMetrics(
                    source.damageByTowerBuildingId),
                killsByTowerBuildingId = SortedMetrics(
                    source.killsByTowerBuildingId),
                consumablesSpentByResourceId = SortedMetrics(
                    source.consumablesSpentByResourceId),
                completedProductionBatchCount =
                    source.completedProductionBatchCount,
                productionActiveProgressSeconds =
                    source.productionActiveProgressSeconds,
                productionEligibleSeconds =
                    source.productionEligibleSeconds,
                cityWasPackedAfterCampaignStart =
                    source.cityWasPackedAfterCampaignStart,
                developmentModifierUsed = source.developmentModifierUsed,
                partialFromMigration = source.partialFromMigration,
            };
        }

        private static FormalThreeDDefenseCampaignEnemyCountSaveData[]
            MigratedPlannedCounts(FormalThreeDDefenseSaveData legacy)
        {
            if (legacy == null || !legacy.tutorialTriggered)
                return Array.Empty<
                    FormalThreeDDefenseCampaignEnemyCountSaveData>();
            return GnawerCounts(IsCompletedTutorial(legacy) ? 10 : 8);
        }

        private static FormalThreeDDefenseCampaignEnemyCountSaveData[]
            GnawerCounts(int amount)
        {
            return amount <= 0
                ? Array.Empty<
                    FormalThreeDDefenseCampaignEnemyCountSaveData>()
                : new[]
                {
                    new FormalThreeDDefenseCampaignEnemyCountSaveData
                    {
                        enemyId = EnemyCatalog.Gnawer.Id.Value,
                        count = amount,
                    },
                };
        }

        private static FormalThreeDDefenseCampaignEnemyCountSaveData[]
            MigratedDefeatedCounts(FormalThreeDDefenseSaveData legacy)
        {
            if (legacy == null || IsCompletedTutorial(legacy) ||
                legacy.spawnedEnemyCount <= 0)
            {
                return Array.Empty<
                    FormalThreeDDefenseCampaignEnemyCountSaveData>();
            }
            return new[]
            {
                new FormalThreeDDefenseCampaignEnemyCountSaveData
                {
                    enemyId = EnemyCatalog.Gnawer.Id.Value,
                    count = Math.Max(0, legacy.defeatedEnemyCount),
                },
            };
        }

        private static FormalThreeDDefenseCampaignMetricSaveData[] Metrics(
            string stableId,
            int amount)
        {
            return amount <= 0
                ? Array.Empty<FormalThreeDDefenseCampaignMetricSaveData>()
                : new[]
                {
                    new FormalThreeDDefenseCampaignMetricSaveData
                    {
                        stableId = stableId,
                        amount = amount,
                    },
                };
        }

        private static FormalThreeDDefenseCampaignSpawnAnchorSaveData[]
            MigrateSpawnAnchors(FormalThreeDDefenseSaveData legacy)
        {
            if (legacy == null || !legacy.tutorialTriggered)
            {
                return Array.Empty<
                    FormalThreeDDefenseCampaignSpawnAnchorSaveData>();
            }
            return new[]
            {
                new FormalThreeDDefenseCampaignSpawnAnchorSaveData
                {
                    direction = CampaignSpawnDirection.East,
                    positionX = legacy.spawnOriginX,
                    positionZ = legacy.spawnOriginZ,
                },
            };
        }

        private static
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
            MigrateBuildingHealthStates(FormalThreeDBuildingsSaveData buildings)
        {
            var result = new List<
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData>();
            FormalThreeDBuildingInstanceSaveData[] instances =
                buildings?.instances;
            if (instances != null)
            for (int index = 0; index < instances.Length; index++)
            {
                FormalThreeDBuildingInstanceSaveData instance =
                    instances[index];
                BuildingDefinition definition = instance == null
                    ? null
                    : FindBuildingDefinition(instance.definitionId);
                if (definition == null || instance.state != 1 ||
                    !instance.isPlayerOwned)
                {
                    continue;
                }
                result.Add(new
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                    {
                        stableInstanceId = instance.stableInstanceId,
                        currentHealth = definition.MaximumHealth,
                        isDestroyed = false,
                    });
            }
            result.Sort(CompareBuildingHealthState);
            return result.ToArray();
        }

        private static DefenseTowerDefinition FormalTower(string buildingId)
        {
            if (!string.Equals(
                    buildingId,
                    BuildingCatalog.MachineGunTurret.Id.Value,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    buildingId,
                    BuildingCatalog.LaserTower.Id.Value,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    buildingId,
                    BuildingCatalog.SporeTower.Id.Value,
                    StringComparison.Ordinal))
            {
                return null;
            }
            return DefenseTowerCatalog.For(buildingId);
        }

        private static BuildingDefinition FindBuildingDefinition(string id)
        {
            for (int index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition definition = BuildingCatalog.All[index];
                if (string.Equals(
                        definition.Id.Value,
                        id,
                        StringComparison.Ordinal))
                    return definition;
            }
            return null;
        }

        private static FormalSaveEnvelope MigrateSchemaThirtyOneToThirtyTwo(
            FormalSaveEnvelope envelope)
        {
            EnsureCampaignState(envelope.formal3D, envelope.checkpoint);
            envelope.saveSchemaVersion = 32;
            envelope.payloadHashSha256 = ComputeSchemaThirtyTwoPayloadHash(
                envelope.formal3D);
            return envelope;
        }

        private static FormalSaveEnvelope MigrateSchemaThirtyTwoToThirtyThree(
            FormalSaveEnvelope envelope)
        {
            envelope.formal3D.progression = CreateCleanProgressionState();
            envelope.saveSchemaVersion = 33;
            envelope.payloadHashSha256 =
                ComputeSchemaThirtyThreePayloadHash(envelope.formal3D);
            return envelope;
        }

        private static FormalThreeDProgressionSaveData
            CreateCleanProgressionState()
        {
            return new FormalThreeDProgressionSaveData
            {
                configurationSignature =
                    FormalThreeDProgressionSaveData.ConfigurationSignature,
                attention = new FormalThreeDAttentionSaveData
                {
                    value = FormalAttentionCatalog.InitialValue,
                    revision = 0,
                    history = Array.Empty<
                        FormalThreeDAttentionHistorySaveData>(),
                    reachedThresholds = Array.Empty<int>(),
                    committedStableEventKeys = Array.Empty<string>(),
                    completedOneShotReasonIds = Array.Empty<string>(),
                },
                fate = new FormalThreeDFateSaveData
                {
                    offeredIds = new[]
                    {
                        FormalFateCatalog.PocketUniverseId,
                        FormalFateCatalog.VoidDebtId,
                        FormalFateCatalog.RewindAnchorId,
                    },
                    selectedId = string.Empty,
                    level = 0,
                    revision = 0,
                },
                fateEffects = new FormalThreeDFateEffectsSaveData(),
                civilization = new FormalThreeDCivilizationSaveData
                {
                    level = 1,
                    committedAscensionIds = Array.Empty<string>(),
                },
            };
        }

        private static void EnsureCampaignState(
            FormalThreeDSaveData payload,
            FormalSaveCheckpointMetadata checkpoint)
        {
            if (payload == null ||
                (payload.defenseCampaign != null &&
                 !string.IsNullOrWhiteSpace(
                     payload.defenseCampaign.campaignId)))
            {
                return;
            }

            FormalThreeDDefenseSaveData legacy = payload.defense;
            var campaign = new FormalThreeDDefenseCampaignSaveData
            {
                campaignId = "campaign.single-city-defense.v1",
                phase = MigratedCampaignPhase(legacy),
                currentWaveNumber = MigratedWaveNumber(legacy),
                plannedEnemyCountsByEnemyId = MigratedPlannedCounts(legacy),
                spawnedEnemyCountsByEnemyId = GnawerCounts(
                    IsCompletedTutorial(legacy)
                        ? 0
                        : legacy?.spawnedEnemyCount ?? 0),
                defeatedEnemyCountsByEnemyId =
                    MigratedDefeatedCounts(legacy),
                frozenSpawnAnchors = MigrateSpawnAnchors(legacy),
                warningRemainingSeconds = MigratedWarningSeconds(legacy),
                spawnClockSeconds = legacy?.spawnClockSeconds ?? 0f,
                fixedStepAccumulatorSeconds =
                    legacy?.fixedStepAccumulatorSeconds ?? 0f,
                nextEnemyOrdinal = IsCompletedTutorial(legacy)
                    ? 0
                    : legacy?.nextEnemyOrdinal ?? 0,
                coreCurrentHealth = legacy != null &&
                    legacy.tutorialTriggered
                        ? legacy.coreCurrentHealth
                        : CityCoreCombatModel.FormalMaximumHealth,
                requestedSpeed = 1f,
                lastNonZeroSpeed = 1f,
                result = legacy != null &&
                    legacy.tutorialTriggered &&
                    legacy.coreCurrentHealth <= 0
                    ? 2
                    : 0,
                statistics = new
                    FormalThreeDDefenseCampaignStatisticsSaveData
                    {
                        elapsedRuleSeconds = Math.Max(
                            0f,
                            checkpoint?.ruleTimeSeconds ?? 0f),
                        spawnedEnemyCount =
                            legacy?.spawnedEnemyCount ?? 0,
                        defeatedEnemyCount =
                            legacy?.defeatedEnemyCount ?? 0,
                        completedWaveCount = IsCompletedTutorial(legacy)
                            ? 1
                            : 0,
                        highestAliveEnemyCount =
                            legacy?.enemies?.Length ?? 0,
                        killsByEnemyId = Metrics(
                            EnemyCatalog.Gnawer.Id.Value,
                            legacy?.defeatedEnemyCount ?? 0),
                        coreDamageTaken = legacy != null &&
                            legacy.tutorialTriggered
                                ? Math.Max(
                                    0,
                                    CityCoreCombatModel.FormalMaximumHealth -
                                        legacy.coreCurrentHealth)
                                : 0,
                        partialFromMigration = true,
                    },
                towerCombatStates = MigrateTowerCombatStates(
                    payload.buildings,
                    legacy),
                enemyStates = IsCompletedTutorial(legacy)
                    ? Array.Empty<
                        FormalThreeDDefenseCampaignEnemyStateSaveData>()
                    : MigrateEnemyStates(legacy),
                buildingHealthStates = MigrateBuildingHealthStates(
                    payload.buildings),
            };

            payload.defenseCampaign = campaign;
        }

        private static int MigratedCampaignPhase(
            FormalThreeDDefenseSaveData legacy)
        {
            if (legacy == null || !legacy.tutorialTriggered) return 0;
            if (legacy.coreCurrentHealth <= 0) return 5;
            if (legacy.wavePhase >= 1 && legacy.wavePhase <= 3)
                return legacy.wavePhase;
            return 1;
        }

        private static int MigratedWaveNumber(
            FormalThreeDDefenseSaveData legacy)
        {
            if (legacy == null || !legacy.tutorialTriggered) return 0;
            return IsCompletedTutorial(legacy) ? 2 : 1;
        }

        private static float MigratedWarningSeconds(
            FormalThreeDDefenseSaveData legacy)
        {
            if (IsCompletedTutorial(legacy)) return 20f;
            return legacy?.warningRemainingSeconds ?? 0f;
        }

        private static bool IsCompletedTutorial(
            FormalThreeDDefenseSaveData legacy)
        {
            return legacy != null &&
                   legacy.tutorialTriggered &&
                   legacy.wavePhase == 0 &&
                   legacy.tutorialWaveTriggerCount > 0;
        }

        private static
            FormalThreeDDefenseCampaignTowerCombatStateSaveData[]
            MigrateTowerCombatStates(
                FormalThreeDBuildingsSaveData buildings,
                FormalThreeDDefenseSaveData legacy)
        {
            var sourceById = new Dictionary<string,
                FormalThreeDDefenseTowerSaveData>(StringComparer.Ordinal);
            FormalThreeDDefenseTowerSaveData[] source = legacy?.towers;
            if (source != null)
            {
                for (int index = 0; index < source.Length; index++)
                {
                    FormalThreeDDefenseTowerSaveData item = source[index];
                    if (item != null &&
                        !string.IsNullOrWhiteSpace(item.stableInstanceId))
                        sourceById[item.stableInstanceId] = item;
                }
            }

            var result = new List<
                FormalThreeDDefenseCampaignTowerCombatStateSaveData>();
            FormalThreeDBuildingInstanceSaveData[] instances =
                buildings?.instances;
            if (instances != null)
            for (int index = 0; index < instances.Length; index++)
            {
                FormalThreeDBuildingInstanceSaveData building =
                    instances[index];
                DefenseTowerDefinition definition = building == null
                    ? null
                    : FormalTower(building.definitionId);
                if (definition == null || building.state != 1 ||
                    !building.isPlayerOwned)
                {
                    continue;
                }
                sourceById.TryGetValue(
                    building.stableInstanceId,
                    out FormalThreeDDefenseTowerSaveData item);
                result.Add(new
                    FormalThreeDDefenseCampaignTowerCombatStateSaveData
                    {
                        stableInstanceId = building.stableInstanceId,
                        consumableId = definition.ConsumableId,
                        amount = item?.ammunitionAmount ?? 0,
                        isPlayerPaused = item?.isPlayerPaused ?? false,
                        activeConsumableSeconds =
                            item?.activeAmmunitionSeconds ?? 0f,
                        damageRemainder = item?.damageRemainder ?? 0f,
                    });
            }
            result.Sort(CompareTowerCombatState);
            return result.ToArray();
        }

        private static FormalThreeDDefenseCampaignEnemyStateSaveData[]
            MigrateEnemyStates(FormalThreeDDefenseSaveData legacy)
        {
            FormalThreeDDefenseEnemySaveData[] source = legacy?.enemies;
            if (source == null || source.Length == 0)
            {
                return Array.Empty<
                    FormalThreeDDefenseCampaignEnemyStateSaveData>();
            }

            var result = new
                FormalThreeDDefenseCampaignEnemyStateSaveData[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseEnemySaveData item = source[index];
                result[index] = item == null
                    ? null
                    : new FormalThreeDDefenseCampaignEnemyStateSaveData
                    {
                        // Schema 31 tutorial identities were runtime-local
                        // (`core.enemy.gnawer.tutorial.000`) and do not match
                        // the formal campaign restore contract. Wave one is
                        // the only possible active legacy wave, so its
                        // canonical identity is derived from the persisted
                        // spawn order rather than the obsolete text ID.
                        stableEnemyId = CampaignEnemyStableId(
                            waveNumber: 1,
                            item.spawnOrder),
                        archetypeId = string.IsNullOrWhiteSpace(
                            item.archetypeId)
                                ? EnemyCatalog.Gnawer.Id.Value
                                : item.archetypeId,
                        spawnOrder = item.spawnOrder,
                        positionX = item.positionX,
                        positionZ = item.positionZ,
                        currentHealth = item.currentHealth,
                        movementRemainder = item.movementRemainder,
                        attackDamageRemainder =
                            item.attackDamageRemainder,
                        targetStableId =
                            SingleCityDefenseCampaignModel.CityCoreTargetId,
                    };
            }
            Array.Sort(result, CompareEnemyState);
            return result;
        }

        private static string CampaignEnemyStableId(
            int waveNumber,
            int spawnOrder)
        {
            return "campaign.enemy.wave-" +
                waveNumber.ToString("00", CultureInfo.InvariantCulture) +
                "." + spawnOrder.ToString(
                    "0000",
                    CultureInfo.InvariantCulture);
        }

        private static
            FormalThreeDDefenseCampaignTowerCombatStateSaveData[]
            SortedTowerCombatStates(
                FormalThreeDDefenseCampaignTowerCombatStateSaveData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<
                    FormalThreeDDefenseCampaignTowerCombatStateSaveData>();
            }
            var result = new
                FormalThreeDDefenseCampaignTowerCombatStateSaveData[
                    source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignTowerCombatStateSaveData value =
                    source[index];
                result[index] = value == null
                    ? null
                    : new
                        FormalThreeDDefenseCampaignTowerCombatStateSaveData
                        {
                            stableInstanceId = value.stableInstanceId,
                            consumableId = value.consumableId,
                            amount = value.amount,
                            isPlayerPaused = value.isPlayerPaused,
                            activeConsumableSeconds =
                                value.activeConsumableSeconds,
                            damageRemainder = value.damageRemainder,
                            targetStableEnemyId =
                                value.targetStableEnemyId,
                        };
            }
            Array.Sort(result, CompareTowerCombatState);
            return result;
        }

        private static FormalThreeDDefenseCampaignEnemyCountSaveData[]
            SortedEnemyCounts(
                FormalThreeDDefenseCampaignEnemyCountSaveData[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<
                    FormalThreeDDefenseCampaignEnemyCountSaveData>();
            var result =
                new FormalThreeDDefenseCampaignEnemyCountSaveData[
                    source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyCountSaveData value =
                    source[index];
                result[index] = value == null
                    ? null
                    : new FormalThreeDDefenseCampaignEnemyCountSaveData
                    {
                        enemyId = value.enemyId,
                        count = value.count,
                    };
            }
            Array.Sort(result, (left, right) =>
                StringComparer.Ordinal.Compare(
                    left?.enemyId ?? string.Empty,
                    right?.enemyId ?? string.Empty));
            return result;
        }

        private static FormalThreeDDefenseCampaignSpawnAnchorSaveData[]
            SortedSpawnAnchors(
                FormalThreeDDefenseCampaignSpawnAnchorSaveData[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<
                    FormalThreeDDefenseCampaignSpawnAnchorSaveData>();
            var result =
                new FormalThreeDDefenseCampaignSpawnAnchorSaveData[
                    source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignSpawnAnchorSaveData value =
                    source[index];
                result[index] = value == null
                    ? null
                    : new FormalThreeDDefenseCampaignSpawnAnchorSaveData
                    {
                        direction = value.direction,
                        positionX = value.positionX,
                        positionZ = value.positionZ,
                    };
            }
            Array.Sort(result, (left, right) =>
                (left?.direction ?? CampaignSpawnDirection.East).CompareTo(
                    right?.direction ?? CampaignSpawnDirection.East));
            return result;
        }

        private static FormalThreeDDefenseCampaignMetricSaveData[]
            SortedMetrics(FormalThreeDDefenseCampaignMetricSaveData[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<
                    FormalThreeDDefenseCampaignMetricSaveData>();
            var result =
                new FormalThreeDDefenseCampaignMetricSaveData[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignMetricSaveData value =
                    source[index];
                result[index] = value == null
                    ? null
                    : new FormalThreeDDefenseCampaignMetricSaveData
                    {
                        stableId = value.stableId,
                        amount = value.amount,
                    };
            }
            Array.Sort(result, (left, right) =>
                StringComparer.Ordinal.Compare(
                    left?.stableId ?? string.Empty,
                    right?.stableId ?? string.Empty));
            return result;
        }

        private static FormalThreeDDefenseCampaignEnemyStateSaveData[]
            SortedEnemyStates(
                FormalThreeDDefenseCampaignEnemyStateSaveData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<
                    FormalThreeDDefenseCampaignEnemyStateSaveData>();
            }
            var result = new
                FormalThreeDDefenseCampaignEnemyStateSaveData[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyStateSaveData value =
                    source[index];
                result[index] = value == null
                    ? null
                    : new FormalThreeDDefenseCampaignEnemyStateSaveData
                    {
                        stableEnemyId = value.stableEnemyId,
                        archetypeId = value.archetypeId,
                        spawnOrder = value.spawnOrder,
                        positionX = value.positionX,
                        positionZ = value.positionZ,
                        currentHealth = value.currentHealth,
                        movementRemainder = value.movementRemainder,
                        attackDamageRemainder =
                            value.attackDamageRemainder,
                        targetStableId = value.targetStableId,
                    };
            }
            Array.Sort(result, CompareEnemyState);
            return result;
        }

        private static
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
            SortedBuildingHealthStates(
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
                    source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData>();
            }
            var result = new
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData[
                    source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData value =
                    source[index];
                result[index] = value == null
                    ? null
                    : new
                        FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                        {
                            stableInstanceId = value.stableInstanceId,
                            currentHealth = value.currentHealth,
                            isDestroyed = value.isDestroyed,
                        };
            }
            Array.Sort(result, CompareBuildingHealthState);
            return result;
        }

        private static int CompareTowerCombatState(
            FormalThreeDDefenseCampaignTowerCombatStateSaveData left,
            FormalThreeDDefenseCampaignTowerCombatStateSaveData right)
        {
            return StringComparer.Ordinal.Compare(
                left?.stableInstanceId ?? string.Empty,
                right?.stableInstanceId ?? string.Empty);
        }

        private static int CompareEnemyState(
            FormalThreeDDefenseCampaignEnemyStateSaveData left,
            FormalThreeDDefenseCampaignEnemyStateSaveData right)
        {
            return StringComparer.Ordinal.Compare(
                left?.stableEnemyId ?? string.Empty,
                right?.stableEnemyId ?? string.Empty);
        }

        private static int CompareBuildingHealthState(
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData left,
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData right)
        {
            return StringComparer.Ordinal.Compare(
                left?.stableInstanceId ?? string.Empty,
                right?.stableInstanceId ?? string.Empty);
        }

        private static bool ContainsRootMember(
            string json,
            string memberName)
        {
            int depth = 0;
            for (int index = 0; index < json.Length; index++)
            {
                char value = json[index];
                if (value == '{' || value == '[')
                {
                    depth++;
                    continue;
                }
                if (value == '}' || value == ']')
                {
                    depth--;
                    continue;
                }
                if (value != '"') continue;

                int start = ++index;
                bool escaped = false;
                while (index < json.Length)
                {
                    char current = json[index];
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        break;
                    }
                    index++;
                }
                if (depth != 1 || index >= json.Length) continue;
                int after = index + 1;
                while (after < json.Length &&
                       char.IsWhiteSpace(json[after]))
                {
                    after++;
                }
                if (after >= json.Length || json[after] != ':') continue;
                int length = index - start;
                if (length == memberName.Length &&
                    string.CompareOrdinal(
                        json,
                        start,
                        memberName,
                        0,
                        length) == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
