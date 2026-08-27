using System;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxElixirUseCommand3DTests
    {
        [Test]
        public void SessionSamplesAreStableByUseOrdinalAndAdvanceOnlyOnCommit()
        {
            const string sessionId = "session.idea0021.elixir";
            var sequence = new ElixirSessionMutationSequence3D(
                () => sessionId);

            int first = sequence.PeekSamplePercent();
            Assert.That(first, Is.EqualTo(
                ElixirSessionMutationSequence3D.SamplePercent(sessionId, 0)));
            Assert.That(sequence.PeekSamplePercent(), Is.EqualTo(first));
            Assert.That(sequence.UseOrdinal, Is.Zero);

            sequence.CommitUse();
            int second = sequence.PeekSamplePercent();
            Assert.That(sequence.UseOrdinal, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(
                ElixirSessionMutationSequence3D.SamplePercent(sessionId, 1)));
            Assert.That(EnumerableSamples(sessionId), Has.Some.Not.EqualTo(first));
        }

        [Test]
        public void OrdinaryUseConsumesNetworkStockAndAppliesBaseHealing()
        {
            using var storage = StorageWithElixir(1);
            var health = new FakeHealthAuthority(
                600, 1000,
                new GrayboxElixirBuildingHealthSnapshot3D(100, 300));

            GrayboxElixirUseResult3D result =
                GrayboxElixirUseCommand3D.TryUse(
                    storage, health, false, 19);

            Assert.That(result.Status,
                Is.EqualTo(GrayboxElixirUseStatus3D.Used));
            Assert.That(result.CoreHealing, Is.EqualTo(250));
            Assert.That(result.BuildingHealing, Is.EqualTo(100));
            Assert.That(result.BacklashDamage, Is.Zero);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Elixir), Is.Zero);
            Assert.That(health.CoreCurrent, Is.EqualTo(850));
            Assert.That(health.BuildingCurrent, Is.EqualTo(200));
            Assert.That(result.Message,
                Does.Contain("核心 +250").And.Contain("建筑 +100"));
        }

        [Test]
        public void FleshElixirTriplesHealingAndAppliesSampledBacklash()
        {
            using var storage = StorageWithElixir(1);
            var health = new FakeHealthAuthority(
                200, 1000,
                new GrayboxElixirBuildingHealthSnapshot3D(50, 300));

            GrayboxElixirUseResult3D result =
                GrayboxElixirUseCommand3D.TryUse(
                    storage, health, true, 19);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.CoreHealing, Is.EqualTo(750));
            Assert.That(result.BuildingHealing, Is.EqualTo(250));
            Assert.That(result.BacklashDamage, Is.EqualTo(150));
            Assert.That(health.CoreCurrent, Is.EqualTo(800));
            Assert.That(health.BuildingCurrent, Is.EqualTo(300));
            Assert.That(result.Message,
                Does.Contain("血肉灵丹").And.Contain("反噬 -150"));
        }

        [TestCase(0, 600, 100, GrayboxElixirUseStatus3D.MissingElixir)]
        [TestCase(1, 1000, 300, GrayboxElixirUseStatus3D.NothingToHeal)]
        public void FailedUseDoesNotSpendOrMutate(
            int stock,
            int coreCurrent,
            int buildingCurrent,
            GrayboxElixirUseStatus3D expected)
        {
            using var storage = StorageWithElixir(stock);
            var health = new FakeHealthAuthority(
                coreCurrent, 1000,
                new GrayboxElixirBuildingHealthSnapshot3D(
                    buildingCurrent, 300));

            GrayboxElixirUseResult3D result =
                GrayboxElixirUseCommand3D.TryUse(
                    storage, health, false, 99);

            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Elixir),
                Is.EqualTo(stock));
            Assert.That(health.CoreCurrent, Is.EqualTo(coreCurrent));
            Assert.That(health.BuildingCurrent, Is.EqualTo(buildingCurrent));
            Assert.That(health.ApplyCount, Is.Zero);
        }

        private static int[] EnumerableSamples(string sessionId)
        {
            var result = new int[8];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] =
                    ElixirSessionMutationSequence3D.SamplePercent(
                        sessionId, index);
            }
            return result;
        }

        private static CityResourceStorageModel StorageWithElixir(int amount)
        {
            var storage = new CityResourceStorageModel(
                new ResourceInventory(150));
            if (amount > 0)
                Assert.That(storage.AddToNetwork(ResourceIds.Elixir, amount),
                    Is.EqualTo(amount));
            return storage;
        }

        private sealed class FakeHealthAuthority :
            IGrayboxElixirHealthAuthority3D
        {
            private readonly int coreMaximum;
            private readonly int buildingMaximum;

            public FakeHealthAuthority(
                int coreCurrent,
                int coreMaximum,
                GrayboxElixirBuildingHealthSnapshot3D building)
            {
                CoreCurrent = coreCurrent;
                this.coreMaximum = coreMaximum;
                BuildingCurrent = building.Current;
                buildingMaximum = building.Maximum;
            }

            public int CoreCurrent { get; private set; }
            public int BuildingCurrent { get; private set; }
            public int ApplyCount { get; private set; }

            public bool TryCaptureElixirHealth(
                out GrayboxElixirHealthSnapshot3D snapshot)
            {
                snapshot = new GrayboxElixirHealthSnapshot3D(
                    CoreCurrent,
                    coreMaximum,
                    new[]
                    {
                        new GrayboxElixirBuildingHealthSnapshot3D(
                            BuildingCurrent, buildingMaximum),
                    });
                return true;
            }

            public void ApplyElixirHealth(
                int coreHealing,
                int buildingHealing,
                int coreBacklashDamage)
            {
                ApplyCount++;
                CoreCurrent = Math.Min(
                    coreMaximum,
                    CoreCurrent + coreHealing);
                CoreCurrent = Math.Max(
                    0,
                    CoreCurrent - coreBacklashDamage);
                BuildingCurrent = Math.Min(
                    buildingMaximum,
                    BuildingCurrent + buildingHealing);
            }
        }
    }
}
