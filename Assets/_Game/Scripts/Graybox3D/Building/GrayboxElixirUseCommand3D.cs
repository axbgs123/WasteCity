using System;
using System.Collections.Generic;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxElixirUseStatus3D
    {
        Used,
        MissingElixir,
        NothingToHeal,
        RuntimeUnavailable,
    }

    public readonly struct GrayboxElixirBuildingHealthSnapshot3D
    {
        public GrayboxElixirBuildingHealthSnapshot3D(
            int current,
            int maximum)
        {
            Current = Math.Max(0, current);
            Maximum = Math.Max(1, maximum);
        }

        public int Current { get; }
        public int Maximum { get; }
    }

    public sealed class GrayboxElixirHealthSnapshot3D
    {
        public GrayboxElixirHealthSnapshot3D(
            int coreCurrent,
            int coreMaximum,
            IReadOnlyList<GrayboxElixirBuildingHealthSnapshot3D> buildings)
        {
            CoreCurrent = Math.Max(0, coreCurrent);
            CoreMaximum = Math.Max(1, coreMaximum);
            Buildings = buildings ??
                Array.Empty<GrayboxElixirBuildingHealthSnapshot3D>();
        }

        public int CoreCurrent { get; }
        public int CoreMaximum { get; }
        public IReadOnlyList<GrayboxElixirBuildingHealthSnapshot3D>
            Buildings { get; }
    }

    public interface IGrayboxElixirHealthAuthority3D
    {
        bool TryCaptureElixirHealth(
            out GrayboxElixirHealthSnapshot3D snapshot);

        void ApplyElixirHealth(
            int coreHealing,
            int buildingHealing,
            int coreBacklashDamage);
    }

    public sealed class GrayboxElixirUseResult3D
    {
        internal GrayboxElixirUseResult3D(
            GrayboxElixirUseStatus3D status,
            int coreHealing,
            int buildingHealing,
            int backlashDamage,
            string message)
        {
            Status = status;
            CoreHealing = Math.Max(0, coreHealing);
            BuildingHealing = Math.Max(0, buildingHealing);
            BacklashDamage = Math.Max(0, backlashDamage);
            Message = message ?? string.Empty;
        }

        public GrayboxElixirUseStatus3D Status { get; }
        public bool Succeeded => Status == GrayboxElixirUseStatus3D.Used;
        public int CoreHealing { get; }
        public int BuildingHealing { get; }
        public int BacklashDamage { get; }
        public string Message { get; }
    }

    public static class GrayboxElixirUseCommand3D
    {
        public const string FleshElixirResearchId =
            "core.research.bridge.flesh-elixir";

        public static GrayboxElixirUseResult3D TryUse(
            CityResourceStorageModel cityStorage,
            IGrayboxElixirHealthAuthority3D healthAuthority,
            bool fleshElixirUnlocked,
            int mutationSamplePercent)
        {
            if (cityStorage == null || healthAuthority == null ||
                !healthAuthority.TryCaptureElixirHealth(
                    out GrayboxElixirHealthSnapshot3D health) ||
                health == null || health.CoreCurrent <= 0)
            {
                return Result(
                    GrayboxElixirUseStatus3D.RuntimeUnavailable,
                    "灵丹无法使用：防御生命状态未就绪");
            }
            if (!cityStorage.CanSpendFromNetwork(ResourceIds.Elixir, 1))
            {
                return Result(
                    GrayboxElixirUseStatus3D.MissingElixir,
                    "灵丹无法使用：城市库存缺少灵丹");
            }

            var simulatedInventory = new ResourceInventory(1);
            simulatedInventory.Add(ResourceIds.Elixir, 1);
            var simulatedCore = new HealthModel(health.CoreMaximum);
            simulatedCore.Restore(health.CoreCurrent);
            var simulatedBuildings = new List<HealthModel>(
                health.Buildings.Count);
            var beforeBuildingHealth = new List<int>(health.Buildings.Count);
            for (var index = 0; index < health.Buildings.Count; index++)
            {
                GrayboxElixirBuildingHealthSnapshot3D building =
                    health.Buildings[index];
                if (building.Current <= 0) continue;
                var model = new HealthModel(building.Maximum);
                model.Restore(building.Current);
                simulatedBuildings.Add(model);
                beforeBuildingHealth.Add(building.Current);
            }

            int coreBefore = simulatedCore.Current;
            if (!ElixirUseModel.TryUse(
                    simulatedInventory,
                    simulatedCore,
                    simulatedBuildings,
                    fleshElixirUnlocked,
                    mutationSamplePercent,
                    out int backlashDamage))
            {
                return Result(
                    GrayboxElixirUseStatus3D.NothingToHeal,
                    "灵丹未消耗：城市核心与建筑无需治疗");
            }

            int coreHealing = Math.Max(
                0,
                simulatedCore.Current - coreBefore + backlashDamage);
            var totalBuildingHealing = 0;
            for (var index = 0; index < simulatedBuildings.Count; index++)
            {
                totalBuildingHealing += Math.Max(
                    0,
                    simulatedBuildings[index].Current -
                    beforeBuildingHealth[index]);
            }

            // Unity rule execution is single-threaded. The successful preflight
            // and immediate spend/health commit form one command boundary.
            if (!cityStorage.TrySpendFromNetwork(ResourceIds.Elixir, 1))
            {
                return Result(
                    GrayboxElixirUseStatus3D.MissingElixir,
                    "灵丹无法使用：城市库存缺少灵丹");
            }
            int healingPerBuilding = fleshElixirUnlocked ? 300 : 100;
            healthAuthority.ApplyElixirHealth(
                coreHealing,
                healingPerBuilding,
                backlashDamage);

            string prefix = fleshElixirUnlocked ? "血肉灵丹" : "灵丹";
            string message = prefix + "已使用：核心 +" + coreHealing +
                "，建筑 +" + totalBuildingHealing;
            if (backlashDamage > 0)
                message += "，核心反噬 -" + backlashDamage;
            return new GrayboxElixirUseResult3D(
                GrayboxElixirUseStatus3D.Used,
                coreHealing,
                totalBuildingHealing,
                backlashDamage,
                message);
        }

        private static GrayboxElixirUseResult3D Result(
            GrayboxElixirUseStatus3D status,
            string message)
        {
            return new GrayboxElixirUseResult3D(
                status, 0, 0, 0, message);
        }
    }

    public sealed class ElixirSessionMutationSequence3D
    {
        private readonly Func<string> sessionIdProvider;
        private string observedSessionId = string.Empty;
        private int useOrdinal;

        public ElixirSessionMutationSequence3D(Func<string> sessionIdProvider)
        {
            this.sessionIdProvider = sessionIdProvider ??
                throw new ArgumentNullException(nameof(sessionIdProvider));
        }

        public int UseOrdinal
        {
            get
            {
                SynchronizeSession();
                return useOrdinal;
            }
        }

        public int PeekSamplePercent()
        {
            SynchronizeSession();
            return SamplePercent(observedSessionId, useOrdinal);
        }

        public void CommitUse()
        {
            SynchronizeSession();
            if (useOrdinal < int.MaxValue) useOrdinal++;
        }

        public static int SamplePercent(string sessionId, int useOrdinal)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string value = sessionId ?? string.Empty;
                for (var index = 0; index < value.Length; index++)
                {
                    char character = value[index];
                    hash = (hash ^ (byte)character) * 16777619u;
                    hash = (hash ^ (byte)(character >> 8)) * 16777619u;
                }
                uint ordinal = (uint)Math.Max(0, useOrdinal);
                hash = (hash ^ ordinal) * 16777619u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return (int)(hash % 100u);
            }
        }

        private void SynchronizeSession()
        {
            string sessionId = sessionIdProvider() ?? string.Empty;
            if (string.Equals(
                    observedSessionId,
                    sessionId,
                    StringComparison.Ordinal))
            {
                return;
            }
            observedSessionId = sessionId;
            useOrdinal = 0;
        }
    }
}
