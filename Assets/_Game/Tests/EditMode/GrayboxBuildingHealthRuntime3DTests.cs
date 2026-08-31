using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingHealthRuntime3DTests
    {
        private const string RuntimeTypeName =
            "WasteCity.Graybox3D.Building.GrayboxBuildingHealthRuntime3D";
        private const BindingFlags InstanceAny =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void PublicContractIsPureAndUsesExistingBuildingTruth()
        {
            Type runtimeType = RequireRuntimeType();
            Assert.That(
                typeof(UnityEngine.Object).IsAssignableFrom(runtimeType),
                Is.False,
                "Building health must remain a pure C# rule owner.");
            Assert.That(runtimeType.GetConstructor(Type.EmptyTypes), Is.Not.Null);
            RequireMethod(
                runtimeType,
                "Synchronize",
                typeof(void),
                typeof(IReadOnlyList<GrayboxBuildingInstance3D>));
            RequireMethod(
                runtimeType,
                "TryGetHealth",
                typeof(bool),
                typeof(string),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType());
            RequireMethod(
                runtimeType,
                "TryApplyDamage",
                typeof(bool),
                typeof(string),
                typeof(int),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType());
            RequireMethod(
                runtimeType,
                "Capture",
                typeof(FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]));
            RequireMethod(
                runtimeType,
                "TryRestore",
                typeof(bool),
                typeof(FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]),
                typeof(IReadOnlyList<GrayboxBuildingInstance3D>),
                typeof(string).MakeByRefType());
        }

        [Test]
        public void SynchronizeInitializesOnlyEligibleCompletedPlayerBuildings()
        {
            GrayboxBuildingInstance3D eligible = Instance(
                "building.instance.health.001",
                BuildingCatalog.Wall,
                GrayboxBuildingInstanceState.Completed);
            GrayboxBuildingInstance3D constructing = Instance(
                "building.instance.health.002",
                BuildingCatalog.Warehouse,
                GrayboxBuildingInstanceState.UnderConstruction);
            GrayboxBuildingInstance3D abandoned = Instance(
                "building.instance.health.003",
                BuildingCatalog.Smelter,
                GrayboxBuildingInstanceState.AbandonedRuin);
            GrayboxBuildingInstance3D locked = Instance(
                "building.instance.health.004",
                BuildingCatalog.MachineGunTurret,
                GrayboxBuildingInstanceState.Completed,
                evacuationLocked: true);
            GrayboxBuildingInstance3D foreign = Instance(
                "building.instance.health.005",
                BuildingCatalog.ResearchStation,
                GrayboxBuildingInstanceState.Completed,
                playerOwned: false);
            object runtime = CreateRuntime();

            Synchronize(runtime, eligible, constructing, abandoned, locked,
                foreign);

            AssertHealth(
                runtime,
                eligible.StableInstanceId,
                expectedCurrent: BuildingCatalog.Wall.MaximumHealth,
                expectedMaximum: BuildingCatalog.Wall.MaximumHealth,
                expectedDestroyed: false);
            AssertMissing(runtime, constructing.StableInstanceId);
            AssertMissing(runtime, abandoned.StableInstanceId);
            AssertMissing(runtime, locked.StableInstanceId);
            AssertMissing(runtime, foreign.StableInstanceId);
        }

        [Test]
        public void AlloyArmorPreviewDoesNotChangeThisCampaignMaximumHealth()
        {
            BuildingDefinition definition = BuildingCatalog.Warehouse;
            Assert.That(
                RouteTechnologyEffects.BuildingMaximumHealth(
                    definition.MaximumHealth,
                    alloyArmor: true),
                Is.GreaterThan(definition.MaximumHealth),
                "The existing route effect remains a preview elsewhere.");
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.alloy",
                definition,
                GrayboxBuildingInstanceState.Completed);
            object runtime = CreateRuntime();

            Synchronize(runtime, instance);

            AssertHealth(
                runtime,
                instance.StableInstanceId,
                definition.MaximumHealth,
                definition.MaximumHealth,
                expectedDestroyed: false);
        }

        [Test]
        public void AlloyArmorDerivedMaximumRestoresWithoutChangingSchema()
        {
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.alloy-restore",
                BuildingCatalog.Warehouse,
                GrayboxBuildingInstanceState.Completed);
            var source = new GrayboxBuildingHealthRuntime3D();
            source.Synchronize(new[] { instance }, alloyArmorCompleted: true);
            Assert.That(source.TryApplyDamage(
                instance.StableInstanceId, 10, out _, out _), Is.True);
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] saved =
                source.Capture();
            var restored = new GrayboxBuildingHealthRuntime3D();

            Assert.That(restored.TryRestore(
                saved,
                new[] { instance },
                alloyArmorCompleted: true,
                out string error), Is.True, error);
            int expectedMaximum = RouteTechnologyEffects.BuildingMaximumHealth(
                BuildingCatalog.Warehouse.MaximumHealth,
                alloyArmor: true);
            Assert.That(restored.TryGetHealth(
                instance.StableInstanceId,
                out int current,
                out int maximum,
                out bool destroyed), Is.True);
            Assert.That(maximum, Is.EqualTo(expectedMaximum));
            Assert.That(current, Is.EqualTo(expectedMaximum - 10));
            Assert.That(destroyed, Is.False);
        }

        [Test]
        public void DamageClampsAtZeroAndPublishesDestroyedOnlyOnce()
        {
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.damage",
                BuildingCatalog.Wall,
                GrayboxBuildingInstanceState.Completed);
            object runtime = CreateRuntime();
            Synchronize(runtime, instance);

            DamageResult first = ApplyDamage(runtime, instance.StableInstanceId,
                250);
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.AppliedDamage, Is.EqualTo(250));
            Assert.That(first.DestroyedNow, Is.False);
            DamageResult lethal = ApplyDamage(runtime, instance.StableInstanceId,
                100);
            Assert.That(lethal.Accepted, Is.True);
            Assert.That(lethal.AppliedDamage, Is.EqualTo(50));
            Assert.That(lethal.DestroyedNow, Is.True);
            DamageResult repeated = ApplyDamage(runtime,
                instance.StableInstanceId, 100);
            Assert.That(repeated.Accepted, Is.True);
            Assert.That(repeated.AppliedDamage, Is.Zero);
            Assert.That(repeated.DestroyedNow, Is.False,
                "The destroyed fact must be emitted exactly once.");
            AssertHealth(runtime, instance.StableInstanceId, 0,
                BuildingCatalog.Wall.MaximumHealth, expectedDestroyed: true);
        }

        [Test]
        public void SynchronizeDoesNotHealOrResetExistingDamage()
        {
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.sync",
                BuildingCatalog.Warehouse,
                GrayboxBuildingInstanceState.Completed);
            object runtime = CreateRuntime();
            Synchronize(runtime, instance);
            Assert.That(
                ApplyDamage(runtime, instance.StableInstanceId, 75)
                    .AppliedDamage,
                Is.EqualTo(75));

            Synchronize(runtime, instance);

            AssertHealth(runtime, instance.StableInstanceId,
                BuildingCatalog.Warehouse.MaximumHealth - 75,
                BuildingCatalog.Warehouse.MaximumHealth,
                expectedDestroyed: false);
        }

        [Test]
        public void SynchronizeRemovesTrackedStateAfterInstanceLeavesTruth()
        {
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.removed",
                BuildingCatalog.Wall,
                GrayboxBuildingInstanceState.Completed);
            object runtime = CreateRuntime();
            Synchronize(runtime, instance);
            Assert.That(
                ApplyDamage(runtime, instance.StableInstanceId, 75)
                    .AppliedDamage,
                Is.EqualTo(75));

            Synchronize(runtime);

            AssertMissing(runtime, instance.StableInstanceId);
            Assert.That(Capture(runtime).Length, Is.Zero,
                "Health truth must not retain a stable ID that no longer " +
                "exists in the building-instance truth.");
        }

        [Test]
        public void SynchronizePreservesDamageAcrossTemporaryEvacuationLock()
        {
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.evacuation-lock",
                BuildingCatalog.Warehouse,
                GrayboxBuildingInstanceState.Completed);
            object runtime = CreateRuntime();
            Synchronize(runtime, instance);
            const int damage = 75;
            Assert.That(
                ApplyDamage(runtime, instance.StableInstanceId, damage)
                    .AppliedDamage,
                Is.EqualTo(damage));

            InvokeInternal(instance, "SetEvacuationLocked", true);
            Synchronize(runtime, instance);
            AssertHealth(runtime, instance.StableInstanceId,
                BuildingCatalog.Warehouse.MaximumHealth - damage,
                BuildingCatalog.Warehouse.MaximumHealth,
                expectedDestroyed: false);

            InvokeInternal(instance, "SetEvacuationLocked", false);
            Synchronize(runtime, instance);
            AssertHealth(runtime, instance.StableInstanceId,
                BuildingCatalog.Warehouse.MaximumHealth - damage,
                BuildingCatalog.Warehouse.MaximumHealth,
                expectedDestroyed: false);
        }

        [Test]
        public void RestoreAcceptsZeroHealthDestroyedAndKeepsItIdempotent()
        {
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.restore",
                BuildingCatalog.ResearchStation,
                GrayboxBuildingInstanceState.Completed);
            object runtime = CreateRuntime();
            Synchronize(runtime, instance);
            var restored = new[]
            {
                State(instance.StableInstanceId, 0, destroyed: true),
            };

            Assert.That(
                TryRestore(runtime, restored, new[] { instance }, out string error),
                Is.True,
                error);
            AssertHealth(runtime, instance.StableInstanceId, 0,
                BuildingCatalog.ResearchStation.MaximumHealth,
                expectedDestroyed: true);
            DamageResult damage = ApplyDamage(runtime, instance.StableInstanceId,
                1);
            Assert.That(damage.AppliedDamage, Is.Zero);
            Assert.That(damage.DestroyedNow, Is.False);

            Array captured = Capture(runtime);
            Assert.That(captured.Length, Is.EqualTo(1));
            object item = captured.GetValue(0);
            Assert.That(ReadString(item, "stableInstanceId"),
                Is.EqualTo(instance.StableInstanceId));
            Assert.That(ReadInt(item, "currentHealth"), Is.Zero);
            Assert.That(ReadBool(item, "isDestroyed"), Is.True);
        }

        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("contradictory")]
        public void RestoreRejectsInvalidHealthStateWithoutPartialMutation(
            string invalidKind)
        {
            GrayboxBuildingInstance3D instance = Instance(
                "building.instance.health.invalid",
                BuildingCatalog.Wall,
                GrayboxBuildingInstanceState.Completed);
            object runtime = CreateRuntime();
            Synchronize(runtime, instance);
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] states;
            switch (invalidKind)
            {
                case "duplicate":
                    states = new[]
                    {
                        State(instance.StableInstanceId, 200, false),
                        State(instance.StableInstanceId, 150, false),
                    };
                    break;
                case "unknown":
                    states = new[]
                    {
                        State("building.instance.health.missing", 100, false),
                    };
                    break;
                case "contradictory":
                    states = new[]
                    {
                        State(instance.StableInstanceId, 0, false),
                    };
                    break;
                default:
                    Assert.Fail("Unknown invalid health state: " + invalidKind);
                    states = Array.Empty<
                        FormalThreeDDefenseCampaignBuildingHealthStateSaveData>();
                    break;
            }

            Assert.That(
                TryRestore(runtime, states, new[] { instance }, out string error),
                Is.False,
                invalidKind);
            Assert.That(error, Is.Not.Empty);
            AssertHealth(runtime, instance.StableInstanceId,
                BuildingCatalog.Wall.MaximumHealth,
                BuildingCatalog.Wall.MaximumHealth,
                expectedDestroyed: false);
        }

        private static object CreateRuntime()
        {
            return Activator.CreateInstance(RequireRuntimeType());
        }

        private static Type RequireRuntimeType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(RuntimeTypeName, false);
                if (type != null) return type;
            }
            Assert.Fail(
                "IDEA-0017 RED: missing pure building-health owner " +
                RuntimeTypeName + ".");
            return null;
        }

        private static MethodInfo RequireMethod(
            Type owner,
            string name,
            Type returnType,
            params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameters,
                null);
            Assert.That(method, Is.Not.Null,
                owner.FullName + " must expose " + name + ".");
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
            return method;
        }

        private static void Synchronize(
            object runtime,
            params GrayboxBuildingInstance3D[] instances)
        {
            RequireMethod(
                runtime.GetType(),
                "Synchronize",
                typeof(void),
                typeof(IReadOnlyList<GrayboxBuildingInstance3D>))
                .Invoke(runtime, new object[] { instances });
        }

        private static void AssertHealth(
            object runtime,
            string stableInstanceId,
            int expectedCurrent,
            int expectedMaximum,
            bool expectedDestroyed)
        {
            MethodInfo method = RequireMethod(
                runtime.GetType(),
                "TryGetHealth",
                typeof(bool),
                typeof(string),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType());
            object[] arguments = { stableInstanceId, 0, 0, false };
            Assert.That((bool)method.Invoke(runtime, arguments), Is.True,
                stableInstanceId);
            Assert.That((int)arguments[1], Is.EqualTo(expectedCurrent));
            Assert.That((int)arguments[2], Is.EqualTo(expectedMaximum));
            Assert.That((bool)arguments[3], Is.EqualTo(expectedDestroyed));
        }

        private static void AssertMissing(object runtime, string stableInstanceId)
        {
            MethodInfo method = RequireMethod(
                runtime.GetType(),
                "TryGetHealth",
                typeof(bool),
                typeof(string),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType());
            object[] arguments = { stableInstanceId, 0, 0, false };
            Assert.That((bool)method.Invoke(runtime, arguments), Is.False,
                stableInstanceId + " must not enter combat health truth.");
        }

        private static DamageResult ApplyDamage(
            object runtime,
            string stableInstanceId,
            int amount)
        {
            MethodInfo method = RequireMethod(
                runtime.GetType(),
                "TryApplyDamage",
                typeof(bool),
                typeof(string),
                typeof(int),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType());
            object[] arguments = { stableInstanceId, amount, 0, false };
            bool accepted = (bool)method.Invoke(runtime, arguments);
            return new DamageResult(
                accepted,
                (int)arguments[2],
                (bool)arguments[3]);
        }

        private static Array Capture(object runtime)
        {
            return (Array)RequireMethod(
                    runtime.GetType(),
                    "Capture",
                    typeof(
                        FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]))
                .Invoke(runtime, null);
        }

        private static bool TryRestore(
            object runtime,
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] states,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out string error)
        {
            MethodInfo method = RequireMethod(
                runtime.GetType(),
                "TryRestore",
                typeof(bool),
                typeof(FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]),
                typeof(IReadOnlyList<GrayboxBuildingInstance3D>),
                typeof(string).MakeByRefType());
            object[] arguments = { states, instances, null };
            bool restored = (bool)method.Invoke(runtime, arguments);
            error = arguments[2] as string ?? string.Empty;
            return restored;
        }

        private static GrayboxBuildingInstance3D Instance(
            string stableInstanceId,
            BuildingDefinition definition,
            GrayboxBuildingInstanceState state,
            bool evacuationLocked = false,
            bool playerOwned = true)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    InstanceAny,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableInstanceId,
                    new PlacedBuilding(definition, 1, 1),
                    new ConstructionProgress(definition.BuildSeconds),
                    default(ResourceNodeBinding),
                });
            if (state == GrayboxBuildingInstanceState.Completed)
                InvokeInternal(instance, "Complete");
            else if (state == GrayboxBuildingInstanceState.AbandonedRuin)
                InvokeInternal(instance, "Abandon");
            if (!playerOwned)
            {
                InvokeInternal(
                    instance,
                    "RestoreEvacuationState",
                    false,
                    GrayboxBuildingInstanceState.Completed);
            }
            if (evacuationLocked)
                InvokeInternal(instance, "SetEvacuationLocked", true);
            return instance;
        }

        private static void InvokeInternal(
            object owner,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                InstanceAny);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(owner, arguments);
        }

        private static
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData State(
                string stableInstanceId,
                int currentHealth,
                bool destroyed)
        {
            return new FormalThreeDDefenseCampaignBuildingHealthStateSaveData
            {
                stableInstanceId = stableInstanceId,
                currentHealth = currentHealth,
                isDestroyed = destroyed,
            };
        }

        private static string ReadString(object owner, string fieldName)
        {
            return RequireField(owner, fieldName).GetValue(owner) as string;
        }

        private static int ReadInt(object owner, string fieldName)
        {
            return (int)RequireField(owner, fieldName).GetValue(owner);
        }

        private static bool ReadBool(object owner, string fieldName)
        {
            return (bool)RequireField(owner, fieldName).GetValue(owner);
        }

        private static FieldInfo RequireField(object owner, string fieldName)
        {
            Assert.That(owner, Is.Not.Null);
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, fieldName);
            return field;
        }

        private readonly struct DamageResult
        {
            public DamageResult(
                bool accepted,
                int appliedDamage,
                bool destroyedNow)
            {
                Accepted = accepted;
                AppliedDamage = appliedDamage;
                DestroyedNow = destroyedNow;
            }

            public bool Accepted { get; }
            public int AppliedDamage { get; }
            public bool DestroyedNow { get; }
        }
    }
}
