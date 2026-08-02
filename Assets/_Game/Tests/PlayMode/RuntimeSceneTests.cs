using System.Collections;
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

namespace WasteCity.Tests.PlayMode
{
    public sealed class RuntimeSceneTests
    {
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
    }
}
