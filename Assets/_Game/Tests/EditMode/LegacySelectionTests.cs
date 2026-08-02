using System.Linq;
using NUnit.Framework;
using WasteCity.Legacy;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class LegacySelectionTests
    {
        [Test]
        public void SeededChoicesAreUniqueAndDeterministic()
        {
            var a = new LegacySelectionModel(new WorldSeed(8128)); var b = new LegacySelectionModel(new WorldSeed(8128));
            Assert.That(a.Choices.Select(x => x.Id.Value).Distinct().Count(), Is.EqualTo(3));
            Assert.That(a.Choices[0].Id, Is.EqualTo(b.Choices[0].Id));
        }
        [Test]
        public void SelectionCannotBeReplaced()
        {
            var model = new LegacySelectionModel(new WorldSeed(1));
            Assert.That(model.Select(1), Is.True); Assert.That(model.Select(2), Is.False);
        }
        [Test]
        public void SelectedLegacyCanUpgradeAndRestoreItsLevel()
        {
            var model = new LegacySelectionModel(new WorldSeed(1)); model.Select(0);
            Assert.That(model.Upgrade(), Is.True); Assert.That(model.Level, Is.EqualTo(2));
            var restored = new LegacySelectionModel(new WorldSeed(1)); Assert.That(restored.Restore(model.Selected.Id.Value, 2), Is.True); Assert.That(restored.Level, Is.EqualTo(2));
        }
    }
}
