using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.City;
using WasteCity.Leader;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FormalCameraControllerTests
    {
        private CameraFixture fixture;

        [SetUp]
        public void SetUp()
        {
            fixture = new CameraFixture();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            fixture.Dispose();
        }

        [Test]
        public void FollowingCopiesCityXYAndPreservesCameraZ()
        {
            fixture.CityObject.transform.position =
                new Vector3(2.5f, -3.25f, -1f);
            fixture.CameraObject.transform.position =
                new Vector3(80f, 90f, -17f);

            fixture.Controller.TickCamera();

            Assert.That(
                fixture.CameraObject.transform.position,
                Is.EqualTo(new Vector3(2.5f, -3.25f, -17f)));
        }

        [Test]
        public void FreeDragMovesOppositePointerAndPreservesZ()
        {
            fixture.CityObject.transform.position =
                new Vector3(2f, 3f, -1f);
            fixture.CameraObject.transform.position =
                new Vector3(2f, 3f, -17f);
            fixture.Camera.orthographicSize = 10f;
            fixture.Controller.BeginFreeDrag();

            fixture.Controller.ApplyPointerDelta(
                new Vector2(10f, -5f),
                screenHeight: 100f);

            Assert.That(fixture.Controller.Mode, Is.EqualTo(CameraFollowMode.Free));
            Vector3 position = fixture.CameraObject.transform.position;
            Assert.That(position.x, Is.EqualTo(0f).Within(.0001f));
            Assert.That(position.y, Is.EqualTo(4f).Within(.0001f));
            Assert.That(position.z, Is.EqualTo(-17f).Within(.0001f));
        }

        [Test]
        public void PauseDoesNotBlockDragOrSameCallReturn()
        {
            fixture.CityObject.transform.position =
                new Vector3(1f, 2f, -1f);
            fixture.Controller.TickCamera();
            Time.timeScale = 0f;
            fixture.Controller.BeginFreeDrag();
            fixture.Controller.ApplyPointerDelta(
                new Vector2(10f, 10f),
                screenHeight: 100f);
            fixture.CityObject.transform.position =
                new Vector3(7f, -6f, -1f);

            fixture.Controller.ReturnToTarget();

            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(
                fixture.CameraObject.transform.position,
                Is.EqualTo(new Vector3(7f, -6f, -17f)));
        }

        [Test]
        public void PointerTranslationUsesPressBaselineAndReleasePosition()
        {
            fixture.CityObject.transform.position =
                new Vector3(0f, 0f, -1f);
            fixture.Controller.TickCamera();
            fixture.Controller.ProcessPointerState(
                new Vector2(10f, 10f),
                pressedThisFrame: false,
                releasedThisFrame: false,
                screenHeight: 100f);

            fixture.Controller.ProcessPointerState(
                new Vector2(20f, 10f),
                pressedThisFrame: true,
                releasedThisFrame: false,
                screenHeight: 100f);

            Assert.That(
                fixture.CameraObject.transform.position.x,
                Is.EqualTo(0f).Within(.0001f));
            fixture.Controller.ProcessPointerState(
                new Vector2(30f, 10f),
                pressedThisFrame: false,
                releasedThisFrame: false,
                screenHeight: 100f);
            Assert.That(
                fixture.CameraObject.transform.position.x,
                Is.EqualTo(-2f).Within(.0001f));

            fixture.Controller.ProcessPointerState(
                new Vector2(40f, 10f),
                pressedThisFrame: false,
                releasedThisFrame: true,
                screenHeight: 100f);

            Assert.That(
                fixture.CameraObject.transform.position.x,
                Is.EqualTo(-4f).Within(.0001f));
            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CameraFollowMode.Free));
        }

        [Test]
        public void MissingLeaderControllerSafelyFollowsCity()
        {
            fixture.CityObject.transform.position =
                new Vector3(-4f, 6f, -1f);
            fixture.Controller.Configure(
                fixture.Camera,
                fixture.City,
                null,
                fixture.LeaderVisual.transform);

            fixture.Controller.TickCamera();

            Assert.That(
                fixture.Controller.CurrentTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(
                fixture.CameraObject.transform.position,
                Is.EqualTo(new Vector3(-4f, 6f, -17f)));
        }

        [Test]
        public void UnavailableLeaderTargetSafelyFallsBackToCity()
        {
            fixture.CityObject.transform.position =
                new Vector3(4f, -2f, -1f);
            fixture.City.RestoreDeployment(CityMode.Fortress, 0f);
            fixture.Leader.Restore(true, false, 0f, 0f, 0f);
            fixture.LeaderVisual.transform.position =
                new Vector3(40f, 50f, 0f);
            fixture.Controller.Configure(
                fixture.Camera,
                fixture.City,
                fixture.Leader,
                null);

            fixture.Controller.TickCamera();

            Assert.That(
                fixture.Controller.CurrentTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(
                fixture.CameraObject.transform.position,
                Is.EqualTo(new Vector3(4f, -2f, -17f)));
        }

        [Test]
        public void DisabledLeaderControllerRestoresCityFollow()
        {
            AssertUnavailableLeaderControllerRestoresCityFollow(
                disableController: true);
        }

        [Test]
        public void InactiveLeaderControllerObjectRestoresCityFollow()
        {
            AssertUnavailableLeaderControllerRestoresCityFollow(
                disableController: false);
        }

        [Test]
        public void EffectiveTargetSwitchRestoresFollowingAndSnapsToLeader()
        {
            fixture.CityObject.transform.position =
                new Vector3(1f, 2f, -1f);
            fixture.Controller.TickCamera();
            fixture.Controller.BeginFreeDrag();
            fixture.Controller.ApplyPointerDelta(
                new Vector2(10f, 0f),
                screenHeight: 100f);
            fixture.City.RestoreDeployment(CityMode.Fortress, 0f);
            fixture.Leader.Restore(true, false, 0f, 0f, 0f);
            fixture.LeaderVisual.transform.position =
                new Vector3(8f, 9f, 0f);

            fixture.Controller.TickCamera();

            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(
                fixture.Controller.CurrentTarget,
                Is.EqualTo(DirectControlTarget.Leader));
            Assert.That(
                fixture.CameraObject.transform.position,
                Is.EqualTo(new Vector3(8f, 9f, -17f)));
        }

        private void AssertUnavailableLeaderControllerRestoresCityFollow(
            bool disableController)
        {
            fixture.CityObject.transform.position =
                new Vector3(-5f, 3f, -1f);
            fixture.City.RestoreDeployment(CityMode.Fortress, 0f);
            fixture.Leader.Restore(true, false, 0f, 0f, 0f);
            fixture.LeaderVisual.transform.position =
                new Vector3(40f, 50f, 0f);
            fixture.Controller.TickCamera();
            Assert.That(
                fixture.Controller.CurrentTarget,
                Is.EqualTo(DirectControlTarget.Leader));
            fixture.Controller.BeginFreeDrag();

            if (disableController)
                fixture.Leader.enabled = false;
            else
                fixture.LeaderObject.SetActive(false);
            fixture.Controller.TickCamera();

            Assert.That(
                fixture.Controller.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(
                fixture.Controller.CurrentTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(
                fixture.CameraObject.transform.position,
                Is.EqualTo(new Vector3(-5f, 3f, -17f)));
        }

        private sealed class CameraFixture
        {
            public GameObject CameraObject { get; }
            public Camera Camera { get; }
            public FormalCameraController Controller { get; }
            public GameObject CityObject { get; }
            public PlaceholderMobileCity City { get; }
            public GameObject LeaderObject { get; }
            public GameObject LeaderVisual { get; }
            public FormalLeaderController Leader { get; }

            public CameraFixture()
            {
                CameraObject = new GameObject("CameraControlCamera");
                Camera = CameraObject.AddComponent<Camera>();
                Camera.orthographic = true;
                Camera.orthographicSize = 10f;
                CameraObject.transform.position =
                    new Vector3(0f, 0f, -17f);
                Controller =
                    CameraObject.AddComponent<FormalCameraController>();

                CityObject = new GameObject("CameraControlCity");
                CityObject.AddComponent<Rigidbody2D>();
                City = CityObject.AddComponent<PlaceholderMobileCity>();
                if (City.Deployment == null)
                    typeof(PlaceholderMobileCity)
                        .GetMethod(
                            "Awake",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(City, null);

                LeaderObject = new GameObject("CameraControlLeader");
                LeaderVisual = new GameObject("CameraControlLeaderVisual");
                Leader = LeaderObject.AddComponent<FormalLeaderController>();
                SetLeaderField("city", City);
                SetLeaderField("visual", LeaderVisual.transform);

                Controller.Configure(
                    Camera,
                    City,
                    Leader,
                    LeaderVisual.transform);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(LeaderVisual);
                Object.DestroyImmediate(LeaderObject);
                Object.DestroyImmediate(CityObject);
                Object.DestroyImmediate(CameraObject);
            }

            private void SetLeaderField(string name, object value)
            {
                typeof(FormalLeaderController)
                    .GetField(
                        name,
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(Leader, value);
            }
        }
    }
}
