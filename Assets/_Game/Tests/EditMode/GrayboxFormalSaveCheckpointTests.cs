using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.City;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveCheckpointTests
    {
        private const string PolicyTypeName =
            "WasteCity.Persistence.FormalSaveCheckpointPolicy";
        private const string ReasonTypeName =
            "WasteCity.Persistence.FormalSaveCheckpointReasonIds";

        [Test]
        public void PolicyPublishesSevenCurrentStableReasons()
        {
            Type reasons = RequireType(ReasonTypeName);
            var expected = new Dictionary<string, string>
            {
                { "NewGameReady", "new-game-ready" },
                {
                    "FirstDeploymentComplete",
                    "first-deployment-complete"
                },
                {
                    "FirstMachineGunComplete",
                    "first-machine-gun-complete"
                },
                {
                    "TutorialCombatStarted",
                    "tutorial-combat-started"
                },
                {
                    "EvacuationBatchConfirmed",
                    "evacuation-batch-confirmed"
                },
                {
                    "EvacuationWorkCommitted",
                    "evacuation-work-committed"
                },
                { "PackingComplete", "packing-complete" },
            };

            foreach (KeyValuePair<string, string> item in expected)
                Assert.That(ReadReason(reasons, item.Key),
                    Is.EqualTo(item.Value));
        }

        [Test]
        public void OneShotMilestoneSavesOnlyOnceAndCommitsAfterSuccess()
        {
            var sink = new FakeSink(true);
            var clock = new FakeClock { RuleTimeSeconds = 12.5f };
            object policy = CreatePolicy(sink, clock);
            string reason = Reason("FirstDeploymentComplete");

            Queue(policy, reason, "deployment.000001");
            Assert.That(Flush(policy), Is.True);
            Queue(policy, reason, "deployment.000002");
            Flush(policy);

            Assert.That(sink.Attempts, Has.Count.EqualTo(1));
            Assert.That(Sequence(policy), Is.EqualTo(1L));
            CollectionAssert.AreEqual(
                new[] { reason },
                CompletedMilestones(policy));
            FormalSaveCheckpointMetadata saved = sink.Attempts.Single();
            Assert.That(saved.sequence, Is.EqualTo(1L));
            Assert.That(saved.reasonId, Is.EqualTo(reason));
            Assert.That(saved.ruleTimeSeconds, Is.EqualTo(12.5f));
            CollectionAssert.AreEqual(
                new[] { reason },
                saved.completedMilestoneIds);
        }

        [Test]
        public void EvacuationWorkDeduplicatesByStableEventIdentity()
        {
            var sink = new FakeSink(true, true);
            object policy = CreatePolicy(sink, new FakeClock());
            string reason = Reason("EvacuationWorkCommitted");

            Queue(policy, reason, "evacuation.batch.000001|building.000001");
            Queue(policy, reason, "evacuation.batch.000001|building.000001");
            Assert.That(sink.Attempts, Is.Empty,
                "Queueing an event must not synchronously write.");
            Assert.That(Flush(policy), Is.True);
            Queue(policy, reason, "evacuation.batch.000001|building.000002");
            Assert.That(Flush(policy), Is.True);

            Assert.That(sink.Attempts, Has.Count.EqualTo(2));
            Assert.That(Sequence(policy), Is.EqualTo(2L));
            Assert.That(sink.Attempts.Select(value => value.sequence),
                Is.EqualTo(new long[] { 1L, 2L }));
            CollectionAssert.IsEmpty(CompletedMilestones(policy),
                "Repeatable work identities are not one-shot milestones.");
        }

        [Test]
        public void FailedSaveDoesNotCommitSequenceOrMilestoneAndCanRetry()
        {
            var sink = new FakeSink(false, true);
            object policy = CreatePolicy(sink, new FakeClock
            {
                RuleTimeSeconds = 21f,
            });
            string reason = Reason("FirstMachineGunComplete");
            const string eventId = "building.instance.000003";

            Queue(policy, reason, eventId);
            Assert.That(Flush(policy), Is.False);
            Assert.That(Sequence(policy), Is.Zero);
            CollectionAssert.IsEmpty(CompletedMilestones(policy));
            Assert.That(ReadBoolProperty(policy, "HasPending"), Is.True);
            Assert.That(ReadBoolProperty(policy, "HasFailureWarning"),
                Is.True);

            Assert.That(Flush(policy), Is.True,
                "A failed request remains pending and can retry without a " +
                "new domain event.");
            Assert.That(sink.Attempts, Has.Count.EqualTo(2));
            Assert.That(sink.Attempts[0].sequence, Is.EqualTo(1L));
            Assert.That(sink.Attempts[1].sequence, Is.EqualTo(1L),
                "A failed attempt must not consume the next sequence.");
            Assert.That(Sequence(policy), Is.EqualTo(1L));
            CollectionAssert.AreEqual(
                new[] { reason },
                CompletedMilestones(policy));
            Assert.That(ReadBoolProperty(policy, "HasPending"), Is.False);
            Assert.That(ReadBoolProperty(policy, "HasFailureWarning"),
                Is.False);
        }

        [Test]
        public void PendingRevisionAdvancesOnlyForNewEffectivePendingEvents()
        {
            var sink = new FakeSink(false, true, true);
            object policy = CreatePolicy(sink, new FakeClock());
            string oneShot = Reason("FirstDeploymentComplete");
            string repeatable = Reason("EvacuationWorkCommitted");
            const string firstEvent = "deployment.000001";

            Assert.That(PendingRevision(policy), Is.Zero);

            Queue(policy, oneShot, firstEvent);
            Assert.That(PendingRevision(policy), Is.EqualTo(1UL));

            Queue(policy, oneShot, firstEvent);
            Queue(policy, "unknown-checkpoint-reason", "unknown.000001");
            Assert.That(PendingRevision(policy), Is.EqualTo(1UL),
                "Duplicate and unknown events do not change pending content.");

            ((FormalSaveCheckpointPolicy)policy).SetSuppressed(true);
            Queue(
                policy,
                repeatable,
                "evacuation.batch.000001|building.000001");
            ((FormalSaveCheckpointPolicy)policy).SetSuppressed(false);
            Assert.That(PendingRevision(policy), Is.EqualTo(1UL),
                "Suppressed events never join pending.");

            Assert.That(Flush(policy), Is.False);
            Assert.That(PendingRevision(policy), Is.EqualTo(1UL),
                "Merging a failed in-flight batch back into pending is not " +
                "a new domain event.");
            Assert.That(Flush(policy), Is.True);
            Assert.That(PendingRevision(policy), Is.EqualTo(1UL),
                "Flushing existing pending content does not create an event.");

            Queue(policy, oneShot, "deployment.000002");
            Assert.That(PendingRevision(policy), Is.EqualTo(1UL),
                "A completed one-shot milestone cannot rejoin pending.");

            Queue(
                policy,
                repeatable,
                "evacuation.batch.000001|building.000001");
            Assert.That(PendingRevision(policy), Is.EqualTo(2UL));
            Queue(
                policy,
                repeatable,
                "evacuation.batch.000001|building.000001");
            Assert.That(PendingRevision(policy), Is.EqualTo(2UL));
            Assert.That(Flush(policy), Is.True);
            Assert.That(PendingRevision(policy), Is.EqualTo(2UL));
        }

        [Test]
        public void SameFrameEventsCoalesceWithPriorityAndMilestoneUnion()
        {
            var sink = new FakeSink(true);
            object policy = CreatePolicy(sink, new FakeClock
            {
                RuleTimeSeconds = 33f,
            });
            string deployment = Reason("FirstDeploymentComplete");
            string machineGun = Reason("FirstMachineGunComplete");

            Queue(policy, deployment, "deployment.000001");
            Queue(policy, machineGun, "building.instance.000003");
            Assert.That(sink.Attempts, Is.Empty);
            Assert.That(Flush(policy), Is.True);

            Assert.That(sink.Attempts, Has.Count.EqualTo(1));
            FormalSaveCheckpointMetadata saved = sink.Attempts.Single();
            Assert.That(saved.reasonId, Is.EqualTo(machineGun),
                "Later-stage first-machine-gun is the deterministic higher " +
                "priority reason regardless of queue timing.");
            CollectionAssert.AreEquivalent(
                new[] { deployment, machineGun },
                saved.completedMilestoneIds);
            CollectionAssert.AreEquivalent(
                new[] { deployment, machineGun },
                CompletedMilestones(policy));
            Assert.That(Sequence(policy), Is.EqualTo(1L));
        }

        [Test]
        public void EventQueuedDuringSaveUsesNextFreshFlush()
        {
            var sink = new FakeSink(true, true);
            var clock = new FakeClock { RuleTimeSeconds = 4f };
            object policy = CreatePolicy(sink, clock);
            string first = Reason("FirstDeploymentComplete");
            string pending = Reason("PackingComplete");
            sink.OnAttempt = attempt =>
            {
                if (attempt != 1) return;
                clock.RuleTimeSeconds = 99f;
                Queue(policy, pending, "packing.000001");
            };

            Queue(policy, first, "deployment.000001");
            Assert.That(Flush(policy), Is.True);
            Assert.That(sink.Attempts, Has.Count.EqualTo(1),
                "An event arriving during save is not folded into the stale " +
                "in-flight metadata.");
            Assert.That(Flush(policy), Is.True);

            Assert.That(sink.Attempts, Has.Count.EqualTo(2));
            Assert.That(sink.Attempts[0].reasonId, Is.EqualTo(first));
            Assert.That(sink.Attempts[0].ruleTimeSeconds, Is.EqualTo(4f));
            Assert.That(sink.Attempts[1].reasonId, Is.EqualTo(pending));
            Assert.That(sink.Attempts[1].ruleTimeSeconds, Is.EqualTo(99f),
                "The latest pending request must recapture at its own flush.");
            Assert.That(sink.Attempts[1].sequence, Is.EqualTo(2L));
            Flush(policy);
            Assert.That(sink.Attempts, Has.Count.EqualTo(2),
                "Only one latest pending request survives the busy save.");
        }

        [Test]
        public void FutureFateAndBossReasonsRemainConstantsOnly()
        {
            Type reasons = RequireType(ReasonTypeName);
            Assert.That(ReadReason(reasons, "FateSelectionComplete"),
                Is.EqualTo("fate-selection-complete"));
            Assert.That(ReadReason(reasons, "BossEventStarted"),
                Is.EqualTo("boss-event-started"));

            Type policy = RequireType(PolicyTypeName);
            string[] publicEntryPoints = policy.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly)
                .Select(value => value.Name)
                .ToArray();
            Assert.That(publicEntryPoints.Any(value =>
                    value.IndexOf("Fate", StringComparison.OrdinalIgnoreCase) >=
                    0),
                Is.False);
            Assert.That(publicEntryPoints.Any(value =>
                    value.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >=
                    0),
                Is.False);

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Persistence/" +
                "FormalSaveCheckpointPolicy.cs"));
            Assert.That(Count(source, "fate-selection-complete"),
                Is.EqualTo(1));
            Assert.That(Count(source, "boss-event-started"),
                Is.EqualTo(1));
        }

        [Test]
        public void PolicyHasNoTickResearchOrUiPollingEntryPoint()
        {
            Type policy = RequireType(PolicyTypeName);
            string[] forbiddenTokens =
            {
                "Tick",
                "Update",
                "Refresh",
                "Research",
                "Ui",
                "UI",
            };
            string[] publicEntryPoints = policy.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly)
                .Select(value => value.Name)
                .ToArray();
            for (var index = 0; index < forbiddenTokens.Length; index++)
            {
                string token = forbiddenTokens[index];
                Assert.That(publicEntryPoints.Any(value =>
                        value.IndexOf(token, StringComparison.Ordinal) >= 0),
                    Is.False,
                    "Automatic checkpoints must be domain-event driven.");
            }
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(policy),
                Is.False,
                "The pure policy must not acquire a per-frame lifecycle.");
        }

        [Test]
        public void RebindingSameSourceDoesNotAccumulateSubscriptions()
        {
            var sink = new FakeSink(true);
            object policy = CreatePolicy(sink, new FakeClock());
            var source = new FakeEventSource();
            var subscribe = new Action<Action<string, string>>(
                source.Subscribe);
            var unsubscribe = new Action<Action<string, string>>(
                source.Unsubscribe);
            MethodInfo bind = FindBindBoundary(policy.GetType());
            object[] arguments = BuildBindArguments(
                bind,
                subscribe,
                unsubscribe);
            bind.Invoke(policy, arguments);
            bind.Invoke(policy, arguments);

            Assert.That(source.ListenerCount, Is.EqualTo(1));
            Assert.That(source.SubscribeCount, Is.EqualTo(2));
            Assert.That(source.UnsubscribeCount, Is.EqualTo(1));
            source.Raise(
                Reason("EvacuationWorkCommitted"),
                "evacuation.batch.000001|building.000001");
            Assert.That(sink.Attempts, Is.Empty,
                "Event bindings queue; the explicit frame boundary writes.");
            Assert.That(Flush(policy), Is.True);
            Assert.That(sink.Attempts, Has.Count.EqualTo(1));
        }

        [TestCase(
            "FirstMachineGunCompleted",
            "TrySynchronizeRuntime",
            "TutorialWaveTriggerCount")]
        [TestCase(
            "TutorialCombatStarted",
            "runtime.Tick(",
            "SpawnedEnemyCount")]
        public void DefensePublishesCheckpointOnlyAfterCommittedTransition(
            string eventName,
            string committedBoundary,
            string transitionCounter)
        {
            Type controller = RequireType(
                "WasteCity.Graybox3D.Building.GrayboxDefenseController3D");
            EventInfo checkpoint = controller.GetEvent(
                eventName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(checkpoint, Is.Not.Null, controller.FullName);
            Assert.That(checkpoint.EventHandlerType,
                Is.EqualTo(typeof(Action<string>)),
                "The event must carry a stable tower/enemy identity.");

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxDefenseController3D.cs"));
            string tick = ExtractMethodBlock(source, "public bool Tick(");
            int transition = tick.IndexOf(
                transitionCounter,
                StringComparison.Ordinal);
            int boundary = tick.IndexOf(
                committedBoundary,
                StringComparison.Ordinal);
            int publish = tick.IndexOf(
                eventName + "?.Invoke(",
                StringComparison.Ordinal);
            Assert.That(transition, Is.GreaterThanOrEqualTo(0),
                "Publishing requires a first-transition counter edge.");
            Assert.That(boundary, Is.GreaterThanOrEqualTo(0));
            Assert.That(publish, Is.GreaterThan(boundary),
                "Checkpoint events publish only after the domain transition " +
                "is committed.");

            string rebuild = ExtractMethodBlock(
                source,
                "public bool TryRebuildAfterPersistenceRestore(");
            StringAssert.DoesNotContain(eventName, rebuild,
                "Restore rebuild is no-advance and must not emit gameplay " +
                "checkpoint events.");
        }

        [Test]
        public void DeploymentPublishesOnlyCommittedStableTransitions()
        {
            var deployment = new CityDeploymentModel(1f, 1f);
            EventInfo checkpoint = typeof(CityDeploymentModel).GetEvent(
                "CheckpointCommitted",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(checkpoint, Is.Not.Null);
            Assert.That(checkpoint.EventHandlerType,
                Is.EqualTo(typeof(Action<string, string>)));

            var raised = new List<string>();
            var listener = new Action<string, string>((reason, identity) =>
                raised.Add(reason + "|" + identity));
            checkpoint.AddEventHandler(deployment, listener);

            deployment.Toggle();
            deployment.Tick(.25f);
            Assert.That(raised, Is.Empty,
                "Starting or advancing a transition is not a checkpoint.");
            deployment.Restore(CityMode.Mobile, 0f);
            Assert.That(raised, Is.Empty,
                "Persistence restore must not emit gameplay checkpoints.");

            deployment.Toggle();
            deployment.Tick(1f);
            Assert.That(raised, Has.Count.EqualTo(1));
            Assert.That(raised[0],
                Does.StartWith(Reason("FirstDeploymentComplete") + "|"));

            deployment.Toggle();
            deployment.Tick(1f);
            Assert.That(raised, Has.Count.EqualTo(2));
            Assert.That(raised[1],
                Does.StartWith(Reason("PackingComplete") + "|"));
        }

        [Test]
        public void BuildingSessionOwnsRestorableMonotonicRuleTime()
        {
            var root = new GameObject("checkpoint-rule-time-test");
            try
            {
                var session = root.AddComponent<GrayboxBuildingSession3D>();
                session.ConfigureFormalSession();
                var presentation = new PassiveBuildingPresentation();

                session.TickConstruction(
                    2f,
                    CityMode.Mobile,
                    paused: false,
                    presentation);
                Assert.That(session.CheckpointRuleTimeSeconds,
                    Is.GreaterThan(0f));
                float advanced = session.CheckpointRuleTimeSeconds;

                session.TickConstruction(
                    5f,
                    CityMode.Mobile,
                    paused: true,
                    presentation);
                Assert.That(session.CheckpointRuleTimeSeconds,
                    Is.EqualTo(advanced));

                Assert.That(session.TryRestoreCheckpointRuleTime(
                    17.5f,
                    out string error), Is.True, error);
                Assert.That(session.CheckpointRuleTimeSeconds,
                    Is.EqualTo(17.5f));
                Assert.That(session.TryRestoreCheckpointRuleTime(
                    float.NaN,
                    out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EvacuationPublishesOnlyAfterConfirmedAndWorkBoundaries()
        {
            Type controller = typeof(GrayboxEvacuationController3D);
            EventInfo checkpoint = controller.GetEvent(
                "CheckpointCommitted",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(checkpoint, Is.Not.Null);
            Assert.That(checkpoint.EventHandlerType,
                Is.EqualTo(typeof(Action<string, string>)));

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxEvacuationController3D.cs"));
            string confirm = ExtractMethodBlock(
                source,
                "public bool ConfirmManifest(");
            Assert.That(confirm.IndexOf(
                    "CheckpointCommitted?.Invoke(",
                    StringComparison.Ordinal),
                Is.GreaterThan(confirm.IndexOf(
                    "activeBatchId =",
                    StringComparison.Ordinal)));
            Assert.That(confirm.IndexOf(
                    "CheckpointCommitted?.Invoke(",
                    StringComparison.Ordinal),
                Is.LessThan(confirm.IndexOf(
                    "AdvanceThroughImmediateWork()",
                    StringComparison.Ordinal)));

            string commit = ExtractMethodBlock(
                source,
                "private CommitCurrentResult TryCommitCurrentCore(");
            Assert.That(Count(
                    commit,
                    "FormalSaveCheckpointReasonIds." +
                    "EvacuationWorkCommitted"),
                Is.EqualTo(2),
                "Capacity-blocked and committed work share one reason family.");
            Assert.That(commit.IndexOf(
                    "CheckpointCommitted?.Invoke(",
                    StringComparison.Ordinal),
                Is.GreaterThan(commit.IndexOf(
                    "persistenceGeneration++",
                    StringComparison.Ordinal)));
        }

        [Test]
        public void CoordinatorReconfigureDoesNotAccumulateDomainListeners()
        {
            var root = new GameObject("checkpoint-coordinator-bind-test");
            try
            {
                var session = root.AddComponent<GrayboxBuildingSession3D>();
                var defense = root.AddComponent<GrayboxDefenseController3D>();
                var evacuation =
                    root.AddComponent<GrayboxEvacuationController3D>();
                var deployment = new CityDeploymentModel(.1f, .1f);
                var sink = new FakeSink(true);
                var policy = new FormalSaveCheckpointPolicy(
                    sink.TrySave,
                    () => 0f);
                var coordinator = CreateBareCoordinator();

                coordinator.ConfigureCheckpointPolicy(
                    policy,
                    deployment,
                    session,
                    defense,
                    evacuation);
                coordinator.ConfigureCheckpointPolicy(
                    policy,
                    deployment,
                    session,
                    defense,
                    evacuation);
                Assert.That(coordinator.QueueNewGameReady(string.Empty),
                    Is.False,
                    "A blank progress identity is never a stable event ID.");

                deployment.Toggle();
                deployment.Tick(1f);
                Assert.That(coordinator.FlushPendingCheckpoint(), Is.True);
                Assert.That(sink.Attempts, Has.Count.EqualTo(1));

                coordinator.UnbindCheckpointPolicy();
                deployment.Toggle();
                deployment.Tick(1f);
                Assert.That(coordinator.FlushPendingCheckpoint(), Is.False);
                Assert.That(sink.Attempts, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CoordinatorWritesOnlyAfterFullCaptureSucceeds()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs"));
            string write = ExtractMethodBlock(
                source,
                "public bool TryWriteCheckpoint(");
            int capture = write.IndexOf(
                "CaptureEnvelope(",
                StringComparison.Ordinal);
            int successGate = write.IndexOf(
                "LastCheckpointCaptureResult.Success",
                StringComparison.Ordinal);
            int singleSlotWrite = write.IndexOf(
                "store.SaveEnvelope(",
                StringComparison.Ordinal);
            Assert.That(capture, Is.GreaterThanOrEqualTo(0));
            Assert.That(successGate, Is.GreaterThan(capture));
            Assert.That(singleSlotWrite, Is.GreaterThan(successGate));
            StringAssert.DoesNotContain("File.", write,
                "Coordinator must use the unique FormalSaveStore boundary.");
        }

        private static object CreatePolicy(FakeSink sink, FakeClock clock)
        {
            Type policy = RequireType(PolicyTypeName);
            Type saveType = typeof(Func<FormalSaveCheckpointMetadata, bool>);
            Type clockType = typeof(Func<float>);
            var save = new Func<FormalSaveCheckpointMetadata, bool>(
                sink.TrySave);
            var time = new Func<float>(clock.Capture);
            ConstructorInfo constructor = policy.GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(value => HasInjectableParameters(
                    value.GetParameters(), saveType, clockType));
            if (constructor != null)
            {
                return constructor.Invoke(BuildInjectionArguments(
                    constructor.GetParameters(), save, time));
            }

            MethodInfo factory = policy.GetMethods(
                    BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(value =>
                    value.ReturnType == policy &&
                    HasInjectableParameters(
                        value.GetParameters(), saveType, clockType));
            Assert.That(factory, Is.Not.Null,
                "Checkpoint policy needs a public constructor or factory " +
                "with injectable save and rule clocks.");
            return factory.Invoke(null, BuildInjectionArguments(
                factory.GetParameters(), save, time));
        }

        private static bool HasInjectableParameters(
            ParameterInfo[] parameters,
            Type saveType,
            Type clockType)
        {
            return parameters.Count(value => value.ParameterType == saveType) ==
                   1 &&
                   parameters.Count(value => value.ParameterType == clockType) ==
                   1 &&
                   parameters.All(value =>
                       value.ParameterType == saveType ||
                       value.ParameterType == clockType ||
                       value.HasDefaultValue);
        }

        private static object[] BuildInjectionArguments(
            ParameterInfo[] parameters,
            Func<FormalSaveCheckpointMetadata, bool> save,
            Func<float> clock)
        {
            return parameters.Select(value =>
            {
                if (value.ParameterType == save.GetType()) return (object)save;
                if (value.ParameterType == clock.GetType()) return clock;
                return value.DefaultValue;
            }).ToArray();
        }

        private static MethodInfo FindBindBoundary(Type policy)
        {
            Type subscribeType = typeof(Action<Action<string, string>>);
            MethodInfo bind = policy.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(value =>
                    value.Name == "Bind" &&
                    value.ReturnType == typeof(void) &&
                    value.GetParameters().Count(parameter =>
                        parameter.ParameterType == subscribeType) == 2 &&
                    value.GetParameters().All(parameter =>
                        parameter.ParameterType == subscribeType ||
                        parameter.ParameterType == typeof(string) ||
                        parameter.HasDefaultValue));
            Assert.That(bind, Is.Not.Null,
                "Policy needs one minimal public subscription boundary.");
            return bind;
        }

        private static object[] BuildBindArguments(
            MethodInfo bind,
            Action<Action<string, string>> subscribe,
            Action<Action<string, string>> unsubscribe)
        {
            var delegateIndex = 0;
            return bind.GetParameters().Select(parameter =>
            {
                if (parameter.ParameterType == typeof(string))
                    return (object)"evacuation";
                if (parameter.ParameterType ==
                    typeof(Action<Action<string, string>>))
                {
                    return delegateIndex++ == 0
                        ? subscribe
                        : unsubscribe;
                }
                return parameter.DefaultValue;
            }).ToArray();
        }

        private static void Queue(
            object policy,
            string reasonId,
            string stableEventId)
        {
            MethodInfo method = policy.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(value =>
                    (value.Name == "QueueCheckpoint" ||
                     value.Name == "TryQueue") &&
                    (value.ReturnType == typeof(void) ||
                     value.ReturnType == typeof(bool)) &&
                    value.GetParameters().Select(parameter =>
                            parameter.ParameterType)
                        .SequenceEqual(new[]
                        {
                            typeof(string),
                            typeof(string),
                        }));
            Assert.That(method, Is.Not.Null,
                "Policy needs an event queue boundary, not immediate I/O.");
            method.Invoke(
                policy,
                new object[] { reasonId, stableEventId });
        }

        private static bool Flush(object policy)
        {
            MethodInfo method = policy.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(value =>
                    (value.Name == "FlushPending" ||
                     value.Name == "TryFlush") &&
                    value.ReturnType == typeof(bool) &&
                    value.GetParameters().Length == 0);
            Assert.That(method, Is.Not.Null,
                "Policy needs one explicit synchronous frame flush boundary.");
            return (bool)method.Invoke(policy, null);
        }

        private static long Sequence(object policy)
        {
            PropertyInfo property = policy.GetType().GetProperty(
                "Sequence",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanWrite, Is.False);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(long)));
            return (long)property.GetValue(policy, null);
        }

        private static ulong PendingRevision(object policy)
        {
            PropertyInfo property = policy.GetType().GetProperty(
                "PendingRevision",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "Checkpoint policy must expose its pending content revision.");
            Assert.That(property.CanWrite, Is.False,
                "Pending revision is observation-only.");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(ulong)));
            return (ulong)property.GetValue(policy, null);
        }

        private static bool ReadBoolProperty(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, owner.GetType().FullName + name);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(bool)));
            Assert.That(property.CanWrite, Is.False);
            return (bool)property.GetValue(owner, null);
        }

        private static string[] CompletedMilestones(object policy)
        {
            PropertyInfo property = policy.GetType().GetProperty(
                "CompletedMilestoneIds",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanWrite, Is.False);
            object value = property.GetValue(policy, null);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>()
                .Select(item => item.ToString())
                .ToArray();
        }

        private static string Reason(string fieldName)
        {
            return ReadReason(RequireType(ReasonTypeName), fieldName);
        }

        private static string ReadReason(Type owner, string fieldName)
        {
            FieldInfo field = owner.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, owner.FullName + "." + fieldName);
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)));
            Assert.That(field.IsLiteral, Is.True,
                "Stable reason IDs must be compile-time constants.");
            return (string)field.GetRawConstantValue();
        }

        private static Type RequireType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            Assert.Fail("Missing Task 11 runtime type: " + fullName);
            return null;
        }

        private static GrayboxFormalSaveCoordinator3D
            CreateBareCoordinator()
        {
            var domains = new List<IFormalThreeDSaveDomain>();
            foreach (GrayboxFormalSaveDomainId3D domainId in
                     Enum.GetValues(typeof(GrayboxFormalSaveDomainId3D)))
            {
                domains.Add(new PassiveSaveDomain(domainId));
            }
            return new GrayboxFormalSaveCoordinator3D(
                domains,
                new PassiveDerivedStateRebuilder());
        }

        private static MethodInfo RequireMethod(
            Type owner,
            string name,
            Type returnType,
            params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameters,
                null);
            Assert.That(method, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
            return method;
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
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

        private sealed class FakeSink
        {
            private readonly Queue<bool> results;

            public FakeSink(params bool[] results)
            {
                this.results = new Queue<bool>(results);
            }

            public List<FormalSaveCheckpointMetadata> Attempts { get; } =
                new List<FormalSaveCheckpointMetadata>();
            public Action<int> OnAttempt { get; set; }

            public bool TrySave(FormalSaveCheckpointMetadata checkpoint)
            {
                Attempts.Add(new FormalSaveCheckpointMetadata
                {
                    sequence = checkpoint.sequence,
                    reasonId = checkpoint.reasonId,
                    ruleTimeSeconds = checkpoint.ruleTimeSeconds,
                    completedMilestoneIds = checkpoint.completedMilestoneIds ==
                        null
                            ? null
                            : (string[])checkpoint.completedMilestoneIds.Clone(),
                });
                OnAttempt?.Invoke(Attempts.Count);
                return results.Count == 0 || results.Dequeue();
            }
        }

        private sealed class FakeClock
        {
            public float RuleTimeSeconds { get; set; }

            public float Capture()
            {
                return RuleTimeSeconds;
            }
        }

        private sealed class FakeEventSource
        {
            private event Action<string, string> Raised;

            public int SubscribeCount { get; private set; }
            public int UnsubscribeCount { get; private set; }
            public int ListenerCount => Raised?.GetInvocationList().Length ?? 0;

            public void Subscribe(Action<string, string> listener)
            {
                SubscribeCount++;
                Raised += listener;
            }

            public void Unsubscribe(Action<string, string> listener)
            {
                UnsubscribeCount++;
                Raised -= listener;
            }

            public void Raise(string reasonId, string stableEventId)
            {
                Raised?.Invoke(reasonId, stableEventId);
            }
        }

        private sealed class PassiveBuildingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
            }
        }

        private sealed class PassiveSaveDomain : IFormalThreeDSaveDomain
        {
            public PassiveSaveDomain(GrayboxFormalSaveDomainId3D domainId)
            {
                DomainId = domainId;
            }

            public GrayboxFormalSaveDomainId3D DomainId { get; }

            public bool TryCapture(
                WasteCity.Persistence.ThreeD.FormalThreeDSaveData destination,
                out string error)
            {
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                WasteCity.Persistence.ThreeD.FormalThreeDSaveData source,
                out string error)
            {
                error = string.Empty;
                return true;
            }
        }

        private sealed class PassiveDerivedStateRebuilder :
            IFormalThreeDDerivedStateRebuilder
        {
            public void RebuildDerivedState()
            {
            }
        }
    }
}
