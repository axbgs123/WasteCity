using System;
using System.Collections.Generic;
using UnityEngine;

namespace WasteCity.ArtIntegration3D
{
    [Serializable]
    public sealed class FirstArtRuinsCliffPrefabBinding3D
    {
        [SerializeField] private string stableId;
        [SerializeField] private GameObject prefab;

        public FirstArtRuinsCliffPrefabBinding3D(
            string stableId,
            GameObject prefab)
        {
            this.stableId = stableId;
            this.prefab = prefab;
        }

        public string StableId => stableId;
        public GameObject Prefab => prefab;
    }

    [Serializable]
    public sealed class FirstArtRuinsCliffMaterialBinding3D
    {
        [SerializeField] private string role;
        [SerializeField] private Material material;

        public FirstArtRuinsCliffMaterialBinding3D(
            string role,
            Material material)
        {
            this.role = role;
            this.material = material;
        }

        public string Role => role;
        public Material Material => material;
    }

    [CreateAssetMenu(menuName = "WasteCity/Art/First Ruins Cliff Profile")]
    public sealed class FirstArtRuinsCliffProfile3D : ScriptableObject
    {
        [SerializeField] private FirstArtRuinsCliffPrefabBinding3D[] prefabBindings =
            Array.Empty<FirstArtRuinsCliffPrefabBinding3D>();
        [SerializeField] private FirstArtRuinsCliffMaterialBinding3D[] materialBindings =
            Array.Empty<FirstArtRuinsCliffMaterialBinding3D>();
        [SerializeField] private Shader geometryShader;

        public IReadOnlyList<FirstArtRuinsCliffPrefabBinding3D> PrefabBindings =>
            prefabBindings;

        public IReadOnlyList<FirstArtRuinsCliffMaterialBinding3D> MaterialBindings =>
            materialBindings;

        public Shader GeometryShader => geometryShader;

        public void Configure(
            Shader geometryShader,
            FirstArtRuinsCliffPrefabBinding3D[] prefabBindings,
            FirstArtRuinsCliffMaterialBinding3D[] materialBindings)
        {
            this.geometryShader = geometryShader;
            this.prefabBindings = prefabBindings == null
                ? null
                : (FirstArtRuinsCliffPrefabBinding3D[])prefabBindings.Clone();
            this.materialBindings = materialBindings == null
                ? null
                : (FirstArtRuinsCliffMaterialBinding3D[])materialBindings.Clone();
        }

        public bool TryValidate(out string error)
        {
            if (geometryShader == null)
            {
                error = "Geometry shader is missing.";
                return false;
            }
            if (!string.Equals(
                    geometryShader.name,
                    FirstArtRuinsCliffCatalog3D.RequiredShaderName,
                    StringComparison.Ordinal))
            {
                error = "Geometry shader must be named " +
                        FirstArtRuinsCliffCatalog3D.RequiredShaderName + ".";
                return false;
            }
            if (!TryValidatePrefabs(out error))
                return false;
            if (!TryValidateMaterials(out error))
                return false;

            error = null;
            return true;
        }

        public bool TryResolvePrefab(string stableId, out GameObject prefab)
        {
            if (prefabBindings != null)
            {
                for (int index = 0; index < prefabBindings.Length; index++)
                {
                    FirstArtRuinsCliffPrefabBinding3D binding = prefabBindings[index];
                    if (binding != null && string.Equals(
                            binding.StableId,
                            stableId,
                            StringComparison.Ordinal))
                    {
                        prefab = binding.Prefab;
                        return prefab != null;
                    }
                }
            }

            prefab = null;
            return false;
        }

        public bool TryResolveMaterial(string role, out Material material)
        {
            if (materialBindings != null)
            {
                for (int index = 0; index < materialBindings.Length; index++)
                {
                    FirstArtRuinsCliffMaterialBinding3D binding =
                        materialBindings[index];
                    if (binding != null && string.Equals(
                            binding.Role,
                            role,
                            StringComparison.Ordinal))
                    {
                        material = binding.Material;
                        return material != null;
                    }
                }
            }

            material = null;
            return false;
        }

        private bool TryValidatePrefabs(out string error)
        {
            if (prefabBindings == null ||
                prefabBindings.Length != FirstArtRuinsCliffCatalog3D.EntryCount)
            {
                error = "Prefab bindings are missing one or more catalog entries.";
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < prefabBindings.Length; index++)
            {
                FirstArtRuinsCliffPrefabBinding3D binding = prefabBindings[index];
                if (binding == null)
                {
                    error = "Prefab binding at index " + index + " is missing.";
                    return false;
                }
                if (!FirstArtRuinsCliffCatalog3D.TryGetEntry(
                        binding.StableId,
                        out FirstArtRuinsCliffCatalogEntry3D entry))
                {
                    error = "Unknown prefab stable ID: " + binding.StableId + ".";
                    return false;
                }
                if (!seen.Add(binding.StableId))
                {
                    error = "Duplicate prefab stable ID: " + binding.StableId + ".";
                    return false;
                }
                string expectedStableId =
                    FirstArtRuinsCliffCatalog3D.Entries[index].StableId;
                if (!string.Equals(
                        binding.StableId,
                        expectedStableId,
                        StringComparison.Ordinal))
                {
                    error = "Prefab binding order must follow the catalog; expected " +
                            expectedStableId + " at index " + index + ".";
                    return false;
                }
                if (binding.Prefab == null)
                {
                    error = "Prefab is missing for " + binding.StableId + ".";
                    return false;
                }

                string expectedName = FileNameWithoutExtension(entry.PrefabPath);
                if (!string.Equals(
                        binding.Prefab.name,
                        expectedName,
                        StringComparison.Ordinal))
                {
                    error = "Prefab name for " + binding.StableId +
                            " must be " + expectedName + ".";
                    return false;
                }
            }

            for (int index = 0;
                 index < FirstArtRuinsCliffCatalog3D.Entries.Count;
                 index++)
            {
                string requiredId =
                    FirstArtRuinsCliffCatalog3D.Entries[index].StableId;
                if (!seen.Contains(requiredId))
                {
                    error = "Prefab binding is missing for " + requiredId + ".";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TryValidateMaterials(out string error)
        {
            if (materialBindings == null ||
                materialBindings.Length !=
                FirstArtRuinsCliffCatalog3D.MaterialRoleCount)
            {
                error = "Material bindings are missing one or more catalog roles.";
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < materialBindings.Length; index++)
            {
                FirstArtRuinsCliffMaterialBinding3D binding =
                    materialBindings[index];
                if (binding == null)
                {
                    error = "Material binding at index " + index + " is missing.";
                    return false;
                }
                if (!FirstArtRuinsCliffCatalog3D.TryGetMaterialRole(
                        binding.Role,
                        out FirstArtRuinsCliffMaterialRole3D role))
                {
                    error = "Unknown material role: " + binding.Role + ".";
                    return false;
                }
                if (!seen.Add(binding.Role))
                {
                    error = "Duplicate material role: " + binding.Role + ".";
                    return false;
                }
                string expectedRole =
                    FirstArtRuinsCliffCatalog3D.MaterialRoles[index].Name;
                if (!string.Equals(
                        binding.Role,
                        expectedRole,
                        StringComparison.Ordinal))
                {
                    error = "Material binding order must follow the catalog; expected " +
                            expectedRole + " at index " + index + ".";
                    return false;
                }
                if (binding.Material == null)
                {
                    error = "Material is missing for " + binding.Role + ".";
                    return false;
                }
                if (!string.Equals(
                        binding.Material.name,
                        role.Name,
                        StringComparison.Ordinal))
                {
                    error = "Material name for " + binding.Role +
                            " must be " + role.Name + ".";
                    return false;
                }
                if (binding.Material.shader != geometryShader)
                {
                    error = "Material shader for " + binding.Role +
                            " must match the geometry shader.";
                    return false;
                }
            }

            for (int index = 0;
                 index < FirstArtRuinsCliffCatalog3D.MaterialRoles.Count;
                 index++)
            {
                string requiredRole =
                    FirstArtRuinsCliffCatalog3D.MaterialRoles[index].Name;
                if (!seen.Contains(requiredRole))
                {
                    error = "Material binding is missing for " + requiredRole + ".";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static string FileNameWithoutExtension(string path)
        {
            int slash = path.LastIndexOf('/');
            int start = slash < 0 ? 0 : slash + 1;
            int dot = path.LastIndexOf('.');
            int length = dot > start ? dot - start : path.Length - start;
            return path.Substring(start, length);
        }
    }
}
