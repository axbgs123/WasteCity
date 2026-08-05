using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.UI;
#endif

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperModifierBootstrap3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GrayboxDeveloperModifier3D modifier;
        private GameObject panelRoot;
#endif

        public static bool ResolveRuntimeAvailability(
            bool isEditor,
            bool isDevelopmentBuild)
        {
            return isEditor || isDevelopmentBuild;
        }

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation)
        {
            this.session = session;
            this.city = city;
            this.presentation = presentation;
        }

        public bool TryTogglePanel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (panelRoot == null) return false;
            panelRoot.SetActive(!panelRoot.activeSelf);
            return true;
#else
            return false;
#endif
        }

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (session == null || city == null || presentation == null)
                return;
            modifier = new GrayboxDeveloperModifier3D(session, city);
            CreatePanel();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void CreatePanel()
        {
            panelRoot = new GameObject("Graybox Developer Modifier");
            panelRoot.transform.SetParent(transform, false);
            var labelRoot = new GameObject("Development Mode Label");
            labelRoot.transform.SetParent(panelRoot.transform, false);
            Text label = labelRoot.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            label.text = "开发模式";
            panelRoot.SetActive(false);
        }
#endif
    }
}
