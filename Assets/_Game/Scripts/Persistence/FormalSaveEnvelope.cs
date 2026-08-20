using System;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Persistence
{
    public enum FormalSavePayloadKind
    {
        None,
        Legacy2D,
        Formal3D,
    }

    public enum FormalSaveDecodeError
    {
        None,
        BlankDocument,
        MalformedJson,
        UnsupportedSchema,
        UnsupportedFutureSchema,
        UnknownRuntimeKind,
        PayloadKindMismatch,
    }

    [Serializable]
    public sealed class FormalSaveCheckpointMetadata
    {
        public long sequence;
        public string reasonId;
        public float ruleTimeSeconds;
        public string[] completedMilestoneIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalSaveEnvelope
    {
        public const int CurrentSchemaVersion = 31;
        public const string FormalThreeDRuntimeKind = "formal-3d";

        public string gameVersion;
        public int saveSchemaVersion = CurrentSchemaVersion;
        public string[] contentSources = Array.Empty<string>();
        public string createdAt;
        public string updatedAt;
        public string runtimeKind = FormalThreeDRuntimeKind;
        public string payloadHashSha256;
        public FormalSaveCheckpointMetadata checkpoint;
        public FormalThreeDSaveData formal3D;
    }

    public sealed class FormalSaveDecodeResult
    {
        private FormalSaveDecodeResult(
            bool success,
            FormalSavePayloadKind payloadKind,
            FormalSaveDecodeError error,
            string message,
            FormalSaveData legacy2D,
            FormalSaveEnvelope envelope)
        {
            Success = success;
            PayloadKind = payloadKind;
            Error = error;
            Message = message ?? string.Empty;
            Legacy2D = legacy2D;
            Envelope = envelope;
        }

        public bool Success { get; }
        public FormalSavePayloadKind PayloadKind { get; }
        public FormalSaveDecodeError Error { get; }
        public string Message { get; }
        public FormalSaveData Legacy2D { get; }
        public FormalSaveEnvelope Envelope { get; }

        internal static FormalSaveDecodeResult Legacy(FormalSaveData data)
        {
            return new FormalSaveDecodeResult(
                true,
                FormalSavePayloadKind.Legacy2D,
                FormalSaveDecodeError.None,
                string.Empty,
                data,
                null);
        }

        internal static FormalSaveDecodeResult ThreeD(
            FormalSaveEnvelope envelope)
        {
            return new FormalSaveDecodeResult(
                true,
                FormalSavePayloadKind.Formal3D,
                FormalSaveDecodeError.None,
                string.Empty,
                null,
                envelope);
        }

        internal static FormalSaveDecodeResult Failed(
            FormalSaveDecodeError error,
            string message)
        {
            return new FormalSaveDecodeResult(
                false,
                FormalSavePayloadKind.None,
                error,
                message,
                null,
                null);
        }
    }
}
