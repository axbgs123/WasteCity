using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Core;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveRuntimeHostTests
    {
        private const string HostTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxFormalSaveRuntimeHost3D";
        private const string LatchTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxFormalSaveWriteIntentLatch3D";
        private const string SourcePath =
            "Assets/_Game/Scripts/Graybox3D/Building/" +
            "GrayboxFormalSaveRuntimeHost3D.cs";

        [Test]
        public void IDEA0015_HostOwnsExplicitSerializedRuntimeReferences()
        {
            Type host = RequireType(HostTypeName);
            var expected = new[]
            {
                typeof(GrayboxSceneBootstrap),
                typeof(GrayboxMobileCityController3D),
                typeof(GrayboxWorldView3D),
                typeof(GrayboxBuildingSession3D),
                typeof(GrayboxBuildingWorldView3D),
                typeof(GrayboxOperationsController3D),
                typeof(GrayboxProductionController3D),
                typeof(GrayboxDefenseController3D),
                typeof(GrayboxEvacuationController3D),
            };

            FieldInfo[] fields = host.GetFields(
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public);
            for (var index = 0; index < expected.Length; index++)
            {
                FieldInfo field = fields.SingleOrDefault(value =>
                    value.FieldType == expected[index]);
                Assert.That(field, Is.Not.Null, expected[index].Name);
                Assert.That(
                    field.GetCustomAttribute<SerializeField>(),
                    Is.Not.Null,
                    expected[index].Name + " must be authored explicitly.");
            }
        }

        [Test]
        public void IDEA0015_HostExposesNarrowStructuredCommandBoundary()
        {
            Type host = RequireType(HostTypeName);
            Assert.That(host.IsSubclassOf(typeof(MonoBehaviour)), Is.True);

            AssertProperty(host, "Speed", typeof(GameSpeedModel));
            AssertProperty(host, "IsInitialized", typeof(bool));
            AssertProperty(
                host,
                "LastStoreResult",
                typeof(FormalSaveStoreResult));
            AssertProperty(
                host,
                "LastCoordinatorResult",
                typeof(GrayboxFormalSaveCoordinatorResult3D));
            AssertProperty(
                host,
                "LastWaveRetryStoreResult",
                typeof(FormalSaveWaveRetryStoreResult));
            AssertMethod(host, "Probe", typeof(FormalSaveStoreResult));
            AssertMethod(host, "TryStartNewProgress", typeof(bool));
            AssertMethod(host, "TryContinue", typeof(bool));
            AssertMethod(host, "TryRetryWaveCheckpoint", typeof(bool));
            AssertMethod(host, "TrySaveAndExit", typeof(bool));
            AssertMethod(host, "FlushPendingCheckpoint", typeof(bool));
        }

        [Test]
        public void IDEA0015_HostIsTheOnlyCompositionRootNotASecondFileService()
        {
            Assert.That(File.Exists(SourcePath), Is.True);
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("new FormalSaveStore(", source);
            StringAssert.Contains("Application.persistentDataPath", source);
            StringAssert.Contains("GrayboxFormalSaveCoordinator3D", source);
            StringAssert.Contains("FormalSaveCheckpointPolicy", source);
            StringAssert.Contains("new GameSpeedModel()", source);
            StringAssert.DoesNotContain("System.IO", source);
            StringAssert.DoesNotContain("File.", source);
            StringAssert.DoesNotContain("FindObjectOfType", source);
            StringAssert.DoesNotContain("PlayerPrefs", source);
        }

        [Test]
        public void IDEA0015_NewProgressIntentClearsOnlyAfterSuccess()
        {
            Type latchType = RequireType(LatchTypeName);
            object latch = Activator.CreateInstance(latchType, true);
            MethodInfo begin = latchType.GetMethod(
                "BeginNewProgress",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            MethodInfo complete = latchType.GetMethod(
                "CompleteWrite",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            PropertyInfo intent = latchType.GetProperty(
                "Intent",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            PropertyInfo archive = latchType.GetProperty(
                "ArchiveLegacy2D",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(begin, Is.Not.Null);
            Assert.That(complete, Is.Not.Null);
            Assert.That(intent, Is.Not.Null);
            Assert.That(archive, Is.Not.Null);

            begin.Invoke(latch, new object[] { true });
            Assert.That(
                intent.GetValue(latch),
                Is.EqualTo(FormalSaveWriteIntent.StartNewProgress));
            Assert.That(archive.GetValue(latch), Is.True);

            complete.Invoke(latch, new object[] { false });
            Assert.That(
                intent.GetValue(latch),
                Is.EqualTo(FormalSaveWriteIntent.StartNewProgress));
            Assert.That(archive.GetValue(latch), Is.True);

            complete.Invoke(latch, new object[] { true });
            Assert.That(
                intent.GetValue(latch),
                Is.EqualTo(FormalSaveWriteIntent.ContinueProgress));
            Assert.That(archive.GetValue(latch), Is.False);
        }

        [Test]
        public void IDEA0015_ContinueLoadsFormal3DBeforeCoordinatorApply()
        {
            string source = File.ReadAllText(SourcePath);
            string method = ExtractMethod(source, "public bool TryContinue(");
            int load = method.IndexOf(
                "store.Load(FormalSavePayloadKind.Formal3D)",
                StringComparison.Ordinal);
            int restore = method.IndexOf(
                "coordinator.RestoreEnvelope(",
                StringComparison.Ordinal);

            Assert.That(load, Is.GreaterThanOrEqualTo(0));
            Assert.That(restore, Is.GreaterThan(load));
            StringAssert.DoesNotContain("Legacy2D", method);
        }

        [Test]
        public void IDEA0017_WaveRetryUsesDedicatedValidatedArtifactAndFullRestore()
        {
            string source = File.ReadAllText(SourcePath);
            string retry = ExtractMethod(
                source,
                "public bool TryRetryWaveCheckpoint(");
            int load = retry.IndexOf(
                "waveRetryStore.Load()",
                StringComparison.Ordinal);
            int restore = retry.IndexOf(
                "coordinator.RestoreEnvelope(",
                StringComparison.Ordinal);
            Assert.That(load, Is.GreaterThanOrEqualTo(0));
            Assert.That(restore, Is.GreaterThan(load));
            StringAssert.DoesNotContain("ApplyCoreDamage", retry);
            StringAssert.DoesNotContain("RestoreCore", retry);
            StringAssert.Contains("currentSessionId", retry);
            StringAssert.Contains("CampaignWaveWarningStarted", retry);
            StringAssert.Contains("CurrentWaveNumber", retry);
            StringAssert.Contains(
                "SingleCityDefenseCampaignPhase.Warning",
                retry,
                "A retry artifact must be a true wave-front snapshot, not a later state carrying the old reason id.");

            string write = ExtractMethod(
                source,
                "private bool TryWriteCheckpoint(");
            StringAssert.Contains(
                "FormalSaveCheckpointReasonIds.CampaignWaveWarningStarted",
                write);
            StringAssert.Contains("waveRetryStore.Save(", write);
            Assert.That(
                write.IndexOf("store.SaveEnvelope(",
                    StringComparison.Ordinal),
                Is.LessThan(write.IndexOf("waveRetryStore.Save(",
                    StringComparison.Ordinal)),
                "The player save must commit before its internal retry copy.");
            StringAssert.Contains("LastWaveRetryStoreResult.Success", write);
            StringAssert.Contains("retryArtifactSucceeded", write);
            StringAssert.Contains("SetCheckpointWarning(true)", write);
            StringAssert.DoesNotContain(
                "checkpointSucceeded =\n                        LastWaveRetryStoreResult.Success",
                write,
                "A failed internal retry copy must not requeue the already committed player checkpoint for recapture.");
            StringAssert.Contains("return checkpointSucceeded", write);
        }

        [Test]
        public void IDEA0015_NewProgressProbesIdentityWithoutApplyingOldPayload()
        {
            string source = File.ReadAllText(SourcePath);
            string method = ExtractMethod(
                source,
                "public bool TryStartNewProgress(");

            StringAssert.Contains("Probe()", method);
            StringAssert.Contains("BeginNewProgress", method);
            StringAssert.Contains("NewGameReady", method);
            StringAssert.DoesNotContain("RestoreEnvelope", method);
            StringAssert.DoesNotContain("TryContinue", method);
        }

        [Test]
        public void IDEA0015_LateUpdateFlushesOnlyAnActualPendingCheckpoint()
        {
            string source = File.ReadAllText(SourcePath);
            string method = ExtractMethod(source, "private void LateUpdate(");

            StringAssert.Contains("checkpointPolicy.HasPending", method);
            StringAssert.Contains("FlushPendingCheckpoint", method);
            Assert.That(
                method.IndexOf("checkpointPolicy.HasPending",
                    StringComparison.Ordinal),
                Is.LessThan(method.IndexOf(
                    "FlushPendingCheckpoint",
                    StringComparison.Ordinal)));
        }

        [Test]
        public void IDEA0015_ManualSaveAdvancesMetadataAndUsesBuildIdentity()
        {
            string source = File.ReadAllText(SourcePath);
            string save = ExtractMethod(
                source,
                "public bool TrySaveAndExit(");
            string write = ExtractMethod(
                source,
                "private bool TryWriteCheckpoint(");

            StringAssert.Contains(
                "checkpointPolicy.Sequence + 1L",
                save);
            StringAssert.Contains("ResolveGameVersion()", write);
            StringAssert.Contains("builtin:wastecity@", write);
            StringAssert.Contains("FallbackGameVersion", source);
        }

        [Test]
        public void IDEA0015_NewProgressRejectsRollbackFailedSafetyBarrierWithoutMutation()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "wastecity-host-safety-barrier-" +
                Guid.NewGuid().ToString("N"));
            var root = new GameObject("FormalSaveHost.SafetyBarrier");
            root.SetActive(false);
            try
            {
                GrayboxFormalSaveRuntimeHost3D host =
                    root.AddComponent<GrayboxFormalSaveRuntimeHost3D>();
                GrayboxBuildingSession3D session =
                    root.AddComponent<GrayboxBuildingSession3D>();
                session.Configure(true);
                Assert.That(
                    session.TryRestoreCheckpointRuleTime(
                        12f,
                        out string checkpointError),
                    Is.True,
                    checkpointError);

                var policy = new FormalSaveCheckpointPolicy(
                    _ => true,
                    () => session.CheckpointRuleTimeSeconds);
                Assert.That(
                    policy.TryRestoreBaseline(
                        new FormalSaveCheckpointMetadata
                        {
                            sequence = 7L,
                            reasonId = FormalSaveCheckpointReasonIds
                                .FirstDeploymentComplete,
                            ruleTimeSeconds = 12f,
                            completedMilestoneIds = new[]
                            {
                                FormalSaveCheckpointReasonIds
                                    .FirstDeploymentComplete,
                            },
                        }),
                    Is.True);

                GrayboxFormalSaveCoordinator3D coordinator =
                    CreateSafetyBarrierCoordinator();
                GrayboxFormalSaveCoordinatorResult3D rollbackFailed =
                    CreateRollbackFailedResult();
                SetPrivateField(host, "store", new FormalSaveStore(directory));
                SetPrivateField(host, "session", session);
                SetPrivateField(host, "coordinator", coordinator);
                SetPrivateField(host, "checkpointPolicy", policy);
                SetPrivateField(
                    host,
                    "<IsInitialized>k__BackingField",
                    true);
                SetPrivateField(
                    host,
                    "<LastCoordinatorResult>k__BackingField",
                    rollbackFailed);

                bool started = host.TryStartNewProgress();

                Assert.That(started, Is.False);
                Assert.That(coordinator.IsTransactionPaused, Is.True);
                Assert.That(host.LastCoordinatorResult, Is.Not.Null);
                Assert.That(
                    host.LastCoordinatorResult.RequiresSafeReturnToTitle,
                    Is.True);
                Assert.That(policy.Sequence, Is.EqualTo(7L));
                CollectionAssert.AreEqual(
                    new[]
                    {
                        FormalSaveCheckpointReasonIds.FirstDeploymentComplete,
                    },
                    policy.CompletedMilestoneIds);
                Assert.That(
                    session.CheckpointRuleTimeSeconds,
                    Is.EqualTo(12f));
                Assert.That(
                    ReadPrivateField<string>(host, "currentSessionId"),
                    Is.Empty);
                Assert.That(policy.HasPending, Is.False);
                Assert.That(
                    ReadWriteIntent(host),
                    Is.EqualTo(FormalSaveWriteIntent.ContinueProgress));
                Assert.That(
                    Directory.Exists(directory),
                    Is.False,
                    "A rejected new session must not touch the save slot.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void IDEA0015_AutomaticCheckpointRetriesOnlyAfterNewEffectiveEvent()
        {
            var root = new GameObject("FormalSaveHost.CheckpointRetry");
            root.SetActive(false);
            try
            {
                GrayboxFormalSaveRuntimeHost3D host =
                    root.AddComponent<GrayboxFormalSaveRuntimeHost3D>();
                var attempts = 0;
                var policy = new FormalSaveCheckpointPolicy(
                    _ => ++attempts >= 2,
                    () => 18f);
                SetPrivateField(host, "checkpointPolicy", policy);
                SetPrivateField(
                    host,
                    "<IsInitialized>k__BackingField",
                    true);
                Assert.That(
                    policy.QueueCheckpoint(
                        FormalSaveCheckpointReasonIds
                            .EvacuationWorkCommitted,
                        "evacuation.batch.000001|building.000001"),
                    Is.True);

                Assert.That(host.FlushPendingCheckpoint(), Is.False);
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(policy.HasPending, Is.True);
                Assert.That(policy.HasFailureWarning, Is.True);
                Assert.That(host.HasCheckpointWarning, Is.True);

                InvokePrivate(host, "LateUpdate");

                Assert.That(
                    attempts,
                    Is.EqualTo(1),
                    "An unchanged failed batch must not retry every frame.");
                Assert.That(policy.HasPending, Is.True);
                Assert.That(policy.HasFailureWarning, Is.True);
                Assert.That(host.HasCheckpointWarning, Is.True);

                Assert.That(
                    policy.QueueCheckpoint(
                        FormalSaveCheckpointReasonIds
                            .EvacuationWorkCommitted,
                        "evacuation.batch.000001|building.000002"),
                    Is.True);
                InvokePrivate(host, "LateUpdate");

                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(policy.HasPending, Is.False);
                Assert.That(policy.HasFailureWarning, Is.False);
                Assert.That(host.HasCheckpointWarning, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IDEA0017_SuccessfulPlayerCheckpointPreservesRetryArtifactWarning()
        {
            var root = new GameObject("FormalSaveHost.RetryArtifactWarning");
            root.SetActive(false);
            try
            {
                GrayboxFormalSaveRuntimeHost3D host =
                    root.AddComponent<GrayboxFormalSaveRuntimeHost3D>();
                var policy = new FormalSaveCheckpointPolicy(
                    _ => true,
                    () => 18f);
                SetPrivateField(host, "checkpointPolicy", policy);
                SetPrivateField(
                    host,
                    "<IsInitialized>k__BackingField",
                    true);
                SetPrivateField(
                    host,
                    "lastCheckpointHadRetryArtifactFailure",
                    true);
                Assert.That(policy.QueueCheckpoint(
                    FormalSaveCheckpointReasonIds.CampaignWaveWarningStarted,
                    "campaign.wave.000005.warning"), Is.True);

                Assert.That(host.FlushPendingCheckpoint(), Is.True);

                Assert.That(host.HasCheckpointWarning, Is.True,
                    "A successful player save must not hide the failed internal retry artifact warning.");
                Assert.That(policy.HasPending, Is.False,
                    "The committed player checkpoint must not be recaptured.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(GrayboxFormalSaveCoordinator3D).Assembly
                .GetType(fullName, false);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static GrayboxFormalSaveCoordinator3D
            CreateSafetyBarrierCoordinator()
        {
            var domains = new List<IFormalThreeDSaveDomain>();
            foreach (GrayboxFormalSaveDomainId3D domainId in
                     GrayboxFormalSaveCoordinator3D.DomainOrder)
            {
                domains.Add(new HostTestDomain(domainId));
            }
            var coordinator = new GrayboxFormalSaveCoordinator3D(
                domains,
                new HostTestRebuilder());
            SetPrivateField(coordinator, "transactionActive", true);
            return coordinator;
        }

        private static GrayboxFormalSaveCoordinatorResult3D
            CreateRollbackFailedResult()
        {
            ConstructorInfo constructor =
                typeof(GrayboxFormalSaveCoordinatorResult3D).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(bool),
                        typeof(GrayboxFormalSaveCoordinatorCode3D),
                        typeof(string),
                        typeof(FormalSaveEnvelope),
                        typeof(GrayboxFormalSaveDomainId3D?),
                        typeof(bool),
                        typeof(bool),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            return (GrayboxFormalSaveCoordinatorResult3D)constructor.Invoke(
                new object[]
                {
                    false,
                    GrayboxFormalSaveCoordinatorCode3D.RollbackFailed,
                    "加载失败且无法安全回滚",
                    null,
                    null,
                    true,
                    false,
                });
        }

        private static void SetPrivateField(
            object owner,
            string fieldName,
            object value)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(owner, value);
        }

        private static T ReadPrivateField<T>(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(owner);
        }

        private static void InvokePrivate(object owner, string methodName)
        {
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            try
            {
                method.Invoke(owner, Array.Empty<object>());
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static FormalSaveWriteIntent ReadWriteIntent(
            GrayboxFormalSaveRuntimeHost3D host)
        {
            object latch = ReadPrivateField<object>(host, "writeIntent");
            PropertyInfo property = latch.GetType().GetProperty(
                "Intent",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (FormalSaveWriteIntent)property.GetValue(latch);
        }

        private static void AssertProperty(
            Type owner,
            string name,
            Type propertyType)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.PropertyType, Is.EqualTo(propertyType), name);
            Assert.That(property.CanRead, Is.True, name);
        }

        private static void AssertMethod(
            Type owner,
            string name,
            Type returnType)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null, name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType), name);
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int open = source.IndexOf('{', start);
            Assert.That(open, Is.GreaterThan(start), signature);
            var depth = 0;
            for (var index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                if (source[index] != '}') continue;
                depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            Assert.Fail("Unterminated method: " + signature);
            return string.Empty;
        }

        private sealed class HostTestDomain : IFormalThreeDSaveDomain
        {
            public HostTestDomain(GrayboxFormalSaveDomainId3D domainId)
            {
                DomainId = domainId;
            }

            public GrayboxFormalSaveDomainId3D DomainId { get; }

            public bool TryCapture(
                FormalThreeDSaveData destination,
                out string error)
            {
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                FormalThreeDSaveData source,
                out string error)
            {
                error = string.Empty;
                return true;
            }
        }

        private sealed class HostTestRebuilder :
            IFormalThreeDDerivedStateRebuilder
        {
            public void RebuildDerivedState()
            {
            }
        }
    }
}
