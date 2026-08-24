using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDefenseSettlementView3D : MonoBehaviour
    {
        private static readonly Color BlockerColor =
            new Color(.015f, .02f, .025f, .84f);
        private static readonly Color PanelColor =
            new Color(.055f, .075f, .09f, .98f);
        private static readonly Color ButtonColor =
            new Color(.17f, .34f, .42f, 1f);

        [SerializeField] private Canvas canvas;

        private readonly List<ButtonBinding> buttonBindings =
            new List<ButtonBinding>();
        private GameObject blockerRoot;
        private RectTransform panelRoot;
        private Text titleLabel;
        private Text statisticsLabel;
        private Text feedbackLabel;
        private IReadOnlyList<SingleCityDefenseSettlementAction>
            visibleActions = Array.Empty<
                SingleCityDefenseSettlementAction>();

        public bool IsOpen { get; private set; }
        public bool BlocksInput => IsOpen;
        public GameObject BlockerRoot => blockerRoot;
        public SingleCityDefenseSettlementSnapshot Snapshot { get; private set; }
        public string TitleText { get; private set; } = string.Empty;
        public string StatisticsText { get; private set; } = string.Empty;
        public string FeedbackText { get; private set; } = string.Empty;
        public IReadOnlyList<SingleCityDefenseSettlementAction>
            VisibleActions => visibleActions;

        public event Action<SingleCityDefenseSettlementAction>
            ActionRequested;

        public void Configure(Canvas configuredCanvas)
        {
            canvas = configuredCanvas ??
                throw new ArgumentNullException(nameof(configuredCanvas));
        }

        public bool Open(SingleCityDefenseSettlementSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (IsOpen && Snapshot != null &&
                Snapshot.TerminalRevision == snapshot.TerminalRevision)
            {
                return false;
            }
            if (IsOpen) Close();

            Snapshot = snapshot;
            TitleText = snapshot.Result ==
                SingleCityDefenseCampaignResult.Victory
                    ? "防御战役完成"
                    : "城市核心失守";
            StatisticsText = FormatStatistics(snapshot);
            FeedbackText = string.Empty;
            visibleActions = CopyActions(snapshot.AvailableActions);
            IsOpen = true;

            if (Application.isPlaying)
                BuildRuntimeUi();
            ApplyText();
            return true;
        }

        public void SetFeedback(string message)
        {
            FeedbackText = message ?? string.Empty;
            if (feedbackLabel != null)
            {
                feedbackLabel.text = FeedbackText;
                feedbackLabel.gameObject.SetActive(
                    !string.IsNullOrWhiteSpace(FeedbackText));
            }
        }

        public void RequestAction(
            SingleCityDefenseSettlementAction action)
        {
            if (!IsOpen || !ContainsAction(action)) return;
            ActionRequested?.Invoke(action);
        }

        public void Close()
        {
            for (var index = 0; index < buttonBindings.Count; index++)
            {
                ButtonBinding binding = buttonBindings[index];
                if (binding.Button != null)
                {
                    binding.Button.onClick.RemoveListener(
                        binding.Callback);
                }
            }
            buttonBindings.Clear();
            ActionRequested = null;

            if (blockerRoot != null)
            {
                blockerRoot.SetActive(false);
                if (Application.isPlaying)
                    Destroy(blockerRoot);
                else
                    DestroyImmediate(blockerRoot);
            }
            blockerRoot = null;
            panelRoot = null;
            titleLabel = null;
            statisticsLabel = null;
            feedbackLabel = null;
            Snapshot = null;
            TitleText = string.Empty;
            StatisticsText = string.Empty;
            FeedbackText = string.Empty;
            visibleActions = Array.Empty<
                SingleCityDefenseSettlementAction>();
            IsOpen = false;
        }

        private void OnDestroy()
        {
            Close();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (panelRoot == null || canvas == null) return;
            panelRoot.sizeDelta = ResolvePanelSize();
            panelRoot.localScale = Vector3.one;
        }

        private void BuildRuntimeUi()
        {
            if (blockerRoot != null) return;
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                    throw new InvalidOperationException(
                        "结算界面需要既有 Canvas");
            }

            RectTransform blocker = CreateRect(
                canvas.transform,
                "Defense.Settlement.Blocker");
            blocker.anchorMin = Vector2.zero;
            blocker.anchorMax = Vector2.one;
            blocker.offsetMin = Vector2.zero;
            blocker.offsetMax = Vector2.zero;
            blocker.SetAsLastSibling();
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = BlockerColor;
            blockerImage.raycastTarget = true;
            blockerRoot = blocker.gameObject;

            RectTransform panel = CreateRect(
                blocker,
                "Defense.Settlement.Panel");
            panel.anchorMin = new Vector2(.5f, .5f);
            panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = ResolvePanelSize();
            panel.localScale = Vector3.one;
            panelRoot = panel;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;

            titleLabel = CreateText(
                panel,
                "Defense.Settlement.Title",
                new Vector2(32f, -82f),
                new Vector2(-32f, -20f),
                32,
                TextAnchor.MiddleCenter);
            RectTransform statisticsScrollRoot = CreateRect(
                panel,
                "Defense.Settlement.Statistics.Scroll");
            statisticsScrollRoot.anchorMin = Vector2.zero;
            statisticsScrollRoot.anchorMax = Vector2.one;
            statisticsScrollRoot.offsetMin = new Vector2(48f, 146f);
            statisticsScrollRoot.offsetMax = new Vector2(-48f, -92f);
            var statisticsScroll = statisticsScrollRoot.gameObject
                .AddComponent<ScrollRect>();
            statisticsScroll.horizontal = false;
            statisticsScroll.vertical = true;
            statisticsScroll.movementType =
                ScrollRect.MovementType.Clamped;
            statisticsScroll.scrollSensitivity = 32f;

            RectTransform statisticsViewport = CreateRect(
                statisticsScrollRoot,
                "Defense.Settlement.Statistics.Viewport");
            statisticsViewport.anchorMin = Vector2.zero;
            statisticsViewport.anchorMax = Vector2.one;
            statisticsViewport.offsetMin = Vector2.zero;
            statisticsViewport.offsetMax = Vector2.zero;
            Image statisticsMaskImage = statisticsViewport.gameObject
                .AddComponent<Image>();
            statisticsMaskImage.color = new Color(1f, 1f, 1f, .01f);
            statisticsViewport.gameObject.AddComponent<RectMask2D>();

            statisticsLabel = CreateText(
                statisticsViewport,
                "Defense.Settlement.Statistics",
                Vector2.zero,
                Vector2.zero,
                18,
                TextAnchor.UpperLeft);
            RectTransform statisticsContent =
                statisticsLabel.rectTransform;
            statisticsContent.anchorMin = new Vector2(0f, 1f);
            statisticsContent.anchorMax = Vector2.one;
            statisticsContent.pivot = new Vector2(.5f, 1f);
            statisticsContent.anchoredPosition = Vector2.zero;
            statisticsContent.sizeDelta = new Vector2(0f, 620f);
            statisticsLabel.lineSpacing = 1.05f;
            statisticsScroll.viewport = statisticsViewport;
            statisticsScroll.content = statisticsContent;
            feedbackLabel = CreateText(
                panel,
                "Defense.Settlement.Feedback",
                new Vector2(48f, 98f),
                new Vector2(-48f, 126f),
                17,
                TextAnchor.MiddleCenter);
            feedbackLabel.color = new Color(1f, .82f, .42f, 1f);

            float totalWidth = visibleActions.Count * 224f;
            float startX = -totalWidth * .5f + 108f;
            for (var index = 0; index < visibleActions.Count; index++)
            {
                SingleCityDefenseSettlementAction action =
                    visibleActions[index];
                Button button = CreateButton(
                    panel,
                    "Defense.Settlement.Action." + action,
                    new Vector2(startX + index * 224f, 48f),
                    new Vector2(208f, 52f));
                Text label = CreateText(
                    button.transform,
                    "Label",
                    new Vector2(8f, 4f),
                    new Vector2(-8f, -4f),
                    18,
                    TextAnchor.MiddleCenter);
                label.text = ActionLabel(action);
                UnityEngine.Events.UnityAction callback =
                    () => RequestAction(action);
                button.onClick.AddListener(callback);
                buttonBindings.Add(new ButtonBinding(button, callback));
            }
        }

        private Vector2 ResolvePanelSize()
        {
            RectTransform canvasRect = canvas == null
                ? null
                : canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect == null
                ? Vector2.zero
                : canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
                canvasSize = canvas == null
                    ? Vector2.zero
                    : canvas.pixelRect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
                canvasSize = FormalUiLayoutProfile3D.Standard
                    .ReferenceResolution;
            Rect modal = FormalUiLayoutPolicy3D.Calculate(
                    new Rect(0f, 0f, canvasSize.x, canvasSize.y))
                .MainModalArea;
            return new Vector2(
                Mathf.Min(860f, modal.width),
                Mathf.Min(760f, modal.height));
        }

        private void ApplyText()
        {
            if (titleLabel != null) titleLabel.text = TitleText;
            if (statisticsLabel != null)
                statisticsLabel.text = StatisticsText;
            SetFeedback(FeedbackText);
        }

        private bool ContainsAction(
            SingleCityDefenseSettlementAction action)
        {
            for (var index = 0; index < visibleActions.Count; index++)
            {
                if (visibleActions[index] == action) return true;
            }
            return false;
        }

        private static IReadOnlyList<SingleCityDefenseSettlementAction>
            CopyActions(
                IReadOnlyList<SingleCityDefenseSettlementAction> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<
                    SingleCityDefenseSettlementAction>();
            }
            var copy = new SingleCityDefenseSettlementAction[source.Count];
            for (var index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return Array.AsReadOnly(copy);
        }

        private static string FormatStatistics(
            SingleCityDefenseSettlementSnapshot snapshot)
        {
            var builder = new StringBuilder(1024);
            builder.Append("战斗用时：")
                .Append(FormatDuration(snapshot.ElapsedRuleSeconds))
                .AppendLine();
            builder.Append("完成波次：")
                .Append(snapshot.CompletedWaveCount).AppendLine();
            builder.Append("总击杀：")
                .Append(snapshot.TotalKillCount).AppendLine();
            builder.Append("同屏敌人峰值：")
                .Append(snapshot.HighestAliveEnemyCount).AppendLine();
            builder.Append("建筑损失：")
                .Append(snapshot.BuildingLossCount).AppendLine();
            builder.Append("核心耐久：")
                .Append(snapshot.CoreCurrentHealth).Append('/')
                .Append(snapshot.CoreMaximumHealth).AppendLine();
            builder.Append("完成生产批次：")
                .Append(snapshot.CompletedProductionBatchCount).AppendLine();
            builder.Append("生产运转效率：")
                .Append(snapshot.HasProductionEfficiency
                    ? (snapshot.ProductionEfficiency * 100f).ToString(
                        "0.0",
                        CultureInfo.InvariantCulture) + "%"
                    : "无可用数据")
                .AppendLine();
            builder.Append("防守方式：")
                .Append(snapshot.DefenseStyle ==
                    SingleCityDefenseStyle.MobileDefense
                        ? "机动防守"
                        : "坚守防御")
                .AppendLine();
            builder.Append("统计来源：")
                .Append(snapshot.PartialFromMigration
                    ? "迁移前统计不完整"
                    : "本次战役完整统计")
                .AppendLine();
            builder.Append("开发修改器：")
                .Append(snapshot.DeveloperModifierUsed
                    ? "使用过开发修改器"
                    : "未使用")
                .AppendLine();

            AppendMetrics(
                builder,
                "敌人击杀",
                snapshot.EnemyKills,
                EnemyName);
            AppendMetrics(
                builder,
                "防御塔伤害",
                snapshot.TowerDamage,
                BuildingName);
            AppendMetrics(
                builder,
                "防御塔击杀",
                snapshot.TowerKills,
                BuildingName);
            AppendMetrics(
                builder,
                "消耗品支出",
                snapshot.ConsumablesSpent,
                ResourceName);
            return builder.ToString().TrimEnd();
        }

        private static void AppendMetrics(
            StringBuilder builder,
            string heading,
            IReadOnlyList<SingleCityDefenseSettlementMetric> metrics,
            Func<string, string> displayName)
        {
            builder.Append(heading).Append("：");
            if (metrics == null || metrics.Count == 0)
            {
                builder.Append('0').AppendLine();
                return;
            }
            for (var index = 0; index < metrics.Count; index++)
            {
                if (index > 0) builder.Append("，");
                builder.Append(displayName(metrics[index].StableId))
                    .Append(' ')
                    .Append(metrics[index].Amount);
            }
            builder.AppendLine();
        }

        private static string EnemyName(string stableId)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                EnemyDefinition definition = EnemyCatalog.All[index];
                if (string.Equals(
                        definition.Id.Value,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return definition.Name;
                }
            }
            return stableId ?? string.Empty;
        }

        private static string BuildingName(string stableId)
        {
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition definition = BuildingCatalog.All[index];
                if (string.Equals(
                        definition.Id.Value,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return definition.Name;
                }
            }
            return stableId ?? string.Empty;
        }

        private static string ResourceName(string stableId)
        {
            return ResourceDefinitionCatalog.TryGet(
                stableId,
                out ResourceDefinition definition)
                    ? definition.ChineseName
                    : stableId ?? string.Empty;
        }

        private static string FormatDuration(float seconds)
        {
            float safeSeconds = Math.Max(0f, seconds);
            int wholeMinutes = (int)(safeSeconds / 60f);
            float remainingSeconds = safeSeconds - wholeMinutes * 60f;
            return wholeMinutes.ToString(CultureInfo.InvariantCulture) +
                   ":" + remainingSeconds.ToString(
                       "00.0",
                       CultureInfo.InvariantCulture);
        }

        private static string ActionLabel(
            SingleCityDefenseSettlementAction action)
        {
            switch (action)
            {
                case SingleCityDefenseSettlementAction.ContinueSandbox:
                    return "继续沙盒";
                case SingleCityDefenseSettlementAction.RetryWaveCheckpoint:
                    return "读取最近波前";
                case SingleCityDefenseSettlementAction.ReturnToTitle:
                    return "返回标题";
                default:
                    return "不可用操作";
            }
        }

        private static RectTransform CreateRect(
            Transform parent,
            string objectName)
        {
            var root = new GameObject(
                objectName,
                typeof(RectTransform));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(parent, objectName);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            FormalUiCanvasConfiguration3D.ApplyReadableFontSize(
                text,
                fontSize);
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string objectName,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rect = CreateRect(parent, objectName);
            rect.anchorMin = new Vector2(.5f, 0f);
            rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private sealed class ButtonBinding
        {
            public ButtonBinding(
                Button button,
                UnityEngine.Events.UnityAction callback)
            {
                Button = button;
                Callback = callback;
            }

            public Button Button { get; }
            public UnityEngine.Events.UnityAction Callback { get; }
        }
    }
}
