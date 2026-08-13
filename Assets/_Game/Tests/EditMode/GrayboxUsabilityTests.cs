using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxUsabilityTests
    {
        [SetUp]
        public void SetUp()
        {
            DeleteApprovedPlayerPrefsKeys();
        }

        [TearDown]
        public void TearDown()
        {
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
    }
}
