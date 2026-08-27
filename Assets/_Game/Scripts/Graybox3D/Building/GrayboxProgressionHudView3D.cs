using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProgressionHudView3D : MonoBehaviour
    {
        private static readonly Color StatusColor =
            new Color(.055f, .09f, .12f, .94f);
        private static readonly Color DetailsColor =
            new Color(.035f, .055f, .075f, .97f);

        [SerializeField] private Canvas canvas;

        private GameObject fallbackCanvasObject;
        private RectTransform uiRoot;
        private RectTransform statusRoot;
        private RectTransform detailsRect;
        private GameObject detailsBlocker;
        private Button statusButton;
        private Button closeButton;
        private Button fateDetailsButton;
        private GameObject detailsRoot;
        private Text valueLabel;
        private Text stageLabel;
        private Text thresholdLabel;
        private Text recentReasonsLabel;
        private Button pressureStatusButton;
        private GameObject pressureDetailsRoot;
        private Text pressureDetailsLabel;
        private IReadOnlyList<string> recentReasonTexts =
            Array.Empty<string>();
        private IReadOnlyList<string> preparedFateIds =
            Array.Empty<string>();

        public string AttentionValueText { get; private set; } = string.Empty;
        public string AttentionStageText { get; private set; } = string.Empty;
        public string NextThresholdText { get; private set; } = string.Empty;
        public IReadOnlyList<string> RecentReasonTexts => recentReasonTexts;
        public IReadOnlyList<string> PreparedFateIds => preparedFateIds;
        public bool IsFateSelectionOpen { get; private set; }
        public int RenderCount { get; private set; }
        public bool IsDetailsOpen { get; private set; }
        public string PressureStatusText { get; private set; } = string.Empty;
        public string BossStatusText { get; private set; } = string.Empty;
        public string BossPhaseText { get; private set; } = string.Empty;
        public int PressureRenderCount { get; private set; }
        public bool IsPressureDetailsOpen { get; private set; }

        public event Action FateDetailsRequested;

        public void Configure(Canvas configuredCanvas)
        {
            if (configuredCanvas == null)
                throw new ArgumentNullException(nameof(configuredCanvas));
            if (ReferenceEquals(canvas, configuredCanvas) && uiRoot != null)
                return;
            TeardownUi();
            canvas = configuredCanvas;
            EnsureUi();
        }

        public void Apply(
            string attentionValueText,
            string attentionStageText,
            string nextThresholdText,
            IReadOnlyList<string> recentReasons,
            IReadOnlyList<string> fateIds,
            bool effectsReady)
        {
            AttentionValueText = attentionValueText ?? string.Empty;
            AttentionStageText = attentionStageText ?? string.Empty;
            NextThresholdText = nextThresholdText ?? string.Empty;
            recentReasonTexts = Copy(recentReasons);
            preparedFateIds = Copy(fateIds);
            if (!effectsReady) IsFateSelectionOpen = false;
            unchecked { RenderCount++; }

            EnsureUi();
            valueLabel.text = AttentionValueText;
            stageLabel.text = AttentionStageText;
            thresholdLabel.text = NextThresholdText;
            recentReasonsLabel.text = recentReasonTexts.Count == 0
                ? "最近变化\n暂无关注度变化"
                : "最近变化\n" + string.Join("\n", recentReasonTexts);
            detailsRoot.SetActive(IsDetailsOpen);
        }

        public void CloseDetails()
        {
            IsDetailsOpen = false;
            ClosePressureDetails();
            if (detailsRoot != null) detailsRoot.SetActive(false);
            if (detailsBlocker != null) detailsBlocker.SetActive(false);
            EventSystem current = EventSystem.current;
            GameObject selected = current?.currentSelectedGameObject;
            if (selected != null && uiRoot != null &&
                selected.transform.IsChildOf(uiRoot))
            {
                current.SetSelectedGameObject(null);
            }
        }

        public void ApplyPressure(
            string pressureStatus,
            string bossStatus,
            string bossPhase)
        {
            PressureStatusText = pressureStatus ?? string.Empty;
            BossStatusText = bossStatus ?? string.Empty;
            BossPhaseText = bossPhase ?? string.Empty;
            unchecked { PressureRenderCount++; }
            EnsureUi();
            Text label = pressureStatusButton.GetComponentInChildren<Text>();
            label.text = PressureStatusText;
            pressureDetailsLabel.text = PressureStatusText + "\n" +
                BossStatusText + "\n" + BossPhaseText;
        }

        public void ClosePressureDetails()
        {
            IsPressureDetailsOpen = false;
            IsDetailsOpen = false;
            if (pressureDetailsRoot != null)
                pressureDetailsRoot.SetActive(false);
        }

        private void OpenPressureDetails()
        {
            IsPressureDetailsOpen = true;
            IsDetailsOpen = true;
            pressureDetailsRoot.SetActive(true);
        }

        private void OpenDetails()
        {
            IsDetailsOpen = true;
            if (detailsBlocker != null) detailsBlocker.SetActive(true);
            if (detailsRoot != null) detailsRoot.SetActive(true);
        }

        private void EnsureUi()
        {
            EnsureCanvas();
            if (uiRoot != null) return;

            uiRoot = CreateRect(canvas.transform, "Progression.Hud");
            Stretch(uiRoot);
            uiRoot.SetAsLastSibling();

            RectTransform status = CreateRect(
                uiRoot,
                "Progression.AttentionStatus");
            statusRoot = status;
            Image statusImage = status.gameObject.AddComponent<Image>();
            statusImage.color = StatusColor;
            statusImage.raycastTarget = true;
            statusButton = status.gameObject.AddComponent<Button>();
            statusButton.targetGraphic = statusImage;
            statusButton.onClick.AddListener(OpenDetails);

            valueLabel = CreateText(
                status,
                "Progression.AttentionStatus.Value",
                new Vector2(12f, 38f),
                new Vector2(-12f, -4f),
                20,
                TextAnchor.MiddleLeft);
            stageLabel = CreateText(
                status,
                "Progression.AttentionStatus.Stage",
                new Vector2(12f, 8f),
                new Vector2(-128f, -38f),
                16,
                TextAnchor.MiddleLeft);
            thresholdLabel = CreateText(
                status,
                "Progression.AttentionStatus.NextThreshold",
                new Vector2(120f, 8f),
                new Vector2(-12f, -38f),
                14,
                TextAnchor.MiddleRight);

            RectTransform pressureStatus = CreateRect(
                uiRoot, "Progression.AttentionPressure.Status");
            pressureStatus.anchorMin = new Vector2(0f, 1f);
            pressureStatus.anchorMax = new Vector2(0f, 1f);
            pressureStatus.pivot = new Vector2(0f, 1f);
            pressureStatus.anchoredPosition = new Vector2(18f, -98f);
            pressureStatus.sizeDelta = new Vector2(300f, 52f);
            Image pressureImage = pressureStatus.gameObject.AddComponent<Image>();
            pressureImage.color = StatusColor;
            pressureImage.raycastTarget = true;
            pressureStatusButton = pressureStatus.gameObject.AddComponent<Button>();
            pressureStatusButton.targetGraphic = pressureImage;
            pressureStatusButton.onClick.AddListener(OpenPressureDetails);
            CreateText(pressureStatus, "Label", Vector2.zero, Vector2.zero,
                15, TextAnchor.MiddleCenter);

            RectTransform pressureDetails = CreateRect(
                uiRoot, "Progression.AttentionPressure.Details");
            Stretch(pressureDetails);
            Image pressureBlocker = pressureDetails.gameObject.AddComponent<Image>();
            pressureBlocker.color = new Color(0f, 0f, 0f, .72f);
            pressureBlocker.raycastTarget = true;
            pressureDetailsRoot = pressureDetails.gameObject;
            pressureDetailsLabel = CreateText(pressureDetails, "Content",
                new Vector2(220f, 180f), new Vector2(-220f, -180f), 20,
                TextAnchor.MiddleCenter);
            pressureDetailsRoot.SetActive(false);

            RectTransform blocker = CreateRect(
                uiRoot,
                "Progression.AttentionDetails.Blocker");
            Stretch(blocker);
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, .22f);
            blockerImage.raycastTarget = true;
            detailsBlocker = blocker.gameObject;

            RectTransform details = CreateRect(
                uiRoot,
                "Progression.AttentionDetails");
            detailsRect = details;
            Image detailsImage = details.gameObject.AddComponent<Image>();
            detailsImage.color = DetailsColor;
            detailsImage.raycastTarget = true;
            detailsRoot = details.gameObject;

            recentReasonsLabel = CreateText(
                details,
                "Progression.AttentionDetails.RecentReasons",
                new Vector2(18f, 54f),
                new Vector2(-18f, -42f),
                16,
                TextAnchor.UpperLeft);
            RectTransform fateDetails = CreateRect(
                details,
                "Progression.AttentionDetails.Fate");
            fateDetails.anchorMin = new Vector2(0f, 0f);
            fateDetails.anchorMax = new Vector2(0f, 0f);
            fateDetails.pivot = Vector2.zero;
            fateDetails.anchoredPosition = new Vector2(18f, 12f);
            fateDetails.sizeDelta = new Vector2(150f, 34f);
            Image fateImage = fateDetails.gameObject.AddComponent<Image>();
            fateImage.color = StatusColor;
            fateImage.raycastTarget = true;
            fateDetailsButton = fateDetails.gameObject.AddComponent<Button>();
            fateDetailsButton.targetGraphic = fateImage;
            fateDetailsButton.onClick.AddListener(
                HandleFateDetailsRequested);
            Text fateText = CreateText(
                fateDetails,
                "Progression.AttentionDetails.Fate.Label",
                Vector2.zero,
                Vector2.zero,
                14,
                TextAnchor.MiddleCenter);
            fateText.text = "命轨详情";
            RectTransform close = CreateRect(
                details,
                "Progression.AttentionDetails.Close");
            close.anchorMin = new Vector2(1f, 1f);
            close.anchorMax = new Vector2(1f, 1f);
            close.pivot = Vector2.one;
            close.anchoredPosition = new Vector2(-8f, -8f);
            close.sizeDelta = new Vector2(72f, 30f);
            Image closeImage = close.gameObject.AddComponent<Image>();
            closeImage.color = StatusColor;
            closeImage.raycastTarget = true;
            closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(CloseDetails);
            Text closeText = CreateText(
                close,
                "Progression.AttentionDetails.Close.Label",
                Vector2.zero,
                Vector2.zero,
                14,
                TextAnchor.MiddleCenter);
            closeText.text = "关闭";

            detailsRoot.SetActive(false);
            detailsBlocker.SetActive(false);
            IsDetailsOpen = false;
            RefreshFormalLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshFormalLayout();
        }

        private void RefreshFormalLayout()
        {
            if (canvas == null || uiRoot == null || statusRoot == null ||
                detailsRect == null)
            {
                return;
            }
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect == null
                ? FormalUiLayoutProfile3D.Standard.ReferenceResolution
                : canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
                canvasSize = FormalUiLayoutProfile3D.Standard
                    .ReferenceResolution;
            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                new Rect(Vector2.zero, canvasSize));
            ApplyCanvasRect(statusRoot, layout.AttentionStatusSlot);

            float detailsWidth = Mathf.Min(
                420f,
                layout.MainModalArea.width);
            float detailsHeight = Mathf.Min(
                220f,
                layout.MainModalArea.height);
            var details = new Rect(
                layout.MainModalArea.center.x - detailsWidth * .5f,
                layout.MainModalArea.center.y - detailsHeight * .5f,
                detailsWidth,
                detailsHeight);
            ApplyCanvasRect(detailsRect, details);
        }

        private static void ApplyCanvasRect(RectTransform target, Rect rect)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.zero;
            target.pivot = Vector2.zero;
            target.anchoredPosition = rect.position;
            target.sizeDelta = rect.size;
            target.localScale = Vector3.one;
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) return;

            fallbackCanvasObject = new GameObject(
                "Progression.Hud.FallbackCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            fallbackCanvasObject.transform.SetParent(transform, false);
            canvas = fallbackCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 360;
            CanvasScaler scaler =
                fallbackCanvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
        }

        private void TeardownUi()
        {
            GameObject previousUiRoot = uiRoot != null
                ? uiRoot.gameObject
                : null;
            if (statusButton != null)
                statusButton.onClick.RemoveListener(OpenDetails);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(CloseDetails);
            if (fateDetailsButton != null)
                fateDetailsButton.onClick.RemoveListener(
                    HandleFateDetailsRequested);
            if (pressureStatusButton != null)
                pressureStatusButton.onClick.RemoveListener(OpenPressureDetails);
            statusButton = null;
            closeButton = null;
            fateDetailsButton = null;
            pressureStatusButton = null;
            pressureDetailsRoot = null;
            pressureDetailsLabel = null;
            valueLabel = null;
            stageLabel = null;
            thresholdLabel = null;
            recentReasonsLabel = null;
            detailsRoot = null;
            detailsBlocker = null;
            statusRoot = null;
            detailsRect = null;
            uiRoot = null;

            if (fallbackCanvasObject != null)
            {
                DestroyObject(fallbackCanvasObject);
                fallbackCanvasObject = null;
                canvas = null;
            }
            else if (previousUiRoot != null)
            {
                DestroyObject(previousUiRoot);
            }
        }

        private void HandleFateDetailsRequested()
        {
            FateDetailsRequested?.Invoke();
        }

        private void OnDestroy()
        {
            TeardownUi();
        }

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();
            var result = new string[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index] ?? string.Empty;
            return Array.AsReadOnly(result);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var value = new GameObject(name, typeof(RectTransform));
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            FormalUiCanvasConfiguration3D.ApplyReadableFontSize(
                text,
                fontSize);
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
