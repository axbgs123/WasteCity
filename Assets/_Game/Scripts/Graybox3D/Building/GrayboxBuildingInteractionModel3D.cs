using System;
using UnityEngine;
using WasteCity.Building;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxBuildingInteractionState
    {
        Inactive,
        CatalogOpen,
        Previewing,
        CancelConfirmation
    }

    public sealed class GrayboxBuildingInteractionModel3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingInteractionState state;
        [SerializeField] private GrayboxBuildingInteractionState catalogReturnState;
        [SerializeField] private GrayboxBuildingInteractionState cancelReturnState;
        [SerializeField] private BuildingOrientation orientation;
        [SerializeField] private string selectedBuildingId;

        public GrayboxBuildingInteractionState State => state;
        public GrayboxBuildingInteractionState CatalogReturnState => catalogReturnState;
        public BuildingDefinition Selected => FindBuildMenuDefinition(selectedBuildingId);
        public BuildingOrientation Orientation => orientation;

        public void ToggleCatalog()
        {
            if (state == GrayboxBuildingInteractionState.CatalogOpen)
            {
                CloseCatalog();
                return;
            }
            if (state != GrayboxBuildingInteractionState.Inactive &&
                state != GrayboxBuildingInteractionState.Previewing)
                return;

            catalogReturnState = state;
            state = GrayboxBuildingInteractionState.CatalogOpen;
        }

        public void CloseCatalog()
        {
            if (state == GrayboxBuildingInteractionState.CatalogOpen)
                state = catalogReturnState;
        }

        public void Select(BuildingDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (FindBuildMenuDefinition(definition.Id.Value) == null)
                throw new ArgumentException("Definition is not in BuildingCatalog.BuildMenu.", nameof(definition));

            selectedBuildingId = definition.Id.Value;
            state = GrayboxBuildingInteractionState.Previewing;
        }

        public void RotateClockwise()
        {
            if (state == GrayboxBuildingInteractionState.Previewing)
                orientation = BuildingOrientationRules.RotateClockwise(orientation);
        }

        public void RequestCancelConstruction()
        {
            if (state != GrayboxBuildingInteractionState.Previewing) return;
            cancelReturnState = state;
            state = GrayboxBuildingInteractionState.CancelConfirmation;
        }

        public void ResolveCancelConfirmation(bool confirmed)
        {
            if (state != GrayboxBuildingInteractionState.CancelConfirmation) return;
            if (confirmed)
            {
                selectedBuildingId = null;
                orientation = BuildingOrientation.North;
                state = GrayboxBuildingInteractionState.Inactive;
                return;
            }
            state = cancelReturnState;
        }

        public void CancelPreview()
        {
            if (state != GrayboxBuildingInteractionState.Previewing) return;
            selectedBuildingId = null;
            orientation = BuildingOrientation.North;
            state = GrayboxBuildingInteractionState.Inactive;
        }

        private static BuildingDefinition FindBuildMenuDefinition(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return null;
            foreach (BuildingDefinition definition in BuildingCatalog.BuildMenu)
            {
                if (definition.Id.Value == stableId) return definition;
            }
            return null;
        }
    }
}
