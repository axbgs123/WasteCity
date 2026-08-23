using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Defense;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxCombatDestructionStatus3D
    {
        Committed,
        CommittedPresentationRebuildRequired,
        AlreadyCommitted,
        NotFound,
        NotEligible,
        HealthNotDestroyed,
        CommitFailed,
    }

    public sealed class GrayboxCombatDestructionResult3D
    {
        internal GrayboxCombatDestructionResult3D(
            GrayboxCombatDestructionStatus3D status,
            string stableInstanceId,
            string buildingDefinitionId,
            IReadOnlyList<ResourceAmount> productionLostResources,
            IReadOnlyList<ResourceAmount> warehouseLostResources,
            IReadOnlyList<ResourceAmount> towerLocalLostResources,
            bool committedNow,
            bool isCommitted,
            bool requiresPresentationRebuild)
        {
            Status = status;
            StableInstanceId = stableInstanceId ?? string.Empty;
            BuildingDefinitionId = buildingDefinitionId ?? string.Empty;
            ProductionLostResources = Freeze(productionLostResources);
            WarehouseLostResources = Freeze(warehouseLostResources);
            TowerLocalLostResources = Freeze(towerLocalLostResources);
            TotalLostResources = Freeze(Merge(
                ProductionLostResources,
                WarehouseLostResources,
                TowerLocalLostResources));
            CommittedNow = committedNow;
            IsCommitted = isCommitted;
            RequiresPresentationRebuild = requiresPresentationRebuild;
        }

        public GrayboxCombatDestructionStatus3D Status { get; }
        public string StableInstanceId { get; }
        public string BuildingDefinitionId { get; }
        public IReadOnlyList<ResourceAmount> ProductionLostResources { get; }
        public IReadOnlyList<ResourceAmount> WarehouseLostResources { get; }
        public IReadOnlyList<ResourceAmount> TowerLocalLostResources { get; }
        public IReadOnlyList<ResourceAmount> TotalLostResources { get; }
        public bool CommittedNow { get; }
        public bool IsCommitted { get; }
        public bool RequiresPresentationRebuild { get; }

        private static ReadOnlyCollection<ResourceAmount> Freeze(
            IReadOnlyList<ResourceAmount> source)
        {
            if (source == null || source.Count == 0)
                return Array.AsReadOnly(Array.Empty<ResourceAmount>());
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < source.Count; index++)
            {
                ResourceAmount amount = source[index];
                if (amount.Amount <= 0 ||
                    string.IsNullOrWhiteSpace(amount.ResourceId))
                {
                    continue;
                }
                totals.TryGetValue(amount.ResourceId, out int before);
                totals[amount.ResourceId] = before + amount.Amount;
            }
            return Array.AsReadOnly(Order(totals));
        }

        private static ResourceAmount[] Merge(
            params IReadOnlyList<ResourceAmount>[] sources)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var sourceIndex = 0;
                 sourceIndex < sources.Length;
                 sourceIndex++)
            {
                IReadOnlyList<ResourceAmount> source = sources[sourceIndex];
                if (source == null) continue;
                for (var index = 0; index < source.Count; index++)
                {
                    ResourceAmount amount = source[index];
                    if (amount.Amount <= 0 ||
                        string.IsNullOrWhiteSpace(amount.ResourceId))
                    {
                        continue;
                    }
                    totals.TryGetValue(amount.ResourceId, out int before);
                    totals[amount.ResourceId] = before + amount.Amount;
                }
            }
            return Order(totals);
        }

        private static ResourceAmount[] Order(
            IReadOnlyDictionary<string, int> totals)
        {
            if (totals == null || totals.Count == 0)
                return Array.Empty<ResourceAmount>();
            var result = new List<ResourceAmount>(totals.Count);
            var registered = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < ResourceIds.All.Length; index++)
            {
                string resourceId = ResourceIds.All[index];
                registered.Add(resourceId);
                if (totals.TryGetValue(resourceId, out int amount) &&
                    amount > 0)
                {
                    result.Add(new ResourceAmount(resourceId, amount));
                }
            }
            var unknownIds = new List<string>();
            foreach (KeyValuePair<string, int> item in totals)
            {
                if (item.Value > 0 && !registered.Contains(item.Key))
                    unknownIds.Add(item.Key);
            }
            unknownIds.Sort(StringComparer.Ordinal);
            for (var index = 0; index < unknownIds.Count; index++)
            {
                string resourceId = unknownIds[index];
                result.Add(new ResourceAmount(resourceId, totals[resourceId]));
            }
            return result.ToArray();
        }
    }

    public sealed class GrayboxCombatDestructionCoordinator3D
    {
        private readonly GrayboxBuildingSession3D session;
        private readonly GrayboxBuildingHealthRuntime3D health;
        private readonly GrayboxProductionRuntime3D production;
        private readonly GrayboxDefenseRuntime3D defense;
        private readonly SingleCityDefenseCampaignModel campaign;
        private readonly IGrayboxBuildingPresentation3D presentation;

        public GrayboxCombatDestructionCoordinator3D(
            GrayboxBuildingSession3D session,
            GrayboxBuildingHealthRuntime3D health,
            GrayboxProductionRuntime3D production,
            GrayboxDefenseRuntime3D defense,
            SingleCityDefenseCampaignModel campaign,
            IGrayboxBuildingPresentation3D presentation)
        {
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.health = health ??
                throw new ArgumentNullException(nameof(health));
            this.production = production ??
                throw new ArgumentNullException(nameof(production));
            this.defense = defense ??
                throw new ArgumentNullException(nameof(defense));
            this.campaign = campaign ??
                throw new ArgumentNullException(nameof(campaign));
            this.presentation = presentation ??
                throw new ArgumentNullException(nameof(presentation));
        }

        public event Action<GrayboxCombatDestructionResult3D>
            DestructionCommitted;

        public Exception LastNotificationFailure { get; private set; }

        public GrayboxCombatDestructionResult3D Commit(
            string stableInstanceId)
        {
            GrayboxBuildingInstance3D instance = Find(stableInstanceId);
            if (instance == null)
                return Result(
                    GrayboxCombatDestructionStatus3D.NotFound,
                    stableInstanceId,
                    null);

            string definitionId =
                instance.Placement?.Definition?.Id.Value ?? string.Empty;
            if (instance.State == GrayboxBuildingInstanceState.DestroyedRuin)
            {
                return Result(
                    GrayboxCombatDestructionStatus3D.AlreadyCommitted,
                    stableInstanceId,
                    definitionId,
                    isCommitted: true);
            }
            if (instance.State != GrayboxBuildingInstanceState.Completed ||
                !instance.IsPlayerOwned ||
                instance.IsEvacuationLocked)
            {
                return Result(
                    GrayboxCombatDestructionStatus3D.NotEligible,
                    stableInstanceId,
                    definitionId);
            }
            if (!health.TryGetHealth(
                    stableInstanceId,
                    out int currentHealth,
                    out _,
                    out bool isDestroyed) ||
                currentHealth != 0 ||
                !isDestroyed)
            {
                return Result(
                    GrayboxCombatDestructionStatus3D.HealthNotDestroyed,
                    stableInstanceId,
                    definitionId);
            }

            bool presentationFailure = false;
            if (!session.TryDestroyBuildingTruthForCombat(
                    stableInstanceId,
                    out GrayboxBuildingInstance3D committedInstance))
            {
                return Result(
                    GrayboxCombatDestructionStatus3D.CommitFailed,
                    stableInstanceId,
                    definitionId);
            }

            production.TryDestroyStateForCombat(
                stableInstanceId,
                out ResourceAmount[] productionLost);
            defense.TryDestroyTowerForCombat(
                stableInstanceId,
                out ResourceAmount[] towerLost);
            session.CityStorage.TryDestroyWarehouseForCombat(
                stableInstanceId,
                out ResourceAmount[] warehouseLost);

            try
            {
                presentation.UpdateInstance(committedInstance);
            }
            catch (Exception)
            {
                presentationFailure = true;
            }

            campaign.RegisterBuildingLoss(definitionId);
            var committed = new GrayboxCombatDestructionResult3D(
                presentationFailure
                    ? GrayboxCombatDestructionStatus3D
                        .CommittedPresentationRebuildRequired
                    : GrayboxCombatDestructionStatus3D.Committed,
                stableInstanceId,
                definitionId,
                productionLost,
                warehouseLost,
                towerLost,
                committedNow: true,
                isCommitted: true,
                requiresPresentationRebuild: presentationFailure);
            Publish(committed);
            return committed;
        }

        private GrayboxBuildingInstance3D Find(string stableInstanceId)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId)) return null;
            IReadOnlyList<GrayboxBuildingInstance3D> instances =
                session.Instances;
            if (instances == null) return null;
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance != null && string.Equals(
                        instance.StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                {
                    return instance;
                }
            }
            return null;
        }

        private static GrayboxCombatDestructionResult3D Result(
            GrayboxCombatDestructionStatus3D status,
            string stableInstanceId,
            string definitionId,
            bool isCommitted = false)
        {
            return new GrayboxCombatDestructionResult3D(
                status,
                stableInstanceId,
                definitionId,
                Array.Empty<ResourceAmount>(),
                Array.Empty<ResourceAmount>(),
                Array.Empty<ResourceAmount>(),
                committedNow: false,
                isCommitted,
                requiresPresentationRebuild: false);
        }

        private void Publish(GrayboxCombatDestructionResult3D result)
        {
            Action<GrayboxCombatDestructionResult3D> handlers =
                DestructionCommitted;
            if (handlers == null) return;
            Delegate[] subscribers = handlers.GetInvocationList();
            for (var index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<GrayboxCombatDestructionResult3D>)
                        subscribers[index])(result);
                }
                catch (Exception failure)
                {
                    LastNotificationFailure = failure;
                }
            }
        }
    }
}
