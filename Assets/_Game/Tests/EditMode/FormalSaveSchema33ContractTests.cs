using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class FormalSaveSchema33ContractTests
    {
        private const BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        private static readonly string[] FateIds =
        {
            "core.legacy.pocket-universe",
            "core.legacy.void-debt",
            "core.legacy.rewind-anchor",
        };

        [Test]
        public void IDEA0020_CurrentSchemaIsThirtyThreeAndOwnsProgressionDto()
        {
            Assert.That(FormalSaveEnvelope.CurrentSchemaVersion, Is.EqualTo(33));

            FieldInfo progression = RequireField(
                typeof(FormalThreeDSaveData),
                "progression");
            Type root = progression.FieldType;
            Type attention = RequireField(root, "attention").FieldType;
            Type fate = RequireField(root, "fate").FieldType;
            Type effects = RequireField(root, "fateEffects").FieldType;
            Type pressure = RequireField(root, "pressure").FieldType;
            Type civilization = RequireField(root, "civilization").FieldType;

            RequireField(attention, "value", typeof(int));
            RequireField(attention, "revision", typeof(ulong));
            RequireArray(attention, "history");
            RequireArray(attention, "reachedThresholds", typeof(int));
            RequireArray(attention, "committedStableEventKeys", typeof(string));
            RequireArray(attention, "completedOneShotReasonIds", typeof(string));

            RequireArray(fate, "offeredIds", typeof(string));
            RequireField(fate, "selectedId", typeof(string));
            RequireField(fate, "level", typeof(int));
            RequireField(fate, "revision", typeof(ulong));

            Type pocket = RequireField(effects, "pocketUniverse").FieldType;
            Type debt = RequireField(effects, "voidDebt").FieldType;
            Type rewind = RequireField(effects, "rewindAnchors").FieldType;
            RequireArray(pocket, "flagships");
            RequireArray(pocket, "collapsedFlagshipIds", typeof(string));
            RequireField(pocket, "firstProductionFlagshipId", typeof(string));
            RequireArray(debt, "debts");
            RequireField(debt, "settlementRemainingSeconds", typeof(double));
            RequireField(debt, "nextSettlementOrdinal", typeof(ulong));
            RequireArray(rewind, "anchors");
            RequireField(rewind, "nextCreationOrdinal", typeof(long));

            RequireField(pressure, "revision", typeof(ulong));
            Type pressureEntry = RequireArray(pressure, "entries")
                .FieldType.GetElementType();
            RequireField(pressureEntry, "threshold", typeof(int));
            RequireField(pressureEntry, "state", typeof(int));
            RequireField(pressureEntry, "warningRemainingSeconds",
                typeof(float));
            RequireField(pressure, "activeEncounterId", typeof(string));
            RequireField(pressure, "activeCampaign");

            RequireField(civilization, "level", typeof(int));
            RequireField(civilization, "revision", typeof(ulong));
            RequireField(civilization, "ascensionId", typeof(string));
            RequireField(civilization, "ascensionCompleted", typeof(bool));
            RequireField(civilization, "sequenceStage", typeof(int));
            RequireField(civilization, "remainingRuleSeconds", typeof(float));
            RequireArray(civilization, "committedAscensionIds", typeof(string));
        }

        [Test]
        public void IDEA0020_LevelTwoCivilizationAndVoidEffectRoundTrip()
        {
            FormalSaveEnvelope envelope = MigratedFixture();
            object progression = ReadField(envelope.formal3D, "progression");
            object fate = ReadField(progression, "fate");
            object effects = ReadField(progression, "fateEffects");
            object debt = ReadField(effects, "voidDebt");
            object civilization = ReadField(progression, "civilization");
            Select(fate, FormalFateCatalog.VoidDebtId, 2);
            WriteField(fate, "revision", 2ul);
            WriteField(debt, "level", 2);
            WriteField(civilization, "level", 2);
            WriteField(civilization, "revision", 1ul);
            WriteField(civilization, "ascensionId",
                "first-civilization-ascension");
            WriteField(civilization, "ascensionCompleted", true);
            WriteField(civilization, "sequenceStage",
                (int)AdvancementSequenceStage.Scanning);
            WriteField(civilization, "remainingRuleSeconds", 1.5f);
            WriteField(civilization, "committedAscensionIds", new[]
            {
                "first-civilization-ascension",
            });
            Rehash(envelope);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(envelope));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            Assert.That(validation.IsValid, Is.True, validation.Message);
            FormalThreeDCivilizationSaveData actual =
                decoded.Envelope.formal3D.progression.civilization;
            Assert.That(actual.level, Is.EqualTo(2));
            Assert.That(actual.revision, Is.EqualTo(1ul));
            Assert.That(actual.ascensionCompleted, Is.True);
            Assert.That(actual.sequenceStage,
                Is.EqualTo((int)AdvancementSequenceStage.Scanning));
            Assert.That(actual.remainingRuleSeconds, Is.EqualTo(1.5f));
            Assert.That(actual.committedAscensionIds,
                Is.EqualTo(new[] { "first-civilization-ascension" }));
        }

        [Test]
        public void IDEA0020_SchemaThirtyThreeProgressionRoundTripsThroughRealCodec()
        {
            FormalSaveEnvelope envelope = MigratedFixture();
            object progression = ReadField(envelope.formal3D, "progression");
            object attention = ReadField(progression, "attention");
            object fate = ReadField(progression, "fate");
            object effects = ReadField(progression, "fateEffects");
            object civilization = ReadField(progression, "civilization");

            WriteField(attention, "value", 42);
            WriteField(attention, "revision", 7ul);
            WriteField(attention, "reachedThresholds", new[] { 30 });
            WriteField(attention, "committedStableEventKeys",
                new[] { "fixture.event.000001" });
            WriteField(attention, "completedOneShotReasonIds",
                new[] { "core.attention.fate.first-activation" });
            WriteField(attention, "history", new[]
            {
                new FormalThreeDAttentionHistorySaveData
                {
                    reasonId = "core.attention.fate.first-activation",
                    stableEventKey = "fixture.event.000001",
                    requestedDelta = 5,
                    appliedDelta = 5,
                    valueAfter = 42,
                    revision = 7ul,
                    ruleTimeSeconds = 7f,
                },
            });
            WriteField(fate, "offeredIds", FateIds.ToArray());
            WriteField(fate, "selectedId", FateIds[1]);
            WriteField(fate, "level", 1);
            WriteField(fate, "revision", 1ul);
            object voidDebt = ReadField(effects, "voidDebt");
            WriteField(voidDebt, "settlementRemainingSeconds", 23d);
            WriteField(voidDebt, "debts", new[]
            {
                new FormalThreeDVoidDebtEntrySaveData
                {
                    resourceId = "core.resource.stone",
                    amount = 7,
                },
            });
            WriteField(civilization, "level", 1);
            WriteField(civilization, "committedAscensionIds",
                Array.Empty<string>());
            Rehash(envelope);

            string json = FormalSaveCodec.EncodeEnvelope(envelope);
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(json);
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(33));
            Assert.That(
                FormalSaveValidator.ValidateDecoded(decoded).IsValid,
                Is.True,
                FormalSaveValidator.ValidateDecoded(decoded).Message);

            object actual = ReadField(decoded.Envelope.formal3D, "progression");
            Assert.That(Read<int>(ReadField(actual, "attention"), "value"),
                Is.EqualTo(42));
            object actualFate = ReadField(actual, "fate");
            Assert.That(Read<string>(actualFate, "selectedId"),
                Is.EqualTo(FateIds[1]));
            Assert.That(Read<int>(actualFate, "level"), Is.EqualTo(1));
            object actualEffects = ReadField(actual, "fateEffects");
            object actualDebt = ReadField(actualEffects, "voidDebt");
            Assert.That(Read<double>(actualDebt,
                "settlementRemainingSeconds"), Is.EqualTo(23d));
            Assert.That(((Array)ReadField(actualDebt, "debts")).Length,
                Is.EqualTo(1));
            Assert.That(Read<int>(ReadField(actual, "civilization"), "level"),
                Is.EqualTo(1));
        }

        [TestCase("attention-out-of-range")]
        [TestCase("duplicate-threshold")]
        [TestCase("duplicate-offer")]
        [TestCase("unknown-fate")]
        [TestCase("unselected-with-level")]
        [TestCase("selected-with-zero-level")]
        [TestCase("void-debt-while-pocket")]
        [TestCase("negative-void-debt")]
        [TestCase("pressure-active-without-campaign")]
        [TestCase("pressure-active-id-mismatch")]
        [TestCase("fate-two-civilization-one")]
        [TestCase("civilization-two-fate-one")]
        [TestCase("civilization-two-missing-lock")]
        [TestCase("civilization-two-wrong-id")]
        [TestCase("civilization-two-no-sequence")]
        [TestCase("civilization-two-invalid-time")]
        public void IDEA0020_ValidatorRejectsInvalidProgressionTruth(string fault)
        {
            FormalSaveEnvelope envelope = MigratedFixture();
            object progression = ReadField(envelope.formal3D, "progression");
            object attention = ReadField(progression, "attention");
            object fate = ReadField(progression, "fate");
            object effects = ReadField(progression, "fateEffects");
            object pressure = ReadField(progression, "pressure");
            object civilization = ReadField(progression, "civilization");

            switch (fault)
            {
                case "attention-out-of-range":
                    WriteField(attention, "value", 101);
                    break;
                case "duplicate-threshold":
                    WriteField(attention, "reachedThresholds", new[] { 30, 30 });
                    break;
                case "duplicate-offer":
                    WriteField(fate, "offeredIds", new[]
                    {
                        FateIds[0], FateIds[0], FateIds[2],
                    });
                    break;
                case "unknown-fate":
                    Select(fate, "core.legacy.unknown", 1);
                    break;
                case "unselected-with-level":
                    Select(fate, string.Empty, 1);
                    break;
                case "selected-with-zero-level":
                    Select(fate, FateIds[0], 0);
                    break;
                case "void-debt-while-pocket":
                    Select(fate, FateIds[0], 1);
                    WriteField(ReadField(effects, "voidDebt"), "debts", new[]
                    {
                        new FormalThreeDVoidDebtEntrySaveData
                        {
                            resourceId = "core.resource.stone",
                            amount = 2,
                        },
                    });
                    break;
                case "negative-void-debt":
                    Select(fate, FateIds[1], 1);
                    WriteField(ReadField(effects, "voidDebt"), "debts", new[]
                    {
                        new FormalThreeDVoidDebtEntrySaveData
                        {
                            resourceId = "core.resource.stone",
                            amount = -1,
                        },
                    });
                    break;
                case "pressure-active-without-campaign":
                    WriteField(pressure, "entries", new[]
                    {
                        new FormalThreeDAttentionPressureEntrySaveData
                        {
                            threshold = 30,
                            state = (int)AttentionPressureState.Active,
                        },
                    });
                    break;
                case "pressure-active-id-mismatch":
                    WriteField(pressure, "entries", new[]
                    {
                        new FormalThreeDAttentionPressureEntrySaveData
                        {
                            threshold = 30,
                            state = (int)AttentionPressureState.Active,
                        },
                    });
                    WriteField(pressure, "activeEncounterId",
                        "core.attention-encounter.high-risk-attack");
                    WriteField(pressure, "activeCampaign",
                        new FormalThreeDPressureCampaignSaveData
                        {
                            campaignId =
                                "core.attention-encounter.high-risk-attack",
                        });
                    break;
                case "fate-two-civilization-one":
                    Select(fate, FateIds[0], 2);
                    WriteField(ReadField(effects, "pocketUniverse"),
                        "level", 2);
                    break;
                case "civilization-two-fate-one":
                    Select(fate, FateIds[0], 1);
                    ConfigureCompletedCivilization(civilization);
                    break;
                case "civilization-two-missing-lock":
                    Select(fate, FateIds[0], 2);
                    WriteField(ReadField(effects, "pocketUniverse"),
                        "level", 2);
                    ConfigureCompletedCivilization(civilization);
                    WriteField(civilization, "committedAscensionIds",
                        Array.Empty<string>());
                    break;
                case "civilization-two-wrong-id":
                    Select(fate, FateIds[0], 2);
                    WriteField(ReadField(effects, "pocketUniverse"),
                        "level", 2);
                    ConfigureCompletedCivilization(civilization);
                    WriteField(civilization, "ascensionId", "wrong.id");
                    break;
                case "civilization-two-no-sequence":
                    Select(fate, FateIds[0], 2);
                    WriteField(ReadField(effects, "pocketUniverse"),
                        "level", 2);
                    ConfigureCompletedCivilization(civilization);
                    WriteField(civilization, "sequenceStage",
                        (int)AdvancementSequenceStage.None);
                    break;
                case "civilization-two-invalid-time":
                    Select(fate, FateIds[0], 2);
                    WriteField(ReadField(effects, "pocketUniverse"),
                        "level", 2);
                    ConfigureCompletedCivilization(civilization);
                    WriteField(civilization, "sequenceStage",
                        (int)AdvancementSequenceStage.Results);
                    WriteField(civilization, "remainingRuleSeconds", 1f);
                    break;
            }

            Rehash(envelope);
            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);
            Assert.That(result.IsValid, Is.False, fault);
            Assert.That(result.FieldPath, Does.StartWith("formal3D.progression"));
        }

        private static void ConfigureCompletedCivilization(object civilization)
        {
            WriteField(civilization, "level", 2);
            WriteField(civilization, "revision", 1ul);
            WriteField(civilization, "ascensionId",
                "first-civilization-ascension");
            WriteField(civilization, "ascensionCompleted", true);
            WriteField(civilization, "sequenceStage",
                (int)AdvancementSequenceStage.Scanning);
            WriteField(civilization, "remainingRuleSeconds", 2f);
            WriteField(civilization, "committedAscensionIds", new[]
            {
                "first-civilization-ascension",
            });
        }

        private static void Select(object fate, string id, int level)
        {
            WriteField(fate, "offeredIds", FateIds.ToArray());
            WriteField(fate, "selectedId", id);
            WriteField(fate, "level", level);
        }

        private static FormalSaveEnvelope MigratedFixture()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope, Is.Not.Null);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(33));
            Assert.That(ReadField(decoded.Envelope.formal3D, "progression"),
                Is.Not.Null);
            return decoded.Envelope;
        }

        private static void Rehash(FormalSaveEnvelope envelope)
        {
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
        }

        private static string ReadFixture(string name)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Tests/Fixtures/Persistence",
                name));
        }

        private static FieldInfo RequireField(
            Type owner,
            string name,
            Type expectedType = null)
        {
            FieldInfo field = owner.GetField(name, PublicInstance);
            Assert.That(field, Is.Not.Null, owner.FullName + "." + name);
            if (expectedType != null)
                Assert.That(field.FieldType, Is.EqualTo(expectedType), name);
            return field;
        }

        private static FieldInfo RequireArray(
            Type owner,
            string name,
            Type elementType = null)
        {
            FieldInfo field = RequireField(owner, name);
            Assert.That(field.FieldType.IsArray, Is.True, name);
            if (elementType != null)
                Assert.That(field.FieldType.GetElementType(), Is.EqualTo(elementType));
            return field;
        }

        private static object ReadField(object owner, string name) =>
            RequireField(owner.GetType(), name).GetValue(owner);

        private static T Read<T>(object owner, string name) =>
            (T)ReadField(owner, name);

        private static void WriteField(object owner, string name, object value)
        {
            RequireField(owner.GetType(), name).SetValue(owner, value);
        }
    }
}
