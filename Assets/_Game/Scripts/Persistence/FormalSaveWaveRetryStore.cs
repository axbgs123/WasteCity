using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WasteCity.Persistence
{
    public enum FormalSaveWaveRetryStoreCode
    {
        SaveSucceeded,
        LoadSucceeded,
        NoCheckpoint,
        InvalidEnvelope,
        UnsupportedFutureSchema,
        DiskWriteFailed,
        DiskReadFailed,
    }

    public sealed class FormalSaveWaveRetryStoreResult
    {
        internal FormalSaveWaveRetryStoreResult(
            FormalSaveWaveRetryStoreCode code,
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

        public bool Success { get; }
        public FormalSaveWaveRetryStoreCode Code { get; }
        public FormalSaveEnvelope Envelope { get; }
        public string Message { get; }
        public string Diagnostic { get; }

        public static FormalSaveWaveRetryStoreResult InvalidCurrentCampaign(
            string message)
        {
            return new FormalSaveWaveRetryStoreResult(
                FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                false,
                message);
        }
    }

    public sealed class FormalSaveWaveRetryStore
    {
        public const string InternalDirectoryName =
            ".internal-wave-retry";
        public const string FileName = "latest-wave-front.json";

        private static readonly UTF8Encoding Utf8 =
            new UTF8Encoding(false, true);

        private readonly IFormalSaveFileSystem fileSystem;
        private readonly FormalSaveFileTransaction transaction;
        private readonly string checkpointPath;

        [Serializable]
        private sealed class IdentityProbe
        {
            public int saveSchemaVersion;
            public string runtimeKind;
        }

        public FormalSaveWaveRetryStore(
            string directory,
            IFormalSaveFileSystem fileSystem = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException(
                    "重试档目录不能为空",
                    nameof(directory));

            this.fileSystem = fileSystem ??
                new SystemFormalSaveFileSystem();
            transaction = new FormalSaveFileTransaction(this.fileSystem);
            checkpointPath = Path.Combine(
                Path.GetFullPath(directory),
                InternalDirectoryName,
                FileName);
        }

        public FormalSaveWaveRetryStoreResult Save(
            FormalSaveEnvelope envelope)
        {
            if (!IsCurrentFormalEnvelope(envelope))
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                    false,
                    "波前重试档数据无效",
                    diagnostic: "只接受完整的 schema 32 正式 3D 存档");
            }

            FormalSaveValidationResult envelopeValidation =
                FormalSaveValidator.ValidateEnvelope(envelope);
            if (!envelopeValidation.IsValid)
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                    false,
                    "波前重试档数据无效",
                    diagnostic: ValidationDiagnostic(envelopeValidation));
            }

            byte[] bytes;
            try
            {
                bytes = Utf8.GetBytes(
                    FormalSaveCodec.EncodeEnvelope(envelope));
            }
            catch (Exception exception)
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                    false,
                    "波前重试档数据无法编码",
                    diagnostic: ExceptionDiagnostic(exception));
            }

            if (!ValidateBytes(bytes))
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                    false,
                    "波前重试档数据无效",
                    diagnostic: "编码后的完整存档验证失败");
            }

            FormalSaveFileTransactionResult committed =
                transaction.Commit(
                    checkpointPath,
                    bytes,
                    ValidateBytes);
            return committed.Success
                ? Result(
                    FormalSaveWaveRetryStoreCode.SaveSucceeded,
                    true,
                    "已记录最近波前重试点",
                    envelope)
                : Result(
                    FormalSaveWaveRetryStoreCode.DiskWriteFailed,
                    false,
                    "无法记录波前重试点，旧重试档未被覆盖",
                    diagnostic: committed.Diagnostic);
        }

        public FormalSaveWaveRetryStoreResult Load()
        {
            if (!fileSystem.FileExists(checkpointPath))
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.NoCheckpoint,
                    false,
                    "尚未记录可重试的波前");
            }

            byte[] bytes;
            string json;
            try
            {
                bytes = fileSystem.ReadAllBytes(checkpointPath);
                json = DecodeJson(bytes);
            }
            catch (Exception exception)
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.DiskReadFailed,
                    false,
                    "无法读取波前重试档",
                    diagnostic: ExceptionDiagnostic(exception));
            }

            IdentityProbe probe = ReadIdentity(json);
            if (probe != null &&
                probe.saveSchemaVersion >
                FormalSaveEnvelope.CurrentSchemaVersion)
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.UnsupportedFutureSchema,
                    false,
                    "波前重试档版本过新，请更新游戏后重试");
            }
            if (!IsCurrentFormalIdentity(probe))
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                    false,
                    "波前重试档已损坏或格式不兼容");
            }

            FormalSaveDecodeResult decoded =
                FormalSaveCodec.DecodeAny(json);
            if (!decoded.Success)
            {
                return decoded.Error ==
                       FormalSaveDecodeError.UnsupportedFutureSchema
                    ? Result(
                        FormalSaveWaveRetryStoreCode.UnsupportedFutureSchema,
                        false,
                        "波前重试档版本过新，请更新游戏后重试",
                        diagnostic: decoded.Message)
                    : Result(
                        FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                        false,
                        "波前重试档已损坏或格式不兼容",
                        diagnostic: decoded.Message);
            }

            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            if (decoded.PayloadKind != FormalSavePayloadKind.Formal3D ||
                !validation.IsValid)
            {
                return Result(
                    FormalSaveWaveRetryStoreCode.InvalidEnvelope,
                    false,
                    "波前重试档未通过完整验证",
                    diagnostic: ValidationDiagnostic(validation));
            }

            return Result(
                FormalSaveWaveRetryStoreCode.LoadSucceeded,
                true,
                "已读取最近波前重试点",
                decoded.Envelope);
        }

        private static bool ValidateBytes(byte[] bytes)
        {
            if (bytes == null) return false;
            try
            {
                string json = DecodeJson(bytes);
                if (!IsCurrentFormalIdentity(ReadIdentity(json)))
                    return false;
                FormalSaveDecodeResult decoded =
                    FormalSaveCodec.DecodeAny(json);
                return decoded.Success &&
                       decoded.PayloadKind ==
                       FormalSavePayloadKind.Formal3D &&
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

        private static bool IsCurrentFormalIdentity(IdentityProbe probe)
        {
            return probe != null &&
                   probe.saveSchemaVersion ==
                   FormalSaveEnvelope.CurrentSchemaVersion &&
                   string.Equals(
                       probe.runtimeKind,
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

        private static string ValidationDiagnostic(
            FormalSaveValidationResult validation)
        {
            if (validation == null) return "验证结果为空";
            return validation.Error + ": " + validation.FieldPath +
                   " " + validation.Message;
        }

        private static string ExceptionDiagnostic(Exception exception)
        {
            return exception.GetType().Name + ": " + exception.Message;
        }

        private static FormalSaveWaveRetryStoreResult Result(
            FormalSaveWaveRetryStoreCode code,
            bool success,
            string message,
            FormalSaveEnvelope envelope = null,
            string diagnostic = null)
        {
            return new FormalSaveWaveRetryStoreResult(
                code,
                success,
                message,
                envelope,
                diagnostic);
        }
    }
}
