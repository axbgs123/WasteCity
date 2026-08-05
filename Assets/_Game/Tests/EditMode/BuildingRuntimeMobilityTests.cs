using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class BuildingRuntimeMobilityTests
    {
        [Test]
        public void CompletedGroundBuildingStopsWhenCityBecomesMobile()
        {
            GameObject cityObject = null;
            GameObject buildingObject = null;
            try
            {
                PlaceholderMobileCity city = CreateCity(CityMode.Fortress, out cityObject);
                BuildingRuntime runtime = CreateCompletedBuilding(
                    BuildingCatalog.Smelter,
                    BuildingSite.Ground,
                    city,
                    out buildingObject);

                Assert.That(runtime.Site, Is.EqualTo(BuildingSite.Ground));
                Assert.That(runtime.IsOperational, Is.True);

                city.RestoreDeployment(CityMode.Mobile, 0f);

                Assert.That(runtime.IsOperational, Is.False);
            }
            finally
            {
                if (buildingObject != null) Object.DestroyImmediate(buildingObject);
                if (cityObject != null) Object.DestroyImmediate(cityObject);
            }
        }

        [Test]
        public void CompletedInnerCityMobileBuildingRunsInMobileButNotTransition()
        {
            GameObject cityObject = null;
            GameObject buildingObject = null;
            try
            {
                PlaceholderMobileCity city = CreateCity(CityMode.Mobile, out cityObject);
                BuildingRuntime runtime = CreateCompletedBuilding(
                    BuildingCatalog.Housing,
                    BuildingSite.InnerCity,
                    city,
                    out buildingObject);

                Assert.That(runtime.IsOperational, Is.True);

                city.RestoreDeployment(CityMode.Deploying, 1f);

                Assert.That(runtime.IsOperational, Is.False);

                city.RestoreDeployment(CityMode.Fortress, 0f);

                Assert.That(runtime.IsOperational, Is.True);
            }
            finally
            {
                if (buildingObject != null) Object.DestroyImmediate(buildingObject);
                if (cityObject != null) Object.DestroyImmediate(cityObject);
            }
        }

        [Test]
        public void IncompleteOrDisconnectedBuildingIsNotOperational()
        {
            GameObject cityObject = null;
            GameObject buildingObject = null;
            try
            {
                PlaceholderMobileCity city = CreateCity(CityMode.Fortress, out cityObject);
                buildingObject = new GameObject("Building");
                buildingObject.AddComponent<HealthComponent>();
                var runtime = buildingObject.AddComponent<BuildingRuntime>();
                runtime.Configure(
                    BuildingCatalog.Assembler,
                    city: city,
                    site: BuildingSite.Ground);

                Assert.That(runtime.IsOperational, Is.False);

                runtime.RestoreState(BuildingCatalog.Assembler.MaximumHealth, 0f);
                runtime.SetLogistics(false);

                Assert.That(runtime.IsOperational, Is.False);
            }
            finally
            {
                if (buildingObject != null) Object.DestroyImmediate(buildingObject);
                if (cityObject != null) Object.DestroyImmediate(cityObject);
            }
        }

        private static PlaceholderMobileCity CreateCity(CityMode mode, out GameObject cityObject)
        {
            cityObject = new GameObject("City");
            cityObject.AddComponent<Rigidbody2D>();
            var city = cityObject.AddComponent<PlaceholderMobileCity>();
            typeof(PlaceholderMobileCity)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(city, null);
            city.RestoreDeployment(mode, mode == CityMode.Deploying || mode == CityMode.Packing ? 1f : 0f);
            return city;
        }

        private static BuildingRuntime CreateCompletedBuilding(
            BuildingDefinition definition,
            BuildingSite site,
            PlaceholderMobileCity city,
            out GameObject buildingObject)
        {
            buildingObject = new GameObject("Building");
            buildingObject.AddComponent<HealthComponent>();
            var runtime = buildingObject.AddComponent<BuildingRuntime>();
            runtime.Configure(definition, city: city, site: site);
            runtime.RestoreState(definition.MaximumHealth, 0f);
            return runtime;
        }
    }
}
