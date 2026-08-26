using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxFateSelectionCard3D
    {
        internal GrayboxFateSelectionCard3D(FormalFateDefinition definition)
        {
            FateId = definition.Id.Value;
            DisplayName = definition.DisplayName;
            Brief = definition.Brief;
            LevelOneSummary = definition.LevelOneSummary;
            LevelTwoSummary = definition.LevelTwoSummary;
            CostSummary = definition.CostSummary;
        }

        public string FateId { get; }
        public string DisplayName { get; }
        public string Brief { get; }
        public string LevelOneSummary { get; }
        public string LevelTwoSummary { get; }
        public string CostSummary { get; }
    }

    public sealed class GrayboxFateSelectionView3D : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;

        private readonly List<Button> cardButtons = new List<Button>();
        private GameObject fallbackCanvasObject;
        private GameObject modalRoot;
        private GameObject confirmationRoot;
        private Button confirmButton;
        private Button cancelButton;
        private IReadOnlyList<GrayboxFateSelectionCard3D> cards =
            Array.Empty<GrayboxFateSelectionCard3D>();

        public IReadOnlyList<GrayboxFateSelectionCard3D> Cards => cards;
        public bool IsOpen { get; private set; }
        public string PendingFateId { get; private set; } = string.Empty;
        public bool IsConfirmationOpen { get; private set; }
        public string SelectedStatusText { get; private set; } = string.Empty;

        public event Action<string> CardSelected;
        public event Action ConfirmRequested;
        public event Action CancelRequested;

        public void Configure(Canvas configuredCanvas)
        {
            canvas = configuredCanvas ??
                throw new ArgumentNullException(nameof(configuredCanvas));
            RebuildUi();
        }

        public void ApplyCards(IReadOnlyList<FormalFateDefinition> definitions)
        {
            var next = new GrayboxFateSelectionCard3D[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
                next[index] = new GrayboxFateSelectionCard3D(definitions[index]);
            cards = Array.AsReadOnly(next);
            RebuildUi();
        }

        public void Open()
        {
            EnsureUi();
            IsOpen = true;
            modalRoot.SetActive(true);
        }

        public void Close()
        {
            EventSystem current = EventSystem.current;
            GameObject selected = current?.currentSelectedGameObject;
            if (selected != null && modalRoot != null &&
                selected.transform.IsChildOf(modalRoot.transform))
            {
                current.SetSelectedGameObject(null);
            }
            IsOpen = false;
            IsConfirmationOpen = false;
            PendingFateId = string.Empty;
            if (confirmationRoot != null) confirmationRoot.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        public void ShowConfirmation(string fateId)
        {
            EnsureUi();
            PendingFateId = fateId ?? string.Empty;
            IsConfirmationOpen = true;
            confirmationRoot.SetActive(true);
        }

        public void CancelConfirmation()
        {
            PendingFateId = string.Empty;
            IsConfirmationOpen = false;
            if (confirmationRoot != null) confirmationRoot.SetActive(false);
        }

        public void SetSelectedStatus(string value)
        {
            SelectedStatusText = value ?? string.Empty;
        }

        private void EnsureUi()
        {
            EnsureCanvas();
            if (modalRoot != null) return;
            BuildUi();
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) return;
            fallbackCanvasObject = new GameObject(
                "FateSelection.FallbackCanvas",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            fallbackCanvasObject.transform.SetParent(transform, false);
            canvas = fallbackCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
        }

        private void RebuildUi()
        {
            TeardownUi();
            EnsureUi();
        }

        private void BuildUi()
        {
            RectTransform modal = CreateRect(
                canvas.transform, "FateSelection.Modal");
            Stretch(modal);
            Image blocker = modal.gameObject.AddComponent<Image>();
            blocker.color = new Color(.01f, .015f, .02f, .94f);
            blocker.raycastTarget = true;
            modalRoot = modal.gameObject;

            for (var index = 0; index < cards.Count; index++)
            {
                GrayboxFateSelectionCard3D card = cards[index];
                RectTransform rect = CreateRect(
                    modal,
                    "FateSelection.Card." + card.FateId);
                rect.anchorMin = new Vector2(.08f + index * .31f, .2f);
                rect.anchorMax = new Vector2(.34f + index * .31f, .82f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                Image image = rect.gameObject.AddComponent<Image>();
                image.color = new Color(.07f, .11f, .14f, .98f);
                Button button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                string fateId = card.FateId;
                button.onClick.AddListener(() => CardSelected?.Invoke(fateId));
                cardButtons.Add(button);
                Text text = CreateText(rect, "Copy", 17);
                text.text = card.DisplayName + "\n\n" + card.Brief +
                    "\n\nLv.1  " + card.LevelOneSummary +
                    "\n\nLv.2  " + card.LevelTwoSummary +
                    "\n\n代价  " + card.CostSummary;
            }

            RectTransform confirmation = CreateRect(
                modal, "FateSelection.Confirmation");
            confirmation.anchorMin = new Vector2(.34f, .36f);
            confirmation.anchorMax = new Vector2(.66f, .64f);
            confirmation.offsetMin = Vector2.zero;
            confirmation.offsetMax = Vector2.zero;
            Image confirmationImage =
                confirmation.gameObject.AddComponent<Image>();
            confirmationImage.color = new Color(.12f, .08f, .08f, .99f);
            confirmationRoot = confirmation.gameObject;

            confirmButton = CreateButton(
                confirmation, "FateSelection.Confirm", .08f, .48f, "确认命轨");
            cancelButton = CreateButton(
                confirmation, "FateSelection.Cancel", .52f, .92f, "返回");
            confirmButton.onClick.AddListener(() => ConfirmRequested?.Invoke());
            cancelButton.onClick.AddListener(() => CancelRequested?.Invoke());
            confirmationRoot.SetActive(false);
            modalRoot.SetActive(IsOpen);
        }

        private Button CreateButton(
            Transform parent, string name, float minX, float maxX, string label)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = new Vector2(minX, .12f);
            rect.anchorMax = new Vector2(maxX, .42f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.2f, .34f, .42f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText(rect, "Label", 16).text = label;
            return button;
        }

        private void TeardownUi()
        {
            for (var index = 0; index < cardButtons.Count; index++)
                cardButtons[index]?.onClick.RemoveAllListeners();
            cardButtons.Clear();
            confirmButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
            if (modalRoot != null) DestroyObject(modalRoot);
            modalRoot = null;
            confirmationRoot = null;
            confirmButton = null;
            cancelButton = null;
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

        private static Text CreateText(Transform parent, string name, int size)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            rect.offsetMin = new Vector2(14f, 14f);
            rect.offsetMax = new Vector2(-14f, -14f);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
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
