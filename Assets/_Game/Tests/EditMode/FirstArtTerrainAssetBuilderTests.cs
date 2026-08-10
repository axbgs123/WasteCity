using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using WasteCity.Editor;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainAssetBuilderTests
    {
        private const string TerrainRoot =
            "Assets/_Game/Art/FirstPass/Environment/Terrain";

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

        private static readonly string[] Channels =
        {
            "BaseColor",
            "Normal",
            "Mask",
            "Height",
        };

        private static readonly string[] ArrayPaths =
        {
            FirstArtTerrainAssetBuilder.BaseColorArrayPath,
            FirstArtTerrainAssetBuilder.NormalArrayPath,
            FirstArtTerrainAssetBuilder.MaskArrayPath,
            FirstArtTerrainAssetBuilder.HeightArrayPath,
        };

        [Test]
        public void BuildTextureArrays_UsesFrozenLayerOrderAndFormats()
        {
            FirstArtTerrainAssetBuilder.BuildTextureArrays();

            Texture2DArray baseColor = LoadArray(
                FirstArtTerrainAssetBuilder.BaseColorArrayPath);
            Texture2DArray normal = LoadArray(
                FirstArtTerrainAssetBuilder.NormalArrayPath);
            Texture2DArray mask = LoadArray(
                FirstArtTerrainAssetBuilder.MaskArrayPath);
            Texture2DArray height = LoadArray(
                FirstArtTerrainAssetBuilder.HeightArrayPath);

            AssertArrayContract(baseColor, 2048, TexturePath("Wasteland", "BaseColor"));
            AssertArrayContract(normal, 2048, TexturePath("Wasteland", "Normal"));
            AssertArrayContract(mask, 2048, TexturePath("Wasteland", "Mask"));
            AssertArrayContract(height, 1024, null);
            Assert.That(height.format, Is.EqualTo(TextureFormat.R8));

            for (int slice = 0; slice < TerrainNames.Length; slice++)
            {
                string sourcePath = TexturePath(TerrainNames[slice], "BaseColor");
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                Assert.That(source, Is.Not.Null, sourcePath);

                Color32 expected = ReadCenterPixel(source);
                Color32 actual = ReadCenterPixel(baseColor, slice);
                AssertColorWithinOneByte(actual, expected, sourcePath);
            }

            AssertHeightBlockMatchesSource(height, 0, "Wasteland", 317, 743);

            foreach (string path in SourcePaths())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.isReadable, Is.False, path);
            }
        }

        [Test]
        public void BuildTextureArrays_TwoBuildsPreserveSourceAndGeneratedIdentity()
        {
            Dictionary<string, AssetState> sourceBefore = CaptureSourceStates();

            FirstArtTerrainAssetBuilder.BuildTextureArrays();
            Dictionary<string, AssetState> sourceAfterFirst = CaptureSourceStates();
            Dictionary<string, AssetState> arraysAfterFirst = CaptureArrayStates();

            FirstArtTerrainAssetBuilder.BuildTextureArrays();
            Dictionary<string, AssetState> sourceAfterSecond = CaptureSourceStates();
            Dictionary<string, AssetState> arraysAfterSecond = CaptureArrayStates();

            AssertStatesEqual(sourceAfterFirst, sourceBefore, "first source build");
            AssertStatesEqual(sourceAfterSecond, sourceBefore, "second source build");
            AssertStatesEqual(arraysAfterSecond, arraysAfterFirst, "generated array rerun");
        }

        [Test]
        public void BuildTextureArrays_MissingSourceLeavesGeneratedAssetsUntouched()
        {
            FirstArtTerrainAssetBuilder.BuildTextureArrays();
            Dictionary<string, AssetState> before = CaptureArrayStates();
            string sourcePath = TexturePath("Cliff", "Height");
            string backupPath = sourcePath + ".task4-missing";
            Assert.That(File.Exists(backupPath), Is.False, backupPath);

            File.Move(sourcePath, backupPath);
            try
            {
                Assert.That(
                    () => FirstArtTerrainAssetBuilder.BuildTextureArrays(),
                    Throws.TypeOf<FileNotFoundException>());
                AssertStatesEqual(CaptureArrayStates(), before, "missing-source abort");
            }
            finally
            {
                if (File.Exists(backupPath))
                    File.Move(backupPath, sourcePath);
            }
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(65535, 65535, 65535, 65535, 255)]
        [TestCase(0, 256, 514, 65535, 64)]
        [TestCase(128, 129, 128, 129, 1)]
        public void QuantizeHeightBlock_UsesRoundedAverageAndUshortToByteRounding(
            int a,
            int b,
            int c,
            int d,
            int expected)
        {
            byte actual = FirstArtTerrainAssetBuilder.QuantizeHeightBlock(
                (ushort)a,
                (ushort)b,
                (ushort)c,
                (ushort)d);

            Assert.That(actual, Is.EqualTo((byte)expected));
        }

        private static void AssertArrayContract(
            Texture2DArray array,
            int expectedSize,
            string formatSourcePath)
        {
            Assert.That(array.depth, Is.EqualTo(7));
            Assert.That(array.width, Is.EqualTo(expectedSize));
            Assert.That(array.height, Is.EqualTo(expectedSize));
            Assert.That(array.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(array.mipmapCount, Is.GreaterThan(1));

            if (formatSourcePath == null)
                return;

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(formatSourcePath);
            Assert.That(source, Is.Not.Null, formatSourcePath);
            Assert.That(array.format, Is.EqualTo(source.format), formatSourcePath);
            Assert.That(array.mipmapCount, Is.EqualTo(source.mipmapCount), formatSourcePath);
        }

        private static Texture2DArray LoadArray(string path)
        {
            Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            Assert.That(array, Is.Not.Null, path);
            return array;
        }

        private static string TexturePath(string terrain, string channel)
        {
            return $"{TerrainRoot}/{terrain}/T_Terrain_{terrain}_{channel}.png";
        }

        private static IEnumerable<string> SourcePaths()
        {
            foreach (string terrain in TerrainNames)
            foreach (string channel in Channels)
                yield return TexturePath(terrain, channel);
        }

        private static Dictionary<string, AssetState> CaptureSourceStates()
        {
            var states = new Dictionary<string, AssetState>(StringComparer.Ordinal);
            foreach (string path in SourcePaths())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                states.Add(
                    path,
                    new AssetState(
                        AssetDatabase.AssetPathToGUID(path),
                        AssetDatabase.GetAssetDependencyHash(path),
                        importer.isReadable,
                        File.ReadAllBytes(path + ".meta")));
            }

            return states;
        }

        private static Dictionary<string, AssetState> CaptureArrayStates()
        {
            var states = new Dictionary<string, AssetState>(StringComparer.Ordinal);
            foreach (string path in ArrayPaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2DArray>(path), Is.Not.Null, path);
                states.Add(
                    path,
                    new AssetState(
                        AssetDatabase.AssetPathToGUID(path),
                        AssetDatabase.GetAssetDependencyHash(path),
                        false,
                        File.ReadAllBytes(path + ".meta")));
            }

            return states;
        }

        private static void AssertStatesEqual(
            Dictionary<string, AssetState> actual,
            Dictionary<string, AssetState> expected,
            string context)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), context);
            foreach (KeyValuePair<string, AssetState> pair in expected)
            {
                AssetState value = actual[pair.Key];
                Assert.That(value.Guid, Is.EqualTo(pair.Value.Guid), $"{context}: {pair.Key} GUID");
                Assert.That(value.DependencyHash, Is.EqualTo(pair.Value.DependencyHash),
                    $"{context}: {pair.Key} dependency hash");
                Assert.That(value.IsReadable, Is.EqualTo(pair.Value.IsReadable),
                    $"{context}: {pair.Key} readability");
                Assert.That(value.MetaBytes, Is.EqualTo(pair.Value.MetaBytes),
                    $"{context}: {pair.Key} meta bytes");
            }
        }

        private static Color32 ReadCenterPixel(Texture2D source)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                pixel.ReadPixels(
                    new Rect(source.width / 2, source.height / 2, 1, 1),
                    0,
                    0,
                    false);
                pixel.Apply(false, false);
                return pixel.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.DestroyImmediate(pixel);
            }
        }

        private static Color32 ReadCenterPixel(Texture2DArray array, int slice)
        {
            var texture = new Texture2D(
                array.width,
                array.height,
                array.format,
                false,
                false);
            try
            {
                Graphics.CopyTexture(array, slice, 0, texture, 0, 0);
                return ReadCenterPixel(texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AssertColorWithinOneByte(
            Color32 actual,
            Color32 expected,
            string context)
        {
            Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThanOrEqualTo(1), context + " R");
            Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThanOrEqualTo(1), context + " G");
            Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThanOrEqualTo(1), context + " B");
            Assert.That(Mathf.Abs(actual.a - expected.a), Is.LessThanOrEqualTo(1), context + " A");
        }

        private static void AssertHeightBlockMatchesSource(
            Texture2DArray height,
            int slice,
            string terrain,
            int outputX,
            int outputY)
        {
            string path = TexturePath(terrain, "Height");
            byte[] metaBefore = File.ReadAllBytes(path + ".meta");
            IDisposable readabilityScope = null;
            try
            {
                readabilityScope = FirstArtPassImportPolicy.AllowTemporaryReadability(path);
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);

                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(source, Is.Not.Null, path);
                Assert.That(source.format, Is.EqualTo(TextureFormat.R16), path);
                NativeArray<ushort> pixels = source.GetPixelData<ushort>(0);
                int sourceX = outputX * 2;
                int sourceY = outputY * 2;
                int first = sourceY * source.width + sourceX;
                uint average = (
                    (uint)pixels[first] +
                    pixels[first + 1] +
                    pixels[first + source.width] +
                    pixels[first + source.width + 1] +
                    2u) / 4u;
                byte expected = (byte)((average + 128u) / 257u);

                NativeArray<byte> generated = height.GetPixelData<byte>(0, slice);
                byte actual = generated[outputY * height.width + outputX];
                Assert.That(actual, Is.EqualTo(expected), path);
            }
            finally
            {
                readabilityScope?.Dispose();
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metaBefore), path);
            }
        }

        private sealed class AssetState
        {
            public AssetState(string guid, Hash128 dependencyHash, bool isReadable, byte[] metaBytes)
            {
                Guid = guid;
                DependencyHash = dependencyHash;
                IsReadable = isReadable;
                MetaBytes = metaBytes;
            }

            public string Guid { get; }

            public Hash128 DependencyHash { get; }

            public bool IsReadable { get; }

            public byte[] MetaBytes { get; }
        }
    }
}
