using System;
using System.Globalization;
using System.IO;
using System.Text;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Persistence
{
    public enum FormalSaveStoreCode
    {
        SaveSucceeded,
        LoadSucceeded,
        BackupRecovered,
        NoValidSave,
        Legacy2DOnly,
        UnsupportedFutureSchema,
        DiskWriteFailed,
        DiskReadFailed,
        CorruptNoBackup,
        IncompatibleRuntime,
    }

    public enum FormalSaveWriteIntent
    {
        ContinueProgress,
        StartNewProgress,
    }

    public interface IFormalSaveClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class FormalSaveStoreResult
    {
        internal FormalSaveStoreResult(
            FormalSaveStoreCode code,
            bool success,
            bool canContinue,
            string message,
            FormalSavePayloadKind payloadKind,
            FormalSaveEnvelope envelope,
            FormalSaveData legacy2D,
            bool usedBackup,
            string diagnostic = null)
        {
            Code = code;
            Success = success;
            CanContinue = canContinue;
            Message = message ?? string.Empty;
            PayloadKind = payloadKind;
            Envelope = envelope;
            Legacy2D = legacy2D;
            UsedBackup = usedBackup;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public FormalSaveStoreCode Code { get; }
        public bool Success { get; }
        public bool CanContinue { get; }
        public string Message { get; }
        public FormalSavePayloadKind PayloadKind { get; }
        public FormalSaveEnvelope Envelope { get; }
        public FormalSaveData Legacy2D { get; }
        public bool UsedBackup { get; }
        public string Diagnostic { get; }
    }

    public sealed class FormalSaveStore
    {
        public const string FileName = "formal-world.json";

        private static readonly UTF8Encoding Utf8 =
            new UTF8Encoding(false, true);
        private readonly IFormalSaveFileSystem fileSystem;
        private readonly IFormalSaveClock clock;
        private readonly FormalSaveFileTransaction transaction;
        private readonly string primaryPath;
        private readonly string backupPath;

        public FormalSaveStore(
            string directory,
            IFormalSaveFileSystem fileSystem = null,
            IFormalSaveClock clock = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException(
                    "存档目录不能为空",
                    nameof(directory));
            this.fileSystem = fileSystem ??
                new SystemFormalSaveFileSystem();
            this.clock = clock ?? new SystemFormalSaveClock();
            string root = Path.GetFullPath(directory);
            primaryPath = Path.Combine(root, FileName);
            backupPath = primaryPath + ".bak";
            transaction = new FormalSaveFileTransaction(this.fileSystem);
        }

        public FormalSaveStoreResult Probe(
            FormalSavePayloadKind requiredKind = FormalSavePayloadKind.None)
        {
            return ReadBestCandidate(requiredKind);
        }

        public FormalSaveStoreResult Load(
            FormalSavePayloadKind requiredKind = FormalSavePayloadKind.None)
        {
            return ReadBestCandidate(requiredKind);
        }

        public FormalSaveStoreResult SaveEnvelope(
            FormalSaveEnvelope envelope,
            bool archiveLegacy2D = false,
            FormalSaveWriteIntent writeIntent =
                FormalSaveWriteIntent.ContinueProgress)
        {
            if (envelope == null || envelope.formal3D == null)
                return FailedWrite("正式 3D 存档数据为空");
            if (!Enum.IsDefined(
                    typeof(FormalSaveWriteIntent),
                    writeIntent))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(writeIntent));
            }

            FormalSaveStoreResult existing = Load(
                FormalSavePayloadKind.Formal3D);
            if (existing.Code ==
                FormalSaveStoreCode.UnsupportedFutureSchema)
                return existing;
            if (existing.Code == FormalSaveStoreCode.Legacy2DOnly)
            {
                if (!archiveLegacy2D) return existing;
                FormalSaveFileTransactionResult archived =
                    ArchiveLegacy(existing);
                if (!archived.Success)
                    return FailedWrite(archived.Diagnostic);
            }

            string originalCreatedAt = envelope.createdAt;
            string originalUpdatedAt = envelope.updatedAt;
            int originalSchemaVersion = envelope.saveSchemaVersion;
            string originalRuntimeKind = envelope.runtimeKind;
            string originalPayloadHash = envelope.payloadHashSha256;
            DateTime now = clock.UtcNow.ToUniversalTime();
            bool startsNewProgress =
                writeIntent == FormalSaveWriteIntent.StartNewProgress;
            envelope.createdAt = !startsNewProgress &&
                                 existing.Success &&
                                 existing.Envelope != null
                ? existing.Envelope.createdAt
                : FormalSaveCodec.FormatUtcTimestamp(now);
            envelope.updatedAt = FormalSaveCodec.FormatUtcTimestamp(now);
            envelope.saveSchemaVersion =
                FormalSaveEnvelope.CurrentSchemaVersion;
            envelope.runtimeKind =
                FormalSaveEnvelope.FormalThreeDRuntimeKind;
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);

            string json = FormalSaveCodec.EncodeEnvelope(envelope);
            byte[] bytes = Utf8.GetBytes(json);
            if (!ValidateBytes(bytes))
            {
                RestoreEnvelopeMetadata(
                    envelope,
                    originalCreatedAt,
                    originalUpdatedAt,
                    originalSchemaVersion,
                    originalRuntimeKind,
                    originalPayloadHash);
                return FailedWrite("保存前语义验证失败");
            }

            FormalSaveFileTransactionResult committed =
                transaction.Commit(primaryPath, bytes, ValidateBytes);
            if (committed.Success)
            {
                return Result(
                    FormalSaveStoreCode.SaveSucceeded,
                    true,
                    true,
                    "游戏已保存",
                    FormalSavePayloadKind.Formal3D,
                    envelope,
                    null,
                    false);
            }
            RestoreEnvelopeMetadata(
                envelope,
                originalCreatedAt,
                originalUpdatedAt,
                originalSchemaVersion,
                originalRuntimeKind,
                originalPayloadHash);
            return FailedWrite(committed.Diagnostic);
        }

        public FormalSaveStoreResult SaveLegacy(FormalSaveData legacy)
        {
            if (legacy == null)
                return FailedWrite("旧版存档数据为空");
            FormalSaveStoreResult existing = Load();
            if (existing.Code ==
                FormalSaveStoreCode.UnsupportedFutureSchema)
                return existing;
            if (existing.Success &&
                existing.PayloadKind == FormalSavePayloadKind.Formal3D)
                return Result(
                    FormalSaveStoreCode.IncompatibleRuntime,
                    false,
                    false,
                    "正式 3D 存档不能由旧版 2D 覆盖",
                    existing.PayloadKind,
                    existing.Envelope,
                    null,
                    existing.UsedBackup);

            byte[] bytes = Utf8.GetBytes(FormalSaveCodec.Encode(legacy));
            if (!ValidateBytes(bytes))
                return FailedWrite("旧版存档验证失败");
            FormalSaveFileTransactionResult committed =
                transaction.Commit(primaryPath, bytes, ValidateBytes);
            return committed.Success
                ? Result(
                    FormalSaveStoreCode.SaveSucceeded,
                    true,
                    true,
                    "游戏已保存",
                    FormalSavePayloadKind.Legacy2D,
                    null,
                    legacy,
                    false)
                : FailedWrite(committed.Diagnostic);
        }

        private FormalSaveStoreResult ReadBestCandidate(
            FormalSavePayloadKind requiredKind)
        {
            Candidate primary = ReadCandidate(primaryPath);
            Candidate backup = ReadCandidate(backupPath);

            if (primary.Valid)
                return CandidateResult(primary, requiredKind, false);
            if (primary.Future)
                return Result(
                    FormalSaveStoreCode.UnsupportedFutureSchema,
                    false,
                    false,
                    "存档版本过新，请更新游戏后重试",
                    FormalSavePayloadKind.None,
                    null,
                    null,
                    false);
            if (backup.Valid)
                return CandidateResult(backup, requiredKind, true);
            if (backup.Future)
                return Result(
                    FormalSaveStoreCode.UnsupportedFutureSchema,
                    false,
                    false,
                    "存档版本过新，请更新游戏后重试",
                    FormalSavePayloadKind.None,
                    null,
                    null,
                    true);
            if (primary.ReadFailed || backup.ReadFailed)
                return Result(
                    FormalSaveStoreCode.DiskReadFailed,
                    false,
                    false,
                    "无法读取存档，请检查权限或磁盘",
                    FormalSavePayloadKind.None,
                    null,
                    null,
                    false,
                    primary.Diagnostic + backup.Diagnostic);
            if (!primary.Exists && !backup.Exists)
                return Result(
                    FormalSaveStoreCode.NoValidSave,
                    false,
                    false,
                    "没有可继续的有效存档",
                    FormalSavePayloadKind.None,
                    null,
                    null,
                    false);
            return Result(
                FormalSaveStoreCode.CorruptNoBackup,
                false,
                false,
                "存档已损坏，且没有可用备份",
                FormalSavePayloadKind.None,
                null,
                null,
                false);
        }

        private FormalSaveFileTransactionResult ArchiveLegacy(
            FormalSaveStoreResult existing)
        {
            string sourcePath = existing.UsedBackup
                ? backupPath
                : primaryPath;
            try
            {
                byte[] bytes = fileSystem.ReadAllBytes(sourcePath);
                if (!ValidateBytes(bytes))
                    return new FormalSaveFileTransactionResult(
                        false,
                        FormalSaveTransactionStage.ValidateTemporary,
                        "旧版存档归档前复读验证失败");
                FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                    Utf8.GetString(bytes));
                if (!decoded.Success ||
                    decoded.PayloadKind != FormalSavePayloadKind.Legacy2D)
                    return new FormalSaveFileTransactionResult(
                        false,
                        FormalSaveTransactionStage.ValidateTemporary,
                        "旧版存档身份不一致");
                string timestamp = clock.UtcNow.ToUniversalTime().ToString(
                    "yyyyMMdd'T'HHmmssfffffff'Z'",
                    CultureInfo.InvariantCulture);
                string archivePath = Path.Combine(
                    Path.GetDirectoryName(primaryPath),
                    "formal-world.legacy-schema-" +
                    decoded.Legacy2D.schema + "-" + timestamp + ".json");
                return transaction.Commit(
                    archivePath,
                    bytes,
                    ValidateBytes);
            }
            catch (Exception exception)
            {
                return new FormalSaveFileTransactionResult(
                    false,
                    FormalSaveTransactionStage.WriteTemporary,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private FormalSaveStoreResult CandidateResult(
            Candidate candidate,
            FormalSavePayloadKind requiredKind,
            bool usedBackup)
        {
            FormalSavePayloadKind actual = candidate.Decoded.PayloadKind;
            if (requiredKind != FormalSavePayloadKind.None &&
                actual != requiredKind)
            {
                if (requiredKind == FormalSavePayloadKind.Formal3D &&
                    actual == FormalSavePayloadKind.Legacy2D)
                    return Result(
                        FormalSaveStoreCode.Legacy2DOnly,
                        false,
                        false,
                        "检测到旧版 2D 存档，不能直接用于当前 3D 游戏",
                        actual,
                        null,
                        candidate.Decoded.Legacy2D,
                        usedBackup);
                return Result(
                    FormalSaveStoreCode.IncompatibleRuntime,
                    false,
                    false,
                    "存档与当前运行时不兼容",
                    actual,
                    candidate.Decoded.Envelope,
                    candidate.Decoded.Legacy2D,
                    usedBackup);
            }
            return Result(
                usedBackup
                    ? FormalSaveStoreCode.BackupRecovered
                    : FormalSaveStoreCode.LoadSucceeded,
                true,
                true,
                usedBackup
                    ? "主存档损坏，已恢复备份"
                    : "已继续最近进度",
                actual,
                candidate.Decoded.Envelope,
                candidate.Decoded.Legacy2D,
                usedBackup);
        }

        private Candidate ReadCandidate(string path)
        {
            if (!fileSystem.FileExists(path)) return Candidate.Missing();
            try
            {
                byte[] bytes = fileSystem.ReadAllBytes(path);
                string json = Utf8.GetString(bytes);
                FormalSaveDecodeResult decoded =
                    FormalSaveCodec.DecodeAny(json);
                if (!decoded.Success)
                    return decoded.Error ==
                           FormalSaveDecodeError.UnsupportedFutureSchema
                        ? Candidate.FutureSchema()
                        : Candidate.Invalid();
                FormalSaveValidationResult validation =
                    FormalSaveValidator.ValidateDecoded(decoded);
                return validation.IsValid
                    ? Candidate.ValidDocument(decoded)
                    : Candidate.Invalid();
            }
            catch (Exception exception)
            {
                return Candidate.ReadError(
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static bool ValidateBytes(byte[] bytes)
        {
            if (bytes == null) return false;
            try
            {
                string json = Utf8.GetString(bytes);
                FormalSaveDecodeResult decoded =
                    FormalSaveCodec.DecodeAny(json);
                return decoded.Success &&
                       FormalSaveValidator.ValidateDecoded(decoded).IsValid;
            }
            catch
            {
                return false;
            }
        }

        private static void RestoreEnvelopeMetadata(
            FormalSaveEnvelope envelope,
            string createdAt,
            string updatedAt,
            int schemaVersion,
            string runtimeKind,
            string payloadHash)
        {
            envelope.createdAt = createdAt;
            envelope.updatedAt = updatedAt;
            envelope.saveSchemaVersion = schemaVersion;
            envelope.runtimeKind = runtimeKind;
            envelope.payloadHashSha256 = payloadHash;
        }

        private static FormalSaveStoreResult FailedWrite(string diagnostic)
        {
            return Result(
                FormalSaveStoreCode.DiskWriteFailed,
                false,
                false,
                "保存失败，原存档未被覆盖",
                FormalSavePayloadKind.None,
                null,
                null,
                false,
                diagnostic);
        }

        private static FormalSaveStoreResult Result(
            FormalSaveStoreCode code,
            bool success,
            bool canContinue,
            string message,
            FormalSavePayloadKind payloadKind,
            FormalSaveEnvelope envelope,
            FormalSaveData legacy,
            bool usedBackup,
            string diagnostic = null)
        {
            return new FormalSaveStoreResult(
                code,
                success,
                canContinue,
                message,
                payloadKind,
                envelope,
                legacy,
                usedBackup,
                diagnostic);
        }

        private sealed class SystemFormalSaveClock : IFormalSaveClock
        {
            public DateTime UtcNow => DateTime.UtcNow;
        }

        private sealed class Candidate
        {
            public bool Exists;
            public bool Valid;
            public bool Future;
            public bool ReadFailed;
            public string Diagnostic;
            public FormalSaveDecodeResult Decoded;

            public static Candidate Missing() => new Candidate();
            public static Candidate Invalid() =>
                new Candidate { Exists = true };
            public static Candidate FutureSchema() =>
                new Candidate { Exists = true, Future = true };
            public static Candidate ReadError(string diagnostic) =>
                new Candidate
                {
                    Exists = true,
                    ReadFailed = true,
                    Diagnostic = diagnostic ?? string.Empty,
                };
            public static Candidate ValidDocument(
                FormalSaveDecodeResult decoded) => new Candidate
                {
                    Exists = true,
                    Valid = true,
                    Decoded = decoded,
                };
        }
    }
}
