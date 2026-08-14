using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using WasteCity.ArtIntegration3D;

[assembly: InternalsVisibleTo("WasteCity.EditModeTests")]

namespace WasteCity.Editor
{
    [InitializeOnLoad]
    public static class FirstArtRuinsCliffAssetBuilder
    {
        public const string ShaderPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassGeometry.shader";
        public const string ProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset";
        public const string StagingRoot =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/_RuinsCliffAssetStaging";
        public const string RestoreRoot =
            "Library/WasteCity.RuinsCliffAssetRestore";

        private const string RestoreMarkerPath = RestoreRoot + "/transaction.json";
        private const string RestoreMarkerTemporaryPath = RestoreRoot + "/transaction.json.tmp";
        private const string RestoreCommittedMarkerPath = RestoreRoot + "/transaction.committed.json";
        private const string RestoreMarkerVersion = "IDEA-0004-asset-transaction-v2";
        private const string IsolatedTestAssetRoot =
            "Assets/_Game/Tests/EditMode/Temp_RuinsCliffAssetTransaction";
        private const string IsolatedTestRestoreRoot =
            "Library/WasteCity.RuinsCliffAssetRestore.IsolatedTest";
        private const string MaterialDirectory =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry";
        private const string RuinsPrefabDirectory =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs";
        private const string CliffPrefabDirectory =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs";
        private const string BaseColorArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset";
        private const string NormalArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset";
        private const string MaskArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset";
        private const string HeightArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset";

        internal enum PublishPhase
        {
            Material = 1,
            Prefab = 2,
            Profile = 3,
            Save = 4,
            Reimport = 5,
            BeforeFinalValidation = 6,
            AfterFinalValidation = 7,
        }

        internal static Action<PublishPhase, int, string> PublishCheckpoint;
        internal static Action BeforeCommitCheckpoint;
        internal static Action AfterCommitCleanupCheckpoint;

        private static readonly RoleSpec[] RoleSpecs =
        {
            RuinsRole("MAT_Ruins_Concrete", C(0.82, 0.74, 0.62), 0.58f, 2.50f, 0.00f, 0.10f, 0.56f, 0.34f),
            RuinsRole("MAT_Ruins_Aggregate", C(0.68, 0.57, 0.44), 0.90f, 5.00f, 0.00f, 0.02f, 0.88f, 0.58f),
            RuinsRole("MAT_Ruins_DustFilm", C(0.60, 0.50, 0.39), 0.82f, 4.50f, 0.00f, 0.01f, 0.34f, 0.16f),
            RuinsRole("MAT_Ruins_Dust", C(0.78, 0.58, 0.34), 0.88f, 4.00f, 0.00f, 0.01f, 0.78f, 0.48f),
            RuinsRole("MAT_Ruins_DarkFloor", C(0.38, 0.39, 0.38), 0.80f, 2.50f, 0.00f, 0.16f, 0.62f, 0.34f),
            RuinsRole("MAT_Ruins_DrainDark", C(0.20, 0.22, 0.22), 0.90f, 3.00f, 0.00f, 0.28f, 0.60f, 0.30f),
            RuinsRole("MAT_Ruins_Rust", C(0.46, 0.12, 0.025), 0.94f, 6.00f, 0.76f, 0.28f, 0.92f, 0.62f),
            RuinsRole("MAT_Ruins_Marking", C(0.78, 0.34, 0.055), 0.90f, 3.50f, 0.00f, 0.12f, 0.48f, 0.20f),
            CliffRole("MAT_Cliff_Strata", C(0.34, 0.25, 0.18), 0.34f, 5.20f, 0.00f, 0.20f, 0.82f, 0.34f),
            CliffRole("MAT_Cliff_Fracture", C(0.105, 0.083, 0.064), 0.54f, 6.00f, 0.00f, 0.12f, 1.05f, 0.48f),
            CliffRole("MAT_Cliff_Dust", C(0.24, 0.145, 0.075), 0.52f, 7.20f, 0.00f, 0.06f, 0.54f, 0.22f),
            CliffRole("MAT_Cliff_Rubble", C(0.25, 0.18, 0.125), 0.46f, 7.00f, 0.00f, 0.08f, 0.96f, 0.42f),
            CliffRole("MAT_Cliff_Mineral", C(0.30, 0.26, 0.22), 0.44f, 5.50f, 0.18f, 0.28f, 0.72f, 0.28f),
        };

        static FirstArtRuinsCliffAssetBuilder()
        {
            EditorApplication.delayCall -= RecoverInterruptedBuild;
            EditorApplication.delayCall += RecoverInterruptedBuild;
        }

        [MenuItem("WasteCity/Art/Build First Ruins Cliff Runtime Assets")]
        public static void BuildRuntimeAssets()
        {
            RecoverInterruptedBuild();
            Preflight preflight = ResolveAndValidateInputs();
            if (TryValidateFinalSet(preflight, out _))
            {
                DeleteStaging();
                return;
            }

            StagedSet staged = null;
            AssetPublishTransaction transaction = null;
            try
            {
                transaction = AssetPublishTransaction.Begin(
                    DestinationPaths(),
                    OutputDirectories());
                EnsureFolder(StagingRoot);
                staged = BuildAndValidateStaging(preflight);
                Publish(preflight, staged, transaction);
                transaction.Complete();
            }
            catch (Exception operationFailure)
            {
                if (transaction == null)
                    throw;
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException(
                        "Ruins/Cliff asset publication and rollback both failed.",
                        operationFailure,
                        rollbackFailure);
                }
                ExceptionDispatchInfo.Capture(operationFailure).Throw();
                throw;
            }
            finally
            {
                PublishCheckpoint = null;
                BeforeCommitCheckpoint = null;
                AfterCommitCleanupCheckpoint = null;
                transaction?.Dispose();
                DeleteStaging();
            }
        }

        internal static void RecoverInterruptedBuild()
        {
            string marker = ProjectPath(RestoreMarkerPath);
            if (!File.Exists(marker))
            {
                string restoreDirectory = ProjectPath(RestoreRoot);
                if (Directory.Exists(restoreDirectory))
                {
                    try
                    {
                        Directory.Delete(restoreDirectory, true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[IDEA-0004] Could not clean committed/stale restore residue; retrying next initialization: " +
                            exception.Message);
                    }
                }
                DeleteStaging();
                return;
            }
            AssetPublishTransaction.Recover(
                marker,
                DestinationPaths(),
                OutputDirectories());
            DeleteStaging();
        }

        private static Preflight ResolveAndValidateInputs()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !string.Equals(
                    shader.name,
                    FirstArtRuinsCliffCatalog3D.RequiredShaderName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Required Ruins/Cliff geometry shader is missing or incorrectly named: " + ShaderPath);
            }
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The Ruins/Cliff geometry shader has compile errors.");

            Texture2DArray baseColor = LoadArray(BaseColorArrayPath);
            Texture2DArray normal = LoadArray(NormalArrayPath);
            Texture2DArray mask = LoadArray(MaskArrayPath);
            Texture2DArray height = LoadArray(HeightArrayPath);
            var meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            var slotMismatches = new List<string>();
            var calibrationMismatches = new List<string>();
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in FirstArtRuinsCliffCatalog3D.Entries)
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entry.FbxPath)))
                    throw new InvalidOperationException("Approved FBX has no stable GUID: " + entry.FbxPath);
                ModelImporter importer =
                    AssetImporter.GetAtPath(entry.FbxPath) as ModelImporter;
                if (importer == null)
                    throw new InvalidOperationException(
                        "Approved FBX has no ModelImporter: " +
                        entry.FbxPath);
                if (importer.isReadable)
                    throw new InvalidOperationException(
                        "Approved raw FBX must keep Read/Write disabled: " +
                        entry.FbxPath);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.FbxPath);
                if (model == null)
                    throw new InvalidOperationException("Approved FBX is missing: " + entry.FbxPath);
                Mesh[] importedMeshes = AssetDatabase.LoadAllAssetsAtPath(entry.FbxPath)
                    .OfType<Mesh>()
                    .Where(candidate => candidate != null)
                    .ToArray();
                if (importedMeshes.Length != 1)
                {
                    throw new InvalidOperationException(
                        entry.FbxPath + " must import exactly one Mesh; found " + importedMeshes.Length + ".");
                }
                Mesh mesh = importedMeshes[0];
                if (mesh.subMeshCount != entry.MaterialRoles.Count)
                {
                    throw new InvalidOperationException(
                        entry.StableId + " has " + mesh.subMeshCount +
                        " submeshes but its approved role list has " + entry.MaterialRoles.Count + ".");
                }
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length != 1)
                    throw new InvalidOperationException(entry.FbxPath + " must import exactly one Renderer.");
                MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
                if (meshFilters.Length != 1 || meshFilters[0].sharedMesh != mesh)
                    throw new InvalidOperationException(entry.FbxPath + " must import exactly one matching MeshFilter.");
                Matrix4x4 importMatrix = meshFilters[0].transform.localToWorldMatrix;
                ValidateImportedRoot(entry, model, importMatrix);
                Material[] importedSlots = renderers[0].sharedMaterials;
                if (importedSlots.Length != entry.MaterialRoles.Count)
                    throw new InvalidOperationException(entry.FbxPath + " material slot count changed.");
                string[] importedNames = importedSlots
                    .Select(slot => slot == null ? string.Empty : slot.name)
                    .ToArray();
                for (int slot = 0; slot < importedSlots.Length; slot++)
                {
                    string importedName = importedNames[slot];
                    if (!string.Equals(importedName, entry.MaterialRoles[slot], StringComparison.Ordinal))
                    {
                        slotMismatches.Add(
                            entry.FbxPath + " slot " + slot + " must be " +
                            entry.MaterialRoles[slot] + " but is " + importedName + ".");
                    }
                }
                try
                {
                    ValidateCalibratedBounds(entry, mesh, importMatrix);
                }
                catch (Exception exception)
                {
                    calibrationMismatches.Add(exception.Message);
                }
                meshes.Add(entry.StableId, mesh);
                ValidateDestinationType<GameObject>(entry.PrefabPath);
            }
            if (slotMismatches.Count > 0 || calibrationMismatches.Count > 0)
            {
                throw new InvalidOperationException(
                    "Approved Catalog material role order differs from imported FBX slots:\n" +
                    string.Join("\n", slotMismatches) +
                    "\nApproved Catalog calibration differs from imported FBX bounds:\n" +
                    string.Join("\n", calibrationMismatches));
            }
            foreach (RoleSpec spec in RoleSpecs)
                ValidateDestinationType<Material>(MaterialPath(spec.Name));
            ValidateDestinationType<FirstArtRuinsCliffProfile3D>(ProfilePath);
            return new Preflight(shader, baseColor, normal, mask, height, meshes);
        }

        private static Texture2DArray LoadArray(string path)
        {
            Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (array == null)
                throw new InvalidOperationException("Required existing terrain array is missing: " + path);
            if (array.depth != FirstArtTerrainCatalog3D.LayerCount)
                throw new InvalidOperationException(path + " must preserve the approved seven-layer order.");
            return array;
        }

        private static void ValidateDestinationType<T>(string path) where T : UnityEngine.Object
        {
            if (!File.Exists(ProjectPath(path)))
                return;
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Output path contains an incompatible asset: " + path);
        }

        private static StagedSet BuildAndValidateStaging(Preflight preflight)
        {
            string materialStaging = StagingRoot + "/Materials";
            string prefabStaging = StagingRoot + "/Prefabs";
            EnsureFolder(materialStaging);
            EnsureFolder(prefabStaging);
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (RoleSpec spec in RoleSpecs)
            {
                var material = new Material(preflight.Shader) { name = spec.Name };
                ConfigureMaterial(material, spec, preflight);
                string path = materialStaging + "/" + spec.Name + ".mat";
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssetIfDirty(material);
                materials.Add(spec.Name, material);
            }

            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in FirstArtRuinsCliffCatalog3D.Entries)
            {
                var root = new GameObject(FileNameWithoutExtension(entry.PrefabPath));
                try
                {
                    root.transform.localPosition = entry.ChildOffset;
                    root.transform.localRotation =
                        FirstArtRuinsCliffCatalog3D.SourceImportRotation;
                    root.transform.localScale = PrefabTransformScale(
                        entry,
                        preflight.Meshes[entry.StableId].bounds);
                    root.isStatic = true;
                    MeshFilter filter = root.AddComponent<MeshFilter>();
                    filter.sharedMesh = preflight.Meshes[entry.StableId];
                    root.AddComponent<MeshRenderer>().sharedMaterials = entry.MaterialRoles
                        .Select(role => materials[role])
                        .ToArray();
                    string stagingPath = prefabStaging + "/" + root.name + ".prefab";
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, stagingPath);
                    if (prefab == null)
                        throw new InvalidOperationException("Could not stage Prefab for " + entry.StableId + ".");
                    filter.sharedMesh = AddReadableMeshSubAsset(
                        preflight.Meshes[entry.StableId],
                        entry.StableId,
                        stagingPath);
                    prefab = PrefabUtility.SaveAsPrefabAsset(
                        root,
                        stagingPath);
                    if (prefab == null)
                        throw new InvalidOperationException(
                            "Could not embed runtime Mesh for " +
                            entry.StableId + ".");
                    prefabs.Add(entry.StableId, prefab);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            var profile = ScriptableObject.CreateInstance<FirstArtRuinsCliffProfile3D>();
            profile.name = "FirstArtRuinsCliffProfile3D";
            profile.Configure(
                preflight.Shader,
                FirstArtRuinsCliffCatalog3D.Entries.Select(entry =>
                    new FirstArtRuinsCliffPrefabBinding3D(entry.StableId, prefabs[entry.StableId])).ToArray(),
                FirstArtRuinsCliffCatalog3D.MaterialRoles.Select(role =>
                    new FirstArtRuinsCliffMaterialBinding3D(role.Name, materials[role.Name])).ToArray());
            string profileStagingPath = StagingRoot + "/FirstArtRuinsCliffProfile3D.asset";
            AssetDatabase.CreateAsset(profile, profileStagingPath);
            AssetDatabase.SaveAssetIfDirty(profile);
            if (!profile.TryValidate(out string error))
                throw new InvalidOperationException("Staged profile is invalid: " + error);
            ValidateAssetSet(profile, prefabs, materials, preflight, true);
            return new StagedSet(materials, prefabs, profile);
        }

        private static void Publish(
            Preflight preflight,
            StagedSet staged,
            AssetPublishTransaction transaction)
        {
            EnsureOutputFolders();
            int ordinal = 0;
            foreach (RoleSpec spec in RoleSpecs)
            {
                string destination = MaterialPath(spec.Name);
                string staging = AssetDatabase.GetAssetPath(staged.Materials[spec.Name]);
                PublishAsset(staging, destination, staged.Materials[spec.Name]);
                PublishCheckpoint?.Invoke(PublishPhase.Material, ++ordinal, destination);
            }

            Dictionary<string, Material> finalMaterials = FirstArtRuinsCliffCatalog3D.MaterialRoles
                .ToDictionary(
                    role => role.Name,
                    role => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(role.Name)),
                    StringComparer.Ordinal);
            ordinal = 0;
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in FirstArtRuinsCliffCatalog3D.Entries)
            {
                string stagingPath = AssetDatabase.GetAssetPath(staged.Prefabs[entry.StableId]);
                string destination = entry.PrefabPath;
                if (File.Exists(ProjectPath(destination)))
                {
                    // Load the destination itself before saving it back.  Loading the
                    // staging Prefab here imports the staging object's fileID layout
                    // into the destination.  A staging component fileID can collide
                    // with the destination's embedded Mesh fileID, causing Unity to
                    // silently allocate a replacement local fileID for that Mesh.
                    GameObject contents = PrefabUtility.LoadPrefabContents(destination);
                    try
                    {
                        ConfigureExistingPrefabContents(
                            contents,
                            entry,
                            preflight.Meshes[entry.StableId].bounds);
                        contents.GetComponent<MeshFilter>().sharedMesh =
                            UpdateReadableMeshSubAssetInPlace(
                                preflight.Meshes[entry.StableId],
                                entry.StableId,
                                destination);
                        contents.GetComponent<MeshRenderer>().sharedMaterials = entry.MaterialRoles
                            .Select(role => finalMaterials[role])
                            .ToArray();
                        if (PrefabUtility.SaveAsPrefabAsset(contents, destination) == null)
                            throw new InvalidOperationException("Could not update Prefab: " + destination);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
                else
                {
                    MoveAsset(stagingPath, destination);
                }
                PublishCheckpoint?.Invoke(PublishPhase.Prefab, ++ordinal, destination);
            }

            Dictionary<string, GameObject> finalPrefabs = FirstArtRuinsCliffCatalog3D.Entries
                .ToDictionary(
                    entry => entry.StableId,
                    entry => AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath),
                    StringComparer.Ordinal);
            FirstArtRuinsCliffProfile3D finalProfile =
                AssetDatabase.LoadAssetAtPath<
                    FirstArtRuinsCliffProfile3D>(ProfilePath);
            if (!ProfileMatchesExpectedBindings(
                    finalProfile,
                    preflight.Shader,
                    finalPrefabs,
                    finalMaterials))
            {
                staged.Profile.Configure(
                    preflight.Shader,
                    FirstArtRuinsCliffCatalog3D.Entries.Select(entry =>
                        new FirstArtRuinsCliffPrefabBinding3D(
                            entry.StableId,
                            finalPrefabs[entry.StableId])).ToArray(),
                    FirstArtRuinsCliffCatalog3D.MaterialRoles.Select(role =>
                        new FirstArtRuinsCliffMaterialBinding3D(
                            role.Name,
                            finalMaterials[role.Name])).ToArray());
                AssetDatabase.SaveAssetIfDirty(staged.Profile);
                string stagedProfilePath =
                    AssetDatabase.GetAssetPath(staged.Profile);
                PublishAsset(
                    stagedProfilePath,
                    ProfilePath,
                    staged.Profile);
            }
            PublishCheckpoint?.Invoke(PublishPhase.Profile, 1, ProfilePath);

            foreach (string path in DestinationPaths())
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                    AssetDatabase.SaveAssetIfDirty(asset);
            }
            PublishCheckpoint?.Invoke(PublishPhase.Save, 1, ProfilePath);
            ordinal = 0;
            foreach (string path in DestinationPaths())
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                PublishCheckpoint?.Invoke(PublishPhase.Reimport, ++ordinal, path);
            }
            PublishCheckpoint?.Invoke(PublishPhase.BeforeFinalValidation, 1, ProfilePath);
            if (!TryValidateFinalSet(preflight, out string error))
                throw new InvalidOperationException("Published Ruins/Cliff assets are invalid: " + error);
            PublishCheckpoint?.Invoke(PublishPhase.AfterFinalValidation, 1, ProfilePath);
        }

        private static void ConfigureExistingPrefabContents(
            GameObject contents,
            FirstArtRuinsCliffCatalogEntry3D entry,
            Bounds sourceBounds)
        {
            if (contents == null)
                throw new InvalidOperationException(
                    "Could not load existing Prefab contents for " +
                    entry.StableId + ".");

            while (contents.transform.childCount > 0)
                UnityEngine.Object.DestroyImmediate(
                    contents.transform.GetChild(0).gameObject);

            foreach (Component component in contents.GetComponents<Component>())
            {
                if (component is Transform ||
                    component is MeshFilter ||
                    component is MeshRenderer)
                    continue;
                UnityEngine.Object.DestroyImmediate(component);
            }

            contents.name = FileNameWithoutExtension(entry.PrefabPath);
            contents.transform.localPosition = entry.ChildOffset;
            contents.transform.localRotation =
                FirstArtRuinsCliffCatalog3D.SourceImportRotation;
            contents.transform.localScale = PrefabTransformScale(
                entry,
                sourceBounds);
            contents.isStatic = true;
            if (contents.GetComponent<MeshFilter>() == null)
                contents.AddComponent<MeshFilter>();
            if (contents.GetComponent<MeshRenderer>() == null)
                contents.AddComponent<MeshRenderer>();
        }

        private static void PublishAsset(
            string stagingPath,
            string destination,
            UnityEngine.Object stagedAsset)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(destination);
            if (existing == null)
            {
                MoveAsset(stagingPath, destination);
                return;
            }
            EditorUtility.CopySerialized(stagedAsset, existing);
            AssetDatabase.SaveAssetIfDirty(existing);
        }

        private static bool ProfileMatchesExpectedBindings(
            FirstArtRuinsCliffProfile3D profile,
            Shader shader,
            IReadOnlyDictionary<string, GameObject> prefabs,
            IReadOnlyDictionary<string, Material> materials)
        {
            if (profile == null ||
                profile.GeometryShader != shader ||
                profile.PrefabBindings.Count !=
                FirstArtRuinsCliffCatalog3D.EntryCount ||
                profile.MaterialBindings.Count !=
                FirstArtRuinsCliffCatalog3D.MaterialRoleCount)
                return false;
            for (var index = 0;
                 index < FirstArtRuinsCliffCatalog3D.EntryCount;
                 index++)
            {
                FirstArtRuinsCliffCatalogEntry3D entry =
                    FirstArtRuinsCliffCatalog3D.Entries[index];
                FirstArtRuinsCliffPrefabBinding3D binding =
                    profile.PrefabBindings[index];
                if (binding == null ||
                    !string.Equals(
                        binding.StableId,
                        entry.StableId,
                        StringComparison.Ordinal) ||
                    binding.Prefab != prefabs[entry.StableId])
                    return false;
            }
            for (var index = 0;
                 index < FirstArtRuinsCliffCatalog3D.MaterialRoleCount;
                 index++)
            {
                FirstArtRuinsCliffMaterialRole3D role =
                    FirstArtRuinsCliffCatalog3D.MaterialRoles[index];
                FirstArtRuinsCliffMaterialBinding3D binding =
                    profile.MaterialBindings[index];
                if (binding == null ||
                    !string.Equals(
                        binding.Role,
                        role.Name,
                        StringComparison.Ordinal) ||
                    binding.Material != materials[role.Name])
                    return false;
            }
            return true;
        }

        private static void MoveAsset(string source, string destination)
        {
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Could not publish " + destination + ": " + error);
        }

        private static bool TryValidateFinalSet(Preflight preflight, out string error)
        {
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (RoleSpec spec in RoleSpecs)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(spec.Name));
                if (material == null)
                {
                    error = "Missing Material: " + spec.Name;
                    return false;
                }
                materials.Add(spec.Name, material);
            }
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in FirstArtRuinsCliffCatalog3D.Entries)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
                if (prefab == null)
                {
                    error = "Missing Prefab: " + entry.StableId;
                    return false;
                }
                prefabs.Add(entry.StableId, prefab);
            }
            FirstArtRuinsCliffProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(ProfilePath);
            if (profile == null)
            {
                error = "Missing geometry Profile.";
                return false;
            }
            try
            {
                ValidateAssetSet(profile, prefabs, materials, preflight, false);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void ValidateAssetSet(
            FirstArtRuinsCliffProfile3D profile,
            IReadOnlyDictionary<string, GameObject> prefabs,
            IReadOnlyDictionary<string, Material> materials,
            Preflight preflight,
            bool staging)
        {
            if (!profile.TryValidate(out string error))
                throw new InvalidOperationException(error);
            foreach (RoleSpec spec in RoleSpecs)
            {
                Material material = materials[spec.Name];
                if (material.shader != preflight.Shader)
                    throw new InvalidOperationException(spec.Name + " uses the wrong shader.");
                ValidateMaterialValue(material, spec, preflight);
            }
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in FirstArtRuinsCliffCatalog3D.Entries)
            {
                GameObject prefab = prefabs[entry.StableId];
                Component[] components = prefab.GetComponents<Component>();
                if (components.Length != 3 ||
                    prefab.GetComponent<MeshFilter>() == null ||
                    prefab.GetComponent<MeshRenderer>() == null ||
                    prefab.transform.childCount != 0)
                    throw new InvalidOperationException(entry.StableId + " is not a mesh-only Prefab.");
                Mesh runtimeMesh =
                    prefab.GetComponent<MeshFilter>().sharedMesh;
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                Mesh approvedEmbeddedMesh = RequireSoleApprovedRuntimeMesh(
                    prefabPath,
                    entry.StableId);
                if (runtimeMesh == null ||
                    runtimeMesh != approvedEmbeddedMesh ||
                    !EditorUtility.IsPersistent(runtimeMesh) ||
                    !runtimeMesh.isReadable ||
                    !string.Equals(
                        AssetDatabase.GetAssetPath(runtimeMesh),
                        prefabPath,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        entry.StableId +
                        " must embed one persistent readable runtime Mesh in its Prefab.");
                Mesh approvedMesh = preflight.Meshes[entry.StableId];
                if (!MeshContentMatches(approvedMesh, runtimeMesh))
                    throw new InvalidOperationException(
                        entry.StableId +
                        " embedded runtime Mesh differs from the approved FBX Mesh.");
                Matrix4x4 approvedMatrix = CalibrationMatrix(entry);
                if (!MatrixApproximately(
                        prefab.transform.localToWorldMatrix,
                        approvedMatrix,
                        0.0000002f))
                    throw new InvalidOperationException(entry.StableId + " does not mirror Catalog calibration.");
                Bounds prefabBounds = TransformBounds(
                    prefab.GetComponent<MeshFilter>().sharedMesh.bounds,
                    prefab.transform.localToWorldMatrix);
                if (Mathf.Abs(prefabBounds.center.x) > 0.0000002f ||
                    Mathf.Abs(prefabBounds.center.z) > 0.0000002f ||
                    Mathf.Abs(prefabBounds.min.y) > 0.0000002f ||
                    !Approximately(prefabBounds.size, entry.CalibratedBounds, 0.0000002f))
                    throw new InvalidOperationException(
                        entry.StableId + " Prefab bounds exceed the approved calibration tolerance: center=" +
                        prefabBounds.center.ToString("R") + ", minY=" + prefabBounds.min.y.ToString("R") +
                        ", size=" + prefabBounds.size.ToString("R") + ", target=" +
                        entry.CalibratedBounds.ToString("R") + ".");
                Material[] slots = prefab.GetComponent<MeshRenderer>().sharedMaterials;
                if (slots.Length != entry.MaterialRoles.Count)
                    throw new InvalidOperationException(entry.StableId + " has the wrong shared Material count.");
                for (int index = 0; index < slots.Length; index++)
                {
                    if (slots[index] == null ||
                        !string.Equals(slots[index].name, entry.MaterialRoles[index], StringComparison.Ordinal))
                        throw new InvalidOperationException(entry.StableId + " has the wrong role at slot " + index + ".");
                    if (!staging && slots[index] != materials[entry.MaterialRoles[index]])
                        throw new InvalidOperationException(entry.StableId + " does not use the shared final Material.");
                }
            }
        }

        internal static bool MeshContentMatchesForTests(
            Mesh approved,
            Mesh runtime)
        {
            return MeshContentMatches(approved, runtime);
        }

        private static bool MeshContentMatches(
            Mesh approved,
            Mesh runtime)
        {
            if (approved == null || runtime == null ||
                approved.vertexCount != runtime.vertexCount ||
                approved.subMeshCount != runtime.subMeshCount ||
                approved.indexFormat != runtime.indexFormat ||
                approved.blendShapeCount != 0 ||
                runtime.blendShapeCount != 0 ||
                !BoundsExactlyEqual(approved.bounds, runtime.bounds))
                return false;

            VertexAttributeDescriptor[] approvedAttributes =
                approved.GetVertexAttributes();
            VertexAttributeDescriptor[] runtimeAttributes =
                runtime.GetVertexAttributes();
            if (approvedAttributes.Length != runtimeAttributes.Length)
                return false;
            for (var index = 0;
                 index < approvedAttributes.Length;
                 index++)
                if (!VertexAttributeExactlyEqual(
                        approvedAttributes[index],
                        runtimeAttributes[index]))
                    return false;

            Mesh.MeshDataArray approvedData = default;
            Mesh.MeshDataArray runtimeData = default;
            var approvedAllocated = false;
            var runtimeAllocated = false;
            try
            {
                approvedData = Mesh.AcquireReadOnlyMeshData(approved);
                approvedAllocated = true;
                runtimeData = Mesh.AcquireReadOnlyMeshData(runtime);
                runtimeAllocated = true;
                Mesh.MeshData approvedMeshData = approvedData[0];
                Mesh.MeshData runtimeMeshData = runtimeData[0];
                if (approvedMeshData.vertexCount !=
                        runtimeMeshData.vertexCount ||
                    approvedMeshData.vertexBufferCount !=
                        runtimeMeshData.vertexBufferCount ||
                    approvedMeshData.indexFormat !=
                        runtimeMeshData.indexFormat ||
                    approvedMeshData.subMeshCount !=
                        runtimeMeshData.subMeshCount)
                    return false;

                for (var stream = 0;
                     stream < approvedMeshData.vertexBufferCount;
                     stream++)
                {
                    if (approvedMeshData.GetVertexBufferStride(stream) !=
                        runtimeMeshData.GetVertexBufferStride(stream))
                        return false;
                    if (!BytesExactlyEqual(
                            approvedMeshData.GetVertexData<byte>(stream),
                            runtimeMeshData.GetVertexData<byte>(stream)))
                        return false;
                }

                if (!BytesExactlyEqual(
                        approvedMeshData.GetIndexData<byte>(),
                        runtimeMeshData.GetIndexData<byte>()))
                    return false;
                for (var subMesh = 0;
                     subMesh < approvedMeshData.subMeshCount;
                     subMesh++)
                    if (!SubMeshExactlyEqual(
                            approvedMeshData.GetSubMesh(subMesh),
                            runtimeMeshData.GetSubMesh(subMesh)))
                        return false;
                return true;
            }
            finally
            {
                if (runtimeAllocated)
                    runtimeData.Dispose();
                if (approvedAllocated)
                    approvedData.Dispose();
            }
        }

        private static bool VertexAttributeExactlyEqual(
            VertexAttributeDescriptor approved,
            VertexAttributeDescriptor runtime)
        {
            return approved.attribute == runtime.attribute &&
                   approved.format == runtime.format &&
                   approved.dimension == runtime.dimension &&
                   approved.stream == runtime.stream;
        }

        private static bool BytesExactlyEqual(
            NativeArray<byte> approved,
            NativeArray<byte> runtime)
        {
            if (approved.Length != runtime.Length)
                return false;
            for (var index = 0; index < approved.Length; index++)
                if (approved[index] != runtime[index])
                    return false;
            return true;
        }

        private static bool SubMeshExactlyEqual(
            SubMeshDescriptor approved,
            SubMeshDescriptor runtime)
        {
            return approved.indexStart == runtime.indexStart &&
                   approved.indexCount == runtime.indexCount &&
                   approved.topology == runtime.topology &&
                   approved.baseVertex == runtime.baseVertex &&
                   approved.firstVertex == runtime.firstVertex &&
                   approved.vertexCount == runtime.vertexCount &&
                   BoundsExactlyEqual(approved.bounds, runtime.bounds);
        }

        private static bool BoundsExactlyEqual(
            Bounds approved,
            Bounds runtime)
        {
            return VectorExactlyEqual(approved.center, runtime.center) &&
                   VectorExactlyEqual(approved.size, runtime.size);
        }

        private static bool VectorExactlyEqual(
            Vector3 approved,
            Vector3 runtime)
        {
            return approved.x == runtime.x &&
                   approved.y == runtime.y &&
                   approved.z == runtime.z;
        }

        private static Mesh AddReadableMeshSubAsset(
            Mesh source,
            string stableId,
            string prefabPath)
        {
            Mesh runtimeMesh = CreateReadableMeshCopy(
                source,
                stableId + "_RuntimeMesh");
            try
            {
                AssetDatabase.AddObjectToAsset(
                    runtimeMesh,
                    prefabPath);
                AssetDatabase.SaveAssetIfDirty(runtimeMesh);
                return runtimeMesh;
            }
            catch
            {
                if (!EditorUtility.IsPersistent(runtimeMesh))
                    UnityEngine.Object.DestroyImmediate(runtimeMesh);
                throw;
            }
        }

        private static Mesh CreateReadableMeshCopy(
            Mesh source,
            string name)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.blendShapeCount != 0)
                throw new InvalidOperationException(
                    source.name +
                    " uses unsupported blend shapes for a static runtime Prefab.");

            Mesh.MeshDataArray readable = default;
            Mesh.MeshDataArray writable = default;
            bool readableAllocated = false;
            bool writableAllocated = false;
            Mesh copy = null;
            try
            {
                readable = Mesh.AcquireReadOnlyMeshData(source);
                readableAllocated = true;
                writable = Mesh.AllocateWritableMeshData(1);
                writableAllocated = true;
                Mesh.MeshData input = readable[0];
                Mesh.MeshData output = writable[0];
                output.SetVertexBufferParams(
                    input.vertexCount,
                    source.GetVertexAttributes());
                for (var stream = 0;
                     stream < input.vertexBufferCount;
                     stream++)
                {
                    var sourceData = input.GetVertexData<byte>(stream);
                    var destinationData =
                        output.GetVertexData<byte>(stream);
                    if (sourceData.Length != destinationData.Length)
                        throw new InvalidOperationException(
                            source.name +
                            " vertex stream size changed while baking a runtime copy.");
                    sourceData.CopyTo(destinationData);
                }

                var sourceIndices = input.GetIndexData<byte>();
                int bytesPerIndex = input.indexFormat == IndexFormat.UInt16
                    ? sizeof(ushort)
                    : sizeof(uint);
                if (sourceIndices.Length % bytesPerIndex != 0)
                    throw new InvalidOperationException(
                        source.name +
                        " has a malformed index buffer.");
                output.SetIndexBufferParams(
                    sourceIndices.Length / bytesPerIndex,
                    input.indexFormat);
                sourceIndices.CopyTo(output.GetIndexData<byte>());
                output.subMeshCount = input.subMeshCount;
                for (var subMesh = 0;
                     subMesh < input.subMeshCount;
                     subMesh++)
                    output.SetSubMesh(
                        subMesh,
                        input.GetSubMesh(subMesh),
                        MeshUpdateFlags.DontRecalculateBounds |
                        MeshUpdateFlags.DontValidateIndices);

                copy = new Mesh { name = name };
                Mesh.ApplyAndDisposeWritableMeshData(
                    writable,
                    copy,
                    MeshUpdateFlags.DontRecalculateBounds |
                    MeshUpdateFlags.DontValidateIndices);
                writableAllocated = false;
                copy.bounds = source.bounds;
                if (!copy.isReadable)
                    throw new InvalidOperationException(
                        source.name +
                        " runtime Mesh copy is unexpectedly unreadable.");
                return copy;
            }
            catch
            {
                if (copy != null)
                    UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }
            finally
            {
                if (writableAllocated)
                    writable.Dispose();
                if (readableAllocated)
                    readable.Dispose();
            }
        }

        private static Mesh UpdateReadableMeshSubAssetInPlace(
            Mesh source,
            string stableId,
            string prefabPath)
        {
            string approvedName = stableId + "_RuntimeMesh";
            Mesh persistentMesh = RequireSoleApprovedRuntimeMesh(
                prefabPath,
                stableId);
            Mesh readableCopy = CreateReadableMeshCopy(source, approvedName);
            try
            {
                EditorUtility.CopySerialized(readableCopy, persistentMesh);
                persistentMesh.name = approvedName;
                AssetDatabase.SaveAssetIfDirty(persistentMesh);
                return persistentMesh;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readableCopy);
            }
        }

        private static Mesh RequireSoleApprovedRuntimeMesh(
            string prefabPath,
            string stableId)
        {
            string approvedName = stableId + "_RuntimeMesh";
            Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(prefabPath)
                .OfType<Mesh>()
                .ToArray();
            if (meshes.Length != 1 || !string.Equals(
                    meshes[0].name,
                    approvedName,
                    StringComparison.Ordinal))
            {
                string found = meshes.Length == 0
                    ? "none"
                    : string.Join(", ", meshes.Select(mesh => mesh.name));
                throw new InvalidOperationException(
                    prefabPath + " must contain exactly one approved embedded runtime Mesh named " +
                    approvedName + "; found " + meshes.Length + " (" + found + ").");
            }
            return meshes[0];
        }

        private static void ConfigureMaterial(Material material, RoleSpec spec, Preflight preflight)
        {
            material.SetTexture("_BaseColorArray", preflight.BaseColor);
            material.SetTexture("_NormalArray", preflight.Normal);
            material.SetTexture("_MaskArray", preflight.Mask);
            material.SetTexture("_HeightArray", preflight.Height);
            material.SetFloat("_LayerIndex", spec.Family == FirstArtRuinsCliffFamily3D.Ruins ? 4f : 6f);
            material.SetFloat("_TriplanarScale", spec.TriplanarScale);
            material.SetColor("_RoleTint", spec.Tint);
            material.SetFloat("_RoleTintStrength", spec.TintStrength);
            material.SetFloat("_MetallicScale", spec.Metallic);
            material.SetFloat("_SmoothnessScale", spec.Smoothness);
            material.SetFloat("_OcclusionStrength", spec.OcclusionStrength);
            material.SetFloat("_NormalStrength", spec.NormalStrength);
            material.SetFloat("_HeightStrength", spec.HeightStrength);
            material.enableInstancing = true;
        }

        private static void ValidateMaterialValue(Material material, RoleSpec spec, Preflight preflight)
        {
            if (material.GetTexture("_BaseColorArray") != preflight.BaseColor ||
                material.GetTexture("_NormalArray") != preflight.Normal ||
                material.GetTexture("_MaskArray") != preflight.Mask ||
                material.GetTexture("_HeightArray") != preflight.Height)
                throw new InvalidOperationException(spec.Name + " does not share the approved four arrays.");
            float expectedLayer = spec.Family == FirstArtRuinsCliffFamily3D.Ruins ? 4f : 6f;
            if (material.GetFloat("_LayerIndex") != expectedLayer ||
                !material.enableInstancing ||
                !Approximately(material.GetColor("_RoleTint"), spec.Tint) ||
                !Mathf.Approximately(material.GetFloat("_RoleTintStrength"), spec.TintStrength) ||
                !Mathf.Approximately(material.GetFloat("_TriplanarScale"), spec.TriplanarScale) ||
                !Mathf.Approximately(material.GetFloat("_MetallicScale"), spec.Metallic) ||
                !Mathf.Approximately(material.GetFloat("_SmoothnessScale"), spec.Smoothness) ||
                !Mathf.Approximately(material.GetFloat("_OcclusionStrength"), spec.OcclusionStrength) ||
                !Mathf.Approximately(material.GetFloat("_NormalStrength"), spec.NormalStrength) ||
                !Mathf.Approximately(material.GetFloat("_HeightStrength"), spec.HeightStrength))
                throw new InvalidOperationException(spec.Name + " role parameters changed.");
        }

        private static void ValidateCalibratedBounds(
            FirstArtRuinsCliffCatalogEntry3D entry,
            Mesh mesh,
            Matrix4x4 importMatrix)
        {
            Matrix4x4 finalMatrix = Matrix4x4.TRS(
                entry.ChildOffset,
                Quaternion.identity,
                entry.RootScale) * importMatrix;
            Bounds bounds = TransformBounds(mesh.bounds, finalMatrix);
            Vector3 derivedOffset = DeriveChildOffset(
                mesh.bounds,
                importMatrix,
                entry.RootScale);
            if (!Approximately(derivedOffset, entry.ChildOffset, 0.0000002f) ||
                Mathf.Abs(bounds.center.x) > 0.0000002f ||
                Mathf.Abs(bounds.center.z) > 0.0000002f ||
                Mathf.Abs(bounds.min.y) > 0.0000002f ||
                bounds.size.x > 0.9002f ||
                bounds.size.z > 0.9002f ||
                !Approximately(bounds.size, entry.CalibratedBounds, 0.0000002f))
                throw new InvalidOperationException(entry.StableId + " no longer matches approved single-cell calibration.");
        }

        private static void ValidateImportedRoot(
            FirstArtRuinsCliffCatalogEntry3D entry,
            GameObject model,
            Matrix4x4 importMatrix)
        {
            if (model.transform.childCount != 0 ||
                model.GetComponentsInChildren<Transform>(true).Length != 1 ||
                !Approximately(model.transform.localPosition, Vector3.zero) ||
                !Approximately(model.transform.localScale, Vector3.one) ||
                !QuaternionApproximately(
                    model.transform.localRotation,
                    FirstArtRuinsCliffCatalog3D.SourceImportRotation,
                    0.0000002f) ||
                !MatrixApproximately(
                    importMatrix,
                    entry.SourceImportMatrix,
                    0.0000002f))
            {
                throw new InvalidOperationException(
                    entry.FbxPath + " imported root transform no longer matches the approved source-import truth.");
            }
        }

        private static Vector3 DeriveChildOffset(
            Bounds rawBounds,
            Matrix4x4 importMatrix,
            Vector3 rootScale)
        {
            Bounds imported = TransformBounds(rawBounds, importMatrix);
            Vector3 scaledCenter = Vector3.Scale(imported.center, rootScale);
            Vector3 scaledExtents = Vector3.Scale(imported.extents, rootScale);
            return new Vector3(
                -scaledCenter.x,
                -(scaledCenter.y - scaledExtents.y),
                -scaledCenter.z);
        }

        private static Matrix4x4 CalibrationMatrix(
            FirstArtRuinsCliffCatalogEntry3D entry)
        {
            return Matrix4x4.Translate(entry.ChildOffset) *
                   Matrix4x4.Scale(entry.RootScale) *
                   entry.SourceImportMatrix;
        }

        private static Vector3 PrefabTransformScale(
            FirstArtRuinsCliffCatalogEntry3D entry,
            Bounds rawBounds)
        {
            if (entry.Family == FirstArtRuinsCliffFamily3D.Ruins)
                return new Vector3(entry.RootScale.x, entry.RootScale.z, entry.RootScale.y);
            Matrix4x4 rotation = Matrix4x4.Rotate(
                FirstArtRuinsCliffCatalog3D.SourceImportRotation);
            float a = Mathf.Abs(rotation.m11) * rawBounds.size.y;
            float b = Mathf.Abs(rotation.m12) * rawBounds.size.z;
            float c = Mathf.Abs(rotation.m21) * rawBounds.size.y;
            float d = Mathf.Abs(rotation.m22) * rawBounds.size.z;
            float determinant = a * d - b * c;
            if (Mathf.Abs(determinant) <= float.Epsilon)
                throw new InvalidOperationException(entry.StableId + " Prefab scale calibration is singular.");
            return new Vector3(
                entry.RootScale.x,
                (entry.CalibratedBounds.y * d - b * entry.CalibratedBounds.z) /
                determinant,
                (a * entry.CalibratedBounds.z - entry.CalibratedBounds.y * c) /
                determinant);
        }

        private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(source.center);
            Vector3 extents = source.extents;
            Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            return new Bounds(center, new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
        }

        private static string[] DestinationPaths()
        {
            return RoleSpecs.Select(spec => MaterialPath(spec.Name))
                .Concat(FirstArtRuinsCliffCatalog3D.Entries.Select(entry => entry.PrefabPath))
                .Concat(new[] { ProfilePath })
                .ToArray();
        }

        private static string[] OutputDirectories()
        {
            return new[]
            {
                MaterialDirectory,
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime",
                RuinsPrefabDirectory,
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime",
                CliffPrefabDirectory,
            };
        }

        private static void EnsureOutputFolders()
        {
            EnsureFolder(MaterialDirectory);
            EnsureFolder(RuinsPrefabDirectory);
            EnsureFolder(CliffPrefabDirectory);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                throw new InvalidOperationException("Invalid project folder path: " + path);
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Could not create project folder: " + path);
        }

        private static void DeleteStaging()
        {
            if (AssetDatabase.IsValidFolder(StagingRoot))
                AssetDatabase.DeleteAsset(StagingRoot);
        }

        private static string MaterialPath(string role)
        {
            return MaterialDirectory + "/" + role + ".mat";
        }

        private static string FileNameWithoutExtension(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        private static string ProjectPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative));
        }

        private static bool Approximately(Vector3 a, Vector3 b, float tolerance = 0.000001f)
        {
            return Mathf.Abs(a.x - b.x) <= tolerance &&
                   Mathf.Abs(a.y - b.y) <= tolerance &&
                   Mathf.Abs(a.z - b.z) <= tolerance;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= 0.000001f &&
                   Mathf.Abs(a.g - b.g) <= 0.000001f &&
                   Mathf.Abs(a.b - b.b) <= 0.000001f &&
                   Mathf.Abs(a.a - b.a) <= 0.000001f;
        }

        private static bool QuaternionApproximately(
            Quaternion a,
            Quaternion b,
            float tolerance)
        {
            return 1f - Mathf.Abs(Quaternion.Dot(a, b)) <= tolerance;
        }

        private static bool MatrixApproximately(
            Matrix4x4 a,
            Matrix4x4 b,
            float tolerance)
        {
            for (int index = 0; index < 16; index++)
            {
                if (Mathf.Abs(a[index] - b[index]) > tolerance)
                    return false;
            }
            return true;
        }

        private static Color C(double r, double g, double b)
        {
            return new Color((float)r, (float)g, (float)b, 1f);
        }

        private static RoleSpec RuinsRole(
            string name, Color tint, float tintStrength, float scale,
            float metallic, float smoothness, float normal, float height)
        {
            return new RoleSpec(name, FirstArtRuinsCliffFamily3D.Ruins, tint,
                tintStrength, scale, metallic, smoothness, 0.30f, normal, height);
        }

        private static RoleSpec CliffRole(
            string name, Color tint, float tintStrength, float scale,
            float metallic, float smoothness, float normal, float height)
        {
            return new RoleSpec(name, FirstArtRuinsCliffFamily3D.Cliff, tint,
                tintStrength, scale, metallic, smoothness, 1.00f, normal, height);
        }

        internal static void LeaveRecoveryMarkerForTests()
        {
            RecoverInterruptedBuild();
            AssetPublishTransaction.Begin(DestinationPaths(), OutputDirectories());
        }

        internal static void RunIsolatedFirstBuildRollbackForTests()
        {
            string absoluteRoot = ProjectPath(IsolatedTestAssetRoot);
            if (Directory.Exists(absoluteRoot) || File.Exists(absoluteRoot + ".meta"))
                throw new InvalidOperationException(
                    "Refusing to reuse a pre-existing isolated Ruins/Cliff transaction root.");
            string isolatedRestoreDirectory = ProjectPath(IsolatedTestRestoreRoot);
            if (Directory.Exists(isolatedRestoreDirectory))
                throw new InvalidOperationException(
                    "Refusing to reuse pre-existing isolated transaction evidence.");

            string[] directories =
            {
                IsolatedTestAssetRoot,
                IsolatedTestAssetRoot + "/Geometry",
                IsolatedTestAssetRoot + "/Ruins",
                IsolatedTestAssetRoot + "/Ruins/Runtime",
                IsolatedTestAssetRoot + "/Ruins/Runtime/Prefabs",
                IsolatedTestAssetRoot + "/Cliff",
                IsolatedTestAssetRoot + "/Cliff/Runtime",
                IsolatedTestAssetRoot + "/Cliff/Runtime/Prefabs",
            };
            string[] assets =
            {
                IsolatedTestAssetRoot + "/Geometry/TestGeometry.mat",
                IsolatedTestAssetRoot + "/Ruins/Runtime/Prefabs/TestRuins.mat",
                IsolatedTestAssetRoot + "/Cliff/Runtime/Prefabs/TestCliff.mat",
            };
            AssetPublishTransaction transaction = AssetPublishTransaction.BeginIsolatedForTests(
                assets,
                directories);
            try
            {
                foreach (string directory in directories)
                    EnsureFolder(directory);
                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                if (shader == null)
                    throw new InvalidOperationException("Could not resolve the isolated-test material shader.");
                foreach (string assetPath in assets)
                    AssetDatabase.CreateAsset(new Material(shader), assetPath);
                AssetDatabase.SaveAssets();
                if (assets.Any(assetPath => AssetDatabase.LoadAssetAtPath<Material>(assetPath) == null))
                    throw new InvalidOperationException("The isolated first-build fixture did not publish all assets.");
                transaction.Rollback();
            }
            catch (Exception operationFailure)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException(
                        "Isolated transaction setup and rollback both failed.",
                        operationFailure,
                        rollbackFailure);
                }
                ExceptionDispatchInfo.Capture(operationFailure).Throw();
                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }

        private sealed class Preflight
        {
            public Preflight(
                Shader shader, Texture2DArray baseColor, Texture2DArray normal,
                Texture2DArray mask, Texture2DArray height,
                IReadOnlyDictionary<string, Mesh> meshes)
            {
                Shader = shader;
                BaseColor = baseColor;
                Normal = normal;
                Mask = mask;
                Height = height;
                Meshes = meshes;
            }
            public Shader Shader { get; }
            public Texture2DArray BaseColor { get; }
            public Texture2DArray Normal { get; }
            public Texture2DArray Mask { get; }
            public Texture2DArray Height { get; }
            public IReadOnlyDictionary<string, Mesh> Meshes { get; }
        }

        private sealed class StagedSet
        {
            public StagedSet(
                IReadOnlyDictionary<string, Material> materials,
                IReadOnlyDictionary<string, GameObject> prefabs,
                FirstArtRuinsCliffProfile3D profile)
            {
                Materials = materials;
                Prefabs = prefabs;
                Profile = profile;
            }
            public IReadOnlyDictionary<string, Material> Materials { get; }
            public IReadOnlyDictionary<string, GameObject> Prefabs { get; }
            public FirstArtRuinsCliffProfile3D Profile { get; }
        }

        private readonly struct RoleSpec
        {
            public RoleSpec(
                string name, FirstArtRuinsCliffFamily3D family, Color tint,
                float tintStrength, float triplanarScale, float metallic,
                float smoothness, float occlusionStrength,
                float normalStrength, float heightStrength)
            {
                Name = name;
                Family = family;
                Tint = tint;
                TintStrength = tintStrength;
                TriplanarScale = triplanarScale;
                Metallic = metallic;
                Smoothness = smoothness;
                OcclusionStrength = occlusionStrength;
                NormalStrength = normalStrength;
                HeightStrength = heightStrength;
            }
            public string Name { get; }
            public FirstArtRuinsCliffFamily3D Family { get; }
            public Color Tint { get; }
            public float TintStrength { get; }
            public float TriplanarScale { get; }
            public float Metallic { get; }
            public float Smoothness { get; }
            public float OcclusionStrength { get; }
            public float NormalStrength { get; }
            public float HeightStrength { get; }
        }

        [Serializable]
        private sealed class RestoreManifest
        {
            public string version = RestoreMarkerVersion;
            public RestoreEntry[] entries;
            public string[] originallyMissingDirectories;
        }

        [Serializable]
        private sealed class RestoreEntry
        {
            public string assetPath;
            public bool existed;
            public string guid;
            public string backupAsset;
            public string backupMeta;
            public string assetSha256;
            public string metaSha256;
        }

        private sealed class AssetPublishTransaction : IDisposable
        {
            private readonly string markerPath;
            private readonly string committedMarkerPath;
            private readonly string[] expectedAssetPaths;
            private readonly string[] expectedOutputDirectories;
            private RestoreManifest manifest;
            private bool completed;

            private AssetPublishTransaction(
                string markerPath,
                string committedMarkerPath,
                IReadOnlyList<string> expectedAssetPaths,
                IReadOnlyList<string> expectedOutputDirectories,
                RestoreManifest manifest)
            {
                this.markerPath = markerPath;
                this.committedMarkerPath = committedMarkerPath;
                this.expectedAssetPaths = expectedAssetPaths.ToArray();
                this.expectedOutputDirectories = expectedOutputDirectories.ToArray();
                this.manifest = manifest;
            }

            public static AssetPublishTransaction Begin(
                IReadOnlyList<string> assetPaths,
                IReadOnlyList<string> outputDirectories)
            {
                return BeginWithAuthority(
                    assetPaths,
                    outputDirectories,
                    ProjectPath(RestoreMarkerPath),
                    ProjectPath(RestoreMarkerTemporaryPath),
                    ProjectPath(RestoreCommittedMarkerPath));
            }

            public static AssetPublishTransaction BeginIsolatedForTests(
                IReadOnlyList<string> assetPaths,
                IReadOnlyList<string> outputDirectories)
            {
                return BeginWithAuthority(
                    assetPaths,
                    outputDirectories,
                    ProjectPath(IsolatedTestRestoreRoot + "/transaction.json"),
                    ProjectPath(IsolatedTestRestoreRoot + "/transaction.json.tmp"),
                    ProjectPath(IsolatedTestRestoreRoot + "/transaction.committed.json"));
            }

            private static AssetPublishTransaction BeginWithAuthority(
                IReadOnlyList<string> assetPaths,
                IReadOnlyList<string> outputDirectories,
                string marker,
                string temporaryMarker,
                string committedMarker)
            {
                string restoreDirectory = Path.GetDirectoryName(marker);
                if (Directory.Exists(restoreDirectory))
                    Directory.Delete(restoreDirectory, true);
                Directory.CreateDirectory(restoreDirectory);
                var entries = new RestoreEntry[assetPaths.Count];
                for (int index = 0; index < assetPaths.Count; index++)
                {
                    string assetPath = assetPaths[index];
                    string absoluteAsset = ProjectPath(assetPath);
                    string absoluteMeta = absoluteAsset + ".meta";
                    bool existed = File.Exists(absoluteAsset);
                    var entry = new RestoreEntry
                    {
                        assetPath = assetPath,
                        existed = existed,
                        guid = existed ? AssetDatabase.AssetPathToGUID(assetPath) : string.Empty,
                        backupAsset = index.ToString("D3") + ".asset.bytes",
                        backupMeta = index.ToString("D3") + ".meta.bytes",
                    };
                    if (existed)
                    {
                        if (!File.Exists(absoluteMeta) || string.IsNullOrEmpty(entry.guid))
                            throw new InvalidOperationException("Existing output lacks stable metadata: " + assetPath);
                        entry.assetSha256 = Sha256File(absoluteAsset);
                        entry.metaSha256 = Sha256File(absoluteMeta);
                        File.Copy(absoluteAsset, Path.Combine(restoreDirectory, entry.backupAsset), true);
                        File.Copy(absoluteMeta, Path.Combine(restoreDirectory, entry.backupMeta), true);
                    }
                    entries[index] = entry;
                }
                string[] missingDirectories = outputDirectories
                    .Where(path => !AssetDatabase.IsValidFolder(path))
                    .ToArray();
                var manifest = new RestoreManifest
                {
                    entries = entries,
                    originallyMissingDirectories = missingDirectories,
                };
                WriteMarkerAtomically(
                    marker,
                    temporaryMarker,
                    JsonUtility.ToJson(manifest, true));
                return new AssetPublishTransaction(
                    marker,
                    committedMarker,
                    assetPaths,
                    outputDirectories,
                    manifest);
            }

            public void Complete()
            {
                BeforeCommitCheckpoint?.Invoke();
                File.Move(markerPath, committedMarkerPath);
                completed = true;
                try
                {
                    AfterCommitCleanupCheckpoint?.Invoke();
                    CleanupCommittedRestoreDirectory();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[IDEA-0004] Publication committed; restore-backup cleanup will retry next initialization: " +
                        exception.Message);
                }
            }

            public void Rollback()
            {
                RecoverWithAuthority(
                    markerPath,
                    markerPath,
                    expectedAssetPaths,
                    expectedOutputDirectories);
                completed = true;
            }

            public static void Recover(
                string markerPath,
                IReadOnlyList<string> expectedAssetPaths,
                IReadOnlyList<string> expectedOutputDirectories)
            {
                RecoverWithAuthority(
                    markerPath,
                    ProjectPath(RestoreMarkerPath),
                    expectedAssetPaths,
                    expectedOutputDirectories);
            }

            private static void RecoverWithAuthority(
                string markerPath,
                string expectedMarkerPath,
                IReadOnlyList<string> expectedAssetPaths,
                IReadOnlyList<string> expectedOutputDirectories)
            {
                RestoreManifest manifest;
                try
                {
                    manifest = JsonUtility.FromJson<RestoreManifest>(File.ReadAllText(markerPath));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Ruins/Cliff recovery marker is truncated or invalid.",
                        exception);
                }
                if (manifest == null || manifest.entries == null ||
                    !string.Equals(manifest.version, RestoreMarkerVersion, StringComparison.Ordinal))
                    throw new InvalidOperationException("Ruins/Cliff recovery marker is invalid.");
                Restore(
                    manifest,
                    markerPath,
                    expectedMarkerPath,
                    expectedAssetPaths,
                    expectedOutputDirectories);
            }

            private static void Restore(
                RestoreManifest manifest,
                string markerPath,
                string expectedMarkerPath,
                IReadOnlyList<string> expectedAssetPaths,
                IReadOnlyList<string> expectedOutputDirectories)
            {
                ValidateManifest(
                    manifest,
                    markerPath,
                    expectedMarkerPath,
                    expectedAssetPaths,
                    expectedOutputDirectories);
                string restoreDirectory = Path.GetDirectoryName(markerPath);
                ValidateBackupEvidence(manifest, restoreDirectory);
                for (int index = manifest.entries.Length - 1; index >= 0; index--)
                {
                    RestoreEntry entry = manifest.entries[index];
                    string absoluteAsset = ProjectPath(entry.assetPath);
                    string absoluteMeta = absoluteAsset + ".meta";
                    if (AssetDatabase.LoadMainAssetAtPath(entry.assetPath) != null ||
                        File.Exists(absoluteAsset))
                        AssetDatabase.DeleteAsset(entry.assetPath);
                    if (File.Exists(absoluteAsset))
                        File.Delete(absoluteAsset);
                    if (File.Exists(absoluteMeta))
                        File.Delete(absoluteMeta);
                    if (!entry.existed)
                        continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(absoluteAsset));
                    File.Copy(Path.Combine(restoreDirectory, entry.backupAsset), absoluteAsset, true);
                    File.Copy(Path.Combine(restoreDirectory, entry.backupMeta), absoluteMeta, true);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                VerifyRestoredEntries(manifest);
                if (manifest.originallyMissingDirectories != null)
                {
                    for (int index = manifest.originallyMissingDirectories.Length - 1; index >= 0; index--)
                        DeleteFolderIfEmpty(manifest.originallyMissingDirectories[index]);
                }
                if (Directory.Exists(restoreDirectory))
                    Directory.Delete(restoreDirectory, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            private static void ValidateManifest(
                RestoreManifest manifest,
                string markerPath,
                string expectedMarkerPath,
                IReadOnlyList<string> expectedAssetPaths,
                IReadOnlyList<string> expectedOutputDirectories)
            {
                if (manifest == null || manifest.entries == null ||
                    manifest.originallyMissingDirectories == null ||
                    expectedAssetPaths == null || expectedOutputDirectories == null ||
                    !string.Equals(manifest.version, RestoreMarkerVersion, StringComparison.Ordinal) ||
                    !CanonicalPathEquals(markerPath, expectedMarkerPath) ||
                    !File.Exists(expectedMarkerPath))
                {
                    throw new InvalidOperationException("Ruins/Cliff recovery manifest authority is invalid.");
                }

                if (manifest.entries.Length != expectedAssetPaths.Count ||
                    expectedAssetPaths.Distinct(StringComparer.Ordinal).Count() != expectedAssetPaths.Count)
                {
                    throw new InvalidOperationException(
                        "Ruins/Cliff recovery manifest must contain every approved destination exactly once.");
                }

                string restoreDirectory = Path.GetDirectoryName(expectedMarkerPath);
                var allowedEvidence = new HashSet<string>(StringComparer.Ordinal)
                {
                    Path.GetFullPath(expectedMarkerPath),
                };
                for (int index = 0; index < manifest.entries.Length; index++)
                {
                    RestoreEntry entry = manifest.entries[index];
                    string expectedAssetPath = expectedAssetPaths[index];
                    string expectedAssetBackup = index.ToString("D3") + ".asset.bytes";
                    string expectedMetaBackup = index.ToString("D3") + ".meta.bytes";
                    if (entry == null ||
                        !IsSafeAssetPath(entry.assetPath) ||
                        !IsSafeAssetPath(expectedAssetPath) ||
                        !string.Equals(entry.assetPath, expectedAssetPath, StringComparison.Ordinal) ||
                        !string.Equals(entry.backupAsset, expectedAssetBackup, StringComparison.Ordinal) ||
                        !string.Equals(entry.backupMeta, expectedMetaBackup, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Ruins/Cliff recovery entry " + index + " violates the approved path/order contract.");
                    }

                    string backupAsset = Path.GetFullPath(Path.Combine(restoreDirectory, expectedAssetBackup));
                    string backupMeta = Path.GetFullPath(Path.Combine(restoreDirectory, expectedMetaBackup));
                    if (entry.existed)
                    {
                        if (!IsHex(entry.guid, 32) ||
                            !IsHex(entry.assetSha256, 64) ||
                            !IsHex(entry.metaSha256, 64) ||
                            !File.Exists(backupAsset) ||
                            !File.Exists(backupMeta))
                        {
                            throw new InvalidOperationException(
                                "Ruins/Cliff recovery entry " + index + " has incomplete existing-output evidence.");
                        }
                        allowedEvidence.Add(backupAsset);
                        allowedEvidence.Add(backupMeta);
                    }
                    else if (!string.IsNullOrEmpty(entry.guid) ||
                             !string.IsNullOrEmpty(entry.assetSha256) ||
                             !string.IsNullOrEmpty(entry.metaSha256) ||
                             File.Exists(backupAsset) ||
                             File.Exists(backupMeta))
                    {
                        throw new InvalidOperationException(
                            "Ruins/Cliff recovery entry " + index + " has contradictory missing-output evidence.");
                    }
                }

                if (expectedOutputDirectories.Distinct(StringComparer.Ordinal).Count() !=
                    expectedOutputDirectories.Count)
                    throw new InvalidOperationException("Ruins/Cliff output-directory authority contains duplicates.");
                int previousDirectoryIndex = -1;
                var seenDirectories = new HashSet<string>(StringComparer.Ordinal);
                foreach (string directory in manifest.originallyMissingDirectories)
                {
                    if (!IsSafeAssetPath(directory) || !seenDirectories.Add(directory))
                        throw new InvalidOperationException("Ruins/Cliff recovery directory path is unsafe or duplicated.");
                    int approvedIndex = IndexOf(expectedOutputDirectories, directory);
                    if (approvedIndex < 0 || approvedIndex <= previousDirectoryIndex)
                        throw new InvalidOperationException(
                            "Ruins/Cliff recovery directories must be an ordered subset of approved outputs.");
                    previousDirectoryIndex = approvedIndex;
                }

                if (Directory.GetDirectories(restoreDirectory).Length != 0 ||
                    Directory.GetFiles(restoreDirectory)
                        .Select(Path.GetFullPath)
                        .Any(path => !allowedEvidence.Contains(path)))
                {
                    throw new InvalidOperationException(
                        "Ruins/Cliff recovery directory contains unapproved evidence files.");
                }
            }

            private static int IndexOf(IReadOnlyList<string> values, string target)
            {
                for (int index = 0; index < values.Count; index++)
                    if (string.Equals(values[index], target, StringComparison.Ordinal))
                        return index;
                return -1;
            }

            private static bool IsSafeAssetPath(string assetPath)
            {
                if (string.IsNullOrEmpty(assetPath) ||
                    Path.IsPathRooted(assetPath) ||
                    !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    assetPath.IndexOf('\\') >= 0 ||
                    assetPath.IndexOf(':') >= 0 ||
                    assetPath.Split('/').Any(segment =>
                        string.IsNullOrEmpty(segment) || segment == "." || segment == ".."))
                    return false;
                string assetsRoot = ProjectPath("Assets") + Path.DirectorySeparatorChar;
                string fullPath = ProjectPath(assetPath);
                return fullPath.StartsWith(assetsRoot, StringComparison.Ordinal);
            }

            private static bool IsHex(string value, int expectedLength)
            {
                return value != null && value.Length == expectedLength &&
                       value.All(character => Uri.IsHexDigit(character));
            }

            private static bool CanonicalPathEquals(string left, string right)
            {
                if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                    return false;
                try
                {
                    return string.Equals(
                        Path.GetFullPath(left),
                        Path.GetFullPath(right),
                        StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            }

            private static void ValidateBackupEvidence(
                RestoreManifest manifest,
                string restoreDirectory)
            {
                foreach (RestoreEntry entry in manifest.entries)
                {
                    if (!entry.existed)
                        continue;
                    string backupAsset = Path.Combine(restoreDirectory, entry.backupAsset);
                    string backupMeta = Path.Combine(restoreDirectory, entry.backupMeta);
                    if (!File.Exists(backupAsset) || !File.Exists(backupMeta) ||
                        !string.Equals(Sha256File(backupAsset), entry.assetSha256, StringComparison.Ordinal) ||
                        !string.Equals(Sha256File(backupMeta), entry.metaSha256, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Ruins/Cliff recovery backup failed byte verification for " + entry.assetPath + ".");
                    }
                }
            }

            private static void VerifyRestoredEntries(RestoreManifest manifest)
            {
                foreach (RestoreEntry entry in manifest.entries)
                {
                    string absoluteAsset = ProjectPath(entry.assetPath);
                    string absoluteMeta = absoluteAsset + ".meta";
                    if (!entry.existed)
                    {
                        if (File.Exists(absoluteAsset) || File.Exists(absoluteMeta))
                            throw new InvalidOperationException(
                                "Rollback retained an originally missing output or metadata: " + entry.assetPath + ".");
                        continue;
                    }
                    if (!File.Exists(absoluteAsset) || !File.Exists(absoluteMeta) ||
                        !string.Equals(Sha256File(absoluteAsset), entry.assetSha256, StringComparison.Ordinal) ||
                        !string.Equals(Sha256File(absoluteMeta), entry.metaSha256, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Rollback did not restore exact asset/meta bytes for " + entry.assetPath + ".");
                    }
                    string restoredGuid = AssetDatabase.AssetPathToGUID(entry.assetPath);
                    if (!string.Equals(restoredGuid, entry.guid, StringComparison.Ordinal))
                        throw new InvalidOperationException("Rollback did not restore GUID for " + entry.assetPath + ".");
                }
            }

            private static void DeleteFolderIfEmpty(string assetFolder)
            {
                string absolute = ProjectPath(assetFolder);
                if (!Directory.Exists(absolute))
                    return;
                string[] entries = Directory.GetFileSystemEntries(absolute);
                if (entries.Length != 0)
                    throw new InvalidOperationException(
                        "Rollback cannot remove non-empty originally missing directory: " + assetFolder + ".");
                if (AssetDatabase.IsValidFolder(assetFolder))
                {
                    if (!AssetDatabase.DeleteAsset(assetFolder))
                        throw new InvalidOperationException("Rollback could not remove directory: " + assetFolder + ".");
                }
                else
                {
                    Directory.Delete(absolute);
                    string meta = absolute + ".meta";
                    if (File.Exists(meta))
                        File.Delete(meta);
                }
                if (Directory.Exists(absolute) || File.Exists(absolute + ".meta"))
                    throw new InvalidOperationException(
                        "Rollback retained an originally missing directory or metadata: " + assetFolder + ".");
            }

            private static void WriteMarkerAtomically(
                string marker,
                string temporaryMarker,
                string json)
            {
                if (File.Exists(temporaryMarker))
                    File.Delete(temporaryMarker);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(
                           temporaryMarker,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryMarker, marker);
            }

            private static string Sha256File(string path)
            {
                using (SHA256 sha256 = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    return BitConverter.ToString(sha256.ComputeHash(stream))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
            }

            private void CleanupCommittedRestoreDirectory()
            {
                string directory = Path.GetDirectoryName(markerPath);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }

            public void Dispose()
            {
                if (!completed && File.Exists(markerPath))
                    return;
                manifest = null;
            }
        }
    }
}
