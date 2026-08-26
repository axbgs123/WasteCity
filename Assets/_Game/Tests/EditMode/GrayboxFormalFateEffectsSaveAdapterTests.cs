using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalFateEffectsSaveAdapterTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxFormalProgressionSaveAdapter3D";

        [Test]
        public void IDEA0020_AdapterCapturesAndRestoresPocketAndVoidEffectTruth()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.PocketUniverseId,
                out _,
                out _,
                out string error), Is.True, error);
            var pocket = new PocketUniverseFateEffect();
            const string flagshipId = "building.instance.000001";
            Assert.That(pocket.SelectFlagships(new[]
            {
                new PocketUniverseBuildingCandidate(
                    flagshipId,
                    "core.building.smelter",
                    true,
                    true),
            }), Is.EqualTo(1));
            Assert.That(pocket.TryCommitFirstProduction(
                flagshipId,
                out _), Is.True);
            Assert.That(pocket.TryCreateCollapseCommand(
                flagshipId,
                4,
                5,
                out _), Is.True);
            var debt = new FormalVoidDebtRuntime();
            object source = CreateAdapter(attention, fate, pocket, debt);

            FormalThreeDProgressionSaveData payload = Capture(source);
            object effects = ReadField(payload, "fateEffects");
            object pocketData = ReadField(effects, "pocketUniverse");
            Assert.That(ReadArray(pocketData, "flagships"), Has.Length.EqualTo(1));
            Assert.That(ReadField<string>(pocketData,
                "firstProductionFlagshipId"), Is.EqualTo(flagshipId));
            Assert.That(ReadArray(pocketData, "collapsedFlagshipIds"),
                Is.EqualTo(new[] { flagshipId }));
            object voidData = ReadField(effects, "voidDebt");
            Assert.That(ReadArray(voidData, "debts"), Is.Empty);

            var restoredAttention = new FormalAttentionRuntime();
            var restoredFate = new FormalFateRuntime();
            var restoredPocket = new PocketUniverseFateEffect();
            var restoredDebt = new FormalVoidDebtRuntime();
            object target = CreateAdapter(
                restoredAttention,
                restoredFate,
                restoredPocket,
                restoredDebt);
            Assert.That(TryRestore(target, payload, out error), Is.True, error);
            Assert.That(restoredFate.Capture().SelectedId,
                Is.EqualTo(FormalFateCatalog.PocketUniverseId));
            Assert.That(restoredPocket.Capture().Flagships[0].StableInstanceId,
                Is.EqualTo(flagshipId));
            Assert.That(restoredPocket.Capture().FirstProductionFlagshipId,
                Is.EqualTo(flagshipId));
            Assert.That(restoredPocket.Capture().CollapsedFlagshipIds,
                Is.EqualTo(new[] { flagshipId }));
            Assert.That(restoredDebt.TotalDebt, Is.Zero);
        }

        [Test]
        public void IDEA0020_SelectedVoidDebtRoundTripsDebtClockAndOrdinal()
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.VoidDebtId,
                out _,
                out _,
                out string error), Is.True, error);
            var debt = new FormalVoidDebtRuntime();
            Assert.That(debt.TryBorrowConstruction(
                "core.resource.stone",
                25,
                out error), Is.True, error);
            Assert.That(debt.Tick(7f, out _, out error), Is.True, error);
            object source = CreateAdapter(
                new FormalAttentionRuntime(),
                fate,
                new PocketUniverseFateEffect(),
                debt);
            FormalThreeDProgressionSaveData payload = Capture(source);

            var restoredDebt = new FormalVoidDebtRuntime();
            object target = CreateAdapter(
                new FormalAttentionRuntime(),
                new FormalFateRuntime(),
                new PocketUniverseFateEffect(),
                restoredDebt);
            Assert.That(TryRestore(target, payload, out error), Is.True, error);
            Assert.That(restoredDebt.GetDebt("core.resource.stone"),
                Is.EqualTo(25));
            Assert.That(restoredDebt.SettlementRemainingSeconds,
                Is.EqualTo(23d).Within(.0001d));
            Assert.That(restoredDebt.NextSettlementOrdinal, Is.EqualTo(1ul));
        }

        [Test]
        public void IDEA0020_NonSelectedEffectStateIsRejectedWithoutPartialWrites()
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.PocketUniverseId,
                out _,
                out _,
                out string error), Is.True, error);
            var debt = new FormalVoidDebtRuntime();
            Assert.That(debt.TryBorrowConstruction(
                "core.resource.stone",
                5,
                out error), Is.True, error);
            var attention = new FormalAttentionRuntime();
            var pocket = new PocketUniverseFateEffect();
            object adapter = CreateAdapter(attention, fate, pocket, debt);
            FormalThreeDProgressionSaveData invalid = Capture(adapter);
            string before = Fingerprint(attention, fate, pocket, debt);

            Assert.That(TryRestore(adapter, invalid, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Fingerprint(attention, fate, pocket, debt),
                Is.EqualTo(before));
        }

        private static object CreateAdapter(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            PocketUniverseFateEffect pocket,
            FormalVoidDebtRuntime debt)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(AdapterTypeName, false))
                .First(value => value != null);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(FormalAttentionRuntime),
                typeof(FormalFateRuntime),
                typeof(PocketUniverseFateEffect),
                typeof(FormalVoidDebtRuntime),
                typeof(FormalRewindAnchorMetadataRuntime),
            });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[]
            {
                attention,
                fate,
                pocket,
                debt,
                new FormalRewindAnchorMetadataRuntime(),
            });
        }

        private static FormalThreeDProgressionSaveData Capture(object adapter)
        {
            return (FormalThreeDProgressionSaveData)adapter.GetType()
                .GetMethod("Capture")
                .Invoke(adapter, null);
        }

        private static bool TryRestore(
            object adapter,
            FormalThreeDProgressionSaveData data,
            out string error)
        {
            object[] arguments = { data, null };
            bool result = (bool)adapter.GetType().GetMethod("TryRestore")
                .Invoke(adapter, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static object ReadField(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name);
            Assert.That(field, Is.Not.Null, name);
            object value = field.GetValue(owner);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        private static T ReadField<T>(object owner, string name) =>
            (T)ReadField(owner, name);

        private static Array ReadArray(object owner, string name)
        {
            object value = ReadField(owner, name);
            Assert.That(value, Is.InstanceOf<Array>());
            return (Array)value;
        }

        private static string Fingerprint(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            PocketUniverseFateEffect pocket,
            FormalVoidDebtRuntime debt)
        {
            return attention.Revision + "|" + fate.Capture().Revision + "|" +
                pocket.Revision + "|" + debt.Revision + "|" +
                debt.TotalDebt;
        }
    }
}
