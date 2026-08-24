using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Graybox3D.Usability
{
    public interface IGrayboxDevelopmentPanelControl3D
    {
        bool IsOpen { get; }
        void Close();
    }

    public sealed class GrayboxUsabilityInputCoordinator3D :
        MonoBehaviour,
        IGrayboxInputInterceptor
    {
        [SerializeField]
        private GrayboxBuildingInputRouter3D buildingInput;
        [SerializeField]
        private GrayboxSystemMenuController3D systemMenu;
        [SerializeField]
        private GrayboxDeveloperModifierBootstrap3D developer;
        [SerializeField]
        private GrayboxOperationsController3D operations;
        [SerializeField]
        private GrayboxDefenseController3D defense;
        [SerializeField]
        private GrayboxFormalSaveEntryController3D formalSaveEntry;

        private IGrayboxDevelopmentPanelControl3D developmentPanel;
        private readonly Dictionary<Keyboard, Action<char>>
            developmentTextInputBindings =
                new Dictionary<Keyboard, Action<char>>();
        private bool observesDevelopmentInputDevices;

        public uint BuildingInputInvocationCount { get; private set; }

        public void ConfigureFormalSaveEntry(
            GrayboxFormalSaveEntryController3D formalSaveEntry)
        {
            this.formalSaveEntry = formalSaveEntry;
        }

        public void Configure(
            GrayboxBuildingInputRouter3D buildingInput,
            GrayboxSystemMenuController3D systemMenu,
            GrayboxDeveloperModifierBootstrap3D developer)
        {
            Configure(buildingInput, systemMenu, developer, null);
        }

        public void Configure(
            GrayboxBuildingInputRouter3D buildingInput,
            GrayboxSystemMenuController3D systemMenu,
            GrayboxDeveloperModifierBootstrap3D developer,
            GrayboxOperationsController3D operations)
        {
            if (developer == null)
                throw new ArgumentNullException(nameof(developer));
            this.buildingInput = buildingInput ??
                throw new ArgumentNullException(nameof(buildingInput));
            this.systemMenu = systemMenu ??
                throw new ArgumentNullException(nameof(systemMenu));
            this.developer = developer;
            this.operations = operations;
            developmentPanel = new DevelopmentPanelAdapter(developer);
        }

        public void Configure(
            GrayboxBuildingInputRouter3D buildingInput,
            GrayboxSystemMenuController3D systemMenu,
            GrayboxDeveloperModifierBootstrap3D developer,
            GrayboxOperationsController3D operations,
            GrayboxDefenseController3D defense)
        {
            Configure(
                buildingInput,
                systemMenu,
                developer,
                operations);
            this.defense = defense;
            buildingInput.ConfigureDefense(defense);
        }

        public void Configure(
            GrayboxBuildingInputRouter3D buildingInput,
            GrayboxSystemMenuController3D systemMenu,
            IGrayboxDevelopmentPanelControl3D developmentPanel)
        {
            this.buildingInput = buildingInput ??
                throw new ArgumentNullException(nameof(buildingInput));
            this.systemMenu = systemMenu ??
                throw new ArgumentNullException(nameof(systemMenu));
            developer = null;
            operations = null;
            this.developmentPanel = developmentPanel;
        }

        public GrayboxInputSuppression ProcessCurrentInput()
        {
            if (formalSaveEntry != null &&
                formalSaveEntry.BlocksGameplayInput)
            {
                return SuppressAll();
            }

            Keyboard keyboard = Keyboard.current;
            bool escapePressed = keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame;

            if (systemMenu != null && systemMenu.IsOpen)
            {
                if (escapePressed)
                    ProcessMenuEscape();
                return SuppressAll();
            }

            EnsureDevelopmentPanelAdapter();
            if (developmentPanel != null && developmentPanel.IsOpen)
            {
                if (HasActiveTextInputFocus())
                {
                    if (escapePressed)
                        ClearActiveTextInputFocus();
                    return SuppressAll();
                }

                bool closeDevelopmentPressed = escapePressed ||
                    (keyboard != null &&
                     keyboard.digit0Key.wasPressedThisFrame);
                if (closeDevelopmentPressed)
                    developmentPanel.Close();
                return SuppressAll();
            }

            bool tacticalPausePressed = keyboard != null &&
                keyboard.spaceKey.wasPressedThisFrame;
            if (tacticalPausePressed &&
                systemMenu != null &&
                !HasActiveTextInputFocus())
            {
                systemMenu.ToggleTacticalPause();
                return SuppressAll();
            }

            if (operations != null && operations.IsResearchOpen)
            {
                if (operations.HasResearchTextInputFocus)
                {
                    if (escapePressed)
                        operations.ConsumeFocusedResearchEscape();
                    return SuppressAll();
                }

                bool researchClosePressed = keyboard != null &&
                    keyboard.tKey.wasPressedThisFrame;
                if (escapePressed || researchClosePressed)
                {
                    operations.ClosePanels();
                    return SuppressAll();
                }
                if (keyboard != null &&
                    keyboard.homeKey.wasPressedThisFrame)
                {
                    operations.FitResearchTree();
                }
                return SuppressAll();
            }

            if (buildingInput != null &&
                buildingInput.HasKeyboardFocus &&
                (operations == null || !operations.IsAnyPanelOpen))
            {
                unchecked { BuildingInputInvocationCount++; }
                return buildingInput.ProcessCurrentInput();
            }

            bool inventoryPressed = keyboard != null &&
                keyboard.eKey.wasPressedThisFrame;
            bool researchPressed = keyboard != null &&
                keyboard.tKey.wasPressedThisFrame;
            bool buildingPressed = keyboard != null &&
                keyboard.bKey.wasPressedThisFrame;

            if ((inventoryPressed || researchPressed || buildingPressed) &&
                defense != null && defense.HasSelection)
            {
                defense.CloseSelection();
            }

            if (escapePressed &&
                defense != null && defense.HasSelection)
            {
                defense.CloseSelection();
                return SuppressAll();
            }

            if (operations != null &&
                (inventoryPressed || researchPressed))
            {
                if (inventoryPressed &&
                    buildingInput != null &&
                    buildingInput.AllowsInventoryDuringEvacuation)
                {
                    EnsureDevelopmentPanelAdapter();
                    if (developmentPanel != null && developmentPanel.IsOpen)
                        developmentPanel.Close();
                    operations.ToggleInventory();
                }
                else if (buildingInput == null ||
                    buildingInput.TryCloseForOperations())
                {
                    EnsureDevelopmentPanelAdapter();
                    if (developmentPanel != null && developmentPanel.IsOpen)
                        developmentPanel.Close();
                    if (inventoryPressed)
                        operations.ToggleInventory();
                    else
                        operations.ToggleResearch();
                }
                return SuppressAll();
            }

            if (operations != null && operations.IsAnyPanelOpen)
            {
                if (escapePressed)
                {
                    operations.ClosePanels();
                    return SuppressAll();
                }
                if (buildingInput != null &&
                    buildingInput.AllowsInventoryDuringEvacuation)
                    return SuppressAll();
                if (buildingPressed)
                    operations.ClosePanels();
                else
                    return SuppressAll();
            }

            GrayboxInputSuppression buildingSuppression = default;
            if (buildingInput != null)
            {
                unchecked { BuildingInputInvocationCount++; }
                buildingSuppression = buildingInput.ProcessCurrentInput();
            }

            bool speedOnePressed = keyboard != null &&
                keyboard.digit1Key.wasPressedThisFrame;
            bool speedTwoPressed = keyboard != null &&
                keyboard.digit2Key.wasPressedThisFrame;
            if (!buildingSuppression.Destination &&
                systemMenu != null &&
                !HasActiveTextInputFocus() &&
                (speedOnePressed || speedTwoPressed))
            {
                systemMenu.RequestSpeed(speedTwoPressed ? 2 : 1);
                return SuppressAll();
            }

            EnsureDevelopmentPanelAdapter();
            if (developmentPanel != null && developmentPanel.IsOpen)
                return SuppressAll();

            if (!escapePressed ||
                buildingInput == null ||
                (buildingInput.LastEscapeConsumed &&
                 !buildingInput.LastEscapeRequestsSystemMenu) ||
                systemMenu == null)
                return buildingSuppression;

            EnsureDevelopmentPanelAdapter();
            if (developmentPanel != null && developmentPanel.IsOpen)
                developmentPanel.Close();
            systemMenu.Open();
            return SuppressAll();
        }

        private void Awake()
        {
            EnsureDevelopmentPanelAdapter();
            BindDevelopmentTextInput();
        }

        private void OnEnable()
        {
            BindDevelopmentTextInput();
        }

        private void OnDisable()
        {
            UnbindDevelopmentTextInput();
            CloseOwnedMenu();
        }

        private void OnDestroy()
        {
            UnbindDevelopmentTextInput();
            CloseOwnedMenu();
            buildingInput = null;
            systemMenu = null;
            developer = null;
            operations = null;
            defense = null;
            formalSaveEntry = null;
            developmentPanel = null;
        }

        private void ProcessMenuEscape()
        {
            switch (systemMenu.Page)
            {
                case GrayboxSystemMenuPage3D.Settings:
                    systemMenu.CancelSettings();
                    break;
                case GrayboxSystemMenuPage3D.OperationGuide:
                    systemMenu.BackFromOperationGuide();
                    break;
                case GrayboxSystemMenuPage3D.ExitConfirm:
                    systemMenu.CancelExit();
                    break;
                default:
                    systemMenu.Continue();
                    break;
            }
        }

        private void CloseOwnedMenu()
        {
            operations?.ClosePanels();
            if (systemMenu != null && systemMenu.IsOpen)
                systemMenu.Close();
        }

        private void EnsureDevelopmentPanelAdapter()
        {
            if (developmentPanel == null && developer != null)
                developmentPanel =
                    new DevelopmentPanelAdapter(developer);
        }

        private static bool HasActiveTextInputFocus()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem == null
                ? null
                : eventSystem.currentSelectedGameObject;
            if (IsActiveTextInput(selected)) return true;

            EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();
            for (var index = 0; index < eventSystems.Length; index++)
            {
                if (eventSystems[index] != null &&
                    IsActiveTextInput(
                        eventSystems[index].currentSelectedGameObject))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsActiveTextInput(GameObject selected)
        {
            if (selected == null || !selected.activeInHierarchy)
                return false;
            InputField input = selected.GetComponentInParent<InputField>();
            return input != null &&
                   input.IsActive() &&
                   input.IsInteractable();
        }

        private static void ClearActiveTextInputFocus()
        {
            EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();
            for (var index = 0; index < eventSystems.Length; index++)
            {
                EventSystem eventSystem = eventSystems[index];
                if (eventSystem != null && IsActiveTextInput(
                        eventSystem.currentSelectedGameObject))
                {
                    eventSystem.SetSelectedGameObject(null);
                }
            }
        }

        private void BindDevelopmentTextInput()
        {
            if (!isActiveAndEnabled) return;
            if (!observesDevelopmentInputDevices)
            {
                InputSystem.onDeviceChange +=
                    OnDevelopmentInputDeviceChange;
                observesDevelopmentInputDevices = true;
            }

            for (var index = 0; index < InputSystem.devices.Count; index++)
                if (InputSystem.devices[index] is Keyboard keyboard)
                    BindDevelopmentKeyboard(keyboard);
        }

        private void UnbindDevelopmentTextInput()
        {
            if (observesDevelopmentInputDevices)
            {
                InputSystem.onDeviceChange -=
                    OnDevelopmentInputDeviceChange;
                observesDevelopmentInputDevices = false;
            }

            foreach (KeyValuePair<Keyboard, Action<char>> binding in
                     developmentTextInputBindings)
                binding.Key.onTextInput -= binding.Value;
            developmentTextInputBindings.Clear();
        }

        private void OnDevelopmentInputDeviceChange(
            InputDevice device,
            InputDeviceChange change)
        {
            if (!(device is Keyboard keyboard)) return;
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                    BindDevelopmentKeyboard(keyboard);
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    UnbindDevelopmentKeyboard(keyboard);
                    break;
            }
        }

        private void BindDevelopmentKeyboard(Keyboard keyboard)
        {
            if (keyboard == null ||
                developmentTextInputBindings.ContainsKey(keyboard))
                return;
            Action<char> callback = character =>
                OnDevelopmentTextInput(keyboard, character);
            developmentTextInputBindings.Add(keyboard, callback);
            keyboard.onTextInput += callback;
        }

        private void UnbindDevelopmentKeyboard(Keyboard keyboard)
        {
            if (keyboard == null ||
                !developmentTextInputBindings.TryGetValue(
                    keyboard,
                    out Action<char> callback))
                return;
            keyboard.onTextInput -= callback;
            developmentTextInputBindings.Remove(keyboard);
        }

        private void OnDevelopmentTextInput(
            Keyboard source,
            char character)
        {
            if (!isActiveAndEnabled ||
                source == null ||
                !ReferenceEquals(source, Keyboard.current) ||
                char.IsControl(character))
                return;

            InputField input = FocusedDevelopmentInput();
            if (input == null) return;
            string value = input.text ?? string.Empty;
            int anchor = Mathf.Clamp(
                input.selectionAnchorPosition,
                0,
                value.Length);
            int focus = Mathf.Clamp(
                input.selectionFocusPosition,
                0,
                value.Length);
            int start = Mathf.Min(anchor, focus);
            int length = Mathf.Abs(anchor - focus);
            input.text = value.Remove(start, length)
                .Insert(start, character.ToString());
            input.caretPosition = start + 1;
        }

        private InputField FocusedDevelopmentInput()
        {
            if (developmentPanel == null || !developmentPanel.IsOpen)
                return null;
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem == null
                ? null
                : eventSystem.currentSelectedGameObject;
            InputField input = selected == null
                ? null
                : selected.GetComponentInParent<InputField>();
            if (input == null || !input.isFocused)
                return null;

            Transform current = input.transform;
            while (current != null)
            {
                if (current.name == "Graybox Developer Modifier")
                    return input;
                current = current.parent;
            }
            return null;
        }

        private static GrayboxInputSuppression SuppressAll()
        {
            return new GrayboxInputSuppression(
                move: true,
                deployment: true,
                destination: true,
                cameraDrag: true,
                home: true);
        }

        private sealed class DevelopmentPanelAdapter :
            IGrayboxDevelopmentPanelControl3D
        {
            private readonly GrayboxDeveloperModifierBootstrap3D developer;

            public DevelopmentPanelAdapter(
                GrayboxDeveloperModifierBootstrap3D developer)
            {
                this.developer = developer;
            }

            public bool IsOpen => developer != null && developer.IsPanelOpen;

            public void Close()
            {
                if (IsOpen)
                    developer.TryTogglePanel();
            }
        }
    }
}
