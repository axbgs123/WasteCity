using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor.ProjectQuality
{
    public static class ProjectQualityScanner
    {
        private static readonly string[] EntryPointOwnerTypeNames =
        {
            "WasteCity.Editor.FirstArtTerrainAssetBuilder",
            "WasteCity.Editor.FirstArtTerrainEvidenceCapture",
            "WasteCity.Editor.FormalBuildTools",
            "WasteCity.Editor.GrayboxPerformanceProbe",
            "WasteCity.Editor.GrayboxSceneAuthoring",
            "WasteCity.Editor.ProjectQuality.ProjectQualityTools",
        };

        public static ProjectInventorySnapshot Scan(string projectRoot)
        {
            string root = NormalizeProjectRoot(projectRoot);
            List<ProjectFileRecord> files = DiscoverFiles(root);
            List<ProjectAssemblyRecord> assemblies = DiscoverAssemblies(root);
            bool isCurrentProject = IsCurrentProject(root);

            var snapshot = new ProjectInventorySnapshot
            {
                FileRecords = files.ToArray(),
                AssemblyRecords = assemblies.ToArray(),
                AssemblyNames = assemblies.Select(record => record.Name)
                    .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                TypeRecords = new ProjectTypeRecord[0],
                SceneRecords = new ProjectSceneRecord[0],
                TestClasses = new ProjectTestClassRecord[0],
                EditorEntryPoints = new ProjectEditorEntryPointRecord[0],
                ScenePaths = new string[0],
            };

            if (!isCurrentProject)
                return snapshot;

            snapshot.TypeRecords = DiscoverTypes(files).ToArray();
            snapshot.SceneRecords = DiscoverEnabledScenes().ToArray();
            snapshot.ScenePaths = snapshot.SceneRecords.Select(record => record.Path).ToArray();
            snapshot.TestClasses = DiscoverTestClasses(files).ToArray();
            snapshot.EditorEntryPoints = DiscoverEditorEntryPoints().ToArray();
            return snapshot;
        }

        private static string NormalizeProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathRooted(projectRoot))
                throw new ArgumentException("project root must be an absolute path", nameof(projectRoot));

            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("project root does not exist: " + root);
            return root;
        }

        private static bool IsCurrentProject(string root)
        {
            string currentRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(root, currentRoot, StringComparison.Ordinal);
        }

        private static List<ProjectFileRecord> DiscoverFiles(string root)
        {
            var records = new List<ProjectFileRecord>();
            AddFiles(root, "Assets/_Game/Scripts", ProjectFileKind.Production, records);
            AddFiles(root, "Assets/_Game/Editor", ProjectFileKind.Production, records);
            AddFiles(root, "Assets/_Game/Tests/EditMode", ProjectFileKind.EditModeTest, records);
            AddFiles(root, "Assets/_Game/Tests/PlayMode", ProjectFileKind.PlayModeTest, records);
            return records.OrderBy(record => record.Path, StringComparer.Ordinal).ToList();
        }

        private static void AddFiles(string root, string relativeDirectory, ProjectFileKind kind, List<ProjectFileRecord> records)
        {
            string directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory)) return;

            foreach (string path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = ToRelativePath(root, path);
                if (!IsExcludedPath(relativePath))
                    records.Add(new ProjectFileRecord { Path = relativePath, Kind = kind });
            }
        }

        private static List<ProjectAssemblyRecord> DiscoverAssemblies(string root)
        {
            var records = new List<ProjectAssemblyRecord>();
            foreach (string path in Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                string relativePath = ToRelativePath(root, path);
                if (IsExcludedPath(relativePath)) continue;

                AsmdefDto dto;
                try
                {
                    dto = JsonUtility.FromJson<AsmdefDto>(File.ReadAllText(path));
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidDataException("invalid asmdef " + relativePath, exception);
                }

                if (dto == null || string.IsNullOrWhiteSpace(dto.name))
                    throw new InvalidDataException("invalid asmdef " + relativePath + ": name is required");
                records.Add(new ProjectAssemblyRecord { Name = dto.name.Trim(), Path = relativePath });
            }
            return records.OrderBy(record => record.Name, StringComparer.Ordinal)
                .ThenBy(record => record.Path, StringComparer.Ordinal).ToList();
        }

        private static List<ProjectTypeRecord> DiscoverTypes(List<ProjectFileRecord> files)
        {
            var records = new List<ProjectTypeRecord>();
            AddTypes(TypeCache.GetTypesDerivedFrom<MonoBehaviour>(), ProjectTypeKind.MonoBehaviour, files, records);
            AddTypes(TypeCache.GetTypesDerivedFrom<ScriptableObject>(), ProjectTypeKind.ScriptableObject, files, records);
            return records.OrderBy(record => record.FullName, StringComparer.Ordinal)
                .ThenBy(record => record.Kind.ToString(), StringComparer.Ordinal).ToList();
        }

        private static void AddTypes(IEnumerable<Type> types, ProjectTypeKind kind, List<ProjectFileRecord> files, List<ProjectTypeRecord> records)
        {
            foreach (Type type in types)
            {
                if (type == null || type.FullName == null || type.Assembly == null) continue;
                string assemblyName = type.Assembly.GetName().Name;
                if (assemblyName == null || !assemblyName.StartsWith("WasteCity", StringComparison.Ordinal)) continue;
                records.Add(new ProjectTypeRecord
                {
                    FullName = type.FullName,
                    AssemblyName = assemblyName,
                    SourcePath = FindUniqueSourcePath(type.Name, files),
                    Kind = kind,
                });
            }
        }

        private static List<ProjectSceneRecord> DiscoverEnabledScenes()
        {
            var records = new List<ProjectSceneRecord>();
            int enabledIndex = 0;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                records.Add(new ProjectSceneRecord { Path = scene.path.Replace('\\', '/'), BuildIndex = enabledIndex });
                enabledIndex++;
            }
            return records.OrderBy(record => record.Path, StringComparer.Ordinal).ToList();
        }

        private static List<ProjectTestClassRecord> DiscoverTestClasses(List<ProjectFileRecord> files)
        {
            var records = new List<ProjectTestClassRecord>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                if (assemblyName == null || !assemblyName.StartsWith("WasteCity", StringComparison.Ordinal)) continue;
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.FullName == null || !ContainsTestMethod(type)) continue;
                    string sourcePath = FindUniqueSourcePath(type.Name, files);
                    if (string.IsNullOrEmpty(sourcePath))
                        throw new InvalidDataException("test class source was not found: " + type.FullName);
                    records.Add(new ProjectTestClassRecord
                    {
                        FullName = type.FullName,
                        SourcePath = sourcePath,
                        Platform = sourcePath.StartsWith("Assets/_Game/Tests/PlayMode/", StringComparison.Ordinal)
                            ? ProjectTestPlatform.PlayMode
                            : ProjectTestPlatform.EditMode,
                    });
                }
            }
            return records.OrderBy(record => record.FullName, StringComparer.Ordinal)
                .ThenBy(record => record.SourcePath, StringComparer.Ordinal).ToList();
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private static bool ContainsTestMethod(Type type)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                foreach (object attribute in method.GetCustomAttributes(true))
                {
                    Type attributeType = attribute.GetType();
                    string name = attributeType.FullName;
                    if (name == "NUnit.Framework.TestAttribute" ||
                        name == "NUnit.Framework.TestCaseAttribute" ||
                        name == "NUnit.Framework.TestCaseSourceAttribute" ||
                        name == "UnityEngine.TestTools.UnityTestAttribute")
                        return true;
                }
            }
            return false;
        }

        private static List<ProjectEditorEntryPointRecord> DiscoverEditorEntryPoints()
        {
            var records = new List<ProjectEditorEntryPointRecord>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                if (assemblyName == null || !assemblyName.StartsWith("WasteCity", StringComparison.Ordinal)) continue;
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.FullName == null ||
                        Array.IndexOf(EntryPointOwnerTypeNames, type.FullName) < 0) continue;
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (method.GetParameters().Length != 0) continue;
                        records.Add(new ProjectEditorEntryPointRecord
                        {
                            OwnerTypeFullName = type.FullName,
                            MethodName = method.Name,
                        });
                    }
                }
            }
            return records.OrderBy(record => record.OwnerTypeFullName, StringComparer.Ordinal)
                .ThenBy(record => record.MethodName, StringComparer.Ordinal).ToList();
        }

        private static string FindUniqueSourcePath(string className, List<ProjectFileRecord> files)
        {
            var matches = new List<string>();
            string declarationName = RemoveGenericArity(className);
            foreach (ProjectFileRecord file in files)
            {
                int declarationCount = CountClassDeclarations(
                    File.ReadAllText(ToAbsolutePath(file.Path)), declarationName);
                for (int index = 0; index < declarationCount; index++)
                    matches.Add(file.Path);
            }

            if (matches.Count > 1)
                throw new InvalidDataException("ambiguous class source mapping for " + className + ": " + string.Join(", ", matches.OrderBy(path => path, StringComparer.Ordinal).ToArray()));
            return matches.Count == 0 ? string.Empty : matches[0];
        }

        private static string RemoveGenericArity(string className)
        {
            int marker = className.IndexOf('`');
            return marker < 0 ? className : className.Substring(0, marker);
        }

        private static int CountClassDeclarations(string source, string declarationName)
        {
            string activeSource = RemoveInactivePreprocessorBranches(source);
            int count = 0;
            for (int index = 0; index < activeSource.Length;)
            {
                char character = activeSource[index];
                if (char.IsWhiteSpace(character)) { index++; continue; }
                if (character == '/' && index + 1 < activeSource.Length)
                {
                    if (activeSource[index + 1] == '/') { index = SkipLineComment(activeSource, index + 2); continue; }
                    if (activeSource[index + 1] == '*') { index = SkipBlockComment(activeSource, index + 2); continue; }
                }
                if (IsStringStart(activeSource, index)) { index = SkipString(activeSource, index); continue; }
                if (character == '\'') { index = SkipQuotedCharacter(activeSource, index + 1, '\''); continue; }
                if (!IsIdentifierStart(character)) { index++; continue; }

                string identifier = ReadIdentifier(activeSource, ref index);
                if (identifier != "class") continue;
                SkipWhitespace(activeSource, ref index);
                if (index < activeSource.Length && activeSource[index] == '@') index++;
                if (index >= activeSource.Length || !IsIdentifierStart(activeSource[index])) continue;
                if (ReadIdentifier(activeSource, ref index) == declarationName) count++;
            }
            return count;
        }

        private static string RemoveInactivePreprocessorBranches(string source)
        {
            var output = new StringBuilder(source.Length);
            var branches = new Stack<PreprocessorBranch>();
            bool active = true;
            bool insideBlockComment = false;
            bool insideVerbatimString = false;
            int rawStringQuoteCount = 0;
            int position = 0;
            while (position < source.Length)
            {
                int lineEnd = source.IndexOf('\n', position);
                if (lineEnd < 0) lineEnd = source.Length;
                string line = source.Substring(position, lineEnd - position);
                string trimmed = line.TrimStart();
                bool isDirective = trimmed.StartsWith("#", StringComparison.Ordinal) &&
                    (!active || (!insideBlockComment && !insideVerbatimString && rawStringQuoteCount == 0));
                if (isDirective)
                {
                    ProcessDirective(trimmed.Substring(1).TrimStart(), branches, ref active);
                    output.Append(' ', line.Length);
                }
                else if (active)
                    output.Append(line);
                else
                    output.Append(' ', line.Length);

                if (active && !isDirective)
                    AdvancePreprocessorLexicalState(line, ref insideBlockComment,
                        ref insideVerbatimString, ref rawStringQuoteCount);

                if (lineEnd < source.Length) output.Append('\n');
                position = lineEnd + 1;
            }
            return output.ToString();
        }

        private static void AdvancePreprocessorLexicalState(string line, ref bool insideBlockComment,
            ref bool insideVerbatimString, ref int rawStringQuoteCount)
        {
            for (int index = 0; index < line.Length;)
            {
                if (insideBlockComment)
                {
                    int end = line.IndexOf("*/", index, StringComparison.Ordinal);
                    if (end < 0) return;
                    insideBlockComment = false;
                    index = end + 2;
                    continue;
                }
                if (insideVerbatimString)
                {
                    if (line[index] != '"') { index++; continue; }
                    if (index + 1 < line.Length && line[index + 1] == '"') { index += 2; continue; }
                    insideVerbatimString = false;
                    index++;
                    continue;
                }
                if (rawStringQuoteCount > 0)
                {
                    int quoteCount = CountQuotes(line, index);
                    if (quoteCount >= rawStringQuoteCount)
                    {
                        rawStringQuoteCount = 0;
                        index += quoteCount;
                    }
                    else
                        index += Math.Max(quoteCount, 1);
                    continue;
                }
                if (line[index] == '/' && index + 1 < line.Length)
                {
                    if (line[index + 1] == '/') return;
                    if (line[index + 1] == '*') { insideBlockComment = true; index += 2; continue; }
                }
                if (line[index] == '\'') { index = SkipQuotedCharacter(line, index + 1, '\''); continue; }
                if (IsStringStart(line, index))
                {
                    int quote;
                    bool verbatim;
                    int dollarCount;
                    TryGetStringStart(line, index, out quote, out verbatim, out dollarCount);
                    int quoteCount = CountQuotes(line, quote);
                    if (quoteCount >= 3)
                    {
                        bool closed;
                        index = SkipRawString(line, quote + quoteCount, quoteCount, out closed);
                        rawStringQuoteCount = closed ? 0 : quoteCount;
                        continue;
                    }
                    if (verbatim)
                    {
                        bool closed;
                        index = SkipVerbatimString(line, quote + 1, out closed);
                        insideVerbatimString = !closed;
                    }
                    else
                        index = SkipString(line, index);
                    continue;
                }
                index++;
            }
        }

        private static int CountQuotes(string source, int index)
        {
            int count = 0;
            while (index + count < source.Length && source[index + count] == '"') count++;
            return count;
        }

        private static void ProcessDirective(string directive, Stack<PreprocessorBranch> branches, ref bool active)
        {
            string name;
            string condition;
            int separator = directive.IndexOfAny(new[] { ' ', '\t' });
            if (separator < 0) { name = directive; condition = string.Empty; }
            else { name = directive.Substring(0, separator); condition = directive.Substring(separator).Trim(); }

            if (name == "if")
            {
                bool branchActive = active && EvaluatePreprocessorCondition(condition);
                branches.Push(new PreprocessorBranch(active, branchActive));
                active = branchActive;
            }
            else if (name == "elif" && branches.Count > 0)
            {
                PreprocessorBranch branch = branches.Pop();
                bool branchActive = branch.ParentActive && !branch.AnyBranchActive && EvaluatePreprocessorCondition(condition);
                branches.Push(new PreprocessorBranch(branch.ParentActive, branch.AnyBranchActive || branchActive));
                active = branchActive;
            }
            else if (name == "else" && branches.Count > 0)
            {
                PreprocessorBranch branch = branches.Pop();
                bool branchActive = branch.ParentActive && !branch.AnyBranchActive;
                branches.Push(new PreprocessorBranch(branch.ParentActive, true));
                active = branchActive;
            }
            else if (name == "endif" && branches.Count > 0)
            {
                PreprocessorBranch branch = branches.Pop();
                active = branch.ParentActive;
            }
        }

        private static bool EvaluatePreprocessorCondition(string condition)
        {
            condition = condition.Trim();
            if (condition == "true" || condition == "UNITY_EDITOR") return true;
            if (condition == "false") return false;
            if (condition.StartsWith("!", StringComparison.Ordinal))
                return !EvaluatePreprocessorCondition(condition.Substring(1));
            return false;
        }

        private static int SkipLineComment(string source, int index)
        {
            int lineEnd = source.IndexOf('\n', index);
            return lineEnd < 0 ? source.Length : lineEnd;
        }

        private static int SkipBlockComment(string source, int index)
        {
            int end = source.IndexOf("*/", index, StringComparison.Ordinal);
            return end < 0 ? source.Length : end + 2;
        }

        private static bool IsStringStart(string source, int index)
        {
            int quote;
            bool verbatim;
            int dollarCount;
            return TryGetStringStart(source, index, out quote, out verbatim, out dollarCount);
        }

        private static int SkipString(string source, int index)
        {
            int quote;
            bool verbatim;
            int dollarCount;
            if (!TryGetStringStart(source, index, out quote, out verbatim, out dollarCount)) return index;
            int quoteCount = CountQuotes(source, quote);
            if (quoteCount >= 3)
            {
                bool closed;
                if (dollarCount > 0)
                    return SkipRawInterpolatedString(source, quote + quoteCount,
                        quoteCount, dollarCount, out closed);
                return SkipRawString(source, quote + quoteCount, quoteCount, out closed);
            }
            if (dollarCount > 0)
                return SkipInterpolatedString(source, quote + 1, verbatim);
            if (verbatim)
            {
                bool closed;
                return SkipVerbatimString(source, quote + 1, out closed);
            }
            return SkipQuotedCharacter(source, quote + 1, '"');
        }

        private static bool TryGetStringStart(string source, int index, out int quote,
            out bool verbatim, out int dollarCount)
        {
            quote = index;
            verbatim = false;
            dollarCount = 0;
            while (quote < source.Length && (source[quote] == '@' || source[quote] == '$'))
            {
                if (source[quote] == '@')
                {
                    if (verbatim) return false;
                    verbatim = true;
                }
                else
                    dollarCount++;
                quote++;
            }
            if (quote >= source.Length || source[quote] != '"') return false;
            return quote == index || verbatim || dollarCount > 0;
        }

        private static int SkipInterpolatedString(string source, int index, bool verbatim)
        {
            int braceDepth = 0;
            while (index < source.Length)
            {
                char character = source[index];
                if (braceDepth == 0)
                {
                    if (character == '"')
                    {
                        if (verbatim && index + 1 < source.Length && source[index + 1] == '"') { index += 2; continue; }
                        return index + 1;
                    }
                    if (character == '{')
                    {
                        if (index + 1 < source.Length && source[index + 1] == '{') { index += 2; continue; }
                        braceDepth = 1;
                        index++;
                        continue;
                    }
                    if (!verbatim && character == '\\' && index + 1 < source.Length) { index += 2; continue; }
                    index++;
                    continue;
                }

                if (character == '/' && index + 1 < source.Length)
                {
                    if (source[index + 1] == '/') { index = SkipLineComment(source, index + 2); continue; }
                    if (source[index + 1] == '*') { index = SkipBlockComment(source, index + 2); continue; }
                }
                if (IsStringStart(source, index)) { index = SkipString(source, index); continue; }
                if (character == '\'') { index = SkipQuotedCharacter(source, index + 1, '\''); continue; }
                if (character == '{') { braceDepth++; index++; continue; }
                if (character == '}') { braceDepth--; index++; continue; }
                index++;
            }
            return source.Length;
        }

        private static int SkipVerbatimString(string source, int index, out bool closed)
        {
            closed = false;
            while (index < source.Length)
            {
                if (source[index] != '"') { index++; continue; }
                if (index + 1 < source.Length && source[index + 1] == '"') { index += 2; continue; }
                closed = true;
                return index + 1;
            }
            return source.Length;
        }

        private static int SkipRawString(string source, int index, int quoteCount, out bool closed)
        {
            closed = false;
            while (index < source.Length)
            {
                int run = CountQuotes(source, index);
                if (run >= quoteCount)
                {
                    closed = true;
                    return index + run;
                }
                index += Math.Max(run, 1);
            }
            return source.Length;
        }

        private static int SkipRawInterpolatedString(string source, int index,
            int quoteCount, int interpolationBraceCount, out bool closed)
        {
            closed = false;
            int braceDepth = 0;
            while (index < source.Length)
            {
                if (braceDepth == 0)
                {
                    int quoteRun = CountQuotes(source, index);
                    if (quoteRun >= quoteCount)
                    {
                        closed = true;
                        return index + quoteRun;
                    }
                    int braceRun = CountRepeatedCharacter(source, index, '{');
                    if (braceRun == interpolationBraceCount)
                    {
                        braceDepth = 1;
                        index += braceRun;
                        continue;
                    }
                    if (braceRun >= interpolationBraceCount * 2)
                    {
                        index += braceRun;
                        continue;
                    }
                    index += Math.Max(Math.Max(quoteRun, braceRun), 1);
                    continue;
                }

                char character = source[index];
                if (character == '/' && index + 1 < source.Length)
                {
                    if (source[index + 1] == '/') { index = SkipLineComment(source, index + 2); continue; }
                    if (source[index + 1] == '*') { index = SkipBlockComment(source, index + 2); continue; }
                }
                if (IsStringStart(source, index)) { index = SkipString(source, index); continue; }
                if (character == '\'') { index = SkipQuotedCharacter(source, index + 1, '\''); continue; }
                if (character == '{') { braceDepth++; index++; continue; }
                if (character == '}') { braceDepth--; index++; continue; }
                index++;
            }
            return source.Length;
        }

        private static int CountRepeatedCharacter(string source, int index, char character)
        {
            int count = 0;
            while (index + count < source.Length && source[index + count] == character) count++;
            return count;
        }

        private static int SkipQuotedCharacter(string source, int index, char quote, bool verbatim = false)
        {
            while (index < source.Length)
            {
                if (source[index] == quote)
                {
                    if (verbatim && index + 1 < source.Length && source[index + 1] == quote)
                    {
                        index += 2;
                        continue;
                    }
                    return index + 1;
                }
                if (!verbatim && source[index] == '\\' && index + 1 < source.Length) index += 2;
                else index++;
            }
            return source.Length;
        }

        private static void SkipWhitespace(string source, ref int index)
        {
            while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
        }

        private static bool IsIdentifierStart(char character)
        {
            return character == '_' || char.IsLetter(character);
        }

        private static string ReadIdentifier(string source, ref int index)
        {
            int start = index++;
            while (index < source.Length && (source[index] == '_' || char.IsLetterOrDigit(source[index]))) index++;
            return source.Substring(start, index - start);
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            return Path.Combine(Application.dataPath, "..", projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ToRelativePath(string root, string path)
        {
            return Path.GetFullPath(path).Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
        }

        private static bool IsExcludedPath(string relativePath)
        {
            return relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("Library/", StringComparison.Ordinal) ||
                relativePath.IndexOf("/Library/", StringComparison.Ordinal) >= 0;
        }

        [Serializable]
        private sealed class AsmdefDto
        {
            public string name;
        }

        private struct PreprocessorBranch
        {
            public readonly bool ParentActive;
            public readonly bool AnyBranchActive;

            public PreprocessorBranch(bool parentActive, bool anyBranchActive)
            {
                ParentActive = parentActive;
                AnyBranchActive = anyBranchActive;
            }
        }
    }
}
