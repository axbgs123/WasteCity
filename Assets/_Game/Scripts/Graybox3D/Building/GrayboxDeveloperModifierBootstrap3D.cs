using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine.UI;
using WasteCity.Progression;
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
        private GrayboxDeveloperProgressionFacade3D progressionFacade;
        private GrayboxDeveloperTechnologyStateFacade3D
            technologyStateFacade;
        private bool retainedModifiedGameState;
        private GameObject panelRoot;
        private CatalogRow[] resourceRows;
        private CatalogRow[] researchRows;
        private CatalogRow[] technologyStatusRows;
        private CatalogRow[] progressionRows;
        private Text selectedResourceLabel;
        private Text selectedResearchLabel;
        private Text selectedTechnologyStatusLabel;
        private Text selectedProgressionActionLabel;
        private Text feedbackLabel;
        private string selectedResourceId;
        private string selectedResearchId;
        private string selectedTechnologyStatusId;
        private string selectedProgressionActionId;
        private InputField progressionAmount;
        private InputField progressionPressureThreshold;
        private InputField progressionAnchorId;
        private int canvasSortingOrderBeforePanel;
        private bool ownsCanvasSortingOverride;
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
            {
                retainedModifiedGameState = false;
                technologyStateFacade = null;
            }
#endif
            this.session = session;
            this.city = city;
            this.presentation = presentation;
            this.canvas = canvas;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryCreateDevelopmentSurface();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ConfigureProgressionFacade(
            GrayboxDeveloperProgressionFacade3D facade)
        {
            if (ReferenceEquals(progressionFacade, facade)) return;
            progressionFacade = facade;
            modifier?.ConfigureProgressionFacade(facade);
        }

        public void ConfigureTechnologyStateFacade(
            GrayboxDeveloperTechnologyStateFacade3D facade)
        {
            if (ReferenceEquals(technologyStateFacade, facade)) return;
            technologyStateFacade = facade;
            modifier?.ConfigureTechnologyStateFacade(facade);
        }
#endif

        public bool TryTogglePanel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsRuntimeAvailable)
                return false;
            bool opening = !panelRoot.activeSelf;
            if (opening)
            {
                AcquireCanvasSortingOverride();
                panelRoot.transform.SetAsLastSibling();
            }
            else
            {
                ReleaseCanvasSortingOverride();
            }
            panelRoot.SetActive(opening);
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
            modifier.ConfigureProgressionFacade(progressionFacade);
            EnsureTechnologyStateFacade();
            CreatePanel();
        }

        private void EnsureTechnologyStateFacade()
        {
            if (technologyStateFacade == null)
            {
                technologyStateFacade =
                    new GrayboxDeveloperTechnologyStateFacade3D(
                        FindObjectOfType<GrayboxDefenseController3D>(),
                        FindObjectOfType<
                            GrayboxCivilizationExpansionController3D>(),
                        session);
            }
            modifier?.ConfigureTechnologyStateFacade(technologyStateFacade);
        }

        private void DisposeDevelopmentSurface()
        {
            ReleaseCanvasSortingOverride();
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
            technologyStatusRows = null;
            progressionRows = null;
            selectedResourceLabel = null;
            selectedResearchLabel = null;
            selectedTechnologyStatusLabel = null;
            selectedProgressionActionLabel = null;
            feedbackLabel = null;
            selectedResourceId = null;
            selectedResearchId = null;
            selectedTechnologyStatusId = null;
            selectedProgressionActionId = null;
            progressionAmount = null;
            progressionPressureThreshold = null;
            progressionAnchorId = null;
            ownedPanel.SetActive(false);
            if (Application.isPlaying)
                Destroy(ownedPanel);
            else
                DestroyImmediate(ownedPanel);
        }

        private void AcquireCanvasSortingOverride()
        {
            if (canvas == null || ownsCanvasSortingOverride) return;
            canvasSortingOrderBeforePanel = canvas.sortingOrder;
            canvas.sortingOrder = Mathf.Max(
                canvas.sortingOrder,
                FormalUiLayoutProfile3D.Standard.OperationsSortingOrder + 1);
            ownsCanvasSortingOverride = true;
        }

        private void ReleaseCanvasSortingOverride()
        {
            if (!ownsCanvasSortingOverride) return;
            if (canvas != null)
                canvas.sortingOrder = canvasSortingOrderBeforePanel;
            ownsCanvasSortingOverride = false;
        }

        private void CreatePanel()
        {
            panelRoot = new GameObject(
                "Graybox Developer Modifier",
                typeof(RectTransform));
            panelRoot.transform.SetParent(canvas.transform, false);
            RectTransform root = (RectTransform)panelRoot.transform;
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(1f, .5f);
            root.anchoredPosition = new Vector2(-16f, 0f);
            root.sizeDelta = new Vector2(420f, -32f);
            root.localScale = Vector3.one;
            Image background = panelRoot.AddComponent<Image>();
            background.color = new Color(.08f, .1f, .11f, .94f);
            RectTransform content = CreateScrollablePanelContent(root);

            CreateLabel(content, "Development Mode Label", "开发模式");
            CreateLabel(content, "Resource Catalog Label", "物品目录（中文搜索）");
            InputField resourceSearch = CreateInput(
                content, "Resource Search", string.Empty);
            resourceRows = CreateCatalogResults(
                content,
                "Resource Results",
                GrayboxDeveloperCatalogQuery3D.ResourceEntries,
                entry => SelectResource(entry));
            selectedResourceId =
                GrayboxDeveloperCatalogQuery3D.ResourceEntries[0].StableId;
            selectedResourceLabel = CreateLabel(
                content,
                "Selected Resource",
                "当前物品：" +
                GrayboxDeveloperCatalogQuery3D.ResourceEntries[0]
                    .DisplayName);
            resourceSearch.onValueChanged.AddListener(value =>
                ApplyCatalogFilter(resourceRows, value));
            CreateButton(content, "Resource +100", "资源 +100", () =>
                SetFeedback(modifier.AddResourceWithFeedback(
                    selectedResourceId,
                    100).Message));
            CreateButton(content, "Resource +1000", "资源 +1000", () =>
                SetFeedback(modifier.AddResourceWithFeedback(
                    selectedResourceId,
                    1000).Message));
            CreateButton(content, "Clear Resource", "资源清零", () =>
            {
                bool cleared = modifier.ClearResource(selectedResourceId);
                SetFeedback(cleared
                    ? "已清零当前物品"
                    : "物品清零失败");
            });
            InputField resourceAmount = CreateInput(
                content, "Resource Amount", "0");
            CreateButton(content, "Set Resource", "设置资源", () =>
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

            CreateLabel(content, "Research Catalog Label", "科技目录（中文搜索）");
            InputField researchSearch = CreateInput(
                content,
                "Research Search",
                string.Empty);
            researchRows = CreateCatalogResults(
                content,
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
                content,
                "Selected Research",
                "当前科技：" + initialResearch.DisplayName);
            researchSearch.onValueChanged.AddListener(value =>
                ApplyCatalogFilter(researchRows, value));
            CreateButton(content, "Unlock Research", "解锁当前科技", () =>
                SetFeedback(modifier.UnlockResearchWithFeedback(
                    selectedResearchId).Message));
            CreateButton(content, "Unlock Technology", "解锁科技路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.Technology).Message));
            CreateButton(content, "Unlock Cultivation", "解锁修仙路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.Cultivation).Message));
            CreateButton(content, "Unlock Biological Ascension", "解锁血肉路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.BiologicalAscension).Message));
            CreateButton(content, "Unlock Psionics", "解锁灵能路线", () =>
                SetFeedback(modifier.UnlockRouteWithFeedback(
                    ContentRoute.Psionics).Message));
            CreateButton(content, "Unlock All", "解锁全部研究", () =>
                SetFeedback(modifier.UnlockAllResearchWithFeedback().Message));

            CreateLabel(
                content,
                "Technology Status Catalog Label",
                "科技状态（列表浏览 + 中文/输入搜索）");
            InputField technologyStatusSearch = CreateInput(
                content,
                "Technology Status Search",
                string.Empty);
            technologyStatusRows = CreateCatalogResults(
                content,
                "Technology Status Results",
                GrayboxDeveloperCatalogQuery3D.TechnologyStatusEntries,
                entry => SelectTechnologyStatus(entry));
            GrayboxDeveloperCatalogEntry3D initialTechnologyStatus =
                GrayboxDeveloperCatalogQuery3D.TechnologyStatusEntries[0];
            selectedTechnologyStatusId = initialTechnologyStatus.StableId;
            selectedTechnologyStatusLabel = CreateLabel(
                content,
                "Selected Technology Status",
                "当前科技状态：" + initialTechnologyStatus.DisplayName);
            technologyStatusSearch.onValueChanged.AddListener(value =>
                ApplyCatalogFilter(technologyStatusRows, value));
            CreateButton(
                content,
                "Apply Technology Status Fixture",
                "施加当前状态",
                () => ExecuteTechnologyStateAction(
                    GrayboxDeveloperTechnologyStateAction3D.Apply));
            CreateButton(
                content,
                "Set Technology Status One Stack",
                "设为 1 层",
                () => ExecuteTechnologyStateAction(
                    GrayboxDeveloperTechnologyStateAction3D.SetOneStack));
            CreateButton(
                content,
                "Fill Technology Status Stacks",
                "补满状态层数",
                () => ExecuteTechnologyStateAction(
                    GrayboxDeveloperTechnologyStateAction3D.FillStacks));
            CreateButton(
                content,
                "Clear Selected Technology Status",
                "清除当前状态",
                () => ExecuteTechnologyStateAction(
                    GrayboxDeveloperTechnologyStateAction3D.Clear));
            CreateButton(
                content,
                "Expire Selected Technology Status",
                "当前状态立即到期",
                () => ExecuteTechnologyStateAction(
                    GrayboxDeveloperTechnologyStateAction3D.Expire));
            CreateButton(
                content,
                "Trigger Technology Overload",
                "触发选中激光塔过载",
                () => ExecuteTechnologyStateAction(
                    GrayboxDeveloperTechnologyStateAction3D.TriggerOverload));
            CreateButton(
                content,
                "List Technology Status Fixtures",
                "列出当前测试状态",
                () =>
                {
                    EnsureTechnologyStateFacade();
                    System.Collections.Generic.IReadOnlyList<string> names =
                        modifier.ListActiveTechnologyStates();
                    SetFeedback(names.Count == 0
                        ? "当前战役无活动科技状态"
                        : "当前科技状态：" + string.Join("、", names));
                });
            CreateButton(
                content,
                "Clear Technology Status Fixtures",
                "清理全部测试状态",
                () =>
                {
                    EnsureTechnologyStateFacade();
                    SetFeedback(modifier
                        .ClearTechnologyStatusFixturesWithFeedback().Message);
                });

            CreateLabel(content, "Progression Catalog Label",
                "文明进程（列表浏览 + 输入搜索）");
            InputField progressionSearch = CreateInput(
                content,
                "Progression Action Search",
                string.Empty);
            progressionRows = CreateCatalogResults(
                content,
                "Progression Action Results",
                GrayboxDeveloperCatalogQuery3D.ProgressionActionEntries,
                entry => SelectProgressionAction(entry));
            GrayboxDeveloperCatalogEntry3D initialProgression =
                GrayboxDeveloperCatalogQuery3D.ProgressionActionEntries[0];
            selectedProgressionActionId = initialProgression.StableId;
            selectedProgressionActionLabel = CreateLabel(
                content,
                "Selected Progression Action",
                "当前文明进程动作：" + initialProgression.DisplayName);
            progressionSearch.onValueChanged.AddListener(value =>
                ApplyCatalogFilter(progressionRows, value));
            progressionAmount = CreateInput(
                content, "Progression Amount", "10");
            progressionPressureThreshold = CreateInput(
                content, "Progression Pressure Threshold", "30");
            progressionAnchorId = CreateInput(
                content,
                "Progression Anchor Id",
                GrayboxRewindAnchorService3D.StableAnchorId);
            CreateLabel(content, "Progression Resource Parameter",
                "资源参数使用上方当前物品");
            CreateButton(
                content,
                "Execute Progression Action",
                "执行当前文明进程动作",
                ExecuteSelectedProgressionAction);

            feedbackLabel = CreateLabel(
                content,
                "Developer Feedback",
                "请选择物品、科技或文明进程动作");

            CreateLabel(content, "Session Tools Label", "城市与施工");
            InputField populationAmount = CreateInput(
                content, "Population Amount", "200");
            CreateButton(content, "Set Population", "设置人口", () =>
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

            CreateButton(content, "Set Mobile", "切换移动形态", () =>
                SetFeedback(modifier.SetCityMode(CityMode.Mobile)
                    ? "已切换为移动形态"
                    : "无法切换为移动形态"));
            CreateButton(content, "Set Fortress", "切换堡垒形态", () =>
                SetFeedback(modifier.SetCityMode(CityMode.Fortress)
                    ? "已切换为堡垒形态"
                    : "无法切换为堡垒形态"));
            CreateButton(content, "Complete Transition", "完成形态转换", () =>
                SetFeedback(modifier.CompleteCityTransition()
                    ? "形态转换已完成"
                    : "当前没有可完成的形态转换"));
            CreateButton(content, "Multiplier 1x", "施工 1×", () =>
            {
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Normal);
                SetFeedback("施工速度已设置为 1×");
            });
            CreateButton(content, "Multiplier 10x", "施工 10×", () =>
            {
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast10);
                SetFeedback("施工速度已设置为 10×");
            });
            CreateButton(content, "Multiplier 100x", "施工 100×", () =>
            {
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast100);
                SetFeedback("施工速度已设置为 100×");
            });
            CreateButton(content, "Complete Construction", "立即完成施工", () =>
            {
                modifier.CompleteAllConstruction();
                SetFeedback("已完成全部施工");
            });
            ApplyResponsivePanelLayout();
            panelRoot.SetActive(false);
        }

        private static RectTransform CreateScrollablePanelContent(
            RectTransform root)
        {
            var scrollObject = new GameObject(
                "Developer.Panel.Scroll",
                typeof(RectTransform));
            scrollObject.transform.SetParent(root, false);
            RectTransform scrollRoot =
                (RectTransform)scrollObject.transform;
            Stretch(scrollRoot);
            scrollRoot.offsetMin = new Vector2(8f, 8f);
            scrollRoot.offsetMax = new Vector2(-8f, -8f);
            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewportObject = new GameObject(
                "Developer.Panel.Viewport",
                typeof(RectTransform));
            viewportObject.transform.SetParent(scrollRoot, false);
            RectTransform viewport =
                (RectTransform)viewportObject.transform;
            Stretch(viewport);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, .01f);
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject(
                "Developer.Panel.Content",
                typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 3f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter =
                contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        private void ApplyResponsivePanelLayout()
        {
            if (panelRoot == null || canvas == null) return;
            RectTransform root = panelRoot.transform as RectTransform;
            if (root == null) return;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float width = canvasRect == null
                ? canvas.pixelRect.width
                : canvasRect.rect.width;
            if (width <= 0f)
                width = FormalUiLayoutProfile3D.Standard
                    .ReferenceResolution.x;
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(1f, .5f);
            root.anchoredPosition = new Vector2(-16f, 0f);
            root.sizeDelta = new Vector2(
                Mathf.Min(420f, Mathf.Max(240f, width - 32f)),
                -32f);
            root.localScale = Vector3.one;
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyResponsivePanelLayout();
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
                string prefix;
                switch (entry.Kind)
                {
                    case GrayboxDeveloperCatalogKind3D.Resource:
                        prefix = "Developer.Resource.";
                        break;
                    case GrayboxDeveloperCatalogKind3D.Research:
                        prefix = "Developer.Research.";
                        break;
                    case GrayboxDeveloperCatalogKind3D.TechnologyStatus:
                        prefix = "Developer.TechnologyStatus.";
                        break;
                    default:
                        prefix = "Developer.Progression.";
                        break;
                }
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

        private void SelectTechnologyStatus(
            GrayboxDeveloperCatalogEntry3D entry)
        {
            selectedTechnologyStatusId = entry.StableId;
            selectedTechnologyStatusLabel.text =
                "当前科技状态：" + entry.DisplayName;
            SetFeedback("已选择科技状态：" + entry.DisplayName);
        }

        private void SelectProgressionAction(
            GrayboxDeveloperCatalogEntry3D entry)
        {
            selectedProgressionActionId = entry.StableId;
            selectedProgressionActionLabel.text =
                "当前文明进程动作：" + entry.DisplayName;
            SetFeedback("已选择文明进程动作：" + entry.DisplayName);
        }

        private void ExecuteTechnologyStateAction(
            GrayboxDeveloperTechnologyStateAction3D action)
        {
            EnsureTechnologyStateFacade();
            SetFeedback(modifier.ExecuteTechnologyStatusActionWithFeedback(
                selectedTechnologyStatusId,
                action).Message);
        }

        private void ExecuteSelectedProgressionAction()
        {
            if (progressionFacade == null)
            {
                SetFeedback("文明进程服务尚未连接");
                return;
            }
            if (!GrayboxDeveloperCatalogQuery3D.TryResolveProgressionAction(
                    selectedProgressionActionId,
                    out GrayboxDeveloperCatalogEntry3D action))
            {
                SetFeedback("请选择有效的文明进程动作");
                return;
            }

            int amount = ReadInt(progressionAmount, 0);
            int threshold = ReadInt(progressionPressureThreshold, 0);
            string anchorId = progressionAnchorId?.text?.Trim() ??
                string.Empty;
            bool isQuery = action.StableId.StartsWith(
                "developer.query.", StringComparison.Ordinal);
            if (isQuery)
            {
                SetFeedback(QueryFeedback(action.StableId));
                return;
            }

            string argument = action.StableId.StartsWith(
                    "developer.void-debt.", StringComparison.Ordinal)
                ? selectedResourceId
                : action.StableId == "developer.rewind.read"
                    ? anchorId
                    : null;
            int commandAmount = action.StableId.StartsWith(
                    "developer.pressure.", StringComparison.Ordinal)
                ? threshold
                : amount;
            GrayboxDeveloperCommandResult3D result =
                modifier.ExecuteProgressionAction(
                    action.StableId,
                    argument,
                    commandAmount);
            SetFeedback(result.Message + ProgressionStatus());
        }

        private string QueryFeedback(string actionId)
        {
            GrayboxDeveloperProgressionQuery3D query =
                modifier.QueryProgression();
            if (query == null) return "文明进程服务尚未连接";
            switch (actionId)
            {
                case "developer.query.committed-ids":
                    return "已提交稳定记录：" + Join(query.CommittedIds);
                case "developer.query.thresholds":
                    return "已到达关注度阈值：" + Join(query.ReachedThresholds);
                case "developer.query.pressure-queue":
                    return "压力队列：" + Join(query.PressureQueue);
                case "developer.query.configuration-signature":
                    return "进度配置签名：" + query.ConfigurationSignature;
                case "developer.query.fate-domain-states":
                    return "命轨领域状态：" + Join(query.FateDomainStates);
                default:
                    return "文明进程查询未执行";
            }
        }

        private string ProgressionStatus()
        {
            GrayboxDeveloperProgressionQuery3D query =
                modifier.QueryProgression();
            if (query == null) return string.Empty;
            FormalFateDefinition fate = FormalFateCatalog.Find(query.FateId);
            string fateName = fate == null ? "未选择命轨" :
                fate.DisplayName + " Lv." + query.FateLevel;
            return " 当前：关注度 " + query.Attention +
                "，文明 Lv." + query.CivilizationLevel +
                "，" + fateName;
        }

        private static int ReadInt(InputField input, int fallback)
        {
            return input != null && int.TryParse(input.text, out int value)
                ? value
                : fallback;
        }

        private static string Join<T>(System.Collections.Generic.IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0) return "无";
            var result = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                object value = values[index];
                result[index] = value?.ToString() ?? string.Empty;
            }
            return string.Join("、", result);
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
            FormalUiCanvasConfiguration3D.ApplyReadableFontSize(
                text,
                FormalUiLayoutProfile3D.Standard.FontDescription);
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
