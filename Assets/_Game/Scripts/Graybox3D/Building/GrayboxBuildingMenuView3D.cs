using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingMenuView3D : MonoBehaviour
    {
        private enum EvacuationViewMode
        {
            None,
            Legacy,
            Manifest,
            Queue
        }

        private static readonly Color PanelColor =
            new Color(.08f, .1f, .11f, .92f);
        private static readonly Color ButtonColor =
            new Color(.2f, .24f, .25f, .96f);
        private static readonly Color LockedColor =
            new Color(.25f, .22f, .2f, .96f);

        [SerializeField] private Canvas canvas;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxBuildingInteractionModel3D interaction;
        [SerializeField] private ResourceIconCatalog3D resourceIconCatalog;
        [SerializeField]
        private GrayboxBuildingPlacementController3D placement;

        private GrayboxBuildingCatalogPresenter3D presenter;
        private GrayboxUiInputGuard3D inputGuard;
        private RectTransform uiRoot;
        private RectTransform quickbarRoot;
        private RectTransform catalogRoot;
        private RectTransform catalogCardsRoot;
        private RectTransform constructionRoot;
        private RectTransform evacuationRoot;
        private RectTransform placementStatusRoot;
        private Text placementStatusText;
        private RectTransform miningGuidanceLegendRoot;
        private InputField searchField;
        private BuildingMenuCategory? category;
        private ContentRoute? route;
        private string searchText = string.Empty;
        private string selectionFailureMessage = string.Empty;
        private GrayboxBuildingInteractionState selectionFailureState;
        private uint selectionFailureCatalogRevision;
        private bool hasPlacementStatusCache;
        private GrayboxBuildingInteractionState lastPlacementState;
        private BuildingPlacementFailure lastPlacementFailure;
        private bool lastPlacementValid;
        private string lastPlacementBuildingId;
        private BuildingOrientation lastPlacementOrientation;
        private ulong lastPlacementStorageRevision;
        private bool hasCatalogRevision;
        private uint lastCatalogRevision;
        private bool constructionCancellationBlocked;
        private ulong lastEvacuationViewRevision;
        private EvacuationViewMode lastEvacuationViewMode;
        private Button cancelConstructionButton;
        private Button confirmCancellationButton;
        private Button rejectCancellationButton;
        private readonly Dictionary<Keyboard, Action<char>>
            textInputBindings =
                new Dictionary<Keyboard, Action<char>>();
        private bool observesInputDevices;

        public bool CatalogVisible
        {
            get
            {
                SyncCatalogVisibility();
                return catalogRoot != null &&
                       catalogRoot.gameObject.activeSelf;
            }
        }

        public bool EvacuationVisible =>
            evacuationRoot != null &&
            evacuationRoot.gameObject.activeSelf;
        public bool ConstructionCancellationBlocked =>
            constructionCancellationBlocked;
        public string SearchText => searchText;
        public string DeploymentFailureMessage { get; private set; }
        public ulong EvacuationRenderedRevision =>
            lastEvacuationViewRevision;

        public event Action CancelSelectedConstructionRequested;
        public event Action<bool>
            CancelConstructionConfirmationResolved;
        public event Action<string, BuildingEvacuationTreatment>
            EvacuationItemTreatmentRequested;
        public event Action<BuildingMenuCategory, BuildingEvacuationTreatment>
            EvacuationCategoryTreatmentRequested;
        public event Action<BuildingEvacuationTreatment>
            EvacuationAllTreatmentRequested;
        public event Action EvacuationConfirmationRequested;
        public event Action EvacuationRetryRequested;

        private void Awake()
        {
            EnsureRuntimeServices();
            TryBuildSerializedUi();
        }

        private void OnEnable()
        {
            TryBuildSerializedUi();
            BindTextInput();
        }

        private void OnDisable()
        {
            UnbindTextInput();
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(selectionFailureMessage) &&
                (interaction == null ||
                 interaction.State != selectionFailureState ||
                 session == null ||
                 session.CatalogRevision !=
                 selectionFailureCatalogRevision))
            {
                DiscardSelectionFailure();
            }
            if (IsConfigured &&
                (!hasCatalogRevision ||
                 session.CatalogRevision != lastCatalogRevision))
                RefreshCatalog();
            SyncCatalogVisibility();
            RefreshPlacementStatus();
            SyncMiningGuidanceLegend();
        }

        private void OnDestroy()
        {
            UnbindTextInput();
            if (uiRoot != null)
            {
                uiRoot.gameObject.SetActive(false);
                DestroyGenerated(uiRoot.gameObject);
            }
            uiRoot = null;
            quickbarRoot = null;
            catalogRoot = null;
            catalogCardsRoot = null;
            constructionRoot = null;
            evacuationRoot = null;
            placementStatusRoot = null;
            placementStatusText = null;
            miningGuidanceLegendRoot = null;
            searchField = null;
            selectionFailureMessage = string.Empty;
            selectionFailureState = default;
            selectionFailureCatalogRevision = 0u;
            cancelConstructionButton = null;
            confirmCancellationButton = null;
            rejectCancellationButton = null;
            CancelSelectedConstructionRequested = null;
            CancelConstructionConfirmationResolved = null;
            EvacuationItemTreatmentRequested = null;
            EvacuationCategoryTreatmentRequested = null;
            EvacuationAllTreatmentRequested = null;
            EvacuationConfirmationRequested = null;
            EvacuationRetryRequested = null;
            ResetEvacuationRenderCache();
            canvas = null;
            eventSystem = null;
            session = null;
            interaction = null;
            placement = null;
        }

        public void Configure(
            Canvas canvas,
            EventSystem eventSystem,
            GrayboxBuildingSession3D session,
            GrayboxBuildingInteractionModel3D interaction)
        {
            ConfigureReferences(
                canvas,
                eventSystem,
                session,
                interaction);
        }

        public void Configure(
            Canvas canvas,
            EventSystem eventSystem,
            GrayboxBuildingSession3D session,
            GrayboxBuildingInteractionModel3D interaction,
            GrayboxBuildingPlacementController3D placement)
        {
            if (placement == null)
                throw new ArgumentNullException(nameof(placement));
            this.placement = placement;
            ConfigureReferences(
                canvas,
                eventSystem,
                session,
                interaction);
        }

        public void SetPlacementController(
            GrayboxBuildingPlacementController3D placement)
        {
            if (placement == null)
                throw new ArgumentNullException(nameof(placement));
            this.placement = placement;
            hasPlacementStatusCache = false;
            RefreshPlacementStatus();
        }

        public void ShowDeploymentFailure(string message)
        {
            DeploymentFailureMessage = string.IsNullOrEmpty(message)
                ? string.Empty
                : message;
            hasPlacementStatusCache = false;
            RefreshPlacementStatus();
        }

        public void ClearDeploymentFailure()
        {
            DeploymentFailureMessage = string.Empty;
            hasPlacementStatusCache = false;
            RefreshPlacementStatus();
        }

        public static string PlacementFailureMessage(
            BuildingPlacementFailure failure)
        {
            switch (failure)
            {
                case BuildingPlacementFailure.None:
                    return "可以放置";
                case BuildingPlacementFailure.MissingReference:
                    return "无法放置：缺少必要引用";
                case BuildingPlacementFailure.ProjectionFailed:
                    return "无法放置：未命中建造表面";
                case BuildingPlacementFailure.OutOfBounds:
                    return "无法放置：超出网格边界";
                case BuildingPlacementFailure.UnsupportedSite:
                    return "无法放置：建筑不支持当前区域";
                case BuildingPlacementFailure.InvalidCityMode:
                    return "无法放置：当前城市形态不可施工";
                case BuildingPlacementFailure.OutsideBuildRange:
                    return "无法放置：超出城市建造范围";
                case BuildingPlacementFailure.Overlap:
                    return "无法放置：与已有建筑重叠";
                case BuildingPlacementFailure.CityOccupied:
                    return "无法放置：占用移动城市范围";
                case BuildingPlacementFailure.InvalidTerrain:
                    return "无法放置：地形不允许施工";
                case BuildingPlacementFailure.Obstacle:
                    return "无法放置：存在地形障碍";
                case BuildingPlacementFailure.IncompatibleResourceNode:
                    return "无法放置：缺少兼容资源节点";
                case BuildingPlacementFailure.ContentUnavailable:
                    return "无法放置：内容尚未开放";
                case BuildingPlacementFailure.PopulationRequired:
                    return "无法放置：人口条件不足";
                case BuildingPlacementFailure.PrerequisiteBuildingRequired:
                    return "无法放置：前置建筑未完成";
                case BuildingPlacementFailure.InsufficientMaterials:
                    return "无法放置：材料不足";
                default:
                    return "无法放置：未知原因";
            }
        }

        private void ConfigureReferences(
            Canvas canvas,
            EventSystem eventSystem,
            GrayboxBuildingSession3D session,
            GrayboxBuildingInteractionModel3D interaction)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (eventSystem == null)
                throw new ArgumentNullException(nameof(eventSystem));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (interaction == null)
                throw new ArgumentNullException(nameof(interaction));

            this.canvas = canvas;
            this.eventSystem = eventSystem;
            this.session = session;
            this.interaction = interaction;
            selectionFailureMessage = string.Empty;
            selectionFailureState = default;
            selectionFailureCatalogRevision = 0u;
            hasPlacementStatusCache = false;
            hasCatalogRevision = false;
            EnsureRuntimeServices();
            if (isActiveAndEnabled)
                RebuildUi();
        }

        public void RefreshCatalog()
        {
            if (!IsConfigured) return;
            RefreshQuickbar();
            RebuildCatalogCards();
            SyncCatalogVisibility();
            lastCatalogRevision = session.CatalogRevision;
            hasCatalogRevision = true;
        }

        public void SetCategory(BuildingMenuCategory category)
        {
            this.category = category;
            if (category != BuildingMenuCategory.Route)
                route = null;
            RebuildCatalogCards();
        }

        public void SetRouteFilter(ContentRoute? route)
        {
            this.route = route;
            if (route.HasValue)
                category = BuildingMenuCategory.Route;
            RebuildCatalogCards();
        }

        public void SetSearchText(string value)
        {
            searchText = value ?? string.Empty;
            if (searchField != null &&
                !string.Equals(
                    searchField.text,
                    searchText,
                    StringComparison.Ordinal))
                searchField.SetTextWithoutNotify(searchText);
            RebuildCatalogCards();
        }

        public bool TrySelectQuickbarSlot(int zeroBasedIndex)
        {
            if (!IsConfigured ||
                zeroBasedIndex < 0 ||
                zeroBasedIndex >=
                GrayboxBuildingCatalogPresenter3D.Quickbar.Count)
                return false;

            BuildingDefinition definition =
                GrayboxBuildingCatalogPresenter3D.Quickbar[zeroBasedIndex];
            GrayboxBuildingCatalogItem3D item =
                presenter.Describe(session, definition);
            if (item.Visibility == BuildingCatalogVisibility.Hidden)
                return false;
            if (item.Visibility == BuildingCatalogVisibility.Locked)
            {
                ShowSelectionFailure(definition, item.PrimaryLockReason);
                return false;
            }
            ClearSelectionFailure();
            interaction.Select(item);
            SyncCatalogVisibility();
            RefreshQuickbar();
            return true;
        }

        public bool TrySelectCatalogItem(string stableBuildingId)
        {
            if (!IsConfigured ||
                string.IsNullOrEmpty(stableBuildingId))
                return false;

            IReadOnlyList<GrayboxBuildingCatalogItem3D> items =
                presenter.Query(session, category, route, searchText);
            for (var index = 0; index < items.Count; index++)
            {
                GrayboxBuildingCatalogItem3D item = items[index];
                if (!string.Equals(
                        item.Definition.Id.Value,
                        stableBuildingId,
                        StringComparison.Ordinal))
                    continue;
                if (item.Visibility != BuildingCatalogVisibility.Buildable)
                {
                    if (item.Visibility == BuildingCatalogVisibility.Locked)
                        ShowSelectionFailure(
                            item.Definition,
                            item.PrimaryLockReason);
                    return false;
                }
                ClearSelectionFailure();
                interaction.Select(item);
                SyncCatalogVisibility();
                RefreshQuickbar();
                return true;
            }
            return false;
        }

        public bool HasKeyboardFocus()
        {
            return inputGuard != null &&
                   inputGuard.HasKeyboardFocus(eventSystem);
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            return inputGuard != null &&
                   inputGuard.IsPointerOverUi(
                       eventSystem,
                       screenPosition);
        }

        public bool ConsumeFocusedEscape()
        {
            return inputGuard != null &&
                   inputGuard.ConsumeFocusedEscape(eventSystem);
        }

        public void ShowEvacuation(
            IReadOnlyList<GrayboxBuildingInstance3D> instances)
        {
            if (instances == null)
                throw new ArgumentNullException(nameof(instances));
            if (!IsConfigured) return;

            lastEvacuationViewMode = EvacuationViewMode.Legacy;
            lastEvacuationViewRevision = 0;
            ClearChildren(evacuationRoot);
            CreateLabel(
                evacuationRoot,
                "Evacuation.Title",
                "撤离处理");
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance == null) continue;
                CreateEvacuationItem(instance);
            }
            foreach (BuildingMenuCategory value in Enum.GetValues(
                         typeof(BuildingMenuCategory)))
                CreateEvacuationCategory(value);
            CreateEvacuationAll();
            CreateButton(
                evacuationRoot,
                "Evacuation.Confirm",
                "确认撤离",
                () => EvacuationConfirmationRequested?.Invoke());
            evacuationRoot.gameObject.SetActive(true);
        }

        public void ShowEvacuationManifest(
            EvacuationManifestViewModel view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (HasRenderedEvacuationView(
                    EvacuationViewMode.Manifest,
                    view.Revision))
            {
                return;
            }
            if (!IsConfigured) return;

            ClearChildren(evacuationRoot);
            CreateLabel(
                evacuationRoot,
                "Evacuation.Title",
                "撤离处理 · " + (view.IsInCombat ? "战斗" : "和平"));
            CreateEvacuationSummary(view);
            RectTransform scrollContent =
                CreateEvacuationManifestScrollContent();
            for (var index = 0; index < view.Items.Count; index++)
            {
                EvacuationManifestItemViewModel item = view.Items[index];
                if (item != null)
                {
                    CreateEvacuationManifestItem(
                        scrollContent,
                        item,
                        view.IsInCombat);
                }
            }
            foreach (BuildingMenuCategory value in Enum.GetValues(
                         typeof(BuildingMenuCategory)))
            {
                CreateEvacuationCategory(scrollContent, value);
            }
            CreateEvacuationAll(scrollContent);
            Button confirm = CreateButton(
                evacuationRoot,
                "Evacuation.Confirm",
                "确认撤离",
                () => EvacuationConfirmationRequested?.Invoke());
            confirm.interactable = view.CanConfirm;
            evacuationRoot.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(evacuationRoot);
            ScrollRect scroll = scrollContent.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
                scroll.Rebuild(CanvasUpdate.PostLayout);
            }
            RememberEvacuationView(
                EvacuationViewMode.Manifest,
                view.Revision);
        }

        public void ShowEvacuationQueue(EvacuationQueueViewModel view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (HasRenderedEvacuationView(
                    EvacuationViewMode.Queue,
                    view.Revision))
            {
                return;
            }
            if (!IsConfigured) return;

            ClearChildren(evacuationRoot);
            CreateLabel(
                evacuationRoot,
                "Evacuation.Title",
                "撤离队列 · " +
                (view.BatchIsInCombat ? "战斗批次" : "和平批次"));
            Text batch = CreateLabel(
                evacuationRoot,
                "Evacuation.Queue.Batch",
                "批次 " + ValueOrDash(view.BatchId) +
                " · 生产率 ×" +
                view.BatchProductivityMultiplier.ToString("0.##"));
            ConfigureEvacuationInfoLabel(batch, 34f);
            Text progress = CreateLabel(
                evacuationRoot,
                "Evacuation.Queue.Progress",
                "进度 " + view.CompletedCount + "/" + view.TotalCount +
                " · 当前 " + ValueOrDash(view.CurrentStableInstanceId));
            ConfigureEvacuationInfoLabel(progress, 42f);
            Text remaining = CreateLabel(
                evacuationRoot,
                "Evacuation.Queue.Remaining",
                "剩余：基础 " + FormatSeconds(view.RemainingBaseSeconds) +
                " / 实际 " + FormatSeconds(view.RemainingActualSeconds) +
                (view.IsPaused ? " · 已暂停" : string.Empty));
            ConfigureEvacuationInfoLabel(remaining, 42f);

            if (view.IsBlocked)
            {
                string failure = string.IsNullOrWhiteSpace(
                        view.LastFailureReason)
                    ? "城市容量不足"
                    : view.LastFailureReason;
                Text blocked = CreateLabel(
                    evacuationRoot,
                    "Evacuation.Queue.Blocked",
                    "队列受阻：" + failure + "\n容量缺口：" +
                    FormatResourceAmounts(view.CapacityShortfalls));
                ConfigureEvacuationInfoLabel(blocked, 66f);

                string hint = "按 E 腾出城市容量";
                if (!string.IsNullOrWhiteSpace(view.CapacityHint) &&
                    !string.Equals(
                        view.CapacityHint,
                        hint,
                        StringComparison.Ordinal))
                {
                    hint += "\n" + view.CapacityHint;
                }
                Text capacityHint = CreateLabel(
                    evacuationRoot,
                    "Evacuation.Queue.CapacityHint",
                    hint);
                ConfigureEvacuationInfoLabel(capacityHint, 52f);
                Button retry = CreateButton(
                    evacuationRoot,
                    "Evacuation.Retry",
                    "重试撤离",
                    () => EvacuationRetryRequested?.Invoke());
                retry.interactable = view.CanRetry;
            }
            else if (!string.IsNullOrWhiteSpace(view.LastFailureReason))
            {
                Text failure = CreateLabel(
                    evacuationRoot,
                    "Evacuation.Queue.Failure",
                    "最近失败：" + view.LastFailureReason);
                ConfigureEvacuationInfoLabel(failure, 42f);
            }

            evacuationRoot.gameObject.SetActive(true);
            RememberEvacuationView(
                EvacuationViewMode.Queue,
                view.Revision);
        }

        public void HideEvacuation()
        {
            ResetEvacuationRenderCache();
            if (evacuationRoot == null) return;
            ClearChildren(evacuationRoot);
            evacuationRoot.gameObject.SetActive(false);
        }

        public void SetConstructionCancellationBlocked(bool blocked)
        {
            constructionCancellationBlocked = blocked;
            bool interactable = !blocked;
            if (cancelConstructionButton != null)
                cancelConstructionButton.interactable = interactable;
            if (confirmCancellationButton != null)
                confirmCancellationButton.interactable = interactable;
            if (rejectCancellationButton != null)
                rejectCancellationButton.interactable = interactable;
        }

        private bool IsConfigured =>
            canvas != null &&
            eventSystem != null &&
            session != null &&
            interaction != null &&
            uiRoot != null;

        private bool HasSerializedUiReferences =>
            canvas != null &&
            eventSystem != null &&
            session != null &&
            interaction != null;

        private void EnsureRuntimeServices()
        {
            if (presenter == null)
                presenter = new GrayboxBuildingCatalogPresenter3D();
            if (inputGuard == null)
                inputGuard = new GrayboxUiInputGuard3D();
        }

        private void TryBuildSerializedUi()
        {
            EnsureRuntimeServices();
            if (uiRoot != null || !HasSerializedUiReferences) return;
            RebuildUi();
        }

        private void RebuildUi()
        {
            if (!HasSerializedUiReferences) return;
            ResetEvacuationRenderCache();
            if (uiRoot != null)
            {
                uiRoot.name = "GrayboxBuildingUi.Retired";
                uiRoot.gameObject.SetActive(false);
                DestroyGenerated(uiRoot.gameObject);
            }
            uiRoot = null;
            quickbarRoot = null;
            catalogRoot = null;
            catalogCardsRoot = null;
            evacuationRoot = null;
            placementStatusRoot = null;
            placementStatusText = null;
            miningGuidanceLegendRoot = null;
            searchField = null;
            hasPlacementStatusCache = false;
            RetireSerializedUiRoots();
            EnsureCanvasContract();
            BuildUi();
            BindTextInput();
            RefreshCatalog();
            RefreshPlacementStatus();
            SyncMiningGuidanceLegend();
        }

        private void BindTextInput()
        {
            if (!isActiveAndEnabled) return;
            if (!observesInputDevices)
            {
                InputSystem.onDeviceChange += OnInputDeviceChange;
                observesInputDevices = true;
            }

            for (var index = 0; index < InputSystem.devices.Count; index++)
                if (InputSystem.devices[index] is Keyboard keyboard)
                    BindKeyboard(keyboard);
        }

        private void UnbindTextInput()
        {
            if (observesInputDevices)
            {
                InputSystem.onDeviceChange -= OnInputDeviceChange;
                observesInputDevices = false;
            }

            foreach (KeyValuePair<Keyboard, Action<char>> binding in
                     textInputBindings)
                binding.Key.onTextInput -= binding.Value;
            textInputBindings.Clear();
        }

        private void OnInputDeviceChange(
            InputDevice device,
            InputDeviceChange change)
        {
            if (!(device is Keyboard keyboard)) return;
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                    BindKeyboard(keyboard);
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    UnbindKeyboard(keyboard);
                    break;
            }
        }

        private void BindKeyboard(Keyboard keyboard)
        {
            if (keyboard == null || textInputBindings.ContainsKey(keyboard))
                return;
            Action<char> callback = character =>
                OnTextInput(keyboard, character);
            textInputBindings.Add(keyboard, callback);
            keyboard.onTextInput += callback;
        }

        private void UnbindKeyboard(Keyboard keyboard)
        {
            if (keyboard == null ||
                !textInputBindings.TryGetValue(
                    keyboard,
                    out Action<char> callback))
                return;
            keyboard.onTextInput -= callback;
            textInputBindings.Remove(keyboard);
        }

        private void OnTextInput(Keyboard source, char character)
        {
            if (!isActiveAndEnabled ||
                source == null ||
                !ReferenceEquals(source, Keyboard.current) ||
                searchField == null ||
                eventSystem == null ||
                eventSystem.currentSelectedGameObject !=
                    searchField.gameObject ||
                !searchField.isFocused)
                return;

            int anchor = Mathf.Clamp(
                searchField.selectionAnchorPosition,
                0,
                searchField.text.Length);
            int focus = Mathf.Clamp(
                searchField.selectionFocusPosition,
                0,
                searchField.text.Length);
            int start = Mathf.Min(anchor, focus);
            int length = Mathf.Abs(anchor - focus);
            searchField.text =
                searchField.text.Remove(start, length)
                    .Insert(start, character.ToString());
            searchField.caretPosition = start + 1;
        }

        private void RetireSerializedUiRoots()
        {
            Transform canvasTransform = canvas.transform;
            for (var index = canvasTransform.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = canvasTransform.GetChild(index);
                if (!string.Equals(
                        child.name,
                        "GrayboxBuildingUi.Root",
                        StringComparison.Ordinal))
                    continue;
                child.name = "GrayboxBuildingUi.Retired";
                child.gameObject.SetActive(false);
                DestroyGenerated(child.gameObject);
            }
        }

        private void EnsureCanvasContract()
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildUi()
        {
            uiRoot = CreateRect(
                canvas.transform,
                "GrayboxBuildingUi.Root");
            Stretch(uiRoot);

            quickbarRoot = CreatePanel(
                uiRoot,
                "Quickbar",
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                new Vector2(0f, 8f),
                new Vector2(620f, 54f));
            var quickbarLayout =
                quickbarRoot.gameObject.AddComponent<GridLayoutGroup>();
            quickbarLayout.cellSize = new Vector2(58f, 46f);
            quickbarLayout.spacing = new Vector2(3f, 0f);
            quickbarLayout.padding = new RectOffset(5, 5, 4, 4);
            quickbarLayout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            quickbarLayout.constraintCount = 10;

            catalogRoot = CreatePanel(
                uiRoot,
                "Catalog",
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                new Vector2(0f, 66f),
                new Vector2(620f, 350f));
            BuildCatalogChrome();

            constructionRoot = CreatePanel(
                uiRoot,
                "Construction",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-8f, 8f),
                new Vector2(190f, 102f));
            var constructionLayout =
                constructionRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            constructionLayout.padding = new RectOffset(5, 5, 5, 5);
            constructionLayout.spacing = 3f;
            constructionLayout.childForceExpandWidth = true;
            constructionLayout.childForceExpandHeight = false;
            cancelConstructionButton = CreateButton(
                constructionRoot,
                "Construction.Cancel",
                "取消选中施工",
                () => CancelSelectedConstructionRequested?.Invoke());
            confirmCancellationButton = CreateButton(
                constructionRoot,
                "Construction.Confirm.Yes",
                "确认取消",
                () => CancelConstructionConfirmationResolved?.Invoke(true));
            rejectCancellationButton = CreateButton(
                constructionRoot,
                "Construction.Confirm.No",
                "返回施工",
                () => CancelConstructionConfirmationResolved?.Invoke(false));
            SetConstructionCancellationBlocked(
                constructionCancellationBlocked);

            float evacuationPanelHeight = Mathf.Min(
                720f,
                Mathf.Max(0f, canvas.pixelRect.height - 24f));
            evacuationRoot = CreatePanel(
                uiRoot,
                "Evacuation",
                new Vector2(0f, .5f),
                new Vector2(0f, .5f),
                new Vector2(8f, 0f),
                new Vector2(560f, evacuationPanelHeight));
            var evacuationLayout =
                evacuationRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            evacuationLayout.spacing = 3f;
            evacuationLayout.padding = new RectOffset(6, 6, 6, 6);
            evacuationLayout.childForceExpandWidth = true;
            evacuationLayout.childForceExpandHeight = false;
            evacuationRoot.gameObject.SetActive(false);

            placementStatusRoot = CreatePanel(
                uiRoot,
                "Placement.Status",
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                new Vector2(0f, 66f),
                new Vector2(480f, 38f));
            placementStatusRoot.GetComponent<Image>().raycastTarget = false;
            placementStatusText = CreateLabel(
                placementStatusRoot,
                "Placement.Status.Text",
                string.Empty);
            placementStatusText.fontSize = 15;
            placementStatusRoot.gameObject.SetActive(false);

            miningGuidanceLegendRoot = CreatePanel(
                uiRoot,
                "Mining.Guidance.Legend",
                new Vector2(.5f, 1f),
                new Vector2(.5f, 1f),
                new Vector2(0f, -52f),
                new Vector2(480f, 58f));
            Text miningLegend = CreateLabel(
                miningGuidanceLegendRoot,
                "Mining.Guidance.Legend.Text",
                "绿色：当前可建造位置\n" +
                "暗黄色：资源兼容，但当前条件不满足");
            miningLegend.fontSize = 14;
            miningGuidanceLegendRoot.gameObject.SetActive(false);
        }

        private void BuildCatalogChrome()
        {
            var catalogLayout =
                catalogRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            catalogLayout.spacing = 4f;
            catalogLayout.padding = new RectOffset(6, 6, 6, 6);

            searchField = CreateInputField(
                catalogRoot,
                "Catalog.Search",
                "搜索可见建筑");
            searchField.onValueChanged.AddListener(SetSearchText);

            RectTransform categories = CreateRect(
                catalogRoot,
                "Catalog.Categories");
            categories.sizeDelta = new Vector2(0f, 34f);
            SetLayout(categories, 0f, 34f, 1f);
            var categoryLayout =
                categories.gameObject.AddComponent<HorizontalLayoutGroup>();
            categoryLayout.spacing = 3f;
            foreach (BuildingMenuCategory value in Enum.GetValues(
                         typeof(BuildingMenuCategory)))
            {
                BuildingMenuCategory captured = value;
                CreateButton(
                    categories,
                    "Category." + value,
                    CategoryLabel(value),
                    () => SetCategory(captured));
            }

            RectTransform routes = CreateRect(
                catalogRoot,
                "Catalog.Routes");
            routes.sizeDelta = new Vector2(0f, 34f);
            SetLayout(routes, 0f, 34f, 1f);
            var routeLayout =
                routes.gameObject.AddComponent<HorizontalLayoutGroup>();
            routeLayout.spacing = 3f;
            ContentRoute[] routeValues =
            {
                ContentRoute.Technology,
                ContentRoute.Cultivation,
                ContentRoute.BiologicalAscension,
                ContentRoute.Psionics
            };
            for (var index = 0; index < routeValues.Length; index++)
            {
                ContentRoute captured = routeValues[index];
                CreateButton(
                    routes,
                    "Route." + captured,
                    RouteLabel(captured),
                    () => SetRouteFilter(captured));
            }

            RectTransform scrollRoot = CreateRect(
                catalogRoot,
                "Catalog.Scroll");
            scrollRoot.sizeDelta = new Vector2(596f, 224f);
            SetLayout(scrollRoot, 596f, 224f, 0f);
            scrollRoot.GetComponent<LayoutElement>().minWidth = 596f;
            scrollRoot.GetComponent<LayoutElement>().minHeight = 224f;
            Image scrollImage =
                scrollRoot.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, .01f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = CreateRect(
                scrollRoot,
                "Catalog.Viewport");
            Stretch(viewport);
            Image viewportImage =
                viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = false;
            viewport.gameObject.AddComponent<RectMask2D>();

            catalogCardsRoot = CreateRect(
                viewport,
                "Catalog.Cards");
            catalogCardsRoot.anchorMin = new Vector2(0f, 1f);
            catalogCardsRoot.anchorMax = new Vector2(1f, 1f);
            catalogCardsRoot.pivot = new Vector2(.5f, 1f);
            catalogCardsRoot.anchoredPosition = Vector2.zero;
            catalogCardsRoot.sizeDelta = Vector2.zero;
            var cardLayout =
                catalogCardsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.spacing = 3f;
            cardLayout.childForceExpandHeight = false;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childControlWidth = true;
            var fitter =
                catalogCardsRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = catalogCardsRoot;
        }

        private void RefreshQuickbar()
        {
            ClearChildren(quickbarRoot);
            for (var index = 0;
                 index < GrayboxBuildingCatalogPresenter3D.Quickbar.Count;
                 index++)
            {
                var captured = index;
                BuildingDefinition definition =
                    GrayboxBuildingCatalogPresenter3D.Quickbar[index];
                GrayboxBuildingCatalogItem3D item =
                    presenter.Describe(session, definition);
                string text = item.Visibility ==
                              BuildingCatalogVisibility.Hidden
                    ? KeyLabel(index)
                    : KeyLabel(index) + "\n" + definition.Name;
                Button button = CreateButton(
                    quickbarRoot,
                    "QuickbarSlot." + index,
                    text,
                    () => TrySelectQuickbarSlot(captured));
                button.interactable =
                    item.Visibility != BuildingCatalogVisibility.Hidden;
                button.image.color =
                    item.Visibility == BuildingCatalogVisibility.Locked
                        ? LockedColor
                        : ButtonColor;
            }
        }

        private void RebuildCatalogCards()
        {
            if (!IsConfigured || catalogCardsRoot == null) return;
            ClearChildren(catalogCardsRoot);
            IReadOnlyList<GrayboxBuildingCatalogItem3D> items =
                presenter.Query(session, category, route, searchText);
            for (var index = 0; index < items.Count; index++)
                CreateCatalogCard(items[index]);
        }

        private void CreateCatalogCard(GrayboxBuildingCatalogItem3D item)
        {
            BuildingDefinition definition = item.Definition;
            Button card = CreateButton(
                catalogCardsRoot,
                "Catalog.Card." + definition.Id.Value,
                string.Empty,
                () => TrySelectCatalogItem(definition.Id.Value));
            card.interactable =
                item.Visibility != BuildingCatalogVisibility.Hidden;
            card.image.color =
                item.Visibility == BuildingCatalogVisibility.Buildable
                    ? ButtonColor
                    : LockedColor;
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(596f, 108f);
            SetLayout(rect, 596f, 108f, 0f);
            rect.GetComponent<LayoutElement>().minWidth = 596f;

            RectTransform summary = CreateRect(rect, "Summary");
            PlaceFixed(summary, new Vector2(6f, 0f), new Vector2(220f, 96f));
            Text name = CreateLabel(
                summary,
                "Name",
                definition.Name);
            AnchorInside(
                name.rectTransform,
                new Vector2(0f, .68f),
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            Text cost = CreateLabel(
                summary,
                "Cost",
                "成本 " + definition.Cost + " " +
                ResourceName(definition.CostId));
            AnchorInside(
                cost.rectTransform,
                new Vector2(0f, .36f),
                new Vector2(1f, .66f),
                Vector2.zero,
                Vector2.zero);
            cost.rectTransform.offsetMin = new Vector2(30f, 0f);
            Image costIcon = CreateResourceIcon(
                summary,
                "Catalog.Card." + definition.Id.Value + ".Cost.Icon",
                definition.CostId);
            AnchorInside(
                costIcon.rectTransform,
                new Vector2(0f, .38f),
                new Vector2(0f, .64f),
                new Vector2(4f, 0f),
                new Vector2(28f, 0f));
            if (!string.IsNullOrEmpty(item.PrimaryLockReason))
            {
                Text reason = CreateLabel(
                    summary,
                    "PrimaryReason",
                    item.PrimaryLockReason);
                AnchorInside(
                    reason.rectTransform,
                    Vector2.zero,
                    new Vector2(1f, .34f),
                    Vector2.zero,
                    Vector2.zero);
            }

            RectTransform details = CreateRect(rect, "Details");
            PlaceFixed(
                details,
                new Vector2(232f, 0f),
                new Vector2(350f, 96f));
            Image detailsBackground =
                details.gameObject.AddComponent<Image>();
            detailsBackground.color =
                new Color(.12f, .15f, .16f, .98f);
            detailsBackground.raycastTarget = false;
            Text detailsText = CreateLabel(
                details,
                "Details.Text",
                BuildDetails(item));
            float detailsHeight = Mathf.Max(
                96f,
                Mathf.Ceil(detailsText.preferredHeight) + 4f);
            float cardHeight = detailsHeight + 12f;
            rect.sizeDelta = new Vector2(596f, cardHeight);
            SetLayout(rect, 596f, cardHeight, 0f);
            PlaceFixed(
                details,
                new Vector2(232f, 0f),
                new Vector2(350f, detailsHeight));
            details.gameObject.SetActive(false);
            EventTrigger trigger =
                card.gameObject.AddComponent<EventTrigger>();
            AddTrigger(
                trigger,
                EventTriggerType.PointerEnter,
                _ => details.gameObject.SetActive(true));
            AddTrigger(
                trigger,
                EventTriggerType.PointerExit,
                _ => details.gameObject.SetActive(false));
        }

        private static string BuildDetails(
            GrayboxBuildingCatalogItem3D item)
        {
            BuildingDefinition definition = item.Definition;
            string reasons = item.LockReasons == null ||
                             item.LockReasons.Count == 0
                ? "无"
                : string.Join("；", item.LockReasons.ToArray());
            var lines = new List<string>
            {
                definition.Name,
                "类别 " + CategoryLabel(item.Category),
                "路线 " + RouteLabel(item.Route),
                "占地 " + definition.Width + "×" + definition.Height,
                "位置 " + BuildingMobilityRules.PlacementName(
                    definition.Placement),
                "运行 " + BuildingMobilityRules.OperationName(
                    definition.Operation),
                "施工 " + definition.BuildSeconds + " 秒",
                "完整成本 " + definition.Cost + " " + definition.CostId
            };
            if (definition.MinimumPopulation > 0)
                lines.Add("最低人口：" + definition.MinimumPopulation);
            lines.Add("研究 " + (definition.RequiredResearchId ?? "无"));
            lines.Add("前置 " + (definition.RequiredBuildingId ?? "无"));
            lines.Add("锁定原因 " + reasons);
            return string.Join("\n", lines);
        }

        private Image CreateResourceIcon(
            Transform parent,
            string name,
            string resourceId)
        {
            RectTransform rect = CreateRect(parent, name);
            var icon = rect.gameObject.AddComponent<Image>();
            icon.sprite = resourceIconCatalog == null
                ? ResourceIconCatalog3D.Resolve(resourceId)
                : resourceIconCatalog.ResolveIcon(resourceId);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private static string ResourceName(string resourceId)
        {
            return ResourceDefinitionCatalog.TryGet(
                    resourceId,
                    out ResourceDefinition definition)
                ? definition.ChineseName
                : resourceId ?? string.Empty;
        }

        private void CreateEvacuationItem(
            GrayboxBuildingInstance3D instance)
        {
            RectTransform row = CreateRect(
                evacuationRoot,
                "Evacuation.Item." + instance.StableInstanceId);
            ConfigureEvacuationRow(row);
            Text label = CreateLabel(
                row,
                "Label",
                instance.Placement.Definition.Name);
            SetLayout(label.rectTransform, 72f, 30f, 0f);
            CreateTreatmentButtons(
                row,
                treatment => EvacuationItemTreatmentRequested?.Invoke(
                    instance.StableInstanceId,
                    treatment));
        }

        private void CreateEvacuationSummary(
            EvacuationManifestViewModel view)
        {
            string status = view.CanConfirm
                ? "可确认"
                : "暂不可确认";
            string details = "环境：" +
                (view.IsInCombat ? "战斗" : "和平") +
                " · 生产率 ×" +
                view.ProductivityMultiplier.ToString("0.##") +
                " · " + status;
            if (view.CapacityShortfalls.Count > 0)
            {
                details += "\n批次容量缺口：" +
                    FormatResourceAmounts(view.CapacityShortfalls);
            }
            if (!string.IsNullOrWhiteSpace(view.FailureReason))
                details += "\n失败：" + view.FailureReason;
            Text summary = CreateLabel(
                evacuationRoot,
                "Evacuation.Summary",
                details);
            ConfigureEvacuationInfoLabel(
                summary,
                view.CapacityShortfalls.Count > 0 ||
                !string.IsNullOrWhiteSpace(view.FailureReason)
                    ? 62f
                    : 38f);
        }

        private RectTransform CreateEvacuationManifestScrollContent()
        {
            RectTransform scrollRoot = CreateRect(
                evacuationRoot,
                "Evacuation.Scroll");
            scrollRoot.sizeDelta = new Vector2(548f, 180f);
            SetLayout(scrollRoot, 548f, 180f, 1f, 1f);
            Image scrollBackground =
                scrollRoot.gameObject.AddComponent<Image>();
            scrollBackground.color = new Color(1f, 1f, 1f, .025f);

            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            RectTransform viewport = CreateRect(
                scrollRoot,
                "Evacuation.Scroll.Viewport");
            Stretch(viewport);
            Image viewportGraphic =
                viewport.gameObject.AddComponent<Image>();
            viewportGraphic.color = new Color(1f, 1f, 1f, .01f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(
                viewport,
                "Evacuation.Scroll.Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 3f;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalNormalizedPosition = 1f;
            return content;
        }

        private void CreateEvacuationManifestItem(
            RectTransform parent,
            EvacuationManifestItemViewModel item,
            bool isInCombat)
        {
            string prefix = "Evacuation.Item." + item.StableInstanceId;
            RectTransform row = CreateRect(parent, prefix);
            SetLayout(row, 0f, 208f, 1f);
            var layout =
                row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = CreateLabel(
                row,
                "Label",
                "建筑：" + ValueOrDash(item.BuildingName) +
                " · " + CategoryLabel(item.Category) +
                " · 状态：" + BuildingStateLabel(item.State) +
                " · " + (isInCombat ? "战斗" : "和平"));
            ConfigureEvacuationInfoLabel(title, 24f);

            string treatment = "处理方式：" +
                TreatmentLabel(item.Treatment) +
                " · 退款：" +
                FormatResourceAmounts(item.ExpectedRefunds) +
                " · 基础/实际耗时：" +
                FormatSeconds(item.BaseDismantleSeconds) + "/" +
                FormatSeconds(item.DismantleSeconds) +
                " · 未完成比例：" +
                FormatPercent(item.RemainingRatio);
            Text work = CreateLabel(row, "Work", treatment);
            ConfigureEvacuationInfoLabel(work, 34f);

            Text payload = CreateLabel(
                row,
                "Payload",
                "输入：" + FormatResourceAmounts(item.Input) +
                " · 预留：" +
                FormatResourceAmounts(item.ReservedInput) +
                " · 输出：" + FormatResourceAmounts(item.Output) +
                " · 弹药：" + Math.Max(0, item.AmmunitionAmount));
            ConfigureEvacuationInfoLabel(payload, 34f);

            Text migration = CreateLabel(
                row,
                "Migration",
                "仓库迁移：" +
                FormatResourceAmounts(item.WarehouseContents) +
                " · 遗弃损失：" +
                FormatResourceAmounts(item.LostOnAbandon));
            ConfigureEvacuationInfoLabel(migration, 34f);

            string capacity = item.CanCommit
                ? "容量：充足"
                : "容量缺口：" +
                  FormatResourceAmounts(item.CapacityShortfalls);
            if (!string.IsNullOrWhiteSpace(item.FailureReason))
                capacity += " · 失败：" + item.FailureReason;
            Text capacityStatus = CreateLabel(
                row,
                "Capacity",
                capacity);
            ConfigureEvacuationInfoLabel(capacityStatus, 34f);

            RectTransform actions = CreateRect(row, prefix + ".Actions");
            ConfigureEvacuationRow(actions);
            CreateTreatmentButtons(
                actions,
                selected => EvacuationItemTreatmentRequested?.Invoke(
                    item.StableInstanceId,
                    selected),
                prefix + ".");
        }

        private void CreateEvacuationCategory(
            BuildingMenuCategory value)
        {
            CreateEvacuationCategory(evacuationRoot, value);
        }

        private void CreateEvacuationCategory(
            RectTransform parent,
            BuildingMenuCategory value)
        {
            RectTransform row = CreateRect(
                parent,
                "Evacuation.Category." + value);
            ConfigureEvacuationRow(row);
            Text label =
                CreateLabel(row, "Label", CategoryLabel(value));
            SetLayout(label.rectTransform, 72f, 30f, 0f);
            CreateTreatmentButtons(
                row,
                treatment =>
                    EvacuationCategoryTreatmentRequested?.Invoke(
                        value,
                        treatment),
                "Evacuation.Category." + value + ".");
        }

        private void CreateEvacuationAll()
        {
            CreateEvacuationAll(evacuationRoot);
        }

        private void CreateEvacuationAll(RectTransform parent)
        {
            RectTransform row = CreateRect(
                parent,
                "Evacuation.All");
            ConfigureEvacuationRow(row);
            Text label = CreateLabel(row, "Label", "全部");
            SetLayout(label.rectTransform, 72f, 30f, 0f);
            CreateTreatmentButtons(
                row,
                treatment =>
                    EvacuationAllTreatmentRequested?.Invoke(treatment),
                "Evacuation.All.");
        }

        private void CreateTreatmentButtons(
            RectTransform parent,
            Action<BuildingEvacuationTreatment> callback,
            string prefix = null)
        {
            BuildingEvacuationTreatment[] values =
            {
                BuildingEvacuationTreatment.Abandon,
                BuildingEvacuationTreatment.FullDismantle,
                BuildingEvacuationTreatment.QuickDismantle
            };
            string actualPrefix = prefix ?? parent.name + ".";
            for (var index = 0; index < values.Length; index++)
            {
                BuildingEvacuationTreatment captured = values[index];
                CreateButton(
                    parent,
                    actualPrefix + captured,
                    TreatmentLabel(captured),
                    () => callback(captured));
            }
        }

        private bool HasRenderedEvacuationView(
            EvacuationViewMode mode,
            ulong revision)
        {
            return lastEvacuationViewMode == mode &&
                   lastEvacuationViewRevision == revision;
        }

        private void RememberEvacuationView(
            EvacuationViewMode mode,
            ulong revision)
        {
            lastEvacuationViewMode = mode;
            lastEvacuationViewRevision = revision;
        }

        private void ResetEvacuationRenderCache()
        {
            lastEvacuationViewMode = EvacuationViewMode.None;
            lastEvacuationViewRevision = 0;
        }

        private static void ConfigureEvacuationInfoLabel(
            Text label,
            float preferredHeight)
        {
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            SetLayout(
                label.rectTransform,
                0f,
                preferredHeight,
                1f);
        }

        private static string FormatResourceAmounts(
            IReadOnlyList<ResourceAmount> values)
        {
            if (values == null || values.Count == 0) return "无";
            string result = string.Empty;
            for (var index = 0; index < values.Count; index++)
            {
                ResourceAmount value = values[index];
                if (index > 0) result += "、";
                result += ResourceName(value.ResourceId) + " " +
                    value.Amount;
            }
            return result;
        }

        private static string FormatSeconds(float value)
        {
            return Math.Max(0f, value).ToString("0.#") + " 秒";
        }

        private static string FormatPercent(double value)
        {
            double safe = Math.Max(0d, Math.Min(1d, value));
            return (safe * 100d).ToString("0.#") + "%";
        }

        private static string ValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private static string BuildingStateLabel(
            GrayboxBuildingInstanceState state)
        {
            switch (state)
            {
                case GrayboxBuildingInstanceState.UnderConstruction:
                    return "施工中";
                case GrayboxBuildingInstanceState.Completed:
                    return "已完成";
                case GrayboxBuildingInstanceState.AbandonedRuin:
                    return "废弃遗迹";
                default:
                    return state.ToString();
            }
        }

        private void SyncCatalogVisibility()
        {
            if (catalogRoot == null || interaction == null) return;
            bool visible = interaction.State ==
                           GrayboxBuildingInteractionState.CatalogOpen;
            if (catalogRoot.gameObject.activeSelf == visible) return;
            catalogRoot.gameObject.SetActive(visible);
            if (visible)
                RebuildCatalogCards();
        }

        private void SyncMiningGuidanceLegend()
        {
            if (miningGuidanceLegendRoot == null || interaction == null)
                return;
            bool visible =
                interaction.State ==
                    GrayboxBuildingInteractionState.Previewing &&
                ReferenceEquals(
                    interaction.Selected,
                    BuildingCatalog.MiningStation);
            if (miningGuidanceLegendRoot.gameObject.activeSelf != visible)
                miningGuidanceLegendRoot.gameObject.SetActive(visible);
        }

        private void RefreshPlacementStatus()
        {
            if (placementStatusRoot == null || interaction == null) return;
            GrayboxBuildingInteractionState state = interaction.State;
            BuildingDefinition selected = interaction.Selected;
            string buildingId = selected == null
                ? null
                : selected.Id.Value;
            BuildingOrientation orientation = interaction.Orientation;
            BuildingPlacementEvaluation evaluation =
                placement == null
                    ? default
                    : placement.CurrentEvaluation;
            BuildingPlacementFailure failure =
                evaluation.PrimaryFailure;
            ulong storageRevision = session?.CityStorage?.Revision ?? 0ul;
            if (hasPlacementStatusCache &&
                lastPlacementState == state &&
                lastPlacementFailure == failure &&
                lastPlacementValid == evaluation.IsValid &&
                string.Equals(
                    lastPlacementBuildingId,
                    buildingId,
                    StringComparison.Ordinal) &&
                lastPlacementOrientation == orientation &&
                lastPlacementStorageRevision == storageRevision)
                return;

            hasPlacementStatusCache = true;
            lastPlacementState = state;
            lastPlacementFailure = failure;
            lastPlacementValid = evaluation.IsValid;
            lastPlacementBuildingId = buildingId;
            lastPlacementOrientation = orientation;
            lastPlacementStorageRevision = storageRevision;
            bool previewVisible =
                state == GrayboxBuildingInteractionState.Previewing &&
                selected != null;
            bool placementFailureVisible =
                previewVisible &&
                placement != null &&
                !evaluation.IsValid &&
                failure != BuildingPlacementFailure.None;
            bool selectionFailureVisible =
                !string.IsNullOrEmpty(selectionFailureMessage);
            bool deploymentFailureVisible =
                !previewVisible &&
                state == GrayboxBuildingInteractionState.Inactive &&
                !string.IsNullOrEmpty(DeploymentFailureMessage);
            bool visible =
                deploymentFailureVisible ||
                selectionFailureVisible ||
                previewVisible;
            placementStatusRoot.gameObject.SetActive(visible);
            if (deploymentFailureVisible)
                placementStatusText.text = DeploymentFailureMessage;
            else if (selectionFailureVisible)
                placementStatusText.text = selectionFailureMessage;
            else if (previewVisible)
            {
                int width = BuildingOrientationRules.Width(
                    selected,
                    orientation);
                int height = BuildingOrientationRules.Height(
                    selected,
                    orientation);
                string summary = selected.Name +
                    " · 方向 " + OrientationName(orientation) +
                    " · 占地 " + width + "×" + height +
                    " · R 旋转";
                string failureMessage = failure ==
                        BuildingPlacementFailure.InsufficientMaterials
                    ? MaterialShortfallMessage(
                        selected,
                        placement.CurrentMaterialShortfalls)
                    : PlacementFailureMessage(failure);
                placementStatusText.text = placementFailureVisible
                    ? summary + "\n" + failureMessage
                    : summary + "\n可以放置";
            }
            else if (!string.IsNullOrEmpty(placementStatusText.text))
                placementStatusText.text = string.Empty;
        }

        private void ShowSelectionFailure(
            BuildingDefinition definition,
            string primaryLockReason)
        {
            if (definition == null) return;
            string reason = string.IsNullOrWhiteSpace(primaryLockReason)
                ? "当前未解锁"
                : primaryLockReason;
            selectionFailureMessage = definition.Name + "：" + reason;
            selectionFailureState = interaction.State;
            selectionFailureCatalogRevision = session.CatalogRevision;
            hasPlacementStatusCache = false;
            RefreshPlacementStatus();
        }

        private void ClearSelectionFailure()
        {
            if (string.IsNullOrEmpty(selectionFailureMessage)) return;
            DiscardSelectionFailure();
            RefreshPlacementStatus();
        }

        private void DiscardSelectionFailure()
        {
            selectionFailureMessage = string.Empty;
            selectionFailureState = default;
            selectionFailureCatalogRevision = 0u;
            hasPlacementStatusCache = false;
        }

        private static string OrientationName(BuildingOrientation orientation)
        {
            switch (orientation)
            {
                case BuildingOrientation.North:
                    return "北";
                case BuildingOrientation.East:
                    return "东";
                case BuildingOrientation.South:
                    return "南";
                case BuildingOrientation.West:
                    return "西";
                default:
                    return "未知";
            }
        }

        private static string MaterialShortfallMessage(
            BuildingDefinition definition,
            IReadOnlyList<ResourceShortfall> shortfalls)
        {
            if (definition == null || shortfalls == null ||
                shortfalls.Count == 0)
            {
                return PlacementFailureMessage(
                    BuildingPlacementFailure.InsufficientMaterials);
            }
            string message = "无法建造" + definition.Name + "：";
            for (var index = 0; index < shortfalls.Count; index++)
            {
                ResourceShortfall shortfall = shortfalls[index];
                if (index > 0) message += "；";
                string name = ResourceDefinitionCatalog.TryGet(
                        shortfall.ResourceId,
                        out ResourceDefinition resource)
                    ? resource.ChineseName
                    : shortfall.ResourceId;
                message += "缺少" + name + " " + shortfall.Missing +
                    "（拥有 " + shortfall.Owned + "，需要 " +
                    shortfall.Required + "）";
            }
            return message;
        }

        private static InputField CreateInputField(
            RectTransform parent,
            string name,
            string placeholderText)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(0f, 34f);
            SetLayout(rect, 0f, 34f, 1f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var input = rect.gameObject.AddComponent<InputField>();
            Text text = CreateLabel(rect, "Text", string.Empty);
            text.color = Color.black;
            Text placeholder = CreateLabel(
                rect,
                "Placeholder",
                placeholderText);
            placeholder.color = new Color(.3f, .3f, .3f, .7f);
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static Button CreateButton(
            RectTransform parent,
            string name,
            string label,
            Action callback)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(100f, 30f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetLayout(rect, 80f, 30f, 1f);
            if (callback != null)
                button.onClick.AddListener(() => callback());
            if (!string.IsNullOrEmpty(label))
                CreateLabel(rect, "Label", label);
            return button;
        }

        private static Text CreateLabel(
            RectTransform parent,
            string name,
            string value)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            return rect;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorInside(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void PlaceFixed(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, .5f);
            rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void ConfigureEvacuationRow(RectTransform row)
        {
            SetLayout(row, 0f, 32f, 1f);
            var layout =
                row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }

        private static void SetLayout(
            RectTransform rect,
            float preferredWidth,
            float preferredHeight,
            float flexibleWidth,
            float flexibleHeight = 0f)
        {
            LayoutElement element =
                rect.GetComponent<LayoutElement>() ??
                rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
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

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                Text[] texts = child.GetComponentsInChildren<Text>(true);
                for (var textIndex = 0;
                     textIndex < texts.Length;
                     textIndex++)
                    texts[textIndex].text = string.Empty;
                Selectable[] selectables =
                    child.GetComponentsInChildren<Selectable>(true);
                for (var selectableIndex = 0;
                     selectableIndex < selectables.Length;
                     selectableIndex++)
                    selectables[selectableIndex].interactable = false;
                child.SetActive(false);
                DestroyGenerated(child);
            }
        }

        private static void DestroyGenerated(GameObject gameObject)
        {
            if (gameObject == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(gameObject);
            else
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private static string KeyLabel(int zeroBasedIndex)
        {
            return zeroBasedIndex == 9
                ? "0"
                : (zeroBasedIndex + 1).ToString();
        }

        private static string CategoryLabel(BuildingMenuCategory category)
        {
            switch (category)
            {
                case BuildingMenuCategory.Basic: return "基础";
                case BuildingMenuCategory.Production: return "生产";
                case BuildingMenuCategory.Logistics: return "物流";
                case BuildingMenuCategory.Defense: return "防御";
                case BuildingMenuCategory.Route: return "路线";
                default: return category.ToString();
            }
        }

        private static string RouteLabel(ContentRoute route)
        {
            switch (route)
            {
                case ContentRoute.Core: return "核心";
                case ContentRoute.Technology: return "科技";
                case ContentRoute.Cultivation: return "修仙";
                case ContentRoute.BiologicalAscension: return "生物";
                case ContentRoute.Psionics: return "灵能";
                default: return route.ToString();
            }
        }

        private static string TreatmentLabel(
            BuildingEvacuationTreatment treatment)
        {
            switch (treatment)
            {
                case BuildingEvacuationTreatment.Abandon: return "遗弃";
                case BuildingEvacuationTreatment.FullDismantle:
                    return "完整拆除";
                case BuildingEvacuationTreatment.QuickDismantle:
                    return "快速拆除";
                default: return "未分配";
            }
        }
    }
}
