using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WasteCity.CivilizationExpansion;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxResearchEffectStateSaveAdapter3D : IDisposable
    {
        public const string GeneSplicingRewardKey =
            ResearchStatusCatalog.GeneSplicingRewardKey;

        private readonly ResearchModel research;
        private readonly GrayboxDefenseRuntime3D defense;
        private readonly Func<CivilizationExpansionRuntime> expansionProvider;
        private readonly SortedSet<string> committedRewardKeys =
            new SortedSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> ordinalByKey =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private long nextOrdinal = 1L;
        private ulong revision;
        private FormalThreeDResearchEffectStateSaveData pending;
        private string lastStateFingerprint = string.Empty;

        public GrayboxResearchEffectStateSaveAdapter3D(
            ResearchModel research,
            GrayboxDefenseRuntime3D defense,
            Func<CivilizationExpansionRuntime> expansionProvider)
        {
            this.research = research ??
                throw new ArgumentNullException(nameof(research));
            this.defense = defense ??
                throw new ArgumentNullException(nameof(defense));
            this.expansionProvider = expansionProvider;
            research.Completed += HandleResearchCompleted;
        }

        public ulong Revision => revision;

        public FormalThreeDResearchEffectStateSaveData Capture()
        {
            var drafts = new List<EntryDraft>();
            var emitterDrafts = new List<EmitterDraft>();
            CaptureDefense(drafts, emitterDrafts);
            CivilizationExpansionRuntime expansion = expansionProvider?.Invoke();
            if (expansion != null) CaptureExpansion(expansion, drafts);
            drafts.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            emitterDrafts.Sort((left, right) =>
                string.CompareOrdinal(left.Key, right.Key));
            var liveKeys = new HashSet<string>(
                drafts.Select(value => value.Key), StringComparer.Ordinal);
            for (var index = 0; index < emitterDrafts.Count; index++)
                liveKeys.Add(emitterDrafts[index].Key);
            string[] removed = ordinalByKey.Keys
                .Where(key => !liveKeys.Contains(key)).ToArray();
            for (var index = 0; index < removed.Length; index++)
                ordinalByKey.Remove(removed[index]);
            var states = new FormalThreeDResearchEffectStateEntrySaveData[
                drafts.Count];
            for (var index = 0; index < drafts.Count; index++)
            {
                EntryDraft draft = drafts[index];
                if (!ordinalByKey.TryGetValue(draft.Key, out long ordinal))
                {
                    ordinal = nextOrdinal++;
                    ordinalByKey.Add(draft.Key, ordinal);
                    unchecked { revision++; }
                }
                states[index] = draft.ToSaveData(ordinal);
            }
            Array.Sort(states, (left, right) =>
                left.creationOrdinal.CompareTo(right.creationOrdinal));
            var emitters = new FormalThreeDResearchEffectEmitterSaveData[
                emitterDrafts.Count];
            for (var index = 0; index < emitterDrafts.Count; index++)
            {
                EmitterDraft draft = emitterDrafts[index];
                if (!ordinalByKey.TryGetValue(draft.Key, out long ordinal))
                {
                    ordinal = nextOrdinal++;
                    ordinalByKey.Add(draft.Key, ordinal);
                    unchecked { revision++; }
                }
                emitters[index] = draft.ToSaveData(ordinal);
            }
            Array.Sort(emitters, (left, right) =>
                left.creationOrdinal.CompareTo(right.creationOrdinal));
            string fingerprint = ComputeFingerprint(
                states, emitters, committedRewardKeys);
            if (!string.Equals(fingerprint, lastStateFingerprint,
                    StringComparison.Ordinal))
            {
                lastStateFingerprint = fingerprint;
                unchecked { revision++; }
            }
            return new FormalThreeDResearchEffectStateSaveData
            {
                configurationSignature =
                    FormalThreeDResearchEffectStateSaveData
                        .ConfigurationSignature,
                revision = revision,
                nextStableStateOrdinal = nextOrdinal,
                states = states,
                emitters = emitters,
                rewardLedger = new FormalThreeDResearchRewardLedgerSaveData
                {
                    committedRewardKeys = committedRewardKeys.ToArray(),
                },
            };
        }

        public bool TryPrepareRestore(
            FormalThreeDResearchEffectStateSaveData source,
            out string error)
        {
            if (source?.states == null || source.emitters == null ||
                source.rewardLedger?.committedRewardKeys == null ||
                !string.Equals(source.configurationSignature,
                    FormalThreeDResearchEffectStateSaveData.ConfigurationSignature,
                    StringComparison.Ordinal))
            {
                error = "研究效果状态不完整或配置签名不兼容";
                return false;
            }
            var emitterPairs = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.emitters.Length; index++)
            {
                FormalThreeDResearchEffectEmitterSaveData emitter =
                    source.emitters[index];
                bool allowed = emitter != null &&
                    (string.Equals(
                         emitter.effectId,
                         ResearchStatusCatalog.SwordIntentId,
                         StringComparison.Ordinal) ||
                     string.Equals(
                         emitter.effectId,
                         ResearchStatusCatalog.InfectionId,
                         StringComparison.Ordinal));
                ResearchStatusDefinition definition = allowed
                    ? ResearchStatusCatalog.Find(emitter.effectId)
                    : null;
                ResearchDefinition sourceResearch = definition == null
                    ? null
                    : ResearchCatalog.Find(definition.SourceResearchId);
                string pair = emitter == null
                    ? string.Empty
                    : EmitterKey(emitter);
                if (!allowed || definition == null ||
                    sourceResearch == null ||
                    !research.IsCompleted(sourceResearch.Id) ||
                    string.IsNullOrWhiteSpace(emitter.sourceTowerStableId) ||
                    string.IsNullOrWhiteSpace(emitter.targetEnemyStableId) ||
                    float.IsNaN(emitter.cooldownRemaining) ||
                    float.IsInfinity(emitter.cooldownRemaining) ||
                    emitter.cooldownRemaining <= 0f ||
                    emitter.cooldownRemaining > 1f ||
                    !emitterPairs.Add(pair))
                {
                    error = "研究命中周期状态无效或来源科技尚未完成";
                    return false;
                }
            }
            var rewardKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.states.Length; index++)
            {
                ResearchStatusDefinition definition =
                    ResearchStatusCatalog.Find(source.states[index]?.effectId);
                ResearchDefinition sourceResearch = definition == null
                    ? null
                    : ResearchCatalog.Find(definition.SourceResearchId);
                if (sourceResearch == null ||
                    !research.IsCompleted(sourceResearch.Id))
                {
                    error = "研究效果来源科技尚未完成";
                    return false;
                }
            }
            for (var index = 0;
                 index < source.rewardLedger.committedRewardKeys.Length;
                 index++)
            {
                string rewardKey =
                    source.rewardLedger.committedRewardKeys[index];
                if (!rewardKeys.Add(rewardKey) ||
                    !string.Equals(
                        rewardKey,
                        GeneSplicingRewardKey,
                        StringComparison.Ordinal))
                {
                    error = "研究一次性奖励键无效";
                    return false;
                }
                ResearchDefinition geneSplicing = ResearchCatalog.Find(
                    ResearchStatusCatalog.Find(
                        ResearchStatusCatalog.GeneSplicingTraitId)
                        .SourceResearchId);
                if (geneSplicing == null ||
                    !research.IsCompleted(geneSplicing.Id))
                {
                    error = "研究一次性奖励来源科技尚未完成";
                    return false;
                }
            }
            pending = Clone(source);
            error = string.Empty;
            return true;
        }

        public bool TryApplyPendingExpansionState(
            CivilizationExpansionRuntime expansion,
            out string error)
        {
            if (expansion == null || pending == null)
            {
                error = pending == null ? string.Empty : "文明扩展运行时不存在";
                return pending == null;
            }
            FormalThreeDResearchEffectStateSaveData target = pending;
            SingleCityDefenseTechnologyPersistenceSnapshot defenseBefore =
                defense.CaptureTechnologyForPersistence();
            GrayboxBuildingTechnologySnapshot3D buildingBefore =
                defense.BuildingTechnologyState;
            int coreBefore = defense.ActiveCampaignSnapshot?.CoreShield ?? 0;
            ArmyTechnologyPersistenceSnapshot armyBefore =
                expansion.Army.CaptureTechnologyState();
            var charactersBefore = new CharacterTechnologyPersistenceState[
                expansion.Characters.Count];
            for (var index = 0; index < charactersBefore.Length; index++)
                charactersBefore[index] =
                    expansion.Characters[index].CaptureTechnologyState();

            if (!TryRestoreDefense(
                    target.states,
                    target.emitters,
                    out error))
            {
                Rollback(defenseBefore, buildingBefore, coreBefore,
                    expansion, armyBefore, charactersBefore);
                return false;
            }
            ArmyTechnologyPersistenceSnapshot army = ToArmyState(
                expansion, target.states);
            if (!expansion.Army.TryRestoreTechnologyState(army, out error))
            {
                Rollback(defenseBefore, buildingBefore, coreBefore,
                    expansion, armyBefore, charactersBefore);
                return false;
            }
            for (var index = 0; index < expansion.Characters.Count; index++)
            {
                CharacterLifeRuntime character = expansion.Characters[index];
                float remaining = FindGeneRemaining(
                    target.states, character.Definition.Id.Value);
                if (!character.TryRestoreTechnologyState(
                        new CharacterTechnologyPersistenceState(
                            character.Definition.Id.Value, remaining),
                        out error))
                {
                    Rollback(defenseBefore, buildingBefore, coreBefore,
                        expansion, armyBefore, charactersBefore);
                    return false;
                }
            }
            committedRewardKeys.Clear();
            for (var index = 0;
                 index < target.rewardLedger.committedRewardKeys.Length;
                 index++)
                committedRewardKeys.Add(
                    target.rewardLedger.committedRewardKeys[index]);
            nextOrdinal = target.nextStableStateOrdinal;
            revision = target.revision;
            ordinalByKey.Clear();
            for (var index = 0; index < target.states.Length; index++)
                ordinalByKey[Key(target.states[index])] =
                    target.states[index].creationOrdinal;
            for (var index = 0; index < target.emitters.Length; index++)
                ordinalByKey[EmitterKey(target.emitters[index])] =
                    target.emitters[index].creationOrdinal;
            lastStateFingerprint = ComputeFingerprint(
                target.states, target.emitters, committedRewardKeys);
            pending = null;
            error = string.Empty;
            return true;
        }

        public void Dispose()
        {
            research.Completed -= HandleResearchCompleted;
        }

        private void HandleResearchCompleted(ResearchDefinition definition)
        {
            if (definition == null || !string.Equals(
                    definition.Id.Value,
                    "core.research.gene-splicing",
                    StringComparison.Ordinal) ||
                committedRewardKeys.Contains(GeneSplicingRewardKey))
                return;
            CivilizationExpansionRuntime expansion = expansionProvider?.Invoke();
            if (expansion == null ||
                !expansion.TryApplyGeneSplicingToCurrentLeader()) return;
            committedRewardKeys.Add(GeneSplicingRewardKey);
            unchecked { revision++; }
        }

        private void CaptureDefense(
            List<EntryDraft> result,
            List<EmitterDraft> emitters)
        {
            SingleCityDefenseTechnologyPersistenceSnapshot snapshot =
                defense.CaptureTechnologyForPersistence();
            for (var index = 0; index < snapshot.Overloads.Length; index++)
            {
                var item = snapshot.Overloads[index];
                FormalResearchEffectStatePhase phase;
                float remaining;
                if (item.BoostRemaining > 0f)
                {
                    phase = FormalResearchEffectStatePhase.Boosting;
                    remaining = item.BoostRemaining;
                }
                else if (item.LockoutRemaining > 0f)
                {
                    phase = FormalResearchEffectStatePhase.Lockout;
                    remaining = item.LockoutRemaining;
                }
                else if (item.CooldownRemaining > 0f)
                {
                    phase = FormalResearchEffectStatePhase.Cooldown;
                    remaining = item.CooldownRemaining;
                }
                else continue;
                result.Add(new EntryDraft(
                    ResearchStatusCatalog.TechnologyOverloadId,
                    FormalResearchEffectTargetKind.Tower,
                    item.TowerStableId, phase, remaining, 1, 0f, 0f));
            }
            for (var index = 0; index < snapshot.Enemies.Length; index++)
            {
                var item = snapshot.Enemies[index];
                AddEnemy(result, ResearchStatusCatalog.SwordIntentId,
                    item.StableEnemyId, item.SwordIntentStacks, 0f, 0f);
                AddEnemy(result, ResearchStatusCatalog.InfectionId,
                    item.StableEnemyId, item.InfectionStacks,
                    item.InfectionElapsed, 0f);
                if (item.ResonanceRemaining > 0f)
                    result.Add(new EntryDraft(
                        ResearchStatusCatalog.PsionicResonanceId,
                        FormalResearchEffectTargetKind.Enemy,
                        item.StableEnemyId,
                        FormalResearchEffectStatePhase.Active,
                        item.ResonanceRemaining, 1, 0f, 0f));
                if (item.Controlled)
                    result.Add(new EntryDraft(
                        ResearchStatusCatalog.MindControlId,
                        FormalResearchEffectTargetKind.Enemy,
                        item.StableEnemyId,
                        FormalResearchEffectStatePhase.Active,
                        0f, 1, 0f, 0f));
            }
            for (var index = 0;
                 index < snapshot.SwordIntentEmitters.Length;
                 index++)
            {
                SingleCityDefenseTechnologyEmitterPersistenceState item =
                    snapshot.SwordIntentEmitters[index];
                emitters.Add(new EmitterDraft(
                    ResearchStatusCatalog.SwordIntentId,
                    item.TowerStableId,
                    item.TargetStableEnemyId,
                    item.CooldownRemaining));
            }
            for (var index = 0;
                 index < snapshot.InfectionEmitters.Length;
                 index++)
            {
                SingleCityDefenseTechnologyEmitterPersistenceState item =
                    snapshot.InfectionEmitters[index];
                emitters.Add(new EmitterDraft(
                    ResearchStatusCatalog.InfectionId,
                    item.TowerStableId,
                    item.TargetStableEnemyId,
                    item.CooldownRemaining));
            }
            GrayboxBuildingTechnologySnapshot3D buildings =
                defense.BuildingTechnologyState;
            if (buildings != null)
            {
                for (var index = 0; index < buildings.Buildings.Count; index++)
                {
                    var item = buildings.Buildings[index];
                    if (item.TissueRemainder > 0f)
                        result.Add(new EntryDraft(
                            ResearchStatusCatalog.TissueRegenerationId,
                            FormalResearchEffectTargetKind.Building,
                            item.StableInstanceId,
                            FormalResearchEffectStatePhase.Active, 0f, 1,
                            item.TissueRemainder, 0f));
                    if (item.CarapaceClock > 0f)
                        result.Add(new EntryDraft(
                            ResearchStatusCatalog.CarapaceRegenerationId,
                            FormalResearchEffectTargetKind.Building,
                            item.StableInstanceId,
                            FormalResearchEffectStatePhase.Active, 0f, 1,
                            item.CarapaceClock, 0f));
                    if (item.Shield > 0)
                        result.Add(new EntryDraft(
                            ResearchStatusCatalog.CityShieldId,
                            FormalResearchEffectTargetKind.Building,
                            item.StableInstanceId,
                            FormalResearchEffectStatePhase.Active, 0f, 1,
                            item.ShieldPulseClock, item.Shield));
                    if (item.RepairClock > 0f)
                        result.Add(new EntryDraft(
                            ResearchStatusCatalog.AutomatedRepairId,
                            FormalResearchEffectTargetKind.Building,
                            item.StableInstanceId,
                            FormalResearchEffectStatePhase.Active, 0f, 1,
                            item.RepairClock, 0f));
                    if (item.ShieldPulseClock > 0f && item.Shield == 0)
                        result.Add(new EntryDraft(
                            ResearchStatusCatalog.CityShieldId,
                            FormalResearchEffectTargetKind.Building,
                            item.StableInstanceId,
                            FormalResearchEffectStatePhase.Active, 0f, 1,
                            item.ShieldPulseClock, 0f));
                }
            }
            SingleCityDefenseCampaignSnapshot campaign =
                defense.ActiveCampaignSnapshot;
            if (campaign?.CoreShield > 0)
            {
                CivilizationExpansionRuntime expansion = expansionProvider?.Invoke();
                string cityId = expansion?.WorldLayer?.PrimaryCity?.StableId;
                if (!string.IsNullOrWhiteSpace(cityId))
                    result.Add(new EntryDraft(
                        ResearchStatusCatalog.CityShieldId,
                        FormalResearchEffectTargetKind.City, cityId,
                        FormalResearchEffectStatePhase.Active, 0f, 1, 0f,
                        campaign.CoreShield));
            }
        }

        private static void AddEnemy(List<EntryDraft> result, string effectId,
            string targetId, int stacks, float period, float value)
        {
            if (stacks <= 0) return;
            result.Add(new EntryDraft(effectId,
                FormalResearchEffectTargetKind.Enemy, targetId,
                FormalResearchEffectStatePhase.Active, 0f, stacks,
                period, value));
        }

        private static void CaptureExpansion(
            CivilizationExpansionRuntime expansion,
            List<EntryDraft> result)
        {
            ArmyTechnologyPersistenceSnapshot army =
                expansion.Army.CaptureTechnologyState();
            for (var index = 0; index < army.Units.Length; index++)
            {
                var item = army.Units[index];
                if (item.RegenerationAccumulatorSeconds <= 0f) continue;
                result.Add(new EntryDraft(
                    ResearchStatusCatalog.TissueRegenerationId,
                    FormalResearchEffectTargetKind.ArmyUnit,
                    item.StableUnitId,
                    FormalResearchEffectStatePhase.Active, 0f, 1,
                    item.RegenerationAccumulatorSeconds, 0f));
            }
            for (var index = 0; index < expansion.Characters.Count; index++)
            {
                CharacterTechnologyPersistenceState item =
                    expansion.Characters[index].CaptureTechnologyState();
                if (item.GeneSplicingRemainingSeconds <= 0f) continue;
                result.Add(new EntryDraft(
                    ResearchStatusCatalog.GeneSplicingTraitId,
                    FormalResearchEffectTargetKind.Character,
                    item.CharacterId,
                    FormalResearchEffectStatePhase.Active,
                    item.GeneSplicingRemainingSeconds, 1, 0f,
                    CharacterLifeRuntime.GeneSplicingMaximumHealthMultiplier));
            }
        }

        private bool TryRestoreDefense(
            FormalThreeDResearchEffectStateEntrySaveData[] states,
            FormalThreeDResearchEffectEmitterSaveData[] emitters,
            out string error)
        {
            SingleCityDefenseCampaignSnapshot campaign =
                defense.ActiveCampaignSnapshot;
            var enemyById = new Dictionary<string,
                SingleCityDefenseEnemySnapshot>(StringComparer.Ordinal);
            if (campaign != null)
                for (var index = 0; index < campaign.Enemies.Count; index++)
                    enemyById[campaign.Enemies[index].StableId] =
                        campaign.Enemies[index];
            var overloads = new List<SingleCityDefenseOverloadPersistenceState>();
            var enemyValues = new Dictionary<string, EnemyDraft>(StringComparer.Ordinal);
            var buildingValues = new Dictionary<string, BuildingDraft>(StringComparer.Ordinal);
            int coreShield = 0;
            for (var index = 0; index < states.Length; index++)
            {
                var item = states[index];
                if (item.effectId == ResearchStatusCatalog.TechnologyOverloadId)
                {
                    float cooldown = item.phase == FormalResearchEffectStatePhase.Boosting
                        ? TechnologyOverloadModel.CooldownSeconds -
                            TechnologyOverloadModel.BoostSeconds +
                            item.remainingRuleSeconds
                        : item.phase == FormalResearchEffectStatePhase.Lockout
                            ? TechnologyOverloadModel.CooldownSeconds -
                                TechnologyOverloadModel.BoostSeconds -
                                TechnologyOverloadModel.LockoutSeconds +
                                item.remainingRuleSeconds
                            : item.remainingRuleSeconds;
                    overloads.Add(new SingleCityDefenseOverloadPersistenceState(
                        item.targetStableId, cooldown,
                        item.phase == FormalResearchEffectStatePhase.Boosting
                            ? item.remainingRuleSeconds : 0f,
                        item.phase == FormalResearchEffectStatePhase.Lockout
                            ? item.remainingRuleSeconds : 0f));
                }
                else if (item.targetKind == FormalResearchEffectTargetKind.Enemy)
                {
                    if (!enemyValues.TryGetValue(item.targetStableId, out var value))
                        value = new EnemyDraft();
                    value.Apply(item);
                    enemyValues[item.targetStableId] = value;
                }
                else if (item.targetKind == FormalResearchEffectTargetKind.Building)
                {
                    if (!buildingValues.TryGetValue(item.targetStableId, out var value))
                        value = new BuildingDraft();
                    value.Apply(item);
                    buildingValues[item.targetStableId] = value;
                }
                else if (item.targetKind == FormalResearchEffectTargetKind.City &&
                    item.effectId == ResearchStatusCatalog.CityShieldId)
                    coreShield = (int)Math.Round(item.currentValue);
            }
            var swordEmitters = new List<
                SingleCityDefenseTechnologyEmitterPersistenceState>();
            var infectionEmitters = new List<
                SingleCityDefenseTechnologyEmitterPersistenceState>();
            for (var index = 0; index < emitters.Length; index++)
            {
                FormalThreeDResearchEffectEmitterSaveData item =
                    emitters[index];
                if (!enemyValues.ContainsKey(item.targetEnemyStableId))
                    enemyValues.Add(item.targetEnemyStableId, new EnemyDraft());
                var value =
                    new SingleCityDefenseTechnologyEmitterPersistenceState(
                        item.sourceTowerStableId,
                        item.targetEnemyStableId,
                        item.cooldownRemaining);
                if (item.effectId == ResearchStatusCatalog.SwordIntentId)
                    swordEmitters.Add(value);
                else
                    infectionEmitters.Add(value);
            }
            var enemies = new List<SingleCityDefenseEnemyTechnologyPersistenceState>();
            foreach (var pair in enemyValues)
            {
                if (!enemyById.TryGetValue(pair.Key, out var topology))
                {
                    error = "研究效果引用未知防御敌人";
                    return false;
                }
                EnemyDefinition definition = FindEnemy(
                    topology.EnemyDefinitionId);
                if (definition == null)
                {
                    error = "研究效果引用未知敌人定义";
                    return false;
                }
                enemies.Add(pair.Value.ToState(topology, definition));
            }
            if (!defense.TryRestoreTechnologyForPersistence(
                    new SingleCityDefenseTechnologyPersistenceSnapshot(
                        overloads.ToArray(),
                        enemies.ToArray(),
                        swordEmitters.ToArray(),
                        infectionEmitters.ToArray()), out error))
                return false;
            GrayboxBuildingTechnologySnapshot3D current =
                defense.BuildingTechnologyState;
            var buildings = new List<GrayboxBuildingTechnologyStateSnapshot3D>();
            if (current != null)
                for (var index = 0; index < current.Buildings.Count; index++)
                {
                    var item = current.Buildings[index];
                    buildingValues.TryGetValue(item.StableInstanceId, out var value);
                    buildings.Add(new GrayboxBuildingTechnologyStateSnapshot3D(
                        item.StableInstanceId, item.BuildingId,
                        item.CurrentHealth, item.MaximumHealth,
                        value?.Shield ?? 0, item.Destroyed,
                        value?.Tissue ?? 0f, value?.Carapace ?? 0f,
                        value?.Repair ?? 0f, value?.ShieldPulse ?? 0f));
                }
            return defense.TryRestoreBuildingTechnologyForPersistence(
                buildings, coreShield, out error);
        }

        private static ArmyTechnologyPersistenceSnapshot ToArmyState(
            CivilizationExpansionRuntime expansion,
            FormalThreeDResearchEffectStateEntrySaveData[] states)
        {
            ArmyTechnologyPersistenceSnapshot current =
                expansion.Army.CaptureTechnologyState();
            var saved = new Dictionary<string, float>(StringComparer.Ordinal);
            for (var index = 0; index < states.Length; index++)
                if (states[index].effectId ==
                        ResearchStatusCatalog.TissueRegenerationId &&
                    states[index].targetKind ==
                        FormalResearchEffectTargetKind.ArmyUnit)
                    saved[states[index].targetStableId] =
                        states[index].periodAccumulatorSeconds;
            var result = new ArmyTechnologyUnitPersistenceState[current.Units.Length];
            for (var index = 0; index < result.Length; index++)
                result[index] = new ArmyTechnologyUnitPersistenceState(
                    current.Units[index].StableUnitId,
                    saved.TryGetValue(current.Units[index].StableUnitId,
                        out float value) ? value : 0f);
            return new ArmyTechnologyPersistenceSnapshot(result);
        }

        private static float FindGeneRemaining(
            FormalThreeDResearchEffectStateEntrySaveData[] states, string id)
        {
            for (var index = 0; index < states.Length; index++)
                if (states[index].effectId ==
                        ResearchStatusCatalog.GeneSplicingTraitId &&
                    states[index].targetKind ==
                        FormalResearchEffectTargetKind.Character &&
                    states[index].targetStableId == id)
                    return states[index].remainingRuleSeconds;
            return 0f;
        }

        private static EnemyDefinition FindEnemy(string id)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
                if (string.Equals(EnemyCatalog.All[index].Id.Value, id,
                        StringComparison.Ordinal))
                    return EnemyCatalog.All[index];
            return null;
        }

        private void Rollback(
            SingleCityDefenseTechnologyPersistenceSnapshot defenseBefore,
            GrayboxBuildingTechnologySnapshot3D buildingBefore,
            int coreBefore,
            CivilizationExpansionRuntime expansion,
            ArmyTechnologyPersistenceSnapshot armyBefore,
            CharacterTechnologyPersistenceState[] charactersBefore)
        {
            defense.TryRestoreTechnologyForPersistence(defenseBefore, out _);
            defense.TryRestoreBuildingTechnologyForPersistence(
                buildingBefore?.Buildings, coreBefore, out _);
            expansion.Army.TryRestoreTechnologyState(armyBefore, out _);
            for (var index = 0;
                 index < expansion.Characters.Count &&
                 index < charactersBefore.Length;
                 index++)
                expansion.Characters[index].TryRestoreTechnologyState(
                    charactersBefore[index], out _);
        }

        private static FormalThreeDResearchEffectStateSaveData Clone(
            FormalThreeDResearchEffectStateSaveData source)
        {
            var states = new FormalThreeDResearchEffectStateEntrySaveData[
                source.states.Length];
            for (var index = 0; index < states.Length; index++)
            {
                var item = source.states[index];
                states[index] = new FormalThreeDResearchEffectStateEntrySaveData
                {
                    stableStateId = item.stableStateId,
                    creationOrdinal = item.creationOrdinal,
                    effectId = item.effectId,
                    targetKind = item.targetKind,
                    targetStableId = item.targetStableId,
                    phase = item.phase,
                    remainingRuleSeconds = item.remainingRuleSeconds,
                    stacks = item.stacks,
                    periodAccumulatorSeconds =
                        item.periodAccumulatorSeconds,
                    currentValue = item.currentValue,
                };
            }
            var emitters = new FormalThreeDResearchEffectEmitterSaveData[
                source.emitters.Length];
            for (var index = 0; index < emitters.Length; index++)
            {
                FormalThreeDResearchEffectEmitterSaveData item =
                    source.emitters[index];
                emitters[index] =
                    new FormalThreeDResearchEffectEmitterSaveData
                    {
                        stableStateId = item.stableStateId,
                        creationOrdinal = item.creationOrdinal,
                        effectId = item.effectId,
                        sourceTowerStableId = item.sourceTowerStableId,
                        targetEnemyStableId = item.targetEnemyStableId,
                        cooldownRemaining = item.cooldownRemaining,
                    };
            }
            return new FormalThreeDResearchEffectStateSaveData
            {
                configurationSignature = source.configurationSignature,
                revision = source.revision,
                nextStableStateOrdinal = source.nextStableStateOrdinal,
                states = states,
                emitters = emitters,
                rewardLedger = new FormalThreeDResearchRewardLedgerSaveData
                {
                    committedRewardKeys =
                        (string[])source.rewardLedger.committedRewardKeys.Clone(),
                },
            };
        }

        private static string Key(FormalThreeDResearchEffectStateEntrySaveData item) =>
            item.effectId + "\n" + ((int)item.targetKind).ToString(
                CultureInfo.InvariantCulture) + "\n" + item.targetStableId;

        private static string EmitterKey(
            FormalThreeDResearchEffectEmitterSaveData item) =>
            "emitter\n" + item.effectId + "\n" +
            item.sourceTowerStableId + "\n" + item.targetEnemyStableId;

        private static string ComputeFingerprint(
            IEnumerable<FormalThreeDResearchEffectStateEntrySaveData> states,
            IEnumerable<FormalThreeDResearchEffectEmitterSaveData> emitters,
            IEnumerable<string> rewardKeys) =>
            string.Join("|", states.Select(value =>
                Key(value) + ":" + (int)value.phase + ":" +
                value.remainingRuleSeconds.ToString("R",
                    CultureInfo.InvariantCulture) + ":" + value.stacks +
                ":" + value.periodAccumulatorSeconds.ToString("R",
                    CultureInfo.InvariantCulture) + ":" +
                value.currentValue.ToString("R",
                    CultureInfo.InvariantCulture))) + "#" +
            string.Join("|", emitters.Select(value =>
                EmitterKey(value) + ":" +
                value.cooldownRemaining.ToString("R",
                    CultureInfo.InvariantCulture))) + "#" +
            string.Join("|", rewardKeys);

        private sealed class EntryDraft
        {
            public EntryDraft(string effectId, FormalResearchEffectTargetKind kind,
                string targetId, FormalResearchEffectStatePhase phase,
                float remaining, int stacks, float period, float value)
            {
                EffectId = effectId; Kind = kind; TargetId = targetId;
                Phase = phase; Remaining = remaining; Stacks = stacks;
                Period = period; Value = value;
            }
            public string EffectId, TargetId;
            public FormalResearchEffectTargetKind Kind;
            public FormalResearchEffectStatePhase Phase;
            public float Remaining, Period, Value;
            public int Stacks;
            public string Key => EffectId + "\n" + (int)Kind + "\n" + TargetId;
            public FormalThreeDResearchEffectStateEntrySaveData ToSaveData(long ordinal) =>
                new FormalThreeDResearchEffectStateEntrySaveData
                {
                    stableStateId = "research.state." + ordinal.ToString("D6",
                        CultureInfo.InvariantCulture),
                    creationOrdinal = ordinal, effectId = EffectId,
                    targetKind = Kind, targetStableId = TargetId, phase = Phase,
                    remainingRuleSeconds = Remaining, stacks = Stacks,
                    periodAccumulatorSeconds = Period, currentValue = Value,
                };
        }

        private sealed class EmitterDraft
        {
            public EmitterDraft(
                string effectId,
                string sourceTowerStableId,
                string targetEnemyStableId,
                float cooldownRemaining)
            {
                EffectId = effectId;
                SourceTowerStableId = sourceTowerStableId;
                TargetEnemyStableId = targetEnemyStableId;
                CooldownRemaining = cooldownRemaining;
            }

            public string EffectId { get; }
            public string SourceTowerStableId { get; }
            public string TargetEnemyStableId { get; }
            public float CooldownRemaining { get; }
            public string Key => "emitter\n" + EffectId + "\n" +
                SourceTowerStableId + "\n" + TargetEnemyStableId;

            public FormalThreeDResearchEffectEmitterSaveData ToSaveData(
                long ordinal)
            {
                return new FormalThreeDResearchEffectEmitterSaveData
                {
                    stableStateId = "research.state." + ordinal.ToString(
                        "D6",
                        CultureInfo.InvariantCulture),
                    creationOrdinal = ordinal,
                    effectId = EffectId,
                    sourceTowerStableId = SourceTowerStableId,
                    targetEnemyStableId = TargetEnemyStableId,
                    cooldownRemaining = CooldownRemaining,
                };
            }
        }

        private sealed class EnemyDraft
        {
            public int Sword, Infection; public float InfectionElapsed, Resonance;
            public bool Controlled;
            public void Apply(FormalThreeDResearchEffectStateEntrySaveData item)
            {
                if (item.effectId == ResearchStatusCatalog.SwordIntentId)
                    Sword = item.stacks;
                else if (item.effectId == ResearchStatusCatalog.InfectionId)
                { Infection = item.stacks; InfectionElapsed = item.periodAccumulatorSeconds; }
                else if (item.effectId == ResearchStatusCatalog.PsionicResonanceId)
                    Resonance = item.remainingRuleSeconds;
                else if (item.effectId == ResearchStatusCatalog.MindControlId)
                    Controlled = true;
            }
            public SingleCityDefenseEnemyTechnologyPersistenceState ToState(
                SingleCityDefenseEnemySnapshot topology, EnemyDefinition definition) =>
                new SingleCityDefenseEnemyTechnologyPersistenceState(
                    topology.StableId, topology.EnemyDefinitionId,
                    definition.MaximumHealth, topology.X, topology.Z,
                    Sword, Infection, InfectionElapsed, Resonance, Controlled);
        }

        private sealed class BuildingDraft
        {
            public int Shield;
            public float Tissue, Carapace, Repair, ShieldPulse;
            public void Apply(FormalThreeDResearchEffectStateEntrySaveData item)
            {
                if (item.effectId == ResearchStatusCatalog.CityShieldId)
                {
                    Shield = (int)Math.Round(item.currentValue);
                    ShieldPulse = item.periodAccumulatorSeconds;
                }
                else if (item.effectId == ResearchStatusCatalog.TissueRegenerationId)
                    Tissue = item.periodAccumulatorSeconds;
                else if (item.effectId == ResearchStatusCatalog.CarapaceRegenerationId)
                    Carapace = item.periodAccumulatorSeconds;
                else if (item.effectId == ResearchStatusCatalog.AutomatedRepairId)
                    Repair = item.periodAccumulatorSeconds;
            }
        }
    }
}
