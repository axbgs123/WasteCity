using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WasteCity.Core;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class FormalGameSpeedCommandFacadeTests
    {
        private const string FacadeTypeName =
            "WasteCity.Graybox3D.Usability." +
            "GrayboxGameSpeedCommandFacade3D, " +
            "WasteCity.Graybox3D.Usability";
        private const float Tolerance = .0001f;

        private readonly List<GameObject> createdObjects =
            new List<GameObject>();
        private Keyboard keyboard;
        private object inputFixture;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            }
            createdObjects.Clear();
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (inputFixture != null)
            {
                inputFixture.GetType().GetMethod("TearDown")
                    .Invoke(inputFixture, null);
                inputFixture = null;
            }
            Time.timeScale = 1f;
        }

        [Test]
        public void FormalFacadeNormalizesToZeroOneTwoWithoutRemovingHalfSpeed()
        {
            var legacy = new GameSpeedModel();
            legacy.Set(.5f);
            Assert.That(legacy.RequestedSpeed, Is.EqualTo(.5f),
                "IDEA-0017 must not silently remove the existing .5 speed " +
                "compatibility contract from the low-level model.");

            object commands = CreateCommands(legacy);
            int[] requests = { -7, 0, 1, 2, 9 };
            float[] expected = { 0f, 0f, 1f, 2f, 2f };
            for (var index = 0; index < requests.Length; index++)
            {
                RequestSpeed(commands, requests[index]);
                Assert.That(
                    ReadFloat(commands, "RequestedSpeed"),
                    Is.EqualTo(expected[index]).Within(Tolerance),
                    "Formal speed commands must normalize before commit.");
                Assert.That(
                    ReadFloat(commands, "RequestedSpeed"),
                    Is.EqualTo(0f).Or.EqualTo(1f).Or.EqualTo(2f));
            }
        }

        [Test]
        public void TacticalPauseRestoresTheLastNonZeroFormalSpeed()
        {
            object commands = CreateCommands(new GameSpeedModel());

            RequestSpeed(commands, 2);
            Invoke(commands, "ToggleTacticalPause");

            Assert.That(ReadFloat(commands, "RequestedSpeed"), Is.Zero);
            Assert.That(ReadFloat(commands, "EffectiveSpeed"), Is.Zero);
            Assert.That(ReadFloat(commands, "LastNonZeroSpeed"),
                Is.EqualTo(2f));

            Invoke(commands, "ToggleTacticalPause");

            Assert.That(ReadFloat(commands, "RequestedSpeed"),
                Is.EqualTo(2f));
            Assert.That(ReadFloat(commands, "EffectiveSpeed"),
                Is.EqualTo(2f));
            Assert.That(ReadFloat(commands, "LastNonZeroSpeed"),
                Is.EqualTo(2f));

            RequestSpeed(commands, 1);
            Invoke(commands, "ToggleTacticalPause");
            Invoke(commands, "ToggleTacticalPause");
            Assert.That(ReadFloat(commands, "RequestedSpeed"),
                Is.EqualTo(1f));
            Assert.That(ReadFloat(commands, "LastNonZeroSpeed"),
                Is.EqualTo(1f));
        }

        [Test]
        public void FormalRuleDeltaUsesUnscaledTimeAndAppliesSpeedExactlyOnce()
        {
            object commands = CreateCommands(new GameSpeedModel());
            RequestSpeed(commands, 2);
            Time.timeScale = 2f;

            float ruleDelta = (float)Invoke(
                commands,
                "ResolveRuleDelta",
                .25f);

            Assert.That(ruleDelta, Is.EqualTo(.5f).Within(Tolerance),
                "The formal rule delta must not multiply both the unscaled " +
                "input and Time.timeScale.");
        }

        [Test]
        public void DigitOneTwoAndSpaceUseTheFormalInputMainLoop()
        {
            CoordinatorFixture fixture = CreateCoordinator();

            Press(fixture.Coordinator, Key.Digit2);
            Assert.That(fixture.Speed.Speed, Is.EqualTo(2f));
            Assert.That(Time.timeScale, Is.EqualTo(2f));

            Press(fixture.Coordinator, Key.Space);
            Assert.That(fixture.Speed.Speed, Is.Zero);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(ReadControllerCommandsFloat(
                    fixture.Menu,
                    "LastNonZeroSpeed"),
                Is.EqualTo(2f));

            Press(fixture.Coordinator, Key.Space);
            Assert.That(fixture.Speed.Speed, Is.EqualTo(2f));
            Assert.That(Time.timeScale, Is.EqualTo(2f));

            Press(fixture.Coordinator, Key.Digit1);
            Assert.That(fixture.Speed.Speed, Is.EqualTo(1f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(ReadControllerCommandsFloat(
                    fixture.Menu,
                    "LastNonZeroSpeed"),
                Is.EqualTo(1f));
        }

        [Test]
        public void ModalInputConsumesDigitBeforeFormalSpeedCommands()
        {
            var modal = new FakeDevelopmentPanel { IsOpen = true };
            CoordinatorFixture fixture = CreateCoordinator(modal);
            fixture.Speed.Set(1f);
            Time.timeScale = 1f;

            GrayboxInputSuppression suppression = Press(
                fixture.Coordinator,
                Key.Digit2);

            AssertSuppressed(suppression);
            Assert.That(fixture.Speed.Speed, Is.EqualTo(1f),
                "A higher-priority modal must consume numeric input before " +
                "the formal speed command sees it.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            modal.IsOpen = false;
            Press(fixture.Coordinator, Key.Digit2);
            Assert.That(fixture.Speed.Speed, Is.EqualTo(2f));
            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        [Test]
        public void RuntimeServiceReconfigureClosesOpenMenuAndMirrorsNewSpeed()
        {
            var firstSpeed = new GameSpeedModel();
            var secondSpeed = new GameSpeedModel();
            secondSpeed.Set(2f);
            var settings = new GrayboxDisplaySettingsModel3D(
                new FakePlatform(),
                new FakeStore());
            GrayboxSystemMenuController3D menu = CreateObject(
                    "FormalSpeedReconfigureMenu")
                .AddComponent<GrayboxSystemMenuController3D>();
            menu.Configure(firstSpeed, settings, new FakeExit());
            menu.Open();
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(firstSpeed.IsPaused(GamePauseReason.SystemMenu),
                Is.True);

            menu.ConfigureRuntimeServices(secondSpeed, new FakeExit());

            Assert.That(menu.IsOpen, Is.False,
                "A runtime rebind cannot leave an unpaused modal visible.");
            Assert.That(firstSpeed.IsPaused(GamePauseReason.SystemMenu),
                Is.False);
            Assert.That(secondSpeed.IsPaused(GamePauseReason.SystemMenu),
                Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        private object CreateCommands(GameSpeedModel model)
        {
            Type type = Type.GetType(FacadeTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Missing IDEA-0017 formal speed command facade: " +
                FacadeTypeName + ".");
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(GameSpeedModel),
            });
            Assert.That(constructor, Is.Not.Null,
                "The formal speed command facade requires one shared " +
                "GameSpeedModel.");
            return constructor.Invoke(new object[] { model });
        }

        private static void RequestSpeed(object commands, int requested)
        {
            Invoke(commands, "RequestSpeed", requested);
        }

        private static object Invoke(
            object owner,
            string methodName,
            params object[] arguments)
        {
            Type[] parameterTypes = new Type[arguments.Length];
            for (var index = 0; index < arguments.Length; index++)
                parameterTypes[index] = arguments[index].GetType();
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null,
                owner.GetType().Name + " must expose " + methodName + ".");
            return method.Invoke(owner, arguments);
        }

        private static float ReadFloat(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                owner.GetType().Name + " must expose " + propertyName + ".");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(float)));
            return (float)property.GetValue(owner);
        }

        private static float ReadControllerCommandsFloat(
            GrayboxSystemMenuController3D menu,
            string propertyName)
        {
            PropertyInfo commandsProperty = menu.GetType().GetProperty(
                "SpeedCommands",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(commandsProperty, Is.Not.Null,
                "The system menu and keyboard main loop must share the same " +
                "formal speed command facade.");
            object commands = commandsProperty.GetValue(menu);
            Assert.That(commands, Is.Not.Null);
            Assert.That(commands.GetType().AssemblyQualifiedName,
                Does.StartWith(
                    "WasteCity.Graybox3D.Usability." +
                    "GrayboxGameSpeedCommandFacade3D,"));
            return ReadFloat(commands, propertyName);
        }

        private CoordinatorFixture CreateCoordinator(
            IGrayboxDevelopmentPanelControl3D development = null)
        {
            var speed = new GameSpeedModel();
            var settings = new GrayboxDisplaySettingsModel3D(
                new FakePlatform(),
                new FakeStore());
            GrayboxSystemMenuController3D menu = CreateObject(
                    "FormalSpeedSystemMenu")
                .AddComponent<GrayboxSystemMenuController3D>();
            menu.Configure(speed, settings, new FakeExit());
            GrayboxBuildingInputRouter3D buildingInput = CreateObject(
                    "FormalSpeedBuildingInput")
                .AddComponent<GrayboxBuildingInputRouter3D>();
            GrayboxUsabilityInputCoordinator3D coordinator = CreateObject(
                    "FormalSpeedInputCoordinator")
                .AddComponent<GrayboxUsabilityInputCoordinator3D>();
            coordinator.Configure(
                buildingInput,
                menu,
                development ?? new FakeDevelopmentPanel());
            return new CoordinatorFixture(coordinator, menu, speed);
        }

        private GrayboxInputSuppression Press(
            GrayboxUsabilityInputCoordinator3D coordinator,
            Key key)
        {
            EnsureKeyboard();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            Assert.That(
                keyboard[key].wasPressedThisFrame,
                Is.True,
                key.ToString());
            GrayboxInputSuppression result =
                coordinator.ProcessCurrentInput();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            return result;
        }

        private void EnsureKeyboard()
        {
            if (inputFixture == null)
            {
                Type fixtureType = Type.GetType(
                    "UnityEngine.InputSystem.InputTestFixture, " +
                    "Unity.InputSystem.TestFramework",
                    throwOnError: true);
                inputFixture = Activator.CreateInstance(fixtureType);
                fixtureType.GetMethod("Setup").Invoke(inputFixture, null);
            }
            if (keyboard == null)
                keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
        }

        private static void AssertSuppressed(
            GrayboxInputSuppression suppression)
        {
            Assert.That(suppression.Move, Is.True);
            Assert.That(suppression.Deployment, Is.True);
            Assert.That(suppression.Destination, Is.True);
            Assert.That(suppression.CameraDrag, Is.True);
            Assert.That(suppression.Home, Is.True);
        }

        private GameObject CreateObject(string name)
        {
            var result = new GameObject(name);
            createdObjects.Add(result);
            return result;
        }

        private sealed class CoordinatorFixture
        {
            public CoordinatorFixture(
                GrayboxUsabilityInputCoordinator3D coordinator,
                GrayboxSystemMenuController3D menu,
                GameSpeedModel speed)
            {
                Coordinator = coordinator;
                Menu = menu;
                Speed = speed;
            }

            public GrayboxUsabilityInputCoordinator3D Coordinator { get; }
            public GrayboxSystemMenuController3D Menu { get; }
            public GameSpeedModel Speed { get; }
        }

        private sealed class FakeDevelopmentPanel :
            IGrayboxDevelopmentPanelControl3D
        {
            public bool IsOpen { get; set; }

            public void Close()
            {
                IsOpen = false;
            }
        }

        private sealed class FakeExit : IGrayboxApplicationExit
        {
            public void Exit()
            {
            }
        }

        private sealed class FakeStore : IGrayboxDisplaySettingsStore
        {
            public bool TryLoad(
                out int version,
                out GrayboxDisplaySettings3D settings)
            {
                version = 0;
                settings = default;
                return false;
            }

            public void Save(int version, GrayboxDisplaySettings3D settings)
            {
            }
        }

        private sealed class FakePlatform : IGrayboxDisplaySettingsPlatform
        {
            private static readonly GrayboxDisplayResolution3D[] Resolutions =
            {
                new GrayboxDisplayResolution3D(1600, 900),
            };

            public IReadOnlyList<GrayboxDisplayResolution3D>
                AvailableResolutions => Resolutions;

            public GrayboxDisplaySettings3D Current =>
                new GrayboxDisplaySettings3D(
                    1600,
                    900,
                    GrayboxWindowMode3D.Windowed);

            public bool TryApply(GrayboxDisplaySettings3D settings)
            {
                return true;
            }
        }
    }
}
