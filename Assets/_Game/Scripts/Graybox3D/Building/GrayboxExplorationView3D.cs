using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Graybox3D.Exploration
{
    public sealed class GrayboxExplorationActionPresentation3D
    {
        public GrayboxExplorationActionPresentation3D(
            string label,
            bool enabled,
            string disabledReason)
        {
            Label = label ?? string.Empty;
            Enabled = enabled;
            DisabledReason = enabled
                ? string.Empty
                : disabledReason ?? string.Empty;
        }

        public string Label { get; }
        public bool Enabled { get; }
        public string DisabledReason { get; }
        public string DisplayText => Enabled ||
            string.IsNullOrWhiteSpace(DisabledReason)
                ? Label
                : Label + "\n不可用：" + DisabledReason;
    }

    public sealed class GrayboxExplorationPresentation3D
    {
        public GrayboxExplorationPresentation3D(
            string leaderName,
            string statusSummary,
            string controlModeText,
            string manualGatherTargetText,
            float manualGatherProgress,
            string manualGatherStatusText,
            string manualGatherFailureReason,
            string cenJinDistressStatusText,
            GrayboxExplorationActionPresentation3D controlAction,
            GrayboxExplorationActionPresentation3D gatherAction,
            GrayboxExplorationActionPresentation3D rescueAction,
            string characterVisualId = null,
            string statusVisualId = null,
            string visionRangeText = null)
        {
            LeaderName = TextOr(leaderName, "尚未招募");
            StatusSummary = TextOr(statusSummary, "暂无领袖状态");
            ControlModeText = TextOr(controlModeText, "AI 控制");
            ManualGatherTargetText = TextOr(
                manualGatherTargetText,
                "未选择资源节点");
            ManualGatherProgress = IsFinite(manualGatherProgress)
                ? Mathf.Clamp01(manualGatherProgress)
                : 0f;
            ManualGatherStatusText = TextOr(
                manualGatherStatusText,
                "未在采集");
            ManualGatherFailureReason = manualGatherFailureReason ??
                string.Empty;
            CenJinDistressStatusText = TextOr(
                cenJinDistressStatusText,
                "尚未发现求救信号");
            ControlAction = controlAction ?? Disabled(
                "接管领袖",
                "领袖状态不可用");
            GatherAction = gatherAction ?? Disabled(
                "开始采集",
                "未选择可采资源节点");
            RescueAction = rescueAction ?? Disabled(
                "开始岑烬救援",
                "尚未发现求救信号");
            CharacterVisualId = TextOr(
                characterVisualId,
                GrayboxExplorationView3D.CenJinCharacterVisualId);
            StatusVisualId = TextOr(
                statusVisualId,
                GrayboxExplorationView3D.FollowStatusVisualId);
            VisionRangeText = TextOr(
                visionRangeText,
                "未载入正式视野范围");
        }

        public string LeaderName { get; }
        public string StatusSummary { get; }
        public string ControlModeText { get; }
        public string ManualGatherTargetText { get; }
        public float ManualGatherProgress { get; }
        public string ManualGatherStatusText { get; }
        public string ManualGatherFailureReason { get; }
        public string CenJinDistressStatusText { get; }
        public GrayboxExplorationActionPresentation3D ControlAction { get; }
        public GrayboxExplorationActionPresentation3D GatherAction { get; }
        public GrayboxExplorationActionPresentation3D RescueAction { get; }
        public string CharacterVisualId { get; }
        public string StatusVisualId { get; }
        public string VisionRangeText { get; }

        private static GrayboxExplorationActionPresentation3D Disabled(
            string label,
            string reason)
        {
            return new GrayboxExplorationActionPresentation3D(
                label,
                false,
                reason);
        }

        private static string TextOr(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class GrayboxExplorationScanFeedback3D
    {
        public GrayboxExplorationScanFeedback3D(
            bool visible,
            string summary,
            bool warning)
        {
            Summary = summary ?? string.Empty;
            Visible = visible && !string.IsNullOrWhiteSpace(Summary);
            Warning = warning;
        }

        public bool Visible { get; }
        public string Summary { get; }
        public bool Warning { get; }
        public bool BlocksWorldInput => false;

        public static GrayboxExplorationScanFeedback3D ForCompletedScan(
            string zoneName,
            string discoverySummary,
            int revealedCellCount,
            int attentionDelta)
        {
            return new GrayboxExplorationScanFeedback3D(
                true,
                "自动扫描完成：" + (zoneName ?? string.Empty) +
                (string.IsNullOrWhiteSpace(discoverySummary)
                    ? string.Empty
                    : " · " + discoverySummary.Trim()) +
                " · 新增情报格 " + Mathf.Max(0, revealedCellCount) +
                " · 关注度 " + (attentionDelta >= 0 ? "+" : string.Empty) +
                attentionDelta,
                false);
        }
    }

    public sealed class GrayboxExplorationOutpostAlertPresentation3D
    {
        public GrayboxExplorationOutpostAlertPresentation3D(
            bool visible,
            string stableAlertId,
            string summary,
            string severityText)
        {
            StableAlertId = stableAlertId ?? string.Empty;
            Summary = summary ?? string.Empty;
            SeverityText = severityText ?? string.Empty;
            Visible = visible &&
                !string.IsNullOrWhiteSpace(StableAlertId) &&
                !string.IsNullOrWhiteSpace(Summary);
        }

        public bool Visible { get; }
        public string StableAlertId { get; }
        public string Summary { get; }
        public string SeverityText { get; }
        public bool CanFocus => Visible;
        public string DisplayText => string.IsNullOrWhiteSpace(SeverityText)
            ? Summary
            : "前哨" + SeverityText + " · " + Summary;
    }

    [DisallowMultipleComponent]
    public sealed class GrayboxExplorationView3D : MonoBehaviour
    {
        public const string CenJinCharacterVisualId =
            CharacterCatalog.CenJinId;
        public const string FollowStatusVisualId = "core.ui.status.follow";
        public const string RescueStatusVisualId = "core.ui.status.rescue";
        public const string OutpostMarkerVisualId =
            "core.world-marker.outpost";

        [SerializeField] private Canvas canvas;

        private GameObject fallbackCanvasObject;
        private GameObject panelRoot;
        private GameObject scanFeedbackRoot;
        private GameObject outpostAlertRoot;
        private Text headingText;
        private Text statusText;
        private Text gatherText;
        private Text distressText;
        private Text scanFeedbackText;
        private Text outpostAlertText;
        private Text controlActionLabel;
        private Text gatherActionLabel;
        private Text rescueActionLabel;
        private Image characterImage;
        private Image statusImage;
        private Image outpostImage;
        private GrayboxExplorationPresentation3D current;
        private GrayboxExplorationOutpostAlertPresentation3D currentAlert;

        public bool IsOpen { get; private set; }
        public Button LeaderControlButton { get; private set; }
        public Button ManualGatherButton { get; private set; }
        public Button CenJinRescueButton { get; private set; }
        public Button OutpostAlertButton { get; private set; }
        public Text HeadingText => headingText;
        public Text StatusText => statusText;
        public Text ManualGatherText => gatherText;
        public Text CenJinDistressText => distressText;
        public Text ScanFeedbackText => scanFeedbackText;
        public Text OutpostAlertText => outpostAlertText;

        public event Action LeaderControlToggleRequested;
        public event Action ManualGatherToggleRequested;
        public event Action CenJinRescueToggleRequested;
        public event Action<string> OutpostAlertFocusRequested;

        public void Configure(Canvas configuredCanvas)
        {
            canvas = configuredCanvas ??
                throw new ArgumentNullException(nameof(configuredCanvas));
            RebuildUi();
        }

        public void Apply(GrayboxExplorationPresentation3D presentation)
        {
            current = presentation ??
                throw new ArgumentNullException(nameof(presentation));
            EnsureUi();
            headingText.text = "领袖 · " + current.LeaderName;
            statusText.text = "状态：" + current.StatusSummary +
                "\n控制模式：" + current.ControlModeText +
                "\n视野：" + current.VisionRangeText;
            gatherText.text = BuildGatherText(current);
            distressText.text = "岑烬求救：" +
                current.CenJinDistressStatusText;
            ApplyAction(
                LeaderControlButton,
                controlActionLabel,
                current.ControlAction);
            ApplyAction(
                ManualGatherButton,
                gatherActionLabel,
                current.GatherAction);
            ApplyAction(
                CenJinRescueButton,
                rescueActionLabel,
                current.RescueAction);
            ApplyVisual(
                characterImage,
                Production2DVisualClass.Character,
                current.CharacterVisualId);
            ApplyVisual(
                statusImage,
                Production2DVisualClass.Ui,
                current.StatusVisualId);
        }

        public void ApplyScanFeedback(
            GrayboxExplorationScanFeedback3D feedback)
        {
            EnsureUi();
            feedback = feedback ?? new GrayboxExplorationScanFeedback3D(
                false,
                string.Empty,
                false);
            scanFeedbackText.text = feedback.Summary;
            scanFeedbackText.color = feedback.Warning
                ? new Color(.98f, .73f, .32f, 1f)
                : new Color(.68f, .95f, .90f, 1f);
            scanFeedbackRoot.SetActive(feedback.Visible);
        }

        public void ApplyOutpostAlert(
            GrayboxExplorationOutpostAlertPresentation3D alert)
        {
            EnsureUi();
            currentAlert = alert;
            bool visible = currentAlert != null && currentAlert.Visible;
            outpostAlertText.text = visible
                ? currentAlert.DisplayText
                : string.Empty;
            outpostAlertRoot.SetActive(visible);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            EnsureUi();
            IsOpen = true;
            panelRoot.SetActive(true);
        }

        public void Close()
        {
            ClearSelectedObject();
            IsOpen = false;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public bool RequestLeaderControlToggle()
        {
            if (current == null || !current.ControlAction.Enabled)
                return false;
            LeaderControlToggleRequested?.Invoke();
            return true;
        }

        public bool RequestManualGatherToggle()
        {
            if (current == null || !current.GatherAction.Enabled)
                return false;
            ManualGatherToggleRequested?.Invoke();
            return true;
        }

        public bool RequestCenJinRescueToggle()
        {
            if (current == null || !current.RescueAction.Enabled)
                return false;
            CenJinRescueToggleRequested?.Invoke();
            return true;
        }

        public bool RequestOutpostAlertFocus()
        {
            if (currentAlert == null || !currentAlert.CanFocus)
                return false;
            OutpostAlertFocusRequested?.Invoke(currentAlert.StableAlertId);
            return true;
        }

        private void EnsureUi()
        {
            EnsureCanvas();
            if (panelRoot == null) BuildUi();
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) return;
            fallbackCanvasObject = new GameObject(
                "Exploration.FallbackCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            fallbackCanvasObject.transform.SetParent(transform, false);
            canvas = fallbackCanvasObject.GetComponent<Canvas>();
            FormalUiCanvasConfiguration3D.Apply(canvas, 55);
        }

        private void RebuildUi()
        {
            TeardownUi();
            EnsureUi();
        }

        private void BuildUi()
        {
            RectTransform modal = Rect(
                canvas.transform,
                "Exploration.LeaderPanel.Root");
            Stretch(modal);
            Image shade = modal.gameObject.AddComponent<Image>();
            shade.color = new Color(.01f, .02f, .025f, .28f);
            shade.raycastTarget = true;
            panelRoot = modal.gameObject;

            RectTransform panel = Rect(
                modal,
                "Exploration.LeaderPanel");
            ApplyLeaderPanelLayout(panel);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(.045f, .08f, .095f, .98f);
            ApplySprite(
                panelImage,
                Production2DVisualClass.Ui,
                "core.ui.frame.primary-panel");

            RectTransform portrait = Rect(
                panel,
                "Exploration.LeaderPortrait");
            Anchors(portrait, .055f, .72f, .27f, .94f);
            characterImage = portrait.gameObject.AddComponent<Image>();
            characterImage.preserveAspect = true;
            characterImage.raycastTarget = false;

            RectTransform statusIcon = Rect(
                panel,
                "Exploration.LeaderStatusIcon");
            Anchors(statusIcon, .29f, .79f, .40f, .90f);
            statusImage = statusIcon.gameObject.AddComponent<Image>();
            statusImage.preserveAspect = true;
            statusImage.raycastTarget = false;

            headingText = TextLabel(
                panel,
                "Exploration.Heading",
                22,
                TextAnchor.MiddleLeft,
                .42f,
                .84f,
                .94f,
                .94f);
            statusText = TextLabel(
                panel,
                "Exploration.Status",
                16,
                TextAnchor.UpperLeft,
                .42f,
                .70f,
                .94f,
                .83f);
            gatherText = CardText(
                panel,
                "Exploration.ManualGather",
                .055f,
                .46f,
                .945f,
                .68f);
            distressText = CardText(
                panel,
                "Exploration.CenJinDistress",
                .055f,
                .34f,
                .945f,
                .45f);

            LeaderControlButton = ActionButton(
                panel,
                "Exploration.Action.LeaderControl",
                .055f,
                .225f,
                .335f,
                .325f,
                out controlActionLabel,
                RequestLeaderControlToggle);
            ManualGatherButton = ActionButton(
                panel,
                "Exploration.Action.ManualGather",
                .36f,
                .225f,
                .64f,
                .325f,
                out gatherActionLabel,
                RequestManualGatherToggle);
            CenJinRescueButton = ActionButton(
                panel,
                "Exploration.Action.CenJinRescue",
                .665f,
                .225f,
                .945f,
                .325f,
                out rescueActionLabel,
                RequestCenJinRescueToggle);

            Text hint = TextLabel(
                panel,
                "Exploration.Hint",
                14,
                TextAnchor.MiddleRight,
                .055f,
                .075f,
                .945f,
                .19f);
            hint.text = "L 打开/关闭领袖面板 · Esc 关闭";
            hint.color = new Color(.53f, .68f, .70f, 1f);

            BuildScanFeedback();
            BuildOutpostAlert();
            panelRoot.SetActive(IsOpen);
        }

        private void BuildScanFeedback()
        {
            RectTransform root = Rect(
                canvas.transform,
                "Exploration.ScanFeedback");
            root.anchorMin = new Vector2(.32f, .82f);
            root.anchorMax = new Vector2(.68f, .875f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(.035f, .10f, .12f, .94f);
            image.raycastTarget = false;
            ApplySprite(
                image,
                Production2DVisualClass.Ui,
                "core.ui.frame.secondary-card");
            scanFeedbackText = TextLabel(
                root,
                "Exploration.ScanFeedback.Text",
                15,
                TextAnchor.MiddleCenter,
                .03f,
                .06f,
                .97f,
                .94f);
            scanFeedbackRoot = root.gameObject;
            scanFeedbackRoot.SetActive(false);
        }

        private void BuildOutpostAlert()
        {
            RectTransform root = Rect(
                canvas.transform,
                "Exploration.OutpostAlert");
            root.anchorMin = new Vector2(.02f, .70f);
            root.anchorMax = new Vector2(.30f, .78f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(.24f, .09f, .055f, .97f);
            ApplySprite(
                image,
                Production2DVisualClass.Ui,
                "core.ui.frame.secondary-card");
            OutpostAlertButton = root.gameObject.AddComponent<Button>();
            OutpostAlertButton.targetGraphic = image;
            OutpostAlertButton.onClick.AddListener(
                () => RequestOutpostAlertFocus());

            RectTransform icon = Rect(
                root,
                "Exploration.OutpostAlert.Icon");
            Anchors(icon, .025f, .12f, .20f, .88f);
            outpostImage = icon.gameObject.AddComponent<Image>();
            outpostImage.preserveAspect = true;
            outpostImage.raycastTarget = false;
            ApplyVisual(
                outpostImage,
                Production2DVisualClass.WorldMarker,
                OutpostMarkerVisualId);
            outpostAlertText = TextLabel(
                root,
                "Exploration.OutpostAlert.Text",
                15,
                TextAnchor.MiddleLeft,
                .22f,
                .08f,
                .97f,
                .92f);
            outpostAlertRoot = root.gameObject;
            outpostAlertRoot.SetActive(false);
        }

        private void ApplyLeaderPanelLayout(RectTransform panel)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 size = canvasRect == null
                ? FormalUiLayoutProfile3D.Standard.ReferenceResolution
                : canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                size = FormalUiLayoutProfile3D.Standard.ReferenceResolution;
            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                new UnityEngine.Rect(0f, 0f, size.x, size.y));
            float width = Mathf.Clamp(
                layout.SafeArea.width * .33f,
                420f,
                600f);
            UnityEngine.Rect slot = new UnityEngine.Rect(
                layout.SafeArea.xMin,
                layout.BuildFeedbackSlot.yMax +
                FormalUiLayoutProfile3D.Standard.SpaceMedium,
                width,
                layout.ResourceStatusSlot.yMin -
                FormalUiLayoutProfile3D.Standard.SpaceMedium -
                layout.BuildFeedbackSlot.yMax);
            panel.anchorMin = new Vector2(
                slot.xMin / size.x,
                slot.yMin / size.y);
            panel.anchorMax = new Vector2(
                slot.xMax / size.x,
                slot.yMax / size.y);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }

        private static string BuildGatherText(
            GrayboxExplorationPresentation3D presentation)
        {
            string text = "手采目标：" +
                presentation.ManualGatherTargetText +
                "\n进度：" + Mathf.RoundToInt(
                    presentation.ManualGatherProgress * 100f) + "%" +
                " · " + presentation.ManualGatherStatusText;
            return string.IsNullOrWhiteSpace(
                presentation.ManualGatherFailureReason)
                ? text
                : text + "\n原因：" +
                  presentation.ManualGatherFailureReason;
        }

        private static void ApplyAction(
            Button button,
            Text label,
            GrayboxExplorationActionPresentation3D action)
        {
            label.text = action.DisplayText;
            button.interactable = action.Enabled;
        }

        private static void ApplyVisual(
            Image image,
            Production2DVisualClass visualClass,
            string contentId)
        {
            image.sprite = Production2DVisualCatalog3D.Resolve(
                visualClass,
                contentId);
            image.gameObject.SetActive(image.sprite != null);
            if (image.sprite != null)
            {
                Production2DVisualScalePolicy3D.ApplyToUiImage(
                    image,
                    visualClass,
                    Production2DVisualCatalog3D.ResolveVisibleBounds(
                        visualClass,
                        contentId),
                    Vector2.zero);
            }
        }

        private static void ApplySprite(
            Image image,
            Production2DVisualClass visualClass,
            string contentId)
        {
            image.sprite = Production2DVisualCatalog3D.Resolve(
                visualClass,
                contentId);
            image.type = image.sprite != null && image.sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
        }

        private static Text CardText(
            Transform parent,
            string name,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            RectTransform card = Rect(parent, name);
            Anchors(card, minX, minY, maxX, maxY);
            Image image = card.gameObject.AddComponent<Image>();
            image.color = new Color(.065f, .11f, .125f, .98f);
            image.raycastTarget = false;
            ApplySprite(
                image,
                Production2DVisualClass.Ui,
                "core.ui.frame.secondary-card");
            return TextLabel(
                card,
                name + ".Text",
                16,
                TextAnchor.MiddleLeft,
                .04f,
                .08f,
                .96f,
                .92f);
        }

        private static Button ActionButton(
            Transform parent,
            string name,
            float minX,
            float minY,
            float maxX,
            float maxY,
            out Text label,
            Func<bool> request)
        {
            RectTransform root = Rect(parent, name);
            Anchors(root, minX, minY, maxX, maxY);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(.14f, .34f, .37f, 1f);
            ApplySprite(
                image,
                Production2DVisualClass.Ui,
                "core.ui.control.primary-button");
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => request());
            label = TextLabel(
                root,
                name + ".Label",
                15,
                TextAnchor.MiddleCenter,
                .03f,
                .03f,
                .97f,
                .97f);
            return button;
        }

        private static Text TextLabel(
            Transform parent,
            string name,
            int fontSize,
            TextAnchor alignment,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            RectTransform root = Rect(parent, name);
            Anchors(root, minX, minY, maxX, maxY);
            Text text = root.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = new Color(.88f, .95f, .92f, 1f);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.gameObject.AddComponent<FormalUiReadableText3D>()
                .Configure(fontSize);
            return text;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            Anchors(rect, 0f, 0f, 1f, 1f);
        }

        private static void Anchors(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearSelectedObject()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void TeardownUi()
        {
            if (panelRoot != null) DestroyUiObject(panelRoot);
            if (scanFeedbackRoot != null) DestroyUiObject(scanFeedbackRoot);
            if (outpostAlertRoot != null) DestroyUiObject(outpostAlertRoot);
            if (fallbackCanvasObject != null)
                DestroyUiObject(fallbackCanvasObject);
            panelRoot = null;
            scanFeedbackRoot = null;
            outpostAlertRoot = null;
            fallbackCanvasObject = null;
            headingText = null;
            statusText = null;
            gatherText = null;
            distressText = null;
            scanFeedbackText = null;
            outpostAlertText = null;
            LeaderControlButton = null;
            ManualGatherButton = null;
            CenJinRescueButton = null;
            OutpostAlertButton = null;
        }

        private static void DestroyUiObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            TeardownUi();
        }
    }
}
