using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Population;

namespace WasteCity.Tests
{
    public sealed class PopulationAndCapacityTests
    {
        [Test] public void InitialPopulationProducesOneHundredPercentProductivity()
        { var model = new PopulationModel(); Assert.That(model.Current, Is.EqualTo(100)); Assert.That(model.Capacity, Is.EqualTo(150)); Assert.That(model.ProductivityMultiplier, Is.EqualTo(1f)); }
        [Test] public void PopulationOverCapacityWaitsAndDoesNotIncreaseProductivity()
        { var model = new PopulationModel(); model.AddPeople(80); Assert.That(model.EffectiveWorkers, Is.EqualTo(150)); Assert.That(model.Waiting, Is.EqualTo(30)); Assert.That(model.ProductivityMultiplier, Is.EqualTo(1.25f)); }
        [Test] public void HousingCapacityMakesWaitingPopulationEffective()
        { var model = new PopulationModel(180, 150); model.AddCapacity(50); Assert.That(model.Waiting, Is.Zero); Assert.That(model.EffectiveWorkers, Is.EqualTo(180)); }
        [Test] public void WarehouseCapacityCanExpandAndShrinkInventory()
        { var inventory = new ResourceInventory(150); inventory.AddCapacity(150); inventory.Add(ResourceIds.Iron, 280); Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(280)); inventory.AddCapacity(-150); Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(150)); }
    }
}
