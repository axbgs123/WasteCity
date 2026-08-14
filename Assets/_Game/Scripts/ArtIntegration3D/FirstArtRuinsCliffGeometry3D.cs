using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace WasteCity.ArtIntegration3D
{
    public sealed class FirstArtRuinsCliffCategoryGeometry3D : IDisposable
    {
        private bool disposed;

        internal FirstArtRuinsCliffCategoryGeometry3D(
            FirstArtRuinsCliffFamily3D family,
            GameObject gameObject,
            Mesh mesh)
        {
            Family = family;
            GameObject = gameObject;
            Mesh = mesh;
        }

        public FirstArtRuinsCliffFamily3D Family { get; }
        public GameObject GameObject { get; }
        public Mesh Mesh { get; }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            // PlayMode destruction is deferred until after the current update.
            // Hide owned geometry now so fallback cannot share a render frame.
            if (Application.isPlaying && GameObject != null)
                GameObject.SetActive(false);
            DestroyOwned(GameObject);
            DestroyOwned(Mesh);
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }

    public static class FirstArtRuinsCliffGeometry3D
    {
        private const string RuntimeMeshName = "FirstArtRuinsCliffCombinedGeometry";
        private static bool? supportsUInt32Override;
        private static string failureCheckpoint;
        private static int readOnlyMeshDataAcquisitionCount;
        private static int directVertexViewCount;

        [StructLayout(LayoutKind.Sequential)]
        private struct OutputVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Tangent;
            public Vector2 Uv0;
        }

        private struct DirectVertexTransformJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<OutputVertex> Source;

            [NativeDisableParallelForRestriction]
            public NativeArray<OutputVertex> Destination;

            [NativeDisableParallelForRestriction]
            public NativeArray<int> ErrorCode;

            public Matrix4x4 Matrix;
            public Matrix4x4 NormalMatrix;
            public int DestinationStart;

            public void Execute(int index)
            {
                OutputVertex sourceVertex = Source[index];
                Vector3 transformedNormal = NormalMatrix.MultiplyVector(
                    sourceVertex.Normal);
                if (!TryNormalize(transformedNormal, out transformedNormal))
                {
                    RecordFirstTransformError(ErrorCode, 1);
                    return;
                }
                Vector3 sourceTangentDirection = new Vector3(
                    sourceVertex.Tangent.x,
                    sourceVertex.Tangent.y,
                    sourceVertex.Tangent.z);
                if (!IsFinite(sourceTangentDirection))
                {
                    RecordFirstTransformError(ErrorCode, 2);
                    return;
                }
                Vector3 transformedTangent = Matrix.MultiplyVector(
                    sourceTangentDirection);
                transformedTangent -= transformedNormal *
                    Vector3.Dot(transformedNormal, transformedTangent);
                if (!TryNormalize(transformedTangent, out transformedTangent))
                {
                    Vector3 fallbackAxis = Mathf.Abs(transformedNormal.y) < 0.9f
                        ? Vector3.up
                        : Vector3.right;
                    transformedTangent = Vector3.Cross(
                        fallbackAxis,
                        transformedNormal).normalized;
                }

                Vector3 position = Matrix.MultiplyPoint3x4(sourceVertex.Position);
                if (!IsFinite(position) || !IsFinite(sourceVertex.Uv0) ||
                    !IsFinite(sourceVertex.Tangent.w))
                {
                    RecordFirstTransformError(ErrorCode, 3);
                    return;
                }
                Destination[DestinationStart + index] = new OutputVertex
                {
                    Position = position,
                    Normal = transformedNormal,
                    Tangent = new Vector4(
                        transformedTangent.x,
                        transformedTangent.y,
                        transformedTangent.z,
                        sourceVertex.Tangent.w),
                    Uv0 = sourceVertex.Uv0,
                };
            }
        }

        private static unsafe void RecordFirstTransformError(
            NativeArray<int> errorCode,
            int value)
        {
            int* errorCodePointer =
                (int*)NativeArrayUnsafeUtility.GetUnsafePtr(errorCode);
            Interlocked.CompareExchange(ref *errorCodePointer, value, 0);
        }

        private sealed class SourcePlacement
        {
            public FirstArtRuinsCliffPlacement3D Placement;
            public FirstArtRuinsCliffCatalogEntry3D Entry;
            public Mesh Mesh;
        }

        public static bool IsUsingSystemIndexCapability =>
            !supportsUInt32Override.HasValue;

        public static int ReadOnlyMeshDataAcquisitionCountForTests =>
            readOnlyMeshDataAcquisitionCount;

        public static int DirectVertexViewCountForTests =>
            directVertexViewCount;

        public static bool TryBuild(
            FirstArtRuinsCliffProfile3D profile,
            IReadOnlyList<FirstArtRuinsCliffPlacement3D> placements,
            Transform parent,
            out FirstArtRuinsCliffCategoryGeometry3D geometry,
            out string error)
        {
            geometry = null;
            Mesh outputMesh = null;
            GameObject categoryObject = null;
            Mesh.MeshDataArray writable = default;
            bool writableAllocated = false;
            try
            {
                if (!TryPreflight(
                        profile,
                        placements,
                        parent,
                        out FirstArtRuinsCliffFamily3D family,
                        out List<SourcePlacement> sources,
                        out List<string> familyRoles,
                        out long vertexCount,
                        out long indexCount,
                        out error))
                    return false;

                Checkpoint("AfterPreflight");
                bool supportsUInt32 = supportsUInt32Override ??
                    SystemInfo.supports32bitsIndexBuffer;
                if (!TrySelectIndexFormatForTests(
                        vertexCount,
                        indexCount,
                        supportsUInt32,
                        out IndexFormat indexFormat,
                        out int checkedVertexCount,
                        out int checkedIndexCount,
                        out error))
                    return false;

                writable = Mesh.AllocateWritableMeshData(1);
                writableAllocated = true;
                Mesh.MeshData output = writable[0];
                output.SetVertexBufferParams(
                    checkedVertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
                output.SetIndexBufferParams(checkedIndexCount, indexFormat);
                output.subMeshCount = familyRoles.Count;
                Checkpoint("AfterWritableAllocated");

                if (!TryPopulate(
                        sources,
                        familyRoles,
                        output,
                        indexFormat,
                        out error))
                    return false;

                outputMesh = new Mesh { name = RuntimeMeshName };
                Mesh.ApplyAndDisposeWritableMeshData(
                    writable,
                    outputMesh,
                    MeshUpdateFlags.DontValidateIndices);
                writableAllocated = false;
                outputMesh.RecalculateBounds();
                Checkpoint("AfterMeshApplied");

                categoryObject = new GameObject(
                    family == FirstArtRuinsCliffFamily3D.Ruins
                        ? "RuinsGeometry"
                        : "CliffGeometry");
                categoryObject.transform.SetParent(parent, false);
                MeshFilter filter = categoryObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = categoryObject.AddComponent<MeshRenderer>();
                filter.sharedMesh = outputMesh;
                var materials = new Material[familyRoles.Count];
                for (int index = 0; index < familyRoles.Count; index++)
                {
                    profile.TryResolveMaterial(familyRoles[index], out materials[index]);
                }
                renderer.sharedMaterials = materials;
                Checkpoint("AfterGameObjectCreated");

                geometry = new FirstArtRuinsCliffCategoryGeometry3D(
                    family,
                    categoryObject,
                    outputMesh);
                categoryObject = null;
                outputMesh = null;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "Combined geometry build failed: " + exception.Message;
                return false;
            }
            finally
            {
                if (writableAllocated)
                    writable.Dispose();
                DestroyTemporary(categoryObject);
                DestroyTemporary(outputMesh);
            }
        }

        public static bool TrySelectIndexFormatForTests(
            long vertexCount,
            long indexCount,
            bool supportsUInt32,
            out IndexFormat format,
            out int checkedVertexCount,
            out int checkedIndexCount,
            out string error)
        {
            format = IndexFormat.UInt16;
            checkedVertexCount = 0;
            checkedIndexCount = 0;
            if (vertexCount < 0 || indexCount < 0)
            {
                error = "Vertex and index counts cannot be negative.";
                return false;
            }
            if (vertexCount > int.MaxValue || indexCount > int.MaxValue)
            {
                error = "Combined geometry exceeds Unity's signed 32-bit count limit.";
                return false;
            }

            checkedVertexCount = checked((int)vertexCount);
            checkedIndexCount = checked((int)indexCount);
            if (vertexCount > ushort.MaxValue)
            {
                if (!supportsUInt32)
                {
                    error = "32-bit mesh indices are required but unsupported.";
                    return false;
                }
                format = IndexFormat.UInt32;
            }

            error = null;
            return true;
        }

        public static IDisposable OverrideTestConfiguration(
            bool supportsUInt32,
            string failAtCheckpoint = null)
        {
            bool? previousCapability = supportsUInt32Override;
            string previousCheckpoint = failureCheckpoint;
            supportsUInt32Override = supportsUInt32;
            failureCheckpoint = failAtCheckpoint;
            return new RestoreScope(previousCapability, previousCheckpoint);
        }

        public static void ResetTestConfiguration()
        {
            supportsUInt32Override = null;
            failureCheckpoint = null;
            readOnlyMeshDataAcquisitionCount = 0;
            directVertexViewCount = 0;
        }

        private static bool TryPreflight(
            FirstArtRuinsCliffProfile3D profile,
            IReadOnlyList<FirstArtRuinsCliffPlacement3D> placements,
            Transform parent,
            out FirstArtRuinsCliffFamily3D family,
            out List<SourcePlacement> sources,
            out List<string> familyRoles,
            out long vertexCount,
            out long indexCount,
            out string error)
        {
            family = default;
            sources = null;
            familyRoles = null;
            vertexCount = 0;
            indexCount = 0;
            if (profile == null)
            {
                error = "A geometry profile is required.";
                return false;
            }
            if (!profile.TryValidate(out error))
                return false;
            if (placements == null || placements.Count == 0)
            {
                error = "At least one placement is required.";
                return false;
            }
            if (parent == null)
            {
                error = "A category parent is required.";
                return false;
            }

            family = placements[0].Family;
            familyRoles = GetFamilyRoles(family);
            sources = new List<SourcePlacement>(placements.Count);
            try
            {
                for (int placementIndex = 0;
                     placementIndex < placements.Count;
                     placementIndex++)
                {
                    FirstArtRuinsCliffPlacement3D placement = placements[placementIndex];
                    if (placement.Family != family)
                    {
                        error = "A category build cannot mix ruins and cliff placements.";
                        return false;
                    }
                    if (placement.CatalogIndex < 0 ||
                        placement.CatalogIndex >= FirstArtRuinsCliffCatalog3D.Entries.Count)
                    {
                        error = "Placement catalog index is out of range.";
                        return false;
                    }

                    FirstArtRuinsCliffCatalogEntry3D entry =
                        FirstArtRuinsCliffCatalog3D.Entries[placement.CatalogIndex];
                    if (entry.Family != family)
                    {
                        error = "Placement family does not match its catalog entry.";
                        return false;
                    }
                    if (placement.WorldMatrix.determinant <= 0.000001f)
                    {
                        error = "Placement matrices must have a positive, non-zero determinant.";
                        return false;
                    }
                    if (!profile.TryResolvePrefab(entry.StableId, out GameObject prefab))
                    {
                        error = "Prefab binding cannot be resolved for " + entry.StableId + ".";
                        return false;
                    }

                    MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                    MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                    if (filters.Length != 1 || renderers.Length != 1 ||
                        filters[0].gameObject != renderers[0].gameObject)
                    {
                        error = "Prefab must expose exactly one paired MeshFilter and MeshRenderer.";
                        return false;
                    }
                    Mesh mesh = filters[0].sharedMesh;
                    if (mesh == null)
                    {
                        error = "Prefab source mesh is missing for " + entry.StableId + ".";
                        return false;
                    }
                    if (!TryValidateSourceMesh(mesh, entry, profile, renderers[0], out error))
                        return false;

                    vertexCount = checked(vertexCount + mesh.vertexCount);
                    for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                        indexCount = checked(indexCount + mesh.GetIndexCount(subMesh));
                    sources.Add(new SourcePlacement
                    {
                        Placement = placement,
                        Entry = entry,
                        Mesh = mesh,
                    });
                }
            }
            catch (OverflowException)
            {
                error = "Combined geometry count overflowed signed 64-bit arithmetic.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryValidateSourceMesh(
            Mesh mesh,
            FirstArtRuinsCliffCatalogEntry3D entry,
            FirstArtRuinsCliffProfile3D profile,
            MeshRenderer renderer,
            out string error)
        {
            if (!TryValidateAttribute(mesh, VertexAttribute.Position, 3, out error) ||
                !TryValidateAttribute(mesh, VertexAttribute.Normal, 3, out error) ||
                !TryValidateAttribute(mesh, VertexAttribute.Tangent, 4, out error) ||
                !TryValidateAttribute(mesh, VertexAttribute.TexCoord0, 2, out error))
                return false;
            if (mesh.subMeshCount != entry.MaterialRoles.Count)
            {
                error = "Source submesh count must match the catalog material slots.";
                return false;
            }
            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials.Length != entry.MaterialRoles.Count)
            {
                error = "Renderer material slot count must match the catalog.";
                return false;
            }
            for (int slot = 0; slot < entry.MaterialRoles.Count; slot++)
            {
                if (mesh.GetTopology(slot) != MeshTopology.Triangles)
                {
                    error = "Only triangle source submeshes are supported.";
                    return false;
                }
                if (!profile.TryResolveMaterial(entry.MaterialRoles[slot], out Material expected) ||
                    sourceMaterials[slot] != expected)
                {
                    error = "Renderer material slots must follow the catalog roles exactly.";
                    return false;
                }
            }
            error = null;
            return true;
        }

        private static bool TryValidateAttribute(
            Mesh mesh,
            VertexAttribute attribute,
            int dimension,
            out string error)
        {
            if (!mesh.HasVertexAttribute(attribute) ||
                mesh.GetVertexAttributeFormat(attribute) != VertexAttributeFormat.Float32 ||
                mesh.GetVertexAttributeDimension(attribute) != dimension)
            {
                error = attribute + " must be a Float32 vertex channel with dimension " +
                        dimension + ".";
                return false;
            }
            error = null;
            return true;
        }

        private static List<string> GetFamilyRoles(FirstArtRuinsCliffFamily3D family)
        {
            var roles = new List<string>();
            for (int index = 0;
                 index < FirstArtRuinsCliffCatalog3D.MaterialRoles.Count;
                 index++)
            {
                FirstArtRuinsCliffMaterialRole3D role =
                    FirstArtRuinsCliffCatalog3D.MaterialRoles[index];
                if (role.Family == family)
                    roles.Add(role.Name);
            }
            return roles;
        }

        private static bool TryPopulate(
            List<SourcePlacement> sources,
            List<string> familyRoles,
            Mesh.MeshData output,
            IndexFormat outputIndexFormat,
            out string error)
        {
            var roleIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var roleCounts = new int[familyRoles.Count];
            for (int index = 0; index < familyRoles.Count; index++)
                roleIndices.Add(familyRoles[index], index);
            try
            {
                for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    SourcePlacement source = sources[sourceIndex];
                    for (int slot = 0; slot < source.Mesh.subMeshCount; slot++)
                    {
                        int roleIndex = roleIndices[source.Entry.MaterialRoles[slot]];
                        roleCounts[roleIndex] = checked(
                            roleCounts[roleIndex] +
                            checked((int)source.Mesh.GetIndexCount(slot)));
                    }
                }
            }
            catch (OverflowException)
            {
                error = "A material role index range exceeds signed 32-bit limits.";
                return false;
            }

            var roleStarts = new int[familyRoles.Count];
            var roleCursors = new int[familyRoles.Count];
            int runningIndex = 0;
            for (int role = 0; role < familyRoles.Count; role++)
            {
                roleStarts[role] = runningIndex;
                roleCursors[role] = runningIndex;
                runningIndex = checked(runningIndex + roleCounts[role]);
            }

            NativeArray<OutputVertex> vertices = output.GetVertexData<OutputVertex>();
            NativeArray<ushort> indices16 = default;
            NativeArray<uint> indices32 = default;
            if (outputIndexFormat == IndexFormat.UInt16)
                indices16 = output.GetIndexData<ushort>();
            else
                indices32 = output.GetIndexData<uint>();

            int vertexBase = 0;
            var readableByMesh = new Dictionary<Mesh, Mesh.MeshDataArray>();
            var directVerticesByMesh =
                new Dictionary<Mesh, NativeArray<OutputVertex>>();
            NativeArray<int> directTransformErrorCode = default;
            try
            {
                for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    SourcePlacement source = sources[sourceIndex];
                    if (!readableByMesh.TryGetValue(
                            source.Mesh,
                            out Mesh.MeshDataArray readable))
                    {
                        readable = Mesh.AcquireReadOnlyMeshData(source.Mesh);
                        readableByMesh.Add(source.Mesh, readable);
                        readOnlyMeshDataAcquisitionCount++;
                        Mesh.MeshData acquiredInput = readable[0];
                        if (HasDirectInterleavedVertexLayout(acquiredInput))
                        {
                            directVerticesByMesh.Add(
                                source.Mesh,
                                acquiredInput.GetVertexData<OutputVertex>(0));
                            directVertexViewCount++;
                        }
                    }
                    Mesh.MeshData input = readable[0];
                    directVerticesByMesh.TryGetValue(
                        source.Mesh,
                        out NativeArray<OutputVertex> directVertices);
                    NativeArray<ushort> sourceIndices16 = default;
                    NativeArray<uint> sourceIndices32 = default;
                    if (source.Mesh.indexFormat == IndexFormat.UInt16)
                        sourceIndices16 = input.GetIndexData<ushort>();
                    else
                        sourceIndices32 = input.GetIndexData<uint>();
                    if (directVertices.IsCreated)
                    {
                        if (!directTransformErrorCode.IsCreated)
                        {
                            directTransformErrorCode = new NativeArray<int>(
                                1,
                                Allocator.TempJob,
                                NativeArrayOptions.ClearMemory);
                        }
                        if (!TryCopyDirectInterleavedPlacement(
                                source,
                                input,
                                directVertices,
                                vertices,
                                directTransformErrorCode,
                                sourceIndices16,
                                sourceIndices32,
                                indices16,
                                indices32,
                                outputIndexFormat,
                                roleIndices,
                                roleCursors,
                                vertexBase,
                                out error))
                            return false;
                    }
                    else
                    {
                        if (!TryCopyVertices(
                                input,
                                source.Placement.WorldMatrix,
                                vertices,
                                vertexBase,
                                out error))
                            return false;
                        if (!TryCopyIndices(
                                source,
                                input,
                                sourceIndices16,
                                sourceIndices32,
                                indices16,
                                indices32,
                                outputIndexFormat,
                                roleIndices,
                                roleCursors,
                                vertexBase,
                                out error))
                            return false;
                    }
                    vertexBase = checked(vertexBase + source.Mesh.vertexCount);
                }
            }
            finally
            {
                if (directTransformErrorCode.IsCreated)
                    directTransformErrorCode.Dispose();
                foreach (Mesh.MeshDataArray readable in readableByMesh.Values)
                    readable.Dispose();
            }

            for (int role = 0; role < familyRoles.Count; role++)
            {
                output.SetSubMesh(
                    role,
                    new SubMeshDescriptor(
                        roleStarts[role],
                        roleCounts[role],
                        MeshTopology.Triangles),
                    MeshUpdateFlags.DontValidateIndices);
            }

            error = null;
            return true;
        }

        private static bool TryCopyVertices(
            Mesh.MeshData input,
            Matrix4x4 matrix,
            NativeArray<OutputVertex> destination,
            int destinationStart,
            out string error)
        {
            int positionStream =
                input.GetVertexAttributeStream(VertexAttribute.Position);
            int normalStream =
                input.GetVertexAttributeStream(VertexAttribute.Normal);
            int tangentStream =
                input.GetVertexAttributeStream(VertexAttribute.Tangent);
            int uvStream =
                input.GetVertexAttributeStream(VertexAttribute.TexCoord0);
            NativeArray<float> positions = input.GetVertexData<byte>(positionStream)
                .Reinterpret<float>(1);
            NativeArray<float> normals = input.GetVertexData<byte>(normalStream)
                .Reinterpret<float>(1);
            NativeArray<float> tangents = input.GetVertexData<byte>(tangentStream)
                .Reinterpret<float>(1);
            NativeArray<float> uvs = input.GetVertexData<byte>(uvStream)
                .Reinterpret<float>(1);
            int positionStride = input.GetVertexBufferStride(positionStream) / 4;
            int normalStride = input.GetVertexBufferStride(normalStream) / 4;
            int tangentStride = input.GetVertexBufferStride(tangentStream) / 4;
            int uvStride = input.GetVertexBufferStride(uvStream) / 4;
            int positionOffset =
                input.GetVertexAttributeOffset(VertexAttribute.Position) / 4;
            int normalOffset =
                input.GetVertexAttributeOffset(VertexAttribute.Normal) / 4;
            int tangentOffset =
                input.GetVertexAttributeOffset(VertexAttribute.Tangent) / 4;
            int uvOffset =
                input.GetVertexAttributeOffset(VertexAttribute.TexCoord0) / 4;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;

            for (int vertex = 0; vertex < input.vertexCount; vertex++)
            {
                Vector3 sourcePosition = ReadVector3(
                    positions,
                    vertex * positionStride + positionOffset);
                Vector3 sourceNormal = ReadVector3(
                    normals,
                    vertex * normalStride + normalOffset);
                Vector4 sourceTangent = ReadVector4(
                    tangents,
                    vertex * tangentStride + tangentOffset);
                Vector2 sourceUv = ReadVector2(
                    uvs,
                    vertex * uvStride + uvOffset);

                Vector3 transformedNormal = normalMatrix.MultiplyVector(sourceNormal);
                if (!TryNormalize(transformedNormal, out transformedNormal))
                {
                    error = "A transformed normal is degenerate or non-finite.";
                    return false;
                }
                Vector3 sourceTangentDirection = new Vector3(
                    sourceTangent.x,
                    sourceTangent.y,
                    sourceTangent.z);
                if (!IsFinite(sourceTangentDirection))
                {
                    error = "Source tangent data is non-finite.";
                    return false;
                }
                Vector3 transformedTangent = matrix.MultiplyVector(
                    sourceTangentDirection);
                transformedTangent -= transformedNormal *
                    Vector3.Dot(transformedNormal, transformedTangent);
                if (!TryNormalize(transformedTangent, out transformedTangent))
                {
                    Vector3 fallbackAxis = Mathf.Abs(transformedNormal.y) < 0.9f
                        ? Vector3.up
                        : Vector3.right;
                    transformedTangent = Vector3.Cross(
                        fallbackAxis,
                        transformedNormal).normalized;
                }

                Vector3 position = matrix.MultiplyPoint3x4(sourcePosition);
                if (!IsFinite(position) || !IsFinite(sourceUv) ||
                    !IsFinite(sourceTangent.w))
                {
                    error = "Source or transformed vertex data is non-finite.";
                    return false;
                }
                destination[destinationStart + vertex] = new OutputVertex
                {
                    Position = position,
                    Normal = transformedNormal,
                    Tangent = new Vector4(
                        transformedTangent.x,
                        transformedTangent.y,
                        transformedTangent.z,
                        sourceTangent.w),
                    Uv0 = sourceUv,
                };
            }

            error = null;
            return true;
        }

        private static unsafe bool TryCopyDirectInterleavedPlacement(
            SourcePlacement source,
            Mesh.MeshData input,
            NativeArray<OutputVertex> sourceVertices,
            NativeArray<OutputVertex> destinationVertices,
            NativeArray<int> transformErrorCode,
            NativeArray<ushort> sourceIndices16,
            NativeArray<uint> sourceIndices32,
            NativeArray<ushort> destinationIndices16,
            NativeArray<uint> destinationIndices32,
            IndexFormat outputIndexFormat,
            Dictionary<string, int> roleIndices,
            int[] roleCursors,
            int vertexBase,
            out string error)
        {
            if (vertexBase < 0 ||
                input.vertexCount < 0 ||
                vertexBase > destinationVertices.Length - input.vertexCount)
            {
                error = "Combined vertex destination range is invalid.";
                return false;
            }

            transformErrorCode[0] = 0;
            Matrix4x4 matrix = source.Placement.WorldMatrix;
            var transformJob = new DirectVertexTransformJob
            {
                Source = sourceVertices,
                Destination = destinationVertices,
                ErrorCode = transformErrorCode,
                Matrix = matrix,
                NormalMatrix = matrix.inverse.transpose,
                DestinationStart = vertexBase,
            };
            transformJob.Schedule(input.vertexCount, 64).Complete();
            switch (transformErrorCode[0])
            {
                case 1:
                    error = "A transformed normal is degenerate or non-finite.";
                    return false;
                case 2:
                    error = "Source tangent data is non-finite.";
                    return false;
                case 3:
                    error = "Source or transformed vertex data is non-finite.";
                    return false;
            }

            ushort* sourceIndex16Pointer = source.Mesh.indexFormat == IndexFormat.UInt16
                ? (ushort*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceIndices16)
                : null;
            uint* sourceIndex32Pointer = source.Mesh.indexFormat == IndexFormat.UInt32
                ? (uint*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceIndices32)
                : null;
            ushort* destinationIndex16Pointer = outputIndexFormat == IndexFormat.UInt16
                ? (ushort*)NativeArrayUnsafeUtility.GetUnsafePtr(destinationIndices16)
                : null;
            uint* destinationIndex32Pointer = outputIndexFormat == IndexFormat.UInt32
                ? (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(destinationIndices32)
                : null;

            int sourceIndexLength = source.Mesh.indexFormat == IndexFormat.UInt16
                ? sourceIndices16.Length
                : sourceIndices32.Length;
            int destinationIndexLength = outputIndexFormat == IndexFormat.UInt16
                ? destinationIndices16.Length
                : destinationIndices32.Length;
            for (int slot = 0; slot < source.Mesh.subMeshCount; slot++)
            {
                int roleIndex = roleIndices[source.Entry.MaterialRoles[slot]];
                SubMeshDescriptor descriptor = input.GetSubMesh(slot);
                for (int localIndex = 0;
                     localIndex < descriptor.indexCount;
                     localIndex++)
                {
                    long sourceIndexOffsetLong =
                        (long)descriptor.indexStart + localIndex;
                    if (sourceIndexOffsetLong < 0 ||
                        sourceIndexOffsetLong >= sourceIndexLength)
                    {
                        error = "Source mesh contains an invalid index range.";
                        return false;
                    }
                    int sourceIndexOffset = (int)sourceIndexOffsetLong;
                    uint rawIndex = source.Mesh.indexFormat == IndexFormat.UInt16
                        ? sourceIndex16Pointer[sourceIndexOffset]
                        : sourceIndex32Pointer[sourceIndexOffset];
                    long combined = (long)vertexBase + descriptor.baseVertex + rawIndex;
                    if (combined < vertexBase ||
                        combined >= (long)vertexBase + source.Mesh.vertexCount)
                    {
                        error = "Source mesh contains an out-of-range index.";
                        return false;
                    }
                    int destination = roleCursors[roleIndex];
                    if (destination < 0 || destination >= destinationIndexLength)
                    {
                        error = "Combined index destination range is invalid.";
                        return false;
                    }
                    roleCursors[roleIndex] = destination + 1;
                    if (outputIndexFormat == IndexFormat.UInt16)
                    {
                        if (combined > ushort.MaxValue)
                        {
                            error = "A combined index exceeds the selected 16-bit format.";
                            return false;
                        }
                        destinationIndex16Pointer[destination] = (ushort)combined;
                    }
                    else
                    {
                        if (combined > uint.MaxValue)
                        {
                            error = "A combined index exceeds the selected 32-bit format.";
                            return false;
                        }
                        destinationIndex32Pointer[destination] = (uint)combined;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool TryCopyIndices(
            SourcePlacement source,
            Mesh.MeshData input,
            NativeArray<ushort> sourceIndices16,
            NativeArray<uint> sourceIndices32,
            NativeArray<ushort> destinationIndices16,
            NativeArray<uint> destinationIndices32,
            IndexFormat outputIndexFormat,
            Dictionary<string, int> roleIndices,
            int[] roleCursors,
            int vertexBase,
            out string error)
        {
            for (int slot = 0; slot < source.Mesh.subMeshCount; slot++)
            {
                int roleIndex = roleIndices[source.Entry.MaterialRoles[slot]];
                SubMeshDescriptor descriptor = input.GetSubMesh(slot);
                for (int localIndex = 0;
                     localIndex < descriptor.indexCount;
                     localIndex++)
                {
                    int sourceIndexOffset = descriptor.indexStart + localIndex;
                    uint rawIndex = source.Mesh.indexFormat == IndexFormat.UInt16
                        ? sourceIndices16[sourceIndexOffset]
                        : sourceIndices32[sourceIndexOffset];
                    long combined = (long)vertexBase + descriptor.baseVertex + rawIndex;
                    if (combined < vertexBase ||
                        combined >= (long)vertexBase + source.Mesh.vertexCount)
                    {
                        error = "Source mesh contains an out-of-range index.";
                        return false;
                    }
                    int destination = roleCursors[roleIndex]++;
                    if (outputIndexFormat == IndexFormat.UInt16)
                        destinationIndices16[destination] = checked((ushort)combined);
                    else
                        destinationIndices32[destination] = checked((uint)combined);
                }
            }

            error = null;
            return true;
        }

        private static bool HasDirectInterleavedVertexLayout(Mesh.MeshData input)
        {
            return Marshal.SizeOf<OutputVertex>() == 48 &&
                input.GetVertexAttributeStream(VertexAttribute.Position) == 0 &&
                input.GetVertexAttributeStream(VertexAttribute.Normal) == 0 &&
                input.GetVertexAttributeStream(VertexAttribute.Tangent) == 0 &&
                input.GetVertexAttributeStream(VertexAttribute.TexCoord0) == 0 &&
                input.GetVertexAttributeOffset(VertexAttribute.Position) == 0 &&
                input.GetVertexAttributeOffset(VertexAttribute.Normal) == 12 &&
                input.GetVertexAttributeOffset(VertexAttribute.Tangent) == 24 &&
                input.GetVertexAttributeOffset(VertexAttribute.TexCoord0) == 40 &&
                input.GetVertexBufferStride(0) == 48 &&
                input.GetVertexAttributeFormat(VertexAttribute.Position) ==
                    VertexAttributeFormat.Float32 &&
                input.GetVertexAttributeFormat(VertexAttribute.Normal) ==
                    VertexAttributeFormat.Float32 &&
                input.GetVertexAttributeFormat(VertexAttribute.Tangent) ==
                    VertexAttributeFormat.Float32 &&
                input.GetVertexAttributeFormat(VertexAttribute.TexCoord0) ==
                    VertexAttributeFormat.Float32 &&
                input.GetVertexAttributeDimension(VertexAttribute.Position) == 3 &&
                input.GetVertexAttributeDimension(VertexAttribute.Normal) == 3 &&
                input.GetVertexAttributeDimension(VertexAttribute.Tangent) == 4 &&
                input.GetVertexAttributeDimension(VertexAttribute.TexCoord0) == 2;
        }

        private static Vector2 ReadVector2(NativeArray<float> values, int offset)
        {
            return new Vector2(values[offset], values[offset + 1]);
        }

        private static Vector3 ReadVector3(NativeArray<float> values, int offset)
        {
            return new Vector3(
                values[offset],
                values[offset + 1],
                values[offset + 2]);
        }

        private static Vector4 ReadVector4(NativeArray<float> values, int offset)
        {
            return new Vector4(
                values[offset],
                values[offset + 1],
                values[offset + 2],
                values[offset + 3]);
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            float square = value.sqrMagnitude;
            if (!IsFinite(square) || square <= 0.000000000001f)
            {
                normalized = default;
                return false;
            }
            normalized = value / Mathf.Sqrt(square);
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void Checkpoint(string name)
        {
            if (string.Equals(failureCheckpoint, name, StringComparison.Ordinal))
                throw new InvalidOperationException("Injected failure at " + name + ".");
        }

        private static void DestroyTemporary(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }

        private sealed class RestoreScope : IDisposable
        {
            private readonly bool? capability;
            private readonly string checkpoint;
            private bool disposed;

            public RestoreScope(bool? capability, string checkpoint)
            {
                this.capability = capability;
                this.checkpoint = checkpoint;
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                supportsUInt32Override = capability;
                failureCheckpoint = checkpoint;
            }
        }
    }
}
