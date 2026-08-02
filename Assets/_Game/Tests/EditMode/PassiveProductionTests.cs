using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class PassiveProductionTests
    {
        [Test] public void PassiveProductionWaitsForItsInterval(){var inventory=new ResourceInventory(100);var process=new PassiveProductionProcess(ResourceIds.EnergyCrystal,1,6f);Assert.That(process.Tick(5.9f,inventory,1,1f),Is.Zero);Assert.That(process.Tick(.1f,inventory,1,1f),Is.EqualTo(1));Assert.That(inventory.Get(ResourceIds.EnergyCrystal),Is.EqualTo(1));}
        [Test] public void FormationMultiplierAddsFiftyPercentProductionRate(){var inventory=new ResourceInventory(100);var process=new PassiveProductionProcess(ResourceIds.EnergyCrystal,1,6f);Assert.That(process.Tick(12f,inventory,1,1.5f),Is.EqualTo(3));Assert.That(inventory.Get(ResourceIds.EnergyCrystal),Is.EqualTo(3));}
        [Test] public void NoConnectedBuildingProducesNothing(){var inventory=new ResourceInventory(100);var process=new PassiveProductionProcess(ResourceIds.EnergyCrystal,1,6f);Assert.That(process.Tick(20f,inventory,0,1.5f),Is.Zero);Assert.That(inventory.Get(ResourceIds.EnergyCrystal),Is.Zero);}
    }
}
