using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WasteCity.Persistence
{
    public enum FormalRewindAnchorStoreCode
    {
        SaveSucceeded,
        LoadSucceeded,
        BackupRecovered,
        ClearSucceeded,
        NoAnchor,
        InvalidEnvelope,
        UnsupportedFutureSchema,
        DiskWriteFailed,
        DiskReadFailed,
    }

    public sealed class FormalRewindAnchorStoreResult
    {
        internal FormalRewindAnchorStoreResult(
            FormalRewindAnchorStoreCode code,
            bool success,
            string message,
            FormalSaveEnvelope envelope = null,
            string diagnostic = null)
        {
            Code = code;
            Success = success;
            Message = message ?? string.Empty;
            Envelope = envelope;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public FormalRewindAnchorStoreCode Code { get; }
        public bool Success { get; }
        public FormalSaveEnvelope Envelope { get; }
        public string Message { get; }
        public string Diagnostic { get; }
    }

    public sealed class FormalRewindAnchorStore
    {
        public const string InternalDirectoryName =
            ".internal-rewind-anchor";
        public const string FileName = "slot-01.json";
        public const string SecondFileName = "slot-02.json";

        private static readonly UTF8Encoding Utf8 =
            new UTF8Encoding(false, true);

        private readonly IFormalSaveFileSystem fileSystem;
        private readonly FormalSaveFileTransaction transaction;
        private readonly string internalRoot;

        [Serializable]
        private sealed class IdentityProbe
        {
            public int saveSchemaVersion;
            public string runtimeKind;
        }

        public FormalRewindAnchorStore(
            string directory,
            IFormalSaveFileSystem fileSystem = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException(
                    "回溯锚点目录不能为空",
                    nameof(directory));
            }

            this.fileSystem = fileSystem ??
                new SystemFormalSaveFileSystem();
            transaction = new FormalSaveFileTransaction(this.fileSystem);
            internalRoot = Path.Combine(
                Path.GetFullPath(directory),
                InternalDirectoryName);
        }

        public FormalRewindAnchorStoreResult Save(
            FormalSaveEnvelope envelope)
        {
            return Save(envelope, 1);
        }

        public FormalRewindAnchorStoreResult Save(
            FormalSaveEnvelope envelope,
            int slot)
        {
            string anchorPath = SlotPath(slot);
            if (!IsCurrentFormalEnvelope(envelope))
                return Invalid("只接受当前正式 3D 存档");

            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateEnvelope(envelope);
            if (!validation.IsValid)
                return Invalid(ValidationDiagnostic(validation));

            byte[] bytes;
            try
            {
                bytes = Utf8.GetBytes(
                    FormalSaveCodec.EncodeEnvelope(envelope));
            }
            catch (Exception exception)
            {
                return Invalid(ExceptionDiagnostic(exception));
            }
            if (!ValidateBytes(bytes))
                return Invalid("编码后的锚点未通过完整复读验证");

            FormalSaveFileTransactionResult committed = transaction.Commit(
                anchorPath,
                bytes,
                ValidateBytes);
            return committed.Success
                ? Result(
                    FormalRewindAnchorStoreCode.SaveSucceeded,
                    true,
                    "已创建回溯锚点",
                    envelope)
                : Result(
                    FormalRewindAnchorStoreCode.DiskWriteFailed,
                    false,
                    "无法创建回溯锚点，旧锚点未被覆盖",
                    diagnostic: committed.Diagnostic);
        }

        public FormalRewindAnchorStoreResult Load()
        {
            return Load(1);
        }

        public FormalRewindAnchorStoreResult Load(int slot)
        {
            string anchorPath = SlotPath(slot);
            string backupPath = anchorPath + ".bak";
            bool hasPrimary = fileSystem.FileExists(anchorPath);
            bool hasBackup = fileSystem.FileExists(backupPath);
            if (!hasPrimary && !hasBackup)
            {
                return Result(
                    FormalRewindAnchorStoreCode.NoAnchor,
                    false,
                    "尚未创建回溯锚点");
            }

            if (hasPrimary)
            {
                FormalRewindAnchorStoreResult primary = ReadCandidate(
                    anchorPath,
                    FormalRewindAnchorStoreCode.LoadSucceeded,
                    "已读取回溯锚点");
                if (primary.Success ||
                    primary.Code ==
                        FormalRewindAnchorStoreCode.UnsupportedFutureSchema ||
                    primary.Code ==
                        FormalRewindAnchorStoreCode.DiskReadFailed)
                {
                    return primary;
                }
            }

            if (hasBackup)
            {
                FormalRewindAnchorStoreResult backup = ReadCandidate(
                    backupPath,
                    FormalRewindAnchorStoreCode.BackupRecovered,
                    "主锚点无效，已读取最近有效锚点备份");
                if (backup.Success ||
                    backup.Code ==
                        FormalRewindAnchorStoreCode.UnsupportedFutureSchema ||
                    backup.Code ==
                        FormalRewindAnchorStoreCode.DiskReadFailed)
                {
                    return backup;
                }
            }

            return Result(
                FormalRewindAnchorStoreCode.InvalidEnvelope,
                false,
                "回溯锚点已损坏或格式不兼容");
        }

        public FormalRewindAnchorStoreResult Clear()
        {
            return Clear(1);
        }

        public FormalRewindAnchorStoreResult Clear(int slot)
        {
            string anchorPath = SlotPath(slot);
            string backupPath = anchorPath + ".bak";
            try
            {
                fileSystem.DeleteIfExists(anchorPath);
                fileSystem.DeleteIfExists(backupPath);
                return Result(
                    FormalRewindAnchorStoreCode.ClearSucceeded,
                    true,
                    "已清除回溯锚点");
            }
            catch (Exception exception)
            {
                return Result(
                    FormalRewindAnchorStoreCode.DiskWriteFailed,
                    false,
                    "无法清除回溯锚点",
                    diagnostic: ExceptionDiagnostic(exception));
            }
        }

        private string SlotPath(int slot)
        {
            if (slot != 1 && slot != 2)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(
                internalRoot,
                slot == 1 ? FileName : SecondFileName);
        }

        private FormalRewindAnchorStoreResult ReadCandidate(
            string path,
            FormalRewindAnchorStoreCode successCode,
            string successMessage)
        {
            byte[] bytes;
            string json;
            try
            {
                bytes = fileSystem.ReadAllBytes(path);
                json = DecodeJson(bytes);
            }
            catch (Exception exception)
            {
                return Result(
                    FormalRewindAnchorStoreCode.DiskReadFailed,
                    false,
                    "无法读取回溯锚点",
                    diagnostic: ExceptionDiagnostic(exception));
            }

            IdentityProbe identity = ReadIdentity(json);
            if (identity != null &&
                identity.saveSchemaVersion >
                    FormalSaveEnvelope.CurrentSchemaVersion)
            {
                return Result(
                    FormalRewindAnchorStoreCode.UnsupportedFutureSchema,
                    false,
                    "回溯锚点版本过新，请更新游戏后重试");
            }
            if (!IsCurrentFormalIdentity(identity))
                return Invalid("锚点身份无效");

            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(json);
            if (!decoded.Success)
            {
                return decoded.Error ==
                       FormalSaveDecodeError.UnsupportedFutureSchema
                    ? Result(
                        FormalRewindAnchorStoreCode.UnsupportedFutureSchema,
                        false,
                        "回溯锚点版本过新，请更新游戏后重试",
                        diagnostic: decoded.Message)
                    : Invalid(decoded.Message);
            }

            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            if (decoded.PayloadKind != FormalSavePayloadKind.Formal3D ||
                !validation.IsValid)
            {
                return Invalid(ValidationDiagnostic(validation));
            }
            return Result(
                successCode,
                true,
                successMessage,
                decoded.Envelope);
        }

        private static bool ValidateBytes(byte[] bytes)
        {
            if (bytes == null) return false;
            try
            {
                string json = DecodeJson(bytes);
                if (!IsCurrentFormalIdentity(ReadIdentity(json))) return false;
                FormalSaveDecodeResult decoded =
                    FormalSaveCodec.DecodeAny(json);
                return decoded.Success &&
                    decoded.PayloadKind == FormalSavePayloadKind.Formal3D &&
                    FormalSaveValidator.ValidateDecoded(decoded).IsValid;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCurrentFormalEnvelope(
            FormalSaveEnvelope envelope)
        {
            return envelope != null &&
                envelope.formal3D != null &&
                envelope.saveSchemaVersion ==
                    FormalSaveEnvelope.CurrentSchemaVersion &&
                string.Equals(
                    envelope.runtimeKind,
                    FormalSaveEnvelope.FormalThreeDRuntimeKind,
                    StringComparison.Ordinal);
        }

        private static bool IsCurrentFormalIdentity(IdentityProbe identity)
        {
            return identity != null &&
                identity.saveSchemaVersion ==
                    FormalSaveEnvelope.CurrentSchemaVersion &&
                string.Equals(
                    identity.runtimeKind,
                    FormalSaveEnvelope.FormalThreeDRuntimeKind,
                    StringComparison.Ordinal);
        }

        private static IdentityProbe ReadIdentity(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<IdentityProbe>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string DecodeJson(byte[] bytes)
        {
            string json = Utf8.GetString(bytes);
            return json.Length > 0 && json[0] == '\uFEFF'
                ? json.Substring(1)
                : json;
        }

        private static FormalRewindAnchorStoreResult Invalid(
            string diagnostic)
        {
            return Result(
                FormalRewindAnchorStoreCode.InvalidEnvelope,
                false,
                "回溯锚点数据无效",
                diagnostic: diagnostic);
        }

        private static string ValidationDiagnostic(
            FormalSaveValidationResult validation)
        {
            if (validation == null) return "验证结果为空";
            return validation.Error + ": " + validation.FieldPath + " " +
                validation.Message;
        }

        private static string ExceptionDiagnostic(Exception exception)
        {
            return exception.GetType().Name + ": " + exception.Message;
        }

        private static FormalRewindAnchorStoreResult Result(
            FormalRewindAnchorStoreCode code,
            bool success,
            string message,
            FormalSaveEnvelope envelope = null,
            string diagnostic = null)
        {
            return new FormalRewindAnchorStoreResult(
                code,
                success,
                message,
                envelope,
                diagnostic);
        }
    }
}
