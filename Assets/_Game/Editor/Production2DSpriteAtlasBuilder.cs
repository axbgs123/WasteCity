using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace WasteCity.Editor
{
    public sealed class Production2DAtlasDefinition
    {
        internal Production2DAtlasDefinition(
            string name,
            string sourceRoot,
            int expectedPackableCount)
        {
            Name = name;
            SourceRoot = sourceRoot;
            ExpectedPackableCount = expectedPackableCount;
            AssetPath = Production2DSpriteAtlasBuilder.AtlasesRoot +
                "Production2D-" + name + ".spriteatlas";
        }

        public string Name { get; }
        public string SourceRoot { get; }
        public int ExpectedPackableCount { get; }
        public string AssetPath { get; }
    }

    public static class Production2DSpriteAtlasBuilder
    {
        public const string AtlasesRoot =
            "Assets/_Game/Art/Production2D/Atlases/";
        public const int Padding = 4;

        private static readonly Production2DAtlasDefinition[] definitions =
        {
            new Production2DAtlasDefinition(
                "Items",
                Production2DItemImportPolicy.Root,
                31),
            new Production2DAtlasDefinition(
                "Technology",
                Production2DTechnologyImportPolicy.Root,
                43),
            new Production2DAtlasDefinition(
                "Buildings",
                Production2DBuildingImportPolicy.Root,
                30),
            new Production2DAtlasDefinition(
                "UI",
                Production2DUiCharacterMarkerImportPolicy.UiRoot,
                7),
            new Production2DAtlasDefinition(
                "Characters",
                Production2DUiCharacterMarkerImportPolicy.CharacterRoot,
                1),
            new Production2DAtlasDefinition(
                "WorldMarkers",
                Production2DUiCharacterMarkerImportPolicy.WorldMarkerRoot,
                2),
        };

        public static IReadOnlyList<Production2DAtlasDefinition> Definitions =>
            definitions;

        [MenuItem("WasteCity/Art/Production 2D/Build Six Sprite Atlases")]
        public static void BuildAtlases()
        {
            EnsureFolder("Assets/_Game/Art/Production2D");
            EnsureFolder(AtlasesRoot.TrimEnd('/'));
            for (var index = 0; index < definitions.Length; index++)
                BuildAtlas(definitions[index]);
        }

        public static UnityEngine.Object[] ExpectedPackables(
            Production2DAtlasDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            string[] paths = Directory.Exists(definition.SourceRoot)
                ? Directory.GetFiles(
                        definition.SourceRoot,
                        "*.png",
                        SearchOption.TopDirectoryOnly)
                    .Select(path => path.Replace('\\', '/'))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
            if (paths.Length != definition.ExpectedPackableCount)
                throw new InvalidDataException(
                    definition.Name + " atlas requires exactly " +
                    definition.ExpectedPackableCount + " PNG packables, got " +
                    paths.Length + ".");
            var packables = new UnityEngine.Object[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                packables[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    paths[index]);
                if (packables[index] == null)
                    throw new FileNotFoundException(
                        "Atlas source PNG is not imported as Texture2D.",
                        paths[index]);
            }
            return packables;
        }

        private static void BuildAtlas(Production2DAtlasDefinition definition)
        {
            UnityEngine.Object[] expected = ExpectedPackables(definition);
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(
                definition.AssetPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas { name = "Production2D-" + definition.Name };
                AssetDatabase.CreateAsset(atlas, definition.AssetPath);
            }

            if (AtlasMatches(atlas, expected)) return;
            UnityEngine.Object[] current =
                SpriteAtlasExtensions.GetPackables(atlas);
            if (current.Length > 0)
                SpriteAtlasExtensions.Remove(atlas, current);
            SpriteAtlasExtensions.Add(atlas, expected);
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);
            SpriteAtlasExtensions.SetPackingSettings(
                atlas,
                ExpectedPackingSettings());
            SpriteAtlasExtensions.SetTextureSettings(
                atlas,
                ExpectedTextureSettings());
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssetIfDirty(atlas);
        }

        private static bool AtlasMatches(
            SpriteAtlas atlas,
            IReadOnlyList<UnityEngine.Object> expected)
        {
            if (!SpriteAtlasExtensions.IsIncludeInBuild(atlas)) return false;
            SpriteAtlasPackingSettings packing =
                SpriteAtlasExtensions.GetPackingSettings(atlas);
            if (packing.blockOffset != 1 ||
                packing.padding != Padding ||
                packing.enableRotation ||
                packing.enableTightPacking)
                return false;
            SpriteAtlasTextureSettings texture =
                SpriteAtlasExtensions.GetTextureSettings(atlas);
            if (texture.readable || texture.generateMipMaps ||
                !texture.sRGB || texture.filterMode != FilterMode.Bilinear)
                return false;
            UnityEngine.Object[] actual =
                SpriteAtlasExtensions.GetPackables(atlas);
            if (actual.Length != expected.Count) return false;
            for (var index = 0; index < expected.Count; index++)
            {
                if (actual[index] != expected[index]) return false;
            }
            return true;
        }

        private static SpriteAtlasPackingSettings ExpectedPackingSettings()
        {
            return new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                padding = Padding,
                enableRotation = false,
                enableTightPacking = false,
            };
        }

        private static SpriteAtlasTextureSettings ExpectedTextureSettings()
        {
            return new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
            };
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException(
                    "Invalid asset folder path: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
