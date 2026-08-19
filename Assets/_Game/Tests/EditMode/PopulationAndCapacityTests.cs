using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Population;

namespace WasteCity.Tests
{
    public sealed class PopulationAndCapacityTests
    {
        [Test] public void InitialPopulationProducesOneHundredPercentProductivity()
        { var model = new PopulationModel(); Assert.That(model.Current, Is.EqualTo(100)); Assert.That(model.Capacity, Is.EqualTo(150)); Assert.That(model.ProductivityMultiplier, Is.EqualTo(1f)); }
        [Test] public void ExplicitFormalPopulationProducesOneHundredPercentProductivity()
        { var model = new PopulationModel(100, 150); Assert.That(model.Current, Is.EqualTo(100)); Assert.That(model.Capacity, Is.EqualTo(150)); Assert.That(model.ProductivityMultiplier, Is.EqualTo(1f)); }
        [Test] public void PopulationOverCapacityWaitsAndDoesNotIncreaseProductivity()
        { var model = new PopulationModel(); model.AddPeople(80); Assert.That(model.EffectiveWorkers, Is.EqualTo(150)); Assert.That(model.Waiting, Is.EqualTo(30)); Assert.That(model.ProductivityMultiplier, Is.EqualTo(1.25f)); }
        [Test] public void HousingCapacityMakesWaitingPopulationEffective()
        { var model = new PopulationModel(180, 150); model.AddCapacity(50); Assert.That(model.Waiting, Is.Zero); Assert.That(model.EffectiveWorkers, Is.EqualTo(180)); }
        [Test] public void WarehouseCapacityCanExpandAndShrinkInventory()
        { var inventory = new ResourceInventory(150); inventory.AddCapacity(150); inventory.Add(ResourceIds.Iron, 280); Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(280)); inventory.AddCapacity(-150); Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(150)); }

        [Test]
        public void Formal3DSessionExposesPopulationCapacityAndProductivity()
        {
            var root = new GameObject("FormalBuildingSession");
            try
            {
                GrayboxBuildingSession3D session =
                    root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureFormalSession();
                Assert.That(session.Population, Is.EqualTo(100));
                Assert.That(session.PopulationCapacity, Is.EqualTo(150));
                Assert.That(session.ProductivityMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DevelopmentRuleTimeMultiplierDoesNotChangeProductivity()
        {
            var root = new GameObject("FormalBuildingSession");
            try
            {
                GrayboxBuildingSession3D session =
                    root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureFormalSession();
                session.SetConstructionMultiplierForDevelopment(10f);

                Assert.That(session.Population, Is.EqualTo(100));
                Assert.That(session.ProductivityMultiplier, Is.EqualTo(1f));
                Assert.That(
                    session.DevelopmentRuleTimeMultiplier,
                    Is.EqualTo(10f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Formal3DSessionConstructionCombinesProductivityAndDevelopmentRuleTime()
        {
            var root = new GameObject("FormalBuildingSession");
            try
            {
                GrayboxBuildingSession3D session =
                    root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureFormalSession();
                session.SetPopulationForDevelopment(200);
                session.SetConstructionMultiplierForDevelopment(2f);
                session.Inventory.Set(ResourceIds.Stone, BuildingCatalog.Wall.Cost);
                var presentation = new RecordingPresentation();
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
                    0,
                    0,
                    4,
                    4,
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
                    true);

                Assert.That(
                    session.TryBeginConstruction(
                        request,
                        presentation,
                        out GrayboxBuildingInstance3D instance,
                        out BuildingPlacementEvaluation evaluation),
                    Is.True,
                    evaluation.PrimaryFailure.ToString());

                session.TickConstruction(
                    .4f,
                    CityMode.Fortress,
                    false,
                    presentation);

                Assert.That(session.ProductivityMultiplier, Is.EqualTo(1.25f));
                Assert.That(
                    instance.Progress.Remaining,
                    Is.EqualTo(1f).Within(.0001f),
                    "0.4s × 1.25 gameplay productivity × 2x development rule time must advance 1 base second.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
