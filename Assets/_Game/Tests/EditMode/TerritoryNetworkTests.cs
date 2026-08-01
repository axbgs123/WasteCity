using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Legacy;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class TerritoryNetworkTests
    {
        [Test] public void NormalTerritoryStoresResourcesLocallyUntilCollected(){var city=new ResourceInventory(100);var n=new TerritoryResourceNetwork(city);n.Deposit(ResourceIds.Iron,12,false);Assert.That(city.Get(ResourceIds.Iron),Is.Zero);Assert.That(n.Local(ResourceIds.Iron),Is.EqualTo(12));Assert.That(n.Collect(ResourceIds.Iron),Is.EqualTo(12));Assert.That(city.Get(ResourceIds.Iron),Is.EqualTo(12));}
        [Test] public void QuantumEntanglementDepositsDirectlyIntoCityInventory(){var city=new ResourceInventory(100);var n=new TerritoryResourceNetwork(city);n.Deposit(ResourceIds.Water,9,true);Assert.That(city.Get(ResourceIds.Water),Is.EqualTo(9));Assert.That(n.Local(ResourceIds.Water),Is.Zero);}
        [Test] public void TerritoryExtractionProducesOneCycleEveryThreeSeconds(){var m=new TerritoryExtractionModel();Assert.That(m.Tick(2.9f),Is.Zero);Assert.That(m.Tick(.2f),Is.EqualTo(1));Assert.That(m.Progress,Is.InRange(.09f,.11f));}
        [Test] public void WorldFogAndDepletionRoundTrip(){var world=new WorldMapModel(8,8,new WorldSeed(22));world.Reveal(2,2,2);var amounts=world.CaptureResourceAmounts();var visible=world.CaptureRevealed();for(int y=0;y<8;y++)for(int x=0;x<8;x++)if(world.Get(x,y).HasResource){world.Harvest(x,y,5,out _);goto restore;}restore:Assert.That(world.Restore(amounts,visible),Is.True);Assert.That(world.CaptureResourceAmounts(),Is.EqualTo(amounts));Assert.That(world.CaptureRevealed(),Is.EqualTo(visible));}
    }
}
