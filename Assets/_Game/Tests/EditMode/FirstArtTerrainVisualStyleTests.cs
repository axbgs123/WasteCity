using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.Editor;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainVisualStyleTests
    {
        private const string TerrainRoot =
            "Assets/_Game/Art/FirstPass/Environment/Terrain";
        private const string ConceptRoot =
            "ArtSource/FirstPass/Environment/Terrain";

        private static readonly string[] TerrainNames =
        {
            "Wasteland",
            "Rocky",
            "Wetland",
            "Crystal",
            "Ruins",
            "DeepWater",
            "Cliff",
        };

        [Test]
        public void VisualStyleCatalog_ExposesSevenDistinctCartographicColors()
        {
            Type catalogType = Type.GetType(
                "WasteCity.ArtIntegration3D.FirstArtTerrainVisualStyleCatalog3D, " +
                "WasteCity.ArtIntegration3D");
            Assert.That(catalogType, Is.Not.Null);

            MethodInfo colorMethod = catalogType.GetMethod(
                "MapColorOf",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo tintMethod = catalogType.GetMethod(
                "TintStrengthOf",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(colorMethod, Is.Not.Null);
            Assert.That(tintMethod, Is.Not.Null);

            var colors = new Color[FirstArtTerrainCatalog3D.LayerCount];
            for (int index = 0; index < colors.Length; index++)
            {
                var layer = (FirstArtTerrainLayer3D)index;
                colors[index] = (Color)colorMethod.Invoke(null, new object[] { layer });
                float tint = (float)tintMethod.Invoke(null, new object[] { layer });
                Assert.That(tint, Is.InRange(.15f, .85f), layer.ToString());
            }

            Assert.That(colors[(int)FirstArtTerrainLayer3D.Wasteland].r,
                Is.GreaterThan(colors[(int)FirstArtTerrainLayer3D.Wasteland].b + .2f));
            Assert.That(colors[(int)FirstArtTerrainLayer3D.Wetland].g,
                Is.GreaterThan(colors[(int)FirstArtTerrainLayer3D.Wetland].b));
            Assert.That(colors[(int)FirstArtTerrainLayer3D.Crystal].b,
                Is.GreaterThan(colors[(int)FirstArtTerrainLayer3D.Crystal].r));
            Assert.That(colors[(int)FirstArtTerrainLayer3D.DeepWater].b,
                Is.GreaterThan(colors[(int)FirstArtTerrainLayer3D.DeepWater].r * 2f));

            for (int left = 0; left < colors.Length; left++)
            for (int right = left + 1; right < colors.Length; right++)
            {
                Assert.That(
                    Vector3.Distance(ToRgb(colors[left]), ToRgb(colors[right])),
                    Is.GreaterThan(.055f),
                    $"{(FirstArtTerrainLayer3D)left}/{(FirstArtTerrainLayer3D)right}");
            }
        }

        [Test]
        [Category("TerrainAssetDeep")]
        public void IDEA0018_ConceptGeneration_IsByteIdenticalAcrossConsecutiveRuns()
        {
            var immutableConcepts = new Dictionary<string, byte[]>(
                StringComparer.Ordinal);
            foreach (string terrain in TerrainNames)
            {
                string conceptPath =
                    $"{ConceptRoot}/{terrain}/References/" +
                    $"{terrain}_IDEA0018_Cartographic_Concept_v001.png";
                Assert.That(File.Exists(conceptPath), Is.True, conceptPath);
                immutableConcepts.Add(
                    conceptPath,
                    File.ReadAllBytes(conceptPath));
            }

            IReadOnlyDictionary<string, byte[]> first =
                FirstArtTerrainAssetBuilder
                    .GenerateCartographicSourceFilesFromConcepts();
            IReadOnlyDictionary<string, byte[]> second =
                FirstArtTerrainAssetBuilder
                    .GenerateCartographicSourceFilesFromConcepts();

            Assert.That(first.Count, Is.EqualTo(TerrainNames.Length * 4));
            Assert.That(second.Keys, Is.EquivalentTo(first.Keys));
            foreach (KeyValuePair<string, byte[]> pair in first)
            {
                Assert.That(File.Exists(pair.Key), Is.True, pair.Key);
                Assert.That(
                    File.ReadAllBytes(pair.Key),
                    Is.EqualTo(pair.Value),
                    pair.Key + " must match the immutable-concept build");
                Assert.That(
                    second[pair.Key],
                    Is.EqualTo(pair.Value),
                    pair.Key);
            }
            foreach (KeyValuePair<string, byte[]> concept in immutableConcepts)
            {
                Assert.That(
                    File.ReadAllBytes(concept.Key),
                    Is.EqualTo(concept.Value),
                    concept.Key + " must remain immutable");
            }
        }

        [Test]
        [Category("TerrainAssetDeep")]
        public void BaseColorSources_AreSquareProductionTexturesWithReadableLayerFamilies()
        {
            var means = new Color[TerrainNames.Length];
            for (int index = 0; index < TerrainNames.Length; index++)
            {
                string terrain = TerrainNames[index];
                string path =
                    $"{TerrainRoot}/{terrain}/T_Terrain_{terrain}_BaseColor.png";
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try
                {
                    Assert.That(texture.LoadImage(bytes, false), Is.True, path);
                    Assert.That(texture.width, Is.EqualTo(2048), path);
                    Assert.That(texture.height, Is.EqualTo(2048), path);
                    means[index] = SampleMean(texture, 64);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Color wasteland = means[(int)FirstArtTerrainLayer3D.Wasteland];
            Color rocky = means[(int)FirstArtTerrainLayer3D.Rocky];
            Color wetland = means[(int)FirstArtTerrainLayer3D.Wetland];
            Color crystal = means[(int)FirstArtTerrainLayer3D.Crystal];
            Color ruins = means[(int)FirstArtTerrainLayer3D.Ruins];
            Color water = means[(int)FirstArtTerrainLayer3D.DeepWater];
            Color cliff = means[(int)FirstArtTerrainLayer3D.Cliff];

            Assert.That(wasteland.r, Is.GreaterThan(wasteland.b + .18f));
            Assert.That(MaxChannel(rocky) - MinChannel(rocky), Is.LessThan(.16f));
            Assert.That(wetland.g, Is.GreaterThan(wetland.b));
            Assert.That(crystal.b, Is.GreaterThan(crystal.r));
            Assert.That(MaxChannel(ruins) - MinChannel(ruins), Is.LessThan(.18f));
            Assert.That(water.b, Is.GreaterThan(water.r * 1.35f));
            Assert.That(Luminance(water), Is.LessThan(.28f));
            Assert.That(cliff.r, Is.GreaterThan(cliff.b));

            Assert.That(
                Vector3.Distance(ToRgb(wasteland), ToRgb(rocky)),
                Is.GreaterThan(.18f));
            Assert.That(
                Vector3.Distance(ToRgb(wetland), ToRgb(crystal)),
                Is.GreaterThan(.14f));
            Assert.That(
                Vector3.Distance(ToRgb(water), ToRgb(crystal)),
                Is.GreaterThan(.2f));
        }

        [Test]
        [Category("TerrainAssetDeep")]
        public void IDEA0018_SourceChannelsMatchTheRebuiltCartographicBaseColors()
        {
            foreach (string terrain in TerrainNames)
            {
                Texture2D baseColor = LoadSource(terrain, "BaseColor");
                Texture2D normal = LoadSource(terrain, "Normal");
                Texture2D mask = LoadSource(terrain, "Mask");
                string heightPath =
                    $"{TerrainRoot}/{terrain}/T_Terrain_{terrain}_Height.png";
                IDisposable heightReadability = null;
                try
                {
                    heightReadability =
                        FirstArtPassImportPolicy.AllowTemporaryReadability(
                            heightPath);
                    AssetDatabase.ImportAsset(
                        heightPath,
                        ImportAssetOptions.ForceUpdate |
                        ImportAssetOptions.ForceSynchronousImport);
                    Texture2D height =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath);
                    Assert.That(height, Is.Not.Null, heightPath);
                    Assert.That(height.width, Is.EqualTo(2048), terrain);
                    Assert.That(height.format,
                        Is.EqualTo(TextureFormat.R16),
                        terrain + " Height source format");
                    Assert.That(normal.width, Is.EqualTo(2048), terrain);
                    Assert.That(mask.width, Is.EqualTo(2048), terrain);
                    var heightPixels = height.GetRawTextureData<ushort>();

                    var luminanceSamples = new List<float>(4096);
                    var heightSamples = new List<float>(4096);
                    var heightGradientX = new List<float>(4096);
                    var heightGradientY = new List<float>(4096);
                    var normalX = new List<float>(4096);
                    var normalY = new List<float>(4096);
                    double laplacianSquared = 0d;
                    double normalZ = 0d;
                    double metallic = 0d;
                    double ao = 0d;
                    double detail = 0d;
                    double detailSquared = 0d;
                    double smoothness = 0d;
                    int count = 0;
                    for (int y = 16; y < 2048; y += 32)
                    for (int x = 16; x < 2048; x += 32)
                    {
                        Color basePixel = baseColor.GetPixel(x, y);
                        float center = HeightAt(
                            heightPixels[y * height.width + x]);
                        float left = HeightAt(
                            heightPixels[y * height.width + x - 1]);
                        float right = HeightAt(
                            heightPixels[y * height.width + x + 1]);
                        float down = HeightAt(
                            heightPixels[(y - 1) * height.width + x]);
                        float up = HeightAt(
                            heightPixels[(y + 1) * height.width + x]);
                        Color normalPixel = normal.GetPixel(x, y);
                        Color maskPixel = mask.GetPixel(x, y);

                        luminanceSamples.Add(Luminance(basePixel));
                        heightSamples.Add(center);
                        heightGradientX.Add(-(right - left));
                        heightGradientY.Add(-(up - down));
                        normalX.Add(normalPixel.r * 2f - 1f);
                        normalY.Add(normalPixel.g * 2f - 1f);
                        float laplacian = center * 4f - left - right - down - up;
                        laplacianSquared += laplacian * laplacian;
                        normalZ += normalPixel.b * 2f - 1f;
                        metallic += maskPixel.r;
                        ao += maskPixel.g;
                        detail += maskPixel.b;
                        detailSquared += maskPixel.b * maskPixel.b;
                        smoothness += maskPixel.a;
                        count++;
                    }

                    Assert.That(
                        Pearson(luminanceSamples, heightSamples),
                        Is.GreaterThan(.25f),
                        terrain + " BaseColor/Height correlation");
                    Assert.That(
                        Pearson(heightGradientX, normalX),
                        Is.GreaterThan(.65f),
                        terrain + " Height/Normal X correlation");
                    Assert.That(
                        Pearson(heightGradientY, normalY),
                        Is.GreaterThan(.65f),
                        terrain + " Height/Normal Y correlation");

                    double highFrequency = Math.Sqrt(
                        laplacianSquared / count);
                    Assert.That(highFrequency,
                        Is.InRange(.000001d, .003d),
                        terrain + " controlled high-frequency amplitude");
                    Assert.That(
                        normalZ / count,
                        Is.GreaterThan(terrain == "DeepWater" ? .985d : .94d),
                        terrain + " low-amplitude normal");
                    AssertHeightSeamless(
                        heightPixels,
                        height.width,
                        terrain + " Height",
                        .65f);
                    AssertSeamless(baseColor, terrain + " BaseColor", .85f);
                    AssertEdgeBandGradients(
                        baseColor,
                        terrain + " BaseColor",
                        96,
                        1.5f);
                    AssertSeamless(normal, terrain + " Normal", .75f);
                    AssertSeamless(mask, terrain + " Mask", .75f);

                    double metallicMean = metallic / count;
                    double aoMean = ao / count;
                    double detailMean = detail / count;
                    double detailVariance =
                        detailSquared / count - detailMean * detailMean;
                    double smoothnessMean = smoothness / count;
                    Assert.That(metallicMean,
                        Is.LessThan(terrain == "Ruins" ? .08d : .02d),
                        terrain + " Mask.R Metallic");
                    Assert.That(aoMean, Is.InRange(.75d, 1d),
                        terrain + " Mask.G AO");
                    Assert.That(Math.Sqrt(Math.Max(0d, detailVariance)),
                        Is.GreaterThan(.025d),
                        terrain + " Mask.B Detail");
                    Assert.That(smoothnessMean,
                        terrain == "DeepWater"
                            ? Is.InRange(.72d, .96d)
                            : Is.InRange(.06d, .62d),
                        terrain + " Mask.A Smoothness");
                }
                finally
                {
                    heightReadability?.Dispose();
                    AssetDatabase.ImportAsset(
                        heightPath,
                        ImportAssetOptions.ForceUpdate |
                        ImportAssetOptions.ForceSynchronousImport);
                    UnityEngine.Object.DestroyImmediate(baseColor);
                    UnityEngine.Object.DestroyImmediate(normal);
                    UnityEngine.Object.DestroyImmediate(mask);
                }
            }
        }

        private static Texture2D LoadSource(string terrain, string channel)
        {
            string path =
                $"{TerrainRoot}/{terrain}/T_Terrain_{terrain}_{channel}.png";
            var texture = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false,
                true);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path), false),
                Is.True,
                path);
            return texture;
        }

        private static float Pearson(
            IReadOnlyList<float> left,
            IReadOnlyList<float> right)
        {
            Assert.That(left.Count, Is.EqualTo(right.Count));
            double leftMean = 0d;
            double rightMean = 0d;
            for (int index = 0; index < left.Count; index++)
            {
                leftMean += left[index];
                rightMean += right[index];
            }
            leftMean /= left.Count;
            rightMean /= right.Count;
            double covariance = 0d;
            double leftVariance = 0d;
            double rightVariance = 0d;
            for (int index = 0; index < left.Count; index++)
            {
                double a = left[index] - leftMean;
                double b = right[index] - rightMean;
                covariance += a * b;
                leftVariance += a * a;
                rightVariance += b * b;
            }
            return (float)(covariance /
                Math.Sqrt(leftVariance * rightVariance));
        }

        private static void AssertSeamless(
            Texture2D texture,
            string context,
            float maximumRatio)
        {
            double edge = 0d;
            double interior = 0d;
            int count = 0;
            for (int coordinate = 16;
                 coordinate < texture.width;
                 coordinate += 32)
            {
                edge += ColorDistance(
                    texture.GetPixel(0, coordinate),
                    texture.GetPixel(texture.width - 1, coordinate));
                edge += ColorDistance(
                    texture.GetPixel(coordinate, 0),
                    texture.GetPixel(coordinate, texture.height - 1));
                interior += ColorDistance(
                    texture.GetPixel(coordinate - 1, coordinate),
                    texture.GetPixel(coordinate, coordinate));
                interior += ColorDistance(
                    texture.GetPixel(coordinate, coordinate - 1),
                    texture.GetPixel(coordinate, coordinate));
                count += 2;
            }
            Assert.That(
                edge / count,
                Is.LessThanOrEqualTo(interior / count * maximumRatio +
                    2d / 65535d),
                context + " edge seam");
        }

        private static void AssertHeightSeamless(
            Unity.Collections.NativeArray<ushort> pixels,
            int size,
            string context,
            float maximumRatio)
        {
            double edge = 0d;
            double interior = 0d;
            int count = 0;
            for (int coordinate = 16;
                 coordinate < size;
                 coordinate += 32)
            {
                edge += Math.Abs(
                    HeightAt(pixels[coordinate * size]) -
                    HeightAt(pixels[coordinate * size + size - 1]));
                edge += Math.Abs(
                    HeightAt(pixels[coordinate]) -
                    HeightAt(pixels[(size - 1) * size + coordinate]));
                interior += Math.Abs(
                    HeightAt(pixels[coordinate * size + coordinate - 1]) -
                    HeightAt(pixels[coordinate * size + coordinate]));
                interior += Math.Abs(
                    HeightAt(pixels[(coordinate - 1) * size + coordinate]) -
                    HeightAt(pixels[coordinate * size + coordinate]));
                count += 2;
            }
            Assert.That(
                edge / count,
                Is.LessThanOrEqualTo(interior / count * maximumRatio +
                    2d / 65535d),
                context + " edge seam");
        }

        private static void AssertEdgeBandGradients(
            Texture2D texture,
            string context,
            int bandWidth,
            float maximumRatio)
        {
            double edgeBand = 0d;
            double interior = 0d;
            int edgeCount = 0;
            int interiorCount = 0;
            int centerStart = texture.width / 2 - bandWidth / 2;
            for (int coordinate = 16;
                 coordinate < texture.width;
                 coordinate += 32)
            for (int distance = 1;
                 distance < bandWidth;
                 distance += 8)
            {
                edgeBand += ColorDistance(
                    texture.GetPixel(distance - 1, coordinate),
                    texture.GetPixel(distance, coordinate));
                edgeBand += ColorDistance(
                    texture.GetPixel(texture.width - distance, coordinate),
                    texture.GetPixel(
                        texture.width - distance - 1,
                        coordinate));
                edgeBand += ColorDistance(
                    texture.GetPixel(coordinate, distance - 1),
                    texture.GetPixel(coordinate, distance));
                edgeBand += ColorDistance(
                    texture.GetPixel(coordinate, texture.height - distance),
                    texture.GetPixel(
                        coordinate,
                        texture.height - distance - 1));
                edgeCount += 4;

                int center = centerStart + distance;
                interior += ColorDistance(
                    texture.GetPixel(center - 1, coordinate),
                    texture.GetPixel(center, coordinate));
                interior += ColorDistance(
                    texture.GetPixel(coordinate, center - 1),
                    texture.GetPixel(coordinate, center));
                interiorCount += 2;
            }
            Assert.That(
                edgeBand / edgeCount,
                Is.LessThanOrEqualTo(
                    interior / interiorCount * maximumRatio + 1d / 255d),
                context + " blended edge-band gradient");
        }

        private static float HeightAt(ushort value)
        {
            return value / 65535f;
        }

        private static float ColorDistance(Color left, Color right)
        {
            float red = Mathf.Abs(left.r - right.r);
            float green = Mathf.Abs(left.g - right.g);
            float blue = Mathf.Abs(left.b - right.b);
            float alpha = Mathf.Abs(left.a - right.a);
            return (red + green + blue + alpha) * .25f;
        }

        private static Color SampleMean(Texture2D texture, int step)
        {
            Vector4 sum = Vector4.zero;
            int count = 0;
            for (int y = step / 2; y < texture.height; y += step)
            for (int x = step / 2; x < texture.width; x += step)
            {
                sum += (Vector4)texture.GetPixel(x, y);
                count++;
            }

            return (Color)(sum / count);
        }

        private static float Luminance(Color color)
        {
            return color.r * .2126f + color.g * .7152f + color.b * .0722f;
        }

        private static float MaxChannel(Color color)
        {
            return Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        }

        private static float MinChannel(Color color)
        {
            return Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        }

        private static Vector3 ToRgb(Color color)
        {
            return new Vector3(color.r, color.g, color.b);
        }
    }
}
