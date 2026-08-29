using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Graybox3D.Usability
{
    public sealed class GrayboxSystemMenuView3D : MonoBehaviour
    {
        private static readonly string[] VisibleControlIds =
        {
            "Start.Continue",
            "Start.NewGame",
            "Start.NewGameConfirm",
            "Start.NewGameCancel",
            "FormalSave.Feedback",
            "FormalSave.CheckpointWarning",
            "Speed.Pause",
            "Speed.1x",
            "Speed.2x",
            "Main.Continue",
            "Main.Settings",
            "Main.Quit",
            "Settings.Resolution",
            "Settings.WindowMode",
            "Settings.Apply",
            "Settings.Cancel",
            "Settings.Defaults",
            "Settings.OperationGuide",
            "Guide.Back",
            "Exit.SaveAndQuit",
            "Exit.QuitWithoutSaving",
            "Exit.Cancel"
        };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly string[] AcceptanceControlIds =
        {
            "Start.AcceptanceConsole",
            "Acceptance.Continue",
            "Acceptance.NewGame",
            "Acceptance.Back"
        };
#endif

        [SerializeField] private Canvas canvas;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private GrayboxSystemMenuController3D controller;

        private RectTransform uiRoot;
        private RectTransform speedControlsRoot;
        private Image blocker;
        private Text pausedTitle;
        private RectTransform startPage;
        private RectTransform newGameConfirmPage;
        private RectTransform mainPage;
        private RectTransform settingsPage;
        private RectTransform operationGuidePage;
        private RectTransform exitConfirmPage;
        private Text startTitle;
        private Button startContinueButton;
        private Button startNewGameButton;
        private Button startNewGameCancelButton;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private RectTransform acceptancePage;
        private Button startAcceptanceButton;
        private Button acceptanceContinueButton;
        private Button acceptanceNewGameButton;
        private Button acceptanceBackButton;
        private bool isAcceptancePageOpen;
#endif
        private Button continueButton;
        private Dropdown resolutionDropdown;
        private Dropdown windowModeDropdown;
        private Button guideBackButton;
        private Button exitWithoutSavingButton;
        private Button exitCancelButton;
        private Text formalSaveFeedback;
        private RectTransform checkpointWarningRoot;
        private Text checkpointWarning;
        private Button speedPauseButton;
        private Button speedOneButton;
        private Button speedTwoButton;
        private bool isOpen;
        private bool isStartPageOpen;
        private bool isNewGameConfirmationOpen;
        [SerializeField]
        private GrayboxFormalSaveEntryController3D formalSaveEntry;

        public bool IsPointerBlockerActive =>
            (isOpen || isStartPageOpen) &&
            blocker != null &&
            blocker.raycastTarget &&
            blocker.gameObject.activeInHierarchy;

        public bool HasMenuFocus
        {
            get
            {
                GameObject selected = eventSystem == null
                    ? null
                    : eventSystem.currentSelectedGameObject;
                return (isOpen || isStartPageOpen) &&
                    uiRoot != null &&
                    selected != null &&
                    selected.transform.IsChildOf(uiRoot);
            }
        }

        public GrayboxSystemMenuPage3D VisiblePage { get; private set; } =
            GrayboxSystemMenuPage3D.Main;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool IsAcceptancePageOpen => isAcceptancePageOpen;
#endif

        public static IReadOnlyList<string> ResolveVisibleControlIds(
            bool developmentBuild)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (developmentBuild)
            {
                var combined = new string[
                    VisibleControlIds.Length + AcceptanceControlIds.Length];
                Array.Copy(
                    VisibleControlIds,
                    0,
                    combined,
                    0,
                    VisibleControlIds.Length);
                Array.Copy(
                    AcceptanceControlIds,
                    0,
                    combined,
                    VisibleControlIds.Length,
                    AcceptanceControlIds.Length);
                return Array.AsReadOnly(combined);
            }
#endif
            return Array.AsReadOnly((string[])VisibleControlIds.Clone());
        }

        public void Configure(
            Canvas canvas,
            EventSystem eventSystem,
            GrayboxSystemMenuController3D controller)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));
            if (eventSystem == null)
                throw new ArgumentNullException(nameof(eventSystem));
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));

            bool canvasChanged = this.canvas != canvas;
            this.canvas = canvas;
            this.eventSystem = eventSystem;
            this.controller = controller;
            if (canvasChanged && uiRoot != null)
                DestroyUi();
            TryBuildUi();
            controller.RefreshView();
        }

        public void SetController(GrayboxSystemMenuController3D controller)
        {
            this.controller = controller;
        }

        public void ConfigureFormalSaveEntry(
            GrayboxFormalSaveEntryController3D formalSaveEntry)
        {
            this.formalSaveEntry = formalSaveEntry;
            TryBuildUi();
        }

        public void RenderStartPage(
            bool open,
            bool canContinue,
            bool newGameConfirmationOpen,
            string feedbackMessage)
        {
            TryBuildUi();
            isStartPageOpen = open;
            isNewGameConfirmationOpen =
                open && newGameConfirmationOpen;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!open) isAcceptancePageOpen = false;
#endif
            if (uiRoot == null) return;

            startPage.gameObject.SetActive(
                open && !isNewGameConfirmationOpen);
            newGameConfirmPage.gameObject.SetActive(
                open && isNewGameConfirmationOpen);
            startContinueButton.interactable = canContinue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (acceptanceContinueButton != null)
                acceptanceContinueButton.interactable = canContinue;
            ApplyStartAcceptanceVisibility();
#endif
            SetFormalSaveFeedback(feedbackMessage);
            UpdateRootVisibility();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshStartPageGraphics();
#endif
            if (open)
                FocusStartPage();
            else if (!isOpen && eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void RenderAcceptancePage(bool open, string feedbackMessage)
        {
            TryBuildUi();
            isAcceptancePageOpen = open && isStartPageOpen;
            if (uiRoot == null) return;
            if (isAcceptancePageOpen)
            {
                uiRoot.SetAsLastSibling();
            }
            startPage.gameObject.SetActive(
                isStartPageOpen && !isNewGameConfirmationOpen);
            ApplyStartAcceptanceVisibility();
            RefreshStartPageGraphics();
            newGameConfirmPage.gameObject.SetActive(
                isStartPageOpen && isNewGameConfirmationOpen);
            SetFormalSaveFeedback(feedbackMessage);
            UpdateRootVisibility();
            if (eventSystem != null)
            {
                GameObject target = isAcceptancePageOpen
                    ? acceptanceContinueButton != null &&
                      acceptanceContinueButton.interactable
                        ? acceptanceContinueButton.gameObject
                        : acceptanceBackButton?.gameObject
                    : startContinueButton.interactable
                        ? startContinueButton.gameObject
                        : startNewGameButton.gameObject;
                eventSystem.SetSelectedGameObject(target);
            }
        }
#endif

        public void SetFormalSaveFeedback(string message)
        {
            if (formalSaveFeedback == null) return;
            formalSaveFeedback.text = message ?? string.Empty;
            formalSaveFeedback.gameObject.SetActive(
                (isOpen || isStartPageOpen) &&
                !string.IsNullOrWhiteSpace(formalSaveFeedback.text));
        }

        public void SetCheckpointWarning(string message)
        {
            TryBuildUi();
            if (checkpointWarning == null || checkpointWarningRoot == null)
                return;
            checkpointWarning.text = message ?? string.Empty;
            checkpointWarningRoot.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(checkpointWarning.text));
        }

        public void Render(
            bool open,
            GrayboxSystemMenuPage3D page,
            GrayboxDisplaySettingsModel3D settings)
        {
            TryBuildUi();
            isOpen = open;
            VisiblePage = page;
            if (uiRoot == null) return;

            UpdateRootVisibility();
            pausedTitle.gameObject.SetActive(open);
            mainPage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.Main);
            settingsPage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.Settings);
            operationGuidePage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.OperationGuide);
            exitConfirmPage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.ExitConfirm);
            exitWithoutSavingButton.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.ExitConfirm &&
                controller.CanExitWithoutSaving);
            exitWithoutSavingButton.interactable =
                controller.CanExitWithoutSaving;

            if (!open && !isStartPageOpen)
            {
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(null);
                return;
            }

            if (settings != null)
                SyncSettings(settings);
            if (open)
                FocusPage(page);
        }

        private void Awake()
        {
            TryBuildUi();
        }

        private void OnEnable()
        {
            TryBuildUi();
            controller?.RefreshView();
            formalSaveEntry?.RefreshView();
        }

        private void OnDisable()
        {
            isOpen = false;
            isStartPageOpen = false;
            isNewGameConfirmationOpen = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            isAcceptancePageOpen = false;
#endif
            if (uiRoot != null)
                uiRoot.gameObject.SetActive(false);
            if (speedControlsRoot != null)
                speedControlsRoot.gameObject.SetActive(false);
            if (checkpointWarningRoot != null)
                checkpointWarningRoot.gameObject.SetActive(false);
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshFormalLayout();
        }

        private void OnDestroy()
        {
            DestroyUi();
            canvas = null;
            eventSystem = null;
            controller = null;
            formalSaveEntry = null;
        }

        private void TryBuildUi()
        {
            if (uiRoot != null || canvas == null || controller == null)
                return;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            RetireStaleRoots();
            BuildUi();
            uiRoot.gameObject.SetActive(false);
        }

        private void BuildUi()
        {
            speedControlsRoot = CreateRect(
                canvas.transform,
                "GrayboxFormalSpeedControls.Root");
            speedControlsRoot.sizeDelta = new Vector2(286f, 44f);
            UpdateSpeedControlsLayout();
            speedControlsRoot.SetAsFirstSibling();
            var speedCanvas =
                speedControlsRoot.gameObject.AddComponent<Canvas>();
            speedCanvas.overrideSorting = true;
            speedCanvas.sortingOrder = -1;
            speedControlsRoot.gameObject.AddComponent<GraphicRaycaster>();
            var speedBackground =
                speedControlsRoot.gameObject.AddComponent<Image>();
            speedBackground.color = new Color(.05f, .07f, .09f, .92f);
            speedBackground.raycastTarget = false;
            var speedLayout =
                speedControlsRoot.gameObject
                    .AddComponent<HorizontalLayoutGroup>();
            speedLayout.padding = new RectOffset(6, 6, 4, 4);
            speedLayout.spacing = 6f;
            speedLayout.childAlignment = TextAnchor.MiddleCenter;
            speedLayout.childForceExpandWidth = true;
            speedLayout.childForceExpandHeight = true;
            speedPauseButton = CreateButton(
                speedControlsRoot,
                "Speed.Pause",
                "暂停",
                () => controller?.ToggleTacticalPause());
            speedOneButton = CreateButton(
                speedControlsRoot,
                "Speed.1x",
                "1×",
                () => controller?.RequestSpeed(1));
            speedTwoButton = CreateButton(
                speedControlsRoot,
                "Speed.2x",
                "2×",
                () => controller?.RequestSpeed(2));

            uiRoot = CreateRect(
                canvas.transform,
                "GrayboxSystemMenuUi.Root");
            Stretch(uiRoot);

            checkpointWarningRoot = CreateRect(
                canvas.transform,
                "GrayboxFormalSaveCheckpointWarning.Root");
            PlaceFixed(
                checkpointWarningRoot,
                new Vector2(0f, -350f),
                new Vector2(760f, 48f));
            var checkpointBackground =
                checkpointWarningRoot.gameObject.AddComponent<Image>();
            checkpointBackground.color = new Color(.35f, .08f, .05f, .92f);
            checkpointBackground.raycastTarget = false;
            checkpointWarning = CreateLabel(
                checkpointWarningRoot,
                "FormalSave.CheckpointWarning",
                string.Empty);
            SetReadableFontSize(checkpointWarning, 17f);
            checkpointWarningRoot.gameObject.SetActive(false);

            RectTransform blockerRect = CreateRect(
                uiRoot,
                "Modal.Blocker");
            Stretch(blockerRect);
            blocker = blockerRect.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, .78f);
            blocker.raycastTarget = true;

            pausedTitle = CreateLabel(
                uiRoot,
                "Paused.Title",
                "游戏已暂停");
            SetReadableFontSize(pausedTitle, 28f);
            PlaceFixed(
                (RectTransform)pausedTitle.transform,
                new Vector2(0f, 260f),
                new Vector2(500f, 48f));
            pausedTitle.gameObject.SetActive(false);

            startPage = CreatePage(uiRoot, "Page.Start");
            startTitle = CreateLabel(
                startPage,
                "Start.Title",
                "废土移动城");
            SetReadableFontSize(startTitle, 26f);
            SetLayout((RectTransform)startTitle.transform, 0f, 48f, 1f);
            startContinueButton = CreateButton(
                startPage,
                "Start.Continue",
                "继续",
                () => formalSaveEntry?.RequestContinue());
            startNewGameButton = CreateButton(
                startPage,
                "Start.NewGame",
                "新游戏",
                () => formalSaveEntry?.RequestNewGame());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            startAcceptanceButton = CreateButton(
                startPage,
                "Start.AcceptanceConsole",
                "验收管理台",
                () => RenderAcceptancePage(
                    true,
                    "验收工具仅用于 Editor / Development"));

            acceptancePage = CreateRect(startPage, "Page.Acceptance");
            SetLayout(acceptancePage, 0f, 92f, 1f);
            var acceptanceLayout = acceptancePage.gameObject
                .AddComponent<VerticalLayoutGroup>();
            acceptanceLayout.spacing = 4f;
            acceptanceLayout.childAlignment = TextAnchor.MiddleCenter;
            acceptanceLayout.childForceExpandWidth = true;
            acceptanceLayout.childForceExpandHeight = false;
            Text acceptanceTitle = CreateLabel(
                acceptancePage,
                "Acceptance.Title",
                "开发验收管理台");
            SetReadableFontSize(acceptanceTitle, 24f);
            Text acceptanceWarning = CreateLabel(
                acceptancePage,
                "Acceptance.Warning",
                "继续或新建仍使用正式存档入口；进入游戏后才打开开发修改器。");
            SetReadableFontSize(acceptanceWarning, 17f);
            SetLayout((RectTransform)acceptanceTitle.transform, 0f, 34f, 1f);
            SetLayout((RectTransform)acceptanceWarning.transform, 0f, 48f, 1f);
            acceptanceContinueButton = CreateButton(
                startPage,
                "Acceptance.Continue",
                "继续并打开修改器",
                () => formalSaveEntry?.RequestAcceptanceContinue());
            acceptanceNewGameButton = CreateButton(
                startPage,
                "Acceptance.NewGame",
                "新游戏并打开修改器",
                () => formalSaveEntry?.RequestAcceptanceNewGame());
            acceptanceBackButton = CreateButton(
                startPage,
                "Acceptance.Back",
                "返回",
                () => formalSaveEntry?.RequestAcceptanceBack());
            ApplyStartAcceptanceVisibility();
#endif

            newGameConfirmPage = CreatePage(
                uiRoot,
                "Page.StartNewGameConfirm");
            Text newGameWarning = CreateLabel(
                newGameConfirmPage,
                "Start.NewGameWarning",
                "开始新的 3D 游戏将覆盖当前进度。\n" +
                "旧版 2D 存档会先安全归档。是否继续？");
            SetReadableFontSize(newGameWarning, 18f);
            SetLayout(
                (RectTransform)newGameWarning.transform,
                0f,
                100f,
                1f);
            CreateButton(
                newGameConfirmPage,
                "Start.NewGameConfirm",
                "确认开始新游戏",
                () => formalSaveEntry?.ConfirmNewGame());
            startNewGameCancelButton = CreateButton(
                newGameConfirmPage,
                "Start.NewGameCancel",
                "取消",
                () => formalSaveEntry?.CancelNewGame());

            mainPage = CreatePage(uiRoot, "Page.Main");
            continueButton = CreateButton(
                mainPage,
                "Main.Continue",
                "继续",
                () => controller?.Continue());
            CreateButton(
                mainPage,
                "Main.Settings",
                "设置",
                () => controller?.OpenSettings());
            CreateButton(
                mainPage,
                "Main.Quit",
                "退出",
                () => controller?.OpenExitConfirmation());

            settingsPage = CreatePage(uiRoot, "Page.Settings");
            CreateLabel(settingsPage, "Settings.Title", "显示设置");
            resolutionDropdown = CreateDropdown(
                settingsPage,
                "Settings.Resolution");
            resolutionDropdown.onValueChanged.AddListener(
                index => controller?.StageResolution(index));
            windowModeDropdown = CreateDropdown(
                settingsPage,
                "Settings.WindowMode");
            windowModeDropdown.AddOptions(new List<string>
            {
                "Windowed",
                "FullScreenWindow"
            });
            windowModeDropdown.onValueChanged.AddListener(
                index => controller?.StageWindowMode(
                    (GrayboxWindowMode3D)index));
            CreateButton(
                settingsPage,
                "Settings.Apply",
                "应用",
                () => controller?.ApplySettings());
            CreateButton(
                settingsPage,
                "Settings.Cancel",
                "取消",
                () => controller?.CancelSettings());
            CreateButton(
                settingsPage,
                "Settings.Defaults",
                "恢复默认",
                () => controller?.RestoreDefaultSettings());
            CreateButton(
                settingsPage,
                "Settings.OperationGuide",
                "操作说明",
                () => controller?.OpenOperationGuide());

            operationGuidePage = CreatePage(
                uiRoot,
                "Page.OperationGuide");
            Text guide = CreateLabel(
                operationGuidePage,
                "Guide.Text",
                "B：打开或关闭建造目录\n" +
                "F：切换城市展开或移动\n" +
                "R：旋转当前建筑\n" +
                "Delete：取消选中的施工\n" +
                "right-click：设置移动目的地\n" +
                "WASD：移动当前控制对象\n" +
                "mouse：指向、选择与拖动镜头\n" +
                "Home：镜头返回当前控制对象\n" +
                "Esc：返回、取消或打开系统菜单");
            guide.alignment = TextAnchor.MiddleLeft;
            SetLayout((RectTransform)guide.transform, 0f, 310f, 1f);
            guideBackButton = CreateButton(
                operationGuidePage,
                "Guide.Back",
                "返回",
                () => controller?.BackFromOperationGuide());

            exitConfirmPage = CreatePage(uiRoot, "Page.ExitConfirm");
            Text warning = CreateLabel(
                exitConfirmPage,
                "Exit.Warning",
                "保存并退出会写入当前完整 3D 进度。\n" +
                "保存失败时游戏会继续运行。");
            SetReadableFontSize(warning, 18f);
            SetLayout((RectTransform)warning.transform, 0f, 100f, 1f);
            CreateButton(
                exitConfirmPage,
                "Exit.SaveAndQuit",
                "保存并退出",
                () => controller?.ConfirmExit());
            exitWithoutSavingButton = CreateButton(
                exitConfirmPage,
                "Exit.QuitWithoutSaving",
                "不保存退出",
                () => controller?.ConfirmExitWithoutSaving());
            exitCancelButton = CreateButton(
                exitConfirmPage,
                "Exit.Cancel",
                "取消",
                () => controller?.CancelExit());

            formalSaveFeedback = CreateLabel(
                uiRoot,
                "FormalSave.Feedback",
                string.Empty);
            SetReadableFontSize(formalSaveFeedback, 17f);
            PlaceFixed(
                (RectTransform)formalSaveFeedback.transform,
                new Vector2(0f, -300f),
                new Vector2(760f, 54f));
            formalSaveFeedback.gameObject.SetActive(false);

            startPage.gameObject.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ApplyStartAcceptanceVisibility();
#endif
            newGameConfirmPage.gameObject.SetActive(false);
            speedControlsRoot.gameObject.SetActive(true);
            RefreshFormalLayout();
        }

        private void SyncSettings(GrayboxDisplaySettingsModel3D settings)
        {
            var labels = new List<string>(
                settings.AvailableResolutions.Count);
            var selectedResolution = 0;
            for (var index = 0;
                 index < settings.AvailableResolutions.Count;
                 index++)
            {
                GrayboxDisplayResolution3D resolution =
                    settings.AvailableResolutions[index];
                labels.Add(resolution.ToString());
                if (resolution.Width == settings.Staged.Width &&
                    resolution.Height == settings.Staged.Height)
                    selectedResolution = index;
            }
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(selectedResolution);
            windowModeDropdown.SetValueWithoutNotify(
                (int)settings.Staged.WindowMode);
        }

        private void FocusPage(GrayboxSystemMenuPage3D page)
        {
            if (eventSystem == null) return;
            GameObject target;
            switch (page)
            {
                case GrayboxSystemMenuPage3D.Settings:
                    target = resolutionDropdown.gameObject;
                    break;
                case GrayboxSystemMenuPage3D.OperationGuide:
                    target = guideBackButton.gameObject;
                    break;
                case GrayboxSystemMenuPage3D.ExitConfirm:
                    target = exitCancelButton.gameObject;
                    break;
                default:
                    target = continueButton.gameObject;
                    break;
            }
            eventSystem.SetSelectedGameObject(target);
        }

        private void FocusStartPage()
        {
            if (eventSystem == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (isAcceptancePageOpen && !isNewGameConfirmationOpen)
            {
                eventSystem.SetSelectedGameObject(
                    acceptanceContinueButton != null &&
                    acceptanceContinueButton.interactable
                        ? acceptanceContinueButton.gameObject
                        : acceptanceBackButton?.gameObject);
                return;
            }
#endif
            GameObject target = isNewGameConfirmationOpen
                ? startNewGameCancelButton.gameObject
                : startContinueButton.interactable
                    ? startContinueButton.gameObject
                    : startNewGameButton.gameObject;
            eventSystem.SetSelectedGameObject(target);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ApplyStartAcceptanceVisibility()
        {
            if (startTitle == null || acceptancePage == null) return;
            bool acceptance = isStartPageOpen && isAcceptancePageOpen &&
                !isNewGameConfirmationOpen;
            SetLayoutPresentationVisible(
                startTitle.transform as RectTransform,
                !acceptance);
            SetLayoutPresentationVisible(
                startContinueButton?.transform as RectTransform,
                !acceptance);
            SetLayoutPresentationVisible(
                startNewGameButton?.transform as RectTransform,
                !acceptance);
            SetLayoutPresentationVisible(
                startAcceptanceButton?.transform as RectTransform,
                !acceptance);
            SetLayoutPresentationVisible(acceptancePage, acceptance);
            SetLayoutPresentationVisible(
                acceptanceContinueButton?.transform as RectTransform,
                acceptance);
            SetLayoutPresentationVisible(
                acceptanceNewGameButton?.transform as RectTransform,
                acceptance);
            SetLayoutPresentationVisible(
                acceptanceBackButton?.transform as RectTransform,
                acceptance);
        }

        private static void SetLayoutPresentationVisible(
            RectTransform rect,
            bool visible)
        {
            if (rect == null) return;
            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element != null) element.ignoreLayout = !visible;
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private void RefreshStartPageGraphics()
        {
            if (startPage == null || !startPage.gameObject.activeSelf) return;
            startPage.gameObject.SetActive(false);
            startPage.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(startPage);
            Canvas.ForceUpdateCanvases();
        }
#endif

        private void UpdateRootVisibility()
        {
            if (uiRoot == null) return;
            UpdateSpeedControlsLayout();
            bool visible = isOpen || isStartPageOpen;
            uiRoot.gameObject.SetActive(visible);
            if (speedControlsRoot != null)
            {
                speedControlsRoot.gameObject.SetActive(!visible);
                speedPauseButton.interactable = !visible;
                speedOneButton.interactable = !visible;
                speedTwoButton.interactable = !visible;
            }
            if (formalSaveFeedback != null)
            {
                formalSaveFeedback.gameObject.SetActive(
                    visible &&
                    !string.IsNullOrWhiteSpace(formalSaveFeedback.text));
            }
        }

        private void UpdateSpeedControlsLayout()
        {
            if (speedControlsRoot == null || canvas == null) return;
            Vector2 canvasSize = ResolveCanvasSize();
            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                new Rect(0f, 0f, canvasSize.x, canvasSize.y));
            Rect slot = layout.SpeedAndMenuSlot;
            Vector2 size = new Vector2(
                Mathf.Min(286f, slot.width),
                Mathf.Min(44f, slot.height));
            speedControlsRoot.anchorMin = Vector2.one;
            speedControlsRoot.anchorMax = Vector2.one;
            speedControlsRoot.pivot = Vector2.one;
            speedControlsRoot.anchoredPosition = new Vector2(
                slot.xMax - canvasSize.x,
                slot.yMax - canvasSize.y);
            speedControlsRoot.sizeDelta = size;
            speedControlsRoot.localScale = Vector3.one;
        }

        private void RefreshFormalLayout()
        {
            if (canvas == null) return;
            UpdateSpeedControlsLayout();
            Vector2 canvasSize = ResolveCanvasSize();
            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                new Rect(0f, 0f, canvasSize.x, canvasSize.y));
            foreach (RectTransform page in new[]
                     {
                         startPage,
                         newGameConfirmPage,
                         mainPage,
                         settingsPage,
                         operationGuidePage,
                         exitConfirmPage
                     })
            {
                if (page == null) continue;
                page.anchorMin = Vector2.one * .5f;
                page.anchorMax = Vector2.one * .5f;
                page.pivot = Vector2.one * .5f;
                page.anchoredPosition = Vector2.zero;
                page.sizeDelta = new Vector2(
                    Mathf.Min(620f, layout.MainModalArea.width),
                    Mathf.Min(460f, layout.MainModalArea.height));
                page.localScale = Vector3.one;
            }
        }

        private Vector2 ResolveCanvasSize()
        {
            RectTransform rect = canvas == null
                ? null
                : canvas.GetComponent<RectTransform>();
            Vector2 size = rect == null ? Vector2.zero : rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                size = canvas != null ? canvas.pixelRect.size : Vector2.zero;
            if (size.x <= 0f || size.y <= 0f)
                size = FormalUiLayoutProfile3D.Standard.ReferenceResolution;
            return size;
        }

        private void RetireStaleRoots()
        {
            for (var index = canvas.transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = canvas.transform.GetChild(index);
                if (child.name != "GrayboxSystemMenuUi.Root" &&
                    child.name !=
                        "GrayboxFormalSaveCheckpointWarning.Root" &&
                    child.name != "GrayboxFormalSpeedControls.Root")
                    continue;
                child.gameObject.SetActive(false);
                DestroyGenerated(child.gameObject);
            }
        }

        private void DestroyUi()
        {
            if (uiRoot != null)
            {
                uiRoot.gameObject.SetActive(false);
                DestroyGenerated(uiRoot.gameObject);
            }
            if (checkpointWarningRoot != null)
            {
                checkpointWarningRoot.gameObject.SetActive(false);
                DestroyGenerated(checkpointWarningRoot.gameObject);
            }
            if (speedControlsRoot != null)
            {
                speedControlsRoot.gameObject.SetActive(false);
                DestroyGenerated(speedControlsRoot.gameObject);
            }
            uiRoot = null;
            speedControlsRoot = null;
            blocker = null;
            pausedTitle = null;
            startPage = null;
            newGameConfirmPage = null;
            mainPage = null;
            settingsPage = null;
            operationGuidePage = null;
            exitConfirmPage = null;
            startTitle = null;
            startContinueButton = null;
            startNewGameButton = null;
            startNewGameCancelButton = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            acceptancePage = null;
            startAcceptanceButton = null;
            acceptanceContinueButton = null;
            acceptanceNewGameButton = null;
            acceptanceBackButton = null;
            isAcceptancePageOpen = false;
#endif
            continueButton = null;
            resolutionDropdown = null;
            windowModeDropdown = null;
            guideBackButton = null;
            exitWithoutSavingButton = null;
            exitCancelButton = null;
            formalSaveFeedback = null;
            checkpointWarningRoot = null;
            checkpointWarning = null;
            speedPauseButton = null;
            speedOneButton = null;
            speedTwoButton = null;
            isOpen = false;
            isStartPageOpen = false;
            isNewGameConfirmationOpen = false;
        }

        private static RectTransform CreatePage(
            Transform parent,
            string name)
        {
            RectTransform page = CreateRect(parent, name);
            page.anchorMin = new Vector2(.5f, .5f);
            page.anchorMax = new Vector2(.5f, .5f);
            page.pivot = new Vector2(.5f, .5f);
            page.sizeDelta = new Vector2(620f, 460f);
            var image = page.gameObject.AddComponent<Image>();
            image.color = new Color(.08f, .1f, .12f, .98f);
            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return page;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Action callback)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.22f, .28f, .34f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetLayout(rect, 0f, 42f, 1f);
            if (callback != null)
                button.onClick.AddListener(() => callback());
            Text text = CreateLabel(rect, "Label", label);
            SetReadableFontSize(text, 16f);
            return button;
        }

        private static Dropdown CreateDropdown(
            Transform parent,
            string name)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.2f, .24f, .28f, 1f);
            var dropdown = rect.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = image;
            SetLayout(rect, 0f, 42f, 1f);

            Text caption = CreateLabel(rect, "Label", string.Empty);
            caption.alignment = TextAnchor.MiddleLeft;
            caption.rectTransform.offsetMin = new Vector2(12f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-30f, 0f);

            RectTransform template = CreateRect(rect, "Template");
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(.5f, 1f);
            template.anchoredPosition = Vector2.zero;
            template.sizeDelta = new Vector2(0f, 180f);
            var templateImage = template.gameObject.AddComponent<Image>();
            templateImage.color = new Color(.1f, .12f, .14f, 1f);
            var scrollRect = template.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateRect(template, "Viewport");
            Stretch(viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(0f, 30f);

            RectTransform item = CreateRect(content, "Item");
            item.anchorMin = new Vector2(0f, .5f);
            item.anchorMax = new Vector2(1f, .5f);
            item.sizeDelta = new Vector2(0f, 30f);
            var itemImage = item.gameObject.AddComponent<Image>();
            itemImage.color = new Color(.2f, .24f, .28f, 1f);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemImage;
            Text itemLabel = CreateLabel(item, "Item Label", "Option");
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.rectTransform.offsetMin = new Vector2(12f, 0f);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            string value)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            SetReadableFontSize(text, 15f);
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private static void SetReadableFontSize(Text text, float designSize)
        {
            FormalUiCanvasConfiguration3D.ApplyReadableFontSize(
                text,
                designSize);
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
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

        private static void PlaceFixed(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetLayout(
            RectTransform rect,
            float preferredWidth,
            float preferredHeight,
            float flexibleWidth)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
        }

        private static void DestroyGenerated(GameObject gameObject)
        {
            if (gameObject == null) return;
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
