using System;
using System.Linq;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;

namespace WasteCity.Tests
{
    public sealed class AttentionPressureCampaignCatalogTests
    {
        [Test]
        public void IDEA0020_PressureDefinitionsHaveExactSingleWaveComposition()
        {
            Assert.That(AttentionPressureCampaignCatalog.All, Has.Count.EqualTo(3));
            AssertDefinition(AttentionPressureCampaignCatalog.Directional,
                "core.attention-encounter.directional-attack",
                new[] { CampaignSpawnDirection.East, CampaignSpawnDirection.North },
                (EnemyArchetype.Gnawer, 18),
                (EnemyArchetype.CrystalBeast, 4));
            AssertDefinition(AttentionPressureCampaignCatalog.HighRisk,
                "core.attention-encounter.high-risk-attack",
                new[] { CampaignSpawnDirection.East, CampaignSpawnDirection.North,
                    CampaignSpawnDirection.South, CampaignSpawnDirection.West },
                (EnemyArchetype.Gnawer, 24),
                (EnemyArchetype.CrystalBeast, 6),
                (EnemyArchetype.Howler, 4),
                (EnemyArchetype.Burrower, 1));
            AssertDefinition(AttentionPressureCampaignCatalog.Boss,
                "core.attention-encounter.crystalline-broodmother",
                new[] { CampaignSpawnDirection.East },
                (EnemyArchetype.CrystalBroodmother, 1));
        }

        [Test]
        public void IDEA0020_ExternalWarningStartsDirectlyAndSingleWaveWins()
        {
            var model = new SingleCityDefenseCampaignModel(
                0f, 0f, AttentionPressureCampaignCatalog.Directional);
            Assert.That(model.TryStartAfterExternalWarning(), Is.True);
            Assert.That(model.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.SpawningAndCombat));
            Assert.That(model.Snapshot.WarningRemainingSeconds, Is.Zero);
            Assert.That(model.TryStartAfterExternalWarning(), Is.False);

            model.Advance(60.1f, 1);
            foreach (SingleCityDefenseEnemySnapshot enemy in model.Snapshot.Enemies)
                Assert.That(model.DefeatEnemy(enemy.StableId,
                    BuildingCatalog.MachineGunTurret.Id.Value), Is.True);
            model.Advance(.1f, 1);
            Assert.That(model.Snapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
            Assert.That(model.CaptureForPersistence().CampaignId,
                Is.EqualTo(AttentionPressureCampaignCatalog.Directional.Id));
        }

        [Test]
        public void IDEA0020_DefaultCannotBypassWarningAndCustomStateRestores()
        {
            var legacy = new SingleCityDefenseCampaignModel(0f, 0f);
            Assert.That(legacy.TryStartAfterExternalWarning(), Is.False);
            Assert.That(legacy.CaptureForPersistence().CampaignId,
                Is.EqualTo(CampaignWaveCatalog.Id));

            var source = new SingleCityDefenseCampaignModel(
                0f, 0f, AttentionPressureCampaignCatalog.HighRisk);
            source.TryStartAfterExternalWarning();
            source.Advance(1f, 1);
            SingleCityDefenseCampaignPersistenceState state =
                source.CaptureForPersistence();
            var restored = new SingleCityDefenseCampaignModel(
                0f, 0f, AttentionPressureCampaignCatalog.HighRisk);
            Assert.That(restored.TryPrepareRestore(
                state, out SingleCityDefenseCampaignRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(restored.TryCommitRestore(plan, out error), Is.True, error);
            Assert.That(restored.CaptureForPersistence().CampaignId,
                Is.EqualTo(state.CampaignId));
            var wrong = new SingleCityDefenseCampaignModel(
                0f, 0f, AttentionPressureCampaignCatalog.Boss);
            Assert.That(wrong.TryPrepareRestore(state, out _, out _), Is.False);
        }

        private static void AssertDefinition(
            SingleCityDefenseCampaignDefinition definition,
            string id,
            CampaignSpawnDirection[] directions,
            params (EnemyArchetype archetype, int count)[] entries)
        {
            Assert.That(definition.Id, Is.EqualTo(id));
            Assert.That(definition.Waves, Has.Count.EqualTo(1));
            CampaignWaveDefinition wave = definition.Waves[0];
            Assert.That(wave.Number, Is.EqualTo(1));
            Assert.That(wave.WarningSeconds, Is.Zero);
            Assert.That(wave.Directions, Is.EqualTo(directions));
            Assert.That(wave.Entries.Select(value =>
                    (value.Archetype, value.Count)), Is.EqualTo(entries));
        }
    }
}
