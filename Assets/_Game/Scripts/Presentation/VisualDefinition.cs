using UnityEngine;
using WasteCity.Content;

namespace WasteCity.Presentation
{
    [CreateAssetMenu(menuName = "Waste City/Presentation/Visual Definition")]
    public sealed class VisualDefinition : ScriptableObject
    {
        [SerializeField] private string stableId = "core.visual.placeholder";
        [SerializeField] private Sprite sprite;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Color fallbackColor = Color.white;
        public StableId Id => new StableId(stableId);
        public Sprite Sprite => sprite;
        public GameObject Prefab => prefab;
        public Color FallbackColor => fallbackColor;
    }
}
