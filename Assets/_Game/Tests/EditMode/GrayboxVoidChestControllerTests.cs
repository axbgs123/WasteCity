using System.IO;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxVoidChestControllerTests
    {
        [Test]
        public void IDEA0028_SelectedFateEvaluatesStableDeathAndClaimsAtomically()
        {
            const string sessionId = "void-chest-consumer-session";
            FormalFateRuntime fate = SelectedVoidChest();
            var runtime = new VoidChestRuntime(sessionId, 1);
            var controller = new GrayboxVoidChestController3D(fate, runtime);
            string deathId = FindFirstDrop(sessionId);

            Assert.That(controller.TryEvaluateOrdinaryEnemyDeath(
                deathId, out VoidChestEvaluation evaluation,
                out string error), Is.True, error);
            Assert.That(evaluation.Dropped, Is.True);
            Assert.That(runtime.Capture().UnclaimedChestIds,
                Is.EqualTo(new[] { evaluation.ChestId }));

            using (var storage = new CityResourceStorageModel(
                       new ResourceInventory(150), 150))
            {
                Assert.That(controller.TryClaim(
                    evaluation.ChestId, storage, out error), Is.True, error);
                Assert.That(storage.GetNetworkAmount(evaluation.ResourceId),
                    Is.EqualTo(evaluation.Amount));
                Assert.That(runtime.Capture().ClaimedChestIds,
                    Is.EqualTo(new[] { evaluation.ChestId }));
                Assert.That(controller.TryClaim(
                    evaluation.ChestId, storage, out _), Is.False);
                Assert.That(storage.GetNetworkAmount(evaluation.ResourceId),
                    Is.EqualTo(evaluation.Amount));
            }
        }

        [Test]
        public void IDEA0028_UnselectedFateDoesNotConsumeDeathOrdinal()
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.PocketUniverseId,
                out _, out _, out _), Is.True);
            var runtime = new VoidChestRuntime("unselected-session", 1);
            var controller = new GrayboxVoidChestController3D(fate, runtime);

            Assert.That(controller.TryEvaluateOrdinaryEnemyDeath(
                "enemy-1", out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.Capture().Evaluations, Is.Empty);
            Assert.That(controller.NextDeathOrdinal, Is.EqualTo(1ul));
        }

        [Test]
        public void IDEA0028_FullStorageLeavesChestUnclaimed()
        {
            const string sessionId = "void-chest-full-storage";
            FormalFateRuntime fate = SelectedVoidChest();
            var runtime = new VoidChestRuntime(sessionId, 1);
            var controller = new GrayboxVoidChestController3D(fate, runtime);
            Assert.That(controller.TryEvaluateOrdinaryEnemyDeath(
                FindFirstDrop(sessionId),
                out VoidChestEvaluation evaluation,
                out _), Is.True);
            using (var storage = new CityResourceStorageModel(
                       new ResourceInventory(0), 0))
            {
                Assert.That(controller.TryClaim(
                    evaluation.ChestId, storage, out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(runtime.Capture().UnclaimedChestIds,
                    Is.EqualTo(new[] { evaluation.ChestId }));
            }
        }

        [Test]
        public void IDEA0028_ControllerResynchronizesAfterRuntimeRestore()
        {
            const string sessionId = "void-chest-controller-restore";
            FormalFateRuntime fate = SelectedVoidChest();
            var runtime = new VoidChestRuntime(sessionId, 1);
            var controller = new GrayboxVoidChestController3D(fate, runtime);
            var restoredSource = new VoidChestRuntime(sessionId, 1);
            Assert.That(restoredSource.TryEvaluateDeath(
                "enemy-restored-1",
                1ul,
                out _,
                out string error), Is.True, error);
            Assert.That(restoredSource.TryEvaluateDeath(
                "enemy-restored-2",
                2ul,
                out _,
                out error), Is.True, error);

            Assert.That(runtime.TryRestore(
                restoredSource.Capture(), out error), Is.True, error);

            Assert.That(controller.NextDeathOrdinal, Is.EqualTo(3ul));
            Assert.That(controller.TryEvaluateOrdinaryEnemyDeath(
                "enemy-restored-1", out _, out error), Is.False);
            Assert.That(error, Does.Contain("已经处理"));
            Assert.That(controller.TryEvaluateOrdinaryEnemyDeath(
                "enemy-after-restore",
                out VoidChestEvaluation evaluation,
                out error), Is.True, error);
            Assert.That(evaluation.SequenceOrdinal, Is.EqualTo(3ul));
            Assert.That(controller.NextDeathOrdinal, Is.EqualTo(4ul));
        }

        [Test]
        public void IDEA0028_FormalCaptureWithPendingChestPassesValidation()
        {
            FormalSaveEnvelope envelope = CapturePendingChestEnvelope(
                out VoidChestEvaluation pending);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(pending.Dropped, Is.True);
            Assert.That(pending.Claimed, Is.False);
            Assert.That(result.IsValid, Is.True,
                result.Error + " @ " + result.FieldPath);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IDEA0028_PendingChestMustMatchCommittedDeathAndOrdinal(
            bool changeDeathId)
        {
            FormalSaveEnvelope envelope = CapturePendingChestEnvelope(out _);
            FormalThreeDVoidChestEntrySaveData pending =
                envelope.formal3D.progression.voidChest.pendingChests[0];
            if (changeDeathId)
                pending.deathEventId += ".different";
            else
                pending.dropOrdinal++;
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);

            FormalSaveValidationResult result =
                FormalSaveValidator.ValidateEnvelope(envelope);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FieldPath,
                Is.EqualTo(
                    "formal3D.progression.voidChest.pendingChests[0]"));
        }

        private static FormalSaveEnvelope CapturePendingChestEnvelope(
            out VoidChestEvaluation pending)
        {
            const string sessionId = "void-chest-formal-capture";
            FormalFateRuntime fate = SelectedVoidChest();
            var chests = new VoidChestRuntime(sessionId, 1);
            var controller = new GrayboxVoidChestController3D(fate, chests);
            Assert.That(controller.TryEvaluateOrdinaryEnemyDeath(
                FindFirstDrop(sessionId),
                out pending,
                out string error), Is.True, error);
            Assert.That(controller.TryEvaluateOrdinaryEnemyDeath(
                FindFirstNonDrop(sessionId, 2ul),
                out VoidChestEvaluation followup,
                out error), Is.True, error);
            Assert.That(followup.Dropped, Is.False);

            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                attention,
                fate,
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                new GrayboxAttentionPressureSaveAdapter3D(
                    pressure,
                    new GrayboxDefenseRuntime3D(0f, 0f, 20f, 0f)),
                null,
                null,
                new QuantumEntanglementRuntime(new[]
                {
                    ResourceIds.Iron,
                    ResourceIds.Stone,
                    ResourceIds.Water,
                    ResourceIds.Biomass,
                }),
                new SpatialTemplateRuntime(),
                new LocalHasteRuntime(),
                new ForesightDelayRuntime(),
                new CausalTransparencyRuntime(),
                chests,
                new CoordinateLockRuntime(attention, pressure));
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeEnvelope(
                File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "_Game/Tests/Fixtures/Persistence/" +
                    "schema-31-formal-3d.json")));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveEnvelope envelope = decoded.Envelope;
            envelope.formal3D.progression = adapter.Capture();
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);
            return envelope;
        }

        private static FormalFateRuntime SelectedVoidChest()
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TryRestore(
                new FormalFateSnapshot(0ul, new[]
                {
                    FormalFateCatalog.VoidChestId,
                    FormalFateCatalog.PocketUniverseId,
                    FormalFateCatalog.LocalHasteId,
                }, string.Empty, 0), out string restoreError),
                Is.True, restoreError);
            Assert.That(fate.TrySelect(
                FormalFateCatalog.VoidChestId,
                out _, out _, out string selectError),
                Is.True, selectError);
            return fate;
        }

        private static string FindFirstDrop(string sessionId)
        {
            for (var index = 1; index <= 10000; index++)
            {
                string deathId = "enemy-" + index;
                if (VoidChestRuntime.ShouldDrop(
                        sessionId, 1, deathId, 1ul))
                    return deathId;
            }
            Assert.Fail("A deterministic 1% drop input was not found.");
            return string.Empty;
        }

        private static string FindFirstNonDrop(
            string sessionId,
            ulong ordinal)
        {
            for (var index = 1; index <= 100; index++)
            {
                string deathId = "non-drop-enemy-" + index;
                if (!VoidChestRuntime.ShouldDrop(
                        sessionId, 1, deathId, ordinal))
                    return deathId;
            }
            Assert.Fail("A deterministic non-drop input was not found.");
            return string.Empty;
        }
    }
}
