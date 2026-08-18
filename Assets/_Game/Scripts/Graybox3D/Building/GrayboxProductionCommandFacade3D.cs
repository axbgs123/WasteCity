using System;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProductionCommandFacade3D
    {
        private readonly GrayboxProductionClock3D owner;

        internal GrayboxProductionCommandFacade3D(
            GrayboxProductionClock3D owner)
        {
            this.owner = owner ??
                throw new ArgumentNullException(nameof(owner));
        }

        public bool TrySetPlayerPaused(
            string stableInstanceId,
            bool paused)
        {
            if (!owner.Runtime.TryGetState(
                    stableInstanceId,
                    out BuildingProductionState state))
            {
                return false;
            }

            if (state.IsPlayerPaused != paused)
            {
                state.SetPlayerPaused(paused);
                owner.PublishObservabilityIfChanged();
            }
            return true;
        }

        public ResourceTransferResult TransferInputFromInventory(
            string stableInstanceId,
            ResourceInventory source,
            string resourceId,
            int requestedAmount,
            bool accessValidated)
        {
            if (!TryGetInputState(
                    stableInstanceId,
                    resourceId,
                    accessValidated,
                    out BuildingProductionState state))
            {
                return Invalid(requestedAmount);
            }

            ResourceTransferResult result;
            using (source.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Production,
                           state.Definition.BuildingId)))
            {
                result = ResourceTransaction.Transfer(
                    source,
                    state.Input,
                    state.InputCapacityPolicy,
                    0,
                    resourceId,
                    requestedAmount);
            }
            PublishAfterSuccess(result);
            return result;
        }

        public ResourceTransferResult TransferInputFromCityStorage(
            string stableInstanceId,
            string resourceId,
            int requestedAmount,
            bool accessValidated)
        {
            CityResourceStorageModel source = owner.LatestCityStorage;
            if (source == null || requestedAmount <= 0 || !TryGetInputState(
                    stableInstanceId,
                    resourceId,
                    accessValidated,
                    out BuildingProductionState state))
            {
                return Invalid(requestedAmount);
            }

            int available = source.GetNetworkAmount(resourceId);
            if (available <= 0)
                return Result(requestedAmount, 0, ResourceTransferStatus.SourceEmpty);
            int candidate = Math.Min(requestedAmount, available);
            int accepted = state.InputCapacityPolicy.GetAcceptableAmount(
                state.Input,
                resourceId,
                candidate,
                activeWarehouseCount: 0);
            if (accepted <= 0)
                return Result(requestedAmount, 0, ResourceTransferStatus.TargetFull);

            using (source.AttributeChanges(new ResourceChangeAttribution(
                       ResourceChangeAttributionKind.Production,
                       state.Definition.BuildingId)))
            {
                if (!source.TrySpendFromNetwork(resourceId, accepted))
                    return Result(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                if (state.Input.Add(resourceId, accepted) != accepted)
                {
                    source.AddToNetwork(resourceId, accepted);
                    return Result(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }
            }
            var result = Result(
                requestedAmount,
                accepted,
                accepted == requestedAmount
                    ? ResourceTransferStatus.Completed
                    : ResourceTransferStatus.Partial);
            PublishAfterSuccess(result);
            return result;
        }

        public ResourceTransferResult TransferInputFromBackpack(
            string stableInstanceId,
            PlayerBackpackModel source,
            string resourceId,
            int requestedAmount,
            bool accessValidated)
        {
            if (!TryGetInputState(
                    stableInstanceId,
                    resourceId,
                    accessValidated,
                    out BuildingProductionState state))
            {
                return Invalid(requestedAmount);
            }

            ResourceTransferResult result =
                ResourceTransaction.TransferFromBackpack(
                    source,
                    state.Input,
                    state.InputCapacityPolicy,
                    0,
                    resourceId,
                    requestedAmount);
            PublishAfterSuccess(result);
            return result;
        }

        public ResourceTransferResult TransferOutputToInventory(
            string stableInstanceId,
            ResourceInventory target,
            ResourceCapacityPolicy targetCapacity,
            string resourceId,
            int requestedAmount,
            bool accessValidated)
        {
            if (!TryGetOutputState(
                    stableInstanceId,
                    resourceId,
                    accessValidated,
                    out BuildingProductionState state))
            {
                return Invalid(requestedAmount);
            }

            ResourceTransferResult result;
            using (target.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Production,
                           state.Definition.BuildingId)))
            {
                result = ResourceTransaction.Transfer(
                    state.Output,
                    target,
                    targetCapacity,
                    owner.Runtime.ActiveWarehouseCount,
                    resourceId,
                    requestedAmount);
            }
            PublishAfterSuccess(result);
            return result;
        }

        public ResourceTransferResult TransferOutputToCityStorage(
            string stableInstanceId,
            string resourceId,
            int requestedAmount,
            bool accessValidated)
        {
            CityResourceStorageModel target = owner.LatestCityStorage;
            if (target == null || requestedAmount <= 0 || !TryGetOutputState(
                    stableInstanceId,
                    resourceId,
                    accessValidated,
                    out BuildingProductionState state))
            {
                return Invalid(requestedAmount);
            }

            int available = state.Output.Get(resourceId);
            if (available <= 0)
                return Result(requestedAmount, 0, ResourceTransferStatus.SourceEmpty);
            int candidate = Math.Min(requestedAmount, available);
            int accepted = target.GetNetworkAcceptableAmount(
                resourceId,
                candidate);
            if (accepted <= 0)
                return Result(requestedAmount, 0, ResourceTransferStatus.TargetFull);

            int before = available;
            using (target.AttributeChanges(new ResourceChangeAttribution(
                       ResourceChangeAttributionKind.Production,
                       state.Definition.BuildingId)))
            {
                if (!state.Output.TrySpend(resourceId, accepted))
                    return Result(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                int stored = target.AddToNetwork(resourceId, accepted);
                if (stored != accepted)
                {
                    if (stored > 0)
                        target.TrySpendFromNetwork(resourceId, stored);
                    state.Output.Restore(resourceId, before);
                    return Result(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }
            }
            var result = Result(
                requestedAmount,
                accepted,
                accepted == requestedAmount
                    ? ResourceTransferStatus.Completed
                    : ResourceTransferStatus.Partial);
            PublishAfterSuccess(result);
            return result;
        }

        public ResourceTransferResult TransferOutputToBackpack(
            string stableInstanceId,
            PlayerBackpackModel target,
            string resourceId,
            int requestedAmount,
            bool accessValidated)
        {
            if (!TryGetOutputState(
                    stableInstanceId,
                    resourceId,
                    accessValidated,
                    out BuildingProductionState state))
            {
                return Invalid(requestedAmount);
            }

            ResourceTransferResult result =
                ResourceTransaction.TransferToBackpack(
                    state.Output,
                    target,
                    resourceId,
                    requestedAmount);
            PublishAfterSuccess(result);
            return result;
        }

        private bool TryGetInputState(
            string stableInstanceId,
            string resourceId,
            bool accessValidated,
            out BuildingProductionState state)
        {
            state = null;
            return accessValidated &&
                owner.Runtime.TryGetState(stableInstanceId, out state) &&
                state.Definition.InputAmount > 0 &&
                string.Equals(
                    state.Definition.InputResourceId,
                    resourceId,
                    StringComparison.Ordinal);
        }

        private bool TryGetOutputState(
            string stableInstanceId,
            string resourceId,
            bool accessValidated,
            out BuildingProductionState state)
        {
            state = null;
            if (!accessValidated ||
                !owner.Runtime.TryGetState(stableInstanceId, out state))
            {
                return false;
            }

            string expectedResourceId =
                ProductionObservabilitySnapshot.ResolveOutputResourceId(
                    state,
                    owner.LatestWorld);
            return !string.IsNullOrEmpty(expectedResourceId) &&
                string.Equals(
                    expectedResourceId,
                    resourceId,
                    StringComparison.Ordinal);
        }

        private void PublishAfterSuccess(ResourceTransferResult result)
        {
            if (result.Succeeded)
                owner.PublishObservabilityIfChanged();
        }

        private static ResourceTransferResult Invalid(int requestedAmount)
        {
            return new ResourceTransferResult(
                requestedAmount,
                0,
                ResourceTransferStatus.InvalidRequest);
        }

        private static ResourceTransferResult Result(
            int requestedAmount,
            int movedAmount,
            ResourceTransferStatus status)
        {
            return new ResourceTransferResult(
                requestedAmount,
                movedAmount,
                status);
        }
    }
}
