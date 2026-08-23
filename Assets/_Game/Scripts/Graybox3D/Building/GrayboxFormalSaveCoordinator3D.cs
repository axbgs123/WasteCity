using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxFormalSaveDomainId3D
    {
        WorldCity,
        BuildingStorage,
        Economy,
        Production,
        Defense,
        Evacuation,
        Pause,
    }

    public enum GrayboxFormalSaveCoordinatorCode3D
    {
        Success,
        Busy,
        DecodeFailed,
        ValidationFailed,
        CaptureFailed,
        ApplyFailed,
        RollbackFailed,
    }

    public interface IFormalThreeDSaveDomain
    {
        GrayboxFormalSaveDomainId3D DomainId { get; }

        bool TryCapture(
            FormalThreeDSaveData destination,
            out string error);

        bool TryApply(
            FormalThreeDSaveData source,
            out string error);
    }

    public interface IFormalThreeDDerivedStateRebuilder
    {
        void RebuildDerivedState();
    }

    public sealed class GrayboxFormalControllerRebuilder3D :
        IFormalThreeDDerivedStateRebuilder
    {
        private readonly GrayboxProductionController3D production;
        private readonly GrayboxDefenseController3D defense;
        private readonly GrayboxEvacuationController3D evacuation;

        public GrayboxFormalControllerRebuilder3D(
            GrayboxProductionController3D production,
            GrayboxDefenseController3D defense,
            GrayboxEvacuationController3D evacuation)
        {
            this.production = production ??
                throw new ArgumentNullException(nameof(production));
            this.defense = defense ??
                throw new ArgumentNullException(nameof(defense));
            this.evacuation = evacuation ??
                throw new ArgumentNullException(nameof(evacuation));
        }

        public void RebuildDerivedState()
        {
            if (!production.TryRebuildAfterPersistenceRestore(
                    out string productionError))
            {
                throw new InvalidOperationException(productionError);
            }
            if (!defense.TryRebuildAfterPersistenceRestore(
                    out string defenseError))
            {
                throw new InvalidOperationException(defenseError);
            }
            if (!evacuation.TryRebuildAfterPersistenceRestore(
                    out string evacuationError))
            {
                throw new InvalidOperationException(evacuationError);
            }
        }
    }

    public sealed class GrayboxFormalPauseSaveDomain3D :
        IFormalThreeDSaveDomain
    {
        private readonly GameSpeedModel speed;

        public GrayboxFormalPauseSaveDomain3D(GameSpeedModel speed)
        {
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
        }

        public GrayboxFormalSaveDomainId3D DomainId =>
            GrayboxFormalSaveDomainId3D.Pause;

        public bool TryCapture(
            FormalThreeDSaveData destination,
            out string error)
        {
            if (destination == null)
            {
                error = "正式 3D 存档载荷不能为空";
                return false;
            }
            destination.pause = new FormalThreeDPauseSaveData
            {
                tacticalPaused = speed.IsPaused(GamePauseReason.User),
            };
            error = string.Empty;
            return true;
        }

        public bool TryApply(
            FormalThreeDSaveData source,
            out string error)
        {
            if (source?.pause == null)
            {
                error = "正式 3D 暂停状态不能为空";
                return false;
            }
            speed.SetPaused(
                GamePauseReason.User,
                source.pause.tacticalPaused);
            error = string.Empty;
            return true;
        }
    }

    public sealed class GrayboxFormalSaveCoordinatorResult3D
    {
        internal GrayboxFormalSaveCoordinatorResult3D(
            bool success,
            GrayboxFormalSaveCoordinatorCode3D code,
            string message,
            FormalSaveEnvelope envelope,
            GrayboxFormalSaveDomainId3D? failedDomain,
            bool rollbackAttempted,
            bool rollbackSucceeded)
        {
            Success = success;
            Code = code;
            Message = message ?? string.Empty;
            Envelope = envelope;
            FailedDomain = failedDomain;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
        }

        public bool Success { get; }
        public GrayboxFormalSaveCoordinatorCode3D Code { get; }
        public string Message { get; }
        public FormalSaveEnvelope Envelope { get; }
        public GrayboxFormalSaveDomainId3D? FailedDomain { get; }
        public bool RollbackAttempted { get; }
        public bool RollbackSucceeded { get; }
        public bool RequiresSafeReturnToTitle =>
            Code == GrayboxFormalSaveCoordinatorCode3D.RollbackFailed;
    }

    public sealed class GrayboxFormalSaveCoordinator3D
    {
        private delegate void DomainCapture(
            FormalThreeDSaveData destination);

        private delegate bool DomainApply(
            FormalThreeDSaveData source,
            out string error);

        private static readonly ReadOnlyCollection<
            GrayboxFormalSaveDomainId3D> OrderedDomainIds =
            Array.AsReadOnly(new[]
            {
                GrayboxFormalSaveDomainId3D.WorldCity,
                GrayboxFormalSaveDomainId3D.BuildingStorage,
                GrayboxFormalSaveDomainId3D.Economy,
                GrayboxFormalSaveDomainId3D.Production,
                GrayboxFormalSaveDomainId3D.Defense,
                GrayboxFormalSaveDomainId3D.Evacuation,
                GrayboxFormalSaveDomainId3D.Pause,
            });

        private readonly IFormalThreeDSaveDomain[] domains;
        private readonly IFormalThreeDDerivedStateRebuilder derivedState;
        private bool transactionActive;
        private FormalSaveCheckpointPolicy checkpointPolicy;
        private GrayboxDefenseController3D checkpointDefense;
        private GrayboxBuildingSession3D checkpointSession;
        private FormalThreeDDefenseCampaignSaveData retainedCampaign;

        public GrayboxFormalSaveCoordinator3D(
            IReadOnlyList<IFormalThreeDSaveDomain> domains,
            IFormalThreeDDerivedStateRebuilder derivedState)
        {
            if (domains == null)
                throw new ArgumentNullException(nameof(domains));
            this.derivedState = derivedState ??
                throw new ArgumentNullException(nameof(derivedState));
            this.domains = ValidateAndOrderDomains(domains);
        }

        public static GrayboxFormalSaveCoordinator3D CreateProduction(
            GrayboxWorldCitySaveAdapter3D worldCity,
            GrayboxBuildingStorageSaveAdapter3D buildingStorage,
            GrayboxEconomySaveAdapter3D economy,
            GrayboxProductionSaveAdapter3D production,
            GrayboxDefenseSaveAdapter3D defense,
            GrayboxEvacuationSaveAdapter3D evacuation,
            IFormalThreeDSaveDomain pauseDomain,
            Func<IReadOnlyList<GrayboxBuildingInstance3D>>
                instancesProvider,
            Func<WorldMapModel> worldProvider,
            bool allowBackpackOverStack,
            IFormalThreeDDerivedStateRebuilder derivedState,
            GrayboxProductionController3D productionController,
            GrayboxDefenseController3D defenseController,
            GrayboxEvacuationController3D evacuationController)
        {
            if (worldCity == null)
                throw new ArgumentNullException(nameof(worldCity));
            if (buildingStorage == null)
                throw new ArgumentNullException(nameof(buildingStorage));
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));
            if (production == null)
                throw new ArgumentNullException(nameof(production));
            if (defense == null)
                throw new ArgumentNullException(nameof(defense));
            if (evacuation == null)
                throw new ArgumentNullException(nameof(evacuation));
            if (pauseDomain == null)
                throw new ArgumentNullException(nameof(pauseDomain));
            if (pauseDomain.DomainId != GrayboxFormalSaveDomainId3D.Pause)
                throw new ArgumentException(
                    "暂停领域标识必须为 Pause",
                    nameof(pauseDomain));
            if (instancesProvider == null)
                throw new ArgumentNullException(nameof(instancesProvider));
            if (worldProvider == null)
                throw new ArgumentNullException(nameof(worldProvider));
            if (productionController == null)
                throw new ArgumentNullException(nameof(productionController));
            if (defenseController == null)
                throw new ArgumentNullException(nameof(defenseController));
            if (evacuationController == null)
                throw new ArgumentNullException(nameof(evacuationController));

            IFormalThreeDSaveDomain[] productionDomains =
            {
                new DelegateDomain(
                    GrayboxFormalSaveDomainId3D.WorldCity,
                    destination =>
                    {
                        destination.world = worldCity.CaptureWorld();
                        destination.city = worldCity.CaptureCity();
                    },
                    (FormalThreeDSaveData source, out string error) =>
                        worldCity.TryRestore(
                            source.world,
                            source.city,
                            out error)),
                new DelegateDomain(
                    GrayboxFormalSaveDomainId3D.BuildingStorage,
                    destination =>
                    {
                        destination.buildings =
                            buildingStorage.CaptureBuildings();
                        destination.storage =
                            buildingStorage.CaptureStorage();
                    },
                    (FormalThreeDSaveData source, out string error) =>
                        buildingStorage.TryRestore(
                            source.buildings,
                            source.storage,
                            out error)),
                new DelegateDomain(
                    GrayboxFormalSaveDomainId3D.Economy,
                    destination =>
                    {
                        destination.backpack = economy.CaptureBackpack();
                        destination.crafting = economy.CaptureCrafting();
                        destination.research = economy.CaptureResearch();
                    },
                    (FormalThreeDSaveData source, out string error) =>
                        economy.TryRestore(
                            source.backpack,
                            source.crafting,
                            source.research,
                            allowBackpackOverStack,
                            out error)),
                new DelegateDomain(
                    GrayboxFormalSaveDomainId3D.Production,
                    destination =>
                        destination.production = production.Capture(),
                    (FormalThreeDSaveData source, out string error) =>
                    {
                        if (!productionController
                                .TryRebuildAfterPersistenceRestore(
                                    out error))
                        {
                            return false;
                        }
                        return production.TryRestore(
                            source.production,
                            instancesProvider(),
                            worldProvider(),
                            out error);
                    }),
                new DelegateDomain(
                    GrayboxFormalSaveDomainId3D.Defense,
                    destination => destination.defense = defense.Capture(),
                    (FormalThreeDSaveData source, out string error) =>
                    {
                        if (!defenseController
                                .TryRebuildAfterPersistenceRestore(
                                    out error))
                        {
                            return false;
                        }
                        return defense.TryRestore(
                            source.defense,
                            instancesProvider(),
                            out error);
                    }),
                new DelegateDomain(
                    GrayboxFormalSaveDomainId3D.Evacuation,
                    destination =>
                        destination.evacuation = evacuation.Capture(),
                    (FormalThreeDSaveData source, out string error) =>
                        evacuation.TryRestore(
                            source.evacuation,
                            out error)),
                pauseDomain,
            };
            var coordinator = new GrayboxFormalSaveCoordinator3D(
                productionDomains,
                derivedState);
            Func<bool> persistencePauseSource = () =>
                coordinator.IsTransactionPaused;
            productionController.ConfigurePersistencePauseSource(
                persistencePauseSource);
            defenseController.ConfigurePersistencePauseSource(
                persistencePauseSource);
            evacuationController.ConfigurePersistencePauseSource(
                persistencePauseSource);
            return coordinator;
        }

        public static IReadOnlyList<GrayboxFormalSaveDomainId3D>
            DomainOrder => OrderedDomainIds;

        public bool IsTransactionPaused => transactionActive;

        public FormalSaveCheckpointPolicy CheckpointPolicy =>
            checkpointPolicy;
        public GrayboxFormalSaveCoordinatorResult3D
            LastCheckpointCaptureResult { get; private set; }
        public FormalSaveStoreResult LastCheckpointStoreResult
        {
            get;
            private set;
        }

        public event Action RestoreCompleted;

        public void ConfigureCheckpointPolicy(
            FormalSaveCheckpointPolicy policy,
            CityDeploymentModel deployment,
            GrayboxBuildingSession3D session,
            GrayboxDefenseController3D defense,
            GrayboxEvacuationController3D evacuation)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            if (deployment == null)
                throw new ArgumentNullException(nameof(deployment));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (defense == null)
                throw new ArgumentNullException(nameof(defense));
            if (evacuation == null)
                throw new ArgumentNullException(nameof(evacuation));

            UnbindCheckpointPolicy();
            checkpointPolicy = policy;
            checkpointDefense = defense;
            checkpointSession = session;
            try
            {
                policy.Bind(
                    "city-deployment",
                    listener =>
                        deployment.CheckpointCommitted += listener,
                    listener =>
                        deployment.CheckpointCommitted -= listener);
                policy.Bind(
                    "evacuation",
                    listener =>
                        evacuation.CheckpointCommitted += listener,
                    listener =>
                        evacuation.CheckpointCommitted -= listener);
                defense.FirstMachineGunCompleted +=
                    HandleFirstMachineGunCompleted;
                defense.TutorialCombatStarted +=
                    HandleTutorialCombatStarted;
            }
            catch
            {
                UnbindCheckpointPolicy();
                throw;
            }
        }

        public void UnbindCheckpointPolicy()
        {
            if (checkpointDefense != null)
            {
                checkpointDefense.FirstMachineGunCompleted -=
                    HandleFirstMachineGunCompleted;
                checkpointDefense.TutorialCombatStarted -=
                    HandleTutorialCombatStarted;
            }
            checkpointPolicy?.Unbind();
            checkpointPolicy = null;
            checkpointDefense = null;
            checkpointSession = null;
        }

        public bool QueueNewGameReady(string stableSessionId)
        {
            return checkpointPolicy != null &&
                !string.IsNullOrWhiteSpace(stableSessionId) &&
                checkpointPolicy.QueueCheckpoint(
                    FormalSaveCheckpointReasonIds.NewGameReady,
                    stableSessionId + "|ready");
        }

        public bool FlushPendingCheckpoint()
        {
            return checkpointPolicy != null &&
                checkpointPolicy.FlushPending();
        }

        public bool TryWriteCheckpoint(
            FormalSaveStore store,
            string sessionId,
            string gameVersion,
            IReadOnlyList<string> contentSources,
            FormalSaveCheckpointMetadata checkpoint,
            DateTime utcNow,
            bool archiveLegacy2D = false)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            LastCheckpointStoreResult = null;
            LastCheckpointCaptureResult = CaptureEnvelope(
                sessionId,
                gameVersion,
                contentSources,
                checkpoint,
                utcNow);
            if (!LastCheckpointCaptureResult.Success)
                return false;
            LastCheckpointStoreResult = store.SaveEnvelope(
                LastCheckpointCaptureResult.Envelope,
                archiveLegacy2D);
            return LastCheckpointStoreResult.Success;
        }

        private void HandleFirstMachineGunCompleted(string stableInstanceId)
        {
            checkpointPolicy?.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.FirstMachineGunComplete,
                stableInstanceId);
        }

        private void HandleTutorialCombatStarted(string stableEnemyId)
        {
            checkpointPolicy?.QueueCheckpoint(
                FormalSaveCheckpointReasonIds.TutorialCombatStarted,
                stableEnemyId);
        }

        public GrayboxFormalSaveCoordinatorResult3D RestoreEncoded(
            string encoded)
        {
            FormalSaveDecodeResult decoded =
                FormalSaveCodec.DecodeAny(encoded);
            if (!decoded.Success)
            {
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.DecodeFailed,
                    decoded.Message);
            }
            if (decoded.PayloadKind != FormalSavePayloadKind.Formal3D)
            {
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.DecodeFailed,
                    "存档不是正式 3D 类型");
            }

            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            if (!validation.IsValid)
            {
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.ValidationFailed,
                    ValidationMessage(validation));
            }

            return RestoreValidatedEnvelope(decoded.Envelope);
        }

        public GrayboxFormalSaveCoordinatorResult3D RestoreEnvelope(
            FormalSaveEnvelope envelope)
        {
            if (transactionActive)
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.Busy,
                    "正式存档事务正在进行");
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateEnvelope(envelope);
            if (!validation.IsValid)
            {
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.ValidationFailed,
                    ValidationMessage(validation));
            }
            return RestoreValidatedEnvelope(envelope);
        }

        public GrayboxFormalSaveCoordinatorResult3D CaptureEnvelope(
            string sessionId,
            string gameVersion,
            IReadOnlyList<string> contentSources,
            FormalSaveCheckpointMetadata checkpoint,
            DateTime utcNow)
        {
            if (transactionActive)
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.Busy,
                    "正式存档事务正在进行");
            transactionActive = true;
            try
            {
                if (!TryCapturePayload(
                        sessionId,
                        out FormalThreeDSaveData payload,
                        out GrayboxFormalSaveDomainId3D? failedDomain,
                        out string error))
                {
                    return Failure(
                        GrayboxFormalSaveCoordinatorCode3D.CaptureFailed,
                        error,
                        failedDomain);
                }

                FormalSaveCodec.EnsureCurrentCampaignState(
                    payload,
                    checkpoint);

                string timestamp = FormalSaveCodec.FormatUtcTimestamp(
                    utcNow.ToUniversalTime());
                var sources = CopyAndSort(contentSources);
                var envelope = new FormalSaveEnvelope
                {
                    gameVersion = gameVersion,
                    contentSources = sources,
                    createdAt = timestamp,
                    updatedAt = timestamp,
                    checkpoint = checkpoint,
                    formal3D = payload,
                };
                envelope.payloadHashSha256 =
                    FormalSaveCodec.ComputePayloadHashSha256(payload);
                FormalSaveValidationResult validation =
                    FormalSaveValidator.ValidateEnvelope(envelope);
                if (!validation.IsValid)
                {
                    return Failure(
                        GrayboxFormalSaveCoordinatorCode3D.ValidationFailed,
                        ValidationMessage(validation));
                }
                return Success(envelope);
            }
            catch (Exception exception)
            {
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.CaptureFailed,
                    exception.Message);
            }
            finally
            {
                transactionActive = false;
            }
        }

        private GrayboxFormalSaveCoordinatorResult3D RestoreValidatedEnvelope(
            FormalSaveEnvelope envelope)
        {
            if (transactionActive)
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.Busy,
                    "正式存档事务正在进行");
            transactionActive = true;
            checkpointPolicy?.SetSuppressed(true);
            bool retainSafetyBarrier = false;
            try
            {
                GrayboxFormalSaveCoordinatorResult3D rollbackCapture =
                    CaptureRollbackEnvelope(envelope);
                if (!rollbackCapture.Success)
                    return rollbackCapture;

                if (!TryApplyDomains(
                        envelope.formal3D,
                        out GrayboxFormalSaveDomainId3D? failedDomain,
                        out string error))
                {
                    bool rollbackSucceeded = TryRollbackDomains(
                        rollbackCapture.Envelope.formal3D,
                        out string rollbackError);
                    if (!rollbackSucceeded)
                    {
                        retainSafetyBarrier = true;
                        return new GrayboxFormalSaveCoordinatorResult3D(
                            false,
                            GrayboxFormalSaveCoordinatorCode3D.RollbackFailed,
                            CombineErrors(error, rollbackError),
                            rollbackCapture.Envelope,
                            failedDomain,
                            true,
                            false);
                    }
                    try
                    {
                        derivedState.RebuildDerivedState();
                    }
                    catch (Exception rebuildException)
                    {
                        retainSafetyBarrier = true;
                        return new GrayboxFormalSaveCoordinatorResult3D(
                            false,
                            GrayboxFormalSaveCoordinatorCode3D.RollbackFailed,
                            CombineErrors(
                                error,
                                rebuildException.Message),
                            rollbackCapture.Envelope,
                            failedDomain,
                            true,
                            false);
                    }
                    return new GrayboxFormalSaveCoordinatorResult3D(
                        false,
                        GrayboxFormalSaveCoordinatorCode3D.ApplyFailed,
                        error,
                        rollbackCapture.Envelope,
                        failedDomain,
                        true,
                        true);
                }

                try
                {
                    derivedState.RebuildDerivedState();
                }
                catch (Exception rebuildException)
                {
                    bool rollbackSucceeded =
                        TryRollbackAfterDerivedFailure(
                            rollbackCapture.Envelope.formal3D,
                            out string rollbackError);
                    if (!rollbackSucceeded)
                    {
                        retainSafetyBarrier = true;
                        return new GrayboxFormalSaveCoordinatorResult3D(
                            false,
                            GrayboxFormalSaveCoordinatorCode3D.RollbackFailed,
                            CombineErrors(
                                rebuildException.Message,
                                rollbackError),
                            rollbackCapture.Envelope,
                            null,
                            true,
                            false);
                    }
                    return new GrayboxFormalSaveCoordinatorResult3D(
                        false,
                        GrayboxFormalSaveCoordinatorCode3D.ApplyFailed,
                        rebuildException.Message,
                        rollbackCapture.Envelope,
                        null,
                        true,
                        true);
                }
                checkpointPolicy?.TryRestoreBaseline(envelope.checkpoint);
                checkpointSession?.TryRestoreCheckpointRuleTime(
                    envelope.checkpoint.ruleTimeSeconds,
                    out _);
                retainedCampaign = FormalSaveCodec.CloneCampaignState(
                    envelope.formal3D.defenseCampaign);
                try
                {
                    RestoreCompleted?.Invoke();
                }
                catch
                {
                    // Completion observers cannot rewrite committed truth.
                }
                return Success(envelope);
            }
            catch (Exception exception)
            {
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.ApplyFailed,
                    exception.Message);
            }
            finally
            {
                if (!retainSafetyBarrier)
                {
                    transactionActive = false;
                    checkpointPolicy?.SetSuppressed(false);
                }
            }
        }

        private GrayboxFormalSaveCoordinatorResult3D
            CaptureRollbackEnvelope(FormalSaveEnvelope target)
        {
            string sessionId = target?.formal3D?.sessionId;
            if (!TryCapturePayload(
                    sessionId,
                    out FormalThreeDSaveData payload,
                    out GrayboxFormalSaveDomainId3D? failedDomain,
                    out string error))
            {
                return Failure(
                    GrayboxFormalSaveCoordinatorCode3D.CaptureFailed,
                    error,
                    failedDomain);
            }

            FormalSaveCodec.EnsureCurrentCampaignState(
                payload,
                target.checkpoint);

            return Success(new FormalSaveEnvelope
            {
                gameVersion = target.gameVersion,
                contentSources = CopyAndSort(target.contentSources),
                createdAt = target.createdAt,
                updatedAt = target.updatedAt,
                checkpoint = target.checkpoint,
                formal3D = payload,
                payloadHashSha256 =
                    FormalSaveCodec.ComputePayloadHashSha256(payload),
            });
        }

        private bool TryCapturePayload(
            string sessionId,
            out FormalThreeDSaveData payload,
            out GrayboxFormalSaveDomainId3D? failedDomain,
            out string error)
        {
            payload = new FormalThreeDSaveData { sessionId = sessionId };
            for (var index = 0; index < domains.Length; index++)
            {
                try
                {
                    if (domains[index].TryCapture(payload, out error))
                        continue;
                    failedDomain = domains[index].DomainId;
                    return false;
                }
                catch (Exception exception)
                {
                    failedDomain = domains[index].DomainId;
                    error = exception.Message;
                    return false;
                }
            }
            if (payload.defenseCampaign == null && retainedCampaign != null)
            {
                payload.defenseCampaign = FormalSaveCodec.CloneCampaignState(
                    retainedCampaign);
            }
            failedDomain = null;
            error = string.Empty;
            return true;
        }

        private bool TryApplyDomains(
            FormalThreeDSaveData payload,
            out GrayboxFormalSaveDomainId3D? failedDomain,
            out string error)
        {
            for (var index = 0; index < domains.Length; index++)
            {
                try
                {
                    if (domains[index].TryApply(payload, out error))
                        continue;
                    failedDomain = domains[index].DomainId;
                    return false;
                }
                catch (Exception exception)
                {
                    failedDomain = domains[index].DomainId;
                    error = exception.Message;
                    return false;
                }
            }
            failedDomain = null;
            error = string.Empty;
            return true;
        }

        private bool TryRollbackDomains(
            FormalThreeDSaveData rollback,
            out string error)
        {
            for (var index = 0; index < domains.Length; index++)
            {
                try
                {
                    if (!domains[index].TryApply(rollback, out error))
                        return false;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private bool TryRollbackAfterDerivedFailure(
            FormalThreeDSaveData rollback,
            out string error)
        {
            if (!TryRollbackDomains(rollback, out error))
                return false;
            try
            {
                derivedState.RebuildDerivedState();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static IFormalThreeDSaveDomain[] ValidateAndOrderDomains(
            IReadOnlyList<IFormalThreeDSaveDomain> source)
        {
            if (source.Count != OrderedDomainIds.Count)
                throw new ArgumentException(
                    "正式存档领域必须完整且唯一",
                    nameof(source));
            var byId = new Dictionary<
                GrayboxFormalSaveDomainId3D,
                IFormalThreeDSaveDomain>();
            for (var index = 0; index < source.Count; index++)
            {
                IFormalThreeDSaveDomain domain = source[index] ??
                    throw new ArgumentException(
                        "正式存档领域不能为空",
                        nameof(source));
                if (!byId.TryAdd(domain.DomainId, domain))
                    throw new ArgumentException(
                        "正式存档领域不能重复",
                        nameof(source));
            }

            var ordered = new IFormalThreeDSaveDomain[
                OrderedDomainIds.Count];
            for (var index = 0; index < ordered.Length; index++)
            {
                if (!byId.TryGetValue(
                        OrderedDomainIds[index],
                        out ordered[index]))
                {
                    throw new ArgumentException(
                        "正式存档领域缺失：" + OrderedDomainIds[index],
                        nameof(source));
                }
            }
            return ordered;
        }

        private sealed class DelegateDomain : IFormalThreeDSaveDomain
        {
            private readonly DomainCapture capture;
            private readonly DomainApply apply;

            public DelegateDomain(
                GrayboxFormalSaveDomainId3D domainId,
                DomainCapture capture,
                DomainApply apply)
            {
                DomainId = domainId;
                this.capture = capture ??
                    throw new ArgumentNullException(nameof(capture));
                this.apply = apply ??
                    throw new ArgumentNullException(nameof(apply));
            }

            public GrayboxFormalSaveDomainId3D DomainId { get; }

            public bool TryCapture(
                FormalThreeDSaveData destination,
                out string error)
            {
                capture(destination);
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                FormalThreeDSaveData source,
                out string error)
            {
                return apply(source, out error);
            }
        }

        private static string[] CopyAndSort(
            IReadOnlyList<string> source)
        {
            if (source == null) return Array.Empty<string>();
            var result = new string[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index];
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static string ValidationMessage(
            FormalSaveValidationResult validation)
        {
            if (validation == null) return "正式存档验证失败";
            return string.IsNullOrEmpty(validation.FieldPath)
                ? validation.Message
                : validation.FieldPath + ": " + validation.Message;
        }

        private static string CombineErrors(
            string applyError,
            string rollbackError)
        {
            return (applyError ?? string.Empty) + "；回滚失败：" +
                   (rollbackError ?? string.Empty);
        }

        private static GrayboxFormalSaveCoordinatorResult3D Success(
            FormalSaveEnvelope envelope)
        {
            return new GrayboxFormalSaveCoordinatorResult3D(
                true,
                GrayboxFormalSaveCoordinatorCode3D.Success,
                string.Empty,
                envelope,
                null,
                false,
                false);
        }

        private static GrayboxFormalSaveCoordinatorResult3D Failure(
            GrayboxFormalSaveCoordinatorCode3D code,
            string message,
            GrayboxFormalSaveDomainId3D? failedDomain = null)
        {
            return new GrayboxFormalSaveCoordinatorResult3D(
                false,
                code,
                message,
                null,
                failedDomain,
                false,
                false);
        }
    }
}
