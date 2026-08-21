using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Core;
using WasteCity.Persistence;

namespace WasteCity.Graybox3D.Building
{
    internal sealed class GrayboxFormalSaveWriteIntentLatch3D
    {
        public FormalSaveWriteIntent Intent { get; private set; } =
            FormalSaveWriteIntent.ContinueProgress;
        public bool ArchiveLegacy2D { get; private set; }

        public void BeginNewProgress(bool archiveLegacy2D)
        {
            Intent = FormalSaveWriteIntent.StartNewProgress;
            ArchiveLegacy2D = archiveLegacy2D;
        }

        public void CompleteWrite(bool succeeded)
        {
            if (!succeeded) return;
            Intent = FormalSaveWriteIntent.ContinueProgress;
            ArchiveLegacy2D = false;
        }

        public void AdoptContinuedProgress()
        {
            Intent = FormalSaveWriteIntent.ContinueProgress;
            ArchiveLegacy2D = false;
        }
    }

    public sealed class GrayboxFormalSaveRuntimeHost3D : MonoBehaviour
    {
        private const string FallbackGameVersion = "0.1.0";
        private const string SaveAndExitReasonId = "save-and-exit";
        private static string storeRootOverrideForTesting;

        [SerializeField] private GrayboxSceneBootstrap bootstrap;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxWorldView3D world;
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField]
        private GrayboxBuildingWorldView3D buildingPresentation;
        [SerializeField] private GrayboxOperationsController3D operations;
        [SerializeField] private GrayboxProductionController3D production;
        [SerializeField] private GrayboxDefenseController3D defense;
        [SerializeField] private GrayboxEvacuationController3D evacuation;

        private readonly GrayboxFormalSaveWriteIntentLatch3D writeIntent =
            new GrayboxFormalSaveWriteIntentLatch3D();
        private FormalSaveStore store;
        private GameSpeedModel speed;
        private GrayboxFormalSaveCoordinator3D coordinator;
        private FormalSaveCheckpointPolicy checkpointPolicy;
        private string currentSessionId = string.Empty;
        private bool automaticCheckpointFailureBlocked;
        private ulong automaticCheckpointFailureRevision;

        public GameSpeedModel Speed => speed ??= new GameSpeedModel();
        public bool IsInitialized { get; private set; }
        public FormalSaveStoreResult LastStoreResult { get; private set; }
        public GrayboxFormalSaveCoordinatorResult3D LastCoordinatorResult
        {
            get;
            private set;
        }
        public bool HasCheckpointWarning { get; private set; }

        public event Action<bool> CheckpointWarningChanged;

        public FormalSaveStoreResult Probe()
        {
            EnsureStore();
            LastStoreResult = store.Probe(FormalSavePayloadKind.Formal3D);
            return LastStoreResult;
        }

        public bool TryInitialize()
        {
            if (IsInitialized) return true;
            EnsureStore();
            _ = Speed;
            if (!HasAuthoredRuntimeReferences() ||
                !bootstrap.IsInitialized ||
                bootstrap.World == null ||
                world.Model == null ||
                session.Inventory == null ||
                session.CityStorage == null ||
                operations.Backpack == null ||
                operations.Crafting == null ||
                operations.Research == null)
            {
                return false;
            }

            if (!production.TryRebuildAfterPersistenceRestore(out _) ||
                !defense.TryRebuildAfterPersistenceRestore(out _))
            {
                return false;
            }

            try
            {
                evacuation.ConfigureOperationalRuntimes(
                    production.Clock.Runtime,
                    defense.Runtime);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            if (!evacuation.TryRebuildAfterPersistenceRestore(out _))
                return false;

            var worldCity = new GrayboxWorldCitySaveAdapter3D(
                bootstrap,
                city,
                session);
            var buildingStorage =
                new GrayboxBuildingStorageSaveAdapter3D(
                    session,
                    buildingPresentation,
                    () => world.Model);
            var economy = new GrayboxEconomySaveAdapter3D(
                operations.Backpack,
                operations.Crafting,
                operations.Research);
            var productionAdapter = new GrayboxProductionSaveAdapter3D(
                production.Clock.Runtime);
            var defenseAdapter = new GrayboxDefenseSaveAdapter3D(
                defense.Runtime);
            var evacuationAdapter = new GrayboxEvacuationSaveAdapter3D(
                evacuation);
            var rebuilder = new GrayboxFormalControllerRebuilder3D(
                production,
                defense,
                evacuation);
            coordinator = GrayboxFormalSaveCoordinator3D.CreateProduction(
                worldCity,
                buildingStorage,
                economy,
                productionAdapter,
                defenseAdapter,
                evacuationAdapter,
                new GrayboxFormalPauseSaveDomain3D(Speed),
                () => session.Instances,
                () => world.Model,
                allowBackpackOverStack: false,
                rebuilder,
                production,
                defense,
                evacuation);
            checkpointPolicy = new FormalSaveCheckpointPolicy(
                TryWriteCheckpoint,
                () => session.CheckpointRuleTimeSeconds);
            coordinator.ConfigureCheckpointPolicy(
                checkpointPolicy,
                city.Deployment,
                session,
                defense,
                evacuation);
            IsInitialized = true;
            return true;
        }

        public bool TryStartNewProgress()
        {
            if (!TryInitialize() || HasCoordinatorSafetyBarrier())
                return false;
            FormalSaveStoreResult existing = Probe();
            if (existing.Code == FormalSaveStoreCode.UnsupportedFutureSchema)
                return false;

            ResetCheckpointBaseline();
            currentSessionId = Guid.NewGuid().ToString("N");
            writeIntent.BeginNewProgress(
                existing.Code == FormalSaveStoreCode.Legacy2DOnly);
            automaticCheckpointFailureBlocked = false;
            automaticCheckpointFailureRevision = 0;
            SetCheckpointWarning(false);
            bool queued = checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.NewGameReady,
                currentSessionId + "|ready");
            if (!queued) return false;

            // A new game may continue when its first automatic save fails;
            // the pending request and new-progress intent remain retryable.
            FlushPendingCheckpoint();
            return true;
        }

        public bool TryContinue()
        {
            if (!TryInitialize()) return false;
            LastStoreResult =
                store.Load(FormalSavePayloadKind.Formal3D);
            if (!LastStoreResult.Success ||
                LastStoreResult.PayloadKind !=
                    FormalSavePayloadKind.Formal3D ||
                LastStoreResult.Envelope == null)
            {
                return false;
            }

            LastCoordinatorResult = coordinator.RestoreEnvelope(
                LastStoreResult.Envelope);
            if (!LastCoordinatorResult.Success) return false;

            currentSessionId =
                LastStoreResult.Envelope.formal3D.sessionId;
            writeIntent.AdoptContinuedProgress();
            automaticCheckpointFailureBlocked = false;
            automaticCheckpointFailureRevision = 0;
            SetCheckpointWarning(false);
            return true;
        }

        public bool TrySaveAndExit()
        {
            if (!TryInitialize() ||
                string.IsNullOrWhiteSpace(currentSessionId))
            {
                return false;
            }

            if (checkpointPolicy.HasPending)
                return FlushPendingCheckpoint();

            var checkpoint = new FormalSaveCheckpointMetadata
            {
                sequence = checkpointPolicy.Sequence + 1L,
                reasonId = SaveAndExitReasonId,
                ruleTimeSeconds = session.CheckpointRuleTimeSeconds,
                completedMilestoneIds = CopyAndSort(
                    checkpointPolicy.CompletedMilestoneIds),
            };
            return TryWriteCheckpoint(checkpoint);
        }

        public bool FlushPendingCheckpoint()
        {
            if (!IsInitialized || checkpointPolicy == null ||
                !checkpointPolicy.HasPending)
            {
                return false;
            }

            ulong attemptedRevision = checkpointPolicy.PendingRevision;
            bool succeeded = checkpointPolicy.FlushPending();
            automaticCheckpointFailureBlocked =
                !succeeded && checkpointPolicy.HasFailureWarning;
            if (automaticCheckpointFailureBlocked)
            {
                automaticCheckpointFailureRevision = attemptedRevision;
                SetCheckpointWarning(true);
            }
            else if (succeeded)
            {
                automaticCheckpointFailureRevision = 0;
                SetCheckpointWarning(false);
            }
            return succeeded;
        }

        private void Awake()
        {
            EnsureStore();
            _ = Speed;
        }

        private void Start()
        {
            if (!TryInitialize())
            {
                Debug.LogError(
                    "Formal 3D save runtime could not initialize from " +
                    "the authored scene references.",
                    this);
            }
        }

        private void LateUpdate()
        {
            if (!IsInitialized || checkpointPolicy == null ||
                !checkpointPolicy.HasPending)
            {
                return;
            }
            if (automaticCheckpointFailureBlocked &&
                checkpointPolicy.PendingRevision <=
                    automaticCheckpointFailureRevision)
                return;
            FlushPendingCheckpoint();
        }

        private void OnDestroy()
        {
            coordinator?.UnbindCheckpointPolicy();
            checkpointPolicy = null;
            coordinator = null;
            store = null;
            speed = null;
            IsInitialized = false;
            CheckpointWarningChanged = null;
        }

        private bool TryWriteCheckpoint(
            FormalSaveCheckpointMetadata checkpoint)
        {
            LastStoreResult = null;
            string gameVersion = ResolveGameVersion();
            LastCoordinatorResult = coordinator.CaptureEnvelope(
                currentSessionId,
                gameVersion,
                new[] { "builtin:wastecity@" + gameVersion },
                checkpoint,
                DateTime.UtcNow);
            if (!LastCoordinatorResult.Success)
                return false;

            LastStoreResult = store.SaveEnvelope(
                LastCoordinatorResult.Envelope,
                writeIntent.ArchiveLegacy2D,
                writeIntent.Intent);
            writeIntent.CompleteWrite(LastStoreResult.Success);
            if (LastStoreResult.Success)
            {
                automaticCheckpointFailureBlocked = false;
                automaticCheckpointFailureRevision = 0;
                SetCheckpointWarning(false);
            }
            return LastStoreResult.Success;
        }

        private bool HasCoordinatorSafetyBarrier()
        {
            return coordinator != null &&
                   (coordinator.IsTransactionPaused ||
                    LastCoordinatorResult?.RequiresSafeReturnToTitle == true);
        }

        private void SetCheckpointWarning(bool hasWarning)
        {
            if (HasCheckpointWarning == hasWarning) return;
            HasCheckpointWarning = hasWarning;
            CheckpointWarningChanged?.Invoke(hasWarning);
        }

        private void ResetCheckpointBaseline()
        {
            checkpointPolicy.SetSuppressed(true);
            try
            {
                checkpointPolicy.TryRestoreBaseline(
                    new FormalSaveCheckpointMetadata
                    {
                        sequence = 0L,
                        reasonId =
                            FormalSaveCheckpointReasonIds.NewGameReady,
                        ruleTimeSeconds = 0f,
                        completedMilestoneIds = Array.Empty<string>(),
                    });
                session.TryRestoreCheckpointRuleTime(0f, out _);
            }
            finally
            {
                checkpointPolicy.SetSuppressed(false);
            }
        }

        private bool HasAuthoredRuntimeReferences()
        {
            return bootstrap != null &&
                   city != null &&
                   world != null &&
                   session != null &&
                   buildingPresentation != null &&
                   operations != null &&
                   production != null &&
                   defense != null &&
                   evacuation != null;
        }

        private void EnsureStore()
        {
            string root = string.IsNullOrWhiteSpace(
                storeRootOverrideForTesting)
                ? Application.persistentDataPath
                : storeRootOverrideForTesting;
            store ??= new FormalSaveStore(
                root);
        }

        private static void ConfigureStoreRootForTesting(string root)
        {
            storeRootOverrideForTesting = string.IsNullOrWhiteSpace(root)
                ? null
                : root;
        }

        private static string[] CopyAndSort(
            IReadOnlyCollection<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();
            var result = new string[source.Count];
            var index = 0;
            foreach (string value in source)
                result[index++] = value;
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static string ResolveGameVersion()
        {
            return string.IsNullOrWhiteSpace(Application.version)
                ? FallbackGameVersion
                : Application.version.Trim();
        }
    }
}
