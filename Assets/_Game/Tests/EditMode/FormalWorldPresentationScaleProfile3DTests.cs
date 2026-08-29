using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class FormalWorldPresentationScaleProfile3DTests
    {
        private const string ProfileTypeName =
            "WasteCity.Graybox3D.FormalWorldPresentationScaleProfile3D, " +
            "WasteCity.Graybox3D";
        private const string PolicyTypeName =
            "WasteCity.Graybox3D.FormalWorldPresentationScalePolicy3D, " +
            "WasteCity.Graybox3D";
        private const string MetricsTypeName =
            "WasteCity.Graybox3D.FormalBuildingVisualMetrics3D, " +
            "WasteCity.Graybox3D";
        private const string MarkerMetricsTypeName =
            "WasteCity.Graybox3D.FormalWorldMarkerMetrics3D, " +
            "WasteCity.Graybox3D";
        private const string ArchetypeTypeName =
            "WasteCity.Graybox3D.FormalBuildingVisualArchetype3D, " +
            "WasteCity.Graybox3D";
        private const string ResourcesPath =
            "Presentation/FormalWorldPresentationScaleProfile3D";

        private static readonly IReadOnlyDictionary<string, string>
            ExpectedArchetypes = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                { "core.building.mining-station", "ExtractorRig" },
                { "core.building.housing", "ResidentialBlock" },
                { "core.building.warehouse", "StorageBlock" },
                { "core.building.wall", "LowBarrier" },
                { "core.building.research-station", "ResearchHub" },
                { "core.building.smelter", "Processor" },
                { "core.building.assembler", "Processor" },
                { "core.building.machine-gun-turret", "DefenseFoundation" },
                { "core.building.heavy-machine-gun-turret", "Tower" },
                { "technology.building.power-plant", "Processor" },
                { "cultivation.building.spirit-fire-furnace", "Processor" },
                { "cultivation.building.artifact-workshop", "Workshop" },
                { "cultivation.building.sword-array-tower", "Tower" },
                { "cultivation.building.sword-riding-platform", "Tower" },
                { "biological.building.colony-pool", "Processor" },
                { "biological.building.breeding-chamber", "Workshop" },
                { "biological.building.spore-tower", "DefenseFoundation" },
                { "biological.building.metabolic-furnace", "Processor" },
                { "psionics.building.resonance-furnace", "Processor" },
                { "psionics.building.workshop", "Workshop" },
                { "psionics.building.mind-spire", "Tower" },
                { "psionics.building.consciousness-network", "ResearchHub" },
                { "core.building.laser-tower", "DefenseFoundation" },
                { "biological.building.acid-tower", "Tower" },
                { "psionics.building.shield-generator", "FieldArray" },
                { "cultivation.building.spirit-gathering-array", "FieldArray" },
                { "core.building.automated-repair-bay", "Workshop" },
                { "cultivation.building.alchemy-chamber", "Workshop" },
                { "cultivation.building.puppet-workshop", "Workshop" },
                { "biological.building.behemoth-pen", "LargeEnclosure" },
                { "bridge.building.psionic-mech-factory", "LargeEnclosure" },
                { "bridge.building.high-frequency-sword-forge", "Workshop" },
                { "bridge.building.bio-hangar", "LargeEnclosure" },
                { "bridge.building.spirit-plant-garden", "FieldArray" },
                { "bridge.building.emp-tower", "DefenseFoundation" },
            };

        [Test]
        public void IDEA0019_ProfileExposesFormalWorldScaleContract()
        {
            Type profile = RequireType(ProfileTypeName);
            Type metrics = RequireType(MetricsTypeName);
            Type markerMetrics = RequireType(MarkerMetricsTypeName);
            Type archetype = RequireType(ArchetypeTypeName);

            Assert.That(profile.IsSubclassOf(typeof(ScriptableObject)), Is.True);
            Assert.That(archetype.IsEnum, Is.True);
            Assert.That(
                Enum.GetNames(archetype),
                Is.EquivalentTo(new[]
                {
                    "LowBarrier",
                    "ResidentialBlock",
                    "StorageBlock",
                    "ExtractorRig",
                    "Processor",
                    "Workshop",
                    "ResearchHub",
                    "DefenseFoundation",
                    "Tower",
                    "FieldArray",
                    "LargeEnclosure",
                }));
            RequirePublicProperty(metrics, "Archetype", archetype);
            RequirePublicFloatProperty(metrics, "FootprintFillRatio");
            RequirePublicFloatProperty(metrics, "VisualHeightInCells");
            RequirePublicFloatProperty(metrics, "FoundationHeightRatio");
            RequirePublicFloatProperty(metrics, "UpperBodyWidthRatio");
            RequirePublicFloatProperty(metrics, "CrownHeightRatio");
            RequirePublicProperty(
                metrics,
                "DefenseOwnsSuperstructure",
                typeof(bool));
            RequirePublicProperty(
                markerMetrics,
                "Lod",
                typeof(ResourceNodeMarkerLod3D));
            RequirePublicFloatProperty(markerMetrics, "FrameReferencePixels");
            RequirePublicFloatProperty(markerMetrics, "IconReferencePixels");
            RequirePublicFloatProperty(markerMetrics, "TextReferencePixels");
            RequirePublicProperty(markerMetrics, "ShowFrame", typeof(bool));
            RequirePublicProperty(markerMetrics, "ShowName", typeof(bool));
            RequirePublicProperty(markerMetrics, "ShowAmount", typeof(bool));

            RequireMethod(
                profile,
                "TryResolveBuilding",
                typeof(bool),
                typeof(BuildingDefinition),
                metrics.MakeByRefType());
            RequireMethod(
                profile,
                "ResolveMarker",
                markerMetrics,
                typeof(float));
            RequireMethod(
                profile,
                "CellSize",
                typeof(float),
                typeof(BuildingSite));
            RequireMethod(
                profile,
                "TryValidate",
                typeof(bool),
                typeof(string).MakeByRefType());
        }

        [Test]
        public void IDEA0019_FormalAssetMapsEveryBuildingToApprovedArchetype()
        {
            object profile = LoadProfile();
            Type profileType = profile.GetType();
            Type metricsType = RequireType(MetricsTypeName);
            MethodInfo validate = RequireMethod(
                profileType,
                "TryValidate",
                typeof(bool),
                typeof(string).MakeByRefType());
            var validationArguments = new object[] { null };
            Assert.That(
                validate.Invoke(profile, validationArguments),
                Is.EqualTo(true),
                validationArguments[0] as string);

            Assert.That(BuildingCatalog.All, Has.Length.EqualTo(35));
            Assert.That(ExpectedArchetypes, Has.Count.EqualTo(35));
            Assert.That(
                BuildingCatalog.All.Select(value => value.Id.Value),
                Is.EquivalentTo(ExpectedArchetypes.Keys));

            MethodInfo resolve = RequireMethod(
                profileType,
                "TryResolveBuilding",
                typeof(bool),
                typeof(BuildingDefinition),
                metricsType.MakeByRefType());
            PropertyInfo archetype = RequirePublicProperty(
                metricsType,
                "Archetype",
                RequireType(ArchetypeTypeName));
            PropertyInfo delegatesSuperstructure = RequirePublicProperty(
                metricsType,
                "DefenseOwnsSuperstructure",
                typeof(bool));
            var delegatedIds = new List<string>();
            foreach (BuildingDefinition definition in BuildingCatalog.All)
            {
                var arguments = new object[] { definition, null };
                Assert.That(
                    resolve.Invoke(profile, arguments),
                    Is.EqualTo(true),
                    definition.Id.Value);
                object resolved = arguments[1];
                Assert.That(resolved, Is.Not.Null, definition.Id.Value);
                Assert.That(
                    archetype.GetValue(resolved).ToString(),
                    Is.EqualTo(ExpectedArchetypes[definition.Id.Value]),
                    definition.Id.Value);
                if ((bool)delegatesSuperstructure.GetValue(resolved))
                    delegatedIds.Add(definition.Id.Value);
            }

            Assert.That(
                delegatedIds,
                Is.EquivalentTo(new[]
                {
                    "core.building.machine-gun-turret",
                    "core.building.laser-tower",
                    "biological.building.spore-tower",
                    "bridge.building.emp-tower",
                }));
        }

        [Test]
        public void IDEA0019_ProfileUsesGroundAndInnerCellScaleForAllAxes()
        {
            object profile = LoadProfile();
            MethodInfo cellSize = RequireMethod(
                profile.GetType(),
                "CellSize",
                typeof(float),
                typeof(BuildingSite));

            Assert.That(
                cellSize.Invoke(profile, new object[] { BuildingSite.Ground }),
                Is.EqualTo(1f));
            Assert.That(
                cellSize.Invoke(profile, new object[] { BuildingSite.InnerCity }),
                Is.EqualTo(1f));

            Type profileType = profile.GetType();
            Assert.That(
                ReadPublicFloat(profile, profileType, "InnerVerticalEmphasis"),
                Is.EqualTo(1f).Within(.0001f));
        }

        [Test]
        public void IDEA0019_DefaultCameraUsesMidMarkerWithoutNames()
        {
            object profile = LoadProfile();
            Type policy = RequireType(PolicyTypeName);
            float unitScreenHeight = InvokeStaticFloat(
                policy,
                "WorldUnitScreenHeight",
                new[] { typeof(float) },
                13f);
            Assert.That(unitScreenHeight, Is.EqualTo(1f / 26f).Within(.0001f));

            MethodInfo resolve = RequireMethod(
                profile.GetType(),
                "ResolveMarker",
                RequireType(MarkerMetricsTypeName),
                typeof(float));
            object metrics = resolve.Invoke(
                profile,
                new object[] { unitScreenHeight });
            Assert.That(ReadProperty(metrics, "Lod").ToString(), Is.EqualTo("Mid"));
            Assert.That(ReadProperty(metrics, "ShowFrame"), Is.EqualTo(true));
            Assert.That(ReadProperty(metrics, "ShowName"), Is.EqualTo(false));
            Assert.That(ReadProperty(metrics, "ShowAmount"), Is.EqualTo(true));
            Assert.That(ReadFloat(metrics, "FrameReferencePixels"), Is.EqualTo(56f));
            Assert.That(ReadFloat(metrics, "IconReferencePixels"), Is.EqualTo(42f));
            Assert.That(ReadFloat(metrics, "TextReferencePixels"), Is.EqualTo(20f));
        }

        [Test]
        public void IDEA0019_ProjectionPolicyClampsReferencePixelsAcrossDesktopResolutions()
        {
            Type policy = RequireType(PolicyTypeName);
            Type[] resolutionParameters = { typeof(int), typeof(int) };
            Assert.That(
                InvokeStaticFloat(policy, "ReferenceScale", resolutionParameters,
                    1280, 720),
                Is.EqualTo(2f / 3f).Within(.0001f));
            Assert.That(
                InvokeStaticFloat(policy, "ReferenceScale", resolutionParameters,
                    1920, 1080),
                Is.EqualTo(1f).Within(.0001f));
            Assert.That(
                InvokeStaticFloat(policy, "ReferenceScale", resolutionParameters,
                    3840, 2160),
                Is.EqualTo(2f).Within(.0001f));

            Type[] physicalParameters =
            {
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(int),
                typeof(int),
            };
            Assert.That(
                InvokeStaticFloat(
                    policy,
                    "ResolvePhysicalPixels",
                    physicalParameters,
                    36f, 24f, 56f, 1280, 720),
                Is.EqualTo(24f).Within(.0001f));
            Assert.That(
                InvokeStaticFloat(
                    policy,
                    "ResolvePhysicalPixels",
                    physicalParameters,
                    36f, 24f, 56f, 1920, 1080),
                Is.EqualTo(36f).Within(.0001f));
            Assert.That(
                InvokeStaticFloat(
                    policy,
                    "ResolvePhysicalPixels",
                    physicalParameters,
                    36f, 24f, 56f, 3840, 2160),
                Is.EqualTo(56f).Within(.0001f));

            float worldHeight = InvokeStaticFloat(
                policy,
                "WorldUnitsForPixels",
                new[] { typeof(float), typeof(float), typeof(int) },
                36f, 13f, 1080);
            Assert.That(worldHeight, Is.EqualTo(36f * 26f / 1080f).Within(.0001f));
        }

        [Test]
        public void IDEA0019_ResourceMarkerExposesProfileDrivenRendererSizing()
        {
            Type markerMetrics = RequireType(MarkerMetricsTypeName);
            MethodInfo apply = RequireMethod(
                typeof(GrayboxResourceNodeMarker3D),
                "ApplyPresentation",
                typeof(void),
                markerMetrics,
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(bool),
                typeof(bool));

            Assert.That(apply.IsPublic, Is.True);
            Assert.That(apply.IsStatic, Is.False);
        }

        [Test]
        public void IDEA0019_FormalUiProfileOwnsApprovedIconHierarchy()
        {
            object profile = FormalUiLayoutProfile3D.Standard;
            Type type = profile.GetType();
            var expected = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                { "IconInline", 16f },
                { "IconCompact", 20f },
                { "IconRow", 24f },
                { "IconSlot", 32f },
                { "IconNode", 48f },
                { "IconHero", 64f },
            };
            foreach (KeyValuePair<string, float> pair in expected)
            {
                Assert.That(
                    ReadPublicFloat(profile, type, pair.Key),
                    Is.EqualTo(pair.Value),
                    pair.Key);
            }

            Assert.That(
                expected.Values,
                Is.Ordered.Ascending,
                "Icon roles must increase monotonically with information weight.");
            Assert.That(
                expected["IconHero"] / 108f,
                Is.LessThanOrEqualTo(.65f),
                "The building catalog hero icon must not dominate its 108 px row.");
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            Assert.That(
                type,
                Is.Not.Null,
                assemblyQualifiedName + " must exist for IDEA-0019.");
            return type;
        }

        private static object LoadProfile()
        {
            Type profileType = RequireType(ProfileTypeName);
            UnityEngine.Object asset = Resources.Load(ResourcesPath, profileType);
            Assert.That(
                asset,
                Is.Not.Null,
                ResourcesPath + " must contain the formal scale profile.");
            return asset;
        }

        private static MethodInfo RequireMethod(
            Type owner,
            string name,
            Type returnType,
            params Type[] parameterTypes)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType), name);
            return method;
        }

        private static PropertyInfo RequirePublicProperty(
            Type owner,
            string name,
            Type propertyType)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(property.PropertyType, Is.EqualTo(propertyType), name);
            Assert.That(property.CanRead, Is.True, name);
            return property;
        }

        private static void RequirePublicFloatProperty(Type owner, string name)
        {
            RequirePublicProperty(owner, name, typeof(float));
        }

        private static float ReadPublicFloat(
            object owner,
            Type ownerType,
            string name)
        {
            return Convert.ToSingle(
                RequirePublicProperty(ownerType, name, typeof(float))
                    .GetValue(owner));
        }

        private static object ReadProperty(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(
                property,
                Is.Not.Null,
                owner.GetType().FullName + "." + name);
            return property.GetValue(owner);
        }

        private static float ReadFloat(object owner, string name)
        {
            return Convert.ToSingle(ReadProperty(owner, name));
        }

        private static float InvokeStaticFloat(
            Type owner,
            string name,
            Type[] parameterTypes,
            params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                owner,
                name,
                typeof(float),
                parameterTypes);
            Assert.That(method.IsStatic, Is.True, owner.FullName + "." + name);
            return Convert.ToSingle(method.Invoke(null, arguments));
        }
    }
}
