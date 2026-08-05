using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
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
        }

        [Test]
        public void DevelopmentChanges_AreDiscardedBySessionRecreation()
        {
            ModifierFixture fixture = CreateFixture();
            fixture.Modifier.SetCurrentResource(ResourceIds.Iron);
            fixture.Modifier.SetCurrentResourceAmount(5000);
            fixture.Modifier.UnlockAllResearch();
            fixture.Modifier.SetConstructionMultiplier(100f);

            fixture.Session.ConfigureDevelopmentFixture();

            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(30));
            Assert.That(fixture.Session.Research.CompletedCount, Is.Zero);
            Assert.That(fixture.Session.ConstructionMultiplier, Is.EqualTo(1f));
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

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
