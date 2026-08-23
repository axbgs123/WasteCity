using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseCampaignCheckpointTests
    {
        private const string CampaignWarningEventName =
            "WaveWarningStarted";
        private const string ControllerWarningEventName =
            "CampaignWaveWarningStarted";
        private const string WarningReasonFieldName =
            "CampaignWaveWarningStarted";

        [Test]
        public void CampaignPublishesEachWarningBoundaryOnceWithLiveTruth()
        {
            var campaign = new SingleCityDefenseCampaignModel(12f, 18f);
            var announcedWaves = new List<int>();
            var observedTruth = new List<string>();
            BindWaveWarning(campaign, waveNumber =>
            {
                SingleCityDefenseCampaignSnapshot snapshot = campaign.Snapshot;
                announcedWaves.Add(waveNumber);
                observedTruth.Add(
                    waveNumber + "|" + snapshot.CurrentWaveNumber + "|" +
                    snapshot.Phase);
            });

            Assert.That(TriggerCampaign(campaign), Is.True);
            campaign.Advance(0f, 1);
            campaign.Advance(.04f, 1);
            campaign.Advance(.04f, 1);
            campaign.Advance(.01f, 1);

            var guard = 0;
            while (!campaign.IsTerminal && guard++ < 20)
            {
                campaign.Advance(500f, 1);
                DefeatAllVisibleEnemies(campaign);
                campaign.Advance(.1f, 1);
            }

            Assert.That(campaign.IsTerminal, Is.True,
                "The fixture must reach the real campaign terminal state.");
            CollectionAssert.AreEqual(
                Enumerable.Range(1, CampaignWaveCatalog.All.Count).ToArray(),
                announcedWaves,
                "Every wave owns exactly one pre-combat warning checkpoint " +
                "boundary, including the first wave.");
            CollectionAssert.AreEqual(
                announcedWaves.Select(wave => wave + "|" + wave + "|Warning")
                    .ToArray(),
                observedTruth,
                "A queued checkpoint must observe the committed live Warning " +
                "state instead of stale pre-transition truth.");

            int terminalCount = announcedWaves.Count;
            campaign.Advance(500f, 1);
            campaign.Advance(.1f, 2);
            Assert.That(announcedWaves, Has.Count.EqualTo(terminalCount),
                "Victory or defeat must not publish another wave checkpoint.");

            var defeated = new SingleCityDefenseCampaignModel(12f, 18f);
            var defeatAnnouncements = new List<int>();
            BindWaveWarning(defeated, defeatAnnouncements.Add);
            Assert.That(TriggerCampaign(defeated), Is.True);
            defeated.ApplyCoreDamage(int.MaxValue);
            Assert.That(defeated.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Defeat));
            defeated.Advance(500f, 2);
            CollectionAssert.AreEqual(new[] { 1 }, defeatAnnouncements,
                "Defeat terminates the campaign without inventing a new wave.");
        }

        [Test]
        public void WarningRestoreAndFragmentedTicksDoNotRepublishCurrentWave()
        {
            var source = new SingleCityDefenseCampaignModel(7f, 9f);
            Assert.That(TriggerCampaign(source), Is.True);
            source.Advance(.4f, 1);
            SingleCityDefenseCampaignPersistenceState saved =
                source.CaptureForPersistence();
            Assert.That(saved.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Warning));

            var restored = new SingleCityDefenseCampaignModel(7f, 9f);
            var announcedWaves = new List<int>();
            BindWaveWarning(restored, announcedWaves.Add);
            Assert.That(restored.TryPrepareRestore(
                saved,
                out SingleCityDefenseCampaignRestorePlan plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(restored.TryCommitRestore(plan, out string commitError),
                Is.True, commitError);
            Assert.That(announcedWaves, Is.Empty,
                "Restore adopts saved truth and is not a gameplay transition.");

            for (var index = 0; index < 20; index++)
            {
                restored.Advance(.01f, 0);
                restored.Advance(.01f, 1);
            }
            Assert.That(announcedWaves, Is.Empty,
                "Repeated and partitioned ticks inside a restored Warning " +
                "must not requeue the same wave.");

            restored.Advance(500f, 1);
            DefeatAllVisibleEnemies(restored);
            restored.Advance(.1f, 1);
            CollectionAssert.AreEqual(new[] { 2 }, announcedWaves,
                "Only the next real Warning transition publishes after restore.");
        }

        [Test]
        public void PolicyDeduplicatesWaveIdentityButAllowsLaterWaves()
        {
            FieldInfo reasonField = typeof(FormalSaveCheckpointReasonIds)
                .GetField(
                    WarningReasonFieldName,
                    BindingFlags.Public | BindingFlags.Static);
            Assert.That(reasonField, Is.Not.Null,
                "Wave warnings need one stable checkpoint reason constant.");
            Assert.That(reasonField.IsLiteral, Is.True);
            string reason = (string)reasonField.GetRawConstantValue();
            Assert.That(reason, Is.EqualTo("campaign-wave-warning-started"));

            var attempts = new List<FormalSaveCheckpointMetadata>();
            var policy = new FormalSaveCheckpointPolicy(checkpoint =>
            {
                attempts.Add(checkpoint);
                return true;
            }, () => 8f);

            Assert.That(policy.QueueCheckpoint(
                reason,
                "campaign.wave.0001|warning"), Is.True);
            Assert.That(policy.QueueCheckpoint(
                reason,
                "campaign.wave.0001|warning"), Is.False);
            Assert.That(policy.FlushPending(), Is.True);
            Assert.That(policy.QueueCheckpoint(
                reason,
                "campaign.wave.0002|warning"), Is.True,
                "The reason is repeatable across distinct stable wave IDs.");
            Assert.That(policy.FlushPending(), Is.True);

            Assert.That(attempts.Select(value => value.reasonId),
                Is.EqualTo(new[] { reason, reason }));
            Assert.That(policy.CompletedMilestoneIds, Is.Empty,
                "Wave checkpoints are repeatable events, not one-shot " +
                "campaign milestones.");
        }

        [Test]
        public void CoordinatorQueuesWaveWarningsWithoutOwningASecondSavePath()
        {
            EventInfo controllerEvent = typeof(GrayboxDefenseController3D)
                .GetEvent(
                    ControllerWarningEventName,
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(controllerEvent, Is.Not.Null,
                "The controller must relay the pure campaign boundary.");
            Assert.That(controllerEvent.EventHandlerType,
                Is.EqualTo(typeof(Action<int>)),
                "The relay carries the stable one-based wave number.");

            string coordinatorSource = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs"));
            string configure = ExtractMethodBlock(
                coordinatorSource,
                "public void ConfigureCheckpointPolicy(");
            string unbind = ExtractMethodBlock(
                coordinatorSource,
                "public void UnbindCheckpointPolicy(");
            string handler = ExtractMethodBlock(
                coordinatorSource,
                "private void HandleCampaignWaveWarningStarted(");
            StringAssert.Contains(
                "defense." + ControllerWarningEventName + " +=",
                configure);
            StringAssert.Contains(
                "checkpointDefense." + ControllerWarningEventName + " -=",
                unbind);
            StringAssert.Contains("checkpointPolicy?.QueueCheckpoint(", handler);
            StringAssert.Contains(
                "FormalSaveCheckpointReasonIds." + WarningReasonFieldName,
                handler);
            StringAssert.DoesNotContain("FlushPending", handler,
                "The domain callback only queues; the existing automatic " +
                "transaction remains the sole flush owner.");
            StringAssert.DoesNotContain("TryWriteCheckpoint", handler);

            string hostSource = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveRuntimeHost3D.cs"));
            string lateUpdate = ExtractMethodBlock(
                hostSource,
                "private void LateUpdate()");
            StringAssert.Contains("FlushPendingCheckpoint()", lateUpdate,
                "Automatic saves keep the existing frame-end transaction.");
            string manualSave = ExtractMethodBlock(
                hostSource,
                "public bool TrySaveAndExit()");
            StringAssert.Contains("checkpointPolicy.HasPending", manualSave,
                "Manual save-and-exit must continue draining the same pending " +
                "transaction before creating its explicit save.");
        }

        [Test]
        public void ControllerSuppressesCampaignStartOnlyDuringPersistencePause()
        {
            string controllerSource = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxDefenseController3D.cs"));
            string synchronize = ExtractMethodBlock(
                controllerSource,
                "private bool TrySynchronizeRuntime(");

            StringAssert.Contains(
                "allowCampaignStart: !IsPersistencePaused",
                synchronize,
                "Persistence rebuild still synchronizes runtime topology, " +
                "but campaign start belongs to the first gameplay tick after " +
                "the coordinator releases its pause.");
        }

        [Test]
        public void ProductionCheckpointCaptureReadsFormalCampaignLiveTruth()
        {
            string coordinatorSource = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs"));
            string productionFactory = ExtractMethodBlock(
                coordinatorSource,
                "public static GrayboxFormalSaveCoordinator3D " +
                "CreateProduction(");
            StringAssert.Contains(
                "destination.defenseCampaign =\n" +
                "                            defense.CaptureCampaign();",
                productionFactory,
                "The queued wave request must capture the live formal " +
                "campaign adapter at flush time.");
            StringAssert.DoesNotContain("retainedCampaign", productionFactory);

            string hostSource = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveRuntimeHost3D.cs"));
            string write = ExtractMethodBlock(
                hostSource,
                "private bool TryWriteCheckpoint(");
            int capture = write.IndexOf("coordinator.CaptureEnvelope(",
                StringComparison.Ordinal);
            int store = write.IndexOf("store.SaveEnvelope(",
                StringComparison.Ordinal);
            Assert.That(capture, Is.GreaterThanOrEqualTo(0));
            Assert.That(store, Is.GreaterThan(capture),
                "The formal campaign live capture precedes the existing " +
                "single-slot store transaction.");
        }

        private static bool TriggerCampaign(
            SingleCityDefenseCampaignModel campaign)
        {
            return campaign.NotifyDefenseTowerCompleted(
                "building.instance.checkpoint-tower",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true);
        }

        private static void BindWaveWarning(
            SingleCityDefenseCampaignModel campaign,
            Action<int> listener)
        {
            EventInfo warning = typeof(SingleCityDefenseCampaignModel).GetEvent(
                CampaignWarningEventName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(warning, Is.Not.Null,
                "The campaign model must publish its authoritative Warning " +
                "transition instead of requiring snapshot polling.");
            Assert.That(warning.EventHandlerType, Is.EqualTo(typeof(Action<int>)));
            warning.AddEventHandler(campaign, listener);
        }

        private static void DefeatAllVisibleEnemies(
            SingleCityDefenseCampaignModel campaign)
        {
            SingleCityDefenseEnemySnapshot[] enemies =
                campaign.Snapshot.Enemies.ToArray();
            for (var index = 0; index < enemies.Length; index++)
            {
                Assert.That(campaign.DefeatEnemy(
                    enemies[index].StableId,
                    BuildingCatalog.MachineGunTurret.Id.Value), Is.True);
            }
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        private static string ExtractMethodBlock(
            string source,
            string declaration)
        {
            int start = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), declaration);
            int opening = source.IndexOf('{', start);
            Assert.That(opening, Is.GreaterThanOrEqualTo(0));
            var depth = 0;
            for (var index = opening; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}') depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            throw new AssertionException("Unbalanced method: " + declaration);
        }
    }
}
