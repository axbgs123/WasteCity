using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public void AnalyzeTestResults_RejectsOutputInsideAssetsAndWritesOnlyExplicitOutput()
        {
            string results = Write("result.xml", "<test-run result=\"Passed\" total=\"1\" passed=\"1\"><test-case fullname=\"WasteCity.Tests.ProjectQualityIntegrationTests.Pass\" result=\"Passed\" /></test-run>");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_TEST_RESULTS", results);
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT",
                Path.Combine(ProjectRoot(), "Assets", "analysis.txt"));
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ProjectQualityTools.AnalyzeTestResults());
            StringAssert.Contains("WASTECITY_QUALITY_ANALYSIS_OUTPUT", error.Message);

            string output = Path.Combine(fixtureDirectory, "analysis.txt");
            Environment.SetEnvironmentVariable("WASTECITY_QUALITY_ANALYSIS_OUTPUT", output);
            ProjectQualityTools.AnalyzeTestResults();
            Assert.That(File.Exists(output), Is.True);
            StringAssert.Contains("测试结果摘要", File.ReadAllText(output));
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
                    "<test-run result=\"Passed\" total=\"2\" passed=\"2\" />"));
                Environment.SetEnvironmentVariable("WASTECITY_QUALITY_PLAYMODE_RESULTS", Write("play.xml",
                    "<test-run result=\"Passed\" total=\"1\" passed=\"1\" />"));
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
    }
}
