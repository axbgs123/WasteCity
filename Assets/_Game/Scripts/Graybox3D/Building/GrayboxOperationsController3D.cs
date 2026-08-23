using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxOperationsController3D : MonoBehaviour
    {
        private const float RecentFlowWindowSeconds = 1f;

        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxProductionController3D production;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxOperationsView3D view;
        [SerializeField]
        private GrayboxDirectControlCoordinator directControl;
        [SerializeField] private GrayboxWorldView3D worldView;
        [SerializeField] private GrayboxLeaderController3D leader;

        private readonly Dictionary<string, float> netFlowByResource =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> incomeFlowByResource =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> expenseFlowByResource =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Queue<InventoryChangeSample> recentInventoryChanges =
            new Queue<InventoryChangeSample>();
        private readonly List<string> recentIncomeSources =
            new List<string>();
        private readonly List<string> recentExpenseDestinations =
            new List<string>();
        private readonly Dictionary<string, string> productionAccessStatus =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> discoveredHudResources =
            new HashSet<string>(StringComparer.Ordinal);

        private GrayboxBuildingSession3D modelSession;
        private CityResourceStorageModel observedStorage;
        private PlayerBackpackModel backpack;
        private CraftingQueueModel crafting;
        private FormalResearchRuntime research;
        private string selectedResearchId;
        private string hoveredResourceId;
        private int selectedBackpackSlot = -1;
        private int selectedBackpackGesture;
        private bool selectedBackpackPlacesOne;
        private bool eventsBound;
        private string inventoryTransferStatus;
        private string craftingFeedback;
        private string selectedProductionId;
        private string selectedWarehouseId;
        private string warehouseFilterFeedback;
        private ulong lastViewFingerprint;
        private bool hasViewFingerprint;

        public bool IsAnyPanelOpen => view != null &&
            (view.IsInventoryOpen ||
             view.IsResearchOpen ||
             view.IsLedgerOpen);
        public bool IsResearchOpen => view != null && view.IsResearchOpen;
        public bool HasResearchTextInputFocus =>
            view != null && view.HasResearchTextInputFocus;
        public PlayerBackpackModel Backpack => backpack;
        public CraftingQueueModel Crafting => crafting;
        public FormalResearchRuntime Research => research;
        public uint ViewRefreshCount { get; private set; }

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxProductionController3D production,
            GrayboxMobileCityController3D city,
            GrayboxOperationsView3D view)
        {
            UnbindViewEvents();
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.production = production ??
                throw new ArgumentNullException(nameof(production));
            this.city = city ??
                throw new ArgumentNullException(nameof(city));
            this.view = view ??
                throw new ArgumentNullException(nameof(view));
            EnsureModels();
            BindViewEvents();
            RefreshView();
        }

        public void ToggleInventory()
        {
            if (!EnsureReady()) return;
            bool open = !view.IsInventoryOpen;
            view.SetInventoryOpen(open);
            if (!open) ClearBackpackSelection();
            if (open)
            {
                view.SetResearchOpen(false);
                view.SetLedgerOpen(false);
                view.SetInventoryTab(GrayboxInventoryTab3D.Inventory);
            }
            RefreshView();
        }

        public void ToggleResearch()
        {
            if (!EnsureReady()) return;
            bool open = !view.IsResearchOpen;
            view.SetResearchOpen(open);
            if (open)
            {
                ClearBackpackSelection();
                view.SetInventoryOpen(false);
                view.SetLedgerOpen(false);
            }
            RefreshView();
            if (open)
                view.FocusResearchTreeOnOpen(
                    LatestProgressionCandidateId());
        }

        public bool ConsumeFocusedResearchEscape()
        {
            return view != null && view.ConsumeFocusedResearchEscape();
        }

        public void FitResearchTree()
        {
            view?.FitResearchTree();
        }

        public void ClosePanels()
        {
            if (view == null) return;
            ClearBackpackSelection();
            view.SetInventoryOpen(false);
            view.SetResearchOpen(false);
            view.SetLedgerOpen(false);
            selectedProductionId = null;
            selectedWarehouseId = null;
        }

        public bool TryOpenProductionDetail(string stableInstanceId)
        {
            return TryFindProductionDetails(stableInstanceId, out _) &&
                TryOpenBuildingDetail(stableInstanceId);
        }

        public bool TryOpenBuildingDetail(string stableInstanceId)
        {
            if (!EnsureReady() ||
                !TryFindBuildingInstance(
                    stableInstanceId,
                    out GrayboxBuildingInstance3D instance) ||
                instance.State != GrayboxBuildingInstanceState.Completed ||
                !instance.IsPlayerOwned)
            {
                return false;
            }
            bool productionDetails =
                TryFindProductionDetails(stableInstanceId, out _);
            bool warehouseDetails = string.Equals(
                    instance.Placement.Definition.Id.Value,
                    BuildingCatalog.Warehouse.Id.Value,
                    StringComparison.Ordinal) &&
                session.CityStorage.ContainsWarehouse(stableInstanceId);
            if (!productionDetails && !warehouseDetails) return false;
            selectedProductionId = productionDetails
                ? stableInstanceId
                : null;
            selectedWarehouseId = warehouseDetails
                ? stableInstanceId
                : null;
            warehouseFilterFeedback = string.Empty;
            ClearBackpackSelection();
            view.SetInventoryOpen(false);
            view.SetResearchOpen(false);
            view.SetLedgerOpen(true);
            RefreshView();
            return true;
        }

        private void Awake()
        {
            EnsureModels();
        }

        private void OnEnable()
        {
            EnsureModels();
            BindViewEvents();
        }

        private void OnDisable()
        {
            UnbindViewEvents();
        }

        private void OnDestroy()
        {
            UnbindViewEvents();
            UnbindInventoryEvents();
        }

        private void Update()
        {
            if (!EnsureReady()) return;

            bool paused = Time.timeScale <= 0f;
            float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
            crafting.Tick(deltaSeconds, paused);
            research.Tick(
                deltaSeconds,
                city.Mode,
                paused,
                HasEligibleResearchStation());
            RefreshIfChanged();
        }

        public bool RefreshIfChanged()
        {
            if (!EnsureReady()) return false;
            PruneRecentFlowSamples();
            ulong fingerprint = ComputeViewFingerprint();
            if (hasViewFingerprint && fingerprint == lastViewFingerprint)
                return false;
            RefreshView();
            return true;
        }

        private bool EnsureReady()
        {
            return EnsureModels() && view != null && production != null &&
                city != null;
        }

        private bool EnsureModels()
        {
            if (session == null || session.Inventory == null ||
                session.CityStorage == null ||
                session.Research == null)
            {
                return false;
            }
            if (ReferenceEquals(modelSession, session) &&
                backpack != null && crafting != null && research != null)
            {
                BindInventoryEvents();
                return true;
            }

            UnbindInventoryEvents();
            modelSession = session;
            backpack = new PlayerBackpackModel();
            crafting = new CraftingQueueModel(
                backpack,
                session.IsResearchCompleted);
            research = new FormalResearchRuntime(session.Research);
            selectedResearchId = null;
            hoveredResourceId = null;
            productionAccessStatus.Clear();
            discoveredHudResources.Clear();
            inventoryTransferStatus = string.Empty;
            craftingFeedback = string.Empty;
            selectedWarehouseId = null;
            warehouseFilterFeedback = string.Empty;
            recentInventoryChanges.Clear();
            hasViewFingerprint = false;
            ClearBackpackSelection();
            BindInventoryEvents();
            return true;
        }

        private void BindInventoryEvents()
        {
            if (session?.CityStorage == null ||
                ReferenceEquals(observedStorage, session.CityStorage))
            {
                return;
            }
            UnbindInventoryEvents();
            observedStorage = session.CityStorage;
            observedStorage.AttributedChanged += HandleInventoryChanged;
        }

        private void UnbindInventoryEvents()
        {
            if (observedStorage != null)
                observedStorage.AttributedChanged -= HandleInventoryChanged;
            observedStorage = null;
        }

        private void HandleInventoryChanged(
            string resourceId,
            int delta,
            ResourceChangeAttribution attribution)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || delta == 0) return;
            recentInventoryChanges.Enqueue(new InventoryChangeSample(
                Time.unscaledTime,
                resourceId,
                delta,
                attribution));
        }

        private void BindViewEvents()
        {
            if (eventsBound || view == null) return;
            view.ResourceClicked += OpenResourceLedger;
            view.CityResourceShiftClicked += TransferCityResourceToBackpack;
            view.BackpackSlotClicked += HandleBackpackSlotClick;
            view.CraftRequested += EnqueueCrafting;
            view.CraftCancelRequested += CancelFirstCraft;
            view.ResourceHoverChanged += HandleResourceHover;
            view.ProductionCacheTransferRequested +=
                TransferProductionCache;
            view.ProductionPauseRequested += ToggleProductionPause;
            view.ResearchSelected += SelectResearch;
            view.ResearchStartRequested += StartResearch;
            view.ResearchCancelRequested += CancelResearch;
            view.WarehouseFilterRequested += SetWarehouseFilter;
            view.InventoryCloseRequested += ClosePanels;
            eventsBound = true;
        }

        private void UnbindViewEvents()
        {
            if (!eventsBound || view == null)
            {
                eventsBound = false;
                return;
            }
            view.ResourceClicked -= OpenResourceLedger;
            view.CityResourceShiftClicked -= TransferCityResourceToBackpack;
            view.BackpackSlotClicked -= HandleBackpackSlotClick;
            view.CraftRequested -= EnqueueCrafting;
            view.CraftCancelRequested -= CancelFirstCraft;
            view.ResourceHoverChanged -= HandleResourceHover;
            view.ProductionCacheTransferRequested -=
                TransferProductionCache;
            view.ProductionPauseRequested -= ToggleProductionPause;
            view.ResearchSelected -= SelectResearch;
            view.ResearchStartRequested -= StartResearch;
            view.ResearchCancelRequested -= CancelResearch;
            view.WarehouseFilterRequested -= SetWarehouseFilter;
            view.InventoryCloseRequested -= ClosePanels;
            eventsBound = false;
        }

        private void OpenResourceLedger(string resourceId)
        {
            if (!ResourceDefinitionCatalog.TryGet(resourceId, out _) ||
                view == null)
            {
                return;
            }
            selectedProductionId = null;
            selectedWarehouseId = null;
            view.SetInventoryOpen(false);
            view.SetResearchOpen(false);
            view.SetLedgerOpen(true);
            RefreshView();
        }

        private void TransferCityResourceToBackpack(string resourceId)
        {
            if (!EnsureReady()) return;
            if (!CanAccessCityInventory())
            {
                inventoryTransferStatus =
                    "无法访问：领队需在城市核心 2 格内";
                RefreshView();
                return;
            }
            int requested = session.CityStorage.GetNetworkAmount(resourceId);
            ResourceTransferResult result;
            using (session.CityStorage.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Backpack)))
            {
                result = ResourceTransaction.TransferToBackpack(
                    session.CityStorage,
                    backpack,
                    resourceId,
                    requested);
            }
            inventoryTransferStatus = TransferStatusText(result);
            RefreshView();
        }

        private void EnqueueCrafting(string recipeId, int count)
        {
            if (!EnsureReady()) return;
            int queuedBefore = crafting.QueuedExecutionCount;
            if (count == 0)
                crafting.EnqueueMaximum(recipeId);
            else if (count == 1 || count == 5)
                crafting.TryEnqueue(recipeId, count);
            int queued = crafting.QueuedExecutionCount - queuedBefore;
            craftingFeedback = queued > 0
                ? "已加入 " + queued + " 次"
                : CraftingRequestFailureText(recipeId, count);
            RefreshView();
        }

        private void CancelFirstCraft()
        {
            if (!EnsureReady()) return;
            bool hadQueuedCraft = crafting.QueuedExecutionCount > 0;
            craftingFeedback = crafting.TryCancelAt(0)
                ? "已取消队首并返还材料"
                : hadQueuedCraft
                    ? "背包空间不足，无法返还材料"
                    : "无可取消的合成";
            RefreshView();
        }

        private void HandleBackpackSlotClick(int slotIndex, int gesture)
        {
            if (!EnsureReady() || slotIndex < 0 ||
                slotIndex >= backpack.SlotCount)
            {
                return;
            }

            if (gesture == 0)
            {
                TransferBackpackSlotToCity(slotIndex);
                ClearBackpackSelection();
                RefreshView();
                return;
            }
            if (gesture != 1 && gesture != 2) return;

            BackpackSlot clicked = backpack.GetSlot(slotIndex);
            if (selectedBackpackSlot < 0)
            {
                if (clicked.Amount <= 0 ||
                    string.IsNullOrWhiteSpace(clicked.ResourceId))
                {
                    return;
                }
                selectedBackpackSlot = slotIndex;
                selectedBackpackGesture = gesture;
                selectedBackpackPlacesOne = false;
                RefreshView();
                return;
            }

            if (selectedBackpackSlot == slotIndex)
            {
                ClearBackpackSelection();
                RefreshView();
                return;
            }
            if (selectedBackpackGesture == 2 && gesture == 2)
            {
                if (!selectedBackpackPlacesOne)
                {
                    if (backpack.SplitHalf(
                            selectedBackpackSlot,
                            slotIndex))
                    {
                        selectedBackpackSlot = slotIndex;
                        selectedBackpackPlacesOne = true;
                    }
                }
                else
                {
                    backpack.MoveOne(selectedBackpackSlot, slotIndex);
                    BackpackSlot source = backpack.GetSlot(
                        selectedBackpackSlot);
                    if (source.Amount <= 0 ||
                        string.IsNullOrWhiteSpace(source.ResourceId))
                    {
                        ClearBackpackSelection();
                    }
                }
                RefreshView();
                return;
            }
            if (selectedBackpackGesture != gesture)
            {
                if (clicked.Amount > 0 &&
                    !string.IsNullOrWhiteSpace(clicked.ResourceId))
                {
                    selectedBackpackSlot = slotIndex;
                    selectedBackpackGesture = gesture;
                    selectedBackpackPlacesOne = false;
                }
                else
                {
                    ClearBackpackSelection();
                }
                RefreshView();
                return;
            }

            int sourceIndex = selectedBackpackSlot;
            ClearBackpackSelection();
            backpack.MoveWholeStack(sourceIndex, slotIndex);
            RefreshView();
        }

        private void TransferBackpackSlotToCity(int slotIndex)
        {
            if (!CanAccessCityInventory())
            {
                inventoryTransferStatus =
                    "无法访问：领队需在城市核心 2 格内";
                return;
            }
            BackpackSlot slot = backpack.GetSlot(slotIndex);
            if (slot.Amount <= 0 || string.IsNullOrWhiteSpace(slot.ResourceId))
                return;
            ResourceTransferResult result;
            using (session.CityStorage.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Backpack)))
            {
                result = ResourceTransaction.TransferFromBackpackSlot(
                    backpack,
                    slotIndex,
                    session.CityStorage,
                    slot.Amount);
            }
            inventoryTransferStatus = TransferStatusText(result);
        }

        private void ClearBackpackSelection()
        {
            selectedBackpackSlot = -1;
            selectedBackpackGesture = 0;
            selectedBackpackPlacesOne = false;
        }

        private void HandleResourceHover(string resourceId, bool entered)
        {
            if (entered)
            {
                hoveredResourceId = ResourceDefinitionCatalog.TryGet(
                        resourceId,
                        out _)
                    ? resourceId
                    : null;
            }
            else if (string.Equals(
                         hoveredResourceId,
                         resourceId,
                         StringComparison.Ordinal))
            {
                hoveredResourceId = null;
            }
            if (EnsureReady())
                RefreshResourceTooltip();
            else
                view?.SetResourceTooltip(string.Empty, visible: false);
        }

        private void SelectResearch(string researchId)
        {
            if (string.IsNullOrEmpty(researchId))
            {
                selectedResearchId = null;
                RefreshView();
                return;
            }
            if (ResearchCatalog.Find(researchId) == null) return;
            selectedResearchId = researchId;
            RefreshView();
        }

        private void StartResearch()
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(selectedResearchId))
                return;
            using (session.CityStorage.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Research,
                           selectedResearchId)))
            {
                research.TryStart(
                    selectedResearchId,
                    session.CityStorage,
                    HasEligibleResearchStation());
            }
            RefreshView();
        }

        private void CancelResearch()
        {
            if (!EnsureReady()) return;
            using (session.CityStorage.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Research,
                           research.Model.Active?.Id.Value)))
            {
                research.TryCancel(session.CityStorage);
            }
            RefreshView();
        }

        private void RefreshView()
        {
            if (!EnsureReady()) return;
            unchecked { ViewRefreshCount++; }
            CaptureRecentFlow();
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                string id = definition.Id;
                int amount = session.CityStorage.GetNetworkAmount(id);
                int capacity =
                    session.CityStorage.GetNetworkCapacityLimit(id);
                float netFlow = NetFlow(id);
                bool visible = IsHudResourceVisible(id, amount);
                view.SetResource(id, visible, amount, capacity, netFlow);
                if (view.IsLedgerOpen)
                    view.SetLedgerResource(id, amount, capacity, netFlow);
                if (view.IsInventoryOpen)
                    view.SetCityResource(id, amount);
                if (amount > 0)
                    discoveredHudResources.Add(id);
            }

            if (view.IsInventoryOpen)
            {
                for (int index = 0; index < backpack.SlotCount; index++)
                {
                    BackpackSlot slot = backpack.GetSlot(index);
                    view.SetBackpackSlot(index, slot.ResourceId, slot.Amount);
                }
                int oneByOneAmount = selectedBackpackPlacesOne &&
                    selectedBackpackSlot >= 0
                    ? backpack.GetSlot(selectedBackpackSlot).Amount
                    : 0;
                view.SetBackpackSelection(
                    selectedBackpackSlot,
                    oneByOneAmount,
                    selectedBackpackPlacesOne);
                view.SetInventoryTransferStatus(inventoryTransferStatus);
                RefreshCrafting();
            }
            if (view.IsResearchOpen) RefreshResearch();
            if (view.IsLedgerOpen) RefreshProductionStates();
            RefreshResourceTooltip();
            lastViewFingerprint = ComputeViewFingerprint();
            hasViewFingerprint = true;
        }

        private void RefreshCrafting()
        {
            float progress = 0f;
            if (ResourceRecipeCatalog.TryGet(
                    crafting.ActiveRecipeId,
                    out ResourceRecipeDefinition activeRecipe) &&
                activeRecipe.DurationSeconds > 0f)
            {
                progress = Mathf.Clamp01(
                    crafting.ActiveProgressSeconds /
                    activeRecipe.DurationSeconds);
            }
            string queueStatus = CraftingBlockReasonText(
                crafting.BlockReason);
            if (crafting.QueuedExecutionCount > 0)
            {
                string activeName = ResourceRecipeCatalog.DisplayName(
                    crafting.ActiveRecipeId);
                queueStatus = "队首：" + activeName + " · " +
                    CraftingQueueText() +
                    (string.IsNullOrEmpty(queueStatus)
                        ? string.Empty
                        : " · " + queueStatus);
            }
            if (!string.IsNullOrEmpty(craftingFeedback))
                queueStatus += (string.IsNullOrEmpty(queueStatus)
                    ? string.Empty
                    : " · ") + craftingFeedback;
            view.SetCraftQueue(
                crafting.QueuedExecutionCount,
                progress,
                queueStatus);

            SetCraftRecipe(
                ResourceRecipeCatalog.FieldAlloyId);
            SetCraftRecipe(
                ResourceRecipeCatalog.FieldAmmunitionId);
        }

        private void SetCraftRecipe(string recipeId)
        {
            bool unlocked = ResourceRecipeCatalog.TryGet(
                    recipeId,
                    out ResourceRecipeDefinition definition) &&
                (string.IsNullOrWhiteSpace(definition.RequiredResearchId) ||
                 session.IsResearchCompleted(
                     definition.RequiredResearchId));
            ResearchDefinition requiredResearch = definition == null
                ? null
                : ResearchCatalog.Find(
                    definition.RequiredResearchId);
            string stateText = unlocked
                ? "当前可排 " +
                  AvailableCraftExecutions(definition)
                : "需要科技：" +
                  (requiredResearch?.Name ?? "未解锁科技");
            view.SetCraftRecipe(
                recipeId,
                stateText,
                unlocked);
        }

        private void RefreshResearch()
        {
            ResearchDefinition active = research.Model.Active;
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                view.SetResearchNode(
                    definition,
                    ResearchStatus(definition, active),
                    string.Equals(
                        selectedResearchId,
                        definition.Id.Value,
                        StringComparison.Ordinal));
            }

            bool hasActive = active != null;
            string progress = hasActive
                ? ResearchProgressText(active)
                : string.Empty;
            view.SetResearchActive(
                hasActive ? active.Name : string.Empty,
                progress,
                hasActive,
                hasActive ? active.Id.Value : null);
            view.SetResearchStartInteractable(CanStartSelectedResearch());
            view.SetResearchCancelInteractable(hasActive);
        }

        private string ResearchStatus(
            ResearchDefinition definition,
            ResearchDefinition active)
        {
            if (research.IsCompleted(definition.Id.Value))
                return "已完成";
            if (active != null && active.Id.Equals(definition.Id))
                return "研究中";
            if (definition.ReleaseState == ResearchReleaseState.PreviewOnly)
            {
                return "本阶段未开放（预览）";
            }
            if (!PrerequisitesCompleted(definition))
                return "锁定：前置 " +
                    MissingPrerequisiteNames(definition);
            if (!HasEligibleResearchStation())
                return "无法研究：缺少研究站";
            for (int index = 0; index < definition.Costs.Count; index++)
            {
                ResourceAmount cost = definition.Costs[index];
                if (!session.CityStorage.CanSpendFromNetwork(
                        cost.ResourceId,
                        cost.Amount))
                    return "资源不足";
            }
            return "可研究";
        }

        private bool CanStartSelectedResearch()
        {
            if (research.Model.Active != null ||
                string.IsNullOrWhiteSpace(selectedResearchId) ||
                !HasEligibleResearchStation())
            {
                return false;
            }
            ResearchDefinition definition =
                ResearchCatalog.Find(selectedResearchId);
            if (definition == null ||
                definition.ReleaseState != ResearchReleaseState.Researchable ||
                research.IsCompleted(definition.Id.Value) ||
                !PrerequisitesCompleted(definition))
            {
                return false;
            }
            for (int index = 0; index < definition.Costs.Count; index++)
            {
                ResourceAmount cost = definition.Costs[index];
                if (!session.CityStorage.CanSpendFromNetwork(
                        cost.ResourceId,
                        cost.Amount))
                    return false;
            }
            return true;
        }

        private bool PrerequisitesCompleted(ResearchDefinition definition)
        {
            for (int index = 0;
                 index < definition.RequiredResearchIds.Count;
                 index++)
            {
                if (!research.IsCompleted(
                        definition.RequiredResearchIds[index]))
                    return false;
            }
            return true;
        }

        private string LatestProgressionCandidateId()
        {
            ResearchDefinition selected = null;
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                if (definition.ReleaseState !=
                        ResearchReleaseState.Researchable ||
                    research.IsCompleted(definition.Id.Value) ||
                    !PrerequisitesCompleted(definition))
                {
                    continue;
                }
                if (selected == null ||
                    definition.LayoutRow > selected.LayoutRow ||
                    definition.LayoutRow == selected.LayoutRow &&
                    definition.CatalogOrder < selected.CatalogOrder)
                {
                    selected = definition;
                }
            }
            return selected?.Id.Value;
        }

        private string ResearchProgressText(ResearchDefinition active)
        {
            float duration = Mathf.Max(.001f, active.Duration);
            float normalized = Mathf.Clamp01(
                1f - research.Model.Remaining / duration);
            string pausedReason = Time.timeScale <= 0f
                ? " · 全局暂停"
                : !HasEligibleResearchStation()
                    ? " · 缺少研究站，已暂停"
                    : string.Empty;
            int efficiency = Mathf.RoundToInt(
                FormalResearchRuntime.SpeedMultiplier(
                    city.Mode,
                    research.IsCompleted(
                        ResearchCatalog.ThoughtAccelerationId)) * 100f);
            return Mathf.RoundToInt(normalized * 100f) + "% · 剩余 " +
                research.Model.Remaining.ToString("0.0") +
                " 秒 · 效率 " + efficiency + "%" + pausedReason;
        }

        private string MissingPrerequisiteNames(
            ResearchDefinition definition)
        {
            string result = string.Empty;
            for (int index = 0;
                 index < definition.RequiredResearchIds.Count;
                 index++)
            {
                string id = definition.RequiredResearchIds[index];
                if (research.IsCompleted(id)) continue;
                if (!string.IsNullOrEmpty(result)) result += "、";
                ResearchDefinition prerequisite =
                    ResearchCatalog.Find(id);
                result += prerequisite?.Name ?? id;
            }
            return result;
        }

        private int AvailableCraftExecutions(
            ResourceRecipeDefinition definition)
        {
            return definition == null
                ? 0
                : crafting.MaximumEnqueueable(definition.Id);
        }

        private string CraftingRequestFailureText(string recipeId, int count)
        {
            if (!ResourceRecipeCatalog.TryGet(
                    recipeId,
                    out ResourceRecipeDefinition definition))
                return "配方不可用";
            if (!string.IsNullOrWhiteSpace(definition.RequiredResearchId) &&
                !session.IsResearchCompleted(definition.RequiredResearchId))
                return "需要科技";
            int remainingQueue =
                CraftingQueueModel.MaximumQueuedExecutions -
                crafting.QueuedExecutionCount;
            if (remainingQueue <= 0 ||
                count > 0 && count > remainingQueue)
                return "队列容量不足";
            return "输入材料不足";
        }

        private string CraftingQueueText()
        {
            string text = "队列：";
            string currentId = null;
            int currentCount = 0;
            for (int index = 0;
                 index < crafting.QueuedExecutionCount;
                 index++)
            {
                string id = crafting.QueuedRecipeIdAt(index);
                if (string.Equals(id, currentId, StringComparison.Ordinal))
                {
                    currentCount++;
                    continue;
                }
                if (currentCount > 0)
                    text += ResourceRecipeCatalog.DisplayName(currentId) +
                        "×" + currentCount + " → ";
                currentId = id;
                currentCount = 1;
            }
            return text + ResourceRecipeCatalog.DisplayName(currentId) +
                "×" + currentCount;
        }

        private bool HasEligibleResearchStation()
        {
            if (session?.Instances == null) return false;
            for (var index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = session.Instances[index];
                if (instance.State ==
                        GrayboxBuildingInstanceState.Completed &&
                    instance.IsPlayerOwned &&
                    !instance.IsEvacuationLocked &&
                    string.Equals(
                        instance.Placement.Definition.Id.Value,
                        BuildingCatalog.ResearchStation.Id.Value,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private int ActiveWarehouseCount()
        {
            return production?.Clock?.Snapshot?.ActiveWarehouseCount ?? 0;
        }

        private bool IsHudResourceVisible(string resourceId, int amount)
        {
            for (int index = 0;
                 index < ResourceDefinitionCatalog.BaseHudResourceIds.Count;
                 index++)
            {
                if (string.Equals(
                        ResourceDefinitionCatalog.BaseHudResourceIds[index],
                        resourceId,
                        StringComparison.Ordinal))
                    return true;
            }
            if (string.Equals(resourceId, ResourceIds.Alloy,
                    StringComparison.Ordinal))
            {
                return discoveredHudResources.Contains(resourceId) ||
                    amount > 0 || research.IsCompleted(
                    ResearchCatalog.AutomatedMachineryId);
            }
            if (string.Equals(resourceId, ResourceIds.Ammunition,
                    StringComparison.Ordinal))
            {
                return discoveredHudResources.Contains(resourceId) ||
                    amount > 0 || research.IsCompleted(
                    ResearchCatalog.PrecisionAssemblyId);
            }
            return false;
        }

        private void CaptureRecentFlow()
        {
            netFlowByResource.Clear();
            incomeFlowByResource.Clear();
            expenseFlowByResource.Clear();
            PruneRecentFlowSamples();
            foreach (InventoryChangeSample sample in recentInventoryChanges)
                AddNetFlow(sample.ResourceId, sample.Delta);
        }

        private void PruneRecentFlowSamples()
        {
            float now = Time.unscaledTime;
            while (recentInventoryChanges.Count > 0 &&
                   now - recentInventoryChanges.Peek().Timestamp >=
                       RecentFlowWindowSeconds)
            {
                recentInventoryChanges.Dequeue();
            }
        }

        private ulong ComputeViewFingerprint()
        {
            ulong value = 1469598103934665603ul;
            MixFingerprint(ref value, view != null && view.IsInventoryOpen);
            MixFingerprint(ref value, view != null && view.IsResearchOpen);
            MixFingerprint(ref value, view != null && view.IsLedgerOpen);
            MixFingerprint(ref value, selectedBackpackSlot);
            MixFingerprint(ref value, selectedBackpackPlacesOne);
            MixFingerprint(ref value, hoveredResourceId);
            MixFingerprint(ref value, selectedResearchId);
            MixFingerprint(ref value, selectedProductionId);
            MixFingerprint(ref value, selectedWarehouseId);
            MixFingerprint(ref value, recentInventoryChanges.Count);
            MixFingerprint(ref value, ActiveWarehouseCount());
            for (int resourceIndex = 0;
                 resourceIndex < ResourceDefinitionCatalog.All.Count;
                 resourceIndex++)
            {
                ResourceDefinition definition =
                    ResourceDefinitionCatalog.All[resourceIndex];
                MixFingerprint(
                    ref value,
                    session.CityStorage.GetNetworkAmount(definition.Id));
            }
            if (backpack != null)
            {
                for (int index = 0; index < backpack.SlotCount; index++)
                {
                    BackpackSlot slot = backpack.GetSlot(index);
                    MixFingerprint(ref value, slot.ResourceId);
                    MixFingerprint(ref value, slot.Amount);
                }
            }
            if (crafting != null)
            {
                MixFingerprint(ref value, crafting.QueuedExecutionCount);
                MixFingerprint(ref value, crafting.ActiveRecipeId);
                MixFingerprint(ref value, Mathf.FloorToInt(
                    crafting.ActiveProgressSeconds * 10f));
                MixFingerprint(ref value, (int)crafting.BlockReason);
            }
            if (research?.Model != null)
            {
                MixFingerprint(ref value, research.Model.Active?.Id.Value);
                MixFingerprint(ref value, Mathf.FloorToInt(
                    research.Model.Remaining * 10f));
            }
            MixFingerprint(ref value, session.CatalogRevision);
            MixFingerprint(ref value, session.CityStorage.Revision);
            MixFingerprint(ref value, city == null ? 0 : (int)city.Mode);
            MixFingerprint(ref value, Time.timeScale <= 0f);
            MixFingerprint(ref value, production?.Clock?.Revision ?? 0ul);
            return value;
        }

        private static void MixFingerprint(ref ulong value, bool item)
        {
            MixFingerprint(ref value, item ? 1 : 0);
        }

        private static void MixFingerprint(ref ulong value, int item)
        {
            unchecked
            {
                value ^= (uint)item;
                value *= 1099511628211ul;
            }
        }

        private static void MixFingerprint(ref ulong value, uint item)
        {
            MixFingerprint(ref value, unchecked((int)item));
        }

        private static void MixFingerprint(ref ulong value, ulong item)
        {
            MixFingerprint(ref value, unchecked((int)item));
            MixFingerprint(ref value, unchecked((int)(item >> 32)));
        }

        private static void MixFingerprint(ref ulong value, string item)
        {
            MixFingerprint(
                ref value,
                string.IsNullOrEmpty(item) ? 0 : item.GetHashCode());
        }

        private void AddNetFlow(string resourceId, float amount)
        {
            netFlowByResource.TryGetValue(resourceId, out float current);
            netFlowByResource[resourceId] = current + amount;
            Dictionary<string, float> direction = amount >= 0f
                ? incomeFlowByResource
                : expenseFlowByResource;
            direction.TryGetValue(resourceId, out float directionalCurrent);
            direction[resourceId] = directionalCurrent + Mathf.Abs(amount);
        }

        private float NetFlow(string resourceId)
        {
            return netFlowByResource.TryGetValue(
                resourceId,
                out float value)
                ? value
                : 0f;
        }

        private void RefreshProductionStates()
        {
            if (!string.IsNullOrWhiteSpace(selectedWarehouseId) &&
                session.CityStorage.TryGetWarehouseSnapshot(
                    selectedWarehouseId,
                    out WarehouseStorageSnapshot warehouse))
            {
                view.SetProductionStateCount(0);
                view.SetWarehouseDetail(
                    warehouse,
                    warehouseFilterFeedback,
                    visible: true);
                return;
            }
            view.SetWarehouseDetail(null, string.Empty, visible: false);
            IReadOnlyList<ProductionBuildingObservability> states =
                production?.Clock?.Snapshot?.Entries;
            int count = states?.Count ?? 0;
            view.SetProductionStateCount(count);
            for (int index = 0;
                 index < count;
                 index++)
            {
                ProductionBuildingObservability state = states[index];
                TryFindBuildingInstance(
                    state.StableInstanceId,
                    out GrayboxBuildingInstance3D instance);
                view.SetProductionState(
                    index,
                    state.StableInstanceId,
                    ProductionTitle(state),
                    state.ProgressNormalized,
                    ProductionStatusText(state, instance),
                    ProductionInputText(state),
                    ProductionOutputText(state),
                    visible: string.IsNullOrEmpty(selectedProductionId) ||
                        string.Equals(
                            selectedProductionId,
                            state.StableInstanceId,
                            StringComparison.Ordinal));
                view.SetProductionResourceIcons(
                    index,
                    state.InputResourceId,
                    state.OutputResourceId);
                view.SetProductionPaused(
                    index,
                    state.IsPlayerPaused,
                    instance != null &&
                    instance.State == GrayboxBuildingInstanceState.Completed &&
                    instance.IsPlayerOwned &&
                    !instance.IsEvacuationLocked);
                view.SetProductionAccessStatus(
                    index,
                    productionAccessStatus.TryGetValue(
                        state.StableInstanceId,
                        out string accessStatus)
                        ? accessStatus
                        : string.Empty);
            }
        }

        private void SetWarehouseFilter(
            string stableInstanceId,
            string resourceId)
        {
            if (!EnsureReady() || !string.Equals(
                    stableInstanceId,
                    selectedWarehouseId,
                    StringComparison.Ordinal) ||
                !TryFindBuildingInstance(
                    stableInstanceId,
                    out GrayboxBuildingInstance3D instance) ||
                instance.State != GrayboxBuildingInstanceState.Completed ||
                !instance.IsPlayerOwned || instance.IsEvacuationLocked)
            {
                warehouseFilterFeedback = "设置失败：仓库状态已变化";
                RefreshView();
                return;
            }
            if (!CanAccessBuildingInventory(instance))
            {
                warehouseFilterFeedback =
                    "设置失败：需由城市或已招募领队在 2 格内操作";
                RefreshView();
                return;
            }
            bool changed = session.CityStorage.TrySetWarehouseFilter(
                stableInstanceId,
                resourceId);
            warehouseFilterFeedback = changed
                ? "过滤设置成功"
                : "设置失败：现有内容与目标过滤不兼容，不能切换";
            RefreshView();
        }

        private void ToggleProductionPause(string stableInstanceId)
        {
            if (!TryFindProductionDetails(
                    stableInstanceId,
                    out ProductionBuildingObservability state) ||
                !TryFindBuildingInstance(
                    stableInstanceId,
                    out GrayboxBuildingInstance3D instance) ||
                instance.State != GrayboxBuildingInstanceState.Completed ||
                !instance.IsPlayerOwned || instance.IsEvacuationLocked)
            {
                return;
            }
            production.Clock.Commands.TrySetPlayerPaused(
                stableInstanceId,
                !state.IsPlayerPaused);
            RefreshView();
        }

        private void TransferProductionCache(
            string stableInstanceId,
            bool input,
            bool useBackpack)
        {
            if (!EnsureReady() ||
                !TryFindProductionDetails(
                    stableInstanceId,
                    out ProductionBuildingObservability state) ||
                !TryFindBuildingInstance(
                    stableInstanceId,
                    out GrayboxBuildingInstance3D instance))
            {
                return;
            }

            if (!CanAccessBuildingInventory(instance))
            {
                productionAccessStatus[stableInstanceId] =
                    "无法访问：需由城市或已招募领队在 2 格内操作";
                RefreshView();
                return;
            }

            if (!useBackpack && !state.IsLogisticsConnected)
            {
                productionAccessStatus[stableInstanceId] =
                    "无法访问：不在物流范围，城市库存不可用";
                RefreshView();
                return;
            }

            string resourceId = input
                ? state.InputResourceId
                : state.OutputResourceId;
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                productionAccessStatus[stableInstanceId] =
                    input ? "无需输入" : "无法访问：无有效输出资源";
                RefreshView();
                return;
            }

            ResourceTransferResult result;
            if (input)
            {
                int requested = useBackpack
                    ? BackpackAmount(resourceId)
                    : session.CityStorage.GetNetworkAmount(resourceId);
                result = useBackpack
                    ? production.Clock.Commands.TransferInputFromBackpack(
                        stableInstanceId,
                        backpack,
                        resourceId,
                        requested,
                        accessValidated: true)
                    : production.Clock.Commands.TransferInputFromCityStorage(
                        stableInstanceId,
                        resourceId,
                        requested,
                        accessValidated: true);
            }
            else
            {
                int requested = state.OutputAmount;
                result = useBackpack
                    ? production.Clock.Commands.TransferOutputToBackpack(
                        stableInstanceId,
                        backpack,
                        resourceId,
                        requested,
                        accessValidated: true)
                    : production.Clock.Commands.TransferOutputToCityStorage(
                        stableInstanceId,
                        resourceId,
                        requested,
                        accessValidated: true);
            }

            productionAccessStatus[stableInstanceId] =
                ProductionTransferStatusText(result);
            RefreshView();
        }

        private bool TryFindProductionDetails(
            string stableInstanceId,
            out ProductionBuildingObservability state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                production?.Clock?.Snapshot != null &&
                production.Clock.Snapshot.TryGet(
                    stableInstanceId,
                    out state);
        }

        private bool TryFindBuildingInstance(
            string stableInstanceId,
            out GrayboxBuildingInstance3D instance)
        {
            instance = null;
            IReadOnlyList<GrayboxBuildingInstance3D> instances =
                session?.Instances;
            if (instances == null ||
                string.IsNullOrWhiteSpace(stableInstanceId))
            {
                return false;
            }
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D candidate = instances[index];
                if (candidate != null && string.Equals(
                        candidate.StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                {
                    instance = candidate;
                    return true;
                }
            }
            return false;
        }

        private bool CanAccessBuildingInventory(
            GrayboxBuildingInstance3D instance)
        {
            if (instance?.Placement?.Definition == null ||
                directControl == null)
            {
                return false;
            }

            DirectControlTarget target = directControl.ControlTarget;
            PlacedBuilding placement = instance.Placement;
            if (!TryGetControlledGroundPosition(
                    target,
                    out float controlledX,
                    out float controlledY))
            {
                return false;
            }

            int cityX = 0;
            int cityY = 0;
            if (placement.Site == BuildingSite.InnerCity &&
                (worldView?.Coordinates == null || city == null ||
                 !worldView.Coordinates.TryWorldToCell(
                     city.transform.position,
                     out cityX,
                     out cityY)))
            {
                return false;
            }

            return EvaluateManualBuildingAccess(
                target,
                leader != null && leader.Model.Recruited,
                controlledX,
                controlledY,
                placement.Site,
                placement.X,
                placement.Y,
                BuildingOrientationRules.Width(
                    placement.Definition,
                    placement.Orientation),
                BuildingOrientationRules.Height(
                    placement.Definition,
                    placement.Orientation),
                cityX,
                cityY,
                instance.State == GrayboxBuildingInstanceState.Completed,
                instance.IsPlayerOwned,
                instance.IsEvacuationLocked);
        }

        public static bool EvaluateManualBuildingAccess(
            DirectControlTarget controlTarget,
            bool leaderRecruited,
            float controlledX,
            float controlledY,
            BuildingSite site,
            int placementX,
            int placementY,
            int footprintWidth,
            int footprintHeight,
            int cityX,
            int cityY,
            bool completed,
            bool playerOwned,
            bool evacuationLocked)
        {
            if (site == BuildingSite.InnerCity)
            {
                placementX = cityX -
                    BuildingRangeRules.CityGroundFootprintRadius;
                placementY = cityY -
                    BuildingRangeRules.CityGroundFootprintRadius;
                footprintWidth =
                    BuildingRangeRules.CityGroundFootprintSize;
                footprintHeight =
                    BuildingRangeRules.CityGroundFootprintSize;
            }
            else if (site != BuildingSite.Ground)
            {
                return false;
            }

            return ManualResourceAccessRules.EvaluateBuildingInventory(
                controlTarget,
                leaderRecruited,
                controlledX,
                controlledY,
                placementX,
                placementY,
                footprintWidth,
                footprintHeight,
                completed,
                playerOwned,
                evacuationLocked);
        }

        private bool TryGetControlledGroundPosition(
            DirectControlTarget target,
            out float controlledX,
            out float controlledY)
        {
            controlledX = 0f;
            controlledY = 0f;
            if (worldView?.Coordinates == null) return false;
            if (target == DirectControlTarget.City)
            {
                if (!worldView.Coordinates.TryWorldToCell(
                        city.transform.position,
                        out int cityX,
                        out int cityY))
                {
                    return false;
                }
                controlledX = cityX;
                controlledY = cityY;
                return true;
            }
            if (target != DirectControlTarget.Leader || leader == null ||
                !leader.Model.Recruited)
            {
                return false;
            }

            Vector2 leaderPlane = worldView.Coordinates.WorldToPlane(
                leader.transform.position);
            controlledX =
                leaderPlane.x + worldView.Coordinates.Width * .5f;
            controlledY =
                leaderPlane.y + worldView.Coordinates.Height * .5f;
            return true;
        }

        private static string ProductionTransferStatusText(
            ResourceTransferResult result)
        {
            return TransferStatusText(result);
        }

        private static string TransferStatusText(
            ResourceTransferResult result)
        {
            if (result.Status == ResourceTransferStatus.Partial)
                return "部分转移 " + result.MovedAmount +
                    "，剩余 " + result.RemainingAmount +
                    "（目标容量不足）";
            if (result.Succeeded)
                return "已转移 " + result.MovedAmount;
            switch (result.Status)
            {
                case ResourceTransferStatus.SourceEmpty:
                    return "无法转移：来源库存为空";
                case ResourceTransferStatus.TargetFull:
                    return "无法转移：目标库存已满";
                case ResourceTransferStatus.InvalidRequest:
                    return "无法转移：请求无效";
                default:
                    return "无法转移：事务未提交";
            }
        }

        private int BackpackAmount(string resourceId)
        {
            int total = 0;
            for (int index = 0; index < backpack.SlotCount; index++)
            {
                BackpackSlot slot = backpack.GetSlot(index);
                if (string.Equals(
                        slot.ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    total += slot.Amount;
            }
            return total;
        }

        private string ProductionTitle(
            ProductionBuildingObservability state)
        {
            string buildingName = state.BuildingDefinitionId;
            for (int index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition candidate = BuildingCatalog.All[index];
                if (!string.Equals(
                        candidate.Id.Value,
                        state.BuildingDefinitionId,
                        StringComparison.Ordinal))
                    continue;
                buildingName = candidate.Name;
                break;
            }
            string recipe = !string.IsNullOrWhiteSpace(
                    state.BoundResourceNodeId)
                ? "采集 " + ResourceName(state.OutputResourceId)
                : ResourceName(state.InputResourceId) + " → " +
                  ResourceName(state.OutputResourceId);
            return buildingName + " · " + recipe;
        }

        private static string ProductionInputText(
            ProductionBuildingObservability state)
        {
            if (string.IsNullOrWhiteSpace(state.InputResourceId) ||
                state.InputRequiredPerCycle <= 0)
            {
                return "输入：无";
            }
            return "输入：" + ResourceName(state.InputResourceId) +
                " " + state.InputAmount + "/" + state.InputCapacity;
        }

        private static string ProductionOutputText(
            ProductionBuildingObservability state)
        {
            string result = "输出：" + ResourceName(
                state.OutputResourceId) + " " + state.OutputAmount + "/" +
                state.OutputCapacity;
            if (!string.IsNullOrWhiteSpace(state.BoundResourceNodeId))
                result += " · 节点剩余 " +
                    state.BoundResourceRemaining;
            return result;
        }

        private static string ProductionStatusText(
            ProductionBuildingObservability state,
            GrayboxBuildingInstance3D instance)
        {
            if (instance != null && instance.IsEvacuationLocked)
                return "撤离处理中 · 已脱离物流";
            ProductionStopReason reason = state.IsPlayerPaused
                ? ProductionStopReason.PlayerPaused
                : state.StopReason;
            string primary;
            switch (reason)
            {
                case ProductionStopReason.MissingInput:
                    primary = "缺少输入";
                    break;
                case ProductionStopReason.OutputFull:
                    primary = "输出已满";
                    break;
                case ProductionStopReason.OutOfLogistics:
                    primary = "不在物流范围";
                    break;
                case ProductionStopReason.Depleted:
                    primary = "矿脉已枯竭";
                    break;
                case ProductionStopReason.PlayerPaused:
                    primary = "玩家暂停运行";
                    break;
                default:
                    primary = "运行中";
                    break;
            }
            return primary + " · " + (state.IsLogisticsConnected
                ? "物流已连接"
                : "已脱离物流（本地缓存）");
        }

        private void RefreshResourceTooltip()
        {
            if (view == null || string.IsNullOrWhiteSpace(hoveredResourceId) ||
                !ResourceDefinitionCatalog.TryGet(
                    hoveredResourceId,
                    out ResourceDefinition definition))
            {
                view?.SetResourceTooltip(string.Empty, visible: false);
                return;
            }

            int warehouses = ActiveWarehouseCount();
            int amount = session.CityStorage.GetNetworkAmount(
                hoveredResourceId);
            int acceptable = session.CityStorage.GetNetworkAcceptableSpace(
                hoveredResourceId);
            int capacity = session.CityStorage.GetNetworkCapacityLimit(
                hoveredResourceId);
            float income = FlowAmount(
                incomeFlowByResource,
                hoveredResourceId);
            float expense = FlowAmount(
                expenseFlowByResource,
                hoveredResourceId);
            float net = NetFlow(hoveredResourceId);
            string capacityText = "容量：基础核心 " +
                ResourceCapacityPolicy.FormalBaseCapacityPerResource +
                " · 联网仓库 " + warehouses +
                " · 当前网络 " + amount + "/" + capacity +
                " · 可接收 " + acceptable;
            string flowText = "近期收入 " + FormatRate(income) +
                " · 近期支出 " + FormatRate(expense) +
                " · 近期净值 " + FormatSignedRate(net);
            string attributionText = RecentFlowAttributionText(
                hoveredResourceId);
            string etaText = ResourceEtaText(
                hoveredResourceId,
                amount,
                capacity,
                net);
            view.SetResourceTooltip(
                definition.ChineseName + "\n" + capacityText + "\n" +
                flowText + "\n" + attributionText + "\n" + etaText,
                visible: true);
        }

        private string RecentFlowAttributionText(string resourceId)
        {
            recentIncomeSources.Clear();
            recentExpenseDestinations.Clear();
            foreach (InventoryChangeSample sample in recentInventoryChanges)
            {
                if (!string.Equals(
                        sample.ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string label = FlowAttributionLabel(sample.Attribution);
                List<string> labels = sample.Delta > 0
                    ? recentIncomeSources
                    : recentExpenseDestinations;
                if (!labels.Contains(label)) labels.Add(label);
            }
            return "收入来源：" + JoinFlowLabels(recentIncomeSources) +
                " · 支出去向：" +
                JoinFlowLabels(recentExpenseDestinations);
        }

        private static string JoinFlowLabels(IReadOnlyList<string> labels)
        {
            return labels.Count == 0 ? "暂无" : string.Join("、", labels);
        }

        private static string FlowAttributionLabel(
            ResourceChangeAttribution attribution)
        {
            if (attribution.Kind ==
                ResourceChangeAttributionKind.Production)
            {
                for (var index = 0;
                     index < BuildingCatalog.All.Length;
                     index++)
                {
                    BuildingDefinition definition = BuildingCatalog.All[index];
                    if (string.Equals(
                            definition.Id.Value,
                            attribution.ReferenceId,
                            StringComparison.Ordinal))
                    {
                        return definition.Name;
                    }
                }
                return "生产建筑";
            }
            if (attribution.Kind == ResourceChangeAttributionKind.Backpack)
                return "个人背包";
            if (attribution.Kind == ResourceChangeAttributionKind.Research)
                return "科技研究";
            if (attribution.Kind == ResourceChangeAttributionKind.Defense)
                return "机枪塔";
            return "其他变动";
        }

        private string ResourceEtaText(
            string resourceId,
            int amount,
            int capacity,
            float net)
        {
            bool stable = !Mathf.Approximately(net, 0f);
            if (!stable) return "预计变化：不可估算";
            if (net > 0f)
            {
                float seconds = Mathf.Max(0f, capacity - amount) / net;
                return "预计满仓：" + seconds.ToString("0.#") + " 秒";
            }
            float depletedSeconds = Mathf.Max(0f, amount) / -net;
            return "预计耗尽：" + depletedSeconds.ToString("0.#") + " 秒";
        }

        private static float FlowAmount(
            IReadOnlyDictionary<string, float> values,
            string resourceId)
        {
            return values.TryGetValue(resourceId, out float value)
                ? value
                : 0f;
        }

        private static string FormatRate(float value)
        {
            return Mathf.Max(0f, value).ToString("0.##") + "/s";
        }

        private static string FormatSignedRate(float value)
        {
            return value.ToString("+0.##;-0.##;0") + "/s";
        }

        private static string ResourceName(string resourceId)
        {
            return ResourceDefinitionCatalog.TryGet(
                    resourceId,
                    out ResourceDefinition definition)
                ? definition.ChineseName
                : "未知资源";
        }

        private readonly struct InventoryChangeSample
        {
            public InventoryChangeSample(
                float timestamp,
                string resourceId,
                int delta,
                ResourceChangeAttribution attribution)
            {
                Timestamp = timestamp;
                ResourceId = resourceId;
                Delta = delta;
                Attribution = attribution;
            }

            public float Timestamp { get; }
            public string ResourceId { get; }
            public int Delta { get; }
            public ResourceChangeAttribution Attribution { get; }
        }

        private bool CanAccessCityInventory()
        {
            if (directControl == null) return false;

            DirectControlTarget target = directControl.ControlTarget;
            if (target == DirectControlTarget.City)
            {
                return ManualResourceAccessRules.EvaluateCityInventory(
                    target,
                    leaderRecruited: false,
                    controlledX: 0f,
                    controlledY: 0f,
                    footprintX: 0,
                    footprintY: 0,
                    footprintWidth:
                        BuildingRangeRules.CityGroundFootprintSize,
                    footprintHeight:
                        BuildingRangeRules.CityGroundFootprintSize);
            }

            if (worldView?.Coordinates == null || leader == null ||
                !worldView.Coordinates.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY))
            {
                return false;
            }

            Vector2 leaderPlane = worldView.Coordinates.WorldToPlane(
                leader.transform.position);
            float leaderGridX =
                leaderPlane.x + worldView.Coordinates.Width * .5f;
            float leaderGridY =
                leaderPlane.y + worldView.Coordinates.Height * .5f;
            return ManualResourceAccessRules.EvaluateCityInventory(
                target,
                leader.Model.Recruited,
                leaderGridX,
                leaderGridY,
                cityX - BuildingRangeRules.CityGroundFootprintRadius,
                cityY - BuildingRangeRules.CityGroundFootprintRadius,
                BuildingRangeRules.CityGroundFootprintSize,
                BuildingRangeRules.CityGroundFootprintSize);
        }

        private static string CraftingBlockReasonText(
            CraftingQueueBlockReason reason)
        {
            return reason == CraftingQueueBlockReason.OutputFull
                ? "输出已满"
                : string.Empty;
        }
    }
}
