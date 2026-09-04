using System;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Leader.Exploration;
using WasteCity.World.Exploration;

namespace WasteCity.Graybox3D.Exploration
{
    public delegate bool TryChangeCenJinBiomass3D(
        int amount,
        out string error);

    public delegate bool TryCommitCenJinRescue3D(
        CenJinRescueCommitRequest request,
        out string error);

    public readonly struct CenJinDistressContext3D
    {
        public CenJinDistressContext3D(
            bool siteVisible,
            float cityDistance,
            bool canOperate)
        {
            SiteVisible = siteVisible;
            CityDistance = cityDistance;
            CanOperate = canOperate;
        }

        public bool SiteVisible { get; }
        public float CityDistance { get; }
        public bool CanOperate { get; }
    }

    public readonly struct GrayboxExplorationTickResult3D
    {
        internal GrayboxExplorationTickResult3D(
            ManualGatherTickResult manualGather,
            CenJinDistressTickResult distress)
        {
            ManualGather = manualGather;
            Distress = distress;
        }

        public ManualGatherTickResult ManualGather { get; }
        public CenJinDistressTickResult Distress { get; }
    }

    public sealed class GrayboxExplorationCapture3D
    {
        internal GrayboxExplorationCapture3D(
            WorldExplorationSnapshot exploration,
            LeaderControlMode leaderControlMode,
            ManualGatherSnapshot manualGather,
            CenJinDistressSnapshot cenJinDistress,
            OutpostAlertRuntimeSnapshot outpostAlerts)
        {
            Exploration = exploration;
            LeaderControlMode = leaderControlMode;
            ManualGather = manualGather;
            CenJinDistress = cenJinDistress;
            OutpostAlerts = outpostAlerts;
        }

        public WorldExplorationSnapshot Exploration { get; }
        public LeaderControlMode LeaderControlMode { get; }
        public ManualGatherSnapshot ManualGather { get; }
        public CenJinDistressSnapshot CenJinDistress { get; }
        public OutpostAlertRuntimeSnapshot OutpostAlerts { get; }
    }

    /// <summary>
    /// Formal 3D composition owner for IDEA-0029. It accepts explicit source
    /// and observation changes and never polls the world or scans the map.
    /// </summary>
    public sealed class GrayboxExplorationController3D : MonoBehaviour
    {
        private sealed class DistressTransactionAdapter :
            ICenJinDistressTransaction
        {
            private readonly TryChangeCenJinBiomass3D reserve;
            private readonly TryChangeCenJinBiomass3D release;
            private readonly TryCommitCenJinRescue3D commit;

            public DistressTransactionAdapter(
                TryChangeCenJinBiomass3D reserve,
                TryChangeCenJinBiomass3D release,
                TryCommitCenJinRescue3D commit)
            {
                this.reserve = reserve;
                this.release = release;
                this.commit = commit;
            }

            public bool TryReserveBiomass(int amount, out string error)
            {
                return reserve(amount, out error);
            }

            public bool TryReleaseBiomass(int amount, out string error)
            {
                return release(amount, out error);
            }

            public bool TryCommit(
                CenJinRescueCommitRequest request,
                out string error)
            {
                return commit(request, out error);
            }
        }

        private Func<ManualGatherContext> captureManualGatherContext;
        private Func<string, WorldHarvestTransactionResult> commitGatherOne;
        private Func<CenJinDistressContext3D> captureDistressContext;
        private DistressTransactionAdapter distressTransaction;

        public bool IsInitialized { get; private set; }
        public bool AreBoundariesConfigured =>
            captureManualGatherContext != null &&
            commitGatherOne != null &&
            captureDistressContext != null &&
            distressTransaction != null;
        public WorldExplorationRuntime Exploration { get; private set; }
        public LeaderControlRuntime LeaderControl { get; private set; }
        public ManualGatherRuntime ManualGather { get; private set; }
        public CenJinDistressRuntime CenJinDistress { get; private set; }
        public OutpostAlertRuntime OutpostAlerts { get; private set; }

        public void Initialize(
            int width,
            int height,
            string sessionId,
            TryCommitExplorationAttention attentionCommitter)
        {
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException(
                    "Exploration session ID is required.",
                    nameof(sessionId));
            if (attentionCommitter == null)
                throw new ArgumentNullException(nameof(attentionCommitter));
            if (IsInitialized)
            {
                throw new InvalidOperationException(
                    "The exploration controller is already initialized.");
            }

            var exploration = new WorldExplorationRuntime(
                width,
                height,
                sessionId,
                attentionCommitter);
            var leaderControl = new LeaderControlRuntime();
            var manualGather = new ManualGatherRuntime();
            var cenJinDistress = new CenJinDistressRuntime(sessionId);
            var outpostAlerts = new OutpostAlertRuntime();

            Exploration = exploration;
            LeaderControl = leaderControl;
            ManualGather = manualGather;
            CenJinDistress = cenJinDistress;
            OutpostAlerts = outpostAlerts;
            IsInitialized = true;
        }

        public bool TryResetSession(
            int width,
            int height,
            string sessionId,
            TryCommitExplorationAttention attentionCommitter,
            out string error)
        {
            if (width < 1 || height < 1 ||
                string.IsNullOrWhiteSpace(sessionId) ||
                attentionCommitter == null)
            {
                error = "探索会话重置参数无效";
                return false;
            }

            WorldExplorationRuntime nextExploration;
            CenJinDistressRuntime nextDistress;
            try
            {
                nextExploration = new WorldExplorationRuntime(
                    width,
                    height,
                    sessionId,
                    attentionCommitter);
                nextDistress = new CenJinDistressRuntime(sessionId);
            }
            catch (Exception exception)
            {
                error = "探索会话重置失败：" + exception.Message;
                return false;
            }

            Exploration = nextExploration;
            LeaderControl = new LeaderControlRuntime();
            ManualGather = new ManualGatherRuntime();
            CenJinDistress = nextDistress;
            OutpostAlerts = new OutpostAlertRuntime();
            captureManualGatherContext = null;
            commitGatherOne = null;
            captureDistressContext = null;
            distressTransaction = null;
            IsInitialized = true;
            error = string.Empty;
            return true;
        }

        public void ConfigureBoundaries(
            Func<ManualGatherContext> captureManualGatherContext,
            Func<string, WorldHarvestTransactionResult> commitGatherOne,
            Func<CenJinDistressContext3D> captureDistressContext,
            TryChangeCenJinBiomass3D reserveBiomass,
            TryChangeCenJinBiomass3D releaseBiomass,
            TryCommitCenJinRescue3D commitRescue)
        {
            if (captureManualGatherContext == null)
                throw new ArgumentNullException(
                    nameof(captureManualGatherContext));
            if (commitGatherOne == null)
                throw new ArgumentNullException(nameof(commitGatherOne));
            if (captureDistressContext == null)
                throw new ArgumentNullException(nameof(captureDistressContext));
            if (reserveBiomass == null)
                throw new ArgumentNullException(nameof(reserveBiomass));
            if (releaseBiomass == null)
                throw new ArgumentNullException(nameof(releaseBiomass));
            if (commitRescue == null)
                throw new ArgumentNullException(nameof(commitRescue));

            this.captureManualGatherContext = captureManualGatherContext;
            this.commitGatherOne = commitGatherOne;
            this.captureDistressContext = captureDistressContext;
            distressTransaction = new DistressTransactionAdapter(
                reserveBiomass,
                releaseBiomass,
                commitRescue);
        }

        public bool TrySyncVisionSource(
            WorldVisionSource source,
            out string error)
        {
            if (!TryRequireInitialized(out error)) return false;
            if (!source.Active)
            {
                bool removed = Exploration.RemoveSource(source.StableId);
                error = string.Empty;
                return removed;
            }
            if (source.X < 0 || source.Y < 0 ||
                source.X >= Exploration.Width ||
                source.Y >= Exploration.Height)
            {
                error = "Vision source is outside the formal world.";
                return false;
            }

            bool changed = Exploration.UpsertSource(source);
            error = string.Empty;
            return changed;
        }

        public bool TryRemoveVisionSource(
            string stableSourceId,
            out string error)
        {
            if (!TryRequireInitialized(out error)) return false;
            if (string.IsNullOrWhiteSpace(stableSourceId))
            {
                error = "A stable vision source ID is required.";
                return false;
            }
            bool changed = Exploration.RemoveSource(stableSourceId);
            error = string.Empty;
            return changed;
        }

        public bool TryObserveVisibleResource(
            WorldIntelObservation observation,
            out WorldScanResult scanResult,
            out string error)
        {
            scanResult = default;
            if (!TryRequireInitialized(out error)) return false;
            return Exploration.TryObserveVisibleResource(
                observation,
                out scanResult,
                out error);
        }

        public bool TryObserveVisibleResource(
            int worldX,
            int worldY,
            string resourceId,
            int amount,
            float ruleTimeSeconds,
            ulong sourceRevision,
            out WorldScanResult scanResult,
            out string stableNodeId,
            out string error)
        {
            scanResult = default;
            stableNodeId = string.Empty;
            if (!TryRequireInitialized(out error)) return false;
            if (worldX < 0 || worldY < 0 ||
                worldX >= Exploration.Width || worldY >= Exploration.Height ||
                amount < 0 || !IsFiniteNonNegative(ruleTimeSeconds) ||
                !ResourceDefinitionCatalog.TryGet(
                    resourceId,
                    out ResourceDefinition definition))
            {
                error = "Visible resource observation is invalid.";
                return false;
            }

            FormalResourceNodeSpec3D? formalNode =
                FormalWorldGenerationCatalog3D.FindResourceNode(
                    worldX,
                    worldY);
            if (formalNode.HasValue &&
                !string.Equals(
                    formalNode.Value.ResourceId,
                    definition.Id,
                    StringComparison.Ordinal))
            {
                error = "Formal resource node type does not match the world.";
                return false;
            }
            stableNodeId = formalNode.HasValue
                ? formalNode.Value.StableId
                : GrayboxResourceNodeIdentity3D.Create(worldX, worldY);
            var observation = new WorldIntelObservation(
                stableNodeId,
                WorldIntelKind.Resource,
                worldX,
                worldY,
                definition.ChineseName + " " + amount,
                true,
                amount,
                ruleTimeSeconds,
                sourceRevision);
            return Exploration.TryObserveVisibleResource(
                observation,
                out scanResult,
                out error);
        }

        public bool TryRequestLeaderControl(
            LeaderControlMode mode,
            out string error)
        {
            if (!TryRequireInitialized(out error)) return false;
            return LeaderControl.TryRequest(mode, out error);
        }

        public bool TryStartManualGather(out string error)
        {
            if (!TryRequireConfigured(out error)) return false;
            if (!TryCaptureManualContext(out ManualGatherContext context,
                    out error))
                return false;
            return ManualGather.TryStart(context, out error);
        }

        public bool CancelManualGather()
        {
            return IsInitialized && ManualGather.Cancel();
        }

        public bool TryBeginCenJinRescue(out string error)
        {
            if (!TryRequireConfigured(out error)) return false;
            if (!TryCaptureDistressContext(
                    out CenJinDistressContext3D context,
                    out error))
                return false;
            return CenJinDistress.TryBeginRescue(
                context.CityDistance,
                context.CanOperate,
                distressTransaction,
                out error);
        }

        public bool TryCancelCenJinRescue(out string error)
        {
            if (!TryRequireConfigured(out error)) return false;
            return CenJinDistress.TryCancel(distressTransaction, out error);
        }

        public bool TryTick(
            float deltaSeconds,
            bool paused,
            out GrayboxExplorationTickResult3D result,
            out string error)
        {
            result = default;
            if (!IsFiniteNonNegative(deltaSeconds))
            {
                error = "Tick delta must be finite and non-negative.";
                return false;
            }
            if (!TryRequireConfigured(out error)) return false;

            if (!TryCaptureDistressContext(
                    out CenJinDistressContext3D distressContext,
                    out error))
                return false;
            if (!TryCaptureManualContext(
                    out ManualGatherContext manualContext,
                    out error))
                return false;

            CenJinDistress.TryDiscover(distressContext.SiteVisible);
            ManualGatherTickResult manual = ManualGather.Tick(
                deltaSeconds,
                paused,
                manualContext,
                commitGatherOne);
            CenJinDistressTickResult distress = CenJinDistress.Tick(
                deltaSeconds,
                paused,
                distressContext.CityDistance,
                distressContext.CanOperate,
                distressTransaction);
            result = new GrayboxExplorationTickResult3D(manual, distress);
            error = string.Empty;
            return true;
        }

        public GrayboxExplorationCapture3D Capture()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the exploration controller before capture.");
            }
            return new GrayboxExplorationCapture3D(
                Exploration.Capture(),
                LeaderControl.RequestedMode,
                ManualGather.Capture(),
                CenJinDistress.Capture(),
                OutpostAlerts.Capture());
        }

        private bool TryRequireInitialized(out string error)
        {
            if (IsInitialized)
            {
                error = string.Empty;
                return true;
            }
            error = "Exploration controller is not initialized.";
            return false;
        }

        private bool TryRequireConfigured(out string error)
        {
            if (!TryRequireInitialized(out error)) return false;
            if (AreBoundariesConfigured) return true;
            error = "Exploration controller boundaries are not configured.";
            return false;
        }

        private bool TryCaptureManualContext(
            out ManualGatherContext context,
            out string error)
        {
            try
            {
                context = captureManualGatherContext();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                context = default;
                error = "Manual gather context failed: " + exception.Message;
                return false;
            }
        }

        private bool TryCaptureDistressContext(
            out CenJinDistressContext3D context,
            out string error)
        {
            try
            {
                context = captureDistressContext();
                if (!IsFiniteNonNegative(context.CityDistance))
                {
                    error = "Cen Jin distress distance is invalid.";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                context = default;
                error = "Cen Jin distress context failed: " +
                    exception.Message;
                return false;
            }
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
