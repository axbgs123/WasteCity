using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

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
                builder.AppendLine("- " + failure.FeatureGroupId);
                builder.AppendLine("失败位置");
                builder.AppendLine("- " + failure.FailureLocationSummary);
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
            string testClass = TestClassName(fullName);
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
                    FeatureGroupId = "未归类",
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
                FeatureGroupId = feature.Id,
                FailureLocationSummary = feature.FailureLocationSummary,
                RelatedFiles = RelatedFiles(feature, catalog),
                ScenePaths = Values(feature.ScenePaths),
                RequirementIds = Values(feature.RequirementIds),
                RerunFilter = RerunFilter(testClass),
                OriginalMessage = message,
                OriginalStack = stack,
            };
        }

        private static string[] RelatedFiles(ProjectFeatureGroup feature, ProjectQualityCatalog catalog)
        {
            var files = new List<string>(Values(feature.PrimarySourceGlobs));
            foreach (ProjectReuseEntry reuse in Values(catalog.ReuseEntries).Where(entry => entry != null &&
                string.Equals(entry.FeatureGroupId, feature.Id, StringComparison.Ordinal)))
                files.AddRange(Values(reuse.AssetPaths));
            return files.Distinct(StringComparer.Ordinal).ToArray();
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

        private static string TestClassName(string fullName)
        {
            int parameterStart = fullName.IndexOf('(');
            string methodName = parameterStart >= 0 ? fullName.Substring(0, parameterStart) : fullName;
            int lastDot = methodName.LastIndexOf('.');
            return lastDot > 0 ? methodName.Substring(0, lastDot) : methodName;
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
