using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#endif

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperModifierBootstrap3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GrayboxDeveloperModifier3D modifier;
        private GameObject panelRoot;
#endif

        public static bool ResolveRuntimeAvailability(
            bool isEditor,
            bool isDevelopmentBuild)
        {
            return isEditor || isDevelopmentBuild;
        }

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation)
        {
            this.session = session;
            this.city = city;
            this.presentation = presentation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryCreateDevelopmentSurface();
#endif
        }

        public bool TryTogglePanel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (panelRoot == null) return false;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void TryCreateDevelopmentSurface()
        {
            if (panelRoot != null || session == null || city == null ||
                presentation == null)
                return;
            EnsureEventSystem();
            modifier = new GrayboxDeveloperModifier3D(
                session,
                city,
                presentation);
            CreatePanel();
        }

        private void CreatePanel()
        {
            panelRoot = new GameObject(
                "Graybox Developer Modifier",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            panelRoot.transform.SetParent(transform, false);
            Canvas canvas = panelRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
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
            CreateButton(root, "Select Resource", "选择资源", () =>
                modifier.SetCurrentResource(resourceId.text));
            CreateButton(root, "Resource +100", "资源 +100", () =>
                modifier.AddCurrentResource100());
            CreateButton(root, "Resource +1000", "资源 +1000", () =>
                modifier.AddCurrentResource1000());
            CreateButton(root, "Clear Resource", "资源清零", () =>
                modifier.ClearCurrentResource());
            InputField resourceAmount = CreateInput(
                root, "Resource Amount", "0");
            CreateButton(root, "Set Resource", "设置资源", () =>
            {
                if (int.TryParse(resourceAmount.text, out int amount))
                    modifier.SetCurrentResourceAmount(amount);
            });

            InputField researchId = CreateInput(root, "Research Id", string.Empty);
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
                modifier.CompleteDeploymentTransition());
            CreateButton(root, "Multiplier 1x", "施工 1×", () =>
                modifier.SetConstructionMultiplier(1f));
            CreateButton(root, "Multiplier 10x", "施工 10×", () =>
                modifier.SetConstructionMultiplier(10f));
            CreateButton(root, "Multiplier 100x", "施工 100×", () =>
                modifier.SetConstructionMultiplier(100f));
            CreateButton(root, "Complete Construction", "立即完成施工", () =>
                modifier.CompleteAllConstruction());
            panelRoot.SetActive(false);
        }

        private static EventSystem EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                EventSystem[] existing =
                    FindObjectsOfType<EventSystem>();
                if (existing.Length > 0)
                    eventSystem = existing[0];
            }

            if (eventSystem == null)
            {
                var root = new GameObject("Graybox Developer EventSystem");
                eventSystem = root.AddComponent<EventSystem>();
            }

            BaseInputModule module = eventSystem.GetComponent<
                BaseInputModule>();
            if (module == null)
                module = eventSystem.gameObject.AddComponent<
                    InputSystemUIInputModule>();
            if (!module.enabled)
                module.enabled = true;
            return eventSystem;
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
