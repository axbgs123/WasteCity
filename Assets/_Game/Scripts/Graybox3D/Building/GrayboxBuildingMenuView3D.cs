using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Content;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingMenuView3D : MonoBehaviour
    {
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

        private GrayboxBuildingCatalogPresenter3D presenter;
        private GrayboxUiInputGuard3D inputGuard;
        private RectTransform uiRoot;
        private RectTransform quickbarRoot;
        private RectTransform catalogRoot;
        private RectTransform catalogCardsRoot;
        private RectTransform evacuationRoot;
        private InputField searchField;
        private BuildingMenuCategory? category;
        private ContentRoute? route;
        private string searchText = string.Empty;

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
        public string SearchText => searchText;

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

        private void Awake()
        {
            presenter = new GrayboxBuildingCatalogPresenter3D();
            inputGuard = new GrayboxUiInputGuard3D();
        }

        private void Update()
        {
            SyncCatalogVisibility();
        }

        private void OnDestroy()
        {
            if (uiRoot != null)
            {
                uiRoot.gameObject.SetActive(false);
                DestroyGenerated(uiRoot.gameObject);
            }
            uiRoot = null;
            quickbarRoot = null;
            catalogRoot = null;
            catalogCardsRoot = null;
            evacuationRoot = null;
            searchField = null;
            CancelSelectedConstructionRequested = null;
            CancelConstructionConfirmationResolved = null;
            EvacuationItemTreatmentRequested = null;
            EvacuationCategoryTreatmentRequested = null;
            EvacuationAllTreatmentRequested = null;
            EvacuationConfirmationRequested = null;
            canvas = null;
            eventSystem = null;
            session = null;
            interaction = null;
        }

        public void Configure(
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
            if (presenter == null)
                presenter = new GrayboxBuildingCatalogPresenter3D();
            if (inputGuard == null)
                inputGuard = new GrayboxUiInputGuard3D();

            if (uiRoot != null)
                DestroyGenerated(uiRoot.gameObject);
            EnsureCanvasContract();
            BuildUi();
            RefreshCatalog();
        }

        public void RefreshCatalog()
        {
            if (!IsConfigured) return;
            RefreshQuickbar();
            RebuildCatalogCards();
            SyncCatalogVisibility();
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
            if (item.Visibility != BuildingCatalogVisibility.Buildable)
                return false;
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
                        StringComparison.Ordinal) ||
                    item.Visibility != BuildingCatalogVisibility.Buildable)
                    continue;
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

        public void HideEvacuation()
        {
            if (evacuationRoot == null) return;
            ClearChildren(evacuationRoot);
            evacuationRoot.gameObject.SetActive(false);
        }

        private bool IsConfigured =>
            canvas != null &&
            eventSystem != null &&
            session != null &&
            interaction != null &&
            uiRoot != null;

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

            RectTransform construction = CreatePanel(
                uiRoot,
                "Construction",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-8f, 8f),
                new Vector2(190f, 102f));
            var constructionLayout =
                construction.gameObject.AddComponent<VerticalLayoutGroup>();
            constructionLayout.padding = new RectOffset(5, 5, 5, 5);
            constructionLayout.spacing = 3f;
            constructionLayout.childForceExpandWidth = true;
            constructionLayout.childForceExpandHeight = false;
            CreateButton(
                construction,
                "Construction.Cancel",
                "取消选中施工",
                () => CancelSelectedConstructionRequested?.Invoke());
            CreateButton(
                construction,
                "Construction.Confirm.Yes",
                "确认取消",
                () => CancelConstructionConfirmationResolved?.Invoke(true));
            CreateButton(
                construction,
                "Construction.Confirm.No",
                "返回施工",
                () => CancelConstructionConfirmationResolved?.Invoke(false));

            evacuationRoot = CreatePanel(
                uiRoot,
                "Evacuation",
                new Vector2(0f, .5f),
                new Vector2(0f, .5f),
                new Vector2(8f, 0f),
                new Vector2(390f, 440f));
            var evacuationLayout =
                evacuationRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            evacuationLayout.spacing = 3f;
            evacuationLayout.padding = new RectOffset(6, 6, 6, 6);
            evacuationRoot.gameObject.SetActive(false);
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

            catalogCardsRoot = CreateRect(
                catalogRoot,
                "Catalog.Cards");
            SetLayout(catalogCardsRoot, 596f, 0f, 0f, 1f);
            var cardLayout =
                catalogCardsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.spacing = 3f;
            cardLayout.childForceExpandHeight = false;
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
                    item.Visibility == BuildingCatalogVisibility.Buildable;
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
                item.Visibility == BuildingCatalogVisibility.Buildable;
            card.image.color = card.interactable
                ? ButtonColor
                : LockedColor;
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 108f);
            SetLayout(rect, 596f, 108f, 0f);

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
                "成本 " + definition.Cost + " " + definition.CostId);
            AnchorInside(
                cost.rectTransform,
                new Vector2(0f, .36f),
                new Vector2(1f, .66f),
                Vector2.zero,
                Vector2.zero);
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
            CreateLabel(
                details,
                "Details.Text",
                BuildDetails(item));
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
            return string.Join(
                "\n",
                new[]
                {
                    definition.Name,
                    "类别 " + CategoryLabel(item.Category),
                    "路线 " + RouteLabel(item.Route),
                    "占地 " + definition.Width + "×" + definition.Height,
                    "位置 " + BuildingMobilityRules.PlacementName(
                        definition.Placement),
                    "施工 " + definition.BuildSeconds + " 秒",
                    "完整成本 " + definition.Cost + " " + definition.CostId,
                    "研究 " + (definition.RequiredResearchId ?? "无"),
                    "前置 " + (definition.RequiredBuildingId ?? "无"),
                    "锁定原因 " + reasons
                });
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

        private void CreateEvacuationCategory(
            BuildingMenuCategory value)
        {
            RectTransform row = CreateRect(
                evacuationRoot,
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
            RectTransform row = CreateRect(
                evacuationRoot,
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
