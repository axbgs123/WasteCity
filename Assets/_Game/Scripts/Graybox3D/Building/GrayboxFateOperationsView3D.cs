using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxFateOperationsView3D : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        private GameObject fallbackCanvas;
        private GameObject modal;
        private GameObject confirmation;
        private Text detailsText;
        private Text anchorStatus;
        private IReadOnlyList<string> pocketFlagships = Array.Empty<string>();
        private IReadOnlyList<string> voidDebts = Array.Empty<string>();
        private IReadOnlyList<string> rewindSlots = Array.Empty<string>();
        private IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D>
            rewindSlotStates =
                Array.Empty<GrayboxRewindAnchorSlotPresentation3D>();

        public bool IsOpen { get; private set; }
        public string SelectedFateText { get; private set; } = string.Empty;
        public IReadOnlyList<string> PocketFlagshipTexts => pocketFlagships;
        public string PocketCollapseStatusText { get; private set; } = string.Empty;
        public IReadOnlyList<string> VoidDebtResourceTexts => voidDebts;
        public string VoidDebtTotalText { get; private set; } = string.Empty;
        public string VoidDebtNextSettlementText { get; private set; } = string.Empty;
        public IReadOnlyList<string> RewindAnchorSlotTexts => rewindSlots;
        public bool RewindLevelTwoSlotsVisible { get; private set; }
        public string SelectedRewindAnchorId { get; private set; } =
            string.Empty;
        public bool RewindCommandsVisible { get; private set; }
        public bool IsReadConfirmationOpen { get; private set; }
        public int RenderCount { get; private set; }

        public event Action CreateRewindAnchorRequested;
        public event Action ReadRewindAnchorRequested;
        public event Action<string> ReadRewindAnchorByIdRequested;
        public event Action ClearRewindAnchorRequested;
        public event Action OpenRequested;

        public void Configure(Canvas value)
        {
            canvas = value ?? throw new ArgumentNullException(nameof(value));
            RebuildUi();
        }

        public void Apply(
            bool open,
            string selectedFate,
            IReadOnlyList<string> pocket,
            string collapse,
            IReadOnlyList<string> debts,
            string debtTotal,
            string debtNext,
            IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D> anchors,
            bool showRewindCommands)
        {
            IsOpen = open;
            SelectedFateText = selectedFate ?? string.Empty;
            pocketFlagships = Copy(pocket);
            PocketCollapseStatusText = collapse ?? string.Empty;
            voidDebts = Copy(debts);
            VoidDebtTotalText = debtTotal ?? string.Empty;
            VoidDebtNextSettlementText = debtNext ?? string.Empty;
            rewindSlotStates = CopySlots(anchors);
            rewindSlots = SlotTexts(rewindSlotStates);
            RewindLevelTwoSlotsVisible = rewindSlotStates.Count == 2;
            if (!ContainsOccupied(SelectedRewindAnchorId))
                SelectedRewindAnchorId = FirstOccupiedAnchorId();
            RewindCommandsVisible = showRewindCommands;
            unchecked { RenderCount++; }
            EnsureUi();
            modal.SetActive(IsOpen);
            detailsText.text = BuildDetailsText();
            anchorStatus.text = rewindSlots.Count == 0
                ? "锚点槽：空"
                : "锚点槽：" + rewindSlots.Count;
            SetCommandVisibility(showRewindCommands);
            RefreshSlotButtons();
        }

        public void Close()
        {
            ClearFocus();
            IsOpen = false;
            CancelReadConfirmation();
            if (modal != null) modal.SetActive(false);
        }

        public void Open()
        {
            OpenRequested?.Invoke();
            IsOpen = true;
            EnsureUi();
            modal.SetActive(true);
        }

        public void BeginReadConfirmation()
        {
            EnsureUi();
            IsReadConfirmationOpen = true;
            confirmation.SetActive(true);
        }

        public void ConfirmRead()
        {
            if (!IsReadConfirmationOpen) return;
            IsReadConfirmationOpen = false;
            confirmation.SetActive(false);
            if (string.IsNullOrEmpty(SelectedRewindAnchorId)) return;
            ReadRewindAnchorByIdRequested?.Invoke(SelectedRewindAnchorId);
            if (!RewindLevelTwoSlotsVisible)
                ReadRewindAnchorRequested?.Invoke();
        }

        public bool TrySelectRewindAnchor(string anchorId)
        {
            if (!ContainsOccupied(anchorId)) return false;
            SelectedRewindAnchorId = anchorId;
            RefreshSlotButtons();
            return true;
        }

        public void CancelReadConfirmation()
        {
            ClearFocus();
            IsReadConfirmationOpen = false;
            if (confirmation != null) confirmation.SetActive(false);
        }

        private void ClearFocus()
        {
            EventSystem current = EventSystem.current;
            GameObject selected = current?.currentSelectedGameObject;
            if (selected != null && modal != null &&
                selected.transform.IsChildOf(modal.transform))
                current.SetSelectedGameObject(null);
        }

        private Button createButton;
        private Button readButton;
        private Button clearButton;
        private Button slotOneButton;
        private Button slotTwoButton;

        private void EnsureUi()
        {
            EnsureCanvas();
            if (modal != null) return;
            RectTransform root = Rect(canvas.transform, "FateOperations.Modal");
            Stretch(root);
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(.01f, .015f, .02f, .94f);
            blocker.raycastTarget = true;
            modal = root.gameObject;
            detailsText = TextLabel(root, "FateOperations.Details", 18);
            anchorStatus = TextLabel(root, "FateOperations.AnchorStatus", 16);
            anchorStatus.rectTransform.anchorMin = new Vector2(.2f, .16f);
            anchorStatus.rectTransform.anchorMax = new Vector2(.8f, .24f);
            createButton = Button(root, "FateOperations.CreateAnchor", .18f,
                "创建锚点", () => CreateRewindAnchorRequested?.Invoke());
            readButton = Button(root, "FateOperations.ReadAnchor", .42f,
                "读取锚点", BeginReadConfirmation);
            clearButton = Button(root, "FateOperations.ClearAnchors", .66f,
                "清除锚点", () => ClearRewindAnchorRequested?.Invoke());
            slotOneButton = SlotButton(
                root,
                "FateOperations.RewindSlot.1",
                .22f,
                GrayboxRewindAnchorService3D.StableAnchorId);
            slotTwoButton = SlotButton(
                root,
                "FateOperations.RewindSlot.2",
                .52f,
                GrayboxRewindAnchorService3D.SecondStableAnchorId);

            RectTransform confirm = Rect(root, "FateOperations.Confirmation");
            confirm.anchorMin = new Vector2(.35f, .38f);
            confirm.anchorMax = new Vector2(.65f, .62f);
            confirm.offsetMin = Vector2.zero;
            confirm.offsetMax = Vector2.zero;
            Image image = confirm.gameObject.AddComponent<Image>();
            image.color = new Color(.15f, .07f, .07f, 1f);
            Button(confirm, "FateOperations.Confirm", .18f,
                "确认读取", ConfirmRead);
            confirmation = confirm.gameObject;
            confirmation.SetActive(false);
            modal.SetActive(IsOpen);
        }

        private void SetCommandVisibility(bool visible)
        {
            createButton.gameObject.SetActive(visible);
            readButton.gameObject.SetActive(visible);
            clearButton.gameObject.SetActive(visible);
            slotOneButton.gameObject.SetActive(
                visible && RewindLevelTwoSlotsVisible);
            slotTwoButton.gameObject.SetActive(
                visible && RewindLevelTwoSlotsVisible);
        }

        private string BuildDetailsText()
        {
            var lines = new List<string> { SelectedFateText };
            lines.AddRange(pocketFlagships);
            if (!string.IsNullOrEmpty(PocketCollapseStatusText))
                lines.Add(PocketCollapseStatusText);
            lines.AddRange(voidDebts);
            if (!string.IsNullOrEmpty(VoidDebtTotalText)) lines.Add(VoidDebtTotalText);
            if (!string.IsNullOrEmpty(VoidDebtNextSettlementText)) lines.Add(VoidDebtNextSettlementText);
            lines.AddRange(rewindSlots);
            return string.Join("\n", lines);
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            fallbackCanvas = new GameObject("FateOperations.Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            fallbackCanvas.transform.SetParent(transform, false);
            canvas = fallbackCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;
        }

        private void RebuildUi()
        {
            if (modal != null) DestroyObject(modal);
            modal = null;
            EnsureUi();
        }

        private void OnDestroy()
        {
            createButton?.onClick.RemoveAllListeners();
            readButton?.onClick.RemoveAllListeners();
            clearButton?.onClick.RemoveAllListeners();
            slotOneButton?.onClick.RemoveAllListeners();
            slotTwoButton?.onClick.RemoveAllListeners();
            if (modal != null) DestroyObject(modal);
            if (fallbackCanvas != null) DestroyObject(fallbackCanvas);
            CreateRewindAnchorRequested = null;
            ReadRewindAnchorRequested = null;
            ReadRewindAnchorByIdRequested = null;
            ClearRewindAnchorRequested = null;
            OpenRequested = null;
        }

        private Button Button(
            Transform parent,
            string name,
            float x,
            string label,
            Action callback)
        {
            RectTransform rect = Rect(parent, name);
            rect.anchorMin = new Vector2(x, .06f);
            rect.anchorMax = new Vector2(x + .18f, .14f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.2f, .35f, .43f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => callback());
            Text text = TextLabel(rect, name + ".Label", 15);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
            return button;
        }

        private Button SlotButton(
            Transform parent,
            string name,
            float x,
            string anchorId)
        {
            RectTransform rect = Rect(parent, name);
            rect.anchorMin = new Vector2(x, .16f);
            rect.anchorMax = new Vector2(x + .26f, .23f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.16f, .25f, .31f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
                TrySelectRewindAnchor(anchorId));
            Text text = TextLabel(rect, name + ".Label", 14);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
            return button;
        }

        private void RefreshSlotButtons()
        {
            if (slotOneButton == null || slotTwoButton == null) return;
            ApplySlotButton(slotOneButton, 0);
            ApplySlotButton(slotTwoButton, 1);
            readButton.interactable =
                !string.IsNullOrEmpty(SelectedRewindAnchorId);
        }

        private void ApplySlotButton(Button button, int index)
        {
            bool occupied = index < rewindSlotStates.Count &&
                rewindSlotStates[index].IsOccupied;
            button.interactable = occupied;
            Text label = button.GetComponentInChildren<Text>();
            label.text = index < rewindSlotStates.Count
                ? rewindSlotStates[index].DisplayText
                : "槽位 " + (index + 1) + "  空";
            Image image = button.targetGraphic as Image;
            if (image != null)
                image.color = occupied && string.Equals(
                    rewindSlotStates[index].AnchorId,
                    SelectedRewindAnchorId,
                    StringComparison.Ordinal)
                        ? new Color(.22f, .5f, .62f, 1f)
                        : new Color(.16f, .25f, .31f, 1f);
        }

        private bool ContainsOccupied(string anchorId)
        {
            for (var index = 0; index < rewindSlotStates.Count; index++)
                if (rewindSlotStates[index].IsOccupied && string.Equals(
                        rewindSlotStates[index].AnchorId,
                        anchorId,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        private string FirstOccupiedAnchorId()
        {
            for (var index = 0; index < rewindSlotStates.Count; index++)
                if (rewindSlotStates[index].IsOccupied)
                    return rewindSlotStates[index].AnchorId;
            return string.Empty;
        }

        private static Text TextLabel(Transform parent, string name, int size)
        {
            RectTransform rect = Rect(parent, name);
            rect.anchorMin = new Vector2(.16f, .25f);
            rect.anchorMax = new Vector2(.84f, .9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(Transform parent, string name)
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

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source[index];
            return Array.AsReadOnly(result);
        }

        private static IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D>
            CopySlots(
                IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<GrayboxRewindAnchorSlotPresentation3D>();
            var result = new GrayboxRewindAnchorSlotPresentation3D[
                source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return Array.AsReadOnly(result);
        }

        private static IReadOnlyList<string> SlotTexts(
            IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index]?.DisplayText ?? string.Empty;
            return Array.AsReadOnly(result);
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
