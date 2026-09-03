using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class FormalFateRuntimeTests
    {
        private const string RuntimeTypeName =
            "WasteCity.Progression.FormalFateRuntime, WasteCity.Game";
        private const string SnapshotTypeName =
            "WasteCity.Progression.FormalFateSnapshot, WasteCity.Game";
        private const string AttentionReasonId =
            "core.attention.fate.first-activation";
        private const string SelectionEventKey = "fate-selection-complete";

        private static readonly string[] FixedOffers =
        {
            "core.legacy.pocket-universe",
            "core.legacy.void-debt",
            "core.legacy.rewind-anchor",
        };

        private static readonly string[] AlternateOffers =
        {
            "core.legacy.quantum-entanglement",
            "core.legacy.spatial-template",
            "core.legacy.causal-transparency",
        };

        [Test]
        public void IDEA0020_NewRuntimeIsPendingAtLevelZeroWithCachedSnapshot()
        {
            object runtime = CreateRuntime();
            object first = Capture(runtime);
            object second = Capture(runtime);

            Assert.That(first, Is.SameAs(second));
            Assert.That(Read<ulong>(first, "Revision"), Is.Zero);
            Assert.That(Read<string>(first, "SelectedId"), Is.Null.Or.Empty);
            Assert.That(Read<int>(first, "Level"), Is.Zero);
            Assert.That(Read<bool>(first, "HasSelection"), Is.False);
            Assert.That(Read<bool>(runtime, "EffectsReady"), Is.True);
            Assert.That(StringSequence(first, "OfferedIds"),
                Is.EqualTo(FixedOffers));
        }

        [Test]
        public void IDEA0028_SeededRuntimeUsesTheFormalOfferSelector()
        {
            Type runtimeType = RequireType(RuntimeTypeName);
            object runtime = Activator.CreateInstance(
                runtimeType,
                new object[] { "session-alpha", 8128, 1 });
            Type selector = RequireType(
                "WasteCity.Progression.FormalFateOfferSelector, WasteCity.Game");
            MethodInfo select = selector.GetMethod(
                "Select",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(int), typeof(int) },
                null);
            Assert.That(select, Is.Not.Null);
            string[] expected = ((IEnumerable)select.Invoke(
                    null,
                    new object[] { "session-alpha", 8128, 1 }))
                .Cast<object>()
                .Select(value => value.ToString())
                .ToArray();

            Assert.That(StringSequence(Capture(runtime), "OfferedIds"),
                Is.EqualTo(expected));
        }

        [Test]
        public void IDEA0020_SelectCommitsOnceAndReturnsAttentionCommandKeys()
        {
            object runtime = CreateRuntime();
            Assert.That(TrySelect(
                runtime,
                FixedOffers[1],
                out string reasonId,
                out string eventKey,
                out string error), Is.True);
            Assert.That(reasonId, Is.EqualTo(AttentionReasonId));
            Assert.That(eventKey, Is.EqualTo(SelectionEventKey));
            Assert.That(error, Is.Empty);

            object selected = Capture(runtime);
            Assert.That(Read<ulong>(selected, "Revision"), Is.EqualTo(1ul));
            Assert.That(Read<string>(selected, "SelectedId"),
                Is.EqualTo(FixedOffers[1]));
            Assert.That(Read<int>(selected, "Level"), Is.EqualTo(1));
            Assert.That(Read<bool>(selected, "HasSelection"), Is.True);

            Assert.That(TrySelect(
                runtime,
                FixedOffers[0],
                out reasonId,
                out eventKey,
                out error), Is.False);
            Assert.That(reasonId, Is.Empty);
            Assert.That(eventKey, Is.Empty);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Capture(runtime), Is.SameAs(selected));
        }

        [Test]
        public void IDEA0020_UnknownOrBlankSelectionCannotMutateOrEmitAttention()
        {
            object runtime = CreateRuntime();
            object pending = Capture(runtime);
            foreach (string invalid in new[]
                {
                    null,
                    string.Empty,
                    "core.legacy.quantum-entanglement",
                    "unknown.fate.id",
                })
            {
                Assert.That(TrySelect(
                    runtime,
                    invalid,
                    out string reasonId,
                    out string eventKey,
                    out string error), Is.False);
                Assert.That(reasonId, Is.Empty);
                Assert.That(eventKey, Is.Empty);
                Assert.That(error, Is.Not.Empty);
                Assert.That(Capture(runtime), Is.SameAs(pending));
            }
        }

        [Test]
        public void IDEA0020_RestoreAcceptsOnlyPendingOrSelectedLevelOneState()
        {
            Type snapshotType = RequireType(SnapshotTypeName);
            object runtime = CreateRuntime();
            object selected = CreateSnapshot(
                snapshotType,
                revision: 7ul,
                selectedId: FixedOffers[2],
                level: 1);

            Assert.That(TryRestore(runtime, selected, out string error), Is.True);
            Assert.That(error, Is.Empty);
            object restored = Capture(runtime);
            Assert.That(Read<ulong>(restored, "Revision"), Is.EqualTo(7ul));
            Assert.That(Read<string>(restored, "SelectedId"),
                Is.EqualTo(FixedOffers[2]));
            Assert.That(Read<int>(restored, "Level"), Is.EqualTo(1));

            object pendingRuntime = CreateRuntime();
            object pending = CreateSnapshot(
                snapshotType,
                revision: 3ul,
                selectedId: null,
                level: 0);
            Assert.That(TryRestore(pendingRuntime, pending, out error), Is.True);
            Assert.That(error, Is.Empty);
            Assert.That(Read<int>(Capture(pendingRuntime), "Level"), Is.Zero);
        }

        [Test]
        public void IDEA0028_RestoreAdoptsAnyOrderedUniqueThreeFromApprovedPool()
        {
            Type snapshotType = RequireType(SnapshotTypeName);
            object runtime = CreateRuntime();
            object snapshot = CreateSnapshot(
                snapshotType,
                11ul,
                AlternateOffers[1],
                2,
                AlternateOffers);

            Assert.That(TryRestore(runtime, snapshot, out string error), Is.True,
                error);
            object restored = Capture(runtime);
            Assert.That(StringSequence(restored, "OfferedIds"),
                Is.EqualTo(AlternateOffers));
            Assert.That(Read<string>(restored, "SelectedId"),
                Is.EqualTo(AlternateOffers[1]));
            Assert.That(Read<int>(restored, "Level"), Is.EqualTo(2));
        }

        [Test]
        public void IDEA0028_RestorePreservesOfferSelectionVersion()
        {
            Type snapshotType = RequireType(SnapshotTypeName);
            object runtime = CreateRuntime();
            object snapshot = Activator.CreateInstance(
                snapshotType,
                new object[]
                {
                    0ul,
                    AlternateOffers,
                    string.Empty,
                    0,
                    1,
                });

            Assert.That(TryRestore(runtime, snapshot, out string error),
                Is.True, error);
            Assert.That(Read<int>(Capture(runtime), "OfferSelectionVersion"),
                Is.EqualTo(1));
        }

        [Test]
        public void IDEA0020_InvalidRestoreIsAtomicAndDoesNotOpenFutureLevels()
        {
            Type snapshotType = RequireType(SnapshotTypeName);
            object runtime = CreateRuntime();
            Assert.That(TrySelect(
                runtime,
                FixedOffers[0],
                out _,
                out _,
                out _), Is.True);
            object before = Capture(runtime);

            object[] invalid =
            {
                CreateSnapshot(snapshotType, 2ul, null, 1),
                CreateSnapshot(snapshotType, 2ul, FixedOffers[0], 0),
                CreateSnapshot(snapshotType, 2ul, FixedOffers[0], 3),
                CreateSnapshot(snapshotType, 2ul, "unknown.fate.id", 1),
                CreateSnapshot(
                    snapshotType,
                    2ul,
                    FixedOffers[0],
                    1,
                    new[] { FixedOffers[0], FixedOffers[0], FixedOffers[2] }),
                CreateSnapshot(
                    snapshotType,
                    2ul,
                    FixedOffers[0],
                    1,
                    new[] { FixedOffers[0], FixedOffers[1] }),
                CreateSnapshot(
                    snapshotType,
                    2ul,
                    FixedOffers[0],
                    1,
                    AlternateOffers),
            };

            foreach (object state in invalid)
            {
                Assert.That(TryRestore(runtime, state, out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(Capture(runtime), Is.SameAs(before));
            }

            Assert.That(runtime.GetType().GetMethod(
                "TryUpgrade",
                BindingFlags.Public | BindingFlags.Instance), Is.Null,
                "Task 2 records the selected Lv.1 state but does not expose upgrades.");
        }

        [Test]
        public void IDEA0020_SelectedLevelOnePromotesOnceAndLevelTwoRestores()
        {
            object runtime = CreateRuntime();
            Assert.That(TrySelect(runtime, FixedOffers[0], out _, out _, out _),
                Is.True);
            MethodInfo promote = runtime.GetType().GetMethod(
                "TryPromoteToLevelTwo");
            Assert.That(promote, Is.Not.Null);
            object[] arguments = { null };
            Assert.That((bool)promote.Invoke(runtime, arguments), Is.True,
                arguments[0] as string);
            Assert.That(Read<int>(Capture(runtime), "Level"), Is.EqualTo(2));
            Assert.That((bool)promote.Invoke(runtime, arguments), Is.False);

            object restored = CreateRuntime();
            object snapshot = CreateSnapshot(
                RequireType(SnapshotTypeName), 9UL, FixedOffers[0], 2);
            Assert.That(TryRestore(restored, snapshot, out string error),
                Is.True, error);
            Assert.That(Read<int>(Capture(restored), "Level"), Is.EqualTo(2));
        }

        [Test]
        public void IDEA0020_CaptureReturnsSameSnapshotFor300ReadsWithZeroAllocation()
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

        private static object CreateRuntime() =>
            Activator.CreateInstance(RequireType(RuntimeTypeName));

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

        private static bool TrySelect(
            object runtime,
            string fateId,
            out string attentionReasonId,
            out string stableEventKey,
            out string error)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TrySelect",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(string).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { fateId, null, null, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            attentionReasonId = arguments[1] as string;
            stableEventKey = arguments[2] as string;
            error = arguments[3] as string;
            Assert.That(attentionReasonId, Is.Not.Null);
            Assert.That(stableEventKey, Is.Not.Null);
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static bool TryRestore(
            object runtime,
            object snapshot,
            out string error)
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
            var arguments = new[] { snapshot, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            error = arguments[1] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static object CreateSnapshot(
            Type snapshotType,
            ulong revision,
            string selectedId,
            int level,
            string[] offeredIds = null)
        {
            return Activator.CreateInstance(snapshotType, new object[]
            {
                revision,
                offeredIds ?? FixedOffers,
                selectedId,
                level,
            });
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

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + "." + name);
            return (T)property.GetValue(owner);
        }

        private static string[] StringSequence(object owner, string name)
        {
            object value = Read<object>(owner, name);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>()
                .Select(item => item.ToString())
                .ToArray();
        }
    }
}
