using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Building
{
    public enum BuildingUnlockFailure
    {
        None = 0,
        InvalidDefinition,
        Population,
        Research,
        RequiredBuilding
    }

    public readonly struct BuildingUnlockEvaluation
    {
        public bool IsUnlocked { get; }
        public BuildingUnlockFailure PrimaryFailure { get; }
        public IReadOnlyList<BuildingUnlockFailure> Failures { get; }
        public string PrimaryReason { get; }
        public IReadOnlyList<string> Reasons { get; }

        internal BuildingUnlockEvaluation(
            IReadOnlyList<BuildingUnlockFailure> failures,
            IReadOnlyList<string> reasons)
        {
            IsUnlocked = failures.Count == 0;
            PrimaryFailure = IsUnlocked ? BuildingUnlockFailure.None : failures[0];
            Failures = failures;
            PrimaryReason = IsUnlocked ? null : reasons[0];
            Reasons = reasons;
        }
    }

    public static class BuildingUnlockModel
    {
        public static BuildingUnlockEvaluation Evaluate(
            BuildingDefinition definition,
            int population,
            Func<string, bool> researchCompleted,
            Func<string, int> completedBuildings)
        {
            var failures = new List<BuildingUnlockFailure>();
            var reasons = new List<string>();
            if (definition == null)
            {
                failures.Add(BuildingUnlockFailure.InvalidDefinition);
                reasons.Add("无效建筑");
            }
            else
            {
                if (population < definition.MinimumPopulation)
                {
                    failures.Add(BuildingUnlockFailure.Population);
                    reasons.Add($"需要人口 {definition.MinimumPopulation}");
                }
                if (!string.IsNullOrEmpty(definition.RequiredResearchId) &&
                    (researchCompleted == null || !researchCompleted(definition.RequiredResearchId)))
                {
                    failures.Add(BuildingUnlockFailure.Research);
                    reasons.Add($"需要研究 {definition.RequiredResearchId}");
                }
                if (!string.IsNullOrEmpty(definition.RequiredBuildingId) &&
                    (completedBuildings == null || completedBuildings(definition.RequiredBuildingId) <= 0))
                {
                    failures.Add(BuildingUnlockFailure.RequiredBuilding);
                    reasons.Add($"需要先完成 {definition.RequiredBuildingId}");
                }
            }
            return new BuildingUnlockEvaluation(Snapshot(failures), Snapshot(reasons));
        }

        public static bool IsUnlocked(BuildingDefinition definition,int population,Func<string,bool> researchCompleted,Func<string,int> completedBuildings,out string reason)
        {
            var evaluation=Evaluate(definition,population,researchCompleted,completedBuildings);
            reason=evaluation.PrimaryReason;
            return evaluation.IsUnlocked;
        }

        private static IReadOnlyList<T> Snapshot<T>(List<T> values)
        {
            return new ReadOnlyCollection<T>(values.ToArray());
        }
    }
}
