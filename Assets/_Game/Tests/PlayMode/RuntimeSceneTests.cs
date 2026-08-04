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
    }
}
