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

            var world = new GameObject("PlaceholderWorld");
            var worldView = world.AddComponent<PlaceholderWorldView>();
            var city = new GameObject("PlaceholderMobileCity"); city.transform.position = new Vector3(-8f, -5f, -1f);
            var renderer = city.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); renderer.color = new Color(0.9f, 0.48f, 0.1f); renderer.sortingOrder = 10;
            city.transform.localScale = new Vector3(3f, 2f, 1f);
            var body = city.AddComponent<Rigidbody2D>(); body.gravityScale = 0f; body.freezeRotation = true; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            city.AddComponent<BoxCollider2D>(); city.AddComponent<PlaceholderMobileCity>();
            var cityHealth = city.AddComponent<HealthComponent>(); cityHealth.Configure(2000, ArmorType.Heavy);

            var systems = new GameObject("FormalGameBootstrap");
            var gameSpeed = systems.AddComponent<GameSpeedController>();
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
            var buildingController = buildingRoot.AddComponent<PlaceholderBuildingController>();
            var buildingData = new SerializedObject(buildingController);
            buildingData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            buildingData.FindProperty("economy").objectReferenceValue = economy;
            buildingData.FindProperty("world").objectReferenceValue = worldView;
            buildingData.FindProperty("population").objectReferenceValue = population;
            buildingData.ApplyModifiedPropertiesWithoutUndo();
            var production = systems.AddComponent<TechnologyProductionController>();
            var productionData = new SerializedObject(production); productionData.FindProperty("economy").objectReferenceValue=economy; productionData.FindProperty("buildings").objectReferenceValue=buildingController; productionData.ApplyModifiedPropertiesWithoutUndo();
            var progression = systems.AddComponent<FormalProgressionController>();
            var progressionData = new SerializedObject(progression);
            progressionData.FindProperty("research").objectReferenceValue = research;
            progressionData.FindProperty("buildings").objectReferenceValue = buildingController;
            progressionData.FindProperty("legacy").objectReferenceValue = legacy;
            progressionData.ApplyModifiedPropertiesWithoutUndo();
            saveData.FindProperty("progression").objectReferenceValue = progression; saveData.ApplyModifiedPropertiesWithoutUndo();
            var combat = systems.AddComponent<FormalCombatController>();
            var combatData = new SerializedObject(combat);
            combatData.FindProperty("cityHealth").objectReferenceValue = cityHealth;
            combatData.FindProperty("city").objectReferenceValue = city.transform;
            combatData.FindProperty("progression").objectReferenceValue = progression;
            combatData.FindProperty("economy").objectReferenceValue = economy;
            combatData.ApplyModifiedPropertiesWithoutUndo();
            var legacyEffects = systems.AddComponent<LegacyEffectsController>();
            var legacyEffectsData = new SerializedObject(legacyEffects); legacyEffectsData.FindProperty("selection").objectReferenceValue = legacy; legacyEffectsData.FindProperty("economy").objectReferenceValue = economy; legacyEffectsData.FindProperty("progression").objectReferenceValue = progression; legacyEffectsData.FindProperty("combat").objectReferenceValue = combat; legacyEffectsData.ApplyModifiedPropertiesWithoutUndo();
            productionData.FindProperty("legacyEffects").objectReferenceValue = legacyEffects; productionData.ApplyModifiedPropertiesWithoutUndo();
            var rewind = systems.AddComponent<RewindAnchorController>(); var rewindData = new SerializedObject(rewind); rewindData.FindProperty("legacy").objectReferenceValue = legacy; rewindData.FindProperty("saves").objectReferenceValue = saves; rewindData.FindProperty("progression").objectReferenceValue = progression; rewindData.ApplyModifiedPropertiesWithoutUndo();
            var guide = systems.AddComponent<OnboardingGuideController>();
            var guideData = new SerializedObject(guide); guideData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>(); guideData.FindProperty("economy").objectReferenceValue = economy; guideData.FindProperty("buildings").objectReferenceValue = buildingController; guideData.ApplyModifiedPropertiesWithoutUndo();
            var hud = new GameObject("PlaceholderHUD").AddComponent<FormalPlaceholderHud>();
            var hudData = new SerializedObject(hud); hudData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>(); hudData.FindProperty("economy").objectReferenceValue = economy; hudData.FindProperty("population").objectReferenceValue = population; hudData.FindProperty("cityHealth").objectReferenceValue = cityHealth; hudData.FindProperty("guide").objectReferenceValue = guide; hudData.FindProperty("gameSpeed").objectReferenceValue = gameSpeed; hudData.ApplyModifiedPropertiesWithoutUndo();
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
    }
}
