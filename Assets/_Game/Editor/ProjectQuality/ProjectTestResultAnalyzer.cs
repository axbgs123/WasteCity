using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace WasteCity.Editor.ProjectQuality
{
    public static class ProjectTestResultAnalyzer
    {
        public static ProjectTestAnalysisReport Analyze(string xmlPath, ProjectQualityCatalog catalog,
            ProjectInventorySnapshot snapshot)
        {
            string localPath = RequireLocalXmlPath(xmlPath);
            XDocument document = LoadLocalXml(localPath);
            XElement root = document.Root;
            if (root == null)
                throw new InvalidDataException("NUnit XML 没有根元素：" + localPath);

            XElement[] testCases = document.Descendants().Where(element =>
                element.Name.LocalName == "test-case").ToArray();
            int observedPassed = testCases.Count(element => ResultIs(element, "Passed"));
            int observedFailed = testCases.Count(element => ResultIs(element, "Failed"));
            int observedSkipped = testCases.Count(element => ResultIs(element, "Skipped"));
            int total = ReadCount(root, "total", testCases.Length);
            int passed = ReadCount(root, "passed", observedPassed);
            int failed = ReadCount(root, "failed", observedFailed);
            int skipped = ReadCount(root, "skipped", observedSkipped);
            bool incomplete = (ResultIs(root, "Failed") && observedFailed == 0) || failed > observedFailed;

            var issues = new List<ProjectQualityIssue>();
            var failures = new List<ProjectFailedTestLocation>();
            foreach (XElement testCase in testCases.Where(element => ResultIs(element, "Failed")))
                failures.Add(MapFailure(testCase, catalog, snapshot, issues));

            return new ProjectTestAnalysisReport
            {
                XmlPath = localPath,
                Total = total,
                Passed = passed,
                Failed = failed,
                Skipped = skipped,
                IsIncomplete = incomplete,
                Issues = issues.OrderBy(issue => issue.Code, StringComparer.Ordinal)
                    .ThenBy(issue => issue.Path, StringComparer.Ordinal).ToArray(),
                Failures = failures.OrderBy(failure => failure.FullName, StringComparer.Ordinal).ToArray(),
            };
        }

        public static string RenderPlainChinese(ProjectTestAnalysisReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            builder.AppendLine("测试结果摘要");
            builder.AppendLine("- 总数：" + report.Total + "；通过：" + report.Passed + "；失败：" +
                report.Failed + "；跳过：" + report.Skipped + "。");
            if (report.IsIncomplete) builder.AppendLine("- 结果不完整：失败运行没有失败测试用例。");
            foreach (ProjectQualityIssue issue in Values(report.Issues))
                builder.AppendLine("- " + issue.Code + "：" + issue.PlainChineseMessage);

            ProjectFailedTestLocation[] failures = Values(report.Failures)
                .OrderBy(failure => failure.FullName, StringComparer.Ordinal).ToArray();
            if (failures.Length == 0)
            {
                builder.AppendLine("问题区域");
                builder.AppendLine("- 无失败测试。");
                return builder.ToString();
            }

            foreach (ProjectFailedTestLocation failure in failures)
            {
                builder.AppendLine("问题区域");
                builder.AppendLine("- " + failure.FeatureGroupChineseName);
                builder.AppendLine("失败位置");
                builder.AppendLine("- " + FailureLocation(failure));
                builder.AppendLine("失败测试");
                builder.AppendLine("- " + failure.FullName);
                builder.AppendLine("优先检查");
                builder.AppendLine("- " + failure.FailureLocationSummary + "；仅作为排查起点，尚待证据确认。");
                builder.AppendLine("相关文件");
                AppendValues(builder, failure.RelatedFiles);
                builder.AppendLine("相关场景");
                AppendValues(builder, failure.ScenePaths);
                builder.AppendLine("相关需求");
                AppendValues(builder, failure.RequirementIds);
                builder.AppendLine("建议复跑");
                builder.AppendLine("- " + failure.RerunFilter);
                builder.AppendLine("原始错误");
                AppendOriginal(builder, failure.OriginalMessage);
                builder.AppendLine("原始堆栈");
                AppendOriginal(builder, failure.OriginalStack);
            }
            return builder.ToString();
        }

        private static ProjectFailedTestLocation MapFailure(XElement testCase, ProjectQualityCatalog catalog,
            ProjectInventorySnapshot snapshot, List<ProjectQualityIssue> issues)
        {
            string fullName = Attribute(testCase, "fullname");
            string testClass = TestClassName(testCase, fullName);
            string testMethod = TestMethodName(testCase, fullName);
            ProjectTestClassRecord test = Values(snapshot == null ? null : snapshot.TestClasses)
                .FirstOrDefault(record => record != null &&
                    string.Equals(record.FullName, testClass, StringComparison.Ordinal));
            ProjectFeatureGroup feature = test == null ? null : Values(catalog == null ? null : catalog.FeatureGroups)
                .FirstOrDefault(group => group != null && Values(group.TestFileGlobs)
                    .Any(glob => PathMatchesGlob(test.SourcePath, glob)));
            XElement failure = testCase.Elements().FirstOrDefault(element => element.Name.LocalName == "failure");
            string message = ChildValue(failure, "message");
            string stack = ChildValue(failure, "stack-trace");

            if (feature == null)
            {
                issues.Add(new ProjectQualityIssue
                {
                    Code = "PQTEST001",
                    Severity = ProjectQualityIssueSeverity.Warning,
                    Path = fullName,
                    PlainChineseMessage = "失败测试未归类：" + fullName,
                });
                return new ProjectFailedTestLocation
                {
                    FullName = fullName,
                    TestClassFullName = testClass,
                    TestMethodName = testMethod,
                    TestSourcePath = test == null ? string.Empty : test.SourcePath,
                    FeatureGroupId = "未归类",
                    FeatureGroupChineseName = "未归类",
                    FailureLocationSummary = "没有目录映射，先核对测试类和功能目录",
                    RelatedFiles = test == null ? new string[0] : new[] { test.SourcePath },
                    ScenePaths = new string[0],
                    RequirementIds = new string[0],
                    RerunFilter = RerunFilter(testClass),
                    OriginalMessage = message,
                    OriginalStack = stack,
                };
            }

            return new ProjectFailedTestLocation
            {
                FullName = fullName,
                TestClassFullName = testClass,
                TestMethodName = testMethod,
                TestSourcePath = test.SourcePath,
                FeatureGroupId = feature.Id,
                FeatureGroupChineseName = feature.ChineseName,
                FailureLocationSummary = feature.FailureLocationSummary,
                RelatedFiles = RelatedFiles(test, feature, catalog, snapshot),
                ScenePaths = Values(feature.ScenePaths),
                RequirementIds = Values(feature.RequirementIds),
                RerunFilter = RerunFilter(testClass),
                OriginalMessage = message,
                OriginalStack = stack,
            };
        }

        private static string[] RelatedFiles(ProjectTestClassRecord test, ProjectFeatureGroup feature,
            ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot)
        {
            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AddExact(files, seen, test.SourcePath);
            ProjectFileRecord[] sourceFiles = Values(snapshot == null ? null : snapshot.FileRecords)
                .Where(record => record != null && record.Kind == ProjectFileKind.Production &&
                    !string.IsNullOrWhiteSpace(record.Path)).ToArray();
            foreach (string glob in Values(feature.PrimarySourceGlobs))
                foreach (string path in sourceFiles.Where(record => PathMatchesGlob(record.Path, glob))
                    .Select(record => record.Path).Concat(ExistingProjectFiles(glob))
                    .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
                    AddExact(files, seen, path);

            var exactPaths = new HashSet<string>(sourceFiles.Select(record => record.Path), StringComparer.Ordinal);
            foreach (string path in Values(snapshot == null ? null : snapshot.ScenePaths)) exactPaths.Add(path);
            foreach (ProjectReuseEntry reuse in Values(catalog.ReuseEntries).Where(entry => entry != null &&
                string.Equals(entry.FeatureGroupId, feature.Id, StringComparison.Ordinal)))
                foreach (string path in Values(reuse.AssetPaths))
                    if (exactPaths.Contains(path)) AddExact(files, seen, path);
            foreach (string path in Values(feature.ScenePaths))
                if (exactPaths.Contains(path)) AddExact(files, seen, path);
            foreach (ProjectUiEntry ui in Values(catalog.UiEntries))
            {
                if (ui == null || !Values(ui.RequiredTestFiles).Any(path =>
                    Values(feature.TestFileGlobs).Any(testPath => string.Equals(path, testPath, StringComparison.Ordinal))))
                    continue;
                foreach (ProjectTypeRecord type in Values(snapshot == null ? null : snapshot.TypeRecords).Where(record =>
                    record != null && exactPaths.Contains(record.SourcePath) && TypeMatches(record.FullName, ui.OwnerTypeName))
                    .OrderBy(record => record.SourcePath, StringComparer.Ordinal))
                    AddExact(files, seen, type.SourcePath);
            }
            return files.ToArray();
        }

        private static void AddExact(List<string> paths, HashSet<string> seen, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && seen.Add(path)) paths.Add(path);
        }

        private static string RequireLocalXmlPath(string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(xmlPath) || !Path.IsPathRooted(xmlPath) ||
                Uri.IsWellFormedUriString(xmlPath, UriKind.Absolute))
                throw new InvalidDataException("NUnit XML 必须是本地绝对路径：" + xmlPath);
            string path = Path.GetFullPath(xmlPath);
            if (!File.Exists(path)) throw new InvalidDataException("NUnit XML 不存在：" + path);
            return path;
        }

        private static XDocument LoadLocalXml(string path)
        {
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreWhitespace = false,
                };
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                    return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
            catch (Exception exception) when (exception is XmlException || exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                throw new InvalidDataException("无法读取 NUnit XML：" + path, exception);
            }
        }

        private static int ReadCount(XElement root, string name, int fallback)
        {
            int value;
            return int.TryParse(Attribute(root, name), out value) && value >= 0 ? value : fallback;
        }

        private static bool ResultIs(XElement element, string result)
        {
            return string.Equals(Attribute(element, "result"), result, StringComparison.OrdinalIgnoreCase);
        }

        private static string Attribute(XElement element, string name)
        {
            XAttribute attribute = element == null ? null : element.Attribute(name);
            return attribute == null ? string.Empty : attribute.Value;
        }

        private static string ChildValue(XElement element, string name)
        {
            XElement child = element == null ? null : element.Elements()
                .FirstOrDefault(candidate => candidate.Name.LocalName == name);
            return child == null ? string.Empty : child.Value;
        }

        private static string TestClassName(XElement testCase, string fullName)
        {
            string className = Attribute(testCase, "classname").Trim();
            return className.Length == 0 ? TestClassNameFallback(fullName) : className;
        }

        private static string TestClassNameFallback(string fullName)
        {
            int parameterStart = fullName.IndexOf('(');
            string methodName = parameterStart >= 0 ? fullName.Substring(0, parameterStart) : fullName;
            int lastDot = methodName.LastIndexOf('.');
            return lastDot > 0 ? methodName.Substring(0, lastDot) : methodName;
        }

        private static string TestMethodName(XElement testCase, string fullName)
        {
            string methodName = Attribute(testCase, "methodname").Trim();
            if (methodName.Length > 0) return methodName;
            int parameterStart = fullName.IndexOf('(');
            string name = parameterStart >= 0 ? fullName.Substring(0, parameterStart) : fullName;
            int lastDot = name.LastIndexOf('.');
            return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
        }

        private static string RerunFilter(string testClass)
        {
            return "-testFilter " + ShellQuote(testClass);
        }

        private static string ShellQuote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\"'\"'") + "'";
        }

        private static bool PathMatchesGlob(string path, string glob)
        {
            if (path == null || glob == null) return false;
            string pattern = "^" + System.Text.RegularExpressions.Regex.Escape(glob.Replace('\\', '/'))
                .Replace("\\*\\*", ".*").Replace("\\*", "[^/]*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(path.Replace('\\', '/'), pattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        private static IEnumerable<string> ExistingProjectFiles(string glob)
        {
            string normalizedGlob = ValidatePrimaryGlob(glob);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string firstSegment = normalizedGlob.Substring(0, normalizedGlob.IndexOf('/'));
            string searchRoot = Path.Combine(root, firstSegment);
            if (!Directory.Exists(searchRoot)) return new string[0];

            var matches = new List<string>();
            foreach (string path in Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                string fullPath = Path.GetFullPath(path);
                if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
                string relative = fullPath.Substring(root.Length + 1).Replace('\\', '/');
                if (PathMatchesGlob(relative, normalizedGlob)) matches.Add(relative);
            }
            return matches;
        }

        private static string ValidatePrimaryGlob(string glob)
        {
            if (string.IsNullOrWhiteSpace(glob))
                throw new InvalidDataException("主源码 glob 无效");
            string value = glob.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(value) || value.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("..", StringComparison.Ordinal) >= 0 || !value.Contains("/"))
                throw new InvalidDataException("主源码 glob 必须是仓库内相对路径：" + glob);
            int recursive = value.IndexOf("**", StringComparison.Ordinal);
            if (recursive >= 0 && (recursive != value.Length - 2 || !value.EndsWith("/**", StringComparison.Ordinal)))
                throw new InvalidDataException("主源码 glob 无效：" + glob);
            string firstSegment = value.Substring(0, value.IndexOf('/'));
            if (firstSegment.Length == 0 || firstSegment.IndexOf('*') >= 0 || firstSegment == ".")
                throw new InvalidDataException("主源码 glob 根目录无效：" + glob);
            return value;
        }

        private static bool TypeMatches(string fullName, string expected)
        {
            return string.Equals(fullName, expected, StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(fullName) && !string.IsNullOrEmpty(expected) &&
                    fullName.EndsWith("." + expected, StringComparison.Ordinal));
        }

        private static string FailureLocation(ProjectFailedTestLocation failure)
        {
            string identity = failure.TestClassFullName + "." + failure.TestMethodName;
            return string.IsNullOrEmpty(failure.TestSourcePath) ? identity : failure.TestSourcePath + "：" + identity;
        }

        private static T[] Values<T>(T[] values)
        {
            return values ?? new T[0];
        }

        private static void AppendValues(StringBuilder builder, string[] values)
        {
            string[] safe = Values(values);
            if (safe.Length == 0) builder.AppendLine("- 无。");
            else foreach (string value in safe) builder.AppendLine("- " + value);
        }

        private static void AppendOriginal(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value)) builder.AppendLine("- 无。");
            else builder.AppendLine(value);
        }
    }
}
