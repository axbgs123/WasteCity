using NUnit.Framework;
using UnityEngine;
using WasteCity.Presentation;

namespace WasteCity.Tests
{
    public sealed class VisualSlotTests
    {
        [Test] public void EmptyLibraryReturnsNoDefinition()
        { var library=ScriptableObject.CreateInstance<VisualLibrary>();Assert.That(library.Resolve("missing"),Is.Null);Object.DestroyImmediate(library); }
    }
}
