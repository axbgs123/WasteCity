using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Core;

namespace WasteCity.Tests
{
    public sealed class SessionStatisticsTests
    {
        private const string SnapshotTypeName =
            "WasteCity.Core.SessionStatisticsSnapshot";
        private const string MetricTypeName =
            "WasteCity.Core.SessionStatisticsMetric";

        [Test]
        public void CampaignStatisticsPublicContractExists()
        {
            Type model = typeof(SessionStatisticsModel);
            Type snapshot = RequireType(SnapshotTypeName);
            Type metric = RequireType(MetricTypeName);

            RequireMethod(model, "AdvanceRuleTime", typeof(float));
            RequireMethod(model, "RegisterCompletedWaves", typeof(int));
            RequireMethod(model, "RegisterEnemyKill", typeof(string),
                typeof(string));
            RequireMethod(model, "RegisterTowerDamage", typeof(string),
                typeof(int));
            RequireMethod(model, "RegisterConsumableSpent", typeof(string),
                typeof(int));
            RequireMethod(model, "RegisterBuildingLoss", typeof(string),
                typeof(int));
            RequireMethod(model, "ObserveAliveEnemyCount", typeof(int));
            RequireMethod(model, "RegisterCompletedProductionBatches",
                typeof(int));
            RequireMethod(model, "RegisterProductionTime", typeof(float),
                typeof(float));
            RequireMethod(model, "MarkCityPackedAfterCampaignStart");
            RequireMethod(model, "MarkDevelopmentModifierUsed");
            RequireMethod(model, "MarkPartialFromMigration");
            RequireMethod(model, "FreezeAtTerminal");
            Assert.That(model.GetMethod("Capture", Type.EmptyTypes)?.ReturnType,
                Is.EqualTo(snapshot));
            Assert.That(model.GetMethod("TryRestore")?.ReturnType,
                Is.EqualTo(typeof(bool)));

            string[] snapshotProperties =
            {
                "ElapsedRuleSeconds", "CompletedWaveCount", "TotalKillCount",
                "KillsByEnemyId", "DamageByTowerBuildingId",
                "KillsByTowerBuildingId", "ConsumablesSpentByResourceId",
                "BuildingLossesByBuildingId", "TotalBuildingLossCount",
                "HighestAliveEnemyCount", "CompletedProductionBatchCount",
                "ProductionActiveProgressSeconds",
                "ProductionEligibleSeconds", "ProductionEfficiency",
                "CityWasPackedAfterCampaignStart", "DevelopmentModifierUsed",
                "PartialFromMigration", "IsTerminal", "HighestObservation",
                "Rescues", "DelayedRescues", "RetreatedDuringBoss",
            };
            foreach (string property in snapshotProperties)
                Assert.That(snapshot.GetProperty(property), Is.Not.Null,
                    property);
            Assert.That(metric.GetProperty("StableId"), Is.Not.Null);
            Assert.That(metric.GetProperty("Amount"), Is.Not.Null);
        }

        [Test]
        public void LegacyApiRemainsCompatibleAndRejectsInvalidValues()
        {
            var model = new SessionStatisticsModel();
            model.Tick(12.5f, 44f);
            model.Tick(-1f, 20f);
            model.Tick(float.NaN, float.PositiveInfinity);
            model.AddKill();
            model.AddProduction(3);
            model.AddProduction(-2);
            model.AddBuildingLoss();
            model.AddRescue(immediate: false);
            model.MarkRetreat();

            Assert.That(model.ElapsedSeconds, Is.EqualTo(12.5f));
            Assert.That(model.HighestObservation, Is.EqualTo(44f));
            Assert.That(model.Kills, Is.EqualTo(1));
            Assert.That(model.ProductionCycles, Is.EqualTo(3));
            Assert.That(model.BuildingLosses, Is.EqualTo(1));
            Assert.That(model.Rescues, Is.EqualTo(1));
            Assert.That(model.DelayedRescues, Is.EqualTo(1));
            Assert.That(model.RetreatedDuringBoss, Is.True);

            model.Restore(90f, 12, 88f, 31, 2, 3, 1, true);
            Assert.That(model.ElapsedSeconds, Is.EqualTo(90f));
            Assert.That(model.Kills, Is.EqualTo(12));
            Assert.That(model.HighestObservation, Is.EqualTo(88f));
            Assert.That(model.ProductionCycles, Is.EqualTo(31));
            Assert.That(model.BuildingLosses, Is.EqualTo(2));
            Assert.That(model.Rescues, Is.EqualTo(3));
            Assert.That(model.DelayedRescues, Is.EqualTo(1));
            Assert.That(model.RetreatedDuringBoss, Is.True);
        }

        [Test]
        public void CampaignFactsAccumulatePositiveActualDeltasInStableOrder()
        {
            var model = new SessionStatisticsModel();
            Invoke(model, "AdvanceRuleTime", 12.5f);
            Invoke(model, "AdvanceRuleTime", -3f);
            Invoke(model, "RegisterCompletedWaves", 2);
            Invoke(model, "RegisterCompletedWaves", -1);
            Invoke(model, "RegisterEnemyKill", "enemy.zeta", "tower.zeta");
            Invoke(model, "RegisterEnemyKill", "enemy.alpha", "tower.alpha");
            Invoke(model, "RegisterEnemyKill", "enemy.alpha", "tower.zeta");
            Invoke(model, "RegisterEnemyKill", "", "tower.alpha");
            Invoke(model, "RegisterTowerDamage", "tower.zeta", 7);
            Invoke(model, "RegisterTowerDamage", "tower.alpha", 11);
            Invoke(model, "RegisterTowerDamage", "tower.alpha", -100);
            Invoke(model, "RegisterConsumableSpent", "resource.zeta", 2);
            Invoke(model, "RegisterConsumableSpent", "resource.alpha", 4);
            Invoke(model, "RegisterBuildingLoss", "building.zeta", 2);
            Invoke(model, "RegisterBuildingLoss", "building.alpha", 1);
            Invoke(model, "ObserveAliveEnemyCount", 6);
            Invoke(model, "ObserveAliveEnemyCount", 2);

            object snapshot = Capture(model);
            Assert.That(Read<float>(snapshot, "ElapsedRuleSeconds"),
                Is.EqualTo(12.5f));
            Assert.That(Read<int>(snapshot, "CompletedWaveCount"), Is.EqualTo(2));
            Assert.That(Read<int>(snapshot, "TotalKillCount"), Is.EqualTo(3));
            Assert.That(Read<int>(snapshot, "TotalBuildingLossCount"),
                Is.EqualTo(3));
            Assert.That(Read<int>(snapshot, "HighestAliveEnemyCount"),
                Is.EqualTo(6));
            CollectionAssert.AreEqual(new[] { "enemy.alpha:2", "enemy.zeta:1" },
                MetricStrings(snapshot, "KillsByEnemyId"));
            CollectionAssert.AreEqual(new[] { "tower.alpha:11", "tower.zeta:7" },
                MetricStrings(snapshot, "DamageByTowerBuildingId"));
            CollectionAssert.AreEqual(new[] { "tower.alpha:1", "tower.zeta:2" },
                MetricStrings(snapshot, "KillsByTowerBuildingId"));
            CollectionAssert.AreEqual(
                new[] { "resource.alpha:4", "resource.zeta:2" },
                MetricStrings(snapshot, "ConsumablesSpentByResourceId"));
            CollectionAssert.AreEqual(
                new[] { "building.alpha:1", "building.zeta:2" },
                MetricStrings(snapshot, "BuildingLossesByBuildingId"));
        }

        [Test]
        public void ProductionEfficiencyRequiresEligibleTimeAndRejectsBadDeltas()
        {
            var model = new SessionStatisticsModel();
            Assert.That(Read<object>(Capture(model), "ProductionEfficiency"),
                Is.Null);
            Invoke(model, "RegisterProductionTime", -1f, 2f);
            Invoke(model, "RegisterProductionTime", 3f, 2f);
            Invoke(model, "RegisterProductionTime", float.NaN, 2f);
            Invoke(model, "RegisterCompletedProductionBatches", -3);
            Assert.That(Read<float>(Capture(model),
                "ProductionEligibleSeconds"), Is.Zero);

            Invoke(model, "RegisterCompletedProductionBatches", 3);
            Invoke(model, "RegisterProductionTime", 1.5f, 2f);
            object snapshot = Capture(model);
            Assert.That(Read<int>(snapshot, "CompletedProductionBatchCount"),
                Is.EqualTo(3));
            Assert.That(Read<float>(snapshot,
                "ProductionActiveProgressSeconds"), Is.EqualTo(1.5f));
            Assert.That(Read<float>(snapshot, "ProductionEligibleSeconds"),
                Is.EqualTo(2f));
            Assert.That(Convert.ToSingle(Read<object>(snapshot,
                    "ProductionEfficiency")), Is.EqualTo(.75f).Within(.0001f));
        }

        [Test]
        public void FlagsAreMonotonicAndTerminalFreezesEveryIncrement()
        {
            var model = new SessionStatisticsModel();
            Invoke(model, "MarkCityPackedAfterCampaignStart");
            Invoke(model, "MarkDevelopmentModifierUsed");
            Invoke(model, "MarkPartialFromMigration");
            object beforeTerminal = Capture(model);
            Assert.That(Read<bool>(beforeTerminal,
                "CityWasPackedAfterCampaignStart"), Is.True);
            Assert.That(Read<bool>(beforeTerminal, "DevelopmentModifierUsed"),
                Is.True);
            Assert.That(Read<bool>(beforeTerminal, "PartialFromMigration"),
                Is.True);

            Invoke(model, "FreezeAtTerminal");
            string frozen = Fingerprint(Capture(model));
            model.Tick(2f, 5f);
            model.AddKill();
            model.AddProduction(1);
            model.AddBuildingLoss();
            model.AddRescue(false);
            model.MarkRetreat();
            Invoke(model, "AdvanceRuleTime", 2f);
            Invoke(model, "RegisterCompletedWaves", 1);
            Invoke(model, "RegisterEnemyKill", "enemy.alpha", "tower.alpha");
            Invoke(model, "RegisterTowerDamage", "tower.alpha", 9);
            Invoke(model, "RegisterConsumableSpent", "resource.alpha", 2);
            Invoke(model, "RegisterBuildingLoss", "building.alpha", 1);
            Invoke(model, "ObserveAliveEnemyCount", 8);
            Invoke(model, "RegisterCompletedProductionBatches", 2);
            Invoke(model, "RegisterProductionTime", 1f, 1f);
            Assert.That(Fingerprint(Capture(model)), Is.EqualTo(frozen));
        }

        [Test]
        public void CaptureIsDetachedAndRestoreIsAtomicAndPreservesTerminal()
        {
            var source = new SessionStatisticsModel();
            source.Tick(1f, 9f);
            source.AddRescue(false);
            source.MarkRetreat();
            Invoke(source, "RegisterEnemyKill", "enemy.alpha", "tower.alpha");
            Invoke(source, "RegisterTowerDamage", "tower.alpha", 8);
            Invoke(source, "RegisterProductionTime", 3f, 4f);
            Invoke(source, "FreezeAtTerminal");
            object saved = Capture(source);
            string savedFingerprint = Fingerprint(saved);

            var restored = new SessionStatisticsModel();
            MethodInfo restore = RequireMethod(typeof(SessionStatisticsModel),
                "TryRestore", RequireType(SnapshotTypeName),
                typeof(string).MakeByRefType());
            object[] arguments = { saved, null };
            Assert.That(restore.Invoke(restored, arguments), Is.True,
                arguments[1] as string);
            Assert.That(Fingerprint(Capture(restored)),
                Is.EqualTo(savedFingerprint));
            Invoke(restored, "AdvanceRuleTime", 1f);
            Assert.That(Fingerprint(Capture(restored)),
                Is.EqualTo(savedFingerprint));

            string beforeInvalid = Fingerprint(Capture(restored));
            object[] invalidArguments = { null, null };
            Assert.That(restore.Invoke(restored, invalidArguments), Is.False);
            Assert.That(invalidArguments[1] as string, Is.Not.Empty);
            Assert.That(Fingerprint(Capture(restored)), Is.EqualTo(beforeInvalid));
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(SessionStatisticsModel).Assembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, parameters);
            Assert.That(method, Is.Not.Null, type.FullName + "." + name);
            return method;
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            Type[] parameterTypes = args.Select(item => item.GetType()).ToArray();
            return RequireMethod(target.GetType(), name, parameterTypes)
                .Invoke(target, args);
        }

        private static object Capture(SessionStatisticsModel model)
        {
            return RequireMethod(typeof(SessionStatisticsModel), "Capture")
                .Invoke(model, null);
        }

        private static T Read<T>(object source, string property)
        {
            PropertyInfo info = source.GetType().GetProperty(property);
            Assert.That(info, Is.Not.Null, property);
            return (T)info.GetValue(source);
        }

        private static string[] MetricStrings(object snapshot, string property)
        {
            var result = new List<string>();
            foreach (object item in (IEnumerable)Read<object>(snapshot, property))
            {
                result.Add(Read<string>(item, "StableId") + ":" +
                    Read<int>(item, "Amount"));
            }
            return result.ToArray();
        }

        private static string Fingerprint(object snapshot)
        {
            string[] scalarNames =
            {
                "ElapsedRuleSeconds", "CompletedWaveCount", "TotalKillCount",
                "TotalBuildingLossCount", "HighestAliveEnemyCount",
                "CompletedProductionBatchCount",
                "ProductionActiveProgressSeconds", "ProductionEligibleSeconds",
                "CityWasPackedAfterCampaignStart", "DevelopmentModifierUsed",
                "PartialFromMigration", "IsTerminal", "HighestObservation",
                "Rescues", "DelayedRescues", "RetreatedDuringBoss",
            };
            string scalars = string.Join("|", scalarNames.Select(name =>
                Convert.ToString(Read<object>(snapshot, name))));
            string[] metricNames =
            {
                "KillsByEnemyId", "DamageByTowerBuildingId",
                "KillsByTowerBuildingId", "ConsumablesSpentByResourceId",
                "BuildingLossesByBuildingId",
            };
            return scalars + "|" + string.Join("|", metricNames.Select(name =>
                string.Join(",", MetricStrings(snapshot, name))));
        }
    }
}
