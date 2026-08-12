using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using WasteCity.Editor.ProjectQuality;

namespace WasteCity.Tests
{
    public sealed class ProjectDocumentationGeneratorTests
    {
        [Test]
        public void RenderStructuralDocuments_SameInputIsByteForByteStable()
        {
            IReadOnlyDictionary<string, string> first = ProjectDocumentationGenerator.RenderStructuralDocuments(
                CatalogFixture(), SnapshotFixture());
            IReadOnlyDictionary<string, string> second = ProjectDocumentationGenerator.RenderStructuralDocuments(
                CatalogFixture(), SnapshotFixture());

            CollectionAssert.AreEquivalent(first.Keys, second.Keys);
            foreach (string path in first.Keys)
                Assert.That(second[path], Is.EqualTo(first[path]));
        }

        [Test]
        public void StructuralDocuments_DoNotContainCurrentTimeOrGitSha()
        {
            string combined = string.Join("\n", ProjectDocumentationGenerator.RenderStructuralDocuments(
                CatalogFixture(), SnapshotFixture()).Values);

            StringAssert.DoesNotMatch(@"\b[0-9a-f]{40}\b", combined);
            StringAssert.DoesNotMatch(@"\d{4}-\d{2}-\d{2}T", combined);
        }

        [Test]
        public void RenderStructuralDocuments_UsesPlainChineseOrdinalSectionsAndRequiredPaths()
        {
            IReadOnlyDictionary<string, string> files = ProjectDocumentationGenerator.RenderStructuralDocuments(
                CatalogFixture(), SnapshotFixture());

            CollectionAssert.AreEquivalent(new[]
            {
                "Docs/Generated/Project-Inventory-ZH.md",
                "Docs/Generated/Test-Inventory-ZH.md",
            }, files.Keys);
            for (int section = 1; section <= 10; section++)
                StringAssert.Contains("## " + section + ".", files["Docs/Generated/Project-Inventory-ZH.md"]);
            for (int section = 1; section <= 6; section++)
                StringAssert.Contains("## " + section + ".", files["Docs/Generated/Test-Inventory-ZH.md"]);
            StringAssert.Contains("自动生成", files["Docs/Generated/Project-Inventory-ZH.md"]);
            StringAssert.Contains("普通中文", files["Docs/Generated/Test-Inventory-ZH.md"]);
            StringAssert.Contains("WasteCity.Tests.FeatureTests|WasteCity.Tests.RuntimeTests",
                files["Docs/Generated/Test-Inventory-ZH.md"]);
        }

        [Test]
        public void RenderDocumentationAttention_EmptyPathsReportsNoPendingReminder()
        {
            string output = ProjectDocumentationGenerator.RenderDocumentationAttention(
                CatalogFixture(), new string[0]);

            StringAssert.Contains("没有待处理的路径提醒", output);
            StringAssert.DoesNotContain("已更新", output);
            StringAssert.DoesNotContain("已批准", output);
        }

        [Test]
        public void RenderDocumentationAttention_MatchingPathsListsOnlyMatchingReminder()
        {
            string output = ProjectDocumentationGenerator.RenderDocumentationAttention(CatalogFixture(), new[]
            {
                "Assets/_Game/Scripts/Feature/FeatureComponent.cs",
                "Assets/_Game/Scripts/Other.cs",
            });

            StringAssert.Contains("Docs/guide.md", output);
            StringAssert.Contains("功能变化需要检查说明", output);
            StringAssert.DoesNotContain("Other.cs", output);
        }

        [Test]
        public void RenderVerification_UsesRecordedEvidenceAndRejectsMalformedSnapshot()
        {
            string output = ProjectDocumentationGenerator.RenderVerification(VerificationFixture());

            StringAssert.Contains("已记录的既有验证证据", output);
            StringAssert.Contains("1121/1121", output);
            StringAssert.Contains("等待用户复验", output);

            ProjectVerificationSnapshot malformed = VerificationFixture();
            malformed.VerifiedCommitSha = "ABC";
            Assert.Throws<InvalidOperationException>(() => ProjectDocumentationGenerator.RenderVerification(malformed));
            malformed = VerificationFixture();
            malformed.VerifiedAtIso8601 = "2026-08-12T12:00:00";
            Assert.Throws<InvalidOperationException>(() => ProjectDocumentationGenerator.RenderVerification(malformed));
            malformed = VerificationFixture();
            malformed.EditMode.XmlPath = string.Empty;
            Assert.Throws<InvalidOperationException>(() => ProjectDocumentationGenerator.RenderVerification(malformed));
            malformed = VerificationFixture();
            malformed.PlayMode.Failed = -1;
            Assert.Throws<InvalidOperationException>(() => ProjectDocumentationGenerator.RenderVerification(malformed));
            malformed = VerificationFixture();
            malformed.EditMode.Total = 2;
            Assert.Throws<InvalidOperationException>(() => ProjectDocumentationGenerator.RenderVerification(malformed));
            malformed = VerificationFixture();
            malformed.HumanPlaytestStatus = "自动推断为通过";
            Assert.Throws<InvalidOperationException>(() => ProjectDocumentationGenerator.RenderVerification(malformed));
        }

        [TestCase("../README.md")]
        [TestCase("Docs/07-Project-Use-and-Development-Guide-ZH.md")]
        [TestCase("/tmp/out.md")]
        public void WriteGeneratedFiles_RejectsOutsideGeneratedDirectory(string path)
        {
            Assert.Throws<InvalidOperationException>(() => ProjectDocumentationGenerator.WriteGeneratedFiles(
                FixtureRoot(), new Dictionary<string, string> { { path, "x" } }));
        }

        [Test]
        public void WriteGeneratedFiles_UsesUtf8LfFinalNewlineAndStableTwoRunHashes()
        {
            string root = FixtureRoot();
            IReadOnlyDictionary<string, string> files = ProjectDocumentationGenerator.RenderStructuralDocuments(
                CatalogFixture(), SnapshotFixture());
            ProjectDocumentationGenerator.WriteGeneratedFiles(root, files);
            string[] firstHashes = HashFiles(root, files.Keys);
            ProjectDocumentationGenerator.WriteGeneratedFiles(root, files);
            string[] secondHashes = HashFiles(root, files.Keys);

            CollectionAssert.AreEqual(firstHashes, secondHashes);
            foreach (string path in files.Keys)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, path));
                Assert.That(bytes.Length, Is.GreaterThan(0));
                Assert.That(bytes.Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
                StringAssert.DoesNotContain("\r", Encoding.UTF8.GetString(bytes));
                Assert.That(bytes[bytes.Length - 1], Is.EqualTo((byte)'\n'));
                Assert.That(File.Exists(Path.Combine(root, path + ".tmp")), Is.False);
            }
        }

        [Test]
        public void Validate_DetectsStaleStructuralDocumentWithoutChangingIt()
        {
            string root = FixtureRoot();
            ProjectQualityCatalog catalog = CatalogFixture();
            ProjectInventorySnapshot snapshot = SnapshotFixture();
            IReadOnlyDictionary<string, string> files = ProjectDocumentationGenerator.RenderStructuralDocuments(catalog, snapshot);
            Directory.CreateDirectory(Path.Combine(root, "Docs"));
            File.WriteAllText(Path.Combine(root, "Docs", "guide.md"), "# 指南\n", new UTF8Encoding(false));
            ProjectDocumentationGenerator.WriteGeneratedFiles(root, files);
            string target = Path.Combine(root, "Docs/Generated/Project-Inventory-ZH.md");
            File.WriteAllText(target, "过期内容\n", new UTF8Encoding(false));
            byte[] before = File.ReadAllBytes(target);

            IReadOnlyList<ProjectQualityIssue> issues = ProjectQualityValidator.Validate(catalog, snapshot, root);

            Assert.That(issues.Any(issue => issue.Code == "PQ011" &&
                issue.Path == "Docs/Generated/Project-Inventory-ZH.md"), Is.True,
                string.Join("\n", issues.Select(issue => issue.Code + " " + issue.Path)));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(target));
        }

        private static string[] HashFiles(string root, IEnumerable<string> paths)
        {
            return paths.OrderBy(path => path, StringComparer.Ordinal).Select(path => path + " " +
                BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(Path.Combine(root, path))))
                    .Replace("-", string.Empty).ToLowerInvariant()).ToArray();
        }

        private static string FixtureRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "wastecity-project-documentation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static ProjectVerificationSnapshot VerificationFixture()
        {
            return new ProjectVerificationSnapshot
            {
                VerifiedCommitSha = "81b2f47d1688a72a7ddba36a2ffa04b1025e40f9",
                VerifiedAtIso8601 = "2026-08-12T12:00:00+08:00",
                EditMode = new ProjectTestRunSummary
                {
                    Total = 1121, Passed = 1121, Failed = 0, Skipped = 0, XmlPath = "task-03/editmode.xml",
                },
                PlayMode = new ProjectTestRunSummary
                {
                    Total = 82, Passed = 82, Failed = 0, Skipped = 0, XmlPath = "task-03/playmode.xml",
                },
                Compile = new ProjectCommandResult { Passed = true, EvidencePath = "task-03/compile.log" },
                Builds = new[]
                {
                    new ProjectCommandResult { Passed = true, EvidencePath = "task-03/windows-a.log" },
                    new ProjectCommandResult { Passed = true, EvidencePath = "task-03/windows-b.log" },
                    new ProjectCommandResult { Passed = true, EvidencePath = "task-03/windows-c.log" },
                },
                HumanPlaytestStatus = "等待用户复验",
            };
        }

        private static ProjectQualityCatalog CatalogFixture()
        {
            return new ProjectQualityCatalog
            {
                SchemaVersion = 1,
                FeatureGroups = new[]
                {
                    new ProjectFeatureGroup
                    {
                        Id = "feature", ChineseName = "功能", SourceGlobs = new[] { "Assets/_Game/Scripts/Feature/**" },
                        TestFileGlobs = new[]
                        {
                            "Assets/_Game/Tests/EditMode/FeatureTests.cs",
                            "Assets/_Game/Tests/PlayMode/RuntimeTests.cs",
                        },
                        ScenePaths = new[] { "Assets/_Game/Scenes/Feature.unity" }, RequirementIds = new[] { "DOC-0001" },
                        HumanDocumentPaths = new[] { "Docs/guide.md" }, MinimumVerification = ProjectVerificationLevel.FocusedEditMode,
                    },
                },
                ReuseEntries = new ProjectReuseEntry[0],
                Scenes = new[]
                {
                    new ProjectSceneEntry
                    {
                        Id = "feature-scene", ChineseName = "功能场景", Path = "Assets/_Game/Scenes/Feature.unity",
                        Purpose = "验证功能", EnabledInBuildSettings = true, ExpectedBuildIndex = 0,
                        ReuseLevel = ProjectReuseLevel.SceneOnly,
                    },
                },
                UiEntries = new[]
                {
                    new ProjectUiEntry
                    {
                        Id = "feature-ui", ChineseName = "功能界面", OwnerTypeName = "WasteCity.Feature.FeatureComponent",
                        SceneId = "feature-scene", InputPrioritySummary = "优先处理功能输入",
                        RequiredTestFiles = new[] { "Assets/_Game/Tests/EditMode/FeatureTests.cs" },
                    },
                },
                DocumentationRules = new[]
                {
                    new ProjectDocumentationRule
                    {
                        Id = "feature-docs", ChangedPathGlobs = new[] { "Assets/_Game/Scripts/Feature/**" },
                        ReviewDocumentPaths = new[] { "Docs/guide.md" }, PlainChineseReason = "功能变化需要检查说明",
                    },
                    new ProjectDocumentationRule
                    {
                        Id = "generated-project-quality-appendices", ChangedPathGlobs = new[] { "Docs/Engineering/**" },
                        ReviewDocumentPaths = new[]
                        {
                            ProjectDocumentationGenerator.ProjectInventoryPath,
                            ProjectDocumentationGenerator.TestInventoryPath,
                            ProjectDocumentationGenerator.VerificationPath,
                            ProjectDocumentationGenerator.AttentionPath,
                        },
                        PlainChineseReason = "自动附录需要重新生成",
                    },
                },
                ExplicitSourceExclusions = new ProjectPathExclusion[0],
                ExplicitTestExclusions = new ProjectPathExclusion[0],
            };
        }

        private static ProjectInventorySnapshot SnapshotFixture()
        {
            return new ProjectInventorySnapshot
            {
                FileRecords = new[]
                {
                    new ProjectFileRecord { Path = "Assets/_Game/Scripts/Feature/FeatureComponent.cs", Kind = ProjectFileKind.Production },
                    new ProjectFileRecord { Path = "Assets/_Game/Tests/EditMode/FeatureTests.cs", Kind = ProjectFileKind.EditModeTest },
                    new ProjectFileRecord { Path = "Assets/_Game/Tests/PlayMode/RuntimeTests.cs", Kind = ProjectFileKind.PlayModeTest },
                },
                TypeRecords = new[]
                {
                    new ProjectTypeRecord { FullName = "WasteCity.Feature.FeatureComponent", AssemblyName = "WasteCity.Game", SourcePath = "Assets/_Game/Scripts/Feature/FeatureComponent.cs", Kind = ProjectTypeKind.MonoBehaviour },
                    new ProjectTypeRecord { FullName = "WasteCity.Feature.FeatureAsset", AssemblyName = "WasteCity.Game", SourcePath = "Assets/_Game/Scripts/Feature/FeatureAsset.cs", Kind = ProjectTypeKind.ScriptableObject },
                },
                AssemblyRecords = new[] { new ProjectAssemblyRecord { Name = "WasteCity.Game", Path = "Assets/_Game/Scripts/WasteCity.Game.asmdef" } },
                SceneRecords = new[] { new ProjectSceneRecord { Path = "Assets/_Game/Scenes/Feature.unity", BuildIndex = 0 } },
                TestClasses = new[]
                {
                    new ProjectTestClassRecord { FullName = "WasteCity.Tests.FeatureTests", SourcePath = "Assets/_Game/Tests/EditMode/FeatureTests.cs", Platform = ProjectTestPlatform.EditMode },
                    new ProjectTestClassRecord { FullName = "WasteCity.Tests.RuntimeTests", SourcePath = "Assets/_Game/Tests/PlayMode/RuntimeTests.cs", Platform = ProjectTestPlatform.PlayMode },
                },
                EditorEntryPoints = new[] { new ProjectEditorEntryPointRecord { OwnerTypeFullName = "WasteCity.Editor.Build", MethodName = "Run" } },
                AssemblyNames = new[] { "WasteCity.Game" },
                ScenePaths = new[] { "Assets/_Game/Scenes/Feature.unity" },
            };
        }
    }
}
