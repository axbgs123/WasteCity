using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class AlchemyTests
    {
        [Test] public void ElixirConsumesOneAndHealsCoreAndBuildings(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Elixir,1);var core=new HealthModel(1000);core.Restore(600);var building=new HealthModel(300);building.Restore(100);Assert.That(ElixirUseModel.TryUse(inventory,core,new[]{building}),Is.True);Assert.That(inventory.Get(ResourceIds.Elixir),Is.Zero);Assert.That(core.Current,Is.EqualTo(850));Assert.That(building.Current,Is.EqualTo(200));}
        [Test] public void ElixirCannotBeUsedWithoutStock(){var inventory=new ResourceInventory(100);var core=new HealthModel(1000);core.Restore(500);Assert.That(ElixirUseModel.TryUse(inventory,core,System.Array.Empty<HealthModel>()),Is.False);Assert.That(core.Current,Is.EqualTo(500));}
        [Test] public void ElixirIsNotWastedWhenEverythingIsHealthy(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Elixir,1);Assert.That(ElixirUseModel.TryUse(inventory,new HealthModel(1000),new[]{new HealthModel(100)}),Is.False);Assert.That(inventory.Get(ResourceIds.Elixir),Is.EqualTo(1));}
        [Test] public void AlchemyRecipeConsumesBiomassAndCrystal(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Biomass,1);inventory.Add(ResourceIds.EnergyCrystal,1);var process=new DualInputProductionProcess(new DualInputProductionRecipe(ResourceIds.Biomass,1,ResourceIds.EnergyCrystal,1,ResourceIds.Elixir,1,10f));Assert.That(process.Tick(10f,inventory,1),Is.EqualTo(1));Assert.That(inventory.Get(ResourceIds.Elixir),Is.EqualTo(1));Assert.That(inventory.Get(ResourceIds.Biomass),Is.Zero);Assert.That(inventory.Get(ResourceIds.EnergyCrystal),Is.Zero);}
    }
}
