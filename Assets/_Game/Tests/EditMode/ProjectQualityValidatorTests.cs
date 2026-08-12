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
        [TestCase("unmapped-source", "PQ001", "Assets/_Game/Scripts/Other.cs")]
        [TestCase("unmapped-test", "PQ002", "Assets/_Game/Tests/EditMode/OtherTests.cs")]
        [TestCase("missing-reuse-path", "PQ003", "Assets/_Game/Scripts/Feature/MissingComponent.cs")]
        [TestCase("missing-required-test", "PQ004", "Assets/_Game/Tests/EditMode/MissingTests.cs")]
        [TestCase("unknown-feature", "PQ005", "feature-component")]
        [TestCase("wrong-scene-index", "PQ006", "Assets/_Game/Scenes/FeatureScene.unity")]
        [TestCase("frozen-recommended", "PQ007", "frozen-scene")]
        [TestCase("placeholder-recommended", "PQ008", "prohibited-entry")]
        [TestCase("missing-ui-owner", "PQ009", "feature-ui")]
        [TestCase("missing-human-link", "PQ010", "Docs/guide.md -> missing.md")]
        public void Validate_ReturnsStableIssueForEachBrokenContract(
            string mutation, string expectedCode, string expectedPath)
        {
            Fixture fixture = ValidFixture();
            fixture.Apply(mutation);

            IReadOnlyList<ProjectQualityIssue> issues =
                ProjectQualityValidator.Validate(
                    fixture.Catalog, fixture.Snapshot, fixture.Root);

            AssertIssuesExactly(issues, expectedCode);
            Assert.That(issues[0].Path, Is.EqualTo(expectedPath));
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

        [Test]
        public void Validate_RealFrozenReuseBecomingRecommended_ReturnsOnlyPq007()
        {
            ProjectQualityCatalog catalog = CurrentCatalog();
            ProjectReuseEntry reuse = catalog.ReuseEntries.Single(entry => entry.Id == "formal-prototype-frozen");
            reuse.ReuseLevel = ProjectReuseLevel.Recommended;

            AssertIssuesExactly(ValidateCurrent(catalog), "PQ007");
        }

        [Test]
        public void Validate_RealFrozenSceneBecomingRecommended_ReturnsOnlyPq007()
        {
            ProjectQualityCatalog catalog = CurrentCatalog();
            ProjectSceneEntry scene = catalog.Scenes.Single(entry => entry.Id == "formal-prototype");
            scene.ReuseLevel = ProjectReuseLevel.Recommended;

            AssertIssuesExactly(ValidateCurrent(catalog), "PQ007");
        }

        [Test]
        public void Validate_RealProhibitedReuseBecomingRecommended_ReturnsOnlyPq008()
        {
            ProjectQualityCatalog catalog = CurrentCatalog();
            ProjectReuseEntry reuse = catalog.ReuseEntries.Single(entry => entry.Id == "placeholder-building-controller-frozen");
            reuse.ReuseLevel = ProjectReuseLevel.Recommended;

            AssertIssuesExactly(ValidateCurrent(catalog), "PQ008");
        }

        [Test]
        public void Validate_RejectsMissingSimpleCatalogTypeAgainstSnapshot()
        {
            ProjectQualityCatalog catalog = CurrentCatalog();
            catalog.ReuseEntries.Single(entry => entry.Id == "stable-id").TypeNames[0] = "MissingStableId";

            AssertIssuesExactly(ValidateCurrent(catalog), "PQ003");
        }

        [Test]
        public void Validate_RejectsUnknownEditorPathAgainstCommittedCatalog()
        {
            ProjectInventorySnapshot snapshot = CurrentSnapshot();
            snapshot.FileRecords = snapshot.FileRecords.Concat(new[]
            {
                new ProjectFileRecord
                {
                    Path = "Assets/_Game/Editor/FutureUnknownTool.cs",
                    Kind = ProjectFileKind.Production,
                },
            }).ToArray();

            AssertIssuesExactly(Validate(CurrentCatalog(), snapshot), "PQ001");
        }

        [Test]
        public void Validate_RejectsUnregisteredEnabledSnapshotScene()
        {
            ProjectInventorySnapshot snapshot = CurrentSnapshot();
            snapshot.SceneRecords = snapshot.SceneRecords.Concat(new[]
            {
                new ProjectSceneRecord { Path = "Assets/_Game/Scenes/Future.unity", BuildIndex = 2 },
            }).ToArray();
            snapshot.ScenePaths = snapshot.ScenePaths.Concat(new[] { "Assets/_Game/Scenes/Future.unity" }).ToArray();

            AssertIssuesExactly(Validate(CurrentCatalog(), snapshot), "PQ006");
        }

        [Test]
        public void Validate_RejectsFeatureScenePathOutsideCatalogSceneEntries()
        {
            ProjectQualityCatalog catalog = CurrentCatalog();
            catalog.FeatureGroups[0].ScenePaths[0] = "Assets/_Game/Scenes/Missing.unity";

            AssertIssuesExactly(ValidateCurrent(catalog), "PQ005");
        }

        [Test]
        public void Validate_RejectsReasonlessExclusionAndDoesNotSuppressCoverage()
        {
            Fixture fixture = ValidFixture();
            const string path = "Assets/_Game/Scripts/Unmapped.cs";
            fixture.Snapshot.FileRecords = fixture.Snapshot.FileRecords.Concat(new[]
            {
                new ProjectFileRecord { Path = path, Kind = ProjectFileKind.Production },
            }).ToArray();
            fixture.Catalog.ExplicitSourceExclusions = new[]
            {
                new ProjectPathExclusion { Path = path, Reason = string.Empty },
            };

            AssertIssuesExactly(ProjectQualityValidator.Validate(fixture.Catalog, fixture.Snapshot, fixture.Root),
                "PQ001", "PQ005");
        }

        [Test]
        public void Validate_ReturnsDeterministicallySortedWellFormedIssues()
        {
            Fixture fixture = ValidFixture();
            fixture.Snapshot.FileRecords = fixture.Snapshot.FileRecords.Concat(new[]
            {
                new ProjectFileRecord { Path = "Assets/_Game/Scripts/Z.cs", Kind = ProjectFileKind.Production },
                new ProjectFileRecord { Path = "Assets/_Game/Scripts/A.cs", Kind = ProjectFileKind.Production },
            }).ToArray();

            IReadOnlyList<ProjectQualityIssue> issues =
                ProjectQualityValidator.Validate(fixture.Catalog, fixture.Snapshot, fixture.Root);

            AssertIssuesExactly(issues, "PQ001", "PQ001");
            for (int index = 1; index < issues.Count; index++)
                Assert.That(CompareIssues(issues[index - 1], issues[index]), Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void SortIssues_UsesMessageAsThirdOrdinalKeyForSameCodeAndPath()
        {
            IReadOnlyList<ProjectQualityIssue> issues = ProjectQualityValidator.SortIssuesForTests(new[]
            {
                new ProjectQualityIssue
                {
                    Code = "PQ001", Path = "Assets/_Game/Scripts/Same.cs", Severity = ProjectQualityIssueSeverity.Error,
                    PlainChineseMessage = "中文乙",
                },
                new ProjectQualityIssue
                {
                    Code = "PQ001", Path = "Assets/_Game/Scripts/Same.cs", Severity = ProjectQualityIssueSeverity.Error,
                    PlainChineseMessage = "中文甲",
                },
            });

            CollectionAssert.AreEqual(new[] { "中文乙", "中文甲" },
                issues.Select(issue => issue.PlainChineseMessage).ToArray());
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
            const string prohibitedPath = "Assets/_Game/Scripts/Frozen/ProhibitedComponent.cs";
            const string testPath = "Assets/_Game/Tests/EditMode/FeatureComponentTests.cs";
            const string scenePath = "Assets/_Game/Scenes/FeatureScene.unity";
            const string frozenScenePath = "Assets/_Game/Scenes/FrozenScene.unity";
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
            var frozenFeature = new ProjectFeatureGroup
            {
                Id = "frozen-2d-regression",
                ChineseName = "冻结二维",
                SourceGlobs = new[] { "Assets/_Game/Scripts/Frozen/**" },
                TestFileGlobs = new[] { testPath },
                ScenePaths = new[] { frozenScenePath },
                RequirementIds = new[] { "DOC-0001" },
                HumanDocumentPaths = new[] { documentationPath },
                MinimumVerification = ProjectVerificationLevel.FullRegression,
            };
            var frozenReuse = new ProjectReuseEntry
            {
                Id = "frozen-scene",
                ChineseName = "冻结场景",
                TypeNames = new[] { "WasteCity.Feature.FeatureComponent" },
                AssetPaths = new[] { frozenScenePath },
                FeatureGroupId = frozenFeature.Id,
                ReuseLevel = ProjectReuseLevel.FrozenRegression,
                UseSummary = "仅供冻结回归",
                BoundarySummary = "不供新工作复用",
                RequiredTestFiles = new[] { testPath },
                RequirementIds = new[] { "DOC-0001" },
            };
            var prohibitedReuse = new ProjectReuseEntry
            {
                Id = "prohibited-entry",
                ChineseName = "禁止条目",
                TypeNames = new[] { "WasteCity.Frozen.ProhibitedComponent" },
                AssetPaths = new[] { prohibitedPath },
                FeatureGroupId = frozenFeature.Id,
                ReuseLevel = ProjectReuseLevel.ProhibitedForNewWork,
                UseSummary = "只供历史回归",
                BoundarySummary = "禁止新工作引用",
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
                OwnerTypeName = "WasteCity.Feature.FeatureUiOwner",
                SceneId = scene.Id,
                InputPrioritySummary = "优先处理功能输入",
                RequiredTestFiles = new[] { testPath },
            };
            var catalog = new ProjectQualityCatalog
            {
                SchemaVersion = 1,
                FeatureGroups = new[] { feature, frozenFeature },
                ReuseEntries = new[] { reuse, frozenReuse, prohibitedReuse },
                Scenes = new[]
                {
                    scene,
                    new ProjectSceneEntry
                    {
                        Id = "frozen-scene", ChineseName = "冻结场景", Path = frozenScenePath,
                        Purpose = "冻结回归", EnabledInBuildSettings = true, ExpectedBuildIndex = 1,
                        ReuseLevel = ProjectReuseLevel.FrozenRegression,
                    },
                },
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
                    new ProjectFileRecord { Path = prohibitedPath, Kind = ProjectFileKind.Production },
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
                    new ProjectTypeRecord
                    {
                        FullName = "WasteCity.Feature.FeatureUiOwner",
                        AssemblyName = "WasteCity.Game",
                        SourcePath = sourcePath,
                        Kind = ProjectTypeKind.MonoBehaviour,
                    },
                    new ProjectTypeRecord
                    {
                        FullName = "WasteCity.Frozen.ProhibitedComponent",
                        AssemblyName = "WasteCity.Game",
                        SourcePath = prohibitedPath,
                        Kind = ProjectTypeKind.PlainCSharp,
                    },
                },
                AssemblyRecords = new ProjectAssemblyRecord[0],
                SceneRecords = new[]
                {
                    new ProjectSceneRecord { Path = scenePath, BuildIndex = 0 },
                    new ProjectSceneRecord { Path = frozenScenePath, BuildIndex = 1 },
                },
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
                ScenePaths = new[] { scenePath, frozenScenePath },
            };
            return new Fixture(root, catalog, snapshot);
        }

        private static ProjectQualityCatalog CurrentCatalog()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return ProjectQualityCatalogLoader.LoadFromFile(
                Path.Combine(root, "Docs/Engineering/project-quality-catalog.json"));
        }

        private static ProjectInventorySnapshot CurrentSnapshot()
        {
            return ProjectQualityScanner.Scan(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        }

        private static IReadOnlyList<ProjectQualityIssue> ValidateCurrent(ProjectQualityCatalog catalog)
        {
            return Validate(catalog, CurrentSnapshot());
        }

        private static IReadOnlyList<ProjectQualityIssue> Validate(ProjectQualityCatalog catalog,
            ProjectInventorySnapshot snapshot)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return ProjectQualityValidator.Validate(catalog, snapshot, root);
        }

        private static void AssertIssuesExactly(IReadOnlyList<ProjectQualityIssue> issues, params string[] codes)
        {
            CollectionAssert.AreEqual(codes, issues.Select(issue => issue.Code).ToArray(),
                string.Join("\n", issues.Select(issue => issue.Code + " " + issue.Path + " " + issue.PlainChineseMessage)));
            foreach (ProjectQualityIssue issue in issues)
            {
                Assert.That(issue.Severity, Is.EqualTo(ProjectQualityIssueSeverity.Error));
                Assert.That(issue.Path, Is.Not.Null.And.Not.Empty);
                Assert.That(issue.PlainChineseMessage, Is.Not.Null.And.Not.Empty);
                Assert.That(issue.PlainChineseMessage.Any(IsCjk), Is.True, issue.Code);
            }
        }

        private static bool IsCjk(char value)
        {
            return value >= '\u3400' && value <= '\u9fff';
        }

        private static int CompareIssues(ProjectQualityIssue left, ProjectQualityIssue right)
        {
            int code = string.CompareOrdinal(left.Code, right.Code);
            if (code != 0) return code;
            int path = string.CompareOrdinal(left.Path, right.Path);
            return path != 0 ? path : string.CompareOrdinal(left.PlainChineseMessage, right.PlainChineseMessage);
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
                        Catalog.ReuseEntries[1].ReuseLevel = ProjectReuseLevel.Recommended;
                        return;
                    case "placeholder-recommended":
                        Catalog.ReuseEntries[2].ReuseLevel = ProjectReuseLevel.Recommended;
                        return;
                    case "missing-ui-owner":
                        Snapshot.TypeRecords = Snapshot.TypeRecords.Where(record =>
                            record.FullName != "WasteCity.Feature.FeatureUiOwner").ToArray();
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
