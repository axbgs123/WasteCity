using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Graybox3D;

namespace WasteCity.Graybox3D.Building
{
    public sealed class EvacuationManifestItemViewModel
    {
        internal EvacuationManifestItemViewModel(
            string stableInstanceId,
            string buildingName,
            BuildingMenuCategory category,
            GrayboxBuildingInstanceState state,
            double remainingRatio,
            BuildingEvacuationTreatment treatment,
            IReadOnlyList<ResourceAmount> expectedRefunds,
            float baseDismantleSeconds,
            float dismantleSeconds,
            IReadOnlyList<ResourceAmount> input,
            IReadOnlyList<ResourceAmount> reservedInput,
            IReadOnlyList<ResourceAmount> output,
            int ammunitionAmount,
            IReadOnlyList<ResourceAmount> warehouseContents,
            IReadOnlyList<ResourceAmount> lostOnAbandon,
            bool canCommit,
            IReadOnlyList<ResourceAmount> capacityShortfalls,
            string failureReason)
        {
            StableInstanceId = stableInstanceId ?? string.Empty;
            BuildingName = buildingName ?? string.Empty;
            Category = category;
            State = state;
            RemainingRatio = Math.Max(0d, Math.Min(1d, remainingRatio));
            Treatment = treatment;
            ExpectedRefunds = CopyAmounts(expectedRefunds);
            BaseDismantleSeconds = Math.Max(0f, baseDismantleSeconds);
            DismantleSeconds = float.IsNaN(dismantleSeconds)
                ? 0f
                : Math.Max(0f, dismantleSeconds);
            Input = CopyAmounts(input);
            ReservedInput = CopyAmounts(reservedInput);
            Output = CopyAmounts(output);
            AmmunitionAmount = Math.Max(0, ammunitionAmount);
            WarehouseContents = CopyAmounts(warehouseContents);
            LostOnAbandon = CopyAmounts(lostOnAbandon);
            CanCommit = canCommit;
            CapacityShortfalls = CopyAmounts(capacityShortfalls);
            FailureReason = failureReason ?? string.Empty;
        }

        public string StableInstanceId { get; }
        public string BuildingName { get; }
        public BuildingMenuCategory Category { get; }
        public GrayboxBuildingInstanceState State { get; }
        public double RemainingRatio { get; }
        public BuildingEvacuationTreatment Treatment { get; }
        public IReadOnlyList<ResourceAmount> ExpectedRefunds { get; }
        public float BaseDismantleSeconds { get; }
        public float DismantleSeconds { get; }
        public IReadOnlyList<ResourceAmount> Input { get; }
        public IReadOnlyList<ResourceAmount> ReservedInput { get; }
        public IReadOnlyList<ResourceAmount> Output { get; }
        public int AmmunitionAmount { get; }
        public IReadOnlyList<ResourceAmount> WarehouseContents { get; }
        public IReadOnlyList<ResourceAmount> LostOnAbandon { get; }
        public bool CanCommit { get; }
        public IReadOnlyList<ResourceAmount> CapacityShortfalls { get; }
        public string FailureReason { get; }

        internal static IReadOnlyList<ResourceAmount> CopyAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ResourceAmount>();
            var copy = new List<ResourceAmount>(source.Count);
            for (var index = 0; index < source.Count; index++)
                copy.Add(source[index]);
            return new ReadOnlyCollection<ResourceAmount>(copy);
        }
    }

    public sealed class EvacuationManifestViewModel
    {
        internal EvacuationManifestViewModel(
            ulong revision,
            bool isInCombat,
            float productivityMultiplier,
            bool canConfirm,
            string failureReason,
            IReadOnlyList<EvacuationManifestItemViewModel> items,
            IReadOnlyList<ResourceAmount> capacityShortfalls)
        {
            Revision = revision;
            IsInCombat = isInCombat;
            ProductivityMultiplier = Math.Max(0f, productivityMultiplier);
            CanConfirm = canConfirm;
            FailureReason = failureReason ?? string.Empty;
            var itemCopy = new List<EvacuationManifestItemViewModel>(
                items?.Count ?? 0);
            if (items != null)
                for (var index = 0; index < items.Count; index++)
                    itemCopy.Add(items[index]);
            Items = new ReadOnlyCollection<EvacuationManifestItemViewModel>(
                itemCopy);
            CapacityShortfalls =
                EvacuationManifestItemViewModel.CopyAmounts(
                    capacityShortfalls);
        }

        public ulong Revision { get; }
        public bool IsInCombat { get; }
        public float ProductivityMultiplier { get; }
        public bool CanConfirm { get; }
        public string FailureReason { get; }
        public IReadOnlyList<EvacuationManifestItemViewModel> Items { get; }
        public IReadOnlyList<ResourceAmount> CapacityShortfalls { get; }
    }

    public sealed class EvacuationQueueViewModel
    {
        internal EvacuationQueueViewModel(
            ulong revision,
            string batchId,
            bool batchIsInCombat,
            float batchProductivityMultiplier,
            int completedCount,
            int totalCount,
            string currentStableInstanceId,
            float remainingBaseSeconds,
            float remainingActualSeconds,
            bool isPaused,
            bool isBlocked,
            bool canRetry,
            string lastFailureReason,
            string capacityHint,
            IReadOnlyList<ResourceAmount> capacityShortfalls)
        {
            Revision = revision;
            BatchId = batchId ?? string.Empty;
            BatchIsInCombat = batchIsInCombat;
            BatchProductivityMultiplier = Math.Max(
                0f,
                batchProductivityMultiplier);
            CompletedCount = Math.Max(0, completedCount);
            TotalCount = Math.Max(0, totalCount);
            CurrentStableInstanceId = currentStableInstanceId ?? string.Empty;
            RemainingBaseSeconds = Math.Max(0f, remainingBaseSeconds);
            RemainingActualSeconds = Math.Max(0f, remainingActualSeconds);
            IsPaused = isPaused;
            IsBlocked = isBlocked;
            CanRetry = canRetry;
            LastFailureReason = lastFailureReason ?? string.Empty;
            CapacityHint = capacityHint ?? string.Empty;
            CapacityShortfalls =
                EvacuationManifestItemViewModel.CopyAmounts(
                    capacityShortfalls);
        }

        public ulong Revision { get; }
        public string BatchId { get; }
        public bool BatchIsInCombat { get; }
        public float BatchProductivityMultiplier { get; }
        public int CompletedCount { get; }
        public int TotalCount { get; }
        public string CurrentStableInstanceId { get; }
        public float RemainingBaseSeconds { get; }
        public float RemainingActualSeconds { get; }
        public bool IsPaused { get; }
        public bool IsBlocked { get; }
        public bool CanRetry { get; }
        public string LastFailureReason { get; }
        public string CapacityHint { get; }
        public IReadOnlyList<ResourceAmount> CapacityShortfalls { get; }
    }

    public interface IGrayboxDeploymentRequest3D
    {
        WasteCity.City.CityMode Mode { get; }
        bool TryToggleDeployment(out string failureReason);
    }

    public sealed class GrayboxEvacuationController3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;
        [SerializeField] private GrayboxBuildingMenuView3D menu;
        private IGrayboxBuildingPresentation3D evacuationPresentation;
        private IGrayboxDeploymentRequest3D deploymentRequest;
        private GrayboxProductionRuntime3D productionRuntime;
        private GrayboxDefenseRuntime3D defenseRuntime;

        private readonly List<GrayboxBuildingInstance3D> manifest =
            new List<GrayboxBuildingInstance3D>();
        private readonly Dictionary<string, BuildingEvacuationTreatment> assignments =
            new Dictionary<string, BuildingEvacuationTreatment>(StringComparer.Ordinal);
        private readonly List<BuildingEvacuationWork> work =
            new List<BuildingEvacuationWork>();
        private readonly List<BuildingEvacuationWork> fullQueue =
            new List<BuildingEvacuationWork>();
        private readonly List<BuildingEvacuationWork> rollbackWork =
            new List<BuildingEvacuationWork>();
        private readonly List<BuildingEvacuationWork> cleanupRollbackSnapshot =
            new List<BuildingEvacuationWork>();
        private readonly Dictionary<string, RuntimePayloadCapture>
            runtimePayloads =
                new Dictionary<string, RuntimePayloadCapture>(
                    StringComparer.Ordinal);
        private readonly ReadOnlyCollection<BuildingEvacuationWork> readOnlyWork;
        private int cleanupRollbackInvocationCount;
        private int cleanupMenuReleaseInvocationCount;
        private bool ownsConstructionCancellation;
        private bool isBlocked;
        private string blockedReason = string.Empty;
        private int fullQueueIndex;
        private float remainingSeconds;
        private bool queuePaused;
        private ulong nextViewRevision;
        private ulong nextBatchOrdinal;
        private string activeBatchId = string.Empty;
        private EvacuationManifestViewModel cachedManifestView;
        private EvacuationQueueViewModel cachedQueueView;
        private ulong cachedManifestSignature;
        private ulong cachedQueueSignature;
        private bool hasManifestSignature;
        private bool hasQueueSignature;

        public GrayboxEvacuationController3D()
        {
            readOnlyWork = new ReadOnlyCollection<BuildingEvacuationWork>(work);
        }

        public bool IsManifestOpen { get; private set; }
        public bool IsProcessing { get; private set; }
        public bool IsBlocked => isBlocked;
        public string BlockedReason => blockedReason;
        public IReadOnlyList<BuildingEvacuationWork> Work => readOnlyWork;

        public EvacuationManifestViewModel CaptureManifestView()
        {
            if (IsManifestOpen && !IsProcessing)
                RefreshManifestPreview();
            ulong signature = ManifestViewSignature();
            if (hasManifestSignature &&
                signature == cachedManifestSignature &&
                cachedManifestView != null)
            {
                return cachedManifestView;
            }

            EvacuationBatchContext context = CurrentBatchContext();
            var items = new List<EvacuationManifestItemViewModel>(
                manifest.Count);
            var totalShortfalls = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            bool canConfirm = IsManifestOpen && HasCompleteAssignments();
            string failureReason = string.Empty;
            for (var index = 0; index < manifest.Count; index++)
            {
                EvacuationManifestItemViewModel item =
                    CreateManifestItem(manifest[index]);
                items.Add(item);
                if (!item.CanCommit)
                {
                    canConfirm = false;
                    if (string.IsNullOrEmpty(failureReason))
                        failureReason = item.FailureReason;
                }
                AddAmounts(totalShortfalls, item.CapacityShortfalls);
            }
            if (!canConfirm && string.IsNullOrEmpty(failureReason))
                failureReason = "请先为全部项目选择处理方式";

            cachedManifestView = new EvacuationManifestViewModel(
                NextViewRevision(),
                context.IsInCombat,
                context.ProductivityMultiplier,
                canConfirm,
                failureReason,
                items,
                ToAmounts(totalShortfalls));
            cachedManifestSignature = signature;
            hasManifestSignature = true;
            return cachedManifestView;
        }

        public EvacuationQueueViewModel CaptureQueueView()
        {
            ulong signature = QueueViewSignature();
            if (hasQueueSignature &&
                signature == cachedQueueSignature &&
                cachedQueueView != null)
            {
                return cachedQueueView;
            }

            BuildingEvacuationWork current = default;
            bool hasCurrent = fullQueueIndex >= 0 &&
                fullQueueIndex < fullQueue.Count;
            if (hasCurrent) current = fullQueue[fullQueueIndex];
            EvacuationBatchContext context = work.Count > 0
                ? work[0].BatchContext
                : default;
            IReadOnlyList<ResourceAmount> shortfalls =
                isBlocked && hasCurrent
                    ? CaptureCapacityShortfalls(current)
                    : Array.Empty<ResourceAmount>();
            float actualRemaining = Math.Max(0f, remainingSeconds);
            float baseRemaining = context.ProductivityMultiplier > 0f
                ? actualRemaining * context.ProductivityMultiplier
                : actualRemaining;

            cachedQueueView = new EvacuationQueueViewModel(
                NextViewRevision(),
                activeBatchId,
                context.IsInCombat,
                context.ProductivityMultiplier,
                Math.Min(fullQueueIndex, fullQueue.Count),
                fullQueue.Count,
                hasCurrent ? current.StableInstanceId : string.Empty,
                baseRemaining,
                actualRemaining,
                queuePaused,
                isBlocked,
                IsProcessing && isBlocked,
                blockedReason,
                isBlocked
                    ? "按 E 打开背包或城市库存腾出容量后重新检查"
                    : string.Empty,
                shortfalls);
            cachedQueueSignature = signature;
            hasQueueSignature = true;
            return cachedQueueView;
        }

        public void ConfigureOperationalRuntimes(
            GrayboxProductionRuntime3D productionRuntime,
            GrayboxDefenseRuntime3D defenseRuntime)
        {
            if (productionRuntime == null)
                throw new ArgumentNullException(nameof(productionRuntime));
            if (defenseRuntime == null)
                throw new ArgumentNullException(nameof(defenseRuntime));
            if (IsManifestOpen || IsProcessing)
                throw new InvalidOperationException(
                    "Cannot replace evacuation runtime owners during a batch.");
            this.productionRuntime = productionRuntime;
            this.defenseRuntime = defenseRuntime;
        }

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation,
            GrayboxBuildingMenuView3D menu)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            ConfigureCore(
                session,
                city,
                new CityDeploymentRequestAdapter(city),
                presentation,
                presentation,
                menu);
        }

        public void Configure(
            GrayboxBuildingSession3D session,
            IGrayboxDeploymentRequest3D deploymentRequest,
            IGrayboxBuildingPresentation3D presentation,
            GrayboxBuildingMenuView3D menu)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (deploymentRequest == null)
                throw new ArgumentNullException(nameof(deploymentRequest));
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            ConfigureCore(
                session,
                null,
                deploymentRequest,
                null,
                presentation,
                menu);
        }

        private void ConfigureCore(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            IGrayboxDeploymentRequest3D deploymentRequest,
            GrayboxBuildingWorldView3D presentation,
            IGrayboxBuildingPresentation3D evacuationPresentation,
            GrayboxBuildingMenuView3D menu)
        {
            CleanupController(true);
            this.session = session;
            this.city = city;
            this.deploymentRequest = deploymentRequest;
            this.presentation = presentation;
            this.evacuationPresentation = evacuationPresentation;
            this.menu = menu;
            ResetLocalState();
            nextBatchOrdinal = 0;
            InvalidateViewCaches();
            menu.SetConstructionCancellationBlocked(false);
            if (isActiveAndEnabled) SubscribeMenu();
        }

        public bool TryHandleDeploymentRequest()
        {
            if (!IsConfigured) return false;
            if (IsManifestOpen || IsProcessing) return true;
            if (!session.HasPlayerOwnedGroundInstances)
            {
                ToggleDeploymentWithFeedback();
                return true;
            }
            if (deploymentRequest.Mode != WasteCity.City.CityMode.Fortress)
                return false;

            session.CopyPlayerOwnedGroundInstances(manifest);
            assignments.Clear();
            ClearWorkOnly();
            IsManifestOpen = true;
            ownsConstructionCancellation = true;
            menu.SetConstructionCancellationBlocked(true);
            menu.ShowEvacuationManifest(CaptureManifestView());
            return true;
        }

        public bool TryCancelManifest()
        {
            if (!IsManifestOpen || IsProcessing)
                return false;

            ResetLocalState();
            menu.HideEvacuation();
            menu.SetConstructionCancellationBlocked(false);
            return true;
        }

        public bool Assign(
            string stableInstanceId,
            BuildingEvacuationTreatment treatment)
        {
            if (!IsManifestOpen || IsProcessing ||
                string.IsNullOrEmpty(stableInstanceId) ||
                treatment == BuildingEvacuationTreatment.Unassigned)
                return false;
            for (var index = 0; index < manifest.Count; index++)
            {
                if (!string.Equals(manifest[index].StableInstanceId,
                        stableInstanceId, StringComparison.Ordinal))
                    continue;
                if (assignments.TryGetValue(
                        stableInstanceId,
                        out BuildingEvacuationTreatment existing) &&
                    existing == treatment)
                    return true;
                assignments[stableInstanceId] = treatment;
                RefreshManifestPreview();
                menu.ShowEvacuationManifest(CaptureManifestView());
                return true;
            }
            return false;
        }

        public int AssignCategory(
            BuildingMenuCategory category,
            BuildingEvacuationTreatment treatment)
        {
            if (!IsManifestOpen || IsProcessing ||
                treatment == BuildingEvacuationTreatment.Unassigned)
                return 0;
            var count = 0;
            for (var index = 0; index < manifest.Count; index++)
            {
                GrayboxBuildingInstance3D instance = manifest[index];
                if (GrayboxBuildingCatalogPresenter3D.CategoryOf(
                        instance.Placement.Definition) != category)
                    continue;
                assignments[instance.StableInstanceId] = treatment;
                count++;
            }
            if (count > 0)
            {
                RefreshManifestPreview();
                menu.ShowEvacuationManifest(CaptureManifestView());
            }
            return count;
        }

        public int AssignAll(BuildingEvacuationTreatment treatment)
        {
            if (!IsManifestOpen || IsProcessing ||
                treatment == BuildingEvacuationTreatment.Unassigned)
                return 0;
            for (var index = 0; index < manifest.Count; index++)
                assignments[manifest[index].StableInstanceId] = treatment;
            RefreshManifestPreview();
            menu.ShowEvacuationManifest(CaptureManifestView());
            return manifest.Count;
        }

        public bool ConfirmManifest()
        {
            if (!IsManifestOpen || IsProcessing) return false;
            if (!HasCompleteAssignments()) return false;
            EvacuationBatchContext frozenContext = CurrentBatchContext();
            BuildWork(frozenContext);
            if (!session.TryCaptureEvacuationWork(work, out _))
            {
                ClearWorkOnly();
                return false;
            }
            for (var index = 0; index < work.Count; index++)
                rollbackWork.Add(work[index]);
            for (var index = 0; index < work.Count; index++)
                if (work[index].Treatment !=
                    BuildingEvacuationTreatment.FullDismantle)
                    fullQueue.Add(work[index]);
            for (var index = 0; index < work.Count; index++)
                if (work[index].Treatment ==
                    BuildingEvacuationTreatment.FullDismantle)
                    fullQueue.Add(work[index]);
            if (!session.TryLockEvacuationWork(work, out _))
            {
                session.RollbackEvacuationLocksAfterFailure(rollbackWork);
                ClearWorkOnly();
                return false;
            }

            try
            {
                CaptureRuntimePayloads();
            }
            catch
            {
                FailProcessing();
                throw;
            }

            IsManifestOpen = false;
            IsProcessing = true;
            isBlocked = false;
            blockedReason = string.Empty;
            unchecked { nextBatchOrdinal++; }
            activeBatchId = "evacuation.batch." +
                nextBatchOrdinal.ToString("D6", CultureInfo.InvariantCulture);
            fullQueueIndex = 0;
            remainingSeconds = 0f;
            bool advanced = AdvanceThroughImmediateWork();
            if (IsProcessing)
                menu.ShowEvacuationQueue(CaptureQueueView());
            return advanced;
        }

        public void Tick(float unscaledDeltaTime, bool paused)
        {
            queuePaused = paused;
            if (IsManifestOpen && !IsProcessing)
            {
                RefreshManifestPreview();
                menu.ShowEvacuationManifest(CaptureManifestView());
            }
            if (!IsProcessing || IsBlocked || paused ||
                unscaledDeltaTime <= 0f)
            {
                if (IsProcessing)
                    menu.ShowEvacuationQueue(CaptureQueueView());
                return;
            }
            remainingSeconds -= unscaledDeltaTime *
                Math.Max(0f, session.DevelopmentRuleTimeMultiplier);
            if (remainingSeconds <= 0f &&
                TryCommitCurrent() == CommitCurrentResult.Succeeded)
                AdvanceThroughImmediateWork();
            if (IsProcessing)
                menu.ShowEvacuationQueue(CaptureQueueView());
        }

        public bool RetryBlockedWork()
        {
            if (!IsProcessing || !IsBlocked ||
                fullQueueIndex < 0 || fullQueueIndex >= fullQueue.Count)
                return false;
            isBlocked = false;
            blockedReason = string.Empty;
            CommitCurrentResult result = TryCommitCurrent();
            if (result != CommitCurrentResult.Succeeded)
            {
                menu.ShowEvacuationQueue(CaptureQueueView());
                return false;
            }
            AdvanceThroughImmediateWork();
            if (IsProcessing)
                menu.ShowEvacuationQueue(CaptureQueueView());
            return true;
        }

        private void RefreshManifestPreview()
        {
            if (!IsManifestOpen || IsProcessing) return;
            BuildWork(CurrentBatchContext());
        }

        private bool HasCompleteAssignments()
        {
            if (manifest.Count == 0) return false;
            for (var index = 0; index < manifest.Count; index++)
            {
                if (!assignments.TryGetValue(
                        manifest[index].StableInstanceId,
                        out BuildingEvacuationTreatment treatment) ||
                    treatment == BuildingEvacuationTreatment.Unassigned)
                {
                    return false;
                }
            }
            return true;
        }

        private EvacuationBatchContext CurrentBatchContext()
        {
            bool isInCombat = defenseRuntime != null &&
                defenseRuntime.Snapshot.AliveEnemyCount > 0;
            return BuildingEvacuationRules.CreateBatchContext(
                isInCombat,
                session.ProductivityMultiplier);
        }

        private void BuildWork(EvacuationBatchContext context)
        {
            work.Clear();
            for (var index = 0; index < manifest.Count; index++)
            {
                GrayboxBuildingInstance3D instance = manifest[index];
                if (!assignments.TryGetValue(
                        instance.StableInstanceId,
                        out BuildingEvacuationTreatment treatment) ||
                    treatment == BuildingEvacuationTreatment.Unassigned)
                {
                    continue;
                }
                double remainingRatio = instance.State ==
                    GrayboxBuildingInstanceState.Completed
                    ? 1d
                    : instance.Progress.BaseDuration > 0f
                        ? instance.Progress.Remaining /
                          instance.Progress.BaseDuration
                        : 0d;
                work.Add(BuildingEvacuationRules.Create(
                    instance.StableInstanceId,
                    instance.Placement.Definition.Cost,
                    instance.Progress.BaseDuration,
                    remainingRatio,
                    treatment,
                    context));
            }
            work.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
        }

        private void CaptureRuntimePayloads()
        {
            runtimePayloads.Clear();
            for (var index = 0; index < work.Count; index++)
            {
                string stableInstanceId = work[index].StableInstanceId;
                GrayboxProductionEvacuationPayload3D productionPayload = null;
                GrayboxDefenseEvacuationPayload3D defensePayload = null;
                productionRuntime?.TryCaptureEvacuationPayload(
                    stableInstanceId,
                    out productionPayload);
                defenseRuntime?.TryCaptureEvacuationPayload(
                    stableInstanceId,
                    out defensePayload);
                if (productionPayload == null && defensePayload == null)
                    continue;
                runtimePayloads.Add(
                    stableInstanceId,
                    RuntimePayloadCapture.Create(
                        productionPayload,
                        defensePayload));
            }
        }

        private bool AdvanceThroughImmediateWork()
        {
            while (IsProcessing && fullQueueIndex < fullQueue.Count)
            {
                BuildingEvacuationWork current = fullQueue[fullQueueIndex];
                if (current.Treatment ==
                    BuildingEvacuationTreatment.FullDismantle)
                {
                    remainingSeconds = current.DismantleSeconds;
                    return true;
                }
                CommitCurrentResult result = TryCommitCurrent();
                if (result == CommitCurrentResult.Succeeded)
                    continue;
                return result == CommitCurrentResult.Blocked;
            }

            if (!IsProcessing) return false;
            IsProcessing = false;
            return FinishIfResolved();
        }

        private CommitCurrentResult TryCommitCurrent()
        {
            if (!IsProcessing || fullQueueIndex < 0 ||
                fullQueueIndex >= fullQueue.Count)
                return CommitCurrentResult.Failed;
            BuildingEvacuationWork current = fullQueue[fullQueueIndex];
            IReadOnlyList<ResourceAmount> payload =
                Array.Empty<ResourceAmount>();
            if (runtimePayloads.TryGetValue(
                    current.StableInstanceId,
                    out RuntimePayloadCapture capture))
            {
                payload = capture.Resources;
            }

            bool committed;
            string failureReason;
            GrayboxEvacuationCommitCode3D commitCode;
            try
            {
                committed = session.TryCommitEvacuationWithPayload(
                    current,
                    payload,
                    EvacuationPresentation,
                    out _,
                    out failureReason,
                    out commitCode);
            }
            catch
            {
                FailProcessing();
                throw;
            }
            if (!committed)
            {
                if (commitCode ==
                    GrayboxEvacuationCommitCode3D.CapacityInsufficient)
                {
                    isBlocked = true;
                    blockedReason = failureReason;
                    remainingSeconds = 0f;
                    return CommitCurrentResult.Blocked;
                }
                FailProcessing();
                return CommitCurrentResult.Failed;
            }

            rollbackWork.Remove(current);
            try
            {
                FinalizeRuntimePayload(current, capture);
            }
            catch
            {
                FailProcessing();
                throw;
            }
            runtimePayloads.Remove(current.StableInstanceId);
            isBlocked = false;
            blockedReason = string.Empty;
            fullQueueIndex++;
            remainingSeconds = 0f;
            return CommitCurrentResult.Succeeded;
        }

        private void FinalizeRuntimePayload(
            in BuildingEvacuationWork current,
            RuntimePayloadCapture capture)
        {
            if (capture == null) return;
            bool abandon = current.Treatment ==
                BuildingEvacuationTreatment.Abandon;
            if (capture.Production != null)
            {
                bool completed = abandon
                    ? productionRuntime.TryDiscardEvacuationPayload(
                        current.StableInstanceId)
                    : productionRuntime.TryFinalizeEvacuationPayload(
                        current.StableInstanceId,
                        capture.Production);
                if (!completed)
                    throw new InvalidOperationException(
                        "Production evacuation payload changed after capture.");
            }
            if (capture.Defense != null)
            {
                bool completed = abandon
                    ? defenseRuntime.TryDiscardEvacuationPayload(
                        current.StableInstanceId)
                    : defenseRuntime.TryFinalizeEvacuationPayload(
                        current.StableInstanceId,
                        capture.Defense);
                if (!completed)
                    throw new InvalidOperationException(
                        "Defense evacuation payload changed after capture.");
            }
        }

        private bool FinishIfResolved()
        {
            if (session.HasPlayerOwnedGroundInstances) return true;
            ToggleDeploymentWithFeedback();
            ResetLocalState();
            menu.HideEvacuation();
            menu.SetConstructionCancellationBlocked(false);
            return true;
        }

        private void ToggleDeploymentWithFeedback()
        {
            bool toggled = deploymentRequest.TryToggleDeployment(
                out string failureReason);
            if (toggled)
                menu.ClearDeploymentFailure();
            else
                menu.ShowDeploymentFailure(failureReason);
        }

        private void FailProcessing()
        {
            session.RollbackEvacuationLocksAfterFailure(rollbackWork);
            IsProcessing = false;
            isBlocked = false;
            blockedReason = string.Empty;
            IsManifestOpen = true;
            fullQueueIndex = 0;
            remainingSeconds = 0f;
            ClearWorkOnly();
            session.CopyPlayerOwnedGroundInstances(manifest);
            menu.SetConstructionCancellationBlocked(true);
            menu.ShowEvacuationManifest(CaptureManifestView());
        }

        private void ResetLocalState()
        {
            IsManifestOpen = false;
            IsProcessing = false;
            isBlocked = false;
            blockedReason = string.Empty;
            ownsConstructionCancellation = false;
            queuePaused = false;
            activeBatchId = string.Empty;
            manifest.Clear();
            assignments.Clear();
            ClearWorkOnly();
            fullQueueIndex = 0;
            remainingSeconds = 0f;
            InvalidateViewCaches();
        }

        private void ClearWorkOnly()
        {
            work.Clear();
            fullQueue.Clear();
            rollbackWork.Clear();
            runtimePayloads.Clear();
        }

        private void InvalidateViewCaches()
        {
            cachedManifestView = null;
            cachedQueueView = null;
            cachedManifestSignature = 0ul;
            cachedQueueSignature = 0ul;
            hasManifestSignature = false;
            hasQueueSignature = false;
        }

        private EvacuationManifestItemViewModel CreateManifestItem(
            GrayboxBuildingInstance3D instance)
        {
            assignments.TryGetValue(
                instance.StableInstanceId,
                out BuildingEvacuationTreatment treatment);
            bool assigned = treatment !=
                BuildingEvacuationTreatment.Unassigned;
            BuildingEvacuationWork itemWork = default;
            bool hasWork = assigned && TryFindWork(
                instance.StableInstanceId,
                out itemWork);
            double remainingRatio = instance.State ==
                GrayboxBuildingInstanceState.Completed
                    ? 1d
                    : instance.Progress.BaseDuration > 0f
                        ? instance.Progress.Remaining /
                          instance.Progress.BaseDuration
                        : 0d;

            GrayboxProductionEvacuationPayload3D production = null;
            GrayboxDefenseEvacuationPayload3D defense = null;
            productionRuntime?.TryCaptureEvacuationPayload(
                instance.StableInstanceId,
                out production);
            defenseRuntime?.TryCaptureEvacuationPayload(
                instance.StableInstanceId,
                out defense);
            RuntimePayloadCapture payload =
                production == null && defense == null
                    ? null
                    : RuntimePayloadCapture.Create(production, defense);
            IReadOnlyList<ResourceAmount> expectedRefunds =
                hasWork && itemWork.Treatment !=
                    BuildingEvacuationTreatment.Abandon &&
                itemWork.Refund > 0
                    ? new[]
                    {
                        new ResourceAmount(
                            instance.Placement.Definition.CostId,
                            itemWork.Refund)
                    }
                    : Array.Empty<ResourceAmount>();
            IReadOnlyList<ResourceAmount> warehouseContents =
                CaptureWarehouseContents(instance.StableInstanceId);
            IReadOnlyList<ResourceAmount> lostOnAbandon =
                hasWork && itemWork.Treatment ==
                    BuildingEvacuationTreatment.Abandon &&
                warehouseContents.Count == 0 && payload != null
                    ? payload.Resources
                    : Array.Empty<ResourceAmount>();
            CityResourceEvacuationPlan plan = hasWork
                ? CreatePreviewStoragePlan(instance, itemWork, payload)
                : null;
            IReadOnlyList<ResourceAmount> shortfalls =
                CaptureShortfalls(plan);
            bool canCommit = assigned && hasWork &&
                (plan == null || plan.CanCommit);
            string failureReason = !assigned
                ? "尚未选择处理方式"
                : plan != null && !plan.IsValid
                    ? "撤离资源预检无效"
                    : shortfalls.Count > 0
                        ? "城市仓储容量不足"
                        : string.Empty;

            return new EvacuationManifestItemViewModel(
                instance.StableInstanceId,
                instance.Placement.Definition.Name,
                GrayboxBuildingCatalogPresenter3D.CategoryOf(
                    instance.Placement.Definition),
                instance.State,
                remainingRatio,
                treatment,
                expectedRefunds,
                hasWork ? itemWork.BaseDismantleSeconds : 0f,
                hasWork ? itemWork.DismantleSeconds : 0f,
                production?.Input,
                production?.ReservedInput,
                production?.Output,
                defense?.AmmunitionAmount ?? 0,
                warehouseContents,
                lostOnAbandon,
                canCommit,
                shortfalls,
                failureReason);
        }

        private bool TryFindWork(
            string stableInstanceId,
            out BuildingEvacuationWork result)
        {
            for (var index = 0; index < work.Count; index++)
            {
                if (!string.Equals(
                        work[index].StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                    continue;
                result = work[index];
                return true;
            }
            result = default;
            return false;
        }

        private IReadOnlyList<ResourceAmount> CaptureWarehouseContents(
            string stableInstanceId)
        {
            if (session?.CityStorage == null ||
                !session.CityStorage.TryGetWarehouseSnapshot(
                    stableInstanceId,
                    out WarehouseStorageSnapshot warehouse))
                return Array.Empty<ResourceAmount>();
            var amounts = new List<ResourceAmount>();
            for (var index = 0; index < ResourceIds.All.Length; index++)
            {
                string resourceId = ResourceIds.All[index];
                int amount = warehouse.Get(resourceId);
                if (amount > 0)
                    amounts.Add(new ResourceAmount(resourceId, amount));
            }
            return amounts;
        }

        private CityResourceEvacuationPlan CreatePreviewStoragePlan(
            GrayboxBuildingInstance3D instance,
            in BuildingEvacuationWork itemWork,
            RuntimePayloadCapture payload)
        {
            if (session?.CityStorage == null || instance == null)
                return null;
            bool removesWarehouse = session.CityStorage.ContainsWarehouse(
                instance.StableInstanceId);
            var additions = new List<ResourceAmount>();
            if (itemWork.Treatment !=
                    BuildingEvacuationTreatment.Abandon &&
                payload != null)
            {
                for (var index = 0; index < payload.Resources.Count; index++)
                    additions.Add(payload.Resources[index]);
            }
            if (itemWork.Treatment !=
                    BuildingEvacuationTreatment.Abandon &&
                itemWork.Refund > 0)
            {
                additions.Add(new ResourceAmount(
                    instance.Placement.Definition.CostId,
                    itemWork.Refund));
            }
            if (!removesWarehouse && additions.Count == 0)
                return null;
            return session.CityStorage.CreateEvacuationPlan(
                removesWarehouse ? instance.StableInstanceId : null,
                additions);
        }

        private IReadOnlyList<ResourceAmount> CaptureCapacityShortfalls(
            in BuildingEvacuationWork itemWork)
        {
            GrayboxBuildingInstance3D instance = null;
            for (var index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D candidate =
                    session.Instances[index];
                if (!string.Equals(
                        candidate.StableInstanceId,
                        itemWork.StableInstanceId,
                        StringComparison.Ordinal))
                    continue;
                instance = candidate;
                break;
            }
            if (instance == null) return Array.Empty<ResourceAmount>();
            runtimePayloads.TryGetValue(
                itemWork.StableInstanceId,
                out RuntimePayloadCapture payload);
            return CaptureShortfalls(
                CreatePreviewStoragePlan(instance, itemWork, payload));
        }

        private static IReadOnlyList<ResourceAmount> CaptureShortfalls(
            CityResourceEvacuationPlan plan)
        {
            if (plan == null || plan.TotalShortfall <= 0)
                return Array.Empty<ResourceAmount>();
            var amounts = new List<ResourceAmount>();
            for (var index = 0; index < ResourceIds.All.Length; index++)
            {
                string resourceId = ResourceIds.All[index];
                int amount = plan.GetShortfall(resourceId);
                if (amount > 0)
                    amounts.Add(new ResourceAmount(resourceId, amount));
            }
            return amounts;
        }

        private static void AddAmounts(
            IDictionary<string, int> totals,
            IReadOnlyList<ResourceAmount> amounts)
        {
            if (amounts == null) return;
            for (var index = 0; index < amounts.Count; index++)
            {
                ResourceAmount amount = amounts[index];
                totals.TryGetValue(amount.ResourceId, out int before);
                totals[amount.ResourceId] = before + Math.Max(0, amount.Amount);
            }
        }

        private static IReadOnlyList<ResourceAmount> ToAmounts(
            IEnumerable<KeyValuePair<string, int>> totals)
        {
            var result = new List<ResourceAmount>();
            foreach (KeyValuePair<string, int> item in totals)
                if (item.Value > 0)
                    result.Add(new ResourceAmount(item.Key, item.Value));
            return result;
        }

        private ulong ManifestViewSignature()
        {
            ulong hash = 1469598103934665603ul;
            Mix(ref hash, IsManifestOpen ? 1 : 0);
            Mix(ref hash, IsProcessing ? 1 : 0);
            Mix(ref hash, session?.CatalogRevision ?? 0u);
            Mix(ref hash, session?.PlacementRevision ?? 0u);
            Mix(ref hash, session?.CityStorage?.Revision ?? 0ul);
            Mix(ref hash, session?.ProductivityMultiplier ?? 0f);
            Mix(ref hash, defenseRuntime?.Snapshot?.AliveEnemyCount ?? 0);
            for (var index = 0; index < manifest.Count; index++)
            {
                GrayboxBuildingInstance3D instance = manifest[index];
                Mix(ref hash, instance.StableInstanceId);
                Mix(ref hash, (int)instance.State);
                Mix(ref hash, instance.Progress.Remaining);
                assignments.TryGetValue(
                    instance.StableInstanceId,
                    out BuildingEvacuationTreatment treatment);
                Mix(ref hash, (int)treatment);
                MixManifestRuntimePayload(
                    ref hash,
                    instance.StableInstanceId);
            }
            for (var index = 0; index < work.Count; index++)
            {
                Mix(ref hash, work[index].StableInstanceId);
                Mix(ref hash, work[index].GetHashCode());
            }
            return hash;
        }

        private void MixManifestRuntimePayload(
            ref ulong hash,
            string stableInstanceId)
        {
            BuildingProductionState productionState = null;
            IReadOnlyList<BuildingProductionState> productionStates =
                productionRuntime?.States;
            if (productionStates != null)
            {
                for (var index = 0; index < productionStates.Count; index++)
                {
                    BuildingProductionState candidate =
                        productionStates[index];
                    if (!string.Equals(
                            candidate.StableInstanceId,
                            stableInstanceId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    productionState = candidate;
                    break;
                }
            }

            Mix(ref hash, productionState == null ? 0 : 1);
            if (productionState != null)
            {
                FormalProductionDefinition definition =
                    productionState.Definition;
                Mix(ref hash, productionState.HasReservedInputs ? 1 : 0);
                for (var index = 0; index < ResourceIds.All.Length; index++)
                {
                    string resourceId = ResourceIds.All[index];
                    Mix(ref hash, productionState.Input.Get(resourceId));
                    int reservedAmount =
                        productionState.HasReservedInputs &&
                        !definition.UsesBoundResourceNode &&
                        definition.InputAmount > 0 &&
                        string.Equals(
                            definition.InputResourceId,
                            resourceId,
                            StringComparison.Ordinal)
                            ? definition.InputAmount
                            : 0;
                    Mix(ref hash, reservedAmount);
                    Mix(ref hash, productionState.Output.Get(resourceId));
                }
            }

            GrayboxDefenseTowerRuntimeState3D defenseTower = null;
            IReadOnlyList<GrayboxDefenseTowerRuntimeState3D> defenseTowers =
                defenseRuntime?.Towers;
            if (defenseTowers != null)
            {
                for (var index = 0; index < defenseTowers.Count; index++)
                {
                    GrayboxDefenseTowerRuntimeState3D candidate =
                        defenseTowers[index];
                    if (!string.Equals(
                            candidate.StableId,
                            stableInstanceId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    defenseTower = candidate;
                    break;
                }
            }

            Mix(ref hash, defenseTower == null ? 0 : 1);
            if (defenseTower != null)
                Mix(ref hash, defenseTower.Combat.Ammo);
        }

        private ulong QueueViewSignature()
        {
            ulong hash = 1469598103934665603ul;
            Mix(ref hash, IsProcessing ? 1 : 0);
            Mix(ref hash, isBlocked ? 1 : 0);
            Mix(ref hash, queuePaused ? 1 : 0);
            Mix(ref hash, fullQueueIndex);
            Mix(ref hash, fullQueue.Count);
            Mix(ref hash, remainingSeconds);
            Mix(ref hash, activeBatchId);
            Mix(ref hash, blockedReason);
            Mix(ref hash, session?.CityStorage?.Revision ?? 0ul);
            return hash;
        }

        private ulong NextViewRevision()
        {
            unchecked { nextViewRevision++; }
            return nextViewRevision;
        }

        private static void Mix(ref ulong hash, string value)
        {
            if (value == null)
            {
                Mix(ref hash, 0);
                return;
            }
            for (var index = 0; index < value.Length; index++)
                Mix(ref hash, value[index]);
        }

        private static void Mix(ref ulong hash, float value)
        {
            Mix(ref hash, value.GetHashCode());
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 1099511628211ul;
            }
        }

        private static void Mix(ref ulong hash, int value)
        {
            Mix(ref hash, unchecked((ulong)(uint)value));
        }

        private bool IsConfigured =>
            session != null && deploymentRequest != null &&
            EvacuationPresentation != null && menu != null;

        private IGrayboxBuildingPresentation3D EvacuationPresentation =>
            evacuationPresentation ?? presentation;

        private void Update()
        {
            Tick(Time.deltaTime, Time.timeScale <= 0f);
        }

        private void OnEnable()
        {
            RestoreSerializedRuntimeDependencies();
            SubscribeMenu();
        }

        private void OnDisable()
        {
            CleanupController(false);
        }

        private void OnDestroy()
        {
            CleanupController(true);
        }

        private void OnItemTreatmentRequested(
            string stableInstanceId,
            BuildingEvacuationTreatment treatment)
        {
            Assign(stableInstanceId, treatment);
        }

        private void OnCategoryTreatmentRequested(
            BuildingMenuCategory category,
            BuildingEvacuationTreatment treatment)
        {
            AssignCategory(category, treatment);
        }

        private void OnAllTreatmentRequested(
            BuildingEvacuationTreatment treatment)
        {
            AssignAll(treatment);
        }

        private void OnConfirmationRequested()
        {
            ConfirmManifest();
        }

        private void OnRetryRequested()
        {
            RetryBlockedWork();
        }

        private void SubscribeMenu()
        {
            if (ReferenceEquals(menu, null)) return;
            UnsubscribeMenu();
            menu.EvacuationItemTreatmentRequested += OnItemTreatmentRequested;
            menu.EvacuationCategoryTreatmentRequested += OnCategoryTreatmentRequested;
            menu.EvacuationAllTreatmentRequested += OnAllTreatmentRequested;
            menu.EvacuationConfirmationRequested += OnConfirmationRequested;
            menu.EvacuationRetryRequested += OnRetryRequested;
        }

        private void UnsubscribeMenu()
        {
            if (ReferenceEquals(menu, null)) return;
            menu.EvacuationItemTreatmentRequested -= OnItemTreatmentRequested;
            menu.EvacuationCategoryTreatmentRequested -= OnCategoryTreatmentRequested;
            menu.EvacuationAllTreatmentRequested -= OnAllTreatmentRequested;
            menu.EvacuationConfirmationRequested -= OnConfirmationRequested;
            menu.EvacuationRetryRequested -= OnRetryRequested;
        }

        private void RestoreSerializedRuntimeDependencies()
        {
            if (session == null || city == null || presentation == null ||
                menu == null)
                return;
            deploymentRequest = new CityDeploymentRequestAdapter(city);
            evacuationPresentation = presentation;
        }

        private void CleanupController(bool clearSerializedDependencies)
        {
            GrayboxBuildingSession3D oldSession = session;
            GrayboxBuildingMenuView3D oldMenu = menu;
            bool releaseOldMenu = ownsConstructionCancellation;
            UnsubscribeMenu();
            try
            {
                if (oldSession != null && rollbackWork.Count > 0)
                    RollbackCleanupWork(oldSession);
            }
            finally
            {
                if (oldMenu != null && releaseOldMenu)
                    ReleaseCleanupMenu(oldMenu);
                ResetLocalState();
                deploymentRequest = null;
                evacuationPresentation = null;
                if (clearSerializedDependencies)
                {
                    session = null;
                    city = null;
                    presentation = null;
                    menu = null;
                    productionRuntime = null;
                    defenseRuntime = null;
                }
            }
        }

        private void RollbackCleanupWork(
            GrayboxBuildingSession3D oldSession)
        {
            cleanupRollbackInvocationCount++;
            cleanupRollbackSnapshot.Clear();
            for (var index = 0; index < rollbackWork.Count; index++)
                cleanupRollbackSnapshot.Add(rollbackWork[index]);
            oldSession.RollbackEvacuationLocksAfterFailure(rollbackWork);
        }

        private void ReleaseCleanupMenu(GrayboxBuildingMenuView3D oldMenu)
        {
            cleanupMenuReleaseInvocationCount++;
            oldMenu.HideEvacuation();
            oldMenu.SetConstructionCancellationBlocked(false);
        }

        private enum CommitCurrentResult
        {
            Succeeded,
            Blocked,
            Failed
        }

        private sealed class RuntimePayloadCapture
        {
            private RuntimePayloadCapture(
                GrayboxProductionEvacuationPayload3D production,
                GrayboxDefenseEvacuationPayload3D defense,
                IReadOnlyList<ResourceAmount> resources)
            {
                Production = production;
                Defense = defense;
                Resources = resources;
            }

            public GrayboxProductionEvacuationPayload3D Production { get; }
            public GrayboxDefenseEvacuationPayload3D Defense { get; }
            public IReadOnlyList<ResourceAmount> Resources { get; }

            public static RuntimePayloadCapture Create(
                GrayboxProductionEvacuationPayload3D production,
                GrayboxDefenseEvacuationPayload3D defense)
            {
                var totals = new Dictionary<string, int>(
                    StringComparer.Ordinal);
                if (production != null)
                {
                    Add(totals, production.Input);
                    Add(totals, production.ReservedInput);
                    Add(totals, production.Output);
                }
                if (defense != null && defense.AmmunitionAmount > 0)
                {
                    Add(
                        totals,
                        ResourceIds.Ammunition,
                        defense.AmmunitionAmount);
                }

                var resourceIds = new List<string>(totals.Keys);
                resourceIds.Sort(StringComparer.Ordinal);
                var resources = new List<ResourceAmount>(resourceIds.Count);
                for (var index = 0; index < resourceIds.Count; index++)
                {
                    string resourceId = resourceIds[index];
                    resources.Add(new ResourceAmount(
                        resourceId,
                        totals[resourceId]));
                }
                return new RuntimePayloadCapture(
                    production,
                    defense,
                    new ReadOnlyCollection<ResourceAmount>(resources));
            }

            private static void Add(
                IDictionary<string, int> totals,
                IReadOnlyList<ResourceAmount> amounts)
            {
                if (amounts == null) return;
                for (var index = 0; index < amounts.Count; index++)
                {
                    ResourceAmount amount = amounts[index];
                    Add(totals, amount.ResourceId, amount.Amount);
                }
            }

            private static void Add(
                IDictionary<string, int> totals,
                string resourceId,
                int amount)
            {
                if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
                    return;
                totals.TryGetValue(resourceId, out int current);
                long sum = (long)current + amount;
                totals[resourceId] = sum >= int.MaxValue
                    ? int.MaxValue
                    : (int)sum;
            }
        }

        private sealed class CityDeploymentRequestAdapter :
            IGrayboxDeploymentRequest3D
        {
            private readonly GrayboxMobileCityController3D city;

            public CityDeploymentRequestAdapter(
                GrayboxMobileCityController3D city)
            {
                this.city = city;
            }

            public WasteCity.City.CityMode Mode => city.Mode;

            public bool TryToggleDeployment(out string failureReason)
            {
                return city.TryToggleDeployment(out failureReason);
            }
        }
    }
}
