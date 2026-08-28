using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxCivilizationExpansionUiInputTests
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
            root = new GameObject("IDEA0022.UI.Input.Tests");
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (inputFixture != null)
                inputFixture.GetType().GetMethod("TearDown")
                    .Invoke(inputFixture, null);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void IDEA0022_ViewAppliesReadableThreeActionPresentation()
        {
            GrayboxCivilizationExpansionView3D view = CreateView();
            view.Apply(new GrayboxCivilizationExpansionPresentation3D(
                "军队与远征",
                "默认小队 4/12",
                "战斗傀儡 2",
                "守卫主城",
                true,
                "跟随领袖",
                true,
                "派出远征",
                false));
            view.Open(GrayboxCivilizationExpansionPage3D.Army);

            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.HeadingText.text, Is.EqualTo("军队与远征"));
            Assert.That(view.SummaryText.text, Does.Contain("4/12"));
            Assert.That(view.DetailsText.text, Does.Contain("战斗傀儡"));
            Assert.That(view.PrimaryButton.interactable, Is.True);
            Assert.That(view.TertiaryButton.interactable, Is.False);
        }

        [Test]
        public void IDEA0022_RealMNPInputOpensMutuallyExclusivePagesAndEscapeCloses()
        {
            GrayboxCivilizationExpansionView3D view = CreateView();
            var coordinator = root.AddComponent<
                GrayboxUsabilityInputCoordinator3D>();
            coordinator.ConfigureCivilizationExpansion(view);

            Tap(Key.M);
            coordinator.ProcessCurrentInput();
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Army));
            Release();

            Tap(Key.N);
            coordinator.ProcessCurrentInput();
            Assert.That(view.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.World));
            Release();

            Tap(Key.P);
            coordinator.ProcessCurrentInput();
            Assert.That(view.Page,
                Is.EqualTo(GrayboxCivilizationExpansionPage3D.Politics));
            Release();

            Tap(Key.Escape);
            coordinator.ProcessCurrentInput();
            Assert.That(view.IsOpen, Is.False);
        }

        private GrayboxCivilizationExpansionView3D CreateView()
        {
            var canvasObject = new GameObject(
                "Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            var view = canvasObject.AddComponent<
                GrayboxCivilizationExpansionView3D>();
            view.Configure(canvas);
            return view;
        }

        private void Tap(Key key)
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
        }

        private void Release()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
    }
}
