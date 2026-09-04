using System;
using NUnit.Framework;
using WasteCity.City;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Leader.Exploration;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029ExplorationPerformanceTests
    {
        [Test]
        public void StableExplorationAndLeaderRulesAllocateZeroAcross300Ticks()
        {
            var exploration = new WorldExplorationRuntime(
                64,
                48,
                "performance-session",
                (_, __) => true);
            var source = new WorldVisionSource(
                "core.city.primary",
                WorldVisionSourceKind.PrimaryCity,
                20,
                20,
                true,
                1ul);
            exploration.UpsertSource(source);
            var control = new LeaderControlRuntime();
            control.TryRequest(LeaderControlMode.Manual, out _);
            var ai = new LeaderAiContext(
                CityMode.Fortress,
                LeaderControlMode.AI,
                3f,
                3f,
                4f,
                4f,
                true);

            exploration.UpsertSource(source);
            exploration.GetState(20, 20);
            control.Resolve(
                CityMode.Fortress,
                true,
                CharacterLifeState.Active,
                false);
            LeaderAiRules.Resolve(ai);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
            {
                exploration.UpsertSource(source);
                exploration.GetState(20, 20);
                control.Resolve(
                    CityMode.Fortress,
                    true,
                    CharacterLifeState.Active,
                    false);
                LeaderAiRules.Resolve(ai);
            }
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
            Assert.That(exploration.VisibilityRevision, Is.EqualTo(1ul));
            Assert.That(control.Revision, Is.EqualTo(1ul));
        }
    }
}
