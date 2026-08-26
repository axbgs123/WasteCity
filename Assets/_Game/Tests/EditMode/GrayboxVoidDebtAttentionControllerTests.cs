using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxVoidDebtAttentionControllerTests
    {
        private const string TypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxVoidDebtAttentionController3D";

        [Test]
        public void IDEA0020_SelectedDebtSettlesAtomicallyIntoAttention()
        {
            var attention = new FormalAttentionRuntime();
            var fate = Selected(FormalFateCatalog.VoidDebtId);
            var debt = Debt(25);
            object controller = Create(attention, fate, debt);

            Assert.That(Tick(controller, 30f, out string error), Is.True, error);
            Assert.That(attention.Value, Is.EqualTo(12));
            Assert.That(attention.Capture().History.Select(value =>
                    value.StableEventKey),
                Is.EqualTo(new[]
                {
                    "void-debt:000001:unit:0001",
                    "void-debt:000001:unit:0002",
                }));
            Assert.That(debt.NextSettlementOrdinal, Is.EqualTo(2ul));
            Assert.That(debt.SettlementRemainingSeconds,
                Is.EqualTo(30d).Within(.0001d));
        }

        [Test]
        public void IDEA0020_PauseOtherFateAndAttentionFailureAreZeroWrite()
        {
            var attention = new FormalAttentionRuntime();
            var fate = Selected(FormalFateCatalog.VoidDebtId);
            var debt = Debt(10);
            object controller = Create(attention, fate, debt);
            string beforePause = Fingerprint(attention, debt);
            Assert.That(Tick(controller, 0f, out _), Is.True);
            Assert.That(Fingerprint(attention, debt), Is.EqualTo(beforePause));

            Assert.That(attention.TryApply(
                "core.attention.fate.void-debt-periodic",
                "void-debt:000001:unit:0001",
                out _), Is.True);
            string blocked = Fingerprint(attention, debt);
            Assert.That(Tick(controller, 30f, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Fingerprint(attention, debt), Is.EqualTo(blocked));

            var otherAttention = new FormalAttentionRuntime();
            var otherDebt = Debt(10);
            object other = Create(
                otherAttention,
                Selected(FormalFateCatalog.PocketUniverseId),
                otherDebt);
            string otherBefore = Fingerprint(otherAttention, otherDebt);
            Assert.That(Tick(other, 30f, out _), Is.True);
            Assert.That(Fingerprint(otherAttention, otherDebt),
                Is.EqualTo(otherBefore));
        }

        private static FormalVoidDebtRuntime Debt(int amount)
        {
            var debt = new FormalVoidDebtRuntime();
            Assert.That(debt.TryBorrowConstruction(
                "core.resource.stone",
                amount,
                out string error), Is.True, error);
            return debt;
        }

        private static FormalFateRuntime Selected(string id)
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(id, out _, out _, out string error),
                Is.True, error);
            return fate;
        }

        private static object Create(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            FormalVoidDebtRuntime debt)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(TypeName, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, TypeName);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(FormalAttentionRuntime),
                typeof(FormalFateRuntime),
                typeof(FormalVoidDebtRuntime),
            });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[] { attention, fate, debt });
        }

        private static bool Tick(
            object controller,
            float delta,
            out string error)
        {
            object[] arguments = { delta, null };
            bool result = (bool)controller.GetType().GetMethod(
                "Tick",
                BindingFlags.Public | BindingFlags.Instance)
                .Invoke(controller, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static string Fingerprint(
            FormalAttentionRuntime attention,
            FormalVoidDebtRuntime debt)
        {
            return attention.Revision + "|" + attention.Value + "|" +
                debt.Revision + "|" + debt.NextSettlementOrdinal + "|" +
                debt.SettlementRemainingSeconds;
        }
    }
}
