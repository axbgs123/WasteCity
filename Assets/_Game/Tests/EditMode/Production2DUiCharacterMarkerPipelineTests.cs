using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Tests
{
    public sealed class Production2DUiCharacterMarkerPipelineTests
    {
        private const string ManifestPath =
            "Docs/Art/IDEA-0016/Manifests/idea-0016-ui-character-marker-visual-assets.json";
        private const string ImportPolicyType =
            "WasteCity.Editor.Production2DUiCharacterMarkerImportPolicy, WasteCity.Editor";

        private static readonly string[] ExpectedIds =
        {
            "core.ui.frame.primary-panel",
            "core.ui.frame.secondary-card",
            "core.ui.control.primary-button",
            "core.ui.frame.technology-node",
            "core.ui.divider.terminal-horizontal",
            "core.ui.connector.technology-branch",
            "core.ui.icon.search",
            "core.character.cen-jin",
            "core.world-marker.resource-node",
            "core.world-marker.selection-reticle"
        };

        [Test]
        public void IDEA0016_OwnedManifestHasReviewedBriefsAndStableMappings()
        {
            VisualManifest manifest = LoadManifest();

            Assert.That(manifest.requirementId, Is.EqualTo("IDEA-0016"));
            Assert.That(manifest.unityVersion, Is.EqualTo("2022.3.62f1"));
            Assert.That(manifest.entries, Has.Length.EqualTo(ExpectedIds.Length));
            Assert.That(manifest.entries.Select(entry => entry.contentId),
                Is.EqualTo(ExpectedIds));
            Assert.That(manifest.entries.Select(entry => entry.contentId).Distinct().Count(),
                Is.EqualTo(ExpectedIds.Length));
            foreach (VisualEntry entry in manifest.entries)
            {
                Assert.That(entry.displayNameZh, Is.Not.Empty, entry.contentId);
                CollectionAssert.Contains(
                    new[] { "ui", "character", "world-marker" },
                    entry.visualClass,
                    entry.contentId);
                Assert.That(entry.loreBriefZh, Is.Not.Empty, entry.contentId);
                Assert.That(entry.useSummaryZh, Is.Not.Empty, entry.contentId);
                Assert.That(entry.visualKeywordsZh, Is.Not.Empty, entry.contentId);
                Assert.That(entry.forbiddenElementsZh, Is.Not.Empty, entry.contentId);
                Assert.That(entry.promptSummaryZh, Is.Not.Empty, entry.contentId);
                Assert.That(entry.sourceAssetPath, Does.StartWith("Docs/Art/IDEA-0016/Source/"), entry.contentId);
                Assert.That(entry.unitySpritePath, Does.StartWith("Assets/_Game/Art/Production2D/"), entry.contentId);
                Assert.That(entry.masterSizePx, Has.Length.EqualTo(2), entry.contentId);
                Assert.That(entry.deliverySizePx, Has.Length.EqualTo(2), entry.contentId);
                Assert.That(entry.displaySizesPx, Is.Not.Empty, entry.contentId);
                Assert.That(entry.reviewState, Is.EqualTo("integrated"),
                    entry.contentId);
            }

            VisualEntry cenJin = manifest.entries.Single(entry =>
                entry.contentId == "core.character.cen-jin");
            Assert.That(cenJin.genderPresentationZh, Is.EqualTo("男性"));
            Assert.That(cenJin.faceVisibilityZh, Is.EqualTo("完全遮脸"));
            Assert.That(cenJin.forbiddenElementsZh, Does.Contain("女性"));
            Assert.That(cenJin.forbiddenElementsZh, Does.Contain("露脸"));
            Assert.That(cenJin.forbiddenElementsZh, Does.Contain("写实"));
        }

        [Test]
        public void IDEA0016_AllFormalMastersAndDeliveriesAreTrueAlphaAndExactSize()
        {
            foreach (VisualEntry entry in LoadManifest().entries)
            {
                AssertPngContract(entry.sourceAssetPath, entry.masterSizePx, entry.contentId + " master");
                AssertPngContract(entry.unitySpritePath, entry.deliverySizePx, entry.contentId + " delivery");
            }

            string[] forbiddenChroma = new[]
                {
                    "Docs/Art/IDEA-0016/Source/UI",
                    "Docs/Art/IDEA-0016/Source/Characters",
                    "Docs/Art/IDEA-0016/Source/WorldMarkers"
                }
                .SelectMany(path => Directory.GetFiles(
                    Path.Combine(ProjectRoot(), path),
                    "*-chroma-*.png",
                    SearchOption.TopDirectoryOnly))
                .ToArray();
            Assert.That(forbiddenChroma, Is.Empty,
                "Color-key intermediates must stay outside the repository.");
        }

        [Test]
        public void IDEA0016_FormalSpritesRespectDeclaredSafeArea()
        {
            foreach (VisualEntry entry in LoadManifest().entries)
            {
                Texture2D texture = LoadPng(entry.unitySpritePath);
                try
                {
                    RectInt bounds = OpaqueBounds(texture);
                    Assert.That(bounds.width, Is.GreaterThan(0), entry.contentId);
                    Assert.That(bounds.height, Is.GreaterThan(0), entry.contentId);
                    if (entry.contentId == "core.world-marker.resource-node")
                        continue;

                    int minX = Mathf.FloorToInt(entry.safeAreaNormalized[0] * texture.width);
                    int minY = Mathf.FloorToInt(entry.safeAreaNormalized[1] * texture.height);
                    int maxX = Mathf.CeilToInt((entry.safeAreaNormalized[0] + entry.safeAreaNormalized[2]) * texture.width) - 1;
                    int maxY = Mathf.CeilToInt((entry.safeAreaNormalized[1] + entry.safeAreaNormalized[3]) * texture.height) - 1;
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(minX), entry.contentId);
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(minY), entry.contentId);
                    Assert.That(bounds.xMax - 1, Is.LessThanOrEqualTo(maxX), entry.contentId);
                    Assert.That(bounds.yMax - 1, Is.LessThanOrEqualTo(maxY), entry.contentId);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void IDEA0016_ResourceNodeMarkerKeepsExact176SquareTransparentCenter()
        {
            VisualEntry entry = LoadManifest().entries.Single(candidate =>
                candidate.contentId == "core.world-marker.resource-node");
            Assert.That(entry.deliverySizePx, Is.EqualTo(new[] { 256, 304 }));
            Assert.That(entry.centralTransparentRectPx,
                Is.EqualTo(new[] { 40, 64, 176, 176 }));

            Texture2D texture = LoadPng(entry.unitySpritePath);
            try
            {
                int[] rect = entry.centralTransparentRectPx;
                for (int y = rect[1]; y < rect[1] + rect[3]; y++)
                for (int x = rect[0]; x < rect[0] + rect[2]; x++)
                    Assert.That(texture.GetPixel(x, y).a, Is.EqualTo(0f),
                        $"central hole pixel {x},{y}");

                Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(0f));
                Assert.That(texture.GetPixel(texture.width - 1, 0).a, Is.EqualTo(0f));
                Assert.That(texture.GetPixel(0, texture.height - 1).a, Is.EqualTo(0f));
                Assert.That(texture.GetPixel(texture.width - 1, texture.height - 1).a, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void IDEA0016_OwnedImportPolicyIsScopedAndMatchesNineSliceBorders()
        {
            Type policy = Type.GetType(ImportPolicyType);
            Assert.That(policy, Is.Not.Null,
                "The owned Production2D importer must exist before assets can be integrated.");

            MethodInfo isOwned = policy.GetMethod("IsOwnedPng",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo expectedBorder = policy.GetMethod("ExpectedBorderForAsset",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo reimport = policy.GetMethod("ReimportOwnedAssets",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(isOwned, Is.Not.Null);
            Assert.That(expectedBorder, Is.Not.Null);
            Assert.That(reimport, Is.Not.Null);
            Assert.That(reimport.GetCustomAttributes(typeof(MenuItem), false), Is.Not.Empty);

            Assert.That(isOwned.Invoke(null, new object[] {
                "Assets/_Game/Art/Production2D/UI/ui-primary-panel.png" }), Is.True);
            Assert.That(isOwned.Invoke(null, new object[] {
                "Assets/_Game/Art/Production2D/Items/item-iron.png" }), Is.False);
            Assert.That(isOwned.Invoke(null, new object[] {
                "Assets/_Game/Art/Production2D/UI/ui-primary-panel.jpg" }), Is.False);

            foreach (VisualEntry entry in LoadManifest().entries)
            {
                var importer = AssetImporter.GetAtPath(entry.unitySpritePath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, entry.contentId);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), entry.contentId);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), entry.contentId);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), entry.contentId);
                Assert.That(importer.alphaIsTransparency, Is.True, entry.contentId);
                Assert.That(importer.sRGBTexture, Is.True, entry.contentId);
                Assert.That(importer.mipmapEnabled, Is.False, entry.contentId);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), entry.contentId);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), entry.contentId);
                Assert.That(importer.crunchedCompression, Is.False, entry.contentId);
                int expectedMaxSize = Math.Max(entry.deliverySizePx[0], entry.deliverySizePx[1]) <= 256
                    ? 256
                    : 512;
                Assert.That(importer.maxTextureSize, Is.EqualTo(expectedMaxSize), entry.contentId);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), entry.contentId);

                var expected = (Vector4)expectedBorder.Invoke(null,
                    new object[] { entry.unitySpritePath });
                Assert.That(importer.spriteBorder, Is.EqualTo(expected), entry.contentId);
            }
        }

        private static VisualManifest LoadManifest()
        {
            string absolute = Path.Combine(ProjectRoot(), ManifestPath);
            Assert.That(File.Exists(absolute), Is.True, ManifestPath);
            VisualManifest manifest = JsonUtility.FromJson<VisualManifest>(File.ReadAllText(absolute));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.entries, Is.Not.Null);
            return manifest;
        }

        private static void AssertPngContract(string projectPath, int[] size, string context)
        {
            string absolute = Path.Combine(ProjectRoot(), projectPath);
            byte[] bytes = File.ReadAllBytes(absolute);
            Assert.That(bytes.Length, Is.GreaterThan(25), context);
            Assert.That(bytes[25], Is.EqualTo(6),
                context + " must use PNG color type 6 (RGBA), independent of Unity runtime texture layout.");
            Texture2D texture = LoadPng(projectPath);
            try
            {
                Assert.That(texture.width, Is.EqualTo(size[0]), context);
                Assert.That(texture.height, Is.EqualTo(size[1]), context);
                Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(0f), context);
                Assert.That(texture.GetPixel(texture.width - 1, 0).a, Is.EqualTo(0f), context);
                Assert.That(texture.GetPixel(0, texture.height - 1).a, Is.EqualTo(0f), context);
                Assert.That(texture.GetPixel(texture.width - 1, texture.height - 1).a, Is.EqualTo(0f), context);
                Assert.That(texture.GetPixels32().Any(pixel => pixel.a >= 128), Is.True, context);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D LoadPng(string projectPath)
        {
            string absolute = Path.Combine(ProjectRoot(), projectPath);
            Assert.That(File.Exists(absolute), Is.True, projectPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(absolute), false), Is.True, projectPath);
            return texture;
        }

        private static RectInt OpaqueBounds(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a < 8)
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            return maxX < minX
                ? new RectInt()
                : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        }

        [Serializable]
        private sealed class VisualManifest
        {
            public string requirementId;
            public string unityVersion;
            public VisualEntry[] entries;
        }

        [Serializable]
        private sealed class VisualEntry
        {
            public string contentId;
            public string displayNameZh;
            public string visualClass;
            public string useSummaryZh;
            public string loreBriefZh;
            public string visualKeywordsZh;
            public string forbiddenElementsZh;
            public string promptSummaryZh;
            public string genderPresentationZh;
            public string faceVisibilityZh;
            public string sourceAssetPath;
            public string unitySpritePath;
            public int[] masterSizePx;
            public int[] deliverySizePx;
            public int[] displaySizesPx;
            public float[] safeAreaNormalized;
            public int[] centralTransparentRectPx;
            public string reviewState;
        }
    }
}
