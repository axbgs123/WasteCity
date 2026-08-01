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

            var systems = new GameObject("FormalGameBootstrap");
            systems.AddComponent<LegacySelectionController>();
            var bootstrap = systems.AddComponent<FormalGameBootstrap>();
            var data = new SerializedObject(bootstrap); data.FindProperty("worldView").objectReferenceValue = worldView; data.ApplyModifiedPropertiesWithoutUndo();
            var exploration = systems.AddComponent<WorldExplorationController>();
            var explorationData = new SerializedObject(exploration);
            explorationData.FindProperty("world").objectReferenceValue = worldView;
            explorationData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            explorationData.ApplyModifiedPropertiesWithoutUndo();
            var economy = systems.AddComponent<FormalEconomyController>();
            var economyData = new SerializedObject(economy);
            economyData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            economyData.FindProperty("world").objectReferenceValue = worldView;
            economyData.ApplyModifiedPropertiesWithoutUndo();
            var research = systems.AddComponent<ResearchController>();
            var researchData = new SerializedObject(research); researchData.FindProperty("economy").objectReferenceValue = economy; researchData.ApplyModifiedPropertiesWithoutUndo();
            var buildingRoot = new GameObject("PlaceholderBuildings");
            var buildingController = buildingRoot.AddComponent<PlaceholderBuildingController>();
            var buildingData = new SerializedObject(buildingController);
            buildingData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>();
            buildingData.FindProperty("economy").objectReferenceValue = economy;
            buildingData.FindProperty("world").objectReferenceValue = worldView;
            buildingData.ApplyModifiedPropertiesWithoutUndo();
            var hud = new GameObject("PlaceholderHUD").AddComponent<FormalPlaceholderHud>();
            var hudData = new SerializedObject(hud); hudData.FindProperty("city").objectReferenceValue = city.GetComponent<PlaceholderMobileCity>(); hudData.FindProperty("economy").objectReferenceValue = economy; hudData.ApplyModifiedPropertiesWithoutUndo();
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
