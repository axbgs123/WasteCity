using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingUpgradeControllerTests
    {
        [Test]
        public void IDEA0020_HeavyMachineGunUpgradePreservesStableCompletedSite()
        {
            using (Fixture fixture = Create(
                BuildingCatalog.MachineGunTurret,
                ResourceIds.Alloy,
                40,
                "core.research.alloy-armor"))
            {
                int completionEvents = 0;
                fixture.Session.BuildingCompleted += _ => completionEvents++;
                GrayboxBuildingInstance3D before = fixture.Instance;
                PlacedBuilding placement = before.Placement;
                int resourcesBefore = fixture.Session.CityStorage
                    .GetNetworkAmount(ResourceIds.Alloy);

                GrayboxBuildingUpgradeResult3D result =
                    fixture.Controller.TryUpgrade(before.StableInstanceId);

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.TargetBuildingId,
                    Is.EqualTo(BuildingCatalog.HeavyMachineGunTurret.Id.Value));
                Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
                Assert.That(fixture.Session.Instances[0], Is.SameAs(before));
                Assert.That(before.StableInstanceId,
                    Is.EqualTo("building.instance.000001"));
                Assert.That(before.State,
                    Is.EqualTo(GrayboxBuildingInstanceState.Completed));
                Assert.That(before.Placement.Definition,
                    Is.SameAs(BuildingCatalog.HeavyMachineGunTurret));
                Assert.That(before.Placement.X, Is.EqualTo(placement.X));
                Assert.That(before.Placement.Y, Is.EqualTo(placement.Y));
                Assert.That(before.Placement.Site, Is.EqualTo(placement.Site));
                Assert.That(before.Placement.Orientation,
                    Is.EqualTo(placement.Orientation));
                Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                    ResourceIds.Alloy), Is.EqualTo(resourcesBefore - 20));
                Assert.That(completionEvents, Is.Zero,
                    "Upgrade is not a second construction completion.");
                Assert.That(fixture.Presentation.ActiveDefinitionId,
                    Is.EqualTo(BuildingCatalog.HeavyMachineGunTurret.Id.Value));
            }
        }

        [Test]
        public void IDEA0020_SwordArrayUpgradeUsesSpiritIronAndResearchGate()
        {
            using (Fixture fixture = Create(
                BuildingCatalog.SwordArrayTower,
                ResourceIds.SpiritIron,
                30,
                "core.research.sword-riding"))
            {
                GrayboxBuildingUpgradeResult3D result =
                    fixture.Controller.TryUpgrade(
                        fixture.Instance.StableInstanceId);
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(fixture.Instance.Placement.Definition,
                    Is.SameAs(BuildingCatalog.SwordRidingPlatform));
                Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                    ResourceIds.SpiritIron), Is.EqualTo(10));
            }
        }

        [TestCase(1, true, 40)]
        [TestCase(2, false, 40)]
        [TestCase(2, true, 0)]
        public void IDEA0020_LockedOrUnfundedUpgradeIsZeroWrite(
            int civilizationLevel,
            bool researchCompleted,
            int alloy)
        {
            using (Fixture fixture = Create(
                BuildingCatalog.MachineGunTurret,
                ResourceIds.Alloy,
                alloy,
                researchCompleted ? "core.research.alloy-armor" : null,
                civilizationLevel))
            {
                GrayboxBuildingInstance3D instance = fixture.Instance;
                PlacedBuilding placement = instance.Placement;
                int resourcesBefore = fixture.Session.CityStorage
                    .GetNetworkAmount(ResourceIds.Alloy);
                uint catalogBefore = fixture.Session.CatalogRevision;
                uint placementBefore = fixture.Session.PlacementRevision;

                GrayboxBuildingUpgradeResult3D result =
                    fixture.Controller.TryUpgrade(instance.StableInstanceId);

                Assert.That(result.Success, Is.False);
                Assert.That(instance.Placement, Is.SameAs(placement));
                Assert.That(instance.Placement.Definition,
                    Is.SameAs(BuildingCatalog.MachineGunTurret));
                Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                    ResourceIds.Alloy), Is.EqualTo(resourcesBefore));
                Assert.That(fixture.Session.CatalogRevision,
                    Is.EqualTo(catalogBefore));
                Assert.That(fixture.Session.PlacementRevision,
                    Is.EqualTo(placementBefore));
            }
        }

        [Test]
        public void IDEA0020_PresentationFailureRollsBackRulesAndPayment()
        {
            using (Fixture fixture = Create(
                BuildingCatalog.MachineGunTurret,
                ResourceIds.Alloy,
                40,
                "core.research.alloy-armor"))
            {
                GrayboxBuildingInstance3D instance = fixture.Instance;
                int resourcesBefore = fixture.Session.CityStorage
                    .GetNetworkAmount(ResourceIds.Alloy);
                fixture.Presentation.FailTargetId =
                    BuildingCatalog.HeavyMachineGunTurret.Id.Value;

                GrayboxBuildingUpgradeResult3D result =
                    fixture.Controller.TryUpgrade(instance.StableInstanceId);

                Assert.That(result.Code,
                    Is.EqualTo(GrayboxBuildingUpgradeCode3D.PresentationFailed));
                Assert.That(instance.Placement.Definition,
                    Is.SameAs(BuildingCatalog.MachineGunTurret));
                Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                    ResourceIds.Alloy), Is.EqualTo(resourcesBefore));
                Assert.That(fixture.Presentation.ActiveDefinitionId,
                    Is.EqualTo(BuildingCatalog.MachineGunTurret.Id.Value));
            }
        }

        [Test]
        public void IDEA0020_AvailabilityExplainsLevelResearchAndMaterials()
        {
            using (Fixture level = Create(
                BuildingCatalog.MachineGunTurret,
                ResourceIds.Alloy,
                40,
                "core.research.alloy-armor",
                civilizationLevel: 1))
            {
                GrayboxBuildingUpgradeAvailability3D projection =
                    level.Controller.CaptureAvailability(
                        level.Instance.StableInstanceId);
                Assert.That(projection.IsVisible, Is.True);
                Assert.That(projection.CanUpgrade, Is.False);
                Assert.That(projection.Feedback, Does.Contain("文明 Lv.2"));
            }
            using (Fixture research = Create(
                BuildingCatalog.MachineGunTurret,
                ResourceIds.Alloy,
                40,
                researchId: null))
            {
                GrayboxBuildingUpgradeAvailability3D projection =
                    research.Controller.CaptureAvailability(
                        research.Instance.StableInstanceId);
                Assert.That(projection.CanUpgrade, Is.False);
                Assert.That(projection.Feedback, Does.Contain("合金装甲"));
            }
            using (Fixture materials = Create(
                BuildingCatalog.MachineGunTurret,
                ResourceIds.Alloy,
                0,
                "core.research.alloy-armor"))
            {
                GrayboxBuildingUpgradeAvailability3D projection =
                    materials.Controller.CaptureAvailability(
                        materials.Instance.StableInstanceId);
                Assert.That(projection.CanUpgrade, Is.False);
                Assert.That(projection.Feedback,
                    Does.Contain("材料不足").And.Contain("20"));
            }
            using (Fixture ready = Create(
                BuildingCatalog.MachineGunTurret,
                ResourceIds.Alloy,
                40,
                "core.research.alloy-armor"))
            {
                GrayboxBuildingUpgradeAvailability3D projection =
                    ready.Controller.CaptureAvailability(
                        ready.Instance.StableInstanceId);
                Assert.That(projection.CanUpgrade, Is.True);
                Assert.That(projection.ButtonLabel, Does.Contain("重型机枪塔"));
            }
        }

        [Test]
        public void IDEA0020_HostUpgradeProjectionCachesStableFramesAtZeroBytes()
        {
            var root = new GameObject("Building.Upgrade.HostCache");
            root.SetActive(false);
            try
            {
                var session = root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureFormalSession();
                var defense = root.AddComponent<GrayboxDefenseController3D>();
                var hud = root.AddComponent<GrayboxDefenseHud3D>();
                var host = root.AddComponent<GrayboxFormalSaveRuntimeHost3D>();
                var presentation = new FakePresentation();
                var controller = new GrayboxBuildingUpgradeController3D(
                    session, () => 1, presentation);
                SetPrivate(defense, "hud", hud);
                SetPrivate(host, "session", session);
                SetPrivate(host, "defense", defense);
                SetPrivate(host, "buildingUpgradeController", controller);
                SetPrivate(host, "<IsInitialized>k__BackingField", true);
                MethodInfo method = typeof(GrayboxFormalSaveRuntimeHost3D)
                    .GetMethod(
                        "RefreshBuildingUpgradeCommand",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var refresh = (Func<bool>)method.CreateDelegate(
                    typeof(Func<bool>), host);
                Assert.That(refresh(), Is.True);
                Assert.That(refresh(), Is.False);

                long before = GC.GetAllocatedBytesForCurrentThread();
                bool changed = false;
                for (var index = 0; index < 300; index++)
                    changed |= refresh();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(changed, Is.False);
                Assert.That(allocated, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivate(object owner, string fieldName,
            object value)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(owner, value);
        }

        private static Fixture Create(
            BuildingDefinition definition,
            string resourceId,
            int amount,
            string researchId,
            int civilizationLevel = 2)
        {
            var root = new GameObject("Building.Upgrade.Test");
            root.SetActive(false);
            var session = root.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureFormalSession();
            var presentation = new FakePresentation();
            Assert.That(session.TryRestoreBuildings(
                new[]
                {
                    new GrayboxBuildingRestoreEntry3D(
                        "building.instance.000001",
                        definition,
                        BuildingSite.Ground,
                        10,
                        10,
                        BuildingOrientation.East,
                        GrayboxBuildingInstanceState.Completed,
                        0f,
                        true,
                        false,
                        default),
                },
                2,
                presentation,
                out string error), Is.True, error);
            session.Inventory.Set(resourceId, 0);
            if (amount > 0)
                Assert.That(session.CityStorage.AddToNetwork(resourceId, amount),
                    Is.EqualTo(amount));
            if (!string.IsNullOrEmpty(researchId))
                session.UnlockResearchForDevelopment(researchId);
            var controller = new GrayboxBuildingUpgradeController3D(
                session,
                () => civilizationLevel,
                presentation);
            return new Fixture(root, session, presentation, controller);
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture(
                GameObject root,
                GrayboxBuildingSession3D session,
                FakePresentation presentation,
                GrayboxBuildingUpgradeController3D controller)
            {
                Root = root;
                Session = session;
                Presentation = presentation;
                Controller = controller;
            }

            public GameObject Root { get; }
            public GrayboxBuildingSession3D Session { get; }
            public FakePresentation Presentation { get; }
            public GrayboxBuildingUpgradeController3D Controller { get; }
            public GrayboxBuildingInstance3D Instance => Session.Instances[0];
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class FakePresentation : IGrayboxBuildingPresentation3D
        {
            private readonly Dictionary<string, string> active =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public string FailTargetId { get; set; }
            public string ActiveDefinitionId => active.Count == 0
                ? string.Empty
                : active["building.instance.000001"];

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                string definitionId = instance?.Placement?.Definition?.Id.Value;
                if (instance == null || string.Equals(
                        definitionId,
                        FailTargetId,
                        StringComparison.Ordinal)) return false;
                active[instance.StableInstanceId] = definitionId;
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                if (instance != null)
                    active[instance.StableInstanceId] =
                        instance.Placement.Definition.Id.Value;
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                if (instance != null) active.Remove(instance.StableInstanceId);
            }
        }
    }
}
