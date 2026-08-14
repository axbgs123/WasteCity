using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace WasteCity.Tests
{
    public sealed class FirstArtRuinsCliffEvidenceCaptureTests
    {
        private const string CaptureTypeName =
            "WasteCity.Editor.FirstArtRuinsCliffEvidenceCapture, WasteCity.Editor";
        private const int ExpectedCaptureWidth = 1280;
        private const int ExpectedCaptureHeight = 720;

        private static readonly string[] ExpectedCaptures =
        {
            "01-default-camera.png",
            "02-top-view.png",
            "03-ruins-closeup.png",
            "04-cliff-straight-a.png",
            "05-cliff-straight-b.png",
            "06-cliff-inner-corner.png",
            "07-cliff-outer-corner.png",
            "08-cliff-end-cap.png",
            "09-cliff-top-cap.png",
            "10-both-presented.png",
            "11-ruins-fallback.png",
            "12-cliff-fallback.png",
        };

        [Test]
        public void IDEA0004_AutomatedEntryIsPublicStaticParameterlessAndOutputIsExternal()
        {
            Type captureType = RequireCaptureType();
            MethodInfo entry = captureType.GetMethod(
                "StartAutomatedCapture",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(entry.GetParameters(), Is.Empty);

            MethodInfo validator = RequireMethod(
                captureType,
                "ValidateOutputDirectoryForTests");
            string projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            string external = Path.Combine(
                Path.GetTempPath(),
                "WasteCity-RuinsCliff-Evidence-" + Guid.NewGuid().ToString("N"));

            Assert.That(
                Invoke<string>(validator, external, projectRoot),
                Is.EqualTo(Path.GetFullPath(external)));
            AssertInvalidOperation(validator, null, projectRoot);
            AssertInvalidOperation(validator, "relative/evidence", projectRoot);
            AssertInvalidOperation(
                validator,
                Path.Combine(projectRoot, "Library", "evidence"),
                projectRoot);

            string caseVariantProjectRoot = ToggleAsciiCase(projectRoot);
            Assert.That(caseVariantProjectRoot, Is.Not.EqualTo(projectRoot));
            AssertInvalidOperation(
                validator,
                caseVariantProjectRoot,
                projectRoot);
            AssertInvalidOperation(
                validator,
                Path.Combine(caseVariantProjectRoot, "Library", "evidence"),
                projectRoot);
        }

        [Test]
        public void IDEA0004_RequiredCaptureSetFreezesApprovedViewsAndFallbackMatrix()
        {
            Type captureType = RequireCaptureType();
            PropertyInfo property = captureType.GetProperty(
                "RequiredCaptureFileNames",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null);
            string[] actual = ((IEnumerable<string>)property.GetValue(null)).ToArray();

            CollectionAssert.AreEqual(ExpectedCaptures, actual);
            Assert.That(actual, Is.Unique);
        }

        [Test]
        public void IDEA0004_PixelGateRejectsBlackAndMissingShaderMagenta()
        {
            MethodInfo validator = RequireMethod(
                RequireCaptureType(),
                "ValidatePixelsForTests");
            const int width = 16;
            const int height = 16;

            Color32[] valid = Enumerable.Repeat(
                new Color32(84, 68, 46, 255),
                width * height).ToArray();
            Assert.That(
                () => validator.Invoke(
                    null,
                    new object[] { valid, width, height, "valid.png" }),
                Throws.Nothing);

            Color32[] black = Enumerable.Repeat(
                new Color32(0, 0, 0, 255),
                width * height).ToArray();
            AssertInvalidOperation(
                validator,
                black,
                width,
                height,
                "black.png");

            Color32[] magenta = Enumerable.Repeat(
                new Color32(255, 0, 255, 255),
                width * height).ToArray();
            AssertInvalidOperation(
                validator,
                magenta,
                width,
                height,
                "magenta.png");
        }

        [Test]
        public void IDEA0004_ManifestValidationRequiresAllFilesHashesMatricesAndAssetGuids()
        {
            MethodInfo validator = RequireMethod(
                RequireCaptureType(),
                "ValidateEvidenceDirectoryForTests");
            string root = Path.Combine(
                Path.GetTempPath(),
                "WasteCity-RuinsCliff-Manifest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestManifest manifest = CreateValidManifest(root);
                WriteManifest(root, manifest);
                Assert.That(
                    () => validator.Invoke(null, new object[] { root }),
                    Throws.Nothing);

                string missing = Path.Combine(root, ExpectedCaptures[0]);
                File.Delete(missing);
                AssertInvalidOperation(validator, root);

                File.WriteAllBytes(missing, CaptureBytes(0));
                WriteManifest(root, manifest);
                File.WriteAllBytes(missing, new byte[] { 9, 9, 9 });
                AssertInvalidOperation(validator, root);

                File.WriteAllBytes(missing, CaptureBytes(0));
                manifest.geometryProfileGuid = string.Empty;
                WriteManifest(root, manifest);
                AssertInvalidOperation(validator, root);

                manifest.geometryProfileGuid = GuidText(2);
                manifest.captures[5].projectionMatrix = string.Empty;
                WriteManifest(root, manifest);
                AssertInvalidOperation(validator, root);

                manifest.captures[5].projectionMatrix =
                    "1,0,0,0|0,1,0,0|0,0,1,0|0,0,0,1";
                manifest.materialGuids = manifest.materialGuids.Take(12).ToArray();
                WriteManifest(root, manifest);
                AssertInvalidOperation(validator, root);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void IDEA0004_ManifestAuthenticatesExactCaptureDimensions()
        {
            Type captureType = RequireCaptureType();
            Assert.That(
                captureType.GetField("CaptureWidth", BindingFlags.Public | BindingFlags.Static)
                    ?.GetRawConstantValue(),
                Is.EqualTo(ExpectedCaptureWidth));
            Assert.That(
                captureType.GetField("CaptureHeight", BindingFlags.Public | BindingFlags.Static)
                    ?.GetRawConstantValue(),
                Is.EqualTo(ExpectedCaptureHeight));

            MethodInfo validator = RequireMethod(
                captureType,
                "ValidateEvidenceDirectoryForTests");
            string root = Path.Combine(
                Path.GetTempPath(),
                "WasteCity-RuinsCliff-Dimensions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestManifest manifest = CreateValidManifest(root);
                byte[] small = CaptureBytes(0, 4, 4);
                string filename = ExpectedCaptures[0];
                File.WriteAllBytes(Path.Combine(root, filename), small);
                manifest.captures[0].sha256 = Sha256(small);
                WriteManifest(root, manifest);

                AssertInvalidOperation(validator, root);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void IDEA0004_RuinsCloseupUsesOneDeterministicPrefabFixture()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FirstArtRuinsCliffEvidenceCapture.cs"));

            StringAssert.Contains(
                "FirstArtRuinsCliffCatalog3D.Entries[0]",
                source);
            StringAssert.Contains("CaptureSinglePrefabFixture", source);
            StringAssert.DoesNotContain(
                "FocusRuntimeCategory(context, \"RuinsGeometry\")",
                source);
        }

        private static TestManifest CreateValidManifest(string root)
        {
            var records = new List<TestCaptureRecord>();
            for (int index = 0; index < ExpectedCaptures.Length; index++)
            {
                byte[] bytes = CaptureBytes(index);
                string filename = ExpectedCaptures[index];
                File.WriteAllBytes(Path.Combine(root, filename), bytes);
                records.Add(new TestCaptureRecord
                {
                    filename = filename,
                    sha256 = Sha256(bytes),
                    result = "passed",
                    ruinsStatus = index == 10 ? "Fallback" : "Presented",
                    cliffStatus = index == 11 ? "Fallback" : "Presented",
                    worldToCameraMatrix = "1,0,0,0|0,1,0,0|0,0,1,0|0,0,0,1",
                    projectionMatrix = "1,0,0,0|0,1,0,0|0,0,1,0|0,0,0,1",
                });
            }

            return new TestManifest
            {
                scene = "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                seed = 8128,
                terrainProfileGuid = GuidText(1),
                geometryProfileGuid = GuidText(2),
                materialGuids = Enumerable.Range(10, 13)
                    .Select(GuidText).ToArray(),
                prefabGuids = Enumerable.Range(30, 14)
                    .Select(GuidText).ToArray(),
                captures = records.ToArray(),
                captureResult = "passed",
            };
        }

        private static byte[] CaptureBytes(
            int index,
            int width = ExpectedCaptureWidth,
            int height = ExpectedCaptureHeight)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32 color = new Color32(
                (byte)(70 + index),
                (byte)(55 + index),
                (byte)(35 + index),
                255);
            texture.SetPixels32(
                Enumerable.Repeat(color, checked(width * height)).ToArray());
            texture.Apply(false, false);
            try
            {
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string GuidText(int value)
        {
            return value.ToString("x32");
        }

        private static string ToggleAsciiCase(string value)
        {
            var characters = value.ToCharArray();
            for (int index = 0; index < characters.Length; index++)
            {
                char character = characters[index];
                if (character >= 'a' && character <= 'z')
                {
                    characters[index] = char.ToUpperInvariant(character);
                    return new string(characters);
                }
                if (character >= 'A' && character <= 'Z')
                {
                    characters[index] = char.ToLowerInvariant(character);
                    return new string(characters);
                }
            }
            throw new InvalidOperationException(
                "The project path must contain an ASCII letter for this test.");
        }

        private static void WriteManifest(string root, TestManifest manifest)
        {
            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                JsonUtility.ToJson(manifest, true));
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(
                    sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static Type RequireCaptureType()
        {
            Type type = Type.GetType(CaptureTypeName, false);
            Assert.That(
                type,
                Is.Not.Null,
                "Task 8 requires the dedicated Ruins/Cliff evidence capture type.");
            return type;
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

        private static T Invoke<T>(MethodInfo method, params object[] arguments)
        {
            return (T)method.Invoke(null, arguments);
        }

        private static void AssertInvalidOperation(
            MethodInfo method,
            params object[] arguments)
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => method.Invoke(null, arguments));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Serializable]
        private sealed class TestManifest
        {
            public string scene;
            public int seed;
            public string terrainProfileGuid;
            public string geometryProfileGuid;
            public string[] materialGuids;
            public string[] prefabGuids;
            public TestCaptureRecord[] captures;
            public string captureResult;
        }

        [Serializable]
        private sealed class TestCaptureRecord
        {
            public string filename;
            public string sha256;
            public string result;
            public string ruinsStatus;
            public string cliffStatus;
            public string worldToCameraMatrix;
            public string projectionMatrix;
        }
    }
}
