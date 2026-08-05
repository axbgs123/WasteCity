using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class PlaceholderMobileCityTests
    {
        [Test]
        public void ReachableDestinationStartsAndManualInputCancelsAutopilot()
        {
            GameObject worldObject = null;
            GameObject cityObject = null;
            try
            {
                CreateRuntime(out worldObject, out PlaceholderWorldView world, out cityObject, out PlaceholderMobileCity city);
                FindReachablePair(world.Model, out int startX, out int startY, out int targetX, out int targetY);
                cityObject.transform.position = world.CellToWorld(startX, startY);

                Assert.That(
                    city.TrySetDestinationCell(targetX, targetY, out string reason),
                    Is.True,
                    reason);
                Assert.That(city.AutopilotActive, Is.True);

                city.ApplyManualInput(Vector2.right);

                Assert.That(city.AutopilotActive, Is.False);
                Assert.That(city.LastMobilityMessage, Is.EqualTo("直接驾驶：已取消自动驾驶"));
            }
            finally
            {
                if (cityObject != null) Object.DestroyImmediate(cityObject);
                if (worldObject != null) Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void InvalidDeploymentKeepsMobileModeAndReportsReason()
        {
            GameObject worldObject = null;
            GameObject cityObject = null;
            try
            {
                CreateRuntime(out worldObject, out PlaceholderWorldView world, out cityObject, out PlaceholderMobileCity city);
                FindDeploymentCell(world.Model, false, out int x, out int y);
                cityObject.transform.position = world.CellToWorld(x, y);

                Assert.That(city.TryToggleDeployment(out string reason), Is.False);
                Assert.That(city.Deployment.Mode, Is.EqualTo(CityMode.Mobile));
                Assert.That(reason, Does.StartWith("展开失败"));
                Assert.That(city.LastMobilityMessage, Is.EqualTo(reason));
            }
            finally
            {
                if (cityObject != null) Object.DestroyImmediate(cityObject);
                if (worldObject != null) Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void ValidDeploymentCancelsAutopilotAndStartsDeploying()
        {
            GameObject worldObject = null;
            GameObject cityObject = null;
            try
            {
                CreateRuntime(out worldObject, out PlaceholderWorldView world, out cityObject, out PlaceholderMobileCity city);
                FindDeploymentCell(world.Model, true, out int x, out int y);
                cityObject.transform.position = world.CellToWorld(x, y);
                FindReachableDestination(world.Model, x, y, out int targetX, out int targetY);
                Assert.That(city.TrySetDestinationCell(targetX, targetY, out _), Is.True);

                Assert.That(city.TryToggleDeployment(out string reason), Is.True, reason);

                Assert.That(city.Deployment.Mode, Is.EqualTo(CityMode.Deploying));
                Assert.That(city.AutopilotActive, Is.False);
                Assert.That(city.LastMobilityMessage, Is.EqualTo("开始展开"));
            }
            finally
            {
                if (cityObject != null) Object.DestroyImmediate(cityObject);
                if (worldObject != null) Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void DestinationOutsideWorldOrOutsideMobileModeIsRejected()
        {
            GameObject worldObject = null;
            GameObject cityObject = null;
            try
            {
                CreateRuntime(out worldObject, out PlaceholderWorldView world, out cityObject, out PlaceholderMobileCity city);
                FindReachablePair(world.Model, out int startX, out int startY, out int targetX, out int targetY);
                cityObject.transform.position = world.CellToWorld(startX, startY);

                Assert.That(city.TrySetDestinationCell(-1, 0, out string outsideReason), Is.False);
                Assert.That(outsideReason, Is.EqualTo("自动驾驶失败：目标不可达"));

                city.RestoreDeployment(CityMode.Fortress, 0f);

                Assert.That(city.TrySetDestinationCell(targetX, targetY, out string modeReason), Is.False);
                Assert.That(modeReason, Is.EqualTo("自动驾驶仅在移动态可用"));
            }
            finally
            {
                if (cityObject != null) Object.DestroyImmediate(cityObject);
                if (worldObject != null) Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void RestoreNavigationDisablesUnreachableSavedDestination()
        {
            GameObject worldObject = null;
            GameObject cityObject = null;
            try
            {
                CreateRuntime(out worldObject, out _, out cityObject, out PlaceholderMobileCity city);

                city.RestoreNavigation(true, -1, -1);

                Assert.That(city.AutopilotActive, Is.False);
                Assert.That(city.DestinationX, Is.EqualTo(-1));
                Assert.That(city.DestinationY, Is.EqualTo(-1));
            }
            finally
            {
                if (cityObject != null) Object.DestroyImmediate(cityObject);
                if (worldObject != null) Object.DestroyImmediate(worldObject);
            }
        }

        private static void CreateRuntime(
            out GameObject worldObject,
            out PlaceholderWorldView world,
            out GameObject cityObject,
            out PlaceholderMobileCity city)
        {
            worldObject = new GameObject("NavigationWorld");
            world = worldObject.AddComponent<PlaceholderWorldView>();
            world.Generate(new WorldSeed(8128));
            cityObject = new GameObject("NavigationCity");
            cityObject.AddComponent<Rigidbody2D>();
            city = cityObject.AddComponent<PlaceholderMobileCity>();
            if (city.Deployment == null)
                typeof(PlaceholderMobileCity)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(city, null);
            city.ConfigureWorld(world);
        }

        private static void FindReachablePair(
            WorldMapModel map,
            out int startX,
            out int startY,
            out int targetX,
            out int targetY)
        {
            for (int sx = 0; sx < map.Width; sx++)
                for (int sy = 0; sy < map.Height; sy++)
                    if (CityTerrainRules.IsPassable(map.Get(sx, sy)))
                        for (int tx = 0; tx < map.Width; tx++)
                            for (int ty = 0; ty < map.Height; ty++)
                                if (System.Math.Abs(tx - sx) + System.Math.Abs(ty - sy) >= 2 &&
                                    CityPathfinder.TryFindPath(map, sx, sy, tx, ty, out _))
                                {
                                    startX = sx;
                                    startY = sy;
                                    targetX = tx;
                                    targetY = ty;
                                    return;
                                }
            throw new AssertionException("Generated map has no reachable pair.");
        }

        private static void FindReachableDestination(
            WorldMapModel map,
            int startX,
            int startY,
            out int targetX,
            out int targetY)
        {
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if ((x != startX || y != startY) &&
                        CityPathfinder.TryFindPath(map, startX, startY, x, y, out _))
                    {
                        targetX = x;
                        targetY = y;
                        return;
                    }
            throw new AssertionException("Generated map has no destination.");
        }

        private static void FindDeploymentCell(
            WorldMapModel map,
            bool valid,
            out int selectedX,
            out int selectedY)
        {
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if ((CityDeploymentRules.Validate(map, x, y) == CityDeploymentFailure.None) == valid)
                    {
                        selectedX = x;
                        selectedY = y;
                        return;
                    }
            throw new AssertionException(valid
                ? "Generated map has no valid deployment cell."
                : "Generated map has no invalid deployment cell.");
        }
    }
}
