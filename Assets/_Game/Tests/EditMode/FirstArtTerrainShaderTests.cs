using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using WasteCity.ArtIntegration3D;
using WasteCity.Editor;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainShaderTests
    {
        private static readonly string[] FrozenPropertyNames =
        {
            "_BaseColorArray",
            "_NormalArray",
            "_MaskArray",
            "_HeightArray",
            "_ControlA",
            "_ControlB",
            "_WorldOriginXZ",
            "_WorldSizeXZ",
            "_CellsPerTexture",
            "_HeightBlendStrength",
            "_MacroVariation",
            "_WaterVelocityA",
            "_WaterVelocityB",
            "_WaterNormalScaleB",
            "_WaterHighlightStrength",
            "_WastelandTint",
            "_RockyTint",
            "_WetlandTint",
            "_CrystalTint",
            "_RuinsTint",
            "_DeepWaterTint",
            "_CliffTint",
            "_DeepWaterNormalStrength",
        };

        [Test]
        public void MasterShader_CompilesAndExposesFrozenProperties()
        {
            Shader shader = Shader.Find(FirstArtTerrainProfile3D.RequiredShaderName);

            Assert.That(shader, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(shader), Is.EqualTo(FirstArtTerrainAssetBuilder.ShaderPath));
            Assert.That(
                ShaderUtil.GetShaderMessages(shader)
                    .Where(message => message.severity == ShaderCompilerMessageSeverity.Error),
                Is.Empty);
            Assert.That(shader.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
            var material = new Material(shader);
            try
            {
                Assert.That(material.FindPass("UniversalForward"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Opaque"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
            CollectionAssert.AreEquivalent(
                FrozenPropertyNames,
                Enumerable.Range(0, shader.GetPropertyCount())
                    .Select(shader.GetPropertyName));

            AssertTextureProperty(shader, "_BaseColorArray", TextureDimension.Tex2DArray);
            AssertTextureProperty(shader, "_NormalArray", TextureDimension.Tex2DArray);
            AssertTextureProperty(shader, "_MaskArray", TextureDimension.Tex2DArray);
            AssertTextureProperty(shader, "_HeightArray", TextureDimension.Tex2DArray);
            AssertTextureProperty(shader, "_ControlA", TextureDimension.Tex2D);
            AssertTextureProperty(shader, "_ControlB", TextureDimension.Tex2D);
            AssertVectorProperty(shader, "_WorldOriginXZ", Vector4.zero);
            AssertVectorProperty(shader, "_WorldSizeXZ", new Vector4(1f, 1f, 0f, 0f));
            AssertFloatProperty(shader, "_CellsPerTexture", 4f);
            AssertFloatProperty(shader, "_HeightBlendStrength", 1f);
            AssertFloatProperty(shader, "_MacroVariation", 0.08f);
            AssertVectorProperty(shader, "_WaterVelocityA", new Vector4(0.006f, 0.002f, 0f, 0f));
            AssertVectorProperty(shader, "_WaterVelocityB", new Vector4(-0.003f, 0.005f, 0f, 0f));
            AssertFloatProperty(shader, "_WaterNormalScaleB", 1.35f);
            AssertFloatProperty(shader, "_WaterHighlightStrength", 0.21f);
            AssertColorProperty(shader, "_WastelandTint", new Color(.58f, .35f, .14f, .18f));
            AssertColorProperty(shader, "_RockyTint", new Color(.33f, .37f, .42f, .26f));
            AssertColorProperty(shader, "_WetlandTint", new Color(.31f, .40f, .20f, .30f));
            AssertColorProperty(shader, "_CrystalTint", new Color(.38f, .66f, .70f, .24f));
            AssertColorProperty(shader, "_RuinsTint", new Color(.43f, .40f, .37f, .22f));
            AssertColorProperty(shader, "_DeepWaterTint", new Color(.025f, .07f, .25f, .82f));
            AssertColorProperty(shader, "_CliffTint", new Color(.24f, .19f, .15f, .30f));
            AssertFloatProperty(shader, "_DeepWaterNormalStrength", 1.45f);
        }

        [Test]
        public void MasterShader_LayerColorGradingUsesExactUniqueMaterialValues()
        {
            Material material = LoadRequired<Material>(
                FirstArtTerrainAssetBuilder.MaterialPath);
            var expected = new Dictionary<string, Color>
            {
                { "_WastelandTint", FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(FirstArtTerrainLayer3D.Wasteland) },
                { "_RockyTint", FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(FirstArtTerrainLayer3D.Rocky) },
                { "_WetlandTint", FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(FirstArtTerrainLayer3D.Wetland) },
                { "_CrystalTint", FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(FirstArtTerrainLayer3D.Crystal) },
                { "_RuinsTint", FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(FirstArtTerrainLayer3D.Ruins) },
                { "_DeepWaterTint", FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(FirstArtTerrainLayer3D.DeepWater) },
                { "_CliffTint", FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(FirstArtTerrainLayer3D.Cliff) },
            };

            foreach (KeyValuePair<string, Color> pair in expected)
            {
                Assert.That(material.HasColor(pair.Key), Is.True, pair.Key);
                Assert.That(material.GetColor(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            }
            Assert.That(expected.Values, Is.Unique);
            Assert.That(material.GetColor("_WastelandTint").a, Is.EqualTo(.18f));
            Assert.That(
                material.GetFloat("_DeepWaterNormalStrength"),
                Is.EqualTo(1.45f).Within(.000001f));
        }

        [Test]
        public void MasterShader_GradesEachFullLayerBeforeTopThreeBlend()
        {
            string source = LoadShaderSource();
            const string gradeCall =
                "sample.baseColor.rgb = GradeLayerBaseColor(sample.baseColor.rgb, LayerTint(layerIndex));";
            int sampleFunction = source.IndexOf(
                "LayerSample SampleLayer(uint layerIndex, float2 worldUV)",
                StringComparison.Ordinal);
            int gradeIndex = source.IndexOf(gradeCall, StringComparison.Ordinal);
            int blendIndex = source.IndexOf(
                "half4 blendedBaseColor =",
                StringComparison.Ordinal);

            Assert.That(sampleFunction, Is.GreaterThanOrEqualTo(0));
            Assert.That(gradeIndex, Is.GreaterThan(sampleFunction));
            Assert.That(blendIndex, Is.GreaterThan(gradeIndex));
            Assert.That(CountOccurrences(source, gradeCall), Is.EqualTo(1));
            Assert.That(
                CountOccurrences(source, "LayerSample sample0 = SampleLayer("),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(source, "LayerSample sample1 = SampleLayer("),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(source, "LayerSample sample2 = SampleLayer("),
                Is.EqualTo(1));
            Assert.That(
                source,
                Does.Contain(
                    "float luminance = dot(sourceColor, float3(0.2126, 0.7152, 0.0722));"));
            Assert.That(
                source,
                Does.Contain(
                    "float3 graded = tint.rgb * lerp(0.65, 1.35, saturate(luminance));"));
            Assert.That(
                source,
                Does.Contain(
                    "return lerp(sourceColor, graded, saturate(tint.a));"));
            Assert.That(
                source,
                Does.Contain(
                    "min(saturate(_MacroVariation), 0.08)"));

            Color sourceColor = new Color(.4f, .3f, .2f, .73f);
            Color neutral = GradeLayerReference(
                sourceColor,
                new Color(.52f, .38f, .22f, 0f));
            Assert.That(neutral, Is.EqualTo(sourceColor));
            Color intermediate = GradeLayerReference(
                sourceColor,
                new Color(.28f, .40f, .22f, .42f));
            Assert.That(intermediate, Is.Not.EqualTo(sourceColor));
            Assert.That(intermediate.a, Is.EqualTo(sourceColor.a));
        }

        [Test]
        public void MasterShader_DefaultUrpForwardVariantWarmsWithoutCompilerErrors()
        {
            Shader shader = Shader.Find(FirstArtTerrainProfile3D.RequiredShaderName);
            Assert.That(shader, Is.Not.Null);
            var collection = new ShaderVariantCollection();
            var variant = new ShaderVariantCollection.ShaderVariant(
                shader,
                PassType.ScriptableRenderPipeline);

            Assert.That(collection.Add(variant), Is.True);
            collection.WarmUp();

            Assert.That(collection.isWarmedUp, Is.True);
            Assert.That(
                ShaderUtil.GetShaderMessages(shader)
                    .Where(message => message.severity == ShaderCompilerMessageSeverity.Error),
                Is.Empty);
        }

        [Test]
        public void MasterShader_ShadowCasterPassAndVariantCompile()
        {
            Shader shader = Shader.Find(FirstArtTerrainProfile3D.RequiredShaderName);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            var collection = new ShaderVariantCollection();
            var variant = new ShaderVariantCollection.ShaderVariant(
                shader,
                PassType.ShadowCaster);
            Assert.That(collection.Add(variant), Is.True);
            collection.WarmUp();

            Assert.That(collection.isWarmedUp, Is.True);
            Assert.That(
                ShaderUtil.GetShaderMessages(shader)
                    .Where(message => message.severity == ShaderCompilerMessageSeverity.Error),
                Is.Empty);
        }

        [Test]
        public void MasterShader_FragmentOutputAppliesUrpFogAndPreservesAlpha()
        {
            string source = LoadShaderSource();

            Assert.That(
                source,
                Does.Contain("half4 litColor = UniversalFragmentPBR(inputData, surfaceData);"));
            Assert.That(
                source,
                Does.Contain("litColor.rgb = MixFog(litColor.rgb, inputData.fogCoord);"));
            Assert.That(source, Does.Contain("return litColor;"));
        }

        [Test]
        public void MasterShader_DetailMaskGatesOnlyPerLayerTangentNormal()
        {
            string source = LoadShaderSource();

            Assert.That(
                source,
                Does.Contain(
                    "return SafeNormalizeTangentNormal(lerp(half3(0, 0, 1), decodedNormalTS, saturate(detailMask)));"));
            Assert.That(
                source,
                Does.Contain(
                    "sample.normalTS = ApplyDetailMaskToNormal(combinedNormalTS, sample.mask.b);"));
            Assert.That(CountOccurrences(source, "sample.mask.b"), Is.EqualTo(1));

            Vector3 decoded = new Vector3(0.6f, 0f, 0.8f);
            Assert.That(ApplyDetailMaskReference(decoded, 0f), Is.EqualTo(Vector3.forward));
            AssertVectorWithin(ApplyDetailMaskReference(decoded, 1f), decoded, 0.000001f);
            Vector3 intermediate = ApplyDetailMaskReference(decoded, 0.5f);
            AssertVectorWithin(
                intermediate,
                new Vector3(0.31622777f, 0f, 0.9486833f),
                0.000001f);
            Assert.That(intermediate, Is.Not.EqualTo(Vector3.forward));
            Assert.That(intermediate, Is.Not.EqualTo(decoded));
        }

        [Test]
        public void MasterShader_VertexAdditionalLightsReachInputData()
        {
            string source = LoadShaderSource();

            Assert.That(source, Does.Contain("half3 vertexLighting : TEXCOORD4;"));
            Assert.That(
                source,
                Does.Contain(
                    "output.vertexLighting = VertexLighting(output.positionWS, output.normalWS);"));
            Assert.That(
                source,
                Does.Contain("inputData.vertexLighting = input.vertexLighting;"));
        }

        [Test]
        public void MasterShader_FinalNormalUsesFiniteSafeFallback()
        {
            string source = LoadShaderSource();

            Assert.That(source, Does.Contain("half3 SafeNormalizeTangentNormal(float3 value)"));
            Assert.That(
                source,
                Does.Contain(
                    "if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) ||"));
            Assert.That(source, Does.Contain("lengthSquared <= 0.000001"));
            Assert.That(source, Does.Contain("return half3(0, 0, 1);"));
            Assert.That(
                source,
                Does.Contain("half3 blendedNormalTS = SafeNormalizeTangentNormal("));
        }

        [Test]
        public void MasterShader_DeepWaterUsesTwoDirectionsAndBoundedHighlight()
        {
            string source = LoadShaderSource();
            Material material = LoadRequired<Material>(
                FirstArtTerrainAssetBuilder.MaterialPath);

            Assert.That(
                material.GetFloat("_WaterNormalScaleB"),
                Is.EqualTo(1.35f).Within(.000001f));
            Assert.That(
                material.GetFloat("_WaterHighlightStrength"),
                Is.EqualTo(.21f).Within(.000001f));
            Assert.That(
                material.GetFloat("_DeepWaterNormalStrength"),
                Is.EqualTo(1.45f).Within(.000001f));
            Assert.That(
                material.GetVector("_WaterVelocityA"),
                Is.EqualTo(new Vector4(.006f, .002f, 0f, 0f)));
            Assert.That(
                material.GetVector("_WaterVelocityB"),
                Is.EqualTo(new Vector4(-.003f, .005f, 0f, 0f)));
            Assert.That(
                Vector2.Dot(
                    new Vector2(.006f, .002f).normalized,
                    new Vector2(-.003f, .005f).normalized),
                Is.Not.EqualTo(1f).Within(.0001f));
            Assert.That(
                Regex.Matches(
                    source,
                    @"SAMPLE_TEXTURE2D_ARRAY\(\s*_NormalArray,"),
                Has.Count.EqualTo(2));
            Assert.That(
                source,
                Does.Contain(
                    "min(saturate(_WaterHighlightStrength), 0.22)"));
            Assert.That(
                source,
                Does.Contain(
                    "clamp(_DeepWaterNormalStrength, 1.0, 1.6)"));
            int deepWaterBranch = source.IndexOf(
                "if (layerIndex == 5u)",
                source.IndexOf("half3 combinedNormalTS", StringComparison.Ordinal),
                StringComparison.Ordinal);
            int strengthUse = source.IndexOf(
                "clamp(_DeepWaterNormalStrength, 1.0, 1.6)",
                StringComparison.Ordinal);
            int branchClose = source.IndexOf(
                "sample.normalTS = ApplyDetailMaskToNormal",
                deepWaterBranch,
                StringComparison.Ordinal);
            Assert.That(strengthUse, Is.GreaterThan(deepWaterBranch));
            Assert.That(strengthUse, Is.LessThan(branchClose));
            Assert.That(
                CountOccurrences(source, "_DeepWaterNormalStrength"),
                Is.EqualTo(3),
                "property, CBUFFER and the DeepWater clamp are the only allowed uses");
            Assert.That(source, Does.Not.Contain("surfaceData.emission ="));
            Assert.That(source, Does.Not.Contain("\"Queue\" = \"Transparent\""));
            Assert.That(source, Does.Not.Contain("new MeshRenderer"));
        }

        [Test]
        public void BuildRuntimeAssets_CreatesValidMaterialAndProfileInPlace()
        {
            FirstArtTerrainAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> firstStates = CaptureRuntimeAssetStates();
            Material firstMaterial = LoadRequired<Material>(FirstArtTerrainAssetBuilder.MaterialPath);
            FirstArtTerrainProfile3D firstProfile = LoadRequired<FirstArtTerrainProfile3D>(
                FirstArtTerrainAssetBuilder.ProfilePath);
            AssertRuntimeContract(firstMaterial, firstProfile);
            string firstMaterialPath = AssetDatabase.GetAssetPath(firstMaterial);
            string firstProfilePath = AssetDatabase.GetAssetPath(firstProfile);

            FirstArtTerrainAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> secondStates = CaptureRuntimeAssetStates();
            Material secondMaterial = LoadRequired<Material>(FirstArtTerrainAssetBuilder.MaterialPath);
            FirstArtTerrainProfile3D secondProfile = LoadRequired<FirstArtTerrainProfile3D>(
                FirstArtTerrainAssetBuilder.ProfilePath);

            AssertRuntimeContract(secondMaterial, secondProfile);
            Assert.That(
                AssetDatabase.GetAssetPath(secondMaterial),
                Is.EqualTo(firstMaterialPath));
            Assert.That(
                AssetDatabase.GetAssetPath(secondProfile),
                Is.EqualTo(firstProfilePath));
            Assert.That(secondStates, Is.EqualTo(firstStates));
        }

        [Test]
        public void Profile_RejectsMaterialUsingUrpLitWithPreciseShaderNameError()
        {
            Shader wrongShader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(wrongShader, Is.Not.Null);
            var wrongMaterial = new Material(wrongShader);
            FirstArtTerrainProfile3D profile = ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();

            try
            {
                profile.Configure(
                    wrongMaterial,
                    LoadRequired<Texture2DArray>(FirstArtTerrainAssetBuilder.BaseColorArrayPath),
                    LoadRequired<Texture2DArray>(FirstArtTerrainAssetBuilder.NormalArrayPath),
                    LoadRequired<Texture2DArray>(FirstArtTerrainAssetBuilder.MaskArrayPath),
                    LoadRequired<Texture2DArray>(FirstArtTerrainAssetBuilder.HeightArrayPath));

                Assert.That(profile.TryValidate(out string error), Is.False);
                Assert.That(
                    error,
                    Is.EqualTo(
                        "Material must use shader " +
                        FirstArtTerrainProfile3D.RequiredShaderName +
                        "."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(wrongMaterial);
            }
        }

        private static void AssertRuntimeContract(
            Material material,
            FirstArtTerrainProfile3D profile)
        {
            Shader shader = LoadRequired<Shader>(FirstArtTerrainAssetBuilder.ShaderPath);
            Texture2DArray baseColor = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.BaseColorArrayPath);
            Texture2DArray normal = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.NormalArrayPath);
            Texture2DArray mask = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.MaskArrayPath);
            Texture2DArray height = LoadRequired<Texture2DArray>(
                FirstArtTerrainAssetBuilder.HeightArrayPath);

            Assert.That(profile.TryValidate(out string error), Is.True, error);
            AssertSameAsset(material.shader, shader, "Material Shader");
            Assert.That(material.shader.name, Is.EqualTo(FirstArtTerrainProfile3D.RequiredShaderName));
            AssertSameAsset(profile.Material, material, "Profile Material");
            AssertSameAsset(profile.BaseColorArray, baseColor, "Profile BaseColor");
            AssertSameAsset(profile.NormalArray, normal, "Profile Normal");
            AssertSameAsset(profile.MaskArray, mask, "Profile Mask");
            AssertSameAsset(profile.HeightArray, height, "Profile Height");
            AssertSameAsset(material.GetTexture("_BaseColorArray"), baseColor, "Material BaseColor");
            AssertSameAsset(material.GetTexture("_NormalArray"), normal, "Material Normal");
            AssertSameAsset(material.GetTexture("_MaskArray"), mask, "Material Mask");
            AssertSameAsset(material.GetTexture("_HeightArray"), height, "Material Height");
        }

        private static void AssertSameAsset(
            UnityEngine.Object actual,
            UnityEngine.Object expected,
            string label)
        {
            Assert.That(actual, Is.Not.Null, label);
            string expectedPath = AssetDatabase.GetAssetPath(expected);
            string actualPath = AssetDatabase.GetAssetPath(actual);
            Assert.That(actualPath, Is.EqualTo(expectedPath), label);
            Assert.That(
                AssetDatabase.AssetPathToGUID(actualPath),
                Is.EqualTo(AssetDatabase.AssetPathToGUID(expectedPath)),
                label);
        }

        private static Dictionary<string, AssetState> CaptureRuntimeAssetStates()
        {
            string[] paths =
            {
                FirstArtTerrainAssetBuilder.MaterialPath,
                FirstArtTerrainAssetBuilder.ProfilePath,
                FirstArtTerrainAssetBuilder.ShaderPath,
            };
            var states = new Dictionary<string, AssetState>(StringComparer.Ordinal);
            foreach (string assetPath in paths)
            {
                states.Add(
                    assetPath,
                    new AssetState(
                        AssetDatabase.AssetPathToGUID(assetPath),
                        AssetDatabase.GetAssetDependencyHash(assetPath)));
            }

            return states;
        }

        private static string LoadShaderSource()
        {
            return File.ReadAllText(FirstArtTerrainAssetBuilder.ShaderPath);
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private static Vector3 ApplyDetailMaskReference(Vector3 decodedNormal, float detailMask)
        {
            Vector3 gated = Vector3.Lerp(Vector3.forward, decodedNormal, Mathf.Clamp01(detailMask));
            return gated.sqrMagnitude <= 0.000001f || !IsFinite(gated)
                ? Vector3.forward
                : gated.normalized;
        }

        private static Color GradeLayerReference(Color source, Color tint)
        {
            float luminance =
                source.r * .2126f + source.g * .7152f + source.b * .0722f;
            float scale = Mathf.Lerp(.65f, 1.35f, Mathf.Clamp01(luminance));
            var graded = new Color(
                tint.r * scale,
                tint.g * scale,
                tint.b * scale,
                source.a);
            Color result = Color.Lerp(source, graded, Mathf.Clamp01(tint.a));
            result.a = source.a;
            return result;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void AssertVectorWithin(Vector3 actual, Vector3 expected, float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
        }

        private static T LoadRequired<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            return asset;
        }

        private static void AssertTextureProperty(
            Shader shader,
            string propertyName,
            TextureDimension expectedDimension)
        {
            int index = shader.FindPropertyIndex(propertyName);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), propertyName);
            Assert.That(shader.GetPropertyType(index), Is.EqualTo(ShaderPropertyType.Texture), propertyName);
            Assert.That(
                shader.GetPropertyTextureDimension(index),
                Is.EqualTo(expectedDimension),
                propertyName);
        }

        private static void AssertVectorProperty(
            Shader shader,
            string propertyName,
            Vector4 expectedDefault)
        {
            int index = shader.FindPropertyIndex(propertyName);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), propertyName);
            Assert.That(shader.GetPropertyType(index), Is.EqualTo(ShaderPropertyType.Vector), propertyName);
            Assert.That(shader.GetPropertyDefaultVectorValue(index), Is.EqualTo(expectedDefault), propertyName);
        }

        private static void AssertFloatProperty(
            Shader shader,
            string propertyName,
            float expectedDefault)
        {
            int index = shader.FindPropertyIndex(propertyName);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), propertyName);
            Assert.That(shader.GetPropertyType(index), Is.EqualTo(ShaderPropertyType.Float), propertyName);
            Assert.That(shader.GetPropertyDefaultFloatValue(index), Is.EqualTo(expectedDefault), propertyName);
        }

        private static void AssertColorProperty(
            Shader shader,
            string propertyName,
            Color expectedDefault)
        {
            int index = shader.FindPropertyIndex(propertyName);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), propertyName);
            Assert.That(shader.GetPropertyType(index), Is.EqualTo(ShaderPropertyType.Color), propertyName);
            Assert.That(
                shader.GetPropertyDefaultVectorValue(index),
                Is.EqualTo(new Vector4(
                    expectedDefault.r,
                    expectedDefault.g,
                    expectedDefault.b,
                    expectedDefault.a)),
                propertyName);
        }

        private readonly struct AssetState : IEquatable<AssetState>
        {
            public AssetState(string guid, Hash128 dependencyHash)
            {
                Guid = guid;
                DependencyHash = dependencyHash;
            }

            private string Guid { get; }

            private Hash128 DependencyHash { get; }

            public bool Equals(AssetState other)
            {
                return string.Equals(Guid, other.Guid, StringComparison.Ordinal) &&
                       DependencyHash == other.DependencyHash;
            }

            public override bool Equals(object obj)
            {
                return obj is AssetState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Guid != null ? Guid.GetHashCode() : 0) * 397) ^
                           DependencyHash.GetHashCode();
                }
            }
        }
    }
}
