using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxPocketUniverseFateController3D : IDisposable
    {
        private const string FirstProductionReasonId =
            "core.attention.fate.pocket-universe-activated";

        private readonly FormalFateRuntime fate;
        private readonly FormalAttentionRuntime attention;
        private readonly PocketUniverseFateEffect effect;
        private GrayboxBuildingSession3D session;
        private GrayboxProductionClock3D clock;
        private GrayboxDefenseController3D defense;
        private GrayboxCombatDestructionCoordinator3D destructionCoordinator;
        private GrayboxPocketUniverseCollapseResolver3D collapseResolver;

        public GrayboxPocketUniverseFateController3D(
            FormalFateRuntime fate,
            FormalAttentionRuntime attention,
            PocketUniverseFateEffect effect)
        {
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.effect = effect ??
                throw new ArgumentNullException(nameof(effect));
        }

        public PocketUniverseFateEffect Effect => effect;
        public bool IsBound => session != null && clock != null;
        public bool IsPocketUniverseActive => IsSelectedPocketUniverse();
        public GrayboxPocketUniverseCollapseResult3D LastCollapseResult
        {
            get;
            private set;
        }

        public void Bind(
            GrayboxBuildingSession3D session,
            GrayboxProductionClock3D clock)
        {
            Bind(session, clock, null);
        }

        public void Bind(
            GrayboxBuildingSession3D session,
            GrayboxProductionClock3D clock,
            GrayboxDefenseController3D defense)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            if (ReferenceEquals(this.session, session) &&
                ReferenceEquals(this.clock, clock) &&
                ReferenceEquals(this.defense, defense))
            {
                TryBindDestruction();
                SynchronizeSelection();
                return;
            }

            Unbind();
            this.session = session;
            this.clock = clock;
            this.defense = defense;
            session.BuildingCompleted += HandleBuildingCompleted;
            clock.ProductionBatchesCompleted += HandleBatchesCompleted;
            TryBindDestruction();
            SynchronizeSelection();
        }

        public void SynchronizeSelection()
        {
            if (!IsBound) return;
            TryBindDestruction();
            FormalFateSnapshot fateSnapshot = fate.Capture();
            if (!string.Equals(
                    fateSnapshot.SelectedId,
                    FormalFateCatalog.PocketUniverseId,
                    StringComparison.Ordinal))
            {
                clock.ConfigureOutputModifier(null);
                return;
            }

            effect.TrySetLevel(fateSnapshot.Level, out _);
            var candidates = new List<PocketUniverseBuildingCandidate>(
                session.Instances.Count);
            for (var index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = session.Instances[index];
                candidates.Add(ToCandidate(instance));
            }
            effect.SelectFlagships(candidates.ToArray());
            clock.ConfigureOutputModifier(effect);
        }

        public void Dispose()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (destructionCoordinator != null)
            {
                destructionCoordinator.DestructionCommitted -=
                    HandleDestructionCommitted;
            }
            if (session != null)
                session.BuildingCompleted -= HandleBuildingCompleted;
            if (clock != null)
            {
                clock.ProductionBatchesCompleted -= HandleBatchesCompleted;
                clock.ConfigureOutputModifier(null);
            }
            session = null;
            clock = null;
            defense = null;
            destructionCoordinator = null;
            collapseResolver = null;
            LastCollapseResult = null;
        }

        private void TryBindDestruction()
        {
            GrayboxCombatDestructionCoordinator3D next =
                defense?.DestructionCoordinator;
            if (ReferenceEquals(destructionCoordinator, next)) return;
            if (destructionCoordinator != null)
            {
                destructionCoordinator.DestructionCommitted -=
                    HandleDestructionCommitted;
            }
            destructionCoordinator = next;
            collapseResolver = next == null
                ? null
                : new GrayboxPocketUniverseCollapseResolver3D(
                    session,
                    defense.BuildingHealth,
                    next);
            if (destructionCoordinator != null)
            {
                destructionCoordinator.DestructionCommitted +=
                    HandleDestructionCommitted;
            }
        }

        private void HandleDestructionCommitted(
            GrayboxCombatDestructionResult3D result)
        {
            if (result == null || !result.CommittedNow ||
                collapseResolver == null || !IsSelectedPocketUniverse())
            {
                return;
            }
            GrayboxBuildingInstance3D instance = Find(
                result.StableInstanceId);
            if (instance?.Placement?.Definition == null) return;
            FormalFateSnapshot fateSnapshot = fate.Capture();
            if (!effect.TrySetLevel(fateSnapshot.Level, out _)) return;
            int width = BuildingOrientationRules.Width(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            int height = BuildingOrientationRules.Height(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            int centerX = instance.Placement.X + (width - 1) / 2;
            int centerY = instance.Placement.Y + (height - 1) / 2;
            if (!effect.TryCreateCollapseCommand(
                    instance.StableInstanceId,
                    centerX,
                    centerY,
                    out PocketUniverseCollapseCommand command))
            {
                return;
            }
            LastCollapseResult = collapseResolver.Resolve(command);
        }

        private GrayboxBuildingInstance3D Find(string stableInstanceId)
        {
            if (session?.Instances == null ||
                string.IsNullOrWhiteSpace(stableInstanceId))
            {
                return null;
            }
            for (var index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D candidate =
                    session.Instances[index];
                if (candidate != null && string.Equals(
                        candidate.StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
        }

        private void HandleBuildingCompleted(GrayboxBuildingInstance3D instance)
        {
            if (!IsSelectedPocketUniverse() || instance == null) return;
            FormalFateSnapshot fateSnapshot = fate.Capture();
            if (!effect.TrySetLevel(fateSnapshot.Level, out _)) return;
            effect.SelectFlagships(new[] { ToCandidate(instance) });
        }

        private void HandleBatchesCompleted(
            BuildingProductionState state,
            ulong completedBatchCount)
        {
            if (state == null || completedBatchCount == 0 ||
                !IsSelectedPocketUniverse())
            {
                return;
            }
            FormalFateSnapshot fateSnapshot = fate.Capture();
            if (!effect.TrySetLevel(fateSnapshot.Level, out _)) return;

            PocketUniverseFateSnapshot before = effect.Capture();
            if (!effect.TryCommitFirstProduction(
                    state.StableInstanceId,
                    out string stableEventKey))
            {
                return;
            }
            if (attention.TryApply(
                    FirstProductionReasonId,
                    stableEventKey,
                    out _))
            {
                return;
            }
            effect.TryRestore(before, out _);
        }

        private bool IsSelectedPocketUniverse()
        {
            return string.Equals(
                fate.Capture().SelectedId,
                FormalFateCatalog.PocketUniverseId,
                StringComparison.Ordinal);
        }

        private static PocketUniverseBuildingCandidate ToCandidate(
            GrayboxBuildingInstance3D instance)
        {
            return new PocketUniverseBuildingCandidate(
                instance?.StableInstanceId,
                instance?.Placement?.Definition?.Id.Value,
                instance != null &&
                    instance.State == GrayboxBuildingInstanceState.Completed,
                instance != null && instance.IsPlayerOwned);
        }
    }
}
