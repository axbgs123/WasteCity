using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Persistence
{
    public static class FormalSaveCodec
    {
        [Serializable]
        private sealed class IdentityProbe
        {
            public int schema;
            public int saveSchemaVersion;
            public string runtimeKind;
        }

        public static string Encode(FormalSaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static FormalSaveData Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                FormalSaveData data =
                    JsonUtility.FromJson<FormalSaveData>(json);
                return data != null && data.schema >= 1 && data.schema <= 30
                    ? data
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public static string EncodeEnvelope(FormalSaveEnvelope envelope)
        {
            if (envelope == null) return null;
            var normalized = new FormalSaveEnvelope
            {
                gameVersion = envelope.gameVersion,
                saveSchemaVersion = envelope.saveSchemaVersion,
                contentSources = SortedCopy(envelope.contentSources),
                createdAt = envelope.createdAt,
                updatedAt = envelope.updatedAt,
                runtimeKind = envelope.runtimeKind,
                payloadHashSha256 = envelope.payloadHashSha256,
                checkpoint = CopyCheckpoint(envelope.checkpoint),
                formal3D = envelope.formal3D,
            };
            return JsonUtility.ToJson(normalized, false);
        }

        public static string ComputePayloadHashSha256(
            FormalThreeDSaveData payload)
        {
            if (payload == null) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(payload, false));
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                    builder.Append(digest[index].ToString("x2"));
                return builder.ToString();
            }
        }

        public static FormalSaveDecodeResult DecodeEnvelope(string json)
        {
            FormalSaveDecodeResult result = DecodeAny(json);
            if (!result.Success ||
                result.PayloadKind == FormalSavePayloadKind.Formal3D)
            {
                return result;
            }
            return FormalSaveDecodeResult.Failed(
                FormalSaveDecodeError.PayloadKindMismatch,
                "存档不是正式 3D 类型");
        }

        public static FormalSaveDecodeResult DecodeAny(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.BlankDocument,
                    "存档内容为空");
            }

            IdentityProbe probe;
            try
            {
                probe = JsonUtility.FromJson<IdentityProbe>(json);
            }
            catch
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.MalformedJson,
                    "存档内容已损坏");
            }
            if (probe == null)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.MalformedJson,
                    "存档内容已损坏");
            }

            bool hasLegacySchema = ContainsRootMember(json, "schema");
            bool hasEnvelopeSchema =
                ContainsRootMember(json, "saveSchemaVersion");
            if (hasLegacySchema && hasEnvelopeSchema)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.PayloadKindMismatch,
                    "存档类型与数据不一致");
            }
            if (probe.schema > FormalSaveEnvelope.CurrentSchemaVersion ||
                probe.saveSchemaVersion >
                FormalSaveEnvelope.CurrentSchemaVersion)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.UnsupportedFutureSchema,
                    "存档版本过新");
            }
            if (hasLegacySchema && probe.schema >= 1 && probe.schema <= 30)
            {
                FormalSaveData legacy = Decode(json);
                return legacy == null
                    ? FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.MalformedJson,
                        "旧版存档内容已损坏")
                    : FormalSaveDecodeResult.Legacy(legacy, json);
            }
            if (hasEnvelopeSchema && probe.saveSchemaVersion ==
                FormalSaveEnvelope.CurrentSchemaVersion)
            {
                if (!string.Equals(
                        probe.runtimeKind,
                        FormalSaveEnvelope.FormalThreeDRuntimeKind,
                        StringComparison.Ordinal))
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.UnknownRuntimeKind,
                        "无法识别存档运行时类型");
                }
                if (!ContainsRootMember(json, "formal3D") ||
                    ContainsRootMember(json, "legacy2D"))
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.PayloadKindMismatch,
                        "存档类型与数据不一致");
                }

                FormalSaveEnvelope envelope;
                try
                {
                    envelope = JsonUtility.FromJson<FormalSaveEnvelope>(json);
                }
                catch
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.MalformedJson,
                        "存档内容已损坏");
                }
                if (envelope == null || envelope.formal3D == null)
                {
                    return FormalSaveDecodeResult.Failed(
                        FormalSaveDecodeError.PayloadKindMismatch,
                        "存档类型与数据不一致");
                }
                return FormalSaveDecodeResult.ThreeD(envelope, json);
            }

            if (probe.schema == FormalSaveEnvelope.CurrentSchemaVersion ||
                probe.saveSchemaVersion != 0)
            {
                return FormalSaveDecodeResult.Failed(
                    FormalSaveDecodeError.PayloadKindMismatch,
                    "存档类型与数据不一致");
            }
            return FormalSaveDecodeResult.Failed(
                FormalSaveDecodeError.UnsupportedSchema,
                "无法识别存档版本");
        }

        public static string FormatUtcTimestamp(DateTime value)
        {
            return value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture);
        }

        private static string[] SortedCopy(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();
            var result = (string[])values.Clone();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static FormalSaveCheckpointMetadata CopyCheckpoint(
            FormalSaveCheckpointMetadata source)
        {
            return source == null
                ? null
                : new FormalSaveCheckpointMetadata
                {
                    sequence = source.sequence,
                    reasonId = source.reasonId,
                    ruleTimeSeconds = source.ruleTimeSeconds,
                    completedMilestoneIds = SortedCopy(
                        source.completedMilestoneIds),
                };
        }

        private static bool ContainsRootMember(
            string json,
            string memberName)
        {
            int depth = 0;
            for (int index = 0; index < json.Length; index++)
            {
                char value = json[index];
                if (value == '{' || value == '[')
                {
                    depth++;
                    continue;
                }
                if (value == '}' || value == ']')
                {
                    depth--;
                    continue;
                }
                if (value != '"') continue;

                int start = ++index;
                bool escaped = false;
                while (index < json.Length)
                {
                    char current = json[index];
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        break;
                    }
                    index++;
                }
                if (depth != 1 || index >= json.Length) continue;
                int after = index + 1;
                while (after < json.Length &&
                       char.IsWhiteSpace(json[after]))
                {
                    after++;
                }
                if (after >= json.Length || json[after] != ':') continue;
                int length = index - start;
                if (length == memberName.Length &&
                    string.CompareOrdinal(
                        json,
                        start,
                        memberName,
                        0,
                        length) == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
