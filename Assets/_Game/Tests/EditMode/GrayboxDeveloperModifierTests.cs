using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxDeveloperModifierTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void RuntimeAvailability_RequiresEditorOrDevelopmentBuild()
        {
            Assert.That(
                GrayboxDeveloperModifierBootstrap3D.ResolveRuntimeAvailability(
                    false,
                    false),
                Is.False);
            Assert.That(
                GrayboxDeveloperModifierBootstrap3D.ResolveRuntimeAvailability(
                    true,
                    false),
                Is.True);
            Assert.That(
                GrayboxDeveloperModifierBootstrap3D.ResolveRuntimeAvailability(
                    false,
                    true),
                Is.True);
        }

        [Test]
        public void Bootstrap_OnlySerializesAlwaysCompiledComponentFields()
        {
            FieldInfo[] fields = typeof(GrayboxDeveloperModifierBootstrap3D)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                if (!field.IsDefined(typeof(SerializeField), true))
                    continue;
                Assert.That(field.FieldType, Is.Not.EqualTo(
                    typeof(GrayboxDeveloperModifier3D)));
            }
        }

        [Test]
        public void Bootstrap_DoesNotPollInputAndOnlyExposesTryTogglePanel()
        {
            string source = ReadSource(
                "GrayboxDeveloperModifierBootstrap3D.cs");

            Assert.That(source, Does.Not.Contain("Keyboard.current"));
            Assert.That(source, Does.Not.Contain("f10Key"));
            Assert.That(source, Does.Not.Match(@"void Update\s*\("));
            Assert.That(source, Does.Contain("TryTogglePanel"));
            Assert.That(
                typeof(GrayboxDeveloperModifierBootstrap3D).GetMethod(
                    "TryTogglePanel",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
        }

        [Test]
        public void Bootstrap_LabelsTheConditionalPanelAsDevelopmentMode()
        {
            string source = ReadSource(
                "GrayboxDeveloperModifierBootstrap3D.cs");

            Assert.That(source, Does.Contain("开发模式"));
        }

        [Test]
        public void Bootstrap_ExposesEveryModifierCommandAfterPanelToggle()
        {
            BootstrapFixture fixture = CreateBootstrapFixture();

            Assert.That(fixture.Bootstrap.TryTogglePanel(), Is.True);
            Assert.That(fixture.Panel.activeSelf, Is.True);
            Assert.That(ButtonNamed(fixture.Panel, "Resource +100"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Resource +1000"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Clear Resource"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Set Resource"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Unlock Research"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Unlock Technology"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Unlock Cultivation"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Unlock Biological Ascension"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Unlock Psionics"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Unlock All"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Set Mobile"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Set Fortress"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Complete Transition"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Multiplier 1x"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Multiplier 10x"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Multiplier 100x"), Is.Not.Null);
            Assert.That(ButtonNamed(fixture.Panel, "Complete Construction"), Is.Not.Null);

            InputNamed(fixture.Panel, "Resource Amount").text = "321";
            ButtonNamed(fixture.Panel, "Set Resource").onClick.Invoke();
            ButtonNamed(fixture.Panel, "Resource +100").onClick.Invoke();
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(421));
            InputNamed(fixture.Panel, "Research Id").text =
                "core.research.automated-machinery";
            ButtonNamed(fixture.Panel, "Unlock Research").onClick.Invoke();
            ButtonNamed(fixture.Panel, "Set Fortress").onClick.Invoke();
            ButtonNamed(fixture.Panel, "Multiplier 10x").onClick.Invoke();
            Assert.That(fixture.Session.IsResearchCompleted(
                "core.research.automated-machinery"), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.Session.ConstructionMultiplier, Is.EqualTo(10f));
        }

        [Test]
        public void Bootstrap_ProvidesIdempotentEventSystemAndRealUguiClickPath()
        {
            BootstrapFixture fixture = CreateBootstrapFixture();
            Assert.That(fixture.Bootstrap.TryTogglePanel(), Is.True);

            EventSystem[] eventSystems =
                UnityEngine.Object.FindObjectsOfType<EventSystem>();
            Assert.That(eventSystems, Has.Length.EqualTo(1));
            EventSystem eventSystem = eventSystems[0];
            Assert.That(eventSystem.gameObject.activeInHierarchy, Is.True);
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(),
                Is.Not.Null);
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>()
                .isActiveAndEnabled, Is.True);
            Assert.That(
                eventSystem.GetComponents<InputSystemUIInputModule>().Length,
                Is.EqualTo(1));

            Button button = ButtonNamed(fixture.Panel, "Resource +100");
            ExecuteEvents.Execute(
                button.gameObject,
                new PointerEventData(eventSystem)
                {
                    button = PointerEventData.InputButton.Left
                },
                ExecuteEvents.pointerClickHandler);
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(130));

            fixture.Bootstrap.Configure(
                fixture.Session,
                fixture.City,
                fixture.Presentation);
            Assert.That(
                UnityEngine.Object.FindObjectsOfType<EventSystem>().Length,
                Is.EqualTo(1));
            Assert.That(
                eventSystem.GetComponents<InputSystemUIInputModule>().Length,
                Is.EqualTo(1));
        }

        [Test]
        public void Modifier_UsesOnlyApprovedModelAndDevelopmentAdapterPaths()
        {
            string source = ReadSource("GrayboxDeveloperModifier3D.cs");

            Assert.That(source, Does.Contain("session.Inventory.Add"));
            Assert.That(source, Does.Contain("session.Inventory.Set"));
            Assert.That(source, Does.Contain(
                "session.UnlockResearchForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.UnlockRouteForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.UnlockAllResearchForDevelopment"));
            Assert.That(source, Does.Contain(
                "city.RestoreDeploymentForDevelopment"));
            Assert.That(source, Does.Contain(
                "city.CompleteDeploymentTransitionForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.SetConstructionMultiplierForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.CompleteAllConstructionForDevelopment"));
            Assert.That(source, Does.Not.Match(@"\btransform\s*\."));
            Assert.That(source, Does.Not.Match(
                @"\b(GroundGrid|InnerGrid)\s*\."));
            Assert.That(source, Does.Not.Match(
                @"BindingFlags|GetField|SetValue|FieldInfo"));
            Assert.That(source, Does.Not.Match(
                @"TryBeginConstruction|TryPlace|TryRestore"));
            Assert.That(source, Does.Not.Match(@"\bdeployment\s*\."));
        }

        [Test]
        public void Commands_ApplyResourceOperationsWithinCapacity()
        {
            ModifierFixture fixture = CreateFixture();
            fixture.Session.Inventory.Set(ResourceIds.Iron, 4900);

            fixture.Modifier.SetCurrentResource(ResourceIds.Iron);
            fixture.Modifier.AddCurrentResource100();
            fixture.Modifier.AddCurrentResource1000();
            fixture.Modifier.ClearCurrentResource();
            fixture.Modifier.SetCurrentResourceAmount(1234);
            fixture.Modifier.SetCurrentResourceAmount(-1);

            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(1234));
            fixture.Modifier.AddCurrentResource1000();
            fixture.Modifier.AddCurrentResource1000();
            fixture.Modifier.AddCurrentResource1000();
            fixture.Modifier.AddCurrentResource1000();
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(5000));
        }

        [Test]
        public void Commands_UnlockWithoutRelocking()
        {
            ModifierFixture fixture = CreateFixture();

            fixture.Modifier.UnlockResearch(
                "core.research.automated-machinery");
            fixture.Modifier.UnlockRoute(ContentRoute.Technology);
            fixture.Modifier.UnlockAllResearch();

            Assert.That(fixture.Session.IsResearchCompleted(
                "core.research.automated-machinery"), Is.True);
            Assert.That(fixture.Session.HasContactedRoute(
                ContentRoute.Technology), Is.True);
            Assert.That(fixture.Session.Research.CompletedCount,
                Is.EqualTo(ResearchCatalog.All.Length));
        }

        [Test]
        public void Commands_UseSafeCityDevelopmentAdapter()
        {
            ModifierFixture fixture = CreateFixture();

            Assert.That(fixture.Modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.Modifier.SetCityMode(CityMode.Mobile), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.Modifier.SetCityMode(CityMode.Deploying), Is.False);
            fixture.City.Deployment.Restore(CityMode.Packing, 1f);
            Assert.That(fixture.Modifier.CompleteDeploymentTransition(), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
        }

        [TestCase(1f)]
        [TestCase(10f)]
        [TestCase(100f)]
        public void Commands_SetApprovedConstructionMultipliers(float multiplier)
        {
            ModifierFixture fixture = CreateFixture();

            fixture.Modifier.SetConstructionMultiplier(multiplier);

            Assert.That(fixture.Session.ConstructionMultiplier,
                Is.EqualTo(multiplier));
        }

        [Test]
        public void ImmediateCompletion_CompletesEveryExistingSiteAndPreservesMultiplier()
        {
            ModifierFixture fixture = CreateFixture();
            var presentation = new RecordingPresentation();
            Begin(fixture.Session, 10, 10, presentation);
            Begin(fixture.Session, 12, 10, presentation);
            fixture.Modifier.SetConstructionMultiplier(10f);

            fixture.Modifier.CompleteAllConstruction(presentation);

            Assert.That(fixture.Session.Instances, Has.All.Matches<
                GrayboxBuildingInstance3D>(instance =>
                    instance.State == GrayboxBuildingInstanceState.Completed));
            Assert.That(fixture.Session.ConstructionMultiplier, Is.EqualTo(10f));
            GrayboxBuildingInstance3D next = Begin(
                fixture.Session, 14, 10, presentation);
            fixture.Session.TickConstruction(
                .1f, CityMode.Fortress, false, presentation);
            Assert.That(next.Progress.Remaining, Is.EqualTo(1f));
            Assert.That(next.State,
                Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
        }

        [Test]
        public void DevelopmentChanges_AreDiscardedBySessionRecreation()
        {
            ModifierFixture fixture = CreateFixture();
            fixture.Modifier.SetCurrentResource(ResourceIds.Iron);
            fixture.Modifier.SetCurrentResourceAmount(5000);
            fixture.Modifier.UnlockAllResearch();
            fixture.Modifier.SetConstructionMultiplier(100f);

            ModifierFixture freshFixture = CreateFixture();

            Assert.That(freshFixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(30));
            Assert.That(freshFixture.Session.Research.CompletedCount, Is.Zero);
            Assert.That(freshFixture.Session.ConstructionMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void CityDevelopmentAdapter_AcceptsOnlyStableModesAndTransitions()
        {
            ModifierFixture fixture = CreateFixture();

            Assert.That(fixture.City.RestoreDeploymentForDevelopment(
                CityMode.Deploying), Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.City.RestoreDeploymentForDevelopment(
                CityMode.Fortress), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.City.CompleteDeploymentTransitionForDevelopment(),
                Is.False);
            fixture.City.Deployment.Restore(CityMode.Deploying, 1f);
            Assert.That(fixture.City.CompleteDeploymentTransitionForDevelopment(),
                Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
        }

        private ModifierFixture CreateFixture()
        {
            var sessionObject = Track(new GameObject("Session"));
            GrayboxBuildingSession3D session = sessionObject.AddComponent<
                GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            var cityObject = Track(new GameObject("City"));
            GrayboxMobileCityController3D city = cityObject.AddComponent<
                GrayboxMobileCityController3D>();
            return new ModifierFixture(
                session,
                city,
                new GrayboxDeveloperModifier3D(session, city));
        }

        private BootstrapFixture CreateBootstrapFixture()
        {
            ModifierFixture fixture = CreateFixture();
            var presentationObject = Track(new GameObject("Presentation"));
            GrayboxBuildingWorldView3D presentation =
                presentationObject.AddComponent<GrayboxBuildingWorldView3D>();
            var bootstrapObject = Track(new GameObject("Modifier Bootstrap"));
            GrayboxDeveloperModifierBootstrap3D bootstrap =
                bootstrapObject.AddComponent<
                    GrayboxDeveloperModifierBootstrap3D>();
            bootstrap.Configure(fixture.Session, fixture.City, presentation);
            return new BootstrapFixture(
                bootstrap,
                fixture.Session,
                fixture.City,
                presentation,
                bootstrap.transform.Find("Graybox Developer Modifier")
                    ?.gameObject);
        }

        private static Button ButtonNamed(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name)
                    return button;
            return null;
        }

        private static InputField InputNamed(GameObject root, string name)
        {
            foreach (InputField input in
                root.GetComponentsInChildren<InputField>(true))
                if (input.name == name)
                    return input;
            return null;
        }

        private static GrayboxBuildingInstance3D Begin(
            GrayboxBuildingSession3D session,
            int x,
            int y,
            IGrayboxBuildingPresentation3D presentation)
        {
            BuildingUnlockEvaluation unlock =
                BuildingUnlockModel.Evaluate(
                    BuildingCatalog.Wall,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                BuildingCatalog.Wall,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                x,
                y,
                session.GroundBuildRadius,
                CityMode.Fortress,
                true,
                false,
                true,
                true,
                true,
                null,
                true,
                unlock,
                session.Inventory.CanSpend(
                    BuildingCatalog.Wall.CostId,
                    BuildingCatalog.Wall.Cost));
            Assert.That(session.TryBeginConstruction(
                request, presentation, out GrayboxBuildingInstance3D instance,
                out _), Is.True);
            return instance;
        }

        private static string ReadSource(string name)
        {
            return File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Game/Scripts/Graybox3D/Building",
                name));
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class ModifierFixture
        {
            public ModifierFixture(
                GrayboxBuildingSession3D session,
                GrayboxMobileCityController3D city,
                GrayboxDeveloperModifier3D modifier)
            {
                Session = session;
                City = city;
                Modifier = modifier;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxDeveloperModifier3D Modifier { get; }
        }

        private sealed class BootstrapFixture
        {
            public BootstrapFixture(
                GrayboxDeveloperModifierBootstrap3D bootstrap,
                GrayboxBuildingSession3D session,
                GrayboxMobileCityController3D city,
                GrayboxBuildingWorldView3D presentation,
                GameObject panel)
            {
                Bootstrap = bootstrap;
                Session = session;
                City = city;
                Presentation = presentation;
                Panel = panel;
            }

            public GrayboxDeveloperModifierBootstrap3D Bootstrap { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public GameObject Panel { get; }
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
