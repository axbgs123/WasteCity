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
        private Text confirmationLabel;
        private IReadOnlyList<string> rewindSlots = Array.Empty<string>();
        private IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D>
            rewindSlotStates =
                Array.Empty<GrayboxRewindAnchorSlotPresentation3D>();

        public bool IsOpen { get; private set; }
        public string SelectedFateText { get; private set; } = string.Empty;
        public string SelectedFateId { get; private set; } = string.Empty;
        public string RuleText { get; private set; } = string.Empty;
        public string CostText { get; private set; } = string.Empty;
        public string LevelText { get; private set; } = string.Empty;
        public string StatusText { get; private set; } = string.Empty;
        public string ActionText { get; private set; } = string.Empty;
        public bool GenericActionVisible { get; private set; }
        public IReadOnlyList<string> RewindAnchorSlotTexts => rewindSlots;
        public bool RewindLevelTwoSlotsVisible { get; private set; }
        public string SelectedRewindAnchorId { get; private set; } =
            string.Empty;
        public bool RewindCommandsVisible { get; private set; }
        public bool IsReadConfirmationOpen { get; private set; }
        public bool IsFateActionConfirmationOpen { get; private set; }
        public string ActionFeedbackText { get; private set; } = string.Empty;
        public int RenderCount { get; private set; }

        public event Action CreateRewindAnchorRequested;
        public event Action ReadRewindAnchorRequested;
        public event Action<string> ReadRewindAnchorByIdRequested;
        public event Action ClearRewindAnchorRequested;
        public event Action<string> FateActionRequested;
        public event Action OpenRequested;

        public void Configure(Canvas value)
        {
            canvas = value ?? throw new ArgumentNullException(nameof(value));
            RebuildUi();
        }

        public void Apply(
            bool open,
            GrayboxFateOperationPresentation3D presentation,
            IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D> anchors,
            bool showRewindCommands)
        {
            IsOpen = open;
            presentation = presentation ??
                new GrayboxFateOperationPresentation3D(
                    string.Empty,
                    string.Empty,
                    "尚未选择命轨",
                    "无",
                    "未激活",
                    "当前没有可显示的命轨状态",
                    "无可用动作",
                    false);
            SelectedFateId = presentation.FateId;
            SelectedFateText = presentation.TitleText;
            RuleText = presentation.RuleText;
            CostText = presentation.CostText;
            LevelText = presentation.LevelText;
            StatusText = presentation.StatusText;
            ActionText = presentation.ActionText;
            GenericActionVisible = presentation.GenericActionAvailable;
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
            SetCommandVisibility(
                showRewindCommands,
                presentation.GenericActionAvailable);
            RefreshSlotButtons();
        }

        public bool RequestFateAction()
        {
            if (!GenericActionVisible ||
                string.IsNullOrEmpty(SelectedFateId)) return false;
            EnsureUi();
            ActionFeedbackText = string.Empty;
            IsReadConfirmationOpen = false;
            IsFateActionConfirmationOpen = true;
            if (confirmationLabel != null)
                confirmationLabel.text = "确认" + ActionText;
            confirmation.SetActive(true);
            return true;
        }

        public void ConfirmFateAction()
        {
            if (!IsFateActionConfirmationOpen) return;
            IsFateActionConfirmationOpen = false;
            confirmation.SetActive(false);
            FateActionRequested?.Invoke(SelectedFateId);
        }

        public void ReportActionResult(bool succeeded, string feedback)
        {
            ActionFeedbackText = string.IsNullOrWhiteSpace(feedback)
                ? (succeeded ? "命轨动作已执行" : "命轨动作未执行")
                : feedback;
            if (detailsText != null)
                detailsText.text = BuildDetailsText();
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
            IsFateActionConfirmationOpen = false;
            IsReadConfirmationOpen = true;
            if (confirmationLabel != null)
                confirmationLabel.text = "确认读取";
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
            IsFateActionConfirmationOpen = false;
            if (confirmation != null) confirmation.SetActive(false);
        }

        private void ConfirmPendingAction()
        {
            if (IsReadConfirmationOpen)
                ConfirmRead();
            else if (IsFateActionConfirmationOpen)
                ConfirmFateAction();
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
        private Button genericActionButton;

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
            genericActionButton = Button(
                root,
                "FateOperations.GenericAction",
                .41f,
                "执行命轨动作",
                () => RequestFateAction());
            RectTransform actionRect =
                genericActionButton.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(.35f, .12f);
            actionRect.anchorMax = new Vector2(.65f, .2f);

            RectTransform confirm = Rect(root, "FateOperations.Confirmation");
            confirm.anchorMin = new Vector2(.35f, .38f);
            confirm.anchorMax = new Vector2(.65f, .62f);
            confirm.offsetMin = Vector2.zero;
            confirm.offsetMax = Vector2.zero;
            Image image = confirm.gameObject.AddComponent<Image>();
            image.color = new Color(.15f, .07f, .07f, 1f);
            Button confirmButton = Button(
                confirm,
                "FateOperations.Confirm",
                .18f,
                "确认读取",
                ConfirmPendingAction);
            confirmationLabel =
                confirmButton.GetComponentInChildren<Text>();
            confirmation = confirm.gameObject;
            confirmation.SetActive(false);
            modal.SetActive(IsOpen);
        }

        private void SetCommandVisibility(
            bool rewindVisible,
            bool genericVisible)
        {
            createButton.gameObject.SetActive(rewindVisible);
            readButton.gameObject.SetActive(rewindVisible);
            clearButton.gameObject.SetActive(rewindVisible);
            slotOneButton.gameObject.SetActive(
                rewindVisible && RewindLevelTwoSlotsVisible);
            slotTwoButton.gameObject.SetActive(
                rewindVisible && RewindLevelTwoSlotsVisible);
            anchorStatus.gameObject.SetActive(rewindVisible);
            genericActionButton.gameObject.SetActive(genericVisible);
            Text label = genericActionButton.GetComponentInChildren<Text>();
            if (label != null) label.text = ActionText;
        }

        private string BuildDetailsText()
        {
            var lines = new List<string>
            {
                SelectedFateText,
                "规则：" + RuleText,
                "代价：" + CostText,
                "等级：" + LevelText,
                "状态：" + StatusText,
                "动作：" + ActionText,
            };
            if (!string.IsNullOrWhiteSpace(ActionFeedbackText))
                lines.Add("反馈：" + ActionFeedbackText);
            if (RewindCommandsVisible) lines.AddRange(rewindSlots);
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
            genericActionButton?.onClick.RemoveAllListeners();
            if (modal != null) DestroyObject(modal);
            if (fallbackCanvas != null) DestroyObject(fallbackCanvas);
            CreateRewindAnchorRequested = null;
            ReadRewindAnchorRequested = null;
            ReadRewindAnchorByIdRequested = null;
            ClearRewindAnchorRequested = null;
            FateActionRequested = null;
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
