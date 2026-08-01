using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Persistence;

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
            Assert.That(buildings.transform.parent, Is.EqualTo(city.transform)); Assert.That(Camera.main, Is.Not.Null);
        }
    }
}
