using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Core;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxUsabilityTests
    {
        private readonly List<GameObject> createdObjects =
            new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            DeleteApprovedPlayerPrefsKeys();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = createdObjects.Count - 1; index >= 0; index--)
                if (createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(
                        createdObjects[index]);
            createdObjects.Clear();
            Time.timeScale = 1f;
            DeleteApprovedPlayerPrefsKeys();
        }

        [Test]
        public void IDEA0007_DisplaySettingsExposeOnlyApprovedWindowModes()
        {
            Assert.That(
                Enum.GetValues(typeof(GrayboxWindowMode3D)),
                Is.EqualTo(new[]
                {
                    GrayboxWindowMode3D.Windowed,
                    GrayboxWindowMode3D.FullScreenWindow
                }));
        }

        [Test]
        public void IDEA0007_ResolutionsArePositiveUniqueSortedAndIncludeCurrent()
        {
            var platform = new FakePlatform(
                Setting(1600, 900, GrayboxWindowMode3D.Windowed),
                Resolution(1920, 1080),
                Resolution(1280, 720),
                Resolution(1920, 1080),
                Resolution(0, 1080),
                Resolution(-1, 720));

            var model = new GrayboxDisplaySettingsModel3D(
                platform,
                new FakeStore());

            Assert.That(
                model.AvailableResolutions,
                Is.EqualTo(new[]
                {
                    Resolution(1280, 720),
                    Resolution(1600, 900),
                    Resolution(1920, 1080)
                }));
        }

        [Test]
        public void IDEA0007_LoadAcceptsOnlyVersionOneSupportedSettings()
        {
            GrayboxDisplaySettings3D stored = Setting(
                1280,
                720,
                GrayboxWindowMode3D.FullScreenWindow);
            var store = new FakeStore(true, 1, stored);
            var model = new GrayboxDisplaySettingsModel3D(
                StandardPlatform(),
                store);

            Assert.That(model.LastApplied, Is.EqualTo(stored));
            Assert.That(model.Staged, Is.EqualTo(stored));
            Assert.That(store.LoadCount, Is.EqualTo(1));
        }

        [TestCase(0, 1280, 720, (int)GrayboxWindowMode3D.Windowed)]
        [TestCase(2, 1280, 720, (int)GrayboxWindowMode3D.Windowed)]
        [TestCase(1, 0, 720, (int)GrayboxWindowMode3D.Windowed)]
        [TestCase(1, 1280, -1, (int)GrayboxWindowMode3D.Windowed)]
        [TestCase(1, 1111, 777, (int)GrayboxWindowMode3D.Windowed)]
        [TestCase(1, 1280, 720, 17)]
        public void IDEA0007_CorruptUnknownOrUnsupportedLoadFallsBackToPlatform(
            int version,
            int width,
            int height,
            int rawMode)
        {
            GrayboxDisplaySettings3D current = Setting(
                1600,
                900,
                GrayboxWindowMode3D.Windowed);
            var store = new FakeStore(
                true,
                version,
                new GrayboxDisplaySettings3D(
                    width,
                    height,
                    (GrayboxWindowMode3D)rawMode));

            var model = new GrayboxDisplaySettingsModel3D(
                StandardPlatform(current),
                store);

            Assert.That(model.LastApplied, Is.EqualTo(current));
            Assert.That(model.Staged, Is.EqualTo(current));
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void IDEA0007_StageDoesNotTouchPlatformOrStore()
        {
            FakePlatform platform = StandardPlatform();
            var store = new FakeStore();
            var model = new GrayboxDisplaySettingsModel3D(platform, store);

            model.StageResolution(Resolution(1280, 720));
            model.StageWindowMode(GrayboxWindowMode3D.FullScreenWindow);

            Assert.That(
                model.Staged,
                Is.EqualTo(Setting(
                    1280,
                    720,
                    GrayboxWindowMode3D.FullScreenWindow)));
            Assert.That(platform.ApplyCount, Is.Zero);
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void IDEA0007_ApplyCallsPlatformThenPersistsExactValueOnce()
        {
            var events = new List<string>();
            var platform = new FakePlatform(
                Setting(1600, 900, GrayboxWindowMode3D.Windowed),
                events,
                Resolution(1280, 720),
                Resolution(1600, 900),
                Resolution(1920, 1080));
            var store = new FakeStore(events);
            var model = new GrayboxDisplaySettingsModel3D(platform, store);
            GrayboxDisplaySettings3D expected = Setting(
                1280,
                720,
                GrayboxWindowMode3D.FullScreenWindow);
            model.StageResolution(Resolution(expected.Width, expected.Height));
            model.StageWindowMode(expected.WindowMode);

            Assert.That(model.Apply(), Is.True);

            Assert.That(platform.ApplyCount, Is.EqualTo(1));
            Assert.That(platform.LastApplied, Is.EqualTo(expected));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.LastSavedVersion, Is.EqualTo(1));
            Assert.That(store.LastSaved, Is.EqualTo(expected));
            Assert.That(model.LastApplied, Is.EqualTo(expected));
            Assert.That(model.Staged, Is.EqualTo(expected));
            Assert.That(events, Is.EqualTo(new[] { "apply", "save" }));
        }

        [Test]
        public void IDEA0007_PlatformFailureWritesNothingAndPreservesAppliedState()
        {
            FakePlatform platform = StandardPlatform();
            platform.ApplySucceeds = false;
            var store = new FakeStore();
            var model = new GrayboxDisplaySettingsModel3D(platform, store);
            GrayboxDisplaySettings3D before = model.LastApplied;
            model.StageResolution(Resolution(1280, 720));

            Assert.That(model.Apply(), Is.False);

            Assert.That(platform.ApplyCount, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(model.LastApplied, Is.EqualTo(before));
            Assert.That(model.Staged, Is.Not.EqualTo(before));
        }

        [Test]
        public void IDEA0007_CancelRestoresStagedWithoutApplying()
        {
            FakePlatform platform = StandardPlatform();
            var store = new FakeStore();
            var model = new GrayboxDisplaySettingsModel3D(platform, store);
            GrayboxDisplaySettings3D applied = model.LastApplied;
            model.StageResolution(Resolution(1280, 720));
            model.StageWindowMode(GrayboxWindowMode3D.FullScreenWindow);

            model.Cancel();

            Assert.That(model.Staged, Is.EqualTo(applied));
            Assert.That(platform.ApplyCount, Is.Zero);
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void IDEA0007_RestoreDefaultsStagesPreferredValueWithoutApplying()
        {
            FakePlatform platform = StandardPlatform();
            var store = new FakeStore();
            var model = new GrayboxDisplaySettingsModel3D(platform, store);

            model.RestoreDefaults();

            Assert.That(
                model.Staged,
                Is.EqualTo(Setting(
                    1920,
                    1080,
                    GrayboxWindowMode3D.FullScreenWindow)));
            Assert.That(platform.ApplyCount, Is.Zero);
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void IDEA0007_DefaultUsesDeterministicNearestSupportedFallback()
        {
            var platform = new FakePlatform(
                Setting(2560, 1440, GrayboxWindowMode3D.Windowed),
                Resolution(1280, 720),
                Resolution(1600, 900),
                Resolution(1680, 1050),
                Resolution(2560, 1440));
            var model = new GrayboxDisplaySettingsModel3D(
                platform,
                new FakeStore());

            model.RestoreDefaults();

            Assert.That(
                model.Staged,
                Is.EqualTo(Setting(
                    1680,
                    1050,
                    GrayboxWindowMode3D.FullScreenWindow)));
        }

        [Test]
        public void IDEA0007_HeadlessResolutionListUsesCurrentValidValue()
        {
            GrayboxDisplaySettings3D current = Setting(
                1024,
                768,
                GrayboxWindowMode3D.Windowed);
            var model = new GrayboxDisplaySettingsModel3D(
                new FakePlatform(current),
                new FakeStore());

            model.RestoreDefaults();

            Assert.That(
                model.AvailableResolutions,
                Is.EqualTo(new[] { Resolution(1024, 768) }));
            Assert.That(
                model.Staged,
                Is.EqualTo(Setting(
                    1024,
                    768,
                    GrayboxWindowMode3D.FullScreenWindow)));
        }

        [Test]
        public void IDEA0007_PlayerPrefsAdapterUsesExactFourApprovedKeys()
        {
            var store = new PlayerPrefsGrayboxDisplaySettingsStore3D();
            GrayboxDisplaySettings3D expected = Setting(
                1280,
                720,
                GrayboxWindowMode3D.FullScreenWindow);

            store.Save(1, expected);

            Assert.That(
                ApprovedPlayerPrefsKeys(),
                Is.EquivalentTo(new[]
                {
                    "wastecity.settings.version",
                    "wastecity.display.width",
                    "wastecity.display.height",
                    "wastecity.display.window-mode"
                }));
            Assert.That(store.TryLoad(out int version, out var loaded), Is.True);
            Assert.That(version, Is.EqualTo(1));
            Assert.That(loaded, Is.EqualTo(expected));
            foreach (string key in ApprovedPlayerPrefsKeys())
                Assert.That(PlayerPrefs.HasKey(key), Is.True, key);
        }

        [Test]
        public void IDEA0007_UsabilityTypesDoNotReferenceGameSaveTypes()
        {
            Assembly usabilityAssembly =
                typeof(GrayboxDisplaySettingsModel3D).Assembly;
            foreach (Type type in usabilityAssembly.GetTypes())
            {
                AssertNotSaveType(type.BaseType);
                foreach (FieldInfo field in type.GetFields(
                             BindingFlags.Instance |
                             BindingFlags.Static |
                             BindingFlags.Public |
                             BindingFlags.NonPublic))
                    AssertNotSaveType(field.FieldType);
                foreach (MethodInfo method in type.GetMethods(
                             BindingFlags.Instance |
                             BindingFlags.Static |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    AssertNotSaveType(method.ReturnType);
                    foreach (ParameterInfo parameter in method.GetParameters())
                        AssertNotSaveType(parameter.ParameterType);
                }
            }
        }

        [TestCase(.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        public void IDEA0007_MenuPauseRestoresOpeningRequestedSpeed(
            float requestedSpeed)
        {
            MenuFixture fixture = CreateMenuControllerFixture();
            fixture.Speed.Set(requestedSpeed);

            fixture.Controller.Open();

            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Main));
            Assert.That(fixture.Speed.RequestedSpeed, Is.EqualTo(requestedSpeed));
            Assert.That(fixture.Speed.Speed, Is.Zero);
            Assert.That(Time.timeScale, Is.Zero);

            fixture.Controller.Continue();

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Speed.RequestedSpeed, Is.EqualTo(requestedSpeed));
            Assert.That(fixture.Speed.Speed, Is.EqualTo(requestedSpeed));
            Assert.That(Time.timeScale, Is.EqualTo(requestedSpeed));
        }

        [Test]
        public void IDEA0007_ClosingMenuDoesNotReleaseAnotherPauseReason()
        {
            MenuFixture fixture = CreateMenuControllerFixture();
            fixture.Speed.Set(2f);
            fixture.Speed.SetPaused(GamePauseReason.Title, true);

            fixture.Controller.Open();
            fixture.Controller.Continue();

            Assert.That(
                fixture.Speed.IsPaused(GamePauseReason.SystemMenu),
                Is.False);
            Assert.That(
                fixture.Speed.IsPaused(GamePauseReason.Title),
                Is.True);
            Assert.That(fixture.Speed.RequestedSpeed, Is.EqualTo(2f));
            Assert.That(fixture.Speed.Speed, Is.Zero);
            Assert.That(Time.timeScale, Is.Zero);
        }

        [TestCase("OnDisable")]
        [TestCase("OnDestroy")]
        public void IDEA0007_ControllerTeardownReleasesOnlyOwnedPause(
            string lifecycleMethod)
        {
            MenuFixture fixture = CreateMenuControllerFixture();
            fixture.Speed.Set(.5f);
            fixture.Controller.Open();

            InvokeLifecycle(fixture.Controller, lifecycleMethod);
            InvokeLifecycle(fixture.Controller, lifecycleMethod);

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(
                fixture.Speed.IsPaused(GamePauseReason.SystemMenu),
                Is.False);
            Assert.That(fixture.Speed.Speed, Is.EqualTo(.5f));
            Assert.That(Time.timeScale, Is.EqualTo(.5f));
        }

        [Test]
        public void IDEA0007_ControllerTeardownPreservesForeignPause()
        {
            MenuFixture fixture = CreateMenuControllerFixture();
            fixture.Speed.SetPaused(GamePauseReason.Session, true);
            fixture.Controller.Open();

            InvokeLifecycle(fixture.Controller, "OnDisable");

            Assert.That(
                fixture.Speed.IsPaused(GamePauseReason.SystemMenu),
                Is.False);
            Assert.That(
                fixture.Speed.IsPaused(GamePauseReason.Session),
                Is.True);
            Assert.That(fixture.Speed.Speed, Is.Zero);
            Assert.That(Time.timeScale, Is.Zero);
        }

        [Test]
        public void IDEA0007_MenuPageTransitionsAreDeterministic()
        {
            MenuFixture fixture = CreateMenuControllerFixture();
            fixture.Controller.Open();

            fixture.Controller.OpenSettings();
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Settings));
            fixture.Controller.OpenOperationGuide();
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.OperationGuide));
            fixture.Controller.BackFromOperationGuide();
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Settings));
            fixture.Controller.CancelSettings();
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Main));
            fixture.Controller.OpenExitConfirmation();
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.ExitConfirm));
            fixture.Controller.CancelExit();
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Main));
        }

        [Test]
        public void IDEA0007_SettingsActionsRespectStageApplyCancelAndDefault()
        {
            MenuFixture fixture = CreateMenuControllerFixture();
            GrayboxDisplaySettings3D applied = fixture.Settings.LastApplied;
            fixture.Controller.Open();
            fixture.Controller.OpenSettings();
            fixture.Controller.StageResolution(0);
            fixture.Controller.StageWindowMode(
                GrayboxWindowMode3D.FullScreenWindow);

            fixture.Controller.CancelSettings();

            Assert.That(fixture.Settings.Staged, Is.EqualTo(applied));
            Assert.That(fixture.Platform.ApplyCount, Is.Zero);
            Assert.That(fixture.Store.SaveCount, Is.Zero);

            fixture.Controller.OpenSettings();
            fixture.Controller.RestoreDefaultSettings();
            Assert.That(
                fixture.Settings.Staged,
                Is.EqualTo(Setting(
                    1920,
                    1080,
                    GrayboxWindowMode3D.FullScreenWindow)));
            Assert.That(fixture.Platform.ApplyCount, Is.Zero);
            Assert.That(fixture.Store.SaveCount, Is.Zero);

            Assert.That(fixture.Controller.ApplySettings(), Is.True);
            Assert.That(fixture.Platform.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Store.SaveCount, Is.EqualTo(1));
            Assert.That(
                fixture.Settings.LastApplied,
                Is.EqualTo(fixture.Settings.Staged));
        }

        [Test]
        public void IDEA0007_ExitConfirmationWarnsAndInvokesExitOnlyOnce()
        {
            MenuFixture fixture = CreateMenuControllerFixture(withView: true);
            fixture.Controller.Open();
            fixture.Controller.OpenExitConfirmation();

            Assert.That(fixture.Exit.Count, Is.Zero);
            Assert.That(fixture.Store.SaveCount, Is.Zero);
            Assert.That(
                TextContent(fixture.Canvas.transform),
                Does.Contain("当前 3D 灰盒进度不会保存"));

            fixture.Controller.CancelExit();
            Assert.That(fixture.Exit.Count, Is.Zero);
            Assert.That(
                fixture.Controller.Page,
                Is.EqualTo(GrayboxSystemMenuPage3D.Main));

            fixture.Controller.OpenExitConfirmation();
            fixture.Controller.ConfirmExit();
            fixture.Controller.ConfirmExit();

            Assert.That(fixture.Exit.Count, Is.EqualTo(1));
            Assert.That(fixture.Store.SaveCount, Is.Zero);
        }

        [Test]
        public void IDEA0007_DevelopmentAndReleaseExposeIdenticalControls()
        {
            IReadOnlyList<string> development =
                GrayboxSystemMenuView3D.ResolveVisibleControlIds(true);
            IReadOnlyList<string> release =
                GrayboxSystemMenuView3D.ResolveVisibleControlIds(false);

            Assert.That(development, Is.EqualTo(release));
            Assert.That(development, Does.Contain("Main.Continue"));
            Assert.That(development, Does.Contain("Settings.Apply"));
            Assert.That(development, Does.Contain("Exit.Confirm"));
            Assert.That(
                development.Any(value => value.IndexOf(
                    "diagnostic",
                    StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void IDEA0007_ViewBlockerFocusGuideAndVisibilityFollowMenu()
        {
            MenuFixture fixture = CreateMenuControllerFixture(withView: true);

            Assert.That(fixture.View.IsPointerBlockerActive, Is.False);
            Assert.That(fixture.View.HasMenuFocus, Is.False);
            fixture.Controller.Open();
            Assert.That(fixture.View.IsPointerBlockerActive, Is.True);
            Assert.That(fixture.View.HasMenuFocus, Is.True);
            Assert.That(
                fixture.View.VisiblePage,
                Is.EqualTo(GrayboxSystemMenuPage3D.Main));
            Assert.That(
                TextContent(fixture.Canvas.transform),
                Does.Contain("游戏已暂停"));

            fixture.Controller.OpenSettings();
            fixture.Controller.OpenOperationGuide();
            string guide = TextContent(fixture.Canvas.transform);
            foreach (string token in new[]
                     {
                         "B", "F", "R", "Delete", "right-click",
                         "WASD", "mouse", "Home", "Esc"
                     })
                Assert.That(guide, Does.Contain(token), token);

            fixture.Controller.Continue();
            Assert.That(fixture.View.IsPointerBlockerActive, Is.False);
            Assert.That(fixture.View.HasMenuFocus, Is.False);
            Assert.That(fixture.EventSystem.currentSelectedGameObject, Is.Null);
        }

        [Test]
        public void IDEA0007_ViewDisableEnableKeepsOneRootAndOneListenerSet()
        {
            MenuFixture fixture = CreateMenuControllerFixture(withView: true);
            fixture.Controller.Open();
            fixture.Controller.OpenSettings();

            fixture.View.enabled = false;
            fixture.View.enabled = true;

            Assert.That(
                NamedTransforms(
                    fixture.Canvas.transform,
                    "GrayboxSystemMenuUi.Root"),
                Has.Count.EqualTo(1));
            Assert.That(fixture.View.IsPointerBlockerActive, Is.True);
            Assert.That(fixture.View.HasMenuFocus, Is.True);

            FindButton(fixture.Canvas.transform, "Settings.Apply")
                .onClick.Invoke();
            Assert.That(fixture.Platform.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Store.SaveCount, Is.EqualTo(1));
        }

        private MenuFixture CreateMenuControllerFixture(bool withView = false)
        {
            var speed = new GameSpeedModel();
            FakePlatform platform = StandardPlatform();
            var store = new FakeStore();
            var settings = new GrayboxDisplaySettingsModel3D(platform, store);
            var exit = new FakeExit();
            GrayboxSystemMenuView3D view = null;
            Canvas canvas = null;
            EventSystem eventSystem = null;
            if (withView)
            {
                GameObject canvasObject = CreateObject(
                    "SystemMenuCanvas",
                    typeof(RectTransform),
                    typeof(Canvas));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                eventSystem = CreateObject("EventSystem", typeof(EventSystem))
                    .GetComponent<EventSystem>();
                view = CreateObject("SystemMenuView")
                    .AddComponent<GrayboxSystemMenuView3D>();
            }
            GrayboxSystemMenuController3D controller =
                CreateObject("SystemMenuController")
                    .AddComponent<GrayboxSystemMenuController3D>();
            if (view != null)
                view.Configure(canvas, eventSystem, controller);
            controller.Configure(speed, settings, exit, view);
            return new MenuFixture(
                controller,
                view,
                speed,
                settings,
                platform,
                store,
                exit,
                canvas,
                eventSystem);
        }

        private GameObject CreateObject(
            string name,
            params Type[] components)
        {
            var gameObject = new GameObject(name, components);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void InvokeLifecycle(
            MonoBehaviour behaviour,
            string methodName)
        {
            MethodInfo method = behaviour.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(behaviour, null);
        }

        private static Button FindButton(Transform root, string name)
        {
            return NamedTransforms(root, name).Single()
                .GetComponent<Button>();
        }

        private static List<Transform> NamedTransforms(
            Transform root,
            string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Where(value => value.name == name)
                .ToList();
        }

        private static string TextContent(Transform root)
        {
            return string.Join(
                "\n",
                root.GetComponentsInChildren<Text>(true)
                    .Select(value => value.text));
        }

        private static void AssertNotSaveType(Type type)
        {
            if (type == null) return;
            Assert.That(type.Name, Is.Not.EqualTo("SaveService"));
            Assert.That(type.Name, Is.Not.EqualTo("GameSaveData"));
        }

        private static FakePlatform StandardPlatform(
            GrayboxDisplaySettings3D? current = null)
        {
            return new FakePlatform(
                current ?? Setting(
                    1600,
                    900,
                    GrayboxWindowMode3D.Windowed),
                Resolution(1280, 720),
                Resolution(1600, 900),
                Resolution(1920, 1080));
        }

        private static GrayboxDisplaySettings3D Setting(
            int width,
            int height,
            GrayboxWindowMode3D mode)
        {
            return new GrayboxDisplaySettings3D(width, height, mode);
        }

        private static GrayboxDisplayResolution3D Resolution(
            int width,
            int height)
        {
            return new GrayboxDisplayResolution3D(width, height);
        }

        private static string[] ApprovedPlayerPrefsKeys()
        {
            return typeof(PlayerPrefsGrayboxDisplaySettingsStore3D)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && !field.IsInitOnly)
                .Select(field => field.GetRawConstantValue() as string)
                .Where(value => value != null)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void DeleteApprovedPlayerPrefsKeys()
        {
            PlayerPrefs.DeleteKey("wastecity.settings.version");
            PlayerPrefs.DeleteKey("wastecity.display.width");
            PlayerPrefs.DeleteKey("wastecity.display.height");
            PlayerPrefs.DeleteKey("wastecity.display.window-mode");
        }

        private sealed class FakeStore : IGrayboxDisplaySettingsStore
        {
            private readonly bool hasValue;
            private readonly int version;
            private readonly GrayboxDisplaySettings3D value;

            public FakeStore() : this((List<string>)null)
            {
            }

            public FakeStore(List<string> events)
            {
                Events = events ?? new List<string>();
            }

            public FakeStore(
                bool hasValue,
                int version,
                GrayboxDisplaySettings3D value)
            {
                this.hasValue = hasValue;
                this.version = version;
                this.value = value;
                Events = new List<string>();
            }

            public int LoadCount { get; private set; }
            public int SaveCount { get; private set; }
            public int LastSavedVersion { get; private set; }
            public GrayboxDisplaySettings3D LastSaved { get; private set; }
            public List<string> Events { get; }

            public bool TryLoad(
                out int storedVersion,
                out GrayboxDisplaySettings3D settings)
            {
                LoadCount++;
                storedVersion = version;
                settings = value;
                return hasValue;
            }

            public void Save(
                int storedVersion,
                GrayboxDisplaySettings3D settings)
            {
                SaveCount++;
                LastSavedVersion = storedVersion;
                LastSaved = settings;
                Events.Add("save");
            }
        }

        private sealed class FakePlatform : IGrayboxDisplaySettingsPlatform
        {
            private readonly GrayboxDisplayResolution3D[] resolutions;

            public FakePlatform(
                GrayboxDisplaySettings3D current,
                params GrayboxDisplayResolution3D[] resolutions)
                : this(current, null, resolutions)
            {
            }

            public FakePlatform(
                GrayboxDisplaySettings3D current,
                List<string> events,
                params GrayboxDisplayResolution3D[] resolutions)
            {
                Current = current;
                this.resolutions = resolutions ??
                    Array.Empty<GrayboxDisplayResolution3D>();
                Events = events ?? new List<string>();
            }

            public IReadOnlyList<GrayboxDisplayResolution3D>
                AvailableResolutions => resolutions;
            public GrayboxDisplaySettings3D Current { get; }
            public bool ApplySucceeds { get; set; } = true;
            public int ApplyCount { get; private set; }
            public GrayboxDisplaySettings3D LastApplied { get; private set; }
            public List<string> Events { get; }

            public bool TryApply(GrayboxDisplaySettings3D settings)
            {
                ApplyCount++;
                LastApplied = settings;
                Events.Add("apply");
                return ApplySucceeds;
            }
        }

        private sealed class FakeExit : IGrayboxApplicationExit
        {
            public int Count { get; private set; }

            public void Exit()
            {
                Count++;
            }
        }

        private sealed class MenuFixture
        {
            public MenuFixture(
                GrayboxSystemMenuController3D controller,
                GrayboxSystemMenuView3D view,
                GameSpeedModel speed,
                GrayboxDisplaySettingsModel3D settings,
                FakePlatform platform,
                FakeStore store,
                FakeExit exit,
                Canvas canvas,
                EventSystem eventSystem)
            {
                Controller = controller;
                View = view;
                Speed = speed;
                Settings = settings;
                Platform = platform;
                Store = store;
                Exit = exit;
                Canvas = canvas;
                EventSystem = eventSystem;
            }

            public GrayboxSystemMenuController3D Controller { get; }
            public GrayboxSystemMenuView3D View { get; }
            public GameSpeedModel Speed { get; }
            public GrayboxDisplaySettingsModel3D Settings { get; }
            public FakePlatform Platform { get; }
            public FakeStore Store { get; }
            public FakeExit Exit { get; }
            public Canvas Canvas { get; }
            public EventSystem EventSystem { get; }
        }
    }
}
