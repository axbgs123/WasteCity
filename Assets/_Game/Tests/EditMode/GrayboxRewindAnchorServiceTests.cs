using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxRewindAnchorServiceTests
    {
        private const string ServiceTypeName =
            "WasteCity.Graybox3D.Building.GrayboxRewindAnchorService3D";
        private string safetyCode;
        private string currentSessionId;

        [Test]
        public void IDEA0020_OnlySelectedRewindFateCanCreateAnchor()
        {
            using (Harness harness = Harness.Create())
            {
                object service = CreateService(harness);
                object blocked = CreateAnchor(service, 1L);
                AssertResult(blocked, false, "WrongFate");
                Assert.That(Read<string>(blocked, "Message"),
                    Does.Contain("回溯锚点命轨"));
                Assert.That(harness.Store.Load().Code,
                    Is.EqualTo(FormalRewindAnchorStoreCode.NoAnchor));

                SelectRewind(harness.Fate);
                AssertResult(CreateAnchor(service, 1L), true, "Created");
            }
        }

        [TestCase("save-transaction", "存档事务正在进行")]
        [TestCase("deployment-transition", "城市正在部署或收拢")]
        [TestCase("evacuation", "撤离流程正在进行")]
        [TestCase("combat", "战斗进行中")]
        public void IDEA0020_SafetyGateReturnsFixedChineseWithoutWrites(
            string code,
            string expectedMessage)
        {
            using (Harness harness = Harness.Create())
            {
                SelectRewind(harness.Fate);
                object service = CreateService(harness);
                safetyCode = code;
                object result = CreateAnchor(service, 2L);

                AssertResult(result, false, "SafetyBlocked");
                Assert.That(Read<string>(result, "Message"),
                    Is.EqualTo(expectedMessage + "，无法使用回溯锚点"));
                Assert.That(harness.Store.Load().Code,
                    Is.EqualTo(FormalRewindAnchorStoreCode.NoAnchor));
                Assert.That(harness.Authority.WorldSeed,
                    Is.EqualTo(harness.InitialWorldSeed));
            }
        }

        [Test]
        public void IDEA0020_CreateAtomicallyReplacesTheSingleAnchor()
        {
            using (Harness harness = Harness.Create())
            {
                SelectRewind(harness.Fate);
                object service = CreateService(harness);
                AssertResult(CreateAnchor(service, 3L), true, "Created");
                harness.Authority.MutateWorldSeed(11);
                AssertResult(CreateAnchor(service, 4L), true, "Replaced");

                FormalRewindAnchorStoreResult loaded = harness.Store.Load();
                Assert.That(loaded.Success, Is.True, loaded.Diagnostic);
                Assert.That(loaded.Envelope.checkpoint.sequence, Is.EqualTo(4L));
                Assert.That(loaded.Envelope.formal3D.world.worldSeed,
                    Is.EqualTo(harness.InitialWorldSeed + 11));
            }
        }

        [Test]
        public void IDEA0020_ReadRestoresWorldButKeepsAttentionThresholdsAndAddsTwelve()
        {
            using (Harness harness = Harness.Create())
            {
                SelectRewind(harness.Fate);
                object service = CreateService(harness);
                AssertResult(CreateAnchor(service, 5L), true, "Created");
                harness.Authority.MutateWorldSeed(23);
                ApplyDebt(harness.Attention, 20, "before-read");
                Assert.That(harness.Attention.Value, Is.EqualTo(30));
                Assert.That(harness.Attention.Capture().ReachedThresholds,
                    Is.EqualTo(new[] { 30 }));

                object first = Invoke(service, "Read");

                AssertResult(first, true, "ReadSucceeded");
                Assert.That(harness.Authority.WorldSeed,
                    Is.EqualTo(harness.InitialWorldSeed));
                Assert.That(harness.Attention.Value, Is.EqualTo(42));
                Assert.That(harness.Attention.Capture().ReachedThresholds,
                    Is.EqualTo(new[] { 30 }));
                FormalAttentionHistoryEntry latest =
                    harness.Attention.Capture().History[
                        harness.Attention.Capture().History.Count - 1];
                Assert.That(latest.ReasonId,
                    Is.EqualTo("core.attention.fate.rewind-anchor-used"));
                Assert.That(latest.AppliedDelta, Is.EqualTo(12));
                Assert.That(latest.StableEventKey,
                    Does.StartWith("rewind-anchor-read:"));

                AssertResult(Invoke(service, "Read"),
                    true, "ReadSucceeded");
                Assert.That(harness.Attention.Value, Is.EqualTo(54));
                string[] committed = Copy(
                    harness.Attention.Capture().CommittedStableEventKeys);
                Assert.That(committed, Has.Exactly(2)
                    .StartsWith("rewind-anchor-read:"));
                Assert.That(harness.Store.Load().Success, Is.True,
                    "Reading must retain the one anchor for later use.");
            }
        }

        [Test]
        public void IDEA0020_SessionMismatchAndRestoreFailureLeaveLiveTruthUntouched()
        {
            using (Harness harness = Harness.Create())
            {
                SelectRewind(harness.Fate);
                object service = CreateService(harness);
                AssertResult(CreateAnchor(service, 6L), true, "Created");
                harness.Authority.MutateWorldSeed(31);
                ApplyDebt(harness.Attention, 5, "unchanged");
                string before = harness.Fingerprint();

                currentSessionId = "different-session";
                AssertResult(Invoke(service, "Read"),
                    false, "SessionMismatch");
                Assert.That(harness.Fingerprint(), Is.EqualTo(before));

                currentSessionId = harness.SessionId;
                harness.Authority.FailNextApply = true;
                AssertResult(Invoke(service, "Read"),
                    false, "RestoreFailed");
                Assert.That(harness.Fingerprint(), Is.EqualTo(before));
                Assert.That(harness.Store.Load().Success, Is.True);
            }
        }

        [Test]
        public void IDEA0020_MetadataCommitsAfterStoreAndReadDoesNotDeleteIt()
        {
            using (Harness harness = Harness.Create())
            {
                SelectRewind(harness.Fate);
                var metadata = new FormalRewindAnchorMetadataRuntime();
                object service = CreateService(harness, metadata);
                AssertResult(CreateAnchor(service, 12L), true, "Created");
                FormalRewindAnchorMetadata first =
                    metadata.Capture().Entries.Single();
                Assert.That(first.CreationOrdinal, Is.EqualTo(1L));
                Assert.That(first.AnchorId,
                    Is.EqualTo("rewind-anchor.slot.0001"));
                Assert.That(first.SessionId, Is.EqualTo(harness.SessionId));
                Assert.That(first.CheckpointSequence, Is.EqualTo(12L));
                Assert.That(first.PayloadHashSha256,
                    Is.EqualTo(harness.Store.Load().Envelope.payloadHashSha256));

                harness.Authority.MutateWorldSeed(1);
                AssertResult(CreateAnchor(service, 13L), true, "Replaced");
                Assert.That(metadata.Capture().Entries.Single().CreationOrdinal,
                    Is.EqualTo(2L));
                FormalRewindAnchorMetadataSnapshot beforeRead =
                    metadata.Capture();
                AssertResult(Invoke(service, "Read"),
                    true, "ReadSucceeded");
                Assert.That(metadata.Capture(), Is.SameAs(beforeRead));
            }

            var files = new FailingWriteFileSystem { FailNextWrite = true };
            using (Harness harness = Harness.Create(files))
            {
                SelectRewind(harness.Fate);
                var metadata = new FormalRewindAnchorMetadataRuntime();
                FormalRewindAnchorMetadataSnapshot before = metadata.Capture();
                object service = CreateService(harness, metadata);

                AssertResult(CreateAnchor(service, 14L),
                    false, "StoreFailed");
                Assert.That(metadata.Capture(), Is.SameAs(before),
                    "Failed internal store writes cannot commit metadata.");
            }
        }

        private object CreateService(
            Harness harness,
            FormalRewindAnchorMetadataRuntime metadata = null)
        {
            safetyCode = string.Empty;
            currentSessionId = harness.SessionId;
            Type type = RequireServiceType();
            Type[] parameters = metadata == null
                ? new[]
                {
                    typeof(FormalRewindAnchorStore),
                    typeof(GrayboxFormalSaveCoordinator3D),
                    typeof(FormalAttentionRuntime),
                    typeof(FormalFateRuntime),
                    typeof(Func<string>),
                    typeof(Func<string>),
                }
                : new[]
                {
                    typeof(FormalRewindAnchorStore),
                    typeof(GrayboxFormalSaveCoordinator3D),
                    typeof(FormalAttentionRuntime),
                    typeof(FormalFateRuntime),
                    typeof(Func<string>),
                    typeof(Func<string>),
                    typeof(FormalRewindAnchorMetadataRuntime),
                };
            ConstructorInfo constructor = type.GetConstructor(parameters);
            Assert.That(constructor, Is.Not.Null);
            object[] arguments =
            {
                harness.Store,
                harness.Coordinator,
                harness.Attention,
                harness.Fate,
                (Func<string>)(() => safetyCode),
                (Func<string>)(() => currentSessionId),
            };
            if (metadata != null)
                Array.Resize(ref arguments, 7);
            if (metadata != null) arguments[6] = metadata;
            return constructor.Invoke(arguments);
        }

        private static object CreateAnchor(object service, long sequence)
        {
            return Invoke(
                service,
                "Create",
                "test.idea-0020",
                new[] { "builtin:wastecity@test.idea-0020" },
                new FormalSaveCheckpointMetadata
                {
                    sequence = sequence,
                    reasonId = FormalSaveCheckpointReasonIds.NewGameReady,
                    ruleTimeSeconds = 15f,
                    completedMilestoneIds = Array.Empty<string>(),
                },
                new DateTime(2026, 8, 26, 5, 0, 0, DateTimeKind.Utc));
        }

        private static object Invoke(
            object owner,
            string name,
            params object[] arguments)
        {
            MethodInfo method = owner.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, name);
            try
            {
                return method.Invoke(owner, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
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
            Assert.That(Read<string>(result, "Message"), Is.Not.Empty);
        }

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(owner);
        }

        private static Type RequireServiceType()
        {
            Type type = typeof(GrayboxFormalSaveCoordinator3D).Assembly
                .GetType(ServiceTypeName, false);
            Assert.That(type, Is.Not.Null, ServiceTypeName);
            return type;
        }

        private static void SelectRewind(FormalFateRuntime fate)
        {
            Assert.That(fate.TrySelect(
                FormalFateCatalog.RewindAnchorId,
                out _,
                out _,
                out string error), Is.True, error);
        }

        private static void ApplyDebt(
            FormalAttentionRuntime attention,
            int count,
            string prefix)
        {
            for (var index = 0; index < count; index++)
            {
                Assert.That(attention.TryApply(
                    "core.attention.fate.void-debt-periodic",
                    prefix + "." + index,
                    out string error), Is.True, error);
            }
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }

        private sealed class Harness : IDisposable
        {
            private readonly TemporaryDirectory root;

            private Harness(
                TemporaryDirectory root,
                MutableAuthority authority,
                FormalAttentionRuntime attention,
                FormalFateRuntime fate,
                FormalRewindAnchorStore store,
                GrayboxFormalSaveCoordinator3D coordinator,
                int initialWorldSeed,
                string sessionId)
            {
                this.root = root;
                Authority = authority;
                Attention = attention;
                Fate = fate;
                Store = store;
                Coordinator = coordinator;
                InitialWorldSeed = initialWorldSeed;
                SessionId = sessionId;
            }

            public MutableAuthority Authority { get; }
            public FormalAttentionRuntime Attention { get; }
            public FormalFateRuntime Fate { get; }
            public FormalRewindAnchorStore Store { get; }
            public GrayboxFormalSaveCoordinator3D Coordinator { get; }
            public int InitialWorldSeed { get; }
            public string SessionId { get; }

            public static Harness Create(
                IFormalSaveFileSystem fileSystem = null)
            {
                FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                    File.ReadAllText(Path.Combine(
                        Application.dataPath,
                        "_Game/Tests/Fixtures/Persistence/" +
                        "schema-31-formal-3d.json")));
                Assert.That(decoded.Success, Is.True, decoded.Message);
                FormalThreeDSaveData payload = Clone(decoded.Envelope.formal3D);
                var authority = new MutableAuthority(payload);
                var attention = new FormalAttentionRuntime();
                var fate = new FormalFateRuntime();
                var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                    attention,
                    fate,
                    new PocketUniverseFateEffect(),
                    new FormalVoidDebtRuntime(),
                    new FormalRewindAnchorMetadataRuntime());
                IFormalThreeDSaveDomain[] domains = new IFormalThreeDSaveDomain[
                    GrayboxFormalSaveCoordinator3D.DomainOrder.Count];
                for (var index = 0; index < domains.Length; index++)
                {
                    GrayboxFormalSaveDomainId3D id =
                        GrayboxFormalSaveCoordinator3D.DomainOrder[index];
                    domains[index] = id == GrayboxFormalSaveDomainId3D.Progression
                        ? (IFormalThreeDSaveDomain)new ProgressionDomain(adapter)
                        : new AuthorityDomain(id, authority);
                }
                var coordinator = new GrayboxFormalSaveCoordinator3D(
                    domains,
                    new NoOpRebuilder());
                var root = new TemporaryDirectory();
                return new Harness(
                    root,
                    authority,
                    attention,
                    fate,
                    new FormalRewindAnchorStore(root.Path, fileSystem),
                    coordinator,
                    payload.world.worldSeed,
                    payload.sessionId);
            }

            public string Fingerprint()
            {
                FormalAttentionSnapshot snapshot = Attention.Capture();
                return Authority.WorldSeed + "|" + snapshot.Value + "|" +
                    snapshot.Revision + "|" + snapshot.History.Count;
            }

            public void Dispose()
            {
                root.Dispose();
            }
        }

        private sealed class MutableAuthority
        {
            public MutableAuthority(FormalThreeDSaveData state)
            {
                State = Clone(state);
            }

            public FormalThreeDSaveData State { get; private set; }
            public bool FailNextApply { get; set; }
            public int WorldSeed => State.world.worldSeed;

            public void MutateWorldSeed(int delta)
            {
                State.world.worldSeed += delta;
            }

            public void Capture(FormalThreeDSaveData destination)
            {
                FormalThreeDSaveData copy = Clone(State);
                destination.sessionId = copy.sessionId;
                destination.world = copy.world;
                destination.city = copy.city;
                destination.buildings = copy.buildings;
                destination.storage = copy.storage;
                destination.backpack = copy.backpack;
                destination.crafting = copy.crafting;
                destination.research = copy.research;
                destination.production = copy.production;
                destination.defense = copy.defense;
                destination.defenseCampaign = copy.defenseCampaign;
                destination.evacuation = copy.evacuation;
                destination.pause = copy.pause;
            }

            public bool TryApply(FormalThreeDSaveData source)
            {
                if (FailNextApply)
                {
                    FailNextApply = false;
                    return false;
                }
                FormalThreeDProgressionSaveData progression = State.progression;
                State = Clone(source);
                State.progression = progression;
                return true;
            }
        }

        private sealed class AuthorityDomain : IFormalThreeDSaveDomain
        {
            private readonly MutableAuthority authority;

            public AuthorityDomain(
                GrayboxFormalSaveDomainId3D domainId,
                MutableAuthority authority)
            {
                DomainId = domainId;
                this.authority = authority;
            }

            public GrayboxFormalSaveDomainId3D DomainId { get; }

            public bool TryCapture(
                FormalThreeDSaveData destination,
                out string error)
            {
                if (DomainId == GrayboxFormalSaveDomainId3D.WorldCity)
                    authority.Capture(destination);
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                FormalThreeDSaveData source,
                out string error)
            {
                if (DomainId == GrayboxFormalSaveDomainId3D.WorldCity &&
                    !authority.TryApply(source))
                {
                    error = "injected restore failure";
                    return false;
                }
                error = string.Empty;
                return true;
            }
        }

        private sealed class ProgressionDomain : IFormalThreeDSaveDomain
        {
            private readonly GrayboxFormalProgressionSaveAdapter3D adapter;

            public ProgressionDomain(
                GrayboxFormalProgressionSaveAdapter3D adapter)
            {
                this.adapter = adapter;
            }

            public GrayboxFormalSaveDomainId3D DomainId =>
                GrayboxFormalSaveDomainId3D.Progression;

            public bool TryCapture(
                FormalThreeDSaveData destination,
                out string error)
            {
                destination.progression = adapter.Capture();
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                FormalThreeDSaveData source,
                out string error)
            {
                return adapter.TryRestore(source.progression, out error);
            }
        }

        private sealed class NoOpRebuilder :
            IFormalThreeDDerivedStateRebuilder
        {
            public void RebuildDerivedState()
            {
            }
        }

        private static FormalThreeDSaveData Clone(FormalThreeDSaveData source)
        {
            return JsonUtility.FromJson<FormalThreeDSaveData>(
                JsonUtility.ToJson(source, false));
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "wastecity-rewind-service-test-" +
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }

        private sealed class FailingWriteFileSystem : IFormalSaveFileSystem
        {
            private readonly Dictionary<string, byte[]> files =
                new Dictionary<string, byte[]>(StringComparer.Ordinal);
            private int ordinal;
            public bool FailNextWrite { get; set; }
            public bool FileExists(string path) => files.ContainsKey(path);
            public byte[] ReadAllBytes(string path) =>
                files.TryGetValue(path, out byte[] value)
                    ? (byte[])value.Clone()
                    : throw new FileNotFoundException(path);
            public void CreateDirectory(string path) { }
            public string CreateTemporarySiblingPath(
                string targetPath,
                string purpose) => targetPath + "." + purpose + "." +
                    ordinal++ + ".tmp";
            public void WriteAllBytesAndFlush(string path, byte[] bytes)
            {
                if (FailNextWrite)
                {
                    FailNextWrite = false;
                    throw new IOException("injected write failure");
                }
                files[path] = (byte[])bytes.Clone();
            }
            public void ReplaceAtomically(string source, string destination)
            {
                if (!files.TryGetValue(source, out byte[] value))
                    throw new FileNotFoundException(source);
                files[destination] = value;
                files.Remove(source);
            }
            public void DeleteIfExists(string path) => files.Remove(path);
        }
    }
}
