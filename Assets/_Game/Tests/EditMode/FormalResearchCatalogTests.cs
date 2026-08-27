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
    /// IDEA-0016 formal research catalog contract. New metadata is read through
    /// reflection so the RED suite still compiles before the production model is
    /// expanded.
    /// </summary>
    public sealed class FormalResearchCatalogTests
    {
        private const string ScrapProcessingId =
            "core.research.scrap-processing";
        private const string LegacyAnalysisId =
            "core.research.legacy-analysis";
        private const string ReinforcedStructuresId =
            "core.research.reinforced-structures";

        private static readonly ExpectedNode[] Expected =
        {
            Node(0, ScrapProcessingId, "Common", 0, 0,
                "InitiallyCompleted", Array.Empty<string>(),
                "building:core.building.mining-station",
                "building:core.building.research-station",
                "building:core.building.housing",
                "building:core.building.warehouse",
                "building:core.building.wall"),

            Node(1, "core.research.automated-machinery", "Technology", 1, 1,
                "Researchable", Req(ScrapProcessingId),
                "building:core.building.smelter",
                "recipe:core.production.smelt-alloy",
                "recipe:core.crafting.field-alloy"),
            Node(2, "core.research.spirit-sensing", "Cultivation", 1, 1,
                "Researchable", Req(ScrapProcessingId),
                "building:cultivation.building.spirit-fire-furnace",
                "recipe:cultivation.production.refine-spirit-iron"),
            Node(3, "core.research.adaptive-tissue", "Biological", 1, 1,
                "Researchable", Req(ScrapProcessingId),
                "building:biological.building.colony-pool",
                "recipe:biological.production.biomass-concentrate",
                "recipe:biological.production.active-biomass",
                "recipe:biological.production.bone-steel"),
            Node(4, "core.research.mind-resonance", "Psionics", 1, 1,
                "Researchable", Req(ScrapProcessingId),
                "building:psionics.building.resonance-furnace",
                "recipe:psionics.production.resonance-metal"),

            Node(5, "core.research.precision-assembly", "Technology", 2, 2,
                "Researchable", Req("core.research.automated-machinery"),
                "building:core.building.assembler",
                "recipe:core.production.assemble-ammunition",
                "recipe:core.crafting.field-ammunition"),
            Node(6, "core.research.automated-defense", "Technology", 2, 2,
                "Researchable", Req("core.research.automated-machinery"),
                "building:core.building.machine-gun-turret"),
            Node(7, "core.research.thermal-engineering", "Technology", 2, 2,
                "Researchable", Req("core.research.automated-machinery"),
                "building:technology.building.power-plant",
                "recipe:technology.production.energy-cell"),
            Node(8, "core.research.ballistics", "Technology", 2, 2,
                "PreviewOnly", Req("core.research.automated-machinery"),
                "rule:core.effect.ballistics"),

            Node(9, "core.research.artifact-crafting", "Cultivation", 2, 2,
                "Researchable", Req("core.research.spirit-sensing"),
                "building:cultivation.building.artifact-workshop",
                "recipe:cultivation.production.flying-sword"),
            Node(10, "core.research.sword-array", "Cultivation", 2, 2,
                "PreviewOnly", Req("core.research.spirit-sensing"),
                "building:cultivation.building.sword-array-tower"),
            Node(11, "core.research.spirit-gathering", "Cultivation", 2, 2,
                "Researchable", Req("core.research.spirit-sensing"),
                "building:cultivation.building.spirit-gathering-array",
                "recipe:cultivation.production.gather-spirit-stone"),
            Node(12, "core.research.talisman-basics", "Cultivation", 2, 2,
                "PreviewOnly", Req("core.research.spirit-sensing"),
                "rule:core.effect.wall-talisman"),

            Node(13, "core.research.bio-cultivation", "Biological", 2, 2,
                "Researchable", Req("core.research.adaptive-tissue"),
                "building:biological.building.breeding-chamber",
                "recipe:biological.production.weapon"),
            Node(14, "core.research.spore-dispersal", "Biological", 2, 2,
                "PreviewOnly", Req("core.research.adaptive-tissue"),
                "building:biological.building.spore-tower"),
            Node(15, "core.research.metabolic-acceleration", "Biological", 2, 2,
                "Researchable", Req("core.research.adaptive-tissue"),
                "building:biological.building.metabolic-furnace",
                "rule:biological.effect.corpse-recovery-150-percent"),
            Node(16, "core.research.carapace-growth", "Biological", 2, 2,
                "Researchable", Req("core.research.adaptive-tissue"),
                "rule:biological.effect.wall-carapace-regeneration"),

            Node(17, "core.research.psionic-workshop", "Psionics", 2, 2,
                "Researchable", Req("core.research.mind-resonance"),
                "building:psionics.building.workshop",
                "recipe:psionics.production.amplifier"),
            Node(18, "core.research.mind-spire", "Psionics", 2, 2,
                "PreviewOnly", Req("core.research.mind-resonance"),
                "building:psionics.building.mind-spire"),
            Node(19, "core.research.consciousness-network", "Psionics", 2, 2,
                "Researchable", Req("core.research.mind-resonance"),
                "building:psionics.building.consciousness-network",
                "recipe:psionics.production.consciousness-shard"),
            Node(20, "core.research.thought-acceleration", "Psionics", 2, 2,
                "Researchable", Req("core.research.mind-resonance"),
                "rule:psionics.effect.research-speed-125-percent"),

            Node(21, "core.research.alloy-armor", "Technology", 3, 3,
                "PreviewOnly", Req("core.research.precision-assembly"),
                "rule:core.effect.alloy-armor",
                "building:core.building.heavy-machine-gun-turret"),
            Node(22, "core.research.unmanned-systems", "Technology", 3, 3,
                "PreviewOnly", Req("core.research.automated-defense"),
                "building:core.building.automated-repair-bay",
                "rule:core.effect.scout-drone"),
            Node(23, "core.research.orbital-supply", "Technology", 3, 3,
                "Researchable", Req("core.research.thermal-engineering"),
                "rule:core.effect.logistics-range-24"),
            Node(24, "core.research.energy-weapons", "Technology", 3, 3,
                "PreviewOnly", Req("core.research.ballistics"),
                "building:core.building.laser-tower",
                "rule:core.effect.technology-overload"),

            Node(25, "core.research.sword-riding", "Cultivation", 3, 3,
                "PreviewOnly", Req("core.research.sword-array"),
                "building:cultivation.building.sword-riding-platform",
                "rule:core.effect.flying-sword-range"),
            Node(26, "core.research.alchemy", "Cultivation", 3, 3,
                "Researchable", Req("core.research.artifact-crafting"),
                "building:cultivation.building.alchemy-chamber",
                "recipe:cultivation.production.elixir"),
            Node(27, "core.research.formation-reinforcement", "Cultivation", 3, 3,
                "Researchable", Req("core.research.spirit-gathering"),
                "rule:cultivation.effect.logistics-range-12",
                "rule:cultivation.effect.spirit-output-150-percent"),
            Node(28, "core.research.puppetry", "Cultivation", 3, 3,
                "PreviewOnly", Req("core.research.talisman-basics"),
                "building:cultivation.building.puppet-workshop",
                "rule:cultivation.effect.puppet-unit"),

            Node(29, "core.research.behemoth-breeding", "Biological", 3, 3,
                "PreviewOnly", Req("core.research.bio-cultivation"),
                "building:biological.building.behemoth-pen",
                "rule:biological.effect.behemoth-unit"),
            Node(30, "core.research.acid-spit", "Biological", 3, 3,
                "PreviewOnly", Req("core.research.spore-dispersal"),
                "building:biological.building.acid-tower",
                "rule:biological.effect.armor-corrosion"),
            Node(31, "core.research.tissue-regeneration", "Biological", 3, 3,
                "Researchable", Req("core.research.metabolic-acceleration"),
                "rule:biological.effect.building-and-unit-regeneration"),
            Node(32, "core.research.gene-splicing", "Biological", 3, 3,
                "PreviewOnly", Req("core.research.carapace-growth"),
                "rule:biological.effect.leader-temporary-trait"),

            Node(33, "core.research.mind-shield", "Psionics", 3, 3,
                "PreviewOnly", Req("core.research.psionic-workshop"),
                "building:psionics.building.shield-generator",
                "rule:psionics.effect.city-damage-shield"),
            Node(34, "core.research.mind-control", "Psionics", 3, 3,
                "PreviewOnly", Req("core.research.mind-spire"),
                "rule:psionics.effect.control-normal-enemy"),
            Node(35, "core.research.precognitive-sense", "Psionics", 3, 3,
                "Researchable", Req("core.research.consciousness-network"),
                "rule:psionics.effect.warning-time-150-percent"),
            Node(36, "core.research.collective-consciousness", "Psionics", 3, 3,
                "PreviewOnly", Req("core.research.thought-acceleration"),
                "recipe:psionics.production.psionic-crystal",
                "rule:psionics.effect.multi-city-shared-progress-20-percent"),

            Node(37, "core.research.bridge.psionic-mech", "Bridge", 3, 4,
                "PreviewOnly", Req("core.research.precision-assembly",
                    "core.research.psionic-workshop"),
                "building:bridge.building.psionic-mech-factory"),
            Node(38, "core.research.bridge.high-frequency-sword", "Bridge", 3, 4,
                "PreviewOnly", Req("core.research.artifact-crafting",
                    "core.research.precision-assembly"),
                "building:bridge.building.high-frequency-sword-forge"),
            Node(39, "core.research.bridge.bio-hangar", "Bridge", 3, 4,
                "PreviewOnly", Req("core.research.bio-cultivation",
                    "core.research.precision-assembly"),
                "building:bridge.building.bio-hangar"),
            Node(40, "core.research.bridge.spirit-plant", "Bridge", 3, 4,
                "PreviewOnly", Req("core.research.artifact-crafting",
                    "core.research.bio-cultivation"),
                "building:bridge.building.spirit-plant-garden",
                "recipe:fusion.production.spirit-plant-extract"),
            Node(41, "core.research.bridge.psionic-pulse", "Bridge", 3, 4,
                "PreviewOnly", Req("core.research.psionic-workshop",
                    "core.research.precision-assembly"),
                "building:bridge.building.emp-tower"),
            Node(42, "core.research.bridge.flesh-elixir", "Bridge", 3, 4,
                "PreviewOnly", Req("core.research.bio-cultivation",
                    "core.research.artifact-crafting"),
                "recipe:fusion.production.flesh-elixir",
                "rule:bridge.effect.elixir-triple-with-mutation-risk"),
            Node(43, LegacyAnalysisId, "Technology", 3, 4,
                "Researchable", Req("core.research.automated-defense"),
                "progression:core.progression.legacy-analysis"),
        };

        [Test]
        public void IDEA0020_FormalCatalogContainsFortyFourNodesIncludingLegacyAnalysis()
        {
            ResearchDefinition[] definitions = ResearchCatalog.All;
            string[] ids = definitions.Select(value => value.Id.Value).ToArray();

            Assert.That(ids, Has.Length.EqualTo(44));
            Assert.That(ids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(44));
            CollectionAssert.AreEqual(
                Expected.Select(value => value.Id).ToArray(),
                ids,
                "Formal order must be the approved CatalogOrder 0..43 order.");
            CollectionAssert.Contains(ids, LegacyAnalysisId);
            CollectionAssert.DoesNotContain(ids, ReinforcedStructuresId);
        }

        [Test]
        public void IDEA0020_LegacyAnalysisHasApprovedFormalCostAndDuration()
        {
            ResearchDefinition legacy = ResearchCatalog.Find(LegacyAnalysisId);
            Assert.That(legacy, Is.Not.Null);
            Assert.That(legacy.Name, Is.EqualTo("遗产解析"));
            Assert.That(legacy.Duration, Is.EqualTo(60f));
            Assert.That(legacy.ReleaseState,
                Is.EqualTo(ResearchReleaseState.Researchable));
            CollectionAssert.AreEqual(
                new[] { "core.research.automated-defense" },
                legacy.RequiredResearchIds);
            Assert.That(legacy.Costs.Select(value =>
                    value.ResourceId + ":" + value.Amount),
                Is.EqualTo(new[]
                {
                    ResourceIds.Alloy + ":30",
                    ResourceIds.Biomass + ":20",
                }));
        }

        [Test]
        public void FormalCatalogHasOneCommonRootExpandedTechnologyAndSixBridges()
        {
            ResearchDefinition[] definitions = ResearchCatalog.All;

            Assert.That(CountRoute(definitions, "Common"), Is.EqualTo(1));
            Assert.That(CountRoute(definitions, "Technology"), Is.EqualTo(10));
            Assert.That(CountRoute(definitions, "Cultivation"), Is.EqualTo(9));
            Assert.That(CountRoute(definitions, "Biological"), Is.EqualTo(9));
            Assert.That(CountRoute(definitions, "Psionics"), Is.EqualTo(9));
            Assert.That(CountRoute(definitions, "Bridge"), Is.EqualTo(6));

            ResearchDefinition root = ResearchCatalog.Find(ScrapProcessingId);
            Assert.That(root, Is.Not.Null, "The formal tree needs its common root.");
            Assert.That(ReadString(root, "Route"), Is.EqualTo("Common"));
            Assert.That(ReadInt(root, "Tier"), Is.Zero);
            Assert.That(ReadInt(root, "LayoutRow"), Is.Zero);
            Assert.That(ReadStrings(root, "RequiredResearchIds"), Is.Empty);
            Assert.That(ReadString(root, "ReleaseState"),
                Is.EqualTo("InitiallyCompleted"));
        }

        [Test]
        public void FormalMetadataIsCompleteAndMatchesTheApprovedCatalog()
        {
            ResearchDefinition[] definitions = ResearchCatalog.All;
            var byId = definitions.ToDictionary(
                value => value.Id.Value,
                StringComparer.Ordinal);
            var nameKeys = new HashSet<string>(StringComparer.Ordinal);
            var briefKeys = new HashSet<string>(StringComparer.Ordinal);
            var iconIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (ExpectedNode expected in Expected)
            {
                Assert.That(byId.TryGetValue(expected.Id, out ResearchDefinition actual),
                    Is.True, expected.Id);
                Assert.That(ReadInt(actual, "CatalogOrder"), Is.EqualTo(expected.Order),
                    expected.Id);
                Assert.That(ReadString(actual, "Route"), Is.EqualTo(expected.Route),
                    expected.Id);
                Assert.That(ReadInt(actual, "Tier"), Is.EqualTo(expected.Tier),
                    expected.Id);
                Assert.That(ReadInt(actual, "LayoutRow"), Is.EqualTo(expected.LayoutRow),
                    expected.Id);
                Assert.That(ReadString(actual, "ReleaseState"),
                    Is.EqualTo(expected.ReleaseState), expected.Id);
                CollectionAssert.AreEqual(
                    expected.Requirements,
                    ReadStrings(actual, "RequiredResearchIds"),
                    expected.Id);
                CollectionAssert.AreEqual(
                    expected.EffectReferences,
                    ReadStrings(actual, "EffectReferences"),
                    expected.Id);

                string iconId = ReadString(actual, "IconId");
                Assert.That(iconId, Is.EqualTo(ExpectedIconId(expected.Id)), expected.Id);
                Assert.That(iconIds.Add(iconId), Is.True, expected.Id);

                string nameKey = ReadString(actual, "NameKey");
                string briefKey = ReadString(actual, "BriefKey");
                Assert.That(nameKey, Is.Not.Null.And.Not.Empty, expected.Id);
                Assert.That(briefKey, Is.Not.Null.And.Not.Empty, expected.Id);
                Assert.That(nameKeys.Add(nameKey), Is.True,
                    $"Duplicate NameKey: {nameKey}");
                Assert.That(briefKeys.Add(briefKey), Is.True,
                    $"Duplicate BriefKey: {briefKey}");
                Assert.That(ReadString(actual, "NameKey"), Is.EqualTo(nameKey),
                    $"NameKey must be deterministic: {expected.Id}");
                Assert.That(ReadString(actual, "BriefKey"), Is.EqualTo(briefKey),
                    $"BriefKey must be deterministic: {expected.Id}");
            }
        }

        [Test]
        public void EveryPrerequisiteExistsAndFormalTreeIsAcyclic()
        {
            ResearchDefinition[] definitions = ResearchCatalog.All;
            var byId = definitions.ToDictionary(
                value => value.Id.Value,
                StringComparer.Ordinal);

            foreach (ResearchDefinition definition in definitions)
            {
                foreach (string requiredId in
                    ReadStrings(definition, "RequiredResearchIds"))
                {
                    Assert.That(requiredId, Is.Not.EqualTo(definition.Id.Value),
                        definition.Id.Value);
                    Assert.That(byId.ContainsKey(requiredId), Is.True,
                        $"Unknown prerequisite {requiredId} on {definition.Id.Value}");
                }
            }

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in byId.Keys)
                Visit(id, byId, visiting, visited);
        }

        [Test]
        public void EveryBridgeHasTwoDistinctTierTwoRoutePrerequisites()
        {
            ResearchDefinition[] definitions = ResearchCatalog.All;
            var byId = definitions.ToDictionary(
                value => value.Id.Value,
                StringComparer.Ordinal);
            ResearchDefinition[] bridges = definitions
                .Where(value => ReadString(value, "Route") == "Bridge")
                .ToArray();

            Assert.That(bridges, Has.Length.EqualTo(6));
            foreach (ResearchDefinition bridge in bridges)
            {
                string[] requirements = ReadStrings(
                    bridge,
                    "RequiredResearchIds");
                Assert.That(requirements, Has.Length.EqualTo(2), bridge.Id.Value);
                Assert.That(requirements.Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(2), bridge.Id.Value);
                Assert.That(ReadInt(bridge, "Tier"), Is.EqualTo(3), bridge.Id.Value);
                Assert.That(ReadInt(bridge, "LayoutRow"), Is.EqualTo(4), bridge.Id.Value);
                Assert.That(ReadString(bridge, "ReleaseState"),
                    Is.EqualTo("PreviewOnly"), bridge.Id.Value);

                ResearchDefinition first = byId[requirements[0]];
                ResearchDefinition second = byId[requirements[1]];
                Assert.That(ReadInt(first, "Tier"), Is.EqualTo(2), bridge.Id.Value);
                Assert.That(ReadInt(second, "Tier"), Is.EqualTo(2), bridge.Id.Value);
                Assert.That(ReadString(first, "Route"),
                    Is.Not.EqualTo(ReadString(second, "Route")), bridge.Id.Value);
                Assert.That(ReadString(first, "Route"),
                    Is.Not.EqualTo("Common").And.Not.EqualTo("Bridge"), bridge.Id.Value);
                Assert.That(ReadString(second, "Route"),
                    Is.Not.EqualTo("Common").And.Not.EqualTo("Bridge"), bridge.Id.Value);
            }
        }

        [Test]
        public void ReleasedEffectReferencesResolveToFormalContentCatalogs()
        {
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                string[] effects = ReadStrings(
                    definition,
                    "EffectReferences");
                foreach (string effect in effects)
                {
                    if (effect.StartsWith("recipe:", StringComparison.Ordinal))
                    {
                        string recipeId = effect.Substring("recipe:".Length);
                        Assert.That(
                            ResourceRecipeCatalog.TryGet(
                                recipeId,
                                out ResourceRecipeDefinition _),
                            Is.True,
                            $"Unknown recipe effect {effect} on {definition.Id.Value}");
                    }

                    if (effect.StartsWith("building:", StringComparison.Ordinal) &&
                        ReadString(definition, "ReleaseState") != "PreviewOnly")
                    {
                        string buildingId = effect.Substring("building:".Length);
                        Assert.That(
                            BuildingCatalog.All.Any(value =>
                                value.Id.Value == buildingId),
                            Is.True,
                            $"Unknown building effect {effect} on {definition.Id.Value}");
                    }
                }
            }
        }

        private static int CountRoute(
            IEnumerable<ResearchDefinition> definitions,
            string route)
        {
            return definitions.Count(value =>
                string.Equals(
                    ReadString(value, "Route"),
                    route,
                    StringComparison.Ordinal));
        }

        private static void Visit(
            string id,
            IReadOnlyDictionary<string, ResearchDefinition> byId,
            ISet<string> visiting,
            ISet<string> visited)
        {
            if (visited.Contains(id)) return;
            Assert.That(visiting.Add(id), Is.True,
                $"Research prerequisite cycle reaches {id}.");
            foreach (string requiredId in
                ReadStrings(byId[id], "RequiredResearchIds"))
            {
                Visit(requiredId, byId, visiting, visited);
            }
            visiting.Remove(id);
            visited.Add(id);
        }

        private static object ReadMember(object target, string memberName)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public);
            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property ?? (MemberInfo)field, Is.Not.Null,
                $"{type.Name} must expose public {memberName} for IDEA-0016.");
            return property != null
                ? property.GetValue(target, null)
                : field.GetValue(target);
        }

        private static int ReadInt(object target, string memberName)
        {
            return Convert.ToInt32(ReadMember(target, memberName));
        }

        private static string ReadString(object target, string memberName)
        {
            object value = ReadMember(target, memberName);
            return value == null ? null : value.ToString();
        }

        private static string[] ReadStrings(object target, string memberName)
        {
            object value = ReadMember(target, memberName);
            Assert.That(value, Is.InstanceOf<IEnumerable>(),
                $"{memberName} must be enumerable.");
            return ((IEnumerable)value)
                .Cast<object>()
                .Select(item => item == null ? null : item.ToString())
                .ToArray();
        }

        private static string ExpectedIconId(string researchId)
        {
            const string prefix = "core.research.";
            Assert.That(researchId.StartsWith(prefix, StringComparison.Ordinal),
                Is.True, researchId);
            return "art.icon.research." + researchId.Substring(prefix.Length);
        }

        private static string[] Req(params string[] ids)
        {
            return ids;
        }

        private static ExpectedNode Node(
            int order,
            string id,
            string route,
            int tier,
            int layoutRow,
            string releaseState,
            string[] requirements,
            params string[] effects)
        {
            return new ExpectedNode(
                order,
                id,
                route,
                tier,
                layoutRow,
                releaseState,
                requirements,
                effects);
        }

        private sealed class ExpectedNode
        {
            public ExpectedNode(
                int order,
                string id,
                string route,
                int tier,
                int layoutRow,
                string releaseState,
                string[] requirements,
                string[] effectReferences)
            {
                Order = order;
                Id = id;
                Route = route;
                Tier = tier;
                LayoutRow = layoutRow;
                ReleaseState = releaseState;
                Requirements = requirements;
                EffectReferences = effectReferences;
            }

            public int Order { get; }
            public string Id { get; }
            public string Route { get; }
            public int Tier { get; }
            public int LayoutRow { get; }
            public string ReleaseState { get; }
            public string[] Requirements { get; }
            public string[] EffectReferences { get; }
        }
    }
}
