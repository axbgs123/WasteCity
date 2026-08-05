using System;
using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class RouteContentDisplayTests
    {
        [Test]
        public void EveryResourceHasFriendlyNameAndAppearsInItsRouteGroup()
        {
            var inventory = new ResourceInventory(1000);
            for (int index = 0; index < ResourceIds.All.Length; index++)
                inventory.Set(ResourceIds.All[index], index + 1);

            string summary = RouteContentDisplayCatalog.InventorySummary(inventory);

            StringAssert.Contains("基础：", summary);
            StringAssert.Contains("科技：", summary);
            StringAssert.Contains("修仙：", summary);
            StringAssert.Contains("生物飞升：", summary);
            StringAssert.Contains("灵能：", summary);
            foreach (string resourceId in ResourceIds.All)
            {
                string friendlyName = RouteContentDisplayCatalog.ResourceName(resourceId);
                Assert.That(friendlyName, Is.Not.Null.And.Not.Empty, resourceId);
                Assert.That(RouteContentDisplayCatalog.ResourceRoute(resourceId), Is.TypeOf<ContentRoute>());
                StringAssert.Contains(friendlyName, summary, resourceId);
            }
            AssertHasNoRawStableId(summary);
        }

        [Test]
        public void EveryRegisteredBuildingHasFriendlyRouteCostFunctionAndUnlockSummary()
        {
            foreach (BuildingDefinition definition in BuildingCatalog.All)
            {
                string summary = RouteContentDisplayCatalog.BuildingSummary(definition);

                StringAssert.Contains(definition.Name, summary, definition.Id.Value);
                StringAssert.Contains(RouteContentDisplayCatalog.RouteName(
                    RouteContentDisplayCatalog.BuildingRoute(definition)), summary, definition.Id.Value);
                StringAssert.Contains($"成本：{RouteContentDisplayCatalog.ResourceName(definition.CostId)} {definition.Cost}", summary, definition.Id.Value);
                StringAssert.Contains("功能：", summary, definition.Id.Value);
                StringAssert.Contains("解锁：", summary, definition.Id.Value);
                StringAssert.Contains("位置：", summary, definition.Id.Value);
                StringAssert.Contains("运行：", summary, definition.Id.Value);
                Assert.That(summary, Does.Not.Contain("未登记功能"), definition.Id.Value);
                AssertHasNoRawStableId(summary);
            }
        }

        [Test]
        public void BuildingSummaryUsesApprovedFriendlyMobilityTerms()
        {
            string housing = RouteContentDisplayCatalog.BuildingSummary(BuildingCatalog.Housing);
            string mining = RouteContentDisplayCatalog.BuildingSummary(BuildingCatalog.MiningStation);

            StringAssert.Contains("位置：两者皆可 · 运行：移动可运行", housing);
            StringAssert.Contains("位置：地面 · 运行：地形依赖", mining);
        }

        [Test]
        public void EveryResearchNodeHasConsistentFriendlyListAndDetailText()
        {
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                string routeName = RouteContentDisplayCatalog.RouteName(definition.Route);
                string resourceName = RouteContentDisplayCatalog.ResourceName(definition.CostId);
                string listLine = RouteContentDisplayCatalog.ResearchListLine(definition, false, true);
                string detail = RouteContentDisplayCatalog.ResearchDetail(definition);

                StringAssert.Contains(definition.Name, listLine, definition.Id.Value);
                StringAssert.Contains(routeName, listLine, definition.Id.Value);
                StringAssert.Contains(resourceName, listLine, definition.Id.Value);
                StringAssert.Contains("前置未完成", listLine, definition.Id.Value);
                StringAssert.Contains(definition.Name, detail, definition.Id.Value);
                StringAssert.Contains(routeName, detail, definition.Id.Value);
                StringAssert.Contains(resourceName, detail, definition.Id.Value);
                StringAssert.Contains(definition.EffectSummary, detail, definition.Id.Value);
                AssertHasNoRawStableId(listLine);
                AssertHasNoRawStableId(detail);
            }
        }

        [TestCase("technology.building.power-plant", "能晶", "当前代理：能源币")]
        [TestCase("cultivation.building.spirit-gathering-array", "能晶", "当前代理：灵石")]
        [TestCase("biological.building.metabolic-furnace", "生物质", "当前代理：能源币")]
        [TestCase("psionics.building.consciousness-network", "灵能增幅器", "当前代理：精神力结晶")]
        public void RouteCapstoneDescriptionsExposeCurrentProxySemantics(
            string buildingId,
            string requiredResourceText,
            string proxyText)
        {
            BuildingDefinition definition = Array.Find(
                BuildingCatalog.All,
                value => value.Id.Value == buildingId);

            string summary = RouteContentDisplayCatalog.BuildingSummary(definition);

            StringAssert.Contains(requiredResourceText, summary);
            StringAssert.Contains(proxyText, summary);
        }

        [Test]
        public void FriendlyUnlockReasonResolvesResearchAndBuildingIdsToNames()
        {
            string researchReason = RouteContentDisplayCatalog.FriendlyUnlockReason(
                BuildingCatalog.Assembler,
                1000,
                _ => false,
                Array.Empty<string>());
            string buildingReason = RouteContentDisplayCatalog.FriendlyUnlockReason(
                BuildingCatalog.Assembler,
                1000,
                _ => true,
                Array.Empty<string>());
            string populationReason = RouteContentDisplayCatalog.FriendlyUnlockReason(
                BuildingCatalog.PowerPlant,
                999,
                _ => true,
                new[] { BuildingCatalog.Smelter.Id.Value });

            Assert.That(researchReason, Is.EqualTo("需要研究：精密装配"));
            Assert.That(buildingReason, Is.EqualTo("需要先完成：冶炼厂"));
            Assert.That(populationReason, Is.EqualTo("需要人口：1000"));
            AssertHasNoRawStableId(researchReason);
            AssertHasNoRawStableId(buildingReason);
        }

        [Test]
        public void UnitProductionBuildingsExposeExactCurrentCostsCadenceAndCapacity()
        {
            string puppet = RouteContentDisplayCatalog.BuildingSummary(BuildingCatalog.PuppetWorkshop);
            string behemoth = RouteContentDisplayCatalog.BuildingSummary(BuildingCatalog.BehemothPen);

            StringAssert.Contains("每 20 秒", puppet);
            StringAssert.Contains("1 合金 + 1 灵铁", puppet);
            StringAssert.Contains("每座工坊容量 3", puppet);
            StringAssert.Contains("每 35 秒", behemoth);
            StringAssert.Contains("2 骨钢 + 3 生物质浓缩液", behemoth);
            StringAssert.Contains("每座巨兽栏容量 1", behemoth);
        }

        private static void AssertHasNoRawStableId(string text)
        {
            Assert.That(text, Does.Not.Contain(".resource."));
            Assert.That(text, Does.Not.Contain(".research."));
            Assert.That(text, Does.Not.Contain(".building."));
        }
    }
}
