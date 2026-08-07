using System;
using UnityEngine;
using WasteCity.City;

namespace WasteCity.Graybox3D.Building
{
    public enum ConstructionCancelResult
    {
        NotFound,
        Cancelled,
        ConfirmationRequired
    }

    public sealed class GrayboxConstructionController3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;
        [SerializeField]
        private GrayboxBuildingInteractionModel3D interaction;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private GrayboxBuildingMenuView3D menu;
        private string selectedStableInstanceId;

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation,
            GrayboxBuildingInteractionModel3D interaction,
            Camera controlledCamera,
            GrayboxBuildingMenuView3D menu)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (city == null)
                throw new ArgumentNullException(nameof(city));
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            if (interaction == null)
                throw new ArgumentNullException(nameof(interaction));
            if (controlledCamera == null)
                throw new ArgumentNullException(nameof(controlledCamera));
            if (menu == null)
                throw new ArgumentNullException(nameof(menu));

            UnsubscribeMenu();
            this.session = session;
            this.city = city;
            this.presentation = presentation;
            this.interaction = interaction;
            this.controlledCamera = controlledCamera;
            this.menu = menu;
            selectedStableInstanceId = null;
            if (isActiveAndEnabled)
                SubscribeMenu();
        }

        public bool SelectAt(Vector2 screenPosition)
        {
            if (!IsConfigured) return false;
            Ray ray = controlledCamera.ScreenPointToRay(screenPosition);
            return presentation.TryPickInstance(
                       ray,
                       out string stableInstanceId) &&
                   SelectInstance(stableInstanceId);
        }

        public bool SelectInstance(string stableInstanceId)
        {
            if (!IsConfigured ||
                string.IsNullOrEmpty(stableInstanceId))
                return false;

            GrayboxBuildingInstance3D instance =
                FindInstance(stableInstanceId);
            if (instance == null ||
                instance.State !=
                GrayboxBuildingInstanceState.UnderConstruction)
                return false;
            selectedStableInstanceId = instance.StableInstanceId;
            return true;
        }

        public ConstructionCancelResult RequestCancelSelected()
        {
            if (!IsConfigured) return ConstructionCancelResult.NotFound;
            GrayboxBuildingInstance3D instance =
                FindInstance(selectedStableInstanceId);
            if (instance == null ||
                instance.State !=
                GrayboxBuildingInstanceState.UnderConstruction ||
                instance.IsEvacuationLocked)
            {
                selectedStableInstanceId = null;
                return ConstructionCancelResult.NotFound;
            }

            if (instance.Progress.Normalized <= 0f)
            {
                bool cancelled = session.TryCancelConstruction(
                    instance.StableInstanceId,
                    1d,
                    presentation,
                    out _);
                if (!cancelled)
                    return ConstructionCancelResult.NotFound;
                selectedStableInstanceId = null;
                return ConstructionCancelResult.Cancelled;
            }

            interaction.RequestCancelConstruction();
            return interaction.State ==
                   GrayboxBuildingInteractionState.CancelConfirmation
                ? ConstructionCancelResult.ConfirmationRequired
                : ConstructionCancelResult.NotFound;
        }

        public bool ResolveCancelSelected(bool confirmed)
        {
            if (!IsConfigured ||
                interaction.State !=
                GrayboxBuildingInteractionState.CancelConfirmation)
                return false;

            if (!confirmed)
            {
                interaction.ResolveCancelConfirmation(false);
                return true;
            }

            GrayboxBuildingInstance3D instance =
                FindInstance(selectedStableInstanceId);
            if (instance == null ||
                instance.State !=
                GrayboxBuildingInstanceState.UnderConstruction ||
                instance.IsEvacuationLocked)
            {
                interaction.ResolveCancelConfirmation(false);
                selectedStableInstanceId = null;
                return false;
            }

            bool cancelled = session.TryCancelConstruction(
                instance.StableInstanceId,
                1d,
                presentation,
                out _);
            interaction.ResolveCancelConfirmation(cancelled);
            if (cancelled)
                selectedStableInstanceId = null;
            return cancelled;
        }

        public void TickConstruction(float unscaledDeltaTime)
        {
            if (!IsConfigured) return;
            session.TickConstruction(
                unscaledDeltaTime,
                city.Mode,
                Time.timeScale <= 0f,
                presentation);
        }

        private bool IsConfigured =>
            session != null &&
            city != null &&
            presentation != null &&
            interaction != null &&
            controlledCamera != null &&
            menu != null;

        private void Update()
        {
            TickConstruction(Time.deltaTime);
        }

        private void OnEnable()
        {
            SubscribeMenu();
        }

        private void OnDisable()
        {
            UnsubscribeMenu();
        }

        private void OnDestroy()
        {
            UnsubscribeMenu();
            menu = null;
        }

        private void OnCancelSelectedConstructionRequested()
        {
            RequestCancelSelected();
        }

        private void OnCancelConstructionConfirmationResolved(
            bool confirmed)
        {
            ResolveCancelSelected(confirmed);
        }

        private GrayboxBuildingInstance3D FindInstance(
            string stableInstanceId)
        {
            if (string.IsNullOrEmpty(stableInstanceId) ||
                session == null ||
                session.Instances == null)
                return null;
            for (var index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance =
                    session.Instances[index];
                if (string.Equals(
                        instance.StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                    return instance;
            }
            return null;
        }

        private void UnsubscribeMenu()
        {
            if (ReferenceEquals(menu, null)) return;
            menu.CancelSelectedConstructionRequested -=
                OnCancelSelectedConstructionRequested;
            menu.CancelConstructionConfirmationResolved -=
                OnCancelConstructionConfirmationResolved;
        }

        private void SubscribeMenu()
        {
            if (ReferenceEquals(menu, null)) return;
            UnsubscribeMenu();
            menu.CancelSelectedConstructionRequested +=
                OnCancelSelectedConstructionRequested;
            menu.CancelConstructionConfirmationResolved +=
                OnCancelConstructionConfirmationResolved;
        }
    }
}
