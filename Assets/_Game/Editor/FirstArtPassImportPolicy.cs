using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor
{
    public sealed class FirstArtPassImportPolicy : AssetPostprocessor
    {
        public const string Root = "Assets/_Game/Art/FirstPass/";

        private const string BaseColorSuffix = "_BaseColor.png";
        private const string NormalSuffix = "_Normal.png";
        private const string MaskSuffix = "_Mask.png";
        private const string HeightSuffix = "_Height.png";

        private static readonly HashSet<string> TemporaryReadablePaths =
            new HashSet<string>(StringComparer.Ordinal);

        internal static Action<string> TemporaryPlatformRestoreCheckpoint;

        private static readonly string[] ApprovedHeightPaths =
        {
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Height.png",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_Height.png",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Height.png",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_Height.png",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_Height.png",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_Height.png",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_Height.png",
        };

        private static readonly HashSet<string> ApprovedReadablePaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_BaseColor.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Normal.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Height.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_BaseColor.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_Normal.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_Height.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_BaseColor.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Normal.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Height.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_BaseColor.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_Normal.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_Height.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_BaseColor.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_Normal.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_Height.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_BaseColor.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_Normal.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_Height.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_BaseColor.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_Normal.png",
                "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_Height.png",
            };

        private void OnPreprocessTexture()
        {
            if (!IsFirstPassAsset(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith(BaseColorSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer, assetPath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            if (assetPath.EndsWith(NormalSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer, assetPath);
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            if (assetPath.EndsWith(MaskSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer, assetPath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (assetPath.EndsWith(HeightSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer, assetPath);
                importer.textureType = TextureImporterType.SingleChannel;
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.singleChannelComponent = importer.isReadable
                    ? TextureImporterSingleChannelComponent.Red
                    : TextureImporterSingleChannelComponent.Alpha;
                importer.SetTextureSettings(settings);
                if (importer.isReadable)
                    ConfigureTemporaryHeightPlatform(importer);
            }
        }

        private void OnPreprocessModel()
        {
            if (!IsFirstPassAsset(assetPath) ||
                !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
        }

        [MenuItem("WasteCity/Art/Reimport First Art Pass")]
        public static void ReimportAll()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { Root.TrimEnd('/') });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string extension = Path.GetExtension(path);
                if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.SaveAssets();
        }

        internal static IDisposable AllowTemporaryReadability(string exactAssetPath)
        {
            if (!ApprovedReadablePaths.Contains(exactAssetPath))
            {
                throw new ArgumentException(
                    "Temporary readability is limited to the exact 21 approved first-art BaseColor, Normal and Height textures.",
                    nameof(exactAssetPath));
            }

            if (!TemporaryReadablePaths.Add(exactAssetPath))
            {
                throw new InvalidOperationException(
                    $"Temporary readability is already active for '{exactAssetPath}'.");
            }

            var importer = AssetImporter.GetAtPath(exactAssetPath) as TextureImporter;
            if (importer == null)
            {
                TemporaryReadablePaths.Remove(exactAssetPath);
                throw new InvalidOperationException(
                    $"Approved Height path has no TextureImporter: '{exactAssetPath}'.");
            }

            string platformName = ActiveTexturePlatformName();
            TextureImporterPlatformSettings originalPlatform =
                importer.GetPlatformTextureSettings(platformName);
            return new TemporaryReadabilityScope(
                exactAssetPath,
                platformName,
                originalPlatform,
                File.ReadAllBytes(exactAssetPath + ".meta"),
                AssetDatabase.AssetPathToGUID(exactAssetPath),
                AssetDatabase.GetAssetDependencyHash(exactAssetPath));
        }

        private static bool IsFirstPassAsset(string path)
        {
            return path.StartsWith(Root, StringComparison.Ordinal);
        }

        private static void ConfigureCommonTexture(TextureImporter importer, string exactAssetPath)
        {
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
            importer.isReadable = TemporaryReadablePaths.Contains(exactAssetPath);
        }

        private static bool IsApprovedHeightPath(string exactAssetPath)
        {
            foreach (string approvedPath in ApprovedHeightPaths)
            {
                if (string.Equals(exactAssetPath, approvedPath, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void ConfigureTemporaryHeightPlatform(TextureImporter importer)
        {
            string platformName = ActiveTexturePlatformName();
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platformName);
            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.format = TextureImporterFormat.R16;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }

        private static string ActiveTexturePlatformName()
        {
            return BuildPipeline
                .GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)
                .ToString();
        }

        private sealed class TemporaryReadabilityScope : IDisposable
        {
            private readonly string exactAssetPath;
            private readonly string platformName;
            private readonly TextureImporterPlatformSettings originalPlatform;
            private readonly byte[] originalMetaBytes;
            private readonly string originalGuid;
            private readonly Hash128 originalDependencyHash;
            private bool disposed;

            public TemporaryReadabilityScope(
                string exactAssetPath,
                string platformName,
                TextureImporterPlatformSettings originalPlatform,
                byte[] originalMetaBytes,
                string originalGuid,
                Hash128 originalDependencyHash)
            {
                this.exactAssetPath = exactAssetPath;
                this.platformName = platformName;
                this.originalPlatform = originalPlatform;
                this.originalMetaBytes = originalMetaBytes;
                this.originalGuid = originalGuid;
                this.originalDependencyHash = originalDependencyHash;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                var failures = new List<Exception>(3);
                try
                {
                    try
                    {
                        TemporaryPlatformRestoreCheckpoint?.Invoke(exactAssetPath);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
                    finally
                    {
                        try
                        {
                            var importer = AssetImporter.GetAtPath(exactAssetPath) as TextureImporter;
                            if (importer == null)
                            {
                                throw new InvalidOperationException(
                                    $"Cannot restore Height importer platform settings: '{exactAssetPath}'.");
                            }

                            if (!string.Equals(
                                    originalPlatform.name,
                                    platformName,
                                    StringComparison.Ordinal))
                            {
                                failures.Add(new InvalidOperationException(
                                    $"Captured Height platform settings do not match '{platformName}'."));
                            }

                            importer.SetPlatformTextureSettings(originalPlatform);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }
                finally
                {
                    TemporaryReadablePaths.Remove(exactAssetPath);
                    disposed = true;
                    try
                    {
                        RestoreExactMetaAndReimport();
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
                }

                ThrowFailures(failures);
            }

            private void RestoreExactMetaAndReimport()
            {
                Reimport(exactAssetPath);
                string metaPath = exactAssetPath + ".meta";
                if (!BytesEqual(File.ReadAllBytes(metaPath), originalMetaBytes))
                {
                    File.WriteAllBytes(metaPath, originalMetaBytes);
                    Reimport(exactAssetPath);
                }

                var importer = AssetImporter.GetAtPath(exactAssetPath) as TextureImporter;
                if (importer == null || importer.isReadable)
                {
                    throw new InvalidOperationException(
                        $"Terrain source readability was not restored: '{exactAssetPath}'.");
                }
                if (!string.Equals(
                        AssetDatabase.AssetPathToGUID(exactAssetPath),
                        originalGuid,
                        StringComparison.Ordinal) ||
                    AssetDatabase.GetAssetDependencyHash(exactAssetPath) != originalDependencyHash ||
                    !BytesEqual(File.ReadAllBytes(metaPath), originalMetaBytes))
                {
                    throw new InvalidOperationException(
                        $"Terrain source identity or exact meta bytes were not restored: '{exactAssetPath}'.");
                }
            }

            private static void Reimport(string path)
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }

            private static bool BytesEqual(byte[] left, byte[] right)
            {
                if (ReferenceEquals(left, right))
                    return true;
                if (left == null || right == null || left.Length != right.Length)
                    return false;
                for (int index = 0; index < left.Length; index++)
                {
                    if (left[index] != right[index])
                        return false;
                }

                return true;
            }

            private static void ThrowFailures(List<Exception> failures)
            {
                if (failures.Count == 0)
                    return;
                if (failures.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(failures[0]).Throw();
                    return;
                }

                throw new AggregateException(
                    "Temporary terrain importer restoration failed.",
                    failures);
            }
        }
    }
}
