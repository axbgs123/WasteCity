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

        [Test]
        public void IDEA0020_ClearCommitsMetadataOnlyAfterStoreSuccess()
        {
            using (Harness harness = Harness.Create())
            {
                SelectRewind(harness.Fate);
                var metadata = new FormalRewindAnchorMetadataRuntime();
                object service = CreateService(harness, metadata);
                AssertResult(CreateAnchor(service, 15L), true, "Created");
                long nextOrdinal =
                    metadata.Capture().NextCreationOrdinal;
                ulong revision = metadata.Capture().Revision;

                AssertResult(Invoke(service, "Clear"), true, "Cleared");

                Assert.That(harness.Store.Load().Code,
                    Is.EqualTo(FormalRewindAnchorStoreCode.NoAnchor));
                Assert.That(metadata.Capture().Entries, Is.Empty);
                Assert.That(metadata.Capture().NextCreationOrdinal,
                    Is.EqualTo(nextOrdinal));
                Assert.That(metadata.Capture().Revision,
                    Is.EqualTo(revision + 1UL));
            }

            var files = new FailingWriteFileSystem { FailNextDelete = true };
            using (Harness harness = Harness.Create(files))
            {
                SelectRewind(harness.Fate);
                var metadata = new FormalRewindAnchorMetadataRuntime();
                object service = CreateService(harness, metadata);
                AssertResult(CreateAnchor(service, 16L), true, "Created");
                FormalRewindAnchorMetadataSnapshot before = metadata.Capture();

                AssertResult(Invoke(service, "Clear"),
                    false, "StoreFailed");
                Assert.That(metadata.Capture(), Is.SameAs(before));
            }
        }

        [Test]
        public void IDEA0020_LevelOneSlotReadAfterLevelTwoPreservesBothSlots()
        {
            var metadata = new FormalRewindAnchorMetadataRuntime();
            using (Harness harness = Harness.Create(
                       null,
                       metadata,
                       selectedRewind: true))
            {
                object service = CreateService(harness, metadata);
                int initial = harness.Authority.WorldSeed;
                AssertResult(CreateAnchor(service, 30L), true, "Created");
                harness.Authority.MutateWorldSeed(1);

                Assert.That(harness.Fate.TryPromoteToLevelTwo(
                    out string error), Is.True, error);
                Assert.That(metadata.TrySetFateLevel(2, out error), Is.True,
                    error);
                Assert.That(harness.Civilization.TryRestore(
                    new FormalCivilizationAscensionSnapshot(
                        2,
                        FormalFateCatalog.RewindAnchorId,
                        2,
                        true,
                        1ul),
                    out error), Is.True, error);
                harness.Sequence.Restore(
                    (int)AdvancementSequenceStage.Continued,
                    0f);
                AssertResult(CreateAnchor(service, 31L), true, "Created");
                FormalRewindAnchorMetadataSnapshot beforeRead =
                    metadata.Capture();
                Assert.That(beforeRead.Entries.Select(value => value.AnchorId),
                    Is.EqualTo(new[]
                    {
                        GrayboxRewindAnchorService3D.StableAnchorId,
                        GrayboxRewindAnchorService3D.SecondStableAnchorId,
                    }));
                Assert.That(harness.Store.Load(1).Envelope.formal3D
                    .progression.fate.level, Is.EqualTo(1));
                Assert.That(harness.Store.Load(2).Envelope.formal3D.world.worldSeed,
                    Is.EqualTo(initial + 1));

                harness.Authority.MutateWorldSeed(10);
                object read = Invoke(
                    service,
                    "Read",
                    GrayboxRewindAnchorService3D.StableAnchorId);
                AssertResult(read, true, "ReadSucceeded");
                Assert.That(harness.Authority.WorldSeed, Is.EqualTo(initial));
                Assert.That(harness.Attention.Value, Is.EqualTo(22));
                Assert.That(harness.Fate.Capture().Level, Is.EqualTo(2));
                Assert.That(harness.Civilization.Capture().CivilizationLevel,
                    Is.EqualTo(2));
                Assert.That(metadata.Capture().Entries.Select(
                        value => value.AnchorId),
                    Is.EqualTo(beforeRead.Entries.Select(
                        value => value.AnchorId)));

                GrayboxFormalSaveCoordinatorResult3D captured =
                    harness.Coordinator.CaptureEnvelope(
                        harness.SessionId,
                        "test.idea-0020",
                        new[] { "builtin:wastecity@test.idea-0020" },
                        new FormalSaveCheckpointMetadata
                        {
                            sequence = 32L,
                            reasonId = FormalSaveCheckpointReasonIds.NewGameReady,
                            ruleTimeSeconds = 18f,
                            completedMilestoneIds = Array.Empty<string>(),
                        },
                        new DateTime(
                            2026, 8, 27, 2, 0, 0, DateTimeKind.Utc));
                Assert.That(captured.Success, Is.True, captured.Message);
                Assert.That(FormalSaveValidator.ValidateEnvelope(
                    captured.Envelope).IsValid, Is.True);
            }
        }

        [Test]
        public void IDEA0028_ReadKeepsVoidChestIdempotencyAcrossRestart()
        {
            using (Harness harness = Harness.Create(
                       selectedRewind: true))
            {
                object service = CreateService(harness);
                AssertResult(CreateAnchor(service, 40L), true, "Created");

                string droppedDeathId = FindDropAtOrdinal(
                    harness.SessionId,
                    harness.VoidChest.SelectionVersion,
                    1ul);
                Assert.That(harness.VoidChest.TryEvaluateDeath(
                    droppedDeathId,
                    1ul,
                    out VoidChestEvaluation dropped,
                    out string error), Is.True, error);
                Assert.That(dropped.Dropped, Is.True);
                Assert.That(harness.VoidChest.TryClaim(
                    dropped.ChestId, out error), Is.True, error);
                Assert.That(harness.VoidChest.TryEvaluateDeath(
                    "zz-enemy-after-claimed-drop",
                    2ul,
                    out _,
                    out error), Is.True, error);

                AssertResult(Invoke(service, "Read"),
                    true, "ReadSucceeded");

                VoidChestSnapshot afterRead = harness.VoidChest.Capture();
                Assert.That(afterRead.Evaluations.Select(value =>
                        (value.DeathId, value.SequenceOrdinal)),
                    Is.EqualTo(new[]
                    {
                        (droppedDeathId, 1ul),
                        ("zz-enemy-after-claimed-drop", 2ul),
                    }));
                Assert.That(afterRead.ClaimedChestIds,
                    Is.EqualTo(new[] { dropped.ChestId }));

                GrayboxFormalSaveCoordinatorResult3D saved =
                    harness.Coordinator.CaptureEnvelope(
                        harness.SessionId,
                        "test.idea-0028",
                        new[] { "builtin:wastecity@test.idea-0028" },
                        new FormalSaveCheckpointMetadata
                        {
                            sequence = 41L,
                            reasonId = FormalSaveCheckpointReasonIds
                                .RewindAnchorUsed,
                            ruleTimeSeconds = 20f,
                            completedMilestoneIds = Array.Empty<string>(),
                        },
                        new DateTime(
                            2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
                Assert.That(saved.Success, Is.True, saved.Message);
                VoidChestRuntime restarted = RestoreVoidChest(
                    saved.Envelope.formal3D.progression,
                    harness.SessionId,
                    harness.VoidChest.SelectionVersion);
                VoidChestSnapshot afterRestart = restarted.Capture();
                Assert.That(afterRestart.Evaluations.Select(value =>
                        (value.DeathId, value.SequenceOrdinal)),
                    Is.EqualTo(afterRead.Evaluations.Select(value =>
                        (value.DeathId, value.SequenceOrdinal))));
                Assert.That(afterRestart.ClaimedChestIds,
                    Is.EqualTo(new[] { dropped.ChestId }));
                Assert.That(saved.Envelope.formal3D.progression.voidChest
                    .nextDropOrdinal, Is.EqualTo(3L));
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

        private static string FindDropAtOrdinal(
            string sessionId,
            int selectionVersion,
            ulong ordinal)
        {
            for (var index = 1; index <= 10000; index++)
            {
                string deathId = "rewind-void-death-" + index;
                if (VoidChestRuntime.ShouldDrop(
                        sessionId,
                        selectionVersion,
                        deathId,
                        ordinal))
                    return deathId;
            }
            Assert.Fail("A deterministic VoidChest drop was not found.");
            return string.Empty;
        }

        private static VoidChestRuntime RestoreVoidChest(
            FormalThreeDProgressionSaveData progression,
            string sessionId,
            int selectionVersion)
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            var fate = new FormalFateRuntime();
            var voidChest = new VoidChestRuntime(sessionId, selectionVersion);
            var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                attention,
                fate,
                new PocketUniverseFateEffect(),
                new FormalVoidDebtRuntime(),
                new FormalRewindAnchorMetadataRuntime(),
                new GrayboxAttentionPressureSaveAdapter3D(
                    pressure,
                    new GrayboxDefenseRuntime3D(0f, 0f, 9f, 0f)),
                new FormalCivilizationAscensionRuntime(
                    FormalFateCatalog.RewindAnchorId),
                new AdvancementSequenceModel(),
                CreateQuantumRuntime(),
                new SpatialTemplateRuntime(),
                new LocalHasteRuntime(),
                new ForesightDelayRuntime(),
                new CausalTransparencyRuntime(),
                voidChest,
                new CoordinateLockRuntime(attention, pressure));
            Assert.That(adapter.TryRestore(
                CloneProgression(progression), out string error),
                Is.True, error);
            return voidChest;
        }

        private static FormalThreeDProgressionSaveData CloneProgression(
            FormalThreeDProgressionSaveData source)
        {
            return JsonUtility.FromJson<FormalThreeDProgressionSaveData>(
                JsonUtility.ToJson(source, false));
        }

        private static QuantumEntanglementRuntime CreateQuantumRuntime()
        {
            return new QuantumEntanglementRuntime(new[]
            {
                "core.resource.iron",
                "core.resource.stone",
                "core.resource.water",
                "core.resource.biomass",
            });
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
            MethodInfo method = owner.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate => candidate.Name == name &&
                    candidate.GetParameters().Length == arguments.Length &&
                    candidate.GetParameters().Select((parameter, index) =>
                        arguments[index] == null ||
                        parameter.ParameterType.IsInstanceOfType(
                            arguments[index])).All(value => value));
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
            Assert.That(Read<bool>(result, "Success"), Is.EqualTo(success),
                Read<string>(result, "Message") + " | " +
                Read<string>(result, "Diagnostic"));
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
                FormalCivilizationAscensionRuntime civilization,
                AdvancementSequenceModel sequence,
                FormalRewindAnchorStore store,
                GrayboxFormalSaveCoordinator3D coordinator,
                VoidChestRuntime voidChest,
                int initialWorldSeed,
                string sessionId)
            {
                this.root = root;
                Authority = authority;
                Attention = attention;
                Fate = fate;
                Civilization = civilization;
                Sequence = sequence;
                Store = store;
                Coordinator = coordinator;
                VoidChest = voidChest;
                InitialWorldSeed = initialWorldSeed;
                SessionId = sessionId;
            }

            public MutableAuthority Authority { get; }
            public FormalAttentionRuntime Attention { get; }
            public FormalFateRuntime Fate { get; }
            public FormalCivilizationAscensionRuntime Civilization { get; }
            public AdvancementSequenceModel Sequence { get; }
            public FormalRewindAnchorStore Store { get; }
            public GrayboxFormalSaveCoordinator3D Coordinator { get; }
            public VoidChestRuntime VoidChest { get; }
            public int InitialWorldSeed { get; }
            public string SessionId { get; }

            public static Harness Create(
                IFormalSaveFileSystem fileSystem = null,
                FormalRewindAnchorMetadataRuntime rewindMetadata = null,
                bool selectedRewind = false)
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
                if (selectedRewind) SelectRewind(fate);
                FormalCivilizationAscensionRuntime civilization =
                    selectedRewind
                        ? new FormalCivilizationAscensionRuntime(
                            FormalFateCatalog.RewindAnchorId)
                        : null;
                AdvancementSequenceModel sequence = selectedRewind
                    ? new AdvancementSequenceModel()
                    : null;
                var pressure = new AttentionPressureRuntime();
                var voidChest = new VoidChestRuntime(payload.sessionId, 1);
                var adapter = new GrayboxFormalProgressionSaveAdapter3D(
                    attention,
                    fate,
                    new PocketUniverseFateEffect(),
                    new FormalVoidDebtRuntime(),
                    rewindMetadata ?? new FormalRewindAnchorMetadataRuntime(),
                    new GrayboxAttentionPressureSaveAdapter3D(
                        pressure,
                        new GrayboxDefenseRuntime3D(0f, 0f, 9f, 0f)),
                    civilization,
                    sequence,
                    CreateQuantumRuntime(),
                    new SpatialTemplateRuntime(),
                    new LocalHasteRuntime(),
                    new ForesightDelayRuntime(),
                    new CausalTransparencyRuntime(),
                    voidChest,
                    new CoordinateLockRuntime(attention, pressure));
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
                    civilization,
                    sequence,
                    new FormalRewindAnchorStore(root.Path, fileSystem),
                    coordinator,
                    voidChest,
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
            public bool FailNextDelete { get; set; }
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
            public void DeleteIfExists(string path)
            {
                if (FailNextDelete)
                {
                    FailNextDelete = false;
                    throw new IOException("injected delete failure");
                }
                files.Remove(path);
            }
        }
    }
}
