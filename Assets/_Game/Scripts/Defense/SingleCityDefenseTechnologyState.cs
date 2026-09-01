using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Research;

namespace WasteCity.Defense
{
    public readonly struct SingleCityDefenseTechnologyUnlocks
    {
        public SingleCityDefenseTechnologyUnlocks(
            bool energyOverload = false,
            bool swordIntent = false,
            bool infection = false,
            bool resonance = false,
            bool mindControl = false,
            bool acidSpit = false,
            bool talismanBasics = false,
            bool automatedRepair = false,
            bool mindShield = false,
            float acidHeavyDamageMultiplier = 1f,
            float wallPhysicalDamageMultiplier = 1f)
        {
            EnergyOverload = energyOverload;
            SwordIntent = swordIntent;
            Infection = infection;
            Resonance = resonance;
            MindControl = mindControl;
            AcidSpit = acidSpit;
            TalismanBasics = talismanBasics;
            AutomatedRepair = automatedRepair;
            MindShield = mindShield;
            AcidHeavyDamageMultiplier = Math.Max(
                1f, acidHeavyDamageMultiplier);
            WallPhysicalDamageMultiplier = Math.Max(
                0f, Math.Min(1f, wallPhysicalDamageMultiplier));
        }

        public bool EnergyOverload { get; }
        public bool SwordIntent { get; }
        public bool Infection { get; }
        public bool Resonance { get; }
        public bool MindControl { get; }
        public bool AcidSpit { get; }
        public bool TalismanBasics { get; }
        public bool AutomatedRepair { get; }
        public bool MindShield { get; }
        public float AcidHeavyDamageMultiplier { get; }
        public float WallPhysicalDamageMultiplier { get; }
    }

    public static class SingleCityDefenseTechnologyRules
    {
        public const float SupportRadius = 6f;
        public const float InfectionSpreadRadius = 3f;

        public static float AutomatedRepairPeriodSeconds =>
            RequireStatus(ResearchStatusCatalog.AutomatedRepairId)
                .PeriodSeconds;
        public static int AutomatedRepairAmount => (int)Math.Round(
            RequireStatus(ResearchStatusCatalog.AutomatedRepairId)
                .MaximumValue);
        public static float ShieldPeriodSeconds =>
            RequireStatus(ResearchStatusCatalog.CityShieldId).PeriodSeconds;
        public static int MaximumShield => (int)Math.Round(
            RequireStatus(ResearchStatusCatalog.CityShieldId).MaximumValue);
        public static int ShieldRechargeAmount
        {
            get
            {
                IReadOnlyList<ResearchEffectDefinition> effects =
                    ResearchEffectCatalog.ForResearch(
                        "core.research.mind-shield");
                for (var index = 0; index < effects.Count; index++)
                {
                    if (effects[index].Kind ==
                            ResearchEffectKind.Regeneration &&
                        string.Equals(
                            effects[index].TargetId,
                            "city.shield",
                            StringComparison.Ordinal))
                    {
                        return Math.Max(
                            0,
                            (int)Math.Round(effects[index].RuntimeValue));
                    }
                }
                return 0;
            }
        }

        private static ResearchStatusDefinition RequireStatus(string id)
        {
            return ResearchStatusCatalog.Find(id) ??
                throw new InvalidOperationException(
                    "Missing formal defense technology status: " + id);
        }
    }

    public sealed class SingleCityDefenseTechnologyDamageEvent
    {
        public SingleCityDefenseTechnologyDamageEvent(
            string targetStableEnemyId,
            int damage,
            bool trueDamage,
            bool synchronized)
        {
            TargetStableEnemyId = targetStableEnemyId ?? string.Empty;
            Damage = Math.Max(0, damage);
            IsTrueDamage = trueDamage;
            IsSynchronized = synchronized;
        }

        public string TargetStableEnemyId { get; }
        public int Damage { get; }
        public bool IsTrueDamage { get; }
        public bool IsSynchronized { get; }
    }

    public sealed class SingleCityDefenseTechnologyHitResult
    {
        private static readonly SingleCityDefenseTechnologyHitResult empty =
            new SingleCityDefenseTechnologyHitResult(
                0,
                false,
                Array.Empty<string>(),
                Array.Empty<SingleCityDefenseTechnologyDamageEvent>(),
                false);

        internal SingleCityDefenseTechnologyHitResult(
            int trueDamage,
            bool infectionBurst,
            string[] spreadTargetStableIds,
            SingleCityDefenseTechnologyDamageEvent[] synchronizedDamageEvents,
            bool controlled)
        {
            TrueDamage = Math.Max(0, trueDamage);
            InfectionBurst = infectionBurst;
            SpreadTargetStableIds = Array.AsReadOnly(
                spreadTargetStableIds ?? Array.Empty<string>());
            SynchronizedDamageEvents = Array.AsReadOnly(
                synchronizedDamageEvents ??
                Array.Empty<SingleCityDefenseTechnologyDamageEvent>());
            Controlled = controlled;
        }

        public static SingleCityDefenseTechnologyHitResult Empty => empty;
        public int TrueDamage { get; }
        public bool InfectionBurst { get; }
        public IReadOnlyList<string> SpreadTargetStableIds { get; }
        public IReadOnlyList<SingleCityDefenseTechnologyDamageEvent>
            SynchronizedDamageEvents { get; }
        public bool Controlled { get; }
    }

    public sealed class SingleCityDefenseOverloadSnapshot
    {
        internal SingleCityDefenseOverloadSnapshot(
            string towerStableId,
            TechnologyOverloadModel source)
        {
            TowerStableId = towerStableId ?? string.Empty;
            Phase = source?.Phase ?? TechnologyOverloadPhase.Ready;
            CooldownRemaining = source?.CooldownRemaining ?? 0f;
            BoostRemaining = source?.BoostRemaining ?? 0f;
            LockoutRemaining = source?.LockoutRemaining ?? 0f;
        }

        public string TowerStableId { get; }
        public TechnologyOverloadPhase Phase { get; }
        public float CooldownRemaining { get; }
        public float BoostRemaining { get; }
        public float LockoutRemaining { get; }
    }

    public sealed class SingleCityDefenseEnemyTechnologySnapshot
    {
        internal SingleCityDefenseEnemyTechnologySnapshot(
            EnemyTechnologyState source)
        {
            StableEnemyId = source.StableEnemyId;
            EnemyDefinitionId = source.EnemyDefinitionId;
            SwordIntentStacks = source.SwordIntent.Stacks;
            InfectionStacks = source.Infection.Stacks;
            InfectionElapsed = source.Infection.Elapsed;
            ResonanceRemaining = source.Resonance.Remaining;
            Controlled = source.Controlled;
        }

        public string StableEnemyId { get; }
        public string EnemyDefinitionId { get; }
        public int SwordIntentStacks { get; }
        public int InfectionStacks { get; }
        public float InfectionElapsed { get; }
        public float ResonanceRemaining { get; }
        public bool Controlled { get; }
    }

    public sealed class SingleCityDefenseTechnologyStateSnapshot
    {
        internal SingleCityDefenseTechnologyStateSnapshot(
            SingleCityDefenseOverloadSnapshot[] overloads,
            SingleCityDefenseEnemyTechnologySnapshot[] enemies)
        {
            Overloads = Array.AsReadOnly(
                overloads ?? Array.Empty<SingleCityDefenseOverloadSnapshot>());
            Enemies = Array.AsReadOnly(
                enemies ??
                Array.Empty<SingleCityDefenseEnemyTechnologySnapshot>());
        }

        public IReadOnlyList<SingleCityDefenseOverloadSnapshot> Overloads
        {
            get;
        }
        public IReadOnlyList<SingleCityDefenseEnemyTechnologySnapshot> Enemies
        {
            get;
        }
    }

    public readonly struct SingleCityDefenseOverloadPersistenceState
    {
        public SingleCityDefenseOverloadPersistenceState(
            string towerStableId,
            float cooldownRemaining,
            float boostRemaining,
            float lockoutRemaining)
        {
            TowerStableId = towerStableId;
            CooldownRemaining = cooldownRemaining;
            BoostRemaining = boostRemaining;
            LockoutRemaining = lockoutRemaining;
        }
        public string TowerStableId { get; }
        public float CooldownRemaining { get; }
        public float BoostRemaining { get; }
        public float LockoutRemaining { get; }
    }

    public readonly struct SingleCityDefenseEnemyTechnologyPersistenceState
    {
        public SingleCityDefenseEnemyTechnologyPersistenceState(
            string stableEnemyId,
            string enemyDefinitionId,
            int maximumHealth,
            float x,
            float z,
            int swordIntentStacks,
            int infectionStacks,
            float infectionElapsed,
            float resonanceRemaining,
            bool controlled)
        {
            StableEnemyId = stableEnemyId;
            EnemyDefinitionId = enemyDefinitionId;
            MaximumHealth = maximumHealth;
            X = x;
            Z = z;
            SwordIntentStacks = swordIntentStacks;
            InfectionStacks = infectionStacks;
            InfectionElapsed = infectionElapsed;
            ResonanceRemaining = resonanceRemaining;
            Controlled = controlled;
        }
        public string StableEnemyId { get; }
        public string EnemyDefinitionId { get; }
        public int MaximumHealth { get; }
        public float X { get; }
        public float Z { get; }
        public int SwordIntentStacks { get; }
        public int InfectionStacks { get; }
        public float InfectionElapsed { get; }
        public float ResonanceRemaining { get; }
        public bool Controlled { get; }
    }

    public readonly struct SingleCityDefenseTechnologyEmitterPersistenceState
    {
        public SingleCityDefenseTechnologyEmitterPersistenceState(
            string towerStableId,
            string targetStableEnemyId,
            float cooldownRemaining)
        {
            TowerStableId = towerStableId;
            TargetStableEnemyId = targetStableEnemyId;
            CooldownRemaining = cooldownRemaining;
        }

        public string TowerStableId { get; }
        public string TargetStableEnemyId { get; }
        public float CooldownRemaining { get; }
    }

    public sealed class SingleCityDefenseTechnologyPersistenceSnapshot
    {
        public SingleCityDefenseTechnologyPersistenceSnapshot(
            SingleCityDefenseOverloadPersistenceState[] overloads,
            SingleCityDefenseEnemyTechnologyPersistenceState[] enemies)
            : this(
                overloads,
                enemies,
                Array.Empty<
                    SingleCityDefenseTechnologyEmitterPersistenceState>(),
                Array.Empty<
                    SingleCityDefenseTechnologyEmitterPersistenceState>())
        {
        }

        public SingleCityDefenseTechnologyPersistenceSnapshot(
            SingleCityDefenseOverloadPersistenceState[] overloads,
            SingleCityDefenseEnemyTechnologyPersistenceState[] enemies,
            SingleCityDefenseTechnologyEmitterPersistenceState[]
                swordIntentEmitters,
            SingleCityDefenseTechnologyEmitterPersistenceState[]
                infectionEmitters)
        {
            Overloads = overloads == null ? null :
                (SingleCityDefenseOverloadPersistenceState[])overloads.Clone();
            Enemies = enemies == null ? null :
                (SingleCityDefenseEnemyTechnologyPersistenceState[])enemies.Clone();
            SwordIntentEmitters = swordIntentEmitters == null ? null :
                (SingleCityDefenseTechnologyEmitterPersistenceState[])
                swordIntentEmitters.Clone();
            InfectionEmitters = infectionEmitters == null ? null :
                (SingleCityDefenseTechnologyEmitterPersistenceState[])
                infectionEmitters.Clone();
        }
        public SingleCityDefenseOverloadPersistenceState[] Overloads { get; }
        public SingleCityDefenseEnemyTechnologyPersistenceState[] Enemies { get; }
        public SingleCityDefenseTechnologyEmitterPersistenceState[]
            SwordIntentEmitters { get; }
        public SingleCityDefenseTechnologyEmitterPersistenceState[]
            InfectionEmitters { get; }
    }

    internal sealed class TechnologyHitEmitterCooldown
    {
        public TechnologyHitEmitterCooldown(float cooldownRemaining)
        {
            CooldownRemaining = Math.Max(0f, cooldownRemaining);
        }

        public float CooldownRemaining { get; private set; }

        public void Advance(float deltaSeconds)
        {
            CooldownRemaining = Math.Max(
                0f,
                CooldownRemaining - Math.Max(0f, deltaSeconds));
        }

        public void Reset(float intervalSeconds)
        {
            CooldownRemaining = Math.Max(0f, intervalSeconds);
        }
    }

    internal sealed class EnemyTechnologyState
    {
        public EnemyTechnologyState(
            string stableEnemyId,
            string enemyDefinitionId,
            int maximumHealth,
            float x,
            float z)
        {
            StableEnemyId = stableEnemyId;
            EnemyDefinitionId = enemyDefinitionId;
            MaximumHealth = Math.Max(1, maximumHealth);
            X = x;
            Z = z;
        }

        public string StableEnemyId { get; }
        public string EnemyDefinitionId { get; private set; }
        public int MaximumHealth { get; private set; }
        public float X { get; private set; }
        public float Z { get; private set; }
        public bool Controlled { get; set; }
        public SwordIntentModel SwordIntent { get; } = new SwordIntentModel();
        public InfectionModel Infection { get; } = new InfectionModel();
        public PsionicResonanceModel Resonance { get; } =
            new PsionicResonanceModel();

        public void Observe(
            string enemyDefinitionId,
            int maximumHealth,
            float x,
            float z)
        {
            EnemyDefinitionId = enemyDefinitionId;
            MaximumHealth = Math.Max(1, maximumHealth);
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// Cross-frame defense technology state shared by the main and pressure
    /// campaigns. It owns only timers/stacks and emits immutable commands;
    /// campaign health, statistics and persistence remain with their existing
    /// authoritative owners.
    /// </summary>
    public sealed class SingleCityDefenseTechnologyRuntime
    {
        private readonly SortedDictionary<string, TechnologyOverloadModel>
            overloadByTowerId =
                new SortedDictionary<string, TechnologyOverloadModel>(
                    StringComparer.Ordinal);
        private readonly SortedDictionary<string, EnemyTechnologyState>
            enemyById =
                new SortedDictionary<string, EnemyTechnologyState>(
                    StringComparer.Ordinal);
        private readonly SortedDictionary<string, TechnologyHitEmitterCooldown>
            swordEmitterByPair =
                new SortedDictionary<string, TechnologyHitEmitterCooldown>(
                    StringComparer.Ordinal);
        private readonly SortedDictionary<string, TechnologyHitEmitterCooldown>
            infectionEmitterByPair =
                new SortedDictionary<string, TechnologyHitEmitterCooldown>(
                    StringComparer.Ordinal);
        private readonly HashSet<string> observedEnemyIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removedIds = new List<string>();
        private SingleCityDefenseTechnologyUnlocks unlocks;

        public SingleCityDefenseTechnologyUnlocks Unlocks => unlocks;

        public SingleCityDefenseTechnologyStateSnapshot Snapshot
        {
            get
            {
                var overloads = new SingleCityDefenseOverloadSnapshot[
                    overloadByTowerId.Count];
                var overloadIndex = 0;
                foreach (KeyValuePair<string, TechnologyOverloadModel> item in
                         overloadByTowerId)
                {
                    overloads[overloadIndex++] =
                        new SingleCityDefenseOverloadSnapshot(
                            item.Key, item.Value);
                }

                var enemies = new SingleCityDefenseEnemyTechnologySnapshot[
                    enemyById.Count];
                var enemyIndex = 0;
                foreach (KeyValuePair<string, EnemyTechnologyState> item in
                         enemyById)
                {
                    enemies[enemyIndex++] =
                        new SingleCityDefenseEnemyTechnologySnapshot(
                            item.Value);
                }
                return new SingleCityDefenseTechnologyStateSnapshot(
                    overloads, enemies);
            }
        }

        public bool HasState => overloadByTowerId.Count > 0 ||
            enemyById.Count > 0 || swordEmitterByPair.Count > 0 ||
            infectionEmitterByPair.Count > 0;

        public bool ClearForDevelopment()
        {
            if (!HasState) return false;
            overloadByTowerId.Clear();
            enemyById.Clear();
            swordEmitterByPair.Clear();
            infectionEmitterByPair.Clear();
            observedEnemyIds.Clear();
            removedIds.Clear();
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool TrySetEnemyStatusForDevelopment(
            string stableEnemyId,
            string statusId,
            bool fillStacks)
        {
            if (string.IsNullOrWhiteSpace(stableEnemyId) ||
                !enemyById.TryGetValue(
                    stableEnemyId,
                    out EnemyTechnologyState state))
            {
                return false;
            }
            switch (statusId)
            {
                case ResearchStatusCatalog.SwordIntentId:
                    state.SwordIntent.Restore(fillStacks
                        ? SwordIntentModel.MaximumStacks - 1
                        : 1);
                    return true;
                case ResearchStatusCatalog.InfectionId:
                    state.Infection.Restore(fillStacks
                        ? InfectionModel.BurstThreshold - 1
                        : 1, 0f);
                    return true;
                case ResearchStatusCatalog.PsionicResonanceId:
                    state.Resonance.Apply();
                    return true;
                default:
                    return false;
            }
        }

        public bool TryClearEnemyStatusForDevelopment(
            string stableEnemyId,
            string statusId)
        {
            if (string.IsNullOrWhiteSpace(stableEnemyId) ||
                !enemyById.TryGetValue(
                    stableEnemyId,
                    out EnemyTechnologyState state))
            {
                return false;
            }
            switch (statusId)
            {
                case ResearchStatusCatalog.SwordIntentId:
                    if (state.SwordIntent.Stacks <= 0) return false;
                    state.SwordIntent.Clear();
                    return true;
                case ResearchStatusCatalog.InfectionId:
                    if (state.Infection.Stacks <= 0) return false;
                    state.Infection.Clear();
                    return true;
                case ResearchStatusCatalog.PsionicResonanceId:
                    if (!state.Resonance.Active) return false;
                    state.Resonance.Clear();
                    return true;
                default:
                    return false;
            }
        }

        public bool TryExpireEnemyStatusForDevelopment(
            string stableEnemyId,
            string statusId)
        {
            if (!string.Equals(
                    statusId,
                    ResearchStatusCatalog.PsionicResonanceId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(stableEnemyId) ||
                !enemyById.TryGetValue(
                    stableEnemyId,
                    out EnemyTechnologyState state) ||
                !state.Resonance.Active)
            {
                return false;
            }
            state.Resonance.Clear();
            return true;
        }

        public bool TryExpireOverloadForDevelopment(string towerStableId)
        {
            if (string.IsNullOrWhiteSpace(towerStableId) ||
                !overloadByTowerId.TryGetValue(
                    towerStableId,
                    out TechnologyOverloadModel model) ||
                model.Phase == TechnologyOverloadPhase.Ready)
            {
                return false;
            }
            model.Tick(
                TechnologyOverloadModel.CooldownSeconds +
                TechnologyOverloadModel.BoostSeconds +
                TechnologyOverloadModel.LockoutSeconds);
            return true;
        }

        public bool TryClearOverloadForDevelopment(string towerStableId)
        {
            return !string.IsNullOrWhiteSpace(towerStableId) &&
                overloadByTowerId.Remove(towerStableId);
        }
#endif

        public bool TryRestore(
            SingleCityDefenseTechnologyPersistenceSnapshot snapshot,
            out string error)
        {
            if (snapshot == null || snapshot.Overloads == null ||
                snapshot.Enemies == null ||
                snapshot.SwordIntentEmitters == null ||
                snapshot.InfectionEmitters == null)
            {
                error = "防御科技状态不完整";
                return false;
            }
            var overloads = new SortedDictionary<string,
                TechnologyOverloadModel>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Overloads.Length; index++)
            {
                SingleCityDefenseOverloadPersistenceState saved =
                    snapshot.Overloads[index];
                if (string.IsNullOrWhiteSpace(saved.TowerStableId) ||
                    overloads.ContainsKey(saved.TowerStableId) ||
                    !IsFinite(saved.CooldownRemaining) ||
                    !IsFinite(saved.BoostRemaining) ||
                    !IsFinite(saved.LockoutRemaining) ||
                    saved.CooldownRemaining < 0f ||
                    saved.CooldownRemaining > TechnologyOverloadModel.CooldownSeconds ||
                    saved.BoostRemaining < 0f ||
                    saved.BoostRemaining > TechnologyOverloadModel.BoostSeconds ||
                    saved.LockoutRemaining < 0f ||
                    saved.LockoutRemaining > TechnologyOverloadModel.LockoutSeconds)
                {
                    error = "过载科技状态无效";
                    return false;
                }
                var model = new TechnologyOverloadModel();
                model.Restore(
                    unlocks.EnergyOverload,
                    saved.CooldownRemaining,
                    saved.BoostRemaining,
                    saved.LockoutRemaining);
                overloads.Add(saved.TowerStableId, model);
            }
            var enemies = new SortedDictionary<string, EnemyTechnologyState>(
                StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Enemies.Length; index++)
            {
                SingleCityDefenseEnemyTechnologyPersistenceState saved =
                    snapshot.Enemies[index];
                if (string.IsNullOrWhiteSpace(saved.StableEnemyId) ||
                    string.IsNullOrWhiteSpace(saved.EnemyDefinitionId) ||
                    enemies.ContainsKey(saved.StableEnemyId) ||
                    saved.MaximumHealth <= 0 || !IsFinite(saved.X) ||
                    !IsFinite(saved.Z) || saved.SwordIntentStacks < 0 ||
                    saved.SwordIntentStacks >= SwordIntentModel.MaximumStacks ||
                    saved.InfectionStacks < 0 ||
                    saved.InfectionStacks >= InfectionModel.BurstThreshold ||
                    !IsFinite(saved.InfectionElapsed) ||
                    saved.InfectionElapsed < 0f ||
                    saved.InfectionElapsed >= InfectionModel.TickSeconds ||
                    !IsFinite(saved.ResonanceRemaining) ||
                    saved.ResonanceRemaining < 0f ||
                    saved.ResonanceRemaining > PsionicResonanceModel.DurationSeconds)
                {
                    error = "敌人科技状态无效";
                    return false;
                }
                var state = new EnemyTechnologyState(
                    saved.StableEnemyId,
                    saved.EnemyDefinitionId,
                    saved.MaximumHealth,
                    saved.X,
                    saved.Z)
                {
                    Controlled = saved.Controlled,
                };
                state.SwordIntent.Restore(saved.SwordIntentStacks);
                state.Infection.Restore(
                    saved.InfectionStacks, saved.InfectionElapsed);
                state.Resonance.Restore(saved.ResonanceRemaining);
                enemies.Add(saved.StableEnemyId, state);
            }
            if (!TryRestoreEmitters(
                    snapshot.SwordIntentEmitters,
                    enemies,
                    SwordIntentEmitterModel.SecondsPerStack,
                    out SortedDictionary<string,
                        TechnologyHitEmitterCooldown> swordEmitters) ||
                !TryRestoreEmitters(
                    snapshot.InfectionEmitters,
                    enemies,
                    InfectionEmitterModel.IntervalSeconds,
                    out SortedDictionary<string,
                        TechnologyHitEmitterCooldown> infectionEmitters))
            {
                error = "防御科技命中周期状态无效";
                return false;
            }
            overloadByTowerId.Clear();
            foreach (var item in overloads) overloadByTowerId.Add(item.Key, item.Value);
            enemyById.Clear();
            foreach (var item in enemies) enemyById.Add(item.Key, item.Value);
            swordEmitterByPair.Clear();
            foreach (var item in swordEmitters)
                swordEmitterByPair.Add(item.Key, item.Value);
            infectionEmitterByPair.Clear();
            foreach (var item in infectionEmitters)
                infectionEmitterByPair.Add(item.Key, item.Value);
            error = string.Empty;
            return true;
        }

        public SingleCityDefenseTechnologyPersistenceSnapshot
            CaptureForPersistence()
        {
            SingleCityDefenseTechnologyStateSnapshot source = Snapshot;
            var overloads = new SingleCityDefenseOverloadPersistenceState[
                source.Overloads.Count];
            for (var index = 0; index < overloads.Length; index++)
            {
                SingleCityDefenseOverloadSnapshot item = source.Overloads[index];
                overloads[index] = new SingleCityDefenseOverloadPersistenceState(
                    item.TowerStableId, item.CooldownRemaining,
                    item.BoostRemaining, item.LockoutRemaining);
            }
            var enemies = new SingleCityDefenseEnemyTechnologyPersistenceState[
                source.Enemies.Count];
            for (var index = 0; index < enemies.Length; index++)
            {
                SingleCityDefenseEnemyTechnologySnapshot item = source.Enemies[index];
                EnemyTechnologyState state = enemyById[item.StableEnemyId];
                enemies[index] = new SingleCityDefenseEnemyTechnologyPersistenceState(
                    item.StableEnemyId, item.EnemyDefinitionId,
                    state.MaximumHealth, state.X, state.Z,
                    item.SwordIntentStacks, item.InfectionStacks,
                    item.InfectionElapsed, item.ResonanceRemaining,
                    item.Controlled);
            }
            return new SingleCityDefenseTechnologyPersistenceSnapshot(
                overloads,
                enemies,
                CaptureEmitters(swordEmitterByPair),
                CaptureEmitters(infectionEmitterByPair));
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        public void Configure(SingleCityDefenseTechnologyUnlocks value)
        {
            unlocks = value;
            if (value.EnergyOverload) return;
            overloadByTowerId.Clear();
        }

        public void SynchronizeEnemies(
            IReadOnlyList<SingleCityDefenseEnemySnapshot> enemies)
        {
            observedEnemyIds.Clear();
            if (enemies != null)
            {
                for (var index = 0; index < enemies.Count; index++)
                {
                    SingleCityDefenseEnemySnapshot enemy = enemies[index];
                    if (enemy == null || enemy.CurrentHealth <= 0 ||
                        string.IsNullOrWhiteSpace(enemy.StableId))
                    {
                        continue;
                    }
                    EnemyDefinition definition = FindEnemyDefinition(
                        enemy.EnemyDefinitionId);
                    if (definition == null) continue;
                    observedEnemyIds.Add(enemy.StableId);
                    if (!enemyById.TryGetValue(
                            enemy.StableId,
                            out EnemyTechnologyState state))
                    {
                        state = new EnemyTechnologyState(
                            enemy.StableId,
                            definition.Id.Value,
                            definition.MaximumHealth,
                            enemy.X,
                            enemy.Z);
                        enemyById.Add(enemy.StableId, state);
                    }
                    else
                    {
                        state.Observe(
                            definition.Id.Value,
                            definition.MaximumHealth,
                            enemy.X,
                            enemy.Z);
                    }
                }
            }

            removedIds.Clear();
            foreach (KeyValuePair<string, EnemyTechnologyState> item in
                     enemyById)
            {
                if (!observedEnemyIds.Contains(item.Key))
                {
                    removedIds.Add(item.Key);
                }
            }
            for (var index = 0; index < removedIds.Count; index++)
            {
                string enemyId = removedIds[index];
                enemyById.Remove(enemyId);
                RemoveEmittersFor(enemyId);
            }
        }

        public bool TryActivateOverload(
            string towerStableId,
            string towerBuildingId)
        {
            if (!unlocks.EnergyOverload ||
                string.IsNullOrWhiteSpace(towerStableId) ||
                !string.Equals(
                    towerBuildingId,
                    BuildingCatalog.LaserTower.Id.Value,
                    StringComparison.Ordinal))
            {
                return false;
            }
            if (!overloadByTowerId.TryGetValue(
                    towerStableId,
                    out TechnologyOverloadModel model))
            {
                model = new TechnologyOverloadModel();
                overloadByTowerId.Add(towerStableId, model);
            }
            return model.TryActivate(unlocked: true);
        }

        public float ResolveTowerFireRateMultiplier(string towerStableId)
        {
            return !string.IsNullOrWhiteSpace(towerStableId) &&
                overloadByTowerId.TryGetValue(
                    towerStableId,
                    out TechnologyOverloadModel model)
                    ? model.FireRateMultiplier
                    : 1f;
        }

        public float ResolveTowerDamageMultiplier(
            string towerStableId,
            string towerBuildingId,
            string targetStableEnemyId)
        {
            var multiplier = 1f;
            if (!string.IsNullOrWhiteSpace(towerStableId) &&
                overloadByTowerId.TryGetValue(
                    towerStableId,
                    out TechnologyOverloadModel overload))
            {
                DefenseTowerDefinition tower = DefenseTowerCatalog.For(
                    towerBuildingId);
                if (tower != null)
                {
                    multiplier *= overload.DamageMultiplier(
                        tower.DamageType);
                }
            }
            if (unlocks.AcidSpit &&
                string.Equals(
                    towerBuildingId,
                    BuildingCatalog.AcidTower.Id.Value,
                    StringComparison.Ordinal) &&
                enemyById.TryGetValue(
                    targetStableEnemyId ?? string.Empty,
                    out EnemyTechnologyState target))
            {
                EnemyDefinition definition = FindEnemyDefinition(
                    target.EnemyDefinitionId);
                if (definition?.Armor == ArmorType.Heavy)
                    multiplier *= unlocks.AcidHeavyDamageMultiplier;
            }
            return multiplier;
        }

        public IReadOnlyList<SingleCityDefenseTechnologyDamageEvent> Advance(
            float deltaSeconds,
            bool paused)
        {
            if (paused || deltaSeconds <= 0f)
                return Array.Empty<SingleCityDefenseTechnologyDamageEvent>();

            foreach (KeyValuePair<string, TechnologyOverloadModel> item in
                     overloadByTowerId)
            {
                item.Value.Tick(deltaSeconds);
            }
            AdvanceEmitters(swordEmitterByPair, deltaSeconds);
            AdvanceEmitters(infectionEmitterByPair, deltaSeconds);

            var damage = new List<SingleCityDefenseTechnologyDamageEvent>();
            foreach (KeyValuePair<string, EnemyTechnologyState> item in
                     enemyById)
            {
                EnemyTechnologyState enemy = item.Value;
                if (enemy.Controlled) continue;
                enemy.Resonance.Tick(deltaSeconds);
                int amount = unlocks.Infection
                    ? enemy.Infection.Tick(
                        deltaSeconds,
                        enemy.MaximumHealth)
                    : 0;
                if (amount > 0)
                {
                    damage.Add(new SingleCityDefenseTechnologyDamageEvent(
                        enemy.StableEnemyId,
                        amount,
                        trueDamage: true,
                        synchronized: false));
                }
            }
            return damage.Count == 0
                ? Array.Empty<SingleCityDefenseTechnologyDamageEvent>()
                : Array.AsReadOnly(damage.ToArray());
        }

        public SingleCityDefenseTechnologyHitResult ApplyTowerHit(
            string towerStableId,
            string towerBuildingId,
            string targetStableEnemyId,
            int primaryAppliedDamage,
            float elapsedSincePreviousHit,
            ulong stableHitSequence)
        {
            // Kept for source compatibility. Only Advance owns rule time.
            _ = elapsedSincePreviousHit;
            if (primaryAppliedDamage <= 0 ||
                string.IsNullOrWhiteSpace(towerStableId) ||
                string.IsNullOrWhiteSpace(targetStableEnemyId) ||
                !enemyById.TryGetValue(
                    targetStableEnemyId,
                    out EnemyTechnologyState target) ||
                target.Controlled)
            {
                return SingleCityDefenseTechnologyHitResult.Empty;
            }

            var trueDamage = 0;
            var infectionBurst = false;
            string[] spread = Array.Empty<string>();
            var synchronized = new List<
                SingleCityDefenseTechnologyDamageEvent>();

            if (unlocks.SwordIntent && IsSwordTower(towerBuildingId))
            {
                string key = PairKey(towerStableId, targetStableEnemyId);
                if (TryEmit(
                        swordEmitterByPair,
                        key,
                        SwordIntentEmitterModel.SecondsPerStack))
                {
                    SwordIntentHitResult result =
                        target.SwordIntent.AddHit(target.MaximumHealth);
                    trueDamage = result.TrueDamage;
                }
            }

            if (unlocks.Infection && IsSporeTower(towerBuildingId))
            {
                string key = PairKey(towerStableId, targetStableEnemyId);
                if (TryEmit(
                        infectionEmitterByPair,
                        key,
                        InfectionEmitterModel.IntervalSeconds))
                {
                    infectionBurst = target.Infection.AddStacks(1);
                    if (infectionBurst)
                        spread = SpreadInfection(target);
                }
            }

            if (unlocks.Resonance && IsMindSpire(towerBuildingId))
            {
                int activeCount = ActiveResonanceCount();
                if (PsionicResonanceRules.CanMark(
                        target.Resonance.Active,
                        activeCount))
                {
                    target.Resonance.Apply();
                }
                int synchronizedDamage =
                    PsionicResonanceRules.SynchronizedRawDamage(
                        primaryAppliedDamage);
                if (synchronizedDamage > 0)
                {
                    foreach (KeyValuePair<string, EnemyTechnologyState> item
                             in enemyById)
                    {
                        EnemyTechnologyState other = item.Value;
                        if (other.Controlled || !other.Resonance.Active ||
                            string.Equals(
                                other.StableEnemyId,
                                targetStableEnemyId,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }
                        synchronized.Add(
                            new SingleCityDefenseTechnologyDamageEvent(
                                other.StableEnemyId,
                                synchronizedDamage,
                                trueDamage: false,
                                synchronized: true));
                    }
                }
            }

            bool controlled = false;
            if (unlocks.MindControl && IsMindSpire(towerBuildingId))
            {
                EnemyDefinition definition = FindEnemyDefinition(
                    target.EnemyDefinitionId);
                int roll = StablePercentRoll(
                    towerStableId,
                    targetStableEnemyId,
                    stableHitSequence);
                controlled = definition != null &&
                    MindControlModel.ShouldConvert(
                        researchCompleted: true,
                        EnemyQuality.Ordinary,
                        definition.IsHeavy,
                        roll);
                if (controlled)
                {
                    // The deterministic roll only proposes conversion. The
                    // campaign remains authoritative and commits through
                    // TryCommitMindControl after it confirms the live target.
                }
            }

            return new SingleCityDefenseTechnologyHitResult(
                trueDamage,
                infectionBurst,
                spread,
                synchronized.ToArray(),
                controlled);
        }

        public bool TryCommitMindControl(string targetStableEnemyId)
        {
            if (string.IsNullOrWhiteSpace(targetStableEnemyId) ||
                !enemyById.TryGetValue(
                    targetStableEnemyId,
                    out EnemyTechnologyState target) ||
                target.Controlled)
            {
                return false;
            }
            target.Controlled = true;
            target.Infection.Clear();
            target.SwordIntent.Clear();
            target.Resonance.Clear();
            RemoveEmittersFor(targetStableEnemyId);
            return true;
        }

        private string[] SpreadInfection(EnemyTechnologyState source)
        {
            var candidates = new InfectionSpreadCandidate[enemyById.Count];
            var stableIds = new string[enemyById.Count];
            var index = 0;
            foreach (KeyValuePair<string, EnemyTechnologyState> item in
                     enemyById)
            {
                EnemyTechnologyState candidate = item.Value;
                candidates[index] = new InfectionSpreadCandidate(
                    index,
                    candidate.X,
                    candidate.Z,
                    isAlive: !candidate.Controlled,
                    isFriendly: candidate.Controlled);
                stableIds[index] = candidate.StableEnemyId;
                index++;
            }

            var affected = new SortedSet<string>(StringComparer.Ordinal);
            var burst = new HashSet<string>(StringComparer.Ordinal)
            {
                source.StableEnemyId,
            };
            var pending = new Queue<EnemyTechnologyState>();
            pending.Enqueue(source);
            while (pending.Count > 0)
            {
                EnemyTechnologyState current = pending.Dequeue();
                int sourceIndex = Array.IndexOf(
                    stableIds,
                    current.StableEnemyId);
                int[] selected = InfectionSpreadRules.SelectTargets(
                    sourceIndex,
                    current.X,
                    current.Z,
                    radius: SingleCityDefenseTechnologyRules
                        .InfectionSpreadRadius,
                    candidates);
                for (index = 0; index < selected.Length; index++)
                {
                    string stableId = stableIds[selected[index]];
                    affected.Add(stableId);
                    EnemyTechnologyState target = enemyById[stableId];
                    if (target.Infection.AddStacks(5) &&
                        burst.Add(stableId))
                    {
                        pending.Enqueue(target);
                    }
                }
            }

            var result = new string[affected.Count];
            affected.CopyTo(result);
            return result;
        }

        private static bool TryEmit(
            IDictionary<string, TechnologyHitEmitterCooldown> emitters,
            string pairKey,
            float intervalSeconds)
        {
            if (emitters.TryGetValue(
                    pairKey,
                    out TechnologyHitEmitterCooldown emitter))
            {
                if (emitter.CooldownRemaining > .00001f) return false;
                emitter.Reset(intervalSeconds);
                return true;
            }
            emitters.Add(
                pairKey,
                new TechnologyHitEmitterCooldown(intervalSeconds));
            return true;
        }

        private void AdvanceEmitters(
            IDictionary<string, TechnologyHitEmitterCooldown> emitters,
            float deltaSeconds)
        {
            removedIds.Clear();
            foreach (KeyValuePair<string, TechnologyHitEmitterCooldown> item
                     in emitters)
            {
                item.Value.Advance(deltaSeconds);
                if (item.Value.CooldownRemaining <= .00001f)
                    removedIds.Add(item.Key);
            }
            for (var index = 0; index < removedIds.Count; index++)
                emitters.Remove(removedIds[index]);
        }

        private static SingleCityDefenseTechnologyEmitterPersistenceState[]
            CaptureEmitters(
                IReadOnlyDictionary<string, TechnologyHitEmitterCooldown>
                    emitters)
        {
            var result =
                new SingleCityDefenseTechnologyEmitterPersistenceState[
                    emitters.Count];
            var index = 0;
            foreach (KeyValuePair<string, TechnologyHitEmitterCooldown> item
                     in emitters)
            {
                if (!TryParsePairKey(
                        item.Key,
                        out string towerStableId,
                        out string targetStableEnemyId))
                {
                    continue;
                }
                result[index++] =
                    new SingleCityDefenseTechnologyEmitterPersistenceState(
                        towerStableId,
                        targetStableEnemyId,
                        item.Value.CooldownRemaining);
            }
            if (index == result.Length) return result;
            Array.Resize(ref result, index);
            return result;
        }

        private static bool TryRestoreEmitters(
            SingleCityDefenseTechnologyEmitterPersistenceState[] saved,
            IReadOnlyDictionary<string, EnemyTechnologyState> enemies,
            float maximumCooldown,
            out SortedDictionary<string, TechnologyHitEmitterCooldown>
                restored)
        {
            restored = new SortedDictionary<string,
                TechnologyHitEmitterCooldown>(StringComparer.Ordinal);
            if (saved == null) return false;
            for (var index = 0; index < saved.Length; index++)
            {
                SingleCityDefenseTechnologyEmitterPersistenceState item =
                    saved[index];
                if (string.IsNullOrWhiteSpace(item.TowerStableId) ||
                    string.IsNullOrWhiteSpace(item.TargetStableEnemyId) ||
                    !enemies.TryGetValue(
                        item.TargetStableEnemyId,
                        out EnemyTechnologyState target) ||
                    target.Controlled ||
                    !IsFinite(item.CooldownRemaining) ||
                    item.CooldownRemaining <= 0f ||
                    item.CooldownRemaining > maximumCooldown)
                {
                    return false;
                }
                string key = PairKey(
                    item.TowerStableId,
                    item.TargetStableEnemyId);
                if (restored.ContainsKey(key)) return false;
                restored.Add(
                    key,
                    new TechnologyHitEmitterCooldown(
                        item.CooldownRemaining));
            }
            return true;
        }

        private static bool TryParsePairKey(
            string pairKey,
            out string towerStableId,
            out string targetStableEnemyId)
        {
            int separator = pairKey?.IndexOf('\n') ?? -1;
            if (separator <= 0 || separator >= pairKey.Length - 1)
            {
                towerStableId = string.Empty;
                targetStableEnemyId = string.Empty;
                return false;
            }
            towerStableId = pairKey.Substring(0, separator);
            targetStableEnemyId = pairKey.Substring(separator + 1);
            return true;
        }

        private int ActiveResonanceCount()
        {
            var count = 0;
            foreach (KeyValuePair<string, EnemyTechnologyState> item in
                     enemyById)
            {
                if (!item.Value.Controlled && item.Value.Resonance.Active)
                    count++;
            }
            return count;
        }

        private void RemoveEmittersFor(string enemyId)
        {
            RemovePairs(swordEmitterByPair, enemyId);
            RemovePairs(infectionEmitterByPair, enemyId);
        }

        private static void RemovePairs<T>(
            IDictionary<string, T> values,
            string enemyId)
        {
            var remove = new List<string>();
            string suffix = "\n" + enemyId;
            foreach (KeyValuePair<string, T> item in values)
            {
                if (item.Key.EndsWith(suffix, StringComparison.Ordinal))
                    remove.Add(item.Key);
            }
            for (var index = 0; index < remove.Count; index++)
                values.Remove(remove[index]);
        }

        private static string PairKey(string source, string target)
        {
            return source + "\n" + target;
        }

        private static bool IsSwordTower(string buildingId)
        {
            return string.Equals(
                       buildingId,
                       BuildingCatalog.SwordArrayTower.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       buildingId,
                       BuildingCatalog.SwordRidingPlatform.Id.Value,
                       StringComparison.Ordinal);
        }

        private static bool IsSporeTower(string buildingId)
        {
            return string.Equals(
                buildingId,
                BuildingCatalog.SporeTower.Id.Value,
                StringComparison.Ordinal);
        }

        private static bool IsMindSpire(string buildingId)
        {
            return string.Equals(
                buildingId,
                BuildingCatalog.MindSpire.Id.Value,
                StringComparison.Ordinal);
        }

        private static int StablePercentRoll(
            string source,
            string target,
            ulong sequence)
        {
            ulong hash = 1469598103934665603ul;
            Mix(ref hash, source);
            Mix(ref hash, target);
            unchecked
            {
                hash ^= sequence;
                hash *= 1099511628211ul;
            }
            return (int)(hash % 100ul);
        }

        private static void Mix(ref ulong hash, string text)
        {
            if (text == null) return;
            unchecked
            {
                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 1099511628211ul;
                }
            }
        }

        private static EnemyDefinition FindEnemyDefinition(string stableId)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                if (string.Equals(
                        EnemyCatalog.All[index].Id.Value,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return EnemyCatalog.All[index];
                }
            }
            return null;
        }
    }
}
