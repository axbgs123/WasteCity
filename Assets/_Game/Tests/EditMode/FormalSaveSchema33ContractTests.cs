using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

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

            RequireField(civilization, "level", typeof(int));
            RequireArray(civilization, "committedAscensionIds", typeof(string));
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
        public void IDEA0020_ValidatorRejectsInvalidProgressionTruth(string fault)
        {
            FormalSaveEnvelope envelope = MigratedFixture();
            object progression = ReadField(envelope.formal3D, "progression");
            object attention = ReadField(progression, "attention");
            object fate = ReadField(progression, "fate");
            object effects = ReadField(progression, "fateEffects");

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
            }

            Rehash(envelope);
            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);
            Assert.That(result.IsValid, Is.False, fault);
            Assert.That(result.FieldPath, Does.StartWith("formal3D.progression"));
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
