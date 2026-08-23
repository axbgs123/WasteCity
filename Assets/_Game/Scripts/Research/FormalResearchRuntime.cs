using System;
using System.Collections.Generic;
using WasteCity.City;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public sealed class FormalResearchRuntime
    {
        private const float ThoughtAccelerationMultiplier = 1.25f;
        private const float CancelRefundRatio = .8f;

        private readonly Func<string, ResearchDefinition> resolver;

        public FormalResearchRuntime(
            ResearchModel model,
            Func<string, ResearchDefinition> resolver = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            this.resolver = resolver ?? ResearchCatalog.Find;
            Model.GrantCompleted(ResolveExact(ResearchCatalog.ScrapProcessingId));
        }

        public ResearchModel Model { get; }
        public bool HasMissingActiveResearch =>
            Model.HasMissingActiveResearch;
        public string MissingActiveResearchId =>
            Model.MissingActiveResearchId;
        public float MissingActiveRemainingSeconds =>
            HasMissingActiveResearch ? Model.Remaining : 0f;

        public ResearchPersistenceSnapshot CaptureForPersistence()
        {
            return Model.CaptureForPersistence();
        }

        public bool TryPrepareRestoreForPersistence(
            IReadOnlyList<string> completedResearchIds,
            string activeResearchId,
            float remainingSeconds,
            out ResearchRestorePlan plan,
            out string error)
        {
            return Model.TryPrepareRestoreForPersistence(
                completedResearchIds,
                activeResearchId,
                remainingSeconds,
                resolver,
                out plan,
                out error);
        }

        public bool TryCommitRestoreForPersistence(
            ResearchRestorePlan plan,
            out string error)
        {
            if (!Model.TryCommitRestoreForPersistence(plan, out error))
                return false;
            Model.GrantCompleted(ResolveExact(ResearchCatalog.ScrapProcessingId));
            return true;
        }

        public bool IsCompleted(string researchId)
        {
            ResearchDefinition definition = ResolveExact(researchId);
            return definition != null && Model.IsCompleted(definition.Id);
        }

        public static float SpeedMultiplier(
            CityMode cityMode,
            bool thoughtAccelerationCompleted)
        {
            float cityMultiplier = cityMode == CityMode.Fortress
                ? 1f
                : .5f;
            return thoughtAccelerationCompleted
                ? cityMultiplier * ThoughtAccelerationMultiplier
                : cityMultiplier;
        }

        public bool TryStart(
            string researchId,
            ResourceInventory cityInventory,
            bool hasEligibleResearchStation)
        {
            ResearchDefinition definition = ResolveExact(researchId);
            return hasEligibleResearchStation &&
                definition != null &&
                definition.ReleaseState == ResearchReleaseState.Researchable &&
                Model.Start(definition, cityInventory);
        }

        public bool TryStart(
            string researchId,
            CityResourceStorageModel cityStorage,
            bool hasEligibleResearchStation)
        {
            ResearchDefinition definition = ResolveExact(researchId);
            return hasEligibleResearchStation &&
                definition != null &&
                definition.ReleaseState == ResearchReleaseState.Researchable &&
                Model.Start(definition, cityStorage);
        }

        public bool Tick(
            float deltaSeconds,
            CityMode cityMode,
            bool globallyPaused,
            bool hasEligibleResearchStation)
        {
            if (globallyPaused ||
                !hasEligibleResearchStation ||
                !OwnsActiveResearch())
            {
                return false;
            }

            return Model.Tick(
                Math.Max(0f, deltaSeconds) * SpeedMultiplier(
                    cityMode,
                    IsCompleted(ResearchCatalog.ThoughtAccelerationId)));
        }

        public bool TryCancel(
            ResourceInventory cityInventory,
            ResourceCapacityPolicy capacity,
            int activeWarehouseCount)
        {
            return OwnsActiveResearch() && Model.TryCancel(
                cityInventory,
                capacity,
                activeWarehouseCount,
                CancelRefundRatio);
        }

        public bool TryCancel(CityResourceStorageModel cityStorage)
        {
            return OwnsActiveResearch() &&
                Model.TryCancel(cityStorage, CancelRefundRatio);
        }

        private bool OwnsActiveResearch()
        {
            ResearchDefinition active = Model.Active;
            if (active == null ||
                active.ReleaseState != ResearchReleaseState.Researchable)
            {
                return false;
            }

            ResearchDefinition resolved = ResolveExact(active.Id.Value);
            return resolved != null &&
                resolved.ReleaseState == ResearchReleaseState.Researchable;
        }

        private ResearchDefinition ResolveExact(string researchId)
        {
            if (string.IsNullOrEmpty(researchId)) return null;

            ResearchDefinition definition;
            try
            {
                definition = resolver(researchId);
            }
            catch
            {
                return null;
            }

            return definition != null && string.Equals(
                    definition.Id.Value,
                    researchId,
                    StringComparison.Ordinal)
                ? definition
                : null;
        }
    }
}
