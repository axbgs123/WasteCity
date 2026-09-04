using NUnit.Framework;
using WasteCity.City;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Leader.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029LeaderControlRuntimeTests
    {
        [Test]
        public void NewRuntimeDefaultsToAiAndFortressManualRequiresRequest()
        {
            var runtime = new LeaderControlRuntime();

            LeaderControlResolution before = runtime.Resolve(
                CityMode.Fortress,
                leaderRecruited: true,
                CharacterLifeState.Active,
                modalBlocksWorldInteraction: false);
            Assert.That(runtime.RequestedMode, Is.EqualTo(LeaderControlMode.AI));
            Assert.That(before.ActualMode, Is.EqualTo(LeaderControlMode.AI));
            Assert.That(before.ControlTarget, Is.EqualTo(DirectControlTarget.City));

            Assert.That(
                runtime.TryRequest(LeaderControlMode.Manual, out string error),
                Is.True,
                error);
            LeaderControlResolution after = runtime.Resolve(
                CityMode.Fortress,
                leaderRecruited: true,
                CharacterLifeState.Active,
                modalBlocksWorldInteraction: false);
            Assert.That(after.ActualMode, Is.EqualTo(LeaderControlMode.Manual));
            Assert.That(after.ControlTarget, Is.EqualTo(DirectControlTarget.Leader));
            Assert.That(after.BlockReason, Is.EqualTo(LeaderControlBlockReason.None));
        }

        [TestCase(CityMode.Mobile)]
        [TestCase(CityMode.Deploying)]
        [TestCase(CityMode.Packing)]
        public void NonFortressKeepsRequestButResolvesToCityAndAi(CityMode mode)
        {
            var runtime = new LeaderControlRuntime();
            Assert.That(runtime.TryRequest(LeaderControlMode.Manual, out _), Is.True);

            LeaderControlResolution result = runtime.Resolve(
                mode,
                leaderRecruited: true,
                CharacterLifeState.Active,
                modalBlocksWorldInteraction: false);

            Assert.That(runtime.RequestedMode, Is.EqualTo(LeaderControlMode.Manual));
            Assert.That(result.ActualMode, Is.EqualTo(LeaderControlMode.AI));
            Assert.That(result.ControlTarget, Is.EqualTo(DirectControlTarget.City));
            Assert.That(
                result.BlockReason,
                Is.EqualTo(LeaderControlBlockReason.CityNotFortress));
        }

        [TestCase(false, CharacterLifeState.Active, LeaderControlBlockReason.NotRecruited)]
        [TestCase(true, CharacterLifeState.Downed, LeaderControlBlockReason.LeaderNotActive)]
        [TestCase(true, CharacterLifeState.Recovering, LeaderControlBlockReason.LeaderNotActive)]
        [TestCase(true, CharacterLifeState.Dead, LeaderControlBlockReason.LeaderNotActive)]
        public void InvalidLeaderQualificationForcesAi(
            bool recruited,
            CharacterLifeState state,
            LeaderControlBlockReason expectedReason)
        {
            var runtime = new LeaderControlRuntime();
            runtime.TryRequest(LeaderControlMode.Manual, out _);

            LeaderControlResolution result = runtime.Resolve(
                CityMode.Fortress,
                recruited,
                state,
                modalBlocksWorldInteraction: false);

            Assert.That(result.ActualMode, Is.EqualTo(LeaderControlMode.AI));
            Assert.That(result.ControlTarget, Is.EqualTo(DirectControlTarget.City));
            Assert.That(result.BlockReason, Is.EqualTo(expectedReason));
        }

        [Test]
        public void ModalBlocksActualManualWithoutDiscardingSavedIntent()
        {
            var runtime = new LeaderControlRuntime();
            runtime.TryRequest(LeaderControlMode.Manual, out _);

            LeaderControlResolution blocked = runtime.Resolve(
                CityMode.Fortress,
                true,
                CharacterLifeState.Active,
                modalBlocksWorldInteraction: true);
            LeaderControlResolution restored = runtime.Resolve(
                CityMode.Fortress,
                true,
                CharacterLifeState.Active,
                modalBlocksWorldInteraction: false);

            Assert.That(blocked.ActualMode, Is.EqualTo(LeaderControlMode.AI));
            Assert.That(
                blocked.BlockReason,
                Is.EqualTo(LeaderControlBlockReason.ModalBlocked));
            Assert.That(restored.ActualMode, Is.EqualTo(LeaderControlMode.Manual));
        }

        [Test]
        public void RestoreRejectsUnknownModeWithoutChangingCurrentIntent()
        {
            var runtime = new LeaderControlRuntime();
            runtime.TryRequest(LeaderControlMode.Manual, out _);

            Assert.That(
                runtime.TryRestore((LeaderControlMode)99, out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.RequestedMode, Is.EqualTo(LeaderControlMode.Manual));
        }
    }
}
