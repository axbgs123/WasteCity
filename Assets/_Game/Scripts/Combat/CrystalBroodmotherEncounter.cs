using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Combat
{
    public enum CrystalBroodmotherCommandKind
    {
        SpawnReinforcements,
        Defeated,
    }

    public sealed class CrystalBroodmotherCommand
    {
        private readonly ReadOnlyCollection<
            CrystalBroodmotherReinforcementDefinition> reinforcements;

        internal CrystalBroodmotherCommand(
            CrystalBroodmotherCommandKind kind,
            string stableCommandId,
            IReadOnlyList<CrystalBroodmotherReinforcementDefinition>
                reinforcements)
        {
            Kind = kind;
            StableCommandId = stableCommandId;
            var copy = new CrystalBroodmotherReinforcementDefinition[
                reinforcements?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = reinforcements[index];
            this.reinforcements = Array.AsReadOnly(copy);
        }

        public CrystalBroodmotherCommandKind Kind { get; }
        public string StableCommandId { get; }
        public IReadOnlyList<CrystalBroodmotherReinforcementDefinition>
            Reinforcements => reinforcements;
    }

    public sealed class CrystalBroodmotherSnapshot
    {
        public CrystalBroodmotherSnapshot(
            string stableInstanceId,
            bool phase70Triggered,
            bool phase35Triggered,
            bool defeated,
            ulong revision)
        {
            StableInstanceId = stableInstanceId;
            Phase70Triggered = phase70Triggered;
            Phase35Triggered = phase35Triggered;
            Defeated = defeated;
            Revision = revision;
        }

        public string StableInstanceId { get; }
        public bool Phase70Triggered { get; }
        public bool Phase35Triggered { get; }
        public bool Defeated { get; }
        public ulong Revision { get; }
    }

    public sealed class CrystalBroodmotherEncounter
    {
        private static readonly IReadOnlyList<CrystalBroodmotherCommand>
            EmptyCommands = Array.AsReadOnly(
                Array.Empty<CrystalBroodmotherCommand>());

        private readonly string stableInstanceId;
        private int fixtureHealth = CrystalBroodmotherCatalog.MaximumHealth;
        private double fixedStepAccumulatorSeconds;
        private int lastObservedAuthorityHealth;
        private bool hasObservedAuthorityHealth;
        private bool phase70Triggered;
        private bool phase35Triggered;
        private bool defeated;
        private ulong revision;
        private CrystalBroodmotherSnapshot cachedSnapshot;

        public CrystalBroodmotherEncounter(string stableInstanceId)
        {
            this.stableInstanceId = string.IsNullOrWhiteSpace(stableInstanceId)
                ? throw new ArgumentException(
                    "晶壳母体稳定实例 ID 不能为空",
                    nameof(stableInstanceId))
                : stableInstanceId;
            RebuildSnapshot();
        }

        // Pure deterministic fixture retained for isolated rule tests. Formal
        // campaign adapters must call ObserveAuthorityHealth and must never
        // use this helper as a second source of boss health truth.
        public IReadOnlyList<CrystalBroodmotherCommand> Tick(
            float deltaSeconds,
            bool paused,
            int damagePerFixedStep)
        {
            if (paused || defeated || deltaSeconds <= 0f ||
                float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                return EmptyCommands;
            }

            double beforeAccumulator = fixedStepAccumulatorSeconds;
            fixedStepAccumulatorSeconds += deltaSeconds;
            int steps = (int)Math.Floor(
                (fixedStepAccumulatorSeconds + .0000001d) /
                CrystalBroodmotherCatalog.FixedStepSeconds);
            if (steps <= 0)
            {
                if (fixedStepAccumulatorSeconds != beforeAccumulator)
                {
                    unchecked { revision++; }
                    RebuildSnapshot();
                }
                return EmptyCommands;
            }
            fixedStepAccumulatorSeconds -=
                steps * CrystalBroodmotherCatalog.FixedStepSeconds;
            if (fixedStepAccumulatorSeconds < 0d)
                fixedStepAccumulatorSeconds = 0d;

            List<CrystalBroodmotherCommand> commands = null;
            int damage = Math.Max(0, damagePerFixedStep);
            for (var step = 0; step < steps && !defeated; step++)
            {
                if (damage > 0)
                    fixtureHealth = Math.Max(0, fixtureHealth - damage);
                AppendAuthorityCommands(fixtureHealth, ref commands);
            }
            unchecked { revision++; }
            RebuildSnapshot();
            return commands == null
                ? EmptyCommands
                : new ReadOnlyCollection<CrystalBroodmotherCommand>(commands);
        }

        public CrystalBroodmotherSnapshot Capture() => cachedSnapshot;

        public IReadOnlyList<CrystalBroodmotherCommand> ObserveAuthorityHealth(
            string stableBossId,
            int currentHealth,
            int maximumHealth)
        {
            if (!string.Equals(
                    stableBossId,
                    stableInstanceId,
                    StringComparison.Ordinal) ||
                maximumHealth != CrystalBroodmotherCatalog.MaximumHealth ||
                currentHealth < 0 || currentHealth > maximumHealth ||
                defeated || hasObservedAuthorityHealth &&
                currentHealth >= lastObservedAuthorityHealth)
            {
                return EmptyCommands;
            }
            hasObservedAuthorityHealth = true;
            lastObservedAuthorityHealth = currentHealth;
            List<CrystalBroodmotherCommand> commands = null;
            AppendAuthorityCommands(currentHealth, ref commands);
            if (commands == null) return EmptyCommands;
            unchecked { revision++; }
            RebuildSnapshot();
            return new ReadOnlyCollection<CrystalBroodmotherCommand>(commands);
        }

        public bool TryRestore(
            CrystalBroodmotherSnapshot snapshot,
            out string error)
        {
            if (!IsValid(snapshot))
            {
                error = "晶壳母体快照无效或阶段状态不一致";
                return false;
            }
            phase70Triggered = snapshot.Phase70Triggered;
            phase35Triggered = snapshot.Phase35Triggered;
            defeated = snapshot.Defeated;
            revision = snapshot.Revision;
            hasObservedAuthorityHealth = false;
            lastObservedAuthorityHealth = 0;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private void AppendAuthorityCommands(
            int currentHealth,
            ref List<CrystalBroodmotherCommand> commands)
        {
            IReadOnlyList<CrystalBroodmotherPhaseDefinition> phases =
                CrystalBroodmotherCatalog.Phases;
            if (!phase70Triggered && currentHealth <= PhaseHealth(phases[0]))
            {
                phase70Triggered = true;
                EnsureCommands(ref commands).Add(PhaseCommand(phases[0]));
            }
            if (!phase35Triggered && currentHealth <= PhaseHealth(phases[1]))
            {
                phase35Triggered = true;
                EnsureCommands(ref commands).Add(PhaseCommand(phases[1]));
            }
            if (currentHealth == 0 && !defeated)
            {
                defeated = true;
                EnsureCommands(ref commands).Add(
                    new CrystalBroodmotherCommand(
                        CrystalBroodmotherCommandKind.Defeated,
                        stableInstanceId + ":defeated",
                        Array.Empty<
                            CrystalBroodmotherReinforcementDefinition>()));
            }
        }

        private CrystalBroodmotherCommand PhaseCommand(
            CrystalBroodmotherPhaseDefinition phase)
        {
            return new CrystalBroodmotherCommand(
                CrystalBroodmotherCommandKind.SpawnReinforcements,
                stableInstanceId + ":" + phase.StablePhaseId,
                phase.Reinforcements);
        }

        private static int PhaseHealth(
            CrystalBroodmotherPhaseDefinition phase)
        {
            return (int)Math.Round(
                CrystalBroodmotherCatalog.MaximumHealth *
                    (double)phase.HealthRatioThreshold,
                MidpointRounding.AwayFromZero);
        }

        private static List<CrystalBroodmotherCommand> EnsureCommands(
            ref List<CrystalBroodmotherCommand> commands)
        {
            return commands ??= new List<CrystalBroodmotherCommand>(3);
        }

        private bool IsValid(CrystalBroodmotherSnapshot snapshot)
        {
            if (snapshot == null || !string.Equals(
                    snapshot.StableInstanceId,
                    stableInstanceId,
                    StringComparison.Ordinal) ||
                snapshot.Phase35Triggered && !snapshot.Phase70Triggered ||
                snapshot.Defeated &&
                (!snapshot.Phase70Triggered || !snapshot.Phase35Triggered))
            {
                return false;
            }
            return true;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new CrystalBroodmotherSnapshot(
                stableInstanceId,
                phase70Triggered,
                phase35Triggered,
                defeated,
                revision);
        }
    }
}
