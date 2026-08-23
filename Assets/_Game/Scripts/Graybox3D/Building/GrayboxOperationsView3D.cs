using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxInventoryTab3D
    {
        Inventory,
        Backpack,
        Crafting,
        City = Inventory,
    }

    public sealed class GrayboxOperationsView3D : MonoBehaviour
    {
        private static readonly Color PanelColor =
            new Color(.07f, .09f, .11f, .96f);
        private static readonly Color ButtonColor =
            new Color(.18f, .25f, .31f, 1f);
        private static readonly Color SelectedColor =
            new Color(.22f, .48f, .55f, 1f);
        private static readonly Color DisabledColor =
            new Color(.22f, .22f, .22f, .92f);

        [SerializeField] private Canvas canvas;
        [SerializeField] private ResourceIconCatalog3D resourceIconCatalog;

        private RectTransform uiRoot;
        private RectTransform resourceStatusBar;
        private RectTransform resourceLedgerPanel;
        private RectTransform inventoryCraftingPanel;
        private RectTransform researchTreePanel;
        private GrayboxResearchTreeView3D researchTreeView;
        private RectTransform resourceTooltipRoot;
        private Text resourceTooltipText;
        private RectTransform cityPage;
        private RectTransform backpackPage;
        private RectTransform craftingPage;
        private RectTransform productionStateContent;
        private RectTransform productionStatePanel;
        private RectTransform warehouseDetailPanel;
        private Text warehouseStableId;
        private Text warehouseLogistics;
        private Text warehouseCapacity;
        private Text warehouseFilterStatus;
        private Text craftQueueCount;
        private Text craftQueueProgress;
        private Text craftQueueReason;
        private Button craftCancelButton;
        private Text inventoryTransferStatus;
        private GrayboxInventoryTab3D inventoryTab;
        private int selectedBackpackSlot = -1;
        private int productionStateCount;
        private string hoveredResourceId;

        private readonly Dictionary<string, ResourceRow> statusRows =
            new Dictionary<string, ResourceRow>(StringComparer.Ordinal);
        private readonly Dictionary<string, ResourceRow> ledgerRows =
            new Dictionary<string, ResourceRow>(StringComparer.Ordinal);
        private readonly Dictionary<string, Text> cityRows =
            new Dictionary<string, Text>(StringComparer.Ordinal);
        private readonly Dictionary<string, Text> warehouseResourceRows =
            new Dictionary<string, Text>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> warehouseFilterButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> recipeButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Text[] backpackSlotLabels = new Text[30];
        private readonly Button[] backpackSlotButtons = new Button[30];
        private readonly Image[] backpackSlotIcons = new Image[30];
        private readonly List<ProductionRow> productionRows =
            new List<ProductionRow>();

        public event Action<string> ResourceClicked;
        public event Action<string> CityResourceShiftClicked;
        public event Action<int, int> BackpackSlotClicked;
        public event Action<string, int> CraftRequested;
        public event Action CraftCancelRequested;
        public event Action<string, bool> ResourceHoverChanged;
        public event Action<string, bool, bool>
            ProductionCacheTransferRequested;
        public event Action<string> ProductionPauseRequested;
        public event Action<string> ResearchSelected;
        public event Action ResearchStartRequested;
        public event Action ResearchCancelRequested;
        public event Action<string, string> WarehouseFilterRequested;
        public event Action InventoryCloseRequested;

        public bool IsInventoryOpen =>
            inventoryCraftingPanel != null &&
            inventoryCraftingPanel.gameObject.activeSelf;

        public bool IsResearchOpen =>
            researchTreePanel != null &&
            researchTreePanel.gameObject.activeSelf;

        public bool IsLedgerOpen =>
            resourceLedgerPanel != null &&
            resourceLedgerPanel.gameObject.activeSelf;

        private void Awake()
        {
            ResolveCanvas();
            TryBuildUi();
        }

        private void OnEnable()
        {
            ResolveCanvas();
            TryBuildUi();
        }

        private void OnDestroy()
        {
            DestroyUi();
            ResourceClicked = null;
            CityResourceShiftClicked = null;
            BackpackSlotClicked = null;
            CraftRequested = null;
            CraftCancelRequested = null;
            ResourceHoverChanged = null;
            ProductionCacheTransferRequested = null;
            ProductionPauseRequested = null;
            ResearchSelected = null;
            ResearchStartRequested = null;
            ResearchCancelRequested = null;
            WarehouseFilterRequested = null;
            InventoryCloseRequested = null;
            canvas = null;
        }

        public void Configure(Canvas value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(canvas, value) && uiRoot != null) return;
            DestroyUi();
            canvas = value;
            TryBuildUi();
        }

        public void ConfigureResourceIcons(ResourceIconCatalog3D catalog)
        {
            if (ReferenceEquals(resourceIconCatalog, catalog) && uiRoot != null)
                return;
            DestroyUi();
            resourceIconCatalog = catalog;
            TryBuildUi();
        }

        public void SetInventoryOpen(bool open)
        {
            TryBuildUi();
            if (inventoryCraftingPanel != null)
                inventoryCraftingPanel.gameObject.SetActive(open);
        }

        public void SetResearchOpen(bool open)
        {
            TryBuildUi();
            if (researchTreePanel != null)
                researchTreePanel.gameObject.SetActive(open);
            if (!open)
                researchTreeView?.NotifyClosed();
        }

        public bool HasResearchTextInputFocus =>
            researchTreeView != null && researchTreeView.HasTextInputFocus;

        public bool ConsumeFocusedResearchEscape()
        {
            return researchTreeView != null &&
                researchTreeView.ConsumeFocusedEscape();
        }

        public void FitResearchTree()
        {
            researchTreeView?.FitAll();
        }

        public void FocusResearchTreeOnOpen(string researchId)
        {
            researchTreeView?.NotifyOpened(researchId);
        }

        public void SetLedgerOpen(bool open)
        {
            TryBuildUi();
            if (resourceLedgerPanel != null)
                resourceLedgerPanel.gameObject.SetActive(open);
        }

        public void SetInventoryTab(GrayboxInventoryTab3D tab)
        {
            inventoryTab = tab;
            if (cityPage != null)
                cityPage.gameObject.SetActive(
                    tab == GrayboxInventoryTab3D.Inventory);
            if (backpackPage != null)
                backpackPage.gameObject.SetActive(
                    tab == GrayboxInventoryTab3D.Backpack);
            if (craftingPage != null)
                craftingPage.gameObject.SetActive(
                    tab == GrayboxInventoryTab3D.Crafting);
        }

        public void SetResource(
            string resourceId,
            bool visible,
            int amount,
            int capacity,
            float netFlow)
        {
            TryBuildUi();
            if (!statusRows.TryGetValue(resourceId, out ResourceRow row))
                return;
            if (row.HasValue && row.Visible == visible &&
                row.AmountValue == amount && row.CapacityValue == capacity &&
                Mathf.Approximately(row.NetFlowValue, netFlow))
                return;
            row.HasValue = true;
            row.Visible = visible;
            row.AmountValue = amount;
            row.CapacityValue = capacity;
            row.NetFlowValue = netFlow;
            row.Root.gameObject.SetActive(visible);
            row.Amount.text = amount.ToString();
            row.Capacity.text = CapacityText(amount, capacity);
            row.NetFlow.text = FormatFlow(netFlow);
        }

        public void SetLedgerResource(
            string resourceId,
            int amount,
            int capacity,
            float netFlow)
        {
            TryBuildUi();
            if (!ledgerRows.TryGetValue(resourceId, out ResourceRow row))
                return;
            if (row.HasValue && row.AmountValue == amount &&
                row.CapacityValue == capacity &&
                Mathf.Approximately(row.NetFlowValue, netFlow))
                return;
            row.HasValue = true;
            row.Visible = true;
            row.AmountValue = amount;
            row.CapacityValue = capacity;
            row.NetFlowValue = netFlow;
            row.Root.gameObject.SetActive(true);
            row.Amount.text = amount.ToString();
            row.Capacity.text = CapacityText(amount, capacity);
            row.NetFlow.text = FormatFlow(netFlow);
        }

        public void SetCityResource(string resourceId, int amount)
        {
            TryBuildUi();
            if (cityRows.TryGetValue(resourceId, out Text label))
                label.text = ResourceName(resourceId) + "  " + amount;
        }

        public void SetInventoryTransferStatus(string status)
        {
            TryBuildUi();
            if (inventoryTransferStatus != null)
                inventoryTransferStatus.text = status ?? string.Empty;
        }

        public void SetBackpackSlot(int index, string resourceId, int amount)
        {
            TryBuildUi();
            if (index < 0 || index >= backpackSlotLabels.Length) return;
            Text label = backpackSlotLabels[index];
            if (label == null) return;
            label.text = string.IsNullOrWhiteSpace(resourceId) || amount <= 0
                ? "空"
                : ResourceName(resourceId) + "\n" + amount;
            Image icon = backpackSlotIcons[index];
            if (icon != null)
            {
                bool visible = !string.IsNullOrWhiteSpace(resourceId) &&
                    amount > 0;
                icon.sprite = visible ? ResolveIcon(resourceId) : null;
                icon.gameObject.SetActive(visible && icon.sprite != null);
            }
        }

        public void SetBackpackSelection(
            int selectedIndex,
            int oneByOneAmount,
            bool placesOne)
        {
            TryBuildUi();
            selectedBackpackSlot = selectedIndex >= 0 &&
                selectedIndex < backpackSlotButtons.Length
                ? selectedIndex
                : -1;
            for (var index = 0;
                 index < backpackSlotButtons.Length;
                 index++)
            {
                Button button = backpackSlotButtons[index];
                if (button != null)
                    button.image.color = index == selectedBackpackSlot
                        ? SelectedColor
                        : ButtonColor;
                Text label = backpackSlotLabels[index];
                if (label != null)
                    label.fontSize = index == selectedBackpackSlot &&
                        placesOne
                        ? 10
                        : 14;
            }
            if (placesOne && selectedBackpackSlot >= 0 &&
                oneByOneAmount > 0 &&
                backpackSlotLabels[selectedBackpackSlot] != null)
            {
                backpackSlotLabels[selectedBackpackSlot].text +=
                    "\n待逐个放置 " + oneByOneAmount;
            }
        }

        public void SetProductionState(
            int index,
            string stableId,
            string title,
            float progress,
            string status,
            string input,
            string output,
            bool visible)
        {
            TryBuildUi();
            if (index < 0 || productionStateContent == null) return;
            ProductionRow row = EnsureProductionRow(index);
            row.StableInstanceId = stableId;
            row.StableId.text = stableId ?? string.Empty;
            row.Title.text = title ?? string.Empty;
            row.Progress.text = "进度 " +
                (Mathf.Clamp01(progress) * 100f).ToString(
                    "0.#",
                    CultureInfo.InvariantCulture) + "%";
            row.Status.text = status ?? string.Empty;
            row.Input.text = input ?? "输入：";
            row.Output.text = output ?? "输出：";
            row.Root.gameObject.SetActive(visible);
        }

        public void SetProductionAccessStatus(int index, string status)
        {
            TryBuildUi();
            if (index < 0 || productionStateContent == null) return;
            EnsureProductionRow(index).AccessStatus.text = status ?? string.Empty;
        }

        public void SetProductionResourceIcons(
            int index,
            string inputResourceId,
            string outputResourceId)
        {
            TryBuildUi();
            if (index < 0 || productionStateContent == null) return;
            ProductionRow row = EnsureProductionRow(index);
            SetIcon(row.InputIcon, inputResourceId);
            SetIcon(row.OutputIcon, outputResourceId);
        }

        public void SetWarehouseDetail(
            WarehouseStorageSnapshot snapshot,
            string feedback,
            bool visible)
        {
            TryBuildUi();
            if (warehouseDetailPanel == null) return;
            warehouseDetailPanel.gameObject.SetActive(visible);
            if (productionStatePanel != null)
                productionStatePanel.gameObject.SetActive(!visible);
            if (!visible || snapshot == null) return;

            warehouseStableId.text = snapshot.StableInstanceId;
            warehouseLogistics.text = snapshot.IsConnected
                ? "物流：已连接城市库存"
                : "物流：脱离物流（内容保留）";
            warehouseCapacity.text = "共享容量：" + snapshot.TotalAmount +
                "/" + snapshot.Capacity + " · 剩余 " + snapshot.FreeSpace;
            warehouseFilterStatus.text = "当前过滤：" +
                (string.IsNullOrWhiteSpace(snapshot.FilterResourceId)
                    ? "不限资源"
                    : ResourceName(snapshot.FilterResourceId)) +
                (string.IsNullOrWhiteSpace(feedback)
                    ? string.Empty
                    : " · " + feedback);

            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                if (warehouseResourceRows.TryGetValue(
                        definition.Id,
                        out Text row))
                {
                    row.text = definition.ChineseName + "  " +
                        snapshot.Get(definition.Id);
                }
                if (warehouseFilterButtons.TryGetValue(
                        definition.Id,
                        out Button button))
                {
                    button.image.color = string.Equals(
                            snapshot.FilterResourceId,
                            definition.Id,
                            StringComparison.Ordinal)
                        ? SelectedColor
                        : ButtonColor;
                }
            }
            if (warehouseFilterButtons.TryGetValue(string.Empty, out Button any))
            {
                any.image.color = string.IsNullOrWhiteSpace(
                        snapshot.FilterResourceId)
                    ? SelectedColor
                    : ButtonColor;
            }
        }

        public void SetProductionPaused(
            int index,
            bool paused,
            bool interactable)
        {
            TryBuildUi();
            if (index < 0 || productionStateContent == null) return;
            ProductionRow row = EnsureProductionRow(index);
            row.Pause.interactable = interactable;
            Text label = row.Pause.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = paused ? "恢复运行" : "暂停运行";
        }

        public void SetProductionStateCount(int count)
        {
            TryBuildUi();
            productionStateCount = Math.Max(0, count);
            if (productionStateContent == null) return;
            for (var index = 0; index < productionStateCount; index++)
                EnsureProductionRow(index).Root.gameObject.SetActive(true);
            for (var index = productionStateCount;
                 index < productionRows.Count;
                 index++)
                productionRows[index].Root.gameObject.SetActive(false);
        }

        public void SetResourceTooltip(string text, bool visible)
        {
            TryBuildUi();
            if (resourceTooltipRoot == null) return;
            if (resourceTooltipText != null)
                resourceTooltipText.text = text ?? string.Empty;
            resourceTooltipRoot.gameObject.SetActive(visible);
            if (visible)
                resourceTooltipRoot.SetAsLastSibling();
        }

        public void SetCraftQueue(int count, float progress, string reason)
        {
            TryBuildUi();
            if (craftQueueCount != null)
                craftQueueCount.text = Math.Max(0, count) + "/" +
                    CraftingQueueModel.MaximumQueuedExecutions;
            if (craftQueueProgress != null)
                craftQueueProgress.text =
                    "进度 " +
                    (Mathf.Clamp01(progress) * 100f).ToString("0.#") + "%";
            if (craftQueueReason != null)
                craftQueueReason.text = reason ?? string.Empty;
            if (craftCancelButton != null)
                craftCancelButton.interactable = count > 0;
        }

        public void SetCraftRecipe(
            string recipeId,
            string stateText,
            bool interactable)
        {
            TryBuildUi();
            if (!recipeButtons.TryGetValue(recipeId, out Button button))
                return;
            button.interactable = interactable;
            button.image.color = interactable ? ButtonColor : DisabledColor;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                string title = RecipeTitle(recipeId);
                label.text = RecipeName(recipeId) +
                    (string.IsNullOrEmpty(stateText) ||
                     string.Equals(
                         stateText,
                         title,
                         StringComparison.Ordinal)
                        ? string.Empty
                        : "\n" + stateText);
            }
        }

        public void SetResearchNode(
            ResearchDefinition definition,
            string stateText,
            bool selected)
        {
            if (definition == null) return;
            TryBuildUi();
            researchTreeView?.SetNode(definition, stateText, selected);
        }

        public void SetResearchActive(
            string name,
            string progressText,
            bool visible)
        {
            TryBuildUi();
            researchTreeView?.SetActiveResearch(
                name,
                progressText,
                visible);
        }

        public void SetResearchActive(
            string name,
            string progressText,
            bool visible,
            string researchId)
        {
            TryBuildUi();
            researchTreeView?.SetActiveResearch(
                name,
                progressText,
                visible,
                researchId);
        }

        public void SetResearchStartInteractable(bool interactable)
        {
            researchTreeView?.SetStartInteractable(interactable);
        }

        public void SetResearchCancelInteractable(bool interactable)
        {
            researchTreeView?.SetCancelInteractable(interactable);
        }

        private void ResolveCanvas()
        {
            if (canvas != null) return;
            canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
        }

        private void TryBuildUi()
        {
            if (uiRoot != null || canvas == null) return;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            RetireStaleRoots();
            BuildUi();
        }

        private void BuildUi()
        {
            uiRoot = CreateRect(canvas.transform, "ProductionObservabilityUi.Root");
            Stretch(uiRoot);
            BuildResourceBar();
            BuildLedger();
            BuildInventoryCrafting();
            BuildResearchTree();
            SetInventoryTab(inventoryTab);
            SetInventoryOpen(false);
            SetResearchOpen(false);
            SetLedgerOpen(false);
        }

        private void BuildResourceBar()
        {
            resourceStatusBar = CreateFixedPanel(
                uiRoot,
                "ResourceStatusBar",
                new Vector2(.5f, 1f),
                new Vector2(0f, -37f),
                new Vector2(1100f, 58f));
            RectTransform background = CreateRect(
                resourceStatusBar,
                "ResourceStatus.Background");
            Stretch(background);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = PanelColor;
            backgroundImage.raycastTarget = false;

            RectTransform items = CreateRect(resourceStatusBar, "Items");
            Stretch(items);
            var layout = items.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                bool isBase = ContainsResource(
                    ResourceDefinitionCatalog.BaseHudResourceIds,
                    definition.Id);
                string resourceId = definition.Id;
                ResourceRow row = CreateResourceRow(
                    items,
                    "ResourceStatus.Item." + resourceId,
                    definition.ChineseName,
                    resourceId,
                    () => ResourceClicked?.Invoke(resourceId),
                    compact: true);
                var trigger = row.Root.gameObject.AddComponent<EventTrigger>();
                AddTrigger(
                    trigger,
                    EventTriggerType.PointerEnter,
                    _ => HandleResourceHover(resourceId, true));
                AddTrigger(
                    trigger,
                    EventTriggerType.PointerExit,
                    _ => HandleResourceHover(resourceId, false));
                row.Root.gameObject.SetActive(isBase);
                statusRows.Add(resourceId, row);
            }

            resourceTooltipRoot = CreateFixedPanel(
                uiRoot,
                "ResourceStatus.Tooltip",
                new Vector2(.5f, 1f),
                new Vector2(0f, -112f),
                new Vector2(620f, 86f));
            resourceTooltipText = CreateLabel(
                resourceTooltipRoot,
                "ResourceStatus.Tooltip.Text",
                string.Empty,
                13);
            resourceTooltipText.alignment = TextAnchor.MiddleLeft;
            resourceTooltipText.rectTransform.offsetMin =
                new Vector2(12f, 8f);
            resourceTooltipText.rectTransform.offsetMax =
                new Vector2(-12f, -8f);
            resourceTooltipRoot.gameObject.SetActive(false);
        }

        private void BuildLedger()
        {
            resourceLedgerPanel = CreateFixedPanel(
                uiRoot,
                "FullResourceLedgerPanel",
                new Vector2(.5f, .5f),
                Vector2.zero,
                new Vector2(1240f, 690f));
            AddVerticalLayout(resourceLedgerPanel, 10, 8f);
            Text title = CreateLabel(
                resourceLedgerPanel,
                "ResourceLedger.Title",
                "完整资源账本",
                20);
            SetLayout(title.rectTransform, 0f, 34f, 1f);

            RectTransform body = CreateRect(
                resourceLedgerPanel,
                "ResourceLedger.Body");
            SetLayout(body, 0f, 580f, 1f);
            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 10f;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;

            RectTransform grid = CreateRect(body, "ResourceLedger.Items");
            SetLayout(grid, 748f, 0f, 0f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(238f, 68f);
            layout.spacing = new Vector2(6f, 6f);
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                ResourceRow row = CreateResourceRow(
                    grid,
                    "ResourceLedger.Item." + definition.Id,
                    definition.ChineseName,
                    definition.Id,
                    callback: null,
                    compact: false);
                ledgerRows.Add(definition.Id, row);
            }

            RectTransform production = CreateRect(
                body,
                "ProductionState.Panel");
            productionStatePanel = production;
            SetLayout(production, 0f, 0f, 1f);
            Image productionBackground =
                production.gameObject.AddComponent<Image>();
            productionBackground.color = new Color(.09f, .12f, .14f, .94f);
            productionBackground.raycastTarget = false;
            var productionLayout =
                production.gameObject.AddComponent<VerticalLayoutGroup>();
            productionLayout.padding = new RectOffset(8, 8, 8, 8);
            productionLayout.spacing = 6f;
            productionLayout.childForceExpandWidth = true;
            productionLayout.childForceExpandHeight = false;
            Text productionTitle = CreateLabel(
                production,
                "ProductionState.Title",
                "生产建筑运行状态",
                16);
            SetLayout(productionTitle.rectTransform, 0f, 30f, 1f);
            RectTransform viewport = CreateRect(
                production,
                "ProductionState.Viewport");
            SetLayout(viewport, 0f, 524f, 1f);
            Image viewportTarget = viewport.gameObject.AddComponent<Image>();
            viewportTarget.color = new Color(0f, 0f, 0f, .01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            productionStateContent = CreateRect(
                viewport,
                "ProductionState.Content");
            productionStateContent.anchorMin = new Vector2(0f, 1f);
            productionStateContent.anchorMax = new Vector2(1f, 1f);
            productionStateContent.pivot = new Vector2(.5f, 1f);
            productionStateContent.anchoredPosition = Vector2.zero;
            productionStateContent.sizeDelta = Vector2.zero;
            var stateLayout = productionStateContent.gameObject
                .AddComponent<VerticalLayoutGroup>();
            stateLayout.spacing = 6f;
            stateLayout.childForceExpandWidth = true;
            stateLayout.childForceExpandHeight = false;
            var contentSize = productionStateContent.gameObject
                .AddComponent<ContentSizeFitter>();
            contentSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = productionStateContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            BuildWarehouseDetail(body);

            CreateButton(
                resourceLedgerPanel,
                "ResourceLedger.Close",
                "关闭",
                () => SetLedgerOpen(false));
        }

        private void BuildWarehouseDetail(Transform parent)
        {
            warehouseDetailPanel = CreateRect(parent, "WarehouseDetailPanel");
            SetLayout(warehouseDetailPanel, 0f, 0f, 1f);
            Image background =
                warehouseDetailPanel.gameObject.AddComponent<Image>();
            background.color = new Color(.09f, .12f, .14f, .94f);
            background.raycastTarget = false;
            AddVerticalLayout(warehouseDetailPanel, 8, 4f);

            Text title = CreateLabel(
                warehouseDetailPanel,
                "WarehouseDetail.Title",
                "仓库真实库存",
                16);
            SetLayout(title.rectTransform, 0f, 28f, 1f);
            warehouseStableId = CreateLabel(
                warehouseDetailPanel,
                "WarehouseDetail.StableId",
                string.Empty,
                10);
            warehouseStableId.alignment = TextAnchor.MiddleLeft;
            SetLayout(warehouseStableId.rectTransform, 0f, 20f, 1f);
            warehouseLogistics = CreateLabel(
                warehouseDetailPanel,
                "WarehouseDetail.Logistics",
                string.Empty,
                12);
            warehouseLogistics.alignment = TextAnchor.MiddleLeft;
            SetLayout(warehouseLogistics.rectTransform, 0f, 22f, 1f);
            warehouseCapacity = CreateLabel(
                warehouseDetailPanel,
                "WarehouseDetail.Capacity",
                string.Empty,
                12);
            warehouseCapacity.alignment = TextAnchor.MiddleLeft;
            SetLayout(warehouseCapacity.rectTransform, 0f, 22f, 1f);

            RectTransform contents = CreateRect(
                warehouseDetailPanel,
                "WarehouseDetail.Resources");
            SetLayout(contents, 0f, 210f, 1f);
            var contentsLayout =
                contents.gameObject.AddComponent<GridLayoutGroup>();
            contentsLayout.cellSize = new Vector2(142f, 36f);
            contentsLayout.spacing = new Vector2(5f, 5f);
            contentsLayout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            contentsLayout.constraintCount = 3;
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                RectTransform row = CreateRect(
                    contents,
                    "WarehouseDetail.Resource." + definition.Id);
                Image rowBackground = row.gameObject.AddComponent<Image>();
                rowBackground.color = ButtonColor;
                rowBackground.raycastTarget = false;
                CreateOverlayResourceIcon(
                    row,
                    "WarehouseDetail.Resource." + definition.Id + ".Icon",
                    definition.Id,
                    28f,
                    new Vector2(18f, 0f));
                Text label = CreateLabel(
                    row,
                    "WarehouseDetail.Resource." + definition.Id + ".Amount",
                    definition.ChineseName + "  0",
                    10);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(38f, 0f);
                warehouseResourceRows.Add(definition.Id, label);
            }

            Text filterTitle = CreateLabel(
                warehouseDetailPanel,
                "WarehouseDetail.Filter",
                "入库过滤（共享容量仍为 150）",
                12);
            filterTitle.alignment = TextAnchor.MiddleLeft;
            SetLayout(filterTitle.rectTransform, 0f, 20f, 1f);
            RectTransform filters = CreateRect(
                warehouseDetailPanel,
                "WarehouseDetail.Filter.Options");
            SetLayout(filters, 0f, 156f, 1f);
            var filterLayout = filters.gameObject.AddComponent<GridLayoutGroup>();
            filterLayout.cellSize = new Vector2(104f, 34f);
            filterLayout.spacing = new Vector2(5f, 5f);
            filterLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            filterLayout.constraintCount = 4;
            Button any = CreateButton(
                filters,
                "WarehouseDetail.Filter.Any",
                "不限",
                () => RequestWarehouseFilter(null));
            warehouseFilterButtons.Add(string.Empty, any);
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                string resourceId = definition.Id;
                Button button = CreateButton(
                    filters,
                    "WarehouseDetail.Filter." + resourceId,
                    definition.ChineseName,
                    () => RequestWarehouseFilter(resourceId));
                CreateOverlayResourceIcon(
                    button.transform,
                    "WarehouseDetail.Filter." + resourceId + ".Icon",
                    resourceId,
                    22f,
                    new Vector2(14f, 0f));
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.rectTransform.offsetMin = new Vector2(28f, 0f);
                warehouseFilterButtons.Add(resourceId, button);
            }
            warehouseFilterStatus = CreateLabel(
                warehouseDetailPanel,
                "WarehouseDetail.FilterStatus",
                string.Empty,
                11);
            warehouseFilterStatus.alignment = TextAnchor.MiddleLeft;
            warehouseFilterStatus.color = new Color(.95f, .78f, .42f, 1f);
            SetLayout(warehouseFilterStatus.rectTransform, 0f, 24f, 1f);
            warehouseDetailPanel.gameObject.SetActive(false);
        }

        private void RequestWarehouseFilter(string resourceId)
        {
            string stableId = warehouseStableId == null
                ? null
                : warehouseStableId.text;
            if (!string.IsNullOrWhiteSpace(stableId))
                WarehouseFilterRequested?.Invoke(stableId, resourceId);
        }

        private void BuildInventoryCrafting()
        {
            inventoryCraftingPanel = CreateFixedPanel(
                uiRoot,
                "InventoryCraftingPanel",
                new Vector2(.5f, .5f),
                Vector2.zero,
                new Vector2(920f, 610f));
            AddVerticalLayout(inventoryCraftingPanel, 12, 8f);

            Text title = CreateLabel(
                inventoryCraftingPanel,
                "InventoryCrafting.Title",
                "库存与应急合成",
                20);
            SetLayout(title.rectTransform, 0f, 36f, 1f);

            RectTransform tabs = CreateRect(
                inventoryCraftingPanel,
                "InventoryCrafting.Tabs");
            SetLayout(tabs, 0f, 38f, 1f);
            var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 6f;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            CreateButton(
                tabs,
                "InventoryCrafting.Tab.City",
                "城市库存",
                () => SetInventoryTab(GrayboxInventoryTab3D.Inventory));
            CreateButton(
                tabs,
                "InventoryCrafting.Tab.Backpack",
                "个人背包",
                () => SetInventoryTab(GrayboxInventoryTab3D.Backpack));
            CreateButton(
                tabs,
                "InventoryCrafting.Tab.Crafting",
                "应急合成",
                () => SetInventoryTab(GrayboxInventoryTab3D.Crafting));

            RectTransform pages = CreateRect(
                inventoryCraftingPanel,
                "InventoryCrafting.Pages");
            SetLayout(pages, 0f, 470f, 1f);
            cityPage = CreatePage(pages, "InventoryCrafting.Page.City");
            backpackPage = CreatePage(
                pages,
                "InventoryCrafting.Page.Backpack");
            craftingPage = CreatePage(
                pages,
                "InventoryCrafting.Page.Crafting");
            BuildCityPage();
            BuildBackpackPage();
            BuildCraftingPage();

            inventoryTransferStatus = CreateLabel(
                inventoryCraftingPanel,
                "Inventory.TransferStatus",
                string.Empty,
                12);
            inventoryTransferStatus.alignment = TextAnchor.MiddleLeft;
            inventoryTransferStatus.color =
                new Color(.95f, .78f, .42f, 1f);
            SetLayout(inventoryTransferStatus.rectTransform, 0f, 24f, 1f);

            CreateButton(
                inventoryCraftingPanel,
                "InventoryCrafting.Close",
                "关闭",
                () => InventoryCloseRequested?.Invoke());
        }

        private void BuildCityPage()
        {
            var layout = cityPage.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(280f, 52f);
            layout.spacing = new Vector2(8f, 8f);
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                string resourceId = definition.Id;
                Button button = CreatePointerButton(
                    cityPage,
                    "Inventory.City." + resourceId,
                    definition.ChineseName + "  0",
                    data => HandleCityResourceClick(resourceId, data));
                CreateOverlayResourceIcon(
                    button.transform,
                    "Inventory.City." + resourceId + ".Icon",
                    resourceId,
                    34f,
                    new Vector2(24f, 0f));
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.rectTransform.offsetMin = new Vector2(48f, 0f);
                cityRows.Add(
                    resourceId,
                    label);
            }
        }

        private void BuildBackpackPage()
        {
            var layout = backpackPage.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(80f, 68f);
            layout.spacing = new Vector2(6f, 6f);
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 10;
            for (var index = 0; index < backpackSlotLabels.Length; index++)
            {
                int capturedIndex = index;
                Button button = CreatePointerButton(
                    backpackPage,
                    "Inventory.Backpack.Slot." + capturedIndex,
                    "空",
                    data => HandleBackpackSlotClick(capturedIndex, data));
                backpackSlotButtons[capturedIndex] = button;
                backpackSlotLabels[capturedIndex] =
                    button.GetComponentInChildren<Text>(true);
                backpackSlotIcons[capturedIndex] = CreateOverlayResourceIcon(
                    button.transform,
                    "Inventory.Backpack.Slot." + capturedIndex + ".Icon",
                    null,
                    30f,
                    new Vector2(40f, 14f));
                backpackSlotIcons[capturedIndex].gameObject.SetActive(false);
                backpackSlotLabels[capturedIndex].rectTransform.offsetMin =
                    new Vector2(0f, 0f);
                backpackSlotLabels[capturedIndex].rectTransform.offsetMax =
                    new Vector2(0f, -24f);
            }
        }

        private void BuildCraftingPage()
        {
            AddVerticalLayout(craftingPage, 10, 8f);
            foreach (ResourceRecipeDefinition definition in
                     ResourceRecipeCatalog.All)
            {
                if (definition.Kind != ResourceRecipeKind.ManualCrafting)
                    continue;
                string recipeId = definition.Id;
                Button button = CreatePointerButton(
                    craftingPage,
                    "Crafting.Recipe." + recipeId,
                    RecipeName(recipeId),
                    data => HandleRecipeClick(recipeId, data));
                SetLayout(
                    button.GetComponent<RectTransform>(),
                    0f,
                    66f,
                    1f);
                recipeButtons.Add(recipeId, button);
                AddAmountIcons(
                    button.transform,
                    "Crafting.Recipe." + recipeId + ".Input.",
                    definition.Inputs,
                    new Vector2(22f, 20f));
                AddAmountIcons(
                    button.transform,
                    "Crafting.Recipe." + recipeId + ".Output.",
                    definition.Outputs,
                    new Vector2(22f, -20f));
            }

            RectTransform queue = CreateRect(
                craftingPage,
                "Crafting.Queue");
            SetLayout(queue, 0f, 136f, 1f);
            Image background = queue.gameObject.AddComponent<Image>();
            background.color = new Color(.1f, .13f, .15f, .9f);
            background.raycastTarget = false;
            var layout = queue.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            craftQueueCount = CreateLabel(
                queue,
                "Crafting.Queue.Count",
                "0/" + CraftingQueueModel.MaximumQueuedExecutions,
                15);
            SetLayout(craftQueueCount.rectTransform, 0f, 24f, 1f);
            craftQueueProgress = CreateLabel(
                queue,
                "Crafting.Queue.Progress",
                "进度 0%",
                13);
            SetLayout(craftQueueProgress.rectTransform, 0f, 22f, 1f);
            craftQueueReason = CreateLabel(
                queue,
                "Crafting.Queue.Reason",
                string.Empty,
                13);
            SetLayout(craftQueueReason.rectTransform, 0f, 22f, 1f);
            craftCancelButton = CreateButton(
                queue,
                "Crafting.Queue.CancelFirst",
                "取消队首（返还预留输入）",
                () => CraftCancelRequested?.Invoke());
            craftCancelButton.interactable = false;
        }

        private void BuildResearchTree()
        {
            researchTreePanel = CreateFixedPanel(
                uiRoot,
                "ResearchTreePanel",
                new Vector2(.5f, .5f),
                Vector2.zero,
                new Vector2(1500f, 850f));
            researchTreeView = researchTreePanel.gameObject.AddComponent<
                GrayboxResearchTreeView3D>();
            researchTreeView.Initialize(
                researchTreePanel,
                ResolveIcon,
                researchId => ResearchSelected?.Invoke(researchId),
                () => ResearchStartRequested?.Invoke(),
                () => ResearchCancelRequested?.Invoke(),
                () => SetResearchOpen(false));
        }

        private ResourceRow CreateResourceRow(
            Transform parent,
            string name,
            string title,
            string resourceId,
            Action callback,
            bool compact)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            Button button = null;
            if (callback != null)
            {
                button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => callback());
            }
            else
            {
                image.raycastTarget = false;
            }

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 2, 2);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            RectTransform header = CreateRect(rect, name + ".Header");
            SetLayout(header, 0f, compact ? 16f : 20f, 1f);
            var headerLayout =
                header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 3f;
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;
            Image icon = CreateResourceIcon(
                header,
                name + ".Icon",
                resourceId);
            SetLayout(icon.rectTransform, compact ? 15f : 18f, 0f, 0f);
            Text nameLabel = CreateLabel(header, name + ".Name", title, 12);
            SetLayout(nameLabel.rectTransform, 0f, 0f, 1f);

            RectTransform values = CreateRect(rect, name + ".Values");
            SetLayout(values, 0f, compact ? 28f : 34f, 1f);
            var valuesLayout =
                values.gameObject.AddComponent<HorizontalLayoutGroup>();
            valuesLayout.childForceExpandWidth = true;
            valuesLayout.childForceExpandHeight = true;
            Text amount = CreateLabel(values, name + ".Amount", "0", 12);
            Text capacity = CreateLabel(values, name + ".Capacity", "/0", 12);
            Text flow = CreateLabel(values, name + ".NetFlow", "0/s", 12);
            return new ResourceRow(rect, icon, amount, capacity, flow, button);
        }

        private void HandleCityResourceClick(
            string resourceId,
            PointerEventData data)
        {
            if (data == null || data.button != PointerEventData.InputButton.Left)
                return;
            if (IsShiftPressed())
                CityResourceShiftClicked?.Invoke(resourceId);
        }

        private void HandleBackpackSlotClick(
            int index,
            PointerEventData data)
        {
            if (data == null || index < 0 ||
                index >= backpackSlotButtons.Length)
                return;
            if (data.button == PointerEventData.InputButton.Right)
            {
                BackpackSlotClicked?.Invoke(index, 2);
                return;
            }
            if (data.button != PointerEventData.InputButton.Left) return;
            BackpackSlotClicked?.Invoke(index, IsShiftPressed() ? 0 : 1);
        }

        private void HandleResourceHover(string resourceId, bool entered)
        {
            if (entered)
            {
                hoveredResourceId = resourceId;
                ResourceHoverChanged?.Invoke(resourceId, true);
                return;
            }
            if (!string.Equals(
                    hoveredResourceId,
                    resourceId,
                    StringComparison.Ordinal))
                return;
            hoveredResourceId = null;
            ResourceHoverChanged?.Invoke(resourceId, false);
        }

        private void HandleRecipeClick(
            string recipeId,
            PointerEventData data)
        {
            if (data == null) return;
            if (!recipeButtons.TryGetValue(recipeId, out Button button) ||
                !button.interactable)
                return;
            if (data.button == PointerEventData.InputButton.Right)
            {
                CraftRequested?.Invoke(recipeId, 5);
                return;
            }
            if (data.button != PointerEventData.InputButton.Left) return;
            CraftRequested?.Invoke(recipeId, IsShiftPressed() ? 0 : 1);
        }

        private static bool IsShiftPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                (keyboard.leftShiftKey.isPressed ||
                 keyboard.rightShiftKey.isPressed);
        }

        private ProductionRow EnsureProductionRow(int index)
        {
            while (productionRows.Count <= index)
                productionRows.Add(CreateProductionRow(productionRows.Count));
            return productionRows[index];
        }

        private ProductionRow CreateProductionRow(int index)
        {
            RectTransform root = CreateRect(
                productionStateContent,
                "ProductionState.Item." + index);
            SetLayout(root, 0f, 158f, 1f);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = ButtonColor;
            background.raycastTarget = false;
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 1f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Text title = CreateLabel(
                root,
                "ProductionState.Item." + index + ".Title",
                string.Empty,
                13);
            title.alignment = TextAnchor.MiddleLeft;
            SetLayout(title.rectTransform, 0f, 19f, 1f);
            Text stableId = CreateLabel(
                root,
                "ProductionState.Item." + index + ".StableId",
                string.Empty,
                9);
            stableId.alignment = TextAnchor.MiddleLeft;
            stableId.color = new Color(.68f, .75f, .79f, 1f);
            SetLayout(stableId.rectTransform, 0f, 14f, 1f);
            Text progress = CreateLabel(
                root,
                "ProductionState.Item." + index + ".Progress",
                "进度 0%",
                11);
            progress.alignment = TextAnchor.MiddleLeft;
            SetLayout(progress.rectTransform, 0f, 16f, 1f);
            Text status = CreateLabel(
                root,
                "ProductionState.Item." + index + ".Status",
                string.Empty,
                11);
            status.alignment = TextAnchor.MiddleLeft;
            SetLayout(status.rectTransform, 0f, 16f, 1f);
            Text input = CreateLabel(
                root,
                "ProductionState.Item." + index + ".Input",
                "输入：",
                10);
            input.alignment = TextAnchor.MiddleLeft;
            Image inputIcon = CreateOverlayResourceIcon(
                input.transform,
                "ProductionState.Item." + index + ".Input.Icon",
                null,
                14f,
                new Vector2(8f, 0f));
            inputIcon.gameObject.SetActive(false);
            input.rectTransform.offsetMin = new Vector2(22f, 0f);
            SetLayout(input.rectTransform, 0f, 15f, 1f);
            Text output = CreateLabel(
                root,
                "ProductionState.Item." + index + ".Output",
                "输出：",
                10);
            output.alignment = TextAnchor.MiddleLeft;
            Image outputIcon = CreateOverlayResourceIcon(
                output.transform,
                "ProductionState.Item." + index + ".Output.Icon",
                null,
                14f,
                new Vector2(8f, 0f));
            outputIcon.gameObject.SetActive(false);
            output.rectTransform.offsetMin = new Vector2(22f, 0f);
            SetLayout(output.rectTransform, 0f, 15f, 1f);

            RectTransform actions = CreateRect(
                root,
                "ProductionState.Item." + index + ".Actions");
            SetLayout(actions, 0f, 28f, 1f);
            var actionLayout =
                actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 6f;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = true;
            Button inputTransfer = CreatePointerButton(
                actions,
                "ProductionState.Item." + index + ".InputTransfer",
                "补给输入（Shift：背包）",
                data => HandleProductionCacheTransfer(
                    index,
                    input: true,
                    data: data));
            Button outputTransfer = CreatePointerButton(
                actions,
                "ProductionState.Item." + index + ".OutputTransfer",
                "取出输出（Shift：背包）",
                data => HandleProductionCacheTransfer(
                    index,
                    input: false,
                    data: data));
            Button pause = CreateButton(
                actions,
                "ProductionState.Item." + index + ".Pause",
                "暂停运行",
                () => HandleProductionPause(index));
            SetLayout(
                inputTransfer.GetComponent<RectTransform>(),
                0f,
                28f,
                1f);
            SetLayout(
                outputTransfer.GetComponent<RectTransform>(),
                0f,
                28f,
                1f);
            SetLayout(
                pause.GetComponent<RectTransform>(),
                0f,
                28f,
                1f);
            Text accessStatus = CreateLabel(
                root,
                "ProductionState.Item." + index + ".AccessStatus",
                string.Empty,
                10);
            accessStatus.alignment = TextAnchor.MiddleLeft;
            accessStatus.color = new Color(.95f, .78f, .42f, 1f);
            SetLayout(accessStatus.rectTransform, 0f, 16f, 1f);
            return new ProductionRow(
                root,
                stableId,
                title,
                progress,
                status,
                input,
                output,
                inputIcon,
                outputIcon,
                pause,
                accessStatus);
        }

        private void HandleProductionPause(int index)
        {
            if (index < 0 || index >= productionRows.Count) return;
            string stableInstanceId = productionRows[index].StableInstanceId;
            if (!string.IsNullOrWhiteSpace(stableInstanceId))
                ProductionPauseRequested?.Invoke(stableInstanceId);
        }

        private void HandleProductionCacheTransfer(
            int index,
            bool input,
            PointerEventData data)
        {
            if (data == null ||
                data.button != PointerEventData.InputButton.Left ||
                index < 0 || index >= productionRows.Count)
            {
                return;
            }

            string stableInstanceId =
                productionRows[index].StableInstanceId;
            if (string.IsNullOrWhiteSpace(stableInstanceId)) return;
            ProductionCacheTransferRequested?.Invoke(
                stableInstanceId,
                input,
                IsShiftPressed());
        }

        private void RetireStaleRoots()
        {
            for (var index = canvas.transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = canvas.transform.GetChild(index);
                if (!string.Equals(
                        child.name,
                        "ProductionObservabilityUi.Root",
                        StringComparison.Ordinal))
                    continue;
                child.name = "ProductionObservabilityUi.Retired";
                child.gameObject.SetActive(false);
                DestroyGenerated(child.gameObject);
            }
        }

        private void DestroyUi()
        {
            if (uiRoot != null)
            {
                uiRoot.gameObject.SetActive(false);
                DestroyGenerated(uiRoot.gameObject);
            }
            uiRoot = null;
            resourceStatusBar = null;
            resourceLedgerPanel = null;
            inventoryCraftingPanel = null;
            researchTreePanel = null;
            researchTreeView = null;
            resourceTooltipRoot = null;
            resourceTooltipText = null;
            cityPage = null;
            backpackPage = null;
            craftingPage = null;
            productionStateContent = null;
            productionStatePanel = null;
            warehouseDetailPanel = null;
            warehouseStableId = null;
            warehouseLogistics = null;
            warehouseCapacity = null;
            warehouseFilterStatus = null;
            craftQueueCount = null;
            craftQueueProgress = null;
            craftQueueReason = null;
            craftCancelButton = null;
            statusRows.Clear();
            ledgerRows.Clear();
            cityRows.Clear();
            warehouseResourceRows.Clear();
            warehouseFilterButtons.Clear();
            recipeButtons.Clear();
            productionRows.Clear();
            Array.Clear(backpackSlotLabels, 0, backpackSlotLabels.Length);
            Array.Clear(backpackSlotButtons, 0, backpackSlotButtons.Length);
            Array.Clear(backpackSlotIcons, 0, backpackSlotIcons.Length);
            selectedBackpackSlot = -1;
            productionStateCount = 0;
            hoveredResourceId = null;
        }

        private static RectTransform CreateFixedPanel(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreatePage(Transform parent, string name)
        {
            RectTransform page = CreateRect(parent, name);
            Stretch(page);
            return page;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Action callback)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetLayout(rect, 0f, 38f, 1f);
            if (callback != null)
                button.onClick.AddListener(() => callback());
            CreateLabel(rect, name + ".Label", label, 14);
            return button;
        }

        private static Button CreatePointerButton(
            Transform parent,
            string name,
            string label,
            Action<PointerEventData> callback)
        {
            Button button = CreateButton(parent, name, label, callback: null);
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            AddTrigger(
                trigger,
                EventTriggerType.PointerClick,
                data => callback?.Invoke(data as PointerEventData));
            return button;
        }

        private Image CreateResourceIcon(
            Transform parent,
            string name,
            string resourceId)
        {
            RectTransform rect = CreateRect(parent, name);
            var icon = rect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = ResolveIcon(resourceId);
            return icon;
        }

        private Image CreateOverlayResourceIcon(
            Transform parent,
            string name,
            string resourceId,
            float size,
            Vector2 anchoredPosition)
        {
            Image icon = CreateResourceIcon(parent, name, resourceId);
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = new Vector2(0f, .5f);
            rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);
            return icon;
        }

        private void AddAmountIcons(
            Transform parent,
            string namePrefix,
            IReadOnlyList<ResourceAmount> amounts,
            Vector2 firstPosition)
        {
            if (amounts == null) return;
            for (var index = 0; index < amounts.Count; index++)
            {
                ResourceAmount amount = amounts[index];
                CreateOverlayResourceIcon(
                    parent,
                    namePrefix + amount.ResourceId + ".Icon",
                    amount.ResourceId,
                    20f,
                    firstPosition + Vector2.right * (index * 24f));
            }
        }

        private Sprite ResolveIcon(string resourceId)
        {
            return resourceIconCatalog == null
                ? ResourceIconCatalog3D.Resolve(resourceId)
                : resourceIconCatalog.ResolveIcon(resourceId);
        }

        private void SetIcon(Image icon, string resourceId)
        {
            if (icon == null) return;
            icon.sprite = ResolveIcon(resourceId);
            icon.gameObject.SetActive(icon.sprite != null);
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            string value,
            int fontSize)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void AddVerticalLayout(
            RectTransform rect,
            int padding,
            float spacing)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                padding,
                padding,
                padding,
                padding);
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void SetLayout(
            RectTransform rect,
            float preferredWidth,
            float preferredHeight,
            float flexibleWidth)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>() ??
                rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
        }

        private static void AddTrigger(
            EventTrigger trigger,
            EventTriggerType type,
            Action<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback(data));
            trigger.triggers.Add(entry);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, float minY, float maxY)
        {
            rect.anchorMin = new Vector2(0f, minY);
            rect.anchorMax = new Vector2(1f, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool ContainsResource(
            IReadOnlyList<string> values,
            string resourceId)
        {
            for (var index = 0; index < values.Count; index++)
                if (string.Equals(
                        values[index],
                        resourceId,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string FormatFlow(float value)
        {
            return value.ToString("+0.##;-0.##;0") + "/s";
        }

        private static string CapacityText(int amount, int capacity)
        {
            int safeCapacity = Math.Max(0, capacity);
            int overage = Math.Max(0, amount - safeCapacity);
            return "/" + safeCapacity + (overage > 0
                ? " · 超出 " + overage + "（禁止继续入库）"
                : string.Empty);
        }

        private static string ResourceName(string resourceId)
        {
            return ResourceDefinitionCatalog.TryGet(
                resourceId,
                out ResourceDefinition definition)
                ? definition.ChineseName
                : resourceId ?? string.Empty;
        }

        private static string RecipeName(string recipeId)
        {
            string title = RecipeTitle(recipeId);
            if (!ResourceRecipeCatalog.TryGet(
                    recipeId,
                    out ResourceRecipeDefinition definition))
                return title;
            return title + "：" + FormatRecipeAmounts(definition.Inputs) +
                " → " + FormatRecipeAmounts(definition.Outputs) +
                " / " + definition.DurationSeconds.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture) + " 秒";
        }

        private static string RecipeTitle(string recipeId)
        {
            return ResourceRecipeCatalog.DisplayName(recipeId);
        }

        private static string FormatRecipeAmounts(
            IReadOnlyList<ResourceAmount> amounts)
        {
            if (amounts == null || amounts.Count == 0) return "无";
            string text = string.Empty;
            for (var index = 0; index < amounts.Count; index++)
            {
                ResourceAmount amount = amounts[index];
                if (index > 0) text += " + ";
                text += amount.Amount + " " + ResourceName(amount.ResourceId);
            }
            return text;
        }

        private static void DestroyGenerated(GameObject gameObject)
        {
            if (gameObject == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(gameObject);
            else
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private sealed class ResourceRow
        {
            public ResourceRow(
                RectTransform root,
                Image icon,
                Text amount,
                Text capacity,
                Text netFlow,
                Button button)
            {
                Root = root;
                Icon = icon;
                Amount = amount;
                Capacity = capacity;
                NetFlow = netFlow;
                Button = button;
            }

            public RectTransform Root { get; }
            public Image Icon { get; }
            public Text Amount { get; }
            public Text Capacity { get; }
            public Text NetFlow { get; }
            public Button Button { get; }
            public bool HasValue { get; set; }
            public bool Visible { get; set; }
            public int AmountValue { get; set; }
            public int CapacityValue { get; set; }
            public float NetFlowValue { get; set; }
        }

        private sealed class ProductionRow
        {
            public ProductionRow(
                RectTransform root,
                Text stableId,
                Text title,
                Text progress,
                Text status,
                Text input,
                Text output,
                Image inputIcon,
                Image outputIcon,
                Button pause,
                Text accessStatus)
            {
                Root = root;
                StableId = stableId;
                Title = title;
                Progress = progress;
                Status = status;
                Input = input;
                Output = output;
                InputIcon = inputIcon;
                OutputIcon = outputIcon;
                Pause = pause;
                AccessStatus = accessStatus;
            }

            public RectTransform Root { get; }
            public string StableInstanceId { get; set; }
            public Text StableId { get; }
            public Text Title { get; }
            public Text Progress { get; }
            public Text Status { get; }
            public Text Input { get; }
            public Text Output { get; }
            public Image InputIcon { get; }
            public Image OutputIcon { get; }
            public Button Pause { get; }
            public Text AccessStatus { get; }
        }
    }
}
