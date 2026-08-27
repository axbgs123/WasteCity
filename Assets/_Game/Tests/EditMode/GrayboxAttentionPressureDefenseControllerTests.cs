using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxAttentionPressureDefenseControllerTests
    {
        [Test]
        public void IDEA0020_RuntimeRejectsPressureUntilMainVictoryAndOnlyOwnsOne()
        {
            var runtime = new GrayboxDefenseRuntime3D(0, 0, 20, 0);
            SetMain(runtime, new SingleCityDefenseCampaignModel(0, 0));
            Assert.That(runtime.TryStartPressure(
                AttentionPressureCampaignCatalog.Directional, out _), Is.False);

            SetMain(runtime, WonCampaign("test.main.victory"));
            Assert.That(runtime.TryStartPressure(
                AttentionPressureCampaignCatalog.Directional,
                out string error), Is.True, error);
            Assert.That(runtime.HasActivePressureCampaign, Is.True);
            Assert.That(runtime.ActiveCampaignSnapshot.PlannedEnemyCount,
                Is.EqualTo(22));
            Assert.That(runtime.TryStartPressure(
                AttentionPressureCampaignCatalog.HighRisk, out _), Is.False);
        }

        [Test]
        public void IDEA0020_PressureVictoryCompletesQueueAndRestoresMainOwner()
        {
            var runtime = new GrayboxDefenseRuntime3D(0, 0, 20, 0);
            SingleCityDefenseCampaignModel main = WonCampaign("test.main.owner");
            SetMain(runtime, main);
            var pressure = new AttentionPressureRuntime();
            pressure.TryQueueThreshold(30, out _);
            pressure.Tick(.1f, false, true, true, out _, out _);
            pressure.Tick(60f, false, true, true,
                out AttentionPressureCommand start, out _);
            using (var controller =
                   new GrayboxAttentionPressureDefenseController3D(
                       pressure, runtime))
            {
                Assert.That(controller.TryHandle(start, out string error),
                    Is.True, error);
                SingleCityDefenseCampaignModel active = ActivePressure(runtime);
                active.Advance(60.1f, 1);
                foreach (SingleCityDefenseEnemySnapshot enemy in
                         active.Snapshot.Enemies.ToArray())
                {
                    active.DefeatEnemy(enemy.StableId,
                        BuildingCatalog.MachineGunTurret.Id.Value);
                }
                active.Advance(.1f, 1);

                Assert.That(runtime.HasActivePressureCampaign, Is.False);
                Assert.That(runtime.ActiveCampaignSnapshot.Result,
                    Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
                Assert.That(runtime.ActiveCampaignSnapshot.CurrentWaveNumber,
                    Is.EqualTo(runtime.CampaignSnapshot.CurrentWaveNumber));
                Assert.That(pressure.Capture().Entries.Single().State,
                    Is.EqualTo(AttentionPressureState.Completed));
                Assert.That(controller.LastCompletionCommand.Kind,
                    Is.EqualTo(
                        AttentionPressureCommandKind.EncounterCompleted));
            }
        }

        [Test]
        public void IDEA0020_ControllerPublishesOnlyCommittedStartAndVictory()
        {
            GrayboxDefenseRuntime3D runtime = RuntimeWithWonMain();
            var pressure = new AttentionPressureRuntime();
            pressure.TryQueueThreshold(30, out _);
            pressure.Tick(0.1f, false, true, true, out _, out _);
            pressure.Tick(60f, false, true, true,
                out AttentionPressureCommand start, out _);
            using (var controller =
                   new GrayboxAttentionPressureDefenseController3D(
                       pressure, runtime))
            {
                var started = new List<string>();
                var completed = new List<string>();
                controller.EncounterStarted += command =>
                    started.Add(command.EncounterId);
                controller.EncounterCompleted += command =>
                    completed.Add(command.EncounterId);

                Assert.That(controller.TryHandle(start, out string error),
                    Is.True, error);
                Assert.That(started, Is.EqualTo(new[] { start.EncounterId }));
                Assert.That(completed, Is.Empty);

                SingleCityDefenseCampaignModel active = ActivePressure(runtime);
                active.Advance(60.1f, 1);
                foreach (SingleCityDefenseEnemySnapshot enemy in
                         active.Snapshot.Enemies.ToArray())
                {
                    active.DefeatEnemy(enemy.StableId,
                        BuildingCatalog.MachineGunTurret.Id.Value);
                }
                active.Advance(.1f, 1);
                Assert.That(completed,
                    Is.EqualTo(new[] { start.EncounterId }));
            }
        }

        [Test]
        public void IDEA0020_ActivePressurePersistenceRoundTripsThroughCampaignModel()
        {
            var source = RuntimeWithWonMain();
            Assert.That(source.TryStartPressure(
                AttentionPressureCampaignCatalog.HighRisk, out _), Is.True);
            ActivePressure(source).Advance(2f, 1);
            SingleCityDefenseCampaignPersistenceState saved = Capture(source);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.CampaignId,
                Is.EqualTo(AttentionPressureCampaignCatalog.HighRisk.Id));

            var restored = RuntimeWithWonMain();
            Assert.That(Restore(restored,
                AttentionPressureCampaignCatalog.HighRisk, saved,
                out string error), Is.True, error);
            SingleCityDefenseCampaignPersistenceState actual = Capture(restored);
            Assert.That(actual.CampaignId, Is.EqualTo(saved.CampaignId));
            Assert.That(actual.CurrentWaveNumber,
                Is.EqualTo(saved.CurrentWaveNumber));
            Assert.That(actual.NextEnemyOrdinal,
                Is.EqualTo(saved.NextEnemyOrdinal));
            Assert.That(actual.Enemies.Count,
                Is.EqualTo(saved.Enemies.Count));
        }

        [Test]
        public void IDEA0020_InvalidPressureRestoreIsZeroWriteAndClearReturnsMain()
        {
            var runtime = RuntimeWithWonMain();
            object before = runtime.ActiveCampaignSnapshot;
            var source = RuntimeWithWonMain();
            source.TryStartPressure(
                AttentionPressureCampaignCatalog.Directional, out _);
            SingleCityDefenseCampaignPersistenceState saved = Capture(source);

            Assert.That(Restore(runtime,
                AttentionPressureCampaignCatalog.Boss, saved, out _), Is.False);
            Assert.That(runtime.ActiveCampaignSnapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
            Assert.That(runtime.HasActivePressureCampaign, Is.False);
            Assert.That(Capture(runtime), Is.Null);

            Assert.That(Restore(runtime,
                AttentionPressureCampaignCatalog.Directional, saved,
                out string error), Is.True, error);
            Assert.That(Clear(runtime), Is.True);
            Assert.That(runtime.HasActivePressureCampaign, Is.False);
            Assert.That(runtime.ActivePressureEncounterId, Is.Empty);
            Assert.That(runtime.ActiveCampaignSnapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
            Assert.That(before, Is.Not.Null);
        }

        private static SingleCityDefenseCampaignModel WonCampaign(string id)
        {
            var definition = new SingleCityDefenseCampaignDefinition(id,
                new CampaignWaveDefinition(1, 0, .1f,
                    new[] { CampaignSpawnDirection.East },
                    new WaveEntry(EnemyArchetype.Gnawer, 1)));
            var model = new SingleCityDefenseCampaignModel(0, 0, definition);
            model.TryStartAfterExternalWarning();
            model.Advance(.2f, 1);
            model.DefeatEnemy(model.Snapshot.Enemies.Single().StableId,
                BuildingCatalog.MachineGunTurret.Id.Value);
            model.Advance(.1f, 1);
            Assert.That(model.Snapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
            return model;
        }

        private static GrayboxDefenseRuntime3D RuntimeWithWonMain()
        {
            var runtime = new GrayboxDefenseRuntime3D(0, 0, 20, 0);
            SetMain(runtime, WonCampaign("test.main." + Guid.NewGuid().ToString("N")));
            return runtime;
        }

        private static SingleCityDefenseCampaignPersistenceState Capture(
            GrayboxDefenseRuntime3D runtime)
        {
            MethodInfo method = typeof(GrayboxDefenseRuntime3D).GetMethod(
                "CaptureActivePressurePersistence");
            Assert.That(method, Is.Not.Null);
            return (SingleCityDefenseCampaignPersistenceState)
                method.Invoke(runtime, null);
        }

        private static bool Restore(
            GrayboxDefenseRuntime3D runtime,
            SingleCityDefenseCampaignDefinition definition,
            SingleCityDefenseCampaignPersistenceState state,
            out string error)
        {
            MethodInfo method = typeof(GrayboxDefenseRuntime3D).GetMethod(
                "TryRestoreActivePressure");
            Assert.That(method, Is.Not.Null);
            object[] arguments = { definition, state, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            error = arguments[2] as string;
            return result;
        }

        private static bool Clear(GrayboxDefenseRuntime3D runtime)
        {
            MethodInfo method = typeof(GrayboxDefenseRuntime3D).GetMethod(
                "ClearActivePressure");
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(runtime, null);
        }

        private static void SetMain(
            GrayboxDefenseRuntime3D runtime,
            SingleCityDefenseCampaignModel campaign)
        {
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "campaign", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(runtime, campaign);
        }

        private static SingleCityDefenseCampaignModel ActivePressure(
            GrayboxDefenseRuntime3D runtime)
        {
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "activePressureCampaign",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (SingleCityDefenseCampaignModel)field.GetValue(runtime);
        }
    }
}
