using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class SceneContractTests
    {
        [Test]
        public void FormalPrototypeHasReplaceablePlaceholderWorldAndCity()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/FormalPrototype.unity");
            Assert.That(Object.FindObjectOfType<FormalGameBootstrap>(), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<PlaceholderWorldView>(), Is.Not.Null);
            Assert.That(Object.FindObjectOfType<PlaceholderMobileCity>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }

        [Test]
        public void FormalPrototypeMainCameraHasCompleteCameraControlWiring()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/FormalPrototype.unity");

            var controller = Object.FindObjectOfType<FormalCameraController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.gameObject, Is.EqualTo(Camera.main.gameObject));
            Assert.That(controller.ReferencesReady, Is.True);
        }
    }
}
