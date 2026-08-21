using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Persistence;

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
        public const string CapacityBlockedCode = "capacity-insufficient";
        private static readonly ProfilerMarker TickMarker =
            new ProfilerMarker("WasteCity.Formal.Evacuation.Tick");
        private static readonly ProfilerMarker ManifestViewBuildMarker =
            new ProfilerMarker(
                "WasteCity.Formal.Evacuation.ManifestView.Build");
        private static readonly ProfilerMarker CapacityPreflightMarker =
            new ProfilerMarker(
                "WasteCity.Formal.Evacuation.CapacityPreflight");
        private static readonly ProfilerMarker CommitMarker =
            new ProfilerMarker("WasteCity.Formal.Evacuation.Commit");

        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;
        [SerializeField] private GrayboxBuildingMenuView3D menu;
        [SerializeField] private GrayboxProductionController3D production;
        [SerializeField] private GrayboxDefenseController3D defense;
        private IGrayboxBuildingPresentation3D evacuationPresentation;
        private IGrayboxDeploymentRequest3D deploymentRequest;
        private GrayboxProductionRuntime3D productionRuntime;
        private GrayboxDefenseRuntime3D defenseRuntime;
        private Func<int> aliveEnemyCountSource;
        private Func<bool> persistencePauseSource;
        private GrayboxMobileCityController3D aliveEnemyCountSourceCity;

        private readonly List<GrayboxBuildingInstance3D> manifest =
            new List<GrayboxBuildingInstance3D>();
        private readonly Dictionary<string, BuildingEvacuationTreatment> assignments =
            new Dictionary<string, BuildingEvacuationTreatment>(StringComparer.Ordinal);
        private readonly List<BuildingEvacuationWork> work =
            new List<BuildingEvacuationWork>();
        private readonly Dictionary<string, BuildingEvacuationWork> workById =
            new Dictionary<string, BuildingEvacuationWork>(
                StringComparer.Ordinal);
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
        private string blockedCode = string.Empty;
        private string blockedReason = string.Empty;
        private int fullQueueIndex;
        private float remainingSeconds;
        private bool queuePaused;
        private ulong nextViewRevision;
        private ulong nextBatchOrdinal;
        private string activeBatchId = string.Empty;
        private ulong persistenceGeneration;
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
        public bool IsPersistencePaused =>
            persistencePauseSource != null && persistencePauseSource();

        public event Action<string, string> CheckpointCommitted;
        public string BlockedReason => blockedReason;
        public IReadOnlyList<BuildingEvacuationWork> Work => readOnlyWork;

        public EvacuationManifestViewModel CaptureManifestView()
        {
            EnsureOperationalRuntimeBindings();
            if (IsManifestOpen && !IsProcessing)
                RefreshManifestPreview();
            ulong signature = ManifestViewSignature();
            if (hasManifestSignature &&
                signature == cachedManifestSignature &&
                cachedManifestView != null)
            {
                return cachedManifestView;
            }

            using (ManifestViewBuildMarker.Auto())
            {
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
            nextBatchOrdinal = 1;
            InvalidateViewCaches();
            menu.SetConstructionCancellationBlocked(false);
            EnsureOperationalRuntimeBindings();
            if (isActiveAndEnabled) SubscribeMenu();
        }

        public void ConfigurePersistencePauseSource(Func<bool> pauseSource)
        {
            persistencePauseSource = pauseSource;
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
            blockedCode = string.Empty;
            blockedReason = string.Empty;
            activeBatchId = "evacuation.batch." +
                nextBatchOrdinal.ToString("D6", CultureInfo.InvariantCulture);
            unchecked { nextBatchOrdinal++; }
            fullQueueIndex = 0;
            remainingSeconds = 0f;
            CheckpointCommitted?.Invoke(
                FormalSaveCheckpointReasonIds.EvacuationBatchConfirmed,
                activeBatchId + "|confirmed");
            bool advanced = AdvanceThroughImmediateWork();
            persistenceGeneration++;
            if (IsProcessing)
                menu.ShowEvacuationQueue(CaptureQueueView());
            return advanced;
        }

        public void Tick(float unscaledDeltaTime, bool paused)
        {
            using (TickMarker.Auto())
            {
                bool effectivePaused = paused || IsPersistencePaused;
                queuePaused = effectivePaused;
                if (IsManifestOpen && !IsProcessing)
                    menu.ShowEvacuationManifest(CaptureManifestView());
                if (!IsProcessing || IsBlocked || effectivePaused ||
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
                persistenceGeneration++;
                if (IsProcessing)
                    menu.ShowEvacuationQueue(CaptureQueueView());
            }
        }

        public bool TryRebuildAfterPersistenceRestore(out string error)
        {
            if (!IsConfigured)
            {
                error = "正式撤离运行时尚未完成恢复后重建配置";
                return false;
            }

            queuePaused = IsPersistencePaused;
            InvalidateViewCaches();
            menu.SetConstructionCancellationBlocked(IsProcessing);
            if (IsManifestOpen && !IsProcessing)
                menu.ShowEvacuationManifest(CaptureManifestView());
            else if (IsProcessing)
                menu.ShowEvacuationQueue(CaptureQueueView());
            else
                menu.HideEvacuation();
            error = string.Empty;
            return true;
        }

        public bool RetryBlockedWork()
        {
            if (!IsProcessing || !IsBlocked ||
                fullQueueIndex < 0 || fullQueueIndex >= fullQueue.Count)
                return false;
            persistenceGeneration++;
            isBlocked = false;
            blockedCode = string.Empty;
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
            EnsureOperationalRuntimeBindings();
            bool isInCombat = defenseRuntime != null &&
                defenseRuntime.Snapshot.AliveEnemyCount > 0;
            return BuildingEvacuationRules.CreateBatchContext(
                isInCombat,
                session.ProductivityMultiplier);
        }

        private void BuildWork(EvacuationBatchContext context)
        {
            work.Clear();
            workById.Clear();
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
                BuildingEvacuationWork item = BuildingEvacuationRules.Create(
                    instance.StableInstanceId,
                    instance.Placement.Definition.Cost,
                    instance.Progress.BaseDuration,
                    remainingRatio,
                    treatment,
                    context);
                work.Add(item);
                workById.Add(item.StableInstanceId, item);
            }
            work.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
        }

        private void CaptureRuntimePayloads()
        {
            EnsureOperationalRuntimeBindings();
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
                    persistenceGeneration++;
                    return true;
                }
                CommitCurrentResult result = TryCommitCurrent();
                if (result == CommitCurrentResult.Succeeded)
                    continue;
                return result == CommitCurrentResult.Blocked;
            }

            if (!IsProcessing) return false;
            IsProcessing = false;
            persistenceGeneration++;
            return FinishIfResolved();
        }

        private CommitCurrentResult TryCommitCurrent()
        {
            using (CommitMarker.Auto())
                return TryCommitCurrentCore();
        }

        private CommitCurrentResult TryCommitCurrentCore()
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
                    blockedCode = CapacityBlockedCode;
                    blockedReason = failureReason;
                    remainingSeconds = 0f;
                    persistenceGeneration++;
                    CheckpointCommitted?.Invoke(
                        FormalSaveCheckpointReasonIds.EvacuationWorkCommitted,
                        WorkCheckpointIdentity(
                            current.StableInstanceId,
                            "capacity-blocked"));
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
            blockedCode = string.Empty;
            blockedReason = string.Empty;
            fullQueueIndex++;
            remainingSeconds = 0f;
            persistenceGeneration++;
            CheckpointCommitted?.Invoke(
                FormalSaveCheckpointReasonIds.EvacuationWorkCommitted,
                WorkCheckpointIdentity(
                    current.StableInstanceId,
                    "committed"));
            return CommitCurrentResult.Succeeded;
        }

        private string WorkCheckpointIdentity(
            string stableInstanceId,
            string boundary)
        {
            return activeBatchId + "|work|" + stableInstanceId + "|" +
                boundary;
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
            blockedCode = string.Empty;
            blockedReason = string.Empty;
            IsManifestOpen = true;
            fullQueueIndex = 0;
            remainingSeconds = 0f;
            ClearWorkOnly();
            persistenceGeneration++;
            session.CopyPlayerOwnedGroundInstances(manifest);
            menu.SetConstructionCancellationBlocked(true);
            menu.ShowEvacuationManifest(CaptureManifestView());
        }

        private void ResetLocalState()
        {
            IsManifestOpen = false;
            IsProcessing = false;
            isBlocked = false;
            blockedCode = string.Empty;
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
            persistenceGeneration++;
        }

        private void ClearWorkOnly()
        {
            work.Clear();
            workById.Clear();
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
            result = default;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                workById.TryGetValue(stableInstanceId, out result);
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
            using (CapacityPreflightMarker.Auto())
                return CreatePreviewStoragePlanCore(
                    instance,
                    itemWork,
                    payload);
        }

        private CityResourceEvacuationPlan CreatePreviewStoragePlanCore(
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
            if (productionRuntime != null)
                productionRuntime.TryGetState(
                    stableInstanceId,
                    out productionState);

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
            if (defenseRuntime != null)
                defenseRuntime.TryGetTowerState(
                    stableInstanceId,
                    out defenseTower);

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
            UnbindAliveEnemyCountSource();
            CleanupController(false);
        }

        private void OnDestroy()
        {
            CleanupController(true);
            persistencePauseSource = null;
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
            if (nextBatchOrdinal == 0)
                nextBatchOrdinal = 1;
            EnsureOperationalRuntimeBindings();
            if (session == null || city == null || presentation == null ||
                menu == null)
                return;
            deploymentRequest = new CityDeploymentRequestAdapter(city);
            evacuationPresentation = presentation;
        }

        private void EnsureOperationalRuntimeBindings()
        {
            if (productionRuntime == null && production != null)
                productionRuntime = production.Clock.Runtime;
            if (defenseRuntime == null && defense != null)
                defenseRuntime = defense.Runtime;
            BindAliveEnemyCountSource();
        }

        private void BindAliveEnemyCountSource()
        {
            if (city == null || defense == null ||
                ReferenceEquals(aliveEnemyCountSourceCity, city))
                return;
            if (aliveEnemyCountSource == null)
                aliveEnemyCountSource = CaptureAliveEnemyCount;
            if (aliveEnemyCountSourceCity != null)
                aliveEnemyCountSourceCity.ConfigureAliveEnemyCountSource(null);
            city.ConfigureAliveEnemyCountSource(aliveEnemyCountSource);
            aliveEnemyCountSourceCity = city;
        }

        private int CaptureAliveEnemyCount()
        {
            return Math.Max(0, defense?.Snapshot?.AliveEnemyCount ?? 0);
        }

        private void UnbindAliveEnemyCountSource()
        {
            if (aliveEnemyCountSourceCity == null) return;
            aliveEnemyCountSourceCity.ConfigureAliveEnemyCountSource(null);
            aliveEnemyCountSourceCity = null;
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
                    UnbindAliveEnemyCountSource();
                    session = null;
                    city = null;
                    presentation = null;
                    menu = null;
                    productionRuntime = null;
                    defenseRuntime = null;
                }
            }
        }

        public GrayboxEvacuationPersistenceState3D CaptureForPersistence()
        {
            if (!IsProcessing)
            {
                return new GrayboxEvacuationPersistenceState3D(
                    (long)nextBatchOrdinal,
                    string.Empty,
                    false,
                    default,
                    Array.Empty<BuildingEvacuationWork>(),
                    Array.Empty<string>(),
                    0,
                    string.Empty,
                    0f,
                    false,
                    string.Empty,
                    string.Empty,
                    Array.Empty<GrayboxEvacuationPayloadPersistenceState3D>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            var queueIds = new string[fullQueue.Count];
            for (var index = 0; index < fullQueue.Count; index++)
                queueIds[index] = fullQueue[index].StableInstanceId;
            var pendingIds = new string[fullQueue.Count - fullQueueIndex];
            for (var index = fullQueueIndex; index < fullQueue.Count; index++)
                pendingIds[index - fullQueueIndex] =
                    fullQueue[index].StableInstanceId;
            var payloadIds = new List<string>(runtimePayloads.Keys);
            payloadIds.Sort(StringComparer.Ordinal);
            var payloadStates =
                new GrayboxEvacuationPayloadPersistenceState3D[
                    payloadIds.Count];
            for (var index = 0; index < payloadIds.Count; index++)
            {
                RuntimePayloadCapture payload =
                    runtimePayloads[payloadIds[index]];
                payloadStates[index] = payload.CaptureForPersistence(
                    payloadIds[index]);
            }
            BuildingEvacuationWork current = fullQueue[fullQueueIndex];
            return new GrayboxEvacuationPersistenceState3D(
                (long)nextBatchOrdinal,
                activeBatchId,
                true,
                work[0].BatchContext,
                work,
                queueIds,
                fullQueueIndex,
                current.StableInstanceId,
                remainingSeconds,
                isBlocked,
                blockedCode,
                isBlocked ? current.StableInstanceId : string.Empty,
                payloadStates,
                pendingIds,
                pendingIds);
        }

        public bool TryPrepareRestore(
            GrayboxEvacuationPersistenceState3D state,
            out GrayboxEvacuationRestorePlan3D plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (!IsConfigured || state == null)
            {
                error = "撤离控制器或持久状态未就绪";
                return false;
            }
            if ((IsProcessing || IsManifestOpen) &&
                !IsPersistencePaused)
            {
                error = "撤离控制器已有活动事务";
                return false;
            }
            if (state.NextBatchOrdinal <= 0 ||
                float.IsNaN(state.RemainingSeconds) ||
                float.IsInfinity(state.RemainingSeconds) ||
                state.RemainingSeconds < 0f)
            {
                error = "撤离高水位或剩余时间无效";
                return false;
            }
            if (!state.IsProcessing)
            {
                if (!string.IsNullOrEmpty(state.ActiveBatchId) ||
                    state.Work.Count != 0 ||
                    state.FullQueueStableInstanceIds.Count != 0 ||
                    state.Payloads.Count != 0 ||
                    state.LockedStableInstanceIds.Count != 0 ||
                    state.PendingRollbackStableInstanceIds.Count != 0 ||
                    state.IsBlocked)
                {
                    error = "未处理撤离状态包含活动事务";
                    return false;
                }
                var emptyWork = Array.Empty<BuildingEvacuationWork>();
                var emptyIds = Array.Empty<string>();
                if (!session.TryPrepareEvacuationRestore(
                        emptyWork,
                        emptyIds,
                        emptyIds,
                        out GrayboxBuildingEvacuationRestorePlan3D emptySessionPlan,
                        out error))
                {
                    return false;
                }
                plan = new GrayboxEvacuationRestorePlan3D(
                    this,
                    persistenceGeneration,
                    state,
                    emptyWork,
                    emptyWork,
                    emptyWork,
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    emptySessionPlan);
                return true;
            }

            if (state.Work.Count == 0 ||
                state.Work.Count != state.FullQueueStableInstanceIds.Count ||
                state.CurrentQueueIndex < 0 ||
                state.CurrentQueueIndex >=
                    state.FullQueueStableInstanceIds.Count ||
                !string.Equals(
                    state.CurrentStableInstanceId,
                    state.FullQueueStableInstanceIds[state.CurrentQueueIndex],
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(state.ActiveBatchId))
            {
                error = "撤离批次、队列或当前项目无效";
                return false;
            }
            const string batchPrefix = "evacuation.batch.";
            if (!state.ActiveBatchId.StartsWith(
                    batchPrefix,
                    StringComparison.Ordinal) ||
                !long.TryParse(
                    state.ActiveBatchId.Substring(batchPrefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long activeBatchOrdinal) ||
                activeBatchOrdinal <= 0 ||
                state.NextBatchOrdinal <= activeBatchOrdinal)
            {
                error = "撤离批次 ID 或下一序号高水位无效";
                return false;
            }

            var workByStableId =
                new Dictionary<string, BuildingEvacuationWork>(
                    StringComparer.Ordinal);
            var preparedWork = new BuildingEvacuationWork[state.Work.Count];
            for (var index = 0; index < state.Work.Count; index++)
            {
                BuildingEvacuationWork item = state.Work[index];
                if (string.IsNullOrWhiteSpace(item.StableInstanceId) ||
                    item.Treatment == BuildingEvacuationTreatment.Unassigned ||
                    !Enum.IsDefined(
                        typeof(BuildingEvacuationTreatment),
                        item.Treatment) ||
                    !workByStableId.TryAdd(item.StableInstanceId, item) ||
                    !item.BatchContext.Equals(state.BatchContext))
                {
                    error = "冻结撤离项目为空、重复或上下文不一致";
                    return false;
                }
                if (index > 0 && string.CompareOrdinal(
                        state.Work[index - 1].StableInstanceId,
                        item.StableInstanceId) >= 0)
                {
                    error = "冻结撤离项目必须按稳定 ID 排序";
                    return false;
                }
                preparedWork[index] = item;
            }

            var preparedQueue =
                new BuildingEvacuationWork[state.FullQueueStableInstanceIds.Count];
            var queueIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 index < state.FullQueueStableInstanceIds.Count;
                 index++)
            {
                string stableId = state.FullQueueStableInstanceIds[index];
                if (!queueIds.Add(stableId) ||
                    !workByStableId.TryGetValue(
                        stableId,
                        out preparedQueue[index]))
                {
                    error = "撤离执行队列重复或缺少冻结项目";
                    return false;
                }
            }
            var expectedQueue = new List<string>(state.Work.Count);
            for (var index = 0; index < state.Work.Count; index++)
                if (state.Work[index].Treatment !=
                    BuildingEvacuationTreatment.FullDismantle)
                    expectedQueue.Add(state.Work[index].StableInstanceId);
            for (var index = 0; index < state.Work.Count; index++)
                if (state.Work[index].Treatment ==
                    BuildingEvacuationTreatment.FullDismantle)
                    expectedQueue.Add(state.Work[index].StableInstanceId);
            for (var index = 0; index < expectedQueue.Count; index++)
            {
                if (!string.Equals(
                        expectedQueue[index],
                        state.FullQueueStableInstanceIds[index],
                        StringComparison.Ordinal))
                {
                    error = "撤离执行队列不符合冻结稳定顺序";
                    return false;
                }
            }

            int pendingCount =
                state.FullQueueStableInstanceIds.Count -
                state.CurrentQueueIndex;
            if (state.PendingRollbackStableInstanceIds.Count != pendingCount)
            {
                error = "待回滚撤离项目必须等于未提交队列后缀";
                return false;
            }
            for (var index = 0; index < pendingCount; index++)
            {
                if (!string.Equals(
                        state.PendingRollbackStableInstanceIds[index],
                        state.FullQueueStableInstanceIds[
                            state.CurrentQueueIndex + index],
                        StringComparison.Ordinal))
                {
                    error = "待回滚撤离项目顺序与未提交队列不一致";
                    return false;
                }
            }

            var rollbackById = new Dictionary<string, BuildingEvacuationWork>(
                StringComparer.Ordinal);
            var preparedRollback = new BuildingEvacuationWork[
                state.PendingRollbackStableInstanceIds.Count];
            for (var index = 0;
                 index < state.PendingRollbackStableInstanceIds.Count;
                 index++)
            {
                string stableId =
                    state.PendingRollbackStableInstanceIds[index];
                if (!workByStableId.TryGetValue(
                        stableId,
                        out BuildingEvacuationWork item) ||
                    !rollbackById.TryAdd(stableId, item))
                {
                    error = "待回滚撤离项目重复或缺少冻结项目";
                    return false;
                }
                preparedRollback[index] = item;
            }

            var preparedPayloads = new Dictionary<string, object>(
                StringComparer.Ordinal);
            for (var index = 0; index < state.Payloads.Count; index++)
            {
                GrayboxEvacuationPayloadPersistenceState3D saved =
                    state.Payloads[index];
                if (saved == null ||
                    !rollbackById.ContainsKey(saved.StableInstanceId) ||
                    preparedPayloads.ContainsKey(saved.StableInstanceId) ||
                    !TryPrepareRuntimePayload(
                        saved,
                        out RuntimePayloadCapture payload,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "撤离运行时载荷重复或不属于未提交项目";
                    return false;
                }
                preparedPayloads.Add(saved.StableInstanceId, payload);
            }
            for (var index = 0; index < preparedRollback.Length; index++)
            {
                string stableId = preparedRollback[index].StableInstanceId;
                bool hasProductionOwner = productionRuntime != null &&
                    productionRuntime.TryGetState(stableId, out _);
                bool hasDefenseOwner = defenseRuntime != null &&
                    defenseRuntime.TryGetTowerState(stableId, out _);
                bool hasPayload = preparedPayloads.TryGetValue(
                    stableId,
                    out object savedPayload);
                RuntimePayloadCapture capture = hasPayload
                    ? (RuntimePayloadCapture)savedPayload
                    : null;
                if ((hasProductionOwner || hasDefenseOwner) && !hasPayload)
                {
                    error = "撤离运行时所有者缺少持久载荷";
                    return false;
                }
                if (hasPayload &&
                    (hasProductionOwner != (capture.Production != null) ||
                     hasDefenseOwner != (capture.Defense != null)))
                {
                    error = "撤离运行时载荷与生产、防御所有者不一致";
                    return false;
                }
                if (hasPayload && !hasProductionOwner &&
                    !hasDefenseOwner && capture.Resources.Count > 0 &&
                    (!TryFindSessionInstance(stableId, out var instance) ||
                     IsKnownBuildingDefinition(instance)))
                {
                    error = "普通建筑不能恢复无所有者资源载荷";
                    return false;
                }
            }
            if (state.IsBlocked &&
                (!string.Equals(
                    state.BlockedCode,
                    CapacityBlockedCode,
                    StringComparison.Ordinal) ||
                 !string.Equals(
                    state.BlockedStableInstanceId,
                    state.CurrentStableInstanceId,
                    StringComparison.Ordinal) ||
                 state.RemainingSeconds != 0f))
            {
                error = "撤离容量阻塞状态无效";
                return false;
            }
            if (!state.IsBlocked &&
                (!string.IsNullOrEmpty(state.BlockedCode) ||
                 !string.IsNullOrEmpty(state.BlockedStableInstanceId)))
            {
                error = "未阻塞撤离状态含有阻塞身份";
                return false;
            }

            if (!session.TryPrepareEvacuationRestore(
                    preparedWork,
                    state.LockedStableInstanceIds,
                    state.PendingRollbackStableInstanceIds,
                    out GrayboxBuildingEvacuationRestorePlan3D sessionPlan,
                    out error))
            {
                return false;
            }
            plan = new GrayboxEvacuationRestorePlan3D(
                this,
                persistenceGeneration,
                state,
                preparedWork,
                preparedQueue,
                preparedRollback,
                preparedPayloads,
                sessionPlan);
            return true;
        }

        public bool TryCommitRestore(
            GrayboxEvacuationRestorePlan3D plan,
            out string error)
        {
            error = string.Empty;
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "撤离恢复计划不属于当前控制器";
                return false;
            }
            if (plan.Consumed)
            {
                error = "撤离恢复计划已经使用";
                return false;
            }
            if (plan.ExpectedGeneration != persistenceGeneration ||
                ((IsProcessing || IsManifestOpen) &&
                 !IsPersistencePaused))
            {
                error = "撤离控制器在提交恢复前发生变化";
                return false;
            }
            if (plan.SessionPlan != null &&
                !session.TryCommitEvacuationRestore(
                    plan.SessionPlan,
                    out error))
            {
                return false;
            }

            ClearWorkOnly();
            for (var index = 0; index < plan.Work.Length; index++)
            {
                work.Add(plan.Work[index]);
                workById.Add(
                    plan.Work[index].StableInstanceId,
                    plan.Work[index]);
            }
            fullQueue.AddRange(plan.FullQueue);
            rollbackWork.AddRange(plan.RollbackWork);
            foreach (KeyValuePair<string, object> item in plan.Payloads)
                runtimePayloads.Add(
                    item.Key,
                    (RuntimePayloadCapture)item.Value);

            GrayboxEvacuationPersistenceState3D state = plan.State;
            nextBatchOrdinal = (ulong)state.NextBatchOrdinal;
            activeBatchId = state.ActiveBatchId;
            IsProcessing = state.IsProcessing;
            fullQueueIndex = state.CurrentQueueIndex;
            remainingSeconds = state.RemainingSeconds;
            isBlocked = state.IsBlocked;
            blockedCode = state.BlockedCode;
            blockedReason = isBlocked ? "城市仓储容量不足" : string.Empty;
            IsManifestOpen = false;
            ownsConstructionCancellation = state.IsProcessing;
            queuePaused = false;
            InvalidateViewCaches();
            menu.SetConstructionCancellationBlocked(state.IsProcessing);
            if (state.IsProcessing)
                menu.ShowEvacuationQueue(CaptureQueueView());
            else
                menu.HideEvacuation();
            plan.Consumed = true;
            persistenceGeneration++;
            return true;
        }

        public bool TryRestore(
            GrayboxEvacuationPersistenceState3D state,
            out string error)
        {
            return TryPrepareRestore(state, out var plan, out error) &&
                   TryCommitRestore(plan, out error);
        }

        private bool TryPrepareRuntimePayload(
            GrayboxEvacuationPayloadPersistenceState3D saved,
            out RuntimePayloadCapture payload,
            out string error)
        {
            payload = null;
            error = string.Empty;
            bool isMissingDefinitionPlaceholder =
                TryFindSessionInstance(
                    saved.StableInstanceId,
                    out GrayboxBuildingInstance3D instance) &&
                !IsKnownBuildingDefinition(instance);
            if (saved.TowerAmmunitionAmount < 0)
            {
                error = "撤离防御载荷弹药不能为负数";
                return false;
            }

            GrayboxProductionEvacuationPayload3D productionPayload = null;
            if (productionRuntime != null &&
                productionRuntime.TryGetState(
                    saved.StableInstanceId,
                    out BuildingProductionState productionState))
            {
                ResourceAmount[] input = CopyAmounts(saved.ProductionInput);
                ResourceAmount[] reserved = CopyAmounts(
                    saved.ProductionReservedInput);
                ResourceAmount[] output = CopyAmounts(saved.ProductionOutput);
                if (!AmountsEqual(
                        productionState.Input.CapturePositiveAmounts(),
                        input) ||
                    !AmountsEqual(productionState.ReservedInputs, reserved) ||
                    !AmountsEqual(
                        productionState.Output.CapturePositiveAmounts(),
                        output))
                {
                    error = "撤离生产载荷与已恢复生产状态不一致";
                    return false;
                }
                productionPayload = new GrayboxProductionEvacuationPayload3D(
                    productionState,
                    input,
                    reserved,
                    output);
            }
            else if (saved.ProductionInput.Count != 0 ||
                     saved.ProductionReservedInput.Count != 0 ||
                     saved.ProductionOutput.Count != 0)
            {
                if (!isMissingDefinitionPlaceholder)
                {
                    error = "撤离生产载荷缺少已恢复生产状态";
                    return false;
                }
            }

            GrayboxDefenseEvacuationPayload3D defensePayload = null;
            if (saved.HasDefensePayload)
            {
                GrayboxDefenseTowerRuntimeState3D tower = null;
                bool hasDefenseOwner = defenseRuntime != null &&
                    defenseRuntime.TryGetTowerState(
                        saved.StableInstanceId,
                        out tower);
                if (hasDefenseOwner &&
                    tower.Combat.Ammo != saved.TowerAmmunitionAmount)
                {
                    error = "撤离防御载荷与已恢复炮塔状态不一致";
                    return false;
                }
                if (hasDefenseOwner)
                {
                    defensePayload =
                        new GrayboxDefenseEvacuationPayload3D(tower);
                }
                else if (!isMissingDefinitionPlaceholder)
                {
                    error = "撤离防御载荷缺少已恢复炮塔状态";
                    return false;
                }
            }
            else if (saved.TowerAmmunitionAmount != 0)
            {
                error = "无防御载荷时炮塔弹药必须为零";
                return false;
            }

            payload = RuntimePayloadCapture.CreateForRestore(
                productionPayload,
                defensePayload,
                saved.Resources);
            bool hasProductionDetails = saved.ProductionInput.Count != 0 ||
                saved.ProductionReservedInput.Count != 0 ||
                saved.ProductionOutput.Count != 0;
            if ((productionPayload != null || hasProductionDetails ||
                 saved.HasDefensePayload) &&
                !AggregatePayloadMatches(saved))
            {
                error = "撤离聚合资源载荷与生产、防御分项不一致";
                payload = null;
                return false;
            }
            return true;
        }

        private bool TryFindSessionInstance(
            string stableInstanceId,
            out GrayboxBuildingInstance3D instance)
        {
            IReadOnlyList<GrayboxBuildingInstance3D> instances =
                session.Instances;
            for (var index = 0; index < instances.Count; index++)
            {
                if (!string.Equals(
                        instances[index].StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                    continue;
                instance = instances[index];
                return true;
            }
            instance = null;
            return false;
        }

        private static bool IsKnownBuildingDefinition(
            GrayboxBuildingInstance3D instance)
        {
            string definitionId = instance.Placement.Definition.Id.Value;
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                if (string.Equals(
                        BuildingCatalog.All[index].Id.Value,
                        definitionId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool AggregatePayloadMatches(
            GrayboxEvacuationPayloadPersistenceState3D saved)
        {
            var expected = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            AddAmounts(expected, saved.ProductionInput);
            AddAmounts(expected, saved.ProductionReservedInput);
            AddAmounts(expected, saved.ProductionOutput);
            if (saved.HasDefensePayload && saved.TowerAmmunitionAmount > 0)
            {
                AddAmounts(
                    expected,
                    new[]
                    {
                        new ResourceAmount(
                            ResourceIds.Ammunition,
                            saved.TowerAmmunitionAmount)
                    });
            }
            IReadOnlyList<ResourceAmount> actual = saved.Resources;
            if (actual.Count != expected.Count) return false;
            var index = 0;
            foreach (KeyValuePair<string, int> item in expected)
            {
                if (!string.Equals(
                        actual[index].ResourceId,
                        item.Key,
                        StringComparison.Ordinal) ||
                    actual[index].Amount != item.Value)
                {
                    return false;
                }
                index++;
            }
            return true;
        }

        private static ResourceAmount[] CopyAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ResourceAmount>();
            var result = new ResourceAmount[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }

        private static bool AmountsEqual(
            IReadOnlyList<ResourceAmount> left,
            IReadOnlyList<ResourceAmount> right)
        {
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                if (!string.Equals(
                        left[index].ResourceId,
                        right[index].ResourceId,
                        StringComparison.Ordinal) ||
                    left[index].Amount != right[index].Amount)
                {
                    return false;
                }
            }
            return true;
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
            internal RuntimePayloadCapture(
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

            public GrayboxEvacuationPayloadPersistenceState3D
                CaptureForPersistence(string stableInstanceId)
            {
                return new GrayboxEvacuationPayloadPersistenceState3D(
                    stableInstanceId,
                    Production?.Input,
                    Production?.ReservedInput,
                    Production?.Output,
                    Defense != null,
                    Defense?.AmmunitionAmount ?? 0,
                    Resources);
            }

            public static RuntimePayloadCapture CreateForRestore(
                GrayboxProductionEvacuationPayload3D production,
                GrayboxDefenseEvacuationPayload3D defense,
                IReadOnlyList<ResourceAmount> resources)
            {
                return new RuntimePayloadCapture(
                    production,
                    defense,
                    EvacuationManifestItemViewModel.CopyAmounts(resources));
            }

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
