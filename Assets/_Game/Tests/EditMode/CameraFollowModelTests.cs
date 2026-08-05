using NUnit.Framework;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class CameraFollowModelTests
    {
        [Test]
        public void NewModelDefaultsToFollowingCity()
        {
            var model = new CameraFollowModel();

            Assert.That(model.Mode, Is.EqualTo(CameraFollowMode.Following));
            Assert.That(model.Target, Is.EqualTo(DirectControlTarget.City));
        }

        [Test]
        public void BeginFreeDragEntersFreeAndReleaseStaysFree()
        {
            var model = new CameraFollowModel();

            model.BeginFreeDrag();
            model.EndFreeDrag();

            Assert.That(model.Mode, Is.EqualTo(CameraFollowMode.Free));
        }

        [Test]
        public void ReturnToTargetRestoresFollowing()
        {
            var model = new CameraFollowModel();
            model.BeginFreeDrag();

            model.ReturnToTarget();

            Assert.That(model.Mode, Is.EqualTo(CameraFollowMode.Following));
        }

        [TestCase(DirectControlTarget.City, true, DirectControlTarget.City)]
        [TestCase(DirectControlTarget.Leader, true, DirectControlTarget.Leader)]
        [TestCase(DirectControlTarget.Leader, false, DirectControlTarget.City)]
        public void ObserveTargetSelectsAvailableCityOrLeader(
            DirectControlTarget requested,
            bool leaderAvailable,
            DirectControlTarget expected)
        {
            var model = new CameraFollowModel();

            model.ObserveTarget(requested, leaderAvailable);

            Assert.That(model.Target, Is.EqualTo(expected));
        }

        [Test]
        public void EffectiveTargetChangeRestoresFollowing()
        {
            var model = new CameraFollowModel();
            model.BeginFreeDrag();

            bool changed = model.ObserveTarget(
                DirectControlTarget.Leader,
                leaderTargetAvailable: true);

            Assert.That(changed, Is.True);
            Assert.That(model.Target, Is.EqualTo(DirectControlTarget.Leader));
            Assert.That(model.Mode, Is.EqualTo(CameraFollowMode.Following));
        }

        [Test]
        public void SameEffectiveTargetDoesNotCancelFreeMode()
        {
            var model = new CameraFollowModel();
            model.BeginFreeDrag();

            bool changed = model.ObserveTarget(
                DirectControlTarget.Leader,
                leaderTargetAvailable: false);

            Assert.That(changed, Is.False);
            Assert.That(model.Target, Is.EqualTo(DirectControlTarget.City));
            Assert.That(model.Mode, Is.EqualTo(CameraFollowMode.Free));
        }
    }
}
