using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainProfileTests
    {
        [TestCase(WorldTraversalKind.Open, TerrainKind.Wasteland, FirstArtTerrainLayer3D.Wasteland)]
        [TestCase(WorldTraversalKind.Open, TerrainKind.Rocky, FirstArtTerrainLayer3D.Rocky)]
        [TestCase(WorldTraversalKind.Open, TerrainKind.Wetland, FirstArtTerrainLayer3D.Wetland)]
        [TestCase(WorldTraversalKind.Open, TerrainKind.Crystal, FirstArtTerrainLayer3D.Crystal)]
        [TestCase(WorldTraversalKind.Ruins, TerrainKind.Crystal, FirstArtTerrainLayer3D.Ruins)]
        [TestCase(WorldTraversalKind.DeepWater, TerrainKind.Rocky, FirstArtTerrainLayer3D.DeepWater)]
        [TestCase(WorldTraversalKind.Cliff, TerrainKind.Wetland, FirstArtTerrainLayer3D.Cliff)]
        public void LayerOf_UsesTraversalAsVisualOverride(
            WorldTraversalKind traversal,
            TerrainKind terrain,
            FirstArtTerrainLayer3D expected)
        {
            var cell = new WorldCell(terrain, null, 0, traversal);

            Assert.That(FirstArtTerrainCatalog3D.LayerOf(cell), Is.EqualTo(expected));
        }

        [Test]
        public void Catalog_HasFrozenSevenLayerOrderAndStableIds()
        {
            Assert.That(FirstArtTerrainCatalog3D.LayerCount, Is.EqualTo(7));
            Assert.That((int)FirstArtTerrainLayer3D.Wasteland, Is.Zero);
            Assert.That((int)FirstArtTerrainLayer3D.Cliff, Is.EqualTo(6));
            Assert.That(
                FirstArtTerrainCatalog3D.StableIdOf(FirstArtTerrainLayer3D.DeepWater),
                Is.EqualTo("world.obstacle.deep-water"));
            Assert.That(
                FirstArtTerrainCatalog3D.IsSurfaceStableId("world.terrain.wasteland"),
                Is.True);
            Assert.That(
                FirstArtTerrainCatalog3D.IsSurfaceStableId("world.obstacle.cliff"),
                Is.False);
        }

        [Test]
        public void Catalog_RejectsInvalidLayersAndCellEnums()
        {
            Assert.That(
                () => FirstArtTerrainCatalog3D.StableIdOf((FirstArtTerrainLayer3D)7),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => FirstArtTerrainCatalog3D.LayerOf(new WorldCell((TerrainKind)4, null, 0)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => FirstArtTerrainCatalog3D.LayerOf(
                    new WorldCell(TerrainKind.Wasteland, null, 0, (WorldTraversalKind)4)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Profile_UsesFrozenControlAndBlendDefaults()
        {
            FirstArtTerrainProfile3D profile =
                ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();

            try
            {
                Assert.That(
                    profile.ControlPixelsPerCell,
                    Is.EqualTo(FirstArtTerrainProfile3D.DefaultControlPixelsPerCell));
                Assert.That(profile.ControlPixelsPerCell, Is.EqualTo(4));
                Assert.That(
                    profile.CellsPerTexture,
                    Is.EqualTo(FirstArtTerrainProfile3D.DefaultCellsPerTexture));
                Assert.That(profile.CellsPerTexture, Is.EqualTo(4f));
                Assert.That(profile.HeightBlendStrength, Is.GreaterThanOrEqualTo(0f));
                Assert.That(
                    Vector2.Dot(
                        profile.WaterNormalVelocityA.normalized,
                        profile.WaterNormalVelocityB.normalized),
                    Is.LessThan(0.999f));
                Assert.That(
                    profile.BlendWidth(FirstArtTerrainLayer3D.Wasteland, FirstArtTerrainLayer3D.Rocky),
                    Is.EqualTo(1.25f));
                Assert.That(
                    profile.BlendWidth(FirstArtTerrainLayer3D.Wasteland, FirstArtTerrainLayer3D.Wetland),
                    Is.EqualTo(1.15f));
                Assert.That(
                    profile.BlendWidth(FirstArtTerrainLayer3D.Wasteland, FirstArtTerrainLayer3D.Crystal),
                    Is.EqualTo(1f));
                Assert.That(
                    profile.BlendWidth(FirstArtTerrainLayer3D.Rocky, FirstArtTerrainLayer3D.Wetland),
                    Is.EqualTo(1.15f));
                Assert.That(
                    profile.BlendWidth(FirstArtTerrainLayer3D.Ruins, FirstArtTerrainLayer3D.DeepWater),
                    Is.EqualTo(0.425f));
                Assert.That(
                    profile.BlendWidth(FirstArtTerrainLayer3D.Cliff, FirstArtTerrainLayer3D.Wetland),
                    Is.EqualTo(0.35f));
                Assert.That(
                    profile.BlendWidth(FirstArtTerrainLayer3D.Crystal, FirstArtTerrainLayer3D.Crystal),
                    Is.Zero);
                Assert.That(
                    profile.TryValidateControlSettings(out string error),
                    Is.True,
                    error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Profile_RejectsMissingArraysWrongDepthAndWrongShader()
        {
            FirstArtTerrainProfile3D profile =
                ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();

            try
            {
                Assert.That(profile.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("Material"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Profile_AllowsSmallOrthogonalWaterVelocities()
        {
            FirstArtTerrainProfile3D profile =
                ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();

            try
            {
                SetWaterNormalVelocities(
                    profile,
                    new Vector2(0.00001f, 0f),
                    new Vector2(0f, 0.00001f));

                Assert.That(profile.TryValidateControlSettings(out string error), Is.True, error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Profile_RejectsAntiparallelWaterVelocities()
        {
            FirstArtTerrainProfile3D profile =
                ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();

            try
            {
                SetWaterNormalVelocities(profile, Vector2.right, Vector2.left);

                Assert.That(profile.TryValidateControlSettings(out string error), Is.False);
                Assert.That(error, Does.Contain("non-parallel"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Profile_RejectsNonFiniteWaterVelocities()
        {
            FirstArtTerrainProfile3D profile =
                ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();

            try
            {
                SetWaterNormalVelocities(profile, new Vector2(float.NaN, 1f), Vector2.up);

                Assert.That(profile.TryValidateControlSettings(out string error), Is.False);
                Assert.That(error, Does.Contain("non-zero and non-parallel"));

                SetWaterNormalVelocities(
                    profile,
                    new Vector2(float.PositiveInfinity, 1f),
                    Vector2.up);

                Assert.That(profile.TryValidateControlSettings(out error), Is.False);
                Assert.That(error, Does.Contain("non-zero and non-parallel"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static void SetWaterNormalVelocities(
            FirstArtTerrainProfile3D profile,
            Vector2 velocityA,
            Vector2 velocityB)
        {
            SetPrivateField(profile, "waterNormalVelocityA", velocityA);
            SetPrivateField(profile, "waterNormalVelocityB", velocityB);
        }

        private static void SetPrivateField(
            FirstArtTerrainProfile3D profile,
            string fieldName,
            Vector2 value)
        {
            FieldInfo field = typeof(FirstArtTerrainProfile3D).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(profile, value);
        }
    }
}
