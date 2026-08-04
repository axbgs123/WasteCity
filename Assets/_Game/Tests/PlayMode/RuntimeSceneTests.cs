using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Persistence;
using WasteCity.Presentation;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Population;
using WasteCity.Research;

namespace WasteCity.Tests.PlayMode
{
    public sealed class RuntimeSceneTests
    {
        [UnityTearDown]
        public IEnumerator RestoreTimeScale()
        {
            var gameSpeed = Object.FindObjectOfType<WasteCity.Core.GameSpeedController>();
            gameSpeed?.SetPaused(WasteCity.Core.GamePauseReason.Title, false);
            gameSpeed?.SetPaused(WasteCity.Core.GamePauseReason.Session, false);
            gameSpeed?.SetPaused(WasteCity.Core.GamePauseReason.Defeat, false);
            gameSpeed?.SetPaused(WasteCity.Core.GamePauseReason.Advancement, false);
            Time.timeScale = 1f;
            foreach (GameObject item in Object.FindObjectsOfType<GameObject>())
                if (item.name.StartsWith("Infection")
                    || item.name.StartsWith("SporeTower")
                    || item.name.StartsWith("AcidTower")
                    || item.name.StartsWith("PhysicalTower")
                    || item.name.StartsWith("Technology"))
                    Object.Destroy(item);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TitleMenuKeepsWorldPausedAcrossFrames()
        {
            SceneManager.LoadScene("FormalPrototype");
            yield return null;
            yield return null;

            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FormalSceneStartsWithPersistentRuntimeAndAttachedBuildingRoot()
        {
            SceneManager.LoadScene("FormalPrototype"); yield return null;
            var city = Object.FindObjectOfType<PlaceholderMobileCity>(); var buildings = Object.FindObjectOfType<PlaceholderBuildingController>();
            Assert.That(city, Is.Not.Null); Assert.That(buildings, Is.Not.Null); Assert.That(Object.FindObjectOfType<FormalSaveController>(), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<FormalFriendlyUnitController>(), Is.Not.Null);
            Assert.That(buildings.transform.parent, Is.EqualTo(city.transform)); Assert.That(Camera.main, Is.Not.Null);
            Assert.That(buildings.HasLocalTimeSource, Is.True);
            Assert.That(city.GetComponent<VisualSlot>()?.StableId, Is.EqualTo("core.city.mobile")); Assert.That(Object.FindObjectOfType<VisualLibraryProvider>()?.Library, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RallyMarkerUsesReplaceableVisualAndClearsBackToCity()
        {
            SceneManager.LoadScene("FormalPrototype");
            yield return null;
            yield return null;
            var controller = Object.FindObjectOfType<FormalFriendlyUnitController>();
            var city = Object.FindObjectOfType<PlaceholderMobileCity>();

            controller.SetRallyPoint(new Vector2(4f, -3f));

            Assert.That(controller.Commands.HasFixedRally, Is.True);
            Assert.That(controller.RallyMarker, Is.Not.Null);
            Assert.That(controller.RallyMarker.activeSelf, Is.True);
            Assert.That(controller.RallyMarker.transform.position.x, Is.EqualTo(4f).Within(.001f));
            Assert.That(controller.RallyMarker.transform.position.y, Is.EqualTo(-3f).Within(.001f));
            Assert.That(controller.RallyMarker.GetComponent<VisualSlot>().StableId, Is.EqualTo("core.command.rally-point"));

            controller.FollowCity();
            FriendlyRallyPoint resolved = controller.Commands.ResolveRally(city.transform.position.x, city.transform.position.y);
            Assert.That(controller.Commands.HasFixedRally, Is.False);
            Assert.That(controller.RallyMarker.activeSelf, Is.False);
            Assert.That(resolved.X, Is.EqualTo(city.transform.position.x));
            Assert.That(resolved.Y, Is.EqualTo(city.transform.position.y));
        }

        [UnityTest]
        public IEnumerator SaveRestoreAppliesBuildingCapacityBeforeStoredValues()
        {
            SceneManager.LoadScene("FormalPrototype");
            yield return null;
            var saves = Object.FindObjectOfType<FormalSaveController>();
            var economy = Object.FindObjectOfType<FormalEconomyController>();
            var population = Object.FindObjectOfType<FormalPopulationController>();
            var data = saves.CaptureComplete();
            data.iron = 250;
            data.population = 100;
            data.populationCapacity = 200;
            data.buildings = new[]
            {
                new BuildingSnapshot { definitionId = BuildingCatalog.Housing.Id.Value, x = 0, y = 0, health = 250, constructionRemaining = 0f },
                new BuildingSnapshot { definitionId = BuildingCatalog.Warehouse.Id.Value, x = 3, y = 0, health = 300, constructionRemaining = 0f }
            };

            Assert.That(saves.ApplyComplete(data, false), Is.True);

            Assert.That(population.Model.Capacity, Is.EqualTo(200));
            Assert.That(economy.Inventory.CapacityPerResource, Is.EqualTo(300));
            Assert.That(economy.Inventory.Get(ResourceIds.Iron), Is.EqualTo(250));
        }

        [UnityTest]
        public IEnumerator ResourceMarkersStayHiddenUntilTheirTileIsRevealed()
        {
            var worldObject = new GameObject("FogVisibilityWorld");
            var world = worldObject.AddComponent<WasteCity.World.PlaceholderWorldView>();
            world.Generate(new WasteCity.World.WorldSeed(8128));
            var marker = worldObject.GetComponentsInChildren<SpriteRenderer>()
                .First(value => value.gameObject.name == "ResourcePlaceholder");

            world.RefreshVisibility();

            Assert.That(marker.enabled, Is.False);
            world.RevealAroundWorld(marker.transform.parent.position, 0);
            Assert.That(marker.enabled, Is.True);
            Object.Destroy(worldObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResourceMarkerHidesAfterNodeIsDepleted()
        {
            var worldObject = new GameObject("DepletedResourceWorld");
            var world = worldObject.AddComponent<WasteCity.World.PlaceholderWorldView>();
            world.Generate(new WasteCity.World.WorldSeed(8128));
            var marker = worldObject.GetComponentsInChildren<SpriteRenderer>()
                .First(value => value.gameObject.name == "ResourcePlaceholder");
            var tilePosition = marker.transform.parent.localPosition;
            int x = Mathf.FloorToInt(tilePosition.x + world.Model.Width * .5f);
            int y = Mathf.FloorToInt(tilePosition.y + world.Model.Height * .5f);
            world.RevealAroundWorld(marker.transform.parent.position, 0);

            world.Model.Harvest(x, y, int.MaxValue, out _);
            world.RefreshVisibility();

            Assert.That(marker.enabled, Is.False);
            Object.Destroy(worldObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostileEnemyRetaliatesAgainstNearbyPuppet()
        {
            var cityObject = new GameObject("TestCityTarget"); cityObject.transform.position = Vector3.right * 50f;
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var enemyObject = new GameObject("TestHostile"); enemyObject.AddComponent<HealthComponent>();
            var enemy = enemyObject.AddComponent<PlaceholderEnemy>(); enemy.Configure(cityHealth, cityObject.transform, EnemyCatalog.Gnawer, new ResourceInventory(100));
            var puppetObject = new GameObject("TestPuppet"); puppetObject.transform.position = Vector3.right * .5f; puppetObject.AddComponent<HealthComponent>();
            var puppet = puppetObject.AddComponent<PlaceholderPuppet>(); puppet.Configure(cityObject.transform);
            int before = puppet.Health.Value.Current;
            yield return new WaitForSeconds(.5f);
            Assert.That(puppet.Health.Value.Current, Is.LessThan(before));
            Object.Destroy(enemyObject); Object.Destroy(puppetObject); Object.Destroy(cityObject); yield return null;
        }

        [UnityTest]
        public IEnumerator HostileEnemyRetaliatesAgainstNearbyBehemoth()
        {
            var cityObject = new GameObject("TestBehemothCityTarget"); cityObject.transform.position = Vector3.right * 50f;
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var enemyObject = new GameObject("TestBehemothHostile"); enemyObject.AddComponent<HealthComponent>();
            var enemy = enemyObject.AddComponent<PlaceholderEnemy>(); enemy.Configure(cityHealth, cityObject.transform, EnemyCatalog.Gnawer, new ResourceInventory(100));
            var behemothObject = new GameObject("TestBehemoth"); behemothObject.transform.position = Vector3.right * .5f; behemothObject.AddComponent<HealthComponent>();
            var behemoth = behemothObject.AddComponent<PlaceholderBehemoth>(); behemoth.Configure(cityObject.transform, null);
            int before = behemoth.Health.Value.Current;
            yield return new WaitForSeconds(.5f);
            Assert.That(behemoth.Health.Value.Current, Is.LessThan(before));
            Object.Destroy(enemyObject); Object.Destroy(behemothObject); Object.Destroy(cityObject); yield return null;
        }

        [UnityTest]
        public IEnumerator PuppetAndBehemothReturnTowardSharedFixedRally()
        {
            var cityObject = new GameObject("SharedRallyCity"); cityObject.transform.position = Vector3.right * 50f;
            var commands = new FriendlyUnitCommandModel(); commands.SetRally(0f, 0f);
            var puppetObject = new GameObject("SharedRallyPuppet"); puppetObject.transform.position = Vector3.right * 4f; puppetObject.AddComponent<HealthComponent>();
            var puppet = puppetObject.AddComponent<PlaceholderPuppet>(); puppet.Configure(cityObject.transform, -1, null, commands);
            var behemothObject = new GameObject("SharedRallyBehemoth"); behemothObject.transform.position = Vector3.up * 4f; behemothObject.AddComponent<HealthComponent>();
            var behemoth = behemothObject.AddComponent<PlaceholderBehemoth>(); behemoth.Configure(cityObject.transform, null, -1, commands);

            yield return new WaitForSeconds(.25f);

            Assert.That(puppetObject.transform.position.x, Is.LessThan(4f));
            Assert.That(behemothObject.transform.position.y, Is.LessThan(4f));
            Assert.That(puppetObject.GetComponent<FriendlyUnitAgent>(), Is.Not.Null);
            Assert.That(behemothObject.GetComponent<FriendlyUnitAgent>(), Is.Not.Null);
            Object.Destroy(puppetObject); Object.Destroy(behemothObject); Object.Destroy(cityObject); yield return null;
        }

        [UnityTest]
        public IEnumerator ConvertedEnemyJoinsSharedRallyAndAttacksHostile()
        {
            var researchObject = new GameObject("SharedAgentResearch");
            var research = researchObject.AddComponent<ResearchController>();
            research.Model.Restore(new[] { "core.research.mind-control" }, null, 0f);
            research.enabled = false;
            var cityObject = new GameObject("SharedAgentCity"); cityObject.transform.position = Vector3.right * 50f;
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var commands = new FriendlyUnitCommandModel(); commands.SetRally(0f, 0f);
            var controlledObject = new GameObject("SharedAgentControlled"); controlledObject.transform.position = Vector3.right * 4f; controlledObject.AddComponent<HealthComponent>();
            var controlled = controlledObject.AddComponent<PlaceholderEnemy>();
            controlled.Configure(cityHealth, cityObject.transform, EnemyCatalog.Gnawer, new ResourceInventory(100), 0, null, EnemyQuality.Ordinary, research, null, commands);
            Assert.That(controlled.TryConvert(), Is.True);
            var hostileObject = new GameObject("SharedAgentHostile"); hostileObject.transform.position = Vector3.right * 4.5f; var hostileHealth = hostileObject.AddComponent<HealthComponent>();
            var hostile = hostileObject.AddComponent<PlaceholderEnemy>(); hostile.Configure(cityHealth, cityObject.transform, EnemyCatalog.Gnawer, new ResourceInventory(100));
            int before = hostileHealth.Value.Current;

            yield return new WaitForSeconds(.4f);

            Assert.That(controlledObject.GetComponent<FriendlyUnitAgent>(), Is.Not.Null);
            Assert.That(hostileHealth.Value.Current, Is.LessThan(before));
            Object.Destroy(controlledObject); Object.Destroy(hostileObject); Object.Destroy(cityObject); Object.Destroy(researchObject); yield return null;
        }

        [UnityTest]
        public IEnumerator SharedAgentRegeneratesEveryFriendlyKindAndCountsLossOnce()
        {
            var researchObject = new GameObject("SharedRegenResearch");
            var research = researchObject.AddComponent<ResearchController>();
            research.Model.Restore(new[] { "core.research.tissue-regeneration", "core.research.mind-control" }, null, 0f);
            research.enabled = false;
            var cityObject = new GameObject("SharedRegenCity");
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var commands = new FriendlyUnitCommandModel();
            var puppetObject = new GameObject("SharedRegenPuppet"); puppetObject.AddComponent<HealthComponent>();
            var puppet = puppetObject.AddComponent<PlaceholderPuppet>(); puppet.Configure(cityObject.transform, 100, research, commands);
            var behemothObject = new GameObject("SharedRegenBehemoth"); behemothObject.AddComponent<HealthComponent>();
            var behemoth = behemothObject.AddComponent<PlaceholderBehemoth>(); behemoth.Configure(cityObject.transform, research, 500, commands);
            var controlledObject = new GameObject("SharedRegenControlled"); controlledObject.AddComponent<HealthComponent>();
            var controlled = controlledObject.AddComponent<PlaceholderEnemy>();
            controlled.Configure(cityHealth, cityObject.transform, EnemyCatalog.Gnawer, new ResourceInventory(100), 0, null, EnemyQuality.Ordinary, research, null, commands);
            controlled.TryConvert(); controlled.Health.Value.Apply(20, DamageType.Physical, controlled.Health.Armor);

            yield return new WaitForSeconds(1.1f);

            Assert.That(puppet.Health.Value.Current, Is.GreaterThan(100));
            Assert.That(behemoth.Health.Value.Current, Is.GreaterThan(500));
            Assert.That(controlled.Health.Value.Current, Is.GreaterThan(EnemyCatalog.Gnawer.MaximumHealth - 20));
            puppet.Health.Value.Apply(1000, DamageType.Energy, puppet.Health.Armor);
            yield return null;
            Assert.That(commands.PuppetLosses, Is.EqualTo(1));
            Assert.That(commands.TotalLosses, Is.EqualTo(1));
            Object.Destroy(behemothObject); Object.Destroy(controlledObject); Object.Destroy(cityObject); Object.Destroy(researchObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_StatusTicksAndUsesReplaceableMarker()
        {
            var cityObject = new GameObject("InfectionTickCity");
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var enemy = CreateInfectionEnemy("InfectionTickEnemy", Vector2.zero, cityHealth, cityObject.transform, 250);

            enemy.ApplyInfection(1);

            Assert.That(enemy.Infection.Stacks, Is.EqualTo(1));
            Assert.That(enemy.Infection.Marker, Is.Not.Null);
            Assert.That(enemy.Infection.Marker.activeSelf, Is.True);
            Assert.That(enemy.Infection.Marker.GetComponent<VisualSlot>().StableId, Is.EqualTo("biological.status.infection"));
            yield return new WaitForSeconds(1.05f);
            Assert.That(enemy.Health.Value.Current, Is.EqualTo(245));
            Object.Destroy(enemy.gameObject); Object.Destroy(cityObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_BurstSpreadsOnlyToLivingHostilesInsideRadius()
        {
            var cityObject = new GameObject("InfectionSpreadCity");
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var researchObject = new GameObject("InfectionSpreadResearch");
            var research = researchObject.AddComponent<ResearchController>();
            research.Model.Restore(new[] { "core.research.mind-control" }, null, 0f);
            research.enabled = false;
            var source = CreateInfectionEnemy("InfectionSource", Vector2.zero, cityHealth, cityObject.transform);
            var inside = CreateInfectionEnemy("InfectionInside", Vector2.right * 3f, cityHealth, cityObject.transform);
            var outside = CreateInfectionEnemy("InfectionOutside", Vector2.right * 3.01f, cityHealth, cityObject.transform);
            var dead = CreateInfectionEnemy("InfectionDead", Vector2.up, cityHealth, cityObject.transform);
            var controlled = CreateInfectionEnemy("InfectionControlled", Vector2.down, cityHealth, cityObject.transform, 60, research);
            dead.Health.Value.Apply(1000, DamageType.Energy, dead.Health.Armor);
            Assert.That(controlled.TryConvert(), Is.True);
            source.ApplyInfection(9);

            source.ApplyInfection(1);

            Assert.That(source.Infection.Stacks, Is.Zero);
            Assert.That(inside.Infection.Stacks, Is.EqualTo(5));
            Assert.That(outside.Infection.Stacks, Is.Zero);
            Assert.That(dead.Infection.Stacks, Is.Zero);
            Assert.That(controlled.Infection.Stacks, Is.Zero);
            Object.Destroy(source.gameObject); Object.Destroy(inside.gameObject); Object.Destroy(outside.gameObject);
            Object.Destroy(dead.gameObject); Object.Destroy(controlled.gameObject); Object.Destroy(cityObject); Object.Destroy(researchObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_MindControlClearsStatusAndMarker()
        {
            var cityObject = new GameObject("InfectionControlCity");
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var researchObject = new GameObject("InfectionControlResearch");
            var research = researchObject.AddComponent<ResearchController>();
            research.Model.Restore(new[] { "core.research.mind-control" }, null, 0f);
            research.enabled = false;
            var enemy = CreateInfectionEnemy("InfectionControlEnemy", Vector2.zero, cityHealth, cityObject.transform, 60, research);
            enemy.ApplyInfection(4);
            GameObject marker = enemy.Infection.Marker;

            Assert.That(enemy.TryConvert(), Is.True);

            Assert.That(enemy.Infection.Stacks, Is.Zero);
            Assert.That(marker.activeSelf, Is.False);
            Object.Destroy(enemy.gameObject); Object.Destroy(cityObject); Object.Destroy(researchObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_PropagationChainTerminatesWithOneBurstPerTarget()
        {
            var cityObject = new GameObject("InfectionChainCity");
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var first = CreateInfectionEnemy("InfectionChainFirst", Vector2.zero, cityHealth, cityObject.transform);
            var second = CreateInfectionEnemy("InfectionChainSecond", Vector2.right, cityHealth, cityObject.transform);
            var third = CreateInfectionEnemy("InfectionChainThird", Vector2.right * 2f, cityHealth, cityObject.transform);
            first.ApplyInfection(9);
            second.ApplyInfection(5);
            third.ApplyInfection(5);

            first.ApplyInfection(1);

            Assert.That(first.Infection.Stacks, Is.EqualTo(9));
            Assert.That(second.Infection.Stacks, Is.EqualTo(5));
            Assert.That(third.Infection.Stacks, Is.EqualTo(5));
            Object.Destroy(first.gameObject); Object.Destroy(second.gameObject); Object.Destroy(third.gameObject); Object.Destroy(cityObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_SporeAndAcidTowersInfectButPhysicalTowerDoesNot()
        {
            var economyObject = new GameObject("InfectionTowerEconomy");
            var economy = economyObject.AddComponent<FormalEconomyController>();
            economy.Inventory.Add(ResourceIds.BiologicalWeapon, 4);
            var cityObject = new GameObject("InfectionTowerCity"); cityObject.transform.position = new Vector2(40f, 40f);
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var sporeTarget = CreateInfectionEnemy("SporeTowerTarget", new Vector2(1f, 0f), cityHealth, cityObject.transform, 250);
            var acidTarget = CreateInfectionEnemy("AcidTowerTarget", new Vector2(21f, 0f), cityHealth, cityObject.transform, 250);
            var physicalTarget = CreateInfectionEnemy("PhysicalTowerTarget", new Vector2(41f, 0f), cityHealth, cityObject.transform, 250);
            GameObject sporeTower = CreateInfectionTurret("SporeTower", Vector2.zero, BuildingCatalog.SporeTower, economy);
            GameObject acidTower = CreateInfectionTurret("AcidTower", new Vector2(20f, 0f), BuildingCatalog.AcidTower, economy);
            GameObject physicalTower = CreateInfectionTurret("PhysicalTower", new Vector2(40f, 0f), BuildingCatalog.MachineGunTurret, economy);

            yield return new WaitForSeconds(.2f);

            Assert.That(sporeTarget.Health.Value.Current, Is.LessThan(250));
            Assert.That(acidTarget.Health.Value.Current, Is.LessThan(250));
            Assert.That(physicalTarget.Health.Value.Current, Is.LessThan(250));
            Assert.That(sporeTarget.Infection.Stacks, Is.EqualTo(1));
            Assert.That(acidTarget.Infection.Stacks, Is.EqualTo(1));
            Assert.That(physicalTarget.Infection.Stacks, Is.Zero);
            Object.Destroy(sporeTarget.gameObject); Object.Destroy(acidTarget.gameObject); Object.Destroy(physicalTarget.gameObject);
            Object.Destroy(sporeTower); Object.Destroy(acidTower); Object.Destroy(physicalTower); Object.Destroy(cityObject); Object.Destroy(economyObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_ContinuousTowerDamageAddsAtMostOneLayerPerSecond()
        {
            var economyObject = new GameObject("InfectionCadenceEconomy");
            var economy = economyObject.AddComponent<FormalEconomyController>();
            economy.Inventory.Add(ResourceIds.BiologicalWeapon, 2);
            var cityObject = new GameObject("InfectionCadenceCity"); cityObject.transform.position = new Vector2(40f, 40f);
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var target = CreateInfectionEnemy("InfectionCadenceTarget", Vector2.right, cityHealth, cityObject.transform, 1000);
            GameObject tower = CreateInfectionTurret("InfectionCadenceTower", Vector2.zero, BuildingCatalog.SporeTower, economy);

            yield return new WaitForSeconds(.2f);
            Assert.That(target.Infection.Stacks, Is.EqualTo(1));
            yield return new WaitForSeconds(.5f);
            Assert.That(target.Infection.Stacks, Is.EqualTo(1));
            yield return new WaitForSeconds(.6f);
            Assert.That(target.Infection.Stacks, Is.EqualTo(2));
            Object.Destroy(target.gameObject); Object.Destroy(tower); Object.Destroy(cityObject); Object.Destroy(economyObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_NoBiologicalAmmunitionMeansNoDamageOrInfection()
        {
            var economyObject = new GameObject("InfectionEmptyEconomy");
            var economy = economyObject.AddComponent<FormalEconomyController>();
            Vector2 isolatedOrigin = new Vector2(100f, 100f);
            var cityObject = new GameObject("InfectionEmptyCity"); cityObject.transform.position = isolatedOrigin + new Vector2(40f, 40f);
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var target = CreateInfectionEnemy("InfectionEmptyTarget", isolatedOrigin + Vector2.right, cityHealth, cityObject.transform, 250);
            GameObject tower = CreateInfectionTurret("InfectionEmptyTower", isolatedOrigin, BuildingCatalog.SporeTower, economy);

            yield return new WaitForSeconds(.2f);

            Assert.That(target.Health.Value.Current, Is.EqualTo(250));
            Assert.That(target.Infection.Stacks, Is.Zero);
            Object.Destroy(target.gameObject); Object.Destroy(tower); Object.Destroy(cityObject); Object.Destroy(economyObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_BehemothDamageInfectsHostile()
        {
            var cityObject = new GameObject("InfectionBehemothCity"); cityObject.transform.position = new Vector2(40f, 40f);
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var target = CreateInfectionEnemy("InfectionBehemothTarget", Vector2.right, cityHealth, cityObject.transform, 250);
            var behemothObject = new GameObject("InfectionBehemoth");
            behemothObject.AddComponent<HealthComponent>();
            var behemoth = behemothObject.AddComponent<PlaceholderBehemoth>();
            var commands = new FriendlyUnitCommandModel(); commands.SetRally(0f, 0f);
            behemoth.Configure(cityObject.transform, null, -1, commands);

            yield return new WaitForSeconds(.2f);

            Assert.That(target.Health.Value.Current, Is.LessThan(250));
            Assert.That(target.Infection.Stacks, Is.EqualTo(1));
            Object.Destroy(target.gameObject); Object.Destroy(behemothObject); Object.Destroy(cityObject); yield return null;
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_SaveRestorePreservesHostileStatus()
        {
            SceneManager.LoadScene("FormalPrototype");
            yield return null;
            yield return null;
            var saves = Object.FindObjectOfType<FormalSaveController>();
            var data = saves.CaptureComplete();
            data.schema = 25;
            data.enemies = new[]
            {
                new EnemySnapshot
                {
                    archetype = (int)EnemyArchetype.Gnawer,
                    quality = (int)EnemyQuality.Ordinary,
                    health = 60,
                    x = 2f,
                    y = -1f,
                    infectionStacks = 7,
                    infectionElapsed = .4f
                }
            };

            Assert.That(saves.ApplyComplete(data, false), Is.True);
            var restoredEnemy = Object.FindObjectsOfType<PlaceholderEnemy>().Single(value => !value.IsControlled);

            Assert.That(restoredEnemy.Infection.Stacks, Is.EqualTo(7));
            Assert.That(restoredEnemy.Infection.Elapsed, Is.EqualTo(.4f).Within(.001f));
            EnemySnapshot captured = saves.CaptureComplete().enemies.Single(value => !value.controlled);
            Assert.That(captured.infectionStacks, Is.EqualTo(7));
            Assert.That(captured.infectionElapsed, Is.EqualTo(.4f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator BiologicalInfection_ControlledSnapshotRestoresWithoutInfection()
        {
            SceneManager.LoadScene("FormalPrototype");
            yield return null;
            yield return null;
            var saves = Object.FindObjectOfType<FormalSaveController>();
            var data = saves.CaptureComplete();
            data.schema = 25;
            data.completedResearchIds = new[] { "core.research.mind-control" };
            data.enemies = new[]
            {
                new EnemySnapshot
                {
                    archetype = (int)EnemyArchetype.Gnawer,
                    quality = (int)EnemyQuality.Ordinary,
                    health = 60,
                    controlled = true,
                    infectionStacks = 7,
                    infectionElapsed = .4f
                }
            };

            Assert.That(saves.ApplyComplete(data, false), Is.True);
            var restoredEnemy = Object.FindObjectsOfType<PlaceholderEnemy>().Single(value => value.IsControlled);

            Assert.That(restoredEnemy.Infection.Stacks, Is.Zero);
            Assert.That(restoredEnemy.Infection.Elapsed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TechnologyOverload_RequiresEnergyWeaponsAndUsesStableMarker()
        {
            var city = new GameObject("TechnologyOverloadCity");
            var researchObject = new GameObject("TechnologyOverloadResearch");
            var research = researchObject.AddComponent<ResearchController>();
            research.enabled = false;
            var controllerObject = new GameObject("TechnologyOverloadController");
            var controller = controllerObject.AddComponent<FormalTechnologyRouteController>();
            controller.Configure(research, null, city.transform);

            Assert.That(controller.TryActivate(), Is.False);
            research.Model.Restore(new[] { "core.research.energy-weapons" }, null, 0f);
            Assert.That(controller.TryActivate(), Is.True);

            Assert.That(controller.FireRateMultiplier, Is.EqualTo(2f));
            Assert.That(controller.DamageMultiplier(DamageType.Energy), Is.EqualTo(1.3f));
            Assert.That(controller.DamageMultiplier(DamageType.Physical), Is.EqualTo(1f));
            Assert.That(controller.Marker, Is.Not.Null);
            Assert.That(controller.Marker.activeSelf, Is.True);
            Assert.That(controller.Marker.GetComponent<VisualSlot>().StableId, Is.EqualTo("technology.status.overload"));
            Object.Destroy(controllerObject); Object.Destroy(researchObject); Object.Destroy(city); yield return null;
        }

        [UnityTest]
        public IEnumerator TechnologyOverload_ComposesWithLeaderWithoutMultiplicationAndEitherLockoutStops()
        {
            var city = new GameObject("TechnologyCompositionCity");
            var researchObject = new GameObject("TechnologyCompositionResearch");
            var research = researchObject.AddComponent<ResearchController>();
            research.Model.Restore(new[] { "core.research.energy-weapons" }, null, 0f);
            research.enabled = false;
            var leaderObject = new GameObject("TechnologyCompositionLeader");
            var leader = leaderObject.AddComponent<WasteCity.Leader.FormalLeaderController>();
            leader.enabled = false;
            leader.Model.Recruit(true);
            leader.Model.Overload.TryActivate();
            var controllerObject = new GameObject("TechnologyCompositionController");
            var controller = controllerObject.AddComponent<FormalTechnologyRouteController>();
            controller.Configure(research, leader, city.transform);

            controller.TryActivate();

            Assert.That(controller.FireRateMultiplier, Is.EqualTo(2f));
            controller.Model.Tick(5f);
            Assert.That(controller.FireRateMultiplier, Is.Zero);
            Object.Destroy(controllerObject); Object.Destroy(leaderObject); Object.Destroy(researchObject); Object.Destroy(city); yield return null;
        }

        [UnityTest]
        public IEnumerator TechnologyOverload_EnergyTurretUsesAttackRateAndDamageBoost()
        {
            Vector2 origin = new Vector2(140f, 140f);
            var economyObject = new GameObject("TechnologyTurretEconomy");
            var economy = economyObject.AddComponent<FormalEconomyController>();
            economy.Inventory.Add(ResourceIds.EnergyCrystal, 10);
            var cityObject = new GameObject("TechnologyTurretCity"); cityObject.transform.position = origin + new Vector2(50f, 50f);
            var cityHealth = cityObject.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var boostedTarget = CreateInfectionEnemy("TechnologyBoostedTarget", origin + Vector2.right, cityHealth, cityObject.transform, 1000);
            var baselineTarget = CreateInfectionEnemy("TechnologyBaselineTarget", origin + new Vector2(31f, 0f), cityHealth, cityObject.transform, 1000);
            var researchObject = new GameObject("TechnologyTurretResearch");
            var research = researchObject.AddComponent<ResearchController>();
            research.Model.Restore(new[] { "core.research.energy-weapons" }, null, 0f);
            research.enabled = false;
            var controllerObject = new GameObject("TechnologyTurretController");
            var controller = controllerObject.AddComponent<FormalTechnologyRouteController>();
            controller.Configure(research, null, cityObject.transform);
            controller.TryActivate();
            GameObject boostedTower = CreateInfectionTurret("TechnologyBoostedTower", origin, BuildingCatalog.LaserTower, economy, controller, research);
            GameObject baselineTower = CreateInfectionTurret("TechnologyBaselineTower", origin + new Vector2(30f, 0f), BuildingCatalog.LaserTower, economy);

            yield return new WaitForSeconds(.5f);

            int boostedDamage = 1000 - boostedTarget.Health.Value.Current;
            int baselineDamage = 1000 - baselineTarget.Health.Value.Current;
            Assert.That(baselineDamage, Is.GreaterThan(0));
            Assert.That(boostedDamage, Is.GreaterThan(baselineDamage * 2.3f));
            Assert.That(boostedDamage, Is.LessThan(baselineDamage * 2.9f));
            Object.Destroy(boostedTarget.gameObject); Object.Destroy(baselineTarget.gameObject);
            Object.Destroy(boostedTower); Object.Destroy(baselineTower); Object.Destroy(controllerObject);
            Object.Destroy(researchObject); Object.Destroy(cityObject); Object.Destroy(economyObject); yield return null;
        }

        private static GameObject CreateInfectionTurret(
            string name,
            Vector2 position,
            BuildingDefinition definition,
            FormalEconomyController economy,
            ITurretCombatModifierSource modifier = null,
            ResearchController research = null)
        {
            var item = new GameObject(name);
            item.transform.position = position;
            item.AddComponent<HealthComponent>();
            var runtime = item.AddComponent<BuildingRuntime>();
            runtime.Configure(definition, economy);
            var turret = item.AddComponent<PlaceholderTurret>();
            turret.Configure(economy, runtime, null, modifier, research);
            return item;
        }

        private static PlaceholderEnemy CreateInfectionEnemy(
            string name,
            Vector2 position,
            HealthComponent cityHealth,
            Transform city,
            int maximumHealth = 60,
            ResearchController research = null)
        {
            var definition = new EnemyDefinition(
                $"test.enemy.{name.ToLowerInvariant()}",
                name,
                EnemyArchetype.Gnawer,
                maximumHealth,
                .1f,
                0f,
                .5f,
                ArmorType.Light,
                0,
                EnemyTargetPriority.Nearest);
            var item = new GameObject(name);
            item.transform.position = position;
            item.AddComponent<HealthComponent>();
            var enemy = item.AddComponent<PlaceholderEnemy>();
            enemy.Configure(cityHealth, city, definition, new ResourceInventory(100), 0, null, EnemyQuality.Ordinary, research);
            return enemy;
        }
    }
}
