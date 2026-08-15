using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Graybox3D.Production;

namespace WasteCity.Tests
{
    public sealed class GrayboxResourceIconAssets3DTests
    {
        private const string Root =
            "Assets/_Game/Art/FirstPass/UI/ResourceIcons/";

        [TestCase(ResourceIds.Iron, "ResourceIcon_Iron.png")]
        [TestCase(ResourceIds.Alloy, "ResourceIcon_Alloy.png")]
        [TestCase(ResourceIds.Ammunition, "ResourceIcon_Ammunition.png")]
        [TestCase(ResourceIds.Stone, "ResourceIcon_Stone.png")]
        [TestCase(ResourceIds.Biomass, "ResourceIcon_Biomass.png")]
        [TestCase(ResourceIds.EnergyCrystal, "ResourceIcon_EnergyCrystal.png")]
        [TestCase(ResourceIds.Water, "ResourceIcon_Water.png")]
        public void IDEA0011_CatalogMapsApprovedResourcesToStableSpriteAssets(
            string resourceId,
            string fileName)
        {
            string expectedPath = Root + fileName;
            Assert.That(
                GrayboxResourcePresentationCatalog3D.IconAssetPath(resourceId),
                Is.EqualTo(expectedPath));
            AssertImporter(expectedPath);
        }

        [Test]
        public void IDEA0011_UnknownResourcesUseOneImportSafeFallback()
        {
            string fallback = Root + "ResourceIcon_Unknown.png";
            Assert.That(
                GrayboxResourcePresentationCatalog3D.FallbackIconAssetPath,
                Is.EqualTo(fallback));
            Assert.That(
                GrayboxResourcePresentationCatalog3D.IconAssetPath(
                    "mod.resource.not-registered"),
                Is.EqualTo(fallback));
            Assert.That(
                GrayboxResourcePresentationCatalog3D.IconAssetPath(null),
                Is.EqualTo(fallback));
            AssertImporter(fallback);
        }

        private static void AssertImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, assetPath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(256));

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.That(sprite, Is.Not.Null, assetPath);
            Assert.That(sprite.rect.width, Is.EqualTo(256f));
            Assert.That(sprite.rect.height, Is.EqualTo(256f));
        }
    }
}
