using System;
using UnityEngine;
using UnityEngine.InputSystem;
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

        private IGrayboxDevelopmentPanelControl3D developmentPanel;

        public uint BuildingInputInvocationCount { get; private set; }

        public void Configure(
            GrayboxBuildingInputRouter3D buildingInput,
            GrayboxSystemMenuController3D systemMenu,
            GrayboxDeveloperModifierBootstrap3D developer)
        {
            if (developer == null)
                throw new ArgumentNullException(nameof(developer));
            this.buildingInput = buildingInput ??
                throw new ArgumentNullException(nameof(buildingInput));
            this.systemMenu = systemMenu ??
                throw new ArgumentNullException(nameof(systemMenu));
            this.developer = developer;
            developmentPanel = new DevelopmentPanelAdapter(developer);
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
            this.developmentPanel = developmentPanel;
        }

        public GrayboxInputSuppression ProcessCurrentInput()
        {
            Keyboard keyboard = Keyboard.current;
            bool escapePressed = keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame;

            if (systemMenu != null && systemMenu.IsOpen)
            {
                if (escapePressed)
                    ProcessMenuEscape();
                return SuppressAll();
            }

            GrayboxInputSuppression buildingSuppression = default;
            if (buildingInput != null)
            {
                unchecked { BuildingInputInvocationCount++; }
                buildingSuppression = buildingInput.ProcessCurrentInput();
            }

            if (!escapePressed ||
                buildingInput == null ||
                buildingInput.LastEscapeConsumed ||
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
        }

        private void OnDisable()
        {
            CloseOwnedMenu();
        }

        private void OnDestroy()
        {
            CloseOwnedMenu();
            buildingInput = null;
            systemMenu = null;
            developer = null;
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
            if (systemMenu != null && systemMenu.IsOpen)
                systemMenu.Close();
        }

        private void EnsureDevelopmentPanelAdapter()
        {
            if (developmentPanel == null && developer != null)
                developmentPanel =
                    new DevelopmentPanelAdapter(developer);
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
