using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxAttentionPressurePresentationTests
    {
        private const string PresenterName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxAttentionPressurePresentationController3D";

        [Test]
        public void IDEA0020_ProjectionShowsAllPressureStatesBossAndCachesSnapshot()
        {
            Type presenterType = typeof(GrayboxProgressionHudView3D).Assembly
                .GetType(PresenterName, false);
            Assert.That(presenterType, Is.Not.Null, PresenterName);
            var root = new GameObject("Pressure.Presentation.Test");
            try
            {
                GrayboxProgressionHudView3D view =
                    root.AddComponent<GrayboxProgressionHudView3D>();
                var runtime = new AttentionPressureRuntime();
                object presenter = Activator.CreateInstance(
                    presenterType, runtime, view);
                Assert.That(Refresh(presenter), Is.True);
                Assert.That(Read<string>(view, "PressureStatusText"),
                    Does.Contain("暂无"));

                string error;
                Assert.That(runtime.TryRestore(new AttentionPressureSnapshot(
                    4UL,
                    new[]
                    {
                        new AttentionPressureEntrySnapshot(
                            30, AttentionPressureState.Queued, 0f),
                    }), out error), Is.True, error);
                Assert.That(Refresh(presenter), Is.True);
                Assert.That(Read<string>(view, "PressureStatusText"),
                    Does.Contain("排队"));

                Assert.That(runtime.TryRestore(new AttentionPressureSnapshot(
                    5UL,
                    new[]
                    {
                        new AttentionPressureEntrySnapshot(
                            30, AttentionPressureState.Completed, 0f),
                        new AttentionPressureEntrySnapshot(
                            60, AttentionPressureState.Warning, 42f),
                    }), out error), Is.True, error);
                Assert.That(Refresh(presenter), Is.True);
                string waiting = Read<string>(view, "PressureStatusText");
                Assert.That(waiting,
                    Does.Contain("预警").And.Contain("42"));

                Assert.That(runtime.TryRestore(new AttentionPressureSnapshot(
                    6UL,
                    new[]
                    {
                        new AttentionPressureEntrySnapshot(
                            30, AttentionPressureState.Completed, 0f),
                        new AttentionPressureEntrySnapshot(
                            60, AttentionPressureState.Completed, 0f),
                        new AttentionPressureEntrySnapshot(
                            90, AttentionPressureState.Active, 0f),
                    }), out error), Is.True, error);
                Assert.That(Refresh(presenter), Is.True);
                string status = Read<string>(view, "PressureStatusText");
                Assert.That(status,
                    Does.Contain("已完成").And.Contain("进行中"));
                Assert.That(Read<string>(view, "BossStatusText"),
                    Does.Contain("晶壳母体"));
                Assert.That(Read<string>(view, "BossPhaseText"),
                    Does.Contain("阶段"));
                int renders = Read<int>(view, "PressureRenderCount");
                Assert.That(Refresh(presenter), Is.False);
                Assert.That(Read<int>(view, "PressureRenderCount"),
                    Is.EqualTo(renders));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool Refresh(object owner) =>
            (bool)owner.GetType().GetMethod("RefreshIfChanged")
                .Invoke(owner, null);

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(owner);
        }
    }
}
