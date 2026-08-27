using System.Collections;
using System.Collections.Generic;
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
using WasteCity.Progression;

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
            GrayboxFormalPlayModeEntryFixture.BeginIsolatedStore();
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(mouse);
        }

        [UnityTearDown]
        public IEnumerator UnloadGrayboxScene()
        {
            Time.timeScale = 1f;
            try
            {
                Scene graybox = SceneManager.GetSceneByName(SceneName);
                if (graybox.IsValid() && graybox.isLoaded)
                {
                    Scene empty = SceneManager.CreateScene(
                        "GrayboxProductionObservabilityRuntimeEmpty");
                    SceneManager.SetActiveScene(empty);
                    yield return SceneManager.UnloadSceneAsync(graybox);
                }
            }
            finally
            {
                try
                {
                    GrayboxFormalPlayModeEntryFixture.CleanupIsolatedStore();
                }
                finally
                {
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
                    GrayboxFormalPlayModeEntryFixture
                        .AssertRealSaveFilesUnchanged();
                }
            }
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
        public IEnumerator IDEA0016_RealInventoryInputShowsFormalPanelAndLeaderArt()
        {
            GameObject inventory = RequireSceneObject(InventoryPanelName, true);
            Assert.That(inventory.activeSelf, Is.False);

            yield return TapKey(Key.E);
            Assert.That(inventory.activeInHierarchy, Is.True);

            Image panel = inventory.GetComponent<Image>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(panel.sprite, Is.Not.Null);
            Assert.That(panel.sprite.name, Is.EqualTo("ui-primary-panel"));

            GameObject portraitObject = RequireSceneObject(
                "InventoryCrafting.LeaderPortrait");
            Image portrait = portraitObject.GetComponent<Image>();
            Assert.That(portrait, Is.Not.Null);
            Assert.That(portrait.sprite, Is.Not.Null);
            Assert.That(portrait.sprite.name, Is.EqualTo("character-cen-jin"));
            Assert.That(portrait.preserveAspect, Is.True);

            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Tab.Backpack"),
                MouseButton.Left);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.Not.Null);
            yield return TapKey(Key.E);
            Assert.That(inventory.activeSelf, Is.False,
                "A selected inventory button must not consume the E close key.");

            yield return TapKey(Key.E);
            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Tab.City"),
                MouseButton.Left);
            yield return TapKey(Key.Escape);
            Assert.That(inventory.activeSelf, Is.False,
                "A selected inventory button must not consume Escape.");
            Assert.That(Object.FindObjectOfType<GrayboxSystemMenuController3D>()
                .IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator IDEA0016_ResearchTreeOwnsRealViewportSearchAndModalInput()
        {
            GrayboxBuildingInteractionModel3D building =
                Object.FindObjectOfType<GrayboxBuildingInteractionModel3D>();
            yield return TapKey(Key.T);
            GameObject panel = RequireSceneObject(ResearchPanelName);
            Assert.That(panel.activeInHierarchy, Is.True);
            Assert.That(panel.GetComponent<Image>().sprite.name,
                Is.EqualTo("ui-primary-panel"));
            Assert.That(RequireSceneObject("Research.Search")
                    .GetComponent<Image>().sprite.name,
                Is.EqualTo("ui-secondary-card"));
            Assert.That(RequireSceneObject("Research.Search.Icon")
                    .GetComponent<Image>().sprite.name,
                Is.EqualTo("ui-search"));
            Assert.That(RequireSceneObject("Research.BranchConnectorLegend")
                    .GetComponent<Image>().sprite.name,
                Is.EqualTo("ui-technology-branch-connector"));
            string firstResearchId = ResearchCatalog.All[0].Id.Value;
            Assert.That(RequireSceneObject("Research.Node." + firstResearchId)
                    .GetComponent<Image>().sprite.name,
                Is.EqualTo("ui-technology-node"));
            Assert.That(RequireSceneObject(
                    "Research.Filter.Route.Technology")
                    .GetComponent<Image>().sprite.name,
                Is.EqualTo("ui-primary-button"));
            RectTransform viewport = RequireSceneObject(
                    "Research.Viewport")
                .GetComponent<RectTransform>();
            RectTransform content = RequireSceneObject(
                    "Research.Content")
                .GetComponent<RectTransform>();
            Assert.That(viewport.GetComponent<GrayboxResearchTreeViewportInput3D>(),
                Is.Not.Null);

            float zoomBefore = content.localScale.x;
            yield return ScrollUiElement(viewport, -1f);
            Assert.That(content.localScale.x, Is.LessThan(zoomBefore));

            Vector2 blank = FindViewportBlank(viewport);
            Vector2 positionBeforeDrag = content.anchoredPosition;
            yield return DragPointer(
                blank,
                blank + new Vector2(-100f, 70f),
                MouseButton.Left);
            Assert.That(content.anchoredPosition,
                Is.Not.EqualTo(positionBeforeDrag));

            yield return TapKey(Key.Home);
            Vector2 fittedPosition = content.anchoredPosition;
            float fittedZoom = content.localScale.x;
            yield return DragPointer(
                blank,
                blank + new Vector2(80f, -55f),
                MouseButton.Middle);
            Assert.That(content.anchoredPosition,
                Is.Not.EqualTo(fittedPosition));
            yield return TapKey(Key.Home);
            Assert.That(
                Vector2.Distance(content.anchoredPosition, fittedPosition),
                Is.LessThan(.01f));
            Assert.That(content.localScale.x,
                Is.EqualTo(fittedZoom).Within(.001f));

            yield return ClickUiElement(
                RequireSceneObject("Research.Filter.Route.Technology"),
                MouseButton.Left);
            yield return TapKey(Key.B);
            Assert.That(panel.activeInHierarchy, Is.True);
            Assert.That(building.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Inactive));

            InputField search = RequireSceneObject("Research.Search")
                .GetComponent<InputField>();
            yield return ClickUiElement(search.gameObject, MouseButton.Left);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(search.gameObject));
            Assert.That(search.isFocused, Is.True);
            yield return TapTextKey(Key.T, 't');
            Assert.That(search.text, Is.EqualTo("t"));
            Assert.That(panel.activeInHierarchy, Is.True,
                "T typed into search must not close the research tree.");
            yield return TapKey(Key.Escape);
            Assert.That(panel.activeInHierarchy, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            yield return TapKey(Key.Escape);
            Assert.That(panel.activeSelf, Is.False);
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
            ScrollRect resourceScroll = RequireSceneObject(
                    "ResourceStatus.Viewport")
                .GetComponent<ScrollRect>();
            Assert.That(resourceScroll, Is.Not.Null);
            Assert.That(resourceScroll.horizontal, Is.True);
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
                Image icon = RequireSceneObject(
                        "ResourceStatus.Item." + resourceId + ".Icon")
                    .GetComponent<Image>();
                Assert.That(icon.sprite, Is.Not.Null, resourceId);
                Assert.That(icon.sprite,
                    Is.Not.SameAs(ResourceIconCatalog3D.Resolve(resourceId)),
                    resourceId + " should use the serialized production icon");
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
                Is.False,
                "Discovery is derived from current authoritative facts and must not persist as hidden schema state.");
            Assert.That(modifier.SetResource(ResourceIds.AcidGland, 3), Is.True);
            yield return null;
            Assert.That(RequireSceneObject(
                    "ResourceStatus.Item." + ResourceIds.AcidGland,
                    includeInactive: true).activeInHierarchy,
                Is.True);
            Assert.That(modifier.SetResource(ResourceIds.AcidGland, 0), Is.True);
            yield return null;
            Assert.That(RequireSceneObject(
                    "ResourceStatus.Item." + ResourceIds.AcidGland,
                    includeInactive: true).activeInHierarchy,
                Is.False);
            yield return HoverUiElement(
                RequireSceneObject(
                    "ResourceStatus.Item." + ResourceIds.Iron));
            GameObject tooltip = RequireSceneObject(
                "ResourceStatus.Tooltip",
                includeInactive: true);
            Assert.That(tooltip.activeInHierarchy, Is.True);
            Text tooltipText = RequireText("ResourceStatus.Tooltip.Text");
            Assert.That(tooltipText.text,
                Does.Contain("氧化废铁").And.Contain("来源：")
                    .And.Contain("用途：").And.Contain("通用 · 原料"));
            Assert.That(tooltipText.text, Does.Contain("容量：基础"));
            Assert.That(tooltipText.text, Does.Contain("近期收入"));
            Assert.That(tooltipText.text, Does.Contain("近期净值"));

            yield return ClickUiElement(
                RequireSceneObject(
                    "ResourceStatus.Item." + ResourceIds.Iron),
                MouseButton.Left);
            GameObject ledger = RequireSceneObject(ResourceLedgerName, true);
            Assert.That(ledger.activeInHierarchy, Is.True);
            Assert.That(RequireSceneObject(
                    "ResourceLedger.Items.Viewport")
                .GetComponent<ScrollRect>(), Is.Not.Null);
            string[] ledgerVisible = ResourceDefinitionCatalog
                .BaseHudResourceIds
                .Concat(new[] { ResourceIds.Alloy })
                .ToArray();
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                Assert.That(
                    RequireSceneObject(
                        "ResourceLedger.Item." + definition.Id,
                        includeInactive: true).activeInHierarchy,
                    Is.EqualTo(ledgerVisible.Contains(definition.Id)),
                    definition.Id);
            }
            yield return ClickUiElement(
                RequireSceneObject(
                    "ResourceLedger.Filter.Route.Technology"),
                MouseButton.Left);
            Assert.That(RequireSceneObject(
                    "ResourceLedger.Item." + ResourceIds.Alloy,
                    includeInactive: true).activeInHierarchy,
                Is.True);
            Assert.That(RequireSceneObject(
                    "ResourceLedger.Item." + ResourceIds.Iron,
                    includeInactive: true).activeInHierarchy,
                Is.False);
            yield return ClickUiElement(
                RequireSceneObject(
                    "ResourceLedger.Filter.Tier.Product"),
                MouseButton.Left);
            Assert.That(RequireSceneObject(
                    "ResourceLedger.Item." + ResourceIds.Alloy,
                    includeInactive: true).activeInHierarchy,
                Is.False);
            yield return ClickUiElement(
                RequireSceneObject("ResourceLedger.Filter.All"),
                MouseButton.Left);
            Assert.That(RequireSceneObject(
                    "ResourceLedger.Item." + ResourceIds.Iron,
                    includeInactive: true).activeInHierarchy,
                Is.True);
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
                ResearchCatalog.AutomatedMachineryId), Is.True);
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
            Assert.That(modifier.SetPopulation(200), Is.True);
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
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                Assert.That(
                    RequireSceneObject(
                        "Research.Node." + definition.Id.Value),
                    Is.Not.Null,
                    definition.Id.Value);
                foreach (ResourceAmount cost in definition.Costs)
                {
                    Image costIcon = RequireSceneObject(
                            "Research.Node." + definition.Id.Value +
                            ".Cost." + cost.ResourceId + ".Icon")
                        .GetComponent<Image>();
                    Assert.That(costIcon.sprite,
                        Is.SameAs(ResolvePresentedResourceIcon(
                            cost.ResourceId)),
                        definition.Id.Value);
                }
            }
            string automatedDefenseState = RequireText(
                    "Research.Node." +
                    ResearchCatalog.AutomatedDefenseId +
                    ".State")
                .text;
            Assert.That(automatedDefenseState, Does.Contain("前置"));
            Assert.That(
                automatedDefenseState,
                Does.Not.Contain("本阶段未开放"));
            Assert.That(RequireText(
                    "Research.Node." +
                    "core.research.ballistics" +
                    ".State").text,
                Does.Contain("本阶段未开放"));
            Assert.That(RequireText(
                    "Research.Node." +
                    "core.research.bridge.psionic-mech" +
                    ".State").text,
                Does.Contain("前置").And.Not.Contain("本阶段未开放"));
            Assert.That(RequireText(
                    "Research.Node." +
                    ResearchCatalog.PrecisionAssemblyId +
                    ".State").text,
                Does.Contain("前置"));

            yield return ClickUiElement(
                RequireSceneObject(
                    "Research.Node." +
                    ResearchCatalog.AutomatedMachineryId),
                MouseButton.Left);
            InputField search = RequireSceneObject("Research.Search")
                .GetComponent<InputField>();
            yield return ClickUiElement(search.gameObject, MouseButton.Left);
            yield return TapTextKey(Key.Z, 'z');
            GameObject selectedNode = RequireSceneObject(
                "Research.Node." +
                ResearchCatalog.AutomatedMachineryId,
                includeInactive: true);
            Button startButton = RequireSceneObject("Research.Start")
                .GetComponent<Button>();
            Assert.That(selectedNode.activeSelf, Is.False);
            Assert.That(startButton.interactable, Is.False,
                "a filtered-out selection must not remain startable");
            int ironBeforeHiddenStart = session.Inventory.Get(ResourceIds.Iron);
            yield return TapKey(Key.Backspace);
            Assert.That(search.text, Is.Empty);
            yield return TapKey(Key.Escape);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            yield return ClickUiElement(startButton.gameObject, MouseButton.Left);
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(ironBeforeHiddenStart));
            yield return ClickUiElement(selectedNode, MouseButton.Left);
            int ironBeforeStart = session.Inventory.Get(ResourceIds.Iron);
            yield return ClickUiElement(
                startButton.gameObject,
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
        public IEnumerator IDEA0021_LevelTwoResearchStartsThroughRealTreeInput()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxFormalSaveRuntimeHost3D host = Object.FindObjectOfType<
                GrayboxFormalSaveRuntimeHost3D>();
            GrayboxOperationsController3D operations = Object.FindObjectOfType<
                GrayboxOperationsController3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            modifier.SetPopulation(200);
            modifier.SetResource(ResourceIds.Iron, 100);
            modifier.SetConstructionSpeed(DevelopmentConstructionSpeed.Fast100);

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit5);
            yield return MoveToInnerCell(
                Object.FindObjectOfType<GrayboxMobileCityController3D>(), 3, 2);
            yield return ClickWorld(MouseButton.Left);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (session.Instances.Count == 0 &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            GrayboxBuildingInstance3D station = session.Instances[0];
            while (station.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(station.Placement.Definition,
                Is.SameAs(BuildingCatalog.ResearchStation));
            yield return TapKey(Key.Escape);
            modifier.SetCityMode(CityMode.Fortress);

            string selectedFate = host.FateRuntime.Capture().SelectedId;
            string error;
            Assert.That(host.FateRuntime.TryPromoteToLevelTwo(out error),
                Is.True, error);
            if (selectedFate == FormalFateCatalog.RewindAnchorId)
                Assert.That(host.RewindAnchorMetadata.TrySetFateLevel(
                    2, out error), Is.True, error);
            else if (selectedFate == FormalFateCatalog.PocketUniverseId)
                Assert.That(host.PocketUniverseEffect.TrySetLevel(
                    2, out error), Is.True, error);
            else
                Assert.That(host.VoidDebtRuntime.TryRestore(
                    new FormalVoidDebtSnapshot(
                        2, 0d, 1ul, 1ul,
                        System.Array.Empty<FormalVoidDebtEntry>()), out error),
                    Is.True, error);
            Assert.That(host.Civilization.TryRestore(
                new FormalCivilizationAscensionSnapshot(
                    2, selectedFate, 2, true, 1ul), out error), Is.True, error);
            host.Sequence.Restore(
                (int)AdvancementSequenceStage.Continued, 0f);

            session.UnlockResearchForDevelopment(
                ResearchCatalog.PrecisionAssemblyId);
            ResearchDefinition alloy = ResearchCatalog.Find(
                CivilizationResearchAvailability.AlloyArmorId);
            foreach (ResourceAmount cost in alloy.Costs)
                modifier.SetResource(cost.ResourceId, cost.Amount);

            yield return TapKey(Key.T);
            yield return null;
            Text state = RequireText(
                "Research.Node." + alloy.Id.Value + ".State");
            Assert.That(state.text,
                Does.Not.Contain("本阶段未开放").And.Not.Contain("文明 Lv.2"));
            InputField search = RequireSceneObject("Research.Search")
                .GetComponent<InputField>();
            yield return ClickUiElement(search.gameObject, MouseButton.Left);
            for (var index = 0; index < alloy.Id.Value.Length; index++)
                InputSystem.QueueTextEvent(keyboard, alloy.Id.Value[index]);
            InputSystem.Update();
            yield return null;
            Assert.That(search.text, Is.EqualTo(alloy.Id.Value));
            yield return ClickUiElement(
                RequireSceneObject("Research.Node." + alloy.Id.Value),
                MouseButton.Left);
            Button start = RequireSceneObject("Research.Start")
                .GetComponent<Button>();
            Assert.That(start.interactable, Is.True);
            int alloyBefore = session.CityStorage.GetNetworkAmount(
                ResourceIds.Alloy);
            int stoneBefore = session.CityStorage.GetNetworkAmount(
                ResourceIds.Stone);
            yield return ClickUiElement(start.gameObject, MouseButton.Left);
            Assert.That(session.Research.Active.Id.Value,
                Is.EqualTo(alloy.Id.Value));
            Assert.That(session.CityStorage.GetNetworkAmount(ResourceIds.Alloy),
                Is.EqualTo(alloyBefore - 24));
            Assert.That(session.CityStorage.GetNetworkAmount(ResourceIds.Stone),
                Is.EqualTo(stoneBefore - 8));
            Assert.That(operations.Research.Tick(
                60f, CityMode.Fortress, false, true), Is.True);
            Assert.That(session.IsResearchCompleted(alloy.Id.Value), Is.True);
        }

        [UnityTest]
        public IEnumerator IDEA0021_ElixirUsesThroughRealCityInventoryInput()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxDefenseController3D defense =
                Object.FindObjectOfType<GrayboxDefenseController3D>();
            GrayboxOperationsView3D operationsView =
                Object.FindObjectOfType<GrayboxOperationsView3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            modifier.SetPopulation(200);
            modifier.SetResource(ResourceIds.Iron, 100);
            modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast100);

            yield return TapKey(Key.B);
            yield return TapKey(Key.Digit5);
            yield return MoveToInnerCell(
                Object.FindObjectOfType<GrayboxMobileCityController3D>(), 3, 2);
            yield return ClickWorld(MouseButton.Left);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (session.Instances.Count == 0 &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            GrayboxBuildingInstance3D station = session.Instances[0];
            while (station.State != GrayboxBuildingInstanceState.Completed &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            yield return TapKey(Key.Escape);
            yield return null;

            while (!defense.BuildingHealth.TryGetHealth(
                       station.StableInstanceId,
                       out _, out _, out _) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(defense.BuildingHealth.TryApplyDamage(
                station.StableInstanceId,
                150,
                out int applied,
                out _), Is.True);
            Assert.That(applied, Is.EqualTo(150));
            Assert.That(defense.BuildingHealth.TryGetHealth(
                station.StableInstanceId,
                out int healthBefore,
                out _,
                out _), Is.True);
            modifier.SetResource(ResourceIds.Elixir, 1);

            yield return TapKey(Key.E);
            GameObject elixirRow = RequireSceneObject(
                "Inventory.City." + ResourceIds.Elixir);
            yield return ScrollIntoView(
                RequireSceneObject("Inventory.City.Viewport")
                    .GetComponent<ScrollRect>(),
                elixirRow.GetComponent<RectTransform>());
            yield return ClickUiElement(elixirRow, MouseButton.Left);
            Assert.That(operationsView.IsInventoryOpen, Is.True,
                "Selecting city elixir must not navigate to the ledger.");
            Button use = RequireSceneObject("Inventory.City.UseSelected")
                .GetComponent<Button>();
            Assert.That(use.interactable, Is.True);
            yield return ClickUiElement(use.gameObject, MouseButton.Left);

            Assert.That(session.CityStorage.GetNetworkAmount(
                ResourceIds.Elixir), Is.Zero);
            Assert.That(defense.BuildingHealth.TryGetHealth(
                station.StableInstanceId,
                out int healthAfter,
                out _,
                out _), Is.True);
            Assert.That(healthAfter, Is.EqualTo(healthBefore + 100));
            Assert.That(RequireText("Inventory.TransferStatus").text,
                Does.Contain("灵丹").And.Contain("建筑 +100"));
        }

        [UnityTest]
        public IEnumerator IDEA0011_RealCraftingClicksMapOneFiveAndMaximum()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.UnlockResearch(
                ResearchCatalog.AutomatedMachineryId), Is.True);
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
            yield return WaitForUiLayout();

            GameObject recipe = RequireSceneObject(
                "Crafting.Recipe." + ResourceRecipeCatalog.FieldAlloyId);
            yield return ScrollIntoView(
                RequireSceneObject("Crafting.Recipes.Viewport")
                    .GetComponent<ScrollRect>(),
                recipe.GetComponent<RectTransform>());
            Assert.That(RequireSceneObject(
                    "Crafting.Recipe." +
                    ResourceRecipeCatalog.FieldAlloyId + ".Input." +
                    ResourceIds.Iron + ".Icon").GetComponent<Image>().sprite,
                Is.SameAs(ResolvePresentedResourceIcon(ResourceIds.Iron)));
            Assert.That(RequireSceneObject(
                    "Crafting.Recipe." +
                    ResourceRecipeCatalog.FieldAlloyId + ".Output." +
                    ResourceIds.Alloy + ".Icon").GetComponent<Image>().sprite,
                Is.SameAs(ResolvePresentedResourceIcon(ResourceIds.Alloy)));
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
        public IEnumerator IDEA0016_CraftingPageShowsScrollableFormalCatalogButQueuesOnlyManualRecipes()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.UnlockResearch(
                ResearchCatalog.AutomatedMachineryId), Is.True);

            yield return TapKey(Key.E);
            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Tab.Crafting"),
                MouseButton.Left);
            yield return WaitForUiLayout();

            foreach (ResourceRecipeDefinition definition in
                     ResourceRecipeCatalog.All)
            {
                GameObject card = RequireSceneObject(
                    "Crafting.Recipe." + definition.Id);
                Assert.That(card.activeInHierarchy, Is.True, definition.Id);
                Assert.That(card.GetComponent<Button>().interactable,
                    Is.EqualTo(
                        definition.Kind == ResourceRecipeKind.ManualCrafting &&
                        (definition.RequiredResearchIds.Count == 0 ||
                         definition.RequiredResearchIds.All(
                             session.IsResearchCompleted))),
                    definition.Id);
            }

            ScrollRect recipes = RequireSceneObject(
                    "Crafting.Recipes.Viewport")
                .GetComponent<ScrollRect>();
            Assert.That(recipes, Is.Not.Null);
            Assert.That(recipes.vertical, Is.True);
            Assert.That(recipes.content.rect.height,
                Is.GreaterThan(recipes.viewport.rect.height));
            float positionBefore = recipes.verticalNormalizedPosition;
            recipes.verticalNormalizedPosition = .75f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(recipes.verticalNormalizedPosition,
                Is.LessThan(positionBefore));

            ResourceRecipeDefinition machine = ResourceRecipeCatalog.All
                .First(value => value.Kind == ResourceRecipeKind.Machine);
            int queueBefore = Object.FindObjectOfType<
                    GrayboxOperationsController3D>()
                .Crafting.QueuedExecutionCount;
            Assert.That(RequireSceneObject(
                    "Crafting.Recipe." + machine.Id)
                .GetComponent<Button>().interactable, Is.False);
            Assert.That(Object.FindObjectOfType<
                    GrayboxOperationsController3D>()
                .Crafting.QueuedExecutionCount, Is.EqualTo(queueBefore));
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
            Assert.That(RequireSceneObject(
                    "Inventory.City." + ResourceIds.Iron + ".Icon")
                    .GetComponent<Image>().sprite,
                Is.SameAs(ResolvePresentedResourceIcon(ResourceIds.Iron)));

            yield return ClickUiElement(
                RequireSceneObject("InventoryCrafting.Tab.Backpack"),
                MouseButton.Left);
            GameObject slot0 = RequireSceneObject(
                "Inventory.Backpack.Slot.0");
            GameObject slot1 = RequireSceneObject(
                "Inventory.Backpack.Slot.1");
            GameObject slot2 = RequireSceneObject(
                "Inventory.Backpack.Slot.2");
            Assert.That(RequireSceneObject(
                    "Inventory.Backpack.Slot.0.Icon")
                    .GetComponent<Image>().sprite,
                Is.SameAs(ResolvePresentedResourceIcon(ResourceIds.Iron)));
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
        public IEnumerator IDEA0012_RealWarehouseClickShowsContentsAndRevalidatesFilter()
        {
            GrayboxBuildingSession3D session =
                Object.FindObjectOfType<GrayboxBuildingSession3D>();
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<GrayboxMobileCityController3D>();
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            GrayboxBuildingWorldView3D presentation =
                Object.FindObjectOfType<GrayboxBuildingWorldView3D>();
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 100), Is.True);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(world.Coordinates.TryWorldToCell(
                city.transform.position,
                out int cityX,
                out int cityY), Is.True);
            int buildingX = cityX + 2;
            int buildingY = cityY;
            GrayboxBuildingInstance3D warehouse = BeginGroundConstruction(
                session,
                presentation,
                BuildingCatalog.Warehouse,
                buildingX,
                buildingY,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            yield return null;
            Assert.That(session.CityStorage.AddToWarehouse(
                warehouse.StableInstanceId,
                ResourceIds.Iron,
                7), Is.EqualTo(7));
            Assert.That(session.CityStorage.AddToWarehouse(
                warehouse.StableInstanceId,
                ResourceIds.Stone,
                3), Is.EqualTo(3));
            yield return null;

            Assert.That(world.Coordinates.TryCellToWorld(
                buildingX,
                buildingY,
                1f,
                out Vector3 buildingWorld), Is.True);
            QueueMouse(Camera.main.WorldToScreenPoint(buildingWorld));
            yield return null;
            yield return ClickWorld(MouseButton.Left);

            GameObject panel = RequireSceneObject("WarehouseDetailPanel", true);
            Assert.That(panel.activeInHierarchy, Is.True);
            Assert.That(RequireText("WarehouseDetail.StableId").text,
                Is.EqualTo(warehouse.StableInstanceId));
            Assert.That(RequireText("WarehouseDetail.Capacity").text,
                Does.Contain("10/150").And.Contain("剩余 140"));
            Assert.That(RequireText(
                    "WarehouseDetail.Resource." + ResourceIds.Iron + ".Amount")
                    .text,
                Does.Contain("7"));
            Assert.That(RequireText(
                    "WarehouseDetail.Resource." + ResourceIds.Stone + ".Amount")
                    .text,
                Does.Contain("3"));

            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                Image ledgerIcon = RequireSceneObject(
                        "ResourceLedger.Item." + definition.Id + ".Icon",
                        true)
                    .GetComponent<Image>();
                Image warehouseIcon = RequireSceneObject(
                        "WarehouseDetail.Resource." + definition.Id + ".Icon",
                        true)
                    .GetComponent<Image>();
                Assert.That(ledgerIcon.sprite,
                    Is.SameAs(ResolvePresentedResourceIcon(definition.Id)),
                    definition.Id);
                Assert.That(warehouseIcon.sprite,
                    Is.SameAs(ledgerIcon.sprite),
                    definition.Id);
            }

            yield return ClickUiElement(
                RequireSceneObject(
                    "WarehouseDetail.Filter." + ResourceIds.Iron),
                MouseButton.Left);
            Assert.That(session.CityStorage.GetWarehouseFilter(
                warehouse.StableInstanceId), Is.Null);
            Assert.That(RequireText("WarehouseDetail.FilterStatus").text,
                Does.Contain("不能").Or.Contain("不兼容"));

            Assert.That(session.CityStorage.TrySpendFromWarehouse(
                warehouse.StableInstanceId,
                ResourceIds.Stone,
                3), Is.True);
            yield return ClickUiElement(
                RequireSceneObject(
                    "WarehouseDetail.Filter." + ResourceIds.Iron),
                MouseButton.Left);
            Assert.That(session.CityStorage.GetWarehouseFilter(
                warehouse.StableInstanceId), Is.EqualTo(ResourceIds.Iron));
            Assert.That(RequireText("WarehouseDetail.FilterStatus").text,
                Does.Contain("铁矿"));
        }

        [UnityTest]
        public IEnumerator IDEA0016_RealMachineRecipeClickSwitchesSmelterAndMarksCurrentRecipe()
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
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(production, Is.Not.Null);
            Assert.That(operations, Is.Not.Null);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Smelter.RequiredResearchId), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Stone, 100), Is.True);
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
            Assert.That(modifier.SetResource(ResourceIds.Stone, 0), Is.True);
            float stateDeadline = Time.realtimeSinceStartup + 1f;
            while (!production.Clock.Runtime.TryGetState(
                       smelter.StableInstanceId,
                       out _) &&
                   Time.realtimeSinceStartup < stateDeadline)
            {
                yield return null;
            }
            Assert.That(production.Clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState initialState), Is.True);
            Assert.That(initialState.Definition.Id,
                Is.EqualTo(FormalProductionDefinitionCatalog.Smelting.Id));

            Time.timeScale = 0f;
            Assert.That(operations.TryOpenProductionDetail(
                smelter.StableInstanceId), Is.True);
            yield return null;
            string rowName = RequireProductionRowName(
                smelter.StableInstanceId);
            string smeltingRecipeId =
                FormalProductionDefinitionCatalog.Smelting.Id;
            const string refinedStoneRecipeId =
                "core.production.refine-stone";
            string smeltingButtonName = rowName + ".Recipe." +
                smeltingRecipeId;
            string refinedStoneButtonName = rowName + ".Recipe." +
                refinedStoneRecipeId;
            Assert.That(RequireText(smeltingButtonName + ".Label").text,
                Does.Contain("当前配方")
                    .And.Contain("合金冶炼")
                    .And.Contain("6秒")
                    .And.Contain("铁矿")
                    .And.Contain("合金")
                    .And.Contain("科技："));
            Assert.That(RequireText(refinedStoneButtonName + ".Label").text,
                Does.Contain("精整石材")
                    .And.Contain("石料")
                    .And.Contain("精制石材"));

            yield return ClickUiElement(
                RequireSceneObject(refinedStoneButtonName),
                MouseButton.Left);
            Assert.That(production.Clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState selectedState), Is.True);
            Assert.That(selectedState.Definition.Id,
                Is.EqualTo(refinedStoneRecipeId));
            Assert.That(RequireText(refinedStoneButtonName + ".Label").text,
                Does.Contain("当前配方"));
            Assert.That(RequireText(rowName + ".AccessStatus").text,
                Does.Contain("已切换配方").And.Contain("精整石材"));

            Assert.That(production.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            operations.RefreshIfChanged();
            Assert.That(production.Clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState synchronizedState), Is.True);
            Assert.That(synchronizedState.Definition.Id,
                Is.EqualTo(refinedStoneRecipeId));
            Assert.That(RequireText(refinedStoneButtonName + ".Label").text,
                Does.Contain("当前配方"));
        }

        [UnityTest]
        public IEnumerator IDEA0016_RealMachineRecipeClickExplainsLockedResearch()
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
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Smelter.RequiredResearchId), Is.True);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Assembler.RequiredResearchId), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Stone, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 100), Is.True);
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
            Assert.That(smelter.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
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
            while (!production.Clock.Runtime.TryGetState(
                       assembler.StableInstanceId,
                       out _) &&
                   Time.realtimeSinceStartup < stateDeadline)
            {
                yield return null;
            }
            Assert.That(production.Clock.Runtime.TryGetState(
                assembler.StableInstanceId,
                out BuildingProductionState initialState), Is.True);

            Time.timeScale = 0f;
            Assert.That(operations.TryOpenProductionDetail(
                assembler.StableInstanceId), Is.True);
            yield return null;
            string rowName = RequireProductionRowName(
                assembler.StableInstanceId);
            const string lockedRecipeId =
                "technology.production.energy-cell";
            string lockedButtonName = rowName + ".Recipe." + lockedRecipeId;
            ResearchDefinition requiredResearch = ResearchCatalog.Find(
                "core.research.thermal-engineering");
            Assert.That(requiredResearch, Is.Not.Null);
            Button lockedButton = RequireSceneObject(lockedButtonName)
                .GetComponent<Button>();
            Assert.That(lockedButton, Is.Not.Null);
            Assert.That(lockedButton.interactable, Is.True,
                "Locked recipes remain clickable so the formal runtime can explain the denial.");
            Assert.That(RequireText(lockedButtonName + ".Label").text,
                Does.Contain("封装能量电池")
                    .And.Contain(requiredResearch.Name)
                    .And.Contain("未解锁"));

            yield return ClickUiElement(
                lockedButton.gameObject,
                MouseButton.Left);
            Assert.That(production.Clock.Runtime.TryGetState(
                assembler.StableInstanceId,
                out BuildingProductionState unchangedState), Is.True);
            Assert.That(unchangedState, Is.SameAs(initialState));
            Assert.That(unchangedState.Definition.Id,
                Is.EqualTo(FormalProductionDefinitionCatalog.Assembly.Id));
            Assert.That(RequireText(rowName + ".AccessStatus").text,
                Does.Contain("需要科技").And.Contain(requiredResearch.Name));
        }

        [UnityTest]
        public IEnumerator IDEA0016_OutOfLogisticsMultiInputRecipeSuppliesEveryChannelFromBackpack()
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
            GrayboxDeveloperModifier3D modifier = CreateModifier(session);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Smelter.RequiredResearchId), Is.True);
            Assert.That(modifier.UnlockResearch(
                BuildingCatalog.Assembler.RequiredResearchId), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Stone, 100), Is.True);
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 100), Is.True);
            Assert.That(modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(world.Coordinates.TryWorldToCell(
                city.transform.position,
                out int cityX,
                out int cityY), Is.True);

            GrayboxBuildingInstance3D smelter = BeginGroundConstruction(
                session,
                presentation,
                BuildingCatalog.Smelter,
                cityX + 4,
                cityY,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            Assert.That(smelter.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            GrayboxBuildingInstance3D assembler = BeginGroundConstruction(
                session,
                presentation,
                BuildingCatalog.Assembler,
                cityX + 1,
                cityY + 1,
                cityX,
                cityY);
            modifier.CompleteAllConstruction();
            Assert.That(modifier.SetResource(ResourceIds.Alloy, 0), Is.True);
            float stateDeadline = Time.realtimeSinceStartup + 1f;
            while (!production.Clock.Runtime.TryGetState(
                       assembler.StableInstanceId,
                       out _) &&
                   Time.realtimeSinceStartup < stateDeadline)
            {
                yield return null;
            }

            Time.timeScale = 0f;
            Assert.That(operations.TryOpenProductionDetail(
                assembler.StableInstanceId), Is.True);
            yield return null;
            string rowName = RequireProductionRowName(
                assembler.StableInstanceId);
            const string recipeId = "core.production.mix-coolant";
            yield return ClickUiElement(
                RequireSceneObject(rowName + ".Recipe." + recipeId),
                MouseButton.Left);
            Assert.That(production.Clock.Runtime.TryGetState(
                assembler.StableInstanceId,
                out BuildingProductionState selectedState), Is.True);
            Assert.That(selectedState.Definition.Id, Is.EqualTo(recipeId));

            Assert.That(modifier.SetCityMode(CityMode.Mobile), Is.True);
            Assert.That(production.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            operations.RefreshIfChanged();
            Assert.That(selectedState.IsLogisticsConnected, Is.False);
            Assert.That(operations.Backpack.Add(ResourceIds.Water, 4),
                Is.EqualTo(4));
            Assert.That(operations.Backpack.Add(ResourceIds.EnergyCrystal, 3),
                Is.EqualTo(3));
            operations.RefreshIfChanged();

            string waterButtonName = rowName + ".InputTransfer." +
                ResourceIds.Water;
            string crystalButtonName = rowName + ".InputTransfer." +
                ResourceIds.EnergyCrystal;
            Assert.That(RequireText(waterButtonName + ".Label").text,
                Does.Contain("补给").And.Contain("水").And.Contain("0/20"));
            Assert.That(RequireText(crystalButtonName + ".Label").text,
                Does.Contain("补给").And.Contain("能晶").And.Contain("0/20"));

            yield return ShiftClickUiElement(
                RequireSceneObject(waterButtonName));
            yield return ShiftClickUiElement(
                RequireSceneObject(crystalButtonName));
            Assert.That(selectedState.Input.Get(ResourceIds.Water),
                Is.EqualTo(4));
            Assert.That(selectedState.Input.Get(ResourceIds.EnergyCrystal),
                Is.EqualTo(3));
            Assert.That(BackpackAmount(operations, ResourceIds.Water), Is.Zero);
            Assert.That(BackpackAmount(
                operations,
                ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(RequireText(rowName + ".AccessStatus").text,
                Does.Contain("已转移 3"));
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
                productionRowName + ".InputTransfer." + ResourceIds.Iron);
            GameObject output = RequireSceneObject(
                productionRowName + ".OutputTransfer." + ResourceIds.Alloy);
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
            Assert.That(RequireSceneObject(
                    productionRowName + ".Input.Icon")
                    .GetComponent<Image>().sprite,
                Is.SameAs(ResolvePresentedResourceIcon(ResourceIds.Iron)));
            Assert.That(RequireSceneObject(
                    productionRowName + ".Output.Icon")
                    .GetComponent<Image>().sprite,
                Is.SameAs(ResolvePresentedResourceIcon(ResourceIds.Alloy)));

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

            Vector3 inaccessibleOffset = new Vector3(20f, 0f, 20f);
            leader.transform.position += inaccessibleOffset;
            city.transform.position += inaccessibleOffset;
            Assert.That(production.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            operations.RefreshIfChanged();
            state.Input.Set(ResourceIds.Iron, 0);
            state.Output.Set(ResourceIds.Alloy, 1);
            operations.Backpack.Add(ResourceIds.Iron, 3);
            int cityAlloyBeforeDenied =
                session.Inventory.Get(ResourceIds.Alloy);
            yield return ShiftClickUiElement(input);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
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

        private IEnumerator TapTextKey(Key key, char character)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.QueueTextEvent(keyboard, character);
            InputSystem.Update();
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator ScrollUiElement(
            RectTransform target,
            float delta)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(
                null,
                target.TransformPoint(target.rect.center));
            QueueMouse(position);
            yield return null;
            var state = new MouseState
            {
                position = position,
                scroll = new Vector2(0f, delta * 120f),
            };
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private static IEnumerator WaitForUiLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private static IEnumerator ScrollIntoView(
            ScrollRect scroll,
            RectTransform target)
        {
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.Not.Null);
            Assert.That(scroll.viewport, Is.Not.Null);
            Assert.That(target, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            float maximumOffset = Mathf.Max(
                0f,
                scroll.content.rect.height - scroll.viewport.rect.height);
            float targetOffset = Mathf.Clamp(
                -target.anchoredPosition.y -
                scroll.viewport.rect.height * .5f,
                0f,
                maximumOffset);
            scroll.verticalNormalizedPosition = maximumOffset <= 0f
                ? 1f
                : 1f - targetOffset / maximumOffset;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private IEnumerator DragPointer(
            Vector2 start,
            Vector2 end,
            MouseButton button)
        {
            QueueMouse(start);
            yield return null;
            QueueMouse(start, button);
            yield return null;
            QueueMouse(Vector2.Lerp(start, end, .5f), button);
            yield return null;
            QueueMouse(end, button);
            yield return null;
            QueueMouse(end);
            yield return null;
        }

        private static Vector2 FindViewportBlank(RectTransform viewport)
        {
            Vector2 center = RectTransformUtility.WorldToScreenPoint(
                null,
                viewport.TransformPoint(viewport.rect.center));
            var results = new List<RaycastResult>();
            for (var y = -200f; y <= 200f; y += 50f)
            {
                for (var x = -300f; x <= 300f; x += 50f)
                {
                    Vector2 candidate = center + new Vector2(x, y);
                    results.Clear();
                    EventSystem.current.RaycastAll(
                        new PointerEventData(EventSystem.current)
                        {
                            position = candidate,
                        },
                        results);
                    if (results.Count > 0 &&
                        results[0].gameObject == viewport.gameObject)
                    {
                        return candidate;
                    }
                }
            }
            Assert.Fail("No visible blank point was found in research viewport.");
            return center;
        }

        private IEnumerator ShiftClickUiElement(GameObject target)
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

            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard.leftShiftKey.isPressed, Is.True);
            QueueMouse(position, MouseButton.Left);
            yield return null;
            QueueMouse(position);
            yield return null;

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

        private static Sprite ResolvePresentedResourceIcon(string resourceId)
        {
            Image icon = RequireSceneObject(
                    "ResourceStatus.Item." + resourceId + ".Icon",
                    true)
                .GetComponent<Image>();
            Assert.That(icon, Is.Not.Null, resourceId);
            Assert.That(icon.sprite, Is.Not.Null, resourceId);
            return icon.sprite;
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
