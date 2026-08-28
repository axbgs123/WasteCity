using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Leader.CivilizationExpansion
{
    public enum CharacterLifeState
    {
        Active,
        Downed,
        Recovering,
        Dead,
    }

    public enum CharacterRescueMethod
    {
        CharacterContact,
        CityMedical,
    }

    public enum CharacterRescueInterruptionReason
    {
        None,
        LeftRange,
        RescuerDamaged,
    }

    public enum CharacterLifeTickKind
    {
        None,
        RescueInterrupted,
        RescueCompleted,
        RecoveryCompleted,
        Died,
    }

    public sealed class CharacterLifeTickResult
    {
        private CharacterLifeTickResult(
            CharacterLifeTickKind kind,
            CharacterRescueInterruptionReason interruptionReason,
            int releasedBiomass,
            int consumedBiomass)
        {
            Kind = kind;
            InterruptionReason = interruptionReason;
            ReleasedBiomass = releasedBiomass;
            ConsumedBiomass = consumedBiomass;
        }

        public CharacterLifeTickKind Kind { get; }
        public CharacterRescueInterruptionReason InterruptionReason { get; }
        public int ReleasedBiomass { get; }
        public int ConsumedBiomass { get; }

        public static CharacterLifeTickResult None { get; } =
            new CharacterLifeTickResult(
                CharacterLifeTickKind.None,
                CharacterRescueInterruptionReason.None,
                0,
                0);

        internal static CharacterLifeTickResult Interrupted(
            CharacterRescueInterruptionReason reason,
            int releasedBiomass)
        {
            return new CharacterLifeTickResult(
                CharacterLifeTickKind.RescueInterrupted,
                reason,
                releasedBiomass,
                0);
        }

        internal static CharacterLifeTickResult RescueCompleted(int consumed)
        {
            return new CharacterLifeTickResult(
                CharacterLifeTickKind.RescueCompleted,
                CharacterRescueInterruptionReason.None,
                0,
                consumed);
        }

        internal static CharacterLifeTickResult For(CharacterLifeTickKind kind)
        {
            return new CharacterLifeTickResult(
                kind,
                CharacterRescueInterruptionReason.None,
                0,
                0);
        }

        internal static CharacterLifeTickResult Died(int releasedBiomass)
        {
            return new CharacterLifeTickResult(
                CharacterLifeTickKind.Died,
                CharacterRescueInterruptionReason.None,
                releasedBiomass,
                0);
        }
    }

    public sealed class CharacterCorpseRecord
    {
        internal CharacterCorpseRecord(
            string characterId,
            string settlementId,
            int x,
            int y,
            IReadOnlyList<string> equipmentIds)
        {
            CharacterId = characterId;
            SettlementId = settlementId ?? string.Empty;
            X = x;
            Y = y;
            string[] copy = new string[equipmentIds?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = equipmentIds[index];
            EquipmentIds = Array.AsReadOnly(copy);
        }

        public string CharacterId { get; }
        public string SettlementId { get; }
        public int X { get; }
        public int Y { get; }
        public IReadOnlyList<string> EquipmentIds { get; }
        public bool IsRecovered { get; internal set; }
    }

    public sealed class CharacterRescueSnapshot
    {
        public CharacterRescueSnapshot(
            CharacterRescueMethod method,
            string sourceId,
            float remainingSeconds,
            int reservedBiomass)
        {
            Method = method;
            SourceId = sourceId;
            RemainingSeconds = remainingSeconds;
            ReservedBiomass = reservedBiomass;
        }

        public CharacterRescueMethod Method { get; }
        public string SourceId { get; }
        public float RemainingSeconds { get; }
        public int ReservedBiomass { get; }
    }

    public sealed class CharacterCorpseSnapshot
    {
        private readonly ReadOnlyCollection<string> equipmentIds;

        public CharacterCorpseSnapshot(
            string characterId,
            string settlementId,
            int x,
            int y,
            IReadOnlyList<string> equipmentIds,
            bool isRecovered)
        {
            CharacterId = characterId;
            SettlementId = settlementId;
            X = x;
            Y = y;
            this.equipmentIds = Copy(equipmentIds);
            IsRecovered = isRecovered;
        }

        public string CharacterId { get; }
        public string SettlementId { get; }
        public int X { get; }
        public int Y { get; }
        public IReadOnlyList<string> EquipmentIds => equipmentIds;
        public bool IsRecovered { get; }

        private static ReadOnlyCollection<string> Copy(
            IReadOnlyList<string> source)
        {
            var result = new string[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return Array.AsReadOnly(result);
        }
    }

    public sealed class CharacterLifeSnapshot
    {
        private readonly ReadOnlyCollection<string> permanentInjuryIds;
        private readonly ReadOnlyCollection<string> equipmentIds;

        public CharacterLifeSnapshot(
            string characterId,
            CharacterLifeState state,
            int currentHealth,
            int loyalty,
            string assignedSettlementId,
            int x,
            int y,
            float downedRemainingSeconds,
            float recoveryRemainingSeconds,
            float downedElapsedSeconds,
            int downCount,
            string downedCauseId,
            CharacterRescueSnapshot rescue,
            IReadOnlyList<string> permanentInjuryIds,
            IReadOnlyList<string> equipmentIds,
            CharacterCorpseSnapshot corpse)
        {
            CharacterId = characterId;
            State = state;
            CurrentHealth = currentHealth;
            Loyalty = loyalty;
            AssignedSettlementId = assignedSettlementId;
            X = x;
            Y = y;
            DownedRemainingSeconds = downedRemainingSeconds;
            RecoveryRemainingSeconds = recoveryRemainingSeconds;
            DownedElapsedSeconds = downedElapsedSeconds;
            DownCount = downCount;
            DownedCauseId = downedCauseId;
            Rescue = rescue;
            this.permanentInjuryIds = Copy(permanentInjuryIds);
            this.equipmentIds = Copy(equipmentIds);
            Corpse = corpse;
        }

        public string CharacterId { get; }
        public CharacterLifeState State { get; }
        public int CurrentHealth { get; }
        public int Loyalty { get; }
        public string AssignedSettlementId { get; }
        public int X { get; }
        public int Y { get; }
        public float DownedRemainingSeconds { get; }
        public float RecoveryRemainingSeconds { get; }
        public float DownedElapsedSeconds { get; }
        public int DownCount { get; }
        public string DownedCauseId { get; }
        public CharacterRescueSnapshot Rescue { get; }
        public IReadOnlyList<string> PermanentInjuryIds => permanentInjuryIds;
        public IReadOnlyList<string> EquipmentIds => equipmentIds;
        public CharacterCorpseSnapshot Corpse { get; }

        private static ReadOnlyCollection<string> Copy(
            IReadOnlyList<string> source)
        {
            var result = new string[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return Array.AsReadOnly(result);
        }
    }

    public sealed class CharacterLifeRuntime
    {
        public const float BaseDownedSeconds = 60f;
        public const float CharacterContactRescueSeconds = 8f;
        public const float CityMedicalRescueSeconds = 4f;
        public const float SevereRecoverySeconds = 30f;
        public const int RescueBiomassCost = 2;
        public const string RescueResourceId = ResourceIds.Biomass;
        public const string DelayedRescueInjuryId =
            "core.injury.slow-reaction";

        private readonly List<string> equipmentIds;
        private readonly List<string> permanentInjuryIds = new List<string>();
        private CharacterRescueMethod rescueMethod;
        private string rescueSourceId = string.Empty;
        private int reservedBiomass;
        private float downedElapsedSeconds;
        private int downCount;

        public CharacterLifeRuntime(CharacterDefinition definition)
        {
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            State = CharacterLifeState.Active;
            CurrentHealth = definition.MaximumHealth;
            Loyalty = definition.InitialLoyalty;
            AssignedSettlementId = CharacterCatalog.MainCityId;
            equipmentIds = new List<string>(definition.InitialEquipmentIds);
        }

        public CharacterDefinition Definition { get; }
        public CharacterLifeState State { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaximumHealth => Definition.MaximumHealth;
        public int Loyalty { get; private set; }
        public string AssignedSettlementId { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public float DownedRemainingSeconds { get; private set; }
        public float RecoveryRemainingSeconds { get; private set; }
        public float RescueRemainingSeconds { get; private set; }
        public bool HasActiveRescue => reservedBiomass > 0;
        public bool HasPermanentInjury => permanentInjuryIds.Count > 0;
        public IReadOnlyList<string> PermanentInjuryIds =>
            new ReadOnlyCollection<string>(permanentInjuryIds);
        public CharacterCorpseRecord Corpse { get; private set; }
        public string DownedCauseId { get; private set; } = string.Empty;

        public void SetPosition(string settlementId, int x, int y)
        {
            AssignedSettlementId = settlementId ?? string.Empty;
            X = x;
            Y = y;
        }

        public void AssignSettlement(string settlementId)
        {
            AssignedSettlementId = settlementId ?? string.Empty;
        }

        public int AdjustLoyalty(int delta)
        {
            Loyalty = Math.Max(0, Math.Min(100, Loyalty + delta));
            return Loyalty;
        }

        public bool TryApplyDamage(
            int rawDamage,
            string causeId,
            out bool enteredDowned)
        {
            enteredDowned = false;
            if (State != CharacterLifeState.Active || rawDamage <= 0)
                return false;
            int applied = Math.Min(CurrentHealth, rawDamage);
            CurrentHealth -= applied;
            if (CurrentHealth > 0) return true;

            State = CharacterLifeState.Downed;
            CurrentHealth = 0;
            DownedRemainingSeconds = BaseDownedSeconds;
            downedElapsedSeconds = 0f;
            DownedCauseId = string.IsNullOrWhiteSpace(causeId)
                ? "unknown.damage.cause"
                : causeId;
            downCount++;
            enteredDowned = true;
            return true;
        }

        public bool TryBeginRescue(
            CharacterRescueMethod method,
            string sourceId,
            int availableBiomass,
            out int biomassToReserve,
            out string error)
        {
            biomassToReserve = 0;
            if (State != CharacterLifeState.Downed)
            {
                error = "目标未处于倒地状态";
                return false;
            }
            if (HasActiveRescue)
            {
                error = "已有救援正在进行";
                return false;
            }
            if (!Enum.IsDefined(typeof(CharacterRescueMethod), method) ||
                string.IsNullOrWhiteSpace(sourceId))
            {
                error = "救援来源无效";
                return false;
            }
            if (method == CharacterRescueMethod.CharacterContact &&
                string.Equals(
                    sourceId,
                    Definition.Id.Value,
                    StringComparison.Ordinal))
            {
                error = "倒地角色不能自救";
                return false;
            }
            if (availableBiomass < RescueBiomassCost)
            {
                error = "救援需要 2 生物质";
                return false;
            }

            rescueMethod = method;
            rescueSourceId = sourceId;
            RescueRemainingSeconds = method ==
                CharacterRescueMethod.CharacterContact
                    ? CharacterContactRescueSeconds
                    : CityMedicalRescueSeconds;
            reservedBiomass = RescueBiomassCost;
            biomassToReserve = RescueBiomassCost;
            error = string.Empty;
            return true;
        }

        public CharacterLifeTickResult Tick(
            float deltaSeconds,
            bool paused,
            bool rescueInRange,
            bool rescuerWasHit)
        {
            float delta = Math.Max(0f, deltaSeconds);
            if (paused || delta <= 0f || State == CharacterLifeState.Dead)
                return CharacterLifeTickResult.None;

            if (State == CharacterLifeState.Recovering)
            {
                RecoveryRemainingSeconds = Math.Max(
                    0f,
                    RecoveryRemainingSeconds - delta);
                if (RecoveryRemainingSeconds > 0f)
                    return CharacterLifeTickResult.None;
                State = CharacterLifeState.Active;
                CurrentHealth = Math.Max(1, MaximumHealth / 2);
                return CharacterLifeTickResult.For(
                    CharacterLifeTickKind.RecoveryCompleted);
            }

            if (State != CharacterLifeState.Downed)
                return CharacterLifeTickResult.None;

            if (HasActiveRescue && (!rescueInRange || rescuerWasHit))
            {
                CharacterRescueInterruptionReason reason = !rescueInRange
                    ? CharacterRescueInterruptionReason.LeftRange
                    : CharacterRescueInterruptionReason.RescuerDamaged;
                int released = ClearRescue();
                AdvanceDowned(delta);
                if (DownedRemainingSeconds <= 0f)
                    return Die(released);
                return CharacterLifeTickResult.Interrupted(reason, released);
            }

            float deathBefore = DownedRemainingSeconds;
            float rescueBefore = RescueRemainingSeconds;
            AdvanceDowned(delta);
            if (HasActiveRescue)
                RescueRemainingSeconds = Math.Max(0f, rescueBefore - delta);

            bool rescueCompletes = HasActiveRescue &&
                rescueBefore <= deathBefore &&
                RescueRemainingSeconds <= 0f;
            if (rescueCompletes)
                return CompleteRescue();
            return DownedRemainingSeconds <= 0f
                ? Die()
                : CharacterLifeTickResult.None;
        }

        public bool TryRecoverCorpse(out string[] recoveredEquipmentIds)
        {
            recoveredEquipmentIds = Array.Empty<string>();
            if (Corpse == null || Corpse.IsRecovered) return false;
            recoveredEquipmentIds = new string[Corpse.EquipmentIds.Count];
            for (var index = 0; index < recoveredEquipmentIds.Length; index++)
                recoveredEquipmentIds[index] = Corpse.EquipmentIds[index];
            Corpse.IsRecovered = true;
            return true;
        }

        public CharacterLifeSnapshot Capture()
        {
            CharacterRescueSnapshot rescue = HasActiveRescue
                ? new CharacterRescueSnapshot(
                    rescueMethod,
                    rescueSourceId,
                    RescueRemainingSeconds,
                    reservedBiomass)
                : null;
            CharacterCorpseSnapshot corpse = Corpse == null
                ? null
                : new CharacterCorpseSnapshot(
                    Corpse.CharacterId,
                    Corpse.SettlementId,
                    Corpse.X,
                    Corpse.Y,
                    Corpse.EquipmentIds,
                    Corpse.IsRecovered);
            return new CharacterLifeSnapshot(
                Definition.Id.Value,
                State,
                CurrentHealth,
                Loyalty,
                AssignedSettlementId,
                X,
                Y,
                DownedRemainingSeconds,
                RecoveryRemainingSeconds,
                downedElapsedSeconds,
                downCount,
                DownedCauseId,
                rescue,
                permanentInjuryIds,
                equipmentIds,
                corpse);
        }

        public bool TryRestore(CharacterLifeSnapshot snapshot, out string error)
        {
            if (!TryValidateRestore(
                    snapshot,
                    out List<string> injuries,
                    out List<string> equipment,
                    out CharacterCorpseRecord corpse,
                    out error))
            {
                return false;
            }

            State = snapshot.State;
            CurrentHealth = snapshot.CurrentHealth;
            Loyalty = snapshot.Loyalty;
            AssignedSettlementId = snapshot.AssignedSettlementId ?? string.Empty;
            X = snapshot.X;
            Y = snapshot.Y;
            DownedRemainingSeconds = snapshot.DownedRemainingSeconds;
            RecoveryRemainingSeconds = snapshot.RecoveryRemainingSeconds;
            downedElapsedSeconds = snapshot.DownedElapsedSeconds;
            downCount = snapshot.DownCount;
            DownedCauseId = snapshot.DownedCauseId ?? string.Empty;
            permanentInjuryIds.Clear();
            permanentInjuryIds.AddRange(injuries);
            equipmentIds.Clear();
            equipmentIds.AddRange(equipment);
            Corpse = corpse;
            if (snapshot.Rescue == null)
            {
                rescueMethod = default;
                rescueSourceId = string.Empty;
                RescueRemainingSeconds = 0f;
                reservedBiomass = 0;
            }
            else
            {
                rescueMethod = snapshot.Rescue.Method;
                rescueSourceId = snapshot.Rescue.SourceId;
                RescueRemainingSeconds = snapshot.Rescue.RemainingSeconds;
                reservedBiomass = snapshot.Rescue.ReservedBiomass;
            }
            error = string.Empty;
            return true;
        }

        private void AdvanceDowned(float delta)
        {
            DownedRemainingSeconds = Math.Max(0f, DownedRemainingSeconds - delta);
            downedElapsedSeconds += delta;
        }

        private CharacterLifeTickResult CompleteRescue()
        {
            int consumed = reservedBiomass;
            ClearRescue();
            State = CharacterLifeState.Recovering;
            CurrentHealth = 1;
            DownedRemainingSeconds = 0f;
            RecoveryRemainingSeconds = SevereRecoverySeconds;
            if ((downedElapsedSeconds > BaseDownedSeconds * .5f ||
                 downCount > 1) &&
                !permanentInjuryIds.Contains(DelayedRescueInjuryId))
            {
                permanentInjuryIds.Add(DelayedRescueInjuryId);
            }
            return CharacterLifeTickResult.RescueCompleted(consumed);
        }

        private CharacterLifeTickResult Die(int alreadyReleasedBiomass = 0)
        {
            int releasedBiomass = alreadyReleasedBiomass + ClearRescue();
            State = CharacterLifeState.Dead;
            CurrentHealth = 0;
            DownedRemainingSeconds = 0f;
            RecoveryRemainingSeconds = 0f;
            Corpse = new CharacterCorpseRecord(
                Definition.Id.Value,
                AssignedSettlementId,
                X,
                Y,
                equipmentIds);
            equipmentIds.Clear();
            return CharacterLifeTickResult.Died(releasedBiomass);
        }

        private int ClearRescue()
        {
            int previous = reservedBiomass;
            reservedBiomass = 0;
            rescueSourceId = string.Empty;
            RescueRemainingSeconds = 0f;
            return previous;
        }

        private bool TryValidateRestore(
            CharacterLifeSnapshot snapshot,
            out List<string> injuries,
            out List<string> equipment,
            out CharacterCorpseRecord corpse,
            out string error)
        {
            injuries = null;
            equipment = null;
            corpse = null;
            if (snapshot == null || !string.Equals(
                    snapshot.CharacterId,
                    Definition.Id.Value,
                    StringComparison.Ordinal))
            {
                error = "角色快照身份不匹配";
                return false;
            }
            if (!Enum.IsDefined(typeof(CharacterLifeState), snapshot.State) ||
                snapshot.CurrentHealth < 0 ||
                snapshot.CurrentHealth > MaximumHealth ||
                snapshot.Loyalty < 0 || snapshot.Loyalty > 100 ||
                snapshot.DownCount < 0 ||
                !IsFiniteRange(snapshot.DownedRemainingSeconds, 0f, BaseDownedSeconds) ||
                !IsFiniteRange(snapshot.RecoveryRemainingSeconds, 0f, SevereRecoverySeconds) ||
                !IsFiniteRange(snapshot.DownedElapsedSeconds, 0f, BaseDownedSeconds))
            {
                error = "角色生命快照数值无效";
                return false;
            }
            if (!TryCopyStableIds(snapshot.PermanentInjuryIds, out injuries) ||
                !TryCopyStableIds(snapshot.EquipmentIds, out equipment))
            {
                error = "角色伤势或装备 ID 无效";
                return false;
            }

            CharacterRescueSnapshot rescue = snapshot.Rescue;
            bool rescueValid = rescue != null &&
                Enum.IsDefined(typeof(CharacterRescueMethod), rescue.Method) &&
                !string.IsNullOrWhiteSpace(rescue.SourceId) &&
                rescue.ReservedBiomass == RescueBiomassCost &&
                IsFiniteRange(
                    rescue.RemainingSeconds,
                    float.Epsilon,
                    rescue.Method == CharacterRescueMethod.CharacterContact
                        ? CharacterContactRescueSeconds
                        : CityMedicalRescueSeconds) &&
                (rescue.Method != CharacterRescueMethod.CharacterContact ||
                 !string.Equals(
                     rescue.SourceId,
                     Definition.Id.Value,
                     StringComparison.Ordinal));

            switch (snapshot.State)
            {
                case CharacterLifeState.Active:
                    if (snapshot.CurrentHealth <= 0 ||
                        snapshot.DownedRemainingSeconds != 0f ||
                        snapshot.RecoveryRemainingSeconds != 0f ||
                        rescue != null || snapshot.Corpse != null)
                    {
                        error = "可行动角色快照不一致";
                        return false;
                    }
                    break;
                case CharacterLifeState.Downed:
                    if (snapshot.CurrentHealth != 0 ||
                        snapshot.DownedRemainingSeconds <= 0f ||
                        snapshot.RecoveryRemainingSeconds != 0f ||
                        snapshot.DownCount <= 0 ||
                        string.IsNullOrWhiteSpace(snapshot.DownedCauseId) ||
                        snapshot.Corpse != null ||
                        (rescue != null && !rescueValid))
                    {
                        error = "倒地角色快照不一致";
                        return false;
                    }
                    break;
                case CharacterLifeState.Recovering:
                    if (snapshot.CurrentHealth <= 0 ||
                        snapshot.DownedRemainingSeconds != 0f ||
                        snapshot.RecoveryRemainingSeconds <= 0f ||
                        snapshot.DownCount <= 0 ||
                        rescue != null || snapshot.Corpse != null)
                    {
                        error = "恢复中角色快照不一致";
                        return false;
                    }
                    break;
                case CharacterLifeState.Dead:
                    if (snapshot.CurrentHealth != 0 ||
                        snapshot.DownedRemainingSeconds != 0f ||
                        snapshot.RecoveryRemainingSeconds != 0f ||
                        rescue != null || snapshot.Corpse == null ||
                        equipment.Count != 0)
                    {
                        error = "死亡角色快照不一致";
                        return false;
                    }
                    break;
            }

            if (snapshot.State == CharacterLifeState.Dead)
            {
                CharacterCorpseSnapshot savedCorpse = snapshot.Corpse;
                if (!string.Equals(
                        savedCorpse.CharacterId,
                        Definition.Id.Value,
                        StringComparison.Ordinal) ||
                    !TryCopyStableIds(
                        savedCorpse.EquipmentIds,
                        out List<string> corpseEquipment))
                {
                    error = "遗体快照无效";
                    return false;
                }
                corpse = new CharacterCorpseRecord(
                    savedCorpse.CharacterId,
                    savedCorpse.SettlementId,
                    savedCorpse.X,
                    savedCorpse.Y,
                    corpseEquipment)
                {
                    IsRecovered = savedCorpse.IsRecovered,
                };
            }
            error = string.Empty;
            return true;
        }

        private static bool TryCopyStableIds(
            IReadOnlyList<string> source,
            out List<string> result)
        {
            result = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (source == null) return false;
            for (var index = 0; index < source.Count; index++)
            {
                string id = source[index];
                try
                {
                    _ = new StableId(id);
                }
                catch (ArgumentException)
                {
                    return false;
                }
                if (!unique.Add(id)) return false;
                result.Add(id);
            }
            return true;
        }

        private static bool IsFiniteRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value >= minimum && value <= maximum;
        }
    }
}
