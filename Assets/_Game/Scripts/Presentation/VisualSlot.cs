using UnityEngine;

namespace WasteCity.Presentation
{
    public sealed class VisualSlot : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private SpriteRenderer placeholderRenderer;
        [SerializeField] private Color fallbackColor = Color.white;
        private GameObject spawnedVisual;
        public string StableId => stableId;
        public bool UsingPrefab => spawnedVisual != null;
        private void Start() { if (!UsingPrefab && placeholderRenderer != null) Apply(FindLibrary()?.Resolve(stableId)); }
        public void Configure(string id, SpriteRenderer renderer, Color fallback)
        {
            stableId = id; placeholderRenderer = renderer; fallbackColor = fallback; Apply(FindLibrary()?.Resolve(stableId));
        }
        public void Apply(VisualDefinition definition)
        {
            if (spawnedVisual != null) { if (Application.isPlaying) Destroy(spawnedVisual); else DestroyImmediate(spawnedVisual); spawnedVisual = null; }
            if (definition != null && definition.Prefab != null)
            {
                spawnedVisual = Instantiate(definition.Prefab, transform); spawnedVisual.name = $"Visual_{stableId}"; spawnedVisual.transform.localPosition = Vector3.zero; placeholderRenderer.enabled = false; return;
            }
            placeholderRenderer.enabled = true; placeholderRenderer.sprite = definition != null && definition.Sprite != null ? definition.Sprite : placeholderRenderer.sprite; placeholderRenderer.color = definition != null ? definition.FallbackColor : fallbackColor;
        }
        private static VisualLibrary FindLibrary() { var provider = Object.FindObjectOfType<VisualLibraryProvider>(); return provider == null ? null : provider.Library; }
        public static VisualSlot Attach(GameObject target, string id, SpriteRenderer renderer, Color fallback)
        { var slot = target.GetComponent<VisualSlot>() ?? target.AddComponent<VisualSlot>(); slot.Configure(id, renderer, fallback); return slot; }
    }
}
