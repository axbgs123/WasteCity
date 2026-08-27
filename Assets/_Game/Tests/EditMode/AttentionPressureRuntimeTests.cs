using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace WasteCity.Tests
{
    public sealed class AttentionPressureRuntimeTests
    {
        private const string RuntimeTypeName =
            "WasteCity.Progression.AttentionPressureRuntime";
        private const string SnapshotTypeName =
            "WasteCity.Progression.AttentionPressureSnapshot";
        private const string EntryTypeName =
            "WasteCity.Progression.AttentionPressureEntrySnapshot";
        private const string StateTypeName =
            "WasteCity.Progression.AttentionPressureState";

        [Test]
        public void IDEA0020_ThresholdsQueueOnceInAscendingOrder()
        {
            object runtime = CreateRuntime();
            Assert.That(Queue(runtime, 90), Is.True);
            Assert.That(Queue(runtime, 30), Is.True);
            Assert.That(Queue(runtime, 60), Is.True);
            object stable = Capture(runtime);
            Assert.That(Thresholds(stable), Is.EqualTo(new[] { 30, 60, 90 }));
            Assert.That(States(stable), Is.All.EqualTo("Queued"));
            Assert.That(Queue(runtime, 30), Is.False);
            Assert.That(Queue(runtime, 45), Is.False);
            Assert.That(Capture(runtime), Is.SameAs(stable));
        }

        [Test]
        public void IDEA0020_ThirtyWaitsForCampaignTutorialAndFirstTower()
        {
            object runtime = CreateRuntime();
            Queue(runtime, 30);
            Assert.That(Tick(runtime, 1f, true, true, true),
                Is.EqualTo("None"));
            AssertState(runtime, 30, "Queued", 0f);
            Assert.That(Tick(runtime, 1f, false, false, true),
                Is.EqualTo("None"));
            Assert.That(Tick(runtime, 1f, false, true, false),
                Is.EqualTo("None"));

            Assert.That(Tick(runtime, 1f, false, true, true),
                Is.EqualTo("WarningStarted"));
            AssertState(runtime, 30, "Warning", 60f);
            object beforePause = Capture(runtime);
            Assert.That(Tick(runtime, 0f, false, true, true),
                Is.EqualTo("None"));
            Assert.That(Capture(runtime), Is.SameAs(beforePause));
            Assert.That(Tick(runtime, 30f, false, true, true),
                Is.EqualTo("None"));
            AssertState(runtime, 30, "Warning", 30f);
            Assert.That(Tick(runtime, 30f, false, true, true),
                Is.EqualTo("StartEncounterRequested"));
            AssertState(runtime, 30, "Active", 0f);
        }

        [Test]
        public void IDEA0020_PressuresExecuteSeriallyByCompletedPredecessor()
        {
            object runtime = CreateRuntime();
            Queue(runtime, 30);
            Queue(runtime, 60);
            Queue(runtime, 90);
            Tick(runtime, 1f, false, true, true);
            Tick(runtime, 60f, false, true, true);
            Assert.That(Tick(runtime, 100f, false, true, true),
                Is.EqualTo("None"),
                "An active encounter exclusively owns pressure authority.");
            AssertState(runtime, 60, "Queued", 0f);

            Assert.That(Complete(
                runtime,
                "core.attention-encounter.directional-attack"),
                Is.EqualTo("EncounterCompleted"));
            Assert.That(Tick(runtime, 1f, false, true, true),
                Is.EqualTo("WarningStarted"));
            AssertState(runtime, 60, "Warning", 75f);
            Tick(runtime, 75f, false, true, true);
            Assert.That(Complete(
                runtime,
                "core.attention-encounter.high-risk-attack"),
                Is.EqualTo("EncounterCompleted"));
            Assert.That(Tick(runtime, 1f, false, true, true),
                Is.EqualTo("WarningStarted"));
            AssertState(runtime, 90, "Warning", 90f);
        }

        [Test]
        public void IDEA0020_TenWaveCampaignFreezesExistingWarning()
        {
            object runtime = CreateRuntime();
            Queue(runtime, 30);
            Tick(runtime, 1f, false, true, true);
            object warning = Capture(runtime);
            Assert.That(Tick(runtime, 50f, true, true, true),
                Is.EqualTo("None"));
            Assert.That(Capture(runtime), Is.SameAs(warning));
            AssertState(runtime, 30, "Warning", 60f);
        }

        [Test]
        public void IDEA0020_SnapshotDeepCopiesAndInvalidRestoreIsAtomic()
        {
            object source = CreateRuntime();
            Queue(source, 30);
            Tick(source, 1f, false, true, true);
            Tick(source, 20f, false, true, true);
            object snapshot = Capture(source);
            object target = CreateRuntime();
            Assert.That(Restore(target, snapshot), Is.True);
            AssertState(target, 30, "Warning", 40f);

            Type entryType = RequireType(EntryTypeName);
            Type stateType = RequireType(StateTypeName);
            Array entries = Array.CreateInstance(entryType, 2);
            object warningState = Enum.Parse(stateType, "Warning");
            entries.SetValue(Activator.CreateInstance(
                entryType, 30, warningState, 10f), 0);
            entries.SetValue(Activator.CreateInstance(
                entryType, 60, warningState, 20f), 1);
            object invalid = Activator.CreateInstance(
                RequireType(SnapshotTypeName), 7ul, entries);
            object before = Capture(target);
            Assert.That(Restore(target, invalid), Is.False,
                "Two simultaneous warning owners violate exclusivity.");
            Assert.That(Capture(target), Is.SameAs(before));
        }

        [Test]
        public void IDEA0020_StaticCaptureIsCachedForThreeHundredReads()
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

        private static bool Queue(object runtime, int threshold)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryQueueThreshold",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(string).MakeByRefType() },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { threshold, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            Assert.That(arguments[1], Is.TypeOf<string>());
            return result;
        }

        private static string Tick(
            object runtime,
            float delta,
            bool tenWaveActive,
            bool tutorialCompleted,
            bool firstTowerCompleted)
        {
            MethodInfo method = runtime.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.Instance)
                .Single(value => value.Name == "Tick");
            object[] arguments =
            {
                delta,
                tenWaveActive,
                tutorialCompleted,
                firstTowerCompleted,
                null,
                null,
            };
            Assert.That((bool)method.Invoke(runtime, arguments), Is.True,
                arguments[5] as string);
            return Read<object>(arguments[4], "Kind").ToString();
        }

        private static string Complete(object runtime, string encounterId)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryCompleteActive",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { encounterId, null, null };
            Assert.That((bool)method.Invoke(runtime, arguments), Is.True,
                arguments[2] as string);
            return Read<object>(arguments[1], "Kind").ToString();
        }

        private static object Capture(object runtime)
        {
            MethodInfo method = runtime.GetType().GetMethod("Capture");
            Assert.That(method, Is.Not.Null);
            return method.Invoke(runtime, null);
        }

        private static bool Restore(object runtime, object snapshot)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryRestore",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { snapshot, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            Assert.That(arguments[1], Is.TypeOf<string>());
            return result;
        }

        private static void AssertState(
            object runtime,
            int threshold,
            string state,
            float remaining)
        {
            object entry = Entries(Capture(runtime)).Single(value =>
                Read<int>(value, "Threshold") == threshold);
            Assert.That(Read<object>(entry, "State").ToString(),
                Is.EqualTo(state));
            Assert.That(Read<float>(entry, "WarningRemainingSeconds"),
                Is.EqualTo(remaining).Within(.001f));
        }

        private static int[] Thresholds(object snapshot) =>
            Entries(snapshot).Select(value => Read<int>(value, "Threshold"))
                .ToArray();

        private static string[] States(object snapshot) =>
            Entries(snapshot).Select(value =>
                    Read<object>(value, "State").ToString())
                .ToArray();

        private static object[] Entries(object snapshot)
        {
            return ((IEnumerable)Read<object>(snapshot, "Entries"))
                .Cast<object>().ToArray();
        }

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + "." + name);
            return (T)property.GetValue(owner);
        }

        private static Func<object, object> CompileCapture(Type runtimeType)
        {
            MethodInfo method = runtimeType.GetMethod("Capture");
            ParameterExpression instance = Expression.Parameter(
                typeof(object), "instance");
            MethodCallExpression call = Expression.Call(
                Expression.Convert(instance, runtimeType), method);
            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(call, typeof(object)), instance).Compile();
        }

        private static Type RequireType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
    }
}
