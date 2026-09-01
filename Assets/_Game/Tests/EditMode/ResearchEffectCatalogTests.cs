using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class ResearchEffectCatalogTests
    {
        private const string SwordArrayId = "core.research.sword-array";
        private const string SwordRidingId = "core.research.sword-riding";

        [Test]
        public void IDEA0026_EffectCatalogCoversEveryFormalResearchNode()
        {
            string[] expected = ResearchCatalog.All
                .Select(value => value.Id.Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actual = ResearchEffectCatalog.All
                .Select(value => value.ResearchId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(expected, Has.Length.EqualTo(44));
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void IDEA0026_EffectIdsAreGloballyUnique()
        {
            string[] ids = ResearchEffectCatalog.All
                .Select(value => value.Id)
                .ToArray();

            Assert.That(ids, Has.All.Not.Null.And.Not.Empty);
            Assert.That(
                ids.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(ids.Length));
            Assert.That(
                ids,
                Has.All.Matches<string>(value => value.All(character =>
                    character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' ||
                    character == '.' || character == '-')),
                "Stable effect IDs must remain ASCII and content-independent.");
        }

        [Test]
        public void IDEA0026_EveryNaturalNodeHasAnExecutableNonUnlockEffect()
        {
            ResearchDefinition[] natural = ResearchCatalog.All
                .Where(value =>
                    value.ReleaseState ==
                        ResearchReleaseState.InitiallyCompleted ||
                    value.ReleaseState == ResearchReleaseState.Researchable)
                .ToArray();

            Assert.That(
                natural,
                Has.Length.EqualTo(44),
                "IDEA-0027 releases all catalogued research nodes.");
            foreach (ResearchDefinition definition in natural)
            {
                Assert.That(
                    ResearchEffectCatalog.ForResearch(definition.Id.Value).Any(
                        effect => effect.IsExecutable &&
                            effect.Activation ==
                                ResearchEffectActivation.Active &&
                            effect.Kind !=
                                ResearchEffectKind.UnlockContent),
                    Is.True,
                    definition.Id.Value +
                    " must change a value or rule, not only unlock content.");
            }
        }

        [Test]
        public void IDEA0027_FormalCatalogNoLongerContainsPreviewOnlyNodes()
        {
            Assert.That(ResearchCatalog.All, Has.None.Matches<
                ResearchDefinition>(definition =>
                    definition.ReleaseState ==
                        ResearchReleaseState.PreviewOnly));
        }

        [Test]
        public void IDEA0026_CompletedSetResolutionIsOrderIndependentAndIdempotent()
        {
            string[] firstOrder =
            {
                ResearchCatalog.ThoughtAccelerationId,
                "core.research.formation-reinforcement",
                ResearchCatalog.ThoughtAccelerationId,
                "core.research.orbital-supply",
                "core.research.alloy-armor",
            };
            string[] secondOrder =
            {
                "core.research.alloy-armor",
                "core.research.orbital-supply",
                ResearchCatalog.ThoughtAccelerationId,
                "core.research.formation-reinforcement",
            };

            ResearchEffectSnapshot first = ResearchEffectResolver.Resolve(
                firstOrder);
            ResearchEffectSnapshot second = ResearchEffectResolver.Resolve(
                secondOrder);

            Assert.That(first.ResearchSpeedMultiplier,
                Is.EqualTo(second.ResearchSpeedMultiplier));
            Assert.That(first.LogisticsRange,
                Is.EqualTo(second.LogisticsRange));
            Assert.That(first.BuildingHealthMultiplier,
                Is.EqualTo(second.BuildingHealthMultiplier));
            CollectionAssert.AreEqual(
                first.AppliedEffectIds.OrderBy(
                    value => value, StringComparer.Ordinal),
                second.AppliedEffectIds.OrderBy(
                    value => value, StringComparer.Ordinal));
        }

        [Test]
        public void IDEA0026_ApprovedScalarEffectsResolveFromCompletedResearch()
        {
            ResearchEffectSnapshot thought = ResearchEffectResolver.Resolve(
                new[] { ResearchCatalog.ThoughtAccelerationId });
            Assert.That(thought.ResearchSpeedMultiplier, Is.EqualTo(1.25f));
            Assert.That(
                FormalResearchRuntime.SpeedMultiplier(
                    WasteCity.City.CityMode.Fortress,
                    thoughtAccelerationCompleted: true),
                Is.EqualTo(thought.ResearchSpeedMultiplier));

            ResearchEffectSnapshot formation = ResearchEffectResolver.Resolve(
                new[] { "core.research.formation-reinforcement" });
            Assert.That(formation.LogisticsRange, Is.EqualTo(12));

            ResearchEffectSnapshot orbital = ResearchEffectResolver.Resolve(
                new[]
                {
                    "core.research.formation-reinforcement",
                    "core.research.orbital-supply",
                });
            Assert.That(orbital.LogisticsRange, Is.EqualTo(24));

            ResearchEffectSnapshot armor = ResearchEffectResolver.Resolve(
                new[] { "core.research.alloy-armor" });
            Assert.That(armor.BuildingHealthMultiplier, Is.EqualTo(1.3f));
        }

        [Test]
        public void IDEA0026_TowerRuntimeValuesComeFromResolvedEffectSnapshot()
        {
            ResearchEffectSnapshot effects = ResearchEffectResolver.Resolve(
                new[]
                {
                    ResearchCatalog.AutomatedDefenseId,
                    SwordArrayId,
                    SwordRidingId,
                });

            Assert.That(
                effects.ResolveTowerDamageMultiplier(
                    BuildingCatalog.MachineGunTurret.Id.Value),
                Is.EqualTo(1f / .9f).Within(.0001f));
            Assert.That(
                effects.ResolveTowerDamageMultiplier(
                    BuildingCatalog.SwordArrayTower.Id.Value),
                Is.EqualTo(1.15f).Within(.0001f));
            Assert.That(
                effects.ResolveTowerRangeMultiplier(
                    BuildingCatalog.SwordRidingPlatform.Id.Value),
                Is.EqualTo(1.3f).Within(.0001f));
            Assert.That(
                effects.ResolveTowerDamageMultiplier(
                    BuildingCatalog.Warehouse.Id.Value),
                Is.EqualTo(1f));
        }

        [Test]
        public void IDEA0026_SwordArrayIsResearchableAndLevelTwoSwordRidingIsReachable()
        {
            ResearchDefinition swordArray = ResearchCatalog.Find(SwordArrayId);
            Assert.That(swordArray, Is.Not.Null);
            Assert.That(
                swordArray.ReleaseState,
                Is.EqualTo(ResearchReleaseState.Researchable));

            ResearchDefinition swordRiding =
                CivilizationResearchAvailability.Resolve(
                    ResearchCatalog.Find(SwordRidingId),
                    civilizationLevel: 2);
            Assert.That(
                swordRiding.ReleaseState,
                Is.EqualTo(ResearchReleaseState.Researchable));
            CollectionAssert.Contains(
                swordRiding.RequiredResearchIds,
                SwordArrayId);
            Assert.That(
                IsReachableAtCivilizationLevel(SwordRidingId, 2),
                Is.True);
        }

        private static bool IsReachableAtCivilizationLevel(
            string targetResearchId,
            int civilizationLevel)
        {
            var reached = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                if (definition.ReleaseState ==
                    ResearchReleaseState.InitiallyCompleted)
                {
                    reached.Add(definition.Id.Value);
                }
            }

            bool changed;
            do
            {
                changed = false;
                foreach (ResearchDefinition source in ResearchCatalog.All)
                {
                    ResearchDefinition definition =
                        CivilizationResearchAvailability.Resolve(
                            source,
                            civilizationLevel);
                    if (definition.ReleaseState !=
                            ResearchReleaseState.Researchable ||
                        reached.Contains(definition.Id.Value) ||
                        definition.RequiredResearchIds.Any(
                            required => !reached.Contains(required)))
                    {
                        continue;
                    }

                    changed |= reached.Add(definition.Id.Value);
                }
            }
            while (changed);

            return reached.Contains(targetResearchId);
        }
    }
}
