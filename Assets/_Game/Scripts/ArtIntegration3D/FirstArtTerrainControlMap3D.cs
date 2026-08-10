using System;
using UnityEngine;

namespace WasteCity.ArtIntegration3D
{
    public readonly struct TerrainControlWeights3D
    {
        public Vector4 Base { get; }
        public Vector4 Special { get; }
        public float Sum { get; }
        public int NonZeroCount { get; }

        internal TerrainControlWeights3D(Vector4 baseWeights, Vector4 specialWeights)
        {
            Base = baseWeights;
            Special = specialWeights;
            Sum = baseWeights.x + baseWeights.y + baseWeights.z + baseWeights.w +
                  specialWeights.x + specialWeights.y + specialWeights.z;
            NonZeroCount = CountNonZero(baseWeights) + CountNonZero(specialWeights);
        }

        private static int CountNonZero(Vector4 weights)
        {
            int count = 0;
            if (weights.x > 0f) count++;
            if (weights.y > 0f) count++;
            if (weights.z > 0f) count++;
            if (weights.w > 0f) count++;
            return count;
        }
    }

    public sealed class FirstArtTerrainControlMap3D : IDisposable
    {
        internal static Action AfterControlAAllocatedForTests;

        private bool disposed;

        public int Width { get; }
        public int Height { get; }
        public Texture2D ControlA { get; }
        public Texture2D ControlB { get; }
        public byte[] ControlABytes { get; }
        public byte[] ControlBBytes { get; }

        internal FirstArtTerrainControlMap3D(
            int width,
            int height,
            byte[] controlABytes,
            byte[] controlBBytes)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (controlABytes == null)
                throw new ArgumentNullException(nameof(controlABytes));
            if (controlBBytes == null)
                throw new ArgumentNullException(nameof(controlBBytes));

            int requiredByteCount = checked(width * height * 4);
            if (controlABytes.Length != requiredByteCount)
                throw new ArgumentException("Control A bytes do not match the control-map dimensions.", nameof(controlABytes));
            if (controlBBytes.Length != requiredByteCount)
                throw new ArgumentException("Control B bytes do not match the control-map dimensions.", nameof(controlBBytes));

            Width = width;
            Height = height;
            ControlABytes = controlABytes;
            ControlBBytes = controlBBytes;

            Texture2D localControlA = null;
            Texture2D localControlB = null;
            try
            {
                localControlA = CreateTexture(
                    width,
                    height,
                    controlABytes,
                    "FirstArtTerrainControlA");
                AfterControlAAllocatedForTests?.Invoke();
                localControlB = CreateTexture(
                    width,
                    height,
                    controlBBytes,
                    "FirstArtTerrainControlB");

                ControlA = localControlA;
                ControlB = localControlB;
                localControlA = null;
                localControlB = null;
            }
            catch
            {
                DestroyOwnedTexture(localControlB);
                DestroyOwnedTexture(localControlA);
                throw;
            }
        }

        public TerrainControlWeights3D GetWeights(int x, int y)
        {
            if (x < 0 || x >= Width)
                throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(y));

            int offset = (y * Width + x) * 4;
            const float byteToWeight = 1f / 255f;
            return new TerrainControlWeights3D(
                new Vector4(
                    ControlABytes[offset] * byteToWeight,
                    ControlABytes[offset + 1] * byteToWeight,
                    ControlABytes[offset + 2] * byteToWeight,
                    ControlABytes[offset + 3] * byteToWeight),
                new Vector4(
                    ControlBBytes[offset] * byteToWeight,
                    ControlBBytes[offset + 1] * byteToWeight,
                    ControlBBytes[offset + 2] * byteToWeight,
                    ControlBBytes[offset + 3] * byteToWeight));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            DestroyOwnedTexture(ControlA);
            DestroyOwnedTexture(ControlB);
        }

        private static Texture2D CreateTexture(
            int width,
            int height,
            byte[] bytes,
            string textureName)
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = textureName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                texture.LoadRawTextureData(bytes);
                texture.Apply(false, true);
                return texture;
            }
            catch
            {
                DestroyOwnedTexture(texture);
                throw;
            }
        }

        private static void DestroyOwnedTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
