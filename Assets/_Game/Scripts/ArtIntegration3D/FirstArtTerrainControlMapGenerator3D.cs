using System;
using UnityEngine;
using WasteCity.World;

namespace WasteCity.ArtIntegration3D
{
    public static class FirstArtTerrainControlMapGenerator3D
    {
        private const int PixelsPerCell = FirstArtTerrainProfile3D.DefaultControlPixelsPerCell;
        private const int MaximumEncodedLayers = 3;

        public static FirstArtTerrainControlMap3D Generate(
            WorldMapModel model,
            FirstArtTerrainProfile3D profile)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (!profile.TryValidateControlSettings(out string validationError))
                throw new ArgumentException(validationError, nameof(profile));
            if (profile.ControlPixelsPerCell != PixelsPerCell)
            {
                throw new ArgumentException(
                    "First-art terrain control maps require four pixels per cell.",
                    nameof(profile));
            }

            int[,] layers = ResolveLayers(model);
            int width = checked(model.Width * PixelsPerCell);
            int height = checked(model.Height * PixelsPerCell);
            var controlA = new byte[checked(width * height * 4)];
            var controlB = new byte[checked(width * height * 4)];
            var weights = new float[FirstArtTerrainCatalog3D.LayerCount];
            var keptLayers = new int[MaximumEncodedLayers];
            var encodedWeights =
                new int[FirstArtTerrainCatalog3D.LayerCount];
            var nearestSquaredDistances =
                new float[FirstArtTerrainCatalog3D.LayerCount];
            var nearestCellX =
                new int[FirstArtTerrainCatalog3D.LayerCount];
            var nearestCellY =
                new int[FirstArtTerrainCatalog3D.LayerCount];
            int candidateRadius = Mathf.CeilToInt(MaximumBlendWidth(profile)) + 1;

            for (int y = 0; y < height; y++)
            {
                float sampleY = (y + 0.5f) / PixelsPerCell - 0.5f;
                int ownerY = Mathf.Clamp(Mathf.FloorToInt(sampleY + 0.5f), 0, model.Height - 1);
                for (int x = 0; x < width; x++)
                {
                    float sampleX = (x + 0.5f) / PixelsPerCell - 0.5f;
                    int ownerX = Mathf.Clamp(Mathf.FloorToInt(sampleX + 0.5f), 0, model.Width - 1);
                    int ownerLayer = layers[ownerX, ownerY];
                    GenerateEncodedWeights(
                        model,
                        profile,
                        layers,
                        sampleX,
                        sampleY,
                        ownerX,
                        ownerY,
                        ownerLayer,
                        candidateRadius,
                        weights,
                        keptLayers,
                        encodedWeights,
                        nearestSquaredDistances,
                        nearestCellX,
                        nearestCellY);
                    Encode(encodedWeights, controlA, controlB, (y * width + x) * 4);
                }
            }

            return new FirstArtTerrainControlMap3D(width, height, controlA, controlB);
        }

        private static int[,] ResolveLayers(WorldMapModel model)
        {
            var layers = new int[model.Width, model.Height];
            for (int y = 0; y < model.Height; y++)
            for (int x = 0; x < model.Width; x++)
                layers[x, y] = (int)FirstArtTerrainCatalog3D.LayerOf(model.Get(x, y));
            return layers;
        }

        private static void GenerateEncodedWeights(
            WorldMapModel model,
            FirstArtTerrainProfile3D profile,
            int[,] layers,
            float sampleX,
            float sampleY,
            int ownerX,
            int ownerY,
            int ownerLayer,
            int candidateRadius,
            float[] weights,
            int[] keptLayers,
            int[] encodedWeights,
            float[] nearestSquaredDistances,
            int[] nearestCellX,
            int[] nearestCellY)
        {
            Array.Clear(weights, 0, weights.Length);
            for (int index = 0; index < keptLayers.Length; index++)
                keptLayers[index] = -1;
            Array.Clear(encodedWeights, 0, encodedWeights.Length);
            for (int layer = 0;
                 layer < FirstArtTerrainCatalog3D.LayerCount;
                 layer++)
            {
                nearestSquaredDistances[layer] = float.PositiveInfinity;
                nearestCellX[layer] = -1;
                nearestCellY[layer] = -1;
            }
            weights[ownerLayer] = 1f;
            int minX = Mathf.Max(0, ownerX - candidateRadius);
            int maxX = Mathf.Min(model.Width - 1, ownerX + candidateRadius);
            int minY = Mathf.Max(0, ownerY - candidateRadius);
            int maxY = Mathf.Min(model.Height - 1, ownerY + candidateRadius);
            for (int candidateY = minY; candidateY <= maxY; candidateY++)
            for (int candidateX = minX; candidateX <= maxX; candidateX++)
            {
                int candidateLayer = layers[candidateX, candidateY];
                if (candidateLayer == ownerLayer)
                    continue;

                float squaredDistance =
                    SquaredDistanceToCellRectangle(
                        sampleX,
                        sampleY,
                        candidateX,
                        candidateY);
                if (squaredDistance <
                    nearestSquaredDistances[candidateLayer])
                {
                    nearestSquaredDistances[candidateLayer] =
                        squaredDistance;
                    nearestCellX[candidateLayer] = candidateX;
                    nearestCellY[candidateLayer] = candidateY;
                }
            }

            for (int candidateLayer = 0;
                 candidateLayer < FirstArtTerrainCatalog3D.LayerCount;
                 candidateLayer++)
            {
                if (candidateLayer == ownerLayer ||
                    nearestCellX[candidateLayer] < 0)
                    continue;

                float blendWidth = profile.BlendWidth(
                    (FirstArtTerrainLayer3D)ownerLayer,
                    (FirstArtTerrainLayer3D)candidateLayer);
                float distance = DistanceToCellRectangle(
                    sampleX,
                    sampleY,
                    nearestCellX[candidateLayer],
                    nearestCellY[candidateLayer]);
                float candidateWeight = 1f - SmoothStep(
                    0f,
                    blendWidth,
                    distance + EdgeNoise(
                        sampleX,
                        sampleY,
                        candidateLayer));
                if (candidateWeight > weights[candidateLayer])
                    weights[candidateLayer] = candidateWeight;
            }

            KeepHighestPositiveLayers(weights, keptLayers);
            Quantize(weights, keptLayers, encodedWeights);
        }

        private static void KeepHighestPositiveLayers(
            float[] weights,
            int[] keptLayers)
        {
            for (int layer = 0; layer < weights.Length; layer++)
            {
                if (weights[layer] <= 0f)
                    continue;

                for (int keptIndex = 0; keptIndex < keptLayers.Length; keptIndex++)
                {
                    int currentLayer = keptLayers[keptIndex];
                    if (currentLayer < 0 || IsHigherPriority(weights, layer, currentLayer))
                    {
                        for (int move = keptLayers.Length - 1; move > keptIndex; move--)
                            keptLayers[move] = keptLayers[move - 1];
                        keptLayers[keptIndex] = layer;
                        break;
                    }
                }
            }

        }

        private static bool IsHigherPriority(float[] weights, int leftLayer, int rightLayer)
        {
            return weights[leftLayer] > weights[rightLayer] ||
                   (weights[leftLayer] == weights[rightLayer] && leftLayer < rightLayer);
        }

        private static void Quantize(
            float[] weights,
            int[] keptLayers,
            int[] encoded)
        {
            float sum = 0f;
            for (int index = 0; index < keptLayers.Length; index++)
            {
                int layer = keptLayers[index];
                if (layer >= 0)
                    sum += weights[layer];
            }

            if (sum <= 0f)
            {
                Debug.LogError("Terrain control-map quantization had no positive weights; falling back to Wasteland.");
                encoded[(int)FirstArtTerrainLayer3D.Wasteland] = 255;
                return;
            }

            int encodedSum = 0;
            for (int index = 0; index < keptLayers.Length; index++)
            {
                int layer = keptLayers[index];
                if (layer < 0)
                    continue;
                int value = Mathf.RoundToInt(weights[layer] / sum * 255f);
                encoded[layer] = value;
                encodedSum += value;
            }

            if (encodedSum == 0)
            {
                Debug.LogError("Terrain control-map quantization produced a zero byte sum; falling back to Wasteland.");
                Array.Clear(encoded, 0, encoded.Length);
                encoded[(int)FirstArtTerrainLayer3D.Wasteland] = 255;
                return;
            }

            encoded[keptLayers[0]] += 255 - encodedSum;
        }

        private static void Encode(int[] encodedWeights, byte[] controlA, byte[] controlB, int offset)
        {
            controlA[offset] = (byte)encodedWeights[0];
            controlA[offset + 1] = (byte)encodedWeights[1];
            controlA[offset + 2] = (byte)encodedWeights[2];
            controlA[offset + 3] = (byte)encodedWeights[3];
            controlB[offset] = (byte)encodedWeights[4];
            controlB[offset + 1] = (byte)encodedWeights[5];
            controlB[offset + 2] = (byte)encodedWeights[6];
            controlB[offset + 3] = 0;
        }

        private static float MaximumBlendWidth(FirstArtTerrainProfile3D profile)
        {
            float maximum = 0f;
            for (int left = 0; left < FirstArtTerrainCatalog3D.LayerCount; left++)
            for (int right = left + 1; right < FirstArtTerrainCatalog3D.LayerCount; right++)
            {
                maximum = Mathf.Max(
                    maximum,
                    profile.BlendWidth(
                        (FirstArtTerrainLayer3D)left,
                        (FirstArtTerrainLayer3D)right));
            }
            return maximum;
        }

        private static float DistanceToCellRectangle(float x, float y, int cellX, int cellY)
        {
            return Mathf.Sqrt(
                SquaredDistanceToCellRectangle(x, y, cellX, cellY));
        }

        private static float SquaredDistanceToCellRectangle(
            float x,
            float y,
            int cellX,
            int cellY)
        {
            float minX = cellX - 0.5f;
            float maxX = cellX + 0.5f;
            float minY = cellY - 0.5f;
            float maxY = cellY + 0.5f;
            float deltaX = x < minX ? minX - x : x > maxX ? x - maxX : 0f;
            float deltaY = y < minY ? minY - y : y > maxY ? y - maxY : 0f;
            return deltaX * deltaX + deltaY * deltaY;
        }

        private static uint Hash(int x, int y, int layer)
        {
            uint value = unchecked((uint)x * 0x8DA6B343u);
            value ^= unchecked((uint)y * 0xD8163841u);
            value ^= unchecked((uint)layer * 0xCB1AB31Fu);
            value ^= value >> 13;
            value *= 0x85EBCA6Bu;
            return value ^ (value >> 16);
        }

        private static float EdgeNoise(float x, float y, int layer)
        {
            float latticeX = x * .25f;
            float latticeY = y * .25f;
            int x0 = Mathf.FloorToInt(latticeX);
            int y0 = Mathf.FloorToInt(latticeY);
            float tx = Smooth01(latticeX - x0);
            float ty = Smooth01(latticeY - y0);
            float h00 = Hash01(Hash(x0, y0, layer));
            float h10 = Hash01(Hash(x0 + 1, y0, layer));
            float h01 = Hash01(Hash(x0, y0 + 1, layer));
            float h11 = Hash01(Hash(x0 + 1, y0 + 1, layer));
            float value = Mathf.Lerp(
                Mathf.Lerp(h00, h10, tx),
                Mathf.Lerp(h01, h11, tx),
                ty);
            return (value * 2f - 1f) * .12f;
        }

        private static float Hash01(uint value)
        {
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static float Smooth01(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            return Smooth01(Mathf.Clamp01((value - edge0) / (edge1 - edge0)));
        }
    }
}
