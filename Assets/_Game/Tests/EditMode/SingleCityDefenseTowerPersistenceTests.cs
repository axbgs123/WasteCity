using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseTowerPersistenceTests
    {
        private const string StateTypeName =
            "WasteCity.Defense.SingleCityDefenseTowerPersistenceState";

        private static readonly string[] FormalTowerIds =
        {
            BuildingCatalog.MachineGunTurret.Id.Value,
            BuildingCatalog.LaserTower.Id.Value,
            BuildingCatalog.SporeTower.Id.Value,
        };

        private static readonly string[] StatePropertyNames =
        {
            "StableInstanceId",
            "BuildingId",
            "X",
            "Z",
            "LocalConsumableAmount",
            "ActiveConsumableSeconds",
            "DamageRemainder",
            "TargetStableEnemyId",
            "IsLogisticsConnected",
            "IsPlayerPaused",
        };

        [Test]
        public void PublicContractIsTypedImmutableAndOwnedByTowerModel()
        {
            Type stateType = RequireStateType();
            Assert.That(stateType.IsClass, Is.True);
            Assert.That(stateType.IsPublic, Is.True);
            RequireStateConstructor(stateType);

            for (var index = 0; index < StatePropertyNames.Length; index++)
            {
                PropertyInfo property = stateType.GetProperty(
                    StatePropertyNames[index],
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(property, Is.Not.Null, StatePropertyNames[index]);
                Assert.That(property.CanRead, Is.True);
                Assert.That(property.SetMethod, Is.Null,
                    StatePropertyNames[index] + " must be immutable.");
            }

            MethodInfo capture = typeof(SingleCityDefenseTowerCombatModel)
                .GetMethod(
                    "CaptureForPersistence",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
            Assert.That(capture, Is.Not.Null,
                "The pure tower model must own capture of its private lease, " +
                "damage remainder and target lock.");
            Assert.That(capture.ReturnType, Is.EqualTo(stateType));
            Assert.That(RequireRestoreMethod(stateType), Is.Not.Null);
        }

        [Test]
        public void CapturePreservesAllFormalTowerIdentityAndPrivateState()
        {
            Type stateType = RequireStateType();
            for (var index = 0; index < FormalTowerIds.Length; index++)
            {
                string buildingId = FormalTowerIds[index];
                string stableId = "building.instance.persistence." + index;
                SingleCityDefenseTowerCombatModel tower = EngagedTower(
                    stableId,
                    buildingId,
                    out _);
                tower.SetLogisticsConnected(false);
                tower.SetPlayerPaused(true);

                object state = Capture(tower, stateType);
                AssertStateMatchesTower(state, tower);
                Assert.That(Read<string>(state, "StableInstanceId"),
                    Is.EqualTo(stableId));
                Assert.That(Read<string>(state, "BuildingId"),
                    Is.EqualTo(buildingId));
                Assert.That(Read<int>(state, "LocalConsumableAmount"),
                    Is.EqualTo(tower.LocalCapacity - 1));
                Assert.That(Read<float>(state, "ActiveConsumableSeconds"),
                    Is.GreaterThan(0f));
                Assert.That(Read<string>(state, "TargetStableEnemyId"),
                    Is.Not.Null.And.Not.Empty);
                Assert.That(Read<bool>(state, "IsLogisticsConnected"),
                    Is.False);
                Assert.That(Read<bool>(state, "IsPlayerPaused"), Is.True);
            }
        }

        [Test]
        public void CaptureIsDetachedAndRestoreRehydratesEveryOwnedField()
        {
            Type stateType = RequireStateType();
            SingleCityDefenseTowerCombatModel original = EngagedTower(
                "building.instance.persistence.deep-copy",
                BuildingCatalog.LaserTower.Id.Value,
                out _);
            original.SetLogisticsConnected(false);
            original.SetPlayerPaused(true);

            object first = Capture(original, stateType);
            object second = Capture(original, stateType);
            Assert.That(first, Is.Not.SameAs(second),
                "Each capture must be a detached value object.");
            AssertEquivalentState(first, second);

            original.SetLogisticsConnected(true);
            original.SetPlayerPaused(false);
            Assert.That(Read<bool>(first, "IsLogisticsConnected"), Is.False);
            Assert.That(Read<bool>(first, "IsPlayerPaused"), Is.True,
                "Mutating the live model after capture must not mutate the " +
                "captured persistence value.");

            Assert.That(TryRestore(
                first,
                original.StableInstanceId,
                out SingleCityDefenseTowerCombatModel restored,
                out string error), Is.True, error);
            Assert.That(restored, Is.Not.SameAs(original));
            AssertTowerMatchesState(restored, first);
        }

        [Test]
        public void RestoreRejectsUnknownTowerMismatchedIdentityAndBadRanges()
        {
            Type stateType = RequireStateType();
            const string stableId = "building.instance.persistence.validation";
            string buildingId = BuildingCatalog.LaserTower.Id.Value;
            DefenseTowerDefinition definition =
                DefenseTowerCatalog.For(buildingId);
            object valid = NewState(
                stateType,
                stableId,
                buildingId,
                x: 3f,
                z: -4f,
                localAmount: definition.LocalCapacity,
                activeSeconds: definition.SecondsPerConsumable,
                damageRemainder: .75f,
                targetId: "campaign.enemy.wave-01.0000",
                connected: false,
                paused: true);

            AssertRejected(valid, "building.instance.wrong-owner");
            AssertRejected(NewState(
                stateType,
                stableId,
                "unknown.tower",
                3f,
                -4f,
                0,
                0f,
                0f,
                null,
                true,
                false), stableId);
            AssertRejected(NewState(
                stateType,
                stableId,
                buildingId,
                float.NaN,
                -4f,
                0,
                0f,
                0f,
                null,
                true,
                false), stableId);
            AssertRejected(NewState(
                stateType,
                stableId,
                buildingId,
                3f,
                -4f,
                definition.LocalCapacity + 1,
                0f,
                0f,
                null,
                true,
                false), stableId);
            AssertRejected(NewState(
                stateType,
                stableId,
                buildingId,
                3f,
                -4f,
                0,
                definition.SecondsPerConsumable + .01f,
                0f,
                null,
                true,
                false), stableId);
            AssertRejected(NewState(
                stateType,
                stableId,
                buildingId,
                3f,
                -4f,
                0,
                0f,
                1f,
                null,
                true,
                false), stableId);
            AssertRejected(NewState(
                stateType,
                stableId,
                buildingId,
                3f,
                -4f,
                0,
                0f,
                0f,
                "   ",
                true,
                false), stableId);
        }

        [Test]
        public void RestoredContinuationMatchesUninterruptedDifferentFrames()
        {
            Type stateType = RequireStateType();
            for (var towerIndex = 0;
                 towerIndex < FormalTowerIds.Length;
                 towerIndex++)
            {
                string buildingId = FormalTowerIds[towerIndex];
                string stableId =
                    "building.instance.persistence.partition." + towerIndex;
                SingleCityDefenseTowerCombatModel uninterrupted =
                    EngagedTower(stableId, buildingId, out
                        SingleCityDefenseCampaignModel baselineCampaign);
                SingleCityDefenseTowerCombatModel checkpoint =
                    EngagedTower(stableId, buildingId, out
                        SingleCityDefenseCampaignModel restoredCampaign);
                object state = Capture(checkpoint, stateType);
                Assert.That(TryRestore(
                    state,
                    stableId,
                    out SingleCityDefenseTowerCombatModel restored,
                    out string error), Is.True, error);

                int baselineDamage = uninterrupted.Tick(
                    .7f,
                    baselineCampaign,
                    globallyPaused: false);
                int restoredDamage = 0;
                float[] frames = { .2f, .1f, .4f };
                for (var index = 0; index < frames.Length; index++)
                {
                    restoredDamage += restored.Tick(
                        frames[index],
                        restoredCampaign,
                        globallyPaused: false);
                }

                Assert.That(restoredDamage, Is.EqualTo(baselineDamage),
                    buildingId);
                Assert.That(
                    restoredCampaign.Snapshot.Enemies
                        .OrderBy(enemy => enemy.SpawnOrder)
                        .Select(enemy => enemy.CurrentHealth),
                    Is.EqualTo(
                        baselineCampaign.Snapshot.Enemies
                            .OrderBy(enemy => enemy.SpawnOrder)
                            .Select(enemy => enemy.CurrentHealth)),
                    buildingId);
                Assert.That(restored.LocalConsumableAmount,
                    Is.EqualTo(uninterrupted.LocalConsumableAmount));
                Assert.That(restored.ActiveConsumableSeconds,
                    Is.EqualTo(uninterrupted.ActiveConsumableSeconds)
                        .Within(.0001f));
                Assert.That(restored.DamageRemainder,
                    Is.EqualTo(uninterrupted.DamageRemainder).Within(.0001f));
                Assert.That(restored.TargetStableEnemyId,
                    Is.EqualTo(uninterrupted.TargetStableEnemyId));
                Assert.That(
                    restoredCampaign.Snapshot.Statistics
                        .DamageByTowerBuildingId,
                    Is.EquivalentTo(
                        baselineCampaign.Snapshot.Statistics
                            .DamageByTowerBuildingId));
                Assert.That(
                    restoredCampaign.Snapshot.Statistics
                        .ConsumablesSpentByResourceId,
                    Is.EquivalentTo(
                        baselineCampaign.Snapshot.Statistics
                            .ConsumablesSpentByResourceId));
            }
        }

        private static SingleCityDefenseTowerCombatModel EngagedTower(
            string stableId,
            string buildingId,
            out SingleCityDefenseCampaignModel campaign)
        {
            campaign = CampaignWithFirstSpawnedEnemy();
            SingleCityDefenseEnemySnapshot target =
                campaign.Snapshot.Enemies.Single();
            var tower = new SingleCityDefenseTowerCombatModel(
                stableId,
                buildingId,
                target.X,
                target.Z);
            DefenseTowerDefinition definition =
                DefenseTowerCatalog.For(buildingId);
            using (CityResourceStorageModel storage = StorageWith(
                       definition.ConsumableId,
                       definition.LocalCapacity))
            {
                Assert.That(tower.RefillFrom(storage, connected: true),
                    Is.EqualTo(definition.LocalCapacity));
            }
            Assert.That(tower.AcquireTarget(campaign.Snapshot.Enemies),
                Is.EqualTo(target.StableId));
            Assert.That(tower.Tick(.1f, campaign, globallyPaused: false),
                Is.GreaterThan(0));
            return tower;
        }

        private static SingleCityDefenseCampaignModel
            CampaignWithFirstSpawnedEnemy()
        {
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);
            Assert.That(campaign.NotifyDefenseTowerCompleted(
                "building.instance.persistence.trigger",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true), Is.True);
            campaign.Advance(20f, requestedSpeed: 1);
            Assert.That(campaign.Snapshot.Enemies, Has.Count.EqualTo(1));
            return campaign;
        }

        private static CityResourceStorageModel StorageWith(
            string resourceId,
            int amount)
        {
            var inventory = new ResourceInventory(1000);
            Assert.That(inventory.Add(resourceId, amount), Is.EqualTo(amount));
            return new CityResourceStorageModel(
                inventory,
                coreCapacityPerResource: 150);
        }

        private static Type RequireStateType()
        {
            Type type = typeof(SingleCityDefenseTowerCombatModel).Assembly
                .GetType(StateTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "RED: formal three-tower persistence state is missing.");
            return type;
        }

        private static ConstructorInfo RequireStateConstructor(Type stateType)
        {
            ConstructorInfo constructor = stateType.GetConstructor(new[]
            {
                typeof(string),
                typeof(string),
                typeof(float),
                typeof(float),
                typeof(int),
                typeof(float),
                typeof(float),
                typeof(string),
                typeof(bool),
                typeof(bool),
            });
            Assert.That(constructor, Is.Not.Null,
                "Persistence state must capture identity, position and every " +
                "private mutable tower field.");
            return constructor;
        }

        private static MethodInfo RequireRestoreMethod(Type stateType)
        {
            MethodInfo method = typeof(SingleCityDefenseTowerCombatModel)
                .GetMethod(
                    "TryCreateForPersistence",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[]
                    {
                        stateType,
                        typeof(string),
                        typeof(SingleCityDefenseTowerCombatModel)
                            .MakeByRefType(),
                        typeof(string).MakeByRefType(),
                    },
                    null);
            Assert.That(method, Is.Not.Null,
                "Restore must validate against the expected stable instance " +
                "identity before publishing a candidate model.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            return method;
        }

        private static object Capture(
            SingleCityDefenseTowerCombatModel tower,
            Type stateType)
        {
            MethodInfo capture = tower.GetType().GetMethod(
                "CaptureForPersistence",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(capture, Is.Not.Null);
            Assert.That(capture.ReturnType, Is.EqualTo(stateType));
            object state = capture.Invoke(tower, null);
            Assert.That(state, Is.Not.Null);
            return state;
        }

        private static bool TryRestore(
            object state,
            string expectedStableId,
            out SingleCityDefenseTowerCombatModel restored,
            out string error)
        {
            MethodInfo method = RequireRestoreMethod(state.GetType());
            object[] arguments =
            {
                state,
                expectedStableId,
                null,
                null,
            };
            bool result = (bool)method.Invoke(null, arguments);
            restored = arguments[2] as SingleCityDefenseTowerCombatModel;
            error = arguments[3] as string;
            return result;
        }

        private static object NewState(
            Type stateType,
            string stableId,
            string buildingId,
            float x,
            float z,
            int localAmount,
            float activeSeconds,
            float damageRemainder,
            string targetId,
            bool connected,
            bool paused)
        {
            return RequireStateConstructor(stateType).Invoke(new object[]
            {
                stableId,
                buildingId,
                x,
                z,
                localAmount,
                activeSeconds,
                damageRemainder,
                targetId,
                connected,
                paused,
            });
        }

        private static void AssertRejected(
            object state,
            string expectedStableId)
        {
            Assert.That(TryRestore(
                state,
                expectedStableId,
                out SingleCityDefenseTowerCombatModel restored,
                out string error), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        private static void AssertStateMatchesTower(
            object state,
            SingleCityDefenseTowerCombatModel tower)
        {
            Assert.That(Read<string>(state, "StableInstanceId"),
                Is.EqualTo(tower.StableInstanceId));
            Assert.That(Read<string>(state, "BuildingId"),
                Is.EqualTo(tower.BuildingId));
            Assert.That(Read<float>(state, "X"), Is.EqualTo(tower.X));
            Assert.That(Read<float>(state, "Z"), Is.EqualTo(tower.Z));
            Assert.That(Read<int>(state, "LocalConsumableAmount"),
                Is.EqualTo(tower.LocalConsumableAmount));
            Assert.That(Read<float>(state, "ActiveConsumableSeconds"),
                Is.EqualTo(tower.ActiveConsumableSeconds));
            Assert.That(Read<float>(state, "DamageRemainder"),
                Is.EqualTo(tower.DamageRemainder));
            Assert.That(Read<string>(state, "TargetStableEnemyId"),
                Is.EqualTo(tower.TargetStableEnemyId));
            Assert.That(Read<bool>(state, "IsLogisticsConnected"),
                Is.EqualTo(tower.IsLogisticsConnected));
            Assert.That(Read<bool>(state, "IsPlayerPaused"),
                Is.EqualTo(tower.IsPlayerPaused));
        }

        private static void AssertTowerMatchesState(
            SingleCityDefenseTowerCombatModel tower,
            object state)
        {
            AssertStateMatchesTower(state, tower);
        }

        private static void AssertEquivalentState(object left, object right)
        {
            for (var index = 0; index < StatePropertyNames.Length; index++)
            {
                string name = StatePropertyNames[index];
                Assert.That(
                    Read<object>(right, name),
                    Is.EqualTo(Read<object>(left, name)),
                    name);
            }
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            object value = property.GetValue(target, null);
            if (value == null) return default;
            return (T)value;
        }
    }
}
