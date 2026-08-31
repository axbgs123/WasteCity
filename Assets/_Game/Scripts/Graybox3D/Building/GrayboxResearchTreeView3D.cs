using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WasteCity.Economy;
using WasteCity.Graybox3D.Usability;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public enum ResearchNodePresentationState3D
    {
        Locked,
        Researchable,
        Active,
        Completed,
    }

    /// <summary>
    /// Immutable view data. The controller derives this from the formal
    /// research runtime so the view never infers state from localized text.
    /// </summary>
    public sealed class ResearchNodePresentation3D
    {
        public ResearchNodePresentation3D(
            ResearchDefinition definition,
            ResearchNodePresentationState3D state,
            string statusText,
            bool selected)
        {
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            State = state;
            StatusText = statusText ?? string.Empty;
            Selected = selected;
        }

        public ResearchDefinition Definition { get; }
        public ResearchNodePresentationState3D State { get; }
        public string StatusText { get; }
        public bool Selected { get; }
    }

    /// <summary>
    /// IDEA-0016 generated uGUI view for the formal research tree. Research
    /// state remains owned by the caller; this component only projects it.
    /// </summary>
    public sealed class GrayboxResearchTreeView3D : MonoBehaviour
    {
        private static readonly Color ButtonColor =
            new Color(.18f, .25f, .31f, 1f);
        private static readonly Color SelectedColor =
            new Color(.22f, .48f, .55f, 1f);
        private static readonly Color FilterDisabledColor =
            new Color(.16f, .16f, .16f, .9f);

        private readonly Dictionary<string, NodeRow> nodeRows =
            new Dictionary<string, NodeRow>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConnectionRow> connectionRows =
            new Dictionary<string, ConnectionRow>(StringComparer.Ordinal);
        private readonly Dictionary<string, JunctionRow> junctionRows =
            new Dictionary<string, JunctionRow>(StringComparer.Ordinal);
        private readonly Dictionary<string, JunctionRow> trunkRows =
            new Dictionary<string, JunctionRow>(StringComparer.Ordinal);
        private readonly Dictionary<DevelopmentRoute, Button> routeButtons =
            new Dictionary<DevelopmentRoute, Button>();
        private readonly Dictionary<ResearchNodePresentationState3D, Button>
            stateButtons = new Dictionary<
                ResearchNodePresentationState3D, Button>();
        private readonly Dictionary<string, ResearchNodePresentation3D>
            presentations = new Dictionary<string,
                ResearchNodePresentation3D>(StringComparer.Ordinal);
        private readonly HashSet<DevelopmentRoute> enabledRoutes =
            new HashSet<DevelopmentRoute>();
        private readonly List<ResourceIconSlot> resourceIcons =
            new List<ResourceIconSlot>();
        private readonly List<GameObject> ownedRoots =
            new List<GameObject>();
        private readonly Dictionary<Keyboard, Action<char>> textInputBindings =
            new Dictionary<Keyboard, Action<char>>();

        private RectTransform panel;
        private RectTransform viewport;
        private RectTransform content;
        private RectTransform nodesLayer;
        private RectTransform connectionsLayer;
        private RectTransform activeRoot;
        private RectTransform footer;
        private RectTransform detailCosts;
        private Image detailIcon;
        private Text detailName;
        private Text detailDuration;
        private Text detailStatus;
        private Text detailPrerequisites;
        private Text detailDescription;
        private Button allRoutesButton;
        private Button allStatesButton;
        private Text activeName;
        private Text activeProgress;
        private InputField searchInput;
        private Button startButton;
        private Button cancelButton;
        private Button focusActiveButton;
        private Func<string, Sprite> resourceIconResolver;
        private Action<string> selected;
        private Action startRequested;
        private Action cancelRequested;
        private Action closeRequested;
        private ResearchTreeProjection3D projection;
        private ResearchTreeViewportState3D viewportState;
        private string activeResearchId;
        private string selectedResearchId;
        private string detailResearchId;
        private bool userNavigated;
        private ResearchNodePresentationState3D? stateFilter;
        private bool searchHasFocus;
        private bool observesInputDevices;
        private InputAction backspaceAction;
        private InputAction deleteAction;

        private void OnEnable()
        {
            BindTextInput();
        }

        private void OnDisable()
        {
            UnbindTextInput();
            searchHasFocus = false;
        }

        private void OnDestroy()
        {
            UnbindTextInput();
            DisposeTextEditingActions();
        }

        public void Initialize(
            RectTransform targetPanel,
            Func<string, Sprite> iconResolver,
            Action<string> selectedCallback,
            Action startCallback,
            Action cancelCallback,
            Action closeCallback)
        {
            if (targetPanel == null)
                throw new ArgumentNullException(nameof(targetPanel));

            resourceIconResolver = iconResolver;
            selected = selectedCallback;
            startRequested = startCallback;
            cancelRequested = cancelCallback;
            closeRequested = closeCallback;

            if (ReferenceEquals(panel, targetPanel) && content != null)
            {
                RefreshResourceIcons();
                ApplyFilter();
                return;
            }

            ClearGeneratedUi();
            panel = targetPanel;
            projection = ResearchTreeProjection3D.Create(ResearchCatalog.All);
            EnablePrimaryRoutes();
            BuildUi();
            BindTextInput();
            RefreshResourceIcons();
            ApplyFilter();
            FitAll();
        }

        public void SetNode(
            ResearchDefinition definition,
            string stateText,
            bool isSelected)
        {
            if (definition == null) return;
            SetNode(new ResearchNodePresentation3D(
                definition,
                ResearchNodePresentationState3D.Locked,
                stateText,
                isSelected));
        }

        public void SetNode(ResearchNodePresentation3D presentation)
        {
            ApplyNodePresentation(presentation);
            ApplyFilter();
        }

        public void SetNodes(
            IReadOnlyList<ResearchNodePresentation3D> values)
        {
            if (values == null) return;
            for (var index = 0; index < values.Count; index++)
                ApplyNodePresentation(values[index]);
            ApplyFilter();
        }

        private void ApplyNodePresentation(
            ResearchNodePresentation3D presentation)
        {
            ResearchDefinition definition = presentation?.Definition;
            if (definition == null ||
                !nodeRows.TryGetValue(definition.Id.Value, out NodeRow row))
            {
                return;
            }

            presentations[definition.Id.Value] = presentation;
            row.Name.text = definition.Name;
            row.State.text = StateGlyph(presentation.State);
            row.State.color = StateColor(presentation.State);
            row.Button.image.color = presentation.Selected
                ? SelectedNodeColor(definition.Route)
                : NodeColor(definition.Route);
            if (presentation.Selected)
            {
                selectedResearchId = definition.Id.Value;
                PopulateFooter(presentation);
            }
            else if (string.Equals(
                         selectedResearchId,
                         definition.Id.Value,
                         StringComparison.Ordinal))
                selectedResearchId = null;
        }

        public void SetActiveResearch(
            string researchName,
            string progressText,
            bool visible)
        {
            SetActiveResearch(
                researchName,
                progressText,
                visible,
                null);
        }

        public void SetActiveResearch(
            string researchName,
            string progressText,
            bool visible,
            string researchId)
        {
            if (activeRoot == null) return;
            activeResearchId = visible ? researchId : null;
            activeName.text = researchName ?? string.Empty;
            activeProgress.text = progressText ?? string.Empty;
            activeRoot.gameObject.SetActive(visible);
            if (focusActiveButton != null)
                focusActiveButton.interactable = visible &&
                    !string.IsNullOrEmpty(activeResearchId);
        }

        public void SetStartInteractable(bool interactable)
        {
            if (startButton != null)
                startButton.interactable = interactable;
        }

        public void SetCancelInteractable(bool interactable)
        {
            if (cancelButton != null)
                cancelButton.interactable = interactable;
        }

        public void FitAll()
        {
            if (projection == null || viewport == null || content == null)
                return;
            Canvas.ForceUpdateCanvases();
            Vector2 viewportSize = viewport.rect.size;
            if (viewportSize.x <= 1f || viewportSize.y <= 1f)
                viewportSize = new Vector2(860f, 390f);
            ApplyViewportState(projection.FitAll(viewportSize, 28f));
            userNavigated = false;
        }

        public void FocusResearch(string researchId, bool force)
        {
            ResearchTreeNodeProjection3D target =
                projection?.FindNode(researchId);
            if (!force && userNavigated || target == null)
            {
                return;
            }
            var context = new HashSet<string>(StringComparer.Ordinal)
            {
                researchId,
            };
            for (var index = 0;
                 index < target.Definition.RequiredResearchIds.Count;
                 index++)
                context.Add(target.Definition.RequiredResearchIds[index]);
            for (var index = 0; index < projection.Edges.Count; index++)
            {
                ResearchTreeEdgeProjection3D edge = projection.Edges[index];
                if (string.Equals(
                        edge.PrerequisiteResearchId,
                        researchId,
                        StringComparison.Ordinal))
                    context.Add(edge.DependentResearchId);
            }
            ResearchTreeViewportState3D state = projection.Focus(
                context,
                ViewportSize(),
                72f);
            ApplyViewportState(new ResearchTreeViewportState3D(
                state.Center,
                Mathf.Min(1f, state.Zoom)));
        }

        public void NotifyOpened(string latestResearchableId)
        {
            userNavigated = false;
            FitAll();
            if (!string.IsNullOrEmpty(latestResearchableId))
                SelectResearch(latestResearchableId);
        }

        public void NotifyClosed()
        {
            ConsumeFocusedEscape();
        }

        public bool ConsumeFocusedEscape()
        {
            if (searchInput == null) return false;
            EventSystem eventSystem = EventSystem.current;
            GameObject current = eventSystem == null
                ? null
                : eventSystem.currentSelectedGameObject;
            bool selectedSearch = current != null &&
                current.GetComponentInParent<InputField>() == searchInput;
            if (!searchHasFocus && !selectedSearch && !searchInput.isFocused)
            {
                return false;
            }
            searchInput.DeactivateInputField();
            searchHasFocus = false;
            eventSystem?.SetSelectedGameObject(null);
            return true;
        }

        public bool HasTextInputFocus
        {
            get
            {
                if (searchInput == null) return false;
                EventSystem eventSystem = EventSystem.current;
                GameObject current = eventSystem == null
                    ? null
                    : eventSystem.currentSelectedGameObject;
                return searchHasFocus || searchInput.isFocused ||
                    current != null &&
                    current.GetComponentInParent<InputField>() == searchInput;
            }
        }

        private void BuildUi()
        {
            RectTransform backgroundRect = CreateRect(
                panel,
                "Research.Background");
            Stretch(backgroundRect);
            Image background = backgroundRect.gameObject.AddComponent<Image>();
            background.sprite = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Ui,
                "core.ui.background.research-tree");
            background.color = Color.white;
            background.preserveAspect = true;
            background.raycastTarget = false;
            backgroundRect.SetAsFirstSibling();
            ownedRoots.Add(backgroundRect.gameObject);

            RectTransform header = CreateRect(panel, "Research.Header");
            SetReferenceRect(
                header,
                ResearchTreeVisualLayoutProfile3D.HeaderRect);
            Image headerImage = header.gameObject.AddComponent<Image>();
            headerImage.color = new Color(.02f, .035f, .045f, .16f);
            headerImage.raycastTarget = false;
            ownedRoots.Add(header.gameObject);

            RectTransform titleIconRect = CreateRect(
                header,
                "Research.Title.Icon");
            SetReferenceChildRect(
                titleIconRect,
                ResearchTreeVisualLayoutProfile3D.HeaderRect,
                new Rect(28f, 992f, 58f, 58f));
            Image titleIcon = titleIconRect.gameObject.AddComponent<Image>();
            titleIcon.sprite = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Technology,
                ResearchCatalog.ScrapProcessingId);
            titleIcon.preserveAspect = true;
            titleIcon.color = new Color(.83f, .63f, .3f, 1f);
            titleIcon.raycastTarget = false;
            ApplyProductionFraming(
                titleIcon,
                Production2DVisualClass.Technology,
                ResearchCatalog.ScrapProcessingId,
                titleIconRect.anchoredPosition);

            Text title = CreateLabel(
                header,
                "Research.Title",
                "科技树",
                34);
            title.alignment = TextAnchor.MiddleLeft;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(.88f, .71f, .43f, 1f);
            SetReferenceChildRect(
                title.rectTransform,
                ResearchTreeVisualLayoutProfile3D.HeaderRect,
                new Rect(94f, 1005f, 230f, 44f));
            Text subtitle = CreateLabel(
                header,
                "Research.Subtitle",
                "WASTECITY",
                12);
            subtitle.alignment = TextAnchor.MiddleLeft;
            subtitle.color = new Color(.61f, .47f, .28f, .9f);
            SetReferenceChildRect(
                subtitle.rectTransform,
                ResearchTreeVisualLayoutProfile3D.HeaderRect,
                new Rect(98f, 986f, 210f, 20f));

            searchInput = CreateInputField(header, "Research.Search");
            RectTransform searchRect = searchInput.GetComponent<RectTransform>();
            SetReferenceChildRect(
                searchRect,
                ResearchTreeVisualLayoutProfile3D.HeaderRect,
                ResearchTreeVisualLayoutProfile3D.SearchSlotRect);
            searchInput.onValueChanged.AddListener(HandleSearchChanged);
            searchInput.onEndEdit.AddListener(_ => searchHasFocus = false);
            searchInput.gameObject
                .AddComponent<GrayboxResearchSearchFocus3D>()
                .Configure(value => searchHasFocus = value);

            RectTransform routeFilters = CreateRect(
                header,
                "Research.Filters.Route");
            SetReferenceChildRect(
                routeFilters,
                ResearchTreeVisualLayoutProfile3D.HeaderRect,
                ResearchTreeVisualLayoutProfile3D.RouteFilterSlotRect);
            Text routeFilterTitle = CreateLabel(
                routeFilters,
                "Research.Filters.Route.Title",
                "路线筛选",
                11);
            routeFilterTitle.alignment = TextAnchor.UpperLeft;
            routeFilterTitle.color = new Color(.75f, .78f, .78f, .9f);
            SetNormalizedRect(
                routeFilterTitle.rectTransform, 0f, .68f, 1f, 1f);
            RectTransform routeButtons = CreateRect(
                routeFilters,
                "Research.Filters.Route.Buttons");
            SetNormalizedRect(routeButtons, 0f, 0f, 1f, .66f);
            var filterLayout = routeButtons.gameObject
                .AddComponent<HorizontalLayoutGroup>();
            filterLayout.spacing = 4f;
            filterLayout.childForceExpandWidth = true;
            filterLayout.childForceExpandHeight = true;
            CreateRouteFilter(
                routeButtons,
                DevelopmentRoute.Technology,
                "Technology",
                "科技");
            CreateRouteFilter(
                routeButtons,
                DevelopmentRoute.Cultivation,
                "Cultivation",
                "修仙");
            CreateRouteFilter(
                routeButtons,
                DevelopmentRoute.Biological,
                "Biological",
                "血肉");
            CreateRouteFilter(
                routeButtons,
                DevelopmentRoute.Psionics,
                "Psionics",
                "灵能");
            allRoutesButton = CreateButton(
                routeButtons,
                "Research.Filter.Route.All",
                "全部",
                EnableAllRoutes);
            AddButtonIcon(
                allRoutesButton,
                ResearchIconCatalog3D.Resolve(
                    ResearchCatalog.ScrapProcessingId),
                ResearchCatalog.ScrapProcessingId);

            RectTransform statusFilters = CreateRect(
                header,
                "Research.Filters.Status");
            SetReferenceChildRect(
                statusFilters,
                ResearchTreeVisualLayoutProfile3D.HeaderRect,
                ResearchTreeVisualLayoutProfile3D.StatusFilterSlotRect);
            Text statusFilterTitle = CreateLabel(
                statusFilters,
                "Research.Filters.Status.Title",
                "状态筛选",
                11);
            statusFilterTitle.alignment = TextAnchor.UpperLeft;
            statusFilterTitle.color = new Color(.75f, .78f, .78f, .9f);
            SetNormalizedRect(
                statusFilterTitle.rectTransform, 0f, .68f, 1f, 1f);
            RectTransform statusButtons = CreateRect(
                statusFilters,
                "Research.Filters.Status.Buttons");
            SetNormalizedRect(statusButtons, 0f, 0f, 1f, .66f);
            var statusLayout = statusButtons.gameObject
                .AddComponent<HorizontalLayoutGroup>();
            statusLayout.spacing = 4f;
            statusLayout.childForceExpandWidth = true;
            statusLayout.childForceExpandHeight = true;
            allStatesButton = CreateButton(
                statusButtons,
                "Research.Filter.Status.All",
                "◆ 全部",
                () => SetStateFilter(null));
            CreateStateFilter(statusButtons,
                ResearchNodePresentationState3D.Researchable,
                "Researchable", "△ 可研究");
            CreateStateFilter(statusButtons,
                ResearchNodePresentationState3D.Active,
                "Active", "◉ 研究中");
            CreateStateFilter(statusButtons,
                ResearchNodePresentationState3D.Completed,
                "Completed", "✓ 已完成");
            CreateStateFilter(statusButtons,
                ResearchNodePresentationState3D.Locked,
                "Locked", "◆ 锁定");

            RectTransform focus = CreateRect(header, "Research.Focus");
            SetReferenceChildRect(
                focus,
                ResearchTreeVisualLayoutProfile3D.HeaderRect,
                ResearchTreeVisualLayoutProfile3D.FocusSlotRect);
            var focusLayout = focus.gameObject
                .AddComponent<HorizontalLayoutGroup>();
            focusLayout.spacing = 5f;
            focusLayout.childForceExpandWidth = true;
            focusLayout.childForceExpandHeight = true;
            focusActiveButton = CreateButton(
                focus,
                "Research.FocusCurrent",
                "当前研究",
                FocusCurrentResearch);
            focusActiveButton.interactable = false;
            CreateButton(focus, "Research.FocusLatest", "最新可研究",
                FocusLatestResearchable);
            Button fitAllButton = CreateButton(
                header, "Research.FitAll", "显示全树", FitAll);
            fitAllButton.gameObject.SetActive(false);
            Button closeButton = CreateButton(
                header, "Research.Close", "关闭",
                () => closeRequested?.Invoke());
            closeButton.gameObject.SetActive(false);

            viewport = CreateRect(panel, "Research.Viewport");
            SetReferenceRect(
                viewport,
                ResearchTreeVisualLayoutProfile3D.TreeRect);
            viewport.offsetMin += new Vector2(8f, 4f);
            viewport.offsetMax += new Vector2(-8f, -4f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(.015f, .035f, .045f, .12f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject
                .AddComponent<GrayboxResearchTreeViewportInput3D>()
                .Configure(PanViewport, ZoomViewport);
            ownedRoots.Add(viewport.gameObject);

            content = CreateRect(viewport, "Research.Content");
            content.anchorMin = new Vector2(.5f, .5f);
            content.anchorMax = new Vector2(.5f, .5f);
            content.pivot = new Vector2(.5f, .5f);
            content.sizeDelta = ContentSize(projection.Bounds);

            connectionsLayer = CreateRect(
                content,
                "Research.Connections");
            Stretch(connectionsLayer);
            nodesLayer = CreateRect(content, "Research.Nodes");
            Stretch(nodesLayer);
            BuildRouteHeaders();
            BuildNodes();
            BuildConnections();

            BuildFooter();
        }

        private void BuildNodes()
        {
            for (var index = 0; index < projection.Nodes.Count; index++)
            {
                ResearchTreeNodeProjection3D projected =
                    projection.Nodes[index];
                ResearchDefinition definition = projected.Definition;
                string researchId = definition.Id.Value;
                Button button = CreateButton(
                    nodesLayer,
                    "Research.Node." + researchId,
                    string.Empty,
                    () => SelectResearch(researchId));
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.5f, .5f);
                rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = projected.Position;
                rect.sizeDelta = NodeSize(definition.Route);
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = ConnectionColor(definition.Route);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                Image icon = CreateResearchIcon(
                    rect,
                    researchId,
                    definition.Route);

                Text name = CreateLabel(
                    rect,
                    "Research.Node." + researchId + ".Name",
                    definition.Name,
                    14);
                name.alignment = definition.Route == DevelopmentRoute.Bridge
                    ? TextAnchor.MiddleCenter
                    : TextAnchor.MiddleLeft;
                Anchor(name.rectTransform, .38f, 1f);
                name.rectTransform.offsetMin = definition.Route ==
                    DevelopmentRoute.Bridge
                        ? new Vector2(5f, 0f)
                        : new Vector2(42f, 0f);
                name.rectTransform.offsetMax = new Vector2(-22f, 0f);
                Text state = CreateLabel(
                    rect,
                    "Research.Node." + researchId + ".State",
                    "◇",
                    14);
                state.alignment = TextAnchor.UpperRight;
                Anchor(state.rectTransform, .55f, 1f);
                state.rectTransform.offsetMin = new Vector2(0f, 2f);
                state.rectTransform.offsetMax = new Vector2(-5f, -2f);
                AddCostIcons(rect, definition);
                nodeRows.Add(
                    researchId,
                    new NodeRow(button, icon, name, state));
            }
        }

        private static Image CreateResearchIcon(
            Transform parent,
            string researchId,
            DevelopmentRoute route)
        {
            RectTransform rect = CreateRect(
                parent,
                "Research.Node." + researchId + ".Icon");
            bool bridge = route == DevelopmentRoute.Bridge;
            rect.anchorMin = bridge ? new Vector2(.5f, 1f) : Vector2.zero;
            rect.anchorMax = rect.anchorMin;
            rect.pivot = bridge ? new Vector2(.5f, 1f) : Vector2.zero;
            rect.anchoredPosition = bridge
                ? new Vector2(0f, -8f)
                : new Vector2(6f, 22f);
            rect.sizeDelta =
                ResearchTreeVisualLayoutProfile3D.CompactNodeIconSize;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Technology,
                researchId);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.SetActive(image.sprite != null);
            ApplyProductionFraming(
                image,
                Production2DVisualClass.Technology,
                researchId,
                rect.anchoredPosition);
            return image;
        }

        private void BuildRouteHeaders()
        {
            float[] lanes =
                ResearchTreeVisualLayoutProfile3D.RouteLaneCenters;
            string[] names =
            {
                "Technology", "Cultivation", "Biological", "Psionics",
            };
            string[] labels =
            {
                "科技路线", "修仙路线", "生物飞升路线", "灵能路线",
            };
            float top = projection.Bounds.yMax -
                ResearchTreeVisualLayoutProfile3D.RouteHeaderSize.y * .5f;
            for (var index = 0; index < lanes.Length; index++)
            {
                DevelopmentRoute route = (DevelopmentRoute)index;
                RectTransform plate = CreateRect(
                    nodesLayer,
                    "Research.RouteHeader." + names[index]);
                plate.anchorMin = plate.anchorMax = new Vector2(.5f, .5f);
                plate.pivot = new Vector2(.5f, .5f);
                plate.anchoredPosition = new Vector2(lanes[index], top);
                plate.sizeDelta =
                    ResearchTreeVisualLayoutProfile3D.RouteHeaderSize;
                Image plateImage = plate.gameObject.AddComponent<Image>();
                plateImage.color = NodeColor(route);
                plateImage.raycastTarget = false;
                var outline = plate.gameObject.AddComponent<Outline>();
                outline.effectColor = ConnectionColor(route);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                ResearchDefinition emblem = ResearchCatalog.All.First(value =>
                    value.Route == route);
                RectTransform iconRect = CreateRect(
                    plate,
                    "Research.RouteHeader." + names[index] + ".Icon");
                SetNormalizedRect(iconRect, .04f, .12f, .28f, .88f);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = Production2DVisualCatalog3D.Resolve(
                    Production2DVisualClass.Technology,
                    emblem.Id.Value);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                ApplyProductionFraming(
                    icon,
                    Production2DVisualClass.Technology,
                    emblem.Id.Value,
                    iconRect.anchoredPosition);

                Text label = CreateLabel(
                    plate,
                    "Research.RouteHeader." + names[index] + ".Label",
                    labels[index].Replace("路线", string.Empty),
                    18);
                label.fontStyle = FontStyle.Bold;
                label.color = ConnectionColor(route);
                label.alignment = TextAnchor.MiddleLeft;
                SetNormalizedRect(label.rectTransform, .31f, 0f, .96f, 1f);
            }
        }

        private void BuildConnections()
        {
            for (var index = 0; index < projection.Trunks.Count; index++)
            {
                ResearchTreeTrunkProjection3D trunk = projection.Trunks[index];
                RectTransform rect = CreateRect(
                    connectionsLayer,
                    "Research.Trunk." + trunk.StableId);
                Stretch(rect);
                rect.gameObject.AddComponent<CanvasRenderer>();
                var graphic = rect.gameObject.AddComponent<
                    ResearchTreeConnectionGraphic3D>();
                graphic.ConfigurePath(
                    trunk.Points,
                    ConnectionColor(trunk.Route),
                    ConnectionColor(trunk.Route),
                    11f,
                    false,
                    false);
                trunkRows.Add(
                    trunk.StableId,
                    CreateSharedRow(
                        rect.gameObject,
                        trunk.PrerequisiteResearchId));
            }
            for (var index = 0; index < projection.Edges.Count; index++)
            {
                ResearchTreeEdgeProjection3D edge = projection.Edges[index];
                string key = ConnectionKey(
                    edge.PrerequisiteResearchId,
                    edge.DependentResearchId);
                RectTransform rect = CreateRect(
                    connectionsLayer,
                    "Research.Connection." + edge.PrerequisiteResearchId +
                    "->" + edge.DependentResearchId);
                Stretch(rect);
                rect.gameObject.AddComponent<CanvasRenderer>();
                var graphic = rect.gameObject.AddComponent<
                    ResearchTreeConnectionGraphic3D>();
                graphic.ConfigurePath(
                    edge.Points,
                    ConnectionColor(edge.StartRoute),
                    ConnectionColor(edge.EndRoute),
                    edge.IsBridge ? 3f : 4f,
                    edge.IsBridge,
                    true);
                connectionRows.Add(
                    key,
                    new ConnectionRow(
                        rect.gameObject,
                        edge.PrerequisiteResearchId,
                        edge.DependentResearchId));
            }
            for (var index = 0; index < projection.Junctions.Count; index++)
            {
                ResearchTreeJunctionProjection3D junction =
                    projection.Junctions[index];
                RectTransform rect = CreateRect(
                    connectionsLayer,
                    "Research.Junction." + junction.StableId);
                Stretch(rect);
                rect.gameObject.AddComponent<CanvasRenderer>();
                var graphic = rect.gameObject.AddComponent<
                    ResearchTreeConnectionGraphic3D>();
                graphic.ConfigureJunction(
                    junction.Position,
                    ConnectionColor(junction.Route),
                    10f);
                var dependents = new List<string>();
                for (var edgeIndex = 0;
                     edgeIndex < projection.Edges.Count;
                     edgeIndex++)
                {
                    ResearchTreeEdgeProjection3D edge =
                        projection.Edges[edgeIndex];
                    if (string.Equals(edge.JunctionId, junction.StableId,
                            StringComparison.Ordinal))
                        dependents.Add(edge.DependentResearchId);
                }
                junctionRows.Add(
                    junction.StableId,
                    new JunctionRow(
                        rect.gameObject,
                        junction.PrerequisiteResearchId,
                        dependents.ToArray()));
            }
        }

        private void BuildFooter()
        {
            footer = CreateRect(panel, "Research.Footer");
            SetReferenceRect(
                footer,
                ResearchTreeVisualLayoutProfile3D.FooterRect);
            Image footerBackground = footer.gameObject.AddComponent<Image>();
            footerBackground.color = new Color(.02f, .035f, .04f, .12f);
            footerBackground.raycastTarget = false;
            ownedRoots.Add(footer.gameObject);

            Rect[] slots = ResearchTreeVisualLayoutProfile3D.FooterSlots;
            RectTransform identity = CreateFooterBay(
                footer,
                "Research.Footer.Identity",
                slots[0]);
            RectTransform costs = CreateFooterBay(
                footer,
                "Research.Footer.Costs",
                slots[1]);
            RectTransform time = CreateFooterBay(
                footer,
                "Research.Footer.Time",
                slots[2]);
            RectTransform actions = CreateFooterBay(
                footer,
                "Research.Footer.Actions",
                slots[3]);
            RectTransform prerequisites = CreateFooterBay(
                footer,
                "Research.Footer.Prerequisites",
                slots[4]);
            RectTransform legendBay = CreateFooterBay(
                footer,
                "Research.Footer.Legend",
                slots[5]);

            RectTransform iconRect = CreateRect(
                identity,
                "Research.Detail.Icon");
            SetNormalizedRect(iconRect, .035f, .16f, .27f, .84f);
            detailIcon = iconRect.gameObject.AddComponent<Image>();
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;

            detailName = CreateLabel(
                identity,
                "Research.Detail.Name",
                "选择一项科技查看详情",
                20);
            detailName.fontStyle = FontStyle.Bold;
            detailName.alignment = TextAnchor.MiddleLeft;
            SetNormalizedRect(
                detailName.rectTransform, .31f, .7f, .96f, .94f);
            detailDescription = CreateLabel(
                identity,
                "Research.Detail.Description",
                string.Empty,
                13);
            detailDescription.alignment = TextAnchor.UpperLeft;
            detailDescription.color = new Color(.78f, .8f, .81f, 1f);
            SetNormalizedRect(
                detailDescription.rectTransform, .31f, .18f, .96f, .69f);

            Text costsTitle = CreateLabel(
                costs,
                "Research.Detail.Costs.Title",
                "研究消耗",
                13);
            costsTitle.alignment = TextAnchor.MiddleCenter;
            SetNormalizedRect(costsTitle.rectTransform, .04f, .78f, .96f, .97f);
            detailCosts = CreateRect(costs, "Research.Detail.Costs");
            SetNormalizedRect(detailCosts, .04f, .08f, .96f, .76f);
            var costLayout = detailCosts.gameObject.AddComponent<GridLayoutGroup>();
            costLayout.cellSize = new Vector2(174f, 46f);
            costLayout.spacing = new Vector2(8f, 7f);
            costLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            costLayout.constraintCount = 2;

            Text timeTitle = CreateLabel(
                time,
                "Research.Detail.Time.Title",
                "研究时间",
                13);
            timeTitle.alignment = TextAnchor.MiddleCenter;
            SetNormalizedRect(timeTitle.rectTransform, .05f, .78f, .95f, .97f);
            detailDuration = CreateLabel(
                time,
                "Research.Detail.Duration",
                string.Empty,
                26);
            detailDuration.fontStyle = FontStyle.Bold;
            detailDuration.color = new Color(.87f, .72f, .43f, 1f);
            SetNormalizedRect(detailDuration.rectTransform, .05f, .4f, .95f, .78f);
            detailStatus = CreateLabel(
                time,
                "Research.Detail.Status",
                string.Empty,
                12);
            detailStatus.color = new Color(.68f, .7f, .72f, 1f);
            SetNormalizedRect(detailStatus.rectTransform, .05f, .27f, .95f, .4f);

            var layout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 22, 22);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            startButton = CreateButton(
                actions,
                "Research.Start",
                "开始研究",
                () => startRequested?.Invoke());
            cancelButton = CreateButton(
                actions,
                "Research.Cancel",
                "取消",
                () => cancelRequested?.Invoke());
            startButton.image.color = new Color(.05f, .36f, .56f, 1f);
            cancelButton.image.color = new Color(.15f, .17f, .18f, 1f);

            Text prerequisiteTitle = CreateLabel(
                prerequisites,
                "Research.Detail.Prerequisites.Title",
                "前置条件",
                13);
            prerequisiteTitle.alignment = TextAnchor.MiddleLeft;
            SetNormalizedRect(
                prerequisiteTitle.rectTransform, .08f, .78f, .92f, .97f);
            detailPrerequisites = CreateLabel(
                prerequisites,
                "Research.Detail.Prerequisites",
                "前置：未选择",
                13);
            detailPrerequisites.alignment = TextAnchor.UpperLeft;
            SetNormalizedRect(
                detailPrerequisites.rectTransform, .08f, .08f, .92f, .76f);

            Text legend = CreateLabel(
                legendBay,
                "Research.StatusLegend",
                "状态说明\n\n✓  已完成\n△  可研究\n◉  研究中\n◆  已锁定",
                12);
            legend.alignment = TextAnchor.UpperLeft;
            SetNormalizedRect(legend.rectTransform, .08f, .06f, .94f, .94f);

            activeRoot = CreateRect(time, "Research.Active");
            SetNormalizedRect(activeRoot, .05f, .02f, .95f, .25f);
            Image activeBackground = activeRoot.gameObject.AddComponent<Image>();
            activeBackground.color = new Color(.1f, .16f, .18f, .72f);
            activeBackground.raycastTarget = false;
            activeName = CreateLabel(
                activeRoot,
                "Research.Active.Name",
                string.Empty,
                14);
            Anchor(activeName.rectTransform, .5f, 1f);
            activeProgress = CreateLabel(
                activeRoot,
                "Research.Active.Progress",
                string.Empty,
                11);
            Anchor(activeProgress.rectTransform, 0f, .5f);
            activeRoot.gameObject.SetActive(false);
        }

        private static RectTransform CreateFooterBay(
            RectTransform parent,
            string name,
            Rect referenceRect)
        {
            RectTransform rect = CreateRect(parent, name);
            SetReferenceChildRect(
                rect,
                ResearchTreeVisualLayoutProfile3D.FooterRect,
                referenceRect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.025f, .04f, .045f, .18f);
            image.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.32f, .29f, .22f, .72f);
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private void CreateRouteFilter(
            Transform parent,
            DevelopmentRoute route,
            string stableName,
            string label)
        {
            Button button = CreateButton(
                parent,
                "Research.Filter.Route." + stableName,
                label,
                () => ToggleRoute(route));
            ResearchDefinition emblem = ResearchCatalog.All.First(value =>
                value.Route == route);
            AddButtonIcon(
                button,
                ResearchIconCatalog3D.Resolve(emblem.Id.Value),
                emblem.Id.Value);
            routeButtons.Add(route, button);
        }

        private static void AddButtonIcon(
            Button button,
            Sprite sprite,
            string researchId)
        {
            if (button == null || sprite == null) return;
            RectTransform rect = CreateRect(
                button.transform,
                button.name + ".Icon");
            rect.anchorMin = new Vector2(0f, .5f);
            rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(5f, 0f);
            rect.sizeDelta = new Vector2(22f, 22f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Production2DVisualScalePolicy3D.ApplyToUiImage(
                image,
                Production2DVisualClass.Technology,
                Production2DVisualCatalog3D.ResolveVisibleBounds(
                    Production2DVisualClass.Technology,
                    researchId),
                new Vector2(5f, 0f));
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.rectTransform.offsetMin = new Vector2(27f, 0f);
        }

        private void ToggleRoute(DevelopmentRoute route)
        {
            if (!enabledRoutes.Remove(route))
                enabledRoutes.Add(route);
            ApplyFilter();
        }

        private void EnableAllRoutes()
        {
            EnablePrimaryRoutes();
            ApplyFilter();
        }

        private void CreateStateFilter(
            Transform parent,
            ResearchNodePresentationState3D state,
            string stableName,
            string label)
        {
            Button button = CreateButton(
                parent,
                "Research.Filter.Status." + stableName,
                label,
                () => SetStateFilter(state));
            stateButtons.Add(state, button);
        }

        private void SetStateFilter(
            ResearchNodePresentationState3D? value)
        {
            stateFilter = value;
            ApplyFilter();
        }

        private void FocusLatestResearchable()
        {
            ResearchTreeNodeProjection3D latest =
                projection?.SelectLatestResearchable(
                    presentations.Values
                        .Where(value => value.State ==
                            ResearchNodePresentationState3D.Researchable &&
                            IsVisibleByCurrentFilter(value.Definition))
                        .Select(value => value.Definition.Id.Value));
            if (latest != null)
                FocusResearch(latest.ResearchId, force: true);
        }

        private void FocusCurrentResearch()
        {
            if (string.IsNullOrEmpty(activeResearchId)) return;
            ResearchTreeNodeProjection3D node =
                projection?.FindNode(activeResearchId);
            if (node == null) return;
            if (!IsVisibleByCurrentFilter(node.Definition))
            {
                enabledRoutes.Clear();
                enabledRoutes.Add(DevelopmentRoute.Technology);
                enabledRoutes.Add(DevelopmentRoute.Cultivation);
                enabledRoutes.Add(DevelopmentRoute.BiologicalAscension);
                enabledRoutes.Add(DevelopmentRoute.Psionics);
                stateFilter = null;
                searchInput?.SetTextWithoutNotify(string.Empty);
                ApplyFilter();
            }
            FocusResearch(activeResearchId, force: true);
        }

        private void HandleSearchChanged(string value)
        {
            ApplyFilter();
            string query = (value ?? string.Empty).Trim();
            if (query.Length == 0) return;
            var matches = new List<string>();
            for (var index = 0; index < projection.Nodes.Count; index++)
            {
                ResearchDefinition definition =
                    projection.Nodes[index].Definition;
                if (RouteEnabled(definition) &&
                    StateEnabled(definition.Id.Value) &&
                    Matches(definition, query))
                    matches.Add(definition.Id.Value);
            }
            if (matches.Count > 0)
                ApplyViewportState(projection.Focus(
                    matches,
                    ViewportSize(),
                    72f));
        }

        private void BindTextInput()
        {
            if (!isActiveAndEnabled) return;
            BindTextEditingActions();
            if (!observesInputDevices)
            {
                InputSystem.onDeviceChange += OnInputDeviceChange;
                observesInputDevices = true;
            }
            for (var index = 0; index < InputSystem.devices.Count; index++)
            {
                if (InputSystem.devices[index] is Keyboard keyboard)
                    BindKeyboard(keyboard);
            }
        }

        private void UnbindTextInput()
        {
            backspaceAction?.Disable();
            deleteAction?.Disable();
            if (observesInputDevices)
            {
                InputSystem.onDeviceChange -= OnInputDeviceChange;
                observesInputDevices = false;
            }
            foreach (KeyValuePair<Keyboard, Action<char>> binding in
                     textInputBindings)
            {
                binding.Key.onTextInput -= binding.Value;
            }
            textInputBindings.Clear();
        }

        private void BindTextEditingActions()
        {
            if (backspaceAction == null)
            {
                backspaceAction = new InputAction(
                    "ResearchSearchBackspace",
                    InputActionType.Button,
                    "<Keyboard>/backspace");
                backspaceAction.performed += _ =>
                    DeleteSearchText(backward: true);
            }
            if (deleteAction == null)
            {
                deleteAction = new InputAction(
                    "ResearchSearchDelete",
                    InputActionType.Button,
                    "<Keyboard>/delete");
                deleteAction.performed += _ =>
                    DeleteSearchText(backward: false);
            }
            backspaceAction.Enable();
            deleteAction.Enable();
        }

        private void DisposeTextEditingActions()
        {
            backspaceAction?.Dispose();
            deleteAction?.Dispose();
            backspaceAction = null;
            deleteAction = null;
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
            {
                return;
            }
            keyboard.onTextInput -= callback;
            textInputBindings.Remove(keyboard);
        }

        private void OnTextInput(Keyboard source, char character)
        {
            if (!isActiveAndEnabled ||
                source == null ||
                !ReferenceEquals(source, Keyboard.current) ||
                searchInput == null ||
                !searchInput.isFocused ||
                !HasTextInputFocus ||
                char.IsControl(character))
            {
                return;
            }

            int anchor = Mathf.Clamp(
                searchInput.selectionAnchorPosition,
                0,
                searchInput.text.Length);
            int focus = Mathf.Clamp(
                searchInput.selectionFocusPosition,
                0,
                searchInput.text.Length);
            int start = Mathf.Min(anchor, focus);
            int length = Mathf.Abs(anchor - focus);
            searchInput.text = searchInput.text
                .Remove(start, length)
                .Insert(start, character.ToString());
            searchInput.caretPosition = start + 1;
        }

        private void DeleteSearchText(bool backward)
        {
            if (!isActiveAndEnabled ||
                searchInput == null ||
                !searchInput.isFocused ||
                !HasTextInputFocus)
            {
                return;
            }

            string value = searchInput.text ?? string.Empty;
            int anchor = Mathf.Clamp(
                searchInput.selectionAnchorPosition,
                0,
                value.Length);
            int focus = Mathf.Clamp(
                searchInput.selectionFocusPosition,
                0,
                value.Length);
            int start = Mathf.Min(anchor, focus);
            int length = Mathf.Abs(anchor - focus);
            if (length == 0)
            {
                if (backward && start > 0)
                {
                    start--;
                    length = 1;
                }
                else if (!backward && start < value.Length)
                {
                    length = 1;
                }
            }
            if (length == 0) return;

            searchInput.text = value.Remove(start, length);
            searchInput.caretPosition = start;
            searchInput.selectionAnchorPosition = start;
            searchInput.selectionFocusPosition = start;
        }

        private void PanViewport(Vector2 screenDelta)
        {
            viewportState = new ResearchTreeViewportState3D(
                viewportState.Center -
                    screenDelta / Mathf.Max(.01f, viewportState.Zoom),
                viewportState.Zoom);
            userNavigated = true;
            ApplyViewportState(viewportState);
        }

        private void ZoomViewport(
            Vector2 screenPosition,
            float scrollDelta,
            Camera eventCamera)
        {
            if (viewport == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport,
                    screenPosition,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return;
            }
            Vector2 viewportSize = ViewportSize();
            Vector2 pointerPosition = localPoint + viewportSize * .5f;
            float requestedZoom = viewportState.Zoom *
                Mathf.Pow(1.12f, scrollDelta);
            viewportState = ResearchTreeProjection3D.ZoomAroundPointer(
                viewportState,
                requestedZoom,
                pointerPosition,
                viewportSize);
            userNavigated = true;
            ApplyViewportState(viewportState);
        }

        private Vector2 ViewportSize()
        {
            if (viewport == null) return new Vector2(860f, 390f);
            Canvas.ForceUpdateCanvases();
            Vector2 size = viewport.rect.size;
            return size.x <= 1f || size.y <= 1f
                ? new Vector2(860f, 390f)
                : size;
        }

        private void ApplyViewportState(ResearchTreeViewportState3D state)
        {
            viewportState = state;
            if (content == null) return;
            content.localScale = Vector3.one * state.Zoom;
            content.anchoredPosition = -state.Center * state.Zoom;
        }

        private void ApplyFilter()
        {
            if (projection == null || nodeRows.Count == 0) return;
            string query = searchInput == null
                ? string.Empty
                : (searchInput.text ?? string.Empty).Trim();
            var visible = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < projection.Nodes.Count; index++)
            {
                ResearchDefinition definition =
                    projection.Nodes[index].Definition;
                if (RouteEnabled(definition) &&
                    StateEnabled(definition.Id.Value) &&
                    Matches(definition, query))
                {
                    if (stateFilter.HasValue)
                        visible.Add(definition.Id.Value);
                    else
                        AddWithPrerequisites(definition.Id.Value, visible);
                }
            }

            foreach (KeyValuePair<string, NodeRow> pair in nodeRows)
                pair.Value.Button.gameObject.SetActive(
                    visible.Contains(pair.Key));
            foreach (ConnectionRow row in connectionRows.Values)
            {
                row.GameObject.SetActive(
                    visible.Contains(row.PrerequisiteResearchId) &&
                    visible.Contains(row.DependentResearchId));
            }
            foreach (JunctionRow row in junctionRows.Values)
            {
                bool active = visible.Contains(row.PrerequisiteResearchId);
                var hasVisibleDependent = false;
                for (var index = 0; index < row.DependentResearchIds.Length;
                     index++)
                    hasVisibleDependent |= visible.Contains(
                        row.DependentResearchIds[index]);
                row.GameObject.SetActive(active && hasVisibleDependent);
            }
            foreach (JunctionRow row in trunkRows.Values)
            {
                bool active = visible.Contains(row.PrerequisiteResearchId);
                var hasVisibleDependent = false;
                for (var index = 0; index < row.DependentResearchIds.Length;
                     index++)
                    hasVisibleDependent |= visible.Contains(
                        row.DependentResearchIds[index]);
                row.GameObject.SetActive(active && hasVisibleDependent);
            }
            if (!string.IsNullOrEmpty(selectedResearchId) &&
                !visible.Contains(selectedResearchId))
            {
                selectedResearchId = null;
                selected?.Invoke(null);
            }
            foreach (KeyValuePair<DevelopmentRoute, Button> pair in
                     routeButtons)
            {
                pair.Value.image.color = enabledRoutes.Contains(pair.Key)
                    ? ConnectionColor(pair.Key)
                    : FilterDisabledColor;
            }
            if (allRoutesButton != null)
                allRoutesButton.image.color = enabledRoutes.Count == 4
                    ? SelectedColor
                    : FilterDisabledColor;
            foreach (KeyValuePair<ResearchNodePresentationState3D, Button>
                     pair in stateButtons)
            {
                pair.Value.image.color = stateFilter == pair.Key
                    ? SelectedColor
                    : FilterDisabledColor;
            }
            if (allStatesButton != null)
                allStatesButton.image.color = stateFilter.HasValue
                    ? FilterDisabledColor
                    : SelectedColor;
        }

        private bool StateEnabled(string researchId)
        {
            if (!stateFilter.HasValue) return true;
            return presentations.TryGetValue(
                    researchId,
                    out ResearchNodePresentation3D presentation) &&
                presentation.State == stateFilter.Value;
        }

        private bool IsVisibleByCurrentFilter(ResearchDefinition definition)
        {
            string query = searchInput == null
                ? string.Empty
                : (searchInput.text ?? string.Empty).Trim();
            return definition != null &&
                RouteEnabled(definition) &&
                StateEnabled(definition.Id.Value) &&
                Matches(definition, query);
        }

        private void AddWithPrerequisites(
            string researchId,
            HashSet<string> visible)
        {
            if (!visible.Add(researchId)) return;
            ResearchTreeNodeProjection3D node = projection.FindNode(researchId);
            if (node == null) return;
            for (var index = 0;
                 index < node.Definition.RequiredResearchIds.Count;
                 index++)
            {
                AddWithPrerequisites(
                    node.Definition.RequiredResearchIds[index],
                    visible);
            }
        }

        private bool RouteEnabled(ResearchDefinition definition)
        {
            if (definition.Route == DevelopmentRoute.Common) return true;
            if (definition.Route != DevelopmentRoute.Bridge)
                return enabledRoutes.Contains(definition.Route);
            for (var index = 0;
                 index < definition.RequiredResearchIds.Count;
                 index++)
            {
                ResearchDefinition prerequisite = ResearchCatalog.Find(
                    definition.RequiredResearchIds[index]);
                if (prerequisite != null &&
                    prerequisite.Route != DevelopmentRoute.Common &&
                    !enabledRoutes.Contains(prerequisite.Route))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Matches(
            ResearchDefinition definition,
            string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return Contains(definition.Name, query) ||
                Contains(definition.Id.Value, query) ||
                Contains(definition.EffectSummary, query);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddCostIcons(
            Transform parent,
            ResearchDefinition definition)
        {
            bool bridge = definition.Route == DevelopmentRoute.Bridge;
            for (var index = 0; index < definition.Costs.Count; index++)
            {
                ResourceAmount amount = definition.Costs[index];
                float left = bridge ? 10f + index * 42f : 52f + index * 54f;
                RectTransform rect = CreateRect(
                    parent,
                    "Research.Node." + definition.Id.Value + ".Cost." +
                    amount.ResourceId + ".Icon");
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(left, 10f);
                rect.sizeDelta =
                    ResearchTreeVisualLayoutProfile3D.CostIconSize;
                Image image = rect.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                resourceIcons.Add(new ResourceIconSlot(
                    image,
                    amount.ResourceId,
                    rect.anchoredPosition));

                Text value = CreateLabel(
                    parent,
                    "Research.Node." + definition.Id.Value + ".Cost." +
                    amount.ResourceId + ".Amount",
                    amount.Amount.ToString(CultureInfo.InvariantCulture),
                    10);
                RectTransform valueRect = value.rectTransform;
                valueRect.anchorMin = valueRect.anchorMax = Vector2.zero;
                valueRect.pivot = new Vector2(0f, .5f);
                valueRect.anchoredPosition = new Vector2(left + 14f, 10f);
                valueRect.sizeDelta = new Vector2(bridge ? 26f : 32f, 17f);
                value.alignment = TextAnchor.MiddleLeft;
                value.color = new Color(.86f, .88f, .9f, .96f);
            }
        }

        private void RefreshResourceIcons()
        {
            for (var index = 0; index < resourceIcons.Count; index++)
            {
                ResourceIconSlot slot = resourceIcons[index];
                slot.Image.sprite = resourceIconResolver?.Invoke(
                    slot.ResourceId);
                slot.Image.gameObject.SetActive(slot.Image.sprite != null);
                ApplyProductionFraming(
                    slot.Image,
                    Production2DVisualClass.Item,
                    slot.ResourceId,
                    slot.BaseAnchoredPosition);
            }
        }

        private void EnablePrimaryRoutes()
        {
            enabledRoutes.Clear();
            enabledRoutes.Add(DevelopmentRoute.Technology);
            enabledRoutes.Add(DevelopmentRoute.Cultivation);
            enabledRoutes.Add(DevelopmentRoute.Biological);
            enabledRoutes.Add(DevelopmentRoute.Psionics);
        }

        private void SelectResearch(string researchId)
        {
            selectedResearchId = researchId;
            if (presentations.TryGetValue(
                    researchId,
                    out ResearchNodePresentation3D presentation))
            {
                PopulateFooter(presentation);
            }
            selected?.Invoke(researchId);
        }

        private void PopulateFooter(ResearchNodePresentation3D presentation)
        {
            if (presentation == null || detailIcon == null) return;
            ResearchDefinition definition = presentation.Definition;
            detailIcon.sprite = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Technology,
                definition.Id.Value);
            detailIcon.gameObject.SetActive(detailIcon.sprite != null);
            ApplyProductionFraming(
                detailIcon,
                Production2DVisualClass.Technology,
                definition.Id.Value,
                detailIcon.rectTransform.anchoredPosition);
            detailName.text = definition.Name;
            detailDuration.text = FormatDuration(definition.Duration);
            detailStatus.text = presentation.StatusText;
            detailPrerequisites.text = PrerequisiteStatusLines(definition);
            detailDescription.text = definition.EffectSummary ?? string.Empty;

            if (string.Equals(
                    detailResearchId,
                    definition.Id.Value,
                    StringComparison.Ordinal))
            {
                return;
            }
            detailResearchId = definition.Id.Value;
            for (var index = detailCosts.childCount - 1; index >= 0; index--)
                DestroyGenerated(detailCosts.GetChild(index).gameObject);
            for (var index = 0; index < definition.Costs.Count; index++)
            {
                ResourceAmount amount = definition.Costs[index];
                string rowName =
                    "Research.Detail.Cost." + amount.ResourceId;
                RectTransform row = CreateRect(detailCosts, rowName);
                var element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 34f;
                Image rowBackground = row.gameObject.AddComponent<Image>();
                rowBackground.color = new Color(.1f, .14f, .17f, .9f);
                rowBackground.raycastTarget = false;

                RectTransform iconRect = CreateRect(
                    row,
                    rowName + ".Icon");
                SetNormalizedRect(iconRect, .02f, .08f, .24f, .92f);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = resourceIconResolver?.Invoke(
                    amount.ResourceId) ??
                    ResourceIconCatalog3D.Resolve(amount.ResourceId);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                ApplyProductionFraming(
                    icon,
                    Production2DVisualClass.Item,
                    amount.ResourceId,
                    iconRect.anchoredPosition);

                Text value = CreateLabel(
                    row,
                    rowName + ".Amount",
                    amount.Amount.ToString(CultureInfo.InvariantCulture),
                    14);
                value.alignment = TextAnchor.MiddleLeft;
                SetNormalizedRect(value.rectTransform, .29f, 0f, .98f, 1f);
            }
        }

        private static string PrerequisiteNames(
            ResearchDefinition definition)
        {
            if (definition.RequiredResearchIds.Count == 0) return "无";
            var names = new string[definition.RequiredResearchIds.Count];
            for (var index = 0; index < names.Length; index++)
            {
                string id = definition.RequiredResearchIds[index];
                names[index] = ResearchCatalog.Find(id)?.Name ?? id;
            }
            return string.Join("、", names);
        }

        private string PrerequisiteStatusLines(ResearchDefinition definition)
        {
            if (definition.RequiredResearchIds.Count == 0) return "✓  无";
            var values = new string[definition.RequiredResearchIds.Count];
            for (var index = 0; index < values.Length; index++)
            {
                string id = definition.RequiredResearchIds[index];
                bool complete = presentations.TryGetValue(
                        id,
                        out ResearchNodePresentation3D prerequisite) &&
                    prerequisite.State ==
                    ResearchNodePresentationState3D.Completed;
                values[index] = (complete ? "✓  " : "◆  ") +
                    (ResearchCatalog.Find(id)?.Name ?? id);
            }
            return string.Join("\n", values);
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (total / 60).ToString("00", CultureInfo.InvariantCulture) +
                ":" +
                (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private void ClearGeneratedUi()
        {
            for (var index = ownedRoots.Count - 1; index >= 0; index--)
                DestroyGenerated(ownedRoots[index]);
            ownedRoots.Clear();
            nodeRows.Clear();
            connectionRows.Clear();
            junctionRows.Clear();
            trunkRows.Clear();
            routeButtons.Clear();
            stateButtons.Clear();
            presentations.Clear();
            resourceIcons.Clear();
            viewport = null;
            content = null;
            nodesLayer = null;
            connectionsLayer = null;
            footer = null;
            detailCosts = null;
            detailIcon = null;
            detailName = null;
            detailDuration = null;
            detailStatus = null;
            detailPrerequisites = null;
            detailDescription = null;
            activeRoot = null;
            activeName = null;
            activeProgress = null;
            searchInput = null;
            searchHasFocus = false;
            startButton = null;
            cancelButton = null;
            focusActiveButton = null;
            allRoutesButton = null;
            allStatesButton = null;
            activeResearchId = null;
            selectedResearchId = null;
            detailResearchId = null;
            stateFilter = null;
            viewportState = default;
            userNavigated = false;
        }

        private static InputField CreateInputField(
            Transform parent,
            string name)
        {
            RectTransform rect = CreateRect(parent, name);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = ButtonColor;
            ApplyFormalUiSprite(
                background,
                "core.ui.frame.secondary-card");
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;

            RectTransform iconRect = CreateRect(rect, name + ".Icon");
            iconRect.anchorMin = new Vector2(0f, .5f);
            iconRect.anchorMax = new Vector2(0f, .5f);
            iconRect.pivot = new Vector2(0f, .5f);
            iconRect.anchoredPosition = new Vector2(6f, 0f);
            iconRect.sizeDelta = new Vector2(20f, 20f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Ui,
                "core.ui.icon.search");
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            Text text = CreateLabel(rect, name + ".Text", string.Empty, 14);
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin = new Vector2(32f, 2f);
            text.rectTransform.offsetMax = new Vector2(-8f, -2f);
            Text placeholder = CreateLabel(
                rect,
                name + ".Placeholder",
                "搜索科技名称或编号",
                14);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(.7f, .75f, .78f, 1f);
            placeholder.rectTransform.offsetMin = new Vector2(32f, 2f);
            placeholder.rectTransform.offsetMax = new Vector2(-8f, -2f);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            return input;
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
            if (!name.StartsWith("Research.Node.", StringComparison.Ordinal))
            {
                ApplyFormalUiSprite(
                    image,
                    "core.ui.control.primary-button");
            }
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (callback != null)
                button.onClick.AddListener(() => callback());
            if (!string.IsNullOrEmpty(label))
                CreateLabel(rect, name + ".Label", label, 13);
            return button;
        }

        private static void ApplyFormalUiSprite(Image image, string contentId)
        {
            if (image == null) return;
            image.sprite = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Ui,
                contentId);
            image.type = image.sprite != null &&
                image.sprite.border.sqrMagnitude > 0f
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
        }

        private static void ApplyProductionFraming(
            Image image,
            Production2DVisualClass visualClass,
            string contentId,
            Vector2 baseAnchoredPosition)
        {
            Production2DVisualScalePolicy3D.ApplyToUiImage(
                image,
                visualClass,
                Production2DVisualCatalog3D.ResolveVisibleBounds(
                    visualClass,
                    contentId),
                baseAnchoredPosition);
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
            FormalUiCanvasConfiguration3D.ApplyReadableFontSize(
                text,
                Mathf.Max(
                    fontSize,
                    FormalUiLayoutProfile3D.Standard.FontDescription));
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

        private static void SetTopRect(
            RectTransform rect,
            float leftRight,
            float top,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.offsetMin = new Vector2(leftRight, -top - height);
            rect.offsetMax = new Vector2(-leftRight, -top);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetNormalizedRect(
            RectTransform rect,
            float minimumX,
            float minimumY,
            float maximumX,
            float maximumY)
        {
            rect.anchorMin = new Vector2(minimumX, minimumY);
            rect.anchorMax = new Vector2(maximumX, maximumY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetReferenceRect(RectTransform rect, Rect target)
        {
            Vector2 reference =
                ResearchTreeVisualLayoutProfile3D.ReferenceResolution;
            SetNormalizedRect(
                rect,
                target.xMin / reference.x,
                target.yMin / reference.y,
                target.xMax / reference.x,
                target.yMax / reference.y);
        }

        private static void SetReferenceChildRect(
            RectTransform rect,
            Rect parentReference,
            Rect targetReference)
        {
            SetNormalizedRect(
                rect,
                (targetReference.xMin - parentReference.xMin) /
                    parentReference.width,
                (targetReference.yMin - parentReference.yMin) /
                    parentReference.height,
                (targetReference.xMax - parentReference.xMin) /
                    parentReference.width,
                (targetReference.yMax - parentReference.yMin) /
                    parentReference.height);
        }

        private static void Anchor(
            RectTransform rect,
            float minimumY,
            float maximumY)
        {
            rect.anchorMin = new Vector2(0f, minimumY);
            rect.anchorMax = new Vector2(1f, maximumY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Vector2 ContentSize(Rect bounds)
        {
            float halfWidth = Mathf.Max(
                Mathf.Abs(bounds.xMin),
                Mathf.Abs(bounds.xMax));
            float halfHeight = Mathf.Max(
                Mathf.Abs(bounds.yMin),
                Mathf.Abs(bounds.yMax));
            return new Vector2(
                halfWidth * 2f + 160f,
                halfHeight * 2f + 160f);
        }

        private static Color ConnectionColor(DevelopmentRoute route)
        {
            switch (route)
            {
                case DevelopmentRoute.Technology:
                    return new Color(.21f, .81f, .95f, .98f);
                case DevelopmentRoute.Cultivation:
                    return new Color(.3f, .85f, .52f, .98f);
                case DevelopmentRoute.BiologicalAscension:
                    return new Color(1f, .42f, .37f, .98f);
                case DevelopmentRoute.Psionics:
                    return new Color(.66f, .47f, 1f, .98f);
                case DevelopmentRoute.Bridge:
                    return new Color(.85f, .64f, .25f, .98f);
                default:
                    return new Color(.72f, .76f, .78f, .72f);
            }
        }

        private static Color NodeColor(DevelopmentRoute route)
        {
            switch (route)
            {
                case DevelopmentRoute.Technology:
                    return new Color(.035f, .12f, .16f, .94f);
                case DevelopmentRoute.Cultivation:
                    return new Color(.04f, .14f, .09f, .94f);
                case DevelopmentRoute.BiologicalAscension:
                    return new Color(.16f, .06f, .055f, .94f);
                case DevelopmentRoute.Psionics:
                    return new Color(.1f, .06f, .16f, .94f);
                case DevelopmentRoute.Bridge:
                    return new Color(.16f, .12f, .04f, .95f);
                default:
                    return ButtonColor;
            }
        }

        private static Color SelectedNodeColor(DevelopmentRoute route)
        {
            Color value = Color.Lerp(
                NodeColor(route),
                ConnectionColor(route),
                .36f);
            value.a = .98f;
            return value;
        }

        private static Vector2 NodeSize(DevelopmentRoute route)
        {
            if (route == DevelopmentRoute.Bridge)
                return ResearchTreeVisualLayoutProfile3D.BridgeNodeSize;
            if (route == DevelopmentRoute.Common)
                return ResearchTreeVisualLayoutProfile3D.CommonNodeSize;
            return ResearchTreeVisualLayoutProfile3D.CompactNodeSize;
        }

        private static string StateGlyph(ResearchNodePresentationState3D state)
        {
            switch (state)
            {
                case ResearchNodePresentationState3D.Completed: return "✓";
                case ResearchNodePresentationState3D.Active: return "◉";
                case ResearchNodePresentationState3D.Researchable: return "△";
                default: return "◆";
            }
        }

        private static Color StateColor(ResearchNodePresentationState3D state)
        {
            switch (state)
            {
                case ResearchNodePresentationState3D.Completed:
                    return new Color(.28f, .95f, .48f, 1f);
                case ResearchNodePresentationState3D.Active:
                    return new Color(1f, .71f, .22f, 1f);
                case ResearchNodePresentationState3D.Researchable:
                    return new Color(.28f, .82f, 1f, 1f);
                default:
                    return new Color(.48f, .5f, .52f, 1f);
            }
        }

        private static string ResearchDetails(ResearchDefinition definition)
        {
            string prerequisites = "无";
            if (definition.RequiredResearchIds.Count > 0)
            {
                var names = new List<string>(
                    definition.RequiredResearchIds.Count);
                for (var index = 0;
                     index < definition.RequiredResearchIds.Count;
                     index++)
                {
                    string id = definition.RequiredResearchIds[index];
                    names.Add(ResearchCatalog.Find(id)?.Name ?? id);
                }
                prerequisites = string.Join("、", names);
            }
            return "前置：" + prerequisites + "\n成本：" +
                FormatAmounts(definition.Costs) + "  时间：" +
                definition.Duration.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture) + " 秒\n效果：" +
                (definition.EffectSummary ?? string.Empty);
        }

        private static string FormatAmounts(
            IReadOnlyList<ResourceAmount> amounts)
        {
            if (amounts == null || amounts.Count == 0) return "无";
            var values = new string[amounts.Count];
            for (var index = 0; index < amounts.Count; index++)
            {
                ResourceAmount amount = amounts[index];
                ResourceDefinitionCatalog.TryGet(
                    amount.ResourceId,
                    out ResourceDefinition resource);
                values[index] = amount.Amount + " " +
                    (resource?.ChineseName ?? amount.ResourceId);
            }
            return string.Join(" + ", values);
        }

        private static string ConnectionKey(string prerequisite, string child)
        {
            return prerequisite + "\n" + child;
        }

        private JunctionRow CreateSharedRow(
            GameObject gameObject,
            string prerequisiteResearchId)
        {
            var dependents = new List<string>();
            for (var index = 0; index < projection.Edges.Count; index++)
                if (string.Equals(
                        projection.Edges[index].PrerequisiteResearchId,
                        prerequisiteResearchId,
                        StringComparison.Ordinal))
                    dependents.Add(
                        projection.Edges[index].DependentResearchId);
            return new JunctionRow(
                gameObject,
                prerequisiteResearchId,
                dependents.ToArray());
        }

        private static void DestroyGenerated(GameObject gameObject)
        {
            if (gameObject == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(gameObject);
            else
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private sealed class NodeRow
        {
            public NodeRow(
                Button button,
                Image icon,
                Text name,
                Text state)
            {
                Button = button;
                Icon = icon;
                Name = name;
                State = state;
            }

            public Button Button { get; }
            public Image Icon { get; }
            public Text Name { get; }
            public Text State { get; }
        }

        private sealed class ConnectionRow
        {
            public ConnectionRow(
                GameObject gameObject,
                string prerequisiteResearchId,
                string dependentResearchId)
            {
                GameObject = gameObject;
                PrerequisiteResearchId = prerequisiteResearchId;
                DependentResearchId = dependentResearchId;
            }

            public GameObject GameObject { get; }
            public string PrerequisiteResearchId { get; }
            public string DependentResearchId { get; }
        }

        private sealed class JunctionRow
        {
            public JunctionRow(
                GameObject gameObject,
                string prerequisiteResearchId,
                string[] dependentResearchIds)
            {
                GameObject = gameObject;
                PrerequisiteResearchId = prerequisiteResearchId;
                DependentResearchIds = dependentResearchIds ??
                    Array.Empty<string>();
            }
            public GameObject GameObject { get; }
            public string PrerequisiteResearchId { get; }
            public string[] DependentResearchIds { get; }
        }

        private readonly struct ResourceIconSlot
        {
            public ResourceIconSlot(
                Image image,
                string resourceId,
                Vector2 baseAnchoredPosition)
            {
                Image = image;
                ResourceId = resourceId;
                BaseAnchoredPosition = baseAnchoredPosition;
            }

            public Image Image { get; }
            public string ResourceId { get; }
            public Vector2 BaseAnchoredPosition { get; }
        }
    }
}
