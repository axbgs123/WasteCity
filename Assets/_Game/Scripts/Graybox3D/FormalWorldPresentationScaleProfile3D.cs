using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;

namespace WasteCity.Graybox3D
{
    public enum FormalBuildingVisualArchetype3D
    {
        LowBarrier,
        ResidentialBlock,
        StorageBlock,
        ExtractorRig,
        Processor,
        Workshop,
        ResearchHub,
        DefenseFoundation,
        Tower,
        FieldArray,
        LargeEnclosure,
    }

    public readonly struct FormalBuildingVisualMetrics3D
    {
        public FormalBuildingVisualMetrics3D(
            FormalBuildingVisualArchetype3D archetype,
            float footprintFillRatio,
            float visualHeightInCells,
            float foundationHeightRatio,
            float upperBodyWidthRatio,
            float crownHeightRatio,
            bool defenseOwnsSuperstructure)
        {
            Archetype = archetype;
            FootprintFillRatio = footprintFillRatio;
            VisualHeightInCells = visualHeightInCells;
            FoundationHeightRatio = foundationHeightRatio;
            UpperBodyWidthRatio = upperBodyWidthRatio;
            CrownHeightRatio = crownHeightRatio;
            DefenseOwnsSuperstructure = defenseOwnsSuperstructure;
        }

        public FormalBuildingVisualArchetype3D Archetype { get; }
        public float FootprintFillRatio { get; }
        public float VisualHeightInCells { get; }
        public float FoundationHeightRatio { get; }
        public float UpperBodyWidthRatio { get; }
        public float CrownHeightRatio { get; }
        public bool DefenseOwnsSuperstructure { get; }
    }

    public readonly struct FormalWorldMarkerMetrics3D
    {
        public FormalWorldMarkerMetrics3D(
            ResourceNodeMarkerLod3D lod,
            float frameReferencePixels,
            float iconReferencePixels,
            float textReferencePixels,
            float minimumPhysicalPixels,
            float maximumPhysicalPixels,
            bool showFrame,
            bool showName,
            bool showAmount)
        {
            Lod = lod;
            FrameReferencePixels = frameReferencePixels;
            IconReferencePixels = iconReferencePixels;
            TextReferencePixels = textReferencePixels;
            MinimumPhysicalPixels = minimumPhysicalPixels;
            MaximumPhysicalPixels = maximumPhysicalPixels;
            ShowFrame = showFrame;
            ShowName = showName;
            ShowAmount = showAmount;
        }

        public ResourceNodeMarkerLod3D Lod { get; }
        public float FrameReferencePixels { get; }
        public float IconReferencePixels { get; }
        public float TextReferencePixels { get; }
        public float MinimumPhysicalPixels { get; }
        public float MaximumPhysicalPixels { get; }
        public bool ShowFrame { get; }
        public bool ShowName { get; }
        public bool ShowAmount { get; }
    }

    [CreateAssetMenu(
        fileName = "FormalWorldPresentationScaleProfile3D",
        menuName = "WasteCity/Presentation/Formal World Scale 3D")]
    public sealed class FormalWorldPresentationScaleProfile3D :
        ScriptableObject
    {
        public const string ResourcesPath =
            "Presentation/FormalWorldPresentationScaleProfile3D";
        public const float GroundCellSize = 1f;
        public const float InnerCellSize = .32f;
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        private static readonly Dictionary<string,
            FormalBuildingVisualArchetype3D> BuildingArchetypes =
                CreateBuildingArchetypes();

        [SerializeField] private float innerVerticalEmphasis = 1.15f;
        [SerializeField] private float nearUnitScreenHeight = .045f;
        [SerializeField] private float midUnitScreenHeight = .027f;
        [SerializeField] private float markerSeparationReferencePixels = 6f;
        [SerializeField] private float selectionLineReferencePixels = 2f;

        public float InnerVerticalEmphasis => innerVerticalEmphasis;
        public float NearUnitScreenHeight => nearUnitScreenHeight;
        public float MidUnitScreenHeight => midUnitScreenHeight;
        public float MarkerSeparationReferencePixels =>
            markerSeparationReferencePixels;
        public float SelectionLineReferencePixels =>
            selectionLineReferencePixels;

        public bool TryResolveBuilding(
            BuildingDefinition definition,
            out FormalBuildingVisualMetrics3D metrics)
        {
            if (definition == null ||
                !BuildingArchetypes.TryGetValue(
                    definition.Id.Value,
                    out FormalBuildingVisualArchetype3D archetype))
            {
                metrics = default;
                return false;
            }

            metrics = MetricsFor(archetype);
            return true;
        }

        public FormalWorldMarkerMetrics3D ResolveMarker(
            float worldUnitScreenHeight)
        {
            if (!IsFinite(worldUnitScreenHeight))
                worldUnitScreenHeight = midUnitScreenHeight;
            if (worldUnitScreenHeight >= nearUnitScreenHeight)
            {
                return new FormalWorldMarkerMetrics3D(
                    ResourceNodeMarkerLod3D.Near,
                    68f,
                    50f,
                    22f,
                    68f,
                    84f,
                    true,
                    true,
                    true);
            }
            if (worldUnitScreenHeight >= midUnitScreenHeight)
            {
                return new FormalWorldMarkerMetrics3D(
                    ResourceNodeMarkerLod3D.Mid,
                    56f,
                    42f,
                    20f,
                    56f,
                    72f,
                    true,
                    false,
                    true);
            }
            return new FormalWorldMarkerMetrics3D(
                ResourceNodeMarkerLod3D.Far,
                0f,
                28f,
                0f,
                28f,
                40f,
                false,
                false,
                false);
        }

        public float CellSize(BuildingSite site)
        {
            return site == BuildingSite.InnerCity
                ? InnerCellSize
                : GroundCellSize;
        }

        public bool TryValidate(out string error)
        {
            if (!IsFinitePositive(innerVerticalEmphasis) ||
                !IsFinitePositive(nearUnitScreenHeight) ||
                !IsFinitePositive(midUnitScreenHeight) ||
                nearUnitScreenHeight <= midUnitScreenHeight ||
                !IsFinitePositive(markerSeparationReferencePixels) ||
                !IsFinitePositive(selectionLineReferencePixels))
            {
                error = "World presentation scale values must be finite, " +
                    "positive, and ordered.";
                return false;
            }
            if (BuildingArchetypes.Count != BuildingCatalog.All.Length)
            {
                error = "Every formal building requires exactly one visual " +
                    "archetype.";
                return false;
            }
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition definition = BuildingCatalog.All[index];
                if (!BuildingArchetypes.TryGetValue(
                        definition.Id.Value,
                        out FormalBuildingVisualArchetype3D archetype) ||
                    !MetricsAreValid(MetricsFor(archetype)))
                {
                    error = "Invalid visual scale for building '" +
                        definition.Id.Value + "'.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static FormalBuildingVisualMetrics3D MetricsFor(
            FormalBuildingVisualArchetype3D archetype)
        {
            switch (archetype)
            {
                case FormalBuildingVisualArchetype3D.LowBarrier:
                    return Metrics(archetype, .92f, .38f, .55f, .95f, .1f);
                case FormalBuildingVisualArchetype3D.ResidentialBlock:
                    return Metrics(archetype, .86f, .68f, .2f, .72f, .2f);
                case FormalBuildingVisualArchetype3D.StorageBlock:
                    return Metrics(archetype, .9f, .58f, .25f, .88f, .12f);
                case FormalBuildingVisualArchetype3D.ExtractorRig:
                    return Metrics(archetype, .82f, .86f, .18f, .58f, .24f);
                case FormalBuildingVisualArchetype3D.Processor:
                    return Metrics(archetype, .88f, .92f, .2f, .7f, .22f);
                case FormalBuildingVisualArchetype3D.Workshop:
                    return Metrics(archetype, .86f, .78f, .2f, .76f, .18f);
                case FormalBuildingVisualArchetype3D.ResearchHub:
                    return Metrics(archetype, .82f, 1.05f, .18f, .62f, .3f);
                case FormalBuildingVisualArchetype3D.DefenseFoundation:
                    return Metrics(
                        archetype,
                        .74f,
                        .14f,
                        1f,
                        .72f,
                        0f,
                        true);
                case FormalBuildingVisualArchetype3D.Tower:
                    return Metrics(archetype, .72f, 1.15f, .18f, .42f, .28f);
                case FormalBuildingVisualArchetype3D.FieldArray:
                    return Metrics(archetype, .84f, .32f, .35f, .7f, .1f);
                default:
                    return Metrics(archetype, .88f, .72f, .22f, .82f, .12f);
            }
        }

        private static FormalBuildingVisualMetrics3D Metrics(
            FormalBuildingVisualArchetype3D archetype,
            float footprintFillRatio,
            float visualHeightInCells,
            float foundationHeightRatio,
            float upperBodyWidthRatio,
            float crownHeightRatio,
            bool defenseOwnsSuperstructure = false)
        {
            return new FormalBuildingVisualMetrics3D(
                archetype,
                footprintFillRatio,
                visualHeightInCells,
                foundationHeightRatio,
                upperBodyWidthRatio,
                crownHeightRatio,
                defenseOwnsSuperstructure);
        }

        private static bool MetricsAreValid(
            FormalBuildingVisualMetrics3D metrics)
        {
            return IsUnitRatio(metrics.FootprintFillRatio) &&
                IsFinitePositive(metrics.VisualHeightInCells) &&
                IsUnitRatio(metrics.FoundationHeightRatio) &&
                IsUnitRatio(metrics.UpperBodyWidthRatio) &&
                IsUnitRatioOrZero(metrics.CrownHeightRatio);
        }

        private static bool IsUnitRatio(float value)
        {
            return IsFinite(value) && value > 0f && value <= 1f;
        }

        private static bool IsUnitRatioOrZero(float value)
        {
            return IsFinite(value) && value >= 0f && value <= 1f;
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Dictionary<string, FormalBuildingVisualArchetype3D>
            CreateBuildingArchetypes()
        {
            return new Dictionary<string,
                FormalBuildingVisualArchetype3D>(StringComparer.Ordinal)
            {
                { "core.building.mining-station", FormalBuildingVisualArchetype3D.ExtractorRig },
                { "core.building.housing", FormalBuildingVisualArchetype3D.ResidentialBlock },
                { "core.building.warehouse", FormalBuildingVisualArchetype3D.StorageBlock },
                { "core.building.wall", FormalBuildingVisualArchetype3D.LowBarrier },
                { "core.building.research-station", FormalBuildingVisualArchetype3D.ResearchHub },
                { "core.building.smelter", FormalBuildingVisualArchetype3D.Processor },
                { "core.building.assembler", FormalBuildingVisualArchetype3D.Processor },
                { "core.building.machine-gun-turret", FormalBuildingVisualArchetype3D.DefenseFoundation },
                { "core.building.heavy-machine-gun-turret", FormalBuildingVisualArchetype3D.Tower },
                { "technology.building.power-plant", FormalBuildingVisualArchetype3D.Processor },
                { "cultivation.building.spirit-fire-furnace", FormalBuildingVisualArchetype3D.Processor },
                { "cultivation.building.artifact-workshop", FormalBuildingVisualArchetype3D.Workshop },
                { "cultivation.building.sword-array-tower", FormalBuildingVisualArchetype3D.Tower },
                { "cultivation.building.sword-riding-platform", FormalBuildingVisualArchetype3D.Tower },
                { "biological.building.colony-pool", FormalBuildingVisualArchetype3D.Processor },
                { "biological.building.breeding-chamber", FormalBuildingVisualArchetype3D.Workshop },
                { "biological.building.spore-tower", FormalBuildingVisualArchetype3D.DefenseFoundation },
                { "biological.building.metabolic-furnace", FormalBuildingVisualArchetype3D.Processor },
                { "psionics.building.resonance-furnace", FormalBuildingVisualArchetype3D.Processor },
                { "psionics.building.workshop", FormalBuildingVisualArchetype3D.Workshop },
                { "psionics.building.mind-spire", FormalBuildingVisualArchetype3D.Tower },
                { "psionics.building.consciousness-network", FormalBuildingVisualArchetype3D.ResearchHub },
                { "core.building.laser-tower", FormalBuildingVisualArchetype3D.DefenseFoundation },
                { "biological.building.acid-tower", FormalBuildingVisualArchetype3D.Tower },
                { "psionics.building.shield-generator", FormalBuildingVisualArchetype3D.FieldArray },
                { "cultivation.building.spirit-gathering-array", FormalBuildingVisualArchetype3D.FieldArray },
                { "core.building.automated-repair-bay", FormalBuildingVisualArchetype3D.Workshop },
                { "cultivation.building.alchemy-chamber", FormalBuildingVisualArchetype3D.Workshop },
                { "cultivation.building.puppet-workshop", FormalBuildingVisualArchetype3D.Workshop },
                { "biological.building.behemoth-pen", FormalBuildingVisualArchetype3D.LargeEnclosure },
                { "bridge.building.psionic-mech-factory", FormalBuildingVisualArchetype3D.LargeEnclosure },
                { "bridge.building.high-frequency-sword-forge", FormalBuildingVisualArchetype3D.Workshop },
                { "bridge.building.bio-hangar", FormalBuildingVisualArchetype3D.LargeEnclosure },
                { "bridge.building.spirit-plant-garden", FormalBuildingVisualArchetype3D.FieldArray },
                { "bridge.building.emp-tower", FormalBuildingVisualArchetype3D.DefenseFoundation },
            };
        }
    }

    public static class FormalWorldPresentationScalePolicy3D
    {
        public static float ReferenceScale(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0)
                return 1f;
            return Mathf.Min(
                pixelWidth / FormalWorldPresentationScaleProfile3D.ReferenceWidth,
                pixelHeight / FormalWorldPresentationScaleProfile3D.ReferenceHeight);
        }

        public static float ResolvePhysicalPixels(
            float referencePixels,
            float minimumPhysicalPixels,
            float maximumPhysicalPixels,
            int pixelWidth,
            int pixelHeight)
        {
            if (!IsFinite(referencePixels) || referencePixels < 0f ||
                !IsFinite(minimumPhysicalPixels) ||
                !IsFinite(maximumPhysicalPixels) ||
                minimumPhysicalPixels < 0f ||
                maximumPhysicalPixels < minimumPhysicalPixels)
                return 0f;
            return Mathf.Clamp(
                referencePixels * ReferenceScale(pixelWidth, pixelHeight),
                minimumPhysicalPixels,
                maximumPhysicalPixels);
        }

        public static float WorldUnitsForPixels(
            float physicalPixels,
            float orthographicSize,
            int pixelHeight)
        {
            if (!IsFinite(physicalPixels) || physicalPixels < 0f ||
                !IsFinite(orthographicSize) || orthographicSize <= 0f ||
                pixelHeight <= 0)
                return 0f;
            return physicalPixels * 2f * orthographicSize / pixelHeight;
        }

        public static float WorldUnitScreenHeight(float orthographicSize)
        {
            return IsFinite(orthographicSize) && orthographicSize > 0f
                ? 1f / (2f * orthographicSize)
                : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
