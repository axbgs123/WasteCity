using System.IO;
using NUnit.Framework;
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
                   "    \"RequirementIds\": [\"IDEA-001\"],\n" +
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
                   "    \"RequirementIds\": [\"DOC-001\"]\n" +
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
