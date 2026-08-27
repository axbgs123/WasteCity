using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class CivilizationResearchOperationsTests
    {
        [Test]
        public void IDEA0021_OperationsStatusAndStartFollowCivilizationRevision()
        {
            var root = new GameObject("Civilization.Research.Operations");
            root.SetActive(false);
            try
            {
                var session = root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureFormalSession();
                Assert.That(session.TryRestoreBuildings(
                    new[]
                    {
                        new GrayboxBuildingRestoreEntry3D(
                            "building.instance.000001",
                            BuildingCatalog.ResearchStation,
                            BuildingSite.InnerCity,
                            0,
                            0,
                            BuildingOrientation.North,
                            GrayboxBuildingInstanceState.Completed,
                            0f,
                            true,
                            false,
                            default),
                    }, 2, new PassivePresentation(), out string error),
                    Is.True, error);
                session.UnlockResearchForDevelopment(
                    "core.research.precision-assembly");
                ResearchDefinition alloy = ResearchCatalog.Find(
                    CivilizationResearchAvailability.AlloyArmorId);
                foreach (ResourceAmount cost in alloy.Costs)
                {
                    session.Inventory.Set(cost.ResourceId, 0);
                    Assert.That(session.CityStorage.AddToNetwork(
                        cost.ResourceId, cost.Amount), Is.EqualTo(cost.Amount));
                }
                var controller = root.AddComponent<
                    GrayboxOperationsController3D>();
                SetPrivate(controller, "session", session);
                var level = 1;
                ulong revision = 0;
                controller.ConfigureCivilizationResearch(
                    () => level,
                    () => revision);
                SetPrivate(controller, "selectedResearchId", alloy.Id.Value);

                Assert.That(ResearchStatus(controller, alloy),
                    Is.EqualTo(CivilizationResearchAvailability.LockedReason));
                Assert.That(CanStart(controller), Is.False);

                level = 2;
                revision = 1;
                Assert.That(ResearchStatus(controller, alloy),
                    Is.EqualTo("可研究"));
                Assert.That(CanStart(controller), Is.True);
                FormalResearchRuntime runtime =
                    (FormalResearchRuntime)ReadPrivate(controller, "research");
                Assert.That(runtime.ResolveForDisplay(alloy).EffectSummary,
                    Does.Contain("重型机枪塔").And.Contain("30%"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string ResearchStatus(
            GrayboxOperationsController3D controller,
            ResearchDefinition definition)
        {
            return (string)InvokePrivate(
                controller,
                "ResearchStatus",
                definition,
                null);
        }

        private static bool CanStart(GrayboxOperationsController3D controller)
        {
            return (bool)InvokePrivate(controller, "CanStartSelectedResearch");
        }

        private static object InvokePrivate(
            object owner,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(owner, arguments);
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

        private static object ReadPrivate(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(owner);
        }

        private sealed class PassivePresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
