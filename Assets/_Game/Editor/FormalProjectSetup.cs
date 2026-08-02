using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WasteCity.City;
using WasteCity.Core;
using WasteCity.UI;
using WasteCity.World;
using WasteCity.Economy;
using WasteCity.Legacy;
using WasteCity.Building;
using WasteCity.Research;
using WasteCity.Persistence;
using WasteCity.Progression;
using WasteCity.Combat;
using WasteCity.Population;
using WasteCity.Presentation;
using WasteCity.Narrative;
using WasteCity.Leader;

namespace WasteCity.Editor
{
    public static class FormalProjectSetup
    {
        public static void Configure()
        {
            EnsureFolder("Assets/_Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 13f;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.05f); camera.clearFlags = CameraClearFlags.SolidColor;
            var scanVisual = new GameObject("AdvancementScanVisual"); scanVisual.transform.SetParent(cameraObject.transform, false); scanVisual.transform.localPosition = new Vector3(0f, 0f, 10f); scanVisual.transform.localScale = new Vector3(32f, 24f, 1f);
            var scanRenderer = scanVisual.AddComponent<SpriteRenderer>(); scanRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); scanRenderer.color = new Color(.15f, .9f, 1f, .12f); scanRenderer.sortingOrder = 100;
            VisualSlot.Attach(scanVisual, "core.effect.civilization-advancement-scan", scanRenderer, scanRenderer.color); scanVisual.SetActive(false);

            var world = new GameObject("PlaceholderWorld");
            var worldView = world.AddComponent<PlaceholderWorldView>();
            var city = new GameObject("PlaceholderMobileCity"); city.transform.position = new Vector3(-8f, -5f, -1f);
            var renderer = city.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); renderer.color = new Color(0.9f, 0.48f, 0.1f); renderer.sortingOrder = 10;
            city.transform.localScale = new Vector3(3f, 2f, 1f);
            VisualSlot.Attach(city,"core.city.mobile",renderer,renderer.color);
            var body = city.AddComponent<Rigidbody2D>(); body.gravityScale = 0f; body.freezeRotation = true; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            city.AddComponent<BoxCollider2D>(); city.AddComponent<PlaceholderMobileCity>();
            var cityHealth = city.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);
            var leaderVisual=new GameObject("PlaceholderLeader_CenJin");var leaderRenderer=leaderVisual.AddComponent<SpriteRenderer>();leaderRenderer.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");leaderRenderer.color=new Color(.2f,.85f,.95f);leaderRenderer.sortingOrder=12;leaderVisual.transform.localScale=Vector3.one*.65f;VisualSlot.Attach(leaderVisual,"core.character.cen-jin",leaderRenderer,leaderRenderer.color);leaderVisual.SetActive(false);

            var systems = new GameObject("FormalGameBootstrap");
            var visualProvider = systems.AddComponent<VisualLibraryProvider>(); var visualLibrary = LoadOrCreateVisualLibrary(); var visualProviderData = new SerializedObject(visualProvider); visualProviderData.FindProperty("library").objectReferenceValue = visualLibrary; visualProviderData.ApplyModifiedPropertiesWithoutUndo();
            var gameSpeed = systems.AddComponent<GameSpeedController>();
            var clock = systems.AddComponent<FormalGameClockController>();
            var legacy = systems.AddComponent<LegacySelectionController>();
            var bootstrap = systems.AddComponent<FormalGameBootstrap>();
            var data = new SerializedObject(bootstrap); data.FindProperty("worldView").objectReferenceValue = worldView; data.ApplyModifiedPropertiesWithoutUndo();
            var exploration = systems.AddComponent<WorldExplorationController>();
            var explorationData = new SerializedObject(exploration);
            explorationData.FindProperty("world").objectReferenceValue = worldView;
            explorationData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            explorationData.ApplyModifiedPropertiesWithoutUndo();
            var economy = systems.AddComponent<FormalEconomyController>();
            var population = systems.AddComponent<FormalPopulationController>();
            var economyData = new SerializedObject(economy);
            economyData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            economyData.FindProperty("world").objectReferenceValue = worldView;
            economyData.ApplyModifiedPropertiesWithoutUndo();
            var research = systems.AddComponent<ResearchController>();
            var researchData = new SerializedObject(research); researchData.FindProperty("economy").objectReferenceValue = economy; researchData.ApplyModifiedPropertiesWithoutUndo();
            var saves = systems.AddComponent<FormalSaveController>();
            var saveData = new SerializedObject(saves);
            saveData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            saveData.FindProperty("economy").objectReferenceValue = economy;
            saveData.FindProperty("legacy").objectReferenceValue = legacy;
            saveData.FindProperty("population").objectReferenceValue = population;
            saveData.FindProperty("cityHealth").objectReferenceValue = cityHealth;
            saveData.ApplyModifiedPropertiesWithoutUndo();
            var buildingRoot = new GameObject("PlaceholderBuildings");
            buildingRoot.transform.SetParent(city.transform, true);
            var buildingController = buildingRoot.AddComponent<PlaceholderBuildingController>();
            var buildingData = new SerializedObject(buildingController);
            buildingData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            buildingData.FindProperty("economy").objectReferenceValue = economy;
            buildingData.FindProperty("world").objectReferenceValue = worldView;
            buildingData.FindProperty("population").objectReferenceValue = population;
            buildingData.FindProperty("research").objectReferenceValue = research;
            buildingData.ApplyModifiedPropertiesWithoutUndo();
            researchData.FindProperty("buildings").objectReferenceValue = buildingController; researchData.FindProperty("city").objectReferenceValue=city.GetComponent<PlaceholderMobileCity>(); researchData.ApplyModifiedPropertiesWithoutUndo(); saveData.FindProperty("research").objectReferenceValue = research; saveData.ApplyModifiedPropertiesWithoutUndo();
            saveData.FindProperty("buildings").objectReferenceValue = buildingController; saveData.ApplyModifiedPropertiesWithoutUndo();
            var production = systems.AddComponent<TechnologyProductionController>();
            var productionData = new SerializedObject(production); productionData.FindProperty("economy").objectReferenceValue=economy; productionData.FindProperty("buildings").objectReferenceValue=buildingController; productionData.ApplyModifiedPropertiesWithoutUndo();
            saveData.FindProperty("production").objectReferenceValue=production;saveData.ApplyModifiedPropertiesWithoutUndo();
            productionData.FindProperty("city").objectReferenceValue=city.GetComponent<PlaceholderMobileCity>();productionData.FindProperty("world").objectReferenceValue=worldView;productionData.ApplyModifiedPropertiesWithoutUndo();
            var elixir=systems.AddComponent<ElixirController>();var elixirData=new SerializedObject(elixir);elixirData.FindProperty("economy").objectReferenceValue=economy;elixirData.FindProperty("city").objectReferenceValue=city.GetComponent<PlaceholderMobileCity>();elixirData.FindProperty("cityHealth").objectReferenceValue=cityHealth;elixirData.ApplyModifiedPropertiesWithoutUndo();
            var progression = systems.AddComponent<FormalProgressionController>();
            var progressionData = new SerializedObject(progression);
            progressionData.FindProperty("research").objectReferenceValue = research;
            progressionData.FindProperty("buildings").objectReferenceValue = buildingController;
            progressionData.FindProperty("legacy").objectReferenceValue = legacy;
            progressionData.FindProperty("production").objectReferenceValue = production;
            progressionData.ApplyModifiedPropertiesWithoutUndo();
            buildingData.FindProperty("progression").objectReferenceValue=progression;buildingData.ApplyModifiedPropertiesWithoutUndo();
            saveData.FindProperty("progression").objectReferenceValue = progression; saveData.ApplyModifiedPropertiesWithoutUndo();
            var combat = systems.AddComponent<FormalCombatController>();
            var combatData = new SerializedObject(combat);
            combatData.FindProperty("cityHealth").objectReferenceValue = cityHealth;
            combatData.FindProperty("city").objectReferenceValue = city.transform;
            combatData.FindProperty("progression").objectReferenceValue = progression;
            combatData.FindProperty("economy").objectReferenceValue = economy;
            combatData.FindProperty("cityState").objectReferenceValue=city.GetComponent<PlaceholderMobileCity>();
            combatData.FindProperty("buildings").objectReferenceValue=buildingController;
            combatData.FindProperty("research").objectReferenceValue=research;
            combatData.ApplyModifiedPropertiesWithoutUndo();
            progressionData.FindProperty("combat").objectReferenceValue=combat;progressionData.ApplyModifiedPropertiesWithoutUndo();
            saveData.FindProperty("combat").objectReferenceValue=combat;saveData.ApplyModifiedPropertiesWithoutUndo();
            var advancement=systems.AddComponent<FormalAdvancementController>();var advancementData=new SerializedObject(advancement);advancementData.FindProperty("progression").objectReferenceValue=progression;advancementData.FindProperty("saves").objectReferenceValue=saves;advancementData.FindProperty("scanVisual").objectReferenceValue=scanVisual;advancementData.ApplyModifiedPropertiesWithoutUndo();saveData.FindProperty("advancement").objectReferenceValue=advancement;saveData.ApplyModifiedPropertiesWithoutUndo();
            var guidance=systems.AddComponent<FormalGuidanceController>();var guidanceData=new SerializedObject(guidance);guidanceData.FindProperty("city").objectReferenceValue=city.GetComponent<PlaceholderMobileCity>();guidanceData.FindProperty("buildings").objectReferenceValue=buildingController;guidanceData.FindProperty("combat").objectReferenceValue=combat;guidanceData.FindProperty("progression").objectReferenceValue=progression;guidanceData.ApplyModifiedPropertiesWithoutUndo();saveData.FindProperty("guidance").objectReferenceValue=guidance;saveData.ApplyModifiedPropertiesWithoutUndo();
            var session=systems.AddComponent<FormalSessionController>();var sessionData=new SerializedObject(session);sessionData.FindProperty("cityHealth").objectReferenceValue=cityHealth;sessionData.FindProperty("saves").objectReferenceValue=saves;sessionData.FindProperty("guidance").objectReferenceValue=guidance;sessionData.FindProperty("advancement").objectReferenceValue=advancement;sessionData.ApplyModifiedPropertiesWithoutUndo();
            var rescueSites = systems.AddComponent<RescueSiteController>(); var rescueData = new SerializedObject(rescueSites); rescueData.FindProperty("world").objectReferenceValue = worldView; rescueData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>(); rescueData.FindProperty("economy").objectReferenceValue = economy; rescueData.FindProperty("population").objectReferenceValue = population; rescueData.FindProperty("progression").objectReferenceValue = progression; rescueData.FindProperty("research").objectReferenceValue=research; rescueData.ApplyModifiedPropertiesWithoutUndo();
            saveData.FindProperty("rescueSites").objectReferenceValue = rescueSites; saveData.ApplyModifiedPropertiesWithoutUndo();
            var leader=systems.AddComponent<FormalLeaderController>();var leaderData=new SerializedObject(leader);leaderData.FindProperty("rescueSites").objectReferenceValue=rescueSites;leaderData.FindProperty("city").objectReferenceValue=city.GetComponent<PlaceholderMobileCity>();leaderData.FindProperty("visual").objectReferenceValue=leaderVisual.transform;leaderData.FindProperty("research").objectReferenceValue=research;leaderData.ApplyModifiedPropertiesWithoutUndo();buildingController.SetTurretFireRateSource(leader);productionData.FindProperty("leader").objectReferenceValue=leader;productionData.ApplyModifiedPropertiesWithoutUndo();saveData.FindProperty("leader").objectReferenceValue=leader;saveData.ApplyModifiedPropertiesWithoutUndo();
            var statistics=systems.AddComponent<FormalSessionStatisticsController>();var statisticsData=new SerializedObject(statistics);statisticsData.FindProperty("combat").objectReferenceValue=combat;statisticsData.FindProperty("buildings").objectReferenceValue=buildingController;statisticsData.FindProperty("production").objectReferenceValue=production;statisticsData.FindProperty("rescue").objectReferenceValue=rescueSites;statisticsData.FindProperty("city").objectReferenceValue=city.GetComponent<PlaceholderMobileCity>();statisticsData.FindProperty("guidance").objectReferenceValue=guidance;statisticsData.FindProperty("progression").objectReferenceValue=progression;statisticsData.ApplyModifiedPropertiesWithoutUndo();advancementData.FindProperty("statistics").objectReferenceValue=statistics;advancementData.ApplyModifiedPropertiesWithoutUndo();saveData.FindProperty("statistics").objectReferenceValue=statistics;saveData.ApplyModifiedPropertiesWithoutUndo();
            var legacyEffects = systems.AddComponent<LegacyEffectsController>();
            var legacyEffectsData = new SerializedObject(legacyEffects); legacyEffectsData.FindProperty("selection").objectReferenceValue = legacy; legacyEffectsData.FindProperty("economy").objectReferenceValue = economy; legacyEffectsData.FindProperty("progression").objectReferenceValue = progression; legacyEffectsData.FindProperty("combat").objectReferenceValue = combat; legacyEffectsData.ApplyModifiedPropertiesWithoutUndo();
            productionData.FindProperty("legacyEffects").objectReferenceValue = legacyEffects; productionData.ApplyModifiedPropertiesWithoutUndo();
            var foresight = systems.AddComponent<ForesightFlashController>(); var foresightData = new SerializedObject(foresight); foresightData.FindProperty("legacy").objectReferenceValue = legacy; foresightData.FindProperty("clock").objectReferenceValue = clock; foresightData.ApplyModifiedPropertiesWithoutUndo();
            saveData.FindProperty("clock").objectReferenceValue = clock; saveData.FindProperty("foresight").objectReferenceValue = foresight; saveData.ApplyModifiedPropertiesWithoutUndo();
            var localHaste = systems.AddComponent<LocalHasteController>(); var hasteData = new SerializedObject(localHaste); hasteData.FindProperty("legacy").objectReferenceValue = legacy; hasteData.FindProperty("clock").objectReferenceValue = clock; hasteData.FindProperty("buildings").objectReferenceValue = buildingController; hasteData.ApplyModifiedPropertiesWithoutUndo(); buildingController.SetLocalTimeSource(localHaste);
            productionData.FindProperty("localHaste").objectReferenceValue = localHaste; productionData.ApplyModifiedPropertiesWithoutUndo(); saveData.FindProperty("localHaste").objectReferenceValue = localHaste; saveData.ApplyModifiedPropertiesWithoutUndo();
            var spatialTemplate = systems.AddComponent<SpatialTemplateController>(); var templateData = new SerializedObject(spatialTemplate); templateData.FindProperty("legacy").objectReferenceValue = legacy; templateData.FindProperty("buildings").objectReferenceValue = buildingController; templateData.ApplyModifiedPropertiesWithoutUndo(); saveData.FindProperty("spatialTemplate").objectReferenceValue = spatialTemplate; saveData.ApplyModifiedPropertiesWithoutUndo();
            var territory = systems.AddComponent<TerritoryCacheController>(); var territoryData = new SerializedObject(territory); territoryData.FindProperty("legacy").objectReferenceValue = legacy; territoryData.FindProperty("world").objectReferenceValue = worldView; territoryData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>(); territoryData.FindProperty("economy").objectReferenceValue = economy; territoryData.ApplyModifiedPropertiesWithoutUndo(); saveData.FindProperty("worldView").objectReferenceValue = worldView; saveData.FindProperty("territory").objectReferenceValue = territory; saveData.ApplyModifiedPropertiesWithoutUndo();
            var rewind = systems.AddComponent<RewindAnchorController>(); var rewindData = new SerializedObject(rewind); rewindData.FindProperty("legacy").objectReferenceValue = legacy; rewindData.FindProperty("saves").objectReferenceValue = saves; rewindData.FindProperty("progression").objectReferenceValue = progression; rewindData.ApplyModifiedPropertiesWithoutUndo();
            var guide = systems.AddComponent<OnboardingGuideController>();
            var guideData = new SerializedObject(guide); guideData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>(); guideData.FindProperty("economy").objectReferenceValue = economy; guideData.FindProperty("buildings").objectReferenceValue = buildingController; guideData.ApplyModifiedPropertiesWithoutUndo();
            var hud = new GameObject("PlaceholderHUD").AddComponent<FormalPlaceholderHud>();
            var hudData = new SerializedObject(hud); hudData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>(); hudData.FindProperty("economy").objectReferenceValue = economy; hudData.FindProperty("population").objectReferenceValue = population; hudData.FindProperty("cityHealth").objectReferenceValue = cityHealth; hudData.FindProperty("guide").objectReferenceValue = guide; hudData.FindProperty("gameSpeed").objectReferenceValue = gameSpeed; hudData.FindProperty("clock").objectReferenceValue = clock; hudData.ApplyModifiedPropertiesWithoutUndo();
            var title=systems.AddComponent<FormalTitleMenuController>();var titleData=new SerializedObject(title);titleData.FindProperty("saves").objectReferenceValue=saves;titleData.FindProperty("advancement").objectReferenceValue=advancement;titleData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/FormalPrototype.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/_Game/Scenes/FormalPrototype.unity", true) };
            PlayerSettings.productName = "Waste City"; PlayerSettings.companyName = "废土游戏";
            AssetDatabase.SaveAssets(); Debug.Log("Formal project foundation configured.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static VisualLibrary LoadOrCreateVisualLibrary()
        {
            EnsureFolder("Assets/_Game/ArtIntegration"); const string path="Assets/_Game/ArtIntegration/VisualLibrary.asset"; var library=AssetDatabase.LoadAssetAtPath<VisualLibrary>(path); if(library!=null)return library; library=ScriptableObject.CreateInstance<VisualLibrary>();AssetDatabase.CreateAsset(library,path);return library;
        }
    }
}
