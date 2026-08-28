using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxCivilizationExpansionPage3D
    {
        Army,
        World,
        Politics,
    }

    public sealed class GrayboxCivilizationExpansionPresentation3D
    {
        public GrayboxCivilizationExpansionPresentation3D(
            string heading,
            string summary,
            string details,
            string primaryLabel,
            bool primaryEnabled,
            string secondaryLabel,
            bool secondaryEnabled,
            string tertiaryLabel,
            bool tertiaryEnabled)
        {
            Heading = heading ?? string.Empty;
            Summary = summary ?? string.Empty;
            Details = details ?? string.Empty;
            PrimaryLabel = primaryLabel ?? string.Empty;
            PrimaryEnabled = primaryEnabled;
            SecondaryLabel = secondaryLabel ?? string.Empty;
            SecondaryEnabled = secondaryEnabled;
            TertiaryLabel = tertiaryLabel ?? string.Empty;
            TertiaryEnabled = tertiaryEnabled;
        }

        public string Heading { get; }
        public string Summary { get; }
        public string Details { get; }
        public string PrimaryLabel { get; }
        public bool PrimaryEnabled { get; }
        public string SecondaryLabel { get; }
        public bool SecondaryEnabled { get; }
        public string TertiaryLabel { get; }
        public bool TertiaryEnabled { get; }
    }

    public sealed class GrayboxCivilizationExpansionView3D : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;

        private GameObject fallbackCanvasObject;
        private GameObject panelRoot;
        private Text headingText;
        private Text summaryText;
        private Text detailsText;
        private Text primaryLabel;
        private Text secondaryLabel;
        private Text tertiaryLabel;

        public bool IsOpen { get; private set; }
        public GrayboxCivilizationExpansionPage3D Page { get; private set; }
        public Button PrimaryButton { get; private set; }
        public Button SecondaryButton { get; private set; }
        public Button TertiaryButton { get; private set; }
        public Text HeadingText => headingText;
        public Text SummaryText => summaryText;
        public Text DetailsText => detailsText;

        public event Action PrimaryRequested;
        public event Action SecondaryRequested;
        public event Action TertiaryRequested;
        public event Action<GrayboxCivilizationExpansionPage3D> PageChanged;

        public void Configure(Canvas configuredCanvas)
        {
            canvas = configuredCanvas ??
                throw new ArgumentNullException(nameof(configuredCanvas));
            RebuildUi();
        }

        public void Toggle(GrayboxCivilizationExpansionPage3D page)
        {
            EnsureUi();
            if (IsOpen && Page == page)
            {
                Close();
                return;
            }
            Page = page;
            IsOpen = true;
            panelRoot.SetActive(true);
            PageChanged?.Invoke(Page);
        }

        public void Open(GrayboxCivilizationExpansionPage3D page)
        {
            EnsureUi();
            Page = page;
            IsOpen = true;
            panelRoot.SetActive(true);
            PageChanged?.Invoke(Page);
        }

        public void Close()
        {
            ClearSelectedObject();
            IsOpen = false;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Apply(GrayboxCivilizationExpansionPresentation3D value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            EnsureUi();
            headingText.text = value.Heading;
            summaryText.text = value.Summary;
            detailsText.text = value.Details;
            ApplyButton(
                PrimaryButton, primaryLabel,
                value.PrimaryLabel, value.PrimaryEnabled);
            ApplyButton(
                SecondaryButton, secondaryLabel,
                value.SecondaryLabel, value.SecondaryEnabled);
            ApplyButton(
                TertiaryButton, tertiaryLabel,
                value.TertiaryLabel, value.TertiaryEnabled);
        }

        private void EnsureUi()
        {
            EnsureCanvas();
            if (panelRoot == null) BuildUi();
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) return;
            fallbackCanvasObject = new GameObject(
                "CivilizationExpansion.FallbackCanvas",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            fallbackCanvasObject.transform.SetParent(transform, false);
            canvas = fallbackCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;
            FormalUiCanvasConfiguration3D.Apply(canvas, 510);
        }

        private void RebuildUi()
        {
            TeardownUi();
            EnsureUi();
        }

        private void BuildUi()
        {
            RectTransform blocker = CreateRect(
                canvas.transform, "CivilizationExpansion.Root");
            Stretch(blocker);
            Image shade = blocker.gameObject.AddComponent<Image>();
            shade.color = new Color(.015f, .025f, .035f, .84f);
            shade.raycastTarget = true;
            panelRoot = blocker.gameObject;

            RectTransform panel = CreateRect(
                blocker, "CivilizationExpansion.Panel");
            SetAnchors(panel, .13f, .10f, .87f, .90f);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(.055f, .085f, .105f, .99f);

            RectTransform accent = CreateRect(
                panel, "CivilizationExpansion.Accent");
            SetAnchors(accent, 0f, .965f, 1f, 1f);
            accent.gameObject.AddComponent<Image>().color =
                new Color(.24f, .72f, .72f, 1f);

            headingText = CreateText(
                panel, "Heading", 28, TextAnchor.MiddleLeft,
                .055f, .82f, .945f, .95f);
            headingText.color = new Color(.83f, .95f, .92f, 1f);
            summaryText = CreateText(
                panel, "Summary", 18, TextAnchor.UpperLeft,
                .055f, .55f, .40f, .80f);
            summaryText.color = new Color(.72f, .88f, .88f, 1f);
            AddBackdrop(summaryText.transform.parent as RectTransform,
                new Color(.075f, .125f, .145f, 1f));
            detailsText = CreateText(
                panel, "Details", 17, TextAnchor.UpperLeft,
                .425f, .25f, .945f, .80f);
            detailsText.color = new Color(.90f, .92f, .86f, 1f);
            AddBackdrop(detailsText.transform.parent as RectTransform,
                new Color(.09f, .105f, .125f, 1f));

            PrimaryButton = CreateButton(
                panel, "Primary", .055f, .40f, .40f, .52f,
                out primaryLabel);
            SecondaryButton = CreateButton(
                panel, "Secondary", .055f, .26f, .40f, .38f,
                out secondaryLabel);
            TertiaryButton = CreateButton(
                panel, "Tertiary", .055f, .12f, .40f, .24f,
                out tertiaryLabel);
            PrimaryButton.onClick.AddListener(
                () => PrimaryRequested?.Invoke());
            SecondaryButton.onClick.AddListener(
                () => SecondaryRequested?.Invoke());
            TertiaryButton.onClick.AddListener(
                () => TertiaryRequested?.Invoke());

            Text hint = CreateText(
                panel, "Hint", 15, TextAnchor.MiddleRight,
                .425f, .10f, .945f, .21f);
            hint.text = "M 军队  ·  N 世界  ·  P 政务  ·  Esc 关闭";
            hint.color = new Color(.56f, .68f, .70f, 1f);
            panelRoot.SetActive(IsOpen);
        }

        private static void ApplyButton(
            Button button,
            Text label,
            string text,
            bool enabled)
        {
            label.text = text;
            button.interactable = enabled;
            button.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            float minX,
            float minY,
            float maxX,
            float maxY,
            out Text label)
        {
            RectTransform rect = CreateRect(parent, name);
            SetAnchors(rect, minX, minY, maxX, maxY);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.17f, .39f, .43f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = CreateText(
                rect, "Label", 17, TextAnchor.MiddleCenter,
                0f, 0f, 1f, 1f);
            return button;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            int size,
            TextAnchor alignment,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            RectTransform container = CreateRect(parent, name);
            SetAnchors(container, minX, minY, maxX, maxY);
            RectTransform rect = CreateRect(container, "Text");
            Stretch(rect);
            rect.offsetMin = new Vector2(16f, 12f);
            rect.offsetMax = new Vector2(-16f, -12f);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void AddBackdrop(RectTransform rect, Color color)
        {
            if (rect == null) return;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.transform.SetAsFirstSibling();
        }

        private void ClearSelectedObject()
        {
            EventSystem current = EventSystem.current;
            GameObject selected = current?.currentSelectedGameObject;
            if (selected != null && panelRoot != null &&
                selected.transform.IsChildOf(panelRoot.transform))
            {
                current.SetSelectedGameObject(null);
            }
        }

        private void TeardownUi()
        {
            PrimaryButton?.onClick.RemoveAllListeners();
            SecondaryButton?.onClick.RemoveAllListeners();
            TertiaryButton?.onClick.RemoveAllListeners();
            if (panelRoot != null) DestroyObject(panelRoot);
            panelRoot = null;
            headingText = null;
            summaryText = null;
            detailsText = null;
            PrimaryButton = null;
            SecondaryButton = null;
            TertiaryButton = null;
            primaryLabel = null;
            secondaryLabel = null;
            tertiaryLabel = null;
        }

        private void OnDestroy()
        {
            TeardownUi();
            if (fallbackCanvasObject != null)
                DestroyObject(fallbackCanvasObject);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var value = new GameObject(name, typeof(RectTransform));
            RectTransform rect = value.GetComponent<RectTransform>();
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

        private static void SetAnchors(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
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
