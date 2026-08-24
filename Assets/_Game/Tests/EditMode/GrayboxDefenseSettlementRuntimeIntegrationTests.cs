using System.IO;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using WasteCity.Defense;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseSettlementRuntimeIntegrationTests
    {
        private Keyboard keyboard;

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            foreach (GameObject value in
                     Object.FindObjectsOfType<GameObject>())
            {
                if (value != null && value.name.StartsWith(
                        "Task9.Settlement.",
                        System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(value);
                }
            }
        }

        [Test]
        public void IDEA0017_DefenseOwnsOneTerminalSettlementPresentation()
        {
            string source = Read(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxDefenseController3D.cs");

            StringAssert.Contains(
                "SingleCityDefenseSettlementModel", source);
            StringAssert.Contains("TerminalCommitted +=", source);
            StringAssert.Contains("TryPresentTerminalSettlement", source);
            Assert.That(
                typeof(GrayboxDefenseController3D).GetProperty(
                    "IsSettlementOpen",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GrayboxDefenseController3D).GetMethod(
                    "ConfigureSettlementCommands",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
        }

        [Test]
        public void IDEA0017_InputCoordinatorRoutesSettlementAndBlocksWorldFirst()
        {
            string source = Read(
                "Assets/_Game/Scripts/Graybox3D/Usability/" +
                "GrayboxUsabilityInputCoordinator3D.cs");
            int settlementGate = source.IndexOf(
                "defense.IsSettlementOpen",
                System.StringComparison.Ordinal);
            int buildingLoop = source.IndexOf(
                "buildingInput.ProcessCurrentInput()",
                System.StringComparison.Ordinal);

            Assert.That(
                typeof(IGrayboxDefenseSettlementCommands3D)
                    .IsAssignableFrom(
                        typeof(GrayboxUsabilityInputCoordinator3D)),
                Is.True);
            Assert.That(settlementGate, Is.GreaterThanOrEqualTo(0));
            Assert.That(buildingLoop, Is.GreaterThan(settlementGate),
                "结算输入门必须先于建造与世界真实输入主循环。 ");
            StringAssert.Contains("systemMenu.Open()", source);
            StringAssert.Contains(
                "SingleCityDefenseSettlementAction.RetryWaveCheckpoint",
                source);
        }

        [Test]
        public void IDEA0017_FormalEntryExposesRetryAndReturnToTitleCommands()
        {
            TypeInfo entry = typeof(GrayboxFormalSaveEntryController3D)
                .GetTypeInfo();

            Assert.That(entry.GetMethod(
                "RetryWaveCheckpoint",
                BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(entry.GetMethod(
                "ReturnToTitle",
                BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);

            string source = Read(
                "Assets/_Game/Scripts/Graybox3D/Usability/" +
                "GrayboxFormalSaveEntryController3D.cs");
            StringAssert.Contains("TryRetryWaveCheckpoint", source);
            StringAssert.Contains("SceneManager.LoadScene", source);
        }

        [Test]
        public void IDEA0017_RealInputLoopBlocksBuildKeyWhileSettlementIsOpen()
        {
            var canvasObject = new GameObject(
                "Task9.Settlement.Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            var settlementObject = new GameObject(
                "Task9.Settlement.Controller");
            var view = settlementObject.AddComponent<
                GrayboxDefenseSettlementView3D>();
            var settlement = settlementObject.AddComponent<
                GrayboxDefenseSettlementController3D>();
            view.Configure(canvas);
            settlement.Configure(view, new NoOpSettlementCommands());
            Assert.That(settlement.Open(CreateDefeatSnapshot()), Is.True);

            var defenseObject = new GameObject(
                "Task9.Settlement.Defense");
            var defense = defenseObject.AddComponent<
                GrayboxDefenseController3D>();
            SetField(defense, "settlementController", settlement);
            var buildingObject = new GameObject(
                "Task9.Settlement.BuildingInput");
            var building = buildingObject.AddComponent<
                GrayboxBuildingInputRouter3D>();
            var coordinatorObject = new GameObject(
                "Task9.Settlement.InputCoordinator");
            var coordinator = coordinatorObject.AddComponent<
                GrayboxUsabilityInputCoordinator3D>();
            SetField(coordinator, "buildingInput", building);
            SetField(coordinator, "defense", defense);

            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.B));
            InputSystem.Update();

            GrayboxInputSuppression suppression =
                coordinator.ProcessCurrentInput();

            Assert.That(coordinator.BuildingInputInvocationCount, Is.Zero);
            Assert.That(suppression.Move, Is.True);
            Assert.That(suppression.Deployment, Is.True);
            Assert.That(suppression.Destination, Is.True);
            Assert.That(suppression.CameraDrag, Is.True);
            Assert.That(suppression.Home, Is.True);
        }

        private static string Read(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(relativePath));
        }

        private static SingleCityDefenseSettlementSnapshot
            CreateDefeatSnapshot()
        {
            var metrics = new Dictionary<string, int>();
            var statistics =
                new SingleCityDefenseCampaignStatisticsSnapshot(
                    12f,
                    0,
                    0,
                    metrics,
                    metrics,
                    metrics,
                    metrics,
                    0,
                    0,
                    2000,
                    0,
                    false);
            var campaign = new SingleCityDefenseCampaignSnapshot(
                1,
                SingleCityDefenseCampaignPhase.Defeat,
                0f,
                0,
                0,
                0,
                0,
                2000,
                SingleCityDefenseCampaignResult.Defeat,
                null,
                statistics);
            var settlementModel = new SingleCityDefenseSettlementModel();
            Assert.That(settlementModel.TryPublish(
                1ul,
                campaign,
                new SingleCityDefenseSettlementSessionStatistics(
                    0,
                    0f,
                    0f,
                    false,
                    false),
                out SingleCityDefenseSettlementSnapshot snapshot), Is.True);
            return snapshot;
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private sealed class NoOpSettlementCommands :
            IGrayboxDefenseSettlementCommands3D
        {
            public GrayboxDefenseSettlementCommandResult3D Execute(
                SingleCityDefenseSettlementAction action)
            {
                return new GrayboxDefenseSettlementCommandResult3D(
                    false,
                    string.Empty);
            }
        }
    }
}
