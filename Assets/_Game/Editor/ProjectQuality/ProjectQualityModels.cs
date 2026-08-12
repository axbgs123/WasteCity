using System;

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
