using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Assert.That(snapshot.TestClasses.Any(record =>
                record.FullName == "WasteCity.Tests.GrayboxBuildingRuntimeSceneTests" &&
                record.Platform == ProjectTestPlatform.PlayMode), Is.True);
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
        public void Scan_RejectsAmbiguousCurrentProjectTestClassSourceMapping()
        {
            string path = Path.Combine(ProjectRoot(), "Assets/_Game/Tests/EditMode/zz_ProjectQualityScannerAmbiguousFixture.cs");
            try
            {
                File.WriteAllText(path, "// class ProjectQualityScannerTests\n");
                InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                    ProjectQualityScanner.Scan(ProjectRoot()));
                StringAssert.Contains("ProjectQualityScannerTests", error.Message);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void Scan_DoesNotWriteFixtureFiles()
        {
            WriteFixtureFile("Assets/_Game/Scripts/Generated/Stable.cs", "public sealed class Stable { }");
            WriteFixtureFile("Assets/_Game/Scripts/Fixture.Runtime.asmdef", "{\"name\":\"Fixture.Runtime\"}");
            Dictionary<string, byte[]> before = ReadFixtureFiles();

            ProjectQualityScanner.Scan(fixtureRoot);

            Dictionary<string, byte[]> after = ReadFixtureFiles();
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (string path in before.Keys)
                CollectionAssert.AreEqual(before[path], after[path], path);
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
    }
}
