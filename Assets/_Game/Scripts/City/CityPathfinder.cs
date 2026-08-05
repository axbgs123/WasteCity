using System;
using System.Collections.Generic;
using WasteCity.World;

namespace WasteCity.City
{
    public readonly struct WorldGridPoint
    {
        public int X { get; }
        public int Y { get; }

        public WorldGridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public static class CityPathfinder
    {
        private static readonly int[] NeighborX = { 1, -1, 0, 0 };
        private static readonly int[] NeighborY = { 0, 0, 1, -1 };

        public static bool TryFindPath(
            WorldMapModel map,
            int startX,
            int startY,
            int destinationX,
            int destinationY,
            out WorldGridPoint[] path)
        {
            path = Array.Empty<WorldGridPoint>();
            if (!IsValidPassable(map, startX, startY) ||
                !IsValidPassable(map, destinationX, destinationY))
                return false;
            if (startX == destinationX && startY == destinationY) return true;

            var bestCost = new float[map.Width, map.Height];
            var parentX = new int[map.Width, map.Height];
            var parentY = new int[map.Width, map.Height];
            var open = new bool[map.Width, map.Height];
            var closed = new bool[map.Width, map.Height];
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    bestCost[x, y] = float.PositiveInfinity;
                    parentX[x, y] = -1;
                    parentY[x, y] = -1;
                }

            bestCost[startX, startY] = 0f;
            open[startX, startY] = true;

            while (TrySelectNext(
                       map,
                       open,
                       closed,
                       bestCost,
                       destinationX,
                       destinationY,
                       out int currentX,
                       out int currentY))
            {
                if (currentX == destinationX && currentY == destinationY)
                {
                    path = Reconstruct(
                        startX,
                        startY,
                        destinationX,
                        destinationY,
                        parentX,
                        parentY);
                    return true;
                }

                open[currentX, currentY] = false;
                closed[currentX, currentY] = true;
                for (int index = 0; index < NeighborX.Length; index++)
                {
                    int nextX = currentX + NeighborX[index];
                    int nextY = currentY + NeighborY[index];
                    if (!IsValidPassable(map, nextX, nextY) || closed[nextX, nextY])
                        continue;

                    float tentative = bestCost[currentX, currentY] +
                                      1f / CityTerrainRules.SpeedMultiplier(
                                          map.Get(nextX, nextY));
                    if (tentative >= bestCost[nextX, nextY]) continue;
                    bestCost[nextX, nextY] = tentative;
                    parentX[nextX, nextY] = currentX;
                    parentY[nextX, nextY] = currentY;
                    open[nextX, nextY] = true;
                }
            }

            return false;
        }

        private static bool TrySelectNext(
            WorldMapModel map,
            bool[,] open,
            bool[,] closed,
            float[,] bestCost,
            int destinationX,
            int destinationY,
            out int selectedX,
            out int selectedY)
        {
            selectedX = -1;
            selectedY = -1;
            float selectedScore = float.PositiveInfinity;
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    if (!open[x, y] || closed[x, y]) continue;
                    float score = bestCost[x, y] +
                                  Math.Abs(destinationX - x) +
                                  Math.Abs(destinationY - y);
                    if (score >= selectedScore) continue;
                    selectedScore = score;
                    selectedX = x;
                    selectedY = y;
                }
            return selectedX >= 0;
        }

        private static WorldGridPoint[] Reconstruct(
            int startX,
            int startY,
            int destinationX,
            int destinationY,
            int[,] parentX,
            int[,] parentY)
        {
            var reverse = new List<WorldGridPoint>();
            int currentX = destinationX;
            int currentY = destinationY;
            while (currentX != startX || currentY != startY)
            {
                reverse.Add(new WorldGridPoint(currentX, currentY));
                int previousX = parentX[currentX, currentY];
                int previousY = parentY[currentX, currentY];
                if (previousX < 0 || previousY < 0) return Array.Empty<WorldGridPoint>();
                currentX = previousX;
                currentY = previousY;
            }
            reverse.Reverse();
            return reverse.ToArray();
        }

        private static bool IsValidPassable(WorldMapModel map, int x, int y)
        {
            return map != null &&
                   x >= 0 &&
                   y >= 0 &&
                   x < map.Width &&
                   y < map.Height &&
                   CityTerrainRules.IsPassable(map.Get(x, y));
        }
    }
}
