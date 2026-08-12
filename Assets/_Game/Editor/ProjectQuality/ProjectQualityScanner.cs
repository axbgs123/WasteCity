using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor.ProjectQuality
{
    public static class ProjectQualityScanner
    {
        private static readonly string[] EntryPointOwnerNames =
        {
            "FirstArtTerrainAssetBuilder",
            "FirstArtTerrainEvidenceCapture",
            "FormalBuildTools",
            "GrayboxPerformanceProbe",
            "GrayboxSceneAuthoring",
            "ProjectQualityTools",
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
                        Array.IndexOf(EntryPointOwnerNames, type.Name) < 0) continue;
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
            Regex pattern = new Regex("\\bclass\\s+" + Regex.Escape(className) + "\\b");
            foreach (ProjectFileRecord file in files)
            {
                if (pattern.IsMatch(File.ReadAllText(ToAbsolutePath(file.Path))))
                    matches.Add(file.Path);
            }

            if (matches.Count > 1)
                throw new InvalidDataException("ambiguous class source mapping for " + className + ": " + string.Join(", ", matches.OrderBy(path => path, StringComparer.Ordinal).ToArray()));
            return matches.Count == 0 ? string.Empty : matches[0];
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
    }
}
