using System;
using System.Collections;
using NUnit.Framework;
using Unity.Profiling;
using WasteCity.Building;
using WasteCity.City;

namespace WasteCity.Tests
{
    public sealed class BuildingMobilityRulesTests
    {
        public static IEnumerable CatalogTags
        {
            get
            {
                yield return Case(BuildingCatalog.MiningStation, BuildingPlacement.Ground, BuildingOperation.TerrainDependent);
                yield return Case(BuildingCatalog.Housing, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.Warehouse, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.Wall, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.ResearchStation, BuildingPlacement.Either, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.Smelter, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.Assembler, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.MachineGunTurret, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.HeavyMachineGunTurret, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.PowerPlant, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.SpiritFireFurnace, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.ArtifactWorkshop, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.SwordArrayTower, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.SwordRidingPlatform, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.ColonyPool, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.BreedingChamber, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.SporeTower, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.MetabolicFurnace, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.ResonanceFurnace, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.PsionicWorkshop, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.MindSpire, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.ConsciousnessNetwork, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.LaserTower, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.AcidTower, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
                yield return Case(BuildingCatalog.ShieldGenerator, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.SpiritGatheringArray, BuildingPlacement.Ground, BuildingOperation.TerrainDependent);
                yield return Case(BuildingCatalog.AutomatedRepairBay, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.AlchemyChamber, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.PuppetWorkshop, BuildingPlacement.Either, BuildingOperation.MobileAllowed);
                yield return Case(BuildingCatalog.BehemothPen, BuildingPlacement.Ground, BuildingOperation.FortressOnly);
            }
        }

        [TestCaseSource(nameof(CatalogTags))]
        public void CatalogCarriesApprovedPlacementAndOperationTags(
            BuildingDefinition definition,
            BuildingPlacement placement,
            BuildingOperation operation)
        {
            Assert.That(definition.Placement, Is.EqualTo(placement));
            Assert.That(definition.Operation, Is.EqualTo(operation));
        }

        [TestCase(CityMode.Mobile, true)]
        [TestCase(CityMode.Deploying, false)]
        [TestCase(CityMode.Fortress, true)]
        [TestCase(CityMode.Packing, false)]
        public void InnerCityMobileBuildingOnlyRunsInStableSupportedModes(
            CityMode mode,
            bool expected)
        {
            Assert.That(
                BuildingMobilityRules.CanOperate(
                    BuildingCatalog.Housing,
                    BuildingSite.InnerCity,
                    mode),
                Is.EqualTo(expected));
        }

        [Test]
        public void GroundFactoryOnlyRunsAndConstructsInFortress()
        {
            Assert.That(
                BuildingMobilityRules.CanConstruct(
                    BuildingCatalog.Smelter,
                    BuildingSite.Ground,
                    CityMode.Mobile),
                Is.False);
            Assert.That(
                BuildingMobilityRules.CanOperate(
                    BuildingCatalog.Smelter,
                    BuildingSite.Ground,
                    CityMode.Mobile),
                Is.False);
            Assert.That(
                BuildingMobilityRules.CanOperate(
                    BuildingCatalog.Smelter,
                    BuildingSite.Ground,
                    CityMode.Fortress),
                Is.True);
        }

        [Test]
        public void UnsupportedSiteNeverConstructsOrOperates()
        {
            Assert.That(
                BuildingMobilityRules.SupportsSite(
                    BuildingCatalog.Smelter,
                    BuildingSite.InnerCity),
                Is.False);
            Assert.That(
                BuildingMobilityRules.CanConstruct(
                    BuildingCatalog.Smelter,
                    BuildingSite.InnerCity,
                    CityMode.Fortress),
                Is.False);
            Assert.That(
                BuildingMobilityRules.CanOperate(
                    BuildingCatalog.Smelter,
                    BuildingSite.InnerCity,
                    CityMode.Fortress),
                Is.False);
        }

        [Test]
        public void BuildingSiteLegalValuesRemainExactlyGroundZeroAndInnerCityOne()
        {
            Assert.That((int)BuildingSite.Ground, Is.Zero);
            Assert.That((int)BuildingSite.InnerCity, Is.EqualTo(1));
            Assert.That(
                BuildingMobilityRules.SupportsSite(
                    BuildingCatalog.Housing,
                    BuildingSite.Ground),
                Is.True);
            Assert.That(
                BuildingMobilityRules.SupportsSite(
                    BuildingCatalog.Housing,
                    BuildingSite.InnerCity),
                Is.True);
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidBuildingSiteValuesNeverSupportOrConstruct(int rawSite)
        {
            BuildingSite invalidSite = (BuildingSite)rawSite;

            Assert.That(
                BuildingMobilityRules.SupportsSite(
                    BuildingCatalog.Housing,
                    invalidSite),
                Is.False);
            Assert.That(
                BuildingMobilityRules.CanConstruct(
                    BuildingCatalog.Housing,
                    invalidSite,
                    CityMode.Fortress),
                Is.False);
        }

        [Test]
        public void MobilityRulesProfilerRecordsZeroAcross300Calls()
        {
            Action supportsSiteCall = () =>
                BuildingMobilityRules.SupportsSite(
                    BuildingCatalog.Housing,
                    BuildingSite.InnerCity);
            Action canConstructCall = () =>
                BuildingMobilityRules.CanConstruct(
                    BuildingCatalog.Housing,
                    BuildingSite.InnerCity,
                    CityMode.Fortress);
            supportsSiteCall();
            canConstructCall();

            AllocationMeasurement supportsSite =
                Profile300Calls(supportsSiteCall);
            AllocationMeasurement canConstruct =
                Profile300Calls(canConstructCall);

            TestContext.WriteLine(
                "Task9SupportsSiteProfilerSamples=" +
                supportsSite.Samples);
            TestContext.WriteLine(
                "Task9SupportsSiteProfilerBytes=" +
                supportsSite.ProfiledBytes);
            TestContext.WriteLine(
                "Task9SupportsSiteCurrentThreadBytes=" +
                supportsSite.CurrentThreadBytes);
            TestContext.WriteLine(
                "Task9CanConstructProfilerSamples=" +
                canConstruct.Samples);
            TestContext.WriteLine(
                "Task9CanConstructProfilerBytes=" +
                canConstruct.ProfiledBytes);
            TestContext.WriteLine(
                "Task9CanConstructCurrentThreadBytes=" +
                canConstruct.CurrentThreadBytes);
            Assert.That(supportsSite.ProfiledBytes, Is.Zero);
            Assert.That(canConstruct.ProfiledBytes, Is.Zero);
        }

        [Test]
        public void TerrainDependentBuildingOnlyUsesGroundFortressRule()
        {
            Assert.That(
                BuildingMobilityRules.CanOperate(
                    BuildingCatalog.MiningStation,
                    BuildingSite.Ground,
                    CityMode.Fortress),
                Is.True);
            Assert.That(
                BuildingMobilityRules.CanOperate(
                    BuildingCatalog.MiningStation,
                    BuildingSite.Ground,
                    CityMode.Mobile),
                Is.False);
            Assert.That(
                BuildingMobilityRules.CanOperate(
                    BuildingCatalog.MiningStation,
                    BuildingSite.InnerCity,
                    CityMode.Fortress),
                Is.False);
        }

        [Test]
        public void FriendlyNamesMatchApprovedChineseTerms()
        {
            Assert.That(BuildingMobilityRules.PlacementName(BuildingPlacement.Ground), Is.EqualTo("地面"));
            Assert.That(BuildingMobilityRules.PlacementName(BuildingPlacement.InnerCity), Is.EqualTo("内城"));
            Assert.That(BuildingMobilityRules.PlacementName(BuildingPlacement.Either), Is.EqualTo("两者皆可"));
            Assert.That(BuildingMobilityRules.OperationName(BuildingOperation.MobileAllowed), Is.EqualTo("移动可运行"));
            Assert.That(BuildingMobilityRules.OperationName(BuildingOperation.FortressOnly), Is.EqualTo("仅展开运行"));
            Assert.That(BuildingMobilityRules.OperationName(BuildingOperation.TerrainDependent), Is.EqualTo("地形依赖"));
        }

        private static TestCaseData Case(
            BuildingDefinition definition,
            BuildingPlacement placement,
            BuildingOperation operation)
        {
            return new TestCaseData(definition, placement, operation)
                .SetName($"{definition.Name}_uses_{placement}_{operation}");
        }

        private static AllocationMeasurement Profile300Calls(Action action)
        {
            ProfilerRecorder recorder =
                ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC.Alloc",
                    2048,
                    ProfilerRecorderOptions.StartImmediately |
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                action();
            long currentThreadBytes =
                GC.GetAllocatedBytesForCurrentThread() - before;
            recorder.Stop();
            int samples = recorder.Count;
            long profiledBytes = 0;
            for (var index = 0; index < recorder.Count; index++)
            {
                ProfilerRecorderSample sample =
                    recorder.GetSample(index);
                profiledBytes += sample.Value * sample.Count;
            }
            recorder.Dispose();
            return new AllocationMeasurement(
                samples,
                profiledBytes,
                currentThreadBytes);
        }

        private readonly struct AllocationMeasurement
        {
            public AllocationMeasurement(
                int samples,
                long profiledBytes,
                long currentThreadBytes)
            {
                Samples = samples;
                ProfiledBytes = profiledBytes;
                CurrentThreadBytes = currentThreadBytes;
            }

            public int Samples { get; }
            public long ProfiledBytes { get; }
            public long CurrentThreadBytes { get; }
        }
    }
}
