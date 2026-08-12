using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Editor.ProjectQuality;

namespace WasteCity.Tests
{
    public sealed class ProjectQualityCatalogTests
    {
        [Test]
        public void LoadFromJson_NormalizesPathsAndReturnsApprovedEnums()
        {
            string json = CatalogJson(
                featureId: "building",
                sourceGlob: "Assets\\_Game\\Scripts\\Building\\**",
                reuseLevel: "Recommended",
                verificationLevel: "FocusedEditMode");

            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromJson(json, "fixture.json");

            Assert.That(catalog.FeatureGroups[0].SourceGlobs[0],
                Is.EqualTo("Assets/_Game/Scripts/Building/**"));
            Assert.That(catalog.ReuseEntries[0].ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            Assert.That(catalog.FeatureGroups[0].MinimumVerification,
                Is.EqualTo(ProjectVerificationLevel.FocusedEditMode));
        }

        [TestCase("duplicate feature id")]
        [TestCase("duplicate reuse id")]
        [TestCase("unknown reuse level")]
        [TestCase("empty Chinese name")]
        [TestCase("absolute repository path")]
        [TestCase("parent traversal")]
        [TestCase("unsupported recursive glob")]
        public void LoadFromJson_RejectsInvalidCatalogWithSourceName(string caseName)
        {
            string json = InvalidCatalogJson(caseName);
            var error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityCatalogLoader.LoadFromJson(json, "bad-catalog.json"));
            StringAssert.Contains("bad-catalog.json", error.Message);
            StringAssert.Contains(ExpectedFragment(caseName), error.Message);
        }

        [TestCase("missing failure summary", "    \"FailureLocationSummary\": \" 先检查建筑定义 \" ,\n", "")]
        [TestCase("empty failure summary", "\"FailureLocationSummary\": \" 先检查建筑定义 \"", "\"FailureLocationSummary\": \"   \"")]
        [TestCase("empty primary source globs", "\"PrimarySourceGlobs\": [\"Assets/_Game/Scripts/Building/**\"]", "\"PrimarySourceGlobs\": []")]
        [TestCase("invalid primary source glob", "Assets/_Game/Scripts/Building/**\"],\n    \"FailureLocationSummary\"", "Assets/_Game/**/Building.cs\"],\n    \"FailureLocationSummary\"")]
        public void LoadFromJson_RejectsInvalidFailureLocationFields(string caseName, string oldValue, string newValue)
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityCatalogLoader.LoadFromJson(CatalogJson().Replace(oldValue, newValue), "failure-location.json"));
            StringAssert.Contains("failure-location.json", error.Message);
            StringAssert.Contains(caseName, error.Message);
        }

        [TestCase("malformed requirement id", "RequirementIds\": [\"DOC-0001\"]", "RequirementIds\": [\"IDEA-01\"]")]
        [TestCase("dangling feature group", "FeatureGroupId\": \"building\"", "FeatureGroupId\": \"missing-feature\"")]
        [TestCase("dangling scene", "SceneId\": \"graybox\"", "SceneId\": \"missing-scene\"")]
        [TestCase("missing scene enabled", "\"EnabledInBuildSettings\": true,\n    ", "")]
        [TestCase("missing scene build index", "\"ExpectedBuildIndex\": 0, ", "")]
        public void LoadFromJson_RejectsNewStrictContractFailures(string caseName, string oldValue, string newValue)
        {
            string json = CatalogJson().Replace(oldValue, newValue);
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityCatalogLoader.LoadFromJson(json, "strict.json"));
            StringAssert.Contains("strict.json", error.Message);
            StringAssert.Contains(caseName, error.Message);
        }

        [TestCase("BUG-0001")]
        [TestCase("DOC-0001")]
        [TestCase("IDEA-0001")]
        public void LoadFromJson_AcceptsEveryControlledRequirementPrefix(string requirementId)
        {
            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromJson(
                CatalogJson(requirementId: requirementId), "requirements.json");
            Assert.That(catalog.FeatureGroups[0].RequirementIds[0], Is.EqualTo(requirementId));
            Assert.That(catalog.ReuseEntries[0].RequirementIds[0], Is.EqualTo(requirementId));
        }

        [TestCase("TASK-0001")]
        [TestCase("BUG-000")]
        [TestCase("BUG-٠٠٠١")]
        [TestCase("DOC-０００１")]
        public void LoadFromJson_RejectsRequirementIdsOutsideExactAsciiGrammar(string requirementId)
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityCatalogLoader.LoadFromJson(CatalogJson(requirementId: requirementId), "requirements.json"));
            StringAssert.Contains("malformed requirement id", error.Message);
        }

        [TestCase("duplicate root Scenes", "duplicate root Scenes", "  \"ReuseEntries\"", "  \"Scenes\": [],\n  \"ReuseEntries\"")]
        [TestCase("duplicate scene enabled", "duplicate scene enabled", "\"EnabledInBuildSettings\": true,", "\"EnabledInBuildSettings\": true, \"EnabledInBuildSettings\": false,")]
        [TestCase("nested wrong scene field", "missing scene enabled", "\"EnabledInBuildSettings\": true,", "\"Metadata\": { \"EnabledInBuildSettings\": true },")]
        public void LoadFromJson_RejectsAmbiguousOrNestedSceneFields(string caseName, string expectedFragment, string oldValue, string newValue)
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityCatalogLoader.LoadFromJson(CatalogJson().Replace(oldValue, newValue), "scene-structure.json"));
            StringAssert.Contains(expectedFragment, error.Message, caseName);
        }

        [Test]
        public void LoadFromJson_DecodesEscapedSceneMemberNames()
        {
            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromJson(
                CatalogJson().Replace("\"EnabledInBuildSettings\"", "\"\\u0045nabledInBuildSettings\""),
                "escaped-scene.json");
            Assert.That(catalog.Scenes[0].EnabledInBuildSettings, Is.True);
        }

        [Test]
        public void PublicApis_AreDeterministicAndDoNotWriteTheCatalog()
        {
            string path = CatalogPath();
            byte[] before = File.ReadAllBytes(path);
            string json = File.ReadAllText(path);

            ProjectQualityCatalog fromFileA = ProjectQualityCatalogLoader.LoadFromFile(path);
            ProjectQualityCatalog fromFileB = ProjectQualityCatalogLoader.LoadFromFile(path);
            ProjectQualityCatalog fromJsonA = ProjectQualityCatalogLoader.LoadFromJson(json, path);
            ProjectQualityCatalog fromJsonB = ProjectQualityCatalogLoader.LoadFromJson(json, path);

            AssertCatalogsEqual(fromFileA, fromFileB);
            AssertCatalogsEqual(fromFileA, fromJsonA);
            AssertCatalogsEqual(fromJsonA, fromJsonB);
            fromJsonB.DocumentationRules[0].PlainChineseReason = "different";
            Assert.That(() => AssertCatalogsEqual(fromJsonA, fromJsonB), Throws.TypeOf<AssertionException>());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void LoadFromFile_RejectsRelativeAndMissingPaths()
        {
            Assert.That(() => ProjectQualityCatalogLoader.LoadFromFile("Docs/Engineering/project-quality-catalog.json"),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(() => ProjectQualityCatalogLoader.LoadFromFile(Path.Combine(ProjectRoot(), "missing.json")),
                Throws.TypeOf<FileNotFoundException>());
        }

        [Test]
        public void LoadFromJson_ConvertsExactExclusionsWithReasons()
        {
            string json = CatalogJson().Replace("\"ExplicitSourceExclusions\": []",
                "\"ExplicitSourceExclusions\": [{\"Path\":\"Assets/_Game/Scripts/Feature/Generated.cs\",\"Reason\":\"生成文件由工具维护\"}]");

            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromJson(json, "exclusions.json");

            Assert.That(catalog.ExplicitSourceExclusions[0].Path,
                Is.EqualTo("Assets/_Game/Scripts/Feature/Generated.cs"));
            Assert.That(catalog.ExplicitSourceExclusions[0].Reason, Is.EqualTo("生成文件由工具维护"));
        }

        [TestCase("Assets/_Game/Scripts/*.cs", "理由", "exact path")]
        [TestCase("Assets/_Game/Scripts/Feature/Generated.cs", "   ", "reason")]
        public void LoadFromJson_RejectsWildcardOrReasonlessExclusion(string path, string reason, string expected)
        {
            string json = CatalogJson().Replace("\"ExplicitSourceExclusions\": []",
                "\"ExplicitSourceExclusions\": [{\"Path\":\"" + path + "\",\"Reason\":\"" + reason + "\"}]");

            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityCatalogLoader.LoadFromJson(json, "exclusions.json"));

            StringAssert.Contains(expected, error.Message);
        }

        [Test]
        public void CommittedCatalog_UsesControlledRequirementsAndMapsCatalogOwnership()
        {
            string docs06 = File.ReadAllText(Path.Combine(ProjectRoot(), "Docs/06-User-Feedback-and-Change-Control-ZH.md"));
            var controlledIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(docs06, @"\b(?:BUG|IDEA|DOC)-\d{4}\b"))
                controlledIds.Add(match.Value);

            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            foreach (ProjectFeatureGroup feature in catalog.FeatureGroups)
            {
                foreach (string requirementId in feature.RequirementIds)
                    Assert.That(controlledIds.Contains(requirementId), Is.True, requirementId);
            }
            foreach (ProjectReuseEntry reuse in catalog.ReuseEntries)
            {
                foreach (string testPath in reuse.RequiredTestFiles)
                    Assert.That(File.Exists(Path.Combine(ProjectRoot(), testPath)), Is.True, testPath);
                foreach (string requirementId in reuse.RequirementIds)
                    Assert.That(controlledIds.Contains(requirementId), Is.True, requirementId);
            }

            ProjectFeatureGroup presentation = FindFeature(catalog, "presentation-art-integration");
            CollectionAssert.Contains(presentation.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/GrayboxVisualSlot.cs");
            ProjectFeatureGroup editor = FindFeature(catalog, "scene-editor-build-performance");
            CollectionAssert.Contains(editor.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs");
            CollectionAssert.DoesNotContain(FindFeature(catalog, "frozen-2d-regression").TestFileGlobs,
                "Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs");
        }

        private static ProjectFeatureGroup FindFeature(ProjectQualityCatalog catalog, string id)
        {
            foreach (ProjectFeatureGroup feature in catalog.FeatureGroups)
                if (feature.Id == id) return feature;
            Assert.Fail("Missing feature: " + id);
            return null;
        }

        private static void AssertCatalogsEqual(ProjectQualityCatalog expected, ProjectQualityCatalog actual)
        {
            Assert.That(actual.SchemaVersion, Is.EqualTo(expected.SchemaVersion));
            Assert.That(actual.FeatureGroups, Has.Length.EqualTo(expected.FeatureGroups.Length));
            for (int index = 0; index < expected.FeatureGroups.Length; index++)
            {
                ProjectFeatureGroup a = expected.FeatureGroups[index]; ProjectFeatureGroup b = actual.FeatureGroups[index];
                Assert.That(b.Id, Is.EqualTo(a.Id)); Assert.That(b.ChineseName, Is.EqualTo(a.ChineseName));
                AssertArraysEqual(a.SourceGlobs, b.SourceGlobs); AssertArraysEqual(a.TestFileGlobs, b.TestFileGlobs);
                AssertArraysEqual(a.ScenePaths, b.ScenePaths); AssertArraysEqual(a.RequirementIds, b.RequirementIds);
                AssertArraysEqual(a.HumanDocumentPaths, b.HumanDocumentPaths); Assert.That(b.MinimumVerification, Is.EqualTo(a.MinimumVerification));
            }
            Assert.That(actual.ReuseEntries, Has.Length.EqualTo(expected.ReuseEntries.Length));
            for (int index = 0; index < expected.ReuseEntries.Length; index++)
            {
                ProjectReuseEntry a = expected.ReuseEntries[index]; ProjectReuseEntry b = actual.ReuseEntries[index];
                Assert.That(b.Id, Is.EqualTo(a.Id)); Assert.That(b.ChineseName, Is.EqualTo(a.ChineseName)); AssertArraysEqual(a.TypeNames, b.TypeNames);
                AssertArraysEqual(a.AssetPaths, b.AssetPaths); Assert.That(b.FeatureGroupId, Is.EqualTo(a.FeatureGroupId));
                Assert.That(b.ReuseLevel, Is.EqualTo(a.ReuseLevel)); Assert.That(b.UseSummary, Is.EqualTo(a.UseSummary));
                Assert.That(b.BoundarySummary, Is.EqualTo(a.BoundarySummary)); AssertArraysEqual(a.RequiredTestFiles, b.RequiredTestFiles); AssertArraysEqual(a.RequirementIds, b.RequirementIds);
            }
            Assert.That(actual.Scenes, Has.Length.EqualTo(expected.Scenes.Length));
            for (int index = 0; index < expected.Scenes.Length; index++)
            {
                ProjectSceneEntry a = expected.Scenes[index]; ProjectSceneEntry b = actual.Scenes[index];
                Assert.That(b.Id, Is.EqualTo(a.Id)); Assert.That(b.ChineseName, Is.EqualTo(a.ChineseName)); Assert.That(b.Path, Is.EqualTo(a.Path));
                Assert.That(b.Purpose, Is.EqualTo(a.Purpose)); Assert.That(b.EnabledInBuildSettings, Is.EqualTo(a.EnabledInBuildSettings));
                Assert.That(b.ExpectedBuildIndex, Is.EqualTo(a.ExpectedBuildIndex)); Assert.That(b.ReuseLevel, Is.EqualTo(a.ReuseLevel));
            }
            Assert.That(actual.UiEntries, Has.Length.EqualTo(expected.UiEntries.Length));
            for (int index = 0; index < expected.UiEntries.Length; index++)
            {
                ProjectUiEntry a = expected.UiEntries[index]; ProjectUiEntry b = actual.UiEntries[index];
                Assert.That(b.Id, Is.EqualTo(a.Id)); Assert.That(b.ChineseName, Is.EqualTo(a.ChineseName)); Assert.That(b.OwnerTypeName, Is.EqualTo(a.OwnerTypeName));
                Assert.That(b.SceneId, Is.EqualTo(a.SceneId)); Assert.That(b.InputPrioritySummary, Is.EqualTo(a.InputPrioritySummary)); AssertArraysEqual(a.RequiredTestFiles, b.RequiredTestFiles);
            }
            Assert.That(actual.DocumentationRules, Has.Length.EqualTo(expected.DocumentationRules.Length));
            for (int index = 0; index < expected.DocumentationRules.Length; index++)
            {
                ProjectDocumentationRule a = expected.DocumentationRules[index]; ProjectDocumentationRule b = actual.DocumentationRules[index];
                Assert.That(b.Id, Is.EqualTo(a.Id)); AssertArraysEqual(a.ChangedPathGlobs, b.ChangedPathGlobs); AssertArraysEqual(a.ReviewDocumentPaths, b.ReviewDocumentPaths); Assert.That(b.PlainChineseReason, Is.EqualTo(a.PlainChineseReason));
            }
            AssertExclusionsEqual(expected.ExplicitSourceExclusions, actual.ExplicitSourceExclusions);
            AssertExclusionsEqual(expected.ExplicitTestExclusions, actual.ExplicitTestExclusions);
        }

        private static void AssertExclusionsEqual(ProjectPathExclusion[] expected, ProjectPathExclusion[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].Path, Is.EqualTo(expected[index].Path));
                Assert.That(actual[index].Reason, Is.EqualTo(expected[index].Reason));
            }
        }

        private static void AssertArraysEqual(string[] expected, string[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++) Assert.That(actual[index], Is.EqualTo(expected[index]));
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string CatalogPath()
        {
            return Path.Combine(ProjectRoot(), "Docs/Engineering/project-quality-catalog.json");
        }

        private static string CatalogJson(
            string featureId = "building",
            string sourceGlob = "Assets/_Game/Scripts/Building/**",
            string reuseLevel = "Recommended",
            string verificationLevel = "FocusedEditMode",
            string requirementId = "DOC-0001")
        {
            return "{\n" +
                   "  \"SchemaVersion\": 1,\n" +
                   "  \"FeatureGroups\": [{\n" +
                   "    \"Id\": \" " + featureId + " \",\n" +
                   "    \"ChineseName\": \" 建造 \",\n" +
                   "    \"SourceGlobs\": [\"" + sourceGlob.Replace("\\", "\\\\") + "\"],\n" +
                   "    \"TestFileGlobs\": [\"Assets/_Game/Tests/EditMode/BuildingGridTests.cs\"],\n" +
                   "    \"ScenePaths\": [\"Assets/_Game/Scenes/GrayboxPrototype3D.unity\"],\n" +
                   "    \"RequirementIds\": [\"" + requirementId + "\"],\n" +
                   "    \"HumanDocumentPaths\": [\"Docs/Engineering/project-quality-catalog.json\"],\n" +
                   "    \"PrimarySourceGlobs\": [\"Assets/_Game/Scripts/Building/**\"],\n" +
                   "    \"FailureLocationSummary\": \" 先检查建筑定义 \" ,\n" +
                   "    \"MinimumVerification\": \"" + verificationLevel + "\"\n" +
                   "  }],\n" +
                   "  \"ReuseEntries\": [{\n" +
                   "    \"Id\": \"building-grid\",\n" +
                   "    \"ChineseName\": \" 建筑网格 \",\n" +
                   "    \"TypeNames\": [\"BuildingGrid\"],\n" +
                   "    \"AssetPaths\": [\"Assets/_Game/Scripts/Building/BuildingGrid.cs\"],\n" +
                   "    \"FeatureGroupId\": \"building\",\n" +
                   "    \"ReuseLevel\": \"" + reuseLevel + "\",\n" +
                   "    \"UseSummary\": \" 用于建筑格位计算 \",\n" +
                   "    \"BoundarySummary\": \" 不负责输入路由 \",\n" +
                   "    \"RequiredTestFiles\": [\"Assets/_Game/Tests/EditMode/BuildingGridTests.cs\"],\n" +
                   "    \"RequirementIds\": [\"" + requirementId + "\"]\n" +
                   "  }],\n" +
                   "  \"Scenes\": [{\n" +
                   "    \"Id\": \"graybox\", \"ChineseName\": \" 灰盒场景 \",\n" +
                   "    \"Path\": \"Assets/_Game/Scenes/GrayboxPrototype3D.unity\",\n" +
                   "    \"Purpose\": \" 运行验证 \", \"EnabledInBuildSettings\": true,\n" +
                   "    \"ExpectedBuildIndex\": 0, \"ReuseLevel\": \"Recommended\"\n" +
                   "  }],\n" +
                   "  \"UiEntries\": [{\n" +
                   "    \"Id\": \"building-ui\", \"ChineseName\": \" 建造界面 \",\n" +
                   "    \"OwnerTypeName\": \"BuildingGrid\", \"SceneId\": \"graybox\",\n" +
                   "    \"InputPrioritySummary\": \" 优先处理界面输入 \",\n" +
                   "    \"RequiredTestFiles\": [\"Assets/_Game/Tests/EditMode/BuildingGridTests.cs\"]\n" +
                   "  }],\n" +
                   "  \"DocumentationRules\": [{\n" +
                   "    \"Id\": \"catalog\",\n" +
                   "    \"ChangedPathGlobs\": [\"Assets/_Game/Scripts/Building/**\"],\n" +
                   "    \"ReviewDocumentPaths\": [\"Docs/Engineering/project-quality-catalog.json\"],\n" +
                   "    \"PlainChineseReason\": \" 建造规则变化需要同步目录 \"\n" +
                   "  }],\n" +
                   "  \"ExplicitSourceExclusions\": [],\n" +
                   "  \"ExplicitTestExclusions\": []\n" +
                   "}";
        }

        private static string InvalidCatalogJson(string caseName)
        {
            string json = CatalogJson();
            switch (caseName)
            {
                case "duplicate feature id":
                    return json.Replace("}],\n  \"ReuseEntries\"", "},{\"Id\":\"building\",\"ChineseName\":\"重复\",\"SourceGlobs\":[],\"TestFileGlobs\":[],\"ScenePaths\":[],\"RequirementIds\":[],\"HumanDocumentPaths\":[],\"MinimumVerification\":\"Compile\"}],\n  \"ReuseEntries\"");
                case "duplicate reuse id":
                    return json.Replace("}],\n  \"Scenes\"", "},{\"Id\":\"building-grid\",\"ChineseName\":\"重复\",\"TypeNames\":[],\"AssetPaths\":[],\"FeatureGroupId\":\"building\",\"ReuseLevel\":\"Recommended\",\"UseSummary\":\"用途\",\"BoundarySummary\":\"边界\",\"RequiredTestFiles\":[],\"RequirementIds\":[]}],\n  \"Scenes\"");
                case "unknown reuse level":
                    return json.Replace("\"ReuseLevel\": \"Recommended\"", "\"ReuseLevel\": \"UnknownLevel\"");
                case "empty Chinese name":
                    return json.Replace("\"ChineseName\": \" 建造 \",", "\"ChineseName\": \"   \",");
                case "absolute repository path":
                    return json.Replace("Assets/_Game/Scripts/Building/**", "/Users/example/Assets/_Game/Scripts/Building/**");
                case "parent traversal":
                    return json.Replace("Assets/_Game/Scripts/Building/**", "Assets/_Game/../Secrets/**");
                case "unsupported recursive glob":
                    return json.Replace("Assets/_Game/Scripts/Building/**", "Assets/_Game/**/Building.cs");
                default:
                    Assert.Fail("Unknown invalid catalog case: " + caseName);
                    return null;
            }
        }

        private static string ExpectedFragment(string caseName)
        {
            return caseName;
        }
    }
}
