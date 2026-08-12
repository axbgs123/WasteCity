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
            ValidateRequiredSceneFields(json, source);

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

            ValidateRelationships(catalog, source);

            return catalog;
        }

        private static void ValidateRelationships(ProjectQualityCatalog catalog, string source)
        {
            var featureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProjectFeatureGroup feature in catalog.FeatureGroups)
                featureIds.Add(feature.Id);
            foreach (ProjectReuseEntry reuse in catalog.ReuseEntries)
                if (!featureIds.Contains(reuse.FeatureGroupId))
                    Fail(source, "dangling feature group: " + reuse.FeatureGroupId);

            var sceneIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProjectSceneEntry scene in catalog.Scenes)
                sceneIds.Add(scene.Id);
            foreach (ProjectUiEntry ui in catalog.UiEntries)
                if (!sceneIds.Contains(ui.SceneId))
                    Fail(source, "dangling scene: " + ui.SceneId);
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
                    RequirementIds = NormalizeRequirementIds(RequireArray(entry.RequirementIds, source, "feature requirement IDs"), source),
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
                    RequirementIds = NormalizeRequirementIds(RequireArray(entry.RequirementIds, source, "reuse requirement IDs"), source),
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

        private static string[] NormalizeRequirementIds(string[] values, string source)
        {
            var result = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                string value = RequiredText(values[index], source, "requirement ID");
                if (!IsRequirementId(value))
                    Fail(source, "malformed requirement id: " + value);
                result[index] = value;
            }
            return result;
        }

        private static bool IsRequirementId(string value)
        {
            int prefixLength;
            if (value.StartsWith("BUG-", StringComparison.Ordinal) || value.StartsWith("DOC-", StringComparison.Ordinal))
                prefixLength = 4;
            else if (value.StartsWith("IDEA-", StringComparison.Ordinal))
                prefixLength = 5;
            else
                return false;

            if (value.Length != prefixLength + 4) return false;
            for (int index = prefixLength; index < value.Length; index++)
                if (value[index] < '0' || value[index] > '9') return false;
            return true;
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

        private static void ValidateRequiredSceneFields(string json, string source)
        {
            int position = 0;
            SkipWhitespace(json, ref position);
            if (position >= json.Length || json[position++] != '{')
                Fail(source, "catalog root is invalid");

            int scenesStart = -1;
            int scenesEnd = -1;
            while (true)
            {
                SkipWhitespace(json, ref position);
                if (position >= json.Length || json[position] == '}') break;
                string name = ReadJsonString(json, ref position);
                SkipWhitespace(json, ref position);
                if (position >= json.Length || json[position++] != ':') Fail(source, "catalog root is invalid");
                SkipWhitespace(json, ref position);
                int valueStart = position;
                int valueEnd = SkipValue(json, ref position);
                if (name == "Scenes")
                {
                    if (scenesStart >= 0) Fail(source, "duplicate root Scenes");
                    scenesStart = valueStart;
                    scenesEnd = valueEnd;
                }
                SkipWhitespace(json, ref position);
                if (position < json.Length && json[position] == ',') { position++; continue; }
                if (position < json.Length && json[position] == '}') break;
                Fail(source, "catalog root is invalid");
            }

            if (scenesStart < 0 || json[scenesStart] != '[')
                Fail(source, "scenes array is required");

            position = scenesStart + 1;
            while (true)
            {
                SkipWhitespace(json, ref position);
                if (position >= scenesEnd || json[position] == ']') return;
                int sceneStart = position;
                int sceneEnd = SkipValue(json, ref position);
                if (sceneStart >= sceneEnd || json[sceneStart] != '{')
                    Fail(source, "scene is invalid");
                ValidateSceneObject(json, sceneStart, sceneEnd, source);
                SkipWhitespace(json, ref position);
                if (position < scenesEnd && json[position] == ',') { position++; continue; }
                if (position < scenesEnd && json[position] == ']') return;
                Fail(source, "scene array is invalid");
            }
        }

        private static void ValidateSceneObject(string json, int start, int end, string source)
        {
            bool hasEnabled = false;
            bool hasBuildIndex = false;
            int position = start + 1;
            while (true)
            {
                SkipWhitespace(json, ref position);
                if (position >= end || json[position] == '}') break;
                string name = ReadJsonString(json, ref position);
                SkipWhitespace(json, ref position);
                if (position >= end || json[position++] != ':') Fail(source, "scene is invalid");
                SkipWhitespace(json, ref position);
                SkipValue(json, ref position);
                if (name == "EnabledInBuildSettings")
                {
                    if (hasEnabled) Fail(source, "duplicate scene enabled");
                    hasEnabled = true;
                }
                else if (name == "ExpectedBuildIndex")
                {
                    if (hasBuildIndex) Fail(source, "duplicate scene build index");
                    hasBuildIndex = true;
                }
                SkipWhitespace(json, ref position);
                if (position < end && json[position] == ',') { position++; continue; }
                if (position < end && json[position] == '}') break;
                Fail(source, "scene is invalid");
            }
            if (!hasEnabled) Fail(source, "missing scene enabled");
            if (!hasBuildIndex) Fail(source, "missing scene build index");
        }

        private static void SkipWhitespace(string json, ref int position)
        {
            while (position < json.Length && char.IsWhiteSpace(json[position])) position++;
        }

        private static string ReadJsonString(string json, ref int position)
        {
            if (position >= json.Length || json[position++] != '"') return null;
            var value = new System.Text.StringBuilder();
            while (position < json.Length)
            {
                char current = json[position++];
                if (current == '"') return value.ToString();
                if (current == '\\' && position < json.Length)
                {
                    char escaped = json[position++];
                    if (escaped == 'u' && position + 4 <= json.Length)
                    {
                        int codePoint;
                        if (!int.TryParse(json.Substring(position, 4), System.Globalization.NumberStyles.AllowHexSpecifier,
                            System.Globalization.CultureInfo.InvariantCulture, out codePoint)) return null;
                        value.Append((char)codePoint);
                        position += 4;
                    }
                    else
                    {
                        switch (escaped)
                        {
                            case '"': value.Append('"'); break; case '\\': value.Append('\\'); break; case '/': value.Append('/'); break;
                            case 'b': value.Append('\b'); break; case 'f': value.Append('\f'); break; case 'n': value.Append('\n'); break;
                            case 'r': value.Append('\r'); break; case 't': value.Append('\t'); break; default: return null;
                        }
                    }
                    continue;
                }
                value.Append(current);
            }
            return null;
        }

        private static int SkipValue(string json, ref int position)
        {
            SkipWhitespace(json, ref position);
            if (position >= json.Length) return position;
            char opening = json[position];
            if (opening == '"') { ReadJsonString(json, ref position); return position; }
            if (opening != '{' && opening != '[') { while (position < json.Length && ",]} \t\r\n".IndexOf(json[position]) < 0) position++; return position; }
            char closing = opening == '{' ? '}' : ']';
            position++;
            while (position < json.Length)
            {
                SkipWhitespace(json, ref position);
                if (position < json.Length && json[position] == closing) { position++; return position; }
                if (opening == '{') { ReadJsonString(json, ref position); SkipWhitespace(json, ref position); if (position < json.Length && json[position] == ':') position++; }
                SkipValue(json, ref position);
                SkipWhitespace(json, ref position);
                if (position < json.Length && json[position] == ',') position++;
            }
            return position;
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
