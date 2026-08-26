using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxPocketUniverseCollapseStatus3D
    {
        Applied,
        AlreadyResolved,
        InvalidCommand,
    }

    public sealed class GrayboxPocketUniverseCollapseResult3D
    {
        internal GrayboxPocketUniverseCollapseResult3D(
            GrayboxPocketUniverseCollapseStatus3D status,
            string stableCommandId,
            int damagePerBuilding,
            IReadOnlyList<string> damagedStableInstanceIds,
            IReadOnlyList<string> destroyedStableInstanceIds,
            bool success)
        {
            Status = status;
            StableCommandId = stableCommandId ?? string.Empty;
            DamagePerBuilding = damagePerBuilding;
            DamagedStableInstanceIds = Freeze(damagedStableInstanceIds);
            DestroyedStableInstanceIds = Freeze(destroyedStableInstanceIds);
            Success = success;
        }

        public GrayboxPocketUniverseCollapseStatus3D Status { get; }
        public string StableCommandId { get; }
        public int DamagePerBuilding { get; }
        public IReadOnlyList<string> DamagedStableInstanceIds { get; }
        public IReadOnlyList<string> DestroyedStableInstanceIds { get; }
        public bool Success { get; }

        private static ReadOnlyCollection<string> Freeze(
            IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.AsReadOnly(Array.Empty<string>());
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return Array.AsReadOnly(result);
        }
    }

    public sealed class GrayboxPocketUniverseCollapseResolver3D
    {
        private readonly GrayboxBuildingSession3D session;
        private readonly GrayboxBuildingHealthRuntime3D health;
        private readonly GrayboxCombatDestructionCoordinator3D destruction;
        private readonly HashSet<string> resolvedCommandIds =
            new HashSet<string>(StringComparer.Ordinal);

        public GrayboxPocketUniverseCollapseResolver3D(
            GrayboxBuildingSession3D session,
            GrayboxBuildingHealthRuntime3D health,
            GrayboxCombatDestructionCoordinator3D destruction)
        {
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.health = health ??
                throw new ArgumentNullException(nameof(health));
            this.destruction = destruction ??
                throw new ArgumentNullException(nameof(destruction));
        }

        public GrayboxPocketUniverseCollapseResult3D Resolve(
            PocketUniverseCollapseCommand command)
        {
            if (command == null ||
                string.IsNullOrWhiteSpace(command.StableCommandId) ||
                string.IsNullOrWhiteSpace(command.StableInstanceId) ||
                (command.Size != 3 && command.Size != 4))
            {
                return Result(
                    GrayboxPocketUniverseCollapseStatus3D.InvalidCommand,
                    command?.StableCommandId,
                    success: false);
            }
            if (!resolvedCommandIds.Add(command.StableCommandId))
            {
                return Result(
                    GrayboxPocketUniverseCollapseStatus3D.AlreadyResolved,
                    command.StableCommandId,
                    success: true);
            }

            int minimumX = command.CenterX - (command.Size - 1) / 2;
            int minimumY = command.CenterY - (command.Size - 1) / 2;
            int maximumXExclusive = minimumX + command.Size;
            int maximumYExclusive = minimumY + command.Size;
            var candidates = new List<GrayboxBuildingInstance3D>();
            IReadOnlyList<GrayboxBuildingInstance3D> instances =
                session.Instances;
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (!IsEligible(instance, command.StableInstanceId) ||
                    !Intersects(
                        instance,
                        minimumX,
                        minimumY,
                        maximumXExclusive,
                        maximumYExclusive))
                {
                    continue;
                }
                candidates.Add(instance);
            }
            candidates.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));

            var damaged = new List<string>(candidates.Count);
            var destroyed = new List<string>();
            for (var index = 0; index < candidates.Count; index++)
            {
                GrayboxBuildingInstance3D instance = candidates[index];
                if (!health.TryApplyDamage(
                        instance.StableInstanceId,
                        FormalFateCatalog.PocketUniverseCollapseDamage,
                        out int appliedDamage,
                        out bool destroyedNow) ||
                    appliedDamage <= 0)
                {
                    continue;
                }
                damaged.Add(instance.StableInstanceId);
                if (!destroyedNow) continue;
                GrayboxCombatDestructionResult3D committed =
                    destruction.Commit(instance.StableInstanceId);
                if (committed.IsCommitted)
                    destroyed.Add(instance.StableInstanceId);
            }

            return new GrayboxPocketUniverseCollapseResult3D(
                GrayboxPocketUniverseCollapseStatus3D.Applied,
                command.StableCommandId,
                FormalFateCatalog.PocketUniverseCollapseDamage,
                damaged,
                destroyed,
                success: true);
        }

        private static bool IsEligible(
            GrayboxBuildingInstance3D instance,
            string sourceStableInstanceId)
        {
            return instance != null &&
                !string.Equals(
                    instance.StableInstanceId,
                    sourceStableInstanceId,
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(instance.StableInstanceId) &&
                instance.Placement?.Definition != null &&
                instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned &&
                !instance.IsEvacuationLocked;
        }

        private static bool Intersects(
            GrayboxBuildingInstance3D instance,
            int minimumX,
            int minimumY,
            int maximumXExclusive,
            int maximumYExclusive)
        {
            int width = BuildingOrientationRules.Width(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            int height = BuildingOrientationRules.Height(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            int buildingMinimumX = instance.Placement.X;
            int buildingMinimumY = instance.Placement.Y;
            return buildingMinimumX < maximumXExclusive &&
                buildingMinimumX + width > minimumX &&
                buildingMinimumY < maximumYExclusive &&
                buildingMinimumY + height > minimumY;
        }

        private static GrayboxPocketUniverseCollapseResult3D Result(
            GrayboxPocketUniverseCollapseStatus3D status,
            string stableCommandId,
            bool success)
        {
            return new GrayboxPocketUniverseCollapseResult3D(
                status,
                stableCommandId,
                FormalFateCatalog.PocketUniverseCollapseDamage,
                Array.Empty<string>(),
                Array.Empty<string>(),
                success);
        }
    }
}
