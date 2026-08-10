using System;
using System.Collections.Generic;
using System.Linq;
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
            AssertFloatProperty(shader, "_MacroVariation", 0.05f);
            AssertVectorProperty(shader, "_WaterVelocityA", new Vector4(0.006f, 0.002f, 0f, 0f));
            AssertVectorProperty(shader, "_WaterVelocityB", new Vector4(-0.003f, 0.005f, 0f, 0f));
            AssertFloatProperty(shader, "_WaterNormalScaleB", 1.17f);
            AssertFloatProperty(shader, "_WaterHighlightStrength", 0.12f);
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
