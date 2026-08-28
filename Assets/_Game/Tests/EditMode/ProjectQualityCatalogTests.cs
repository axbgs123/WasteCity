using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            ProjectFeatureGroup terrain = FindFeature(
                catalog,
                "world-terrain");
            CollectionAssert.Contains(
                terrain.SourceGlobs,
                "ArtSource/FirstPass/Environment/Terrain/**");
            CollectionAssert.Contains(
                terrain.PrimarySourceGlobs,
                "ArtSource/FirstPass/Environment/Terrain/**");
            CollectionAssert.Contains(
                terrain.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FirstArtTerrainVisualStyleTests.cs");
            CollectionAssert.Contains(
                terrain.RequirementIds,
                "IDEA-0018");
            CollectionAssert.Contains(
                terrain.HumanDocumentPaths,
                "Docs/Art/IDEA-0018/IDEA-0018-Terrain-Visual-Asset-Record-ZH.md");
            CollectionAssert.Contains(
                terrain.HumanDocumentPaths,
                "Docs/superpowers/specs/2026-08-24-idea-0018-civilization-map-ui-design.md");
            ProjectFeatureGroup editor = FindFeature(catalog, "scene-editor-build-performance");
            CollectionAssert.Contains(editor.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs");
            CollectionAssert.DoesNotContain(FindFeature(catalog, "legacy-rules-compatibility").TestFileGlobs,
                "Assets/_Game/Tests/EditMode/ProjectQualityCatalogTests.cs");

            string[] expectedFailureLocations =
            {
                "foundation-clock|先检查时钟、会话、资源与稳定标识|Assets/_Game/Scripts/Core/**|Assets/_Game/Scripts/Content/StableId.cs",
                "world-terrain|先检查地图模型、地形规则和世界投影|Assets/_Game/Scripts/World/**|Assets/_Game/Scripts/Graybox3D/FormalWorldGenerationCatalog3D.cs|Assets/_Game/Scripts/Graybox3D/FormalWorldGenerator3D.cs|Assets/_Game/Art/FirstPass/Environment/Terrain/**|ArtSource/FirstPass/Environment/Terrain/**",
                "city-navigation-deployment|先检查城市规则、寻路、部署状态和场景接线|Assets/_Game/Scripts/City/**|Assets/_Game/Scripts/Graybox3D/GrayboxMobileCityController3D.cs",
                "leader-direct-control|先检查领袖状态、控制切换和场景输入接线|Assets/_Game/Scripts/Leader/**|Assets/_Game/Scripts/Graybox3D/GrayboxDirectControlCoordinator.cs",
                "building-construction-evacuation|先检查建筑定义、建造限制、放置会话和场景接线|Assets/_Game/Scripts/Building/**|Assets/_Game/Scripts/Graybox3D/Building/*.cs",
                "ui-input|先检查焦点、输入优先级、界面组件和真实场景引用|Assets/_Game/Scripts/UI/**|Assets/_Game/Scripts/Graybox3D/Building/GrayboxProgressionHud*.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxFateSelection*.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxFateOperations*.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationAdvancement*.cs|Assets/_Game/Scripts/Graybox3D/GrayboxInputRouter.cs|Assets/_Game/Scripts/Graybox3D/Usability/**",
                "economy-production-logistics|先检查库存、生产循环、物流网络和建筑接线|Assets/_Game/Scripts/Economy/**|Assets/_Game/Scripts/Building/LogisticsNetworkModel.cs|Assets/_Game/Scripts/Progression/FormalVoidDebtRuntime.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxPocketUniverseFateController3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxVoidDebtController3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxVoidDebtAttentionController3D.cs",
                "research-population|先检查研究、人口、关注度、命轨、文明等级与升阶真值|Assets/_Game/Scripts/Research/**|Assets/_Game/Scripts/Population/**|Assets/_Game/Scripts/Progression/**|Assets/_Game/Scripts/Graybox3D/Building/GrayboxProgressionEventRouter3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxPocketUniverseFateController3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxVoidDebtController3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxRewindAnchorService3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxPocketUniverseCollapseResolver3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxVoidDebtAttentionController3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxFateSelection*.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxAttentionPressure*.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxCivilizationAdvancement*.cs",
                "combat-routes|先检查战斗规则、路线内容、单位状态和事件接线|Assets/_Game/Scripts/Combat/**|Assets/_Game/Scripts/Defense/**|Assets/_Game/Scripts/Progression/AttentionPressureCatalog.cs|Assets/_Game/Scripts/Progression/AttentionPressureRuntime.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxAttentionPressure*.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefense*.cs|Assets/_Game/Scripts/Content/RouteContentDisplayCatalog.cs",
                "persistence-migration|先检查存档格式、迁移步骤和读写边界|Assets/_Game/Scripts/Persistence/**|Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveRuntimeHost3D.cs|Assets/_Game/Scripts/Graybox3D/Building/GrayboxRewindAnchorService3D.cs|Assets/_Game/Scripts/Progression/FormalRewindAnchorMetadataRuntime.cs|Assets/_Game/Scripts/Graybox3D/Usability/GrayboxFormalSaveEntryController3D.cs",
                "presentation-art-integration|先检查视觉槽、材质接入、投影与相机场景引用|Assets/_Game/Scripts/Presentation/**|Assets/_Game/Scripts/ArtIntegration3D/**|Assets/_Game/Scripts/Graybox3D/FormalWorldPresentationScaleProfile3D.cs",
                "scene-editor-build-performance|先检查编辑工具、场景生成、构建配置和性能边界|Assets/_Game/Editor/ProjectQuality/**|Assets/_Game/Editor/FormalBuildTools.cs|Assets/_Game/Editor/GrayboxSceneAuthoring.cs",
                "civilization-expansion|先检查军队制造/命令/远征、settlement与运输、角色生命/继承/外交，再检查schema 34适配与M/N/P接线|Assets/_Game/Scripts/CivilizationExpansion/**|Assets/_Game/Scripts/Combat/Army*.cs|Assets/_Game/Scripts/Leader/CivilizationExpansion/**|Assets/_Game/Scripts/World/CivilizationExpansion/**",
                "legacy-rules-compatibility|先检查历史规则、schema 1–30 兼容与固定回归样本|Assets/_Game/Scripts/Legacy/**|Assets/_Game/Scripts/Persistence/Legacy2D/**",
            };
            CollectionAssert.AreEqual(expectedFailureLocations, catalog.FeatureGroups.Select(feature =>
                feature.Id + "|" + feature.FailureLocationSummary + "|" +
                string.Join("|", feature.PrimarySourceGlobs)).ToArray());
        }

        [Test]
        public void CommittedCatalog_Maps3DUsabilityFollowupOwnershipAndBoundaries()
        {
            ProjectQualityCatalog catalog = ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup building = FindFeature(catalog, "building-construction-evacuation");
            ProjectFeatureGroup ui = FindFeature(catalog, "ui-input");

            CollectionAssert.IsSubsetOf(new[] { "IDEA-0008", "IDEA-0009", "IDEA-0010" },
                building.RequirementIds);
            CollectionAssert.Contains(building.SourceGlobs,
                "Assets/_Game/Scripts/Building/BuildingResourceNodeCompatibilityRules.cs");
            CollectionAssert.Contains(building.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/BuildingResourceNodeCompatibilityRulesTests.cs");
            CollectionAssert.Contains(building.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxProductionLifecycleTests.cs");
            CollectionAssert.Contains(building.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");

            CollectionAssert.IsSubsetOf(new[] { "IDEA-0007", "IDEA-0008", "IDEA-0010" },
                ui.RequirementIds);
            CollectionAssert.Contains(ui.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Usability/**");
            CollectionAssert.Contains(ui.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Usability/WasteCity.Graybox3D.Usability.asmdef");
            CollectionAssert.Contains(ui.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs");
            CollectionAssert.Contains(ui.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsView3D.cs");
            CollectionAssert.Contains(ui.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxUsabilityTests.cs");
            CollectionAssert.Contains(ui.TestFileGlobs,
                "Assets/_Game/Tests/PlayMode/GrayboxUsabilityRuntimeSceneTests.cs");
            CollectionAssert.Contains(ui.TestFileGlobs,
                "Assets/_Game/Tests/PlayMode/GrayboxProductionObservabilityRuntimeInputTests.cs");
            CollectionAssert.Contains(ui.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
            CollectionAssert.Contains(ui.RequirementIds, "IDEA-0011");

            ProjectReuseEntry compatibility = FindReuse(catalog,
                "building-resource-node-compatibility-rules");
            Assert.That(compatibility.FeatureGroupId,
                Is.EqualTo("building-construction-evacuation"));
            Assert.That(compatibility.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            CollectionAssert.Contains(compatibility.TypeNames,
                "BuildingResourceNodeCompatibilityRules");
            CollectionAssert.Contains(compatibility.RequirementIds,
                "IDEA-0010");

            ProjectReuseEntry nodeBinding = FindReuse(catalog,
                "resource-node-binding");
            Assert.That(nodeBinding.FeatureGroupId,
                Is.EqualTo("building-construction-evacuation"));
            Assert.That(nodeBinding.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            CollectionAssert.AreEqual(
                new[] { "ResourceNodeBinding" },
                nodeBinding.TypeNames);
            CollectionAssert.IsSubsetOf(
                new[] { "IDEA-0010", "IDEA-0011" },
                nodeBinding.RequirementIds);

            ProjectReuseEntry displaySettings = FindReuse(catalog,
                "graybox-display-settings");
            CollectionAssert.IsSubsetOf(new[]
            {
                "GrayboxDisplaySettingsModel3D",
                "PlayerPrefsGrayboxDisplaySettingsStore3D",
                "UnityGrayboxDisplaySettingsPlatform3D",
            }, displaySettings.TypeNames);
            StringAssert.Contains("IGrayboxDisplaySettingsStore",
                displaySettings.UseSummary);
            StringAssert.Contains("IGrayboxDisplaySettingsPlatform",
                displaySettings.UseSummary);
            StringAssert.Contains("PlayerPrefs", displaySettings.BoundarySummary);
            StringAssert.Contains("schema 30", displaySettings.BoundarySummary);

            Assert.That(FindReuse(catalog, "graybox-system-menu-controller").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            Assert.That(FindReuse(catalog, "graybox-usability-input-coordinator").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            Assert.That(FindReuse(catalog, "graybox-system-menu-view").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            ProjectReuseEntry operationsController = FindReuse(catalog,
                "graybox-operations-controller-3d");
            Assert.That(operationsController.FeatureGroupId,
                Is.EqualTo("ui-input"));
            Assert.That(operationsController.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            CollectionAssert.AreEqual(
                new[] { "GrayboxOperationsController3D" },
                operationsController.TypeNames);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsController3D.cs",
            }, operationsController.AssetPaths);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/ManualResourceAccessRulesTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxProductionObservabilityRuntimeInputTests.cs",
            }, operationsController.RequiredTestFiles);
            CollectionAssert.AreEqual(new[]
                { "IDEA-0011", "IDEA-0012", "IDEA-0016", "IDEA-0021" },
                operationsController.RequirementIds);

            ProjectReuseEntry operationsView = FindReuse(catalog,
                "graybox-operations-view-3d");
            Assert.That(operationsView.FeatureGroupId,
                Is.EqualTo("ui-input"));
            Assert.That(operationsView.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxOperationsView3D",
                "GrayboxResearchTreeView3D",
                "GrayboxResearchTreeViewportInput3D",
                "GrayboxResearchSearchFocus3D",
                "ResearchTreeConnectionGraphic3D",
            }, operationsView.TypeNames);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxOperationsView3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchTreeView3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchTreeViewportInput3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxResearchSearchFocus3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/ResearchTreeConnectionGraphic3D.cs",
            }, operationsView.AssetPaths);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs",
                "Assets/_Game/Tests/EditMode/ResearchTreeUiContractTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxProductionObservabilityRuntimeInputTests.cs",
            }, operationsView.RequiredTestFiles);
            CollectionAssert.AreEqual(new[]
            {
                "IDEA-0011",
                "IDEA-0012",
                "IDEA-0016",
                "IDEA-0018",
                "IDEA-0021",
            },
                operationsView.RequirementIds);

            ProjectUiEntry systemMenu = FindUi(catalog, "graybox-system-menu");
            Assert.That(systemMenu.OwnerTypeName,
                Is.EqualTo("GrayboxSystemMenuView3D"));
            Assert.That(systemMenu.SceneId, Is.EqualTo("graybox-prototype-3d"));
            ProjectUiEntry coordinator = FindUi(catalog,
                "graybox-usability-input-coordinator");
            Assert.That(coordinator.OwnerTypeName,
                Is.EqualTo("GrayboxUsabilityInputCoordinator3D"));
            Assert.That(coordinator.SceneId, Is.EqualTo("graybox-prototype-3d"));

            Assert.That(catalog.ReuseEntries.Where(entry =>
                    entry.TypeNames.Contains("GrayboxSystemMenuView3D"))
                .All(entry => entry.ReuseLevel == ProjectReuseLevel.SceneOnly),
                Is.True,
                "Scene-specific system menu View must not be promoted to general reuse.");
        }

        [Test]
        public void CommittedCatalog_MapsFormal3DSaveSchema31RuntimeAndEntryBoundaries()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());

            ProjectFeatureGroup persistence = FindFeature(
                catalog,
                "persistence-migration");
            CollectionAssert.IsSubsetOf(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxWorldCitySaveAdapter3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingStorageSaveAdapter3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveCoordinator3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalSaveRuntimeHost3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Usability/GrayboxFormalSaveEntryController3D.cs",
            }, persistence.SourceGlobs);
            CollectionAssert.IsSubsetOf(new[]
            {
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveCheckpointTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveCoordinatorTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveRuntimeHostTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveUiAndInputTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalPlayModeEntryFixture.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRoundTripTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRuntimeInputTests.cs",
            }, persistence.TestFileGlobs);
            CollectionAssert.AreEqual(
                new[] { "persistence-migration" },
                catalog.FeatureGroups.Where(feature =>
                    feature.TestFileGlobs.Contains(
                        "Assets/_Game/Tests/PlayMode/" +
                        "GrayboxFormalPlayModeEntryFixture.cs"))
                    .Select(feature => feature.Id));
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
            }, persistence.ScenePaths);

            ProjectReuseEntry envelope = FindReuse(
                catalog,
                "formal-save-envelope-schema-31");
            Assert.That(envelope.FeatureGroupId,
                Is.EqualTo("persistence-migration"));
            CollectionAssert.AreEqual(new[]
            {
                "FormalSaveCheckpointMetadata",
                "FormalSaveEnvelope",
                "FormalSaveDecodeResult",
                "FormalSaveCodec",
                "FormalSaveValidationResult",
                "FormalSaveValidator",
                "FormalThreeDProgressionSaveData",
                "FormalThreeDAttentionSaveData",
                "FormalThreeDFateSaveData",
                "FormalThreeDCivilizationSaveData",
                "FormalThreeDWorldSaveData",
                "FormalThreeDBuildingsSaveData",
                "FormalThreeDStorageSaveData",
                "FormalThreeDWarehouseSaveData",
                "FormalThreeDBackpackSaveData",
                "FormalThreeDCraftingSaveData",
                "FormalThreeDCraftingExecutionSaveData",
                "FormalThreeDResearchSaveData",
                "FormalThreeDProductionSaveData",
                "FormalThreeDProductionStateSaveData",
                "FormalThreeDDefenseSaveData",
                "FormalThreeDDefenseCampaignSaveData",
                "FormalThreeDDefenseCampaignStatisticsSaveData",
                "FormalThreeDEvacuationSaveData",
                "FormalThreeDEvacuationRuntimePayloadSaveData",
            }, envelope.TypeNames);
            string[] expectedFormalThreeDDtoTypes =
            {
                "FormalThreeDSaveData",
                "FormalThreeDProgressionSaveData",
                "FormalThreeDAttentionSaveData",
                "FormalThreeDAttentionHistorySaveData",
                "FormalThreeDAttentionPressureSaveData",
                "FormalThreeDAttentionPressureEntrySaveData",
                "FormalThreeDPressureCampaignSaveData",
                "FormalThreeDPressureInjectedReinforcementSaveData",
                "FormalThreeDFateSaveData",
                "FormalThreeDFateEffectsSaveData",
                "FormalThreeDPocketUniverseSaveData",
                "FormalThreeDPocketUniverseFlagshipSaveData",
                "FormalThreeDVoidDebtSaveData",
                "FormalThreeDVoidDebtEntrySaveData",
                "FormalThreeDRewindAnchorMetadataSaveData",
                "FormalThreeDRewindAnchorEntrySaveData",
                "FormalThreeDCivilizationSaveData",
                "FormalThreeDWorldSaveData",
                "FormalThreeDResourceNodeSaveData",
                "FormalThreeDResourceAmountSaveData",
                "FormalThreeDOrphanResourceSaveData",
                "FormalThreeDCitySaveData",
                "FormalThreeDBuildingsSaveData",
                "FormalThreeDBuildingInstanceSaveData",
                "FormalThreeDStorageSaveData",
                "FormalThreeDWarehouseSaveData",
                "FormalThreeDBackpackSaveData",
                "FormalThreeDBackpackSlotSaveData",
                "FormalThreeDCraftingSaveData",
                "FormalThreeDCraftingExecutionSaveData",
                "FormalThreeDResearchSaveData",
                "FormalThreeDProductionSaveData",
                "FormalThreeDProductionStateSaveData",
                "FormalThreeDDefenseSaveData",
                "FormalThreeDDefenseTowerSaveData",
                "FormalThreeDDefenseEnemySaveData",
                "FormalThreeDDefenseCampaignSaveData",
                "FormalThreeDDefenseCampaignStatisticsSaveData",
                "FormalThreeDDefenseCampaignEnemyCountSaveData",
                "FormalThreeDDefenseCampaignSpawnAnchorSaveData",
                "FormalThreeDDefenseCampaignMetricSaveData",
                "FormalThreeDDefenseCampaignTowerCombatStateSaveData",
                "FormalThreeDDefenseCampaignEnemyStateSaveData",
                "FormalThreeDDefenseCampaignBuildingHealthStateSaveData",
                "FormalThreeDEvacuationSaveData",
                "FormalThreeDEvacuationBatchContextSaveData",
                "FormalThreeDEvacuationWorkSaveData",
                "FormalThreeDEvacuationRuntimePayloadSaveData",
                "FormalThreeDPauseSaveData",
            };
            string formalThreeDDtoPath = Path.Combine(
                ProjectRoot(),
                "Assets/_Game/Scripts/Persistence/ThreeD/" +
                "FormalThreeDSaveData.cs");
            Assert.That(File.Exists(formalThreeDDtoPath), Is.True);
            string formalThreeDDtoSource = File.ReadAllText(
                formalThreeDDtoPath);
            string[] actualFormalThreeDDtoTypes = Regex.Matches(
                    formalThreeDDtoSource,
                    @"public\s+sealed\s+class\s+(FormalThreeD\w+)")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToArray();
            CollectionAssert.AreEqual(
                expectedFormalThreeDDtoTypes,
                actualFormalThreeDDtoTypes,
                "The source-level current formal 3D DTO inventory must remain " +
                "complete even when field-only DTOs are not emitted by " +
                "the project snapshot scanner.");
            CollectionAssert.DoesNotContain(
                envelope.TypeNames,
                "FormalSaveData",
                "Legacy schema 1-30 DTO must remain a separate reuse entry.");
            CollectionAssert.Contains(
                envelope.AssetPaths,
                "Assets/_Game/Scripts/Persistence/ThreeD/FormalThreeDSaveData.cs");
            var fixtureContracts = new Dictionary<string, string>
            {
                {
                    "Assets/_Game/Tests/Fixtures/Persistence/schema-01-legacy-2d.json",
                    "\"schema\": 1"
                },
                {
                    "Assets/_Game/Tests/Fixtures/Persistence/schema-30-legacy-2d.json",
                    "\"schema\": 30"
                },
                {
                    "Assets/_Game/Tests/Fixtures/Persistence/schema-31-formal-3d.json",
                    "\"saveSchemaVersion\": 31"
                },
                {
                    "Assets/_Game/Tests/Fixtures/Persistence/schema-31-invalid-cross-reference.json",
                    "\"saveSchemaVersion\": 31"
                },
                {
                    "Assets/_Game/Tests/Fixtures/Persistence/schema-32-future.json",
                    "\"saveSchemaVersion\": 32"
                },
            };
            foreach (KeyValuePair<string, string> fixture in
                     fixtureContracts)
            {
                string path = Path.Combine(ProjectRoot(), fixture.Key);
                Assert.That(File.Exists(path), Is.True, fixture.Key);
                StringAssert.Contains(
                    fixture.Value,
                    File.ReadAllText(path),
                    fixture.Key);
                CollectionAssert.DoesNotContain(
                    envelope.AssetPaths,
                    fixture.Key,
                    "Fixtures are test evidence, not scanner source assets.");
            }

            ProjectReuseEntry store = FindReuse(
                catalog,
                "formal-save-store-transaction");
            CollectionAssert.IsSubsetOf(new[]
            {
                "FormalSaveFileTransaction",
                "FormalSaveStore",
            }, store.TypeNames);
            StringAssert.Contains(".bak", store.UseSummary);
            StringAssert.Contains("原子", store.UseSummary);

            ProjectReuseEntry checkpoint = FindReuse(
                catalog,
                "formal-save-checkpoint-policy");
            CollectionAssert.Contains(
                checkpoint.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveCheckpointTests.cs");
            StringAssert.Contains("每帧", checkpoint.BoundarySummary);

            ProjectReuseEntry worldCity = FindReuse(
                catalog,
                "graybox-world-city-save-adapter-3d");
            CollectionAssert.AreEqual(
                new[] { "GrayboxWorldCitySaveAdapter3D" },
                worldCity.TypeNames);
            CollectionAssert.Contains(
                worldCity.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveWorldCityTests.cs");

            CollectionAssert.Contains(
                persistence.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalProgressionSaveAdapter3D.cs");
            CollectionAssert.Contains(
                persistence.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxFormalProgressionSaveAdapterTests.cs");
            CollectionAssert.Contains(
                persistence.RequirementIds,
                "IDEA-0020");
            ProjectReuseEntry progression = FindReuse(
                catalog,
                "graybox-progression-save-adapter-3d");
            Assert.That(
                progression.FeatureGroupId,
                Is.EqualTo("persistence-migration"));
            Assert.That(
                progression.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxFormalProgressionRestorePlan3D",
                "GrayboxFormalProgressionSaveAdapter3D",
            }, progression.TypeNames);
            CollectionAssert.Contains(
                progression.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxFormalProgressionSaveAdapterTests.cs");
            StringAssert.Contains(
                "零写入恢复计划",
                progression.BoundarySummary);
            StringAssert.Contains(
                "不读写文件",
                progression.BoundarySummary);

            ProjectReuseEntry coordinator = FindReuse(
                catalog,
                "graybox-formal-save-coordinator-3d");
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxFormalControllerRebuilder3D",
                "GrayboxFormalPauseSaveDomain3D",
                "GrayboxFormalSaveCoordinatorResult3D",
                "GrayboxFormalSaveCoordinator3D",
            }, coordinator.TypeNames);
            CollectionAssert.Contains(
                coordinator.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveCoordinatorTests.cs");
            CollectionAssert.Contains(
                coordinator.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxFormalProgressionSaveAdapterTests.cs");
            StringAssert.Contains("progression", coordinator.UseSummary);
            StringAssert.Contains("回滚", coordinator.BoundarySummary);

            ProjectReuseEntry runtimeHost = FindReuse(
                catalog,
                "graybox-formal-save-runtime-host-3d");
            Assert.That(runtimeHost.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            CollectionAssert.IsSubsetOf(new[]
            {
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveRuntimeHostTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxFormalProgressionSaveAdapterTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRuntimeInputTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRoundTripTests.cs",
            }, runtimeHost.RequiredTestFiles);
            StringAssert.Contains("八领域", runtimeHost.UseSummary);

            ProjectReuseEntry entry = FindReuse(
                catalog,
                "graybox-formal-save-entry-controller-3d");
            Assert.That(entry.FeatureGroupId, Is.EqualTo("ui-input"));
            Assert.That(entry.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            StringAssert.Contains("真实 UGUI 输入主循环",
                entry.BoundarySummary);

            ProjectUiEntry ui = FindUi(
                catalog,
                "graybox-formal-save-entry");
            Assert.That(ui.OwnerTypeName,
                Is.EqualTo("GrayboxFormalSaveEntryController3D"));
            Assert.That(ui.SceneId, Is.EqualTo("graybox-prototype-3d"));
            CollectionAssert.Contains(
                ui.RequiredTestFiles,
                "Assets/_Game/Tests/PlayMode/GrayboxFormalSaveRuntimeInputTests.cs");

            ProjectReuseEntry legacy = FindReuse(
                catalog,
                "formal-save-data");
            StringAssert.Contains("legacy 2D", legacy.UseSummary);
            StringAssert.Contains("schema 1–30", legacy.UseSummary);
            StringAssert.Contains("不承载 schema 31", legacy.BoundarySummary);
            Assert.That(
                catalog.ReuseEntries.Any(reuse =>
                    reuse.Id == "formal-prototype-frozen"),
                Is.False);
            Assert.That(
                catalog.ReuseEntries.Any(reuse =>
                    reuse.Id == "placeholder-building-controller-frozen"),
                Is.False);
        }

        [Test]
        public void CommittedCatalog_ExcludesRetired2DSceneAuthoringAndControllers()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            string[] cataloguedPaths = catalog.FeatureGroups
                .SelectMany(feature =>
                    feature.SourceGlobs
                        .Concat(feature.PrimarySourceGlobs)
                        .Concat(feature.ScenePaths))
                .Concat(catalog.ReuseEntries.SelectMany(reuse =>
                    reuse.AssetPaths))
                .Concat(catalog.Scenes.Select(scene => scene.Path))
                .ToArray();
            string[] retiredPaths =
            {
                "Assets/_Game/Scenes/FormalPrototype.unity",
                "Assets/_Game/Editor/FormalProjectSetup.cs",
                "Assets/_Game/Scripts/Building/PlaceholderBuildingController.cs",
                "Assets/_Game/Scripts/City/PlaceholderMobileCity.cs",
                "Assets/_Game/Scripts/Combat/FormalCombatController.cs",
                "Assets/_Game/Scripts/Combat/FormalFriendlyUnitController.cs",
                "Assets/_Game/Scripts/Combat/FormalTechnologyRouteController.cs",
                "Assets/_Game/Scripts/Combat/PlaceholderBehemoth.cs",
                "Assets/_Game/Scripts/Combat/PlaceholderBossEncounter.cs",
                "Assets/_Game/Scripts/Combat/PlaceholderEnemy.cs",
                "Assets/_Game/Scripts/Combat/PlaceholderPuppet.cs",
                "Assets/_Game/Scripts/Core/FormalGameClockController.cs",
                "Assets/_Game/Scripts/Core/FormalSessionController.cs",
                "Assets/_Game/Scripts/Core/FormalSessionStatisticsController.cs",
                "Assets/_Game/Scripts/Economy/FormalEconomyController.cs",
                "Assets/_Game/Scripts/Leader/FormalLeaderController.cs",
                "Assets/_Game/Scripts/Narrative/FormalGuidanceController.cs",
                "Assets/_Game/Scripts/Persistence/FormalSaveController.cs",
                "Assets/_Game/Scripts/Population/FormalPopulationController.cs",
                "Assets/_Game/Scripts/Progression/FormalAdvancementController.cs",
                "Assets/_Game/Scripts/Progression/FormalProgressionController.cs",
                "Assets/_Game/Scripts/UI/FormalPlaceholderHud.cs",
                "Assets/_Game/Scripts/UI/FormalTitleMenuController.cs",
                "Assets/_Game/Scripts/World/FormalCameraController.cs",
                "Assets/_Game/Scripts/World/FormalDroneController.cs",
                "Assets/_Game/Scripts/World/PlaceholderWorldView.cs",
            };

            foreach (string retiredPath in retiredPaths)
                CollectionAssert.DoesNotContain(
                    cataloguedPaths,
                    retiredPath,
                    retiredPath);
            Assert.That(
                catalog.Scenes.Any(scene =>
                    scene.Id == "formal-prototype"),
                Is.False);
        }

        [Test]
        public void CommittedCatalog_MapsProductionLogisticsFoundationOwnershipAndReuse()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup economy =
                FindFeature(catalog, "economy-production-logistics");

            CollectionAssert.IsSubsetOf(new[]
            {
                "Assets/_Game/Tests/EditMode/ResourceDefinitionCatalogTests.cs",
                "Assets/_Game/Tests/EditMode/PlayerBackpackModelTests.cs",
                "Assets/_Game/Tests/EditMode/ResourceInventoryChangeTests.cs",
                "Assets/_Game/Tests/EditMode/ResourceTransactionAndCapacityTests.cs",
                "Assets/_Game/Tests/EditMode/CraftingQueueModelTests.cs",
                "Assets/_Game/Tests/EditMode/ManualResourceAccessRulesTests.cs",
                "Assets/_Game/Tests/EditMode/FormalProductionSimulationTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveProductionTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxProductionRuntimeTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxProductionClockTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxProductionControllerTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxProductionObservabilityFacadeTests.cs",
            }, economy.TestFileGlobs);
            CollectionAssert.IsSubsetOf(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionEligibility3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionSaveAdapter3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionCommandFacade3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionClock3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionController3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/ProductionObservabilitySnapshot.cs",
            }, economy.SourceGlobs);
            CollectionAssert.Contains(economy.RequirementIds, "IDEA-0011");
            CollectionAssert.Contains(economy.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");

            AssertReuseContract(
                FindReuse(catalog, "resource-inventory"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/ResourceInventory.cs",
                },
                new[]
                {
                    "ResourceChangeAttribution",
                    "ResourceInventory",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/FoundationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveProductionTests.cs",
                    "Assets/_Game/Tests/EditMode/ResourceInventoryChangeTests.cs",
                },
                new[] { "DOC-0001", "IDEA-0011", "IDEA-0015" });
            AssertReuseContract(
                FindReuse(catalog, "resource-definition-catalog"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/ResourceDefinitionCatalog.cs",
                },
                new[]
                {
                    "ResourceDefinition",
                    "ResourceDefinitionCatalog",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/ResourceDefinitionCatalogTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0016" });
            AssertReuseContract(
                FindReuse(catalog, "player-backpack-model"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/PlayerBackpackModel.cs",
                },
                new[]
                {
                    "BackpackSlot",
                    "PlayerBackpackRestoreSlot",
                    "PlayerBackpackRestorePlan",
                    "PlayerBackpackModel",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/PlayerBackpackModelTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveEconomyTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0015" });
            AssertReuseContract(
                FindReuse(catalog, "resource-recipe-catalog"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/ResourceRecipeCatalog.cs",
                },
                new[]
                {
                    "ResourceRecipeDefinition",
                    "ResourceRecipeCatalog",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/CraftingQueueModelTests.cs",
                    "Assets/_Game/Tests/EditMode/ResourceRecipeCatalogIntegrityTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0016", "IDEA-0021" });
            AssertReuseContract(
                FindReuse(catalog, "crafting-queue-model"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/CraftingQueueModel.cs",
                },
                new[]
                {
                    "CraftingQueueRestoreEntry",
                    "CraftingQueueExecutionSnapshot",
                    "CraftingQueueRestorePlan",
                    "CraftingQueueModel",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/CraftingQueueModelTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveEconomyTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0015" });
            AssertReuseContract(
                FindReuse(catalog, "manual-resource-access-rules"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/ManualResourceAccessRules.cs",
                },
                new[]
                {
                    "ManualResourceAccessRules",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/ManualResourceAccessRulesTests.cs",
                });
            AssertReuseContract(
                FindReuse(catalog, "resource-capacity-policy"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/ResourceCapacityPolicy.cs",
                },
                new[]
                {
                    "ResourceCapacityPolicy",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/ResourceTransactionAndCapacityTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0012" });
            AssertReuseContract(
                FindReuse(catalog, "resource-transaction"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/ResourceTransaction.cs",
                },
                new[]
                {
                    "ResourceAmount",
                    "ResourceTransferResult",
                    "ResourceTransaction",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/ResourceTransactionAndCapacityTests.cs",
                });
            AssertReuseContract(
                FindReuse(catalog, "formal-production-definition-catalog"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/FormalProductionDefinitionCatalog.cs",
                },
                new[]
                {
                    "FormalProductionDefinition",
                    "FormalProductionDefinitionCatalog",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/FormalProductionSimulationTests.cs",
                    "Assets/_Game/Tests/EditMode/ResourceRecipeCatalogIntegrityTests.cs",
                });
            AssertReuseContract(
                FindReuse(catalog, "building-production-state"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/BuildingProductionState.cs",
                },
                new[]
                {
                    "BuildingProductionState",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/FormalProductionSimulationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveProductionTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0015" });
            AssertReuseContract(
                FindReuse(catalog, "formal-production-simulation"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/FormalProductionSimulation.cs",
                },
                new[]
                {
                    "FormalProductionSimulation",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/FormalProductionSimulationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0012" });
            AssertReuseContract(
                FindReuse(catalog, "graybox-production-eligibility-3d"),
                new[]
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionEligibility3D.cs",
                },
                new[]
                {
                    "GrayboxProductionEligibility3D",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxProductionLifecycleTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxProductionRuntimeTests.cs",
                });
            AssertReuseContract(
                FindReuse(catalog, "graybox-production-runtime-3d"),
                new[]
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionRuntime3D.cs",
                },
                new[]
                {
                    "GrayboxProductionEvacuationPayload3D",
                    "GrayboxProductionPersistenceState3D",
                    "GrayboxProductionRestorePlan3D",
                    "GrayboxProductionRuntime3D",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveProductionTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxProductionCombatLossTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxProductionRuntimeTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxProductionLifecycleTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                },
                new[]
                {
                    "IDEA-0011",
                    "IDEA-0012",
                    "IDEA-0014",
                    "IDEA-0015",
                    "IDEA-0017",
                });
            ProjectReuseEntry saveAdapter = FindReuse(
                catalog,
                "graybox-production-save-adapter-3d");
            Assert.That(saveAdapter.FeatureGroupId,
                Is.EqualTo("persistence-migration"));
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionSaveAdapter3D.cs",
            }, saveAdapter.AssetPaths);
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxProductionSaveAdapter3D",
            }, saveAdapter.TypeNames);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveProductionTests.cs",
            }, saveAdapter.RequiredTestFiles);
            CollectionAssert.AreEqual(new[] { "IDEA-0015" },
                saveAdapter.RequirementIds);
            AssertReuseContract(
                FindReuse(catalog, "graybox-production-clock-3d"),
                new[]
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionClock3D.cs",
                },
                new[]
                {
                    "ProductionStatisticsDelta",
                    "GrayboxProductionClock3D",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxProductionClockTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxProductionStatisticsDeltaTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0012", "IDEA-0017" });
            AssertReuseContract(
                FindReuse(catalog, "production-observability-boundary-3d"),
                new[]
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/ProductionObservabilitySnapshot.cs",
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionCommandFacade3D.cs",
                },
                new[]
                {
                    "ProductionResourceObservability",
                    "ProductionBuildingObservability",
                    "ProductionObservabilitySnapshot",
                    "GrayboxProductionCommandFacade3D",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxProductionObservabilityFacadeTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0012" });
            AssertReuseContract(
                FindReuse(catalog, "graybox-production-controller-3d"),
                new[]
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxProductionController3D.cs",
                },
                new[]
                {
                    "GrayboxProductionController3D",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxProductionControllerTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                },
                new[] { "IDEA-0011", "IDEA-0012" });
            Assert.That(FindReuse(catalog,
                    "graybox-production-eligibility-3d").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            Assert.That(FindReuse(catalog,
                    "graybox-production-runtime-3d").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            Assert.That(FindReuse(catalog,
                    "graybox-production-clock-3d").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            Assert.That(FindReuse(catalog,
                    "production-observability-boundary-3d").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            Assert.That(FindReuse(catalog,
                    "graybox-production-controller-3d").ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
        }

        [Test]
        public void CommittedCatalog_MapsBug0006AndIdea0012ProductionFilesAndBoundaries()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup building = FindFeature(
                catalog,
                "building-construction-evacuation");
            ProjectFeatureGroup ui = FindFeature(catalog, "ui-input");
            ProjectFeatureGroup economy = FindFeature(
                catalog,
                "economy-production-logistics");
            ProjectFeatureGroup presentation = FindFeature(
                catalog,
                "presentation-art-integration");

            CollectionAssert.IsSubsetOf(
                new[] { "BUG-0006", "IDEA-0012" },
                building.RequirementIds);
            CollectionAssert.IsSubsetOf(
                new[] { "BUG-0006", "IDEA-0012" },
                ui.RequirementIds);
            CollectionAssert.Contains(economy.RequirementIds, "IDEA-0012");
            CollectionAssert.Contains(
                presentation.RequirementIds,
                "IDEA-0012");
            CollectionAssert.IsSubsetOf(new[]
            {
                "Assets/_Game/Tests/EditMode/CityResourceStorageModelTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                "Assets/_Game/Tests/EditMode/ResourceShortfallRulesTests.cs",
            }, economy.TestFileGlobs);
            CollectionAssert.IsSubsetOf(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/ResourceIconCatalog3D.cs",
                "Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeIdentity3D.cs",
                "Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeMarker3D.cs",
                "Assets/_Game/Rendering/Graybox3D/ResourceIconCatalog3D.asset",
            }, presentation.SourceGlobs);

            AssertReuseContract(
                FindReuse(catalog, "city-resource-storage-model"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/CityResourceStorageModel.cs",
                },
                new[]
                {
                    "CityResourceStorageRestorePlan",
                    "CityResourceEvacuationPlan",
                    "CityResourceChangeAttributionScope",
                    "CityResourceStorageModel",
                    "CityResourceStorageSnapshot",
                    "CityStorageOrphanResource",
                    "CityWarehouseRestoreEntry",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/CityResourceStorageModelTests.cs",
                    "Assets/_Game/Tests/EditMode/CityResourceStorageCombatLossTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveBuildingStorageTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                },
                new[]
                {
                    "IDEA-0012",
                    "IDEA-0014",
                    "IDEA-0015",
                    "IDEA-0017",
                });
            AssertReuseContract(
                FindReuse(catalog, "warehouse-storage-state"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/WarehouseStorageState.cs",
                },
                new[]
                {
                    "WarehouseStorageState",
                    "WarehouseStorageSnapshot",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/CityResourceStorageModelTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveBuildingStorageTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                },
                new[] { "IDEA-0012", "IDEA-0015" });
            AssertReuseContract(
                FindReuse(catalog, "resource-shortfall-rules"),
                new[]
                {
                    "Assets/_Game/Scripts/Economy/ResourceShortfallRules.cs",
                },
                new[]
                {
                    "ResourceShortfall",
                    "ResourceShortfallRules",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/ResourceShortfallRulesTests.cs",
                },
                new[] { "IDEA-0012" });

            ProjectReuseEntry iconCatalog = FindReuse(
                catalog,
                "resource-icon-catalog-3d");
            Assert.That(iconCatalog.FeatureGroupId,
                Is.EqualTo("presentation-art-integration"));
            Assert.That(iconCatalog.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/ResourceIconCatalog3D.cs",
            }, iconCatalog.AssetPaths);
            CollectionAssert.AreEqual(
                new[] { "ResourceIconCatalog3D" },
                iconCatalog.TypeNames);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxSceneContractTests.cs",
                "Assets/_Game/Tests/EditMode/Production2DItemIconPipelineTests.cs",
            }, iconCatalog.RequiredTestFiles);
            CollectionAssert.AreEqual(
                new[] { "IDEA-0012", "IDEA-0016" },
                iconCatalog.RequirementIds);

            ProjectReuseEntry nodeMarkers = FindReuse(
                catalog,
                "graybox-resource-node-markers-3d");
            Assert.That(nodeMarkers.FeatureGroupId,
                Is.EqualTo("presentation-art-integration"));
            Assert.That(nodeMarkers.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.SceneOnly));
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeIdentity3D.cs",
                "Assets/_Game/Scripts/Graybox3D/GrayboxResourceNodeMarker3D.cs",
            }, nodeMarkers.AssetPaths);
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxResourceNodeIdentity3D",
                "GrayboxResourceNodeMarker3D",
            }, nodeMarkers.TypeNames);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs",
            }, nodeMarkers.RequiredTestFiles);
            CollectionAssert.AreEqual(
                new[] { "IDEA-0012", "IDEA-0018", "IDEA-0019" },
                nodeMarkers.RequirementIds);

            ProjectReuseEntry legacyCapacity = FindReuse(
                catalog,
                "resource-capacity-policy");
            Assert.That(
                legacyCapacity.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            StringAssert.Contains("IDEA-0012", legacyCapacity.UseSummary);
            StringAssert.Contains(
                "CityResourceStorageModel",
                legacyCapacity.BoundarySummary);

            ProjectReuseEntry operationsController = FindReuse(
                catalog,
                "graybox-operations-controller-3d");
            CollectionAssert.AreEqual(
                new[]
                    { "IDEA-0011", "IDEA-0012", "IDEA-0016", "IDEA-0021" },
                operationsController.RequirementIds);
            CollectionAssert.Contains(
                operationsController.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs");
            ProjectReuseEntry operationsView = FindReuse(
                catalog,
                "graybox-operations-view-3d");
            CollectionAssert.AreEqual(
                new[]
                {
                    "IDEA-0011",
                    "IDEA-0012",
                    "IDEA-0016",
                    "IDEA-0018",
                    "IDEA-0021",
                },
                operationsView.RequirementIds);
            CollectionAssert.Contains(
                operationsView.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs");
        }

        [Test]
        public void CommittedCatalog_MapsDemoResearchOwnershipAndReuse()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup research =
                FindFeature(catalog, "research-population");

            CollectionAssert.Contains(research.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/DemoResearchRuntimeTests.cs");
            CollectionAssert.Contains(research.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
            CollectionAssert.Contains(research.RequirementIds, "IDEA-0011");

            ProjectReuseEntry demoCatalog = FindReuse(catalog,
                "demo-research-catalog");
            Assert.That(demoCatalog.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(demoCatalog.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            CollectionAssert.AreEqual(new[]
            {
                "DemoResearchCatalog",
            }, demoCatalog.TypeNames);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scripts/Research/DemoResearchCatalog.cs",
            }, demoCatalog.AssetPaths);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/DemoResearchRuntimeTests.cs",
            }, demoCatalog.RequiredTestFiles);
            CollectionAssert.AreEqual(new[] { "IDEA-0011" },
                demoCatalog.RequirementIds);

            ProjectReuseEntry demoRuntime = FindReuse(catalog,
                "demo-research-runtime");
            Assert.That(demoRuntime.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(demoRuntime.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            CollectionAssert.AreEqual(new[] { "DemoResearchRuntime" },
                demoRuntime.TypeNames);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Scripts/Research/DemoResearchRuntime.cs",
            }, demoRuntime.AssetPaths);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/DemoResearchRuntimeTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxFormalSaveEconomyTests.cs",
            }, demoRuntime.RequiredTestFiles);
            CollectionAssert.AreEqual(new[] { "IDEA-0011", "IDEA-0015" },
                demoRuntime.RequirementIds);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0020AttentionOwnershipAndReuse()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup progression =
                FindFeature(catalog, "research-population");

            Assert.That(
                progression.ChineseName,
                Is.EqualTo("研究、人口与文明进程"));
            CollectionAssert.Contains(
                progression.PrimarySourceGlobs,
                "Assets/_Game/Scripts/Progression/**");
            CollectionAssert.Contains(
                progression.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FormalAttentionCatalogTests.cs");
            CollectionAssert.Contains(
                progression.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FormalAttentionRuntimeTests.cs");
            CollectionAssert.Contains(
                progression.RequirementIds,
                "IDEA-0020");

            ProjectReuseEntry sourceCatalog =
                FindReuse(catalog, "formal-attention-catalog");
            Assert.That(
                sourceCatalog.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(
                sourceCatalog.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.AreEqual(
                new[]
                {
                    "FormalAttentionReasonDefinition",
                    "FormalAttentionCatalog",
                },
                sourceCatalog.TypeNames);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Assets/_Game/Tests/EditMode/FormalAttentionCatalogTests.cs",
                },
                sourceCatalog.RequiredTestFiles);

            ProjectReuseEntry runtime =
                FindReuse(catalog, "formal-attention-runtime");
            Assert.That(
                runtime.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(
                runtime.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            CollectionAssert.AreEqual(
                new[]
                {
                    "FormalAttentionHistoryEntry",
                    "FormalAttentionSnapshot",
                    "FormalAttentionRuntime",
                },
                runtime.TypeNames);
            CollectionAssert.Contains(
                runtime.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/FormalProgressionTests.cs");
            CollectionAssert.AreEqual(
                new[] { "IDEA-0020" },
                runtime.RequirementIds);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0020PressureAndBroodmotherRules()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup progression =
                FindFeature(catalog, "research-population");
            ProjectFeatureGroup combat = FindFeature(catalog, "combat-routes");

            foreach (string test in new[]
            {
                "Assets/_Game/Tests/EditMode/AttentionPressureCatalogTests.cs",
                "Assets/_Game/Tests/EditMode/AttentionPressureRuntimeTests.cs",
            })
            {
                CollectionAssert.Contains(progression.TestFileGlobs, test);
                CollectionAssert.Contains(combat.TestFileGlobs, test);
            }
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FormalAttentionRuntimeTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/CrystalBroodmotherCatalogTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/CrystalBroodmotherEncounterTests.cs");
            CollectionAssert.Contains(combat.RequirementIds, "IDEA-0020");

            ProjectReuseEntry pressure = FindReuse(
                catalog,
                "attention-pressure-runtime");
            Assert.That(pressure.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(pressure.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.AreEqual(new[]
            {
                "AttentionPressureDefinition",
                "AttentionPressureCatalog",
                "AttentionPressureCommand",
                "AttentionPressureEntrySnapshot",
                "AttentionPressureSnapshot",
                "AttentionPressureRuntime",
                "GrayboxAttentionPressureDefenseController3D",
                "GrayboxAttentionPressureRuntimeController3D",
                "GrayboxAttentionPressureRestorePlan3D",
                "GrayboxAttentionPressureSaveAdapter3D",
                "GrayboxAttentionPressurePresentationController3D",
            }, pressure.TypeNames);
            string pressureGuidance = pressure.UseSummary + "\n" +
                pressure.BoundarySummary;
            StringAssert.Contains("控制器", pressureGuidance);
            StringAssert.Contains("Defense", pressureGuidance);
            StringAssert.Contains("schema 33", pressureGuidance);
            StringAssert.Contains("HUD", pressureGuidance);

            ProjectReuseEntry broodmother = FindReuse(
                catalog,
                "crystal-broodmother-encounter");
            Assert.That(broodmother.FeatureGroupId,
                Is.EqualTo("combat-routes"));
            Assert.That(broodmother.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.Contains(broodmother.TypeNames,
                "CrystalBroodmotherCatalog");
            CollectionAssert.Contains(broodmother.TypeNames,
                "CrystalBroodmotherEncounter");
            CollectionAssert.Contains(broodmother.TypeNames,
                "AttentionPressureCampaignCatalog");
            StringAssert.Contains("正式 Defense",
                broodmother.BoundarySummary);
            CollectionAssert.AreEqual(new[] { "IDEA-0020" },
                broodmother.RequirementIds);

            ProjectReuseEntry research = FindReuse(
                catalog,
                "formal-research-runtime");
            CollectionAssert.Contains(research.TypeNames, "ResearchCatalog");
            CollectionAssert.Contains(research.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/FormalResearchCatalogTests.cs");
            CollectionAssert.Contains(research.RequirementIds, "IDEA-0020");
            StringAssert.Contains("44", research.UseSummary);
            StringAssert.Contains("legacy-analysis", research.UseSummary);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0020FateOwnershipWithoutLegacyCode()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup progression =
                FindFeature(catalog, "research-population");
            CollectionAssert.Contains(
                progression.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FormalFateCatalogTests.cs");
            CollectionAssert.Contains(
                progression.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FormalFateRuntimeTests.cs");

            ProjectReuseEntry fateCatalog =
                FindReuse(catalog, "formal-fate-catalog");
            Assert.That(
                fateCatalog.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(
                fateCatalog.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.AreEqual(
                new[] { "FormalFateDefinition", "FormalFateCatalog" },
                fateCatalog.TypeNames);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Assets/_Game/Tests/EditMode/FormalFateCatalogTests.cs",
                },
                fateCatalog.RequiredTestFiles);

            ProjectReuseEntry fateRuntime =
                FindReuse(catalog, "formal-fate-runtime");
            Assert.That(
                fateRuntime.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(
                fateRuntime.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.Recommended));
            CollectionAssert.AreEqual(
                new[] { "FormalFateSnapshot", "FormalFateRuntime" },
                fateRuntime.TypeNames);
            CollectionAssert.AreEqual(
                new[] { "IDEA-0020" },
                fateRuntime.RequirementIds);

            foreach (string relativePath in new[]
            {
                "Assets/_Game/Scripts/Progression/FormalFateCatalog.cs",
                "Assets/_Game/Scripts/Progression/FormalFateRuntime.cs",
            })
            {
                string source = File.ReadAllText(Path.Combine(
                    ProjectRoot(),
                    relativePath));
                StringAssert.DoesNotContain("WasteCity.Legacy", source);
                StringAssert.DoesNotContain("LegacyPathCatalog", source);
                StringAssert.DoesNotContain("LegacySelectionModel", source);
            }
        }

        [Test]
        public void CommittedCatalog_MapsIdea0020ProgressionEventsWithoutPolling()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            const string integrationTest =
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxProgressionEventIntegrationTests.cs";

            ProjectFeatureGroup progression =
                FindFeature(catalog, "research-population");
            CollectionAssert.Contains(
                progression.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxProgressionEventRouter3D.cs");
            CollectionAssert.Contains(
                progression.PrimarySourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxProgressionEventRouter3D.cs");
            CollectionAssert.Contains(
                progression.TestFileGlobs,
                integrationTest);

            ProjectReuseEntry router = FindReuse(
                catalog,
                "graybox-progression-event-router-3d");
            Assert.That(router.FeatureGroupId,
                Is.EqualTo("research-population"));
            Assert.That(router.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.AreEqual(
                new[] { "GrayboxProgressionEventRouter3D" },
                router.TypeNames);
            CollectionAssert.AreEqual(
                new[] { integrationTest },
                router.RequiredTestFiles);
            StringAssert.Contains("不扫描建筑或科技", router.BoundarySummary);
            StringAssert.Contains("不轮询 revision", router.BoundarySummary);
            StringAssert.Contains("不从存档恢复状态追补历史",
                router.BoundarySummary);
            StringAssert.Contains("保持未接线", router.BoundarySummary);

            ProjectFeatureGroup building = FindFeature(
                catalog,
                "building-construction-evacuation");
            CollectionAssert.Contains(building.TestFileGlobs,
                integrationTest);
            CollectionAssert.Contains(building.RequirementIds, "IDEA-0020");
            CollectionAssert.Contains(
                building.HumanDocumentPaths,
                "Docs/superpowers/specs/" +
                "2026-08-26-idea-0020-progression-attention-fate-" +
                "ascension-design.md");

            ProjectReuseEntry session = FindReuse(
                catalog,
                "building-session-3d");
            CollectionAssert.Contains(session.RequiredTestFiles,
                integrationTest);
            CollectionAssert.Contains(session.RequirementIds, "IDEA-0020");
            StringAssert.Contains("BuildingCompleted",
                session.UseSummary + "\n" + session.BoundarySummary);
            StringAssert.Contains("Configure", session.BoundarySummary);
            StringAssert.Contains("存档恢复", session.BoundarySummary);
            StringAssert.Contains("订阅者异常逐个隔离",
                session.BoundarySummary);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0020ProgressionHudReadOnlyBoundary()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup ui = FindFeature(catalog, "ui-input");
            const string hudGlob =
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxProgressionHud*.cs";
            const string editTest =
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxProgressionPresentationTests.cs";
            const string playTest =
                "Assets/_Game/Tests/PlayMode/" +
                "GrayboxProgressionRuntimeInputTests.cs";
            const string spec =
                "Docs/superpowers/specs/" +
                "2026-08-26-idea-0020-progression-attention-fate-" +
                "ascension-design.md";

            CollectionAssert.Contains(ui.SourceGlobs, hudGlob);
            CollectionAssert.Contains(ui.PrimarySourceGlobs, hudGlob);
            CollectionAssert.Contains(ui.TestFileGlobs, editTest);
            CollectionAssert.Contains(ui.TestFileGlobs, playTest);
            CollectionAssert.Contains(ui.RequirementIds, "IDEA-0020");
            CollectionAssert.Contains(ui.HumanDocumentPaths, spec);

            ProjectReuseEntry hud = FindReuse(
                catalog,
                "graybox-progression-hud-3d");
            Assert.That(hud.FeatureGroupId, Is.EqualTo("ui-input"));
            Assert.That(hud.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxProgressionHudView3D",
                "GrayboxProgressionHudController3D",
            }, hud.TypeNames);
            CollectionAssert.AreEqual(new[] { editTest, playTest },
                hud.RequiredTestFiles);
            CollectionAssert.AreEqual(new[] { "IDEA-0020" },
                hud.RequirementIds);
            StringAssert.Contains("不可变快照", hud.UseSummary);
            StringAssert.Contains("真实 UGUI", hud.BoundarySummary);
            StringAssert.Contains("不写 Attention/Fate runtime",
                hud.BoundarySummary);
            StringAssert.Contains("不进入 schema", hud.BoundarySummary);
            StringAssert.Contains("EffectsReady=false", hud.BoundarySummary);
            StringAssert.Contains("零重复刷新", hud.BoundarySummary);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0020FateDomainControlBoundaries()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            const string spec =
                "Docs/superpowers/specs/" +
                "2026-08-26-idea-0020-progression-attention-fate-" +
                "ascension-design.md";

            ProjectFeatureGroup progression =
                FindFeature(catalog, "research-population");
            foreach (string test in new[]
            {
                "Assets/_Game/Tests/EditMode/PocketUniverseFateEffectTests.cs",
                "Assets/_Game/Tests/EditMode/" +
                    "PocketUniverseProductionIntegrationTests.cs",
                "Assets/_Game/Tests/EditMode/FormalVoidDebtRuntimeTests.cs",
                "Assets/_Game/Tests/EditMode/" +
                    "GrayboxPocketUniverseFateControllerTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxVoidDebtIntegrationTests.cs",
                "Assets/_Game/Tests/EditMode/GrayboxRewindAnchorServiceTests.cs",
                "Assets/_Game/Tests/EditMode/" +
                    "GrayboxPocketUniverseCollapseResolverTests.cs",
                "Assets/_Game/Tests/EditMode/" +
                    "GrayboxVoidDebtAttentionControllerTests.cs",
                "Assets/_Game/Tests/EditMode/" +
                    "FormalRewindAnchorMetadataRuntimeTests.cs",
            })
            {
                CollectionAssert.Contains(progression.TestFileGlobs, test);
            }

            ProjectFeatureGroup economy = FindFeature(
                catalog,
                "economy-production-logistics");
            CollectionAssert.Contains(
                economy.SourceGlobs,
                "Assets/_Game/Scripts/Progression/FormalVoidDebtRuntime.cs");
            CollectionAssert.Contains(
                economy.PrimarySourceGlobs,
                "Assets/_Game/Scripts/Progression/FormalVoidDebtRuntime.cs");
            CollectionAssert.Contains(economy.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/" +
                "PocketUniverseProductionIntegrationTests.cs");
            CollectionAssert.Contains(economy.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FormalVoidDebtRuntimeTests.cs");
            CollectionAssert.Contains(economy.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxPocketUniverseFateControllerTests.cs");
            CollectionAssert.Contains(economy.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxVoidDebtIntegrationTests.cs");
            CollectionAssert.Contains(economy.RequirementIds, "IDEA-0020");
            CollectionAssert.Contains(economy.HumanDocumentPaths, spec);

            ProjectFeatureGroup persistence = FindFeature(
                catalog,
                "persistence-migration");
            CollectionAssert.Contains(persistence.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FormalRewindAnchorStoreTests.cs");
            CollectionAssert.Contains(persistence.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxRewindAnchorServiceTests.cs");

            ProjectReuseEntry pocket = FindReuse(
                catalog,
                "pocket-universe-fate-effect");
            Assert.That(pocket.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.Contains(pocket.TypeNames,
                "PocketUniverseFateEffect");
            CollectionAssert.Contains(pocket.TypeNames,
                "GrayboxPocketUniverseFateController3D");
            CollectionAssert.Contains(pocket.TypeNames,
                "GrayboxPocketUniverseCollapseResolver3D");
            CollectionAssert.Contains(pocket.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxPocketUniverseFateControllerTests.cs");
            StringAssert.Contains("Lv.1/Lv.2 已由 Host",
                pocket.BoundarySummary);
            StringAssert.Contains("不修改输入、周期或容量",
                pocket.BoundarySummary);
            StringAssert.Contains("Lv.2", pocket.BoundarySummary);

            ProjectReuseEntry debt = FindReuse(
                catalog,
                "formal-void-debt-runtime");
            Assert.That(debt.FeatureGroupId,
                Is.EqualTo("research-population"));
            CollectionAssert.Contains(debt.TypeNames,
                "GrayboxVoidDebtController3D");
            CollectionAssert.Contains(debt.TypeNames,
                "GrayboxVoidDebtAttentionController3D");
            CollectionAssert.Contains(debt.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/GrayboxVoidDebtIntegrationTests.cs");
            StringAssert.Contains("Lv.1/Lv.2 已由 Host",
                debt.BoundarySummary);
            StringAssert.Contains("普通消费仍不得透支",
                debt.BoundarySummary);
            StringAssert.Contains("命轨专属详情", debt.BoundarySummary);

            ProjectReuseEntry rewind = FindReuse(
                catalog,
                "formal-rewind-anchor-store");
            Assert.That(rewind.FeatureGroupId,
                Is.EqualTo("persistence-migration"));
            CollectionAssert.AreEqual(new[]
            {
                "FormalRewindAnchorStoreResult",
                "FormalRewindAnchorStore",
                "GrayboxRewindAnchorServiceResult3D",
                "GrayboxRewindAnchorService3D",
                "FormalRewindAnchorMetadata",
                "FormalRewindAnchorMetadataSnapshot",
                "FormalRewindAnchorMetadataUpsertPlan",
                "FormalRewindAnchorMetadataClearPlan",
                "FormalRewindAnchorMetadataRuntime",
            }, rewind.TypeNames);
            CollectionAssert.Contains(rewind.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/GrayboxRewindAnchorServiceTests.cs");
            StringAssert.Contains("不是第二个玩家存档槽",
                rewind.BoundarySummary);
            StringAssert.Contains("Host、schema 33", rewind.BoundarySummary);
            StringAssert.Contains("指定槽 Create/Read/Clear 已接",
                rewind.BoundarySummary);
            StringAssert.Contains("跨等级读取",
                rewind.BoundarySummary);

            ProjectReuseEntry adapter = FindReuse(
                catalog,
                "graybox-progression-save-adapter-3d");
            CollectionAssert.Contains(adapter.RequiredTestFiles,
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxFormalFateEffectsSaveAdapterTests.cs");
            StringAssert.Contains("绑定 owner 身份",
                adapter.BoundarySummary);

            ProjectReuseEntry fateUi = FindReuse(
                catalog,
                "graybox-fate-selection-ui-3d");
            Assert.That(fateUi.FeatureGroupId, Is.EqualTo("ui-input"));
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxFateSelectionCard3D",
                "GrayboxFateSelectionView3D",
                "GrayboxFateSelectionController3D",
            }, fateUi.TypeNames);
            StringAssert.Contains("真实输入已接", fateUi.BoundarySummary);
            StringAssert.Contains("回溯锚点创建/读取按钮",
                fateUi.BoundarySummary);
            foreach (ProjectReuseEntry entry in new[] { pocket, debt, rewind })
                CollectionAssert.AreEqual(new[] { "IDEA-0020" },
                    entry.RequirementIds);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0020FateOperationsReadOnlyCommands()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup ui = FindFeature(catalog, "ui-input");
            const string glob =
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFateOperations*.cs";
            const string edit =
                "Assets/_Game/Tests/EditMode/" +
                "GrayboxFateOperationsPresentationTests.cs";
            const string play =
                "Assets/_Game/Tests/PlayMode/" +
                "GrayboxFateOperationsRuntimeInputTests.cs";
            CollectionAssert.Contains(ui.SourceGlobs, glob);
            CollectionAssert.Contains(ui.PrimarySourceGlobs, glob);
            CollectionAssert.Contains(ui.TestFileGlobs, edit);
            CollectionAssert.Contains(ui.TestFileGlobs, play);

            ProjectReuseEntry operations = FindReuse(
                catalog,
                "graybox-fate-operations-ui-3d");
            Assert.That(operations.FeatureGroupId, Is.EqualTo("ui-input"));
            Assert.That(operations.ReuseLevel,
                Is.EqualTo(ProjectReuseLevel.ReviewBeforeReuse));
            CollectionAssert.AreEqual(new[]
            {
                "GrayboxFateOperationsView3D",
                "GrayboxFateOperationsController3D",
            }, operations.TypeNames);
            CollectionAssert.AreEqual(new[] { edit, play },
                operations.RequiredTestFiles);
            StringAssert.Contains("四份不可变快照", operations.UseSummary);
            StringAssert.Contains("不直接写 runtime、schema 或文件",
                operations.BoundarySummary);
            StringAssert.Contains("Host 绑定到唯一 Rewind Service",
                operations.BoundarySummary);
            StringAssert.Contains("读取必须二次确认",
                operations.BoundarySummary);

            ProjectReuseEntry rewind = FindReuse(
                catalog,
                "formal-rewind-anchor-store");
            CollectionAssert.Contains(rewind.RequiredTestFiles, edit);
            CollectionAssert.Contains(rewind.RequiredTestFiles, play);
            StringAssert.Contains("指定槽 Create/Read/Clear 已接",
                rewind.BoundarySummary);
            StringAssert.Contains("按钮只能调用 Host Service",
                rewind.BoundarySummary);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0013DefenseOwnershipReuseAndHud()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());
            ProjectFeatureGroup combat = FindFeature(catalog, "combat-routes");

            CollectionAssert.Contains(combat.SourceGlobs,
                "Assets/_Game/Scripts/Defense/**");
            CollectionAssert.Contains(combat.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefense*.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FirstDefenseLoopTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/FirstDefenseWaveRuntimeTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxFirstDefenseRuntimeTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxDefenseControllerTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxDefenseObservabilityTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxDefensePresentationTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxDefenseSnapshotStabilityTests.cs");
            CollectionAssert.Contains(combat.TestFileGlobs,
                "Assets/_Game/Tests/PlayMode/GrayboxDefenseRuntimeInputTests.cs");
            CollectionAssert.Contains(combat.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
            CollectionAssert.Contains(combat.RequirementIds, "IDEA-0013");
            Assert.That(combat.MinimumVerification,
                Is.EqualTo(ProjectVerificationLevel.FocusedPlayMode));

            foreach (string featureId in new[]
            {
                "building-construction-evacuation",
                "ui-input",
                "economy-production-logistics",
                "research-population",
                "scene-editor-build-performance",
            })
                CollectionAssert.Contains(
                    FindFeature(catalog, featureId).RequirementIds,
                    "IDEA-0013",
                    featureId);

            AssertDefenseReuse(
                FindReuse(catalog, "first-defense-combat-models"),
                "combat-routes",
                ProjectReuseLevel.Recommended,
                new[] { "MachineGunTurretPersistenceState", "MachineGunTurretCombatModel", "DefenseEnemyPersistenceState", "DefenseEnemyCombatModel", "CityCoreCombatModel" },
                new[] { "Assets/_Game/Scripts/Defense/FirstDefenseCombatModels.cs" },
                new[] { "Assets/_Game/Tests/EditMode/FirstDefenseLoopTests.cs", "Assets/_Game/Tests/EditMode/GrayboxFormalSaveDefenseTests.cs" },
                new[] { "IDEA-0013", "IDEA-0015" });
            AssertDefenseReuse(
                FindReuse(catalog, "tutorial-defense-runtime"),
                "combat-routes",
                ProjectReuseLevel.Recommended,
                new[] { "DefenseEnemyRuntimeSnapshot", "DefenseRuntimeSnapshot", "TutorialDefensePersistenceState", "TutorialDefenseRuntimeModel" },
                new[] { "Assets/_Game/Scripts/Defense/FirstDefenseWaveRuntime.cs" },
                new[] { "Assets/_Game/Tests/EditMode/FirstDefenseWaveRuntimeTests.cs", "Assets/_Game/Tests/EditMode/GrayboxFormalSaveDefenseTests.cs" },
                new[] { "IDEA-0013", "IDEA-0015" });
            AssertDefenseReuse(
                FindReuse(catalog, "graybox-building-operational-access-3d"),
                "building-construction-evacuation",
                ProjectReuseLevel.Recommended,
                new[] { "GrayboxBuildingOperationalAccess3D" },
                new[] { "Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingOperationalAccess3D.cs" },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxProductionRuntimeTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFirstDefenseRuntimeTests.cs",
                });
            AssertDefenseReuse(
                FindReuse(catalog, "graybox-defense-runtime-3d"),
                "combat-routes",
                ProjectReuseLevel.ReviewBeforeReuse,
                new[]
                {
                    "GrayboxDefenseEvacuationPayload3D",
                    "GrayboxDefenseTowerRuntimeState3D",
                    "GrayboxDefenseTowerSnapshot3D",
                    "GrayboxDefenseEnemySnapshot3D",
                    "GrayboxDefenseSettledAttackEvent3D",
                    "GrayboxDefenseRuntimeSnapshot3D",
                    "GrayboxDefensePersistenceState3D",
                    "GrayboxDefenseRestorePlan3D",
                    "GrayboxDefenseRuntime3D",
                },
                new[] { "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs" },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFirstDefenseRuntimeTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxDefenseSnapshotStabilityTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveDefenseTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalDefenseCampaignRuntimeIntegrationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxDefenseSettledAttackPresentationTests.cs",
                },
                new[]
                {
                    "IDEA-0013",
                    "IDEA-0014",
                    "IDEA-0015",
                    "IDEA-0017",
                });
            AssertDefenseReuse(
                FindReuse(catalog, "graybox-defense-save-adapter-3d"),
                "persistence-migration",
                ProjectReuseLevel.ReviewBeforeReuse,
                new[] { "GrayboxDefenseSaveAdapter3D" },
                new[] { "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSaveAdapter3D.cs" },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxFormalSaveDefenseTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxFormalDefenseCampaignSaveAdapterTests.cs",
                    "Assets/_Game/Tests/EditMode/FormalSaveSchema32ContractTests.cs",
                    "Assets/_Game/Tests/EditMode/SessionStatisticsTests.cs",
                },
                new[] { "IDEA-0013", "IDEA-0015", "IDEA-0017" });
            AssertDefenseReuse(
                FindReuse(catalog, "graybox-defense-scene-presentation-3d"),
                "ui-input",
                ProjectReuseLevel.SceneOnly,
                new[]
                {
                    "GrayboxDefenseController3D",
                    "GrayboxDefenseHud3D",
                    "GrayboxDefenseHudView3D",
                    "GrayboxDefenseWorldView3D",
                },
                new[]
                {
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseController3D.cs",
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHud3D.cs",
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs",
                    "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs",
                },
                new[]
                {
                    "Assets/_Game/Tests/EditMode/GrayboxDefenseControllerTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxDefenseObservabilityTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxDefensePresentationTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxDefenseSelectionProjectionTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxDefenseSettledAttackPresentationTests.cs",
                    "Assets/_Game/Tests/PlayMode/GrayboxDefenseRuntimeInputTests.cs",
                    "Assets/_Game/Tests/EditMode/GrayboxDefenseSettlementRuntimeIntegrationTests.cs",
                },
                new[] { "IDEA-0013", "IDEA-0017" });

            ProjectUiEntry hud = FindUi(catalog, "graybox-defense-hud");
            Assert.That(hud.OwnerTypeName, Is.EqualTo("GrayboxDefenseHudView3D"));
            Assert.That(hud.SceneId, Is.EqualTo("graybox-prototype-3d"));
            CollectionAssert.AreEqual(new[]
            {
                "Assets/_Game/Tests/EditMode/GrayboxDefenseObservabilityTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxDefenseRuntimeInputTests.cs",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs",
            }, hud.RequiredTestFiles);
        }

        [Test]
        public void CommittedCatalog_MapsIdea0017CampaignOwnershipAndReuse()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());

            foreach (string featureId in new[]
            {
                "foundation-clock",
                "city-navigation-deployment",
                "building-construction-evacuation",
                "ui-input",
                "economy-production-logistics",
                "combat-routes",
                "persistence-migration",
                "scene-editor-build-performance",
            })
            {
                CollectionAssert.Contains(
                    FindFeature(catalog, featureId).RequirementIds,
                    "IDEA-0017",
                    featureId);
            }

            ProjectFeatureGroup foundation = FindFeature(
                catalog,
                "foundation-clock");
            CollectionAssert.Contains(
                foundation.SourceGlobs,
                "Assets/_Game/Scripts/Graybox3D/GrayboxFormalRuleClock3D.cs");
            var expectedTestOwnership = new Dictionary<string, string[]>
            {
                {
                    "foundation-clock",
                    new[]
                    {
                        "Assets/_Game/Tests/EditMode/GrayboxUnifiedRuleClockContractTests.cs",
                    }
                },
                {
                    "building-construction-evacuation",
                    new[]
                    {
                        "Assets/_Game/Tests/EditMode/GrayboxBuildingCombatLifecycleTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxBuildingHealthRuntime3DTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxCombatDestructionCoordinator3DTests.cs",
                    }
                },
                {
                    "ui-input",
                    new[]
                    {
                        "Assets/_Game/Tests/EditMode/FormalGameSpeedCommandFacadeTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxFormalSpeedHudAndTerminalTests.cs",
                    }
                },
                {
                    "economy-production-logistics",
                    new[]
                    {
                        "Assets/_Game/Tests/EditMode/CityResourceStorageCombatLossTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxProductionCombatLossTests.cs",
                    }
                },
                {
                    "combat-routes",
                    new[]
                    {
                        "Assets/_Game/Tests/EditMode/GrayboxDefenseSettledAttackPresentationTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxDefenseSelectionProjectionTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxDefenseTowerCombatLossTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxFormalDefenseCampaignRuntimeIntegrationTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseCampaignCatalogTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseCampaignCheckpointTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseCampaignModelContractTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseCampaignPersistenceTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseEnemyCampaignCombatTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseTowerCombatModelTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseTowerPersistenceTests.cs",
                        "Assets/_Game/Tests/EditMode/SingleCityDefenseTowerTargetingTests.cs",
                    }
                },
                {
                    "persistence-migration",
                    new[]
                    {
                        "Assets/_Game/Tests/EditMode/FormalSaveDestroyedRuinSchema32Tests.cs",
                        "Assets/_Game/Tests/EditMode/FormalSaveSchema32ContractTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxFormalDefenseCampaignSaveAdapterTests.cs",
                        "Assets/_Game/Tests/EditMode/GrayboxFormalGameSpeedPersistenceTests.cs",
                    }
                },
            };
            foreach (KeyValuePair<string, string[]> owner in
                     expectedTestOwnership)
            {
                ProjectFeatureGroup feature = FindFeature(catalog, owner.Key);
                CollectionAssert.IsSubsetOf(
                    owner.Value,
                    feature.TestFileGlobs,
                    owner.Key);
            }

            AssertIdea0017ReuseBoundary(
                FindReuse(catalog, "single-city-defense-campaign-model"),
                "combat-routes",
                ProjectReuseLevel.Recommended,
                "Assets/_Game/Scripts/Defense/SingleCityDefenseCampaignModel.cs",
                "SingleCityDefenseCampaignModel",
                "Assets/_Game/Tests/EditMode/SingleCityDefenseCampaignModelContractTests.cs",
                "不读取 Unity 时间",
                "不直接摧毁建筑");
            AssertIdea0017ReuseBoundary(
                FindReuse(catalog, "graybox-building-health-runtime-3d"),
                "building-construction-evacuation",
                ProjectReuseLevel.ReviewBeforeReuse,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingHealthRuntime3D.cs",
                "GrayboxBuildingHealthRuntime3D",
                "Assets/_Game/Tests/EditMode/GrayboxBuildingHealthRuntime3DTests.cs",
                "生命真值",
                "统一战斗摧毁协调器");
            AssertIdea0017ReuseBoundary(
                FindReuse(catalog, "graybox-combat-destruction-coordinator-3d"),
                "building-construction-evacuation",
                ProjectReuseLevel.ReviewBeforeReuse,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxCombatDestructionCoordinator3D.cs",
                "GrayboxCombatDestructionCoordinator3D",
                "Assets/_Game/Tests/EditMode/GrayboxCombatDestructionCoordinator3DTests.cs",
                "幂等",
                "不计算攻击命中");
            AssertIdea0017ReuseBoundary(
                FindReuse(catalog, "formal-defense-campaign-persistence-3d"),
                "persistence-migration",
                ProjectReuseLevel.ReviewBeforeReuse,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxFormalDefenseCampaignPersistence3D.cs",
                "GrayboxFormalDefenseCampaignRestorePlan3D",
                "Assets/_Game/Tests/EditMode/GrayboxFormalDefenseCampaignSaveAdapterTests.cs",
                "零写入恢复计划",
                "保守默认值");
            AssertIdea0017ReuseBoundary(
                FindReuse(catalog, "graybox-formal-rule-clock-3d"),
                "foundation-clock",
                ProjectReuseLevel.SceneOnly,
                "Assets/_Game/Scripts/Graybox3D/GrayboxFormalRuleClock3D.cs",
                "GrayboxFormalRuleClock3D",
                "Assets/_Game/Tests/EditMode/GrayboxUnifiedRuleClockContractTests.cs",
                "唯一有效规则时间",
                "另起 Update 时钟");
            AssertIdea0017ReuseBoundary(
                FindReuse(
                    catalog,
                    "graybox-defense-settled-attack-presentation-3d"),
                "combat-routes",
                ProjectReuseLevel.SceneOnly,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseWorldView3D.cs",
                "GrayboxDefenseSettledAttackEvent3D",
                "Assets/_Game/Tests/EditMode/GrayboxDefenseSettledAttackPresentationTests.cs",
                "已结算",
                "不得反向决定命中");
            AssertIdea0017ReuseBoundary(
                FindReuse(catalog, "graybox-defense-selection-hud-3d"),
                "ui-input",
                ProjectReuseLevel.SceneOnly,
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseSelectionProjection3D.cs",
                "GrayboxDefenseSelectionProjection3D",
                "Assets/_Game/Tests/EditMode/GrayboxDefenseSelectionProjectionTests.cs",
                "只读投影",
                "不伪造废墟库存数量");
        }

        [Test]
        public void CommittedCatalog_MapsIdea0014FormalEvacuationOwnershipAndBoundaries()
        {
            ProjectQualityCatalog catalog =
                ProjectQualityCatalogLoader.LoadFromFile(CatalogPath());

            foreach (string featureId in new[]
            {
                "city-navigation-deployment",
                "building-construction-evacuation",
                "ui-input",
                "economy-production-logistics",
                "research-population",
                "combat-routes",
                "scene-editor-build-performance",
            })
                CollectionAssert.Contains(
                    FindFeature(catalog, featureId).RequirementIds,
                    "IDEA-0014",
                    featureId + " must own its IDEA-0014 changes");

            ProjectFeatureGroup building = FindFeature(
                catalog,
                "building-construction-evacuation");
            CollectionAssert.Contains(
                building.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxFormalEvacuationPerformanceTests.cs");
            CollectionAssert.Contains(
                building.TestFileGlobs,
                "Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs");
            CollectionAssert.Contains(
                building.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");

            ProjectFeatureGroup ui = FindFeature(catalog, "ui-input");
            CollectionAssert.Contains(
                ui.TestFileGlobs,
                "Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs");
            CollectionAssert.Contains(
                ui.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");

            ProjectFeatureGroup performance = FindFeature(
                catalog,
                "scene-editor-build-performance");
            CollectionAssert.Contains(
                performance.TestFileGlobs,
                "Assets/_Game/Tests/EditMode/GrayboxFormalEvacuationPerformanceTests.cs");
            CollectionAssert.Contains(
                performance.ScenePaths,
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity");

            ProjectFeatureGroup legacyCompatibility = FindFeature(
                catalog,
                "legacy-rules-compatibility");
            CollectionAssert.DoesNotContain(
                legacyCompatibility.RequirementIds,
                "IDEA-0014");
            CollectionAssert.DoesNotContain(
                legacyCompatibility.TestFileGlobs,
                "Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs");

            AssertIdea0014ReuseBoundary(
                FindReuse(catalog, "city-deployment-state"),
                "Assets/_Game/Scripts/City/CityDeploymentModel.cs",
                "CityDeploymentModel",
                "Assets/_Game/Tests/EditMode/CityDeploymentRulesTests.cs",
                "部署状态");
            AssertIdea0014ReuseBoundary(
                FindReuse(catalog, "building-evacuation-rules"),
                "Assets/_Game/Scripts/Building/BuildingEvacuationRules.cs",
                "BuildingEvacuationRules",
                "Assets/_Game/Tests/EditMode/GrayboxEvacuationTests.cs",
                "纯规则");
            AssertIdea0014ReuseBoundary(
                FindReuse(catalog, "graybox-evacuation-runtime-3d"),
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs",
                "GrayboxEvacuationController3D",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs",
                "内部载荷",
                "不可变");
            AssertIdea0014ReuseBoundary(
                FindReuse(catalog, "city-resource-storage-model"),
                "Assets/_Game/Scripts/Economy/CityResourceStorageModel.cs",
                "CityResourceStorageModel",
                "Assets/_Game/Tests/EditMode/GrayboxWarehouseStorageIntegrationTests.cs",
                "原子");
            AssertIdea0014ReuseBoundary(
                FindReuse(catalog, "building-input-router-3d"),
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingInputRouter3D.cs",
                "GrayboxBuildingInputRouter3D",
                "Assets/_Game/Tests/PlayMode/GrayboxFormalEvacuationVerticalSliceTests.cs",
                "真实输入");
            AssertIdea0014ReuseBoundary(
                FindReuse(catalog, "graybox-performance-probe"),
                "Assets/_Game/Editor/GrayboxPerformanceProbe.cs",
                "GrayboxPerformanceProbe",
                "Assets/_Game/Tests/EditMode/GrayboxFormalEvacuationPerformanceTests.cs",
                "混合");
        }

        private static void AssertIdea0014ReuseBoundary(
            ProjectReuseEntry entry,
            string assetPath,
            string typeName,
            string requiredTestFile,
            params string[] boundaryTerms)
        {
            CollectionAssert.Contains(entry.AssetPaths, assetPath, entry.Id);
            CollectionAssert.Contains(entry.TypeNames, typeName, entry.Id);
            CollectionAssert.Contains(
                entry.RequiredTestFiles,
                requiredTestFile,
                entry.Id);
            CollectionAssert.Contains(entry.RequirementIds, "IDEA-0014", entry.Id);
            string guidance = entry.UseSummary + "\n" + entry.BoundarySummary;
            foreach (string term in boundaryTerms)
                StringAssert.Contains(term, guidance, entry.Id);
        }

        private static void AssertIdea0017ReuseBoundary(
            ProjectReuseEntry entry,
            string featureGroupId,
            ProjectReuseLevel reuseLevel,
            string assetPath,
            string typeName,
            string requiredTestFile,
            params string[] boundaryTerms)
        {
            Assert.That(entry.FeatureGroupId, Is.EqualTo(featureGroupId));
            Assert.That(entry.ReuseLevel, Is.EqualTo(reuseLevel));
            CollectionAssert.Contains(entry.AssetPaths, assetPath, entry.Id);
            CollectionAssert.Contains(entry.TypeNames, typeName, entry.Id);
            CollectionAssert.Contains(
                entry.RequiredTestFiles,
                requiredTestFile,
                entry.Id);
            CollectionAssert.AreEqual(
                entry.Id == "graybox-defense-selection-hud-3d"
                    ? new[] { "IDEA-0017", "IDEA-0018" }
                    : entry.Id == "graybox-formal-rule-clock-3d"
                        ? new[] { "IDEA-0017", "IDEA-0020" }
                        : new[] { "IDEA-0017" },
                entry.RequirementIds,
                entry.Id);
            string guidance = entry.UseSummary + "\n" +
                              entry.BoundarySummary;
            foreach (string term in boundaryTerms)
                StringAssert.Contains(term, guidance, entry.Id);
        }

        private static void AssertDefenseReuse(
            ProjectReuseEntry entry,
            string featureGroupId,
            ProjectReuseLevel reuseLevel,
            string[] typeNames,
            string[] assetPaths,
            string[] requiredTestFiles,
            string[] requirementIds = null)
        {
            Assert.That(entry.FeatureGroupId, Is.EqualTo(featureGroupId));
            Assert.That(entry.ReuseLevel, Is.EqualTo(reuseLevel));
            CollectionAssert.AreEqual(typeNames, entry.TypeNames);
            CollectionAssert.AreEqual(assetPaths, entry.AssetPaths);
            CollectionAssert.AreEqual(requiredTestFiles, entry.RequiredTestFiles);
            CollectionAssert.AreEqual(
                requirementIds ?? new[] { "IDEA-0013" },
                entry.RequirementIds);
        }

        private static void AssertReuseContract(
            ProjectReuseEntry entry,
            string[] expectedAssetPaths,
            string[] expectedTypeNames,
            string[] expectedRequiredTestFiles,
            string[] expectedRequirementIds = null)
        {
            Assert.That(entry.FeatureGroupId,
                Is.EqualTo("economy-production-logistics"));
            CollectionAssert.AreEqual(expectedAssetPaths, entry.AssetPaths);
            CollectionAssert.AreEqual(expectedTypeNames, entry.TypeNames);
            CollectionAssert.AreEqual(
                expectedRequiredTestFiles,
                entry.RequiredTestFiles);
            CollectionAssert.AreEqual(
                expectedRequirementIds ?? new[] { "IDEA-0011" },
                entry.RequirementIds);
        }

        private static ProjectFeatureGroup FindFeature(ProjectQualityCatalog catalog, string id)
        {
            foreach (ProjectFeatureGroup feature in catalog.FeatureGroups)
                if (feature.Id == id) return feature;
            Assert.Fail("Missing feature: " + id);
            return null;
        }

        private static ProjectReuseEntry FindReuse(ProjectQualityCatalog catalog, string id)
        {
            foreach (ProjectReuseEntry entry in catalog.ReuseEntries)
                if (entry.Id == id) return entry;
            Assert.Fail("Missing reuse entry: " + id);
            return null;
        }

        private static ProjectUiEntry FindUi(ProjectQualityCatalog catalog, string id)
        {
            foreach (ProjectUiEntry entry in catalog.UiEntries)
                if (entry.Id == id) return entry;
            Assert.Fail("Missing UI entry: " + id);
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
                AssertArraysEqual(a.PrimarySourceGlobs, b.PrimarySourceGlobs);
                Assert.That(b.FailureLocationSummary, Is.EqualTo(a.FailureLocationSummary));
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
