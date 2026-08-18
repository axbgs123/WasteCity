using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Defense
{
    public sealed class MachineGunTurretCombatModel
    {
        private const float TimeEpsilon = .00001f;
        private const float DamageBoundaryEpsilon = .00001f;

        private readonly DefenseTowerDefinition definition;
        private float activeAmmunitionSeconds;
        private float damageRemainder;

        public MachineGunTurretCombatModel(
            string stableId,
            float x,
            float z,
            int initialAmmo = 0)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException(
                    "A stable turret instance ID is required.",
                    nameof(stableId));

            definition = DefenseTowerCatalog.For(
                BuildingCatalog.MachineGunTurret.Id.Value) ??
                throw new InvalidOperationException(
                    "The machine-gun turret combat definition is missing.");
            StableId = stableId;
            X = x;
            Z = z;
            Ammo = Math.Min(AmmoCapacity, Math.Max(0, initialAmmo));
        }

        public string StableId { get; }
        public float X { get; }
        public float Z { get; }
        public float Range => definition.Range;
        public int AmmoCapacity =>
            DefenseTowerCatalog.MachineGunAmmunitionCapacity;
        public int Ammo { get; private set; }
        public bool IsLogisticsConnected { get; private set; } = true;
        public bool IsPlayerPaused { get; private set; }

        public void SetLogisticsConnected(bool connected)
        {
            IsLogisticsConnected = connected;
        }

        public void SetPlayerPaused(bool paused)
        {
            IsPlayerPaused = paused;
        }

        public int RefillFrom(CityResourceStorageModel cityStorage)
        {
            if (!IsLogisticsConnected || cityStorage == null)
                return 0;

            int missing = Math.Max(0, AmmoCapacity - Ammo);
            int moved = Math.Min(
                missing,
                cityStorage.GetNetworkAmount(definition.ConsumableId));
            if (moved <= 0)
                return 0;

            using (cityStorage.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Defense,
                           StableId)))
            {
                if (!cityStorage.TrySpendFromNetwork(
                        definition.ConsumableId,
                        moved))
                {
                    return 0;
                }
            }

            // Capacity and source availability were both preflighted. The
            // integer cache cannot reject a partial amount after the spend.
            Ammo += moved;
            return moved;
        }

        public DefenseEnemyCombatModel AcquireTarget(
            IReadOnlyList<DefenseEnemyCombatModel> candidates)
        {
            DefenseEnemyCombatModel selected = null;
            float selectedDistanceSquared = float.MaxValue;
            if (candidates == null)
                return null;

            float rangeSquared = definition.Range * definition.Range;
            for (int index = 0; index < candidates.Count; index++)
            {
                DefenseEnemyCombatModel candidate = candidates[index];
                if (candidate == null || candidate.IsDead)
                    continue;

                float distanceSquared = DistanceSquared(candidate.X, candidate.Z);
                if (distanceSquared > rangeSquared)
                    continue;

                if (selected == null ||
                    distanceSquared < selectedDistanceSquared ||
                    distanceSquared == selectedDistanceSquared &&
                    string.Compare(
                        candidate.StableId,
                        selected.StableId,
                        StringComparison.Ordinal) < 0)
                {
                    selected = candidate;
                    selectedDistanceSquared = distanceSquared;
                }
            }
            return selected;
        }

        public int Tick(
            float deltaSeconds,
            DefenseEnemyCombatModel target,
            bool globallyPaused)
        {
            if (globallyPaused || IsPlayerPaused || deltaSeconds <= 0f ||
                target == null || target.IsDead || !IsInRange(target))
            {
                return 0;
            }

            float remainingSeconds = deltaSeconds;
            int totalAppliedDamage = 0;
            while (remainingSeconds > TimeEpsilon && !target.IsDead)
            {
                if (activeAmmunitionSeconds <= TimeEpsilon)
                {
                    activeAmmunitionSeconds = 0f;
                    if (Ammo <= 0)
                        break;
                    Ammo--;
                    activeAmmunitionSeconds =
                        definition.SecondsPerConsumable;
                }

                float activeSeconds = Math.Min(
                    remainingSeconds,
                    activeAmmunitionSeconds);
                remainingSeconds -= activeSeconds;
                activeAmmunitionSeconds -= activeSeconds;
                if (activeAmmunitionSeconds < TimeEpsilon)
                    activeAmmunitionSeconds = 0f;

                damageRemainder += definition.DamagePerSecond * activeSeconds;
                int rawDamage = WholeDamage(ref damageRemainder);
                if (rawDamage <= 0)
                    continue;

                totalAppliedDamage += target.ApplyDamage(
                    rawDamage,
                    definition.DamageType);
            }
            return totalAppliedDamage;
        }

        private bool IsInRange(DefenseEnemyCombatModel target)
        {
            float rangeSquared = definition.Range * definition.Range;
            return DistanceSquared(target.X, target.Z) <= rangeSquared;
        }

        private float DistanceSquared(float targetX, float targetZ)
        {
            float offsetX = targetX - X;
            float offsetZ = targetZ - Z;
            return offsetX * offsetX + offsetZ * offsetZ;
        }

        private static int WholeDamage(ref float remainder)
        {
            int whole = (int)remainder;
            float fraction = remainder - whole;
            if (fraction > 0f && 1f - fraction <= DamageBoundaryEpsilon)
            {
                whole++;
                fraction = 0f;
            }
            remainder = fraction;
            return whole;
        }
    }

    public sealed class DefenseEnemyCombatModel
    {
        private readonly HealthModel health;
        private float attackDamageRemainder;

        public DefenseEnemyCombatModel(
            string stableId,
            EnemyDefinition definition,
            float x,
            float z)
            : this(stableId, definition, x, z, spawnOrder: -1)
        {
        }

        internal DefenseEnemyCombatModel(
            string stableId,
            EnemyDefinition definition,
            float x,
            float z,
            int spawnOrder)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException(
                    "A stable enemy instance ID is required.",
                    nameof(stableId));
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            StableId = stableId;
            SpawnOrder = spawnOrder;
            X = x;
            Z = z;
            health = new HealthModel(definition.MaximumHealth);
        }

        public string StableId { get; }
        public int SpawnOrder { get; }
        public EnemyDefinition Definition { get; }
        public float X { get; private set; }
        public float Z { get; private set; }
        public int MaximumHealth => health.Maximum;
        public int CurrentHealth => health.Current;
        public bool IsDead => health.IsDead;

        public float MoveTowards(
            float targetX,
            float targetZ,
            float deltaSeconds,
            float stopDistance)
        {
            if (IsDead || deltaSeconds <= 0f)
                return 0f;

            float offsetX = targetX - X;
            float offsetZ = targetZ - Z;
            float distance = (float)Math.Sqrt(
                offsetX * offsetX + offsetZ * offsetZ);
            float safeStopDistance = Math.Max(0f, stopDistance);
            float availableDistance = Math.Max(
                0f,
                distance - safeStopDistance);
            if (distance <= 0f || availableDistance <= 0f)
                return 0f;

            float moved = Math.Min(
                availableDistance,
                Definition.MoveSpeed * deltaSeconds);
            float directionX = offsetX / distance;
            float directionZ = offsetZ / distance;
            if (moved >= availableDistance)
            {
                X = targetX - directionX * safeStopDistance;
                Z = targetZ - directionZ * safeStopDistance;
            }
            else
            {
                X += directionX * moved;
                Z += directionZ * moved;
            }
            return moved;
        }

        public int TickAttack(
            float deltaSeconds,
            CityCoreCombatModel target,
            bool globallyPaused)
        {
            if (globallyPaused || deltaSeconds <= 0f ||
                IsDead || target == null || target.IsDestroyed)
            {
                return 0;
            }

            attackDamageRemainder += Definition.DamagePerSecond * deltaSeconds;
            int rawDamage = (int)attackDamageRemainder;
            if (rawDamage <= 0)
                return 0;
            attackDamageRemainder -= rawDamage;
            return target.ApplyDamage(rawDamage);
        }

        internal int ApplyDamage(int rawDamage, DamageType damageType)
        {
            return health.Apply(rawDamage, damageType, Definition.Armor);
        }
    }

    public sealed class CityCoreCombatModel
    {
        public const int FormalMaximumHealth = 2000;

        private readonly HealthModel health =
            new HealthModel(FormalMaximumHealth);

        public int MaximumHealth => health.Maximum;
        public int CurrentHealth => health.Current;
        public bool IsDestroyed => health.IsDead;

        internal int ApplyDamage(int rawDamage)
        {
            return health.ApplyTrueDamage(rawDamage);
        }
    }
}
