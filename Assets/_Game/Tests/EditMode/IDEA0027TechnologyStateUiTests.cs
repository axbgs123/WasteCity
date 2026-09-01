using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class IDEA0027TechnologyStateUiTests
    {
        [Test]
        public void DevelopmentCatalogListsElevenChineseTechnologyStates()
        {
            Assert.That(
                GrayboxDeveloperCatalogQuery3D.TechnologyStatusEntries.Count,
                Is.EqualTo(11));
            Assert.That(
                GrayboxDeveloperCatalogQuery3D.SearchTechnologyStatuses(
                    "感染").Single().DisplayName,
                Is.EqualTo("感染"));
            Assert.That(
                GrayboxDeveloperCatalogQuery3D.TechnologyStatusEntries.All(
                    value => !value.DisplayName.Contains("status") &&
                             !value.DisplayName.Contains("developer")),
                Is.True);
        }

        [Test]
        public void SelectedLaserShowsOverloadNameSourcePhaseAndRemainingTime()
        {
            GrayboxBuildingInstance3D laser = Instance(
                "building.instance.idea0027-laser",
                BuildingCatalog.LaserTower);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { laser });
            var tower = new GrayboxDefenseTowerSnapshot3D(
                laser.StableInstanceId,
                ammo: 5,
                ammoCapacity: 10,
                range: 12f,
                connected: true,
                canRunLocally: true,
                playerPaused: false,
                targetId: null,
                status: GrayboxDefenseTowerStatus3D.NoTarget);
            var technology = new SingleCityDefenseTechnologyRuntime();
            technology.Configure(new SingleCityDefenseTechnologyUnlocks(
                energyOverload: true));
            Assert.That(technology.TryActivateOverload(
                laser.StableInstanceId,
                BuildingCatalog.LaserTower.Id.Value), Is.True);

            GrayboxDefenseSelectionSnapshot3D selected =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Tower,
                    laser.StableInstanceId,
                    Defense(new[] { tower }),
                    new[] { laser },
                    health,
                    ProductionObservabilitySnapshot.Empty,
                    technologyState: technology.Snapshot,
                    energyOverloadUnlocked: true);

            Assert.That(selected.TechnologyStatuses.Count, Is.EqualTo(1));
            Assert.That(selected.TechnologyStatuses[0].DisplayName,
                Is.EqualTo("能量过载"));
            Assert.That(selected.TechnologyStatuses[0].SourceResearchName,
                Is.EqualTo("能量武器"));
            Assert.That(selected.TechnologyStatuses[0].PhaseText,
                Is.EqualTo("强化"));
            Assert.That(selected.TechnologyStatuses[0].RemainingSeconds,
                Is.EqualTo(5f).Within(.001f));
            Assert.That(selected.IsTechnologyOverloadVisible, Is.True);
            Assert.That(selected.CanActivateTechnologyOverload, Is.False);
            Assert.That(selected.TechnologyOverloadButtonLabel,
                Does.Contain("强化"));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public void DevelopmentStateCommandsMutateAuthoritativeEnemyAndOverloadModels()
        {
            var technology = new SingleCityDefenseTechnologyRuntime();
            technology.Configure(new SingleCityDefenseTechnologyUnlocks(
                energyOverload: true,
                swordIntent: true,
                infection: true,
                resonance: true));
            Assert.That(technology.TryRestore(
                new SingleCityDefenseTechnologyPersistenceSnapshot(
                    Array.Empty<SingleCityDefenseOverloadPersistenceState>(),
                    new[]
                    {
                        new SingleCityDefenseEnemyTechnologyPersistenceState(
                            "enemy.idea0027",
                            "enemy.raider",
                            100,
                            3f,
                            4f,
                            0,
                            0,
                            0f,
                            0f,
                            false),
                    }),
                out string restoreError), Is.True, restoreError);

            Assert.That(technology.TrySetEnemyStatusForDevelopment(
                "enemy.idea0027",
                ResearchStatusCatalog.SwordIntentId,
                fillStacks: false), Is.True);
            Assert.That(technology.Snapshot.Enemies[0].SwordIntentStacks,
                Is.EqualTo(1));
            Assert.That(technology.TrySetEnemyStatusForDevelopment(
                "enemy.idea0027",
                ResearchStatusCatalog.SwordIntentId,
                fillStacks: true), Is.True);
            Assert.That(technology.Snapshot.Enemies[0].SwordIntentStacks,
                Is.EqualTo(SwordIntentModel.MaximumStacks - 1));

            Assert.That(technology.TrySetEnemyStatusForDevelopment(
                "enemy.idea0027",
                ResearchStatusCatalog.InfectionId,
                fillStacks: true), Is.True);
            Assert.That(technology.Snapshot.Enemies[0].InfectionStacks,
                Is.EqualTo(InfectionModel.BurstThreshold - 1));
            Assert.That(technology.TrySetEnemyStatusForDevelopment(
                "enemy.idea0027",
                ResearchStatusCatalog.PsionicResonanceId,
                fillStacks: false), Is.True);
            Assert.That(technology.Snapshot.Enemies[0].ResonanceRemaining,
                Is.EqualTo(PsionicResonanceModel.DurationSeconds));
            Assert.That(technology.TryExpireEnemyStatusForDevelopment(
                "enemy.idea0027",
                ResearchStatusCatalog.PsionicResonanceId), Is.True);
            Assert.That(technology.Snapshot.Enemies[0].ResonanceRemaining,
                Is.Zero);
            Assert.That(technology.TryClearEnemyStatusForDevelopment(
                "enemy.idea0027",
                ResearchStatusCatalog.InfectionId), Is.True);
            Assert.That(technology.Snapshot.Enemies[0].InfectionStacks,
                Is.Zero);

            Assert.That(technology.TryActivateOverload(
                "tower.idea0027",
                BuildingCatalog.LaserTower.Id.Value), Is.True);
            Assert.That(technology.TryExpireOverloadForDevelopment(
                "tower.idea0027"), Is.True);
            Assert.That(technology.Snapshot.Overloads[0].Phase,
                Is.EqualTo(TechnologyOverloadPhase.Ready));
            Assert.That(technology.TryClearOverloadForDevelopment(
                "tower.idea0027"), Is.True);
            Assert.That(technology.Snapshot.Overloads, Is.Empty);
        }
#endif

        [Test]
        public void CivilizationControllerResolvesArmyEffectsFromThreeDSession()
        {
            GameObject sessionRoot = new GameObject("IDEA0027.Session");
            GameObject controllerRoot = new GameObject("IDEA0027.Controller");
            try
            {
                GrayboxBuildingSession3D session = sessionRoot.AddComponent<
                    GrayboxBuildingSession3D>();
                session.ConfigureDevelopmentFixture();
                session.UnlockResearchForDevelopment(
                    "core.research.puppetry");
                session.UnlockResearchForDevelopment(
                    "core.research.behemoth-breeding");
                session.UnlockResearchForDevelopment(
                    "core.research.tissue-regeneration");
                GrayboxCivilizationExpansionController3D controller =
                    controllerRoot.AddComponent<
                        GrayboxCivilizationExpansionController3D>();
                FieldInfo sessionField = typeof(
                        GrayboxCivilizationExpansionController3D)
                    .GetField(
                        "session",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo resolver = typeof(
                        GrayboxCivilizationExpansionController3D)
                    .GetMethod(
                        "ResolveResearchEffects",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(sessionField, Is.Not.Null);
                Assert.That(resolver, Is.Not.Null);
                sessionField.SetValue(controller, session);
                var effects = (ResearchEffectSnapshot)resolver.Invoke(
                    controller,
                    null);

                Assert.That(effects.ResolveUnitCapacity(
                    BuildingCatalog.PuppetWorkshop.Id.Value, 3), Is.EqualTo(4));
                Assert.That(effects.ResolveUnitHealthMultiplier(
                    ArmyUnitCatalog.BredBehemothId), Is.EqualTo(1.1f));
                Assert.That(effects.TissueRegeneration, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerRoot);
                UnityEngine.Object.DestroyImmediate(sessionRoot);
            }
        }

        private static GrayboxDefenseRuntimeSnapshot3D Defense(
            GrayboxDefenseTowerSnapshot3D[] towers)
        {
            return new GrayboxDefenseRuntimeSnapshot3D(
                tutorialWaveTriggerCount: 0,
                WavePhase.Idle,
                warningRemainingSeconds: 0f,
                spawnedEnemyCount: 0,
                aliveEnemyCount: 0,
                defeatedEnemyCount: 0,
                coreMaximumHealth: 2000,
                coreCurrentHealth: 2000,
                towers,
                Array.Empty<GrayboxDefenseEnemySnapshot3D>());
        }

        private static GrayboxBuildingInstance3D Instance(
            string stableId,
            BuildingDefinition definition)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableId,
                    new PlacedBuilding(definition, 1, 1),
                    new ConstructionProgress(definition.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, null);
            return instance;
        }
    }
}
