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

    public sealed class BuildingUnlockEvaluationWorkspace
    {
        private readonly List<BuildingUnlockFailure> failures =
            new List<BuildingUnlockFailure>();
        private readonly List<string> reasons =
            new List<string>();
        private readonly ReadOnlyCollection<BuildingUnlockFailure>
            readOnlyFailures;
        private readonly ReadOnlyCollection<string> readOnlyReasons;

        public BuildingUnlockEvaluationWorkspace()
        {
            readOnlyFailures =
                new ReadOnlyCollection<BuildingUnlockFailure>(
                    failures);
            readOnlyReasons =
                new ReadOnlyCollection<string>(reasons);
        }

        internal IReadOnlyList<BuildingUnlockFailure> Failures =>
            readOnlyFailures;
        internal IReadOnlyList<string> Reasons => readOnlyReasons;

        internal void Prepare()
        {
            failures.Clear();
            reasons.Clear();
        }

        internal void Add(
            BuildingUnlockFailure failure,
            string reason)
        {
            failures.Add(failure);
            reasons.Add(reason);
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
            var workspace =
                new BuildingUnlockEvaluationWorkspace();
            BuildingUnlockEvaluation evaluation = Evaluate(
                definition,
                population,
                researchCompleted,
                completedBuildings,
                workspace);
            return new BuildingUnlockEvaluation(
                Snapshot(evaluation.Failures),
                Snapshot(evaluation.Reasons));
        }

        public static BuildingUnlockEvaluation Evaluate(
            BuildingDefinition definition,
            int population,
            Func<string, bool> researchCompleted,
            Func<string, int> completedBuildings,
            BuildingUnlockEvaluationWorkspace workspace)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));
            workspace.Prepare();
            if (definition == null)
            {
                workspace.Add(
                    BuildingUnlockFailure.InvalidDefinition,
                    "无效建筑");
            }
            else
            {
                if (population < definition.MinimumPopulation)
                {
                    workspace.Add(
                        BuildingUnlockFailure.Population,
                        $"需要人口 {definition.MinimumPopulation}");
                }
                if (!string.IsNullOrEmpty(definition.RequiredResearchId) &&
                    (researchCompleted == null || !researchCompleted(definition.RequiredResearchId)))
                {
                    workspace.Add(
                        BuildingUnlockFailure.Research,
                        $"需要研究 {definition.RequiredResearchId}");
                }
                if (!string.IsNullOrEmpty(definition.RequiredBuildingId) &&
                    (completedBuildings == null || completedBuildings(definition.RequiredBuildingId) <= 0))
                {
                    workspace.Add(
                        BuildingUnlockFailure.RequiredBuilding,
                        $"需要先完成 {definition.RequiredBuildingId}");
                }
            }
            return new BuildingUnlockEvaluation(
                workspace.Failures,
                workspace.Reasons);
        }

        public static bool IsUnlocked(BuildingDefinition definition,int population,Func<string,bool> researchCompleted,Func<string,int> completedBuildings,out string reason)
        {
            var evaluation=Evaluate(definition,population,researchCompleted,completedBuildings);
            reason=evaluation.PrimaryReason;
            return evaluation.IsUnlocked;
        }

        private static ReadOnlyCollection<T> Snapshot<T>(
            IReadOnlyList<T> values)
        {
            var snapshot = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
                snapshot[index] = values[index];
            return new ReadOnlyCollection<T>(snapshot);
        }
    }
}
