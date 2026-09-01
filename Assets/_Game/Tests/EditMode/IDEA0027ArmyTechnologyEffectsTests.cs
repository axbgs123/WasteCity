using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class IDEA0027ArmyTechnologyEffectsTests
    {
        [TestCase(false, 3)]
        [TestCase(true, 4)]
        public void PuppetryRaisesPerWorkshopCapacityWithoutChangingSquadCap(
            bool completed,
            int expectedUnits)
        {
            var model = new SingleCityArmyModel();
            model.ConfigureResearchEffects(() =>
                ResearchEffectResolver.Resolve(completed
                    ? new[] { "core.research.puppetry" }
                    : System.Array.Empty<string>()));
            using CityResourceStorageModel storage = Storage(
                ResourceIds.Alloy, 8,
                ResourceIds.SpiritIron, 8);

            model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                100f,
                operationalSourceBuildings: 1,
                globallyPaused: false,
                storage);

            Assert.That(model.UnitCount(ArmyUnitCatalog.CombatPuppetId),
                Is.EqualTo(expectedUnits));
            Assert.That(model.DefaultSquad.MaximumUnits,
                Is.EqualTo(SingleCityArmyModel.DefaultSquadMaximumUnits));
        }

        [TestCase(false, 320)]
        [TestCase(true, 352)]
        public void BehemothBreedingRaisesFormalMaximumAndSpawnHealth(
            bool completed,
            int expectedHealth)
        {
            var model = new SingleCityArmyModel();
            model.ConfigureResearchEffects(() =>
                ResearchEffectResolver.Resolve(completed
                    ? new[] { "core.research.behemoth-breeding" }
                    : System.Array.Empty<string>()));
            using CityResourceStorageModel storage = Storage(
                ResourceIds.BoneSteel, 2,
                ResourceIds.BiomassConcentrate, 3);

            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.BredBehemothId,
                35f,
                operationalSourceBuildings: 1,
                globallyPaused: false,
                storage), Is.EqualTo(1));

            ArmyUnitSnapshot unit = model.Units[0];
            Assert.That(unit.MaximumHealth, Is.EqualTo(expectedHealth));
            Assert.That(unit.CurrentHealth, Is.EqualTo(expectedHealth));
        }

        [Test]
        public void BehemothResearchAdjustsExistingUnitAtSameHealthRatio()
        {
            bool completed = false;
            var model = new SingleCityArmyModel();
            model.ConfigureResearchEffects(() =>
                ResearchEffectResolver.Resolve(completed
                    ? new[] { "core.research.behemoth-breeding" }
                    : System.Array.Empty<string>()));
            using CityResourceStorageModel storage = Storage(
                ResourceIds.BoneSteel, 2,
                ResourceIds.BiomassConcentrate, 3);
            model.TickManufacturing(
                ArmyUnitCatalog.BredBehemothId,
                35f,
                1,
                false,
                storage);
            string unitId = model.Units[0].StableId;
            Assert.That(model.ApplyDamage(
                unitId,
                160,
                DamageType.Physical), Is.EqualTo(160));

            completed = true;
            ArmyUnitSnapshot adjusted = model.Units[0];

            Assert.That(adjusted.MaximumHealth, Is.EqualTo(352));
            Assert.That(adjusted.CurrentHealth, Is.EqualTo(176));
        }

        [Test]
        public void TissueRegenerationUsesFractionalClockAndFreezesWhenPaused()
        {
            var model = ArmyWithPuppet(
                "core.research.tissue-regeneration");
            string unitId = model.Units[0].StableId;
            model.ApplyDamage(unitId, 10, DamageType.Physical);

            Assert.That(model.TickTechnologyEffects(.4f, false),
                Is.EqualTo(0));
            Assert.That(model.TickTechnologyEffects(10f, true),
                Is.EqualTo(0));
            Assert.That(model.Units[0].CurrentHealth, Is.EqualTo(90));
            Assert.That(model.TickTechnologyEffects(.6f, false),
                Is.EqualTo(1));
            Assert.That(model.Units[0].CurrentHealth, Is.EqualTo(91));
        }

        [Test]
        public void ArmyRegenerationAccumulatorRoundTripsWithoutFreeHealing()
        {
            var source = ArmyWithPuppet(
                "core.research.tissue-regeneration");
            string unitId = source.Units[0].StableId;
            source.ApplyDamage(unitId, 10, DamageType.Physical);
            source.TickTechnologyEffects(.75f, false);
            ArmyTechnologyPersistenceSnapshot technology =
                source.CaptureTechnologyState();
            SingleCityArmyPersistenceSnapshot army =
                source.CaptureForPersistence();

            var restored = new SingleCityArmyModel();
            restored.ConfigureResearchEffects(() =>
                ResearchEffectResolver.Resolve(new[]
                {
                    "core.research.tissue-regeneration",
                }));
            Assert.That(restored.TryPrepareRestoreForPersistence(
                army,
                out SingleCityArmyRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(restored.TryCommitRestoreForPersistence(
                plan,
                out error), Is.True, error);
            Assert.That(restored.TryRestoreTechnologyState(
                technology,
                out error), Is.True, error);

            Assert.That(restored.TickTechnologyEffects(.24f, false),
                Is.EqualTo(0));
            Assert.That(restored.TickTechnologyEffects(.01f, false),
                Is.EqualTo(1));
            Assert.That(restored.Units[0].CurrentHealth, Is.EqualTo(91));
        }

        [Test]
        public void ArmyRegenerationIsDeterministicAcrossDeltaChunking()
        {
            SingleCityArmyModel oneTick = ArmyWithPuppet(
                "core.research.tissue-regeneration");
            SingleCityArmyModel manyTicks = ArmyWithPuppet(
                "core.research.tissue-regeneration");
            oneTick.ApplyDamage(
                oneTick.Units[0].StableId,
                10,
                DamageType.Physical);
            manyTicks.ApplyDamage(
                manyTicks.Units[0].StableId,
                10,
                DamageType.Physical);

            oneTick.TickTechnologyEffects(2.5f, false);
            for (var index = 0; index < 25; index++)
                manyTicks.TickTechnologyEffects(.1f, false);

            Assert.That(manyTicks.Units[0].CurrentHealth,
                Is.EqualTo(oneTick.Units[0].CurrentHealth));
            Assert.That(
                manyTicks.CaptureTechnologyState().Units[0]
                    .RegenerationAccumulatorSeconds,
                Is.EqualTo(
                    oneTick.CaptureTechnologyState().Units[0]
                        .RegenerationAccumulatorSeconds).Within(.0001f));
        }

        [Test]
        public void GeneSplicingTraitPreservesHealthRatioAndExpiresAtThreeHundredSeconds()
        {
            var character = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            int baseMaximum = character.MaximumHealth;
            character.TryApplyDamage(
                baseMaximum / 2,
                "test.damage",
                out _);

            Assert.That(character.TryApplyGeneSplicingTrait(), Is.True);
            Assert.That(character.MaximumHealth,
                Is.EqualTo((int)System.Math.Round(baseMaximum * 1.2f)));
            Assert.That(character.CurrentHealth,
                Is.EqualTo(character.MaximumHealth / 2));
            character.TickTechnologyEffects(299f, paused: false);
            character.TickTechnologyEffects(20f, paused: true);
            Assert.That(character.HasGeneSplicingTrait, Is.True);
            Assert.That(character.GeneSplicingRemainingSeconds,
                Is.EqualTo(1f).Within(.0001f));

            character.TickTechnologyEffects(1f, paused: false);

            Assert.That(character.HasGeneSplicingTrait, Is.False);
            Assert.That(character.MaximumHealth, Is.EqualTo(baseMaximum));
            Assert.That(character.CurrentHealth, Is.EqualTo(baseMaximum / 2));
        }

        [Test]
        public void GeneSplicingTechnologyStateRestoresBeforeLifeSnapshot()
        {
            var source = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            source.TryApplyGeneSplicingTrait();
            source.TickTechnologyEffects(17.25f, paused: false);
            source.TryApplyDamage(5, "test.damage", out _);
            CharacterTechnologyPersistenceState technology =
                source.CaptureTechnologyState();
            CharacterLifeSnapshot life = source.Capture();

            var restored = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            Assert.That(restored.TryRestoreTechnologyState(
                technology,
                out string error), Is.True, error);
            Assert.That(restored.TryRestore(life, out error), Is.True, error);

            Assert.That(restored.MaximumHealth, Is.EqualTo(source.MaximumHealth));
            Assert.That(restored.CurrentHealth, Is.EqualTo(source.CurrentHealth));
            Assert.That(restored.GeneSplicingRemainingSeconds,
                Is.EqualTo(282.75f).Within(.0001f));
        }

        [Test]
        public void GeneSplicingTraitClearsImmediatelyWhenCharacterDies()
        {
            var character = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            Assert.That(character.TryApplyGeneSplicingTrait(), Is.True);
            character.TryApplyDamage(1000, "combat.enemy.hit", out _);

            character.Tick(60f, false, true, false);

            Assert.That(character.State, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(character.HasGeneSplicingTrait, Is.False);
            Assert.That(character.GeneSplicingRemainingSeconds, Is.Zero);
            Assert.That(character.MaximumHealth,
                Is.EqualTo(CharacterCatalog.CenJin.MaximumHealth));
            Assert.That(character.CaptureTechnologyState()
                .GeneSplicingRemainingSeconds, Is.Zero);
        }

        [Test]
        public void DeadCharacterRejectsActiveGeneSplicingRestore()
        {
            var character = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            character.TryApplyDamage(1000, "combat.enemy.hit", out _);
            character.Tick(60f, false, true, false);

            bool restored = character.TryRestoreTechnologyState(
                new CharacterTechnologyPersistenceState(
                    CharacterCatalog.CenJinId,
                    CharacterLifeRuntime.GeneSplicingTraitDurationSeconds),
                out string error);

            Assert.That(restored, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(character.GeneSplicingRemainingSeconds, Is.Zero);
        }

        private static SingleCityArmyModel ArmyWithPuppet(
            params string[] completedResearchIds)
        {
            var model = new SingleCityArmyModel();
            model.ConfigureResearchEffects(() =>
                ResearchEffectResolver.Resolve(completedResearchIds));
            using CityResourceStorageModel storage = Storage(
                ResourceIds.Alloy, 1,
                ResourceIds.SpiritIron, 1);
            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f,
                1,
                false,
                storage), Is.EqualTo(1));
            return model;
        }

        private static CityResourceStorageModel Storage(
            string firstId,
            int firstAmount,
            string secondId,
            int secondAmount)
        {
            var inventory = new ResourceInventory(1000);
            inventory.Add(firstId, firstAmount);
            inventory.Add(secondId, secondAmount);
            return new CityResourceStorageModel(inventory, 1000);
        }
    }
}
