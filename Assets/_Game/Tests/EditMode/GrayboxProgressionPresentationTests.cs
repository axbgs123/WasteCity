using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxProgressionPresentationTests
    {
        private const string ViewTypeName =
            "WasteCity.Graybox3D.Building.GrayboxProgressionHudView3D";
        private const string ControllerTypeName =
            "WasteCity.Graybox3D.Building.GrayboxProgressionHudController3D";

        [Test]
        public void IDEA0020_HudShowsValueFourChineseStagesAndNextThreshold()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            using (PresentationFixture fixture = Create(attention, fate))
            {
                Assert.That(Refresh(fixture.Controller), Is.True);
                Assert.That(Read<string>(fixture.View, "AttentionValueText"),
                    Is.EqualTo("关注度 10/100"));
                Assert.That(Read<string>(fixture.View, "AttentionStageText"),
                    Is.EqualTo("未锁定"));
                StringAssert.Contains("30", Read<string>(
                    fixture.View,
                    "NextThresholdText"));
                StringAssert.Contains("20", Read<string>(
                    fixture.View,
                    "NextThresholdText"));

                ApplyDebt(attention, 20, "stage.echo");
                Assert.That(Refresh(fixture.Controller), Is.True);
                Assert.That(Read<string>(fixture.View, "AttentionStageText"),
                    Is.EqualTo("异常回波"));
                StringAssert.Contains("60", Read<string>(
                    fixture.View,
                    "NextThresholdText"));

                ApplyDebt(attention, 30, "stage.observed");
                Assert.That(Refresh(fixture.Controller), Is.True);
                Assert.That(Read<string>(fixture.View, "AttentionStageText"),
                    Is.EqualTo("定向观测"));
                StringAssert.Contains("90", Read<string>(
                    fixture.View,
                    "NextThresholdText"));

                ApplyDebt(attention, 30, "stage.locked");
                Assert.That(Refresh(fixture.Controller), Is.True);
                Assert.That(Read<string>(fixture.View, "AttentionStageText"),
                    Is.EqualTo("坐标锁定"));
                Assert.That(Read<string>(fixture.View, "AttentionValueText"),
                    Is.EqualTo("关注度 90/100"));
            }
        }

        [Test]
        public void IDEA0020_HudProjectsOnlyLatestThreeReasonsAsChineseText()
        {
            var attention = new FormalAttentionRuntime();
            Assert.That(attention.TryApply(
                "core.attention.fate.first-activation",
                "presentation.fate",
                out _), Is.True);
            Assert.That(attention.TryApply(
                "core.attention.city.first-deployment",
                "presentation.deployment",
                out _), Is.True);
            Assert.That(attention.TryApply(
                "core.attention.building.first-mining-station",
                "presentation.mining",
                out _), Is.True);
            Assert.That(attention.TryApply(
                "core.attention.building.first-smelter",
                "presentation.smelter",
                out _), Is.True);

            using (PresentationFixture fixture = Create(
                       attention,
                       new FormalFateRuntime()))
            {
                Assert.That(Refresh(fixture.Controller), Is.True);
                string[] reasons = StringSequence(
                    fixture.View,
                    "RecentReasonTexts");
                Assert.That(reasons, Has.Length.EqualTo(3));
                Assert.That(reasons.All(value =>
                    Regex.IsMatch(value, "[\\u4e00-\\u9fff]")), Is.True);
                Assert.That(reasons.All(value =>
                    !value.Contains("core.attention")), Is.True);
                StringAssert.Contains("+5", reasons[0]);
                StringAssert.Contains("+2", reasons[1]);
                StringAssert.Contains("+3", reasons[2]);
            }
        }

        [Test]
        public void IDEA0020_UnchangedSnapshotDoesNotRefreshAndUnreadyFateStaysClosed()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            using (PresentationFixture fixture = Create(attention, fate))
            {
                Assert.That(Read<bool>(fixture.Controller, "EffectsReady"),
                    Is.False);
                Assert.That(Refresh(fixture.Controller), Is.True);
                int rendered = Read<int>(fixture.View, "RenderCount");
                Assert.That(Refresh(fixture.Controller), Is.False);
                Assert.That(Read<int>(fixture.View, "RenderCount"),
                    Is.EqualTo(rendered));

                Assert.That(StringSequence(fixture.View, "PreparedFateIds"),
                    Is.EqualTo(new[]
                    {
                        "core.legacy.pocket-universe",
                        "core.legacy.void-debt",
                        "core.legacy.rewind-anchor",
                    }));
                Assert.That(Read<bool>(fixture.View, "IsFateSelectionOpen"),
                    Is.False,
                    "EffectsReady=false may prepare cards but cannot force " +
                    "the formal selection modal open.");
            }
        }

        [Test]
        public void IDEA0020_StableHudRefreshAllocatesZeroBytesAcrossThreeHundredCalls()
        {
            var root = new GameObject("ProgressionPresentation.Performance");
            try
            {
                var view = root.AddComponent<GrayboxProgressionHudView3D>();
                var controller = new GrayboxProgressionHudController3D(
                    new FormalAttentionRuntime(),
                    new FormalFateRuntime(),
                    view);
                Assert.That(controller.RefreshIfChanged(), Is.True);
                for (var index = 0; index < 8; index++)
                    controller.RefreshIfChanged();

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (var index = 0; index < 300; index++)
                    controller.RefreshIfChanged();
                long allocated =
                    GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(allocated, Is.Zero);
                Assert.That(view.RenderCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static PresentationFixture Create(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            Type viewType = RequireType(ViewTypeName);
            Type controllerType = RequireType(ControllerTypeName);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(viewType),
                Is.True);
            var root = new GameObject("ProgressionPresentation.Test");
            object view = root.AddComponent(viewType);
            ConstructorInfo constructor = controllerType.GetConstructor(new[]
            {
                typeof(FormalAttentionRuntime),
                typeof(FormalFateRuntime),
                viewType,
            });
            Assert.That(constructor, Is.Not.Null);
            object controller = constructor.Invoke(new[]
            {
                attention,
                fate,
                view,
            });
            return new PresentationFixture(root, view, controller);
        }

        private static bool Refresh(object controller)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "RefreshIfChanged",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(controller, null);
        }

        private static void ApplyDebt(
            FormalAttentionRuntime attention,
            int count,
            string prefix)
        {
            for (var index = 0; index < count; index++)
            {
                Assert.That(attention.TryApply(
                    "core.attention.fate.void-debt-periodic",
                    prefix + "." + index,
                    out string error), Is.True, error);
            }
        }

        private static Type RequireType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(value => value != null);
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

        private static string[] StringSequence(
            object owner,
            string propertyName)
        {
            object value = Read<object>(owner, propertyName);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>()
                .Select(item => item?.ToString() ?? string.Empty)
                .ToArray();
        }

        private sealed class PresentationFixture : IDisposable
        {
            public PresentationFixture(
                GameObject root,
                object view,
                object controller)
            {
                Root = root;
                View = view;
                Controller = controller;
            }

            public GameObject Root { get; }
            public object View { get; }
            public object Controller { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}
