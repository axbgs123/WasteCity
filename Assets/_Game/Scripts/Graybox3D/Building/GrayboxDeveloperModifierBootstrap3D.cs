using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine.UI;
#endif

namespace WasteCity.Graybox3D.Building
{
    public enum DevelopmentConstructionSpeed
    {
        Normal = 1,
        Fast10 = 10,
        Fast100 = 100
    }

    public sealed class GrayboxDeveloperModifierBootstrap3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;
        [SerializeField] private Canvas canvas;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GrayboxDeveloperModifier3D modifier;
        private bool retainedModifiedGameState;
        private GameObject panelRoot;
        private CatalogRow[] resourceRows;
        private CatalogRow[] researchRows;
        private Text selectedResourceLabel;
        private Text selectedResearchLabel;
        private Text feedbackLabel;
        private string selectedResourceId;
        private string selectedResearchId;
#endif

        public bool IsRuntimeAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Application.isPlaying &&
                    isActiveAndEnabled &&
                    ResolveRuntimeAvailability(
                        Application.isEditor,
                        Debug.isDebugBuild) &&
                    modifier != null &&
                    panelRoot != null;
#else
                return false;
#endif
            }
        }

        public bool IsPanelOpen
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return IsRuntimeAvailable && panelRoot.activeSelf;
#else
                return false;
#endif
            }
        }

        public bool HasModifiedGameState
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return retainedModifiedGameState ||
                    (modifier != null && modifier.HasModifiedGameState);
#else
                return false;
#endif
            }
        }

        public static bool ResolveRuntimeAvailability(
            bool isEditor,
            bool isDevelopmentBuild)
        {
            return isEditor || isDevelopmentBuild;
        }

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation,
            Canvas canvas)
        {
            bool sessionChanged = this.session != session;
            if (this.session == session &&
                this.city == city &&
                this.presentation == presentation &&
                this.canvas == canvas)
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DisposeDevelopmentSurface();
            if (sessionChanged)
                retainedModifiedGameState = false;
#endif
            this.session = session;
            this.city = city;
            this.presentation = presentation;
            this.canvas = canvas;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryCreateDevelopmentSurface();
#endif
        }

        public bool TryTogglePanel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsRuntimeAvailable)
                return false;
            panelRoot.SetActive(!panelRoot.activeSelf);
            return true;
#else
            return false;
#endif
        }

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryCreateDevelopmentSurface();
#endif
        }

        private void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryCreateDevelopmentSurface();
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DisposeDevelopmentSurface();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DisposeDevelopmentSurface();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void TryCreateDevelopmentSurface()
        {
            if (!Application.isPlaying ||
                !isActiveAndEnabled ||
                !ResolveRuntimeAvailability(
                    Application.isEditor,
                    Debug.isDebugBuild) ||
                session == null ||
                city == null ||
                presentation == null ||
                canvas == null)
                return;
            if (modifier != null && panelRoot != null)
                return;

            DisposeDevelopmentSurface();
            modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            CreatePanel();
        }

        private void DisposeDevelopmentSurface()
        {
            if (modifier != null && modifier.HasModifiedGameState)
                retainedModifiedGameState = true;
            modifier = null;
            if (panelRoot == null)
                return;

            Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
                buttons[index].onClick.RemoveAllListeners();
            InputField[] inputs = panelRoot.GetComponentsInChildren<
                InputField>(true);
            for (var index = 0; index < inputs.Length; index++)
                inputs[index].onValueChanged.RemoveAllListeners();
            GameObject ownedPanel = panelRoot;
            panelRoot = null;
            resourceRows = null;
            researchRows = null;
            selectedResourceLabel = null;
            selectedResearchLabel = null;
            feedbackLabel = null;
            selectedResourceId = null;
            selectedResearchId = null;
            ownedPanel.SetActive(false);
            if (Application.isPlaying)
                Destroy(ownedPanel);
            else
                DestroyImmediate(ownedPanel);
        }

        private void CreatePanel()
        {
            panelRoot = new GameObject(
                "Graybox Developer Modifier",
                typeof(RectTransform));
            panelRoot.transform.SetParent(canvas.transform, false);
            RectTransform root = (RectTransform)panelRoot.transform;
            root.anchorMin = new Vector2(1f, .5f);
            root.anchorMax = new Vector2(1f, .5f);
            root.pivot = new Vector2(1f, .5f);
            root.anchoredPosition = new Vector2(-16f, 0f);
            root.sizeDelta = new Vector2(420f, 1040f);
            ApplyResponsivePanelScale();
            Image background = panelRoot.AddComponent<Image>();
            background.color = new Color(.08f, .1f, .11f, .94f);
            var layout = panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 3f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateLabel(root, "Development Mode Label", "开发模式");
            CreateLabel(root, "Resource Catalog Label", "物品目录（中文搜索）");
            InputField resourceSearch = CreateInput(
                root, "Resource Search", string.Empty);
            resourceRows = CreateCatalogResults(
                root,
                "Resource Results",
                GrayboxDeveloperCatalogQuery3D.ResourceEntries,
                entry => SelectResource(entry));
            selectedResourceId =
                GrayboxDeveloperCatalogQuery3D.ResourceEntries[0].StableId;
            selectedResourceLabel = CreateLabel(
                root,
                "Selected Resource",
                "当前物品：" +
                GrayboxDeveloperCatalogQuery3D.ResourceEntries[0]
                    .DisplayName);
            resourceSearch.onValueChanged.AddListener(value =>
                ApplyCatalogFilter(resourceRows, value));
            CreateButton(root, "Resource +100", "资源 +100", () =>
                SetFeedback(modifier.AddResourceWithFeedback(
                    selectedResourceId,
                    100).Message));
            CreateButton(root, "Resource +1000", "资源 +1000", () =>
                SetFeedback(modifier.AddResourceWithFeedback(
                    selectedResourceId,
                    1000).Message));
            CreateButton(root, "Clear Resource", "资源清零", () =>
            {
                bool cleared = modifier.ClearResource(selectedResourceId);
                SetFeedback(cleared
                    ? "已清零当前物品"
                    : "物品清零失败");
            });
            InputField resourceAmount = CreateInput(
                root, "Resource Amount", "0");
            CreateButton(root, "Set Resource", "设置资源", () =>
            {
                if (int.TryParse(resourceAmount.text, out int amount))
                {
                    SetFeedback(modifier.SetResourceWithFeedback(
                        selectedResourceId,
                        amount).Message);
                }
                else
                {
                    SetFeedback("请输入非负整数");
                }
            });

            CreateLabel(root, "Research Catalog Label", "科技目录（中文搜索）");
            InputField researchSearch = CreateInput(
                root,
                "Research Search",
                string.Empty);
            researchRows = CreateCatalogResults(
                root,
                "Research Results",
                GrayboxDeveloperCatalogQuery3D.ResearchEntries,
                entry => SelectResearch(entry));
            int initialResearchIndex = Math.Min(
                1,
                GrayboxDeveloperCatalogQuery3D.ResearchEntries.Count - 1);
            GrayboxDeveloperCatalogEntry3D initialResearch =
                GrayboxDeveloperCatalogQuery3D.ResearchEntries[
                    initialResearchIndex];
            selectedResearchId = initialResearch.StableId;
            selectedResearchLabel = CreateLabel(
                root,
                "Selected Research",
                "当前科技：" + initialResearch.DisplayName);
            researchSearch.onValueChanged.AddListener(value =>
                ApplyCatalogFilter(researchRows, value));
            CreateButton(root, "Unlock Research", "解锁当前科技", () =>
                SetFeedback(modifier.UnlockResearchWithFeedback(
                    selectedResearchId).Message));
            CreateButton(root, "Unlock Technology", "解锁科技路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.Technology).Message));
            CreateButton(root, "Unlock Cultivation", "解锁修仙路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.Cultivation).Message));
            CreateButton(root, "Unlock Biological Ascension", "解锁血肉路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.BiologicalAscension).Message));
            CreateButton(root, "Unlock Psionics", "解锁灵能路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.Psionics).Message));
            CreateButton(root, "Unlock All", "解锁全部研究", () =>
                SetFeedback(modifier.UnlockAllResearchWithFeedback().Message));

            feedbackLabel = CreateLabel(
                root,
                "Developer Feedback",
                "请选择物品或科技");

            CreateLabel(root, "Session Tools Label", "城市与施工");
            InputField populationAmount = CreateInput(
                root, "Population Amount", "200");
            CreateButton(root, "Set Population", "设置人口", () =>
            {
                if (int.TryParse(populationAmount.text, out int amount))
                {
                    SetFeedback(modifier.SetPopulation(amount)
                        ? "人口已设置为 " + amount
                        : "人口设置失败");
                }
                else
                {
                    SetFeedback("请输入非负整数");
                }
            });

            CreateButton(root, "Set Mobile", "切换移动形态", () =>
                SetFeedback(modifier.SetCityMode(CityMode.Mobile)
                    ? "已切换为移动形态"
                    : "无法切换为移动形态"));
            CreateButton(root, "Set Fortress", "切换堡垒形态", () =>
                SetFeedback(modifier.SetCityMode(CityMode.Fortress)
                    ? "已切换为堡垒形态"
                    : "无法切换为堡垒形态"));
            CreateButton(root, "Complete Transition", "完成形态转换", () =>
                SetFeedback(modifier.CompleteCityTransition()
                    ? "形态转换已完成"
                    : "当前没有可完成的形态转换"));
            CreateButton(root, "Multiplier 1x", "施工 1×", () =>
            {
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Normal);
                SetFeedback("施工速度已设置为 1×");
            });
            CreateButton(root, "Multiplier 10x", "施工 10×", () =>
            {
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast10);
                SetFeedback("施工速度已设置为 10×");
            });
            CreateButton(root, "Multiplier 100x", "施工 100×", () =>
            {
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast100);
                SetFeedback("施工速度已设置为 100×");
            });
            CreateButton(root, "Complete Construction", "立即完成施工", () =>
            {
                modifier.CompleteAllConstruction();
                SetFeedback("已完成全部施工");
            });
            panelRoot.SetActive(false);
        }

        private static float CalculateResponsivePanelScale(
            float viewportWidth,
            float viewportHeight,
            float panelWidth,
            float panelHeight)
        {
            if (viewportWidth <= 0f || viewportHeight <= 0f ||
                panelWidth <= 0f || panelHeight <= 0f)
            {
                return 1f;
            }

            const float margin = 16f;
            float widthScale = Mathf.Max(0f, viewportWidth - margin) /
                panelWidth;
            float heightScale = Mathf.Max(0f, viewportHeight - margin) /
                panelHeight;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), .25f, 1f);
        }

        private void ApplyResponsivePanelScale()
        {
            if (panelRoot == null || canvas == null) return;
            RectTransform root = panelRoot.transform as RectTransform;
            if (root == null) return;
            float scale = CalculateResponsivePanelScale(
                canvas.pixelRect.width,
                canvas.pixelRect.height,
                root.rect.width,
                root.rect.height);
            root.localScale = Vector3.one * scale;
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyResponsivePanelScale();
        }

        private CatalogRow[] CreateCatalogResults(
            RectTransform parent,
            string name,
            System.Collections.Generic.IReadOnlyList<
                GrayboxDeveloperCatalogEntry3D> entries,
            Action<GrayboxDeveloperCatalogEntry3D> selected)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.AddComponent<LayoutElement>().preferredHeight = 100f;
            Image background = root.AddComponent<Image>();
            background.color = new Color(.04f, .055f, .06f, .95f);
            var scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform));
            viewportObject.transform.SetParent(root.transform, false);
            RectTransform viewport =
                (RectTransform)viewportObject.transform;
            Stretch(viewport);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, .01f);
            viewportObject.AddComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject(
                "Content",
                typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter =
                contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            var rows = new CatalogRow[entries.Count];
            for (var index = 0; index < rows.Length; index++)
            {
                GrayboxDeveloperCatalogEntry3D entry = entries[index];
                string prefix = entry.Kind ==
                    GrayboxDeveloperCatalogKind3D.Resource
                    ? "Developer.Resource."
                    : "Developer.Research.";
                Button button = CreateButton(
                    content,
                    prefix + entry.StableId,
                    entry.DisplayName,
                    () => selected(entry));
                rows[index] = new CatalogRow(entry, button.gameObject);
            }
            return rows;
        }

        private void SelectResource(GrayboxDeveloperCatalogEntry3D entry)
        {
            selectedResourceId = entry.StableId;
            selectedResourceLabel.text = "当前物品：" + entry.DisplayName;
            SetFeedback("已选择物品：" + entry.DisplayName);
        }

        private void SelectResearch(GrayboxDeveloperCatalogEntry3D entry)
        {
            selectedResearchId = entry.StableId;
            selectedResearchLabel.text = "当前科技：" + entry.DisplayName;
            SetFeedback("已选择科技：" + entry.DisplayName);
        }

        private static void ApplyCatalogFilter(
            CatalogRow[] rows,
            string query)
        {
            if (rows == null) return;
            for (var index = 0; index < rows.Length; index++)
                rows[index].Root.SetActive(rows[index].Entry.Matches(query));
        }

        private void SetFeedback(string message)
        {
            if (feedbackLabel != null)
                feedbackLabel.text = string.IsNullOrWhiteSpace(message)
                    ? "操作未完成"
                    : message;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static InputField CreateInput(
            RectTransform parent,
            string name,
            string initialValue)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.AddComponent<LayoutElement>().preferredHeight = 28f;
            Image image = root.AddComponent<Image>();
            image.color = Color.white;
            var input = root.AddComponent<InputField>();
            Text text = CreateText(root.transform, "Text", initialValue);
            text.color = Color.black;
            input.textComponent = text;
            input.text = initialValue;
            return input;
        }

        private static Button CreateButton(
            RectTransform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction callback)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.AddComponent<LayoutElement>().preferredHeight = 26f;
            Image image = root.AddComponent<Image>();
            image.color = new Color(.2f, .24f, .25f, .96f);
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(callback);
            CreateText(root.transform, "Label", label);
            return button;
        }

        private static Text CreateLabel(
            RectTransform parent,
            string name,
            string value)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.AddComponent<LayoutElement>().preferredHeight = 28f;
            return CreateText(root.transform, "Text", value);
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = root.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private sealed class CatalogRow
        {
            public CatalogRow(
                GrayboxDeveloperCatalogEntry3D entry,
                GameObject root)
            {
                Entry = entry;
                Root = root;
            }

            public GrayboxDeveloperCatalogEntry3D Entry { get; }
            public GameObject Root { get; }
        }
#endif
    }
}
