using NUnit.Framework;
using WasteCity.Legacy;

namespace WasteCity.Tests
{
    public sealed class SpatialTemplateTests
    {
        [Test] public void TemplateRecordsOnlyValidThreeByThreeOffsets(){var m=new SpatialTemplateModel();Assert.That(m.Record(new[]{new SpatialTemplateEntry{definitionId="a",dx=0,dy=2},new SpatialTemplateEntry{definitionId="b",dx=3,dy=0}}),Is.True);Assert.That(m.Entries.Count,Is.EqualTo(1));Assert.That(m.Entries[0].definitionId,Is.EqualTo("a"));}
        [Test] public void EmptyTemplateIsRejected(){var m=new SpatialTemplateModel();Assert.That(m.Record(System.Array.Empty<SpatialTemplateEntry>()),Is.False);Assert.That(m.HasTemplate,Is.False);}
        [Test] public void TemplateCaptureIsAnIndependentCopy(){var m=new SpatialTemplateModel();m.Record(new[]{new SpatialTemplateEntry{definitionId="a",dx=1,dy=1}});var copy=m.Capture();copy[0].definitionId="changed";Assert.That(m.Entries[0].definitionId,Is.EqualTo("a"));}
        [Test] public void TemplateCanBeRestoredFromSave(){var m=new SpatialTemplateModel();m.Restore(new[]{new SpatialTemplateEntry{definitionId="core.building.wall",dx=2,dy=2}});Assert.That(m.HasTemplate,Is.True);Assert.That(m.Entries[0].dx,Is.EqualTo(2));}
    }
}
