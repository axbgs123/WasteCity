using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class FormalVoidDebtRuntimeTests
    {
        private const string RuntimeTypeName =
            "WasteCity.Progression.FormalVoidDebtRuntime";
        private const string SnapshotTypeName =
            "WasteCity.Progression.FormalVoidDebtSnapshot";
        private const string EntryTypeName =
            "WasteCity.Progression.FormalVoidDebtEntry";

        [Test]
        public void IDEA0020_BorrowTracksPositiveDebtByResource()
        {
            object runtime = CreateRuntime();
            Assert.That(Read<int>(runtime, "Level"), Is.EqualTo(1));
            Assert.That(Read<int>(runtime, "TotalDebt"), Is.Zero);
            Assert.That(Read<double>(runtime, "SettlementRemainingSeconds"),
                Is.Zero);
            Assert.That(Read<ulong>(runtime, "NextSettlementOrdinal"),
                Is.EqualTo(1ul));

            Assert.That(Borrow(runtime, "core.resource.iron", 7), Is.True);
            Assert.That(Borrow(runtime, "core.resource.stone", 13), Is.True);
            Assert.That(Read<int>(runtime, "TotalDebt"), Is.EqualTo(20));
            Assert.That(GetDebt(runtime, "core.resource.iron"), Is.EqualTo(7));
            Assert.That(GetDebt(runtime, "core.resource.stone"), Is.EqualTo(13));
            Assert.That(Borrow(runtime, "core.resource.iron", 0), Is.False);
            Assert.That(Borrow(runtime, "unknown.resource.id", 1), Is.False);
            Assert.That(Read<int>(runtime, "TotalDebt"), Is.EqualTo(20));

            object snapshot = Capture(runtime);
            string[] ids = Sequence(snapshot, "Debts")
                .Select(value => Read<string>(value, "ResourceId"))
                .ToArray();
            Assert.That(ids, Is.EqualTo(new[]
            {
                "core.resource.iron",
                "core.resource.stone",
            }));
        }

        [Test]
        public void IDEA0020_RepayOnlyConsumesMatchingDebtAndReturnsResidual()
        {
            object runtime = CreateRuntime();
            Assert.That(Borrow(runtime, "core.resource.iron", 12), Is.True);
            Assert.That(Borrow(runtime, "core.resource.stone", 5), Is.True);
            Assert.That(Tick(runtime, 5f), Is.Empty);
            Assert.That(Read<double>(runtime, "SettlementRemainingSeconds"),
                Is.EqualTo(25d).Within(.0001d));

            Assert.That(Repay(
                runtime,
                "core.resource.iron",
                7,
                out int repaid,
                out int residual), Is.True);
            Assert.That(repaid, Is.EqualTo(7));
            Assert.That(residual, Is.Zero);
            Assert.That(GetDebt(runtime, "core.resource.iron"), Is.EqualTo(5));
            Assert.That(GetDebt(runtime, "core.resource.stone"), Is.EqualTo(5));

            Assert.That(Repay(
                runtime,
                "core.resource.iron",
                20,
                out repaid,
                out residual), Is.True);
            Assert.That(repaid, Is.EqualTo(5));
            Assert.That(residual, Is.EqualTo(15));
            Assert.That(GetDebt(runtime, "core.resource.iron"), Is.Zero);
            Assert.That(GetDebt(runtime, "core.resource.stone"), Is.EqualTo(5));

            Assert.That(Repay(
                runtime,
                "core.resource.stone",
                10,
                out repaid,
                out residual), Is.True);
            Assert.That(repaid, Is.EqualTo(5));
            Assert.That(residual, Is.EqualTo(5));
            Assert.That(Read<int>(runtime, "TotalDebt"), Is.Zero);
            Assert.That(Read<double>(runtime, "SettlementRemainingSeconds"),
                Is.Zero,
                "Clearing all debt resets the periodic settlement clock.");
        }

        [Test]
        public void IDEA0020_LevelOneSettlementCreatesOneStableKeyPerTenDebt()
        {
            object runtime = CreateRuntime();
            Assert.That(Borrow(runtime, "core.resource.iron", 35), Is.True);
            Assert.That(Tick(runtime, 29f), Is.Empty);
            Assert.That(Tick(runtime, 1f), Is.EqualTo(new[]
            {
                "void-debt:000001:unit:0001",
                "void-debt:000001:unit:0002",
                "void-debt:000001:unit:0003",
            }));
            Assert.That(Read<ulong>(runtime, "NextSettlementOrdinal"),
                Is.EqualTo(2ul));
            Assert.That(Read<double>(runtime, "SettlementRemainingSeconds"),
                Is.EqualTo(30d).Within(.0001d));

            object small = CreateRuntime();
            Assert.That(Borrow(small, "core.resource.iron", 9), Is.True);
            Assert.That(Tick(small, 30f), Is.Empty);
            Assert.That(Read<ulong>(small, "NextSettlementOrdinal"),
                Is.EqualTo(2ul),
                "A zero-charge settlement still advances its stable cycle.");
        }

        [Test]
        public void IDEA0020_LevelTwoUsesSixtySecondsAndSplitTicksAreDeterministic()
        {
            object whole = CreateRuntime(level: 2);
            object split = CreateRuntime(level: 2);
            Assert.That(Borrow(whole, "core.resource.iron", 20), Is.True);
            Assert.That(Borrow(split, "core.resource.iron", 20), Is.True);

            Assert.That(Tick(whole, 0f), Is.Empty,
                "A paused zero delta cannot advance the debt clock.");
            Assert.That(Tick(whole, 59f), Is.Empty);
            string[] wholeKeys = Tick(whole, 1f);
            Assert.That(wholeKeys, Has.Length.EqualTo(2));

            Assert.That(Tick(split, 10f), Is.Empty);
            Assert.That(Tick(split, 20f), Is.Empty);
            string[] splitKeys = Tick(split, 30f);
            Assert.That(splitKeys, Is.EqualTo(wholeKeys));
            Assert.That(Read<double>(split, "SettlementRemainingSeconds"),
                Is.EqualTo(Read<double>(whole, "SettlementRemainingSeconds"))
                    .Within(.0001d));
            Assert.That(Read<ulong>(split, "NextSettlementOrdinal"),
                Is.EqualTo(Read<ulong>(whole, "NextSettlementOrdinal")));
        }

        [Test]
        public void IDEA0020_SnapshotDeepCopiesAndInvalidRestoreIsAtomic()
        {
            Type entryType = RequireType(EntryTypeName);
            Type snapshotType = RequireType(SnapshotTypeName);
            Array debts = Array.CreateInstance(entryType, 1);
            object iron = Activator.CreateInstance(
                entryType,
                "core.resource.iron",
                20);
            debts.SetValue(iron, 0);
            object snapshot = Activator.CreateInstance(
                snapshotType,
                2,
                45d,
                7ul,
                9ul,
                debts);
            debts.SetValue(Activator.CreateInstance(
                entryType,
                "core.resource.stone",
                99), 0);

            object runtime = CreateRuntime();
            Assert.That(Restore(runtime, snapshot), Is.True);
            Assert.That(Read<int>(runtime, "Level"), Is.EqualTo(2));
            Assert.That(GetDebt(runtime, "core.resource.iron"), Is.EqualTo(20));
            Assert.That(GetDebt(runtime, "core.resource.stone"), Is.Zero);
            Assert.That(Read<double>(runtime, "SettlementRemainingSeconds"),
                Is.EqualTo(45d));
            Assert.That(Read<ulong>(runtime, "NextSettlementOrdinal"),
                Is.EqualTo(7ul));
            Assert.That(Read<ulong>(runtime, "Revision"), Is.EqualTo(9ul));

            object before = Capture(runtime);
            Array duplicates = Array.CreateInstance(entryType, 2);
            duplicates.SetValue(iron, 0);
            duplicates.SetValue(iron, 1);
            object invalid = Activator.CreateInstance(
                snapshotType,
                1,
                30d,
                8ul,
                10ul,
                duplicates);
            Assert.That(Restore(runtime, invalid), Is.False);
            Assert.That(Capture(runtime), Is.SameAs(before));
        }

        [Test]
        public void IDEA0020_StaticCaptureReturnsSameSnapshotForThreeHundredReads()
        {
            object runtime = CreateRuntime();
            object expected = Capture(runtime);
            Func<object, object> capture = CompileCapture(runtime.GetType());
            capture(runtime);

            long before = GC.GetAllocatedBytesForCurrentThread();
            bool same = true;
            for (var index = 0; index < 300; index++)
                same &= ReferenceEquals(capture(runtime), expected);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(same, Is.True);
            Assert.That(allocated, Is.Zero);
        }

        private static object CreateRuntime(int level = 1)
        {
            Type type = RequireType(RuntimeTypeName);
            return level == 1
                ? Activator.CreateInstance(type)
                : Activator.CreateInstance(type, level);
        }

        private static bool Borrow(object runtime, string resourceId, int amount)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryBorrowConstruction",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(int),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { resourceId, amount, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            Assert.That(arguments[2], Is.TypeOf<string>());
            return result;
        }

        private static bool Repay(
            object runtime,
            string resourceId,
            int amount,
            out int repaid,
            out int residual)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "Repay",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(int),
                    typeof(int).MakeByRefType(),
                    typeof(int).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { resourceId, amount, 0, 0, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            repaid = (int)arguments[2];
            residual = (int)arguments[3];
            Assert.That(arguments[4], Is.TypeOf<string>());
            return result;
        }

        private static string[] Tick(object runtime, float delta)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "Tick",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(float),
                    typeof(IReadOnlyList<string>).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { delta, null, null };
            Assert.That((bool)method.Invoke(runtime, arguments), Is.True,
                arguments[2] as string);
            Assert.That(arguments[1], Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)arguments[1]).Cast<object>()
                .Select(value => value.ToString())
                .ToArray();
        }

        private static int GetDebt(object runtime, string resourceId)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "GetDebt",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(runtime, new object[] { resourceId });
        }

        private static object Capture(object runtime)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "Capture",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(RequireType(SnapshotTypeName)));
            return method.Invoke(runtime, null);
        }

        private static bool Restore(object runtime, object snapshot)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryRestore",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    RequireType(SnapshotTypeName),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { snapshot, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            Assert.That(arguments[1], Is.TypeOf<string>());
            return result;
        }

        private static Func<object, object> CompileCapture(Type runtimeType)
        {
            MethodInfo method = runtimeType.GetMethod(
                "Capture",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            ParameterExpression instance = Expression.Parameter(
                typeof(object),
                "instance");
            MethodCallExpression call = Expression.Call(
                Expression.Convert(instance, runtimeType),
                method);
            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(call, typeof(object)),
                instance).Compile();
        }

        private static IEnumerable<object> Sequence(
            object owner,
            string propertyName)
        {
            object value = Read<object>(owner, propertyName);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>();
        }

        private static T Read<T>(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(owner);
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
