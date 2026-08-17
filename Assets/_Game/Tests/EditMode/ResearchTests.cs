using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Research;
using System.Linq;

namespace WasteCity.Tests
{
    public sealed class ResearchTests
    {
        [Test]
        public void ResearchConsumesRouteResourceAndCompletesAfterDuration()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.EnergyCrystal, 10);
            var model = new ResearchModel(); var definition = ResearchCatalog.Starting[1];
            Assert.That(model.Start(definition, inventory), Is.True); Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(model.Tick(19.9f), Is.False); Assert.That(model.Tick(0.1f), Is.True); Assert.That(model.IsCompleted(definition.Id), Is.True);
        }
        [Test]
        public void OnlyOneResearchRunsAtATime()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.Iron, 20); inventory.Add(ResourceIds.Water, 20);
            var model = new ResearchModel(); Assert.That(model.Start(ResearchCatalog.Starting[0], inventory), Is.True);
            Assert.That(model.Start(ResearchCatalog.Starting[3], inventory), Is.False);
        }
        [Test] public void ResearchProgressCanBeRestored()
        {var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Iron,10);var model=new ResearchModel();model.Start(ResearchCatalog.Starting[0],inventory);model.Tick(5);var restored=new ResearchModel();restored.Restore(model.CaptureCompleted(),model.Active.Id.Value,model.Remaining);Assert.That(restored.Active.Id.Value,Is.EqualTo("core.research.automated-machinery"));Assert.That(restored.Remaining,Is.EqualTo(15));}
        [Test] public void LegacyAnalysisRequiresAutomatedMachinery(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Alloy,60);inventory.Add(ResourceIds.Iron,10);var model=new ResearchModel();Assert.That(model.Start(ResearchCatalog.Starting[4],inventory),Is.False);Assert.That(model.Start(ResearchCatalog.Starting[0],inventory),Is.True);model.Tick(20);Assert.That(model.Start(ResearchCatalog.Starting[4],inventory),Is.True);}
        [Test] public void FormalCatalogContainsFortyTwoTreeNodesPlusLegacyAnalysis(){Assert.That(ResearchCatalog.All.Length,Is.EqualTo(43));Assert.That(ResearchCatalog.All.Select(value=>value.Id.Value).Distinct().Count(),Is.EqualTo(43));}
        [Test] public void BridgeResearchRequiresBothRoutes(){var bridge=ResearchCatalog.Find("core.research.bridge.psionic-mech");var inventory=new ResourceInventory(200);inventory.Add(ResourceIds.Alloy,100);var model=new ResearchModel();model.Restore(new[]{"core.research.precision-assembly"},null,0);Assert.That(model.Start(bridge,inventory),Is.False);model.Restore(new[]{"core.research.precision-assembly","core.research.psionic-workshop"},null,0);Assert.That(model.Start(bridge,inventory),Is.True);}
        [Test] public void ExtendedResearchNodesRoundTrip(){var model=new ResearchModel();model.Restore(new[]{"core.research.alloy-armor","core.research.collective-consciousness"},"core.research.bridge.bio-hangar",17);Assert.That(model.IsCompleted(ResearchCatalog.Find("core.research.alloy-armor").Id),Is.True);Assert.That(model.Active.Id.Value,Is.EqualTo("core.research.bridge.bio-hangar"));Assert.That(model.Remaining,Is.EqualTo(17));}

        [Test]
        public void CollectiveConsciousnessStartsResearchWithTwentyPercentProgress()
        {
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Iron, 20);
            var definition = new ResearchDefinition(
                "test.research.collective-inheritance",
                "共享研究",
                DevelopmentRoute.Technology,
                ResourceIds.Iron,
                10,
                60f);
            var model = new ResearchModel();

            Assert.That(model.Start(definition, inventory, .2f), Is.True);

            Assert.That(model.Remaining, Is.EqualTo(48f));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(10));
        }

        [Test]
        public void CollectiveConsciousnessRuleOnlyProvidesProgressWhenUnlocked()
        {
            Assert.That(CollectiveConsciousnessRules.InheritedProgressRatio(false), Is.Zero);
            Assert.That(CollectiveConsciousnessRules.InheritedProgressRatio(true), Is.EqualTo(.2f));
        }

        [TestCase(-1f, 60f)]
        [TestCase(0f, 60f)]
        [TestCase(1f, .001f)]
        [TestCase(2f, .001f)]
        public void ResearchStartClampsInheritedProgress(
            float inheritedProgressRatio,
            float expectedRemaining)
        {
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Iron, 10);
            var definition = new ResearchDefinition(
                "test.research.inheritance-clamp",
                "边界研究",
                DevelopmentRoute.Technology,
                ResourceIds.Iron,
                10,
                60f);
            var model = new ResearchModel();

            Assert.That(model.Start(definition, inventory, inheritedProgressRatio), Is.True);
            Assert.That(model.Remaining, Is.EqualTo(expectedRemaining).Within(.0001f));
        }

        [Test]
        public void ResearchStartPreservesConfiguredDebtAllowance()
        {
            var inventory = new ResourceInventory(100);
            inventory.SetDebtLimit(10);
            var model = new ResearchModel();

            Assert.That(
                model.Start(ResearchCatalog.Starting[0], inventory),
                Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(-10));
        }

        [Test]
        public void LegacyZeroCostDefinitionKeepsItsCostIdAndCanStart()
        {
            var definition = new ResearchDefinition(
                "test.research.free",
                "免费研究",
                DevelopmentRoute.Technology,
                ResourceIds.Iron,
                0,
                5f);
            var inventory = new ResourceInventory(100);
            var model = new ResearchModel();

            Assert.That(definition.CostId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(definition.Cost, Is.Zero);
            Assert.That(definition.Costs, Is.Empty);
            Assert.That(model.Start(definition, inventory), Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.Zero);
        }
    }
}
