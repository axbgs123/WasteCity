using UnityEngine;
using WasteCity.Economy;

namespace WasteCity.World
{
    public sealed class PlaceholderWorldView : MonoBehaviour
    {
        [SerializeField] private int width = 32;
        [SerializeField] private int height = 24;
        public int GeneratedTileCount { get; private set; }
        public WorldMapModel Model { get; private set; }
        private SpriteRenderer[,] tileRenderers;
        private static Sprite square;

        public void Generate(WorldSeed seed)
        {
            if (GeneratedTileCount > 0) return;
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            Model = new WorldMapModel(width, height, seed);
            tileRenderers = new SpriteRenderer[width, height];
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
            {
                var tile = new GameObject($"Tile_{x}_{y}"); tile.transform.SetParent(transform);
                tile.transform.localPosition = new Vector3(x - width * 0.5f, y - height * 0.5f, 0f);
                var renderer = tile.AddComponent<SpriteRenderer>(); renderer.sprite = square;
                renderer.color = TerrainColor(Model.Get(x, y).Terrain);
                renderer.sortingOrder = 0; tile.transform.localScale = Vector3.one * 0.96f;
                tileRenderers[x, y] = renderer;
                if (Model.Get(x, y).HasResource) CreateResourceMarker(tile.transform, Model.Get(x, y).ResourceId);
                GeneratedTileCount++;
            }
        }

        public void RevealAroundWorld(Vector2 world, int radius)
        {
            if (Model == null) return;
            int centerX = Mathf.FloorToInt(world.x + width * 0.5f); int centerY = Mathf.FloorToInt(world.y + height * 0.5f);
            Model.Reveal(centerX, centerY, radius);
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
                tileRenderers[x, y].color = Model.IsRevealed(x, y) ? TerrainColor(Model.Get(x, y).Terrain) : new Color(0.025f, 0.03f, 0.035f);
        }
        public void Restore(int[] amounts, bool[] visibility) { if(Model!=null&&Model.Restore(amounts,visibility))RefreshVisibility(); }
        public void RefreshVisibility(){if(Model==null||tileRenderers==null)return;for(int x=0;x<width;x++)for(int y=0;y<height;y++)tileRenderers[x,y].color=Model.IsRevealed(x,y)?TerrainColor(Model.Get(x,y).Terrain):new Color(0.025f,0.03f,0.035f);}

        private static Color TerrainColor(TerrainKind terrain)
        {
            if (terrain == TerrainKind.Crystal) return new Color(0.16f, 0.3f, 0.34f);
            if (terrain == TerrainKind.Rocky) return new Color(0.31f, 0.24f, 0.16f);
            if (terrain == TerrainKind.Wetland) return new Color(0.13f, 0.28f, 0.22f);
            return new Color(0.2f, 0.22f, 0.18f);
        }

        private static void CreateResourceMarker(Transform parent, string id)
        {
            var marker = new GameObject("ResourcePlaceholder"); marker.transform.SetParent(parent); marker.transform.localPosition = new Vector3(0f, 0f, -0.1f); marker.transform.localScale = Vector3.one * 0.35f;
            var renderer = marker.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sortingOrder = 2;
            renderer.color = id == ResourceIds.EnergyCrystal ? Color.cyan : id == ResourceIds.Water ? Color.blue : id == ResourceIds.Biomass ? Color.green : id == ResourceIds.Stone ? Color.gray : new Color(0.75f, 0.45f, 0.2f);
        }
    }
}
