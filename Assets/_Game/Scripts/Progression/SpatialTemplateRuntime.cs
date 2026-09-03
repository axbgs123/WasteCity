using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Progression
{
    public sealed class SpatialTemplateCell
    {
        public SpatialTemplateCell(
            int x,
            int y,
            string buildingDefinitionId,
            int rotationQuarterTurns)
        {
            X = x;
            Y = y;
            BuildingDefinitionId = buildingDefinitionId ?? string.Empty;
            RotationQuarterTurns = rotationQuarterTurns;
        }

        public int X { get; }
        public int Y { get; }
        public string BuildingDefinitionId { get; }
        public int RotationQuarterTurns { get; }

        internal SpatialTemplateCell Copy()
        {
            return new SpatialTemplateCell(
                X,
                Y,
                BuildingDefinitionId,
                RotationQuarterTurns);
        }
    }

    public sealed class SpatialTemplateDefinition
    {
        private readonly ReadOnlyCollection<SpatialTemplateCell> cells;

        public SpatialTemplateDefinition(
            string id,
            SpatialTemplateCell[] cells)
        {
            Id = id ?? string.Empty;
            Width = SpatialTemplateRuntime.TemplateSize;
            Height = SpatialTemplateRuntime.TemplateSize;
            cells = cells ?? Array.Empty<SpatialTemplateCell>();
            var copy = new SpatialTemplateCell[cells.Length];
            for (var index = 0; index < cells.Length; index++)
                copy[index] = cells[index]?.Copy();
            this.cells = Array.AsReadOnly(copy);
        }

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<SpatialTemplateCell> Cells => cells;

        internal SpatialTemplateDefinition Copy()
        {
            var copy = new SpatialTemplateCell[cells.Count];
            for (var index = 0; index < cells.Count; index++)
                copy[index] = cells[index].Copy();
            return new SpatialTemplateDefinition(Id, copy);
        }
    }

    public sealed class SpatialTemplateSnapshot
    {
        private readonly ReadOnlyCollection<SpatialTemplateDefinition>
            templates;

        public SpatialTemplateSnapshot(
            ulong revision,
            SpatialTemplateDefinition[] templates)
        {
            Revision = revision;
            templates = templates ??
                Array.Empty<SpatialTemplateDefinition>();
            var copy = new SpatialTemplateDefinition[templates.Length];
            for (var index = 0; index < templates.Length; index++)
                copy[index] = templates[index]?.Copy();
            this.templates = Array.AsReadOnly(copy);
        }

        public ulong Revision { get; }
        public IReadOnlyList<SpatialTemplateDefinition> Templates => templates;
    }

    public sealed class SpatialTemplateRecordPlan
    {
        internal SpatialTemplateRecordPlan(
            SpatialTemplateRuntime owner,
            ulong expectedRevision,
            SpatialTemplateDefinition candidate)
        {
            Owner = owner;
            ExpectedRevision = expectedRevision;
            Candidate = candidate;
        }

        internal SpatialTemplateRuntime Owner { get; }
        internal ulong ExpectedRevision { get; }
        internal SpatialTemplateDefinition Candidate { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class SpatialTemplateRuntime
    {
        public const int TemplateSize = 3;
        public const int TemplateRadius = TemplateSize / 2;

        private readonly List<SpatialTemplateDefinition> templates =
            new List<SpatialTemplateDefinition>();
        private ulong revision;
        private SpatialTemplateSnapshot cachedSnapshot;

        public SpatialTemplateRuntime()
        {
            RebuildSnapshot();
        }

        public ulong Revision => revision;

        public bool TryPrepareRecord(
            string templateId,
            IEnumerable<SpatialTemplateCell> cells,
            out SpatialTemplateRecordPlan plan,
            out string error)
        {
            plan = null;
            if (string.IsNullOrWhiteSpace(templateId) || cells == null)
            {
                error = "Template ID and cells are required.";
                return false;
            }

            var prepared = new List<SpatialTemplateCell>();
            var occupied = new HashSet<int>();
            foreach (SpatialTemplateCell cell in cells)
            {
                if (cell == null || !IsCoordinateInBounds(cell.X) ||
                    !IsCoordinateInBounds(cell.Y) ||
                    string.IsNullOrWhiteSpace(cell.BuildingDefinitionId) ||
                    cell.RotationQuarterTurns < 0 ||
                    cell.RotationQuarterTurns > 3 ||
                    !occupied.Add(CellKey(cell.X, cell.Y)))
                {
                    error = "Template cells are invalid or overlap.";
                    return false;
                }
                prepared.Add(cell.Copy());
            }
            if (prepared.Count == 0)
            {
                error = "A template must contain at least one cell.";
                return false;
            }
            prepared.Sort(CompareCells);
            plan = new SpatialTemplateRecordPlan(
                this,
                revision,
                new SpatialTemplateDefinition(templateId, prepared.ToArray()));
            error = string.Empty;
            return true;
        }

        public bool TryCommit(
            SpatialTemplateRecordPlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.Consumed || plan.ExpectedRevision != revision)
            {
                error = "Template plan is missing, stale, foreign, or consumed.";
                return false;
            }

            plan.Consumed = true;
            for (var index = templates.Count - 1; index >= 0; index--)
            {
                if (string.Equals(
                    templates[index].Id,
                    plan.Candidate.Id,
                    StringComparison.Ordinal))
                {
                    templates.RemoveAt(index);
                }
            }
            templates.Add(plan.Candidate.Copy());
            templates.Sort((left, right) => string.CompareOrdinal(
                left.Id,
                right.Id));
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public SpatialTemplateSnapshot Capture() => cachedSnapshot;

        public bool TryRestore(
            SpatialTemplateSnapshot snapshot,
            out string error)
        {
            if (snapshot == null)
            {
                error = "Spatial template snapshot is required.";
                return false;
            }

            var restored = new List<SpatialTemplateDefinition>();
            var templateIds = new HashSet<string>(StringComparer.Ordinal);
            for (var templateIndex = 0;
                 templateIndex < snapshot.Templates.Count;
                 templateIndex++)
            {
                SpatialTemplateDefinition template =
                    snapshot.Templates[templateIndex];
                if (template == null || string.IsNullOrWhiteSpace(template.Id) ||
                    template.Width != TemplateSize ||
                    template.Height != TemplateSize ||
                    template.Cells.Count == 0 ||
                    !templateIds.Add(template.Id))
                {
                    error = "Restored templates must be unique and non-empty.";
                    return false;
                }

                var cells = new List<SpatialTemplateCell>();
                var occupied = new HashSet<int>();
                for (var cellIndex = 0;
                     cellIndex < template.Cells.Count;
                     cellIndex++)
                {
                    SpatialTemplateCell cell = template.Cells[cellIndex];
                    if (cell == null || !IsCoordinateInBounds(cell.X) ||
                        !IsCoordinateInBounds(cell.Y) ||
                        string.IsNullOrWhiteSpace(cell.BuildingDefinitionId) ||
                        cell.RotationQuarterTurns < 0 ||
                        cell.RotationQuarterTurns > 3 ||
                        !occupied.Add(CellKey(cell.X, cell.Y)))
                    {
                        error = "Restored template cells are invalid or overlap.";
                        return false;
                    }
                    cells.Add(cell.Copy());
                }
                cells.Sort(CompareCells);
                restored.Add(new SpatialTemplateDefinition(
                    template.Id,
                    cells.ToArray()));
            }

            if ((restored.Count == 0) != (snapshot.Revision == 0))
            {
                error = "Template revision is inconsistent with saved data.";
                return false;
            }

            restored.Sort((left, right) => string.CompareOrdinal(
                left.Id,
                right.Id));
            templates.Clear();
            templates.AddRange(restored);
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new SpatialTemplateSnapshot(
                revision,
                templates.ToArray());
        }

        private static int CompareCells(
            SpatialTemplateCell left,
            SpatialTemplateCell right)
        {
            int byY = left.Y.CompareTo(right.Y);
            return byY != 0 ? byY : left.X.CompareTo(right.X);
        }

        private static bool IsCoordinateInBounds(int coordinate)
        {
            return coordinate >= -TemplateRadius &&
                   coordinate <= TemplateRadius;
        }

        private static int CellKey(int x, int y)
        {
            return (y + TemplateRadius) * TemplateSize +
                   x + TemplateRadius;
        }
    }
}
