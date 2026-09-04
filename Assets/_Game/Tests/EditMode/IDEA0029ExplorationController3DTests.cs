using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Exploration;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Leader.Exploration;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029ExplorationController3DTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void IDEA0029_InitializeOwnsOneRuntimePerApprovedDomain()
        {
            GrayboxExplorationController3D controller = CreateController();
            controller.Initialize(64, 48, "session-controller", (_, __) => true);

            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(controller.Exploration, Is.Not.Null);
            Assert.That(controller.LeaderControl, Is.Not.Null);
            Assert.That(controller.ManualGather, Is.Not.Null);
            Assert.That(controller.CenJinDistress, Is.Not.Null);
            Assert.That(controller.OutpostAlerts, Is.Not.Null);
            Assert.That(controller.Exploration.Width, Is.EqualTo(64));
            Assert.That(controller.Exploration.Height, Is.EqualTo(48));
            Assert.That(controller.CenJinDistress.SessionId,
                Is.EqualTo("session-controller"));
        }

        [Test]
        public void IDEA0029_InvalidReinitializeDoesNotReplaceOwnedState()
        {
            GrayboxExplorationController3D controller = CreateController();
            controller.Initialize(32, 24, "session-a", (_, __) => true);
            WorldExplorationRuntime exploration = controller.Exploration;
            Assert.That(controller.TrySyncVisionSource(
                    new WorldVisionSource(
                        "city.primary",
                        WorldVisionSourceKind.PrimaryCity,
                        10,
                        10,
                        true,
                        1),
                    out string syncError),
                Is.True,
                syncError);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                controller.Initialize(0, 24, "bad", (_, __) => true));
            Assert.That(controller.Exploration, Is.SameAs(exploration));
            Assert.That(controller.Exploration.SourceCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() =>
                controller.Initialize(32, 24, "session-b", (_, __) => true));
            Assert.That(controller.Exploration, Is.SameAs(exploration));
        }

        [Test]
        public void IDEA0029_VisionSyncIsExplicitIdempotentAndRejectsOutOfBounds()
        {
            GrayboxExplorationController3D controller = InitializedController();
            var source = new WorldVisionSource(
                "city.primary",
                WorldVisionSourceKind.PrimaryCity,
                16,
                15,
                true,
                1);

            Assert.That(controller.TrySyncVisionSource(
                    source,
                    out string error),
                Is.True,
                error);
            Assert.That(controller.Exploration.IsVisible(16, 15), Is.True);
            ulong revision = controller.Exploration.Revision;
            Assert.That(controller.TrySyncVisionSource(source, out error),
                Is.False);
            Assert.That(error, Is.Empty);
            Assert.That(controller.Exploration.Revision, Is.EqualTo(revision));

            Assert.That(controller.TrySyncVisionSource(
                    new WorldVisionSource(
                        "leader.invalid",
                        WorldVisionSourceKind.Leader,
                        -1,
                        15,
                        true),
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("outside"));
            Assert.That(controller.Exploration.SourceCount, Is.EqualTo(1));
            Assert.That(controller.Exploration.Revision, Is.EqualTo(revision));

            Assert.That(controller.TryRemoveVisionSource(
                    source.StableId,
                    out error),
                Is.True,
                error);
            Assert.That(controller.Exploration.GetState(16, 15),
                Is.EqualTo(WorldVisibilityState.Explored));
        }

        [Test]
        public void IDEA0029_VisibleResourceObservationUsesOwnedScanAndAttentionOnce()
        {
            var attentionCalls = new List<string>();
            GrayboxExplorationController3D controller = CreateController();
            controller.Initialize(
                64,
                48,
                "session-scan",
                (reason, key) =>
                {
                    attentionCalls.Add(reason + "|" + key);
                    return true;
                });
            controller.TrySyncVisionSource(
                new WorldVisionSource(
                    "city.primary",
                    WorldVisionSourceKind.PrimaryCity,
                    16,
                    15,
                    true,
                    1),
                out _);
            var observation = new WorldIntelObservation(
                "world.deposit.safe-iron.01",
                WorldIntelKind.Resource,
                16,
                15,
                "铁矿 240",
                true,
                240,
                5f,
                1);

            Assert.That(controller.TryObserveVisibleResource(
                    observation,
                    out WorldScanResult first,
                    out string error),
                Is.True,
                error);
            Assert.That(first.Status, Is.EqualTo(WorldScanStatus.Committed));
            Assert.That(controller.TryObserveVisibleResource(
                    observation,
                    out WorldScanResult duplicate,
                    out error),
                Is.True,
                error);
            Assert.That(duplicate.Status,
                Is.EqualTo(WorldScanStatus.AlreadyScanned));
            Assert.That(attentionCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public void IDEA0029_CoordinateObservationResolvesFormalIdentityOrSafeFallback()
        {
            GrayboxExplorationController3D controller = InitializedController();
            controller.TrySyncVisionSource(
                new WorldVisionSource(
                    "city.primary",
                    WorldVisionSourceKind.PrimaryCity,
                    16,
                    15,
                    true,
                    1),
                out _);

            Assert.That(controller.TryObserveVisibleResource(
                    16,
                    15,
                    ResourceIds.Iron,
                    240,
                    4f,
                    1,
                    out WorldScanResult formalScan,
                    out string formalId,
                    out string error),
                Is.True,
                error);
            Assert.That(formalId,
                Is.EqualTo("world.deposit.safe-iron.01"));
            Assert.That(formalScan.Status,
                Is.EqualTo(WorldScanStatus.Committed));
            Assert.That(FormalWorldGenerationCatalog3D.FindResourceNode(
                    16,
                    15).Value.StableId,
                Is.EqualTo(formalId));

            controller.TrySyncVisionSource(
                new WorldVisionSource(
                    "leader.fallback",
                    WorldVisionSourceKind.Leader,
                    5,
                    5,
                    true,
                    1),
                out _);
            Assert.That(controller.TryObserveVisibleResource(
                    5,
                    5,
                    ResourceIds.Stone,
                    20,
                    5f,
                    1,
                    out WorldScanResult fallbackScan,
                    out string fallbackId,
                    out error),
                Is.True,
                error);
            Assert.That(fallbackId,
                Is.EqualTo(GrayboxResourceNodeIdentity3D.Create(5, 5)));
            Assert.That(fallbackScan.Status,
                Is.EqualTo(WorldScanStatus.None));
            Assert.That(controller.Exploration.TryGetIntel(
                    fallbackId,
                    5f,
                    out WorldIntelSnapshot fallbackIntel),
                Is.True);
            Assert.That(fallbackIntel.MutableValue, Is.EqualTo(20));
        }

        [Test]
        public void IDEA0029_FormalCoordinateMismatchDoesNotCommitIntelOrScan()
        {
            GrayboxExplorationController3D controller = InitializedController();
            controller.TrySyncVisionSource(
                new WorldVisionSource(
                    "city.primary",
                    WorldVisionSourceKind.PrimaryCity,
                    16,
                    15,
                    true,
                    1),
                out _);
            ulong revision = controller.Exploration.Revision;

            Assert.That(controller.TryObserveVisibleResource(
                    16,
                    15,
                    ResourceIds.Stone,
                    240,
                    4f,
                    1,
                    out _,
                    out string stableId,
                    out string error),
                Is.False);
            Assert.That(stableId, Is.Empty);
            Assert.That(error, Does.Contain("does not match"));
            Assert.That(controller.Exploration.IntelCount, Is.Zero);
            Assert.That(controller.Exploration.ScannedZoneCount, Is.Zero);
            Assert.That(controller.Exploration.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void IDEA0029_OwnedExplorationFeedsFogWithoutStateCopy()
        {
            GrayboxExplorationController3D controller = InitializedController();
            GrayboxFogPresenter3D fog = CreateFogPresenter(64, 48);
            controller.TrySyncVisionSource(
                new WorldVisionSource(
                    "city.primary",
                    WorldVisionSourceKind.PrimaryCity,
                    10,
                    9,
                    true,
                    1),
                out _);

            Assert.That(fog.ApplyVisibility(controller.Exploration), Is.True);
            Assert.That(fog.GetPresentedState(10, 9),
                Is.EqualTo(WorldVisibilityState.Visible));
            Assert.That(fog.GetPresentedState(63, 47),
                Is.EqualTo(WorldVisibilityState.Hidden));
            int applyCount = fog.MaskApplyCount;
            ulong visibilityRevision =
                controller.Exploration.VisibilityRevision;
            ulong explorationRevision = controller.Exploration.Revision;
            Assert.That(controller.TryObserveVisibleResource(
                    10,
                    9,
                    ResourceIds.Iron,
                    12,
                    3f,
                    1,
                    out _,
                    out _,
                    out string error),
                Is.True,
                error);
            Assert.That(controller.Exploration.Revision,
                Is.GreaterThan(explorationRevision));
            Assert.That(controller.Exploration.VisibilityRevision,
                Is.EqualTo(visibilityRevision));
            Assert.That(fog.ApplyVisibility(controller.Exploration), Is.False);
            Assert.That(fog.MaskApplyCount, Is.EqualTo(applyCount));
        }

        [Test]
        public void IDEA0029_InjectedTickAdvancesGatherAndDistressWithoutPollingWorld()
        {
            GrayboxExplorationController3D controller = InitializedController();
            int gathered = 0;
            int reserveCalls = 0;
            int commitCalls = 0;
            var gatherContext = new ManualGatherContext(
                true,
                CharacterLifeState.Active,
                LeaderControlMode.Manual,
                false,
                true,
                "world.deposit.safe-iron.01",
                ResourceIds.Iron,
                20,
                1f);
            var distressContext = new CenJinDistressContext3D(
                true,
                2f,
                true);
            controller.ConfigureBoundaries(
                () => gatherContext,
                _ =>
                {
                    gathered++;
                    return WorldHarvestTransactionResult.Completed(
                        ResourceIds.Iron);
                },
                () => distressContext,
                (int amount, out string error) =>
                {
                    reserveCalls++;
                    error = string.Empty;
                    return amount == LeaderInteractionCatalog.CenJinBiomassCost;
                },
                (int amount, out string error) =>
                {
                    error = string.Empty;
                    return true;
                },
                (CenJinRescueCommitRequest request, out string error) =>
                {
                    commitCalls++;
                    error = string.Empty;
                    return request.ReservedBiomass ==
                        LeaderInteractionCatalog.CenJinBiomassCost;
                });

            Assert.That(controller.TryRequestLeaderControl(
                    LeaderControlMode.Manual,
                    out string error),
                Is.True,
                error);
            Assert.That(controller.TryStartManualGather(out error),
                Is.True,
                error);
            Assert.That(controller.TryTick(
                    0f,
                    false,
                    out GrayboxExplorationTickResult3D discovery,
                    out error),
                Is.True,
                error);
            Assert.That(discovery.Distress.Kind,
                Is.EqualTo(CenJinDistressTickKind.None));
            Assert.That(controller.CenJinDistress.State,
                Is.EqualTo(CenJinDistressState.Discovered));
            Assert.That(controller.TryBeginCenJinRescue(out error),
                Is.True,
                error);
            Assert.That(reserveCalls, Is.EqualTo(1));

            Assert.That(controller.TryTick(
                    12f,
                    false,
                    out GrayboxExplorationTickResult3D result,
                    out error),
                Is.True,
                error);
            Assert.That(result.ManualGather.UnitsGathered, Is.EqualTo(2));
            Assert.That(gathered, Is.EqualTo(2));
            Assert.That(result.Distress.Kind,
                Is.EqualTo(CenJinDistressTickKind.Completed));
            Assert.That(commitCalls, Is.EqualTo(1));
            Assert.That(controller.CenJinDistress.IsCompleted, Is.True);
        }

        [Test]
        public void IDEA0029_InvalidTickOrMissingDependenciesChangesNothing()
        {
            GrayboxExplorationController3D controller = InitializedController();
            ulong explorationRevision = controller.Exploration.Revision;
            ulong gatherRevision = controller.ManualGather.Revision;
            ulong distressRevision = controller.CenJinDistress.Revision;

            Assert.That(controller.TryTick(
                    -1f,
                    false,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("finite"));
            Assert.That(controller.Exploration.Revision,
                Is.EqualTo(explorationRevision));
            Assert.That(controller.ManualGather.Revision,
                Is.EqualTo(gatherRevision));
            Assert.That(controller.CenJinDistress.Revision,
                Is.EqualTo(distressRevision));

            Assert.That(controller.TryTick(1f, false, out _, out error),
                Is.False);
            Assert.That(error, Does.Contain("configured"));
            Assert.That(controller.ManualGather.Revision,
                Is.EqualTo(gatherRevision));
            Assert.That(controller.CenJinDistress.Revision,
                Is.EqualTo(distressRevision));
        }

        [Test]
        public void IDEA0029_CaptureProjectsAllOwnedSaveFacingState()
        {
            GrayboxExplorationController3D controller = InitializedController();
            controller.TryRequestLeaderControl(
                LeaderControlMode.Manual,
                out _);
            Assert.That(controller.OutpostAlerts.TryReport(
                    "alert.outpost.01",
                    "settlement.outpost.01",
                    8,
                    9,
                    OutpostAlertSeverity.Guard,
                    "游荡敌群",
                    20,
                    90f,
                    10d,
                    out string error),
                Is.True,
                error);

            GrayboxExplorationCapture3D capture = controller.Capture();

            Assert.That(capture.Exploration, Is.Not.Null);
            Assert.That(capture.LeaderControlMode,
                Is.EqualTo(LeaderControlMode.Manual));
            Assert.That(capture.ManualGather.IsActive, Is.False);
            Assert.That(capture.CenJinDistress, Is.Not.Null);
            Assert.That(capture.OutpostAlerts.Alerts,
                Has.Count.EqualTo(1));
        }

        private GrayboxExplorationController3D InitializedController()
        {
            GrayboxExplorationController3D controller = CreateController();
            controller.Initialize(64, 48, "session-controller", (_, __) => true);
            return controller;
        }

        private GrayboxExplorationController3D CreateController()
        {
            GameObject gameObject = Track(new GameObject(
                "IDEA0029 Exploration Controller"));
            return gameObject.AddComponent<GrayboxExplorationController3D>();
        }

        private GrayboxFogPresenter3D CreateFogPresenter(int width, int height)
        {
            GameObject root = Track(new GameObject("Fog"));
            Shader shader = Shader.Find(
                "WasteCity/World/ExplorationFogOverlay");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            GrayboxFogPresenter3D presenter =
                root.AddComponent<GrayboxFogPresenter3D>();
            presenter.Configure(root.transform, material);
            presenter.Generate(new PlanarCoordinateMapper3D(width, height));
            return presenter;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }
    }
}
