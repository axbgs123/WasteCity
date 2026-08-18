using System;
using WasteCity.City;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public sealed class DemoResearchRuntime
    {
        public DemoResearchRuntime(ResearchModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Model.GrantCompleted(
                DemoResearchCatalog.Find(
                    DemoResearchCatalog.ScrapProcessingId));
        }

        public ResearchModel Model { get; }

        public bool IsCompleted(string researchId)
        {
            ResearchDefinition definition =
                DemoResearchCatalog.Find(researchId);
            return definition != null && Model.IsCompleted(definition.Id);
        }

        public static float SpeedMultiplier(CityMode cityMode)
        {
            return cityMode == CityMode.Fortress ? 1f : .5f;
        }

        public bool TryStart(
            string researchId,
            ResourceInventory cityInventory,
            bool hasEligibleResearchStation)
        {
            ResearchDefinition definition =
                DemoResearchCatalog.Find(researchId);
            return hasEligibleResearchStation &&
                definition != null &&
                DemoResearchCatalog.ReleaseState(researchId) ==
                    DemoResearchReleaseState.Researchable &&
                Model.Start(definition, cityInventory);
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
                Math.Max(0f, deltaSeconds) * SpeedMultiplier(cityMode));
        }

        public bool TryCancel(
            ResourceInventory cityInventory,
            ResourceCapacityPolicy capacity,
            int activeWarehouseCount)
        {
            if (!OwnsActiveResearch()) return false;
            return Model.TryCancel(
                cityInventory,
                capacity,
                activeWarehouseCount,
                .8f);
        }

        private bool OwnsActiveResearch()
        {
            ResearchDefinition active = Model.Active;
            if (active == null ||
                DemoResearchCatalog.ReleaseState(active.Id.Value) !=
                    DemoResearchReleaseState.Researchable)
            {
                return false;
            }

            return ReferenceEquals(
                active,
                DemoResearchCatalog.Find(active.Id.Value));
        }
    }
}
