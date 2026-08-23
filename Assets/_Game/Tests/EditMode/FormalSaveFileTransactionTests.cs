using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence;

namespace WasteCity.Tests
{
    public sealed class FormalSaveFileTransactionTests
    {
        [Test]
        public void TaskThreeProductionTypesExistInGameAssembly()
        {
            Assembly game = typeof(FormalSaveCodec).Assembly;

            Assert.That(game.GetType(
                "WasteCity.Persistence.FormalSaveFileTransaction"),
                Is.Not.Null);
            Assert.That(game.GetType(
                "WasteCity.Persistence.FormalSaveStore"),
                Is.Not.Null);
            Assert.That(game.GetType(
                "WasteCity.Persistence.FormalSaveStoreResult"),
                Is.Not.Null);
            Assert.That(game.GetType(
                "WasteCity.Persistence.FormalSaveStoreCode"),
                Is.Not.Null);
        }

        [Test]
        public void StoreResultPublishesStablePlayerFacingOutcomes()
        {
            Assembly game = typeof(FormalSaveCodec).Assembly;
            Type code = game.GetType(
                "WasteCity.Persistence.FormalSaveStoreCode");

            Assert.That(code, Is.Not.Null);
            string[] required =
            {
                "SaveSucceeded",
                "LoadSucceeded",
                "BackupRecovered",
                "NoValidSave",
                "Legacy2DOnly",
                "UnsupportedFutureSchema",
                "DiskWriteFailed",
                "DiskReadFailed",
                "CorruptNoBackup",
            };
            foreach (string value in required)
                Assert.That(Enum.IsDefined(code, value), Is.True, value);
        }

        [Test]
        public void TransactionUsesValidatedTemporaryBackupThenPrimaryOrder()
        {
            var fileSystem = new MemoryFileSystem();
            fileSystem.Seed("slot.json", "old-valid");
            fileSystem.Seed("slot.json.bak", "backup-valid");
            var transaction = new FormalSaveFileTransaction(fileSystem);

            FormalSaveFileTransactionResult result = transaction.Commit(
                "slot.json",
                Bytes("new-valid"),
                IsValidBytes);

            Assert.That(result.Success, Is.True, result.Diagnostic);
            Assert.That(fileSystem.Text("slot.json"),
                Is.EqualTo("new-valid"));
            Assert.That(fileSystem.Text("slot.json.bak"),
                Is.EqualTo("old-valid"));
            Assert.That(fileSystem.Operations, Is.EqualTo(new[]
            {
                "write:slot.json.primary.tmp",
                "read:slot.json.primary.tmp",
                "read:slot.json",
                "write:slot.json.bak.backup.tmp",
                "read:slot.json.bak.backup.tmp",
                "replace:slot.json.bak.backup.tmp->slot.json.bak",
                "replace:slot.json.primary.tmp->slot.json",
                "read:slot.json",
            }));
            Assert.That(fileSystem.Paths.Any(
                value => value.EndsWith(".tmp", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void EveryTransactionFailureKeepsAValidPrimaryOrBackup()
        {
            var baseline = new MemoryFileSystem();
            baseline.Seed("slot.json", "old-valid");
            baseline.Seed("slot.json.bak", "backup-valid");
            FormalSaveFileTransactionResult baselineResult =
                new FormalSaveFileTransaction(baseline).Commit(
                "slot.json",
                Bytes("new-valid"),
                IsValidBytes);
            Assert.That(baselineResult.Success, Is.True,
                baselineResult.Diagnostic);
            int operationCount = baseline.Operations.Count;
            Assert.That(operationCount, Is.GreaterThan(0));

            for (int failure = 0; failure < operationCount; failure++)
            {
                var fileSystem = new MemoryFileSystem
                {
                    FailAtOperation = failure,
                };
                fileSystem.Seed("slot.json", "old-valid");
                fileSystem.Seed("slot.json.bak", "backup-valid");

                FormalSaveFileTransactionResult result =
                    new FormalSaveFileTransaction(fileSystem).Commit(
                        "slot.json",
                        Bytes("new-valid"),
                        IsValidBytes);

                Assert.That(result.Success, Is.False,
                    "failure operation " + failure);
                bool validPrimary = fileSystem.TryText(
                    "slot.json",
                    out string primary) &&
                    IsValidBytes(Bytes(primary));
                bool validBackup = fileSystem.TryText(
                    "slot.json.bak",
                    out string backup) &&
                    IsValidBytes(Bytes(backup));
                Assert.That(validPrimary || validBackup, Is.True,
                    "failure operation " + failure);
                Assert.That(fileSystem.Paths.Any(
                    value => value.EndsWith(".tmp",
                        StringComparison.Ordinal)), Is.False,
                    "failure operation " + failure);
            }
        }

        [Test]
        public void InvalidExistingPrimaryNeverOverwritesValidBackup()
        {
            var fileSystem = new MemoryFileSystem();
            fileSystem.Seed("slot.json", "broken");
            fileSystem.Seed("slot.json.bak", "backup-valid");

            FormalSaveFileTransactionResult result =
                new FormalSaveFileTransaction(fileSystem).Commit(
                    "slot.json",
                    Bytes("new-valid"),
                    IsValidBytes);

            Assert.That(result.Success, Is.True, result.Diagnostic);
            Assert.That(fileSystem.Text("slot.json"),
                Is.EqualTo("new-valid"));
            Assert.That(fileSystem.Text("slot.json.bak"),
                Is.EqualTo("backup-valid"));
        }

        [Test]
        public void InvalidTemporaryDocumentCannotChangeSlot()
        {
            var fileSystem = new MemoryFileSystem();
            fileSystem.Seed("slot.json", "old-valid");
            fileSystem.Seed("slot.json.bak", "backup-valid");

            FormalSaveFileTransactionResult result =
                new FormalSaveFileTransaction(fileSystem).Commit(
                    "slot.json",
                    Bytes("broken"),
                    IsValidBytes);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailedStage,
                Is.EqualTo(FormalSaveTransactionStage.ValidateTemporary));
            Assert.That(fileSystem.Text("slot.json"),
                Is.EqualTo("old-valid"));
            Assert.That(fileSystem.Text("slot.json.bak"),
                Is.EqualTo("backup-valid"));
        }

        [Test]
        public void StoreSavePreservesCreatedAtAndAdvancesUpdatedAt()
        {
            using (var directory = new TemporaryDirectory())
            {
                var clock = new FakeClock(
                    new DateTime(2031, 2, 3, 4, 5, 6,
                        DateTimeKind.Utc));
                var store = new FormalSaveStore(
                    directory.Path,
                    null,
                    clock);
                FormalSaveEnvelope envelope = LoadFixtureEnvelope();

                FormalSaveStoreResult first = store.SaveEnvelope(envelope);
                clock.UtcNow = clock.UtcNow.AddMinutes(7);
                envelope.checkpoint.sequence++;
                FormalSaveStoreResult second = store.SaveEnvelope(envelope);
                FormalSaveStoreResult loaded = store.Load(
                    FormalSavePayloadKind.Formal3D);

                Assert.That(first.Code,
                    Is.EqualTo(FormalSaveStoreCode.SaveSucceeded),
                    first.Message);
                Assert.That(second.Code,
                    Is.EqualTo(FormalSaveStoreCode.SaveSucceeded),
                    second.Message);
                Assert.That(loaded.Code,
                    Is.EqualTo(FormalSaveStoreCode.LoadSucceeded),
                    loaded.Message);
                Assert.That(loaded.Envelope.createdAt,
                    Is.EqualTo("2031-02-03T04:05:06.0000000Z"));
                Assert.That(loaded.Envelope.updatedAt,
                    Is.EqualTo("2031-02-03T04:12:06.0000000Z"));
                Assert.That(loaded.Envelope.checkpoint.sequence,
                    Is.EqualTo(8L));
                FormalSaveDecodeResult backup = FormalSaveCodec.DecodeAny(
                    File.ReadAllText(Path.Combine(
                        directory.Path,
                        FormalSaveStore.FileName + ".bak")));
                Assert.That(
                    FormalSaveValidator.ValidateDecoded(backup).IsValid,
                    Is.True);
                Assert.That(backup.Envelope.updatedAt,
                    Is.EqualTo("2031-02-03T04:05:06.0000000Z"));
            }
        }

        [Test]
        public void StartNewProgressResetsCreatedAtAndBacksUpPriorProgress()
        {
            using (var directory = new TemporaryDirectory())
            {
                var clock = new FakeClock(
                    new DateTime(2031, 2, 3, 4, 5, 6,
                        DateTimeKind.Utc));
                var store = new FormalSaveStore(
                    directory.Path,
                    null,
                    clock);
                FormalSaveEnvelope firstEnvelope = LoadFixtureEnvelope();
                Assert.That(store.SaveEnvelope(firstEnvelope).Success, Is.True);
                string primaryPath = Path.Combine(
                    directory.Path,
                    FormalSaveStore.FileName);
                string oldProgress = File.ReadAllText(primaryPath);

                clock.UtcNow = clock.UtcNow.AddHours(2);
                FormalSaveEnvelope nextEnvelope = LoadFixtureEnvelope();
                nextEnvelope.formal3D.sessionId = "session-new-progress";
                FormalSaveStoreResult result = store.SaveEnvelope(
                    nextEnvelope,
                    writeIntent: FormalSaveWriteIntent.StartNewProgress);

                Assert.That(result.Success, Is.True, result.Diagnostic);
                FormalSaveStoreResult loaded = store.Load(
                    FormalSavePayloadKind.Formal3D);
                Assert.That(
                    loaded.Envelope.createdAt,
                    Is.EqualTo("2031-02-03T06:05:06.0000000Z"));
                Assert.That(
                    loaded.Envelope.updatedAt,
                    Is.EqualTo("2031-02-03T06:05:06.0000000Z"));
                Assert.That(
                    loaded.Envelope.formal3D.sessionId,
                    Is.EqualTo("session-new-progress"));
                Assert.That(
                    File.ReadAllText(primaryPath + ".bak"),
                    Is.EqualTo(oldProgress));
                FormalSaveDecodeResult backup = FormalSaveCodec.DecodeAny(
                    File.ReadAllText(primaryPath + ".bak"));
                Assert.That(backup.Success, Is.True, backup.Message);
                Assert.That(
                    backup.Envelope.createdAt,
                    Is.EqualTo("2031-02-03T04:05:06.0000000Z"));
            }
        }

        [Test]
        public void FailedStartNewProgressKeepsOldSaveAndCallerMetadata()
        {
            string directory = Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "wastecity-new-progress-memory"));
            string primaryPath = Path.Combine(
                directory,
                FormalSaveStore.FileName);
            var fileSystem = new MemoryFileSystem();
            var clock = new FakeClock(
                new DateTime(2031, 2, 3, 4, 5, 6,
                    DateTimeKind.Utc));
            var store = new FormalSaveStore(
                directory,
                fileSystem,
                clock);
            FormalSaveEnvelope firstEnvelope = LoadFixtureEnvelope();
            Assert.That(store.SaveEnvelope(firstEnvelope).Success, Is.True);
            string oldProgress = fileSystem.Text(primaryPath);

            clock.UtcNow = clock.UtcNow.AddHours(2);
            FormalSaveEnvelope nextEnvelope = LoadFixtureEnvelope();
            string callerCreatedAt = nextEnvelope.createdAt;
            string callerUpdatedAt = nextEnvelope.updatedAt;
            fileSystem.FailAtOperation = fileSystem.Operations.Count + 1;

            FormalSaveStoreResult result = store.SaveEnvelope(
                nextEnvelope,
                writeIntent: FormalSaveWriteIntent.StartNewProgress);

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.Code,
                Is.EqualTo(FormalSaveStoreCode.DiskWriteFailed));
            Assert.That(fileSystem.Text(primaryPath), Is.EqualTo(oldProgress));
            Assert.That(nextEnvelope.createdAt, Is.EqualTo(callerCreatedAt));
            Assert.That(nextEnvelope.updatedAt, Is.EqualTo(callerUpdatedAt));
        }

        [Test]
        public void ProbeValidatesContentRatherThanOnlyFileExistence()
        {
            using (var directory = new TemporaryDirectory())
            {
                File.WriteAllText(Path.Combine(
                    directory.Path,
                    FormalSaveStore.FileName), "{broken");
                var store = new FormalSaveStore(directory.Path);

                FormalSaveStoreResult result = store.Probe(
                    FormalSavePayloadKind.Formal3D);

                Assert.That(result.CanContinue, Is.False);
                Assert.That(result.Code,
                    Is.EqualTo(FormalSaveStoreCode.CorruptNoBackup));
                Assert.That(result.Message, Is.Not.Empty);
            }
        }

        [Test]
        public void CorruptPrimaryFallsBackToValidatedBackupWithoutDeletingIt()
        {
            using (var directory = new TemporaryDirectory())
            {
                var store = new FormalSaveStore(directory.Path);
                FormalSaveEnvelope envelope = LoadFixtureEnvelope();
                Assert.That(store.SaveEnvelope(envelope).Success, Is.True);
                envelope.checkpoint.sequence++;
                Assert.That(store.SaveEnvelope(envelope).Success, Is.True);
                string primary = Path.Combine(
                    directory.Path,
                    FormalSaveStore.FileName);
                File.WriteAllText(primary, "{broken");

                FormalSaveStoreResult result = store.Load(
                    FormalSavePayloadKind.Formal3D);

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Code,
                    Is.EqualTo(FormalSaveStoreCode.BackupRecovered));
                Assert.That(result.UsedBackup, Is.True);
                Assert.That(result.Envelope, Is.Not.Null);
                Assert.That(File.ReadAllText(primary), Is.EqualTo("{broken"));
                Assert.That(File.Exists(primary + ".bak"), Is.True);
            }
        }

        [Test]
        public void ProbeDistinguishesNoSaveLegacyAndFutureSchema()
        {
            using (var directory = new TemporaryDirectory())
            {
                var store = new FormalSaveStore(directory.Path);
                string primary = Path.Combine(
                    directory.Path,
                    FormalSaveStore.FileName);
                Assert.That(store.Probe(FormalSavePayloadKind.Formal3D).Code,
                    Is.EqualTo(FormalSaveStoreCode.NoValidSave));

                File.WriteAllText(primary,
                    ReadFixture("schema-30-legacy-2d.json"));
                FormalSaveStoreResult legacy = store.Probe(
                    FormalSavePayloadKind.Formal3D);
                Assert.That(legacy.Code,
                    Is.EqualTo(FormalSaveStoreCode.Legacy2DOnly));
                Assert.That(legacy.Message, Does.Contain("旧版 2D"));

                File.WriteAllText(primary,
                    FutureSchemaJson());
                FormalSaveStoreResult future = store.Probe(
                    FormalSavePayloadKind.Formal3D);
                Assert.That(future.Code,
                    Is.EqualTo(FormalSaveStoreCode.UnsupportedFutureSchema));
                Assert.That(future.Message, Does.Contain("版本过新"));
            }
        }

        [Test]
        public void LegacySaveCannotOverwriteFutureSchema()
        {
            using (var directory = new TemporaryDirectory())
            {
                string primary = Path.Combine(
                    directory.Path,
                    FormalSaveStore.FileName);
                byte[] futureBytes = Encoding.UTF8.GetBytes(
                    FutureSchemaJson());
                File.WriteAllBytes(primary, futureBytes);
                var store = new FormalSaveStore(directory.Path);

                FormalSaveStoreResult result = store.SaveLegacy(
                    new FormalSaveData());

                Assert.That(result.Success, Is.False);
                Assert.That(result.Code,
                    Is.EqualTo(FormalSaveStoreCode.UnsupportedFutureSchema));
                Assert.That(File.ReadAllBytes(primary), Is.EqualTo(futureBytes));
                Assert.That(File.Exists(primary + ".bak"), Is.False);
            }
        }

        private static string FutureSchemaJson()
        {
            return ReadFixture("schema-32-future.json").Replace(
                "\"saveSchemaVersion\": 32",
                "\"saveSchemaVersion\": " +
                (FormalSaveEnvelope.CurrentSchemaVersion + 1));
        }

        [Test]
        public void FirstThreeDSaveRequiresExplicitValidatedLegacyArchive()
        {
            using (var directory = new TemporaryDirectory())
            {
                string primary = Path.Combine(
                    directory.Path,
                    FormalSaveStore.FileName);
                string legacyJson =
                    ReadFixture("schema-30-legacy-2d.json");
                File.WriteAllText(primary, legacyJson);
                var clock = new FakeClock(
                    new DateTime(2031, 2, 3, 4, 5, 6,
                        DateTimeKind.Utc));
                var store = new FormalSaveStore(
                    directory.Path,
                    null,
                    clock);
                FormalSaveEnvelope envelope = LoadFixtureEnvelope();

                FormalSaveStoreResult blocked =
                    store.SaveEnvelope(envelope);
                Assert.That(blocked.Code,
                    Is.EqualTo(FormalSaveStoreCode.Legacy2DOnly));
                Assert.That(File.ReadAllText(primary),
                    Is.EqualTo(legacyJson));

                FormalSaveStoreResult saved =
                    store.SaveEnvelope(envelope, archiveLegacy2D: true);

                Assert.That(saved.Code,
                    Is.EqualTo(FormalSaveStoreCode.SaveSucceeded),
                    saved.Message + " " + saved.Diagnostic);
                FormalSaveDecodeResult current = FormalSaveCodec.DecodeAny(
                    File.ReadAllText(primary));
                Assert.That(current.PayloadKind,
                    Is.EqualTo(FormalSavePayloadKind.Formal3D));
                string[] archives = Directory.GetFiles(
                    directory.Path,
                    "formal-world.legacy-schema-30-*.json");
                Assert.That(archives, Has.Length.EqualTo(1));
                Assert.That(File.ReadAllText(archives[0]),
                    Is.EqualTo(legacyJson));
                FormalSaveDecodeResult archived = FormalSaveCodec.DecodeAny(
                    File.ReadAllText(archives[0]));
                Assert.That(
                    FormalSaveValidator.ValidateDecoded(archived).IsValid,
                    Is.True);
            }
        }

        private static bool IsValidBytes(byte[] bytes)
        {
            return bytes != null &&
                   Encoding.UTF8.GetString(bytes).EndsWith(
                       "-valid",
                       StringComparison.Ordinal);
        }

        private static byte[] Bytes(string value) =>
            Encoding.UTF8.GetBytes(value);

        private static FormalSaveEnvelope LoadFixtureEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            return decoded.Envelope;
        }

        private static string ReadFixture(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Tests/Fixtures/Persistence",
                fileName));
        }

        private sealed class FakeClock : IFormalSaveClock
        {
            public FakeClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; set; }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "wastecity-save-test-" + Guid.NewGuid().ToString("N"));
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
            private int operation;

            public int FailAtOperation { get; set; } = -1;
            public List<string> Operations { get; } = new List<string>();
            public IEnumerable<string> Paths => files.Keys;

            public void Seed(string path, string value)
            {
                files[path] = Bytes(value);
            }

            public string Text(string path) =>
                Encoding.UTF8.GetString(files[path]);

            public bool TryText(string path, out string value)
            {
                if (files.TryGetValue(path, out byte[] bytes))
                {
                    value = Encoding.UTF8.GetString(bytes);
                    return true;
                }
                value = null;
                return false;
            }

            public bool FileExists(string path) => files.ContainsKey(path);

            public byte[] ReadAllBytes(string path)
            {
                Step("read:" + path);
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
                return targetPath + "." + purpose + ".tmp";
            }

            public void WriteAllBytesAndFlush(string path, byte[] bytes)
            {
                Step("write:" + path);
                files[path] = (byte[])bytes.Clone();
            }

            public void ReplaceAtomically(
                string sourcePath,
                string destinationPath)
            {
                Step("replace:" + sourcePath + "->" + destinationPath);
                if (!files.TryGetValue(sourcePath, out byte[] bytes))
                    throw new FileNotFoundException(sourcePath);
                files[destinationPath] = bytes;
                files.Remove(sourcePath);
            }

            public void DeleteIfExists(string path)
            {
                files.Remove(path);
            }

            private void Step(string description)
            {
                Operations.Add(description);
                int current = operation++;
                if (current != FailAtOperation) return;
                FailAtOperation = -1;
                throw new IOException("injected: " + description);
            }
        }
    }
}
