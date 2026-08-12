using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Pdb;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("WasteCity.EditModeTests")]

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

        private static readonly string[] SourceIdentityRoots =
        {
            "Assets/_Game/Editor/", "Assets/_Game/Scripts/",
            "Assets/_Game/Tests/EditMode/", "Assets/_Game/Tests/PlayMode/",
        };

        private static readonly string[] TestAssemblyNames =
        {
            "WasteCity.EditModeTests", "WasteCity.PlayModeTests",
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

            Dictionary<string, List<string>> pdb = BuildPdbIndex(root);
            snapshot.TypeRecords = DiscoverTypes(pdb).ToArray();
            snapshot.SceneRecords = DiscoverEnabledScenes().ToArray();
            snapshot.ScenePaths = snapshot.SceneRecords.Select(record => record.Path).ToArray();
            snapshot.TestClasses = DiscoverTestClasses(pdb).ToArray();
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

        private static List<ProjectTypeRecord> DiscoverTypes(Dictionary<string, List<string>> pdb)
        {
            var records = new List<ProjectTypeRecord>();
            AddTypes(TypeCache.GetTypesDerivedFrom<MonoBehaviour>(), ProjectTypeKind.MonoBehaviour, pdb, records);
            AddTypes(TypeCache.GetTypesDerivedFrom<ScriptableObject>(), ProjectTypeKind.ScriptableObject, pdb, records);
            return records.OrderBy(record => record.FullName, StringComparer.Ordinal)
                .ThenBy(record => record.Kind.ToString(), StringComparer.Ordinal).ToList();
        }

        private static void AddTypes(IEnumerable<Type> types, ProjectTypeKind kind, Dictionary<string, List<string>> pdb, List<ProjectTypeRecord> records)
        {
            foreach (Type type in types)
            {
                if (type == null || type.FullName == null || type.Assembly == null) continue;
                string assemblyName = type.Assembly.GetName().Name;
                if (assemblyName == null || !assemblyName.StartsWith("WasteCity", StringComparison.Ordinal) ||
                    Array.IndexOf(TestAssemblyNames, assemblyName) >= 0) continue;
                records.Add(new ProjectTypeRecord
                {
                    FullName = type.FullName,
                    AssemblyName = assemblyName,
                    SourcePath = ResolvePdb(type.FullName, pdb),
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

        private static List<ProjectTestClassRecord> DiscoverTestClasses(Dictionary<string, List<string>> pdb)
        {
            var records = new List<ProjectTestClassRecord>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                if (assemblyName == null || !assemblyName.StartsWith("WasteCity", StringComparison.Ordinal)) continue;
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.FullName == null || !ContainsTestMethod(type)) continue;
                    string sourcePath = ResolvePdb(type.FullName, pdb);
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

        private static Dictionary<string, List<string>> BuildPdbIndex(string root)
        {
            var documents = new List<SourceDocumentInput>();
            var zeroSequenceTypeNames = new HashSet<string>(DiscoverSourceIdentityTypeNames(), StringComparer.Ordinal);
            var sequencePointTypeNames = new HashSet<string>(StringComparer.Ordinal);
            string assemblies = Path.Combine(root, "Library", "ScriptAssemblies");
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name == null || !name.StartsWith("WasteCity", StringComparison.Ordinal)) continue;
                string dllPath = Path.Combine(assemblies, name + ".dll");
                string pdbPath = Path.Combine(assemblies, name + ".pdb");
                if (!File.Exists(dllPath) || !File.Exists(pdbPath))
                    throw new InvalidDataException("compiled symbols missing for assembly " + name);
                using (FileStream dll = File.OpenRead(dllPath))
                using (FileStream pdb = File.OpenRead(pdbPath))
                using (AssemblyDefinition definition = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters
                {
                    ReadSymbols = true,
                    SymbolStream = pdb,
                    SymbolReaderProvider = new DefaultSymbolReaderProvider(false),
                }))
                {
                    foreach (TypeDefinition type in AllTypeDefinitions(definition.MainModule.Types))
                    {
                        string typeName = ToRuntimeFullName(type);
                        if (!zeroSequenceTypeNames.Contains(typeName)) continue;
                        string[] urls = type.Methods.Where(method => method.DebugInformation.HasSequencePoints)
                            .SelectMany(method => method.DebugInformation.SequencePoints)
                            .Where(point => point.Document != null)
                            .Select(point => point.Document.Url).Distinct(StringComparer.Ordinal).ToArray();
                        if (urls.Length == 0) continue;
                        sequencePointTypeNames.Add(typeName);
                        foreach (string url in urls)
                            documents.Add(new SourceDocumentInput(typeName, url));
                    }
                }
            }
            var fallbacks = new List<SourceFallbackInput>();
            foreach (string path in AssetDatabase.FindAssets("t:MonoScript", SourceIdentityRoots
                .Select(rootPath => rootPath.TrimEnd('/')).ToArray())
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script == null ? null : script.GetClass();
                if (type != null && zeroSequenceTypeNames.Contains(type.FullName) &&
                    !sequencePointTypeNames.Contains(type.FullName))
                    fallbacks.Add(new SourceFallbackInput(type.FullName, path));
            }
            return BuildSourceIndex(root, documents, fallbacks);
        }

        private static IEnumerable<string> DiscoverSourceIdentityTypeNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Concat(TypeCache.GetTypesDerivedFrom<ScriptableObject>()))
            {
                if (type == null || type.FullName == null || type.Assembly == null) continue;
                string assemblyName = type.Assembly.GetName().Name;
                if (assemblyName != null && assemblyName.StartsWith("WasteCity", StringComparison.Ordinal) &&
                    Array.IndexOf(TestAssemblyNames, assemblyName) < 0)
                    names.Add(type.FullName);
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                if (assemblyName == null || !assemblyName.StartsWith("WasteCity", StringComparison.Ordinal)) continue;
                foreach (Type type in GetLoadableTypes(assembly))
                    if (type != null && type.FullName != null && ContainsTestMethod(type)) names.Add(type.FullName);
            }
            return names;
        }

        private static IEnumerable<TypeDefinition> AllTypeDefinitions(IEnumerable<TypeDefinition> types)
        {
            foreach (TypeDefinition type in types)
            {
                yield return type;
                foreach (TypeDefinition nested in AllTypeDefinitions(type.NestedTypes)) yield return nested;
            }
        }

        private static string ToRuntimeFullName(TypeDefinition type)
        {
            return type.FullName.Replace('/', '+');
        }

        internal static Dictionary<string, List<string>> BuildSourceIndexForTests(string root,
            IEnumerable<SourceDocumentInput> documents, IEnumerable<SourceFallbackInput> fallbacks)
        {
            return BuildSourceIndex(root, documents, fallbacks);
        }

        private static Dictionary<string, List<string>> BuildSourceIndex(string root,
            IEnumerable<SourceDocumentInput> documents, IEnumerable<SourceFallbackInput> fallbacks)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (SourceDocumentInput document in documents)
                AddSourcePath(map, document.TypeFullName, NormalizeSourcePath(root, document.DocumentPath));
            foreach (SourceFallbackInput fallback in fallbacks)
                AddSourcePath(map, fallback.TypeFullName, NormalizeSourcePath(root, fallback.SourcePath));
            foreach (List<string> paths in map.Values) paths.Sort(StringComparer.Ordinal);
            return map;
        }

        private static string NormalizeSourcePath(string root, string documentUrl)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.IsPathRooted(documentUrl) ? documentUrl : Path.Combine(fullRoot, documentUrl));
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("symbol source is outside project: " + documentUrl);
            string path = fullPath.Substring(prefix.Length).Replace('\\', '/');
            if (!SourceIdentityRoots.Any(rootPath => path.StartsWith(rootPath, StringComparison.Ordinal)))
                throw new InvalidDataException("symbol source is outside approved roots: " + path);
            if (!File.Exists(fullPath))
                throw new InvalidDataException("symbol source does not exist: " + path);
            return path;
        }

        private static void AddSourcePath(Dictionary<string, List<string>> map, string name, string path)
        {
            List<string> paths;
            if (!map.TryGetValue(name, out paths)) { paths = new List<string>(); map.Add(name, paths); }
            if (!paths.Contains(path)) paths.Add(path);
        }

        private static string ResolvePdb(string name, Dictionary<string, List<string>> map)
        {
            List<string> paths;
            if (!map.TryGetValue(name, out paths) || paths.Count == 0)
                throw new InvalidDataException("test class source was not found: " + name);
            if (paths.Count != 1)
                throw new InvalidDataException("ambiguous class source mapping for " + name + ": " + string.Join(", ", paths));
            return paths[0];
        }

        internal static string ResolveSourcePathForTests(string name, Dictionary<string, List<string>> map)
        {
            return ResolvePdb(name, map);
        }

        internal sealed class SourceDocumentInput
        {
            public readonly string TypeFullName;
            public readonly string DocumentPath;
            public SourceDocumentInput(string typeFullName, string documentPath)
            {
                TypeFullName = typeFullName;
                DocumentPath = documentPath;
            }
        }

        internal sealed class SourceFallbackInput
        {
            public readonly string TypeFullName;
            public readonly string SourcePath;
            public SourceFallbackInput(string typeFullName, string sourcePath)
            {
                TypeFullName = typeFullName;
                SourcePath = sourcePath;
            }
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
