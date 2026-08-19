using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxMobileCityController3DTests
    {
        private const float MoveSpeed = 4f;

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private SimulationMode originalSimulationMode;

        [SetUp]
        public void SetUp()
        {
            originalSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                for (int index = cleanup.Count - 1; index >= 0; index--)
                {
                    if (cleanup[index] != null)
                        UnityEngine.Object.DestroyImmediate(cleanup[index]);
                }
                cleanup.Clear();
            }
            finally
            {
                Physics.simulationMode = originalSimulationMode;
            }
        }

        [Test]
        public void Configure_CreatesDeploymentAndKinematicBodyContract()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);

            Assert.That(fixture.Controller.Deployment, Is.Not.Null);
            Assert.That(fixture.Controller.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.Body.isKinematic, Is.True);
            Assert.That(fixture.Body.useGravity, Is.False);
            Assert.That(
                fixture.Body.interpolation,
                Is.EqualTo(RigidbodyInterpolation.Interpolate));
            Assert.That(
                fixture.Body.constraints,
                Is.EqualTo(
                    RigidbodyConstraints.FreezePositionY |
                    RigidbodyConstraints.FreezeRotation));
        }

        [Test]
        public void TickMovement_NormalizesManualInputAndPreservesY()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                3,
                3);
            Vector3 start = fixture.Body.position;

            fixture.Controller.ApplyManualInput(new Vector2(3f, 4f));
            fixture.Controller.TickMovement(.1f);
            fixture.SimulateFixedStep();

            Assert.That(
                fixture.Body.position.x - start.x,
                Is.EqualTo(.24f).Within(.0001f));
            Assert.That(
                fixture.Body.position.z - start.z,
                Is.EqualTo(.32f).Within(.0001f));
            Assert.That(fixture.Body.position.y, Is.EqualTo(start.y));
        }

        [TestCase(
            WorldTraversalKind.Ruins,
            TerrainKind.Wasteland)]
        [TestCase(
            WorldTraversalKind.Open,
            TerrainKind.Wetland)]
        [TestCase(
            WorldTraversalKind.Open,
            TerrainKind.Rocky)]
        [TestCase(
            WorldTraversalKind.Open,
            TerrainKind.Wasteland)]
        public void TickMovement_UsesExistingTerrainMultiplier(
            WorldTraversalKind traversal,
            TerrainKind terrain)
        {
            var cell = new WorldCell(terrain, null, 0, traversal);
            ControllerFixture fixture = CreateFixture(
                FilledMap(7, 7, cell),
                3,
                3);
            Vector3 start = fixture.Body.position;
            float expected =
                MoveSpeed * CityTerrainRules.SpeedMultiplier(cell) * .1f;

            fixture.Controller.ApplyManualInput(Vector2.right);
            fixture.Controller.TickMovement(.1f);
            fixture.SimulateFixedStep();

            Assert.That(
                fixture.Body.position.x - start.x,
                Is.EqualTo(expected).Within(.0001f));
            Assert.That(fixture.Body.position.y, Is.EqualTo(start.y));
            Assert.That(fixture.Body.position.z, Is.EqualTo(start.z));
        }

        [Test]
        public void ApplyManualInput_CancelsExistingAutopilot()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                3,
                3);
            Assert.That(
                fixture.Controller.TrySetDestinationCell(4, 3, out _),
                Is.True);
            Assert.That(fixture.Controller.AutopilotActive, Is.True);

            fixture.Controller.ApplyManualInput(Vector2.up);

            Assert.That(fixture.Controller.AutopilotActive, Is.False);
            Assert.That(fixture.Controller.Destination, Is.Null);
        }

        [Test]
        public void Autopilot_UsesFourDirectionPathAndCompletes()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                2,
                2);
            Assert.That(
                fixture.Controller.TrySetDestinationCell(4, 3, out _),
                Is.True);
            Vector3 start = fixture.Body.position;

            fixture.Controller.TickMovement(.05f);
            fixture.SimulateFixedStep();

            Vector3 firstStep = fixture.Body.position - start;
            bool movedOnlyOnePlaneAxis =
                Mathf.Abs(firstStep.x) > .0001f &&
                Mathf.Abs(firstStep.z) < .0001f ||
                Mathf.Abs(firstStep.z) > .0001f &&
                Mathf.Abs(firstStep.x) < .0001f;
            Assert.That(movedOnlyOnePlaneAxis, Is.True);

            for (int index = 0;
                 index < 40 && fixture.Controller.AutopilotActive;
                 index++)
            {
                fixture.Controller.TickMovement(.05f);
                fixture.SimulateFixedStep();
            }

            Assert.That(fixture.Controller.AutopilotActive, Is.False);
            Assert.That(fixture.Controller.Destination, Is.Null);
            Assert.That(
                fixture.View.Coordinates.TryCellToWorld(
                    4,
                    3,
                    fixture.Body.position.y,
                    out Vector3 expected),
                Is.True);
            Assert.That(
                Vector3.Distance(fixture.Body.position, expected),
                Is.LessThanOrEqualTo(.08f));
        }

        [Test]
        public void Autopilot_CompletesInsidePointZeroEightTolerance()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                3,
                3);
            Assert.That(
                fixture.Controller.TrySetDestinationCell(4, 3, out _),
                Is.True);
            fixture.View.Coordinates.TryCellToWorld(
                4,
                3,
                fixture.Body.position.y,
                out Vector3 target);
            fixture.Body.position = target + Vector3.left * .079f;

            fixture.Controller.TickMovement(.02f);
            fixture.SimulateFixedStep();

            Assert.That(fixture.Controller.AutopilotActive, Is.False);
            Assert.That(fixture.Controller.Destination, Is.Null);
        }

        [TestCase(WorldTraversalKind.DeepWater)]
        [TestCase(WorldTraversalKind.Cliff)]
        public void TrySetDestination_BlockedTargetPreservesOldPath(
            WorldTraversalKind traversal)
        {
            WorldCell[,] cells = FilledMap(7, 7, OpenCell());
            cells[5, 3] = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                traversal);
            ControllerFixture fixture = CreateFixture(cells, 3, 3);
            Assert.That(
                fixture.Controller.TrySetDestinationCell(4, 3, out _),
                Is.True);

            Assert.That(
                fixture.Controller.TrySetDestinationCell(5, 3, out _),
                Is.False);

            AssertDestination(fixture.Controller, 4, 3);
        }

        [Test]
        public void TrySetDestination_OutsideWorldPreservesOldPath()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                3,
                3);
            Assert.That(
                fixture.Controller.TrySetDestinationCell(4, 3, out _),
                Is.True);

            Assert.That(
                fixture.Controller.TrySetDestinationCell(99, 99, out _),
                Is.False);

            AssertDestination(fixture.Controller, 4, 3);
        }

        [Test]
        public void TrySetDestination_UnreachableTargetPreservesOldPath()
        {
            WorldCell[,] cells = FilledMap(7, 7, OpenCell());
            WorldCell water = BlockedCell(WorldTraversalKind.DeepWater);
            cells[4, 5] = water;
            cells[5, 4] = water;
            cells[6, 5] = water;
            cells[5, 6] = water;
            ControllerFixture fixture = CreateFixture(cells, 3, 3);
            Assert.That(
                fixture.Controller.TrySetDestinationCell(4, 3, out _),
                Is.True);

            Assert.That(
                fixture.Controller.TrySetDestinationCell(5, 5, out _),
                Is.False);

            AssertDestination(fixture.Controller, 4, 3);
        }

        [TestCase(WorldTraversalKind.DeepWater)]
        [TestCase(WorldTraversalKind.Cliff)]
        public void TickMovement_BlockedCandidateDoesNotMove(
            WorldTraversalKind traversal)
        {
            WorldCell[,] cells = FilledMap(7, 7, OpenCell());
            cells[4, 3] = BlockedCell(traversal);
            ControllerFixture fixture = CreateFixture(cells, 3, 3);
            Vector3 start = fixture.Body.position;

            fixture.Controller.ApplyManualInput(Vector2.right);
            fixture.Controller.TickMovement(.3f);
            fixture.SimulateFixedStep();

            Assert.That(fixture.Body.position, Is.EqualTo(start));
        }

        [TestCase(CityMode.Deploying)]
        [TestCase(CityMode.Fortress)]
        [TestCase(CityMode.Packing)]
        public void TickMovement_NonMobileModeDoesNotMove(CityMode mode)
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                3,
                3);
            fixture.Controller.Deployment.Restore(mode, 1f);
            Vector3 start = fixture.Body.position;

            fixture.Controller.ApplyManualInput(Vector2.right);
            fixture.Controller.TickMovement(1f);
            fixture.SimulateFixedStep();

            Assert.That(fixture.Body.position, Is.EqualTo(start));
        }

        [TestCase(
            CityDeploymentFailure.OutsideWorld,
            0,
            0)]
        [TestCase(
            CityDeploymentFailure.Blocked,
            2,
            2)]
        [TestCase(
            CityDeploymentFailure.UnstableGround,
            2,
            2)]
        public void TryToggleDeployment_PreservesExistingFailureSemantics(
            CityDeploymentFailure expectedFailure,
            int startX,
            int startY)
        {
            WorldCell[,] cells = FilledMap(5, 5, OpenCell());
            if (expectedFailure == CityDeploymentFailure.Blocked)
                cells[1, 2] =
                    BlockedCell(WorldTraversalKind.DeepWater);
            if (expectedFailure == CityDeploymentFailure.UnstableGround)
                cells[1, 2] = new WorldCell(
                    TerrainKind.Wasteland,
                    null,
                    0,
                    WorldTraversalKind.Ruins);
            ControllerFixture fixture =
                CreateFixture(cells, startX, startY);

            bool started =
                fixture.Controller.TryToggleDeployment(out string reason);

            Assert.That(started, Is.False);
            Assert.That(
                fixture.Controller.LastDeploymentFailure,
                Is.EqualTo(expectedFailure));
            Assert.That(
                reason,
                Is.EqualTo(
                    CityDeploymentRules.FailureReason(expectedFailure)));
            Assert.That(
                fixture.Controller.LastFailureReason,
                Is.EqualTo(reason));
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Mobile));
        }

        [Test]
        public void TickDeployment_UsesFiveSecondDeployAndEightSecondPack()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Deploying));
            fixture.Controller.TickDeployment(4.99f);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Deploying));
            fixture.Controller.TickDeployment(.01f);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Fortress));

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Packing));
            fixture.Controller.TickDeployment(7.99f);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Packing));
            fixture.Controller.TickDeployment(.02f);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Mobile));
        }

        [Test]
        public void TickDeployment_InterpolatesVisualAndColliderWithStableId()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);
            AssertPresentation(
                fixture,
                new Vector3(3f, 1f, 2f),
                "core.city.mobile");

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            fixture.Controller.TickDeployment(2.5f);
            AssertPresentation(
                fixture,
                new Vector3(3f, 1.25f, 2.5f),
                "core.city.mobile");

            fixture.Controller.TickDeployment(2.5f);
            AssertPresentation(
                fixture,
                new Vector3(3f, 1.5f, 3f),
                "core.city.mobile");

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            fixture.Controller.TickDeployment(4f);
            AssertPresentation(
                fixture,
                new Vector3(3f, 1.25f, 2.5f),
                "core.city.mobile");

            fixture.Controller.TickDeployment(4f);
            AssertPresentation(
                fixture,
                new Vector3(3f, 1f, 2f),
                "core.city.mobile");
        }

        [Test]
        public void TryToggleDeployment_CancelsTransitionsAndDiscardsProgress()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            fixture.Controller.TickDeployment(2f);
            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            Assert.That(fixture.Controller.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.Controller.Deployment.Remaining, Is.Zero);

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            fixture.Controller.TickDeployment(4.99f);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Deploying),
                "A cancelled deployment must restart from the full duration.");
            fixture.Controller.TickDeployment(.01f);
            Assert.That(fixture.Controller.Mode, Is.EqualTo(CityMode.Fortress));

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            fixture.Controller.TickDeployment(3f);
            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            Assert.That(fixture.Controller.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.Controller.Deployment.Remaining, Is.Zero);

            Assert.That(
                fixture.Controller.TryToggleDeployment(out _),
                Is.True);
            fixture.Controller.TickDeployment(7.99f);
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Packing),
                "A cancelled packing transition must restart in full.");
            fixture.Controller.TickDeployment(.01f);
            Assert.That(fixture.Controller.Mode, Is.EqualTo(CityMode.Mobile));
        }

        [Test]
        public void TickDeployment_IsPausedByZeroDeltaAndFramePartitionInvariant()
        {
            ControllerFixture whole = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);
            ControllerFixture split = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);
            Assert.That(whole.Controller.TryToggleDeployment(out _), Is.True);
            Assert.That(split.Controller.TryToggleDeployment(out _), Is.True);

            whole.Controller.TickDeployment(0f);
            Assert.That(whole.Controller.Mode, Is.EqualTo(CityMode.Deploying));
            Assert.That(whole.Controller.Deployment.Remaining, Is.EqualTo(5f));

            whole.Controller.TickDeployment(5f);
            for (int index = 0; index < 50; index++)
                split.Controller.TickDeployment(.1f);

            Assert.That(split.Controller.Mode, Is.EqualTo(whole.Controller.Mode));
            Assert.That(
                split.Controller.Deployment.Remaining,
                Is.EqualTo(whole.Controller.Deployment.Remaining)
                    .Within(.0001f));
            Assert.That(
                split.Controller.Deployment.Progress,
                Is.EqualTo(whole.Controller.Deployment.Progress)
                    .Within(.0001f));
        }

        [Test]
        public void TickDeployment_CombinesGameplayAndDevelopmentRuleTime()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);
            fixture.Controller.ConfigureRuleTimeSource(
                new FixedRuleTimeSource(1f, 10f));
            Assert.That(fixture.Controller.TryToggleDeployment(out _), Is.True);

            fixture.Controller.TickDeployment(.5f);

            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CityMode.Fortress),
                "0.5s × 1.0 gameplay productivity × 10x development rule time must advance 5 base seconds.");
        }

        [Test]
        public void Packing_ReadsAliveEnemyCountEachSegmentAndRecoversAfterCombat()
        {
            ControllerFixture fixture = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);
            GrayboxBuildingSession3D ruleTimeSource =
                CreateFormalRuleTimeSession();
            fixture.Controller.ConfigureRuleTimeSource(
                ruleTimeSource);
            GrayboxDefenseRuntimeSnapshot3D snapshot =
                DefenseSnapshot(WavePhase.Warning, spawnedEnemyCount: 0);
            ConfigureAliveEnemyCountSource(
                fixture.Controller,
                () => snapshot.AliveEnemyCount);
            BeginPacking(fixture.Controller);

            fixture.Controller.TickDeployment(.4f);

            Assert.That(fixture.Controller.Deployment.Remaining,
                Is.EqualTo(7f).Within(.0001f),
                "Warning with no living enemy must use the full 1.25 × 2.0 rule-time advance.");

            snapshot = DefenseSnapshot(
                WavePhase.Active,
                1,
                Enemy("enemy.packing.1"));
            fixture.Controller.TickDeployment(.4f);

            Assert.That(fixture.Controller.Deployment.Remaining,
                Is.EqualTo(6.3f).Within(.0001f),
                "A live enemy must apply 0.7 after formal productivity and development rule time.");

            snapshot = DefenseSnapshot(
                WavePhase.Active,
                spawnedEnemyCount: 1);
            fixture.Controller.TickDeployment(.4f);

            Assert.That(fixture.Controller.Deployment.Remaining,
                Is.EqualTo(5.3f).Within(.0001f),
                "The next Packing segment after the final enemy dies must immediately return to 1.0 combat scaling.");
        }

        [Test]
        public void Packing_CombatZeroDeltaPausesAndLargeDeltaMatchesPartitions()
        {
            GrayboxDefenseRuntimeSnapshot3D combat = DefenseSnapshot(
                WavePhase.Active,
                1,
                Enemy("enemy.partition.1"));
            ControllerFixture whole = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);
            ControllerFixture split = CreateFixture(
                FilledMap(5, 5, OpenCell()),
                2,
                2);
            GrayboxBuildingSession3D ruleTimeSource =
                CreateFormalRuleTimeSession();
            whole.Controller.ConfigureRuleTimeSource(
                ruleTimeSource);
            split.Controller.ConfigureRuleTimeSource(
                ruleTimeSource);
            ConfigureAliveEnemyCountSource(
                whole.Controller,
                () => combat.AliveEnemyCount);
            ConfigureAliveEnemyCountSource(
                split.Controller,
                () => combat.AliveEnemyCount);
            BeginPacking(whole.Controller);
            BeginPacking(split.Controller);

            whole.Controller.TickDeployment(0f);

            Assert.That(whole.Controller.Deployment.Remaining,
                Is.EqualTo(8f).Within(.0001f),
                "System or tactical pause supplies zero rule delta and must not advance Packing.");

            whole.Controller.TickDeployment(4f);
            for (int index = 0; index < 40; index++)
                split.Controller.TickDeployment(.1f);

            Assert.That(whole.Controller.Mode, Is.EqualTo(CityMode.Packing));
            Assert.That(split.Controller.Mode, Is.EqualTo(CityMode.Packing));
            Assert.That(whole.Controller.Deployment.Remaining,
                Is.EqualTo(1f).Within(.0001f));
            Assert.That(split.Controller.Deployment.Remaining,
                Is.EqualTo(whole.Controller.Deployment.Remaining)
                    .Within(.0001f));
            Assert.That(split.Controller.Deployment.Progress,
                Is.EqualTo(whole.Controller.Deployment.Progress)
                    .Within(.0001f));
        }

        private static void ConfigureAliveEnemyCountSource(
            GrayboxMobileCityController3D controller,
            Func<int> source)
        {
            MethodInfo method = typeof(GrayboxMobileCityController3D)
                .GetMethod(
                    "ConfigureAliveEnemyCountSource",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(Func<int>),
                    },
                    null);
            Assert.That(method, Is.Not.Null,
                "Packing requires a public narrow configuration seam that reads AliveEnemyCount from the current authoritative defense snapshot instead of storing a second enemy/combat bool.");
            method.Invoke(controller, new object[] { source });
        }

        private static void BeginPacking(
            GrayboxMobileCityController3D controller)
        {
            Assert.That(controller.TryToggleDeployment(out _), Is.True);
            Assert.That(
                controller.CompleteDeploymentTransitionForDevelopment(),
                Is.True);
            Assert.That(controller.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(controller.TryToggleDeployment(out _), Is.True);
            Assert.That(controller.Mode, Is.EqualTo(CityMode.Packing));
        }

        private static GrayboxDefenseRuntimeSnapshot3D DefenseSnapshot(
            WavePhase wavePhase,
            int spawnedEnemyCount,
            params GrayboxDefenseEnemySnapshot3D[] enemies)
        {
            enemies = enemies ?? Array.Empty<GrayboxDefenseEnemySnapshot3D>();
            return new GrayboxDefenseRuntimeSnapshot3D(
                wavePhase == WavePhase.Idle ? 0 : 1,
                wavePhase,
                wavePhase == WavePhase.Warning ? 15f : 0f,
                spawnedEnemyCount,
                enemies.Length,
                Math.Max(0, spawnedEnemyCount - enemies.Length),
                2000,
                2000,
                Array.Empty<GrayboxDefenseTowerSnapshot3D>(),
                enemies);
        }

        private static GrayboxDefenseEnemySnapshot3D Enemy(string stableId)
        {
            return new GrayboxDefenseEnemySnapshot3D(
                stableId,
                1,
                4f,
                2f,
                100,
                isAttackingCore: false);
        }

        private GrayboxBuildingSession3D CreateFormalRuleTimeSession()
        {
            var root = Track(new GameObject("FormalRuleTimeSession"));
            GrayboxBuildingSession3D session =
                root.AddComponent<GrayboxBuildingSession3D>();
            session.SetPopulationForDevelopment(150);
            session.SetConstructionMultiplierForDevelopment(2f);
            Assert.That(session.ProductivityMultiplier,
                Is.EqualTo(1.25f).Within(.0001f));
            Assert.That(session.DevelopmentRuleTimeMultiplier,
                Is.EqualTo(2f).Within(.0001f));
            return session;
        }

        private sealed class FixedRuleTimeSource : IGrayboxRuleTimeSource3D
        {
            public FixedRuleTimeSource(
                float productivityMultiplier,
                float developmentRuleTimeMultiplier)
            {
                RuleTimeContext = new GrayboxRuleTimeContext3D(
                    productivityMultiplier,
                    developmentRuleTimeMultiplier);
            }

            public GrayboxRuleTimeContext3D RuleTimeContext { get; }
        }

        private ControllerFixture CreateFixture(
            WorldCell[,] cells,
            int startX,
            int startY)
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = Track(new Material(shader));

            var worldRoot = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(
                worldRoot.transform,
                "TerrainRoot");
            Transform resources = NewChild(
                worldRoot.transform,
                "ResourceRoot");
            Transform obstacles = NewChild(
                worldRoot.transform,
                "ObstacleRoot");
            GrayboxWorldView3D view =
                worldRoot.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            view.Generate(new WorldMapModel(cells));

            var city = Track(new GameObject("MobileCity"));
            Assert.That(
                view.Coordinates.TryCellToWorld(
                    startX,
                    startY,
                    .5f,
                    out Vector3 start),
                Is.True);
            city.transform.position = start;
            Rigidbody body = city.AddComponent<Rigidbody>();
            BoxCollider bodyCollider = city.AddComponent<BoxCollider>();

            Transform visualTransform =
                NewChild(city.transform, "Visual");
            MeshRenderer renderer =
                visualTransform.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            GrayboxVisualSlot visual =
                visualTransform.gameObject
                    .AddComponent<GrayboxVisualSlot>();
            visual.Configure(
                "core.city.mobile",
                renderer,
                new Color(.9f, .48f, .1f));
            visual.ApplyFallback(material);

            GrayboxMobileCityController3D controller =
                city.AddComponent<GrayboxMobileCityController3D>();
            controller.Configure(view, body, bodyCollider);

            return new ControllerFixture(
                controller,
                view,
                body,
                bodyCollider,
                visual,
                visualTransform,
                start);
        }

        private static void AssertPresentation(
            ControllerFixture fixture,
            Vector3 expectedSize,
            string expectedStableId)
        {
            Assert.That(
                fixture.VisualTransform.localScale,
                Is.EqualTo(expectedSize));
            Assert.That(
                fixture.BodyCollider.size,
                Is.EqualTo(expectedSize));
            Assert.That(
                fixture.Visual.StableId,
                Is.EqualTo(expectedStableId));
            float visualBottom =
                fixture.VisualTransform.position.y -
                fixture.VisualTransform.lossyScale.y * .5f;
            float colliderBottom =
                fixture.Body.position.y +
                fixture.BodyCollider.center.y -
                fixture.BodyCollider.size.y * .5f;
            Assert.That(visualBottom, Is.EqualTo(0f).Within(.0001f));
            Assert.That(colliderBottom, Is.EqualTo(0f).Within(.0001f));
        }

        private static void AssertDestination(
            GrayboxMobileCityController3D controller,
            int expectedX,
            int expectedY)
        {
            Assert.That(controller.AutopilotActive, Is.True);
            Assert.That(controller.Destination.HasValue, Is.True);
            Assert.That(
                controller.Destination.Value.X,
                Is.EqualTo(expectedX));
            Assert.That(
                controller.Destination.Value.Y,
                Is.EqualTo(expectedY));
        }

        private static WorldCell[,] FilledMap(
            int width,
            int height,
            WorldCell cell)
        {
            var cells = new WorldCell[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = cell;
            return cells;
        }

        private static WorldCell OpenCell()
        {
            return new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
        }

        private static WorldCell BlockedCell(
            WorldTraversalKind traversal)
        {
            return new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                traversal);
        }

        private static Transform NewChild(
            Transform parent,
            string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class ControllerFixture
        {
            public GrayboxMobileCityController3D Controller { get; }
            public GrayboxWorldView3D View { get; }
            public Rigidbody Body { get; }
            public BoxCollider BodyCollider { get; }
            public GrayboxVisualSlot Visual { get; }
            public Transform VisualTransform { get; }
            public Vector3 Start { get; }

            public ControllerFixture(
                GrayboxMobileCityController3D controller,
                GrayboxWorldView3D view,
                Rigidbody body,
                BoxCollider bodyCollider,
                GrayboxVisualSlot visual,
                Transform visualTransform,
                Vector3 start)
            {
                Controller = controller;
                View = view;
                Body = body;
                BodyCollider = bodyCollider;
                Visual = visual;
                VisualTransform = visualTransform;
                Start = start;
            }

            public void SimulateFixedStep()
            {
                Physics.SyncTransforms();
                Physics.Simulate(.02f);
            }
        }
    }
}
