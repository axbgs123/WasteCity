using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor.ProjectQuality
{
    public static class ProjectQualityTools
    {
        public const string CatalogPath = "Docs/Engineering/project-quality-catalog.json";
        public const string GeneratedRoot = "Docs/Generated";
        public const string TestResultsEnvironment = "WASTECITY_QUALITY_TEST_RESULTS";
        public const string AnalysisOutputEnvironment = "WASTECITY_QUALITY_ANALYSIS_OUTPUT";
        public const string VerifiedShaEnvironment = "WASTECITY_QUALITY_VERIFIED_SHA";
        public const string VerifiedAtEnvironment = "WASTECITY_QUALITY_VERIFIED_AT";
        public const string EditModeResultsEnvironment = "WASTECITY_QUALITY_EDITMODE_RESULTS";
        public const string PlayModeResultsEnvironment = "WASTECITY_QUALITY_PLAYMODE_RESULTS";
        public const string CompileLogEnvironment = "WASTECITY_QUALITY_COMPILE_LOG";
        public const string BuildSummaryEnvironment = "WASTECITY_QUALITY_BUILD_SUMMARY";
        public const string HumanPlaytestEnvironment = "WASTECITY_QUALITY_HUMAN_PLAYTEST";
        public const string ChangedPathsEnvironment = "WASTECITY_QUALITY_CHANGED_PATHS";

        [MenuItem("WasteCity/Project Quality/Generate Documentation")]
        public static void GenerateDocumentation()
        {
            string root = ProjectRoot();
            ProjectQualityCatalog catalog = LoadCatalog(root);
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(root);
            ThrowIfIssues(ProjectQualityValidator.Validate(catalog, snapshot, root, false));

            IReadOnlyDictionary<string, string> structural =
                ProjectDocumentationGenerator.RenderStructuralDocuments(catalog, snapshot);
            var files = new Dictionary<string, string>(structural, StringComparer.Ordinal)
            {
                { ProjectDocumentationGenerator.AttentionPath,
                    ProjectDocumentationGenerator.RenderDocumentationAttention(catalog, ReadChangedPaths(root)) },
                { ProjectDocumentationGenerator.VerificationPath, ReadExistingVerification(root) },
            };
            ProjectDocumentationGenerator.WriteGeneratedFiles(root, files);
            ValidateDocumentation();
        }

        [MenuItem("WasteCity/Project Quality/Validate Documentation")]
        public static void ValidateDocumentation()
        {
            string root = ProjectRoot();
            ProjectQualityCatalog catalog = LoadCatalog(root);
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(root);
            ThrowIfIssues(ProjectQualityValidator.Validate(catalog, snapshot, root));
        }

        [MenuItem("WasteCity/Project Quality/Analyze Test Results")]
        public static void AnalyzeTestResults()
        {
            string[] values = RequireEnvironment(TestResultsEnvironment, AnalysisOutputEnvironment);
            string root = ProjectRoot();
            string output = RequireAnalysisOutput(values[1], root);
            ProjectTestAnalysisReport report = ProjectTestResultAnalyzer.Analyze(values[0], LoadCatalog(root),
                ProjectQualityScanner.Scan(root));
            WriteText(output, ProjectTestResultAnalyzer.RenderPlainChinese(report));
        }

        [MenuItem("WasteCity/Project Quality/Record Verification")]
        public static void RecordVerification()
        {
            string[] values = RequireEnvironment(VerifiedShaEnvironment, VerifiedAtEnvironment,
                EditModeResultsEnvironment, PlayModeResultsEnvironment, CompileLogEnvironment,
                BuildSummaryEnvironment, HumanPlaytestEnvironment);
            string root = ProjectRoot();
            ProjectQualityCatalog catalog = LoadCatalog(root);
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(root);
            ProjectTestAnalysisReport edit = ProjectTestResultAnalyzer.Analyze(values[2], catalog, snapshot);
            ProjectTestAnalysisReport play = ProjectTestResultAnalyzer.Analyze(values[3], catalog, snapshot);
            if (edit.IsIncomplete || play.IsIncomplete)
                throw new InvalidOperationException("测试 XML 结果不完整，不能记录验证");

            var verification = new ProjectVerificationSnapshot
            {
                VerifiedCommitSha = values[0],
                VerifiedAtIso8601 = values[1],
                EditMode = TestSummary(edit),
                PlayMode = TestSummary(play),
                Compile = ReadCompileResult(values[4]),
                Builds = ReadBuildSummary(values[5]),
                HumanPlaytestStatus = values[6],
            };
            ProjectDocumentationGenerator.WriteVerificationFile(root,
                ProjectDocumentationGenerator.RenderVerification(verification));
        }

        private static ProjectQualityCatalog LoadCatalog(string root)
        {
            return ProjectQualityCatalogLoader.LoadFromFile(Path.Combine(root,
                CatalogPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void ThrowIfIssues(IReadOnlyList<ProjectQualityIssue> issues)
        {
            if (issues == null || issues.Count == 0) return;
            foreach (ProjectQualityIssue issue in issues)
                Debug.LogError("[ProjectQuality:" + issue.Code + "] " + issue.PlainChineseMessage + " " + issue.Path);
            throw new InvalidOperationException("Project quality validation failed with " + issues.Count + " issues.");
        }

        private static string ReadExistingVerification(string root)
        {
            string path = Path.Combine(root, ProjectDocumentationGenerator.VerificationPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new InvalidOperationException("缺少既有验证记录：" + path);
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static string[] ReadChangedPaths(string root)
        {
            string value = Environment.GetEnvironmentVariable(ChangedPathsEnvironment);
            if (string.IsNullOrWhiteSpace(value)) return new string[0];
            string path = RequireLocalExistingFile(value, ChangedPathsEnvironment);
            var paths = new List<string>();
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string relative = line.Trim().Replace('\\', '/');
                if (relative.Length == 0) continue;
                if (Path.IsPathRooted(relative) || relative.StartsWith("../", StringComparison.Ordinal) ||
                    relative == ".." || relative.IndexOf("/../", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(ChangedPathsEnvironment + " contains a non-relative path: " + line);
                string candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidOperationException(ChangedPathsEnvironment + " escapes the project: " + line);
                paths.Add(relative);
            }
            return paths.ToArray();
        }

        private static string[] RequireEnvironment(params string[] names)
        {
            var missing = new List<string>();
            var values = new string[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                values[index] = Environment.GetEnvironmentVariable(names[index]);
                if (string.IsNullOrWhiteSpace(values[index])) missing.Add(names[index]);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("Missing required environment variables: " + string.Join(", ", missing.ToArray()));
            return values;
        }

        private static string RequireAnalysisOutput(string value, string root)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value) ||
                Uri.IsWellFormedUriString(value, UriKind.Absolute))
                throw new InvalidOperationException(AnalysisOutputEnvironment + " must be a local absolute output path");
            string output = Path.GetFullPath(value);
            foreach (string protectedDirectory in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string protectedRoot = Path.GetFullPath(Path.Combine(root, protectedDirectory));
                if (output == protectedRoot || output.StartsWith(protectedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidOperationException(AnalysisOutputEnvironment + " must not write under " + protectedDirectory);
            }
            return output;
        }

        private static string RequireLocalExistingFile(string value, string environment)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value) ||
                Uri.IsWellFormedUriString(value, UriKind.Absolute))
                throw new InvalidOperationException(environment + " must be a local absolute path");
            string path = Path.GetFullPath(value);
            if (!File.Exists(path)) throw new InvalidOperationException(environment + " does not exist: " + path);
            return path;
        }

        private static void WriteText(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("输出目录无效：" + path);
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, content ?? string.Empty, new UTF8Encoding(false));
        }

        private static ProjectTestRunSummary TestSummary(ProjectTestAnalysisReport report)
        {
            return new ProjectTestRunSummary
            {
                Total = report.Total,
                Passed = report.Passed,
                Failed = report.Failed,
                Skipped = report.Skipped,
                XmlPath = report.XmlPath,
            };
        }

        private static ProjectCommandResult ReadCompileResult(string value)
        {
            string path = RequireLocalExistingFile(value, CompileLogEnvironment);
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException(CompileLogEnvironment + " is empty");
            bool passed = text.IndexOf("Compilation succeeded", StringComparison.OrdinalIgnoreCase) >= 0 ||
                Regex.IsMatch(text, @"\berrors?\s*:\s*0\b", RegexOptions.IgnoreCase);
            if (!passed) throw new InvalidOperationException(CompileLogEnvironment + " has no successful compilation summary");
            return new ProjectCommandResult { Passed = true, EvidencePath = path };
        }

        private static ProjectCommandResult[] ReadBuildSummary(string value)
        {
            string path = RequireLocalExistingFile(value, BuildSummaryEnvironment);
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json) || Regex.Matches(json, "\\\"Builds\\\"\\s*:").Count != 1)
                throw new InvalidOperationException(BuildSummaryEnvironment + " must contain one Builds array");
            foreach (Match name in Regex.Matches(json, "\\\"([^\\\"]+)\\\"\\s*:"))
                if (name.Groups[1].Value != "Builds" && name.Groups[1].Value != "Name" &&
                    name.Groups[1].Value != "Status" && name.Groups[1].Value != "EvidenceLogPath" &&
                    name.Groups[1].Value != "Reason")
                    throw new InvalidOperationException(BuildSummaryEnvironment + " has an unsupported field: " + name.Groups[1].Value);

            BuildSummaryFile parsed;
            try { parsed = JsonUtility.FromJson<BuildSummaryFile>(json); }
            catch (ArgumentException exception) { throw new InvalidOperationException(BuildSummaryEnvironment + " is not valid JSON", exception); }
            if (parsed == null || parsed.Builds == null || parsed.Builds.Length == 0)
                throw new InvalidOperationException(BuildSummaryEnvironment + " must contain at least one build");

            var results = new List<ProjectCommandResult>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuildSummaryEntry build in parsed.Builds)
            {
                if (build == null || string.IsNullOrWhiteSpace(build.Name) || !names.Add(build.Name) ||
                    (build.Status != "Succeeded" && build.Status != "Failed" && build.Status != "NotRequired"))
                    throw new InvalidOperationException(BuildSummaryEnvironment + " has an invalid build name or status");
                string evidence = RequireLocalExistingFile(build.EvidenceLogPath, BuildSummaryEnvironment);
                if (build.Status == "NotRequired" && string.IsNullOrWhiteSpace(build.Reason))
                    throw new InvalidOperationException(BuildSummaryEnvironment + " NotRequired builds require Reason");
                if (build.Status != "NotRequired" && !string.IsNullOrEmpty(build.Reason))
                    throw new InvalidOperationException(BuildSummaryEnvironment + " Reason is only valid for NotRequired builds");
                results.Add(new ProjectCommandResult { Passed = build.Status != "Failed", EvidencePath = evidence });
            }
            return results.ToArray();
        }

        [Serializable]
        private sealed class BuildSummaryFile
        {
            public BuildSummaryEntry[] Builds;
        }

        [Serializable]
        private sealed class BuildSummaryEntry
        {
            public string Name;
            public string Status;
            public string EvidenceLogPath;
            public string Reason;
        }
    }
}
