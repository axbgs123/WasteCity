using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WasteCity.Economy;
using WasteCity.Graybox3D.Usability;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
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
        private readonly Dictionary<DevelopmentRoute, Button> routeButtons =
            new Dictionary<DevelopmentRoute, Button>();
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
        private bool userNavigated;
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
            if (definition == null ||
                !nodeRows.TryGetValue(definition.Id.Value, out NodeRow row))
            {
                return;
            }

            row.Name.text = definition.Name;
            row.Details.text = ResearchDetails(definition);
            row.State.text = stateText ?? string.Empty;
            row.Button.image.color = isSelected
                ? SelectedColor
                : ButtonColor;
            if (isSelected)
                selectedResearchId = definition.Id.Value;
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
            if (!force && userNavigated ||
                projection?.FindNode(researchId) == null)
            {
                return;
            }
            ApplyViewportState(projection.Focus(
                new[] { researchId },
                ViewportSize(),
                72f));
        }

        public void NotifyOpened(string latestResearchableId)
        {
            userNavigated = false;
            if (string.IsNullOrEmpty(latestResearchableId))
                FitAll();
            else
                FocusResearch(latestResearchableId, force: true);
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
            Text title = CreateLabel(
                panel,
                "Research.Title",
                "正式四路线科技树",
                20);
            SetTopRect(title.rectTransform, 10f, 12f, 32f);
            ownedRoots.Add(title.gameObject);

            searchInput = CreateInputField(panel, "Research.Search");
            RectTransform searchRect = searchInput.GetComponent<RectTransform>();
            searchRect.anchorMin = new Vector2(0f, 1f);
            searchRect.anchorMax = new Vector2(0f, 1f);
            searchRect.pivot = new Vector2(0f, 1f);
            searchRect.anchoredPosition = new Vector2(12f, -50f);
            searchRect.sizeDelta = new Vector2(300f, 32f);
            searchInput.onValueChanged.AddListener(HandleSearchChanged);
            searchInput.onEndEdit.AddListener(_ => searchHasFocus = false);
            searchInput.gameObject
                .AddComponent<GrayboxResearchSearchFocus3D>()
                .Configure(value => searchHasFocus = value);
            ownedRoots.Add(searchInput.gameObject);

            RectTransform filters = CreateRect(panel, "Research.Filters");
            filters.anchorMin = new Vector2(1f, 1f);
            filters.anchorMax = new Vector2(1f, 1f);
            filters.pivot = new Vector2(1f, 1f);
            filters.anchoredPosition = new Vector2(-12f, -50f);
            filters.sizeDelta = new Vector2(548f, 32f);
            var filterLayout = filters.gameObject
                .AddComponent<HorizontalLayoutGroup>();
            filterLayout.spacing = 6f;
            filterLayout.childForceExpandWidth = true;
            filterLayout.childForceExpandHeight = true;
            ownedRoots.Add(filters.gameObject);
            CreateRouteFilter(
                filters,
                DevelopmentRoute.Technology,
                "Technology",
                "科技");
            CreateRouteFilter(
                filters,
                DevelopmentRoute.Cultivation,
                "Cultivation",
                "修仙");
            CreateRouteFilter(
                filters,
                DevelopmentRoute.Biological,
                "Biological",
                "血肉");
            CreateRouteFilter(
                filters,
                DevelopmentRoute.Psionics,
                "Psionics",
                "灵能");

            viewport = CreateRect(panel, "Research.Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 112f);
            viewport.offsetMax = new Vector2(-12f, -94f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(.035f, .05f, .06f, .98f);
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
            BuildNodes();
            BuildConnections();

            BuildActiveResearch();
            BuildActions();
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
                rect.sizeDelta = ResearchTreeProjection3D.NodeSize;

                Text name = CreateLabel(
                    rect,
                    "Research.Node." + researchId + ".Name",
                    definition.Name,
                    14);
                Anchor(name.rectTransform, .72f, 1f);
                Text details = CreateLabel(
                    rect,
                    "Research.Node." + researchId + ".Details",
                    ResearchDetails(definition),
                    9);
                details.alignment = TextAnchor.MiddleLeft;
                Anchor(details.rectTransform, .18f, .72f);
                details.rectTransform.offsetMin = new Vector2(5f, 0f);
                details.rectTransform.offsetMax = new Vector2(-5f, 0f);
                Text state = CreateLabel(
                    rect,
                    "Research.Node." + researchId + ".State",
                    "待刷新",
                    11);
                Anchor(state.rectTransform, 0f, .18f);
                AddCostIcons(rect, definition);
                nodeRows.Add(
                    researchId,
                    new NodeRow(button, name, details, state));
            }
        }

        private void BuildConnections()
        {
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
                ResearchTreeNodeProjection3D prerequisite =
                    projection.FindNode(edge.PrerequisiteResearchId);
                ResearchTreeNodeProjection3D dependent =
                    projection.FindNode(edge.DependentResearchId);
                graphic.Configure(
                    prerequisite.Position,
                    dependent.Position,
                    ConnectionColor(dependent.Definition.Route),
                    4f);
                connectionRows.Add(
                    key,
                    new ConnectionRow(
                        rect.gameObject,
                        edge.PrerequisiteResearchId,
                        edge.DependentResearchId));
            }
        }

        private void BuildActiveResearch()
        {
            activeRoot = CreateRect(panel, "Research.Active");
            activeRoot.anchorMin = new Vector2(0f, 0f);
            activeRoot.anchorMax = new Vector2(1f, 0f);
            activeRoot.pivot = new Vector2(.5f, 0f);
            activeRoot.offsetMin = new Vector2(12f, 54f);
            activeRoot.offsetMax = new Vector2(-12f, 102f);
            Image background = activeRoot.gameObject.AddComponent<Image>();
            background.color = new Color(.1f, .16f, .18f, .96f);
            background.raycastTarget = false;
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
                13);
            Anchor(activeProgress.rectTransform, 0f, .5f);
            activeRoot.gameObject.SetActive(false);
            ownedRoots.Add(activeRoot.gameObject);
        }

        private void BuildActions()
        {
            RectTransform actions = CreateRect(panel, "Research.Actions");
            actions.anchorMin = Vector2.zero;
            actions.anchorMax = new Vector2(1f, 0f);
            actions.pivot = new Vector2(.5f, 0f);
            actions.offsetMin = new Vector2(12f, 10f);
            actions.offsetMax = new Vector2(-12f, 44f);
            var layout = actions.gameObject
                .AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
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
                "取消研究",
                () => cancelRequested?.Invoke());
            CreateButton(
                actions,
                "Research.FitAll",
                "显示全树",
                FitAll);
            focusActiveButton = CreateButton(
                actions,
                "Research.FocusActive",
                "定位进行中",
                () => FocusResearch(activeResearchId, force: true));
            focusActiveButton.interactable = false;
            CreateButton(
                actions,
                "Research.Close",
                "关闭",
                () => closeRequested?.Invoke());
            ownedRoots.Add(actions.gameObject);
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
            routeButtons.Add(route, button);
        }

        private void ToggleRoute(DevelopmentRoute route)
        {
            if (!enabledRoutes.Remove(route))
                enabledRoutes.Add(route);
            ApplyFilter();
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
                if (RouteEnabled(definition) && Matches(definition, query))
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
                if (RouteEnabled(definition) && Matches(definition, query))
                    AddWithPrerequisites(definition.Id.Value, visible);
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
                    ? SelectedColor
                    : FilterDisabledColor;
            }
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
            for (var index = 0; index < definition.Costs.Count; index++)
            {
                ResourceAmount amount = definition.Costs[index];
                RectTransform rect = CreateRect(
                    parent,
                    "Research.Node." + definition.Id.Value + ".Cost." +
                    amount.ResourceId + ".Icon");
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(15f + index * 20f, 11f);
                rect.sizeDelta = new Vector2(16f, 16f);
                Image image = rect.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                resourceIcons.Add(new ResourceIconSlot(
                    image,
                    amount.ResourceId));
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
            selected?.Invoke(researchId);
        }

        private void ClearGeneratedUi()
        {
            for (var index = ownedRoots.Count - 1; index >= 0; index--)
                DestroyGenerated(ownedRoots[index]);
            ownedRoots.Clear();
            nodeRows.Clear();
            connectionRows.Clear();
            routeButtons.Clear();
            resourceIcons.Clear();
            viewport = null;
            content = null;
            nodesLayer = null;
            connectionsLayer = null;
            activeRoot = null;
            activeName = null;
            activeProgress = null;
            searchInput = null;
            searchHasFocus = false;
            startButton = null;
            cancelButton = null;
            focusActiveButton = null;
            activeResearchId = null;
            selectedResearchId = null;
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
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;

            Text text = CreateLabel(rect, name + ".Text", string.Empty, 14);
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin = new Vector2(8f, 2f);
            text.rectTransform.offsetMax = new Vector2(-8f, -2f);
            Text placeholder = CreateLabel(
                rect,
                name + ".Placeholder",
                "搜索科技名称或编号",
                14);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(.7f, .75f, .78f, 1f);
            placeholder.rectTransform.offsetMin = new Vector2(8f, 2f);
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
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (callback != null)
                button.onClick.AddListener(() => callback());
            if (!string.IsNullOrEmpty(label))
                CreateLabel(rect, name + ".Label", label, 13);
            return button;
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
                    return new Color(.32f, .7f, .88f, .8f);
                case DevelopmentRoute.Cultivation:
                    return new Color(.38f, .86f, .62f, .8f);
                case DevelopmentRoute.BiologicalAscension:
                    return new Color(.82f, .4f, .38f, .8f);
                case DevelopmentRoute.Psionics:
                    return new Color(.7f, .48f, .92f, .8f);
                default:
                    return new Color(.72f, .76f, .78f, .72f);
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
                Text name,
                Text details,
                Text state)
            {
                Button = button;
                Name = name;
                Details = details;
                State = state;
            }

            public Button Button { get; }
            public Text Name { get; }
            public Text Details { get; }
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

        private readonly struct ResourceIconSlot
        {
            public ResourceIconSlot(Image image, string resourceId)
            {
                Image = image;
                ResourceId = resourceId;
            }

            public Image Image { get; }
            public string ResourceId { get; }
        }
    }
}
