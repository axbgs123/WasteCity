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
        public void LoadFromJson_RejectsInvalidCatalogWithSourceName(string caseName)
        {
            string json = InvalidCatalogJson(caseName);
            var error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityCatalogLoader.LoadFromJson(json, "bad-catalog.json"));
            StringAssert.Contains("bad-catalog.json", error.Message);
            StringAssert.Contains(ExpectedFragment(caseName), error.Message);
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
            Assert.That(actual.FeatureGroups.Length, Is.EqualTo(expected.FeatureGroups.Length));
            Assert.That(actual.ReuseEntries.Length, Is.EqualTo(expected.ReuseEntries.Length));
            Assert.That(actual.Scenes.Length, Is.EqualTo(expected.Scenes.Length));
            Assert.That(actual.UiEntries.Length, Is.EqualTo(expected.UiEntries.Length));
            Assert.That(actual.DocumentationRules.Length, Is.EqualTo(expected.DocumentationRules.Length));
            Assert.That(actual.FeatureGroups[0].Id, Is.EqualTo(expected.FeatureGroups[0].Id));
            Assert.That(actual.ReuseEntries[0].Id, Is.EqualTo(expected.ReuseEntries[0].Id));
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
            string verificationLevel = "FocusedEditMode")
        {
            return "{\n" +
                   "  \"SchemaVersion\": 1,\n" +
                   "  \"FeatureGroups\": [{\n" +
                   "    \"Id\": \" " + featureId + " \",\n" +
                   "    \"ChineseName\": \" 建造 \",\n" +
                   "    \"SourceGlobs\": [\"" + sourceGlob.Replace("\\", "\\\\") + "\"],\n" +
                   "    \"TestFileGlobs\": [\"Assets/_Game/Tests/EditMode/BuildingGridTests.cs\"],\n" +
                   "    \"ScenePaths\": [\"Assets/_Game/Scenes/GrayboxPrototype3D.unity\"],\n" +
                   "    \"RequirementIds\": [\"DOC-0001\"],\n" +
                   "    \"HumanDocumentPaths\": [\"Docs/Engineering/project-quality-catalog.json\"],\n" +
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
                   "    \"RequirementIds\": [\"DOC-0001\"]\n" +
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
