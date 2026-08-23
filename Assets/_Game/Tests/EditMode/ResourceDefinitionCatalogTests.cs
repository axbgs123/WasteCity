using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ResourceDefinitionCatalogTests
    {
        private static readonly string[] ExpectedLegacyStableOrder =
        {
            ResourceIds.Iron,
            ResourceIds.EnergyCrystal,
            ResourceIds.Stone,
            ResourceIds.Biomass,
            ResourceIds.Water,
            ResourceIds.Alloy,
            ResourceIds.Ammunition,
            ResourceIds.SpiritIron,
            ResourceIds.FlyingSword,
            ResourceIds.BoneSteel,
            ResourceIds.BiomassConcentrate,
            ResourceIds.BiologicalWeapon,
            ResourceIds.ResonanceMetal,
            ResourceIds.PsionicAmplifier,
            ResourceIds.Elixir
        };

        private static readonly string[] ExpectedAddedStableOrder =
        {
            "core.resource.refined-stone",
            "core.resource.coolant",
            "core.resource.carbon-fiber",
            "technology.resource.energy-cell",
            "technology.resource.mechanical-component",
            "technology.resource.control-chip",
            "technology.resource.superconductive-coil",
            "cultivation.resource.spirit-stone",
            "cultivation.resource.formation-core",
            "biological.resource.active-biomass",
            "biological.resource.mutant-gene",
            "biological.resource.acid-gland",
            "psionics.resource.consciousness-shard",
            "psionics.resource.psionic-crystal",
            "fusion.resource.spirit-plant-extract",
            "fusion.resource.hybrid-core"
        };

        private static readonly string[] ExpectedStableOrder =
            ExpectedLegacyStableOrder.Concat(ExpectedAddedStableOrder).ToArray();

        private static readonly int[] ExpectedDisplaySizesPx =
        {
            20,
            24,
            32,
            40,
            64
        };

        [Test]
        public void All_PreservesTheLegacyPrefixAndAppendsTheExactSixteenStableResourceIds()
        {
            Assert.That(ResourceDefinitionCatalog.All, Has.Count.EqualTo(31));
            CollectionAssert.AreEqual(
                ExpectedLegacyStableOrder,
                ResourceDefinitionCatalog.All
                    .Take(ExpectedLegacyStableOrder.Length)
                    .Select(value => value.Id)
                    .ToArray());
            CollectionAssert.AreEqual(
                ExpectedAddedStableOrder,
                ResourceDefinitionCatalog.All
                    .Skip(ExpectedLegacyStableOrder.Length)
                    .Select(value => value.Id)
                    .ToArray());
            CollectionAssert.AreEqual(
                ExpectedStableOrder,
                ResourceDefinitionCatalog.All.Select(value => value.Id).ToArray());
            Assert.That(
                ResourceDefinitionCatalog.All.Select(value => value.Id).Distinct().Count(),
                Is.EqualTo(ResourceDefinitionCatalog.All.Count));
        }

        [TestCase(ResourceIds.Iron, "铁矿")]
        [TestCase(ResourceIds.EnergyCrystal, "能晶")]
        [TestCase(ResourceIds.Stone, "石料")]
        [TestCase(ResourceIds.Biomass, "生物质")]
        [TestCase(ResourceIds.Water, "水")]
        [TestCase(ResourceIds.Alloy, "合金")]
        [TestCase(ResourceIds.Ammunition, "弹药")]
        [TestCase(ResourceIds.SpiritIron, "灵铁")]
        [TestCase(ResourceIds.FlyingSword, "飞剑")]
        [TestCase(ResourceIds.BoneSteel, "骨钢")]
        [TestCase(ResourceIds.BiomassConcentrate, "生物质浓缩液")]
        [TestCase(ResourceIds.BiologicalWeapon, "生物武器")]
        [TestCase(ResourceIds.ResonanceMetal, "共振金属")]
        [TestCase(ResourceIds.PsionicAmplifier, "灵能增幅器")]
        [TestCase(ResourceIds.Elixir, "灵丹")]
        [TestCase("core.resource.refined-stone", "精制石材")]
        [TestCase("core.resource.coolant", "冷却液")]
        [TestCase("core.resource.carbon-fiber", "碳纤维")]
        [TestCase("technology.resource.energy-cell", "能量电池")]
        [TestCase("technology.resource.mechanical-component", "机械组件")]
        [TestCase("technology.resource.control-chip", "控制芯片")]
        [TestCase("technology.resource.superconductive-coil", "超导线圈")]
        [TestCase("cultivation.resource.spirit-stone", "灵石")]
        [TestCase("cultivation.resource.formation-core", "阵法核心")]
        [TestCase("biological.resource.active-biomass", "活性生物质")]
        [TestCase("biological.resource.mutant-gene", "变异基因")]
        [TestCase("biological.resource.acid-gland", "酸腺")]
        [TestCase("psionics.resource.consciousness-shard", "意识碎片")]
        [TestCase("psionics.resource.psionic-crystal", "灵能结晶")]
        [TestCase("fusion.resource.spirit-plant-extract", "灵植精华")]
        [TestCase("fusion.resource.hybrid-core", "融合核心")]
        public void EveryFormalResource_HasApprovedNameAndStackLimit(
            string resourceId,
            string expectedChineseName)
        {
            Assert.That(
                ResourceDefinitionCatalog.TryGet(resourceId, out ResourceDefinition definition),
                Is.True);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Id, Is.EqualTo(resourceId));
            Assert.That(definition.ChineseName, Is.EqualTo(expectedChineseName));
            Assert.That(definition.StackLimit, Is.EqualTo(100));
        }

        [Test]
        public void EveryFormalResource_HasCompleteIdea0016Metadata()
        {
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                AssertReflectedValueIsNonEmpty(definition, "Route");
                AssertReflectedValueIsNonEmpty(definition, "Tier");
                AssertReflectedValueIsNonEmpty(definition, "SourceKinds");
                AssertReflectedValueIsNonEmpty(definition, "SourceSummary");
                AssertReflectedValueIsNonEmpty(definition, "UseKinds");
                AssertReflectedValueIsNonEmpty(definition, "UseSummary");
                AssertReflectedValueIsNonEmpty(definition, "DiscoveryRule");
                AssertReflectedValueExists(definition, "RequiredResearchIds");
                AssertReflectedValueIsNonEmpty(definition, "IconId");
                AssertReflectedValueIsNonEmpty(definition, "LoreBrief");
                AssertReflectedValueIsNonEmpty(definition, "VisualKeywords");
                AssertReflectedValueIsNonEmpty(
                    definition,
                    "ForbiddenVisualElements");

                object displaySizes = AssertReflectedValueIsNonEmpty(
                    definition,
                    "DisplaySizesPx");
                CollectionAssert.AreEqual(
                    ExpectedDisplaySizesPx,
                    ((IEnumerable)displaySizes).Cast<object>()
                        .Select(Convert.ToInt32)
                        .ToArray(),
                    definition.Id);

                string expectedIconId =
                    "art.icon.item." +
                    definition.Id.Substring(definition.Id.LastIndexOf('.') + 1);
                Assert.That(
                    AssertReflectedValueIsNonEmpty(definition, "IconId"),
                    Is.EqualTo(expectedIconId),
                    definition.Id);
            }
        }

        [Test]
        public void EveryFormalResource_HasANonEmptyUniqueStableIconFallbackKey()
        {
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                Assert.That(definition.IconFallbackKey, Is.Not.Null.And.Not.Empty);
                Assert.That(definition.IconFallbackKey, Is.EqualTo(definition.Id));
            }

            Assert.That(
                ResourceDefinitionCatalog.All
                    .Select(definition => definition.IconFallbackKey)
                    .Distinct()
                    .Count(),
                Is.EqualTo(ResourceDefinitionCatalog.All.Count));
        }

        [TestCase(ResourceIds.Iron, 20)]
        [TestCase(ResourceIds.EnergyCrystal, 0)]
        [TestCase(ResourceIds.Stone, 0)]
        [TestCase(ResourceIds.Biomass, 10)]
        [TestCase(ResourceIds.Water, 0)]
        [TestCase(ResourceIds.Alloy, 20)]
        [TestCase(ResourceIds.Ammunition, 30)]
        [TestCase(ResourceIds.SpiritIron, 0)]
        [TestCase(ResourceIds.FlyingSword, 0)]
        [TestCase(ResourceIds.BoneSteel, 0)]
        [TestCase(ResourceIds.BiomassConcentrate, 0)]
        [TestCase(ResourceIds.BiologicalWeapon, 0)]
        [TestCase(ResourceIds.ResonanceMetal, 0)]
        [TestCase(ResourceIds.PsionicAmplifier, 0)]
        [TestCase(ResourceIds.Elixir, 0)]
        public void EveryFormalResource_HasTheExactFormalInitialCityAmount(
            string resourceId,
            int expectedAmount)
        {
            Assert.That(
                ResourceDefinitionCatalog.TryGet(resourceId, out ResourceDefinition definition),
                Is.True);
            Assert.That(definition.FormalInitialCityAmount, Is.EqualTo(expectedAmount));
        }

        [Test]
        public void CreateFormalCityInventory_LoadsInitialAmountsIntoAWarehouseReadyLedger()
        {
            ResourceInventory inventory =
                ResourceDefinitionCatalog.CreateFormalCityInventory();

            Assert.That(inventory, Is.Not.Null);
            Assert.That(
                inventory.CapacityPerResource,
                Is.GreaterThanOrEqualTo(300));

            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                Assert.That(
                    inventory.Get(definition.Id),
                    Is.EqualTo(definition.FormalInitialCityAmount),
                    definition.Id);
            }
        }

        [Test]
        public void SourceMetadataSeparatesWorldEntryInitialInventoryAndFusionProduction()
        {
            ResourceDefinitionCatalog.TryGet(
                ResourceIds.Biomass,
                out ResourceDefinition biomass);
            ResourceDefinitionCatalog.TryGet(
                ResourceIds.Water,
                out ResourceDefinition water);
            ResourceDefinitionCatalog.TryGet(
                ResourceIds.SpiritPlantExtract,
                out ResourceDefinition spiritPlantExtract);
            ResourceDefinitionCatalog.TryGet(
                ResourceIds.HybridCore,
                out ResourceDefinition hybridCore);

            Assert.That(biomass.SourceKinds,
                Does.Contain(ResourceSourceKind.InitialInventory));
            Assert.That(
                water.SourceKinds.Contains(ResourceSourceKind.InitialInventory),
                Is.False);
            Assert.That(spiritPlantExtract.SourceKinds,
                Does.Contain(ResourceSourceKind.FusionProduction));
            Assert.That(hybridCore.SourceKinds,
                Does.Contain(ResourceSourceKind.FusionProduction));
        }

        [Test]
        public void SpiritPlantExtractDiscoveryUsesBothDirectRouteRequirements()
        {
            Assert.That(ResourceDefinitionCatalog.TryGet(
                ResourceIds.SpiritPlantExtract,
                out ResourceDefinition definition), Is.True);
            Assert.That(definition.DiscoveryRule,
                Is.EqualTo(ResourceDiscoveryRule.OwnedOrAllRequirements));
            CollectionAssert.AreEqual(
                new[]
                {
                    "core.research.artifact-crafting",
                    "core.research.bio-cultivation"
                },
                definition.RequiredResearchIds);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("unknown.resource.not-registered")]
        public void TryGet_EmptyOrUnknownIdFailsClosed(string resourceId)
        {
            Assert.That(
                ResourceDefinitionCatalog.TryGet(resourceId, out ResourceDefinition definition),
                Is.False);
            Assert.That(definition, Is.Null);
        }

        [Test]
        public void BaseHudResourceIds_PreserveTheApprovedPermanentOrder()
        {
            string[] expected =
            {
                ResourceIds.Iron,
                ResourceIds.EnergyCrystal,
                ResourceIds.Stone,
                ResourceIds.Biomass,
                ResourceIds.Water
            };

            CollectionAssert.AreEqual(expected, ResourceIds.Base);
            CollectionAssert.AreEqual(
                expected,
                ResourceDefinitionCatalog.BaseHudResourceIds);
        }

        private static object AssertReflectedValueExists(
            ResourceDefinition definition,
            string propertyName)
        {
            PropertyInfo property = typeof(ResourceDefinition).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                property,
                Is.Not.Null,
                $"{definition.Id} 缺少正式资源字段 {propertyName}");

            object value = property.GetValue(definition);
            Assert.That(
                value,
                Is.Not.Null,
                $"{definition.Id}.{propertyName} 不得为 null");
            return value;
        }

        private static object AssertReflectedValueIsNonEmpty(
            ResourceDefinition definition,
            string propertyName)
        {
            object value = AssertReflectedValueExists(definition, propertyName);
            if (value is string text)
            {
                Assert.That(
                    string.IsNullOrWhiteSpace(text),
                    Is.False,
                    $"{definition.Id}.{propertyName} 不得为空");
                return value;
            }

            if (value is IEnumerable values)
            {
                var materialized = new List<object>();
                foreach (object item in values)
                {
                    materialized.Add(item);
                }

                Assert.That(
                    materialized,
                    Is.Not.Empty,
                    $"{definition.Id}.{propertyName} 不得为空");
                Assert.That(
                    materialized,
                    Has.None.Null,
                    $"{definition.Id}.{propertyName} 不得包含 null");
                return value;
            }

            Assert.That(
                string.IsNullOrWhiteSpace(value.ToString()),
                Is.False,
                $"{definition.Id}.{propertyName} 不得为空");
            return value;
        }
    }
}
