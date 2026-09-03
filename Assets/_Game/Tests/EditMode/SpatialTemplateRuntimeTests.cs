using System.Linq;
using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class SpatialTemplateRuntimeTests
    {
        [Test]
        public void IDEA0028_ThreeByThreeTemplateUsesPrepareCommitAtomically()
        {
            var runtime = new SpatialTemplateRuntime();
            var cells = new[]
            {
                new SpatialTemplateCell(-1, -1, "core.building.mining-station", 0),
                new SpatialTemplateCell(1, 1, "core.building.warehouse", 3),
            };

            Assert.That(runtime.TryPrepareRecord(
                "player.template.first", cells, out SpatialTemplateRecordPlan plan,
                out string error), Is.True, error);
            Assert.That(runtime.Capture().Templates, Is.Empty,
                "Preparing a plan must not mutate the owner.");
            Assert.That(runtime.TryCommit(plan, out error), Is.True, error);
            Assert.That(runtime.TryCommit(plan, out _), Is.False,
                "A consumed plan cannot be replayed.");

            SpatialTemplateDefinition saved = runtime.Capture().Templates.Single();
            Assert.That(saved.Width, Is.EqualTo(3));
            Assert.That(saved.Height, Is.EqualTo(3));
            Assert.That(saved.Cells.Select(value => (value.X, value.Y)),
                Is.EqualTo(new[] { (-1, -1), (1, 1) }));
        }

        [Test]
        public void IDEA0028_InvalidOrStalePlansLeaveOwnerUnchanged()
        {
            var runtime = new SpatialTemplateRuntime();
            var duplicate = new[]
            {
                new SpatialTemplateCell(0, 0, "core.building.warehouse", 0),
                new SpatialTemplateCell(0, 0, "core.building.housing", 0),
            };
            Assert.That(runtime.TryPrepareRecord(
                "player.template.bad", duplicate, out _, out _), Is.False);
            Assert.That(runtime.Capture().Templates, Is.Empty);

            var valid = new[]
            {
                new SpatialTemplateCell(0, 0, "core.building.warehouse", 0),
            };
            Assert.That(runtime.TryPrepareRecord(
                "player.template.a", valid, out SpatialTemplateRecordPlan a,
                out _), Is.True);
            Assert.That(runtime.TryPrepareRecord(
                "player.template.b", valid, out SpatialTemplateRecordPlan b,
                out _), Is.True);
            Assert.That(runtime.TryCommit(a, out _), Is.True);
            SpatialTemplateSnapshot committed = runtime.Capture();
            Assert.That(runtime.TryCommit(b, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(committed));
        }

        [Test]
        public void IDEA0028_RestoreRoundTripsTemplatesAndRevision()
        {
            var source = new SpatialTemplateRuntime();
            Assert.That(source.TryPrepareRecord(
                "player.template.saved",
                new[]
                {
                    new SpatialTemplateCell(
                        1, -1, "core.building.warehouse", 3),
                    new SpatialTemplateCell(
                        -1, 1, "core.building.housing", 1),
                },
                out SpatialTemplateRecordPlan plan,
                out string error), Is.True, error);
            Assert.That(source.TryCommit(plan, out error), Is.True, error);

            var restored = new SpatialTemplateRuntime();
            Assert.That(restored.TryRestore(
                source.Capture(), out error), Is.True, error);
            Assert.That(restored.Capture().Revision,
                Is.EqualTo(source.Capture().Revision));
            Assert.That(restored.Capture().Templates.Single().Id,
                Is.EqualTo("player.template.saved"));
            Assert.That(restored.Capture().Templates.Single().Cells
                    .Select(value => (value.X, value.Y)),
                Is.EqualTo(new[] { (1, -1), (-1, 1) }));
        }

        [Test]
        public void IDEA0028_CoordinatesOutsideCenteredThreeByThreeAreRejected()
        {
            var runtime = new SpatialTemplateRuntime();

            Assert.That(runtime.TryPrepareRecord(
                "player.template.outside",
                new[]
                {
                    new SpatialTemplateCell(
                        2, 0, "core.building.warehouse", 0),
                },
                out _,
                out _), Is.False);
            Assert.That(runtime.Capture().Templates, Is.Empty);
        }

        [Test]
        public void IDEA0028_InvalidRestoreIsAtomic()
        {
            var runtime = new SpatialTemplateRuntime();
            SpatialTemplateSnapshot before = runtime.Capture();
            var invalid = new SpatialTemplateSnapshot(
                1,
                new[]
                {
                    new SpatialTemplateDefinition(
                        "player.template.invalid",
                        new[]
                        {
                            new SpatialTemplateCell(
                                0, 0, "core.building.warehouse", 0),
                            new SpatialTemplateCell(
                                0, 0, "core.building.housing", 0),
                        }),
                });

            Assert.That(runtime.TryRestore(invalid, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }
    }
}
