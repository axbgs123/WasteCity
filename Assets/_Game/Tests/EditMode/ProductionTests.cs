using NUnit.Framework;
using WasteCity.Economy;
namespace WasteCity.Tests
{
 public sealed class ProductionTests
 {
  [Test] public void SmeltingConsumesIronAndCreatesAlloy(){var i=new ResourceInventory(100);i.Add(ResourceIds.Iron,4);var p=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.Alloy,1,6));Assert.That(p.Tick(6,i,1),Is.EqualTo(1));Assert.That(i.Get(ResourceIds.Iron),Is.EqualTo(2));Assert.That(i.Get(ResourceIds.Alloy),Is.EqualTo(1));}
  [Test] public void NoBuildingMeansNoProduction(){var i=new ResourceInventory(100);i.Add(ResourceIds.Iron,10);var p=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.Alloy,1,6));Assert.That(p.Tick(60,i,0),Is.Zero);}
 }
}
