using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WasteCity.ArtIntegration3D;
using WasteCity.Editor;

namespace WasteCity.Tests
{
    public sealed class FirstArtRuinsCliffAssetBuilderTests
    {
        private const string ShaderPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassGeometry.shader";
        private const string ProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset";
        private const string MaterialDirectory =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/";
        private const string GrayboxScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";

        private static readonly string[] ArrayPaths =
        {
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset",
        };

        private static readonly string[] ArrayProperties =
        {
            "_BaseColorArray",
            "_NormalArray",
            "_MaskArray",
            "_HeightArray",
        };

        [TearDown]
        public void RestoreBuilderAndPipelineState()
        {
            FirstArtRuinsCliffAssetBuilder.PublishCheckpoint = null;
            ClearBuilderActionField("BeforeCommitCheckpoint");
            ClearBuilderActionField("AfterCommitCleanupCheckpoint");
            FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
            GrayboxRenderPipelineBuildScope.RestoreAfterBuild();
        }

        [Test]
        public void BuilderAndGeneratedAssets_ExistAndProfileValidates()
        {
            Type builderType = Type.GetType(
                "WasteCity.Editor.FirstArtRuinsCliffAssetBuilder, WasteCity.Editor",
                false);
            Assert.That(builderType, Is.Not.Null, "IDEA-0004 geometry asset builder is missing.");
            Assert.That(
                builderType.GetMethod("BuildRuntimeAssets"),
                Is.Not.Null,
                "The approved public batch-mode entry is missing.");

            FirstArtRuinsCliffProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(ProfilePath);
            Assert.That(profile, Is.Not.Null, "Generated geometry profile is missing.");
            Assert.That(profile.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void GeometryShader_CompilesAndFreezesApprovedContract()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null, "Geometry shader asset is missing.");
            Assert.That(shader.name, Is.EqualTo(FirstArtRuinsCliffCatalog3D.RequiredShaderName));
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                foreach (string property in ArrayProperties)
                    Assert.That(material.HasProperty(property), Is.True, property);
                Assert.That(material.HasProperty("_LayerIndex"), Is.True);
                Assert.That(material.HasProperty("_TriplanarScale"), Is.True);
                Assert.That(material.HasProperty("_RoleTint"), Is.True);
                Assert.That(material.HasProperty("_RoleTintStrength"), Is.True);
                Assert.That(material.HasProperty("_MetallicScale"), Is.True);
                Assert.That(material.HasProperty("_SmoothnessScale"), Is.True);
                Assert.That(material.HasProperty("_OcclusionStrength"), Is.True);
                Assert.That(material.HasProperty("_NormalStrength"), Is.True);
                Assert.That(material.HasProperty("_HeightStrength"), Is.True);
                Assert.That(material.FindPass("UniversalForward"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            string source = File.ReadAllText(ProjectPath(ShaderPath));
            Assert.That(source, Does.Contain("UniversalFragmentPBR"));
            Assert.That(source, Does.Contain("SampleTriplanar"));
            Assert.That(source, Does.Contain("RedirectNormalX"));
            Assert.That(source, Does.Contain("RedirectNormalY"));
            Assert.That(source, Does.Contain("RedirectNormalZ"));
            Assert.That(source, Does.Contain("_CASTING_PUNCTUAL_LIGHT_SHADOW"));
            Assert.That(source, Does.Contain("float _OcclusionStrength;"));
            Assert.That(source, Does.Contain("surfaceData.occlusion = lerp("));
            foreach (string property in ArrayProperties)
                Assert.That(source, Does.Contain("SAMPLE_TEXTURE2D_ARRAY(" + property));
        }

        [Test]
        public void SharedMaterials_UseExistingArraysAndApprovedLayers()
        {
            var seenSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (FirstArtRuinsCliffMaterialRole3D role in
                     FirstArtRuinsCliffCatalog3D.MaterialRoles)
            {
                string path = MaterialDirectory + role.Name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.name, Is.EqualTo(role.Name));
                Assert.That(material.shader.name,
                    Is.EqualTo(FirstArtRuinsCliffCatalog3D.RequiredShaderName));
                Assert.That(
                    material.GetFloat("_LayerIndex"),
                    Is.EqualTo(role.Family == FirstArtRuinsCliffFamily3D.Ruins ? 4f : 6f));
                Assert.That(
                    material.GetFloat("_OcclusionStrength"),
                    Is.EqualTo(role.Family == FirstArtRuinsCliffFamily3D.Ruins ? 0.30f : 1.00f),
                    role.Name + " approved module-generator AO strength");
                for (int arrayIndex = 0; arrayIndex < ArrayProperties.Length; arrayIndex++)
                {
                    string property = ArrayProperties[arrayIndex];
                    Texture texture = material.GetTexture(property);
                    Assert.That(texture, Is.TypeOf<Texture2DArray>(), role.Name + ":" + property);
                    string texturePath = AssetDatabase.GetAssetPath(texture);
                    Assert.That(texturePath, Is.EqualTo(ArrayPaths[arrayIndex]));
                    Assert.That(((Texture2DArray)texture).depth,
                        Is.EqualTo(FirstArtTerrainCatalog3D.LayerCount));
                }

                Color tint = material.GetColor("_RoleTint");
                string signature = string.Join(
                    ":",
                    tint.r.ToString("R"), tint.g.ToString("R"),
                    tint.b.ToString("R"), tint.a.ToString("R"),
                    material.GetFloat("_MetallicScale").ToString("R"),
                    material.GetFloat("_SmoothnessScale").ToString("R"),
                    material.GetFloat("_OcclusionStrength").ToString("R"),
                    material.GetFloat("_NormalStrength").ToString("R"));
                Assert.That(seenSignatures.Add(signature), Is.True,
                    "Every approved material role must remain visually distinct: " + role.Name);
            }
        }

        [Test]
        public void Prefabs_AreCalibratedStaticMeshOnlyAssets()
        {
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in
                     FirstArtRuinsCliffCatalog3D.Entries)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
                Assert.That(prefab, Is.Not.Null, entry.PrefabPath);
                Assert.That(prefab.transform.childCount, Is.EqualTo(0), entry.StableId);
                Assert.That(prefab.GetComponents<Component>().Select(component => component.GetType()),
                    Is.EquivalentTo(new[]
                    {
                        typeof(Transform),
                        typeof(MeshFilter),
                        typeof(MeshRenderer),
                    }), entry.StableId);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty, entry.StableId);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty, entry.StableId);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, entry.StableId);

                MeshFilter filter = prefab.GetComponent<MeshFilter>();
                MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
                Assert.That(filter.sharedMesh, Is.Not.Null, entry.StableId);
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.EqualTo(entry.FbxPath));
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(entry.MaterialRoles.Count));
                Assert.That(filter.sharedMesh.subMeshCount, Is.EqualTo(entry.MaterialRoles.Count));
                for (int index = 0; index < entry.MaterialRoles.Count; index++)
                {
                    Material material = renderer.sharedMaterials[index];
                    Assert.That(material, Is.Not.Null, entry.StableId + ":" + index);
                    Assert.That(material.name, Is.EqualTo(entry.MaterialRoles[index]));
                    Assert.That(AssetDatabase.GetAssetPath(material),
                        Is.EqualTo(MaterialDirectory + entry.MaterialRoles[index] + ".mat"));
                }

                AssertVector(prefab.transform.localPosition, entry.ChildOffset, entry.StableId + " offset");
                Matrix4x4 expectedMatrix = CalibrationMatrix(entry);
                AssertMatrix(
                    prefab.transform.localToWorldMatrix,
                    expectedMatrix,
                    entry.StableId + " full calibration matrix",
                    0.0000002f);
                Bounds calibrated = TransformBounds(filter.sharedMesh.bounds, prefab.transform.localToWorldMatrix);
                Assert.That(calibrated.center.x, Is.EqualTo(0f).Within(0.0000002f), entry.StableId);
                Assert.That(calibrated.center.z, Is.EqualTo(0f).Within(0.0000002f), entry.StableId);
                Assert.That(calibrated.min.y, Is.EqualTo(0f).Within(0.0000002f), entry.StableId);
                Assert.That(calibrated.size.x, Is.LessThanOrEqualTo(0.9002f), entry.StableId);
                Assert.That(calibrated.size.z, Is.LessThanOrEqualTo(0.9002f), entry.StableId);
                AssertVector(calibrated.size, entry.CalibratedBounds, entry.StableId + " bounds", 0.0000002f);
            }
        }

        [Test]
        public void ImportedFbxRoots_MatchOneSharedImportMatrixSlotsAndDerivedCalibration()
        {
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in FirstArtRuinsCliffCatalog3D.Entries)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.FbxPath);
                Assert.That(model, Is.Not.Null, entry.FbxPath);
                Assert.That(model.transform.childCount, Is.Zero, entry.StableId);
                AssertVector(model.transform.localPosition, Vector3.zero, entry.StableId + " import position");
                AssertVector(model.transform.localScale, Vector3.one, entry.StableId + " import scale");
                AssertQuaternion(model.transform.localRotation,
                    FirstArtRuinsCliffCatalog3D.SourceImportRotation,
                    entry.StableId + " import rotation", 0.0000002f);
                AssertMatrix(model.transform.localToWorldMatrix,
                    FirstArtRuinsCliffCatalog3D.SourceImportMatrix,
                    entry.StableId + " shared import matrix", 0.0000002f);
                AssertMatrix(entry.SourceImportMatrix,
                    FirstArtRuinsCliffCatalog3D.SourceImportMatrix,
                    entry.StableId + " catalog import matrix", 0f);

                Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(entry.FbxPath)
                    .OfType<Mesh>().ToArray();
                Assert.That(meshes.Length, Is.EqualTo(1), entry.StableId);
                MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                Assert.That(filters.Length, Is.EqualTo(1), entry.StableId);
                Assert.That(renderers.Length, Is.EqualTo(1), entry.StableId);
                Assert.That(filters[0].sharedMesh, Is.SameAs(meshes[0]), entry.StableId);
                Assert.That(renderers[0].sharedMaterials.Select(material => material.name),
                    Is.EqualTo(entry.MaterialRoles), entry.StableId + " imported slot order");

                Vector3 derivedOffset = DeriveChildOffset(
                    meshes[0].bounds,
                    FirstArtRuinsCliffCatalog3D.SourceImportMatrix,
                    entry.RootScale);
                AssertVector(derivedOffset, entry.ChildOffset,
                    entry.StableId + " derived offset", 0.0000002f);
                Bounds finalBounds = TransformBounds(meshes[0].bounds, CalibrationMatrix(entry));
                Assert.That(finalBounds.center.x, Is.EqualTo(0f).Within(0.0000002f), entry.StableId);
                Assert.That(finalBounds.center.z, Is.EqualTo(0f).Within(0.0000002f), entry.StableId);
                Assert.That(finalBounds.min.y, Is.EqualTo(0f).Within(0.0000002f), entry.StableId);
                AssertVector(finalBounds.size, entry.CalibratedBounds,
                    entry.StableId + " final size", 0.0000002f);
            }
        }

        [Test]
        public void PlacementContract_AppliesCatalogCalibrationOnceAndIgnoresPrefabTransform()
        {
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in FirstArtRuinsCliffCatalog3D.Entries)
            {
                Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(entry.FbxPath).OfType<Mesh>().Single();
                Matrix4x4 placement = CalibrationMatrix(entry);
                Bounds once = TransformBounds(mesh.bounds, placement);
                AssertVector(once.size, entry.CalibratedBounds,
                    entry.StableId + " single calibration", 0.0000002f);

                Bounds twice = TransformBounds(mesh.bounds, placement * placement);
                float maximumDifference = Mathf.Max(
                    Mathf.Abs(twice.size.x - entry.CalibratedBounds.x),
                    Mathf.Abs(twice.size.y - entry.CalibratedBounds.y),
                    Mathf.Abs(twice.size.z - entry.CalibratedBounds.z));
                Assert.That(maximumDifference, Is.GreaterThan(0.001f),
                    entry.StableId + " would hide a double-calibration bug; Task 5 must read only Mesh/materials from the Prefab.");
            }
        }

        [Test]
        public void Builder_TwoConsecutiveRunsPreserveAssetMetaBytesAndGuids()
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> first = CaptureOutputStates();
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> second = CaptureOutputStates();
            AssertStates(second, first, "second idempotent run");
            Assert.That(Directory.Exists(ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot)), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(FirstArtRuinsCliffAssetBuilder.StagingRoot), Is.False);
        }

        [TestCase((int)FirstArtRuinsCliffAssetBuilder.PublishPhase.Material)]
        [TestCase((int)FirstArtRuinsCliffAssetBuilder.PublishPhase.Prefab)]
        [TestCase((int)FirstArtRuinsCliffAssetBuilder.PublishPhase.Profile)]
        [TestCase((int)FirstArtRuinsCliffAssetBuilder.PublishPhase.Save)]
        [TestCase((int)FirstArtRuinsCliffAssetBuilder.PublishPhase.Reimport)]
        [TestCase((int)FirstArtRuinsCliffAssetBuilder.PublishPhase.BeforeFinalValidation)]
        [TestCase((int)FirstArtRuinsCliffAssetBuilder.PublishPhase.AfterFinalValidation)]
        public void Builder_InjectedFailureAtEveryPublishPhaseRollsBackExactBytesAndGuids(
            int targetPhaseValue)
        {
            FirstArtRuinsCliffAssetBuilder.PublishPhase targetPhase =
                (FirstArtRuinsCliffAssetBuilder.PublishPhase)targetPhaseValue;
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            CorruptOneMaterial();
            Dictionary<string, AssetState> expected = CaptureOutputStates();
            try
            {
                FirstArtRuinsCliffAssetBuilder.PublishCheckpoint = (phase, ordinal, path) =>
                {
                    if (phase == targetPhase && ordinal == 1)
                        throw new InjectedPublishFailure(targetPhase + ":" + path);
                };
                Assert.Throws<InjectedPublishFailure>(
                    FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets,
                    targetPhase.ToString());
                AssertStates(CaptureOutputStates(), expected, targetPhase + " rollback");
                Assert.That(Directory.Exists(ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot)), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(FirstArtRuinsCliffAssetBuilder.StagingRoot), Is.False);
            }
            finally
            {
                FirstArtRuinsCliffAssetBuilder.PublishCheckpoint = null;
                FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            }
        }

        [Test]
        public void Builder_FirstMissingDestinationIsRemovedAgainWhenPublicationFails()
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> original = CaptureOutputStates();
            try
            {
                Assert.That(AssetDatabase.DeleteAsset(ProfilePath), Is.True);
                Dictionary<string, AssetState> expectedMissing = CaptureOutputStates();
                Assert.That(expectedMissing[ProfilePath].Exists, Is.False);
                FirstArtRuinsCliffAssetBuilder.PublishCheckpoint = (phase, ordinal, path) =>
                {
                    if (phase == FirstArtRuinsCliffAssetBuilder.PublishPhase.Reimport && ordinal == 1)
                        throw new InjectedPublishFailure("missing destination rollback");
                };
                Assert.Throws<InjectedPublishFailure>(FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets);
                AssertStates(CaptureOutputStates(), expectedMissing, "first-missing rollback");
                Assert.That(File.Exists(ProjectPath(ProfilePath)), Is.False);
                Assert.That(File.Exists(ProjectPath(ProfilePath) + ".meta"), Is.False);
            }
            finally
            {
                FirstArtRuinsCliffAssetBuilder.PublishCheckpoint = null;
                FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                RestoreStates(original);
            }
        }

        [Test]
        public void Builder_AbandonedMarkerRestoresExactBytesAndGuidsOnRecovery()
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> expected = CaptureOutputStates();
            FirstArtRuinsCliffAssetBuilder.LeaveRecoveryMarkerForTests();
            Assert.That(Directory.Exists(ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot)), Is.True);
            CorruptOneMaterial();

            FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();

            AssertStates(CaptureOutputStates(), expected, "abandoned-marker recovery");
            Assert.That(Directory.Exists(ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot)), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(FirstArtRuinsCliffAssetBuilder.StagingRoot), Is.False);
        }

        [Test]
        public void Builder_RecoveryDeletesOrphanedRepositoryStagingWithoutMarker()
        {
            Assert.That(Directory.Exists(ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot)), Is.False);
            try
            {
                EnsureAssetFolderForTest(FirstArtRuinsCliffAssetBuilder.StagingRoot);
                File.WriteAllText(
                    ProjectPath(FirstArtRuinsCliffAssetBuilder.StagingRoot + "/domain-reload.tmp"),
                    "simulated interrupted staging");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.IsValidFolder(FirstArtRuinsCliffAssetBuilder.StagingRoot), Is.True);

                FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();

                Assert.That(AssetDatabase.IsValidFolder(FirstArtRuinsCliffAssetBuilder.StagingRoot), Is.False,
                    "Initialization recovery must remove uncommitted staging even when a crash preceded marker publication.");
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(FirstArtRuinsCliffAssetBuilder.StagingRoot))
                    AssetDatabase.DeleteAsset(FirstArtRuinsCliffAssetBuilder.StagingRoot);
            }
        }

        [Test]
        public void Builder_SourceOrdersRecoverableTransactionBeforeStagingAndPublishesMarkerAtomically()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Editor/FirstArtRuinsCliffAssetBuilder.cs"));
            int buildStart = source.IndexOf("public static void BuildRuntimeAssets()", StringComparison.Ordinal);
            int transactionBegin = source.IndexOf(
                "transaction = AssetPublishTransaction.Begin", buildStart, StringComparison.Ordinal);
            int stagingCreation = source.IndexOf("EnsureFolder(StagingRoot)", buildStart, StringComparison.Ordinal);
            Assert.That(buildStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(transactionBegin, Is.GreaterThan(buildStart));
            Assert.That(stagingCreation, Is.GreaterThan(transactionBegin),
                "A recoverable marker must exist before repository staging is created.");
            Assert.That(source, Does.Contain("RestoreMarkerTemporaryPath"));
            Assert.That(source, Does.Contain("File.Move(temporaryMarker, marker)"));
            Assert.That(source, Does.Not.Contain("File.WriteAllText(marker, JsonUtility.ToJson"),
                "The final recovery marker must never be directly truncated or overwritten.");
        }

        [Test]
        public void Builder_CorruptedBackupFailsClosedAndPreservesRecoveryEvidence()
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> original = CaptureOutputStates();
            string restoreDirectory = ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot);
            string firstBackup = Path.Combine(restoreDirectory, "000.asset.bytes");
            byte[] validBackup = null;
            try
            {
                FirstArtRuinsCliffAssetBuilder.LeaveRecoveryMarkerForTests();
                validBackup = File.ReadAllBytes(firstBackup);
                File.Copy(
                    ProjectPath(MaterialDirectory + "MAT_Ruins_Aggregate.mat"),
                    firstBackup,
                    true);

                Assert.Throws<InvalidOperationException>(
                    FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild,
                    "A same-GUID but byte-corrupted restore backup must not be accepted.");
                Assert.That(Directory.Exists(restoreDirectory), Is.True,
                    "Failed verification must preserve marker and backups for the next recovery attempt.");
                Assert.That(File.Exists(Path.Combine(restoreDirectory, "transaction.json")), Is.True);
                Assert.That(File.Exists(firstBackup), Is.True);
            }
            finally
            {
                if (Directory.Exists(restoreDirectory) && validBackup != null)
                {
                    File.WriteAllBytes(firstBackup, validBackup);
                    FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                }
                else
                {
                    RestoreStates(original);
                }
            }
            AssertStates(CaptureOutputStates(), original, "verified recovery cleanup");
        }

        [Test]
        public void Builder_TruncatedFinalMarkerIsRejectedWithoutDeletingRecoveryEvidence()
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> original = CaptureOutputStates();
            string restoreDirectory = ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot);
            string marker = Path.Combine(restoreDirectory, "transaction.json");
            byte[] validMarker = null;
            try
            {
                FirstArtRuinsCliffAssetBuilder.LeaveRecoveryMarkerForTests();
                validMarker = File.ReadAllBytes(marker);
                File.WriteAllText(marker, "{\"version\":");

                Assert.Throws<InvalidOperationException>(
                    FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild,
                    "A truncated final marker must never be accepted as an empty or successful transaction.");
                Assert.That(Directory.Exists(restoreDirectory), Is.True);
                Assert.That(File.Exists(marker), Is.True);
                Assert.That(File.Exists(Path.Combine(restoreDirectory, "000.asset.bytes")), Is.True);
            }
            finally
            {
                if (Directory.Exists(restoreDirectory) && validMarker != null)
                {
                    File.WriteAllBytes(marker, validMarker);
                    FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                }
                else
                {
                    RestoreStates(original);
                }
            }
            AssertStates(CaptureOutputStates(), original, "truncated-marker recovery retry");
        }

        [TestCase(0, TestName = "Builder_ManifestRejectsEmptyEntries")]
        [TestCase(1, TestName = "Builder_ManifestRejectsMissingEntry")]
        [TestCase(2, TestName = "Builder_ManifestRejectsDuplicateEntry")]
        [TestCase(3, TestName = "Builder_ManifestRejectsExtraEntry")]
        [TestCase(4, TestName = "Builder_ManifestRejectsTraversalAssetPath")]
        [TestCase(5, TestName = "Builder_ManifestRejectsUnauthorizedDirectory")]
        public void Builder_ManifestAllowlistRejectsLegalJsonAttacksBeforeTouchingTargets(int attack)
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> original = CaptureOutputStates();
            string restoreDirectory = ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot);
            string marker = Path.Combine(restoreDirectory, "transaction.json");
            byte[] validMarker = null;
            try
            {
                FirstArtRuinsCliffAssetBuilder.LeaveRecoveryMarkerForTests();
                validMarker = File.ReadAllBytes(marker);
                RecoveryManifestFixture manifest = JsonUtility.FromJson<RecoveryManifestFixture>(
                    File.ReadAllText(marker));
                ApplyManifestAttack(manifest, attack);
                File.WriteAllText(marker, JsonUtility.ToJson(manifest, true));

                Assert.Throws<InvalidOperationException>(
                    FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild,
                    "A legal-JSON manifest attack must fail before any destination is touched.");
                Assert.That(File.Exists(marker), Is.True, "Invalid manifest evidence must remain.");
                Assert.That(File.Exists(Path.Combine(restoreDirectory, "000.asset.bytes")), Is.True);
                AssertStates(CaptureOutputStates(), original, "manifest attack " + attack);
            }
            finally
            {
                if (Directory.Exists(restoreDirectory) && validMarker != null)
                {
                    File.WriteAllBytes(marker, validMarker);
                    FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                }
                else
                {
                    RestoreStates(original);
                }
            }
        }

        [Test]
        public void Builder_BeforeCommitFailureRollsBackBecauseRecoveryMarkerStillOwnsOutputs()
        {
            FieldInfo checkpoint = RequireBuilderActionField("BeforeCommitCheckpoint");
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            CorruptOneMaterial();
            Dictionary<string, AssetState> expected = CaptureOutputStates();
            try
            {
                checkpoint.SetValue(null, (Action)(() =>
                    throw new InjectedPublishFailure("before commit")));
                Assert.Throws<InjectedPublishFailure>(FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets);
                AssertStates(CaptureOutputStates(), expected, "before-commit rollback");
                Assert.That(Directory.Exists(ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot)), Is.False);
            }
            finally
            {
                checkpoint.SetValue(null, null);
                FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            }
        }

        [Test]
        public void Builder_AfterCommitCleanupFailureKeepsPublishedOutputsAndNextRecoveryOnlyCleansResidue()
        {
            FieldInfo checkpoint = RequireBuilderActionField("AfterCommitCleanupCheckpoint");
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> expectedPublished = CaptureOutputStates();
            CorruptOneMaterial();
            try
            {
                checkpoint.SetValue(null, (Action)(() =>
                    throw new InjectedPublishFailure("after commit cleanup")));
                Assert.DoesNotThrow(FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets,
                    "Cleanup failure after the atomic commit boundary must not request rollback.");
                AssertStates(CaptureOutputStates(), expectedPublished, "committed publication");
                string restoreDirectory = ProjectPath(FirstArtRuinsCliffAssetBuilder.RestoreRoot);
                Assert.That(Directory.Exists(restoreDirectory), Is.True);
                Assert.That(File.Exists(Path.Combine(restoreDirectory, "transaction.json")), Is.False);
                Assert.That(File.Exists(Path.Combine(restoreDirectory, "transaction.committed.json")), Is.True);

                checkpoint.SetValue(null, null);
                FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                Assert.That(Directory.Exists(restoreDirectory), Is.False);
                AssertStates(CaptureOutputStates(), expectedPublished, "post-commit residue cleanup");
            }
            finally
            {
                checkpoint.SetValue(null, null);
                FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();
                FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            }
        }

        [Test]
        public void Builder_IsolatedFirstBuildRollbackRemovesOnlyTestRootDirectoriesAndMetadata()
        {
            const string isolatedRoot =
                "Assets/_Game/Tests/EditMode/Temp_RuinsCliffAssetTransaction";
            bool preexisting = Directory.Exists(ProjectPath(isolatedRoot)) ||
                               File.Exists(ProjectPath(isolatedRoot) + ".meta");
            Assert.That(preexisting, Is.False,
                "The isolated transaction root pre-existed; refusing to delete unknown test content.");
            MethodInfo seam = typeof(FirstArtRuinsCliffAssetBuilder).GetMethod(
                "RunIsolatedFirstBuildRollbackForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(seam, Is.Not.Null, "The isolated internal transaction seam is missing.");
            try
            {
                seam.Invoke(null, null);
                foreach (string path in new[]
                         {
                             isolatedRoot,
                             isolatedRoot + "/Geometry",
                             isolatedRoot + "/Ruins/Runtime",
                             isolatedRoot + "/Ruins/Runtime/Prefabs",
                             isolatedRoot + "/Cliff/Runtime",
                             isolatedRoot + "/Cliff/Runtime/Prefabs",
                         })
                {
                    Assert.That(Directory.Exists(ProjectPath(path)), Is.False, path);
                    Assert.That(File.Exists(ProjectPath(path) + ".meta"), Is.False, path + ".meta");
                }
            }
            finally
            {
                if (!preexisting)
                {
                    if (AssetDatabase.IsValidFolder(isolatedRoot))
                        AssetDatabase.DeleteAsset(isolatedRoot);
                    else if (Directory.Exists(ProjectPath(isolatedRoot)))
                        Directory.Delete(ProjectPath(isolatedRoot), true);
                    if (File.Exists(ProjectPath(isolatedRoot) + ".meta"))
                        File.Delete(ProjectPath(isolatedRoot) + ".meta");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        [Test]
        public void Builder_RecoveryRemovesAssetAndMetaForOriginallyMissingOutput()
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> original = CaptureOutputStates();
            try
            {
                Assert.That(AssetDatabase.DeleteAsset(ProfilePath), Is.True);
                FirstArtRuinsCliffAssetBuilder.LeaveRecoveryMarkerForTests();
                File.WriteAllBytes(ProjectPath(ProfilePath) + ".meta", original[ProfilePath].MetaBytes);

                FirstArtRuinsCliffAssetBuilder.RecoverInterruptedBuild();

                Assert.That(File.Exists(ProjectPath(ProfilePath)), Is.False);
                Assert.That(File.Exists(ProjectPath(ProfilePath) + ".meta"), Is.False,
                    "Recovery must explicitly verify that metadata for an originally missing output is gone.");
            }
            finally
            {
                RestoreStates(original);
            }
        }

        [Test]
        public void Builder_FastReturnRepairsLayerIndexAndInstancingTampering()
        {
            FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();
            Dictionary<string, AssetState> original = CaptureOutputStates();
            try
            {
                string path = MaterialDirectory + "MAT_Ruins_Concrete.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                material.SetFloat("_LayerIndex", 3f);
                material.enableInstancing = false;
                AssetDatabase.SaveAssetIfDirty(material);

                FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets();

                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material.GetFloat("_LayerIndex"), Is.EqualTo(4f));
                Assert.That(material.enableInstancing, Is.True);
            }
            finally
            {
                RestoreStates(original);
            }
        }

        [Test]
        public void GeometryShader_FinalNormalUsesSafeFallbackForOppositeOrZeroBlend()
        {
            string source = File.ReadAllText(ProjectPath(ShaderPath));
            Assert.That(source, Does.Contain("half3 geometricNormalWS = SafeNormalize"));
            Assert.That(source, Does.Contain("inputData.normalWS = SafeNormalize("));
            Assert.That(source, Does.Contain("geometricNormalWS);"),
                "A zero lerp between opposite normals must fall back to the geometric normal rather than normalize NaN.");
            Assert.That(source, Does.Not.Contain(
                "inputData.normalWS = normalize(lerp(normalize(input.normalWS), detailNormalWS"));
        }

        [Test]
        public void GeometryShader_VerticalAndHorizontalSurfacesHaveNonDegenerateVariation()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);

            string[] protectedPaths =
            {
                "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset",
                "ProjectSettings/GraphicsSettings.asset",
                "ProjectSettings/QualitySettings.asset",
            };
            Dictionary<string, byte[]> protectedBytes = protectedPaths.ToDictionary(
                path => path,
                path => File.ReadAllBytes(ProjectPath(path)),
                StringComparer.Ordinal);
            RenderPipelineAsset previousPipeline = GraphicsSettings.defaultRenderPipeline;
            int previousAntiAliasing = QualitySettings.antiAliasing;
            bool previousLightsUseColorTemperature = GraphicsSettings.lightsUseColorTemperature;
            var material = new Material(shader);
            try
            {
                Assert.That(
                    GrayboxRenderPipelineBuildScope.BeginForScenes(new[] { GrayboxScenePath }),
                    Is.True,
                    "The render test must exercise the approved URP rather than Built-in fallback behavior.");
                for (int index = 0; index < ArrayProperties.Length; index++)
                    material.SetTexture(ArrayProperties[index],
                        AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArrayPaths[index]));
                material.SetFloat("_LayerIndex", 6f);
                material.SetFloat("_TriplanarScale", 2.25f);
                material.SetColor("_RoleTint", Color.white);
                material.SetFloat("_RoleTintStrength", 0f);
                material.SetFloat("_MetallicScale", 0.1f);
                material.SetFloat("_SmoothnessScale", 0.35f);
                material.SetFloat("_NormalStrength", 1f);
                material.SetFloat("_HeightStrength", 0.35f);
                var rendered = new List<KeyValuePair<Vector3, RenderStats>>();
                foreach (Vector3 normalDirection in new[] { Vector3.up, Vector3.right, Vector3.forward })
                {
                    RenderStats stats = RenderPlane(material, normalDirection);
                    Debug.Log("[IDEA-0004-RENDER] normal=" + normalDirection +
                              " mean=" + stats.MeanLuminance.ToString("R") +
                              " magenta=" + stats.MagentaRatio.ToString("R") +
                              " dx=" + stats.HorizontalGradient.ToString("R") +
                              " dy=" + stats.VerticalGradient.ToString("R"));
                    rendered.Add(new KeyValuePair<Vector3, RenderStats>(normalDirection, stats));
                }
                foreach (KeyValuePair<Vector3, RenderStats> pair in rendered)
                {
                    Vector3 normalDirection = pair.Key;
                    RenderStats stats = pair.Value;
                    Assert.That(stats.MeanLuminance, Is.GreaterThan(0.01f), normalDirection + " black");
                    Assert.That(stats.MeanLuminance, Is.LessThan(0.95f), normalDirection + " white");
                    Assert.That(stats.MagentaRatio, Is.EqualTo(0f), normalDirection + " error shader");
                    Assert.That(stats.HorizontalGradient, Is.GreaterThan(0.0002f),
                        normalDirection + " horizontal variation");
                    Assert.That(stats.VerticalGradient, Is.GreaterThan(0.0002f),
                        normalDirection + " vertical variation");
                }
            }
            finally
            {
                GrayboxRenderPipelineBuildScope.RestoreAfterBuild();
                GraphicsSettings.lightsUseColorTemperature = previousLightsUseColorTemperature;
                UnityEngine.Object.DestroyImmediate(material);
            }

            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(previousPipeline));
            Assert.That(QualitySettings.antiAliasing, Is.EqualTo(previousAntiAliasing));
            Assert.That(GraphicsSettings.lightsUseColorTemperature,
                Is.EqualTo(previousLightsUseColorTemperature));
            foreach (string path in protectedPaths)
                CollectionAssert.AreEqual(protectedBytes[path], File.ReadAllBytes(ProjectPath(path)), path);
            foreach (string path in GrayboxRestoreArtifacts())
                Assert.That(File.Exists(ProjectPath(path)), Is.False, path);
        }

        private static RenderStats RenderPlane(Material material, Vector3 normal)
        {
            var root = new GameObject("GeometryShaderRenderFixture");
            var cameraObject = new GameObject("GeometryShaderCamera");
            var lightObject = new GameObject("GeometryShaderLight");
            var renderTexture = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(96, 96, TextureFormat.RGBA32, false, false);
            try
            {
                Mesh mesh = CreatePlane(normal);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightObject.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -1f, -1f).normalized);

                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 1.05f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.transform.position = normal * 3f;
                camera.transform.rotation = Quaternion.LookRotation(-normal, Math.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up);
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;
                Color32[] pixels = readback.GetPixels32();
                double sum = 0;
                double horizontalGradient = 0;
                double verticalGradient = 0;
                int magentaCount = 0;
                int count = 0;
                for (int y = 16; y < 80; y++)
                for (int x = 16; x < 80; x++)
                {
                    Color32 pixel = pixels[y * 96 + x];
                    float luminance = (pixel.r + pixel.g + pixel.b) / (3f * 255f);
                    sum += luminance;
                    if (pixel.r > 220 && pixel.b > 220 && pixel.g < 80)
                        magentaCount++;
                    if (x > 16)
                    {
                        Color32 left = pixels[y * 96 + x - 1];
                        float leftLuminance = (left.r + left.g + left.b) / (3f * 255f);
                        horizontalGradient += Math.Abs(luminance - leftLuminance);
                    }
                    if (y > 16)
                    {
                        Color32 below = pixels[(y - 1) * 96 + x];
                        float belowLuminance = (below.r + below.g + below.b) / (3f * 255f);
                        verticalGradient += Math.Abs(luminance - belowLuminance);
                    }
                    count++;
                }
                UnityEngine.Object.DestroyImmediate(mesh);
                return new RenderStats(
                    (float)(sum / count),
                    magentaCount / (float)count,
                    (float)(horizontalGradient / (63 * 64)),
                    (float)(verticalGradient / (63 * 64)));
            }
            finally
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                    camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(readback);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        private static Mesh CreatePlane(Vector3 normal)
        {
            Vector3 tangent = Math.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f
                ? Vector3.right
                : Vector3.up;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            tangent = Vector3.Cross(bitangent, normal).normalized;
            var mesh = new Mesh { name = "TriplanarContractPlane" };
            mesh.vertices = new[]
            {
                -tangent - bitangent,
                tangent - bitangent,
                tangent + bitangent,
                -tangent + bitangent,
            };
            mesh.normals = Enumerable.Repeat(normal, 4).ToArray();
            mesh.tangents = Enumerable.Repeat(new Vector4(tangent.x, tangent.y, tangent.z, 1f), 4).ToArray();
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(source.center);
            Vector3 extents = source.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Math.Abs(axisX.x) + Math.Abs(axisY.x) + Math.Abs(axisZ.x),
                Math.Abs(axisX.y) + Math.Abs(axisY.y) + Math.Abs(axisZ.y),
                Math.Abs(axisX.z) + Math.Abs(axisY.z) + Math.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static Matrix4x4 CalibrationMatrix(FirstArtRuinsCliffCatalogEntry3D entry)
        {
            return Matrix4x4.Translate(entry.ChildOffset) *
                   Matrix4x4.Scale(entry.RootScale) *
                   entry.SourceImportMatrix;
        }

        private static Vector3 DeriveChildOffset(
            Bounds rawBounds,
            Matrix4x4 importMatrix,
            Vector3 rootScale)
        {
            Bounds imported = TransformBounds(rawBounds, importMatrix);
            Vector3 center = Vector3.Scale(imported.center, rootScale);
            Vector3 extents = Vector3.Scale(imported.extents, rootScale);
            return new Vector3(-center.x, -(center.y - extents.y), -center.z);
        }

        private static string[] OutputPaths()
        {
            return FirstArtRuinsCliffCatalog3D.MaterialRoles
                .Select(role => MaterialDirectory + role.Name + ".mat")
                .Concat(FirstArtRuinsCliffCatalog3D.Entries.Select(entry => entry.PrefabPath))
                .Concat(new[] { ProfilePath })
                .ToArray();
        }

        private static Dictionary<string, AssetState> CaptureOutputStates()
        {
            return OutputPaths().ToDictionary(
                path => path,
                path => AssetState.Capture(path),
                StringComparer.Ordinal);
        }

        private static void AssertStates(
            IReadOnlyDictionary<string, AssetState> actual,
            IReadOnlyDictionary<string, AssetState> expected,
            string message)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), message);
            foreach (string path in expected.Keys)
            {
                AssetState expectedState = expected[path];
                AssetState actualState = actual[path];
                Assert.That(actualState.Exists, Is.EqualTo(expectedState.Exists), message + ":" + path);
                Assert.That(actualState.Guid, Is.EqualTo(expectedState.Guid), message + ":GUID:" + path);
                if (!expectedState.Exists)
                    continue;
                CollectionAssert.AreEqual(expectedState.AssetBytes, actualState.AssetBytes,
                    message + ":asset:" + path);
                CollectionAssert.AreEqual(expectedState.MetaBytes, actualState.MetaBytes,
                    message + ":meta:" + path);
            }
        }

        private static void RestoreStates(IReadOnlyDictionary<string, AssetState> states)
        {
            foreach (KeyValuePair<string, AssetState> pair in states)
            {
                string absolute = ProjectPath(pair.Key);
                if (File.Exists(absolute) || AssetDatabase.LoadMainAssetAtPath(pair.Key) != null)
                    AssetDatabase.DeleteAsset(pair.Key);
                if (!pair.Value.Exists)
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                File.WriteAllBytes(absolute, pair.Value.AssetBytes);
                File.WriteAllBytes(absolute + ".meta", pair.Value.MetaBytes);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssertStates(CaptureOutputStates(), states, "external test restoration");
        }

        private static void CorruptOneMaterial()
        {
            string path = MaterialDirectory + "MAT_Ruins_Concrete.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null, path);
            material.SetFloat("_HeightStrength", material.GetFloat("_HeightStrength") + 0.125f);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                relativePath));
        }

        private static void EnsureAssetFolderForTest(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            int slash = assetPath.LastIndexOf('/');
            string parent = assetPath.Substring(0, slash);
            EnsureAssetFolderForTest(parent);
            Assert.That(AssetDatabase.CreateFolder(parent, assetPath.Substring(slash + 1)), Is.Not.Empty);
        }

        private static FieldInfo RequireBuilderActionField(string name)
        {
            FieldInfo field = typeof(FirstArtRuinsCliffAssetBuilder).GetField(
                name,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name + " test checkpoint is missing.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(Action)), name);
            return field;
        }

        private static void ClearBuilderActionField(string name)
        {
            FieldInfo field = typeof(FirstArtRuinsCliffAssetBuilder).GetField(
                name,
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        private static void ApplyManifestAttack(RecoveryManifestFixture manifest, int attack)
        {
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.entries, Is.Not.Null.And.Not.Empty);
            switch (attack)
            {
                case 0:
                    manifest.entries = new RecoveryEntryFixture[0];
                    break;
                case 1:
                    manifest.entries = manifest.entries.Take(manifest.entries.Length - 1).ToArray();
                    break;
                case 2:
                    manifest.entries[manifest.entries.Length - 1] = CloneEntry(manifest.entries[0]);
                    break;
                case 3:
                    manifest.entries = manifest.entries.Concat(new[]
                    {
                        new RecoveryEntryFixture
                        {
                            assetPath = "Assets/_Game/Tests/EditMode/Temp_RuinsCliffManifestExtra.mat",
                            existed = false,
                            guid = string.Empty,
                            backupAsset = "028.asset.bytes",
                            backupMeta = "028.meta.bytes",
                            assetSha256 = string.Empty,
                            metaSha256 = string.Empty,
                        },
                    }).ToArray();
                    break;
                case 4:
                    RecoveryEntryFixture traversal = manifest.entries[manifest.entries.Length - 1];
                    traversal.assetPath =
                        "Assets/_Game/Tests/EditMode/../EditMode/Temp_RuinsCliffTraversal.asset";
                    traversal.existed = false;
                    traversal.guid = string.Empty;
                    traversal.assetSha256 = string.Empty;
                    traversal.metaSha256 = string.Empty;
                    break;
                case 5:
                    manifest.originallyMissingDirectories = new[]
                    {
                        "Assets/_Game/Tests/EditMode/Temp_RuinsCliffUnauthorizedDirectory",
                    };
                    break;
                default:
                    Assert.Fail("Unknown manifest attack: " + attack);
                    break;
            }
        }

        private static RecoveryEntryFixture CloneEntry(RecoveryEntryFixture source)
        {
            return new RecoveryEntryFixture
            {
                assetPath = source.assetPath,
                existed = source.existed,
                guid = source.guid,
                backupAsset = source.backupAsset,
                backupMeta = source.backupMeta,
                assetSha256 = source.assetSha256,
                metaSha256 = source.metaSha256,
            };
        }

        private static string[] GrayboxRestoreArtifacts()
        {
            return new[]
            {
                "Library/WasteCity.GrayboxBuildPipelineRestore.txt",
                "Library/WasteCity.GrayboxBuildPipelineRestore.asset",
                "Library/WasteCity.GrayboxBuildPipelineRestore.GraphicsSettings.asset",
                "Library/WasteCity.GrayboxBuildPipelineRestore.QualitySettings.asset",
            };
        }

        private static void AssertMatrix(
            Matrix4x4 actual,
            Matrix4x4 expected,
            string message,
            float tolerance)
        {
            for (int index = 0; index < 16; index++)
                Assert.That(actual[index], Is.EqualTo(expected[index]).Within(tolerance),
                    message + "[" + index + "]");
        }

        private static void AssertQuaternion(
            Quaternion actual,
            Quaternion expected,
            string message,
            float tolerance)
        {
            Assert.That(1f - Mathf.Abs(Quaternion.Dot(actual, expected)),
                Is.LessThanOrEqualTo(tolerance), message);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string message, float tolerance = 0.000001f)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), message + ".x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), message + ".y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), message + ".z");
        }

        private readonly struct RenderStats
        {
            public RenderStats(
                float meanLuminance,
                float magentaRatio,
                float horizontalGradient,
                float verticalGradient)
            {
                MeanLuminance = meanLuminance;
                MagentaRatio = magentaRatio;
                HorizontalGradient = horizontalGradient;
                VerticalGradient = verticalGradient;
            }

            public float MeanLuminance { get; }
            public float MagentaRatio { get; }
            public float HorizontalGradient { get; }
            public float VerticalGradient { get; }
        }

        private sealed class AssetState
        {
            private AssetState(bool exists, byte[] assetBytes, byte[] metaBytes, string guid)
            {
                Exists = exists;
                AssetBytes = assetBytes;
                MetaBytes = metaBytes;
                Guid = guid;
            }

            public bool Exists { get; }
            public byte[] AssetBytes { get; }
            public byte[] MetaBytes { get; }
            public string Guid { get; }

            public static AssetState Capture(string assetPath)
            {
                string absolute = ProjectPath(assetPath);
                if (!File.Exists(absolute))
                    return new AssetState(false, null, null, string.Empty);
                Assert.That(File.Exists(absolute + ".meta"), Is.True, assetPath + " metadata");
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                Assert.That(guid, Is.Not.Empty, assetPath + " GUID");
                return new AssetState(
                    true,
                    File.ReadAllBytes(absolute),
                    File.ReadAllBytes(absolute + ".meta"),
                    guid);
            }
        }

        private sealed class InjectedPublishFailure : Exception
        {
            public InjectedPublishFailure(string message) : base(message) { }
        }

        [Serializable]
        private sealed class RecoveryManifestFixture
        {
            public string version;
            public RecoveryEntryFixture[] entries;
            public string[] originallyMissingDirectories;
        }

        [Serializable]
        private sealed class RecoveryEntryFixture
        {
            public string assetPath;
            public bool existed;
            public string guid;
            public string backupAsset;
            public string backupMeta;
            public string assetSha256;
            public string metaSha256;
        }
    }
}
