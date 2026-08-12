using System;
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

            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(xml, CurrentCatalog(), CurrentSnapshot());
            ProjectFailedTestLocation failed = report.Failures.Single();

            Assert.That(failed.FeatureGroupId, Is.EqualTo("building-construction-evacuation"));
            CollectionAssert.Contains(failed.RequirementIds, "IDEA-0005");
            CollectionAssert.Contains(failed.ScenePaths, "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
            StringAssert.Contains("GrayboxBuildingRuntimeSceneTests", failed.RerunFilter);
            StringAssert.Contains("Expected Valid but was InvalidCityMode", failed.OriginalMessage);
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

        private string WriteNUnitXml(string fullName, string message, string stack)
        {
            return WriteXml("<test-run result=\"Failed\" total=\"1\" failed=\"1\">" +
                FailedCase(fullName, message, stack) + "</test-run>");
        }

        private static string FailedCase(string fullName, string message, string stack)
        {
            return "<test-case fullname=\"" + fullName + "\" result=\"Failed\"><failure>" +
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
    }
}
