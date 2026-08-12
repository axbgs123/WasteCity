using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WasteCity.Editor.ProjectQuality
{
    public static class ProjectQualityValidator
    {
        public static IReadOnlyList<ProjectQualityIssue> Validate(
            ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot, string projectRoot)
        {
            var issues = new List<ProjectQualityIssue>();
            if (catalog == null || snapshot == null || string.IsNullOrWhiteSpace(projectRoot))
            {
                Add(issues, "PQ005", "目录、项目快照或项目根目录无效", string.Empty);
                return Sort(issues);
            }

            ValidateCatalogRelationships(catalog, issues);
            ValidateFileCoverage(catalog, snapshot, issues);
            ValidateReuse(catalog, snapshot, issues);
            ValidateScenes(catalog, snapshot, issues);
            ValidateUi(catalog, snapshot, issues);
            ValidateDocumentation(catalog, projectRoot, issues);
            if (issues.Count == 0)
                ValidateGeneratedDocumentation(catalog, snapshot, projectRoot, issues);
            return Sort(issues);
        }

        private static void ValidateCatalogRelationships(ProjectQualityCatalog catalog, List<ProjectQualityIssue> issues)
        {
            if (catalog.SchemaVersion != 1 || catalog.FeatureGroups == null || catalog.ReuseEntries == null ||
                catalog.Scenes == null || catalog.UiEntries == null || catalog.DocumentationRules == null ||
                catalog.ExplicitSourceExclusions == null || catalog.ExplicitTestExclusions == null)
            {
                Add(issues, "PQ005", "目录结构不完整或版本不受支持", string.Empty);
                return;
            }

            var featureIds = new HashSet<string>(catalog.FeatureGroups.Where(value => value != null)
                .Select(value => value.Id), StringComparer.Ordinal);
            var sceneIds = new HashSet<string>(catalog.Scenes.Where(value => value != null)
                .Select(value => value.Id), StringComparer.Ordinal);
            foreach (ProjectReuseEntry reuse in catalog.ReuseEntries)
            {
                if (reuse == null || string.IsNullOrWhiteSpace(reuse.FeatureGroupId) ||
                    !featureIds.Contains(reuse.FeatureGroupId))
                    Add(issues, "PQ005", "复用条目引用了未知功能分组", reuse == null ? string.Empty : reuse.Id);
            }
            foreach (ProjectUiEntry ui in catalog.UiEntries)
            {
                if (ui == null || string.IsNullOrWhiteSpace(ui.SceneId) || !sceneIds.Contains(ui.SceneId))
                    Add(issues, "PQ005", "界面条目引用了未知场景", ui == null ? string.Empty : ui.Id);
            }
            foreach (ProjectFeatureGroup feature in catalog.FeatureGroups)
            {
                if (feature == null) continue;
                foreach (string scenePath in Values(feature.ScenePaths))
                    if (!Values(catalog.Scenes).Any(scene => scene != null && scene.Path == scenePath))
                        Add(issues, "PQ005", "功能分组引用了未登记场景", scenePath);
            }
            ValidateExclusions(catalog.ExplicitSourceExclusions, "生产文件排除项", issues);
            ValidateExclusions(catalog.ExplicitTestExclusions, "测试文件排除项", issues);
        }

        private static void ValidateFileCoverage(ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot,
            List<ProjectQualityIssue> issues)
        {
            foreach (ProjectFileRecord file in Values(snapshot.FileRecords))
            {
                if (file == null || string.IsNullOrWhiteSpace(file.Path)) continue;
                if (file.Kind == ProjectFileKind.Production)
                {
                    if (!IsExplicitlyExcluded(file.Path, catalog.ExplicitSourceExclusions) &&
                        !Values(catalog.FeatureGroups).Any(feature => MatchesAny(file.Path, feature.SourceGlobs)))
                        Add(issues, "PQ001", "生产文件没有功能分组映射", file.Path);
                }
                else if (!IsExplicitlyExcluded(file.Path, catalog.ExplicitTestExclusions) &&
                    !Values(catalog.FeatureGroups).Any(feature => MatchesAny(file.Path, feature.TestFileGlobs)))
                {
                    Add(issues, "PQ002", "测试文件没有功能分组映射", file.Path);
                }
            }

            foreach (ProjectTestClassRecord testClass in Values(snapshot.TestClasses))
            {
                if (testClass == null || string.IsNullOrWhiteSpace(testClass.SourcePath)) continue;
                if (!IsExplicitlyExcluded(testClass.SourcePath, catalog.ExplicitTestExclusions) &&
                    !Values(catalog.FeatureGroups).Any(feature => MatchesAny(testClass.SourcePath, feature.TestFileGlobs)))
                    Add(issues, "PQ002", "测试类没有功能分组映射", testClass.SourcePath);
            }
        }

        private static void ValidateReuse(ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot,
            List<ProjectQualityIssue> issues)
        {
            var files = new HashSet<string>(Values(snapshot.FileRecords).Where(value => value != null)
                .Select(value => value.Path), StringComparer.Ordinal);
            var testFiles = new HashSet<string>(Values(snapshot.FileRecords)
                .Where(value => value != null && value.Kind != ProjectFileKind.Production).Select(value => value.Path),
                StringComparer.Ordinal);
            foreach (ProjectReuseEntry reuse in Values(catalog.ReuseEntries))
            {
                if (reuse == null) continue;
                foreach (string path in Values(reuse.AssetPaths))
                    if (!files.Contains(path) && !Values(snapshot.ScenePaths).Contains(path))
                        Add(issues, "PQ003", "复用资源路径不存在于项目快照", path);
                foreach (string typeName in Values(reuse.TypeNames))
                    if (!Values(snapshot.TypeRecords).Any(type => TypeMatches(type, typeName)))
                        Add(issues, "PQ003", "复用类型不存在于项目快照", typeName);
                foreach (string testPath in Values(reuse.RequiredTestFiles))
                    if (!testFiles.Contains(testPath)) Add(issues, "PQ004", "复用条目缺少必需测试", testPath);

                if (reuse.ReuseLevel == ProjectReuseLevel.Recommended && IsFrozenFeature(reuse.FeatureGroupId))
                {
                    if (Values(reuse.AssetPaths).Any(path => IsFrozenScenePath(catalog, path)))
                        Add(issues, "PQ007", "冻结二维场景条目不能标记为推荐复用", reuse.Id);
                    else
                        Add(issues, "PQ008", "禁止新工作的冻结二维条目不能标记为推荐复用", reuse.Id);
                }
            }
        }

        private static void ValidateScenes(ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot,
            List<ProjectQualityIssue> issues)
        {
            foreach (ProjectSceneEntry scene in Values(catalog.Scenes))
            {
                if (scene == null) continue;
                ProjectSceneRecord actual = Values(snapshot.SceneRecords)
                    .FirstOrDefault(value => value != null && value.Path == scene.Path);
                if (scene.EnabledInBuildSettings)
                {
                    if (actual == null || actual.BuildIndex != scene.ExpectedBuildIndex)
                        Add(issues, "PQ006", "场景启用状态或构建顺序与目录不一致", scene.Path);
                }
                else if (actual != null)
                    Add(issues, "PQ006", "目录要求禁用的场景仍出现在构建设置中", scene.Path);
                if (scene.ReuseLevel == ProjectReuseLevel.Recommended && IsFrozenScenePath(catalog, scene.Path))
                    Add(issues, "PQ007", "冻结二维场景不能标记为推荐复用", scene.Path);
            }
            foreach (ProjectSceneRecord actual in Values(snapshot.SceneRecords))
                if (actual != null && !Values(catalog.Scenes).Any(scene => scene != null && scene.Path == actual.Path))
                    Add(issues, "PQ006", "构建设置包含未登记场景", actual.Path);
        }

        private static void ValidateUi(ProjectQualityCatalog catalog, ProjectInventorySnapshot snapshot,
            List<ProjectQualityIssue> issues)
        {
            var testFiles = new HashSet<string>(Values(snapshot.FileRecords)
                .Where(value => value != null && value.Kind != ProjectFileKind.Production).Select(value => value.Path),
                StringComparer.Ordinal);
            foreach (ProjectUiEntry ui in Values(catalog.UiEntries))
            {
                if (ui == null) continue;
                bool hasOwner = Values(snapshot.TypeRecords).Any(type => TypeMatches(type, ui.OwnerTypeName));
                bool hasScene = Values(catalog.Scenes).Any(scene => scene != null && scene.Id == ui.SceneId &&
                    Values(snapshot.SceneRecords).Any(actual => actual != null && actual.Path == scene.Path));
                bool hasTests = Values(ui.RequiredTestFiles).All(testFiles.Contains);
                if (!hasOwner || !hasScene || !hasTests)
                    Add(issues, "PQ009", "界面所有者、场景或必需测试与项目快照不一致", ui.Id);
            }
        }

        private static void ValidateDocumentation(ProjectQualityCatalog catalog, string projectRoot,
            List<ProjectQualityIssue> issues)
        {
            var paths = Values(catalog.FeatureGroups).SelectMany(feature => Values(feature.HumanDocumentPaths))
                .Concat(Values(catalog.DocumentationRules).SelectMany(rule => Values(rule.ReviewDocumentPaths)))
                .Distinct(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (IsGeneratedOutputPath(path)) continue;
                string absolutePath = ToProjectPath(projectRoot, path);
                if (absolutePath == null || !File.Exists(absolutePath))
                {
                    Add(issues, "PQ010", "人工文档路径不存在", path);
                    continue;
                }
                if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string target in MarkdownTargets(File.ReadAllText(absolutePath)))
                {
                    string linkedPath = ResolveMarkdownPath(projectRoot, path, target);
                    if (linkedPath == null || !File.Exists(linkedPath))
                        Add(issues, "PQ010", "Markdown 链接目标不存在", path + " -> " + target);
                }
            }
        }

        private static void ValidateGeneratedDocumentation(ProjectQualityCatalog catalog,
            ProjectInventorySnapshot snapshot, string projectRoot, List<ProjectQualityIssue> issues)
        {
            if (!Values(catalog.DocumentationRules).Any(rule => rule != null &&
                rule.Id == "generated-project-quality-appendices" &&
                ApprovedGeneratedPaths.All(path => Values(rule.ReviewDocumentPaths).Contains(path)))) return;

            IReadOnlyDictionary<string, string> expected =
                ProjectDocumentationGenerator.RenderStructuralDocuments(catalog, snapshot);
            foreach (KeyValuePair<string, string> document in expected.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                string absolutePath = ToProjectPath(projectRoot, document.Key);
                byte[] expectedBytes = new UTF8Encoding(false).GetBytes(document.Value);
                if (absolutePath == null || !File.Exists(absolutePath) ||
                    !File.ReadAllBytes(absolutePath).SequenceEqual(expectedBytes))
                    Add(issues, "PQ011", "自动生成文档缺失或内容已过期", document.Key);
            }
        }

        private static IEnumerable<string> MarkdownTargets(string markdown)
        {
            foreach (Match match in Regex.Matches(markdown ?? string.Empty, @"\[[^\]]*\]\(([^\s\)]+)(?:\s+[^\)]*)?\)"))
            {
                string target = match.Groups[1].Value.Trim();
                if (target.Length == 0 || target[0] == '#' || target.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) continue;
                int fragment = target.IndexOfAny(new[] { '#', '?' });
                yield return fragment >= 0 ? target.Substring(0, fragment) : target;
            }
        }

        private static string ResolveMarkdownPath(string root, string documentPath, string target)
        {
            string combined = target.StartsWith("/", StringComparison.Ordinal)
                ? target.TrimStart('/')
                : Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, target);
            return ToProjectPath(root, combined.Replace('\\', '/'));
        }

        private static string ToProjectPath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? candidate : null;
        }

        private static readonly string[] ApprovedGeneratedPaths =
        {
            ProjectDocumentationGenerator.ProjectInventoryPath,
            ProjectDocumentationGenerator.TestInventoryPath,
            ProjectDocumentationGenerator.VerificationPath,
            ProjectDocumentationGenerator.AttentionPath,
        };

        private static bool IsGeneratedOutputPath(string path)
        {
            return ApprovedGeneratedPaths.Contains(path, StringComparer.Ordinal);
        }

        private static bool MatchesAny(string path, IEnumerable<string> globs)
        {
            return Values(globs).Any(glob => PathMatchesGlob(path, glob));
        }

        private static bool PathMatchesGlob(string path, string glob)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(glob)) return false;
            if (glob.EndsWith("/**", StringComparison.Ordinal))
                return path.StartsWith(glob.Substring(0, glob.Length - 2), StringComparison.Ordinal);
            return MatchSegment(path, 0, glob, 0);
        }

        private static bool MatchSegment(string path, int pathIndex, string glob, int globIndex)
        {
            while (globIndex < glob.Length)
            {
                char token = glob[globIndex];
                if (token == '*')
                {
                    globIndex++;
                    for (int index = pathIndex; index <= path.Length && (index == pathIndex ||
                        (index > 0 && path[index - 1] != '/')); index++)
                        if (MatchSegment(path, index, glob, globIndex)) return true;
                    return false;
                }
                if (pathIndex >= path.Length) return false;
                if (token == '?')
                {
                    if (path[pathIndex] == '/') return false;
                }
                else if (token != path[pathIndex]) return false;
                pathIndex++;
                globIndex++;
            }
            return pathIndex == path.Length;
        }

        private static bool IsExplicitlyExcluded(string path, IEnumerable<ProjectPathExclusion> exclusions)
        {
            return Values(exclusions).Any(exclusion => IsValidExclusion(exclusion) && exclusion.Path == path);
        }

        private static void ValidateExclusions(IEnumerable<ProjectPathExclusion> exclusions, string label,
            List<ProjectQualityIssue> issues)
        {
            foreach (ProjectPathExclusion exclusion in Values(exclusions))
                if (!IsValidExclusion(exclusion))
                    Add(issues, "PQ005", label + "必须使用精确路径和非空理由",
                        exclusion == null || string.IsNullOrWhiteSpace(exclusion.Path) ? label : exclusion.Path);
        }

        private static bool IsValidExclusion(ProjectPathExclusion exclusion)
        {
            return exclusion != null && !string.IsNullOrWhiteSpace(exclusion.Path) &&
                exclusion.Path.IndexOf('*') < 0 && exclusion.Path.IndexOf('?') < 0 &&
                !string.IsNullOrWhiteSpace(exclusion.Reason);
        }

        private static bool IsFrozenFeature(string featureGroupId)
        {
            return string.Equals(featureGroupId, "frozen-2d-regression", StringComparison.Ordinal);
        }

        private static bool IsFrozenScenePath(ProjectQualityCatalog catalog, string path)
        {
            return Values(catalog.FeatureGroups).Any(feature => feature != null &&
                IsFrozenFeature(feature.Id) && Values(feature.ScenePaths).Contains(path));
        }

        private static bool TypeMatches(ProjectTypeRecord type, string typeName)
        {
            return type != null && !string.IsNullOrWhiteSpace(type.FullName) && !string.IsNullOrWhiteSpace(typeName) &&
                (type.FullName == typeName || type.FullName.EndsWith("." + typeName, StringComparison.Ordinal));
        }

        private static void Add(List<ProjectQualityIssue> issues, string code, string message, string path)
        {
            issues.Add(new ProjectQualityIssue
            {
                Code = code,
                Severity = ProjectQualityIssueSeverity.Error,
                PlainChineseMessage = message,
                Path = path ?? string.Empty,
            });
        }

        private static IReadOnlyList<ProjectQualityIssue> Sort(IEnumerable<ProjectQualityIssue> issues)
        {
            return issues.OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.PlainChineseMessage, StringComparer.Ordinal).ToArray();
        }

        internal static IReadOnlyList<ProjectQualityIssue> SortIssuesForTests(IEnumerable<ProjectQualityIssue> issues)
        {
            return Sort(issues);
        }

        private static IEnumerable<T> Values<T>(IEnumerable<T> values)
        {
            return values ?? Enumerable.Empty<T>();
        }
    }
}
