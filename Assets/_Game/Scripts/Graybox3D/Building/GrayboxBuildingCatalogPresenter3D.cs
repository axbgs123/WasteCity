using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WasteCity.Building;
using WasteCity.Content;

namespace WasteCity.Graybox3D.Building
{
    public enum BuildingMenuCategory
    {
        Basic,
        Production,
        Logistics,
        Defense,
        Route
    }

    public enum BuildingCatalogVisibility
    {
        Hidden,
        Locked,
        Buildable
    }

    public readonly struct GrayboxBuildingCatalogItem3D
    {
        public GrayboxBuildingCatalogItem3D(
            BuildingDefinition definition,
            BuildingMenuCategory category,
            ContentRoute route,
            BuildingCatalogVisibility visibility,
            string primaryLockReason,
            IReadOnlyList<string> lockReasons)
        {
            Definition = definition;
            Category = category;
            Route = route;
            Visibility = visibility;
            PrimaryLockReason = primaryLockReason;
            LockReasons = lockReasons;
        }

        public BuildingDefinition Definition { get; }
        public BuildingMenuCategory Category { get; }
        public ContentRoute Route { get; }
        public BuildingCatalogVisibility Visibility { get; }
        public string PrimaryLockReason { get; }
        public IReadOnlyList<string> LockReasons { get; }
    }

    public interface IGrayboxBuildingCatalogContext3D
    {
        int Population { get; }
        bool IsResearchCompleted(string id);
        int CompletedBuildingCount(string id);
        bool HasContactedRoute(ContentRoute route);
    }

    public sealed class GrayboxBuildingCatalogPresenter3D
    {
        public const int BuildMenuCount = 28;

        private static readonly IReadOnlyList<string> NoLockReasons =
            new ReadOnlyCollection<string>(Array.Empty<string>());

        private static readonly Dictionary<string, BuildingMenuCategory> Categories =
            new Dictionary<string, BuildingMenuCategory>(StringComparer.Ordinal)
            {
                { "core.building.housing", BuildingMenuCategory.Basic },
                { "core.building.wall", BuildingMenuCategory.Basic },
                { "core.building.research-station", BuildingMenuCategory.Basic },
                { "core.building.mining-station", BuildingMenuCategory.Production },
                { "core.building.smelter", BuildingMenuCategory.Production },
                { "core.building.assembler", BuildingMenuCategory.Production },
                { "core.building.warehouse", BuildingMenuCategory.Logistics },
                { "core.building.automated-repair-bay", BuildingMenuCategory.Logistics },
                { "core.building.machine-gun-turret", BuildingMenuCategory.Defense },
                { "core.building.laser-tower", BuildingMenuCategory.Defense },
                { "technology.building.power-plant", BuildingMenuCategory.Route },
                { "cultivation.building.spirit-fire-furnace", BuildingMenuCategory.Route },
                { "cultivation.building.artifact-workshop", BuildingMenuCategory.Route },
                { "cultivation.building.sword-array-tower", BuildingMenuCategory.Route },
                { "cultivation.building.spirit-gathering-array", BuildingMenuCategory.Route },
                { "cultivation.building.alchemy-chamber", BuildingMenuCategory.Route },
                { "cultivation.building.puppet-workshop", BuildingMenuCategory.Route },
                { "biological.building.colony-pool", BuildingMenuCategory.Route },
                { "biological.building.breeding-chamber", BuildingMenuCategory.Route },
                { "biological.building.spore-tower", BuildingMenuCategory.Route },
                { "biological.building.metabolic-furnace", BuildingMenuCategory.Route },
                { "biological.building.acid-tower", BuildingMenuCategory.Route },
                { "biological.building.behemoth-pen", BuildingMenuCategory.Route },
                { "psionics.building.resonance-furnace", BuildingMenuCategory.Route },
                { "psionics.building.workshop", BuildingMenuCategory.Route },
                { "psionics.building.mind-spire", BuildingMenuCategory.Route },
                { "psionics.building.consciousness-network", BuildingMenuCategory.Route },
                { "psionics.building.shield-generator", BuildingMenuCategory.Route }
            };

        private static readonly IReadOnlyList<BuildingDefinition> quickbar =
            new ReadOnlyCollection<BuildingDefinition>(new[]
            {
                BuildingCatalog.MiningStation,
                BuildingCatalog.Housing,
                BuildingCatalog.Warehouse,
                BuildingCatalog.Wall,
                BuildingCatalog.ResearchStation,
                BuildingCatalog.Smelter,
                BuildingCatalog.Assembler,
                BuildingCatalog.MachineGunTurret,
                BuildingCatalog.AutomatedRepairBay,
                BuildingCatalog.LaserTower
            });

        public static IReadOnlyList<BuildingDefinition> Quickbar => quickbar;

        static GrayboxBuildingCatalogPresenter3D()
        {
            if (Categories.Count != BuildMenuCount ||
                BuildingCatalog.BuildMenu.Length != BuildMenuCount ||
                BuildingCatalog.BuildMenu.Any(definition =>
                    definition == null || !Categories.ContainsKey(definition.Id.Value)))
                throw new InvalidOperationException("The building catalog mapping must exactly match BuildMenu.");
        }

        public static BuildingMenuCategory CategoryOf(BuildingDefinition definition)
        {
            if (definition == null ||
                !Categories.TryGetValue(definition.Id.Value, out BuildingMenuCategory category))
                throw new ArgumentException("Definition is not in BuildingCatalog.BuildMenu.", nameof(definition));
            return category;
        }

        public static ContentRoute RouteOf(BuildingDefinition definition)
        {
            CategoryOf(definition);
            return RouteContentDisplayCatalog.BuildingRoute(definition);
        }

        public IReadOnlyList<GrayboxBuildingCatalogItem3D> Query(
            IGrayboxBuildingCatalogContext3D context,
            BuildingMenuCategory? category,
            ContentRoute? route,
            string visibleSearchText)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var items = new List<GrayboxBuildingCatalogItem3D>();
            foreach (BuildingDefinition definition in BuildingCatalog.BuildMenu)
            {
                GrayboxBuildingCatalogItem3D item = Describe(context, definition);
                if (item.Visibility == BuildingCatalogVisibility.Hidden ||
                    (category.HasValue && item.Category != category.Value) ||
                    (route.HasValue && item.Route != route.Value) ||
                    !MatchesVisibleSearch(item.Definition, visibleSearchText))
                    continue;
                items.Add(item);
            }
            return new ReadOnlyCollection<GrayboxBuildingCatalogItem3D>(items);
        }

        public GrayboxBuildingCatalogItem3D Describe(
            IGrayboxBuildingCatalogContext3D context,
            BuildingDefinition definition)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            BuildingMenuCategory category = CategoryOf(definition);
            ContentRoute route = RouteOf(definition);
            if (route != ContentRoute.Core && !context.HasContactedRoute(route))
            {
                return new GrayboxBuildingCatalogItem3D(
                    definition,
                    category,
                    route,
                    BuildingCatalogVisibility.Hidden,
                    null,
                    NoLockReasons);
            }

            BuildingUnlockEvaluation evaluation = BuildingUnlockModel.Evaluate(
                definition,
                context.Population,
                context.IsResearchCompleted,
                context.CompletedBuildingCount);
            return new GrayboxBuildingCatalogItem3D(
                definition,
                category,
                route,
                evaluation.IsUnlocked
                    ? BuildingCatalogVisibility.Buildable
                    : BuildingCatalogVisibility.Locked,
                evaluation.PrimaryReason,
                evaluation.IsUnlocked ? NoLockReasons : evaluation.Reasons);
        }

        private static bool MatchesVisibleSearch(
            BuildingDefinition definition,
            string visibleSearchText)
        {
            if (string.IsNullOrWhiteSpace(visibleSearchText)) return true;
            return definition.Name.IndexOf(
                       visibleSearchText,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   definition.Id.Value.IndexOf(
                       visibleSearchText,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
