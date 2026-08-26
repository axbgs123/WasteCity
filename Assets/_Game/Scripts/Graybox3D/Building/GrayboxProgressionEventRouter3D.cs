using System;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Persistence;
using WasteCity.Progression;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProgressionEventRouter3D : IDisposable
    {
        private const string FateSelectionReasonId =
            "core.attention.fate.first-activation";

        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;

        private CityDeploymentModel deployment;
        private GrayboxBuildingSession3D session;
        private ResearchModel research;

        public GrayboxProgressionEventRouter3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.fate = fate ??
                throw new ArgumentNullException(nameof(fate));
        }

        public void Bind(
            CityDeploymentModel deployment,
            GrayboxBuildingSession3D session)
        {
            if (deployment == null)
                throw new ArgumentNullException(nameof(deployment));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            ResearchModel nextResearch = session.Research ??
                throw new InvalidOperationException(
                    "Progression routing requires a configured research model.");

            Unbind();
            this.deployment = deployment;
            this.session = session;
            research = nextResearch;
            deployment.CheckpointCommitted += HandleDeploymentCheckpoint;
            session.BuildingCompleted += HandleBuildingCompleted;
            research.Completed += HandleResearchCompleted;
        }

        public bool TrySelectFate(string fateId, out string error)
        {
            FormalFateSnapshot previous = fate.Capture();
            if (!fate.TrySelect(
                    fateId,
                    out string attentionReasonId,
                    out string stableEventKey,
                    out error))
            {
                return false;
            }

            if (string.Equals(
                    attentionReasonId,
                    FateSelectionReasonId,
                    StringComparison.Ordinal) &&
                attention.TryApply(
                    attentionReasonId,
                    stableEventKey,
                    out error))
            {
                return true;
            }

            string attentionError = string.IsNullOrWhiteSpace(error)
                ? "Formal fate selection returned an invalid attention command."
                : error;
            if (!fate.TryRestore(previous, out string rollbackError))
            {
                error = attentionError + " Fate rollback failed: " +
                    rollbackError;
                return false;
            }
            error = attentionError;
            return false;
        }

        public void Dispose()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (deployment != null)
            {
                deployment.CheckpointCommitted -=
                    HandleDeploymentCheckpoint;
            }
            if (session != null)
                session.BuildingCompleted -= HandleBuildingCompleted;
            if (research != null)
                research.Completed -= HandleResearchCompleted;
            deployment = null;
            session = null;
            research = null;
        }

        private void HandleDeploymentCheckpoint(
            string reasonId,
            string stableEventKey)
        {
            if (!string.Equals(
                    reasonId,
                    FormalSaveCheckpointReasonIds.FirstDeploymentComplete,
                    StringComparison.Ordinal))
            {
                return;
            }
            attention.TryApply(
                "core.attention.city.first-deployment",
                stableEventKey,
                out _);
        }

        private void HandleBuildingCompleted(
            GrayboxBuildingInstance3D instance)
        {
            if (instance == null ||
                !instance.IsPlayerOwned ||
                instance.State != GrayboxBuildingInstanceState.Completed ||
                instance.Placement?.Definition == null ||
                string.IsNullOrWhiteSpace(instance.StableInstanceId))
            {
                return;
            }

            string reasonId = BuildingReasonId(
                instance.Placement.Definition.Id.Value);
            if (reasonId == null) return;
            attention.TryApply(
                reasonId,
                "building-completed:" + instance.StableInstanceId,
                out _);
        }

        private void HandleResearchCompleted(ResearchDefinition definition)
        {
            string researchId = definition?.Id.Value;
            string reasonId = ResearchReasonId(researchId);
            if (reasonId == null) return;
            attention.TryApply(
                reasonId,
                "research-completed:" + researchId,
                out _);
        }

        private static string BuildingReasonId(string definitionId)
        {
            if (string.Equals(
                    definitionId,
                    BuildingCatalog.MiningStation.Id.Value,
                    StringComparison.Ordinal))
            {
                return "core.attention.building.first-mining-station";
            }
            if (string.Equals(
                    definitionId,
                    BuildingCatalog.Smelter.Id.Value,
                    StringComparison.Ordinal))
            {
                return "core.attention.building.first-smelter";
            }
            if (string.Equals(
                    definitionId,
                    BuildingCatalog.Assembler.Id.Value,
                    StringComparison.Ordinal))
            {
                return "core.attention.building.first-assembler";
            }
            return string.Equals(
                    definitionId,
                    BuildingCatalog.MachineGunTurret.Id.Value,
                    StringComparison.Ordinal)
                ? "core.attention.building.machine-gun-turret"
                : null;
        }

        private static string ResearchReasonId(string researchId)
        {
            switch (researchId)
            {
                case "core.research.automated-machinery":
                    return "core.attention.research.automated-machinery";
                case "core.research.precision-assembly":
                    return "core.attention.research.precision-assembly";
                case "core.research.automated-defense":
                    return "core.attention.research.automated-defense";
                case "core.research.reinforced-structures":
                    return "core.attention.research.reinforced-structures";
                case "core.research.legacy-analysis":
                    return "core.attention.research.legacy-analysis";
                default:
                    return null;
            }
        }
    }
}
