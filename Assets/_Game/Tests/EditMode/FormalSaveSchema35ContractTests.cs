using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class FormalSaveSchema35ContractTests
    {
        private const string OverloadEffectId =
            "technology.status.overload";

        [Test]
        public void IDEA0027_CurrentSchemaOwnsNonNullResearchEffectState()
        {
            Assert.That(FormalSaveEnvelope.CurrentSchemaVersion,
                Is.EqualTo(36));
            var payload = new FormalThreeDSaveData();
            Assert.That(payload.researchEffectState, Is.Not.Null);
            Assert.That(payload.researchEffectState.nextStableStateOrdinal,
                Is.EqualTo(1L));
            Assert.That(payload.researchEffectState.states, Is.Empty);
            Assert.That(payload.researchEffectState.emitters, Is.Empty);
            Assert.That(payload.researchEffectState.rewardLedger,
                Is.Not.Null);
            Assert.That(payload.researchEffectState.rewardLedger
                .committedRewardKeys, Is.Empty);
            Assert.That(payload.researchEffectState.configurationSignature,
                Is.EqualTo(FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature));
            Assert.That(payload.researchEffectState.revision, Is.Zero);
        }

        [Test]
        public void IDEA0027_StateDtoOwnsStableIdentityAndRuntimeFields()
        {
            Type type = typeof(FormalThreeDResearchEffectStateEntrySaveData);
            foreach (string fieldName in new[]
                     {
                         "stableStateId",
                         "creationOrdinal",
                         "effectId",
                         "targetKind",
                         "targetStableId",
                         "phase",
                         "remainingRuleSeconds",
                         "stacks",
                         "periodAccumulatorSeconds",
                         "currentValue",
                     })
            {
                Assert.That(type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public),
                    Is.Not.Null,
                    fieldName);
            }
        }

        [Test]
        public void IDEA0027_EmitterDtoOwnsSourceTargetAndCooldownIdentity()
        {
            Type type = typeof(FormalThreeDResearchEffectEmitterSaveData);
            foreach (string fieldName in new[]
                     {
                         "stableStateId",
                         "creationOrdinal",
                         "effectId",
                         "sourceTowerStableId",
                         "targetEnemyStableId",
                         "cooldownRemaining",
                     })
            {
                Assert.That(type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public),
                    Is.Not.Null,
                    fieldName);
            }
        }

        [Test]
        public void IDEA0027_OverloadCatalogOwnsPhaseSpecificSaveBounds()
        {
            ResearchStatusDefinition overload = ResearchStatusCatalog.Find(
                ResearchStatusCatalog.TechnologyOverloadId);
            Assert.That(overload, Is.Not.Null);
            Assert.That(overload.MaximumRemainingSeconds(
                ResearchStatusPhase.Active), Is.Zero);
            Assert.That(overload.MaximumRemainingSeconds(
                ResearchStatusPhase.Boosting), Is.EqualTo(5f));
            Assert.That(overload.MaximumRemainingSeconds(
                ResearchStatusPhase.Lockout), Is.EqualTo(3f));
            Assert.That(overload.MaximumRemainingSeconds(
                ResearchStatusPhase.Cooldown), Is.EqualTo(30f));
        }

        [Test]
        public void IDEA0027_SchemaThirtyFourValidatesOldHashBeforeCleanMigration()
        {
            FormalSaveEnvelope historical = LoadMigratedEnvelope();
            historical.saveSchemaVersion = 34;
            historical.formal3D.researchEffectState = null;
            historical.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(
                    historical.formal3D);
            string json = FormalSaveCodec.EncodeEnvelope(historical);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                json);

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(36));
            Assert.That(decoded.Envelope.formal3D.researchEffectState,
                Is.Not.Null);
            Assert.That(decoded.Envelope.formal3D.researchEffectState.states,
                Is.Empty);
            Assert.That(decoded.Envelope.formal3D.researchEffectState.emitters,
                Is.Empty);
            Assert.That(decoded.Envelope.formal3D.researchEffectState
                .rewardLedger.committedRewardKeys, Is.Empty);
            Assert.That(decoded.Envelope.formal3D.researchEffectState
                    .configurationSignature,
                Is.EqualTo(FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature));
            Assert.That(decoded.Envelope.formal3D.researchEffectState.revision,
                Is.Zero);
        }

        [Test]
        public void IDEA0027_SchemaThirtyFourRejectsBadOldHashBeforeMigration()
        {
            FormalSaveEnvelope historical = LoadMigratedEnvelope();
            historical.saveSchemaVersion = 34;
            historical.formal3D.researchEffectState = null;
            historical.payloadHashSha256 = new string('0', 64);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(historical));

            Assert.That(decoded.Success, Is.False);
            Assert.That(decoded.Error,
                Is.EqualTo(FormalSaveDecodeError.MalformedJson));
        }

        [Test]
        public void IDEA0027_SchemaThirtyOneMigratesThroughThirtyFive()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                ReadFixture("schema-31-formal-3d.json"));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(36));
            Assert.That(decoded.Envelope.formal3D.progression, Is.Not.Null);
            Assert.That(decoded.Envelope.formal3D.civilizationExpansion,
                Is.Not.Null);
            Assert.That(decoded.Envelope.formal3D.researchEffectState,
                Is.Not.Null);
            Assert.That(decoded.Envelope.formal3D.researchEffectState.states,
                Is.Empty);
        }

        [Test]
        public void IDEA0027_CurrentStateAndRewardLedgerRoundTrip()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            EnsureResearchCompleted(
                envelope,
                "core.research.energy-weapons");
            envelope.formal3D.researchEffectState = StateWithOneOverload();
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(envelope));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalThreeDResearchEffectStateSaveData restored =
                decoded.Envelope.formal3D.researchEffectState;
            Assert.That(restored.configurationSignature,
                Is.EqualTo(FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature));
            Assert.That(restored.revision, Is.EqualTo(1UL));
            Assert.That(restored.nextStableStateOrdinal, Is.EqualTo(8L));
            Assert.That(restored.states, Has.Length.EqualTo(1));
            Assert.That(restored.states[0].stableStateId,
                Is.EqualTo("research.state.000007"));
            Assert.That(restored.states[0].creationOrdinal, Is.EqualTo(7L));
            Assert.That(restored.states[0].effectId,
                Is.EqualTo(OverloadEffectId));
            Assert.That(restored.states[0].targetKind,
                Is.EqualTo(FormalResearchEffectTargetKind.Tower));
            Assert.That(restored.states[0].targetStableId,
                Is.EqualTo("building.instance.000003"));
            Assert.That(restored.states[0].phase,
                Is.EqualTo(FormalResearchEffectStatePhase.Boosting));
            Assert.That(restored.states[0].remainingRuleSeconds,
                Is.EqualTo(4.25f));
            Assert.That(restored.states[0].stacks, Is.EqualTo(1));
            Assert.That(restored.states[0].periodAccumulatorSeconds,
                Is.EqualTo(0f));
            Assert.That(restored.states[0].currentValue, Is.EqualTo(0f));
            Assert.That(restored.rewardLedger.committedRewardKeys,
                Is.EqualTo(new[]
                {
                    ResearchStatusCatalog.GeneSplicingRewardKey,
                }));
        }

        [Test]
        public void IDEA0027_EmitterRoundTripsWithoutCollapsingSharedTarget()
        {
            FormalSaveEnvelope envelope = EmitterEnvelope();
            string enemyId = envelope.formal3D.defenseCampaign.enemyStates[0]
                .stableEnemyId;
            envelope.formal3D.researchEffectState.emitters = new[]
            {
                Emitter("research.state.000002", 2L,
                    ResearchStatusCatalog.SwordIntentId,
                    "building.instance.000001", enemyId, .25f),
                Emitter("research.state.000003", 3L,
                    ResearchStatusCatalog.SwordIntentId,
                    "building.instance.000002", enemyId, .75f),
            };
            envelope.formal3D.researchEffectState.nextStableStateOrdinal = 4L;
            Rehash(envelope);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                FormalSaveCodec.EncodeEnvelope(envelope));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.formal3D.researchEffectState.emitters,
                Has.Length.EqualTo(2));
            Assert.That(decoded.Envelope.formal3D.researchEffectState.emitters
                [0].cooldownRemaining, Is.EqualTo(.25f));
            Assert.That(decoded.Envelope.formal3D.researchEffectState.emitters
                [1].sourceTowerStableId,
                Is.EqualTo("building.instance.000002"));
        }

        [Test]
        public void IDEA0027_EmitterValidatorRejectsBadGates()
        {
            FormalSaveEnvelope unknown = EmitterEnvelope();
            unknown.formal3D.researchEffectState.emitters[0].effectId =
                "technology.status.unknown-emitter";
            Rehash(unknown);
            AssertInvalid(unknown, FormalSaveValidationError.InvalidStableId,
                "formal3D.researchEffectState.emitters[0].effectId");

            FormalSaveEnvelope wrongTower = EmitterEnvelope();
            wrongTower.formal3D.buildings.instances[0].definitionId =
                WasteCity.Building.BuildingCatalog.SporeTower.Id.Value;
            Rehash(wrongTower);
            AssertInvalid(wrongTower,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.emitters[0]." +
                "sourceTowerStableId");

            FormalSaveEnvelope missingEnemy = EmitterEnvelope();
            missingEnemy.formal3D.researchEffectState.emitters[0]
                .targetEnemyStableId = "campaign.enemy.missing.000001";
            Rehash(missingEnemy);
            AssertInvalid(missingEnemy,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.emitters[0]." +
                "targetEnemyStableId");

            FormalSaveEnvelope badClock = EmitterEnvelope();
            badClock.formal3D.researchEffectState.emitters[0]
                .cooldownRemaining = 0f;
            Rehash(badClock);
            Assert.That(FormalSaveValidator.ValidateEnvelope(badClock).IsValid,
                Is.False);

            FormalSaveEnvelope duplicate = EmitterEnvelope();
            FormalThreeDResearchEffectEmitterSaveData copy = Emitter(
                "research.state.000002", 2L,
                duplicate.formal3D.researchEffectState.emitters[0].effectId,
                duplicate.formal3D.researchEffectState.emitters[0]
                    .sourceTowerStableId,
                duplicate.formal3D.researchEffectState.emitters[0]
                    .targetEnemyStableId,
                .5f);
            duplicate.formal3D.researchEffectState.emitters = new[]
            {
                duplicate.formal3D.researchEffectState.emitters[0],
                copy,
            };
            duplicate.formal3D.researchEffectState.nextStableStateOrdinal = 3L;
            Rehash(duplicate);
            AssertInvalid(duplicate,
                FormalSaveValidationError.DuplicateStableId,
                "formal3D.researchEffectState.emitters[1].effectId");

            FormalSaveEnvelope unresearched = EmitterEnvelope();
            unresearched.formal3D.research.completedResearchIds =
                Array.FindAll(
                    unresearched.formal3D.research.completedResearchIds,
                    value => !string.Equals(
                        value,
                        "core.research.sword-array",
                        StringComparison.Ordinal));
            Rehash(unresearched);
            AssertInvalid(unresearched,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.emitters[0].effectId");

            FormalSaveEnvelope highWater = EmitterEnvelope();
            highWater.formal3D.researchEffectState
                .nextStableStateOrdinal = 1L;
            Rehash(highWater);
            AssertInvalid(highWater,
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.researchEffectState.nextStableStateOrdinal");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsDuplicateStateIdentity()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            FormalThreeDResearchEffectStateEntrySaveData state =
                envelope.formal3D.researchEffectState.states[0];
            envelope.formal3D.researchEffectState.states = new[]
            {
                state,
                state,
            };
            Rehash(envelope);

            AssertInvalid(envelope, FormalSaveValidationError.DuplicateStableId,
                "formal3D.researchEffectState.states[1].stableStateId");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsUnknownEffectAndDanglingTarget()
        {
            FormalSaveEnvelope unknown = EnvelopeWithOneOverload();
            unknown.formal3D.researchEffectState.states[0].effectId =
                "core.effect.research.unknown.rule.unknown";
            Rehash(unknown);
            AssertInvalid(unknown,
                FormalSaveValidationError.InvalidStableId,
                "formal3D.researchEffectState.states[0].effectId");

            FormalSaveEnvelope dangling = EnvelopeWithOneOverload();
            dangling.formal3D.researchEffectState.states[0].targetStableId =
                "building.instance.999999";
            Rehash(dangling);
            AssertInvalid(dangling,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.states[0].targetStableId");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsStatusBeforeSourceResearch()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            envelope.formal3D.research.completedResearchIds = Array.FindAll(
                envelope.formal3D.research.completedResearchIds,
                value => !string.Equals(
                    value,
                    "core.research.energy-weapons",
                    StringComparison.Ordinal));
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.states[0].effectId");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsInvalidRuntimeNumbersAndLedger()
        {
            FormalSaveEnvelope phase = EnvelopeWithOneOverload();
            phase.formal3D.researchEffectState.states[0].phase =
                (FormalResearchEffectStatePhase)99;
            Rehash(phase);
            AssertInvalid(phase,
                FormalSaveValidationError.InvalidEnumValue,
                "formal3D.researchEffectState.states[0].phase");

            FormalSaveEnvelope time = EnvelopeWithOneOverload();
            time.formal3D.researchEffectState.states[0]
                .remainingRuleSeconds = float.NaN;
            Rehash(time);
            AssertInvalid(time,
                FormalSaveValidationError.NonFiniteNumber,
                "formal3D.researchEffectState.states[0].remainingRuleSeconds");

            FormalSaveEnvelope stack = EnvelopeWithOneOverload();
            stack.formal3D.researchEffectState.states[0].stacks = -1;
            Rehash(stack);
            AssertInvalid(stack,
                FormalSaveValidationError.NegativeValue,
                "formal3D.researchEffectState.states[0].stacks");

            FormalSaveEnvelope ledger = EnvelopeWithOneOverload();
            ledger.formal3D.researchEffectState.rewardLedger
                .committedRewardKeys = new[]
                {
                    ResearchStatusCatalog.GeneSplicingRewardKey,
                    ResearchStatusCatalog.GeneSplicingRewardKey,
                };
            Rehash(ledger);
            AssertInvalid(ledger,
                FormalSaveValidationError.DuplicateStableId,
                "formal3D.researchEffectState.rewardLedger." +
                "committedRewardKeys[1]");
        }

        [Test]
        public void IDEA0027_RewardLedgerRejectsUnknownAndUnresearchedKeys()
        {
            FormalSaveEnvelope unknown = EnvelopeWithOneOverload();
            unknown.formal3D.researchEffectState.rewardLedger
                .committedRewardKeys = new[]
                {
                    "research.reward.unknown.first-completion",
                };
            Rehash(unknown);
            AssertInvalid(unknown,
                FormalSaveValidationError.InvalidStableId,
                "formal3D.researchEffectState.rewardLedger." +
                "committedRewardKeys[0]");

            FormalSaveEnvelope unresearched = LoadMigratedEnvelope();
            unresearched.formal3D.research.completedResearchIds =
                Array.FindAll(
                    unresearched.formal3D.research.completedResearchIds,
                    value => !string.Equals(
                        value,
                        "core.research.gene-splicing",
                        StringComparison.Ordinal));
            unresearched.formal3D.researchEffectState.revision = 1UL;
            unresearched.formal3D.researchEffectState.rewardLedger
                .committedRewardKeys = new[]
                {
                    ResearchStatusCatalog.GeneSplicingRewardKey,
                };
            Rehash(unresearched);
            AssertInvalid(unresearched,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.rewardLedger." +
                "committedRewardKeys[0]");

            EnsureResearchCompleted(
                unresearched,
                "core.research.gene-splicing");
            Rehash(unresearched);
            Assert.That(FormalSaveValidator.ValidateEnvelope(unresearched)
                .IsValid, Is.True);
        }

        [Test]
        public void IDEA0027_ValidatorRejectsInvalidHighWaterMark()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            envelope.formal3D.researchEffectState.nextStableStateOrdinal = 7L;
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.researchEffectState.nextStableStateOrdinal");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsInvalidStateAggregateIdentity()
        {
            FormalSaveEnvelope signature = EnvelopeWithOneOverload();
            signature.formal3D.researchEffectState.configurationSignature =
                "builtin:research-effect-state@999";
            Rehash(signature);
            AssertInvalid(signature,
                FormalSaveValidationError.InvalidStableId,
                "formal3D.researchEffectState.configurationSignature");

            FormalSaveEnvelope revision = EnvelopeWithOneOverload();
            revision.formal3D.researchEffectState.revision = 0;
            Rehash(revision);
            AssertInvalid(revision,
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.researchEffectState.revision");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsDuplicateStatusTargetAggregate()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            FormalThreeDResearchEffectStateEntrySaveData first =
                envelope.formal3D.researchEffectState.states[0];
            envelope.formal3D.researchEffectState.states = new[]
            {
                first,
                CopyState(first, "research.state.000008", 8L),
            };
            envelope.formal3D.researchEffectState.nextStableStateOrdinal = 9L;
            envelope.formal3D.researchEffectState.revision = 2UL;
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.DuplicateStableId,
                "formal3D.researchEffectState.states[1].effectId");
        }

        [TestCase(FormalResearchEffectStatePhase.Boosting, 5.01f)]
        [TestCase(FormalResearchEffectStatePhase.Lockout, 3.01f)]
        [TestCase(FormalResearchEffectStatePhase.Cooldown, 30.01f)]
        public void IDEA0027_ValidatorRejectsOverloadPhaseSpecificTimeOverflow(
            FormalResearchEffectStatePhase phase,
            float seconds)
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            envelope.formal3D.researchEffectState.states[0].phase = phase;
            envelope.formal3D.researchEffectState.states[0]
                .remainingRuleSeconds = seconds;
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.InvalidHighWaterMark,
                "formal3D.researchEffectState.states[0].remainingRuleSeconds");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsReadyOverloadAsActiveState()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            envelope.formal3D.researchEffectState.states[0].phase =
                FormalResearchEffectStatePhase.Active;
            envelope.formal3D.researchEffectState.states[0]
                .remainingRuleSeconds = 0f;
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.InvalidEnumValue,
                "formal3D.researchEffectState.states[0].phase");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsStateIdOrdinalMismatch()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            envelope.formal3D.researchEffectState.states[0].stableStateId =
                "research.state.000008";
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.InvalidStableId,
                "formal3D.researchEffectState.states[0].stableStateId");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsNonCanonicalStateOrder()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            FormalThreeDResearchEffectStateEntrySaveData first =
                envelope.formal3D.researchEffectState.states[0];
            envelope.formal3D.researchEffectState.states = new[]
            {
                CopyState(first, "research.state.000008", 8L),
                first,
            };
            envelope.formal3D.researchEffectState.nextStableStateOrdinal = 9L;
            envelope.formal3D.researchEffectState.revision = 2UL;
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.InvalidArray,
                "formal3D.researchEffectState.states[1]");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsZeroStackActivityState()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            envelope.formal3D.researchEffectState.states[0].stacks = 0;
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.NegativeValue,
                "formal3D.researchEffectState.states[0].stacks");
        }

        [Test]
        public void IDEA0027_ValidatorRejectsStateWithoutCompletedSourceResearch()
        {
            FormalSaveEnvelope envelope = EnvelopeWithOneOverload();
            envelope.formal3D.research.completedResearchIds = Array.FindAll(
                envelope.formal3D.research.completedResearchIds,
                value => !string.Equals(
                    value,
                    "core.research.energy-weapons",
                    StringComparison.Ordinal));
            Rehash(envelope);

            AssertInvalid(envelope,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.states[0].effectId");
        }

        [Test]
        public void IDEA0027_MindControlStateAndCampaignFactionMustMatch()
        {
            FormalSaveEnvelope valid = LoadMigratedEnvelope();
            EnsureResearchCompleted(valid, "core.research.mind-control");
            FormalThreeDDefenseCampaignEnemyStateSaveData enemy =
                valid.formal3D.defenseCampaign.enemyStates[0];
            enemy.isControlled = true;
            valid.formal3D.researchEffectState =
                StateForTarget(
                    ResearchStatusCatalog.MindControlId,
                    FormalResearchEffectTargetKind.Enemy,
                    enemy.stableEnemyId,
                    remainingSeconds: 0f,
                    currentValue: 0f);
            Rehash(valid);
            Assert.That(FormalSaveValidator.ValidateEnvelope(valid).IsValid,
                Is.True);

            FormalSaveEnvelope missingState = LoadMigratedEnvelope();
            missingState.formal3D.defenseCampaign.enemyStates[0]
                .isControlled = true;
            Rehash(missingState);
            AssertInvalid(
                missingState,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.defenseCampaign.enemyStates");

            FormalSaveEnvelope hostileTarget = LoadMigratedEnvelope();
            EnsureResearchCompleted(hostileTarget, "core.research.mind-control");
            hostileTarget.formal3D.researchEffectState = StateForTarget(
                ResearchStatusCatalog.MindControlId,
                FormalResearchEffectTargetKind.Enemy,
                hostileTarget.formal3D.defenseCampaign.enemyStates[0]
                    .stableEnemyId,
                remainingSeconds: 0f,
                currentValue: 0f);
            Rehash(hostileTarget);
            AssertInvalid(
                hostileTarget,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.states[0].targetStableId");
        }

        [Test]
        public void IDEA0027_PriorWaveControlledDoesNotPolluteEnemyCounts()
        {
            MethodInfo method = typeof(FormalSaveValidator).GetMethod(
                "ValidateCampaignCountRelationships",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var campaign = new FormalThreeDDefenseCampaignSaveData
            {
                phase = (int)SingleCityDefenseCampaignPhase.Warning,
                currentWaveNumber = 2,
                result = 0,
                nextEnemyOrdinal = 0,
                spawnedEnemyCountsByEnemyId = Array.Empty<
                    FormalThreeDDefenseCampaignEnemyCountSaveData>(),
                defeatedEnemyCountsByEnemyId = Array.Empty<
                    FormalThreeDDefenseCampaignEnemyCountSaveData>(),
                enemyStates = new[]
                {
                    new FormalThreeDDefenseCampaignEnemyStateSaveData
                    {
                        stableEnemyId = "campaign.enemy.wave-01.0000",
                        archetypeId = EnemyCatalog.Gnawer.Id.Value,
                        spawnOrder = 0,
                        currentHealth = 10,
                        isControlled = true,
                    },
                },
            };

            Assert.That(method.Invoke(null, new object[]
                { campaign, "formal3D.defenseCampaign" }), Is.Null);

            campaign.enemyStates[0].isControlled = false;
            Assert.That(method.Invoke(null, new object[]
                { campaign, "formal3D.defenseCampaign" }), Is.Not.Null);
        }

        [Test]
        public void IDEA0027_ActiveGeneTraitRequiresCommittedRewardKey()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            EnsureResearchCompleted(envelope, "core.research.gene-splicing");
            string leaderId = envelope.formal3D.civilizationExpansion
                .charactersPolitics.currentLeaderId;
            envelope.formal3D.researchEffectState = StateForTarget(
                ResearchStatusCatalog.GeneSplicingTraitId,
                FormalResearchEffectTargetKind.Character,
                leaderId,
                remainingSeconds: 100f,
                currentValue: 1.2f);
            Rehash(envelope);

            AssertInvalid(
                envelope,
                FormalSaveValidationError.MissingStableReference,
                "formal3D.researchEffectState.rewardLedger." +
                "committedRewardKeys");
        }

        [Test]
        public void IDEA0027_GeneSplicingStateRaisesOnlyItsCharacterSaveLimit()
        {
            MethodInfo method = typeof(FormalSaveValidator).GetMethod(
                "ResolveCharacterMaximumHealth",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var active = new FormalThreeDResearchEffectStateSaveData
            {
                states = new[]
                {
                    new FormalThreeDResearchEffectStateEntrySaveData
                    {
                        effectId = "biological.trait.gene-splicing",
                        targetKind =
                            FormalResearchEffectTargetKind.Character,
                        targetStableId = CharacterCatalog.CenJinId,
                        phase = FormalResearchEffectStatePhase.Active,
                        remainingRuleSeconds = 100f,
                        stacks = 1,
                    },
                },
            };

            Assert.That(method.Invoke(null, new object[]
                { CharacterCatalog.CenJin, active }), Is.EqualTo(120));
            Assert.That(method.Invoke(null, new object[]
                { CharacterCatalog.LinXi, active }),
                Is.EqualTo(CharacterCatalog.LinXi.MaximumHealth));
            active.states[0].remainingRuleSeconds = 0f;
            Assert.That(method.Invoke(null, new object[]
                { CharacterCatalog.CenJin, active }),
                Is.EqualTo(CharacterCatalog.CenJin.MaximumHealth));
        }

        [Test]
        public void IDEA0027_ValidatorRejectsActiveGeneTraitOnDeadCharacter()
        {
            MethodInfo method = typeof(FormalSaveValidator).GetMethod(
                "IsCharacterResearchEffectStateCompatible",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var character = new FormalThreeDCharacterSaveData
            {
                characterId = CharacterCatalog.CenJinId,
                state = (int)CharacterLifeState.Dead,
            };
            FormalThreeDResearchEffectStateSaveData active = StateForTarget(
                ResearchStatusCatalog.GeneSplicingTraitId,
                FormalResearchEffectTargetKind.Character,
                CharacterCatalog.CenJinId,
                remainingSeconds: 100f,
                currentValue: 1.2f);

            Assert.That(method.Invoke(null, new object[]
                { character, active }), Is.False);

            character.state = (int)CharacterLifeState.Active;
            Assert.That(method.Invoke(null, new object[]
                { character, active }), Is.True);
        }

        [Test]
        public void IDEA0027_BehemothBreedingCompletionRaisesArmySaveLimit()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            FormalThreeDArmyLeaderSaveData army =
                envelope.formal3D.civilizationExpansion.armyLeader;
            army.nextUnitOrdinal = 2;
            army.units = new[]
            {
                new FormalThreeDArmyUnitSaveData
                {
                    stableUnitId = "core.army-unit.000001",
                    definitionId = ArmyUnitCatalog.BredBehemothId,
                    squadId = SingleCityArmyModel.DefaultSquadId,
                    currentHealth = 352,
                    maintenanceElapsedSeconds = 0f,
                    maintenanceRemainingSeconds = 0f,
                },
            };
            army.squads = new[]
            {
                new FormalThreeDArmySquadSaveData
                {
                    stableSquadId = SingleCityArmyModel.DefaultSquadId,
                    command = (int)FriendlySquadCommandType.Rally,
                    unitIds = new[] { "core.army-unit.000001" },
                    path = Array.Empty<FormalThreeDGridPointSaveData>(),
                },
            };
            string[] before =
                envelope.formal3D.research.completedResearchIds;
            var completed = new string[before.Length + 1];
            Array.Copy(before, completed, before.Length);
            completed[before.Length] =
                "core.research.behemoth-breeding";
            envelope.formal3D.research.completedResearchIds = completed;
            Rehash(envelope);

            Assert.That(FormalSaveValidator.ValidateEnvelope(envelope).IsValid,
                Is.True);

            envelope.formal3D.research.completedResearchIds = before;
            Rehash(envelope);
            Assert.That(FormalSaveValidator.ValidateEnvelope(envelope).IsValid,
                Is.False);
        }

        [Test]
        public void IDEA0027_FutureSchemaThirtySevenIsRejected()
        {
            string json = ReadFixture("schema-32-future.json").Replace(
                "\"saveSchemaVersion\": 32",
                "\"saveSchemaVersion\": 37");

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(json);

            Assert.That(decoded.Success, Is.False);
            Assert.That(decoded.Error,
                Is.EqualTo(FormalSaveDecodeError.UnsupportedFutureSchema));
        }

        private static FormalSaveEnvelope EnvelopeWithOneOverload()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            EnsureResearchCompleted(
                envelope,
                "core.research.energy-weapons");
            EnsureResearchCompleted(
                envelope,
                "core.research.gene-splicing");
            envelope.formal3D.researchEffectState = StateWithOneOverload();
            Rehash(envelope);
            return envelope;
        }

        private static FormalSaveEnvelope EmitterEnvelope()
        {
            FormalSaveEnvelope envelope = LoadMigratedEnvelope();
            EnsureResearchCompleted(envelope, "core.research.sword-array");
            for (var index = 0; index < 2; index++)
            {
                envelope.formal3D.buildings.instances[index].definitionId =
                    WasteCity.Building.BuildingCatalog.SwordArrayTower.Id.Value;
                envelope.formal3D.buildings.instances[index].state = 1;
                envelope.formal3D.buildings.instances[index].isPlayerOwned =
                    true;
            }
            string enemyId = envelope.formal3D.defenseCampaign.enemyStates[0]
                .stableEnemyId;
            envelope.formal3D.researchEffectState =
                new FormalThreeDResearchEffectStateSaveData
                {
                    revision = 1UL,
                    nextStableStateOrdinal = 2L,
                    emitters = new[]
                    {
                        Emitter(
                            "research.state.000001",
                            1L,
                            ResearchStatusCatalog.SwordIntentId,
                            "building.instance.000001",
                            enemyId,
                            .5f),
                    },
                };
            Rehash(envelope);
            return envelope;
        }

        private static FormalThreeDResearchEffectEmitterSaveData Emitter(
            string stableStateId,
            long creationOrdinal,
            string effectId,
            string sourceTowerStableId,
            string targetEnemyStableId,
            float cooldownRemaining)
        {
            return new FormalThreeDResearchEffectEmitterSaveData
            {
                stableStateId = stableStateId,
                creationOrdinal = creationOrdinal,
                effectId = effectId,
                sourceTowerStableId = sourceTowerStableId,
                targetEnemyStableId = targetEnemyStableId,
                cooldownRemaining = cooldownRemaining,
            };
        }

        private static FormalThreeDResearchEffectStateSaveData
            StateWithOneOverload()
        {
            return new FormalThreeDResearchEffectStateSaveData
            {
                configurationSignature =
                    FormalThreeDResearchEffectStateSaveData
                        .ConfigurationSignature,
                revision = 1UL,
                nextStableStateOrdinal = 8L,
                states = new[]
                {
                    new FormalThreeDResearchEffectStateEntrySaveData
                    {
                        stableStateId = "research.state.000007",
                        creationOrdinal = 7L,
                        effectId = OverloadEffectId,
                        targetKind =
                            FormalResearchEffectTargetKind.Tower,
                        targetStableId = "building.instance.000003",
                        phase =
                            FormalResearchEffectStatePhase.Boosting,
                        remainingRuleSeconds = 4.25f,
                        stacks = 1,
                        periodAccumulatorSeconds = 0f,
                        currentValue = 0f,
                    },
                },
                rewardLedger = new FormalThreeDResearchRewardLedgerSaveData
                {
                    committedRewardKeys = new[]
                    {
                        ResearchStatusCatalog.GeneSplicingRewardKey,
                    },
                },
            };
        }

        private static FormalThreeDResearchEffectStateSaveData StateForTarget(
            string effectId,
            FormalResearchEffectTargetKind targetKind,
            string targetStableId,
            float remainingSeconds,
            float currentValue)
        {
            return new FormalThreeDResearchEffectStateSaveData
            {
                configurationSignature =
                    FormalThreeDResearchEffectStateSaveData
                        .ConfigurationSignature,
                revision = 1UL,
                nextStableStateOrdinal = 2L,
                states = new[]
                {
                    new FormalThreeDResearchEffectStateEntrySaveData
                    {
                        stableStateId = "research.state.000001",
                        creationOrdinal = 1L,
                        effectId = effectId,
                        targetKind = targetKind,
                        targetStableId = targetStableId,
                        phase = FormalResearchEffectStatePhase.Active,
                        remainingRuleSeconds = remainingSeconds,
                        stacks = 1,
                        currentValue = currentValue,
                    },
                },
                rewardLedger = new FormalThreeDResearchRewardLedgerSaveData(),
            };
        }

        private static FormalThreeDResearchEffectStateEntrySaveData CopyState(
            FormalThreeDResearchEffectStateEntrySaveData source,
            string stableStateId,
            long creationOrdinal)
        {
            return new FormalThreeDResearchEffectStateEntrySaveData
            {
                stableStateId = stableStateId,
                creationOrdinal = creationOrdinal,
                effectId = source.effectId,
                targetKind = source.targetKind,
                targetStableId = source.targetStableId,
                phase = source.phase,
                remainingRuleSeconds = source.remainingRuleSeconds,
                stacks = source.stacks,
                periodAccumulatorSeconds =
                    source.periodAccumulatorSeconds,
                currentValue = source.currentValue,
            };
        }

        private static FormalSaveEnvelope LoadMigratedEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            return decoded.Envelope;
        }

        private static void Rehash(FormalSaveEnvelope envelope)
        {
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
        }

        private static void EnsureResearchCompleted(
            FormalSaveEnvelope envelope,
            string researchId)
        {
            string[] source =
                envelope.formal3D.research.completedResearchIds ??
                Array.Empty<string>();
            if (Array.IndexOf(source, researchId) >= 0) return;
            var result = new string[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[source.Length] = researchId;
            envelope.formal3D.research.completedResearchIds = result;
        }

        private static void AssertInvalid(
            FormalSaveEnvelope envelope,
            FormalSaveValidationError expectedError,
            string expectedPath)
        {
            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);
            Assert.That(result.IsValid, Is.False, result.Message);
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.FieldPath, Is.EqualTo(expectedPath));
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
