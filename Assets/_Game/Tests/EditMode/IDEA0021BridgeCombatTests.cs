using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class IDEA0021BridgeCombatTests
    {
        [Test]
        public void SixBridgeNodesBecomeResearchableWithoutChangingRules()
        {
            AssertBridge("core.research.bridge.psionic-mech", 90f,
                (ResourceIds.Alloy, 30), (ResourceIds.PsionicAmplifier, 20));
            AssertBridge("core.research.bridge.high-frequency-sword", 90f,
                (ResourceIds.FlyingSword, 12), (ResourceIds.Alloy, 30));
            AssertBridge("core.research.bridge.bio-hangar", 90f,
                (ResourceIds.BoneSteel, 25), (ResourceIds.Alloy, 25));
            AssertBridge("core.research.bridge.spirit-plant", 90f,
                (ResourceIds.SpiritIron, 20),
                (ResourceIds.BiomassConcentrate, 20));
            AssertBridge("core.research.bridge.psionic-pulse", 90f,
                (ResourceIds.PsionicAmplifier, 20),
                (ResourceIds.Ammunition, 30));
            AssertBridge("core.research.bridge.flesh-elixir", 90f,
                (ResourceIds.BiomassConcentrate, 25),
                (ResourceIds.EnergyCrystal, 25));
        }

        [Test]
        public void FiveBridgeBuildingsJoinFormalCatalogAndBuildMenu()
        {
            Assert.That(BuildingCatalog.All, Has.Length.EqualTo(35));
            Assert.That(BuildingCatalog.BuildMenu, Has.Length.EqualTo(33));
            AssertBuilding(BuildingCatalog.PsionicMechFactory, 3, 2,
                "core.research.bridge.psionic-mech");
            AssertBuilding(BuildingCatalog.HighFrequencySwordForge, 2, 2,
                "core.research.bridge.high-frequency-sword");
            AssertBuilding(BuildingCatalog.BioHangar, 3, 2,
                "core.research.bridge.bio-hangar");
            AssertBuilding(BuildingCatalog.SpiritPlantGarden, 2, 2,
                "core.research.bridge.spirit-plant");
            AssertBuilding(BuildingCatalog.EmpTower, 1, 1,
                "core.research.bridge.psionic-pulse");
        }

        [Test]
        public void BridgeRecipesUseExistingResourcesAndApprovedBuildings()
        {
            AssertRecipe(
                "fusion.production.psionic-mech-components",
                BuildingCatalog.PsionicMechFactory.Id.Value,
                18f,
                new[]
                {
                    (ResourceIds.MechanicalComponent, 2),
                    (ResourceIds.PsionicAmplifier, 1),
                    (ResourceIds.EnergyCell, 1),
                },
                new[] { (ResourceIds.ControlChip, 2) });
            AssertRecipe(
                "fusion.production.high-frequency-flying-sword",
                BuildingCatalog.HighFrequencySwordForge.Id.Value,
                10f,
                new[]
                {
                    (ResourceIds.SpiritIron, 2),
                    (ResourceIds.SuperconductiveCoil, 1),
                },
                new[] { (ResourceIds.FlyingSword, 3) });
            AssertRecipe(
                "fusion.production.bio-hangar-weapons",
                BuildingCatalog.BioHangar.Id.Value,
                14f,
                new[]
                {
                    (ResourceIds.BoneSteel, 2),
                    (ResourceIds.MechanicalComponent, 1),
                    (ResourceIds.ActiveBiomass, 1),
                },
                new[] { (ResourceIds.BiologicalWeapon, 3) });

            Assert.That(Recipe(
                    "fusion.production.spirit-plant-extract").AllowedBuildingIds,
                Does.Contain(BuildingCatalog.BreedingChamber.Id.Value)
                    .And.Contain(BuildingCatalog.SpiritPlantGarden.Id.Value));
            Assert.That(Recipe(
                    "fusion.production.flesh-elixir").AllowedBuildingIds,
                Does.Contain(BuildingCatalog.AlchemyChamber.Id.Value));
        }

        [Test]
        public void FormalCombatWhitelistCapacityRangeAndUpgradeRebuildAreStable()
        {
            foreach (BuildingDefinition building in new[]
                     {
                         BuildingCatalog.HeavyMachineGunTurret,
                         BuildingCatalog.SwordArrayTower,
                         BuildingCatalog.SwordRidingPlatform,
                         BuildingCatalog.EmpTower,
                     })
            {
                DefenseTowerDefinition definition =
                    DefenseTowerCatalog.For(building.Id.Value);
                Assert.That(definition, Is.Not.Null, building.Name);
                Assert.That(definition.LocalCapacity, Is.EqualTo(30),
                    building.Name);
                Assert.DoesNotThrow(() => new SingleCityDefenseTowerCombatModel(
                    "tower." + building.Id.Value,
                    building.Id.Value,
                    0f,
                    0f,
                    30));
            }

            var sword = new SingleCityDefenseTowerCombatModel(
                "tower.sword", BuildingCatalog.SwordArrayTower.Id.Value,
                0f, 0f, 15);
            sword.SetRangeMultiplier(1.3f);
            Assert.That(sword.Range,
                Is.EqualTo(DefenseTowerCatalog.For(
                    BuildingCatalog.SwordArrayTower.Id.Value).Range * 1.3f));

            var machineGun = new SingleCityDefenseTowerCombatModel(
                "tower.upgrade", BuildingCatalog.MachineGunTurret.Id.Value,
                1f, 2f, 27);
            machineGun.SetPlayerPaused(true);
            machineGun.SetLogisticsConnected(false);
            SingleCityDefenseTowerCombatModel heavy =
                machineGun.RebuildForBuilding(
                    BuildingCatalog.HeavyMachineGunTurret.Id.Value, 1f);
            Assert.That(heavy.StableInstanceId, Is.EqualTo("tower.upgrade"));
            Assert.That(heavy.BuildingId,
                Is.EqualTo(BuildingCatalog.HeavyMachineGunTurret.Id.Value));
            Assert.That(heavy.LocalConsumableAmount, Is.EqualTo(27));
            Assert.That(heavy.IsPlayerPaused, Is.True);
            Assert.That(heavy.IsLogisticsConnected, Is.False);
        }

        [Test]
        public void EmpSuppressesMechanicalMovementForExactlyNextMoveAttempt()
        {
            Assert.That(EnemyCatalog.Burrower.IsMechanical, Is.True,
                "The formal campaign needs an observable Mechanical target for EMP.");
            var mechanical = new EnemyDefinition(
                "test.enemy.mechanical", "机械测试体", EnemyArchetype.Gnawer,
                100, 2f, 0f, 1f, ArmorType.Light, 0,
                EnemyTargetPriority.Core, isMechanical: true);
            var target = new DefenseEnemyCombatModel(
                "enemy.mechanical", mechanical, 5f, 0f);
            var emp = new SingleCityDefenseTowerCombatModel(
                "tower.emp", BuildingCatalog.EmpTower.Id.Value,
                0f, 0f, 1);

            Assert.That(emp.Tick(.1f, target, false), Is.GreaterThan(0));
            Assert.That(target.MoveTowards(0f, 0f, .1f, 0f), Is.Zero);
            Assert.That(target.MoveTowards(0f, 0f, .1f, 0f),
                Is.GreaterThan(0f));
        }

        [Test]
        public void FleshElixirTriplesHealingThenAppliesTwentyPercentBacklash()
        {
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.Elixir, 1);
            var city = new HealthModel(1000);
            city.Restore(200);
            var building = new HealthModel(300);
            building.Restore(50);

            Assert.That(ElixirUseModel.TryUse(
                inventory, city, new[] { building },
                fleshElixirUnlocked: true,
                mutationSamplePercent: 19,
                out int backlash), Is.True);
            Assert.That(backlash, Is.EqualTo(150));
            Assert.That(city.Current, Is.EqualTo(800));
            Assert.That(building.Current, Is.EqualTo(300));

            inventory.Add(ResourceIds.Elixir, 1);
            city.Restore(200);
            building.Restore(50);
            Assert.That(ElixirUseModel.TryUse(
                inventory, city, new[] { building }, true, 20,
                out backlash), Is.True);
            Assert.That(backlash, Is.Zero);
            Assert.That(city.Current, Is.EqualTo(950));
        }

        [Test]
        public void AlloyArmorHealthResyncPreservesDamageAmount()
        {
            var root = new GameObject("Bridge.Health.Test");
            root.SetActive(false);
            try
            {
                var session = root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureFormalSession();
                var presentation = new NullPresentation();
                Assert.That(session.TryRestoreBuildings(
                    new[]
                    {
                        new GrayboxBuildingRestoreEntry3D(
                            "building.instance.000001",
                            BuildingCatalog.Warehouse,
                            BuildingSite.Ground,
                            2, 2, BuildingOrientation.North,
                            GrayboxBuildingInstanceState.Completed,
                            0f, true, false, default),
                    }, 2, presentation, out string error), Is.True, error);
                var health = new GrayboxBuildingHealthRuntime3D();
                health.Synchronize(session.Instances, alloyArmorCompleted: false);
                Assert.That(health.TryApplyDamage(
                    "building.instance.000001", 80,
                    out _, out _), Is.True);
                health.Synchronize(session.Instances, alloyArmorCompleted: true);
                Assert.That(health.TryGetHealth(
                    "building.instance.000001",
                    out int current,
                    out int maximum,
                    out _), Is.True);
                Assert.That(maximum, Is.EqualTo(390));
                Assert.That(current, Is.EqualTo(310));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertBridge(
            string id,
            float seconds,
            params (string Id, int Amount)[] costs)
        {
            ResearchDefinition node = ResearchCatalog.Find(id);
            Assert.That(node, Is.Not.Null, id);
            Assert.That(node.ReleaseState,
                Is.EqualTo(ResearchReleaseState.Researchable), id);
            Assert.That(node.Tier, Is.EqualTo(3), id);
            Assert.That(node.LayoutRow, Is.EqualTo(4), id);
            Assert.That(node.Duration, Is.EqualTo(seconds), id);
            Assert.That(node.RequiredResearchIds, Has.Count.EqualTo(2), id);
            Assert.That(node.Costs.Select(value =>
                    (value.ResourceId, value.Amount)),
                Is.EqualTo(costs), id);
        }

        private static void AssertBuilding(
            BuildingDefinition value,
            int width,
            int height,
            string researchId)
        {
            Assert.That(value.Width, Is.EqualTo(width), value.Name);
            Assert.That(value.Height, Is.EqualTo(height), value.Name);
            Assert.That(value.RequiredResearchId,
                Is.EqualTo(researchId), value.Name);
            Assert.That(BuildingCatalog.All, Does.Contain(value));
            Assert.That(BuildingCatalog.BuildMenu, Does.Contain(value));
        }

        private static void AssertRecipe(
            string id,
            string buildingId,
            float seconds,
            (string Id, int Amount)[] inputs,
            (string Id, int Amount)[] outputs)
        {
            ResourceRecipeDefinition recipe = Recipe(id);
            Assert.That(recipe, Is.Not.Null, id);
            Assert.That(recipe.AllowedBuildingIds,
                Is.EqualTo(new[] { buildingId }), id);
            Assert.That(recipe.DurationSeconds, Is.EqualTo(seconds), id);
            Assert.That(recipe.Inputs.Select(value =>
                    (value.ResourceId, value.Amount)),
                Is.EqualTo(inputs), id);
            Assert.That(recipe.Outputs.Select(value =>
                    (value.ResourceId, value.Amount)),
                Is.EqualTo(outputs), id);
        }

        private static ResourceRecipeDefinition Recipe(string id)
        {
            Assert.That(ResourceRecipeCatalog.TryGet(
                id, out ResourceRecipeDefinition recipe), Is.True, id);
            return recipe;
        }

        private sealed class NullPresentation : IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
