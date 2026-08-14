using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WasteCity.Editor
{
    public static class FormalBuildTools
    {
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/GrayboxPrototype3D.unity" },
                    locationPathName = "Builds/Windows/WasteCity.exe",
                    target = BuildTarget.StandaloneWindows64
                });
            }
            finally
            {
                GrayboxRenderPipelineBuildScope.RestoreAfterBuild();
            }
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
        }

        public static void BuildWindowsLegacy2D()
        {
            Directory.CreateDirectory("Builds/Windows2D");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/FormalPrototype.unity" },
                    locationPathName = "Builds/Windows2D/WasteCity2D.exe",
                    target = BuildTarget.StandaloneWindows64
                });
            }
            finally
            {
                GrayboxRenderPipelineBuildScope.RestoreAfterBuild();
            }
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
        }

        public static void BuildWindowsGraybox3D()
        {
            Directory.CreateDirectory("Builds/Windows3D");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/GrayboxPrototype3D.unity" },
                    locationPathName = "Builds/Windows3D/WasteCityGraybox.exe",
                    target = BuildTarget.StandaloneWindows64
                });
            }
            finally
            {
                GrayboxRenderPipelineBuildScope.RestoreAfterBuild();
            }
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
        }

        public static void BuildWindowsGraybox3DDevelopment()
        {
            Directory.CreateDirectory("Builds/Windows3DDevelopment");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/GrayboxPrototype3D.unity" },
                    locationPathName = "Builds/Windows3DDevelopment/WasteCityGrayboxDev.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
            }
            finally
            {
                GrayboxRenderPipelineBuildScope.RestoreAfterBuild();
            }
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
        }
    }

    [InitializeOnLoad]
    internal sealed class GrayboxRenderPipelineBuildScope :
        BuildPlayerProcessor,
        IPostprocessBuildWithReport
    {
        private const string GrayboxScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        private const string GrayboxPipelinePath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset";
        private const string GraphicsSettingsPath =
            "ProjectSettings/GraphicsSettings.asset";
        private const string QualitySettingsPath =
            "ProjectSettings/QualitySettings.asset";
        private const string NullPipelineMarker = "<null>";
        private const string RestoreMarkerVersion =
            "BUG-0005 protected-files-v2";
        private const string RestoreMarkerRelativePath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.txt";
        private const string PipelineBackupRelativePath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.asset";
        private const string GraphicsSettingsBackupRelativePath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.GraphicsSettings.asset";
        private const string QualitySettingsBackupRelativePath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.QualitySettings.asset";

        private static RenderPipelineAsset originalPipeline;
        private static int originalAntiAliasing;
        private static bool hasOriginalQualityState;
        private static bool ownsOverride;

        static GrayboxRenderPipelineBuildScope()
        {
            RecoverAbandonedBuild();
            EditorApplication.quitting -= RestoreAfterBuild;
            EditorApplication.quitting += RestoreAfterBuild;
        }

        public override int callbackOrder => -1000;

        public override void PrepareForBuild(
            BuildPlayerContext buildPlayerContext)
        {
            if (buildPlayerContext == null)
                throw new ArgumentNullException(nameof(buildPlayerContext));
            string[] scenes = buildPlayerContext.BuildPlayerOptions.scenes;
            if (scenes == null || scenes.Length == 0)
                scenes = EnabledBuildScenes();
            BeginForScenes(scenes);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RestoreAfterBuild();
        }

        internal static bool RequiresGrayboxPipeline(string[] scenes)
        {
            if (scenes == null) return false;
            for (var index = 0; index < scenes.Length; index++)
                if (string.Equals(
                        scenes[index],
                        GrayboxScenePath,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        internal static bool BeginForScenes(string[] scenes)
        {
            if (!RequiresGrayboxPipeline(scenes)) return false;
            if (ownsOverride) return true;

            RecoverAbandonedBuild();
            UniversalRenderPipelineAsset approved =
                AssetDatabase.LoadAssetAtPath<
                    UniversalRenderPipelineAsset>(GrayboxPipelinePath);
            if (approved == null)
                throw new BuildFailedException(
                    "BUG-0005: missing approved Graybox URP asset at " +
                    GrayboxPipelinePath);

            RenderPipelineAsset current =
                GraphicsSettings.defaultRenderPipeline;
            originalPipeline = current;
            originalAntiAliasing = QualitySettings.antiAliasing;
            hasOriginalQualityState = true;
            try
            {
                BackupProtectedFiles();
                WriteRestoreMarker(current);
                ownsOverride = true;
                if (current != approved)
                    GraphicsSettings.defaultRenderPipeline = approved;
            }
            catch
            {
                RestoreAfterBuild();
                throw;
            }
            Debug.Log(
                "[BUG-0005] Registered GrayboxURP for player-build " +
                "shader variant collection.");
            return true;
        }

        internal static void RestoreAfterBuild()
        {
            string markerPath = RestoreMarkerPath();
            if (!ownsOverride &&
                !File.Exists(markerPath) &&
                !HasProtectedFileBackups())
                return;

            var restoreCompleted = false;
            try
            {
                RenderPipelineAsset restore = originalPipeline;
                if (restore == null && File.Exists(markerPath))
                    restore = ReadRestoreMarker(markerPath);
                if (GraphicsSettings.defaultRenderPipeline != restore)
                    GraphicsSettings.defaultRenderPipeline = restore;
                int restoreAntiAliasing = hasOriginalQualityState
                    ? originalAntiAliasing
                    : ReadRestoreAntiAliasing(markerPath);
                if (restoreAntiAliasing >= 0 &&
                    QualitySettings.antiAliasing != restoreAntiAliasing)
                    QualitySettings.antiAliasing = restoreAntiAliasing;
                RestoreProtectedFiles();
                restoreCompleted = !HasProtectedFileBackups();
            }
            finally
            {
                ownsOverride = false;
                originalPipeline = null;
                originalAntiAliasing = 0;
                hasOriginalQualityState = false;
                if (restoreCompleted && File.Exists(markerPath))
                    File.Delete(markerPath);
            }
            Debug.Log(
                "[BUG-0005] Restored the pre-build render pipeline.");
        }

        internal static void RecoverAbandonedBuild()
        {
            string markerPath = RestoreMarkerPath();
            if (!File.Exists(markerPath) && !HasProtectedFileBackups())
                return;
            RestoreAfterBuild();
        }

        private static string[] EnabledBuildScenes()
        {
            EditorBuildSettingsScene[] configured =
                EditorBuildSettings.scenes;
            var enabled = new System.Collections.Generic.List<string>();
            for (var index = 0; index < configured.Length; index++)
                if (configured[index].enabled)
                    enabled.Add(configured[index].path);
            return enabled.ToArray();
        }

        private static void WriteRestoreMarker(
            RenderPipelineAsset pipeline)
        {
            string path = RestoreMarkerPath();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            string assetPath = pipeline == null
                ? NullPipelineMarker
                : AssetDatabase.GetAssetPath(pipeline);
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new BuildFailedException(
                    "BUG-0005: the previous render pipeline is not a persistent asset.");
            File.WriteAllLines(path, new[]
            {
                RestoreMarkerVersion,
                "pipeline=" + assetPath,
                "antiAliasing=" + originalAntiAliasing,
                "backup=" + PipelineBackupRelativePath,
                "backup=" + GraphicsSettingsBackupRelativePath,
                "backup=" + QualitySettingsBackupRelativePath
            });
        }

        private static void BackupProtectedFiles()
        {
            BackupFile(GrayboxPipelinePath, PipelineBackupRelativePath);
            BackupFile(
                GraphicsSettingsPath,
                GraphicsSettingsBackupRelativePath);
            BackupFile(
                QualitySettingsPath,
                QualitySettingsBackupRelativePath);
        }

        private static void BackupFile(
            string projectRelativePath,
            string backupRelativePath)
        {
            string source = ProjectPath(projectRelativePath);
            if (!File.Exists(source))
                throw new BuildFailedException(
                    "BUG-0005: cannot back up protected file " +
                    projectRelativePath);
            string backup = ProjectPath(backupRelativePath);
            string directory = Path.GetDirectoryName(backup);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.Copy(source, backup, true);
        }

        private static void RestoreProtectedFiles()
        {
            RestoreFile(
                GrayboxPipelinePath,
                PipelineBackupRelativePath,
                true);
            RestoreFile(
                GraphicsSettingsPath,
                GraphicsSettingsBackupRelativePath,
                false);
            RestoreFile(
                QualitySettingsPath,
                QualitySettingsBackupRelativePath,
                false);
        }

        private static void RestoreFile(
            string projectRelativePath,
            string backupRelativePath,
            bool importAsset)
        {
            string backup = ProjectPath(backupRelativePath);
            if (!File.Exists(backup)) return;
            string target = ProjectPath(projectRelativePath);
            File.Copy(backup, target, true);
            if (importAsset)
                AssetDatabase.ImportAsset(
                    projectRelativePath,
                    ImportAssetOptions.ForceUpdate);
            File.Delete(backup);
        }

        private static bool HasProtectedFileBackups()
        {
            return File.Exists(ProjectPath(PipelineBackupRelativePath)) ||
                   File.Exists(ProjectPath(
                       GraphicsSettingsBackupRelativePath)) ||
                   File.Exists(ProjectPath(
                       QualitySettingsBackupRelativePath));
        }

        private static RenderPipelineAsset ReadRestoreMarker(string path)
        {
            string[] lines = File.ReadAllLines(path);
            string assetPath = lines.Length >= 2 &&
                               lines[0] == RestoreMarkerVersion &&
                               lines[1].StartsWith(
                                   "pipeline=",
                                   StringComparison.Ordinal)
                ? lines[1].Substring("pipeline=".Length).Trim()
                : File.ReadAllText(path).Trim();
            if (assetPath == NullPipelineMarker) return null;
            RenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                    assetPath);
            if (pipeline == null)
                throw new BuildFailedException(
                    "BUG-0005: cannot restore render pipeline at " +
                    assetPath);
            return pipeline;
        }

        private static int ReadRestoreAntiAliasing(string markerPath)
        {
            if (!File.Exists(markerPath)) return -1;
            string[] lines = File.ReadAllLines(markerPath);
            const string prefix = "antiAliasing=";
            for (var index = 0; index < lines.Length; index++)
                if (lines[index].StartsWith(
                        prefix,
                        StringComparison.Ordinal) &&
                    int.TryParse(
                        lines[index].Substring(prefix.Length),
                        out int antiAliasing))
                    return antiAliasing;
            return -1;
        }

        private static string RestoreMarkerPath()
        {
            return ProjectPath(RestoreMarkerRelativePath);
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }
    }
}
