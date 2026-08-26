using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Persistence;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxFateOperationsPresentationTests
    {
        private const string ViewName =
            "WasteCity.Graybox3D.Building.GrayboxFateOperationsView3D";
        private const string ControllerName =
            "WasteCity.Graybox3D.Building.GrayboxFateOperationsController3D";

        [Test]
        public void IDEA0020_UnselectedFateDoesNotOpenOperations()
        {
            using (Fixture fixture = Create(new FormalFateRuntime()))
            {
                Assert.That(Refresh(fixture.Controller), Is.True);
                Assert.That(Read<bool>(fixture.View, "IsOpen"), Is.False);
                Assert.That(Read<bool>(fixture.View,
                    "RewindCommandsVisible"), Is.False);
            }
        }

        [Test]
        public void IDEA0020_PocketDetailsShowFlagshipsAndCollapseState()
        {
            FormalFateRuntime fate = Selected(FormalFateCatalog.PocketUniverseId);
            var pocket = new PocketUniverseFateEffect();
            string buildingId = FormalProductionDefinitionCatalog.All[0].BuildingId;
            const string stableId = "building.instance.000001";
            pocket.SelectFlagships(new[]
            {
                new PocketUniverseBuildingCandidate(
                    stableId, buildingId, true, true),
            });
            pocket.TryCreateCollapseCommand(stableId, 3, 4, out _);

            using (Fixture fixture = Create(fate, pocket: pocket))
            {
                Assert.That(Refresh(fixture.Controller), Is.True);
                StringAssert.Contains("袖珍宇宙",
                    Read<string>(fixture.View, "SelectedFateText"));
                StringAssert.Contains("Lv.1",
                    Read<string>(fixture.View, "SelectedFateText"));
                string[] flagships = Strings(fixture.View,
                    "PocketFlagshipTexts");
                Assert.That(flagships, Has.Length.EqualTo(1));
                StringAssert.Contains(stableId, flagships[0]);
                StringAssert.Contains("已坍缩",
                    Read<string>(fixture.View, "PocketCollapseStatusText"));
                Assert.That(Read<bool>(fixture.View,
                    "RewindCommandsVisible"), Is.False);
            }
        }

        [Test]
        public void IDEA0020_VoidDebtShowsResourcesTotalAndNextSettlement()
        {
            FormalFateRuntime fate = Selected(FormalFateCatalog.VoidDebtId);
            var debt = new FormalVoidDebtRuntime();
            Assert.That(debt.TryBorrowConstruction(
                ResourceIds.Iron, 12, out _), Is.True);
            debt.Tick(.1f, out _, out _);

            using (Fixture fixture = Create(fate, debt: debt))
            {
                Assert.That(Refresh(fixture.Controller), Is.True);
                StringAssert.Contains("虚空债",
                    Read<string>(fixture.View, "SelectedFateText"));
                string[] resources = Strings(fixture.View,
                    "VoidDebtResourceTexts");
                Assert.That(resources, Has.Length.EqualTo(1));
                Assert.That(resources[0], Does.Contain("铁矿").And.Contain("12"));
                StringAssert.Contains("12",
                    Read<string>(fixture.View, "VoidDebtTotalText"));
                StringAssert.Contains("秒",
                    Read<string>(fixture.View, "VoidDebtNextSettlementText"));
            }
        }

        [Test]
        public void IDEA0020_RewindCommandsAreExclusiveCachedAndReadNeedsConfirm()
        {
            FormalFateRuntime fate = Selected(FormalFateCatalog.RewindAnchorId);
            var rewind = new FormalRewindAnchorMetadataRuntime();
            var metadata = new FormalRewindAnchorMetadata(
                "rewind-anchor.slot.0001",
                ".internal-rewind-anchor/slot-01.json",
                "session.operations",
                "hash.operations",
                new FormalSaveCheckpointMetadata
                {
                    sequence = 4,
                    reasonId = "save-and-exit",
                    ruleTimeSeconds = 12f,
                    completedMilestoneIds = Array.Empty<string>(),
                },
                1);
            Assert.That(rewind.TryRestore(
                new FormalRewindAnchorMetadataSnapshot(
                    1, 2, new[] { metadata }), out _), Is.True);

            using (Fixture fixture = Create(fate, rewind: rewind))
            {
                Assert.That(Refresh(fixture.Controller), Is.True);
                int renderCount = Read<int>(fixture.View, "RenderCount");
                Assert.That(Refresh(fixture.Controller), Is.False,
                    "The same Fate/Pocket/Void/Rewind snapshots must be cached.");
                Assert.That(Read<int>(fixture.View, "RenderCount"),
                    Is.EqualTo(renderCount));
                Assert.That(Read<bool>(fixture.View,
                    "RewindCommandsVisible"), Is.True);
                Assert.That(Strings(fixture.View, "RewindAnchorSlotTexts"),
                    Has.Length.EqualTo(1));

                RequireEvent(fixture.View.GetType(),
                    "CreateRewindAnchorRequested");
                RequireEvent(fixture.View.GetType(),
                    "ReadRewindAnchorRequested");
                RequireEvent(fixture.View.GetType(),
                    "ClearRewindAnchorRequested");
                Assert.That(InvokeBool(fixture.Controller,
                    "TryRequestReadAnchor"), Is.True);
                Assert.That(Read<bool>(fixture.View,
                    "IsReadConfirmationOpen"), Is.True);
                Assert.That(InvokeBool(fixture.Controller,
                    "TryConfirmReadAnchor"), Is.True);
                Assert.That(Read<bool>(fixture.View,
                    "IsReadConfirmationOpen"), Is.False);
            }
        }

        private static Fixture Create(
            FormalFateRuntime fate,
            PocketUniverseFateEffect pocket = null,
            FormalVoidDebtRuntime debt = null,
            FormalRewindAnchorMetadataRuntime rewind = null)
        {
            Type viewType = RequireType(ViewName);
            Type controllerType = RequireType(ControllerName);
            var root = new GameObject("FateOperations.Test");
            object view = root.AddComponent(viewType);
            object controller = Activator.CreateInstance(controllerType,
                fate,
                pocket ?? new PocketUniverseFateEffect(),
                debt ?? new FormalVoidDebtRuntime(),
                rewind ?? new FormalRewindAnchorMetadataRuntime(),
                view);
            return new Fixture(root, view, controller);
        }

        private static FormalFateRuntime Selected(string id)
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(id, out _, out _, out _), Is.True);
            return fate;
        }

        private static bool Refresh(object owner) =>
            InvokeBool(owner, "RefreshIfChanged");

        private static bool InvokeBool(object owner, string name)
        {
            MethodInfo method = owner.GetType().GetMethod(
                name, BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, name);
            return (bool)method.Invoke(owner, null);
        }

        private static string[] Strings(object owner, string property)
        {
            object value = Read<object>(owner, property);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>()
                .Select(item => item?.ToString() ?? string.Empty).ToArray();
        }

        private static T Read<T>(object owner, string property)
        {
            PropertyInfo value = owner.GetType().GetProperty(
                property, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(value, Is.Not.Null, property);
            return (T)value.GetValue(owner);
        }

        private static void RequireEvent(Type owner, string name)
        {
            Assert.That(owner.GetEvent(
                name, BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, name);
        }

        private static Type RequireType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(value => value.GetType(name, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture(GameObject root, object view, object controller)
            {
                Root = root;
                View = view;
                Controller = controller;
            }
            public GameObject Root { get; }
            public object View { get; }
            public object Controller { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
