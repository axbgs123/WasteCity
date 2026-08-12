using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Editor.ProjectQuality;

namespace WasteCity.Tests
{
    public sealed class ProjectTestResultAnalyzerTests
    {
        private string fixtureDirectory;

        [SetUp]
        public void SetUp()
        {
            fixtureDirectory = Path.Combine(Path.GetTempPath(), "wastecity-project-quality-task-05",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(fixtureDirectory)) Directory.Delete(fixtureDirectory, true);
        }

        [Test]
        public void Analyze_KnownFailureMapsFeatureFilesSceneRequirementAndRerun()
        {
            string xml = WriteNUnitXml(
                "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.MobileResearchStation_CanBePlacedInInnerCity",
                "Expected Valid but was InvalidCityMode",
                "at WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.cs:410");

            ProjectInventorySnapshot snapshot = CurrentSnapshot();
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(xml, CurrentCatalog(), snapshot);
            ProjectFailedTestLocation failed = report.Failures.Single();

            Assert.That(failed.FeatureGroupId, Is.EqualTo("building-construction-evacuation"));
            Assert.That(failed.FeatureGroupChineseName, Is.EqualTo("建筑建造与疏散"));
            Assert.That(failed.TestClassFullName, Is.EqualTo("WasteCity.Tests.GrayboxBuildingRuntimeSceneTests"));
            Assert.That(failed.RelatedFiles[0], Is.EqualTo("Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs"));
            Assert.That(failed.RelatedFiles.All(path => !path.Contains("*")), Is.True);
            Assert.That(failed.RelatedFiles.All(path => snapshot.FileRecords.Any(record => record.Path == path) ||
                snapshot.ScenePaths.Contains(path)), Is.True);
            CollectionAssert.Contains(failed.RequirementIds, "IDEA-0005");
            CollectionAssert.Contains(failed.ScenePaths, "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
            Assert.That(failed.RerunFilter, Is.EqualTo("-testFilter 'WasteCity.Tests.GrayboxBuildingRuntimeSceneTests'"));
            StringAssert.Contains("Expected Valid but was InvalidCityMode", failed.OriginalMessage);
        }

        [Test]
        public void Analyze_ExpandsPrimarySourcesInDeclaredOrderAndNeverReturnsGlobs()
        {
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteNUnitXml(
                "WasteCity.Tests.AlphaTests.Fails", "消息", "stack", "WasteCity.Tests.AlphaTests"),
                FixtureCatalog(), FixtureSnapshot());
            ProjectFailedTestLocation failed = report.Failures.Single();

            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/AlphaTests.cs",
                "Assets/_Game/Scripts/First/A.cs",
                "Assets/_Game/Scripts/First/Z.cs",
                "Assets/_Game/Scripts/Second/Only.cs",
                "Assets/_Game/Scripts/Reusable.cs",
                "Assets/_Game/Scenes/Feature.unity",
            }, failed.RelatedFiles);
            Assert.That(failed.RelatedFiles.All(path => !path.Contains("*")), Is.True);
            Assert.That(failed.RelatedFiles.All(path => FixturePaths().Contains(path)), Is.True);
        }

        [Test]
        public void Analyze_UnknownClassRetainsItsExactTestSourceAndDoesNotReturnGlobs()
        {
            ProjectInventorySnapshot snapshot = FixtureSnapshot();
            snapshot.TestClasses = snapshot.TestClasses.Concat(new[]
            {
                new ProjectTestClassRecord
                {
                    FullName = "WasteCity.Tests.UnknownTests",
                    SourcePath = "Assets/_Game/Tests/EditMode/UnknownTests.cs",
                    Platform = ProjectTestPlatform.EditMode,
                },
            }).ToArray();
            snapshot.FileRecords = snapshot.FileRecords.Concat(new[]
            {
                new ProjectFileRecord { Path = "Assets/_Game/Tests/EditMode/UnknownTests.cs", Kind = ProjectFileKind.EditModeTest },
            }).ToArray();

            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteNUnitXml(
                "WasteCity.Tests.UnknownTests.Fails", "消息", "stack", "WasteCity.Tests.UnknownTests"),
                FixtureCatalog(), snapshot);

            CollectionAssert.AreEqual(new[] { "Assets/_Game/Tests/EditMode/UnknownTests.cs" },
                report.Failures.Single().RelatedFiles);
            Assert.That(report.Issues.Select(issue => issue.Code), Is.EqualTo(new[] { "PQTEST001" }));
        }

        [TestCase("WasteCity.Tests.Namespace.AlphaTests", "WasteCity.Tests.Namespace.AlphaTests.Custom.Name.With.Dots", "WasteCity.Tests.Namespace.AlphaTests", "feature", false)]
        [TestCase("WasteCity.Tests.Outer+NestedTests", "WasteCity.Tests.Outer+NestedTests.Case.With.Dots", "WasteCity.Tests.Outer+NestedTests", "feature", false)]
        [TestCase("WasteCity.Tests.AlphaTests", "WasteCity.Tests.AlphaTests.Method(42)", "WasteCity.Tests.AlphaTests", "feature", false)]
        [TestCase("WasteCity.Tests.UnknownTests", "WasteCity.Tests.AlphaTests.Method", "WasteCity.Tests.UnknownTests", "未归类", true)]
        public void Analyze_UsesNUnitClassnameAsAuthoritativeIdentity(string className, string fullName,
            string expectedClass, string expectedFeature, bool expectsIssue)
        {
            ProjectInventorySnapshot snapshot = FixtureSnapshotWithClass(className);
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteNUnitXml(
                fullName, "消息", "stack", className), FixtureCatalog(), snapshot);
            ProjectFailedTestLocation failure = report.Failures.Single();

            Assert.That(failure.TestClassFullName, Is.EqualTo(expectedClass));
            Assert.That(failure.FeatureGroupId, Is.EqualTo(expectedFeature));
            Assert.That(failure.RerunFilter, Is.EqualTo("-testFilter '" + expectedClass + "'"));
            Assert.That(report.Issues.Any(issue => issue.Code == "PQTEST001"), Is.EqualTo(expectsIssue));
        }

        [Test]
        public void Analyze_FallsBackOnlyWhenClassnameIsMissingOrWhitespace()
        {
            string fullName = "WasteCity.Tests.AlphaTests.Method(one.two)";
            foreach (string classAttribute in new[] { string.Empty, "   " })
            {
                ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteNUnitXml(
                    fullName, "消息", "stack", classAttribute), FixtureCatalog(), FixtureSnapshot());
                ProjectFailedTestLocation failure = report.Failures.Single();
                Assert.That(failure.TestClassFullName, Is.EqualTo("WasteCity.Tests.AlphaTests"));
                Assert.That(failure.FeatureGroupId, Is.EqualTo("feature"));
                Assert.That(failure.RerunFilter, Is.EqualTo("-testFilter 'WasteCity.Tests.AlphaTests'"));
            }
        }

        [Test]
        public void Analyze_RerunFilterKeepsAnApostropheBearingClassAsOneShellArgument()
        {
            const string className = "WasteCity.Tests.O'BrienTests";
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteNUnitXml(
                "WasteCity.Tests.O'BrienTests.Custom.Name", "消息", "stack", className),
                FixtureCatalog(), FixtureSnapshotWithClass(className));

            Assert.That(report.Failures.Single().RerunFilter,
                Is.EqualTo("-testFilter 'WasteCity.Tests.O'\"'\"'BrienTests'"));
        }

        [Test]
        public void Analyze_UnknownFailureIsExplicitlyUnclassifiedAndNeverOmitted()
        {
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteNUnitXml(
                "WasteCity.Tests.FutureTests.NewFailure", "消息", "stack"), CurrentCatalog(), CurrentSnapshot());

            Assert.That(report.Failures, Has.Length.EqualTo(1));
            Assert.That(report.Failures[0].FeatureGroupId, Is.EqualTo("未归类"));
            Assert.That(report.Issues.Any(issue => issue.Code == "PQTEST001"), Is.True);
        }

        [Test]
        public void Analyze_MalformedXmlNamesTheLocalPath()
        {
            string path = Path.Combine(fixtureDirectory, "broken.xml");
            File.WriteAllText(path, "<test-run>");

            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                ProjectTestResultAnalyzer.Analyze(path, CurrentCatalog(), CurrentSnapshot()));

            StringAssert.Contains(path, error.Message);
        }

        [Test]
        public void Analyze_FailedRunWithoutTestCasesReportsIncompleteResult()
        {
            string path = WriteXml("<test-run result=\"Failed\" total=\"1\" failed=\"1\" />");

            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(path, CurrentCatalog(), CurrentSnapshot());

            Assert.That(report.IsIncomplete, Is.True);
            StringAssert.Contains("结果不完整", ProjectTestResultAnalyzer.RenderPlainChinese(report));
        }

        [Test]
        public void Analyze_SkippedTestIsCountedButNotReportedAsFailure()
        {
            string path = WriteXml("<test-run result=\"Passed\" total=\"2\" passed=\"1\" skipped=\"1\">" +
                "<test-case fullname=\"WasteCity.Tests.FutureTests.Skipped\" result=\"Skipped\" />" +
                "<test-case fullname=\"WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.Pass\" result=\"Passed\" />" +
                "</test-run>");

            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(path, CurrentCatalog(), CurrentSnapshot());

            Assert.That(report.Skipped, Is.EqualTo(1));
            Assert.That(report.Failures, Is.Empty);
        }

        [Test]
        public void Analyze_MultipleFailuresSortsByFullNameAndPreservesEntitiesAndMultilineStack()
        {
            string path = WriteXml("<test-run result=\"Failed\"><test-suite>" +
                "<test-case fullname=\"WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.Zeta\" result=\"Failed\">" +
                "<failure><message>a &amp; b</message><stack-trace>line one\nline two</stack-trace></failure></test-case>" +
                "<test-case fullname=\"WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.Alpha(&quot;one.two&quot;)\" result=\"Failed\">" +
                "<failure><message>&lt;expected&gt;</message><stack-trace>first\nsecond</stack-trace></failure></test-case>" +
                "</test-suite></test-run>");

            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(path, CurrentCatalog(), CurrentSnapshot());

            CollectionAssert.AreEqual(new[]
            {
                "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.Alpha(\"one.two\")",
                "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.Zeta",
            }, report.Failures.Select(failure => failure.FullName).ToArray());
            Assert.That(report.Failures[0].OriginalMessage, Is.EqualTo("<expected>"));
            Assert.That(report.Failures[1].OriginalMessage, Is.EqualTo("a & b"));
            Assert.That(report.Failures[0].OriginalStack, Is.EqualTo("first\nsecond"));
        }

        [Test]
        public void Analyze_RejectsRemoteAndExternalEntityInputsWithoutFetchingAnything()
        {
            Assert.That(() => ProjectTestResultAnalyzer.Analyze("https://example.test/results.xml", CurrentCatalog(), CurrentSnapshot()),
                Throws.TypeOf<InvalidDataException>());
            string path = WriteXml("<!DOCTYPE test-run [<!ENTITY remote SYSTEM \"http://example.test/entity\">]><test-run result=\"Passed\" />");
            Assert.That(() => ProjectTestResultAnalyzer.Analyze(path, CurrentCatalog(), CurrentSnapshot()),
                Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void RenderPlainChinese_UsesRequiredOrderAndDoesNotClaimRootCauseOrUnprovenFix()
        {
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteNUnitXml(
                "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests.Fails", "消息", "stack"), CurrentCatalog(), CurrentSnapshot());

            string text = ProjectTestResultAnalyzer.RenderPlainChinese(report);
            string[] headings = { "测试结果摘要", "问题区域", "失败位置", "失败测试", "优先检查", "相关文件", "相关场景", "相关需求", "建议复跑", "原始错误", "原始堆栈" };
            for (int index = 1; index < headings.Length; index++)
                Assert.That(text.IndexOf(headings[index - 1], StringComparison.Ordinal), Is.LessThan(text.IndexOf(headings[index], StringComparison.Ordinal)));
            StringAssert.DoesNotContain("根因已确定", text);
            StringAssert.DoesNotContain("修复为", text);
            StringAssert.Contains("仅作为排查起点", text);
        }

        [Test]
        public void RenderPlainChinese_UsesChineseAreaAndFactualLocationForEveryFailureBlock()
        {
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(WriteXml("<test-run result=\"Failed\">" +
                FailedCase("WasteCity.Tests.AlphaTests.Zeta", "第一行\n第二行", "stack-z", "WasteCity.Tests.AlphaTests") +
                FailedCase("WasteCity.Tests.UnknownTests.Alpha", "未知", "stack-u", "WasteCity.Tests.UnknownTests") +
                "</test-run>"), FixtureCatalog(), FixtureSnapshotWithClass("WasteCity.Tests.UnknownTests"));

            string text = ProjectTestResultAnalyzer.RenderPlainChinese(report);
            StringAssert.Contains("问题区域\n- 功能甲", text);
            StringAssert.Contains("问题区域\n- 未归类", text);
            StringAssert.DoesNotContain("- feature\n", text);
            StringAssert.Contains("失败位置\n- Assets/_Game/Tests/EditMode/AlphaTests.cs：WasteCity.Tests.AlphaTests.Zeta", text);
            StringAssert.Contains("优先检查\n- 先检查第一项；仅作为排查起点，尚待证据确认。", text);
            StringAssert.Contains("原始错误\n第一行\n第二行", text);
            Assert.That(Count(text, "问题区域"), Is.EqualTo(2));
            Assert.That(Count(text, "原始堆栈"), Is.EqualTo(2));
        }

        private string WriteNUnitXml(string fullName, string message, string stack, string className = null)
        {
            return WriteXml("<test-run result=\"Failed\" total=\"1\" failed=\"1\">" +
                FailedCase(fullName, message, stack, className) + "</test-run>");
        }

        private static string FailedCase(string fullName, string message, string stack, string className = null)
        {
            string classAttribute = className == null ? string.Empty : " classname=\"" + className + "\"";
            return "<test-case fullname=\"" + fullName + "\"" + classAttribute + " result=\"Failed\"><failure>" +
                "<message><![CDATA[" + message + "]]></message><stack-trace><![CDATA[" + stack +
                "]]></stack-trace></failure></test-case>";
        }

        private string WriteXml(string xml)
        {
            string path = Path.Combine(fixtureDirectory, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, xml);
            return path;
        }

        private static ProjectQualityCatalog CurrentCatalog()
        {
            return ProjectQualityCatalogLoader.LoadFromFile(Path.Combine(ProjectRoot(),
                "Docs/Engineering/project-quality-catalog.json"));
        }

        private static ProjectInventorySnapshot CurrentSnapshot()
        {
            return ProjectQualityScanner.Scan(ProjectRoot());
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static ProjectQualityCatalog FixtureCatalog()
        {
            return new ProjectQualityCatalog
            {
                FeatureGroups = new[]
                {
                    new ProjectFeatureGroup
                    {
                        Id = "feature", ChineseName = "功能甲",
                        PrimarySourceGlobs = new[] { "Assets/_Game/Scripts/First/**", "Assets/_Game/Scripts/Second/*.cs" },
                        FailureLocationSummary = "先检查第一项",
                        TestFileGlobs = new[] { "Assets/_Game/Tests/EditMode/AlphaTests.cs" },
                        ScenePaths = new[] { "Assets/_Game/Scenes/Feature.unity" },
                        RequirementIds = new[] { "DOC-0001" },
                    },
                },
                ReuseEntries = new[]
                {
                    new ProjectReuseEntry { FeatureGroupId = "feature", AssetPaths = new[] { "Assets/_Game/Scripts/Reusable.cs" } },
                },
                UiEntries = new ProjectUiEntry[0],
            };
        }

        private static ProjectInventorySnapshot FixtureSnapshot()
        {
            return new ProjectInventorySnapshot
            {
                FileRecords = new[]
                {
                    new ProjectFileRecord { Path = "Assets/_Game/Scripts/First/Z.cs", Kind = ProjectFileKind.Production },
                    new ProjectFileRecord { Path = "Assets/_Game/Scripts/First/A.cs", Kind = ProjectFileKind.Production },
                    new ProjectFileRecord { Path = "Assets/_Game/Scripts/Second/Only.cs", Kind = ProjectFileKind.Production },
                    new ProjectFileRecord { Path = "Assets/_Game/Scripts/Reusable.cs", Kind = ProjectFileKind.Production },
                    new ProjectFileRecord { Path = "Assets/_Game/Tests/EditMode/AlphaTests.cs", Kind = ProjectFileKind.EditModeTest },
                },
                TypeRecords = new ProjectTypeRecord[0],
                TestClasses = new[]
                {
                    new ProjectTestClassRecord { FullName = "WasteCity.Tests.AlphaTests", SourcePath = "Assets/_Game/Tests/EditMode/AlphaTests.cs", Platform = ProjectTestPlatform.EditMode },
                },
                ScenePaths = new[] { "Assets/_Game/Scenes/Feature.unity" },
            };
        }

        private static ProjectInventorySnapshot FixtureSnapshotWithClass(string fullName)
        {
            ProjectInventorySnapshot snapshot = FixtureSnapshot();
            if (fullName == "WasteCity.Tests.AlphaTests") return snapshot;
            string sourcePath = fullName == "WasteCity.Tests.UnknownTests"
                ? "Assets/_Game/Tests/EditMode/UnknownTests.cs"
                : "Assets/_Game/Tests/EditMode/AlphaTests.cs";
            snapshot.TestClasses = snapshot.TestClasses.Concat(new[]
            {
                new ProjectTestClassRecord { FullName = fullName, SourcePath = sourcePath, Platform = ProjectTestPlatform.EditMode },
            }).ToArray();
            if (sourcePath != "Assets/_Game/Tests/EditMode/AlphaTests.cs")
                snapshot.FileRecords = snapshot.FileRecords.Concat(new[]
                {
                    new ProjectFileRecord { Path = sourcePath, Kind = ProjectFileKind.EditModeTest },
                }).ToArray();
            return snapshot;
        }

        private static string[] FixturePaths()
        {
            return new[]
            {
                "Assets/_Game/Tests/EditMode/AlphaTests.cs", "Assets/_Game/Scripts/First/A.cs",
                "Assets/_Game/Scripts/First/Z.cs", "Assets/_Game/Scripts/Second/Only.cs",
                "Assets/_Game/Scripts/Reusable.cs", "Assets/_Game/Scenes/Feature.unity",
            };
        }

        private static int Count(string value, string fragment)
        {
            int count = 0;
            for (int index = 0; (index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0; index += fragment.Length)
                count++;
            return count;
        }
    }
}
