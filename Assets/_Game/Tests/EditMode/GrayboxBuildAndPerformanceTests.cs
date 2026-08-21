using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildAndPerformanceTests
    {
        private const string FormalBuildToolsTypeName =
            "WasteCity.Editor.FormalBuildTools";
        private const string RenderPipelineBuildScopeTypeName =
            "WasteCity.Editor.GrayboxRenderPipelineBuildScope";
        private const string GrayboxScenePath =
            "Assets/_Game/Scenes/GrayboxPrototype3D.unity";
        private const string RetiredFormalScenePath =
            "Assets/_Game/Scenes/FormalPrototype.unity";
        private const string GrayboxPipelinePath =
            "Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset";
        private const string GraphicsSettingsPath =
            "ProjectSettings/GraphicsSettings.asset";
        private const string QualitySettingsPath =
            "ProjectSettings/QualitySettings.asset";
        private const string PlayerSettingsPath =
            "ProjectSettings/ProjectSettings.asset";
        private const string RestoreMarkerPath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.txt";
        private const string GrayboxPipelineBackupPath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.asset";
        private const string GraphicsSettingsBackupPath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.GraphicsSettings.asset";
        private const string QualitySettingsBackupPath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.QualitySettings.asset";
        private const string PlayerSettingsBackupPath =
            "Library/WasteCity.GrayboxBuildPipelineRestore.ProjectSettings.asset";
        private const string MacArchitectureRestoreMarkerPath =
            "Library/WasteCity.GrayboxBuildMacArchitectureRestore.txt";
        private const string PerformanceProbeTypeName =
            "WasteCity.Editor.GrayboxPerformanceProbe";
        private const string FormalSavePerformanceResultEnvironmentVariable =
            "WASTECITY_FORMAL_SAVE_PERF_RESULT";
        private const int FormalSaveCaptureSampleCount = 20;
        private const int FormalSaveIdleCallbackCount = 300;
        private const long FormalSaveSnapshotAllocationBudgetBytes =
            1024L * 1024L;
        private const long FormalSaveFileTransactionAllocationBudgetBytes =
            4L * 1024L * 1024L;
        private const int BuildingInstanceCount = 128;

        private static readonly string[] FormalSavePerformanceMarkerNames =
        {
            "WasteCity.Formal.Save.Capture",
            "WasteCity.Formal.Save.Validate",
            "WasteCity.Formal.Save.WriteTransaction",
            "WasteCity.Formal.Save.Apply",
            "WasteCity.Formal.Save.Rebuild"
        };

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void BuildTools_ExposeOnlyFormal3DTargets()
        {
            Type buildTools = FindLoadedType(FormalBuildToolsTypeName);
            Assert.That(buildTools, Is.Not.Null);
            Assert.That(
                buildTools.GetMethod(
                    "BuildWindows",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                buildTools.GetMethod(
                    "BuildWindowsGraybox3D",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                buildTools.GetMethod(
                    "BuildWindowsLegacy2D",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Null);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FormalBuildTools.cs"));
            string default3D = ExtractMethodBlock(source, "BuildWindows");
            string explicitGraybox3D =
                ExtractMethodBlock(source, "BuildWindowsGraybox3D");
            StringAssert.Contains(
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                default3D);
            StringAssert.Contains(
                "Builds/Windows/WasteCity.exe",
                default3D);
            StringAssert.DoesNotContain("FormalPrototype", default3D);
            StringAssert.Contains(
                "BuildTarget.StandaloneWindows64",
                default3D);

            StringAssert.Contains(
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                explicitGraybox3D);
            StringAssert.Contains(
                "Builds/Windows3D/WasteCityGraybox.exe",
                explicitGraybox3D);
            StringAssert.DoesNotContain(
                "FormalPrototype",
                explicitGraybox3D);
            StringAssert.Contains(
                "BuildTarget.StandaloneWindows64",
                explicitGraybox3D);

            StringAssert.DoesNotContain(
                "BuildOptions.Development",
                default3D);
            StringAssert.DoesNotContain(
                "BuildOptions.Development",
                explicitGraybox3D);
            StringAssert.DoesNotContain(
                "BuildWindowsLegacy2D",
                source);
            StringAssert.DoesNotContain(
                "Assets/_Game/Scenes/FormalPrototype.unity",
                source);
            StringAssert.DoesNotContain(
                "Builds/Windows2D",
                source);
        }

        [Test]
        public void BuildTools_ExposeIsolatedGraybox3DDevelopmentTarget()
        {
            Type buildTools = FindLoadedType(FormalBuildToolsTypeName);
            Assert.That(buildTools, Is.Not.Null);
            MethodInfo method = buildTools?.GetMethod(
                "BuildWindowsGraybox3DDevelopment",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FormalBuildTools.cs"));
            string development = ExtractMethodBlock(
                source,
                "BuildWindowsGraybox3DDevelopment");
            StringAssert.Contains(
                "Assets/_Game/Scenes/GrayboxPrototype3D.unity",
                development);
            StringAssert.DoesNotContain("FormalPrototype", development);
            StringAssert.Contains(
                "Builds/Windows3DDevelopment/WasteCityGrayboxDev.exe",
                development);
            StringAssert.Contains(
                "BuildTarget.StandaloneWindows64",
                development);
            StringAssert.Contains(
                "BuildOptions.Development",
                development);
        }

        [Test]
        public void Bug0005_BuildTools_ExposeRestorableUniversalMacOSGrayboxTarget()
        {
            Type buildTools = FindLoadedType(FormalBuildToolsTypeName);
            Assert.That(buildTools, Is.Not.Null);
            MethodInfo method = buildTools?.GetMethod(
                "BuildMacOSGraybox3D",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FormalBuildTools.cs"));
            string macOS = ExtractMethodBlock(
                source,
                "BuildMacOSGraybox3D");
            StringAssert.Contains(GrayboxScenePath, macOS);
            StringAssert.DoesNotContain(RetiredFormalScenePath, macOS);
            StringAssert.Contains(
                "Builds/macOS/WasteCity.app",
                macOS);
            StringAssert.Contains(
                "BuildTarget.StandaloneOSX",
                macOS);
            StringAssert.DoesNotContain(
                "BuildOptions.Development",
                macOS);
            StringAssert.Contains(
                "GrayboxRenderPipelineBuildScope.BeginUniversalMacBuild()",
                macOS);
            StringAssert.Contains("finally", macOS);
            StringAssert.Contains(
                "GrayboxRenderPipelineBuildScope.RestoreAfterBuild()",
                macOS);

            string begin = ExtractMethodBlock(
                source,
                "BeginUniversalMacBuild");
            StringAssert.Contains("BeginForScenes", begin);
            StringAssert.Contains("OSArchitecture.x64ARM64", begin);
            StringAssert.Contains(
                "MacArchitectureRestoreMarkerPath",
                ExtractMethodBlock(source, "RestoreAfterBuild"));
            StringAssert.Contains(
                "MacArchitectureRestoreMarkerPath",
                ExtractMethodBlock(source, "RecoverAbandonedBuild"));
            StringAssert.Contains(
                "EditorApplication.quitting += RestoreAfterBuild",
                source);
            StringAssert.Contains(
                "public void OnPostprocessBuild(BuildReport report)\n" +
                "        {\n" +
                "            RestoreAfterPostprocessBuild();\n" +
                "        }",
                source);
            string postprocessRestore = ExtractMethodBlock(
                source,
                "RestoreAfterPostprocessBuild");
            StringAssert.Contains(
                "deferPostprocessRestoreForUniversalMacBuild",
                postprocessRestore);
            StringAssert.Contains("RestoreAfterBuild();", postprocessRestore);
        }

        [Test]
        public void Bug0005_RestoreFlushesSettingsMemoryBeforeExactProtectedBytes()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FormalBuildTools.cs"));
            string restore = ExtractMethodBlock(source, "RestoreAfterBuild");
            int saveAssets = restore.IndexOf(
                "AssetDatabase.SaveAssets();",
                StringComparison.Ordinal);
            int restoreProtectedFiles = restore.IndexOf(
                "RestoreProtectedFiles();",
                StringComparison.Ordinal);

            Assert.That(saveAssets, Is.GreaterThanOrEqualTo(0),
                "Restoration must flush and clean Unity's in-memory ProjectSettings state before exit.");
            Assert.That(restoreProtectedFiles, Is.GreaterThan(saveAssets),
                "Exact protected bytes must be the final disk authority after the in-memory settings flush.");
        }

        [Test]
        public void Bug0005_QualityBackupParserUsesCurrentEntryAndFailsClosed()
        {
            Type scope = FindLoadedType(RenderPipelineBuildScopeTypeName);
            MethodInfo parser = scope?.GetMethod(
                "ParseCurrentQualityAntiAliasingForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(parser, Is.Not.Null);

            const string valid =
                "QualitySettings:\n" +
                "  m_CurrentQuality: 1\n" +
                "  m_QualitySettings:\n" +
                "  - name: Low\n" +
                "    antiAliasing: 0\n" +
                "  - name: Current\n" +
                "    antiAliasing: 2\n" +
                "  - name: LastButNotCurrent\n" +
                "    antiAliasing: 8\n" +
                "  m_PerPlatformDefaultQuality:\n" +
                "    Standalone: 0\n" +
                "    iPhone: 2\n";
            Assert.That(
                parser.Invoke(null, new object[] { valid }),
                Is.EqualTo(2),
                "The current quality entry, not the last entry or a platform default, owns restored AA.");

            AssertQualityParserRejects(
                parser,
                valid.Replace("m_CurrentQuality: 1", "m_CurrentQuality: 3"));
            AssertQualityParserRejects(
                parser,
                valid.Replace("    antiAliasing: 2\n", string.Empty));
            AssertQualityParserRejects(
                parser,
                valid.Replace("    antiAliasing: 2", "    antiAliasing: 3"));
        }

        [Test]
        public void Bug0005_FinalExitRestoreActivatesOnlyForQuitFormalBuilds()
        {
            Type scope = FindLoadedType(RenderPipelineBuildScopeTypeName);
            MethodInfo activation = scope?.GetMethod(
                "IsCommandLineQuitFormalBuildForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(activation, Is.Not.Null);

            string[] formalMethods =
            {
                "WasteCity.Editor.FormalBuildTools.BuildWindows",
                "WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3D",
                "WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment",
                "WasteCity.Editor.FormalBuildTools.BuildMacOSGraybox3D"
            };
            foreach (string formalMethod in formalMethods)
                Assert.That(
                    activation.Invoke(null, new object[]
                    {
                        new[]
                        {
                            "Unity",
                            "-batchmode",
                            "-quit",
                            "-executeMethod",
                            formalMethod
                        }
                    }),
                    Is.EqualTo(true),
                    formalMethod);

            Assert.That(
                activation.Invoke(null, new object[]
                {
                    new[]
                    {
                        "Unity",
                        "-batchmode",
                        "-quit",
                        "-executeMethod",
                        "WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D"
                    }
                }),
                Is.EqualTo(false),
                "The retired legacy 2D entry must not arm formal-build restoration.");

            Assert.That(
                activation.Invoke(null, new object[]
                {
                    new[]
                    {
                        "Unity",
                        "-executeMethod",
                        formalMethods[3]
                    }
                }),
                Is.EqualTo(false),
                "A GUI/editor invocation without -quit must not retain final-exit state.");
            Assert.That(
                activation.Invoke(null, new object[]
                {
                    new[]
                    {
                        "Unity",
                        "-quit",
                        "-executeMethod",
                        "WasteCity.Editor.GrayboxPerformanceProbe.MeasureRuinsCliffPerformance"
                    }
                }),
                Is.EqualTo(false),
                "An unrelated command-line task must not arm formal-build exit restoration.");
        }

        [Test]
        public void Bug0005_FinalExitRestoreSynchronizesRuntimeBeforeExactBytes()
        {
            Type scope = FindLoadedType(RenderPipelineBuildScopeTypeName);
            MethodInfo exactRestore = scope?.GetMethod(
                "RestoreExactProtectedBytesForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(exactRestore, Is.Not.Null);

            string root = Path.Combine(
                Path.GetTempPath(),
                "WasteCity-BUG0005-FinalExit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string backupA = Path.Combine(root, "a.backup");
                string backupB = Path.Combine(root, "b.backup");
                string targetA = Path.Combine(root, "a.asset");
                string targetB = Path.Combine(root, "b.asset");
                byte[] expectedA = { 0, 1, 2, 13, 10, 255 };
                byte[] expectedB = { 9, 8, 7, 0, 6 };
                File.WriteAllBytes(backupA, expectedA);
                File.WriteAllBytes(backupB, expectedB);
                File.WriteAllBytes(targetA, new byte[] { 4 });
                File.WriteAllBytes(targetB, new byte[] { 5 });

                exactRestore.Invoke(null, new object[]
                {
                    new[] { backupA, backupB },
                    new[] { targetA, targetB }
                });
                CollectionAssert.AreEqual(expectedA, File.ReadAllBytes(targetA));
                CollectionAssert.AreEqual(expectedB, File.ReadAllBytes(targetB));

                byte[] unchangedA = { 4, 3, 2, 1 };
                File.WriteAllBytes(targetA, unchangedA);
                File.Delete(backupB);
                TargetInvocationException incompleteRestore =
                    Assert.Throws<TargetInvocationException>(() =>
                        exactRestore.Invoke(null, new object[]
                        {
                            new[] { backupA, backupB },
                            new[] { targetA, targetB }
                        }));
                Assert.That(
                    incompleteRestore?.InnerException,
                    Is.InstanceOf<IOException>());
                CollectionAssert.AreEqual(
                    unchangedA,
                    File.ReadAllBytes(targetA),
                    "All backups must validate before any protected target is replaced.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/FormalBuildTools.cs"));
            string finalExit = ExtractMethodBlock(
                source,
                "RestoreFinalExitOnEditorQuitting");
            int readBackup = finalExit.IndexOf(
                "ReadFinalExitRestoreAntiAliasing",
                StringComparison.Ordinal);
            int synchronizeRuntime = finalExit.IndexOf(
                "QualitySettings.antiAliasing = antiAliasing;",
                StringComparison.Ordinal);
            int saveAssets = finalExit.IndexOf(
                "AssetDatabase.SaveAssets();",
                StringComparison.Ordinal);
            int restoreExact = finalExit.IndexOf(
                "RestoreFinalExitProtectedFiles",
                StringComparison.Ordinal);
            Assert.That(readBackup, Is.GreaterThanOrEqualTo(0));
            Assert.That(synchronizeRuntime, Is.GreaterThan(readBackup));
            Assert.That(saveAssets, Is.GreaterThan(synchronizeRuntime));
            Assert.That(restoreExact, Is.GreaterThan(saveAssets));
            StringAssert.DoesNotContain("RestoreAfterBuild", finalExit);
            StringAssert.Contains(
                "EditorApplication.quitting +=\n" +
                "                RestoreFinalExitOnEditorQuitting",
                source);
            StringAssert.DoesNotContain("ProcessExit", source);

            int normalQuittingRestore = source.IndexOf(
                "EditorApplication.quitting += RestoreAfterBuild",
                StringComparison.Ordinal);
            int finalQuittingRestore = source.IndexOf(
                "EditorApplication.quitting +=\n" +
                "                RestoreFinalExitOnEditorQuitting",
                StringComparison.Ordinal);
            Assert.That(normalQuittingRestore, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                finalQuittingRestore,
                Is.GreaterThan(normalQuittingRestore),
                "The ordinary restoration handler must run before the final exact-byte handler.");

            foreach (string buildMethod in new[]
                     {
                         "BuildWindows",
                         "BuildWindowsGraybox3D",
                         "BuildWindowsGraybox3DDevelopment",
                         "BuildMacOSGraybox3D"
                     })
            {
                string block = ExtractMethodBlock(source, buildMethod);
                int arm = block.IndexOf(
                    "BeginCommandLineFinalExitRestore();",
                    StringComparison.Ordinal);
                int build = block.IndexOf(
                    "BuildPipeline.BuildPlayer",
                    StringComparison.Ordinal);
                Assert.That(arm, Is.GreaterThanOrEqualTo(0), buildMethod);
                Assert.That(build, Is.GreaterThan(arm), buildMethod);
            }
        }

        [Test]
        public void Bug0005_MacOSGrayboxArchitecture_RestoresAfterBuildAndRecovery()
        {
            Type scope = FindLoadedType(RenderPipelineBuildScopeTypeName);
            Assert.That(scope, Is.Not.Null);
            MethodInfo begin = scope?.GetMethod(
                "BeginUniversalMacBuild",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo restore = scope?.GetMethod(
                "RestoreAfterBuild",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo restoreAfterPostprocess = scope?.GetMethod(
                "RestoreAfterPostprocessBuild",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo recover = scope?.GetMethod(
                "RecoverAbandonedBuild",
                BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo ownsArchitecture = scope?.GetField(
                "ownsMacArchitectureOverride",
                BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo hasOriginalArchitecture = scope?.GetField(
                "hasOriginalMacArchitectureState",
                BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo originalArchitectureState = scope?.GetField(
                "originalMacArchitecture",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(begin, Is.Not.Null);
            Assert.That(restore, Is.Not.Null);
            Assert.That(restoreAfterPostprocess, Is.Not.Null);
            Assert.That(recover, Is.Not.Null);
            Assert.That(ownsArchitecture, Is.Not.Null);
            Assert.That(hasOriginalArchitecture, Is.Not.Null);
            Assert.That(originalArchitectureState, Is.Not.Null);

            int originalArchitecture = PlayerSettings.GetArchitecture(
                BuildTargetGroup.Standalone);
            string projectSettingsPath = ProjectPath(
                "ProjectSettings/ProjectSettings.asset");
            byte[] originalProjectSettings =
                File.ReadAllBytes(projectSettingsPath);
            try
            {
                begin.Invoke(null, null);
                Assert.That(
                    PlayerSettings.GetArchitecture(
                        BuildTargetGroup.Standalone),
                    Is.EqualTo((int)OSArchitecture.x64ARM64));
                Assert.That(
                    File.Exists(ProjectPath(
                        MacArchitectureRestoreMarkerPath)),
                    Is.True);
                Assert.That(
                    File.Exists(ProjectPath(
                        PlayerSettingsBackupPath)),
                    Is.True,
                    "The macOS architecture override must protect ProjectSettings.asset bytes.");

                restoreAfterPostprocess.Invoke(null, null);
                Assert.That(
                    PlayerSettings.GetArchitecture(
                        BuildTargetGroup.Standalone),
                    Is.EqualTo((int)OSArchitecture.x64ARM64),
                    "The inner postprocess callback must defer restoration until the explicit macOS build returns.");
                Assert.That(
                    File.Exists(ProjectPath(
                        MacArchitectureRestoreMarkerPath)),
                    Is.True,
                    "Deferred postprocess restoration must retain the macOS recovery marker.");
                Assert.That(
                    File.Exists(ProjectPath(
                        PlayerSettingsBackupPath)),
                    Is.True,
                    "Deferred postprocess restoration must retain exact protected-file backups.");

                restore.Invoke(null, null);
                Assert.That(
                    PlayerSettings.GetArchitecture(
                        BuildTargetGroup.Standalone),
                    Is.EqualTo(originalArchitecture));
                Assert.That(
                    File.Exists(ProjectPath(
                        MacArchitectureRestoreMarkerPath)),
                    Is.False);
                Assert.That(
                    File.Exists(ProjectPath(
                        PlayerSettingsBackupPath)),
                    Is.False);
                CollectionAssert.AreEqual(
                    originalProjectSettings,
                    File.ReadAllBytes(projectSettingsPath),
                    "The normal build restoration path must preserve ProjectSettings.asset exactly.");
                AssetDatabase.SaveAssets();
                CollectionAssert.AreEqual(
                    originalProjectSettings,
                    File.ReadAllBytes(projectSettingsPath),
                    "An editor-exit settings save must not overwrite the restored macOS ProjectSettings bytes.");

                begin.Invoke(null, null);
                restoreAfterPostprocess.Invoke(null, null);
                InvalidOperationException simulatedBuildFailure =
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        try
                        {
                            throw new InvalidOperationException(
                                "simulated macOS build failure");
                        }
                        finally
                        {
                            restore.Invoke(null, null);
                        }
                    });
                Assert.That(
                    simulatedBuildFailure?.Message,
                    Is.EqualTo("simulated macOS build failure"),
                    "The outer finally must restore state without swallowing the build failure.");
                Assert.That(
                    PlayerSettings.GetArchitecture(
                        BuildTargetGroup.Standalone),
                    Is.EqualTo(originalArchitecture));
                Assert.That(
                    File.Exists(ProjectPath(
                        MacArchitectureRestoreMarkerPath)),
                    Is.False);
                CollectionAssert.AreEqual(
                    originalProjectSettings,
                    File.ReadAllBytes(projectSettingsPath),
                    "The explicit macOS build failure path must preserve ProjectSettings.asset exactly.");

                begin.Invoke(null, null);
                ownsArchitecture.SetValue(null, false);
                hasOriginalArchitecture.SetValue(null, false);
                originalArchitectureState.SetValue(null, 0);
                recover.Invoke(null, null);
                Assert.That(
                    PlayerSettings.GetArchitecture(
                        BuildTargetGroup.Standalone),
                    Is.EqualTo(originalArchitecture),
                    "The next editor initialization must recover an abandoned universal override.");
                Assert.That(
                    File.Exists(ProjectPath(
                        MacArchitectureRestoreMarkerPath)),
                    Is.False);
                CollectionAssert.AreEqual(
                    originalProjectSettings,
                    File.ReadAllBytes(projectSettingsPath),
                    "Initialization recovery must preserve ProjectSettings.asset exactly.");
                AssetDatabase.SaveAssets();
                CollectionAssert.AreEqual(
                    originalProjectSettings,
                    File.ReadAllBytes(projectSettingsPath),
                    "Recovery must leave ProjectSettings clean before editor exit.");
            }
            finally
            {
                try
                {
                    restore?.Invoke(null, null);
                }
                finally
                {
                    try
                    {
                        if (PlayerSettings.GetArchitecture(
                                BuildTargetGroup.Standalone) !=
                            originalArchitecture)
                            PlayerSettings.SetArchitecture(
                                BuildTargetGroup.Standalone,
                                originalArchitecture);
                    }
                    finally
                    {
                        File.WriteAllBytes(
                            projectSettingsPath,
                            originalProjectSettings);
                        AssetDatabase.Refresh(
                            ImportAssetOptions.ForceSynchronousImport |
                            ImportAssetOptions.ForceUpdate);
                    }
                }
            }
        }

        [Test]
        public void Bug0005_PlayerBuildScope_RegistersGrayboxUrpOnlyFor3DBuildsAndRestores()
        {
            Type scope = FindLoadedType(RenderPipelineBuildScopeTypeName);
            Assert.That(scope, Is.Not.Null,
                "BUG-0005 requires a generic player-build callback so direct macOS builds retain URP shader variants.");
            Assert.That(typeof(BuildPlayerProcessor).IsAssignableFrom(scope),
                Is.True);
            Assert.That(typeof(IPostprocessBuildWithReport).IsAssignableFrom(scope),
                Is.True);

            MethodInfo requires = scope.GetMethod(
                "RequiresGrayboxPipeline",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo begin = scope.GetMethod(
                "BeginForScenes",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo restore = scope.GetMethod(
                "RestoreAfterBuild",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo restoreAfterPostprocess = scope.GetMethod(
                "RestoreAfterPostprocessBuild",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo qualityParser = scope.GetMethod(
                "ParseCurrentQualityAntiAliasingForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo recover = scope.GetMethod(
                "RecoverAbandonedBuild",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(requires, Is.Not.Null);
            Assert.That(begin, Is.Not.Null);
            Assert.That(restore, Is.Not.Null);
            Assert.That(restoreAfterPostprocess, Is.Not.Null);
            Assert.That(qualityParser, Is.Not.Null);
            Assert.That(recover, Is.Not.Null);

            Assert.That(
                requires.Invoke(null, new object[]
                {
                    new[] { GrayboxScenePath, RetiredFormalScenePath }
                }),
                Is.EqualTo(true));
            Assert.That(
                requires.Invoke(null, new object[]
                {
                    new[] { RetiredFormalScenePath }
                }),
                Is.EqualTo(false));

            UniversalRenderPipelineAsset approved =
                UnityEditor.AssetDatabase.LoadAssetAtPath<
                    UniversalRenderPipelineAsset>(GrayboxPipelinePath);
            RenderPipelineAsset original =
                GraphicsSettings.defaultRenderPipeline;
            int serializedAntiAliasing = (int)qualityParser.Invoke(
                null,
                new object[]
                {
                    File.ReadAllText(ProjectPath(QualitySettingsPath))
                });
            Assert.That(serializedAntiAliasing, Is.EqualTo(2),
                "The committed current quality level is the AA=2 restore authority.");
            const int simulatedBatchAntiAliasing = 0;
            string[] protectedPaths =
            {
                ProjectPath(GrayboxPipelinePath),
                ProjectPath(GraphicsSettingsPath),
                ProjectPath(QualitySettingsPath),
                ProjectPath(PlayerSettingsPath)
            };
            var originalBytes = new Dictionary<string, byte[]>();
            foreach (string protectedPath in protectedPaths)
                originalBytes.Add(
                    protectedPath,
                    File.ReadAllBytes(protectedPath));
            Assert.That(approved, Is.Not.Null);
            try
            {
                QualitySettings.antiAliasing = simulatedBatchAntiAliasing;
                Assert.That(
                    begin.Invoke(null, new object[]
                    {
                        new[] { GrayboxScenePath }
                    }),
                    Is.EqualTo(true));
                Assert.That(
                    GraphicsSettings.defaultRenderPipeline,
                    Is.SameAs(approved));
                Assert.That(
                    File.Exists(ProjectPath(GraphicsSettingsBackupPath)),
                    Is.True,
                    "BUG-0005 must preserve the exact pre-build GraphicsSettings bytes.");
                Assert.That(
                    File.Exists(ProjectPath(QualitySettingsBackupPath)),
                    Is.True,
                    "BUG-0005 must preserve the exact pre-build QualitySettings bytes.");
                foreach (string protectedPath in protectedPaths)
                    File.AppendAllText(
                        protectedPath,
                        "\n# BUG-0005 simulated build mutation\n");
                QualitySettings.antiAliasing = simulatedBatchAntiAliasing;
                Assert.That(
                    begin.Invoke(null, new object[]
                    {
                        new[] { GrayboxScenePath }
                    }),
                    Is.EqualTo(true),
                    "Repeated build preparation must not replace the original byte backups.");
                StringAssert.Contains(
                    "antiAliasing=" + serializedAntiAliasing,
                    File.ReadAllText(ProjectPath(RestoreMarkerPath)),
                    "The marker must record serialized current-quality AA instead of batch runtime AA.");

                restoreAfterPostprocess.Invoke(null, null);
                AssertProtectedFilesMatch(
                    protectedPaths,
                    originalBytes,
                    "Postprocess/finally restoration");
                Assert.That(
                    File.Exists(ProjectPath(RestoreMarkerPath)),
                    Is.False);
                Assert.That(
                    File.Exists(ProjectPath(GrayboxPipelineBackupPath)),
                    Is.False);
                Assert.That(
                    File.Exists(ProjectPath(GraphicsSettingsBackupPath)),
                    Is.False);
                Assert.That(
                    File.Exists(ProjectPath(QualitySettingsBackupPath)),
                    Is.False);
                Assert.That(
                    GraphicsSettings.defaultRenderPipeline,
                    Is.SameAs(original));
                Assert.That(
                    QualitySettings.antiAliasing,
                    Is.EqualTo(serializedAntiAliasing),
                    "The backup's serialized current-quality AA must replace the batch runtime value before exact file restoration.");
                AssetDatabase.SaveAssets();
                AssertProtectedFilesMatch(
                    protectedPaths,
                    originalBytes,
                    "Simulated editor-exit settings save");

                Assert.That(
                    begin.Invoke(null, new object[]
                    {
                        new[] { RetiredFormalScenePath }
                    }),
                    Is.EqualTo(false));
                Assert.That(
                    GraphicsSettings.defaultRenderPipeline,
                    Is.SameAs(original));

                Assert.That(
                    begin.Invoke(null, new object[]
                    {
                        new[] { GrayboxScenePath }
                    }),
                    Is.EqualTo(true));
                foreach (string protectedPath in protectedPaths)
                    File.AppendAllText(
                        protectedPath,
                        "\n# BUG-0005 simulated interrupted build mutation\n");
                QualitySettings.antiAliasing = simulatedBatchAntiAliasing;
                recover.Invoke(null, null);
                AssertProtectedFilesMatch(
                    protectedPaths,
                    originalBytes,
                    "Interrupted-build recovery");
                Assert.That(
                    GraphicsSettings.defaultRenderPipeline,
                    Is.SameAs(original),
                    "A stale build override must recover on the next editor initialization.");
                Assert.That(
                    QualitySettings.antiAliasing,
                    Is.EqualTo(serializedAntiAliasing));
                AssetDatabase.SaveAssets();
                AssertProtectedFilesMatch(
                    protectedPaths,
                    originalBytes,
                    "Interrupted-build recovery followed by editor-exit settings save");
            }
            finally
            {
                restore?.Invoke(null, null);
                GraphicsSettings.defaultRenderPipeline = original;
                QualitySettings.antiAliasing = serializedAntiAliasing;
                AssetDatabase.SaveAssets();
                foreach (KeyValuePair<string, byte[]> snapshot in originalBytes)
                    File.WriteAllBytes(snapshot.Key, snapshot.Value);
            }
        }

        private static void AssertProtectedFilesMatch(
            IEnumerable<string> protectedPaths,
            IReadOnlyDictionary<string, byte[]> originalBytes,
            string restorationPath)
        {
            foreach (string protectedPath in protectedPaths)
                CollectionAssert.AreEqual(
                    originalBytes[protectedPath],
                    File.ReadAllBytes(protectedPath),
                    restorationPath + " must restore exact bytes for " +
                    protectedPath);
        }

        private static void AssertQualityParserRejects(
            MethodInfo parser,
            string yaml)
        {
            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => parser.Invoke(null, new object[] { yaml }));
            Assert.That(
                exception?.InnerException,
                Is.InstanceOf<BuildFailedException>());
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        [Test]
        public void PerformanceProbe_ExposesFiveRunWorldGenerationEntryPoint()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);
            MethodInfo method = probe?.GetMethod(
                "MeasureWorldGeneration",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);
        }

        [Test]
        public void PerformanceProbe_ExposesFiveRunBuildingEntryPoint()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);
            MethodInfo method = probe?.GetMethod(
                "MeasureBuildingPerformance",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxPerformanceProbe.cs"));
            StringAssert.Contains(
                "WASTECITY_BUILDING_PERF_RESULT",
                source);
            StringAssert.Contains("MeasureBuildingPerformance", source);
            StringAssert.Contains("GrayboxSceneBootstrap.WorldSeedValue", source);
            StringAssert.Contains("BuildingInstanceCount", source);
        }

        [Test]
        public void PerformanceProbe_ExposesExternalGuiProfilerSummaryEntryPoint()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);
            MethodInfo method = probe?.GetMethod(
                "SummarizeGuiProfilerCapture",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxPerformanceProbe.cs"));
            StringAssert.Contains(
                "WASTECITY_GUI_PROFILER_INPUT",
                source);
            StringAssert.Contains(
                "WASTECITY_GUI_PROFILER_RESULT",
                source);
            StringAssert.Contains("LoadProfile", source);
            StringAssert.Contains("RawFrameDataView", source);
        }

        [Test]
        public void PerformanceProbe_ExposesFormalSavePersistenceEntryPointAndFiveMarkers()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);
            MethodInfo method = probe?.GetMethod(
                "MeasureFormalSavePersistencePerformance",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);

            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "_Game/Editor/GrayboxPerformanceProbe.cs"));
            StringAssert.Contains(
                FormalSavePerformanceResultEnvironmentVariable,
                source);
            CollectionAssert.AreEqual(
                FormalSavePerformanceMarkerNames,
                ExtractFormalSavePerformanceMarkerNames(source));
            StringAssert.Contains(
                "FormalSaveSnapshotAllocationBudgetBytes",
                source);
            StringAssert.Contains(
                "FormalSaveFileTransactionAllocationBudgetBytes",
                source);
            StringAssert.Contains(
                "GrayboxFormalSaveCoordinator3D",
                source);
            StringAssert.Contains("CaptureEnvelope(", source);
            StringAssert.Contains("FormalSaveValidator", source);
            StringAssert.Contains("FormalSaveFileTransaction", source);
            StringAssert.Contains(
                "GrayboxFormalSaveRuntimeHost3D",
                source);
        }

        [Test]
        public void FormalSavePerformanceProbe_RecordsCaptureAllocationTransactionAndIdleCallbackBudgets()
        {
            FormalSavePerformanceResult result =
                InvokeFormalSavePerformanceProbe();

            Assert.That(
                result.coordinatorCaptureCount,
                Is.EqualTo(FormalSaveCaptureSampleCount));
            Assert.That(
                result.successfulCaptureCount,
                Is.EqualTo(FormalSaveCaptureSampleCount));
            AssertStableCaptureHashes(result.capturePayloadHashes);
            Assert.That(result.fullSnapshotCount, Is.EqualTo(1));
            Assert.That(
                result.snapshotAllocationBudgetBytes,
                Is.EqualTo(FormalSaveSnapshotAllocationBudgetBytes));
            Assert.That(
                result.snapshotMeasuredAllocationBytes,
                Is.EqualTo(Math.Max(
                    result.snapshotManagedAllocationBytes,
                    result.snapshotProfiledAllocationBytes)));
            Assert.That(
                result.snapshotMeasuredAllocationBytes,
                Is.LessThanOrEqualTo(
                    result.snapshotAllocationBudgetBytes));

            Assert.That(result.fileTransactionCount, Is.EqualTo(1));
            Assert.That(
                result.fileTransactionAllocationBudgetBytes,
                Is.EqualTo(
                    FormalSaveFileTransactionAllocationBudgetBytes));
            Assert.That(
                result.fileTransactionMeasuredAllocationBytes,
                Is.EqualTo(Math.Max(
                    result.fileTransactionManagedAllocationBytes,
                    result.fileTransactionProfiledAllocationBytes)));
            Assert.That(
                result.fileTransactionMeasuredAllocationBytes,
                Is.LessThanOrEqualTo(
                    result.fileTransactionAllocationBudgetBytes));

            Assert.That(
                result.idleCallbackCount,
                Is.EqualTo(FormalSaveIdleCallbackCount));
            Assert.That(result.idlePendingCheckpointCount, Is.Zero);
            Assert.That(result.idleFileWriteCount, Is.Zero);
            Assert.That(
                result.idlePersistentObjectCountAfter,
                Is.EqualTo(result.idlePersistentObjectCountBefore));
            CollectionAssert.AreEqual(
                FormalSavePerformanceMarkerNames,
                result.markerNames);
        }

        [Test]
        public void MixedBuildingPopulation_StaysWithinStructuralBudgets()
        {
            BuildingPerformanceFixture fixture =
                CreateBuildingPerformanceFixture();

            int completed = 0;
            int construction = 0;
            int ruins = 0;
            for (var index = 0;
                 index < fixture.Session.Instances.Count;
                 index++)
            {
                switch (fixture.Session.Instances[index].State)
                {
                    case GrayboxBuildingInstanceState.Completed:
                        completed++;
                        break;
                    case GrayboxBuildingInstanceState.UnderConstruction:
                        construction++;
                        break;
                    case GrayboxBuildingInstanceState.AbandonedRuin:
                        ruins++;
                        break;
                }
            }

            int persistentObjectCount =
                fixture.Root.GetComponentsInChildren<Transform>(true).Length;
            int catalogObjectCount = 0;
            Transform[] transforms =
                fixture.Root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
                if (transforms[index].name.StartsWith(
                        "Catalog.Card.",
                        StringComparison.Ordinal))
                    catalogObjectCount++;

            TestContext.WriteLine(
                "BuildingInstanceCount=" +
                fixture.Session.Instances.Count);
            TestContext.WriteLine(
                "BuildingStateCounts=" + completed + "/" +
                construction + "/" + ruins);
            TestContext.WriteLine(
                "BuildingInstanceRendererCount=" +
                fixture.Presentation.InstanceRendererCount);
            TestContext.WriteLine(
                "BuildingInfrastructureRendererCount=" +
                fixture.Presentation.InfrastructureRendererCount);
            TestContext.WriteLine(
                "BuildingPersistentObjectCount=" + persistentObjectCount);
            TestContext.WriteLine(
                "PrecreatedCatalogObjectCount=" + catalogObjectCount);

            Assert.That(
                fixture.Session.Instances.Count,
                Is.EqualTo(BuildingInstanceCount));
            Assert.That(completed, Is.GreaterThan(0));
            Assert.That(construction, Is.GreaterThan(0));
            Assert.That(ruins, Is.GreaterThan(0));
            Assert.That(
                fixture.Presentation.InstanceRendererCount,
                Is.LessThanOrEqualTo(BuildingInstanceCount));
            Assert.That(
                fixture.Presentation.InfrastructureRendererCount,
                Is.LessThanOrEqualTo(8));
            Assert.That(persistentObjectCount, Is.LessThan(32 * 24));
            Assert.That(catalogObjectCount, Is.Zero);
        }

        [Test]
        public void BuildingAdapters_AllocateNoManagedBytesAcross300Calls()
        {
            BuildingPerformanceFixture fixture =
                CreateBuildingPerformanceFixture();
            fixture.TickAll();
            fixture.TickAll();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 300; frame++)
                fixture.TickAll();
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "BuildingAdapterAllocationDifference=" + difference);
            Assert.That(difference, Is.Zero);
        }

        [Test]
        public void GeneratedWorld_StaysWithinStructuralBudgets()
        {
            int rendererCountBefore =
                UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Length;
            GrayboxWorldView3D view = CreateWorldView();

            view.Generate(
                new WorldMapModel(
                    GrayboxSceneBootstrap.WorldWidth,
                    GrayboxSceneBootstrap.WorldHeight,
                    new WorldSeed(
                        GrayboxSceneBootstrap.WorldSeedValue)));

            int generatedRendererCount =
                UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Length -
                rendererCountBefore;
            TestContext.WriteLine(
                "WorldRendererCount=" + view.WorldRendererCount);
            TestContext.WriteLine(
                "PersistentGeneratedObjectCount=" +
                view.PersistentGeneratedObjectCount);
            TestContext.WriteLine(
                "GeneratedMeshRendererCount=" +
                generatedRendererCount);
            Assert.That(view.WorldRendererCount, Is.LessThanOrEqualTo(16));
            Assert.That(
                view.PersistentGeneratedObjectCount,
                Is.LessThanOrEqualTo(16));
            Assert.That(
                generatedRendererCount,
                Is.EqualTo(view.TotalGeneratedRendererCount));
            Assert.That(generatedRendererCount, Is.LessThan(64 * 24));
        }

        [Test]
        public void AdapterTicks_AllocateNoManagedBytesAcross300Calls()
        {
            AdapterFixture fixture = CreateAdapterFixture();
            fixture.TickAll();
            fixture.TickAll();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 300; frame++)
                fixture.TickAll();
            long difference =
                GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "Task9AdapterAllocationDifference=" + difference);
            Assert.That(difference, Is.Zero);
        }

        private AdapterFixture CreateAdapterFixture()
        {
            GrayboxWorldView3D world = CreateWorldView();
            world.Generate(
                new WorldMapModel(
                    GrayboxSceneBootstrap.WorldWidth,
                    GrayboxSceneBootstrap.WorldHeight,
                    new WorldSeed(
                        GrayboxSceneBootstrap.WorldSeedValue)));

            Material material = Track(CreateTestMaterial());
            var cityObject = Track(new GameObject("MobileCity"));
            world.Coordinates.TryCellToWorld(
                8,
                7,
                .5f,
                out Vector3 cityPosition);
            cityObject.transform.position = cityPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider bodyCollider =
                cityObject.AddComponent<BoxCollider>();
            var cityVisual = new GameObject("Visual");
            cityVisual.transform.SetParent(cityObject.transform, false);
            MeshRenderer cityRenderer =
                cityVisual.AddComponent<MeshRenderer>();
            cityRenderer.sharedMaterial = material;
            GrayboxVisualSlot citySlot =
                cityVisual.AddComponent<GrayboxVisualSlot>();
            citySlot.Configure(
                "core.city.mobile",
                cityRenderer,
                new Color(.9f, .48f, .1f));
            citySlot.ApplyFallback(material);
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, bodyCollider);

            var leaderObject = Track(new GameObject("Leader_CenJin"));
            leaderObject.transform.position =
                cityPosition + new Vector3(1.8f, .5f, 1.2f);
            GrayboxLeaderController3D leader =
                leaderObject.AddComponent<GrayboxLeaderController3D>();
            leader.Configure(world, city, true);

            var directObject = Track(new GameObject("DirectControl"));
            GrayboxDirectControlCoordinator directControl =
                directObject.AddComponent<
                    GrayboxDirectControlCoordinator>();
            directControl.Configure(city, leader);

            var rigObject = Track(new GameObject("CameraRig"));
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(rigObject.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.localPosition = new Vector3(0f, 18f, -14f);
            camera.transform.localEulerAngles =
                new Vector3(52f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 13f;

            var projectorObject =
                Track(new GameObject("GroundProjector"));
            GrayboxGroundProjector projector =
                projectorObject.AddComponent<GrayboxGroundProjector>();
            projector.Configure(camera, world.Coordinates);

            var cameraControllerObject =
                Track(new GameObject("CameraController"));
            GrayboxCameraController3D cameraController =
                cameraControllerObject.AddComponent<
                    GrayboxCameraController3D>();
            cameraController.Configure(
                camera,
                rigObject.transform,
                city,
                leader,
                directControl,
                projector);

            var routerObject = Track(new GameObject("InputRouter"));
            GrayboxInputRouter router =
                routerObject.AddComponent<GrayboxInputRouter>();
            router.Configure(
                city,
                leader,
                directControl,
                projector,
                cameraController);

            return new AdapterFixture(
                city,
                directControl,
                router,
                cameraController);
        }

        private BuildingPerformanceFixture
            CreateBuildingPerformanceFixture()
        {
            GrayboxWorldView3D world = CreateWorldView();
            world.Generate(
                new WorldMapModel(
                    GrayboxSceneBootstrap.WorldWidth,
                    GrayboxSceneBootstrap.WorldHeight,
                    new WorldSeed(
                        GrayboxSceneBootstrap.WorldSeedValue)));

            Material material = Track(CreateTestMaterial());
            var root = Track(new GameObject("BuildingPerformanceRoot"));
            var cityObject = new GameObject("MobileCity");
            cityObject.transform.SetParent(root.transform, false);
            world.Coordinates.TryCellToWorld(
                16,
                12,
                .5f,
                out Vector3 cityPosition);
            cityObject.transform.position = cityPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider bodyCollider = cityObject.AddComponent<BoxCollider>();
            var cityVisual = new GameObject("Visual");
            cityVisual.transform.SetParent(cityObject.transform, false);
            MeshRenderer cityRenderer =
                cityVisual.AddComponent<MeshRenderer>();
            cityRenderer.sharedMaterial = material;
            GrayboxVisualSlot citySlot =
                cityVisual.AddComponent<GrayboxVisualSlot>();
            citySlot.Configure(
                "core.city.mobile",
                cityRenderer,
                new Color(.9f, .48f, .1f));
            citySlot.ApplyFallback(material);
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, bodyCollider);
            Assert.That(
                city.RestoreDeploymentForDevelopment(CityMode.Fortress),
                Is.True);

            var sessionObject = new GameObject("BuildingSession");
            sessionObject.transform.SetParent(root.transform, false);
            GrayboxBuildingSession3D session =
                sessionObject.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            session.Inventory.Set(ResourceIds.Stone, 5000);

            Transform instanceRoot =
                NewChild(root.transform, "InstanceRoot");
            Transform infrastructureRoot =
                NewChild(root.transform, "InfrastructureRoot");
            var presentationObject = new GameObject("BuildingPresentation");
            presentationObject.transform.SetParent(root.transform, false);
            GrayboxBuildingWorldView3D presentation =
                presentationObject.AddComponent<
                    GrayboxBuildingWorldView3D>();
            presentation.Configure(
                instanceRoot,
                infrastructureRoot,
                material,
                city);

            var cells = new List<BuildingCell>(BuildingInstanceCount);
            for (var y = 4;
                 y <= 20 && cells.Count < BuildingInstanceCount;
                 y++)
            {
                for (var x = 8;
                     x <= 24 && cells.Count < BuildingInstanceCount;
                     x++)
                {
                    if (Math.Abs(x - 16) <= 1 &&
                        Math.Abs(y - 12) <= 1)
                        continue;
                    cells.Add(new BuildingCell(x, y));
                }
            }
            Assert.That(cells.Count, Is.EqualTo(BuildingInstanceCount));

            const int completedCount = 43;
            const int constructionCount = 43;
            for (var index = 0; index < completedCount; index++)
                BeginWall(
                    session,
                    presentation,
                    cells[index].X,
                    cells[index].Y);
            session.TickConstruction(
                10f,
                CityMode.Fortress,
                false,
                presentation);

            for (var index = completedCount;
                 index < completedCount + constructionCount;
                 index++)
                BeginWall(
                    session,
                    presentation,
                    cells[index].X,
                    cells[index].Y);

            for (var index = completedCount + constructionCount;
                 index < BuildingInstanceCount;
                 index++)
            {
                GrayboxBuildingInstance3D ruin = BeginWall(
                    session,
                    presentation,
                    cells[index].X,
                    cells[index].Y);
                BuildingEvacuationWork work =
                    BuildingEvacuationRules.Create(
                        ruin.StableInstanceId,
                        ruin.Placement.Definition.Cost,
                        ruin.Progress.BaseDuration,
                        1d,
                        BuildingEvacuationTreatment.Abandon);
                Assert.That(
                    session.TryCaptureEvacuationWork(
                        new[] { work },
                        out string captureFailure),
                    Is.True,
                    captureFailure);
                Assert.That(
                    session.TryCommitEvacuation(
                        work,
                        presentation,
                        out _,
                        out string commitFailure),
                    Is.True,
                    commitFailure);
            }

            var menuObject = new GameObject("BuildingMenu");
            menuObject.transform.SetParent(root.transform, false);
            GrayboxBuildingMenuView3D menu =
                menuObject.AddComponent<GrayboxBuildingMenuView3D>();
            var interactionObject = new GameObject("BuildingInteraction");
            interactionObject.transform.SetParent(root.transform, false);
            GrayboxBuildingInteractionModel3D interaction =
                interactionObject.AddComponent<
                    GrayboxBuildingInteractionModel3D>();
            var evacuationObject = new GameObject("Evacuation");
            evacuationObject.transform.SetParent(root.transform, false);
            GrayboxEvacuationController3D evacuation =
                evacuationObject.AddComponent<
                    GrayboxEvacuationController3D>();
            evacuation.Configure(
                session,
                city,
                presentation,
                menu);
            var inputObject = new GameObject("BuildingInput");
            inputObject.transform.SetParent(root.transform, false);
            GrayboxBuildingInputRouter3D input =
                inputObject.AddComponent<GrayboxBuildingInputRouter3D>();
            input.Configure(
                menu,
                interaction,
                null,
                null,
                evacuation,
                null);

            return new BuildingPerformanceFixture(
                root,
                session,
                presentation,
                evacuation,
                input);
        }

        private static GrayboxBuildingInstance3D BeginWall(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation,
            int x,
            int y)
        {
            BuildingDefinition definition = BuildingCatalog.Wall;
            BuildingUnlockEvaluation unlock =
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                16,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
                true,
                false,
                true,
                true,
                true,
                null,
                true,
                unlock,
                session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost));
            Assert.That(
                session.TryBeginConstruction(
                    request,
                    presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private GrayboxWorldView3D CreateWorldView()
        {
            var root = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Material material = Track(CreateTestMaterial());
            GrayboxWorldView3D view =
                root.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            return view;
        }

        private static Material CreateTestMaterial()
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        private static Transform NewChild(
            Transform parent,
            string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static string ExtractMethodBlock(
            string source,
            string methodName)
        {
            string signature = "void " + methodName + "()";
            int start = source.IndexOf(
                signature,
                StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int openingBrace = source.IndexOf('{', start);
            Assert.That(openingBrace, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{')
                    depth++;
                else if (source[index] == '}')
                    depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }

            throw new AssertionException(
                "Unbalanced method block for " + methodName + ".");
        }

        private static string[] ExtractFormalSavePerformanceMarkerNames(
            string source)
        {
            const string declaration =
                "FormalSavePersistenceMarkerNames";
            int declarationStart = source.IndexOf(
                declaration,
                StringComparison.Ordinal);
            Assert.That(
                declarationStart,
                Is.GreaterThanOrEqualTo(0),
                declaration);
            int openingBrace = source.IndexOf('{', declarationStart);
            int closingBrace = source.IndexOf("};", openingBrace,
                StringComparison.Ordinal);
            Assert.That(openingBrace, Is.GreaterThanOrEqualTo(0));
            Assert.That(closingBrace, Is.GreaterThan(openingBrace));

            var names = new List<string>();
            int cursor = openingBrace + 1;
            while (cursor < closingBrace)
            {
                int openingQuote = source.IndexOf('"', cursor);
                if (openingQuote < 0 || openingQuote >= closingBrace)
                    break;
                int closingQuote = source.IndexOf('"', openingQuote + 1);
                Assert.That(
                    closingQuote,
                    Is.GreaterThan(openingQuote));
                names.Add(source.Substring(
                    openingQuote + 1,
                    closingQuote - openingQuote - 1));
                cursor = closingQuote + 1;
            }
            return names.ToArray();
        }

        private static FormalSavePerformanceResult
            InvokeFormalSavePerformanceProbe()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            Assert.That(probe, Is.Not.Null);
            MethodInfo method = probe?.GetMethod(
                "MeasureFormalSavePersistencePerformance",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            string resultPath = Path.Combine(
                Path.GetTempPath(),
                "wastecity-task13-formal-save-performance-" +
                Guid.NewGuid().ToString("N") + ".json");
            string previous = Environment.GetEnvironmentVariable(
                FormalSavePerformanceResultEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    FormalSavePerformanceResultEnvironmentVariable,
                    resultPath);
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
                try
                {
                    method?.Invoke(null, null);
                }
                catch (TargetInvocationException exception)
                {
                    Assert.Fail(
                        "Formal save performance probe failed before " +
                        "producing Task 13 evidence: " +
                        (exception.InnerException?.Message ??
                         exception.Message));
                }

                Assert.That(
                    File.Exists(resultPath),
                    Is.True,
                    "Formal save performance evidence was not written.");
                FormalSavePerformanceResult result =
                    JsonUtility.FromJson<FormalSavePerformanceResult>(
                        File.ReadAllText(resultPath));
                Assert.That(result, Is.Not.Null);
                return result;
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    FormalSavePerformanceResultEnvironmentVariable,
                    previous);
                if (File.Exists(resultPath))
                    File.Delete(resultPath);
            }
        }

        private static void AssertStableCaptureHashes(string[] hashes)
        {
            Assert.That(hashes, Is.Not.Null);
            Assert.That(
                hashes.Length,
                Is.EqualTo(FormalSaveCaptureSampleCount));
            string expected = hashes[0];
            Assert.That(expected, Is.Not.Null.And.Not.Empty);
            for (int index = 1; index < hashes.Length; index++)
            {
                Assert.That(
                    hashes[index],
                    Is.EqualTo(expected),
                    "Capture payload hash changed at sample " + index + ".");
            }
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        [Serializable]
        private sealed class FormalSavePerformanceResult
        {
            public int coordinatorCaptureCount;
            public int successfulCaptureCount;
            public string[] capturePayloadHashes;
            public int fullSnapshotCount;
            public long snapshotManagedAllocationBytes;
            public long snapshotProfiledAllocationBytes;
            public long snapshotMeasuredAllocationBytes;
            public long snapshotAllocationBudgetBytes;
            public int fileTransactionCount;
            public long fileTransactionManagedAllocationBytes;
            public long fileTransactionProfiledAllocationBytes;
            public long fileTransactionMeasuredAllocationBytes;
            public long fileTransactionAllocationBudgetBytes;
            public int idleCallbackCount;
            public int idlePendingCheckpointCount;
            public int idleFileWriteCount;
            public int idlePersistentObjectCountBefore;
            public int idlePersistentObjectCountAfter;
            public string[] markerNames;
        }

        private sealed class AdapterFixture
        {
            private readonly GrayboxMobileCityController3D city;
            private readonly GrayboxDirectControlCoordinator directControl;
            private readonly GrayboxInputRouter router;
            private readonly GrayboxCameraController3D cameraController;
            private readonly GrayboxInputFrame inputFrame =
                new GrayboxInputFrame(
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false);

            public AdapterFixture(
                GrayboxMobileCityController3D city,
                GrayboxDirectControlCoordinator directControl,
                GrayboxInputRouter router,
                GrayboxCameraController3D cameraController)
            {
                this.city = city;
                this.directControl = directControl;
                this.router = router;
                this.cameraController = cameraController;
            }

            public void TickAll()
            {
                city.TickMovement(.02f);
                city.TickDeployment(.016f);
                directControl.Refresh();
                router.ProcessFrame(inputFrame);
                router.TickGameplay(.016f);
                cameraController.TickCamera();
            }
        }

        private sealed class BuildingPerformanceFixture
        {
            public BuildingPerformanceFixture(
                GameObject root,
                GrayboxBuildingSession3D session,
                GrayboxBuildingWorldView3D presentation,
                GrayboxEvacuationController3D evacuation,
                GrayboxBuildingInputRouter3D input)
            {
                Root = root;
                Session = session;
                Presentation = presentation;
                Evacuation = evacuation;
                Input = input;
            }

            public GameObject Root { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public GrayboxEvacuationController3D Evacuation { get; }
            public GrayboxBuildingInputRouter3D Input { get; }

            public void TickAll()
            {
                Input.ProcessCurrentInput();
                Session.TickConstruction(
                    .016f,
                    CityMode.Mobile,
                    false,
                    Presentation);
                Evacuation.Tick(.016f, false);
            }
        }
    }
}
