using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseTowerCombatLossTests
    {
        private const string DestroyedTowerId =
            "building.instance.turret.combat-loss-a";
        private const string OtherTowerId =
            "building.instance.turret.combat-loss-b";

        [Test]
        public void MachineGunTowerCombatLossRemovesOnlyDestroyedRuntimeState()
        {
            GrayboxBuildingInstance3D destroyed = CompletedTurret(
                DestroyedTowerId,
                x: 0,
                y: 0);
            GrayboxBuildingInstance3D other = CompletedTurret(
                OtherTowerId,
                x: 1,
                y: 0);
            var runtime = new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 9f,
                spawnZ: 0f);
            runtime.Synchronize(
                new[] { other, destroyed },
                CityMode.Fortress,
                cityX: 0,
                cityY: 0,
                BuildingRangeRules.InitialGroundRadius);

            var inventory = new ResourceInventory(500);
            Assert.That(inventory.Add(ResourceIds.Ammunition, 45),
                Is.EqualTo(45));
            using (var storage = new CityResourceStorageModel(inventory, 150))
                runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);

            Assert.That(runtime.TryGetTowerState(
                DestroyedTowerId,
                out GrayboxDefenseTowerRuntimeState3D destroyedState),
                Is.True);
            Assert.That(runtime.TryGetTowerState(
                OtherTowerId,
                out GrayboxDefenseTowerRuntimeState3D otherState),
                Is.True);
            Assert.That(destroyedState.Combat.Ammo, Is.EqualTo(30));
            Assert.That(otherState.Combat.Ammo, Is.EqualTo(15));

            int otherAmmoBefore = otherState.Combat.Ammo;
            bool otherCanRunBefore = otherState.CanRunLocally;
            string otherTargetBefore = otherState.TargetId;
            GrayboxDefenseTowerStatus3D otherStatusBefore = otherState.Status;
            GrayboxDefenseRuntimeSnapshot3D cachedSnapshotBefore =
                runtime.Snapshot;
            GrayboxDefenseTowerSnapshot3D otherSnapshotBefore =
                cachedSnapshotBefore.Towers.Single(
                    value => value.StableId == OtherTowerId);
            Assert.That(RunnableIds(runtime), Does.Contain(DestroyedTowerId));
            Assert.That(RunnableIds(runtime), Does.Contain(OtherTowerId));
            Assert.That(SynchronizedLocks(runtime).ContainsKey(DestroyedTowerId),
                Is.True);
            Assert.That(SynchronizedLocks(runtime).ContainsKey(OtherTowerId),
                Is.True);
            bool otherSynchronizedLockBefore =
                SynchronizedLocks(runtime)[OtherTowerId];

            bool removed = TryDestroyTowerForCombat(
                runtime,
                DestroyedTowerId,
                out ResourceAmount[] lostResources);

            Assert.That(removed, Is.True);
            Assert.That(lostResources, Has.Length.EqualTo(1));
            Assert.That(lostResources[0].ResourceId,
                Is.EqualTo(ResourceIds.Ammunition));
            Assert.That(lostResources[0].Amount, Is.EqualTo(30),
                "Combat loss must report the tower's exact local cache.");
            Assert.That(runtime.TryGetTowerState(DestroyedTowerId, out _),
                Is.False);
            Assert.That(runtime.Towers.Select(value => value.StableId),
                Does.Not.Contain(DestroyedTowerId));
            Assert.That(RunnableIds(runtime), Does.Not.Contain(DestroyedTowerId));
            Assert.That(SynchronizedLocks(runtime).ContainsKey(DestroyedTowerId),
                Is.False,
                "The first destruction must immediately clear the tower's " +
                "synchronized evacuation-lock cache.");
            Assert.That(SynchronizedLocks(runtime).ContainsKey(OtherTowerId),
                Is.True);
            Assert.That(SynchronizedLocks(runtime)[OtherTowerId],
                Is.EqualTo(otherSynchronizedLockBefore));

            GrayboxDefenseRuntimeSnapshot3D snapshotAfter = runtime.Snapshot;
            Assert.That(snapshotAfter, Is.Not.SameAs(cachedSnapshotBefore),
                "Removing a tower must invalidate the cached selection snapshot.");
            Assert.That(snapshotAfter.Towers.Select(value => value.StableId),
                Does.Not.Contain(DestroyedTowerId));
            Assert.That(snapshotAfter.Towers.Select(value => value.StableId),
                Is.EqualTo(new[] { OtherTowerId }));

            Assert.That(runtime.TryGetTowerState(OtherTowerId, out var otherAfter),
                Is.True);
            Assert.That(otherAfter, Is.SameAs(otherState));
            Assert.That(otherAfter.Combat.Ammo, Is.EqualTo(otherAmmoBefore));
            Assert.That(otherAfter.CanRunLocally, Is.EqualTo(otherCanRunBefore));
            Assert.That(otherAfter.TargetId, Is.EqualTo(otherTargetBefore));
            Assert.That(otherAfter.Status, Is.EqualTo(otherStatusBefore));
            Assert.That(RunnableIds(runtime), Does.Contain(OtherTowerId));

            GrayboxDefenseTowerSnapshot3D otherSnapshotAfter =
                snapshotAfter.Towers.Single();
            Assert.That(otherSnapshotAfter.Ammo,
                Is.EqualTo(otherSnapshotBefore.Ammo));
            Assert.That(otherSnapshotAfter.Connected,
                Is.EqualTo(otherSnapshotBefore.Connected));
            Assert.That(otherSnapshotAfter.CanRunLocally,
                Is.EqualTo(otherSnapshotBefore.CanRunLocally));
            Assert.That(otherSnapshotAfter.PlayerPaused,
                Is.EqualTo(otherSnapshotBefore.PlayerPaused));
            Assert.That(otherSnapshotAfter.TargetId,
                Is.EqualTo(otherSnapshotBefore.TargetId));
            Assert.That(otherSnapshotAfter.Status,
                Is.EqualTo(otherSnapshotBefore.Status));

            Assert.That(TryDestroyTowerForCombat(
                runtime,
                DestroyedTowerId,
                out ResourceAmount[] repeatedLoss), Is.False);
            Assert.That(repeatedLoss, Is.Not.Null);
            Assert.That(repeatedLoss, Is.Empty);
            Assert.That(runtime.TryGetTowerState(OtherTowerId, out var finalOther),
                Is.True);
            Assert.That(finalOther, Is.SameAs(otherState));
            Assert.That(finalOther.Combat.Ammo, Is.EqualTo(otherAmmoBefore));
            Assert.That(SynchronizedLocks(runtime), Has.Count.EqualTo(1));
            Assert.That(SynchronizedLocks(runtime).ContainsKey(DestroyedTowerId),
                Is.False);
            Assert.That(SynchronizedLocks(runtime)[OtherTowerId],
                Is.EqualTo(otherSynchronizedLockBefore),
                "An idempotent repeat must not mutate another tower's lock.");
        }

        private static bool TryDestroyTowerForCombat(
            GrayboxDefenseRuntime3D runtime,
            string stableInstanceId,
            out ResourceAmount[] lostResources)
        {
            MethodInfo method = typeof(GrayboxDefenseRuntime3D).GetMethod(
                "TryDestroyTowerForCombat",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(ResourceAmount[]).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Combat destruction requires the public atomic API " +
                "TryDestroyTowerForCombat(string, out ResourceAmount[]).");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));

            object[] arguments = { stableInstanceId, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            lostResources = arguments[1] as ResourceAmount[];
            Assert.That(lostResources, Is.Not.Null,
                "The combat-loss result must always be a non-null array.");
            return result;
        }

        private static IReadOnlyCollection<string> RunnableIds(
            GrayboxDefenseRuntime3D runtime)
        {
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "runnableIds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var ids = field.GetValue(runtime) as HashSet<string>;
            Assert.That(ids, Is.Not.Null);
            return ids;
        }

        private static IReadOnlyDictionary<string, bool> SynchronizedLocks(
            GrayboxDefenseRuntime3D runtime)
        {
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "synchronizedLockById",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var locks = field.GetValue(runtime) as Dictionary<string, bool>;
            Assert.That(locks, Is.Not.Null);
            return locks;
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
            string stableInstanceId,
            int x,
            int y)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(complete, Is.Not.Null);

            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableInstanceId,
                    new PlacedBuilding(
                        BuildingCatalog.MachineGunTurret,
                        x,
                        y,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(
                        BuildingCatalog.MachineGunTurret.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            complete.Invoke(instance, null);
            return instance;
        }
    }
}
