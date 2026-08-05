using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.Leader;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxLeaderControlTests
    {
        private const float LeaderMoveSpeed = 5f;
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

        [TestCase(CityMode.Mobile, false)]
        [TestCase(CityMode.Mobile, true)]
        [TestCase(CityMode.Deploying, false)]
        [TestCase(CityMode.Deploying, true)]
        [TestCase(CityMode.Fortress, false)]
        [TestCase(CityMode.Fortress, true)]
        [TestCase(CityMode.Packing, false)]
        [TestCase(CityMode.Packing, true)]
        public void Refresh_MatchesDirectControlRules(
            CityMode mode,
            bool recruited)
        {
            LeaderFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                recruited);
            fixture.City.Deployment.Restore(mode, 1f);

            fixture.Coordinator.Refresh();

            DirectControlTarget expected =
                DirectControlRules.Resolve(mode, recruited);
            Assert.That(
                fixture.Coordinator.ControlTarget,
                Is.EqualTo(expected));
        }

        [Test]
        public void Configure_DefaultFixtureLeavesLeaderUnrecruited()
        {
            LeaderFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                false);

            Assert.That(
                fixture.Leader.DevelopmentFixtureRecruited,
                Is.False);
            Assert.That(fixture.Leader.Model.Recruited, Is.False);
            Assert.That(fixture.Leader.Model.Injured, Is.False);
            Assert.That(
                fixture.Leader.Model.Overload.Phase,
                Is.EqualTo(OverloadPhase.Ready));
        }

        [Test]
        public void Configure_DevelopmentFixtureRestoresHealthyReadyLeader()
        {
            LeaderFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                true);

            Assert.That(
                fixture.Leader.DevelopmentFixtureRecruited,
                Is.True);
            Assert.That(fixture.Leader.Model.Recruited, Is.True);
            Assert.That(fixture.Leader.Model.Injured, Is.False);
            Assert.That(
                fixture.Leader.Model.Overload.CooldownRemaining,
                Is.Zero);
            Assert.That(
                fixture.Leader.Model.Overload.BoostRemaining,
                Is.Zero);
            Assert.That(
                fixture.Leader.Model.Overload.LockoutRemaining,
                Is.Zero);
            Assert.That(
                fixture.Leader.Model.Overload.Phase,
                Is.EqualTo(OverloadPhase.Ready));
        }

        [Test]
        public void Refresh_MissingLeaderFallsBackToCity()
        {
            LeaderFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                true);
            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            Assert.That(fixture.Coordinator.Refresh(), Is.True);
            Assert.That(
                fixture.Coordinator.ControlTarget,
                Is.EqualTo(DirectControlTarget.Leader));

            fixture.Coordinator.Configure(fixture.City, null);

            Assert.That(fixture.Coordinator.Refresh(), Is.True);

            Assert.That(
                fixture.Coordinator.ControlTarget,
                Is.EqualTo(DirectControlTarget.City));
        }

        [Test]
        public void Refresh_TargetChangedFiresOncePerActualChange()
        {
            LeaderFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                true);
            int eventCount = 0;
            DirectControlTarget lastTarget = DirectControlTarget.City;
            fixture.Coordinator.TargetChanged += target =>
            {
                eventCount++;
                lastTarget = target;
            };

            Assert.That(fixture.Coordinator.Refresh(), Is.False);
            Assert.That(eventCount, Is.Zero);

            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            Assert.That(fixture.Coordinator.Refresh(), Is.True);
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(lastTarget, Is.EqualTo(DirectControlTarget.Leader));

            Assert.That(fixture.Coordinator.Refresh(), Is.False);
            Assert.That(eventCount, Is.EqualTo(1));

            fixture.City.Deployment.Restore(CityMode.Mobile, 0f);
            Assert.That(fixture.Coordinator.Refresh(), Is.True);
            Assert.That(eventCount, Is.EqualTo(2));
            Assert.That(lastTarget, Is.EqualTo(DirectControlTarget.City));

            Assert.That(fixture.Coordinator.Refresh(), Is.False);
            Assert.That(eventCount, Is.EqualTo(2));
        }

        [Test]
        public void TickControl_LeaderTargetMovesNormalizedXZAndPreservesY()
        {
            LeaderFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                true);
            Vector3 start = fixture.Leader.transform.position;
            Vector3 cityStart = fixture.City.transform.position;
            bool recruited = fixture.Leader.Model.Recruited;
            bool injured = fixture.Leader.Model.Injured;

            fixture.Leader.ApplyManualInput(new Vector2(3f, 4f));
            fixture.Leader.TickControl(DirectControlTarget.Leader, .1f);

            Vector3 position = fixture.Leader.transform.position;
            Assert.That(
                position.x - start.x,
                Is.EqualTo(
                    LeaderMoveSpeed * .6f * .1f).Within(.0001f));
            Assert.That(
                position.z - start.z,
                Is.EqualTo(
                    LeaderMoveSpeed * .8f * .1f).Within(.0001f));
            Assert.That(position.y, Is.EqualTo(start.y));
            Assert.That(fixture.City.transform.position, Is.EqualTo(cityStart));
            Assert.That(
                fixture.Leader.Model.Recruited,
                Is.EqualTo(recruited));
            Assert.That(fixture.Leader.Model.Injured, Is.EqualTo(injured));
        }

        [TestCase(WorldTraversalKind.DeepWater)]
        [TestCase(WorldTraversalKind.Cliff)]
        public void TickControl_BlockedTargetCellDoesNotMove(
            WorldTraversalKind traversal)
        {
            WorldCell[,] cells = FilledMap(7, 7, OpenCell());
            cells[4, 3] = BlockedCell(traversal);
            LeaderFixture fixture = CreateFixture(cells, true);
            Vector3 start = fixture.Leader.transform.position;

            fixture.Leader.ApplyManualInput(Vector2.right);
            fixture.Leader.TickControl(DirectControlTarget.Leader, .3f);

            Assert.That(fixture.Leader.transform.position, Is.EqualTo(start));
        }

        [Test]
        public void TickControl_CityTargetDocksAndIgnoresMovementInput()
        {
            LeaderFixture fixture = CreateFixture(
                FilledMap(7, 7, OpenCell()),
                true);
            Vector3 cityStart = fixture.City.transform.position;
            float leaderY = fixture.Leader.transform.position.y;
            bool recruited = fixture.Leader.Model.Recruited;
            bool injured = fixture.Leader.Model.Injured;
            fixture.Leader.transform.position +=
                new Vector3(-2f, 0f, 3f);

            fixture.Leader.ApplyManualInput(Vector2.left);
            fixture.Leader.TickControl(DirectControlTarget.City, 1f);

            Assert.That(
                fixture.Leader.transform.position,
                Is.EqualTo(
                    new Vector3(
                        cityStart.x + 1.8f,
                        leaderY,
                        cityStart.z + 1.2f)));
            Assert.That(fixture.City.transform.position, Is.EqualTo(cityStart));
            Assert.That(
                fixture.Leader.Model.Recruited,
                Is.EqualTo(recruited));
            Assert.That(fixture.Leader.Model.Injured, Is.EqualTo(injured));
        }

        private LeaderFixture CreateFixture(
            WorldCell[,] cells,
            bool developmentFixtureRecruited)
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

            var cityObject = Track(new GameObject("MobileCity"));
            Assert.That(
                view.Coordinates.TryCellToWorld(
                    3,
                    3,
                    .5f,
                    out Vector3 cityStart),
                Is.True);
            cityObject.transform.position = cityStart;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider bodyCollider =
                cityObject.AddComponent<BoxCollider>();
            Transform cityVisual = NewChild(
                cityObject.transform,
                "Visual");
            MeshRenderer cityRenderer =
                cityVisual.gameObject.AddComponent<MeshRenderer>();
            cityRenderer.sharedMaterial = material;
            GrayboxVisualSlot citySlot =
                cityVisual.gameObject.AddComponent<GrayboxVisualSlot>();
            citySlot.Configure(
                "core.city.mobile",
                cityRenderer,
                new Color(.9f, .48f, .1f));
            citySlot.ApplyFallback(material);

            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(view, body, bodyCollider);

            var leaderObject = Track(new GameObject("Leader"));
            Assert.That(
                view.Coordinates.TryCellToWorld(
                    3,
                    3,
                    .9f,
                    out Vector3 leaderStart),
                Is.True);
            leaderObject.transform.position = leaderStart;
            GrayboxLeaderController3D leader =
                leaderObject.AddComponent<GrayboxLeaderController3D>();
            leader.Configure(
                view,
                city,
                developmentFixtureRecruited);

            var coordinatorObject =
                Track(new GameObject("DirectControl"));
            GrayboxDirectControlCoordinator coordinator =
                coordinatorObject
                    .AddComponent<GrayboxDirectControlCoordinator>();
            coordinator.Configure(city, leader);

            return new LeaderFixture(
                view,
                city,
                leader,
                coordinator);
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

        private sealed class LeaderFixture
        {
            public GrayboxWorldView3D View { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxLeaderController3D Leader { get; }
            public GrayboxDirectControlCoordinator Coordinator { get; }

            public LeaderFixture(
                GrayboxWorldView3D view,
                GrayboxMobileCityController3D city,
                GrayboxLeaderController3D leader,
                GrayboxDirectControlCoordinator coordinator)
            {
                View = view;
                City = city;
                Leader = leader;
                Coordinator = coordinator;
            }
        }
    }
}
