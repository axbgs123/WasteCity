using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Editor.ProjectQuality;

namespace WasteCity.Tests
{
    public sealed class ProjectQualityValidatorTests
    {
        [TestCase("unmapped-source", "PQ001")]
        [TestCase("unmapped-test", "PQ002")]
        [TestCase("missing-reuse-path", "PQ003")]
        [TestCase("missing-required-test", "PQ004")]
        [TestCase("unknown-feature", "PQ005")]
        [TestCase("wrong-scene-index", "PQ006")]
        [TestCase("frozen-recommended", "PQ007")]
        [TestCase("placeholder-recommended", "PQ008")]
        [TestCase("missing-ui-owner", "PQ009")]
        [TestCase("missing-human-link", "PQ010")]
        public void Validate_ReturnsStableIssueForEachBrokenContract(
            string mutation, string expectedCode)
        {
            Fixture fixture = ValidFixture();
            fixture.Apply(mutation);

            IReadOnlyList<ProjectQualityIssue> issues =
                ProjectQualityValidator.Validate(
                    fixture.Catalog, fixture.Snapshot, fixture.Root);

            Assert.That(issues.Any(x => x.Code == expectedCode), Is.True,
                string.Join("\n", issues.Select(x => x.PlainChineseMessage)));
        }

        [Test]
        public void Validate_DoesNotWriteAnyFixtureFile()
        {
            Fixture fixture = ValidFixture();
            string[] before = HashFixtureFiles(fixture.Root);

            ProjectQualityValidator.Validate(fixture.Catalog, fixture.Snapshot, fixture.Root);

            CollectionAssert.AreEqual(before, HashFixtureFiles(fixture.Root));
        }

        [Test]
        public void Validate_RejectsANewFakeSourcePathWithoutABroadCatchAll()
        {
            Fixture fixture = ValidFixture();
            fixture.Snapshot.FileRecords = fixture.Snapshot.FileRecords.Concat(new[]
            {
                new ProjectFileRecord
                {
                    Path = "Assets/_Game/Scripts/NewFakeFeature.cs",
                    Kind = ProjectFileKind.Production,
                },
            }).ToArray();

            IReadOnlyList<ProjectQualityIssue> issues =
                ProjectQualityValidator.Validate(fixture.Catalog, fixture.Snapshot, fixture.Root);

            Assert.That(issues.Any(x => x.Code == "PQ001" &&
                x.Path == "Assets/_Game/Scripts/NewFakeFeature.cs"), Is.True);
        }

        [Test]
        public void Validate_CurrentCatalogAndSnapshotHaveNoActiveIssues()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromFile(
                Path.Combine(root, "Docs/Engineering/project-quality-catalog.json"));
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(root);

            IReadOnlyList<ProjectQualityIssue> issues =
                ProjectQualityValidator.Validate(catalog, snapshot, root);

            Assert.That(issues, Is.Empty, string.Join("\n", issues.Select(issue =>
                issue.Code + " " + issue.Path + " " + issue.PlainChineseMessage)));
        }

        private static Fixture ValidFixture()
        {
            string root = Path.Combine(Path.GetTempPath(), "wastecity-project-quality-validator",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Docs"));
            File.WriteAllText(Path.Combine(root, "Docs", "guide.md"),
                "[目录](catalog.md)\n");
            File.WriteAllText(Path.Combine(root, "Docs", "catalog.md"), "# 目录\n");

            const string sourcePath = "Assets/_Game/Scripts/Feature/FeatureComponent.cs";
            const string testPath = "Assets/_Game/Tests/EditMode/FeatureComponentTests.cs";
            const string scenePath = "Assets/_Game/Scenes/FeatureScene.unity";
            const string documentationPath = "Docs/guide.md";

            var feature = new ProjectFeatureGroup
            {
                Id = "feature",
                ChineseName = "功能",
                SourceGlobs = new[] { "Assets/_Game/Scripts/Feature/**" },
                TestFileGlobs = new[] { testPath },
                ScenePaths = new[] { scenePath },
                RequirementIds = new[] { "DOC-0001" },
                HumanDocumentPaths = new[] { documentationPath },
                MinimumVerification = ProjectVerificationLevel.FocusedEditMode,
            };
            var reuse = new ProjectReuseEntry
            {
                Id = "feature-component",
                ChineseName = "功能组件",
                TypeNames = new[] { "WasteCity.Feature.FeatureComponent" },
                AssetPaths = new[] { sourcePath },
                FeatureGroupId = feature.Id,
                ReuseLevel = ProjectReuseLevel.Recommended,
                UseSummary = "供功能使用",
                BoundarySummary = "不负责界面",
                RequiredTestFiles = new[] { testPath },
                RequirementIds = new[] { "DOC-0001" },
            };
            var scene = new ProjectSceneEntry
            {
                Id = "feature-scene",
                ChineseName = "功能场景",
                Path = scenePath,
                Purpose = "验证功能",
                EnabledInBuildSettings = true,
                ExpectedBuildIndex = 0,
                ReuseLevel = ProjectReuseLevel.SceneOnly,
            };
            var ui = new ProjectUiEntry
            {
                Id = "feature-ui",
                ChineseName = "功能界面",
                OwnerTypeName = "WasteCity.Feature.FeatureComponent",
                SceneId = scene.Id,
                InputPrioritySummary = "优先处理功能输入",
                RequiredTestFiles = new[] { testPath },
            };
            var catalog = new ProjectQualityCatalog
            {
                SchemaVersion = 1,
                FeatureGroups = new[] { feature },
                ReuseEntries = new[] { reuse },
                Scenes = new[] { scene },
                UiEntries = new[] { ui },
                DocumentationRules = new[]
                {
                    new ProjectDocumentationRule
                    {
                        Id = "feature-docs",
                        ChangedPathGlobs = new[] { "Assets/_Game/Scripts/Feature/**" },
                        ReviewDocumentPaths = new[] { documentationPath },
                        PlainChineseReason = "功能变化需要更新说明",
                    },
                },
                ExplicitSourceExclusions = new ProjectPathExclusion[0],
                ExplicitTestExclusions = new ProjectPathExclusion[0],
            };
            var snapshot = new ProjectInventorySnapshot
            {
                FileRecords = new[]
                {
                    new ProjectFileRecord { Path = sourcePath, Kind = ProjectFileKind.Production },
                    new ProjectFileRecord { Path = testPath, Kind = ProjectFileKind.EditModeTest },
                },
                TypeRecords = new[]
                {
                    new ProjectTypeRecord
                    {
                        FullName = "WasteCity.Feature.FeatureComponent",
                        AssemblyName = "WasteCity.Game",
                        SourcePath = sourcePath,
                        Kind = ProjectTypeKind.MonoBehaviour,
                    },
                },
                AssemblyRecords = new ProjectAssemblyRecord[0],
                SceneRecords = new[] { new ProjectSceneRecord { Path = scenePath, BuildIndex = 0 } },
                TestClasses = new[]
                {
                    new ProjectTestClassRecord
                    {
                        FullName = "WasteCity.Tests.FeatureComponentTests",
                        SourcePath = testPath,
                        Platform = ProjectTestPlatform.EditMode,
                    },
                },
                EditorEntryPoints = new ProjectEditorEntryPointRecord[0],
                AssemblyNames = new[] { "WasteCity.Game" },
                ScenePaths = new[] { scenePath },
            };
            return new Fixture(root, catalog, snapshot);
        }

        private static string[] HashFixtureFiles(string root)
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => ToRelativePath(root, path) + ":" + HashFile(path))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string HashFile(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string ToRelativePath(string root, string path)
        {
            return Path.GetFullPath(path).Substring(Path.GetFullPath(root).Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        private sealed class Fixture
        {
            public readonly string Root;
            public readonly ProjectQualityCatalog Catalog;
            public readonly ProjectInventorySnapshot Snapshot;

            public Fixture(string root, ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot)
            {
                Root = root;
                Catalog = catalog;
                Snapshot = snapshot;
            }

            public void Apply(string mutation)
            {
                switch (mutation)
                {
                    case "unmapped-source":
                        Snapshot.FileRecords = Snapshot.FileRecords.Concat(new[]
                        {
                            new ProjectFileRecord { Path = "Assets/_Game/Scripts/Other.cs", Kind = ProjectFileKind.Production },
                        }).ToArray();
                        return;
                    case "unmapped-test":
                        Snapshot.FileRecords = Snapshot.FileRecords.Concat(new[]
                        {
                            new ProjectFileRecord { Path = "Assets/_Game/Tests/EditMode/OtherTests.cs", Kind = ProjectFileKind.EditModeTest },
                        }).ToArray();
                        return;
                    case "missing-reuse-path":
                        Catalog.ReuseEntries[0].AssetPaths[0] = "Assets/_Game/Scripts/Feature/MissingComponent.cs";
                        return;
                    case "missing-required-test":
                        Catalog.ReuseEntries[0].RequiredTestFiles[0] = "Assets/_Game/Tests/EditMode/MissingTests.cs";
                        return;
                    case "unknown-feature":
                        Catalog.ReuseEntries[0].FeatureGroupId = "unknown";
                        return;
                    case "wrong-scene-index":
                        Snapshot.SceneRecords[0].BuildIndex = 1;
                        return;
                    case "frozen-recommended":
                        Catalog.ReuseEntries[0].ReuseLevel = ProjectReuseLevel.FrozenRegression;
                        return;
                    case "placeholder-recommended":
                        Catalog.ReuseEntries[0].AssetPaths[0] = "Assets/_Game/Scripts/Feature/PlaceholderComponent.cs";
                        return;
                    case "missing-ui-owner":
                        Snapshot.TypeRecords = new ProjectTypeRecord[0];
                        return;
                    case "missing-human-link":
                        File.WriteAllText(Path.Combine(Root, "Docs", "guide.md"), "[缺失](missing.md)\n");
                        return;
                    default:
                        Assert.Fail("Unknown mutation: " + mutation);
                        return;
                }
            }
        }
    }
}
