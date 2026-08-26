using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxFateSelectionPresentationTests
    {
        private const string ViewTypeName =
            "WasteCity.Graybox3D.Building.GrayboxFateSelectionView3D";
        private const string ControllerTypeName =
            "WasteCity.Graybox3D.Building.GrayboxFateSelectionController3D";
        private const string CardTypeName =
            "WasteCity.Graybox3D.Building.GrayboxFateSelectionCard3D";

        [Test]
        public void IDEA0020_ThreeCardsExposeAllApprovedChineseCopy()
        {
            using (Fixture fixture = Create(effectsReady: true))
            {
                Assert.That(Refresh(fixture.Controller), Is.True);
                object[] cards = Sequence(fixture.View, "Cards");
                Assert.That(cards, Has.Length.EqualTo(3));
                Assert.That(cards.Select(value => Read<string>(value, "FateId")),
                    Is.EqualTo(FormalFateCatalog.All.Select(
                        value => value.Id.Value)));
                for (var index = 0; index < cards.Length; index++)
                {
                    FormalFateDefinition expected = FormalFateCatalog.All[index];
                    object actual = cards[index];
                    Assert.That(actual.GetType(), Is.EqualTo(
                        RequireType(CardTypeName)));
                    Assert.That(Read<string>(actual, "DisplayName"),
                        Is.EqualTo(expected.DisplayName));
                    Assert.That(Read<string>(actual, "Brief"),
                        Is.EqualTo(expected.Brief));
                    Assert.That(Read<string>(actual, "LevelOneSummary"),
                        Is.EqualTo(expected.LevelOneSummary));
                    Assert.That(Read<string>(actual, "LevelTwoSummary"),
                        Is.EqualTo(expected.LevelTwoSummary));
                    Assert.That(Read<string>(actual, "CostSummary"),
                        Is.EqualTo(expected.CostSummary));
                }
                Assert.That(Read<bool>(fixture.View, "IsOpen"), Is.True,
                    "Pending fate plus EffectsReady must force the modal open.");
            }
        }

        [Test]
        public void IDEA0020_SelectingCardRequiresSecondConfirmation()
        {
            using (Fixture fixture = Create(effectsReady: true))
            {
                Refresh(fixture.Controller);
                Assert.That(SelectCard(
                    fixture.Controller,
                    FormalFateCatalog.RewindAnchorId,
                    out string error), Is.True, error);
                Assert.That(fixture.Fate.Capture().HasSelection, Is.False,
                    "Card click is preview only and cannot commit fate.");
                Assert.That(Read<string>(fixture.View, "PendingFateId"),
                    Is.EqualTo(FormalFateCatalog.RewindAnchorId));
                Assert.That(Read<bool>(fixture.View, "IsConfirmationOpen"),
                    Is.True);

                CancelConfirmation(fixture.Controller);
                Assert.That(Read<bool>(fixture.View, "IsConfirmationOpen"),
                    Is.False);
                Assert.That(Read<bool>(fixture.View, "IsOpen"), Is.True);
                Assert.That(fixture.Fate.Capture().HasSelection, Is.False);
            }
        }

        [Test]
        public void IDEA0020_ConfirmationCommitsAndRefreshesExclusiveStatus()
        {
            using (Fixture fixture = Create(effectsReady: true))
            {
                Refresh(fixture.Controller);
                Assert.That(SelectCard(
                    fixture.Controller,
                    FormalFateCatalog.RewindAnchorId,
                    out _), Is.True);
                Assert.That(Confirm(
                    fixture.Controller,
                    out string error), Is.True, error);
                Assert.That(fixture.Fate.Capture().SelectedId,
                    Is.EqualTo(FormalFateCatalog.RewindAnchorId));
                Assert.That(fixture.Fate.Capture().Level, Is.EqualTo(1));
                Assert.That(fixture.Attention.Value, Is.EqualTo(15));
                Assert.That(Read<bool>(fixture.View, "IsOpen"), Is.False);
                Assert.That(Read<bool>(fixture.View, "IsConfirmationOpen"),
                    Is.False);
                Assert.That(Read<string>(fixture.View, "SelectedStatusText"),
                    Does.Contain("回溯锚点").And.Contain("Lv.1"));
            }
        }

        private static Fixture Create(bool effectsReady)
        {
            Type viewType = RequireType(ViewTypeName);
            Type controllerType = RequireType(ControllerTypeName);
            var root = new GameObject("FateSelection.Presentation.Test");
            object view = root.AddComponent(viewType);
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            var router = new GrayboxProgressionEventRouter3D(attention, fate);
            ConstructorInfo constructor = controllerType.GetConstructor(new[]
            {
                typeof(FormalFateRuntime),
                typeof(GrayboxProgressionEventRouter3D),
                viewType,
                typeof(Func<bool>),
            });
            Assert.That(constructor, Is.Not.Null);
            object controller = constructor.Invoke(new object[]
            {
                fate,
                router,
                view,
                new Func<bool>(() => effectsReady),
            });
            return new Fixture(root, view, controller, attention, fate, router);
        }

        private static bool Refresh(object controller) =>
            InvokeBool(controller, "RefreshIfChanged", Array.Empty<object>());

        private static bool SelectCard(
            object controller,
            string fateId,
            out string error)
        {
            object[] arguments = { fateId, null };
            bool result = InvokeBool(controller, "TrySelectCard", arguments);
            error = arguments[1] as string;
            return result;
        }

        private static bool Confirm(object controller, out string error)
        {
            object[] arguments = { null };
            bool result = InvokeBool(
                controller,
                "TryConfirmSelection",
                arguments);
            error = arguments[0] as string;
            return result;
        }

        private static void CancelConfirmation(object controller)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "CancelConfirmation",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private static bool InvokeBool(
            object owner,
            string methodName,
            object[] arguments)
        {
            MethodInfo method = owner.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(value =>
                    value.Name == methodName &&
                    value.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, methodName);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            return (bool)method.Invoke(owner, arguments);
        }

        private static object[] Sequence(object owner, string propertyName)
        {
            object value = Read<object>(owner, propertyName);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>().ToArray();
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

        private sealed class Fixture : IDisposable
        {
            public Fixture(
                GameObject root,
                object view,
                object controller,
                FormalAttentionRuntime attention,
                FormalFateRuntime fate,
                GrayboxProgressionEventRouter3D router)
            {
                Root = root;
                View = view;
                Controller = controller;
                Attention = attention;
                Fate = fate;
                Router = router;
            }

            public GameObject Root { get; }
            public object View { get; }
            public object Controller { get; }
            public FormalAttentionRuntime Attention { get; }
            public FormalFateRuntime Fate { get; }
            public GrayboxProgressionEventRouter3D Router { get; }

            public void Dispose()
            {
                Router.Dispose();
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}
