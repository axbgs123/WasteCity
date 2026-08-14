using System;
using System.Collections.Generic;
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
                GrayboxRenderPipelineBuildScope.
                    BeginCommandLineFinalExitRestore();
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
                GrayboxRenderPipelineBuildScope.
                    BeginCommandLineFinalExitRestore();
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
                GrayboxRenderPipelineBuildScope.
                    BeginCommandLineFinalExitRestore();
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
                GrayboxRenderPipelineBuildScope.
                    BeginCommandLineFinalExitRestore();
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

        public static void BuildMacOSGraybox3D()
        {
            Directory.CreateDirectory("Builds/macOS");
            BuildReport report;
            try
            {
                GrayboxRenderPipelineBuildScope.
                    BeginCommandLineFinalExitRestore();
                GrayboxRenderPipelineBuildScope.BeginUniversalMacBuild();
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/GrayboxPrototype3D.unity" },
                    locationPathName = "Builds/macOS/WasteCity.app",
                    target = BuildTarget.StandaloneOSX
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
        private const string PlayerSettingsPath =
            "ProjectSettings/ProjectSettings.asset";
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
        private const string PlayerSettingsBackupRelativePath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.ProjectSettings.asset";
        private const string MacArchitectureRestoreMarkerVersion =
            "BUG-0005 mac-architecture-v2";
        private const string MacArchitectureRestoreMarkerRelativePath =
            "Library/WasteCity.GrayboxBuildMacArchitectureRestore.txt";
        private const string FinalExitRestoreMarkerVersion =
            "BUG-0005 formal-command-final-exit-v1";
        private const string FinalExitRestoreMarkerRelativePath =
            "Library/WasteCity.GrayboxBuildFinalExitRestore.txt";
        private const string FinalExitPipelineBackupRelativePath =
            "Library/WasteCity.GrayboxBuildFinalExitRestore.asset";
        private const string FinalExitGraphicsSettingsBackupRelativePath =
            "Library/WasteCity.GrayboxBuildFinalExitRestore.GraphicsSettings.asset";
        private const string FinalExitQualitySettingsBackupRelativePath =
            "Library/WasteCity.GrayboxBuildFinalExitRestore.QualitySettings.asset";
        private const string FinalExitPlayerSettingsBackupRelativePath =
            "Library/WasteCity.GrayboxBuildFinalExitRestore.ProjectSettings.asset";

        private static RenderPipelineAsset originalPipeline;
        private static int originalAntiAliasing;
        private static bool hasOriginalQualityState;
        private static bool ownsOverride;
        private static int originalMacArchitecture;
        private static bool hasOriginalMacArchitectureState;
        private static bool originalMacArchitectureWasExplicit;
        private static bool ownsMacArchitectureOverride;
        private static bool deferPostprocessRestoreForUniversalMacBuild;
        private static string finalExitRestoreProjectRoot;
        private static bool finalExitRestoreArmed;

        static GrayboxRenderPipelineBuildScope()
        {
            RecoverFinalExitRestore();
            RecoverAbandonedBuild();
            EditorApplication.quitting -= RestoreAfterBuild;
            EditorApplication.quitting += RestoreAfterBuild;
            EditorApplication.quitting -=
                RestoreFinalExitOnEditorQuitting;
            EditorApplication.quitting +=
                RestoreFinalExitOnEditorQuitting;
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
            RestoreAfterPostprocessBuild();
        }

        internal static void RestoreAfterPostprocessBuild()
        {
            if (deferPostprocessRestoreForUniversalMacBuild)
                return;
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
            originalAntiAliasing = -1;
            hasOriginalQualityState = false;
            try
            {
                BackupProtectedFiles();
                originalAntiAliasing =
                    ReadCurrentQualityAntiAliasingFromBackup();
                hasOriginalQualityState = true;
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

        internal static void BeginUniversalMacBuild()
        {
            BeginForScenes(new[] { GrayboxScenePath });
            if (ownsMacArchitectureOverride)
            {
                deferPostprocessRestoreForUniversalMacBuild = true;
                return;
            }

            originalMacArchitecture = PlayerSettings.GetArchitecture(
                BuildTargetGroup.Standalone);
            hasOriginalMacArchitectureState = true;
            try
            {
                originalMacArchitectureWasExplicit =
                    HasExplicitStandaloneArchitecture();
                WriteMacArchitectureRestoreMarker(
                    originalMacArchitecture,
                    originalMacArchitectureWasExplicit);
                ownsMacArchitectureOverride = true;
                int universalArchitecture =
                    (int)OSArchitecture.x64ARM64;
                if (originalMacArchitecture != universalArchitecture)
                    PlayerSettings.SetArchitecture(
                        BuildTargetGroup.Standalone,
                        universalArchitecture);
                deferPostprocessRestoreForUniversalMacBuild = true;
            }
            catch
            {
                RestoreAfterBuild();
                throw;
            }
        }

        internal static bool IsCommandLineQuitFormalBuildForTests(
            string[] arguments)
        {
            if (arguments == null) return false;
            var quit = false;
            string executeMethod = null;
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        "-quit",
                        StringComparison.OrdinalIgnoreCase))
                {
                    quit = true;
                    continue;
                }
                if (!string.Equals(
                        arguments[index],
                        "-executeMethod",
                        StringComparison.OrdinalIgnoreCase) ||
                    index + 1 >= arguments.Length)
                    continue;
                executeMethod = arguments[++index];
            }

            return quit && IsFormalBuildMethod(executeMethod);
        }

        internal static void BeginCommandLineFinalExitRestore()
        {
            if (!IsCommandLineQuitFormalBuildForTests(
                    Environment.GetCommandLineArgs()) ||
                finalExitRestoreArmed)
                return;

            string projectRoot = ProjectRoot();
            RecoverFinalExitRestoreAtRoot(projectRoot);
            string markerPath = ProjectPath(
                projectRoot,
                FinalExitRestoreMarkerRelativePath);
            string temporaryMarkerPath = markerPath + ".tmp";
            string[] backupPaths = FinalExitBackupPaths(projectRoot);
            try
            {
                RestoreExactProtectedBytesForTests(
                    FinalExitProtectedPaths(projectRoot),
                    backupPaths);
                int antiAliasing =
                    ParseCurrentQualityAntiAliasingForTests(
                        File.ReadAllText(backupPaths[2]));
                string directory = Path.GetDirectoryName(markerPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllLines(temporaryMarkerPath, new[]
                {
                    FinalExitRestoreMarkerVersion,
                    "antiAliasing=" + antiAliasing,
                    "backup=" + FinalExitPipelineBackupRelativePath,
                    "backup=" +
                    FinalExitGraphicsSettingsBackupRelativePath,
                    "backup=" + FinalExitQualitySettingsBackupRelativePath,
                    "backup=" + FinalExitPlayerSettingsBackupRelativePath
                });
                File.Move(temporaryMarkerPath, markerPath);
                finalExitRestoreProjectRoot = projectRoot;
                finalExitRestoreArmed = true;
            }
            catch
            {
                if (!File.Exists(markerPath))
                    DeleteFinalExitResidue(projectRoot);
                throw;
            }
        }

        private static bool IsFormalBuildMethod(string executeMethod)
        {
            if (string.IsNullOrWhiteSpace(executeMethod)) return false;
            switch (executeMethod)
            {
                case "WasteCity.Editor.FormalBuildTools.BuildWindows":
                case "WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D":
                case "WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3D":
                case "WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment":
                case "WasteCity.Editor.FormalBuildTools.BuildMacOSGraybox3D":
                    return true;
                default:
                    return false;
            }
        }

        private static void RestoreFinalExitOnEditorQuitting()
        {
            if (!finalExitRestoreArmed ||
                string.IsNullOrWhiteSpace(finalExitRestoreProjectRoot))
                return;
            try
            {
                int antiAliasing =
                    ReadFinalExitRestoreAntiAliasing(
                        finalExitRestoreProjectRoot);
                if (QualitySettings.antiAliasing != antiAliasing)
                    QualitySettings.antiAliasing = antiAliasing;
                AssetDatabase.SaveAssets();
                RestoreFinalExitProtectedFiles(
                    finalExitRestoreProjectRoot);
            }
            catch
            {
                // The committed marker and backups intentionally remain so
                // the next editor start can retry the exact-byte restore.
            }
        }

        internal static void RestoreExactProtectedBytesForTests(
            string[] backupPaths,
            string[] targetPaths)
        {
            if (backupPaths == null || targetPaths == null ||
                backupPaths.Length == 0 ||
                backupPaths.Length != targetPaths.Length)
                throw new ArgumentException(
                    "BUG-0005: exact protected-file restore paths do not match.");
            for (var index = 0; index < backupPaths.Length; index++)
                if (string.IsNullOrWhiteSpace(backupPaths[index]) ||
                    string.IsNullOrWhiteSpace(targetPaths[index]) ||
                    !File.Exists(backupPaths[index]))
                    throw new IOException(
                        "BUG-0005: an exact protected-file backup is missing.");
            for (var index = 0; index < backupPaths.Length; index++)
            {
                string directory = Path.GetDirectoryName(targetPaths[index]);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.Copy(
                    backupPaths[index],
                    targetPaths[index],
                    true);
            }
        }

        private static void RecoverFinalExitRestore()
        {
            RecoverFinalExitRestoreAtRoot(ProjectRoot());
        }

        private static void RecoverFinalExitRestoreAtRoot(
            string projectRoot)
        {
            string markerPath = ProjectPath(
                projectRoot,
                FinalExitRestoreMarkerRelativePath);
            if (!File.Exists(markerPath))
            {
                DeleteFinalExitResidue(projectRoot);
                return;
            }
            int antiAliasing =
                ReadFinalExitRestoreAntiAliasing(projectRoot);
            if (QualitySettings.antiAliasing != antiAliasing)
                QualitySettings.antiAliasing = antiAliasing;
            AssetDatabase.SaveAssets();
            RestoreFinalExitProtectedFiles(projectRoot);
        }

        private static int ReadFinalExitRestoreAntiAliasing(
            string projectRoot)
        {
            string markerPath = ProjectPath(
                projectRoot,
                FinalExitRestoreMarkerRelativePath);
            string[] lines = File.ReadAllLines(markerPath);
            string[] expectedBackups =
            {
                FinalExitPipelineBackupRelativePath,
                FinalExitGraphicsSettingsBackupRelativePath,
                FinalExitQualitySettingsBackupRelativePath,
                FinalExitPlayerSettingsBackupRelativePath
            };
            if (lines.Length != expectedBackups.Length + 2 ||
                lines[0] != FinalExitRestoreMarkerVersion ||
                !lines[1].StartsWith(
                    "antiAliasing=",
                    StringComparison.Ordinal) ||
                !int.TryParse(
                    lines[1].Substring("antiAliasing=".Length),
                    out int markerAntiAliasing))
                throw new InvalidDataException(
                    "BUG-0005: final-exit restore marker is invalid.");
            ValidateAntiAliasing(markerAntiAliasing);
            for (var index = 0; index < expectedBackups.Length; index++)
                if (!string.Equals(
                        lines[index + 2],
                        "backup=" + expectedBackups[index],
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "BUG-0005: final-exit restore marker paths are invalid.");

            string[] backupPaths = FinalExitBackupPaths(projectRoot);
            for (var index = 0; index < backupPaths.Length; index++)
                if (!File.Exists(backupPaths[index]))
                    throw new IOException(
                        "BUG-0005: a committed final-exit backup is missing.");
            int backupAntiAliasing =
                ParseCurrentQualityAntiAliasingForTests(
                    File.ReadAllText(backupPaths[2]));
            if (backupAntiAliasing != markerAntiAliasing)
                throw new InvalidDataException(
                    "BUG-0005: final-exit QualitySettings backup does not match its marker.");
            return backupAntiAliasing;
        }

        private static void RestoreFinalExitProtectedFiles(
            string projectRoot)
        {
            ReadFinalExitRestoreAntiAliasing(projectRoot);
            string markerPath = ProjectPath(
                projectRoot,
                FinalExitRestoreMarkerRelativePath);
            string[] backupPaths = FinalExitBackupPaths(projectRoot);
            RestoreExactProtectedBytesForTests(
                backupPaths,
                FinalExitProtectedPaths(projectRoot));

            // Delete the commit marker before its now-unneeded backups. A
            // shutdown between these operations leaves only harmless residue.
            File.Delete(markerPath);
            DeleteFinalExitResidue(projectRoot);
            finalExitRestoreArmed = false;
            finalExitRestoreProjectRoot = null;
        }

        private static string[] FinalExitProtectedPaths(string projectRoot)
        {
            return new[]
            {
                ProjectPath(projectRoot, GrayboxPipelinePath),
                ProjectPath(projectRoot, GraphicsSettingsPath),
                ProjectPath(projectRoot, QualitySettingsPath),
                ProjectPath(projectRoot, PlayerSettingsPath)
            };
        }

        private static string[] FinalExitBackupPaths(string projectRoot)
        {
            return new[]
            {
                ProjectPath(
                    projectRoot,
                    FinalExitPipelineBackupRelativePath),
                ProjectPath(
                    projectRoot,
                    FinalExitGraphicsSettingsBackupRelativePath),
                ProjectPath(
                    projectRoot,
                    FinalExitQualitySettingsBackupRelativePath),
                ProjectPath(
                    projectRoot,
                    FinalExitPlayerSettingsBackupRelativePath)
            };
        }

        private static void DeleteFinalExitResidue(string projectRoot)
        {
            string[] backupPaths = FinalExitBackupPaths(projectRoot);
            for (var index = 0; index < backupPaths.Length; index++)
                if (File.Exists(backupPaths[index]))
                    File.Delete(backupPaths[index]);
            string temporaryMarkerPath = ProjectPath(
                projectRoot,
                FinalExitRestoreMarkerRelativePath) + ".tmp";
            if (File.Exists(temporaryMarkerPath))
                File.Delete(temporaryMarkerPath);
        }

        internal static void RestoreAfterBuild()
        {
            string markerPath = RestoreMarkerPath();
            string macArchitectureMarkerPath =
                MacArchitectureRestoreMarkerPath();
            bool restorePipeline =
                ownsOverride ||
                File.Exists(markerPath) ||
                HasProtectedFileBackups();
            bool restoreMacArchitecture =
                ownsMacArchitectureOverride ||
                File.Exists(macArchitectureMarkerPath);
            if (!restorePipeline && !restoreMacArchitecture)
            {
                deferPostprocessRestoreForUniversalMacBuild = false;
                return;
            }

            bool pipelineRestoreCompleted = !restorePipeline;
            bool macArchitectureRestoreCompleted =
                !restoreMacArchitecture;
            try
            {
                if (restoreMacArchitecture)
                {
                    int architecture =
                        hasOriginalMacArchitectureState
                            ? originalMacArchitecture
                            : ReadMacArchitectureRestoreMarker(
                                macArchitectureMarkerPath);
                    bool architectureWasExplicit =
                        hasOriginalMacArchitectureState
                            ? originalMacArchitectureWasExplicit
                            : ReadMacArchitectureWasExplicit(
                                macArchitectureMarkerPath);
                    if (PlayerSettings.GetArchitecture(
                            BuildTargetGroup.Standalone) != architecture)
                        PlayerSettings.SetArchitecture(
                            BuildTargetGroup.Standalone,
                            architecture);
                    RestoreStandaloneArchitectureShape(
                        architectureWasExplicit);
                    macArchitectureRestoreCompleted = true;
                }

                if (restorePipeline)
                {
                    RenderPipelineAsset restore = originalPipeline;
                    if (restore == null && File.Exists(markerPath))
                        restore = ReadRestoreMarker(markerPath);
                    if (GraphicsSettings.defaultRenderPipeline != restore)
                        GraphicsSettings.defaultRenderPipeline = restore;
                    int restoreAntiAliasing =
                        ReadRestoreAntiAliasing(markerPath);
                    if (QualitySettings.antiAliasing !=
                        restoreAntiAliasing)
                        QualitySettings.antiAliasing =
                            restoreAntiAliasing;
                }

                // A player build can leave ProjectSettings singletons dirty
                // even when their public values already look restored. Flush
                // that memory state first so editor shutdown cannot serialize
                // it over the exact pre-build files restored below.
                AssetDatabase.SaveAssets();
                if (restorePipeline)
                {
                    RestoreProtectedFiles();
                    pipelineRestoreCompleted =
                        !HasProtectedFileBackups();
                }
            }
            finally
            {
                deferPostprocessRestoreForUniversalMacBuild = false;
                if (restoreMacArchitecture)
                {
                    ownsMacArchitectureOverride = false;
                    originalMacArchitecture = 0;
                    hasOriginalMacArchitectureState = false;
                    originalMacArchitectureWasExplicit = false;
                    if (macArchitectureRestoreCompleted &&
                        File.Exists(macArchitectureMarkerPath))
                        File.Delete(macArchitectureMarkerPath);
                }

                if (restorePipeline)
                {
                    ownsOverride = false;
                    originalPipeline = null;
                    originalAntiAliasing = 0;
                    hasOriginalQualityState = false;
                    if (pipelineRestoreCompleted &&
                        File.Exists(markerPath))
                        File.Delete(markerPath);
                }
            }
            if (restorePipeline)
                Debug.Log(
                    "[BUG-0005] Restored the pre-build render pipeline.");
            if (restoreMacArchitecture)
                Debug.Log(
                    "[BUG-0005] Restored the pre-build macOS architecture.");
        }

        internal static void RecoverAbandonedBuild()
        {
            string markerPath = RestoreMarkerPath();
            string macArchitectureMarkerPath =
                MacArchitectureRestoreMarkerPath();
            if (!File.Exists(markerPath) &&
                !HasProtectedFileBackups() &&
                !File.Exists(macArchitectureMarkerPath))
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
                "backup=" + QualitySettingsBackupRelativePath,
                "backup=" + PlayerSettingsBackupRelativePath
            });
        }

        private static void BackupProtectedFiles()
        {
            BackupFile(
                QualitySettingsPath,
                QualitySettingsBackupRelativePath);
            BackupFile(GrayboxPipelinePath, PipelineBackupRelativePath);
            BackupFile(
                GraphicsSettingsPath,
                GraphicsSettingsBackupRelativePath);
            BackupFile(
                PlayerSettingsPath,
                PlayerSettingsBackupRelativePath);
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
            RestoreFile(
                PlayerSettingsPath,
                PlayerSettingsBackupRelativePath,
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
                       QualitySettingsBackupRelativePath)) ||
                   File.Exists(ProjectPath(
                       PlayerSettingsBackupRelativePath));
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
            string qualityBackup = ProjectPath(
                QualitySettingsBackupRelativePath);
            if (File.Exists(qualityBackup))
                return ReadCurrentQualityAntiAliasingFromBackup();
            if (hasOriginalQualityState && originalAntiAliasing >= 0)
                return originalAntiAliasing;
            if (!File.Exists(markerPath))
                throw new BuildFailedException(
                    "BUG-0005: cannot restore current-quality anti-aliasing without its backup or marker.");
            string[] lines = File.ReadAllLines(markerPath);
            const string prefix = "antiAliasing=";
            for (var index = 0; index < lines.Length; index++)
                if (lines[index].StartsWith(
                        prefix,
                        StringComparison.Ordinal) &&
                    int.TryParse(
                        lines[index].Substring(prefix.Length),
                        out int antiAliasing))
                    return ValidateAntiAliasing(antiAliasing);
            throw new BuildFailedException(
                "BUG-0005: restore marker has no valid current-quality anti-aliasing value.");
        }

        private static int ReadCurrentQualityAntiAliasingFromBackup()
        {
            string path = ProjectPath(QualitySettingsBackupRelativePath);
            if (!File.Exists(path))
                throw new BuildFailedException(
                    "BUG-0005: the protected QualitySettings backup is missing.");
            return ParseCurrentQualityAntiAliasingForTests(
                File.ReadAllText(path));
        }

        internal static int ParseCurrentQualityAntiAliasingForTests(
            string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                throw InvalidQualityBackup("is empty");
            string[] lines = yaml.Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
            int currentQuality = -1;
            int qualityArrayLine = -1;
            for (var index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (line.IndexOf('\t') >= 0)
                    throw InvalidQualityBackup("contains tab indentation");
                int indentation = LeadingSpaces(line);
                string content = line.Substring(indentation);
                if (content.StartsWith(
                        "m_CurrentQuality:",
                        StringComparison.Ordinal))
                {
                    if (indentation != 2 || currentQuality >= 0)
                        throw InvalidQualityBackup(
                            "has an invalid or duplicate m_CurrentQuality");
                    currentQuality = ParseYamlInteger(
                        content,
                        "m_CurrentQuality:");
                    if (currentQuality < 0)
                        throw InvalidQualityBackup(
                            "has a negative m_CurrentQuality");
                }
                else if (string.Equals(
                             content,
                             "m_QualitySettings:",
                             StringComparison.Ordinal))
                {
                    if (indentation != 2 || qualityArrayLine >= 0)
                        throw InvalidQualityBackup(
                            "has an invalid or duplicate m_QualitySettings array");
                    qualityArrayLine = index;
                }
            }

            if (currentQuality < 0 || qualityArrayLine < 0)
                throw InvalidQualityBackup(
                    "does not define current quality and its settings array");

            var antiAliasingByQuality = new List<int?>();
            for (var index = qualityArrayLine + 1;
                 index < lines.Length;
                 index++)
            {
                string line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                int indentation = LeadingSpaces(line);
                string content = line.Substring(indentation);
                if (indentation == 2 &&
                    (content == "-" || content.StartsWith(
                        "- ",
                        StringComparison.Ordinal)))
                {
                    antiAliasingByQuality.Add(null);
                    continue;
                }
                if (indentation <= 2)
                    break;
                if (indentation == 4 &&
                    content.StartsWith(
                        "antiAliasing:",
                        StringComparison.Ordinal))
                {
                    if (antiAliasingByQuality.Count == 0)
                        throw InvalidQualityBackup(
                            "defines antiAliasing before its first quality entry");
                    int entry = antiAliasingByQuality.Count - 1;
                    if (antiAliasingByQuality[entry].HasValue)
                        throw InvalidQualityBackup(
                            "duplicates antiAliasing in a quality entry");
                    antiAliasingByQuality[entry] = ValidateAntiAliasing(
                        ParseYamlInteger(content, "antiAliasing:"));
                }
            }

            if (currentQuality >= antiAliasingByQuality.Count)
                throw InvalidQualityBackup(
                    "has m_CurrentQuality outside m_QualitySettings");
            for (var index = 0;
                 index < antiAliasingByQuality.Count;
                 index++)
                if (!antiAliasingByQuality[index].HasValue)
                    throw InvalidQualityBackup(
                        "has a quality entry without antiAliasing");
            return antiAliasingByQuality[currentQuality].Value;
        }

        private static int ParseYamlInteger(
            string content,
            string prefix)
        {
            string value = content.Substring(prefix.Length).Trim();
            for (var index = 0; index < value.Length; index++)
                if (value[index] < '0' || value[index] > '9')
                    throw InvalidQualityBackup(
                        "has a non-integer " + prefix.TrimEnd(':'));
            if (!int.TryParse(value, out int parsed))
                throw InvalidQualityBackup(
                    "has a non-integer " + prefix.TrimEnd(':'));
            return parsed;
        }

        private static int ValidateAntiAliasing(int antiAliasing)
        {
            if (antiAliasing != 0 &&
                antiAliasing != 2 &&
                antiAliasing != 4 &&
                antiAliasing != 8)
                throw InvalidQualityBackup(
                    "has unsupported antiAliasing " + antiAliasing);
            return antiAliasing;
        }

        private static int LeadingSpaces(string line)
        {
            var count = 0;
            while (count < line.Length && line[count] == ' ')
                count++;
            return count;
        }

        private static BuildFailedException InvalidQualityBackup(
            string reason)
        {
            return new BuildFailedException(
                "BUG-0005: protected QualitySettings YAML " +
                reason + ".");
        }

        private static void WriteMacArchitectureRestoreMarker(
            int architecture,
            bool architectureWasExplicit)
        {
            string path = MacArchitectureRestoreMarkerPath();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllLines(path, new[]
            {
                MacArchitectureRestoreMarkerVersion,
                "architecture=" + architecture,
                "explicit=" + architectureWasExplicit
            });
        }

        private static int ReadMacArchitectureRestoreMarker(
            string markerPath)
        {
            string[] lines = File.ReadAllLines(markerPath);
            const string prefix = "architecture=";
            if (lines.Length == 3 &&
                lines[0] == MacArchitectureRestoreMarkerVersion &&
                lines[1].StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(
                    lines[1].Substring(prefix.Length),
                    out int architecture))
                return architecture;
            throw new BuildFailedException(
                "BUG-0005: cannot restore the pre-build macOS architecture.");
        }

        private static bool ReadMacArchitectureWasExplicit(
            string markerPath)
        {
            string[] lines = File.ReadAllLines(markerPath);
            const string prefix = "explicit=";
            if (lines.Length == 3 &&
                lines[0] == MacArchitectureRestoreMarkerVersion &&
                lines[2].StartsWith(prefix, StringComparison.Ordinal) &&
                bool.TryParse(
                    lines[2].Substring(prefix.Length),
                    out bool architectureWasExplicit))
                return architectureWasExplicit;
            throw new BuildFailedException(
                "BUG-0005: cannot restore the pre-build macOS architecture shape.");
        }

        private static bool HasExplicitStandaloneArchitecture()
        {
            SerializedObject serialized = PlayerSettingsSerializedObject();
            SerializedProperty architectures = serialized.FindProperty(
                "platformArchitecture");
            if (architectures == null || !architectures.isArray)
                throw new BuildFailedException(
                    "BUG-0005: cannot inspect PlayerSettings platform architectures.");
            for (var index = 0;
                 index < architectures.arraySize;
                 index++)
            {
                SerializedProperty key = architectures
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("first");
                if (key != null &&
                    string.Equals(
                        key.stringValue,
                        "Standalone",
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void RestoreStandaloneArchitectureShape(
            bool architectureWasExplicit)
        {
            SerializedObject serialized = PlayerSettingsSerializedObject();
            SerializedProperty architectures = serialized.FindProperty(
                "platformArchitecture");
            if (architectures == null || !architectures.isArray)
                throw new BuildFailedException(
                    "BUG-0005: cannot restore PlayerSettings platform architectures.");
            if (!architectureWasExplicit)
            {
                for (var index = architectures.arraySize - 1;
                     index >= 0;
                     index--)
                {
                    SerializedProperty key = architectures
                        .GetArrayElementAtIndex(index)
                        .FindPropertyRelative("first");
                    if (key != null &&
                        string.Equals(
                            key.stringValue,
                            "Standalone",
                            StringComparison.Ordinal))
                        architectures.DeleteArrayElementAtIndex(index);
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(serialized.targetObject);
        }

        private static SerializedObject PlayerSettingsSerializedObject()
        {
            UnityEngine.Object singleton =
                Unsupported.GetSerializedAssetInterfaceSingleton(
                    "PlayerSettings");
            if (singleton == null)
                throw new BuildFailedException(
                    "BUG-0005: cannot access the PlayerSettings singleton.");
            return new SerializedObject(singleton);
        }

        private static string RestoreMarkerPath()
        {
            return ProjectPath(RestoreMarkerRelativePath);
        }

        private static string MacArchitectureRestoreMarkerPath()
        {
            return ProjectPath(
                MacArchitectureRestoreMarkerRelativePath);
        }

        private static string ProjectPath(string relativePath)
        {
            return ProjectPath(ProjectRoot(), relativePath);
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
        }

        private static string ProjectPath(
            string projectRoot,
            string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                relativePath));
        }
    }
}
