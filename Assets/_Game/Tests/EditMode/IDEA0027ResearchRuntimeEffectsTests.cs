using System;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class IDEA0027ResearchRuntimeEffectsTests
    {
        private const string TestResearchId =
            "test.research.idea-0027-next-project";

        [TestCase(false, 100f)]
        [TestCase(true, 80f)]
        public void CollectiveConsciousnessSeedsOnlyNewResearchProgress(
            bool completed,
            float expectedRemaining)
        {
            var model = new ResearchModel();
            var runtime = new FormalResearchRuntime(model, Resolve);
            if (completed)
            {
                model.GrantCompletedForDevelopment(ResearchCatalog.Find(
                    "core.research.collective-consciousness"));
            }
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Iron, 10);

            Assert.That(runtime.TryStart(
                TestResearchId,
                inventory,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(model.Remaining, Is.EqualTo(expectedRemaining));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.Zero);
        }

        [Test]
        public void DevelopmentCompletionUsesNaturalCompletionNotificationOnce()
        {
            var model = new ResearchModel();
            ResearchDefinition definition = ResearchCatalog.Find(
                ResearchCatalog.CollectiveConsciousnessId);
            var notifications = 0;
            model.Completed += completed =>
            {
                if (ReferenceEquals(completed, definition)) notifications++;
            };

            model.GrantCompletedForDevelopment(definition);
            model.GrantCompletedForDevelopment(definition);

            Assert.That(model.IsCompleted(definition.Id), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }

        private static ResearchDefinition Resolve(string id)
        {
            return string.Equals(id, TestResearchId, StringComparison.Ordinal)
                ? new ResearchDefinition(
                    TestResearchId,
                    "下一项研究",
                    DevelopmentRoute.Common,
                    ResourceIds.Iron,
                    10,
                    100f)
                : ResearchCatalog.Find(id);
        }
    }
}
