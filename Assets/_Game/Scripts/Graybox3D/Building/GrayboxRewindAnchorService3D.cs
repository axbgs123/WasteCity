using System;
using System.Globalization;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxRewindAnchorServiceCode3D
    {
        Created,
        Replaced,
        Cleared,
        ReadSucceeded,
        WrongFate,
        SafetyBlocked,
        NoAnchor,
        InvalidAnchor,
        SessionMismatch,
        CaptureFailed,
        StoreFailed,
        AttentionFailed,
        RestoreFailed,
    }

    public sealed class GrayboxRewindAnchorServiceResult3D
    {
        internal GrayboxRewindAnchorServiceResult3D(
            GrayboxRewindAnchorServiceCode3D code,
            bool success,
            string message,
            string diagnostic = null)
        {
            Code = code;
            Success = success;
            Message = message ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public GrayboxRewindAnchorServiceCode3D Code { get; }
        public bool Success { get; }
        public string Message { get; }
        public string Diagnostic { get; }
    }

    public sealed class GrayboxRewindAnchorService3D
    {
        public const string StableAnchorId = "rewind-anchor.slot.0001";
        public const string SecondStableAnchorId = "rewind-anchor.slot.0002";
        public const string SaveTransactionSafetyCode = "save-transaction";
        public const string DeploymentSafetyCode = "deployment-transition";
        public const string EvacuationSafetyCode = "evacuation";
        public const string CombatSafetyCode = "combat";

        private const string AttentionReasonId =
            "core.attention.fate.rewind-anchor-used";

        private readonly FormalRewindAnchorStore store;
        private readonly GrayboxFormalSaveCoordinator3D coordinator;
        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;
        private readonly Func<string> safetyCodeProvider;
        private readonly Func<string> currentSessionIdProvider;
        private readonly FormalRewindAnchorMetadataRuntime metadata;

        public GrayboxRewindAnchorService3D(
            FormalRewindAnchorStore store,
            GrayboxFormalSaveCoordinator3D coordinator,
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            Func<string> safetyCodeProvider,
            Func<string> currentSessionIdProvider)
            : this(
                store,
                coordinator,
                attention,
                fate,
                safetyCodeProvider,
                currentSessionIdProvider,
                null)
        {
        }

        public GrayboxRewindAnchorService3D(
            FormalRewindAnchorStore store,
            GrayboxFormalSaveCoordinator3D coordinator,
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            Func<string> safetyCodeProvider,
            Func<string> currentSessionIdProvider,
            FormalRewindAnchorMetadataRuntime metadata)
        {
            this.store = store ??
                throw new ArgumentNullException(nameof(store));
            this.coordinator = coordinator ??
                throw new ArgumentNullException(nameof(coordinator));
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.fate = fate ??
                throw new ArgumentNullException(nameof(fate));
            this.safetyCodeProvider = safetyCodeProvider ??
                throw new ArgumentNullException(nameof(safetyCodeProvider));
            this.currentSessionIdProvider = currentSessionIdProvider ??
                throw new ArgumentNullException(
                    nameof(currentSessionIdProvider));
            this.metadata = metadata;
        }

        public GrayboxRewindAnchorServiceResult3D Create(
            string gameVersion,
            string[] contentSources,
            FormalSaveCheckpointMetadata checkpoint,
            DateTime utcNow)
        {
            GrayboxRewindAnchorServiceResult3D unavailable =
                CheckAvailability();
            if (unavailable != null) return unavailable;

            string sessionId = CurrentSessionId();
            if (sessionId == null)
                return SessionMismatch("当前正式会话身份无效");

            int slot = SelectCreationSlot();
            string anchorId = AnchorIdForSlot(slot);
            FormalRewindAnchorStoreResult existing = store.Load(slot);
            bool replacing = existing.Success;
            if (existing.Code ==
                    FormalRewindAnchorStoreCode.UnsupportedFutureSchema ||
                existing.Code == FormalRewindAnchorStoreCode.DiskReadFailed)
            {
                return StoreFailure(existing);
            }

            GrayboxFormalSaveCoordinatorResult3D captured =
                coordinator.CaptureEnvelope(
                    sessionId,
                    gameVersion,
                    contentSources,
                    checkpoint,
                    utcNow);
            if (!captured.Success || captured.Envelope == null)
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.CaptureFailed,
                    "无法捕获当前世界锚点",
                    captured.Message);
            }

            FormalRewindAnchorMetadataUpsertPlan metadataPlan = null;
            if (metadata != null && !metadata.TryPrepareUpsert(
                    anchorId,
                    FormalRewindAnchorStore.InternalDirectoryName + "/" +
                        (slot == 1
                            ? FormalRewindAnchorStore.FileName
                            : FormalRewindAnchorStore.SecondFileName),
                    sessionId,
                    captured.Envelope.payloadHashSha256,
                    captured.Envelope.checkpoint,
                    out metadataPlan,
                    out string metadataError))
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.CaptureFailed,
                    "无法准备回溯锚点元数据",
                    metadataError);
            }

            FormalRewindAnchorStoreResult saved =
                store.Save(captured.Envelope, slot);
            if (!saved.Success) return StoreFailure(saved);
            if (metadata != null &&
                !metadata.TryCommitUpsert(metadataPlan, out string commitError))
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.StoreFailed,
                    "锚点已写入但元数据提交失败",
                    commitError);
            }
            return new GrayboxRewindAnchorServiceResult3D(
                replacing
                    ? GrayboxRewindAnchorServiceCode3D.Replaced
                    : GrayboxRewindAnchorServiceCode3D.Created,
                true,
                replacing ? "已替换回溯锚点" : "已创建回溯锚点");
        }

        public GrayboxRewindAnchorServiceResult3D Read()
        {
            return Read(StableAnchorId);
        }

        public GrayboxRewindAnchorServiceResult3D Read(string anchorId)
        {
            GrayboxRewindAnchorServiceResult3D unavailable =
                CheckAvailability();
            if (unavailable != null) return unavailable;

            int slot = SlotForAnchorId(anchorId);
            if (slot == 0)
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.InvalidAnchor,
                    "回溯锚点稳定身份无效");
            FormalRewindAnchorStoreResult loaded = store.Load(slot);
            if (!loaded.Success || loaded.Envelope?.formal3D == null)
                return LoadFailure(loaded);

            string currentSessionId = CurrentSessionId();
            if (currentSessionId == null ||
                !string.Equals(
                    loaded.Envelope.formal3D.sessionId,
                    currentSessionId,
                    StringComparison.Ordinal))
            {
                return SessionMismatch("回溯锚点不属于当前正式会话");
            }

            if (!TryCreateAttentionCandidate(
                    anchorId,
                    out FormalThreeDProgressionSaveData candidate,
                    out string attentionError))
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.AttentionFailed,
                    "无法保留当前关注度并读取锚点",
                    attentionError);
            }

            GrayboxFormalSaveCoordinatorResult3D currentCapture =
                coordinator.CaptureEnvelope(
                    currentSessionId,
                    loaded.Envelope.gameVersion,
                    loaded.Envelope.contentSources ?? Array.Empty<string>(),
                    loaded.Envelope.checkpoint,
                    DateTime.UtcNow);
            FormalThreeDProgressionSaveData currentProgression =
                currentCapture.Envelope?.formal3D?.progression;
            if (!currentCapture.Success || currentProgression == null)
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.CaptureFailed,
                    "无法捕获当前命轨与文明状态",
                    currentCapture.Message);
            }

            FormalSaveEnvelope target = loaded.Envelope;
            target.formal3D.progression.attention = candidate.attention;
            target.formal3D.progression.fate = Clone(
                currentProgression.fate);
            target.formal3D.progression.civilization = Clone(
                currentProgression.civilization);
            target.formal3D.progression.fateEffects.rewindAnchors = Clone(
                currentProgression.fateEffects.rewindAnchors);
            if (!GrayboxFormalProgressionSaveAdapter3D
                    .TryMergeVoidChestMonotonic(
                        currentProgression.voidChest,
                        target.formal3D.progression.voidChest,
                        out FormalThreeDVoidChestSaveData mergedVoidChest,
                        out string mergeError))
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.InvalidAnchor,
                    "无法保留当前虚空宝箱幂等状态",
                    mergeError);
            }
            target.formal3D.progression.voidChest = mergedVoidChest;
            target.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(target.formal3D);
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateEnvelope(target);
            if (!validation.IsValid)
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.InvalidAnchor,
                    "合并关注度后的回溯锚点无效",
                    ValidationDiagnostic(validation));
            }

            GrayboxFormalSaveCoordinatorResult3D restored =
                coordinator.RestoreEnvelope(target);
            if (!restored.Success)
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.RestoreFailed,
                    "无法读取回溯锚点，当前世界保持不变",
                    restored.Message);
            }
            return new GrayboxRewindAnchorServiceResult3D(
                GrayboxRewindAnchorServiceCode3D.ReadSucceeded,
                true,
                "已读取回溯锚点，关注度增加 12");
        }

        private static T Clone<T>(T value) where T : class
        {
            return value == null
                ? default
                : UnityEngine.JsonUtility.FromJson<T>(
                    UnityEngine.JsonUtility.ToJson(value, false));
        }

        public GrayboxRewindAnchorServiceResult3D Clear()
        {
            GrayboxRewindAnchorServiceResult3D unavailable =
                CheckAvailability();
            if (unavailable != null) return unavailable;

            FormalRewindAnchorMetadataClearPlan metadataPlan = null;
            if (metadata != null && !metadata.TryPrepareClear(
                    out metadataPlan,
                    out string metadataError))
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.StoreFailed,
                    "无法准备清理回溯锚点元数据",
                    metadataError);
            }

            FormalRewindAnchorStoreResult cleared = store.Clear(1);
            if (!cleared.Success) return StoreFailure(cleared);
            if (metadata?.MaximumAnchors == 2)
            {
                cleared = store.Clear(2);
                if (!cleared.Success) return StoreFailure(cleared);
            }
            if (metadata != null && !metadata.TryCommitClear(
                    metadataPlan,
                    out string commitError))
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.StoreFailed,
                    "锚点文件已清理但元数据提交失败",
                    commitError);
            }
            return new GrayboxRewindAnchorServiceResult3D(
                GrayboxRewindAnchorServiceCode3D.Cleared,
                true,
                "已清理回溯锚点");
        }

        private GrayboxRewindAnchorServiceResult3D CheckAvailability()
        {
            FormalFateSnapshot fateSnapshot = fate.Capture();
            if (!fateSnapshot.HasSelection ||
                fateSnapshot.Level < 1 ||
                !string.Equals(
                    fateSnapshot.SelectedId,
                    FormalFateCatalog.RewindAnchorId,
                    StringComparison.Ordinal))
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.WrongFate,
                    "只有已激活的回溯锚点命轨可以使用锚点");
            }

            string safetyCode = safetyCodeProvider() ?? string.Empty;
            if (safetyCode.Length == 0) return null;
            return Failure(
                GrayboxRewindAnchorServiceCode3D.SafetyBlocked,
                SafetyMessage(safetyCode) + "，无法使用回溯锚点");
        }

        private bool TryCreateAttentionCandidate(
            string anchorId,
            out FormalThreeDProgressionSaveData candidate,
            out string error)
        {
            candidate = null;
            var candidateAttention = new FormalAttentionRuntime();
            if (!candidateAttention.TryRestore(attention.Capture(), out error))
                return false;

            string eventKey = "rewind-anchor-read:" + anchorId + ":" +
                (candidateAttention.Revision + 1UL).ToString(
                    CultureInfo.InvariantCulture);
            if (!candidateAttention.TryApply(
                    AttentionReasonId,
                    eventKey,
                    out error))
            {
                return false;
            }

            var candidateFate = new FormalFateRuntime();
            if (!candidateFate.TryRestore(fate.Capture(), out error))
                return false;
            var candidateMetadata = new FormalRewindAnchorMetadataRuntime(
                metadata?.MaximumAnchors == 2 ? 2 : 1);
            if (metadata != null && !candidateMetadata.TryRestore(
                    metadata.Capture(),
                    out error))
            {
                return false;
            }
            candidate = new GrayboxFormalProgressionSaveAdapter3D(
                    candidateAttention,
                    candidateFate,
                    new PocketUniverseFateEffect(),
                    new FormalVoidDebtRuntime(),
                    candidateMetadata)
                .Capture();
            error = string.Empty;
            return true;
        }

        private string CurrentSessionId()
        {
            string value = currentSessionIdProvider();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private int SelectCreationSlot()
        {
            if (metadata == null || metadata.MaximumAnchors == 1) return 1;
            var snapshot = metadata.Capture();
            bool hasOne = false;
            bool hasTwo = false;
            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                hasOne |= snapshot.Entries[index].AnchorId == StableAnchorId;
                hasTwo |= snapshot.Entries[index].AnchorId ==
                    SecondStableAnchorId;
            }
            if (!hasOne) return 1;
            if (!hasTwo) return 2;
            return SlotForAnchorId(snapshot.Entries[0].AnchorId);
        }

        private static int SlotForAnchorId(string anchorId)
        {
            if (string.Equals(anchorId, StableAnchorId,
                    StringComparison.Ordinal)) return 1;
            return string.Equals(anchorId, SecondStableAnchorId,
                    StringComparison.Ordinal) ? 2 : 0;
        }

        private static string AnchorIdForSlot(int slot) =>
            slot == 2 ? SecondStableAnchorId : StableAnchorId;

        private static string SafetyMessage(string code)
        {
            switch (code)
            {
                case SaveTransactionSafetyCode:
                    return "存档事务正在进行";
                case DeploymentSafetyCode:
                    return "城市正在部署或收拢";
                case EvacuationSafetyCode:
                    return "撤离流程正在进行";
                case CombatSafetyCode:
                    return "战斗进行中";
                default:
                    return "锚点安全状态无效";
            }
        }

        private static GrayboxRewindAnchorServiceResult3D LoadFailure(
            FormalRewindAnchorStoreResult result)
        {
            if (result == null ||
                result.Code == FormalRewindAnchorStoreCode.NoAnchor)
            {
                return Failure(
                    GrayboxRewindAnchorServiceCode3D.NoAnchor,
                    "尚未创建可读取的回溯锚点");
            }
            return result.Code ==
                       FormalRewindAnchorStoreCode.InvalidEnvelope ||
                   result.Code ==
                       FormalRewindAnchorStoreCode.UnsupportedFutureSchema
                ? Failure(
                    GrayboxRewindAnchorServiceCode3D.InvalidAnchor,
                    "回溯锚点无效或版本不兼容",
                    result.Diagnostic)
                : StoreFailure(result);
        }

        private static GrayboxRewindAnchorServiceResult3D StoreFailure(
            FormalRewindAnchorStoreResult result)
        {
            return Failure(
                GrayboxRewindAnchorServiceCode3D.StoreFailed,
                result?.Message ?? "回溯锚点存储不可用",
                result?.Diagnostic);
        }

        private static GrayboxRewindAnchorServiceResult3D SessionMismatch(
            string message)
        {
            return Failure(
                GrayboxRewindAnchorServiceCode3D.SessionMismatch,
                message);
        }

        private static string ValidationDiagnostic(
            FormalSaveValidationResult validation)
        {
            if (validation == null) return "验证结果为空";
            return validation.Error + ": " + validation.FieldPath + " " +
                validation.Message;
        }

        private static GrayboxRewindAnchorServiceResult3D Failure(
            GrayboxRewindAnchorServiceCode3D code,
            string message,
            string diagnostic = null)
        {
            return new GrayboxRewindAnchorServiceResult3D(
                code,
                false,
                message,
                diagnostic);
        }
    }
}
