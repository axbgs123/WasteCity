using System;
using UnityEngine;

namespace WasteCity.ArtIntegration3D
{
    public static class FirstArtTerrainMeshBuilder3D
    {
        public static Mesh Build(int width, int height)
        {
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height));

            float minX = -width * .5f - .5f;
            float maxX = width * .5f - .5f;
            float minZ = -height * .5f - .5f;
            float maxZ = height * .5f - .5f;
            var mesh = new Mesh { name = "first-art.terrain.surface" };
            mesh.vertices = new[]
            {
                new Vector3(minX, 0f, minZ),
                new Vector3(maxX, 0f, minZ),
                new Vector3(minX, 0f, maxZ),
                new Vector3(maxX, 0f, maxZ)
            };
            mesh.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };
            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, -1f),
                new Vector4(1f, 0f, 0f, -1f),
                new Vector4(1f, 0f, 0f, -1f),
                new Vector4(1f, 0f, 0f, -1f)
            };
            mesh.uv = new[]
            {
                Vector2.zero, Vector2.right, Vector2.up, Vector2.one
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
