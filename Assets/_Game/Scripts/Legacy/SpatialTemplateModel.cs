using System;
using System.Collections.Generic;
using System.Linq;

namespace WasteCity.Legacy
{
    [Serializable]
    public sealed class SpatialTemplateEntry { public string definitionId; public int dx, dy; }
    public sealed class SpatialTemplateModel
    {
        private SpatialTemplateEntry[] entries = Array.Empty<SpatialTemplateEntry>();
        public IReadOnlyList<SpatialTemplateEntry> Entries => entries;
        public bool HasTemplate => entries.Length > 0;
        public bool Record(IEnumerable<SpatialTemplateEntry> values)
        {
            if (values == null) return false; var copy = values.Where(value => value != null && !string.IsNullOrWhiteSpace(value.definitionId) && value.dx >= 0 && value.dy >= 0 && value.dx < 3 && value.dy < 3).Select(value => new SpatialTemplateEntry { definitionId = value.definitionId, dx = value.dx, dy = value.dy }).ToArray();
            if (copy.Length == 0) return false; entries = copy; return true;
        }
        public SpatialTemplateEntry[] Capture() => entries.Select(value => new SpatialTemplateEntry { definitionId = value.definitionId, dx = value.dx, dy = value.dy }).ToArray();
        public void Restore(SpatialTemplateEntry[] values) { entries = Array.Empty<SpatialTemplateEntry>(); Record(values); }
    }
}
