using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WasteCity.Core;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.World;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionRuntimeAdapter3DTests
    {
        private const string ControllerTypeName =
            "WasteCity.Graybox3D.Building.GrayboxProductionController3D, " +
            "WasteCity.Graybox3D.Building";
        private const string ControllerSourcePath =
            "_Game/Scripts/Graybox3D/Building/" +
            "GrayboxProductionController3D.cs";
        private const string AuthoringSourcePath =
            "_Game/Editor/GrayboxSceneAuthoring.cs";
        private const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";

        [Test]
        public void IDEA0011_ProductionController_ExposesMinimalRuntimeContract()
        {
            Type controllerType = RequiredControllerType();

            Assert.That(
                typeof(MonoBehaviour).IsAssignableFrom(controllerType),
                Is.True);
            MethodInfo configure = controllerType.GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(GrayboxBuildingSession3D),
                    typeof(GrayboxWorldView3D),
                    typeof(GrayboxMobileCityController3D),
                    typeof(GameSpeedModel)
                },
                null);
            Assert.That(
                configure,
                Is.Not.Null,
                "Configure must receive the formal session, world, city and rule-time source.");

            MethodInfo tick = controllerType.GetMethod(
                "TickProduction",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float) },
                null);
            Assert.That(tick, Is.Not.Null);
            Assert.That(
                tick.ReturnType,
                Is.EqualTo(typeof(float)),
                "TickProduction returns the applied rule delta for diagnostics and tests.");
        }

        [Test]
        public void IDEA0011_ProductionController_ConfigureRejectsMissingOwners()
        {
            Type controllerType = RequiredControllerType();
            var owner = new GameObject("ProductionControllerContract");
            var sessionObject = new GameObject("Session");
            var worldObject = new GameObject("World");
            var cityObject = new GameObject("City");
            try
            {
                Component controller = owner.AddComponent(controllerType);
                var session = sessionObject.AddComponent<GrayboxBuildingSession3D>();
                var world = worldObject.AddComponent<GrayboxWorldView3D>();
                var city = cityObject.AddComponent<GrayboxMobileCityController3D>();
                var speed = new GameSpeedModel();
                MethodInfo configure = RequiredConfigure(controllerType);

                AssertConfigureNull(configure, controller, null, world, city, speed);
                AssertConfigureNull(configure, controller, session, null, city, speed);
                AssertConfigureNull(configure, controller, session, world, null, speed);
                AssertConfigureNull(configure, controller, session, world, city, null);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(sessionObject);
                Object.DestroyImmediate(worldObject);
                Object.DestroyImmediate(cityObject);
            }
        }

        [Test]
        public void IDEA0011_ProductionController_PauseAndResumeHaveNoDeltaJump()
        {
            Type controllerType = RequiredControllerType();
            var root = new GameObject("ProductionRuntimeFixture");
            try
            {
                var session = root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureDevelopmentFixture();
                var worldView = root.AddComponent<GrayboxWorldView3D>();
                var city = root.AddComponent<GrayboxMobileCityController3D>();
                Component controller = root.AddComponent(controllerType);
                ConfigureWorldModel(worldView);
                var speed = new GameSpeedModel();
                RequiredConfigure(controllerType).Invoke(
                    controller,
                    new object[] { session, worldView, city, speed });
                MethodInfo tick = RequiredTick(controllerType);

                speed.SetPaused(GamePauseReason.User, true);
                Assert.That(
                    AppliedDelta(tick, controller, 10f),
                    Is.Zero,
                    "A long tactical pause must apply zero rule time.");

                speed.SetPaused(GamePauseReason.User, false);
                Assert.That(
                    AppliedDelta(tick, controller, 0.25f),
                    Is.EqualTo(0.25f).Within(0.0001f),
                    "Resume must apply only the current frame, not paused wall time.");

                speed.SetPaused(GamePauseReason.SystemMenu, true);
                Assert.That(
                    AppliedDelta(tick, controller, 8f),
                    Is.Zero,
                    "System menu pause must apply zero rule time.");
                speed.SetPaused(GamePauseReason.SystemMenu, false);
                Assert.That(
                    AppliedDelta(tick, controller, 0.1f),
                    Is.EqualTo(0.1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IDEA0011_ProductionController_SynchronizesBeforeSimulationWithoutDiscovery()
        {
            string source = ReadProjectSource(ControllerSourcePath);
            string tick = ExtractMethod(source, "public float TickProduction");
            int synchronize = tick.IndexOf(
                "session.SynchronizeProductionRuntime",
                StringComparison.Ordinal);
            int simulate = tick.IndexOf(
                "simulation.Tick",
                StringComparison.Ordinal);

            Assert.That(synchronize, Is.GreaterThanOrEqualTo(0));
            Assert.That(simulate, Is.GreaterThan(synchronize));
            StringAssert.Contains("worldView.Model", tick);
            StringAssert.Contains("cityController.Mode", tick);
            StringAssert.Contains("speed.Speed", tick);
            StringAssert.Contains(
                "ruleTimeSource",
                source,
                "The formal scene must share the system-menu rule-time source.");
            StringAssert.DoesNotContain(
                "CityOperationalRules.ProductionMultiplier",
                tick,
                "The pure simulation is the single city production multiplier consumer.");
            StringAssert.DoesNotContain("FindObject", source);
            StringAssert.DoesNotContain("FindObjects", source);
            StringAssert.DoesNotContain("System.Linq", source);
            StringAssert.DoesNotContain(".Where(", source);
            StringAssert.DoesNotContain(".Select(", source);

            string update = ExtractMethod(source, "private void Update");
            StringAssert.Contains(
                "TickProduction(Time.unscaledDeltaTime)",
                update,
                "Update must pass only this frame's unscaled time to the rule-time source.");
        }

        [Test]
        public void IDEA0011_FormalScene_HasOneProductionControllerWithFormalOwners()
        {
            Type controllerType = RequiredControllerType();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Object[] controllers = Object.FindObjectsOfType(controllerType, true);
            GrayboxBuildingSession3D[] sessions =
                Object.FindObjectsOfType<GrayboxBuildingSession3D>(true);
            GrayboxWorldView3D[] worlds =
                Object.FindObjectsOfType<GrayboxWorldView3D>(true);
            GrayboxMobileCityController3D[] cities =
                Object.FindObjectsOfType<GrayboxMobileCityController3D>(true);
            GrayboxSystemMenuController3D[] systemMenus =
                Object.FindObjectsOfType<GrayboxSystemMenuController3D>(true);

            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(sessions, Has.Length.EqualTo(1));
            Assert.That(worlds, Has.Length.EqualTo(1));
            Assert.That(cities, Has.Length.EqualTo(1));
            Assert.That(systemMenus, Has.Length.EqualTo(1));

            var controller = (Component)controllers[0];
            var serialized = new SerializedObject(controller);
            AssertSerializedReference(serialized, "session", sessions[0]);
            AssertSerializedReference(serialized, "worldView", worlds[0]);
            AssertSerializedReference(serialized, "cityController", cities[0]);
            AssertSerializedReference(
                serialized,
                "ruleTimeSource",
                systemMenus[0]);
            Assert.That(
                controller.name,
                Is.EqualTo("GrayboxProductionController"));
            Assert.That(
                controller.transform.parent.name,
                Is.EqualTo("GrayboxSystems"));
        }

        [Test]
        public void IDEA0011_Authoring_UsesDedicatedIdempotentProductionContract()
        {
            string source = ReadProjectSource(AuthoringSourcePath);
            StringAssert.Contains(
                "EnsureProductionContract(scene);",
                source,
                "Production authoring must be an explicit stage after building ownership exists.");
            string method = ExtractMethod(
                source,
                "private static void EnsureProductionContract");

            StringAssert.Contains("GrayboxProductionController", method);
            StringAssert.Contains(
                "EnsureComponent<GrayboxProductionController3D>",
                method);
            StringAssert.Contains("GrayboxBuildingSession3D", method);
            StringAssert.Contains("GrayboxWorldView3D", method);
            StringAssert.Contains("GrayboxMobileCityController3D", method);
            StringAssert.Contains("GrayboxSystemMenuController3D", method);
            StringAssert.Contains("SetReferences", method);
            StringAssert.Contains("session", method);
            StringAssert.Contains("worldView", method);
            StringAssert.Contains("cityController", method);
            StringAssert.Contains("ruleTimeSource", method);
            StringAssert.DoesNotContain("AddComponent", method);
            StringAssert.DoesNotContain("FindObject", method);
            StringAssert.DoesNotContain("FindObjects", method);
        }

        private static Type RequiredControllerType()
        {
            Type type = Type.GetType(ControllerTypeName);
            Assert.That(
                type,
                Is.Not.Null,
                "IDEA0011 requires the formal 3D production runtime adapter.");
            return type;
        }

        private static MethodInfo RequiredConfigure(Type controllerType)
        {
            MethodInfo method = controllerType.GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(GrayboxBuildingSession3D),
                    typeof(GrayboxWorldView3D),
                    typeof(GrayboxMobileCityController3D),
                    typeof(GameSpeedModel)
                },
                null);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static MethodInfo RequiredTick(Type controllerType)
        {
            MethodInfo method = controllerType.GetMethod(
                "TickProduction",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float) },
                null);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static void AssertConfigureNull(
            MethodInfo configure,
            Component controller,
            GrayboxBuildingSession3D session,
            GrayboxWorldView3D world,
            GrayboxMobileCityController3D city,
            GameSpeedModel speed)
        {
            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => configure.Invoke(
                        controller,
                        new object[] { session, world, city, speed }));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<ArgumentNullException>());
        }

        private static float AppliedDelta(
            MethodInfo tick,
            Component controller,
            float unscaledDeltaTime)
        {
            return (float)tick.Invoke(
                controller,
                new object[] { unscaledDeltaTime });
        }

        private static void ConfigureWorldModel(GrayboxWorldView3D worldView)
        {
            var cells = new WorldCell[2, 2];
            for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                    cells[x, y] = new WorldCell(
                        TerrainKind.Wasteland,
                        null,
                        0);
            SetAutoProperty(
                worldView,
                "Model",
                new WorldMapModel(cells));
            var coordinates = new PlanarCoordinateMapper3D(2, 2);
            SetAutoProperty(
                worldView,
                "Coordinates",
                coordinates);
            Assert.That(
                coordinates.TryCellToWorld(
                    0,
                    0,
                    worldView.transform.position.y,
                    out Vector3 cityPosition),
                Is.True);
            worldView.transform.position = cityPosition;
        }

        private static void SetAutoProperty(
            object owner,
            string propertyName,
            object value)
        {
            FieldInfo field = owner.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, propertyName);
            field.SetValue(owner, value);
        }

        private static void AssertSerializedReference(
            SerializedObject owner,
            string propertyName,
            Object expected)
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(
                property.objectReferenceValue,
                Is.SameAs(expected),
                propertyName);
        }

        private static string ReadProjectSource(string relativePath)
        {
            string path = Path.Combine(Application.dataPath, relativePath);
            Assert.That(File.Exists(path), Is.True, path);
            return File.ReadAllText(path);
        }

        private static string ExtractMethod(string source, string marker)
        {
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), marker);
            int openingBrace = source.IndexOf('{', start);
            Assert.That(openingBrace, Is.GreaterThan(start), marker);
            var depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}') depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            Assert.Fail($"Unclosed method: {marker}");
            return string.Empty;
        }
    }
}
