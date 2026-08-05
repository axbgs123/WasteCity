using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace WasteCity.Editor
{
    public static class FormalBuildTools
    {
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/GrayboxPrototype3D.unity" },
                locationPathName = "Builds/Windows/WasteCity.exe",
                target = BuildTarget.StandaloneWindows64
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
        }

        public static void BuildWindowsLegacy2D()
        {
            Directory.CreateDirectory("Builds/Windows2D");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/FormalPrototype.unity" },
                locationPathName = "Builds/Windows2D/WasteCity2D.exe",
                target = BuildTarget.StandaloneWindows64
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
        }

        public static void BuildWindowsGraybox3D()
        {
            Directory.CreateDirectory("Builds/Windows3D");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/GrayboxPrototype3D.unity" },
                locationPathName = "Builds/Windows3D/WasteCityGraybox.exe",
                target = BuildTarget.StandaloneWindows64
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
        }
    }
}
