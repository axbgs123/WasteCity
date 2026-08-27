using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxCivilizationAdvancementView3D : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;

        private GameObject fallbackCanvasObject;
        private GameObject modalRoot;
        private GameObject promptRoot;

        public bool IsOpen { get; private set; }
        public bool IsPromptVisible { get; private set; }
        public Text RequirementsText { get; private set; }
        public Text SummaryText { get; private set; }
        public Text FatePreviewText { get; private set; }
        public Text StageText { get; private set; }
        public Text HintText { get; private set; }
        public Button AdvanceButton { get; private set; }
        public Button ContinueButton { get; private set; }

        public event Action AdvanceRequested;
        public event Action ContinueRequested;

        public void Configure(Canvas configuredCanvas)
        {
            canvas = configuredCanvas ??
                throw new ArgumentNullException(nameof(configuredCanvas));
            RebuildUi();
        }

        public void Apply(
            GrayboxCivilizationAdvancementPresentation3D projection)
        {
            if (projection == null)
                throw new ArgumentNullException(nameof(projection));
            EnsureUi();
            RequirementsText.text = projection.RequirementsText;
            SummaryText.text = projection.SummaryText;
            FatePreviewText.text = projection.FatePreviewText;
            StageText.text = projection.StageText;
            HintText.text = projection.HintText;
            AdvanceButton.interactable = projection.CanAdvance;
            AdvanceButton.gameObject.SetActive(!projection.CanContinue);
            ContinueButton.gameObject.SetActive(projection.CanContinue);
            IsPromptVisible = projection.PromptVisible && !IsOpen;
            promptRoot.SetActive(IsPromptVisible);
            modalRoot.SetActive(IsOpen);
        }

        public void Open()
        {
            EnsureUi();
            IsOpen = true;
            IsPromptVisible = false;
            promptRoot.SetActive(false);
            modalRoot.SetActive(true);
        }

        public void Close()
        {
            EventSystem current = EventSystem.current;
            GameObject selected = current?.currentSelectedGameObject;
            if (selected != null && modalRoot != null &&
                selected.transform.IsChildOf(modalRoot.transform))
                current.SetSelectedGameObject(null);
            IsOpen = false;
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        private void EnsureUi()
        {
            EnsureCanvas();
            if (modalRoot == null) BuildUi();
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) return;
            fallbackCanvasObject = new GameObject(
                "CivilizationAdvancement.FallbackCanvas",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            fallbackCanvasObject.transform.SetParent(transform, false);
            canvas = fallbackCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;
        }

        private void RebuildUi()
        {
            TeardownUi();
            EnsureUi();
        }

        private void BuildUi()
        {
            RectTransform prompt = CreateRect(
                canvas.transform, "CivilizationAdvancement.Prompt");
            SetAnchors(prompt, .73f, .055f, .97f, .12f);
            Image promptImage = prompt.gameObject.AddComponent<Image>();
            promptImage.color = new Color(.055f, .10f, .13f, .96f);
            promptImage.raycastTarget = false;
            HintText = CreateText(prompt, "Hint", 16, TextAnchor.MiddleCenter);
            HintText.color = new Color(.55f, .92f, .92f, 1f);
            promptRoot = prompt.gameObject;

            RectTransform blocker = CreateRect(
                canvas.transform, "CivilizationAdvancement.Modal");
            Stretch(blocker);
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = new Color(.015f, .025f, .035f, .92f);
            blockerImage.raycastTarget = true;
            modalRoot = blocker.gameObject;

            RectTransform panel = CreateRect(
                blocker, "CivilizationAdvancement.Panel");
            SetAnchors(panel, .16f, .1f, .84f, .9f);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(.07f, .105f, .12f, .99f);

            RectTransform stage = CreateRect(
                panel, "CivilizationAdvancement.Stage");
            SetAnchors(stage, .065f, .80f, .935f, .94f);
            StageText = CreateText(
                stage, "Text", 22, TextAnchor.MiddleCenter);
            StageText.color = new Color(.82f, .92f, .88f, 1f);

            RectTransform requirements = CreateRect(
                panel, "CivilizationAdvancement.Requirements");
            SetAnchors(requirements, .065f, .21f, .455f, .78f);
            AddPanelImage(requirements, new Color(.09f, .14f, .15f, 1f));
            RequirementsText = CreateText(
                requirements, "Text", 18, TextAnchor.UpperLeft);
            RequirementsText.color = new Color(.79f, .90f, .83f, 1f);

            RectTransform rail = CreateRect(
                panel, "CivilizationAdvancement.AscentRail");
            SetAnchors(rail, .485f, .21f, .505f, .78f);
            AddPanelImage(rail, new Color(.25f, .76f, .78f, .9f));

            RectTransform summary = CreateRect(
                panel, "CivilizationAdvancement.Summary");
            SetAnchors(summary, .535f, .47f, .935f, .78f);
            AddPanelImage(summary, new Color(.10f, .135f, .17f, 1f));
            SummaryText = CreateText(
                summary, "Text", 19, TextAnchor.MiddleLeft);

            RectTransform fate = CreateRect(
                panel, "CivilizationAdvancement.FatePreview");
            SetAnchors(fate, .535f, .21f, .935f, .45f);
            AddPanelImage(fate, new Color(.13f, .105f, .16f, 1f));
            FatePreviewText = CreateText(
                fate, "Text", 17, TextAnchor.MiddleLeft);
            FatePreviewText.color = new Color(.91f, .82f, .96f, 1f);

            AdvanceButton = CreateButton(
                panel,
                "CivilizationAdvancement.Advance",
                .24f,
                .07f,
                .49f,
                .16f,
                "执行文明升阶");
            ContinueButton = CreateButton(
                panel,
                "CivilizationAdvancement.Continue",
                .51f,
                .07f,
                .76f,
                .16f,
                "确认结果并继续");
            AdvanceButton.onClick.AddListener(
                () => AdvanceRequested?.Invoke());
            ContinueButton.onClick.AddListener(
                () => ContinueRequested?.Invoke());
            ContinueButton.gameObject.SetActive(false);
            promptRoot.SetActive(IsPromptVisible);
            modalRoot.SetActive(IsOpen);
        }

        private static void AddPanelImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            float minX,
            float minY,
            float maxX,
            float maxY,
            string label)
        {
            RectTransform rect = CreateRect(parent, name);
            SetAnchors(rect, minX, minY, maxX, maxY);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.19f, .43f, .47f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText(rect, "Label", 17, TextAnchor.MiddleCenter).text = label;
            return button;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            int size,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            rect.offsetMin = new Vector2(16f, 12f);
            rect.offsetMax = new Vector2(-16f, -12f);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void TeardownUi()
        {
            AdvanceButton?.onClick.RemoveAllListeners();
            ContinueButton?.onClick.RemoveAllListeners();
            if (modalRoot != null) DestroyObject(modalRoot);
            if (promptRoot != null) DestroyObject(promptRoot);
            modalRoot = null;
            promptRoot = null;
            RequirementsText = null;
            SummaryText = null;
            FatePreviewText = null;
            StageText = null;
            HintText = null;
            AdvanceButton = null;
            ContinueButton = null;
        }

        private void OnDestroy()
        {
            TeardownUi();
            if (fallbackCanvasObject != null)
                DestroyObject(fallbackCanvasObject);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
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
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
