using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Persistence;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class FormalRewindAnchorMetadataRuntimeTests
    {
        private const string Namespace = "WasteCity.Progression.";

        [Test]
        public void IDEA0020_PrepareIsZeroWriteAndReplacementIncrementsOrdinal()
        {
            object runtime = New("FormalRewindAnchorMetadataRuntime");
            object before = Capture(runtime);
            Assert.That(TryPrepare(runtime, "hash-a", 3L,
                out object first, out string error), Is.True, error);
            Assert.That(Capture(runtime), Is.SameAs(before));
            Assert.That(TryCommit(runtime, first, out error), Is.True, error);
            object firstEntry = Entries(Capture(runtime)).Single();
            Assert.That(Read<long>(firstEntry, "CreationOrdinal"), Is.EqualTo(1));
            Assert.That(Read<string>(firstEntry, "PayloadHashSha256"),
                Is.EqualTo("hash-a"));

            Assert.That(TryPrepare(runtime, "hash-b", 4L,
                out object second, out error), Is.True, error);
            Assert.That(TryCommit(runtime, second, out error), Is.True, error);
            object replacement = Entries(Capture(runtime)).Single();
            Assert.That(Read<long>(replacement, "CreationOrdinal"),
                Is.EqualTo(2));
            Assert.That(Read<string>(replacement, "PayloadHashSha256"),
                Is.EqualTo("hash-b"));
            Assert.That(Entries(Capture(runtime)), Has.Length.EqualTo(1));
        }

        [Test]
        public void IDEA0020_PlanIsOwnerRevisionBoundAndSingleUse()
        {
            object runtime = New("FormalRewindAnchorMetadataRuntime");
            object other = New("FormalRewindAnchorMetadataRuntime");
            Assert.That(TryPrepare(runtime, "hash-a", 1L,
                out object stale, out _), Is.True);
            Assert.That(TryPrepare(runtime, "hash-b", 2L,
                out object winner, out _), Is.True);
            Assert.That(TryCommit(runtime, winner, out _), Is.True);
            Assert.That(TryCommit(runtime, stale, out _), Is.False);

            Assert.That(TryPrepare(runtime, "hash-c", 3L,
                out object foreign, out _), Is.True);
            Assert.That(TryCommit(other, foreign, out _), Is.False);
            Assert.That(TryCommit(runtime, foreign, out _), Is.True);
            Assert.That(TryCommit(runtime, foreign, out _), Is.False);
        }

        [Test]
        public void IDEA0020_CaptureIsStableAndThreeHundredReadsAllocateZero()
        {
            var runtime = new FormalRewindAnchorMetadataRuntime();
            Assert.That(runtime.TryPrepareUpsert(
                "rewind-anchor.slot.0001",
                ".internal-rewind-anchor/slot-01.json",
                "session-a",
                "hash-stable",
                new FormalSaveCheckpointMetadata
                {
                    sequence = 8L,
                    reasonId = "rewind-anchor-created",
                    ruleTimeSeconds = 12f,
                    completedMilestoneIds = Array.Empty<string>(),
                },
                out FormalRewindAnchorMetadataUpsertPlan plan,
                out string error), Is.True, error);
            Assert.That(runtime.TryCommitUpsert(plan, out error),
                Is.True, error);
            FormalRewindAnchorMetadataSnapshot snapshot = runtime.Capture();
            for (var index = 0; index < 20; index++)
                Assert.That(runtime.Capture(), Is.SameAs(snapshot));

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
            {
                if (!ReferenceEquals(runtime.Capture(), snapshot))
                    Assert.Fail("Capture must return its cached snapshot.");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void IDEA0020_RestoreIsAtomicAndRejectsMoreThanOneSlot()
        {
            object source = New("FormalRewindAnchorMetadataRuntime");
            Assert.That(TryPrepare(source, "hash-source", 9L,
                out object plan, out _), Is.True);
            Assert.That(TryCommit(source, plan, out _), Is.True);
            object snapshot = Capture(source);
            object target = New("FormalRewindAnchorMetadataRuntime");

            Assert.That(TryRestore(target, snapshot, out string error),
                Is.True, error);
            Assert.That(Read<long>(Entries(Capture(target)).Single(),
                "CreationOrdinal"), Is.EqualTo(1));

            object invalid = NewSnapshotWithDuplicateEntries(snapshot);
            object before = Capture(target);
            Assert.That(TryRestore(target, invalid, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Capture(target), Is.SameAs(before));
        }

        [Test]
        public void IDEA0020_ClearPlanIsBoundIdempotentAndKeepsOrdinalHighWater()
        {
            object runtime = New("FormalRewindAnchorMetadataRuntime");
            Assert.That(TryPrepare(runtime, "hash-clear", 10L,
                out object upsert, out _), Is.True);
            Assert.That(TryCommit(runtime, upsert, out _), Is.True);
            object before = Capture(runtime);
            ulong revision = Read<ulong>(before, "Revision");
            long nextOrdinal = Read<long>(before, "NextCreationOrdinal");

            Assert.That(TryPrepareClear(runtime, out object clear, out _),
                Is.True);
            Assert.That(Capture(runtime), Is.SameAs(before));
            Assert.That(TryCommitClear(runtime, clear, out string error),
                Is.True, error);
            object cleared = Capture(runtime);
            Assert.That(Entries(cleared), Is.Empty);
            Assert.That(Read<long>(cleared, "NextCreationOrdinal"),
                Is.EqualTo(nextOrdinal));
            Assert.That(Read<ulong>(cleared, "Revision"),
                Is.EqualTo(revision + 1UL));
            Assert.That(TryCommitClear(runtime, clear, out _), Is.False);

            object other = New("FormalRewindAnchorMetadataRuntime");
            Assert.That(TryPrepareClear(runtime, out object foreign, out _),
                Is.True);
            Assert.That(TryCommitClear(other, foreign, out _), Is.False);
            ulong emptyRevision = Read<ulong>(Capture(runtime), "Revision");
            Assert.That(TryCommitClear(runtime, foreign, out _), Is.True);
            Assert.That(Read<ulong>(Capture(runtime), "Revision"),
                Is.EqualTo(emptyRevision),
                "Clearing an already empty slot is idempotent.");
        }

        private static bool TryPrepare(
            object runtime,
            string hash,
            long checkpointSequence,
            out object plan,
            out string error)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryPrepareUpsert",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            object[] arguments =
            {
                "rewind-anchor.slot.0001",
                ".internal-rewind-anchor/slot-01.json",
                "session-a",
                hash,
                new FormalSaveCheckpointMetadata
                {
                    sequence = checkpointSequence,
                    reasonId = "rewind-anchor-created",
                    ruleTimeSeconds = 12f,
                    completedMilestoneIds = Array.Empty<string>(),
                },
                null,
                null,
            };
            bool result = (bool)method.Invoke(runtime, arguments);
            plan = arguments[5];
            error = arguments[6] as string;
            return result;
        }

        private static bool TryCommit(
            object runtime,
            object plan,
            out string error)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryCommitUpsert",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { plan, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static bool TryRestore(
            object runtime,
            object snapshot,
            out string error)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryRestore",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { snapshot, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static bool TryPrepareClear(
            object runtime,
            out object plan,
            out string error)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryPrepareClear",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { null, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            plan = arguments[0];
            error = arguments[1] as string;
            return result;
        }

        private static bool TryCommitClear(
            object runtime,
            object plan,
            out string error)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryCommitClear",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { plan, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static object Capture(object runtime)
        {
            return runtime.GetType().GetMethod("Capture")?.Invoke(runtime, null);
        }

        private static object[] Entries(object snapshot)
        {
            return ((IEnumerable)Read<object>(snapshot, "Entries"))
                .Cast<object>().ToArray();
        }

        private static object NewSnapshotWithDuplicateEntries(object source)
        {
            Type type = Require("FormalRewindAnchorMetadataSnapshot");
            object entry = Entries(source).Single();
            ConstructorInfo constructor = type.GetConstructors().Single();
            ParameterInfo[] parameters = constructor.GetParameters();
            var entries = Array.CreateInstance(entry.GetType(), 2);
            entries.SetValue(entry, 0);
            entries.SetValue(entry, 1);
            object[] arguments = parameters.Length == 3
                ? new object[]
                {
                    Read<ulong>(source, "Revision"),
                    Read<long>(source, "NextCreationOrdinal"),
                    entries,
                }
                : new object[] { entries };
            return constructor.Invoke(arguments);
        }

        private static object New(string name) =>
            Activator.CreateInstance(Require(name));

        private static Type Require(string name)
        {
            Type type = typeof(FormalSaveEnvelope).Assembly.GetType(
                Namespace + name,
                false);
            Assert.That(type, Is.Not.Null, Namespace + name);
            return type;
        }

        private static T Read<T>(object owner, string property)
        {
            Assert.That(owner, Is.Not.Null, property);
            PropertyInfo info = owner.GetType().GetProperty(
                property,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, property);
            return (T)info.GetValue(owner);
        }
    }
}
