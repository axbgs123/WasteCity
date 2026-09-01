using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveCoordinatorTests
    {
        private const string RuntimeNamespace =
            "WasteCity.Graybox3D.Building.";

        [Test]
        public void CoordinatorPublishesStructuredResultAndStableCodes()
        {
            Type coordinator = RequireRuntimeType(
                "GrayboxFormalSaveCoordinator3D");
            Type result = RequireRuntimeType(
                "GrayboxFormalSaveCoordinatorResult3D");
            Type code = RequireRuntimeType(
                "GrayboxFormalSaveCoordinatorCode3D");
            Assert.That(code.IsEnum, Is.True);
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "Success",
                    "Busy",
                    "DecodeFailed",
                    "ValidationFailed",
                    "CaptureFailed",
                    "ApplyFailed",
                    "RollbackFailed",
                },
                Enum.GetNames(code));
            RequireReadOnlyProperty(result, "Success", typeof(bool));
            RequireReadOnlyProperty(result, "Code", code);
            RequireReadOnlyProperty(result, "Message", typeof(string));
            RequireReadOnlyProperty(
                result,
                "Envelope",
                typeof(WasteCity.Persistence.FormalSaveEnvelope));
            Assert.That(coordinator.IsAbstract, Is.False);
            Assert.That(FindPublicMethod(
                coordinator,
                "RestoreEncoded",
                new[] { typeof(string) })?.ReturnType,
                Is.EqualTo(result));
        }

        [Test]
        public void CoordinatorPublishesTheFixedTenDomainOrder()
        {
            Type coordinator = RequireRuntimeType(
                "GrayboxFormalSaveCoordinator3D");
            Type domainId = RequireRuntimeType(
                "GrayboxFormalSaveDomainId3D");
            Assert.That(domainId.IsEnum, Is.True);
            PropertyInfo orderProperty = coordinator.GetProperty(
                "DomainOrder",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(orderProperty, Is.Not.Null);
            Assert.That(orderProperty.CanWrite, Is.False);
            object order = orderProperty.GetValue(null, null);
            Assert.That(order, Is.InstanceOf<IEnumerable>());
            string[] names = ((IEnumerable)order).Cast<object>()
                .Select(value => value.ToString()).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "WorldCity",
                    "BuildingStorage",
                    "Economy",
                    "Production",
                    "Defense",
                    "ResearchEffectState",
                    "Progression",
                    "Evacuation",
                    "CivilizationExpansion",
                    "Pause",
                },
                names);
        }

        [Test]
        public void RuleControllersExposeTransactionPauseAndGuardTick()
        {
            string[] controllers =
            {
                "GrayboxProductionController3D",
                "GrayboxDefenseController3D",
                "GrayboxEvacuationController3D",
            };
            for (var index = 0; index < controllers.Length; index++)
            {
                Type controller = RequireRuntimeType(controllers[index]);
                RequireReadOnlyProperty(
                    controller,
                    "IsPersistencePaused",
                    typeof(bool));
                MethodInfo configure = FindPublicMethod(
                    controller,
                    "ConfigurePersistencePauseSource",
                    new[] { typeof(Func<bool>) });
                Assert.That(configure, Is.Not.Null, controllers[index]);
                Assert.That(configure.ReturnType, Is.EqualTo(typeof(void)));

                string source = File.ReadAllText(ProjectPath(
                    "Assets/_Game/Scripts/Graybox3D/Building/" +
                    controllers[index] + ".cs"));
                string tick = ExtractMethodBlock(
                    source,
                    controllers[index] == "GrayboxEvacuationController3D"
                        ? "public void Tick("
                        : "public bool Tick(");
                StringAssert.Contains("IsPersistencePaused", tick,
                    controllers[index] +
                    ".Tick must combine caller pause with transaction pause.");
            }
        }

        [Test]
        public void CoordinatorExposesInjectableDomainAndDerivedRebuildSeams()
        {
            Type coordinator = RequireRuntimeType(
                "GrayboxFormalSaveCoordinator3D");
            Type domain = RequireRuntimeType("IFormalThreeDSaveDomain");
            Type domainId = RequireRuntimeType(
                "GrayboxFormalSaveDomainId3D");
            Type rebuilder = RequireRuntimeType(
                "IFormalThreeDDerivedStateRebuilder");
            Assert.That(domain.IsInterface, Is.True);
            Assert.That(rebuilder.IsInterface, Is.True);
            RequireReadOnlyProperty(domain, "DomainId", domainId);
            RequireInterfaceMethod(
                domain,
                "TryCapture",
                typeof(bool),
                typeof(FormalThreeDSaveData),
                typeof(string).MakeByRefType());
            RequireInterfaceMethod(
                domain,
                "TryApply",
                typeof(bool),
                typeof(FormalThreeDSaveData),
                typeof(string).MakeByRefType());
            RequireInterfaceMethod(
                rebuilder,
                "RebuildDerivedState",
                typeof(void));
            Assert.That(HasInjectionBoundary(coordinator, domain), Is.True,
                "Tests need an ordered real-domain wrapper seam for apply " +
                "failure and zero-apply verification.");
            Assert.That(HasInjectionBoundary(coordinator, rebuilder), Is.True,
                "The single derived rebuild boundary must be countable.");
            EventInfo completed = coordinator.GetEvent(
                "RestoreCompleted",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.EventHandlerType, Is.EqualTo(typeof(Action)));
        }

        [Test]
        public void EncodedRestoreGatesApplyAndRebuildBehindDecodeValidation()
        {
            string sourcePath = ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs");
            Assert.That(File.Exists(sourcePath), Is.True,
                "Task 10 requires the transactional coordinator source.");
            string source = File.ReadAllText(sourcePath);
            string restore = ExtractMethodBlock(
                source,
                "public GrayboxFormalSaveCoordinatorResult3D " +
                "RestoreEncoded(");
            int decode = restore.IndexOf(
                "FormalSaveCodec.DecodeAny",
                StringComparison.Ordinal);
            int validate = restore.IndexOf(
                "FormalSaveValidator.ValidateDecoded",
                StringComparison.Ordinal);
            int apply = restore.IndexOf(
                "RestoreValidatedEnvelope",
                StringComparison.Ordinal);
            Assert.That(decode, Is.GreaterThanOrEqualTo(0));
            Assert.That(validate, Is.GreaterThan(decode));
            Assert.That(apply, Is.GreaterThan(validate));
            StringAssert.Contains("if (!decoded.Success)", restore);
            StringAssert.Contains("if (!validation.IsValid)", restore);
            StringAssert.DoesNotContain("TryApply", restore,
                "Encoded restore must not enter a domain until both gates pass.");

            string validated = ExtractMethodBlock(
                source,
                "private GrayboxFormalSaveCoordinatorResult3D " +
                "RestoreValidatedEnvelope(");
            Assert.That(Count(validated, "RebuildDerivedState()"),
                Is.EqualTo(2),
                "Target success and successful rollback each rebuild " +
                "derived state through the same boundary.");
            Assert.That(Count(validated, "RestoreCompleted?.Invoke()"),
                Is.EqualTo(1));
        }

        [Test]
        public void ValidatedRestoreCapturesRollbackBeforeApplyAndUsesItOnFailure()
        {
            string sourcePath = ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs");
            Assert.That(File.Exists(sourcePath), Is.True,
                "Task 10 requires the transactional coordinator source.");
            string source = File.ReadAllText(sourcePath);
            string restore = ExtractMethodBlock(
                source,
                "private GrayboxFormalSaveCoordinatorResult3D " +
                "RestoreValidatedEnvelope(");
            int capture = restore.IndexOf(
                "CaptureRollbackEnvelope",
                StringComparison.Ordinal);
            int apply = restore.IndexOf(
                "TryApplyDomains",
                StringComparison.Ordinal);
            int rollback = restore.IndexOf(
                "TryRollbackDomains",
                StringComparison.Ordinal);

            Assert.That(capture, Is.GreaterThanOrEqualTo(0));
            Assert.That(apply, Is.GreaterThan(capture),
                "Rollback truth must freeze before the first domain apply.");
            Assert.That(rollback, Is.GreaterThan(apply),
                "A later apply failure must replay the frozen rollback truth.");
            Assert.That(Count(restore, "TryRollbackDomains"), Is.EqualTo(1));
        }

        [TestCase("{ definitely-not-json", GrayboxFormalSaveCoordinatorCode3D.DecodeFailed)]
        [TestCase("schema-31-invalid-cross-reference.json", GrayboxFormalSaveCoordinatorCode3D.ValidationFailed)]
        [TestCase("schema-30-legacy-2d.json", GrayboxFormalSaveCoordinatorCode3D.DecodeFailed)]
        public void InvalidEncodedRestoreDoesNotTouchAnyRuntimeDomain(
            string document,
            GrayboxFormalSaveCoordinatorCode3D expectedCode)
        {
            CoordinatorHarness harness = CoordinatorHarness.Create();
            string encoded = document.EndsWith(
                ".json",
                StringComparison.Ordinal)
                    ? ReadFixture(document)
                    : document;

            GrayboxFormalSaveCoordinatorResult3D result =
                harness.Coordinator.RestoreEncoded(encoded);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo(expectedCode));
            Assert.That(harness.TotalCaptureCount, Is.Zero);
            Assert.That(harness.TotalApplyCount, Is.Zero);
            Assert.That(harness.Rebuilder.InvocationCount, Is.Zero);
            Assert.That(harness.CompletionCount, Is.Zero);
            Assert.That(harness.Coordinator.IsTransactionPaused, Is.False);
        }

        [Test]
        public void ValidRestoreCapturesRollbackThenAppliesInFixedOrderUnderBarrier()
        {
            FormalSaveEnvelope target = LoadFixtureEnvelope();
            CoordinatorHarness harness = CoordinatorHarness.Create(
                MutatedPreloadPayload(target.formal3D));
            string targetCanonical = CanonicalPayload(target.formal3D);

            GrayboxFormalSaveCoordinatorResult3D result =
                harness.Coordinator.RestoreEncoded(
                    ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Code,
                Is.EqualTo(GrayboxFormalSaveCoordinatorCode3D.Success));
            CollectionAssert.AreEqual(
                ExpectedCalls("capture", "apply")
                    .Concat(new[] { "rebuild", "event" })
                    .ToArray(),
                harness.Calls,
                "All rollback captures must finish before target apply.");
            Assert.That(harness.PauseObservations,
                Has.All.True,
                "Domains, derived rebuild, and completion event must all " +
                "run behind the persistence barrier.");
            Assert.That(CanonicalPayload(harness.Authority.State),
                Is.EqualTo(targetCanonical));
            Assert.That(harness.Rebuilder.InvocationCount, Is.EqualTo(1));
            Assert.That(harness.CompletionCount, Is.EqualTo(1));
            Assert.That(harness.Coordinator.IsTransactionPaused, Is.False);
        }

        [TestCase(GrayboxFormalSaveDomainId3D.WorldCity)]
        [TestCase(GrayboxFormalSaveDomainId3D.BuildingStorage)]
        [TestCase(GrayboxFormalSaveDomainId3D.Economy)]
        [TestCase(GrayboxFormalSaveDomainId3D.Production)]
        [TestCase(GrayboxFormalSaveDomainId3D.Progression)]
        [TestCase(GrayboxFormalSaveDomainId3D.Defense)]
        [TestCase(GrayboxFormalSaveDomainId3D.ResearchEffectState)]
        [TestCase(GrayboxFormalSaveDomainId3D.Evacuation)]
        [TestCase(GrayboxFormalSaveDomainId3D.CivilizationExpansion)]
        public void ApplyFailureReplaysRollbackAndRestoresCanonicalAuthority(
            GrayboxFormalSaveDomainId3D failedDomain)
        {
            FormalSaveEnvelope target = LoadFixtureEnvelope();
            FormalThreeDSaveData preload =
                MutatedPreloadPayload(target.formal3D);
            string preloadCanonical = CanonicalPayload(preload);
            CoordinatorHarness harness = CoordinatorHarness.Create(
                preload,
                failedDomain,
                DomainFault.FailFirstApply);

            GrayboxFormalSaveCoordinatorResult3D result =
                harness.Coordinator.RestoreEncoded(
                    ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code,
                Is.EqualTo(GrayboxFormalSaveCoordinatorCode3D.ApplyFailed));
            Assert.That(result.FailedDomain, Is.EqualTo(failedDomain));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(CanonicalPayload(harness.Authority.State),
                Is.EqualTo(preloadCanonical),
                "Rollback must restore the complete pre-load authority.");
            Assert.That(harness.TotalCaptureCount, Is.EqualTo(10));
            Assert.That(harness.Rebuilder.InvocationCount, Is.EqualTo(1),
                "Successful rollback must rebuild derived state once.");
            Assert.That(harness.CompletionCount, Is.Zero);
            Assert.That(harness.PauseObservations, Has.All.True);
            Assert.That(harness.Coordinator.IsTransactionPaused, Is.False);
        }

        [Test]
        public void LaterDomainFailureRollsBackResearchEmitterArrayExactly()
        {
            FormalSaveEnvelope target = LoadFixtureEnvelope();
            FormalThreeDSaveData preload =
                MutatedPreloadPayload(target.formal3D);
            preload.researchEffectState.revision = 3UL;
            preload.researchEffectState.nextStableStateOrdinal = 8L;
            preload.researchEffectState.emitters = new[]
            {
                new FormalThreeDResearchEffectEmitterSaveData
                {
                    stableStateId = "research.state.000007",
                    creationOrdinal = 7L,
                    effectId = "cultivation.status.sword-intent",
                    sourceTowerStableId = "building.instance.000001",
                    targetEnemyStableId =
                        "campaign.enemy.wave-01.0000",
                    cooldownRemaining = .375f,
                },
            };
            CoordinatorHarness harness = CoordinatorHarness.Create(
                preload,
                GrayboxFormalSaveDomainId3D.Progression,
                DomainFault.FailFirstApply);

            GrayboxFormalSaveCoordinatorResult3D result =
                harness.Coordinator.RestoreEncoded(
                    ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.RollbackSucceeded, Is.True);
            FormalThreeDResearchEffectEmitterSaveData restored =
                harness.Authority.State.researchEffectState.emitters.Single();
            Assert.That(restored.stableStateId,
                Is.EqualTo("research.state.000007"));
            Assert.That(restored.sourceTowerStableId,
                Is.EqualTo("building.instance.000001"));
            Assert.That(restored.targetEnemyStableId,
                Is.EqualTo("campaign.enemy.wave-01.0000"));
            Assert.That(restored.cooldownRemaining,
                Is.EqualTo(.375f).Within(.0001f));
        }

        [Test]
        public void RollbackFailureRequiresSafeReturnAndKeepsBarrierRaised()
        {
            CoordinatorHarness harness = CoordinatorHarness.Create(
                MutatedPreloadPayload(LoadFixtureEnvelope().formal3D),
                GrayboxFormalSaveDomainId3D.WorldCity,
                DomainFault.FailFirstApplyAndRollback);

            GrayboxFormalSaveCoordinatorResult3D result =
                harness.Coordinator.RestoreEncoded(
                    ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code,
                Is.EqualTo(GrayboxFormalSaveCoordinatorCode3D.RollbackFailed));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.False);
            Assert.That(result.RequiresSafeReturnToTitle, Is.True);
            Assert.That(harness.Rebuilder.InvocationCount, Is.Zero);
            Assert.That(harness.CompletionCount, Is.Zero);
            Assert.That(harness.PauseObservations, Has.All.True);
            Assert.That(harness.Coordinator.IsTransactionPaused, Is.True,
                "Unsafe partial authority must remain frozen until title " +
                "recovery takes ownership.");
        }

        [Test]
        public void DomainExceptionStillAttemptsRollbackAndRestoresAuthority()
        {
            FormalThreeDSaveData preload = MutatedPreloadPayload(
                LoadFixtureEnvelope().formal3D);
            string preloadCanonical = CanonicalPayload(preload);
            CoordinatorHarness harness = CoordinatorHarness.Create(
                preload,
                GrayboxFormalSaveDomainId3D.Production,
                DomainFault.ThrowFirstApply);

            GrayboxFormalSaveCoordinatorResult3D result =
                harness.Coordinator.RestoreEncoded(
                    ReadFixture("schema-31-formal-3d.json"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code,
                Is.EqualTo(GrayboxFormalSaveCoordinatorCode3D.ApplyFailed));
            Assert.That(result.FailedDomain,
                Is.EqualTo(GrayboxFormalSaveDomainId3D.Production));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(CanonicalPayload(harness.Authority.State),
                Is.EqualTo(preloadCanonical));
            Assert.That(harness.Rebuilder.InvocationCount, Is.EqualTo(1),
                "Exception rollback must rebuild derived state once.");
            Assert.That(harness.CompletionCount, Is.Zero);
            Assert.That(harness.PauseObservations, Has.All.True);
            Assert.That(harness.Coordinator.IsTransactionPaused, Is.False);
        }

        [Test]
        public void DerivedRebuildExceptionRollsBackAndReturnsStructuredFailure()
        {
            FormalThreeDSaveData preload = MutatedPreloadPayload(
                LoadFixtureEnvelope().formal3D);
            string preloadCanonical = CanonicalPayload(preload);
            CoordinatorHarness harness = CoordinatorHarness.Create(
                preload,
                throwOnFirstRebuild: true);

            GrayboxFormalSaveCoordinatorResult3D result = null;
            Assert.DoesNotThrow(() => result =
                harness.Coordinator.RestoreEncoded(
                    ReadFixture("schema-31-formal-3d.json")));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Code,
                Is.EqualTo(GrayboxFormalSaveCoordinatorCode3D.ApplyFailed));
            Assert.That(result.Message,
                Does.Contain(CountingRebuilder.InjectedFailure));
            Assert.That(result.Envelope, Is.Not.Null,
                "Structured failure must retain the captured rollback truth.");
            Assert.That(result.FailedDomain, Is.Null);
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(CanonicalPayload(harness.Authority.State),
                Is.EqualTo(preloadCanonical));
            Assert.That(harness.Rebuilder.InvocationCount, Is.EqualTo(2),
                "Target rebuild throws, then rollback state rebuilds once.");
            Assert.That(harness.CompletionCount, Is.Zero);
            Assert.That(harness.PauseObservations, Has.All.True);
            Assert.That(harness.Coordinator.IsTransactionPaused, Is.False);
        }

        [Test]
        public void CompletionObserverExceptionCannotRewriteCommittedSuccess()
        {
            FormalSaveEnvelope target = LoadFixtureEnvelope();
            CoordinatorHarness harness = CoordinatorHarness.Create(
                MutatedPreloadPayload(target.formal3D));
            var throwingObserverCount = 0;
            var observerSawBarrier = false;
            harness.Coordinator.RestoreCompleted += () =>
            {
                throwingObserverCount++;
                observerSawBarrier =
                    harness.Coordinator.IsTransactionPaused;
                throw new InvalidOperationException(
                    "injected completion observer failure");
            };

            GrayboxFormalSaveCoordinatorResult3D result = null;
            Assert.DoesNotThrow(() => result =
                harness.Coordinator.RestoreEncoded(
                    ReadFixture("schema-31-formal-3d.json")));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True,
                "An observer cannot turn an already committed restore into " +
                "an apply failure.");
            Assert.That(result.Code,
                Is.EqualTo(GrayboxFormalSaveCoordinatorCode3D.Success));
            Assert.That(CanonicalPayload(harness.Authority.State),
                Is.EqualTo(CanonicalPayload(target.formal3D)));
            Assert.That(harness.Rebuilder.InvocationCount, Is.EqualTo(1));
            Assert.That(harness.CompletionCount, Is.EqualTo(1));
            Assert.That(throwingObserverCount, Is.EqualTo(1));
            Assert.That(observerSawBarrier, Is.True);
            Assert.That(harness.Coordinator.IsTransactionPaused, Is.False);
        }

        [Test]
        public void DefenseBarrierHardStopsBeforeRuntimeSynchronization()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxDefenseController3D.cs"));
            string tick = ExtractMethodBlock(source, "public bool Tick(");
            Match guard = Regex.Match(
                tick,
                @"if\s*\(\s*effectivePaused\s*\)\s*" +
                @"(?:\{\s*)?return\s+(?:true|false)\s*;");
            Assert.That(guard.Success, Is.True,
                "Persistence pause must hard-return before any defense " +
                "authority synchronization.");
            int synchronize = tick.IndexOf(
                "TrySynchronizeRuntime",
                StringComparison.Ordinal);
            Assert.That(synchronize, Is.GreaterThan(guard.Index));
        }

        [Test]
        public void EvacuationPauseBindingSurvivesConfigureAndDisableLifecycle()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxEvacuationController3D.cs"));
            string configure = ExtractMethodBlock(
                source,
                "public void ConfigurePersistencePauseSource(");
            string configureCore = ExtractMethodBlock(
                source,
                "private void ConfigureCore(");
            string cleanup = ExtractMethodBlock(
                source,
                "private void CleanupController(");
            string disable = ExtractMethodBlock(
                source,
                "private void OnDisable(");
            string destroy = ExtractMethodBlock(
                source,
                "private void OnDestroy(");

            StringAssert.Contains("persistencePauseSource = pauseSource",
                configure);
            StringAssert.DoesNotContain("ArgumentNullException", configure,
                "Explicit null is the supported production unbind.");
            StringAssert.DoesNotContain(
                "persistencePauseSource",
                configureCore);
            StringAssert.DoesNotContain(
                "persistencePauseSource",
                cleanup,
                "Ordinary Configure and OnDisable both use cleanup.");
            StringAssert.DoesNotContain(
                "ConfigurePersistencePauseSource(null)",
                disable);
            Assert.That(
                destroy.Contains("ConfigurePersistencePauseSource(null)") ||
                destroy.Contains("persistencePauseSource = null"),
                Is.True,
                "Only destruction or an explicit caller unbind may release " +
                "the coordinator barrier delegate.");
        }

        [Test]
        public void EvacuationRestoreReplacesActiveStateOnlyBehindBarrier()
        {
            Type controller = typeof(GrayboxEvacuationController3D);
            Assert.That(controller.GetMethod(
                    "TryPrepareRestore",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(controller.GetMethod(
                    "TryCommitRestore",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxEvacuationController3D.cs"));
            string prepare = ExtractMethodBlock(
                source,
                "public bool TryPrepareRestore(");
            string commit = ExtractMethodBlock(
                source,
                "public bool TryCommitRestore(");

            Assert.That(HasPersistenceAwareActivityGuard(prepare), Is.True,
                "Ordinary callers must not overwrite active evacuation, " +
                "while the coordinator barrier may prepare replacement " +
                "after BuildingStorage restored the session.");
            Assert.That(HasPersistenceAwareActivityGuard(commit), Is.True,
                "Commit must apply the same active-state/barrier rule.");
            StringAssert.DoesNotContain("CleanupController", prepare);
            StringAssert.DoesNotContain("CleanupController", commit);
            StringAssert.DoesNotContain("RollbackCleanupWork", prepare,
                "Persistence replacement must not execute gameplay refunds.");
            StringAssert.DoesNotContain("RollbackCleanupWork", commit,
                "Persistence replacement must not execute gameplay refunds.");
        }

        [Test]
        public void CoordinatorExposesCompleteProductionAssemblyFactory()
        {
            Type coordinator = typeof(GrayboxFormalSaveCoordinator3D);
            MethodInfo factory = coordinator.GetMethod(
                "CreateProduction",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(factory, Is.Not.Null,
                "Production needs one public assembly boundary for the seven " +
                "real adapters, Pause domain, live providers, and barrier " +
                "consumers.");
            Assert.That(factory.ReturnType, Is.EqualTo(coordinator));

            ParameterInfo[] parameters = factory.GetParameters();
            Type[] requiredTypes =
            {
                typeof(GrayboxWorldCitySaveAdapter3D),
                typeof(GrayboxBuildingStorageSaveAdapter3D),
                typeof(GrayboxEconomySaveAdapter3D),
                typeof(GrayboxProductionSaveAdapter3D),
                typeof(GrayboxFormalProgressionSaveAdapter3D),
                typeof(GrayboxDefenseSaveAdapter3D),
                typeof(GrayboxEvacuationSaveAdapter3D),
                typeof(IFormalThreeDSaveDomain),
                typeof(Func<IReadOnlyList<GrayboxBuildingInstance3D>>),
                typeof(Func<WorldMapModel>),
                typeof(IFormalThreeDDerivedStateRebuilder),
                typeof(GrayboxProductionController3D),
                typeof(GrayboxDefenseController3D),
                typeof(GrayboxEvacuationController3D),
            };
            for (var index = 0; index < requiredTypes.Length; index++)
            {
                Assert.That(parameters.Count(parameter =>
                        parameter.ParameterType == requiredTypes[index]),
                    Is.EqualTo(1),
                    "CreateProduction requires exactly one " +
                    requiredTypes[index].Name + ".");
            }

            ParameterInfo overStack = parameters.SingleOrDefault(parameter =>
                parameter.ParameterType == typeof(bool) &&
                parameter.Name == "allowBackpackOverStack");
            Assert.That(overStack, Is.Not.Null,
                "Backpack over-stack migration policy must be explicit at " +
                "the production composition root.");
        }

        [Test]
        public void ProductionAssemblyUsesLiveProvidersAndOneSharedBarrier()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs"));
            string factory = ExtractMethodBlock(
                source,
                "public static GrayboxFormalSaveCoordinator3D " +
                "CreateProduction(");

            StringAssert.Contains("allowBackpackOverStack", factory);
            Assert.That(ContainsProviderInvocation(source, "instancesProvider"),
                Is.True,
                "Production and defense restore must resolve current " +
                "building instances when apply runs.");
            Assert.That(ContainsProviderInvocation(source, "worldProvider"),
                Is.True,
                "Production restore must resolve the current restored world " +
                "when apply runs.");

            string[] controllerParameters =
            {
                "productionController",
                "defenseController",
                "evacuationController",
            };
            var pauseArguments = new List<string>();
            for (var index = 0;
                 index < controllerParameters.Length;
                 index++)
            {
                Match binding = Regex.Match(
                    factory,
                    Regex.Escape(controllerParameters[index]) +
                    @"\.ConfigurePersistencePauseSource\(\s*" +
                    @"(?<argument>[A-Za-z_]\w*)\s*\)");
                Assert.That(binding.Success, Is.True,
                    controllerParameters[index] +
                    " must be bound by CreateProduction.");
                pauseArguments.Add(binding.Groups["argument"].Value);
            }
            Assert.That(pauseArguments.Distinct().Count(), Is.EqualTo(1),
                "All three rule controllers must share one pause delegate.");
            StringAssert.Contains("IsTransactionPaused", factory,
                "The shared delegate must read the returned coordinator's " +
                "transaction barrier.");
        }

        [Test]
        public void ProductionCaptureWithoutEffectAdapterCreatesCanonicalState()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs"));
            string factory = ExtractMethodBlock(
                source,
                "public static GrayboxFormalSaveCoordinator3D " +
                "CreateProduction(");

            StringAssert.Contains(
                "? new FormalThreeDResearchEffectStateSaveData()",
                factory);
            StringAssert.DoesNotContain(
                "destination.researchEffectState ??",
                factory,
                "A stale preloaded default must not preserve a null " +
                "schema-35 configuration signature.");
            Assert.That(
                new FormalThreeDResearchEffectStateSaveData()
                    .configurationSignature,
                Is.EqualTo(FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature));
        }

        [TestCase("productionController", "production")]
        [TestCase("defenseController", "defense")]
        public void ProductionAssemblyRebuildsRuntimeShapeBeforeAdapterRestore(
            string controllerName,
            string adapterName)
        {
            Type error = typeof(string).MakeByRefType();
            Type controllerType = controllerName == "productionController"
                ? typeof(GrayboxProductionController3D)
                : typeof(GrayboxDefenseController3D);
            MethodInfo rebuildMethod = FindPublicMethod(
                controllerType,
                "TryRebuildAfterPersistenceRestore",
                new[] { error });
            Assert.That(rebuildMethod, Is.Not.Null);
            Assert.That(rebuildMethod.ReturnType, Is.EqualTo(typeof(bool)));

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs"));
            string factory = ExtractMethodBlock(
                source,
                "public static GrayboxFormalSaveCoordinator3D " +
                "CreateProduction(");

            AssertRebuildBeforeAdapterRestore(
                factory,
                controllerName,
                adapterName);
        }

        [Test]
        public void ConcreteRebuilderUsesThreeNoAdvanceControllerBoundaries()
        {
            Type rebuilder = RequireRuntimeType(
                "GrayboxFormalControllerRebuilder3D");
            Assert.That(
                typeof(IFormalThreeDDerivedStateRebuilder)
                    .IsAssignableFrom(rebuilder),
                Is.True);
            Type[] controllers =
            {
                typeof(GrayboxProductionController3D),
                typeof(GrayboxDefenseController3D),
                typeof(GrayboxEvacuationController3D),
            };
            ParameterInfo[] parameters = rebuilder.GetConstructors()
                .Single().GetParameters();
            for (var index = 0; index < controllers.Length; index++)
            {
                Assert.That(parameters.Count(parameter =>
                        parameter.ParameterType == controllers[index]),
                    Is.EqualTo(1));
                MethodInfo rebuild = controllers[index].GetMethod(
                    "TryRebuildAfterPersistenceRestore",
                    new[] { typeof(string).MakeByRefType() });
                Assert.That(rebuild, Is.Not.Null, controllers[index].Name);
                Assert.That(rebuild.ReturnType, Is.EqualTo(typeof(bool)));
            }

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs"));
            string rebuildSource = ExtractMethodBlock(
                source,
                "public void RebuildDerivedState(");
            Assert.That(Count(
                    rebuildSource,
                    ".TryRebuildAfterPersistenceRestore("),
                Is.EqualTo(3));
        }

        [Test]
        public void ProductionPauseDomainRoundTripsOnlyTheUserPauseReason()
        {
            Type pauseType = RequireRuntimeType(
                "GrayboxFormalPauseSaveDomain3D");
            var speed = new GameSpeedModel();
            speed.SetPaused(GamePauseReason.SystemMenu, true);
            var domain = (IFormalThreeDSaveDomain)Activator.CreateInstance(
                pauseType,
                speed);
            var payload = new FormalThreeDSaveData();

            Assert.That(domain.DomainId,
                Is.EqualTo(GrayboxFormalSaveDomainId3D.Pause));
            Assert.That(domain.TryCapture(payload, out string captureError),
                Is.True,
                captureError);
            Assert.That(payload.pause, Is.Not.Null);
            Assert.That(payload.pause.tacticalPaused, Is.False);

            speed.SetPaused(GamePauseReason.User, true);
            Assert.That(domain.TryApply(payload, out string applyError),
                Is.True,
                applyError);
            Assert.That(speed.IsPaused(GamePauseReason.User), Is.False);
            Assert.That(speed.IsPaused(GamePauseReason.SystemMenu), Is.True,
                "Persistence must not overwrite unrelated pause ownership.");
        }

        [Test]
        public void Schema31CoordinatorRoundTripsRouteBuildingNonDefaultRecipe()
        {
            const string stableId = "building.instance.000001";
            const string recipeId =
                "biological.production.active-biomass";
            FormalSaveEnvelope fixture = LoadFixtureEnvelope();
            FormalThreeDSaveData payload = ClonePayload(fixture.formal3D);
            Assert.That(payload.researchEffectState.configurationSignature,
                Is.EqualTo(FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature));
            FormalThreeDBuildingInstanceSaveData savedBuilding =
                payload.buildings.instances.Single(instance =>
                    instance.stableInstanceId == stableId);
            savedBuilding.definitionId = BuildingCatalog.ColonyPool.Id.Value;
            savedBuilding.boundResourceNodeId = string.Empty;
            savedBuilding.boundNodeX = -1;
            savedBuilding.boundNodeY = -1;

            GrayboxBuildingInstance3D sourceInstance = CompleteInstance(
                stableId,
                BuildingCatalog.ColonyPool,
                savedBuilding.x,
                savedBuilding.y);
            GrayboxProductionRuntime3D sourceRuntime = ProductionRuntime(
                sourceInstance,
                payload.city);
            Assert.That(sourceRuntime.TrySelectRecipe(
                    stableId,
                    recipeId,
                    new[] { "core.research.adaptive-tissue" },
                    out GrayboxProductionRecipeSelectionResult3D selection),
                Is.True,
                selection.Status.ToString());
            Assert.That(sourceRuntime.TryGetState(
                stableId,
                out BuildingProductionState sourceState), Is.True);
            Assert.That(sourceState.Input.Add(
                ResourceIds.BiomassConcentrate, 5), Is.EqualTo(5));
            Assert.That(sourceState.Input.Add(
                ResourceIds.EnergyCrystal, 3), Is.EqualTo(3));
            Assert.That(sourceState.Output.Add(
                ResourceIds.ActiveBiomass, 2), Is.EqualTo(2));
            sourceState.SetPlayerPaused(true);

            GrayboxFormalSaveCoordinator3D sourceCoordinator =
                CoordinatorWithProduction(
                    payload,
                    sourceRuntime,
                    new[] { sourceInstance });
            GrayboxFormalSaveCoordinatorResult3D captured =
                sourceCoordinator.CaptureEnvelope(
                    payload.sessionId,
                    fixture.gameVersion,
                    fixture.contentSources,
                    fixture.checkpoint,
                    new DateTime(2026, 8, 23, 0, 0, 0,
                        DateTimeKind.Utc));
            Assert.That(captured.Success, Is.True, captured.Message);
            Assert.That(
                captured.Envelope.saveSchemaVersion,
                Is.EqualTo(FormalSaveEnvelope.CurrentSchemaVersion));
            Assert.That(captured.Envelope.formal3D.researchEffectState
                    .configurationSignature,
                Is.EqualTo(FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature));

            GrayboxBuildingInstance3D targetInstance = CompleteInstance(
                stableId,
                BuildingCatalog.ColonyPool,
                savedBuilding.x,
                savedBuilding.y);
            GrayboxProductionRuntime3D targetRuntime = ProductionRuntime(
                targetInstance,
                payload.city);
            GrayboxFormalSaveCoordinator3D targetCoordinator =
                CoordinatorWithProduction(
                    ClonePayload(payload),
                    targetRuntime,
                    new[] { targetInstance });

            string encoded = FormalSaveCodec.EncodeEnvelope(
                captured.Envelope);
            StringAssert.Contains(
                "\"researchEffectState\":{\"configurationSignature\":" +
                "\"" + FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature + "\"",
                encoded);
            FormalSaveDecodeResult roundTrip =
                FormalSaveCodec.DecodeEnvelope(encoded);
            Assert.That(roundTrip.Success, Is.True, roundTrip.Message);
            Assert.That(roundTrip.Envelope.formal3D.researchEffectState
                    .configurationSignature,
                Is.EqualTo(FormalThreeDResearchEffectStateSaveData
                    .ConfigurationSignature));
            FormalSaveValidationResult roundTripValidation =
                FormalSaveValidator.ValidateDecoded(roundTrip);
            Assert.That(roundTripValidation.IsValid, Is.True,
                roundTripValidation.Message);
            GrayboxFormalSaveCoordinatorResult3D restored =
                targetCoordinator.RestoreEncoded(encoded);

            Assert.That(restored.Success, Is.True, restored.Message);
            Assert.That(targetRuntime.TryGetState(
                stableId,
                out BuildingProductionState targetState), Is.True);
            Assert.That(targetState.Definition.Id, Is.EqualTo(recipeId));
            Assert.That(targetState.Input.Get(ResourceIds.BiomassConcentrate),
                Is.EqualTo(5));
            Assert.That(targetState.Input.Get(ResourceIds.EnergyCrystal),
                Is.EqualTo(3));
            Assert.That(targetState.Output.Get(ResourceIds.ActiveBiomass),
                Is.EqualTo(2));
            Assert.That(targetState.IsPlayerPaused, Is.True);
        }

        private static Type RequireRuntimeType(string shortName)
        {
            string fullName = RuntimeNamespace + shortName;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            Assert.Fail("Missing Task 10 runtime type: " + fullName);
            return null;
        }

        private static PropertyInfo RequireReadOnlyProperty(
            Type owner,
            string name,
            Type propertyType)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(property.PropertyType, Is.EqualTo(propertyType));
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.CanWrite, Is.False);
            return property;
        }

        private static MethodInfo FindPublicMethod(
            Type owner,
            string name,
            Type[] parameterTypes)
        {
            return owner.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
        }

        private static void RequireInterfaceMethod(
            Type owner,
            string name,
            Type returnType,
            params Type[] parameters)
        {
            MethodInfo method = FindPublicMethod(owner, name, parameters);
            Assert.That(method, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
        }

        private static bool HasInjectionBoundary(Type owner, Type seam)
        {
            IEnumerable<ParameterInfo> parameters = owner
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(value => value.GetParameters())
                .Concat(owner.GetMethods(
                        BindingFlags.Instance | BindingFlags.Public)
                    .SelectMany(value => value.GetParameters()));
            return parameters.Any(parameter =>
                parameter.ParameterType == seam ||
                parameter.ParameterType.GetGenericArguments()
                    .Contains(seam));
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        private static string ExtractMethodBlock(
            string source,
            string declaration)
        {
            int start = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), declaration);
            int opening = source.IndexOf('{', start);
            Assert.That(opening, Is.GreaterThanOrEqualTo(0));
            var depth = 0;
            for (var index = opening; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}') depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            throw new AssertionException("Unbalanced method: " + declaration);
        }

        private static int Count(string source, string value)
        {
            var count = 0;
            var offset = 0;
            while ((offset = source.IndexOf(
                       value,
                       offset,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        private static bool ContainsProviderInvocation(
            string source,
            string providerName)
        {
            return Regex.IsMatch(
                source,
                @"\b" + Regex.Escape(providerName) +
                @"\s*(?:\(\s*\)|\.Invoke\(\s*\))");
        }

        private static bool HasPersistenceAwareActivityGuard(string source)
        {
            string compact = Regex.Replace(source, @"\s+", string.Empty);
            return compact.Contains(
                       "(IsProcessing||IsManifestOpen)&&" +
                       "!IsPersistencePaused") ||
                   compact.Contains(
                       "!IsPersistencePaused&&" +
                       "(IsProcessing||IsManifestOpen)");
        }

        private static void AssertRebuildBeforeAdapterRestore(
            string factory,
            string controllerName,
            string adapterName)
        {
            Match rebuild = Regex.Match(
                factory,
                @"if\s*\(\s*!\s*" + Regex.Escape(controllerName) +
                @"\s*\.TryRebuildAfterPersistenceRestore\(\s*out\s+error\s*\)");
            Assert.That(rebuild.Success, Is.True,
                controllerName + " must fail the domain apply when runtime " +
                "shape cannot be synchronized to restored buildings.");
            int restore = factory.IndexOf(
                adapterName + ".TryRestore(",
                StringComparison.Ordinal);
            Assert.That(restore, Is.GreaterThan(rebuild.Index),
                controllerName + " must synchronize the BuildingStorage " +
                "instance shape before " + adapterName +
                " validates and restores its payload.");
        }

        private static FormalSaveEnvelope LoadFixtureEnvelope()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            Assert.That(validation.IsValid, Is.True, validation.Message);
            return decoded.Envelope;
        }

        private static string ReadFixture(string fileName)
        {
            string path = ProjectPath(
                "Assets/_Game/Tests/Fixtures/Persistence/" + fileName);
            Assert.That(File.Exists(path), Is.True, "Missing fixture: " + path);
            return File.ReadAllText(path);
        }

        private static FormalThreeDSaveData MutatedPreloadPayload(
            FormalThreeDSaveData target)
        {
            FormalThreeDSaveData preload = ClonePayload(target);
            preload.world.worldSeed += 17;
            preload.city.positionX += 2.5f;
            preload.buildings.nextStableInstanceOrdinal += 10;
            preload.storage.configurationSignature += ".preload";
            preload.backpack.slots[0].amount += 3;
            preload.crafting.activeProgressSeconds += 0.5f;
            preload.research.remainingSeconds += 1f;
            preload.production.states[0].progressSeconds += 0.75f;
            preload.defense.coreCurrentHealth -= 7;
            preload.evacuation.remainingSeconds += 0.25f;
            preload.pause.tacticalPaused = !preload.pause.tacticalPaused;
            return preload;
        }

        private static FormalThreeDSaveData ClonePayload(
            FormalThreeDSaveData source)
        {
            return JsonUtility.FromJson<FormalThreeDSaveData>(
                JsonUtility.ToJson(source, false));
        }

        private static string CanonicalPayload(FormalThreeDSaveData payload)
        {
            return JsonUtility.ToJson(payload, false);
        }

        private static string[] ExpectedCalls(params string[] phases)
        {
            return phases.SelectMany(phase =>
                    GrayboxFormalSaveCoordinator3D.DomainOrder.Select(
                        domain => phase + ":" + domain))
                .ToArray();
        }

        private enum DomainFault
        {
            None,
            FailFirstApply,
            FailFirstApplyAndRollback,
            ThrowFirstApply,
        }

        private sealed class MutableAuthority
        {
            public MutableAuthority(FormalThreeDSaveData state)
            {
                State = ClonePayload(state);
            }

            public FormalThreeDSaveData State { get; }
        }

        private sealed class FakeDomain : IFormalThreeDSaveDomain
        {
            private readonly MutableAuthority authority;
            private readonly List<string> calls;
            private readonly List<bool> pauseObservations;
            private readonly DomainFault fault;
            private int applyCount;

            public FakeDomain(
                GrayboxFormalSaveDomainId3D domainId,
                MutableAuthority authority,
                List<string> calls,
                List<bool> pauseObservations,
                DomainFault fault)
            {
                DomainId = domainId;
                this.authority = authority;
                this.calls = calls;
                this.pauseObservations = pauseObservations;
                this.fault = fault;
            }

            public GrayboxFormalSaveDomainId3D DomainId { get; }
            public Func<bool> PauseSource { private get; set; }
            public int CaptureCount { get; private set; }
            public int ApplyCount => applyCount;

            public bool TryCapture(
                FormalThreeDSaveData destination,
                out string error)
            {
                CaptureCount++;
                Observe("capture");
                CopyOwned(authority.State, destination, DomainId);
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                FormalThreeDSaveData source,
                out string error)
            {
                applyCount++;
                Observe("apply");
                CopyOwned(source, authority.State, DomainId);
                error = "injected " + DomainId + " apply failure";
                if (fault == DomainFault.ThrowFirstApply && applyCount == 1)
                    throw new InvalidOperationException(error);
                if (fault == DomainFault.FailFirstApply && applyCount == 1)
                    return false;
                if (fault == DomainFault.FailFirstApplyAndRollback &&
                    applyCount <= 2)
                    return false;
                error = string.Empty;
                return true;
            }

            private void Observe(string phase)
            {
                calls.Add(phase + ":" + DomainId);
                pauseObservations.Add(PauseSource != null && PauseSource());
            }

            private static void CopyOwned(
                FormalThreeDSaveData source,
                FormalThreeDSaveData destination,
                GrayboxFormalSaveDomainId3D domainId)
            {
                FormalThreeDSaveData copy = ClonePayload(source);
                switch (domainId)
                {
                    case GrayboxFormalSaveDomainId3D.WorldCity:
                        destination.world = copy.world;
                        destination.city = copy.city;
                        return;
                    case GrayboxFormalSaveDomainId3D.BuildingStorage:
                        destination.buildings = copy.buildings;
                        destination.storage = copy.storage;
                        return;
                    case GrayboxFormalSaveDomainId3D.Economy:
                        destination.backpack = copy.backpack;
                        destination.crafting = copy.crafting;
                        destination.research = copy.research;
                        return;
                    case GrayboxFormalSaveDomainId3D.Production:
                        destination.production = copy.production;
                        return;
                    case GrayboxFormalSaveDomainId3D.Progression:
                        destination.progression = copy.progression;
                        return;
                    case GrayboxFormalSaveDomainId3D.Defense:
                        destination.defense = copy.defense;
                        return;
                    case GrayboxFormalSaveDomainId3D.ResearchEffectState:
                        destination.researchEffectState =
                            copy.researchEffectState;
                        return;
                    case GrayboxFormalSaveDomainId3D.Evacuation:
                        destination.evacuation = copy.evacuation;
                        return;
                    case GrayboxFormalSaveDomainId3D.CivilizationExpansion:
                        destination.civilizationExpansion =
                            copy.civilizationExpansion;
                        return;
                    case GrayboxFormalSaveDomainId3D.Pause:
                        destination.pause = copy.pause;
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(domainId), domainId, null);
                }
            }
        }

        private sealed class CountingRebuilder :
            IFormalThreeDDerivedStateRebuilder
        {
            public const string InjectedFailure =
                "injected derived rebuild failure";

            private readonly List<string> calls;
            private readonly List<bool> pauseObservations;

            public CountingRebuilder(
                List<string> calls,
                List<bool> pauseObservations)
            {
                this.calls = calls;
                this.pauseObservations = pauseObservations;
            }

            public Func<bool> PauseSource { private get; set; }
            public bool ThrowOnFirstInvocation { get; set; }
            public int InvocationCount { get; private set; }

            public void RebuildDerivedState()
            {
                InvocationCount++;
                calls.Add("rebuild");
                pauseObservations.Add(PauseSource != null && PauseSource());
                if (ThrowOnFirstInvocation && InvocationCount == 1)
                    throw new InvalidOperationException(InjectedFailure);
            }
        }

        private sealed class ProductionDomain : IFormalThreeDSaveDomain
        {
            private readonly GrayboxProductionSaveAdapter3D adapter;
            private readonly IReadOnlyList<GrayboxBuildingInstance3D>
                instances;

            public ProductionDomain(
                GrayboxProductionRuntime3D runtime,
                IReadOnlyList<GrayboxBuildingInstance3D> instances)
            {
                adapter = new GrayboxProductionSaveAdapter3D(runtime);
                this.instances = instances;
            }

            public GrayboxFormalSaveDomainId3D DomainId =>
                GrayboxFormalSaveDomainId3D.Production;

            public bool TryCapture(
                FormalThreeDSaveData destination,
                out string error)
            {
                destination.production = adapter.Capture();
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                FormalThreeDSaveData source,
                out string error)
            {
                return adapter.TryRestore(
                    source.production,
                    instances,
                    world: null,
                    out error);
            }
        }

        private static GrayboxFormalSaveCoordinator3D
            CoordinatorWithProduction(
                FormalThreeDSaveData payload,
                GrayboxProductionRuntime3D runtime,
                IReadOnlyList<GrayboxBuildingInstance3D> instances)
        {
            var authority = new MutableAuthority(payload);
            var calls = new List<string>();
            var pauses = new List<bool>();
            IFormalThreeDSaveDomain[] domains =
                GrayboxFormalSaveCoordinator3D.DomainOrder
                    .Select(domainId => domainId ==
                        GrayboxFormalSaveDomainId3D.Production
                        ? (IFormalThreeDSaveDomain)new ProductionDomain(
                            runtime,
                            instances)
                        : new FakeDomain(
                            domainId,
                            authority,
                            calls,
                            pauses,
                            DomainFault.None))
                    .ToArray();
            return new GrayboxFormalSaveCoordinator3D(
                domains,
                new CountingRebuilder(calls, pauses));
        }

        private static GrayboxProductionRuntime3D ProductionRuntime(
            GrayboxBuildingInstance3D instance,
            FormalThreeDCitySaveData city)
        {
            var runtime = new GrayboxProductionRuntime3D();
            runtime.Synchronize(
                new[] { instance },
                (CityMode)city.cityMode,
                city.cellX,
                city.cellY,
                BuildingRangeRules.InitialGroundRadius);
            return runtime;
        }

        private static GrayboxBuildingInstance3D CompleteInstance(
            string stableId,
            BuildingDefinition definition,
            int x,
            int y)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableId,
                    new PlacedBuilding(
                        definition,
                        x,
                        y,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(definition.BuildSeconds),
                    default(ResourceNodeBinding),
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D)
                .GetMethod(
                    "Complete",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, Array.Empty<object>());
            return instance;
        }

        private sealed class CoordinatorHarness
        {
            private CoordinatorHarness(
                FormalThreeDSaveData preload,
                GrayboxFormalSaveDomainId3D? faultDomain,
                DomainFault fault,
                bool throwOnFirstRebuild)
            {
                Authority = new MutableAuthority(preload);
                Calls = new List<string>();
                PauseObservations = new List<bool>();
                Domains = GrayboxFormalSaveCoordinator3D.DomainOrder
                    .Select(domainId => new FakeDomain(
                        domainId,
                        Authority,
                        Calls,
                        PauseObservations,
                        domainId == faultDomain ? fault : DomainFault.None))
                    .ToArray();
                Rebuilder = new CountingRebuilder(
                    Calls,
                    PauseObservations)
                {
                    ThrowOnFirstInvocation = throwOnFirstRebuild,
                };
                Coordinator = new GrayboxFormalSaveCoordinator3D(
                    Domains,
                    Rebuilder);
                Func<bool> pauseSource = () =>
                    Coordinator.IsTransactionPaused;
                for (var index = 0; index < Domains.Length; index++)
                    Domains[index].PauseSource = pauseSource;
                Rebuilder.PauseSource = pauseSource;
                Coordinator.RestoreCompleted += () =>
                {
                    CompletionCount++;
                    Calls.Add("event");
                    PauseObservations.Add(pauseSource());
                };
            }

            public MutableAuthority Authority { get; }
            public List<string> Calls { get; }
            public List<bool> PauseObservations { get; }
            public FakeDomain[] Domains { get; }
            public CountingRebuilder Rebuilder { get; }
            public GrayboxFormalSaveCoordinator3D Coordinator { get; }
            public int CompletionCount { get; private set; }
            public int TotalCaptureCount =>
                Domains.Sum(domain => domain.CaptureCount);
            public int TotalApplyCount =>
                Domains.Sum(domain => domain.ApplyCount);

            public static CoordinatorHarness Create(
                FormalThreeDSaveData preload = null,
                GrayboxFormalSaveDomainId3D? faultDomain = null,
                DomainFault fault = DomainFault.None,
                bool throwOnFirstRebuild = false)
            {
                return new CoordinatorHarness(
                    preload ?? LoadFixtureEnvelope().formal3D,
                    faultDomain,
                    fault,
                    throwOnFirstRebuild);
            }
        }
    }
}
