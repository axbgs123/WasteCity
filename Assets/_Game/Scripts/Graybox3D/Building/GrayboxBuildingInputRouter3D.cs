using UnityEngine;
using UnityEngine.InputSystem;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingInputRouter3D :
        MonoBehaviour,
        IGrayboxInputInterceptor
    {
        [SerializeField] private GrayboxBuildingMenuView3D menu;
        [SerializeField]
        private GrayboxBuildingInteractionModel3D interaction;
        [SerializeField]
        private GrayboxBuildingPlacementController3D placement;
        [SerializeField]
        private GrayboxConstructionController3D construction;
        [SerializeField]
        private GrayboxEvacuationController3D evacuation;
        [SerializeField]
        private GrayboxDeveloperModifierBootstrap3D developer;

        public void Configure(
            GrayboxBuildingMenuView3D menu,
            GrayboxBuildingInteractionModel3D interaction,
            GrayboxBuildingPlacementController3D placement,
            GrayboxConstructionController3D construction,
            GrayboxEvacuationController3D evacuation,
            GrayboxDeveloperModifierBootstrap3D developer)
        {
            this.menu = menu;
            this.interaction = interaction;
            this.placement = placement;
            this.construction = construction;
            this.evacuation = evacuation;
            this.developer = developer;
        }

        public GrayboxInputSuppression ProcessCurrentInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Vector2 pointerPosition =
                mouse == null
                    ? Vector2.zero
                    : mouse.position.ReadValue();
            bool wasBuildMode =
                interaction != null &&
                interaction.State !=
                GrayboxBuildingInteractionState.Inactive;
            bool pointerActive =
                mouse != null &&
                (mouse.leftButton.wasPressedThisFrame ||
                 mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame ||
                 mouse.middleButton.isPressed ||
                 mouse.middleButton.wasReleasedThisFrame);
            bool pointerOverUi =
                pointerActive &&
                menu != null &&
                menu.IsPointerOverUi(pointerPosition);

            if (menu != null && menu.HasKeyboardFocus())
            {
                if (keyboard != null &&
                    keyboard.escapeKey.wasPressedThisFrame)
                    menu.ConsumeFocusedEscape();
                return new GrayboxInputSuppression(
                    move: true,
                    deployment: true,
                    destination: pointerOverUi || wasBuildMode,
                    cameraDrag: pointerOverUi,
                    home: true);
            }

            if (interaction != null &&
                interaction.State ==
                GrayboxBuildingInteractionState.CancelConfirmation)
            {
                if (keyboard != null &&
                    keyboard.escapeKey.wasPressedThisFrame)
                    construction?.ResolveCancelSelected(false);
                return SuppressAll();
            }

            if (evacuation != null &&
                (evacuation.IsManifestOpen ||
                 evacuation.IsProcessing))
                return SuppressAll();

            if (keyboard != null &&
                keyboard.f10Key.wasPressedThisFrame)
                developer?.TryTogglePanel();

            ProcessKeyboardActions(keyboard);

            bool paused = Time.timeScale <= 0f;
            if (!pointerOverUi &&
                interaction != null &&
                interaction.State ==
                GrayboxBuildingInteractionState.Previewing)
            {
                placement?.UpdatePointer(pointerPosition);
                if (!paused &&
                    mouse != null &&
                    mouse.leftButton.wasPressedThisFrame)
                    placement?.ConfirmCurrentPlacement(out _);
            }

            if (!paused &&
                keyboard != null &&
                keyboard.deleteKey.wasPressedThisFrame)
                construction?.RequestCancelSelected();

            bool deployment = paused;
            if (!paused &&
                keyboard != null &&
                keyboard.fKey.wasPressedThisFrame)
                deployment =
                    evacuation != null &&
                    evacuation.TryHandleDeploymentRequest();

            if (!pointerOverUi &&
                mouse != null &&
                mouse.rightButton.wasPressedThisFrame)
                CancelBuildState();

            return new GrayboxInputSuppression(
                move: false,
                deployment: deployment,
                destination: pointerOverUi || wasBuildMode,
                cameraDrag: pointerOverUi,
                home: false);
        }

        private void ProcessKeyboardActions(Keyboard keyboard)
        {
            if (keyboard == null || interaction == null)
                return;

            if (keyboard.bKey.wasPressedThisFrame)
                interaction.ToggleCatalog();

            if (interaction.State !=
                GrayboxBuildingInteractionState.Inactive)
            {
                int quickbarIndex = QuickbarIndex(keyboard);
                if (quickbarIndex >= 0)
                    menu?.TrySelectQuickbarSlot(quickbarIndex);
            }

            if (keyboard.rKey.wasPressedThisFrame)
                interaction.RotateClockwise();

            if (keyboard.escapeKey.wasPressedThisFrame)
                CancelBuildState();
        }

        private void CancelBuildState()
        {
            if (interaction == null)
                return;
            if (interaction.State ==
                GrayboxBuildingInteractionState.CatalogOpen)
            {
                interaction.CloseCatalog();
                return;
            }
            if (interaction.State ==
                GrayboxBuildingInteractionState.Previewing)
            {
                interaction.CancelPreview();
                placement?.HidePreview();
            }
        }

        private static int QuickbarIndex(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame) return 4;
            if (keyboard.digit6Key.wasPressedThisFrame) return 5;
            if (keyboard.digit7Key.wasPressedThisFrame) return 6;
            if (keyboard.digit8Key.wasPressedThisFrame) return 7;
            if (keyboard.digit9Key.wasPressedThisFrame) return 8;
            if (keyboard.digit0Key.wasPressedThisFrame) return 9;
            return -1;
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
    }
}
