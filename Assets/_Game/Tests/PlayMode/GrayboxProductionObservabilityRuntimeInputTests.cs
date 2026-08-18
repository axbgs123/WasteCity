using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionObservabilityRuntimeInputTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private const string ObservabilityCanvasName =
            "ProductionObservabilityCanvas";
        private const string InventoryPanelName =
            "InventoryCraftingPanel";
        private const string ResearchPanelName = "ResearchTreePanel";
        private const string ResourceBarName = "ResourceStatusBar";
        private const string ResourceLedgerName =
            "FullResourceLedgerPanel";

        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator LoadGrayboxScene()
        {
            previousTimeScale = Time.timeScale;
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.timeScale = 1f;
            InputSystem.settings.updateMode =
                InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadGrayboxScene()
        {
            Time.timeScale = 1f;
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            if (graybox.IsValid() && graybox.isLoaded)
            {
                Scene empty = SceneManager.CreateScene(
                    "GrayboxProductionObservabilityRuntimeEmpty");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(graybox);
            }
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added)
                InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior =
                previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                previousEditorInputBehavior;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0011_RealEAndTKeysCoordinateWithBuildAndSystemMenu()
        {
            AssertSerializedObservabilityContract();
            GameObject inventory = RequireSceneObject(InventoryPanelName, true);
            GameObject research = RequireSceneObject(ResearchPanelName, true);
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            GrayboxSystemMenuController3D systemMenu =
                Object.FindObjectOfType<GrayboxSystemMenuController3D>();
            Assert.That(inventory.activeSelf, Is.False);
            Assert.That(research.activeSelf, Is.False);

            yield return TapKey(Key.E);
            Assert.That(inventory.activeInHierarchy, Is.True);
            Assert.That(research.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            yield return TapKey(Key.T);
            Assert.That(inventory.activeSelf, Is.False);
            Assert.That(research.activeInHierarchy, Is.True);
            yield return TapKey(Key.T);
            Assert.That(research.activeSelf, Is.False);

            yield return TapKey(Key.B);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            yield return TapKey(Key.E);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));
            Assert.That(inventory.activeInHierarchy, Is.True);
            yield return TapKey(Key.E);
            Assert.That(inventory.activeSelf, Is.False);

            yield return TapKey(Key.Escape);
            Assert.That(systemMenu.IsOpen, Is.True);
            yield return TapKey(Key.E);
            yield return TapKey(Key.T);
            Assert.That(inventory.activeSelf, Is.False);
            Assert.That(research.activeSelf, Is.False);
            Assert.That(systemMenu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest]
        public IEnumerator IDEA0011_ResourceBarShowsCapacityFlowAndRealClickLedger()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 157), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Ammunition, 9), Is.True);
            yield return null;

            GameObject bar = RequireSceneObject(ResourceBarName);
            Assert.That(bar.activeInHierarchy, Is.True);
            Graphic passiveBackground = RequireSceneObject(
                "ResourceStatus.Background").GetComponent<Graphic>();
            Assert.That(passiveBackground, Is.Not.Null);
            Assert.That(passiveBackground.raycastTarget, Is.False);

            string[] expectedVisible = ResourceDefinitionCatalog
                .BaseHudResourceIds
                .Concat(new[] { ResourceIds.Alloy, ResourceIds.Ammunition })
                .ToArray();
            foreach (string resourceId in expectedVisible)
            {
                GameObject item = RequireSceneObject(
                    "ResourceStatus.Item." + resourceId);
                Assert.That(item.activeInHierarchy, Is.True, resourceId);
                Assert.That(item.GetComponent<Button>(), Is.Not.Null, resourceId);
                Text amount = RequireText(
                    "ResourceStatus.Item." + resourceId + ".Amount");
                Text capacity = RequireText(
                    "ResourceStatus.Item." + resourceId + ".Capacity");
                Text flow = RequireText(
                    "ResourceStatus.Item." + resourceId + ".NetFlow");
                Assert.That(amount.text,
                    Does.Contain(session.Inventory.Get(resourceId).ToString()),
                    resourceId);
                Assert.That(capacity.text,
                    Does.Contain(
                        ResourceCapacityPolicy.FormalBaseCapacityPerResource
                            .ToString()),
                    resourceId);
                Assert.That(flow.text, Does.Contain("/s"), resourceId);
                Assert.That(amount.raycastTarget, Is.False, resourceId);
                Assert.That(capacity.raycastTarget, Is.False, resourceId);
                Assert.That(flow.raycastTarget, Is.False, resourceId);
            }
            Assert.That(RequireText(
                    "ResourceStatus.Item." + ResourceIds.Alloy +
                    ".Capacity").text,
                Does.Contain("超出 7"));
            Assert.That(modifier.SetResource(ResourceIds.Ammunition, 0), Is.True);
            yield return null;
            Assert.That(RequireSceneObject(
                    "ResourceStatus.Item." + ResourceIds.Ammunition,
                    includeInactive: true).activeInHierarchy,
                Is.True,
                "A discovered HUD resource must stay visible after depletion.");
            yield return HoverUiElement(
                RequireSceneObject(
                    "ResourceStatus.Item." + ResourceIds.Iron));
            GameObject tooltip = RequireSceneObject(
                "ResourceStatus.Tooltip",
                includeInactive: true);
            Assert.That(tooltip.activeInHierarchy, Is.True);
            Text tooltipText = RequireText("ResourceStatus.Tooltip.Text");
            Assert.That(tooltipText.text, Does.Contain("容量：基础"));
            Assert.That(tooltipText.text, Does.Contain("近期收入"));
            Assert.That(tooltipText.text, Does.Contain("近期净值"));

            yield return ClickUiElement(
                RequireSceneObject(
                    "ResourceStatus.Item." + ResourceIds.Iron),
                MouseButton.Left);
            GameObject ledger = RequireSceneObject(ResourceLedgerName, true);
            Assert.That(ledger.activeInHierarchy, Is.True);
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                Assert.That(
                    RequireSceneObject(
                        "ResourceLedger.Item." + definition.Id,
                        includeInactive: true).activeInHierarchy,
                    Is.True,
                    definition.Id);
            }
        }

        [UnityTest]
        public IEnumerator IDEA0011_DefaultSceneUsesFormalInitialInventory()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            Assert.That(session.DevelopmentFixtureEnabled, Is.False);
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                Assert.That(
                    session.Inventory.Get(definition.Id),
                    Is.EqualTo(definition.FormalInitialCityAmount),
                    definition.Id);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IDEA0011_ClosedStaticUiDoesNotRefreshEveryFrame()
        {
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            yield return null;
            uint refreshes = operations.ViewRefreshCount;

            for (var frame = 0; frame < 300; frame++)
                yield return null;

            Assert.That(operations.ViewRefreshCount, Is.EqualTo(refreshes));
            Assert.That(operations.RefreshIfChanged(), Is.False);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            bool refreshed = false;
            for (var call = 0; call < 300; call++)
                refreshed |= operations.RefreshIfChanged();
            long allocated =
                System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(refreshed, Is.False);
            Assert.That(allocated, Is.Zero);

            yield return TapKey(Key.E);
            Assert.That(operations.RefreshIfChanged(), Is.False);
            before = System.GC.GetAllocatedBytesForCurrentThread();
            refreshed = false;
            for (var call = 0; call < 300; call++)
                refreshed |= operations.RefreshIfChanged();
            allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(refreshed, Is.False);
            Assert.That(allocated, Is.Zero);

            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.UnlockResearch(
                DemoResearchCatalog.BasicMetallurgyId), Is.True);
            Assert.That(operations.Backpack.Add(ResourceIds.Iron, 4),
                Is.EqualTo(4));
            Assert.That(operations.Crafting.TryEnqueue(
                ResourceRecipeCatalog.FieldAlloyId,
                1), Is.True);
            Assert.That(operations.RefreshIfChanged(), Is.True);
            before = System.GC.GetAllocatedBytesForCurrentThread();
            refreshed = false;
            for (var call = 0; call < 300; call++)
            {
                operations.Crafting.Tick(.0001f, globallyPaused: false);
                refreshed |= operations.RefreshIfChanged();
            }
            allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(refreshed, Is.False);
            Assert.That(allocated, Is.Zero);
            Assert.That(operations.Crafting.ActiveProgressSeconds,
                Is.GreaterThan(0f),
                "The stable queue probe must advance real rule time without crossing the displayed precision boundary.");
        }

        [UnityTest]
        public IEnumerator IDEA0011_RecentActualInventoryFlowUsesUnscaledWindow()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            Assert.That(session, Is.Not.Null);
            int before = session.Inventory.Get(ResourceIds.Iron);
            Assert.That(session.Inventory.Add(ResourceIds.Iron, 5), Is.EqualTo(5));
            Assert.That(session.Inventory.TrySpend(ResourceIds.Iron, 2), Is.True);
            yield return null;

            Text flow = RequireText(
                "ResourceStatus.Item." + ResourceIds.Iron + ".NetFlow");
            Assert.That(flow.text, Does.Contain("+3/s"));
            yield return HoverUiElement(RequireSceneObject(
                "ResourceStatus.Item." + ResourceIds.Iron));
            Text tooltip = RequireText("ResourceStatus.Tooltip.Text");
            Assert.That(tooltip.text,
                Does.Contain("近期收入 5/s")
                    .And.Contain("近期支出 2/s")
                    .And.Contain("近期净值 +3/s"));

            yield return new WaitForSecondsRealtime(1.1f);
            yield return null;
            Assert.That(flow.text, Does.Contain("0/s"));

            Time.timeScale = 0f;
            Assert.That(session.Inventory.Add(ResourceIds.Iron, 6), Is.EqualTo(6));
            yield return null;
            Assert.That(flow.text, Does.Contain("+6/s"),
                "Manual inventory changes must remain observable while globally paused.");

            yield return new WaitForSecondsRealtime(1.1f);
            yield return null;
            Assert.That(flow.text, Does.Contain("0/s"));

            int rollbackBefore = session.Inventory.Get(ResourceIds.Iron);
            Assert.That(session.Inventory.TrySpend(ResourceIds.Iron, 4), Is.True);
            session.Inventory.Restore(ResourceIds.Iron, rollbackBefore);
            yield return null;
            Assert.That(flow.text, Does.Contain("0/s"));
            Assert.That(tooltip.text,
                Does.Contain("近期收入 4/s")
                    .And.Contain("近期支出 4/s")
                    .And.Contain("近期净值 0/s"));
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(before + 9));
        }

        [UnityTest]
        public IEnumerator IDEA0011_ProductionFlowTooltipNamesSourceAndDestination()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxProductionController3D production =
                Object.FindObjectOfType<GrayboxProductionController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(production, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Smelter.RequiredResearchId), Is.True);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Assembler.RequiredResearchId), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Stone, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Iron, 0), Is.True);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(world.Coordinates.TryWorldToCell(
                city.transform.position,
                out int cityX,
                out int cityY), Is.True);

            GrayboxBuildingInstance3D smelter = BeginGroundConstruction(
                session,
                presentation,
                BuildingCatalog.Smelter,
                cityX + 2,
                cityY,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            GrayboxBuildingInstance3D assembler = BeginGroundConstruction(
                session,
                presentation,
                BuildingCatalog.Assembler,
                cityX + 2,
                cityY + 3,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 0), Is.True);

            float stateDeadline = Time.realtimeSinceStartup + 1f;
            while ((!production.Clock.Runtime.TryGetState(
                        smelter.StableInstanceId,
                        out _) ||
                    !production.Clock.Runtime.TryGetState(
                        assembler.StableInstanceId,
                        out _)) &&
                   Time.realtimeSinceStartup < stateDeadline)
            {
                yield return null;
            }
            Assert.That(production.Clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState smelterState), Is.True);
            Assert.That(production.Clock.Runtime.TryGetState(
                assembler.StableInstanceId,
                out BuildingProductionState assemblerState), Is.True);

            yield return new WaitForSecondsRealtime(1.1f);
            yield return null;
            Time.timeScale = 0f;
            smelterState.Output.Set(ResourceIds.Alloy, 1);
            assemblerState.Input.Set(ResourceIds.Alloy, 0);
            Assert.That(production.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            Assert.That(production.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            yield return null;

            Assert.That(session.Inventory.Get(ResourceIds.Alloy), Is.Zero,
                "The observed alloy must travel from the smelter through the city inventory into the assembler.");
            Assert.That(assemblerState.Input.Get(ResourceIds.Alloy),
                Is.EqualTo(1));
            yield return HoverUiElement(RequireSceneObject(
                "ResourceStatus.Item." + ResourceIds.Alloy));
            Text tooltip = RequireText("ResourceStatus.Tooltip.Text");
            Assert.That(tooltip.text,
                Does.Contain("近期收入 1/s")
                    .And.Contain("近期支出 1/s")
                    .And.Contain("收入来源")
                    .And.Contain(BuildingCatalog.Smelter.Name)
                    .And.Contain("支出去向")
                    .And.Contain(BuildingCatalog.Assembler.Name));
        }

        [UnityTest]
        public IEnumerator IDEA0011_RealResearchClicksStartProgressAndCancel()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.SetResource(ResourceIds.Iron, 100), Is.True);
            Assert.That(modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast100), Is.True);

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit5);
            yield return MoveToInnerCell(
                Object.FindObjectOfType<GrayboxMobileCityController3D>(),
                3,
                2);
            yield return ClickWorld(MouseButton.Left);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (session.Instances.Count == 0 &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            GrayboxBuildingInstance3D station = session.Instances[0];
            while (station.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(station.Placement.Definition,
                Is.SameAs(BuildingCatalog.ResearchStation));
            Assert.That(station.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            yield return TapKey(Key.Escape);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);

            yield return TapKey(Key.T);
            GameObject researchPanel = RequireSceneObject(ResearchPanelName);
            Assert.That(researchPanel.activeInHierarchy, Is.True);
            foreach (ResearchDefinition definition in DemoResearchCatalog.All)
            {
                Assert.That(
                    RequireSceneObject(
                        "Research.Node." + definition.Id.Value),
                    Is.Not.Null,
                    definition.Id.Value);
            }
            Assert.That(RequireText(
                    "Research.Node." +
                    DemoResearchCatalog.AutomatedDefenseId +
                    ".State").text,
                Does.Contain("本阶段未开放"));
            Assert.That(RequireText(
                    "Research.Node." +
                    DemoResearchCatalog.AmmunitionAssemblyId +
                    ".State").text,
                Does.Contain("前置"));

            yield return ClickUiElement(
                RequireSceneObject(
                    "Research.Node." +
                    DemoResearchCatalog.BasicMetallurgyId),
                MouseButton.Left);
            int ironBeforeStart = session.Inventory.Get(ResourceIds.Iron);
            yield return ClickUiElement(
                RequireSceneObject("Research.Start"),
                MouseButton.Left);
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(ironBeforeStart - 10));
            Text progress = RequireText("Research.Active.Progress");
            string progressBefore = progress.text;
            Assert.That(progressBefore,
                Does.Contain("剩余").And.Contain("效率 100%"));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(progress.text, Is.Not.EqualTo(progressBefore));
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            Assert.That(modifier.SetCityMode(CityMode.Mobile), Is.True);
            yield return null;
            Assert.That(progress.text, Does.Contain("效率 50%"));
            Time.timeScale = 0f;
            yield return null;
            Assert.That(progress.text, Does.Contain("全局暂停"));
            Time.timeScale = 1f;

            yield return ClickUiElement(
                RequireSceneObject("Research.Cancel"),
                MouseButton.Left);
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(ironBeforeStart - 2));
            Assert.That(RequireSceneObject("Research.Active", true).activeSelf,
                Is.False);
        }

        [UnityTest]
        public IEnumerator IDEA0011_RealCraftingClicksMapOneFiveAndMaximum()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.UnlockResearch(
                DemoResearchCatalog.BasicMetallurgyId), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Iron, 100), Is.True);

            yield return TapKey(Key.E);
            Assert.That(RequireSceneObject(InventoryPanelName).activeInHierarchy,
                Is.True);
            yield return ShiftClickUiElement(
                RequireSceneObject(
                    "Inventory.City." + ResourceIds.Iron));
            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Tab.Crafting"),
                MouseButton.Left);

            GameObject recipe = RequireSceneObject(
                "Crafting.Recipe." + ResourceRecipeCatalog.FieldAlloyId);
            Assert.That(recipe.GetComponentInChildren<Text>(true).text,
                Does.Contain("当前可排 20"));
            Text count = RequireText("Crafting.Queue.Count");
            yield return ClickUiElement(recipe, MouseButton.Left);
            Assert.That(ParseLeadingInteger(count.text), Is.EqualTo(1));
            yield return ClickUiElement(recipe, MouseButton.Right);
            Assert.That(ParseLeadingInteger(count.text), Is.EqualTo(6));
            yield return ShiftClickUiElement(recipe);
            Assert.That(ParseLeadingInteger(count.text),
                Is.EqualTo(CraftingQueueModel.MaximumQueuedExecutions));
            Assert.That(RequireText("Crafting.Queue.Reason").text,
                Does.Contain("队首：").And.Contain("应急合金"));
            Assert.That(recipe.GetComponentInChildren<Text>(true).text,
                Does.Contain("当前可排 0"));
            yield return ClickUiElement(recipe, MouseButton.Right);
            Assert.That(RequireText("Crafting.Queue.Reason").text,
                Does.Contain("队列容量不足"));

            int ironBeforeCancel = BackpackAmount(
                Object.FindObjectOfType<
                    GrayboxOperationsController3D>(),
                ResourceIds.Iron);
            yield return ClickUiElement(
                RequireSceneObject("Crafting.Queue.CancelFirst"),
                MouseButton.Left);
            Assert.That(ParseLeadingInteger(count.text),
                Is.EqualTo(CraftingQueueModel.MaximumQueuedExecutions - 1));
            Assert.That(BackpackAmount(
                    Object.FindObjectOfType<
                        GrayboxOperationsController3D>(),
                    ResourceIds.Iron),
                Is.EqualTo(ironBeforeCancel + 4));
        }

        [UnityTest]
        public IEnumerator IDEA0011_RealBackpackClicksMoveSplitAndReturnAtomically()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(operations, Is.Not.Null);
            Assert.That(modifier.SetResource(ResourceIds.Iron, 100), Is.True);

            yield return TapKey(Key.E);
            yield return ShiftClickUiElement(
                RequireSceneObject(
                    "Inventory.City." + ResourceIds.Iron));
            Assert.That(session.Inventory.Get(ResourceIds.Iron), Is.Zero);
            AssertBackpackSlot(
                operations,
                0,
                ResourceIds.Iron,
                100);

            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Tab.Backpack"),
                MouseButton.Left);
            GameObject slot0 = RequireSceneObject(
                "Inventory.Backpack.Slot.0");
            GameObject slot1 = RequireSceneObject(
                "Inventory.Backpack.Slot.1");
            GameObject slot2 = RequireSceneObject(
                "Inventory.Backpack.Slot.2");
            Assert.That(slot0.GetComponent<Button>(), Is.Not.Null,
                "Backpack slots must receive real Input System clicks.");
            Assert.That(slot1.GetComponent<Button>(), Is.Not.Null);
            Assert.That(slot2.GetComponent<Button>(), Is.Not.Null);

            yield return ClickUiElement(slot0, MouseButton.Left);
            yield return ClickUiElement(slot1, MouseButton.Left);
            AssertBackpackSlot(operations, 0, null, 0);
            AssertBackpackSlot(
                operations,
                1,
                ResourceIds.Iron,
                100);

            yield return ClickUiElement(slot1, MouseButton.Right);
            yield return ClickUiElement(slot2, MouseButton.Right);
            AssertBackpackSlot(
                operations,
                1,
                ResourceIds.Iron,
                50);
            AssertBackpackSlot(
                operations,
                2,
                ResourceIds.Iron,
                50);
            Assert.That(RequireText(
                    "Inventory.Backpack.Slot.2.Label").text,
                Does.Contain("待逐个放置 50"));

            yield return ClickUiElement(slot0, MouseButton.Right);
            AssertBackpackSlot(
                operations,
                0,
                ResourceIds.Iron,
                1);
            AssertBackpackSlot(
                operations,
                2,
                ResourceIds.Iron,
                49);
            yield return ClickUiElement(slot0, MouseButton.Right);
            AssertBackpackSlot(
                operations,
                0,
                ResourceIds.Iron,
                2);
            AssertBackpackSlot(
                operations,
                2,
                ResourceIds.Iron,
                48);

            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Close"),
                MouseButton.Left);
            Assert.That(RequireSceneObject(
                    InventoryPanelName,
                    includeInactive: true).activeSelf,
                Is.False);
            yield return TapKey(Key.E);
            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Tab.Backpack"),
                MouseButton.Left);
            slot0 = RequireSceneObject("Inventory.Backpack.Slot.0");
            yield return ClickUiElement(slot0, MouseButton.Right);
            AssertBackpackSlot(
                operations,
                0,
                ResourceIds.Iron,
                2);
            AssertBackpackSlot(
                operations,
                2,
                ResourceIds.Iron,
                48);

            yield return ShiftClickUiElement(slot0);
            AssertBackpackSlot(operations, 0, null, 0);
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator IDEA0011_CityBackpackPartialTransferShowsResult()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(operations, Is.Not.Null);
            Assert.That(modifier.SetResource(ResourceIds.Iron, 10), Is.True);
            Assert.That(operations.Backpack.Add(
                ResourceIds.Alloy,
                2900), Is.EqualTo(2900));
            Assert.That(operations.Backpack.Add(
                ResourceIds.Iron,
                95), Is.EqualTo(95));

            yield return TapKey(Key.E);
            yield return ShiftClickUiElement(RequireSceneObject(
                "Inventory.City." + ResourceIds.Iron));

            Assert.That(session.Inventory.Get(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(BackpackAmount(operations, ResourceIds.Iron),
                Is.EqualTo(100));
            Assert.That(RequireText("Inventory.TransferStatus").text,
                Does.Contain("部分转移 5").And.Contain("剩余 5"));

            yield return ShiftClickUiElement(RequireSceneObject(
                "Inventory.City." + ResourceIds.Iron));
            Assert.That(session.Inventory.Get(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(RequireText("Inventory.TransferStatus").text,
                Does.Contain("目标库存已满"));
        }

        [UnityTest]
        public IEnumerator IDEA0011_RealProductionRowClicksAccessCachesOnlyWhenAllowed()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxProductionController3D production =
                Object.FindObjectOfType<GrayboxProductionController3D>();
            GrayboxOperationsController3D operations =
                Object.FindObjectOfType<GrayboxOperationsController3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxLeaderController3D leader =
                Object.FindObjectOfType<GrayboxLeaderController3D>();
            GrayboxDirectControlCoordinator directControl =
                Object.FindObjectOfType<GrayboxDirectControlCoordinator>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(production, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(city, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(leader, Is.Not.Null);
            Assert.That(directControl, Is.Not.Null);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Smelter.RequiredResearchId), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Stone, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Iron, 10), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 0), Is.True);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(world.Coordinates.TryWorldToCell(
                city.transform.position,
                out int cityX,
                out int cityY), Is.True);

            const int offsetFromCity = 2;
            int buildingX = cityX + offsetFromCity;
            int buildingY = cityY;
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                BuildingCatalog.Smelter,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                BuildingCatalog.Smelter,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                buildingX,
                buildingY,
                cityX,
                cityY,
                session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                compatibleResourceNode: ResourceNodeBinding.None,
                contentVisible: true,
                unlock: unlock,
                canAfford: true);
            Assert.That(session.TryBeginConstruction(
                request,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            modifier.CompleteAllConstruction();
            float stateDeadline = Time.realtimeSinceStartup + 1f;
            while (!production.Clock.Runtime.TryGetState(
                       instance.StableInstanceId,
                       out _) &&
                   Time.realtimeSinceStartup < stateDeadline)
            {
                yield return null;
            }
            Assert.That(production.Clock.Runtime.TryGetState(
                instance.StableInstanceId,
                out BuildingProductionState state), Is.True);

            Time.timeScale = 0f;
            float alignToStep = GrayboxProductionClock3D.StepSeconds -
                production.Clock.AccumulatorSeconds;
            Assert.That(production.Tick(alignToStep, paused: false), Is.True);
            Assert.That(production.Clock.AccumulatorSeconds,
                Is.EqualTo(0f).Within(.00001f));
            Assert.That(operations.TryOpenProductionDetail(
                instance.StableInstanceId), Is.True);
            Assert.That(operations.RefreshIfChanged(), Is.False);
            long allocationBefore =
                System.GC.GetAllocatedBytesForCurrentThread();
            bool productionRefreshed = false;
            bool productionTicked = true;
            for (var call = 0; call < 300; call++)
            {
                productionTicked &= production.Tick(
                    .0001f,
                    paused: false);
                productionRefreshed |= operations.RefreshIfChanged();
            }
            long productionAllocated =
                System.GC.GetAllocatedBytesForCurrentThread() -
                allocationBefore;
            Assert.That(production.Clock.AccumulatorSeconds,
                Is.EqualTo(.03f).Within(.0001f),
                "The stable production probe must advance non-zero rule time without crossing the fixed simulation step.");
            Assert.That(productionTicked, Is.True);
            Assert.That(productionRefreshed, Is.False);
            Assert.That(productionAllocated, Is.Zero);
            operations.ClosePanels();
            operations.RefreshIfChanged();

            Assert.That(world.Coordinates.TryCellToWorld(
                buildingX,
                buildingY,
                leader.transform.position.y,
                out Vector3 leaderWorld), Is.True);
            leader.transform.position = leaderWorld +
                new Vector3(.5f, 0f, .5f);
            directControl.Refresh();
            Assert.That(directControl.ControlTarget,
                Is.EqualTo(DirectControlTarget.Leader));
            Time.timeScale = 0f;
            state.Input.Set(ResourceIds.Iron, 0);
            state.Output.Set(ResourceIds.Alloy, 3);
            session.Inventory.Set(ResourceIds.Iron, 10);
            session.Inventory.Set(ResourceIds.Alloy, 0);
            operations.Backpack.Add(ResourceIds.Iron, 4);
            yield return null;

            Time.timeScale = 1f;
            Assert.That(world.Coordinates.TryCellToWorld(
                buildingX,
                buildingY,
                1f,
                out Vector3 buildingWorld), Is.True);
            QueueMouse(Camera.main.WorldToScreenPoint(buildingWorld));
            yield return null;
            yield return ClickWorld(MouseButton.Left);
            Assert.That(RequireSceneObject(ResourceLedgerName).activeSelf,
                Is.True);
            Time.timeScale = 0f;
            string productionRowName = RequireProductionRowName(
                instance.StableInstanceId);
            GameObject input = RequireSceneObject(
                productionRowName + ".InputTransfer");
            GameObject output = RequireSceneObject(
                productionRowName + ".OutputTransfer");
            GameObject pause = RequireSceneObject(
                productionRowName + ".Pause");
            Assert.That(input.GetComponent<Button>(), Is.Not.Null);
            Assert.That(output.GetComponent<Button>(), Is.Not.Null);
            Assert.That(pause.GetComponent<Button>(), Is.Not.Null);
            Assert.That(RequireText(productionRowName + ".Input").text,
                Does.StartWith("输入：").And.Not.Contains("输入：输入："));
            Assert.That(RequireText(productionRowName + ".Output").text,
                Does.StartWith("输出：").And.Not.Contains("输出：输出："));
            Assert.That(RequireText(productionRowName + ".Status").text,
                Does.Contain("物流已连接"));

            yield return ClickUiElement(pause, MouseButton.Left);
            Assert.That(state.IsPlayerPaused, Is.True);
            Assert.That(RequireText(productionRowName + ".Status").text,
                Does.Contain("玩家暂停运行"));
            yield return ClickUiElement(pause, MouseButton.Left);
            Assert.That(state.IsPlayerPaused, Is.False);

            yield return ClickUiElement(input, MouseButton.Left);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(session.Inventory.Get(ResourceIds.Iron), Is.Zero);
            yield return ClickUiElement(output, MouseButton.Left);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(session.Inventory.Get(ResourceIds.Alloy), Is.EqualTo(3));

            state.Input.Set(ResourceIds.Iron, 0);
            state.Output.Set(ResourceIds.Alloy, 2);
            yield return ShiftClickUiElement(input);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(4));
            AssertBackpackSlot(operations, 0, null, 0);
            yield return ShiftClickUiElement(output);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            AssertBackpackSlot(
                operations,
                0,
                ResourceIds.Alloy,
                2);

            state.Output.Set(ResourceIds.Alloy, 1);
            Assert.That(modifier.SetCityMode(CityMode.Mobile), Is.True);
            Assert.That(production.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            operations.RefreshIfChanged();
            int cityAlloyBeforeLogisticsDenied =
                session.Inventory.Get(ResourceIds.Alloy);
            yield return ClickUiElement(output, MouseButton.Left);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(cityAlloyBeforeLogisticsDenied));
            Assert.That(RequireText(
                    productionRowName + ".AccessStatus").text,
                Does.Contain("不在物流范围"));
            Assert.That(RequireText(productionRowName + ".Status").text,
                Does.Contain("已脱离物流"));
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(production.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            operations.RefreshIfChanged();

            leader.transform.position += new Vector3(20f, 0f, 20f);
            state.Input.Set(ResourceIds.Iron, 0);
            state.Output.Set(ResourceIds.Alloy, 1);
            operations.Backpack.Add(ResourceIds.Iron, 3);
            int cityAlloyBeforeDenied =
                session.Inventory.Get(ResourceIds.Alloy);
            yield return ShiftClickUiElement(input);
            yield return ClickUiElement(output, MouseButton.Left);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(cityAlloyBeforeDenied));
            Assert.That(operations.Backpack.GetSlot(1).ResourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(operations.Backpack.GetSlot(1).Amount,
                Is.EqualTo(3));
            Assert.That(RequireText(
                    productionRowName + ".AccessStatus").text,
                Does.Contain("无法访问"));
        }

        private GrayboxDeveloperModifier3D CreateModifier(
            GrayboxBuildingSession3D session)
        {
            Assert.That(session, Is.Not.Null);
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            Assert.That(city, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            return new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
        }

        private static GrayboxBuildingInstance3D BeginGroundConstruction(
            GrayboxBuildingSession3D session,
            GrayboxBuildingWorldView3D presentation,
            BuildingDefinition definition,
            int buildingX,
            int buildingY,
            int cityX,
            int cityY)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                buildingX,
                buildingY,
                cityX,
                cityY,
                session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                compatibleResourceNode: ResourceNodeBinding.None,
                contentVisible: true,
                unlock: unlock,
                canAfford: true);
            Assert.That(session.TryBeginConstruction(
                request,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static void AssertBackpackSlot(
            GrayboxOperationsController3D operations,
            int index,
            string resourceId,
            int amount)
        {
            BackpackSlot slot = operations.Backpack.GetSlot(index);
            Assert.That(slot.ResourceId, Is.EqualTo(resourceId), index.ToString());
            Assert.That(slot.Amount, Is.EqualTo(amount), index.ToString());
        }

        private static int BackpackAmount(
            GrayboxOperationsController3D operations,
            string resourceId)
        {
            Assert.That(operations, Is.Not.Null);
            int amount = 0;
            for (var index = 0;
                 index < operations.Backpack.SlotCount;
                 index++)
            {
                BackpackSlot slot = operations.Backpack.GetSlot(index);
                if (string.Equals(
                        slot.ResourceId,
                        resourceId,
                        System.StringComparison.Ordinal))
                {
                    amount += slot.Amount;
                }
            }
            return amount;
        }

        private static string RequireProductionRowName(
            string stableInstanceId)
        {
            Text[] labels = Object.FindObjectsOfType<Text>(true);
            for (var index = 0; index < labels.Length; index++)
            {
                Text label = labels[index];
                if (!label.name.EndsWith(
                        ".StableId",
                        System.StringComparison.Ordinal) ||
                    !string.Equals(
                        label.text,
                        stableInstanceId,
                        System.StringComparison.Ordinal) ||
                    label.transform.parent == null)
                {
                    continue;
                }
                return label.transform.parent.name;
            }
            Assert.Fail(
                "No production row projects stable ID " +
                stableInstanceId);
            return null;
        }

        private static void AssertSerializedObservabilityContract()
        {
            GameObject grayboxUi = FindSceneObject("GrayboxUI");
            GameObject observability = FindSceneObject(
                ObservabilityCanvasName,
                includeInactive: true);
            Assert.That(grayboxUi, Is.Not.Null);
            Assert.That(
                observability,
                Is.Not.Null,
                "The formal scene must serialize the production observability canvas.");
            Assert.That(observability.transform.parent.gameObject,
                Is.SameAs(grayboxUi));
            Canvas canvas = observability.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(observability.GetComponent<GraphicRaycaster>(),
                Is.Not.Null);
            Canvas building = RequireSceneObject("BuildingCanvas")
                .GetComponent<Canvas>();
            Canvas systemMenu = RequireSceneObject("SystemMenuCanvas")
                .GetComponent<Canvas>();
            Assert.That(canvas.sortingOrder,
                Is.GreaterThan(building.sortingOrder));
            Assert.That(canvas.sortingOrder,
                Is.LessThan(systemMenu.sortingOrder));
            Assert.That(FindSceneObject(ResourceBarName, true), Is.Not.Null);
            Assert.That(FindSceneObject(InventoryPanelName, true), Is.Not.Null);
            Assert.That(FindSceneObject(ResearchPanelName, true), Is.Not.Null);
            Assert.That(FindSceneObject(ResourceLedgerName, true), Is.Not.Null);
            Assert.That(Object.FindObjectsOfType<EventSystem>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<InputSystemUIInputModule>(true),
                Has.Length.EqualTo(1));
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator ShiftClickUiElement(GameObject target)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            yield return null;
            yield return ClickUiElement(target, MouseButton.Left);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator ClickUiElement(
            GameObject target,
            MouseButton button)
        {
            InputSystemUIInputModule module =
                Object.FindObjectOfType<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.enabled, Is.True);
            Assert.That(module.point?.action?.enabled, Is.True);
            if (button == MouseButton.Left)
                Assert.That(module.leftClick?.action?.enabled, Is.True);
            else if (button == MouseButton.Right)
                Assert.That(module.rightClick?.action?.enabled, Is.True);
            else
                Assert.Fail("Only real left and right UI clicks are supported.");
            Assert.That(target.activeInHierarchy, Is.True, target.name);
            RectTransform rect = target.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, target.name);
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
            QueueMouse(position);
            yield return null;
            QueueMouse(position, button);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator HoverUiElement(GameObject target)
        {
            Assert.That(target.activeInHierarchy, Is.True, target.name);
            RectTransform rect = target.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, target.name);
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
            QueueMouse(position);
            yield return null;
            yield return null;
        }

        private IEnumerator ClickWorld(MouseButton button)
        {
            Vector2 position = mouse.position.ReadValue();
            QueueMouse(position, button);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator MoveToInnerCell(
            GrayboxMobileCityController3D city,
            int x,
            int y)
        {
            Assert.That(city, Is.Not.Null);
            Transform platform = city.transform.Find("InnerCityPlatform");
            Assert.That(platform, Is.Not.Null);
            BoxCollider surface = platform.GetComponent<BoxCollider>();
            Assert.That(surface, Is.Not.Null);
            Vector3 worldPoint = city.transform.TransformPoint(new Vector3(
                -1.28f + (x + .5f) * .32f,
                0f,
                -.96f + (y + .5f) * .32f));
            worldPoint.y = surface.bounds.max.y;
            QueueMouse(Camera.main.WorldToScreenPoint(worldPoint));
            yield return null;
        }

        private void QueueMouse(
            Vector2 position,
            MouseButton? button = null)
        {
            var state = new MouseState { position = position };
            if (button.HasValue)
                state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static GameObject RequireSceneObject(
            string name,
            bool includeInactive = false)
        {
            GameObject value = FindSceneObject(name, includeInactive);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        private static GameObject FindSceneObject(
            string name,
            bool includeInactive = false)
        {
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            Transform[] transforms = Object.FindObjectsOfType<Transform>(
                includeInactive);
            return transforms
                .Where(value => value.gameObject.scene == graybox)
                .Select(value => value.gameObject)
                .FirstOrDefault(value => value.name == name);
        }

        private static Text RequireText(string name)
        {
            GameObject value = RequireSceneObject(name, true);
            Text text = value.GetComponent<Text>();
            Assert.That(text, Is.Not.Null, name);
            Assert.That(value.activeInHierarchy, Is.True, name);
            return text;
        }

        private static int ParseLeadingInteger(string value)
        {
            Assert.That(value, Is.Not.Null.And.Not.Empty);
            int index = 0;
            while (index < value.Length && !char.IsDigit(value[index]))
                index++;
            Assert.That(index, Is.LessThan(value.Length), value);
            int result = 0;
            while (index < value.Length && char.IsDigit(value[index]))
            {
                result = result * 10 + value[index] - '0';
                index++;
            }
            return result;
        }
    }
}
