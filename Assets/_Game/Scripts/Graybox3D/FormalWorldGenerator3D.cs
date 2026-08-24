using System;
using System.Collections.Generic;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public static class FormalWorldGenerator3D
    {
        private const int TerrainWarpXChannel = 101;
        private const int TerrainWarpYChannel = 102;

        public static WorldMapModel Generate(int seedValue)
        {
            ValidateCatalog();
            var seed = new WorldSeed(seedValue);
            TerrainKind[,] terrain = GenerateTerrain(seed);
            WorldTraversalKind[,] traversal = GenerateTraversal();
            var resourceIds = new string[
                FormalWorldGenerationCatalog3D.Width,
                FormalWorldGenerationCatalog3D.Height];
            var resourceAmounts = new int[
                FormalWorldGenerationCatalog3D.Width,
                FormalWorldGenerationCatalog3D.Height];

            PlaceResources(
                traversal,
                resourceIds,
                resourceAmounts);
            var cells = new WorldCell[
                FormalWorldGenerationCatalog3D.Width,
                FormalWorldGenerationCatalog3D.Height];
            for (var x = 0; x < cells.GetLength(0); x++)
            for (var y = 0; y < cells.GetLength(1); y++)
            {
                cells[x, y] = new WorldCell(
                    terrain[x, y],
                    resourceIds[x, y],
                    resourceAmounts[x, y],
                    traversal[x, y]);
            }
            return new WorldMapModel(cells);
        }

        private static TerrainKind[,] GenerateTerrain(WorldSeed seed)
        {
            int width = FormalWorldGenerationCatalog3D.Width;
            int height = FormalWorldGenerationCatalog3D.Height;
            int macroSize = FormalWorldGenerationCatalog3D.MacroCellSize;
            var terrain = new TerrainKind[width, height];
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                int macroX = x / macroSize;
                int macroY = y / macroSize;
                int warpedX = Clamp(
                    x + SignedWarp(seed.Sample(
                        macroX,
                        macroY,
                        TerrainWarpXChannel)),
                    0,
                    width - 1);
                int warpedY = Clamp(
                    y + SignedWarp(seed.Sample(
                        macroX,
                        macroY,
                        TerrainWarpYChannel)),
                    0,
                    height - 1);
                terrain[x, y] = FormalWorldGenerationCatalog3D.MacroTerrain[
                    warpedY / macroSize * (width / macroSize) +
                    warpedX / macroSize];
            }

            for (var pass = 0;
                 pass < FormalWorldGenerationCatalog3D.TerrainCleanupPasses;
                 pass++)
                terrain = CleanupTerrain(terrain);
            ForceStartTerrain(terrain);
            return terrain;
        }

        private static TerrainKind[,] CleanupTerrain(TerrainKind[,] source)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            var result = (TerrainKind[,])source.Clone();
            var counts = new int[4];
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                Array.Clear(counts, 0, counts.Length);
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    if (offsetX == 0 && offsetY == 0)
                        continue;
                    int neighborX = x + offsetX;
                    int neighborY = y + offsetY;
                    if (neighborX < 0 || neighborY < 0 ||
                        neighborX >= width || neighborY >= height)
                        continue;
                    counts[(int)source[neighborX, neighborY]]++;
                }
                int selected = (int)source[x, y];
                for (var candidate = 0; candidate < counts.Length; candidate++)
                {
                    if (counts[candidate] >= 5 &&
                        counts[candidate] > counts[selected])
                        selected = candidate;
                }
                result[x, y] = (TerrainKind)selected;
            }
            return result;
        }

        private static void ForceStartTerrain(TerrainKind[,] terrain)
        {
            int radius = FormalWorldGenerationCatalog3D.StartProtectionRadius;
            for (int x = FormalWorldGenerationCatalog3D.StartCellX - radius;
                 x <= FormalWorldGenerationCatalog3D.StartCellX + radius;
                 x++)
            for (int y = FormalWorldGenerationCatalog3D.StartCellY - radius;
                 y <= FormalWorldGenerationCatalog3D.StartCellY + radius;
                 y++)
                terrain[x, y] = TerrainKind.Wasteland;
        }

        private static WorldTraversalKind[,] GenerateTraversal()
        {
            int width = FormalWorldGenerationCatalog3D.Width;
            int height = FormalWorldGenerationCatalog3D.Height;
            var traversal = new WorldTraversalKind[width, height];
            IReadOnlyList<FormalTraversalRegionSpec3D> regions =
                FormalWorldGenerationCatalog3D.TraversalRegions;
            for (var index = 0; index < regions.Count; index++)
            {
                FormalTraversalRegionSpec3D region = regions[index];
                for (var x = region.MinimumX;
                     x < region.MinimumX + region.Width;
                     x++)
                for (var y = region.MinimumY;
                     y < region.MinimumY + region.Height;
                     y++)
                    traversal[x, y] = region.Traversal;
            }

            ClearStartProtection(traversal);
            IReadOnlyList<FormalWorldCorridorSpec3D> corridors =
                FormalWorldGenerationCatalog3D.Corridors;
            for (var index = 0; index < corridors.Count; index++)
                ClearCorridor(traversal, corridors[index]);
            return traversal;
        }

        private static void ClearStartProtection(
            WorldTraversalKind[,] traversal)
        {
            int radius = FormalWorldGenerationCatalog3D.StartProtectionRadius;
            for (int x = FormalWorldGenerationCatalog3D.StartCellX - radius;
                 x <= FormalWorldGenerationCatalog3D.StartCellX + radius;
                 x++)
            for (int y = FormalWorldGenerationCatalog3D.StartCellY - radius;
                 y <= FormalWorldGenerationCatalog3D.StartCellY + radius;
                 y++)
                traversal[x, y] = WorldTraversalKind.Open;
        }

        private static void ClearCorridor(
            WorldTraversalKind[,] traversal,
            FormalWorldCorridorSpec3D corridor)
        {
            for (var segment = 1; segment < corridor.Points.Count; segment++)
            {
                FormalWorldCellPoint3D from = corridor.Points[segment - 1];
                FormalWorldCellPoint3D to = corridor.Points[segment];
                if (from.Y == to.Y)
                {
                    for (int x = Math.Min(from.X, to.X);
                         x <= Math.Max(from.X, to.X);
                         x++)
                    for (int y = from.Y - 1; y <= from.Y + 1; y++)
                        traversal[x, y] = WorldTraversalKind.Open;
                }
                else
                {
                    for (int y = Math.Min(from.Y, to.Y);
                         y <= Math.Max(from.Y, to.Y);
                         y++)
                    for (int x = from.X - 1; x <= from.X + 1; x++)
                        traversal[x, y] = WorldTraversalKind.Open;
                }
            }
        }

        private static void PlaceResources(
            WorldTraversalKind[,] traversal,
            string[,] resourceIds,
            int[,] resourceAmounts)
        {
            IReadOnlyList<FormalResourceNodeSpec3D> nodes =
                FormalWorldGenerationCatalog3D.ResourceNodes;
            for (var index = 0; index < nodes.Count; index++)
            {
                FormalResourceNodeSpec3D node = nodes[index];
                if (node.X < 0 || node.Y < 0 ||
                    node.X >= resourceIds.GetLength(0) ||
                    node.Y >= resourceIds.GetLength(1) ||
                    node.Amount <= 0 ||
                    traversal[node.X, node.Y] != WorldTraversalKind.Open ||
                    IsStartProtected(node.X, node.Y) ||
                    resourceIds[node.X, node.Y] != null)
                {
                    throw new InvalidOperationException(
                        "Formal resource-node configuration is invalid: " +
                        node.StableId);
                }
                resourceIds[node.X, node.Y] = node.ResourceId;
                resourceAmounts[node.X, node.Y] = node.Amount;
            }
            if (nodes.Count !=
                FormalWorldGenerationCatalog3D.ExpectedResourceNodeCount)
            {
                throw new InvalidOperationException(
                    "Formal resource-node total differs from the catalog.");
            }
        }

        private static bool IsStartProtected(int x, int y)
        {
            return Math.Max(
                       Math.Abs(
                           x - FormalWorldGenerationCatalog3D.StartCellX),
                       Math.Abs(
                           y - FormalWorldGenerationCatalog3D.StartCellY)) <=
                   FormalWorldGenerationCatalog3D.StartProtectionRadius;
        }

        private static int SignedWarp(int sample)
        {
            int amplitude =
                FormalWorldGenerationCatalog3D.BoundaryWarpAmplitude;
            return sample % (amplitude * 2 + 1) - amplitude;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum
                ? minimum
                : value > maximum
                    ? maximum
                    : value;
        }

        private static void ValidateCatalog()
        {
            int macroWidth =
                FormalWorldGenerationCatalog3D.Width /
                FormalWorldGenerationCatalog3D.MacroCellSize;
            int macroHeight =
                FormalWorldGenerationCatalog3D.Height /
                FormalWorldGenerationCatalog3D.MacroCellSize;
            if (FormalWorldGenerationCatalog3D.MacroTerrain.Count !=
                macroWidth * macroHeight)
                throw new InvalidOperationException(
                    "Formal macro-terrain dimensions are invalid.");
            if (FormalWorldGenerationCatalog3D.ResourceNodes.Count !=
                FormalWorldGenerationCatalog3D.ExpectedResourceNodeCount)
                throw new InvalidOperationException(
                    "Formal resource-node catalog is invalid.");
        }
    }
}
