using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.CivilizationExpansion;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;
using WasteCity.World;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Tests
{
    public sealed class IDEA0027ResearchEffectStateSaveAdapterTests
    {
        [Test]
        public void GeneCompletionRoundTripsOnceThroughFormalEffectState()
        {
            var sourceResearch = new ResearchModel();
            CivilizationExpansionRuntime sourceExpansion = Expansion();
            var sourceDefense = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            using var source = new GrayboxResearchEffectStateSaveAdapter3D(
                sourceResearch, sourceDefense, () => sourceExpansion);

            sourceResearch.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.gene-splicing"));
            var saved = source.Capture();

            Assert.That(saved.rewardLedger.committedRewardKeys,
                Does.Contain(
                    GrayboxResearchEffectStateSaveAdapter3D
                        .GeneSplicingRewardKey));
            Assert.That(sourceExpansion.Characters[0].HasGeneSplicingTrait,
                Is.True);

            var targetResearch = new ResearchModel();
            targetResearch.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.gene-splicing"));
            CivilizationExpansionRuntime targetExpansion = Expansion();
            var targetDefense = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            using var target = new GrayboxResearchEffectStateSaveAdapter3D(
                targetResearch, targetDefense, () => targetExpansion);
            Assert.That(target.TryPrepareRestore(saved, out string error),
                Is.True, error);
            saved.states[0].remainingRuleSeconds = 1f;
            Assert.That(target.TryApplyPendingExpansionState(
                targetExpansion, out error), Is.True, error);

            Assert.That(targetExpansion.Characters[0]
                    .GeneSplicingRemainingSeconds,
                Is.EqualTo(300f));
            FormalThreeDResearchEffectStateSaveData restored =
                target.Capture();
            Assert.That(restored.revision, Is.EqualTo(saved.revision),
                "An unchanged capture after restore must preserve the exact " +
                "saved revision.");
            Assert.That(restored.rewardLedger.committedRewardKeys,
                Has.Length.EqualTo(1));
        }

        [Test]
        public void RewardLedgerRejectsUnknownOrUnresearchedKeysWithoutSwallowingReward()
        {
            var research = new ResearchModel();
            CivilizationExpansionRuntime expansion = Expansion();
            using var adapter = new GrayboxResearchEffectStateSaveAdapter3D(
                research,
                new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f),
                () => expansion);
            FormalThreeDResearchEffectStateSaveData saved = adapter.Capture();
            saved.revision = 1UL;
            saved.rewardLedger.committedRewardKeys = new[]
            {
                "research.reward.unknown.first-completion",
            };
            Assert.That(adapter.TryPrepareRestore(saved, out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);

            saved.rewardLedger.committedRewardKeys = new[]
            {
                ResearchStatusCatalog.GeneSplicingRewardKey,
            };
            Assert.That(adapter.TryPrepareRestore(saved, out error), Is.False);

            research.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.gene-splicing"));
            FormalThreeDResearchEffectStateSaveData captured =
                adapter.Capture();
            Assert.That(captured.rewardLedger.committedRewardKeys,
                Is.EqualTo(new[]
                {
                    ResearchStatusCatalog.GeneSplicingRewardKey,
                }));
            Assert.That(expansion.Characters[0].HasGeneSplicingTrait, Is.True);
        }

        [Test]
        public void TechnologyModelsRejectInvalidClocksAndRoundTripState()
        {
            var pulse = new ShieldPulseModel(8f);
            Assert.That(pulse.TryRestoreClock(7.5f), Is.True);
            Assert.That(pulse.Clock, Is.EqualTo(7.5f));
            Assert.That(pulse.TryRestoreClock(8f), Is.False);

            var source = new SingleCityDefenseTechnologyRuntime();
            source.Configure(new SingleCityDefenseTechnologyUnlocks(
                energyOverload: true));
            Assert.That(source.TryActivateOverload(
                "tower.instance.000001",
                WasteCity.Building.BuildingCatalog.LaserTower.Id.Value),
                Is.True);
            source.Advance(2f, paused: false);
            SingleCityDefenseTechnologyPersistenceSnapshot saved =
                source.CaptureForPersistence();
            var restored = new SingleCityDefenseTechnologyRuntime();
            restored.Configure(new SingleCityDefenseTechnologyUnlocks(
                energyOverload: true));
            Assert.That(restored.TryRestore(saved, out string error),
                Is.True, error);
            Assert.That(restored.Snapshot.Overloads[0].BoostRemaining,
                Is.EqualTo(3f).Within(.0001f));
        }

        [Test]
        public void MultipleSwordEmittersSharingTargetRoundTripWithoutEarlyStack()
        {
            GrayboxDefenseRuntime3D sourceDefense = DefenseWithEnemy(
                out SingleCityDefenseEnemySnapshot enemy);
            sourceDefense.ConfigureTechnologyStates(
                new SingleCityDefenseTechnologyUnlocks(swordIntent: true));
            var sourceState =
                new SingleCityDefenseTechnologyPersistenceSnapshot(
                    System.Array.Empty<
                        SingleCityDefenseOverloadPersistenceState>(),
                    new[]
                    {
                        new SingleCityDefenseEnemyTechnologyPersistenceState(
                            enemy.StableId,
                            enemy.EnemyDefinitionId,
                            EnemyCatalog.Gnawer.MaximumHealth,
                            enemy.X,
                            enemy.Z,
                            swordIntentStacks: 1,
                            infectionStacks: 0,
                            infectionElapsed: 0f,
                            resonanceRemaining: 0f,
                            controlled: false),
                    },
                    new[]
                    {
                        new SingleCityDefenseTechnologyEmitterPersistenceState(
                            "building.instance.000001",
                            enemy.StableId,
                            .25f),
                        new SingleCityDefenseTechnologyEmitterPersistenceState(
                            "building.instance.000002",
                            enemy.StableId,
                            .75f),
                    },
                    System.Array.Empty<
                        SingleCityDefenseTechnologyEmitterPersistenceState>());
            Assert.That(sourceDefense.TryRestoreTechnologyForPersistence(
                sourceState, out string error), Is.True, error);
            var sourceResearch = new ResearchModel();
            sourceResearch.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.sword-array"));
            using var sourceAdapter =
                new GrayboxResearchEffectStateSaveAdapter3D(
                    sourceResearch,
                    sourceDefense,
                    Expansion);

            FormalThreeDResearchEffectStateSaveData saved =
                sourceAdapter.Capture();

            Assert.That(saved.emitters, Has.Length.EqualTo(2));
            Assert.That(saved.emitters[0].targetEnemyStableId,
                Is.EqualTo(saved.emitters[1].targetEnemyStableId));

            GrayboxDefenseRuntime3D targetDefense = DefenseWithEnemy(
                out SingleCityDefenseEnemySnapshot targetEnemy);
            targetDefense.ConfigureTechnologyStates(
                new SingleCityDefenseTechnologyUnlocks(swordIntent: true));
            var targetResearch = new ResearchModel();
            targetResearch.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.sword-array"));
            CivilizationExpansionRuntime expansion = Expansion();
            using var targetAdapter =
                new GrayboxResearchEffectStateSaveAdapter3D(
                    targetResearch,
                    targetDefense,
                    () => expansion);
            Assert.That(targetAdapter.TryPrepareRestore(saved, out error),
                Is.True, error);
            Assert.That(targetAdapter.TryApplyPendingExpansionState(
                expansion, out error), Is.True, error);

            SingleCityDefenseTechnologyPersistenceSnapshot restored =
                targetDefense.CaptureTechnologyForPersistence();
            Assert.That(restored.SwordIntentEmitters, Has.Length.EqualTo(2));
            Assert.That(restored.SwordIntentEmitters[0].CooldownRemaining,
                Is.EqualTo(.25f).Within(.0001f));
            Assert.That(restored.SwordIntentEmitters[1].CooldownRemaining,
                Is.EqualTo(.75f).Within(.0001f));
            var probe = new SingleCityDefenseTechnologyRuntime();
            probe.Configure(new SingleCityDefenseTechnologyUnlocks(
                swordIntent: true));
            Assert.That(probe.TryRestore(restored, out error), Is.True, error);
            probe.ApplyTowerHit(
                "building.instance.000001",
                WasteCity.Building.BuildingCatalog.SwordArrayTower.Id.Value,
                targetEnemy.StableId,
                1,
                0f,
                1UL);
            probe.ApplyTowerHit(
                "building.instance.000002",
                WasteCity.Building.BuildingCatalog.SwordArrayTower.Id.Value,
                targetEnemy.StableId,
                1,
                0f,
                2UL);
            Assert.That(probe.Snapshot.Enemies[0].SwordIntentStacks,
                Is.EqualTo(1),
                "Loading emitter cooldowns must not grant an early stack.");
        }

        [Test]
        public void SecondCharacterFailureRollsBackFirstCharacterTrait()
        {
            var sourceResearch = new ResearchModel();
            CivilizationExpansionRuntime sourceExpansion = Expansion();
            using var source = new GrayboxResearchEffectStateSaveAdapter3D(
                sourceResearch,
                new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f),
                () => sourceExpansion);
            sourceResearch.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.gene-splicing"));
            var saved = source.Capture();
            System.Array.Resize(ref saved.states, 2);
            saved.states[1] = new WasteCity.Persistence.ThreeD.
                FormalThreeDResearchEffectStateEntrySaveData
            {
                stableStateId = "research.state.000002",
                creationOrdinal = 2,
                effectId = ResearchStatusCatalog.GeneSplicingTraitId,
                targetKind = WasteCity.Persistence.ThreeD.
                    FormalResearchEffectTargetKind.Character,
                targetStableId = WasteCity.Leader.CivilizationExpansion.
                    CharacterCatalog.LinXiId,
                phase = WasteCity.Persistence.ThreeD.
                    FormalResearchEffectStatePhase.Active,
                remainingRuleSeconds = 301f,
                stacks = 1,
                currentValue = 1.2f,
            };
            saved.nextStableStateOrdinal = 3;

            var targetResearch = new ResearchModel();
            targetResearch.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.gene-splicing"));
            CivilizationExpansionRuntime targetExpansion = Expansion();
            using var target = new GrayboxResearchEffectStateSaveAdapter3D(
                targetResearch,
                new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f),
                () => targetExpansion);
            Assert.That(target.TryPrepareRestore(saved, out string error),
                Is.True, error);
            Assert.That(target.TryApplyPendingExpansionState(
                targetExpansion, out error), Is.False);
            Assert.That(targetExpansion.Characters[0]
                .HasGeneSplicingTrait, Is.False);
            Assert.That(targetExpansion.Characters[1]
                .HasGeneSplicingTrait, Is.False);
        }

        [Test]
        public void DisappearedEffectGetsNewOrdinalWhenItReappears()
        {
            var research = new ResearchModel();
            CivilizationExpansionRuntime expansion = Expansion();
            using var adapter = new GrayboxResearchEffectStateSaveAdapter3D(
                research,
                new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f),
                () => expansion);

            Assert.That(expansion.Characters[0].TryApplyGeneSplicingTrait(),
                Is.True);
            var first = adapter.Capture();
            long firstOrdinal = first.states[0].creationOrdinal;
            ulong firstRevision = first.revision;

            expansion.Characters[0].TickTechnologyEffects(
                300f, paused: false);
            var disappeared = adapter.Capture();
            Assert.That(disappeared.states, Is.Empty);
            Assert.That(disappeared.revision, Is.GreaterThan(firstRevision));

            Assert.That(expansion.Characters[0].TryApplyGeneSplicingTrait(),
                Is.True);
            var reappeared = adapter.Capture();
            Assert.That(reappeared.states, Has.Length.EqualTo(1));
            Assert.That(reappeared.states[0].creationOrdinal,
                Is.GreaterThan(firstOrdinal));
            Assert.That(reappeared.revision,
                Is.GreaterThan(disappeared.revision));
        }

        [Test]
        public void InvalidBuildingStateRollsBackAlreadyAppliedDefenseState()
        {
            var research = new ResearchModel();
            research.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.energy-weapons"));
            research.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.mind-shield"));
            CivilizationExpansionRuntime expansion = Expansion();
            var defense = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            using var adapter = new GrayboxResearchEffectStateSaveAdapter3D(
                research, defense, () => expansion);
            var saved = adapter.Capture();
            saved.states = new[]
            {
                new WasteCity.Persistence.ThreeD.
                    FormalThreeDResearchEffectStateEntrySaveData
                {
                    stableStateId = "research.state.000001",
                    creationOrdinal = 1,
                    effectId = ResearchStatusCatalog.TechnologyOverloadId,
                    targetKind = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectTargetKind.Tower,
                    targetStableId = "tower.instance.000001",
                    phase = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectStatePhase.Boosting,
                    remainingRuleSeconds = 4f,
                    stacks = 1,
                },
                new WasteCity.Persistence.ThreeD.
                    FormalThreeDResearchEffectStateEntrySaveData
                {
                    stableStateId = "research.state.000002",
                    creationOrdinal = 2,
                    effectId = ResearchStatusCatalog.CityShieldId,
                    targetKind = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectTargetKind.City,
                    targetStableId = WorldLayerCatalog.PrimaryCity.Id,
                    phase = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectStatePhase.Active,
                    stacks = 1,
                    currentValue =
                        SingleCityDefenseTechnologyRules.MaximumShield + 1,
                },
            };
            saved.nextStableStateOrdinal = 3;

            Assert.That(adapter.TryPrepareRestore(saved, out string error),
                Is.True, error);
            Assert.That(adapter.TryApplyPendingExpansionState(
                expansion, out error), Is.False);
            Assert.That(defense.TechnologyState.Overloads, Is.Empty);
        }

        [Test]
        public void DownstreamCharacterFailureRollsBackAppliedCoreShieldToZero()
        {
            var research = new ResearchModel();
            research.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.mind-shield"));
            research.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.gene-splicing"));
            CivilizationExpansionRuntime expansion = Expansion();
            var defense = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(System.Array.Empty<GrayboxBuildingInstance3D>());
            typeof(GrayboxDefenseRuntime3D).GetField(
                    "campaign",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(defense, campaign);
            typeof(GrayboxDefenseRuntime3D).GetField(
                    "campaignBuildingHealth",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(defense, health);
            using var adapter = new GrayboxResearchEffectStateSaveAdapter3D(
                research, defense, () => expansion);
            var saved = adapter.Capture();
            saved.states = new[]
            {
                new WasteCity.Persistence.ThreeD.
                    FormalThreeDResearchEffectStateEntrySaveData
                {
                    stableStateId = "research.state.000001",
                    creationOrdinal = 1,
                    effectId = ResearchStatusCatalog.CityShieldId,
                    targetKind = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectTargetKind.City,
                    targetStableId = WorldLayerCatalog.PrimaryCity.Id,
                    phase = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectStatePhase.Active,
                    stacks = 1,
                    currentValue = 50,
                },
                new WasteCity.Persistence.ThreeD.
                    FormalThreeDResearchEffectStateEntrySaveData
                {
                    stableStateId = "research.state.000002",
                    creationOrdinal = 2,
                    effectId = ResearchStatusCatalog.GeneSplicingTraitId,
                    targetKind = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectTargetKind.Character,
                    targetStableId = expansion.Characters[0]
                        .Definition.Id.Value,
                    phase = WasteCity.Persistence.ThreeD.
                        FormalResearchEffectStatePhase.Active,
                    remainingRuleSeconds =
                        CharacterLifeRuntime
                            .GeneSplicingTraitDurationSeconds + 1f,
                    stacks = 1,
                },
            };
            saved.nextStableStateOrdinal = 3;

            Assert.That(adapter.TryPrepareRestore(saved, out string error),
                Is.True, error);
            Assert.That(adapter.TryApplyPendingExpansionState(
                expansion, out error), Is.False);
            Assert.That(defense.ActiveCampaignSnapshot.CoreShield, Is.Zero,
                "A later domain failure must restore the exact pre-transaction " +
                "core shield instead of retaining the already-applied value.");
        }

        [Test]
        public void PoliticsFailureRollsBackAlreadyAppliedGeneEffect()
        {
            var owner = new GameObject("civilization-save-rollback-test");
            try
            {
                CivilizationExpansionRuntime expansion = Expansion();
                GrayboxCivilizationExpansionController3D controller =
                    owner.AddComponent<
                        GrayboxCivilizationExpansionController3D>();
                typeof(GrayboxCivilizationExpansionController3D)
                    .GetProperty(
                        "Runtime",
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .SetValue(controller, expansion);
                var research = new ResearchModel();
                research.GrantCompletedForDevelopment(
                    ResearchCatalog.Find("core.research.gene-splicing"));
                var defense = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
                using var effects =
                    new GrayboxResearchEffectStateSaveAdapter3D(
                        research, defense, () => expansion);
                var adapter =
                    new GrayboxCivilizationExpansionSaveAdapter3D(controller);
                adapter.ConfigureResearchEffectStateAdapter(effects);
                var saved = adapter.Capture();
                saved.charactersPolitics.characters[1].characterId =
                    "core.character.unknown";
                string leaderId = expansion.Characters[0].Definition.Id.Value;
                var effectState = new WasteCity.Persistence.ThreeD.
                    FormalThreeDResearchEffectStateSaveData
                {
                    revision = 1,
                    nextStableStateOrdinal = 2,
                    states = new[]
                    {
                        new WasteCity.Persistence.ThreeD.
                            FormalThreeDResearchEffectStateEntrySaveData
                        {
                            stableStateId = "research.state.000001",
                            creationOrdinal = 1,
                            effectId =
                                ResearchStatusCatalog.GeneSplicingTraitId,
                            targetKind = WasteCity.Persistence.ThreeD.
                                FormalResearchEffectTargetKind.Character,
                            targetStableId = leaderId,
                            phase = WasteCity.Persistence.ThreeD.
                                FormalResearchEffectStatePhase.Active,
                            remainingRuleSeconds = 300f,
                            stacks = 1,
                            currentValue = 1.2f,
                        },
                    },
                    rewardLedger = new WasteCity.Persistence.ThreeD.
                        FormalThreeDResearchRewardLedgerSaveData
                    {
                        committedRewardKeys = new[]
                        {
                            ResearchStatusCatalog.GeneSplicingRewardKey,
                        },
                    },
                };
                Assert.That(effects.TryPrepareRestore(
                    effectState, out string error), Is.True, error);
                int originalHealth = expansion.Characters[0].CurrentHealth;
                int originalMaximum = expansion.Characters[0].MaximumHealth;

                Assert.That(adapter.TryRestore(saved, out error), Is.False);
                Assert.That(expansion.Characters[0].HasGeneSplicingTrait,
                    Is.False);
                Assert.That(expansion.Characters[0].CurrentHealth,
                    Is.EqualTo(originalHealth));
                Assert.That(expansion.Characters[0].MaximumHealth,
                    Is.EqualTo(originalMaximum));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static CivilizationExpansionRuntime Expansion()
        {
            return new CivilizationExpansionRuntime(
                new WorldMapModel(12, 12, new WorldSeed(8128)),
                6, 6, new Endpoint());
        }

        private static GrayboxDefenseRuntime3D DefenseWithEnemy(
            out SingleCityDefenseEnemySnapshot enemy)
        {
            var definition = new SingleCityDefenseCampaignDefinition(
                "test.campaign.research-emitter",
                new CampaignWaveDefinition(
                    1,
                    0f,
                    0f,
                    new[] { CampaignSpawnDirection.East },
                    new WaveEntry(EnemyArchetype.Gnawer, 1)));
            var campaign = new SingleCityDefenseCampaignModel(
                0f,
                0f,
                definition);
            Assert.That(campaign.TryStartAfterExternalWarning(), Is.True);
            campaign.Advance(.2f, requestedSpeed: 1);
            enemy = campaign.Snapshot.Enemies[0];
            var defense = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            typeof(GrayboxDefenseRuntime3D).GetField(
                    "campaign",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(defense, campaign);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(System.Array.Empty<GrayboxBuildingInstance3D>());
            typeof(GrayboxDefenseRuntime3D).GetField(
                    "campaignBuildingHealth",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(defense, health);
            return defense;
        }

        private sealed class Endpoint : ISettlementInventoryEndpoint
        {
            private readonly SettlementInventory inventory =
                new SettlementInventory(150);
            public string StableSettlementId =>
                WorldLayerCatalog.PrimaryCity.Id;
            public int GetAmount(string resourceId) => inventory.Get(resourceId);
            public int AcceptableSpace => inventory.FreeSpace;
            public bool TryExtract(IReadOnlyList<ResourceAmount> amounts) =>
                inventory.TryExtract(amounts);
            public bool TryAccept(IReadOnlyList<ResourceAmount> amounts) =>
                inventory.TryAccept(amounts);
        }
    }
}
