using System;
using System.Collections.Generic;
using UnityEngine;

namespace WasteCity.Graybox3D.Usability
{
    public interface IGrayboxApplicationExit
    {
        void Exit();
    }

    public sealed class UnityGrayboxApplicationExit3D :
        IGrayboxApplicationExit
    {
        public void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    public sealed class PlayerPrefsGrayboxDisplaySettingsStore3D :
        IGrayboxDisplaySettingsStore
    {
        public const string VersionKey = "wastecity.settings.version";
        public const string WidthKey = "wastecity.display.width";
        public const string HeightKey = "wastecity.display.height";
        public const string WindowModeKey =
            "wastecity.display.window-mode";

        public bool TryLoad(
            out int version,
            out GrayboxDisplaySettings3D settings)
        {
            version = 0;
            settings = default;
            if (!PlayerPrefs.HasKey(VersionKey) ||
                !PlayerPrefs.HasKey(WidthKey) ||
                !PlayerPrefs.HasKey(HeightKey) ||
                !PlayerPrefs.HasKey(WindowModeKey))
                return false;

            version = PlayerPrefs.GetInt(VersionKey);
            settings = new GrayboxDisplaySettings3D(
                PlayerPrefs.GetInt(WidthKey),
                PlayerPrefs.GetInt(HeightKey),
                (GrayboxWindowMode3D)PlayerPrefs.GetInt(WindowModeKey));
            return true;
        }

        public void Save(
            int version,
            GrayboxDisplaySettings3D settings)
        {
            PlayerPrefs.SetInt(VersionKey, version);
            PlayerPrefs.SetInt(WidthKey, settings.Width);
            PlayerPrefs.SetInt(HeightKey, settings.Height);
            PlayerPrefs.SetInt(WindowModeKey, (int)settings.WindowMode);
            PlayerPrefs.Save();
        }
    }

    public sealed class UnityGrayboxDisplaySettingsPlatform3D :
        IGrayboxDisplaySettingsPlatform
    {
        public IReadOnlyList<GrayboxDisplayResolution3D>
            AvailableResolutions
        {
            get
            {
                Resolution[] source = Screen.resolutions;
                var result = new GrayboxDisplayResolution3D[source.Length];
                for (var index = 0; index < source.Length; index++)
                    result[index] = new GrayboxDisplayResolution3D(
                        source[index].width,
                        source[index].height);
                return result;
            }
        }

        public GrayboxDisplaySettings3D Current
        {
            get
            {
                Resolution current = Screen.currentResolution;
                return new GrayboxDisplaySettings3D(
                    current.width,
                    current.height,
                    Screen.fullScreenMode == FullScreenMode.Windowed
                        ? GrayboxWindowMode3D.Windowed
                        : GrayboxWindowMode3D.FullScreenWindow);
            }
        }

        public bool TryApply(GrayboxDisplaySettings3D settings)
        {
            if (settings.Width <= 0 || settings.Height <= 0 ||
                (settings.WindowMode != GrayboxWindowMode3D.Windowed &&
                 settings.WindowMode !=
                    GrayboxWindowMode3D.FullScreenWindow))
                return false;
            try
            {
                Screen.SetResolution(
                    settings.Width,
                    settings.Height,
                    settings.WindowMode ==
                        GrayboxWindowMode3D.Windowed
                        ? FullScreenMode.Windowed
                        : FullScreenMode.FullScreenWindow);
                return true;
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "Display settings could not be applied: " +
                    exception.Message);
#else
                _ = exception;
#endif
                return false;
            }
        }
    }
}
