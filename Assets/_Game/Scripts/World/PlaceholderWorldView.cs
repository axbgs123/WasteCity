using UnityEngine;

namespace WasteCity.World
{
    public sealed class PlaceholderWorldView : MonoBehaviour
    {
        [SerializeField] private int width = 32;
        [SerializeField] private int height = 24;
        public int GeneratedTileCount { get; private set; }
        private static Sprite square;

        public void Generate(WorldSeed seed)
        {
            if (GeneratedTileCount > 0) return;
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
            {
                var tile = new GameObject($"Tile_{x}_{y}"); tile.transform.SetParent(transform);
                tile.transform.localPosition = new Vector3(x - width * 0.5f, y - height * 0.5f, 0f);
                var renderer = tile.AddComponent<SpriteRenderer>(); renderer.sprite = square;
                int sample = seed.Sample(x, y) % 100;
                renderer.color = sample < 12 ? new Color(0.16f, 0.3f, 0.34f) : sample < 32 ? new Color(0.31f, 0.24f, 0.16f) : new Color(0.2f, 0.22f, 0.18f);
                renderer.sortingOrder = 0; tile.transform.localScale = Vector3.one * 0.96f;
                GeneratedTileCount++;
            }
        }
    }
}
