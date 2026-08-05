using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Graybox3D;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxEvacuationController3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;
        [SerializeField] private GrayboxBuildingMenuView3D menu;
        private IGrayboxBuildingPresentation3D evacuationPresentation;

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
        private readonly ReadOnlyCollection<BuildingEvacuationWork> readOnlyWork;
        private int fullQueueIndex;
        private float remainingSeconds;

        public GrayboxEvacuationController3D()
        {
            readOnlyWork = new ReadOnlyCollection<BuildingEvacuationWork>(work);
        }

        public bool IsManifestOpen { get; private set; }
        public bool IsProcessing { get; private set; }
        public IReadOnlyList<BuildingEvacuationWork> Work => readOnlyWork;

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

            UnsubscribeMenu();
            this.session = session;
            this.city = city;
            this.presentation = presentation;
            evacuationPresentation = presentation;
            this.menu = menu;
            ClearProcessingState();
            if (isActiveAndEnabled) SubscribeMenu();
        }

        public bool TryHandleDeploymentRequest()
        {
            if (!IsConfigured || IsManifestOpen || IsProcessing) return false;
            if (!session.HasPlayerOwnedGroundInstances)
                return city.TryToggleDeployment(out _);
            if (city.Mode != WasteCity.City.CityMode.Fortress) return false;

            session.CopyPlayerOwnedGroundInstances(manifest);
            assignments.Clear();
            work.Clear();
            fullQueue.Clear();
            rollbackWork.Clear();
            IsManifestOpen = true;
            menu.ShowEvacuation(manifest);
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
            return count;
        }

        public int AssignAll(BuildingEvacuationTreatment treatment)
        {
            if (!IsManifestOpen || IsProcessing ||
                treatment == BuildingEvacuationTreatment.Unassigned)
                return 0;
            for (var index = 0; index < manifest.Count; index++)
                assignments[manifest[index].StableInstanceId] = treatment;
            return manifest.Count;
        }

        public bool ConfirmManifest()
        {
            if (!IsManifestOpen || IsProcessing) return false;
            work.Clear();
            fullQueue.Clear();
            rollbackWork.Clear();
            for (var index = 0; index < manifest.Count; index++)
            {
                GrayboxBuildingInstance3D instance = manifest[index];
                if (!assignments.TryGetValue(instance.StableInstanceId,
                        out BuildingEvacuationTreatment treatment) ||
                    treatment == BuildingEvacuationTreatment.Unassigned)
                    return false;
                double remainingRatio = instance.State ==
                    GrayboxBuildingInstanceState.Completed
                    ? 1d
                    : instance.Progress.Remaining /
                      instance.Progress.BaseDuration;
                work.Add(BuildingEvacuationRules.Create(
                    instance.StableInstanceId,
                    instance.Placement.Definition.Cost,
                    instance.Progress.BaseDuration,
                    remainingRatio,
                    treatment));
            }
            IReadOnlyList<BuildingEvacuationWork> sorted =
                BuildingEvacuationRules.CreateStableFullDismantleQueue(work);
            if (!session.TryCaptureEvacuationWork(work, out _))
            {
                ClearWorkOnly();
                return false;
            }
            for (var index = 0; index < sorted.Count; index++)
                fullQueue.Add(sorted[index]);
            for (var index = 0; index < work.Count; index++)
                rollbackWork.Add(work[index]);
            if (fullQueue.Count > 0 &&
                !session.TryLockEvacuationWork(fullQueue, out _))
            {
                session.RollbackEvacuationLocksAfterFailure(rollbackWork);
                ClearWorkOnly();
                return false;
            }

            try
            {
                for (var index = 0; index < work.Count; index++)
                {
                    BuildingEvacuationWork item = work[index];
                    if (item.Treatment == BuildingEvacuationTreatment.FullDismantle)
                        continue;
                    if (!session.TryCommitEvacuation(
                            item, EvacuationPresentation, out _, out _))
                    {
                        FailProcessing();
                        return false;
                    }
                    rollbackWork.Remove(item);
                }
            }
            catch
            {
                FailProcessing();
                throw;
            }

            IsManifestOpen = false;
            menu.HideEvacuation();
            if (fullQueue.Count == 0)
                return FinishIfResolved();
            IsProcessing = true;
            fullQueueIndex = 0;
            remainingSeconds = fullQueue[0].DismantleSeconds;
            return true;
        }

        public void Tick(float unscaledDeltaTime, bool paused)
        {
            if (!IsProcessing || paused || unscaledDeltaTime <= 0f) return;
            remainingSeconds -= unscaledDeltaTime;
            if (remainingSeconds > 0f) return;
            BuildingEvacuationWork current = fullQueue[fullQueueIndex];
            try
            {
                if (!session.TryCommitEvacuation(
                        current, EvacuationPresentation, out _, out _))
                {
                    FailProcessing();
                    return;
                }
            }
            catch
            {
                FailProcessing();
                throw;
            }
            rollbackWork.Remove(current);
            fullQueueIndex++;
            if (fullQueueIndex < fullQueue.Count)
            {
                remainingSeconds = fullQueue[fullQueueIndex].DismantleSeconds;
                return;
            }
            IsProcessing = false;
            FinishIfResolved();
        }

        private bool FinishIfResolved()
        {
            if (session.HasPlayerOwnedGroundInstances) return true;
            bool packed = city.TryToggleDeployment(out _);
            if (packed) ClearProcessingState();
            return packed;
        }

        private void FailProcessing()
        {
            session.RollbackEvacuationLocksAfterFailure(rollbackWork);
            IsProcessing = false;
            IsManifestOpen = true;
            fullQueueIndex = 0;
            remainingSeconds = 0f;
            ClearWorkOnly();
            session.CopyPlayerOwnedGroundInstances(manifest);
            menu.ShowEvacuation(manifest);
        }

        private void ClearProcessingState()
        {
            IsManifestOpen = false;
            IsProcessing = false;
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
        }

        private bool IsConfigured =>
            session != null && city != null &&
            EvacuationPresentation != null && menu != null;

        private IGrayboxBuildingPresentation3D EvacuationPresentation =>
            evacuationPresentation ?? presentation;

        private void Update()
        {
            Tick(Time.deltaTime, Time.timeScale <= 0f);
        }

        private void OnEnable()
        {
            SubscribeMenu();
        }

        private void OnDisable()
        {
            UnsubscribeMenu();
        }

        private void OnDestroy()
        {
            UnsubscribeMenu();
            menu = null;
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
    }
}
