using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class FriendlyUnitCommandTests
    {
        [Test]
        public void DefaultRallyFollowsLatestCityPosition()
        {
            var model = new FriendlyUnitCommandModel();

            FriendlyRallyPoint first = model.ResolveRally(3f, -2f);
            FriendlyRallyPoint moved = model.ResolveRally(8f, 5f);

            Assert.That(first.X, Is.EqualTo(3f));
            Assert.That(first.Y, Is.EqualTo(-2f));
            Assert.That(moved.X, Is.EqualTo(8f));
            Assert.That(moved.Y, Is.EqualTo(5f));
            Assert.That(model.HasFixedRally, Is.False);
        }

        [Test]
        public void SetRallyKeepsPointFixedWhenCityMoves()
        {
            var model = new FriendlyUnitCommandModel();
            model.SetRally(-4f, 7f);

            FriendlyRallyPoint resolved = model.ResolveRally(20f, 30f);

            Assert.That(resolved.X, Is.EqualTo(-4f));
            Assert.That(resolved.Y, Is.EqualTo(7f));
            Assert.That(model.HasFixedRally, Is.True);
        }

        [Test]
        public void ClearRallyResumesFollowingCity()
        {
            var model = new FriendlyUnitCommandModel();
            model.SetRally(-4f, 7f);
            model.ClearRally();

            FriendlyRallyPoint resolved = model.ResolveRally(6f, 9f);

            Assert.That(resolved.X, Is.EqualTo(6f));
            Assert.That(resolved.Y, Is.EqualTo(9f));
            Assert.That(model.HasFixedRally, Is.False);
        }

        [Test]
        public void LossesAreCountedIndependentlyByUnitKind()
        {
            var model = new FriendlyUnitCommandModel();

            model.RecordLoss(FriendlyUnitKind.Puppet);
            model.RecordLoss(FriendlyUnitKind.Behemoth);
            model.RecordLoss(FriendlyUnitKind.Controlled);
            model.RecordLoss(FriendlyUnitKind.Controlled);

            Assert.That(model.PuppetLosses, Is.EqualTo(1));
            Assert.That(model.BehemothLosses, Is.EqualTo(1));
            Assert.That(model.ControlledLosses, Is.EqualTo(2));
            Assert.That(model.TotalLosses, Is.EqualTo(4));
        }

        [Test]
        public void RestoreClampsNegativeLossesToZero()
        {
            var model = new FriendlyUnitCommandModel();

            model.Restore(true, 2f, 4f, -1, -2, -3);

            Assert.That(model.HasFixedRally, Is.True);
            Assert.That(model.ResolveRally(0f, 0f).X, Is.EqualTo(2f));
            Assert.That(model.ResolveRally(0f, 0f).Y, Is.EqualTo(4f));
            Assert.That(model.PuppetLosses, Is.Zero);
            Assert.That(model.BehemothLosses, Is.Zero);
            Assert.That(model.ControlledLosses, Is.Zero);
        }
    }
}
