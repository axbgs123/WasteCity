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
    public sealed class FormalRewindAnchorStoreTests
    {
        private const string StoreTypeName =
            "WasteCity.Persistence.FormalRewindAnchorStore";
        private const string ResultTypeName =
            "WasteCity.Persistence.FormalRewindAnchorStoreResult";
        private const string CodeTypeName =
            "WasteCity.Persistence.FormalRewindAnchorStoreCode";

        [Test]
        public void IDEA0020_ContractOwnsOneHiddenSlotOutsideOtherSaveSlots()
        {
            Type store = RequireType(StoreTypeName);
            Type result = RequireType(ResultTypeName);
            Type code = RequireType(CodeTypeName);
            Assert.That(code.IsEnum, Is.True);
            foreach (string value in new[]
            {
                "SaveSucceeded",
                "LoadSucceeded",
                "BackupRecovered",
                "ClearSucceeded",
                "NoAnchor",
                "InvalidEnvelope",
                "UnsupportedFutureSchema",
                "DiskWriteFailed",
                "DiskReadFailed",
            })
            {
                Assert.That(Enum.IsDefined(code, value), Is.True, value);
            }

            string directory = Constant(store, "InternalDirectoryName");
            string file = Constant(store, "FileName");
            Assert.That(directory, Does.StartWith(".internal-"));
            Assert.That(directory,
                Is.Not.EqualTo(FormalSaveWaveRetryStore.InternalDirectoryName));
            Assert.That(file, Is.Not.EqualTo(FormalSaveStore.FileName));
            Assert.That(file, Is.Not.EqualTo(FormalSaveWaveRetryStore.FileName));
            RequireMethod(store, "Save", typeof(FormalSaveEnvelope));
            RequireMethod(store, "Load");
            RequireMethod(store, "Clear");
            RequireProperty(result, "Success", typeof(bool));
            RequireProperty(result, "Code", code);
            RequireProperty(result, "Envelope", typeof(FormalSaveEnvelope));
            RequireProperty(result, "Message", typeof(string));
            RequireProperty(result, "Diagnostic", typeof(string));

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Persistence/FormalRewindAnchorStore.cs"));
            StringAssert.Contains("FormalSaveFileTransaction", source);
            StringAssert.Contains("FormalSaveCodec.DecodeAny", source);
            StringAssert.Contains("FormalSaveValidator.ValidateDecoded", source);
            StringAssert.DoesNotContain("new FormalSaveStore(", source);
            StringAssert.DoesNotContain("new FormalSaveWaveRetryStore(", source);
        }

        [Test]
        public void IDEA0020_SaveLoadIsValidatedNonRecursiveAndTouchesNoOtherSlot()
        {
            using (var root = new TemporaryDirectory())
            {
                object store = CreateStore(root.Path);
                FormalSaveEnvelope expected = ValidEnvelope(3L);

                AssertResult(Invoke(store, "Save", expected),
                    true, "SaveSucceeded");
                object loaded = Invoke(store, "Load");

                AssertResult(loaded, true, "LoadSucceeded");
                Assert.That(ReadEnvelope(loaded).checkpoint.sequence,
                    Is.EqualTo(3L));
                string path = AnchorPath(store.GetType(), root.Path);
                string json = File.ReadAllText(path);
                FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(json);
                Assert.That(decoded.Success, Is.True, decoded.Message);
                Assert.That(
                    FormalSaveValidator.ValidateDecoded(decoded).IsValid,
                    Is.True);
                StringAssert.DoesNotContain("anchorPayload", json);
                StringAssert.DoesNotContain("encodedEnvelope", json);
                Assert.That(File.Exists(Path.Combine(
                    root.Path,
                    FormalSaveStore.FileName)), Is.False);
                Assert.That(File.Exists(Path.Combine(
                    root.Path,
                    FormalSaveWaveRetryStore.InternalDirectoryName,
                    FormalSaveWaveRetryStore.FileName)), Is.False);
            }
        }

        [Test]
        public void IDEA0020_CorruptPrimaryLoadsLastValidatedAnchorBackup()
        {
            using (var root = new TemporaryDirectory())
            {
                object store = CreateStore(root.Path);
                AssertResult(Invoke(store, "Save", ValidEnvelope(4L)),
                    true, "SaveSucceeded");
                AssertResult(Invoke(store, "Save", ValidEnvelope(5L)),
                    true, "SaveSucceeded");
                string path = AnchorPath(store.GetType(), root.Path);
                File.WriteAllText(path, "{broken", Encoding.UTF8);

                object recovered = Invoke(store, "Load");

                AssertResult(recovered, true, "BackupRecovered");
                Assert.That(ReadEnvelope(recovered).checkpoint.sequence,
                    Is.EqualTo(4L));
                Assert.That(File.ReadAllText(path), Is.EqualTo("{broken"),
                    "Read-only recovery must not silently rewrite the slot.");
            }
        }

        [Test]
        public void IDEA0020_InvalidOrFailedReplacementPreservesOldAnchor()
        {
            var files = new MemoryFileSystem();
            object store = CreateStore("anchor-memory-root", files);
            AssertResult(Invoke(store, "Save", ValidEnvelope(6L)),
                true, "SaveSucceeded");
            string path = AnchorPath(store.GetType(), "anchor-memory-root");
            byte[] before = files.ReadWithoutFailure(path);

            FormalSaveEnvelope invalid = ValidEnvelope(7L);
            invalid.formal3D.storage = null;
            AssertResult(Invoke(store, "Save", invalid),
                false, "InvalidEnvelope");
            Assert.That(files.ReadWithoutFailure(path), Is.EqualTo(before));

            files.FailNextWrite = true;
            AssertResult(Invoke(store, "Save", ValidEnvelope(8L)),
                false, "DiskWriteFailed");
            Assert.That(files.ReadWithoutFailure(path), Is.EqualTo(before));
            object loaded = Invoke(store, "Load");
            AssertResult(loaded, true, "LoadSucceeded");
            Assert.That(ReadEnvelope(loaded).checkpoint.sequence,
                Is.EqualTo(6L));
        }

        [Test]
        public void IDEA0020_ClearDeletesOnlyExactAnchorAndBackup()
        {
            var files = new MemoryFileSystem();
            const string root = "anchor-clear-root";
            object store = CreateStore(root, files);
            AssertResult(Invoke(store, "Save", ValidEnvelope(9L)),
                true, "SaveSucceeded");
            AssertResult(Invoke(store, "Save", ValidEnvelope(10L)),
                true, "SaveSucceeded");
            string path = AnchorPath(store.GetType(), root);
            string player = Path.Combine(
                Path.GetFullPath(root),
                FormalSaveStore.FileName);
            string wave = Path.Combine(
                Path.GetFullPath(root),
                FormalSaveWaveRetryStore.InternalDirectoryName,
                FormalSaveWaveRetryStore.FileName);
            files.Seed(player, Encoding.UTF8.GetBytes("player"));
            files.Seed(wave, Encoding.UTF8.GetBytes("wave"));

            AssertResult(Invoke(store, "Clear"), true, "ClearSucceeded");

            Assert.That(files.FileExists(path), Is.False);
            Assert.That(files.FileExists(path + ".bak"), Is.False);
            Assert.That(Encoding.UTF8.GetString(
                    files.ReadWithoutFailure(player)),
                Is.EqualTo("player"));
            Assert.That(Encoding.UTF8.GetString(
                    files.ReadWithoutFailure(wave)),
                Is.EqualTo("wave"));
            AssertResult(Invoke(store, "Load"), false, "NoAnchor");
        }

        [Test]
        public void IDEA0020_MissingCorruptFutureAndReadFailureAreStructured()
        {
            var files = new MemoryFileSystem();
            const string root = "anchor-failure-root";
            object store = CreateStore(root, files);
            AssertResult(Invoke(store, "Load"), false, "NoAnchor");
            string path = AnchorPath(store.GetType(), root);

            files.Seed(path, Encoding.UTF8.GetBytes("{broken"));
            AssertResult(Invoke(store, "Load"), false, "InvalidEnvelope");
            files.Seed(path, Encoding.UTF8.GetBytes(FutureSchemaJson()));
            AssertResult(Invoke(store, "Load"),
                false, "UnsupportedFutureSchema");
            files.Seed(path, Encoding.UTF8.GetBytes(
                FormalSaveCodec.EncodeEnvelope(ValidEnvelope(11L))));
            files.FailNextRead = true;
            AssertResult(Invoke(store, "Load"), false, "DiskReadFailed");
        }

        [Test]
        public void IDEA0020_LevelTwoSlotsUseIndependentNonRecursiveFiles()
        {
            using (var root = new TemporaryDirectory())
            {
                var store = new FormalRewindAnchorStore(root.Path);
                Assert.That(store.Save(ValidEnvelope(20L), 1).Success, Is.True);
                Assert.That(store.Save(ValidEnvelope(21L), 2).Success, Is.True);
                Assert.That(store.Load(1).Envelope.checkpoint.sequence,
                    Is.EqualTo(20L));
                Assert.That(store.Load(2).Envelope.checkpoint.sequence,
                    Is.EqualTo(21L));
                string directory = Path.Combine(root.Path,
                    FormalRewindAnchorStore.InternalDirectoryName);
                string first = File.ReadAllText(Path.Combine(directory,
                    FormalRewindAnchorStore.FileName));
                string second = File.ReadAllText(Path.Combine(directory,
                    FormalRewindAnchorStore.SecondFileName));
                StringAssert.DoesNotContain("anchorPayload", first + second);
                StringAssert.DoesNotContain("encodedEnvelope", first + second);
                Assert.That(store.Clear(1).Success, Is.True);
                Assert.That(store.Load(1).Code,
                    Is.EqualTo(FormalRewindAnchorStoreCode.NoAnchor));
                Assert.That(store.Load(2).Success, Is.True);
            }
        }

        private static object CreateStore(
            string root,
            IFormalSaveFileSystem fileSystem = null)
        {
            ConstructorInfo constructor = RequireType(StoreTypeName)
                .GetConstructor(new[]
                {
                    typeof(string),
                    typeof(IFormalSaveFileSystem),
                });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[] { root, fileSystem });
        }

        private static object Invoke(
            object owner,
            string method,
            params object[] arguments)
        {
            Type[] types = new Type[arguments.Length];
            for (var index = 0; index < arguments.Length; index++)
                types[index] = arguments[index].GetType();
            MethodInfo target = owner.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                types,
                null);
            Assert.That(target, Is.Not.Null, method);
            return target.Invoke(owner, arguments);
        }

        private static void AssertResult(
            object result,
            bool success,
            string code)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(Read<bool>(result, "Success"), Is.EqualTo(success));
            Assert.That(Read<object>(result, "Code").ToString(),
                Is.EqualTo(code));
            Assert.That(Read<string>(result, "Message"), Is.Not.Null);
            Assert.That(Read<string>(result, "Diagnostic"), Is.Not.Null);
        }

        private static FormalSaveEnvelope ReadEnvelope(object result) =>
            Read<FormalSaveEnvelope>(result, "Envelope");

        private static T Read<T>(object owner, string property)
        {
            PropertyInfo info = owner.GetType().GetProperty(
                property,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, property);
            return (T)info.GetValue(owner);
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(FormalSaveStore).Assembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static MethodInfo RequireMethod(
            Type owner,
            string name,
            params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameters,
                null);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

        private static void RequireProperty(
            Type owner,
            string name,
            Type type)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.PropertyType, Is.EqualTo(type));
            Assert.That(property.CanWrite, Is.False);
        }

        private static string Constant(Type owner, string name)
        {
            FieldInfo field = owner.GetField(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            Assert.That(field.IsLiteral, Is.True, name);
            return (string)field.GetRawConstantValue();
        }

        private static string AnchorPath(Type store, string root)
        {
            return Path.Combine(
                Path.GetFullPath(root),
                Constant(store, "InternalDirectoryName"),
                Constant(store, "FileName"));
        }

        private static FormalSaveEnvelope ValidEnvelope(long sequence)
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            decoded.Envelope.checkpoint.sequence = sequence;
            return decoded.Envelope;
        }

        private static string FutureSchemaJson()
        {
            return ReadFixture("schema-32-future.json").Replace(
                "\"saveSchemaVersion\": 32",
                "\"saveSchemaVersion\": " +
                (FormalSaveEnvelope.CurrentSchemaVersion + 1));
        }

        private static string ReadFixture(string file)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Tests/Fixtures/Persistence",
                file));
        }

        private static string ProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relative));
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "wastecity-rewind-anchor-test-" +
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
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

            public void Seed(string path, byte[] bytes)
            {
                files[path] = (byte[])bytes.Clone();
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
