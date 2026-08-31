using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class IDEA0024CityBuildingPresentationTests
    {
        private const string ProfilePath =
            "Presentation/FormalWorldPresentationScaleProfile3D";
        private const string CoordinatePolicyTypeName =
            "WasteCity.Graybox3D.FormalInnerCityPresentationPolicy3D, " +
            "WasteCity.Graybox3D";
        private const string ScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";

        private GameObject root;
        private Material material;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            for (var index = 0; index < cameras.Length; index++)
                if (cameras[index].name == "IDEA0024.Camera")
                    UnityEngine.Object.DestroyImmediate(cameras[index].gameObject);
        }

        [Test]
        public void ProfileOwnsOneToOneInnerGridAndFlatCityMetrics()
        {
            FormalWorldPresentationScaleProfile3D profile = LoadProfile();
            Assert.That(FormalWorldPresentationScaleProfile3D.GroundCellSize,
                Is.EqualTo(1f));
            Assert.That(FormalWorldPresentationScaleProfile3D.InnerCellSize,
                Is.EqualTo(1f));
            Assert.That(Read<int>(profile, "InnerGridWidth"), Is.EqualTo(8));
            Assert.That(Read<int>(profile, "InnerGridHeight"), Is.EqualTo(6));
            Assert.That(Read<Vector2>(profile, "InnerGridAnchor"),
                Is.EqualTo(new Vector2(-4f, -3f)));
            Assert.That(Read<Vector2>(profile, "InnerPlatformSize"),
                Is.EqualTo(new Vector2(8f, 6f)));
            Assert.That(profile.InnerVerticalEmphasis, Is.EqualTo(1f));

            Vector3 mobile = Read<Vector3>(profile, "MobileCityVisualSize");
            Vector3 fortress = Read<Vector3>(profile, "FortressCityVisualSize");
            Assert.That(mobile,
                Is.EqualTo(new Vector3(8.6f, .65f, 6.6f)));
            Assert.That(fortress.x, Is.InRange(8.6f, 9f));
            Assert.That(fortress.y, Is.InRange(.65f, .9f));
            Assert.That(fortress.z, Is.InRange(6.6f, 7f));
            Assert.That(Read<Vector3>(profile, "MobileGameplayColliderSize"),
                Is.EqualTo(new Vector3(3f, 1f, 2f)));
            Assert.That(Read<Vector3>(profile, "FortressGameplayColliderSize"),
                Is.EqualTo(new Vector3(3f, 1.5f, 3f)));
            Assert.That(mobile,
                Is.Not.EqualTo(Read<Vector3>(profile,
                    "MobileGameplayColliderSize")));
            Assert.That(Read<float>(profile, "MobileDeckLocalY"),
                Is.EqualTo(mobile.y - .5f).Within(.0001f));
            Assert.That(Read<float>(profile, "FortressDeckLocalY"),
                Is.EqualTo(fortress.y - .5f).Within(.0001f));
            Assert.That(Read<float>(profile, "BuildingIconRoofClearance"),
                Is.GreaterThan(0f));
        }

        [Test]
        public void PureInnerCoordinatePolicyCoversCornersAndRotatedRoundTrip()
        {
            Type policy = Type.GetType(CoordinatePolicyTypeName, false);
            Assert.That(policy, Is.Not.Null, CoordinatePolicyTypeName);
            MethodInfo localCenter = RequireStatic(
                policy,
                "CellCenterLocal",
                typeof(Vector3),
                typeof(int), typeof(int), typeof(float));
            Assert.That(localCenter.Invoke(null, new object[] { 0, 0, .2f }),
                Is.EqualTo(new Vector3(-3.5f, .2f, -2.5f)));
            Assert.That(localCenter.Invoke(null, new object[] { 7, 5, .2f }),
                Is.EqualTo(new Vector3(3.5f, .2f, 2.5f)));

            MethodInfo tryProject = RequireStatic(
                policy,
                "TryProjectWorldPoint",
                typeof(bool),
                typeof(Vector3), typeof(Quaternion), typeof(Vector3),
                typeof(float), typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(), typeof(Vector3).MakeByRefType());
            Vector3 city = new Vector3(12f, .5f, -8f);
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 expectedLocal = new Vector3(3.5f, .2f, 2.5f);
            Vector3 point = city + rotation * expectedLocal;
            object[] args = { city, rotation, point, .2f, 0, 0, default(Vector3) };
            Assert.That(tryProject.Invoke(null, args), Is.EqualTo(true));
            Assert.That(args[4], Is.EqualTo(7));
            Assert.That(args[5], Is.EqualTo(5));
            Assert.That((Vector3)args[6], Is.EqualTo(point));

            Vector3 outside = city + rotation * new Vector3(4.01f, .2f, 0f);
            args = new object[]
                { city, rotation, outside, .2f, 0, 0, default(Vector3) };
            Assert.That(tryProject.Invoke(null, args), Is.EqualTo(false));
        }

        [Test]
        public void SceneKeepsStableCityPlatformAndVisualObjectIdentities()
        {
            string yaml = File.ReadAllText(Absolute(ScenePath));
            StringAssert.Contains("--- !u!1 &505702828\nGameObject:", yaml);
            StringAssert.Contains("  m_Name: MobileCity\n", yaml);
            StringAssert.Contains("--- !u!1 &39003984\nGameObject:", yaml);
            StringAssert.Contains("  m_Name: MobileCityVisual\n", yaml);
            StringAssert.Contains("--- !u!1 &406215438\nGameObject:", yaml);
            StringAssert.Contains("  m_Name: InnerCityPlatform\n", yaml);
            string authoring = File.ReadAllText(Absolute(
                "Assets/_Game/Editor/GrayboxSceneAuthoring.cs"));
            StringAssert.Contains(
                "FindDirectChild(city, \"InnerCityPlatform\")",
                authoring);
        }

        [Test]
        public void InnerAndGroundBuildingsUseEqualWorldCellScale()
        {
            Fixture fixture = CreateFixture();
            GrayboxBuildingInstance3D ground = NewInstance(
                "idea0024.test.ground", BuildingCatalog.Housing,
                BuildingSite.Ground, 20, 15, complete: true);
            GrayboxBuildingInstance3D inner = NewInstance(
                "idea0024.test.inner", BuildingCatalog.Housing,
                BuildingSite.InnerCity, 0, 0, complete: true);
            Assert.That(fixture.View.TryCreate(ground), Is.True);
            Assert.That(fixture.View.TryCreate(inner), Is.True);
            Bounds groundBounds = MeshBounds(fixture, ground.StableInstanceId);
            Bounds innerBounds = MeshBounds(fixture, inner.StableInstanceId);
            Assert.That(innerBounds.size.x,
                Is.EqualTo(groundBounds.size.x).Within(.02f));
            Assert.That(innerBounds.size.y,
                Is.EqualTo(groundBounds.size.y).Within(.02f));
            Assert.That(innerBounds.size.z,
                Is.EqualTo(groundBounds.size.z).Within(.02f));
        }

        [Test]
        public void EveryCompletedBuildingHasVisibleRoofClearedVerticalSprite()
        {
            Fixture fixture = CreateFixture();
            CreateCamera();
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition definition = BuildingCatalog.All[index];
                GrayboxBuildingInstance3D instance = NewInstance(
                    "idea0024.sprite." + index,
                    definition,
                    BuildingSite.Ground,
                    index % 10 + 10,
                    index / 10 + 10,
                    complete: true);
                Assert.That(fixture.View.TryCreate(instance), Is.True,
                    definition.Id.Value);
                Transform visual = fixture.InstanceRoot.Find(
                    instance.StableInstanceId);
                SpriteRenderer icon = visual.GetComponentInChildren<
                    SpriteRenderer>(true);
                MeshRenderer mesh = visual.GetComponent<MeshRenderer>();
                Assert.That(icon.sprite, Is.Not.Null, definition.Id.Value);
                Assert.That(icon.enabled, Is.True, definition.Id.Value);
                InvokeLateUpdate(fixture.View);
                Rect visibleBounds = Production2DVisualCatalog3D
                    .ResolveVisibleBounds(
                        Production2DVisualClass.Building,
                        definition.Id.Value);
                float visibleBottom = icon.transform.position.y +
                    Production2DVisualScalePolicy3D
                        .ResolveVisibleBottomLocal(
                            icon.sprite,
                            visibleBounds,
                            icon.transform.lossyScale.y);
                Assert.That(visibleBottom,
                    Is.GreaterThan(mesh.bounds.max.y), definition.Id.Value);
                Assert.That(Vector3.Dot(icon.transform.up, Vector3.up),
                    Is.GreaterThan(.999f), definition.Id.Value);
                Camera activeCamera = Camera.main;
                Assert.That(activeCamera, Is.Not.Null);
                Vector3 horizontal = activeCamera.transform.position -
                    icon.transform.position;
                horizontal.y = 0f;
                horizontal.Normalize();
                Assert.That(Vector3.Dot(icon.transform.forward, horizontal),
                    Is.GreaterThan(.999f), definition.Id.Value);
            }
        }

        [Test]
        public void ConstructionAndRuinHideSpriteAndRepeatedUpdatesDoNotGrow()
        {
            Fixture fixture = CreateFixture();
            GrayboxBuildingInstance3D instance = NewInstance(
                "idea0024.test.lifecycle", BuildingCatalog.Warehouse,
                BuildingSite.Ground, 20, 15, complete: false);
            fixture.View.TryCreate(instance);
            Transform rootVisual = fixture.InstanceRoot.Find(
                instance.StableInstanceId);
            SpriteRenderer icon = rootVisual.GetComponentInChildren<
                SpriteRenderer>(true);
            Assert.That(icon.enabled, Is.False);
            InvokeTransition(instance, "Complete");
            fixture.View.UpdateInstance(instance);
            Assert.That(icon.enabled, Is.True);
            int childCount = rootVisual.childCount;
            for (var index = 0; index < 300; index++)
                fixture.View.UpdateInstance(instance);
            Assert.That(rootVisual.childCount, Is.EqualTo(childCount));
            Assert.That(rootVisual.GetComponentInChildren<SpriteRenderer>(true),
                Is.SameAs(icon));
            InvokeTransition(instance, "DestroyForCombat");
            fixture.View.UpdateInstance(instance);
            Assert.That(icon.enabled, Is.False);
        }

        private Fixture CreateFixture()
        {
            root = new GameObject("IDEA0024.Root");
            Transform instances = NewChild(root.transform, "Instances");
            Transform infrastructure = NewChild(root.transform,
                "Infrastructure");
            GameObject cityObject = new GameObject("City");
            cityObject.transform.SetParent(root.transform, false);
            GrayboxMobileCityController3D city = cityObject.AddComponent<
                GrayboxMobileCityController3D>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            GrayboxBuildingWorldView3D view = root.AddComponent<
                GrayboxBuildingWorldView3D>();
            view.Configure(instances, infrastructure, material, material, city);
            return new Fixture(view, instances);
        }

        private static GrayboxBuildingInstance3D NewInstance(
            string id,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            bool complete)
        {
            var instance = (GrayboxBuildingInstance3D)Activator.CreateInstance(
                typeof(GrayboxBuildingInstance3D),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    id,
                    new PlacedBuilding(definition, x, y, site),
                    new ConstructionProgress(definition.BuildSeconds),
                    default(ResourceNodeBinding),
                },
                null);
            if (complete) InvokeTransition(instance, "Complete");
            return instance;
        }

        private static void InvokeTransition(
            GrayboxBuildingInstance3D instance,
            string name)
        {
            MethodInfo method = typeof(GrayboxBuildingInstance3D).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(instance, null);
        }

        private static void InvokeLateUpdate(GrayboxBuildingWorldView3D view)
        {
            MethodInfo method = typeof(GrayboxBuildingWorldView3D).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(view, null);
        }

        private static Bounds MeshBounds(Fixture fixture, string id)
        {
            return fixture.InstanceRoot.Find(id)
                .GetComponent<MeshRenderer>().bounds;
        }

        private Camera CreateCamera()
        {
            var cameraObject = new GameObject("IDEA0024.Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(18f, 20f, -16f);
            camera.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            return camera;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static FormalWorldPresentationScaleProfile3D LoadProfile()
        {
            FormalWorldPresentationScaleProfile3D profile = Resources.Load<
                FormalWorldPresentationScaleProfile3D>(ProfilePath);
            Assert.That(profile, Is.Not.Null, ProfilePath);
            return profile;
        }

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                owner.GetType().Name + "." + name);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(T)), name);
            return (T)property.GetValue(owner);
        }

        private static MethodInfo RequireStatic(
            Type owner,
            string name,
            Type returnType,
            params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                null,
                parameters,
                null);
            Assert.That(method, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType), name);
            return method;
        }

        private static string Absolute(string projectPath)
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                projectPath);
        }

        private sealed class Fixture
        {
            public Fixture(
                GrayboxBuildingWorldView3D view,
                Transform instanceRoot)
            {
                View = view;
                InstanceRoot = instanceRoot;
            }

            public GrayboxBuildingWorldView3D View { get; }
            public Transform InstanceRoot { get; }
        }
    }
}
