using System.Linq;
using NUnit.Framework;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Tests.EditMode
{
    public sealed class IDEA0022CharacterCatalogTests
    {
        [Test]
        public void F5ACharacterCatalogHasThreeStablePersistentCharacters()
        {
            Assert.That(CharacterCatalog.All.Count, Is.EqualTo(3));
            Assert.That(
                CharacterCatalog.All.Select(item => item.Id.Value).Distinct().Count(),
                Is.EqualTo(3));
            Assert.That(CharacterCatalog.CenJin.DisplayName, Is.EqualTo("岑烬"));
            Assert.That(CharacterCatalog.LinXi.DisplayName, Is.EqualTo("林溪"));
            Assert.That(CharacterCatalog.HanGu.DisplayName, Is.EqualTo("韩骨"));
            Assert.That(CharacterCatalog.CenJin.VisualId.Value,
                Is.EqualTo("art.character.cen-jin"));
            Assert.That(CharacterCatalog.CenJin.Prestige, Is.EqualTo(70));
            Assert.That(CharacterCatalog.CenJin.InitialLoyalty, Is.EqualTo(80));
            Assert.That(CharacterCatalog.LinXi.Prestige, Is.EqualTo(55));
            Assert.That(CharacterCatalog.LinXi.InitialLoyalty, Is.EqualTo(75));
            Assert.That(CharacterCatalog.HanGu.Prestige, Is.EqualTo(65));
            Assert.That(CharacterCatalog.HanGu.InitialLoyalty, Is.EqualTo(55));
        }

        [Test]
        public void DefinitionsProvideSpecializationPrestigeRouteAndEquipment()
        {
            foreach (CharacterDefinition definition in CharacterCatalog.All)
            {
                Assert.That(definition.Specialization, Is.Not.Empty);
                Assert.That(definition.Prestige, Is.InRange(0, 100));
                Assert.That(definition.RouteInclinationId.Value, Is.Not.Empty);
                Assert.That(definition.MaximumHealth, Is.GreaterThan(0));
                Assert.That(definition.InitialEquipmentIds, Is.Not.Empty);
                Assert.That(CharacterCatalog.Find(definition.Id.Value),
                    Is.SameAs(definition));
            }
        }
    }
}
