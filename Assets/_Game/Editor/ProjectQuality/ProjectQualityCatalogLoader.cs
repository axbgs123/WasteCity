using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WasteCity.Editor.ProjectQuality
{
    public static class ProjectQualityCatalogLoader
    {
        public static ProjectQualityCatalog LoadFromFile(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !Path.IsPathRooted(absolutePath))
                throw new InvalidDataException("catalog path must be absolute");

            return LoadFromJson(File.ReadAllText(absolutePath), absolutePath);
        }

        public static ProjectQualityCatalog LoadFromJson(string json, string sourceName)
        {
            string source = string.IsNullOrWhiteSpace(sourceName) ? "catalog" : sourceName.Trim();
            if (string.IsNullOrWhiteSpace(json))
                Fail(source, "catalog JSON is empty");

            CatalogDto dto;
            try
            {
                dto = JsonUtility.FromJson<CatalogDto>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(source + ": invalid catalog JSON", exception);
            }

            if (dto == null)
                Fail(source, "invalid catalog JSON");
            if (dto.SchemaVersion != 1)
                Fail(source, "schema version must be 1");

            RequireArray(dto.FeatureGroups, source, "feature groups");
            RequireArray(dto.ReuseEntries, source, "reuse entries");
            RequireArray(dto.Scenes, source, "scenes");
            RequireArray(dto.UiEntries, source, "UI entries");
            RequireArray(dto.DocumentationRules, source, "documentation rules");
            RequireArray(dto.ExplicitSourceExclusions, source, "explicit source exclusions");
            RequireArray(dto.ExplicitTestExclusions, source, "explicit test exclusions");

            var featureIds = new HashSet<string>(StringComparer.Ordinal);
            var reuseIds = new HashSet<string>(StringComparer.Ordinal);
            var sceneIds = new HashSet<string>(StringComparer.Ordinal);
            var uiIds = new HashSet<string>(StringComparer.Ordinal);
            var documentationRuleIds = new HashSet<string>(StringComparer.Ordinal);

            var catalog = new ProjectQualityCatalog
            {
                SchemaVersion = dto.SchemaVersion,
                FeatureGroups = ConvertFeatureGroups(dto.FeatureGroups, source, featureIds),
                ReuseEntries = ConvertReuseEntries(dto.ReuseEntries, source, reuseIds),
                Scenes = ConvertScenes(dto.Scenes, source, sceneIds),
                UiEntries = ConvertUiEntries(dto.UiEntries, source, uiIds),
                DocumentationRules = ConvertDocumentationRules(dto.DocumentationRules, source, documentationRuleIds),
                ExplicitSourceExclusions = NormalizePaths(dto.ExplicitSourceExclusions, source, "explicit source exclusion"),
                ExplicitTestExclusions = NormalizePaths(dto.ExplicitTestExclusions, source, "explicit test exclusion"),
            };

            return catalog;
        }

        private static ProjectFeatureGroup[] ConvertFeatureGroups(FeatureGroupDto[] entries, string source, HashSet<string> ids)
        {
            var result = new ProjectFeatureGroup[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                FeatureGroupDto entry = RequireEntry(entries[index], source, "feature group");
                result[index] = new ProjectFeatureGroup
                {
                    Id = UniqueId(entry.Id, ids, source, "feature"),
                    ChineseName = HumanText(entry.ChineseName, source, "Chinese name"),
                    SourceGlobs = NormalizePaths(RequireArray(entry.SourceGlobs, source, "feature source globs"), source, "source glob"),
                    TestFileGlobs = NormalizePaths(RequireArray(entry.TestFileGlobs, source, "feature test file globs"), source, "test file glob"),
                    ScenePaths = NormalizePaths(RequireArray(entry.ScenePaths, source, "feature scene paths"), source, "scene path"),
                    RequirementIds = NormalizeTextArray(RequireArray(entry.RequirementIds, source, "feature requirement IDs"), source, "requirement ID"),
                    HumanDocumentPaths = NormalizePaths(RequireArray(entry.HumanDocumentPaths, source, "feature human document paths"), source, "human document path"),
                    MinimumVerification = ParseVerification(entry.MinimumVerification, source),
                };
            }
            return result;
        }

        private static ProjectReuseEntry[] ConvertReuseEntries(ReuseEntryDto[] entries, string source, HashSet<string> ids)
        {
            var result = new ProjectReuseEntry[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                ReuseEntryDto entry = RequireEntry(entries[index], source, "reuse entry");
                result[index] = new ProjectReuseEntry
                {
                    Id = UniqueId(entry.Id, ids, source, "reuse"),
                    ChineseName = HumanText(entry.ChineseName, source, "Chinese name"),
                    TypeNames = NormalizeTextArray(RequireArray(entry.TypeNames, source, "reuse type names"), source, "type name"),
                    AssetPaths = NormalizePaths(RequireArray(entry.AssetPaths, source, "reuse asset paths"), source, "asset path"),
                    FeatureGroupId = RequiredText(entry.FeatureGroupId, source, "feature group ID"),
                    ReuseLevel = ParseReuse(entry.ReuseLevel, source),
                    UseSummary = HumanText(entry.UseSummary, source, "use summary"),
                    BoundarySummary = HumanText(entry.BoundarySummary, source, "boundary summary"),
                    RequiredTestFiles = NormalizePaths(RequireArray(entry.RequiredTestFiles, source, "required test files"), source, "required test file"),
                    RequirementIds = NormalizeTextArray(RequireArray(entry.RequirementIds, source, "reuse requirement IDs"), source, "requirement ID"),
                };
            }
            return result;
        }

        private static ProjectSceneEntry[] ConvertScenes(SceneEntryDto[] entries, string source, HashSet<string> ids)
        {
            var result = new ProjectSceneEntry[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                SceneEntryDto entry = RequireEntry(entries[index], source, "scene");
                result[index] = new ProjectSceneEntry
                {
                    Id = UniqueId(entry.Id, ids, source, "scene"),
                    ChineseName = HumanText(entry.ChineseName, source, "Chinese name"),
                    Path = NormalizePath(entry.Path, source, "scene path"),
                    Purpose = HumanText(entry.Purpose, source, "scene purpose"),
                    EnabledInBuildSettings = entry.EnabledInBuildSettings,
                    ExpectedBuildIndex = entry.ExpectedBuildIndex,
                    ReuseLevel = ParseReuse(entry.ReuseLevel, source),
                };
            }
            return result;
        }

        private static ProjectUiEntry[] ConvertUiEntries(UiEntryDto[] entries, string source, HashSet<string> ids)
        {
            var result = new ProjectUiEntry[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                UiEntryDto entry = RequireEntry(entries[index], source, "UI entry");
                result[index] = new ProjectUiEntry
                {
                    Id = UniqueId(entry.Id, ids, source, "UI"),
                    ChineseName = HumanText(entry.ChineseName, source, "Chinese name"),
                    OwnerTypeName = RequiredText(entry.OwnerTypeName, source, "owner type name"),
                    SceneId = RequiredText(entry.SceneId, source, "scene ID"),
                    InputPrioritySummary = HumanText(entry.InputPrioritySummary, source, "input priority summary"),
                    RequiredTestFiles = NormalizePaths(RequireArray(entry.RequiredTestFiles, source, "UI required test files"), source, "required test file"),
                };
            }
            return result;
        }

        private static ProjectDocumentationRule[] ConvertDocumentationRules(DocumentationRuleDto[] entries, string source, HashSet<string> ids)
        {
            var result = new ProjectDocumentationRule[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                DocumentationRuleDto entry = RequireEntry(entries[index], source, "documentation rule");
                result[index] = new ProjectDocumentationRule
                {
                    Id = UniqueId(entry.Id, ids, source, "documentation rule"),
                    ChangedPathGlobs = NormalizePaths(RequireArray(entry.ChangedPathGlobs, source, "changed path globs"), source, "changed path glob"),
                    ReviewDocumentPaths = NormalizePaths(RequireArray(entry.ReviewDocumentPaths, source, "review document paths"), source, "review document path"),
                    PlainChineseReason = HumanText(entry.PlainChineseReason, source, "plain Chinese reason"),
                };
            }
            return result;
        }

        private static string UniqueId(string value, HashSet<string> ids, string source, string kind)
        {
            string id = RequiredText(value, source, kind + " id");
            if (!ids.Add(id))
                Fail(source, "duplicate " + kind + " id: " + id);
            return id;
        }

        private static T RequireEntry<T>(T entry, string source, string label) where T : class
        {
            if (entry == null)
                Fail(source, label + " is null");
            return entry;
        }

        private static T[] RequireArray<T>(T[] values, string source, string label)
        {
            if (values == null)
                Fail(source, label + " array is required");
            return values;
        }

        private static string[] NormalizeTextArray(string[] values, string source, string label)
        {
            var result = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
                result[index] = RequiredText(values[index], source, label);
            return result;
        }

        private static string[] NormalizePaths(string[] values, string source, string label)
        {
            var result = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
                result[index] = NormalizePath(values[index], source, label);
            return result;
        }

        private static string NormalizePath(string value, string source, string label)
        {
            string path = RequiredText(value, source, label).Replace('\\', '/');
            if (path.StartsWith("/", StringComparison.Ordinal) ||
                (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '/'))
                Fail(source, "absolute repository path: " + path);

            string[] segments = path.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index] == "..")
                    Fail(source, "parent traversal: " + path);
            }
            return path;
        }

        private static string RequiredText(string value, string source, string label)
        {
            if (value == null)
                Fail(source, label + " is required");
            string trimmed = value.Trim();
            if (trimmed.Length == 0)
                Fail(source, label + " is empty");
            return trimmed;
        }

        private static string HumanText(string value, string source, string label)
        {
            if (string.Equals(label, "Chinese name", StringComparison.Ordinal) &&
                (value == null || value.Trim().Length == 0))
                Fail(source, "empty Chinese name");
            return RequiredText(value, source, label);
        }

        private static ProjectReuseLevel ParseReuse(string value, string source)
        {
            ProjectReuseLevel parsed;
            if (!Enum.TryParse(value, false, out parsed) || !Enum.IsDefined(typeof(ProjectReuseLevel), parsed))
                Fail(source, "unknown reuse level: " + value);
            return parsed;
        }

        private static ProjectVerificationLevel ParseVerification(string value, string source)
        {
            ProjectVerificationLevel parsed;
            if (!Enum.TryParse(value, false, out parsed) || !Enum.IsDefined(typeof(ProjectVerificationLevel), parsed))
                Fail(source, "unknown verification level: " + value);
            return parsed;
        }

        private static void Fail(string source, string message)
        {
            throw new InvalidDataException(source + ": " + message);
        }

        [Serializable]
        private sealed class CatalogDto
        {
            public int SchemaVersion;
            public FeatureGroupDto[] FeatureGroups;
            public ReuseEntryDto[] ReuseEntries;
            public SceneEntryDto[] Scenes;
            public UiEntryDto[] UiEntries;
            public DocumentationRuleDto[] DocumentationRules;
            public string[] ExplicitSourceExclusions;
            public string[] ExplicitTestExclusions;
        }

        [Serializable]
        private sealed class FeatureGroupDto
        {
            public string Id;
            public string ChineseName;
            public string[] SourceGlobs;
            public string[] TestFileGlobs;
            public string[] ScenePaths;
            public string[] RequirementIds;
            public string[] HumanDocumentPaths;
            public string MinimumVerification;
        }

        [Serializable]
        private sealed class ReuseEntryDto
        {
            public string Id;
            public string ChineseName;
            public string[] TypeNames;
            public string[] AssetPaths;
            public string FeatureGroupId;
            public string ReuseLevel;
            public string UseSummary;
            public string BoundarySummary;
            public string[] RequiredTestFiles;
            public string[] RequirementIds;
        }

        [Serializable]
        private sealed class SceneEntryDto
        {
            public string Id;
            public string ChineseName;
            public string Path;
            public string Purpose;
            public bool EnabledInBuildSettings;
            public int ExpectedBuildIndex;
            public string ReuseLevel;
        }

        [Serializable]
        private sealed class UiEntryDto
        {
            public string Id;
            public string ChineseName;
            public string OwnerTypeName;
            public string SceneId;
            public string InputPrioritySummary;
            public string[] RequiredTestFiles;
        }

        [Serializable]
        private sealed class DocumentationRuleDto
        {
            public string Id;
            public string[] ChangedPathGlobs;
            public string[] ReviewDocumentPaths;
            public string PlainChineseReason;
        }
    }
}
