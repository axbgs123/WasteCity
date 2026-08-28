using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.Defense;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;
using WasteCity.Graybox3D.Usability;

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
        [SerializeField]
        private GrayboxFateOperationsView3D fateOperationsView;
        [SerializeField]
        private GrayboxDeveloperModifierBootstrap3D developerModifier;
        [SerializeField]
        private GrayboxCivilizationAdvancementView3D advancementView;
        [SerializeField]
        private GrayboxCivilizationExpansionController3D expansionController;
        [SerializeField]
        private MonoBehaviour inputCoordinator;

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
        private AttentionPressureRuntime attentionPressureRuntime;
        private FormalCivilizationAscensionRuntime civilization;
        private AdvancementSequenceModel advancementSequence;
        private GrayboxCivilizationAdvancementController3D
            civilizationAdvancementController;
        private GrayboxBuildingUpgradeController3D buildingUpgradeController;
        private ElixirSessionMutationSequence3D elixirMutationSequence;
        private string buildingUpgradeFeedbackStableId = string.Empty;
        private string buildingUpgradeFeedback = string.Empty;
        private bool buildingUpgradeProjectionInitialized;
        private string presentedBuildingUpgradeStableId = string.Empty;
        private uint presentedBuildingUpgradeCatalogRevision;
        private ulong presentedBuildingUpgradeStorageRevision;
        private ulong presentedBuildingUpgradeCivilizationRevision;
        private FormalCivilizationAscensionRequirements cachedRequirements;
        private uint cachedRequirementsCatalogRevision;
        private bool cachedRequirementsProductionRunning;
        private bool cachedRequirementsBossDefeated;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private FormalCivilizationAscensionRequirements
            developmentRequirementsOverride;
        private GrayboxDeveloperProgressionFacade3D
            developerProgressionFacade;
#endif
        private GrayboxFormalProgressionSaveAdapter3D progressionAdapter;
        private GrayboxAttentionPressureSaveAdapter3D pressureSaveAdapter;
        private GrayboxProgressionEventRouter3D progressionEventRouter;
        private GrayboxPocketUniverseFateController3D pocketUniverseController;
        private GrayboxVoidDebtController3D voidDebtController;
        private GrayboxVoidDebtAttentionController3D
            voidDebtAttentionController;
        private FormalRewindAnchorStore rewindAnchorStore;
        private GrayboxRewindAnchorService3D rewindAnchorService;
        private GrayboxAttentionPressureDefenseController3D
            pressureDefenseController;
        private GrayboxAttentionPressureRuntimeController3D
            pressureRuntimeController;
        private GrayboxAttentionPressurePresentationController3D
            pressurePresentation;
        private GrayboxProgressionHudController3D progressionHudController;
        private GrayboxFateSelectionController3D fateSelectionController;
        private GrayboxFateOperationsController3D fateOperationsController;
        private GrayboxCivilizationAdvancementPresentationController3D
            advancementPresentationController;
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
        public AttentionPressureRuntime AttentionPressureRuntime =>
            attentionPressureRuntime ??= new AttentionPressureRuntime();
        public FormalCivilizationAscensionRuntime Civilization =>
            civilization ??= new FormalCivilizationAscensionRuntime();
        public AdvancementSequenceModel Sequence =>
            advancementSequence ??= new AdvancementSequenceModel();
        public GrayboxFormalProgressionSaveAdapter3D ProgressionAdapter =>
            progressionAdapter;
        public GrayboxProgressionEventRouter3D ProgressionEventRouter =>
            progressionEventRouter;
        public GrayboxPocketUniverseFateController3D PocketUniverseController =>
            pocketUniverseController;
        public GrayboxVoidDebtController3D VoidDebtController =>
            voidDebtController;
        public GrayboxRewindAnchorService3D RewindAnchorService =>
            rewindAnchorService;
        public bool BossDefeated
        {
            get
            {
                return IsBossDefeated(AttentionPressureRuntime.Capture());
            }
        }
        public GrayboxProgressionHudController3D ProgressionHudController =>
            progressionHudController;
        public GrayboxFateSelectionController3D FateSelectionController =>
            fateSelectionController;
        public GrayboxFateOperationsController3D FateOperationsController =>
            fateOperationsController;
        public GrayboxCivilizationExpansionController3D
            CivilizationExpansionController => expansionController;
        public string CurrentSessionId => currentSessionId;
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
        public string LastInitializationError { get; private set; } =
            string.Empty;
        public string LastProgressionRestoreError { get; private set; } =
            string.Empty;
        public string LastStartNewProgressError { get; private set; } =
            string.Empty;

        public event Action<bool> CheckpointWarningChanged;

        public FormalSaveStoreResult Probe()
        {
            EnsureStore();
            LastStoreResult = store.Probe(FormalSavePayloadKind.Formal3D);
            return LastStoreResult;
        }

        public bool TryInitialize()
        {
            LastInitializationError = string.Empty;
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
                LastInitializationError =
                    "场景运行时引用或正式经济模型尚未就绪";
                return false;
            }

            BindRuleClock();
            if (expansionController != null)
            {
                expansionController.ConfigureSessionIdProvider(
                    () => currentSessionId);
                if (!expansionController.TryInitialize(
                        out string expansionError))
                {
                    LastInitializationError =
                        "文明扩展运行时初始化失败：" + expansionError;
                    return false;
                }
                production.ConfigureCivilizationEfficiencySource(
                    () => expansionController
                        .CivilizationEfficiencyMultiplier);
                operations.ConfigureCivilizationResearchEfficiency(
                    () => expansionController
                        .ResearchEfficiencyMultiplier);
            }
            operations.ConfigureCivilizationResearch(
                () => Civilization.Capture().CivilizationLevel,
                () => Civilization.Capture().Revision);
            elixirMutationSequence ??=
                new ElixirSessionMutationSequence3D(
                    () => currentSessionId);
            operations.ConfigureElixirUse(() =>
            {
                GrayboxElixirUseResult3D result =
                    GrayboxElixirUseCommand3D.TryUse(
                        session.CityStorage,
                        defense,
                        session.IsResearchCompleted(
                            GrayboxElixirUseCommand3D
                                .FleshElixirResearchId),
                        elixirMutationSequence.PeekSamplePercent());
                if (result.Succeeded)
                    elixirMutationSequence.CommitUse();
                return result;
            });

            if (!production.TryRebuildAfterPersistenceRestore(
                    out string productionError))
            {
                LastInitializationError =
                    "生产运行时重建失败：" + productionError;
                return false;
            }
            if (!defense.TryRebuildAfterPersistenceRestore(
                    out string defenseError))
            {
                LastInitializationError =
                    "防御运行时重建失败：" + defenseError;
                return false;
            }

            try
            {
                evacuation.ConfigureOperationalRuntimes(
                    production.Clock.Runtime,
                    defense.Runtime);
            }
            catch (InvalidOperationException exception)
            {
                LastInitializationError =
                    "撤离运行时接线失败：" + exception.Message;
                return false;
            }
            if (!evacuation.TryRebuildAfterPersistenceRestore(
                    out string evacuationError))
            {
                LastInitializationError =
                    "撤离运行时重建失败：" + evacuationError;
                return false;
            }

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
            RebuildPressureComposition();
            if (buildingUpgradeController == null)
            {
                buildingUpgradeController =
                    new GrayboxBuildingUpgradeController3D(
                        session,
                        () => Civilization.Capture().CivilizationLevel,
                        buildingPresentation);
                if (defense.Hud != null)
                    defense.Hud.BuildingUpgradeRequested +=
                        HandleBuildingUpgradeRequested;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            developerProgressionFacade ??=
                new GrayboxDeveloperProgressionFacade3D(this);
            developerModifier?.ConfigureProgressionFacade(
                developerProgressionFacade);
#endif
            if (progressionHudController == null && progressionView != null)
            {
                progressionHudController =
                    new GrayboxProgressionHudController3D(
                        AttentionRuntime,
                        FateRuntime,
                        progressionView);
                progressionHudController.RefreshIfChanged();
            }
            if (pressurePresentation == null && progressionView != null)
            {
                pressurePresentation =
                    new GrayboxAttentionPressurePresentationController3D(
                        AttentionPressureRuntime,
                        progressionView);
                pressurePresentation.RefreshIfChanged();
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
            GrayboxCivilizationExpansionSaveAdapter3D expansionAdapter =
                expansionController == null
                    ? null
                    : new GrayboxCivilizationExpansionSaveAdapter3D(
                        expansionController);
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
                evacuation,
                expansionAdapter);
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
            if (fateOperationsController == null &&
                fateOperationsView != null && progressionView != null)
            {
                fateOperationsController =
                    new GrayboxFateOperationsController3D(
                        FateRuntime,
                        PocketUniverseEffect,
                        VoidDebtRuntime,
                        RewindAnchorMetadata,
                        fateOperationsView);
                progressionView.FateDetailsRequested +=
                    HandleFateDetailsRequested;
                fateOperationsView.CreateRewindAnchorRequested +=
                    HandleCreateRewindAnchorRequested;
                fateOperationsView.ReadRewindAnchorByIdRequested +=
                    HandleReadRewindAnchorByIdRequested;
                fateOperationsView.ClearRewindAnchorRequested +=
                    HandleClearRewindAnchorRequested;
                fateOperationsController.RefreshIfChanged();
            }
            if (advancementPresentationController == null &&
                advancementView != null && inputCoordinator is
                    IGrayboxCivilizationAdvancementInputBinding3D inputBinding)
            {
                advancementPresentationController =
                    new GrayboxCivilizationAdvancementPresentationController3D(
                        Civilization,
                        FateRuntime,
                        Sequence,
                        advancementView,
                        CaptureRequirements);
                advancementPresentationController.AdvanceRequested +=
                    HandleAdvancementRequested;
                advancementPresentationController.ContinueRequested +=
                    HandleAdvancementContinueRequested;
                inputBinding.ConfigureAdvancement(
                    advancementView,
                    TryAdvanceCivilizationFromInput,
                    TryContinueCivilizationAdvancement);
                advancementPresentationController.RefreshIfChanged();
            }
            IsInitialized = true;
            return true;
        }

        public bool TryStartNewProgress()
        {
            LastStartNewProgressError = string.Empty;
            LastProgressionRestoreError = string.Empty;
            if (!TryInitialize())
            {
                LastStartNewProgressError =
                    string.IsNullOrWhiteSpace(LastInitializationError)
                        ? "正式 3D 运行时初始化失败"
                        : LastInitializationError;
                return false;
            }
            if (HasCoordinatorSafetyBarrier())
            {
                LastStartNewProgressError =
                    "存档事务处于安全阻断状态";
                return false;
            }
            FormalSaveStoreResult existing = Probe();
            if (existing.Code == FormalSaveStoreCode.UnsupportedFutureSchema)
            {
                LastStartNewProgressError = existing.Message;
                return false;
            }
            if (!ProgressionAdapter.TryRestore(
                    new FormalThreeDProgressionSaveData(),
                    out string progressionError))
            {
                LastProgressionRestoreError = progressionError;
                LastStartNewProgressError = progressionError;
                return false;
            }
            pocketUniverseController?.SynchronizeSelection();
            fateSelectionController?.RefreshIfChanged();
            fateOperationsController?.RefreshIfChanged();
            SynchronizeAdvancementPause();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            developmentRequirementsOverride = null;
#endif

            ResetCheckpointBaseline();
            currentSessionId = Guid.NewGuid().ToString("N");
            if (expansionController != null &&
                !expansionController.ResetForNewProgress(
                    out string expansionResetError))
            {
                LastStartNewProgressError =
                    "文明扩展新进度初始化失败：" + expansionResetError;
                return false;
            }
            rewindAnchorStore?.Clear();
            writeIntent.BeginNewProgress(
                existing.Code == FormalSaveStoreCode.Legacy2DOnly);
            automaticCheckpointFailureBlocked = false;
            automaticCheckpointFailureRevision = 0;
            SetCheckpointWarning(false);
            bool queued = checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.NewGameReady,
                currentSessionId + "|ready");
            if (!queued)
            {
                LastStartNewProgressError =
                    "新进度就绪检查点无法加入保存队列";
                return false;
            }

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
            fateOperationsController?.RefreshIfChanged();
            SynchronizeAdvancementPause();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            developmentRequirementsOverride = null;
#endif
            writeIntent.AdoptContinuedProgress();
            automaticCheckpointFailureBlocked = false;
            automaticCheckpointFailureRevision = 0;
            SetCheckpointWarning(false);
            return true;
        }

        public FormalCivilizationAscensionRequirements CaptureRequirements()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (developmentRequirementsOverride != null)
                return developmentRequirementsOverride;
#endif
            uint catalogRevision = session?.CatalogRevision ?? 0u;
            ProductionObservabilitySnapshot productionSnapshot =
                production?.Snapshot ?? ProductionObservabilitySnapshot.Empty;
            AttentionPressureSnapshot pressureSnapshot =
                AttentionPressureRuntime.Capture();
            bool bossDefeated = pressureSnapshot.CrystalBroodmotherDefeated;
            if (cachedRequirements != null &&
                cachedRequirementsCatalogRevision == catalogRevision &&
                cachedRequirementsProductionRunning ==
                    productionSnapshot.HasCurrentlyRunnableBuilding &&
                cachedRequirementsBossDefeated == bossDefeated)
            {
                return cachedRequirements;
            }
            bool legacyAnalysisCompleted = session != null &&
                session.IsResearchCompleted(
                    FormalCivilizationAscensionRuntime
                        .LegacyAnalysisResearchId);
            var machineGunTurrets = 0;
            IReadOnlyList<GrayboxBuildingInstance3D> instances =
                session?.Instances;
            if (instances != null)
            {
                for (var index = 0; index < instances.Count; index++)
                {
                    GrayboxBuildingInstance3D instance = instances[index];
                    if (instance != null && instance.IsPlayerOwned &&
                        instance.State ==
                            GrayboxBuildingInstanceState.Completed &&
                        string.Equals(
                            instance.Placement?.Definition?.Id.Value,
                            BuildingCatalog.MachineGunTurret.Id.Value,
                            StringComparison.Ordinal))
                    {
                        machineGunTurrets++;
                    }
                }
            }
            bool productionRunning =
                productionSnapshot.HasCurrentlyRunnableBuilding;
            cachedRequirements =
                new FormalCivilizationAscensionRequirements(
                legacyAnalysisCompleted,
                machineGunTurrets,
                bossDefeated,
                productionRunning);
            cachedRequirementsCatalogRevision = catalogRevision;
            cachedRequirementsProductionRunning = productionRunning;
            cachedRequirementsBossDefeated = bossDefeated;
            return cachedRequirements;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool SatisfyAscensionRequirementsForDevelopment()
        {
            if (developmentRequirementsOverride?.CanAscend == true)
                return false;
            developmentRequirementsOverride =
                new FormalCivilizationAscensionRequirements(
                    true,
                    FormalCivilizationAscensionRuntime
                        .RequiredMachineGunTurretCount,
                    true,
                    true);
            return true;
        }

        public bool ClearAscensionRequirementsForDevelopment()
        {
            if (developmentRequirementsOverride == null) return false;
            developmentRequirementsOverride = null;
            cachedRequirements = null;
            return true;
        }

        public bool CompletePressureFixtureForDevelopment(int threshold)
        {
            return TryRestorePressureFixtureForDevelopment(threshold);
        }

        public bool ResetPressureFixtureForDevelopment()
        {
            return TryRestorePressureFixtureForDevelopment(0);
        }

        public bool SetBossDefeatedForDevelopment(bool defeated)
        {
            if (AttentionPressureRuntime.Capture()
                    .CrystalBroodmotherDefeated == defeated)
                return false;
            return TryRestorePressureFixtureForDevelopment(
                defeated ? 90 : 60);
        }

        private bool TryRestorePressureFixtureForDevelopment(int threshold)
        {
            if (threshold != 0 &&
                AttentionPressureCatalog.FindByThreshold(threshold) == null)
                return false;
            AttentionPressureSnapshot before =
                AttentionPressureRuntime.Capture();
            var entries = new List<AttentionPressureEntrySnapshot>();
            for (var index = 0;
                 index < AttentionPressureCatalog.All.Count;
                 index++)
            {
                AttentionPressureDefinition definition =
                    AttentionPressureCatalog.All[index];
                if (definition.Threshold > threshold) break;
                entries.Add(new AttentionPressureEntrySnapshot(
                    definition.Threshold,
                    AttentionPressureState.Completed,
                    0f));
            }
            var candidate = new AttentionPressureSnapshot(
                before.Revision + 1UL,
                entries.ToArray());
            var validator = new AttentionPressureRuntime();
            if (!validator.TryRestore(candidate, out _)) return false;
            bool pressureChanged = before.Entries.Count != entries.Count;
            if (!pressureChanged)
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    if (before.Entries[index].Threshold !=
                            entries[index].Threshold ||
                        before.Entries[index].State != entries[index].State)
                    {
                        pressureChanged = true;
                        break;
                    }
                }
            }
            bool defenseChanged = defense?.Runtime?
                .HasActivePressureCampaign == true;
            if (!pressureChanged && !defenseChanged) return false;
            if (defenseChanged) defense.Runtime.ClearActivePressure();
            return AttentionPressureRuntime.TryRestore(candidate, out _);
        }
#endif

        public GrayboxCivilizationAdvancementResult3D
            TryAdvanceCivilization()
        {
            if (!TryInitialize() ||
                civilizationAdvancementController == null)
            {
                return AdvancementFailure(
                    "advancement.runtime-unavailable",
                    LastInitializationError);
            }
            if (!Sequence.Start())
                return AdvancementFailure("advancement.sequence-unavailable");
            GrayboxCivilizationAdvancementResult3D result =
                civilizationAdvancementController.Execute(
                    CaptureRequirements());
            if (!result.Success)
            {
                Sequence.Restore((int)AdvancementSequenceStage.None, 0f);
                return result;
            }
            Speed.SetPaused(GamePauseReason.Advancement, true);
            checkpointPolicy?.QueueCheckpoint(
                result.CheckpointReasonId,
                result.StableEventKey);
            return result;
        }

        public bool TryContinueCivilizationAdvancement()
        {
            if (!TryInitialize() || !Sequence.Continue()) return false;
            Speed.SetPaused(GamePauseReason.Advancement, false);
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
            if (IsInitialized && expansionController != null)
            {
                float expansionDelta = RuleClock.ResolveRuleDelta(
                    Time.unscaledDeltaTime);
                expansionController.Tick(
                    expansionDelta,
                    expansionDelta <= 0f ||
                    coordinator?.IsTransactionPaused == true);
            }
            TickCivilizationAdvancement();
            TickVoidDebt();
            TickAttentionPressure();
            RefreshBuildingUpgradeCommand();
            fateSelectionController?.RefreshIfChanged();
            fateOperationsController?.RefreshIfChanged();
            progressionHudController?.RefreshIfChanged();
            pressurePresentation?.RefreshIfChanged();
            advancementPresentationController?.RefreshIfChanged();
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
            production?.ConfigureCivilizationEfficiencySource(null);
            operations?.ConfigureCivilizationResearchEfficiency(null);
            expansionController = null;
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
            if (pressureRuntimeController != null)
            {
                pressureRuntimeController.WarningStarted -=
                    HandlePressureWarningStarted;
                pressureRuntimeController.Dispose();
                pressureRuntimeController = null;
            }
            if (pressureDefenseController != null)
            {
                pressureDefenseController.EncounterStarted -=
                    HandlePressureEncounterStarted;
                pressureDefenseController.EncounterCompleted -=
                    HandlePressureEncounterCompleted;
                pressureDefenseController.Dispose();
                pressureDefenseController = null;
            }
            pressurePresentation = null;
            attentionPressureRuntime = null;
            if (advancementPresentationController != null)
            {
                advancementPresentationController.AdvanceRequested -=
                    HandleAdvancementRequested;
                advancementPresentationController.ContinueRequested -=
                    HandleAdvancementContinueRequested;
                advancementPresentationController.Dispose();
                advancementPresentationController = null;
            }
            Speed.SetPaused(GamePauseReason.Advancement, false);
            civilizationAdvancementController = null;
            if (defense?.Hud != null)
                defense.Hud.BuildingUpgradeRequested -=
                    HandleBuildingUpgradeRequested;
            buildingUpgradeController = null;
            elixirMutationSequence = null;
            buildingUpgradeFeedbackStableId = string.Empty;
            buildingUpgradeFeedback = string.Empty;
            advancementSequence = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            developerModifier?.ConfigureProgressionFacade(null);
            developerProgressionFacade = null;
            developmentRequirementsOverride = null;
#endif
            civilization = null;
            cachedRequirements = null;
            progressionHudController = null;
            if (fateSelectionController != null)
            {
                fateSelectionController.SelectionCommitted -=
                    HandleFateSelectionCommitted;
                fateSelectionController.Dispose();
                fateSelectionController = null;
            }
            if (fateOperationsView != null)
            {
                progressionView.FateDetailsRequested -=
                    HandleFateDetailsRequested;
                fateOperationsView.CreateRewindAnchorRequested -=
                    HandleCreateRewindAnchorRequested;
                fateOperationsView.ReadRewindAnchorByIdRequested -=
                    HandleReadRewindAnchorByIdRequested;
                fateOperationsView.ClearRewindAnchorRequested -=
                    HandleClearRewindAnchorRequested;
            }
            fateOperationsController = null;
            progressionAdapter = null;
            pressureSaveAdapter = null;
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
                   evacuation != null &&
                   expansionController != null;
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
            if (!Civilization.TryBindFate(fateId, out string bindError))
            {
                Debug.LogError(bindError, this);
                return;
            }
            if (checkpointPolicy == null ||
                string.IsNullOrWhiteSpace(fateId))
            {
                return;
            }
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.FateSelectionComplete,
                "fate-selection:" + fateId);
        }

        private void HandleAdvancementRequested()
        {
            TryAdvanceCivilization();
            advancementPresentationController?.RefreshIfChanged();
        }

        private void HandleAdvancementContinueRequested()
        {
            TryContinueCivilizationAdvancement();
            advancementPresentationController?.RefreshIfChanged();
        }

        private bool TryAdvanceCivilizationFromInput()
        {
            return TryAdvanceCivilization().Success;
        }

        private void HandleFateDetailsRequested()
        {
            if (fateOperationsController?.TryOpen() == true)
                progressionHudController?.RefreshIfChanged();
        }

        private void HandleCreateRewindAnchorRequested()
        {
            if (rewindAnchorService == null || checkpointPolicy == null) return;
            var checkpoint = new FormalSaveCheckpointMetadata
            {
                sequence = checkpointPolicy.Sequence + 1L,
                reasonId = FormalSaveCheckpointReasonIds.RewindAnchorCreated,
                ruleTimeSeconds = session.CheckpointRuleTimeSeconds,
                completedMilestoneIds = CopyAndSort(
                    checkpointPolicy.CompletedMilestoneIds),
            };
            GrayboxRewindAnchorServiceResult3D result =
                rewindAnchorService.Create(
                    ResolveGameVersion(),
                    new[] { "builtin:wastecity@" + ResolveGameVersion() },
                    checkpoint,
                    DateTime.UtcNow);
            if (result.Success)
            {
                checkpointPolicy.QueueCheckpoint(
                    FormalSaveCheckpointReasonIds.RewindAnchorCreated,
                    "rewind-anchor-created:" +
                    RewindAnchorMetadata.Revision);
                fateOperationsController?.RefreshIfChanged();
            }
        }

        private void HandleReadRewindAnchorRequested()
        {
            GrayboxRewindAnchorServiceResult3D result =
                rewindAnchorService?.Read();
            if (result?.Success != true || checkpointPolicy == null) return;
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.RewindAnchorUsed,
                "rewind-anchor-used:" + AttentionRuntime.Revision);
            pocketUniverseController?.SynchronizeSelection();
            fateOperationsController?.RefreshIfChanged();
        }

        private void HandleReadRewindAnchorByIdRequested(string anchorId)
        {
            GrayboxRewindAnchorServiceResult3D result =
                rewindAnchorService?.Read(anchorId);
            if (result?.Success != true || checkpointPolicy == null) return;
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.RewindAnchorUsed,
                "rewind-anchor-used:" + AttentionRuntime.Revision);
            pocketUniverseController?.SynchronizeSelection();
            fateOperationsController?.RefreshIfChanged();
            SynchronizeAdvancementPause();
        }

        private void HandleClearRewindAnchorRequested()
        {
            GrayboxRewindAnchorServiceResult3D result =
                rewindAnchorService?.Clear();
            if (result?.Success != true || checkpointPolicy == null) return;
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.RewindAnchorCleared,
                "rewind-anchor-cleared:" + RewindAnchorMetadata.Revision);
            fateOperationsController?.RefreshIfChanged();
        }

        private void TickAttentionPressure()
        {
            if (!IsInitialized || pressureRuntimeController == null ||
                coordinator?.IsTransactionPaused == true)
                return;
            GrayboxDefenseRuntimeSnapshot3D defenseState = defense?.Snapshot;
            SingleCityDefenseCampaignSnapshot campaignState =
                defense?.CampaignSnapshot;
            bool mainCampaignActive = campaignState == null ||
                campaignState.Result != SingleCityDefenseCampaignResult.Victory;
            bool tutorialCompleted = defenseState != null &&
                defenseState.TutorialWaveTriggerCount > 0 &&
                defenseState.SpawnedEnemyCount > 0 &&
                defenseState.AliveEnemyCount == 0;
            bool firstTowerCompleted = defenseState != null &&
                defenseState.Towers.Count > 0;
            pressureRuntimeController.Tick(
                RuleClock.ResolveRuleDelta(Time.unscaledDeltaTime),
                mainCampaignActive,
                tutorialCompleted,
                firstTowerCompleted,
                out _);
        }

        private void HandleBuildingUpgradeRequested(string stableInstanceId)
        {
            if (buildingUpgradeController == null ||
                string.IsNullOrWhiteSpace(stableInstanceId)) return;
            GrayboxBuildingUpgradeResult3D result =
                buildingUpgradeController.TryUpgrade(stableInstanceId);
            buildingUpgradeFeedbackStableId = stableInstanceId;
            buildingUpgradeFeedback = result.Message;
            buildingUpgradeProjectionInitialized = false;
            if (result.Success)
            {
                production?.TryRebuildAfterPersistenceRestore(out _);
                defense?.TryRebuildAfterPersistenceRestore(out _);
            }
            RefreshBuildingUpgradeCommand();
        }

        private bool RefreshBuildingUpgradeCommand()
        {
            if (!IsInitialized || buildingUpgradeController == null ||
                defense?.Hud == null) return false;
            string stableId = defense.SelectedStableId ?? string.Empty;
            uint catalogRevision = session?.CatalogRevision ?? 0u;
            ulong storageRevision = session?.CityStorage?.Revision ?? 0UL;
            ulong civilizationRevision = Civilization.Capture().Revision;
            if (buildingUpgradeProjectionInitialized && string.Equals(
                    stableId,
                    presentedBuildingUpgradeStableId,
                    StringComparison.Ordinal) &&
                catalogRevision == presentedBuildingUpgradeCatalogRevision &&
                storageRevision == presentedBuildingUpgradeStorageRevision &&
                civilizationRevision ==
                    presentedBuildingUpgradeCivilizationRevision)
                return false;
            GrayboxBuildingUpgradeAvailability3D availability =
                buildingUpgradeController.CaptureAvailability(stableId);
            if (!string.Equals(
                    stableId,
                    buildingUpgradeFeedbackStableId,
                    StringComparison.Ordinal))
            {
                buildingUpgradeFeedbackStableId = string.Empty;
                buildingUpgradeFeedback = string.Empty;
            }
            defense.Hud.ApplyBuildingUpgradeCommand(
                availability.IsVisible,
                availability.CanUpgrade,
                availability.ButtonLabel,
                string.IsNullOrEmpty(buildingUpgradeFeedback)
                    ? availability.Feedback
                    : buildingUpgradeFeedback);
            buildingUpgradeProjectionInitialized = true;
            presentedBuildingUpgradeStableId = stableId ?? string.Empty;
            presentedBuildingUpgradeCatalogRevision = catalogRevision;
            presentedBuildingUpgradeStorageRevision = storageRevision;
            presentedBuildingUpgradeCivilizationRevision =
                civilizationRevision;
            return true;
        }

        private void RebuildPressureComposition()
        {
            if (pressureRuntimeController != null)
            {
                pressureRuntimeController.WarningStarted -=
                    HandlePressureWarningStarted;
                pressureRuntimeController.Dispose();
            }
            if (pressureDefenseController != null)
            {
                pressureDefenseController.EncounterStarted -=
                    HandlePressureEncounterStarted;
                pressureDefenseController.EncounterCompleted -=
                    HandlePressureEncounterCompleted;
                pressureDefenseController.Dispose();
            }

            pressureDefenseController =
                new GrayboxAttentionPressureDefenseController3D(
                    AttentionPressureRuntime,
                    defense.Runtime);
            pressureDefenseController.EncounterStarted +=
                HandlePressureEncounterStarted;
            pressureDefenseController.EncounterCompleted +=
                HandlePressureEncounterCompleted;
            pressureRuntimeController =
                new GrayboxAttentionPressureRuntimeController3D(
                    AttentionRuntime,
                    AttentionPressureRuntime,
                    pressureDefenseController);
            pressureRuntimeController.WarningStarted +=
                HandlePressureWarningStarted;
            pressureRuntimeController.Bind();
            pressureSaveAdapter =
                new GrayboxAttentionPressureSaveAdapter3D(
                    AttentionPressureRuntime,
                    defense.Runtime);
            RebuildProgressionComposition();
        }

        private void RebuildProgressionComposition()
        {
            civilizationAdvancementController ??=
                new GrayboxCivilizationAdvancementController3D(
                    Civilization,
                    FateRuntime,
                    AttentionRuntime,
                    PocketUniverseEffect,
                    VoidDebtRuntime,
                    RewindAnchorMetadata);
            progressionAdapter =
                new GrayboxFormalProgressionSaveAdapter3D(
                    AttentionRuntime,
                    FateRuntime,
                    PocketUniverseEffect,
                    VoidDebtRuntime,
                    RewindAnchorMetadata,
                    pressureSaveAdapter,
                    Civilization,
                    Sequence);
        }

        private void TickCivilizationAdvancement()
        {
            if (!IsInitialized) return;
            AdvancementSequenceStage stage = Sequence.Stage;
            bool presenting = stage >= AdvancementSequenceStage.Scanning &&
                stage <= AdvancementSequenceStage.Results;
            Speed.SetPaused(GamePauseReason.Advancement, presenting);
            if (stage < AdvancementSequenceStage.Scanning ||
                stage >= AdvancementSequenceStage.Results)
                return;
            Sequence.Tick(RuleClock.ResolvePresentationDelta(
                Time.unscaledDeltaTime));
        }

        private void SynchronizeAdvancementPause()
        {
            AdvancementSequenceStage stage = Sequence.Stage;
            Speed.SetPaused(
                GamePauseReason.Advancement,
                stage >= AdvancementSequenceStage.Scanning &&
                stage <= AdvancementSequenceStage.Results);
        }

        private static GrayboxCivilizationAdvancementResult3D
            AdvancementFailure(string message, string diagnostic = null)
        {
            return new GrayboxCivilizationAdvancementResult3D(
                GrayboxCivilizationAdvancementCode3D.PrepareFailed,
                false,
                message,
                diagnostic: diagnostic);
        }

        private static bool IsBossDefeated(
            AttentionPressureSnapshot snapshot)
        {
            return snapshot?.CrystalBroodmotherDefeated == true;
        }

        private void HandlePressureWarningStarted(
            AttentionPressureCommand command)
        {
            if (checkpointPolicy == null || command == null) return;
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.PressureWarningStarted,
                "pressure-warning:" + command.EncounterId);
        }

        private void HandlePressureEncounterStarted(
            AttentionPressureCommand command)
        {
            if (checkpointPolicy == null || command == null) return;
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.PressureEncounterStarted,
                "pressure-started:" + command.EncounterId);
        }

        private void HandlePressureEncounterCompleted(
            AttentionPressureCommand command)
        {
            if (checkpointPolicy == null || command == null) return;
            checkpointPolicy.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.PressureEncounterCompleted,
                "pressure-completed:" + command.EncounterId);
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
