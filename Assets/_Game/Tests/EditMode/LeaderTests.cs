using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.City;
using WasteCity.Leader;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class LeaderTests
    {
        [Test]
        public void ImmediateRescueProvidesFullOverload()
        {
            var model = new LeaderModel();
            model.Recruit(true);
            Assert.That(model.Overload.TryActivate(), Is.True);
            Assert.That(model.Overload.FireRateMultiplier, Is.EqualTo(1.75f));
            Assert.That(model.AssemblerEfficiency, Is.EqualTo(1.25f));
        }

        [Test]
        public void DelayedRescueProvidesReducedOverload()
        {
            var model = new LeaderModel();
            model.Recruit(false);
            model.Overload.TryActivate();
            Assert.That(model.Injured, Is.True);
            Assert.That(model.Overload.FireRateMultiplier, Is.EqualTo(1.35f));
        }

        [Test]
        public void OverloadBoostThenLocksTurretsBeforeCooldown()
        {
            var model = new LeaderModel();
            model.Recruit(true);
            model.Overload.TryActivate();
            model.Tick(5f);
            Assert.That(model.Overload.FireRateMultiplier, Is.Zero);
            model.Tick(3f);
            Assert.That(model.Overload.FireRateMultiplier, Is.EqualTo(1f));
            Assert.That(model.Overload.TryActivate(), Is.False);
            model.Tick(22f);
            Assert.That(model.Overload.TryActivate(), Is.True);
        }

        [Test]
        public void LeaderStateCanBeRestored()
        {
            var model = new LeaderModel();
            model.Restore(true, true, 12f, 0f, 0f);
            Assert.That(model.Recruited, Is.True);
            Assert.That(model.Injured, Is.True);
            Assert.That(model.Overload.CooldownRemaining, Is.EqualTo(12f));
        }

        [Test]
        public void RecruitedLeaderMovesUnderDirectFortressControl()
        {
            LeaderFixture fixture = null;
            try
            {
                fixture = new LeaderFixture();
                fixture.Leader.Model.Recruit(true);
                fixture.City.RestoreDeployment(CityMode.Fortress, 0f);
                fixture.Leader.RestorePosition(2f, 3f, true);

                fixture.Leader.ApplyManualInput(Vector2.right);
                fixture.Leader.TickDirectControl(.5f);

                Assert.That(fixture.Leader.ControlTarget, Is.EqualTo(DirectControlTarget.Leader));
                Assert.That(fixture.Leader.Position.x, Is.EqualTo(4.5f).Within(.001f));
                Assert.That(fixture.Leader.Position.y, Is.EqualTo(3f).Within(.001f));
            }
            finally
            {
                fixture?.Dispose();
            }
        }

        [Test]
        public void MobileModeReattachesLeaderToCity()
        {
            LeaderFixture fixture = null;
            try
            {
                fixture = new LeaderFixture();
                fixture.Leader.Model.Recruit(true);
                fixture.CityObject.transform.position = new Vector3(2f, 3f, -1f);
                fixture.City.RestoreDeployment(CityMode.Mobile, 0f);
                fixture.Leader.RestorePosition(9f, 9f, true);

                fixture.Leader.TickDirectControl(.1f);

                Assert.That(fixture.Leader.ControlTarget, Is.EqualTo(DirectControlTarget.City));
                Assert.That(fixture.Leader.Position.x, Is.EqualTo(3.8f).Within(.001f));
                Assert.That(fixture.Leader.Position.y, Is.EqualTo(4.2f).Within(.001f));
            }
            finally
            {
                fixture?.Dispose();
            }
        }

        [Test]
        public void LeaderCannotEnterDeepWaterOrCliff()
        {
            LeaderFixture fixture = null;
            try
            {
                fixture = new LeaderFixture();
                fixture.Leader.Model.Recruit(true);
                fixture.City.RestoreDeployment(CityMode.Fortress, 0f);
                FindBlockedWithOpenLeft(
                    fixture.World.Model,
                    out int blockedX,
                    out int blockedY);
                Vector2 start = fixture.World.CellToWorld(blockedX - 1, blockedY);
                fixture.Leader.RestorePosition(start.x, start.y, true);

                fixture.Leader.ApplyManualInput(Vector2.right);
                fixture.Leader.TickDirectControl(.25f);

                Assert.That(fixture.Leader.Position, Is.EqualTo(start));
            }
            finally
            {
                fixture?.Dispose();
            }
        }

        [Test]
        public void MissingSavedPositionUsesCityAttachmentPoint()
        {
            LeaderFixture fixture = null;
            try
            {
                fixture = new LeaderFixture();
                fixture.CityObject.transform.position = new Vector3(-4f, 2f, -1f);

                fixture.Leader.RestorePosition(99f, 99f, false);

                Assert.That(fixture.Leader.Position.x, Is.EqualTo(-2.2f).Within(.001f));
                Assert.That(fixture.Leader.Position.y, Is.EqualTo(3.2f).Within(.001f));
            }
            finally
            {
                fixture?.Dispose();
            }
        }

        private static void FindBlockedWithOpenLeft(
            WorldMapModel map,
            out int blockedX,
            out int blockedY)
        {
            for (int x = 1; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (!CityTerrainRules.IsPassable(map.Get(x, y)) &&
                        CityTerrainRules.IsPassable(map.Get(x - 1, y)))
                    {
                        blockedX = x;
                        blockedY = y;
                        return;
                    }
            throw new AssertionException("Generated map has no blocked cell with open left neighbor.");
        }

        private sealed class LeaderFixture
        {
            public GameObject WorldObject { get; }
            public PlaceholderWorldView World { get; }
            public GameObject CityObject { get; }
            public PlaceholderMobileCity City { get; }
            public GameObject LeaderObject { get; }
            public GameObject VisualObject { get; }
            public FormalLeaderController Leader { get; }

            public LeaderFixture()
            {
                WorldObject = new GameObject("LeaderControlWorld");
                World = WorldObject.AddComponent<PlaceholderWorldView>();
                World.Generate(new WorldSeed(8128));
                CityObject = new GameObject("LeaderControlCity");
                CityObject.AddComponent<Rigidbody2D>();
                City = CityObject.AddComponent<PlaceholderMobileCity>();
                if (City.Deployment == null)
                    typeof(PlaceholderMobileCity)
                        .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(City, null);
                City.ConfigureWorld(World);
                LeaderObject = new GameObject("LeaderController");
                VisualObject = new GameObject("LeaderVisual");
                VisualObject.AddComponent<SpriteRenderer>();
                Leader = LeaderObject.AddComponent<FormalLeaderController>();
                SetField("city", City);
                SetField("visual", VisualObject.transform);
                Leader.ConfigureWorld(World);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(VisualObject);
                Object.DestroyImmediate(LeaderObject);
                Object.DestroyImmediate(CityObject);
                Object.DestroyImmediate(WorldObject);
            }

            private void SetField(string name, object value)
            {
                typeof(FormalLeaderController)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(Leader, value);
            }
        }
    }
}
