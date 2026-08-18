using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefensePresentationTests
    {
        private const float PositionTolerance = .01f;
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void EnemyPoolPrebuildsEightAndReusesSharedMaterialVisuals()
        {
            WorldFixture fixture = CreateWorldFixture();
            Transform[] prebuilt = Children(fixture.EnemyRoot);

            Assert.That(fixture.View.PooledEnemyCapacity, Is.EqualTo(8));
            Assert.That(prebuilt, Has.Length.EqualTo(8));
            Assert.That(fixture.View.EnemyVisualCount, Is.Zero);

            GrayboxDefenseRuntimeSnapshot3D eightEnemies = Snapshot(
                enemies: Enumerable.Range(0, 8)
                    .Select(index => Enemy(
                        "enemy.pool." + index,
                        index,
                        x: 8 + index,
                        z: 10,
                        health: 60))
                    .ToArray());
            fixture.View.Apply(
                eightEnemies,
                Array.Empty<GrayboxBuildingInstance3D>());
            Assert.That(fixture.View.EnemyVisualCount, Is.EqualTo(8));
            Assert.That(
                fixture.View.GetComponentsInChildren<Renderer>(true),
                Is.All.Matches<Renderer>(renderer =>
                    ReferenceEquals(
                        renderer.sharedMaterial,
                        fixture.SharedMaterial)));

            fixture.View.Apply(
                Snapshot(),
                Array.Empty<GrayboxBuildingInstance3D>());
            fixture.View.Apply(
                eightEnemies,
                Array.Empty<GrayboxBuildingInstance3D>());

            Assert.That(Children(fixture.EnemyRoot), Is.EqualTo(prebuilt));
            Assert.That(fixture.View.PooledEnemyCapacity, Is.EqualTo(8));
            Assert.That(
                Resources.FindObjectsOfTypeAll<Material>().Count(material =>
                    material.name.StartsWith(
                        fixture.SharedMaterial.name,
                        StringComparison.Ordinal)),
                Is.EqualTo(1),
                "Defense placeholders must use sharedMaterial without clones.");
        }

        [Test]
        public void EnemyVisualMapsCellToWorldAndColliderCanBePicked()
        {
            WorldFixture fixture = CreateWorldFixture();
            const string stableId = "enemy.pick.gnawer.000";
            fixture.View.Apply(
                Snapshot(enemies: new[]
                {
                    Enemy(stableId, 0, x: 10, z: 12, health: 60),
                }),
                Array.Empty<GrayboxBuildingInstance3D>());
            Physics.SyncTransforms();

            Assert.That(fixture.View.EnemyVisualCount, Is.EqualTo(1));
            Assert.That(
                fixture.Mapper.TryCellToWorld(
                    10,
                    12,
                    fixture.EnemyRoot.GetChild(0).position.y,
                    out Vector3 expectedWorld),
                Is.True);
            Transform visibleEnemy = Children(fixture.EnemyRoot)
                .Single(child => child.gameObject.activeSelf);
            Assert.That(
                visibleEnemy.position.x,
                Is.EqualTo(expectedWorld.x).Within(PositionTolerance));
            Assert.That(
                visibleEnemy.position.z,
                Is.EqualTo(expectedWorld.z).Within(PositionTolerance));
            Assert.That(
                visibleEnemy.GetComponentInChildren<Collider>(true),
                Is.Not.Null);

            var ray = new Ray(
                expectedWorld + Vector3.up * 10f,
                Vector3.down);
            Assert.That(
                fixture.View.TryPick(
                    ray,
                    out GrayboxDefenseSelectionKind3D kind,
                    out string pickedId),
                Is.True);
            Assert.That(kind, Is.EqualTo(GrayboxDefenseSelectionKind3D.Enemy));
            Assert.That(pickedId, Is.EqualTo(stableId));
        }

        [Test]
        public void TowerPlaceholderHasBarrelAndColliderCanBePicked()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.presentation-turret",
                x: 6,
                y: 7);
            fixture.View.Apply(
                Snapshot(towers: new[]
                {
                    Tower(turret.StableInstanceId),
                }),
                new[] { turret });
            Physics.SyncTransforms();

            Assert.That(fixture.View.TowerVisualCount, Is.EqualTo(1));
            Assert.That(
                fixture.TowerRoot.GetComponentsInChildren<Renderer>(true),
                Has.Length.GreaterThanOrEqualTo(2),
                "The replaceable placeholder needs a base and visible barrel.");
            Assert.That(
                fixture.TowerRoot.GetComponentInChildren<Collider>(true),
                Is.Not.Null);
            Assert.That(
                fixture.Mapper.TryCellToWorld(
                    6,
                    7,
                    0f,
                    out Vector3 towerWorld),
                Is.True);

            Assert.That(
                fixture.View.TryPick(
                    new Ray(towerWorld + Vector3.up * 10f, Vector3.down),
                    out GrayboxDefenseSelectionKind3D kind,
                    out string stableId),
                Is.True);
            Assert.That(kind, Is.EqualTo(GrayboxDefenseSelectionKind3D.Tower));
            Assert.That(stableId, Is.EqualTo(turret.StableInstanceId));
        }

        [Test]
        public void FiringTowerShowsOneTracerBetweenTowerAndTargetWithoutMaterialClone()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.tracer-turret",
                x: 6,
                y: 7);
            const string enemyId = "enemy.tracer.gnawer.000";
            fixture.View.Apply(
                Snapshot(
                    towers: new[]
                    {
                        Tower(
                            turret.StableInstanceId,
                            targetId: enemyId,
                            status: GrayboxDefenseTowerStatus3D.Firing),
                    },
                    enemies: new[]
                    {
                        Enemy(enemyId, 0, x: 10, z: 7, health: 60),
                    }),
                new[] { turret });

            Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(1));
            Assert.That(
                fixture.View.TryGetTowerObject(
                    turret.StableInstanceId,
                    out GameObject towerObject),
                Is.True);
            Assert.That(
                fixture.View.TryGetEnemyObject(enemyId, out GameObject enemyObject),
                Is.True);
            LineRenderer[] visibleTracers = fixture.View
                .GetComponentsInChildren<LineRenderer>(true)
                .Where(line => line.enabled && line.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(visibleTracers, Has.Length.EqualTo(1));
            LineRenderer tracer = visibleTracers[0];
            Assert.That(tracer.useWorldSpace, Is.True);
            Assert.That(tracer.positionCount, Is.EqualTo(2));
            Assert.That(
                tracer.GetPosition(0),
                Is.EqualTo(towerObject.transform.position)
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                tracer.GetPosition(1),
                Is.EqualTo(enemyObject.transform.position)
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                tracer.sharedMaterial,
                Is.SameAs(fixture.SharedMaterial));
            Assert.That(
                towerObject.GetComponentsInChildren<Renderer>(true),
                Is.All.Matches<Renderer>(renderer =>
                    ReferenceEquals(
                        renderer.sharedMaterial,
                        fixture.SharedMaterial)));
            Assert.That(
                Resources.FindObjectsOfTypeAll<Material>().Count(material =>
                    material.name.StartsWith(
                        fixture.SharedMaterial.name,
                        StringComparison.Ordinal)),
                Is.EqualTo(1));
        }

        [Test]
        public void NonFiringStatesAndGlobalPauseHideTracer()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.hidden-tracer-turret",
                x: 6,
                y: 7);
            const string enemyId = "enemy.hidden-tracer.gnawer.000";
            var enemy = new[]
            {
                Enemy(enemyId, 0, x: 10, z: 7, health: 60),
            };

            GrayboxDefenseTowerStatus3D[] hiddenStatuses =
            {
                GrayboxDefenseTowerStatus3D.NoTarget,
                GrayboxDefenseTowerStatus3D.MissingAmmunition,
                GrayboxDefenseTowerStatus3D.PlayerPaused,
            };
            for (int index = 0; index < hiddenStatuses.Length; index++)
            {
                fixture.View.Apply(
                    Snapshot(
                        towers: new[]
                        {
                            Tower(
                                turret.StableInstanceId,
                                targetId: enemyId,
                                status: hiddenStatuses[index]),
                        },
                        enemies: enemy),
                    new[] { turret });
                Assert.That(
                    fixture.View.VisibleTracerCount,
                    Is.Zero,
                    hiddenStatuses[index].ToString());
            }

            fixture.View.Apply(
                Snapshot(
                    towers: new[]
                    {
                        Tower(
                            turret.StableInstanceId,
                            targetId: enemyId,
                            status: GrayboxDefenseTowerStatus3D.Firing),
                    },
                    enemies: enemy),
                new[] { turret });
            Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(1));

            fixture.View.SetSimulationPaused(true);
            Assert.That(fixture.View.VisibleTracerCount, Is.Zero);
            fixture.View.SetSimulationPaused(false);
            Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovedTowerVisualIsPooledAndReusedForDifferentStableId()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D first = CompletedTurret(
                "building.instance.pool-turret-a",
                x: 6,
                y: 7);
            fixture.View.Apply(
                Snapshot(towers: new[] { Tower(first.StableInstanceId) }),
                new[] { first });
            Assert.That(
                fixture.View.TryGetTowerObject(
                    first.StableInstanceId,
                    out GameObject firstObject),
                Is.True);
            Assert.That(fixture.View.PooledTowerCapacity, Is.EqualTo(1));

            fixture.View.Apply(
                Snapshot(),
                Array.Empty<GrayboxBuildingInstance3D>());
            Assert.That(fixture.View.TowerVisualCount, Is.Zero);
            Assert.That(
                fixture.View.TryGetTowerObject(first.StableInstanceId, out _),
                Is.False);

            GrayboxBuildingInstance3D second = CompletedTurret(
                "building.instance.pool-turret-b",
                x: 9,
                y: 7);
            fixture.View.Apply(
                Snapshot(towers: new[] { Tower(second.StableInstanceId) }),
                new[] { second });
            Assert.That(
                fixture.View.TryGetTowerObject(
                    second.StableInstanceId,
                    out GameObject secondObject),
                Is.True);
            Assert.That(secondObject, Is.SameAs(firstObject));
            Assert.That(fixture.View.PooledTowerCapacity, Is.EqualTo(1));
            Assert.That(fixture.View.TowerVisualCount, Is.EqualTo(1));
        }

        [Test]
        public void EightEnemiesAndMultipleTowersKeepPeakObjectsForThreeHundredRefreshes()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D[] cohortA = Enumerable.Range(0, 4)
                .Select(index => CompletedTurret(
                    "building.instance.pool-long-a-" + index,
                    x: 5 + index,
                    y: 7))
                .ToArray();
            GrayboxBuildingInstance3D[] cohortB = Enumerable.Range(0, 4)
                .Select(index => CompletedTurret(
                    "building.instance.pool-long-b-" + index,
                    x: 5 + index,
                    y: 7))
                .ToArray();
            GrayboxDefenseEnemySnapshot3D[] initialEnemies =
                Enumerable.Range(0, 8)
                    .Select(index => Enemy(
                        "enemy.pool-long-a-" + index,
                        index,
                        x: 10 + index * .1f,
                        z: 7,
                        health: 60))
                    .ToArray();
            GrayboxDefenseTowerSnapshot3D[] initialTowers = cohortA
                .Select((tower, index) => Tower(
                    tower.StableInstanceId,
                    targetId: initialEnemies[index].StableId,
                    status: GrayboxDefenseTowerStatus3D.Firing))
                .ToArray();
            fixture.View.Apply(
                Snapshot(towers: initialTowers, enemies: initialEnemies),
                cohortA);

            int[] objectIds = VisualObjectIds(fixture.View);
            int[] rendererIds = ComponentIds<Renderer>(fixture.View);
            int[] tracerIds = ComponentIds<LineRenderer>(fixture.View);
            int enemyRootChildren = fixture.EnemyRoot.childCount;
            int towerRootChildren = fixture.TowerRoot.childCount;
            int namedMaterialCount = CountNamedMaterials(
                fixture.SharedMaterial.name);
            Assert.That(fixture.View.PooledEnemyCapacity, Is.EqualTo(8));
            Assert.That(fixture.View.PooledTowerCapacity, Is.EqualTo(4));
            Assert.That(enemyRootChildren, Is.EqualTo(8));
            Assert.That(towerRootChildren, Is.EqualTo(4));
            Assert.That(tracerIds, Has.Length.EqualTo(4));

            for (int refresh = 0; refresh < 300; refresh++)
            {
                bool useA = (refresh & 1) == 0;
                GrayboxBuildingInstance3D[] instances = useA
                    ? cohortA
                    : cohortB;
                string enemyCohort = useA ? "a" : "b";
                GrayboxDefenseEnemySnapshot3D[] enemies =
                    Enumerable.Range(0, 8)
                        .Select(index => Enemy(
                            "enemy.pool-long-" + enemyCohort + "-" + index,
                            index,
                            x: 10 + index * .1f,
                            z: 7,
                            health: 60 - refresh % 20))
                        .ToArray();
                GrayboxDefenseTowerSnapshot3D[] towers = instances
                    .Select((tower, index) => Tower(
                        tower.StableInstanceId,
                        targetId: enemies[index].StableId,
                        status: (refresh + index & 1) == 0
                            ? GrayboxDefenseTowerStatus3D.Firing
                            : GrayboxDefenseTowerStatus3D.NoTarget))
                    .ToArray();

                fixture.View.Apply(
                    Snapshot(towers: towers, enemies: enemies),
                    instances);

                Assert.That(fixture.View.PooledEnemyCapacity, Is.EqualTo(8));
                Assert.That(fixture.View.PooledTowerCapacity, Is.EqualTo(4));
                Assert.That(fixture.EnemyRoot.childCount,
                    Is.EqualTo(enemyRootChildren));
                Assert.That(fixture.TowerRoot.childCount,
                    Is.EqualTo(towerRootChildren));
                Assert.That(VisualObjectIds(fixture.View), Is.EqualTo(objectIds));
                Assert.That(ComponentIds<Renderer>(fixture.View),
                    Is.EqualTo(rendererIds));
                Assert.That(ComponentIds<LineRenderer>(fixture.View),
                    Is.EqualTo(tracerIds));
                Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(2));
                Assert.That(
                    fixture.View.GetComponentsInChildren<Renderer>(true),
                    Is.All.Matches<Renderer>(renderer => ReferenceEquals(
                        renderer.sharedMaterial,
                        fixture.SharedMaterial)));
                string staleTowerId = useA
                    ? cohortB[0].StableInstanceId
                    : cohortA[0].StableInstanceId;
                Assert.That(fixture.View.TryGetTowerObject(
                    staleTowerId,
                    out _), Is.False);
                string staleEnemyId = "enemy.pool-long-" +
                    (useA ? "b" : "a") + "-0";
                Assert.That(fixture.View.TryGetEnemyObject(
                    staleEnemyId,
                    out _), Is.False);
            }

            Assert.That(
                CountNamedMaterials(fixture.SharedMaterial.name),
                Is.EqualTo(namedMaterialCount));
        }

        [Test]
        public void HudRefreshesSummaryTowerAndEnemySelectionDetails()
        {
            HudFixture fixture = CreateHudFixture();
            const string towerId = "building.instance.hud-turret";
            const string enemyId = "enemy.hud.gnawer.000";
            GrayboxDefenseRuntimeSnapshot3D snapshot = Snapshot(
                wavePhase: WavePhase.Warning,
                warningSeconds: 12.5f,
                coreHealth: 1984,
                towers: new[]
                {
                    Tower(
                        towerId,
                        ammo: 7,
                        connected: true,
                        targetId: enemyId,
                        status: GrayboxDefenseTowerStatus3D.Firing),
                },
                enemies: new[]
                {
                    Enemy(enemyId, 0, x: 10, z: 12, health: 42,
                        attacking: true),
                });

            fixture.View.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.Tower,
                towerId);
            Assert.That(fixture.View.SummaryText.text, Does.Contain("1984/2000"));
            Assert.That(fixture.View.SummaryText.text, Does.Contain("预警"));
            Assert.That(fixture.View.SummaryText.text, Does.Contain("12.5"));
            Assert.That(fixture.View.SummaryText.text, Does.Contain("敌人 1"));
            Assert.That(fixture.View.IsSelectionVisible, Is.True);
            Assert.That(fixture.View.SelectionText.text, Does.Contain("250/250"));
            Assert.That(fixture.View.SelectionText.text, Does.Contain("7/30"));
            Assert.That(fixture.View.SelectionText.text, Does.Contain("已连接"));
            Assert.That(fixture.View.SelectionText.text, Does.Contain("射击"));
            Assert.That(fixture.View.SelectionText.text, Does.Contain(enemyId));

            fixture.View.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.Enemy,
                enemyId);
            Assert.That(fixture.View.SelectionText.text, Does.Contain("42/60"));
            Assert.That(
                fixture.View.SelectionText.text,
                Does.Contain("攻击城市核心"));

            GrayboxDefenseRuntimeSnapshot3D updated = Snapshot(
                wavePhase: WavePhase.Active,
                coreHealth: 1900,
                enemies: new[]
                {
                    Enemy(enemyId, 0, x: 2, z: 0, health: 18,
                        attacking: false),
                });
            fixture.View.Apply(
                updated,
                GrayboxDefenseSelectionKind3D.Enemy,
                enemyId);
            Assert.That(fixture.View.SummaryText.text, Does.Contain("1900/2000"));
            Assert.That(fixture.View.SelectionText.text, Does.Contain("18/60"));
            Assert.That(
                fixture.View.SelectionText.text,
                Does.Contain("接近城市核心"));

            fixture.View.Apply(
                updated,
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);
            Assert.That(fixture.View.IsSelectionVisible, Is.False);
        }

        [Test]
        public void PassiveHudDoesNotBlockInputOrOverlapExistingBars()
        {
            HudFixture fixture = CreateHudFixture();
            fixture.View.Apply(
                Snapshot(),
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                fixture.Canvas.GetComponent<RectTransform>());

            Graphic[] passiveGraphics = fixture.View
                .GetComponentsInChildren<Graphic>(true)
                .Where(graphic =>
                    graphic.GetComponentInParent<Button>() == null)
                .ToArray();
            Assert.That(passiveGraphics, Is.Not.Empty);
            Assert.That(
                passiveGraphics,
                Is.All.Matches<Graphic>(graphic => !graphic.raycastTarget));

            var resourceBar = Rect.MinMaxRect(250f, 842f, 1350f, 900f);
            var quickbarCenter = Rect.MinMaxRect(400f, 0f, 1200f, 150f);
            Rect summary = CanvasRect(fixture.Canvas, fixture.View.SummaryRect);
            Rect selection = CanvasRect(
                fixture.Canvas,
                fixture.View.SelectionRect);
            Assert.That(summary.Overlaps(resourceBar), Is.False);
            Assert.That(selection.Overlaps(resourceBar), Is.False);
            Assert.That(summary.Overlaps(quickbarCenter), Is.False);
            Assert.That(selection.Overlaps(quickbarCenter), Is.False);
        }

        [Test]
        public void PauseButtonRaisesSelectedTowerRequestThroughButtonEvent()
        {
            HudFixture fixture = CreateHudFixture();
            const string towerId = "building.instance.pause-turret";
            fixture.View.Apply(
                Snapshot(towers: new[] { Tower(towerId) }),
                GrayboxDefenseSelectionKind3D.Tower,
                towerId);
            string requestedId = null;
            fixture.View.TowerPauseRequested += stableId =>
                requestedId = stableId;

            Button pauseButton = fixture.View
                .GetComponentsInChildren<Button>(true)
                .Single();
            Assert.That(pauseButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(pauseButton.interactable, Is.True);
            pauseButton.onClick.Invoke();

            Assert.That(requestedId, Is.EqualTo(towerId));
        }

        private WorldFixture CreateWorldFixture()
        {
            GameObject root = Track(new GameObject("DefensePresentation"));
            Transform enemyRoot = NewChild(root.transform, "Enemies");
            Transform towerRoot = NewChild(root.transform, "Towers");
            Material material = Track(CreateMaterial());
            var mapper = new PlanarCoordinateMapper3D(32, 24);
            GrayboxDefenseWorldView3D view =
                root.AddComponent<GrayboxDefenseWorldView3D>();
            view.Configure(enemyRoot, towerRoot, material, mapper);
            return new WorldFixture(
                view,
                enemyRoot,
                towerRoot,
                material,
                mapper);
        }

        private HudFixture CreateHudFixture()
        {
            GameObject canvasObject = Track(new GameObject(
                "DefenseCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster)));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<RectTransform>().sizeDelta =
                new Vector2(1600f, 900f);

            GameObject eventObject = Track(new GameObject("DefenseEventSystem"));
            EventSystem eventSystem = eventObject.AddComponent<EventSystem>();
            GameObject viewObject = Track(new GameObject("DefenseHud"));
            GrayboxDefenseHudView3D view =
                viewObject.AddComponent<GrayboxDefenseHudView3D>();
            view.Configure(canvas, eventSystem);
            return new HudFixture(view, canvas);
        }

        private static GrayboxDefenseRuntimeSnapshot3D Snapshot(
            WavePhase wavePhase = WavePhase.Idle,
            float warningSeconds = 0f,
            int coreHealth = 2000,
            IReadOnlyList<GrayboxDefenseTowerSnapshot3D> towers = null,
            IReadOnlyList<GrayboxDefenseEnemySnapshot3D> enemies = null)
        {
            towers = towers ?? Array.Empty<GrayboxDefenseTowerSnapshot3D>();
            enemies = enemies ?? Array.Empty<GrayboxDefenseEnemySnapshot3D>();
            return new GrayboxDefenseRuntimeSnapshot3D(
                wavePhase == WavePhase.Idle ? 0 : 1,
                wavePhase,
                warningSeconds,
                enemies.Count,
                enemies.Count,
                0,
                2000,
                coreHealth,
                towers,
                enemies);
        }

        private static GrayboxDefenseTowerSnapshot3D Tower(
            string stableId,
            int ammo = 30,
            bool connected = true,
            string targetId = null,
            GrayboxDefenseTowerStatus3D status =
                GrayboxDefenseTowerStatus3D.NoTarget)
        {
            return new GrayboxDefenseTowerSnapshot3D(
                stableId,
                ammo,
                30,
                connected,
                false,
                targetId,
                status);
        }

        private static GrayboxDefenseEnemySnapshot3D Enemy(
            string stableId,
            int order,
            float x,
            float z,
            int health,
            bool attacking = false)
        {
            return new GrayboxDefenseEnemySnapshot3D(
                stableId,
                order,
                x,
                z,
                health,
                attacking);
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
            string stableId,
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

        private static Rect CanvasRect(Canvas canvas, RectTransform target)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                canvasRect,
                target);
            Vector2 offset = canvasRect.rect.size * .5f;
            return Rect.MinMaxRect(
                bounds.min.x + offset.x,
                bounds.min.y + offset.y,
                bounds.max.x + offset.x,
                bounds.max.y + offset.y);
        }

        private static Transform[] Children(Transform parent)
        {
            return Enumerable.Range(0, parent.childCount)
                .Select(parent.GetChild)
                .ToArray();
        }

        private static int[] VisualObjectIds(
            GrayboxDefenseWorldView3D view)
        {
            return view.GetComponentsInChildren<Transform>(true)
                .Select(value => value.gameObject.GetInstanceID())
                .OrderBy(value => value)
                .ToArray();
        }

        private static int[] ComponentIds<T>(
            GrayboxDefenseWorldView3D view)
            where T : Component
        {
            return view.GetComponentsInChildren<T>(true)
                .Select(value => value.GetInstanceID())
                .OrderBy(value => value)
                .ToArray();
        }

        private static int CountNamedMaterials(string name)
        {
            return Resources.FindObjectsOfTypeAll<Material>().Count(material =>
                material.name.StartsWith(name, StringComparison.Ordinal));
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private Material CreateMaterial()
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader)
            {
                name = "DefenseSharedMaterial." + Guid.NewGuid().ToString("N"),
            };
            return material;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class WorldFixture
        {
            public WorldFixture(
                GrayboxDefenseWorldView3D view,
                Transform enemyRoot,
                Transform towerRoot,
                Material sharedMaterial,
                PlanarCoordinateMapper3D mapper)
            {
                View = view;
                EnemyRoot = enemyRoot;
                TowerRoot = towerRoot;
                SharedMaterial = sharedMaterial;
                Mapper = mapper;
            }

            public GrayboxDefenseWorldView3D View { get; }
            public Transform EnemyRoot { get; }
            public Transform TowerRoot { get; }
            public Material SharedMaterial { get; }
            public PlanarCoordinateMapper3D Mapper { get; }
        }

        private sealed class HudFixture
        {
            public HudFixture(GrayboxDefenseHudView3D view, Canvas canvas)
            {
                View = view;
                Canvas = canvas;
            }

            public GrayboxDefenseHudView3D View { get; }
            public Canvas Canvas { get; }
        }
    }
}
