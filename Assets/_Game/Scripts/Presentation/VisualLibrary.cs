using UnityEngine;

namespace WasteCity.Presentation
{
    [CreateAssetMenu(menuName = "Waste City/Presentation/Visual Library")]
    public sealed class VisualLibrary : ScriptableObject
    {
        [SerializeField] private VisualDefinition[] definitions = System.Array.Empty<VisualDefinition>();
        public VisualDefinition Resolve(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            foreach (var definition in definitions) if (definition != null && definition.Id.Value == stableId) return definition;
            return null;
        }
    }
}
