using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class IDEA0027DefenseTowerCatalogTests
    {
        [TestCase("psionics.building.mind-spire")]
        [TestCase("biological.building.acid-tower")]
        public void IDEA0027_NewFormalStatusTowersHaveConfiguredLocalCapacity(
            string buildingId)
        {
            DefenseTowerDefinition definition =
                DefenseTowerCatalog.For(buildingId);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.LocalCapacity, Is.EqualTo(30));
        }
    }
}
