using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class FormalAttentionRuntimeTests
    {
        private const string RuntimeTypeName =
            "WasteCity.Progression.FormalAttentionRuntime, WasteCity.Game";
        private const string SnapshotTypeName =
            "WasteCity.Progression.FormalAttentionSnapshot, WasteCity.Game";
        private const string HistoryTypeName =
            "WasteCity.Progression.FormalAttentionHistoryEntry, " +
            "WasteCity.Game";

        [Test]
        public void IDEA0020_RuntimeStartsAtTenAndClampsPositiveAndNegativeChanges()
        {
            object runtime = CreateRuntime();
            Assert.That(Read<int>(runtime, "Value"), Is.EqualTo(10));
            Assert.That(Read<ulong>(runtime, "Revision"), Is.Zero);

            Assert.That(Apply(runtime,
                "core.attention.civilization.advanced", "advance.1"), Is.True);
            Assert.That(Read<int>(runtime, "Value"), Is.EqualTo(35));
            Assert.That(Apply(runtime,
                "core.attention.escape.locked-region", "escape.1"), Is.True);
            Assert.That(Read<int>(runtime, "Value"), Is.EqualTo(27));
            for (var index = 0; index < 100; index++)
                Apply(runtime, "core.attention.fate.void-debt-periodic",
                    "debt." + index);
            Assert.That(Read<int>(runtime, "Value"), Is.EqualTo(100));
            object latest = Sequence(Capture(runtime), "RecentHistory").Last();
            Assert.That(Read<int>(latest, "AppliedDelta"), Is.Zero,
                "Events at the clamp are still consumed and recorded.");
        }

        [Test]
        public void IDEA0020_StableEventsAndOneShotReasonsAreIdempotent()
        {
            object runtime = CreateRuntime();
            Assert.That(Apply(runtime,
                "core.attention.city.first-deployment", "deploy.1"), Is.True);
            object first = Capture(runtime);
            Assert.That(Apply(runtime,
                "core.attention.city.first-deployment", "deploy.1"), Is.False);
            Assert.That(Apply(runtime,
                "core.attention.city.first-deployment", "deploy.2"), Is.False,
                "Once-per-session reasons ignore alternate event keys.");
            Assert.That(Capture(runtime), Is.SameAs(first));

            Assert.That(Apply(runtime,
                "core.attention.building.machine-gun-turret", "tower.1"),
                Is.True);
            Assert.That(Apply(runtime,
                "core.attention.building.machine-gun-turret", "tower.2"),
                Is.True);
            Assert.That(Apply(runtime,
                "core.attention.building.machine-gun-turret", "tower.2"),
                Is.False);
            Assert.That(Apply(runtime, "unknown.reason", "unknown.1"),
                Is.False);
        }

        [Test]
        public void IDEA0020_ThresholdsReachThirtySixtyNinetyOnlyOnce()
        {
            object runtime = CreateRuntime();
            for (var index = 0; index < 90; index++)
                Apply(runtime, "core.attention.fate.void-debt-periodic",
                    "threshold." + index);
            Assert.That(IntSequence(Capture(runtime), "ReachedThresholds"),
                Is.EqualTo(new[] { 30, 60, 90 }));

            Apply(runtime, "core.attention.escape.locked-region", "escape.1");
            Apply(runtime, "core.attention.fate.rewind-anchor-used", "rewind.1");
            Assert.That(IntSequence(Capture(runtime), "ReachedThresholds"),
                Is.EqualTo(new[] { 30, 60, 90 }),
                "Lowering and re-crossing cannot duplicate thresholds.");
        }

        [Test]
        public void IDEA0020_HistoryIsBoundedTo128AndRecentContainsLatestThree()
        {
            object runtime = CreateRuntime();
            for (var index = 0; index < 140; index++)
                Assert.That(Apply(runtime,
                    "core.attention.fate.void-debt-periodic",
                    "history." + index), Is.True);

            object snapshot = Capture(runtime);
            object[] history = Sequence(snapshot, "History").ToArray();
            object[] recent = Sequence(snapshot, "RecentHistory").ToArray();
            Assert.That(history, Has.Length.EqualTo(128));
            Assert.That(recent, Has.Length.EqualTo(3));
            Assert.That(Read<string>(history[0], "StableEventKey"),
                Is.EqualTo("history.12"));
            Assert.That(recent.Select(value =>
                    Read<string>(value, "StableEventKey")),
                Is.EqualTo(new[] { "history.137", "history.138", "history.139" }));
        }

        [Test]
        public void IDEA0020_CaptureRestorePreservesUnknownReasonAsOrphanEvidence()
        {
            Type historyType = RequireType(HistoryTypeName);
            Type snapshotType = RequireType(SnapshotTypeName);
            object unknown = Activator.CreateInstance(historyType, new object[]
            {
                "mod.attention.unknown",
                "mod.event.1",
                7,
                7,
                42,
                7ul,
            });
            Array history = Array.CreateInstance(historyType, 1);
            history.SetValue(unknown, 0);
            object restoredSnapshot = Activator.CreateInstance(snapshotType,
                new object[]
                {
                    42,
                    7ul,
                    history,
                    new[] { 30 },
                    new[] { "mod.event.1" },
                    Array.Empty<string>(),
                });
            object runtime = CreateRuntime();

            Assert.That(Restore(runtime, restoredSnapshot), Is.True);
            object captured = Capture(runtime);
            Assert.That(Read<int>(captured, "Value"), Is.EqualTo(42));
            Assert.That(Read<ulong>(captured, "Revision"), Is.EqualTo(7ul));
            Assert.That(Read<string>(Sequence(captured, "History").Single(),
                "ReasonId"), Is.EqualTo("mod.attention.unknown"));
            Assert.That(Apply(runtime, "mod.attention.unknown", "mod.event.2"),
                Is.False,
                "Unknown restored reasons are preserved but cannot be applied.");

            object beforeInvalid = Capture(runtime);
            object invalid = Activator.CreateInstance(snapshotType,
                new object[]
                {
                    -1,
                    8ul,
                    history,
                    Array.Empty<int>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                });
            Assert.That(Restore(runtime, invalid), Is.False);
            Assert.That(Capture(runtime), Is.SameAs(beforeInvalid));
        }

        [Test]
        public void IDEA0020_StaticCaptureReturnsCachedSnapshotWithoutAllocation()
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

        [Test]
        public void IDEA0020_ThresholdEventsPublishCommittedFactsOnceWithoutRestoreReplay()
        {
            object runtime = CreateRuntime();
            EventInfo thresholdReached = runtime.GetType().GetEvent(
                "ThresholdReached",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(thresholdReached, Is.Not.Null);
            Assert.That(thresholdReached.EventHandlerType,
                Is.EqualTo(typeof(Action<int>)));
            var observed = new System.Collections.Generic.List<int>();
            Action<int> throwing = _ => throw new InvalidOperationException(
                "injected threshold observer failure");
            Action<int> healthy = value => observed.Add(value);
            thresholdReached.AddEventHandler(runtime, throwing);
            thresholdReached.AddEventHandler(runtime, healthy);

            for (var index = 0; index < 90; index++)
            {
                Assert.That(Apply(
                    runtime,
                    "core.attention.fate.void-debt-periodic",
                    "threshold.event." + index), Is.True);
            }
            Assert.That(observed, Is.EqualTo(new[] { 30, 60, 90 }));

            object snapshot = Capture(runtime);
            object restored = CreateRuntime();
            var replayed = new System.Collections.Generic.List<int>();
            thresholdReached = restored.GetType().GetEvent(
                "ThresholdReached");
            thresholdReached.AddEventHandler(
                restored,
                new Action<int>(value => replayed.Add(value)));
            Assert.That(Restore(restored, snapshot), Is.True);
            Assert.That(replayed, Is.Empty,
                "Persistence restore cannot replay threshold facts.");
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

        private static bool Apply(
            object runtime,
            string reasonId,
            string stableEventKey)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryApply",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { reasonId, stableEventKey, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            Assert.That(arguments[2], Is.TypeOf<string>());
            if (result) Assert.That((string)arguments[2], Is.Empty);
            else Assert.That((string)arguments[2], Is.Not.Empty);
            return result;
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
            var arguments = new[] { snapshot, null };
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

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
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

        private static IEnumerable<object> Sequence(
            object owner,
            string propertyName)
        {
            object value = Read<object>(owner, propertyName);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>();
        }

        private static int[] IntSequence(object owner, string propertyName) =>
            Sequence(owner, propertyName)
                .Select(value => Convert.ToInt32(value))
                .ToArray();
    }
}
