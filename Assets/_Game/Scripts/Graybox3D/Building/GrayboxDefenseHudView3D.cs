using System;
using System.Globalization;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Combat;

namespace WasteCity.Graybox3D.Building
{
    public class GrayboxDefenseHudView3D : MonoBehaviour
    {
        private static readonly ProfilerMarker ApplyMarker =
            new ProfilerMarker("WasteCity.Formal.DefenseHud.Apply");
        private static readonly Color PanelColor =
            new Color(.06f, .09f, .12f, .9f);
        private static readonly Color ButtonColor =
            new Color(.2f, .36f, .44f, .98f);

        [SerializeField] private Canvas canvas;
        [SerializeField] private EventSystem eventSystem;

        private RectTransform uiRoot;
        private Button towerPauseButton;
        private Text towerPauseButtonText;
        private CanvasGroup selectionGroup;
        private GameObject fallbackCanvasObject;
        private GameObject fallbackEventSystemObject;
        private GrayboxDefenseSelectionKind3D selectedKind;
        private string selectedStableId;

        public Text SummaryText { get; private set; }
        public Text SelectionText { get; private set; }
        public RectTransform SummaryRect { get; private set; }
        public RectTransform SelectionRect { get; private set; }
        public bool IsSelectionVisible { get; private set; }
        public bool DetailsVisible => IsSelectionVisible;
        public bool WarningVisible { get; private set; }
        public string TowerPauseButtonLabel { get; private set; } =
            "暂停运行";
        public GrayboxDefenseRuntimeSnapshot3D LastSnapshot { get; private set; }
        public int RefreshCount { get; private set; }

        public event Action<string> TowerPauseRequested;

        public void Configure(
            Canvas configuredCanvas,
            EventSystem configuredEventSystem)
        {
            canvas = configuredCanvas ??
                throw new ArgumentNullException(nameof(configuredCanvas));
            eventSystem = configuredEventSystem ??
                throw new ArgumentNullException(nameof(configuredEventSystem));
            if (!transform.IsChildOf(canvas.transform))
                transform.SetParent(canvas.transform, false);
            if (uiRoot != null && uiRoot.parent != transform)
                uiRoot.SetParent(transform, false);
            EnsureUi();
        }

        public void Apply(
            GrayboxDefenseRuntimeSnapshot3D snapshot,
            GrayboxDefenseSelectionKind3D selectionKind,
            string stableId)
        {
            using (ApplyMarker.Auto())
            {
                LastSnapshot = snapshot;
                RefreshCount++;
                EnsureFallbackConfiguration();
                selectedKind = selectionKind;
                selectedStableId = stableId;
                WarningVisible = snapshot != null &&
                    snapshot.WavePhase == WavePhase.Warning;

                string summary = FormatSummary(snapshot);
                string details = FormatSelection(
                    snapshot,
                    selectionKind,
                    stableId,
                    out bool visible,
                    out bool towerSelected,
                    out bool towerPaused);
                IsSelectionVisible = visible;
                TowerPauseButtonLabel = towerPaused
                    ? "恢复运行"
                    : "暂停运行";

                if (SummaryText != null)
                    SummaryText.text = summary;
                if (SelectionText != null)
                    SelectionText.text = details;
                if (selectionGroup != null)
                {
                    selectionGroup.alpha = visible ? 1f : 0f;
                    selectionGroup.interactable = visible;
                    selectionGroup.blocksRaycasts = visible;
                }
                if (towerPauseButton != null)
                {
                    towerPauseButton.interactable = towerSelected;
                }
                if (towerPauseButtonText != null)
                    towerPauseButtonText.text = TowerPauseButtonLabel;
            }
        }

        protected virtual void OnDestroy()
        {
            if (towerPauseButton != null)
                towerPauseButton.onClick.RemoveListener(HandlePauseClicked);
        }

        private void EnsureFallbackConfiguration()
        {
            if (canvas == null)
            {
                fallbackCanvasObject = new GameObject(
                    "Defense.Hud.FallbackCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                fallbackCanvasObject.transform.SetParent(transform, false);
                canvas = fallbackCanvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(1600f, 900f);
            }
            if (eventSystem == null)
            {
                fallbackEventSystemObject = new GameObject(
                    "Defense.Hud.FallbackEventSystem",
                    typeof(EventSystem));
                fallbackEventSystemObject.transform.SetParent(transform, false);
                eventSystem = fallbackEventSystemObject
                    .GetComponent<EventSystem>();
            }
            EnsureUi();
        }

        private void EnsureUi()
        {
            if (uiRoot != null || canvas == null)
                return;

            Transform uiParent = fallbackCanvasObject != null &&
                                 canvas != null &&
                                 canvas.gameObject == fallbackCanvasObject
                ? canvas.transform
                : transform;
            Vector2 canvasSize = canvas.GetComponent<RectTransform>() != null
                ? canvas.GetComponent<RectTransform>().rect.size
                : new Vector2(1600f, 900f);
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
                canvasSize = new Vector2(1600f, 900f);
            uiRoot = CreateRect(
                uiParent,
                "Defense.Hud.Root",
                Vector2.one * .5f,
                Vector2.one * .5f,
                Vector2.one * .5f,
                Vector2.zero,
                canvasSize);

            SummaryRect = CreatePanel(
                uiRoot,
                "DefenseWaveWarning",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -70f),
                new Vector2(360f, 90f));
            SummaryText = CreateText(
                SummaryRect,
                "Defense.Summary.Text",
                new Vector2(12f, 8f),
                new Vector2(-24f, -16f),
                16,
                TextAnchor.MiddleLeft);

            SelectionRect = CreatePanel(
                uiRoot,
                "DefenseDetailsPanel",
                new Vector2(1f, .5f),
                new Vector2(1f, .5f),
                new Vector2(1f, .5f),
                new Vector2(-20f, 0f),
                new Vector2(310f, 300f));
            selectionGroup = SelectionRect.gameObject
                .AddComponent<CanvasGroup>();
            SelectionText = CreateText(
                SelectionRect,
                "Defense.Selection.Text",
                new Vector2(14f, 54f),
                new Vector2(-28f, -68f),
                16,
                TextAnchor.UpperLeft);
            towerPauseButton = CreateButton(
                SelectionRect,
                "DefenseDetails.TowerPauseButton",
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                new Vector2(0f, 12f),
                new Vector2(180f, 38f));
            towerPauseButtonText = CreateText(
                towerPauseButton.GetComponent<RectTransform>(),
                "Defense.Selection.Pause.Text",
                Vector2.zero,
                Vector2.zero,
                15,
                TextAnchor.MiddleCenter);
            towerPauseButtonText.rectTransform.anchorMin = Vector2.zero;
            towerPauseButtonText.rectTransform.anchorMax = Vector2.one;
            towerPauseButtonText.rectTransform.offsetMin = Vector2.zero;
            towerPauseButtonText.rectTransform.offsetMax = Vector2.zero;
            towerPauseButton.onClick.AddListener(HandlePauseClicked);

            SummaryText.text = FormatSummary(LastSnapshot);
            selectionGroup.alpha = 0f;
            selectionGroup.interactable = false;
            selectionGroup.blocksRaycasts = false;
        }

        private void HandlePauseClicked()
        {
            if (selectedKind == GrayboxDefenseSelectionKind3D.Tower &&
                !string.IsNullOrWhiteSpace(selectedStableId))
            {
                TowerPauseRequested?.Invoke(selectedStableId);
            }
        }

        private static string FormatSummary(
            GrayboxDefenseRuntimeSnapshot3D snapshot)
        {
            if (snapshot == null)
                return "防御 | 核心 --/-- | 波次 未开始 | 敌人 0";

            string wave;
            if (snapshot.IsCoreDestroyed)
            {
                wave = "城市核心失守";
            }
            else switch (snapshot.WavePhase)
            {
                case WavePhase.Warning:
                    wave = "预警 " + snapshot.WarningRemainingSeconds
                        .ToString("0.0", CultureInfo.InvariantCulture) + " 秒";
                    break;
                case WavePhase.Spawning:
                    wave = "敌袭生成中";
                    break;
                case WavePhase.Active:
                    wave = "敌袭进行中";
                    break;
                default:
                    wave = "安全";
                    break;
            }
            return "防御 | 核心 " + snapshot.CoreCurrentHealth + "/" +
                   snapshot.CoreMaximumHealth + " | " + wave +
                   " | 敌人 " + snapshot.AliveEnemyCount;
        }

        private static string FormatSelection(
            GrayboxDefenseRuntimeSnapshot3D snapshot,
            GrayboxDefenseSelectionKind3D kind,
            string stableId,
            out bool visible,
            out bool towerSelected,
            out bool towerPaused)
        {
            visible = false;
            towerSelected = false;
            towerPaused = false;
            if (snapshot == null || string.IsNullOrWhiteSpace(stableId))
                return string.Empty;

            if (kind == GrayboxDefenseSelectionKind3D.Tower)
            {
                GrayboxDefenseTowerSnapshot3D tower = snapshot.Towers
                    .FirstOrDefault(value => string.Equals(
                        value.StableId,
                        stableId,
                        StringComparison.Ordinal));
                if (tower == null)
                    return string.Empty;
                visible = true;
                towerSelected = true;
                towerPaused = tower.PlayerPaused;
                return "机枪塔\n生命 " +
                       BuildingCatalog.MachineGunTurret.MaximumHealth + "/" +
                       BuildingCatalog.MachineGunTurret.MaximumHealth +
                       "\n射程 " + tower.Range.ToString(
                           "0.#",
                           CultureInfo.InvariantCulture) + " 格" +
                       "\n弹药 " + tower.Ammo + "/" + tower.AmmoCapacity +
                       "\n物流 " + (tower.Connected ? "已连接" : "已断开") +
                       "\n状态 " + TowerStatusLabel(tower.Status) +
                       "\n目标 " +
                       (string.IsNullOrEmpty(tower.TargetId)
                           ? "无"
                           : tower.TargetId);
            }

            if (kind == GrayboxDefenseSelectionKind3D.Enemy)
            {
                GrayboxDefenseEnemySnapshot3D enemy = snapshot.Enemies
                    .FirstOrDefault(value => string.Equals(
                        value.StableId,
                        stableId,
                        StringComparison.Ordinal));
                if (enemy == null)
                    return string.Empty;
                visible = true;
                return "啃噬者\n生命 " + enemy.CurrentHealth + "/" +
                       EnemyCatalog.Gnawer.MaximumHealth +
                       "\n目标 " + enemy.TargetName +
                       "\n距离 " + enemy.DistanceToCore.ToString(
                           "0.0",
                           CultureInfo.InvariantCulture) + "格" +
                       "\n状态 " + (enemy.IsAttackingCore
                           ? "攻击城市核心"
                           : "接近城市核心");
            }
            return string.Empty;
        }

        private static string TowerStatusLabel(
            GrayboxDefenseTowerStatus3D status)
        {
            switch (status)
            {
                case GrayboxDefenseTowerStatus3D.Firing:
                    return "射击中";
                case GrayboxDefenseTowerStatus3D.MissingAmmunition:
                    return "缺少弹药";
                case GrayboxDefenseTowerStatus3D.OutOfLogistics:
                    return "不在物流范围";
                case GrayboxDefenseTowerStatus3D.PlayerPaused:
                    return "玩家暂停";
                case GrayboxDefenseTowerStatus3D.Unavailable:
                    return "建筑未运行";
                default:
                    return "等待目标";
            }
        }

        private static RectTransform CreatePanel(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rect = CreateRect(
                parent,
                objectName,
                anchorMin,
                anchorMax,
                pivot,
                anchoredPosition,
                size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            return rect;
        }

        private static Button CreateButton(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rect = CreateRect(
                parent,
                objectName,
                anchorMin,
                anchorMax,
                pivot,
                anchoredPosition,
                size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(
                parent,
                objectName,
                Vector2.zero,
                Vector2.one,
                Vector2.one * .5f,
                Vector2.zero,
                Vector2.zero);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var value = new GameObject(
                objectName,
                typeof(RectTransform));
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }
    }

}
