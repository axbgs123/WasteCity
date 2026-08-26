using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalProgressionSaveAdapterTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxFormalProgressionSaveAdapter3D, WasteCity.Game";
        private const string PlanTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxFormalProgressionRestorePlan3D, WasteCity.Game";

        private static readonly string[] FateIds =
        {
            "core.legacy.pocket-universe",
            "core.legacy.void-debt",
            "core.legacy.rewind-anchor",
        };

        [Test]
        public void IDEA0020_AdapterRequiresExplicitAttentionAndFateOwners()
        {
            Type adapter = RequireType(AdapterTypeName);
            ConstructorInfo constructor = adapter.GetConstructor(new[]
            {
                typeof(FormalAttentionRuntime),
                typeof(FormalFateRuntime),
            });
            Assert.That(constructor, Is.Not.Null);
            Assert.That(adapter.GetConstructors(), Has.Length.EqualTo(1));
            Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke(new object[]
                {
                    null,
                    new FormalFateRuntime(),
                }));
            Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke(new object[]
                {
                    new FormalAttentionRuntime(),
                    null,
                }));
        }

        [Test]
        public void IDEA0020_CaptureMapsBothRuntimesToIndependentSchema33Dto()
        {
            var attention = new FormalAttentionRuntime();
            Assert.That(attention.TryApply(
                "core.attention.fate.first-activation",
                "fate-selection-complete",
                out string attentionError), Is.True, attentionError);
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FateIds[1],
                out _,
                out _,
                out string fateError), Is.True, fateError);
            object adapter = CreateAdapter(attention, fate);

            FormalThreeDProgressionSaveData first = Capture(adapter);
            Assert.That(first.configurationSignature,
                Is.EqualTo("builtin:progression@1"));
            Assert.That(first.attention.value, Is.EqualTo(15));
            Assert.That(first.attention.revision, Is.EqualTo(1ul));
            Assert.That(first.attention.history, Has.Length.EqualTo(1));
            Assert.That(first.attention.history[0].reasonId,
                Is.EqualTo("core.attention.fate.first-activation"));
            Assert.That(first.attention.history[0].stableEventKey,
                Is.EqualTo("fate-selection-complete"));
            Assert.That(first.attention.committedStableEventKeys,
                Is.EqualTo(new[] { "fate-selection-complete" }));
            Assert.That(first.attention.completedOneShotReasonIds,
                Is.EqualTo(new[]
                {
                    "core.attention.fate.first-activation",
                }));
            Assert.That(first.fate.offeredIds, Is.EqualTo(FateIds));
            Assert.That(first.fate.selectedId, Is.EqualTo(FateIds[1]));
            Assert.That(first.fate.level, Is.EqualTo(1));
            Assert.That(first.fate.revision, Is.EqualTo(1ul));
            Assert.That(first.civilization.level, Is.EqualTo(1));

            first.attention.history[0].reasonId = "mutated.reason.id";
            first.attention.committedStableEventKeys[0] = "mutated.event.key";
            first.fate.offeredIds[0] = FateIds[2];
            FormalThreeDProgressionSaveData second = Capture(adapter);
            Assert.That(second.attention.history[0].reasonId,
                Is.EqualTo("core.attention.fate.first-activation"));
            Assert.That(second.attention.committedStableEventKeys[0],
                Is.EqualTo("fate-selection-complete"));
            Assert.That(second.fate.offeredIds, Is.EqualTo(FateIds));
        }

        [Test]
        public void IDEA0020_PrepareIsZeroWriteAndDeepCopiesUnknownHistory()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object adapter = CreateAdapter(attention, fate);
            FormalThreeDProgressionSaveData source = UnknownHistoryPayload(
                FateIds[2],
                fateRevision: 7ul);
            string before = RuntimeFingerprint(attention, fate);

            Assert.That(TryPrepare(
                adapter,
                source,
                out object plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(before),
                "Prepare must validate and clone without writing either runtime.");

            source.attention.value = 100;
            source.attention.history[0].reasonId = "mutated.reason.id";
            source.attention.committedStableEventKeys[0] =
                "mutated.event.key";
            source.fate.selectedId = "core.legacy.unknown";
            Assert.That(TryCommit(
                adapter,
                plan,
                out string commitError), Is.True, commitError);

            FormalThreeDProgressionSaveData restored = Capture(adapter);
            Assert.That(restored.attention.value, Is.EqualTo(15));
            Assert.That(restored.attention.history[0].reasonId,
                Is.EqualTo("removed.attention.reason"),
                "Unknown historical reasons are preserved as orphan evidence.");
            Assert.That(restored.attention.committedStableEventKeys[0],
                Is.EqualTo("removed.attention.event"));
            Assert.That(restored.fate.selectedId, Is.EqualTo(FateIds[2]));
            Assert.That(restored.fate.level, Is.EqualTo(1));
            Assert.That(restored.fate.revision, Is.EqualTo(7ul));
        }

        [Test]
        public void IDEA0020_RestorePlanIsOwnerRevisionBoundAndSingleUse()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object adapter = CreateAdapter(attention, fate);
            FormalThreeDProgressionSaveData source = UnknownHistoryPayload(
                FateIds[0],
                fateRevision: 3ul);

            Assert.That(TryPrepare(adapter, source, out object stale, out _),
                Is.True);
            Assert.That(attention.TryApply(
                "core.attention.scan.safe-mining-zone",
                "runtime.changed.after-prepare",
                out _), Is.True);
            string changed = RuntimeFingerprint(attention, fate);
            Assert.That(TryCommit(adapter, stale, out _), Is.False);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(changed));

            Assert.That(TryPrepare(adapter, source, out object foreign, out _),
                Is.True);
            var otherAttention = new FormalAttentionRuntime();
            var otherFate = new FormalFateRuntime();
            object other = CreateAdapter(otherAttention, otherFate);
            string otherBefore = RuntimeFingerprint(otherAttention, otherFate);
            Assert.That(TryCommit(other, foreign, out _), Is.False);
            Assert.That(RuntimeFingerprint(otherAttention, otherFate),
                Is.EqualTo(otherBefore));

            Assert.That(TryPrepare(adapter, source, out object valid, out _),
                Is.True);
            Assert.That(TryCommit(adapter, valid, out string error),
                Is.True, error);
            string committed = RuntimeFingerprint(attention, fate);
            Assert.That(TryCommit(adapter, valid, out _), Is.False);
            Assert.That(RuntimeFingerprint(attention, fate),
                Is.EqualTo(committed));
        }

        [Test]
        public void IDEA0020_InvalidAttentionOrFateFailsWithoutPartialMutation()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object adapter = CreateAdapter(attention, fate);
            string before = RuntimeFingerprint(attention, fate);

            FormalThreeDProgressionSaveData invalidAttention =
                UnknownHistoryPayload(FateIds[0], 1ul);
            invalidAttention.attention.value = 101;
            Assert.That(TryPrepare(
                adapter,
                invalidAttention,
                out _,
                out string attentionError), Is.False);
            Assert.That(attentionError, Is.Not.Empty);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(before));

            FormalThreeDProgressionSaveData invalidFate =
                UnknownHistoryPayload(FateIds[0], 1ul);
            invalidFate.fate.selectedId = "core.legacy.quantum-entanglement";
            Assert.That(TryPrepare(
                adapter,
                invalidFate,
                out _,
                out string fateError), Is.False);
            Assert.That(fateError, Is.Not.Empty);
            Assert.That(RuntimeFingerprint(attention, fate), Is.EqualTo(before));
        }

        [Test]
        public void IDEA0020_CoordinatorOwnsProgressionAsEighthTransactionalDomain()
        {
            Type coordinator = RequireType(
                "WasteCity.Graybox3D.Building." +
                "GrayboxFormalSaveCoordinator3D, WasteCity.Game");
            Type domainId = RequireType(
                "WasteCity.Graybox3D.Building." +
                "GrayboxFormalSaveDomainId3D, WasteCity.Game");
            Assert.That(Enum.GetNames(domainId), Does.Contain("Progression"));
            PropertyInfo order = coordinator.GetProperty(
                "DomainOrder",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(order, Is.Not.Null);
            string[] names = ((IEnumerable)order.GetValue(null))
                .Cast<object>()
                .Select(value => value.ToString())
                .ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                "WorldCity",
                "BuildingStorage",
                "Economy",
                "Production",
                "Progression",
                "Defense",
                "Evacuation",
                "Pause",
            }));

            string source = File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveCoordinator3D.cs")));
            StringAssert.Contains("GrayboxFormalProgressionSaveAdapter3D", source);
            StringAssert.Contains("GrayboxFormalSaveDomainId3D.Progression", source);
            StringAssert.Contains("destination.progression", source);
            StringAssert.Contains("source.progression", source);
            StringAssert.Contains("for (var index = 0; index < domains.Length; index++)",
                source,
                "Capture, apply and rollback must include the eighth domain " +
                "through the single ordered domain array.");
        }

        private static FormalThreeDProgressionSaveData UnknownHistoryPayload(
            string selectedFateId,
            ulong fateRevision)
        {
            return new FormalThreeDProgressionSaveData
            {
                attention = new FormalThreeDAttentionSaveData
                {
                    value = 15,
                    revision = 1ul,
                    history = new[]
                    {
                        new FormalThreeDAttentionHistorySaveData
                        {
                            reasonId = "removed.attention.reason",
                            stableEventKey = "removed.attention.event",
                            requestedDelta = 5,
                            appliedDelta = 5,
                            valueAfter = 15,
                            revision = 1ul,
                            ruleTimeSeconds = 12.5f,
                            sourceInstanceId = "building.instance.000001",
                        },
                    },
                    reachedThresholds = Array.Empty<int>(),
                    committedStableEventKeys = new[]
                    {
                        "removed.attention.event",
                    },
                    completedOneShotReasonIds = Array.Empty<string>(),
                },
                fate = new FormalThreeDFateSaveData
                {
                    offeredIds = FateIds.ToArray(),
                    selectedId = selectedFateId,
                    level = 1,
                    revision = fateRevision,
                },
                civilization = new FormalThreeDCivilizationSaveData(),
            };
        }

        private static object CreateAdapter(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            return Activator.CreateInstance(
                RequireType(AdapterTypeName),
                attention,
                fate);
        }

        private static FormalThreeDProgressionSaveData Capture(object adapter)
        {
            MethodInfo method = adapter.GetType().GetMethod(
                "Capture",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType,
                Is.EqualTo(typeof(FormalThreeDProgressionSaveData)));
            return (FormalThreeDProgressionSaveData)method.Invoke(adapter, null);
        }

        private static bool TryPrepare(
            object adapter,
            FormalThreeDProgressionSaveData source,
            out object plan,
            out string error)
        {
            Type planType = RequireType(PlanTypeName);
            MethodInfo method = adapter.GetType().GetMethod(
                "TryPrepareRestore",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(FormalThreeDProgressionSaveData),
                    planType.MakeByRefType(),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { source, null, null };
            bool result = (bool)method.Invoke(adapter, arguments);
            plan = arguments[1];
            error = arguments[2] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static bool TryCommit(
            object adapter,
            object plan,
            out string error)
        {
            Type planType = RequireType(PlanTypeName);
            MethodInfo method = adapter.GetType().GetMethod(
                "TryCommitRestore",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    planType,
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { plan, null };
            bool result = (bool)method.Invoke(adapter, arguments);
            error = arguments[1] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static string RuntimeFingerprint(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            FormalAttentionSnapshot attentionState = attention.Capture();
            FormalFateSnapshot fateState = fate.Capture();
            return attentionState.Value + "|" + attentionState.Revision + "|" +
                string.Join(",", attentionState.History.Select(
                    value => value.ReasonId + ":" + value.StableEventKey)) +
                "|" + fateState.SelectedId + "|" + fateState.Level + "|" +
                fateState.Revision;
        }

        private static Type RequireType(string name)
        {
            string fullName = name.Split(',')[0].Trim();
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    fullName,
                    throwOnError: false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
    }
}
