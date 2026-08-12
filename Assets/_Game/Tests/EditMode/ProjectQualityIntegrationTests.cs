using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Editor.ProjectQuality;

namespace WasteCity.Tests
{
    public sealed class ProjectQualityIntegrationTests
    {
        private static readonly string[] EnvironmentNames =
        {
            "WASTECITY_QUALITY_TEST_RESULTS", "WASTECITY_QUALITY_ANALYSIS_OUTPUT",
            "WASTECITY_QUALITY_VERIFIED_SHA", "WASTECITY_QUALITY_VERIFIED_AT",
            "WASTECITY_QUALITY_EDITMODE_RESULTS", "WASTECITY_QUALITY_PLAYMODE_RESULTS",
            "WASTECITY_QUALITY_COMPILE_LOG", "WASTECITY_QUALITY_BUILD_SUMMARY",
            "WASTECITY_QUALITY_HUMAN_PLAYTEST", "WASTECITY_QUALITY_CHANGED_PATHS",
        };

        private readonly Dictionary<string, string> savedEnvironment = new Dictionary<string, string>();
        private string fixtureDirectory;

        [SetUp]
        public void SetUp()
        {
            fixtureDirectory = Path.Combine(Path.GetTempPath(), "wastecity-project-quality-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureDirectory);
            foreach (string name in EnvironmentNames)
            {
                savedEnvironment[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (KeyValuePair<string, string> value in savedEnvironment)
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            if (Directory.Exists(fixtureDirectory)) Directory.Delete(fixtureDirectory, true);
        }

        [Test]
        public void PublicBatchMethods_AreStaticParameterlessVoid()
        {
            foreach (string name in new[]
            {
                "GenerateDocumentation", "ValidateDocumentation", "AnalyzeTestResults", "RecordVerification",
            })
            {
                MethodInfo method = typeof(ProjectQualityTools).GetMethod(name,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                Assert.That(method, Is.Not.Null, name + " must be public static");
                Assert.That(method.ReturnType, Is.EqualTo(typeof(void)), name + " must return void");
                Assert.That(method.GetParameters(), Is.Empty, name + " must be parameterless");
            }
        }

        [Test]
        public void MenuAndBatchSource_UseOnlyTheRequiredSafeEntryPoints()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot(),
                "Assets/_Game/Editor/ProjectQuality/ProjectQualityTools.cs"));
            foreach (string name in new[] { "Generate Documentation", "Validate Documentation", "Analyze Test Results", "Record Verification" })
                StringAssert.Contains("WasteCity/Project Quality/" + name, source);
            foreach (string environment in EnvironmentNames) StringAssert.Contains(environment, source);
            StringAssert.DoesNotContain("git", source.ToLowerInvariant());
            StringAssert.DoesNotContain("process.", source.ToLowerInvariant());
        }

        [Test]
        public void AnalyzeTestResults_MissingRequiredVariablesNamesTheMissingVariables()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ProjectQualityTools.AnalyzeTestResults());

            StringAssert.Contains("WASTECITY_QUALITY_TEST_RESULTS", error.Message);
            StringAssert.Contains("WASTECITY_QUALITY_ANALYSIS_OUTPUT", error.Message);
        }

        [Test]
        public void AnalyzeTestResults_RejectsEveryProjectPathAndLeavesRepositoryBytesUnchanged()
        {
            string results = Write("result.xml", "<test-run result=\"Passed\" total=\"1\" passed=\"1\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Pass\" result=\"Passed\" /></test-run>");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_TEST_RESULTS", results);
            foreach (string rejectedOutput in new[]
            {
                Path.Combine(ProjectRoot(), "Assets", "quality-analysis-rejected.txt"),
                Path.Combine(ProjectRoot(), "Packages", "quality-analysis-rejected.txt"),
                Path.Combine(ProjectRoot(), "ProjectSettings", "quality-analysis-rejected.txt"),
                Path.Combine(ProjectRoot(), "Docs", "Generated", "Latest-Verification-ZH.md"),
                Path.Combine(ProjectRoot(), "Docs", "Engineering", "project-quality-catalog.json"),
                Path.Combine(ProjectRoot(), "README.md"),
            })
            {
                Dictionary<string, string> before = HashRepositoryFiles(ProjectRoot());
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT", rejectedOutput);
                bool existed = File.Exists(rejectedOutput);
                byte[] original = existed ? File.ReadAllBytes(rejectedOutput) : null;
                try
                {
                    InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                        () => ProjectQualityTools.AnalyzeTestResults());
                    StringAssert.Contains("WASTECITY_QUALITY_ANALYSIS_OUTPUT", error.Message);
                }
                finally
                {
                    RestoreExactFile(rejectedOutput, existed, original);
                }
                CollectionAssert.IsEmpty(ChangedPaths(before, HashRepositoryFiles(ProjectRoot())));
            }

            string output = Path.Combine(fixtureDirectory, "analysis.txt");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT", output);
            ProjectQualityTools.AnalyzeTestResults();
            Assert.That(File.Exists(output), Is.True);
            StringAssert.Contains("测试结果摘要", File.ReadAllText(output));
        }

        [Test]
        public void AnalyzeTestResults_RejectsReparsePointAncestorsWithoutWritingProtectedRoots()
        {
            string results = Write("result.xml", StandardNUnitXml("Passed", 1, 1, 0, 0));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_TEST_RESULTS", results);
            foreach (string protectedRoot in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string link = Path.Combine(fixtureDirectory, "link-" + protectedRoot);
                if (CreateSymbolicLink(Path.Combine(ProjectRoot(), protectedRoot), link) != 0)
                    Assert.Ignore("symbolic links are unavailable on this platform");
                string target = Path.Combine(link, "quality-analysis-" + Guid.NewGuid().ToString("N") + ".txt");
                Dictionary<string, string> before = HashRepositoryFiles(ProjectRoot());
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT", target);

                try
                {
                    Assert.Throws<InvalidOperationException>(() => ProjectQualityTools.AnalyzeTestResults());
                    Assert.That(File.Exists(target), Is.False);
                }
                finally
                {
                    if (File.Exists(target)) File.Delete(target);
                }
                CollectionAssert.IsEmpty(ChangedPaths(before, HashRepositoryFiles(ProjectRoot())));
            }
        }

        [Test]
        public void RecordVerification_MissingEveryExplicitValueNamesEachVariable()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ProjectQualityTools.RecordVerification());

            foreach (string name in EnvironmentNames.Where(name => name != "WASTECITY_QUALITY_TEST_RESULTS" &&
                name != "WASTECITY_QUALITY_ANALYSIS_OUTPUT" && name != "WASTECITY_QUALITY_CHANGED_PATHS"))
                StringAssert.Contains(name, error.Message);
        }

        [Test]
        public void RecordVerification_ParsesSuppliedEvidenceAndOnlyChangesLatestVerification()
        {
            string verification = Path.Combine(ProjectRoot(), ProjectDocumentationGenerator.VerificationPath);
            string original = File.ReadAllText(verification);
            Dictionary<string, string> before = HashFiles(ProjectRoot(), true);
            try
            {
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_SHA", "0123456789abcdef0123456789abcdef01234567");
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_AT", "2026-08-12T12:00:00+08:00");
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_EDITMODE_RESULTS", Write("edit.xml",
                    StandardNUnitXml("Passed", 2, 2, 0, 0)));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_PLAYMODE_RESULTS", Write("play.xml",
                    StandardNUnitXml("Passed", 1, 1, 0, 0)));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_COMPILE_LOG", Write("compile.log", "Compilation succeeded\n"));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_BUILD_SUMMARY", Write("builds.json",
                    "{\"Builds\":[{\"Name\":\"Windows 3D\",\"Status\":\"NotRequired\",\"EvidenceLogPath\":\"" +
                    EscapeJson(Write("build.log", "runtime inputs unchanged\n")) + "\",\"Reason\":\"runtime/build inputs unchanged\"}]}"));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_HUMAN_PLAYTEST", "未进行");

                ProjectQualityTools.RecordVerification();

                StringAssert.Contains("2/2", File.ReadAllText(verification));
                Dictionary<string, string> after = HashFiles(ProjectRoot(), true);
                CollectionAssert.AreEquivalent(new[] { ProjectDocumentationGenerator.VerificationPath },
                    ChangedPaths(before, after));
            }
            finally
            {
                File.WriteAllText(verification, original, new UTF8Encoding(false));
            }
        }

        [Test]
        public void RecordVerification_RejectsFailedTestEvidenceWithoutChangingLatestVerification()
        {
            ConfigureValidRecordEvidence(StandardNUnitXml("Failed", 1, 0, 1, 0));
            AssertRecordRejectedWithoutWrite();
        }

        [Test]
        public void RecordVerification_RejectsFailedBuildWithoutChangingLatestVerification()
        {
            ConfigureValidRecordEvidence(StandardNUnitXml("Passed", 1, 1, 0, 0), "Failed");
            AssertRecordRejectedWithoutWrite();
        }

        [Test]
        public void RecordVerification_RejectsZeroCaseClaimWithoutChangingLatestVerification()
        {
            ConfigureValidRecordEvidence("<test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"0\" />");
            AssertRecordRejectedWithoutWrite();
        }

        [TestCase("<test-run result=\"Passed\" total=\"2\" passed=\"2\" failed=\"0\" skipped=\"0\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Pass\" result=\"Passed\" /></test-run>")]
        [TestCase("<test-run result=\"Passed\" total=\"1\" passed=\"0\" failed=\"0\" skipped=\"1\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Pass\" result=\"Passed\" /></test-run>")]
        [TestCase("<test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"0\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Skip\" result=\"Skipped\" /></test-run>")]
        public void RecordVerification_RejectsSummaryMismatchWithoutChangingLatestVerification(string xml)
        {
            ConfigureValidRecordEvidence(xml);
            AssertRecordRejectedWithoutWrite();
        }

        [Test]
        public void GenerateThenValidate_ChangesOnlyGeneratedFilesAndUsesNoImplicitChangedPaths()
        {
            Dictionary<string, string> before = HashFiles(ProjectRoot(), false);
            ProjectQualityTools.GenerateDocumentation();
            Dictionary<string, string> afterGeneration = HashFiles(ProjectRoot(), false);
            Assert.That(ChangedPaths(before, afterGeneration).All(path => path.StartsWith("Docs/Generated/", StringComparison.Ordinal)), Is.True);
            StringAssert.Contains("当前没有待处理的路径提醒", File.ReadAllText(Path.Combine(ProjectRoot(),
                ProjectDocumentationGenerator.AttentionPath)));

            Dictionary<string, string> beforeValidation = HashFiles(ProjectRoot(), true);
            ProjectQualityTools.ValidateDocumentation();
            CollectionAssert.IsEmpty(ChangedPaths(beforeValidation, HashFiles(ProjectRoot(), true)));
        }

        private string Write(string name, string content)
        {
            string path = Path.Combine(fixtureDirectory, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        private void ConfigureValidRecordEvidence(string editXml, string buildStatus = "NotRequired")
        {
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_SHA", "0123456789abcdef0123456789abcdef01234567");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_VERIFIED_AT", "2026-08-12T12:00:00+08:00");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_EDITMODE_RESULTS", Write("edit.xml", editXml));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_PLAYMODE_RESULTS", Write("play.xml",
                StandardNUnitXml("Passed", 1, 1, 0, 0)));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_COMPILE_LOG", Write("compile.log", "Compilation succeeded\n"));
            string reason = buildStatus == "NotRequired" ? ",\"Reason\":\"runtime/build inputs unchanged\"" : string.Empty;
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_BUILD_SUMMARY", Write("builds.json",
                "{\"Builds\":[{\"Name\":\"Windows 3D\",\"Status\":\"" + buildStatus + "\",\"EvidenceLogPath\":\"" +
                EscapeJson(Write("build.log", "build evidence\n")) + "\"" + reason + "}]}"));
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_HUMAN_PLAYTEST", "未进行");
        }

        private static void AssertRecordRejectedWithoutWrite()
        {
            string verification = Path.Combine(ProjectRoot(), ProjectDocumentationGenerator.VerificationPath);
            byte[] before = File.ReadAllBytes(verification);
            try
            {
                Assert.Throws<InvalidOperationException>(() => ProjectQualityTools.RecordVerification());
            }
            finally
            {
                File.WriteAllBytes(verification, before);
            }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(verification));
        }

        private static void RestoreExactFile(string path, bool existed, byte[] bytes)
        {
            if (existed) File.WriteAllBytes(path, bytes);
            else if (File.Exists(path)) File.Delete(path);
        }

        private static string StandardNUnitXml(string runResult, int total, int passed, int failed, int skipped)
        {
            var cases = new StringBuilder();
            int index = 0;
            AppendCases(cases, "Passed", passed, ref index);
            AppendCases(cases, "Failed", failed, ref index);
            AppendCases(cases, "Skipped", skipped, ref index);
            return "<test-run result=\"" + runResult + "\" total=\"" + total + "\" passed=\"" + passed +
                "\" failed=\"" + failed + "\" skipped=\"" + skipped + "\"><test-suite>" + cases + "</test-suite></test-run>";
        }

        private static void AppendCases(StringBuilder builder, string result, int count, ref int index)
        {
            for (int value = 0; value < count; value++)
            {
                index++;
                builder.Append("<test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Case")
                    .Append(index).Append("\" result=\"").Append(result).Append("\"");
                if (result == "Failed") builder.Append("><failure><message>failed</message></failure></test-case>");
                else builder.Append(" />");
            }
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static Dictionary<string, string> HashFiles(string root, bool includeGenerated)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string directory in new[] { "Assets", "Packages", "ProjectSettings", "Docs" })
            {
                string fullDirectory = Path.Combine(root, directory);
                if (!Directory.Exists(fullDirectory)) continue;
                foreach (string path in Directory.EnumerateFiles(fullDirectory, "*", SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    if (!includeGenerated && relative.StartsWith("Docs/Generated/", StringComparison.Ordinal)) continue;
                    using (SHA256 hash = SHA256.Create())
                        values[relative] = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));
                }
            }
            return values;
        }

        private static Dictionary<string, string> HashRepositoryFiles(string root)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Concat(new[] { "Assets", "Packages", "ProjectSettings", "Docs" }.SelectMany(directory =>
                {
                    string fullDirectory = Path.Combine(root, directory);
                    return Directory.Exists(fullDirectory)
                        ? Directory.EnumerateFiles(fullDirectory, "*", SearchOption.AllDirectories)
                        : Enumerable.Empty<string>();
                })))
            {
                string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                using (SHA256 hash = SHA256.Create())
                    values[relative] = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));
            }
            return values;
        }

        private static string[] ChangedPaths(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            return before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal)
                .Where(path => !before.ContainsKey(path) || !after.ContainsKey(path) || before[path] != after[path])
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        [DllImport("libc", EntryPoint = "symlink", SetLastError = true)]
        private static extern int CreateSymbolicLink(string target, string linkPath);
    }
}
