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
    public sealed class FormalSaveSchema33MigrationTests
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
        public void IDEA0020_SchemaThirtyTwoMigratesToCleanProgressionDefaults()
        {
            FormalSaveEnvelope current = DecodeFixtureEnvelope();
            FieldInfo progression = RequireProgressionField();
            progression.SetValue(current.formal3D, null);
            current.saveSchemaVersion = 32;
            current.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(current.formal3D);

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                FormalSaveCodec.EncodeEnvelope(current));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(33));
            AssertDefaults(decoded.Envelope.formal3D);
        }

        [Test]
        public void IDEA0020_SchemaThirtyOneTraversesThirtyTwoIntoThirtyThree()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));

            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.PayloadKind,
                Is.EqualTo(FormalSavePayloadKind.Formal3D));
            Assert.That(decoded.Envelope.saveSchemaVersion, Is.EqualTo(33));
            Assert.That(decoded.Envelope.formal3D.defenseCampaign, Is.Not.Null,
                "The schema 31 to 32 campaign migration must still run.");
            AssertDefaults(decoded.Envelope.formal3D);
        }

        [Test]
        public void IDEA0020_HistoricalSchemasOneThroughThirtyRemainLegacyTwoD()
        {
            for (var schema = 1; schema <= 30; schema++)
            {
                FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                    "{\"schema\":" + schema +
                    ",\"worldSeed\":8128,\"observation\":99," +
                    "\"legacyPathId\":\"core.legacy.void-debt\"}");
                Assert.That(decoded.Success, Is.True, decoded.Message);
                Assert.That(decoded.PayloadKind,
                    Is.EqualTo(FormalSavePayloadKind.Legacy2D), schema.ToString());
                Assert.That(decoded.Legacy2D.schema, Is.EqualTo(schema));
                Assert.That(decoded.Envelope, Is.Null);
            }
        }

        private static void AssertDefaults(FormalThreeDSaveData payload)
        {
            object progression = RequireProgressionField().GetValue(payload);
            Assert.That(progression, Is.Not.Null);
            object attention = ReadField(progression, "attention");
            object fate = ReadField(progression, "fate");
            object effects = ReadField(progression, "fateEffects");
            object pressure = ReadField(progression, "pressure");
            object civilization = ReadField(progression, "civilization");

            Assert.That(Read<int>(attention, "value"), Is.EqualTo(10),
                "Migration must not infer attention from old buildings, " +
                "research, observation, or legacy-path fields.");
            Assert.That(Read<ulong>(attention, "revision"), Is.Zero);
            Assert.That(Sequence(attention, "history"), Is.Empty);
            Assert.That(Sequence(attention, "reachedThresholds"), Is.Empty);
            Assert.That(Sequence(attention, "committedStableEventKeys"), Is.Empty);
            Assert.That(Sequence(attention, "completedOneShotReasonIds"), Is.Empty);

            Assert.That(Sequence(fate, "offeredIds").Cast<string>(),
                Is.EqualTo(FateIds));
            Assert.That(Read<string>(fate, "selectedId"), Is.Empty);
            Assert.That(Read<int>(fate, "level"), Is.Zero);
            Assert.That(Read<ulong>(fate, "revision"), Is.Zero);
            object pocket = ReadField(effects, "pocketUniverse");
            object debt = ReadField(effects, "voidDebt");
            object rewind = ReadField(effects, "rewindAnchors");
            Assert.That(Sequence(pocket, "flagships"), Is.Empty);
            Assert.That(Sequence(pocket, "collapsedFlagshipIds"), Is.Empty);
            Assert.That(Read<string>(pocket, "firstProductionFlagshipId"),
                Is.Empty);
            Assert.That(Sequence(debt, "debts"), Is.Empty);
            Assert.That(Read<double>(debt, "settlementRemainingSeconds"),
                Is.Zero);
            Assert.That(Read<ulong>(debt, "nextSettlementOrdinal"),
                Is.EqualTo(1ul));
            Assert.That(Sequence(rewind, "anchors"), Is.Empty);
            Assert.That(Read<long>(rewind, "nextCreationOrdinal"),
                Is.EqualTo(1L));
            Assert.That(Read<ulong>(pressure, "revision"), Is.Zero);
            Assert.That(Sequence(pressure, "entries"), Is.Empty);
            Assert.That(Read<string>(pressure, "activeEncounterId"), Is.Empty);
            Assert.That(ReadField(pressure, "activeCampaign"), Is.Null);
            Assert.That(Read<int>(civilization, "level"), Is.EqualTo(1));
            Assert.That(Read<ulong>(civilization, "revision"), Is.Zero);
            Assert.That(Read<string>(civilization, "ascensionId"), Is.Empty);
            Assert.That(Read<bool>(civilization, "ascensionCompleted"),
                Is.False);
            Assert.That(Read<int>(civilization, "sequenceStage"),
                Is.EqualTo(0));
            Assert.That(Read<float>(civilization, "remainingRuleSeconds"),
                Is.Zero);
            Assert.That(Sequence(civilization, "committedAscensionIds"),
                Is.Empty);
        }

        private static FormalSaveEnvelope DecodeFixtureEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope, Is.Not.Null);
            return decoded.Envelope;
        }

        private static FieldInfo RequireProgressionField()
        {
            FieldInfo field = typeof(FormalThreeDSaveData).GetField(
                "progression",
                PublicInstance);
            Assert.That(field, Is.Not.Null,
                "Schema 33 requires FormalThreeDSaveData.progression.");
            return field;
        }

        private static object ReadField(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, PublicInstance);
            Assert.That(field, Is.Not.Null, owner.GetType().FullName + "." + name);
            return field.GetValue(owner);
        }

        private static T Read<T>(object owner, string name) =>
            (T)ReadField(owner, name);

        private static IEnumerable Sequence(object owner, string name)
        {
            object value = ReadField(owner, name);
            Assert.That(value, Is.Not.Null, name);
            Assert.That(value, Is.InstanceOf<IEnumerable>(), name);
            return (IEnumerable)value;
        }

        private static string ReadFixture(string name)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Tests/Fixtures/Persistence",
                name));
        }
    }
}
