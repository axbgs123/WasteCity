using UnityEngine;

namespace WasteCity.Presentation
{
    public sealed class VisualLibraryProvider : MonoBehaviour
    {
        [SerializeField] private VisualLibrary library;
        public VisualLibrary Library => library;
    }
}
