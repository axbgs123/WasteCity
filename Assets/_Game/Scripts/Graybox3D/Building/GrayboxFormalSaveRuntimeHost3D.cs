using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.Defense;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

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
        [SerializeField] private GrayboxProgressionHudView3D progressionView;
        [SerializeField]
        private GrayboxFateSelectionView3D fateSelectionView;

        private readonly GrayboxFormalSaveWriteIntentLatch3D writeIntent =
            new GrayboxFormalSaveWriteIntentLatch3D();
        private FormalSaveStore store;
        private FormalSaveWaveRetryStore waveRetryStore;
        private GameSpeedModel speed;
        private GrayboxFormalRuleClock3D ruleClock;
        private GrayboxCampaignTerminalSpeedGate3D terminalSpeedGate;
        private FormalAttentionRuntime attentionRuntime;
        private FormalFateRuntime fateRuntime;
        private PocketUniverseFateEffect pocketUniverseEffect;
        private FormalVoidDebtRuntime voidDebtRuntime;
        private FormalRewindAnchorMetadataRuntime rewindAnchorMetadata;
        private GrayboxFormalProgressionSaveAdapter3D progressionAdapter;
        private GrayboxProgressionEventRouter3D progressionEventRouter;
        private GrayboxPocketUniverseFateController3D pocketUniverseController;
        private GrayboxVoidDebtController3D voidDebtController;
        private GrayboxVoidDebtAttentionController3D
            voidDebtAttentionController;
        private FormalRewindAnchorStore rewindAnchorStore;
        private GrayboxRewindAnchorService3D rewindAnchorService;
        private GrayboxProgressionHudController3D progressionHudController;
        private GrayboxFateSelectionController3D fateSelectionController;
        private GrayboxFormalSaveCoordinator3D coordinator;
        private FormalSaveCheckpointPolicy checkpointPolicy;
        private string currentSessionId = string.Empty;
        private bool automaticCheckpointFailureBlocked;
        private ulong automaticCheckpointFailureRevision;
        private bool lastCheckpointHadRetryArtifactFailure;

        public GameSpeedModel Speed => speed ??= new GameSpeedModel();
        public GrayboxFormalRuleClock3D RuleClock =>
            ruleClock ??= new GrayboxFormalRuleClock3D(Speed);
        public GrayboxCampaignTerminalSpeedGate3D TerminalSpeedGate =>
            terminalSpeedGate ??=
                new GrayboxCampaignTerminalSpeedGate3D(Speed);
        public FormalAttentionRuntime AttentionRuntime =>
            attentionRuntime ??= new FormalAttentionRuntime();
        public FormalFateRuntime FateRuntime =>
            fateRuntime ??= new FormalFateRuntime();
        public PocketUniverseFateEffect PocketUniverseEffect =>
            pocketUniverseEffect ??= new PocketUniverseFateEffect();
        public FormalVoidDebtRuntime VoidDebtRuntime =>
            voidDebtRuntime ??= new FormalVoidDebtRuntime();
        public FormalRewindAnchorMetadataRuntime RewindAnchorMetadata =>
            rewindAnchorMetadata ??=
                new FormalRewindAnchorMetadataRuntime();
        public GrayboxFormalProgressionSaveAdapter3D ProgressionAdapter =>
            progressionAdapter ??=
                new GrayboxFormalProgressionSaveAdapter3D(
                    AttentionRuntime,
                    FateRuntime,
                    PocketUniverseEffect,
                    VoidDebtRuntime,
                    RewindAnchorMetadata);
        public GrayboxProgressionEventRouter3D ProgressionEventRouter =>
            progressionEventRouter;
        public GrayboxPocketUniverseFateController3D PocketUniverseController =>
            pocketUniverseController;
        public GrayboxVoidDebtController3D VoidDebtController =>
            voidDebtController;
        public GrayboxRewindAnchorService3D RewindAnchorService =>
            rewindAnchorService;
        public GrayboxProgressionHudController3D ProgressionHudController =>
            progressionHudController;
        public GrayboxFateSelectionController3D FateSelectionController =>
            fateSelectionController;
        public bool IsInitialized { get; private set; }
        public FormalSaveStoreResult LastStoreResult { get; private set; }
        public FormalSaveWaveRetryStoreResult LastWaveRetryStoreResult
        {
            get;
            private set;
        }
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
            _ = ProgressionAdapter;
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

            BindRuleClock();

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

            if (progressionEventRouter == null)
            {
                var eventRouter = new GrayboxProgressionEventRouter3D(
                    AttentionRuntime,
                    FateRuntime);
                eventRouter.Bind(city.Deployment, session);
                progressionEventRouter = eventRouter;
            }
            if (pocketUniverseController == null)
            {
                pocketUniverseController =
                    new GrayboxPocketUniverseFateController3D(
                        FateRuntime,
                        AttentionRuntime,
                        PocketUniverseEffect);
                pocketUniverseController.Bind(
                    session,
                    production.Clock,
                    defense);
            }
            if (voidDebtController == null)
            {
                voidDebtController = new GrayboxVoidDebtController3D(
                    FateRuntime,
                    VoidDebtRuntime);
                voidDebtController.Bind(
                    session.CityStorage,
                    () => coordinator?.IsTransactionPaused == true);
                session.ConfigureConstructionPaymentPolicy(
                    voidDebtController);
                voidDebtAttentionController =
                    new GrayboxVoidDebtAttentionController3D(
                        AttentionRuntime,
                        FateRuntime,
                        VoidDebtRuntime);
            }
            if (progressionHudController == null && progressionView != null)
            {
                progressionHudController =
                    new GrayboxProgressionHudController3D(
                        AttentionRuntime,
                        FateRuntime,
                        progressionView);
                progressionHudController.RefreshIfChanged();
            }

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
                ProgressionAdapter,
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
            if (rewindAnchorStore == null)
                rewindAnchorStore = new FormalRewindAnchorStore(StoreRoot());
            rewindAnchorService ??= new GrayboxRewindAnchorService3D(
                rewindAnchorStore,
                coordinator,
                AttentionRuntime,
                FateRuntime,
                ResolveRewindSafetyCode,
                () => currentSessionId,
                RewindAnchorMetadata);
            if (fateSelectionController == null && fateSelectionView != null)
            {
                fateSelectionController =
                    new GrayboxFateSelectionController3D(
                        FateRuntime,
                        progressionEventRouter,
                        fateSelectionView,
                        () => FormalFateCatalog.EffectsReady);
                fateSelectionController.SelectionCommitted +=
                    HandleFateSelectionCommitted;
                fateSelectionController.RefreshIfChanged();
            }
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
            if (!ProgressionAdapter.TryRestore(
                    new FormalThreeDProgressionSaveData(),
                    out _))
            {
                return false;
            }
            pocketUniverseController?.SynchronizeSelection();
            fateSelectionController?.RefreshIfChanged();

            ResetCheckpointBaseline();
            currentSessionId = Guid.NewGuid().ToString("N");
            rewindAnchorStore?.Clear();
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
            pocketUniverseController?.SynchronizeSelection();
            fateSelectionController?.RefreshIfChanged();
            writeIntent.AdoptContinuedProgress();
            automaticCheckpointFailureBlocked = false;
            automaticCheckpointFailureRevision = 0;
            SetCheckpointWarning(false);
            return true;
        }

        public bool TryRetryWaveCheckpoint()
        {
            if (!TryInitialize() || HasCoordinatorSafetyBarrier())
                return false;

            LastWaveRetryStoreResult = waveRetryStore.Load();
            if (!LastWaveRetryStoreResult.Success ||
                LastWaveRetryStoreResult.Envelope == null)
            {
                return false;
            }

            FormalSaveEnvelope retryEnvelope =
                LastWaveRetryStoreResult.Envelope;
            int currentWaveNumber =
                defense?.CampaignSnapshot?.CurrentWaveNumber ?? 0;
            if (string.IsNullOrWhiteSpace(currentSessionId) ||
                !string.Equals(
                    retryEnvelope.formal3D?.sessionId,
                    currentSessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    retryEnvelope.checkpoint?.reasonId,
                    FormalSaveCheckpointReasonIds
                        .CampaignWaveWarningStarted,
                    StringComparison.Ordinal) ||
                retryEnvelope.formal3D?.defenseCampaign == null ||
                currentWaveNumber <= 0 ||
                retryEnvelope.formal3D.defenseCampaign.currentWaveNumber !=
                    currentWaveNumber ||
                retryEnvelope.formal3D.defenseCampaign.phase !=
                    (int)SingleCityDefenseCampaignPhase.Warning)
            {
                LastWaveRetryStoreResult =
                    FormalSaveWaveRetryStoreResult.InvalidCurrentCampaign(
                        "最近波前重试档不属于当前战役波次");
                return false;
            }

            LastCoordinatorResult = coordinator.RestoreEnvelope(
                retryEnvelope);
            if (!LastCoordinatorResult.Success) return false;

            currentSessionId =
                retryEnvelope.formal3D.sessionId;
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
                SetCheckpointWarning(
                    lastCheckpointHadRetryArtifactFailure);
            }
            return succeeded;
        }

        private void Awake()
        {
            EnsureStore();
            _ = Speed;
            BindRuleClock();
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
            TickVoidDebt();
            fateSelectionController?.RefreshIfChanged();
            progressionHudController?.RefreshIfChanged();
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
            waveRetryStore = null;
            ruleClock = null;
            terminalSpeedGate?.Synchronize(null);
            terminalSpeedGate = null;
            progressionEventRouter?.Dispose();
            progressionEventRouter = null;
            pocketUniverseController?.Dispose();
            pocketUniverseController = null;
            session?.ConfigureConstructionPaymentPolicy(null);
            voidDebtController?.Dispose();
            voidDebtController = null;
            voidDebtAttentionController = null;
            rewindAnchorService = null;
            rewindAnchorStore = null;
            rewindAnchorMetadata = null;
            progressionHudController = null;
            if (fateSelectionController != null)
            {
                fateSelectionController.SelectionCommitted -=
                    HandleFateSelectionCommitted;
                fateSelectionController.Dispose();
                fateSelectionController = null;
            }
            progressionAdapter = null;
            voidDebtRuntime = null;
            pocketUniverseEffect = null;
            fateRuntime = null;
            attentionRuntime = null;
            speed = null;
            IsInitialized = false;
            CheckpointWarningChanged = null;
        }

        private bool TryWriteCheckpoint(
            FormalSaveCheckpointMetadata checkpoint)
        {
            LastStoreResult = null;
            lastCheckpointHadRetryArtifactFailure = false;
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
            bool checkpointSucceeded = LastStoreResult.Success;
            bool retryArtifactSucceeded = true;
            if (LastStoreResult.Success)
            {
                if (string.Equals(
                        checkpoint.reasonId,
                        FormalSaveCheckpointReasonIds.CampaignWaveWarningStarted,
                        StringComparison.Ordinal))
                {
                    LastWaveRetryStoreResult = waveRetryStore.Save(
                        LastCoordinatorResult.Envelope);
                    retryArtifactSucceeded =
                        LastWaveRetryStoreResult.Success;
                }
                if (retryArtifactSucceeded)
                {
                    automaticCheckpointFailureBlocked = false;
                    automaticCheckpointFailureRevision = 0;
                    SetCheckpointWarning(false);
                }
                else
                {
                    lastCheckpointHadRetryArtifactFailure = true;
                    SetCheckpointWarning(true);
                }
            }
            return checkpointSucceeded;
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

        private void BindRuleClock()
        {
            if (session != null)
                session.ConfigureRuleClock(RuleClock);
            if (city != null)
                city.ConfigureRuleClock(RuleClock);
            if (defense != null)
            {
                defense.ConfigureFormalSpeedRuntime(
                    Speed,
                    RuleClock,
                    TerminalSpeedGate);
            }
        }

        private void EnsureStore()
        {
            string root = StoreRoot();
            store ??= new FormalSaveStore(
                root);
            waveRetryStore ??= new FormalSaveWaveRetryStore(root);
        }

        private static string StoreRoot()
        {
            return string.IsNullOrWhiteSpace(storeRootOverrideForTesting)
                ? Application.persistentDataPath
                : storeRootOverrideForTesting;
        }

        private void TickVoidDebt()
        {
            if (!IsInitialized || voidDebtAttentionController == null ||
                coordinator?.IsTransactionPaused == true)
            {
                return;
            }
            float delta = RuleClock.ResolveRuleDelta(Time.unscaledDeltaTime);
            if (delta <= 0f || VoidDebtRuntime.TotalDebt <= 0) return;
            voidDebtAttentionController.Tick(delta, out _);
        }

        private string ResolveRewindSafetyCode()
        {
            if (coordinator?.IsTransactionPaused == true)
                return GrayboxRewindAnchorService3D
                    .SaveTransactionSafetyCode;
            if (city?.Deployment != null &&
                (city.Deployment.Mode == CityMode.Deploying ||
                 city.Deployment.Mode == CityMode.Packing))
                return GrayboxRewindAnchorService3D.DeploymentSafetyCode;
            if (evacuation != null &&
                (evacuation.IsManifestOpen || evacuation.IsProcessing))
                return GrayboxRewindAnchorService3D.EvacuationSafetyCode;
            SingleCityDefenseCampaignPhase phase =
                defense?.CampaignSnapshot?.Phase ??
                SingleCityDefenseCampaignPhase.Idle;
            return phase == SingleCityDefenseCampaignPhase.Warning ||
                   phase == SingleCityDefenseCampaignPhase.SpawningAndCombat ||
                   phase == SingleCityDefenseCampaignPhase.CombatCleanup
                ? GrayboxRewindAnchorService3D.CombatSafetyCode
                : string.Empty;
        }

        private void HandleFateSelectionCommitted(string fateId)
        {
            pocketUniverseController?.SynchronizeSelection();
            if (checkpointPolicy == null ||
                string.IsNullOrWhiteSpace(fateId))
            {
                return;
            }
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.FateSelectionComplete,
                "fate-selection:" + fateId);
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
