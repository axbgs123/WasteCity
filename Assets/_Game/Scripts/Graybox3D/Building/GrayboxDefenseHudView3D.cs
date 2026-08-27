using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;

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
        private Button buildingUpgradeButton;
        private Text buildingUpgradeButtonText;
        private Text buildingUpgradeFeedbackText;
        private CanvasGroup selectionGroup;
        private GameObject fallbackCanvasObject;
        private GameObject fallbackEventSystemObject;
        private GrayboxDefenseSelectionKind3D selectedKind;
        private string selectedStableId;
        private GrayboxDefenseSelectionSnapshot3D selectedDetails;
        private bool usesSelectionDetails;
        private bool selectedCanToggleTowerPause;
        private float requestedSpeed = 1f;
        private float effectiveSpeed = 1f;
        private bool hasAppliedProjection;
        private bool hasAppliedSpeed;

        public Text SummaryText { get; private set; }
        public Text SpeedText { get; private set; }
        public Text SelectionText { get; private set; }
        public RectTransform SummaryRect { get; private set; }
        public RectTransform SpeedRect { get; private set; }
        public RectTransform SelectionRect { get; private set; }
        public bool IsSelectionVisible { get; private set; }
        public bool DetailsVisible => IsSelectionVisible;
        public bool WarningVisible { get; private set; }
        public string TowerPauseButtonLabel { get; private set; } =
            "暂停运行";
        public GrayboxDefenseRuntimeSnapshot3D LastSnapshot { get; private set; }
        public int RefreshCount { get; private set; }

        public event Action<string> TowerPauseRequested;
        public event Action<string> BuildingUpgradeRequested;

        public bool IsBuildingUpgradeVisible { get; private set; }
        public bool CanUpgradeSelectedBuilding { get; private set; }
        public string BuildingUpgradeFeedback { get; private set; } =
            string.Empty;

        public void ApplyBuildingUpgradeCommand(
            bool visible,
            bool canUpgrade,
            string buttonLabel,
            string feedback)
        {
            EnsureFallbackConfiguration();
            IsBuildingUpgradeVisible = visible;
            CanUpgradeSelectedBuilding = visible && canUpgrade;
            BuildingUpgradeFeedback = feedback ?? string.Empty;
            buildingUpgradeButton.gameObject.SetActive(visible);
            buildingUpgradeButton.interactable = CanUpgradeSelectedBuilding;
            if (buildingUpgradeButton.targetGraphic != null)
                buildingUpgradeButton.targetGraphic.raycastTarget = visible;
            buildingUpgradeButtonText.text = string.IsNullOrWhiteSpace(
                buttonLabel) ? "升级选中建筑" : buttonLabel;
            buildingUpgradeFeedbackText.text = BuildingUpgradeFeedback;
            buildingUpgradeFeedbackText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(BuildingUpgradeFeedback));
        }

        public void ApplySpeed(
            float requestedSpeed,
            float effectiveSpeed)
        {
            float normalizedRequested =
                NormalizeDisplaySpeed(requestedSpeed);
            float normalizedEffective =
                NormalizeDisplaySpeed(effectiveSpeed);
            EnsureFallbackConfiguration();
            if (hasAppliedSpeed &&
                this.requestedSpeed == normalizedRequested &&
                this.effectiveSpeed == normalizedEffective)
            {
                return;
            }
            this.requestedSpeed = normalizedRequested;
            this.effectiveSpeed = normalizedEffective;
            hasAppliedSpeed = true;
            if (SpeedText != null)
            {
                SpeedText.text = FormatSpeed(
                    this.requestedSpeed,
                    this.effectiveSpeed);
            }
        }

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
            ApplyInternal(
                snapshot,
                selectionKind,
                stableId,
                details: null,
                useDetails: false);
        }

        public void Apply(
            GrayboxDefenseRuntimeSnapshot3D snapshot,
            GrayboxDefenseSelectionKind3D selectionKind,
            string stableId,
            GrayboxDefenseSelectionSnapshot3D details)
        {
            ApplyInternal(
                snapshot,
                selectionKind,
                stableId,
                details,
                useDetails: true);
        }

        private void ApplyInternal(
            GrayboxDefenseRuntimeSnapshot3D snapshot,
            GrayboxDefenseSelectionKind3D selectionKind,
            string stableId,
            GrayboxDefenseSelectionSnapshot3D details,
            bool useDetails)
        {
            using (ApplyMarker.Auto())
            {
                EnsureFallbackConfiguration();
                if (hasAppliedProjection &&
                    ReferenceEquals(LastSnapshot, snapshot) &&
                    selectedKind == selectionKind &&
                    usesSelectionDetails == useDetails &&
                    ReferenceEquals(selectedDetails, details) &&
                    string.Equals(
                        selectedStableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return;
                }
                LastSnapshot = snapshot;
                RefreshCount++;
                hasAppliedProjection = true;
                selectedKind = selectionKind;
                selectedStableId = stableId;
                selectedDetails = details;
                usesSelectionDetails = useDetails;
                WarningVisible = snapshot != null &&
                    snapshot.WavePhase == WavePhase.Warning;

                string summary = FormatSummary(snapshot);
                bool visible;
                bool towerSelected;
                bool towerPaused;
                string selectionText;
                if (useDetails)
                {
                    selectionText = FormatSelection(
                        details,
                        selectionKind,
                        stableId,
                        out visible,
                        out towerSelected,
                        out towerPaused);
                }
                else
                {
                    selectionText = FormatSelection(
                        snapshot,
                        selectionKind,
                        stableId,
                        out visible,
                        out towerSelected,
                        out towerPaused);
                }
                IsSelectionVisible = visible;
                selectedCanToggleTowerPause = towerSelected;
                TowerPauseButtonLabel = towerPaused
                    ? "恢复运行"
                    : "暂停运行";

                if (SummaryText != null)
                    SummaryText.text = summary;
                if (SelectionText != null)
                    SelectionText.text = selectionText;
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
            if (buildingUpgradeButton != null)
                buildingUpgradeButton.onClick.RemoveListener(
                    HandleBuildingUpgradeClicked);
            BuildingUpgradeRequested = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshFormalLayout();
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
                new Vector2(360f, 140f));
            SummaryText = CreateText(
                SummaryRect,
                "Defense.Summary.Text",
                new Vector2(12f, 8f),
                new Vector2(-24f, -16f),
                16,
                TextAnchor.MiddleLeft);

            SpeedRect = CreatePanel(
                uiRoot,
                "DefenseSpeedStatus",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(390f, -70f),
                new Vector2(230f, 46f));
            SpeedText = CreateText(
                SpeedRect,
                "Defense.Speed.Text",
                new Vector2(10f, 6f),
                new Vector2(-20f, -12f),
                15,
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
            buildingUpgradeButton = CreateButton(
                SelectionRect,
                "DefenseDetails.BuildingUpgradeButton",
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                new Vector2(0f, 56f),
                new Vector2(220f, 38f));
            buildingUpgradeButtonText = CreateText(
                buildingUpgradeButton.GetComponent<RectTransform>(),
                "Defense.Selection.Upgrade.Text",
                Vector2.zero,
                Vector2.zero,
                15,
                TextAnchor.MiddleCenter);
            buildingUpgradeButtonText.rectTransform.anchorMin = Vector2.zero;
            buildingUpgradeButtonText.rectTransform.anchorMax = Vector2.one;
            buildingUpgradeButtonText.rectTransform.offsetMin = Vector2.zero;
            buildingUpgradeButtonText.rectTransform.offsetMax = Vector2.zero;
            buildingUpgradeButton.onClick.AddListener(
                HandleBuildingUpgradeClicked);
            buildingUpgradeFeedbackText = CreateText(
                SelectionRect,
                "Defense.Selection.Upgrade.Feedback",
                new Vector2(14f, 96f),
                new Vector2(-14f, -150f),
                14,
                TextAnchor.LowerCenter);
            buildingUpgradeButton.gameObject.SetActive(false);
            buildingUpgradeButton.targetGraphic.raycastTarget = false;
            buildingUpgradeFeedbackText.gameObject.SetActive(false);

            SummaryText.text = FormatSummary(LastSnapshot);
            SpeedText.text = FormatSpeed(requestedSpeed, effectiveSpeed);
            selectionGroup.alpha = 0f;
            selectionGroup.interactable = false;
            selectionGroup.blocksRaycasts = false;
            RefreshFormalLayout();
        }

        private void RefreshFormalLayout()
        {
            if (canvas == null || uiRoot == null) return;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect == null
                ? FormalUiLayoutProfile3D.Standard.ReferenceResolution
                : canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
                canvasSize = FormalUiLayoutProfile3D.Standard
                    .ReferenceResolution;
            uiRoot.anchorMin = Vector2.one * .5f;
            uiRoot.anchorMax = Vector2.one * .5f;
            uiRoot.pivot = Vector2.one * .5f;
            uiRoot.anchoredPosition = Vector2.zero;
            uiRoot.sizeDelta = canvasSize;
            uiRoot.localScale = Vector3.one;

            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                new Rect(0f, 0f, canvasSize.x, canvasSize.y));
            FormalUiLayoutProfile3D profile =
                FormalUiLayoutProfile3D.Standard;
            Rect danger = layout.DangerAndCoreSlot;
            Rect summary = new Rect(
                danger.xMin,
                danger.yMax - 140f,
                danger.width,
                140f);
            Rect speed = new Rect(
                summary.xMin,
                summary.yMin - profile.SpaceSmall - 46f,
                Mathf.Min(230f, summary.width),
                46f);
            Rect drawer = layout.SelectionDrawerSlot;
            Rect selection = new Rect(
                drawer.xMax - Mathf.Min(310f, drawer.width),
                drawer.center.y - Mathf.Min(300f, drawer.height) * .5f,
                Mathf.Min(310f, drawer.width),
                Mathf.Min(300f, drawer.height));
            ApplyCanvasRect(SummaryRect, summary);
            ApplyCanvasRect(SpeedRect, speed);
            ApplyCanvasRect(SelectionRect, selection);
        }

        private static void ApplyCanvasRect(RectTransform target, Rect rect)
        {
            if (target == null) return;
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.zero;
            target.pivot = Vector2.zero;
            target.anchoredPosition = rect.position;
            target.sizeDelta = rect.size;
            target.localScale = Vector3.one;
        }

        private void HandlePauseClicked()
        {
            if (selectedKind == GrayboxDefenseSelectionKind3D.Tower &&
                selectedCanToggleTowerPause &&
                !string.IsNullOrWhiteSpace(selectedStableId))
            {
                TowerPauseRequested?.Invoke(selectedStableId);
            }
        }

        private void HandleBuildingUpgradeClicked()
        {
            if (IsBuildingUpgradeVisible && CanUpgradeSelectedBuilding &&
                !string.IsNullOrWhiteSpace(selectedStableId))
                BuildingUpgradeRequested?.Invoke(selectedStableId);
        }

        private static string FormatSummary(
            GrayboxDefenseRuntimeSnapshot3D snapshot)
        {
            if (snapshot == null)
                return "防御 | 第 0/10 波\n" +
                       "阶段 未开始 | 倒计时 --\n" +
                       "入口 无 | 组成 无\n" +
                       "已生成 0/0 | 存活 0\n" +
                       "核心 --/--";

            string countdown = snapshot.CampaignPhase ==
                    SingleCityDefenseCampaignPhase.Warning
                ? snapshot.WarningRemainingSeconds.ToString(
                      "0.0",
                      CultureInfo.InvariantCulture) + " 秒"
                : "--";
            string core = snapshot.CoreCurrentHealth + "/" +
                          snapshot.CoreMaximumHealth;
            if (snapshot.IsCoreDestroyed)
                core += "（城市核心失守）";
            return "防御 | 第 " + snapshot.CurrentWaveNumber + "/" +
                   snapshot.TotalWaveCount + " 波\n" +
                   "阶段 " + PhaseLabel(snapshot.CampaignPhase) +
                   " | 倒计时 " + countdown + "\n" +
                   "入口 " + FormatDirections(snapshot.SpawnDirections) +
                   " | 组成 " +
                   FormatComposition(snapshot.WaveComposition) + "\n" +
                   "已生成 " + snapshot.SpawnedEnemyCount + "/" +
                   snapshot.PlannedEnemyCount + " | 存活敌人 " +
                   snapshot.AliveEnemyCount + "\n" +
                   "核心 " + core;
        }

        private static string PhaseLabel(
            SingleCityDefenseCampaignPhase phase)
        {
            switch (phase)
            {
                case SingleCityDefenseCampaignPhase.Warning:
                    return "预警";
                case SingleCityDefenseCampaignPhase.SpawningAndCombat:
                    return "生成与战斗";
                case SingleCityDefenseCampaignPhase.CombatCleanup:
                    return "战斗清理";
                case SingleCityDefenseCampaignPhase.Victory:
                    return "胜利";
                case SingleCityDefenseCampaignPhase.Defeat:
                    return "失败";
                default:
                    return "未开始";
            }
        }

        private static string FormatComposition(
            System.Collections.Generic.IReadOnlyList<WaveEntry> entries)
        {
            if (entries == null || entries.Count == 0) return "无";

            var result = new StringBuilder(48);
            for (var index = 0; index < entries.Count; index++)
            {
                if (index > 0) result.Append(" / ");
                WaveEntry entry = entries[index];
                result.Append(EnemyName(entry.Archetype));
                result.Append('×');
                result.Append(entry.Count);
            }
            return result.ToString();
        }

        private static string FormatDirections(
            System.Collections.Generic.IReadOnlyList<
                CampaignSpawnDirection> directions)
        {
            if (directions == null || directions.Count == 0) return "无";
            var result = new StringBuilder(16);
            for (var index = 0; index < directions.Count; index++)
            {
                if (index > 0) result.Append(" / ");
                switch (directions[index])
                {
                    case CampaignSpawnDirection.East:
                        result.Append('东');
                        break;
                    case CampaignSpawnDirection.North:
                        result.Append('北');
                        break;
                    case CampaignSpawnDirection.South:
                        result.Append('南');
                        break;
                    case CampaignSpawnDirection.West:
                        result.Append('西');
                        break;
                }
            }
            return result.Length == 0 ? "无" : result.ToString();
        }

        private static string EnemyName(EnemyArchetype archetype)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
                if (EnemyCatalog.All[index].Archetype == archetype)
                    return EnemyCatalog.All[index].Name;
            return archetype.ToString();
        }

        private static string FormatSpeed(
            float requested,
            float effective)
        {
            return "速度 | 请求 " + requested.ToString(
                       "0.#",
                       CultureInfo.InvariantCulture) +
                   "× | 有效 " + effective.ToString(
                       "0.#",
                       CultureInfo.InvariantCulture) + "×";
        }

        private static float NormalizeDisplaySpeed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Mathf.Clamp(value, 0f, 2f);
        }

        private static string FormatSelection(
            GrayboxDefenseSelectionSnapshot3D details,
            GrayboxDefenseSelectionKind3D kind,
            string stableId,
            out bool visible,
            out bool towerSelected,
            out bool towerPaused)
        {
            visible = false;
            towerSelected = false;
            towerPaused = false;
            if (details == null ||
                kind == GrayboxDefenseSelectionKind3D.None ||
                details.Kind != kind ||
                string.IsNullOrWhiteSpace(stableId) ||
                !string.Equals(
                    details.StableId,
                    stableId,
                    StringComparison.Ordinal))
            {
                return string.Empty;
            }

            visible = true;
            towerSelected = kind == GrayboxDefenseSelectionKind3D.Tower &&
                details.CanToggleTowerPause;
            towerPaused = details.Tower != null &&
                details.Tower.PlayerPaused;

            var result = new StringBuilder(160);
            result.Append(string.IsNullOrWhiteSpace(details.DisplayName)
                ? "未知目标"
                : details.DisplayName);
            result.Append("\n生命 ");
            result.Append(kind == GrayboxDefenseSelectionKind3D.Ruin
                ? 0
                : details.CurrentHealth);
            result.Append('/');
            result.Append(details.MaximumHealth);
            result.Append("\n状态 ");
            result.Append(string.IsNullOrWhiteSpace(details.StatusText)
                ? "未知"
                : details.StatusText);

            if (kind == GrayboxDefenseSelectionKind3D.Ruin)
            {
                result.Append("\n损失 ");
                if (details.LostResources == null ||
                    details.LostResources.Count == 0)
                {
                    result.Append("无物资损失或明细不可用");
                }
                else
                {
                    for (var index = 0;
                         index < details.LostResources.Count;
                         index++)
                    {
                        if (index > 0) result.Append(" / ");
                        ResourceAmount amount = details.LostResources[index];
                        result.Append(ResourceName(amount.ResourceId));
                        result.Append('×');
                        result.Append(amount.Amount);
                    }
                }
                result.Append("；内部库存与预留已清空");
                return result.ToString();
            }

            if (kind == GrayboxDefenseSelectionKind3D.Building)
            {
                AppendProductionDetails(
                    result,
                    details.Production);
                return result.ToString();
            }

            if (kind == GrayboxDefenseSelectionKind3D.Tower)
            {
                GrayboxDefenseTowerSnapshot3D tower = details.Tower;
                AppendTowerCombatDetails(
                    result,
                    details.DefinitionId,
                    tower);
                if (tower != null)
                {
                    DefenseTowerDefinition definition =
                        DefenseTowerCatalog.For(details.DefinitionId);
                    result.Append("\n射程 ");
                    result.Append(tower.Range.ToString(
                        "0.#",
                        CultureInfo.InvariantCulture));
                    result.Append(" 格\n本地 ");
                    result.Append(ResourceName(
                        definition?.ConsumableId ?? ResourceIds.Ammunition));
                    result.Append(' ');
                    result.Append(tower.Ammo);
                    result.Append('/');
                    result.Append(tower.AmmoCapacity);
                    result.Append("\n物流 ");
                    result.Append(tower.Connected ? "已连接" : "已断开");
                }
                result.Append("\n目标 ");
                result.Append(string.IsNullOrWhiteSpace(
                        details.TargetDisplayName)
                    ? string.IsNullOrWhiteSpace(details.TargetStableId)
                    ? "无"
                    : details.TargetStableId
                    : details.TargetDisplayName);
                return result.ToString();
            }

            if (kind == GrayboxDefenseSelectionKind3D.Enemy)
            {
                AppendEnemyCombatDetails(result, details.DefinitionId);
                result.Append("\n当前目标 ");
                result.Append(string.IsNullOrWhiteSpace(
                        details.TargetDisplayName)
                    ? string.IsNullOrWhiteSpace(details.TargetStableId)
                    ? "无"
                    : details.TargetStableId
                    : details.TargetDisplayName);
                if (details.Enemy != null)
                {
                    result.Append("\n距目标 ");
                    result.Append(details.Enemy.DistanceToTarget.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture));
                    result.Append(" 格");
                }
                return result.ToString();
            }

            visible = false;
            return string.Empty;
        }

        private static void AppendProductionDetails(
            StringBuilder result,
            ProductionBuildingObservability production)
        {
            if (production == null) return;
            result.Append("\n配方 ");
            result.Append(ResourceRecipeCatalog.DisplayName(
                production.ProductionDefinitionId));
            result.Append('：');
            AppendRecipeChannels(result, production.Inputs, "无");
            result.Append(" → ");
            AppendRecipeChannels(result, production.Outputs, "无");
            result.Append('（');
            result.Append(production.DurationSeconds.ToString(
                "0.#",
                CultureInfo.InvariantCulture));
            result.Append("秒）");
            result.Append("\n内部输入 ");
            AppendInventoryChannels(result, production.Inputs);
            result.Append("\n内部输出 ");
            AppendInventoryChannels(result, production.Outputs);
            result.Append("\n物流 ");
            result.Append(production.IsLogisticsConnected
                ? "已连接"
                : "已断开");
            result.Append("\n停工原因 ");
            result.Append(GrayboxDefenseSelectionProjection3D
                .ProductionStopReasonText(
                    production.IsPlayerPaused
                        ? ProductionStopReason.PlayerPaused
                        : production.StopReason));
        }

        private static void AppendTowerCombatDetails(
            StringBuilder result,
            string buildingDefinitionId,
            GrayboxDefenseTowerSnapshot3D tower)
        {
            DefenseTowerDefinition definition =
                DefenseTowerCatalog.For(buildingDefinitionId);
            if (definition == null) return;
            result.Append("\n伤害 ");
            result.Append(DamageTypeLabel(definition.DamageType));
            result.Append(" | DPS ");
            result.Append(definition.DamagePerSecond.ToString(
                "0.#",
                CultureInfo.InvariantCulture));
            result.Append("\n耗材 ");
            result.Append(ResourceName(definition.ConsumableId));
            result.Append(" | 每 ");
            result.Append(definition.SecondsPerConsumable.ToString(
                "0.#",
                CultureInfo.InvariantCulture));
            result.Append(" 秒 1");
            if (tower == null) return;
            result.Append("\n预计续航 ");
            result.Append((tower.Ammo * definition.SecondsPerConsumable +
                    tower.ActiveConsumableSeconds)
                .ToString("0.#", CultureInfo.InvariantCulture));
            result.Append(" 秒");
        }

        private static void AppendEnemyCombatDetails(
            StringBuilder result,
            string enemyDefinitionId)
        {
            EnemyDefinition definition = null;
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                if (!string.Equals(
                        EnemyCatalog.All[index].Id.Value,
                        enemyDefinitionId,
                        StringComparison.Ordinal))
                    continue;
                definition = EnemyCatalog.All[index];
                break;
            }
            if (definition == null) return;
            result.Append("\n移速 ");
            result.Append(definition.MoveSpeed.ToString(
                "0.#",
                CultureInfo.InvariantCulture));
            result.Append(" | DPS ");
            result.Append(definition.DamagePerSecond.ToString(
                "0.#",
                CultureInfo.InvariantCulture));
            result.Append(" | 射程 ");
            result.Append(definition.AttackRange.ToString(
                "0.#",
                CultureInfo.InvariantCulture));
            result.Append(" 格\n护甲 ");
            result.Append(ArmorLabel(definition.Armor));
        }

        private static void AppendRecipeChannels(
            StringBuilder result,
            System.Collections.Generic.IReadOnlyList<
                ProductionResourceObservability> channels,
            string emptyLabel)
        {
            if (channels == null || channels.Count == 0)
            {
                result.Append(emptyLabel);
                return;
            }
            for (var index = 0; index < channels.Count; index++)
            {
                if (index > 0) result.Append(" + ");
                ProductionResourceObservability channel = channels[index];
                result.Append(ResourceName(channel.ResourceId));
                result.Append('×');
                result.Append(channel.AmountPerCycle);
            }
        }

        private static void AppendInventoryChannels(
            StringBuilder result,
            System.Collections.Generic.IReadOnlyList<
                ProductionResourceObservability> channels)
        {
            if (channels == null || channels.Count == 0)
            {
                result.Append('无');
                return;
            }
            for (var index = 0; index < channels.Count; index++)
            {
                if (index > 0) result.Append(" / ");
                ProductionResourceObservability channel = channels[index];
                result.Append(ResourceName(channel.ResourceId));
                result.Append(' ');
                result.Append(channel.CurrentAmount);
                result.Append('/');
                result.Append(channel.Capacity);
            }
        }

        private static string ResourceName(string resourceId)
        {
            return ResourceDefinitionCatalog.TryGet(
                resourceId,
                out ResourceDefinition definition)
                    ? definition.ChineseName
                    : string.IsNullOrWhiteSpace(resourceId)
                        ? "无"
                        : resourceId;
        }

        private static string DamageTypeLabel(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical:
                    return "物理";
                case DamageType.Energy:
                    return "能量";
                case DamageType.Psionic:
                    return "灵能";
                case DamageType.Biological:
                    return "生物";
                case DamageType.TrueEssence:
                    return "真元";
                default:
                    return damageType.ToString();
            }
        }

        private static string ArmorLabel(ArmorType armor)
        {
            switch (armor)
            {
                case ArmorType.Light:
                    return "轻型";
                case ArmorType.Heavy:
                    return "重型";
                case ArmorType.PsionicShield:
                    return "灵能护盾";
                case ArmorType.BiologicalShell:
                    return "生物甲壳";
                case ArmorType.SpiritualBarrier:
                    return "灵力屏障";
                default:
                    return armor.ToString();
            }
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
                return EnemyName(enemy.EnemyDefinitionId) +
                       "\n生命 " + enemy.CurrentHealth + "/" +
                       enemy.MaximumHealth +
                       "\n目标 " + enemy.TargetName +
                       "\n距核心 " + enemy.DistanceToCore.ToString(
                           "0.0",
                           CultureInfo.InvariantCulture) + " 格" +
                       "\n状态 " + (enemy.IsAttackingCore
                           ? "攻击城市核心"
                           : "接近城市核心");
            }
            return string.Empty;
        }

        private static string EnemyName(string definitionId)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
                if (string.Equals(
                        EnemyCatalog.All[index].Id.Value,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    return EnemyCatalog.All[index].Name;
                }
            return string.IsNullOrWhiteSpace(definitionId)
                ? "未知敌人"
                : definitionId;
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
