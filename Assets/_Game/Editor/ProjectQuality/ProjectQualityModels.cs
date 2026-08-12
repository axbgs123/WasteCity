using System;
using System.Text;

namespace WasteCity.Editor.ProjectQuality
{
    public enum ProjectReuseLevel
    {
        Recommended,
        ReviewBeforeReuse,
        SceneOnly,
        FrozenRegression,
        ProhibitedForNewWork,
    }

    public enum ProjectVerificationLevel
    {
        FocusedEditMode,
        FocusedPlayMode,
        FullRegression,
        Compile,
        WindowsBuilds,
        Performance,
        HumanPlaytest,
    }

    public enum ProjectFileKind
    {
        Production,
        EditModeTest,
        PlayModeTest,
    }

    public enum ProjectTypeKind
    {
        MonoBehaviour,
        ScriptableObject,
    }

    public enum ProjectTestPlatform
    {
        EditMode,
        PlayMode,
    }

    [Serializable]
    public sealed class ProjectFileRecord
    {
        public string Path;
        public ProjectFileKind Kind;
    }

    [Serializable]
    public sealed class ProjectTypeRecord
    {
        public string FullName;
        public string AssemblyName;
        public string SourcePath;
        public ProjectTypeKind Kind;
    }

    [Serializable]
    public sealed class ProjectAssemblyRecord
    {
        public string Name;
        public string Path;
    }

    [Serializable]
    public sealed class ProjectSceneRecord
    {
        public string Path;
        public int BuildIndex;
    }

    [Serializable]
    public sealed class ProjectTestClassRecord
    {
        public string FullName;
        public string SourcePath;
        public ProjectTestPlatform Platform;
    }

    [Serializable]
    public sealed class ProjectEditorEntryPointRecord
    {
        public string OwnerTypeFullName;
        public string MethodName;
    }

    [Serializable]
    public sealed class ProjectInventorySnapshot
    {
        public ProjectFileRecord[] FileRecords;
        public ProjectTypeRecord[] TypeRecords;
        public ProjectAssemblyRecord[] AssemblyRecords;
        public ProjectSceneRecord[] SceneRecords;
        public ProjectTestClassRecord[] TestClasses;
        public ProjectEditorEntryPointRecord[] EditorEntryPoints;
        public string[] AssemblyNames;
        public string[] ScenePaths;

        public string ToDeterministicJson()
        {
            ValidateCompleteState();
            var builder = new StringBuilder();
            builder.Append('{');
            AppendFiles(builder); builder.Append(',');
            AppendTypes(builder); builder.Append(',');
            AppendAssemblies(builder); builder.Append(',');
            AppendScenes(builder); builder.Append(',');
            AppendTests(builder); builder.Append(',');
            AppendEntryPoints(builder); builder.Append(',');
            AppendStringArray(builder, "AssemblyNames", AssemblyNames); builder.Append(',');
            AppendStringArray(builder, "ScenePaths", ScenePaths);
            builder.Append('}');
            return builder.ToString();
        }

        private void ValidateCompleteState()
        {
            if (FileRecords == null || TypeRecords == null || AssemblyRecords == null ||
                SceneRecords == null || TestClasses == null || EditorEntryPoints == null ||
                AssemblyNames == null || ScenePaths == null)
                throw new InvalidOperationException("inventory snapshot arrays must not be null");
            ValidateRecords(FileRecords, "file");
            ValidateRecords(TypeRecords, "type");
            ValidateRecords(AssemblyRecords, "assembly");
            ValidateRecords(SceneRecords, "scene");
            ValidateRecords(TestClasses, "test");
            ValidateRecords(EditorEntryPoints, "editor entry point");
            ValidateStrings(AssemblyNames, "assembly name");
            ValidateStrings(ScenePaths, "scene path");
        }

        private static void ValidateRecords<T>(T[] values, string name) where T : class
        {
            for (int index = 0; index < values.Length; index++)
                if (values[index] == null)
                    throw new InvalidOperationException("inventory " + name + " record must not be null");
        }

        private static void ValidateStrings(string[] values, string name)
        {
            for (int index = 0; index < values.Length; index++)
                if (values[index] == null)
                    throw new InvalidOperationException("inventory " + name + " must not be null");
        }

        private void AppendFiles(StringBuilder builder)
        {
            AppendPropertyName(builder, "FileRecords"); builder.Append('[');
            for (int index = 0; index < FileRecords.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append('{'); AppendStringProperty(builder, "Path", FileRecords[index].Path); builder.Append(',');
                AppendStringProperty(builder, "Kind", FileRecords[index].Kind.ToString()); builder.Append('}');
            }
            builder.Append(']');
        }

        private void AppendTypes(StringBuilder builder)
        {
            AppendPropertyName(builder, "TypeRecords"); builder.Append('[');
            for (int index = 0; index < TypeRecords.Length; index++)
            {
                if (index > 0) builder.Append(',');
                ProjectTypeRecord record = TypeRecords[index];
                builder.Append('{'); AppendStringProperty(builder, "FullName", record.FullName); builder.Append(',');
                AppendStringProperty(builder, "AssemblyName", record.AssemblyName); builder.Append(',');
                AppendStringProperty(builder, "SourcePath", record.SourcePath); builder.Append(',');
                AppendStringProperty(builder, "Kind", record.Kind.ToString()); builder.Append('}');
            }
            builder.Append(']');
        }

        private void AppendAssemblies(StringBuilder builder)
        {
            AppendPropertyName(builder, "AssemblyRecords"); builder.Append('[');
            for (int index = 0; index < AssemblyRecords.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append('{'); AppendStringProperty(builder, "Name", AssemblyRecords[index].Name); builder.Append(',');
                AppendStringProperty(builder, "Path", AssemblyRecords[index].Path); builder.Append('}');
            }
            builder.Append(']');
        }

        private void AppendScenes(StringBuilder builder)
        {
            AppendPropertyName(builder, "SceneRecords"); builder.Append('[');
            for (int index = 0; index < SceneRecords.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append('{'); AppendStringProperty(builder, "Path", SceneRecords[index].Path); builder.Append(',');
                AppendPropertyName(builder, "BuildIndex"); builder.Append(SceneRecords[index].BuildIndex); builder.Append('}');
            }
            builder.Append(']');
        }

        private void AppendTests(StringBuilder builder)
        {
            AppendPropertyName(builder, "TestClasses"); builder.Append('[');
            for (int index = 0; index < TestClasses.Length; index++)
            {
                if (index > 0) builder.Append(',');
                ProjectTestClassRecord record = TestClasses[index];
                builder.Append('{'); AppendStringProperty(builder, "FullName", record.FullName); builder.Append(',');
                AppendStringProperty(builder, "SourcePath", record.SourcePath); builder.Append(',');
                AppendStringProperty(builder, "Platform", record.Platform.ToString()); builder.Append('}');
            }
            builder.Append(']');
        }

        private void AppendEntryPoints(StringBuilder builder)
        {
            AppendPropertyName(builder, "EditorEntryPoints"); builder.Append('[');
            for (int index = 0; index < EditorEntryPoints.Length; index++)
            {
                if (index > 0) builder.Append(',');
                ProjectEditorEntryPointRecord record = EditorEntryPoints[index];
                builder.Append('{'); AppendStringProperty(builder, "OwnerTypeFullName", record.OwnerTypeFullName); builder.Append(',');
                AppendStringProperty(builder, "MethodName", record.MethodName); builder.Append('}');
            }
            builder.Append(']');
        }

        private static void AppendStringArray(StringBuilder builder, string name, string[] values)
        {
            AppendPropertyName(builder, name); builder.Append('[');
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0) builder.Append(',');
                AppendString(builder, values[index]);
            }
            builder.Append(']');
        }

        private static void AppendStringProperty(StringBuilder builder, string name, string value)
        {
            AppendPropertyName(builder, name); AppendString(builder, value);
        }

        private static void AppendPropertyName(StringBuilder builder, string value)
        {
            AppendString(builder, value); builder.Append(':');
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (value != null)
            {
                for (int index = 0; index < value.Length; index++)
                {
                    char character = value[index];
                    switch (character)
                    {
                        case '\\': builder.Append("\\\\"); break;
                        case '"': builder.Append("\\\""); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (character < 32)
                                builder.Append("\\u").Append(((int)character).ToString("x4"));
                            else
                                builder.Append(character);
                            break;
                    }
                }
            }
            builder.Append('"');
        }
    }

    [Serializable]
    public sealed class ProjectFeatureGroup
    {
        public string Id;
        public string ChineseName;
        public string[] SourceGlobs;
        public string[] TestFileGlobs;
        public string[] ScenePaths;
        public string[] RequirementIds;
        public string[] HumanDocumentPaths;
        public ProjectVerificationLevel MinimumVerification;
    }

    [Serializable]
    public sealed class ProjectReuseEntry
    {
        public string Id;
        public string ChineseName;
        public string[] TypeNames;
        public string[] AssetPaths;
        public string FeatureGroupId;
        public ProjectReuseLevel ReuseLevel;
        public string UseSummary;
        public string BoundarySummary;
        public string[] RequiredTestFiles;
        public string[] RequirementIds;
    }

    [Serializable]
    public sealed class ProjectSceneEntry
    {
        public string Id;
        public string ChineseName;
        public string Path;
        public string Purpose;
        public bool EnabledInBuildSettings;
        public int ExpectedBuildIndex;
        public ProjectReuseLevel ReuseLevel;
    }

    [Serializable]
    public sealed class ProjectUiEntry
    {
        public string Id;
        public string ChineseName;
        public string OwnerTypeName;
        public string SceneId;
        public string InputPrioritySummary;
        public string[] RequiredTestFiles;
    }

    [Serializable]
    public sealed class ProjectDocumentationRule
    {
        public string Id;
        public string[] ChangedPathGlobs;
        public string[] ReviewDocumentPaths;
        public string PlainChineseReason;
    }

    [Serializable]
    public sealed class ProjectQualityCatalog
    {
        public int SchemaVersion;
        public ProjectFeatureGroup[] FeatureGroups;
        public ProjectReuseEntry[] ReuseEntries;
        public ProjectSceneEntry[] Scenes;
        public ProjectUiEntry[] UiEntries;
        public ProjectDocumentationRule[] DocumentationRules;
        public string[] ExplicitSourceExclusions;
        public string[] ExplicitTestExclusions;
    }
}
