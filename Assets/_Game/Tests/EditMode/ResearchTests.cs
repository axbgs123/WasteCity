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
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.EnergyCrystal, 8);
            inventory.Add(ResourceIds.Iron, 4);
            var model = FormalModel();
            var definition = ResearchCatalog.Find("core.research.spirit-sensing");
            Assert.That(model.Start(definition, inventory), Is.True); Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(model.Tick(19.9f), Is.False); Assert.That(model.Tick(0.1f), Is.True); Assert.That(model.IsCompleted(definition.Id), Is.True);
        }
        [Test]
        public void OnlyOneResearchRunsAtATime()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.Iron, 20); inventory.Add(ResourceIds.Water, 20);
            var model = FormalModel(); Assert.That(model.Start(ResearchCatalog.Find("core.research.automated-machinery"), inventory), Is.True);
            Assert.That(model.Start(ResearchCatalog.Find("core.research.mind-resonance"), inventory), Is.False);
        }
        [Test] public void ResearchProgressCanBeRestored()
        {var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Iron,10);var model=FormalModel();model.Start(ResearchCatalog.Find("core.research.automated-machinery"),inventory);model.Tick(5);var restored=new ResearchModel();restored.Restore(model.CaptureCompleted(),model.Active.Id.Value,model.Remaining);Assert.That(restored.Active.Id.Value,Is.EqualTo("core.research.automated-machinery"));Assert.That(restored.Remaining,Is.EqualTo(15));}
        [Test] public void RetiredLegacyAnalysisCannotStartOrSpend(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Alloy,30);inventory.Add(ResourceIds.Biomass,20);var model=new ResearchModel();model.Restore(new[]{"core.research.scrap-processing","core.research.automated-defense"},null,0);var legacy=ResearchCatalog.Find("core.research.legacy-analysis");Assert.That(legacy.ReleaseState,Is.EqualTo(ResearchReleaseState.RetiredCompatibility));Assert.That(model.Start(legacy,inventory),Is.False);Assert.That(inventory.Get(ResourceIds.Alloy),Is.EqualTo(30));Assert.That(inventory.Get(ResourceIds.Biomass),Is.EqualTo(20));}
        [Test] public void FormalCatalogContainsExactFortyThreeNodesWithoutRetiredEntries(){Assert.That(ResearchCatalog.All.Length,Is.EqualTo(43));Assert.That(ResearchCatalog.All.Select(value=>value.Id.Value).Distinct().Count(),Is.EqualTo(43));Assert.That(ResearchCatalog.All.Any(value=>value.ReleaseState==ResearchReleaseState.RetiredCompatibility),Is.False);}
        [Test] public void ResearchWithTwoRequirementsRequiresBothRoutes(){var bridge=new ResearchDefinition("test.research.bridge","双路线研究",DevelopmentRoute.Bridge,ResourceIds.Alloy,50,90f,"core.research.precision-assembly",3,"测试双路线前置","core.research.psionic-workshop");var inventory=new ResourceInventory(200);inventory.Add(ResourceIds.Alloy,100);var model=new ResearchModel();model.Restore(new[]{"core.research.precision-assembly"},null,0);Assert.That(model.Start(bridge,inventory),Is.False);model.Restore(new[]{"core.research.precision-assembly","core.research.psionic-workshop"},null,0);Assert.That(model.Start(bridge,inventory),Is.True);}
        [Test] public void PreviewResearchCannotStartOrSpend(){var preview=ResearchCatalog.Find("core.research.bridge.psionic-mech");var inventory=new ResourceInventory(200);inventory.Add(ResourceIds.Alloy,30);inventory.Add(ResourceIds.PsionicAmplifier,20);var model=new ResearchModel();model.Restore(new[]{"core.research.precision-assembly","core.research.psionic-workshop"},null,0);Assert.That(preview.ReleaseState,Is.EqualTo(ResearchReleaseState.PreviewOnly));Assert.That(model.Start(preview,inventory),Is.False);Assert.That(inventory.Get(ResourceIds.Alloy),Is.EqualTo(30));Assert.That(inventory.Get(ResourceIds.PsionicAmplifier),Is.EqualTo(20));}
        [Test] public void ExtendedResearchNodesRoundTripWithPreviewActiveFrozen(){var model=new ResearchModel();model.Restore(new[]{"core.research.alloy-armor","core.research.collective-consciousness"},"core.research.bridge.bio-hangar",17);Assert.That(model.IsCompleted(ResearchCatalog.Find("core.research.alloy-armor").Id),Is.True);Assert.That(model.Active,Is.Null);Assert.That(model.MissingActiveResearchId,Is.EqualTo("core.research.bridge.bio-hangar"));Assert.That(model.Remaining,Is.EqualTo(17));Assert.That(model.CaptureForPersistence().ActiveResearchId,Is.EqualTo("core.research.bridge.bio-hangar"));}

        [Test]
        public void RetiredCompletedResearchPersistsAsInertCompatibilityData()
        {
            const string legacy = "core.research.legacy-analysis";
            var model = new ResearchModel();

            Assert.That(model.TryPrepareRestoreForPersistence(
                new[] { legacy },
                null,
                0f,
                ResearchCatalog.Find,
                out ResearchRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(model.TryCommitRestoreForPersistence(plan, out error),
                Is.True, error);

            Assert.That(model.CompletedCount, Is.Zero);
            Assert.That(model.IsCompleted(ResearchCatalog.Find(legacy).Id),
                Is.False);
            CollectionAssert.Contains(
                model.CaptureForPersistence().CompletedResearchIds,
                legacy);
        }

        [Test]
        public void RetiredActiveResearchPersistsFrozenWithoutBecomingActive()
        {
            const string legacy = "core.research.legacy-analysis";
            var model = new ResearchModel();

            Assert.That(model.TryPrepareRestoreForPersistence(
                System.Array.Empty<string>(),
                legacy,
                17f,
                ResearchCatalog.Find,
                out ResearchRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(model.TryCommitRestoreForPersistence(plan, out error),
                Is.True, error);

            Assert.That(model.Active, Is.Null);
            Assert.That(model.HasMissingActiveResearch, Is.True);
            Assert.That(model.MissingActiveResearchId, Is.EqualTo(legacy));
            Assert.That(model.Remaining, Is.EqualTo(17f));
            Assert.That(model.Tick(100f), Is.False);
            Assert.That(model.CaptureForPersistence().ActiveResearchId,
                Is.EqualTo(legacy));
        }

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
            var model = FormalModel();

            Assert.That(
                model.Start(
                    ResearchCatalog.Find("core.research.automated-machinery"),
                    inventory),
                Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(-10));
        }

        private static ResearchModel FormalModel()
        {
            var model = new ResearchModel();
            model.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.scrap-processing"));
            return model;
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
