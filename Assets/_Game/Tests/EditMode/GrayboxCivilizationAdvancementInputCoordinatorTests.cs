using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxCivilizationAdvancementInputCoordinatorTests
    {
        private GameObject root;
        private Keyboard keyboard;
        private object inputFixture;

        [SetUp]
        public void SetUp()
        {
            Type fixtureType = Type.GetType(
                "UnityEngine.InputSystem.InputTestFixture, " +
                "Unity.InputSystem.TestFramework",
                throwOnError: true);
            inputFixture = Activator.CreateInstance(fixtureType);
            fixtureType.GetMethod("Setup").Invoke(inputFixture, null);
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            root = new GameObject("Advancement.InputCoordinator.Test");
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (inputFixture != null)
            {
                inputFixture.GetType().GetMethod("TearDown")
                    .Invoke(inputFixture, null);
                inputFixture = null;
            }
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void IDEA0020_RealUUsesAdvanceCallbackAndResultsUsesContinue()
        {
            var coordinator = root.AddComponent<
                GrayboxUsabilityInputCoordinator3D>();
            var canvasObject = new GameObject(
                "Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            var view = canvasObject.AddComponent<
                GrayboxCivilizationAdvancementView3D>();
            view.Configure(canvas);
            int advances = 0;
            int continues = 0;
            coordinator.ConfigureAdvancement(
                view,
                () => { advances++; return true; },
                () => { continues++; return true; });

            Tap(Key.U);
            coordinator.ProcessCurrentInput();
            Assert.That(advances, Is.EqualTo(1));
            Assert.That(continues, Is.Zero);
            ReleaseKeys();

            view.Open();
            SetContinueVisible(view);
            Tap(Key.U);
            coordinator.ProcessCurrentInput();
            Assert.That(advances, Is.EqualTo(1));
            Assert.That(continues, Is.EqualTo(1));
            ReleaseKeys();
        }

        [Test]
        public void IDEA0020_ExistingModalAndFocusBranchesPrecedeUDispatch()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Usability/" +
                "GrayboxUsabilityInputCoordinator3D.cs"));
            int dispatch = source.IndexOf(
                "bool advancementRequested",
                StringComparison.Ordinal);
            Assert.That(dispatch, Is.GreaterThan(0));
            AssertPrecedes(source, "systemMenu.IsOpen", dispatch);
            AssertPrecedes(source, "fateSelectionView.IsOpen", dispatch);
            AssertPrecedes(source, "fateOperationsView.IsOpen", dispatch);
            AssertPrecedes(source, "progressionView.IsDetailsOpen", dispatch);
            AssertPrecedes(source, "HasActiveTextInputFocus()", dispatch);
            AssertPrecedes(source, "buildingInput.HasKeyboardFocus", dispatch);
            AssertPrecedes(source, "operations.IsResearchOpen", dispatch);
            AssertPrecedes(source, "operations.IsAnyPanelOpen", dispatch);
            StringAssert.Contains(
                "buildingInput.IsBuildInteractionActive",
                source);
        }

        [Test]
        public void IDEA0020_OpenAdvancementConsumesEscapeBeforeWorldInput()
        {
            var coordinator = root.AddComponent<
                GrayboxUsabilityInputCoordinator3D>();
            var canvasObject = new GameObject(
                "Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            var view = canvasObject.AddComponent<
                GrayboxCivilizationAdvancementView3D>();
            view.Configure(canvas);
            view.Open();
            coordinator.ConfigureAdvancement(view, () => true, () => true);

            Tap(Key.Escape);
            WasteCity.Graybox3D.GrayboxInputSuppression suppression =
                coordinator.ProcessCurrentInput();

            Assert.That(suppression.Move, Is.True);
            Assert.That(suppression.Deployment, Is.True);
            Assert.That(suppression.Destination, Is.True);
            Assert.That(view.IsOpen, Is.True,
                "Escape is reserved for the higher-priority system menu; " +
                "the advancement presentation stays open underneath.");
        }

        private void Tap(Key key)
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True, key.ToString());
        }

        private void ReleaseKeys()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        private static void SetContinueVisible(
            GrayboxCivilizationAdvancementView3D view)
        {
            view.AdvanceButton.gameObject.SetActive(false);
            view.ContinueButton.gameObject.SetActive(true);
        }

        private static void AssertPrecedes(
            string source,
            string token,
            int dispatch)
        {
            int index = source.IndexOf(token, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), token);
            Assert.That(index, Is.LessThan(dispatch), token);
        }

        private static string ProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relative));
        }
    }
}
