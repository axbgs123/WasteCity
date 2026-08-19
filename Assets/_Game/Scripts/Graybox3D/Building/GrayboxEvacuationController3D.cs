using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Graybox3D;

namespace WasteCity.Graybox3D.Building
{
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

        public GrayboxEvacuationController3D()
        {
            readOnlyWork = new ReadOnlyCollection<BuildingEvacuationWork>(work);
        }

        public bool IsManifestOpen { get; private set; }
        public bool IsProcessing { get; private set; }
        public bool IsBlocked => isBlocked;
        public string BlockedReason => blockedReason;
        public IReadOnlyList<BuildingEvacuationWork> Work => readOnlyWork;

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
            menu.ShowEvacuation(manifest);
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
                assignments[stableInstanceId] = treatment;
                RefreshManifestPreview();
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
            if (count > 0) RefreshManifestPreview();
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
            menu.HideEvacuation();
            fullQueueIndex = 0;
            remainingSeconds = 0f;
            return AdvanceThroughImmediateWork();
        }

        public void Tick(float unscaledDeltaTime, bool paused)
        {
            if (IsManifestOpen && !IsProcessing)
                RefreshManifestPreview();
            if (!IsProcessing || IsBlocked || paused ||
                unscaledDeltaTime <= 0f)
                return;
            remainingSeconds -= unscaledDeltaTime *
                Math.Max(0f, session.DevelopmentRuleTimeMultiplier);
            if (remainingSeconds > 0f) return;
            if (TryCommitCurrent() != CommitCurrentResult.Succeeded) return;
            AdvanceThroughImmediateWork();
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
                return false;
            AdvanceThroughImmediateWork();
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
            menu.ShowEvacuation(manifest);
        }

        private void ResetLocalState()
        {
            IsManifestOpen = false;
            IsProcessing = false;
            isBlocked = false;
            blockedReason = string.Empty;
            ownsConstructionCancellation = false;
            manifest.Clear();
            assignments.Clear();
            ClearWorkOnly();
            fullQueueIndex = 0;
            remainingSeconds = 0f;
        }

        private void ClearWorkOnly()
        {
            work.Clear();
            fullQueue.Clear();
            rollbackWork.Clear();
            runtimePayloads.Clear();
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

        private void SubscribeMenu()
        {
            if (ReferenceEquals(menu, null)) return;
            UnsubscribeMenu();
            menu.EvacuationItemTreatmentRequested += OnItemTreatmentRequested;
            menu.EvacuationCategoryTreatmentRequested += OnCategoryTreatmentRequested;
            menu.EvacuationAllTreatmentRequested += OnAllTreatmentRequested;
            menu.EvacuationConfirmationRequested += OnConfirmationRequested;
        }

        private void UnsubscribeMenu()
        {
            if (ReferenceEquals(menu, null)) return;
            menu.EvacuationItemTreatmentRequested -= OnItemTreatmentRequested;
            menu.EvacuationCategoryTreatmentRequested -= OnCategoryTreatmentRequested;
            menu.EvacuationAllTreatmentRequested -= OnAllTreatmentRequested;
            menu.EvacuationConfirmationRequested -= OnConfirmationRequested;
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
