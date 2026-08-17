using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ResourceDefinitionCatalogTests
    {
        private static readonly string[] ExpectedStableOrder =
        {
            ResourceIds.Iron,
            ResourceIds.EnergyCrystal,
            ResourceIds.Stone,
            ResourceIds.Biomass,
            ResourceIds.Water,
            ResourceIds.Alloy,
            ResourceIds.Ammunition,
            ResourceIds.SpiritIron,
            ResourceIds.FlyingSword,
            ResourceIds.BoneSteel,
            ResourceIds.BiomassConcentrate,
            ResourceIds.BiologicalWeapon,
            ResourceIds.ResonanceMetal,
            ResourceIds.PsionicAmplifier,
            ResourceIds.Elixir
        };

        [Test]
        public void All_MatchesTheExactFifteenStableResourceIds()
        {
            Assert.That(ResourceIds.All, Has.Length.EqualTo(15));
            CollectionAssert.AreEqual(ExpectedStableOrder, ResourceIds.All);
            Assert.That(
                ResourceIds.All.Distinct().Count(),
                Is.EqualTo(ResourceIds.All.Length));

            Assert.That(ResourceDefinitionCatalog.All, Has.Count.EqualTo(15));
            CollectionAssert.AreEqual(
                ExpectedStableOrder,
                ResourceDefinitionCatalog.All.Select(value => value.Id).ToArray());
            Assert.That(
                ResourceDefinitionCatalog.All.Select(value => value.Id).Distinct().Count(),
                Is.EqualTo(ResourceDefinitionCatalog.All.Count));
        }

        [TestCase(ResourceIds.Iron, "铁矿")]
        [TestCase(ResourceIds.EnergyCrystal, "能晶")]
        [TestCase(ResourceIds.Stone, "石料")]
        [TestCase(ResourceIds.Biomass, "生物质")]
        [TestCase(ResourceIds.Water, "水")]
        [TestCase(ResourceIds.Alloy, "合金")]
        [TestCase(ResourceIds.Ammunition, "弹药")]
        [TestCase(ResourceIds.SpiritIron, "灵铁")]
        [TestCase(ResourceIds.FlyingSword, "飞剑")]
        [TestCase(ResourceIds.BoneSteel, "骨钢")]
        [TestCase(ResourceIds.BiomassConcentrate, "生物质浓缩液")]
        [TestCase(ResourceIds.BiologicalWeapon, "生物武器")]
        [TestCase(ResourceIds.ResonanceMetal, "共振金属")]
        [TestCase(ResourceIds.PsionicAmplifier, "灵能增幅器")]
        [TestCase(ResourceIds.Elixir, "灵丹")]
        public void EveryFormalResource_HasApprovedNameAndStackLimit(
            string resourceId,
            string expectedChineseName)
        {
            Assert.That(
                ResourceDefinitionCatalog.TryGet(resourceId, out ResourceDefinition definition),
                Is.True);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Id, Is.EqualTo(resourceId));
            Assert.That(definition.ChineseName, Is.EqualTo(expectedChineseName));
            Assert.That(definition.StackLimit, Is.EqualTo(100));
        }

        [Test]
        public void EveryFormalResource_HasANonEmptyUniqueStableIconFallbackKey()
        {
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                Assert.That(definition.IconFallbackKey, Is.Not.Null.And.Not.Empty);
                Assert.That(definition.IconFallbackKey, Is.EqualTo(definition.Id));
            }

            Assert.That(
                ResourceDefinitionCatalog.All
                    .Select(definition => definition.IconFallbackKey)
                    .Distinct()
                    .Count(),
                Is.EqualTo(ResourceDefinitionCatalog.All.Count));
        }

        [TestCase(ResourceIds.Iron, 20)]
        [TestCase(ResourceIds.EnergyCrystal, 0)]
        [TestCase(ResourceIds.Stone, 0)]
        [TestCase(ResourceIds.Biomass, 10)]
        [TestCase(ResourceIds.Water, 0)]
        [TestCase(ResourceIds.Alloy, 20)]
        [TestCase(ResourceIds.Ammunition, 30)]
        [TestCase(ResourceIds.SpiritIron, 0)]
        [TestCase(ResourceIds.FlyingSword, 0)]
        [TestCase(ResourceIds.BoneSteel, 0)]
        [TestCase(ResourceIds.BiomassConcentrate, 0)]
        [TestCase(ResourceIds.BiologicalWeapon, 0)]
        [TestCase(ResourceIds.ResonanceMetal, 0)]
        [TestCase(ResourceIds.PsionicAmplifier, 0)]
        [TestCase(ResourceIds.Elixir, 0)]
        public void EveryFormalResource_HasTheExactFormalInitialCityAmount(
            string resourceId,
            int expectedAmount)
        {
            Assert.That(
                ResourceDefinitionCatalog.TryGet(resourceId, out ResourceDefinition definition),
                Is.True);
            Assert.That(definition.FormalInitialCityAmount, Is.EqualTo(expectedAmount));
        }

        [Test]
        public void CreateFormalCityInventory_LoadsInitialAmountsIntoAWarehouseReadyLedger()
        {
            ResourceInventory inventory =
                ResourceDefinitionCatalog.CreateFormalCityInventory();

            Assert.That(inventory, Is.Not.Null);
            Assert.That(
                inventory.CapacityPerResource,
                Is.GreaterThanOrEqualTo(300));

            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                Assert.That(
                    inventory.Get(definition.Id),
                    Is.EqualTo(definition.FormalInitialCityAmount),
                    definition.Id);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("unknown.resource.not-registered")]
        public void TryGet_EmptyOrUnknownIdFailsClosed(string resourceId)
        {
            Assert.That(
                ResourceDefinitionCatalog.TryGet(resourceId, out ResourceDefinition definition),
                Is.False);
            Assert.That(definition, Is.Null);
        }

        [Test]
        public void BaseHudResourceIds_PreserveTheApprovedPermanentOrder()
        {
            string[] expected =
            {
                ResourceIds.Iron,
                ResourceIds.EnergyCrystal,
                ResourceIds.Stone,
                ResourceIds.Biomass,
                ResourceIds.Water
            };

            CollectionAssert.AreEqual(expected, ResourceIds.Base);
            CollectionAssert.AreEqual(
                expected,
                ResourceDefinitionCatalog.BaseHudResourceIds);
        }
    }
}
