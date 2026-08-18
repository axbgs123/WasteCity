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
        [SerializeField]
        private GrayboxBuildingWorldView3D productionPresentation;
        [SerializeField]
        private GrayboxOperationsController3D operations;
        [SerializeField]
        private GrayboxDefenseController3D defense;

        public bool LastEscapeConsumed { get; private set; }
        public bool HasKeyboardFocus =>
            menu != null && menu.HasKeyboardFocus();

        public void ConfigureProductionDetails(
            GrayboxBuildingWorldView3D presentation,
            GrayboxOperationsController3D operations)
        {
            productionPresentation = presentation;
            this.operations = operations;
        }

        public void ConfigureDefense(
            GrayboxDefenseController3D defense)
        {
            this.defense = defense;
        }

        public bool TryCloseForOperations()
        {
            if (interaction == null ||
                interaction.State ==
                    GrayboxBuildingInteractionState.CancelConfirmation ||
                (evacuation != null &&
                 (evacuation.IsManifestOpen || evacuation.IsProcessing)))
            {
                return false;
            }

            if (interaction.State ==
                GrayboxBuildingInteractionState.CatalogOpen)
            {
                interaction.CloseCatalog();
            }
            if (interaction.State ==
                GrayboxBuildingInteractionState.Previewing)
            {
                interaction.CancelPreview();
                placement?.HidePreview();
            }
            return interaction.State ==
                GrayboxBuildingInteractionState.Inactive;
        }

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

        public void Configure(
            GrayboxBuildingMenuView3D menu,
            GrayboxBuildingInteractionModel3D interaction,
            GrayboxBuildingPlacementController3D placement,
            GrayboxConstructionController3D construction,
            GrayboxEvacuationController3D evacuation,
            GrayboxDeveloperModifierBootstrap3D developer,
            GrayboxDefenseController3D defense)
        {
            Configure(
                menu,
                interaction,
                placement,
                construction,
                evacuation,
                developer);
            this.defense = defense;
        }

        public GrayboxInputSuppression ProcessCurrentInput()
        {
            LastEscapeConsumed = false;
            try
            {
                return ProcessCurrentInputCore();
            }
            finally
            {
                placement?.RefreshMiningGuidance();
                placement?.SetBuildGridVisible(
                    interaction != null &&
                    interaction.State !=
                    GrayboxBuildingInteractionState.Inactive);
            }
        }

        private GrayboxInputSuppression ProcessCurrentInputCore()
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
            bool buildInputOwnedThisFrame = wasBuildMode;
            bool previewing =
                interaction != null &&
                interaction.State ==
                GrayboxBuildingInteractionState.Previewing;
            bool pointerActive =
                mouse != null &&
                (previewing ||
                 mouse.leftButton.wasPressedThisFrame ||
                 mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame ||
                 mouse.middleButton.isPressed ||
                 mouse.middleButton.wasReleasedThisFrame);
            bool pointerClassified =
                pointerActive &&
                menu != null;
            bool pointerOverUi =
                pointerClassified &&
                menu.IsPointerOverUi(pointerPosition);

            if (menu != null && menu.HasKeyboardFocus())
            {
                if (keyboard != null &&
                    keyboard.escapeKey.wasPressedThisFrame)
                    LastEscapeConsumed =
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
                {
                    LastEscapeConsumed = true;
                    construction?.ResolveCancelSelected(false);
                }
                return SuppressAll();
            }

            if (evacuation != null &&
                (evacuation.IsManifestOpen ||
                 evacuation.IsProcessing))
            {
                if (keyboard != null &&
                    keyboard.escapeKey.wasPressedThisFrame)
                {
                    LastEscapeConsumed =
                        evacuation.IsProcessing ||
                        evacuation.TryCancelManifest();
                }
                return SuppressAll();
            }

            if (keyboard != null &&
                keyboard.digit0Key.wasPressedThisFrame)
                developer?.TryTogglePanel();

            buildInputOwnedThisFrame |=
                ProcessKeyboardActions(keyboard);
            previewing =
                interaction != null &&
                interaction.State ==
                GrayboxBuildingInteractionState.Previewing;
            buildInputOwnedThisFrame |=
                interaction != null &&
                interaction.State !=
                GrayboxBuildingInteractionState.Inactive;
            if (previewing &&
                !pointerClassified &&
                mouse != null &&
                menu != null)
            {
                pointerOverUi =
                    menu.IsPointerOverUi(pointerPosition);
                pointerClassified = true;
            }

            bool paused = Time.timeScale <= 0f;
            if (!pointerOverUi &&
                previewing)
            {
                placement?.UpdatePointer(pointerPosition);
                if (!paused &&
                    mouse != null &&
                    mouse.leftButton.wasPressedThisFrame)
                    placement?.ConfirmCurrentPlacement(out _);
            }

            if (!pointerOverUi && !previewing &&
                interaction != null &&
                interaction.State ==
                    GrayboxBuildingInteractionState.Inactive &&
                mouse != null &&
                mouse.leftButton.wasPressedThisFrame &&
                Camera.main != null)
            {
                Ray pointerRay =
                    Camera.main.ScreenPointToRay(pointerPosition);
                if (defense != null && defense.TrySelect(pointerRay))
                {
                    operations?.ClosePanels();
                    buildInputOwnedThisFrame = true;
                }
                else if (productionPresentation != null &&
                         productionPresentation.TryPickInstance(
                             pointerRay,
                             out string selectedStableId) &&
                         operations != null &&
                         operations.TryOpenBuildingDetail(selectedStableId))
                {
                    defense?.CloseSelection();
                    buildInputOwnedThisFrame = true;
                }
            }

            if (!paused &&
                keyboard != null &&
                keyboard.deleteKey.wasPressedThisFrame)
                construction?.RequestCancelSelected();

            if (interaction != null &&
                interaction.State ==
                GrayboxBuildingInteractionState.CancelConfirmation)
                return SuppressAll();

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
            {
                bool rightClickOwned =
                    interaction != null &&
                    interaction.State !=
                    GrayboxBuildingInteractionState.Inactive;
                if (rightClickOwned)
                {
                    buildInputOwnedThisFrame = true;
                    CancelBuildState();
                }
            }

            bool isBuildMode =
                interaction != null &&
                interaction.State !=
                GrayboxBuildingInteractionState.Inactive;

            return new GrayboxInputSuppression(
                move: false,
                deployment: deployment,
                destination:
                    pointerOverUi ||
                    buildInputOwnedThisFrame ||
                    isBuildMode,
                cameraDrag: pointerOverUi,
                home: false);
        }

        private bool ProcessKeyboardActions(Keyboard keyboard)
        {
            if (keyboard == null || interaction == null)
                return false;

            bool owned = false;

            if (keyboard.bKey.wasPressedThisFrame)
            {
                if (defense != null && defense.HasSelection)
                    defense.CloseSelection();
                interaction.ToggleCatalog();
                owned = true;
            }

            if (interaction.State !=
                GrayboxBuildingInteractionState.Inactive)
            {
                int quickbarIndex = QuickbarIndex(keyboard);
                if (quickbarIndex >= 0)
                {
                    menu?.TrySelectQuickbarSlot(quickbarIndex);
                    owned = true;
                }
            }

            if (keyboard.rKey.wasPressedThisFrame &&
                interaction.State !=
                GrayboxBuildingInteractionState.Inactive)
            {
                interaction.RotateClockwise();
                owned = true;
            }

            if (keyboard.escapeKey.wasPressedThisFrame &&
                interaction.State !=
                GrayboxBuildingInteractionState.Inactive)
            {
                CancelBuildState();
                LastEscapeConsumed = true;
                owned = true;
            }

            return owned;
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
