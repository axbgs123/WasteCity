using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;

namespace WasteCity.Editor.ProjectQuality
{
    public sealed class ProjectTestRunSummary
    {
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public string XmlPath;
    }

    public sealed class ProjectCommandResult
    {
        public bool Passed;
        public string EvidencePath;
    }

    public sealed class ProjectVerificationSnapshot
    {
        public string VerifiedCommitSha;
        public string VerifiedAtIso8601;
        public ProjectTestRunSummary EditMode;
        public ProjectTestRunSummary PlayMode;
        public ProjectCommandResult Compile;
        public ProjectCommandResult[] Builds;
        public string HumanPlaytestStatus;
    }

    public static class ProjectDocumentationGenerator
    {
        private const string GeneratorSchema = "project-quality-documentation/v2";
        public const string ProjectInventoryPath = "Docs/Generated/Project-Inventory-ZH.md";
        public const string TestInventoryPath = "Docs/Generated/Test-Inventory-ZH.md";
        public const string VerificationPath = "Docs/Generated/Latest-Verification-ZH.md";
        public const string AttentionPath = "Docs/Generated/Documentation-Attention-ZH.md";

        private static readonly string[] ApprovedPaths =
        {
            ProjectInventoryPath, TestInventoryPath, VerificationPath, AttentionPath,
        };

        public static IReadOnlyDictionary<string, string> RenderStructuralDocuments(
            ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot)
        {
            RequireCatalog(catalog);
            RequireSnapshot(snapshot);
            string inventoryBody = RenderProjectInventoryBody(catalog, snapshot);
            string testBody = RenderTestInventoryBody(catalog, snapshot);
            string fingerprint = ContentFingerprint(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { ProjectInventoryPath, inventoryBody }, { TestInventoryPath, testBody },
            });
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { ProjectInventoryPath, WithStructuralHeader("项目自动清单", fingerprint, inventoryBody) },
                { TestInventoryPath, WithStructuralHeader("测试自动清单", fingerprint, testBody) },
            };
            return files;
        }

        public static string RenderDocumentationAttention(ProjectQualityCatalog catalog,
            IReadOnlyList<string> changedPaths)
        {
            RequireCatalog(catalog);
            var reminders = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string changedPath in Values(changedPaths).Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                foreach (ProjectDocumentationRule rule in Values(catalog.DocumentationRules).Where(value => value != null))
                {
                    if (!Values(rule.ChangedPathGlobs).Any(glob => PathMatchesGlob(changedPath, glob))) continue;
                    foreach (string documentPath in Values(rule.ReviewDocumentPaths).OrderBy(path => path, StringComparer.Ordinal))
                        reminders.Add("- 检查 " + Code(documentPath) + "：" + Text(rule.PlainChineseReason));
                }
            }

            var builder = NewDocument("文档关注提醒", "本文件只列出根据变更路径得到的提醒，不代表文档已经更新或获得批准。");
            if (reminders.Count == 0)
                builder.AppendLine("当前没有待处理的路径提醒。");
            else
                foreach (string reminder in reminders) builder.AppendLine(reminder);
            return Finish(builder);
        }

        public static string RenderVerification(ProjectVerificationSnapshot snapshot)
        {
            ValidateVerificationSnapshot(snapshot);
            var builder = NewDocument("最近验证快照", "这是已记录的既有验证证据，不是本次运行自动推断的结果。");
            builder.AppendLine("- 已验证提交：" + Code(snapshot.VerifiedCommitSha));
            builder.AppendLine("- 记录时间：" + Code(snapshot.VerifiedAtIso8601));
            builder.AppendLine();
            builder.AppendLine("## 1. 自动测试");
            AppendTestRun(builder, "EditMode", snapshot.EditMode);
            AppendTestRun(builder, "PlayMode", snapshot.PlayMode);
            builder.AppendLine();
            builder.AppendLine("## 2. 编译与正式构建");
            builder.AppendLine("- 无界面编译：" + (snapshot.Compile.Passed ? "通过" : "未通过") + "，证据：" + Code(snapshot.Compile.EvidencePath));
            builder.AppendLine("- 正式构建：" + snapshot.Builds.Length + " 项，" +
                (snapshot.Builds.All(build => build.Passed) ? "均通过" : "存在未通过项") + "。");
            foreach (ProjectCommandResult build in snapshot.Builds)
                builder.AppendLine("  - " + (build.Passed ? "通过" : "未通过") + "：" + Code(build.EvidencePath));
            builder.AppendLine();
            builder.AppendLine("## 3. 人工试玩");
            builder.AppendLine("- 状态：" + Text(snapshot.HumanPlaytestStatus));
            return Finish(builder);
        }

        public static void WriteGeneratedFiles(string projectRoot, IReadOnlyDictionary<string, string> files)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || files == null)
                throw new InvalidOperationException("项目根目录或生成文件无效");
            if (files.Count != ApprovedPaths.Length || !new HashSet<string>(files.Keys, StringComparer.Ordinal)
                .SetEquals(ApprovedPaths)) throw new InvalidOperationException("必须同批写入四份批准的生成文件");
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var prepared = new List<KeyValuePair<string, byte[]>>();
            foreach (KeyValuePair<string, string> pair in files.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                string target = ResolveApprovedTarget(fullRoot, pair.Key);
                prepared.Add(new KeyValuePair<string, byte[]>(target, Utf8Bytes(pair.Value)));
            }

            foreach (KeyValuePair<string, byte[]> pair in prepared)
            {
                string temporaryPath = pair.Key + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(pair.Key));
                    using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(pair.Value, 0, pair.Value.Length);
                        stream.Flush(true);
                    }
                    if (File.Exists(pair.Key) && File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))
                    {
                        File.Delete(temporaryPath);
                        continue;
                    }
                    if (File.Exists(pair.Key)) File.Replace(temporaryPath, pair.Key, null);
                    else File.Move(temporaryPath, pair.Key);
                }
                catch
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                    throw;
                }
            }
        }

        public static void WriteVerificationFile(string projectRoot, string content)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException("项目根目录无效");
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            WriteApprovedFile(ResolveApprovedTarget(fullRoot, VerificationPath), Utf8Bytes(content));
        }

        public static ProjectVerificationSnapshot CreateRecordedPriorVerificationSnapshot()
        {
            return new ProjectVerificationSnapshot
            {
                VerifiedCommitSha = "81b2f47d1688a72a7ddba36a2ffa04b1025e40f9",
                VerifiedAtIso8601 = "2026-08-12T00:26:11+08:00",
                EditMode = new ProjectTestRunSummary
                {
                    Total = 1121, Passed = 1121, Failed = 0, Skipped = 0,
                    XmlPath = "/tmp/wastecity-sparse-map/task-05-full-editmode-v2.xml",
                },
                PlayMode = new ProjectTestRunSummary
                {
                    Total = 82, Passed = 82, Failed = 0, Skipped = 0,
                    XmlPath = "/tmp/wastecity-sparse-map/task-05-full-playmode.xml",
                },
                Compile = new ProjectCommandResult
                {
                    Passed = true, EvidencePath = "/tmp/wastecity-sparse-map/task-05-compile.log",
                },
                Builds = new[]
                {
                    new ProjectCommandResult { Passed = true, EvidencePath = "/tmp/wastecity-sparse-map/task-05-build-release-3d.log" },
                    new ProjectCommandResult { Passed = true, EvidencePath = "/tmp/wastecity-sparse-map/task-05-build-development-3d.log" },
                    new ProjectCommandResult { Passed = true, EvidencePath = "/tmp/wastecity-sparse-map/task-05-build-legacy-2d.log" },
                },
                HumanPlaytestStatus = "等待用户复验",
            };
        }

        public static void GenerateForCurrentProject()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromFile(
                Path.Combine(root, "Docs", "Engineering", "project-quality-catalog.json"));
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(root);
            IReadOnlyDictionary<string, string> structural = RenderStructuralDocuments(catalog, snapshot);
            var files = new Dictionary<string, string>(structural, StringComparer.Ordinal)
            {
                { AttentionPath, RenderDocumentationAttention(catalog, new string[0]) },
                { VerificationPath, RenderVerification(CreateRecordedPriorVerificationSnapshot()) },
            };
            WriteGeneratedFiles(root, files);
        }

        private static string RenderProjectInventoryBody(ProjectQualityCatalog catalog,
            ProjectInventorySnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## 1. 生成说明与内容指纹");
            builder.AppendLine("- 指纹覆盖本文件与测试清单的最终正文和生成器 schema，不读取当前时间、Git 提交或机器路径。");
            builder.AppendLine("## 2. 程序集");
            AppendList(builder, Values(snapshot.AssemblyRecords).OrderBy(value => value.Name, StringComparer.Ordinal)
                .Select(value => Code(value.Name) + "：" + Code(value.Path)));
            builder.AppendLine("## 3. 启用场景与顺序");
            AppendList(builder, Values(catalog.Scenes).Where(value => value.EnabledInBuildSettings)
                .OrderBy(value => value.ExpectedBuildIndex).ThenBy(value => value.Path, StringComparer.Ordinal)
                .Select(value => value.ExpectedBuildIndex + "：" + Code(value.Path) + "（" + Text(value.ChineseName) + "）"));
            builder.AppendLine("## 4. 按功能分组的生产文件");
            foreach (ProjectFeatureGroup feature in Values(catalog.FeatureGroups).OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                builder.AppendLine("- " + Text(feature.ChineseName) + "（" + Code(feature.Id) + "）：");
                AppendList(builder, Values(snapshot.FileRecords).Where(value => value.Kind == ProjectFileKind.Production &&
                    Values(feature.SourceGlobs).Any(glob => PathMatchesGlob(value.Path, glob))).OrderBy(value => value.Path, StringComparer.Ordinal)
                    .Select(value => Code(value.Path)));
            }
            builder.AppendLine("## 5. MonoBehaviour 组件");
            AppendList(builder, Values(snapshot.TypeRecords).Where(value => value.Kind == ProjectTypeKind.MonoBehaviour)
                .OrderBy(value => value.FullName, StringComparer.Ordinal).Select(TypeLine));
            builder.AppendLine("## 6. ScriptableObject 资源");
            AppendList(builder, Values(snapshot.TypeRecords).Where(value => value.Kind == ProjectTypeKind.ScriptableObject)
                .OrderBy(value => value.FullName, StringComparer.Ordinal).Select(TypeLine));
            builder.AppendLine("## 7. 界面所有者");
            AppendList(builder, Values(catalog.UiEntries).OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(value => Text(value.ChineseName) + "：" + Code(value.OwnerTypeName) + "，场景 " + Code(value.SceneId)));
            builder.AppendLine("## 8. 编辑器、构建与性能入口");
            AppendList(builder, Values(snapshot.EditorEntryPoints).OrderBy(value => value.OwnerTypeFullName, StringComparer.Ordinal)
                .ThenBy(value => value.MethodName, StringComparer.Ordinal)
                .Select(value => Code(value.OwnerTypeFullName + "." + value.MethodName)));
            builder.AppendLine("## 9. 美术接入与稳定展示路径");
            ProjectFeatureGroup presentation = Values(catalog.FeatureGroups).FirstOrDefault(value =>
                value.Id == "presentation-art-integration");
            var presentationPaths = new SortedSet<string>(StringComparer.Ordinal);
            if (presentation != null)
                foreach (ProjectFileRecord file in Values(snapshot.FileRecords).Where(value => value.Kind == ProjectFileKind.Production &&
                    Values(presentation.SourceGlobs).Any(glob => PathMatchesGlob(value.Path, glob)))) presentationPaths.Add(file.Path);
            foreach (ProjectReuseEntry reuse in Values(catalog.ReuseEntries).Where(value =>
                value.FeatureGroupId == "presentation-art-integration"))
                foreach (string path in Values(reuse.AssetPaths)) presentationPaths.Add(path);
            AppendList(builder, presentationPaths.Select(Code));
            builder.AppendLine("## 10. 明确排除项");
            AppendList(builder, Values(catalog.ExplicitSourceExclusions).Concat(Values(catalog.ExplicitTestExclusions))
                .OrderBy(value => value.Path, StringComparer.Ordinal).Select(value => Code(value.Path) + "：" + Text(value.Reason)));
            return Finish(builder);
        }

        private static string RenderTestInventoryBody(ProjectQualityCatalog catalog,
            ProjectInventorySnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## 1. 生成说明与内容指纹");
            builder.AppendLine("- 指纹只来自已提供的目录和项目快照，方便确认清单是否对应同一份事实。");
            builder.AppendLine("## 2. EditMode 与 PlayMode 的普通中文说明");
            builder.AppendLine("- EditMode 在不启动完整游戏画面的情况下检查规则和资料；PlayMode 会启动运行时流程，检查玩家实际会遇到的互动。");
            builder.AppendLine("## 3. 每个功能分组的最低验证门");
            foreach (ProjectFeatureGroup feature in Values(catalog.FeatureGroups).OrderBy(value => value.Id, StringComparer.Ordinal))
                builder.AppendLine("- " + Text(feature.ChineseName) + "：" + VerificationName(feature.MinimumVerification));
            builder.AppendLine("## 4. 精确测试文件与测试类");
            foreach (ProjectTestClassRecord test in Values(snapshot.TestClasses).OrderBy(value => value.FullName, StringComparer.Ordinal))
                builder.AppendLine("- " + Code(test.SourcePath) + "：" + Code(test.FullName) + "（" + Text(test.Platform.ToString()) + "）");
            builder.AppendLine("## 5. 可复制的测试筛选命令");
            string filter = string.Join("|", Values(snapshot.TestClasses).Select(value => value.FullName)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            builder.AppendLine("- -testFilter '" + filter + "'");
            builder.AppendLine("## 6. 失败定位用的源码路径与受控需求编号");
            foreach (ProjectFeatureGroup feature in Values(catalog.FeatureGroups).OrderBy(value => value.Id, StringComparer.Ordinal))
                builder.AppendLine("- " + Text(feature.ChineseName) + "：源码 " + JoinCode(Values(feature.SourceGlobs)) + "；需求 " + JoinCode(Values(feature.RequirementIds)));
            return Finish(builder);
        }

        private static void AppendTestRun(StringBuilder builder, string name, ProjectTestRunSummary run)
        {
            builder.AppendLine("- " + name + "：" + run.Passed + "/" + run.Total + " 通过，失败 " + run.Failed +
                "，跳过 " + run.Skipped + "，证据：" + Code(run.XmlPath));
        }

        private static StringBuilder NewDocument(string title, string purpose)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# " + title);
            builder.AppendLine();
            builder.AppendLine(purpose);
            builder.AppendLine();
            return builder;
        }

        private static string Finish(StringBuilder builder)
        {
            string value = builder.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
            return value.EndsWith("\n", StringComparison.Ordinal) ? value : value + "\n";
        }

        private static void AppendList(StringBuilder builder, IEnumerable<string> values)
        {
            string[] items = Values(values).ToArray();
            if (items.Length == 0) builder.AppendLine("- 无。");
            else foreach (string item in items) builder.AppendLine("- " + item);
        }

        private static string TypeLine(ProjectTypeRecord type)
        {
            return Code(type.FullName) + "：" + Code(type.SourcePath);
        }

        private static string VerificationName(ProjectVerificationLevel level)
        {
            switch (level)
            {
                case ProjectVerificationLevel.FocusedEditMode: return "针对性 EditMode 测试";
                case ProjectVerificationLevel.FocusedPlayMode: return "针对性 PlayMode 测试";
                case ProjectVerificationLevel.FullRegression: return "完整回归";
                case ProjectVerificationLevel.Compile: return "无界面编译";
                case ProjectVerificationLevel.WindowsBuilds: return "Windows 构建";
                case ProjectVerificationLevel.Performance: return "性能检查";
                case ProjectVerificationLevel.HumanPlaytest: return "人工试玩";
                default: return "未登记验证门";
            }
        }

        private static string JoinCode(IEnumerable<string> values)
        {
            string[] items = Values(values).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return items.Length == 0 ? "无" : string.Join("、", items.Select(Code).ToArray());
        }

        private static string WithStructuralHeader(string title, string fingerprint, string body)
        {
            return Finish(NewDocument(title, "本附录为自动生成内容，由目录和项目快照生成；请不要手工修改。生成器 schema：" +
                Code(GeneratorSchema) + "，内容指纹：" + Code(fingerprint) + "。").Append(body));
        }

        private static string ContentFingerprint(IReadOnlyDictionary<string, string> bodies)
        {
            var bytes = new List<byte>();
            AppendFingerprintPart(bytes, GeneratorSchema);
            foreach (KeyValuePair<string, string> pair in bodies.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                AppendFingerprintPart(bytes, pair.Key);
                AppendFingerprintPart(bytes, pair.Value);
            }
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void AppendFingerprintPart(List<byte> bytes, string value)
        {
            byte[] valueBytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] lengthBytes = Encoding.UTF8.GetBytes(valueBytes.Length.ToString(CultureInfo.InvariantCulture) + ":");
            bytes.AddRange(lengthBytes);
            bytes.AddRange(valueBytes);
        }

        private static string Text(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ")
                .Replace("\\", "\\\\").Replace("|", "\\|").Replace("`", "\\`");
        }

        private static string Code(string value)
        {
            return "`" + Text(value) + "`";
        }

        private static byte[] Utf8Bytes(string content)
        {
            return new UTF8Encoding(false).GetBytes(Finish(new StringBuilder(content ?? string.Empty)));
        }

        private static string ResolveApprovedTarget(string fullRoot, string relativePath)
        {
            if (!ApprovedPaths.Contains(relativePath, StringComparer.Ordinal))
                throw new InvalidOperationException("只允许写入 Docs/Generated 的批准文件");
            string target = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string generatedDirectory = Path.GetFullPath(Path.Combine(fullRoot, "Docs", "Generated"));
            if (!target.StartsWith(generatedDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("生成文件路径超出 Docs/Generated");
            return target;
        }

        private static void WriteApprovedFile(string target, byte[] bytes)
        {
            string temporaryPath = target + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(bytes))
                {
                    File.Delete(temporaryPath);
                    return;
                }
                if (File.Exists(target)) File.Replace(temporaryPath, target, null);
                else File.Move(temporaryPath, target);
            }
            catch
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                throw;
            }
        }

        private static void RequireCatalog(ProjectQualityCatalog catalog)
        {
            if (catalog == null || catalog.FeatureGroups == null || catalog.ReuseEntries == null || catalog.Scenes == null ||
                catalog.UiEntries == null || catalog.DocumentationRules == null || catalog.ExplicitSourceExclusions == null ||
                catalog.ExplicitTestExclusions == null) throw new InvalidOperationException("项目目录不完整");
        }

        private static void RequireSnapshot(ProjectInventorySnapshot snapshot)
        {
            if (snapshot == null) throw new InvalidOperationException("项目快照无效");
            snapshot.ToDeterministicJson();
        }

        private static void ValidateVerificationSnapshot(ProjectVerificationSnapshot snapshot)
        {
            if (snapshot == null || !Regex.IsMatch(snapshot.VerifiedCommitSha ?? string.Empty, "^[0-9a-f]{40}$") ||
                !IsOffsetIso8601(snapshot.VerifiedAtIso8601) || !IsValidTestRun(snapshot.EditMode) ||
                !IsValidTestRun(snapshot.PlayMode) || !IsValidCommand(snapshot.Compile) || snapshot.Builds == null ||
                snapshot.Builds.Any(build => !IsValidCommand(build)) || !IsControlledHumanStatus(snapshot.HumanPlaytestStatus))
                throw new InvalidOperationException("验证快照不完整或不受控");
        }

        private static bool IsOffsetIso8601(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value,
                @"^\d{4}-\d{2}-\d{2}T.+(?:Z|[+-]\d{2}:\d{2})$") &&
                DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }

        private static bool IsValidTestRun(ProjectTestRunSummary value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.XmlPath) || !value.XmlPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(value.XmlPath) || value.Total < 0 || value.Passed < 0 || value.Failed < 0 || value.Skipped < 0) return false;
            try
            {
                XDocument document = XDocument.Load(value.XmlPath);
                if (document.Root == null || document.Root.Name.LocalName != "test-run") return false;
                return checked((long)value.Passed + value.Failed + value.Skipped) == value.Total;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsValidCommand(ProjectCommandResult value)
        {
            return value != null && !string.IsNullOrWhiteSpace(value.EvidencePath) && File.Exists(value.EvidencePath);
        }

        private static bool IsControlledHumanStatus(string value)
        {
            if (value == "未进行" || value == "等待用户复验") return true;
            Match match = Regex.Match(value ?? string.Empty,
                @"^(?:BUG|IDEA|DOC)-\d{4} 已由用户于 (\d{4}-\d{2}-\d{2}) 验证$");
            return match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }

        private static bool PathMatchesGlob(string path, string glob)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(glob)) return false;
            if (glob.EndsWith("/**", StringComparison.Ordinal)) return path.StartsWith(glob.Substring(0, glob.Length - 2), StringComparison.Ordinal);
            return MatchSegment(path, 0, glob, 0);
        }

        private static bool MatchSegment(string path, int pathIndex, string glob, int globIndex)
        {
            while (globIndex < glob.Length)
            {
                char token = glob[globIndex++];
                if (token == '*')
                {
                    for (int index = pathIndex; index <= path.Length && (index == pathIndex ||
                        (index > 0 && path[index - 1] != '/')); index++)
                        if (MatchSegment(path, index, glob, globIndex)) return true;
                    return false;
                }
                if (pathIndex >= path.Length || (token == '?' ? path[pathIndex] == '/' : token != path[pathIndex])) return false;
                pathIndex++;
            }
            return pathIndex == path.Length;
        }

        private static IEnumerable<T> Values<T>(IEnumerable<T> values)
        {
            return values ?? Enumerable.Empty<T>();
        }
    }
}
