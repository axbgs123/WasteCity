using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Usability
{
    public sealed class GrayboxSystemMenuView3D : MonoBehaviour
    {
        private static readonly string[] VisibleControlIds =
        {
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
            "Exit.Confirm",
            "Exit.Cancel"
        };

        [SerializeField] private Canvas canvas;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private GrayboxSystemMenuController3D controller;

        private RectTransform uiRoot;
        private Image blocker;
        private RectTransform mainPage;
        private RectTransform settingsPage;
        private RectTransform operationGuidePage;
        private RectTransform exitConfirmPage;
        private Button continueButton;
        private Dropdown resolutionDropdown;
        private Dropdown windowModeDropdown;
        private Button guideBackButton;
        private Button exitCancelButton;
        private bool isOpen;

        public bool IsPointerBlockerActive =>
            isOpen &&
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
                return isOpen &&
                    uiRoot != null &&
                    selected != null &&
                    selected.transform.IsChildOf(uiRoot);
            }
        }

        public GrayboxSystemMenuPage3D VisiblePage { get; private set; } =
            GrayboxSystemMenuPage3D.Main;

        public static IReadOnlyList<string> ResolveVisibleControlIds(
            bool developmentBuild)
        {
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

        public void Render(
            bool open,
            GrayboxSystemMenuPage3D page,
            GrayboxDisplaySettingsModel3D settings)
        {
            TryBuildUi();
            isOpen = open;
            VisiblePage = page;
            if (uiRoot == null) return;

            uiRoot.gameObject.SetActive(open);
            mainPage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.Main);
            settingsPage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.Settings);
            operationGuidePage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.OperationGuide);
            exitConfirmPage.gameObject.SetActive(
                open && page == GrayboxSystemMenuPage3D.ExitConfirm);

            if (!open)
            {
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(null);
                return;
            }

            if (settings != null)
                SyncSettings(settings);
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
        }

        private void OnDisable()
        {
            isOpen = false;
            if (uiRoot != null)
                uiRoot.gameObject.SetActive(false);
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            DestroyUi();
            canvas = null;
            eventSystem = null;
            controller = null;
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
            uiRoot = CreateRect(
                canvas.transform,
                "GrayboxSystemMenuUi.Root");
            Stretch(uiRoot);

            RectTransform blockerRect = CreateRect(
                uiRoot,
                "Modal.Blocker");
            Stretch(blockerRect);
            blocker = blockerRect.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, .78f);
            blocker.raycastTarget = true;

            Text title = CreateLabel(
                uiRoot,
                "Paused.Title",
                "游戏已暂停");
            title.fontSize = 28;
            PlaceFixed(
                (RectTransform)title.transform,
                new Vector2(0f, 260f),
                new Vector2(500f, 48f));

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
                "当前 3D 灰盒进度不会保存\n确认退出游戏吗？");
            warning.fontSize = 18;
            SetLayout((RectTransform)warning.transform, 0f, 100f, 1f);
            CreateButton(
                exitConfirmPage,
                "Exit.Confirm",
                "确认退出",
                () => controller?.ConfirmExit());
            exitCancelButton = CreateButton(
                exitConfirmPage,
                "Exit.Cancel",
                "取消",
                () => controller?.CancelExit());
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

        private void RetireStaleRoots()
        {
            for (var index = canvas.transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = canvas.transform.GetChild(index);
                if (child.name != "GrayboxSystemMenuUi.Root")
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
            uiRoot = null;
            blocker = null;
            mainPage = null;
            settingsPage = null;
            operationGuidePage = null;
            exitConfirmPage = null;
            continueButton = null;
            resolutionDropdown = null;
            windowModeDropdown = null;
            guideBackButton = null;
            exitCancelButton = null;
            isOpen = false;
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
            text.fontSize = 16;
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
            text.fontSize = 15;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
            return text;
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
