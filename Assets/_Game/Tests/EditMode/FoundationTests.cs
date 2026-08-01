using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FoundationTests
    {
        [Test] public void StableIdRejectsUnscopedValue() => Assert.Throws<System.ArgumentException>(() => new StableId("iron"));
        [Test] public void InventoryCapsEachFormalResourceIndependently()
        {
            var inventory = new ResourceInventory(100);
            Assert.That(inventory.Add(ResourceIds.Iron, 120), Is.EqualTo(100));
            Assert.That(inventory.Add(ResourceIds.Water, 40), Is.EqualTo(40));
        }
        [Test] public void HeavyArmorUsesFormalPhysicalModifier() => Assert.That(DamageMatrix.Apply(100, DamageType.Physical, ArmorType.Heavy), Is.EqualTo(70));
        [Test] public void WorldSeedIsDeterministic()
        {
            var a = new WorldSeed(8128); var b = new WorldSeed(8128);
            Assert.That(a.Sample(12, 7, 2), Is.EqualTo(b.Sample(12, 7, 2)));
        }
    }
}
