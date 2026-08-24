using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence;

namespace WasteCity.Tests
{
    public sealed class FormalSaveWaveRetryStoreTests
    {
        private const string StoreTypeName =
            "WasteCity.Persistence.FormalSaveWaveRetryStore";
        private const string ResultTypeName =
            "WasteCity.Persistence.FormalSaveWaveRetryStoreResult";
        private const string CodeTypeName =
            "WasteCity.Persistence.FormalSaveWaveRetryStoreCode";

        [Test]
        public void ContractIsAnInternalCheckpointStoreNotAPlayerSlot()
        {
            Assembly game = typeof(FormalSaveStore).Assembly;
            Type storeType = game.GetType(StoreTypeName);
            Type resultType = game.GetType(ResultTypeName);
            Type codeType = game.GetType(CodeTypeName);

            Assert.That(storeType, Is.Not.Null, StoreTypeName);
            Assert.That(resultType, Is.Not.Null, ResultTypeName);
            Assert.That(codeType, Is.Not.Null, CodeTypeName);
            Assert.That(codeType.IsEnum, Is.True);
            foreach (string value in new[]
            {
                "SaveSucceeded",
                "LoadSucceeded",
                "NoCheckpoint",
                "InvalidEnvelope",
                "UnsupportedFutureSchema",
                "DiskWriteFailed",
                "DiskReadFailed",
            })
            {
                Assert.That(Enum.IsDefined(codeType, value), Is.True, value);
            }

            FieldInfo directory = storeType.GetField(
                "InternalDirectoryName",
                BindingFlags.Public | BindingFlags.Static);
            FieldInfo file = storeType.GetField(
                "FileName",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(directory, Is.Not.Null);
            Assert.That(file, Is.Not.Null);
            Assert.That(directory.IsLiteral, Is.True);
            Assert.That(file.IsLiteral, Is.True);
            Assert.That((string)directory.GetRawConstantValue(),
                Is.Not.EqualTo(string.Empty));
            Assert.That((string)file.GetRawConstantValue(),
                Is.Not.EqualTo(FormalSaveStore.FileName));

            RequireMethod(storeType, "Save", typeof(FormalSaveEnvelope));
            RequireMethod(storeType, "Load");
            RequireReadOnlyProperty(
                resultType,
                "Success",
                typeof(bool));
            RequireReadOnlyProperty(
                resultType,
                "Code",
                codeType);
            RequireReadOnlyProperty(
                resultType,
                "Envelope",
                typeof(FormalSaveEnvelope));
            RequireReadOnlyProperty(
                resultType,
                "Message",
                typeof(string));
            RequireReadOnlyProperty(
                resultType,
                "Diagnostic",
                typeof(string));

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Persistence/" +
                "FormalSaveWaveRetryStore.cs"));
            StringAssert.Contains("FormalSaveCodec.DecodeAny", source);
            StringAssert.Contains("FormalSaveValidator.ValidateDecoded", source);
            StringAssert.Contains("FormalSaveFileTransaction", source);
            StringAssert.DoesNotContain("new FormalSaveStore(", source,
                "The retry artifact is not a second player-visible slot.");
        }

        [Test]
        public void SaveAndLoadRoundTripsOneCompleteSchema32Envelope()
        {
            using (var directory = new TemporaryDirectory())
            {
                object store = CreateStore(directory.Path);
                FormalSaveEnvelope expected = ValidEnvelope(7L);

                object saved = Invoke(store, "Save", expected);
                object loaded = Invoke(store, "Load");

                AssertResult(saved, true, "SaveSucceeded");
                AssertResult(loaded, true, "LoadSucceeded");
                FormalSaveEnvelope actual = ReadEnvelope(loaded);
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual.saveSchemaVersion,
                    Is.EqualTo(FormalSaveEnvelope.CurrentSchemaVersion));
                Assert.That(actual.runtimeKind,
                    Is.EqualTo(FormalSaveEnvelope.FormalThreeDRuntimeKind));
                Assert.That(actual.formal3D.sessionId,
                    Is.EqualTo(expected.formal3D.sessionId));
                Assert.That(actual.checkpoint.sequence, Is.EqualTo(7L));
                Assert.That(
                    FormalSaveValidator.ValidateEnvelope(actual).IsValid,
                    Is.True);

                string retryPath = RetryPath(store.GetType(), directory.Path);
                Assert.That(File.Exists(retryPath), Is.True);
                Assert.That(File.Exists(Path.Combine(
                    directory.Path,
                    FormalSaveStore.FileName)), Is.False,
                    "Retry storage must not create the player progress slot.");
            }
        }

        [Test]
        public void NewWaveAtomicallyReplacesThePreviousRetryCheckpoint()
        {
            using (var directory = new TemporaryDirectory())
            {
                object store = CreateStore(directory.Path);
                AssertResult(
                    Invoke(store, "Save", ValidEnvelope(3L)),
                    true,
                    "SaveSucceeded");
                AssertResult(
                    Invoke(store, "Save", ValidEnvelope(4L)),
                    true,
                    "SaveSucceeded");

                object loaded = Invoke(store, "Load");
                AssertResult(loaded, true, "LoadSucceeded");
                Assert.That(ReadEnvelope(loaded).checkpoint.sequence,
                    Is.EqualTo(4L));
                string path = RetryPath(store.GetType(), directory.Path);
                FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                    File.ReadAllText(path));
                Assert.That(decoded.Success, Is.True, decoded.Message);
                Assert.That(
                    FormalSaveValidator.ValidateDecoded(decoded).IsValid,
                    Is.True);
            }
        }

        [Test]
        public void InvalidIncomingEnvelopeCannotReplaceAnExistingWave()
        {
            using (var directory = new TemporaryDirectory())
            {
                object store = CreateStore(directory.Path);
                AssertResult(
                    Invoke(store, "Save", ValidEnvelope(5L)),
                    true,
                    "SaveSucceeded");
                string path = RetryPath(store.GetType(), directory.Path);
                byte[] before = File.ReadAllBytes(path);

                FormalSaveEnvelope invalid = ValidEnvelope(6L);
                invalid.formal3D.storage = null;
                object rejected = Invoke(store, "Save", invalid);

                AssertResult(rejected, false, "InvalidEnvelope");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
                object loaded = Invoke(store, "Load");
                AssertResult(loaded, true, "LoadSucceeded");
                Assert.That(ReadEnvelope(loaded).checkpoint.sequence,
                    Is.EqualTo(5L));
            }
        }

        [Test]
        public void EmptyCorruptAndFutureDocumentsReturnStructuredFailures()
        {
            using (var directory = new TemporaryDirectory())
            {
                object store = CreateStore(directory.Path);
                AssertResult(
                    Invoke(store, "Load"),
                    false,
                    "NoCheckpoint");

                string path = RetryPath(store.GetType(), directory.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{broken", Encoding.UTF8);
                AssertResult(
                    Invoke(store, "Load"),
                    false,
                    "InvalidEnvelope");

                File.WriteAllText(path, FutureSchemaJson(), Encoding.UTF8);
                AssertResult(
                    Invoke(store, "Load"),
                    false,
                    "UnsupportedFutureSchema");
            }
        }

        [Test]
        public void FailedAtomicWriteRetainsThePreviousValidatedWave()
        {
            var fileSystem = new MemoryFileSystem();
            object store = CreateStore("retry-memory-root", fileSystem);
            AssertResult(
                Invoke(store, "Save", ValidEnvelope(8L)),
                true,
                "SaveSucceeded");
            string path = RetryPath(store.GetType(), "retry-memory-root");
            byte[] previous = fileSystem.ReadWithoutFailure(path);

            fileSystem.FailNextWrite = true;
            object failed = Invoke(store, "Save", ValidEnvelope(9L));

            AssertResult(failed, false, "DiskWriteFailed");
            Assert.That(fileSystem.ReadWithoutFailure(path),
                Is.EqualTo(previous));
            object loaded = Invoke(store, "Load");
            AssertResult(loaded, true, "LoadSucceeded");
            Assert.That(ReadEnvelope(loaded).checkpoint.sequence,
                Is.EqualTo(8L));
        }

        [Test]
        public void ReadFailureDoesNotMasqueradeAsMissingOrCorrupt()
        {
            var fileSystem = new MemoryFileSystem();
            object store = CreateStore("retry-read-root", fileSystem);
            AssertResult(
                Invoke(store, "Save", ValidEnvelope(10L)),
                true,
                "SaveSucceeded");
            fileSystem.FailNextRead = true;

            AssertResult(
                Invoke(store, "Load"),
                false,
                "DiskReadFailed");
        }

        private static object CreateStore(
            string directory,
            IFormalSaveFileSystem fileSystem = null)
        {
            Type type = RequireType(StoreTypeName);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(string),
                typeof(IFormalSaveFileSystem),
            });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[] { directory, fileSystem });
        }

        private static object Invoke(
            object owner,
            string methodName,
            params object[] arguments)
        {
            Type[] parameterTypes = new Type[arguments.Length];
            for (var index = 0; index < arguments.Length; index++)
                parameterTypes[index] = arguments[index].GetType();
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(owner, arguments);
        }

        private static void AssertResult(
            object result,
            bool success,
            string code)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(ReadProperty<bool>(result, "Success"),
                Is.EqualTo(success));
            Assert.That(ReadProperty<object>(result, "Code").ToString(),
                Is.EqualTo(code));
            Assert.That(ReadProperty<string>(result, "Message"),
                Is.Not.Null);
            Assert.That(ReadProperty<string>(result, "Diagnostic"),
                Is.Not.Null);
        }

        private static FormalSaveEnvelope ReadEnvelope(object result)
        {
            return ReadProperty<FormalSaveEnvelope>(result, "Envelope");
        }

        private static T ReadProperty<T>(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(owner, null);
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(FormalSaveStore).Assembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static MethodInfo RequireMethod(
            Type owner,
            string methodName,
            params Type[] parameterTypes)
        {
            MethodInfo method = owner.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
        }

        private static void RequireReadOnlyProperty(
            Type owner,
            string propertyName,
            Type propertyType)
        {
            PropertyInfo property = owner.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.PropertyType, Is.EqualTo(propertyType));
            Assert.That(property.CanWrite, Is.False);
        }

        private static string RetryPath(Type storeType, string root)
        {
            string directory = (string)storeType.GetField(
                "InternalDirectoryName",
                BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
            string file = (string)storeType.GetField(
                "FileName",
                BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
            return Path.Combine(Path.GetFullPath(root), directory, file);
        }

        private static FormalSaveEnvelope ValidEnvelope(long sequence)
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Envelope.saveSchemaVersion,
                Is.EqualTo(FormalSaveEnvelope.CurrentSchemaVersion));
            decoded.Envelope.checkpoint.sequence = sequence;
            decoded.Envelope.checkpoint.reasonId =
                FormalSaveCheckpointReasonIds.CampaignWaveWarningStarted;
            return decoded.Envelope;
        }

        private static string FutureSchemaJson()
        {
            return ReadFixture("schema-32-future.json").Replace(
                "\"saveSchemaVersion\": 32",
                "\"saveSchemaVersion\": " +
                (FormalSaveEnvelope.CurrentSchemaVersion + 1));
        }

        private static string ReadFixture(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Tests/Fixtures/Persistence",
                fileName));
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "wastecity-wave-retry-test-" +
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
        }

        private sealed class MemoryFileSystem : IFormalSaveFileSystem
        {
            private readonly Dictionary<string, byte[]> files =
                new Dictionary<string, byte[]>(StringComparer.Ordinal);
            private int temporaryOrdinal;

            public bool FailNextRead { get; set; }
            public bool FailNextWrite { get; set; }

            public bool FileExists(string path) => files.ContainsKey(path);

            public byte[] ReadAllBytes(string path)
            {
                if (FailNextRead)
                {
                    FailNextRead = false;
                    throw new IOException("injected read failure");
                }
                return ReadWithoutFailure(path);
            }

            public byte[] ReadWithoutFailure(string path)
            {
                if (!files.TryGetValue(path, out byte[] bytes))
                    throw new FileNotFoundException(path);
                return (byte[])bytes.Clone();
            }

            public void CreateDirectory(string path)
            {
            }

            public string CreateTemporarySiblingPath(
                string targetPath,
                string purpose)
            {
                return targetPath + "." + purpose + "." +
                    temporaryOrdinal++ + ".tmp";
            }

            public void WriteAllBytesAndFlush(string path, byte[] bytes)
            {
                if (FailNextWrite)
                {
                    FailNextWrite = false;
                    throw new IOException("injected write failure");
                }
                files[path] = (byte[])bytes.Clone();
            }

            public void ReplaceAtomically(
                string sourcePath,
                string destinationPath)
            {
                if (!files.TryGetValue(sourcePath, out byte[] bytes))
                    throw new FileNotFoundException(sourcePath);
                files[destinationPath] = bytes;
                files.Remove(sourcePath);
            }

            public void DeleteIfExists(string path)
            {
                files.Remove(path);
            }
        }
    }
}
