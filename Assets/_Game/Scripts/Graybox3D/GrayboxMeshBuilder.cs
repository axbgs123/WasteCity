using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WasteCity.Graybox3D
{
    public static class GrayboxMeshBuilder
    {
        public static Mesh CombinePrimitive(
            PrimitiveType primitiveType,
            IReadOnlyList<Matrix4x4> instances,
            string meshName)
        {
            if (instances == null)
                throw new ArgumentNullException(nameof(instances));
            if (string.IsNullOrWhiteSpace(meshName))
                throw new ArgumentException(
                    "A combined mesh name is required.",
                    nameof(meshName));

            Mesh source = GetBuiltinMesh(primitiveType);
            var combined = new Mesh
            {
                name = meshName
            };
            if ((long)source.vertexCount * instances.Count > ushort.MaxValue)
                combined.indexFormat = IndexFormat.UInt32;

            var combineInstances = new CombineInstance[instances.Count];
            for (int index = 0; index < instances.Count; index++)
            {
                combineInstances[index] = new CombineInstance
                {
                    mesh = source,
                    transform = instances[index]
                };
            }

            combined.CombineMeshes(
                combineInstances,
                true,
                true,
                false);
            combined.RecalculateBounds();
            return combined;
        }

        private static Mesh GetBuiltinMesh(PrimitiveType primitiveType)
        {
            string path;
            switch (primitiveType)
            {
                case PrimitiveType.Cube:
                    path = "Cube.fbx";
                    break;
                case PrimitiveType.Capsule:
                    path = "Capsule.fbx";
                    break;
                case PrimitiveType.Plane:
                    path = "Plane.fbx";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(primitiveType),
                        primitiveType,
                        "The graybox catalog only uses Plane, Cube, and Capsule.");
            }

            Mesh mesh = Resources.GetBuiltinResource<Mesh>(path);
            if (mesh == null)
                throw new InvalidOperationException(
                    $"Unity built-in mesh '{path}' is unavailable.");
            return mesh;
        }
    }
}
