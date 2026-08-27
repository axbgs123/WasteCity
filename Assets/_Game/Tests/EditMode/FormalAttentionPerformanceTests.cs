using System;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class FormalAttentionPerformanceTests
    {
        [Test]
        public void StaticAttentionPressureControllerAndPresenterStayZeroAlloc()
        {
            var root = new GameObject("Attention.Performance.View");
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            var defense = new GrayboxDefenseRuntime3D(0f, 0f, 20f, 0f);
            var defenseController =
                new GrayboxAttentionPressureDefenseController3D(
                    pressure, defense);
            var runtimeController =
                new GrayboxAttentionPressureRuntimeController3D(
                    attention, pressure, defenseController);
            try
            {
                var view = root.AddComponent<GrayboxProgressionHudView3D>();
                var presenter =
                    new GrayboxAttentionPressurePresentationController3D(
                        pressure, view);
                runtimeController.Bind();
                Assert.That(presenter.RefreshIfChanged(), Is.True);
                Assert.That(runtimeController.Tick(
                    1f, true, true, true, out string error), Is.True, error);
                Assert.That(presenter.RefreshIfChanged(), Is.False);
                FormalAttentionSnapshot attentionStable = attention.Capture();
                AttentionPressureSnapshot pressureStable = pressure.Capture();

                bool stable = true;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (var index = 0; index < 300; index++)
                {
                    stable &= runtimeController.Tick(
                        1f, true, true, true, out _);
                    stable &= !presenter.RefreshIfChanged();
                    stable &= ReferenceEquals(
                        attentionStable, attention.Capture());
                    stable &= ReferenceEquals(
                        pressureStable, pressure.Capture());
                }
                long allocated =
                    GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(stable, Is.True);
                Assert.That(allocated, Is.Zero);
            }
            finally
            {
                runtimeController.Dispose();
                defenseController.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PressureChunksAndSaveBreakpointReachSameStableResult()
        {
            var seed = new AttentionPressureRuntime();
            Assert.That(seed.TryQueueThreshold(30, out string error),
                Is.True, error);
            Assert.That(seed.Tick(
                .1f, false, true, true,
                out AttentionPressureCommand warning,
                out error), Is.True, error);
            Assert.That(warning.Kind,
                Is.EqualTo(AttentionPressureCommandKind.WarningStarted));
            AttentionPressureSnapshot start = seed.Capture();

            var whole = RestorePressure(start);
            Assert.That(whole.Tick(
                60f, false, true, true,
                out AttentionPressureCommand wholeCommand,
                out error), Is.True, error);

            var split = RestorePressure(start);
            Assert.That(split.Tick(
                20f, false, true, true, out _, out error), Is.True, error);
            AttentionPressureSnapshot checkpoint = split.Capture();
            var resumed = RestorePressure(checkpoint);
            Assert.That(resumed.Tick(
                40f, false, true, true,
                out AttentionPressureCommand resumedCommand,
                out error), Is.True, error);

            Assert.That(resumedCommand.Kind, Is.EqualTo(wholeCommand.Kind));
            Assert.That(resumedCommand.EncounterId,
                Is.EqualTo(wholeCommand.EncounterId));
            AssertPressureEquivalent(whole.Capture(), resumed.Capture());

            var attention = new FormalAttentionRuntime();
            Assert.That(attention.TryApply(
                "core.attention.civilization.advanced",
                "performance.same-event",
                out error), Is.True, error);
            var restoredAttention = new FormalAttentionRuntime();
            Assert.That(restoredAttention.TryRestore(
                attention.Capture(), out error), Is.True, error);
            FormalAttentionSnapshot beforeDuplicate =
                restoredAttention.Capture();
            Assert.That(restoredAttention.TryApply(
                "core.attention.civilization.advanced",
                "performance.same-event",
                out _), Is.False);
            Assert.That(restoredAttention.Capture(),
                Is.SameAs(beforeDuplicate));
        }

        [Test]
        public void LongWarningPublishesAndFormatsAtMostTenTimesPerSecond()
        {
            var root = new GameObject("Pressure.Warning.Performance.View");
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            var defense = new GrayboxDefenseRuntime3D(0f, 0f, 20f, 0f);
            var defenseController =
                new GrayboxAttentionPressureDefenseController3D(
                    pressure, defense);
            var runtimeController =
                new GrayboxAttentionPressureRuntimeController3D(
                    attention, pressure, defenseController);
            try
            {
                var view = root.AddComponent<GrayboxProgressionHudView3D>();
                var presenter =
                    new GrayboxAttentionPressurePresentationController3D(
                        pressure, view);
                runtimeController.Bind();
                Assert.That(pressure.TryQueueThreshold(
                    30, out string error), Is.True, error);
                Assert.That(runtimeController.Tick(
                    .001f, false, true, true, out error), Is.True, error);
                Assert.That(presenter.RefreshIfChanged(), Is.True);

                var refreshes = 0;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (var frame = 0; frame < 300; frame++)
                {
                    Assert.That(runtimeController.Tick(
                        1f / 60f, false, true, true, out error),
                        Is.True, error);
                    if (presenter.RefreshIfChanged()) refreshes++;
                }
                long allocated =
                    GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(refreshes, Is.LessThanOrEqualTo(50));
                Assert.That(allocated, Is.LessThanOrEqualTo(65536L));
                Assert.That(pressure.Capture().Entries[0]
                    .WarningRemainingSeconds,
                    Is.EqualTo(55f).Within(.11f));
            }
            finally
            {
                runtimeController.Dispose();
                defenseController.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AttentionPressureRuntime RestorePressure(
            AttentionPressureSnapshot snapshot)
        {
            var runtime = new AttentionPressureRuntime();
            Assert.That(runtime.TryRestore(snapshot, out string error),
                Is.True, error);
            return runtime;
        }

        private static void AssertPressureEquivalent(
            AttentionPressureSnapshot expected,
            AttentionPressureSnapshot actual)
        {
            Assert.That(actual.Entries, Has.Count.EqualTo(expected.Entries.Count));
            for (var index = 0; index < expected.Entries.Count; index++)
            {
                Assert.That(actual.Entries[index].Threshold,
                    Is.EqualTo(expected.Entries[index].Threshold));
                Assert.That(actual.Entries[index].State,
                    Is.EqualTo(expected.Entries[index].State));
                Assert.That(actual.Entries[index].WarningRemainingSeconds,
                    Is.EqualTo(expected.Entries[index].WarningRemainingSeconds)
                        .Within(.0001f));
            }
        }
    }
}
