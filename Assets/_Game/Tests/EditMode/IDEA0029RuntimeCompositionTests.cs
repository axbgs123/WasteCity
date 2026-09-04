using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Exploration;
using WasteCity.Leader.Exploration;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029RuntimeCompositionTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void SameSessionAndFactsProduceSameCompositeSignature()
        {
            GrayboxExplorationController3D left = Create("Left");
            GrayboxExplorationController3D right = Create("Right");
            left.Initialize(64, 48, "same-session", (_, __) => true);
            right.Initialize(64, 48, "same-session", (_, __) => true);

            ApplyFacts(left);
            ApplyFacts(right);

            GrayboxExplorationCapture3D a = left.Capture();
            GrayboxExplorationCapture3D b = right.Capture();
            Assert.That(a.LeaderControlMode, Is.EqualTo(b.LeaderControlMode));
            Assert.That(a.Exploration.ExploredCells,
                Is.EqualTo(b.Exploration.ExploredCells));
            Assert.That(a.Exploration.ScanRecords,
                Has.Length.EqualTo(b.Exploration.ScanRecords.Length));
            Assert.That(a.OutpostAlerts, Is.EqualTo(b.OutpostAlerts));
            Assert.That(a.CenJinDistress.State,
                Is.EqualTo(b.CenJinDistress.State));
        }

        [Test]
        public void ResetKeepsExactlyOneOwnerPerExplorationDomain()
        {
            GrayboxExplorationController3D controller = Create("Reset");
            controller.Initialize(64, 48, "first", (_, __) => true);
            WorldExplorationRuntime before = controller.Exploration;

            Assert.That(controller.TryResetSession(
                    64,
                    48,
                    "second",
                    (_, __) => true,
                    out string error),
                Is.True,
                error);
            Assert.That(controller.Exploration, Is.Not.SameAs(before));
            Assert.That(controller.Exploration, Is.Not.Null);
            Assert.That(controller.LeaderControl, Is.Not.Null);
            Assert.That(controller.ManualGather, Is.Not.Null);
            Assert.That(controller.CenJinDistress, Is.Not.Null);
            Assert.That(controller.OutpostAlerts, Is.Not.Null);
            Assert.That(controller.GetComponents<
                GrayboxExplorationController3D>(), Has.Length.EqualTo(1));
        }

        private static void ApplyFacts(
            GrayboxExplorationController3D controller)
        {
            controller.TrySyncVisionSource(
                new WorldVisionSource(
                    "core.city.primary",
                    WorldVisionSourceKind.PrimaryCity,
                    20,
                    20,
                    true,
                    1ul),
                out _);
            controller.TryRequestLeaderControl(
                LeaderControlMode.Manual,
                out _);
            controller.OutpostAlerts.TryReport(
                "alert.same.1",
                "core.settlement.outpost",
                40,
                20,
                OutpostAlertSeverity.Guard,
                "游荡敌群",
                25,
                120f,
                4d,
                out _);
        }

        private GrayboxExplorationController3D Create(string name)
        {
            var root = new GameObject(name);
            cleanup.Add(root);
            return root.AddComponent<GrayboxExplorationController3D>();
        }
    }
}
