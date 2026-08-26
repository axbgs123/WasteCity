using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxVoidDebtIntegrationTests
    {
        private const string ControllerTypeName =
            "WasteCity.Graybox3D.Building.GrayboxVoidDebtController3D";

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        [Test]
        public void IDEA0020_OnlySelectedVoidDebtConstructionCanBorrow()
        {
            GrayboxBuildingSession3D session = CreateSession();
            session.Inventory.Set(ResourceIds.Stone, 1);
            var fate = new FormalFateRuntime();
            var debt = new FormalVoidDebtRuntime();
            object controller = CreateController(fate, debt);
            Bind(controller, session.CityStorage, () => false);
            ConfigureSession(session, controller);
            var presentation = new RecordingPresentation();

            Assert.That(session.TryBeginConstruction(
                ValidRequest(session, BuildingCatalog.Wall, 10, 10),
                presentation,
                out _,
                out BuildingPlacementEvaluation pending), Is.False);
            Assert.That(pending.Failures,
                Does.Contain(BuildingPlacementFailure.InsufficientMaterials));
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(1));
            Assert.That(debt.TotalDebt, Is.Zero);

            Assert.That(fate.TrySelect(
                FormalFateCatalog.VoidDebtId,
                out _,
                out _,
                out string fateError), Is.True, fateError);
            Assert.That(session.TryBeginConstruction(
                ValidRequest(session, BuildingCatalog.Wall, 10, 10),
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation selected), Is.True,
                selected.PrimaryFailure.ToString());
            Assert.That(instance, Is.Not.Null);
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.Zero);
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(1));

            Assert.That(session.CityStorage.TrySpendFromNetwork(
                ResourceIds.Stone,
                1), Is.False,
                "Production, research, crafting and transfers retain the " +
                "ordinary non-borrowing storage command.");
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(1));
        }

        [Test]
        public void IDEA0020_PresentationFailureRollsBackCashDebtAndGrid()
        {
            GrayboxBuildingSession3D session = CreateSession();
            session.Inventory.Set(ResourceIds.Stone, 1);
            FormalVoidDebtRuntime debt;
            object controller = SelectedController(session, out debt);
            var presentation = new RecordingPresentation
            {
                CreateResult = false,
            };

            Assert.That(session.TryBeginConstruction(
                ValidRequest(session, BuildingCatalog.Wall, 10, 10),
                presentation,
                out _,
                out BuildingPlacementEvaluation evaluation), Is.False);
            Assert.That(evaluation.IsValid, Is.True,
                "Payment was affordable through the selected fate.");
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(1));
            Assert.That(debt.TotalDebt, Is.Zero);
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);

            ((IDisposable)controller).Dispose();
        }

        [Test]
        public void IDEA0020_AllNetworkIncomeRepaysMatchingDebtBeforeStorage()
        {
            GrayboxBuildingSession3D session = CreateSession();
            session.Inventory.Set(ResourceIds.Stone, 0);
            FormalVoidDebtRuntime debt;
            object controller = SelectedController(session, out debt);
            var presentation = new RecordingPresentation();
            Assert.That(session.TryBeginConstruction(
                ValidRequest(session, BuildingCatalog.Wall, 10, 10),
                presentation,
                out _,
                out _), Is.True);
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(2));

            var observed = new List<int>();
            session.CityStorage.AttributedChanged +=
                (resourceId, delta, attribution) =>
                {
                    if (resourceId == ResourceIds.Stone) observed.Add(delta);
                };
            Assert.That(session.CityStorage.AddToNetwork(
                ResourceIds.Stone,
                1), Is.EqualTo(1));
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(1));
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.Zero);
            Assert.That(observed, Is.Empty,
                "Observers see only the residual stored income, not a " +
                "transient positive followed by repayment.");

            Assert.That(session.CityStorage.AddToNetwork(
                ResourceIds.Stone,
                2), Is.EqualTo(2));
            Assert.That(debt.TotalDebt, Is.Zero);
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(1));
            Assert.That(observed, Is.EqualTo(new[] { 1 }));

            Assert.That(debt.TryBorrowConstruction(
                ResourceIds.Iron,
                3,
                out _), Is.True);
            Assert.That(session.CityStorage.AddToNetwork(
                ResourceIds.Stone,
                2), Is.EqualTo(2));
            Assert.That(debt.GetDebt(ResourceIds.Iron), Is.EqualTo(3),
                "Income cannot repay debt of another resource.");
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(3));

            ((IDisposable)controller).Dispose();
        }

        [Test]
        public void IDEA0020_CancellationRefundRepaysConstructionDebtFirst()
        {
            GrayboxBuildingSession3D session = CreateSession();
            session.Inventory.Set(ResourceIds.Stone, 0);
            FormalVoidDebtRuntime debt;
            object controller = SelectedController(session, out debt);
            var presentation = new RecordingPresentation();
            Assert.That(session.TryBeginConstruction(
                ValidRequest(session, BuildingCatalog.Wall, 10, 10),
                presentation,
                out GrayboxBuildingInstance3D instance,
                out _), Is.True);
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(2));

            Assert.That(session.TryCancelConstruction(
                instance.StableInstanceId,
                handlingRatio: 1d,
                presentation,
                out int acceptedRefund), Is.True);
            Assert.That(acceptedRefund, Is.EqualTo(2));
            Assert.That(debt.TotalDebt, Is.Zero);
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.Zero,
                "The refund clears its matching construction debt before " +
                "becoming spendable storage.");

            ((IDisposable)controller).Dispose();
        }

        [Test]
        public void IDEA0020_PersistenceSuppressionRepeatedBindAndDisposeAreSafe()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.VoidDebtId,
                out _,
                out _,
                out _), Is.True);
            var debt = new FormalVoidDebtRuntime();
            Assert.That(debt.TryBorrowConstruction(
                ResourceIds.Stone,
                5,
                out _), Is.True);
            object controller = CreateController(fate, debt);
            bool suppressed = true;
            Bind(controller, session.CityStorage, () => suppressed);
            Bind(controller, session.CityStorage, () => suppressed);

            Assert.That(session.CityStorage.AddToNetwork(
                ResourceIds.Stone,
                2), Is.EqualTo(2));
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(5));
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(32));

            suppressed = false;
            Assert.That(session.CityStorage.AddToNetwork(
                ResourceIds.Stone,
                2), Is.EqualTo(2));
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(3),
                "Repeated Bind cannot install duplicate credit hooks.");
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(32));

            ((IDisposable)controller).Dispose();
            ((IDisposable)controller).Dispose();
            Assert.That(session.CityStorage.AddToNetwork(
                ResourceIds.Stone,
                2), Is.EqualTo(2));
            Assert.That(debt.GetDebt(ResourceIds.Stone), Is.EqualTo(3));
            Assert.That(session.GetCityResourceAmount(ResourceIds.Stone),
                Is.EqualTo(34));
        }

        private object SelectedController(
            GrayboxBuildingSession3D session,
            out FormalVoidDebtRuntime debt)
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.VoidDebtId,
                out _,
                out _,
                out string error), Is.True, error);
            debt = new FormalVoidDebtRuntime();
            object controller = CreateController(fate, debt);
            Bind(controller, session.CityStorage, () => false);
            ConfigureSession(session, controller);
            return controller;
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var owner = new GameObject("VoidDebt.Integration.Session");
            created.Add(owner);
            GrayboxBuildingSession3D session =
                owner.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static object CreateController(
            FormalFateRuntime fate,
            FormalVoidDebtRuntime debt)
        {
            Type type = RequireControllerType();
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(FormalFateRuntime),
                typeof(FormalVoidDebtRuntime),
            });
            Assert.That(constructor, Is.Not.Null);
            object value = constructor.Invoke(new object[] { fate, debt });
            Assert.That(value, Is.InstanceOf<IDisposable>());
            return value;
        }

        private static void Bind(
            object controller,
            CityResourceStorageModel storage,
            Func<bool> suppression)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Bind",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(CityResourceStorageModel),
                    typeof(Func<bool>),
                },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { storage, suppression });
        }

        private static void ConfigureSession(
            GrayboxBuildingSession3D session,
            object controller)
        {
            MethodInfo method = typeof(GrayboxBuildingSession3D).GetMethod(
                "ConfigureConstructionPaymentPolicy",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetParameters(), Has.Length.EqualTo(1));
            method.Invoke(session, new[] { controller });
        }

        private static BuildingPlacementRequest ValidRequest(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            int x,
            int y)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            return new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                12,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                coversCompatibleResourceNode: true,
                compatibleResourceNodeId: "test.resource-node",
                contentVisible: true,
                unlock,
                canAfford: false);
        }

        private static Type RequireControllerType()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    ControllerTypeName,
                    false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, ControllerTypeName);
            return type;
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool CreateResult { get; set; } = true;

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return CreateResult;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
