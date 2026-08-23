using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Tests
{
    /// <summary>
    /// RED contract for the IDEA-0016 single-source recipe catalog.
    /// Reflection is intentional for fields that the production catalog does not
    /// expose yet: the test assembly must compile before the GREEN implementation.
    /// </summary>
    public sealed class ResourceRecipeCatalogIntegrityTests
    {
        private static readonly string[] ExpectedRecipeIds =
        {
            "core.production.extract-node-resource",
            "core.production.smelt-alloy",
            "core.production.assemble-ammunition",
            "core.crafting.field-alloy",
            "core.crafting.field-ammunition",
            "core.production.refine-stone",
            "core.production.mix-coolant",
            "core.production.spin-carbon-fiber",
            "technology.production.energy-cell",
            "technology.production.mechanical-component",
            "technology.production.control-chip",
            "technology.production.superconductive-coil",
            "cultivation.production.refine-spirit-iron",
            "cultivation.production.gather-spirit-stone",
            "cultivation.production.flying-sword",
            "cultivation.production.formation-core",
            "cultivation.production.elixir",
            "biological.production.biomass-concentrate",
            "biological.production.active-biomass",
            "biological.production.bone-steel",
            "biological.production.mutant-gene",
            "biological.production.acid-gland",
            "biological.production.weapon",
            "psionics.production.resonance-metal",
            "psionics.production.consciousness-shard",
            "psionics.production.amplifier",
            "psionics.production.psionic-crystal",
            "fusion.production.spirit-plant-extract",
            "fusion.production.flesh-elixir",
            "fusion.production.hybrid-core"
        };

        private static readonly string[] ManualRecipeIds =
        {
            "core.crafting.field-alloy",
            "core.crafting.field-ammunition"
        };

        private static readonly IReadOnlyDictionary<string, string[]>
            ExpectedBuildingIds = new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["core.production.extract-node-resource"] =
                    new[] { "core.building.mining-station" },
                ["core.production.smelt-alloy"] =
                    new[] { "core.building.smelter" },
                ["core.production.assemble-ammunition"] =
                    new[] { "core.building.assembler" },
                ["core.crafting.field-alloy"] = Array.Empty<string>(),
                ["core.crafting.field-ammunition"] = Array.Empty<string>(),
                ["core.production.refine-stone"] =
                    new[] { "core.building.smelter" },
                ["core.production.mix-coolant"] =
                    new[] { "core.building.assembler" },
                ["core.production.spin-carbon-fiber"] =
                    new[] { "core.building.assembler" },
                ["technology.production.energy-cell"] =
                    new[] { "core.building.assembler" },
                ["technology.production.mechanical-component"] =
                    new[] { "core.building.assembler" },
                ["technology.production.control-chip"] =
                    new[] { "core.building.assembler" },
                ["technology.production.superconductive-coil"] =
                    new[] { "core.building.assembler" },
                ["cultivation.production.refine-spirit-iron"] =
                    new[] { "cultivation.building.spirit-fire-furnace" },
                ["cultivation.production.gather-spirit-stone"] =
                    new[] { "cultivation.building.spirit-gathering-array" },
                ["cultivation.production.flying-sword"] =
                    new[] { "cultivation.building.artifact-workshop" },
                ["cultivation.production.formation-core"] =
                    new[] { "cultivation.building.artifact-workshop" },
                ["cultivation.production.elixir"] =
                    new[] { "cultivation.building.alchemy-chamber" },
                ["biological.production.biomass-concentrate"] =
                    new[] { "biological.building.colony-pool" },
                ["biological.production.active-biomass"] =
                    new[] { "biological.building.colony-pool" },
                ["biological.production.bone-steel"] =
                    new[] { "biological.building.colony-pool" },
                ["biological.production.mutant-gene"] =
                    new[] { "biological.building.breeding-chamber" },
                ["biological.production.acid-gland"] =
                    new[] { "biological.building.breeding-chamber" },
                ["biological.production.weapon"] =
                    new[] { "biological.building.breeding-chamber" },
                ["psionics.production.resonance-metal"] =
                    new[] { "psionics.building.resonance-furnace" },
                ["psionics.production.consciousness-shard"] =
                    new[] { "psionics.building.consciousness-network" },
                ["psionics.production.amplifier"] =
                    new[] { "psionics.building.workshop" },
                ["psionics.production.psionic-crystal"] =
                    new[] { "psionics.building.consciousness-network" },
                ["fusion.production.spirit-plant-extract"] =
                    new[] { "biological.building.breeding-chamber" },
                ["fusion.production.flesh-elixir"] =
                    new[] { "cultivation.building.alchemy-chamber" },
                ["fusion.production.hybrid-core"] =
                    new[] { "core.building.assembler" }
            };

        private static readonly IReadOnlyDictionary<string, string[]>
            ExpectedResearchIds = new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["core.production.extract-node-resource"] =
                    new[] { "core.research.scrap-processing" },
                ["core.production.smelt-alloy"] =
                    new[] { "core.research.automated-machinery" },
                ["core.production.assemble-ammunition"] =
                    new[] { "core.research.precision-assembly" },
                ["core.crafting.field-alloy"] =
                    new[] { "core.research.automated-machinery" },
                ["core.crafting.field-ammunition"] =
                    new[] { "core.research.precision-assembly" },
                ["core.production.refine-stone"] =
                    new[] { "core.research.automated-machinery" },
                ["core.production.mix-coolant"] =
                    new[] { "core.research.precision-assembly" },
                ["core.production.spin-carbon-fiber"] =
                    new[] { "core.research.precision-assembly" },
                ["technology.production.energy-cell"] =
                    new[] { "core.research.thermal-engineering" },
                ["technology.production.mechanical-component"] =
                    new[] { "core.research.precision-assembly" },
                ["technology.production.control-chip"] =
                    new[] { "core.research.unmanned-systems" },
                ["technology.production.superconductive-coil"] =
                    new[] { "core.research.energy-weapons" },
                ["cultivation.production.refine-spirit-iron"] =
                    new[] { "core.research.spirit-sensing" },
                ["cultivation.production.gather-spirit-stone"] =
                    new[] { "core.research.spirit-gathering" },
                ["cultivation.production.flying-sword"] =
                    new[] { "core.research.artifact-crafting" },
                ["cultivation.production.formation-core"] =
                    new[] { "core.research.formation-reinforcement" },
                ["cultivation.production.elixir"] =
                    new[] { "core.research.alchemy" },
                ["biological.production.biomass-concentrate"] =
                    new[] { "core.research.adaptive-tissue" },
                ["biological.production.active-biomass"] =
                    new[] { "core.research.adaptive-tissue" },
                ["biological.production.bone-steel"] =
                    new[] { "core.research.adaptive-tissue" },
                ["biological.production.mutant-gene"] =
                    new[] { "core.research.gene-splicing" },
                ["biological.production.acid-gland"] =
                    new[] { "core.research.acid-spit" },
                ["biological.production.weapon"] =
                    new[] { "core.research.bio-cultivation" },
                ["psionics.production.resonance-metal"] =
                    new[] { "core.research.mind-resonance" },
                ["psionics.production.consciousness-shard"] =
                    new[] { "core.research.consciousness-network" },
                ["psionics.production.amplifier"] =
                    new[] { "core.research.psionic-workshop" },
                ["psionics.production.psionic-crystal"] =
                    new[] { "core.research.collective-consciousness" },
                ["fusion.production.spirit-plant-extract"] =
                    new[] { "core.research.bridge.spirit-plant" },
                ["fusion.production.flesh-elixir"] =
                    new[] { "core.research.bridge.flesh-elixir" },
                ["fusion.production.hybrid-core"] = new[]
                {
                    "core.research.unmanned-systems",
                    "core.research.formation-reinforcement",
                    "core.research.gene-splicing",
                    "core.research.collective-consciousness"
                }
            };

        [Test]
        public void All_MatchesTheExactIdea0016StableRecipeOrderAndIsUnique()
        {
            string[] actual = ResourceRecipeCatalog.All
                .Select(definition => definition.Id)
                .ToArray();

            CollectionAssert.AreEqual(ExpectedRecipeIds, actual);
            Assert.That(actual.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(actual.Length));
        }

        [Test]
        public void Definitions_ExposePluralInputsOutputsBuildingsAndResearch()
        {
            AssertReadOnlySequenceProperty(
                typeof(ResourceRecipeDefinition),
                "Inputs",
                typeof(ResourceAmount));
            AssertReadOnlySequenceProperty(
                typeof(ResourceRecipeDefinition),
                "Outputs",
                typeof(ResourceAmount));
            AssertReadOnlySequenceProperty(
                typeof(ResourceRecipeDefinition),
                "AllowedBuildingIds",
                typeof(string));
            AssertReadOnlySequenceProperty(
                typeof(ResourceRecipeDefinition),
                "RequiredResearchIds",
                typeof(string));
            AssertPublicProperty(typeof(ResourceRecipeDefinition), "ChineseName");
            AssertPublicProperty(typeof(ResourceRecipeDefinition), "DefaultForBuilding");
            AssertPublicProperty(typeof(ResourceRecipeDefinition), "IconProjection");
            AssertPublicProperty(typeof(ResourceRecipeDefinition), "LoreBrief");

            AssertReadOnlySequenceProperty(
                typeof(FormalProductionDefinition),
                "Inputs",
                typeof(ResourceAmount));
            AssertReadOnlySequenceProperty(
                typeof(FormalProductionDefinition),
                "Outputs",
                typeof(ResourceAmount));
        }

        [Test]
        public void Recipes_HaveExactMachineManualBuildingAndResearchBoundaries()
        {
            foreach (ResourceRecipeDefinition recipe in ResourceRecipeCatalog.All)
            {
                bool expectedManual = ManualRecipeIds.Contains(
                    recipe.Id,
                    StringComparer.Ordinal);
                Assert.That(
                    recipe.Kind,
                    Is.EqualTo(expectedManual
                        ? ResourceRecipeKind.ManualCrafting
                        : ResourceRecipeKind.Machine),
                    recipe.Id);

                CollectionAssert.AreEqual(
                    ExpectedBuildingIds[recipe.Id],
                    ReflectedStrings(recipe, "AllowedBuildingIds"),
                    recipe.Id);
                CollectionAssert.AreEqual(
                    ExpectedResearchIds[recipe.Id],
                    ReflectedStrings(
                        recipe,
                        "RequiredResearchIds",
                        "RequiredResearchId"),
                    recipe.Id);
            }
        }

        [Test]
        public void EveryResourceBuildingAndResearchReference_ExistsInItsCatalog()
        {
            var resourceIds = new HashSet<string>(
                ResourceDefinitionCatalog.All.Select(value => value.Id),
                StringComparer.Ordinal);
            var buildingIds = new HashSet<string>(
                BuildingCatalog.All.Select(value => value.Id.Value),
                StringComparer.Ordinal);
            var researchIds = new HashSet<string>(
                ResearchCatalog.All.Select(value => value.Id.Value)
                    .Concat(DemoResearchCatalog.All.Select(value => value.Id.Value)),
                StringComparer.Ordinal);

            foreach (ResourceRecipeDefinition recipe in ResourceRecipeCatalog.All)
            {
                foreach (ResourceAmount amount in recipe.Inputs.Concat(recipe.Outputs))
                {
                    Assert.That(amount.Amount, Is.GreaterThan(0), recipe.Id);
                    Assert.That(
                        resourceIds.Contains(amount.ResourceId),
                        Is.True,
                        $"{recipe.Id} 引用未登记资源 {amount.ResourceId}");
                }

                foreach (string buildingId in ReflectedStrings(
                             recipe,
                             "AllowedBuildingIds"))
                {
                    Assert.That(
                        buildingIds.Contains(buildingId),
                        Is.True,
                        $"{recipe.Id} 引用未登记建筑 {buildingId}");
                }

                foreach (string researchId in ReflectedStrings(
                             recipe,
                             "RequiredResearchIds",
                             "RequiredResearchId"))
                {
                    Assert.That(
                        researchIds.Contains(researchId),
                        Is.True,
                        $"{recipe.Id} 引用未登记科技 {researchId}");
                }
            }
        }

        [Test]
        public void EveryNonMapResource_HasAMachineProductionSource()
        {
            var mapResourceIds = new HashSet<string>(
                ResourceDefinitionCatalog.BaseHudResourceIds,
                StringComparer.Ordinal);
            var machineOutputs = new HashSet<string>(
                ResourceRecipeCatalog.All
                    .Where(recipe => recipe.Kind == ResourceRecipeKind.Machine)
                    .SelectMany(recipe => recipe.Outputs)
                    .Select(output => output.ResourceId),
                StringComparer.Ordinal);

            foreach (ResourceDefinition resource in ResourceDefinitionCatalog.All)
            {
                if (mapResourceIds.Contains(resource.Id))
                    continue;

                Assert.That(
                    machineOutputs.Contains(resource.Id),
                    Is.True,
                    $"非地图资源 {resource.Id} 没有正式机器配方来源");
            }
        }

        [Test]
        public void EveryFormalResource_DeclaresAtLeastOneConcreteUse()
        {
            foreach (ResourceDefinition resource in ResourceDefinitionCatalog.All)
            {
                IReadOnlyList<object> useKinds = ReflectedObjects(
                    resource,
                    "UseKinds");
                Assert.That(
                    useKinds,
                    Is.Not.Empty,
                    $"{resource.Id} 未登记正式用途类别");
                Assert.That(
                    ReflectedString(resource, "UseSummary"),
                    Is.Not.Null.And.Not.Empty,
                    $"{resource.Id} 未说明正式用途");
            }
        }

        [Test]
        public void FusionRecipes_HaveMultipleRouteInputsOrMultiplePrerequisites()
        {
            string[] expectedFusionIds = ExpectedRecipeIds
                .Where(id => id.StartsWith(
                    "fusion.production.",
                    StringComparison.Ordinal))
                .ToArray();

            foreach (string recipeId in expectedFusionIds)
            {
                Assert.That(
                    ResourceRecipeCatalog.TryGet(
                        recipeId,
                        out ResourceRecipeDefinition recipe),
                    Is.True,
                    recipeId);

                int inputRouteCount = recipe.Inputs
                    .Select(input => RoutePrefix(input.ResourceId))
                    .Where(route => !string.Equals(
                        route,
                        "core",
                        StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                int prerequisiteCount = ReflectedStrings(
                    recipe,
                    "RequiredResearchIds",
                    "RequiredResearchId").Count;

                Assert.That(
                    inputRouteCount >= 2 || prerequisiteCount >= 2,
                    Is.True,
                    $"{recipeId} 必须有多路线输入或多个显式科技前置");
            }
        }

        [Test]
        public void EveryMachineBuilding_HasExactlyOneOrderedDefaultRecipe()
        {
            ResourceRecipeDefinition[] machines = ResourceRecipeCatalog.All
                .Where(recipe => recipe.Kind == ResourceRecipeKind.Machine)
                .ToArray();
            string[] usedBuildingIds = machines
                .SelectMany(recipe => ReflectedStrings(
                    recipe,
                    "AllowedBuildingIds"))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.That(usedBuildingIds, Is.Not.Empty);
            foreach (string buildingId in usedBuildingIds)
            {
                ResourceRecipeDefinition[] allowed = machines
                    .Where(recipe => ReflectedStrings(
                        recipe,
                        "AllowedBuildingIds").Contains(
                            buildingId,
                            StringComparer.Ordinal))
                    .ToArray();
                Assert.That(allowed, Is.Not.Empty, buildingId);
                Assert.That(
                    allowed.Count(recipe => ReflectedBool(
                        recipe,
                        "DefaultForBuilding")),
                    Is.EqualTo(1),
                    $"{buildingId} 必须且只能有一个默认配方");
            }
        }

        private static void AssertReadOnlySequenceProperty(
            Type ownerType,
            string propertyName,
            Type elementType)
        {
            PropertyInfo property = AssertPublicProperty(ownerType, propertyName);
            Assert.That(
                property.PropertyType.GetInterfaces()
                    .Concat(new[] { property.PropertyType })
                    .Any(type => type.IsGenericType &&
                        type.GetGenericTypeDefinition() ==
                        typeof(IReadOnlyList<>) &&
                        type.GetGenericArguments()[0] == elementType),
                Is.True,
                $"{ownerType.Name}.{propertyName} 必须是只读 {elementType.Name} 数组合同");
        }

        private static PropertyInfo AssertPublicProperty(
            Type ownerType,
            string propertyName)
        {
            PropertyInfo property = ownerType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                property,
                Is.Not.Null,
                $"{ownerType.Name} 缺少 IDEA-0016 字段 {propertyName}");
            return property;
        }

        private static IReadOnlyList<string> ReflectedStrings(
            object owner,
            string propertyName,
            string legacySinglePropertyName = null)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                object value = property.GetValue(owner);
                if (value is IEnumerable values)
                {
                    return values.Cast<object>()
                        .Select(item => item as string)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToArray();
                }
            }

            if (!string.IsNullOrWhiteSpace(legacySinglePropertyName))
            {
                string legacy = ReflectedString(
                    owner,
                    legacySinglePropertyName,
                    propertyRequired: false);
                if (!string.IsNullOrWhiteSpace(legacy))
                    return new[] { legacy };
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<object> ReflectedObjects(
            object owner,
            string propertyName)
        {
            PropertyInfo property = AssertPublicProperty(
                owner.GetType(),
                propertyName);
            object value = property.GetValue(owner);
            Assert.That(value, Is.Not.Null, propertyName);
            Assert.That(value, Is.InstanceOf<IEnumerable>(), propertyName);
            return ((IEnumerable)value).Cast<object>().ToArray();
        }

        private static string ReflectedString(
            object owner,
            string propertyName,
            bool propertyRequired = true)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (propertyRequired)
            {
                Assert.That(
                    property,
                    Is.Not.Null,
                    $"{owner.GetType().Name} 缺少字段 {propertyName}");
            }

            return property == null ? null : property.GetValue(owner) as string;
        }

        private static bool ReflectedBool(object owner, string propertyName)
        {
            PropertyInfo property = AssertPublicProperty(
                owner.GetType(),
                propertyName);
            object value = property.GetValue(owner);
            Assert.That(value, Is.TypeOf<bool>(), propertyName);
            return (bool)value;
        }

        private static string RoutePrefix(string stableId)
        {
            int separator = stableId == null ? -1 : stableId.IndexOf('.');
            return separator <= 0 ? string.Empty : stableId.Substring(0, separator);
        }
    }
}
