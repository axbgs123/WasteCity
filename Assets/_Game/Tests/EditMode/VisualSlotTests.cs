using NUnit.Framework;
using UnityEngine;
using WasteCity.Presentation;

namespace WasteCity.Tests
{
    public sealed class VisualSlotTests
    {
        [Test] public void MissingDefinitionKeepsConfiguredPlaceholder()
        { var item=new GameObject("visual-test");var renderer=item.AddComponent<SpriteRenderer>();var slot=VisualSlot.Attach(item,"core.test.visual",renderer,Color.magenta);Assert.That(slot.StableId,Is.EqualTo("core.test.visual"));Assert.That(renderer.enabled,Is.True);Assert.That(renderer.color,Is.EqualTo(Color.magenta));Object.DestroyImmediate(item); }
        [Test] public void EmptyLibraryReturnsNoDefinition()
        { var library=ScriptableObject.CreateInstance<VisualLibrary>();Assert.That(library.Resolve("missing"),Is.Null);Object.DestroyImmediate(library); }
    }
}
