using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseObservabilityTests
    {
        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void TowerSnapshotOwnsFormalRangeAndHudShowsTenCells()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-range");
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, turret, CityMode.Fortress);

            GrayboxDefenseTowerSnapshot3D tower =
                runtime.Snapshot.Towers.Single();
            Assert.That(
                tower.Range,
                Is.EqualTo(DefenseTowerCatalog.For(
                    BuildingCatalog.MachineGunTurret.Id.Value).Range));
            Assert.That(tower.Range, Is.EqualTo(10f));

            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                runtime.Snapshot,
                GrayboxDefenseSelectionKind3D.Tower,
                turret.StableInstanceId);
            Assert.That(hud.SelectionText.text, Does.Contain("射程 10"));
        }

        [Test]
        public void EnemySnapshotOwnsCoreTargetAndDistanceAndHudUpdatesAfterMove()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-enemy");
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, turret, CityMode.Fortress);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            using var storage = EmptyStorage();
            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseEnemySnapshot3D before =
                runtime.Snapshot.Enemies.Single();
            float expectedBefore = Distance(before.X, before.Z, 0f, 0f);
            Assert.That(before.TargetName, Is.EqualTo("城市核心"));
            Assert.That(
                before.DistanceToCore,
                Is.EqualTo(expectedBefore).Within(.001f));

            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                runtime.Snapshot,
                GrayboxDefenseSelectionKind3D.Enemy,
                before.StableId);
            Assert.That(hud.SelectionText.text, Does.Contain("目标 城市核心"));
            Assert.That(
                hud.SelectionText.text,
                Does.Contain("距离 " + FormatDistance(expectedBefore) + "格"));

            runtime.SetCorePosition(x: 30f, z: 0f);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseEnemySnapshot3D after =
                runtime.Snapshot.Enemies.Single(value =>
                    value.StableId == before.StableId);
            float expectedAfter = Distance(after.X, after.Z, 30f, 0f);
            Assert.That(after.TargetName, Is.EqualTo("城市核心"));
            Assert.That(
                after.DistanceToCore,
                Is.EqualTo(expectedAfter).Within(.001f));
            Assert.That(
                after.DistanceToCore,
                Is.Not.EqualTo(before.DistanceToCore).Within(.001f));

            hud.Apply(
                runtime.Snapshot,
                GrayboxDefenseSelectionKind3D.Enemy,
                after.StableId);
            Assert.That(
                hud.SelectionText.text,
                Does.Contain("距离 " + FormatDistance(expectedAfter) + "格"));
        }

        [Test]
        public void DestroyedCoreSnapshotClampsHealthAndHudAnnouncesLoss()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-core-loss");
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, turret, CityMode.Fortress);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            using var storage = EmptyStorage();

            runtime.Tick(120f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseRuntimeSnapshot3D snapshot = runtime.Snapshot;
            Assert.That(snapshot.CoreCurrentHealth, Is.Zero);
            Assert.That(snapshot.IsCoreDestroyed, Is.True);
            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);
            Assert.That(hud.SummaryText.text, Does.Contain("城市核心失守"));
        }

        [Test]
        public void LocallyUnavailableTowerClearsTargetTracerAndRecovers()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-unavailable");
            GrayboxDefenseRuntime3D runtime = Runtime();
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            Synchronize(runtime, turret, CityMode.Fortress);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: false), Is.True);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseRuntimeSnapshot3D firing = runtime.Snapshot;
            Assert.That(firing.Towers.Single().CanRunLocally, Is.True);
            Assert.That(
                firing.Towers.Single().Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(firing.Towers.Single().TargetId, Is.Not.Null);
            GrayboxDefenseWorldView3D worldView = CreateWorldView();
            worldView.Apply(firing, new[] { turret });
            Assert.That(worldView.VisibleTracerCount, Is.EqualTo(1));

            SetEvacuationLocked(turret, locked: true);
            Synchronize(runtime, turret, CityMode.Fortress);
            GrayboxDefenseRuntimeSnapshot3D unavailable = runtime.Snapshot;
            GrayboxDefenseTowerSnapshot3D unavailableTower =
                unavailable.Towers.Single();
            Assert.That(unavailableTower.CanRunLocally, Is.False);
            Assert.That(unavailableTower.TargetId, Is.Null);
            Assert.That(
                unavailableTower.Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Unavailable));
            worldView.Apply(unavailable, new[] { turret });
            Assert.That(worldView.VisibleTracerCount, Is.Zero);

            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                unavailable,
                GrayboxDefenseSelectionKind3D.Tower,
                turret.StableInstanceId);
            Assert.That(hud.SelectionText.text, Does.Contain("建筑未运行"));

            SetEvacuationLocked(turret, locked: false);
            Synchronize(runtime, turret, CityMode.Fortress);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseRuntimeSnapshot3D recovered = runtime.Snapshot;
            Assert.That(recovered.Towers.Single().CanRunLocally, Is.True);
            Assert.That(
                recovered.Towers.Single().Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(recovered.Towers.Single().TargetId, Is.Not.Null);
            worldView.Apply(recovered, new[] { turret });
            Assert.That(worldView.VisibleTracerCount, Is.EqualTo(1));
        }

        private GrayboxDefenseHudView3D CreateHud()
        {
            GameObject root = Track(new GameObject("DefenseObservabilityHud"));
            return root.AddComponent<GrayboxDefenseHudView3D>();
        }

        private GrayboxDefenseWorldView3D CreateWorldView()
        {
            GameObject root = Track(new GameObject(
                "DefenseObservabilityWorld"));
            Transform enemies = NewChild(root.transform, "Enemies");
            Transform towers = NewChild(root.transform, "Towers");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            GrayboxDefenseWorldView3D view =
                root.AddComponent<GrayboxDefenseWorldView3D>();
            view.Configure(
                enemies,
                towers,
                material,
                new PlanarCoordinateMapper3D(32, 24));
            return view;
        }

        private static GrayboxDefenseRuntime3D Runtime()
        {
            return new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 9f,
                spawnZ: 0f);
        }

        private static CityResourceStorageModel EmptyStorage()
        {
            return new CityResourceStorageModel(
                new ResourceInventory(500),
                coreCapacityPerResource: 150);
        }

        private static void Synchronize(
            GrayboxDefenseRuntime3D runtime,
            GrayboxBuildingInstance3D turret,
            CityMode cityMode)
        {
            runtime.Synchronize(
                new[] { turret },
                cityMode,
                cityX: 0,
                cityY: 0,
                BuildingRangeRules.InitialGroundRadius);
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
            string stableId)
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
            MethodInfo complete = typeof(GrayboxBuildingInstance3D)
                .GetMethod(
                    "Complete",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(complete, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableId,
                    new PlacedBuilding(
                        BuildingCatalog.MachineGunTurret,
                        0,
                        0,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(
                        BuildingCatalog.MachineGunTurret.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            complete.Invoke(instance, null);
            return instance;
        }

        private static void SetEvacuationLocked(
            GrayboxBuildingInstance3D instance,
            bool locked)
        {
            MethodInfo method = typeof(GrayboxBuildingInstance3D).GetMethod(
                "SetEvacuationLocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(instance, new object[] { locked });
        }

        private static float Distance(
            float x,
            float z,
            float targetX,
            float targetZ)
        {
            float offsetX = x - targetX;
            float offsetZ = z - targetZ;
            return Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
        }

        private static string FormatDistance(float distance)
        {
            return distance.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }
    }
}
