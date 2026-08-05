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

        public void Select(GrayboxBuildingCatalogItem3D item)
        {
            EnsureSelectionAllowed();
            if (item.Visibility != BuildingCatalogVisibility.Buildable ||
                item.Definition == null ||
                item.Category != GrayboxBuildingCatalogPresenter3D.CategoryOf(item.Definition) ||
                item.Route != GrayboxBuildingCatalogPresenter3D.RouteOf(item.Definition))
            {
                throw new ArgumentException(
                    "Only a canonical buildable catalog item may be selected.",
                    nameof(item));
            }

            Select(item.Definition);
        }

        public void Select(BuildingDefinition definition)
        {
            EnsureSelectionAllowed();
            if (!IsCanonicalBuildMenuDefinition(definition))
                throw new ArgumentException(
                    "Only a canonical BuildMenu definition may be selected.",
                    nameof(definition));

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

        private static bool IsCanonicalBuildMenuDefinition(BuildingDefinition definition)
        {
            if (definition == null) return false;
            foreach (BuildingDefinition candidate in BuildingCatalog.BuildMenu)
                if (ReferenceEquals(candidate, definition)) return true;
            return false;
        }

        private void EnsureSelectionAllowed()
        {
            if (state != GrayboxBuildingInteractionState.Inactive &&
                state != GrayboxBuildingInteractionState.CatalogOpen &&
                state != GrayboxBuildingInteractionState.Previewing)
            {
                throw new InvalidOperationException(
                    "Selection is unavailable while confirmation is pending.");
            }
        }
    }
}
