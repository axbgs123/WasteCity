using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        private GameObject panelRoot;
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
            if (this.session == session &&
                this.city == city &&
                this.presentation == presentation &&
                this.canvas == canvas)
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DisposeDevelopmentSurface();
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
            modifier = null;
            if (panelRoot == null)
                return;

            Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
                buttons[index].onClick.RemoveAllListeners();
            GameObject ownedPanel = panelRoot;
            panelRoot = null;
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
            root.sizeDelta = new Vector2(250f, 620f);
            Image background = panelRoot.AddComponent<Image>();
            background.color = new Color(.08f, .1f, .11f, .94f);
            var layout = panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 3f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateLabel(root, "Development Mode Label", "开发模式");
            InputField resourceId = CreateInput(
                root, "Resource Id", ResourceIds.Iron);
            CreateButton(root, "Resource +100", "资源 +100", () =>
                modifier.AddResource(resourceId.text, 100));
            CreateButton(root, "Resource +1000", "资源 +1000", () =>
                modifier.AddResource(resourceId.text, 1000));
            CreateButton(root, "Clear Resource", "资源清零", () =>
                modifier.ClearResource(resourceId.text));
            InputField resourceAmount = CreateInput(
                root, "Resource Amount", "0");
            CreateButton(root, "Set Resource", "设置资源", () =>
            {
                if (int.TryParse(resourceAmount.text, out int amount))
                    modifier.SetResource(resourceId.text, amount);
            });
            InputField populationAmount = CreateInput(
                root, "Population Amount", "200");
            CreateButton(root, "Set Population", "设置人口", () =>
            {
                if (int.TryParse(populationAmount.text, out int amount))
                    modifier.SetPopulation(amount);
            });

            InputField researchId = CreateInput(
                root,
                "Research Id",
                string.Empty);
            CreateButton(root, "Unlock Research", "解锁单项研究", () =>
                modifier.UnlockResearch(researchId.text));
            CreateButton(root, "Unlock Technology", "解锁科技路线", () =>
                modifier.UnlockRoute(ContentRoute.Technology));
            CreateButton(root, "Unlock Cultivation", "解锁修仙路线", () =>
                modifier.UnlockRoute(ContentRoute.Cultivation));
            CreateButton(root, "Unlock Biological Ascension", "解锁生物路线", () =>
                modifier.UnlockRoute(ContentRoute.BiologicalAscension));
            CreateButton(root, "Unlock Psionics", "解锁灵能路线", () =>
                modifier.UnlockRoute(ContentRoute.Psionics));
            CreateButton(root, "Unlock All", "解锁全部研究", () =>
                modifier.UnlockAllResearch());

            CreateButton(root, "Set Mobile", "切换 Mobile", () =>
                modifier.SetCityMode(CityMode.Mobile));
            CreateButton(root, "Set Fortress", "切换 Fortress", () =>
                modifier.SetCityMode(CityMode.Fortress));
            CreateButton(root, "Complete Transition", "完成形态转换", () =>
                modifier.CompleteCityTransition());
            CreateButton(root, "Multiplier 1x", "施工 1×", () =>
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Normal));
            CreateButton(root, "Multiplier 10x", "施工 10×", () =>
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast10));
            CreateButton(root, "Multiplier 100x", "施工 100×", () =>
                modifier.SetConstructionSpeed(
                    DevelopmentConstructionSpeed.Fast100));
            CreateButton(root, "Complete Construction", "立即完成施工", () =>
                modifier.CompleteAllConstruction());
            panelRoot.SetActive(false);
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
#endif
    }
}
