using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Editor.ProjectQuality;

namespace WasteCity.Tests
{
    public sealed class ProjectQualityScannerTests
    {
        private string fixtureRoot;

        [SetUp]
        public void SetUp()
        {
            fixtureRoot = Path.Combine(Path.GetTempPath(), "WasteCityProjectQualityScanner", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(fixtureRoot))
                Directory.Delete(fixtureRoot, true);
        }

        [Test]
        public void Scan_CurrentProjectFindsKnownAssembliesScenesTypesTestsAndEditorEntryPoints()
        {
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(ProjectRoot());

            RecordCounts(snapshot);

            CollectionAssert.Contains(snapshot.AssemblyNames, "WasteCity.Game");
            CollectionAssert.Contains(snapshot.AssemblyNames, "WasteCity.Editor");
            CollectionAssert.Contains(snapshot.ScenePaths, "Assets/_Game/Scenes/GrayboxPrototype3D.unity");
            Assert.That(snapshot.TypeRecords.Any(record =>
                record.FullName == "WasteCity.Graybox3D.GrayboxMobileCityController3D" &&
                record.Kind == ProjectTypeKind.MonoBehaviour), Is.True);
            Assert.That(snapshot.TypeRecords.Any(record =>
                record.FullName == "WasteCity.ArtIntegration3D.FirstArtTerrainProfile3D" &&
                record.Kind == ProjectTypeKind.ScriptableObject), Is.True);
            Assert.That(snapshot.TestClasses.Any(record =>
                record.FullName == "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests" &&
                record.Platform == ProjectTestPlatform.PlayMode), Is.True);
            Assert.That(snapshot.TestClasses.Single(record =>
                record.FullName == "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests").SourcePath,
                Is.EqualTo("Assets/_Game/Tests/PlayMode/GrayboxBuildingRuntimeSceneTests.cs"));
            Assert.That(snapshot.EditorEntryPoints.Any(record =>
                record.OwnerTypeFullName == "WasteCity.Editor.FormalBuildTools" &&
                record.MethodName == "BuildWindows"), Is.True);
            string[] entryPointOwners =
            {
                "FirstArtTerrainAssetBuilder",
                "FirstArtTerrainEvidenceCapture",
                "FormalBuildTools",
                "GrayboxPerformanceProbe",
                "GrayboxSceneAuthoring",
            };
            foreach (string owner in entryPointOwners)
                Assert.That(snapshot.EditorEntryPoints.Any(record =>
                    record.OwnerTypeFullName.EndsWith("." + owner, StringComparison.Ordinal)), Is.True, owner);

            CollectionAssert.AreEqual(snapshot.SceneRecords.Select(record => record.Path)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray(), snapshot.ScenePaths);
            Assert.That(snapshot.SceneRecords.Single(record =>
                record.Path == "Assets/_Game/Scenes/GrayboxPrototype3D.unity").BuildIndex, Is.EqualTo(0));
            Assert.That(snapshot.SceneRecords.Single(record =>
                record.Path == "Assets/_Game/Scenes/FormalPrototype.unity").BuildIndex, Is.EqualTo(1));
        }

        [Test]
        public void Scan_MapsEachProductionTypeInBuildingRuntimeSourceFile()
        {
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(ProjectRoot());
            const string source = "Assets/_Game/Scripts/Building/BuildingRuntime.cs";
            Assert.That(snapshot.TypeRecords.Single(x => x.FullName == "WasteCity.Building.BuildingRuntime").SourcePath, Is.EqualTo(source));
            Assert.That(snapshot.TypeRecords.Single(x => x.FullName == "WasteCity.Building.PlaceholderShieldGenerator").SourcePath, Is.EqualTo(source));
        }

        [Test]
        public void Scan_MapsCompiledPreprocessorProbeTestClass()
        {
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(ProjectRoot());
            Assert.That(snapshot.TestClasses.Single(x => x.FullName == "WasteCity.Tests.ProjectQualityPdbSourceMappingProbeTests").SourcePath,
                Is.EqualTo("Assets/_Game/Tests/EditMode/ProjectQualityScannerTests.cs"));
        }

        [Test]
        public void Scan_RepeatedRunProducesEqualOrderedSnapshot()
        {
            ProjectInventorySnapshot first = ProjectQualityScanner.Scan(ProjectRoot());
            ProjectInventorySnapshot second = ProjectQualityScanner.Scan(ProjectRoot());

            Assert.That(second.ToDeterministicJson(), Is.EqualTo(first.ToDeterministicJson()));
        }

        [Test]
        public void Scan_FixtureFindsGeneratedSourceExcludesMetaAndLibraryAndSortsPathsOrdinally()
        {
            WriteFixtureFile("Assets/_Game/Scripts/Generated/zeta.cs", "public sealed class Zeta { }");
            WriteFixtureFile("Assets/_Game/Scripts/Generated/Alpha.cs", "public sealed class Alpha { }");
            WriteFixtureFile("Assets/_Game/Editor/Tools/Beta.cs", "public sealed class Beta { }");
            WriteFixtureFile("Assets/_Game/Tests/EditMode/FixtureEditTests.cs", "public sealed class FixtureEditTests { }");
            WriteFixtureFile("Assets/_Game/Tests/PlayMode/FixturePlayTests.cs", "public sealed class FixturePlayTests { }");
            WriteFixtureFile("Assets/_Game/Scripts/Generated/Hidden.cs.meta", "not source");
            WriteFixtureFile("Library/Generated/ShouldNeverAppear.cs", "public sealed class Hidden { }");
            WriteFixtureFile("Assets/_Game/Scripts/Fixture.Runtime.asmdef", "{\"name\":\"Fixture.Runtime\"}");

            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(fixtureRoot);
            string[] paths = snapshot.FileRecords.Select(record => record.Path).ToArray();

            CollectionAssert.Contains(paths, "Assets/_Game/Scripts/Generated/Alpha.cs");
            CollectionAssert.Contains(paths, "Assets/_Game/Scripts/Generated/zeta.cs");
            CollectionAssert.DoesNotContain(paths, "Assets/_Game/Scripts/Generated/Hidden.cs.meta");
            Assert.That(paths.Any(path => path.IndexOf("Library", StringComparison.Ordinal) >= 0), Is.False);
            CollectionAssert.AreEqual(paths.OrderBy(path => path, StringComparer.Ordinal).ToArray(), paths);
            Assert.That(snapshot.AssemblyRecords.Single().Name, Is.EqualTo("Fixture.Runtime"));
        }

        [Test]
        public void Scan_RejectsInvalidProjectRootsAndMalformedAssemblyDefinitions()
        {
            Assert.That(() => ProjectQualityScanner.Scan(null), Throws.TypeOf<ArgumentException>());
            Assert.That(() => ProjectQualityScanner.Scan("Assets"), Throws.TypeOf<ArgumentException>());
            Assert.That(() => ProjectQualityScanner.Scan(Path.Combine(fixtureRoot, "missing")),
                Throws.TypeOf<DirectoryNotFoundException>());

            WriteFixtureFile("Assets/_Game/Scripts/Broken.asmdef", "{\"name\":");
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                ProjectQualityScanner.Scan(fixtureRoot));
            StringAssert.Contains("Broken.asmdef", error.Message);
        }

        [Test]
        public void Scan_RestrictsEditorEntriesToApprovedOwnersAndPublicStaticParameterlessMethods()
        {
            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(ProjectRoot());

            Assert.That(snapshot.EditorEntryPoints.Any(record =>
                record.OwnerTypeFullName == "WasteCity.Tests.FormalBuildTools"), Is.False);
            Assert.That(snapshot.EditorEntryPoints.Any(record =>
                record.OwnerTypeFullName == "WasteCity.Tests.GrayboxPerformanceProbe"), Is.False);
            foreach (ProjectEditorEntryPointRecord record in snapshot.EditorEntryPoints)
            {
                Type owner = Type.GetType(record.OwnerTypeFullName + ", " + "WasteCity.Editor");
                Assert.That(owner, Is.Not.Null, record.OwnerTypeFullName);
                MethodInfo method = owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Single(candidate => candidate.Name == record.MethodName && candidate.GetParameters().Length == 0);
                Assert.That(method.IsPublic && method.IsStatic && method.GetParameters().Length == 0, Is.True);
            }
        }

        [Test]
        public void TestDiscovery_RecognizesEachSupportedMethodAttribute()
        {
            Assert.That(ContainsTestMethod(typeof(TestAttributeFixture)), Is.True);
            Assert.That(ContainsTestMethod(typeof(TestCaseAttributeFixture)), Is.True);
            Assert.That(ContainsTestMethod(typeof(TestCaseSourceAttributeFixture)), Is.True);
            Assert.That(ContainsTestMethod(typeof(UnityTestAttributeFixture)), Is.True);
        }

        [Test]
        public void Scan_DoesNotWriteFixtureFiles()
        {
            WriteFixtureFile("Assets/_Game/Scripts/Generated/Stable.cs", "public sealed class Stable { }");
            WriteFixtureFile("Assets/_Game/Scripts/Fixture.Runtime.asmdef", "{\"name\":\"Fixture.Runtime\"}");
            Dictionary<string, byte[]> before = ReadFixtureFiles();

            ProjectInventorySnapshot snapshot = ProjectQualityScanner.Scan(fixtureRoot);
            snapshot.ToDeterministicJson();

            Dictionary<string, byte[]> after = ReadFixtureFiles();
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (string path in before.Keys)
                CollectionAssert.AreEqual(before[path], after[path], path);
        }

        [Test]
        public void ToDeterministicJson_EscapesControlCharactersAndIsCultureIndependent()
        {
            const string value = "a\"\\\n\r\t\u0001";
            const string escapedValue = "a\\\"\\\\\\n\\r\\t\\u0001";
            ProjectInventorySnapshot snapshot = EmptySnapshot();
            snapshot.FileRecords = new[] { new ProjectFileRecord { Path = value, Kind = ProjectFileKind.Production } };

            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                Assert.That(snapshot.ToDeterministicJson(), Is.EqualTo(
                    "{\"FileRecords\":[{\"Path\":\"" + escapedValue +
                    "\",\"Kind\":\"Production\"}],\"TypeRecords\":[],\"AssemblyRecords\":[],\"SceneRecords\":[],\"TestClasses\":[],\"EditorEntryPoints\":[],\"AssemblyNames\":[],\"ScenePaths\":[]}"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        [Test]
        public void ToDeterministicJson_RejectsIncompleteSnapshotState()
        {
            Assert.That(() => new ProjectInventorySnapshot().ToDeterministicJson(),
                Throws.TypeOf<InvalidOperationException>());
        }

        private void WriteFixtureFile(string relativePath, string content)
        {
            string path = Path.Combine(fixtureRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private Dictionary<string, byte[]> ReadFixtureFiles()
        {
            return Directory.GetFiles(fixtureRoot, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    path => path.Substring(fixtureRoot.Length + 1).Replace('\\', '/'),
                    File.ReadAllBytes,
                    StringComparer.Ordinal);
        }

        private static bool ContainsTestMethod(Type type)
        {
            MethodInfo method = typeof(ProjectQualityScanner).GetMethod("ContainsTestMethod",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)method.Invoke(null, new object[] { type });
        }

        private static ProjectInventorySnapshot EmptySnapshot()
        {
            return new ProjectInventorySnapshot
            {
                FileRecords = new ProjectFileRecord[0],
                TypeRecords = new ProjectTypeRecord[0],
                AssemblyRecords = new ProjectAssemblyRecord[0],
                SceneRecords = new ProjectSceneRecord[0],
                TestClasses = new ProjectTestClassRecord[0],
                EditorEntryPoints = new ProjectEditorEntryPointRecord[0],
                AssemblyNames = new string[0],
                ScenePaths = new string[0],
            };
        }

        private static void RecordCounts(ProjectInventorySnapshot snapshot)
        {
            TestContext.CurrentContext.Test.Properties.Set("ProjectQualityFileRecords", snapshot.FileRecords.Length);
            TestContext.CurrentContext.Test.Properties.Set("ProjectQualityTypeRecords", snapshot.TypeRecords.Length);
            TestContext.CurrentContext.Test.Properties.Set("ProjectQualityAssemblyRecords", snapshot.AssemblyRecords.Length);
            TestContext.CurrentContext.Test.Properties.Set("ProjectQualitySceneRecords", snapshot.SceneRecords.Length);
            TestContext.CurrentContext.Test.Properties.Set("ProjectQualityTestClasses", snapshot.TestClasses.Length);
            TestContext.CurrentContext.Test.Properties.Set("ProjectQualityEditorEntryPoints", snapshot.EditorEntryPoints.Length);
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private sealed class TestAttributeFixture
        {
            [Test] public void DiscoversTest() { }
        }

        private sealed class TestCaseAttributeFixture
        {
            [TestCase(1)] public void DiscoversTestCase(int value) { }
        }

        private sealed class TestCaseSourceAttributeFixture
        {
            private static IEnumerable Cases { get { return new[] { new TestCaseData(1) }; } }
            [TestCaseSource("Cases")] public void DiscoversTestCaseSource(int value) { }
        }

        private sealed class UnityTestAttributeFixture
        {
            [UnityEngine.TestTools.UnityTest] public IEnumerator DiscoversUnityTest() { yield break; }
        }
    }

    public static class FormalBuildTools
    {
        public static void FixturePublicStaticParameterless() { }
        private static void FixturePrivateStaticParameterless() { }
        public static void FixtureParameterized(int value) { }
    }

    public sealed class GrayboxPerformanceProbe
    {
        public void FixtureInstance() { }
        private static void FixturePrivateStaticParameterless() { }
        public static void FixtureParameterized(int value) { }
    }

#if true
    internal static class ProjectQualityScannerPreprocessorProbe
    {
        internal static readonly string Content = $@"{string.Concat("x")}
#if false
";
    }

    public sealed class ProjectQualityPdbSourceMappingProbeTests
    {
        [Test] public void CompiledProbeExists() { Assert.That(true, Is.True); }
    }
#endif
}
