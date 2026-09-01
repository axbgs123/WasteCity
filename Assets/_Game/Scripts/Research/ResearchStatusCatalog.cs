using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WasteCity.Combat;

namespace WasteCity.Research
{
    [Flags]
    public enum ResearchStatusTarget
    {
        None = 0,
        Tower = 1 << 0,
        Enemy = 1 << 1,
        Building = 1 << 2,
        CityCore = 1 << 3,
        ArmyUnit = 1 << 4,
        Leader = 1 << 5,
    }

    public enum ResearchStatusApplication
    {
        Singleton,
        Refresh,
        StackAndRefresh,
        Recharge,
    }

    public enum ResearchStatusPersistence
    {
        ActivityState,
        TargetDisposition,
    }

    public enum ResearchStatusPhase
    {
        Active = 0,
        Boosting = 1,
        Lockout = 2,
        Cooldown = 3,
    }

    public sealed class ResearchStatusDefinition
    {
        internal ResearchStatusDefinition(
            string id,
            string displayName,
            string sourceResearchId,
            ResearchStatusTarget allowedTargets,
            ResearchStatusApplication application,
            ResearchStatusPersistence persistence,
            float durationSeconds,
            float periodSeconds,
            int maximumStacks,
            int maximumPersistedStacks,
            float maximumValue,
            bool savesPhase,
            float activeDurationSeconds,
            float boostingDurationSeconds,
            float lockoutDurationSeconds,
            float cooldownDurationSeconds)
        {
            Id = id;
            DisplayName = displayName;
            SourceResearchId = sourceResearchId;
            AllowedTargets = allowedTargets;
            Application = application;
            Persistence = persistence;
            DurationSeconds = durationSeconds;
            PeriodSeconds = periodSeconds;
            MaximumStacks = maximumStacks;
            MaximumPersistedStacks = maximumPersistedStacks;
            MaximumValue = maximumValue;
            SavesPhase = savesPhase;
            ActiveDurationSeconds = activeDurationSeconds;
            BoostingDurationSeconds = boostingDurationSeconds;
            LockoutDurationSeconds = lockoutDurationSeconds;
            CooldownDurationSeconds = cooldownDurationSeconds;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string SourceResearchId { get; }
        public ResearchStatusTarget AllowedTargets { get; }
        public ResearchStatusApplication Application { get; }
        public ResearchStatusPersistence Persistence { get; }
        public float DurationSeconds { get; }
        public float PeriodSeconds { get; }
        public int MaximumStacks { get; }
        public int MaximumPersistedStacks { get; }
        public float MaximumValue { get; }
        public bool SavesPhase { get; }
        public float ActiveDurationSeconds { get; }
        public float BoostingDurationSeconds { get; }
        public float LockoutDurationSeconds { get; }
        public float CooldownDurationSeconds { get; }
        public bool SavesPeriodRemainder => PeriodSeconds > 0f;

        public bool Allows(ResearchStatusTarget target)
        {
            return target != ResearchStatusTarget.None &&
                (AllowedTargets & target) == target;
        }

        public float MaximumRemainingSeconds(ResearchStatusPhase phase)
        {
            switch (phase)
            {
                case ResearchStatusPhase.Active:
                    return ActiveDurationSeconds;
                case ResearchStatusPhase.Boosting:
                    return BoostingDurationSeconds;
                case ResearchStatusPhase.Lockout:
                    return LockoutDurationSeconds;
                case ResearchStatusPhase.Cooldown:
                    return CooldownDurationSeconds;
                default:
                    return -1f;
            }
        }
    }

    public static class ResearchStatusCatalog
    {
        public const string TechnologyOverloadId =
            "technology.status.overload";
        public const string AutomatedRepairId =
            "technology.status.automated-repair";
        public const string SwordIntentId =
            "cultivation.status.sword-intent";
        public const string PuppetMaintenanceId =
            "cultivation.status.puppet-maintenance";
        public const string InfectionId =
            "biological.status.infection";
        public const string CarapaceRegenerationId =
            "biological.status.carapace-regeneration";
        public const string TissueRegenerationId =
            "biological.status.tissue-regeneration";
        public const string GeneSplicingTraitId =
            "biological.trait.gene-splicing";
        public const string GeneSplicingRewardKey =
            "research.reward.gene-splicing.first-completion";
        public const string PsionicResonanceId =
            "psionics.status.resonance";
        public const string CityShieldId =
            "psionics.status.city-shield";
        public const string MindControlId =
            "psionics.status.mind-control";

        private static readonly ReadOnlyCollection<ResearchStatusDefinition>
            all = Array.AsReadOnly(new[]
            {
                Status(
                    TechnologyOverloadId,
                    "能量过载",
                    "core.research.energy-weapons",
                    ResearchStatusTarget.Tower,
                    ResearchStatusApplication.Singleton,
                    TechnologyOverloadModel.CooldownSeconds,
                    periodSeconds: 0f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 0f,
                    savesPhase: true,
                    activeDurationSeconds: 0f,
                    boostingDurationSeconds:
                        TechnologyOverloadModel.BoostSeconds,
                    lockoutDurationSeconds:
                        TechnologyOverloadModel.LockoutSeconds,
                    cooldownDurationSeconds:
                        TechnologyOverloadModel.CooldownSeconds),
                Status(
                    AutomatedRepairId,
                    "自动维修",
                    "core.research.unmanned-systems",
                    ResearchStatusTarget.Building |
                    ResearchStatusTarget.CityCore,
                    ResearchStatusApplication.Singleton,
                    durationSeconds: 0f,
                    periodSeconds: 5.1f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 20f),
                Status(
                    SwordIntentId,
                    "剑意",
                    "core.research.sword-array",
                    ResearchStatusTarget.Enemy,
                    ResearchStatusApplication.StackAndRefresh,
                    durationSeconds: 0f,
                    periodSeconds: SwordIntentEmitterModel.SecondsPerStack,
                    maximumStacks: SwordIntentModel.MaximumStacks,
                    maximumPersistedStacks:
                        SwordIntentModel.MaximumStacks - 1,
                    maximumValue: 0f),
                Status(
                    PuppetMaintenanceId,
                    "傀儡维护",
                    "core.research.puppetry",
                    ResearchStatusTarget.ArmyUnit,
                    ResearchStatusApplication.Singleton,
                    durationSeconds: 0f,
                    periodSeconds: 60f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 0f),
                Status(
                    InfectionId,
                    "感染",
                    "core.research.spore-dispersal",
                    ResearchStatusTarget.Enemy,
                    ResearchStatusApplication.StackAndRefresh,
                    durationSeconds: 0f,
                    periodSeconds: InfectionModel.TickSeconds,
                    maximumStacks: InfectionModel.BurstThreshold,
                    maximumPersistedStacks:
                        InfectionModel.BurstThreshold - 1,
                    maximumValue: 0f),
                Status(
                    CarapaceRegenerationId,
                    "甲壳再生",
                    "core.research.carapace-growth",
                    ResearchStatusTarget.Building,
                    ResearchStatusApplication.Singleton,
                    durationSeconds: 0f,
                    periodSeconds: 5f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 10f),
                Status(
                    TissueRegenerationId,
                    "组织再生",
                    "core.research.tissue-regeneration",
                    ResearchStatusTarget.Building |
                    ResearchStatusTarget.ArmyUnit,
                    ResearchStatusApplication.Singleton,
                    durationSeconds: 0f,
                    periodSeconds: 1f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 1f),
                Status(
                    GeneSplicingTraitId,
                    "基因强化",
                    "core.research.gene-splicing",
                    ResearchStatusTarget.Leader,
                    ResearchStatusApplication.Singleton,
                    durationSeconds: 300f,
                    periodSeconds: 0f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 1.2f),
                Status(
                    PsionicResonanceId,
                    "灵能共鸣",
                    "core.research.mind-spire",
                    ResearchStatusTarget.Enemy,
                    ResearchStatusApplication.Refresh,
                    PsionicResonanceModel.DurationSeconds,
                    periodSeconds: 0f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 0f),
                Status(
                    CityShieldId,
                    "城市护盾",
                    "core.research.mind-shield",
                    ResearchStatusTarget.Building |
                    ResearchStatusTarget.CityCore,
                    ResearchStatusApplication.Recharge,
                    durationSeconds: 0f,
                    periodSeconds: 8f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 100f),
                Status(
                    MindControlId,
                    "精神操控",
                    "core.research.mind-control",
                    ResearchStatusTarget.Enemy,
                    ResearchStatusApplication.Singleton,
                    durationSeconds: 0f,
                    periodSeconds: 0f,
                    maximumStacks: 1,
                    maximumPersistedStacks: 1,
                    maximumValue: 0f,
                    persistence: ResearchStatusPersistence.TargetDisposition),
            });

        private static readonly IReadOnlyDictionary<string,
            ResearchStatusDefinition> byId = all.ToDictionary(
                value => value.Id,
                StringComparer.Ordinal);

        public static IReadOnlyList<ResearchStatusDefinition> All => all;

        public static ResearchStatusDefinition Find(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                byId.TryGetValue(id, out ResearchStatusDefinition value)
                    ? value
                    : null;
        }

        private static ResearchStatusDefinition Status(
            string id,
            string displayName,
            string sourceResearchId,
            ResearchStatusTarget targets,
            ResearchStatusApplication application,
            float durationSeconds,
            float periodSeconds,
            int maximumStacks,
            int maximumPersistedStacks,
            float maximumValue,
            bool savesPhase = false,
            ResearchStatusPersistence persistence =
                ResearchStatusPersistence.ActivityState,
            float activeDurationSeconds = -1f,
            float boostingDurationSeconds = 0f,
            float lockoutDurationSeconds = 0f,
            float cooldownDurationSeconds = 0f)
        {
            float resolvedActiveDuration = activeDurationSeconds < 0f
                ? durationSeconds
                : activeDurationSeconds;
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(sourceResearchId) ||
                targets == ResearchStatusTarget.None ||
                !IsFinite(durationSeconds) || durationSeconds < 0f ||
                !IsFinite(periodSeconds) || periodSeconds < 0f ||
                maximumStacks < 1 || maximumPersistedStacks < 0 ||
                maximumPersistedStacks > maximumStacks ||
                !IsFinite(maximumValue) || maximumValue < 0f ||
                !IsFinite(resolvedActiveDuration) ||
                resolvedActiveDuration < 0f ||
                !IsFinite(boostingDurationSeconds) ||
                boostingDurationSeconds < 0f ||
                !IsFinite(lockoutDurationSeconds) ||
                lockoutDurationSeconds < 0f ||
                !IsFinite(cooldownDurationSeconds) ||
                cooldownDurationSeconds < 0f)
            {
                throw new InvalidOperationException(
                    "高级科技状态目录包含无效定义：" + id);
            }

            return new ResearchStatusDefinition(
                id,
                displayName,
                sourceResearchId,
                targets,
                application,
                persistence,
                durationSeconds,
                periodSeconds,
                maximumStacks,
                maximumPersistedStacks,
                maximumValue,
                savesPhase,
                resolvedActiveDuration,
                boostingDurationSeconds,
                lockoutDurationSeconds,
                cooldownDurationSeconds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
